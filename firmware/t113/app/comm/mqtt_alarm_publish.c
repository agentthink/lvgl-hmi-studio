/*
 * mqtt_alarm_publish.c — 报警上行发布器（见 mqtt_alarm_publish.h 的契约与三个刻意决定）
 */
#include "mqtt_alarm_publish.h"
#include "mqtt_contract.h"
#include "mqtt_client.h"
#include "runtime_config.h"

#include <string.h>

/* 上一轮快照（只保存发布器自己的状态；不碰 .so 的报警记录） */
static hmi_alarm_snapshot_t s_prev[HMI_ALARM_PUBLISH_MAX];
static int s_prev_n = 0;

void hmi_alarm_publish_reset(void)
{
    s_prev_n = 0;
    memset(s_prev, 0, sizeof(s_prev));
}

/* 与上一轮相比 (state,severity) 是否变了；新出现的报警算变 */
static int edge_changed(const hmi_alarm_snapshot_t* cur)
{
    for (int j = 0; j < s_prev_n; j++) {
        if (strcmp(s_prev[j].id, cur->id) == 0)
            return s_prev[j].state != cur->state || s_prev[j].severity != cur->severity;
    }
    return 1;
}

int hmi_alarm_publish_scan(hmi_alarm_fetch_fn fetch, unsigned long long now_ms, int time_base_utc)
{
    hmi_alarm_snapshot_t cur[HMI_ALARM_PUBLISH_MAX];
    int n = 0, sent = 0;

    if (fetch != NULL) {
        n = fetch(cur, HMI_ALARM_PUBLISH_MAX);
        if (n < 0) n = 0;
        if (n > HMI_ALARM_PUBLISH_MAX) n = HMI_ALARM_PUBLISH_MAX;
    }

    for (int i = 0; i < n; i++) {
        if (cur[i].state == 0) continue;        /* Inactive 不是可上行状态（契约 1..4） */
        if (!edge_changed(&cur[i])) continue;   /* 边沿触发 */

        char topic[192], payload[80];
        if (hmi_mqtt_format_alarm_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE, cur[i].id) != 0)
            continue;                            /* 主题不合法（如 id 含 `/` / 超长）⇒ 跳过 */
        if (hmi_mqtt_format_alarm_payload(payload, sizeof(payload), cur[i].state, cur[i].severity,
                                          now_ms, time_base_utc) != 0)
            continue;                            /* 状态/严重度越界 ⇒ 跳过（格式化函数会拒） */
        if (mqtt_publish(topic, payload, (int)strlen(payload), 0) == 0) sent++;
    }

    /* ⚠ 快照无论发没发都要更新：否则"被跳过的那条"下一轮还会被当成边沿。 */
    memcpy(s_prev, cur, sizeof(cur[0]) * (size_t)n);
    s_prev_n = n;
    return sent;
}
