#include "mqtt_acl_contract.h"

/* 权限位**只能**来自编辑器生成的 generated_security.h —— 设备端不得手写权限位
 * （阶段 282 起仓里有这条守卫：`SecurityConfigCodeGenTests.Firmware_DoesNotHandWritePermissionBits`；
 *  本模块第一版写了 `#define HMI_PERM_* 0x…` 兜底，当场被它拦下 —— 守卫是对的，删掉兜底）。
 * 因此本模块与 hmi_actions.c 一样，是"必须先生成"的编译单元。 */
#include "generated_security.h"

#include <string.h>

static int starts_with(const char* s, const char* prefix, size_t prefix_len)
{
    return strncmp(s, prefix, prefix_len) == 0;
}

hmi_mqtt_topic_kind_t hmi_mqtt_topic_kind(const char* topic, const char* prefix)
{
    size_t n;

    if (topic == NULL || prefix == NULL || prefix[0] == '\0') return HMI_MQTT_TOPIC_UNKNOWN;
    n = strlen(prefix);
    /* ⚠ `topic[n] != '/'` 是**刻意的纵深防御**：`rest` 从 `+n+1` 开始（假定有 '/'），所以去掉这个检查
     *  时样例表**看不出差别**（负控 nc5 实测：摘掉它照样全绿）—— 但它挡住了"prefix 少写一个字符"
     *  这类配置错误。删它之前先想清楚：`rest` 的 +1 是对它的假设。 */
    if (!starts_with(topic, prefix, n) || topic[n] != '/') return HMI_MQTT_TOPIC_UNKNOWN;

    const char* rest = topic + n + 1;

    if (strncmp(rest, "value/", 6) == 0)  return rest[6] != '\0' ? HMI_MQTT_TOPIC_VALUE  : HMI_MQTT_TOPIC_UNKNOWN;
    if (strncmp(rest, "alarm/", 6) == 0)  return rest[6] != '\0' ? HMI_MQTT_TOPIC_ALARM  : HMI_MQTT_TOPIC_UNKNOWN;
    if (strncmp(rest, "status", 6) == 0)  return rest[6] == '\0' ? HMI_MQTT_TOPIC_STATUS : HMI_MQTT_TOPIC_UNKNOWN;
    if (strncmp(rest, "cmd/", 4) == 0)    return rest[4] != '\0' ? HMI_MQTT_TOPIC_CMD    : HMI_MQTT_TOPIC_UNKNOWN;

    return HMI_MQTT_TOPIC_UNKNOWN;
}

/* `<prefix>/cmd/<target>` 里的 target（本函数只在 kind==CMD 时被调用） */
static const char* cmd_target(const char* topic, const char* prefix)
{
    return topic + strlen(prefix) + 1 + 4;   /* 跳过 "cmd/" */
}

unsigned hmi_mqtt_acl_required_perm(hmi_mqtt_topic_kind_t kind, int is_write, const char* target)
{
    switch (kind) {
        case HMI_MQTT_TOPIC_VALUE:
            return is_write ? 0u : HMI_PERM_VIEW_SCREEN;            /* A1：远程不许伪造遥测 */
        case HMI_MQTT_TOPIC_ALARM:
            return is_write ? 0u : HMI_PERM_VIEW_ALARM_HISTORY;
        case HMI_MQTT_TOPIC_STATUS:
            return is_write ? 0u : HMI_PERM_VIEW_SCREEN;
        case HMI_MQTT_TOPIC_CMD:
            if (!is_write) return 0u;                                /* 设备只写不读命令 */
            if (target == NULL) return (unsigned)-1;
            if (strncmp(target, "ack/", 4) == 0)      return HMI_PERM_ACKNOWLEDGE_ALARM;
            if (strncmp(target, "shelve/", 7) == 0)   return HMI_PERM_SHELVE_ALARM;
            if (strncmp(target, "unshelve/", 9) == 0) return HMI_PERM_SHELVE_ALARM;
            return HMI_PERM_OPERATE_CONTROL;
        default:
            return (unsigned)-1;                                     /* A3 */
    }
}

int hmi_mqtt_acl_allows(const char* topic, const char* prefix, int is_write,
                        unsigned perm_mask, const char** reason)
{
    const char** set_reason = reason;
    hmi_mqtt_topic_kind_t kind = hmi_mqtt_topic_kind(topic, prefix);
    unsigned required;

    if (kind == HMI_MQTT_TOPIC_UNKNOWN) {
        if (set_reason) *set_reason = HMI_MQTT_ACL_UNKNOWN_TOPIC;
        return -1;
    }

    required = hmi_mqtt_acl_required_perm(kind, is_write,
                                         kind == HMI_MQTT_TOPIC_CMD ? cmd_target(topic, prefix) : NULL);
    if (required == (unsigned)-1) {
        if (set_reason) *set_reason = HMI_MQTT_ACL_UNKNOWN_TOPIC;
        return -1;
    }
    if (required == 0u) {
        if (set_reason) *set_reason = HMI_MQTT_ACL_WRONG_DIRECTION;
        return -1;
    }
    if ((perm_mask & required) != required) {
        if (set_reason) *set_reason = HMI_MQTT_ACL_FORBIDDEN;
        return -1;
    }

    if (set_reason) *set_reason = NULL;
    return 0;
}
