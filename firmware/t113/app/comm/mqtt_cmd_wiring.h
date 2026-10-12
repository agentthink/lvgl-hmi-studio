/*
 * mqtt_cmd_wiring.h — 远程写命令的**宿主接线**（2026-10-11，M5 的"设备真的执行"最后一公里）
 *
 * 背景（登记册 §204.6 自陈的缺口）：`mqtt_cmd_dispatch.{h,c}` 与 `mqtt_write_guard.c` /
 * `mqtt_cmd_ledger.c` 都已落地、都有判据，**但没有任何调用点** —— `mqtt_client.c` 收报文
 * 之后不会走到它们。本模块就是那条线，且只做**接线**（判定逻辑一律留在原来那几处）：
 *
 *   ① 订阅：为每个**可写**标签注册 `<prefix>/cmd/<name>`（只读标签不订阅，订阅面越小越好）；
 *   ② 路由：`variable_table.c` 的消息回调把 `<prefix>/cmd/...` 分流到这里（与 `<pre>/set/` 分开）；
 *   ③ 元数据：按载荷里的 `target=` 查采纳的标签表（`tag_meta_t`）—— **按值**填
 *      `hmi_cmd_target_t`（只读 / 量程），不做 lookup 回调（理由见 mqtt_cmd_dispatch.h）；
 *   ④ 宿主能力：`get_value`/`set_value` 接变量缓存，`audit` 接 `hmi_audit`（角色由宿主补）；
 *   ⑤ 权限：当前会话掩码**逐位问 `hmi_has_permission`**（唯一口径在那儿，不在这里复制
 *      "未登录只含查看画面 / HMI_SEC_ENFORCE=0 恒真"这两条语义 —— 复制必然漂移）。
 *
 * 判据可注入：核心入口 `hmi_cmd_wiring_handle()` 的 `now_ms` 与 `perm_mask` 由调用方给
 * ⇒ 主机侧判据不睡觉、不依赖会话与真实时钟。
 */
#ifndef HMI_MQTT_CMD_WIRING_H
#define HMI_MQTT_CMD_WIRING_H

#include "mqtt_cmd_dispatch.h"

/* 主题分类：是否为本前缀下的 `<prefix>/cmd/<target>`（供 variable_table 分流 /set/） */
int hmi_cmd_wiring_is_cmd_topic(const char* topic);

/* 订阅注册：为每个可写标签注册 `<prefix>/cmd/<name>`。
 * 在标签表采纳/重建之后调用（`mqtt_clear_topics()` 之后），断线重订阅由 mqtt_client 负责。
 * 订阅表满（MQTT_MAX_TOPICS）时跳过并报一行诊断 —— 不静默。 */
void hmi_cmd_wiring_register_topics(void);

/* 设备侧入口：自己取墙钟与当前会话掩码，再走核心。返回同 `hmi_cmd_handle`。 */
int hmi_cmd_wiring_on_message(const char* topic, const char* payload, int len);

/* 核心（可注入 now/perm）。`out` 非空。 */
int hmi_cmd_wiring_handle(const char* topic, const char* payload, int len,
                          unsigned long long now_ms, unsigned perm_mask,
                          hmi_cmd_result_t* out);

/* 当前会话权限掩码；**唯一口径 = `hmi_has_permission`**（逐位问）。 */
unsigned hmi_cmd_wiring_current_perm_mask(void);

/* 墙钟毫秒（UTC epoch）—— 与信封 `ts=` 同一时间基（设备无 RTC 时该值为 1970 附近，
 * 于是命令一律判"过期/来自未来" ⇒ **宁拒不放**，等 NTP/手动校时后自动恢复）。 */
unsigned long long hmi_cmd_wiring_now_ms(void);

/* 判据用：本次注册成功的 cmd 订阅数 / 重置幂等台账（每例独立） */
int  hmi_cmd_wiring_registered_count(void);
void hmi_cmd_wiring_reset_ledger(void);

/* 稳定码：主题里的 target 与载荷 `target=` 不一致（两个字段都能决定"写谁" ⇒ 必须拒） */
#define HMI_CMD_DENIED_TARGET_MISMATCH "target-mismatch"

#endif /* HMI_MQTT_CMD_WIRING_H */
