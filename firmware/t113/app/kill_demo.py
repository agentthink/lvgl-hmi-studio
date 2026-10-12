#!/usr/bin/env python3
"""
kill_demo.py — 杀掉 lv_examples demo 程序, 让 hmi_runtime 独占显示/触摸
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
        print("ERROR: Permission denied. Run: sudo python3 kill_demo.py")
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

    # 1. 查看 lv_examples 进程
    out = send_line(s, b"ps | grep lv_examples | grep -v grep\n", 1.0)
    show("1. lv_examples 进程", out)

    # 2. 杀掉
    out = send_line(s, b"killall lv_examples 2>&1; sleep 1; ps | grep lv_examples | grep -v grep; echo KILLED\n", 1.5)
    show("2. 杀掉后", out)

    # 3. 确认 hmi_runtime 还在
    out = send_line(s, b"pidof hmi_runtime && echo HMI_OK || /etc/init.d/S97hmiruntime start 2>&1; sleep 2; pidof hmi_runtime\n", 3.0)
    show("3. hmi_runtime 状态", out)

    # 4. 确认 lv_examples 不再重启 (等 3 秒)
    out = send_line(s, b"sleep 3; ps | grep lv_examples | grep -v grep || echo NO_LV_EXAMPLES\n", 4.0)
    show("4. lv_examples 是否复活", out)

    s.close()
    print("\n========== 完成 ==========")
    print("请测试: 连续点击屏幕, 应不再跳转到 lvgl demo 页面!")


if __name__ == "__main__":
    main()
