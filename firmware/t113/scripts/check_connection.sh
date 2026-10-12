#!/usr/bin/env bash
# ============================================================
# T113 控制板连接体检
# 用法：
#   ./check_connection.sh [板子IP] [网口名]
#   默认板子IP: 192.168.100.100（与 deploy.sh 一致）
#   默认网口:   自动检测（优先选无 carrier 的物理网口）
#
# 检测项：
#   1. USB FEL 设备 (1f3a:efe8) - 烧写模式
#   2. USB 串口 / RNDIS 网卡枚举（正常运行模式）
#   3. 本机网口 carrier 状态
#   4. 板子 IP 可达性（ping + ARP）
#   5. SSH 端口可达性
#   6. 主机侧工具链（sunxi-fel/sshpass/picocom/adb）
#
# 退出码：0 = 板子可烧写或可达，可继续后续操作
#         1 = 未连接或工具未就绪
# ============================================================
# 注意：不使用 set -e，体检需要逐项检查到底

BOARD_IP="${1:-192.168.100.100}"
BOARD_IF_ARG="${2:-}"

# ---------- 颜色与输出 ----------
if [ -t 1 ]; then
    C_GREEN="\033[32m"; C_RED="\033[31m"; C_YELLOW="\033[33m"
    C_BLUE="\033[36m";  C_BOLD="\033[1m"; C_DIM="\033[2m"
    C_RESET="\033[0m"
else
    C_GREEN=""; C_RED=""; C_YELLOW=""; C_BLUE=""; C_BOLD=""; C_DIM=""; C_RESET=""
fi

PASS="${C_GREEN}✓${C_RESET}"
FAIL="${C_RED}✗${C_RESET}"
WARN="${C_YELLOW}!${C_RESET}"
INFO="${C_BLUE}·${C_RESET}"

PASS_CNT=0; FAIL_CNT=0; WARN_CNT=0

ok()   { echo -e "  ${PASS} $1"; PASS_CNT=$((PASS_CNT+1)); }
no()   { echo -e "  ${FAIL} $1"; FAIL_CNT=$((FAIL_CNT+1)); }
warn() { echo -e "  ${WARN} $1"; WARN_CNT=$((WARN_CNT+1)); }
info() { echo -e "  ${INFO} $1"; }

echo -e "${C_BOLD}=== T113 控制板连接体检 ===${C_RESET}"
echo    "时间:   $(date '+%Y-%m-%d %H:%M:%S')"
echo    "目标IP: ${BOARD_IP}"
echo    "主机:   $(hostname) ($(uname -sr))"
echo    ""

# ---------- 1. USB FEL 设备 ----------
echo -e "${C_BOLD}[1/6] USB - FEL 烧写模式 (1f3a:efe8)${C_RESET}"
FEL_DEVICE=$(lsusb 2>/dev/null | grep -iE "1f3a:efe8|allwinner.*fel")
if [ -n "$FEL_DEVICE" ]; then
    ok "已识别 FEL 设备: $(echo "$FEL_DEVICE" | head -1 | sed 's/.*ID //')"
    FEL_MODE=1
else
    info "未发现 FEL 模式设备（板子未上电或已正常启动）"
    FEL_MODE=0
fi

# ---------- 2. USB 串口 / 网卡枚举 ----------
echo ""
echo -e "${C_BOLD}[2/6] USB 串口 / 网卡设备${C_RESET}"

TTY_USB=$(ls /dev/ttyUSB* 2>/dev/null | tr '\n' ' ')
TTY_ACM=$(ls /dev/ttyACM* 2>/dev/null | tr '\n' ' ')
if [ -n "$TTY_USB" ]; then
    ok "USB-Serial: ${TTY_USB}"
else
    no "无 /dev/ttyUSB*（未接 TTL-USB 串口线，无法抓 UART1 日志或 RS485）"
fi
[ -n "$TTY_ACM" ] && ok "USB-CDC: ${TTY_ACM}"

# T113 正常启动后可能枚举为 USB 网卡（CDC/RNDIS）
USB_NET=$(ip -o link show 2>/dev/null | awk -F': ' '{print $2}' \
          | xargs -I{} sh -c 'ethtool -i {} 2>/dev/null | grep -qE "cdc_ether|rndis" && echo {}' \
          | tr '\n' ' ')
if [ -n "$USB_NET" ]; then
    ok "USB 网卡接口: ${USB_NET}"
else
    info "无 USB 网卡接口（CDC/RNDIS）"
fi

# dmesg 检查 sunxi 事件（需 sudo，否则给出提示）
SUNXI_DMESG=$(dmesg 2>/dev/null | grep -iE "sunxi|allwinner|1f3a" | tail -3)
if [ -n "$SUNXI_DMESG" ]; then
    ok "内核日志含 sunxi 事件"
elif [ "$(id -u)" != "0" ]; then
    info "dmesg 不可读，可加 sudo 获取内核事件"
else
    info "dmesg 无 sunxi 事件"
fi

# ---------- 3. 本机网口状态 ----------
echo ""
echo -e "${C_BOLD}[3/6] 本机网口 carrier 状态${C_RESET}"

BOARD_IF="$BOARD_IF_ARG"
if [ -z "$BOARD_IF" ]; then
    # 优先选 DOWN（无 carrier）的物理网口，更可能是连 T113 的
    for ifc in $(ip -br link show 2>/dev/null \
                  | grep -vE "^lo|^vir|^veth|^docker|^br-|^vmnet" \
                  | awk '{print $1}'); do
        if [ -f "/sys/class/net/$ifc/carrier" ] && \
           [ "$(cat /sys/class/net/$ifc/carrier 2>/dev/null)" = "0" ]; then
            BOARD_IF="$ifc"; break
        fi
    done
fi

if [ -z "$BOARD_IF" ]; then
    no "未找到候选网口（可指定：./check_connection.sh $BOARD_IP enp8s0）"
else
    CARRIER=$(cat /sys/class/net/$BOARD_IF/carrier 2>/dev/null)
    BR_LINE=$(ip -br addr show "$BOARD_IF" 2>/dev/null)
    IF_STATE=$(echo "$BR_LINE" | awk '{print $2}')
    IF_ADDR=$(echo "$BR_LINE" | awk '{print $3}')
    echo -e "  ${INFO} 候选网口: ${BOARD_IF}"
    echo -e "  ${INFO} 状态: ${IF_STATE:-?}, Carrier: ${CARRIER:-?}, 地址: ${IF_ADDR:-无}"

    if [ "$CARRIER" = "1" ]; then
        ok "${BOARD_IF} 物理连接正常（carrier=1）"
    else
        no "${BOARD_IF} 无物理连接（carrier=0）— T113 网口未接或线缆故障"
    fi
fi

# ---------- 4. 板子可达性 ----------
echo ""
echo -e "${C_BOLD}[4/6] 板子可达性 (${BOARD_IP})${C_RESET}"

if ping -c 2 -W 1 -q "$BOARD_IP" >/dev/null 2>&1; then
    ok "ping ${BOARD_IP} 成功"
    BOARD_REACHABLE=1
else
    no "ping ${BOARD_IP} 失败（板子未启动 / 网段不对 / 防火墙禁 ICMP）"
    BOARD_REACHABLE=0
fi

ARP_ENTRY=$(ip neigh show 2>/dev/null | grep "^${BOARD_IP} ")
if [ -n "$ARP_ENTRY" ]; then
    ARP_MAC=$(echo "$ARP_ENTRY" | awk '{print $5}')
    ARP_STATE=$(echo "$ARP_ENTRY" | awk '{print $NF}')
    echo -e "  ${INFO} ARP: ${ARP_MAC:-?} (${ARP_STATE})"
fi

# ---------- 5. SSH 通道 ----------
echo ""
echo -e "${C_BOLD}[5/6] SSH 通道 (root@${BOARD_IP}:22)${C_RESET}"

if [ "$BOARD_REACHABLE" = "1" ]; then
    if nc -z -w 2 "$BOARD_IP" 22 2>/dev/null; then
        ok "SSH 端口 22 可达（可用: sshpass -p tina ssh root@${BOARD_IP}）"
    else
        warn "SSH 端口 22 不可达（板子可能未启用 sshd 或被防火墙拦截）"
    fi
else
    info "跳过（板子不可达）"
fi

# ---------- 6. 主机侧工具链 ----------
echo ""
echo -e "${C_BOLD}[6/6] 主机侧工具链${C_RESET}"

check_tool() {
    local tool=$1; local desc=$2
    if command -v "$tool" >/dev/null 2>&1; then
        ok "${tool} (${desc}): $(command -v $tool)"
    else
        warn "${tool} (${desc}): 未安装"
    fi
}

check_tool sunxi-fel "FEL 烧写"
check_tool sshpass   "SSH 自动登录"
check_tool picocom   "串口终端"
check_tool minicom   "串口终端（备选）"
check_tool adb       "ADB 调试"

# ---------- 总结 ----------
echo ""
echo -e "${C_BOLD}=== 体检结论 ===${C_RESET}"
echo -e "  通过: ${C_GREEN}${PASS_CNT}${C_RESET}    \
警告: ${C_YELLOW}${WARN_CNT}${C_RESET}    \
失败: ${C_RED}${FAIL_CNT}${C_RESET}"
echo ""

if [ "$FEL_MODE" = "1" ]; then
    echo -e "${C_YELLOW}【结论】板子处于 FEL 烧写模式${C_RESET}"
    echo    "  - 可执行烧写:  sunxi-fel spiflash-write 0 <镜像路径>"
    echo    "  - 不能进行运行时测试（B 类用例需板子正常启动）"
    if ! command -v sunxi-fel >/dev/null 2>&1; then
        echo -e "  - ${C_RED}缺少 sunxi-fel${C_RESET}，安装: sudo pacman -S sunxi-tools"
    fi
elif [ "$BOARD_REACHABLE" = "1" ]; then
    echo -e "${C_GREEN}【结论】板子已正常启动且网络可达${C_RESET}"
    echo    "  - 部署固件:    ./deploy.sh ${BOARD_IP}"
    echo    "  - 进板子调试:  sshpass -p tina ssh root@${BOARD_IP}"
    echo    "  - 可执行 B 类实机测试（参考 docs/test-report.md）"
else
    echo -e "${C_RED}【结论】T113 未连接或未正常启动${C_RESET}"
    echo    "  建议:"
    echo    "   1. 检查 USB OTG 线（应有 1f3a:efe8 FEL 设备）"
    echo    "   2. 检查 ${BOARD_IF:-网口} 接线（应有 carrier）"
    echo    "   3. 检查板子供电与上电状态"
    echo    "   4. 若从未烧录，先用 sunxi-fel 烧录镜像到 SPI Flash"
fi
echo ""

# ---------- 退出码 ----------
if [ "$FEL_MODE" = "1" ] || [ "$BOARD_REACHABLE" = "1" ]; then
    exit 0
else
    exit 1
fi
