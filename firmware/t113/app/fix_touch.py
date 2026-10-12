#!/usr/bin/env python3
"""
fix_touch.py — 修复 /dev/input/event2 被错误 symlink 覆盖的问题 (v1)

根因: /dev/input/event2 -> /dev/input/event1 (音频插口) 的符号链接
      覆盖了 GT911 的真实节点 (应为 c 13 66)

步骤:
1. 找到创建错误 symlink 的脚本源头
2. 删除错误 symlink
3. mdev -s 重建设备节点 (或手动 mknod)
4. 验证 event2 正确性
5. 触摸测试: 读 event2 事件流
6. 重启 hmi_runtime
"""
import serial, time, sys

PORT = "/dev/ttyUSB0"
BAUD = 115200
TOUCH_TEST_SECONDS = 12


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
        print("ERROR: Permission denied. Run: sudo python3 fix_touch.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()

    # 1. 找 symlink 创建源头
    out = send_line(s, b"grep -rs event2 /etc/ /usr/bin/ /root/ /mnt/UDISK/ 2>/dev/null\n", 2.0)
    show("1. 查找创建 event2 symlink 的脚本源头", out)

    # 2. 当前 /dev/input 状态
    out = send_line(s, b"ls -la /dev/input/\n", 1.0)
    show("2. 修复前 /dev/input/", out)

    # 3. 删除错误 symlink
    print("\n========== 3. 删除错误 symlink ==========")
    out = send_line(s, b"rm -f /dev/input/event2 /dev/input/touchscreen\n", 1.0)
    show("   rm 结果", out)

    # 4. 重建节点: mdev -s, 若失败则 mknod
    out = send_line(s, b"mdev -s 2>&1; ls -la /dev/input/\n", 2.0)
    show("4. mdev -s 后 /dev/input/", out)
    # 检查 event2 是否重建为设备节点 (c 13 66)
    out = send_line(s, b"ls -la /dev/input/event2\n", 1.0)
    txt = out.decode('ascii', errors='ignore')
    if 'event2' in txt and '->' in txt:
        # 还是 symlink, 手动 mknod
        print("\n   event2 仍是 symlink, 手动 mknod...")
        out = send_line(s, b"rm -f /dev/input/event2; mknod /dev/input/event2 c 13 66; chmod 666 /dev/input/event2; ls -la /dev/input/event2\n", 1.5)
        show("   手动 mknod 后", out)
    elif 'event2' not in txt:
        print("\n   event2 不存在, 手动 mknod...")
        out = send_line(s, b"mknod /dev/input/event2 c 13 66; chmod 666 /dev/input/event2; ls -la /dev/input/event2\n", 1.5)
        show("   手动 mknod 后", out)

    # 5. 触摸测试: 读 event2 事件流
    print("\n" + "=" * 62)
    print(f">>> 触摸测试: 请触摸屏幕 {TOUCH_TEST_SECONDS} 秒 <<<")
    print("=" * 62)
    s.write(b"od -tx1 /dev/input/event2\n")
    time.sleep(0.5)
    s.read_all()
    start = time.time()
    while time.time() - start < TOUCH_TEST_SECONDS:
        out = read_all_avail(s, 0.3)
        if out:
            print(out.decode('ascii', errors='ignore'), end='', flush=True)
    s.write(b"\x03")
    time.sleep(0.3)
    s.read_all()

    # 6. 重启 hmi_runtime (procd 会自动 respawn)
    print("\n========== 6. 重启 hmi_runtime ==========")
    out = send_line(s, b"killall hmi_runtime; sleep 2; ps | grep hmi | grep -v grep\n", 3.0)
    show("   重启后进程", out)

    s.close()
    print("\n========== 修复脚本完成 ==========")
    print("下一步: 让用户测试 UI 触摸是否恢复")
    print("若恢复, 需修改创建 symlink 的脚本以永久修复")


if __name__ == "__main__":
    main()
