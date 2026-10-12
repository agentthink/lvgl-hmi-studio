/*
 * mqtt_contract.h — 远程访问的**主题 / payload 契约 v1**（阶段 264；登记 §2-#12 的 M1）
 *
 * 为什么要有这一层：改造前设备侧的上行是**就地拼串**（`variable_table.c` 里两行 `snprintf`）：
 *   主题 `"%s/val/%s"`，payload `"%g"` —— 于是 ① 契约散在调用点、② payload 只有数值
 *   （**没有时间戳、没有质量位**，远程端无法判断"这是什么时候的值""这个值可信吗"）。
 *   本文件把契约收成**纯函数**：两侧（C 与 C#）用**同一张样例表**逐字节判。
 *
 * 契约 v1（对外承诺，改动必须两端同步 + 更新样例表）：
 *   主题：`<prefix>/val/<变量名>`
 *         · `prefix` 可以含 `/`（如 `plant1/line3`）；`变量名` **不允许含 `/`**（否则层级被改写）
 *         · 任一段为空 ⇒ 拒绝（返回 -1）
 *   payload：`v=<值>;t=<毫秒>;tb=<utc|boot>;q=<0|1>`
 *         · `v` 用 `%.6g`（与旧 `%g` 兼容的数值写法；**小数点固定 `.`**，不随 locale 变）
 *         · `t` 的时间基必须**显式声明**：`tb=utc`（墙钟 epoch ms）/ `tb=boot`（开机时长 ms）
 *           —— 设备侧没有 RTC，`lv_tick_get()` 给的是开机时长，这一点与
 *           `DataLogger.cs:190-199` 记的 CSV 时间基问题是**同一个**，所以契约里必须写死
 *         · `q=1` 好值 / `q=0` 坏值（保留位：将来接驱动质量码）
 *
 * 返回值约定（三条，判据逐条钉）：
 *    0   成功
 *   -1   参数非法（out/cap/prefix/变量名为空，或变量名含 `/`）
 *   -2   容量不足 —— **一个字节都不写**（`out[0]='\0'` 当 cap>0），绝不产出半截主题/半截 payload
 */
#ifndef HMI_MQTT_CONTRACT_H
#define HMI_MQTT_CONTRACT_H

#include <stddef.h>

/* 主题：<prefix>/val/<var_name> */
int hmi_mqtt_format_value_topic(char* out, size_t cap, const char* prefix, const char* var_name);

/* payload：v=<%.6g>;t=<t_ms>;tb=<utc|boot>;q=<0|1> */
int hmi_mqtt_format_value_payload(char* out, size_t cap, double value,
                                  unsigned long long t_ms, int time_base_utc, int quality);

/* `tb=` 的取值 token：time_base_utc != 0 ⇒ "utc"，否则 "boot" */
const char* hmi_mqtt_time_base_token(int time_base_utc);

/* ══════════════════════════════════════════════════════════════════
 * M2：报警/状态上行（阶段 272；登记 §2-#12 的 M2）
 *
 * 主题：`<prefix>/alarm/<报警 ID>`      —— 报警 ID 由工程给出，**不许含 `/`**
 *       `<prefix>/status`
 * payload（报警）：`st=<1..4>;sev=<0..3>;t=<毫秒>;tb=<boot|utc>`
 *       `st` 与 `AlarmState` 同号：1=Active · 2=Acknowledged · 3=Returned ·
 *       **4 = 保留给「搁置（Shelved）」**（A1=② 决定搁置设备侧也做；契约先留位，实现另立阶段）
 *       `sev` 与 `AlarmSeverity` 同号：0=Info · 1=Warning · 2=Critical · 3=Emergency
 * payload（状态）：`on=<0|1>;t=<毫秒>;tb=<boot|utc>`；**遗嘱（LWT）用常量
 *       `HMI_MQTT_STATUS_LWT_PAYLOAD`**（`on=0`，无时间戳 —— 设备已经掉线，没人能给它打时间）
 *
 * 返回约定与 v1 相同：0 成功 / -1 参数非法 / -2 容量不足且一个字节都不写。
 * ══════════════════════════════════════════════════════════════════ */

#define HMI_MQTT_ALARM_STATE_ACTIVE        1
#define HMI_MQTT_ALARM_STATE_ACKNOWLEDGED  2
#define HMI_MQTT_ALARM_STATE_RETURNED      3
#define HMI_MQTT_ALARM_STATE_SHELVED       4   /* 保留：搁置（尚未产出） */
#define HMI_MQTT_ALARM_STATE_MAX           HMI_MQTT_ALARM_STATE_SHELVED
#define HMI_MQTT_ALARM_SEVERITY_MAX        3

#define HMI_MQTT_STATUS_LWT_PAYLOAD "on=0"

int hmi_mqtt_format_alarm_topic(char* out, size_t cap, const char* prefix, const char* alarm_id);
int hmi_mqtt_format_alarm_payload(char* out, size_t cap, int state, int severity,
                                  unsigned long long t_ms, int time_base_utc);
int hmi_mqtt_format_status_topic(char* out, size_t cap, const char* prefix);
int hmi_mqtt_format_status_payload(char* out, size_t cap, int online,
                                   unsigned long long t_ms, int time_base_utc);

#endif /* HMI_MQTT_CONTRACT_H */
