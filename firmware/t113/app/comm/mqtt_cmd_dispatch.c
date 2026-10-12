#include "mqtt_cmd_dispatch.h"
#include "mqtt_write_guard.h"
#include <stdio.h>
#include <string.h>

#define HMI_CMD_BAD_ARG "bad-arg"

static void audit_denied(const hmi_cmd_io_t* io, const char* by, const char* target, const char* code)
{
    if (io->audit == NULL) return;
    io->audit("remote-write-denied", by, code);
    (void)target;
}

static int deny(const hmi_cmd_io_t* io, const char* by, const char* target,
                const char* code, hmi_cmd_result_t* out)
{
    out->code = code;
    out->target = target;
    out->wrote = 0;
    audit_denied(io, by, target, code);
    return -1;
}

int hmi_cmd_handle(const char* topic, const char* prefix, const char* payload,
                   unsigned long long now_ms, unsigned perm_mask,
                   hmi_mqtt_cmd_ledger_t* ledger,
                   const hmi_cmd_target_t* target, const hmi_cmd_io_t* io,
                   hmi_cmd_result_t* out)
{
    MqttCommand cmd;
    const char* reason = NULL;
    char detail[192];
    double new_value = 0.0;
    int write_result;

    if (out == NULL) return -1;
    memset(out, 0, sizeof(*out));

    /* ① ACL（方向 + 权限）—— 主体为 NULL（拒的时候还没有 by） */
    {
        const char* acl_reason = NULL;
        if (hmi_mqtt_acl_allows(topic, prefix, 1, perm_mask, &acl_reason) != 0) {
            /* ⚠ `-1` **必须**带一个非空码：`code == NULL` 在本契约里表示"成功"，
             *   返回 -1 却留 NULL 会让调用方 `strcmp(res.code, …)` 直接崩（负控首跑就是这么崩的）。 */
            const char* code = acl_reason != NULL ? acl_reason : HMI_MQTT_ACL_FORBIDDEN;
            if (io != NULL) audit_denied(io, NULL, NULL, code);
            out->code = code;
            return -1;
        }
    }

    if (ledger == NULL || io == NULL) {
        out->code = HMI_CMD_BAD_ARG;
        return -1;
    }

    /* ② 信封解析 */
    if (hmi_mqtt_cmd_parse_payload(payload, &cmd) != 0) {
        if (io->audit) io->audit("remote-write-denied", NULL, "malformed");
        out->code = "malformed";
        return -1;
    }

    out->target = cmd.target;

    /* ③ 新鲜度 */
    if (hmi_mqtt_cmd_is_fresh(cmd.issued_ms, cmd.ttl_ms, now_ms) != 0) {
        const char* code = cmd.issued_ms > now_ms ? "from-future" : "expired";
        return deny(io, cmd.by[0] ? cmd.by : NULL, cmd.target, code, out);
    }

    /* ④ target 存在性（元数据由宿主查好传进来） */
    if (target == NULL || target->target == NULL)
        return deny(io, cmd.by[0] ? cmd.by : NULL, cmd.target, HMI_CMD_DENIED_NOT_FOUND, out);

    /* ⑤ 只读 / 量程 */
    {
        int rc = hmi_mqtt_write_check(target->writable,
                                      target->has_range ? target->min_value : -1.0e308,
                                      target->has_range ? target->max_value : 1.0e308,
                                      cmd.value, &new_value, &reason);
        if (rc != HMI_MQTT_WRITE_OK) return deny(io, cmd.by[0] ? cmd.by : NULL, cmd.target, reason, out);
    }

    /* ⑥ 幂等台账（通过前五步才收 —— 被拒的命令不进台账） */
    {
        int accepted = hmi_mqtt_cmd_ledger_accept(ledger, cmd.id, now_ms, cmd.ttl_ms);
        if (accepted < 0) return deny(io, cmd.by[0] ? cmd.by : NULL, cmd.target, "ledger-rejected", out);
        if (accepted == 0) return deny(io, cmd.by[0] ? cmd.by : NULL, cmd.target, "duplicate", out);
        out->ledger_used = 1;
    }

    /* ⑦ 读旧值 → 写 */
    if (io->get_value != NULL) io->get_value(cmd.target, &out->old_value);
    write_result = io->set_value != NULL ? io->set_value(cmd.target, new_value) : -1;
    if (write_result != 0) return deny(io, cmd.by[0] ? cmd.by : NULL, cmd.target, HMI_CMD_DENIED_WRITE_FAILED, out);

    out->new_value = new_value;
    out->wrote = 1;
    out->code = HMI_CMD_OK;

    /* ⑧ 审计（成功那条带**旧值→新值**） */
    if (io->audit != NULL) {
        snprintf(detail, sizeof(detail), "target=%s old=%.6g new=%.6g",
                 cmd.target, out->old_value, new_value);
        io->audit("remote-write", cmd.by[0] ? cmd.by : NULL, detail);
    }
    return 0;
}
