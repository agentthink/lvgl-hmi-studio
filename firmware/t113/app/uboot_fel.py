#!/usr/bin/env python3
"""
uboot_fel.py — 从 U-Boot 提示符进入 FEL 模式 (重新烧录准备)
"""
import serial, time, sys

PORT = "/dev/ttyUSB0"
BAUD = 115200


def read_all_avail(s, idle=0.5):
    out = b""
    last_new = time.time()
    while time.time() - last_new < idle:
        n = s.in_waiting
        if n:
            out += s.read(n)
            last_new = time.time()
        else:
            time.sleep(0.05)
    return out


def main():
    print(f"Connecting to {PORT} @ {BAUD}...")
    try:
        s = serial.Serial(PORT, BAUD, timeout=0.5)
    except PermissionError:
        print("ERROR: Permission denied. Run: sudo python3 uboot_fel.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    out = read_all_avail(s, 0.5)
    print("提示符:", out.decode('ascii', errors='ignore').strip())

    # 尝试 fel 命令
    print("\n发送: fel")
    s.write(b"fel\n")
    time.sleep(2)
    out = read_all_avail(s, 1.0)
    print("输出:", out.decode('ascii', errors='ignore').strip())

    # 如果 fel 不存在, 尝试 reset + fel 键? 或 sunxi 其他命令
    if b"Unknown command" in out or b"## Error" in out:
        print("\nfel 命令不可用, 尝试: run felboot")
        s.write(b"run felboot\n")
        time.sleep(2)
        out = read_all_avail(s, 1.0)
        print("输出:", out.decode('ascii', errors='ignore').strip())
        if b"Unknown command" in out or b"## Error" in out:
            print("\n尝试: reset")
            s.write(b"reset\n")
            time.sleep(2)
            out = read_all_avail(s, 1.0)
            print("输出:", out.decode('ascii', errors='ignore').strip())
            print("\n>>> 如果板子重启, 请在上电后立即按住板上的 FEL 键(或按下 UART 附近的 FEL 按钮) <<<")
            print(">>> 或者: 断电 → 按住 FEL 键 → 上电 → 松开 FEL 键 <<<")

    s.close()
    print("\n=== 完成 ===\n请运行: sudo /tmp/openixcli_extract/openixcli scan 确认 FEL 设备")


if __name__ == "__main__":
    main()
