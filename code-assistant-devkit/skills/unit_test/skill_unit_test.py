"""技能：单元测试（unit_test）。

能力：从源码自动抽取函数签名，生成 unittest 测试用例并实际执行，返回通过/失败统计。

用例生成策略：
  1. 冒烟测试 —— 对每个函数用类型推断的默认参数调用一次，验证不抛异常
  2. 显式用例 —— 调用方通过 context["cases"] 提供 {function, args, expected}
生成测试文件后以子进程方式运行（隔离 + 可观测），解析 unittest 输出统计。

依赖：context["source"] 必填。
"""

from __future__ import annotations

import ast
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

from devkit_common.settings import get_settings
from skills.core.skill_base import BaseSkill, SkillMeta, SkillResult

# 类型注解 -> 冒烟测试默认参数
_DEFAULT_ARGS: Dict[str, Any] = {
    "int": 0,
    "float": 0.0,
    "str": "",
    "bool": False,
    "list": [],
    "dict": {},
    "tuple": (),
    "set": set(),
    "None": None,
    "Any": 0,
}


class UnitTestSkill(BaseSkill):
    """单元测试技能。"""

    meta = SkillMeta(
        name="unit_test",
        display_name="单元测试",
        description="自动生成单元测试（unittest 框架）并执行，返回通过/失败统计",
        module_path="skills.unit_test.skill_unit_test",
        class_name="UnitTestSkill",
        scenarios=["验证新生成代码的正确性", "修复/优化后的回归测试", "为已有函数补充测试用例"],
        triggers=["测试", "单测", "单元测试", "验证", "用例", "回归", "test", "unittest", "冒烟"],
        inputs={
            "source": "被测源码（必填）",
            "cases": "示例用例（可选）[{'function': 'add', 'args': [1, 2], 'expected': 3}, ...]",
            "framework": "测试框架，默认 unittest",
        },
        outputs={"test_code": "生成的测试代码", "passed": "通过用例数", "failed": "失败用例数", "output": "执行输出摘要"},
        required_inputs=["source"],
        dependencies=[],
        suggested_next=["doc_writing"],
    )

    # ---------- 辅助 ----------
    @staticmethod
    def _annotation_name(annotation: Optional[ast.AST]) -> str:
        """把类型注解 AST 转为名字字符串（无注解时返回 Any）。"""
        if annotation is None:
            return "Any"
        return ast.unparse(annotation)

    @staticmethod
    def _extract_functions(code: str) -> List[Dict[str, Any]]:
        """抽取函数签名：name、参数（含默认值）、返回注解。"""
        funcs = []
        try:
            tree = ast.parse(code)
        except SyntaxError:
            return funcs  # 语法错误时无法生成测试，返回空
        for node in ast.walk(tree):
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
                # 统计本函数内被"迭代/求长"的参数（for x in 参数 / len(参数)），
                # 冒烟测试据此推断 [] 而非 0，避免对可迭代参数传错类型
                iterated: set[str] = set()
                for inner in ast.walk(node):
                    if isinstance(inner, (ast.For, ast.AsyncFor)) and isinstance(inner.iter, ast.Name):
                        iterated.add(inner.iter.id)
                    elif (
                        isinstance(inner, ast.Call)
                        and isinstance(inner.func, ast.Name)
                        and inner.func.id == "len"
                        and inner.args
                        and isinstance(inner.args[0], ast.Name)
                    ):
                        iterated.add(inner.args[0].id)

                params = []
                for arg in node.args.args:
                    default = None
                    idx = len(params)
                    # 默认值从右往左与参数对应
                    if len(node.args.defaults) > 0:
                        defaults = node.args.defaults
                        if idx >= len(node.args.args) - len(defaults):
                            default = ast.unparse(defaults[idx - (len(node.args.args) - len(defaults))])
                    params.append(
                        {
                            "name": arg.arg,
                            "annotation": UnitTestSkill._annotation_name(arg.annotation),
                            "default": default,
                            "iterated": arg.arg in iterated,
                        }
                    )
                funcs.append(
                    {
                        "name": node.name,
                        "params": params,
                        "returns": UnitTestSkill._annotation_name(node.returns),
                    }
                )
        return funcs

    @staticmethod
    def _smoke_args(params: List[Dict[str, Any]]) -> List[str]:
        """为冒烟测试生成参数表达式：默认值 > 可迭代推断 [] > 类型注解推断。"""
        args = []
        for p in params:
            if p.get("default") is not None:
                args.append(p["default"])
            elif p.get("iterated"):
                args.append("[]")  # 参数在函数体内被迭代，用空列表最安全
            else:
                args.append(repr(_DEFAULT_ARGS.get(p.get("annotation") or "Any", 0)))
        return args

    @staticmethod
    def _boundary_variants(func: Dict[str, Any]) -> List[str]:
        """为含数值参数的函数生成边界变体参数表达式（覆盖 0/正/负 分支）。

        仅当函数有数值型（int/float/无注解）参数时生成 3 个变体；否则返回 []。
        """
        params = func["params"]
        numeric_idx = [
            i for i, p in enumerate(params)
            if (p.get("annotation") or "Any") in ("int", "float", "Any")
            and not p.get("iterated")  # 被迭代的参数（如 list）不生成数值边界
        ]
        if not numeric_idx:
            return []
        variants: List[str] = []
        for fill in (0, 1, -1):
            args = []
            for i, p in enumerate(params):
                if i in numeric_idx:
                    args.append(str(fill))
                elif p.get("default") is not None:
                    args.append(p["default"])
                else:
                    args.append(repr(_DEFAULT_ARGS.get(p.get("annotation") or "Any", 0)))
            variants.append(", ".join(args))
        return variants

    # ---------- 测试生成与执行 ----------
    def _build_test_code(
        self, code: str, funcs: List[Dict[str, Any]], cases: List[Dict[str, Any]]
    ) -> str:
        """生成 unittest 测试文件源码。"""
        body: List[str] = []
        # 1) 冒烟测试：每个函数调用一次
        for f in funcs:
            args = ", ".join(self._smoke_args(f["params"]))
            body.append(
                f"    def test_smoke_{f['name']}(self):\n"
                f"        # 冒烟测试：默认参数调用，若抛异常则用例失败\n"
                f"        {f['name']}({args})\n"
            )
        # 1.5) 边界变体用例：数值参数 0/正/负，增加分支触达
        for f in funcs:
            for i, bargs in enumerate(self._boundary_variants(f), start=1):
                body.append(
                    f"    def test_boundary_{f['name']}_{i}(self):\n"
                    f"        # 边界用例：{f['name']}({bargs}) 不抛异常\n"
                    f"        {f['name']}({bargs})\n"
                )
        # 2) 显式用例：断言返回值
        for i, case in enumerate(cases, start=1):
            fn = case["function"]
            args = ", ".join(repr(a) for a in case.get("args", []))
            expected = repr(case["expected"])
            body.append(
                f"    def test_case_{fn}_{i}(self):\n"
                f"        # 显式用例：{fn}({args}) 应等于 {expected}\n"
                f"        self.assertEqual({fn}({args}), {expected})\n"
            )
        test_methods = "\n".join(body)
        return f'''"""自动生成的单元测试（由 unit_test 技能产出，请勿手改头部注释）。"""
import unittest

# ===== 被测源码（原样内嵌，保证测试自包含） =====
{code}

# ===== 测试用例 =====
class TestGenerated(unittest.TestCase):
{test_methods}

if __name__ == "__main__":
    unittest.main(verbosity=2)
'''

    @staticmethod
    def _parse_result(output: str) -> Tuple[int, int]:
        """从 unittest 输出解析 (通过数, 失败数)。"""
        passed = failed = 0
        for line in output.splitlines():
            m = re.search(r"ok\s*$", line)
            if m:
                passed += 1
            if "FAIL:" in line or "ERROR:" in line:
                failed += 1
        # 兜底：无逐条输出时按最终汇总统计
        if passed == 0 and failed == 0:
            m_ok = re.search(r"Ran (\d+) tests?.*?OK", output, re.S)
            m_fail = re.search(r"Ran (\d+) tests?.*?FAILED", output, re.S)
            if m_ok:
                passed = int(m_ok.group(1))
            elif m_fail:
                failed = 1
        return passed, failed

    @staticmethod
    def _has_coverage() -> bool:
        """是否安装了 coverage（可选增强；无则覆盖率字段为 None）。"""
        try:
            import coverage  # noqa: F401
            return True
        except ImportError:
            return False

    @staticmethod
    def _parse_coverage(report_text: str) -> Optional[int]:
        """从 `coverage report` 文本解析 TOTAL 行的覆盖率百分比。"""
        for line in report_text.splitlines():
            if line.strip().startswith("TOTAL"):
                m = re.search(r"(\d+)%", line)
                return int(m.group(1)) if m else None
        return None

    # ---------- 执行入口 ----------
    def run(self, context: Dict[str, Any]) -> SkillResult:
        code = context["source"]
        cases: List[Dict[str, Any]] = context.get("cases") or []
        funcs = self._extract_functions(code)

        if not funcs:
            return SkillResult(success=False, error="未从源码中解析到任何函数定义，无法生成测试")

        test_code = self._build_test_code(code, funcs, cases)

        # 写入临时文件并以子进程执行（隔离环境，输出可解析）
        with tempfile.NamedTemporaryFile("w", suffix=".py", encoding="utf-8", delete=False) as f:
            f.write(test_code)
            tmp_path = f.name
        coverage_pct: Optional[int] = None
        try:
            if self._has_coverage():
                # 有 coverage：用 coverage run --branch 跑，再 report 取覆盖率
                cov_cwd = str(get_settings().tmp_dir)
                os.makedirs(cov_cwd, exist_ok=True)
                proc = subprocess.run(
                    [sys.executable, "-m", "coverage", "run", "--branch", tmp_path],
                    capture_output=True, text=True, timeout=60, cwd=cov_cwd,
                )
                output = proc.stdout + proc.stderr
                passed, failed = self._parse_result(output)
                cov_proc = subprocess.run(
                    [sys.executable, "-m", "coverage", "report"],
                    capture_output=True, text=True, timeout=30, cwd=cov_cwd,
                )
                coverage_pct = self._parse_coverage(cov_proc.stdout + cov_proc.stderr)
                (Path(cov_cwd) / ".coverage").unlink(missing_ok=True)
            else:
                proc = subprocess.run(
                    [sys.executable, tmp_path],
                    capture_output=True, text=True, timeout=60,
                )
                output = proc.stdout + proc.stderr
                passed, failed = self._parse_result(output)
        except subprocess.TimeoutExpired:
            Path(tmp_path).unlink(missing_ok=True)
            return SkillResult(success=False, error="测试执行超时（60s）")
        finally:
            Path(tmp_path).unlink(missing_ok=True)

        ok = proc.returncode == 0
        cov_text = f"，行覆盖率 {coverage_pct}%" if coverage_pct is not None else ""
        return SkillResult(
            success=ok,
            data={
                "test_code": test_code,
                "passed": passed,
                "failed": failed,
                "coverage_pct": coverage_pct,
                "output": output[-1500:],
                "summary": f"生成并执行 {passed + failed} 条用例，通过 {passed} / 失败 {failed}{cov_text}",
            },
            error=None if ok else "存在失败的测试用例，请检查被测代码",
        )
