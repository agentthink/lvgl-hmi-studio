#!/usr/bin/env python3
"""
setup_network.py — 通过串口配置 T113 板子网络并启动 SSH 服务

流程:
1. 启动 eth0 并 DHCP 获取 IP
2. 检查 dropbear (SSH) 是否运行, 未运行则启动
3. 设置 root 密码 (空密码以便 SSH 登录)
4. 打印板子 IP, 之后主机即可 `ssh root@<IP>` 登录
"""
import serial, time, sys, re

SERIAL_PORT = "/dev/ttyUSB0"
BAUD_RATE = 115200


def send(s, cmd, wait=0.8, echo=True):
    if echo:
        print(f">>> {cmd}")
    s.write(cmd.encode() + b"\n")
    time.sleep(wait)
    out = b""
    while s.in_waiting:
        out += s.read(s.in_waiting)
    text = out.decode('ascii', errors='ignore')
    if echo and text.strip():
        # 只打印非命令回显的部分
        lines = [ln for ln in text.split('\n') if ln.strip() and ln.strip() != cmd]
        if lines:
            print('\n'.join(lines))
    return text


def main():
    print(f"Connecting to {SERIAL_PORT} @ {BAUD_RATE}...")
    try:
        s = serial.Serial(SERIAL_PORT, BAUD_RATE, timeout=0.5)
    except PermissionError:
        print("ERROR: Permission denied. Run: sudo python3 setup_network.py")
        sys.exit(1)
    except Exception as e:
        print(f"ERROR: {e}")
        sys.exit(1)

    print("Connected. Waking up shell...")
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    s.read_all()
    # 多发几次回车确保进入 shell
    s.write(b"\n")
    time.sleep(0.3)
    s.read_all()

    # 1. 检查当前网络状态
    print("\n[1/5] Checking current network state...")
    send(s, "ifconfig eth0", 0.8)

    # 2. 启动 eth0 并 DHCP
    print("\n[2/5] Bringing up eth0 and DHCP...")
    send(s, "ifconfig eth0 up", 0.5)
    send(s, "udhcpc -i eth0 -t 5 -n 2>&1", 3.0)

    # 3. 再次查看 IP
    print("\n[3/5] Checking assigned IP...")
    resp = send(s, "ifconfig eth0 2>&1 | grep 'inet addr' || ifconfig eth0 2>&1 | grep 'inet '", 0.8)

    # 4. 检查/启动 dropbear (SSH)
    print("\n[4/5] Checking SSH (dropbear) service...")
    resp = send(s, "which dropbear; ls /etc/init.d/*dropbear* /etc/init.d/*ssh* 2>&1", 0.8)
    send(s, "/etc/init.d/dropbear start 2>&1 || dropbear 2>&1", 1.5)
    send(s, "netstat -lnt 2>&1 | grep :22 || ss -lnt 2>&1 | grep :22", 0.5)

    # 5. 设置 root 空密码 (允许 SSH 登录)
    print("\n[5/5] Configuring root password for SSH login...")
    # busybox passwd 可能交互, 用 chpasswd 替代; 或直接改 shadow
    send(s, "grep '^root:' /etc/shadow", 0.5)
    # 如果 shadow 是 root:: (空密码) 直接可用; 否则置空
    send(s, "sed -i 's|^root:[^:]*:|root::|' /etc/shadow 2>&1", 0.5)
    send(s, "grep '^root:' /etc/shadow", 0.5)

    # 确认 dropbear 配置允许空密码登录
    send(s, "grep -E 'dropbear|ssh' /etc/config/* 2>/dev/null; cat /etc/init.d/dropbear 2>/dev/null | head -30", 0.8)

    # 6. 最终状态报告
    print("\n" + "=" * 60)
    print("Final network state:")
    print("=" * 60)
    send(s, "ifconfig eth0", 0.8)
    send(s, "ifconfig wlan0 2>&1 | head -5", 0.5)
    send(s, "ps | grep -E 'dropbear|ssh' | grep -v grep", 0.5)

    # 提取 IP
    s.write(b"ip addr show eth0 2>/dev/null | grep 'inet '\n")
    time.sleep(0.8)
    out = b""
    while s.in_waiting:
        out += s.read(s.in_waiting)
    text = out.decode('ascii', errors='ignore')
    print("\n" + text)

    m = re.search(r'inet (\d+\.\d+\.\d+\.\d+)', text)
    if m:
        ip = m.group(1)
        print(f"\n*** Board IP: {ip} ***")
        print(f"*** On host run: ssh root@{ip}  (empty password) ***")
    else:
        print("\nCould not auto-detect IP. Check the ifconfig output above.")

    s.close()


if __name__ == "__main__":
    main()
