/*
 * comm_core.c — 通信模块统一入口
 *
 * 通信线程循环（~100ms 周期）：
 *   1. hmi_var_drain_writes()        消费 UI 写请求（设备下发）
 *   2. hmi_var_poll_once()           变量轮询组 round-robin 读（自动块合并）
 *   3. hmi_comm_poll()               Modbus TCP 重连 + MQTT 连接状态机
 *   4. hmi_var_mqtt_publish_scan()   MQTT 变化发布（~1s）
 */
#include "comm_core.h"
#include "runtime_config.h"
#include "modbus_rtu.h"
#include "modbus_tcp.h"
#include "mqtt_client.h"
#include "mqtt_tls_contract.h"   /* 阶段 300：TLS 决定逻辑（纯函数在那边判死） */
#include "hmi_actions.h"         /* hmi_audit：配置非法时留痕 */
#include "variable_table.h"
#include <pthread.h>
#include <ctype.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

/* IDE 导出工程时生成 generated_comm.h（src-generated/，-I src/ 可见）；缺省回退
 * runtime_config.h 硬编码宏。IDE 通讯配置（COM→/dev/ttyS<N> 映射）经此生效。 */
#if __has_include("generated_comm.h")
#include "generated_comm.h"
#endif

/* ── 运行时连接配置（2026-08-18） ──
 * IDE 下载时下发 /mnt/UDISK/hmi/comm_config.ini（CommConfigCodeGen.GenerateRuntimeConfigText
 * 生成，key=value 文本），宿主启动 hmi_comm_init 读取，改连接参数免重编固件。
 * 优先级：comm_config.ini（运行时） > generated_comm.h（编译期） > runtime_config.h 默认。
 * 仅覆盖文件里存在的键；文件整体缺失回退编译期配置。 */
#define HMI_COMM_CFG_PATH "/mnt/UDISK/hmi/comm_config.ini"

/* 主机侧单元测试支持：暴露内部函数与状态符号（HMI_UNIT_TEST 由测试编译指定）；
 * 配置文件路径指向临时文件，避免依赖设备目录 */
#ifdef HMI_UNIT_TEST
#define static
#undef HMI_COMM_CFG_PATH
#define HMI_COMM_CFG_PATH "/tmp/hmi_comm_test.ini"
#endif

static char s_cfg_rtu_dev[64] = "";
static int  s_cfg_rtu_baud = 0;
static char s_cfg_rtu_parity = 0;
static int  s_cfg_rtu_tmo = 0;
static char s_cfg_tcp_host[64] = "";
static int  s_cfg_tcp_port = 0;
static int  s_cfg_tcp_tmo = 0;
static char s_cfg_mqtt_host[64] = "";
static int  s_cfg_mqtt_port = 0;
static char s_cfg_mqtt_client[64] = "";
static char s_cfg_mqtt_user[64] = "";
static char s_cfg_mqtt_pass[64] = "";
static int  s_cfg_mqtt_keep = 0;

/* ── TLS（阶段 300；用户拍板：PSK 与 X.509 **都做**、**永不降级**）──
 * 键见 docs/2026-10-01-local-tls-proxy-contract.md §2；
 * 决定逻辑在 mqtt_tls_contract.c 的 hmi_mqtt_tls_resolve_config()（纯函数 ⇒ 主机侧穷举判死）。 */
static char s_cfg_tls_mode[16] = "";
static char s_cfg_tls_local_port[8] = "";
static char s_cfg_tls_psk_id[64] = "";
static char s_cfg_tls_psk_file[128] = "";
static char s_cfg_tls_ca_path[128] = "";
/* 最近一次"拒绝启动 MQTT"的原因（审计与日志用；"" = 没拒绝）。**不含密钥材料**。 */
static const char* s_tls_refuse_reason = "";
/* 解析后的"客户端该连的 host"缓冲（**文件作用域**：主机侧测试把 `static` 抹成空，
 * 若写成函数内 static，它会退化成局部变量 ⇒ `mqtt_host` 指向已失效的栈）。 */
static char s_eff_mqtt_host[64] = "";

/* 解析一行 "key=value"：值首尾空白裁剪，放入 out（容量 cap）。 */
static void cfg_set(char* out, size_t cap, const char* value)
{
    if (!out || cap == 0 || !value) return;
    while (*value == ' ' || *value == '\t') value++;
    size_t len = strlen(value);
    while (len > 0 && (value[len - 1] == ' ' || value[len - 1] == '\t' ||
                       value[len - 1] == '\r' || value[len - 1] == '\n'))
        len--;
    if (len >= cap) len = cap - 1;
    memcpy(out, value, len);
    out[len] = '\0';
}

static void comm_config_load(void)
{
    FILE* f = fopen(HMI_COMM_CFG_PATH, "r");
    if (!f) return;   /* 无配置文件：回退编译期默认 */
    fprintf(stderr, "[comm] reading runtime config %s\n", HMI_COMM_CFG_PATH);
    char line[160];
    while (fgets(line, sizeof(line), f)) {
        char* eq = strchr(line, '=');
        if (!eq) continue;
        *eq = '\0';
        char* key = line;
        while (*key == ' ' || *key == '\t') key++;
        /* 裁剪 key 尾部空白（容忍 "tcp.host = ..." 写法） */
        char* ke = key + strlen(key);
        while (ke > key && (ke[-1] == ' ' || ke[-1] == '\t')) *--ke = '\0';
        const char* value = eq + 1;
        if (strcmp(key, "rtu.dev") == 0)            cfg_set(s_cfg_rtu_dev, sizeof(s_cfg_rtu_dev), value);
        else if (strcmp(key, "rtu.baud") == 0)      s_cfg_rtu_baud = atoi(value);
        else if (strcmp(key, "rtu.parity") == 0)    s_cfg_rtu_parity = (char)toupper((unsigned char)value[0]);
        else if (strcmp(key, "rtu.tmo_ms") == 0)    s_cfg_rtu_tmo = atoi(value);
        else if (strcmp(key, "tcp.host") == 0)      cfg_set(s_cfg_tcp_host, sizeof(s_cfg_tcp_host), value);
        else if (strcmp(key, "tcp.port") == 0)      s_cfg_tcp_port = atoi(value);
        else if (strcmp(key, "tcp.tmo_ms") == 0)    s_cfg_tcp_tmo = atoi(value);
        else if (strcmp(key, "mqtt.host") == 0)     cfg_set(s_cfg_mqtt_host, sizeof(s_cfg_mqtt_host), value);
        else if (strcmp(key, "mqtt.port") == 0)     s_cfg_mqtt_port = atoi(value);
        else if (strcmp(key, "mqtt.client_id") == 0) cfg_set(s_cfg_mqtt_client, sizeof(s_cfg_mqtt_client), value);
        else if (strcmp(key, "mqtt.username") == 0) cfg_set(s_cfg_mqtt_user, sizeof(s_cfg_mqtt_user), value);
        else if (strcmp(key, "mqtt.password") == 0) cfg_set(s_cfg_mqtt_pass, sizeof(s_cfg_mqtt_pass), value);
        else if (strcmp(key, "mqtt.keepalive") == 0) s_cfg_mqtt_keep = atoi(value);
        else if (strcmp(key, "mqtt.tls") == 0)            cfg_set(s_cfg_tls_mode, sizeof(s_cfg_tls_mode), value);
        else if (strcmp(key, "mqtt.tls.local_port") == 0) cfg_set(s_cfg_tls_local_port, sizeof(s_cfg_tls_local_port), value);
        else if (strcmp(key, "mqtt.tls.psk_id") == 0)     cfg_set(s_cfg_tls_psk_id, sizeof(s_cfg_tls_psk_id), value);
        else if (strcmp(key, "mqtt.tls.psk_file") == 0)   cfg_set(s_cfg_tls_psk_file, sizeof(s_cfg_tls_psk_file), value);
        else if (strcmp(key, "mqtt.tls.ca_path") == 0)    cfg_set(s_cfg_tls_ca_path, sizeof(s_cfg_tls_ca_path), value);
    }
    fclose(f);
    fprintf(stderr, "[comm] rtu=%s:%d%c tcp=%s:%d mqtt=%s:%d (runtime overrides)\n",
            s_cfg_rtu_dev[0] ? s_cfg_rtu_dev : "(default)",
            s_cfg_rtu_baud, s_cfg_rtu_parity ? s_cfg_rtu_parity : '?',
            s_cfg_tcp_host[0] ? s_cfg_tcp_host : "(default)", s_cfg_tcp_port,
            s_cfg_mqtt_host[0] ? s_cfg_mqtt_host : "(default)", s_cfg_mqtt_port);
}

/* ── 配置非法时的**统一处置**：日志 ＋ 审计一条（detail 只放原因标识，**不含密钥材料**）──
 * 抽成函数是为了可判：主机侧直接调它断言"审计事件名 + 原因"（阶段 300）。
 * 恒返回 -1，供调用方直接传播。 */
static int comm_tls_refuse_notice(const char* reason)
{
    const char* why = (reason != NULL && reason[0] != '\0') ? reason : "tls-config-invalid";
    fprintf(stderr, "[comm] MQTT 不启动：TLS 配置非法（%s）—— **绝不降级为明文**\n", why);
    hmi_audit("mqtt-tls-config-refused", "", "", why);
    return -1;
}

static pthread_t s_thread;
static volatile int s_running = 0;

/* ── 阶段 300：TLS 决定「MQTT 客户端该连哪里」 ──
 *
 * 返回 0 = 可用（out_host/out_port 填好）；-1 = **拒绝启动 MQTT**
 * （out_reason 给出原因；out_host 清空、out_port 归零 ⇒ fail-closed：
 *  调用方即便漏判返回值，也拿不到一个"能连上 broker"的地址）。
 *
 * ⚠ 这里**没有**"连不上 TLS 就换明文"的分支 —— 那是自动降级，拍板明确禁止（§7-2 ①）。
 */
static int comm_tls_effective(const char* broker_host, unsigned broker_port,
                              char* out_host, size_t out_len, unsigned* out_port,
                              const char** out_reason)
{
    hmi_mqtt_tls_resolution_t r;

    int rc = hmi_mqtt_tls_resolve_config(s_cfg_tls_mode[0] ? s_cfg_tls_mode : NULL,
                                         broker_host, broker_port,
                                         s_cfg_tls_local_port[0] ? s_cfg_tls_local_port : NULL,
                                         s_cfg_tls_psk_id[0] ? s_cfg_tls_psk_id : NULL,
                                         s_cfg_tls_psk_file[0] ? s_cfg_tls_psk_file : NULL,
                                         s_cfg_tls_ca_path[0] ? s_cfg_tls_ca_path : NULL,
                                         &r);

    if (out_reason != NULL) *out_reason = r.reason;

    if (rc != 0 || r.ok != 0) {
        if (out_host != NULL && out_len > 0) out_host[0] = '\0';
        if (out_port != NULL) *out_port = 0;
        return -1;
    }
    if (out_host == NULL || out_len == 0 || out_port == NULL) return -1;

    size_t n = strlen(r.host);
    if (n + 1 > out_len) { out_host[0] = '\0'; return -1; }
    memcpy(out_host, r.host, n + 1);
    *out_port = r.port;
    return 0;
}

/* ── 驱动映射 ── */
hmi_driver_t hmi_driver_from_name(const char* name)
{
    if (!name) return HMI_DRV_NONE;
    if (strcmp(name, "modbus-rtu") == 0) return HMI_DRV_MODBUS_RTU;
    if (strcmp(name, "modbus-tcp") == 0) return HMI_DRV_MODBUS_TCP;
    if (strcmp(name, "mqtt") == 0) return HMI_DRV_MQTT;
    return HMI_DRV_NONE;
}

/*
 * 地址解析。支持：
 *   "40001"/"30001"/"10001"/"00001"  Modbus 数据区规范
 *   "h:0"/"c:5"/"i:0"/"d:7"          显式类型前缀
 *   "3:0"/"1:7"                       显式功能码:偏移
 *
 * 契约（与 IDE 生成器 RuntimeTagsCodeGen 对齐，2026-08-18）：
 *   tag_binding_t.addr 必须是纯 Modbus 地址（"40001"）——从站号走 binding.unit。
 *   "站号:地址"（如 "1:40001"）会被下方 "%d:%d" 分支解析为 fn=站号(功能码)/off=完整地址
 *   （40001 偏移越界或错区），轮询组请求错误导致 Modbus 无读写数据（实机故障根因，
 *   IDE 已改为生成 纯地址+unit，禁止再生成该格式）。
 */
int hmi_tag_parse_addr(const char* s, uint8_t* fn, uint16_t* off)
{
    if (!s || !*s) return -1;
    int a = 0, b = 0;
    if (sscanf(s, "h:%d", &a) == 1) { *fn = MB_FC_READ_HOLDING; *off = (uint16_t)a; return 0; }
    if (sscanf(s, "c:%d", &a) == 1) { *fn = MB_FC_READ_COILS;   *off = (uint16_t)a; return 0; }
    if (sscanf(s, "i:%d", &a) == 1) { *fn = MB_FC_READ_INPUT;   *off = (uint16_t)a; return 0; }
    if (sscanf(s, "d:%d", &a) == 1) { *fn = MB_FC_READ_DISCRETE; *off = (uint16_t)a; return 0; }
    if (sscanf(s, "%d:%d", &a, &b) == 2) {
        if (a < 1 || a > 0x10 || b < 0 || b > 0xFFFF) return -1;
        *fn = (uint8_t)a;
        *off = (uint16_t)b;
        return 0;
    }
    if (sscanf(s, "%d", &a) == 1 && a > 0) {
        if (a <= 9999)      { *fn = MB_FC_READ_COILS;    *off = (uint16_t)(a - 1); }
        else if (a <= 19999){ *fn = MB_FC_READ_DISCRETE; *off = (uint16_t)(a - 10001); }
        else if (a <= 39999){ *fn = MB_FC_READ_INPUT;    *off = (uint16_t)(a - 30001); }
        else if (a <= 49999){ *fn = MB_FC_READ_HOLDING;  *off = (uint16_t)(a - 40001); }
        else                { *fn = MB_FC_READ_HOLDING;  *off = (uint16_t)a; }
        return 0;
    }
    return -1;
}

/* ── 生命周期 ── */
int hmi_comm_init(void)
{
    /* 运行时连接配置（comm_config.ini）优先；其次编译期 generated_comm.h；缺省 runtime_config.h */
    comm_config_load();

    const char* rtu_dev = HMI_MBUS_RTU_DEV;
    int  rtu_baud = HMI_MBUS_RTU_BAUD;
    char rtu_parity = HMI_MBUS_RTU_PARITY;
    int  rtu_tmo = HMI_MBUS_RTU_TMO_MS;
    const char* tcp_host = HMI_MBUS_TCP_HOST;
    int  tcp_port = HMI_MBUS_TCP_PORT;
    int  tcp_tmo = HMI_MBUS_TCP_TMO_MS;
    const char* mqtt_host = HMI_MQTT_HOST;
    int  mqtt_port = HMI_MQTT_PORT;
    const char* mqtt_client = HMI_MQTT_CLIENT_ID;
    const char* mqtt_user = HMI_MQTT_USER;
    const char* mqtt_pass = HMI_MQTT_PASS;
    int  mqtt_keep = HMI_MQTT_KEEPALIVE;
#ifdef HMI_COMM_RTU_DEV
    rtu_dev = HMI_COMM_RTU_DEV;
    rtu_baud = HMI_COMM_RTU_BAUD;
    rtu_parity = HMI_COMM_RTU_PARITY;
    rtu_tmo = HMI_COMM_RTU_TMO_MS;
#endif
#ifdef HMI_COMM_TCP_HOST
    tcp_host = HMI_COMM_TCP_HOST;
    tcp_port = HMI_COMM_TCP_PORT;
    tcp_tmo = HMI_COMM_TCP_TMO_MS;
#endif
#ifdef HMI_COMM_MQTT_HOST
    mqtt_host = HMI_COMM_MQTT_HOST;
    mqtt_port = HMI_COMM_MQTT_PORT;
    mqtt_client = HMI_COMM_MQTT_CLIENT_ID;
    mqtt_user = HMI_COMM_MQTT_USER;
    mqtt_pass = HMI_COMM_MQTT_PASS;
    mqtt_keep = HMI_COMM_MQTT_KEEPALIVE;
#endif
    /* 运行时覆盖（仅覆盖文件中存在的键） */
    if (s_cfg_rtu_dev[0])      rtu_dev = s_cfg_rtu_dev;
    if (s_cfg_rtu_baud > 0)    rtu_baud = s_cfg_rtu_baud;
    if (s_cfg_rtu_parity)      rtu_parity = s_cfg_rtu_parity;
    if (s_cfg_rtu_tmo > 0)     rtu_tmo = s_cfg_rtu_tmo;
    if (s_cfg_tcp_host[0])     tcp_host = s_cfg_tcp_host;
    if (s_cfg_tcp_port > 0)    tcp_port = s_cfg_tcp_port;
    if (s_cfg_tcp_tmo > 0)     tcp_tmo = s_cfg_tcp_tmo;
    if (s_cfg_mqtt_host[0])    mqtt_host = s_cfg_mqtt_host;
    if (s_cfg_mqtt_port > 0)   mqtt_port = s_cfg_mqtt_port;
    if (s_cfg_mqtt_client[0])  mqtt_client = s_cfg_mqtt_client;
    if (s_cfg_mqtt_user[0])    mqtt_user = s_cfg_mqtt_user;
    if (s_cfg_mqtt_pass[0])    mqtt_pass = s_cfg_mqtt_pass;
    if (s_cfg_mqtt_keep > 0)   mqtt_keep = s_cfg_mqtt_keep;

    int rc = 0;
    if (mb_rtu_init(rtu_dev, rtu_baud, rtu_parity, rtu_tmo) != 0) {
        fprintf(stderr, "[comm] modbus-rtu init failed (RS485 %s, baud %d)\n", rtu_dev, rtu_baud);
        rc = -1;   /* 不阻塞启动 */
    }
    mb_tcp_init(tcp_host, tcp_port, tcp_tmo);

    /* ── 阶段 300：TLS 配置决定 MQTT 连哪里；配置非法 ⇒ **拒绝启动 MQTT** ＋ 审计一条。
     * 注意这里**不**回退到"直连 broker"：拍板 §7-2 ① 永不降级。 ── */
    {
        char tls_host[64];
        unsigned tls_port = 0;

        if (comm_tls_effective(mqtt_host, (unsigned)mqtt_port, tls_host, sizeof(tls_host),
                               &tls_port, &s_tls_refuse_reason) != 0) {
            rc = comm_tls_refuse_notice(s_tls_refuse_reason);   /* 不阻塞启动（Modbus 等照常） */
        } else {
            memcpy(s_eff_mqtt_host, tls_host, sizeof(s_eff_mqtt_host));
            mqtt_host = s_eff_mqtt_host;
            mqtt_port = (int)tls_port;
            mqtt_start(mqtt_host, mqtt_port, mqtt_client, mqtt_user, mqtt_pass, mqtt_keep);
        }
    }
    fprintf(stderr, "[comm] rtu=%s:%d%c tcp=%s:%d mqtt=%s:%d tls=%s (effective)\n",
            rtu_dev, rtu_baud, rtu_parity ? rtu_parity : '?',
            tcp_host, tcp_port, mqtt_host, mqtt_port,
            s_cfg_tls_mode[0] ? s_cfg_tls_mode : "off");
    return rc;
}

static void* comm_thread_main(void* arg)
{
    (void)arg;
    fprintf(stderr, "[comm] thread started\n");
    while (s_running) {
        hmi_var_drain_writes();
        hmi_var_poll_once();
        hmi_comm_poll();
        hmi_var_mqtt_publish_scan();
        usleep(HMI_POLL_INTERVAL_MS * 1000);
    }
    return NULL;
}

int hmi_comm_start(void)
{
    if (s_running) return 0;
    s_running = 1;
    if (pthread_create(&s_thread, NULL, comm_thread_main, NULL) != 0) {
        s_running = 0;
        return -1;
    }
    return 0;
}

void hmi_comm_stop(void)
{
    if (!s_running) return;
    s_running = 0;
    pthread_join(s_thread, NULL);
    mb_rtu_deinit();
    mqtt_stop();
}

/* ── 轮询 ── */
void hmi_comm_poll(void)
{
    mb_tcp_poll();      /* Modbus TCP 断线重连 */
    mqtt_poll();        /* MQTT 连接状态机/收发/保活 */
}

/* ── 读写分发 ── */
int hmi_comm_read_regs(hmi_driver_t drv, uint8_t unit, uint8_t fn,
                       uint16_t off, uint16_t count, uint16_t* out)
{
    switch (drv) {
    case HMI_DRV_MODBUS_RTU: return mb_rtu_read_regs(unit, fn, off, count, out);
    case HMI_DRV_MODBUS_TCP: return mb_tcp_read_regs(unit, fn, off, count, out);
    default: return -1;
    }
}

int hmi_comm_write_reg(hmi_driver_t drv, uint8_t unit, uint8_t fn,
                       uint16_t off, uint16_t val)
{
    switch (drv) {
    case HMI_DRV_MODBUS_RTU: return mb_rtu_write_reg(unit, fn, off, val);
    case HMI_DRV_MODBUS_TCP: return mb_tcp_write_reg(unit, fn, off, val);
    default: return -1;
    }
}

int hmi_comm_write_regs(hmi_driver_t drv, uint8_t unit, uint16_t off,
                        const uint16_t* regs, uint16_t count)
{
    switch (drv) {
    case HMI_DRV_MODBUS_RTU: return mb_rtu_write_regs(unit, off, regs, count);
    case HMI_DRV_MODBUS_TCP: return mb_tcp_write_regs(unit, off, regs, count);
    default: return -1;
    }
}
