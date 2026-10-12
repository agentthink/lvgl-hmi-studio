#!/usr/bin/env bash
# =============================================================
# 环境自检：健康检查 + 能力清单 + 服务连通性
# 用法：./scripts/verify.sh
# =============================================================
set -euo pipefail
cd "$(dirname "$0")/.."

echo "========== 1) 能力清单 =========="
.venv/bin/python - <<'PY'
from skills.core.registry import SkillRegistry
registry = SkillRegistry()
for meta in registry.metas:
    deps = " -> ".join(meta.dependencies) if meta.dependencies else "无"
    print(f"  [{meta.name}] {meta.display_name} | 依赖: {deps}")
    print(f"      场景: {'; '.join(meta.scenarios[:2])}")
    print(f"      触发: {', '.join(meta.triggers[:5])}")
PY

echo ""
echo "========== 2) 服务健康检查 =========="
# 若服务已在运行则直接探测；否则拉起一个临时实例再探测
HOST="${DEVKIT_HOST:-127.0.0.1}"
PORT="${DEVKIT_PORT:-8000}"
URL="http://$HOST:$PORT/health"

if curl -s -m 3 "$URL" >/dev/null 2>&1; then
  echo "  服务已在运行:"
  curl -s "$URL" | .venv/bin/python -m json.tool
else
  echo "  服务未运行，拉起临时实例探测..."
  .venv/bin/python -m uvicorn services.api.main:app --host "$HOST" --port "$PORT" &
  SERVER_PID=$!
  trap 'kill $SERVER_PID 2>/dev/null || true' EXIT
  sleep 2
  curl -s "$URL" | .venv/bin/python -m json.tool
  echo "  （临时实例已停止）"
fi

echo ""
echo "✔ 自检完成"
