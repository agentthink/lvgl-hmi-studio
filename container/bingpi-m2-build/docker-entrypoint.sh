#!/bin/bash
# Docker 容器入口脚本
# 用于 Tina Linux 固件编译

set -e

# 颜色输出
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}=== Tina Linux 固件编译容器 ===${NC}"
echo "工作目录: $PWD"
echo "TINA_TOP: $TINA_TOP"
echo "ARCH: $ARCH"

# 检查 Tina SDK 是否挂载
if [ ! -f "$TINA_TOP/build/envsetup.sh" ]; then
    echo -e "${YELLOW}警告: Tina SDK 未挂载到 $TINA_TOP${NC}"
    echo "请使用 -v 挂载 Tina SDK："
    echo "  docker run -v /path/to/Tina:$TINA_TOP -v /path/to/out:$OUT_DIR <image>"
fi

# 执行传入的命令
exec "$@"