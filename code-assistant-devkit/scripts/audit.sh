#!/usr/bin/env bash
# =============================================================
# 依赖漏洞扫描（pip-audit）：扫描当前环境已装依赖的已知 CVE
# 用法：./scripts/audit.sh
# 首次运行会自动安装 pip-audit（仅开发期工具，不入运行依赖）
# =============================================================
set -euo pipefail
cd "$(dirname "$0")/.."

PY=.venv/bin/python
[ -x "$PY" ] || PY=python3

# 确保 pip-audit 可用
if ! "$PY" -m pip_audit --help >/dev/null 2>&1; then
  echo "==> 安装 pip-audit（开发期工具）..."
  "$PY" -m pip install -q pip-audit
fi

echo "==> 扫描依赖已知漏洞（pip-audit）..."
"$PY" -m pip_audit --desc
