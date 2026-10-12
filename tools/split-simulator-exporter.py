#!/usr/bin/env python3
"""SimulatorProjectExporter 按域拆 partial（P0-3）。

零丢失验证：每个新文件的 body（header/`{`/`}` 之间）必须与原文对应区间
逐行一致（唯一变化：段末空行去除、partial 关键字、文件头）。边界行逐一断言内容，防 off-by-one。

用法：
    python3 tools/split-simulator-exporter.py
"""
import sys
from pathlib import Path

SRC = Path("src/HmiEditor.Core/Services/SimulatorProjectExporter.cs")
OUT = Path("src/HmiEditor.Core/Services")

# (输出文件名, 类体段首[1基], 类体段末[1基,含], 段首断言, 段末断言, 分部文档注释)
# 段首/段末必须都是成员行或成员文档注释行；段间空行不进入任何段。
SECTIONS = [
    ("SimulatorProjectExporter.cs", 36, 203,
     "    private static readonly Regex ScreenFuncRegex =", "    }",
     None),  # 主文件：保留 SimulatorExportResult record + 类文档
    ("SimulatorProjectExporter.MainC.cs", 205, 864,
     "    private static string BuildMainC(HmiProject project, IReadOnlyList<string> screenFunctions, HmiScreen bootScreen, CResourceSymbols symbols, IReadOnlyList<RuntimeTag>? tags = null)",
     "    }",
     "/// <summary>SimulatorProjectExporter 分部 —— main.c 生成（SDL/LVGL 主循环、标签表、登录门、引擎轮询）。</summary>"),
    ("SimulatorProjectExporter.Configs.cs", 866, 1183,
     "    /// <summary>生成 sim_config.h —— 模拟器参数配置（分辨率/帧率/全屏/缩放/自检帧数）。</summary>",
     "    }",
     "/// <summary>SimulatorProjectExporter 分部 —— 配置类产物（sim_config.h / CMakeLists.txt / lv_conf.h）。</summary>"),
    ("SimulatorProjectExporter.Readme.cs", 1185, 1324,
     "    private static string BuildReadme(HmiProject project, IReadOnlyList<string> screenFunctions)",
     "    }",
     "/// <summary>SimulatorProjectExporter 分部 —— README.md 生成。</summary>"),
    ("SimulatorProjectExporter.Assets.cs", 1326, 1538,
     "    /// <summary>",
     "    }",
     "/// <summary>SimulatorProjectExporter 分部 —— 图片/字体资源 C 文件生成（BuildImagesC / BuildFontsC）。</summary>"),
]

def main():
    lines = SRC.read_text(encoding="utf-8").splitlines(keepends=True)
    total = len(lines)
    print(f"原文 {SRC}: {total} 行")

    # 头：1..11 行（usings + namespace）；12 空行；record 13..17（13 是文档、14 声明、15..17 参数/收尾）
    # 18 空行；类文档 19..33；class 声明 34；`{` 35；类体 36..1538；1539 空行；1540 `}`（类闭合）
    header = lines[:11]                      # 1..11
    assert lines[10].strip() == "namespace HmiEditor.Core.Services;", lines[10]
    assert lines[11].strip() == "", lines[11]
    assert "public record SimulatorExportResult" in lines[13], lines[13]
    assert lines[33].strip() == "public static class SimulatorProjectExporter", lines[33]
    assert lines[34].strip() == "{", lines[34]
    assert lines[1539].rstrip() == "}", lines[1539]
    record_and_doc = lines[12:33]            # 12..33（record + 类文档，不含 class 声明行 34）
    # 类体原文 = 36..1538（1 基）= 0 基 35..1537

    # 校验所有边界行内容
    for name, start, end, want_start, want_end, _ in SECTIONS:
        got_start = lines[start - 1].rstrip("\n")
        got_end = lines[end - 1].rstrip("\n")
        assert got_start == want_start, f"{name} 段首断言失败\n  期望: {want_start!r}\n  实际: {got_start!r}"
        assert got_end == want_end, f"{name} 段末断言失败\n  期望: {want_end!r}\n  实际: {got_end!r}"
    print("边界断言: 全部通过 ✓")

    # 段间不得重叠 / 不得漏行：下一段首 = 上一段末 + 2（中间恰好一个空行）
    separators = []
    for i in range(len(SECTIONS) - 1):
        end = SECTIONS[i][2]
        next_start = SECTIONS[i + 1][1]
        gap = next_start - end - 1
        assert gap == 1, f"段 {SECTIONS[i][0]} 与 {SECTIONS[i+1][0]} 之间空行数 = {gap}，应为 1"
        sep_line_1based = end + 1
        assert not lines[sep_line_1based - 1].strip(), f"段间第 {sep_line_1based} 行应为空行"
        separators.append(sep_line_1based - 1)
    print("段间空行: 全部恰好 1 行且为空 ✓")

    # 写文件
    for name, start, end, _, _, domain_doc in SECTIONS:
        body = lines[start - 1:end]          # 1 基 → 0 基切片（类体内成员）
        if name == "SimulatorProjectExporter.cs":
            head = header + ["\n"] + record_and_doc
            # class 声明改 partial
            head_lines = list(header) + ["\n"] + list(record_and_doc)
            content_lines = head_lines + ["public static partial class SimulatorProjectExporter\n", "{\n"] + list(body) + ["}\n"]
        else:
            content_lines = (list(header) + ["\n", domain_doc + "\n",
                             "public static partial class SimulatorProjectExporter\n", "{\n"]
                             + list(body) + ["}\n"])
        (OUT / name).write_text("".join(content_lines), encoding="utf-8")
        print(f"OK  {name:<44} body {len(body):>4} 行 == 原 {start}-{end}")

    # 零丢失：拼接各段类体 == 原文 36..1538 行（去掉段间空行）
    rebuilt = []
    for name, start, end, _, _, _ in SECTIONS:
        rebuilt.extend(lines[start - 1:end])
    original_body = [l for i, l in enumerate(lines[35:1538]) if (35 + i) not in set(separators)]
    if rebuilt != original_body:
        for k, (a, b) in enumerate(zip(rebuilt, original_body)):
            if a != b:
                print(f"差异 @ 拼接第 {k} 行: {a!r} vs {b!r}")
                break
        if len(rebuilt) != len(original_body):
            print(f"行数不等: rebuilt={len(rebuilt)} original_body={len(original_body)}")
        sys.exit("零丢失验证失败")
    print("结论: 零丢失 ✓（类体各段逐行一致，去除段间空行）")

if __name__ == "__main__":
    main()