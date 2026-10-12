/*
 * mqtt_fake_broker_test.c — **假 broker** 主机判据（阶段 290，`§2-#12` M2 的判据面）
 *
 * 为什么需要它：远程访问方案 §5 给 M2 定的判据面原文是
 *   「主机测试用**假 broker**（本地 socket 监听）断言报文序列」
 * —— 前面所有判据都停在纯函数/单报文层，没有一条证明"设备真的把 CONNECT/SUBSCRIBE/PUBLISH
 * 按契约发出去、并且真的收得到下行"。
 *
 * ⚠ 本文件**驱动的是产品代码本身**（不是照抄一遍）：
 *   · `#include "variable_table.c"` ⇒ 订阅注册、LWT、上线报状态走的都是 `var_groups_rebuild`
 *     与 `on_mqtt_connected`；
 *   · 下行报文经 `on_mqtt_message` → `mqtt_cmd_wiring` → `hmi_cmd_handle` 真的写变量缓存。
 *   `hmi_actions.c` 依赖 lv_tick_get 等宿主符号、不参与本步链接 ⇒ 这里给
 *   `hmi_audit` / `hmi_current_role` / `hmi_has_permission` 三个假实现（与 hmi_comm_test 同款）。
 *
 * 断言：
 *   ① CONNECT：遗嘱三件事（will flag / retain / QoS=0）＋ 段顺序 ClientId → Will Topic → Will Message；
 *   ② SUBSCRIBE：**逐个**发订阅，其中必须出现 `<pre>/cmd/<tag>`（阶段 289 的接线在真实链路上有效）；
 *   ③ 上行：上线后自动发 `<pre>/status` 的 `on=1;…;tb=boot`，**retain=1**；
 *   ④ 下行（成功）：合法信封 ⇒ 变量缓存真的被写 ＋ 审计一条 `remote-write`；
 *   ⑤ 下行（拒绝）：topic/payload 的 target 不一致 ⇒ 不写；过期信封 ⇒ 不写。
 *
 * 时序：单进程、单线程，交替驱动 `mqtt_poll()` 与非阻塞 `recv()`。
 * ⚠ 接收侧用**跨调用持久**的缓冲：一个 recv 里可能同时到达多个报文，
 *   第一版每次调用各自 `have=0` ⇒ 第二个报文被静默丢掉（本文件已修）。
 * 所有等待都有毫秒上限 ⇒ 失败是"红"，不是"挂"。
 */
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <stdint.h>
#include <errno.h>
#include <unistd.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <netinet/tcp.h>
#include <sys/select.h>
#include <sys/time.h>

#include "mqtt_client.h"
#include "mqtt_contract.h"
#include "mqtt_cmd_contract.h"   /* hmi_mqtt_format_cmd_topic（阶段 289 的 cmd 主题形状） */
#include "mqtt_alarm_publish.h"  /* 报警上行发布器（阶段 291） */
#include "runtime_config.h"      /* HMI_MQTT_TOPIC_PRE */
#include "variable_table.h"      /* hmi_var_init / var_raw_read / hmi_tag_meta_at */

/* 变量表实现（HMI_UNIT_TEST 下暴露内部符号；与 hmi_comm_test 同一套做法） */
#include "variable_table.c"
#include "../generated/tags.c"

/* HMI_UNIT_TEST 下 mqtt_client.c 暴露的构建器：用它拼"broker → 设备"的 PUBLISH 报文 */
extern int mqtt_test_build_publish(const char* topic, const char* payload, int len,
                                   int retain, uint8_t* out, size_t cap);

static int g_pass = 0, g_fail = 0;
#define CHECK(cond, msg) do { \
    if (cond) { printf("  PASS  %s\n", msg); g_pass++; } \
    else      { printf("  FAIL  %s (line %d)\n", msg, __LINE__); g_fail++; } \
} while (0)

/* ── 假宿主注入（hmi_actions.c 不参与本步链接） ──
 * ⚠ 假 `hmi_has_permission` 不复刻真实现的两条语义（未登录只含"查看画面" / ENFORCE=0 恒真）
 *   —— 那两条由 hmi_actions_permission_test 覆盖；接线只负责**问**它。这里放行全部位。 */
static char g_audit_last[192];
static int  g_audit_count = 0;

int hmi_has_permission(unsigned int bits) { (void)bits; return 1; }
const char* hmi_current_role(void) { return "testrole"; }
void hmi_audit(const char* event, const char* user, const char* role, const char* detail)
{
    (void)user; (void)role;
    g_audit_count++;
    snprintf(g_audit_last, sizeof(g_audit_last), "%s|%s",
             event != NULL ? event : "", detail != NULL ? detail : "");
}

/* ── 时间 ── */
static int64_t now_ms(void)
{
    struct timeval tv;
    gettimeofday(&tv, NULL);
    return (int64_t)tv.tv_sec * 1000 + tv.tv_usec / 1000;
}

static int varint_decode(const uint8_t* b, size_t n, size_t* used, uint32_t* val)
{
    uint32_t v = 0; int mult = 1;
    for (size_t i = 0; i < n && i < 4; i++) {
        uint8_t d = b[i];
        v += (uint32_t)(d & 0x7F) * (uint32_t)mult;
        if (!(d & 0x80)) { *used = i + 1; *val = v; return 0; }
        mult *= 128;
    }
    return -1;
}

/* ── 接收侧：跨调用持久的字节缓冲 ── */
typedef struct { uint8_t buf[4096]; size_t len; } rxbuf_t;

/* 取出一个完整报文（返回总长；缓冲区里不够一个报文则返回 0；出错返回 -1） */
static int rx_take_packet(rxbuf_t* rx, uint8_t* out, size_t cap)
{
    if (rx->len < 2) return 0;
    size_t used = 0; uint32_t rl = 0;
    if (varint_decode(rx->buf + 1, rx->len - 1, &used, &rl) != 0) { rx->len = 0; return -1; }
    size_t total = 1 + used + rl;
    if (rx->len < total) return 0;
    if (total > cap) { rx->len = 0; return -1; }
    memcpy(out, rx->buf, total);
    memmove(rx->buf, rx->buf + total, rx->len - total);
    rx->len -= total;
    return (int)total;
}

/* 一边驱动客户端状态机、一边收；拿到一个完整报文就返回它的总长，超时返回 -1 */
static int pump_recv_packet(int fd, rxbuf_t* rx, uint8_t* out, size_t cap, int deadline_ms)
{
    int64_t end = now_ms() + deadline_ms;
    while (now_ms() < end) {
        mqtt_poll();
        int got = rx_take_packet(rx, out, cap);
        if (got != 0) return got;
        uint8_t tmp[512];
        ssize_t n = recv(fd, tmp, sizeof(tmp), MSG_DONTWAIT);
        if (n > 0) {
            if (rx->len + (size_t)n <= sizeof(rx->buf)) {
                memcpy(rx->buf + rx->len, tmp, (size_t)n);
                rx->len += (size_t)n;
            } else {
                rx->len = 0;                 /* 溢出：丢弃（本测试报文都很小） */
            }
            continue;                        /* 立刻再检查完整报文 */
        }
        if (n < 0 && errno != EAGAIN && errno != EWOULDBLOCK) return -1;
        usleep(1000);
    }
    return -1;
}

static int accept_with_pump(int lfd, int deadline_ms)
{
    int64_t end = now_ms() + deadline_ms;
    while (now_ms() < end) {
        mqtt_poll();
        fd_set rf; FD_ZERO(&rf); FD_SET(lfd, &rf);
        struct timeval tv = { 0, 1000 };
        if (select(lfd + 1, &rf, NULL, NULL, &tv) > 0) {
            int c = accept(lfd, NULL, NULL);
            if (c >= 0) return c;
        }
        usleep(1000);
    }
    return -1;
}

/* ── 报文解析小工具 ── */
static const uint8_t* rd_str(const uint8_t* p, const uint8_t* end, const char** s, size_t* len)
{
    if (p + 2 > end) return NULL;
    size_t n = ((size_t)p[0] << 8) | p[1];
    p += 2;
    if (p + n > end) return NULL;
    *s = (const char*)p;
    *len = n;
    return p + n;
}

static const uint8_t* body_of(const uint8_t* pkt, int total)
{
    size_t used = 0; uint32_t rl = 0;
    if (total < 2 || varint_decode(pkt + 1, (size_t)total - 1, &used, &rl) != 0) return NULL;
    return pkt + 1 + used;
}

/* ── 谓词 ── */
typedef int (*pkt_pred_t)(const uint8_t* pkt, int n, void* ctx);

typedef struct { int fd; const char* want; } sub_ctx_t;

/* 是不是"我们要找的那条" SUBSCRIBE；**其他**订阅也回 SUBACK（否则客户端会重发），但不算命中 */
static int pred_subscribe(const uint8_t* pkt, int n, void* ctx)
{
    if ((pkt[0] >> 4) != 8) return 0;
    sub_ctx_t* sc = (sub_ctx_t*)ctx;
    const uint8_t* p = body_of(pkt, n);
    if (p == NULL || p + 2 > pkt + n) return 0;
    uint8_t pid_hi = p[0], pid_lo = p[1];
    p += 2;
    const char* s; size_t sl;
    const uint8_t* q = rd_str(p, pkt + n, &s, &sl);
    if (q == NULL) return 0;
    uint8_t suback[5] = { 0x90, 0x03, pid_hi, pid_lo, 0x00 };
    (void)send(sc->fd, suback, sizeof(suback), 0);
    return sl == strlen(sc->want) && memcmp(s, sc->want, sl) == 0;
}

typedef struct {
    const char* want;            /* NULL = 任意主题都收；否则只收该主题 */
    char topic[192];
    char payload[256];
    int  retain;
} pub_t;

static int pred_publish(const uint8_t* pkt, int n, void* ctx)
{
    if ((pkt[0] >> 4) != 3) return 0;
    pub_t* o = (pub_t*)ctx;
    char topic[192], payload[256];
    int retain = (pkt[0] & 0x01) ? 1 : 0;
    const uint8_t* p = body_of(pkt, n);
    const char* s; size_t sl;
    p = p ? rd_str(p, pkt + n, &s, &sl) : NULL;
    if (p == NULL) return 0;
    size_t tl = sl < sizeof(topic) - 1 ? sl : sizeof(topic) - 1;
    memcpy(topic, s, tl); topic[tl] = '\0';
    size_t pl = (size_t)(pkt + n - p);
    if (pl > sizeof(payload) - 1) pl = sizeof(payload) - 1;
    memcpy(payload, p, pl); payload[pl] = '\0';
    if (o->want != NULL && strcmp(topic, o->want) != 0) return 0;
    snprintf(o->topic, sizeof(o->topic), "%s", topic);
    snprintf(o->payload, sizeof(o->payload), "%s", payload);
    o->retain = retain;
    return 1;
}

/* 反复收，直到收到一条 **该主题** 的 PUBLISH（期间持续驱动客户端） */
static int wait_publish(int fd, rxbuf_t* rx, uint8_t* pkt, size_t cap, int deadline_ms, pub_t* o)
{
    int64_t end = now_ms() + deadline_ms;
    while (now_ms() < end) {
        int n = pump_recv_packet(fd, rx, pkt, cap, 500);
        if (n <= 0) continue;
        if (pred_publish(pkt, n, o)) return 1;
    }
    return 0;
}

/* ── 报警快照的假 fetch（设备侧真实现经 hmi_ui_api 的 alarm_snapshot，见 hmi_alarm_source.c） ── */
static hmi_alarm_snapshot_t g_snap[4];
static int g_snap_n = 0;

static int fake_alarm_fetch(hmi_alarm_snapshot_t* out, int cap)
{
    int n = g_snap_n < cap ? g_snap_n : cap;
    for (int i = 0; i < n; i++) out[i] = g_snap[i];
    return n;
}

int main(void)
{
    printf("=== 假 broker 判据：设备侧 MQTT 上线/订阅/上报/下行（阶段 290）===\n\n");

    /* 监听 127.0.0.1:0（ephemeral） */
    int lfd = socket(AF_INET, SOCK_STREAM, 0);
    if (lfd < 0) { printf("  FAIL  socket()\n"); return 1; }
    int one = 1;
    setsockopt(lfd, SOL_SOCKET, SO_REUSEADDR, &one, sizeof(one));
    struct sockaddr_in sa;
    memset(&sa, 0, sizeof(sa));
    sa.sin_family = AF_INET;
    sa.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    sa.sin_port = 0;
    if (bind(lfd, (struct sockaddr*)&sa, sizeof(sa)) != 0 || listen(lfd, 1) != 0) {
        printf("  FAIL  bind/listen\n"); close(lfd); return 1;
    }
    socklen_t slen = sizeof(sa);
    getsockname(lfd, (struct sockaddr*)&sa, &slen);
    int port = ntohs(sa.sin_port);
    printf("[假 broker] 监听 127.0.0.1:%d\n", port);

    /* 真实启动顺序：先 hmi_var_init()（采纳出厂标签表 ⇒ 注册 /set/ 与 /cmd/ 主题、
     * 设 LWT、挂上线回调），再 mqtt_start()。阶段 290 修的就是"start 不许清空订阅表"。 */
    hmi_var_init();
    mqtt_start("127.0.0.1", port, "hmi-fake", NULL, NULL, 30);

    char cmd_topic[128], status_topic[128];
    hmi_mqtt_format_cmd_topic(cmd_topic, sizeof(cmd_topic), HMI_MQTT_TOPIC_PRE, "SV");
    hmi_mqtt_format_status_topic(status_topic, sizeof(status_topic), HMI_MQTT_TOPIC_PRE);

    /* ① 握手：accept + CONNECT */
    int cfd = accept_with_pump(lfd, 3000);
    CHECK(cfd >= 0, "客户端连上假 broker（TCP 层）");

    rxbuf_t rx; memset(&rx, 0, sizeof(rx));
    uint8_t pkt[1024];
    int n = pump_recv_packet(cfd, &rx, pkt, sizeof(pkt), 3000);
    CHECK(n > 0, "收到 CONNECT 报文");
    CHECK(n > 0 && pkt[0] == 0x10, "CONNECT：固定头 0x10");
    const uint8_t* body = (n > 0) ? body_of(pkt, n) : NULL;
    const uint8_t* end = pkt + (n > 0 ? n : 0);
    CHECK(body != NULL, "CONNECT：剩余长度可解");
    if (body != NULL) {
        const char* s; size_t sl;
        const uint8_t* p = rd_str(body, end, &s, &sl);
        CHECK(p != NULL && sl == 4 && memcmp(s, "MQTT", 4) == 0, "CONNECT：协议名 MQTT");
        CHECK(p != NULL && p + 4 <= end && p[0] == 4, "CONNECT：协议级别 3.1.1");
        uint8_t flags = (p != NULL && p + 4 <= end) ? p[1] : 0;
        CHECK((flags & 0x04) != 0, "CONNECT：will flag = 1（设备接线设上了 LWT）");
        CHECK((flags & 0x20) != 0, "CONNECT：will retain = 1");
        CHECK((flags & 0x18) == 0, "CONNECT：will QoS = 0");
        p = p ? p + 4 : NULL;                                   /* 级别+flags+keepalive */
        p = p ? rd_str(p, end, &s, &sl) : NULL;
        CHECK(p != NULL && sl == 8 && memcmp(s, "hmi-fake", 8) == 0, "CONNECT：ClientId");
        p = p ? rd_str(p, end, &s, &sl) : NULL;
        CHECK(p != NULL && sl == strlen(status_topic) && memcmp(s, status_topic, sl) == 0,
              "CONNECT：LWT 主题 = <pre>/status");
        p = p ? rd_str(p, end, &s, &sl) : NULL;
        CHECK(p != NULL && sl == strlen("on=0") && memcmp(s, "on=0", sl) == 0,
              "CONNECT：LWT 载荷 = on=0");
    }

    /* ② CONNACK ⇒ 客户端进入 CONNECTED，随后自动上报状态并逐个发 SUBSCRIBE */
    uint8_t connack[4] = { 0x20, 0x02, 0x00, 0x00 };
    CHECK(send(cfd, connack, sizeof(connack), 0) == (ssize_t)sizeof(connack), "发出 CONNACK");

    /* ⚠ 状态 PUBLISH 可能**先于** SUBSCRIBE 到达（上线回调在 CONNACK 处理里就 publish 了，
     *   而 SUBSCRIBE 要等下一次 poll）⇒ 必须在一个循环里同时收两类，不能先等 SUBSCRIBE
     *   —— 第一版就是"先等订阅"把先到的状态报文丢掉了。 */
    sub_ctx_t sc = { cfd, cmd_topic };
    pub_t status; memset(&status, 0, sizeof(status));
    status.want = status_topic;
    int got_sub = 0, got_status = 0;
    {
        int64_t end = now_ms() + 5000;
        while ((!got_sub || !got_status) && now_ms() < end) {
            int t = pump_recv_packet(cfd, &rx, pkt, sizeof(pkt), 500);
            if (t <= 0) continue;
            if ((pkt[0] >> 4) == 8) {
                if (pred_subscribe(pkt, t, &sc)) got_sub = 1;      /* 谓词内部回 SUBACK */
            } else if ((pkt[0] >> 4) == 3) {
                if (pred_publish(pkt, t, &status)) got_status = 1;
            }
        }
    }
    CHECK(got_sub == 1, "SUBSCRIBE 里出现 <pre>/cmd/SV（阶段 289 的接线在真实链路上有效）");
    CHECK(got_status == 1, "上线后自动发 <pre>/status（真实 on_mqtt_connected）");
    CHECK(strcmp(status.topic, status_topic) == 0, "状态主题 = <pre>/status");
    CHECK(status.retain == 1, "状态报文 retain=1（晚加入的远程端也能立刻看到）");
    CHECK(strncmp(status.payload, "on=1;", 5) == 0, "状态载荷 on=1");
    CHECK(strstr(status.payload, ";tb=boot") != NULL, "状态载荷显式声明时间基 tb=boot");

    /* ③ 下行（成功）：合法信封 ⇒ 变量缓存真的被写 ＋ 审计一条 remote-write */
    {
        var_raw_write(TAG_SV, 3.0f);
        g_audit_count = 0;
        char payload[192];
        snprintf(payload, sizeof(payload), "id=fb1;ts=%lld;ttl=60000;by=broker;target=SV;value=7",
                 (long long)now_ms());
        uint8_t out[256];
        int m = mqtt_test_build_publish(cmd_topic, payload, (int)strlen(payload), 0, out, sizeof(out));
        CHECK(m > 0 && send(cfd, out, (size_t)m, 0) == (ssize_t)m, "发出下行 PUBLISH（合法）");
        int64_t end2 = now_ms() + 3000;
        while (var_raw_read(TAG_SV) != 7.0f && now_ms() < end2) { mqtt_poll(); usleep(1000); }
        CHECK(var_raw_read(TAG_SV) == 7.0f, "变量缓存被写成 7（/cmd/ 端到端真的执行）");
        CHECK(g_audit_count == 1 && strncmp(g_audit_last, "remote-write|", 13) == 0,
              "审计一条 remote-write");
    }

    /* ④ 下行（拒绝）：主题 target ≠ 载荷 target ⇒ 不写、留痕 */
    {
        var_raw_write(TAG_SV, 3.0f);
        g_audit_count = 0;
        char payload[192];
        snprintf(payload, sizeof(payload), "id=fb2;ts=%lld;ttl=60000;by=broker;target=PV;value=9",
                 (long long)now_ms());
        uint8_t out[256];
        int m = mqtt_test_build_publish(cmd_topic, payload, (int)strlen(payload), 0, out, sizeof(out));
        (void)send(cfd, out, (size_t)m, 0);
        int64_t end3 = now_ms() + 1500;
        while (now_ms() < end3) { mqtt_poll(); usleep(1000); }
        CHECK(var_raw_read(TAG_SV) == 3.0f, "target 不一致 ⇒ 不写变量（主题 SV / 载荷 PV）");
        CHECK(g_audit_count >= 1 && strstr(g_audit_last, "target-mismatch") != NULL,
              "target 不一致 ⇒ 审计留痕 target-mismatch");
    }

    /* ⑤ 下行（拒绝）：过期信封 ⇒ 不写、留痕 expired（主题仍是已订阅的 /cmd/SV，场景真实） */
    {
        var_raw_write(TAG_SV, 3.0f);
        g_audit_count = 0;
        char payload[192];
        snprintf(payload, sizeof(payload), "id=fb3;ts=%lld;ttl=60000;by=broker;target=SV;value=11",
                 (long long)now_ms() - 120000);
        uint8_t out[256];
        int m = mqtt_test_build_publish(cmd_topic, payload, (int)strlen(payload), 0, out, sizeof(out));
        (void)send(cfd, out, (size_t)m, 0);
        int64_t end4 = now_ms() + 1500;
        while (now_ms() < end4) { mqtt_poll(); usleep(1000); }
        CHECK(var_raw_read(TAG_SV) == 3.0f, "过期命令 ⇒ 不写变量");
        CHECK(g_audit_count >= 1 && strstr(g_audit_last, "expired") != NULL,
              "过期 ⇒ 审计留痕 expired");
    }

    /* ⑥ 报警上行（M2 报警那一半）：<pre>/alarm/<id> 的 st=/sev=，且**边沿触发** */
    {
        hmi_alarm_publish_reset();
        memset(g_snap, 0, sizeof(g_snap));
        g_snap_n = 2;
        snprintf(g_snap[0].id, sizeof(g_snap[0].id), "al1");
        g_snap[0].config_id = 0; g_snap[0].state = 1; g_snap[0].severity = 2;   /* Active / Critical */
        snprintf(g_snap[1].id, sizeof(g_snap[1].id), "al2");
        g_snap[1].config_id = 1; g_snap[1].state = 0; g_snap[1].severity = 0;   /* Inactive ⇒ 不上行 */

        char alarm_topic[128];
        hmi_mqtt_format_alarm_topic(alarm_topic, sizeof(alarm_topic), HMI_MQTT_TOPIC_PRE, "al1");

        pub_t alarm; memset(&alarm, 0, sizeof(alarm));
        alarm.want = alarm_topic;

        CHECK(hmi_alarm_publish_scan(fake_alarm_fetch, 1000ULL, 0) == 1,
              "报警上行：只发 1 条（Inactive 的那条跳过）");
        CHECK(wait_publish(cfd, &rx, pkt, sizeof(pkt), 2000, &alarm) == 1,
              "broker 收到 <pre>/alarm/al1");
        CHECK(strcmp(alarm.payload, "st=1;sev=2;t=1000;tb=boot") == 0,
              "报警载荷 st=1;sev=2;t=1000;tb=boot");

        CHECK(hmi_alarm_publish_scan(fake_alarm_fetch, 2000ULL, 0) == 0,
              "状态未变 ⇒ 一条都不重复发（边沿触发）");

        g_snap[0].state = 2;   /* Acknowledged */
        CHECK(hmi_alarm_publish_scan(fake_alarm_fetch, 3000ULL, 0) == 1,
              "状态变化 ⇒ 再发一条");
        memset(&alarm, 0, sizeof(alarm));
        alarm.want = alarm_topic;
        CHECK(wait_publish(cfd, &rx, pkt, sizeof(pkt), 2000, &alarm) == 1,
              "broker 收到状态变化那条");
        CHECK(strcmp(alarm.payload, "st=2;sev=2;t=3000;tb=boot") == 0,
              "变化后的载荷 st=2");

        /* 越界状态（0 / 5）不许上线：0 被发布器跳过，5 被契约格式化函数拒 */
        g_snap[0].state = 0;
        g_snap[1].state = 5;
        CHECK(hmi_alarm_publish_scan(fake_alarm_fetch, 4000ULL, 0) == 0,
              "state=0 跳过、state=5 被契约拒 ⇒ 一条都不发");
    }

    mqtt_stop();
    CHECK(mqtt_is_connected() == 0, "mqtt_stop 后不再是已连接状态");

    close(cfd);
    close(lfd);

    printf("\n=== 结果: %d 通过, %d 失败 ===\n", g_pass, g_fail);
    return g_fail == 0 ? 0 : 1;
}
