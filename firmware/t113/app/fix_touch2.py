#!/usr/bin/env python3
"""
fix_touch2.py — 触摸修复 v2 (v1 的 mdev/od 不可用, 改手动 mknod)

步骤:
1. 查看 S97hmiruntime 完整内容 (找 event2 symlink 创建逻辑)
2. 查看 S99swupdate_autorun
3. 手动 mknod 重建 /dev/input/event2 (c 13 66)
4. 确认 GT911 input 索引 + hmi_runtime 状态
5. 重启 hmi_runtime
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
        print("ERROR: Permission denied. Run: sudo python3 fix_touch2.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()

    # 1. S97hmiruntime 完整内容
    out = send_line(s, b"cat /etc/init.d/S97hmiruntime\n", 2.0)
    show("1. /etc/init.d/S97hmiruntime", out)

    # 2. S99swupdate_autorun
    out = send_line(s, b"cat /etc/init.d/S99swupdate_autorun\n", 1.5)
    show("2. /etc/init.d/S99swupdate_autorun", out)

    # 3. 手动重建 event2
    print("\n========== 3. 手动重建 /dev/input/event2 ==========")
    out = send_line(s, b"mknod /dev/input/event2 c 13 66 2>&1; chmod 666 /dev/input/event2; ls -la /dev/input/\n", 1.5)
    show("   重建后 /dev/input/", out)

    # 4. GT911 input 索引 + 进程状态
    out = send_line(s, b"cat /proc/bus/input/devices | grep -B1 -A4 gt9xx\n", 1.0)
    show("4a. GT911 input 索引", out)
    out = send_line(s, b"ps | grep -E 'hmi|procd' | grep -v grep\n", 1.0)
    show("4b. hmi_runtime 状态", out)

    # 5. 重启 hmi_runtime (procd respawn)
    print("\n========== 5. 重启 hmi_runtime ==========")
    out = send_line(s, b"killall hmi_runtime 2>&1; sleep 3; ps | grep hmi | grep -v grep\n", 4.0)
    show("   重启后", out)

    s.close()
    print("\n========== 修复 v2 完成 ==========")
    print("请测试 UI 触摸是否恢复! (点按屏幕按钮)")


if __name__ == "__main__":
    main()
