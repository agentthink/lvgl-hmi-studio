#!/bin/sh
# 跑门禁并把**退出码写进日志正文**（固定模板，别手打）。
#
# 为什么要有这个脚本（本仓踩过 4 次）：
#   门禁日志必须**自给自足** —— 事后要能从日志本身看出「那一跑过没过」，而不是依赖
#   任务状态或终端滚动。而取退出码这一步在不同 shell 下反复出错：
#     ① `bash gate.sh > log 2>&1; echo "gate_exit=$?"` 在 **fish** 里直接报错
#        （`$?` 不是退出代码）⇒ 门禁照跑、日志照写，但退出码**拿不到**；
#     ② 「半吊子 bash」：只把脚本塞进 `bash`，重定向与 `echo` 留在 fish
#        ⇒ `$?` 仍由 fish 解析 ⇒ **报错、什么都不追加**，而 `;` 后面的命令照跑
#        ⇒ 退出码**既不落日志、也不落终端**，任务还显示成功。
#        症状只有一个：日志末行是 `🎉 门禁全部通过（7 步）`，**没有 `GATE_EXIT=`**。
#   ⇒ 唯一写法是「**整条**（含重定向与 `echo`）都在同一个 shell 里」。本脚本把这条
#     固化成一个命令，省得每次都要默念一遍。
#
# 产物形态与历史日志**逐字节兼容**：`scripts/ci-gate.sh` 的完整输出 ＋ 最后一行为
#     `GATE_EXIT=<n>`
#   ⇒ `scripts/gate-readings.py` / `scripts/gate-diff.py` 直接可用，
#     也能与旧日志（如 `.workbuddy/tmp/s118-gate*.log`）对拍。
#
# 用法：
#     sh scripts/gate-run.sh <日志路径>
#     sh scripts/gate-run.sh --selftest          # 自检：3 档
#
# ⚠ 自检用 `GATE_CMD` 覆盖被跑的命令（默认 `bash scripts/ci-gate.sh`）。
#   除了自检**别设它** —— 设了就是拿假命令冒充门禁。
set -u

if [ "${1:-}" = "--selftest" ]; then
    TMPD="$(mktemp -d)"
    trap 'rm -rf "$TMPD"' EXIT
    fail=0

    # 档 1：成功命令 ⇒ 末行 GATE_EXIT=0，且 stdout 与 stderr **都**在日志里
    GATE_CMD='echo OUT; echo ERR >&2; exit 0' sh "$0" "$TMPD/a.log" >/dev/null
    if [ "$(tail -1 "$TMPD/a.log")" != "GATE_EXIT=0" ]; then
        echo "❌ 档1 末行应为 GATE_EXIT=0，实为：$(tail -1 "$TMPD/a.log")"; fail=1
    fi
    if ! grep -q OUT "$TMPD/a.log" || ! grep -q ERR "$TMPD/a.log"; then
        echo "❌ 档1 stderr 没被并进日志（2>&1 丢了）"; fail=1
    fi

    # 档 2：**失败命令的退出码必须被记下来**（不是硬写 0）—— 这是本脚本存在的全部理由
    GATE_CMD='echo boom; exit 7' sh "$0" "$TMPD/b.log" >/dev/null
    if [ "$(tail -1 "$TMPD/b.log")" != "GATE_EXIT=7" ]; then
        echo "❌ 档2 末行应为 GATE_EXIT=7，实为：$(tail -1 "$TMPD/b.log")"; fail=1
    fi

    # 档 3：产物形态 = 命令输出 N 行 ＋ GATE_EXIT 1 行（**不多不少**，否则与旧日志不可对拍）
    GATE_CMD='printf "l1\nl2\nl3\n"' sh "$0" "$TMPD/c.log" >/dev/null
    n=$(wc -l < "$TMPD/c.log" | tr -d ' ')
    if [ "$n" != "4" ]; then
        echo "❌ 档3 日志应为 3 ＋ 1 ＝ 4 行，实为 $n 行"; fail=1
    fi

    [ "$fail" = 0 ] && echo "✅ gate-run 自检 3 档全过"
    exit "$fail"
fi

LOG="${1:-}"
if [ -z "$LOG" ]; then
    echo "用法： sh scripts/gate-run.sh <日志路径>" >&2
    echo "       sh scripts/gate-run.sh --selftest" >&2
    exit 2
fi

REPO="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO" || exit 2
mkdir -p "$(dirname "$LOG")"

CMD="${GATE_CMD:-bash scripts/ci-gate.sh}"
# 整条都在**同一个 shell** 里：重定向 ＋ 取 `$?` ＋ 追加，不留任何东西给外层 shell 解析。
sh -c "$CMD" > "$LOG" 2>&1
RC=$?
echo "GATE_EXIT=$RC" >> "$LOG"
tail -1 "$LOG"
exit "$RC"
