/*
 * tls_proxy_config.h — 本机 TLS 终止代理的**配置契约**（阶段 301）
 *
 * 上游：docs/2026-10-01-local-tls-proxy-contract.md（拓扑与键）、
 *       docs/2026-10-01-device-tls-assessment.md（结论：本机终止代理 ＋ PSK/X.509 **都做**）。
 * 用户拍板（2026-10-11，登记册 §0.2）：§7-1 **两者都做**（PSK ＋ X.509）；§7-2 **永不降级**。
 *
 * 分工（契约 §1）：`mqtt.host`/`mqtt.port` 是**客户端**读的；`mqtt.tls.upstream_host`/
 * `upstream_port` 是**代理**读的。两侧各读各的，没有交叠。
 *
 * 本文件只做**决定与校验**（纯函数 ＋ 一个文件读取入口）；真正的 TLS 握手在 tls_proxy.c 里。
 */
#ifndef HMI_TLS_PROXY_CONFIG_H
#define HMI_TLS_PROXY_CONFIG_H

#include <stddef.h>
#include "mqtt_tls_contract.h"

/* ── 退出码契约（init 脚本与 `--check` 消费；**数值别改**，脚本按数值分派）──
 *   0  配置可用 ⇒ 可以启动
 *   3  `mqtt.tls=off` ⇒ **不需要**代理（脚本视为正常跳过，不是错误）
 *   4  配置非法 ⇒ **不启动** 代理 ＋ 审计一条（**绝不降级**：不许改成直连 broker）
 */
#define HMI_TLS_PROXY_EXIT_OK         0
#define HMI_TLS_PROXY_EXIT_NOT_NEEDED 3
#define HMI_TLS_PROXY_EXIT_BAD_CONFIG 4

/* 明文侧监听地址**恒为回环**：绑 0.0.0.0 等于把 LAN 的明文 MQTT 暴露出去
 * —— 契约 §4 明说"比不做 TLS 更危险"，所以这里**没有**任何键能改它。 */
#define HMI_TLS_PROXY_LISTEN_HOST "127.0.0.1"
/* 明文侧监听端口缺省（同 HMI_MQTT_TLS_DEFAULT_LOCAL_PORT） */
#define HMI_TLS_PROXY_DEFAULT_LISTEN_PORT 1883u
/* TLS 侧对端端口缺省（8883 = MQTT over TLS 惯例端口） */
#define HMI_TLS_PROXY_DEFAULT_UPSTREAM_PORT 8883u

typedef struct {
    hmi_mqtt_tls_mode_t mode;
    char     listen_host[16];   /* 恒为 HMI_TLS_PROXY_LISTEN_HOST */
    unsigned listen_port;       /* 明文侧（客户端连这里） */
    char     upstream_host[64]; /* 真 broker（TLS 对端） */
    unsigned upstream_port;
    char     psk_id[64];
    char     psk_file[128];
    char     ca_path[128];
    int      status;            /* HMI_TLS_PROXY_EXIT_* */
    const char* reason;         /* 稳定标识（落审计；**不含密钥材料**） */
} hmi_tls_proxy_config_t;

/*
 * **纯函数**：把读到的键算成"代理该怎么起 ＋ 能不能起"。
 *   返回 HMI_TLS_PROXY_EXIT_*（可直接作进程退出码）。
 *   reason 词汇：
 *     ""                      ok（status 0）
 *     "tls-proxy-not-needed"  mqtt.tls 缺键/off ⇒ 正常跳过（status 3）
 *     "tls-mode-unknown"      模式串不认识（status 4）
 *     "tls-config-incomplete" 该模式缺必需项（psk 缺 id/file；ca 缺 ca_path）（status 4）
 *     "tls-endpoint-invalid"  upstream 缺失 / 端口非法（status 4）
 *
 * ⚠ status != 0 时 `listen_port`/`upstream_port` 归零、host 清空（fail-closed）。
 */
int hmi_tls_proxy_resolve(const char* mode_text, const char* listen_port_text,
                          const char* upstream_host, const char* upstream_port_text,
                          const char* psk_id, const char* psk_file, const char* ca_path,
                          hmi_tls_proxy_config_t* out);

/*
 * 从 `comm_config.ini` 读取代理需要的键并解析（文件 I/O 只在这一处）。
 * 返回 HMI_TLS_PROXY_EXIT_*；文件不存在 ⇒ 视作"没有 TLS 键" ⇒ off ⇒ 3。
 */
int hmi_tls_proxy_config_load(const char* ini_path, hmi_tls_proxy_config_t* out);

#endif /* HMI_TLS_PROXY_CONFIG_H */
