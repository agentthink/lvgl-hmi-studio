#!/usr/bin/env python3
"""
fix_touch8.py — 加载 GT911 驱动 + 验证触摸 (v8, 修复 head not found)

用完整路径 insmod, 重建 event2 节点, 重启 hmi_runtime, dd 验证
"""
import serial, time, sys

PORT = "/dev/ttyUSB0"
BAUD = 115200
DD_SECONDS = 8

KO_PATH = "/lib/modules/5.4.61/gt9xxnew_ts.ko"


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
        print("ERROR: Permission denied. Run: sudo python3 fix_touch8.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    s.read_all()

    # 1. insmod 完整路径
    print("\n========== 1. insmod GT911 驱动 ==========")
    out = send_line(s, f"insmod {KO_PATH} 2>&1; echo INSMOD_EXIT=$?; sleep 2; ls -la /dev/input/\n".encode(), 3.0)
    show("   insmod 后 /dev/input/", out)

    # 2. 确认 event2
    out = send_line(s, b"ls -la /dev/input/event2 2>&1; cat /proc/bus/input/devices | grep -A2 gt9xx\n", 1.0)
    show("2. event2 状态", out)

    # 3. 重启 hmi_runtime (procd respawn)
    print("\n========== 3. 重启 hmi_runtime ==========")
    out = send_line(s, b"killall hmi_runtime 2>&1; sleep 4; pidof hmi_runtime; ls -la /proc/$(pidof hmi_runtime)/fd/ 2>&1 | grep -E 'event|input'\n", 5.0)
    show("   fd (应含 event2)", out)

    # 4. 触摸验证
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
    print("\n========== fix_touch8 完成 ==========")
    print("判断: event2=c 13:66 + fd 含 event2 + 触摸>0 → 当前会话触摸恢复!")
    print("提示: 重启丢失! 需重编固件固化 (SDK 已改好)")


if __name__ == "__main__":
    main()
