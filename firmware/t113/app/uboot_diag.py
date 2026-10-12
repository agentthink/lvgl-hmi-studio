#!/usr/bin/env python3
"""
uboot_diag.py — U-Boot 启动失败诊断

查看 boot_normal / setargs_nand_ubi / boot_dsp0 环境变量 + UBI 卷列表
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


def cmd(s, data, wait=1.5):
    print(f"\n>>> {data.decode().strip()}")
    s.write(data + b"\n")
    time.sleep(wait)
    out = read_all_avail(s, 1.0)
    text = out.decode('ascii', errors='ignore')
    print(text)
    return text


def main():
    print(f"Connecting to {PORT} @ {BAUD}...")
    try:
        s = serial.Serial(PORT, BAUD, timeout=0.5)
    except PermissionError:
        print("ERROR: Permission denied. Run: sudo python3 uboot_diag.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    out = read_all_avail(s, 0.5)
    if '=>' not in out.decode('ascii', errors='ignore'):
        print("!! 不在 U-Boot 提示符, 当前:", out.decode('ascii', errors='ignore').strip())
        s.close()
        return
    print("[U-Boot 提示符 OK]")

    cmd(s, b"printenv boot_normal")
    cmd(s, b"printenv setargs_nand_ubi")
    cmd(s, b"printenv boot_dsp0")
    cmd(s, b"printenv bootdelay")
    cmd(s, b"ubi part sys")
    cmd(s, b"ubi list")

    s.close()
    print("\n=== 诊断完成 ===")


if __name__ == "__main__":
    main()
