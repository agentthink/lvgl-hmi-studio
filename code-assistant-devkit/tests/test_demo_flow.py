"""端到端集成测试：技能模块 + 服务模块全链路打通。

链路：编排(debug_fix->unit_test->doc_writing->perf_opt)
      -> 修复代码落盘 -> 分析 -> 构建 -> 部署 -> 日志验证
"""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from fastapi.testclient import TestClient

from services.api.main import app
from skills.core.orchestrator import SkillOrchestrator
from skills.core.registry import SkillRegistry

# 与 run_demo_flow.py 保持一致的演示素材
BUGGY_SOURCE = '''"""demo_add 模块：演示技能链与服务链的示例模块。"""


def add(a, b)
    """两数相加。"""
    return a + b


def total_str(values):
    """将数值列表拼接为逗号分隔字符串。"""
    out = ""
    for v in values:
        out = out + str(v) + ","
    return out.rstrip(",")
'''

USER_REQUEST = "我的 demo_add 模块报语法错误，修复后请生成单元测试、写文档、做性能优化，再构建部署到 staging 环境"

TEST_CASES = [
    {"function": "add", "args": [1, 2], "expected": 3},
    {"function": "total_str", "args": [[1, 2, 3]], "expected": "1,2,3"},
]

DEMO_PROJECT = ROOT / "examples" / "demo_project"
DEMO_MODULE = DEMO_PROJECT / "src" / "demo_add" / "__init__.py"


def test_full_pipeline():
    """全链路：技能编排 -> 服务接口，逐段断言。"""
    client = TestClient(app)
    headers = {"X-API-Key": "dev-key-001"}

    # ---- 阶段一：技能编排 ----
    orchestrator = SkillOrchestrator(SkillRegistry())
    report = orchestrator.execute(
        USER_REQUEST,
        context={"source": BUGGY_SOURCE, "cases": TEST_CASES, "output_dir": str(DEMO_PROJECT)},
    )
    assert report.ok, f"编排失败: {report.error}"
    # 编排链应完整覆盖五类技能（codegen 因"生成单元测试"的"生成"触发词被纳入）
    assert report.chain == ["debug_fix", "unit_test", "doc_writing", "perf_opt", "codegen"]

    # 修复产物：语法错误被修复
    fixed_code = report.results["debug_fix"]["data"]["fixed_code"]
    assert "def add(a, b):" in fixed_code
    assert report.results["unit_test"]["data"]["failed"] == 0
    assert report.results["perf_opt"]["data"]["report"]  # 性能问题非空
    assert report.results["doc_writing"]["data"]["document"]

    # ---- 阶段二：修复代码落盘 ----
    original = DEMO_MODULE.read_text(encoding="utf-8")  # 保存现场，测试后恢复
    try:
        DEMO_MODULE.write_text(fixed_code, encoding="utf-8")

        # ---- 阶段三：服务接口 ----
        # 1) 静态分析
        resp = client.post(
            "/api/v1/analysis",
            json={"file_path": str(DEMO_MODULE)},
            headers=headers,
        )
        assert resp.status_code == 200
        assert resp.json()["data"]["ok"] is True

        # 2) 构建
        resp = client.post("/api/v1/build", json={"project_dir": str(DEMO_PROJECT)}, headers=headers)
        assert resp.status_code == 200
        build = resp.json()["data"]
        artifact = Path(build["artifact_path"])
        assert artifact.exists()

        # 3) 部署
        resp = client.post(
            "/api/v1/deploy",
            json={"artifact_path": build["artifact_path"], "env": "staging", "note": "集成测试"},
            headers=headers,
        )
        assert resp.status_code == 200
        deploy = resp.json()["data"]
        assert Path(deploy["release_dir"]).exists()

        # 4) 日志监控：应能查到 deploy 与 build 的日志
        resp = client.get("/api/v1/logs", params={"service": "deploy", "limit": 5}, headers=headers)
        assert resp.status_code == 200
        assert resp.json()["data"]["total"] >= 1

        resp = client.get("/api/v1/logs", params={"keyword": "构建完成"}, headers=headers)
        assert resp.json()["data"]["total"] >= 1
    finally:
        # 恢复演示项目源码（避免演示素材被测试污染）
        DEMO_MODULE.write_text(original, encoding="utf-8")
