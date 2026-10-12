#!/usr/bin/env bash
# 代码覆盖率：跑全量测试并汇总（汇总逻辑在 scripts/coverage-summary.py）。
#
# 为什么**不进 ci-gate**：
#   覆盖率是"看哪里没测"的**可见性工具**，不是判据。把它当门禁有两个坏处：
#   ① 新增代码天然拉低总量 ⇒ 阈值要么天天红、要么被迫定得毫无意义；
#   ② 逼人写"为覆盖率而写"的测试。
#   所以这里只负责**把事实量出来**，基线数字登记在 docs/project-status.md。
#
# 用法：
#   bash scripts/coverage.sh                # Release，自己构建
#   bash scripts/coverage.sh --no-build     # 已经构建过（快很多）
#   CONFIG=Debug bash scripts/coverage.sh
#
# 产物：<COVERAGE_DIR>/<时间戳>/<guid>/coverage.cobertura.xml —— 每个测试项目一份，
#       summary 脚本会按「文件 + 行号」取并集（同一份产品代码被多个测试项目覆盖时，
#       只看单份报告会**系统性低估**）。每次跑用新的时间戳目录，避免把上一轮的报告
#       一起汇总进来。

set -u
cd "$(dirname "$0")/.."

CONFIG=${CONFIG:-Release}
OUT=${COVERAGE_DIR:-.workbuddy/tmp/coverage}
RUN_DIR="$OUT/$(date +%Y%m%d-%H%M%S)"

# ── coverlet 的临时目录 ────────────────────────────────────────────────
# 它收尾时**先把报告写进 TMPDIR**、再 attach 到 --results-directory。
# 本机 /tmp 是 10 MB 的 tmpfs（`df -h /tmp`），多项目并行时必炸 ——
# 实测：Failed to create '/tmp/<guid>/coverage.cobertura.xml' because the disk was full。
# 指到仓库**外**的大分区目录：必须仓库外，因为 TestTempDir.NewIsolatedDir 会拒收仓库内的路径。
COV_TMP=${COVERAGE_TMP:-$HOME/.cache/hmi-coverage-tmp}
rm -rf "$COV_TMP" 2>/dev/null || true
mkdir -p "$COV_TMP"
export TMPDIR="$COV_TMP"

mkdir -p "$RUN_DIR"

echo "===== [1/2] 跑全量测试（$CONFIG）并收集覆盖率 ====="
echo "     输出目录：$RUN_DIR"
echo "     coverlet 临时目录：$COV_TMP（绕开 10 MB 的 /tmp）"
dotnet test HmiEditor.slnx -c "$CONFIG" --nologo \
    --collect:"XPlat Code Coverage" --results-directory "$RUN_DIR" "$@"
rc=$?

if [ "$rc" -ne 0 ]; then
    echo
    echo "✗ 测试未全部通过（rc=$rc）—— 覆盖率数字仅供排查，不要当作基线"
    exit "$rc"
fi

echo
echo "===== [2/2] 汇总 ====="
python3 scripts/coverage-summary.py "$RUN_DIR"
