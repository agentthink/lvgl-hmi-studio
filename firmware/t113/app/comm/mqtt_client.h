/*
 * mqtt_client.h — 最小 MQTT 3.1.1 客户端（QoS0）
 *
 * 用途：HMI 遥测（发布 <pre>/val/<tag>）与远程设定（订阅 <pre>/set/<tag>）
 * 特性：
 *   - CONNECT/CONNACK、PUBLISH(QoS0)、SUBSCRIBE/SUBACK、PINGREQ/PINGRESP、DISCONNECT
 *   - 断线自动重连，重连成功后自动重订阅全部主题
 *   - 非阻塞轮询（mqtt_poll 由通信线程每 ~100ms 调用）
 */
#ifndef HMI_MQTT_CLIENT_H
#define HMI_MQTT_CLIENT_H

#include <stdint.h>
#include <stddef.h>

typedef void (*mqtt_msg_cb_t)(const char* topic, const char* payload, int len, void* user);

/* 连接建立回调：CONNACK 成功（rc=0）后调用一次，**在通信线程上下文**里
 * （回调内可安全调用 mqtt_publish —— 它不持锁）。设备侧用它做"上线即报状态"。 */
typedef void (*mqtt_connect_cb_t)(void);

/* 启动客户端（仅记录配置；实际连接由 mqtt_poll 驱动） */
int mqtt_start(const char* host, int port, const char* client_id,
               const char* user, const char* pass, int keepalive);

/* 注册订阅主题（user 原样传给消息回调）；重连后自动重订阅 */
int mqtt_register_topic(const char* topic, void* user);

/* 清空全部已注册主题（标签表采纳重建时调用，避免主题重复累积） */
void mqtt_clear_topics(void);

/* 设置统一消息分发回调（PUBLISH 命中已注册主题时触发） */
void mqtt_set_message_cb(mqtt_msg_cb_t cb);

/* 设置连接建立回调（可传 NULL 取消）。 */
void mqtt_set_connect_cb(mqtt_connect_cb_t cb);

/*
 * 遗嘱（LWT）：设备断线时由 broker **代发**这条报文 —— 远程端据此知道"设备掉了"，
 * 而不是把最后一次"在线"当成现状。契约见 mqtt_contract.h 的 `HMI_MQTT_STATUS_LWT_PAYLOAD`。
 * ⚠ 必须在**首次 CONNECT 之前**设置（每次重连都会带上它）。topic/payload 按值拷贝。
 * retain=1 时 broker 会保留它，晚加入的远程端也能立刻看到"离线"。
 * 返回 0 成功；-1 = 参数非法（空 topic / 超长）。
 */
int mqtt_set_will(const char* topic, const char* payload, int retain);

/* 取消遗嘱（后续 CONNECT 不再带 will 段）。判据需要它把状态复位。 */
void mqtt_clear_will(void);

/* 发布 QoS0 消息；未连接返回 -1 */
int mqtt_publish(const char* topic, const char* payload, int len, int retain);

/* 周期轮询：连接状态机/收发/保活/重连 */
void mqtt_poll(void);

int mqtt_is_connected(void);
void mqtt_stop(void);

#endif /* HMI_MQTT_CLIENT_H */
