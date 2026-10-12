#!/usr/bin/env python3
"""
raw_probe.py — 原始字节级串口探测 (v1)

用 repr 显示真实收发字节, 判断:
- 整行发送 vs 逐字符发送 哪个可靠
- 板子是否真的执行命令并返回输出
"""
import serial, time, sys

PORT = "/dev/ttyUSB0"
BAUD = 115200


def read_all_avail(s, wait=0.2):
    """读取当前所有可用数据 (最多 wait 秒)"""
    time.sleep(wait)
    out = b""
    deadline = time.time() + wait
    while time.time() < deadline:
        n = s.in_waiting
        if n:
            out += s.read(n)
        else:
            time.sleep(0.05)
    return out


def probe(s, label, data, wait):
    print(f"\n--- {label} ---")
    print(f"SEND: {data!r}")
    s.write(data)
    time.sleep(0.5)
    out = read_all_avail(s, wait)
    print(f"RECV ({len(out)} bytes):")
    print(f"  {out!r}")


def main():
    print(f"Connecting to {PORT} @ {BAUD}...")
    try:
        s = serial.Serial(PORT, BAUD, timeout=0.5)
    except PermissionError:
        print("ERROR: Permission denied. Run: sudo python3 raw_probe.py")
        sys.exit(1)

    # 清空缓冲
    s.read_all()
    time.sleep(0.5)
    s.read_all()

    # 0. 回车唤醒
    probe(s, "0. wake (Enter)", b"\n", 0.5)

    # 1. 简单 echo (整行发送)
    probe(s, "1. echo ABC 整行", b"echo ABC\n", 1.5)

    # 2. echo 带 marker (整行)
    probe(s, "2. echo END 整行", b"echo END==\n", 1.5)

    # 3. ls / 整行
    probe(s, "3. ls / 整行", b"ls /\n", 2.0)

    # 4. cat /proc/interrupts 整行 (大输出)
    probe(s, "4. cat /proc/interrupts 整行", b"cat /proc/interrupts\n", 3.0)

    # 5. 简单 echo 逐字符
    print("\n--- 5. echo XYZ 逐字符 ---")
    print(f"SEND: {b'echo XYZ\\n'!r} (逐字符)")
    for ch in b"echo XYZ\n":
        s.write(bytes([ch]))
        time.sleep(0.02)
    out = read_all_avail(s, 1.5)
    print(f"RECV ({len(out)} bytes):")
    print(f"  {out!r}")

    # 6. cat /proc/modules 整行
    probe(s, "6. cat /proc/modules 整行", b"cat /proc/modules\n", 2.0)

    s.close()
    print("\n=== raw_probe 完成 ===")
    print("判读: 若 1-4 有输出说明整行发送 OK; 若都无输出则串口/终端状态异常")


if __name__ == "__main__":
    main()
