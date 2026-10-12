"""devkit-cli：代码助手开发套件命令行入口。

在本地直接调用核心能力（不依赖 HTTP 服务常驻），供 Agent / 脚本 / 人工使用。
所有子命令支持 --json 输出原始 JSON（机器可读）；默认输出人类可读文本。

用法示例：
    devkit-cli health
    devkit-cli analyze src/mod.py
    devkit-cli analyze --source "def add(a,b): return a+b"
    devkit-cli build . --output dist
    devkit-cli deploy dist/demo-add-0.1.0.tar.gz --env staging --note "发布 v0.1.0"
    devkit-cli rollback --env staging
    devkit-cli logs --service build --limit 10
    devkit-cli skills-list
    devkit-cli skills "修复这个报错" --context source=@src/mod.py
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any, Dict, List

# ---- 命令实现（延迟导入，保证 help/--version 快速响应） ----


def _cmd_health(args: argparse.Namespace) -> Dict[str, Any]:
    """输出配置信息与可用服务。"""
    from devkit_common.settings import get_settings

    s = get_settings()
    return {
        "status": "ok",
        "version": s.version,
        "host": s.host,
        "port": s.port,
        "work_dir": s.work_dir,
        "log_level": s.log_level,
        "services": ["analysis", "build", "deploy", "logs"],
    }


def _cmd_analyze(args: argparse.Namespace) -> Dict[str, Any]:
    """静态分析：文件或源码。"""
    from services.core import analyzer, log_monitor

    if args.source is not None:
        report = analyzer.analyze_source(args.source, filename=args.filename or "<内存源码>")
    elif args.file:
        report = analyzer.analyze_file(args.file, deep=getattr(args, "deep", False))
    else:
        raise SystemExit("错误：analyze 需要 --source 或 FILE 参数")
    log_monitor.write_log(
        "analysis", "INFO",
        f"分析完成: {report['filename']}（{report['sloc']} 行, {len(report['issues'])} 个问题）",
        ok=report["ok"],
        issue_count=len(report["issues"]),
    )
    return report


def _cmd_build(args: argparse.Namespace) -> Dict[str, Any]:
    """构建打包项目目录。"""
    from services.core import builder, log_monitor

    project_dir = Path(args.project_dir)
    has_manifest = (project_dir / "pyproject.toml").exists() or bool(list(project_dir.glob("*.csproj")))
    if not project_dir.exists() or not has_manifest:
        raise SystemExit(f"错误：无效项目目录或缺少 pyproject.toml/*.csproj: {project_dir}")
    record = builder.build_project(project_dir, args.output)
    log_monitor.write_log(
        "build", "INFO",
        f"构建完成: {record['name']}-{record['version']}（{record['file_count']} 个文件）",
        build_id=record["build_id"],
        artifact=record["artifact_path"],
    )
    return record


def _cmd_deploy(args: argparse.Namespace) -> Dict[str, Any]:
    """部署构建产物。"""
    from services.core import deployer, log_monitor

    artifact = Path(args.artifact)
    if not artifact.exists():
        raise SystemExit(f"错误：构建产物不存在: {artifact}")
    record = deployer.deploy(artifact, args.env, args.note)
    log_monitor.write_log(
        "deploy", "INFO",
        f"部署完成: {record['version']} -> {record['env']}",
        deploy_id=record["deploy_id"],
        release_dir=record["release_dir"],
    )
    return record


def _cmd_rollback(args: argparse.Namespace) -> Dict[str, Any]:
    """回滚部署。"""
    from services.core import deployer, log_monitor

    current = deployer.rollback(args.env, args.deploy_id)
    log_monitor.write_log("deploy", "WARN", f"回滚完成: 当前版本 -> {current['version']} ({args.env})")
    return current


def _cmd_logs(args: argparse.Namespace) -> Dict[str, Any]:
    """查询日志。"""
    from services.core import log_monitor

    if args.tail:
        return {"total": args.tail, "returned": args.tail, "logs": log_monitor.tail_logs(args.tail)}
    return log_monitor.query_logs(
        level=args.level,
        service=args.service,
        keyword=args.keyword,
        since=args.since,
        limit=args.limit,
    )


def _cmd_skills_list(args: argparse.Namespace) -> Dict[str, Any]:
    """输出能力清单。"""
    from skills.core.registry import SkillRegistry

    registry = SkillRegistry()
    return {
        "count": len(registry.metas),
        "skills": [
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
        ],
    }


def _cmd_skills(args: argparse.Namespace) -> Dict[str, Any]:
    """技能编排：意图检测 -> 链路规划 -> 顺序执行。"""
    from skills.core.orchestrator import SkillOrchestrator

    context: Dict[str, Any] = {}
    for item in args.context or []:
        key, _, value = item.partition("=")
        if value.startswith("@"):  # @path 表示从文件读取内容
            value = Path(value[1:]).read_text(encoding="utf-8")
        context[key.strip()] = value

    report = SkillOrchestrator().execute(args.request, context=context, explicit=args.skill)
    return {
        "request": report.request,
        "chain": report.chain,
        "results": report.results,
        "ok": report.ok,
        "error": report.error,
        "duration_ms": round(report.duration_ms, 1),
    }


# ---- 输出渲染 ----


def _humanize(cmd: str, data: Any) -> str:
    """把命令结果渲染为人类可读文本（Agent 友好，尽量紧凑）。"""
    if cmd == "health":
        s = data
        return (
            f"✔ 套件 v{s['version']} | 监听 {s['host']}:{s['port']} | "
            f"work_dir={s['work_dir']} | 服务: {', '.join(s['services'])}"
        )
    if cmd == "analyze":
        r = data
        if not r.get("ok"):
            return f"❌ 分析失败：{r.get('syntax_error') or r.get('issues')}"
        lang = r.get("language", "python")
        if lang == "csharp":
            lines = [
                f"✔ {r['filename']} [{lang}] | {r['sloc']} 行 | "
                f"{r['class_count']} 类 / {r['method_count']} 方法 | using {len(r.get('usings', []))} 条"
            ]
        else:
            lines = [f"✔ {r['filename']} | {r['sloc']} 行 | {r['function_count']} 个函数"]
        for issue in r.get("issues", []):
            lines.append(f"  [{issue['severity']}] 行{issue.get('lineno', '-')} {issue['message']}")
        if not r.get("issues"):
            lines.append("  无问题")
        compile_info = r.get("compile")
        if compile_info is not None:
            if compile_info.get("checked"):
                n = len([d for d in compile_info.get("syntax", []) if d.get("severity") == "error"])
                lines.append(f"  Roslyn 语法检查: ✅ 已启用（{n} 个语法错误）")
            else:
                lines.append(f"  Roslyn 语法检查: ⏭ 跳过（{compile_info.get('reason', '不可用')}）")
        deep_info = r.get("deep")
        if deep_info is not None:
            if deep_info.get("checked"):
                n = len([d for d in deep_info.get("diagnostics", []) if d.get("severity") == "error"])
                lines.append(f"  深度构建检查: ✅ 完成（{n} 个语义错误, 构建{'成功' if deep_info.get('build_success') else '失败'}）")
            else:
                lines.append(f"  深度构建检查: ⏭ 跳过（{deep_info.get('reason', '不可用')}）")
        return "\n".join(lines)
    if cmd == "build":
        r = data
        errs = r.get("compile_errors") or []
        lines = [
            f"✔ 构建完成 {r['name']}-{r['version']} | build_id={r['build_id']}",
            f"  产物: {r['artifact_path']}（{r['artifact_size']} 字节, {r['file_count']} 个文件）",
        ]
        if errs:
            lines.append(f"  ⚠ 语法错误 {len(errs)} 个：{errs[:3]}")
        return "\n".join(lines)
    if cmd == "deploy":
        r = data
        return f"✔ 部署 {r['version']} -> {r['env']} | deploy_id={r['deploy_id']} | {r['release_dir']}"
    if cmd == "rollback":
        r = data
        return f"✔ 回滚完成 -> {r['version']}（{r['env']}）| note: {r.get('note', '')}"
    if cmd == "logs":
        r = data
        lines = [f"日志 {r['returned']}/{r['total']} 条（最新在前）:"]
        for log in r["logs"]:
            lines.append(
                f"  [{log.get('level', 'INFO')}] {log.get('ts', '')} {log.get('service', '')}: {log.get('message', '')}"
            )
        return "\n".join(lines)
    if cmd == "skills-list":
        lines = [f"能力清单（{data['count']} 项）:"]
        for s in data["skills"]:
            lines.append(
                f"  [{s['name']}] {s['display_name']} — {s['description']} | "
                f"触发: {', '.join(s['triggers'][:3])}"
            )
        return "\n".join(lines)
    if cmd == "skills":
        r = data
        lines = [
            f"编排报告: 请求「{r['request']}」",
            f"  执行链: {' → '.join(r['chain']) or '（无命中）'}",
            f"  结果: {'✅ 全部成功' if r['ok'] else '❌ 存在失败'} | 耗时 {r['duration_ms']} ms",
        ]
        if r.get("error"):
            lines.append(f"  ⚠ {r['error']}")
        return "\n".join(lines)
    return json.dumps(data, ensure_ascii=False, indent=2)


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="devkit-cli",
        description="代码助手开发套件命令行工具（Agent 工具箱）",
    )
    parser.add_argument("--json", action="store_true", help="输出原始 JSON（机器可读）")
    sub = subparsers = parser.add_subparsers(dest="command", required=True)

    def _add_json(p: argparse.ArgumentParser) -> None:
        # 每个子命令都接受 --json（default=SUPPRESS：子命令未提供时继承顶层值）
        p.add_argument("--json", action="store_true", default=argparse.SUPPRESS, help=argparse.SUPPRESS)

    p = sub.add_parser("health", help="配置与状态")
    _add_json(p)
    p.set_defaults(func=_cmd_health)

    p = sub.add_parser("analyze", help="静态分析（文件或源码）")
    _add_json(p)
    p.add_argument("file", nargs="?", help="待分析文件路径")
    p.add_argument("--source", help="待分析源码内容（与 file 二选一）")
    p.add_argument("--filename", help="源码对应的文件名（展示用，默认 <内存源码>）")
    p.add_argument("--deep", action="store_true", help="C# 文件执行项目级 dotnet build 语义检查")
    p.set_defaults(func=_cmd_analyze)

    p = sub.add_parser("build", help="构建打包项目目录")
    _add_json(p)
    p.add_argument("project_dir", help="项目根目录（须含 pyproject.toml）")
    p.add_argument("--output", help="产物输出目录（默认取配置）")
    p.set_defaults(func=_cmd_build)

    p = sub.add_parser("deploy", help="部署构建产物")
    _add_json(p)
    p.add_argument("artifact", help="构建产物 .tar.gz 路径")
    p.add_argument("--env", default="staging", choices=["dev", "staging", "prod"], help="目标环境")
    p.add_argument("--note", default="", help="部署备注")
    p.set_defaults(func=_cmd_deploy)

    p = sub.add_parser("rollback", help="回滚部署")
    _add_json(p)
    p.add_argument("--env", default="staging", choices=["dev", "staging", "prod"], help="目标环境")
    p.add_argument("--deploy-id", help="回滚到的部署记录 id（缺省回退上一版本）")
    p.set_defaults(func=_cmd_rollback)

    p = sub.add_parser("logs", help="查询日志")
    _add_json(p)
    p.add_argument("--level", help="级别过滤 DEBUG/INFO/WARN/ERROR/FATAL")
    p.add_argument("--service", help="服务名过滤")
    p.add_argument("--keyword", help="关键词过滤（message 子串）")
    p.add_argument("--since", help="起始时间（ISO 8601）")
    p.add_argument("--limit", type=int, default=50, help="返回条数上限")
    p.add_argument("--tail", type=int, help="最近 N 条（不过滤）")
    p.set_defaults(func=_cmd_logs)

    p = sub.add_parser("skills-list", help="输出技能能力清单")
    _add_json(p)
    p.set_defaults(func=_cmd_skills_list)

    p = sub.add_parser("skills", help="技能编排：意图检测 -> 执行链")
    _add_json(p)
    p.add_argument("request", help="用户诉求文本")
    p.add_argument("--skill", action="append", help="显式指定技能（可多次）")
    p.add_argument("--context", action="append", help="上下文 KEY=VALUE（VALUE 以 @ 开头表示读取文件内容）")
    p.set_defaults(func=_cmd_skills)

    return parser


def main(argv: List[str] | None = None) -> int:
    """CLI 入口。返回进程退出码。"""
    parser = _build_parser()
    args = parser.parse_args(argv)

    try:
        data = args.func(args)
    except FileNotFoundError as exc:
        print(f"错误: {exc}", file=sys.stderr)
        return 2
    except (ValueError, RuntimeError, KeyError) as exc:
        print(f"错误: {exc}", file=sys.stderr)
        return 2
    except SystemExit as exc:  # 参数校验类错误
        if isinstance(exc.code, str):
            print(exc.code, file=sys.stderr)
            return 2
        raise

    if getattr(args, "json", False):
        print(json.dumps(data, ensure_ascii=False, indent=2))
    else:
        print(_humanize(args.command, data))
    return 0


if __name__ == "__main__":
    sys.exit(main())
