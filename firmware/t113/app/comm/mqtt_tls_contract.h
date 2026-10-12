/*
 * mqtt_tls_contract.h — MQTT 传输层安全（TLS）的**配置契约**（阶段 282；`C4=②` 的 M4 第一块）
 *
 * 评估结论见 docs/2026-10-01-device-tls-assessment.md：
 *   ① T113 生产/OTA 镜像 **libopenssl=y、libmbedtls 未选** ⇒ 设备侧终结放进**代理**、不改 mqtt_client.c；
 *   ② 设备**无 RTC** ⇒ X.509 有效期不可靠 ⇒ **PSK 优先**；
 *   ③ 红线：**绝不静默降级为明文**。
 *
 * 本文件只做**契约与校验**（纯函数）：解析 `comm_config.ini` 里的三个键并判定"这组配置能不能用"。
 * 真正的 TLS 握手在代理侧，不在本层。
 *
 * 键（写在 `/mnt/UDISK/hmi/comm_config.ini`，与 mqtt.host/port 同一处）：
 *   mqtt.tls        = off | psk | ca        （缺省 ⇒ off，**向后兼容**：老工程不带这个键照旧）
 *   mqtt.tls.psk_id = PSK 身份（psk 模式必需）
 *   mqtt.tls.psk_file = PSK 密钥文件路径（psk 模式必需；**密钥不进 ini**，单独文件便于设 0600）
 *   mqtt.tls.ca_path  = CA 链文件路径（ca 模式必需）
 *
 * 返回值约定（与 mqtt_contract v1 同族）：
 *    0 成功
 *   -1 参数非法（NULL / 未知模式 / 该模式缺必需项）
 */
#ifndef HMI_MQTT_TLS_CONTRACT_H
#define HMI_MQTT_TLS_CONTRACT_H

#include <stddef.h>

/* 本机终止代理的回环地址（明文监听**只许绑这个地址** —— 见 §284 契约） */
#define HMI_MQTT_TLS_LOCALHOST "127.0.0.1"
/* 代理默认监听端口（INI 缺 `mqtt.tls.local_port` 时用） */
#define HMI_MQTT_TLS_DEFAULT_LOCAL_PORT 1883u

typedef enum {
    HMI_MQTT_TLS_OFF = 0,   /* 明文（**只有显式配置**才允许 —— 绝不自动降级到这里） */
    HMI_MQTT_TLS_PSK = 1,   /* 预共享密钥：无证书、无时钟依赖（推荐首选） */
    HMI_MQTT_TLS_CA  = 2    /* X.509：需要正确的时钟（设备无 RTC ⇒ 必须先校时） */
} hmi_mqtt_tls_mode_t;

/* 解析模式串：只认小写 "off"/"psk"/"ca"（大小写敏感 —— 契约只留一种写法，免得两侧归一化规则分叉） */
int hmi_mqtt_tls_parse_mode(const char* text, hmi_mqtt_tls_mode_t* out);

/* mode → token（"off"/"psk"/"ca"；未知 ⇒ "off" 的 token? 不 —— 未知返回 NULL，逼调用方先 parse） */
const char* hmi_mqtt_tls_mode_token(hmi_mqtt_tls_mode_t mode);

/* 校验必需项：psk ⇒ psk_id 且 psk_file 都非空；ca ⇒ ca_path 非空；off ⇒ 忽略其余。
 * ⚠ **缺件就是失败**（返回 -1）：绝不允许"缺证书 ⇒ 退回明文"。 */
int hmi_mqtt_tls_validate(hmi_mqtt_tls_mode_t mode, const char* psk_id,
                          const char* psk_file, const char* ca_path);

/* 端口文本 → 端口号：缺省/空串 ⇒ *out = default_port 并返回 0；非法（非数字 / 0 / >65535）⇒ -1 */
int hmi_mqtt_tls_parse_port(const char* text, unsigned default_port, unsigned* out);

/*
 * **MQTT 客户端该连哪里**（阶段 284；本机终止代理的核心决定）：
 *   mode = off      ⇒ 直连 broker：out_host = broker_host, out_port = broker_port；
 *   mode = psk/ca   ⇒ 连**本机代理**：out_host = HMI_MQTT_TLS_LOCALHOST,
 *                     out_port = （tls_local_port 文本解析，缺省 HMI_MQTT_TLS_DEFAULT_LOCAL_PORT）。
 * ⚠ TLS 开着时**绝不允许**把 broker_host 交给客户端（那就是绕过代理 ⇒ 明文上行）。
 * 返回 0 成功 / -1 参数非法（broker_host 空、端口非法、缓冲区不够）。
 */
int hmi_mqtt_tls_resolve_endpoint(hmi_mqtt_tls_mode_t mode, const char* broker_host,
                                  unsigned broker_port, const char* tls_local_port,
                                  char* out_host, size_t out_len, unsigned* out_port);

/*
 * **不许绕过代理**的断言（给"从别处读了个 host 就直连"的调用点用）：
 *   mode = off ⇒ 恒 0（此时直连 broker 是**显式配置**的结果）；
 *   mode = psk/ca ⇒ 仅当 client_host 是回环（127.0.0.1 / ::1 / localhost）才 0，否则 -1。
 */
int hmi_mqtt_tls_assert_no_bypass(hmi_mqtt_tls_mode_t mode, const char* client_host);

/* ── 阶段 300：把"读到的键"一次算成「客户端该连哪里 ＋ 这组配置能不能用」 ──
 *
 * 为什么要这一层：`comm_core.c` 读 INI 是文件 I/O（判不了）；把**决定**抽成纯函数之后，
 * 「缺件就拒绝启动、绝不降级」才能被主机侧穷举判死（与阶段 282~284 的契约同族）。
 *
 * `reason` 是**稳定的 ASCII 标识**（落审计；**不含任何密钥材料**）：
 *   ""                       ok
 *   "tls-mode-unknown"       模式串不认识 —— **不许**当 off（静默降级），直接拒绝
 *   "tls-config-incomplete"  该模式缺必需项（psk 缺 id/file；ca 缺 ca_path）
 *   "tls-endpoint-invalid"   broker 缺失 / 本地端口非法
 *   "tls-bypass"             解析出的客户端 host 不是回环 —— **绝不允许**
 *
 * 失败时 `host` 被清空、`port` 归零（fail-closed：调用方即便漏判 `ok` 也连不到 broker）。
 * 返回 0 成功（`out->ok == 0`）/ -1 参数或配置非法（`out->reason` 给出原因）。
 *
 * `mode_text` 为 NULL 或空串 ⇒ **off**（缺键 = 老工程，向后兼容，不是降级）。
 */
typedef struct {
    hmi_mqtt_tls_mode_t mode;
    char     host[64];    /* 客户端该连的 host */
    unsigned port;        /* 客户端该连的 port */
    int      ok;          /* 0 = 可用；-1 = 配置非法 ⇒ **拒绝启动 MQTT**（不是降级） */
    const char* reason;   /* 见上；ok = 0 时指向 "" */
} hmi_mqtt_tls_resolution_t;

int hmi_mqtt_tls_resolve_config(const char* mode_text, const char* broker_host, unsigned broker_port,
                                const char* local_port_text, const char* psk_id,
                                const char* psk_file, const char* ca_path,
                                hmi_mqtt_tls_resolution_t* out);

#endif /* HMI_MQTT_TLS_CONTRACT_H */
