#!/usr/bin/env python3
"""PropertyPanelView 按域拆 partial（P0-2）。

零丢失验证：每个新文件的 body（header/`{`/`}` 之间）必须与原文对应区间逐行一致
（唯一变化：段末空行去除、partial 关键字、文件头）。边界行逐一断言内容，防 off-by-one。

用法：
    python3 tools/split-propertypanelview.py
"""
import sys
from pathlib import Path

SRC = Path("src/HmiEditor.App/Views/PropertyPanelView.axaml.cs")
OUT = Path("src/HmiEditor.App/Views")

# (文件名, 段首行号[1基], 段末行号[1基, 含], 断言段首原文, 断言段末原文)
# 段首/段末必须都是成员行或段注释行；边界空行不进入任何段。
SECTIONS = [
    ("PropertyPanelView.axaml.cs", 25, 276,
     "    private HmiControl? _currentControl;", "    }"),
    ("PropertyPanelView.Screen.cs", 278, 537,
     "    // ═══════════════════════════════════════════════", "    }"),
    ("PropertyPanelView.Display.cs", 539, 616,
     "    // ── 状态化样式（完整四态：Normal/Pressed/Checked/Disabled）──", "    }"),
    ("PropertyPanelView.Parameters.cs", 618, 871,
     "    // ── 控件功能参数（按控件类型显隐）──",
     "        => (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();"),
    ("PropertyPanelView.Assets.cs", 873, 1074,
     "    // ── 图片 / SVG 资源选择 ──", "    }"),
    ("PropertyPanelView.VisualStates.cs", 1076, 1404,
     "    /// <summary>", "    }"),
    ("PropertyPanelView.Binding.cs", 1406, 1753,
     "    // ── 变量绑定（联动模型）──", "    }"),
    ("PropertyPanelView.Actions.cs", 1755, 2087,
     "    // ── 动作（联动模型）──", "    }"),
    ("PropertyPanelView.Faceplate.cs", 2089, 2427,
     "    // ── Faceplate 参数 ──", "    }"),
    ("PropertyPanelView.Changes.cs", 2429, 2694,
     "    // ── 属性变更 ──", "    }"),
    ("PropertyPanelView.Ai.cs", 2696, 2796,
     "    // ═══════════════════════════════════════════════",
     "    public HmiScreen? CurrentScreen { get; set; }"),
]

def main():
    lines = SRC.read_text(encoding="utf-8").splitlines(keepends=True)  # lines[i] 是第 i+1 行
    total = len(lines)
    print(f"原文 {SRC}: {total} 行")

    # 头：1..23 行（usings + namespace + class 声明行，不含 `{`）
    # 24 行是 `{`
    assert lines[22].strip() == "public partial class PropertyPanelView : UserControl", lines[22]
    assert lines[23].strip() == "{", lines[23]
    header = lines[:23]  # 1..23 行
    # 类闭合 `}` 在 2797 行；2799..2804 行是 ActionChipModel
    assert lines[2796].rstrip() == "}", lines[2796]
    action_chip = lines[2798:]  # 2799..2804 行（含其前的注释行 2799）

    # 校验所有边界行内容
    for name, start, end, want_start, want_end in SECTIONS:
        got_start = lines[start - 1].rstrip("\n")
        got_end = lines[end - 1].rstrip("\n")
        assert got_start == want_start, f"{name} 段首断言失败\n  期望: {want_start!r}\n  实际: {got_start!r}"
        assert got_end == want_end, f"{name} 段末断言失败\n  期望: {want_end!r}\n  实际: {got_end!r}"
    print("边界断言: 全部通过 ✓")

    # 段间不得重叠 / 不得漏行：下一段首 = 上一段末 + 2（中间恰好一个空行）
    separators = []  # 段间空行的 0 基行号（这些行被刻意去除：段末空行并入前段并去除）
    for i in range(len(SECTIONS) - 1):
        end = SECTIONS[i][2]
        next_start = SECTIONS[i + 1][1]
        gap = next_start - end - 1
        assert gap == 1, f"段 {SECTIONS[i][0]} 与 {SECTIONS[i+1][0]} 之间空行数 = {gap}，应为 1"
        sep_line_1based = end + 1
        assert not lines[sep_line_1based - 1].strip(), f"段间第 {sep_line_1based} 行应为空行"
        separators.append(sep_line_1based - 1)  # 0 基
    print("段间空行: 全部恰好 1 行且为空 ✓")

    # 写文件
    for name, start, end, _, _ in SECTIONS:
        body = lines[start - 1:end]  # 1 基 → 0 基切片
        file_lines = list(header) + ["{\n"] + list(body) + ["}\n"]
        if name == "PropertyPanelView.Ai.cs":
            file_lines += action_chip
        (OUT / name).write_text("".join(file_lines), encoding="utf-8")
        print(f"OK  {name:<40} body {len(body):>4} 行 == 原 {start}-{end}")

    # 零丢失：拼接各段 body == 原文 25..2796 行类体（去掉被去除的段间空行）
    rebuilt = []
    for name, start, end, _, _ in SECTIONS:
        if name == "PropertyPanelView.Ai.cs":
            rebuilt.extend(lines[start - 1:2796])
        else:
            rebuilt.extend(lines[start - 1:end])
    original_body = [l for i, l in enumerate(lines[24:2796]) if (24 + i) not in separators]
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