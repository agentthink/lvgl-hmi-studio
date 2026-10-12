#!/usr/bin/env python3
"""
diag_touch.py — 综合触摸诊断 (v1)

收集: 中断表 / 驱动日志 / 输入设备 / 事件节点 / 模块 / GPIO / 网络
"""
import serial, time, sys

PORT = "/dev/ttyUSB0"
BAUD = 115200
MARK = "==END=="


def drain(s, wait=0.4):
    time.sleep(wait)
    out = b""
    while s.in_waiting:
        out += s.read(s.in_waiting)
        time.sleep(0.05)
    return out


def send_chars(s, data, inter=0.02):
    for ch in data:
        s.write(bytes([ch]))
        time.sleep(inter)


def send(s, cmd, wait=2.0):
    send_chars(s, f"{cmd}; echo {MARK}\n".encode())
    deadline = time.time() + wait + 8
    buf = b""
    while time.time() < deadline:
        buf += drain(s, 0.1)
        if MARK.encode() in buf:
            break
    text = buf.decode('ascii', errors='ignore')
    print(f">>> {cmd}")
    out_part = text.split(MARK)[0]
    cmd_echo = f"{cmd}; echo {MARK}"
    for ln in out_part.split('\n'):
        ln = ln.rstrip()
        if ln and ln != cmd_echo:
            print(f"  {ln}")
    return text


def main():
    print(f"Connecting to {PORT} @ {BAUD}...")
    try:
        s = serial.Serial(PORT, BAUD, timeout=0.5)
    except PermissionError:
        print("ERROR: Permission denied. Run: sudo python3 diag_touch.py")
        sys.exit(1)

    send_chars(s, b"\n")
    drain(s, 0.3)
    s.write(b"echo SHELL_OK_123\n")
    out = drain(s, 0.8)
    if b"SHELL_OK_123" not in out:
        print("!! Shell 未就绪, 先运行: sudo python3 recover_serial.py")
        s.close()
        return
    print("[Shell OK]")

    send(s, "killall udhcpc", 0.5)

    print("\n========== 1. 完整中断表 /proc/interrupts ==========")
    send(s, "cat /proc/interrupts", 1.0)

    print("\n========== 2. 驱动日志 dmesg (gt9/goodix/ctp) ==========")
    send(s, "dmesg | grep -iE 'gt9|goodix|ctp'", 1.0)

    print("\n========== 3. 注册的输入设备 ==========")
    send(s, "cat /proc/bus/input/devices", 1.0)

    print("\n========== 4. 事件节点 ==========")
    send(s, "ls -la /dev/input/", 0.8)

    print("\n========== 5. 驱动模块 ==========")
    send(s, "cat /proc/modules | grep -i gt", 0.8)

    print("\n========== 6. GPIO 状态 (PB6/PB7/PA3) ==========")
    send(s, "cat /sys/kernel/debug/gpio | grep -iE 'PB6|PB7|PA3|gt9|touch' | head -20", 0.8)

    print("\n========== 7. pinctrl 引脚复用 ==========")
    send(s, "cat /sys/kernel/debug/pinctrl/2000000.pinctrl/pins | grep -iE 'PB6|PB7|PE12|PE13'", 0.8)

    print("\n========== 8. 网络状态 ==========")
    send(s, "ifconfig eth0", 1.0)
    send(s, "ifconfig eth0 | grep inet", 0.6)

    s.close()
    print("\n========== 诊断完成 ==========")
    print("关键判读: 第1项无 gt9xxnew_ts 行 → 驱动未申请中断; 第2项看 probe 失败原因")


if __name__ == "__main__":
    main()
