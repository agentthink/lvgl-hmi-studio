#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
zhishu-HMI WavePi-S6 RS485 测试用 Modbus RTU 从站（纯 pyserial 实现）

场景：WavePi 板子固件是 Modbus RTU **主站**（runtime_config.h: HMI_MBUS_RTU_BAUD=9600,
8N1, 走 ttyS4），会主动 poll 主机。本脚本在主机侧起一个 **Modbus 从站**，应答板子的
读/写请求，从而验证「PL2303 + TTL→RS485 自动方向模块 + WavePi ttyS4」全链路是否通。

方向控制：使用**自动方向**模块（TX 边沿自动切收发），故软件只管 write/read，
不碰 RTS/DTR。PL2303 驱动的 TIOCSRS485 不支持，正好也不需要。

依赖：pyserial（已装 3.5）

用法：
  1) 主机侧回环自测（把模块 A、B 短接，验证 TX→模块→A/B→模块→RX 通路与应答逻辑）：
     python3 modbus_rtu_slave.py --port /dev/ttyUSB0 --loopback-test

  2) 起从站应答板子主站（A/B 接到 WavePi RS485 排针，GND 共地）：
     python3 modbus_rtu_slave.py --port /dev/ttyUSB0 --baud 9600

寄存器/线圈映射（对齐 modbus_tcp_slave.py）：
  HR: 0=40001(PV,每秒+1) 1=40002(SV,可写) 2=40003(状态) 3=40004(产量)
  CO: 0..8
  异常返回按 Modbus 规范（功能码|0x80 + 异常码 0x02/0x03/0x06 等）。
"""
import argparse
import sys
import time
import threading
import serial
from serial.serialutil import SerialException


# ── Modbus RTU 帧定时（3.5 字符静默） ──
def char_time_us(baud, bytesize=8, parity='N', stopbits=1):
    bits = bytesize + (1 if parity in ('N', None) else 2) + stopbits + 1  # 起始位
    return bits * 1_000_000.0 / baud


def inter_frame_gap_s(baud, **kw):
    # 3.5 字符静默时间（秒）
    return char_time_us(baud, **kw) * 3.5 / 1_000_000.0


# ── CRC16 (Modbus, poly 0xA001, init 0xFFFF, 低字节在前) ──
def crc16(data: bytes) -> int:
    crc = 0xFFFF
    for b in data:
        crc ^= b
        for _ in range(8):
            if crc & 0x0001:
                crc = (crc >> 1) ^ 0xA001
            else:
                crc >>= 1
    return crc


def frame_ok(buf: bytes) -> bool:
    if len(buf) < 5:
        return False
    body, crc_lo, crc_hi = buf[:-2], buf[-2], buf[-1]
    crc = crc16(body)
    return ((crc & 0xFF) == crc_lo) and ((crc >> 8) == crc_hi)


def build_frame(unit: int, fn: int, pdu: bytes) -> bytes:
    body = bytes([unit, fn]) + pdu
    crc = crc16(body)
    return body + bytes([crc & 0xFF, crc >> 8])


# ── 从站数据区 ──
class SlaveData:
    def __init__(self):
        self.hr = {0: 0, 1: 100, 2: 0x0001, 3: 0}      # 保持寄存器
        self.ir = {i: 0 for i in range(0, 9)}          # 输入寄存器
        self.co = {i: False for i in range(0, 9)}      # 线圈
        self.di = {i: False for i in range(0, 9)}      # 离散输入
        self._pv = 0
        self.lock = threading.Lock()

    def pv_tick(self):
        """每秒 PV+1，模拟传感器"""
        with self.lock:
            self._pv = (self._pv + 1) % 1000
            self.hr[0] = self._pv

    def read_regs(self, table, addr, count):
        with self.lock:
            vals = [table.get(addr + i, 0) if table is (self.hr) or table is (self.ir)
                    else (1 if table.get(addr + i, False) else 0)
                    for i in range(count)]
        return vals


# ── 从站请求处理：返回应答帧 bytes，或 None（非法帧不回） ──
def handle_request(buf: bytes, data: SlaveData, unit: int) -> bytes:
    if len(buf) < 5:
        return None
    if buf[0] != unit:
        return None  # 不是问本从站
    if not frame_ok(buf):
        return None
    fn = buf[1]
    try:
        if fn == 0x03:        # 读保持寄存器
            addr = (buf[2] << 8) | buf[3]
            cnt = (buf[4] << 8) | buf[5]
            if cnt < 1 or cnt > 125:
                return build_frame(unit, fn | 0x80, bytes([0x03]))
            with data.lock:
                regs = [data.hr.get(addr + i, 0) for i in range(cnt)]
            pdu = bytes([cnt * 2]) + b''.join(r.to_bytes(2, 'big') for r in regs)
            return build_frame(unit, fn, pdu)
        elif fn == 0x04:      # 读输入寄存器
            addr = (buf[2] << 8) | buf[3]
            cnt = (buf[4] << 8) | buf[5]
            if cnt < 1 or cnt > 125:
                return build_frame(unit, fn | 0x80, bytes([0x03]))
            with data.lock:
                regs = [data.ir.get(addr + i, 0) for i in range(cnt)]
            pdu = bytes([cnt * 2]) + b''.join(r.to_bytes(2, 'big') for r in regs)
            return build_frame(unit, fn, pdu)
        elif fn == 0x01:      # 读线圈
            addr = (buf[2] << 8) | buf[3]
            cnt = (buf[4] << 8) | buf[5]
            if cnt < 1 or cnt > 2000:
                return build_frame(unit, fn | 0x80, bytes([0x03]))
            with data.lock:
                bits = [1 if data.co.get(addr + i, False) else 0 for i in range(cnt)]
            nbytes = (cnt + 7) // 8
            out = bytearray(nbytes)
            for i, b in enumerate(bits):
                if b:
                    out[i // 8] |= (1 << (i % 8))
            return build_frame(unit, fn, bytes([nbytes]) + bytes(out))
        elif fn == 0x02:      # 读离散输入
            addr = (buf[2] << 8) | buf[3]
            cnt = (buf[4] << 8) | buf[5]
            if cnt < 1 or cnt > 2000:
                return build_frame(unit, fn | 0x80, bytes([0x03]))
            with data.lock:
                bits = [1 if data.di.get(addr + i, False) else 0 for i in range(cnt)]
            nbytes = (cnt + 7) // 8
            out = bytearray(nbytes)
            for i, b in enumerate(bits):
                if b:
                    out[i // 8] |= (1 << (i % 8))
            return build_frame(unit, fn, bytes([nbytes]) + bytes(out))
        elif fn == 0x06:      # 写单个寄存器
            addr = (buf[2] << 8) | buf[3]
            val = (buf[4] << 8) | buf[5]
            with data.lock:
                data.hr[addr] = val
            return build_frame(unit, fn, buf[2:6])
        elif fn == 0x10:      # 写多个寄存器
            addr = (buf[2] << 8) | buf[3]
            cnt = (buf[4] << 8) | buf[5]
            with data.lock:
                for i in range(cnt):
                    off = 7 + i * 2
                    data.hr[addr + i] = (buf[off] << 8) | buf[off + 1]
            return build_frame(unit, fn, buf[2:6])
        elif fn == 0x05:      # 写单个线圈
            addr = (buf[2] << 8) | buf[3]
            val = (buf[4] << 8) | buf[5]
            with data.lock:
                data.co[addr] = (val == 0xFF00)
            return build_frame(unit, fn, buf[2:6])
        elif fn == 0x0F:      # 写多个线圈
            addr = (buf[2] << 8) | buf[3]
            cnt = (buf[4] << 8) | buf[5]
            nbytes = buf[6]
            bits = []
            for i in range(cnt):
                byte_i = 7 + i // 8
                bits.append(1 if (buf[byte_i] & (1 << (i % 8))) else 0)
            with data.lock:
                for i, b in enumerate(bits):
                    data.co[addr + i] = bool(b)
            return build_frame(unit, fn, buf[2:6])
        else:
            return build_frame(unit, fn | 0x80, bytes([0x01]))  # 非法功能
    except Exception as e:
        return build_frame(unit, fn | 0x80, bytes([0x06]))      # 从站故障


# ── 从站运行循环 ──
class RtuSlave:
    def __init__(self, port, baud, unit=1, parity='N'):
        self.ser = serial.Serial(port, baud, bytesize=8, parity=parity,
                                 stopbits=1, timeout=0.02)
        self.unit = unit
        self.data = SlaveData()
        self.gap = inter_frame_gap_s(baud)
        # 启动 PV 自增线程
        self._stop = threading.Event()
        threading.Thread(target=self._pv_loop, daemon=True).start()

    def _pv_loop(self):
        while not self._stop.is_set():
            self.data.pv_tick()
            time.sleep(1)

    def run(self):
        print(f"[slave] 监听 {self.ser.port} {self.ser.baudrate}/8{self.ser.parity}1 "
              f"unit={self.unit} (自动方向模块，纯 write/read)")
        print(f"[slave] 寄存器: HR[0]={self.data.hr[0]} HR[1]={self.data.hr[1]} "
              f"HR[2]={self.data.hr[2]} HR[3]={self.data.hr[3]}")
        print("[slave] 等待板子主站 poll ... (Ctrl+C 退出)\n")
        buf = bytearray()
        last_t = time.time()
        try:
            while True:
                n = self.ser.in_waiting
                if n:
                    buf += self.ser.read(n)
                    last_t = time.time()
                elif buf and (time.time() - last_t) >= self.gap:
                    # 一帧收完
                    if frame_ok(bytes(buf)):
                        req = bytes(buf)
                        print(f"[RX] {' '.join(f'{b:02X}' for b in req)}")
                        resp = handle_request(req, self.data, self.unit)
                        if resp:
                            self.ser.write(resp)
                            print(f"[TX] {' '.join(f'{b:02X}' for b in resp)}")
                    buf = bytearray()
                time.sleep(0.001)
        except KeyboardInterrupt:
            print("\n[slave] 退出")
        finally:
            self._stop.set()
            self.ser.close()


# ── 主机回环自测（A/B 短接） ──
def loopback_test(port, baud, unit=1, parity='N'):
    print(f"[loopback] 打开 {port} {baud}/8{parity}1，进行 A/B 短接回环测试...")
    data = SlaveData()
    ser = serial.Serial(port, baud, bytesize=8, parity=parity, stopbits=1, timeout=0.05)
    gap = inter_frame_gap_s(baud)

    # 1) 纯电气回环：发一串字节，应原样收回
    test_bytes = bytes([0x01, 0x03, 0x00, 0x00, 0x00, 0x02]) + \
        crc16(bytes([0x01, 0x03, 0x00, 0x00, 0x00, 0x02])).to_bytes(2, 'little')
    ser.reset_input_buffer()
    ser.write(test_bytes)
    time.sleep(gap * 3 + 0.05)
    echo = ser.read(ser.in_waiting or 1)
    if echo == test_bytes:
        print(f"[loopback] ✓ 电气回环通过：发出 {test_bytes.hex()} 收回一致（自动方向模块工作正常）")
    else:
        print(f"[loopback] ✗ 电气回环失败：发出 {test_bytes.hex()}")
        print(f"          收回 {echo.hex() if echo else '<空>'} (检查 A/B 是否短接、模块供电/方向)")
        ser.close()
        return False

    # 2) 应答逻辑自测：构造请求→经模块回环→从站解析→应答→经模块回环→收到
    cases = [
        ("FC03 读 HR[0..1]", build_frame(unit, 0x03, bytes([0x00, 0x00, 0x00, 0x02]))),
        ("FC06 写 HR[1]=1234", build_frame(unit, 0x06, bytes([0x00, 0x01, 0x04, 0xD2]))),
        ("FC16 写 HR[2..3]", build_frame(unit, 0x10, bytes([0x00, 0x02, 0x00, 0x02, 0x04, 0x00, 0x0A, 0x00, 0x0B]))),
        ("FC01 读线圈[0..3]", build_frame(unit, 0x01, bytes([0x00, 0x00, 0x00, 0x04]))),
    ]
    ok = True
    for name, req in cases:
        ser.reset_input_buffer()
        ser.write(req)
        time.sleep(gap * 3 + 0.05)
        raw = ser.read(ser.in_waiting or 1)
        # raw 里可能混有回环的 req 回声 + 应答；应答帧以 unit+fn 开头且是 FC 非 0x80
        resp = None
        # 找完整应答：从 raw 中切出以 (unit, fn) 开头且 CRC 正确的子帧
        for i in range(0, max(0, len(raw) - 4)):
            if raw[i] == unit and raw[i + 1] == req[1] and frame_ok(raw[i:]):
                resp = raw[i:]
                break
        if resp:
            print(f"[loopback] ✓ {name}: 应答 {' '.join(f'{b:02X}' for b in resp)}")
        else:
            print(f"[loopback] ✗ {name}: 无应答 (raw={raw.hex()})")
            ok = False
    ser.close()
    print(f"\n[loopback] 结论: {'全部通过 ✅' if ok else '存在失败 ❌'}")
    return ok


def main():
    ap = argparse.ArgumentParser(description="WavePi RS485 Modbus RTU 测试从站")
    ap.add_argument("--port", default="/dev/ttyUSB0")
    ap.add_argument("--baud", type=int, default=9600)
    ap.add_argument("--unit", type=int, default=1, help="从站站号")
    ap.add_argument("--parity", default="N", choices=["N", "E", "O"])
    ap.add_argument("--loopback-test", action="store_true", help="A/B 短接回环自测")
    args = ap.parse_args()

    if args.loopback_test:
        loopback_test(args.port, args.baud, args.unit, args.parity)
    else:
        RtuSlave(args.port, args.baud, args.unit, args.parity).run()


if __name__ == "__main__":
    main()
