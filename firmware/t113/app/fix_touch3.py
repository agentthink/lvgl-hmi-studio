#!/usr/bin/env python3
"""
fix_touch3.py — 触摸验证 v3 (完整链路验证)

1. /dev/input/ 现状 (event2 节点设备号)
2. 所有 input 设备的 name + dev 号 (确认 GT911 的真实 minor)
3. 触摸事件测试: dd 读 event2 (用户触摸)
4. hmi_runtime 进程/fd/日志
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
        print("ERROR: Permission denied. Run: sudo python3 fix_touch3.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()

    # 1. /dev/input/ 现状
    out = send_line(s, b"ls -la /dev/input/\n", 1.0)
    show("1. /dev/input/ 现状", out)

    # 2. 所有 input 设备 name+dev
    out = send_line(s, b"for d in /sys/class/input/input*; do echo \"$d: $(cat $d/name 2>/dev/null) dev=$(cat $d/dev 2>/dev/null)\"; done\n", 1.5)
    show("2. input 设备 name+dev", out)

    # 3. 触摸事件测试: dd 读 event2
    print("\n" + "=" * 62)
    print(f">>> 触摸测试: 请连续触摸屏幕 {DD_SECONDS} 秒 <<<")
    print("=" * 62)
    # dd 后台读, 6 秒后查看文件大小
    s.write(b"dd if=/dev/input/event2 of=/tmp/t.dat bs=24 count=5 2>&1 & sleep 6; ls -l /tmp/t.dat; wc -c /tmp/t.dat 2>&1\n")
    time.sleep(DD_SECONDS)
    out = read_all_avail(s, 1.0)
    show("3. dd 触摸测试结果", out)
    # 清理
    s.write(b"kill %1 2>/dev/null; rm -f /tmp/t.dat\n")
    time.sleep(0.5)
    s.read_all()

    # 4a. hmi_runtime 进程
    out = send_line(s, b"pidof hmi_runtime; ps | grep hmi | grep -v grep\n", 1.0)
    show("4a. hmi_runtime 进程", out)

    # 4b. hmi_runtime 打开的 fd
    out = send_line(s, b"ls -la /proc/$(pidof hmi_runtime)/fd/ 2>&1 | grep -E 'event|input|hmi|so'\n", 1.0)
    show("4b. hmi_runtime fd", out)

    # 4c. hmi_runtime 日志
    out = send_line(s, b"logread 2>/dev/null | grep -iE 'hmi|touch|lvgl' | sed -n '1,25p'\n", 1.5)
    show("4c. hmi 日志", out)

    s.close()
    print("\n========== 验证完成 ==========")
    print("重点: 3 触摸数据量 / 4b fd 是否打开 event2 / 4c 日志错误")


if __name__ == "__main__":
    main()
