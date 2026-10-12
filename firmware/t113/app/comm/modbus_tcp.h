/*
 * modbus_tcp.h — Modbus TCP 驱动（eth0:502）
 *
 * 协议：GB/T 19582 / Modbus Messaging Implementation Guide V1.0b
 *   - MBAP：TransactionId(2) + ProtocolId(2)=0 + Length(2) + UnitId(1)
 *   - 大端序，寄存器 16 位
 * 设计要点（对齐项目 Hmi.Communication.Builtin/ModbusTcpRuntimeDriver）：
 *   - TransactionId 由互斥锁保护递增，响应必须回显相同 TID，防止帧交错
 *   - 断线自动重连（hmi_comm_poll 中调用 mb_tcp_poll）
 */
#ifndef HMI_MODBUS_TCP_H
#define HMI_MODBUS_TCP_H

#include <stdint.h>
#include <stddef.h>

/* 纯协议函数（可独立做主机侧单元测试） */

/* 构建 MBAP 报文：tid + pid(0) + len + unit + fn + pdu。成功返回总长 */
size_t mb_tcp_build_mbap(uint16_t tid, uint8_t unit, uint8_t fn,
                         const uint8_t* pdu, size_t pdulen, uint8_t* out, size_t cap);

/*
 * 解析并校验 MBAP 响应：
 *   - 校验 ProtocolId==0、Length 一致、UnitId 回显、TransactionId == expect_tid
 *   - fn 高位 0x80 视为异常，返回 -2 并输出异常码
 * 成功返回 0；失败返回 -1。
 */
int mb_tcp_parse_mbap(const uint8_t* buf, size_t len, uint16_t expect_tid,
                      uint8_t expect_unit, uint8_t* pdu, size_t* pdulen,
                      uint8_t* excode);

/* 驱动接口 */
int mb_tcp_init(const char* host, int port, int timeout_ms);
int mb_tcp_poll(void);   /* 断线重连（每轮询周期调用） */
int mb_tcp_is_connected(void);

int mb_tcp_read_regs(uint8_t unit, uint8_t fn, uint16_t off, uint16_t count, uint16_t* out);
int mb_tcp_write_reg(uint8_t unit, uint8_t fn, uint16_t off, uint16_t val);
int mb_tcp_write_regs(uint8_t unit, uint16_t off, const uint16_t* regs, uint16_t count);

#endif /* HMI_MODBUS_TCP_H */
