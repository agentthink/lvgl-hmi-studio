"""服务模块调用示例：遍历全部接口（需先启动服务）。

用法：
  1. ./scripts/setup.sh && ./scripts/dev.sh   （或手动 uvicorn services.api.main:app）
  2. python examples/call_all_services.py

本脚本演示：
  - 鉴权头 X-API-Key 的正确使用与缺失时的错误响应
  - 四个接口的请求/响应格式
  - 统一错误处理（404 资源不存在）
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import httpx

# 保证从项目根目录可导入（脚本被直接运行时把根目录加入 sys.path）
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

BASE_URL = "http://127.0.0.1:8000"
API_KEY = "dev-key-001"          # 与 .env / DEVKIT_API_KEYS 保持一致

# 演示用源码（刻意包含一个语法错误：def 缺冒号）
DEMO_SOURCE = '''def add(a, b)
    return a + b
'''


def show(title: str, resp: httpx.Response) -> None:
    """打印一次调用的标题与响应体。"""
    print(f"\n----- {title} -----")
    print(f"HTTP {resp.status_code}")
    print(json.dumps(resp.json(), ensure_ascii=False, indent=2)[:800])


def main() -> None:
    headers = {"X-API-Key": API_KEY}

    # 0) 健康检查（免鉴权）
    show("0. 健康检查 GET /health", httpx.get(f"{BASE_URL}/health"))

    # 1) 静态分析（源码）
    show("1. 静态分析 POST /api/v1/analysis",
         httpx.post(f"{BASE_URL}/api/v1/analysis", json={"source": DEMO_SOURCE}, headers=headers))

    # 2) 未带鉴权头 -> 统一 401 错误
    show("2. 缺少鉴权头（预期 401）",
         httpx.post(f"{BASE_URL}/api/v1/analysis", json={"source": DEMO_SOURCE}))

    # 3) 不存在的资源 -> 统一 404 错误
    show("3. 资源不存在 GET /api/v1/nothing（预期 404）",
         httpx.get(f"{BASE_URL}/api/v1/nothing", headers=headers))

    # 4) 日志查询
    show("4. 日志查询 GET /api/v1/logs?service=analysis",
         httpx.get(f"{BASE_URL}/api/v1/logs", params={"service": "analysis", "limit": 5}, headers=headers))

    print("\n✔ 全部示例调用完成（构建/部署示例见 run_demo_flow.py）")


if __name__ == "__main__":
    main()
