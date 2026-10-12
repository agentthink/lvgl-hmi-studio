#!/usr/bin/env python3
"""
uboot_diag2.py — 打印关键启动环境变量 + 测试 boot 分区读取
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
        print("ERROR: Permission denied. Run: sudo python3 uboot_diag2.py")
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

    # 启动相关变量
    for var in ["boot_partition", "dsp0_partition", "root_partition",
                "nand_root", "rootfstype", "mtd_name", "mtdparts",
                "partition", "kernel_partition", "bootargs"]:
        cmd(s, f"printenv {var}".encode())

    # 分区表
    cmd(s, b"mtdparts")

    # 尝试从 boot 分区读 kernel
    print("\n>>> 测试: sunxi_flash read 41000000 boot (boot_partition)")
    s.write(b"sunxi_flash read 41000000 boot\n")
    time.sleep(2)
    out = read_all_avail(s, 1.5)
    print(out.decode('ascii', errors='ignore'))

    s.close()
    print("\n=== 诊断完成 ===")


if __name__ == "__main__":
    main()
