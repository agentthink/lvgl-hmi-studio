#!/bin/bash
set -e

# Tina Linux 固件构建脚本
# 用法: ./build.sh [board] [target]

BOARD=${1:-bingpi_m2}
TARGET=${2:-tina_t113-bingpi_m2_uart3}
CONFIG_DIR="/workspace/Tina/device/config/chips/t113/configs/${BOARD}/linux-5.4"

echo "=== Tina Linux 固件构建 ==="
echo "Board: $BOARD"
echo "Target: $TARGET"

# 检查配置目录
if [ ! -d "$CONFIG_DIR" ]; then
    echo "错误: 配置目录不存在 $CONFIG_DIR"
    exit 1
fi

# 设置源码路径
TINA_TOP="/workspace/Tina"
source "$TINA_TOP/build/envsetup.sh"

# 选择目标
lunch "${TARGET}-tina"

# 构建
make -j$(nproc)

# 输出产物说明
echo "=== 构建完成 ==="
echo "固件产物: $TINA_TOP/out/$BOARD/$TARGET.img"
ls -lh "$TINA_TOP/out/$BOARD/"*.img 2>/dev/null || echo "未生成固件"
