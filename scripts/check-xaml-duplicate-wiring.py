#!/usr/bin/env python3
"""扫描「XAML 已声明事件属性 + code-behind 又对同一控件同一事件 +=」的重复接线。

判据（不要求 handler 同名）：控件 X 在 .axaml 里写了 `Click="..."`，
则 .axaml.cs 里再出现 `X.Click += ...` 就是**第二路接线** —— Avalonia 的
`+=` 不去重（实测：`Click += H` 两次 ⇒ handler 跑 2 次；
`Click += H` 与 `AddHandler(ClickEvent, H)` 混用同样 2 次）。

⚠ **单向判据**：只判"XAML 声明过的那个事件没被重复接"。
   反过来（code-behind 接了、XAML 没声明）是合法且常见的（代码构建的控件只能这么接），
   不能反过来判，否则一片假红。

用法：
    python3 scripts/check-xaml-duplicate-wiring.py             # 判据（门禁用）
    python3 scripts/check-xaml-duplicate-wiring.py --json      # 机器可读
    python3 scripts/check-xaml-duplicate-wiring.py --selftest  # 自检：判据两侧都真被读取？
"""
from __future__ import annotations

import json
import re
import sys
import tempfile
from pathlib import Path

EVENT_NAMES = {
    "Click", "TextChanged", "SelectionChanged", "ValueChanged", "DoubleTapped",
    "Tapped", "RightTapped", "Checked", "Unchecked", "Toggled",
    "PointerPressed", "PointerReleased", "PointerMoved", "PointerWheelChanged",
    "PointerEntered", "PointerExited", "Loaded", "KeyDown", "KeyUp",
    "GotFocus", "LostFocus", "Drop", "DragOver", "TextInput",
    "AttachedToVisualTree", "DetachedFromVisualTree",
}

TAG_RE = re.compile(r"<([A-Za-z][\w:.-]*)((?:\"[^\"]*\"|'[^']*'|[^<>\"'])*?)/?>", re.S)
XNAME_RE = re.compile(r"\bx:Name\s*=\s*\"([^\"]+)\"")
EVENT_ATTR_RE = re.compile(r"\b([A-Za-z_][\w.]*)\s*=\s*\"([^\"]*)\"")
CS_WIRE_RE = re.compile(r"\b([A-Za-z_]\w*)\.([A-Za-z_]\w*)\s*\+=\s*([A-Za-z_]\w*|\()")


def xaml_events(path: Path) -> dict[tuple[str, str], tuple[int, str]]:
    """{(x:Name, EventName): (line, handler)}"""
    text = path.read_text(encoding="utf-8", errors="replace")
    found: dict[tuple[str, str], tuple[int, str]] = {}
    for m in TAG_RE.finditer(text):
        attrs = m.group(2)
        name_m = XNAME_RE.search(attrs)
        if not name_m:
            continue
        name = name_m.group(1)
        for am in EVENT_ATTR_RE.finditer(attrs):
            leaf = am.group(1).split(".")[-1]
            if leaf in EVENT_NAMES:
                line = text.count("\n", 0, m.start()) + 1
                found.setdefault((name, leaf), (line, am.group(2)))
    return found


def cs_wirings(path: Path) -> list[tuple[str, str, str, int]]:
    """[(control, event, handler, line)]；handler 为 `(`/`async` 表示 lambda"""
    text = path.read_text(encoding="utf-8", errors="replace")
    out = []
    for m in CS_WIRE_RE.finditer(text):
        line = text.count("\n", 0, m.start()) + 1
        out.append((m.group(1), m.group(2), m.group(3), line))
    return out


def scan(root: Path) -> tuple[list[dict], int]:
    """返回 (重复项, 扫过的 .axaml 个数)。"""
    dupes: list[dict] = []
    scanned = 0
    for axaml in sorted((root / "src").rglob("*.axaml")):
        cs = axaml.with_suffix(".axaml.cs")
        if not cs.exists():
            continue
        declared = xaml_events(axaml)
        if not declared:
            continue
        scanned += 1
        for ctrl, evt, handler, cs_line in cs_wirings(cs):
            if (ctrl, evt) in declared:
                xl, xh = declared[(ctrl, evt)]
                dupes.append({
                    "axaml": str(axaml.relative_to(root)),
                    "xaml_line": xl,
                    "xaml_handler": xh,
                    "cs": str(cs.relative_to(root)),
                    "cs_line": cs_line,
                    "cs_handler": handler,
                    "control": ctrl,
                    "event": evt,
                })
    return dupes, scanned


AXAML_ON = ('<UserControl>\n'
            '  <Button x:Name="B" Click="H" />\n'
            '</UserControl>\n')
AXAML_OFF = ('<UserControl>\n'
             '  <Button x:Name="B" />\n'
             '</UserControl>\n')
CS_ON = 'internal partial class A { void M() { B.Click += H; } }\n'
CS_OFF = 'internal partial class A { void M() { } }\n'


def selftest() -> int:
    """三档：全接 ⇒ 1 条；只 XAML / 只 cs ⇒ 0 条。

    只做"全接 ⇒ 1 条"是**半**个负控 —— 若脚本其实只读了 cs 侧（或只读了 XAML 侧），
    后两档会给出同一个答案，而我们看不出来。三档齐了才证明两侧都真被读取。
    """
    cases = [
        ("全接（XAML 声明 + cs +=）", AXAML_ON, CS_ON, 1),
        ("只 XAML 声明", AXAML_ON, CS_OFF, 0),
        ("只 cs +=（合法形态）", AXAML_OFF, CS_ON, 0),
    ]
    failed = 0
    with tempfile.TemporaryDirectory(prefix="xamldup-selftest-") as tmp:
        root = Path(tmp)
        for label, ax, csb, want in cases:
            src = root / "src"
            if src.exists():
                for f in src.iterdir():
                    f.unlink()
            else:
                src.mkdir(parents=True)
            (src / "A.axaml").write_text(ax, encoding="utf-8")
            (src / "A.axaml.cs").write_text(csb, encoding="utf-8")
            got, _ = scan(root)
            mark = "✅" if len(got) == want else "❌"
            if len(got) != want:
                failed += 1
            print(f"  {mark} {label:<28} 期望 {want} 条，实际 {len(got)} 条")
    print("✅ 自检通过：判据的两侧都被真实读取" if failed == 0
          else f"❌ 自检失败 {failed} 档 —— 判据可能只看了一侧（假门禁）")
    return 1 if failed else 0


# 扫描面下限（阶段 121）：`scanned` = 含事件声明的 .axaml 个数。
# 事件属性正则一旦失效，scanned 会塌成 0 ⇒ "无重复接线" 是**假绿**。
MIN_SCANNED_AXAML = 30   # 实测 40


def main() -> int:
    root = Path(__file__).resolve().parent.parent
    if "--selftest" in sys.argv:
        return selftest()

    dupes, scanned = scan(root)

    # fail-closed 放在 --json 之前：两个出口都要受同一条下限保护
    if scanned < MIN_SCANNED_AXAML:
        print(f"❌ 只扫到 {scanned} 个有事件声明的 .axaml（下限 {MIN_SCANNED_AXAML}）——"
              f" 事件属性解析可能失效，本判据会退化成永远通过")
        return 1

    if "--json" in sys.argv:
        print(json.dumps(dupes, ensure_ascii=False, indent=2))
        return 0

    if not dupes:
        print(f"✅ 无重复接线（扫了 {scanned} 个有事件声明的 .axaml）")
        return 0

    files = sorted({d["axaml"] for d in dupes})
    print(f"❌ 发现 {len(dupes)} 处重复接线，分布在 {len(files)} 个文件"
          f"（扫了 {scanned} 个有事件声明的 .axaml）\n")
    last = None
    for d in sorted(dupes, key=lambda x: (x["axaml"], x["xaml_line"])):
        if d["axaml"] != last:
            last = d["axaml"]
            print(f"{d['axaml']}")
        if d["cs_handler"] in {"(", "async"}:
            same = "lambda"
        elif d["xaml_handler"] == d["cs_handler"]:
            same = "同名"
        else:
            same = "⚠异名"
        print(f"    XAML:{d['xaml_line']:<5} {d['control']}.{d['event']:<18}"
              f" {d['xaml_handler']}")
        print(f"    CS  :{d['cs_line']:<5} {'':<22} {d['cs_handler']}   [{same}]")
    print()
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
