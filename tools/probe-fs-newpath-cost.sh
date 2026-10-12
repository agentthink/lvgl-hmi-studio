#!/usr/bin/env bash
# ============================================================
# probe-fs-newpath-cost.sh — 量化「首次出现的路径」在本沙箱的开销及其串行性
#
# 用途：门禁/测试出现「外部命令（git 等）在满载时慢到秒级甚至十秒级」时，
# 先用本脚本判断是不是环境问题（而不是产品缺陷）。判据与实测见
# docs/2026-09-17-sandbox-newpath-serialization.md。
#
# 为什么不测产品代码：本脚本**完全不碰 .NET**，只用 shell + 文件操作 + git。
# 若纯 shell 就能复现，根因就与测试宿主、与 git、与被测代码无关。
#
# 用法：bash tools/probe-fs-newpath-cost.sh
# 退出码：0 = 正常跑完（结论看输出）；非 0 = 环境不满足（缺 git）。
#
# 落盘位置：系统临时目录下的 hmi-fs-newpath-probe-<pid>/（约 2500 个 0 字节文件，空闲后自删）。
# 为什么放系统临时目录而不是仓库内：这类探针文件数远超任何批量删除阈值，放在系统临时目录
# 才删得干净（临时目录是"用完即弃"语义的正确落点）。
# ============================================================
set -uo pipefail

TMPBASE="${TMPDIR:-/tmp}"
[ -d "$TMPBASE" ] || { echo "临时目录不存在：$TMPBASE" >&2; exit 1; }
DIR="$TMPBASE/hmi-fs-newpath-probe-$$"

command -v git >/dev/null 2>&1 || { echo "缺 git" >&2; exit 1; }

rm -rf "$DIR"
mkdir -p "$DIR"

now() { date +%s%N; }
msd() { echo $(( ($2 - $1) / 1000000 )); }
stats() { printf '%s\n' "$@" | sort -n | awk '{a[NR]=$1} END {printf "min=%-6s p50=%-6s max=%-6s", a[1], a[int((NR+1)/2)], a[NR]}'; }

echo "== 环境 =="
echo "  nproc=$(nproc)  loadavg=$(cut -d' ' -f1-3 /proc/loadavg)  git=$(git --version | awk '{print $3}')"
echo "  落点 $DIR  卷=$(df -h "$DIR" | awk 'NR==2{print $1" "$2}')"
echo

# ── [1] 开销是不是「按新路径」线性？ ──
echo "== [1] 全新目录 + N 个新文件（每档 4 轮，均值）=="
prev=""
for n in 1 2 5 10 20 40; do
    t=()
    for i in 1 2 3 4; do
        D="$DIR/lin-${n}-${i}"
        mkdir -p "$D"
        S=$(now)
        for j in $(seq "$n"); do : > "$D/f$j"; done
        E=$(now)
        t+=("$(msd "$S" "$E")")
    done
    avg=$(printf '%s\n' "${t[@]}" | awk '{s+=$1} END {printf "%.0f", s/NR}')
    echo "  N=$(printf '%-3s' "$n") 均值 ${avg} ms   $prev"
    prev="（= $(awk -v a="$avg" -v n="$n" 'BEGIN{printf "%.0f", a/n}') ms/文件）"
done
echo

# ── [2] 串行还是可并行？ ──
echo "== [2] 8 进程并行 vs 单进程（各自全新目录，各 20 个新文件）=="
S=$(now)
D="$DIR/serial"; mkdir -p "$D"
for j in $(seq 20); do : > "$D/f$j"; done
E=$(now)
solo=$(msd "$S" "$E")
echo "  单进程            ：${solo} ms"
for round in 1 2 3; do
    S=$(now)
    for k in $(seq 8); do
        (
            D="$DIR/par-${round}-${k}"; mkdir -p "$D"
            for j in $(seq 20); do : > "$D/f$j"; done
        ) &
    done
    wait
    E=$(now)
    echo "  8 进程并行（第 ${round} 轮）：$(msd "$S" "$E") ms"
done
echo "  ⇒ 并行档 ≈ 单进程 × 8 ⇒ 该开销**全局串行**（并行换不到吞吐）"
echo

# ── [3] 负载下外部命令的延迟 ──
echo "== [3] 8 路「新路径写入」负载下，外部命令的延迟 =="
(
    for k in $(seq 8); do
        (
            for r in $(seq 10); do
                D="$DIR/loadA-${k}-${r}"; mkdir -p "$D"
                for j in $(seq 20); do : > "$D/f$j"; done
            done
        ) &
    done
    wait
) &
LOAD=$!
sleep 0.3
vt=(); vf=0
for i in $(seq 20); do
    S=$(now); git --version >/dev/null 2>&1 || vf=$((vf + 1)); E=$(now)
    vt+=("$(msd "$S" "$E")")
done
wait "$LOAD"
echo "  git --version ×20：$(stats "${vt[@]}")   失败=$vf"
echo "  （常态 1~3 ms；尾部到秒级 ⇒ 它只是排在那条串行队列后面，不是命令本身慢）"
echo

# ── [4] 目录 vs 文件：单位是什么？ ──
echo "== [4] 单位归属（各 5 轮）=="
d1=(); for i in 1 2 3 4 5; do S=$(now); mkdir -p "$DIR/only-${i}"; E=$(now); d1+=("$(msd "$S" "$E")"); done
echo "  新目录（只 mkdir，1 个新路径）        ：$(stats "${d1[@]}")"
d2=(); for i in 1 2 3; do
    W="$DIR/warmdir-${i}"; mkdir -p "$W"; : > "$W/warm"
    S=$(now); for j in $(seq 20); do : > "$W/new$j"; done; E=$(now); d2+=("$(msd "$S" "$E")")
done
echo "  旧目录 + 20 个【从没出现过】的新文件名：$(stats "${d2[@]}")"
d3=(); for i in 1 2 3; do
    V="$DIR/same-${i}"; mkdir -p "$V"
    S=$(now); for j in $(seq 20); do : > "$V/f$j"; done; E=$(now); d3+=("$(msd "$S" "$E")")
done
echo "  新目录 + 20 个【同批名字】（路径首次）  ：$(stats "${d3[@]}")"
echo "  ⇒ 收的是「这个**文件**路径第一次出现」，与目录新旧无关；目录本身几乎免费（见上一行）"
echo

echo "（清理落点）"
rm -rf "$DIR"
echo "完成。判据：若 [2] 的并行档 ≈ 单进程 × 8，则本环境对测试套件是**串行瓶颈** ——"
echo "       门禁里「外部命令偶发超时」应当按环境处理（让这些用例避开队列），不要在窗口大小上花时间。"
