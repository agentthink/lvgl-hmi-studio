#!/usr/bin/env python3
"""
touch_diag2.py — 触摸诊断 v2 (用 raw_probe 验证过的可靠读取模式)

重点:
1. gt9xx 驱动 probe 日志 (成功? 中断申请失败?)
2. /dev/input/ 节点列表
3. 已注册输入设备
4. 触摸时监控 PA3_EINT / sun4i-ts 计数变化 (15s)
5. 网络状态 (eth0 IP)
"""
import serial, time, sys

PORT = "/dev/ttyUSB0"
BAUD = 115200


def read_all_avail(s, idle=0.4):
    """读取直到连续 idle 秒无新数据"""
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
    """整行发送 + 等待输出稳定"""
    s.write(data)
    time.sleep(0.3)
    out = read_all_avail(s, wait)
    return out


def show(label, out):
    print(f"\n========== {label} ==========")
    print(out.decode('ascii', errors='ignore'))


def main():
    print(f"Connecting to {PORT} @ {BAUD}...")
    try:
        s = serial.Serial(PORT, BAUD, timeout=0.5)
    except PermissionError:
        print("ERROR: Permission denied. Run: sudo python3 touch_diag2.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()

    # 验证
    out = send_line(s, b"echo SHELL_OK_123\n", 0.8)
    if b"SHELL_OK_123" not in out:
        print("!! Shell 未就绪")
        s.close()
        return
    print("[Shell OK]")

    # 1. gt9xx 驱动日志
    out = send_line(s, b"dmesg | grep -iE 'gt9|goodix|ctp'\n", 3.0)
    show("1. dmesg gt9/goodix/ctp", out)

    # 2. /dev/input/
    out = send_line(s, b"ls -la /dev/input/\n", 1.5)
    show("2. /dev/input/", out)

    # 3. 注册的输入设备
    out = send_line(s, b"cat /proc/bus/input/devices\n", 1.5)
    show("3. /proc/bus/input/devices", out)

    # 4. 触摸监控: 中断计数变化 (15s)
    print("\n" + "=" * 62)
    print(">>> 现在连续触摸屏幕 15 秒 (点按/滑动) <<<")
    print(">>> 观察 PA3_EINT / sun4i-ts 计数是否增长 <<<")
    print("=" * 62)
    # 板上循环: 每 1 秒打印相关中断行
    loop = b"while :; do cat /proc/interrupts | grep -E 'PA3|sun4i|gt9| 43:'; echo ---; sleep 1; done\n"
    s.write(loop)
    time.sleep(0.5)
    print("\n[监控启动, 15s]")
    start = time.time()
    while time.time() - start < 15:
        out = read_all_avail(s, 0.3)
        if out:
            print(out.decode('ascii', errors='ignore'), end='', flush=True)
    s.write(b"\x03")  # 停止循环
    time.sleep(0.3)
    s.read_all()

    # 5. 网络状态
    out = send_line(s, b"ifconfig eth0\n", 1.5)
    show("5. eth0 网络", out)

    s.close()
    print("\n========== 诊断完成 ==========")
    print("判读:")
    print("  1. dmesg 看驱动是否 probe 成功 / 中断申请错误")
    print("  4. PA3_EINT 增长 → GT911 INT 已到 CPU; 不增长 → GT911 未发中断")


if __name__ == "__main__":
    main()
