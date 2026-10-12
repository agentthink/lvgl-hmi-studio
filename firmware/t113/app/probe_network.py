#!/usr/bin/env python3
"""
probe_network.py — 通过串口探测 T113 板子网络/远程能力 (v4)

v4 修复:
- marker 改为 ==END== (不含 #, 避免被 shell 注释吞掉)
- 先 killall udhcpc 清场 (避免后台输出干扰)
- 等提示符判断命令完成
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
    """低速发送, 等 ==END== 标记"""
    send_chars(s, f"{cmd}; echo {MARK}\n".encode())
    deadline = time.time() + wait + 6
    buf = b""
    while time.time() < deadline:
        buf += drain(s, 0.1)
        if MARK.encode() in buf:
            break
    text = buf.decode('ascii', errors='ignore')
    # 打印: 去掉命令回显行和 marker 之后
    print(f">>> {cmd}")
    out_part = text.split(MARK)[0]
    cmd_echo = f"{cmd}; echo {MARK}"
    for ln in out_part.split('\n'):
        ln = ln.strip()
        if ln and ln != cmd_echo:
            print(f"  {ln}")
    return text


def main():
    print(f"Connecting to {PORT} @ {BAUD}...")
    try:
        s = serial.Serial(PORT, BAUD, timeout=0.5)
    except PermissionError:
        print("ERROR: Permission denied. Run: sudo python3 probe_network.py")
        sys.exit(1)

    # 唤醒
    send_chars(s, b"\n")
    drain(s, 0.3)
    # 验证 shell
    s.write(b"echo SHELL_OK_123\n")
    out = drain(s, 0.8)
    if b"SHELL_OK_123" not in out:
        print("!! Shell 未就绪, 先运行: sudo python3 recover_serial.py")
        s.close()
        return
    print("[Shell OK]")

    print("\n===== 0. 清理 udhcpc (避免干扰) =====")
    send(s, "killall udhcpc", 0.5)

    print("\n===== 1. Network interfaces =====")
    send(s, "ifconfig eth0", 1.0)
    send(s, "ifconfig wlan0 | head -3", 0.6)

    print("\n===== 2. DHCP (10s) =====")
    send(s, "ifconfig eth0 up", 0.6)
    send(s, "udhcpc -i eth0 -t 5 -n &", 0.5)
    time.sleep(8)
    send(s, "ifconfig eth0 | grep inet", 0.6)

    print("\n===== 3. Remote tools =====")
    send(s, "which dropbear nc telnetd", 0.6)
    send(s, "ls /usr/sbin/", 0.6)

    print("\n===== 4. Touch device =====")
    send(s, "ls -la /dev/input/", 0.6)

    print("\n===== 5. GT911 irq baseline =====")
    send(s, "cat /proc/interrupts | grep -i gt9", 0.6)

    print("\n===== 6. hmi_runtime =====")
    send(s, "ps | grep hmi | grep -v grep", 0.6)

    print("\n===== 7. 手动配静态 IP (备用) =====")
    send(s, "ifconfig eth0 192.168.100.99 netmask 255.255.255.0 up", 0.6)
    send(s, "ifconfig eth0 | grep inet", 0.6)

    s.close()
    print("\n===== Probe done =====")


if __name__ == "__main__":
    main()
