/*
 * mqtt_client.c — 最小 MQTT 3.1.1 客户端实现（QoS0）
 */
#include "mqtt_client.h"
#include <pthread.h>
#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <errno.h>
#include <fcntl.h>
#include <sys/time.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <sys/select.h>
#include <netinet/in.h>
#include <netinet/tcp.h>
#include <arpa/inet.h>
#include <netdb.h>

#define MQTT_MAX_TOPICS     32
#define MQTT_MAX_TOPIC_LEN  128
#define MQTT_RX_BUF         512
#define MQTT_RECONNECT_MS   5000
#define MQTT_PACKET_ID_MAX  0xFFFF

typedef enum {
    MQTT_DISCONNECTED,
    MQTT_CONNECT_INPROGRESS,
    MQTT_WAIT_CONNACK,
    MQTT_CONNECTED,
} mqtt_state_t;

typedef struct {
    char topic[MQTT_MAX_TOPIC_LEN];
    void* user;
    uint16_t pending_pid;    /* 0 = 已订阅 */
    int64_t  pending_at;
} mqtt_sub_t;

typedef struct {
    char host[64];
    int  port;
    char client_id[64];
    char user[64];
    char pass[64];
    int  keepalive;
} mqtt_cfg_t;

static pthread_mutex_t s_lock = PTHREAD_MUTEX_INITIALIZER;
static mqtt_cfg_t s_cfg;
static mqtt_msg_cb_t s_cb = NULL;
static mqtt_connect_cb_t s_connect_cb = NULL;

/* 遗嘱（LWT）—— 每次 CONNECT 都带上（见 mqtt_client.h 的 mqtt_set_will）。
 * retain 走 flags 的 bit5：broker 保留它 ⇒ 晚加入的远程端立刻看到"离线"。 */
static char s_will_topic[MQTT_MAX_TOPIC_LEN];
static char s_will_payload[64];
static int  s_will_retain = 0;
static int  s_will_set = 0;
static mqtt_sub_t s_subs[MQTT_MAX_TOPICS];
static int s_sub_count = 0;

static int s_sock = -1;
static mqtt_state_t s_state = MQTT_DISCONNECTED;
static int64_t s_last_attempt = 0;
static int64_t s_last_sent = 0;
static int64_t s_last_ping_resp = 0;
static int64_t s_connect_started = 0;
static uint16_t s_packet_id = 0;

static uint8_t s_rx[MQTT_RX_BUF];
static size_t  s_rx_len = 0;

static int64_t now_ms(void)
{
    struct timeval tv;
    gettimeofday(&tv, NULL);
    return (int64_t)tv.tv_sec * 1000 + tv.tv_usec / 1000;
}

/* ── 剩余长度编码/解码 ── */
static size_t varint_encode(uint32_t v, uint8_t* out)
{
    size_t n = 0;
    do {
        uint8_t b = (uint8_t)(v % 128);
        v /= 128;
        if (v) b |= 0x80;
        out[n++] = b;
    } while (v);
    return n;
}

static int varint_decode(const uint8_t* b, size_t n, size_t* used, uint32_t* val)
{
    uint32_t v = 0;
    int mult = 1;
    size_t i;
    for (i = 0; i < n && i < 4; i++) {
        uint8_t d = b[i];
        v += (uint32_t)(d & 0x7F) * (uint32_t)mult;
        if (!(d & 0x80)) { *used = i + 1; *val = v; return 0; }
        mult *= 128;
    }
    return -1;
}

/* ── UTF-8 字符串写入 ── */
/*
 * ⚠ 这里刻意**没有**「无容量的 put_str」这种辅助函数了。
 * 旧版 `static size_t put_str(uint8_t* out, const char* s)` 把长度前缀与内容写进调用者
 * 给的缓冲区，却不看容量 —— `build_subscribe` 用 `uint8_t vh[8]` 装上限 128 字节的主题，
 * 每次订阅（默认主题 "hmi/t113/set/<tag>"）越界 ≥9 字节，写坏通信线程的栈。
 * 三个构建器现在一律**直接写进 `out`**，只受 `cap` 约束；`put_str_literal` 只用于
 * 编译期已知长度的字面量，长度算得出来就不会越界。
 */
static int put_str_literal(uint8_t* out, size_t cap, size_t* pos, const char* s, size_t len)
{
    if (*pos + 2 + len > cap) return -1;
    out[*pos]     = (uint8_t)(len >> 8);
    out[*pos + 1] = (uint8_t)len;
    memcpy(&out[*pos + 2], s, len);
    *pos += 2 + len;
    return 0;
}

/* ── 套接字 ── */
static void sock_set_nonblock(int fd, int nb)
{
    int fl = fcntl(fd, F_GETFL, 0);
    if (nb) fl |= O_NONBLOCK; else fl &= ~O_NONBLOCK;
    fcntl(fd, F_SETFL, fl);
}

static int sock_send_all(int fd, const uint8_t* buf, size_t len)
{
    size_t sent = 0;
    while (sent < len) {
        ssize_t n = send(fd, &buf[sent], len - sent, MSG_NOSIGNAL);
        if (n <= 0) return -1;
        sent += (size_t)n;
    }
    return 0;
}

/* ── 报文构建 ── */

static int build_connect(uint8_t* out, size_t cap)
{
    size_t clen = strlen(s_cfg.client_id);
    size_t ulen = s_cfg.user[0] ? strlen(s_cfg.user) : 0;
    size_t plen = s_cfg.pass[0] ? strlen(s_cfg.pass) : 0;
    size_t wlen = s_will_set ? strlen(s_will_topic) : 0;
    size_t mlen = s_will_set ? strlen(s_will_payload) : 0;
    if (clen > 0xFFFF || ulen > 0xFFFF || plen > 0xFFFF) return -1;

    /* 可变头 10 字节：协议名(2+4) + 级别(1) + 标志(1) + keepalive(2) */
    size_t rl = 10 + (2 + clen)
              + (s_will_set ? (2 + wlen) + (2 + mlen) : 0)
              + (ulen ? 2 + ulen : 0) + (plen ? 2 + plen : 0);
    uint8_t flags = 0x02;                          /* clean session */
    /* 遗嘱三件事（MQTT 3.1.1 §3.1.2.5）：flag(bit2) ＋ QoS(bits3-4=0) ＋ retain(bit5)。
     * 载荷顺序也是契约：ClientId → Will Topic → Will Message → User → Password。 */
    if (s_will_set) {
        flags |= 0x04;
        if (s_will_retain) flags |= 0x20;
    }
    if (ulen) flags |= 0x80;
    if (plen) flags |= 0x40;

    uint8_t rlb[4];
    size_t rln = varint_encode((uint32_t)rl, rlb);
    if (1 + rln + rl > cap) return -1;

    size_t p = 0;
    out[p++] = 0x10;                               /* CONNECT */
    memcpy(&out[p], rlb, rln); p += rln;
    if (put_str_literal(out, cap, &p, "MQTT", 4) != 0) return -1;
    out[p++] = 4;                                  /* 协议级别 3.1.1 */
    out[p++] = flags;
    out[p++] = (uint8_t)(s_cfg.keepalive >> 8);
    out[p++] = (uint8_t)s_cfg.keepalive;
    out[p++] = (uint8_t)(clen >> 8);
    out[p++] = (uint8_t)clen;
    memcpy(&out[p], s_cfg.client_id, clen); p += clen;
    if (s_will_set) {
        /* 两段都是 2 字节长度前缀 ＋ 内容：Will Topic 是 UTF-8 串，Will Message 是二进制 */
        if (put_str_literal(out, cap, &p, s_will_topic, wlen) != 0) return -1;
        if (put_str_literal(out, cap, &p, s_will_payload, mlen) != 0) return -1;
    }
    if (ulen) {
        out[p++] = (uint8_t)(ulen >> 8);
        out[p++] = (uint8_t)ulen;
        memcpy(&out[p], s_cfg.user, ulen); p += ulen;
    }
    if (plen) {
        out[p++] = (uint8_t)(plen >> 8);
        out[p++] = (uint8_t)plen;
        memcpy(&out[p], s_cfg.pass, plen); p += plen;
    }
    return (int)p;
}

static int build_subscribe(uint16_t pid, const char* topic, uint8_t* out, size_t cap)
{
    size_t tlen = strlen(topic);
    if (tlen > 0xFFFF) return -1;

    /* 可变头：pid(2) + UTF-8 字符串(2+tlen) + QoS(1) */
    size_t vh = 2 + 2 + tlen + 1;
    uint8_t rlb[4];
    size_t rln = varint_encode((uint32_t)vh, rlb);
    if (1 + rln + vh > cap) return -1;

    size_t p = 0;
    out[p++] = 0x82;                               /* SUBSCRIBE */
    memcpy(&out[p], rlb, rln); p += rln;
    out[p++] = (uint8_t)(pid >> 8);
    out[p++] = (uint8_t)pid;
    out[p++] = (uint8_t)(tlen >> 8);
    out[p++] = (uint8_t)tlen;
    memcpy(&out[p], topic, tlen); p += tlen;
    out[p++] = 0x00;                               /* QoS0 */
    return (int)p;
}

static int build_publish(const char* topic, const char* payload, int len,
                         int retain, uint8_t* out, size_t cap)
{
    size_t tlen = strlen(topic);
    if (tlen > 0xFFFF) return -1;
    if (len < 0) len = 0;

    size_t rl = 2 + tlen + (size_t)len;
    uint8_t rlb[4];
    size_t rln = varint_encode((uint32_t)rl, rlb);
    if (1 + rln + rl > cap) return -1;

    size_t p = 0;
    out[p++] = (uint8_t)(retain ? 0x31 : 0x30);    /* PUBLISH QoS0 */
    memcpy(&out[p], rlb, rln); p += rln;
    out[p++] = (uint8_t)(tlen >> 8);
    out[p++] = (uint8_t)tlen;
    memcpy(&out[p], topic, tlen); p += tlen;
    if (len > 0 && payload) { memcpy(&out[p], payload, (size_t)len); p += (size_t)len; }
    return (int)p;
}

/* ── 连接 ── */
static void close_sock(void)
{
    /* 阶段 121：**所有** socket 访问都在 s_lock 内（publish 的 send、poll 的 select/recv/ping、
       finish_connect 的 getsockopt/setsockopt/send、start_connect 的赋值，以及这里的 close），
       并且每个 syscall 都在锁内**重新读** s_sock。
       于是不再有"读到 fd → 被别的线程 close 掉 → 继续用旧 fd"这个窗口
       （fd 号可能被同进程的其它 socket 复用 ⇒ 把 MQTT 字节写进无关连接）。
       ⚠ 锁只在**少数几处**取，且每次只包住一次（非阻塞或已设 SO_SNDTIMEO 的）syscall；
       `process_rx()`（可能回调用户）与 `do_disconnect()`（内部要再取锁）**一律在锁外**调用。 */
    pthread_mutex_lock(&s_lock);
    if (s_sock >= 0) { close(s_sock); s_sock = -1; }
    pthread_mutex_unlock(&s_lock);
}

static void do_disconnect(void)
{
    close_sock();
    s_state = MQTT_DISCONNECTED;
    s_last_attempt = now_ms();
}

static void start_connect(void)
{
    struct addrinfo hints, *res = NULL;
    char portstr[8];
    snprintf(portstr, sizeof(portstr), "%d", s_cfg.port);
    memset(&hints, 0, sizeof(hints));
    hints.ai_family = AF_INET;
    hints.ai_socktype = SOCK_STREAM;
    if (getaddrinfo(s_cfg.host, portstr, &hints, &res) != 0) {
        s_last_attempt = now_ms();
        return;
    }
    int fd = -1;
    int chosen = -1;
    for (struct addrinfo* ai = res; ai; ai = ai->ai_next) {
        fd = socket(ai->ai_family, ai->ai_socktype, ai->ai_protocol);
        if (fd < 0) continue;
        sock_set_nonblock(fd, 1);
        if (connect(fd, ai->ai_addr, ai->ai_addrlen) == 0) { chosen = fd; break; }
        if (errno == EINPROGRESS) { chosen = fd; break; }
        close(fd);
        fd = -1;
    }
    freeaddrinfo(res);
    if (chosen >= 0) {
        /* ⚠ 赋值必须在锁内（阶段 121）：`mqtt_poll` 之后会在锁内读 s_sock，
           而 close_sock 也在锁内写它 —— 三边共用同一把锁，才没有"读到半改"的窗口。 */
        pthread_mutex_lock(&s_lock);
        s_sock = chosen;
        pthread_mutex_unlock(&s_lock);
        s_connect_started = now_ms();
        s_state = MQTT_CONNECT_INPROGRESS;
    } else {
        s_last_attempt = now_ms();
    }
}

/* CONNECT 完成后发送 CONNECT 报文并等待 CONNACK */
static int finish_connect(void)
{
    int err = 0;
    socklen_t elen = sizeof(err);
    /* ⚠ 每个 syscall 都在锁内**重新读** s_sock（阶段 121）：这样 close 不可能落在
       "读到 fd"与"用它"之间；读到 -1 就按失败处理（不许拿旧 fd 继续跑）。 */
    pthread_mutex_lock(&s_lock);
    int fd = s_sock;
    int getrc = (fd < 0) ? -1 : getsockopt(fd, SOL_SOCKET, SO_ERROR, &err, &elen);
    pthread_mutex_unlock(&s_lock);
    if (getrc != 0 || err != 0) {
        do_disconnect();
        return -1;
    }

    pthread_mutex_lock(&s_lock);
    fd = s_sock;
    if (fd < 0) { pthread_mutex_unlock(&s_lock); return -1; }
    sock_set_nonblock(fd, 0);
    /* 发送封顶（阶段 121）：`sock_send_all` 用的是**阻塞** send，broker 不收包时会把
       通信线程（以及调用 mqtt_publish 的 LVGL 线程）无限挂住。2s 足够覆盖 QoS0 小报文。 */
    struct timeval sndto;
    sndto.tv_sec = 2;
    sndto.tv_usec = 0;
    setsockopt(fd, SOL_SOCKET, SO_SNDTIMEO, &sndto, sizeof(sndto));
    int one = 1;
    setsockopt(fd, IPPROTO_TCP, TCP_NODELAY, &one, sizeof(one));
    pthread_mutex_unlock(&s_lock);

    uint8_t pkt[512];   /* 阶段 290：加上遗嘱（topic ≤127 ＋ payload ≤63）后 256 会不够 */
    int n = build_connect(pkt, sizeof(pkt));
    if (n < 0) { do_disconnect(); return -1; }
    int snd;
    pthread_mutex_lock(&s_lock);
    snd = (s_sock < 0) ? -1 : sock_send_all(s_sock, pkt, (size_t)n);
    pthread_mutex_unlock(&s_lock);
    if (snd != 0) { do_disconnect(); return -1; }
    s_last_sent = now_ms();
    s_state = MQTT_WAIT_CONNACK;
    return 0;
}

/* 处理 CONNACK 响应 */
static void handle_connack(const uint8_t* body, size_t blen)
{
    if (blen >= 2) {
        uint8_t rc = body[1];
        if (rc == 0) {
            s_state = MQTT_CONNECTED;
            s_last_ping_resp = now_ms();
            for (int i = 0; i < s_sub_count; i++) s_subs[i].pending_pid = 0; /* 触发重订阅 */
            fprintf(stderr, "[mqtt] connected to %s:%d\n", s_cfg.host, s_cfg.port);
            /* 上线通知：这里**不持锁**（process_rx 在锁外调用本函数）⇒ 回调里可安全 publish。 */
            if (s_connect_cb) s_connect_cb();
            return;
        }
        fprintf(stderr, "[mqtt] CONNACK error rc=%u\n", rc);
    }
    do_disconnect();
}

/* 发送下一个待订阅主题 */
static void send_next_subscribe(void)
{
    for (int i = 0; i < s_sub_count; i++) {
        mqtt_sub_t* st = &s_subs[i];
        if (st->pending_pid != 0) continue;
        uint16_t pid = ++s_packet_id;
        if (pid == 0) pid = ++s_packet_id;
        uint8_t pkt[192];
        int n = build_subscribe(pid, st->topic, pkt, sizeof(pkt));
        if (n < 0) continue;
        int snd;
        pthread_mutex_lock(&s_lock);
        snd = (s_sock < 0) ? -1 : sock_send_all(s_sock, pkt, (size_t)n);
        pthread_mutex_unlock(&s_lock);
        if (snd == 0) {
            st->pending_pid = pid;
            st->pending_at = now_ms();
            s_last_sent = now_ms();
        }
        return;   /* 一次一条，避免突发 */
    }
}

/* 处理 PUBLISH（QoS0） */
static void handle_publish(const uint8_t* body, size_t blen)
{
    if (blen < 2) return;
    size_t tlen = ((size_t)body[0] << 8) | body[1];
    if (blen < 2 + tlen) return;
    char topic[128];
    size_t cplen = tlen < sizeof(topic) - 1 ? tlen : sizeof(topic) - 1;
    memcpy(topic, &body[2], cplen);
    topic[cplen] = '\0';
    const char* payload = (const char*)&body[2 + tlen];
    int plen = (int)(blen - 2 - tlen);

    for (int i = 0; i < s_sub_count; i++) {
        if (strcmp(s_subs[i].topic, topic) == 0) {
            if (s_cb) s_cb(topic, payload, plen, s_subs[i].user);
            return;
        }
    }
    if (s_cb) s_cb(topic, payload, plen, NULL);
}

/* 解析接收缓冲中的完整报文 */
static void process_rx(void)
{
    for (;;) {
        if (s_rx_len < 2) return;
        uint32_t rl = 0;
        size_t used = 0;
        if (varint_decode(&s_rx[1], s_rx_len - 1, &used, &rl) != 0) {
            s_rx_len = 0;   /* 变长头损坏，丢弃 */
            return;
        }
        size_t total = 1 + used + rl;
        if (s_rx_len < total) return;              /* 等待完整包 */

        uint8_t type = (uint8_t)(s_rx[0] >> 4);
        const uint8_t* body = &s_rx[1 + used];
        switch (type) {
            case 2:  handle_connack(body, rl); break;                    /* CONNACK */
            case 3:  handle_publish(body, rl); break;                    /* PUBLISH */
            case 9:  /* SUBACK：忽略细节，标记对应订阅完成 */
                if (rl >= 3) {
                    uint16_t pid = (uint16_t)((body[0] << 8) | body[1]);
                    for (int i = 0; i < s_sub_count; i++)
                        if (s_subs[i].pending_pid == pid) s_subs[i].pending_pid = 0;
                }
                break;
            case 13: s_last_ping_resp = now_ms(); break;                 /* PINGRESP */
            default: break;
        }
        memmove(s_rx, &s_rx[total], s_rx_len - total);
        s_rx_len -= total;
    }
}

/* ── 公共 API ── */

int mqtt_start(const char* host, int port, const char* client_id,
               const char* user, const char* pass, int keepalive)
{
    memset(&s_cfg, 0, sizeof(s_cfg));
    snprintf(s_cfg.host, sizeof(s_cfg.host), "%s", host ? host : "127.0.0.1");
    s_cfg.port = port > 0 ? port : 1883;
    snprintf(s_cfg.client_id, sizeof(s_cfg.client_id), "%s", client_id ? client_id : "hmi");
    if (user) snprintf(s_cfg.user, sizeof(s_cfg.user), "%s", user);
    if (pass) snprintf(s_cfg.pass, sizeof(s_cfg.pass), "%s", pass);
    s_cfg.keepalive = keepalive > 0 ? keepalive : 30;
    s_state = MQTT_DISCONNECTED;
    s_last_attempt = 0;
    s_rx_len = 0;
    /* ⚠ **不清空订阅表**（阶段 290 修）：订阅表表达的是"宿主要订阅什么"，
     * 而 `mqtt_start` 的语义只是"用这组连接参数开始"。真实启动顺序是
     * `hmi_var_init()`（采纳出厂标签表 → 注册 /set/ 与 /cmd/ 主题）**先于**
     * `hmi_comm_start()` → `mqtt_start()`；旧版在这里写 `s_sub_count = 0`，
     * 于是**没有 .so 时（hmi_ui_load 失败，不会再触发一次重注册）订阅表恒为空**
     * —— 表现为"设备连上 broker 但一条远程命令都收不到"。
     * 清空订阅是 `mqtt_clear_topics()` 的职责（标签表采纳重建时显式调用）。 */
    return 0;
}

int mqtt_register_topic(const char* topic, void* user)
{
    if (!topic || s_sub_count >= MQTT_MAX_TOPICS) return -1;
    mqtt_sub_t* st = &s_subs[s_sub_count++];
    snprintf(st->topic, sizeof(st->topic), "%s", topic);
    st->user = user;
    st->pending_pid = 0;
    st->pending_at = 0;
    return 0;
}

void mqtt_clear_topics(void)
{
    s_sub_count = 0;
}

void mqtt_set_message_cb(mqtt_msg_cb_t cb) { s_cb = cb; }

void mqtt_set_connect_cb(mqtt_connect_cb_t cb) { s_connect_cb = cb; }

int mqtt_set_will(const char* topic, const char* payload, int retain)
{
    if (topic == NULL || topic[0] == '\0') return -1;
    size_t tlen = strlen(topic);
    size_t plen = payload != NULL ? strlen(payload) : 0;
    if (tlen >= sizeof(s_will_topic) || plen >= sizeof(s_will_payload)) return -1;
    memcpy(s_will_topic, topic, tlen + 1);
    memcpy(s_will_payload, payload != NULL ? payload : "", plen + 1);
    s_will_retain = retain ? 1 : 0;
    s_will_set = 1;
    return 0;
}

void mqtt_clear_will(void)
{
    s_will_set = 0;
    s_will_retain = 0;
    s_will_topic[0] = '\0';
    s_will_payload[0] = '\0';
}

int mqtt_publish(const char* topic, const char* payload, int len, int retain)
{
    if (s_state != MQTT_CONNECTED || !topic) return -1;
    uint8_t pkt[256];
    int n = build_publish(topic, payload, len, retain, pkt, sizeof(pkt));
    if (n < 0) return -1;
    pthread_mutex_lock(&s_lock);
    int rc = sock_send_all(s_sock, pkt, (size_t)n);
    if (rc == 0) s_last_sent = now_ms();
    pthread_mutex_unlock(&s_lock);
    return rc;
}

int mqtt_is_connected(void) { return s_state == MQTT_CONNECTED; }

void mqtt_poll(void)
{
    int64_t t = now_ms();

    switch (s_state) {
    case MQTT_DISCONNECTED:
        if (t - s_last_attempt >= MQTT_RECONNECT_MS) {
            s_last_attempt = t;
            start_connect();
        }
        return;

    case MQTT_CONNECT_INPROGRESS:
        if (t - s_connect_started > 3000) { do_disconnect(); return; }
        {
            /* ⚠ 锁内取 fd 并在锁内 select（阶段 121）：close_sock 也在同一把锁里，
               于是"读到 fd"与"用它"之间不可能被 close 切开。 */
            pthread_mutex_lock(&s_lock);
            int fd = s_sock;
            int ready = 0;
            if (fd >= 0) {
                fd_set wfds;
                struct timeval tv = { 0, 0 };
                FD_ZERO(&wfds);
                FD_SET(fd, &wfds);
                ready = select(fd + 1, NULL, &wfds, NULL, &tv) > 0;
            }
            pthread_mutex_unlock(&s_lock);
            if (ready) {
                finish_connect();   /* 它自己逐 syscall 取锁；内部失败会 do_disconnect */
            }
        }
        return;

    case MQTT_WAIT_CONNACK:
        if (t - s_last_sent > 5000) { do_disconnect(); return; }
        {
            uint8_t buf[64];
            ssize_t n;
            pthread_mutex_lock(&s_lock);
            n = (s_sock < 0) ? -1 : recv(s_sock, buf, sizeof(buf), MSG_DONTWAIT);
            pthread_mutex_unlock(&s_lock);
            if (n > 0) {
                if (s_rx_len + (size_t)n <= MQTT_RX_BUF) {
                    memcpy(&s_rx[s_rx_len], buf, (size_t)n);
                    s_rx_len += (size_t)n;
                }
                process_rx();   /* 不持锁：它可能回调用户（用户再调 publish 会自锁） */
            } else if (n == 0) {
                do_disconnect();
            }
        }
        return;

    case MQTT_CONNECTED:
        /* 收包 */
        for (;;) {
            uint8_t buf[128];
            ssize_t n;
            pthread_mutex_lock(&s_lock);
            n = (s_sock < 0) ? -1 : recv(s_sock, buf, sizeof(buf), MSG_DONTWAIT);
            pthread_mutex_unlock(&s_lock);
            if (n > 0) {
                if (s_rx_len + (size_t)n <= MQTT_RX_BUF) {
                    memcpy(&s_rx[s_rx_len], buf, (size_t)n);
                    s_rx_len += (size_t)n;
                }
                process_rx();   /* 同上：回调在锁外 */
            } else {
                if (n == 0) { do_disconnect(); return; }
                break;   /* EAGAIN（或 fd 已被并发关闭：下一轮按状态机处理） */
            }
        }
        /* 保活：空闲 keepalive/2 发 PINGREQ；超过 2*keepalive 未收 PINGRESP 判死 */
        if (t - s_last_sent >= (int64_t)s_cfg.keepalive * 500) {
            uint8_t ping[2] = { 0xC0, 0x00 };
            int snd;
            pthread_mutex_lock(&s_lock);
            snd = (s_sock < 0) ? -1 : sock_send_all(s_sock, ping, 2);
            pthread_mutex_unlock(&s_lock);
            if (snd != 0) { do_disconnect(); return; }
            s_last_sent = t;
        }
        if (t - s_last_ping_resp > (int64_t)s_cfg.keepalive * 2000) { do_disconnect(); return; }
        /* 订阅进度 */
        send_next_subscribe();
        /* 重订阅超时重发（>10s 未 SUBACK） */
        for (int i = 0; i < s_sub_count; i++) {
            if (s_subs[i].pending_pid != 0 && t - s_subs[i].pending_at > 10000)
                s_subs[i].pending_pid = 0;   /* 触发重发 */
        }
        return;
    }
}

void mqtt_stop(void)
{
    close_sock();
    s_state = MQTT_DISCONNECTED;
}

#ifdef HMI_UNIT_TEST
/*
 * 仅供 host 测试使用：把三个报文构建器暴露出来。
 * 判据是**容量语义**（不是"跑完没报错"）：
 *   · 长主题 / 长凭据必须建出正确的字节序列；
 *   · `cap` 不够必须返回 -1，且**一个字节都不许写到 out 之外**（调用方用前后哨兵验证）。
 * 之所以敢断言"不越界"，是因为这三个构建器现在只写 `out[0..cap)`，没有任何中间栈缓冲。
 */
int mqtt_test_build_connect(uint8_t* out, size_t cap)
{
    return build_connect(out, cap);
}

int mqtt_test_build_subscribe(uint16_t pid, const char* topic, uint8_t* out, size_t cap)
{
    return build_subscribe(pid, topic, out, cap);
}

int mqtt_test_build_publish(const char* topic, const char* payload, int len,
                            int retain, uint8_t* out, size_t cap)
{
    return build_publish(topic, payload, len, retain, out, cap);
}

/* 当前已注册订阅数（判据用：验证 mqtt_start 不再清空订阅表） */
int mqtt_test_sub_count(void) { return s_sub_count; }
#endif
