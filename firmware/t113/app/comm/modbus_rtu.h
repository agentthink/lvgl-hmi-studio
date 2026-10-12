/*
 * modbus_rtu.h — Modbus RTU 驱动（RS485）
 *
 * 目标板：t113-mq_r = UART3 /dev/ttyS3（硬件 RS485）；
 *         WavePI-S6 = UART4 /dev/ttyS4（SP485，GPIO 方向控制，见 runtime_config.h）
 *
 * 协议：GB/T 19582 / Modbus Serial Line V1.02
 *   - RTU 帧：unit + fn + pdu + CRC16(Modbus, poly 0xA001)
 *   - 半双工：GPIO 方向控制（WavePI）或 TIOCSRS485/硬件 RS485（mq_r 及跨平台）
 *   - 线程安全：全部请求由互斥锁串行化
 */
#ifndef HMI_MODBUS_RTU_H
#define HMI_MODBUS_RTU_H

#include <stdint.h>
#include <stddef.h>

/* 纯协议函数（可独立于设备做主机侧单元测试） */

/* Modbus CRC16（poly 0xA001, init 0xFFFF），结果低字节在前 */
uint16_t mb_rtu_crc16(const uint8_t* data, size_t len);

/* 构建 RTU 帧：unit + fn + payload + CRC。成功返回帧长，失败返回 -1 */
int mb_rtu_build_frame(uint8_t unit, uint8_t fn, const uint8_t* payload,
                       size_t plen, uint8_t* frame, size_t fcap);

/*
 * 解析并校验 RTU 响应帧：
 *   - 校验 unit/fn 回显（fn 高位带 0x80 时视为异常，返回 -2 并在 *excode 输出异常码）
 *   - 校验 CRC
 * 成功返回 0；帧长不足/校验失败返回 -1。
 */
int mb_rtu_parse_frame(const uint8_t* frame, size_t len, uint8_t expect_unit,
                       uint8_t expect_fn, uint8_t* pdu, size_t* pdulen,
                       uint8_t* excode);

/*
 * 读响应 PDU 的字节数校验：期望字节数 = 2×寄存器数（位读 = ceil(n/8)）。
 * `mb_rtu_parse_frame` 只按响应自称的 bc 校验 CRC；调用方必须再核一次，
 * 否则「bc 偏小但 CRC 合法」的响应会让按 count 的索引读到未初始化字节。
 * 返回 0 = 相符。
 */
int mb_rtu_check_read_pdu(size_t pdulen, size_t expected_bytes);

/* 驱动接口 */
int mb_rtu_init(const char* dev, int baud, char parity, int timeout_ms);
void mb_rtu_deinit(void);

/* fn: 1/2 读线圈/离散输入（out[i] = 0/1）；3/4 读寄存器（out[i] = 16bit 值） */
int mb_rtu_read_regs(uint8_t unit, uint8_t fn, uint16_t off, uint16_t count, uint16_t* out);
/* fn: 5 写单线圈 / 6 写单寄存器 */
int mb_rtu_write_reg(uint8_t unit, uint8_t fn, uint16_t off, uint16_t val);
/* fn: 16 写多寄存器 */
int mb_rtu_write_regs(uint8_t unit, uint16_t off, const uint16_t* regs, uint16_t count);

#endif /* HMI_MODBUS_RTU_H */
