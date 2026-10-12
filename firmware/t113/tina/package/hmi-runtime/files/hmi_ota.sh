#!/bin/sh
# ============================================================================
# hmi_ota.sh — zhishu-HMI 三通道 OTA 统一触发脚本
#
# 用法:
#   hmi_ota.sh <fw|hmi> <tf|usb|net> <source>
#
# 参数:
#   target  : fw  整固件 OTA（recovery→kernel→usr 三阶段 + 系统 reboot）
#             hmi 仅 HMI .so OTA（单步 raw 文件写入 + hmi_runtime 进程重启）
#   channel : tf  从 TF 卡读取 .swu（自动挂载 /dev/mmcblk0p1 → /mnt/SDCARD）
#             usb 从 USB 存储读取 .swu（自动挂载 /dev/sda1 → /mnt/usb）
#             net 从 HTTP URL 下载 .swu
#   source  : tf/usb 时为 .swu 文件名（在挂载根目录下）或绝对路径
#             net  时为 .swu 的完整 HTTP URL
#
# 示例:
#   hmi_ota.sh fw  tf  firmware.swu
#   hmi_ota.sh hmi usb hmi-ui-v2.swu
#   hmi_ota.sh fw  net http://192.168.1.100/t113/firmware.swu
#   hmi_ota.sh hmi net http://192.168.1.100/hmi/hmi-ui-v2.swu
#
# 依赖: swupdate、uboot-envtools (fw_setenv/fw_printenv)
# ============================================================================

set -e

usage() {
    echo "Usage: $0 <fw|hmi> <tf|usb|net> <source>"
    echo "  fw  : full firmware OTA (3-stage recovery→kernel→usr, system reboot)"
    echo "  hmi : HMI UI .so OTA (single step, hmi_runtime process restart)"
    echo "  tf/usb : .swu filename or path on TF card / USB storage"
    echo "  net    : full HTTP URL of .swu"
    exit 1
}

[ $# -eq 3 ] || usage

TARGET=$1
CHANNEL=$2
SOURCE=$3

# 解析 swupdate 的 -e <software>,<mode> 参数
case "$TARGET" in
    fw)
        SWU_SOFTWARE="stable"
        SWU_MODE="upgrade_recovery"
        ;;
    hmi)
        SWU_SOFTWARE="stable"
        SWU_MODE="upgrade_hmi_ui"
        ;;
    *)
        echo "[OTA] unknown target: $TARGET (expect fw|hmi)"
        usage
        ;;
esac

# swupdate 依赖 /var/lock/fw_xxx.lock
mkdir -p /var/lock

# 通道处理
case "$CHANNEL" in
    tf)
        MNT=/mnt/SDCARD
        mkdir -p "$MNT"
        # 若 source 是绝对路径且文件存在，直接用；否则尝试挂载 TF 卡
        case "$SOURCE" in
            /*) [ -f "$SOURCE" ] || { echo "[OTA] swu not found: $SOURCE"; exit 1; }; SWU_FILE="$SOURCE" ;;
            *)
                mount | grep -q " $MNT " || mount /dev/mmcblk0p1 "$MNT" 2>/dev/null || \
                    mount /dev/mmcblk0 "$MNT" 2>/dev/null || true
                SWU_FILE="$MNT/$SOURCE"
                [ -f "$SWU_FILE" ] || { echo "[OTA] swu not found: $SWU_FILE"; exit 1; }
                ;;
        esac
        echo "[OTA] $TARGET firmware from TF: $SWU_FILE"
        swupdate -v -i "$SWU_FILE" -e "$SWU_SOFTWARE,$SWU_MODE"
        ;;
    usb)
        MNT=/mnt/usb
        mkdir -p "$MNT"
        case "$SOURCE" in
            /*) [ -f "$SOURCE" ] || { echo "[OTA] swu not found: $SOURCE"; exit 1; }; SWU_FILE="$SOURCE" ;;
            *)
                mount | grep -q " $MNT " || mount /dev/sda1 "$MNT" 2>/dev/null || \
                    mount /dev/sda "$MNT" 2>/dev/null || true
                SWU_FILE="$MNT/$SOURCE"
                [ -f "$SWU_FILE" ] || { echo "[OTA] swu not found: $SWU_FILE"; exit 1; }
                ;;
        esac
        echo "[OTA] $TARGET firmware from USB: $SWU_FILE"
        swupdate -v -i "$SWU_FILE" -e "$SWU_SOFTWARE,$SWU_MODE"
        ;;
    net)
        echo "[OTA] $TARGET firmware from net: $SOURCE"
        swupdate -d -u "$SOURCE" -e "$SWU_SOFTWARE,$SWU_MODE"
        ;;
    *)
        echo "[OTA] unknown channel: $CHANNEL (expect tf|usb|net)"
        usage
        ;;
esac

echo "[OTA] swupdate finished, swu_next will be handled by swupdate_cmd.sh"
