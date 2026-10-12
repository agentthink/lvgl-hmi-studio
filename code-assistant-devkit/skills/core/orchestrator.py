"""技能编排器：把用户的自然语言诉求转换为可执行的技能调用链。

工作流程：
  1. 意图检测 —— 用触发条件匹配用户文本，得到候选技能及得分
  2. 链路规划 —— 主技能 + 其他命中技能 + 依赖补全（拓扑去重）
  3. 顺序执行 —— 前一个技能的输出数据合并进 context，作为下游输入；
     任一技能失败即中止（fail-fast），保证修复/优化类技能必有回归测试兜底
"""

from __future__ import annotations

import time
from dataclasses import dataclass, field
from typing import Any, Dict, List

from .registry import SkillRegistry
from .skill_base import SkillResult


@dataclass
class OrchestrationReport:
    """一次编排的完整记录：请求文本、执行链、每步结果。"""

    request: str
    chain: List[str] = field(default_factory=list)
    results: Dict[str, Dict[str, Any]] = field(default_factory=dict)
    duration_ms: float = 0.0
    error: str | None = None
    warning: str | None = None   # P0-3：低置信度时的澄清建议（非阻断）

    @property
    def ok(self) -> bool:
        """整条链路是否全部成功。"""
        return all(r.get("success") for r in self.results.values())

    def to_markdown(self) -> str:
        """渲染为 Markdown 报告（供日志/文档/终端展示）。"""
        lines = [
            f"## 技能编排报告",
            "",
            f"- 请求文本：`{self.request}`",
            f"- 执行链：{' → '.join(self.chain) or '（无命中）'}",
            f"- 总耗时：{self.duration_ms:.1f} ms",
            f"- 结果：{'✅ 全部成功' if self.ok else '❌ 存在失败'}",
            "",
            "| 步骤 | 技能 | 成功 | 耗时(ms) | 摘要 |",
            "| --- | --- | --- | --- | --- |",
        ]
        for name in self.chain:
            r = self.results.get(name, {})
            data = r.get("data") or {}
            summary = str(data.get("summary") or data.get("message") or "")
            lines.append(
                f"| {self.chain.index(name) + 1} | `{name}` | "
                f"{'✅' if r.get('success') else '❌'} | {r.get('elapsed_ms', '-')} | {summary[:60]} |"
            )
        if self.error:
            lines.append("")
            lines.append(f"> 编排级错误：{self.error}")
        if self.warning:
            lines.append("")
            lines.append(f"> 意图澄清建议：{self.warning}")
        return "\n".join(lines)


class SkillOrchestrator:
    """技能编排器。"""

    def __init__(self, registry: SkillRegistry | None = None) -> None:
        self.registry = registry or SkillRegistry()

    # ---------- 规划 ----------
    def detect_skills(self, text: str) -> List[str]:
        """意图检测：返回按相关性排序的技能名列表。"""
        return [name for name, _ in self.registry.match_triggers(text)]

    def plan_chain(self, text: str, explicit: List[str] | None = None) -> List[str]:
        """链路规划：主技能 + 显式指定 + 依赖补全（拓扑顺序，去重）。

        Args:
            text: 用户诉求文本（用于触发条件匹配）
            explicit: 调用方显式指定的技能（可选，优先级最高）

        Returns:
            按执行顺序排列的技能名列表。
        """
        detected = self.detect_skills(text)
        ordered: List[str] = []

        def append(name: str) -> None:
            """按依赖深度优先补全（先前置依赖后本体），并追加后置校验。"""
            if name in ordered:
                return
            meta = self.registry.get_meta(name)
            if meta is None:
                return
            for dep in meta.dependencies:          # 前置依赖先入链
                append(dep)
            ordered.append(name)
            for check in meta.post_checks:         # 后置校验（如修复后必须回归测试）
                append(check)

        # 1) 显式指定优先，其次触发命中（保持相关性排序）
        for name in list(explicit or []) + detected:
            append(name)
        return ordered

    # ---------- 执行 ----------
    def execute(
        self,
        request: str,
        context: Dict[str, Any] | None = None,
        explicit: List[str] | None = None,
    ) -> OrchestrationReport:
        """执行一次编排：规划链路并依次运行，产出结构化报告。

        Args:
            request: 用户诉求文本（既是意图来源，也是报告记录）
            context: 初始上下文（如 source 源码、cases 用例等）
            explicit: 显式指定技能

        Returns:
            OrchestrationReport：含执行链与逐步结果。
        """
        start = time.perf_counter()
        chain = self.plan_chain(request, explicit)
        report = OrchestrationReport(request=request, chain=chain)

        # P0-3：意图置信度评估——最高分 ≤1 且有多个候选时提示澄清（非阻断）
        scored = self.registry.match_triggers(request)
        top_score = scored[0][1] if scored else 0
        if chain and top_score <= 1 and len(chain) >= 2:
            report.warning = (
                f"意图置信度较低（最高得分 {top_score}），候选：{', '.join(chain[:3])}；"
                '建议明确诉求，如「修复并回归测试」而非泛指。'
            )

        if not chain:
            report.error = "未命中任何技能，请检查触发条件或显式指定技能"
            report.duration_ms = (time.perf_counter() - start) * 1000
            return report

        # 深拷贝上下文，避免污染调用方
        merged: Dict[str, Any] = dict(context or {})
        # 用户诉求文本注入技能上下文（技能可用 context["request"] 作为输入，
        # 如 codegen；调用方显式提供的 request 优先）
        if request and "request" not in merged:
            merged["request"] = request
        for name in chain:
            skill = self.registry.get(name)
            result: SkillResult = skill(merged)
            report.results[name] = result.to_dict()
            if result.success:
                # 技能输出合并进上下文，供下游技能消费（契约式数据传递）
                merged.update(result.data)
                # 特殊约定：debug_fix 产出的 fixed_code 回填 source，
                # 使后续 unit_test/doc_writing/perf_opt 自动基于"修复后"代码工作
                if result.data.get("fixed_code"):
                    merged["source"] = result.data["fixed_code"]
            else:
                # 失败即停：修复/优化类链路必须保证回归测试通过才继续
                report.error = f"技能 {name} 执行失败: {result.error}"
                break
        report.duration_ms = (time.perf_counter() - start) * 1000
        return report
