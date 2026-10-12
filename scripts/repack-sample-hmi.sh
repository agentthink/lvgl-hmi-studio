#!/bin/bash
# repack-sample-hmi.sh — 把仓内的**示例工程定义**与设备侧资产重新打包成 .hmi（阶段 307）
#
# 为什么 `.hmi` 本身不入库：源包 **24.3 MB**，其中 **19.5 MB** 是 CJK 字体（**按约定不入 git**，
# 见固件包 Makefile 对 font_cjk_sc.ttf 的说明）；剩下 4 张 BMP 与
# `firmware/t113/generated/assets/` 里的**逐字节相同** ⇒ 直接入库等于白存 5.9 MB。
#
# ⇒ 仓里只放**工程定义**（`project.json` / `assets.json` / `manifest.json` ≈ 53 KB），
#   资产**复用**设备侧那一份；要 .hmi 时用本脚本现场打（够复现、可 diff、可再导出）。
#
# 用法：bash scripts/repack-sample-hmi.sh [输出路径]
#   缺省输出 build/sample/MockChineseProject.hmi（build/ 不入 git）
set -u
cd "$(dirname "$0")/.." || exit 1

DEF=firmware/t113/sample/MockChineseProject
ASSETS=firmware/t113/generated/assets
OUT="${1:-build/sample/MockChineseProject.hmi}"
OUT_ABS=$(readlink -f "$OUT" 2>/dev/null || echo "$PWD/$OUT")

WORK=$(mktemp -d /tmp/hmi_sample_XXXX)
trap 'rm -rf "$WORK"' EXIT
mkdir -p "$WORK/assets" "$(dirname "$OUT_ABS")"

cp "$DEF/manifest.json" "$DEF/project.json" "$DEF/assets.json" "$WORK/"

# 只放 assets.json 点到名的资产（字体条目已在入库时摘掉 —— 见上面的说明）
if ! python3 - "$DEF/assets.json" "$ASSETS" "$WORK" <<'PY'
import json, shutil, sys, pathlib
aj, assets, work = sys.argv[1], sys.argv[2], sys.argv[3]
entries = json.load(open(aj, encoding='utf-8'))['assets']
skipped = []
for a in entries:
    src = pathlib.Path(assets) / pathlib.Path(a['filePath']).name
    if not src.exists():
        # 字体（19.5 MB）**按约定不入库**：条目留着（生成器要靠它发 hmi_resolve_font），
        # 文件不在就跳过并说明 —— 不要把"条目"和"文件"混成一件事（阶段 307 就是这么错的）。
        if a.get('type') == 'font':
            skipped.append(src.name)
            continue
        print(f"  ✗ 缺资产：{src}", file=sys.stderr)
        sys.exit(1)
    shutil.copy(src, pathlib.Path(work) / 'assets' / src.name)
if skipped:
    print(f"  跳过 {len(skipped)} 个字体文件（不入库）：{', '.join(skipped)}")
print(f"  放入 {len(entries)} 个资产（字体不含）")
PY
then
    echo "打包失败" >&2
    exit 1
fi

# 用 python 的 zipfile 而不是 zip(1)：本机没有 zip；而且这样能把时间戳**钉死**
# ⇒ 同样的输入打出**逐字节相同**的包（可 diff、可复现）。
python3 - "$WORK" "$OUT_ABS" <<'PY'
import pathlib, sys, zipfile
work, out = pathlib.Path(sys.argv[1]), sys.argv[2]
files = sorted(p for p in work.rglob('*') if p.is_file())
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
    for p in files:
        info = zipfile.ZipInfo(str(p.relative_to(work)), date_time=(1980, 1, 1, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        z.writestr(info, p.read_bytes())
print(f"  打包 {len(files)} 个条目（时间戳已钉死 ⇒ 确定性）")
PY
echo "  产物：$OUT_ABS  ($(stat -c%s "$OUT_ABS") B)"
sha256sum "$OUT_ABS"
