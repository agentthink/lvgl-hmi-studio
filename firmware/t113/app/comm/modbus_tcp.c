/*
 * modbus_tcp.c — Modbus TCP 驱动实现（eth0:502）
 */
#include "modbus_tcp.h"
#include "comm_core.h"
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

#define MB_TCP_READ_TMO_MS  3000
#define MB_TCP_RECONNECT_MS 5000   /* 断线后重连间隔 */

typedef struct {
    char host[64];
    int  port;
    int  timeout_ms;
} mb_tcp_cfg_t;

static pthread_mutex_t s_lock = PTHREAD_MUTEX_INITIALIZER;
static mb_tcp_cfg_t s_cfg;
static int s_sock = -1;
static uint16_t s_tid = 0;
static int64_t s_last_connect_attempt = 0;

static int64_t now_ms(void)
{
    struct timeval tv;
    gettimeofday(&tv, NULL);
    return (int64_t)tv.tv_sec * 1000 + tv.tv_usec / 1000;
}

/* ── 纯协议：MBAP 构建 ──
 * 帧结构：tid(2) + pid(2)=0 + length(2) + unit(1) + fn(1) + pdu
 * length = unit + fn + pdu 的字节数（即 pdulen + 2）
 */
size_t mb_tcp_build_mbap(uint16_t tid, uint8_t unit, uint8_t fn,
                         const uint8_t* pdu, size_t pdulen, uint8_t* out, size_t cap)
{
    size_t total = 8 + pdulen;   /* 6 字节 MBAP 头 + unit + fn + pdu */
    if (cap < total) return 0;
    out[0] = (uint8_t)(tid >> 8);  out[1] = (uint8_t)tid;
    out[2] = 0; out[3] = 0;                                    /* ProtocolId=0 */
    out[4] = (uint8_t)((pdulen + 2) >> 8);                     /* Length = unit+fn+pdu */
    out[5] = (uint8_t)(pdulen + 2);
    out[6] = unit;
    out[7] = fn;
    if (pdulen) memcpy(&out[8], pdu, pdulen);
    return total;
}

/* ── 纯协议：MBAP 解析 ──
 * 响应：tid + pid + length + unit + fn(+0x80) + pdu
 * pdu 输出不含 fn（读响应 = bc + 数据；写响应 = 回显数据）
 */
int mb_tcp_parse_mbap(const uint8_t* buf, size_t len, uint16_t expect_tid,
                      uint8_t expect_unit, uint8_t* pdu, size_t* pdulen,
                      uint8_t* excode)
{
    if (len < 9) return -1;                       /* 至少 6 头 + unit + fn + 1 字节 pdu */
    uint16_t tid = (uint16_t)((buf[0] << 8) | buf[1]);
    uint16_t pid = (uint16_t)((buf[2] << 8) | buf[3]);
    uint16_t length = (uint16_t)((buf[4] << 8) | buf[5]);
    if (pid != 0) return -1;
    if (tid != expect_tid) return -1;             /* TID 校验：防止帧交错 */
    if (buf[6] != expect_unit) return -1;
    if (length < 3 || len < 6 + (size_t)length) return -1;

    uint8_t fn = buf[7];
    if (fn == 0) return -1;
    if (fn & 0x80) {                              /* 异常响应 */
        if (excode) *excode = buf[8];
        return -2;
    }
    size_t plen = (size_t)length - 2;             /* 除去 unit+fn */
    if (pdu && pdulen) {
        if (*pdulen < plen) return -1;
        memcpy(pdu, &buf[8], plen);
        *pdulen = plen;
    }
    return 0;
}

/* ── 套接字 ── */

static void sock_set_nonblock(int fd, int nb)
{
    int fl = fcntl(fd, F_GETFL, 0);
    if (nb) fl |= O_NONBLOCK; else fl &= ~O_NONBLOCK;
    fcntl(fd, F_SETFL, fl);
}

static int sock_connect_timeout(const char* host, int port, int tmo_ms)
{
    struct addrinfo hints, *res = NULL;
    char portstr[8];
    snprintf(portstr, sizeof(portstr), "%d", port);
    memset(&hints, 0, sizeof(hints));
    hints.ai_family = AF_INET;
    hints.ai_socktype = SOCK_STREAM;
    if (getaddrinfo(host, portstr, &hints, &res) != 0) return -1;

    int fd = -1;
    for (struct addrinfo* ai = res; ai; ai = ai->ai_next) {
        fd = socket(ai->ai_family, ai->ai_socktype, ai->ai_protocol);
        if (fd < 0) continue;
        sock_set_nonblock(fd, 1);
        int rc = connect(fd, ai->ai_addr, ai->ai_addrlen);
        if (rc == 0) { sock_set_nonblock(fd, 0); break; }
        if (errno == EINPROGRESS) {
            struct timeval tv = { tmo_ms / 1000, (tmo_ms % 1000) * 1000 };
            fd_set wfds;
            FD_ZERO(&wfds);
            FD_SET(fd, &wfds);
            rc = select(fd + 1, NULL, &wfds, NULL, &tv);
            int err = 0;
            socklen_t elen = sizeof(err);
            getsockopt(fd, SOL_SOCKET, SO_ERROR, &err, &elen);
            if (rc > 0 && err == 0) { sock_set_nonblock(fd, 0); break; }
        }
        close(fd);
        fd = -1;
    }
    freeaddrinfo(res);
    if (fd >= 0) {
        int one = 1;
        setsockopt(fd, IPPROTO_TCP, TCP_NODELAY, &one, sizeof(one));
    }
    return fd;
}

static int sock_send_all(int fd, const uint8_t* buf, size_t len, int tmo_ms)
{
    size_t sent = 0;
    struct timeval tv;
    while (sent < len) {
        tv.tv_sec = tmo_ms / 1000;
        tv.tv_usec = (tmo_ms % 1000) * 1000;
        fd_set wfds;
        FD_ZERO(&wfds);
        FD_SET(fd, &wfds);
        if (select(fd + 1, NULL, &wfds, NULL, &tv) <= 0) return -1;
        ssize_t n = send(fd, &buf[sent], len - sent, 0);
        if (n <= 0) return -1;
        sent += (size_t)n;
    }
    return 0;
}

/* 读取完整一帧：6 字节头 + length 指定长度 */
static int sock_recv_frame(int fd, uint8_t* buf, size_t cap, int tmo_ms)
{
    size_t n = 0;
    struct timeval tv;
    while (n < cap) {
        tv.tv_sec = tmo_ms / 1000;
        tv.tv_usec = (tmo_ms % 1000) * 1000;
        fd_set rfds;
        FD_ZERO(&rfds);
        FD_SET(fd, &rfds);
        int r = select(fd + 1, &rfds, NULL, NULL, &tv);
        if (r <= 0) return -1;
        ssize_t got = recv(fd, &buf[n], cap - n, 0);
        if (got <= 0) return -1;
        n += (size_t)got;
        if (n >= 6) {
            size_t need = 6 + (size_t)((buf[4] << 8) | buf[5]);
            if (need > cap) return -1;
            if (n >= need) break;
        }
        tmo_ms = 100;   /* 剩余字节按短超时等待 */
    }
    return (int)n;
}

int mb_tcp_init(const char* host, int port, int timeout_ms)
{
    memset(&s_cfg, 0, sizeof(s_cfg));
    snprintf(s_cfg.host, sizeof(s_cfg.host), "%s", host ? host : "127.0.0.1");
    s_cfg.port = port > 0 ? port : 502;
    s_cfg.timeout_ms = timeout_ms > 0 ? timeout_ms : MB_TCP_READ_TMO_MS;
    s_sock = -1;
    s_last_connect_attempt = 0;
    return 0;
}

/* 每轮询周期调用：断线后按间隔重连 */
int mb_tcp_poll(void)
{
    pthread_mutex_lock(&s_lock);
    if (s_sock >= 0) { pthread_mutex_unlock(&s_lock); return 0; }
    int64_t t = now_ms();
    if (t - s_last_connect_attempt < MB_TCP_RECONNECT_MS) {
        pthread_mutex_unlock(&s_lock);
        return -1;
    }
    s_last_connect_attempt = t;
    int fd = sock_connect_timeout(s_cfg.host, s_cfg.port, 3000);
    if (fd >= 0) {
        s_sock = fd;
        fprintf(stderr, "[modbus-tcp] connected %s:%d\n", s_cfg.host, s_cfg.port);
    }
    pthread_mutex_unlock(&s_lock);
    return s_sock >= 0 ? 0 : -1;
}

int mb_tcp_is_connected(void)
{
    pthread_mutex_lock(&s_lock);
    int ok = s_sock >= 0;
    pthread_mutex_unlock(&s_lock);
    return ok;
}

/* 执行事务：构建请求 → 发送 → 读取 → 校验 TID */
static int execute(uint8_t unit, uint8_t fn, const uint8_t* pdu, size_t pdulen,
                   uint8_t* rsp, size_t* rsplen, uint8_t* excode)
{
    pthread_mutex_lock(&s_lock);
    if (s_sock < 0) { pthread_mutex_unlock(&s_lock); return -1; }

    uint16_t tid = ++s_tid;   /* 互斥锁保护递增，防帧交错 */
    uint8_t frame[300];
    size_t flen = mb_tcp_build_mbap(tid, unit, fn, pdu, pdulen, frame, sizeof(frame));
    if (flen == 0) { pthread_mutex_unlock(&s_lock); return -1; }

    if (sock_send_all(s_sock, frame, flen, 2000) != 0) {
        close(s_sock); s_sock = -1;
        pthread_mutex_unlock(&s_lock);
        return -1;
    }
    uint8_t buf[300];
    int n = sock_recv_frame(s_sock, buf, sizeof(buf), s_cfg.timeout_ms);
    if (n < 0) {
        close(s_sock); s_sock = -1;
        pthread_mutex_unlock(&s_lock);
        return -1;
    }
    pthread_mutex_unlock(&s_lock);

    size_t plen = *rsplen;
    int rc = mb_tcp_parse_mbap(buf, (size_t)n, tid, unit, rsp, &plen, excode);
    if (rc == -2) {
        fprintf(stderr, "[modbus-tcp] unit=%d fn=0x%02X exception 0x%02X\n", unit, fn, excode ? *excode : 0);
        return -1;
    }
    if (rc != 0) return -1;
    *rsplen = plen;
    return 0;
}

int mb_tcp_read_regs(uint8_t unit, uint8_t fn, uint16_t off, uint16_t count, uint16_t* out)
{
    if (!out || count == 0 || count > 125) return -1;
    uint8_t pdu[4];
    pdu[0] = (uint8_t)(off >> 8); pdu[1] = (uint8_t)off;
    pdu[2] = (uint8_t)(count >> 8); pdu[3] = (uint8_t)count;

    uint8_t rsp[256];
    size_t rsplen = sizeof(rsp);
    uint8_t excode = 0;
    if (execute(unit, fn, pdu, 4, rsp, &rsplen, &excode) != 0) return -1;

    uint8_t bc = rsp[0];                          /* 字节数（读响应首个字节） */
    if (fn == MB_FC_READ_COILS || fn == MB_FC_READ_DISCRETE) {
        size_t need = (size_t)((count + 7) / 8);
        if ((size_t)bc < need || rsplen < 1 + need) return -1;
        for (uint16_t i = 0; i < count; i++)
            out[i] = (rsp[1 + i / 8] >> (i % 8)) & 1;
    } else {
        if ((size_t)bc < (size_t)(count * 2) || rsplen < 1 + (size_t)(count * 2)) return -1;
        for (uint16_t i = 0; i < count; i++)
            out[i] = (uint16_t)((rsp[1 + i * 2] << 8) | rsp[2 + i * 2]);
    }
    return 0;
}

int mb_tcp_write_reg(uint8_t unit, uint8_t fn, uint16_t off, uint16_t val)
{
    uint16_t v = (fn == MB_FC_WRITE_COIL) ? (val ? 0xFF00 : 0x0000) : val;
    uint8_t pdu[4];
    pdu[0] = (uint8_t)(off >> 8); pdu[1] = (uint8_t)off;
    pdu[2] = (uint8_t)(v >> 8);   pdu[3] = (uint8_t)v;
    uint8_t rsp[16];
    size_t rsplen = sizeof(rsp);
    uint8_t excode = 0;
    if (execute(unit, fn, pdu, 4, rsp, &rsplen, &excode) != 0) return -1;
    /* 校验回显：off/val 与请求一致 */
    if (rsplen < 4) return -1;
    if (pdu[0] != rsp[0] || pdu[1] != rsp[1] || pdu[2] != rsp[2] || pdu[3] != rsp[3])
        return -1;
    return 0;
}

int mb_tcp_write_regs(uint8_t unit, uint16_t off, const uint16_t* regs, uint16_t count)
{
    if (!regs || count == 0 || count > 123) return -1;
    uint8_t pdu[2 + 1 + 2 * count];
    pdu[0] = (uint8_t)(off >> 8); pdu[1] = (uint8_t)off;
    pdu[2] = (uint8_t)(count * 2);
    for (uint16_t i = 0; i < count; i++) {
        pdu[3 + i * 2]     = (uint8_t)(regs[i] >> 8);
        pdu[3 + i * 2 + 1] = (uint8_t)regs[i];
    }
    uint8_t rsp[16];
    size_t rsplen = sizeof(rsp);
    uint8_t excode = 0;
    if (execute(unit, MB_FC_WRITE_MULTI_REG, pdu, sizeof(pdu), rsp, &rsplen, &excode) != 0)
        return -1;
    /* 校验回显：off/count 与请求一致 */
    if (rsplen < 4) return -1;
    if (pdu[0] != rsp[0] || pdu[1] != rsp[1] || pdu[2] != rsp[2] || pdu[3] != rsp[3])
        return -1;
    return 0;
}
