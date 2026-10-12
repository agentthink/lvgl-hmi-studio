/*
 * mqtt_cmd_dispatch.h — 远程写命令的**调用点**（阶段 288；M5 的"设备真的执行"）
 *
 * 顺序是契约的一部分（每一步失败都**不写变量**、都**留一条审计**）：
 *   ① ACL（方向 + 权限位，§286）
 *   ② 信封解析（id/ts/ttl/target，§285）
 *   ③ 新鲜度（过期/来自未来 ⇒ 拒）
 *   ④ target 存在性（宿主查表后**按值传元数据**；NULL = 不存在）
 *   ⑤ 只读 / 类型量程（§287 值校验）
 *   ⑥ **幂等台账**（通过前五步才收 —— 被拒的命令不进台账，§285 语义）
 *   ⑦ 写变量
 *   ⑧ 审计（成功与拒绝都写；成功那条含**旧值→新值**）
 *
 * 宿主依赖全部走 `hmi_cmd_host_t` 回调（可注入假实现 ⇒ 主机侧可判）。
 */
#ifndef HMI_MQTT_CMD_DISPATCH_H
#define HMI_MQTT_CMD_DISPATCH_H

#include "mqtt_acl_contract.h"
#include "mqtt_cmd_contract.h"
#include "mqtt_cmd_ledger.h"

/* 目标（变量）元数据 —— 设备侧从采纳的标签表（tag_meta_t）取 */
typedef struct {
    const char* target;
    int         writable;      /* 0 = 只读 */
    int         has_range;     /* 0 = 不限量程（Float/未知类型） */
    double      min_value;
    double      max_value;
} hmi_cmd_target_t;

/* 宿主能力（设备侧接真实变量表；主机测试接假实现）。
 * ⚠ **不做 lookup 回调**：target 元数据由调用方查好、**按值传进** `hmi_cmd_handle`
 *   —— 设备侧本来就要查采纳的标签表（`tag_meta_t`），多一层"回调返回结构体指针"
 *   只增加跨编译单元的耦合面（首版这么写时遇过一次极难定位的返回指针问题，
 *   换成按值传后立刻正常；契约上也不需要这一层）。 */
typedef struct {
    int (*get_value)(const char* target, double* out);          /* 旧值（工程值） */
    int (*set_value)(const char* target, double value);         /* 写入 */
    void (*audit)(const char* event, const char* by, const char* detail);
} hmi_cmd_io_t;

/* 结果（判据与审计共用）；code 为稳定码（见 mqtt_acl_contract.h / mqtt_write_guard.h / 下面 4 个） */
typedef struct {
    const char* code;         /* NULL = 成功 */
    const char* target;
    double      old_value;
    double      new_value;
    int         wrote;        /* 1 = 真的调用了 set_value 且成功 */
    int         ledger_used;  /* 1 = 台账收下了这个 id */
} hmi_cmd_result_t;

#define HMI_CMD_DENIED_NOT_FOUND   "target-not-found"
#define HMI_CMD_DENIED_WRITE_FAILED "write-failed"
#define HMI_CMD_OK                 NULL

/*
 * 处理一条 `/cmd/<target>` 报文。返回 0 = 已执行写入；-1 = 拒绝（`out->code` 给出原因）。
 * ⚠ `out` 必须非空；`host`/`ledger` 任一为空 ⇒ -1（`bad-arg`，不算"命令被拒"）。
 */
int hmi_cmd_handle(const char* topic, const char* prefix, const char* payload,
                   unsigned long long now_ms, unsigned perm_mask,
                   hmi_mqtt_cmd_ledger_t* ledger,
                   const hmi_cmd_target_t* target,   /* 宿主查表结果；NULL = 不存在 */
                   const hmi_cmd_io_t* io,           /* 旧值/写入/审计 */
                   hmi_cmd_result_t* out);

#endif /* HMI_MQTT_CMD_DISPATCH_H */
