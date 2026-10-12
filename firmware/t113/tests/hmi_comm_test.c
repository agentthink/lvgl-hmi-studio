/*
 * hmi_comm_test.c — 主机侧单元测试（不依赖 LVGL/T113 硬件）
 *
 * 覆盖：
 *   - Modbus RTU CRC16（标准向量 + 参考实现交叉验证）
 *   - RTU 帧构建/解析（正常/异常/CRC 损坏/从站不匹配）
 *   - Modbus TCP MBAP 构建/解析（TID 校验防帧交错/异常响应）
 *   - 地址解析（40001 / 00001 / h:0 / 3:0 / 非法）
 *   - 变量表轮询组构建（块合并）与值转换（Bool/U16/I16/U32/I32/Float）
 *   - 写路径投递与消费（未连接时安全降级）
 *   - MQTT **主题/payload 契约 v1**（mqtt_contract.c）：跨语言同一份样例表 ＋ 非法参数
 *     （空/含 `/` 的变量名）＋ 容量不足必须返回 -2 且一个字节都不写（阶段 264，登记 §2-#12 的 M1）
 *   - MQTT 报文构建（CONNECT/SUBSCRIBE/PUBLISH）：长主题 / 长凭据的容量边界 ——
 *     容量不足必须返回 -1 且一个字节都不写；`out` 前后放哨兵，越界即失败。
 *     编译时额外加 -fstack-protector-all：函数**内部**栈缓冲的越界写只有金丝雀看得见。
 *
 * 编译（在 firmware/t113 目录）：
 *   gcc -std=c99 -D_GNU_SOURCE -DHMI_UNIT_TEST \
 *       -Iapp/runtime -Iapp/comm -Iconfig -Igenerated \
 *       tests/hmi_comm_test.c app/comm/comm_core.c app/comm/modbus_rtu.c \
 *       app/comm/modbus_tcp.c app/comm/mqtt_client.c -lpthread -o /tmp/hmi_comm_test
 */
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <stdint.h>

#include "modbus_rtu.h"
#include "modbus_tcp.h"
#include "comm_core.h"
#include "mqtt_client.h"
#include "mqtt_contract.h"
#include "mqtt_tls_contract.h"
#include "tls_proxy_config.h"
#include "mqtt_cmd_contract.h"
#include "mqtt_acl_contract.h"
#include "mqtt_cmd_ledger.h"
#include "mqtt_write_guard.h"
#include "mqtt_cmd_dispatch.h"
#include "mqtt_cmd_wiring.h"      /* 远程写命令的宿主接线（阶段 289）：订阅/元数据/权限 → hmi_cmd_handle */
#include "hmi_actions.h"          /* 审计/角色/权限 API —— 本步用假实现注入（见文件尾），故只取声明 */
#include "generated_security.h"   /* 权限位唯一来源（与设备 ACL 模块同源） */

/* mqtt_client.c 的报文构建器（HMI_UNIT_TEST 下暴露，见文件尾） */
extern int mqtt_test_build_connect(uint8_t* out, size_t cap);
extern int mqtt_test_build_subscribe(uint16_t pid, const char* topic, uint8_t* out, size_t cap);
extern int mqtt_test_build_publish(const char* topic, const char* payload, int len,
                                   int retain, uint8_t* out, size_t cap);
/* 当前已注册订阅数（阶段 290：验证 mqtt_start 不再清空订阅表） */
extern int mqtt_test_sub_count(void);

/* modbus_rtu.c 的写回显校验器（HMI_UNIT_TEST 下暴露，见文件尾） */
extern int mb_rtu_test_check_write_echo(const uint8_t* frame, size_t len, uint8_t expect_unit,
                                        uint8_t expect_fn, const uint8_t* echo, size_t echo_len,
                                        uint8_t* excode);

/* comm_core.c 运行时连接配置符号（HMI_UNIT_TEST 下去 static 后链接可见） */
extern void comm_config_load(void);
extern char s_cfg_rtu_dev[64];
extern int  s_cfg_rtu_baud;
extern char s_cfg_rtu_parity;
extern int  s_cfg_rtu_tmo;
extern char s_cfg_tcp_host[64];
extern int  s_cfg_tcp_port;
extern int  s_cfg_tcp_tmo;
extern char s_cfg_mqtt_host[64];
extern int  s_cfg_mqtt_port;
extern char s_cfg_mqtt_client[64];
extern char s_cfg_mqtt_user[64];
extern char s_cfg_mqtt_pass[64];
extern int  s_cfg_mqtt_keep;
/* TLS 配置与决定（阶段 300；HMI_UNIT_TEST 下去 static 后链接可见） */
extern char s_cfg_tls_mode[16];
extern char s_cfg_tls_local_port[8];
extern char s_cfg_tls_psk_id[64];
extern char s_cfg_tls_psk_file[128];
extern char s_cfg_tls_ca_path[128];
extern int  comm_tls_effective(const char* broker_host, unsigned broker_port,
                               char* out_host, size_t out_len, unsigned* out_port,
                               const char** out_reason);
extern int  comm_tls_refuse_notice(const char* reason);

/* 变量表实现（HMI_UNIT_TEST 暴露内部符号供断言） */
#include "variable_table.c"
#include "../generated/tags.c"

static int g_pass = 0, g_fail = 0;

#define CHECK(cond, msg) do { \
    if (cond) { printf("  PASS  %s\n", msg); g_pass++; } \
    else      { printf("  FAIL  %s (line %d)\n", msg, __LINE__); g_fail++; } \
} while (0)

/* 参考 CRC16（逐位法） */
static uint16_t ref_crc16(const uint8_t* d, size_t n)
{
    uint16_t crc = 0xFFFF;
    for (size_t i = 0; i < n; i++) {
        crc ^= d[i];
        for (int b = 0; b < 8; b++)
            crc = (crc & 1) ? (uint16_t)((crc >> 1) ^ 0xA001) : (uint16_t)(crc >> 1);
    }
    return crc;
}

static void test_crc16(void)
{
    printf("[Modbus RTU CRC16]\n");
    /* 标准向量（由独立 Python 实现计算） */
    const uint8_t v1[] = {0x01, 0x03, 0x00, 0x00, 0x00, 0x01};
    const uint8_t v2[] = {0x01, 0x04, 0x00, 0x01, 0x00, 0x02};
    const uint8_t v3[] = {0x01, 0x03, 0x00, 0x00, 0x00, 0x0A};
    CHECK(mb_rtu_crc16(v1, sizeof(v1)) == 0x0A84, "向量1: 010300000001 -> 0x0A84");
    CHECK(mb_rtu_crc16(v2, sizeof(v2)) == 0x0B20, "向量2: 010400010002 -> 0x0B20");
    CHECK(mb_rtu_crc16(v3, sizeof(v3)) == 0xCDC5, "向量3: 01030000000A -> 0xCDC5");

    /* 随机样本与参考实现交叉验证 */
    uint8_t buf[32];
    srand(42);
    int ok = 1;
    for (int t = 0; t < 200; t++) {
        size_t n = (size_t)(rand() % 32);
        for (size_t i = 0; i < n; i++) buf[i] = (uint8_t)rand();
        if (mb_rtu_crc16(buf, n) != ref_crc16(buf, n)) { ok = 0; break; }
    }
    CHECK(ok, "200 组随机样本与参考实现一致");
}

static void test_rtu_frame(void)
{
    printf("[Modbus RTU 帧]\n");
    uint8_t frame[64];
    const uint8_t pdu[4] = {0x00, 0x00, 0x00, 0x01};
    int flen = mb_rtu_build_frame(1, 0x03, pdu, 4, frame, sizeof(frame));
    CHECK(flen == 8, "读保持寄存器请求帧长 8 (unit+fn+4pdu+2crc)");
    const uint8_t expect[9] = {0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x84, 0x0A};
    CHECK(memcmp(frame, expect, 6) == 0, "帧体(不含CRC)正确");
    CHECK(frame[6] == 0x84 && frame[7] == 0x0A, "CRC 低字节在前（84 0A）");

    /* 有效响应：01 03 02 12 34 CRC(0103021234) */
    uint8_t resp[9] = {0x01, 0x03, 0x02, 0x12, 0x34, 0, 0, 0, 0};
    uint16_t crc = ref_crc16(resp, 5);
    resp[5] = (uint8_t)(crc & 0xFF);
    resp[6] = (uint8_t)(crc >> 8);
    uint8_t pdu_out[8];
    size_t plen = sizeof(pdu_out);
    CHECK(mb_rtu_parse_frame(resp, 7, 1, 0x03, pdu_out, &plen, NULL) == 0, "有效响应解析成功");
    CHECK(plen == 2 && pdu_out[0] == 0x12 && pdu_out[1] == 0x34, "数据载荷正确 (0x1234)");

    /* CRC 损坏 */
    resp[5] ^= 0xFF;
    CHECK(mb_rtu_parse_frame(resp, 7, 1, 0x03, pdu_out, &plen, NULL) != 0, "CRC 损坏被拒绝");
    resp[5] ^= 0xFF;

    /* 从站号不匹配 */
    CHECK(mb_rtu_parse_frame(resp, 7, 2, 0x03, pdu_out, &plen, NULL) != 0, "从站号不匹配被拒绝");

    /* 异常响应：01 83 02 CRC(01038302) */
    uint8_t exc[5] = {0x01, 0x83, 0x02, 0, 0};
    uint16_t ecrc = ref_crc16(exc, 3);
    exc[3] = (uint8_t)(ecrc & 0xFF);
    exc[4] = (uint8_t)(ecrc >> 8);
    uint8_t excode = 0;
    CHECK(mb_rtu_parse_frame(exc, 5, 1, 0x03, pdu_out, &plen, &excode) == -2, "异常响应返回 -2");
    CHECK(excode == 0x02, "异常码 0x02 (ILLEGAL DATA ADDRESS)");

    /* 读响应字节数校验（阶段 121）：解析器只按响应**自称**的 bc 校验 CRC，
       所以"bc 偏小但 CRC 合法"的帧能过解析；调用方必须再核一次，否则按 count 索引
       会读到未初始化/陈旧字节（垃圾值进变量缓存）。 */
    uint8_t short_resp[7] = {0x01, 0x03, 0x02, 0x12, 0x34, 0, 0};   /* 声称 2 字节 */
    uint16_t scrc = ref_crc16(short_resp, 5);
    short_resp[5] = (uint8_t)(scrc & 0xFF);
    short_resp[6] = (uint8_t)(scrc >> 8);
    size_t slen = sizeof(pdu_out);
    CHECK(mb_rtu_parse_frame(short_resp, 7, 1, 0x03, pdu_out, &slen, NULL) == 0,
          "bc 偏小的响应能过解析器（它只按自称的 bc 校验 CRC）");
    CHECK(mb_rtu_check_read_pdu(slen, 4) != 0,
          "字节数校验拒绝 bc=2 而期望 4（旧实现会读到未初始化字节）");
    CHECK(mb_rtu_check_read_pdu(slen, slen) == 0, "字节数相符时接受");
}

/* Modbus RTU **写回显**校验
 *
 * 背景（阶段 120）：写操作（fn5/fn6/fn16）的响应被拿去给 `mb_rtu_parse_frame()` ——
 * 那是**读响应**解析器（`frame[2]` 是字节数、CRC 在 `3+bc`），而写响应里 `frame[2]`
 * 是地址高位 ⇒ 合法回显必然被判失败，且调用方忽略返回值 ⇒ **RTU 写全部静默失败**。
 * 判据的核心是「两种形状必须被区别对待」，这一点不需要串口就能钉死。
 */
static void test_rtu_write_echo(void)
{
    printf("[Modbus RTU 写回显]\n");

    /* fn6 写单寄存器回显：unit=1 fn=6 addr=0x0002 val=0x1234 */
    uint8_t w6[8] = {0x01, 0x06, 0x00, 0x02, 0x12, 0x34, 0, 0};
    uint16_t crc = ref_crc16(w6, 6);
    w6[6] = (uint8_t)(crc & 0xFF);
    w6[7] = (uint8_t)(crc >> 8);
    const uint8_t echo6[4] = {0x00, 0x02, 0x12, 0x34};
    uint8_t excode = 0;

    /* 🩸 根因本身是一条断言：读解析器**必须继续拒绝**写回显（两者形状不同）。
       旧实现拿它校验写响应 ⇒ 每一次写都被误判 —— 这条把"为什么必须换解析器"钉死。 */
    uint8_t pdu_out[8]; size_t plen = sizeof(pdu_out);
    CHECK(mb_rtu_parse_frame(w6, sizeof(w6), 1, 0x06, pdu_out, &plen, NULL) != 0,
          "读解析器拒绝写回显（旧 bug 根因：形状不同）");
    CHECK(mb_rtu_test_check_write_echo(w6, sizeof(w6), 1, 0x06, echo6, 4, &excode) == 0,
          "合法 fn6 回显被接受");

    const uint8_t bad6[4] = {0x00, 0x03, 0x12, 0x34};
    CHECK(mb_rtu_test_check_write_echo(w6, sizeof(w6), 1, 0x06, bad6, 4, &excode) == -1,
          "地址回显不符被拒绝");
    CHECK(mb_rtu_test_check_write_echo(w6, sizeof(w6), 2, 0x06, echo6, 4, &excode) == -1,
          "从站号不符被拒绝");
    CHECK(mb_rtu_test_check_write_echo(w6, sizeof(w6), 1, 0x05, echo6, 4, &excode) == -1,
          "功能码不符被拒绝");
    w6[6] ^= 0xFF;
    CHECK(mb_rtu_test_check_write_echo(w6, sizeof(w6), 1, 0x06, echo6, 4, &excode) == -1,
          "CRC 损坏被拒绝");
    w6[6] ^= 0xFF;
    CHECK(mb_rtu_test_check_write_echo(w6, 4, 1, 0x06, echo6, 4, &excode) == -1,
          "短帧被拒绝（不越界读）");

    /* fn5 写线圈回显（值 0xFF00） */
    uint8_t w5[8] = {0x01, 0x05, 0x00, 0x01, 0xFF, 0x00, 0, 0};
    crc = ref_crc16(w5, 6);
    w5[6] = (uint8_t)(crc & 0xFF);
    w5[7] = (uint8_t)(crc >> 8);
    const uint8_t echo5[4] = {0x00, 0x01, 0xFF, 0x00};
    CHECK(mb_rtu_test_check_write_echo(w5, sizeof(w5), 1, 0x05, echo5, 4, &excode) == 0,
          "合法 fn5 回显被接受");

    /* fn16 多寄存器写回显：addr=0x0000 count=0x0002 */
    uint8_t w16[8] = {0x01, 0x10, 0x00, 0x00, 0x00, 0x02, 0, 0};
    crc = ref_crc16(w16, 6);
    w16[6] = (uint8_t)(crc & 0xFF);
    w16[7] = (uint8_t)(crc >> 8);
    const uint8_t echo16[4] = {0x00, 0x00, 0x00, 0x02};
    CHECK(mb_rtu_test_check_write_echo(w16, sizeof(w16), 1, 0x10, echo16, 4, &excode) == 0,
          "合法 fn16 回显被接受");
    const uint8_t bad16[4] = {0x00, 0x00, 0x00, 0x03};
    CHECK(mb_rtu_test_check_write_echo(w16, sizeof(w16), 1, 0x10, bad16, 4, &excode) == -1,
          "fn16 数量回显不符被拒绝");

    /* 异常响应：01 86 02 + CRC ⇒ -2 且异常码出参正确 */
    uint8_t exc[5] = {0x01, 0x86, 0x02, 0, 0};
    crc = ref_crc16(exc, 3);
    exc[3] = (uint8_t)(crc & 0xFF);
    exc[4] = (uint8_t)(crc >> 8);
    excode = 0;
    CHECK(mb_rtu_test_check_write_echo(exc, sizeof(exc), 1, 0x06, echo6, 4, &excode) == -2,
          "写异常响应返回 -2");
    CHECK(excode == 0x02, "写异常码出参 = 0x02");
    exc[3] ^= 0xFF;
    CHECK(mb_rtu_test_check_write_echo(exc, sizeof(exc), 1, 0x06, echo6, 4, &excode) == -1,
          "写异常响应 CRC 损坏被拒绝");
}

static void test_tcp_mbap(void)
{
    printf("[Modbus TCP MBAP]\n");
    uint8_t frame[32];
    const uint8_t pdu[4] = {0x00, 0x00, 0x00, 0x01};
    size_t n = mb_tcp_build_mbap(0x1234, 1, 0x03, pdu, 4, frame, sizeof(frame));
    CHECK(n == 12, "帧总长 12 (6 头 + unit + fn + 4)");
    CHECK(frame[0] == 0x12 && frame[1] == 0x34, "TID 大端序");
    CHECK(frame[2] == 0 && frame[3] == 0, "ProtocolId=0");
    CHECK(frame[4] == 0 && frame[5] == 6, "Length = 6 (unit+fn+4)");
    CHECK(frame[6] == 1 && frame[7] == 3, "unit/fn 位置正确");
    CHECK(memcmp(&frame[8], pdu, 4) == 0, "PDU 位置正确");

    /* 有效响应：echo tid + pid0 + len5 + unit1 + fn3 + bc2 + 1234 */
    uint8_t resp[11];
    memcpy(resp, frame, 6);
    resp[4] = 0; resp[5] = 5;
    resp[6] = 1; resp[7] = 0x03;
    resp[8] = 2; resp[9] = 0x12; resp[10] = 0x34;
    uint8_t pdu_out[8];
    size_t plen = sizeof(pdu_out);
    CHECK(mb_tcp_parse_mbap(resp, sizeof(resp), 0x1234, 1, pdu_out, &plen, NULL) == 0, "有效响应解析成功");
    CHECK(plen == 3 && pdu_out[0] == 2, "载荷 = bc+data (2, 0x12, 0x34)");

    /* TID 不匹配（帧交错防护） */
    CHECK(mb_tcp_parse_mbap(resp, sizeof(resp), 0x5678, 1, pdu_out, &plen, NULL) != 0, "TID 不匹配被拒绝");

    /* ProtocolId 非 0 */
    uint8_t bad[11];
    memcpy(bad, resp, sizeof(resp));
    bad[2] = 0x00; bad[3] = 0x01;
    CHECK(mb_tcp_parse_mbap(bad, sizeof(bad), 0x1234, 1, pdu_out, &plen, NULL) != 0, "ProtocolId!=0 被拒绝");

    /* 异常响应 */
    uint8_t exc[9] = {0x12, 0x34, 0, 0, 0, 3, 1, 0x83, 0x02};
    uint8_t excode = 0;
    CHECK(mb_tcp_parse_mbap(exc, sizeof(exc), 0x1234, 1, pdu_out, &plen, &excode) == -2, "异常响应返回 -2");
    CHECK(excode == 0x02, "异常码 0x02");

    /* 截断帧 */
    CHECK(mb_tcp_parse_mbap(resp, 7, 0x1234, 1, pdu_out, &plen, NULL) != 0, "截断帧被拒绝");
}

static void test_addr_parse(void)
{
    printf("[地址解析]\n");
    uint8_t fn; uint16_t off;
    CHECK(hmi_tag_parse_addr("40001", &fn, &off) == 0 && fn == 0x03 && off == 0, "40001 -> fn3/off0");
    CHECK(hmi_tag_parse_addr("00001", &fn, &off) == 0 && fn == 0x01 && off == 0, "00001 -> fn1/off0");
    CHECK(hmi_tag_parse_addr("30001", &fn, &off) == 0 && fn == 0x04 && off == 0, "30001 -> fn4/off0");
    CHECK(hmi_tag_parse_addr("40010", &fn, &off) == 0 && fn == 0x03 && off == 9, "40010 -> fn3/off9");
    CHECK(hmi_tag_parse_addr("h:2", &fn, &off) == 0 && fn == 0x03 && off == 2, "h:2 -> fn3/off2");
    CHECK(hmi_tag_parse_addr("c:7", &fn, &off) == 0 && fn == 0x01 && off == 7, "c:7 -> fn1/off7");
    CHECK(hmi_tag_parse_addr("3:5", &fn, &off) == 0 && fn == 0x03 && off == 5, "3:5 -> fn3/off5");
    CHECK(hmi_tag_parse_addr("", &fn, &off) != 0, "空串解析失败");
    CHECK(hmi_tag_parse_addr("abc", &fn, &off) != 0, "非法串解析失败");
}

static void test_var_table(void)
{
    printf("[变量表/轮询组]\n");
    hmi_var_init();

    /* 预期组：modbus-tcp 保持寄存器 40001..40003 合并为一组(off0 count3)；
       modbus-rtu 线圈 00001 一组(off0 count1)；TEMP 为 mqtt 不建组 */
    CHECK(s_group_count == 2, "轮询组数 = 2");
    int g_tcp = -1, g_rtu = -1;
    for (int i = 0; i < s_group_count; i++) {
        if (s_groups[i].drv == HMI_DRV_MODBUS_TCP) g_tcp = i;
        if (s_groups[i].drv == HMI_DRV_MODBUS_RTU) g_rtu = i;
    }
    CHECK(g_tcp >= 0 && g_rtu >= 0, "存在 TCP 与 RTU 组");
    CHECK(s_groups[g_tcp].fn == 0x03 && s_groups[g_tcp].off == 0 && s_groups[g_tcp].count == 3,
          "TCP 组: 保持寄存器 off0 连续 3 个 (PV/SV/ALARM 块合并)");
    CHECK(s_groups[g_rtu].fn == 0x01 && s_groups[g_rtu].off == 0 && s_groups[g_rtu].count == 1,
          "RTU 组: 线圈 off0 1 个 (RUN)");

    /* 值转换 */
    uint16_t r[2];
    CHECK(float_to_regs("U16", 123.0f, r, 2) == 1 && r[0] == 123, "U16 转换");
    CHECK(float_to_regs("I16", -5.0f, r, 2) == 1 && (int16_t)r[0] == -5, "I16 转换");
    float f = 1.0f;
    CHECK(float_to_regs("Float", f, r, 2) == 2 && r[0] == 0x3F80 && r[1] == 0x0000,
          "Float 1.0 -> 3F80 0000");
    CHECK(regs_to_float("Float", r, 2) == 1.0f, "Float 回读 1.0");
    uint16_t u32r[2] = {0x0001, 0x0002};
    CHECK(regs_to_float("U32", u32r, 2) == (float)0x10002, "U32 合并");
    /* 🩸 阶段 121：U32 曾走有符号 `(r[0] << 16) | r[1]` ⇒ 高位置 1 时读数变负。
       下面两档是"修复前必红"的见证（不是补充覆盖率）：
       0x80000000 会读成 -2147483648、0xFFFFFFFF 会读成 -1。 */
    uint16_t u32_hi[2] = {0x8000, 0x0000};
    CHECK(regs_to_float("U32", u32_hi, 2) == 2147483648.0f, "U32 高位置 1 仍为正（0x80000000）");
    uint16_t u32_max[2] = {0xFFFF, 0xFFFF};
    CHECK(regs_to_float("U32", u32_max, 2) == 4294967295.0f, "U32 最大值（0xFFFFFFFF）");
    CHECK(regs_to_float("I32", u32_max, 2) == -1.0f, "I32 同字节的有符号解释 = -1");
    /* 写方向：越界/负值先钳再转（直接 (uint32_t)负数 是 UB） */
    CHECK(float_to_regs("U32", -1.0f, r, 2) == 2 && r[0] == 0 && r[1] == 0,
          "U32 写入负值钳到 0（不做 UB 转换）");
    CHECK(float_to_regs("U32", 4294967295.0f, r, 2) == 2 && r[0] == 0xFFFF && r[1] == 0xFFFF,
          "U32 写入最大值");
    CHECK(float_to_regs("I32", -1.0f, r, 2) == 2 && r[0] == 0xFFFF && r[1] == 0xFFFF,
          "I32 写入 -1");
    CHECK(regs_to_float("Bool", (uint16_t[]){1}, 1) == 1.0f, "Bool 转换");
    CHECK(regs_to_float("Bool", (uint16_t[]){0}, 1) == 0.0f, "Bool 0 转换");

    /* 写路径：投递后消费（未连接时安全降级，队列应清空） */
    var_raw_write(TAG_SV, 200.0f);
    CHECK(s_wq_head != s_wq_tail, "写请求已入队");
    hmi_var_drain_writes();
    CHECK(s_wq_head == s_wq_tail, "写请求队列已消费（未连接时降级不崩溃）");
    CHECK(s_cache[TAG_SV] == 200.0f, "缓存已即时更新");

    /* 只读保护：ALARM 只读，写不投递 */
    int before = s_wq_head;
    var_raw_write(TAG_ALARM, 1.0f);
    CHECK(s_wq_head == before, "只读标签写被拒绝");

    /* ── 子设备（从站）绑定回归（2026-08-18） ──
     * IDE 生成约束：tag_binding_t.addr 必须是纯 Modbus 地址（"40001"），
     * 从站号必须走 binding.unit —— "站号:地址"（如 "1:40001"）会被
     * hmi_tag_parse_addr 的 "%d:%d" 分支解析成 fn=站号(功能码)/off=完整地址(偏移)，
     * 轮询组请求错误导致 Modbus 无读写数据（曾为实机故障根因）。 */
    static const tag_meta_t s_meta3[] = {
        /* 尾三字段 = has_range/min/max（远程写量程）；这些用例判的是轮询组与地址解析，
         * 量程一律 0 = 不限（量程本身的判据在 test_mqtt_cmd_wiring）。 */
        { "PV",  "1:40001", 0, 1.0f, 0.0f, 0, 0.0f, 0.0f },
        { "RUN", "2:00001", 0, 1.0f, 0.0f, 0, 0.0f, 0.0f },
        { "TEMP","3:30001", 1, 0.1f, 0.0f, 0, 0.0f, 0.0f },
    };
    static const tag_binding_t s_bind3[] = {
        { "modbus-tcp", "40001", 3, "U16",   100 },
        { "modbus-rtu", "00001", 2, "Bool",  100 },
        { "modbus-tcp", "30001", 3, "U16",   100 },
    };
    hmi_tags_adopt(s_meta3, s_bind3, 3);
    var_groups_rebuild();
    CHECK(s_group_count == 3, "子设备: 3 个绑定 → 3 个轮询组（TCP保持/TCP输入/RTU线圈）");
    int g_tcp_hold = -1, g_tcp_in = -1, g_rtu2 = -1;
    for (int i = 0; i < s_group_count; i++) {
        poll_group_t* pg = &s_groups[i];
        if (pg->drv == HMI_DRV_MODBUS_TCP && pg->fn == 0x03 && pg->unit == 3) g_tcp_hold = i;
        if (pg->drv == HMI_DRV_MODBUS_TCP && pg->fn == 0x04 && pg->unit == 3) g_tcp_in = i;
        if (pg->drv == HMI_DRV_MODBUS_RTU && pg->fn == 0x01 && pg->unit == 2) g_rtu2 = i;
    }
    CHECK(g_tcp_hold >= 0 && s_groups[g_tcp_hold].off == 0, "TCP 组: unit=3 保持寄存器 40001 → off0");
    CHECK(g_tcp_in >= 0 && s_groups[g_tcp_in].off == 0, "TCP 组: unit=3 输入寄存器 30001 → off0");
    CHECK(g_rtu2 >= 0 && s_groups[g_rtu2].off == 0, "RTU 组: unit=2 线圈 00001 → off0");

    /* 防御断言：旧格式 "站号:地址" 绝不应再被生成（解析语义为 功能码:偏移） */
    uint8_t bad_fn = 0; uint16_t bad_off = 0;
    CHECK(hmi_tag_parse_addr("1:40001", &bad_fn, &bad_off) == 0 &&
          bad_fn == 0x01 && bad_off == 40001,
          "防御: 旧格式 \"1:40001\" 会解析为 fn=01/off=40001（IDE 禁止再生成此格式）");

    /* ── 乱序地址块合并回归（2026-08-18 实机定位） ──
     * group_add 旧实现用 g->count = need 直接覆盖：标签地址乱序（如 off2 先于 off1）
     * 时 need < count 回退 count，下次扩展的清空循环从回退处开始抹掉已填槽位 →
     * 部分变量丢失不轮询。修复：need ≤ count 时不得回退/清槽。 */
    static const tag_meta_t s_meta_oo[] = {
        { "A","1:10001",0,1,0,0,0.0f,0.0f },{ "B","1:10003",0,1,0,0,0.0f,0.0f },{ "C","1:10002",0,1,0,0,0.0f,0.0f },
        { "D","1:10004",0,1,0,0,0.0f,0.0f },{ "E","1:10005",0,1,0,0,0.0f,0.0f },{ "F","1:10006",0,1,0,0,0.0f,0.0f },
        { "G","1:10008",0,1,0,0,0.0f,0.0f },{ "H","1:10007",0,1,0,0,0.0f,0.0f },
    };
    static const tag_binding_t s_bind_oo[] = {
        { "modbus-rtu","10001",1,"Bool",100 },{ "modbus-rtu","10003",1,"Bool",100 },
        { "modbus-rtu","10002",1,"Bool",100 },{ "modbus-rtu","10004",1,"Bool",100 },
        { "modbus-rtu","10005",1,"Bool",100 },{ "modbus-rtu","10006",1,"Bool",100 },
        { "modbus-rtu","10008",1,"Bool",100 },{ "modbus-rtu","10007",1,"Bool",100 },
    };
    hmi_tags_adopt(s_meta_oo, s_bind_oo, 8);
    var_groups_rebuild();
    CHECK(s_group_count == 1 && s_groups[0].count == 8,
          "乱序: 8 个离散输入(10001..10008) 合并为单组 count=8");
    CHECK(s_groups[0].fn == 0x02 && s_groups[0].off == 0 && s_groups[0].unit == 1,
          "乱序: fn=02(离散输入) off=0 unit=1");
    {
        int filled = 0;
        for (int j = 0; j < 8; j++) if (s_groups[0].idx[j] != HMI_SLOT_EMPTY) filled++;
        CHECK(filled == 8, "乱序: 8 个槽位全部填充（无变量丢失）");
    }

    hmi_var_init();   /* 恢复出厂表，避免影响后续测试 */
}

/* 运行时连接配置（comm_config.ini）解析测试 */
static void test_comm_config(void)
{
    printf("[运行时连接配置]\n");
#define RESET_CFG() do { \
    memset(s_cfg_rtu_dev, 0, sizeof(s_cfg_rtu_dev)); s_cfg_rtu_baud = 0; \
    s_cfg_rtu_parity = 0; s_cfg_rtu_tmo = 0; \
    memset(s_cfg_tcp_host, 0, sizeof(s_cfg_tcp_host)); s_cfg_tcp_port = 0; s_cfg_tcp_tmo = 0; \
    memset(s_cfg_mqtt_host, 0, sizeof(s_cfg_mqtt_host)); s_cfg_mqtt_port = 0; \
    memset(s_cfg_mqtt_client, 0, sizeof(s_cfg_mqtt_client)); \
    memset(s_cfg_mqtt_user, 0, sizeof(s_cfg_mqtt_user)); \
    memset(s_cfg_mqtt_pass, 0, sizeof(s_cfg_mqtt_pass)); s_cfg_mqtt_keep = 0; \
} while (0)

    /* 文件不存在 → 全部保持默认（不崩溃） */
    remove("/tmp/hmi_comm_test.ini");
    RESET_CFG();
    comm_config_load();
    CHECK(s_cfg_rtu_dev[0] == 0 && s_cfg_tcp_host[0] == 0, "无配置文件: 保持编译期默认");

    /* 写入配置并解析 */
    FILE* f = fopen("/tmp/hmi_comm_test.ini", "w");
    CHECK(f != NULL, "创建测试配置文件");
    if (f) {
        fprintf(f, "# zhishu-HMI runtime communication config\n");
        fprintf(f, "rtu.dev=/dev/ttyS3\nrtu.baud=19200\nrtu.parity=o\nrtu.tmo_ms=500\n");
        fprintf(f, "tcp.host=192.168.100.50\ntcp.port=1502\n");
        fprintf(f, "mqtt.host=10.0.0.9\nmqtt.keepalive=15\n");
        fclose(f);
    }
    RESET_CFG();
    comm_config_load();
    CHECK(strcmp(s_cfg_rtu_dev, "/dev/ttyS3") == 0, "rtu.dev 解析");
    CHECK(s_cfg_rtu_baud == 19200, "rtu.baud 解析");
    CHECK(s_cfg_rtu_parity == 'O', "rtu.parity 小写归一化为大写");
    CHECK(s_cfg_rtu_tmo == 500, "rtu.tmo_ms 解析");
    CHECK(strcmp(s_cfg_tcp_host, "192.168.100.50") == 0, "tcp.host 解析");
    CHECK(s_cfg_tcp_port == 1502, "tcp.port 解析");
    CHECK(strcmp(s_cfg_mqtt_host, "10.0.0.9") == 0, "mqtt.host 解析");
    CHECK(s_cfg_mqtt_keep == 15, "mqtt.keepalive 解析");
    CHECK(s_cfg_mqtt_user[0] == 0 && s_cfg_mqtt_client[0] == 0, "未写键保持默认");

    /* 阶段 300：TLS 五键解析（值含空白 ⇒ 裁剪） */
    f = fopen("/tmp/hmi_comm_test.ini", "w");
    if (f) {
        fprintf(f, "mqtt.tls=psk\nmqtt.tls.local_port=11883\n");
        fprintf(f, "mqtt.tls.psk_id = hmi-01  \n");
        fprintf(f, "mqtt.tls.psk_file=/mnt/UDISK/hmi/psk.key\n");
        fprintf(f, "mqtt.tls.ca_path=/mnt/UDISK/hmi/ca.pem\n");
        fclose(f);
    }
    s_cfg_tls_mode[0] = s_cfg_tls_local_port[0] = s_cfg_tls_psk_id[0] = '\0';
    s_cfg_tls_psk_file[0] = s_cfg_tls_ca_path[0] = '\0';
    comm_config_load();
    CHECK(strcmp(s_cfg_tls_mode, "psk") == 0, "mqtt.tls 解析");
    CHECK(strcmp(s_cfg_tls_local_port, "11883") == 0, "mqtt.tls.local_port 解析");
    CHECK(strcmp(s_cfg_tls_psk_id, "hmi-01") == 0, "mqtt.tls.psk_id 解析（空白裁剪）");
    CHECK(strcmp(s_cfg_tls_psk_file, "/mnt/UDISK/hmi/psk.key") == 0, "mqtt.tls.psk_file 解析");
    CHECK(strcmp(s_cfg_tls_ca_path, "/mnt/UDISK/hmi/ca.pem") == 0, "mqtt.tls.ca_path 解析");

    /* 值含首尾空白 → 裁剪 */
    f = fopen("/tmp/hmi_comm_test.ini", "w");
    if (f) {
        fprintf(f, "tcp.host = 10.1.2.3  \n");
        fclose(f);
    }
    RESET_CFG();
    comm_config_load();
    CHECK(strcmp(s_cfg_tcp_host, "10.1.2.3") == 0, "值首尾空白裁剪");
    remove("/tmp/hmi_comm_test.ini");
#undef RESET_CFG
}

/* MQTT 报文构建：长主题 / 长凭据不得越界，容量不足必须拒绝
 *
 * 背景（阶段 120）：`build_subscribe` 曾用 `uint8_t vh[8]` 装上限 128 字节的主题，
 * 默认主题 "hmi/t113/set/TEMP" 每次订阅越界 ≥9 字节；`build_connect` 的
 * `payload[192]` 在 client_id/user/pass 各 63 字符时差 3 字节。两者都是**栈**越界。
 * 判据不是"跑完没报错"，而是：① 字节序列正确；② `out` 前后哨兵一个都不许动。
 */
static size_t mqtt_varint_len(size_t v)
{
    size_t n = 1;
    while (v >= 128) { v >>= 7; n++; }
    return n;
}

static int mqtt_sentinels_intact(const uint8_t* buf, size_t total, size_t off, int n)
{
    for (size_t i = 0; i < off; i++) if (buf[i] != 0xA5) return 0;
    if (n > 0)
        for (size_t i = off + (size_t)n; i < total; i++) if (buf[i] != 0xA5) return 0;
    return 1;
}

static void test_mqtt_packet_build(void)
{
    printf("[MQTT 报文构建]\n");

    uint8_t buf[400];
    const size_t off = 16;
    const char* topic = "hmi/t113/set/TEMP";
    size_t tlen = strlen(topic);

    /* ① SUBSCRIBE：默认主题（旧版就是在这里越界的） */
    memset(buf, 0xA5, sizeof(buf));
    int n = mqtt_test_build_subscribe(0x1234, topic, buf + off, sizeof(buf) - off);
    size_t rl = 2 + 2 + tlen + 1;
    CHECK(n == (int)(1 + mqtt_varint_len(rl) + rl), "subscribe: 报文总长");
    CHECK(buf[off] == 0x82, "subscribe: 固定头 0x82");
    CHECK(buf[off + 1] == (uint8_t)rl, "subscribe: 剩余长度（单字节）");
    CHECK(buf[off + 2] == 0x12 && buf[off + 3] == 0x34, "subscribe: packet id 大端");
    CHECK(buf[off + 4] == 0x00 && buf[off + 5] == (uint8_t)tlen, "subscribe: 主题长度前缀");
    CHECK(memcmp(buf + off + 6, topic, tlen) == 0, "subscribe: 主题完整写入");
    CHECK(buf[off + 6 + tlen] == 0x00, "subscribe: QoS0");
    CHECK(mqtt_sentinels_intact(buf, sizeof(buf), off, n), "subscribe: 哨兵未被动（无越界写）");

    /* ② SUBSCRIBE：127 字符主题（剩余长度跨两字节 varint） */
    char long_topic[128];
    memset(long_topic, 't', sizeof(long_topic) - 1);
    long_topic[0] = 'h';
    long_topic[sizeof(long_topic) - 1] = '\0';
    memset(buf, 0xA5, sizeof(buf));
    n = mqtt_test_build_subscribe(1, long_topic, buf + off, sizeof(buf) - off);
    rl = 2 + 2 + strlen(long_topic) + 1;
    CHECK(n == (int)(1 + mqtt_varint_len(rl) + rl), "subscribe: 127 字符主题总长（varint 2 字节）");
    CHECK(memcmp(buf + off + 1 + mqtt_varint_len(rl) + 4, long_topic, strlen(long_topic)) == 0,
          "subscribe: 长主题完整写入");
    CHECK(mqtt_sentinels_intact(buf, sizeof(buf), off, n), "subscribe: 长主题哨兵未被动");

    /* ③ SUBSCRIBE：容量不足 ⇒ -1，且**一个字节都不写** */
    memset(buf, 0xA5, sizeof(buf));
    CHECK(mqtt_test_build_subscribe(1, topic, buf + off, 4) == -1, "subscribe: cap 不足返回 -1");
    CHECK(mqtt_sentinels_intact(buf, sizeof(buf), off, -1), "subscribe: 拒绝时不写任何字节");

    /* ④ CONNECT：client_id/user/pass 各 63 字符（旧版 payload[192] 差 3 字节） */
    char cred[64];
    memset(cred, 'c', sizeof(cred) - 1);
    cred[sizeof(cred) - 1] = '\0';
    mqtt_start("127.0.0.1", 1883, cred, cred, cred, 30);
    /* ⚠ 前面几个用例调过 `hmi_var_init()`/`hmi_tags_adopt()` ⇒ 真实的订阅接线会把 LWT 设上
     *   （阶段 290），本节判的是"无遗嘱时的报文长度"，所以先显式清掉。 */
    mqtt_clear_will();
    memset(buf, 0xA5, sizeof(buf));
    n = mqtt_test_build_connect(buf + off, sizeof(buf) - off);
    rl = 10 + 3 * (2 + 63);
    CHECK(n == (int)(1 + mqtt_varint_len(rl) + rl), "connect: 三份 63 字符凭据总长");
    CHECK(buf[off] == 0x10, "connect: 固定头 0x10");
    CHECK(buf[off + 1 + mqtt_varint_len(rl)] == 0x00 &&
          buf[off + 2 + mqtt_varint_len(rl)] == 0x04 &&
          memcmp(buf + off + 3 + mqtt_varint_len(rl), "MQTT", 4) == 0, "connect: 协议名 MQTT");
    CHECK(mqtt_sentinels_intact(buf, sizeof(buf), off, n), "connect: 哨兵未被动（无越界写）");
    CHECK(mqtt_test_build_connect(buf, 5) == -1, "connect: cap 不足返回 -1");

    /* ⑤ PUBLISH：主题 + 负载 */
    memset(buf, 0xA5, sizeof(buf));
    n = mqtt_test_build_publish(topic, "42", 2, 0, buf + off, sizeof(buf) - off);
    rl = 2 + tlen + 2;
    CHECK(n == (int)(1 + mqtt_varint_len(rl) + rl), "publish: 报文总长");
    CHECK(buf[off] == 0x30, "publish: 固定头 QoS0/不保留");
    CHECK(memcmp(buf + off + 1 + mqtt_varint_len(rl) + 2 + tlen, "42", 2) == 0, "publish: 负载写入");
    CHECK(mqtt_sentinels_intact(buf, sizeof(buf), off, n), "publish: 哨兵未被动");
    CHECK(mqtt_test_build_publish(topic, "42", 2, 0, buf, 5) == -1, "publish: cap 不足返回 -1");

    mqtt_stop();
}

/* LWT（遗嘱）＋ 订阅表跨 `mqtt_start` 的存续（阶段 290，M2 的"状态上行"那一半）
 *
 * 为什么两件事一起判：设备启动顺序是 `hmi_var_init()`（注册 /set/ 与 /cmd/ 主题）**先于**
 * `hmi_comm_start()` → `mqtt_start()`。旧版 `mqtt_start` 里写着 `s_sub_count = 0`
 * ⇒ 注册被整表清掉；没有 .so 时（不会再触发一次标签采纳重注册）表现为
 * "设备连上了 broker 却一条远程命令都收不到"。
 */
static void test_mqtt_will_and_subscription_persistence(void)
{
    printf("[MQTT 遗嘱 + 订阅表存续]\n");

    uint8_t buf[700];
    const size_t off = 16;
    char id63[64];
    memset(id63, 'c', sizeof(id63) - 1);
    id63[sizeof(id63) - 1] = '\0';

    /* ① 不设遗嘱：will flag(bit2) 必须为 0 */
    mqtt_stop();
    mqtt_clear_will();
    mqtt_start("127.0.0.1", 1883, id63, NULL, NULL, 30);
    memset(buf, 0xA5, sizeof(buf));
    int n = mqtt_test_build_connect(buf + off, sizeof(buf) - off);
    size_t rl = 10 + (2 + 63);
    CHECK(n == (int)(1 + mqtt_varint_len(rl) + rl), "无遗嘱：CONNECT 总长不含 will 段");
    /* 布局：固定头(1) + 剩余长度 + 协议名(2+4) + 级别(1) ⇒ flags 在此之后 */
    size_t flags_off = off + 1 + mqtt_varint_len(rl) + 2 + 4 + 1;
    CHECK((buf[flags_off] & 0x04) == 0, "无遗嘱：will flag = 0");

    /* ② 设遗嘱：flags 的 will(bit2) + retain(bit5)，QoS=0；段顺序 = ClientId → Will Topic → Will Message */
    mqtt_stop();
    mqtt_start("127.0.0.1", 1883, id63, NULL, NULL, 30);
    CHECK(mqtt_set_will("hmi/t113/status", "on=0", 1) == 0, "设置遗嘱成功");
    const char* wt = "hmi/t113/status";
    const char* wm = "on=0";
    memset(buf, 0xA5, sizeof(buf));
    n = mqtt_test_build_connect(buf + off, sizeof(buf) - off);
    rl = 10 + (2 + 63) + (2 + strlen(wt)) + (2 + strlen(wm));
    CHECK(n == (int)(1 + mqtt_varint_len(rl) + rl), "有遗嘱：CONNECT 总长含两段");
    CHECK(buf[off] == 0x10, "有遗嘱：固定头 0x10");
    CHECK((buf[flags_off] & 0x04) != 0, "有遗嘱：will flag = 1");
    CHECK((buf[flags_off] & 0x20) != 0, "有遗嘱：will retain = 1");
    CHECK((buf[flags_off] & 0x18) == 0, "有遗嘱：will QoS = 0");
    size_t p = flags_off + 3 + 2 + 63;          /* keepalive(2) + client_id(2+63) */
    CHECK(buf[p] == 0x00 && buf[p + 1] == (uint8_t)strlen(wt), "遗嘱主题长度前缀");
    CHECK(memcmp(buf + p + 2, wt, strlen(wt)) == 0, "遗嘱主题完整写入");
    p = p + 2 + strlen(wt);
    CHECK(buf[p] == 0x00 && buf[p + 1] == (uint8_t)strlen(wm), "遗嘱负载长度前缀");
    CHECK(memcmp(buf + p + 2, wm, strlen(wm)) == 0, "遗嘱负载完整写入（on=0）");
    CHECK(mqtt_sentinels_intact(buf, sizeof(buf), off, n), "有遗嘱：哨兵未被动（无越界写）");

    /* 容量：遗嘱 ＋ 三份 63 字符凭据仍建得出（finish_connect 的 256 字节缓冲曾不够） */
    mqtt_start("127.0.0.1", 1883, id63, id63, id63, 30);
    CHECK(mqtt_set_will(wt, wm, 1) == 0, "重设遗嘱");
    memset(buf, 0xA5, sizeof(buf));
    CHECK(mqtt_test_build_connect(buf + off, sizeof(buf) - off) > 0,
          "遗嘱 + 三份 63 字符凭据：仍建得出");

    /* ③ 非法参数 ⇒ 拒绝（不许"半设"） */
    CHECK(mqtt_set_will("", "on=0", 1) == -1, "空遗嘱主题 ⇒ 拒绝");
    CHECK(mqtt_set_will(NULL, "on=0", 1) == -1, "空指针遗嘱主题 ⇒ 拒绝");
    mqtt_clear_will();
    mqtt_stop();
    mqtt_start("127.0.0.1", 1883, id63, NULL, NULL, 30);
    memset(buf, 0xA5, sizeof(buf));
    n = mqtt_test_build_connect(buf + off, sizeof(buf) - off);
    rl = 10 + (2 + 63);
    CHECK(n == (int)(1 + mqtt_varint_len(rl) + rl) && (buf[flags_off] & 0x04) == 0,
          "mqtt_clear_will 后回到无遗嘱形态");

    /* ④ 订阅表跨 mqtt_start 存续（真实启动顺序：先注册、后 start） */
    mqtt_stop();
    mqtt_clear_topics();
    CHECK(mqtt_register_topic("hmi/t113/set/TEMP", NULL) == 0, "注册 /set/ 主题");
    CHECK(mqtt_register_topic("hmi/t113/cmd/SV", NULL) == 0, "注册 /cmd/ 主题");
    CHECK(mqtt_test_sub_count() == 2, "注册后订阅数 = 2");
    mqtt_start("127.0.0.1", 1883, "hmi-test", NULL, NULL, 30);
    CHECK(mqtt_test_sub_count() == 2, "mqtt_start 不再清空订阅表（阶段 290 修）");
    mqtt_clear_topics();
    CHECK(mqtt_test_sub_count() == 0, "mqtt_clear_topics 仍是唯一的清空出口");

    mqtt_stop();
    mqtt_start("127.0.0.1", 1883, "hmi", NULL, NULL, 30);   /* 复位，避免影响后续用例 */
}

/* 标签表采纳的失败路径：分配失败时可见表必须**一字不变**
 *
 * 背景（阶段 120）：旧 `hmi_tags_adopt` 先写 g_meta/g_bind/g_count、再 calloc 新
 * s_cache/s_last_pub；分配失败直接 return -1 ⇒ g_count 已变大而 s_cache 仍是旧小块
 * ⇒ 之后按 g_count 索引是堆越界。判据：失败后 g_count/g_meta/g_bind **都不许动**，
 * 且重试必须能成功。用 `s_tags_adopt_fail_alloc` 注入失败（正常环境 calloc 不会失败）。
 */
static void test_tags_adopt_failure_keeps_table(void)
{
    printf("[标签表采纳失败路径]\n");

    static const tag_meta_t metas[8] = {
        { "T0", "30001", 1, 1.0f, 0.0f, 0, 0.0f, 0.0f }, { "T1", "30002", 1, 1.0f, 0.0f, 0, 0.0f, 0.0f },
        { "T2", "30003", 1, 1.0f, 0.0f, 0, 0.0f, 0.0f }, { "T3", "30004", 1, 1.0f, 0.0f, 0, 0.0f, 0.0f },
        { "T4", "30005", 1, 1.0f, 0.0f, 0, 0.0f, 0.0f }, { "T5", "30006", 1, 1.0f, 0.0f, 0, 0.0f, 0.0f },
        { "T6", "30007", 1, 1.0f, 0.0f, 0, 0.0f, 0.0f }, { "T7", "30008", 1, 1.0f, 0.0f, 0, 0.0f, 0.0f },
    };
    static const tag_binding_t binds[8] = {
        { "mqtt", "30001", 0, "Float", 100 }, { "mqtt", "30002", 0, "Float", 100 },
        { "mqtt", "30003", 0, "Float", 100 }, { "mqtt", "30004", 0, "Float", 100 },
        { "mqtt", "30005", 0, "Float", 100 }, { "mqtt", "30006", 0, "Float", 100 },
        { "mqtt", "30007", 0, "Float", 100 }, { "mqtt", "30008", 0, "Float", 100 },
    };

    /* 基线：采纳 3 条 */
    CHECK(hmi_tags_adopt(metas, binds, 3) == 0, "采纳 3 条标签成功");
    CHECK(hmi_tag_count() == 3, "基线计数 = 3");

    const tag_meta_t*    before_meta  = g_meta;
    const tag_binding_t* before_bind  = g_bind;
    int                  before_count = g_count;

    /* 注入分配失败，要采纳 8 条（count 变大）—— 失败后可见表必须完全不动 */
    s_tags_adopt_fail_alloc = 1;
    CHECK(hmi_tags_adopt(metas, binds, 8) == -1, "分配失败返回 -1");
    s_tags_adopt_fail_alloc = 0;
    CHECK(g_count == before_count, "失败后 g_count 未变（旧写法会先变成 8）");
    CHECK(g_meta == before_meta && g_bind == before_bind, "失败后 g_meta/g_bind 未切换");
    CHECK(hmi_tag_count() == 3, "失败后对外计数仍为 3");

    /* 失败之后必须还能成功采纳 */
    CHECK(hmi_tags_adopt(metas, binds, 8) == 0, "失败后重试成功");
    CHECK(hmi_tag_count() == 8, "重试后计数 = 8");

    hmi_var_init();   /* 恢复出厂表，避免影响后续测试 */
    CHECK(hmi_tag_count() == TAG_COUNT, "恢复出厂表");
}

/* ──────────────────────────────────────────────────────────────
 * 主题/payload 契约 v1（阶段 264；登记 §2-#12 的 M1）
 *
 * ⚠ 下面这张表是**跨语言同一份样例**：C# 侧 `MqttValueContractTests` 会解析本文件的
 * `k_contract_samples[]` 区块并逐行比对（漂移即红）。**改表必须两端一起改**，
 * 且格式要保持"一行一条、逗号分隔"（解析器只认这个形状）。
 * ────────────────────────────────────────────────────────────── */
typedef struct {
    const char* prefix;      /* 主题前缀（可含 `/`） */
    const char* name;        /* 变量名 */
    double      value;       /* 值 */
    unsigned long long t_ms; /* 时间戳 */
    int         tb_utc;      /* 0=boot, 1=utc */
    int         quality;     /* 0/1 */
    const char* topic;       /* 期望主题 */
    const char* payload;     /* 期望 payload */
} contract_sample_t;

/* clang-format off */
static const contract_sample_t k_contract_samples[] = {
    /* prefix, name,       value,      t_ms,         tb_utc, q, topic,              payload */
    {  "hmi",   "temp",     12.5,       1000ULL,      0,      1, "hmi/val/temp",     "v=12.5;t=1000;tb=boot;q=1" },
    {  "hmi",   "pressure", -3.25,      250ULL,       1,      0, "hmi/val/pressure", "v=-3.25;t=250;tb=utc;q=0" },
    {  "hmi",   "count",    1000000.0,  1750000000ULL,1,     1, "hmi/val/count",    "v=1e+06;t=1750000000;tb=utc;q=1" },
    {  "hmi",   "ratio",    0.125,      0ULL,         0,      1, "hmi/val/ratio",    "v=0.125;t=0;tb=boot;q=1" },
    {  "a/b",   "nested",   42.0,       7ULL,         0,      1, "a/b/val/nested",   "v=42;t=7;tb=boot;q=1" },
};
/* clang-format on */

/* ──────────────────────────────────────────────────────────────
 * M2：报警/状态上行样例表（阶段 272；登记 §2-#12 的 M2）
 *
 * ⚠ 与 k_contract_samples[] 一样，这两张表是**跨语言同一份样例**：C# 侧
 * `MqttAlarmStatusContractTests` 会解析本文件的两个区块并逐字段比对（漂移即红）。
 * **改表必须两端一起改**，且保持"一行一条"。
 * ────────────────────────────────────────────────────────────── */
typedef struct {
    const char* prefix;
    const char* alarm_id;
    int         state;       /* 1=Active 2=Ack 3=Returned 4=Sheved(保留) */
    int         severity;    /* 0=Info 1=Warning 2=Critical 3=Emergency */
    unsigned long long t_ms;
    int         tb_utc;
    const char* topic;
    const char* payload;
} alarm_sample_t;

/* clang-format off */
static const alarm_sample_t k_alarm_samples[] = {
    /* prefix, alarm_id, state, severity, t_ms,  tb_utc, topic,             payload */
    {  "hmi",   "al1",    1,     2,        1000ULL, 0, "hmi/alarm/al1",     "st=1;sev=2;t=1000;tb=boot" },
    {  "hmi",   "al1",    2,     2,        1500ULL, 1, "hmi/alarm/al1",     "st=2;sev=2;t=1500;tb=utc" },
    {  "hmi",   "al2",    3,     0,        7ULL,    0, "hmi/alarm/al2",     "st=3;sev=0;t=7;tb=boot" },
    {  "hmi",   "al9",    4,     3,        0ULL,    0, "hmi/alarm/al9",     "st=4;sev=3;t=0;tb=boot" },
    {  "a/b",   "al3",    1,     1,        42ULL,   1, "a/b/alarm/al3",     "st=1;sev=1;t=42;tb=utc" },
};
/* clang-format on */

typedef struct {
    const char* prefix;
    int         online;
    unsigned long long t_ms;
    int         tb_utc;
    const char* topic;
    const char* payload;
} status_sample_t;

/* clang-format off */
static const status_sample_t k_status_samples[] = {
    /* prefix, online, t_ms,   tb_utc, topic,        payload */
    {  "hmi",   1,      250ULL, 1,      "hmi/status", "on=1;t=250;tb=utc" },
    {  "hmi",   1,      0ULL,   0,      "hmi/status", "on=1;t=0;tb=boot" },
    {  "a/b",   1,      9ULL,   0,      "a/b/status", "on=1;t=9;tb=boot" },
};
/* clang-format on */

/* ──────────────────────────────────────────────────────────────
 * MQTT TLS 配置契约（阶段 282）—— 样例表是**跨语言同一份**：
 * C# 侧 `MqttTlsContractTests` 解析本区块逐行比对（漂移即红）。
 * 列序：mode, psk_id, psk_file, ca_path, 期望结果(0=通过 / -1=拒)
 * ────────────────────────────────────────────────────────────── */
typedef struct {
    const char* mode;
    const char* psk_id;
    const char* psk_file;
    const char* ca_path;
    int         expect;
} tls_sample_t;

/* clang-format off */
static const tls_sample_t k_tls_samples[] = {
    /* mode,  psk_id,     psk_file,            ca_path,          expect */
    {  "off", NULL,       NULL,                NULL,             0 },
    {  "off", "ignored",  "ignored",           "ignored",        0 },
    {  "psk", "hmi-01",   "/mnt/UDISK/hmi/psk.key", NULL,        0 },
    {  "psk", "",         "/mnt/UDISK/hmi/psk.key", NULL,       -1 },   /* 缺身份 */
    {  "psk", "hmi-01",   "",                  NULL,            -1 },   /* 缺密钥文件 */
    {  "ca",  NULL,       NULL,                "/mnt/UDISK/hmi/ca.pem", 0 },
    {  "ca",  NULL,       NULL,                "",              -1 },   /* 缺 CA */
};
/* clang-format on */

/* ──────────────────────────────────────────────────────────────
 * MQTT TLS **端点解析**样例（阶段 284）—— 同样跨语言同源：
 * C# 侧 `MqttTlsContractTests.EndpointSamples_...` 解析本区块逐行比对。
 * 列序：mode, broker_host, broker_port, local_port_text, 期望 host, 期望 port, 期望结果
 * ────────────────────────────────────────────────────────────── */
typedef struct {
    const char* mode;
    const char* broker;
    unsigned    broker_port;
    const char* local_port;
    const char* want_host;
    unsigned    want_port;
    int         expect;
} tls_endpoint_sample_t;

/* clang-format off */
static const tls_endpoint_sample_t k_tls_endpoint_samples[] = {
    /* mode,  broker,       bport, local_port, want_host,      wport, expect */
    {  "off", "10.0.0.5",   1883,  NULL,       "10.0.0.5",     1883,  0 },
    {  "off", "broker.lan", 8883,  NULL,       "broker.lan",   8883,  0 },
    {  "psk", "10.0.0.5",   1883,  NULL,       "127.0.0.1",    1883,  0 },  /* TLS ⇒ 连本机代理 */
    {  "psk", "10.0.0.5",   1883,  "11883",    "127.0.0.1",    11883, 0 },
    {  "ca",  "broker.lan", 8883,  "1884",     "127.0.0.1",    1884,  0 },
    {  "psk", "",           1883,  NULL,       "",             0,    -1 },  /* 空 broker 配对时也不放行 */
    {  "psk", "10.0.0.5",   1883,  "0",        "",             0,    -1 },  /* 端口 0 = 写错 */
    {  "psk", "10.0.0.5",   1883,  "70k",      "",             0,    -1 },  /* 非数字 */
    {  "off", "",           1883,  NULL,       "",             0,    -1 },  /* off 也要有 broker */
};
/* clang-format on */

static void test_mqtt_tls_endpoint(void)
{
    for (size_t i = 0; i < sizeof(k_tls_endpoint_samples) / sizeof(k_tls_endpoint_samples[0]); i++) {
        const tls_endpoint_sample_t* s = &k_tls_endpoint_samples[i];
        hmi_mqtt_tls_mode_t mode;
        char host[64];
        unsigned port = 0;

        CHECK(hmi_mqtt_tls_parse_mode(s->mode, &mode) == 0, s->mode);
        int rc = hmi_mqtt_tls_resolve_endpoint(mode, s->broker, s->broker_port, s->local_port,
                                               host, sizeof(host), &port);
        CHECK(rc == s->expect, s->mode);
        if (s->expect == 0) {
            CHECK(strcmp(host, s->want_host) == 0, s->want_host);
            CHECK(port == s->want_port, "port");
        }
    }

    /* 不许绕过代理：TLS 开着时客户端只能连回环 */
    CHECK(hmi_mqtt_tls_assert_no_bypass(HMI_MQTT_TLS_OFF, "10.0.0.5") == 0, "off 直连合法");
    CHECK(hmi_mqtt_tls_assert_no_bypass(HMI_MQTT_TLS_PSK, "127.0.0.1") == 0, "psk 连回环");
    CHECK(hmi_mqtt_tls_assert_no_bypass(HMI_MQTT_TLS_CA, "localhost") == 0, "ca 连 localhost");
    CHECK(hmi_mqtt_tls_assert_no_bypass(HMI_MQTT_TLS_PSK, "10.0.0.5") == -1, "psk 直连 broker = 绕过");
    CHECK(hmi_mqtt_tls_assert_no_bypass(HMI_MQTT_TLS_CA, NULL) == -1, "ca 无 host");
    CHECK(hmi_mqtt_tls_assert_no_bypass((hmi_mqtt_tls_mode_t)9, "127.0.0.1") == -1, "未知模式");

    /* 端口解析边界 */
    unsigned p = 0;
    CHECK(hmi_mqtt_tls_parse_port(NULL, 1883, &p) == 0 && p == 1883, "缺省端口");
    CHECK(hmi_mqtt_tls_parse_port("", 1883, &p) == 0 && p == 1883, "空串=缺省");
    CHECK(hmi_mqtt_tls_parse_port("65535", 1, &p) == 0 && p == 65535, "上界");
    CHECK(hmi_mqtt_tls_parse_port("65536", 1, &p) == -1, "超界");
    CHECK(hmi_mqtt_tls_parse_port("-1", 1, &p) == -1, "负号");
    CHECK(hmi_mqtt_tls_parse_port(" 1883", 1, &p) == -1, "前导空格");
    CHECK(hmi_mqtt_tls_parse_port("1883", 1, NULL) == -1, "空出参");
}

/* ──────────────────────────────────────────────────────────────
 * MQTT TLS **一次算完**（阶段 300）—— 设备运行期真正走的那条路径：
 * 「读到的键 ⇒ 客户端该连哪里 ＋ 这组配置能不能用 ＋ 拒绝原因」。
 * 列序：mode, broker, bport, local_port, psk_id, psk_file, ca_path, 期望ok, 期望host, 期望port, 期望reason
 *
 * ⚠ 两类刻意安排：
 *   ① 「缺件」与「模式串不认识」⇒ 期望 **ok=-1 且 host 为空串**：fail-closed，
 *      调用方即便漏判 ok 也连不到 broker（这就是「永不降级」的机械形状）。
 *   ② 缺键（NULL/"") ⇒ **off**（向后兼容，老工程不带这个键）——那不是降级。
 * ────────────────────────────────────────────────────────────── */
typedef struct {
    const char* mode;
    const char* broker;
    unsigned    broker_port;
    const char* local_port;
    const char* psk_id;
    const char* psk_file;
    const char* ca_path;
    int         want_ok;
    const char* want_host;
    unsigned    want_port;
    const char* want_reason;
} tls_resolve_sample_t;

/* clang-format off */
static const tls_resolve_sample_t k_tls_resolve_samples[] = {
    /* mode, broker,       bport, local,   psk_id,   psk_file,                  ca_path,                 ok, host,        port,  reason */
    {  NULL, "10.0.0.5",   1883,  NULL,    NULL,     NULL,                      NULL,                     0, "10.0.0.5",  1883, "" },
    {  "",   "10.0.0.5",   1883,  NULL,    NULL,     NULL,                      NULL,                     0, "10.0.0.5",  1883, "" },
    {  "off","10.0.0.5",   1883,  "11883", NULL,     NULL,                      NULL,                     0, "10.0.0.5",  1883, "" },
    {  "psk","10.0.0.5",   1883,  NULL,    "hmi-01", "/mnt/UDISK/hmi/psk.key",  NULL,                     0, "127.0.0.1", 1883, "" },
    {  "psk","10.0.0.5",   1883,  "11883", "hmi-01", "/mnt/UDISK/hmi/psk.key",  NULL,                     0, "127.0.0.1", 11883, "" },
    {  "ca", "broker.lan", 8883,  "1884",  NULL,     NULL,                      "/mnt/UDISK/hmi/ca.pem",  0, "127.0.0.1", 1884, "" },
    {  "psk","10.0.0.5",   1883,  NULL,    "",       "/mnt/UDISK/hmi/psk.key",  NULL,                    -1, "",          0, "tls-config-incomplete" },
    {  "psk","10.0.0.5",   1883,  NULL,    "hmi-01", "",                        NULL,                    -1, "",          0, "tls-config-incomplete" },
    {  "ca", "broker.lan", 8883,  NULL,    NULL,     NULL,                      "",                      -1, "",          0, "tls-config-incomplete" },
    {  "PSK","10.0.0.5",   1883,  NULL,    "hmi-01", "/mnt/UDISK/hmi/psk.key",  NULL,                    -1, "",          0, "tls-mode-unknown" },
    {  "tls","10.0.0.5",   1883,  NULL,    NULL,     NULL,                      NULL,                    -1, "",          0, "tls-mode-unknown" },
    {  "psk","",           1883,  NULL,    "hmi-01", "/mnt/UDISK/hmi/psk.key",  NULL,                    -1, "",          0, "tls-endpoint-invalid" },
    {  "psk","10.0.0.5",   1883,  "0",     "hmi-01", "/mnt/UDISK/hmi/psk.key",  NULL,                    -1, "",          0, "tls-endpoint-invalid" },
    {  "off","",           1883,  NULL,    NULL,     NULL,                      NULL,                    -1, "",          0, "tls-endpoint-invalid" },
};
/* clang-format on */

static void test_mqtt_tls_resolve_config(void)
{
    for (size_t i = 0; i < sizeof(k_tls_resolve_samples) / sizeof(k_tls_resolve_samples[0]); i++) {
        const tls_resolve_sample_t* s = &k_tls_resolve_samples[i];
        hmi_mqtt_tls_resolution_t r;

        int rc = hmi_mqtt_tls_resolve_config(s->mode, s->broker, s->broker_port, s->local_port,
                                             s->psk_id, s->psk_file, s->ca_path, &r);

        CHECK(rc == s->want_ok, s->mode ? s->mode : "(缺键)");
        CHECK(r.ok == s->want_ok, "ok 字段必须与返回值同向");
        CHECK(strcmp(r.host, s->want_host) == 0, "host");
        CHECK(r.port == s->want_port, "port");
        CHECK(strcmp(r.reason, s->want_reason) == 0, s->want_reason);
    }

    /* 空出参：必须报错而不是写坏内存 */
    CHECK(hmi_mqtt_tls_resolve_config("off", "10.0.0.5", 1883, NULL, NULL, NULL, NULL, NULL) == -1,
          "空出参");

    /* **永不降级**的总断言：无论怎么配错，都不许给出"可用的 off" */
    {
        hmi_mqtt_tls_resolution_t r;
        /* psk 缺件 ⇒ 拒绝，且**不得**留下一个可连的 broker 地址 */
        (void)hmi_mqtt_tls_resolve_config("psk", "10.0.0.5", 1883, NULL, NULL, NULL, NULL, &r);
        CHECK(r.ok == -1 && r.host[0] == '\0' && r.port == 0, "psk 缺件必须 fail-closed");
        /* ca 缺件 ⇒ 同上 */
        (void)hmi_mqtt_tls_resolve_config("ca", "10.0.0.5", 1883, NULL, NULL, NULL, NULL, &r);
        CHECK(r.ok == -1 && r.host[0] == '\0' && r.port == 0, "ca 缺件必须 fail-closed");
        /* 模式串不认识 ⇒ 同上（不许当 off 直连 broker） */
        (void)hmi_mqtt_tls_resolve_config("yes", "10.0.0.5", 1883, NULL, NULL, NULL, NULL, &r);
        CHECK(r.ok == -1 && r.host[0] == '\0' && r.port == 0, "未知模式必须 fail-closed");
    }
}


/* ──────────────────────────────────────────────────────────────
 * 远程写命令信封样例（阶段 285）—— 跨语言同源：C# 侧
 * `MqttCommandContractTests.Samples_MatchTheDeviceHostTestTable` 解析本区块逐行比对。
 * 列序：id, ts, ttl, now, 期望 (0=收 / -1=拒)
 * ────────────────────────────────────────────────────────────── */
typedef struct {
    const char*  id;
    unsigned long long ts;
    unsigned long long ttl;
    unsigned long long now;
    int          expect;
} cmd_fresh_sample_t;

/* clang-format off */
static const cmd_fresh_sample_t k_cmd_fresh_samples[] = {
    /* id,        ts,       ttl,     now,        expect */
    {  "cmd-001",  1000000,  60000,   1030000,    0 },   /* 半分钟内 ⇒ 收 */
    {  "cmd-002",  1000000,  60000,   1060000,    0 },   /* 恰好 = TTL ⇒ 收（闭区间） */
    {  "cmd-003",  1000000,  60000,   1060001,   -1 },   /* 超 1ms ⇒ 过期 */
    {  "cmd-004",  1000000,  60000,    900000,   -1 },   /* now 早于 ts（远超偏斜） */
    {  "cmd-005",  1000000,  60000,   1003000,    0 },   /* 偏斜容差内 */
    {  "cmd-006",  1000000,      0,   1000000,   -1 },   /* TTL 非法（0） */
    {  "cmd-007",  1000000, 600000,   1000000,   -1 },   /* TTL 超上界 */
};
/* clang-format on */

/* ──────────────────────────────────────────────────────────────
 * 主题 ACL 样例（阶段 286）—— 跨语言同源：C# 侧
 * `MqttTopicAclTests.Samples_MatchTheDeviceHostTestTable` 解析本区块逐行比对。
 * 前缀统一 "hmi/s1"；列序：topic, is_write, perm_mask(hex), 期望(0=允许/-1=拒), 期望原因
 *   perm_mask 位（与生成头 HMI_PERM_* 同值）：
 *     0x001 ViewScreen   0x002 OperateControl   0x008 AcknowledgeAlarm   0x040 ViewAlarmHistory   0x200 ShelveAlarm
 * ────────────────────────────────────────────────────────────── */
typedef struct {
    const char* topic;
    int         is_write;
    unsigned    perm_mask;
    int         expect;
    const char* reason;     /* "-" = 期望 reason 为 NULL */
} acl_sample_t;

/* clang-format off */
static const acl_sample_t k_acl_samples[] = {
    /* topic,                    wr, mask,   expect, reason */
    {  "hmi/s1/value/v1",         0, 0x001,   0,     "-" },                     /* 读数据：有查看权 */
    {  "hmi/s1/value/v1",         0, 0x000,  -1,     "forbidden" },             /* 读数据：无查看权 */
    {  "hmi/s1/value/v1",         1, 0x3FF,  -1,     "wrong-direction" },       /* A1：远程不许伪造遥测 */
    {  "hmi/s1/alarm/a1",         0, 0x040,   0,     "-" },                     /* 读报警：有历史权 */
    {  "hmi/s1/alarm/a1",         0, 0x001,  -1,     "forbidden" },             /* 只有查看权 ⇒ 不够 */
    {  "hmi/s1/alarm/a1",         1, 0x3FF,  -1,     "wrong-direction" },       /* A1：不许伪造报警 */
    {  "hmi/s1/status",           0, 0x001,   0,     "-" },
    {  "hmi/s1/status",           1, 0x3FF,  -1,     "wrong-direction" },
    {  "hmi/s1/cmd/v1",           1, 0x002,   0,     "-" },                     /* 控制命令：有操作权 */
    {  "hmi/s1/cmd/v1",           1, 0x001,  -1,     "forbidden" },             /* A2：只有查看权 */
    {  "hmi/s1/cmd/v1",           0, 0x3FF,  -1,     "wrong-direction" },       /* 命令方向反了 */
    {  "hmi/s1/cmd/ack/a1",       1, 0x008,   0,     "-" },                     /* 确认：要确认权 */
    {  "hmi/s1/cmd/ack/a1",       1, 0x002,  -1,     "forbidden" },             /* 只有操作权 ⇒ 不够 */
    {  "hmi/s1/cmd/shelve/a1",    1, 0x200,   0,     "-" },                     /* 搁置：要 §275 那一位 */
    {  "hmi/s1/cmd/shelve/a1",    1, 0x008,  -1,     "forbidden" },             /* 能确认 ≠ 能搁置 */
    {  "hmi/s1/cmd/unshelve/a1",  1, 0x200,   0,     "-" },
    {  "hmi/s1/cmd/unknown/a1",   1, 0x002,   0,     "-" },                     /* 未知名 ⇒ 按操作权 */
    {  "other/value/v1",          0, 0x3FF,  -1,     "unknown-topic" },         /* A3：前缀不符 */
    {  "hmi/s1/telemetry/v1",     0, 0x3FF,  -1,     "unknown-topic" },         /* A3：形状不符 */
    {  "hmi/s1/value/",           0, 0x3FF,  -1,     "unknown-topic" },         /* A3：空目标 */
    {  "hmi/s1/cmd/",             1, 0x3FF,  -1,     "unknown-topic" },         /* A3：空命令目标 */
    {  "hmi/s1values/v1",         0, 0x3FF,  -1,     "unknown-topic" },         /* A3：前缀"包含"不算匹配 */
    {  "hmi/s1status",            0, 0x3FF,  -1,     "unknown-topic" },         /* A3：前缀后必须紧跟 '/' */
    {  "hmi/s1/statusx",          0, 0x3FF,  -1,     "unknown-topic" },         /* A3：status 不许带后缀 */
};
/* clang-format on */

/* ──────────────────────────────────────────────────────────────
 * 写命令**值校验**样例（阶段 287）—— 跨语言同源：C# 侧
 * `MqttWriteGuardTests.Samples_MatchTheDeviceHostTestTable` 解析本区块逐行比对。
 * 列序：writable, min, max, value_text(或 NULL), 期望码, 期望 token(或 -)
 * ────────────────────────────────────────────────────────────── */
typedef struct {
    int         writable;
    double      min;
    double      max;
    const char* value;
    int         expect;
    const char* token;      /* "-" = 期望 token 为 NULL（OK） */
} write_guard_sample_t;

/* clang-format off */
static const write_guard_sample_t k_write_guard_samples[] = {
    /* wr, min,   max,   value,    expect,  token */
    {  1,   0.0,  100.0, "42",      0,      "-" },
    {  1,   0.0,  100.0, "0",       0,      "-" },              /* 下界含 */
    {  1,   0.0,  100.0, "100",     0,      "-" },              /* 上界含 */
    {  1,  -1.0,  100.0, "-0.5",    0,      "-" },              /* 负数 + 小数（min 允许时为 OK） */
    {  1, -50.0,  100.0, "-50",     0,      "-" },
    {  1,   0.0,  100.0, "100.001", -3,     "out-of-range" },
    {  1,   0.0,  100.0, "-0.001",  -3,     "out-of-range" },
    {  1,   0.0,  100.0, "abc",     -2,     "not-a-number" },
    {  1,   0.0,  100.0, "",        -2,     "not-a-number" },
    {  1,   0.0,  100.0, " 42",     -2,     "not-a-number" },   /* 前导空格 */
    {  1,   0.0,  100.0, "42 ",     -2,     "not-a-number" },   /* 尾随空格 */
    {  1,   0.0,  100.0, "42kg",    -2,     "not-a-number" },   /* 尾随垃圾 */
    {  1,   0.0,  100.0, "nan",     -2,     "not-a-number" },
    {  1,   0.0,  100.0, "inf",     -2,     "not-a-number" },
    {  0,   0.0,  100.0, "42",      -1,     "read-only" },      /* 只读优先：值合法也拒 */
    {  0,   0.0,  100.0, "abc",     -1,     "read-only" },      /* 只读优先于"不是数字" */
    {  1,  100.0,   0.0, "42",      -4,     "bad-target" },     /* min > max = 配置错 */
};
/* clang-format on */

/* ──────────────────────────────────────────────────────────────
 * 幂等台账样例（阶段 287）—— 跨语言同源：C# 侧 `CommandLedger` 必须同语义。
 * 列序：id, now, ttl, 期望 accept(1=收/0=重复), 期望 accept 后条目数
 * ────────────────────────────────────────────────────────────── */
typedef struct {
    const char*        id;
    unsigned long long now;
    unsigned long long ttl;
    int                expect;
    int                count_after;
} cmd_ledger_sample_t;

/* clang-format off */
static const cmd_ledger_sample_t k_cmd_ledger_samples[] = {
    /* id,       now,     ttl,    expect, count_after */
    {  "c1",     1000,    60000,   1,      1 },
    {  "c1",     2000,    60000,   0,      1 },   /* 未过期 ⇒ 重复 */
    {  "c1",     62001,   60000,   1,      1 },   /* 已过期 ⇒ 可再用（淘汰后收下） */
    {  "c2",     63000,    60000,   1,      2 },
    {  "c3",     64000,    60000,   1,      3 },
};
/* clang-format on */

/* ──────────────────────────────────────────────────────────────
 * 远程写命令的**调用点**（阶段 288）—— 假宿主（可注入）驱动，判"五类拒绝各不写变量、
 * 各留一条审计；成功那条要写变量且审计含旧值→新值"。
 * ────────────────────────────────────────────────────────────── */
static double              s_fake_value = 0.0;
static int                 s_fake_set_calls = 0;
static int                 s_fake_set_result = 0;      /* 0 = 写成功；非 0 = 写失败 */
static int                 s_fake_audit_count = 0;
static char                s_fake_audit_last_event[64];
static char                s_fake_audit_last_by[32];
static char                s_fake_audit_last_detail[160];
static int                 s_fake_denied_count = 0;

static void fake_reset(void)
{
    s_fake_value = 0.0;
    s_fake_set_calls = 0;
    s_fake_set_result = 0;
    s_fake_audit_count = 0;
    s_fake_denied_count = 0;
    memset(s_fake_audit_last_event, 0, sizeof(s_fake_audit_last_event));
    memset(s_fake_audit_last_by, 0, sizeof(s_fake_audit_last_by));
    memset(s_fake_audit_last_detail, 0, sizeof(s_fake_audit_last_detail));
}

/* 造一份 target 元数据（设备侧本该从采纳的标签表按值取出来） */
static hmi_cmd_target_t fake_meta(const char* target, int writable, int has_range, double min, double max)
{
    hmi_cmd_target_t m;
    m.target = target;
    m.writable = writable;
    m.has_range = has_range;
    m.min_value = min;
    m.max_value = max;
    return m;
}

static int fake_get_value(const char* target, double* out) { (void)target; *out = s_fake_value; return 0; }
static int fake_set_value(const char* target, double value)
{
    (void)target;
    s_fake_set_calls++;
    if (s_fake_set_result != 0) return s_fake_set_result;
    s_fake_value = value;
    return 0;
}
static void fake_audit(const char* event, const char* by, const char* detail)
{
    s_fake_audit_count++;
    if (strcmp(event, "remote-write-denied") == 0) s_fake_denied_count++;
    snprintf(s_fake_audit_last_event, sizeof(s_fake_audit_last_event), "%s", event);
    snprintf(s_fake_audit_last_by, sizeof(s_fake_audit_last_by), "%s", by != NULL ? by : "");
    snprintf(s_fake_audit_last_detail, sizeof(s_fake_audit_last_detail), "%s", detail != NULL ? detail : "");
}

static const hmi_cmd_io_t k_fake_io = {
    fake_get_value, fake_set_value, fake_audit
};

/* 组一条命令载荷（target 用参数，value 字符串直给） */
static void fake_payload(char* out, size_t cap, const char* id, const char* target, const char* value)
{
    snprintf(out, cap, "id=%s;ts=1000000;ttl=60000;by=alice;target=%s;value=%s", id, target, value);
}

static void test_mqtt_cmd_dispatch(void)
{
    hmi_mqtt_cmd_ledger_t ledger;
    hmi_cmd_result_t res;
    hmi_cmd_target_t meta;
    hmi_cmd_target_t ro_meta;
    char payload[192];
    const char* topic = "hmi/s1/cmd/v1";
    const unsigned kOperate = 0x002u;

    /* ① 成功：写变量 + 审计一条（含旧值→新值） */
    hmi_mqtt_cmd_ledger_init(&ledger);
    fake_reset();
    meta = fake_meta("v1", 1, 1, 0.0, 100.0);
    s_fake_value = 7.5;
    fake_payload(payload, sizeof(payload), "ok1", "v1", "42");
    CHECK(hmi_cmd_handle(topic, "hmi/s1", payload, 1000500, kOperate, &ledger, &meta, &k_fake_io, &res) == 0, "成功");
    CHECK(res.wrote == 1 && s_fake_set_calls == 1, "调用了写");
    CHECK(s_fake_value == 42.0, "新值写入");
    CHECK(res.old_value == 7.5 && res.new_value == 42.0, "旧值→新值");
    CHECK(s_fake_audit_count == 1 && s_fake_denied_count == 0, "成功只写一条审计");
    CHECK(strcmp(s_fake_audit_last_event, "remote-write") == 0, "审计事件名");
    CHECK(strstr(s_fake_audit_last_detail, "old=7.5") != NULL &&
          strstr(s_fake_audit_last_detail, "new=42") != NULL, "审计明细含旧值→新值");

    /* ② 重复（同 id）⇒ 不写变量、审计一条 denied */
    {
        int before = s_fake_set_calls;
        fake_payload(payload, sizeof(payload), "ok1", "v1", "43");
        CHECK(hmi_cmd_handle(topic, "hmi/s1", payload, 1001000, kOperate, &ledger, &meta, &k_fake_io, &res) == -1, "重复被拒");
        CHECK(strcmp(res.code, "duplicate") == 0, "重复码");
        CHECK(s_fake_set_calls == before, "重复不写变量");
        CHECK(strcmp(s_fake_audit_last_event, "remote-write-denied") == 0, "重复留痕");
    }

    /* ③ 未授权（掩码里没有操作权）⇒ 不写变量、留痕 */
    {
        int before = s_fake_set_calls;
        int audits = s_fake_audit_count;
        fake_payload(payload, sizeof(payload), "noauth", "v1", "42");
        CHECK(hmi_cmd_handle(topic, "hmi/s1", payload, 1001000, 0x001u, &ledger, &meta, &k_fake_io, &res) == -1, "未授权被拒");
        CHECK(strcmp(res.code, HMI_MQTT_ACL_FORBIDDEN) == 0, "forbidden");
        CHECK(s_fake_set_calls == before, "未授权不写变量");
        CHECK(s_fake_audit_count == audits + 1, "留了一条审计");
    }

    /* ④ 过期 ⇒ expired、不写、留痕；且**不进台账** */
    {
        char old_payload[192];
        snprintf(old_payload, sizeof(old_payload),
                 "id=old1;ts=1000000;ttl=60000;by=alice;target=v1;value=42");
        CHECK(hmi_cmd_handle(topic, "hmi/s1", old_payload, 1200000, kOperate, &ledger, &meta, &k_fake_io, &res) == -1, "过期被拒");
        CHECK(strcmp(res.code, "expired") == 0, "expired 码");
        CHECK(res.ledger_used == 0, "被拒命令不进台账");
    }

    /* ⑤ 只读 target ⇒ read-only */
    {
        char ro[192];
        ro_meta = fake_meta("ro1", 0, 1, 0.0, 100.0);
        snprintf(ro, sizeof(ro), "id=ro-cmd;ts=1000000;ttl=60000;by=alice;target=ro1;value=1");
        CHECK(hmi_cmd_handle(topic, "hmi/s1", ro, 1000500, kOperate, &ledger, &ro_meta, &k_fake_io, &res) == -1, "只读被拒");
        CHECK(strcmp(res.code, "read-only") == 0, "read-only 码");
    }

    /* ⑥ 超量程 ⇒ out-of-range；target 不存在 ⇒ target-not-found */
    {
        char oor[192], nf[192];
        snprintf(oor, sizeof(oor), "id=oor;ts=1000000;ttl=60000;by=alice;target=v1;value=101");
        CHECK(hmi_cmd_handle(topic, "hmi/s1", oor, 1000500, kOperate, &ledger, &meta, &k_fake_io, &res) == -1, "超量程被拒");
        CHECK(strcmp(res.code, "out-of-range") == 0, "out-of-range 码");

        snprintf(nf, sizeof(nf), "id=nf;ts=1000000;ttl=60000;by=alice;target=ghost;value=1");
        CHECK(hmi_cmd_handle(topic, "hmi/s1", nf, 1000500, kOperate, &ledger, NULL, &k_fake_io, &res) == -1, "未知 target 被拒");
        CHECK(strcmp(res.code, HMI_CMD_DENIED_NOT_FOUND) == 0, "target-not-found 码");
    }

    /* ⑦ 方向反了 / 主题不认识 ⇒ ACL 直接拒（连解析都不做） */
    {
        CHECK(hmi_cmd_handle("hmi/s1/value/v1", "hmi/s1", "id=x;ts=1;ttl=60000;target=v1;value=1",
                             1000500, 0x3FFu, &ledger, &meta, &k_fake_io, &res) == -1, "读主题写被拒");
        CHECK(strcmp(res.code, HMI_MQTT_ACL_WRONG_DIRECTION) == 0, "wrong-direction");
        CHECK(hmi_cmd_handle("other/cmd/v1", "hmi/s1", "id=x;ts=1;ttl=60000;target=v1;value=1",
                             1000500, 0x3FFu, &ledger, &meta, &k_fake_io, &res) == -1, "前缀不符被拒");
        CHECK(strcmp(res.code, HMI_MQTT_ACL_UNKNOWN_TOPIC) == 0, "unknown-topic");
    }

    /* ⑧ 解析失败（缺字段 / 非法 id / 负 ts）⇒ malformed，不写变量 */
    {
        int before = s_fake_set_calls;
        CHECK(hmi_cmd_handle(topic, "hmi/s1", "id=x;ttl=60000;target=v1;value=1",
                             1000500, kOperate, &ledger, &meta, &k_fake_io, &res) == -1, "缺 ts");
        CHECK(strcmp(res.code, "malformed") == 0, "malformed 码（缺 ts）");
        CHECK(hmi_cmd_handle(topic, "hmi/s1", "id=bad id;ts=1;ttl=60000;target=v1;value=1",
                             1000500, kOperate, &ledger, &meta, &k_fake_io, &res) == -1, "非法 id");
        CHECK(hmi_cmd_handle(topic, "hmi/s1", "id=x;ts=-1;ttl=60000;target=v1;value=1",
                             1000500, kOperate, &ledger, &meta, &k_fake_io, &res) == -1, "负 ts（strtoull 会回绕 ⇒ 必须显式拒）");
        CHECK(s_fake_set_calls == before, "malformed 不写变量");
    }

    /* ⑨ 写失败（宿主返回非 0）⇒ write-failed，且**台账已消耗**（同 id 不能重放） */
    {
        hmi_mqtt_cmd_ledger_init(&ledger);
        fake_reset();
        meta = fake_meta("v1", 1, 1, 0.0, 100.0);
        s_fake_set_result = -1;
        fake_payload(payload, sizeof(payload), "wf1", "v1", "42");
        CHECK(hmi_cmd_handle(topic, "hmi/s1", payload, 1000500, kOperate, &ledger, &meta, &k_fake_io, &res) == -1, "写失败");
        CHECK(strcmp(res.code, HMI_CMD_DENIED_WRITE_FAILED) == 0, "write-failed 码");
        CHECK(res.ledger_used == 1, "写失败时台账已消耗（同一 id 不许重放）");
        s_fake_set_result = 0;
        CHECK(hmi_cmd_handle(topic, "hmi/s1", payload, 1000600, kOperate, &ledger, &meta, &k_fake_io, &res) == -1, "同 id 重放被拒");
        CHECK(strcmp(res.code, "duplicate") == 0, "重放 = duplicate");
    }
}

static void test_mqtt_write_guard(void)
{
    for (size_t i = 0; i < sizeof(k_write_guard_samples) / sizeof(k_write_guard_samples[0]); i++) {
        const write_guard_sample_t* s = &k_write_guard_samples[i];
        const char* reason = "unset";
        double out = -12345.0;
        int rc = hmi_mqtt_write_check(s->writable, s->min, s->max, s->value, &out, &reason);

        CHECK(rc == s->expect, s->value == NULL ? "(null)" : s->value);
        if (s->expect == 0) {
            CHECK(reason == NULL, "OK 时 reason 必须为 NULL");
            CHECK(out != -12345.0, "OK 时必须写 out_value");     /* 0.0 也要写（用哨兵值判） */
        } else {
            CHECK(reason != NULL && strcmp(reason, s->token) == 0, s->token);
        }
    }

    /* reason 出参可空；out_value 可空 */
    CHECK(hmi_mqtt_write_check(1, 0, 10, "5", NULL, NULL) == HMI_MQTT_WRITE_OK, "out/reason 都可空");
    CHECK(hmi_mqtt_write_check(1, 0, 10, NULL, NULL, NULL) == HMI_MQTT_WRITE_NOT_A_NUMBER, "value NULL");
    CHECK(hmi_mqtt_write_issue_token(HMI_MQTT_WRITE_OK) == NULL, "OK 无 token");
    CHECK(hmi_mqtt_write_issue_token(999) == NULL, "未知码无 token");
    CHECK(strcmp(hmi_mqtt_write_issue_token(HMI_MQTT_WRITE_OUT_OF_RANGE), "out-of-range") == 0, "token 文案");
}

static void test_mqtt_cmd_ledger(void)
{
    hmi_mqtt_cmd_ledger_t ledger;

    hmi_mqtt_cmd_ledger_init(&ledger);
    CHECK(hmi_mqtt_cmd_ledger_count(&ledger) == 0, "初始空");

    for (size_t i = 0; i < sizeof(k_cmd_ledger_samples) / sizeof(k_cmd_ledger_samples[0]); i++) {
        const cmd_ledger_sample_t* s = &k_cmd_ledger_samples[i];
        int rc = hmi_mqtt_cmd_ledger_accept(&ledger, s->id, s->now, s->ttl);
        CHECK(rc == s->expect, s->id);
        CHECK(hmi_mqtt_cmd_ledger_count(&ledger) == s->count_after, "条目数");
    }

    /* 参数非法 */
    CHECK(hmi_mqtt_cmd_ledger_accept(NULL, "a", 0, 1000) == -1, "台账 NULL");
    CHECK(hmi_mqtt_cmd_ledger_accept(&ledger, "bad id", 0, 1000) == -1, "id 形状");
    CHECK(hmi_mqtt_cmd_ledger_accept(&ledger, "ok", 0, 0) == -1, "TTL 非法");
    CHECK(hmi_mqtt_cmd_ledger_count(NULL) == 0, "count(NULL)");

    /* 有界：容量满且都未过期 ⇒ 丢最先到期的（不是最先来的） */
    hmi_mqtt_cmd_ledger_init(&ledger);
    {
        char id[16];
        /* 先塞一条"很快到期"的，再塞满容量，最后加一条新的 ⇒ 被丢的应是那条短命的 */
        CHECK(hmi_mqtt_cmd_ledger_accept(&ledger, "short", 0, HMI_MQTT_CMD_MIN_TTL_MS) == 1, "短命条目");
        for (int i = 0; i < HMI_MQTT_CMD_LEDGER_CAP - 1; i++) {
            snprintf(id, sizeof(id), "long%d", i);
            CHECK(hmi_mqtt_cmd_ledger_accept(&ledger, id, 0, HMI_MQTT_CMD_MAX_TTL_MS) == 1, "长命条目");
        }
        CHECK(hmi_mqtt_cmd_ledger_count(&ledger) == HMI_MQTT_CMD_LEDGER_CAP, "已满");
        CHECK(hmi_mqtt_cmd_ledger_accept(&ledger, "new", 0, HMI_MQTT_CMD_MAX_TTL_MS) == 1, "满后仍可收");
        CHECK(hmi_mqtt_cmd_ledger_count(&ledger) == HMI_MQTT_CMD_LEDGER_CAP, "仍满（有界）");
        CHECK(hmi_mqtt_cmd_ledger_accept(&ledger, "short", 0, HMI_MQTT_CMD_MIN_TTL_MS) == 1,
              "短命的那条被丢 ⇒ 同 id 可再收");
        CHECK(hmi_mqtt_cmd_ledger_accept(&ledger, "long0", 0, HMI_MQTT_CMD_MAX_TTL_MS) == 0, "长命的还在");
    }
}

static void test_mqtt_acl_contract(void)
{
    const char* prefix = "hmi/s1";

    for (size_t i = 0; i < sizeof(k_acl_samples) / sizeof(k_acl_samples[0]); i++) {
        const acl_sample_t* s = &k_acl_samples[i];
        const char* reason = "unset";
        int rc = hmi_mqtt_acl_allows(s->topic, prefix, s->is_write, s->perm_mask, &reason);

        CHECK(rc == s->expect, s->topic);
        if (s->expect == 0) {
            CHECK(reason == NULL, "允许时 reason 必须为 NULL");
        } else {
            CHECK(reason != NULL && strcmp(reason, s->reason) == 0, s->reason);
        }
    }

    /* reason 出参可空（调用方只关心允许/拒绝时） */
    CHECK(hmi_mqtt_acl_allows("hmi/s1/value/v1", prefix, 0, 0x001, NULL) == 0, "reason=NULL 允许");
    CHECK(hmi_mqtt_acl_allows("hmi/s1/value/v1", prefix, 0, 0x000, NULL) == -1, "reason=NULL 拒绝");

    /* 主题分类本身（判据与实现同源） */
    CHECK(hmi_mqtt_topic_kind("hmi/s1/value/v", "hmi/s1") == HMI_MQTT_TOPIC_VALUE, "kind value");
    CHECK(hmi_mqtt_topic_kind("hmi/s1/alarm/a", "hmi/s1") == HMI_MQTT_TOPIC_ALARM, "kind alarm");
    CHECK(hmi_mqtt_topic_kind("hmi/s1/status", "hmi/s1") == HMI_MQTT_TOPIC_STATUS, "kind status");
    CHECK(hmi_mqtt_topic_kind("hmi/s1/cmd/x", "hmi/s1") == HMI_MQTT_TOPIC_CMD, "kind cmd");
    CHECK(hmi_mqtt_topic_kind(NULL, "hmi/s1") == HMI_MQTT_TOPIC_UNKNOWN, "topic NULL");
    CHECK(hmi_mqtt_topic_kind("hmi/s1/value/v", NULL) == HMI_MQTT_TOPIC_UNKNOWN, "prefix NULL");
    CHECK(hmi_mqtt_topic_kind("hmi/s1/value/v", "") == HMI_MQTT_TOPIC_UNKNOWN, "空前缀");

    /* 未知名命令目标按操作权（required_perm 直判）—— 用**生成头里的宏**：
     * ACL 模块本身就依赖 generated_security.h，这里直接用宏比十六进制字面量更硬
     * （位值改了两侧一起红）。 */
    CHECK(hmi_mqtt_acl_required_perm(HMI_MQTT_TOPIC_CMD, 1, "x") == HMI_PERM_OPERATE_CONTROL, "默认操作权");
    CHECK(hmi_mqtt_acl_required_perm(HMI_MQTT_TOPIC_CMD, 1, "ack/a") == HMI_PERM_ACKNOWLEDGE_ALARM, "确认权");
    CHECK(hmi_mqtt_acl_required_perm(HMI_MQTT_TOPIC_CMD, 1, "shelve/a") == HMI_PERM_SHELVE_ALARM, "搁置权");
    CHECK(hmi_mqtt_acl_required_perm(HMI_MQTT_TOPIC_VALUE, 1, NULL) == 0u, "上行不可写");
    CHECK(hmi_mqtt_acl_required_perm(HMI_MQTT_TOPIC_UNKNOWN, 0, NULL) == (unsigned)-1, "未知名");
}

static void test_mqtt_cmd_contract(void)
{
    /* R1 幂等 id 形状 */
    CHECK(hmi_mqtt_cmd_is_id_valid("cmd-001") == 0, "普通 id");
    CHECK(hmi_mqtt_cmd_is_id_valid("A_z-9") == 0, "字母数字下划横线");
    CHECK(hmi_mqtt_cmd_is_id_valid("") == -1, "空 id");
    CHECK(hmi_mqtt_cmd_is_id_valid(NULL) == -1, "NULL id");
    CHECK(hmi_mqtt_cmd_is_id_valid("has space") == -1, "空格");
    CHECK(hmi_mqtt_cmd_is_id_valid("has;semi") == -1, "分号（会破坏载荷）");
    CHECK(hmi_mqtt_cmd_is_id_valid("has=eq") == -1, "等号");
    {
        char long_id[HMI_MQTT_CMD_ID_MAX + 2];
        memset(long_id, 'a', HMI_MQTT_CMD_ID_MAX + 1);
        long_id[HMI_MQTT_CMD_ID_MAX + 1] = '\0';
        CHECK(hmi_mqtt_cmd_is_id_valid(long_id) == -1, "超长 id");
        long_id[HMI_MQTT_CMD_ID_MAX] = '\0';
        CHECK(hmi_mqtt_cmd_is_id_valid(long_id) == 0, "恰好 64 字符");
    }

    /* R2 TTL 上下界 */
    CHECK(hmi_mqtt_cmd_is_ttl_valid(HMI_MQTT_CMD_MIN_TTL_MS) == 0, "下界");
    CHECK(hmi_mqtt_cmd_is_ttl_valid(HMI_MQTT_CMD_MAX_TTL_MS) == 0, "上界");
    CHECK(hmi_mqtt_cmd_is_ttl_valid(HMI_MQTT_CMD_MIN_TTL_MS - 1) == -1, "低于下界");
    CHECK(hmi_mqtt_cmd_is_ttl_valid(HMI_MQTT_CMD_MAX_TTL_MS + 1) == -1, "高于上界");
    CHECK(hmi_mqtt_cmd_is_ttl_valid(0) == -1, "0 = 永久有效（拒）");

    /* R3 新鲜度：**样例表**（跨语言同源）＋ 边界 */
    for (size_t i = 0; i < sizeof(k_cmd_fresh_samples) / sizeof(k_cmd_fresh_samples[0]); i++) {
        const cmd_fresh_sample_t* s = &k_cmd_fresh_samples[i];
        CHECK(hmi_mqtt_cmd_is_fresh(s->ts, s->ttl, s->now) == s->expect, s->id);
    }

    CHECK(hmi_mqtt_cmd_is_fresh(1000000, 60000, 1030000) == 0, "半分钟");
    CHECK(hmi_mqtt_cmd_is_fresh(1000000, 60000, 1060000) == 0, "恰好 TTL（闭区间）");
    CHECK(hmi_mqtt_cmd_is_fresh(1000000, 60000, 1060001) == -1, "超 1ms 即过期");
    CHECK(hmi_mqtt_cmd_is_fresh(1000000, 60000, 900000) == -1, "now 早于 ts（远超偏斜）");
    CHECK(hmi_mqtt_cmd_is_fresh(1000000, 60000, 1003000) == 0, "now 略晚于 ts（3s，容差内）");
    CHECK(hmi_mqtt_cmd_is_fresh(1000000, 0, 1000000) == -1, "TTL 非法");
    CHECK(hmi_mqtt_cmd_is_fresh(1000000, 600000, 1000000) == -1, "TTL 超上界");

    /* 主题 */
    char topic[64];
    CHECK(hmi_mqtt_format_cmd_topic(topic, sizeof(topic), "hmi/site1", "v1") == 0, "主题");
    CHECK(strcmp(topic, "hmi/site1/cmd/v1") == 0, "主题内容");
    CHECK(hmi_mqtt_format_cmd_topic(topic, sizeof(topic), "", "v1") == -1, "空前缀");
    CHECK(hmi_mqtt_format_cmd_topic(topic, sizeof(topic), "p", "") == -1, "空目标");
    CHECK(hmi_mqtt_format_cmd_topic(topic, 4, "hmi/site1", "v1") == -1, "容量不够");

    /* 载荷：字段顺序与分段；空 by 不出该段 */
    char payload[256];
    CHECK(hmi_mqtt_format_cmd_payload(payload, sizeof(payload), "cmd-001", 1000000, 60000,
                                      "alice", "v1", "42") == 0, "拼载荷");
    CHECK(strcmp(payload, "id=cmd-001;ts=1000000;ttl=60000;by=alice;target=v1;value=42") == 0, "载荷内容");
    CHECK(hmi_mqtt_format_cmd_payload(payload, sizeof(payload), "cmd-001", 1000000, 60000,
                                      "", "v1", "42") == 0, "无 by");
    CHECK(strcmp(payload, "id=cmd-001;ts=1000000;ttl=60000;target=v1;value=42") == 0, "无 by 的载荷");
    CHECK(hmi_mqtt_format_cmd_payload(payload, sizeof(payload), "bad id", 1000000, 60000,
                                      NULL, "v1", "42") == -1, "非法 id 不许拼");
}

static void test_mqtt_tls_contract(void)
{
    for (size_t i = 0; i < sizeof(k_tls_samples) / sizeof(k_tls_samples[0]); i++) {
        const tls_sample_t* s = &k_tls_samples[i];
        hmi_mqtt_tls_mode_t mode;
        CHECK(hmi_mqtt_tls_parse_mode(s->mode, &mode) == 0, s->mode);
        CHECK(strcmp(hmi_mqtt_tls_mode_token(mode), s->mode) == 0, "token 往返");
        CHECK(hmi_mqtt_tls_validate(mode, s->psk_id, s->psk_file, s->ca_path) == s->expect, s->mode);
    }

    /* 未知模式**不许**当 off（静默降级） */
    hmi_mqtt_tls_mode_t m;
    CHECK(hmi_mqtt_tls_parse_mode("tls", &m) == -1, "未知模式拒绝");
    CHECK(hmi_mqtt_tls_parse_mode("OFF", &m) == -1, "大小写敏感（只认小写）");
    CHECK(hmi_mqtt_tls_parse_mode("", &m) == -1, "空串拒绝");
    CHECK(hmi_mqtt_tls_parse_mode(NULL, &m) == -1, "NULL 拒绝");
    CHECK(hmi_mqtt_tls_mode_token((hmi_mqtt_tls_mode_t)9) == NULL, "未知枚举无 token");
    CHECK(hmi_mqtt_tls_validate((hmi_mqtt_tls_mode_t)9, "a", "b", "c") == -1, "未知枚举拒绝");
}

static void test_mqtt_alarm_status_contract(void)
{
    char topic[192], payload[64];

    for (size_t i = 0; i < sizeof(k_alarm_samples) / sizeof(k_alarm_samples[0]); i++) {
        const alarm_sample_t* s = &k_alarm_samples[i];
        CHECK(hmi_mqtt_format_alarm_topic(topic, sizeof(topic), s->prefix, s->alarm_id) == 0, s->topic);
        CHECK(strcmp(topic, s->topic) == 0, s->topic);
        CHECK(hmi_mqtt_format_alarm_payload(payload, sizeof(payload), s->state, s->severity,
                                            s->t_ms, s->tb_utc) == 0, s->payload);
        CHECK(strcmp(payload, s->payload) == 0, s->payload);
    }

    for (size_t i = 0; i < sizeof(k_status_samples) / sizeof(k_status_samples[0]); i++) {
        const status_sample_t* s = &k_status_samples[i];
        CHECK(hmi_mqtt_format_status_topic(topic, sizeof(topic), s->prefix) == 0, s->topic);
        CHECK(strcmp(topic, s->topic) == 0, s->topic);
        CHECK(hmi_mqtt_format_status_payload(payload, sizeof(payload), s->online,
                                             s->t_ms, s->tb_utc) == 0, s->payload);
        CHECK(strcmp(payload, s->payload) == 0, s->payload);
    }

    /* 遗嘱：离线 payload 必须与常量**逐字相同**（设备掉线时 broker 代发的就是这一条） */
    CHECK(hmi_mqtt_format_status_payload(payload, sizeof(payload), 0, 12345ULL, 1) == 0, "离线 payload");
    CHECK(strcmp(payload, HMI_MQTT_STATUS_LWT_PAYLOAD) == 0, "离线 payload == 遗嘱常量");

    /* 非法参数：状态 0 / 越界、severity 越界、报警 ID 含 `/` */
    CHECK(hmi_mqtt_format_alarm_payload(payload, sizeof(payload), 0, 1, 1ULL, 0) == -1, "状态 0 拒绝");
    CHECK(hmi_mqtt_format_alarm_payload(payload, sizeof(payload), 5, 1, 1ULL, 0) == -1, "状态越界拒绝");
    CHECK(hmi_mqtt_format_alarm_payload(payload, sizeof(payload), 1, 4, 1ULL, 0) == -1, "severity 越界拒绝");
    CHECK(hmi_mqtt_format_alarm_topic(topic, sizeof(topic), "hmi", "a/b") == -1, "报警 ID 含 / 拒绝");
    CHECK(hmi_mqtt_format_alarm_topic(topic, sizeof(topic), "", "al1") == -1, "空前缀拒绝");

    /* 容量不足：一个字节都不写 */
    char small[8];
    memset(small, 'S', sizeof(small));
    small[sizeof(small) - 1] = '\0';
    CHECK(hmi_mqtt_format_alarm_topic(small, 8, "hmi", "al1") == -2, "报警主题容量不足 ⇒ -2");
    CHECK(small[0] == '\0', "容量不足时首字节清空");
}

static void test_mqtt_contract(void)
{
    char topic[192], payload[64];

    for (size_t i = 0; i < sizeof(k_contract_samples) / sizeof(k_contract_samples[0]); i++) {
        const contract_sample_t* s = &k_contract_samples[i];
        /* 主题：前缀由样例给（`a/b` 那条覆盖"前缀可含层级"） */
        CHECK(hmi_mqtt_format_value_topic(topic, sizeof(topic), s->prefix, s->name) == 0, s->topic);
        CHECK(strcmp(topic, s->topic) == 0, s->topic);

        CHECK(hmi_mqtt_format_value_payload(payload, sizeof(payload), s->value, s->t_ms,
                                            s->tb_utc, s->quality) == 0, s->payload);
        CHECK(strcmp(payload, s->payload) == 0, s->payload);
    }

    /* 非法参数：空前缀 / 空变量名 / 变量名含 `/` ⇒ -1，且 out 被清空 */
    memset(topic, 'X', sizeof(topic));
    topic[sizeof(topic) - 1] = '\0';
    CHECK(hmi_mqtt_format_value_topic(topic, sizeof(topic), "", "temp") == -1, "空前缀拒绝");
    CHECK(topic[0] == '\0', "拒绝时清空 out");
    CHECK(hmi_mqtt_format_value_topic(topic, sizeof(topic), "hmi", "") == -1, "空变量名拒绝");
    CHECK(hmi_mqtt_format_value_topic(topic, sizeof(topic), "hmi", "a/b") == -1, "变量名含 / 拒绝");
    CHECK(hmi_mqtt_format_value_payload(payload, sizeof(payload), 1.0, 1ULL, 0, 1) == 0, "正常 payload");
    CHECK(hmi_mqtt_format_value_payload(NULL, 8, 1.0, 1ULL, 0, 1) == -1, "out=NULL 拒绝");
    CHECK(hmi_mqtt_format_value_payload(payload, 0, 1.0, 1ULL, 0, 1) == -1, "cap=0 拒绝");

    /* 容量不足：-2，且**一个字节都不写**（哨兵法：前后各放哨兵） */
    char small[16];
    memset(small, 'S', sizeof(small));
    small[sizeof(small) - 1] = '\0';
    int rc = hmi_mqtt_format_value_topic(small, 16, "hmi", "long_variable_name_here");
    CHECK(rc == -2, "主题容量不足 ⇒ -2");
    CHECK(small[0] == '\0', "容量不足时首字节被清空");
    for (size_t i = 1; i < sizeof(small) - 1; i++)
        CHECK(small[i] == 'S', "容量不足时一个字节都不写");

    /* tb token 直判 */
    CHECK(strcmp(hmi_mqtt_time_base_token(0), "boot") == 0, "tb=boot");
    CHECK(strcmp(hmi_mqtt_time_base_token(1), "utc") == 0, "tb=utc");
}

/* ══════════════════════════════════════════════════════════════════
 * 远程写命令的**宿主接线**（阶段 289）：从"报文进来"到"变量真的被写"这一条线。
 *
 * 假宿主注入：`hmi_actions.c` 依赖 `lv_tick_get` 等宿主符号、不参与本步链接，
 * 所以这里给 `hmi_audit` / `hmi_current_role` / `hmi_has_permission` 三个假实现。
 * ⚠ 假 `hmi_has_permission` **不**复刻真实现的"未登录只含查看画面 / ENFORCE=0 恒真"
 *   两条语义（那两条由 hmi_actions_permission_test 覆盖）；接线只负责**问**它，
 *   所以这里给定掩码即可 —— 这正是"权限语义只有一处实现"的可判形状。
 * ══════════════════════════════════════════════════════════════════ */
static unsigned s_wire_perm_mask = 0u;
static int      s_wire_audit_count = 0;
static char     s_wire_audit_last[192];

int hmi_has_permission(unsigned int bits) { return (bits & s_wire_perm_mask) == bits; }
const char* hmi_current_role(void) { return "testrole"; }
void hmi_audit(const char* event, const char* user, const char* role, const char* detail)
{
    (void)user; (void)role;
    s_wire_audit_count++;
    snprintf(s_wire_audit_last, sizeof(s_wire_audit_last), "%s|%s",
             event != NULL ? event : "", detail != NULL ? detail : "");
}

static void wire_reset(unsigned mask)
{
    s_wire_perm_mask = mask;
    s_wire_audit_count = 0;
    s_wire_audit_last[0] = '\0';
    hmi_cmd_wiring_reset_ledger();   /* 每例独立：台账是幂等的，不重置会串味 */
}

/* 组一条命令载荷；ts/now 都由调用方给 ⇒ 新鲜度判据不睡觉 */
static void wire_payload(char* out, size_t cap, const char* id, unsigned long long ts,
                         const char* target, const char* value)
{
    snprintf(out, cap, "id=%s;ts=%llu;ttl=60000;by=alice;target=%s;value=%s",
             id, ts, target, value);
}

static void test_mqtt_cmd_wiring(void)
{
    const unsigned kOperate = HMI_PERM_OPERATE_CONTROL;
    const unsigned long long kNow = 1000500ull;
    hmi_cmd_result_t res;
    char topic[128], payload[192];

    printf("[远程写命令接线：/cmd/ → hmi_cmd_handle（阶段 289）]\n");

    /* 恢复出厂表（5 条：PV/SV/RUN 可写，ALARM/TEMP 只读），订阅随之重建 */
    hmi_var_init();
    CHECK(hmi_cmd_wiring_registered_count() == 3,
          "只为 3 个可写标签订阅 <pre>/cmd/<tag>（只读的 ALARM/TEMP 不订阅）");

    CHECK(hmi_cmd_wiring_is_cmd_topic("hmi/t113/cmd/SV") == 1, "识别本前缀的 cmd 主题");
    CHECK(hmi_cmd_wiring_is_cmd_topic("hmi/t113/set/SV") == 0, "set 主题不是 cmd");
    CHECK(hmi_cmd_wiring_is_cmd_topic("hmi/t113/value/SV") == 0, "value 主题不是 cmd");
    CHECK(hmi_cmd_wiring_is_cmd_topic("other/cmd/SV") == 0, "前缀不符 ⇒ 不是本前缀的 cmd");

    /* ① 成功：报文 → 变量缓存真的被写；审计一条（含旧值→新值） */
    wire_reset(kOperate);
    var_raw_write(TAG_SV, 7.5f);
    hmi_mqtt_format_cmd_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE, "SV");
    wire_payload(payload, sizeof(payload), "w1", kNow - 500, "SV", "42");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, kOperate, &res) == 0,
          "端到端：命令被接受");
    CHECK(res.wrote == 1 && res.code == HMI_CMD_OK, "确实调用了写");
    CHECK(var_raw_read(TAG_SV) == 42.0f, "变量缓存 = 42（原始值口径）");
    CHECK(res.old_value == 7.5 && res.new_value == 42.0, "旧值→新值 回填");
    CHECK(s_wire_audit_count == 1 && strncmp(s_wire_audit_last, "remote-write|", 13) == 0,
          "成功审计一条 remote-write（detail 含 old→new）");

    /* ② 量程**来自 tag_meta_t**（不是 C 侧另写一份口径）：PV 是 U16 ⇒ 0..65535，含端点 */
    wire_reset(kOperate);
    hmi_mqtt_format_cmd_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE, "PV");
    wire_payload(payload, sizeof(payload), "hi", kNow, "PV", "70000");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, kOperate, &res) == -1 &&
          strcmp(res.code, "out-of-range") == 0, "超上界 ⇒ out-of-range");
    wire_payload(payload, sizeof(payload), "lo", kNow, "PV", "-1");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, kOperate, &res) == -1 &&
          strcmp(res.code, "out-of-range") == 0, "低于下界 ⇒ out-of-range");
    wire_payload(payload, sizeof(payload), "max", kNow, "PV", "65535");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, kOperate, &res) == 0,
          "上界端点被接受（含端点）");
    wire_payload(payload, sizeof(payload), "min", kNow, "PV", "0");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, kOperate, &res) == 0,
          "下界端点被接受（含端点）");

    /* ③ Bool 的量程是 0..1（RUN）—— 同一张表、不同类型各推各的 */
    wire_reset(kOperate);
    hmi_mqtt_format_cmd_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE, "RUN");
    wire_payload(payload, sizeof(payload), "b2", kNow, "RUN", "2");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, kOperate, &res) == -1 &&
          strcmp(res.code, "out-of-range") == 0, "Bool 量程 0..1 ⇒ 2 被拒");

    /* ④ 只读标签（ALARM）⇒ read-only，且缓存一字不变 */
    wire_reset(kOperate);
    var_raw_write(TAG_ALARM, 5.0f);
    hmi_mqtt_format_cmd_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE, "ALARM");
    wire_payload(payload, sizeof(payload), "ro", kNow, "ALARM", "9");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, kOperate, &res) == -1 &&
          strcmp(res.code, "read-only") == 0, "只读 target ⇒ read-only");
    CHECK(var_raw_read(TAG_ALARM) == 5.0f, "被拒命令不改变量");

    /* ⑤ 主题 target 与载荷 target 不一致 ⇒ 拒
     *   （两个字段都能决定"写谁"：ACL 看主题、真正写的是载荷 ⇒ 必须一致） */
    wire_reset(kOperate);
    hmi_mqtt_format_cmd_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE, "SV");
    wire_payload(payload, sizeof(payload), "mm", kNow, "PV", "1");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, kOperate, &res) == -1 &&
          strcmp(res.code, HMI_CMD_DENIED_TARGET_MISMATCH) == 0, "target 不一致 ⇒ target-mismatch");
    CHECK(s_wire_audit_count == 1, "不一致也留一条痕");

    /* ⑥ 未授权 / 重复 / 过期 / 来自未来 / 未知 target / malformed */
    hmi_mqtt_format_cmd_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE, "SV");
    wire_reset(0u);
    wire_payload(payload, sizeof(payload), "noauth", kNow, "SV", "1");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, 0u, &res) == -1 &&
          strcmp(res.code, HMI_MQTT_ACL_FORBIDDEN) == 0, "无操作权 ⇒ forbidden");

    wire_reset(kOperate);
    wire_payload(payload, sizeof(payload), "dup", kNow, "SV", "1");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, kOperate, &res) == 0,
          "首次接受（进台账）");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow + 10, kOperate, &res) == -1 &&
          strcmp(res.code, "duplicate") == 0, "同 id 重放 ⇒ duplicate");

    wire_payload(payload, sizeof(payload), "old", kNow - 120000, "SV", "1");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, kOperate, &res) == -1 &&
          strcmp(res.code, "expired") == 0, "过期 ⇒ expired");
    wire_payload(payload, sizeof(payload), "fut", kNow + 600000, "SV", "1");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, kOperate, &res) == -1 &&
          strcmp(res.code, "from-future") == 0, "来自未来 ⇒ from-future");

    /* 未知 target：**主题与载荷必须指向同一个不存在的名字**（否则先撞 target-mismatch） */
    hmi_mqtt_format_cmd_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE, "NOSUCH");
    wire_payload(payload, sizeof(payload), "nf", kNow, "NOSUCH", "1");
    CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, kOperate, &res) == -1 &&
          strcmp(res.code, HMI_CMD_DENIED_NOT_FOUND) == 0, "未知 target ⇒ target-not-found");
    hmi_mqtt_format_cmd_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE, "SV");

    CHECK(hmi_cmd_wiring_handle(topic, "garbage", 7, kNow, kOperate, &res) == -1 &&
          strcmp(res.code, "malformed") == 0, "非信封载荷 ⇒ malformed");

    /* ⑦ 超长载荷：**不截断** ⇒ 必然 malformed（截断会让 target 指向另一个变量） */
    {
        static char big[600];
        memset(big, 'x', sizeof(big) - 1);
        big[sizeof(big) - 1] = '\0';
        CHECK(hmi_cmd_wiring_handle(topic, big, (int)strlen(big), kNow, kOperate, &res) == -1 &&
              strcmp(res.code, "malformed") == 0, "超长载荷 ⇒ malformed（不截断）");
    }

    /* ⑧ has_range=0（Float/未知）⇒ 不限量程；出厂表没有可写 Float，用自定义表验 */
    {
        static const tag_meta_t fmeta[1] = { { "FV", "40009", 0, 1.0f, 0.0f, 0, 0.0f, 0.0f } };
        static const tag_binding_t fbind[1] = { { "modbus-tcp", "40009", 1, "Float", 100 } };
        CHECK(hmi_tags_adopt(fmeta, fbind, 1) == 0, "采纳 1 条可写 Float 标签");
        wire_reset(kOperate);
        hmi_mqtt_format_cmd_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE, "FV");
        wire_payload(payload, sizeof(payload), "fl", kNow, "FV", "1e30");
        CHECK(hmi_cmd_wiring_handle(topic, payload, (int)strlen(payload), kNow, kOperate, &res) == 0,
              "has_range=0 ⇒ 不限量程（Float 大值也收）");
    }

    /* ⑨ 设备侧入口（真时钟 + 真掩码**来源**）：on_mqtt_message 把 /cmd/ 路由到接线；
     *   同时确认旧 /set/ 通道没被新路由吞掉 */
    hmi_var_init();
    wire_reset(kOperate);
    {
        unsigned long long now = hmi_cmd_wiring_now_ms();
        hmi_mqtt_format_cmd_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE, "SV");
        wire_payload(payload, sizeof(payload), "live", now, "SV", "11");
        on_mqtt_message(topic, payload, (int)strlen(payload), NULL);
        CHECK(var_raw_read(TAG_SV) == 11.0f, "on_mqtt_message：/cmd/ 走接线并写变量");

        var_raw_write(TAG_TEMP, 0.0f);
        on_mqtt_message("hmi/t113/set/TEMP", "3.5", 3, (void*)(intptr_t)TAG_TEMP);
        CHECK(var_raw_read(TAG_TEMP) == 3.5f, "/set/ 旧通道仍工作（未被 /cmd/ 路由吞掉）");
    }

    hmi_var_init();   /* 恢复出厂表，避免影响后续用例 */
}

/* ──────────────────────────────────────────────────────────────
 * TLS **运行期接线**（阶段 300）—— 拍板：PSK 与 X.509 都做、**永不降级**。
 *
 * 与 test_mqtt_tls_resolve_config 的分工：那边穷举**纯函数**自己的对错；
 * 这边判"设备**真的**按 mqtt.tls* 决定连哪里"，三层：
 *   ① 键解析（comm_config_load → s_cfg_tls_*）
 *   ② 决定（comm_tls_effective：可用 ⇒ 客户端地址；非法 ⇒ 拒绝 ＋ 原因）
 *   ③ 拒绝处置（comm_tls_refuse_notice：审计事件名与原因，**不含密钥材料**）
 * ────────────────────────────────────────────────────────────── */
static void tls_reset_cfg(void)
{
    s_cfg_tls_mode[0] = '\0';
    s_cfg_tls_local_port[0] = '\0';
    s_cfg_tls_psk_id[0] = '\0';
    s_cfg_tls_psk_file[0] = '\0';
    s_cfg_tls_ca_path[0] = '\0';
}

static void test_comm_tls_wiring(void)
{
    char host[64];
    unsigned port = 0;
    const char* reason = NULL;

    printf("[TLS 运行期接线]\n");

    /* ① 一个 TLS 键都没有 ⇒ off ⇒ 直连 broker（向后兼容，**不是降级**） */
    tls_reset_cfg();
    CHECK(comm_tls_effective("10.0.0.5", 1883, host, sizeof(host), &port, &reason) == 0, "无键=off");
    CHECK(strcmp(host, "10.0.0.5") == 0 && port == 1883, "off ⇒ 直连 broker");

    /* ② psk 完整 ⇒ 客户端只能连回环代理 */
    tls_reset_cfg();
    strcpy(s_cfg_tls_mode, "psk");
    strcpy(s_cfg_tls_psk_id, "hmi-01");
    strcpy(s_cfg_tls_psk_file, "/mnt/UDISK/hmi/psk.key");
    strcpy(s_cfg_tls_local_port, "11883");
    CHECK(comm_tls_effective("10.0.0.5", 1883, host, sizeof(host), &port, &reason) == 0, "psk 完整可用");
    CHECK(strcmp(host, "127.0.0.1") == 0 && port == 11883, "psk ⇒ 连回环 local_port");

    /* ③ ca 完整 ⇒ 同样连回环（local_port 缺省 1883） */
    tls_reset_cfg();
    strcpy(s_cfg_tls_mode, "ca");
    strcpy(s_cfg_tls_ca_path, "/mnt/UDISK/hmi/ca.pem");
    CHECK(comm_tls_effective("broker.lan", 8883, host, sizeof(host), &port, &reason) == 0, "ca 完整可用");
    CHECK(strcmp(host, "127.0.0.1") == 0 && port == 1883, "ca ⇒ 连回环缺省端口");

    /* ④ 缺件 ⇒ **拒绝**，且拿不到 broker 地址（fail-closed：漏判返回值也连不上） */
    tls_reset_cfg();
    strcpy(s_cfg_tls_mode, "psk");            /* 有模式、没 id/file */
    CHECK(comm_tls_effective("10.0.0.5", 1883, host, sizeof(host), &port, &reason) == -1, "psk 缺件必须拒绝");
    CHECK(host[0] == '\0' && port == 0, "拒绝时不得留下可连地址");
    CHECK(strcmp(reason, "tls-config-incomplete") == 0, "原因标识=缺件");

    /* ⑤ 模式串不认识 ⇒ 拒绝（**不许**当 off 直连 broker） */
    tls_reset_cfg();
    strcpy(s_cfg_tls_mode, "PSK");            /* 大小写敏感 */
    CHECK(comm_tls_effective("10.0.0.5", 1883, host, sizeof(host), &port, &reason) == -1, "未知模式必须拒绝");
    CHECK(host[0] == '\0' && port == 0, "未知模式时不得留下可连地址");
    CHECK(strcmp(reason, "tls-mode-unknown") == 0, "原因标识=模式未知");

    /* ⑥ 本地端口写错 ⇒ 拒绝 */
    tls_reset_cfg();
    strcpy(s_cfg_tls_mode, "ca");
    strcpy(s_cfg_tls_ca_path, "/mnt/UDISK/hmi/ca.pem");
    strcpy(s_cfg_tls_local_port, "0");
    CHECK(comm_tls_effective("broker.lan", 8883, host, sizeof(host), &port, &reason) == -1, "端口 0 必须拒绝");
    CHECK(strcmp(reason, "tls-endpoint-invalid") == 0, "原因标识=端点非法");

    /* ⑦ 拒绝处置：审计事件名 ＋ 原因；**不含密钥材料** */
    s_wire_audit_count = 0;
    s_wire_audit_last[0] = '\0';
    CHECK(comm_tls_refuse_notice("tls-config-incomplete") == -1, "拒绝通知恒返回 -1");
    CHECK(s_wire_audit_count == 1, "恰好审计一条");
    CHECK(strcmp(s_wire_audit_last, "mqtt-tls-config-refused|tls-config-incomplete") == 0,
          "审计 = 事件名|原因");
    CHECK(strstr(s_wire_audit_last, "psk.key") == NULL && strstr(s_wire_audit_last, "hmi-01") == NULL,
          "审计里不得出现密钥/身份材料");

    /* ⑧ 空出参：报错而不是写坏内存 */
    tls_reset_cfg();
    CHECK(comm_tls_effective("10.0.0.5", 1883, NULL, 0, &port, &reason) == -1, "空 host 出参");
}

/* ──────────────────────────────────────────────────────────────
 * 本机 TLS 终止代理的**配置契约**（阶段 301）—— 纯函数面。
 * 列序：mode, listen_port, upstream_host, upstream_port, psk_id, psk_file, ca_path,
 *       期望 status, 期望 listen, 期望 upstream, 期望 reason
 *
 * ⚠ 三档状态各有代表（退出码契约见 tls_proxy_config.h）：
 *   0 = 可用；**3 = 不需要代理**（`mqtt.tls` 缺键或 off ⇒ 脚本正常跳过，不是错误）；
 *   4 = 配置非法 ⇒ **拒绝启动**（绝不降级成直连 broker）。
 * ────────────────────────────────────────────────────────────── */
typedef struct {
    const char* mode;
    const char* listen_port;
    const char* upstream_host;
    const char* upstream_port;
    const char* psk_id;
    const char* psk_file;
    const char* ca_path;
    int         want_status;
    unsigned    want_listen;
    unsigned    want_upstream;
    const char* want_reason;
} tls_proxy_sample_t;

/* clang-format off */
static const tls_proxy_sample_t k_tls_proxy_samples[] = {
    /* mode,  listen,  upstream,     uport,  psk_id,   psk_file,                    ca_path,                   st, lport, uport, reason */
    {  NULL,  NULL,    "10.0.0.5",   NULL,   NULL,     NULL,                        NULL,                       3, 0,     0,     "tls-proxy-not-needed" },
    {  "",    NULL,    "10.0.0.5",   NULL,   NULL,     NULL,                        NULL,                       3, 0,     0,     "tls-proxy-not-needed" },
    {  "off", NULL,    "10.0.0.5",   NULL,   NULL,     NULL,                        NULL,                       3, 0,     0,     "tls-proxy-not-needed" },
    {  "psk", "11883", "10.0.0.5",   "8883", "hmi-01", "/mnt/UDISK/hmi/psk.key",    NULL,                       0, 11883, 8883,  "" },
    {  "psk", NULL,    "10.0.0.5",   NULL,   "hmi-01", "/mnt/UDISK/hmi/psk.key",    NULL,                       0, 1883,  8883,  "" },
    {  "ca",  "1884",  "broker.lan", "8884", NULL,     NULL,                        "/mnt/UDISK/hmi/ca.pem",    0, 1884,  8884,  "" },
    {  "psk", "11883", NULL,         "8883", "hmi-01", "/mnt/UDISK/hmi/psk.key",    NULL,                       4, 0,     0,     "tls-endpoint-invalid" },
    {  "psk", "11883", "10.0.0.5",   "0",    "hmi-01", "/mnt/UDISK/hmi/psk.key",    NULL,                       4, 0,     0,     "tls-endpoint-invalid" },
    {  "psk", "0",     "10.0.0.5",   "8883", "hmi-01", "/mnt/UDISK/hmi/psk.key",    NULL,                       4, 0,     0,     "tls-endpoint-invalid" },
    {  "psk", "11883", "10.0.0.5",   "8883", NULL,     NULL,                        NULL,                       4, 0,     0,     "tls-config-incomplete" },
    {  "ca",  "11883", "10.0.0.5",   "8883", NULL,     NULL,                        NULL,                       4, 0,     0,     "tls-config-incomplete" },
    {  "tls", "11883", "10.0.0.5",   "8883", NULL,     NULL,                        NULL,                       4, 0,     0,     "tls-mode-unknown" },
};
/* clang-format on */

static void test_tls_proxy_config(void)
{
    printf("[TLS 代理：配置契约]\n");

    for (size_t i = 0; i < sizeof(k_tls_proxy_samples) / sizeof(k_tls_proxy_samples[0]); i++) {
        const tls_proxy_sample_t* s = &k_tls_proxy_samples[i];
        hmi_tls_proxy_config_t c;

        int st = hmi_tls_proxy_resolve(s->mode, s->listen_port, s->upstream_host, s->upstream_port,
                                       s->psk_id, s->psk_file, s->ca_path, &c);

        CHECK(st == s->want_status, s->mode ? s->mode : "(缺键)");
        CHECK(c.status == s->want_status, "status 字段与返回值同向");
        CHECK(strcmp(c.reason, s->want_reason) == 0, s->want_reason);

        if (s->want_status == HMI_TLS_PROXY_EXIT_OK) {
            /* 监听地址**恒为回环** —— 没有任何键能改它（契约 §4：绑 0.0.0.0 更危险） */
            CHECK(strcmp(c.listen_host, "127.0.0.1") == 0, "监听恒为 127.0.0.1");
            CHECK(c.listen_port == s->want_listen, "listen_port");
            CHECK(c.upstream_port == s->want_upstream, "upstream_port");
            CHECK(strcmp(c.upstream_host, s->upstream_host) == 0, "upstream_host");
        } else {
            /* fail-closed：非 0 状态一律不留下可用端点 */
            CHECK(c.listen_host[0] == '\0' && c.listen_port == 0, "非 0 状态不留下监听端点");
            CHECK(c.upstream_host[0] == '\0' && c.upstream_port == 0, "非 0 状态不留下上游端点");
        }
    }

    CHECK(hmi_tls_proxy_resolve("psk", NULL, "10.0.0.5", NULL, "hmi-01", "/k", NULL, NULL) ==
              HMI_TLS_PROXY_EXIT_BAD_CONFIG,
          "空出参 ⇒ 拒绝");

    /* 文件读取腿：同一份 INI，代理与客户端各读各的键（契约 §1 的"没有交叠"） */
    {
        FILE* f = fopen("/tmp/hmi_tls_proxy_test.ini", "w");
        CHECK(f != NULL, "创建代理配置文件");
        if (f) {
            fprintf(f, "mqtt.host=127.0.0.1\nmqtt.port=11883\n");          /* 客户端读的键 */
            fprintf(f, "mqtt.tls=psk\nmqtt.tls.local_port=11883\n");
            fprintf(f, "mqtt.tls.upstream_host=10.0.0.5\nmqtt.tls.upstream_port=8883\n");
            fprintf(f, "mqtt.tls.psk_id=hmi-01\nmqtt.tls.psk_file=/mnt/UDISK/hmi/psk.key\n");
            fclose(f);
        }
        hmi_tls_proxy_config_t c;
        CHECK(hmi_tls_proxy_config_load("/tmp/hmi_tls_proxy_test.ini", &c) == HMI_TLS_PROXY_EXIT_OK,
              "INI 读取 ⇒ 可用");
        CHECK(strcmp(c.listen_host, "127.0.0.1") == 0 && c.listen_port == 11883, "监听 = 回环:11883");
        CHECK(strcmp(c.upstream_host, "10.0.0.5") == 0 && c.upstream_port == 8883, "上游 = broker:8883");
        CHECK(strcmp(c.psk_id, "hmi-01") == 0, "psk_id");
        remove("/tmp/hmi_tls_proxy_test.ini");

        /* 文件不存在 ⇒ "没有 TLS 键" ⇒ off ⇒ 不需要代理（不是错误） */
        hmi_tls_proxy_config_t c2;
        CHECK(hmi_tls_proxy_config_load("/tmp/definitely-not-here.ini", &c2) ==
                  HMI_TLS_PROXY_EXIT_NOT_NEEDED,
              "INI 不存在 ⇒ 不需要代理");
    }
}

int main(void)
{
    printf("=== zhishu-HMI T113 固件 主机侧单元测试 ===\n\n");
    test_crc16();
    test_rtu_frame();
    test_rtu_write_echo();
    test_tcp_mbap();
    test_addr_parse();
    test_comm_config();
    test_var_table();
    test_tags_adopt_failure_keeps_table();
    test_mqtt_packet_build();
    test_mqtt_will_and_subscription_persistence();
    test_mqtt_contract();
    test_mqtt_alarm_status_contract();
    test_mqtt_tls_contract();
    test_mqtt_cmd_contract();
    test_mqtt_acl_contract();
    test_mqtt_write_guard();
    test_mqtt_cmd_dispatch();
    test_mqtt_cmd_ledger();
    test_mqtt_cmd_wiring();
    test_mqtt_tls_endpoint();
    test_mqtt_tls_resolve_config();
    test_comm_tls_wiring();
    test_tls_proxy_config();
    printf("\n=== 结果: %d 通过, %d 失败 ===\n", g_pass, g_fail);
    return g_fail == 0 ? 0 : 1;
}
