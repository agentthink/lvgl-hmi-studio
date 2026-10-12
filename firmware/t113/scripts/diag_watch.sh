#!/bin/sh
# ============================================================================
# diag_watch.sh — 设备侧后台采样器：把「某一刻发生了什么」持续落到 UDISK
#
# 用途：排查**间歇性**故障。典型是 adbd 在开机几分钟后停止应答（gadget 仍在
# USB 上、板子没重启、无 OOM），但那一刻的内核日志/进程状态在 adb 掉线后就取
# 不回来了。本脚本在 UDISK 上持续留痕 —— UDISK 是持久分区，重启不丢，等板子
# 下次起来再 `adb pull` 就能看到「掉线前后」的现场。
#
# ⚠ 设备侧 busybox 很精简（实测无 head / tail / wc / tr），所以本脚本只用
#    grep / cat / echo / date / ps / pidof / mkdir / sleep 这些一定存在的。
#
# 部署（主机侧）：
#   adb push firmware/t113/scripts/diag_watch.sh /tmp/
#   adb shell "chmod +x /tmp/diag_watch.sh; \
#              start-stop-daemon -S -b -x /bin/sh -- /tmp/diag_watch.sh"
#   # 必须用 start-stop-daemon -b 脱离 adb 会话：否则 adb 一掉，采样器跟着死
#
# 取回：
#   adb pull /mnt/UDISK/diag/            # watch.log（时间线）+ dmesg.last（末次内核日志）
#
# 可调：DIAG_DIR（默认 /mnt/UDISK/diag）、DIAG_INTERVAL（默认 10 秒）
# ============================================================================

DIR="${DIAG_DIR:-/mnt/UDISK/diag}"
INTERVAL="${DIAG_INTERVAL:-10}"
LOG="$DIR/watch.log"

# 本脚本的价值全在「重启后还能读到」⇒ 必须落在持久分区。UDISK 没挂上时明确降级
# 到 /tmp 并在日志里写明，免得事后以为日志丢了是别的原因。
PERSIST=1
if [ -z "${DIAG_DIR:-}" ] && ! grep -q " /mnt/UDISK " /proc/mounts 2>/dev/null; then
    DIR="/tmp/diag"
    PERSIST=0
fi

mkdir -p "$DIR" 2>/dev/null

log() {
    echo "[$(date '+%m-%d %H:%M:%S')] $*" >> "$LOG"
}

log "=== diag_watch start (interval=${INTERVAL}s pid=$$) ==="
[ "$PERSIST" = "0" ] && log "!!! WARNING /mnt/UDISK 未挂载 ⇒ 落到 $DIR（重启即丢，仅供在线观察）"

prev_adbd=1
prev_hmi=1

while :; do
    up="$(cat /proc/uptime 2>/dev/null)"
    adbd="$(pidof adbd 2>/dev/null)"
    hmi="$(pidof hmi_runtime 2>/dev/null)"

    # 状态**变化**单独记一行并标感叹号：这是查「哪一刻没的」最快的锚点
    if [ -z "$adbd" ] && [ "$prev_adbd" = "1" ]; then
        log "!!! adbd GONE  (uptime=$up)"
        dmesg > "$DIR/dmesg.at-adbd-gone" 2>/dev/null
        prev_adbd=0
    fi
    if [ -n "$adbd" ] && [ "$prev_adbd" = "0" ]; then
        log "+++ adbd back  (pid=$adbd)"
        prev_adbd=1
    fi
    if [ -z "$hmi" ] && [ "$prev_hmi" = "1" ]; then
        log "!!! hmi_runtime GONE (uptime=$up)"
        prev_hmi=0
    fi
    if [ -n "$hmi" ] && [ "$prev_hmi" = "0" ]; then
        log "+++ hmi_runtime back (pid=$hmi)"
        prev_hmi=1
    fi

    {
        echo "[$(date '+%m-%d %H:%M:%S')] up=$up adbd=${adbd:-none} hmi=${hmi:-none}"
        grep -E 'MemFree|MemAvailable|Committed_AS|CommitLimit' /proc/meminfo 2>/dev/null
    } >> "$LOG"

    # 末次内核日志快照：覆盖写（历史靠 watch.log 的时间线对齐），
    # 这样 adbd 掉线那一刻若内核有痕迹，重启后仍能读到
    dmesg > "$DIR/dmesg.last" 2>/dev/null

    sleep "$INTERVAL"
done
