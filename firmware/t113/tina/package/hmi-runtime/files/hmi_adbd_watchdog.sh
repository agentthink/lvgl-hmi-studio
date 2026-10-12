#!/bin/sh
# adbd 看护循环（由 S97hmiruntime 开机启动，见该脚本里的 "adbd 看护" 段）
#
# 为什么需要它：
#   本镜像（bingpi_m2 / WavePI-S6）**没有 procd**，rcS 只通过
#   /etc/init.d/load_script.conf 在开机时跑一次 `/etc/init.d/adbd "boot" &`；
#   之后 adbd 若退出，**没有任何机制把它拉起来** ⇒ USB 调试通道消失，只能重启板子。
#   实机取证（2026-10-10，采样器 /mnt/UDISK/diag/watch.log）：
#       [01-01 00:16:06] !!! adbd GONE  (uptime=966.08 10.02)
#   杀掉 adbd 后 25s 复核 `pidof adbd` 仍为空、USB 侧 `adb devices` 也空了。
#
# 做法：每 30s pidof 一次；不在就用**与 rcS 完全相同**的方式拉起
#   （`/etc/init.d/adbd boot`，rc.common 内部的 USE_PROCD 分支会把 action 背景化，
#    实测该路径在开机时确实能把 adbd 跑起来）。用 pidof 判断而不是 pid 文件，
#    因为 adbd 可能被 OEM 方式重新拉起。
#
# 代价：一个常驻的 busybox ash（每 30s 醒来执行一次 pidof），可忽略。

LOG=/mnt/UDISK/adbd_watchdog.log
[ -d /mnt/UDISK ] || LOG=/tmp/adbd_watchdog.log

while :; do
    sleep 30
    if ! pidof adbd >/dev/null 2>&1; then
        echo "$(cat /proc/uptime) adbd gone -> /etc/init.d/adbd boot" >>"$LOG" 2>/dev/null
        /etc/init.d/adbd boot >/dev/null 2>&1
        sleep 5
        echo "$(cat /proc/uptime) after restart: adbd=[$(pidof adbd)]" >>"$LOG" 2>/dev/null
    fi
done
