"""C# 语言适配测试：analyzer 语言检测/启发式体检、builder .csproj 支持、codegen C# 生成。"""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from services.core import analyzer, builder
from skills.codegen.skill_codegen import CodeGenSkill

# 一份"健康"的 C# 源码（括号平衡、含 using/类/方法）
HEALTHY_CS = """using System;
using System.Collections.Generic;

namespace Demo
{
    public class Calc
    {
        public static int Add(int a, int b)
        {
            return a + b;
        }
    }
}
"""

# 一份"带缺陷"的 C# 源码（括号不平衡 + TODO）
BROKEN_CS = """using System;

public class Broken
{
    public void F()
    {
        Console.WriteLine("x");  // TODO: 待完善
}
"""


# ---------------- 语言检测 ----------------
def test_detect_language_by_extension():
    assert analyzer.detect_language(filename="a.py") == "python"
    assert analyzer.detect_language(filename="a.cs") == "csharp"


def test_detect_language_by_content():
    assert analyzer.detect_language(source="public class Foo { }") == "csharp"
    assert analyzer.detect_language(source="def f():\n    pass") == "unknown"


# ---------------- C# 静态分析 ----------------
def test_analyze_csharp_healthy():
    report = analyzer.analyze_csharp_source(HEALTHY_CS, "Calc.cs")
    assert report["language"] == "csharp"
    assert report["ok"] is True
    assert report["class_count"] == 1
    assert report["method_count"] == 1
    assert report["usings"] == ["System", "System.Collections.Generic"]
    assert report["sloc"] > 0


def test_analyze_csharp_unbalanced_bracket():
    report = analyzer.analyze_csharp_source(BROKEN_CS, "Broken.cs")
    assert report["ok"] is False
    types = [i["type"] for i in report["issues"]]
    assert "unbalanced_bracket" in types
    assert any(i["message"].startswith("未闭合") for i in report["issues"])


def test_analyze_csharp_todo_marker():
    report = analyzer.analyze_csharp_source(BROKEN_CS, "Broken.cs")
    assert any(i["type"] == "todo_marker" for i in report["issues"])


def test_analyze_source_auto_detect_csharp():
    """analyze_source 入口自动按内容/扩展名检测语言。"""
    report = analyzer.analyze_source(HEALTHY_CS, "Calc.cs")
    assert report["language"] == "csharp"
    assert report["ok"] is True
    # Python 报告不受影响
    py_report = analyzer.analyze_source("def add(a, b):\n    return a + b\n")
    assert py_report["language"] == "python"


def test_analyze_csharp_strings_not_counted_as_brackets():
    """字符串/注释内的括号不应影响平衡检查。"""
    src = 'public class A\n{\n    public string S => "a{b}c"; // })\n}\n'
    report = analyzer.analyze_csharp_source(src, "A.cs")
    assert report["ok"] is True, report["issues"]


# ---------------- builder .csproj 支持 ----------------
def test_build_csproj_project(tmp_path):
    """无 pyproject.toml、有 .csproj 的项目可构建，元数据来自 csproj。"""
    proj = tmp_path / "CalcLib"
    proj.mkdir()
    (proj / "CalcLib.csproj").write_text(
        "<Project><PropertyGroup>"
        "<AssemblyName>CalcLib</AssemblyName><Version>2.3.1</Version>"
        "</PropertyGroup></Project>",
        encoding="utf-8",
    )
    (proj / "Calc.cs").write_text("namespace CalcLib { public class Calc { } }", encoding="utf-8")

    record = builder.build_project(proj, output_dir=tmp_path / "out")
    assert record["name"] == "CalcLib"
    assert record["version"] == "2.3.1"
    artifact = Path(record["artifact_path"])
    assert artifact.exists() and artifact.name == "CalcLib-2.3.1.tar.gz"


def test_build_plain_dir_fallback(tmp_path):
    """既无 pyproject.toml 也无 .csproj：核心层兜底用目录名 + 0.0.0。"""
    proj = tmp_path / "plain"
    proj.mkdir()
    record = builder.build_project(proj, output_dir=tmp_path / "out")
    assert record["name"] == "plain"
    assert record["version"] == "0.0.0"


# ---------------- codegen C# 支持 ----------------
def test_codegen_csharp_template():
    """显式 language=csharp 时命中 C# 模板，产出 .cs 文件。"""
    skill = CodeGenSkill()
    result = skill({"request": "生成一个HTTP请求工具", "language": "csharp"})
    assert result.success
    assert result.data["language"] == "csharp"
    assert result.data["filename"].endswith(".cs")
    assert "System.Net.Http" in result.data["code"]


def test_codegen_csharp_spec():
    """按 spec 生成 C# 方法骨架（类型映射）。"""
    skill = CodeGenSkill()
    result = skill(
        {
            "spec": {
                "function_name": "CalculateTotal",
                "params": [{"name": "prices", "type": "list"}, {"name": "taxRate", "type": "float", "default": 0.1}],
                "returns": "float",
                "docstring": "计算含税总价",
            },
            "language": "csharp",
        }
    )
    assert result.success
    code = result.data["code"]
    assert "public static double CalculateTotal(List<T> prices, double taxRate = 0.1)" in code


def test_codegen_python_default_still_works():
    """默认语言仍为 python，输出 .py 文件。"""
    skill = CodeGenSkill()
    result = skill({"request": "生成一个HTTP请求工具"})
    assert result.success
    assert result.data["language"] == "python"
    assert result.data["filename"].endswith(".py")


def test_codegen_unsupported_language():
    skill = CodeGenSkill()
    result = skill({"request": "生成代码", "language": "rust"})
    assert result.success is False
    assert "不支持" in result.error


# ---------------- Roslyn 集成（dotnet 可用时） ----------------
def test_analyze_file_roslyn_integration(tmp_path):
    """.cs 文件经 analyze_file 时叠加 Roslyn 真实语法检查（dotnet 可用则 checked=True）。"""
    cs = tmp_path / "Bad.cs"
    cs.write_text('public class Bad {\n    public void F() {\n        Console.WriteLine("x")\n    }\n}\n', encoding="utf-8")
    report = analyzer.analyze_file(cs)
    assert report["language"] == "csharp"
    if report["compile"]["checked"]:
        # Roslyn 启用时：语法错误应被标记为高严重度
        assert report["ok"] is False
        assert any(i["type"] == "csharp_syntax" for i in report["issues"])
    else:
        # dotnet 不可用：跳过（不失败）
        assert report["compile"]["reason"]


def test_analyze_file_roslyn_healthy(tmp_path):
    """健康的 .cs 文件：Roslyn 语法检查通过（若启用）。"""
    cs = tmp_path / "Ok.cs"
    cs.write_text(HEALTHY_CS, encoding="utf-8")
    report = analyzer.analyze_file(cs)
    if report["compile"]["checked"]:
        assert not any(i["type"] == "csharp_syntax" for i in report["issues"])


def test_mask_csharp_raw_string_literal():
    """三引号原始字符串字面量内的括号不应影响平衡检查。"""
    src = 'public class Raw\n{\n    public string Gen() =>\n        """\n        opcua_connect("opc.tcp://<host>:4840");\n        opcua_start_polling(500);\n        """ + (1 > 0 ? "yes" : "no");\n}'
    report = analyzer.analyze_csharp_source(src, "Raw.cs")
    assert report["ok"] is True, report["issues"]


def test_mask_csharp_interpolated_string():
    """插值字符串 $"{expr}"：表达式内括号参与平衡，字符串内容遮蔽。"""
    src = 'public class S\n{\n    public string F(string v) =>\n        $"value={v} {(1 + 2)}";\n}'
    report = analyzer.analyze_csharp_source(src, "S.cs")
    assert report["ok"] is True, report["issues"]


def test_mask_csharp_escaped_braces():
    """插值字符串内 {{ }} 转义字面花括号不应触发插值代码区。"""
    src = 'public class S\n{\n    public string F(string a, string b) =>\n        $"    {{\\"{a}\\", \\"{b}\\"}},";\n}'
    report = analyzer.analyze_csharp_source(src, "S.cs")
    assert report["ok"] is True, report["issues"]


def test_mask_csharp_verbatim_string():
    """verbatim 字符串 @"" 内容遮蔽（含双引号转义 ""）。"""
    src = 'public class S\n{\n    public string P => @"a{b}c ""quoted"" (x)";\n}'
    report = analyzer.analyze_csharp_source(src, "S.cs")
    assert report["ok"] is True, report["issues"]


def test_mask_csharp_unicode_escape_char():
    """字符字面量里的 \\uXXXX 转义（PromptTemplateEngine 场景）。"""
    src = "public class S\n{\n    public bool F(char c) =>\n        c == '\\u200b' || c == '\\ufeff';\n}"
    report = analyzer.analyze_csharp_source(src, "S.cs")
    assert report["ok"] is True, report["issues"]


# ---------------- deep 语义检查（项目级 dotnet build） ----------------
def test_find_project_dir(tmp_path):
    """向上查找 *.csproj 项目目录。"""
    proj = tmp_path / "Sub" / "Nested"
    proj.mkdir(parents=True)
    (proj / "App.csproj").write_text("<Project/>", encoding="utf-8")
    f = proj / "X.cs"
    assert analyzer._find_project_dir(f) == proj

    # 无 csproj 的独立目录（祖先链上无任何 csproj）：返回 None
    orphan = tmp_path / "OrphanDir"
    orphan.mkdir()
    assert analyzer._find_project_dir(orphan / "Orphan.cs") is None


def test_analyze_file_deep_semantic_error(tmp_path):
    """--deep：dotnet build 检出语义错误（dotnet 可用时）。"""
    def make_project(root: Path) -> Path:
        root.mkdir(parents=True, exist_ok=True)
        (root / "Demo.csproj").write_text(
            "<Project Sdk=\"Microsoft.NET.Sdk\">"
            "<PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>"
            "</Project>",
            encoding="utf-8",
        )
        return root

    # 正常项目：构建成功，无语义错误
    good_proj = make_project(tmp_path / "Good")
    good = good_proj / "Good.cs"
    good.write_text("public static class Good { public static int F() => 1; }\n", encoding="utf-8")
    r_good = analyzer.analyze_file(good, deep=True)
    if r_good["deep"]["checked"]:
        assert r_good["deep"]["build_success"] is True, r_good["deep"]
        assert not any(i["type"] == "csharp_semantic" for i in r_good["issues"])

    # 错误项目：检出 CS0029 语义错误
    bad_proj = make_project(tmp_path / "Bad")
    bad = bad_proj / "Bad.cs"
    bad.write_text('public static class Bad { public static void F() { int x = "abc"; } }\n', encoding="utf-8")
    r_bad = analyzer.analyze_file(bad, deep=True)
    if r_bad["deep"]["checked"]:
        assert r_bad["deep"]["build_success"] is False
        sem = [i for i in r_bad["issues"] if i["type"] == "csharp_semantic"]
        assert len(sem) >= 1
        assert "CS0029" in sem[0]["message"]
