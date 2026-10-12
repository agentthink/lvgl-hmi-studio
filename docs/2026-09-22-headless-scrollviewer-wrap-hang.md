# headless 下「ScrollViewer + Wrap TextBlock」布局不收敛（阶段 73，2026-09-22）

> 目标：把登记表 §5 **#40** 从「触发条件**未定**」推进到定论，并回答它的另外两条遗留
> （真机是否也挂 / 处置方向）。结论：**触发条件已定论，且是 Avalonia.Headless 的限制、不是产品缺陷**
> —— 产品代码不为此改渲染方式（改 `NoWrap` 这条路被实测否证）。
>
> 全部读数的落盘位置：`.workbuddy/tmp/e73-*.log`、`.workbuddy/tmp/neg73-case*.log`、
> `.workbuddy/tmp/probe73-x11/r*.log`、`.workbuddy/tmp/e73-geom.txt`。

## 1 起点：登记表 #40 的三条遗留

阶段 50 的探针已经把面收窄到「`ScrollViewer` 包 `TextWrapping.Wrap` 的 `TextBlock`」，并排除了三条常见嫌疑
（文本长度 / 字体回退链 / owner 类型）。但登记表自己写着：

| 遗留 | 阶段 50 的状态 |
|---|---|
| ① 有显示服务器 / 真机上是否也挂 | 未验 |
| ② 触发条件 | **未定**。嫌疑收窄到一条**未验证**的机制：水平滚动默认 `Auto` ⇒ 以**无限宽度**测量 ⇒ `Wrap` 的换行宽度定不下来 |
| ③ 处置方向 | 需产品决策 |

阶段 73 把 ①② 都验了，③ 因此有了依据。

## 2 判别实验：一次只改一个变量，**退出码即结论**

工具 `.workbuddy/tmp/e73.py`（每个 case 都从**备份原文**出发、只注入一处、`assert count == 1`）。
判据是外部 `timeout 120` 的退出码：**124 = 挂死**，**0 = 收敛**。

| # | 注入（只改这一处） | 退出码 | 说明 |
|---|---|---|---|
| E0 | 无（原产品代码） | **124**（4m1s） | 今天仍复现 ⇒ 不是失效登记 |
| E1 | `HorizontalScrollBarVisibility = Disabled` | **124** | **否证**「水平无限宽测量」 |
| 2 | 帮助文本换成 `"短文本"` | 0（682 ms） | 一行的内容不挂 |
| 3 | `VerticalScrollBarVisibility = Disabled` | 0（660 ms） | 垂直**不可滚动**就收敛 |
| 4 | `TextWrapping.Wrap → NoWrap` | 0（694 ms） | 不 Wrap 就收敛（内容仍靠 `\n` 分行） |
| 5 | `LineHeight 20 → 20.0`（**等价改写**） | **124** | **负控**：排除「随便改一行就收敛」的伪影 |
| 6 | `ScrollViewer` → `Border` | 0（674 ms） | 兜底判别：没有 ScrollViewer 就不挂 |
| 7 | `VerticalScrollBarVisibility = Hidden` | **124** | 仍可滚动 ⇒ 挂（与滚动条**可见性**无关） |
| 8 | `Height 420 → 900` | **124** | 视口高到 828 也挂 ⇒ 与**是否溢出视口**无关 |

case 5 是这次实验设计的要点：**它不改变任何布局语义**，却照样挂死 ⇒ 上面那些「0」不是"改了文件"带来的。

## 3 前置读数：headless 的窗口尺寸与字体度量

在挂死的路上，有两个量必须先量出来，否则 case 8 无法解读（探针 `ZzProbe73GeoUiTests`，取证后已删）：

```
① Window{Width=480,Height=420} -> Bounds=0,0,480,420   ClientSize=480,420
① Window{Width=480,Height=900} -> Bounds=0,0,480,900   ClientSize=480,900   ← headless 尊重 Window.Height
② 窗 Height=420: ScrollViewer(Disabled) Bounds=448×348  Viewport=448×348
                 TextBlock Bounds=448×348  DesiredSize=273×340
   Button Bounds=384,360,64,28
② 窗 Height=900: ScrollViewer(Disabled) Bounds=448×828  Viewport=448×828
                 TextBlock DesiredSize=273×820
```

两个关键推论（**都是我事先估错的地方**）：

1. **`Window.Height` 是生效的** ⇒ case 8 的 900 不是被 clamp 掉的，视口确实到了 828。
2. 🩸 **帮助文本在 headless 下根本不高**。源文本是 29 行，但 `TextBlock.DesiredSize` 只有 **340px**
   —— 因为 headless 的字体是 fallback，CJK 也被按极窄字形渲染（≈5px/字符）：54 字符的最长行只要
   273px **（< 448 可用宽）** ⇒ `Wrap` 在 420 高的窗里**一次都没生效**，29 行 × 实际行高 ≈11.7 = 340。
   ⇒ 结论：**在 headless 的取值下，内容 340 < 视口 348，压根不需要滚动。**

于是 case 8 的挂死完全自洽：**"内容溢出 ⇒ 出现滚动条"这条因果链在 E0 里根本就没发生过**，
它自然不可能是触发条件。

## 4 定论

> **触发条件 = `ScrollViewer` 的垂直方向处于「可滚动」模式（`Auto` / `Hidden` / `Visible`），
> 且内容是 `TextWrapping.Wrap` 的多行 `TextBlock`；在**首次布局**（`Show()` 内）定不下来。**

支撑与逐条排除：

| 维度 | 结论 | 证据 |
|---|---|---|
| 垂直可滚动 | **是必要条件**（唯一开关） | case 3（Disabled⇒0）vs E0（Auto⇒124）；Probe2/Probe3 同树只差这一个变量 |
| `Wrap` | **是必要条件** | case 4（NoWrap⇒0） |
| 多行内容 | **是必要条件** | case 2（一行⇒0） |
| 内容是否溢出视口 | **无关** | case 8（视口 828 ≫ 内容 340 ⇒ 仍 124）+ §3 的几何 |
| 滚动条是否可见 | **无关** | case 7（`Hidden` 仍 124） |
| 水平侧 | **无关** | E1（水平 `Disabled` 仍 124） |
| 挂在哪一步 | **`Show()` 内的首次布局** | Probe2 落盘停在「即将 `w.Show()`」，且它**不经过** `ShowDialog` |

## 5 否证：登记表原来猜的机制

登记表 §5 #40 写着：

> 嫌疑已收窄到一条**具体**的机制（**未验证**）：`ScrollViewer` 的水平滚动默认 `Auto`
> ⇒ 它以**无限宽度**测量内容 ⇒ `TextWrapping.Wrap` 的换行宽度永远定不下来 ⇒ 布局不收敛。

**这条被 E1 直接否证**：把水平滚动设成 `Disabled`（宽度约束回到 448）**仍然挂死**。
同时 Probe2/Probe3 给出了镜像读数 —— 改**垂直**侧的同一个开关，结果从挂死翻成收敛。
方向是**垂直**的，不是水平的。

登记表当时那条推理里，还有一个「吻合的反证」其实反着用了：说 `SettingsDialog`(14 处)、
`ProjectSettingsView`(12)、`CommunicationConfigView`(8)、`RecipeEditorView`(5) 里的 `ScrollViewer` **全都没挂**。
那些读数全部来自**同一个 headless 平台**，所以它们证明的是「ScrollViewer + 非 Wrap 内容不挂」，
正好是 §4 里 `Wrap` 那一条的必要性证据 —— 不是"水平/垂直"的证据。

## 6 最小复现：从 `ScriptEditorView` 摘出来

`Probe2` / `Probe3` 把树压到 `Border(Padding 16) → ScrollViewer → TextBlock(Wrap)`，
**两侧只差 `VerticalScrollBarVisibility`**，同一条命令：

| 侧 | 变量 | 退出码 | 落盘短语 |
|---|---|---|---|
| Probe2 | `Auto` | **124** | 停在「**即将 w.Show()**」 |
| Probe3 | `Disabled` | **0** | `w.Show()` 已返回 / `Pump()` 已返回 / 全部走完 |

⇒ 复现面从「一包 ScrollViewer 就挂」进一步压到「**垂直可滚动的那一个开关**」。
也说明挂死与 `ShowDialog`、owner、`ScriptEditorView` 都无关。

## 7 真平台对照：X11 / Skia 下**不挂**

回答遗留①。探针是 file-based app（`.workbuddy/tmp/probe73-x11/probe.cs`，取证后已删），
在 `xvfb-run` 里跑真实 X11 + Skia 后端，两侧各 2 轮：

| 轮 | `Disabled` | `Auto` |
|---|---|---|
| 1 | EXIT=0 | EXIT=0 |
| 2 | EXIT=0 | EXIT=0 |

`Auto` 侧还打出了几何（`win.Show() 已返回 ✓`、`ScrollViewer Bounds=480×420`）。

**🩸 两个必须记录的坑（都不是本议题的）：

1. **`xvfb-run` 必须自己带上 `-extension GLX`**。`--server-args` 一旦覆盖默认参数就不会带它，
   而本容器有 NVIDIA 用户态库却**没有** `/dev/nvidia*` ⇒ Xvfb 一开就 `核心已转储`（segfault），
   表现为 `EXIT=1` + `xvfb-run: 61 已中止`。**只设 `LIBGL_ALWAYS_SOFTWARE=1` 不管用。**
2. **首轮的对照侧 `EXIT=134`（`free(): invalid pointer`）是环境噪声，不是"Disabled 会崩"**。
   成因是系统 `libharfbuzz.so.0` 与 Avalonia 自带那份 `libHarfBuzzSharp.so` 的符号介入冲突。
   按 `LD_PRELOAD=<harfbuzzsharp.nativeassets.linux>/runtimes/linux-x64/native/libHarfBuzzSharp.so`
   预加载**自带那份**之后，**4/4 全绿**。
   ⇒ 如果没做这一步，我会得出"Auto 不崩、Disabled 崩"的**假结论** —— 这正是「一次只放开一个变量」
   之外还需要「**先把噪声排掉再下结论**」的实例。

## 8 被否证的修法：`Wrap` → `NoWrap`

既然触发条件在 `Wrap`，最诱人的修法就是把 `TextWrapping` 改成 `NoWrap`（case 4 已证它收敛，
而且帮助文本本来就带 `\n` 手工折行）。**实测否证**：

| 语言 | 总长 / 行数 | 最长行 | 估算宽度（半角前进 7.15px） | 可用宽 448px |
|---|---|---|---|---|
| zh-CN | 575 字符 / 29 行 | 54 字符（48 ASCII + 6 CJK） | **421px** | 勉强 ✓（余量仅 27px） |
| **en-US** | 986 字符 / 29 行 | **93 字符（全 ASCII）** | **665px** | ✗ **超 217px** |

⇒ 改 `NoWrap` 会让**英文用户必须横向滚动**才能读完帮助 ⇒ **产品退化，不能改**。
（zh-CN 侧的 421px 也贴着 448px，若半角前进是 7.8px 就是 452px —— 同样不安全。）

这条否证是"先量再改"的直接收益：单看 zh-CN 或单看 headless 的 273px，都会误判成"装得下"。

## 9 落地

**产品代码**：把「造窗」与「显示窗」拆开，`ShowLuaHelp()` 行为不变（纯重构）：

```csharp
internal static Window CreateLuaHelpDialog() { …构造 + 关闭按钮接线 + return helpDialog… }
private void ShowLuaHelp() => CreateLuaHelpDialog().ShowDialog(this);
```

**判据** `LuaHelpDialogUiTests`（**3 条**，走 `CreateLuaHelpDialog()`、**只构造不 Show** ⇒ 不碰会挂死的那条路径）：

| # | 判据 | 抓什么 |
|---|---|---|
| ① | `HelpDialog_TitleAndBodyComeFromTheDictionary` | 标题走 `ScriptHelpTitle`、正文走 `ScriptLuaHelpText`；文案空 / 等于键名（缺键回落）都要红 |
| ② | `HelpDialog_HasExactlyOneButtonLabelledClose` | 按**文案**找关闭按钮（不按 `grid.Children[1]` 下标），恰好一个 |
| ③ | `HelpDialog_CloseButtonIsActuallyWiredToClose` | **点那个按钮必须真的调 `Close()`**（`Closed` 只由 `Close()` 触发 ⇒ 计数即等价物）。**阶段 75 补**，§10 边界 1 因此被推翻 |

两条都带「输入自检」（文案不能空、不能等于键名），否则断言会被坏输入养活。
遍历用**自写的 `Content`/`Child`/`Children` 递归** —— `GetVisualDescendants()` 要求已 attach
（要 `Show()`，正是会挂死的一步）；`GetLogicalDescendants()` 又枚举不到 `Panel.Children`。

**`DialogInnerInvocationUiTests`**：`BtnHelp` **仍在 `Never` 桶**（点它就走 `ShowDialog` ⇒ 挂死），
但理由从「触发条件未定」改成 §4 的定论 + 三条否证 + 「真平台不挂」+ 「内容已由另一条判据覆盖」。

## 10 诚实边界

1. ~~**判据只覆盖「内容」，不覆盖「行为」**：关闭按钮的接线是 `closeBtn.Click += (_, _) => helpDialog.Close()`，
   而「点了真的会关窗」只能把窗口**真显示出来**才观察得到 —— 那正是会挂死的一步。
   所以「产品按 `grid.Children[1]` 下标认按钮」这个脆弱点**仍然是空的**：将来有人往那个 Grid 里插控件、
   没改下标，接线会静默失效，而没有任何判据会红。~~
   ⇒ ✅ **已闭环（阶段 75），且这条边界本身被实测推翻**：「`Show()` 是观察接线的必要条件」是**未经实测的推断**。
   实测（探针 P1/P2）：从未 `Show()` 的 `Window.Close()` **不抛且会触发 `Closed`**；未 attach 的按钮上
   `RaiseEvent(Button.ClickEvent)` **会**触发直接挂上的 handler ⇒ **不用显示窗就能判接线**。
   于是补了第 3 条判据，并把产品里 `grid.Children[1]` 那个**位置下标**换成对按钮实例的直接引用。
   配对实验（同一注入、只差新旧代码）：往那个 Grid 里插控件 ⇒ **旧 RED / 新 GREEN**；摘掉接线 ⇒ 两代都 RED。
   详见 `docs/project-status.md` 阶段 75。
2. **`NoWrap` 的否证用的是估算宽度**（Consolas 半角前进按 0.55em ≈ 7.15px 算），不是真实渲染量。
   但结论方向很硬：en-US 最长行 93 个 ASCII 字符，要压进 448px 需要每字符 ≤4.8px，等宽字体不可能。
3. **真平台对照只到「不挂」**：没有验证真平台上滚动条行为、文本换行结果是否正常（那是另一件事）。
   另外那个探针用的是**编造的多行文本**（686 字符 / 29 行，贴近形状），不是真文案。
4. **触发条件写成了「垂直可滚动 + Wrap 多行」，但机制未落到 Avalonia 源码级**：
   没有去定位 `ScrollViewer.MeasureOverride` 与 `TextLayout` 之间到底是哪一步不收敛。
   本阶段给的是**可复现的边界**（哪些开关能翻）与**否证**，不是根因代码位置。
5. `headless` 的字体度量（273×340 / 12.0 px 行高）**不能外推到真机** —— 测试文本在 headless 下
   全是 `.notdef` 字形。§8 的表就是按真机字体另算的。

## 11 验证

**负控**（`.workbuddy/tmp/neg73.py --run`，5 个 case + 还原复跑）：

| case | 注入 | 结果 |
|---|---|---|
| 1 | 标题串键（`ScriptHelpTitle` → `GenClose`） | 红 ✓（失败 1） |
| 2 | 正文硬编码，不再取字典 | 红 ✓（失败 1） |
| 3 | 关闭按钮文案换成别的键 | 红 ✓（失败 1） |
| 4 | 再加一个同文案的关闭按钮 | 红 ✓（失败 1，`Single` 抓到） |
| 5 | 砍断内容递归（`Window` 不再下钻） | 红 ✓（**失败 2** —— 两条判据都依赖递归） |
| — | 还原后复跑 | **绿 ✓** |

判红谓词取测试**自己的汇总行**（`失败: N`，N>0）**且**要求出现 `错误消息:`；
认不出归 `unknown`，与 `build-fail` 一样**不算数**。还原一律**写回备份原文**（不用 `shutil.copy2`）。

`BtnHelp` 的 skip 理由也仍然受 `BucketTables_AreNotEmptiedAndAreSelfConsistent` 管辖
（理由长度、名字必须真存在于某个 `.axaml`）。
