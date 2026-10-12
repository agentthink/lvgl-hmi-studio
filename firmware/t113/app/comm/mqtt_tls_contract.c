#include "mqtt_tls_contract.h"
#include <string.h>

int hmi_mqtt_tls_parse_mode(const char* text, hmi_mqtt_tls_mode_t* out)
{
    if (text == NULL || out == NULL) return -1;
    if (strcmp(text, "off") == 0) { *out = HMI_MQTT_TLS_OFF; return 0; }
    if (strcmp(text, "psk") == 0) { *out = HMI_MQTT_TLS_PSK; return 0; }
    if (strcmp(text, "ca")  == 0) { *out = HMI_MQTT_TLS_CA;  return 0; }
    return -1;                       /* 未知模式**不许**当 off —— 那是静默降级 */
}

const char* hmi_mqtt_tls_mode_token(hmi_mqtt_tls_mode_t mode)
{
    switch (mode) {
        case HMI_MQTT_TLS_OFF: return "off";
        case HMI_MQTT_TLS_PSK: return "psk";
        case HMI_MQTT_TLS_CA:  return "ca";
        default: return NULL;
    }
}

int hmi_mqtt_tls_validate(hmi_mqtt_tls_mode_t mode, const char* psk_id,
                          const char* psk_file, const char* ca_path)
{
    switch (mode) {
        case HMI_MQTT_TLS_OFF:
            return 0;                /* 明文：显式配置的结果，其余键忽略 */
        case HMI_MQTT_TLS_PSK:
            if (psk_id == NULL || psk_id[0] == '\0') return -1;
            if (psk_file == NULL || psk_file[0] == '\0') return -1;
            return 0;
        case HMI_MQTT_TLS_CA:
            if (ca_path == NULL || ca_path[0] == '\0') return -1;
            return 0;
        default:
            return -1;
    }
}

int hmi_mqtt_tls_parse_port(const char* text, unsigned default_port, unsigned* out)
{
    unsigned v = 0;

    if (out == NULL) return -1;
    if (text == NULL || text[0] == '\0') { *out = default_port; return 0; }

    for (const char* p = text; *p != '\0'; p++) {
        if (*p < '0' || *p > '9') return -1;      /* 不许 +、空格、进制前缀 */
        v = v * 10u + (unsigned)(*p - '0');
        if (v > 65535u) return -1;
    }
    if (v == 0) return -1;                        /* 0 号端口不是"缺省"，是写错 */
    *out = v;
    return 0;
}

static int tls_copy_host(const char* src, char* out, size_t out_len)
{
    size_t n = 0;

    if (src == NULL || out == NULL || out_len == 0) return -1;
    while (src[n] != '\0') {
        if (n + 1 >= out_len) return -1;          /* 缓冲区不够 —— 截断比报错更危险 */
        n++;
    }
    for (size_t i = 0; i <= n; i++) out[i] = src[i];
    return 0;
}

int hmi_mqtt_tls_resolve_endpoint(hmi_mqtt_tls_mode_t mode, const char* broker_host,
                                  unsigned broker_port, const char* tls_local_port,
                                  char* out_host, size_t out_len, unsigned* out_port)
{
    if (out_port == NULL) return -1;

    if (mode == HMI_MQTT_TLS_OFF) {
        if (broker_host == NULL || broker_host[0] == '\0') return -1;
        if (broker_port == 0) return -1;
        if (tls_copy_host(broker_host, out_host, out_len) != 0) return -1;
        *out_port = broker_port;
        return 0;
    }

    if (mode != HMI_MQTT_TLS_PSK && mode != HMI_MQTT_TLS_CA) return -1;

    /* ⚠ 即便 TLS 开着，broker_host 也不能空：它是**代理的上游**，缺了就是配置不全
     *   （让客户端连回环、代理再失败，会把"没配 broker"变成一条看不懂的运行时故障） */
    if (broker_host == NULL || broker_host[0] == '\0') return -1;

    /* TLS 开着 ⇒ 客户端只能连本机代理（broker_host/broker_port 交给代理用，不进客户端） */
    unsigned local = 0;
    if (hmi_mqtt_tls_parse_port(tls_local_port, HMI_MQTT_TLS_DEFAULT_LOCAL_PORT, &local) != 0) return -1;
    if (tls_copy_host(HMI_MQTT_TLS_LOCALHOST, out_host, out_len) != 0) return -1;
    *out_port = local;
    return 0;
}

int hmi_mqtt_tls_assert_no_bypass(hmi_mqtt_tls_mode_t mode, const char* client_host)
{
    if (mode == HMI_MQTT_TLS_OFF) return 0;
    if (mode != HMI_MQTT_TLS_PSK && mode != HMI_MQTT_TLS_CA) return -1;
    if (client_host == NULL) return -1;

    return (strcmp(client_host, "127.0.0.1") == 0 ||
            strcmp(client_host, "::1") == 0 ||
            strcmp(client_host, "localhost") == 0) ? 0 : -1;
}

int hmi_mqtt_tls_resolve_config(const char* mode_text, const char* broker_host, unsigned broker_port,
                                const char* local_port_text, const char* psk_id,
                                const char* psk_file, const char* ca_path,
                                hmi_mqtt_tls_resolution_t* out)
{
    if (out == NULL) return -1;

    out->mode   = HMI_MQTT_TLS_OFF;
    out->host[0] = '\0';
    out->port   = 0;
    out->ok     = -1;
    out->reason = "tls-mode-unknown";

    /* 缺键 / 空串 ⇒ off：这是**向后兼容**（老工程不带这个键），不是降级。 */
    hmi_mqtt_tls_mode_t mode = HMI_MQTT_TLS_OFF;
    if (mode_text != NULL && mode_text[0] != '\0') {
        if (hmi_mqtt_tls_parse_mode(mode_text, &mode) != 0) {
            /* ⚠ 记 mode=OFF 只为让调用方的日志有话说；**不要**拿它去连 —— ok 已经是 -1。 */
            out->reason = "tls-mode-unknown";
            return -1;
        }
    }
    out->mode = mode;

    if (hmi_mqtt_tls_validate(mode, psk_id, psk_file, ca_path) != 0) {
        out->reason = "tls-config-incomplete";
        return -1;
    }

    unsigned port = 0;
    if (hmi_mqtt_tls_resolve_endpoint(mode, broker_host, broker_port, local_port_text,
                                      out->host, sizeof(out->host), &port) != 0) {
        out->host[0] = '\0';
        out->reason  = "tls-endpoint-invalid";
        return -1;
    }

    /* 最后一道：把 R2 钉在**运行期路径**上（而不是只在测试里断言）。 */
    if (hmi_mqtt_tls_assert_no_bypass(mode, out->host) != 0) {
        out->host[0] = '\0';
        out->port    = 0;
        out->reason  = "tls-bypass";
        return -1;
    }

    out->port   = port;
    out->ok     = 0;
    out->reason = "";
    return 0;
}
