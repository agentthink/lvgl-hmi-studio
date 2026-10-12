#include "mqtt_cmd_ledger.h"
#include <string.h>

void hmi_mqtt_cmd_ledger_init(hmi_mqtt_cmd_ledger_t* l)
{
    if (l == NULL) return;
    memset(l, 0, sizeof(*l));
}

int hmi_mqtt_cmd_ledger_count(const hmi_mqtt_cmd_ledger_t* l)
{
    return l == NULL ? 0 : l->count;
}

/* 淘汰已过期条目；返回淘汰后是否还满 */
static void ledger_evict(hmi_mqtt_cmd_ledger_t* l, unsigned long long now_ms)
{
    int w = 0;

    for (int i = 0; i < l->count; i++) {
        if (now_ms > l->expiry[i]) continue;          /* 过期 ⇒ 丢 */
        if (w != i) {
            memcpy(l->ids[w], l->ids[i], sizeof(l->ids[w]));
            l->expiry[w] = l->expiry[i];
        }
        w++;
    }
    l->count = w;
}

/* 满时丢**最先到期**的（不是最先来的）—— 与 C# CommandLedger 同语义 */
static void ledger_drop_earliest(hmi_mqtt_cmd_ledger_t* l)
{
    int victim = 0;

    for (int i = 1; i < l->count; i++)
        if (l->expiry[i] < l->expiry[victim]) victim = i;

    l->count--;
    if (victim != l->count) {
        memcpy(l->ids[victim], l->ids[l->count], sizeof(l->ids[victim]));
        l->expiry[victim] = l->expiry[l->count];
    }
}

int hmi_mqtt_cmd_ledger_accept(hmi_mqtt_cmd_ledger_t* l, const char* id,
                               unsigned long long now_ms, unsigned long long ttl_ms)
{
    size_t len;

    if (l == NULL) return -1;
    if (hmi_mqtt_cmd_is_id_valid(id) != 0) return -1;
    if (hmi_mqtt_cmd_is_ttl_valid(ttl_ms) != 0) return -1;

    ledger_evict(l, now_ms);

    for (int i = 0; i < l->count; i++)
        if (strcmp(l->ids[i], id) == 0) return 0;      /* 未过期 ⇒ 重复 */

    if (l->count >= HMI_MQTT_CMD_LEDGER_CAP) ledger_drop_earliest(l);

    len = strlen(id);
    memcpy(l->ids[l->count], id, len + 1);
    l->expiry[l->count] = now_ms + ttl_ms;
    l->count++;
    return 1;
}
