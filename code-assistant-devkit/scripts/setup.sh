#!/usr/bin/env bash
# =============================================================
# 一键初始化环境（Linux / macOS）
# 功能：创建虚拟环境 -> 安装依赖 -> 生成 .env -> 运行自检
# 用法：./scripts/setup.sh   或   make setup
# =============================================================
set -euo pipefail

# 切换到项目根目录（脚本所在目录的上一级）
cd "$(dirname "$0")/.."
ROOT="$(pwd)"
echo "==> 项目根目录: $ROOT"

# 1) 选择 Python 解释器（可通过 PYTHON 环境变量覆盖）
PYTHON="${PYTHON:-python3}"
if ! command -v "$PYTHON" >/dev/null 2>&1; then
  echo "!! 未找到 $PYTHON，请先安装 Python 3.10+"
  exit 1
fi
echo "==> Python: $($PYTHON --version)"

# 2) 创建虚拟环境（已存在则复用）
if [ ! -d ".venv" ]; then
  echo "==> 创建虚拟环境 .venv ..."
  "$PYTHON" -m venv .venv
fi
# shellcheck disable=SC1091
source .venv/bin/activate

# 3) 升级 pip 并安装依赖
echo "==> 安装依赖（requirements.txt + 本项目 editable）..."
python -m pip install --upgrade pip -q
python -m pip install -r requirements.txt -q
python -m pip install -e . -q

# 4) 生成本地 .env（若不存在）
if [ ! -f ".env" ]; then
  cp .env.example .env
  echo "==> 已从模板生成 .env（含默认开发密钥）"
fi

# 5) 环境自检
echo "==> 运行环境自检..."
python -c "import skills, services, devkit_common; print('导入 OK')"
python -m pytest -q tests/test_skills.py tests/test_services.py 2>/dev/null || true

echo ""
echo "✔ 初始化完成！下一步："
echo "   启动服务:  ./scripts/dev.sh"
echo "   跑全量测试: make test"
echo "   端到端演示: make demo"
