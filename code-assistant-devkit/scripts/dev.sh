#!/usr/bin/env bash
# =============================================================
# 启动服务（Linux / macOS，开发模式）
# 用法：./scripts/dev.sh
# 说明：自动读取 .env / 环境变量中的 DEVKIT_HOST、DEVKIT_PORT
# =============================================================
set -euo pipefail
cd "$(dirname "$0")/.."

# 加载 .env（若存在）到进程环境
if [ -f ".env" ]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a
fi

HOST="${DEVKIT_HOST:-127.0.0.1}"
PORT="${DEVKIT_PORT:-8000}"

PYTHON_BIN=".venv/bin/python"
if [ ! -x "$PYTHON_BIN" ]; then
  echo "!! 未找到虚拟环境，请先运行 ./scripts/setup.sh"
  exit 1
fi

echo "==> 启动服务: http://$HOST:$PORT  （文档: /docs）"
exec "$PYTHON_BIN" -m uvicorn services.api.main:app --host "$HOST" --port "$PORT"
