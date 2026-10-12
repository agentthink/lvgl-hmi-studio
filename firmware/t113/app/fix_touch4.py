#!/usr/bin/env python3
"""
fix_touch4.py — 触摸修复 v4 (确认重启后节点状态 + 找脚本逻辑)

1. /dev/input/ 当前状态 (重启后是否又出现错误 symlink)
2. GT911 真实设备号 (/sys/class/input/event2/dev)
3. S97hmiruntime 完整内容 (symlink 创建逻辑)
4. hmi_runtime 日志 (dmesg)
5. 修复节点 + 重启 hmi_runtime
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
        print("ERROR: Permission denied. Run: sudo python3 fix_touch4.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()
    # 等 shell 稳定
    s.write(b"\n")
    time.sleep(0.5)
    s.read_all()

    # 1. /dev/input/ 现状
    out = send_line(s, b"ls -la /dev/input/\n", 1.0)
    show("1. /dev/input/ 现状", out)

    # 2. GT911 设备号
    out = send_line(s, b"cat /sys/class/input/event2/dev 2>&1; cat /sys/class/input/input2/name 2>&1\n", 1.0)
    show("2. event2 dev + input2 name", out)

    # 3. S97hmiruntime 全文
    out = send_line(s, b"cat /etc/init.d/S97hmiruntime\n", 2.0)
    show("3. S97hmiruntime 全文", out)

    # 4. hmi_runtime 日志
    out = send_line(s, b"dmesg | grep -iE 'hmi|dlopen|UI loaded' | sed -n '1,20p'\n", 1.5)
    show("4. dmesg hmi 日志", out)

    # 5. 修复节点: 删 symlink + mknod 正确设备号
    print("\n========== 5. 修复 /dev/input/event2 ==========")
    # 先拿到 event2 真实设备号 (从 /sys/class/input/event2/dev)
    out = send_line(s, b"cat /sys/class/input/event2/dev 2>&1\n", 0.8)
    dev = out.decode('ascii', errors='ignore').strip().split('\n')[-1].strip()
    print(f"   检测到 event2 dev = {dev!r}")
    if ':' in dev:
        maj, min_ = dev.split(':')
        cmd = f"rm -f /dev/input/event2; mknod /dev/input/event2 c {maj.strip()} {min_.strip()}; chmod 666 /dev/input/event2; ls -la /dev/input/\n"
    else:
        cmd = b"rm -f /dev/input/event2; mknod /dev/input/event2 c 13 66; chmod 666 /dev/input/event2; ls -la /dev/input/\n"
    out = send_line(s, cmd.encode(), 1.5)
    show("   修复后 /dev/input/", out)

    # 6. 重启 hmi_runtime
    print("\n========== 6. 重启 hmi_runtime ==========")
    out = send_line(s, b"killall hmi_runtime 2>&1; sleep 3; pidof hmi_runtime; ls -la /proc/$(pidof hmi_runtime)/fd/ 2>&1 | grep event\n", 4.0)
    show("   重启后 fd", out)

    s.close()
    print("\n========== 修复 v4 完成 ==========")
    print("请测试 UI 触摸!")


if __name__ == "__main__":
    main()
