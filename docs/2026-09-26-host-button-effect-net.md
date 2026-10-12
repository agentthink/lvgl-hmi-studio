# 阶段 107 —— 宿主内部按钮的「可观测效果」判据（第五层动作面，登记 #39 的「事件接线」那一半）

> **范围**：登记表 §4 **#39** 里那句「本层能证明『窗口打得开』，**不能**证明『按钮接对了处理器』」
> 的正面处置 —— 给**不弹窗**的按钮建运行期判据。
> **产品代码 0 行改动**（负控注入后 SHA256 逐字节还原）。
> 判据文件 ⇒ `tests/HmiEditor.App.UiTests/HostButtonEffectUiTests.cs`（627 行，新增）。

---

## 107.1 缺口：四层判据判的都是「弹窗」，看不见「副作用」

既有四层动作面判据的面与形状：

| 层 | 文件 | 判什么 | 明示不判 |
|---|---|---|---|
| 1 菜单 | `MainWindowMenuInvocationUiTests` | 主窗口 62 个菜单项 → 弹窗增量与类型 | 副作用 |
| 2 工具条 | `MainWindowToolbarInvocationUiTests` | 主窗口自己的 65 个按钮 | 副作用 |
| 3 对话框内 | `DialogInnerInvocationUiTests` | 6 个宿主内 57 个具名按钮 → 宿主 `OwnedWindows` 的增量与类型 | 「不弹窗」是静默的；动作型按钮的副作用不判 |
| 4 停靠面板 | `DockPanelInvocationUiTests` | 12 个面板内 22 个 `Btn*` | 不判副作用（切换语言、写状态栏、改内存对象都不可见也**不该在这层判**） |

而**面板里绝大多数按钮根本不弹窗**：它们改数据、刷列表、写状态栏。四种情况在四层判据里
长得完全一样：

| 情况 | 四层判据 |
|---|---|
| 接线正常、改数据 | 静默 → 绿 |
| **接线被删掉** | 静默 → **绿** |
| **接到了另一个处理器上** | 静默 → **绿** |
| 接了线但什么都没做 | 静默 → 绿 |

⇒ **三种坏情况与「正常」不可分辨**。这就是 #39 的那一半。

---

## 107.2 量的那一遍（三轮口径修正，第三轮才可用）

| 版 | 分母（面） | 分子（覆盖） | 哪里错了 |
|---|---|---|---|
| 1 | `X.Click +=` 的接收方 | — | **分子错**：只认大写开头的标识符 ⇒ 漏掉 `FindControl` 之后的小写局部量（`okBtn` 之类） |
| 2 | 带事件属性（`Click=` / `SelectionChanged=` …）的元素 | 守卫里出现过的名字 | **分母错**：漏掉「显示型控件」与「代码里按名找控件再接线」的那一大批 |
| 3 | XAML 里 `x:Name` 落在**可交互类型**上的元素 | 两种分子口径（字面量 / 词边界） | 分母仍混着 `ComboBox` / `TextBox` / `ListBox` ⇒ 与判据口径不一致 |
| **4（已定稿）** | `x:Name` 落在 **Avalonia 的 Button 面**上的元素（`Button` / `ToggleButton` / `CheckBox` / `RadioButton` / `RepeatButton` / `SplitButton`） | 五层动作面判据文件里提过的名字 | — |

🩸 第 1 版那个错误**在第二版就报了错的方向**：仪器误差 **13 个**（只被「生成字段 / 词边界」
访问、第二版却算成残余）—— 也就是说「残余 60%」这个数里有一部分是**我自己的仪器**。
定稿口径的理由：判据用 `GetLogicalDescendants().OfType<Button>()`，而 Avalonia 里
`CheckBox : ToggleButton : Button` ⇒ 分母必须含 `CheckBox`，否则读数与判据对不上
（第 3 版的「32 个」正是漏了 CheckBox）。

### 读数（口径 4，只读探针 `.workbuddy/tmp/s107-face4.py`）

| 量 | 值 |
|---|---|
| 面：App 层 `x:Name` 落在 Button 面上的元素 | **307** 个，分布在 43 个 `.axaml` |
| 五层动作面判据**提过名字**的 | **161**（52%） |
| **残余**（只在 XAML 里具名、五层都没提过） | **146**（47%），27 个视图 |

残余最多的几个视图：

| 视图 | 残余 | 例 |
|---|---|---|
| `SettingsDialog` | 25 | `BtnOk` `BtnApply` `ChkShowGrid` `ChkCtxRename` `BtnThemeDark` … |
| `ProjectSettingsView` | 18 | `BtnAddRuntimeUser` `ChkAuditLog` `ChkTamper` `BtnApplySizeToAll` … |
| `CommunicationConfigView` | 14 | `BtnAddConnection` `BtnTestConnection` `ChkConnEnabled` … |
| `TrendConfigView` | 11 | `BtnAddTrend` `BtnToggleRecord` `ChkShowLegend` … |
| `PropertyPanelView` | 10 | `EditCellsBtn` `PropIsChecked` `ScreenIsBoot` … |

⚠ **「残余」的正确读法**：分子只数**这一族（动作面）判据**提过的名字。
`ProjectSettingsView` 另有 `ProjectSettingsRoundTripUiTests` / `ProjectSettingsRuntimeUserUiTests`
盯着它的往返保真 ⇒ 残余 **不等于**「没有任何判据碰过」，只等于
**「不在动作面这一族的覆盖面里」**。口径写清避免下一次误读。

---

## 107.3 改法：判「点一下之后数据面变成什么」

新增 `HostButtonEffectUiTests`，三层形状：

| 层 | 内容 |
|---|---|
| **① 可观测效果** | 每个宿主声明一组 `Probe`（分量），逐条按钮声明「点完之后哪些分量**等于什么值**」。分量从**两处**读：**模型**（`depot` 里的集合计数）证明「数据真的变了」，**窗口控件**（`FindControl<ListBox>(…).Items.Count`）证明「界面真的刷新了」 |
| **② 面双向差集** | 宿主里**每一个具名 `Button`** 必须在表里分类：少一个（控件被删/改名/表里名字写错）与多一个（新增按钮没人分类）都红 |
| **③ 表卫生** | 面非空、桶与字段自洽、理由写得出、`Expect` 的分量名真的存在、宿主类型真的存在 |

### 桶

| 桶 | 语义 | 字段约束 |
|---|---|---|
| `Moves` | 本状态下点下去**会**改这些分量 | 至少 1 条期望值；**且至少一条真的动了**（自检） |
| `Inert` | 本状态下点下去**数据面一字不变** | 不许有期望值；理由 ≥ 10 字符 |
| `Never` | 不点 | 理由必填；名字必须在某个 `.axaml` 里找得到 |

### 🩸 断言为什么是**双向**的

- **声明的分量必须「等于声明值」，不是「变了」**：把
  `Click += AddGroup` 接成 `DeleteGroup`，读数照样变 —— 「变了」判据照样绿。
  声明**目标值**才抓得住「接错处理器」（核心负控 NC1）。
- **没声明的数据面分量必须一字不变**：把「副作用外溢」（点一个按钮顺带改了别的东西）
  也变成红。少了这一半，一条「顺手多改一个集合」的接线缺陷会静默溜过（NC4）。
- **「声明值恰好等于点击前的值」单列一条自检**：否则 `Moves` 可以被写成一份
  「当时读到的值」抄本 —— 那种声明恒真、等于没写。

---

## 107.4 🩸 「点一下」怎么模拟 —— `CheckBox` 是这里的关键

对普通 `Button` 就是 `RaiseEvent(Button.ClickEvent)`。对 `ToggleButton` 系（含
`CheckBox`）**必须先翻转 `IsChecked`、再引发 `Click`**：

```csharp
if (control is ToggleButton toggle)
    toggle.IsChecked = !(toggle.IsChecked ?? false);
control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
```

理由：框架里 `ToggleButton.OnClick` 干的正是这两件事（翻转 → 引发 `IsCheckedChanged` →
引发 `Click`）。只做后者会得到**假阴性** —— `IsCheckedChanged="OnXxx"` 这种接线
**一次都跑不到**。本阶段实测的见证者是 `ScreenManagerView.ChkPublic`：它的 XAML 里
**没有** `Click=`，只有 `IsCheckedChanged="OnPublicChanged"`；补上翻转之后读数才从
「公开屏 0 → 0」变成「**0 → 1**」。**这是本层与四层判据的一处真实能力差异**。

---

## 107.5 读数

本层首批：**3 个宿主 / 面 31 个具名 Button，逐条双向分类**。

| 宿主 | 面 | Moves | Inert | Never |
|---|---|---|---|---|
| `VariableManagerView` | 20 | 5 | 14 | 1 |
| `AlarmConfigView` | 7 | 3 | 3 | 1 |
| `ScreenManagerView` | 4 | 3 | 0 | 1 |
| **合计** | **31** | **11** | **17** | **3** |

⚠ **本行原写 `Inert` 14，阶段 108 更正为 17**（逐宿主 14＋3＋0）。
14 是把 `VariableManagerView` 那一行误当总和的**手抄错误** —— 分档数字**只许从判据的输出行抄**。
⚠ 上面这张表是**阶段 107 的历史态**；阶段 108 修好 #64 后桶变为 `Moves` **15** / `Inert` **13** / `Never` **3**
（合计 31 不变）⇒ 见 `2026-09-26-list-selection-id-contract.md`。

| 量 | 前 | 后 |
|---|---|---|
| 该类用例数 | 0 | **4**（3 Theory ＋ 1 表卫生） |
| `App.UiTests` | 580 | **584** |
| 动作面判据的层数 | 4 | **5** |
| `Core.Tests` / 两本字典 / `UnusedBaseline` | 2122 / 2296 / 1108 | **全不动** |

**产品代码 0 行改动。**

---

## 107.6 负控 4/4（判红 ＋ 点名，两条都要）

| # | 注入（都能编译） | 期望子串 | 实测 |
|---|---|---|---|
| NC1 | `VariableManagerView`：`BtnAddVariable` 接到 `DeleteVariable()`（**接错处理器**） | `分量「变量」期望 3、实际 2` | **红 ✓** |
| NC2 | `ScreenManagerView`：注释掉 `BtnAddScreen.Click += …`（**摘掉接线**） | `分量「屏幕」期望 3、实际 2` | **红 ✓** |
| NC3 | 表里塞一个不存在的控件名 `BtnNotExist`（**面守卫**） | `找不到这些按钮：BtnNotExist` | **红 ✓** |
| NC4 | 删掉 `AlarmConfigView.BtnDeleteGroup` 的 `("报警", "1")` 声明（**未声明分量外溢**） | `**未声明**的分量「报警」从 2 变成了 1` | **红 ✓** |

逐支一个日志（`s107-nc1..4.log`）、逐支**写回原文**并断言 SHA256 与注入前相同
（**不用** `shutil.copy2` —— 它会连 mtime 一起复制 ⇒ MSBuild 判定无需重建 ⇒
「还原后的复跑」实际跑的是负控那次的二进制）。

⚠ **NC3 有一处必须记住的副作用**：注入的名字是 `Inert` 桶 ⇒ 表卫生那支**照样绿**，
只有 Theory 那支红。这正是「面守卫在运行期、卫生检查在静态」两层分工的实证。

---

## 107.7 门禁

| 时刻（birth / mtime） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 16:30:09 | — | 内容批（判据）`ef6e22c` | 1 file，**＋627**，产品代码 0 行 |
| **16:30:33 / 16:31:56** | `s107-gate1.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；44 / 44 / 75 / 87 / 4 / 315 / **2122** / **584** ＋ 8 ＋ 2；路径卫生 **667** |

提交 `ef6e22c` 时刻 **16:30:26** ⇒ 门禁日志 birth **16:30:33**（**晚 7 秒**）。

---

## 107.8 ⚠ 顺带量出的东西：登记 #64

首跑（`s107-run2.log`）里 `VariableManagerView` 那支当场红了 4 条，全指向同一件事：
**点了 `BtnAddVariable` 之后 `BtnAddAlarm` 是空操作**（报警列表 0 → 0）。
追下去 ⇒ **产品代码里的一个真缺陷**：

`VariableManagerView.SelectListItemByTag(ListBox, string tag)` 的判据是
`lbi.Tag?.ToString() == tag`，而 `CreateTaggedListItem` 把**模型对象**整个塞进 `Tag`
（`Variable` / `VariableGroup` / `VariableAlarmConfig` / `UdtDefinition`），
这四个类型**都没有覆写 `ToString()`** ⇒ `ToString()` 恒定返回类型全名、与 Id 永不相等
⇒ **6 处调用全部是空操作**（`VariableManagerView.axaml.cs:374,422,482,495,545,569`）。

于是 `AddVariable` 里那句注释「自动选中新添加的变量，提升反馈感」**声明的行为不存在**。
注意**不是全仓**：`SelectComboBoxByTag` 那一族是对的（它的 `Tag` 是**字符串**）。

⚠ **不把它固化成期望**：本层的表里那几条 `Inert` 的 `Why` 明写
「登记 #64 现场 …… 缺陷修好后这条会红 —— 届时改成 Moves」。
**「修好就红」是设计要求**，不是脆弱 —— 表记的是**事实**，不是**期望**。

同一批还记下一条**不同根因**的交互事实（**未判为缺陷**）：
`AlarmConfigView.OnAddAlarm` 末尾的 `RefreshGroupList()` 重建左列表时把右列表切回第一个组
⇒ 刚新建的报警立刻「选不上」，紧接着点「删除报警」删不动。用户要先在新组里点一下那条报警。
这是「列表选中会被重建吃掉」这一族的**第二例**（第一例就是 #64 的症状本身）。

---

## 107.9 诚实边界 / 未闭环

1. **本层不声称「这条接线是对的」**，只声称「行为与声明一致」。表里 `Inert` 的条目里
   混着**登记在案的缺陷现场**；判据的作用是「缺陷被修好时点名」，不是「证明它对」。
2. **面只覆盖 3 个宿主 / 31 个按钮**；Button 面整体是 **307**、五层提过 161 ⇒
   残余 **146**（口径见 §107.2）。残余仍集中在 `SettingsDialog` / `ProjectSettingsView` /
   `CommunicationConfigView` / `TrendConfigView` / `PropertyPanelView` 五个大面板。
   ⚠ **本行是阶段 107 的历史态。** 阶段 109 收了第一个大面板（`SettingsDialog`，面 26）
   ⇒ 提过 **194**、残余 **113**（25 个视图）；🩸 但探针的「残余」是**名字级**的，
   那 −33 里只有 **25 个是真覆盖**（见阶段 109 专题 §109.2）。
3. **不判跨层副作用**：写盘 / 起进程 / 走 `IStorageProvider` 的按钮一律不点。
4. **`Voice` 分量（状态栏文案）只进读数、不进断言** —— 断言它等于把本地化文案冻结。
5. **顺序是被测语义**：数组顺序 = 点击顺序，改顺序等于改语义（表里已写进类注释与各条 `Why`）。
6. ⚠ **本阶段也没有把「软件侧」清空**：还开着 **#39**（本阶段只覆盖了它的「事件接线」
   一半里的一刀）与 **#45**（Dock 面板用例与大队同进程僵死、**根因未定**）；
   **#61p / #61q / #61r / #61s / #61k** 属口径 / 产品决定，**#58** 与 §5 属环境限制。
7. `TrendConfigView` / `SimulatorView` / `AiChatPanelView` 等**刻意没进首批**：
   它们带 500 ms 刷新定时器或 `DataLogger`（真写盘）⇒ 进面之前要先确认「短命定时器
   会不会跨用例留痕」，那是**独立的一刀**。
