#!/usr/bin/env bash
# ============================================================
# package-release.sh —— 编辑器侧「一键出包」（登记表 §4 第 29 项）
#
# 产物：<out>/zhishu-hmi-editor-<版本>-<git短号>-<日期>/
#         ├── editor/            dotnet publish 的编辑器（框架依赖）
#         ├── docs/              随包固件文档（烧录指南 / 版本信息 / 触摸诊断）
#         ├── UPGRADE.md         安装 / 升级 / 回滚说明
#         ├── manifest.json      版本清单（编辑器 ↔ 固件四组标记）+ 逐文件 sha256
#         └── SHA256SUMS         sha256sum -c 兼容
#       以及同名 .tar.gz。
#
# 判据：版本清单由事实源派生（firmware/t113/app/runtime/*.h + AllwinnerT113Target），
#       装配完立即自检；随后可用 `--verify <目录>` 重跑同一套校验。
#
# 用法：
#   bash scripts/package-release.sh [--out release] [--with-kit] [--no-archive]
#   bash scripts/package-release.sh --verify release/<包名>
#
# 真实逻辑在 tools/ReleasePackager（C#，可单测），本脚本只是入口 ——
# 不在这里复制第二份版本解析/校验逻辑。
# ============================================================
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

CONFIG="${CONFIG:-Release}"
exec dotnet run --project tools/ReleasePackager/ReleasePackager.csproj -c "$CONFIG" -- "$@"
