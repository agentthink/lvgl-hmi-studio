# 窗口视图冒烟网：32 个窗口进网，首跑抓到 3 个 NRE + 1 个面外类型

> 2026-09-20 · 阶段 48 · 登记表 §4 **#38 未闭环 ②** 收口
> 提交 `8e9add4`（代码）· 新增 `tests/HmiEditor.App.UiTests/WindowViewSmokeUiTests.cs`（34 条）

## 0. 一句话

阶段 47 的 #38 遗留②说「26 个 `ChromeWindow` 视图不在任何冒烟面里」——
本轮把**窗口这一类**整个拉进网（实测 **32** 个，不是 26），顺带修掉首跑抓出的
**1 个生产路径构造崩溃** + **2 个关闭期 NRE**，并补上一条**面覆盖守卫**，
它又抓出**第 3 类视图形状**（既不是 UserControl 也不是 Window）。

## 1. 先纠正登记里的数字：是 28 不是 26

登记里「26 个」来自 `grep "class .*: *ChromeWindow"` —— 这个模式**漏掉经泛型基类继承的**：

```
Controls/AiGenerateDialogBase.cs:28   :  public abstract class AiGenerateDialogBase<TResult> : ChromeWindow
Views/AiTemplateDialog.axaml.cs:15    :  public partial class AiTemplateDialog : AiGenerateDialogBase<ControlTemplate>
Views/AiScriptDialog.axaml.cs:15      :  public partial class AiScriptDialog : AiGenerateDialogBase<HmiScript>
```

`class \w+\s*:\s*ChromeWindow` 里的 `\w+` 撞上 `<TResult>` 就匹配不上了。
所以按**类型关系**（`typeof(ChromeWindow).IsAssignableFrom`）数是 **28**。
这也正是本轮把面从"列名单/按名字 grep"改成"按类型关系反射"的理由 ——
同一个漏法在**计数**和**取面**上是同一个错。

## 2. 面：口径与两个守卫

| 面 | 口径 | 实测 |
|---|---|---|
| 窗口面（本层） | 命名空间以 `HmiEditor.App.Views` 开头、非抽象、非开放泛型、是 `Window`、有无参构造函数 | **32** |
| UserControl 面（既有 `ViewSmokeUiTests`） | 同上但是 `UserControl` | **15** |

32 = **28** 个 `ChromeWindow` 子类 + `SplashWindow`（`: Window`）+ **3** 个代码里造的 private 嵌套对话框
（`RecipeEditorView` 里的 `TextInputDialog` / `ConfirmDialog`、`AnimationEditorView` 里的 `ConfirmDeleteDialog`）。
嵌套的那三个进面是有意的：**"没人构造过"正是本层要防的事**，它们确实由宿主视图 `new` 出来并 `Show`。

### 守卫一：面自检（`WindowCatalog_IsNotEmpty`）

`count >= 32` + 「面里至少有 28 个 `ChromeWindow` 子类」。
后半条是关键：若口径被改成只收 `Window` 直系，外壳断言与手柄断言会**全部静默跳过**，数量下限拦不住。

### 守卫二：**面覆盖守卫**（`EveryViewType_IsInSomeSmokeSurface`）

把两个面**并起来**，要求收走 `HmiEditor.App.Views` 下**所有**非抽象、有无参构造的
`Control` 派生类，外加空集守卫（`all.Count >= 48`，防"命名空间改名 ⇒ 差集恒空"的假门禁）。

**这条守卫才是"窗口长期没人测"的真正解药**：原来两个面各自守自己的清单，谁都不问
「还有没有第三类」。首跑它立刻报出 `TrendChartPanel` —— 一个 `internal class ... : Control`
的自绘趋势面板，两边都不收。它由 `TrendConfigView.OnOpened` 里
`parent.Children.Insert(idx, customPanel)` 换进树，所以进**豁免表**（`ReachedByAHost`）。

### 豁免必须可验证

`ReachedByAHost` 不是一句注释：宿主视图（`TrendConfigView`）的用例结尾会断言
「这个被豁免的类型确实出现在可视树里」。为此 `Snapshot` 多带一个
`VisualTypeNames`（可视树里所有控件的 `FullName` 集合）。
**宿主哪天不再构造它，判据就红** —— 见负控 N8/N9。

## 3. 首跑抓到的真缺陷（取证在修复之前）

首跑读数：34 个用例 **30 通过 / 4 失败**。3 个是窗口、1 个是面外类型。

### 3.1 `DeviceManagerView` —— 生产路径**构造即崩**（P1）

```
System.NullReferenceException
  at HmiEditor.App.Views.DeviceManagerView.OnChannelTypeChanged(...) ... :line 99
  at HmiEditor.App.Views.DeviceManagerView.!XamlIlPopulate(...) DeviceManagerView.axaml:line 63
  at HmiEditor.App.Views.DeviceManagerView.InitializeComponent(Boolean loadXaml)
  at HmiEditor.App.Views.DeviceManagerView..ctor()                     :line 40
```

`DeviceManagerView.axaml:60`：

```xml
<ComboBox x:Name="CmbChannelType" SelectedIndex="0" SelectionChanged="OnChannelTypeChanged" ...>
```

XAML 里设 `SelectedIndex` 会在元素 `EndInit()` 时提交选择并**同步**抛 `SelectionChanged`；
而处理器读的 `SerialPanel`(67) / `BaudPanel`(77) / `TcpPanel`(85) 声明在它**后面**
—— 字段按 XAML 声明顺序赋值，那一刻三个都还是 `null`。

**为什么"从来没被发现"**：异常从 `new DeviceManagerView()` 抛出，调用方
`Wiring.Panels.cs:54` 的 `dialog.ShowDialog(this)` 根本执行不到，**没有 try/catch 兜底**；
而全仓没有任何判据构造过这个窗口。`git log -S` 显示该行自 **`2adb1f8`（2026-08-11）**
设备子系统那次提交起就在 ⇒ 长期存在，不是回归。

**修法**（`28` 行 diff，逻辑零改动）：

- 处理器加 null 前置检查；
- 把「按当前通道类型落地面板可见性」抽成 `ApplyChannelPanels()`，构造函数在
  `InitializeComponent()` **之后**调一次 ⇒ 初始状态不再依赖"XAML 里各面板的 `IsVisible`
  默认值碰巧对得上"（实测默认值确实对得上，但那只是巧合）。

### 3.2 `AlarmListView` / `SimulatorView` —— 关闭期 NRE

```
at HmiEditor.App.Views.AlarmListView.OnClosed(EventArgs e) :line 57
at Avalonia.Controls.Window.Close()
```

两者的无参构造是**明确标注「仅用于 XAML 设计器」**的那条：`_engine = null!`（`AlarmListView`）、
四个字段全 `null!`（`SimulatorView` 的两个模拟器 + 两个 `DispatcherTimer`），
而 `OnClosed` 里的退订/停表代码**直接解引用**。⇒ 在 XAML 设计器里关掉预览就是一次 NRE。

不算生产崩溃（生产走带参构造），但**"public 构造产出关不掉的窗口"是真缺陷** ——
注释写「不要直接使用」不是守卫。修法：退订/收尾加空值前置，语义与生命周期对称性不变。

> 这三个注入点后来都成了负控 N1/N2/N3（**拿真实缺陷当负控**，
> 且取证发生在修复之前 —— 这是最可信的一种负控）。

## 4. 判据（34 条）

| # | 判据 | 类型 | 守什么 |
|---|---|---|---|
| 1 | `WindowCatalog_IsNotEmpty` | 面自检 | `count ≥ 32`；且面里确有 `ChromeWindow` 子类（否则外壳/手柄断言静默跳过） |
| 2 | `EveryViewType_IsInSomeSmokeSurface` | **面覆盖** | 两个面的并集 ⊇ `Views` 下所有可构造 `Control` 派生类（**豁免表外零遗漏**）+ 空集守卫 |
| 3 | `Window_LoadsAndLaysOut`（32 例） | 行为 | 构造 → `Show()` → 布局 → `Close()` 不抛；可视树非空；至少一个具名控件 |
| 3a | 同上，`ChromeWindow` 专属 | 行为 | **外壳契约**：`Content is Border` 且 `Child is Grid` 且根 Grid **行数 ≥ 2** |
| 3b | 同上，`ChromeWindow` 专属 | 行为 | **八向缩放手柄 = 8**（4 边 4px + 4 角 10×10） |
| 3c | 同上，豁免作证 | 行为 | `ReachedByAHost` 里认这个窗口当宿主的类型，必须出现在可视树里 |

### 为什么行数 ≥ 2 也要判

`ChromeWindow.InjectResizeGrips` 注入的 overlay 用 `Grid.SetRowSpan(overlay, 2)`。
根 Grid 只有 1 行时它被约束在第 0 行内 ⇒ 南/北手柄落在**标题栏下沿**而不是窗口边缘。
实测 29 个 XAML 窗口的根 Grid 都是 `RowDefinitions="36,…"`（≥2 行），
`CommunicationConfigView` 与 `SplashWindow` 是 `36,*,Auto`。
这是"以后加窗口时忘了写行"的绊线，不是现存缺陷。

## 5. 本层**不**抓什么（明确写进类注释，避免重犯阶段 47 的高估）

`{StaticResource}` 键缺失**不是**本层的职责。阶段 47 已实测定性：落在**控件属性**上时是
**静默跳过赋值**（属性回落主题默认，布局照样跑完，不抛不报）⇒ 窗口在构造与布局期都不会红。
那类缺陷归 `XamlResourceKeyClosureTests`（静态扫描全部 47 个 axaml）。

**负控 N10 就是这条边界的证据**：往 `VariableImportView.axaml` 的一个 `Button` 上注入
`BorderBrush="{StaticResource NoSuchBorderBrushInjected}"` ⇒

| 层 | 结果 |
|---|---|
| `WindowViewSmokeUiTests`（本层，34 条） | **全绿**（静默缺键不抛） |
| `XamlResourceKeyClosureTests`（静态层，4 条） | **红 1 条** |

两层的分工不是"更保险"，是**各自可判的那一类不同**。

## 6. 为什么不能把两层的口径合成一个

- 宿主方式不同：`UserControl` 要包进一个宿主 `Window` 才能 `Show()`；
  `ChromeWindow` **本身就是窗口**，不能当 `Content`。
- 断言集不同：外壳契约与缩放手柄只对 `ChromeWindow` 有意义。
- 但**面的覆盖**必须合起来守 ⇒ 就是守卫二。

## 7. 负控（10/10，逐条断言"红哪些"）

脚本 `.workbuddy/tmp/negative-control-winview.py`（支持 `python3 … N1 N7` 只跑指定几条）。

| 注入 | 期望 | 实测 |
|---|---|---|
| N1 复原 `DeviceManagerView` 构造期 NRE（删 null 检查） | 红，点名该窗口 | ✅ 1 条：`…(typeName: "DeviceManagerView")` |
| N2 复原 `AlarmListView.OnClosed` NRE | 红，点名该窗口 | ✅ 1 条：`…"AlarmListView"` |
| N3 复原 `SimulatorView.OnClosed` NRE | 红，点名该窗口 | ✅ 1 条：`…"SimulatorView"` |
| N4 `CommunicationConfigView` 根 Grid `36,*,Auto → 36` | 只红行数那条 | ✅ 1 条：`…"CommunicationConfigView"` |
| N5 `VariableImportView` 外层多包一层 `Grid` | 只红外壳那条 | ✅ 1 条：`…"VariableImportView"`，消息带 `Content=Grid` |
| N6 删掉两个角手柄 `AddGrip` | 只红手柄那条，**且恰好 28 条** | ✅ **28 失败 / 6 通过**（6 = 4 个非 ChromeWindow 窗口 + 2 条守卫） |
| N7 面口径 `Window → ChromeWindow` | 两条守卫红，用例总数 34→30 | ✅ 2 条红、总计 30 |
| N8 豁免键名拼错一位 | orphan 报出 + 豁免作证红 | ✅ 2 条红，含 `EveryViewType_IsInSomeSmokeSurface` |
| N9 豁免宿主改填 `VariableImportView` | 宿主用例的可视树断言红 | ✅ 1 条：`…"VariableImportView"` |
| N10 **必须仍绿**：幽灵 `{StaticResource}` 注入控件属性 | 本层 ∅ | ✅ 全线绿；对照静态层**红 1 条** |

**N6 是本轮最有力的一条**：28 这个数字恰好等于面里 `ChromeWindow` 子类的个数 ——
它同时证明了「手柄判据对**每一个** `ChromeWindow` 生效」与「对 4 个非 `ChromeWindow` 窗口不生效」。

还原后 4 个被动过的文件 `sha256` 逐字节一致，复跑 308 全绿。

### 脚本本身踩的坑（值得记住）

第一版还原用 `text.replace(new, old, 1)` **反向**还原。N1/N6 的 `new` 是**空串**，
`replace("", old, 1)` 会把 `old` **插到文件开头** ⇒ 源码写坏、后续 9 条注入全部"编译失败"，
而且脚本自己报了 `sha256 不一致` 却继续跑完了。
改成「注入前把原始字节存下来，还原时**直接写回**」后 10/10 通过。
教训：**还原要写回快照，不要指望反向 replace** —— 空串、重复串、链式替换都会让它失真。

## 8. 顺带的重构（唯一实现）

`MainWindowConstructionTests` 里有一份 55 行的私有 `Sandbox`（隔离应用数据根）。
窗口视图的 `Opened`/`Closing` 会经 `WindowGeometryService` 读写
`AppPaths.DataRoot/windows.json`（`Load` 还会 `CreateDirectory`），所以本层也要同一个隔离件。
抽成共用 `internal sealed class DataRootSandbox`（带 `prefix` 参数）。

两个消费者各抄一份 55 行 fixture，等于把「**先还原环境变量、再删目录**」这条顺序约定抄两遍 ——
它一旦漂移，症状是"某个用例偶尔读到即将消失的数据根"，最难查的那种红。

## 9. 读数

| 项 | 值 |
|---|---|
| 面 | 窗口 **32**（28 `ChromeWindow` + `SplashWindow` + 3 嵌套）/ UserControl **15** / 并集人口 **48** |
| 新增用例 | **34**（32 窗口 + 2 条守卫） |
| 产品修复 | 3 个文件，**+52 / −15**（`DeviceManagerView` +26、`SimulatorView` 26 行重排、`AlarmListView` +15） |
| `App.UiTests` | 274 → **308** |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**；八程序集合计 **2993**（+8 单独 git 段 = **3001**），比阶段 47 的 2967 **净增 34**；构建 **0 警告 / 0 错误** |
| 门禁日志 | `.workbuddy/tmp/ci-gate-2026-09-20-winview.log`，mtime **`21:43:05`** ⇒ 晚于提交 `8e9add4`（**`21:42:08`**）**57 秒** |

> 收尾提交 `cc129f2`（同文件、**纯注释**：把类注释里沿用的「26 个」纠正为 **28** 并写明成因；
> 正文引号从半角 `"` 统一成 `「」`，`<see cref="X"/>` 的属性定界符**刻意不动**）。
> 它的门禁日志 `.workbuddy/tmp/ci-gate-2026-09-20-winview-final.log`，mtime **`21:49:01`** ⇒
> 晚于 `cc129f2`（**`21:48:17`**）**44 秒**，`GATE_EXIT=0`，八程序集合计仍是 **3001**。

## 10. 仍未闭环

① 本层走**无参构造** ⇒ 对 5 个类型（`AiTemplateDialog`/`AiScriptDialog`/`SplashWindow`/
`AlarmListView`/`SimulatorView`）走的是**设计器路径**：不接 Click、不订阅事件、不读最近工程。
XAML 解析与布局覆盖等价，但**事件接线仍未覆盖** —— 要覆盖得起"生产构造 + 依赖注入"那一层，
得为 32 个窗口各准备一份服务对象，是另一个量级的工作。

② 3 个嵌套对话框（`ConfirmDialog` / `ConfirmDeleteDialog` / `TextInputDialog`）本层只保证
「能造出来、能布局、能关掉」，**不保证宿主视图真的用了它们**（宿主用例没有对它们的作证断言）。

③ `SplashWindow` 只断言「构造 + 布局 + 有具名控件」：它是 `: Window`，
不接 `ChromeWindow` 的外壳/手柄契约，也没为它单独立契约。

④ 阶段 47 的三条遗留（键解析类型不匹配 / 50 个"定义未被引用"的键 / 运行期腿的前提）
原样留着，见 `docs/2026-09-20-xaml-resource-key-closure.md` §9。
