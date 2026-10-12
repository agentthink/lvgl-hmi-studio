#!/usr/bin/env python3
"""
uboot_boot.py — 板子在 U-Boot 提示符时, 手动触发启动 Linux

流程:
1. 检测 U-Boot 提示符
2. 发送 boot 启动
3. 监控启动日志 (等 shell 或失败信息)
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
        print("ERROR: Permission denied. Run: sudo python3 uboot_boot.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()

    # 1. 发回车确认提示符
    s.write(b"\n")
    time.sleep(0.5)
    out = read_all_avail(s, 0.5)
    print("提示符状态:", out.decode('ascii', errors='ignore').strip())

    # 2. 查看 bootcmd 环境变量
    s.write(b"printenv bootcmd\n")
    time.sleep(1)
    out = read_all_avail(s, 1.0)
    print("=== bootcmd ===")
    print(out.decode('ascii', errors='ignore'))

    # 3. 发送 boot 启动
    print("\n=== 发送 boot ===")
    s.write(b"boot\n")
    time.sleep(2)
    out = read_all_avail(s, 1.0)
    print(out.decode('ascii', errors='ignore'))

    # 4. 监控启动 40 秒, 等 shell 或错误
    print("\n=== 监控启动 (40s) ===")
    start = time.time()
    while time.time() - start < 40:
        out = read_all_avail(s, 0.5)
        if out:
            text = out.decode('ascii', errors='ignore')
            print(text, end='', flush=True)
            if 'root@TinaLinux' in text or 'login:' in text.lower():
                print("\n>>> Shell 出现! 启动成功 <<<")
                break
    s.close()
    print("\n=== 完成 ===")


if __name__ == "__main__":
    main()
