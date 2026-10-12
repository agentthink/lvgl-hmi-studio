#!/usr/bin/env python3
"""
串口监听脚本 - 等待开发板启动并捕获NAND Flash信息
"""

import serial
import time
import re
import sys

def main():
    print("=" * 60)
    print("  T113 串口监听 - 等待开发板启动")
    print("=" * 60)
    print("\n请重启开发板 (断电后重新上电)...")
    print("监听中... (按 Ctrl+C 停止)\n")

    try:
        ser = serial.Serial('/dev/ttyUSB0', 115200, timeout=0.5)
    except Exception as e:
        print(f"无法打开串口: {e}")
        return

    buffer = b''
    nand_detected = False
    start_time = time.time()

    try:
        while True:
            if ser.in_waiting:
                data = ser.read(ser.in_waiting)
                buffer += data
                text = data.decode('utf-8', errors='replace')
                print(text, end='', flush=True)

                # 检测 NAND Flash 信息
                text_all = buffer.decode('utf-8', errors='replace')
                if 'Model:' in text_all and 'nand' in text_all.lower():
                    print("\n" + "=" * 60)
                    print("  检测到 NAND Flash 信息!")
                    print("=" * 60)
                    nand_detected = True

                # 检测提示符
                if '=>' in text or re.search(r'root@.*#', text):
                    print("\n\n[检测到提示符，可以发送命令]")
                    # 发送NAND检测命令
                    if not nand_detected:
                        print("\n>>> 发送: cat /proc/mtd")
                        ser.write(b'cat /proc/mtd\n')
                        time.sleep(1)
                        print("\n>>> 发送: dmesg | grep -i nand")
                        ser.write(b'dmesg | grep -i nand\n')
                        time.sleep(1)

            time.sleep(0.01)

    except KeyboardInterrupt:
        print("\n\n[用户中断，停止监听]")
    finally:
        ser.close()

    # 分析缓冲区内容
    text = buffer.decode('utf-8', errors='replace')
    print("\n" + "=" * 60)
    print("  分析结果")
    print("=" * 60)

    # 提取关键信息
    patterns = [
        (r'Model:\s*(\S+)', 'NAND型号'),
        (r'TotalSize:\s*(\d+)\s*M', '容量'),
        (r'PageSize:\s*(\d+)\s*[KM]', '页大小'),
        (r'BlockSize:\s*(\d+)\s*[KM]', '块大小'),
        (r'spinand.*?:\s*(.+)', 'SPI NAND信息'),
    ]

    for pattern, desc in patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match:
            print(f"  {desc}: {match.group(1)}")

    print("=" * 60)

if __name__ == '__main__':
    main()