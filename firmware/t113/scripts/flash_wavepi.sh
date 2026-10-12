#!/usr/bin/env bash
# ============================================================
# WavePI-S6 (T113-S3) 固件刷写脚本
# 用于在 USB 供电条件良好时一键刷写自研固件
#
# 用法：
#   ./flash_wavepi.sh                    # 自动检测 FEL 并刷写完整固件
#   ./flash_wavepi.sh --test             # 刷写测试镜像（factory boot_package）
#   ./flash_wavepi.sh --factory          # 刷回官方 factory 镜像
#   ./flash_wavepi.sh --check            # 检查 USB 供电/通信状态
#
# 前置条件（重要！）：
#   1. 板子必须有稳定供电（5V/1A 充电头或移动电源），
#      仅 USB 供电会导致传输超时（实测窗口仅 ~5 秒）
#   2. USB 数据线必须是高质量数据线（非充电线）
#   3. 板子进入 FEL 模式：按住 FEL 键 + 按一下 RST
# ============================================================
set -e

# 颜色
C_GREEN="\033[32m"; C_RED="\033[31m"; C_YELLOW="\033[33m"
C_BLUE="\033[36m"; C_BOLD="\033[1m"; C_RESET="\033[0m"
OK="${C_GREEN}OK${C_RESET}"; FAIL="${C_RED}FAIL${C_RESET}"

# 路径（相对仓库定位，避免写死开发机家目录；均可用环境变量覆盖）
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
SDK_OUT="${ZHISHU_TINA_SDK_OUT:-$REPO_ROOT/firmware/Tina-SDK/out/t113-bingpi_m2}"
IMG_CUSTOM="$SDK_OUT/tina_t113-bingpi_m2_uart3.img"
IMG_TEST="$SDK_OUT/final_test.img"
FACTORY_IMG=$(find "${ZHISHU_FACTORY_IMG_DIR:-$HOME/下载/WavePi}" -name "*tina*7.img" -type f 2>/dev/null | head -1)

MODE="custom"

usage() {
    cat <<EOF
WavePI-S6 固件刷写脚本

用法: $0 [选项]

选项:
  --custom    刷写完整自研固件（默认）
  --test      刷写测试镜像（factory boot_package + 自研内核/rootfs）
  --factory   刷回官方 factory 镜像
  --check     检查 USB 供电/通信状态
  -h, --help  显示帮助
EOF
}

# USB 供电检查
check_usb() {
    echo -e "${C_BOLD}[检查] USB 供电/通信状态${C_RESET}"
    
    # 检查 FEL 设备
    if ! lsusb 2>/dev/null | grep -q '1f3a:efe8'; then
        echo -e "  ${FAIL} 未检测到 FEL 设备"
        echo "  请按住 FEL 键 + 按一下 RST 键"
        return 1
    fi
    echo -e "  ${OK} FEL 设备已检测到"
    
    # 检查 USB 速度
    for d in /sys/bus/usb/devices/*/; do
        if [ -f "$d/idVendor" ] && [ "$(cat $d/idVendor 2>/dev/null)" = "1f3a" ]; then
            speed=$(cat $d/speed 2>/dev/null)
            if [ "$speed" = "480" ]; then
                echo -e "  ${OK} USB 速度: 480M (High Speed) - 供电良好"
            else
                echo -e "  ${YELLOW}⚠ USB 速度: ${speed}M (Full Speed) - 可能供电不足${C_RESET}"
                echo "    建议使用 5V/1A 独立电源供电"
            fi
        fi
    done
    
    # 传输测试（pyusb 64B）
    if python3 -c "
import usb.core
dev = usb.core.find(idVendor=0x1f3a, idProduct=0xefe8)
if not dev: exit(1)
dev.write(0x01, b'\x00'*64, timeout=3000)
print('传输 OK')
" 2>/dev/null; then
        echo -e "  ${OK} USB 传输正常"
        return 0
    else
        echo -e "  ${FAIL} USB 传输超时"
        echo "  原因：供电不足（仅 USB 供电时窗口仅 ~5 秒）"
        echo "  解决：连接 5V/1A 充电头或移动电源给板子供电"
        return 1
    fi
}

# 刷写主流程
flash() {
    local img="$1"
    local label="$2"
    
    echo -e "${C_BOLD}[刷写] $label${C_RESET}"
    echo "  镜像: $img ($(du -h "$img" | cut -f1))"
    
    if [ ! -f "$img" ]; then
        echo -e "  ${FAIL} 镜像不存在: $img"
        return 1
    fi
    
    # 先检查 USB
    if ! check_usb; then
        echo -e "  ${FAIL} USB 检查未通过，中止刷写"
        return 1
    fi
    
    # 立即刷写（趁 USB 状态好）
    echo -e "  ${C_BLUE}开始刷写...${C_RESET}"
    openixcli flash -m full_erase -a reboot "$img" 2>&1
    local rc=$?
    
    if [ $rc -eq 0 ]; then
        echo -e "  ${OK} 刷写成功！板子将自动重启"
    else
        echo -e "  ${FAIL} 刷写失败（可能是 USB 供电不稳）"
        echo "  请：1) 连接独立电源  2) 重新插拔 USB  3) 重试"
    fi
    return $rc
}

# 主逻辑
case "$1" in
    --test)
        flash "$IMG_TEST" "测试镜像（factory boot_package + 自研内核/rootfs）"
        ;;
    --factory)
        flash "$FACTORY_IMG" "官方 factory 镜像"
        ;;
    --check)
        check_usb
        ;;
    -h|--help)
        usage
        ;;
    *)
        flash "$IMG_CUSTOM" "完整自研固件"
        ;;
esac
