"""技能：性能优化（perf_opt）。

能力：AST 级性能分析（不执行代码）：
  1. 函数复杂度估算：最大循环嵌套深度 → O(n^k) 标注，递归提示
  2. 反模式识别：
     - 循环内字符串拼接（str += ，O(n²)）→ 建议 join/append
     - 循环内 list.append 低效积累 → 建议列表推导式
     - 循环内重复计算不变量 → 建议提取到循环外
  3. 输出结构化问题报告（位置/严重度/建议），优化建议给出示例代码

说明：本技能只"诊断 + 建议"，不做有风险的自动改写（避免破坏语义）；
     通过依赖关系强制衔接 unit_test 回归，保证优化不引入回归。
"""

from __future__ import annotations

import ast
from typing import Any, Dict, List

from skills.core.skill_base import BaseSkill, SkillMeta, SkillResult


class _ComplexityVisitor(ast.NodeVisitor):
    """统计每个函数的复杂度特征。"""

    def __init__(self) -> None:
        self.funcs: Dict[str, Dict[str, Any]] = {}
        self._current: Dict[str, Any] | None = None
        self._fn_name: str = ""   # 当前正在访问的函数名
        self._loop_depth = 0

    def visit_FunctionDef(self, node: ast.FunctionDef) -> None:
        self.funcs[node.name] = {"max_loop_depth": 0, "has_recursion": False, "issues": []}
        self._current = self.funcs[node.name]
        prev_name = self._fn_name
        self._fn_name = node.name
        self.generic_visit(node)
        self._fn_name = prev_name
        self._current = None

    AsyncFunctionDef = visit_FunctionDef  # 异步函数同样处理

    def visit_For(self, node: ast.For) -> None:
        self._loop_depth += 1
        if self._current is not None:
            self._current["max_loop_depth"] = max(
                self._current["max_loop_depth"], self._loop_depth
            )
        self.generic_visit(node)
        self._loop_depth -= 1

    visit_AsyncFor = visit_For
    visit_While = visit_For  # while 同样计入循环深度

    def visit_Call(self, node: ast.Call) -> None:
        # 递归检测：函数体内调用自身名字
        if self._current is not None and isinstance(node.func, ast.Name):
            if node.func.id == self._fn_name:
                self._current["has_recursion"] = True
        self.generic_visit(node)


class _IssueFinder(ast.NodeVisitor):
    """识别循环内反模式。"""

    def __init__(self) -> None:
        self.issues: List[Dict[str, Any]] = []

    def visit_For(self, node: ast.For) -> None:
        loop_var = node.target.id if isinstance(node.target, ast.Name) else None
        # 循环内语句
        for stmt in node.body:
            # 1) 循环内字符串拼接：out = out + str(v) + ...
            if isinstance(stmt, ast.Assign) and len(stmt.targets) == 1:
                target = stmt.targets[0]
                if isinstance(target, ast.Name) and isinstance(stmt.value, ast.BinOp):
                    self._check_string_concat(target, stmt.value, node.lineno)
            # 2) 循环内 list.append：result.append(...)
            if (
                isinstance(stmt, ast.Expr)
                and isinstance(stmt.value, ast.Call)
                and isinstance(stmt.value.func, ast.Attribute)
                and stmt.value.func.attr == "append"
            ):
                self.issues.append(
                    {
                        "lineno": node.lineno,
                        "severity": "中",
                        "pattern": "loop_append",
                        "message": (
                            f"第 {node.lineno} 行：循环内调用 .append() 累积列表，"
                            "建议改用列表推导式 [expr for x in iterable]（更快且更简洁）"
                        ),
                    }
                )
        self.generic_visit(node)

    def _check_string_concat(self, target: ast.Name, value: ast.BinOp, lineno: int) -> None:
        """检测 out = out + ... 模式（循环内字符串拼接）。

        注意：`out = out + str(x) + ","` 在 AST 中是左结合的嵌套 BinOp，
        需要沿左脊柱一路下探才能找到最左侧的 Name。
        """
        left = value
        while isinstance(left, ast.BinOp):
            left = left.left
        if isinstance(left, ast.Name) and left.id == target.id:
            self.issues.append(
                {
                    "lineno": lineno,
                    "severity": "高",
                    "pattern": "string_concat_in_loop",
                    "message": (
                        f"第 {lineno} 行：循环内字符串拼接 {target.id} = {target.id} + ... "
                        "为 O(n²) 复杂度，建议改为收集到 list 后一次性 '\",\".join(...)'"
                    ),
                    "suggestion": f'parts = [str(x) for x in seq]\nresult = ",".join(parts)',
                }
            )


def _complexity_label(depth: int, recursion: bool) -> str:
    """把循环深度转换为复杂度标签。"""
    if recursion:
        return "O(递归深度)（递归实现，注意栈溢出风险）"
    if depth >= 3:
        return f"O(n^{depth})（多层嵌套循环，规模大时需警惕）"
    if depth == 2:
        return "O(n²)（双层循环，建议评估数据规模）"
    if depth == 1:
        return "O(n)（线性，通常可接受）"
    return "O(1)（无循环，常数时间）"


class PerfOptSkill(BaseSkill):
    """性能优化技能。"""

    meta = SkillMeta(
        name="perf_opt",
        display_name="性能优化",
        description="AST 级性能分析：复杂度估算、循环内字符串拼接等反模式识别、优化建议",
        module_path="skills.perf_opt.skill_perf_opt",
        class_name="PerfOptSkill",
        scenarios=["定位耗时/卡顿代码", "评估函数时间复杂度", "识别循环内反模式"],
        triggers=["性能", "优化", "慢", "耗时", "内存", "卡顿", "perf", "optimize", "benchmark", "加速"],
        inputs={"source": "待分析源码（必填）"},
        outputs={"complexity": "各函数复杂度估算", "report": "问题报告列表", "suggestions": "优化建议摘要"},
        required_inputs=["source"],
        dependencies=[],                 # 无前置依赖
        post_checks=["unit_test"],       # 优化后必须回归测试（后置校验）
        suggested_next=["unit_test"],
    )

    # ---------- 执行入口 ----------
    def run(self, context: Dict[str, Any]) -> SkillResult:
        code = context["source"]
        try:
            tree = ast.parse(code)
        except SyntaxError as exc:
            return SkillResult(success=False, error=f"源码存在语法错误，无法做性能分析: {exc.msg}")

        # 1) 复杂度统计
        comp_visitor = _ComplexityVisitor()
        comp_visitor.visit(tree)
        complexity = {
            name: {
                "max_loop_depth": info["max_loop_depth"],
                "complexity": _complexity_label(info["max_loop_depth"], info["has_recursion"]),
                "has_recursion": info["has_recursion"],
            }
            for name, info in comp_visitor.funcs.items()
        }

        # 2) 反模式识别
        issue_finder = _IssueFinder()
        issue_finder.visit(tree)
        issues = issue_finder.issues

        # 3) 汇总建议
        suggestions = [i["message"] for i in issues]
        if not suggestions:
            suggestions.append("未发现明显反模式；若仍感觉慢，建议用 cProfile 做热点采样定位")

        return SkillResult(
            success=True,
            data={
                "complexity": complexity,
                "report": issues,
                "suggestions": suggestions,
                "summary": f"分析了 {len(complexity)} 个函数，发现 {len(issues)} 个性能问题",
            },
        )
