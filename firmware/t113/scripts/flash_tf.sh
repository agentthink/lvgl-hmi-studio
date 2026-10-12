#!/usr/bin/env bash
# ============================================================
# T113 TF 卡固件烧录脚本
# 用法：
#   ./flash_tf.sh                     # 自动检测 TF 卡 → 交互确认 → 烧录
#   ./flash_tf.sh /dev/sdb            # 指定设备路径直接烧录
#   ./flash_tf.sh -f /dev/sdb         # 强制烧录（跳过二次确认）
#   ./flash_tf.sh -i <img> -d <dev>  # 指定镜像和设备
#
# 镜像默认路径（$HOME 下的 Tina 构建输出；本脚本与本机 SDK 布局无关地可移植）：
#   $HOME/Tina-Linux/Tina-Linux/out/t113-mq_r/tina_t113-mq_r_uart1.img
# 可用 -i <img> 或环境变量 ZHISHU_TINA_IMG 覆盖。
#
# 注意：此脚本会 dd 整块 TF 卡，TF 卡上原有数据将被完全清除！
# ============================================================
set -e

# ---------- 默认配置 ----------
IMG_DEFAULT="${ZHISHU_TINA_IMG:-$HOME/Tina-Linux/Tina-Linux/out/t113-mq_r/tina_t113-mq_r_uart1.img}"
IMG="${IMG_DEFAULT}"
DEV=""
FORCE=0
SPEED=0

# ---------- 颜色 ----------
if [ -t 1 ]; then
    C_GREEN="\033[32m"; C_RED="\033[31m"; C_YELLOW="\033[33m"
    C_BLUE="\033[36m";  C_BOLD="\033[1m"; C_DIM="\033[2m"
    C_RESET="\033[0m"
else
    C_GREEN=""; C_RED=""; C_YELLOW=""; C_BLUE=""; C_BOLD=""; C_DIM=""; C_RESET=""
fi

PASS="${C_GREEN}OK${C_RESET}"
FAIL="${C_RED}FAIL${C_RESET}"

# ---------- 帮助 ----------
usage() {
    cat <<EOF
T113 TF 卡固件烧录工具

用法: $0 [选项] [设备路径]

选项:
  -i, --image <path>    固件镜像路径（默认: Tina SDK out/t113-mq_r/tina_t113-mq_r_uart1.img）
  -d, --device <path>   TF 卡设备路径（默认自动检测）
  -f, --force           跳过二次确认直接烧录
  -s, --speed <MB/s>    目标写入速度上限（MB/s，0=不限）
  -h, --help            显示帮助

示例:
  $0                         # 自动检测 + 交互确认
  $0 /dev/sdb                # 指定设备，需确认
  $0 -f /dev/sdb             # 强制烧录，不确认
  $0 -i ./firmware.img -d /dev/sdc -f

烧录后:
  TF 卡插入 T113 板子 → 板子优先从 SD 卡启动（插卡时）
EOF
}

# ---------- 解析参数 ----------
while [ $# -gt 0 ]; do
    case "$1" in
        -i|--image)   IMG="$2"; shift 2 ;;
        -d|--device)  DEV="$2"; shift 2 ;;
        -f|--force)   FORCE=1; shift ;;
        -s|--speed)   SPEED="$2"; shift 2 ;;
        -h|--help)    usage; exit 0 ;;
        -*)           echo "未知选项: $1"; usage; exit 1 ;;
        *)            [ -z "$DEV" ] && DEV="$1"; shift ;;
    esac
done

# ---------- 1. 检查镜像 ----------
echo -e "${C_BOLD}[1/5] 检查固件镜像${C_RESET}"

if [ ! -f "$IMG" ]; then
    echo -e "  ${FAIL} 镜像不存在: ${IMG}"
    echo "  请先编译打包: cd /home/chen/Tina-Linux/Tina-Linux && make pack"
    exit 1
fi

IMG_SIZE=$(stat --format='%s' "$IMG" 2>/dev/null || stat -c '%s' "$IMG" 2>/dev/null)
IMG_MD5=$(md5sum "$IMG" 2>/dev/null | awk '{print $1}')
echo -e "  路径: ${IMG}"
echo -e "  大小: $(numfmt --to=iec $IMG_SIZE 2>/dev/null || echo $IMG_SIZE) ($IMG_SIZE bytes)"
echo -e "  MD5:  ${IMG_MD5}"

# ---------- 2. 自动检测 TF 卡 ----------
echo ""
echo -e "${C_BOLD}[2/5] 检测 TF 卡设备${C_RESET}"

if [ -z "$DEV" ]; then
    # 优先选可移动设备（TF 卡通常标记为 removable）
    REMOVABLE=""
    FALLBACK=""
    while read -r name size typ model; do
        [ -z "$name" ] && continue
        # 检查 /sys/block/NAME/removable
        sys_rem="/sys/block/${name}/removable"
        if [ -f "$sys_rem" ] && [ "$(cat "$sys_rem" 2>/dev/null)" = "1" ]; then
            REMOVABLE="${REMOVABLE}${name} ${size} ${typ} ${model}
"
        else
            FALLBACK="${FALLBACK}${name} ${size} ${typ} ${model}
"
        fi
    done < <(lsblk -d -n -o NAME,SIZE,TYPE,MODEL 2>/dev/null | grep -E "^\s*sd[a-z]\s")

    DEVICES="${REMOVABLE:-$FALLBACK}"

    if [ -z "$DEVICES" ]; then
        echo -e "  ${FAIL} 未检测到任何 SD 设备（/dev/sdX）"
        echo "  请插入 TF 卡并重新运行，或手动指定: $0 /dev/sdb"
        exit 1
    fi

    DEV_COUNT=$(echo "$DEVICES" | grep -c .)
    if [ "$DEV_COUNT" -gt 1 ]; then
        echo "  检测到 ${DEV_COUNT} 个候选设备:"
        echo "$DEVICES" | while read -r name size typ model; do
            [ -z "$name" ] && continue
            sys_rem="/sys/block/${name}/removable"
            tag=""
            [ -f "$sys_rem" ] && [ "$(cat "$sys_rem" 2>/dev/null)" = "1" ] && tag="(可移动)"
            echo "    /dev/${name}  ${size}  ${model:-}  ${tag}"
        done
        echo ""
        read -r -p "  请输入设备名 (如 sdb): " DEV_NAME
        DEV="/dev/${DEV_NAME}"
    else
        DEV_NAME=$(echo "$DEVICES" | head -1 | awk '{print $1}')
        DEV="/dev/${DEV_NAME}"
        sys_rem="/sys/block/${DEV_NAME}/removable"
        if [ -f "$sys_rem" ] && [ "$(cat "$sys_rem" 2>/dev/null)" = "1" ]; then
            echo -e "  ${PASS} 自动选择可移动设备: ${DEV}"
        else
            echo -e "  ${C_YELLOW}自动选择: ${DEV}（非可移动设备，可能是系统盘，请确认）${C_RESET}"
        fi
    fi
fi

# ---------- 3. 验证设备 ----------
echo ""
echo -e "${C_BOLD}[3/5] 验证设备: ${DEV}${C_RESET}"

if [ ! -b "$DEV" ]; then
    echo -e "  ${FAIL} 设备不存在或不是块设备: ${DEV}"
    exit 1
fi

# 禁止烧录到系统盘：基于 removable 标志判断（非可移动设备才拦截）
DEV_NAME=$(basename "$DEV")
DEV_REMOVABLE=$(cat "/sys/block/${DEV_NAME}/removable" 2>/dev/null || echo "0")
DEV_ROTA=$(cat "/sys/block/${DEV_NAME}/rotation" 2>/dev/null || echo "?")

if [ "$DEV_REMOVABLE" != "1" ]; then
    # 非可移动设备：可能是系统盘，拒绝（除非 -f 强制）
    if [ "$FORCE" != "1" ]; then
        echo -e "  ${FAIL} 目标设备非可移动（removable=0），疑似系统盘，拒绝烧录！"
        echo "  设备: ${DEV} (removable=${DEV_REMOVABLE})"
        echo "  如确认为 TF 卡，请用 -f 强制: $0 -f ${DEV}"
        exit 1
    else
        echo -e "  ${C_YELLOW}强制模式：跳过可移动性检查（removable=${DEV_REMOVABLE}）${C_RESET}"
    fi
else
    echo -e "  ${PASS} 可移动设备确认 (removable=1)"
fi

DEV_SIZE=$(lsblk -d -n -o SIZE "$DEV" 2>/dev/null || echo "?")
DEV_MODEL=$(lsblk -d -n -o MODEL "$DEV" 2>/dev/null)
echo -e "  设备: ${DEV}"
echo -e "  型号: ${DEV_MODEL:-?}"
echo -e "  大小: ${DEV_SIZE}"

# 镜像大小不应超过设备大小
DEV_SIZE_BYTES=$(lsblk -d -n -b -o SIZE "$DEV" 2>/dev/null || echo "0")
if [ "$DEV_SIZE_BYTES" -gt 0 ] && [ "$IMG_SIZE" -gt "$DEV_SIZE_BYTES" ]; then
    echo -e "  ${FAIL} 镜像(${IMG_SIZE}) > 设备(${DEV_SIZE_BYTES})，TF 卡容量不足！"
    exit 1
fi

# ---------- 4. 确认烧录 ----------
echo ""
echo -e "${C_BOLD}[4/5] 烧录确认${C_RESET}"

if [ "$FORCE" != "1" ]; then
    echo ""
    echo -e "  ${C_YELLOW}即将执行: dd if=${IMG} of=${DEV} bs=4M status=progress${C_RESET}"
    echo -e "  ${C_RED}⚠ 此操作将清除 ${DEV} 上的所有数据！${C_RESET}"
    echo ""
    read -r -p "  输入 YES 确认烧录: " CONFIRM
    if [ "$CONFIRM" != "YES" ]; then
        echo -e "  ${C_YELLOW}已取消${C_RESET}"
        exit 0
    fi
else
    echo -e "  ${C_YELLOW}强制模式，跳过确认${C_RESET}"
fi

# ---------- 5. 执行烧录 ----------
echo ""
echo -e "${C_BOLD}[5/5] 开始烧录${C_RESET}"
echo ""

# 卸载可能挂载的分区
echo "  卸载分区..."
for part in "${DEV}"?*; do
    mountpoint=$(findmnt -n -o TARGET "$part" 2>/dev/null || true)
    if [ -n "$mountpoint" ]; then
        echo -e "  ${C_DIM}卸载 $part ($mountpoint)${C_RESET}"
        umount "$part" 2>/dev/null || umount -l "$part" 2>/dev/null || true
    fi
done

# dd 写入
DD_SPEED_ARGS=""
if [ "$SPEED" != "0" ] && [ -n "$SPEED" ]; then
    DD_SPEED_ARGS="conv=fdatasync oflag=direct"
    echo -e "  ${C_DIM}限速 ${SPEED} MB/s${C_RESET}"
fi

echo "  正在写入 ${IMG_SIZE} 字节到 ${DEV}..."
echo ""

START_TIME=$(date +%s)
if command -v pv >/dev/null 2>&1; then
    # 使用 pv 显示进度
    dd if="$IMG" bs=4M 2>/dev/null | pv -s "$IMG_SIZE" -p -t -e | sudo dd of="$DEV" bs=4M ${DD_SPEED_ARGS} status=none
else
    # 使用 dd 原生进度
    sudo dd if="$IMG" of="$DEV" bs=4M ${DD_SPEED_ARGS} status=progress
fi

# sync 确保写入完成
sudo sync
sync

END_TIME=$(date +%s)
ELAPSED=$((END_TIME - START_TIME))
WRITE_SPEED=$(echo "scale=1; $IMG_SIZE / $ELAPSED / 1048576" | bc 2>/dev/null || echo "?")
echo ""
echo -e "  ${PASS} 烧录完成！用时 ${ELAPSED} 秒 (约 ${WRITE_SPEED} MB/s)"

# ---------- 验证 ----------
echo ""
echo -e "${C_BOLD}=== 校验（读取前 ${IMG_SIZE} 字节计算 MD5）===${C_RESET}"

# 计算需要读取的 4M 块数
BLOCKS=$((IMG_SIZE / 4194304))
if [ $((IMG_SIZE % 4194304)) -gt 0 ]; then
    BLOCKS=$((BLOCKS + 1))
fi
DEV_MD5=$(sudo dd if="$DEV" bs=4M count="$BLOCKS" 2>/dev/null | head -c "$IMG_SIZE" | md5sum 2>/dev/null | awk '{print $1}')

if [ "$DEV_MD5" = "$IMG_MD5" ]; then
    echo -e "  ${PASS} MD5 校验一致"
    echo -e "  镜像: ${IMG_MD5}"
    echo -e "  设备: ${DEV_MD5}"
else
    echo -e "  ${C_RED}MD5 校验不一致！${C_RESET}"
    echo -e "  镜像: ${IMG_MD5}"
    echo -e "  设备: ${DEV_MD5}"
    echo -e "  ${C_YELLOW}可能写入不完整，建议重新烧录或更换 TF 卡${C_RESET}"
    exit 1
fi

# ---------- 完成 ----------
echo ""
echo -e "${C_GREEN}${C_BOLD}=== 烧录成功 ===${C_RESET}"
echo -e "  设备: ${DEV}"
echo -e "  镜像: ${IMG}"
echo -e "  大小: $(numfmt --to=iec $IMG_SIZE 2>/dev/null || echo $IMG_SIZE)"
echo -e "  用时: ${ELAPSED} 秒"
echo ""
echo -e "  ${C_BLUE}下一步:${C_RESET} 将 TF 卡插入 T113 板子 → 上电启动"
echo -e "  ${C_BLUE}验证:${C_RESET} 串口/SSH 登录 (root / tina), 192.168.100.100"
echo -e "  ${C_BLUE}热部署:${C_RESET} ./scripts/deploy.sh 192.168.100.100"
echo ""