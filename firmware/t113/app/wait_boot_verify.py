#!/usr/bin/env python3
"""
wait_boot_verify.py — 等待板子启动到 Linux, 然后自动验证触摸修复

流程:
1. 连接串口
2. 若在 U-Boot 提示符, 发送 boot 手动启动
3. 监控最多 120s, 等 shell 出现 (root@TinaLinux / login:)
4. 启动后验证: /dev/input/ + S97hmiruntime + hmi_runtime fd + 触摸测试
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
        print("ERROR: Permission denied. Run: sudo python3 wait_boot_verify.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.5)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    out = read_all_avail(s, 0.5)
    prompt = out.decode('ascii', errors='ignore').strip()
    print("当前状态:", prompt)

    # 如果在 U-Boot, 发送 boot
    if '=>' in prompt:
        print(">>> 在 U-Boot, 发送 boot 启动...")
        s.write(b"boot\n")
        time.sleep(2)
        out = read_all_avail(s, 1.0)
        print(out.decode('ascii', errors='ignore'))

    # 监控等 shell (最多 120s)
    print(">>> 等待 Linux 启动 (最多 120s)...")
    start = time.time()
    booted = False
    while time.time() - start < 120:
        out = read_all_avail(s, 0.5)
        if out:
            text = out.decode('ascii', errors='ignore')
            print(text, end='', flush=True)
            if 'root@TinaLinux' in text or 'login:' in text.lower():
                booted = True
                break
    if not booted:
        print("\n!!! 120s 内未启动到 shell!")
        s.close()
        return

    print("\n>>> Linux 已启动, 等待 shell 稳定...")
    time.sleep(5)
    s.read_all()

    # 验证 1: /dev/input/
    out = send_line(s, b"ls -la /dev/input/\n", 1.0)
    show("1. /dev/input/ (event2 应为 c 13:66)", out)

    # 验证 2: S97hmiruntime
    out = send_line(s, b"grep -nE 'TOUCH_NAME|TOUCH_SRC|ln -sf' /etc/init.d/S97hmiruntime\n", 1.0)
    show("2. S97hmiruntime (应含 TOUCH_NAME, 无 TOUCH_SRC)", out)

    # 验证 3: hmi_runtime fd
    out = send_line(s, b"pidof hmi_runtime; ls -la /proc/$(pidof hmi_runtime)/fd/ 2>&1 | grep -E 'event|input'\n", 1.5)
    show("3. hmi_runtime fd (应含 event2)", out)

    # 验证 4: 触摸测试
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
    print("\n========== 验证完成 ==========")
    print("全过 = 烧录成功 + 触摸修复生效!")


if __name__ == "__main__":
    main()
