#!/usr/bin/env python3
"""
T113 NAND Flash 检测脚本
通过串口连接开发板，执行命令获取NAND Flash型号和容量信息

使用方法:
    python3 check_nand.py [--port /dev/ttyUSB0] [--baud 115200] [--timeout 30]
"""

import serial
import serial.tools.list_ports
import time
import re
import sys
import argparse


def find_serial_port():
    """自动查找可用的USB串口"""
    ports = serial.tools.list_ports.comports()
    if not ports:
        return None
    for port in ports:
        if any(keyword in port.description for keyword in 
               ['USB', 'UART', 'CP210', 'CH340', 'FTDI', 'Silicon Labs', 'USB-Serial']):
            return port.device
    return ports[0].device


def wait_for_prompt(ser, timeout=30):
    """等待系统提示符出现（支持U-Boot和Linux两种）"""
    start = time.time()
    buffer = b''
    while time.time() - start < timeout:
        if ser.in_waiting:
            buffer += ser.read(ser.in_waiting)
            text = buffer.decode('utf-8', errors='replace')
            # 检测 U-Boot 提示符
            if '=>' in text:
                return 'uboot', text
            # 检测 Linux shell 提示符
            if re.search(r'root@.*#', text) or re.search(r'@.*#', text):
                return 'linux', text
            if '~#' in text or '# ' in text[-5:]:
                return 'linux', text
        time.sleep(0.1)
    return 'unknown', buffer.decode('utf-8', errors='replace')


def send_command(ser, cmd, wait_time=2.0):
    """发送命令并等待响应"""
    ser.write((cmd + '\n').encode())
    time.sleep(wait_time)
    response = b''
    while ser.in_waiting:
        response += ser.read(ser.in_waiting)
        time.sleep(0.1)
    return response.decode('utf-8', errors='replace')


def detect_nand_linux(ser):
    """在Linux系统中检测NAND Flash信息"""
    print("\n" + "=" * 60)
    print("  Linux 系统 - NAND Flash 检测")
    print("=" * 60)

    commands = [
        ("cat /proc/mtd", "MTD分区信息"),
        ("cat /sys/class/mtd/mtd0/size 2>/dev/null || echo 'N/A'", "mtd0大小(字节)"),
        ("cat /sys/class/mtd/mtd0/type 2>/dev/null || echo 'N/A'", "mtd0类型"),
        ("cat /sys/class/mtd/mtd0/ecc 2>/dev/null || echo 'N/A'", "mtd0 ECC信息"),
        ("dmesg 2>/dev/null | grep -i 'nand\\|spinand\\|flash' | head -30", "内核NAND日志"),
        ("cat /proc/mtd 2>/dev/null | head -20", "MTD分区表"),
        ("ls /sys/class/mtd/ 2>/dev/null", "MTD设备列表"),
        ("nand-info 2>/dev/null || flash_info 2>/dev/null || echo '无nand工具'", "NAND工具信息"),
    ]

    nand_model = None
    nand_size = None

    for cmd, desc in commands:
        print(f"\n>>> {desc}:")
        print(f"    命令: {cmd}")
        response = send_command(ser, cmd, wait_time=1.5)
        if response.strip():
            for line in response.strip().split('\n')[:15]:
                print(f"    | {line}")
        else:
            print("    (无输出)")

        # 尝试提取型号
        if 'Model:' in response or 'model' in response.lower():
            match = re.search(r'(?:Model|model)[:\s]+(.+)', response)
            if match:
                nand_model = match.group(1).strip()
        # 尝试提取容量
        if 'TotalSize:' in response or 'total_size' in response.lower():
            match = re.search(r'(?:TotalSize|total_size)[:\s]+(\d+)\s*M', response)
            if match:
                nand_size = match.group(1)

    return nand_model, nand_size


def detect_nand_uboot(ser):
    """在U-Boot中检测NAND Flash信息"""
    print("\n" + "=" * 60)
    print("  U-Boot 系统 - NAND Flash 检测")
    print("=" * 60)

    nand_model = None
    nand_size = None

    # U-Boot 常用NAND命令
    uboot_commands = [
        ("nand info", "NAND信息"),
        ("nand device 0", "选择NAND设备0"),
        ("nand info", "再次获取信息"),
        ("mtdparts", "MTD分区信息"),
        ("flinfo", "Flash信息"),
        ("bdinfo", "板级信息"),
        ("version", "U-Boot版本"),
    ]

    for cmd, desc in uboot_commands:
        print(f"\n>>> {desc}:")
        print(f"    命令: {cmd}")
        response = send_command(ser, cmd, wait_time=2.0)
        if response.strip():
            for line in response.strip().split('\n')[:20]:
                print(f"    | {line}")
        else:
            print("    (无输出)")

        # 解析nand info输出
        if 'NAND Device' in response or 'Page size' in response:
            # 提取页大小
            page_match = re.search(r'Page size[:\s]+(\d+)\s*([kKmM])?', response)
            # 提取块大小
            block_match = re.search(r'(?:Block|Erase) size[:\s]+(\d+)\s*([kKmM])?', response)
            # 提取总大小
            size_match = re.search(r'(?:Total|total)[:\s]+(\d+)\s*([kKmM])?\s*\(?iB\)?', response, re.IGNORECASE)
            if size_match:
                nand_size = size_match.group(1) + size_match.group(2) if size_match.group(2) else size_match.group(1)

    # 如果在U-Boot中无法识别，尝试进入Linux
    print("\n" + "-" * 40)
    print("尝试启动Linux以获取更详细的NAND信息...")
    print("如果已在Linux中请忽略此步骤")

    return nand_model, nand_size


def analyze_nand_info(model, size):
    """分析NAND Flash信息并给出结论"""
    print("\n" + "=" * 60)
    print("  检测结果汇总")
    print("=" * 60)

    if model or size:
        print(f"\n  NAND Flash 型号: {model or '未知'}")
        print(f"  NAND Flash 容量: {size or '未知'}")
    else:
        print("\n  未能从输出中解析到具体型号信息")
        print("  请查看上方完整日志获取详细信息")

    # 根据ID信息推断常见规格
    print("\n  常见 SPI NAND Flash 规格参考:")
    print("  " + "-" * 50)
    print(f"  {'容量':<10} {'页大小':<10} {'块大小':<10} {'块数':<10} {'常见型号'}")
    print("  " + "-" * 50)
    specs = [
        ("128 MB", "2 KB", "128 KB", "1024", "GD5F1GQ4, W25N01, MT29F1G01, MX35LF1GE4"),
        ("256 MB", "2 KB", "128 KB", "2048", "GD5F2GQ4, W25N02, MT29F2G01, MX35LF2GE4"),
        ("512 MB", "2 KB", "128 KB", "4096", "GD5F4GQ6, W25N04, MT29F4G01"),
    ]
    for cap, page, block, blocks, models in specs:
        print(f"  {cap:<10} {page:<10} {block:<10} {blocks:<10} {models}")
    print("  " + "-" * 50)


def main():
    parser = argparse.ArgumentParser(
        description='T113 NAND Flash 串口检测工具',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
示例:
  python3 check_nand.py                          # 自动检测串口
  python3 check_nand.py -p /dev/ttyUSB0          # 指定串口
  python3 check_nand.py -p /dev/ttyUSB0 -b 115200 # 指定串口和波特率
        """
    )
    parser.add_argument('-p', '--port', help='串口设备路径 (默认自动检测)')
    parser.add_argument('-b', '--baud', type=int, default=115200, help='波特率 (默认: 115200)')
    parser.add_argument('-t', '--timeout', type=int, default=30, help='等待提示符超时秒数 (默认: 30)')
    args = parser.parse_args()

    # 确定串口
    port = args.port
    if not port:
        port = find_serial_port()
        if not port:
            print("[错误] 未检测到可用的USB串口设备!")
            print("请确保开发板已通过USB转串口线连接到电脑")
            print("常见USB转串口芯片: CP2102/CP2103, CH340/CH341, FTDI FT232等")
            print()
            print("Linux系统下可通过以下命令查看:")
            print("  ls /dev/ttyUSB* /dev/ttyACM*")
            print("  dmesg | grep -i tty")
            sys.exit(1)
        print(f"[信息] 自动检测到串口: {port}")

    # 连接串口
    print(f"[信息] 正在连接 {port} @ {args.baud} ...")
    try:
        ser = serial.Serial(
            port=port,
            baudrate=args.baud,
            timeout=0.5,
            write_timeout=0.5,
        )
    except serial.SerialException as e:
        print(f"[错误] 无法打开串口 {port}: {e}")
        print("可能原因:")
        print("  1. 串口被其他程序占用 (如minicom, picocom)")
        print("  2. 权限不足 (尝试: sudo chmod 666 /dev/ttyUSB0)")
        print("  3. 串口设备不存在或已断开")
        sys.exit(1)

    print("[信息] 串口连接成功")
    print("[信息] 正在等待系统启动...")

    # 清空串口缓冲
    time.sleep(0.5)
    ser.reset_input_buffer()
    ser.reset_output_buffer()

    # 发送回车触发提示符
    ser.write(b'\n')
    time.sleep(0.5)

    # 等待提示符
    system_type, initial_output = wait_for_prompt(ser, timeout=args.timeout)

    print(f"\n[信息] 检测到系统类型: {system_type.upper()}")

    if system_type == 'linux':
        model, size = detect_nand_linux(ser)
    elif system_type == 'uboot':
        model, size = detect_nand_uboot(ser)
    else:
        print("[警告] 无法识别系统类型，尝试发送命令...")
        print("\n>>> 尝试检测 (发送help命令):")
        response = send_command(ser, 'help', wait_time=1.0)
        print(response[:500])
        print("\n>>> 尝试检测 (发送cat /proc/mtd):")
        response = send_command(ser, 'cat /proc/mtd', wait_time=2.0)
        print(response[:500])
        model, size = None, None

    analyze_nand_info(model, size)

    # 关闭串口
    ser.close()
    print("\n[信息] 串口已关闭，检测完成")


if __name__ == '__main__':
    main()