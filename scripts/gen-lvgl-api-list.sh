#!/usr/bin/env bash
# ============================================================
# gen-lvgl-api-list.sh — 从 LVGL 源码头文件抽取 API 名清单
#
# 用途：生成 src/HmiEditor.Core/Resources/lvgl-api-names.txt（入库的对照表），
#       供 ValidatorSelfConsistencyTests 断言 GeneratedCodeValidator 的
#       KnownLvglApis 白名单里每个名字都真实存在 —— 防止再次出现陈旧/拼错条目
#       （2026-09-13 审查曾发现白名单漏 40 项、且混入 v8 旧名）。
#
# 用法： ./scripts/gen-lvgl-api-list.sh [LVGL 源码根目录]
#        默认取环境变量 LVGL_DIR，再退到 ~/Git/lvgl
#
# 抽取规则（去注释后）：
#   1) 函数式声明/宏：  lv_xxx(...)
#   2) 对象式兼容别名： #define lv_xxx lv_yyy   ← 必须包含！
#      LVGL 的 lv_api_map_v8.h / lv_api_map_v9_1.h 用这种形式提供旧名别名，
#      生成代码可能引用它们（如 lv_chart_set_range → lv_chart_set_axis_range）。
#
# 依赖：python3（仅本脚本；测试只读入库的清单文件，不需要 python）。
# ============================================================
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LVGL_ROOT="${1:-${LVGL_DIR:-$HOME/Git/lvgl}}"
OUT="$ROOT/src/HmiEditor.Core/Resources/lvgl-api-names.txt"

if [ ! -f "$LVGL_ROOT/lvgl.h" ]; then
    echo "error: 未找到 LVGL 源码根（需含 lvgl.h）: $LVGL_ROOT" >&2
    echo "       用法: $0 <LVGL 源码根目录>  或  LVGL_DIR=<dir> $0" >&2
    exit 1
fi

command -v python3 >/dev/null 2>&1 || { echo "error: 需要 python3" >&2; exit 1; }

LVGL_ROOT="$LVGL_ROOT" OUT="$OUT" python3 - <<'PY'
import os
import re
from pathlib import Path

root = Path(os.environ["LVGL_ROOT"])
out = Path(os.environ["OUT"])
src = root / "src"

def strip_comments(text: str) -> str:
    text = re.sub(r"/\*.*?\*/", " ", text, flags=re.S)
    text = re.sub(r"//[^\n]*", " ", text)
    return text

func_re = re.compile(r"\blv_[a-z0-9_]+(?=\s*\()")
macro_re = re.compile(r"^\s*#\s*define\s+(lv_[a-z0-9_]+)", re.M)

names = set()
for p in sorted(src.rglob("*.h")):
    try:
        body = strip_comments(p.read_text(encoding="utf-8", errors="ignore"))
    except OSError:
        continue
    names.update(m.group(0) for m in func_re.finditer(body))
    names.update(m.group(1) for m in macro_re.finditer(body))

# 过滤文档占位符（lv_xxx_set_yyy 之类）
names = {n for n in names if not re.search(r"_(x{2,}|y{2,}|z{2,})(_|$)", n)}

if len(names) < 1000:
    raise SystemExit(f"error: 只抽到 {len(names)} 个名字，LVGL 源码树似乎不完整")

header = [
    "# LVGL API 名清单（由 scripts/gen-lvgl-api-list.sh 生成，请勿手改）",
    f"# 来源: {root.name}（lvgl.h 所在目录）",
    "# 用途: 校验 GeneratedCodeValidator.KnownLvglApis 白名单中的名字真实存在",
    "# 重新生成: ./scripts/gen-lvgl-api-list.sh <LVGL 源码根>",
]
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text("\n".join(header + sorted(names)) + "\n", encoding="utf-8")
print(f"已写出 {out.relative_to(Path.cwd()) if out.is_relative_to(Path.cwd()) else out}: {len(names)} 个 API 名")
PY
