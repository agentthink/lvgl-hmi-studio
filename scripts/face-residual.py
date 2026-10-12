#!/usr/bin/env python3
"""五层动作面的**残余探针** —— 只读，不判定、不写文件。

用途：收口「下一刀该扩哪儿」时，先把**面**和**残余**量出来（阶段 109–118 一直用它选题）。

口径：
    面   = `src/HmiEditor.App/**/*.axaml` 里带 `x:Name` 的动作控件
           （Button / ToggleButton / CheckBox / RadioButton / RepeatButton / SplitButton），
           排除 `obj/` / `bin/`；
    残余 = 面里那些名字在**面判据源码**中一次都没出现过的格；
    「出现过」= 判据源码里含 `"<名字>"` 字面量
           ⇒ 这是**名字级**口径（跨视图重名会一起消失），不是「控件级」。
    ⚠ 语料是**五层守卫 ＋ 专题判据**（阶段 250 起把 `TableCellEditorDialogUiTests.cs`、阶段 252 起
      把 `ToolboxSearchClearUiTests.cs`（阶段 252）、`AiIconDialogButtonUiTests.cs`（阶段 253）、
      `VariableImportViewUiTests.cs`（阶段 254）、`ValidationResultViewUiTests.cs`（阶段 255）与
      `ReportColumnDialogUiTests.cs`（阶段 256）、`LoginDialogUiTests.cs`（阶段 257）与
      `FontSubsetViewUiTests.cs`/`FaceplateTemplateEditorViewUiTests.cs`（阶段 258）与
      `BuildRunDialogUiTests.cs`（阶段 259）也算进来 ——
      它们都不是"能进某一层的宿主"（一个不是菜单打开的窗、一个只有 1 个按钮进不了第五层的"每宿主 ≥3 条"
      门槛、一个是被停靠面板按钮打开的对话框），但它们确实各自是一张行为面；
      不收它们，探针就会把已经判过的名字继续报成残余）。

⚠ **两种「面外」不是同一个口径，别混着引**（阶段 119 实测，数字为 2026-10-01 阶段 250 重数）：
    本探针是**按钮类 · 名字级**：面 **329 格 / 254 个名字 / 45 个视图**，残余 **10 格 / 10 个名字 / 1 个视图**；
    而 `tests/HmiEditor.App.UiTests/SelectionFaceClassificationGuardTests.cs` 里那张
    `OutsideFaceCensus` 是**选择类 · 接线控件级**：**1 个文件 / 31 个接线控件**。
    两边**曾经**出现过 `63` 这个**巧合同数**（阶段 119 时，分子根本不同：一个数「名字从没被任何一层提过的按钮格」，
    一个数「有 `SelectionChanged` 类接线却没被分类的选择控件」）—— 数字会各自漂，**别按记忆引**。
    ⚠ 这正是本仓吃过的那类错（门禁 `7 步` 与固件 host `9 步` 同列在日志尾部相邻三行）
    ⇒ **引数字把来源工具名一起写上**。⚠ 另：一个判据的收入会同时清掉**别的视图**里同名的格
    （阶段 253 实测：收录 `AiIconDialogButtonUiTests.cs` 后残余 31→23 格，其中 7 格是 `AiIconDialog` 自己的、
    另外几格是别处同名的 `CancelBtn` / `GenerateBtn` / `BtnTitleClose`）—— 所以"补一个判据掉几格"**不等于**"那个视图有几格"。

⚠ **「残余个数」不是「覆盖缺口的大小」**（阶段 118 实测）：本探针里 `PropertyPanelView`
    残余 10 / 面 15，看着像下一刀；但它的数据面已由 `PropertyPanelWiringTests` 判过、且写入是
    **批量**的（与本层「未声明分量一字不变」正面冲突）⇒ 扩它是重复记账。
    **账本的排序口径 ≠ 覆盖缺口的大小**（把 `PropertyPanelView` 记成 31 的是**上面那张选择类普查表**，
    不是本探针）。本探针只回答「这里还有几个没提过的名字」，**不回答**「该不该扩」。

⚠ **fail-closed**：五层守卫文件必须都在、且拼起来的文本非空。缺文件直接判失败 ——
    否则守卫改名 / 搬走后，残余会**全量报出来**（看着像「覆盖率崩了」，其实是探针读错了）。
    与本书其他检查同源：**先确认你在读的东西真的是你要的那个信号**。

用法：
    python3 scripts/face-residual.py                # 全局汇总 ＋ 残余最多的 20 个视图
    python3 scripts/face-residual.py --top 40
    python3 scripts/face-residual.py --selftest     # 自检：3 档
"""
from __future__ import annotations

import re
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
APP = ROOT / "src" / "HmiEditor.App"
TESTS = ROOT / "tests" / "HmiEditor.App.UiTests"

# 五层各一个守卫文件 —— 「提过」是**五层合起来**算的（阶段 109 起就是这个口径）
GUARDS = [
    "MainWindowMenuInvocationUiTests.cs",       # 第一层 菜单
    "MainWindowToolbarInvocationUiTests.cs",    # 第二层 工具条
    "DialogInnerInvocationUiTests.cs",          # 第三层 对话框内
    "DockPanelInvocationUiTests.cs",            # 第四层 停靠面板
    "HostButtonEffectUiTests.cs",               # 第五层 宿主效果
    "TableCellEditorDialogUiTests.cs",          # 专题对话框判据（阶段 250 起：它进不了第三层，但确实是行为面）
    "ToolboxSearchClearUiTests.cs",             # 专题面板判据（阶段 252 起：只有 1 个按钮，进不了第五层的 ≥3 条门槛）
    "AiIconDialogButtonUiTests.cs",             # 专题对话框判据（阶段 253 起：被停靠面板按钮打开的窗，第三层够不到）
    "VariableImportViewUiTests.cs",             # 专题对话框判据（阶段 254 起：由项目树配置节点打开，第三层同样够不到）
    "ValidationResultViewUiTests.cs",           # 专题对话框判据（阶段 255 起：由"生成→验证"链打开，第三层同样够不到）
    "ReportColumnDialogUiTests.cs",             # 专题对话框判据（阶段 256 起：由报表导出链弹的第二个模态窗）
    "LoginDialogUiTests.cs",                    # 专题对话框判据（阶段 257 起：应用启动链的登录窗）
    "FontSubsetViewUiTests.cs",                 # 专题对话框判据（阶段 258 起：生成链的字体子集窗）
    "FaceplateTemplateEditorViewUiTests.cs",    # 专题对话框判据（阶段 258 起：模板库的模板编辑器）
    "BuildRunDialogUiTests.cs",                 # 专题对话框判据（阶段 259 起：编译并运行窗）
]

FACE = {"Button", "ToggleButton", "CheckBox", "RadioButton", "RepeatButton", "SplitButton"}
TAG = re.compile(r'<(?:(?P<pre>[A-Za-z0-9_.]+):)?(?P<type>[A-Za-z0-9_]+)(?P<rest>[^>]*?)/?>')
NAME = re.compile(r'x:Name\s*=\s*"([^"]+)"')


def named_face(axaml: Path) -> list:
    """一个 .axaml 里带 `x:Name` 的动作控件（按出现序）。"""
    out = []
    for m in TAG.finditer(axaml.read_text(encoding="utf-8")):
        if m.group("type") not in FACE:
            continue
        nm = NAME.search(m.group("rest"))
        if nm:
            out.append((m.group("type"), nm.group(1)))
    return out


def scan(app_dir: Path, guard_text: str) -> dict:
    """核心口径（单独抽出来是为了能拿合成树做自检）。"""
    views = {}
    for ax in sorted(app_dir.rglob("*.axaml")):
        s = ax.as_posix()
        if "/obj/" in s or "/bin/" in s:
            continue
        got = named_face(ax)
        if got:
            views[ax.relative_to(app_dir).as_posix()] = got

    cells = [(v, ty, nm) for v, got in views.items() for ty, nm in got]
    names = {nm for _, _, nm in cells}
    residual = {v: [(ty, nm) for ty, nm in got if f'"{nm}"' not in guard_text]
                for v, got in views.items()}
    resid_names = sorted({nm for got in residual.values() for _, nm in got})
    return {
        "views": views, "cells": len(cells), "names": len(names),
        "residual": residual,
        "resid_cells": sum(len(g) for g in residual.values()),
        "resid_names": resid_names,
        "resid_views": [v for v, got in residual.items() if got],
    }


def load_guards() -> str:
    chunks = []
    for g in GUARDS:
        p = TESTS / g
        if not p.is_file():
            raise SystemExit("❌ 守卫文件不在：%s\n"
                             "   （残余会因此**全量**报出来 —— 这是读错了，不是覆盖率崩了）" % p)
        chunks.append(p.read_text(encoding="utf-8"))
    text = "\n".join(chunks)
    if len(text) < 10000:
        raise SystemExit("❌ 五层守卫拼起来只有 %d 字符 ⇒ 明显不对，拒绝出读数" % len(text))
    return text


def main(argv: list) -> int:
    top = 20
    if "--top" in argv:
        top = int(argv[argv.index("--top") + 1])

    r = scan(APP, load_guards())
    covered = r["names"] - len(r["resid_names"])
    print("面：%d 格 / %d 个不同名字 / %d 个有动作控件的视图"
          % (r["cells"], r["names"], len(r["views"])))
    print("残余：%d 格 / %d 个不同名字 / %d 个视图"
          % (r["resid_cells"], len(r["resid_names"]), len(r["resid_views"])))
    print("覆盖（名字级）：%d / %d = %.1f%%"
          % (covered, r["names"], covered / r["names"] * 100))
    print()
    print("=== 残余最多的 %d 个视图（⚠ 个数 ≠ 该不该扩，见文件头）===" % top)
    for v, got in sorted(r["residual"].items(), key=lambda kv: -len(kv[1]))[:top]:
        if not got:
            continue
        print("  %-52s 残余 %2d / 面 %2d    %s"
              % (v, len(got), len(r["views"][v]), "  ".join(nm for _, nm in got)))
    return 0


# ────────────────────────── 自检（3 档） ──────────────────────────
def selftest() -> int:
    fails = []
    with tempfile.TemporaryDirectory(prefix="face-residual-selftest-") as tmp:
        app = Path(tmp)
        (app / "sub").mkdir()

        def write(rel: str, body: str) -> None:
            (app / rel).write_text(body, encoding="utf-8")

        write("AView.axaml", """
<Window xmlns="x">
  <StackPanel>
    <Button x:Name="BtnAlpha" Content="a"/>
    <ToggleButton x:Name="ToggleBeta"/>
    <Button x:Name="BtnGamma"/>
    <TextBlock x:Name="NotAFace"/>
  </StackPanel>
</Window>""")
        # 跨视图重名：BtnAlpha 在这里又出现一次 ⇒ 名字级口径下**一起**算「提过」
        write("sub/BView.axaml", """
<UserControl xmlns="x">
  <Button x:Name="BtnAlpha"/>
  <CheckBox x:Name="ChkDelta"/>
</UserControl>""")
        write("sub/CView.axaml", """<UserControl xmlns="x"><TextBlock/></UserControl>""")
        # obj/ 下的必须被排除
        (app / "obj").mkdir()
        write("obj/DView.axaml", """<UserControl><Button x:Name="BtnInObj"/></UserControl>""")

        # 档 1：面只数带 x:Name 的动作控件；obj/ 排除；无动作控件的视图不进表
        r = scan(app, '"BtnAlpha" "ToggleBeta"')
        if r["cells"] != 5:
            fails.append("档1 面应为 5 格（BtnAlpha×2 / ToggleBeta / BtnGamma / ChkDelta），实为 %d"
                         % r["cells"])
        if "obj/DView.axaml" in r["views"]:
            fails.append("档1 obj/ 下的视图没被排除")
        if "sub/CView.axaml" in r["views"]:
            fails.append("档1 没有动作控件的视图不该进表")
        if r["names"] != 4:
            fails.append("档1 不同名字应为 4，实为 %d" % r["names"])

        # 档 2：**名字级**口径 —— 提过 BtnAlpha 就该让它**在所有视图里**一起消失
        resid = {nm for got in r["residual"].values() for _, nm in got}
        if "BtnAlpha" in resid:
            fails.append("档2 BtnAlpha 在守卫里出现过，不该是残余")
        if resid != {"BtnGamma", "ChkDelta"}:
            fails.append("档2 残余应为 {BtnGamma, ChkDelta}，实为 %s" % sorted(resid))
        if r["resid_views"] != ["AView.axaml", "sub/BView.axaml"]:
            fails.append("档2 有残余的视图应为 AView / sub/BView，实为 %s" % r["resid_views"])

        # 档 3：空守卫文本 ⇒ 残余必须**全量**（这才让 load_guards 的 fail-closed 有意义）
        r0 = scan(app, "")
        if len(r0["resid_names"]) != 4 or r0["resid_cells"] != 5:
            fails.append("档3 空守卫下残余应全量（4 个名字 / 5 格），实为 %d / %d"
                         % (len(r0["resid_names"]), r0["resid_cells"]))

    for x in fails:
        print("❌ %s" % x)
    if fails:
        return 1
    print("✅ face-residual 自检 3 档全过")
    return 0


if __name__ == "__main__":
    if "--selftest" in sys.argv:
        raise SystemExit(selftest())
    raise SystemExit(main(sys.argv[1:]))
