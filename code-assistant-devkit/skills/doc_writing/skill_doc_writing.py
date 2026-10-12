"""技能：文档编写（doc_writing）。

能力：从源码自动抽取模块 docstring、函数签名与参数说明，生成 Markdown 文档；
     也支持把纯文本内容直接排版为文档（doc_type 支持 README / api / changelog）。

输入：
  - source：   源码（可选，自动抽取函数信息）
  - content：  纯文本内容（可选，source 缺失时使用）
  - doc_type： README / api / changelog（默认 README）
  - output_dir：保存目录（可选，写入 document.md 并返回路径）
"""

from __future__ import annotations

import ast
import textwrap
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List

from skills.core.skill_base import BaseSkill, SkillMeta, SkillResult


class DocWritingSkill(BaseSkill):
    """文档编写技能。"""

    meta = SkillMeta(
        name="doc_writing",
        display_name="文档编写",
        description="从源码或内容生成 Markdown 文档（README/API/变更记录）",
        module_path="skills.doc_writing.skill_doc_writing",
        class_name="DocWritingSkill",
        scenarios=["为项目/模块生成 README", "为接口生成 API 文档", "生成变更记录"],
        triggers=["文档", "说明", "注释", "readme", "doc", "documentation", "写文档", "说明文档"],
        inputs={
            "source": "源码（可选，自动抽取函数签名）",
            "content": "纯文本内容（可选）",
            "doc_type": "文档类型：README/api/changelog（默认 README）",
            "output_dir": "保存目录（可选）",
        },
        outputs={"document": "生成的 Markdown 文档", "doc_type": "文档类型", "path": "保存路径"},
        required_inputs=[],
        dependencies=[],
        suggested_next=[],
    )

    # ---------- 抽取 ----------
    @staticmethod
    def _extract_functions(code: str) -> List[Dict[str, Any]]:
        """抽取函数：name / params(name, default) / docstring。"""
        funcs = []
        try:
            tree = ast.parse(code)
        except SyntaxError:
            return funcs
        for node in ast.walk(tree):
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
                params = []
                for i, arg in enumerate(node.args.args):
                    default = None
                    if len(node.args.defaults) > 0:
                        idx = i - (len(node.args.args) - len(node.args.defaults))
                        if idx >= 0:
                            default = ast.unparse(node.args.defaults[idx])
                    params.append({"name": arg.arg, "default": default})
                funcs.append(
                    {
                        "name": node.name,
                        "params": params,
                        "docstring": ast.get_docstring(node) or "",
                    }
                )
        return funcs

    @staticmethod
    def _extract_module_docstring(code: str) -> str:
        """抽取模块级 docstring。"""
        try:
            tree = ast.parse(code)
            return ast.get_docstring(tree) or ""
        except SyntaxError:
            return ""

    # ---------- 文档渲染 ----------
    def _render(self, doc_type: str, title: str, content: str) -> str:
        """按文档类型渲染模板。"""
        now = datetime.now().strftime("%Y-%m-%d %H:%M")
        if doc_type == "changelog":
            return (
                f"# 变更记录（{title}）\n\n"
                f"> 本文档由 doc_writing 技能自动生成于 {now}\n\n"
                f"## [未发布]\n\n{content}\n"
            )
        if doc_type == "api":
            return (
                f"# API 文档：{title}\n\n"
                f"> 本文档由 doc_writing 技能自动生成于 {now}\n\n"
                f"## 函数一览\n\n{content}\n"
            )
        # README
        return (
            f"# {title}\n\n"
            f"> 本文档由 doc_writing 技能自动生成于 {now}\n\n"
            f"## 简介\n\n{content}\n"
        )

    # ---------- 执行入口 ----------
    def run(self, context: Dict[str, Any]) -> SkillResult:
        doc_type = (context.get("doc_type") or "README").lower()
        if doc_type not in ("readme", "api", "changelog"):
            return SkillResult(success=False, error=f"不支持的文档类型: {doc_type}")

        source = context.get("source") or ""
        content = (context.get("content") or "").strip()

        # 1) 有源码：自动生成结构化文档
        if source:
            module_doc = self._extract_module_docstring(source)
            funcs = self._extract_functions(source)
            title = module_doc.splitlines()[0] if module_doc else "自动生成模块"
            sections = [textwrap.dedent(module_doc) if module_doc else "暂无模块说明。"]

            if funcs:
                rows = []
                for f in funcs:
                    sig_params = ", ".join(
                        p["name"] + (f"={p['default']}" if p["default"] is not None else "")
                        for p in f["params"]
                    )
                    rows.append(
                        f"### `{f['name']}({sig_params})`\n\n"
                        f"{f['docstring'] or '无参数说明。'}\n"
                    )
                sections.append("## 函数\n\n" + "\n".join(rows))
                if doc_type == "api":
                    # API 文档模式下追加参数表格
                    table = ["| 函数 | 参数 | 说明 |", "| --- | --- | --- |"]
                    for f in funcs:
                        sig = ", ".join(p["name"] for p in f["params"])
                        table.append(f"| `{f['name']}` | `{sig}` | {f['docstring'].splitlines()[0] if f['docstring'] else '-'} |")
                    sections.append("## 参数说明\n\n" + "\n".join(table))
            document = self._render(doc_type, title, "\n\n".join(sections))
        # 2) 无源码：把 content 排版为文档
        elif content:
            title = context.get("title") or "文档"
            document = self._render(doc_type, title, content)
        else:
            return SkillResult(success=False, error="缺少输入：source 与 content 至少提供一个")

        path = None
        if context.get("output_dir"):
            # 按文档类型命名输出文件：README.md / API.md / CHANGELOG.md
            file_names = {"readme": "README.md", "api": "API.md", "changelog": "CHANGELOG.md"}
            out_dir = Path(context["output_dir"])
            out_dir.mkdir(parents=True, exist_ok=True)
            path = out_dir / file_names[doc_type]
            path.write_text(document, encoding="utf-8")

        return SkillResult(
            success=True,
            data={
                "document": document,
                "doc_type": doc_type,
                "path": str(path) if path else None,
                "summary": f"已生成 {doc_type.upper()} 文档（{len(document.splitlines())} 行）",
            },
        )
