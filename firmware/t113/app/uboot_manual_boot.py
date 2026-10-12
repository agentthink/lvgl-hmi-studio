#!/usr/bin/env python3
"""
uboot_manual_boot.py — 手动执行完整启动流程 (验证 UBI attach 后能否 boot Linux)

流程:
1. run setargs_nand_ubi (设置 bootargs)
2. ubi part sys (attach UBI)
3. sunxi_flash read 41000000 boot (读 kernel)
4. bootm 41000000 (启动 kernel)
5. 监控 60s 等 Linux shell
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
        print("ERROR: Permission denied. Run: sudo python3 uboot_manual_boot.py")
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

    cmd(s, b"mtdparts default")
    cmd(s, b"run setargs_nand_ubi")
    cmd(s, b"ubi part sys", wait=3.0)
    cmd(s, b"sunxi_flash read 41000000 boot", wait=3.0)
    cmd(s, b"bootm 41000000", wait=3.0)

    # 监控 60s 等 Linux
    print("\n=== 监控 Linux 启动 (60s) ===")
    start = time.time()
    booted = False
    while time.time() - start < 60:
        out = read_all_avail(s, 0.5)
        if out:
            text = out.decode('ascii', errors='ignore')
            print(text, end='', flush=True)
            if 'root@TinaLinux' in text or 'login:' in text.lower():
                booted = True
                print("\n>>> Linux 启动成功! <<<")
                break
    if not booted:
        print("\n!!! 60s 内未启动, 请检查上面的 kernel 日志")
    s.close()
    print("\n=== 完成 ===")


if __name__ == "__main__":
    main()
