"""技能模块测试：能力清单、五个技能、触发条件与编排链。"""

from __future__ import annotations

import sys
from pathlib import Path

# 保证 tests 目录下可直接导入项目包
ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from skills.core.orchestrator import SkillOrchestrator  # noqa: E402
from skills.core.registry import SkillRegistry  # noqa: E402

# 含缺陷的演示源码（def 缺冒号）
BUGGY_SOURCE = '''def add(a, b)
    return a + b
'''

FIXED_SOURCE = '''def add(a, b):
    """两数相加。"""
    return a + b
'''


def test_registry_loads_all_skills():
    """能力清单应包含全部 5 项技能且元数据完整。"""
    registry = SkillRegistry()
    names = [m.name for m in registry.metas]
    assert names == ["codegen", "debug_fix", "unit_test", "doc_writing", "perf_opt"]
    for meta in registry.metas:
        assert meta.display_name and meta.description       # 中文能力描述
        assert meta.scenarios and meta.triggers             # 场景与触发条件
        assert meta.inputs and meta.outputs                 # 输入输出契约


def test_trigger_matching():
    """触发条件应按相关性排序返回技能。"""
    registry = SkillRegistry()
    hits = registry.match_triggers("代码报错了，帮我修复并做性能优化")
    names = [name for name, _ in hits]
    assert "debug_fix" in names and "perf_opt" in names
    # debug_fix 触发词命中最多，应排在首位
    assert names[0] == "debug_fix"


def test_codegen_from_request():
    """需求文本应命中模板并生成可运行代码。"""
    registry = SkillRegistry()
    result = registry.get("codegen")({"request": "请生成一个排序函数"})
    assert result.success
    assert "def quick_sort" in result.data["code"]


def test_codegen_from_spec():
    """结构化规格应生成带类型注解的函数骨架。"""
    registry = SkillRegistry()
    spec = {
        "function_name": "calc",
        "params": [{"name": "a", "type": "int"}, {"name": "b", "type": "int"}],
        "returns": "int",
        "docstring": "两数求和",
    }
    result = registry.get("codegen")({"spec": spec})
    assert result.success
    assert "def calc(a: int, b: int) -> int:" in result.data["code"]


def test_debug_fix_auto_fix_missing_colon():
    """def 缺冒号应被自动修复。"""
    registry = SkillRegistry()
    result = registry.get("debug_fix")({"source": BUGGY_SOURCE})
    assert result.success
    assert result.data["auto_fixed"] is True
    assert "def add(a, b):" in result.data["fixed_code"]


def test_debug_fix_undefined_name():
    """未定义名称应被诊断出来。"""
    registry = SkillRegistry()
    source = "def f():\n    return undefined_var\n"
    result = registry.get("debug_fix")({"source": source})
    assert result.success
    types = [i["type"] for i in result.data["issues"]]
    assert "undefined_name" in types


def test_unit_test_generates_and_passes():
    """对修复后的代码生成用例并全部通过。"""
    registry = SkillRegistry()
    cases = [{"function": "add", "args": [1, 2], "expected": 3}]
    result = registry.get("unit_test")({"source": FIXED_SOURCE, "cases": cases})
    assert result.success
    assert result.data["passed"] >= 1
    assert result.data["failed"] == 0
    assert "unittest" in result.data["test_code"]


def test_unit_test_fails_on_wrong_expected():
    """期望值错误时测试应失败（校验结果的真实性）。"""
    registry = SkillRegistry()
    cases = [{"function": "add", "args": [1, 2], "expected": 999}]
    result = registry.get("unit_test")({"source": FIXED_SOURCE, "cases": cases})
    assert result.success is False  # 用例失败 -> 技能返回失败
    assert result.data["failed"] >= 1


def test_doc_writing_from_source():
    """从源码生成 README，应包含函数签名。"""
    registry = SkillRegistry()
    result = registry.get("doc_writing")({"source": FIXED_SOURCE})
    assert result.success
    assert "#" in result.data["document"]
    assert "add" in result.data["document"]


def test_perf_opt_detects_string_concat():
    """循环内字符串拼接应被识别为性能问题。"""
    registry = SkillRegistry()
    source = '''def f(seq):
    out = ""
    for x in seq:
        out = out + str(x) + ","
    return out
'''
    result = registry.get("perf_opt")({"source": source})
    assert result.success
    patterns = [i["pattern"] for i in result.data["report"]]
    assert "string_concat_in_loop" in patterns


def test_orchestrator_chain_order():
    """编排链：debug_fix 执行后必须紧跟 unit_test 回归。"""
    registry = SkillRegistry()
    orchestrator = SkillOrchestrator(registry)
    report = orchestrator.execute(
        "我的模块报语法错误，修复后请做单元测试",
        context={"source": BUGGY_SOURCE, "cases": [{"function": "add", "args": [1, 2], "expected": 3}]},
    )
    assert report.ok
    chain = report.chain
    assert "debug_fix" in chain and "unit_test" in chain
    assert chain.index("debug_fix") < chain.index("unit_test")
    # 编排器应把修复后的代码回填，unit_test 基于修复版执行并通过
    assert report.results["unit_test"]["success"] is True


def test_orchestrator_fail_fast():
    """链路中某技能失败时应立即中止。"""
    registry = SkillRegistry()
    orchestrator = SkillOrchestrator(registry)
    report = orchestrator.execute(
        "我的模块报错了，请修复",
        context={"source": "def f():\n    return undefined_name\n"},  # 有未定义名但无语法错误
    )
    # debug_fix 成功，unit_test 因含未定义名（subprocess 运行 NameError）失败
    assert report.chain == ["debug_fix", "unit_test"]
    assert report.results["debug_fix"]["success"] is True
    assert report.results["unit_test"]["success"] is False
    assert report.ok is False


# ===== P0 补强：新增断言 =====

def test_debug_fix_indent_auto_fix():
    """Tab/空格混用导致的缩进错误应被自动统一为 4 空格。"""
    registry = SkillRegistry()
    source = "def f():\n\tx = 1\n    y = 2\n"  # tab 与 4 空格混用 -> TabError
    result = registry.get("debug_fix")({"source": source})
    assert result.success
    assert result.data["auto_fixed"] is True
    assert "\t" not in result.data["fixed_code"]


def test_debug_fix_undefined_name_has_suggestion():
    """未定义名应附带拼写建议字段（difflib 近似匹配）。"""
    registry = SkillRegistry()
    source = "def f():\n    return unknwon_var\n"  # 拼写错误
    result = registry.get("debug_fix")({"source": source})
    assert result.success
    undef = [i for i in result.data["issues"] if i["type"] == "undefined_name"]
    assert undef, "应检测到未定义名"
    assert "suggestion" in undef[0]  # 字段必须存在


def test_unit_test_reports_coverage():
    """安装了 coverage 后，结果应含 coverage_pct（非 None）。"""
    registry = SkillRegistry()
    result = registry.get("unit_test")(
        {"source": FIXED_SOURCE, "cases": [{"function": "add", "args": [1, 2], "expected": 3}]}
    )
    assert result.success
    assert result.data["coverage_pct"] is not None, "应报告覆盖率"
    assert 0 <= result.data["coverage_pct"] <= 100


def test_registry_negation_does_not_match():
    """否定词紧邻触发词时不应命中该技能（'不要测试' 不命中 unit_test）。"""
    registry = SkillRegistry()
    hits = dict(registry.match_triggers("这个模块不要测试，只看性能优化"))
    assert "unit_test" not in hits, "否定后不应命中 unit_test"
    assert "perf_opt" in hits, "perf_opt 应正常命中"


def test_orchestrator_low_confidence_warning():
    """低置信度诉求应在报告中给出澄清建议（warning 非阻断）。"""
    registry = SkillRegistry()
    orchestrator = SkillOrchestrator(registry)
    report = orchestrator.execute("修复并优化", context={})
    assert report.warning is not None
    assert "置信度" in report.warning
