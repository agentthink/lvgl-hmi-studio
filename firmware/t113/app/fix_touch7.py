#!/usr/bin/env python3
"""
fix_touch7.py — 当前会话立即修复触摸 (免重启)

关键: overlay 在 /tmp (tmpfs), 重启即丢 → 必须当前会话修复 + 重编固件固化

步骤:
1. 确认 rmmod/insmod 可用 + 找 gt9xxnew_ts 模块路径
2. 删错误 symlink
3. rmmod + insmod gt9xxnew_ts → devtmpfs 重建 event2 节点
4. 重启 hmi_runtime (让 procd respawn 重新 open 正确的 event2)
5. dd 触摸验证
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
        print("ERROR: Permission denied. Run: sudo python3 fix_touch7.py")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    s.read_all()

    # 1. 工具 + 模块路径
    print("\n========== 1. 检查工具 ==========")
    out = send_line(s, b"which rmmod insmod modprobe 2>&1; echo ---; find /lib/modules -name '*gt9xx*' 2>/dev/null; ls /lib/modules/ 2>&1\n", 1.5)
    show("   rmmod/insmod + 模块路径", out)

    # 2. 停 hmi_runtime
    print("\n========== 2. 停止 hmi_runtime ==========")
    out = send_line(s, b"killall hmi_runtime 2>&1; sleep 1; pidof hmi_runtime; echo KILLED\n", 2.0)
    show("   已停止", out)

    # 3. 删 symlink
    print("\n========== 3. 删除错误 symlink ==========")
    out = send_line(s, b"rm -f /dev/input/event2 /dev/input/touchscreen; ls -la /dev/input/\n", 1.0)
    show("   删除后", out)

    # 4. rmmod + insmod 重建节点
    print("\n========== 4. 重载 gt9xxnew_ts ==========")
    out = send_line(s, b"rmmod gt9xxnew_ts 2>&1; echo RMMOD_EXIT=$?; sleep 1; cat /proc/modules | grep gt9\n", 1.5)
    show("   rmmod 结果 (应无 gt9xx 行)", out)
    # 找模块并加载
    out = send_line(s, b"KO=$(find /lib/modules -name '*gt9xx*.ko' 2>/dev/null | head -1); echo KO=$KO; insmod $KO 2>&1; echo INSMOD_EXIT=$?; sleep 2; ls -la /dev/input/\n", 2.5)
    show("   insmod 后 /dev/input/", out)

    # 5. 检查 event2
    out = send_line(s, b"ls -la /dev/input/event2; cat /proc/bus/input/devices | grep -A2 gt9xx\n", 1.0)
    show("5. event2 状态", out)

    # 6. 启动 hmi_runtime (procd)
    print("\n========== 6. 启动 hmi_runtime ==========")
    out = send_line(s, b"/etc/init.d/S97hmiruntime start 2>&1; sleep 4; pidof hmi_runtime; ls -la /proc/$(pidof hmi_runtime)/fd/ 2>&1 | grep -E 'event|input'\n", 5.0)
    show("   fd (应含 event2)", out)

    # 7. 触摸验证
    print("\n" + "=" * 62)
    print(f">>> 触摸测试: 请连续触摸屏幕 {DD_SECONDS} 秒 <<<")
    print("=" * 62)
    s.write(b"dd if=/dev/input/event2 of=/tmp/t.dat bs=24 count=5 2>&1 & sleep 6; ls -l /tmp/t.dat; wc -c /tmp/t.dat 2>&1\n")
    time.sleep(DD_SECONDS)
    out = read_all_avail(s, 1.0)
    show("7. dd 触摸测试", out)
    s.write(b"kill %1 2>/dev/null; rm -f /tmp/t.dat\n")
    time.sleep(0.5)
    s.read_all()

    s.close()
    print("\n========== 修复完成 ==========")
    print("若 event2 为 c 13:66 + fd 含 event2 + 触摸数据>0 → 当前会话触摸已恢复!")
    print("注意: 重启会丢失! 需重编固件固化 (SDK 源文件已改好)")


if __name__ == "__main__":
    main()
