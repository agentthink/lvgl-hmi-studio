/*
 * hmi_alarm_snapshot.h — 报警**逐条快照**的 ABI 数据类型（阶段 291，M2 的报警上行数据源）
 *
 * 为什么单独一个头（而不是塞进 hmi_ui_api.h）：`hmi_ui_api.h` 拉 lvgl.h，而**主机侧**
 * 的发布器与判据都不能拉 lvgl（`firmware/t113/tests` 刻意不解析 lvgl.h）。
 * 把数据类型抽出来 ⇒ 宿主、.so、主机判据三处共用同一个定义，且没有 lvgl 依赖。
 *
 * 为什么需要它：M2 要发 `<prefix>/alarm/<报警 ID>` 的 `st=`/`sev=`，
 * 而既有的 `alarm_active_count()` **只给一个数字** —— 拿不到 id / 严重度 / 状态。
 *
 * ⚠ `id` 是**定长缓冲**而不是 `const char*`：不让宿主与 .so 的字符串生命周期耦合
 * （热更新/卸载后宿主手里不能还攥着 .so 的指针）。上限与 MQTT 契约的主题约束一致：
 * 报警 ID 由工程给出、**不许含 `/`**，超长按截断处理（生成端有判据钉住上限）。
 */
#ifndef HMI_ALARM_SNAPSHOT_H
#define HMI_ALARM_SNAPSHOT_H

#include <stdint.h>

#define HMI_ALARM_SNAPSHOT_ID_MAX 32

typedef struct {
    char     id[HMI_ALARM_SNAPSHOT_ID_MAX];  /* 工程报警 ID（不含 `/`） */
    uint16_t config_id;                      /* 与 _alarm_lookup / 确认函数同一口径 */
    uint8_t  state;     /* 1=Active 2=Acknowledged 3=Returned 4=Shelved；**0=Inactive 不上行** */
    uint8_t  severity;  /* 0=Info 1=Warning 2=Critical 3=Emergency */
} hmi_alarm_snapshot_t;

#endif /* HMI_ALARM_SNAPSHOT_H */
