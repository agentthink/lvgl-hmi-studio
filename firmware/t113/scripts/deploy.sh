#!/usr/bin/env bash
# ============================================================
# 部署 hmi_runtime 到 T113 开发板
# 用法：
#   ./deploy.sh <IP> [二进制路径]
#   默认二进制: ../app/hmi_runtime（先交叉编译）
#
# 方式：
#   1) SSH/SCP（推荐，开发板默认 IP 192.168.100.100，root/tina）
#   2) ADB：adb push 到 /mnt/UDISK 后手动拷贝
#   3) U 盘：拷贝到 U 盘根目录，插板后 /mnt/UDISK
# ============================================================
set -e

IP="${1:-192.168.100.100}"
BIN="${2:-$(dirname "$0")/../app/hmi_runtime}"
USER=root
PASS=tina

[ -f "$BIN" ] || {
    echo "error: 未找到 $BIN，请先在 app/ 下交叉编译（见 README.md）"
    exit 1
}

echo "==> 上传 $BIN 到 $IP:/tmp/ (sshpass root/tina)"
sshpass -p "$PASS" scp -o StrictHostKeyChecking=no "$BIN" "$USER@$IP:/tmp/hmi_runtime"

echo "==> 安装并启动"
sshpass -p "$PASS" ssh -o StrictHostKeyChecking=no "$USER@$IP" '
    killall hmi_runtime 2>/dev/null || true
    cp /tmp/hmi_runtime /usr/bin/hmi_runtime
    chmod +x /usr/bin/hmi_runtime
    /etc/init.d/S99hmiruntime restart 2>/dev/null || /usr/bin/hmi_runtime >/var/log/hmi_runtime.log 2>&1 &
    sleep 1
    pgrep -x hmi_runtime && echo "hmi_runtime running" || echo "FAILED: 查看 /var/log/hmi_runtime.log"
'
echo "==> 完成"
