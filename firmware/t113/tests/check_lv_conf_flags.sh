#!/usr/bin/env bash
# ============================================================
# check_lv_conf_flags.sh — 跟踪的固件配置（config/lv_conf.h）关键项守卫
#
# 为什么需要它：这份配置**不在任何编译路径的门禁里**。
#   run_host_tests.sh 虽然带 `-Iconfig`，但那些 host 测试刻意不拉 lvgl.h
#   （见 hmi_log.h 独立成文件的原因）⇒ 没有任何一步会真去解析 lv_conf.h。
#   而 LV_USE_DRAW_SW_COMPLEX_GRADIENTS 默认 0（lv_conf_internal.h），被删掉时
#   渐变**不报错、不空白**，只是被当成 HOR 一维色带画出来（方向和形状都不对）
#   —— 最难发现的那种坏法，所以必须有一条自动判据盯着它。
#
# 判据分两层：
#   [A] 文本守卫（无外部依赖，永远跑）
#   [B] 预处理探针（找到 LVGL 头链才跑；让 LVGL 自己的 lv_conf_internal.h 去消费
#       这份配置，证"宏真的等于 1"）。找不到头链就**明确报未验证**，
#       CI 可设 CI_REQUIRE_FIRMWARE_TESTS=1 把它升级为失败。
#
# ⚠ [B] 的 include 顺序是判据的一部分：`-Iconfig` **必须排在** `-I<lvgl>` 前面。
#   LVGL 目录自己带一份 lv_conf.h，且那份里该项就是 1 ⇒ 顺序写反时
#   `#include "lv_conf.h"` 命中那一份，正控照样"通过"、负控也不报错，
#   整个探针退化成恒真的假判据（本脚本第一版就是这么写的，被负控抓出来）。
#
# 用法：bash tests/check_lv_conf_flags.sh（cwd 任意；脚本自己定位到 firmware/t113）
# ============================================================
set -euo pipefail
cd "$(dirname "$0")/.."

CONF=config/lv_conf.h
FLAG=LV_USE_DRAW_SW_COMPLEX_GRADIENTS
MAXSTOPS_MACRO=LV_GRADIENT_MAX_STOPS
# LVGL 未定义该宏时的默认值（lv_conf_internal.h 的 `#define LV_GRADIENT_MAX_STOPS 2`）。
# 这里写死一份是**有意**的：它就是下面 [B] 探针要去证明的东西 —— 头链在时由预处理器
# 直接判 `LV_GRADIENT_MAX_STOPS == MaxStops`，这里只是头链缺席时的保守取值。
LVGL_DEFAULT_MAX_STOPS=2
CS_FILE=../../src/HmiEditor.Core/Services/SvgGradientGeometry.cs

fail() { echo "❌ $*" >&2; exit 1; }

# ── [A] 文本守卫 ──────────────────────────────────────────
echo "########## [A] 跟踪配置关键项（文本守卫） ##########"
[ -f "$CONF" ] || fail "找不到 $CONF"

# 行首锚定匹配，而不是裸 grep：配置里那些**被注释掉的样例**以 ` * ` 或 `// ` 开头，
# 永远命中不了 `^[[:space:]]*#`，所以不需要先剥注释（剥注释的实现比它要防的 bug 更容易出错）。
n=$(grep -cE "^[[:space:]]*#[[:space:]]*define[[:space:]]+$FLAG[[:space:]]+1[[:space:]]*$" "$CONF" || true)
[ "$n" -eq 1 ] || fail "$CONF 里必须**恰好一处** '#define $FLAG 1'，实测 $n 处"

if grep -qE "^[[:space:]]*#[[:space:]]*undef[[:space:]]+$FLAG" "$CONF"; then
    fail "$CONF 里出现了 '#undef $FLAG'"
fi
echo "✅ $FLAG = 1（恰好一处，无 #undef）"

# 停靠点上限与 C# 侧常量必须同源：抬这个宏会改 lv_grad_dsc_t 里停靠点数组的大小，
# SvgGradientGeometry.MaxStops 是发射侧的唯一来源，两边一旦走样就是**写到数组外**。
cs_max=$(grep -oE "MaxStops[[:space:]]*=[[:space:]]*[0-9]+" "$CS_FILE" | grep -oE "[0-9]+" | head -1 || true)
[ -n "$cs_max" ] || fail "读不到 SvgGradientGeometry.MaxStops（$CS_FILE）—— 守卫不得静默降级"

if grep -qE "^[[:space:]]*#[[:space:]]*define[[:space:]]+$MAXSTOPS_MACRO" "$CONF"; then
    conf_max=$(grep -oE "^[[:space:]]*#[[:space:]]*define[[:space:]]+$MAXSTOPS_MACRO[[:space:]]+[0-9]+" "$CONF" \
               | grep -oE "[0-9]+$" | head -1)
    conf_max_src="$CONF"
else
    conf_max="$LVGL_DEFAULT_MAX_STOPS"
    conf_max_src="LVGL 默认（$CONF 未定义该宏）"
fi
[ "$conf_max" -eq "$cs_max" ] \
    || fail "$MAXSTOPS_MACRO=$conf_max（$conf_max_src）与 SvgGradientGeometry.MaxStops=$cs_max 不一致：两边必须同源，否则发射会写到数组外"
echo "✅ $MAXSTOPS_MACRO = $cs_max（与 SvgGradientGeometry.MaxStops 同源）"

# ── [B] 预处理探针 ───────────────────────────────────────
echo
echo "########## [B] 预处理探针（让 LVGL 消费这份配置） ##########"

# 仓库里没有入库的 LVGL 源码：host_test/lvgl 与 Tina-*/package/gui/lvgl 都被
# .gitignore 忽略（.gitignore:43 / :33,34），是新克隆上通常不存在的本机副本。
candidates=()
if [ -n "${LVGL_SRC_DIR:-}" ] && [ -f "${LVGL_SRC_DIR:-/nonexistent}/lvgl.h" ]; then
    candidates+=("$LVGL_SRC_DIR")
fi
if [ -f host_test/lvgl/lvgl.h ]; then candidates+=("host_test/lvgl"); fi
for d in ../Tina-Linux/package/gui/lvgl ../Tina-SDK/package/gui/lvgl; do
    if [ -f "$d/lvgl.h" ]; then candidates+=("$d"); fi
done

PROBE_DIR="$(mktemp -d)"
trap 'rm -rf "$PROBE_DIR"' EXIT

cat > "$PROBE_DIR/probe.c" <<EOF
#include "lvgl.h"
#if $FLAG != 1
#error tracked-config-lacks-flag
#endif
#if $MAXSTOPS_MACRO != $cs_max
#error gradient-max-stops-out-of-sync-with-SvgGradientGeometry-MaxStops
#endif
EOF

if [ "${#candidates[@]}" -eq 0 ]; then
    MSG="未找到 LVGL 头链（host_test/lvgl 与 Tina-*/package/gui/lvgl 都是 .gitignore 的本机副本）⇒ 只验证了文本层，宏是否真被 LVGL 采纳**未验证**"
    if [ "${CI_REQUIRE_FIRMWARE_TESTS:-0}" = "1" ]; then fail "$MSG"; fi
    echo "⚠ $MSG"
else
    for inc in "${candidates[@]}"; do
        # 注意 -Iconfig 在前（见文件头注释）
        if gcc -fsyntax-only -std=c99 -DLV_CONF_INCLUDE_SIMPLE -Iconfig -I"$inc" "$PROBE_DIR/probe.c"; then
            echo "✅ $inc：$FLAG == 1，$MAXSTOPS_MACRO == $cs_max"
        else
            fail "探针失败（头链 $inc）：上面那条 #error 点明了是哪一项不成立"
        fi
    done
fi

echo
echo "########## 配置守卫全部通过 ##########"
