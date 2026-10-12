#!/usr/bin/env python3
"""守卫：宿主要消费面板视图的事件，**只能经 DockLayoutState 总线**，不得直订视图实例。

判据（缺陷 #46 的静态面）：面板视图在自己的构造函数里把内部事件转发到静态总线
`DockLayoutState`（如 `FaceplateLibraryView.axaml.cs:65-69`）。宿主若**同时**对视图实例
再 `+=` 一次，同一个处理逻辑就被接了两路 —— CLR/Avalonia 的 `+=` 都不去重，两路都会跑：

    实测读数（`DockPanelInvocationUiTests`）：`BtnAiTemplate` 弹窗数 **+4 → +2 → +1**，
    最外层那个 ×2 就是本判据守的这一路（`docs/2026-09-17-open-items-register.md` #46）。

为什么**留总线、删直订**（而不是反过来）：直订订的是**构造 MainWindow 那一刻**的视图实例，
Dock 重建换实例后即失效；视图内部转发随**新实例的构造**自动重建，能自愈。

「接收者是不是面板视图」怎么判 —— **从声明推导，不猜名字后缀**：
    `MainWindow.axaml.cs:105-117` 里 10 个面板成员长这样
        private FaceplateLibraryView FaceplatePanel => GetActiveView<FaceplateLibraryView>("Faceplate") ?? _faceplatePanel!;
    接收者名出现在「类型名以 `View` 结尾」的声明里，才算面板/视图成员。
    首版用「标识符以 `Panel` 结尾」这个约定，实测**误报 4 处**：
    `NumLeftPanel/NumRightPanel/NumBottomPanel`（SettingsDialog 的 `NumericUpDown`）与
    `ChartPanel`（TrendConfigView 的 `TrendChartPanel`）—— 名字碰巧沾了 Panel。
    按声明推导仍然自维护：新增 `FooView FooPanel` 自动进入守备范围。

⚠ **单向判据**：只判"宿主不得直订面板视图事件"。
   反过来（视图转发到总线）正是要保留的那一路，不能反判。
   同理，宿主订阅 `DockLayoutState.*` 一律合法。
⚠ **fail-closed**：推导出的成员集合若小于 `MIN_VIEW_MEMBERS`，判失败 —— 否则声明格式一变、
   正则失效，判据就退化成"永远 0 违规"的假门禁（本仓库 [1] 步踩过"清单为空 ⇒ 永远通过"）。

扫描范围：`src/HmiEditor.App/**/*.cs`（默认全 App，不只 Wiring.*，
   因为直订可以写进任何一个 partial 或对话框）。

用法：
    python3 scripts/check-dock-bus-only-subscription.py             # 判据（门禁用）
    python3 scripts/check-dock-bus-only-subscription.py --json      # 机器可读
    python3 scripts/check-dock-bus-only-subscription.py --selftest  # 自检：8 档
"""
from __future__ import annotations

import json
import re
import sys
import tempfile
from pathlib import Path

# 接收链：`Identifier(.Identifier)*` 紧跟 `+=`。
# 链形式是必要的 —— `CanvasPanel.Canvas.SelectionChanged +=` 有三段（面板 → 内层画布 → 事件），
# 只匹配 `A.B +=` 会漏掉它。
# **不锚行首**：真实仓库里 `+=` 都单独成行，但行内写法（`if (ok) FaceplatePanel.X += H;`）
# 同样是"接了一路"，锚住行首会漏。误报由"接收者必须是从声明推导出的视图成员"兜住。
SUB_RE = re.compile(r"\b([A-Za-z_]\w*)((?:\.[A-Za-z_]\w*)*)[ \t]*\+=")

# 视图/面板成员的声明：`<类型名以 View 结尾> <成员名>` 后面跟 `=>` / `;` / `{` / `=`。
# 尾随字符的限制把参数（`,)`）、泛型实参（`>`）排除掉。
VIEW_MEMBER_RE = re.compile(r"\b([A-Z]\w*View)\s+([A-Za-z_]\w*)\s*(?:=>|[;={])")

# 第二条规则：`GetActiveView<...>("...")!.Xxx += ...` —— 接收者不是成员名，上一条看不出来。
# 当前基线 0 条（宿主取活动视图都是拿去读/调方法，不订阅）。
GET_ACTIVE_RE = re.compile(r"GetActiveView[^\n]*\+=")

# 推导出的成员数下限（实测 10 个）。低于此值说明声明解析失效 ⇒ 判红，别让它变成假门禁。
MIN_VIEW_MEMBERS = 8

# ── 白名单：合法且**唯一**的单路订阅 ──────────────────────────────────
# 每条都必须能回答"这个事件为什么没有总线那一路"。视图确实没转发它，才轮得到宿主持有。
ALLOW: dict[tuple[str, str], str] = {
    ("AiChatPanel", "ConfigChanged"):
        "AiChatPanelView 只转发 Preview/Apply/TemplateApply/ScriptApply/Cancel 五个"
        "（AiChatPanelView.axaml.cs:77-81），**没有**转发 ConfigChanged ⇒ 全仓唯一的单路事件",
}


def strip_noise(text: str) -> str:
    """把**注释**与**字符串/字符字面量的内容**替换成等长空白（保留行列位置）。

    必须先剥注释：判据的 own doc、以及解释"为什么不能再 += 一次"的注释里，
    都会**原样引用**那些被禁掉的写法 —— 不剥就会把自己的说明判红。

    字符串也要抹掉：判据**不锚行首**（行内写法同样是"接了一路"），
    于是 `var s = "FaceplatePanel.X += Y";` 里那一段会被当成真接线 —— 实测误报过。
    """
    out: list[str] = []
    i, n = 0, len(text)
    state = ""  # "" | line | block | str | char | verbatim
    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if state == "":
            if c == "/" and nxt == "/":
                state, out = "line", out + [" ", " "]
                i += 2
                continue
            if c == "/" and nxt == "*":
                state, out = "block", out + [" ", " "]
                i += 2
                continue
            if c == "@" and nxt == '"':
                state, out = "verbatim", out + [c, nxt]
                i += 2
                continue
            if c == '"':
                state = "str"
            elif c == "'":
                state = "char"
            out.append(c)
            i += 1
            continue
        if state == "line":
            if c == "\n":
                state = ""
                out.append(c)
            else:
                out.append(" ")
            i += 1
            continue
        if state == "block":
            if c == "*" and nxt == "/":
                state = ""
                out += [" ", " "]
                i += 2
                continue
            out.append("\n" if c == "\n" else " ")
            i += 1
            continue
        if state == "verbatim":
            if c == '"' and nxt == '"':      # 逐字字符串里的 "" 是转义
                out += [" ", " "]
                i += 2
                continue
            if c == '"':
                state = ""
            out.append("\n" if c == "\n" else " ")
            i += 1
            continue
        # str / char：内容与结尾引号都抹成空白
        if c == "\\":
            out += [" ", " "]
            i += 2
            continue
        if (state == "str" and c == '"') or (state == "char" and c == "'"):
            state = ""
        out.append("\n" if c == "\n" else " ")
        i += 1
    return "".join(out)


def cs_files(root: Path) -> list[Path]:
    app = root / "src" / "HmiEditor.App"
    return [p for p in sorted(app.rglob("*.cs"))
            if "/bin/" not in p.as_posix() and "/obj/" not in p.as_posix()]


def sources(root: Path) -> list[tuple[str, str]]:
    """[(相对路径, 已剥注释的正文)]"""
    out = []
    for p in cs_files(root):
        out.append((p.relative_to(root).as_posix(),
                    strip_noise(p.read_text(encoding="utf-8", errors="replace"))))
    return out


def view_members(files: list[tuple[str, str]]) -> dict[str, str]:
    """{成员名: 类型名} —— 只收类型名以 `View` 结尾的声明。"""
    members: dict[str, str] = {}
    for _, text in files:
        for m in VIEW_MEMBER_RE.finditer(text):
            members.setdefault(m.group(2), m.group(1))
    return members


def scan(root: Path) -> tuple[list[dict], int, list[tuple[str, str]], dict[str, str]]:
    """返回 (违规项, 扫过的 .cs 个数, 命中的白名单项, 推导出的视图成员)。"""
    files = sources(root)
    members = view_members(files)
    bad: list[dict] = []
    used_allow: list[tuple[str, str]] = []

    for rel, text in files:
        for m in SUB_RE.finditer(text):
            receiver, chain = m.group(1), m.group(2)
            if receiver not in members:
                continue
            event = chain.rsplit(".", 1)[-1] if chain else ""
            line = text.count("\n", 0, m.start()) + 1
            if (receiver, event) in ALLOW:
                used_allow.append((receiver, event))
                continue
            bad.append({"file": rel, "line": line, "receiver": receiver, "event": event,
                        "path": f"{receiver}{chain}", "member_type": members[receiver],
                        "text": text.splitlines()[line - 1].strip(),
                        "rule": "宿主直订面板视图事件"})
        for m in GET_ACTIVE_RE.finditer(text):
            line = text.count("\n", 0, m.start()) + 1
            bad.append({"file": rel, "line": line, "receiver": "GetActiveView", "event": "",
                        "path": "GetActiveView(...)…+=", "member_type": "-",
                        "text": text.splitlines()[line - 1].strip(),
                        "rule": "GetActiveView 结果上直订"})
    return bad, len(files), used_allow, members


# ── 自检用的最小树 ────────────────────────────────────────────────────
_PANEL_DECL = ('    private FaceplateLibraryView FaceplatePanel'
               ' => GetActiveView<FaceplateLibraryView>("Faceplate") ?? _faceplatePanel!;\n')
_CANVAS_DECL = ('    private DesignCanvasView CanvasPanel'
                ' => GetActiveView<DesignCanvasView>("Canvas") ?? _canvasPanel!;\n')
_AICHAT_DECL = ('    private AiChatPanelView AiChatPanel'
                ' => GetActiveView<AiChatPanelView>("AiChat") ?? _aiChatPanel!;\n')

CS_DIRECT = ('internal partial class M {\n' + _PANEL_DECL +
             '    void W() { FaceplatePanel.AiTemplateRequested += (_, _) => Open(); }\n}\n')
CS_NESTED = ('internal partial class M {\n' + _CANVAS_DECL +
             '    void W() { CanvasPanel.Canvas.SelectionChanged += OnSel; }\n}\n')
CS_BUS_ONLY = ('internal partial class M {\n' + _PANEL_DECL +
               '    void W() {\n'
               '        DockLayoutState.FaceplateAiTemplateRequested += (_, _) => Open();\n'
               '        _undoRedoManager.StateChanged += (_, _) => Upd();\n'
               '    }\n}\n')
CS_VIEW_FORWARD = ('internal partial class V { public V() {\n'
                   '    ToolItemSelected += _forwardToolSelected;\n'
                   '} }\n')
CS_ALLOWED = ('internal partial class M {\n' + _AICHAT_DECL +
              '    void W() { AiChatPanel.ConfigChanged += (_, _) => Sync(); }\n}\n')
CS_GETACTIVE = ('internal partial class M { void W() {\n'
                '    GetActiveView<FaceplateLibraryView>("Faceplate")!.AiTemplateRequested += H;\n'
                '} }\n')
# 同名后缀的非视图控件 —— 真实仓库里误报过的四行（SettingsDialog 的数值框 / TrendConfigView 的图表面板）。
CS_NOT_A_VIEW = ('internal partial class M {\n'
                 '    private NumericUpDown NumLeftPanel;\n'
                 '    private TrendChartPanel ChartPanel;\n'
                 '    void W() {\n'
                 '        NumLeftPanel.ValueChanged += (_, _) => SetLeft();\n'
                 '        ChartPanel.PointerPressed += (_, _) => { };\n'
                 '    }\n}\n')
# 注释/字符串里出现被禁写法 —— 真实仓库里到处都是（Wiring.* 的解释性注释），不该算违规。
CS_COMMENTED = ('internal partial class M {\n' + _PANEL_DECL +
                '    void W() {\n'
                '        // 反例：FaceplatePanel.AiTemplateRequested += (_, _) => Open();\n'
                '        /* CanvasPanel.Canvas.SelectionChanged += OnSel; */\n'
                '        var s = "FaceplatePanel.X += Y";\n'
                '    }\n}\n')


def selftest() -> int:
    """8 档：3 档"该红" + 5 档"该绿"（合法形态与两类误报）。

    只做"直订 ⇒ 1 条"是**半**个负控：若脚本因为任何原因（正则写宽/写窄、
    声明解析失效）恒返回某个值，单档看不出来。
    第 1 档同时是"两侧都真被读取"的证明：它要红，必须**声明解析**与**订阅解析**都工作。
    """
    cases = [
        ("面板直订（该红）", CS_DIRECT, 1),
        ("三点链 CanvasPanel.Canvas.X（该红）", CS_NESTED, 1),
        ("GetActiveView 上直订（该红）", CS_GETACTIVE, 1),
        ("只订总线 + 非视图接收者（该绿）", CS_BUS_ONLY, 0),
        ("视图自己的转发（该绿）", CS_VIEW_FORWARD, 0),
        ("白名单单路 AiChatPanel.ConfigChanged（该绿）", CS_ALLOWED, 0),
        ("名字带 Panel 的非视图控件（该绿）", CS_NOT_A_VIEW, 0),
        ("注释/字符串里的反例（该绿）", CS_COMMENTED, 0),
    ]
    failed = 0
    with tempfile.TemporaryDirectory(prefix="dockbus-selftest-") as tmp:
        root = Path(tmp)
        src = root / "src" / "HmiEditor.App"
        src.mkdir(parents=True)
        for label, code, want in cases:
            f = src / "A.cs"
            f.write_text(code, encoding="utf-8")
            bad, _, _, _ = scan(root)
            ok = len(bad) == want
            failed += 0 if ok else 1
            print(f"  {'✅' if ok else '❌'} {label:<44} 期望 {want} 条，实际 {len(bad)} 条")
            f.unlink()
    print("✅ 自检通过：该红的红、该绿的绿（含白名单、剥噪声、非视图同缀三档）" if failed == 0
          else f"❌ 自检失败 {failed} 档 —— 判据会误报或漏报（假门禁）")
    return 1 if failed else 0


def main() -> int:
    root = Path(__file__).resolve().parent.parent
    if "--selftest" in sys.argv:
        return selftest()

    bad, scanned, used, members = scan(root)

    # fail-closed：声明解析失效会让判据恒绿，比漏报更危险。
    # ⚠ 阶段 121：这条检查原先**只在下游（非 --json）分支**，于是 `--json` 出口绕开了它 ——
    #   量具的机器可读出口同样不许"空集 ⇒ 绿"。现在放在分支之前，两个出口共用。
    if len(members) < MIN_VIEW_MEMBERS:
        print(f"❌ 只推导出 {len(members)} 个视图成员（下限 {MIN_VIEW_MEMBERS}，实测 10）——"
              f" 声明解析可能失效，本判据会退化成永远通过")
        print(f"   推导到的成员：{', '.join(sorted(members)) or '（空）'}")
        return 1

    if "--json" in sys.argv:
        print(json.dumps({"violations": bad, "view_members": members,
                          "allow": [f"{r}.{e}" for r, e in used]}, ensure_ascii=False, indent=2))
        return 1 if bad else 0

    allow_txt = "、".join(f"{r}.{e}" for r, e in sorted(used)) or "（无命中）"
    if not bad:
        print(f"✅ 面板事件只经总线消费（扫了 {scanned} 个 .cs；"
              f"识别 {len(members)} 个视图成员；白名单命中：{allow_txt}）")
        for (r, e), why in ALLOW.items():
            print(f"     白名单 {r}.{e} —— {why}")
        return 0

    print(f"❌ 发现 {len(bad)} 处「宿主直订面板视图事件」（扫了 {scanned} 个 .cs）\n")
    for d in sorted(bad, key=lambda x: (x["file"], x["line"])):
        print(f"  {d['file']}:{d['line']}  [{d['rule']}]  {d['path']}"
              + (f"（{d['member_type']}）" if d["member_type"] != "-" else ""))
        print(f"      {d['text']}")
    print()
    print("面板视图已把该事件转发到静态总线 DockLayoutState（构造时转发、随新实例自愈）。")
    print("宿主再加一次直订 ⇒ 同一处理逻辑跑两遍（缺陷 #46）。")
    print("要么删掉这行直订，要么把白名单补进本脚本并写明它为什么是单路。")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
