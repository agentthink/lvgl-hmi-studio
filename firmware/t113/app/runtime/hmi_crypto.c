/*
 * hmi_crypto.c — SHA-256 / Base64 / 定长比较（见 hmi_crypto.h 的契约说明）
 *
 * ── 正确性怎么被钉住 ──
 * SHA-256 是"写错一个常量就静默产出错误摘要"的典型：它不会崩、不会报错，只会让
 * 所有账户都登不上（或更糟：让某个错误口令意外通过）。所以本文件不接受"看着像对"，
 * 必须由 tests/hmi_crypto_test.c 用**外部真值**钉住：
 *   1) NIST 标准向量：SHA256("")、SHA256("abc")、双块 56 字节向量
 *   2) 本工程向量：salt=00..0F + "op1234" 在 1000 轮下的 32 字节（由 Python hashlib 独立算出，
 *      与编辑器侧 RuntimePasswordHasherTests 用的是**同一组**）
 * 第 2 条同时钉住了"迭代次数语义"——循环少跑一轮或多跑一轮都会红。
 */
#include "hmi_crypto.h"
#include <string.h>
#include <stdint.h>

/* ── SHA-256 ── */

static const uint32_t HMI_SHA256_K[64] = {
    0x428a2f98u, 0x71374491u, 0xb5c0fbcfu, 0xe9b5dba5u,
    0x3956c25bu, 0x59f111f1u, 0x923f82a4u, 0xab1c5ed5u,
    0xd807aa98u, 0x12835b01u, 0x243185beu, 0x550c7dc3u,
    0x72be5d74u, 0x80deb1feu, 0x9bdc06a7u, 0xc19bf174u,
    0xe49b69c1u, 0xefbe4786u, 0x0fc19dc6u, 0x240ca1ccu,
    0x2de92c6fu, 0x4a7484aau, 0x5cb0a9dcu, 0x76f988dau,
    0x983e5152u, 0xa831c66du, 0xb00327c8u, 0xbf597fc7u,
    0xc6e00bf3u, 0xd5a79147u, 0x06ca6351u, 0x14292967u,
    0x27b70a85u, 0x2e1b2138u, 0x4d2c6dfcu, 0x53380d13u,
    0x650a7354u, 0x766a0abbu, 0x81c2c92eu, 0x92722c85u,
    0xa2bfe8a1u, 0xa81a664bu, 0xc24b8b70u, 0xc76c51a3u,
    0xd192e819u, 0xd6990624u, 0xf40e3585u, 0x106aa070u,
    0x19a4c116u, 0x1e376c08u, 0x2748774cu, 0x34b0bcb5u,
    0x391c0cb3u, 0x4ed8aa4au, 0x5b9cca4fu, 0x682e6ff3u,
    0x748f82eeu, 0x78a5636fu, 0x84c87814u, 0x8cc70208u,
    0x90befffau, 0xa4506cebu, 0xbef9a3f7u, 0xc67178f2u,
};

typedef struct {
    uint32_t h[8];
    uint64_t total;                     /* 已吃进的消息字节数 */
    unsigned char buf[64];
    size_t buflen;
} hmi_sha256_ctx;

static uint32_t hmi_rotr(uint32_t x, int n)
{
    return (x >> n) | (x << (32 - n));
}

static void hmi_sha256_init(hmi_sha256_ctx* c)
{
    c->h[0] = 0x6a09e667u; c->h[1] = 0xbb67ae85u;
    c->h[2] = 0x3c6ef372u; c->h[3] = 0xa54ff53au;
    c->h[4] = 0x510e527fu; c->h[5] = 0x9b05688cu;
    c->h[6] = 0x1f83d9abu; c->h[7] = 0x5be0cd19u;
    c->total = 0u;
    c->buflen = 0u;
}

static void hmi_sha256_block(hmi_sha256_ctx* c, const unsigned char* p)
{
    uint32_t w[64];
    uint32_t a, b, cc, d, e, f, g, h;
    int i;

    for (i = 0; i < 16; i++) {
        w[i] = ((uint32_t)p[i * 4] << 24) | ((uint32_t)p[i * 4 + 1] << 16)
             | ((uint32_t)p[i * 4 + 2] << 8) | (uint32_t)p[i * 4 + 3];
    }
    for (i = 16; i < 64; i++) {
        const uint32_t s0 = hmi_rotr(w[i - 15], 7) ^ hmi_rotr(w[i - 15], 18) ^ (w[i - 15] >> 3);
        const uint32_t s1 = hmi_rotr(w[i - 2], 17) ^ hmi_rotr(w[i - 2], 19) ^ (w[i - 2] >> 10);
        w[i] = w[i - 16] + s0 + w[i - 7] + s1;
    }

    a = c->h[0]; b = c->h[1]; cc = c->h[2]; d = c->h[3];
    e = c->h[4]; f = c->h[5]; g = c->h[6]; h = c->h[7];

    for (i = 0; i < 64; i++) {
        const uint32_t S1 = hmi_rotr(e, 6) ^ hmi_rotr(e, 11) ^ hmi_rotr(e, 25);
        const uint32_t ch = (e & f) ^ ((~e) & g);
        const uint32_t t1 = h + S1 + ch + HMI_SHA256_K[i] + w[i];
        const uint32_t S0 = hmi_rotr(a, 2) ^ hmi_rotr(a, 13) ^ hmi_rotr(a, 22);
        const uint32_t maj = (a & b) ^ (a & cc) ^ (b & cc);
        const uint32_t t2 = S0 + maj;

        h = g; g = f; f = e; e = d + t1;
        d = cc; cc = b; b = a; a = t1 + t2;
    }

    c->h[0] += a; c->h[1] += b; c->h[2] += cc; c->h[3] += d;
    c->h[4] += e; c->h[5] += f; c->h[6] += g; c->h[7] += h;
}

static void hmi_sha256_update(hmi_sha256_ctx* c, const unsigned char* p, size_t len)
{
    c->total += (uint64_t)len;
    while (len > 0) {
        const size_t space = 64u - c->buflen;
        const size_t take = (len < space) ? len : space;
        memcpy(c->buf + c->buflen, p, take);
        c->buflen += take;
        p += take;
        len -= take;
        if (c->buflen == 64u) {
            hmi_sha256_block(c, c->buf);
            c->buflen = 0u;
        }
    }
}

static void hmi_sha256_final(hmi_sha256_ctx* c, unsigned char out[HMI_SHA256_DIGEST_BYTES])
{
    const uint64_t bits = c->total * 8ull;
    size_t i = c->buflen;
    int k;

    c->buf[i++] = 0x80u;
    if (i > 56u) {
        while (i < 64u) c->buf[i++] = 0x00u;
        hmi_sha256_block(c, c->buf);
        i = 0u;
    }
    while (i < 56u) c->buf[i++] = 0x00u;
    for (k = 0; k < 8; k++)
        c->buf[56 + k] = (unsigned char)(bits >> (56 - k * 8));
    hmi_sha256_block(c, c->buf);

    for (k = 0; k < 8; k++) {
        out[k * 4 + 0] = (unsigned char)(c->h[k] >> 24);
        out[k * 4 + 1] = (unsigned char)(c->h[k] >> 16);
        out[k * 4 + 2] = (unsigned char)(c->h[k] >> 8);
        out[k * 4 + 3] = (unsigned char)(c->h[k]);
    }
}

void hmi_sha256(const unsigned char* data, size_t len, unsigned char out[HMI_SHA256_DIGEST_BYTES])
{
    hmi_sha256_ctx c;
    hmi_sha256_init(&c);
    hmi_sha256_update(&c, data, len);
    hmi_sha256_final(&c, out);
}

int hmi_sha256_iter(const unsigned char* salt, size_t salt_len,
                    const char* password, int iterations,
                    unsigned char out[HMI_SHA256_DIGEST_BYTES])
{
    hmi_sha256_ctx c;
    int i;

    if (salt == NULL || password == NULL || out == NULL) return -1;
    if (iterations < 1) return -1;

    hmi_sha256_init(&c);
    hmi_sha256_update(&c, salt, salt_len);
    /* 口令按 UTF-8 字节参与 —— 与 C# 侧 Encoding.UTF8.GetBytes 一致 */
    hmi_sha256_update(&c, (const unsigned char*)password, strlen(password));
    hmi_sha256_final(&c, out);

    for (i = 1; i < iterations; i++) {
        hmi_sha256_init(&c);
        hmi_sha256_update(&c, out, HMI_SHA256_DIGEST_BYTES);
        hmi_sha256_final(&c, out);
    }
    return 0;
}

/* ── Base64 解码 ── */

static int hmi_b64_value(int ch)
{
    if (ch >= 'A' && ch <= 'Z') return ch - 'A';
    if (ch >= 'a' && ch <= 'z') return ch - 'a' + 26;
    if (ch >= '0' && ch <= '9') return ch - '0' + 52;
    if (ch == '+') return 62;
    if (ch == '/') return 63;
    return -1;
}

int hmi_base64_decode(const char* in, unsigned char* out, size_t out_cap)
{
    size_t len, o, i;
    int j;

    if (in == NULL || out == NULL) return -1;
    len = strlen(in);
    if (len == 0u || (len % 4u) != 0u) return -1;

    o = 0u;
    for (i = 0u; i < len; i += 4u) {
        int v[4];
        int pad = 0;
        unsigned char b[3];
        int n, k;

        for (j = 0; j < 4; j++) {
            const int ch = (int)(unsigned char)in[i + (size_t)j];
            if (ch == '=') {
                /* '=' 只能出现在最后一组，且只能占该组的后两个位置 */
                if (i + 4u != len || j < 2) return -1;
                v[j] = 0;
                pad++;
            } else {
                if (pad > 0) return -1;      /* '=' 之后不得再有数据字符 */
                v[j] = hmi_b64_value(ch);
                if (v[j] < 0) return -1;
            }
        }

        b[0] = (unsigned char)((v[0] << 2) | (v[1] >> 4));
        b[1] = (unsigned char)(((v[1] & 0x0F) << 4) | (v[2] >> 2));
        b[2] = (unsigned char)(((v[2] & 0x03) << 6) | v[3]);

        n = 3 - pad;
        for (k = 0; k < n; k++) {
            if (o >= out_cap) return -1;
            out[o++] = b[k];
        }
    }
    return (int)o;
}

/* ── 定长比较 ── */

int hmi_ct_equal(const unsigned char* a, const unsigned char* b, size_t len)
{
    unsigned char diff = 0u;
    size_t i;

    if (a == NULL || b == NULL) return 0;
    for (i = 0u; i < len; i++)
        diff |= (unsigned char)(a[i] ^ b[i]);
    return diff == 0u;
}
