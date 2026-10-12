#!/usr/bin/env python3
"""
recover_serial.py — 恢复卡在 PS2 (>) 提示符的 T113 串口 shell

原理:
- 之前 base64 上传脚本残留 stty 状态, 可能导致 ISIG 关闭, Ctrl+C 无法中断
- 卡住的命令通常是未闭合的引号/括号 (丢字符导致)
- 策略: EOF 取消 → Ctrl+C → 逐个尝试闭合符 (', ", `, ), }, ]) → 验证
- 所有发送低速逐字符, 避免 UART FIFO 溢出丢字符
"""
import serial, time, sys

PORT = "/dev/ttyUSB0"
BAUD = 115200
TEST_CMD = b"echo SHELL_OK_123\n"


def drain(s, wait=0.5):
    time.sleep(wait)
    out = b""
    while s.in_waiting:
        out += s.read(s.in_waiting)
        time.sleep(0.05)
    return out


def send_chars(s, data, inter=0.025):
    """逐字符低速发送"""
    for ch in data:
        s.write(bytes([ch]))
        time.sleep(inter)


def shell_test(s):
    """发送测试命令, 返回是否成功 (SHELL_OK_123 回显)"""
    s.write(TEST_CMD)
    out = drain(s, 0.8)
    ok = b"SHELL_OK_123" in out
    return ok, out


def main():
    print(f"Connecting to {PORT} @ {BAUD}...")
    try:
        s = serial.Serial(PORT, BAUD, timeout=0.5)
    except PermissionError:
        print("ERROR: Permission denied. Run: sudo python3 recover_serial.py")
        sys.exit(1)

    print("=== Step 0: drain initial ===")
    out = drain(s, 0.3)
    print(f"  initial: {out!r}")

    print("=== Step 1: EOF x3 (取消未完成命令) ===")
    send_chars(s, b"\x04\x04\x04")
    out = drain(s, 0.5)
    print(f"  out: {out!r}")

    print("=== Step 2: Ctrl+C x5 ===")
    send_chars(s, b"\x03\x03\x03\x03\x03")
    out = drain(s, 0.5)
    print(f"  out: {out!r}")
    ok, out = shell_test(s)
    print(f"  test: {out!r}  -> {'OK!' if ok else 'still stuck'}")
    if ok:
        print("\n*** SHELL RECOVERED! ***")
        s.close()
        return

    print("=== Step 3: try closing quotes/parens one by one ===")
    # 每个闭合符都可能是未完成语法的缺失部分
    for c in [b"'", b'"', b"`", b")", b"}", b"]"]:
        print(f"  trying closer {c!r}...")
        send_chars(s, c + b"\n")
        out = drain(s, 0.4)
        print(f"    out: {out!r}")
        ok, out = shell_test(s)
        print(f"    test: {out!r}  -> {'OK!' if ok else 'still stuck'}")
        if ok:
            print("\n*** SHELL RECOVERED! ***")
            s.close()
            return
        # 未成功, 清理: Ctrl+C + 回车重置输入
        send_chars(s, b"\x03\n")
        drain(s, 0.3)

    print("=== Step 4: try EOF inside, then Ctrl+C again ===")
    send_chars(s, b"\n\x04\n\x04\n\x03\x03")
    out = drain(s, 0.6)
    print(f"  out: {out!r}")
    ok, out = shell_test(s)
    print(f"  test: {out!r}  -> {'OK!' if ok else 'still stuck'}")

    print("\n=== Step 5: try comment char # (注释掉残留内容) ===")
    send_chars(s, b"#\n")
    out = drain(s, 0.4)
    print(f"  out: {out!r}")
    ok, out = shell_test(s)
    print(f"  test: {out!r}  -> {'OK!' if ok else 'still stuck'}")

    s.close()
    if not ok:
        print("""
=============================================================
软恢复失败。原因分析: tty 的 ISIG 可能被之前脚本关闭,
且存在未闭合命令, 普通控制字符无法中断。

建议操作 (二选一):
  A. 断电重启板子 (拔电 5 秒再上电), 串口会完全重置
  B. 如果有物理复位键, 按复位键

重启后 hmi_runtime 会自动启动 (procd respawn),
然后运行:  sudo python3 probe_network.py   (低速版)
=============================================================
""")


if __name__ == "__main__":
    main()
