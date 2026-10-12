#!/usr/bin/env python3
"""
reboot_fel.py — 发送 reboot bootloader 让 T113 进入 FEL 模式 (烧录前)
"""
import serial, time, sys

PORT = "/dev/ttyUSB0"
BAUD = 115200


def main():
    print(f"Connecting to {PORT} @ {BAUD}...")
    try:
        s = serial.Serial(PORT, BAUD, timeout=0.5)
    except PermissionError:
        print("ERROR: Permission denied. Run: sudo python3 reboot_fel.py")
        sys.exit(1)
    except Exception as e:
        print(f"ERROR: {e}")
        sys.exit(1)

    s.read_all()
    time.sleep(0.3)
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    s.read_all()

    print("Sending: reboot bootloader")
    s.write(b"reboot bootloader\n")
    time.sleep(3)
    out = b""
    while s.in_waiting:
        out += s.read(s.in_waiting)
        time.sleep(0.1)
    print("Output:", out.decode('ascii', errors='ignore'))
    s.close()
    print("板子应已进入 FEL 模式。现在可以运行 openixcli scan + flash")


if __name__ == "__main__":
    main()
