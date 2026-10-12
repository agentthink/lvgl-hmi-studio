#!/usr/bin/env python3
"""
fix_final2.py — 诊断 hmi_runtime 未运行原因 + 启动它 + 处理 bootcmd 固化

1. /etc/init.d/ 下的 hmi 脚本
2. hmi_runtime 二进制
3. 启动 hmi_runtime
4. env 分区 / bootcmd 固化 (U-Boot 方式备用)
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
        print("ERROR: Permission denied. Run: sudo python3 fix_final2.py")
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

    # 1. init.d 里的 hmi 脚本
    out = send_line(s, b"ls -la /etc/init.d/ | grep -iE 'hmi|S97|S99'\n", 1.0)
    show("1. init.d hmi 脚本", out)

    # 2. hmi_runtime 二进制
    out = send_line(s, b"ls -la /usr/bin/hmi_runtime /usr/lib/hmi/ 2>&1; which hmi_runtime 2>&1\n", 1.0)
    show("2. hmi_runtime 二进制", out)

    # 3. hmi-runtime.conf
    out = send_line(s, b"cat /etc/hmi/hmi-runtime.conf 2>&1 | grep -i touch\n", 1.0)
    show("3. 触摸配置", out)

    # 4. 启动 hmi_runtime
    print("\n========== 4. 启动 hmi_runtime ==========")
    out = send_line(s, b"ls /etc/init.d/ | grep -iE 'hmi' 2>&1\n", 0.8)
    scripts = out.decode('ascii', errors='ignore')
    if 'S97hmiruntime' in scripts or 'S99hmiruntime' in scripts:
        # 找到脚本名
        name = [ln.strip() for ln in scripts.split('\n') if 'hmi' in ln.lower() and ln.strip()]
        if name:
            out = send_line(s, f"/etc/init.d/{name[0]} start 2>&1\n".encode(), 3.0)
            show(f"   启动 {name[0]}", out)
    else:
        out = send_line(s, b"nohup /usr/bin/hmi_runtime > /tmp/hmi.log 2>&1 &\n", 1.0)
        show("   直接启动", out)
    time.sleep(3)
    out = send_line(s, b"pidof hmi_runtime; ls -la /proc/$(pidof hmi_runtime)/fd/ 2>&1 | grep -E 'event|input'\n", 1.5)
    show("   启动后 fd", out)

    # 5. /dev/by-name/ (env 分区)
    out = send_line(s, b"ls -la /dev/by-name/ 2>&1\n", 1.0)
    show("5. /dev/by-name/", out)

    s.close()
    print("\n========== 诊断完成 ==========")
    print("若 hmi_runtime 已启动且 fd 含 event2 → UI 触摸应正常, 请操作 UI 验证")


if __name__ == "__main__":
    main()
