#!/usr/bin/env python3
"""
fix_final.py — 收尾: 固化 bootcmd + 验证触摸修复

1. fw_setenv 固化 bootcmd (加 ubi part sys, 解决自动启动)
2. 验证 /dev/input/ (event2 真实节点)
3. 验证 S97hmiruntime (修复版)
4. 验证 hmi_runtime fd
5. 触摸测试 (dd)
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
        print("ERROR: Permission denied. Run: sudo python3 fix_final.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    out = read_all_avail(s, 0.5)
    if b'root@TinaLinux' not in out:
        print("!! 未在 Linux shell:", out.decode('ascii', errors='ignore').strip())
        s.close()
        return
    print("[Linux shell OK]")

    # 1. 固化 bootcmd
    print("\n========== 1. 固化 bootcmd (加 ubi part sys) ==========")
    out = send_line(s, b"which fw_setenv fw_printenv 2>&1; ls /sbin/fw_setenv /usr/sbin/fw_setenv 2>&1\n", 1.0)
    show("   fw 工具", out)
    # 用单引号防止 ${bootdelay} 被 shell 展开
    cmd = "fw_setenv bootcmd 'sleep ${bootdelay}; mtdparts default; ubi part sys; run setargs_nand_ubi; run boot_dsp0; run boot_normal' 2>&1; echo FW_EXIT=$?\n"
    out = send_line(s, cmd.encode(), 2.0)
    show("   setenv bootcmd", out)
    out = send_line(s, b"fw_printenv bootcmd 2>&1\n", 1.0)
    show("   验证 bootcmd", out)

    # 2. /dev/input/
    out = send_line(s, b"ls -la /dev/input/\n", 1.0)
    show("2. /dev/input/ (event2 应为 c 13:66)", out)

    # 3. S97hmiruntime
    out = send_line(s, b"grep -nE 'TOUCH_NAME|TOUCH_SRC|ln -sf' /etc/init.d/S97hmiruntime\n", 1.0)
    show("3. S97hmiruntime (应含 TOUCH_NAME, 无 TOUCH_SRC)", out)

    # 4. hmi_runtime fd
    out = send_line(s, b"pidof hmi_runtime; ls -la /proc/$(pidof hmi_runtime)/fd/ 2>&1 | grep -E 'event|input'\n", 1.5)
    show("4. hmi_runtime fd (应含 event2)", out)

    # 5. 触摸测试
    print("\n" + "=" * 62)
    print(f">>> 触摸测试: 请连续触摸屏幕 {DD_SECONDS} 秒 <<<")
    print("=" * 62)
    s.write(b"dd if=/dev/input/event2 of=/tmp/t.dat bs=24 count=5 2>&1 & sleep 6; ls -l /tmp/t.dat; wc -c /tmp/t.dat 2>&1\n")
    time.sleep(DD_SECONDS)
    out = read_all_avail(s, 1.0)
    show("5. dd 触摸测试", out)
    s.write(b"kill %1 2>/dev/null; rm -f /tmp/t.dat\n")
    time.sleep(0.5)
    s.read_all()

    s.close()
    print("\n========== 完成 ==========")
    print("bootcmd 已固化 + 触摸验证完成")


if __name__ == "__main__":
    main()
