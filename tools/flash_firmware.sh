#!/usr/bin/env bash
# ============================================================
# flash_firmware.sh — T113 实机固件烧录（需在宿主机终端运行，非沙箱）
# 前置：板子已进入 FEL 模式（按住 FEL 键 → 按一下 RST 松开）
# 用法：./tools/flash_firmware.sh [镜像路径]
# 默认镜像：firmware/Tina-Linux/out/t113-mq_r/tina_t113-mq_r_uart1.img
# ============================================================
set -e
IMG="${1:-$(dirname "$0")/../firmware/Tina-Linux/out/t113-mq_r/tina_t113-mq_r_uart1.img}"
IMG="$(cd "$(dirname "$IMG")" && pwd)/$(basename "$IMG")"

echo "== 1. 检测 FEL 设备 =="
if ! lsusb 2>/dev/null | grep -q "1f3a:efe8"; then
    echo "!! 未检测到 FEL 设备 (1f3a:efe8)。"
    echo "   请按住开发板 FEL 键 → 按一下 RST 键松开（保持 FEL 按住到烧录开始提示）"
    exit 1
fi
echo "   FEL 设备在线 ✓"

echo "== 2. 校验镜像 =="
ls -la "$IMG"
echo "   md5: $(md5sum "$IMG" | awk '{print $1}')"

echo "== 3. 开始烧录（全盘擦除 + 烧完自动重启）=="
echo "   镜像: $IMG"
openixcli flash -m full_erase -a reboot "$IMG"

echo "== 4. 烧录完成，等待板子重启上线 =="
for i in $(seq 1 60); do
    if ping -c 1 -W 1 192.168.100.100 >/dev/null 2>&1; then
        echo "   板子网络已恢复 ($((i*2))s) ✓"
        break
    fi
    sleep 2
done
echo "== 完成。板子应从新固件启动（开机 5 秒点击窗口 → 设置 / 超时进运行页）=="
