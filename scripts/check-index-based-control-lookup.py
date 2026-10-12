#!/usr/bin/env python3
"""守卫：不得用「位置」在容器的视觉子集合 `.Children` 里定位控件。

判据守的是这一族写法：

    ((Button)p2.Children[2]).Click += (_, _) => dialog.Close();        # F1 下标
    var buttons = panel.Children.OfType<StackPanel>().First().Children; # F2 取「第一个」
    ((Button)buttons[0]).Click += (_, _) => dialog.Close();             # F3 经别名下标
    while (p.Children.Count > 2) p.Children.RemoveAt(2);                # F4 位置写入

**为什么它是缺陷而不是风格问题**（登记表 #57 / 阶段 75 的 LuaHelpDialog、阶段 76 的 GitDialogService）：
`Children[2]` 把「哪个控件」编码成了**位置**，而位置由别人（XAML / 构造顺序 / 后续维护者）决定，
于是往容器里插一个控件就会**静默**改变语义：

    在 index 2 之前插一个**别的控件**   → `((Button)…)` 硬转抛异常（响）
    在 index 2 之前插另一个 **Button**   → Close 挂到**错的按钮**上，真关闭按钮点不动（**静默**）
    `dialog.Content is StackPanel` 失配  → **整段接线被跳过**，两个按钮都点不动（**静默**）
    `.First()` 那一行被挪走/删掉        → `First()` 抛；插在它前面则取到**错的按钮**（静默）
    往「保留前 2 个」的容器加第 3 个静态元素 → 每次刷新被 `RemoveAt(2)` **静默删掉**（静默）

对照的合法写法（**判据刻意不拦**，也是推荐的修法）：
    · 构造时把目标控件**建成局部变量**，接线/操作**直接用实例**；
    · `FindControl<T>("名字")` —— 按**名字**找（名字是契约，位置不是）；
    · 绑定 / `ItemTemplate`（把「更新那个 TextBlock」变成数据驱动）。

⚠ **覆盖范围（写清它看不见什么）**：判**容器的视觉子集合 `.Children` 上的「位置」用法**，
   两半都判 ——
   · **位置读取**：下标（含 `^N`）、`First/Last/ElementAt`、别名（声明式与两步赋值）转手后的下标；
   · **位置写入**（阶段 78 起，形态 **F4**）：`RemoveAt(<字面整数>)` / `Insert(<字面整数>, …)`。
     与下标读是**同一个假设**（「前 N 个是固定保留区」），只是后果从「读错」变成「删错/插错」。
   **刻意不判**（阶段 78 全仓清点过，反判会一片假红）：
     ① 数据集合 `Items.OfType<ListBoxItem>()` —— `ListBoxItem` 是该集合**固有**的容器类型，不是猜；
     ② `Content is X` / `.Child is X` **容器类型导航** —— 清点结果 **3 处，全部是「取
        `ContentControl` 的**唯一**子元素」的类型探针**（`Border.Child` / `Window.Content`
        语义上只有一个子元素 ⇒ 根本不存在"第几个"的问题）⇒ 这一维**不该**硬判；
     ③ 按**运行期类型读取**（`is` / `as` / `OfType<T>`）—— 全仓大量且合法（阶段 77 自己的修法
        就是 `Items.OfType<AnimationRow>()` 按模型身份找行）；
     ④ **按身份找、按位置放回**：`var i = parent.Children.IndexOf(ChartPanel); …RemoveAt(i);
        …Insert(i, x);` —— 位置来自 `IndexOf` 的**返回值**，不是写死的契约（`TrendConfigView` 实写）；
     ⑤ `Children.Count`、`Children.Add(...)`、`Children.Clear()`、对象初始化器 `Children = { a, b }`；
     ⑥ `RemoveAt(<变量>)` / `Insert(<变量>, …)` —— 同 ④，不判。
   ⇒ 真正**没有**判据的是「**变量下标**的删改」（`for (i = Count - 1; i >= 2; i--) RemoveAt(i)`）。
      它是本判据的**已知绕过路径**（与 `BASELINE` 时代「别名只追单层」同族）⇒ 用**行为判据**兜：
      `tests/HmiEditor.App.UiTests/FaceplateParamRefreshUiTests` 往静态区注入一个探针元素、
      驱动一次刷新、断言探针**存活** —— 形状判据看不见的写法，行为判据抓得到（阶段 78 实测）。

⚠ **两级判据（阶段 77 起，两档都硬判为 0）**：
   · **wire（接线类）**：命中所在语句还做了 `+=`/`-=` 或**硬转** `(T)` ⇒ 目标是「给控件挂行为」，
     后果是**功能断线**（用户可见）⇒ **硬判为 0**，不许进豁免表（`WIRE_ALLOWED` 刻意留空）。
   · **read（读取类）**：命中只做 `is`/`as`/取值 ⇒ 后果是**该处文本不刷新**（静默但轻）。
     它**曾经**是基线登记制（阶段 76 登记了 7 处）；阶段 77 把三份视图的列表行改成
     「构造时捕获控件的行类型」之后**清零** ⇒ 现在同样**硬判为 0**，`BASELINE` 空置。
     双向差集机制刻意留着：`BASELINE` 一旦非空，表外新增与表内失效**各自**都会红。

⚠ **基线清空拿掉了一条 fail-closed，必须换承担者**（阶段 77 的关键一步，别省）：
   此前「表非空」顺便是**正则失效的探测器** —— 正则一坏，表内 7 条集体「消失」⇒ 立刻红。
   表清空之后，正则坏掉 ⇒ 0 条命中 ⇒ **绿**（假门禁）。⇒ 正控改由 `meta_positive_control()`
   承担：**每次运行**都在最小临时树上验证**四档**已知坏形态（F1 / F2 / F3 / F4）**真的能被命中**，
   少一档就判红。它比原来那条更强 —— 原来只护 read 类，这条两类都护、且不再依赖表非空。

⚠ **必须先剥注释**（#48 的教训）：解释这段历史的注释里**原样写着**被禁的写法
   （本脚本 docstring、GitDialogService 的说明注释都写着 `Children[2]`）⇒ 不剥注释，
   判据会被**它自己的说明**满足/污染。

用法：
    python3 scripts/check-index-based-control-lookup.py             # 判据（门禁用）
    python3 scripts/check-index-based-control-lookup.py --json      # 机器可读
    python3 scripts/check-index-based-control-lookup.py --dump      # 打印基线表条目（生成/更新基线用）
    python3 scripts/check-index-based-control-lookup.py --selftest  # 自检：14 档
"""
from __future__ import annotations

import json
import re
import sys
import tempfile
from collections import Counter
from pathlib import Path

# ── 三个形态 ────────────────────────────────────────────────────────
# 容器表达式：`Identifier(.Identifier)*`，可为空前缀之外必须有 `.Children`。
_CONTAINER = r"([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\.Children"
# F1：`<容器>.Children[<整数>]` / `[^<整数>]`
F1 = re.compile(_CONTAINER + r"[ \t]*\[[ \t]*(\^?[ \t]*\d+)[ \t]*\]")
# F2：`<容器>.Children.First()/Last()/ElementAt(...)`（含 `OfType<T>().First()`）
#     ⚠ `OfType<T>()` 的空实参表必须显式写出来 —— 首版漏了 `()`，实测漏掉真实仓库里的
#       `panel.Children.OfType<StackPanel>().First()`（阶段 76 踩到）。
F2 = re.compile(_CONTAINER + r"[ \t]*\.[ \t]*(?:OfType\s*<[^>]*>\s*\(\s*\)\s*\.\s*)?"
                           r"(First|Last|ElementAt)\s*\(")
# F4（阶段 78）：**位置写入** —— `<容器>.Children.RemoveAt(<字面整数>)` / `.Insert(<字面整数>,`。
#     `Children[2]` 读 与 `RemoveAt(2)` 写是**同一个假设**（「前 2 个是固定保留区」），
#     而阶段 76 只守了读的那一半 ⇒ 往 XAML 里加第 3 个静态元素就会被静默删掉。
#     ⚠ 只认**字面整数**：`RemoveAt(i)` 是「按身份找完再放回」（见覆盖范围 ④），不判。
F4 = re.compile(_CONTAINER + r"[ \t]*\.[ \t]*(RemoveAt|Insert)[ \t]*\([ \t]*(\d+)[ \t]*(?:,|\))")

# F3：别名 —— 声明式 `var X = …<某表达式>.Children…;`，随后 `X[<整数>]`
ALIAS_DECL = re.compile(r"(?:\bvar\b|[\w<>,\[\]\?\.]+)[ \t]+([a-z_]\w*)[ \t]*=[ \t]*"
                        r"([^;\n]*\.Children[^;\n]*);")
# …以及**两步赋值** `X = …Children…;`（首版只认声明式 —— 实测这是同一个洞的另一种写法：
#     `IList<Control> xs; xs = grid.Children; ((Button)xs[0]).Click += …;` 照样把下标藏起来）。
# 前缀 `^[ \t]*` 让"行首的裸赋值"才认，声明式（`var xs = …`）不会被它二次匹配（`var` 后面不是 `=`）。
ALIAS_ASSIGN = re.compile(r"(?:^[ \t]*|[;{}][ \t]*)([a-z_]\w*)[ \t]*=[ \t]*"
                          r"([^;\n=]*\.Children[^;\n]*);", re.MULTILINE)
IDX_ON = re.compile(r"\b([a-z_]\w*)[ \t]*\[[ \t]*(\^?[ \t]*\d+)[ \t]*\]")

# 接线类的判据：语句里有事件挂接，或对命中结果做**硬转** `(T)`
WIRE_OP = re.compile(r"\+=|-=(?!=)")
HARDCAST = re.compile(r"\(\s*\([A-Z]\w*[ \t]*\)")

# 解析面下限（fail-closed）：实测 src 下 .cs 数量远大于此；塌到 0 说明扫描面坏了
MIN_SCANNED_FILES = 300

# ── 豁免表 ─────────────────────────────────────────────────────────
# wire 类**不许有**条目 —— 这一档的后果是功能断线，只能改代码，不能登记。
# （刻意留空：要豁免必须先改本文件，那一步会强制写理由。见「豁免表不许硬塞」。）
WIRE_ALLOWED: dict[str, str] = {}

# read 类基线：键 `<相对路径>::<容器>.Children[<下标>]`，值 = (类, 理由)。
# 双向判定：键多了红、键少了也红。每条都必须能回答「为什么不能持引用/走模板」。
#
# 这三份视图是**同一形态**（阶段 76 实测）：列表项由 CreateXItem() 手工构造后 Items.Add(...)，
# 且**全仓没有 ItemTemplate**（在三份 .axaml/.axaml.cs 里 grep ItemTemplate 命中 0）
# ⇒ 「编辑后把那一行文本改掉」除了回头去项树里找那个 TextBlock 没有别的入口。
# 失效模式统一是**静默不刷新**（都带 Count>= 与 is/as 守卫 ⇒ 不抛、不接错），
# 后果限于「该行显示停留在旧值」，与 wire 类的**功能断线**不同级 ⇒ 登记而不硬判为 0。
# 消除途径＝列表项改成 ItemTemplate + 绑定（或项对象实现 INPC 后直接绑字段），属独立改造；
# 届时这些条目必须**逐条删掉** —— 双向判定会因「表内条目在代码里消失」强制这件事发生。
BASELINE: dict[str, tuple[str, str]] = {}
# ⬆ **阶段 77 起为空**：三份视图的列表行改成「构造时捕获控件的行类型」
#   （AlarmGroupRow / AnimationRow / RecipeParamRow）之后，原先登记的 7 条全部消失，
#   读取类随之**硬判为 0**。双向差集机制保留 —— 基线一旦非空，表外新增与表内失效各自都会红；
#   但它**不再是**「正则失效」的 fail-closed，那个角色已交给 meta_positive_control()。



def strip_noise(text: str) -> str:
    """把注释与字符串/字符字面量的内容替换成等长空白（保留行列位置）。

    与 scripts/check-dock-bus-only-subscription.py 同源：本判据的 docstring、以及产品代码里
    **解释这段历史**的注释都原样引用着被禁的写法（`Children[2]`、`OfType<…>().First()`），
    不剥就会把自己的说明判红 / 或让说明替真违规做担保。
    """
    out: list[str] = []
    i, n = 0, len(text)
    state = ""
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
            out.append("\n" if c == "\n" else " ")
            if c == "\n":
                state = ""
            i += 1
            continue
        if state == "block":
            if c == "*" and nxt == "/":
                state, out = "", out + [" ", " "]
                i += 2
                continue
            out.append("\n" if c == "\n" else " ")
            i += 1
            continue
        if state == "verbatim":
            if c == '"' and nxt == '"':
                out += [" ", " "]
                i += 2
                continue
            if c == '"':
                state = ""
            out.append("\n" if c == "\n" else " ")
            i += 1
            continue
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
    d = root / "src"
    return [p for p in sorted(d.rglob("*.cs"))
            if "/bin/" not in p.as_posix() and "/obj/" not in p.as_posix()]


def _norm(expr: str) -> str:
    return re.sub(r"[ \t]+", "", expr)


def scan_tree(root: Path, rel_prefix: str = "", patterns: tuple | None = None) -> list[dict]:
    """扫一个源码树，返回全部命中（不判红绿）。

    patterns 只给元判据 / 自检用：把那几个形态换成**被改坏的**正则传进来，
    好验证「扫描器坏了能被发现」。生产路径永远传 None（= 模块级那五个正则）。
    """
    f1, f2, f4, alias_decl, alias_assign, idx_on = patterns or (
        F1, F2, F4, ALIAS_DECL, ALIAS_ASSIGN, IDX_ON)
    hits: list[dict] = []
    for p in cs_files(root):
        rel = rel_prefix + p.relative_to(root).as_posix()
        text = strip_noise(p.read_text(encoding="utf-8", errors="replace"))
        lines = text.splitlines()

        alias_src: dict[str, str] = {}
        for rx in (alias_decl, alias_assign):
            for m in rx.finditer(text):
                alias_src[m.group(1)] = m.group(2).strip()

        def emit(pos: int, form: str, expr: str, extra: str = "") -> None:
            ln = text.count("\n", 0, pos) + 1
            stmt = lines[ln - 1].strip()
            cls = "wire" if (WIRE_OP.search(stmt) or HARDCAST.search(stmt)) else "read"
            hits.append({"file": rel, "line": ln, "form": form, "expr": _norm(expr),
                         "class": cls, "stmt": stmt, "extra": extra,
                         "key": f"{rel}::{_norm(expr)}"})

        for m in f1.finditer(text):
            emit(m.start(), "F1", f"{m.group(1)}.Children[{m.group(2)}]")
        for m in f2.finditer(text):
            emit(m.start(), "F2", f"{m.group(1)}.Children.{m.group(2)}()")
        for m in f4.finditer(text):
            emit(m.start(), "F4", f"{m.group(1)}.Children.{m.group(2)}({m.group(3)}, …)")
        for m in idx_on.finditer(text):
            name = m.group(1)
            if name in alias_src:
                emit(m.start(), "F3", f"{name}[{m.group(2)}]",
                     extra=f"别名 = {alias_src[name]}")
    return hits


# ── 元判据：扫描器「真的能产出命中」的正控（阶段 78 起 4 档：F1/F2/F3/F4） ──
# 为什么必须有：基线清空（阶段 77）顺手拿掉了「表非空」这条 fail-closed ⇒ 正则一坏，
# 0 条命中，判据全绿（**假门禁**）。这条元判据每次运行都当场验证四档已知坏形态能被抓到。
# 它自己也有负控（--selftest 的最后一档：把 F1 换坏 ⇒ 本函数必须报红）。
_META_SHAPES: list[tuple[str, str]] = [
    ("F1", 'class C { void W() { ((Button)p.Children[2]).Click += (_, _) => Close(); } }\n'),
    ("F2", 'class C { void W() { var b = panel.Children.OfType<StackPanel>().First(); } }\n'),
    ("F3", ('class C { void W() {\n    IList<Control> bs = grid.Children;\n'
            '    ((Button)bs[0]).Click += (_, _) => Close();\n} }\n')),
    # 阶段 78：位置**写入**那一半。少了这一档，F4 正则坏掉时元判据会照样全绿。
    ("F4", 'class C { void R() { while (sp.Children.Count > 2) sp.Children.RemoveAt(2); } }\n'),
]


def meta_positive_control(patterns: tuple | None = None) -> list[str]:
    """在最小树上验证**每一档**已知坏形态真的能被命中；返回错误列表（空 = 通过）。"""
    errs: list[str] = []
    with tempfile.TemporaryDirectory(prefix="indexlookup-meta-") as tmp:
        root = Path(tmp)
        (root / "src").mkdir(parents=True)
        sample = root / "src" / "Meta.cs"
        for want_form, code in _META_SHAPES:
            sample.write_text(code, encoding="utf-8")
            forms = sorted({h["form"] for h in scan_tree(root, patterns=patterns)})
            if want_form not in forms:
                errs.append(
                    f"[元判据] 形态 {want_form} 的已知坏样本**没被命中**（实际命中 {forms}）："
                    f"{code.strip()}"
                    f"  ⇒ 扫描器坏了，本判据会退化成**永远通过**")
    return errs


def judge(wire: list[dict], read: list[dict],
          baseline: dict[str, tuple[str, str]]) -> list[str]:
    """返回错误列表；空 = 通过。"""
    errs: list[str] = []

    for h in sorted(wire, key=lambda x: (x["file"], x["line"])):
        if h["key"] in WIRE_ALLOWED:
            continue
        errs.append(f"[wire] {h['file']}:{h['line']}  {h['expr']}\n         {h['stmt']}")

    got = Counter(h["key"] for h in read)
    want = Counter({k: 1 for k in baseline})
    for k, c in sorted((got - want).items()):
        sample = next(h for h in read if h["key"] == k)
        errs.append(f"[read/新增] {k}  ×{c}\n         {sample['stmt']}")
    for k in sorted((want - got).keys()):
        errs.append(f"[read/失效] 基线里有但代码里找不到：{k}"
                    f"（若已改成持引用，请删掉基线这一行）")
    # 同一 key 出现多次（代码重复）也要看得见
    for k in sorted(set(got) & set(want)):
        if got[k] != want[k]:
            errs.append(f"[read/重复] {k} 代码 {got[k]} 处、基线 {want[k]} 处")
    return errs


def report(errs: list[str], scanned: int, wire: int, read: int) -> None:
    if not errs:
        print(f"✅ 无「按位置在 .Children 里定位控件」的写法（位置读取 + 位置写入；"
              f"扫了 {scanned} 个 .cs；接线 0 处、读取 {read} 处；两级都硬判为 0，基线已空置）")
        return
    print(f"❌ 按下标/按第一个元素找控件（扫了 {scanned} 个 .cs；接线 {wire} 处、读取 {read} 处）\n")
    for e in errs:
        print(f"  {e}")
    print()
    print("修法：构造时把目标控件建成**局部变量**（或字段），接线/操作直接用实例；")
    print("     按名字找用 FindControl<T>(\"名字\")；列表项刷新改用 ItemTemplate + 绑定。")
    print("     『读取类』若确认要留，把该条加进本脚本的 BASELINE 并写明理由（双向判定）。")
    print("     『接线类』不许登记 —— 后果是功能静默断线，只能改代码。")


# ── 自检用的最小树 ───────────────────────────────────────────────────
_T_WIRE_F1 = 'class C { void W() { ((Button)p.Children[2]).Click += (_, _) => Close(); } }\n'
_T_WIRE_F3 = ('class C { void W() {\n'
              '    var bs = panel.Children.OfType<StackPanel>().First().Children;\n'
              '    ((Button)bs[1]).Click += (_, _) => Commit();\n'
              '} }\n')
_T_READ = 'class C { void R() { if (sp.Children.Count >= 2 && sp.Children[1] is TextBlock t) t.Text = "x"; } }\n'
_T_WIRE_BARE = ('class C { void W() {\n'
                '    IList<Control> bs;\n'
                '    bs = grid.Children;\n'
                '    ((Button)bs[0]).Click += (_, _) => Close();\n'
                '} }\n')
_T_OK_REF = 'class C { void W() { closeBtn.Click += (_, _) => Close(); } }\n'
_T_OK_NAME = 'class C { void W() { FindControl<Button>("CloseBtn")!.Click += (_, _) => Close(); } }\n'
_T_OK_INIT = 'class C { void B() { var p = new StackPanel { Children = { a, b, c } }; } }\n'
_T_OK_ITEMS = ('class C { void R() { foreach (var i in List.Items.OfType<ListBoxItem>())\n'
               '    { if (i.Content is Grid g) { } } } }\n')
_T_F4 = ('class C { void R() {\n'
         '    while (sp.Children.Count > 2) sp.Children.RemoveAt(2);\n'
         '    sp2.Children.Insert(0, x);\n'
         '} }\n')
# 合法对照：位置来自 `IndexOf` 的返回值（按身份找、按位置放回）⇒ 该绿
_T_OK_BYIDENTITY = ('class C { void R() {\n'
                   '    var i = parent.Children.IndexOf(ChartPanel);\n'
                   '    parent.Children.RemoveAt(i);\n'
                   '    parent.Children.Insert(i, q);\n'
                   '} }\n')
_T_COMMENTED = ('class C { void W() {\n'
                '    // 反例：((Button)p.Children[2]).Click += (_, _) => Close();\n'
                '    /* var bs = panel.Children.OfType<StackPanel>().First().Children; */\n'
                '} }\n')


def selftest() -> int:
    """14 档：7 档该红（3 档 wire 形态 + 读取类「表外新增」+ **F4 位置写入** + 元判据负控
    + 「表内失效」那一档）+ 7 档该绿。

    为什么不是「只做一档 wire ⇒ 1 条违规」：那是**半个**负控 —— 若 judge 因为任何原因
    恒返回空（基线匹配写坏、Counter 算错），单档看不出来。这里两个方向都测：
    「表外新增」与「表内失效」必须**各自**能红，才说明双向判定真的在工作。
    """
    cases: list[tuple[str, str, dict[str, tuple[str, str]], bool]] = [
        ("F1 硬转 + 事件挂接（wire 该红）", _T_WIRE_F1, {}, False),
        ("F2/F3 别名 + 硬转 + 挂接（wire 该红）", _T_WIRE_F3, {}, False),
        ("F3 **两步赋值**别名 + 硬转 + 挂接（wire 该红）", _T_WIRE_BARE, {}, False),
        ("读取类但**不在**基线（该红）", _T_READ, {}, False),
        ("读取类**在**基线（该绿）", _T_READ, {"src/A.cs::sp.Children[1]": ("read", "自检")}, True),
        ("持引用的合法接线（该绿）", _T_OK_REF, {}, True),
        ("按名字 FindControl（该绿）", _T_OK_NAME, {}, True),
        ("对象初始化器 Children = { … }（该绿）", _T_OK_INIT, {}, True),
        ("Items.OfType<ListBoxItem>() + Content is（范围外，该绿）", _T_OK_ITEMS, {}, True),
        ("注释里的反例（剥注释后该绿）", _T_COMMENTED, {}, True),
        ("F4 位置写入：RemoveAt(2)/Insert(0, …)（该红）", _T_F4, {}, False),
        ("按身份 IndexOf → RemoveAt(i)/Insert(i)（范围外，该绿）", _T_OK_BYIDENTITY, {}, True),
    ]
    failed = 0
    with tempfile.TemporaryDirectory(prefix="indexlookup-selftest-") as tmp:
        root = Path(tmp)
        (root / "src").mkdir(parents=True)
        for label, code, baseline, want_green in cases:
            f = root / "src" / "A.cs"
            f.write_text(code, encoding="utf-8")
            hits = scan_tree(root)
            errs = judge([h for h in hits if h["class"] == "wire"],
                         [h for h in hits if h["class"] == "read"], baseline)
            green = not errs
            ok = green == want_green
            failed += 0 if ok else 1
            print(f"  {'✅' if ok else '❌'} {label:<52} 期望{'绿' if want_green else '红'}，"
                  f"实际{'绿' if green else '红'}")
            f.unlink()

    # 第 4 档的**消失方向**：基线里有、代码里没有 ⇒ 必须红
    missing = judge([], [], {"A.cs::gone.Children[0]": ("read", "自检")})
    ok = bool(missing)
    failed += 0 if ok else 1
    print(f"  {'✅' if ok else '❌'} {'基线条目在代码里消失（该红）':<52} 期望红，"
          f"实际{'红' if ok else '绿'}")

    # 元判据自身的**负控**：把 F1 换成一个永不命中的正则 ⇒ 元判据必须报红
    # （没有这一档，元判据自己就可能是一条恒绿的摆设）
    broken = meta_positive_control(patterns=(
        re.compile(r"\bNEVER_MATCHES_ANYTHING\b"), F2, F4, ALIAS_DECL, ALIAS_ASSIGN, IDX_ON))
    ok = bool(broken)
    failed += 0 if ok else 1
    print(f"  {'✅' if ok else '❌'} {'元判据的负控：F1 换坏后它必须报红':<52} 期望红，"
          f"实际{'红' if ok else '绿'}")

    print("✅ 自检通过：两个方向都能红、合法写法与范围边界都绿" if failed == 0
          else f"❌ 自检失败 {failed} 档 —— 判据会误报或漏报（假门禁）")
    return 1 if failed else 0


def main() -> int:
    root = Path(__file__).resolve().parent.parent
    if "--selftest" in sys.argv:
        return selftest()

    if not (root / "src").is_dir():
        print("❌ 找不到 src/ —— 扫描面失效，本判据会退化成永远通过")
        return 1

    hits = scan_tree(root)
    wire = [h for h in hits if h["class"] == "wire"]
    read = [h for h in hits if h["class"] == "read"]
    scanned = len(cs_files(root))

    if "--dump" in sys.argv:
        for h in sorted(read, key=lambda x: (x["file"], x["line"])):
            print(f'    "{h["key"]}": ("read", ""),   # {h["file"]}:{h["line"]}  {h["stmt"]}')
        for h in sorted(wire, key=lambda x: (x["file"], x["line"])):
            print(f'  ⚠ wire: {h["file"]}:{h["line"]}  {h["stmt"]}')
        return 0

    # 元判据先跑：扫描器坏了的话下面的 errs 会是空的（**假绿**），必须先发现
    meta = meta_positive_control()
    errs = meta + judge(wire, read, BASELINE)

    if "--json" in sys.argv:
        print(json.dumps({"scanned": scanned, "wire": wire, "read": read,
                          "errors": errs}, ensure_ascii=False, indent=2))
        return 1 if errs or scanned < MIN_SCANNED_FILES else 0

    if scanned < MIN_SCANNED_FILES:
        print(f"❌ 只扫到 {scanned} 个 .cs（下限 {MIN_SCANNED_FILES}）——"
              f" 扫描面塌了，本判据会退化成永远通过")
        return 1

    report(errs, scanned, len(wire), len(read))
    return 1 if errs else 0


if __name__ == "__main__":
    raise SystemExit(main())
