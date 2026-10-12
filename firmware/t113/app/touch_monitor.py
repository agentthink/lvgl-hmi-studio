#!/usr/bin/env python3
"""
touch_monitor.py — 串口实时监控 GT911 触摸事件 (v1)

原理:
- /proc/interrupts 的 gt9xx 中断计数: 每按一次屏幕, INT 引脚拉低触发一次中断
  → 计数增长 = 硬件→中断链路 OK (与 hmi_runtime 无关, 最可靠)
- /dev/input/event2: 驱动上报的触摸事件流 (需 hmi_runtime 未独占消费)

用法: 启动后按提示触摸屏幕, 观察 IRQ 计数是否增长
"""
import serial, time, sys

PORT = "/dev/ttyUSB0"
BAUD = 115200
MONITOR_SECONDS = 30


def drain(s, wait=0.3):
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


def send(s, cmd, wait=1.0):
    send_chars(s, (cmd + "\n").encode())
    time.sleep(wait)
    return drain(s, 0.2)


def main():
    print(f"Connecting to {PORT} @ {BAUD}...")
    try:
        s = serial.Serial(PORT, BAUD, timeout=0.5)
    except PermissionError:
        print("ERROR: Permission denied. Run: sudo python3 touch_monitor.py")
        sys.exit(1)

    # 唤醒 + 验证
    send_chars(s, b"\n")
    drain(s, 0.3)
    s.write(b"echo SHELL_OK_123\n")
    out = drain(s, 0.8)
    if b"SHELL_OK_123" not in out:
        print("!! Shell 未就绪, 先运行: sudo python3 recover_serial.py")
        s.close()
        return
    print("[Shell OK]")

    # 清场: 杀掉 udhcpc 等后台输出源
    send(s, "killall udhcpc", 0.5)

    print("=== 1. GT911 中断基线 ===")
    resp = send(s, "cat /proc/interrupts | grep -i gt9", 0.8)
    print(resp, end='')

    print("=== 2. 触摸设备节点 ===")
    resp = send(s, "ls -la /dev/input/", 0.8)
    print(resp, end='')

    print("\n" + "=" * 62)
    print(">>> 现在请连续触摸屏幕 (点按/滑动均可) <<<")
    print(f">>> 监控 {MONITOR_SECONDS} 秒, 观察 IRQ 计数是否增长 <<<")
    print("=" * 62)

    # 板上跑监控循环: 每 1 秒打印一次 gt9xx 中断行
    # busybox ash 兼容写法
    loop = "i=0; while [ $i -lt 30 ]; do cat /proc/interrupts | grep -i gt9; echo --; i=$((i+1)); sleep 1; done"
    print(f"\n[启动板上监控循环, {MONITOR_SECONDS}s]")
    send_chars(s, loop.encode() + b"\n")

    start = time.time()
    while time.time() - start < MONITOR_SECONDS + 5:
        out = drain(s, 0.5)
        if out:
            text = out.decode('ascii', errors='ignore')
            print(text, end='', flush=True)

    # 停止循环
    send_chars(s, b"\x03")
    time.sleep(0.3)
    s.read_all()
    send(s, "echo MONITOR_STOPPED", 0.5)
    s.close()
    print("\n=== 监控结束 ===")
    print("""
结果判断:
  IRQ 计数增长  → 硬件+中断链路 OK, 触摸信号已到 CPU (问题在驱动上报或 hmi_runtime)
  IRQ 计数不变  → GT911 未产生 INT (硬件/配置/复位问题)
""")


if __name__ == "__main__":
    main()
