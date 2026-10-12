/*
 * hmi_crypto.h — 运行时凭据所需的密码学原语（SHA-256 / Base64 / 定长比较）
 *
 * ── 为什么固件自带一份而不是引 mbedtls ──
 * 需要的只有三个原语，且都不涉密钥管理/TLS。引入 mbedtls 会把一个安全库拖进构建，
 * 而它 99% 的功能这里用不到（固件全树此前零命中 mbedtls）。自带的代价是必须被
 * **固定向量**钉住 —— 见 hmi_crypto.c 顶部注释与 tests/hmi_crypto_test.c。
 *
 * ── 跨语言契约 ──
 * hmi_sha256_iter() 的输出必须与编辑器 Core/Security/RuntimePasswordHasher.cs 的
 * Iterate() **逐位一致**（同一组 salt/口令/轮数 ⇒ 同一 32 字节）。两侧用同一组
 * 外部真值（NIST SHA-256 向量 + 由 Python 独立算出的本工程向量）各测一遍。
 *
 * ── 不依赖 LVGL ──
 * 只依赖 stddef/stdint/string，因此可被主机侧测试直接 gcc 编译（不建 LVGL 环境）。
 */
#ifndef HMI_CRYPTO_H
#define HMI_CRYPTO_H

#include <stddef.h>

/* SHA-256 摘要字节数 */
#define HMI_SHA256_DIGEST_BYTES 32

/* 单次 SHA-256：把 data[0..len) 的摘要写入 out[HMI_SHA256_DIGEST_BYTES] */
void hmi_sha256(const unsigned char* data, size_t len, unsigned char out[HMI_SHA256_DIGEST_BYTES]);

/*
 * 迭代哈希 —— 与编辑器 RuntimePasswordHasher.Iterate 逐位一致的**跨语言契约**：
 *
 *   h0  = SHA256( salt ‖ utf8(password) )
 *   h_n = SHA256( h_{n-1} )            n = 1 … iterations-1
 *   结果 = h_{iterations-1}
 *
 * password 按 UTF-8 字节序列参与运算（即 C 字符串的 strlen 字节数）。
 * iterations < 1 视为无效，返回 -1 且不写 out。
 */
int hmi_sha256_iter(const unsigned char* salt, size_t salt_len,
                    const char* password, int iterations,
                    unsigned char out[HMI_SHA256_DIGEST_BYTES]);

/*
 * 标准 Base64 解码（字母表 A-Za-z0-9+/，'=' 填充）。
 * 返回写入 out 的字节数；以下情况一律返回 -1：
 *   - 长度为 0 或不是 4 的倍数
 *   - 出现非法字符
 *   - '=' 出现在非末组、或末组的前两个位置
 *   - 解码结果超过 out_cap（不写越界）
 */
int hmi_base64_decode(const char* in, unsigned char* out, size_t out_cap);

/*
 * 定长比较（不提前返回，避免时序侧信道）。
 * 长度由调用方保证一致；此处不做长度检查。
 */
int hmi_ct_equal(const unsigned char* a, const unsigned char* b, size_t len);

#endif /* HMI_CRYPTO_H */
