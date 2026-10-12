/*
 * mqtt_cmd_contract.h — 远程**写命令**的信封契约（阶段 285；`§2-#12` M5 的第一块）
 *
 * 提案 §4「失效」那一行：**命令带 TTL ＋ 幂等 id；离线命令不排队**。
 * 本文件把那行写成可判的纯函数（设备侧唯一实现）。
 *
 * 主题：`<prefix>/cmd/<target>`（与 M1 的 `<prefix>/value/<var>` 同族形状）
 * 载荷：`key=value;` 序列（与 M1/M2 同一套 `MqttPayloadFields` 语义）
 *   id=<幂等 id>      1..64 字符，只许 [A-Za-z0-9_-]
 *   ts=<签发时刻>     毫秒（UTC epoch；设备无 RTC ⇒ 见 §4 的 tb 说明）
 *   ttl=<有效期 ms>   1000..300000（1s..5min）
 *   by=<主体>         操作人（审计用；可空）
 *   target=<目标>     变量名 / 控件 id
 *   value=<值>
 *
 * 三条硬规则：
 *   R1 **幂等 id 形状**：不合规 ⇒ 拒绝（不是"当作新命令"）；
 *   R2 **TTL 有上下界**：越界 ⇒ 拒绝（避免"永久有效"或"必然过期"的命令）；
 *   R3 **过期即丢**：`now - ts > ttl` ⇒ 拒绝（也拒绝"来自未来"的 ts，容差见 HMI_MQTT_CMD_CLOCK_SKEW_MS）。
 */
#ifndef HMI_MQTT_CMD_CONTRACT_H
#define HMI_MQTT_CMD_CONTRACT_H

#include <stddef.h>

#define HMI_MQTT_CMD_ID_MAX          64
#define HMI_MQTT_CMD_MIN_TTL_MS      1000ull
#define HMI_MQTT_CMD_MAX_TTL_MS      300000ull
/* 允许的时钟偏斜（设备无 RTC：远程端时钟可能略快）—— 超出即认为"来自未来" */
#define HMI_MQTT_CMD_CLOCK_SKEW_MS   5000ull

/* `<prefix>/cmd/<target>`；返回 0 成功 / -1 非法（空参、容量不够） */
int hmi_mqtt_format_cmd_topic(char* out, size_t cap, const char* prefix, const char* target);

/* 幂等 id 形状：非空、≤64、只许 [A-Za-z0-9_-] */
int hmi_mqtt_cmd_is_id_valid(const char* id);

/* TTL 上下界 */
int hmi_mqtt_cmd_is_ttl_valid(unsigned long long ttl_ms);

/* 新鲜度：ts 在未来超过 skew ⇒ -1；now - ts > ttl ⇒ -1；否则 0 */
int hmi_mqtt_cmd_is_fresh(unsigned long long issued_ms, unsigned long long ttl_ms,
                          unsigned long long now_ms);

#define HMI_MQTT_CMD_BY_MAX     64
#define HMI_MQTT_CMD_TARGET_MAX 96
#define HMI_MQTT_CMD_VALUE_MAX  96

/* 解析结果（阶段 288：设备侧第一次真的把报文收进来）。
 * ⚠ 字段是**定长缓冲**而不是"指向载荷内部的指针"：指针无法按 `;` 分段结束
 *   （`strcmp` 会一路比到载荷末尾）—— 首跑就是这么红的；超长 ⇒ 解析失败（不截断）。 */
typedef struct {
    char               id[HMI_MQTT_CMD_ID_MAX + 1];
    unsigned long long issued_ms;
    unsigned long long ttl_ms;
    char               by[HMI_MQTT_CMD_BY_MAX + 1];      /* 缺 ⇒ "" */
    char               target[HMI_MQTT_CMD_TARGET_MAX + 1];
    char               value[HMI_MQTT_CMD_VALUE_MAX + 1]; /* 缺 ⇒ "" */
} MqttCommand;

/* 解析 `id;ts;ttl;[by;]target;value`：0 = 成功；-1 = 缺字段/形状不合规（**不尽力而为**）*/
int hmi_mqtt_cmd_parse_payload(const char* payload, MqttCommand* out);

/* 载荷：拼 `id=..;ts=..;ttl=..;by=..;target=..;value=..`（by 为空则不出该段） */
int hmi_mqtt_format_cmd_payload(char* out, size_t cap, const char* id,
                                unsigned long long issued_ms, unsigned long long ttl_ms,
                                const char* by, const char* target, const char* value);

#endif /* HMI_MQTT_CMD_CONTRACT_H */
