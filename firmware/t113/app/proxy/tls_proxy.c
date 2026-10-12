/*
 * tls_proxy.c — 本机 TLS 终止代理（阶段 302；路线 B 的"唯一新代码"）
 *
 * 上游：docs/2026-10-01-device-tls-assessment.md（结论：本机终止代理 ＋ PSK/X.509 都做）
 *       docs/2026-10-01-local-tls-proxy-contract.md（拓扑 / 键 / 进程要求 / 留痕）
 * 用户拍板（2026-10-11）：§7-1 **两者都做**；§7-2 **永不降级**。
 *
 * 拓扑：
 *   hmi_runtime ──明文 MQTT──▶ 127.0.0.1:<local_port> ──本进程──TLS──▶ upstream(broker)
 *
 * 用法：
 *   hmi-tls-proxy --check [--config PATH]      只校验配置，按 0 / 3 / 4 退出（init 脚本用）
 *   hmi-tls-proxy [--config PATH] [--once]     起代理（--once = 处理一个连接后退出，供测试）
 *
 * 红线（契约 §4/§5）：
 *   · 明文监听**只绑回环**（地址由契约写死，代码这里也不接受任何覆盖）
 *   · 握手失败 ⇒ **关闭连接 ＋ 审计**，绝不改成明文上行（不降级）
 *   · 配置非法 ⇒ **不启动** ＋ 审计
 *   · 审计 detail 只放**原因标识**，不含密钥材料
 *
 * 测试缝（只为判据存在，设备上不需要设）：
 *   HMI_TLS_PROXY_AUDIT      审计文件路径（缺省 /mnt/UDISK/hmi/audit.log）
 *   HMI_TLS_PROXY_CONFIG     配置文件路径（缺省 /mnt/UDISK/hmi/comm_config.ini）
 *   HMI_TLS_PROXY_MIN_EPOCH  视作"时钟已同步"的最小 epoch（缺省 2020-01-01）——
 *                            用来在主机上模拟"无 RTC 设备开机早期"（§6-4 判据）
 */
#ifndef _GNU_SOURCE
#define _GNU_SOURCE
#endif
#include <arpa/inet.h>
#include <errno.h>
#include <netinet/in.h>
#include <poll.h>
#include <pthread.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/socket.h>
#include <time.h>
#include <unistd.h>

#include <openssl/err.h>
#include <openssl/ssl.h>
#include <openssl/x509v3.h>

#include "tls_proxy_config.h"

#ifndef HMI_TLS_PROXY_AUDIT_PATH
#define HMI_TLS_PROXY_AUDIT_PATH "/mnt/UDISK/hmi/audit.log"
#endif
#define HMI_TLS_PROXY_DEFAULT_CONFIG "/mnt/UDISK/hmi/comm_config.ini"
#define HMI_TLS_PROXY_MIN_EPOCH_DEFAULT 1577836800L /* 2020-01-01T00:00:00Z */

static volatile sig_atomic_t g_stop = 0;
static void on_signal(int sig) { (void)sig; g_stop = 1; }

/* 凭据/配置热加载（阶段 322）：SIGHUP 只**置位**，真正的重载在主循环里做 ——
   信号处理器里只许干 sig_atomic_t 赋值这种活（malloc/IO/OpenSSL 都不安全）。 */
static volatile sig_atomic_t g_reload = 0;
static void on_reload(int sig) { (void)sig; g_reload = 1; }

/* ── 审计：与 hmi_actions.c 的 hmi_audit **同一行形状**
 *    "<stamp> <event> user= role= <detail>"；detail 只放原因标识。 ── */
static void audit(const char* event, const char* detail)
{
    const char* path = getenv("HMI_TLS_PROXY_AUDIT");
    if (path == NULL || path[0] == '\0') path = HMI_TLS_PROXY_AUDIT_PATH;

    FILE* f = fopen(path, "a");
    if (f == NULL) return;

    char stamp[32];
    time_t now = time(NULL);
    struct tm tmv;
    if (gmtime_r(&now, &tmv) != NULL)
        strftime(stamp, sizeof(stamp), "%Y-%m-%dT%H:%M:%SZ", &tmv);
    else
        snprintf(stamp, sizeof(stamp), "epoch=%ld", (long)now);

    fprintf(f, "%s %s user=%s role=%s %s\n", stamp, event, "", "", detail != NULL ? detail : "");
    (void)fclose(f);
}

/* ── PSK：回调里读身份与密钥文件（十六进制；密钥**不进 ini**） ── */
static int load_psk_hex(const char* path, unsigned char* out, unsigned int* out_len)
{
    FILE* f = fopen(path, "r");
    if (f == NULL) return -1;

    char buf[256];
    size_t n = fread(buf, 1, sizeof(buf) - 1, f);
    (void)fclose(f);
    buf[n] = '\0';

    unsigned int nib = 0, val = 0, len = 0;
    for (size_t i = 0; i < n; i++) {
        char ch = buf[i];
        if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n' || ch == ':') continue;
        unsigned int d;
        if (ch >= '0' && ch <= '9') d = (unsigned int)(ch - '0');
        else if (ch >= 'a' && ch <= 'f') d = (unsigned int)(ch - 'a' + 10);
        else if (ch >= 'A' && ch <= 'F') d = (unsigned int)(ch - 'A' + 10);
        else return -1;                          /* 非十六进制 ⇒ 拒绝（不猜） */
        val = (nib == 0) ? d : (val << 4) | d;
        if (++nib == 2) {
            if (len >= *out_len) return -1;
            out[len++] = (unsigned char)val;
            nib = 0;
            val = 0;
        }
    }
    if (nib != 0) return -1;                     /* 奇数个十六进制位 ⇒ 文件坏了 */
    *out_len = len;
    return (len > 0) ? 0 : -1;
}

static unsigned int psk_client_cb(SSL* ssl, const char* hint, char* identity,
                                  unsigned int max_identity_len, unsigned char* psk,
                                  unsigned int max_psk_len)
{
    (void)hint;
    const hmi_tls_proxy_config_t* cfg = (const hmi_tls_proxy_config_t*)SSL_get_app_data(ssl);
    if (cfg == NULL) return 0;

    size_t idlen = strlen(cfg->psk_id);
    if (idlen + 1 > max_identity_len) return 0;
    memcpy(identity, cfg->psk_id, idlen + 1);

    unsigned int klen = max_psk_len;
    if (load_psk_hex(cfg->psk_file, psk, &klen) != 0) return 0;
    return klen;
}

/* ── TLS：两条路径（psk / ca）。返回 0 成功；否则已审计，调用方**关闭连接** ── */
static int tls_upstream(SSL_CTX* ctx, SSL** out_ssl, const hmi_tls_proxy_config_t* cfg, int fd)
{
    SSL* ssl = SSL_new(ctx);
    if (ssl == NULL) { audit("mqtt-tls-handshake-failed", "ssl-new"); return -1; }
    SSL_set_fd(ssl, fd);
    SSL_set_app_data(ssl, (void*)cfg);

    if (cfg->mode == HMI_MQTT_TLS_CA) {
        /* §6-4：**无 RTC 设备开机早期**不得放行 X.509（PSK 不受影响）。
         * 时间未同步 ⇒ 拒绝 ＋ 审计，绝不"跳过校验"。 */
        long min_epoch = HMI_TLS_PROXY_MIN_EPOCH_DEFAULT;
        const char* env = getenv("HMI_TLS_PROXY_MIN_EPOCH");
        if (env != NULL && env[0] != '\0') min_epoch = strtol(env, NULL, 10);
        if ((long)time(NULL) < min_epoch) {
            audit("mqtt-tls-clock-unsynced", "ca-mode");
            SSL_free(ssl);
            return -1;
        }
        if (SSL_CTX_load_verify_locations(ctx, cfg->ca_path, NULL) != 1) {
            audit("mqtt-tls-handshake-failed", "ca-load");
            SSL_free(ssl);
            return -1;
        }
        SSL_CTX_set_verify(ctx, SSL_VERIFY_PEER, NULL);
        X509_VERIFY_PARAM* param = SSL_get0_param(ssl);
        X509_VERIFY_PARAM_set1_host(param, cfg->upstream_host, 0);
    }

    if (SSL_connect(ssl) != 1) {
        /* ⚠ 只报"大类"，不把 OpenSSL 细节里的任何材料写进审计 */
        audit("mqtt-tls-handshake-failed", cfg->mode == HMI_MQTT_TLS_PSK ? "psk" : "ca");
        SSL_free(ssl);
        return -1;
    }

    *out_ssl = ssl;
    return 0;
}

/* ── 双向转发：明文侧(fd) ↔ TLS 侧(ssl)。
 *   用**两条线程**而不是 poll+SSL 状态机：后者要自己管 WANT_READ/WANT_WRITE 与重协商，
 *   一旦漏一条就变成"连上了却搬不动数据"。收尾靠 shutdown 把对面那条线程解开。 ── */
typedef struct {
    int   fd;           /* 明文侧（客户端） */
    int   upstream_fd;  /* TLS 侧的底层 socket */
    SSL*  ssl;
} relay_ctx_t;

static void* pump_plain_to_tls(void* arg)
{
    relay_ctx_t* c = (relay_ctx_t*)arg;
    unsigned char buf[2048];

    for (;;) {
        ssize_t n = read(c->fd, buf, sizeof(buf));
        if (n <= 0) break;
        int off = 0;
        while (off < (int)n) {
            int w = SSL_write(c->ssl, buf + off, (int)n - off);
            if (w <= 0) {
                audit("mqtt-tls-handshake-failed", "upstream-write");
                goto done;
            }
            off += w;
        }
    }
done:
    (void)shutdown(c->fd, SHUT_RD);
    /* 把上游也关掉，否则对面那条线程的 SSL_read 会一直等 */
    (void)shutdown(c->upstream_fd, SHUT_RDWR);
    return NULL;
}

static void* pump_tls_to_plain(void* arg)
{
    relay_ctx_t* c = (relay_ctx_t*)arg;
    unsigned char buf[2048];

    for (;;) {
        int r = SSL_read(c->ssl, buf, sizeof(buf));
        if (r <= 0) break;
        int off = 0;
        while (off < r) {
            ssize_t w = write(c->fd, buf + off, (size_t)(r - off));
            if (w <= 0) goto done;
            off += (int)w;
        }
    }
done:
    (void)shutdown(c->fd, SHUT_WR);
    return NULL;
}

static void relay(int fd, int upstream_fd, SSL* ssl)
{
    relay_ctx_t c;
    c.fd = fd;
    c.upstream_fd = upstream_fd;
    c.ssl = ssl;

    pthread_t up, down;
    if (pthread_create(&up, NULL, pump_plain_to_tls, &c) != 0) return;
    if (pthread_create(&down, NULL, pump_tls_to_plain, &c) != 0) {
        (void)shutdown(fd, SHUT_RDWR);
        (void)pthread_join(up, NULL);
        return;
    }
    (void)pthread_join(up, NULL);
    (void)pthread_join(down, NULL);
}

static int connect_upstream(const hmi_tls_proxy_config_t* cfg)
{
    int fd = socket(AF_INET, SOCK_STREAM, 0);
    if (fd < 0) return -1;

    struct sockaddr_in sa;
    memset(&sa, 0, sizeof(sa));
    sa.sin_family = AF_INET;
    sa.sin_port = htons((uint16_t)cfg->upstream_port);
    if (inet_pton(AF_INET, cfg->upstream_host, &sa.sin_addr) != 1) {
        (void)close(fd);
        return -1;
    }
    if (connect(fd, (struct sockaddr*)&sa, sizeof(sa)) != 0) {
        (void)close(fd);
        return -1;
    }
    return fd;
}

int main(int argc, char** argv)
{
    const char* cfgpath = getenv("HMI_TLS_PROXY_CONFIG");
    if (cfgpath == NULL || cfgpath[0] == '\0') cfgpath = HMI_TLS_PROXY_DEFAULT_CONFIG;
    int check_only = 0, once = 0;

    for (int i = 1; i < argc; i++) {
        if (strcmp(argv[i], "--check") == 0) check_only = 1;
        else if (strcmp(argv[i], "--once") == 0) once = 1;
        else if (strcmp(argv[i], "--config") == 0 && i + 1 < argc) cfgpath = argv[++i];
        else {
            fprintf(stderr, "用法: hmi-tls-proxy [--check] [--config PATH] [--once]\n");
            return HMI_TLS_PROXY_EXIT_BAD_CONFIG;
        }
    }

    hmi_tls_proxy_config_t cfg;
    int st = hmi_tls_proxy_config_load(cfgpath, &cfg);

    if (st == HMI_TLS_PROXY_EXIT_NOT_NEEDED) {
        /* off / 缺键 ⇒ **不需要代理**：正常跳过（不是错误，也不审计） */
        fprintf(stderr, "[tls-proxy] mqtt.tls=off ⇒ 不需要代理\n");
        return check_only ? st : 0;
    }
    if (st != HMI_TLS_PROXY_EXIT_OK) {
        fprintf(stderr, "[tls-proxy] 配置非法（%s）⇒ **不启动**（绝不降级为明文）\n", cfg.reason);
        audit("mqtt-tls-config-refused", cfg.reason);
        return st;
    }
    if (check_only) return HMI_TLS_PROXY_EXIT_OK; /* 校验不是事件：**不落审计** */

    /* 凭据文件必须真的在（配置层只判"非空"） */
    if (cfg.mode == HMI_MQTT_TLS_PSK && access(cfg.psk_file, R_OK) != 0) {
        fprintf(stderr, "[tls-proxy] PSK 密钥文件不可读：%s\n", cfg.psk_file);
        audit("mqtt-tls-psk-file-missing", "psk-file");
        return HMI_TLS_PROXY_EXIT_BAD_CONFIG;
    }
    if (cfg.mode == HMI_MQTT_TLS_CA && access(cfg.ca_path, R_OK) != 0) {
        fprintf(stderr, "[tls-proxy] CA 文件不可读：%s\n", cfg.ca_path);
        audit("mqtt-tls-ca-file-missing", "ca-path");
        return HMI_TLS_PROXY_EXIT_BAD_CONFIG;
    }

#if OPENSSL_VERSION_NUMBER < 0x10100000L
    SSL_library_init();
    SSL_load_error_strings();
    OpenSSL_add_all_algorithms();
#else
    OPENSSL_init_ssl(0, NULL);
#endif

    const SSL_METHOD* method = TLS_client_method();
    SSL_CTX* ctx = SSL_CTX_new(method);
    if (ctx == NULL) { audit("mqtt-tls-handshake-failed", "ctx-new"); return HMI_TLS_PROXY_EXIT_BAD_CONFIG; }

    if (cfg.mode == HMI_MQTT_TLS_PSK) {
        SSL_CTX_set_psk_client_callback(ctx, psk_client_cb);
        /* 目标是设备镜像里的 OpenSSL 1.1.0i（TLS1.2 的 PSK 套件）；显式收窄，别指望协商 */
        SSL_CTX_set_cipher_list(ctx, "PSK-AES128-GCM-SHA256:PSK-AES256-GCM-SHA384:PSK-AES128-CBC-SHA");
    }
    SSL_CTX_set_max_proto_version(ctx, TLS1_2_VERSION);

    /* ── 监听：地址**恒为回环**（契约里写死；这里也不接受覆盖） ── */
    int lfd = socket(AF_INET, SOCK_STREAM, 0);
    if (lfd < 0) { audit("mqtt-tls-proxy-listen-failed", "socket"); SSL_CTX_free(ctx); return 4; }

    int one = 1;
    (void)setsockopt(lfd, SOL_SOCKET, SO_REUSEADDR, &one, sizeof(one));

    struct sockaddr_in la;
    memset(&la, 0, sizeof(la));
    la.sin_family = AF_INET;
    la.sin_port = htons((uint16_t)cfg.listen_port);
    if (inet_pton(AF_INET, cfg.listen_host, &la.sin_addr) != 1 ||
        bind(lfd, (struct sockaddr*)&la, sizeof(la)) != 0 || listen(lfd, 4) != 0) {
        fprintf(stderr, "[tls-proxy] 监听 %s:%u 失败\n", cfg.listen_host, cfg.listen_port);
        audit("mqtt-tls-proxy-listen-failed", "bind");
        (void)close(lfd);
        SSL_CTX_free(ctx);
        return 4;
    }

    signal(SIGINT, on_signal);
    signal(SIGTERM, on_signal);
    signal(SIGHUP, on_reload);
    signal(SIGPIPE, SIG_IGN);

    audit("mqtt-tls-proxy-start", cfg.mode == HMI_MQTT_TLS_PSK ? "psk" : "ca");
    fprintf(stderr, "[tls-proxy] 监听 %s:%u → TLS(%s) → %s:%u\n", cfg.listen_host, cfg.listen_port,
            cfg.mode == HMI_MQTT_TLS_PSK ? "psk" : "ca", cfg.upstream_host, cfg.upstream_port);

    while (!g_stop) {
        /* ⚠ 用 poll(200ms) 再 accept，而不是直接阻塞在 accept：
         *   `signal()` 装的处理器默认带 SA_RESTART ⇒ accept 会被**自动重启** ⇒
         *   SIGTERM 收得到、进程却退不出来（procd 只能等超时杀；测试里表现为 hang）。 */
        struct pollfd lp;
        lp.fd = lfd;
        lp.events = POLLIN;
        lp.revents = 0;

        /* ── SIGHUP：热加载配置与凭据（阶段 322）──────────────────────────────
         * 掉监听吗？**不掉** —— 只换 cfg，后续连接与新握手用新的；
         * 失败呢？保留旧 cfg 并审计 mqtt-tls-reload-failed，**不降级、不退出**。
         * ⚠ 新配置若写着 mqtt.tls=off（"不需要代理"），**拒绝**这次重载并留痕 ——
         *   否则一次误编辑就把运行中的明文监听打开/把 TLS 静默关掉，违反"永不降级"。
         */
        if (g_reload) {
            g_reload = 0;
            hmi_tls_proxy_config_t ncfg;
            int rst = hmi_tls_proxy_config_load(cfgpath, &ncfg);
            const char* why = NULL;
            if (rst == HMI_TLS_PROXY_EXIT_NOT_NEEDED) {
                why = "tls-proxy-not-needed-refused";   /* 见上面那条 ⚠ */
            } else if (rst != HMI_TLS_PROXY_EXIT_OK) {
                why = ncfg.reason;
            } else if (ncfg.mode == HMI_MQTT_TLS_PSK && access(ncfg.psk_file, R_OK) != 0) {
                why = "psk-file";
            } else if (ncfg.mode == HMI_MQTT_TLS_CA && access(ncfg.ca_path, R_OK) != 0) {
                why = "ca-path";
            }
            if (why != NULL) {
                audit("mqtt-tls-reload-failed", why);
            } else {
                cfg = ncfg;   /* CA 路径等后续连接即生效；PSK 由 psk_client_cb 每次握手读文件 */
                audit("mqtt-tls-credentials-reloaded",
                      cfg.mode == HMI_MQTT_TLS_PSK ? "psk" : "ca");
            }
        }

        int pr = poll(&lp, 1, 200);
        if (pr < 0) {
            if (errno == EINTR) continue;
            break;
        }
        if (pr == 0) continue;

        int cfd = accept(lfd, NULL, NULL);
        if (cfd < 0) {
            if (errno == EINTR) continue;
            break;
        }

        int ufd = connect_upstream(&cfg);
        if (ufd < 0) {
            /* 上游连不上：**关掉客户端**（不许降级），下一轮重连 */
            audit("mqtt-tls-upstream-unreachable", "connect");
            (void)close(cfd);
            if (once) break;
            continue;
        }

        SSL* ssl = NULL;
        if (tls_upstream(ctx, &ssl, &cfg, ufd) == 0) {
            relay(cfd, ufd, ssl);
            SSL_shutdown(ssl);
            SSL_free(ssl);
        }
        /* 握手失败时 tls_upstream 已审计 ⇒ 这里只关连接（**绝不**改成明文转发） */

        (void)close(ufd);
        (void)close(cfd);
        if (once) break;
    }

    audit("mqtt-tls-proxy-stop", "");
    (void)close(lfd);
    SSL_CTX_free(ctx);
    return 0;
}
