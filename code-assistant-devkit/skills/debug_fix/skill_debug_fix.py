"""技能：调试修复（debug_fix）。

能力：对源码做"语法级 + 静态启发式"诊断，定位根因，并在安全场景下自动应用修复。

诊断维度：
  1. 语法错误（ast.parse）—— 含自动修复：def/class/if/for/while 缺冒号等
  2. 未定义名称（Name 引用未赋值/未导入/非内建）
  3. 未使用导入（import 后从未引用）
  4. 用户提供 error 文本时的根因匹配（NameError/TypeError 等常见模式）

说明：这是"可运行的启发式诊断器"，非完整语义分析；复杂问题给出定位与建议，
     由下游 unit_test 技能回归验证修复效果（通过依赖关系强制衔接）。
"""

from __future__ import annotations

import ast
import builtins
import difflib
import io
import re
from typing import Any, Dict, List

from skills.core.skill_base import BaseSkill, SkillMeta, SkillResult

# 内建名称集合，用于判断"未定义名称"
_BUILTINS = set(dir(builtins)) | {"True", "False", "None"}

# 常见错误模式 -> 根因解释（用于 error 文本匹配）
_ERROR_PATTERNS: List[tuple] = [
    ("NameError", "引用了未定义/未导入的名称，或变量作用域外使用"),
    ("TypeError", "函数参数个数/类型不匹配，或对不支持的类型做了运算"),
    ("IndexError", "下标越界：索引超出序列长度"),
    ("KeyError", "字典/映射中不存在该键"),
    ("AttributeError", "访问了对象不存在的属性，或对象为 None"),
    ("IndentationError", "缩进不一致：请统一使用 4 空格缩进"),
    ("UnboundLocalError", "局部变量在赋值前被引用"),
    ("ZeroDivisionError", "除数为 0"),
    ("ImportError", "导入失败：模块不存在或未安装"),
]


class DebugFixSkill(BaseSkill):
    """调试修复技能。"""

    meta = SkillMeta(
        name="debug_fix",
        display_name="调试修复",
        description="对源码做语法/静态诊断，定位根因并给出（或自动应用）修复",
        module_path="skills.debug_fix.skill_debug_fix",
        class_name="DebugFixSkill",
        scenarios=["用户上报报错信息需要定位根因", "源码存在语法错误或未定义名称", "修复后回归验证"],
        triggers=["报错", "错误", "bug", "修复", "修一下", "异常", "traceback", "崩溃", "failed", "error"],
        inputs={
            "source": "待诊断的源码（必填）",
            "error": "错误信息/traceback（可选）",
            "filename": "文件名（可选）",
        },
        outputs={
            "root_cause": "根因分析",
            "diagnosis": "诊断详情",
            "fixed_code": "修复后的代码",
            "auto_fixed": "是否应用自动修复",
            "issues": "问题列表",
        },
        required_inputs=["source"],
        dependencies=[],                 # 无前置依赖
        post_checks=["unit_test"],       # 修复后必须回归测试（后置校验）
        suggested_next=["unit_test", "doc_writing"],
    )

    # ---------- 静态诊断 ----------
    @staticmethod
    def _diagnose(code: str) -> Dict[str, Any]:
        """返回诊断结果：syntax_error/issues（含 undefined/unused）。"""
        result: Dict[str, Any] = {"syntax_error": None, "issues": []}

        try:
            tree = ast.parse(code)
        except SyntaxError as exc:
            result["syntax_error"] = {
                "lineno": exc.lineno,
                "offset": exc.offset,
                "msg": exc.msg,
                "text": exc.text,
            }
            return result

        # ---- 收集"已定义"名称（粗糙的全局视图：赋值/导入/定义/参数）----
        defined: set[str] = set()
        for node in ast.walk(tree):
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
                defined.add(node.name)
                # 函数参数也算已定义
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
            elif isinstance(node, ast.ExceptHandler) and node.name:
                defined.add(node.name)

        # ---- 未定义名称（含拼写建议：与已定义/内建/已导入名做近似匹配）----
        candidates = defined | _BUILTINS
        seen_undefined: set[str] = set()
        for node in ast.walk(tree):
            if isinstance(node, ast.Name) and isinstance(node.ctx, ast.Load):
                if node.id not in defined and node.id not in _BUILTINS and node.id not in seen_undefined:
                    seen_undefined.add(node.id)
                    suggestions = difflib.get_close_matches(node.id, list(candidates), n=3, cutoff=0.6)
                    suggestion_text = (
                        f"（是否应为 {suggestions[0]!r}？）" if suggestions
                        else "：请先赋值/导入，或检查拼写"
                    )
                    result["issues"].append(
                        {
                            "type": "undefined_name",
                            "lineno": getattr(node, "lineno", None),
                            "name": node.id,
                            "severity": "高",
                            "suggestion": suggestions,
                            "message": f"未定义名称 {node.id!r}{suggestion_text}",
                        }
                    )

        # ---- 未使用导入 ----
        imported: List[tuple] = []
        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                for alias in node.names:
                    imported.append((alias.asname or alias.name.split(".")[0], node.lineno))
            elif isinstance(node, ast.ImportFrom):
                for alias in node.names:
                    imported.append((alias.asname or alias.name, node.lineno))
        used = {n.id for n in ast.walk(tree) if isinstance(n, ast.Name) and isinstance(n.ctx, ast.Load)}
        for name, lineno in imported:
            if name not in used:
                result["issues"].append(
                    {
                        "type": "unused_import",
                        "lineno": lineno,
                        "name": name,
                        "severity": "低",
                        "message": f"导入 {name!r} 未被使用，建议移除",
                    }
                )
        # ---- 可选：pyflakes 增强检测（有则更准，无则回退上面的 AST 启发式）----
        pf_issues = DebugFixSkill._pyflakes_check(code)
        if pf_issues:
            result["issues"] = [
                i for i in result["issues"]
                if i["type"] not in ("undefined_name", "unused_import")
            ]
            result["issues"].extend(pf_issues)
            result["analyzer"] = "pyflakes"
        # 统一为所有 undefined_name 补拼写建议（AST 与 pyflakes 路径一致）
        candidates = defined | _BUILTINS
        for iss in result["issues"]:
            if iss.get("type") == "undefined_name" and "suggestion" not in iss and iss.get("name"):
                iss["suggestion"] = difflib.get_close_matches(iss["name"], list(candidates), n=3, cutoff=0.6)
        return result

    @staticmethod
    def _pyflakes_check(code: str) -> List[dict]:
        """可选：调用 pyflakes 做更准的未定义名/未用导入检测。

        无 pyflakes 时返回 []（技能保持零运行依赖可用）。
        """
        try:
            from pyflakes.api import check as pf_check
            from pyflakes.reporter import Reporter
        except ImportError:
            return []
        buf = io.StringIO()
        pf_check(code, "<source>", Reporter(buf, buf))
        issues: List[dict] = []
        for line in buf.getvalue().splitlines():
            m = re.match(r"<source>:(\d+)(?::\d+)?: (.+)", line)
            if not m:
                continue
            lineno, msg = int(m.group(1)), m.group(2)
            name_m = re.search(r"'([^']+)'", msg)
            if "undefined name" in msg:
                issues.append(
                    {
                        "type": "undefined_name",
                        "lineno": lineno,
                        "name": name_m.group(1) if name_m else None,
                        "severity": "高",
                        "message": f"未定义名称（pyflakes）: {msg}",
                    }
                )
            elif "imported but unused" in msg:
                issues.append(
                    {
                        "type": "unused_import",
                        "lineno": lineno,
                        "name": name_m.group(1) if name_m else None,
                        "severity": "低",
                        "message": f"导入未使用（pyflakes）: {msg}",
                    }
                )
        return issues

    # ---------- 自动修复 ----------
    @staticmethod
    def _auto_fix(code: str, syntax_error: Dict[str, Any]) -> tuple[str, bool, str]:
        """针对可安全自动修复的语法错误应用修复。

        支持场景（P0-1 扩展）：
          1. def/class/if/for/while/with/try/except/finally 缺冒号 → 补冒号
          2. Tab/空格混用导致的 IndentationError/TabError → 统一为 4 空格
          3. 括号未闭合 → 仅定位建议（不自动改，避免破坏结构），由 run() 转为 issue
        返回 (修复后代码, 是否修复, 说明)。
        """
        lines = code.splitlines(keepends=True)
        lineno = syntax_error.get("lineno")
        msg = (syntax_error.get("msg") or "").lower()

        if lineno is None or lineno < 1 or lineno > len(lines):
            return code, False, ""
        line = lines[lineno - 1]
        stripped = line.strip()

        # 场景 1：语句头缺冒号（"expected ':'"）
        if "expected ':'" in msg:
            heads = ("def ", "class ", "if ", "elif ", "else", "for ", "while ", "with ",
                     "try", "except", "finally")
            if any(stripped.startswith(h) for h in heads) and not stripped.endswith(":"):
                lines[lineno - 1] = line.rstrip("\n") + ":\n"
                return "".join(lines), True, f"第 {lineno} 行语句头缺少冒号，已自动补上"

        # 场景 2：缩进不一致（TabError / IndentationError）→ tab 统一为 4 空格
        if ("indent" in msg or "tab" in msg or "inconsistent use" in msg) and "\t" in code:
            fixed = code.replace("\t", "    ")
            try:
                ast.parse(fixed)
                return fixed, True, "检测到 Tab/空格混用，已统一为 4 空格缩进"
            except SyntaxError:
                pass  # 统一后仍报错 → 非单纯缩进问题，交由后续诊断

        # 场景 3：括号未闭合（"'(' was never closed" 等）→ 不自动改，返回标记由 run 转为建议 issue
        return code, False, ""

    # ---------- 执行入口 ----------
    def run(self, context: Dict[str, Any]) -> SkillResult:
        code = context["source"]
        filename = context.get("filename") or "<内存源码>"
        issues: List[dict] = []
        root_cause: List[str] = []
        fixed_code = code
        auto_fixed = False

        # 1) 语法 + 静态诊断
        diag = self._diagnose(code)
        if diag["syntax_error"]:
            se = diag["syntax_error"]
            root_cause.append(f"[语法错误] 第 {se['lineno']} 行: {se['msg']}")
            issues.append(
                {
                    "type": "syntax_error",
                    "lineno": se["lineno"],
                    "severity": "高",
                    "message": f"语法错误: {se['msg']}（行 {se['lineno']}: {se['text'] or ''}）",
                }
            )
            # 尝试自动修复
            fixed_code, auto_fixed, fix_note = self._auto_fix(code, se)
            if auto_fixed:
                root_cause.append(f"已自动修复：{fix_note}")
            else:
                # 未自动修复时，对括号不配对类错误补充定位建议
                msg_lower = (se.get("msg") or "").lower()
                if "never closed" in msg_lower or "unmatched" in msg_lower:
                    issues.append(
                        {
                            "type": "unbalanced_bracket",
                            "lineno": se["lineno"],
                            "severity": "高",
                            "message": f"第 {se['lineno']} 行括号不配对：{se['msg']}；请补全对应闭合符",
                        }
                    )
                    root_cause.append(f"[括号不配对] 第 {se['lineno']} 行: {se['msg']}（建议补全闭合符）")
        else:
            root_cause.append("语法检查通过，无语法错误")
            fixed_code = code  # 语法 OK 则原文即是修复基线

        issues.extend(diag["issues"])
        for issue in diag["issues"]:
            if issue["type"] == "undefined_name":
                root_cause.append(f"[未定义名称] 第 {issue['lineno']} 行: {issue['message']}")

        # 2) 用户提供错误文本时的根因匹配
        error_text = context.get("error") or ""
        matched = []
        if error_text:
            for pattern, explanation in _ERROR_PATTERNS:
                if pattern.lower() in error_text.lower():
                    matched.append(f"{pattern}: {explanation}")
            if matched:
                root_cause.append("根据用户提供的错误信息：" + "；".join(matched))

        diagnosis = {
            "filename": filename,
            "issues": issues,
            "error_patterns": matched,
            "auto_fixed": auto_fixed,
        }
        return SkillResult(
            success=True,
            data={
                "root_cause": root_cause,
                "diagnosis": diagnosis,
                "fixed_code": fixed_code,
                "auto_fixed": auto_fixed,
                "issues": issues,
                "summary": ("已自动修复" if auto_fixed else "未自动修改代码")
                + f"，发现 {len(issues)} 个问题",
            },
        )
