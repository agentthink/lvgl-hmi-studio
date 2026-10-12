"""静态分析服务（analyzer）：AST 级代码体检。

支持语言：
  - python：AST 级分析（语法错误 / 未定义名称 / 未使用导入 / 复杂度指标）
  - csharp：启发式分析（括号平衡 / 字符串闭合 / using 收集 / 类与方法统计 /
    TODO 标记 / 行级指标）+ 可选 Roslyn 真实语法检查（需 dotnet SDK）。

Roslyn 深度检查（analyze_file 对 .cs 文件自动启用）：
  - 调用 tools/roslyn_check/（C# 单文件编译诊断工具，Roslyn CSharpCompilation）
  - 纯语法诊断（SyntaxTree 级，不依赖引用）可靠，作为"高"严重度加入 issues
  - 编译诊断（含语义/引用）可能有缺引用误报，仅放入 report["compile"] 供参考
  - dotnet 不可用或工具未构建时静默跳过（compile.checked = false）

说明：本实现以"可运行 + 结果可解释"为目标，是启发式静态分析，
     生产环境可无缝替换为 pyflakes / mypy / bandit / Roslyn 等真实分析器。
"""

from __future__ import annotations

import ast
import builtins
import json
import os
import re
import shutil
import subprocess
from pathlib import Path
from typing import Any, Dict, List

from devkit_common.settings import get_settings

# 内建名称集合
_BUILTINS = set(dir(builtins)) | {"True", "False", "None"}

# RoslynCheck 工具路径（tools/roslyn_check/bin/Release/net10.0/RoslynCheck.dll）
_ROSELYN_TOOL = (
    Path(__file__).resolve().parents[2]
    / "tools"
    / "roslyn_check"
    / "bin"
    / "Release"
    / "net10.0"
    / "RoslynCheck.dll"
)


def _complexity_of(node: ast.AST) -> int:
    """圈复杂度：if/for/while/and/or/bool 运算 + 1。"""
    complexity = 1
    for child in ast.walk(node):
        if isinstance(child, (ast.If, ast.For, ast.AsyncFor, ast.While, ast.Assert)):
            complexity += 1
        elif isinstance(child, ast.BoolOp):
            complexity += len(child.values) - 1
    return complexity


# ============================================================
# 语言检测
# ============================================================

_CSHARP_HINTS = (
    "using System",
    "using System.",
    "namespace ",
    "public class",
    "internal class",
    "static void Main",
    "Console.WriteLine",
    "StringBuilder",
)


def detect_language(filename: str | None = None, source: str | None = None) -> str:
    """检测源码语言：优先按扩展名，其次按内容特征。

    Returns:
        "python" / "csharp" / "unknown"
    """
    if filename:
        suffix = Path(filename).suffix.lower()
        if suffix == ".cs":
            return "csharp"
        if suffix == ".py":
            return "python"
    if source:
        for hint in _CSHARP_HINTS:
            if hint in source:
                return "csharp"
    return "unknown"


# ============================================================
# C# 启发式分析器
# ============================================================

# C# 括号配对（用于平衡检查）
_CS_PAIRS = {"{": "}", "(": ")", "[": "]"}

# 正则：using 指令 / 类 / 接口 / 方法 / 枚举
_CS_USING_RE = re.compile(r"^\s*using\s+(?:static\s+)?([\w.]+)\s*;", re.MULTILINE)
_CS_CLASS_RE = re.compile(
    r"(?:\b(?:public|private|protected|internal)\s+)?"
    r"(?:static\s+|partial\s+|abstract\s+|sealed\s+|record\s+)*"
    r"\bclass\s+([A-Za-z_]\w*)"
)
_CS_INTERFACE_RE = re.compile(
    r"(?:\b(?:public|private|protected|internal)\s+)?"
    r"\binterface\s+([A-Za-z_]\w*)"
)
_CS_METHOD_RE = re.compile(
    r"(?:\b(?:public|private|protected|internal)\s+)?"
    r"(?:static\s+|async\s+|virtual\s+|override\s+|sealed\s+|abstract\s+)*"
    r"[\w<>\[\],\?\s]+\s+([A-Za-z_]\w*)\s*\("
)
_CS_TODO_RE = re.compile(r"TODO|FIXME|HACK|XXX", re.IGNORECASE)


def _mask_csharp(source: str) -> str:
    """把 C# 字符串字面量与注释替换为空格，返回可供括号平衡/扫描的文本。

    支持的 C# 词法：/* */ 块注释、// 行注释、普通字符串（含 \\ 转义）、
    @"" verbatim 字符串（"" 转义）、$"" 插值字符串（{...} 代码区内的
    括号/字符串按代码处理）、三引号原始字符串（跨行保留换行）。
    逐字符状态机，无第三方依赖。
    """
    out: List[str] = []
    i, n = 0, len(source)
    in_block = False        # /* */ 块注释
    in_string = False       # 字符串遮蔽中
    string_kind = "normal"  # normal / interpolated（$" 或 $@"）
    string_verbatim = False  # @" 或 $@"（" 不转义，"" 转义）
    interp_depth = 0        # 插值代码区 { 深度
    while i < n:
        c = source[i]

        # ---- 块注释 ----
        if in_block:
            if source.startswith("*/", i):
                in_block = False
                out.append("  ")
                i += 2
            else:
                out.append(" ")
                i += 1
            continue
        if not in_string and interp_depth == 0 and source.startswith("/*", i):
            in_block = True
            out.append("  ")
            i += 2
            continue

        # ---- 行注释（字符串外）----
        if not in_string and source.startswith("//", i):
            out.append("  ")
            i += 2
            while i < n and source[i] != "\n":
                out.append(" ")
                i += 1
            continue

        # ---- 原始字符串字面量 """..."""（字符串外）----
        if not in_string and source.startswith('"""', i):
            out.append("   ")
            i += 3
            while i < n:
                if source.startswith('"""', i):
                    out.append("   ")
                    i += 3
                    break
                out.append("\n" if source[i] == "\n" else " ")
                i += 1
            continue

        # ---- 字符串内：遮蔽内容 ----
        if in_string:
            if string_kind == "interpolated":
                if c == "{":
                    if source.startswith("{{", i):
                        out.append("  ")  # 转义字面量 {{
                        i += 2
                        continue
                    # 单个 { → 进入插值代码区（代码内的括号参与平衡检查）
                    interp_depth = 1
                    in_string = False
                    out.append("{")
                    i += 1
                    continue
                if c == "}" and source.startswith("}}", i):
                    out.append("  ")  # 转义字面量 }}
                    i += 2
                    continue
            if c == "\\" and not string_verbatim:
                out.append("  ")
                i += 2
                continue
            if c == '"' and string_verbatim and source.startswith('""', i):
                out.append("  ")  # verbatim 字符串内的 "" 转义
                i += 2
                continue
            if c == '"':
                out.append(" ")
                i += 1
                in_string = False
                string_kind = "normal"
                string_verbatim = False
                continue
            out.append("\n" if c == "\n" else " ")
            i += 1
            continue

        # ---- 插值代码区（$"..." 的 {..} 内，按代码处理并跟踪深度）----
        if interp_depth > 0:
            if c == "{":
                interp_depth += 1
                out.append("{")
                i += 1
                continue
            if c == "}":
                interp_depth -= 1
                out.append("}")
                i += 1
                if interp_depth == 0:
                    in_string = True
                    string_kind = "interpolated"  # 回到插值字符串继续遮蔽
                continue
            # 代码区内的字符串字面量：遮蔽内容但不改变插值深度
            if c == '"':
                out.append(" ")
                i += 1
                while i < n and source[i] != '"':
                    if source[i] == "\\":
                        out.append("  ")
                        i += 2
                        continue
                    out.append(" ")
                    i += 1
                if i < n:
                    out.append(" ")
                    i += 1
                continue
            if c == "'":
                out.append(" ")
                i += 1
                if i < n and source[i] == "\\":
                    out.append("  ")
                    i += 2
                elif i < n:
                    out.append(" ")
                    i += 1
                if i < n and source[i] == "'":
                    out.append(" ")
                    i += 1
                continue
            out.append(c)
            i += 1
            continue

        # ---- 代码区：字符串开始（识别 $ / @ 前缀）----
        if c == '"':
            out.append(" ")
            i += 1
            in_string = True
            # 前缀反查：$" → interpolated；@" → verbatim；$@" → interpolated+verbatim
            prev1 = source[i - 2] if i >= 2 else ""
            prev2 = source[i - 3] if i >= 3 else ""
            string_kind = "interpolated" if prev1 == "$" else "normal"
            string_verbatim = prev1 == "@" or prev2 == "$"
            continue

        # ---- 代码区：字符字面量 'x' / '\n' / '\u200b' ----
        if c == "'":
            out.append(" ")
            i += 1
            if i < n and source[i] == "\\":
                # 转义：\x \uXXXX \UXXXXXXXX 等，跳过整个转义序列
                out.append("  ")
                i += 2
                # \u200b 是 4 位十六进制；\x 可变长——统一跳过到下一个 ' 或行尾
                while i < n and source[i] != "'" and source[i] != "\n":
                    out.append(" ")
                    i += 1
            elif i < n:
                out.append(" ")
                i += 1
            if i < n and source[i] == "'":
                out.append(" ")
                i += 1
            continue

        out.append(c)
        i += 1
    return "".join(out)


def _check_csharp_balance(masked: str) -> List[Dict[str, Any]]:
    """括号平衡检查：返回问题列表（多余/缺失/不匹配）。"""
    stack: List[tuple] = []
    issues: List[Dict[str, Any]] = []
    for lineno, line in enumerate(masked.splitlines(), 1):
        for ch in line:
            if ch in "{([":
                stack.append((ch, lineno))
            elif ch in "})]":
                if not stack:
                    issues.append(
                        {
                            "type": "unbalanced_bracket",
                            "lineno": lineno,
                            "severity": "高",
                            "message": f"多余的闭合括号 {ch}",
                        }
                    )
                else:
                    open_ch, open_line = stack.pop()
                    if _CS_PAIRS[open_ch] != ch:
                        issues.append(
                            {
                                "type": "unbalanced_bracket",
                                "lineno": lineno,
                                "severity": "高",
                                "message": f"括号不匹配：第 {open_line} 行的 {open_ch} 与第 {lineno} 行的 {ch}",
                            }
                        )
    for ch, lineno in stack:
        issues.append(
            {
                "type": "unbalanced_bracket",
                "lineno": lineno,
                "severity": "高",
                "message": f"未闭合的括号 {ch}",
            }
        )
    return issues


def analyze_csharp_source(source: str, filename: str = "<内存源码>") -> Dict[str, Any]:
    """分析 C# 源码，返回启发式体检报告。

    报告字段：filename / language / ok / sloc / comment_lines / class_count /
    method_count / usings / issues / syntax_error。
    """
    lines = source.splitlines()
    sloc = len([l for l in lines if l.strip()])
    comment_lines = len(
        [l for l in lines if l.strip().startswith("//") or l.strip().startswith("/*") or l.strip().startswith("*")]
    )

    usings = [m.group(1) for m in _CS_USING_RE.finditer(source)]
    class_count = len(_CS_CLASS_RE.findall(source))
    method_count = len(_CS_METHOD_RE.findall(source))
    interface_count = len(_CS_INTERFACE_RE.findall(source))

    masked = _mask_csharp(source)
    issues = _check_csharp_balance(masked)

    # 字符串闭合检查：屏蔽后仍残留的未配对双引号（粗略启发式）
    for lineno, line in enumerate(masked.splitlines(), 1):
        if '"' in line:
            issues.append(
                {
                    "type": "unclosed_string",
                    "lineno": lineno,
                    "severity": "中",
                    "message": "存在未闭合的字符串字面量（或转义解析异常）",
                }
            )

    # TODO / FIXME 标记
    for lineno, line in enumerate(lines, 1):
        m = _CS_TODO_RE.search(line)
        if m:
            issues.append(
                {
                    "type": "todo_marker",
                    "lineno": lineno,
                    "severity": "低",
                    "message": f"遗留标记 {m.group(0)}",
                }
            )

    return {
        "filename": filename,
        "language": "csharp",
        "ok": not any(i["severity"] == "高" for i in issues),
        "sloc": sloc,
        "comment_lines": comment_lines,
        "class_count": class_count,
        "method_count": method_count,
        "interface_count": interface_count,
        "usings": usings,
        "syntax_error": None,
        "issues": issues,
    }


def analyze_source(source: str, filename: str = "<内存源码>") -> Dict[str, Any]:
    """分析源码字符串，返回报告 dict。自动检测语言（python / csharp）。

    Args:
        source: 源码
        filename: 文件名（用于语言检测与展示）

    Returns:
        语言相关报告；python 含 AST 级指标，csharp 含启发式体检。
    """
    language = detect_language(filename=filename, source=source)
    if language == "csharp":
        return analyze_csharp_source(source, filename)

    # ---- Python（默认分支）----
    report: Dict[str, Any] = {
        "filename": filename,
        "language": "python",
        "ok": True,
        "sloc": len([l for l in source.splitlines() if l.strip()]),
        "function_count": 0,
        "avg_function_lines": 0.0,
        "syntax_error": None,
        "issues": [],
        "functions": [],
    }

    try:
        tree = ast.parse(source)
    except SyntaxError as exc:
        report["ok"] = False
        report["syntax_error"] = {
            "lineno": exc.lineno,
            "offset": exc.offset,
            "msg": exc.msg,
            "text": exc.text,
        }
        report["issues"].append(
            {
                "type": "syntax_error",
                "lineno": exc.lineno,
                "severity": "高",
                "message": f"语法错误: {exc.msg}",
            }
        )
        return report

    # ---- 函数级指标 ----
    funcs_info = []
    total_lines = 0
    for node in ast.walk(tree):
        if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            length = (node.end_lineno or node.lineno) - node.lineno + 1
            total_lines += length
            funcs_info.append(
                {
                    "name": node.name,
                    "lineno": node.lineno,
                    "lines": length,
                    "complexity": _complexity_of(node),
                    "params": [a.arg for a in node.args.args],
                }
            )
    report["function_count"] = len(funcs_info)
    report["avg_function_lines"] = round(total_lines / len(funcs_info), 1) if funcs_info else 0.0
    report["functions"] = funcs_info

    # ---- 已定义名称收集（粗糙全局视图）----
    defined: set[str] = set()
    for node in ast.walk(tree):
        if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
            defined.add(node.name)
            for arg in node.args.args + node.args.kwonlyargs:
                defined.add(arg.arg)
            if node.args.vararg:
                defined.add(node.args.vararg.arg)
            if node.args.kwarg:
                defined.add(node.args.kwarg.arg)
        elif isinstance(node, ast.arg):
            defined.add(node.arg)
        elif isinstance(node, (ast.Import, ast.ImportFrom)):
            for alias in node.names:
                defined.add(alias.asname or alias.name.split(".")[0])
        elif isinstance(node, ast.Assign):
            for t in node.targets:
                if isinstance(t, ast.Name):
                    defined.add(t.id)
        elif isinstance(node, ast.AugAssign) and isinstance(node.target, ast.Name):
            defined.add(node.target.id)
        elif isinstance(node, (ast.For, ast.AsyncFor)) and isinstance(node.target, ast.Name):
            defined.add(node.target.id)

    # ---- 未定义名称 ----
    for node in ast.walk(tree):
        if isinstance(node, ast.Name) and isinstance(node.ctx, ast.Load):
            if node.id not in defined and node.id not in _BUILTINS:
                report["issues"].append(
                    {
                        "type": "undefined_name",
                        "lineno": getattr(node, "lineno", None),
                        "severity": "高",
                        "message": f"未定义名称 {node.id!r}",
                    }
                )

    # ---- 未使用导入 ----
    used = {n.id for n in ast.walk(tree) if isinstance(n, ast.Name) and isinstance(n.ctx, ast.Load)}
    for node in ast.walk(tree):
        if isinstance(node, (ast.Import, ast.ImportFrom)):
            for alias in node.names:
                name = alias.asname or alias.name.split(".")[0]
                if name not in used:
                    report["issues"].append(
                        {
                            "type": "unused_import",
                            "lineno": getattr(node, "lineno", None),
                            "severity": "低",
                            "message": f"导入 {name!r} 未被使用",
                        }
                    )
    return report


def _run_roslyn_check(file_path: Path) -> Dict[str, Any]:
    """调用 RoslynCheck 对 .cs 文件做真实编译诊断。

    Returns:
        {"checked": True, "syntax": [...], "compile": [...]} —— 成功
        {"checked": False, "reason": "..."}                     —— 不可用/失败
    """
    if not _ROSELYN_TOOL.exists():
        return {"checked": False, "reason": "RoslynCheck 未构建（cd tools/roslyn_check && dotnet build -c Release）"}
    if shutil.which("dotnet") is None:
        return {"checked": False, "reason": "未找到 dotnet SDK"}
    try:
        proc = subprocess.run(
            ["dotnet", str(_ROSELYN_TOOL), str(file_path)],
            capture_output=True,
            text=True,
            timeout=30,
        )
        data = json.loads(proc.stdout or "[]")
        # RoslynCheck 输出为 JSON 数组（多文件模式），取当前文件对应项
        item = data[0] if isinstance(data, list) and data else data
        return {
            "checked": True,
            "syntax": item.get("syntax_diagnostics", []) if isinstance(item, dict) else [],
            "compile": item.get("compile_diagnostics", []) if isinstance(item, dict) else [],
        }
    except (subprocess.TimeoutExpired, json.JSONDecodeError, OSError) as exc:
        return {"checked": False, "reason": f"RoslynCheck 执行失败: {exc}"}


# 项目构建错误行解析：/path/File.cs(12,5): error CS0029: message [/proj.csproj]
_BUILD_ERR_RE = re.compile(
    r"^(.+?)\((\d+),(\d+)\):\s+(error|warning)\s+(CS\d+):\s+(.*)$",
    re.MULTILINE,
)


def _find_project_dir(file_path: Path) -> Path | None:
    """从文件所在目录向上查找包含 *.csproj 的项目目录。"""
    for d in (file_path.parent, *file_path.parent.parents):
        if list(d.glob("*.csproj")):
            return d
        if d.parent == d:
            break
    return None


def _dotnet_env() -> Dict[str, str]:
    """构造 dotnet 子进程环境：DOTNET_CLI_HOME 未设置时固定到 devkit 根目录。"""
    env = dict(os.environ)
    if "DOTNET_CLI_HOME" not in env:
        dotnet_home = Path(__file__).resolve().parents[2] / ".runtime" / "tmp" / "dotnet-home"
        dotnet_home.mkdir(parents=True, exist_ok=True)
        env["DOTNET_CLI_HOME"] = str(dotnet_home)
    return env


def _build_project_diagnostics(project_dir: Path) -> Dict[str, Any]:
    """dotnet build 整个项目，返回 {绝对文件路径: [诊断]} 与构建状态。

    Returns:
        {"checked": True, "build_success": bool,
         "diagnostics": {"/abs/File.cs": [{"severity","code","message","line","column"}]}}
        {"checked": False, "reason": "..."}
    """
    if shutil.which("dotnet") is None:
        return {"checked": False, "reason": "未找到 dotnet SDK"}
    csproj_files = sorted(project_dir.glob("*.csproj"))
    if not csproj_files:
        return {"checked": False, "reason": f"项目目录无 *.csproj: {project_dir}"}
    csproj = csproj_files[0]
    try:
        proc = subprocess.run(
            ["dotnet", "build", str(csproj), "--nologo", "-v", "q"],
            capture_output=True,
            text=True,
            timeout=120,
            env=_dotnet_env(),
        )
    except (subprocess.TimeoutExpired, OSError) as exc:
        return {"checked": False, "reason": f"dotnet build 执行失败: {exc}"}

    by_file: Dict[str, List[Dict[str, Any]]] = {}
    for m in _BUILD_ERR_RE.finditer(proc.stdout):
        f, line, col, sev, code, msg = m.groups()
        key = str(Path(f).resolve())
        by_file.setdefault(key, []).append(
            {
                "severity": "error" if sev == "error" else "warning",
                "code": code,
                # 去掉消息尾部附带的 [xxx.csproj] 引用
                "message": re.sub(r"\s*\[[^\]]*\.csproj\]\s*$", "", msg).strip(),
                "line": int(line),
                "column": int(col),
            }
        )
    return {
        "checked": True,
        "build_success": proc.returncode == 0,
        "diagnostics": by_file,
    }


def _run_deep_build(file_path: Path) -> Dict[str, Any]:
    """dotnet build 文件所属项目，提取当前文件的真实语义错误。

    与单文件 RoslynCheck 不同，本项目构建包含完整引用上下文，
    语义错误（类型不匹配/签名错误等）是编译器判定，可靠。

    Returns:
        {"checked": True, "build_success": bool, "diagnostics": [...]}
        {"checked": False, "reason": "..."}
    """
    project_dir = _find_project_dir(file_path)
    if project_dir is None:
        return {"checked": False, "reason": f"未找到 {file_path.name} 所属项目（*.csproj）"}
    result = _build_project_diagnostics(project_dir)
    if not result.get("checked"):
        return result
    return {
        "checked": True,
        "build_success": result["build_success"],
        "diagnostics": result["diagnostics"].get(str(file_path.resolve()), []),
    }


def analyze_file(path: str | Path, deep: bool = False) -> Dict[str, Any]:
    """分析指定文件。文件不存在或不可读时返回带错误标记的报告。

    Args:
        path: 文件路径
        deep: C# 文件是否执行项目级深度检查（dotnet build 语义诊断，
              含完整项目引用上下文；默认仅启发式 + Roslyn 单文件语法检查）
    """
    p = Path(path)
    lang = detect_language(filename=str(p))
    if not p.exists():
        return {
            "filename": str(p),
            "language": lang,
            "ok": False,
            "syntax_error": None,
            "issues": [{"type": "file_not_found", "severity": "高", "message": f"文件不存在: {p}"}],
            "sloc": 0,
            "function_count": 0,
            "avg_function_lines": 0.0,
            "functions": [],
        }
    try:
        source = p.read_text(encoding="utf-8")
    except (UnicodeDecodeError, OSError) as exc:
        return {
            "filename": str(p),
            "language": lang,
            "ok": False,
            "syntax_error": None,
            "issues": [{"type": "read_error", "severity": "高", "message": f"读取失败: {exc}"}],
            "sloc": 0,
            "function_count": 0,
            "avg_function_lines": 0.0,
            "functions": [],
        }
    report = analyze_source(source, filename=str(p))

    # C# 文件：叠加 Roslyn 真实语法检查（仅 analyze_file 路径触发）
    if report.get("language") == "csharp":
        roslyn = _run_roslyn_check(p)
        if roslyn.get("checked"):
            for diag in roslyn["syntax"]:
                if diag.get("severity") == "error":
                    report["issues"].append(
                        {
                            "type": "csharp_syntax",
                            "lineno": diag.get("line"),
                            "severity": "高",
                            "message": f"[Roslyn {diag.get('code')}] {diag.get('message')}",
                        }
                    )
            report["ok"] = not any(i["severity"] == "高" for i in report["issues"])
        report["compile"] = roslyn

        # 深度检查：项目级 dotnet build 语义诊断
        if deep:
            deep_result = _run_deep_build(p)
            if deep_result.get("checked"):
                for diag in deep_result["diagnostics"]:
                    if diag.get("severity") == "error":
                        report["issues"].append(
                            {
                                "type": "csharp_semantic",
                                "lineno": diag.get("line"),
                                "severity": "高",
                                "message": f"[构建 {diag.get('code')}] {diag.get('message')}",
                            }
                        )
                report["ok"] = not any(i["severity"] == "高" for i in report["issues"])
            report["deep"] = deep_result
    return report
