"""devkit-mcp：代码助手开发套件 MCP Server。

把套件核心能力（静态分析 / 构建打包 / 自动部署 / 回滚 / 日志监控 / 技能编排）
暴露为 MCP 工具，供支持 MCP 协议的 Agent 客户端直接调用。

启动方式：
    python -m services.mcp_server                      # stdio 传输（默认）
    python -m services.mcp_server --transport http --port 9000   # HTTP 传输

依赖：mcp>=1.0（可选依赖，未安装时本模块给出明确提示）。
"""

from __future__ import annotations

import argparse
import json
import sys
from typing import Any, Optional

# ---- 依赖检查：mcp 为可选依赖，缺失时给出明确提示 ----
try:
    from mcp.server.fastmcp import FastMCP
except ImportError:  # pragma: no cover
    print(
        "错误：未安装 mcp 包。请先执行：pip install -e '.[mcp]'（注意：需 mcp>=1.0,<2.0，2.x API 不兼容）",
        file=sys.stderr,
    )
    sys.exit(1)


def _dumps(data: Any) -> str:
    """统一把结果序列化为 JSON 字符串（MCP 工具返回值）。"""
    return json.dumps(data, ensure_ascii=False, indent=2)


def _loads(raw: Optional[str]) -> dict:
    """解析可选的 JSON 字符串参数（容错：空/非法返回 {}）。"""
    if not raw:
        return {}
    try:
        data = json.loads(raw)
        return data if isinstance(data, dict) else {}
    except json.JSONDecodeError:
        return {}


mcp = FastMCP(
    "devkit",
    instructions=(
        "代码助手开发套件：静态分析(analyze) / 构建打包(build) / 自动部署(deploy) / "
        "回滚(rollback) / 日志监控(query_logs) / 能力清单(list_skills) / 技能编排(run_skills)。"
        "analyze 与 run_skills 的 source/context 支持 @/path/to/file 形式读取文件内容。"
    ),
)


# ---------- 静态分析 ----------
@mcp.tool(
    description="对 Python 源码或文件做 AST 级静态分析，返回语法错误/未定义名称/未使用导入/复杂度指标",
)
def analyze(
    file_path: Optional[str] = None,
    source: Optional[str] = None,
    filename: Optional[str] = None,
) -> str:
    """静态分析。file_path 与 source 二选一；source 以 @ 开头表示读取文件内容。"""
    from services.core import analyzer, log_monitor

    if source and source.startswith("@"):
        report = analyzer.analyze_file(source[1:])
    elif source is not None:
        report = analyzer.analyze_source(source, filename=filename or "<内存源码>")
    elif file_path:
        report = analyzer.analyze_file(file_path)
    else:
        return _dumps({"error": "analyze 需要 file_path 或 source 参数"})
    log_monitor.write_log(
        "analysis", "INFO",
        f"分析完成: {report['filename']}（{report['sloc']} 行, {len(report['issues'])} 个问题）",
        ok=report["ok"],
        issue_count=len(report["issues"]),
    )
    return _dumps(report)


# ---------- 构建打包 ----------
@mcp.tool(description="构建打包项目目录（须含 pyproject.toml），产出 sdist 风格 tar.gz 产物")
def build(project_dir: str, output_dir: Optional[str] = None) -> str:
    """构建项目并返回产物信息。"""
    from pathlib import Path

    from services.core import builder, log_monitor

    pdir = Path(project_dir)
    if not pdir.exists() or not (pdir / "pyproject.toml").exists():
        return _dumps({"error": f"无效项目目录或缺少 pyproject.toml: {pdir}"})
    try:
        record = builder.build_project(pdir, output_dir)
    except Exception as exc:  # noqa: BLE001
        return _dumps({"error": f"构建失败: {exc}"})
    log_monitor.write_log(
        "build", "INFO",
        f"构建完成: {record['name']}-{record['version']}（{record['file_count']} 个文件）",
        build_id=record["build_id"],
        artifact=record["artifact_path"],
    )
    return _dumps(record)


# ---------- 自动部署 ----------
@mcp.tool(description="部署构建产物（.tar.gz）到指定环境 dev/staging/prod，返回部署记录")
def deploy(artifact_path: str, env: str = "staging", note: str = "") -> str:
    """部署构建产物。"""
    from pathlib import Path

    from services.core import deployer, log_monitor

    artifact = Path(artifact_path)
    if not artifact.exists():
        return _dumps({"error": f"构建产物不存在: {artifact}"})
    try:
        record = deployer.deploy(artifact, env, note)
    except Exception as exc:  # noqa: BLE001
        return _dumps({"error": f"部署失败: {exc}"})
    log_monitor.write_log(
        "deploy", "INFO",
        f"部署完成: {record['version']} -> {record['env']}",
        deploy_id=record["deploy_id"],
        release_dir=record["release_dir"],
    )
    return _dumps(record)


@mcp.tool(description="回滚部署：恢复到上一个版本或指定 deploy_id")
def rollback(env: str = "staging", deploy_id: Optional[str] = None) -> str:
    """回滚部署。"""
    from services.core import deployer, log_monitor

    try:
        current = deployer.rollback(env, deploy_id)
    except Exception as exc:  # noqa: BLE001
        return _dumps({"error": f"回滚失败: {exc}"})
    log_monitor.write_log("deploy", "WARN", f"回滚完成: 当前版本 -> {current['version']} ({env})")
    return _dumps(current)


# ---------- 日志监控 ----------
@mcp.tool(
    description="查询运行日志（JSON Lines），支持 level/service/keyword/since 过滤，按时间倒序",
)
def query_logs(
    level: Optional[str] = None,
    service: Optional[str] = None,
    keyword: Optional[str] = None,
    since: Optional[str] = None,
    limit: int = 50,
) -> str:
    """查询日志。"""
    from services.core import log_monitor

    return _dumps(
        log_monitor.query_logs(
            level=level,
            service=service,
            keyword=keyword,
            since=since,
            limit=limit,
        )
    )


@mcp.tool(description="返回最近 N 条日志（不过滤）")
def tail_logs(lines: int = 20) -> str:
    """最近 N 条日志。"""
    from services.core import log_monitor

    return _dumps(log_monitor.tail_logs(lines))


# ---------- 技能模块 ----------
@mcp.tool(description="输出技能能力清单（注册表）：名称/描述/触发条件/依赖/后置校验")
def list_skills() -> str:
    """能力清单。"""
    from skills.core.registry import SkillRegistry

    registry = SkillRegistry()
    return _dumps(
        [
            {
                "name": m.name,
                "display_name": m.display_name,
                "description": m.description,
                "triggers": m.triggers,
                "required_inputs": m.required_inputs,
                "dependencies": m.dependencies,
                "post_checks": m.post_checks,
            }
            for m in registry.metas
        ]
    )


@mcp.tool(
    description=(
        "技能编排：按用户诉求文本做意图检测->链路规划->顺序执行，返回执行链与逐步结果。"
        "context 为 JSON 对象（key 如 source/cases）；值以 @ 开头表示读取文件内容。"
    )
)
def run_skills(request: str, context: Optional[str] = None, explicit: Optional[str] = None) -> str:
    """技能编排。explicit 为逗号分隔的技能名列表（可选）。"""
    from pathlib import Path

    from skills.core.orchestrator import SkillOrchestrator

    ctx: dict = _loads(context)
    for key, value in ctx.items():
        if isinstance(value, str) and value.startswith("@"):
            ctx[key] = Path(value[1:]).read_text(encoding="utf-8")
    names = [n.strip() for n in (explicit or "").split(",") if n.strip()] or None
    report = SkillOrchestrator().execute(request, context=ctx, explicit=names)
    return _dumps(
        {
            "request": report.request,
            "chain": report.chain,
            "results": report.results,
            "ok": report.ok,
            "error": report.error,
            "duration_ms": round(report.duration_ms, 1),
        }
    )


def main() -> None:
    """命令行入口：支持 stdio 与 http 传输。"""
    parser = argparse.ArgumentParser(prog="devkit-mcp", description="代码助手开发套件 MCP Server")
    parser.add_argument(
        "--transport",
        choices=["stdio", "http"],
        default="stdio",
        help="传输方式（默认 stdio）",
    )
    parser.add_argument("--host", default="127.0.0.1", help="HTTP 传输监听地址（默认 127.0.0.1）")
    parser.add_argument("--port", type=int, default=9000, help="HTTP 传输监听端口（默认 9000）")
    args = parser.parse_args()

    if args.transport == "http":
        import uvicorn

        # streamable-http 传输：FastMCP 暴露为 ASGI app，用 uvicorn 托管
        app = mcp.streamable_http_app()
        uvicorn.run(app, host=args.host, port=args.port, log_level="warning")
    else:
        mcp.run(transport="stdio")


if __name__ == "__main__":
    main()
