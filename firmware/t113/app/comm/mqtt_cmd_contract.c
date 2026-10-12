#include "mqtt_cmd_contract.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

int hmi_mqtt_format_cmd_topic(char* out, size_t cap, const char* prefix, const char* target)
{
    int n;

    if (out == NULL || cap == 0 || prefix == NULL || prefix[0] == '\0') return -1;
    if (target == NULL || target[0] == '\0') return -1;

    n = snprintf(out, cap, "%s/cmd/%s", prefix, target);
    return (n < 0 || (size_t)n >= cap) ? -1 : 0;
}

int hmi_mqtt_cmd_is_id_valid(const char* id)
{
    size_t n = 0;

    if (id == NULL || id[0] == '\0') return -1;
    for (const char* p = id; *p != '\0'; p++, n++) {
        if (n >= HMI_MQTT_CMD_ID_MAX) return -1;
        int ok = (*p >= 'a' && *p <= 'z') || (*p >= 'A' && *p <= 'Z') ||
                 (*p >= '0' && *p <= '9') || *p == '_' || *p == '-';
        if (!ok) return -1;
    }
    return 0;
}

int hmi_mqtt_cmd_is_ttl_valid(unsigned long long ttl_ms)
{
    return (ttl_ms >= HMI_MQTT_CMD_MIN_TTL_MS && ttl_ms <= HMI_MQTT_CMD_MAX_TTL_MS) ? 0 : -1;
}

int hmi_mqtt_cmd_is_fresh(unsigned long long issued_ms, unsigned long long ttl_ms,
                          unsigned long long now_ms)
{
    if (hmi_mqtt_cmd_is_ttl_valid(ttl_ms) != 0) return -1;

    /* 来自"未来"太多：远程端时钟快了，或 ts 被伪造 —— 一律不收 */
    if (issued_ms > now_ms && (issued_ms - now_ms) > HMI_MQTT_CMD_CLOCK_SKEW_MS) return -1;

    /* 过期（含"刚好等于 TTL"—— 闭区间判定写在契约里） */
    if (now_ms > issued_ms && (now_ms - issued_ms) > ttl_ms) return -1;

    return 0;
}

int hmi_mqtt_format_cmd_payload(char* out, size_t cap, const char* id,
                                unsigned long long issued_ms, unsigned long long ttl_ms,
                                const char* by, const char* target, const char* value)
{
    int n;

    if (out == NULL || cap == 0) return -1;
    if (hmi_mqtt_cmd_is_id_valid(id) != 0) return -1;
    if (hmi_mqtt_cmd_is_ttl_valid(ttl_ms) != 0) return -1;
    if (target == NULL || target[0] == '\0') return -1;
    if (value == NULL) value = "";

    if (by != NULL && by[0] != '\0') {
        n = snprintf(out, cap, "id=%s;ts=%llu;ttl=%llu;by=%s;target=%s;value=%s",
                     id, issued_ms, ttl_ms, by, target, value);
    } else {
        n = snprintf(out, cap, "id=%s;ts=%llu;ttl=%llu;target=%s;value=%s",
                     id, issued_ms, ttl_ms, target, value);
    }

    return (n < 0 || (size_t)n >= cap) ? -1 : 0;
}

/* ── 解析（阶段 288）────────────────────────────────────────────
 * 逐字段严格：id 形状、ts/ttl 纯数字、target 非空；by/value 可缺（by ⇒ ""，value ⇒ ""）。
 * 字段拷进**定长缓冲**；放不下 ⇒ 失败（**不截断**：截断后的 target 会指向另一个变量）。 */
static const char* payload_segment(const char* payload, const char* key, size_t* len_out)
{
    size_t klen = strlen(key);
    const char* p = payload;

    while (*p != '\0') {
        const char* seg_end = strchr(p, ';');
        size_t seg_len = seg_end != NULL ? (size_t)(seg_end - p) : strlen(p);

        if (seg_len > klen && strncmp(p, key, klen) == 0 && p[klen] == '=') {
            size_t vlen = seg_len - (klen + 1);
            if (len_out != NULL) *len_out = vlen;
            return p + klen + 1;
        }

        if (seg_end == NULL) break;
        p = seg_end + 1;
    }
    return NULL;
}

/* 拷贝字段；缺 ⇒ 返回 -2（可选字段）/ 0（已拷）；放不下 ⇒ -1 */
static int payload_copy(const char* payload, const char* key, char* out, size_t cap, int optional)
{
    size_t len = 0;
    const char* v = payload_segment(payload, key, &len);

    if (v == NULL) {
        out[0] = '\0';
        return optional ? -2 : 0;
    }
    if (len + 1 > cap) return -1;
    memcpy(out, v, len);
    out[len] = '\0';
    return 0;
}

/* ts/ttl：整串必须是**纯数字**（`strtoull("-1")` 会静默回绕成 ULLONG_MAX —— 与 C# 口径不一致） */
static int payload_ull(const char* payload, const char* key, unsigned long long* out)
{
    char buf[24];
    size_t len = 0;
    const char* v = payload_segment(payload, key, &len);

    if (v == NULL || len == 0 || len >= sizeof(buf)) return -1;
    for (size_t i = 0; i < len; i++) if (v[i] < '0' || v[i] > '9') return -1;
    memcpy(buf, v, len);
    buf[len] = '\0';

    {
        char* end = NULL;
        unsigned long long parsed = strtoull(buf, &end, 10);
        if (end == buf || *end != '\0') return -1;
        *out = parsed;
    }
    return 0;
}

int hmi_mqtt_cmd_parse_payload(const char* payload, MqttCommand* out)
{
    if (payload == NULL || out == NULL) return -1;
    memset(out, 0, sizeof(*out));

    if (payload_copy(payload, "id", out->id, sizeof(out->id), 0) != 0) return -1;
    if (hmi_mqtt_cmd_is_id_valid(out->id) != 0) return -1;

    if (payload_ull(payload, "ts", &out->issued_ms) != 0) return -1;
    if (payload_ull(payload, "ttl", &out->ttl_ms) != 0) return -1;
    if (hmi_mqtt_cmd_is_ttl_valid(out->ttl_ms) != 0) return -1;

    (void)payload_copy(payload, "by", out->by, sizeof(out->by), 1);
    if (payload_copy(payload, "target", out->target, sizeof(out->target), 0) != 0) return -1;
    if (out->target[0] == '\0') return -1;
    (void)payload_copy(payload, "value", out->value, sizeof(out->value), 1);
    return 0;
}
