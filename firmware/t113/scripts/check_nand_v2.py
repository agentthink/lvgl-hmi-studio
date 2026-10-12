#!/usr/bin/env python3
"""
T113 NAND Flash 检测脚本 v2
修复误报问题，等待shell提示符后发送查询命令
"""

import serial
import time
import re
import sys


def main():
    port = '/dev/ttyUSB0'
    baud = 115200

    print("=" * 60)
    print("  T113 NAND Flash 检测 v2")
    print("=" * 60)

    try:
        ser = serial.Serial(port, baud, timeout=0.3)
    except Exception as e:
        print(f"无法打开串口: {e}")
        return

    print(f"[信息] 已连接 {port} @ {baud}")
    print("[信息] 监听启动日志中... (等待shell提示符)")
    print()

    buffer = b''
    shell_ready = False
    start_time = time.time()
    boot_log = []  # 保存启动日志用于后续分析

    # 阶段1: 等待启动完成，捕获日志
    while time.time() - start_time < 90:
        if ser.in_waiting:
            data = ser.read(ser.in_waiting)
            buffer += data
            text = data.decode('utf-8', errors='replace')

            # 打印启动日志（不插入误报信息）
            print(text, end='', flush=True)

            # 保存到日志列表
            for line in text.split('\n'):
                if line.strip():
                    boot_log.append(line.strip())

            # 检测shell提示符
            full_text = buffer.decode('utf-8', errors='replace')
            if re.search(r'root@.*[#\$]\s*$', full_text) or \
               re.search(r'~#\s*$', full_text) or \
               re.search(r'/ #\s*$', full_text) or \
               'login:' in full_text[-50:]:
                shell_ready = True
                print("\n\n[信息] 检测到Shell提示符/登录界面!")
                break

        time.sleep(0.01)

    # 如果是登录界面，尝试登录
    full_text = buffer.decode('utf-8', errors='replace')
    if 'login:' in full_text[-100:]:
        print("\n[信息] 检测到登录界面，尝试登录...")
        ser.write(b'root\n')
        time.sleep(2)
        # 可能需要密码
        resp = b''
        while ser.in_waiting:
            resp += ser.read(ser.in_waiting)
            time.sleep(0.2)
        text = resp.decode('utf-8', errors='replace')
        print(text, end='')
        if 'password' in text.lower():
            ser.write(b'\n')  # 空密码
            time.sleep(2)
            while ser.in_waiting:
                print(ser.read(ser.in_waiting).decode('utf-8', errors='replace'), end='')
        shell_ready = True

    if not shell_ready:
        print("\n[警告] 90秒内未检测到Shell提示符")
        print("[信息] 尝试发送回车看是否有响应...")
        ser.write(b'\n\n')
        time.sleep(2)
        resp = b''
        while ser.in_waiting:
            resp += ser.read(ser.in_waiting)
        text = resp.decode('utf-8', errors='replace')
        print(f"响应: {repr(text)}")

    # 阶段2: 发送命令查询NAND信息
    print("\n" + "=" * 60)
    print("  开始查询 NAND Flash 信息")
    print("=" * 60)

    commands = [
        "cat /proc/mtd",
        "ls /sys/class/mtd/",
        "cat /sys/class/mtd/mtd*/name 2>/dev/null",
        "cat /sys/class/mtd/mtd*/size 2>/dev/null",
        "dmesg | grep -iE 'spinand|nand.*model|nand.*size|TotalSize| PageSize|BlockSize' | head -20",
        "cat /proc/cmdline",
    ]

    results = {}
    for cmd in commands:
        print(f"\n>>> {cmd}")
        # 清空缓冲
        ser.reset_input_buffer()
        time.sleep(0.1)
        # 发送命令
        ser.write((cmd + '\n').encode())
        time.sleep(2.0)
        # 读取响应
        resp = b''
        deadline = time.time() + 3
        while time.time() < deadline:
            if ser.in_waiting:
                resp += ser.read(ser.in_waiting)
            time.sleep(0.1)
        text = resp.decode('utf-8', errors='replace')
        print(text)
        results[cmd] = text

    ser.close()

    # 阶段3: 分析启动日志中的NAND信息
    print("\n" + "=" * 60)
    print("  启动日志中的NAND相关信息")
    print("=" * 60)

    nand_lines = []
    for line in boot_log:
        # 精确匹配，避免误报
        if re.search(r'spinand|spi-nand|spi_nand|sunxi-spinand|nand.*Model|nand.*TotalSize|nand.*PageSize|nand.*BlockSize|aw-spinand|mtd\d+:', line, re.IGNORECASE):
            nand_lines.append(line)
            print(f"  {line}")

    if not nand_lines:
        print("  启动日志中未找到明确的NAND信息")

    # 提取关键参数
    print("\n" + "=" * 60)
    print("  NAND Flash 分析结果")
    print("=" * 60)

    all_text = '\n'.join(boot_log) + '\n' + '\n'.join(results.values())

    # 提取型号
    model_match = re.search(r'Model:\s*(\S+)', all_text)
    if model_match:
        print(f"  型号:     {model_match.group(1)}")

    # 提取容量
    size_match = re.search(r'TotalSize:\s*(\d+)\s*M', all_text)
    if size_match:
        print(f"  容量:     {size_match.group(1)} MB")

    # 提取页大小
    page_match = re.search(r'PageSize:\s*(\d+)\s*([KM])', all_text)
    if page_match:
        print(f"  页大小:   {page_match.group(1)} {page_match.group(2)}B")

    # 提取块大小
    block_match = re.search(r'BlockSize:\s*(\d+)\s*([KM])', all_text)
    if block_match:
        print(f"  块大小:   {block_match.group(1)} {block_match.group(2)}B")

    # 从 /proc/mtd 提取
    mtd_match = re.findall(r'(mtd\d+):\s+([0-9a-f]+)\s+([0-9a-f]+)\s+"([^"]*)"', all_text)
    if mtd_match:
        print("\n  MTD分区:")
        total_size = 0
        for dev, size_hex, erase_hex, name in mtd_match:
            size = int(size_hex, 16)
            total_size += size
            print(f"    {dev}: {size//1024}KB  \"{name}\"")
        print(f"  MTD总分区大小: {total_size//1024}KB = {total_size//1024//1024}MB")

    # 从dmesg提取制造商
    mfg_match = re.search(r'detect munufacture.*?:\s*(\w+)', all_text)
    if mfg_match:
        print(f"\n  制造商:   {mfg_match.group(1)}")

    print("\n" + "=" * 60)


if __name__ == '__main__':
    main()