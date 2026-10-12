"""skills —— 代码助手技能模块。

包含：
  - registry.json：能力清单（适用场景 / 输入输出 / 触发条件 / 调用关系）
  - core/：技能基类、注册表加载器、编排器
  - codegen/ debug_fix/ unit_test/ doc_writing/ perf_opt/：五个具体技能实现
"""

from .core.skill_base import BaseSkill, SkillMeta, SkillResult
from .core.registry import SkillRegistry
from .core.orchestrator import SkillOrchestrator

__all__ = ["BaseSkill", "SkillMeta", "SkillResult", "SkillRegistry", "SkillOrchestrator"]
