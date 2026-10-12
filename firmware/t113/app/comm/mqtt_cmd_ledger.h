/*
 * mqtt_cmd_ledger.h — 设备侧**幂等台账**（阶段 287；§285 契约的设备端执行）
 *
 * 语义与编辑器侧 `CommandLedger`（C#）**同一套**：
 *   · 同一个 id 在**未过期**期间再来 ⇒ **重复**（不执行）；
 *   · 过了 `now + ttl` ⇒ 条目淘汰，同一 id 可再次使用（命令可以复用 id 重发）；
 *   · 容量有界：满了先丢**已过期**的；都未过期则丢**最先到期的**（不是最先来的）。
 * 为什么有界：远程命令是不受信输入，台账不能无限涨（内存耗尽攻击面）。
 */
#ifndef HMI_MQTT_CMD_LEDGER_H
#define HMI_MQTT_CMD_LEDGER_H

#include "mqtt_cmd_contract.h"   /* HMI_MQTT_CMD_ID_MAX */

#define HMI_MQTT_CMD_LEDGER_CAP 16

typedef struct {
    char               ids[HMI_MQTT_CMD_LEDGER_CAP][HMI_MQTT_CMD_ID_MAX + 1];
    unsigned long long expiry[HMI_MQTT_CMD_LEDGER_CAP];   /* 到期时刻（ms） */
    int                count;
} hmi_mqtt_cmd_ledger_t;

void hmi_mqtt_cmd_ledger_init(hmi_mqtt_cmd_ledger_t* l);

/* 1=收下（首次）/ 0=重复（未过期）/ -1=参数非法（台账 NULL、id 形状不合规、TTL 非法） */
int hmi_mqtt_cmd_ledger_accept(hmi_mqtt_cmd_ledger_t* l, const char* id,
                               unsigned long long now_ms, unsigned long long ttl_ms);

/* 当前条目数（判据用） */
int hmi_mqtt_cmd_ledger_count(const hmi_mqtt_cmd_ledger_t* l);

#endif /* HMI_MQTT_CMD_LEDGER_H */
