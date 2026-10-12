# 数字框接线面（`NumericUpDown` / `fa:FANumberBox` 的 `ValueChanged`，阶段 195）

> 承 `docs/2026-10-01-settings-textbox-wiring-face.md`：那一族（TextBox 的 `TextChanged`）
> 在阶段 188~194 逐文件驱动完毕；这一阶段把**同一套做法**搬到**数字框族**，
> 并修掉新判据当场逼出来的一条真缺陷（登记 `#90`）。

---

## 1. 实测（2026-10-01，阶段 195）

`Views/` 下 **60 个具名数字框**（`<NumericUpDown>` 13 个 ＋ `<fa:FANumberBox>` 47 个），
其中 **45 条**真的接了 `ValueChanged`（4 个文件）＝ **面 A 43 条** ＋ **面 B 2 条**
（后者都在 `ProjectSettingsView` 的 `AttachChangeHandler` 里）：

| 文件 | 具名框 | 面 A（有接线） | 面 B（按名找不到） | 本阶段处置 |
|---|---|---|---|---|
| `PropertyPanelView.axaml` | 31 | **31** | 0 | **`enumerated`**（由既有 `PropertyPanelWiringTests` 覆盖，见 §4） |
| `SettingsDialog.axaml` | 9 | **9** | 0 | `perBox`：9 条手势，读**设置** |
| `ScreenManagerView.axaml` | 2 | **2** | 0 | `perBox`：2 条手势，读**屏幕模型** |
| `ProjectSettingsView.axaml` | 16 | **1** | **2** | `perBox`：1 条手势（`NumPwdMinLen`）＋ 面 B 两条用代表手势驱动 |

> 另外 **17 个**具名数字框**没有**接线：`ProjectSettingsView` 里 15 个（DPI / 通讯 / 运行期 / 会话那一批）
> ＋ `AiIconDialog` 里 2 个（`HeightBox` / `WidthBox`）—— 都由"点保存/应用"一次性读取，
> **不是**缺口；`ProjectSettingsView` 那 15 个在各驱动面的 `withoutWiring` 登记表里逐条写了理由。

## 2. 两个族的**驱动纪律相反**

| 族 | 事件 | 程序化赋值 | 驱动怎么做 |
|---|---|---|---|
| TextBox | `TextChanged` | **不触发**（阶段 187 实测） | 写文本 ＋ **补发 `TextChangedEvent`** |
| 数字框 | `ValueChanged` | **会触发**（阶段 188 在 `NumAutoExportInterval` 上实测） | **写值**即可（不用补发） |

⚠ 但"写**同值**不发事件" ⇒ 每条手势的值必须与**起点值不同**。阶段 195 第一版在
`ScreenManagerView` 上把 `1024/600` 当成"新值"，而**那正是 `ScreenFactory` 的兜底尺寸**
（`FallbackWidth = 1024` / `FallbackHeight = 600`）⇒ 两个框写的是同值、事件根本不发，
断言**假绿**。**负控 nc2 摘掉 `NumScreenHeight` 的接线时它没红** —— 只有"扫描面下限"那条腿红了，
才把这件事暴露出来。处置：换成 `1280/720`，并加**三条前提自检**：

1. 起点必须是 `ScreenFactory` 的兜底尺寸（把"起点是什么"量出来，不靠记忆）；
2. 每条手势的值**必须与起点不同**（否则那条腿是空转）；
3. 两个框回填的也是**模型值**。

## 3. 两种控件类型：`NumericUpDown` 与 `fa:FANumberBox` **不是父子关系**

阶段 195 探针实测（`typeof(FANumberBox)`）：

| 读数 | 值 |
|---|---|
| 基类 | `Avalonia.Controls.Primitives.TemplatedControl`（**不是** `NumericUpDown`） |
| `Value` | `System.Double`（即 `double?`；而 `NumericUpDown.Value` 是 `decimal?`） |
| `ValueChanged` | `FluentAvalonia.Core.TypedEventHandler<FANumberBox, FANumberBoxValueChangedEventArgs>` |
| 属性名 | `Minimum` / `Maximum` / `SmallChange`（与 `NumericUpDown` **同名但各自实现**） |

⇒ 两条后果：

- `FindControl<NumericUpDown>("X")` 对 `FANumberBox` **会抛**
  （`Expected control 'X' to be 'NumericUpDown' but it was 'FANumberBox'`）——
  Avalonia 的 `Find<T>` 要求 `is T`；
- 驱动/读值必须**按运行时类型分派**：`ControlWiringFace.WriteNumber` / `NumberValue`
  同时吃两种类型（`FindControl<Control>` ＋ 类型分派）。

## 4. `enumerated` 这一档：**不重复记账**

`PropertyPanelView` 的 31 条已经由**枚举驱动**的 `PropertyPanelWiringTests` 覆盖（阶段 118 起：
从逻辑树枚举 70 个编辑器、逐个写值断言模型、最后 `uncovered == 0`）。
它**从不提控件名**（枚举出来的），所以本面那套"逐条点名"的核对对它不适用。

处置：状态列加 **`enumerated`**，核对是**指针级**的 —— 判据文件存在 ＋ 提到该视图 ＋
含它自己的"未覆盖必须为空"字样（`uncovered`）。**强证据在那一族判据自己身上**（阶段 118 的三条实测），
本行只保证"指针没断"。（重复逐条记账 = 把同一件事记两遍，本仓对"重复记账"的态度见阶段 118 的三条否证。）

## 5. 🩸 新判据当场逼出的真缺陷：`#90`（本阶段修掉）

`ProjectSettingsNumberBoxWiringTests` 的第一版就红了：驱动 `NumPwdMinLen` → 12，
**点「确定」之后工程里还是 8**。追下去：

- `ProjectSettingsView.AttachChangeHandler` 的第一支是 **`ctl is NumericUpDown nud`**；
- 而该视图的 **16 个数字框全是 `fa:FANumberBox`**（§3：**不继承** `NumericUpDown`）；
- ⇒ 那个 `AttachChangeHandler(...)`（16 次调用）**一次都没挂上**（`else if` 链全不匹配、静默跳过）；
- ⇒ `_hasChanges` 永远是 `false` ⇒ `OnOkClicked` 里那句 `if (_hasChanges) ApplyInternal();`
  **直接跳过保存** ⇒ **用户的改动被静默丢弃**（点「应用」才保存 —— 那条路无条件 `ApplyInternal()`）。

用户可见症状：在「工程设置」里改任何一个数字（屏幕尺寸 / 通讯超时 / 看门狗 / 心跳 / 会话超时 /
口令下限 / 最大尝试 / 锁定……共 16 个），点「确定」= **什么都没发生**；点「应用」才生效。

**修法**（2 行）：`AttachChangeHandler` 加一支
`else if (ctl is FluentAvalonia.UI.Controls.FANumberBox fanumber) fanumber.ValueChanged += (_, _) => _hasChanges = true;`。
**判据**：`ProjectSettingsNumberBoxWiringTests.PasswordMinLength_MarksDirty_AndOkPersistsIt`
（驱动力 → 提示文案跟着变 → 点「确定」→ 工程真的被写）。
**负控 nc1** 把这一支去掉 ⇒ 它当场红，读数 `Expected: 12 / Actual: 8` —— 正是缺陷现场。
⚠ 补上这一支之后，面 B 的接线标识从 1 条变 **2 条**（`nud` ＋ `fanumber`）——
普查表与驱动面的 `anonymousWired` 一起同步（这正是"面 B 认账"双向核对该有的反应）。

## 6. 判据与负控

- **普查面** `AppNumberBoxWiringCensusTests` **5 条**：面 A 双向差集（自检 ≥ 40）· 面 B 双向差集
  ＋ 逐条点名 · `perBox` 行必须"判据文件存在 ＋ 覆盖每个具名框" · `enumerated` 行的**指针级**核对 ·
  每條接线恰好进一个面。
- **驱动面 6 条**：`SettingsDialogNumberBoxWiringTests` 2（9 条手势 ＋ 双向差集）·
  `ScreenManagerNumberBoxWiringTests` 2（2 条手势 ＋ 双向差集）·
  `ProjectSettingsNumberBoxWiringTests` 2（面 A 的提示口径 ＋ 面 B 的代表见证 ＋ 双向差集）。
- **共享工具改名**：`TextBoxWiringFace` → **`ControlWiringFace`**（阶段 195）—— 它现在管两个族，
  差集逻辑抽成一处 `AssertBidirectional`（16 个调用点机械改名的 diff 一起进本阶段）。
- **负控 5 支**（`.workbuddy/tmp/s195-nc.py`）：

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | 去掉 `AttachChangeHandler` 的 FANumberBox 分支（＝复现 `#90`） | ProjectSettings 数字框面 | `Expected: 12 / Actual: 8` |
| nc2 | `ScreenManagerView` 摘掉 `NumScreenHeight` 的接线 | 驱动面 | `NumScreenHeight（HmiScreen.Height）驱动 720 后读到 600，期望 720` |
| nc3 | `SettingsDialog` 摘掉 `NumGridSize` 的接线 | 驱动面 | 网格尺寸读回旧值 |
| nc4 | `SettingsDialog` 去掉面板占比的 `/100.0` | 驱动面 | `驱动 25 后读到 '25'，期望 '0.25'` |
| nc5 | 普查面 `enumerated` 行指向不存在的判据 | 指针级核对 | `指向的枚举驱动判据 NoSuchWiringTests.cs 不存在` |

5 支逐支独立日志（`.workbuddy/tmp/s195-nc{1..5}.log`）、每腿限时、开跑前扫残留注入，
还原后 4 个文件的 SHA256 逐字节相同。

## 7. 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 778 / 8`（合计 **3619**）
＝ **3619 通过 / 0 失败**（阶段 194 为 3608，**＋11**）；
全量 App.UiTests **778 / 0**（连跑 2 次，40 / 36 s）；`UnusedBaseline` **不变 1109**
（本阶段没加 Lang 常量）；**产品侧 `ProjectSettingsView.axaml.cs` 1285 → 1299 行**
（＋14：2 行订阅 ＋ 12 行注释）；`MainWindow.axaml.cs` 仍 **2983**。

## 8. 仍未做（如实留开）

- **别的输入控件族**：`ComboBox.SelectionChanged`（已在选择族面内，阶段 115）·
  `CheckBox/ToggleButton.IsCheckedChanged`（第五层按钮面覆盖一部分）·
  `Slider`/`RangeBase.ValueChanged`（本仓暂未用）—— 要不要照本族再做一遍，是**口径**选择。
- **`Views/` 之外**（`Controls/`、设计器里的输入）不在本面扫描面内。
- **面 A 里那 96 个没接线的具名 TextBox / 17 个没接线的数字框**（含 `AiIconDialog` 的 2 个）：由
  "点保存/应用"一次性读取，**不是**缺口；但它们"到底有没有人读过"目前只由各自的**保存路径判据**
  间接看着（例：`ProjectSettingsRuntimeUserUiTests` 的"保存"腿），没有一张"谁读谁"的账。
