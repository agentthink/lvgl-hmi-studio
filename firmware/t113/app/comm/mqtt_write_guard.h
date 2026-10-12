/*
 * mqtt_write_guard.h — 远程写命令的**值校验**（阶段 287；M5 设备端执行的另一半）
 *
 * 目标（变量）的元数据由调用方提供 —— 设备侧只读保护/量程来自 .so 的 tag 元数据
 * （`hmi_ui_api.h` 的 `tag_meta_t`，宿主在 hmi_ui_load 时采纳）。本模块只做**纯判定**：
 *   · 只读 ⇒ 拒（**先于**值域判断：只读变量的值再合法也不许写）；
 *   · 值不是严格数字（空/空白/后随垃圾/NaN/inf 文本）⇒ 拒；
 *   · 超出 [min, max]（含端点）⇒ 拒；min > max ⇒ 参数非法（配置错）。
 * 返回码即稳定码（见下面的 token），审计直接落它。
 */
#ifndef HMI_MQTT_WRITE_GUARD_H
#define HMI_MQTT_WRITE_GUARD_H

#include <stddef.h>

#define HMI_MQTT_WRITE_OK            0
#define HMI_MQTT_WRITE_READ_ONLY    -1
#define HMI_MQTT_WRITE_NOT_A_NUMBER -2
#define HMI_MQTT_WRITE_OUT_OF_RANGE -3
#define HMI_MQTT_WRITE_BAD_TARGET   -4   /* min > max / 参数非法（配置错，不是"值错"） */

/* 稳定码 token（审计/判据用；未知码 ⇒ NULL） */
const char* hmi_mqtt_write_issue_token(int code);

/* writable: 1=可写 · 0=只读；min/max：量程（inclusive）；value_text：远程来的字符串。
 * out_value 非空时写解析结果（仅在返回 OK 时有意义）。reason 非空时写稳定码 token（OK ⇒ NULL）。 */
int hmi_mqtt_write_check(int writable, double min, double max, const char* value_text,
                         double* out_value, const char** reason);

#endif /* HMI_MQTT_WRITE_GUARD_H */
