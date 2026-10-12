#!/usr/bin/env python3
"""
uboot_diag3.py — 测试 UBI attach + 卷列表 + 手动 boot 流程
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


def cmd(s, data, wait=2.0):
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
        print("ERROR: Permission denied. Run: sudo python3 uboot_diag3.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    out = read_all_avail(s, 0.5)
    if '=>' not in out.decode('ascii', errors='ignore'):
        print("!! 不在 U-Boot 提示符:", out.decode('ascii', errors='ignore').strip())
        s.close()
        return
    print("[U-Boot 提示符 OK]")

    # 1. 加载默认 mtdparts
    cmd(s, b"mtdparts default")
    # 2. 列出 MTD
    cmd(s, b"mtd list")
    # 3. attach UBI (sys 分区)
    cmd(s, b"ubi part sys", wait=3.0)
    # 4. UBI 卷列表
    cmd(s, b"ubi list", wait=2.0)

    # 如果 attach 成功, 测试读 boot 卷
    print("\n>>> 测试: sunxi_flash read 41000000 boot")
    s.write(b"sunxi_flash read 41000000 boot\n")
    time.sleep(3)
    out = read_all_avail(s, 1.5)
    print(out.decode('ascii', errors='ignore'))

    s.close()
    print("\n=== 诊断完成 ===")
    print("若 ubi list 有 boot/rootfs 卷 → UBI 正常, 可手动 boot")
    print("若 attach 失败 → 烧录的 UBI 数据有问题, 需重新烧录")


if __name__ == "__main__":
    main()
