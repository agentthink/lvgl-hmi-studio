#!/usr/bin/env python3
"""
verify_boot.py — 烧录后全面验证 (v1)

1. 串口连接 (若板子已重启到新固件, 应有 shell)
2. /dev/input/ 状态 (event2 应为真实节点 c 13 66, 非 symlink)
3. S97hmiruntime 内容 (修复版特征: TOUCH_NAME 动态检测, 无 TOUCH_SRC)
4. hmi_runtime fd (应打开 event2)
5. 触摸测试 (dd 读 event2)
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
        print("ERROR: Permission denied. Run: sudo python3 verify_boot.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.5)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    out = read_all_avail(s, 0.5)
    if not out.strip():
        print("!! 串口无响应 — 板子可能在 FEL 模式(未烧录/烧录中断)或未上电")
        print("   请确认: 烧录完成后 openixcli 应自动 reboot")
        s.close()
        return
    show("0. 串口响应", out)

    # 1. /dev/input/
    out = send_line(s, b"ls -la /dev/input/\n", 1.0)
    show("1. /dev/input/ (event2 应为 c 13:66)", out)

    # 2. S97hmiruntime 修复检查
    out = send_line(s, b"grep -nE 'TOUCH_NAME|TOUCH_SRC|ln -sf' /etc/init.d/S97hmiruntime\n", 1.0)
    show("2. S97hmiruntime (应含 TOUCH_NAME, 无 TOUCH_SRC)", out)

    # 3. hmi_runtime fd
    out = send_line(s, b"pidof hmi_runtime; ls -la /proc/$(pidof hmi_runtime)/fd/ 2>&1 | grep -E 'event|input'\n", 1.5)
    show("3. hmi_runtime fd (应含 event2)", out)

    # 4. 触摸测试
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
    print("最后实际操作 UI 验证!")


if __name__ == "__main__":
    main()
