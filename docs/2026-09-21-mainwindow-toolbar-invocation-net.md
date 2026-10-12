# 主窗口工具栏判据（NameScope 面）：65 个具名 `Btn*` 全部归类，58 个真点

> 阶段 51，2026-09-21。测试 `tests/HmiEditor.App.UiTests/MainWindowToolbarInvocationUiTests.cs`，
> 提交 `90ad636`。**产品代码改动 0 行** —— 判据建起来就全绿。
> 负控脚本 `.workbuddy/tmp/negative-control-toolbar.py`（未提交，10 条注入中的 8 条已跑）。

## 0. 一句话

主窗口**自己**那 65 个工具栏按钮，从「只有静态绊线（名字被碰过）」变成「按生产构造路径
真点一遍、断言弹窗增量与类型」；而这一层真正的发现不是任何产品缺陷，是**面选错了**：
这 65 个里 **44 个压根不在逻辑树上**，所以主面必须走 NameScope（`FindControl`），
不能遍历逻辑树。

## 1. 起因：三层网之间还漏了一段

| 层 | 已覆盖 | 它不覆盖 |
|---|---|---|
| 阶段 46 主窗口装配 | 窗口能构造 + **143** 个具名控件的接线闭合（**静态**：名字在 code-behind 里被碰过） | 名字被碰过 ≠ 接的是**正确的**处理器 |
| 阶段 49 菜单判据 | 62 个具名菜单项 → 宿主弹得出来 | 宿主**内部**的按钮 |
| 阶段 50 宿主内部按钮 | 6 个宿主内 **57** 个具名按钮 | 主窗口**自己**的工具栏按钮 |

缺的正是最后一行。而且这里有个静态绊线**原理上**看不见的形状：

```csharp
EventHandler<RoutedEventArgs> openUserManager = (_, _) => { ... new Views.UserManagerView(...) ... };
MenuUserManager.Click += openUserManager;   // 菜单项
BtnUserManager.Click  += openUserManager;   // 工具栏按钮 —— 同一个委托实例
```

**10 个**工具栏按钮与菜单项共用同一个处理器委托。把这行接到别的窗口上、
或者干脆摘掉，`EveryNamedControl_IsTouchedByTheCodeBehind` 照样绿 —— 它只问名字有没有出现过。

> 这条缺口在登记表 §14 的「不抓」里其实早就写着：
> 「只覆盖 `Menu*`，`Btn*` 面未覆盖（65 个具名按钮、其中 10 个是弹窗入口，**形状同族可直接套**）」。
> **形状同族 ≠ 零成本** —— 本文记的就是那部分成本，以及成本花在哪儿。

## 2. 判据形状

```
构造 MainWindow（生产构造路径：SettingsService → ctor → Show → Measure/Arrange 1024×600 → Pump）
  → 按表里登记的名字逐个 FindControl（NameScope 面）解析实例
  → 按表里**声明的顺序**逐个 RaiseEvent(Button.ClickEvent)
  → 断言主窗口 Window.OwnedWindows 的**增量与类型**
```

- **11 个**必须弹：增量恰 1 且**类型名相符**（不符即点名实际类型 —— 这一条同时说明
  Click 接到了别的处理器上）；
- **47 个**静默：增量恰 0（针对本初始状态 + 本表顺序）；
- **7 个**不点：理由必填（≥10 字符）；
- 另覆盖三个边界：一次点击弹 > 1 个窗口 / 关闭后 `OwnedWindows` 没回落 / 点击把主窗口关掉
  （则 break，避免后续断言变成幻觉）；
- 一句反向守卫：`clicked < 40` ⇒ 报「判据可能退化了」。

与宿主层（阶段 50）的差别只有一处：宿主层断言的是**宿主自身**的 `OwnedWindows`，
本层断言的是**主窗口**的 —— 同一把尺，换了个 owner。

## 3. 三个面（本文的核心）

### 3.1 表格

| 面 | 取法 | 实测覆盖 | 它漏什么 |
|---|---|---|---|
| **NameScope（主路）** | `window.FindControl<Control>(name)` 按表逐个 | **65 / 65** | 名字写错就是解析不出（⇒ missing）；**不反映渲染** |
| **XAML 声明** | `MainWindow.axaml` 上正则 `x:Name="(Btn[A-Za-z0-9_]*)"` | 65 | 只证明"写了名字"，不证明控件真的可解析、更不证明接线 |
| 逻辑树 | `GetLogicalDescendants()` | **21** | 集合属性里的元素**不进 `LogicalChildren`** ⇒ 整族正式按钮看不见 |
| 可视树 | `GetVisualDescendants()` | **32** | 反映"渲染出来的"，与登记完整性无关 |

三个面的**分工**：前两个守"登记"（双向差集各自点名），后两个只作**读数**与"多出来"的检查。
把它们合并成一条会丢掉可诊断性 —— 首跑正是**声明面那条独立的判据是绿的**，
才立刻把"我表写错了"排除掉、定性成"面选错了"。

### 3.2 为什么逻辑树不能当登记判据（首跑实测）

首跑（遍历逻辑树）报：

```
表里登记了但主窗口逻辑树上找不到这些按钮：BtnAlarmConfig, BtnAlignBottom, …（44 个）
逻辑树上只找到 21 个具名 Btn*（预期 60+）—— 运行期面可能整体失效
```

21 = `BtnTool*` **7** + `BtnQuick*` **14**，全是 `StackPanel` 里的普通 `Button`/`ToggleButton`。
用 `.workbuddy/tmp/btn-ancestry.py` 解析全部 65 个的祖先链，定性立刻清楚：

| 直接父元素 | 元素类型 | 个数 |
|---|---|---|
| `CommandBar.PrimaryCommands` | `CommandBarButton` | **44** |
| `StackPanel` | `Button` | 14 |
| `StackPanel` | `ToggleButton` | 7 |

`CommandBar.PrimaryCommands` 是一个**集合属性**，不是 `LogicalChildren`；Avalonia 只在控件的
**模板**里把它渲染成可视子级 ⇒ 逻辑树里没有它们。而 `x:Name` 是 XAML 加载时按**词法作用域**
注册进窗口 NameScope 的，**与渲染无关** ⇒ 只有按名解析能拿全。

这也是**唯一**不关心"这些项此刻挂在哪儿"（未实例化 / 收进溢出区 / 挂在独立的弹层根上）的取法。

### 3.3 三条腿的可证读数与差集定性

同一份日志（`.workbuddy/tmp/nc-toolbar/T5.log`，由正控 T5 逼出）：

```
面：按名解析 65 个；逻辑树已渲染 21 个，可视树 32 个
```

`T7.log`（把主面换回遍历逻辑树）把缺的 44 个逐个标了去向：

- **11 个**标「只有可视树里有」= `BtnUndo`、`BtnRedo`、`BtnZoomIn`、`BtnZoomOut`、
  `BtnNewProject`、`BtnOpenProject`、`BtnSaveProject`、`BtnExportCode`、`BtnCompile`、
  `BtnBuildRun`、`BtnDownloadHmi` ⇒ **1024×600 下渲染出来了**（21 + 11 = 32 ✓）；
- **33 个**标「两个树里都没有」—— 含 `BtnUserManager`、`BtnReloadPanels`、对齐/剪贴板/变换整组。
  即：**这 33 个在 1024×600 下没有被实例化到任何一棵树里**。

> **机制只到"未验证"为止**：最像的解释是 `CommandBar` 模板里的溢出收纳区**未展开时不生成内容**。
> 本层**刻意不区分**「根本没实例化」与「实例化在独立的弹层根上」—— 因为主面按名取实例，
> 不依赖树。**这正是选 NameScope 的实际收益**：这些项挂在哪儿与本判据无关。

## 4. 桶表（65 项全部归类，无剩余）

| 组 | 个数 | 桶 | 为什么 |
|---|---|---|---|
| 多选对齐/尺寸/组合（`BtnAlign*` `BtnSameSize` `BtnDistribute*` `BtnGroup` `BtnUngroup`） | 11 | Quiet | 无选中 ⇒ 判据不成立、提前返回；这些按钮初始还 `IsVisible=false` |
| 剪贴板/删除（`BtnCopy` `BtnPaste` `BtnDuplicate` `BtnDeleteControl`） | 4 | Quiet | 同上：初始无选中、剪贴板为空 |
| 旋转/镜像/复位（`BtnRotate*` `BtnMirror*` `BtnResetTransform`） | 5 | Quiet | 同上 |
| 撤销/重做（`BtnUndo` `BtnRedo`） | 2 | Quiet | 撤销/重做栈为空 |
| 绘图工具（`BtnTool*`） | 7 | Quiet | `ToggleButton`，只切 `DockLayoutState.ActiveDrawTool` |
| 缩放（`BtnZoomIn` `BtnZoomOut`） | 2 | Quiet | 只改缩放系数 |
| **弹窗入口** | **11** | **Popup** | 前 10 个与菜单项**共用同一个处理器委托**；`BtnSettings` 是工具栏独有入口 |
| 起外部进程 / 写盘 / 换工程 / 推设备 | 7 | **Never** | `BtnNewProject` `BtnOpenProject` `BtnSaveProject` `BtnExportCode` `BtnCompile` `BtnBuildRun` `BtnDownloadHmi` |
| 改状态（14 个 `BtnQuick*` + `BtnNewScreen` + `BtnReloadPanels`） | 16 | Quiet | 改内存状态、不弹窗，但会动控件树 ⇒ 排在最后 |
| **合计** | **65** | 11 Popup / 47 Silent / 7 Never | 真点 **58** |

11 个弹窗的期望类型：`UserManagerView`、`AlarmConfigView`、`TrendConfigView`、
`ScriptEditorView`、`RecipeEditorView`、`VariableImportView`、`VariableManagerView`、
`SimulatorView`、`ScreenManagerView`、`StyleEditorView`、`SettingsDialog`。

### 4.1 数组顺序 = 点击顺序（显式声明的量，沿用阶段 50 的教训）

表里的顺序**不是字母序**，而是按"对状态的扰动从小到大"：

```
no-op 组（对齐/剪贴板/变换/撤销） → 绘图工具切换 → 缩放 → 弹窗入口
  → 加控件（BtnQuick*） → 加屏幕（BtnNewScreen） → 重建 Dock 面板（BtnReloadPanels，必须最后）
```

`BtnReloadPanels` 会重建整个 Dock 面板树 ⇒ 之后任何"再点一个按钮看看"都不再有意义。
**改动数组顺序 = 改动被测语义**（阶段 50 有配套负控 N4 证明过这一点；本层的顺序边界见 §5.1）。

### 4.2 不点的 7 个，理由都是"碰外部世界"

起外部进程 / 写盘 / 换掉当前工程 / 向设备推送。其中 `BtnDownloadHmi` 在 XAML 里本来就是
`IsEnabled="False"`（未连设备）。**注意**：`RaiseEvent` 不受 `IsEnabled` 限制 ——
所以"它现在是灰的"**不能**当成不点的理由，必须自己写清楚为什么不点。

## 5. 负控 8/8（6 红 + 2 条**期望绿**的对照）

| 注入 | 打哪条断言 | 期望 | 实测 |
|---|---|---|---|
| T1 摘掉 `BtnUserManager.Click += openUserManager` | 弹窗桶增量必须为 1 | 红 | ✅ `BtnUserManager：预期弹出 1 个 UserManagerView，实际增量 0` |
| T2 把它接到 `SettingsDialog` | 类型必须相符 | 红 | ✅ `弹出的窗口类型是 SettingsDialog，预期 UserManagerView` |
| **T3 交换 `BtnCopy` / `BtnPaste` 的顺序** | —— | **绿** | ✅ **3/3 全绿**（见 §5.1） |
| T4 表里登记一个不存在的名字 | 面差集 missing | 红 | ✅ 主面点名 missing；**声明面同时红**（表里有、XAML 没有 1 个） |
| T5 三处下界（`found` / `clicked` / `Buttons.Length`）抬到 99 | 恒真守卫 | 红 | ✅ 红 2 条，失败消息里带出 **65 / 58 / 65** 与完整读数 |
| T6 在 `MainWindow.axaml` **新增**一个具名按钮 | 面差集 extra | 红 | ✅ 主面点名"没被分类"；**声明面同时红**（XAML 有、表里没有 1 个） |
| T7 把主面换回"遍历逻辑树" | 面差集 missing | 红 | ✅ 44 个逐个点名，并标注各自在哪棵树 |
| T8 只改一行注释 | —— | **绿** | ✅ 3/3 全绿 |

还原后 4 个文件 `sha256` **逐字节一致**（脚本每条注入后自己校验，收尾再全量校验一次）。

**T1 与阶段 49/50 同构、价值相同**：摘掉接线后按钮**还在**（NameScope 里照样解析得到）
⇒ 任何"控件存在性"类守卫完全看不见，只有真点才抓得到。
**T2 就是"接了正确的处理器"那条判据**，且**不需要任何源码解析**。

### 5.1 两条期望绿的对照，各自钉住一件事

**T3（交换 `BtnCopy`/`BtnPaste` 顺序 ⇒ 仍绿）钉住的是本层的观测边界。**
交换后 `Paste` 先跑（剪贴板空 ⇒ no-op）、再 `Copy`（复制了个空）—— 内部状态**确实变了**，
但本层的锚点是 `OwnedWindows`，状态变化看不见。这不是缺陷：**本层管接线，不管状态**。
阶段 50 的宿主层顺序敏感（`BtnDeleteAnimation` 依赖前序 `BtnNewAnimation`）是因为那里
顺序影响"弹不弹窗"；本层的 65 个里没有这种依赖。
**这个差别是量出来的**，所以写在这里 —— 免得以后有人以为本层覆盖了顺序。

**T8（只改注释）** 是对照基线：排除"随便动一下就红"。

### 5.2 T5 同时是"绿判据没有读数"的解药

`Assert.True(cond, message)` 的 message **只在红的时候打印** ⇒ 判据全绿时，
「面到底解析出几个、真点了几个」是**拿不到**的。首跑绿了之后我只能说
"应该点了 58 个"，那是推断不是证据。

把三处下界抬到 99（正控）之后，失败消息里同时带出：`只解析出 65 个具名 Btn* 实例`、
`只点了 58 个按钮`、`表只剩 65 个按钮`，以及完整的逐按钮读数（含"面：按名解析 65；
逻辑树已渲染 21；可视树 32"）。**这是目前复现这些数字的唯一路径**（脚本 T5 + 日志）。
已登记为候选改进 **#42**（`ITestOutputHelper` 写日志 + `--logger console;verbosity=detailed`）。

## 6. 不抓什么

1. **「不弹窗」是静默的** —— 没接线、接了线但什么都不做，在这一层长得一样（T1 只覆盖了
   "该弹的没弹"，覆盖不了"该改状态的没改"）；
2. 动作型按钮的**副作用**不判（只判不抛异常 + 弹窗层数）—— 那 7 个干脆不点；
3. **不做真实命中测试 ⇒ 可达性不在面内**：本层会点那 33 个在 1024×600 下根本没渲染的按钮
   （`RaiseEvent` 直接投递，不经过命中测试）。本层管的是**接线通不通**，
   **不是**"用户在 1024×600 下够不够得到" —— 后者需要另一条判据（用可视树面 + 溢出状态去查），
   目前**没有**；
4. 画布上的实际效果（对齐算得对不对、控件加没加上）不判。

## 7. 读数

| 指标 | 值 |
|---|---|
| 新增用例 | **3**（`App.UiTests` 318 → **321**） |
| 两段之和（全量） | 3011 → **3014**（净增 3，纯净增） |
| 面 | **65** 个具名按钮 / **58** 个真点 / 11 弹窗 / 47 静默 / 7 不点 |
| 负控 | **8/8**（6 红 + 2 绿），4 个文件 `sha256` 逐字节一致 |
| 构建 | **0 警告 / 0 错误** |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**，日志 `.workbuddy/tmp/ci-gate-2026-09-21-toolbar.log` mtime **`10:07:47`** ⇒ 晚于提交 `90ad636`（**`10:06:43`**）**64 秒** |
| 产品修复 | **0 行** |

面与读数的分级最细的一组（含"逻辑树 21 / 可视树 32"）在 §3.3。

## 8. 仍未闭环

① **停靠面板内部的 `Btn*` 仍未覆盖**（`PropertyPanelView` / `LayerPanelView` /
`FaceplateLibraryView` / `ProjectExplorerView` / `TagMonitorView` / `AssetManagerView` …）——
那些是主窗口里**停靠的面板**，与"主窗口自己的工具栏"是**两个不同的面**。
本层**不等于**收掉了阶段 49 遗留① 的全部；登记表 #41。
② **绿判据没有读数**（§5.2），登记表 #42。
③ **造状态能力缺失**：47 个 Quiet 只证明了"不弹窗"，**没有**证明"做对了事"。
要往前一步必须能在 headless 里"先造一个选中控件、再点对齐" ⇒ 本层只覆盖了
无选中那条 no-op 分支。
④ **`BtnSettings` 弹 `SettingsDialog` 没有触发 #40 的挂死**（`SettingsDialog.axaml` 里有
14 处 `ScrollViewer`）。这是 **#40 触发条件未定**的又一条反证，
**不能**据此推出 #40 不存在 —— 它只是把"`ScrollViewer` 一律挂死"这个假设再否一次。
