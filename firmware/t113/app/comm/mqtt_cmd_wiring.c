/*
 * mqtt_cmd_wiring.c — 远程写命令的宿主接线（见 mqtt_cmd_wiring.h 的契约说明）
 *
 * 本文件**只做接线**：所有判定仍在 mqtt_acl_contract / mqtt_cmd_contract /
 * mqtt_write_guard / mqtt_cmd_ledger / mqtt_cmd_dispatch 里。这里只负责
 * 「收报文 → 查标签表拿元数据 → 补宿主能力与权限掩码 → 交给 hmi_cmd_handle」这一条线。
 */
#include "mqtt_cmd_wiring.h"
#include "mqtt_cmd_contract.h"
#include "mqtt_client.h"
#include "runtime_config.h"
#include "variable_table.h"
#include "hmi_actions.h"

#include <stdio.h>
#include <string.h>
#include <time.h>

/* 载荷上限：信封字段上限之和（id 64 + ts + ttl + by 64 + target 96 + value 96 + 分隔符）≈ 340，
 * 取 384 留余量。超长 ⇒ **不截断**（截断后的 target 可能指向另一个变量）⇒ 按必然解析失败处理。 */
#define HMI_CMD_WIRING_PAYLOAD_MAX 384

static hmi_mqtt_cmd_ledger_t s_ledger;
static int s_ledger_ready = 0;
static int s_registered = 0;

unsigned long long hmi_cmd_wiring_now_ms(void)
{
    struct timespec ts;
    /* 墙钟（与 hmi_actions.c 的审计同一来源 time()）—— 信封 `ts=` 就是"远程端时钟"，
     * 两条时间必须同一基；设备无 RTC 时这里是 1970 附近 ⇒ 命令一律判过期/来自未来（宁拒不放）。 */
    clock_gettime(CLOCK_REALTIME, &ts);
    return (unsigned long long)ts.tv_sec * 1000ULL
         + (unsigned long long)(ts.tv_nsec / 1000000);
}

unsigned hmi_cmd_wiring_current_perm_mask(void)
{
    /* 逐位问 `hmi_has_permission`：权限语义（未登录只含"查看画面" / HMI_SEC_ENFORCE=0 恒真）
     * **只有那一处实现** —— 在这里复制一份必然漂移，而漂移的表现是"门在动作路径上有效、
     * 在远程路径上失效"。远程命令是低频事件，32 次调用可忽略。 */
    unsigned mask = 0u;
    for (int b = 0; b < 32; b++)
        if (hmi_has_permission(1u << b)) mask |= (1u << b);
    return mask;
}

int hmi_cmd_wiring_is_cmd_topic(const char* topic)
{
    if (topic == NULL) return 0;
    return hmi_mqtt_topic_kind(topic, HMI_MQTT_TOPIC_PRE) == HMI_MQTT_TOPIC_CMD;
}

/* `<prefix>/cmd/<target>` 里的 target —— 与 mqtt_acl_contract.c 的 `cmd_target` 同一算法
 * （调用方必须先用 `hmi_cmd_wiring_is_cmd_topic` 判过形状）。 */
static const char* topic_target(const char* topic)
{
    return topic + strlen(HMI_MQTT_TOPIC_PRE) + 1 + 4;   /* 跳过 "/" + "cmd/" */
}

void hmi_cmd_wiring_register_topics(void)
{
    s_registered = 0;
    int n = hmi_tag_count();
    for (int i = 0; i < n; i++) {
        const tag_meta_t* m = hmi_tag_meta_at(i);
        if (m == NULL || m->read_only) continue;   /* 只读标签不订阅命令主题 */
        char topic[192];
        if (hmi_mqtt_format_cmd_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE, m->name) != 0)
            continue;
        if (mqtt_register_topic(topic, NULL) == 0) {
            s_registered++;
        } else {
            /* 订阅表有界（MQTT_MAX_TOPICS=32）⇒ **不静默**：报一行，别让"少订阅了一个 target"
             * 表现成"远程写某个变量没反应"这种极难归因的现象。 */
            fprintf(stderr, "[hmi] cmd topic skipped (subscribe table full): %s\n", topic);
        }
    }
}

int hmi_cmd_wiring_registered_count(void) { return s_registered; }

void hmi_cmd_wiring_reset_ledger(void)
{
    hmi_mqtt_cmd_ledger_init(&s_ledger);
    s_ledger_ready = 1;
}

/* ── 宿主能力：变量缓存 + 审计 ── */

static int io_get(const char* target, double* out)
{
    int i = hmi_tag_index_by_name(target);
    if (i < 0 || out == NULL) return -1;
    *out = (double)var_raw_read(i);     /* 原始值口径：量程也是原始值口径（§204.1） */
    return 0;
}

static int io_set(const char* target, double value)
{
    int i = hmi_tag_index_by_name(target);
    if (i < 0) return -1;
    var_raw_write(i, (float)value);     /* 只读保护在 var_raw_write 与上层校验里各有一道 */
    return 0;
}

static void io_audit(const char* event, const char* by, const char* detail)
{
    /* hmi_cmd_io_t 是 3 参（event/by/detail），设备审计是 4 参（多一个 role）——
     * 角色由宿主补（.so 侧不知道角色，见 hmi_actions.h 的注释）。 */
    hmi_audit(event, by, hmi_current_role(), detail);
}

int hmi_cmd_wiring_handle(const char* topic, const char* payload, int len,
                          unsigned long long now_ms, unsigned perm_mask,
                          hmi_cmd_result_t* out)
{
    static const hmi_cmd_io_t k_io = { io_get, io_set, io_audit };
    MqttCommand cmd;
    hmi_cmd_target_t target;
    char buf[HMI_CMD_WIRING_PAYLOAD_MAX];
    const char* text;
    int has_target = 0;

    if (out == NULL || topic == NULL) return -1;

    if (!s_ledger_ready) hmi_cmd_wiring_reset_ledger();

    if (payload == NULL || len <= 0) {
        text = "";                                  /* ⇒ 解析失败（malformed），由 dispatch 留痕 */
    } else if (len >= (int)sizeof(buf)) {
        text = "";                                  /* 超长不截断（见上方常量注释） */
    } else {
        memcpy(buf, payload, (size_t)len);
        buf[len] = '\0';
        text = buf;
    }

    if (text[0] != '\0' && hmi_mqtt_cmd_parse_payload(text, &cmd) == 0) {
        /* 主题里的 target 与载荷 `target=` 必须一致：两个字段都能决定"写谁"，
         * 不一致时 ACL 检查的是**主题**、真正写的是**载荷** ⇒ 直接拒（顺序即契约）。 */
        if (hmi_cmd_wiring_is_cmd_topic(topic) &&
            strcmp(topic_target(topic), cmd.target) != 0) {
            memset(out, 0, sizeof(*out));
            out->code = HMI_CMD_DENIED_TARGET_MISMATCH;
            out->target = cmd.target;
            io_audit("remote-write-denied", cmd.by[0] ? cmd.by : NULL,
                     HMI_CMD_DENIED_TARGET_MISMATCH);
            return -1;
        }
        /* 元数据按值传（dispatch 不做 lookup 回调）；查不到 ⇒ NULL ⇒ dispatch 报 target-not-found。 */
        const tag_meta_t* m = hmi_tag_meta_at(hmi_tag_index_by_name(cmd.target));
        if (m != NULL) {
            target.target = cmd.target;
            target.writable = !m->read_only;
            target.has_range = m->has_range;
            target.min_value = (double)m->min_value;
            target.max_value = (double)m->max_value;
            has_target = 1;
        }
    }

    return hmi_cmd_handle(topic, HMI_MQTT_TOPIC_PRE, text, now_ms, perm_mask,
                          &s_ledger, has_target ? &target : NULL, &k_io, out);
}

int hmi_cmd_wiring_on_message(const char* topic, const char* payload, int len)
{
    hmi_cmd_result_t res;
    return hmi_cmd_wiring_handle(topic, payload, len,
                                 hmi_cmd_wiring_now_ms(),
                                 hmi_cmd_wiring_current_perm_mask(), &res);
}
