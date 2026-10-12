"""技能基类：定义所有技能的统一契约。

每个技能由两部分组成：
  1. meta（SkillMeta）—— 声明式元数据：能力描述、适用场景、触发条件、
     输入输出格式、调用关系（dependencies）。这是"能力清单"的运行时载体，
     与 skills/registry.json 一一对应。
  2. run(context) —— 实际执行逻辑：接收统一上下文 dict，返回 SkillResult。

统一入口 __call__ 负责：输入校验 → 计时执行 → 结果包装，
保证所有技能的错误行为一致（失败也返回 SkillResult，绝不抛裸异常）。
"""

from __future__ import annotations

import time
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from typing import TYPE_CHECKING, Any, Dict, List, Optional

if TYPE_CHECKING:  # 仅用于类型标注，避免运行时循环导入
    from .registry import SkillRegistry


@dataclass
class SkillMeta:
    """技能的声明式元数据（与 registry.json 字段对应）。"""

    name: str                      # 技能唯一标识（英文小写）
    display_name: str              # 中文显示名
    description: str               # 一句话能力描述
    module_path: str               # 技能实现模块（可导入路径）
    class_name: str                # 技能实现类名
    scenarios: List[str] = field(default_factory=list)       # 适用场景
    triggers: List[str] = field(default_factory=list)        # 触发条件（关键词/短语）
    inputs: Dict[str, str] = field(default_factory=dict)     # 输入参数说明 {key: 说明}
    outputs: Dict[str, str] = field(default_factory=dict)    # 输出字段说明 {key: 说明}
    required_inputs: List[str] = field(default_factory=list) # 必填输入参数
    dependencies: List[str] = field(default_factory=list)    # 调用关系（前置）：执行前须先运行的其他技能
    post_checks: List[str] = field(default_factory=list)     # 调用关系（后置）：本技能执行后必须衔接的校验技能
    suggested_next: List[str] = field(default_factory=list)  # 建议后续衔接技能（非强制）
    version: str = "1.0.0"         # 技能版本


class SkillResult:
    """技能执行结果：成功/失败、数据、错误信息、耗时。"""

    def __init__(
        self,
        success: bool,
        data: Optional[Dict[str, Any]] = None,
        error: Optional[str] = None,
        elapsed_ms: Optional[float] = None,
    ) -> None:
        self.success = success
        self.data = data or {}
        self.error = error
        self.elapsed_ms = elapsed_ms

    def to_dict(self) -> Dict[str, Any]:
        """转字典，便于序列化/落日志/拼接下游上下文。"""
        return {
            "success": self.success,
            "data": self.data,
            "error": self.error,
            "elapsed_ms": round(self.elapsed_ms, 1) if self.elapsed_ms is not None else None,
        }


class BaseSkill(ABC):
    """所有技能的抽象基类。子类只需实现 run()，并设置 meta。"""

    meta: SkillMeta

    def __init__(self, registry: Optional["SkillRegistry"] = None) -> None:
        # registry 注入：技能间调用（如 debug_fix 修复后调用 unit_test 回归）依赖它
        self.registry = registry

    # ---- 公共入口 ----
    def __call__(self, context: Dict[str, Any]) -> SkillResult:
        """统一执行入口：校验输入 → 执行 → 包装结果（含耗时）。"""
        start = time.perf_counter()
        try:
            self.validate_input(context)
            result = self.run(context)
        except Exception as exc:  # noqa: BLE001 —— 技能失败统一转为 SkillResult
            result = SkillResult(success=False, error=f"[{self.meta.name}] 执行异常: {exc}")
        result.elapsed_ms = (time.perf_counter() - start) * 1000
        return result

    def validate_input(self, context: Dict[str, Any]) -> None:
        """输入校验：检查必填参数是否存在。子类可覆写做更细校验。"""
        missing = [k for k in self.meta.required_inputs if not context.get(k)]
        if missing:
            raise ValueError(f"缺少必填参数: {', '.join(missing)}")

    # ---- 子类实现 ----
    @abstractmethod
    def run(self, context: Dict[str, Any]) -> SkillResult:
        """执行技能逻辑，返回 SkillResult。"""
        raise NotImplementedError
