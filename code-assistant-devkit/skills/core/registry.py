"""技能注册表：加载 skills/registry.json（能力清单），动态实例化技能类。

职责：
  - 读取能力清单并校验字段
  - 按 name 获取技能实例（惰性加载 + 缓存）
  - 触发条件匹配：给定一段用户诉求文本，返回命中的技能及得分
  - 输出能力清单 Markdown 表格（供文档/对话展示）
"""

from __future__ import annotations

import importlib
import json
import re
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

from .skill_base import BaseSkill, SkillMeta

# 能力清单文件路径（相对本文件：../../registry.json，即 skills/registry.json）
_REGISTRY_PATH = Path(__file__).resolve().parents[1] / "registry.json"


class SkillRegistry:
    """技能注册表。线程安全级别：进程内只读共享。"""

    def __init__(self, registry_path: Optional[Path] = None) -> None:
        self._path = Path(registry_path) if registry_path else _REGISTRY_PATH
        self._metas: List[SkillMeta] = []
        self._skills: Dict[str, BaseSkill] = {}   # name -> 技能实例缓存
        self._load_metas()

    # ---------- 加载 ----------
    def _load_metas(self) -> None:
        """从 registry.json 解析技能元数据。"""
        if not self._path.exists():
            raise FileNotFoundError(f"能力清单不存在: {self._path}")
        raw = json.loads(self._path.read_text(encoding="utf-8"))
        for item in raw.get("skills", []):
            self._metas.append(SkillMeta(**item))

    # ---------- 查询 ----------
    @property
    def metas(self) -> List[SkillMeta]:
        """全部技能元数据（能力清单）。"""
        return list(self._metas)

    def get_meta(self, name: str) -> Optional[SkillMeta]:
        """按 name 查询元数据。"""
        for m in self._metas:
            if m.name == name:
                return m
        return None

    def get(self, name: str) -> BaseSkill:
        """按 name 获取技能实例（惰性加载并缓存）。"""
        if name not in self._skills:
            meta = self.get_meta(name)
            if meta is None:
                raise KeyError(f"技能不存在: {name}")
            # 动态导入技能模块并实例化（类名与技能名驼峰对应，见各技能文件）
            module = importlib.import_module(meta.module_path)
            cls = getattr(module, meta.class_name)
            self._skills[name] = cls(registry=self)
        return self._skills[name]

    def all(self) -> List[BaseSkill]:
        """返回全部技能实例。"""
        return [self.get(m.name) for m in self._metas]

    # ---------- 触发条件匹配 ----------
    # 否定词：紧邻触发词时降低该技能得分（"不要测试" 不应命中 unit_test）
    _NEGATIONS = ("不", "不要", "别", "无需", "不用", "没有", "no ", "not ")

    def match_triggers(self, text: str) -> List[Tuple[str, int]]:
        """根据用户诉求文本匹配技能，返回 [(技能名, 得分)] 按得分降序。

        匹配规则（P0-3 升级：分词 + 权重 + 否定）：
          - 基础分：触发词作为子串命中 +1
          - 词边界加权：英文触发词以 \\b 词边界命中再 +1（避免 implement 误命中 important）
          - 否定扣分：文本中出现"否定词+触发词"（如"不要测试"）则 -2
        得分 > 0 才进入结果。
        """
        lowered = (text or "").lower()
        hits: List[Tuple[str, int]] = []
        for meta in self._metas:
            score = 0
            for trigger in meta.triggers:
                tl = trigger.lower()
                if tl not in lowered:
                    continue
                s = 1  # 基础分：子串命中
                # 英文触发词词边界加权
                if tl.isascii() and re.search(rf"\b{re.escape(tl)}\b", lowered):
                    s += 1
                # 否定扣分：否定词紧邻触发词
                for neg in self._NEGATIONS:
                    if f"{neg}{tl}" in lowered:
                        s -= 2
                score += max(s, 0)
            if score > 0:
                hits.append((meta.name, score))
        # 得分降序；得分相同则按注册表顺序（保持稳定）
        hits.sort(key=lambda item: (-item[1], [m.name for m in self._metas].index(item[0])))
        return hits

    # ---------- 展示 ----------
    def to_markdown_table(self) -> str:
        """把能力清单渲染为 Markdown 表格（用于文档与对话展示）。"""
        lines = [
            "| 技能 | 能力 | 适用场景 | 触发条件 | 必填输入 | 前置依赖 | 后置校验 |",
            "| --- | --- | --- | --- | --- | --- | --- |",
        ]
        for m in self._metas:
            lines.append(
                f"| `{m.name}` | {m.description} | "
                f"{'; '.join(m.scenarios[:2])} | {', '.join(m.triggers[:4])} | "
                f"{', '.join(m.required_inputs) or '无'} | "
                f"{', '.join(m.dependencies) or '无'} | "
                f"{', '.join(m.post_checks) or '无'} |"
            )
        return "\n".join(lines)
