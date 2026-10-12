/*
 * mqtt_acl_contract.h — MQTT **主题级 ACL**（阶段 286；提案 §4「授权」那一行）
 *
 * 规则一句话：**设备只往 `<prefix>/{value,alarm,status}` 写（上行），远程端只往 `<prefix>/cmd/` 写（下行）**；
 * 每个方向上的每类主题各自要求一个**权限位**（复用编辑器生成的 `HMI_PERM_*`，与 §275 的搁置权限同源）。
 *
 * 主题形状（与 M1/M2 同族）：
 *   <prefix>/value/<var>       上行数据（远程**只读**）
 *   <prefix>/alarm/<id>        上行报警（远程**只读**）
 *   <prefix>/status            上行状态/LWT（远程**只读**）
 *   <prefix>/cmd/<target>      下行命令（**设备只读**）；target 决定所需权限：
 *                                 ack/<id>      ⇒ HMI_PERM_ACKNOWLEDGE_ALARM
 *                                 shelve/<id>   ⇒ HMI_PERM_SHELVE_ALARM
 *                                 unshelve/<id> ⇒ HMI_PERM_SHELVE_ALARM
 *                                 其它          ⇒ HMI_PERM_OPERATE_CONTROL
 *
 * 三条硬规则：
 *   A1 **方向不能反**：远程端往 value/alarm/status 写 ⇒ 拒绝（伪造遥测/报警比不加密更危险）；
 *   A2 **未授权即拒**：所需权限位不在主体掩码里 ⇒ 拒绝；
 *   A3 **不认识的主题即拒**（前缀不符/形状不符/空目标 ⇒ 拒绝，绝不"默认放行"）。
 */
#ifndef HMI_MQTT_ACL_CONTRACT_H
#define HMI_MQTT_ACL_CONTRACT_H

#include <stddef.h>

typedef enum {
    HMI_MQTT_TOPIC_UNKNOWN = 0,
    HMI_MQTT_TOPIC_VALUE   = 1,
    HMI_MQTT_TOPIC_ALARM   = 2,
    HMI_MQTT_TOPIC_STATUS  = 3,
    HMI_MQTT_TOPIC_CMD     = 4
} hmi_mqtt_topic_kind_t;

/* 主题分类：prefix 必须逐字符前缀匹配（不是"包含"）；<prefix>/value/ 等后缀也判形状 */
hmi_mqtt_topic_kind_t hmi_mqtt_topic_kind(const char* topic, const char* prefix);

/*
 * 该类主题在**该方向**上要求的权限位（is_write: 1=写/发布, 0=读/订阅）。
 * 返回 0 表示"这个方向根本不允许"（A1）；返回 (unsigned)-1 表示主题不认识（A3）。
 */
unsigned hmi_mqtt_acl_required_perm(hmi_mqtt_topic_kind_t kind, int is_write, const char* target);

/* 端到端判定：0=允许，-1=拒绝（A1/A2/A3 任一）。reason 非空时写稳定码字符串。 */
int hmi_mqtt_acl_allows(const char* topic, const char* prefix, int is_write,
                        unsigned perm_mask, const char** reason);

/* 稳定码（判据与审计用） */
#define HMI_MQTT_ACL_UNKNOWN_TOPIC  "unknown-topic"
#define HMI_MQTT_ACL_WRONG_DIRECTION "wrong-direction"
#define HMI_MQTT_ACL_FORBIDDEN      "forbidden"

#endif /* HMI_MQTT_ACL_CONTRACT_H */
