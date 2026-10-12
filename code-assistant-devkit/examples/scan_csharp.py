"""批量 C# 体检脚本：扫描目录下全部 .cs 文件，启发式 + Roslyn 真实语法检查。

用法：
    python examples/scan_csharp.py [目录...]          # 默认扫描仓库根 src/（firmware/ 为第三方 SDK，默认排除）
    python examples/scan_csharp.py src --json          # JSON 输出
    python examples/scan_csharp.py src --min-severity 高
    python examples/scan_csharp.py src firmware --include-build-out   # 强制包含构建产物目录

输出：按文件列出问题（高/中/低），末尾给出统计摘要。
     高 = 括号不平衡 / Roslyn 语法错误；中 = 未闭合字符串；低 = TODO 标记。
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path
from typing import Any, Dict, List

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from services.core import analyzer  # noqa: E402

# 跳过目录（构建/缓存/产物：bin、obj、out 等）
_SKIP_DIRS = {
    ".git", ".venv", ".runtime", "bin", "obj", "out", "node_modules",
    "build", "dist", ".pytest_cache", "__pycache__",
}


def _read_text_safe(path: Path) -> str:
    """读取文件文本，对非 UTF-8 编码容错（失败时按 UTF-8 替换非法字节，不再抛异常）。"""
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return path.read_text(encoding="utf-8", errors="replace")


def collect_cs_files(dirs: List[Path], include_build_out: bool = False) -> List[Path]:
    """递归收集 .cs 文件（默认跳过构建/缓存/产物目录）。"""
    files: List[Path] = []
    for d in dirs:
        if not d.exists():
            print(f"!! 目录不存在: {d}", file=sys.stderr)
            continue
        for p in sorted(d.rglob("*.cs")):
            parts = p.relative_to(d).parts
            if not include_build_out and any(part in _SKIP_DIRS for part in parts):
                continue
            files.append(p)
    return files


def roslyn_batch(files: List[Path]) -> Dict[str, Any]:
    """RoslynCheck 批量语法检查，返回 {file_path: syntax_diagnostics}。"""
    tool = analyzer._ROSELYN_TOOL
    if not tool.exists():
        print(f"!! RoslynCheck 未构建: {tool}", file=sys.stderr)
        return {}
    try:
        proc = subprocess.run(
            ["dotnet", str(tool), *[str(f) for f in files]],
            capture_output=True,
            text=True,
            timeout=120,
        )
        results = json.loads(proc.stdout or "[]")
    except (subprocess.TimeoutExpired, json.JSONDecodeError, OSError) as exc:
        print(f"!! RoslynCheck 执行失败: {exc}", file=sys.stderr)
        return {}
    return {Path(r["file"]).resolve(): r.get("syntax_diagnostics", []) for r in results}


def inspect_file(
    path: Path,
    syntax_diags: List[Dict[str, Any]],
    semantic_map: Dict[str, List[Dict[str, Any]]] | None = None,
) -> Dict[str, Any]:
    """单个文件：启发式 + Roslyn 语法 + （可选）deep 语义错误合并为问题列表。"""
    report = analyzer.analyze_csharp_source(_read_text_safe(path), filename=str(path))
    issues: List[Dict[str, Any]] = list(report["issues"])

    # Roslyn 语法错误 -> 高严重度
    for diag in syntax_diags:
        if diag.get("severity") == "error":
            issues.append(
                {
                    "type": "csharp_syntax",
                    "lineno": diag.get("line"),
                    "severity": "高",
                    "message": f"[Roslyn {diag.get('code')}] {diag.get('message')}",
                }
            )

    # deep：项目级 dotnet build 语义错误 -> 高严重度
    if semantic_map:
        for diag in semantic_map.get(str(path.resolve()), []):
            if diag.get("severity") == "error":
                issues.append(
                    {
                        "type": "csharp_semantic",
                        "lineno": diag.get("line"),
                        "severity": "高",
                        "message": f"[构建 {diag.get('code')}] {diag.get('message')}",
                    }
                )

    return {
        "file": str(path),
        "sloc": report["sloc"],
        "classes": report["class_count"],
        "methods": report["method_count"],
        "issues": sorted(issues, key=lambda i: {"高": 0, "中": 1, "低": 2}.get(i["severity"], 3)),
    }


def deep_build_all(files: List[Path]) -> Dict[str, List[Dict[str, Any]]]:
    """按所属项目分组，每组 dotnet build 一次，返回 {绝对路径: [语义诊断]}。"""
    projects: Dict[Path, List[Path]] = {}
    for f in files:
        pdir = analyzer._find_project_dir(f)
        projects.setdefault(pdir, []).append(f)

    merged: Dict[str, List[Dict[str, Any]]] = {}
    for pdir, pfiles in projects.items():
        if pdir is None:
            continue
        result = analyzer._build_project_diagnostics(pdir)
        if not result.get("checked"):
            print(f"!! deep 跳过 {pdir}: {result.get('reason', '不可用')}", file=sys.stderr)
            continue
        status = "成功" if result["build_success"] else "失败"
        n = sum(len(v) for v in result["diagnostics"].values())
        print(f"  deep: {pdir} 构建{status}（{len(pfiles)} 个文件, {n} 条诊断）")
        merged.update(result["diagnostics"])
    return merged


def main() -> int:
    parser = argparse.ArgumentParser(description="批量 C# 体检（启发式 + Roslyn [+ deep 语义]）")
    parser.add_argument("dirs", nargs="*", default=[], help="扫描目录（默认仓库根 src/；firmware/ 为第三方 SDK 默认不扫）")
    parser.add_argument("--json", action="store_true", help="JSON 输出")
    parser.add_argument("--deep", action="store_true", help="项目级 dotnet build 语义检查")
    parser.add_argument("--min-severity", choices=["高", "中", "低"], default="高", help="最低报告级别（默认 高）")
    parser.add_argument("--max-files", type=int, default=0, help="最多扫描文件数（0=不限，调试用）")
    parser.add_argument("--include-build-out", action="store_true", help="包含 out/ 等构建产物目录（默认跳过）")
    args = parser.parse_args()

    base = ROOT.parent  # 仓库根目录（code-assistant-devkit/ 的上一级）
    if args.dirs:
        targets = [Path(d) if Path(d).is_absolute() else base / d for d in args.dirs]
    else:
        # 默认只扫项目自身源码 src/；firmware/ 为第三方 SDK 源码树（含大量构建产物），不默认扫描
        targets = [base / "src"]

    files = collect_cs_files(targets, include_build_out=args.include_build_out)
    if args.max_files:
        files = files[: args.max_files]
    if not files:
        print("未找到 .cs 文件")
        return 1
    print(f"扫描 {len(files)} 个 .cs 文件...")

    syntax_map = roslyn_batch(files)
    semantic_map = deep_build_all(files) if args.deep else None
    results = [inspect_file(f, syntax_map.get(f.resolve(), []), semantic_map) for f in files]

    if args.json:
        print(json.dumps(results, ensure_ascii=False, indent=2))
        return 0

    # ---- 文本报告 ----
    rank = {"高": 0, "中": 1, "低": 2}
    min_rank = rank[args.min_severity]
    problem_files = 0
    for r in results:
        high = [i for i in r["issues"] if i["severity"] == "高"]
        if not high:
            continue
        problem_files += 1
        print(f"\n❌ {r['file']}（{r['sloc']} 行, {r['classes']} 类 / {r['methods']} 方法）")
        for i in r["issues"]:
            if rank.get(i["severity"], 3) <= min_rank:
                print(f"  [{i['severity']}] 行{i.get('lineno', '-')} {i['message'][:80]}")

    total_issues = sum(len(r["issues"]) for r in results)
    print(f"\n===== 摘要 =====")
    print(f"  文件总数: {len(files)}")
    print(f"  问题文件: {problem_files}（存在高严重度问题）")
    print(f"  问题总数: {total_issues}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
