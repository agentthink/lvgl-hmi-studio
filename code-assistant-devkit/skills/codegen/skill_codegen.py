"""技能：代码生成（codegen）。

能力：根据自然语言需求或结构化函数规格，生成可运行的 Python / C# 源码。

两种输入模式（二选一）：
  - request：需求描述文本，按关键词命中内置模板（HTTP 客户端 / 排序 / 文件读写 / 计算器）
  - spec：   结构化规格 {function_name, params, returns, docstring}，生成函数骨架

输出：code / language / filename / notes。支持语言：python / csharp。
"""

from __future__ import annotations

from typing import Any, Dict, List

from skills.core.skill_base import BaseSkill, SkillMeta, SkillResult

# 内置模板库：关键词 -> (语言, 代码文本)
_TEMPLATES: List[tuple] = [
    (
        ("http", "请求", "接口", "客户端", "抓取", "fetch"),
        "python",
        '''"""HTTP 请求工具：基于标准库 urllib，无第三方依赖。"""

from urllib import request as _request
from urllib.error import URLError
import json


def fetch_json(url: str, timeout: float = 10.0, headers: dict | None = None) -> dict:
    """发起 GET 请求并以 JSON 解析返回。

    Args:
        url: 目标地址
        timeout: 超时秒数（默认 10s）
        headers: 附加请求头（可选）

    Returns:
        解析后的 JSON 对象。

    Raises:
        URLError: 网络或 HTTP 错误时抛出
        json.JSONDecodeError: 响应不是合法 JSON 时抛出
    """
    req = _request.Request(url, headers=headers or {"User-Agent": "codegen-demo"})
    with _request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


if __name__ == "__main__":
    # 使用示例：python generated_module.py
    data = fetch_json("https://httpbin.org/json")
    print("标题:", data.get("slideshow", {}).get("title"))
''',
    ),
    (
        ("排序", "sort", "sorting"),
        "python",
        '''"""快速排序实现。"""


def quick_sort(items: list) -> list:
    """对列表进行升序排序（快速排序，平均 O(n log n)）。

    Args:
        items: 待排序列表

    Returns:
        排序后的新列表（不改动原列表）。
    """
    if len(items) <= 1:
        return list(items)
    pivot = items[len(items) // 2]
    left = [x for x in items if x < pivot]
    mid = [x for x in items if x == pivot]
    right = [x for x in items if x > pivot]
    return quick_sort(left) + mid + quick_sort(right)


if __name__ == "__main__":
    data = [5, 2, 9, 1, 7]
    print("排序前:", data)
    print("排序后:", quick_sort(data))
''',
    ),
    (
        ("文件", "读写", "读取", "写入", "file", "io"),
        "python",
        '''"""文件读写工具：安全的文本文件读写封装。"""

from pathlib import Path


def read_text(path: str, encoding: str = "utf-8") -> str:
    """读取文本文件内容。

    Args:
        path: 文件路径
        encoding: 编码（默认 utf-8）

    Returns:
        文件内容字符串。
    """
    return Path(path).read_text(encoding=encoding)


def write_text(path: str, content: str, encoding: str = "utf-8") -> None:
    """写入文本文件（自动创建父目录）。

    Args:
        path: 文件路径
        content: 内容
        encoding: 编码（默认 utf-8）
    """
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(content, encoding=encoding)


if __name__ == "__main__":
    write_text("example.txt", "你好，代码助手！")
    print("读回内容:", read_text("example.txt"))
''',
    ),
    (
        ("计算器", "计算", "calculator"),
        "python",
        '''"""简易计算器：加减乘除四则运算。"""


def add(a: float, b: float) -> float:
    """加法。"""
    return a + b


def subtract(a: float, b: float) -> float:
    """减法。"""
    return a - b


def multiply(a: float, b: float) -> float:
    """乘法。"""
    return a * b


def divide(a: float, b: float) -> float:
    """除法（除数为 0 时抛出 ValueError）。

    Raises:
        ValueError: 当 b == 0 时
    """
    if b == 0:
        raise ValueError("除数不能为 0")
    return a / b


if __name__ == "__main__":
    print("1 + 2 =", add(1, 2))
    print("10 / 4 =", divide(10, 4))
''',
    ),
    # ---- C# 模板 ----
    (
        ("http", "请求", "接口", "客户端", "抓取", "fetch", "web api"),
        "csharp",
        '''// HTTP 请求工具：基于 System.Net.Http 的轻量封装
using System;
using System.Net.Http;
using System.Threading.Tasks;

public static class HttpHelper
{
    /// <summary>GET 请求并返回响应文本。</summary>
    public static async Task<string> GetAsync(string url)
    {
        using var client = new HttpClient();
        return await client.GetStringAsync(url);
    }

    /// <summary>GET 请求并解析为 JSON 字符串（可配合 System.Text.Json 使用）。</summary>
    public static async Task Main()
    {
        var body = await GetAsync("https://httpbin.org/json");
        Console.WriteLine(body);
    }
}
''',
    ),
    (
        ("排序", "sort", "sorting", "quicksort"),
        "csharp",
        '''// 快速排序实现（泛型，原地改写列表）
using System;
using System.Collections.Generic;
using System.Linq;

public static class SortHelper
{
    /// <summary>对列表执行快速排序（升序，平均 O(n log n)）。</summary>
    public static void QuickSort<T>(List<T> items) where T : IComparable<T>
    {
        QuickSort(items, 0, items.Count - 1);
    }

    private static void QuickSort<T>(List<T> items, int lo, int hi) where T : IComparable<T>
    {
        if (lo >= hi) return;
        int p = Partition(items, lo, hi);
        QuickSort(items, lo, p - 1);
        QuickSort(items, p + 1, hi);
    }

    private static int Partition<T>(List<T> items, int lo, int hi) where T : IComparable<T>
    {
        T pivot = items[hi];
        int i = lo - 1;
        for (int j = lo; j < hi; j++)
        {
            if (items[j].CompareTo(pivot) <= 0)
            {
                i++;
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
        (items[i + 1], items[hi]) = (items[hi], items[i + 1]);
        return i + 1;
    }

    public static void Main()
    {
        var data = new List<int> { 5, 2, 9, 1, 7 };
        QuickSort(data);
        Console.WriteLine(string.Join(", ", data));
    }
}
''',
    ),
]


class CodeGenSkill(BaseSkill):
    """代码生成技能。"""

    meta = SkillMeta(
        name="codegen",
        display_name="代码生成",
        description="根据需求描述或函数规格生成 Python / C# 源码",
        module_path="skills.codegen.skill_codegen",
        class_name="CodeGenSkill",
        scenarios=["从零实现新功能模块", "按函数规格生成骨架代码", "生成工具函数/模板代码"],
        triggers=["生成", "实现", "编写", "写一个", "create", "implement", "generate", "写代码"],
        inputs={
            "request": "需求描述文本（与 spec 二选一）",
            "spec": "函数规格 dict：function_name/params/returns/docstring",
            "language": "目标语言：python / csharp（默认 python）",
            "filename": "生成文件的建议文件名（可选）",
        },
        outputs={"code": "生成的源码", "language": "语言", "filename": "文件名", "notes": "使用说明列表"},
        required_inputs=[],
        dependencies=[],
        suggested_next=["unit_test", "doc_writing"],
    )

    # ---------- 内部实现 ----------
    # Python 类型 -> C# 类型映射（用于 spec 骨架生成）
    _CS_TYPE_MAP = {
        "int": "int",
        "float": "double",
        "str": "string",
        "bool": "bool",
        "list": "List<T>",
        "dict": "Dictionary<string, T>",
        "None": "void",
        "any": "object",
    }

    @classmethod
    def _cs_type(cls, t: str) -> str:
        """把规格中的 Python 风格类型映射为 C# 类型。"""
        base = (t or "any").strip()
        return cls._CS_TYPE_MAP.get(base.lower(), base)

    @staticmethod
    def _gen_from_spec_python(spec: Dict[str, Any]) -> str:
        """按结构化规格生成 Python 函数骨架（带类型注解与 docstring）。"""
        name = spec.get("function_name", "generated_func")
        params: List[dict] = spec.get("params", [])          # [{"name","type","default"}]
        returns = spec.get("returns", "None")
        docstring = spec.get("docstring", "").strip() or "函数功能说明。"

        # 参数 -> "name: type = default" 或 "name: type"
        def _fmt(p: dict) -> str:
            base = f"{p['name']}: {p.get('type', 'Any')}"
            if p.get("default") is not None:
                base += f" = {p['default']}"
            return base

        signature = ", ".join(_fmt(p) for p in params)
        return f'''"""自动生成的模块：{docstring}"""


def {name}({signature}) -> {returns}:
    """{docstring}

    参数与返回值类型由规格声明，请按需补充实现逻辑。
    """
    # TODO: 在这里实现具体逻辑
    return None
'''

    @classmethod
    def _gen_from_spec_csharp(cls, spec: Dict[str, Any]) -> str:
        """按结构化规格生成 C# 静态方法骨架。"""
        name = spec.get("function_name", "GeneratedFunc")
        params: List[dict] = spec.get("params", [])
        returns = spec.get("returns", "None")
        docstring = spec.get("docstring", "").strip() or "函数功能说明。"

        # 参数 -> "type name" 或 "type name = default"
        def _fmt(p: dict) -> str:
            base = f"{cls._cs_type(p.get('type', 'object'))} {p['name']}"
            if p.get("default") is not None:
                base += f" = {p['default']}"
            return base

        signature = ", ".join(_fmt(p) for p in params)
        cs_return = cls._cs_type(returns)
        default_return = "return default;" if cs_return != "void" else ""
        return f'''// 自动生成：{docstring}
using System;

public static class Generated
{{
    /// <summary>{docstring}</summary>
    public static {cs_return} {name}({signature})
    {{
        // TODO: 在这里实现具体逻辑
        {default_return}
    }}
}}
'''

    @staticmethod
    def _gen_from_request(request: str, language: str | None = None) -> tuple[str, str, str, List[str]]:
        """按需求文本命中模板；返回 (代码, 语言, 文件名, 使用说明)。

        Args:
            request: 需求描述文本
            language: 目标语言过滤（None 表示命中任一语言的模板）
        """
        lowered = request.lower()
        for keywords, lang, code in _TEMPLATES:
            if any(k in lowered for k in keywords):
                if language and lang != language:
                    continue  # 用户指定语言时不使用其他语言模板
                filename = "generated_module.cs" if lang == "csharp" else "generated_module.py"
                return code, lang, filename, [f"命中模板：{keywords[0]}（{lang}）"]
        # 兜底：按语言生成通用骨架
        if language == "csharp":
            code = '''// 自动生成的通用骨架（C#）
using System;

public static class Program
{
    /// <summary>程序入口。</summary>
    public static void Main()
    {
        // TODO: 在此实现业务逻辑
        Console.WriteLine("Hello from codegen!");
    }
}
'''
            return code, "csharp", "generated_module.cs", ["未命中具体模板，生成 C# 通用骨架"]
        code = '''"""自动生成的通用模块（骨架）。

请在 TODO 处补充你的业务逻辑。
"""


def main():
    """程序入口。"""
    # TODO: 在此实现业务逻辑
    print("Hello from codegen!")


if __name__ == "__main__":
    main()
'''
        return code, "python", "generated_module.py", ["未命中具体模板，生成通用骨架"]

    # ---------- 执行入口 ----------
    def run(self, context: Dict[str, Any]) -> SkillResult:
        language = (context.get("language") or "python").lower()
        if language not in ("python", "csharp"):
            return SkillResult(success=False, error=f"暂不支持语言: {language}，当前支持 python / csharp")

        if context.get("spec"):
            if language == "csharp":
                code = self._gen_from_spec_csharp(context["spec"])
                notes = ["按函数规格生成 C# 方法骨架，请补充方法体实现"]
            else:
                code = self._gen_from_spec_python(context["spec"])
                notes = ["按函数规格生成，请补充函数体实现"]
        else:
            request = context.get("request") or context.get("source") or ""
            if not request:
                return SkillResult(success=False, error="缺少输入：request 与 spec 至少提供一个")
            code, language, default_name, notes = self._gen_from_request(request, language)

        suffix = ".cs" if language == "csharp" else ".py"
        filename = context.get("filename") or f"generated_module{suffix}"
        if not filename.endswith(suffix):
            filename += suffix
        return SkillResult(
            success=True,
            data={
                "code": code,
                "language": language,
                "filename": filename,
                "notes": notes,
                "summary": f"已生成 {filename}（{len(code.splitlines())} 行, {language}）",
            },
        )
