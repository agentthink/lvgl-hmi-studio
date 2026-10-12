# `SettingsDialog` 的 TextBox 驱动面（阶段 188，闭环 `#88`）

> 起因是阶段 187 量到的一件事：**程序化写 `TextBox.Text` 不触发 `TextChanged`**（真实输入才触发）
> ⇒ `SettingsDialog` 里那一族 `TextBox → 设置` 的接线**从来没有判据驱动过**。
> 这与"按钮点了没反应"是同一类缺口，只是入口换成了输入框。

---

## 1. 缺口长什么样（`#88` 的原文）

| 框 | 写入的设置 | 当时的判据面 |
|---|---|---|
| `TxtAccentColor` | `AppThemeService.CustomAccent` | **0** |
| `TxtCanvasBgColor` | `Display.CanvasBackgroundColor` | **0** |
| `TxtLvglDir` / `TxtSimWorkDir` | `Simulator.LvglDir` / `WorkDir` | **0** |
| `TxtT113SdkRoot` / `TxtT113Toolchain` | `Device.T113SdkRoot` / `T113ToolchainGcc` | **0** |
| `TxtAutoExportDirectory` | `AutoExport.Directory` | 阶段 187 补了 1 条（本面之外） |

真实输入下它们都工作（阶段 188 的驱动一次全绿就是证据）；问题是**没有任何判据**，
摘掉一条接线、或者把 `.Trim()` 删掉，都不会红。

## 2. 口径：判据要**按框架的做法**驱动

程序化赋值不触发 `TextChanged`，所以驱动必须补上框架会做的那一步：

```csharp
box.Text = text;                                                     // 1) 写文本
box.RaiseEvent(new TextChangedEventArgs(TextBox.TextChangedEvent));   // 2) 补发事件（框架在真实输入时会做）
```

这与 `HostButtonEffectUiTests` 里"**ToggleButton 先翻转 `IsChecked` 再引发 `Click`**"是**同一条纪律**：
判据要把框架的动作补齐，否则驱动的不是生产路径。补发事件**不会**让判据变成恒真 ——
接线缺失时补发也没有人接（负控 nc1 证明），所以它仍然是有牙齿的。

## 3. 面：一份表 ＋ 双向差集

`SettingsTextBoxWiringTests` 的表里每条**手势**＝
（框名，所在分类面，设置名，**从设置读值的委托**，驱动文本，期望值，是否"应一字不变"）：

- **驱动**：逐条切到该控件所在的面（面板没显示 ⇒ 当场红，顺带证明"界面上真的够得着"）⇒
  写文本 ＋ 补发事件 ⇒ **读设置**（不是读界面）断言它变成了什么。
  一个控件可以有多条手势：强调色就有三条（合法 ⇒ 写、非法 ⇒ 不动、**留空 ⇒ 恢复默认**）；
  画布底色两条（合法 ⇒ 写、非法 ⇒ 不动）；四个目录各一条（**裁剪**是口径的一部分）。
- **双向差集（XAML 侧）**：`SettingsDialog.axaml` 里每个具名 `TextBox` 都必须在表里
  ⇒ 新增一个没人驱动的输入框会被点名。
- **双向差集（代码侧）**：每条 `X.TextChanged += …` 也必须在表里，**且**表里声明的每条接线
  在代码里必须真的存在 ⇒ "接了线没人驱动"与"表里说接了其实没接"都红。

## 4. 判据与负控

- App `SettingsTextBoxWiringTests` **3 条**：驱动 9 条手势（含两条 `Unchanged` 的校验手势，起点值由前提
  自检摆好，确保不是空转）· XAML 侧差集（扫描面下限 7）· 代码侧差集（扫描面下限 7）。
- **负控 5 支**（`.workbuddy/tmp/s188-nc.py`，逐支独立日志 ＋ 还原 SHA256 逐字节相同）：
  摘一条接线 · 去掉 `Trim` · **十六进制校验恒假** · 去掉"留空恢复默认" · XAML 里新增一个没人驱动的 TextBox。

### 🩸 本阶段踩到的两件事（都写进脚本头与判据注释）

1. **"去掉校验"那一版注入不可用**：第一版 nc3 把强调色的 `else if (IsValidHex(hex))` 改成恒真
   ⇒ 非法十六进制直达主题层，下游 `Color.Parse` 抛，而异常从 `RaiseEvent` 里穿出来时
   **headless 宿主不返回**（实测两次：`dotnet test` 挂到超时）。⇒ 改成"**合法值也拒**"
   （同样打在 `IsValidHex` 这唯一校验点上，但不把垃圾送下去）。
   **如实记下**："非法值不进设置"这两条手势因此**没有可用的负控**（不改校验就测不出，
   改了校验宿主会挂）—— 它们是"实现显然如此 ＋ 驱动判据覆盖写路径"的组合，不假装有负控。
2. **负控一个进程跑多腿时有连带风险**：某腿挂住 → 整个脚本被拖住 → 连 `try/finally` 都来不及跑
   ⇒ 注入残留在工作树里（本阶段真的发生过两次）。处置：① 每腿 `timeout` 限时；
   ② 上手先扫一遍"注入标记"（若还在 ⇒ 直接拒绝开跑并提示先还原）；
   ③ 单腿跑时用 `trap ... EXIT` 保证还原。

## 5. 这条纪律还能用在哪儿（后续候选）

- 本仓其它视图里同样有 `TextBox → 模型` 的接线（属性面板的数值/文本输入尤其多）；
  把"驱动器"推广成**跨视图的一份面**（扫所有视图的 `TextBox` ＋ `TextChanged` 接线 ＋ 逐条驱动）
  是后续候选 —— 本阶段只闭环了 `SettingsDialog` 这一族，**没有**声称覆盖全仓。
- 同类入口还有 `NumericUpDown.ValueChanged`（本阶段顺带在 `NumAutoExportInterval` 上证明了它**程序化赋值可以驱动**，
  与 `TextBox` 不同）与 `ComboBox.SelectionChanged`（后者已在阶段 115 的选择族面内）。

---

## 6. 阶段 189：把这一族**推广成一张跨视图的账**（普查面）

阶段 188 只驱动了 `SettingsDialog`。这一阶段把"到底还有多少同类接线没人管"量清楚，
并把"**新增一条没人管的接线**"变成红。

### 6.1 实测（2026-10-01）

`Views/` 下共 **153 个具名 `TextBox`**，其中 **57 条**真的接了 `TextChanged`：

| 面 | 含义 | 规模 |
|---|---|---|
| **面 A** | 本文件的具名 `TextBox` ∩ 本文件的 `TextChanged` 接线（按名**可**驱动） | **11 个文件 / 48 条**（`SettingsDialog` 的 7 条已驱动，其余 10 个文件只普查） |
| **面 B** | 接了线但**按名找不到** —— 全是代码里 `new` 出来的输入框（例：`var txtParamName = new TextBox { … }`） | **4 个文件 / 9 条**（现有驱动法够不着，需要一个不同的入口） |

> 其余 96 个具名 `TextBox` **没有** `TextChanged` 接线 —— 那**不是**缺口：它们由"点保存/应用"那条路
> 一次性读取（例如 `DeviceManagerView` 的 13 个输入框）。所以这一族的面只算**接了线**的那些。

### 6.2 判据（`AppTextBoxWiringCensusTests`，4 条）

1. **面 A 双向差集**：逐文件条数必须与表一致 —— 新增一条没人管的接线（无论新文件还是既有文件）会红；
   接线被删而表没同步也红（扫描面自检：总数 ≥ 40）。
2. **面 B 双向差集**：按名找不到的那一族同样数清楚（自检：总数 ≥ 5）。
3. **"已驱动"不许是空话**：状态列只许有一个 `driven`（`SettingsDialog`），且驱动判据
   `SettingsTextBoxWiringTests` 里必须真的出现在该文件的**每一个**具名 `TextBox`。
4. **每条接线恰好进一个面**：`面A(文件) + 面B(文件) == 该文件的接线总数`。
   ⚠ 一个文件**可以同时出现在两个面里**（`ProjectSettingsView`：2 条具名 ＋ 1 条按名找不到）——
   所以"不重叠"是**接线级**的性质，不是文件级的（这一条我第一版写错了：断言两个面的**文件集**不相交，
   当场被 `ProjectSettingsView` 红）。

### 6.3 把某个文件升级成"已驱动"的配方

1. 建该宿主的窗口（**生产构造**）；
2. 表里每条手势＝（框名 · 驱动文本 · **从模型读值的委托** · 期望值 · 可选"应一字不变"）；
3. 驱动时写文本 ＋ **补发 `TextChangedEvent`**（程序化赋值不触发，见 §2）；
4. 先切到该控件所在的面/页（够不着的地方不算驱动面），并把本文件在普查表里的状态改成 `driven`、
   指向新的判据类（第 3 条判据会核对"driven 的必须真被驱动判据覆盖"）。

**未覆盖（如实留开）**：面 A 剩下 10 个文件 / 41 条、面 B 全部 9 条 —— 逐条驱动要各自的夹具与读面，
按宿主逐个扩（与 `HostButtonEffectUiTests` 长到 23 个宿主的路径一致）。

### 6.4 负控（4 支）

| 支 | 注入 | 期望红 |
|---|---|---|
| nc1 | 在**表外**视图（`TagMonitorView`）新增一条具名框接线 | 面 A 差集 |
| nc2 | 摘掉表内一条接线（`AuditLogView.FilterBox`） | 面 A 条数漂移 |
| nc3 | 在 `CrossReferenceView` 新增一个**代码创建**的输入框接线 | 面 B 差集 |
| nc4 | 驱动判据里把一个框名写歪（等于该框没人驱动） | "已驱动"那条 |

---

## 7. 阶段 190：把「每个具名框都接了线」的三个视图升级成 `driven`

阶段 189 把这一族推广成一张账（11 文件 / 48 条），但只有 `SettingsDialog` 那 7 条是 `driven`。
这一阶段把其中**三个「每个具名 `TextBox` 都接了线」**的视图升级成 `driven`。

### 7.1 为什么先挑这三个（不是随手一刀）

第 3 条判据（"已驱动不许是空话"）的口径是：**驱动表必须覆盖该文件的每一个具名框**
（见 §6.2 第 3 条 —— 它比"覆盖每一条接线"更严，因为一个框可以没有接线）。
全仓面 A 的 11 个文件里，只有这三个 + `SettingsDialog` 满足「具名框全部接了线」：

| 文件 | 具名框 | 其中接线 | 其余 7 个文件的反例 |
|---|---|---|---|
| `AlarmConfigView.axaml` | 7 | 7 | `CommunicationConfigView` 10 框只接 **4** |
| `AnimationEditorView.axaml` | 9 | 9 | `PropertyPanelView` 21 框只接 **1** |
| `AuditLogView.axaml` | 1 | 1 | `ProjectSettingsView` 12 框只接 2（＋1 条按名找不到） |

⇒ 升级它们**不需要**放宽判据（把"没接线的框"也硬塞进驱动表是假账：那些框走"点保存/应用"一次性读取，
本来就不是缺口）。面 A 还剩 **7 文件 / 31 条**，面 B 全部 **9 条** —— 见 §7.5。

### 7.2 三个驱动面（6 条判据 ＝ 3 ×［驱动 ＋ 单文件双向差集］）

| 文件 | 框 | 手势 | 被写进期望值的口径 |
|---|---|---|---|
| `AlarmConfigView` | 7 | **10** | 名称/描述**不裁剪**（原样写模型）· `int/double.TryParse` 失败**不动** · 位偏移**钳到 0…63** |
| `AnimationEditorView` | 9 | **12** | 描述 **`Trim`** · 时长负数被 **`Math.Max(0,…)`** 抬回 0 · 关键帧时间**钳到 0…100** · 解析失败不动 |
| `AuditLogView` | 1 | **4** | 过滤**先 `Trim`** · 过滤到空集 · **清空回到全量**（不是留在上一次结果上） |

- 每个面都走**生产构造**：真 `AlarmConfigView` ＋ 真 `AlarmGroup`/`AlarmConfig`；真
  `AnimationEditorView` ＋ 真 `AnimationConfig`（关键帧那两条先真的点 `BtnAddKeyframe` 再选中它）；
  真 `AuditLogView.Attach(auth)`（审计条目由**真的** `CreateUser` 走生产审计路径造出）。
- 每条手势都先做**前提自检**（编辑面板真的开了、回填的真是模型值、关键帧面板真的显示了），
  否则"什么都没跑"会以假绿通过。
- 驱动的共享原语收进 `TextBoxWiringFace`（`Find` / `Write` ＝ 写文本 ＋ **补发 `TextChangedEvent`** /
  `AssertFullyDriven` ＝ 单文件双向差集）；`SettingsTextBoxWiringTests` 与普查面也改用它 ——
  扫描正则只有**一处**实现，免得"普查说 7 条、驱动面说 6 条"这种读数只能靠人肉发现。

### 7.3 普查面第 3 条的推广

`OnlySettingsDialog_IsMarkedDriven_…` 换成 `EveryDrivenFile_HasADriverTestThatCoversItsTextBoxes`：

1. 每一行状态只许是 `driven` / `census`（防拼错导致该行被静默跳过）；
2. 每个 `driven` 行必须给出**存在**的驱动判据文件，且该文件里出现它的**每一个**具名框；
3. 反过来，`census` 行**不许**写驱动判据 —— 状态列要么被驱动、要么如实只普查，混着写会让"还剩多少没管"失真。

### 7.4 负控（5 支，`.workbuddy/tmp/s190-nc.py`）

| 支 | 注入 | 期望红 |
|---|---|---|
| nc1 | 摘掉 `AlarmConfigView` 的**表内**接线（阈值） | 报警驱动面 |
| nc2 | 摘掉 `AnimationEditorView` 的表内接线（关键帧时间） | 动画驱动面 |
| nc3 | 摘掉 `AuditLogView` 唯一那条接线（过滤框） | 审计驱动面（证明普查那 1 条不是空话） |
| nc4 | **保留接线**、只去掉 `ApplyFilter` 的 `Trim()` | 审计驱动面的**裁剪手势**（打的是行为，不是"接线在不在"） |
| nc5 | `driven` 行的驱动判据名指向不存在的文件 | "已驱动不许是空话" |

5 支逐支独立日志（`.workbuddy/tmp/s190-nc{1..5}.log`）、每腿限时、开跑前先扫残留注入，
还原后 5 个文件的 SHA256 逐字节相同。

### 7.5 仍未做（如实留开）

面 A 剩下 **7 个文件 / 24 条**（🩸 阶段 191 更正：这里原写 **31 条**，是手抄错 ——
逐项 `4+1+2+1+6+3+7 = 24`；错因与同族教训见 §8.6）：
`CommunicationConfigView` 4 · `Editors/VariablePicker` 1 · `ProjectSettingsView` 2 ·
`PropertyPanelView` 1 · `RecipeEditorView` 6 · `ScriptEditorView` 3 · `SimulatorView` 7；
另有面 B 全部 **9 条**（代码里 `new` 出来的框，需要一个不同的入口）。
配方见 §6.3；⚠ 面 A 剩下这些文件**都有"没接线的具名框"**，所以它们的驱动面要么按"覆盖每条接线"的口径写，
要么先决定"没接线的框算不算已驱动"—— 这一步是**口径**问题，阶段 190 刻意没有替它们定
（**阶段 191 定了**：见 §8.1／§8.3）。

---

## 8. 阶段 191：面 A 的**三态分账**（driven / blocked / census）＋ `SimulatorView` 升级

阶段 190 结尾留了一句"先要定口径"。这一阶段把口径定下来（两条），顺着它把 `SimulatorView`
升级成 `driven`，并**量到一条真缺陷**（登记 `#89`）。

### 8.1 口径一：没接线的具名框要**显式登记**（`withoutWiring`）

老口径（"驱动表必须覆盖该文件的**每个**具名框"）只有在"具名框全部接了线"的文件上才成立。
面 A 剩下 7 个文件都有"没接线的具名框"（`PropertyPanelView` 21 框只接 1）。两种糊法都不能要：
把它们硬塞进驱动表是**假账**（那些框本来就不是缺口）；不管它们又会把"框在不在"这笔账丢掉。

⇒ `TextBoxWiringFace.AssertFullyDriven` 加 **`withoutWiring` 登记表**（名字 → 理由）：

| 方向 | 要求 |
|---|---|
| 接线 | 每条 `TextChanged` 都必须有驱动条目（同老口径） |
| 具名框 | 每个具名框要么在驱动面里、要么在登记表里 —— **"既没驱动也没登记"当场红** |
| 登记表（反查 XAML） | 表里的名字 XAML 里必须真有（名字写歪 ＝ 漏登记） |
| 登记表（反查接线） | 表里的框**不许**接了线（接了线就该进驱动面，不许在登记表里躲着） |
| 两处 | 同一个框不许同时出现在驱动面与登记表里 |

### 8.2 `SimulatorView`（7 条接线 ＋ 2 个没接线的框）升级成 driven

| 框 | 手势 | 口径 |
|---|---|---|
| `AmplitudeInput` · `OffsetInput` · `FrequencyInput` · `MinValueInput` · `MaxValueInput` · `NoiseInput` | 各写一条 ＋ `AmplitudeInput` 补一条**解析拒绝**（`abc` ⇒ 不动） | `TryUpdateDouble`：`double.TryParse(NumberStyles.Any, InvariantCulture)` 失败就不调 setter |
| `ManualValueInput` | 一条写 —— **前提**：先走生产处理器把信号切到 `Manual` | `OnManualValueChanged` 首行判 `Signal != Manual` 就 return；夹具把这个前提当场量出来（信号位 ＋ 框的 `IsEnabled`） |
| `RegAddressInput` · `RegValueInput` | **进登记表**（没有 TextChanged） | "读/写寄存器"按钮**点击时**一次性读取 —— 改一个字就写一次寄存器不是要的语义 |

### 8.3 口径二：`blocked`（接线在，但**前提不满足**）—— 并量到真缺陷 `#89`

`ScriptEditorView`（4 个具名框 / 3 条接线）是面 A 里最特别的一个：它**自己是 `ChromeWindow : Window`**，
三个字段处理器（`OnScriptFieldChanged` / `OnCategoryChanged` / `OnCodeTextChanged`）首行都判
`Parent == null` 就 `return`。

阶段 157 只在**直接 `Show()`** 的宿主里量过 `Parent == null`，把它记成"夹具装不出 `Parent`"。
本阶段按**生产形状**（`MainWindow.OpenScriptEditorDialog` / `Wiring.Panels.cs` 都是
`dialog.ShowDialog(this)`）重建宿主并当场量：

| 读数 | 值 |
|---|---|
| `view.Owner` | **非 null**（生产形状确实装上了 owner） |
| `view.Parent` | **null** |
| 驱动 `TxtScriptName` / `TxtDescription` / `TxtCodeEditor` 之后的模型 | **一字不动** |

⇒ **不是夹具问题**：真实输入下 `TextChanged` 会触发，处理器首行就返回 —— 脚本的**改名 / 改描述 /
改代码都不落模型**（`ScriptListChanged` 也不触发 ⇒ 不标脏、不触发自动保存；500ms 去抖语法校验
从不启动）。`InsertTemplate`（`_currentScript.Code = newText;`）与 AI 生成两条**按钮**路径之所以"能用"，
是因为它们**自己显式写了模型** —— 那是绕着这条守卫打的补丁。

**登记 `#89`**（疑似产品缺陷）：修法与波及面写在登记册。本阶段先**取证**：
`ScriptEditorTextBoxWiringTests` 把"当前实现的行为"（一字不动）断言下来，并在断言消息里写上
"若这是有意修好的，请把本文件改成驱动面并同步普查表"。
这条判据**承重** —— 负控 nc4 摘掉那句守卫，它当场红，且失败消息里模型读数是 `开机脚本 → 改过名`。
**（阶段 192 已修：见 §9 —— 并且"修好第一层才露出第二层"也记在那里。）**

### 8.4 判据与负控

- App **4 条**：`SimulatorTextBoxWiringTests` 2（驱动 8 条手势 ＋ 单文件双向差集含登记表）·
  `ScriptEditorTextBoxWiringTests` 2（前提取证 ＋ 单文件双向差集"前提不满足版"）。
- 普查面第 3 条扩成**三态**：`driven`（驱动判据）／`blocked`（前提判据）／`census`（不许写判据）；
  三态各自的下限自检与"判据文件必须覆盖该文件每个具名框"照旧。
- **负控 5 支**（`.workbuddy/tmp/s191-nc.py`）：

| 支 | 注入 | 期望红 |
|---|---|---|
| nc1 | 摘掉 `SimulatorView` 的表内接线（幅度） | 驱动面 |
| nc2 | 往 `SimulatorView.axaml` 新增一个**没接线也没登记**的具名框 | 单文件双向差集 |
| nc3 | 往登记表里塞一个**其实接了线**的框 | 登记表反查 |
| nc4 | 摘掉 `ScriptEditorView` 的 `Parent == null` 守卫 | `blocked` 前提判据（承重证据） |
| nc5 | 把只普查的文件标成 `driven` 并指向不含它框名的判据 | 普查面"覆盖每个具名框" |

5 支逐支独立日志（`.workbuddy/tmp/s191-nc{1..5}.log`）、每腿限时、开跑前扫残留注入，
还原后 5 个文件的 SHA256 逐字节相同。

### 8.5 仍未做（如实留开）

面 A 剩 **5 文件 / 14 条**：`CommunicationConfigView` 4 · `Editors/VariablePicker` 1 ·
`ProjectSettingsView` 2 · `PropertyPanelView` 1 · `RecipeEditorView` 6 ——
新口径之后它们**都成了可做的**（没接线的框进登记表即可），差的是各自的夹具与读面（配方 §6.3）。
另：`ScriptEditorView` 那 3 条要等 `#89` 修好（修完必须把 `blocked` 改成 `driven`）；
面 B 9 条照旧。**（阶段 192 已修：那 3 条已转 `driven`，`blocked` 归零 —— 见 §9。）**

### 8.6 🩸 顺带更正一处**手抄错的合计数**（阶段 190 的 §7.5）

阶段 190 写"面 A 剩下 **7 个文件 / 31 条**"，**31 是错的** —— 逐项相加
`CommunicationConfigView 4 ＋ VariablePicker 1 ＋ ProjectSettingsView 2 ＋ PropertyPanelView 1 ＋
RecipeEditorView 6 ＋ ScriptEditorView 3 ＋ SimulatorView 7 = **24**`（`48 − 已驱动的 24 = 24` 也对得上）。
本阶段把专题 §7.5 与登记册 §106.6 两处活文档就地改掉（提交信息 `db2eae4` 不可改）。
错因与阶段 111「手写的合计数从邻近的另一个事实借了数字」同族：**合计数必须现算，不许凭印象写**——
这次的近邻是"面外 31 个接线控件"那一族的旧读数。

---

## 9. 阶段 192：修 `#89` —— 但**修好第一层才露出第二层**

§8.3 量出的缺陷（三个字段处理器首行 `Parent == null`，生产形状下 `Parent` 恒 null ⇒ 打字不落模型）
这一阶段修掉了。修的过程里又**逼出第二层**：把守卫摘掉之后，"改一个字就丢当前脚本"立刻现形。
两处都在 `ScriptEditorView.axaml.cs`（**产品代码 +63 行，几乎全是注释**）。

### 9.1 第一层：去掉 `Parent == null`（`#89` 本体）

三个处理器（`OnScriptFieldChanged` / `OnCategoryChanged` / `OnCodeTextChanged`）的条件从
`_isUpdating || _currentScript == null || Parent == null` 收成 `_isUpdating || _currentScript == null`。
防重入改由 `_isUpdating` 负责 —— 前提是**程序化回填全部收进 `_isUpdating` 窗口**，这一阶段把漏在外面的三处补上：

| 位置 | 原先 | 现在 |
|---|---|---|
| `PopulateEditPanel` | `TxtCodeEditor.Text = script.Code;` 在 `_isUpdating = false` **之后** | 挪进窗口内（否则一打开对话框就把代码"再写一遍"并点起去抖定时器） |
| `InsertTemplate` | `TxtCodeEditor.Text = newText;` 裸写（模型另有一行显式写） | 包进窗口（模型仍由那行显式写 —— 这条路径不依赖事件） |
| AI 生成后的刷新 | 四个控件裸写（模型已显式写好） | 包进窗口 |

> ⚠ 阶段 157 的宿主注释曾说"`TxtCodeEditor.Text = script.Code` 会触发 `OnCodeTextChanged`"，
> 而阶段 187 的探针实测"程序化赋值**不**触发"。两种说法至今没复测，**所以窗口包裹按"会触发"写** ——
> 在两种框架行为下都安全（会触发 ⇒ 被 `_isUpdating` 挡掉；不触发 ⇒ 本来也无事）。

### 9.2 第二层：`RefreshScriptList()` 会把"正在编辑的那一条"清掉

`OnScriptFieldChanged` → `RefreshScriptList()` → `ScriptList.Items.Clear()` **引发一次
`SelectionChanged`**（选中 → null）→ `OnScriptSelectionChanged` 的 `else` 分支
（`Items.Count == 0`）把 `_currentScript` 置 **null**、`UpdateRightPanel()` 收起编辑面板；
而原来的"恢复选中"看的就是 `_currentScript`（已经 null）⇒ 永远不命中。

⇒ 症状：**改一个字 ⇒ 面板收起、当前脚本丢失，后面每个字都不落模型**。
⚠ 这一层在阶段 192 之前**走不到**（处理器被 `Parent == null` 整个挡掉了）—— 它不是"老 bug"，
是"修好第一层才露出来的第二层"（本仓第若干次同形：判据/修复到一起，下一层当场现形）。

**修法**（一处标记 ＋ 一处显式化）：

```csharp
_rebuildingScriptList = true;                       // 重建窗口
try { ScriptList.Items.Clear(); …重建… }
finally { _rebuildingScriptList = false; }          // ⚠ try/finally：中途抛了不许把选择处理永久禁掉

// OnScriptSelectionChanged 首行
if (_rebuildingScriptList) return;                  // 重建期间的"选中 → null"不是用户意图

// 恢复选中：按 Id 找回正在编辑的那一条；**找不到 ⇒ 显式清空并收起面板**
if (!restored) { _currentScript = null; UpdateRightPanel(); }
```

最后那一句是**把原来靠副作用实现的行为显式化**：以前"筛选到别的分类 ⇒ 编辑面板隐藏"是
`Items.Clear()` 那一下顺手清出来的；现在由重建窗口挡住副作用、再由这句如实清 —— 行为一样，来路清楚。

### 9.3 判据侧

- `ScriptEditorTextBoxWiringTests`：从**前提取证**（断言"一字不动"）改成**驱动面**（3 条手势：
  名称 / 描述 / **代码** → 模型），并断言 `ScriptListChanged` 真的抬了（脏标记 / 自动保存那条线靠它）。
  `Owner` 有、`Parent == null` **继续断言** —— 那是 `#89` 的根因现场，证明修的是"处理器不再依赖它"，
  而不是"`Parent` 碰巧变非 null 了"。
- 普查表 `ScriptEditorView.axaml`：`blocked` → `driven`（现在 **6 个 driven / 0 个 blocked**）。
- 🩸 `blocked` 归零 ⇒ 那一档**没有运行期覆盖**了 ⇒ 把「状态 × 判据文件」的组合规则抽成纯函数
  `StatusAllowsDriver` ＋ 自测 `StatusAllowsDriver_SelfTest`（7 条断言）——"机制留着但没人用"
  不会退化成"机制坏了没人知道"。
- 第五层宿主 `HostButtonEffectUiTests`：两条 `Inert`（`ChkEnabled` / `CboCategory`）→
  `Moves` / `SelMoves`，**期望值一个字没改**（改的只是"处理器到底跑没跑"）；
  `有Parent` 分量从"前提"降级成"读数"（不再有腿靠它承重）。

### 9.4 负控（5 支，`.workbuddy/tmp/s192-nc.py`）

| 支 | 注入 | 期望红 | 读数（日志里的失败正文） |
|---|---|---|---|
| nc1 | 把 `Parent == null` 守卫**注回**（模拟修复回退） | 驱动面 | `TxtScriptName … 读到 '开机脚本'，期望 '开机脚本改名'` |
| nc2 | 去掉**重建窗口**的阻挡 | 驱动面 | `TxtDescription … 读到 ''` ← **第二层的直接证据**（第一个字段改完 `_currentScript` 就没了） |
| nc3 | 驱动面把一个框名写歪 | 单文件双向差集 | 该接线"没人驱动" |
| nc4 | 产品侧不再写 `_currentScript.Code` | 驱动面 | 代码那格读回空串 |
| nc5 | `driven` 行改成 `blocked` 且判据文件不存在 | 三态/判据组合规则 | 「`blocked`」是空话（**0 行 blocked 那一档仍然咬人**，回应 §8.4 的担心） |

5 支逐支独立日志（`.workbuddy/tmp/s192-nc{1..5}.log`）、每腿限时、开跑前扫残留注入，
还原后 3 个文件的 SHA256 逐字节相同。

### 9.5 仍未做（如实留开）

面 A 剩 **5 文件 / 14 条**（`CommunicationConfigView` 4 · `Editors/VariablePicker` 1 ·
`ProjectSettingsView` 2 · `PropertyPanelView` 1 · `RecipeEditorView` 6）；面 B **9 条**照旧。
**（阶段 193 已收口面 A：见 §10。）**
⚠ 另有一件**如实记下的边界**：`OnScriptSelectionChanged` 的 `else if (Items.Count > 0) SelectedIndex = 0;`
分支现在只在重建窗口之外才有意义，本阶段**没有**为它单独造判据（它属于"点空白取消选中"那条用户路径，
本仓 UI 层目前不驱动那种手势）。

---

## 10. 阶段 193：面 A **收口** —— 最后 5 个文件 / 14 条接线全部 driven

`#89` 修完（§9）之后，面 A 只剩 5 个文件 / 14 条。这一阶段把它们全部升级成 `driven`：
**11 个文件 / 48 条接线一个不剩**（`blocked` 0 · `census` 0）。**产品代码 0 行改动**。

### 10.1 五个驱动面

| 文件 | 接线 | 手势 | 读的是哪一面 / 被写进期望值的口径 |
|---|---|---|---|
| `Editors/VariablePicker.axaml` | 1 | 4 | **变量树的两层叶子**（`SearchBox` → `RebuildTree`）：过滤 · **Trim** · 空集 · 清空回全量 |
| `CommunicationConfigView.axaml` | 4 | 8 | **`TxtBatchPreview` 的空/非空**（这条接线的全部产出就是那句预览）：闸门①条数 1…500 · 闸门②前缀 **Trim** · 起始序号/地址参与样例 |
| `ProjectSettingsView.axaml` | 2 | 2 | **草稿**里的 `RuntimeUser`（面板装载时深拷贝，点应用才写回）：用户名 **Trim** · 显示名**不裁剪** |
| `RecipeEditorView.axaml` | 6 | 8 | `RecipeItem` 模型：标签/默认值/排序号 · 单位**不裁剪** · **解析失败 ⇒ 抬到 `double.MinValue` / `MaxValue`**（不是"不动"） |
| `PropertyPanelView.axaml` | 1 | 5 | 选中控件上的 `VariableBindingData`：合法表达式**不裁剪** · 编译失败/空白**一个字段都不写** |

两条"够得着才算驱动面"的前提：`VariablePicker` 的 `VarTree` 在**未打开的 Popup** 里（控件仍在，
`RebuildTree` 直接写它）；`ExpressionInput` 出厂 `IsVisible="False"` —— 必须先把 `BindingKindCombo`
拨到「表达式」（**走生产处理器** `OnBindingKindChanged`）并断言它真的显示出来。

### 10.2 口径表又补了一张：面 B 的 `anonymousWired`

- **`withoutWiring`（阶段 191 立）** 这一片把 5 个文件的"没接线的具名框"全登记了
  （20 ＋ 10 ＋ 6 ＋ 2 ＋ 1 条，逐条写理由）。
- 🆕 **`anonymousWired`（面 B 认账）**：`ProjectSettingsView.axaml.cs` 里还有一条**按名找不到**的接线 ——
  `AttachChangeHandler<T>` 里的 `txt.TextChanged += (_, _) => _hasChanges = true;`。
  它是**泛型参数**，不是 `new` 出来的框 ⇒ 属于面 B，本面够不着，**单独认账**。双向判：
  按名找得到的框不许往里塞（那是把该驱动的接线往暗面里塞）；说有这么条接线、代码里没有也红。
- ⚠ 顺带**更正面 B 的描述**：面 B 的两种形态是「代码里 `new` 出来的框」＋「**辅助函数注入**（泛型参数）」
  —— 阶段 189 只写了前一种，而 `ProjectSettingsView` 这条是后一种
  （与阶段 115 的选择族面记的"接线有三种形态"同源）。

### 10.3 三条"顺序/前提"教训（本阶段踩到的）

1. **`CommunicationConfigView` 的闸门分辨**：四个框每次一起读，所以"预览为空"必须知道是**哪道闸门**造成的。
   首版把 `TxtBatchCount` 的初值当成 `0`（实际 XAML 写死 **`10`**）⇒ 判据当场红。
   处置：动作序列里先把其余输入摆成合法值，并把 XAML 初值写成**前提自检**（`1` / `10` / 空）。
2. **`PropertyPanelView` 的前提自检写错**：以为"切到表达式源"之前没有绑定，实测那一跳本身
   `GetOrCreateReadBinding()` 已经建了一条（`Expression = null`）⇒ 自检从 `Assert.Empty` 改成
   `Assert.Single` ＋ `Assert.Null(Expression)`。
3. **`ProjectSettingsView` 的深拷贝**：那两条接线写的是**草稿**、不是工程 ⇒ 判据读草稿，
   并加一条反面见证（`project.SecurityConfig.RuntimeUsers[0].Username` 仍是原值）；
   "草稿 → 工程"那一跳另有 `ProjectSettingsRuntimeUserUiTests` 看着，本判据不重复记账。

### 10.4 判据与负控

- App **10 条**（5 文件 × ［驱动 ＋ 单文件双向差集］）＋ 普查表 5 行 → `driven`（下限提到 **11** = 面 A 全量）。
- **负控 6 支**（`.workbuddy/tmp/s193-nc.py`）：

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | `VariablePicker`：过滤不再裁剪 | 变量树过滤面 | `过滤 '  温度  ' 后树里应当有「温度」，实际叶子：` |
| nc2 | `RecipeEditorView`：摘掉排序号那条接线 | 参数驱动面 | 排序号读回旧值 |
| nc3 | `CommunicationConfigView`：去掉条数**上界**闸门 | 预览面 | `驱动 '501' … 后预览应当为空，实际 '将生成 501 个变量: …'` |
| nc4 | `ProjectSettingsView`：用户名不再裁剪 | 运行时用户驱动面 | 用户名读回带空格的原文 |
| nc5 | `PropertyPanelView`：表达式改成裁剪后存 | 表达式驱动面 | `读到 'B * 2'，期望 '  B * 2  '` |
| nc6 | 普查面：一行改回 `census` | 面 A 下限 | `标了 driven 的文件只有 10 个 …（阶段 193 实测 11 个 ＝ 面 A 全量）` |

6 支逐支独立日志（`.workbuddy/tmp/s193-nc{1..6}.log`）、每腿限时、开跑前扫残留注入，
还原后 6 个文件的 SHA256 逐字节相同。

### 10.5 仍未做（如实留开）

**面 B 9 条**（`FaceplateTemplateEditorView` 6 · `ProjectSettingsView` 1 · `StyleEditorView` 1 ·
`UserManagerView` 1）—— `anonymousWired` 只是"认账"，不是"驱动"：这些接线没有 XAML 名，
要驱动它们得**换一个入口**（按代码里的构造点拿控件，或在辅助函数注入那条路上按调用点驱动）。
⚠ 其中 `ProjectSettingsView` 那条（`AttachChangeHandler` 的 `_hasChanges = true`）**其实够得着**
（调用点传的就是具名控件），只是它的效果（私有 `_hasChanges`）在这个面里没有探针 ——
要收它得先给"面板被标脏"这件事造一个见证（标题 `*` / `BtnApply` 使能），那是**下一刀**的口径。
**（阶段 194 已收口面 B：见 §11。）**

---

## 11. 阶段 194：面 B 收口 —— 9 条「按名找不到」的接线**各找各的入口**

面 A 收口（§10）之后，账上只剩**面 B**：4 个文件 / 9 条"接了线但按名字找不到"的接线。
这一阶段给每个文件一个**不同的入口**，把它们全部驱动起来。**产品代码 0 行改动**。

### 11.1 四个入口（都不按名字找控件）

| 文件 | 条数 | 入口 | 读的是哪一面 |
|---|---|---|---|
| `FaceplateTemplateEditorView.axaml` | 6 | 卡片是 `Border`（`Tag = index`）→ 里面一个 `Grid` → **按 (行, 列)** 取框（`Assert.Single` 把这个坐标契约**当场钉住**） | 驱动完点「保存」→ 读 `template.Parameters[0]`（产物） |
| `StyleEditorView.axaml` | 1 | **一条辅助函数线管着十个框**（`WireTextChanged(tb)`）：按"树序 ＋ `PlaceholderText == "#RRGGBB"`"取，并断言**恰好 10 个** | 直接读 `project.Theme`（`_theme` 就是它本人，不是克隆） |
| `UserManagerView.axaml` | 1 | 从 `view.OwnedWindows` 拿到「重置口令」对话框，再取其可视树里**唯一**的 `TextBox` / `Button` | 读确认按钮的 `IsEnabled`（这条线**没有模型落点**，产出就是按钮使能态） |
| `ProjectSettingsView.axaml` | 1 | 辅助函数里的**泛型参数** `txt` ⇒ 用 `TxtAuthor` **代表驱动** | 点「确定」后读 `project.Author`（`_hasChanges` 唯一的消费者就是 `OnOkClicked` 的 `if (_hasChanges) ApplyInternal();`） |

### 11.2 口径：面 B 的"认账"也变成**双向**（`AssertAnonymousFace`）

`TextBoxWiringFace` 新增 `AssertAnonymousFace(file, identifiers, floor)`：扫描到的接线标识必须与表
**完全一致** —— 新接线不认账 ⇒ 红；认了但代码里没有 ⇒ 红；**把具名框往里塞**也红（那是把该驱动的
接线往暗面里塞）。普查面的面 B 表加了 `Driver` 列：给了判据的行，那个判据文件里必须**逐条点名**
这些接线标识（判据 2 核对 —— 重命名后不更新就红）。
⇒ 面 A 用"每个具名框"核对，面 B 没有具名框，**改用接线标识**核对。

### 11.3 顺带更正一处口径（阶段 193 的一句话说错了）

阶段 193 我把 `TxtAuthor` / `TxtCompany` / … 这 9 个框登记为「**没有** TextChanged 接线」——
**不准确**：它们的 `TextChanged` 是**辅助函数注入**的（`AttachChangeHandler(TxtAuthor)`），
只是没有 `TxtAuthor.TextChanged +=` 这种**直接**写法。这一阶段把那 9 条的**理由**改成
「辅助函数注入（面 B 那条线），由 `TxtAuthor` 代表见证」——"该由谁驱动"不变，**来路**说清楚了
（同时 `AssertFullyDriven` 的文档也从"没接线"改成"没**直接**接线"）。

### 11.4 判据与负控

- App **8 条**：`FaceplateTemplateEditorTextBoxWiringTests` 3（6 条手势经保存落模板 ＋ 两条清空口径 ＋
  面 B 认账）· `StyleEditorTextBoxWiringTests` 2（十个框逐个写各自字段 ＋ 面 B 认账）·
  `UserManagerTextBoxWiringTests` 2（按钮使能态三档 ＋ 面 B 认账）·
  `ProjectSettingsTextBoxWiringTests` ＋1（`AuthorEdit_MarksDirty_AndOkPersistsIt`）。
- **负控 6 支**（`.workbuddy/tmp/s194-nc.py`）：

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | `Faceplate` 摘掉「描述」那条接线 | 参数驱动面 ＋ 扫描面下限 | `只扫到 5 条 TextChanged 接线 …（下限 6）` |
| nc2 | `Style` 的 `AddColorField` 不再接线 | 颜色框驱动面 | `第 0 个颜色框（PrimaryColor）驱动 '#000000' 后模型读到 '#2563eb'` |
| nc3 | `UserManager` 摘掉口令框那条接线 | 按钮使能态面 ＋ 扫描面下限 | `只扫到 0 条 TextChanged 接线 …（下限 1）` |
| nc4 | `ProjectSettings` 那条 `txt.TextChanged` 变空转 | `TxtAuthor` 代表见证 | `Assert.Equal() Failure: Strings differ`（确定不再保存） |
| nc5 | `Faceplate` 新增一条没认账的接线 | 面 B 逐条认账 | `这些接线没有认账（新接线必须进面 B 表）：ncBox` |
| nc6 | 普查面面 B 表条数写错 | 面 B 双向差集 | `条数对不上` |

6 支逐支独立日志（`.workbuddy/tmp/s194-nc{1..6}.log`）、每腿限时、开跑前扫残留注入，
还原后 5 个文件的 SHA256 逐字节相同。
🩸 其中 nc4 的第一版注入**构建不过**（把 `else if (ctl is TextBox txt)` 的分支体注释掉 ⇒ 语法错），
改成"接线还在但空转"才成立 —— 负控**必须自己先可编译**这条纪律又用上一次（s188 起）。

### 11.5 仍未做（"收口"不等于"全仓输入控件都驱动了"）

- 面 A / 面 B 都收口了（11/11 ＋ 4/4），但普查面**只算接了线的框**：`Views/` 下 153 个具名 `TextBox`
  里有 **96 个没有** `TextChanged` 接线（走"点保存/应用"一次性读取）—— 它们**不是**缺口，
  而且其中大多数有**别的**判据看着（如 `PropertyPanelWiringTests` 逐编辑器驱动）。
- 面外还有：**别的输入控件族**（`NumericUpDown.ValueChanged`、`ComboBox.SelectionChanged` 已在
  选择族面内、代码现造的 `CheckBox`…）与 `Views/` 之外（`Controls/`、设计器）的输入。
- ⚠ 同一处理器、**别的触发源**不算本面（例：`VariablePicker.FilterControlType` 是属性赋值，
  不是打字）——"接线"的边界是**文本框的 `TextChanged`**，这条边界写在 §1/§6.1。
- 🩸 **（阶段 199 勘误）** 本面（与数字框那一面）扫的是 **code-behind 形态**的接线（`X.TextChanged += …`）：
  `Views/` 下还有 **26 个具名 TextBox** 走 **XAML 属性式**（`TextChanged="OnX"`），
  而**两形态互斥**（26 条里没有一条同时在 code-behind 里）⇒ 本文件里"这些框没有 `TextChanged` 接线"
  之类的说法对这 26 个框是**错的**。第三形态自成一面 ⇒ `docs/2026-10-01-xaml-attribute-wiring-face.md`；
  受影响的登记表与分账都已同步更正。
- **（阶段 195）** **数字框族**（`NumericUpDown` / `fa:FANumberBox` 的 `ValueChanged`）
  已照本文件这一套做了一遍 —— 45 条接线（4 文件：面 A 43 ＋ 面 B 2），并修掉新判据逼出的 `#90`：
  专题 ⇒ `docs/2026-10-01-numberbox-wiring-face.md`。
