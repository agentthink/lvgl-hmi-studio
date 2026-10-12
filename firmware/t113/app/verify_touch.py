#!/usr/bin/env python3
"""
verify_touch.py — 重启后验证触摸修复 (v1)

1. /dev/input/ 状态 (event2 应为真实节点 c 13 66)
2. hmi_runtime 进程 + fd (应打开 event2)
3. 触摸事件测试: dd 读 event2 (用户触摸)
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
        print("ERROR: Permission denied. Run: sudo python3 verify_touch.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.5)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    s.read_all()

    # 1. /dev/input/ 状态
    out = send_line(s, b"ls -la /dev/input/\n", 1.0)
    show("1. /dev/input/ 状态", out)

    # 2. hmi_runtime 进程 + fd
    out = send_line(s, b"pidof hmi_runtime; ls -la /proc/$(pidof hmi_runtime)/fd/ 2>&1 | grep -E 'event|input'\n", 1.5)
    show("2. hmi_runtime fd (应含 event2)", out)

    # 3. 触摸测试
    print("\n" + "=" * 62)
    print(f">>> 触摸测试: 请连续触摸屏幕 {DD_SECONDS} 秒 <<<")
    print("=" * 62)
    s.write(b"dd if=/dev/input/event2 of=/tmp/t.dat bs=24 count=5 2>&1 & sleep 6; ls -l /tmp/t.dat; wc -c /tmp/t.dat 2>&1\n")
    time.sleep(DD_SECONDS)
    out = read_all_avail(s, 1.0)
    show("3. dd 触摸测试", out)
    s.write(b"kill %1 2>/dev/null; rm -f /tmp/t.dat\n")
    time.sleep(0.5)
    s.read_all()

    s.close()
    print("\n========== 验证完成 ==========")
    print("判断: 1 event2 为 crw 13:66 真实节点 / 2 fd 打开 event2 / 3 触摸数据 >0 → 修复成功")
    print("最后请实际操作 UI 验证触摸响应!")


if __name__ == "__main__":
    main()
