#!/usr/bin/env bash
# ============================================================
# check-path-hygiene.sh — 产品/工具代码不得硬编码开发机家目录
#
# 背景：tools/MockHmiGen 曾在两处写死 /home/<开发者>/...（默认输出目录 +
# "下一步"提示语）。后果不是"不好看"，而是三条实际故障：
#   ① 在别的机器/CI 上凭空创建 /home/<别人>/文档 并往里写文件（越界写盘）；
#   ② 产物路径随机器漂移，导出的模拟器工程在他人机器上不可复现；
#   ③ 提示语把人指向本机绝对路径，复制粘贴即失败。
# 替代做法：相对路径 / 命令行参数 / 环境变量 / Path.GetTempPath()。
#
# 判定范围：src/ tools/ scripts/ firmware/（不含 firmware/Tina-* 第三方 SDK）。
# tests/ 有意排除：测试夹具有正当理由构造 /home/user/... 这类**通用占位**
# 绝对路径（见 ConfigCodeGenConsumptionTests、MockImageLoadingTests）。
#
# 用法：bash scripts/check-path-hygiene.sh [--selftest]
# 退出码：0 = 干净；1 = 命中（明细打到 stderr）。
#
# 判据是「路径被 ASCII 引号包裹」，不是「路径出现」。这是刻意的：
#   ✅ 注释/文档里裸写 /home/xxx/... —— 不报（历史记录与说明需要它）
#   ❌ 代码里把路径放进字符串字面量（“/home/…” 那种形态）—— 报
# 相应契约：在注释里举例路径时不要用 ASCII 引号包住它，否则会撞上本检查。
# （本脚本首版就自撞过一次：注释里写了带 ASCII 引号的示例，被自己拦下。）
#
# ⚠ 扫描面是**两条腿**（阶段 123）：**已跟踪 ∪ 未跟踪**（`--others --exclude-standard`）。
#   此前只用 `git ls-files`（已跟踪）—— 于是**新文件在被 `git add` 之前是盲区**
#   （登记表 §41.3 第 2 条 / 阶段 119 的 N4 实测：同一份注入，未跟踪绿、`git add` 后红）。
#   现在未跟踪的新文件当场就会被红，不再依赖"提交前记得先 add"这条纪律。
#   `--exclude-standard` 仍尊重 .gitignore ⇒ bin/obj/__pycache__ 等不会进面。
# ============================================================
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

SQ="'"
# 引号内的 /home/<name>/ 或 /Users/<name>/：覆盖 C#/JSON 的 "..." 与 shell 的 '...'。
# 必须带引号：注释与文档里出现路径是允许的（历史记录需要）。
PATTERN="[\"$SQ]/(home|Users)/[A-Za-z0-9._-]+/"

# 扫描面下限（阶段 121）：清单塌掉时下面的循环会一条都不查却打出"✅ 已检查 0 个文件"。
# 实测 675+（阶段 120 起）；取 300 留正常增减余量。
MIN_FILES="${MIN_FILES:-300}"
# 文本类过滤：资源与压缩包一律跳过（避免二进制误报）；Tina 第三方 SDK 不扫。
EXCLUDE_RE='(^firmware/Tina-|\.(png|jpg|jpeg|bmp|gif|webp|ttf|ttc|otf|woff2?|bin|img|so|dll|exe|zip|hmi|md)$)'

# ── 取数（抽成函数，好让 --selftest 在临时 git 仓库上跑**同一段**逻辑）──
collect_files() {
    local root="${1:-$ROOT}"
    if git -C "$root" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
        # ⚠ 用 `-z` + tr：默认输出会对含空格/非 ASCII 的名字**加引号并转义**
        #   （`"firmware/…\345…md"`），那样 `[ -f ]` 与后缀过滤都会失配。
        {
            git -C "$root" ls-files -z 'src/*' 'tools/*' 'scripts/*' 'firmware/*' 2>/dev/null
            git -C "$root" ls-files -z --others --exclude-standard \
                'src/*' 'tools/*' 'scripts/*' 'firmware/*' 2>/dev/null
        } | tr '\0' '\n' | sort -u | grep -Ev "$EXCLUDE_RE" || true
    else
        ( cd "$root" && find src tools scripts firmware -type f -not -path 'firmware/Tina-*' 2>/dev/null )
    fi
}

# ── 扫描：命中写 stderr；返回 0 = 干净，1 = 有命中 ──
scan_hits() {
    local files="$1" hits="" f out
    while IFS= read -r f; do
        [ -n "$f" ] || continue
        [ -f "$f" ] || continue
        out="$(grep -nE "$PATTERN" "$f" 2>/dev/null || true)"
        [ -n "$out" ] && hits="${hits}${f}
${out}
"
    done <<< "$files"
    if [ -n "$hits" ]; then printf '%s' "$hits" >&2; return 1; fi
    return 0
}

# ── 自检（--selftest）──
# A/B/C 三档守**模式**（正控两条 + 负控一条）；D/E 两档守**取数面**：
#   D：临时 git 仓库里，**未跟踪**的脏文件必须被抓到（这就是阶段 123 关掉的盲区）
#   E：同一个仓库全干净时必须不报（防"永远红"）
if [ "${1:-}" = "--selftest" ]; then
    tmp="$(mktemp -d)"
    trap 'rm -rf "$tmp"' EXIT
    failed=0
    check() { # $1=标签 $2=实际 $3=期望
        if [ "$2" = "$3" ]; then echo "  ✅ $1 期望 $3，实际 $2"
        else echo "  ❌ $1 期望 $3，实际 $2"; failed=$((failed + 1)); fi
    }

    # ⚠ 样本里的引号必须用变量拼进去：把 `"` 直接写在源文件里会被**本检查自己**拦下
    #   （脚本头部记的那次自撞）。写成 %s/home/... 则源码里没有"引号紧跟家目录"这个形状。
    mkdir -p "$tmp/pat"
    printf 'var p = %s/home/somebody/secret%s;\n' "$SQ" "$SQ" > "$tmp/pat/bad.cs"
    printf 'var q = %s/Users/somebody/x%s;\n' "$SQ" "$SQ" > "$tmp/pat/bad2.cs"
    printf '// /home/somebody/secret 在注释里，不该命中\n' > "$tmp/pat/good.cs"
    check "正控·引号内 /home" "$(grep -cE "$PATTERN" "$tmp/pat/bad.cs" || true)" 1
    check "正控·引号内 /Users" "$(grep -cE "$PATTERN" "$tmp/pat/bad2.cs" || true)" 1
    check "负控·注释里裸写" "$(grep -cE "$PATTERN" "$tmp/pat/good.cs" || true)" 0

    # D/E：取数面（未跟踪腿）
    mkdir -p "$tmp/repo/src"
    git -C "$tmp/repo" init -q
    printf 'var ok = 1;\n' > "$tmp/repo/src/clean.cs"
    printf 'var bad = %s/home/somebody/secret%s;\n' "$SQ" "$SQ" > "$tmp/repo/src/untracked_dirty.cs"
    git -C "$tmp/repo" add src/clean.cs   # 只跟踪干净那个；脏的那个保持**未跟踪**
    repo_out="$(cd "$tmp/repo" && list="$(collect_files "$tmp/repo")" && \
        { printf '%s\n' "$list" | grep -q 'untracked_dirty.cs' && echo SeesUntracked=1 || echo SeesUntracked=0; } && \
        { printf '%s\n' "$list" | grep -q 'clean.cs' && echo SeesTracked=1 || echo SeesTracked=0; } && \
        { scan_hits "$list" >/dev/null 2>&1 && echo Clean=1 || echo Clean=0; })"
    check "取数档·未跟踪文件也在面内" "$(printf '%s\n' "$repo_out" | sed -n 's/^SeesUntracked=//p')" 1
    check "取数档·已跟踪文件仍在面内" "$(printf '%s\n' "$repo_out" | sed -n 's/^SeesTracked=//p')" 1
    check "取数档·未跟踪的脏文件必须红" "$(printf '%s\n' "$repo_out" | sed -n 's/^Clean=//p')" 0

    # E：全干净的仓库必须不报（否则判据是"永远红"的摆设）
    mkdir -p "$tmp/repo2/src"
    git -C "$tmp/repo2" init -q
    printf 'var ok = 1;\n' > "$tmp/repo2/src/only_clean.cs"
    clean_out="$(cd "$tmp/repo2" && list="$(collect_files "$tmp/repo2")" && \
        { scan_hits "$list" >/dev/null 2>&1 && echo Clean=1 || echo Clean=0; })"
    check "取数档·全干净必须绿" "$(printf '%s\n' "$clean_out" | sed -n 's/^Clean=//p')" 1

    if [ "$failed" -eq 0 ]; then echo "✅ 自检通过：模式抓得到真命中不误伤注释；取数面覆盖已跟踪 ∪ 未跟踪"
    else echo "❌ 自检失败 ${failed} 档 —— 判据会误报或漏报（假门禁）"; fi
    exit $([ "$failed" -eq 0 ] && echo 0 || echo 1)
fi

FILES="$(collect_files "$ROOT")"
[ -n "$FILES" ] || { echo "❌ 未找到任何待检查文件（清单为空，检查取数命令）" >&2; exit 1; }

# fail-closed（阶段 121）：清单**非空但塌了**也会一片绿 —— 加上扫描面下限。
# 计数与扫描用**同一条**过滤（只数真实存在的文件），否则报出来的数字会比实际扫的多。
LIST_COUNT="$(printf '%s\n' "$FILES" | while IFS= read -r f; do [ -n "$f" ] && [ -f "$f" ] && echo x; done | grep -c . || true)"
if [ "$LIST_COUNT" -lt "$MIN_FILES" ]; then
    echo "❌ 待检查文件只有 ${LIST_COUNT} 个（下限 ${MIN_FILES}）—— 取数命令或 git 索引出了问题，" >&2
    echo "   本判据会退化成'永远通过'（假门禁）。" >&2
    exit 1
fi

HITS_OUT=""
if ! HITS_OUT="$(scan_hits "$FILES" 2>&1)"; then
    echo "❌ 产品/工具代码中出现硬编码的开发机家目录路径：" >&2
    echo "$HITS_OUT" >&2
    echo "   请改用相对路径、命令行参数或环境变量；临时文件用 Path.GetTempPath() / \$TMPDIR。" >&2
    exit 1
fi

echo "✅ 无硬编码开发机家目录路径（已检查 ${LIST_COUNT} 个文件；已跟踪 ∪ 未跟踪）"
exit 0
