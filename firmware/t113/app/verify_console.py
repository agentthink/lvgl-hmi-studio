#!/usr/bin/env python3
"""
verify_console.py — 按 Enter 激活 console, 验证触摸完整链路

1. 发送 Enter 激活 shell
2. /dev/input/ (event2 真实节点)
3. hmi_runtime 进程 + fd
4. 触摸测试 (dd)
"""
import serial, time, sys

PORT = "/dev/ttyUSB0"
BAUD = 115200
DD_SECONDS = 8


def read_all_avail(s, idle=0.4):
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


def send_line(s, data, wait=2.0):
    s.write(data)
    time.sleep(0.3)
    return read_all_avail(s, wait)


def show(label, out):
    print(f"\n========== {label} ==========")
    print(out.decode('ascii', errors='ignore'))


def main():
    print(f"Connecting to {PORT} @ {BAUD}...")
    try:
        s = serial.Serial(PORT, BAUD, timeout=0.5)
    except PermissionError:
        print("ERROR: Permission denied. Run: sudo python3 verify_console.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()

    # 1. 按 Enter 激活 console
    print(">>> 按 Enter 激活 console...")
    s.write(b"\n")
    time.sleep(1.0)
    out = read_all_avail(s, 1.0)
    print(out.decode('ascii', errors='ignore'))
    if b'root@TinaLinux' not in out:
        # 再试一次
        s.write(b"\n")
        time.sleep(1.0)
        out = read_all_avail(s, 1.0)
        print(out.decode('ascii', errors='ignore'))
    if b'root@TinaLinux' not in out:
        print("!! shell 未就绪")
        s.close()
        return
    print("[shell OK]")

    # 2. /dev/input/
    out = send_line(s, b"ls -la /dev/input/\n", 1.0)
    show("2. /dev/input/ (event2 应为 c 13:66)", out)

    # 3. hmi_runtime
    out = send_line(s, b"pidof hmi_runtime; ls -la /proc/$(pidof hmi_runtime)/fd/ 2>&1 | grep -E 'event|input'; ls -la /etc/init.d/S97hmiruntime 2>&1\n", 1.5)
    show("3. hmi_runtime 进程/fd/脚本", out)

    # 4. 触摸测试
    print("\n" + "=" * 62)
    print(f">>> 触摸测试: 请连续触摸屏幕 {DD_SECONDS} 秒 <<<")
    print("=" * 62)
    s.write(b"dd if=/dev/input/event2 of=/tmp/t.dat bs=24 count=5 2>&1 & sleep 6; ls -l /tmp/t.dat; wc -c /tmp/t.dat 2>&1\n")
    time.sleep(DD_SECONDS)
    out = read_all_avail(s, 1.0)
    show("4. dd 触摸测试", out)
    s.write(b"kill %1 2>/dev/null; rm -f /tmp/t.dat\n")
    time.sleep(0.5)
    s.read_all()

    s.close()
    print("\n========== 完成 ==========")
    print("event2 真实节点 + hmi_runtime fd 含 event2 + 触摸>0 → 触摸修复完整生效!")


if __name__ == "__main__":
    main()
