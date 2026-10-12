#!/usr/bin/env python3
"""
verify_touch2.py — 决定性诊断: 重启后脚本是否还原 + 谁创建 event2 symlink

1. 重启后 S97hmiruntime 内容 (TOUCH/ln -sf 是否还原)
2. S99swupdate_autorun 完整内容 (变量式 ln 可能漏 grep)
3. S99hmiruntime 是否存在及内容
4. 所有 init.d 脚本中含 'ln -s' / 'event1' 的行 (不限定 event2 字面量)
5. overlay/squashfs 挂载状态 + 空间 (判断持久化)
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
        print("ERROR: Permission denied. Run: sudo python3 verify_touch2.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    s.read_all()

    # 1. 重启后 S97hmiruntime 是否还原 (关键!)
    out = send_line(s, b"grep -nE 'TOUCH|ln -sf|ln -s' /etc/init.d/S97hmiruntime\n", 1.0)
    show("1. 重启后 S97hmiruntime 残留 (有输出=脚本还原)", out)

    # 2. S99swupdate_autorun 全文
    out = send_line(s, b"cat /etc/init.d/S99swupdate_autorun\n", 1.5)
    show("2. S99swupdate_autorun 全文", out)

    # 3. S99hmiruntime
    out = send_line(s, b"ls -la /etc/init.d/S99hmiruntime 2>&1; cat /etc/init.d/S99hmiruntime 2>&1\n", 1.5)
    show("3. S99hmiruntime", out)

    # 4. 所有 init.d 含 ln -s 或 event1 的行
    out = send_line(s, b"grep -rn 'ln -s\\|event1' /etc/init.d/ 2>/dev/null\n", 1.5)
    show("4. init.d 所有 ln -s / event1 行", out)

    # 5. overlay 挂载状态 + 空间
    out = send_line(s, b"mount | grep -E 'overlay|squashfs|rootfs|jffs2|ubifs'\n", 1.0)
    show("5a. 挂载状态", out)
    out = send_line(s, b"df -h / /overlay /rom /mnt/UDISK 2>&1\n", 1.0)
    show("5b. 空间", out)

    # 6. /etc/init.d/S97hmiruntime 文件所在层
    out = send_line(s, b"ls -la /etc/init.d/S97hmiruntime; ls -la /etc/init.d/S97hmiruntime.bak 2>&1\n", 1.0)
    show("6. 脚本文件 + bak 是否存在 (bak 存在=修改曾生效)", out)

    s.close()
    print("\n========== 诊断完成 ==========")
    print("判读: 1 有残留=overlay未持久化; 2/3 找真正创建者; 5 看 overlay 挂载")


if __name__ == "__main__":
    main()
