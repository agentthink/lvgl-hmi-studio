/*
 * mqtt_alarm_publish.h — 报警**上行发布器**（阶段 291，远程访问 `§2-#12` M2 的报警那一半）
 *
 * 契约（阶段 272 已定，本模块只消费）：
 *   主题   `<prefix>/alarm/<报警 ID>`   报警 ID 由工程给出、不许含 `/`
 *   payload `st=<1..4>;sev=<0..3>;t=<毫秒>;tb=<boot|utc>`
 *   `st` 与 AlarmState 同号：1=Active 2=Acknowledged 3=Returned 4=Shelved；**0=Inactive 不上行**
 *
 * 为什么需要这一层：`hmi_mqtt_format_alarm_*` 是纯函数、`alarm_snapshot` 是 .so 的能力，
 * 两者之间缺一个"**谁在什么时候发**"的决策点 —— 就是本模块。
 *
 * 三个刻意决定：
 *   ① **边沿触发**：(state,severity) 与上一轮不同才发 ⇒ 不每秒刷同一状态（远程端只关心变化）；
 *   ② `state == 0` 跳过：Inactive 不是"可发布的状态迁移"，契约的格式化函数也会拒它；
 *   ③ fetch 走**注入**（函数指针）而不是直接调 `hmi_ui_get_current_api()`：
 *      主机判据因此能驱动真发布器而不拉 lvgl（设备侧的真实 fetch 见 hmi_alarm_source.c）。
 */
#ifndef HMI_MQTT_ALARM_PUBLISH_H
#define HMI_MQTT_ALARM_PUBLISH_H

#include "hmi_alarm_snapshot.h"

/* 取一次报警快照：写进 out，返回条数（≤ cap）。≤0 视为"当前无报警"。 */
typedef int (*hmi_alarm_fetch_fn)(hmi_alarm_snapshot_t* out, int cap);

/* 一轮最多处理多少条（报警数量有界；超出部分下一轮再发）。 */
#define HMI_ALARM_PUBLISH_MAX 16

/* 扫描并发布；返回**本轮实际发布**的条数（判据与诊断用）。 */
int hmi_alarm_publish_scan(hmi_alarm_fetch_fn fetch, unsigned long long now_ms, int time_base_utc);

/* 清空"上一轮快照" ⇒ 下一轮全量重发（判据用；设备侧断线重连后也可用它避免漏发）。 */
void hmi_alarm_publish_reset(void);

#endif /* HMI_MQTT_ALARM_PUBLISH_H */
