#!/usr/bin/env python3
"""
uboot_fix_bootcmd.py — U-Boot 下修改 bootcmd (去掉不存在的 dsp0) + saveenv + 启动

bootcmd 新值:
  sleep ${bootdelay}; mtdparts default; ubi part sys; run setargs_nand_ubi; run boot_normal

(去掉 boot_dsp0, 因为分区表里没有 dsp0 卷, 它失败导致 boot_normal 不执行)
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
        print("ERROR: Permission denied. Run: sudo python3 uboot_fix_bootcmd.py")
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

    # 1. 设置新 bootcmd (去掉 boot_dsp0)
    cmd(s, b"setenv bootcmd 'sleep ${bootdelay}; mtdparts default; ubi part sys; run setargs_nand_ubi; run boot_normal'", wait=1.5)

    # 2. saveenv
    cmd(s, b"saveenv", wait=2.0)

    # 3. 验证
    cmd(s, b"printenv bootcmd")

    # 4. boot 启动 + 监控
    print("\n>>> boot 启动, 监控 60s...")
    s.write(b"boot\n")
    time.sleep(3)
    out = read_all_avail(s, 1.5)
    print(out.decode('ascii', errors='ignore'))
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
        print("\n!!! 60s 内未启动")

    s.close()
    print("\n=== 完成 ===")
    print("若启动成功, bootcmd 已固化 (saveenv), 下次开机自动启动")


if __name__ == "__main__":
    main()
