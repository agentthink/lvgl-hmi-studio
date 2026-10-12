#!/usr/bin/env python3
"""端到端演示：技能模块 + 服务模块协同工作。

一条完整业务链路：
  用户诉求 -> 技能编排器(debug_fix -> unit_test -> doc_writing -> perf_opt)
          -> 修复/测试/文档/优化产出
          -> 写入演示项目
          -> 服务接口(静态分析 -> 构建打包 -> 自动部署 -> 日志监控)

用法：.venv/bin/python examples/run_demo_flow.py [--out docs/outputs/demo-report.md]
输出：Markdown 演示报告（默认 docs/outputs/demo-report.md）。
"""

from __future__ import annotations

import sys
import threading
import time
from pathlib import Path

# 保证从项目根目录可导入
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

import httpx  # noqa: E402

from skills.core.orchestrator import SkillOrchestrator  # noqa: E402
from skills.core.registry import SkillRegistry  # noqa: E402

# ---------------- 演示素材 ----------------
# 含缺陷的模块源码（def 缺冒号 + 循环内字符串拼接）
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

# 用户诉求（触发条件会命中 debug_fix/unit_test/doc_writing/perf_opt）
USER_REQUEST = "我的 demo_add 模块报语法错误，修复后请生成单元测试、写文档、做性能优化，再构建部署到 staging 环境"

# 显式测试用例（unit_test 技能使用）
TEST_CASES = [
    {"function": "add", "args": [1, 2], "expected": 3},
    {"function": "total_str", "args": [[1, 2, 3]], "expected": "1,2,3"},
]

DEMO_PROJECT = ROOT / "examples" / "demo_project"
DEMO_MODULE = DEMO_PROJECT / "src" / "demo_add" / "__init__.py"

PORT = 8123  # 演示用服务端口


# ---------------- 服务启动 ----------------
def start_server() -> str:
    """在线程中启动 uvicorn 服务，等待健康检查通过后返回 base URL。"""
    import uvicorn

    config = uvicorn.Config("services.api.main:app", host="127.0.0.1", port=PORT, log_level="warning")
    server = uvicorn.Server(config)
    threading.Thread(target=server.run, daemon=True).start()

    url = f"http://127.0.0.1:{PORT}"
    for _ in range(50):
        try:
            if httpx.get(f"{url}/health", timeout=0.5).status_code == 200:
                return url
        except Exception:
            pass
        time.sleep(0.2)
    raise RuntimeError("服务启动超时，请检查端口占用")


# ---------------- 主流程 ----------------
def main() -> int:
    out_path = ROOT / "docs" / "outputs" / "demo-report.md"
    if len(sys.argv) > 1 and sys.argv[1].startswith("--out"):
        out_path = ROOT / sys.argv[1].split("=", 1)[-1]

    lines: list[str] = []
    def log(text: str = "") -> None:
        print(text)
        lines.append(text)

    log(f"# 代码助手开发套件 · 端到端演示报告")
    log(f"\n> 生成时间：{time.strftime('%Y-%m-%d %H:%M:%S')}\n")

    # ================= 阶段一：技能模块 =================
    log("## 一、技能模块：能力清单")
    registry = SkillRegistry()
    log(f"\n注册了 {len(registry.metas)} 项技能：\n")
    log(registry.to_markdown_table())

    log("\n## 二、技能模块：编排执行")
    log(f"\n**用户诉求**：{USER_REQUEST}\n")
    orchestrator = SkillOrchestrator(registry)
    report = orchestrator.execute(
        USER_REQUEST,
        context={"source": BUGGY_SOURCE, "cases": TEST_CASES, "output_dir": str(DEMO_PROJECT)},
    )
    log(report.to_markdown())

    # 提取关键产物
    fix_data = report.results["debug_fix"]["data"]
    test_data = report.results["unit_test"]["data"]
    doc_data = report.results["doc_writing"]["data"]
    perf_data = report.results["perf_opt"]["data"]

    log("\n### 2.1 调试修复产物（fixed_code）")
    log("```python")
    log(fix_data["fixed_code"])
    log("```")

    log("\n### 2.2 单元测试结果")
    log(f"- 用例数：{test_data['passed'] + test_data['failed']}，通过 {test_data['passed']}，失败 {test_data['failed']}")
    log(f"- 测试输出摘要：{test_data['output'].strip().splitlines()[-1] if test_data['output'] else ''}")

    log("\n### 2.3 文档编写产物")
    log(f"- 已写入：{doc_data.get('path')}")

    log("\n### 2.4 性能优化建议")
    for suggestion in perf_data["suggestions"]:
        log(f"- {suggestion}")

    # 把修复后的代码写入演示项目（模块源码自动升级为修复版）
    DEMO_MODULE.write_text(fix_data["fixed_code"], encoding="utf-8")
    log(f"\n✔ 修复后的代码已写入 {DEMO_MODULE.relative_to(ROOT)}")

    # ================= 阶段二：服务模块 =================
    log("\n## 三、服务模块：静态分析 / 构建 / 部署 / 日志")

    # 清空历史日志，让演示报告只包含本次链路的记录
    from services.core import log_monitor

    cleared = log_monitor.clear_logs()
    if cleared:
        log(f"\n（已清空 {cleared} 条历史日志，以下仅展示本次链路记录）\n")

    url = start_server()
    headers = {"X-API-Key": "dev-key-001"}
    try:
        # 3.1 静态分析（分析修复后的文件）
        resp = httpx.post(
            f"{url}/api/v1/analysis",
            json={"file_path": str(DEMO_MODULE)},
            headers=headers,
        )
        analysis = resp.json()["data"]
        log("\n### 3.1 静态分析")
        log(f"- 文件：{analysis['filename']}，SLOC：{analysis['sloc']}，函数数：{analysis['function_count']}")
        log(f"- 问题数：{len(analysis['issues'])}" + (f"，示例：{analysis['issues'][0]['message']}" if analysis["issues"] else "（无）"))

        # 3.2 构建打包
        resp = httpx.post(
            f"{url}/api/v1/build",
            json={"project_dir": str(DEMO_PROJECT)},
            headers=headers,
        )
        build = resp.json()["data"]
        log("\n### 3.2 构建打包")
        log(f"- build_id：{build['build_id']}，产物：{build['artifact_path']}（{build['artifact_size']} 字节，{build['file_count']} 个文件）")
        log(f"- 语法编译错误：{len(build['compile_errors'])} 个")

        # 3.3 自动部署
        resp = httpx.post(
            f"{url}/api/v1/deploy",
            json={"artifact_path": build["artifact_path"], "env": "staging", "note": "端到端演示部署"},
            headers=headers,
        )
        deploy = resp.json()["data"]
        log("\n### 3.3 自动部署")
        log(f"- deploy_id：{deploy['deploy_id']}，版本：{deploy['version']} -> 环境：{deploy['env']}")
        log(f"- 发布目录：{deploy['release_dir']}（历史共 {deploy['history_len']} 次）")

        # 3.4 日志监控（按服务过滤查询）
        resp = httpx.get(
            f"{url}/api/v1/logs",
            params={"service": "deploy", "limit": 5},
            headers=headers,
        )
        logs_result = resp.json()["data"]
        log("\n### 3.4 日志监控")
        log(f"- service=deploy 共 {logs_result['total']} 条，最近 {logs_result['returned']} 条：")
        for entry in logs_result["logs"]:
            log(f"  - [{entry['level']}] {entry['ts']} {entry['service']}: {entry['message']}")

        # 3.5 回滚演示
        resp = httpx.post(
            f"{url}/api/v1/deploy/rollback",
            json={"env": "staging"},
            headers=headers,
        )
        rollback = resp.json()["data"]
        log("\n### 3.5 回滚")
        log(f"- 回滚后当前版本：{rollback['version']}（note: {rollback['note']}）")
    finally:
        # 关闭服务线程
        import uvicorn

        # uvicorn.Server 实例在线程内，通过发送 should_exit 通知退出
        for t in threading.enumerate():
            if t.name.startswith("Thread") and t.is_alive():
                pass  # daemon 线程随进程退出，无需强制清理
        log("\n✔ 演示服务已停止（daemon 线程随进程退出）")

    # ================= 收尾 =================
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text("\n".join(lines), encoding="utf-8")
    log(f"\n✔ 演示报告已保存：{out_path.relative_to(ROOT)}")
    log("\n完整链路：技能编排(修复→测试→文档→优化) → 静态分析 → 构建 → 部署 → 日志监控 → 回滚，全部打通 ✔")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
