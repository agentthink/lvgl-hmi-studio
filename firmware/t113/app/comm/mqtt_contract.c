/*
 * mqtt_contract.c — 契约 v1 的实现（见 mqtt_contract.h 的注释：主题/payload 形状与三条返回约定）
 *
 * 实现纪律：
 *   - **先算长度、后落笔**：容量不足时直接返回 -2，绝不写半截（调用点的 `out` 常是共享缓冲）。
 *   - 数值一律 `%.6g`：与旧 `%g` 同族、且**不依赖 locale**（设备进程不调 setlocale）。
 *   - 变量名含 `/` 直接拒绝：那会把主题层级改写掉（远程端按层级订阅就串了）。
 */
#include "mqtt_contract.h"

#include <stdio.h>
#include <string.h>

#define MQTT_CONTRACT_MAX_PREFIX 128
#define MQTT_CONTRACT_MAX_NAME   96

const char* hmi_mqtt_time_base_token(int time_base_utc)
{
    return time_base_utc ? "utc" : "boot";
}

int hmi_mqtt_format_value_topic(char* out, size_t cap, const char* prefix, const char* var_name)
{
    if (out == NULL || cap == 0) return -1;
    out[0] = '\0';
    if (prefix == NULL || var_name == NULL) return -1;
    if (prefix[0] == '\0' || var_name[0] == '\0') return -1;
    if (strchr(var_name, '/') != NULL) return -1;            /* 变量名不许含层级分隔符 */
    if (strlen(prefix) > MQTT_CONTRACT_MAX_PREFIX) return -1;
    if (strlen(var_name) > MQTT_CONTRACT_MAX_NAME) return -1;

    /* "<prefix>/val/<name>" */
    int need = snprintf(NULL, 0, "%s/val/%s", prefix, var_name);
    if (need < 0) return -1;
    if ((size_t)need + 1 > cap) return -2;                    /* 容量不足：不写半截 */
    snprintf(out, cap, "%s/val/%s", prefix, var_name);
    return 0;
}

int hmi_mqtt_format_value_payload(char* out, size_t cap, double value,
                                  unsigned long long t_ms, int time_base_utc, int quality)
{
    if (out == NULL || cap == 0) return -1;
    out[0] = '\0';

    const char* tb = hmi_mqtt_time_base_token(time_base_utc);
    int need = snprintf(NULL, 0, "v=%.6g;t=%llu;tb=%s;q=%d",
                        value, t_ms, tb, quality ? 1 : 0);
    if (need < 0) return -1;
    if ((size_t)need + 1 > cap) return -2;                    /* 容量不足：不写半截 */
    snprintf(out, cap, "v=%.6g;t=%llu;tb=%s;q=%d",
             value, t_ms, tb, quality ? 1 : 0);
    return 0;
}

int hmi_mqtt_format_alarm_topic(char* out, size_t cap, const char* prefix, const char* alarm_id)
{
    if (out == NULL || cap == 0) return -1;
    out[0] = '\0';
    if (prefix == NULL || alarm_id == NULL) return -1;
    if (prefix[0] == '\0' || alarm_id[0] == '\0') return -1;
    if (strchr(alarm_id, '/') != NULL) return -1;             /* 报警 ID 不许含层级分隔符 */
    if (strlen(prefix) > MQTT_CONTRACT_MAX_PREFIX) return -1;
    if (strlen(alarm_id) > MQTT_CONTRACT_MAX_NAME) return -1;

    int need = snprintf(NULL, 0, "%s/alarm/%s", prefix, alarm_id);
    if (need < 0) return -1;
    if ((size_t)need + 1 > cap) return -2;
    snprintf(out, cap, "%s/alarm/%s", prefix, alarm_id);
    return 0;
}

int hmi_mqtt_format_alarm_payload(char* out, size_t cap, int state, int severity,
                                  unsigned long long t_ms, int time_base_utc)
{
    if (out == NULL || cap == 0) return -1;
    out[0] = '\0';
    /* 状态只许 1..4（0=Inactive 不是"可发布的状态迁移"；4=搁置保留位，见头文件） */
    if (state < HMI_MQTT_ALARM_STATE_ACTIVE || state > HMI_MQTT_ALARM_STATE_MAX) return -1;
    if (severity < 0 || severity > HMI_MQTT_ALARM_SEVERITY_MAX) return -1;

    const char* tb = hmi_mqtt_time_base_token(time_base_utc);
    int need = snprintf(NULL, 0, "st=%d;sev=%d;t=%llu;tb=%s", state, severity, t_ms, tb);
    if (need < 0) return -1;
    if ((size_t)need + 1 > cap) return -2;
    snprintf(out, cap, "st=%d;sev=%d;t=%llu;tb=%s", state, severity, t_ms, tb);
    return 0;
}

int hmi_mqtt_format_status_topic(char* out, size_t cap, const char* prefix)
{
    if (out == NULL || cap == 0) return -1;
    out[0] = '\0';
    if (prefix == NULL || prefix[0] == '\0') return -1;
    if (strlen(prefix) > MQTT_CONTRACT_MAX_PREFIX) return -1;

    int need = snprintf(NULL, 0, "%s/status", prefix);
    if (need < 0) return -1;
    if ((size_t)need + 1 > cap) return -2;
    snprintf(out, cap, "%s/status", prefix);
    return 0;
}

int hmi_mqtt_format_status_payload(char* out, size_t cap, int online,
                                   unsigned long long t_ms, int time_base_utc)
{
    if (out == NULL || cap == 0) return -1;
    out[0] = '\0';

    /* 离线时**与遗嘱逐字相同**：设备掉线时 broker 代发的就是那一条，两条必须一模一样 */
    if (!online) {
        int need = snprintf(NULL, 0, "%s", HMI_MQTT_STATUS_LWT_PAYLOAD);
        if (need < 0) return -1;
        if ((size_t)need + 1 > cap) return -2;
        snprintf(out, cap, "%s", HMI_MQTT_STATUS_LWT_PAYLOAD);
        return 0;
    }

    const char* tb = hmi_mqtt_time_base_token(time_base_utc);
    int need = snprintf(NULL, 0, "on=1;t=%llu;tb=%s", t_ms, tb);
    if (need < 0) return -1;
    if ((size_t)need + 1 > cap) return -2;
    snprintf(out, cap, "on=1;t=%llu;tb=%s", t_ms, tb);
    return 0;
}
