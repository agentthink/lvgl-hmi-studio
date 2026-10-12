"""服务模块测试：鉴权、统一错误处理、四组接口的功能验证（TestClient 直连应用，无需真实端口）。"""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

import pytest
from fastapi.testclient import TestClient

from services.api.main import app
from services.core import analyzer

# 演示项目路径（构建/部署素材）
DEMO_PROJECT = ROOT / "examples" / "demo_project"
KEY = "dev-key-001"


@pytest.fixture(scope="module")
def client() -> TestClient:
    """共享一个测试客户端（同一应用实例）。"""
    return TestClient(app)


def _headers() -> dict:
    return {"X-API-Key": KEY}


# ---------------- 健康检查 / 鉴权 ----------------
def test_health_open(client: TestClient):
    """健康检查免鉴权。"""
    resp = client.get("/health")
    assert resp.status_code == 200
    assert resp.json()["data"]["status"] == "ok"


def test_analysis_requires_auth(client: TestClient):
    """缺少 API Key -> 统一 401。"""
    resp = client.post("/api/v1/analysis", json={"source": "x = 1"})
    assert resp.status_code == 401
    body = resp.json()
    assert body["code"] == "AUTH_MISSING_KEY"
    assert "message" in body


def test_invalid_key_rejected(client: TestClient):
    """无效 API Key -> 统一 401。"""
    resp = client.post("/api/v1/analysis", json={"source": "x = 1"}, headers={"X-API-Key": "wrong-key"})
    assert resp.status_code == 401
    assert resp.json()["code"] == "AUTH_INVALID_KEY"


# ---------------- 统一错误处理 ----------------
def test_404_unified_format(client: TestClient):
    """不存在的路由 -> 统一 404 错误格式。"""
    resp = client.get("/api/v1/nothing", headers=_headers())
    assert resp.status_code == 404
    body = resp.json()
    assert body["code"] == "NOT_FOUND"
    assert "code" in body and "message" in body


def test_analysis_empty_source(client: TestClient):
    """source 与 file_path 都缺 -> 400 业务错误码。"""
    resp = client.post("/api/v1/analysis", json={}, headers=_headers())
    assert resp.status_code == 400
    assert resp.json()["code"] == "ANALYSIS_EMPTY_SOURCE"


# ---------------- 静态分析 ----------------
def test_analysis_source(client: TestClient):
    """源码分析：报告含 SLOC 与问题列表。"""
    resp = client.post("/api/v1/analysis", json={"source": "def f(a):\n    return a\n"}, headers=_headers())
    assert resp.status_code == 200
    data = resp.json()["data"]
    assert data["ok"] is True
    assert data["sloc"] == 2
    assert data["function_count"] == 1


def test_analysis_file(client: TestClient):
    """文件分析：对一份合法 Python 源码文件执行分析（ok=True）。"""
    file_path = ROOT / "devkit_common" / "envfile.py"  # 合法源码文件
    resp = client.post("/api/v1/analysis", json={"file_path": str(file_path)}, headers=_headers())
    assert resp.status_code == 200
    assert resp.json()["data"]["ok"] is True
    assert resp.json()["data"]["function_count"] >= 1


# ---------------- 构建 / 部署 / 日志 ----------------
def test_build_project(client: TestClient):
    """构建演示项目：应产出 tar.gz 产物。"""
    resp = client.post("/api/v1/build", json={"project_dir": str(DEMO_PROJECT)}, headers=_headers())
    assert resp.status_code == 200
    data = resp.json()["data"]
    assert data["name"] == "demo-add"
    assert data["version"] == "0.1.0"
    artifact = Path(data["artifact_path"])
    assert artifact.exists() and artifact.name.endswith(".tar.gz")

    # 查询构建记录
    resp2 = client.get(f"/api/v1/build/{data['build_id']}", headers=_headers())
    assert resp2.status_code == 200
    assert resp2.json()["data"]["build_id"] == data["build_id"]


def test_build_invalid_project(client: TestClient):
    """无效项目目录 -> 400 BUILD_INVALID_PROJECT。"""
    resp = client.post("/api/v1/build", json={"project_dir": str(ROOT / "不存在的目录")}, headers=_headers())
    assert resp.status_code == 400
    assert resp.json()["code"] == "BUILD_INVALID_PROJECT"


def test_deploy_and_rollback(client: TestClient):
    """部署 -> 查询 -> 再部署 -> 回滚，完整生命周期。"""
    # 先构建
    build_resp = client.post("/api/v1/build", json={"project_dir": str(DEMO_PROJECT)}, headers=_headers())
    artifact = build_resp.json()["data"]["artifact_path"]

    # 第一次部署
    resp = client.post(
        "/api/v1/deploy",
        json={"artifact_path": artifact, "env": "staging", "note": "测试部署-1"},
        headers=_headers(),
    )
    assert resp.status_code == 200
    first = resp.json()["data"]
    assert first["env"] == "staging"
    assert Path(first["release_dir"]).exists()

    # 查询部署记录
    resp2 = client.get(f"/api/v1/deploy/{first['deploy_id']}?env=staging", headers=_headers())
    assert resp2.status_code == 200
    assert resp2.json()["data"]["deploy_id"] == first["deploy_id"]

    # 第二次部署（产生回滚历史）
    client.post(
        "/api/v1/deploy",
        json={"artifact_path": artifact, "env": "staging", "note": "测试部署-2"},
        headers=_headers(),
    )

    # 回滚
    resp3 = client.post("/api/v1/deploy/rollback", json={"env": "staging"}, headers=_headers())
    assert resp3.status_code == 200
    assert resp3.json()["data"]["version"] == "0.1.0"

    # 非法环境
    resp4 = client.post(
        "/api/v1/deploy",
        json={"artifact_path": artifact, "env": "prod-extra"},
        headers=_headers(),
    )
    assert resp4.status_code == 400
    assert resp4.json()["code"] == "DEPLOY_INVALID_ENV"


def test_deploy_missing_artifact(client: TestClient):
    """产物不存在 -> 404 DEPLOY_ARTIFACT_NOT_FOUND。"""
    resp = client.post(
        "/api/v1/deploy",
        json={"artifact_path": str(ROOT / ".runtime" / "not-exists.tar.gz"), "env": "staging"},
        headers=_headers(),
    )
    assert resp.status_code == 404
    assert resp.json()["code"] == "DEPLOY_ARTIFACT_NOT_FOUND"


def test_logs_query(client: TestClient):
    """日志查询：按服务过滤应命中前面写入的日志。"""
    resp = client.get("/api/v1/logs", params={"service": "build", "limit": 10}, headers=_headers())
    assert resp.status_code == 200
    data = resp.json()["data"]
    assert data["total"] >= 1
    assert all(log["service"] == "build" for log in data["logs"])


def test_analyzer_core_direct():
    """核心层直接调用（无 HTTP）：语法错误报告。"""
    report = analyzer.analyze_source("def add(a, b)\n    return a + b\n")
    assert report["ok"] is False
    assert report["syntax_error"]["msg"] == "expected ':'"
