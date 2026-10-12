/*
 * hmi_crypto_test.c — hmi_crypto.c 的主机侧测试（不依赖 LVGL / 硬件）
 *
 * 期望值全部来自**外部真值**，不是"用本实现算一遍再把结果存下来":
 *   - SHA-256：标准教学向量，长度覆盖 0 / 3 / 43 / 56 / 64 / 65 / 112 字节 ——
 *     其中 56 触发"padding 跨块"分支（hmi_sha256_final 里 i > 56 那条），
 *     64 / 65 触发 update 的分块边界。
 *   - 迭代哈希：由 Python hashlib 独立算出，与编辑器侧
 *     tests/HmiEditor.Core.Tests/Support/RuntimeCredentialVectors.cs 用的是**同一组**。
 *
 * 为什么必须外部真值：两侧实现（C# 与 C）一起错时，"各自自洽"的测试全绿，
 * 而现场表现是所有账户都登不上、且看不出哪边错。这是跨语言契约最危险的失效形态。
 *
 * 编译（见 run_host_tests.sh）：
 *   gcc -std=c99 -Wall -Wextra -Iapp/runtime tests/hmi_crypto_test.c \
 *       app/runtime/hmi_crypto.c -o /tmp/hmi_crypto_test
 */
#include <stdio.h>
#include <string.h>
#include "hmi_crypto.h"

static int s_pass;
static int s_fail;

static void check(int condition, const char* what)
{
    if (condition) {
        s_pass++;
        printf("  PASS  %s\n", what);
    } else {
        s_fail++;
        printf("  FAIL  %s\n", what);
    }
}

static void hex_of(const unsigned char* bytes, size_t n, char* out)
{
    static const char* digits = "0123456789abcdef";
    size_t i;
    for (i = 0; i < n; i++) {
        out[i * 2] = digits[bytes[i] >> 4];
        out[i * 2 + 1] = digits[bytes[i] & 0x0Fu];
    }
    out[n * 2] = '\0';
}

static void check_hex(const unsigned char* got, size_t n, const char* expect, const char* what)
{
    char buf[HMI_SHA256_DIGEST_BYTES * 2 + 1];
    hex_of(got, n, buf);
    if (strcmp(buf, expect) == 0) {
        s_pass++;
        printf("  PASS  %s\n", what);
    } else {
        s_fail++;
        printf("  FAIL  %s\n        期望 %s\n        实际 %s\n", what, expect, buf);
    }
}

static void check_sha256(const char* label, const char* msg, const char* expect)
{
    unsigned char out[HMI_SHA256_DIGEST_BYTES];
    hmi_sha256((const unsigned char*)msg, strlen(msg), out);
    check_hex(out, HMI_SHA256_DIGEST_BYTES, expect, label);
}

int main(void)
{
    unsigned char out[HMI_SHA256_DIGEST_BYTES];
    unsigned char salt[16];
    int i;

    printf("=== hmi_crypto: SHA-256 / Base64 / 定长比较 ===\n");

    /* ── 1. SHA-256 标准向量 ── */
    printf("--- SHA-256（标准向量，覆盖 padding 与分块边界）---\n");

    hmi_sha256((const unsigned char*)"", 0, out);
    check_hex(out, HMI_SHA256_DIGEST_BYTES,
              "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
              "SHA256(\"\") 空输入");

    check_sha256("SHA256(\"abc\")",
        "abc",
        "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");

    check_sha256("SHA256(43 字节) —— 单块内",
        "The quick brown fox jumps over the lazy dog",
        "d7a8fbb307d7809469ca9abcb0082e4f8d5651e46d3cdb762d02d0bf37c9e592");

    check_sha256("SHA256(56 字节) —— padding 跨块（i > 56 分支）",
        "abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq",
        "248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1");

    check_sha256("SHA256(112 字节) —— 两块消息",
        "abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmno"
        "ijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu",
        "cf5b16a778af8380036ce59e7b0492370b249b11e8f07a51afac45037afee9d1");

    {
        /* 64 / 65 字节：正好一个块 与 一个块 + 1 字节（update 分块边界） */
        char buf64[65];
        char buf65[66];
        for (i = 0; i < 64; i++) buf64[i] = 'a';
        buf64[64] = '\0';
        for (i = 0; i < 65; i++) buf65[i] = 'a';
        buf65[65] = '\0';

        check_sha256("SHA256(64 字节) —— 正好整块",
            buf64, "ffe054fe7ae0cb6dc65c3af9b61d5209f439851db43d0ba5997337df154668eb");
        check_sha256("SHA256(65 字节) —— 整块 + 1",
            buf65, "635361c48bb9eab14198e76ea8ab7f1a41685d6ad62aa9146d301d4f17eb0ae0");
    }

    /* ── 2. 迭代哈希（跨语言契约）── */
    printf("--- 迭代哈希（与编辑器 RuntimePasswordHasher 同源向量）---\n");

    for (i = 0; i < 16; i++) salt[i] = (unsigned char)i;

    check(hmi_sha256_iter(salt, 16, "op1234", 1000, out) == 0, "iter=1000 返回 0");
    check_hex(out, HMI_SHA256_DIGEST_BYTES,
              "c0dbb584355371bd9739789d1a091be1cf4612beb0c630c6006687d955ab38b9",
              "salt=00..0F + \"op1234\" 迭代 1000 轮");

    check(hmi_sha256_iter(salt, 16, "op1234", 3, out) == 0, "iter=3 返回 0");
    check_hex(out, HMI_SHA256_DIGEST_BYTES,
              "b3e3209bcc92878e176d186d4c0fdaaabdf6b74a7b2b0e5d59dc6814dba7492a",
              "同一向量迭代 3 轮（中间档，钉住循环次数）");

    check(hmi_sha256_iter(salt, 16, "op1234", 1, out) == 0, "iter=1 返回 0");
    check_hex(out, HMI_SHA256_DIGEST_BYTES,
              "45defc078498f27aa6cea9e203b76283e587de0a5c933f2562142fca1bc9ae0b",
              "同一向量迭代 1 轮（下界：不得多跑一轮）");

    /* 换 salt（0x10..0x1F）与口令，证明 salt 真的参与运算 */
    for (i = 0; i < 16; i++) salt[i] = (unsigned char)(i + 16);
    check(hmi_sha256_iter(salt, 16, "eng1234", 1000, out) == 0, "第二用户 iter=1000 返回 0");
    check_hex(out, HMI_SHA256_DIGEST_BYTES,
              "0abb36e4cb6b6eeef1ce335e1c42fb036a5ad2973f3dc274ebb237a21674fd4e",
              "salt=10..1F + \"eng1234\" 迭代 1000 轮");

    /* 迭代 1 轮必须退化成"单轮 SHA256(salt‖pwd)" —— 用独立的单次计算交叉验证 */
    for (i = 0; i < 16; i++) salt[i] = (unsigned char)i;
    {
        unsigned char combined[16 + 6];
        unsigned char once[HMI_SHA256_DIGEST_BYTES];
        unsigned char iter1[HMI_SHA256_DIGEST_BYTES];

        memcpy(combined, salt, 16);
        memcpy(combined + 16, "op1234", 6);
        hmi_sha256(combined, sizeof(combined), once);
        check(hmi_sha256_iter(salt, 16, "op1234", 1, iter1) == 0, "iter=1 计算成功");
        check(memcmp(once, iter1, HMI_SHA256_DIGEST_BYTES) == 0,
              "iter=1 等价于单次 SHA256(salt‖password)");
    }

    check(hmi_sha256_iter(salt, 16, "op1234", 0, out) == -1, "iter=0 拒绝（返回 -1）");
    check(hmi_sha256_iter(salt, 16, "op1234", -3, out) == -1, "iter 为负拒绝");
    check(hmi_sha256_iter(NULL, 16, "op1234", 1, out) == -1, "salt 为 NULL 拒绝");
    check(hmi_sha256_iter(salt, 16, NULL, 1, out) == -1, "password 为 NULL 拒绝");

    /* 空口令也要能算（不是"拒绝"）—— 否则"密码为空"会变成崩溃或不一致。
     * 期望值 = SHA256(00..0F)，由 Python 独立算出。 */
    check(hmi_sha256_iter(salt, 16, "", 1, out) == 0, "空口令可计算（不拒绝）");
    check_hex(out, HMI_SHA256_DIGEST_BYTES,
              "be45cb2605bf36bebde684841a28f0fd43c69850a3dce5fedba69928ee3a8991",
              "salt=00..0F + 空口令 迭代 1 轮（= SHA256(salt)）");

    /* ── 3. Base64 解码 ── */
    printf("--- Base64 解码 ---\n");
    {
        unsigned char buf[64];
        int n;

        n = hmi_base64_decode("AAECAwQFBgcICQoLDA0ODw==", buf, sizeof(buf));
        check(n == 16, "24 字符（含填充）→ 16 字节");
        if (n == 16) {
            int ok = 1;
            for (i = 0; i < 16; i++)
                if (buf[i] != (unsigned char)i) ok = 0;
            check(ok == 1, "解出的字节恰为 00..0F");
        }

        n = hmi_base64_decode("EBESExQVFhcYGRobHB0eHw==", buf, sizeof(buf));
        check(n == 16, "第二把 salt 解出 16 字节");
        if (n == 16) {
            int ok = 1;
            for (i = 0; i < 16; i++)
                if (buf[i] != (unsigned char)(i + 16)) ok = 0;
            check(ok == 1, "解出的字节恰为 10..1F");
        }

        check(hmi_base64_decode("", buf, sizeof(buf)) == -1, "空串拒绝");
        check(hmi_base64_decode("AAA", buf, sizeof(buf)) == -1, "长度非 4 的倍数拒绝");
        check(hmi_base64_decode("AA=A", buf, sizeof(buf)) == -1, "'=' 在组内非末位拒绝");
        check(hmi_base64_decode("A===", buf, sizeof(buf)) == -1, "'=' 占三个位置拒绝");
        check(hmi_base64_decode("AA*A", buf, sizeof(buf)) == -1, "非法字符拒绝");
        check(hmi_base64_decode("AAAAAAAA=AAA", buf, sizeof(buf)) == -1, "'=' 之后仍有数据拒绝");
        check(hmi_base64_decode("AAA=", buf, 1) == -1, "out_cap 不足拒绝（且不越界）");
        check(hmi_base64_decode("AAA=", buf, 2) == 2, "out_cap 恰够时解出 2 字节");
    }

    /* ── 4. 定长比较 ── */
    printf("--- 定长比较 ---\n");
    {
        const unsigned char a[4] = { 1, 2, 3, 4 };
        const unsigned char same[4] = { 1, 2, 3, 4 };
        const unsigned char diffLast[4] = { 1, 2, 3, 5 };
        const unsigned char diffFirst[4] = { 9, 2, 3, 4 };

        check(hmi_ct_equal(a, same, 4) == 1, "完全相同返回 1");
        check(hmi_ct_equal(a, diffLast, 4) == 0, "末字节不同返回 0");
        check(hmi_ct_equal(a, diffFirst, 4) == 0, "首字节不同返回 0");
        check(hmi_ct_equal(a, same, 0) == 1, "长度 0 恒返回 1");
        check(hmi_ct_equal(NULL, same, 4) == 0, "NULL 指针返回 0");
    }

    printf("=== 结果: %d 通过, %d 失败 ===\n", s_pass, s_fail);
    return s_fail == 0 ? 0 : 1;
}
