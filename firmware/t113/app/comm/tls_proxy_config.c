#include "tls_proxy_config.h"
#include <stdio.h>
#include <string.h>

/* 值首尾空白裁剪后放入 out（容量 cap）—— 与 comm_core.c 的 cfg_set 同口径。 */
static void trim_copy(char* out, size_t cap, const char* value)
{
    if (out == NULL || cap == 0 || value == NULL) return;
    while (*value == ' ' || *value == '\t') value++;
    size_t len = strlen(value);
    while (len > 0 && (value[len - 1] == ' ' || value[len - 1] == '\t' ||
                       value[len - 1] == '\r' || value[len - 1] == '\n'))
        len--;
    if (len >= cap) len = cap - 1;
    memcpy(out, value, len);
    out[len] = '\0';
}

static void reset_out(hmi_tls_proxy_config_t* out)
{
    out->mode = HMI_MQTT_TLS_OFF;
    out->listen_host[0] = '\0';
    out->listen_port = 0;
    out->upstream_host[0] = '\0';
    out->upstream_port = 0;
    out->psk_id[0] = '\0';
    out->psk_file[0] = '\0';
    out->ca_path[0] = '\0';
    out->status = HMI_TLS_PROXY_EXIT_BAD_CONFIG;
    out->reason = "tls-config-incomplete";
}

int hmi_tls_proxy_resolve(const char* mode_text, const char* listen_port_text,
                          const char* upstream_host, const char* upstream_port_text,
                          const char* psk_id, const char* psk_file, const char* ca_path,
                          hmi_tls_proxy_config_t* out)
{
    if (out == NULL) return HMI_TLS_PROXY_EXIT_BAD_CONFIG;
    reset_out(out);

    /* 缺键 / 空串 ⇒ off ⇒ **不需要代理**（正常跳过，不是错误）。
     * 注意这与"模式串不认识"是两件事：后者是配置写错 ⇒ 拒绝。 */
    hmi_mqtt_tls_mode_t mode = HMI_MQTT_TLS_OFF;
    if (mode_text == NULL || mode_text[0] == '\0') {
        out->status = HMI_TLS_PROXY_EXIT_NOT_NEEDED;
        out->reason = "tls-proxy-not-needed";
        return out->status;
    }
    if (hmi_mqtt_tls_parse_mode(mode_text, &mode) != 0) {
        out->reason = "tls-mode-unknown";
        return out->status;                       /* 4 */
    }
    out->mode = mode;

    /* 显式 `off` 与"缺键"同义：客户端直连 broker ⇒ **不需要代理**（正常跳过）。
     * ⚠ 与"模式串不认识"严格区分：后者是配置写错 ⇒ 拒绝（上面那支）。 */
    if (mode == HMI_MQTT_TLS_OFF) {
        out->status = HMI_TLS_PROXY_EXIT_NOT_NEEDED;
        out->reason = "tls-proxy-not-needed";
        return out->status;                       /* 3 */
    }

    if (hmi_mqtt_tls_validate(mode, psk_id, psk_file, ca_path) != 0) {
        out->reason = "tls-config-incomplete";
        return out->status;                       /* 4 */
    }

    unsigned upstream_port = 0;
    if (upstream_host == NULL || upstream_host[0] == '\0' ||
        hmi_mqtt_tls_parse_port(upstream_port_text, HMI_TLS_PROXY_DEFAULT_UPSTREAM_PORT,
                                &upstream_port) != 0) {
        out->reason = "tls-endpoint-invalid";
        return out->status;                       /* 4 */
    }

    unsigned listen_port = 0;
    if (hmi_mqtt_tls_parse_port(listen_port_text, HMI_TLS_PROXY_DEFAULT_LISTEN_PORT,
                                &listen_port) != 0) {
        out->reason = "tls-endpoint-invalid";
        return out->status;                       /* 4 */
    }

    /* 到这里全部合法：**监听地址写死回环**（没有任何键能改它） */
    trim_copy(out->listen_host, sizeof(out->listen_host), HMI_TLS_PROXY_LISTEN_HOST);
    out->listen_port = listen_port;
    trim_copy(out->upstream_host, sizeof(out->upstream_host), upstream_host);
    out->upstream_port = upstream_port;
    trim_copy(out->psk_id, sizeof(out->psk_id), psk_id);
    trim_copy(out->psk_file, sizeof(out->psk_file), psk_file);
    trim_copy(out->ca_path, sizeof(out->ca_path), ca_path);
    out->status = HMI_TLS_PROXY_EXIT_OK;
    out->reason = "";
    return out->status;
}

int hmi_tls_proxy_config_load(const char* ini_path, hmi_tls_proxy_config_t* out)
{
    if (out == NULL) return HMI_TLS_PROXY_EXIT_BAD_CONFIG;

    char mode[16] = "", listen_port[8] = "", upstream_host[64] = "";
    char upstream_port[8] = "", psk_id[64] = "", psk_file[128] = "", ca_path[128] = "";

    if (ini_path != NULL) {
        FILE* f = fopen(ini_path, "r");
        if (f != NULL) {
            char line[256];
            while (fgets(line, sizeof(line), f)) {
                char* eq = strchr(line, '=');
                if (eq == NULL) continue;
                *eq = '\0';
                char* key = line;
                while (*key == ' ' || *key == '\t') key++;
                char* ke = key + strlen(key);
                while (ke > key && (ke[-1] == ' ' || ke[-1] == '\t')) *--ke = '\0';

                const char* value = eq + 1;
                if (strcmp(key, "mqtt.tls") == 0)                  trim_copy(mode, sizeof(mode), value);
                else if (strcmp(key, "mqtt.tls.local_port") == 0)   trim_copy(listen_port, sizeof(listen_port), value);
                else if (strcmp(key, "mqtt.tls.upstream_host") == 0) trim_copy(upstream_host, sizeof(upstream_host), value);
                else if (strcmp(key, "mqtt.tls.upstream_port") == 0) trim_copy(upstream_port, sizeof(upstream_port), value);
                else if (strcmp(key, "mqtt.tls.psk_id") == 0)       trim_copy(psk_id, sizeof(psk_id), value);
                else if (strcmp(key, "mqtt.tls.psk_file") == 0)     trim_copy(psk_file, sizeof(psk_file), value);
                else if (strcmp(key, "mqtt.tls.ca_path") == 0)      trim_copy(ca_path, sizeof(ca_path), value);
            }
            fclose(f);
        }
    }

    return hmi_tls_proxy_resolve(mode[0] ? mode : NULL,
                                 listen_port[0] ? listen_port : NULL,
                                 upstream_host[0] ? upstream_host : NULL,
                                 upstream_port[0] ? upstream_port : NULL,
                                 psk_id[0] ? psk_id : NULL,
                                 psk_file[0] ? psk_file : NULL,
                                 ca_path[0] ? ca_path : NULL,
                                 out);
}
