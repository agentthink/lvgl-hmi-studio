#!/usr/bin/env python3
"""
fix_touch6.py — 板子临时修复: 修改 S97hmiruntime 去除硬编码 symlink + 重启

(与 SDK 源文件修复一致, 先改板子上当前脚本, 验证触摸后再重编固件固化)
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
        print("ERROR: Permission denied. Run: sudo python3 fix_touch6.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    s.read_all()

    # 1. 诊断: rc.d/S97hmiruntime 是否 symlink
    print("\n========== 1. 诊断 ==========")
    out = send_line(s, b"ls -la /etc/init.d/S97hmiruntime /etc/rc.d/S97hmiruntime; echo ---; grep -nE 'TOUCH|ln -sf' /etc/init.d/S97hmiruntime\n", 1.5)
    show("   init.d/rc.d 状态 + init.d 残留", out)

    # 2. 修改 init.d
    print("\n========== 2. 修改 init.d/S97hmiruntime ==========")
    out = send_line(s, b"cp /etc/init.d/S97hmiruntime /etc/init.d/S97hmiruntime.bak 2>&1; sed -i '/TOUCH_SRC/d; /TOUCH_LINK=/d; /ln -sf/d' /etc/init.d/S97hmiruntime 2>&1; echo SED_EXIT=$?; grep -nE 'TOUCH|ln -sf' /etc/init.d/S97hmiruntime; echo GREP_EXIT=$?\n", 1.5)
    show("   sed 后残留检查 (应无输出)", out)

    # 3. rc.d 若独立文件则同步修改
    print("\n========== 3. 修改 rc.d (若独立) ==========")
    out = send_line(s, b"if [ -L /etc/rc.d/S97hmiruntime ]; then echo 'rc.d is symlink, skip'; else cp /etc/rc.d/S97hmiruntime /etc/rc.d/S97hmiruntime.bak 2>&1; sed -i '/TOUCH_SRC/d; /TOUCH_LINK=/d; /ln -sf/d' /etc/rc.d/S97hmiruntime 2>&1; echo 'rc.d fixed'; grep -nE 'TOUCH|ln -sf' /etc/rc.d/S97hmiruntime; echo RC_GREP_EXIT=$?; fi\n", 1.5)
    show("   rc.d 处理", out)

    # 4. 确认修改后脚本关键段
    print("\n========== 4. 修改后关键段 ==========")
    out = send_line(s, b"sed -n '15,40p' /etc/init.d/S97hmiruntime\n", 1.0)
    show("   init.d 15-40 行", out)

    # 5. 搜索其他 ln -sf event 脚本
    print("\n========== 5. 其他 symlink 脚本 ==========")
    out = send_line(s, b"grep -rs 'ln -s.*input/event' /etc/ 2>/dev/null\n", 1.5)
    show("   其他 (应只剩 touchscreen->event2)", out)

    # 6. 删除错误 symlink
    print("\n========== 6. 删除错误 symlink ==========")
    out = send_line(s, b"rm -f /dev/input/event2 /dev/input/touchscreen; ls -la /dev/input/\n", 1.0)
    show("   删除后", out)

    # 7. 重启
    print("\n========== 7. 重启板子 ==========")
    print("[即将 reboot]")
    s.write(b"reboot\n")
    time.sleep(2)
    s.read_all()
    s.close()
    print("\n已重启! 等 25 秒后运行: sudo python3 verify_touch.py")


if __name__ == "__main__":
    main()
