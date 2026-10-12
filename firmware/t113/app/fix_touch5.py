#!/usr/bin/env python3
"""
fix_touch5.py — 永久修复: 修改 S97hmiruntime 去除错误 symlink 逻辑 + 重启

根因: S97hmiruntime 每次开机执行 ln -sf /dev/input/event1 /dev/input/event2
     覆盖 GT911 真实节点 (GT911 现在是 event2 13:66, event1 是音频插口)

修复:
1. 备份原脚本
2. sed 删除 TOUCH_SRC/TOUCH_LINK/ln -sf 相关行
3. 验证修改结果
4. 删除当前错误 symlink
5. reboot (devtmpfs 重新创建正确节点)
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
        print("ERROR: Permission denied. Run: sudo python3 fix_touch5.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    s.read_all()

    # 1. 备份
    print("\n========== 1. 备份原脚本 ==========")
    out = send_line(s, b"cp /etc/init.d/S97hmiruntime /etc/init.d/S97hmiruntime.bak && echo BACKUP_OK\n", 1.0)
    show("   备份", out)

    # 2. sed 修改
    print("\n========== 2. sed 删除 symlink 逻辑 ==========")
    sed = "sed -i '/TOUCH_SRC/d; /TOUCH_LINK=/d; /ln -sf/d' /etc/init.d/S97hmiruntime && echo SED_OK\n"
    out = send_line(s, sed.encode(), 1.0)
    show("   sed", out)

    # 3. 验证: 确认 TOUCH/ln -sf 已删除
    print("\n========== 3. 验证修改 ==========")
    out = send_line(s, b"grep -nE 'TOUCH|ln -sf|event' /etc/init.d/S97hmiruntime\n", 1.0)
    show("   残留检查 (应无 TOUCH_SRC/ln -sf)", out)
    out = send_line(s, b"sed -n '15,30p' /etc/init.d/S97hmiruntime\n", 1.0)
    show("   修改后脚本关键段", out)

    # 4. 删除当前错误 symlink
    print("\n========== 4. 删除错误 symlink ==========")
    out = send_line(s, b"rm -f /dev/input/event2 /dev/input/touchscreen; ls -la /dev/input/\n", 1.0)
    show("   删除后 /dev/input/", out)

    # 5. 重启
    print("\n========== 5. 重启板子 ==========")
    print("[即将执行 reboot, 串口会话将断开]")
    s.write(b"reboot\n")
    time.sleep(2)
    s.read_all()
    s.close()
    print("\n板子已重启! 等 20-30 秒后运行:")
    print("  sudo python3 verify_touch.py  (验证触摸)")


if __name__ == "__main__":
    main()
