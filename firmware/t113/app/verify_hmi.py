#!/usr/bin/env python3
"""
verify_hmi.py — 诊断并启动 hmi_runtime

1. rc.d symlink 检查
2. 手动 start S97hmiruntime
3. 直接运行 hmi_runtime 看报错 (依赖缺失?)
4. 依赖库检查
"""
import serial, time, sys

PORT = "/dev/ttyUSB0"
BAUD = 115200


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
        print("ERROR: Permission denied. Run: sudo python3 verify_hmi.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    out = read_all_avail(s, 0.5)
    if b'root@TinaLinux' not in out:
        s.write(b"\n")
        time.sleep(0.5)
        out = read_all_avail(s, 0.5)
    if b'root@TinaLinux' not in out:
        print("!! shell 未就绪")
        s.close()
        return
    print("[shell OK]")

    # 1. rc.d symlink
    out = send_line(s, b"ls -la /etc/rc.d/ | grep -i hmi; ls -la /etc/rc.d/S97hmiruntime 2>&1\n", 1.0)
    show("1. rc.d symlink", out)

    # 2. 手动 start
    print("\n========== 2. 手动 start ==========")
    out = send_line(s, b"/etc/init.d/S97hmiruntime start 2>&1; sleep 3; pidof hmi_runtime\n", 4.0)
    show("   start 后进程", out)

    # 3. 如果没跑, 直接运行看报错
    out = send_line(s, b"pidof hmi_runtime || (timeout 5 /usr/bin/hmi_runtime 2>&1 & sleep 6; echo RUN_DONE)\n", 7.0)
    show("3. 直接运行输出", out)

    # 4. 依赖库
    out = send_line(s, b"ls -la /usr/lib/libpng12* /usr/lib/libuapi* 2>&1; echo ---; ls /usr/lib/hmi/ 2>&1\n", 1.0)
    show("4. 依赖库", out)

    # 5. 当前进程状态
    out = send_line(s, b"pidof hmi_runtime && ls -la /proc/$(pidof hmi_runtime)/fd/ 2>&1 | grep -E 'event|input' || echo NO_PROCESS\n", 1.5)
    show("5. 进程状态", out)

    s.close()
    print("\n========== 完成 ==========")


if __name__ == "__main__":
    main()
