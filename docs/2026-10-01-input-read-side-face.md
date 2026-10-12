# 输入框「谁读」面（阶段 196）

> 前两个面（`docs/2026-10-01-settings-textbox-wiring-face.md` 与
> `docs/2026-10-01-numberbox-wiring-face.md`）只算**接了线**的框 —— 那是**写侧**（改了就把值写进模型）。
> 本面补**读侧**：`Views/` 下还有一大批具名输入框**没有接线**，它们"不是缺口"的**前提是有人读它** ——
> 一个只被回填、从来没人读的输入框，用户打字之后什么都不会发生（`#90` 那一类形状）。

---

## 1. 实测（2026-10-01，阶段 196）

`Views/**/*.axaml` 下 **213 个具名输入框**（按「文件 ＋ 名字」计：`TextBox` ＋
`NumericUpDown`/`fa:FANumberBox`），四个桶**完美分账**（**阶段 199 更正后**：117 ＋ 92 ＋ 4 ＝ 213）：

| 桶 | 数量 | 含义 |
|---|---|---|
| **已接线** | **117** | 归三个接线面（写侧）：code-behind `+=` **91** ＋ **XAML 属性式 26**（阶段 199 才量出来） |
| **没接线、但有读点** | **92** | 由"点保存/应用"或动作处理器一次性读取 —— **有读点是"不是缺口"的证据** |
| **只读展示（登记）** | **4** | `IsReadOnly` 的回填框，存在的意义就是"给人看"（见 §3） |
| **没人读** | **0** | **这一格是要被守住的**（一旦非空 ⇒ 要么是缺口、要么该进登记表） |

> 🩸 **阶段 199 勘误**：这一面第一次发布时的读数是 `91 / 122 / 118 / 4` —— 它把 **26 个 XAML 属性式
> 接线**（`TextChanged="…"`）也算进了"没接线"。三个数都偏了，原因与更正见
> `docs/2026-10-01-xaml-attribute-wiring-face.md`。

> 118 里有 2 个是**经转发属性读**的：`AiScriptDialog` / `AiTemplateDialog` 的 `InputBox`
> （`protected override TextBox AiInput => InputBox;` —— 基类拿它读写）。它们按"裸名出现"算读。

## 2. 判据形状

**读点判定**（`HasReadSite(strippedCode, name)`，抽成纯函数好让它自己有判据）：

1. `X.member` 后面**不是赋值号** ⇒ 读（`==` 算读；`+=` / `-=` 算**写**）；
2. **裸名** `X`（实参 / 转发属性 `=> X`）⇒ 有人拿到了这个控件 ⇒ 读；
   ⚠ **但"写侧助手函数的实参"不算读** —— `AttachChangeHandler(TxtAuthor)` 只给这个框挂
   "改了就标脏"，它既不读值也不保存（阶段 196 nc1 就是这一刀：把 `SaveToProject` 里 `TxtAuthor`
   的读换成常量之后，这条口径必须让它变成"没人读"）；
3. 别的框的名字**不算**（`OtherBox.Text` / 前缀相同的 `TxtAuthorX` 都不算 —— 自测里有这两条）。

**四条断言**（3 个 `[Fact]`）：

- `EveryUnwiredInput_IsReadOrRegisteredAsDisplayOnly`：没接线的框必须**有读点**或在
  `DisplayOnly` 登记表里，否则**点名**（新加一个"只回填、没人读"的输入框会被当场挡住）；
  含三条**扫描面自检**（具名框 ≥ 200 · 已接线 ≥ 80 · 有读点 ≥ 100 —— 阶段 196 实测 213 / 91 / 118）。
- `DisplayOnlyRegistry_IsHonest`（**反向**）：每条登记都必须①真的存在（名字写歪 ⇒ 红）·
  ②**真的没有读点**（不许拿登记表藏一个其实有人读的框）·③**没接线**（接了线的归那两个面管）。
- `ReadDetection_SelfTest`（**量具自测**，9 条断言）：把"读"与"写"的形状钉住 ——
  这一面全靠这个判定，它一旦把"写"误判成"读"，整面就**假绿**（每个框都算有人读）。

## 3. 那 4 个"只读展示"框（登记表原文）

| 文件 | 框 | 理由 |
|---|---|---|
| `DeviceManagerView.axaml` | `TxtDeviceVersion` | `IsReadOnly="True"`：由设备信息回填（`info.HmiVersion` / `result.DeviceVersion`），没人读它 |
| `DownloadToHmiView.axaml` | `TxtSoPath` | `IsReadOnly="True"`：由「选择 .so」回填；真正下发用的是那个路径变量，不是这个框 |
| `Editors/VariablePicker.axaml` | `DisplayField` | `IsReadOnly="True"`：`UpdateDisplay()` 写选中变量的显示名，没人读它 |
| `UserManagerView.axaml` | `RoleNameBox` | 只读展示（`#50`：角色名与描述都是只读展示）：由 `ShowRoleDetail` 回填 |

## 4. 🩸 本阶段踩到的：**改口径忘了同步自测**

把"裸名算读"**收窄**成"写侧助手函数的实参不算读"时，我只改了判定、**没改自测里那条断言**
（`Assert.True(HasReadSite("AttachChangeHandler(TxtAuthor);", ...))`）⇒ 干净树上
`ReadDetection_SelfTest` 当场红（`Assert.True() Failure`）。

**这正是量具自测存在的理由**：判定改了、自测没跟上，红的是自测而不是"某条业务判据莫名其妙地红"。
处置：把那条断言改成 `Assert.False` 并写明"为什么不算读"（它就是 nc1 打的那一刀）。

## 5. 负控（5 支，`.workbuddy/tmp/s196-nc.py`）

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | 把 `SaveToProject` 里 `TxtAuthor` 的**读点**换成常量（只剩写 ＋ 写侧助手实参） | 读侧面 | `ProjectSettingsView.axaml::TxtAuthor` |
| nc2 | 往 `ProjectSettingsView.axaml` 加一个**只有写、没人读**的具名框 | 读侧面 | `ProjectSettingsView.axaml::NcUnreadBox` |
| nc3 | `DisplayOnly` 表里把一个名字写歪 | 登记表双向差集 | `DeviceManagerView.axaml::TxtDeviceVersion`（框没了登记） |
| nc4 | 往 `DisplayOnly` 塞一个**其实有读点**的框 | 登记表反向判 | `这些框登记为「只读展示」，但代码里有读点 … ::TxtAuthor` |
| nc5 | 把读点判定里的"赋值"判定改成恒假（`isAssign = false`） | **量具自测** | `ReadDetection_SelfTest`（写被误判成读 ⇒ 整面会假绿） |

5 支逐支独立日志（`.workbuddy/tmp/s196-nc{1..5}.log`）、每腿限时、开跑前扫残留注入，
还原后 3 个文件的 SHA256 逐字节相同。
🩸 其中 nc5 的第一版注入**构建不过**（只锚了 `var isAssign = after.StartsWith("=", …)` 那一行，
把 `&& !after.StartsWith("==", …)` 留在原地）⇒ 改成锚**两行**表达式才成立 ——
"负控自己必须先可编译"这条纪律再一次用上（s188 起）。

## 6. 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2252 / 315 / 781 / 8`（合计 **3622**）
＝ **3622 通过 / 0 失败**（阶段 195 为 3619，**＋3** ＝ 本面的 3 条判据）；
全量 App.UiTests **781 / 0**（连跑 2 次，40 / 38 s）；`UnusedBaseline` **不变 1109**（本阶段没加 Lang 常量）；
**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

## 7. 仍未做（如实留开）

- **读点的粒度**：本面只判"有没有人读"，**不判"读得对不对"**（例：读了但算错 / 读了却写进了别的字段）。
  要往下一层走，得对每条读点断言"它写进了哪个模型字段"，那是**逐视图**的活（与两个接线面的驱动面同形）。
- **扫描面边界**：只扫 `Views/**/*.axaml` 里的 `TextBox` 与两种数字框 ——
  `ComboBox` / `CheckBox` / `Slider` 等别的输入族、以及 `Views/` 之外的输入不在内。
- **`IsReadOnly` 的判定靠人**：登记表里 4 条都是只读展示，但"是不是只读"写在 XAML 里，
  本面**没有**去核对 XAML 的 `IsReadOnly="True"`（把 XAML 属性搬进判据是一条独立的守卫）。

---

## 8. 阶段 197：把这一面往**行为**推一层（`ProjectSettingsView` 的 15 个框）

§2 的结构面只能证"**有人读**"；这一阶段给**最大的一簇**（`ProjectSettingsView` 里 15 个
「点应用/确定时一次性读取」的数字框）做**行为面**：**读得对不对**。

### 8.1 判据形状

15 个框**逐个驱动成互不相同的值**（且都**不等于起点值** —— 前提自检）→ 点一次「应用」
（`SaveToProject()` 一次性读走全部 15 个框）→ **逐字段**断言模型里是"自己那个框"的值。

- 抄错一行（`WatchdogTimeoutMs` 读成 `NumHeartbeat`）⇒ 那一格读到**别的框**的值 ⇒ 红（nc1 实测
  `读到 '2000'，期望 '8000'`）；
- 保存时写常量（不读框）⇒ 红（nc2 实测 `读到 '30'，期望 '45'`）；
- 三条前提自检：① 15 个框回填的都是模型值 · ② 手势值 ≠ 起点值（否则"抄对了"与"压根没抄"同形 ——
  阶段 195 在 `ScreenManagerView` 上踩过这条**假绿**）· ③ 点「应用」**之前**一个都没落盘
  （落了盘说明它其实有接线，该归接线面）。
- 另外两条：`Specs_AreDistinct`（表内自检：15 条手势值 / 字段路径 / 框名**各自互异**）·
  `TheseBoxes_ReallyHaveNoDirectWiring`（用扫描再核一遍"这 15 个确实没直接接线" ——
  否则本面就与接线面**重复记账**了）。

### 8.2 §112 登记表补一条 XAML 依据

4 个「只读展示」框的登记原先只是**一句话**；这一阶段加判据
`DisplayOnlyRegistrations_AreReallyReadOnlyInXaml`：每个登记必须在 XAML 里真的带
`IsReadOnly="True"` —— 否则"只读展示"这个理由就是空话（一个**可编辑**、又没人读的输入框
恰恰是本面要抓的缺口）。nc3 去掉 `RoleNameBox` 的 `IsReadOnly` ⇒ 当场红。

### 8.3 负控（5 支，`.workbuddy/tmp/s197-nc.py`）

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | 抄错一行（Watchdog 那格改读 Heartbeat 的框） | 保存路径行为面 | `NumWatchdog 驱动 8000 并点「应用」后，RuntimeConfig.WatchdogTimeoutMs 读到 '2000'，期望 '8000'` |
| nc2 | 保存时写常量（不再读框） | 保存路径行为面 | `NumSessionTimeout … 读到 '30'，期望 '45'` |
| nc3 | 去掉 `RoleNameBox` 的 `IsReadOnly="True"` | 只读展示的 XAML 依据 | `UserManagerView.axaml::RoleNameBox（这个 TextBox 没有 IsReadOnly="True"）` |
| nc4 | 登记表里塞一个没登记的框（`TxtAuthor`） | 登记表反向判 | `这些框登记为「只读展示」，但代码里有读点 … ::TxtAuthor` |
| nc5 | 我自己的表里把两行手势值改成相同 | 表自检（互异） | `Assert.Equal() Failure: Expected 15 / Actual 14` |

5 支逐支独立日志（`.workbuddy/tmp/s197-nc{1..5}.log`）、每腿限时、开跑前扫残留注入，
还原后 5 个文件的 SHA256 逐字节相同。

### 8.4 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 785 / 8`（合计 **3626**）
＝ **3626 通过 / 0 失败**（阶段 196 为 3622，**＋4**）；
全量 App.UiTests **785 / 0**（连跑 2 次，38 / 37 s）；`UnusedBaseline` **不变 1109**；
**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

### 8.5 仍未做（本面后续的配方）

- **其余视图的"读点行为面"**：同形照抄即可 —— 找那个视图的**保存路径**（`Save*` / `BtnApply` /
  `BtnOk`），把那一簇框逐个驱动成互异值、点一次保存、逐字段断言。候选：
  `DeviceManagerView`（13 个框，读点在**各个动作处理器里**，要连服务一起驱动 ⇒ 成本高）·
  `DownloadToHmiView`（`TxtSoPath` 是只读展示；其余走 `SaveToProject`）等。
- **读点的静态明细**：现在是"有没有人读"＋"这一簇读进了哪一格"；还没做的是
  **逐读点**把"哪个读点写进哪个字段"变成表（本阶段只对 `ProjectSettingsView` 那 15 个做了行为版）。


---

## 9. 阶段 198：同一个视图的**另一半**（9 个文本输入框）

§8 做的是 `ProjectSettingsView` 的 **15 个数字框**；这一阶段补上同一个视图的 **9 个文本输入框**
（`TxtAuthor` / `TxtCompany` / `TxtDescription` / `TxtProjectVersion` / `TxtDefaultBgColor` /
`TxtHistoryPath` / `TxtOutputDir` / `TxtAuditPath` / `TxtDefaultRoleId`）—— 同一个保存路径
（`SaveToProject()`，一次「应用」读走全部），**多验一条口径：空值落哪**。

### 9.1 判据形状（两条口径，两次「应用」）

| 支 | 手势 | 断言 |
|---|---|---|
| **非空** | 9 个框**各带两侧空白**的互异值 → 点「应用」 | 每一格读到**裁剪后**的原值（9 条） |
| **清空** | 9 个框都写空串（起点刻意全非空）→ 点「应用」 | **6 个落 `null`** ＋ **3 个落各自的兜底**：`ProjectVersion → "1.0.0"` · `OutputDirectory → "./export"` · `DefaultRoleId → "role-operator"` |

> 那三条兜底**只有"清空"这一支才看得见** —— 接线面照不到（这些框根本没接线），
> 结构面也照不到（它只问"有没有人读"）。

其余三条判据：`Specs_AreDistinct_AndFallbacksCoverThemAll`（表内自检：9 条文本 / 字段路径 / 框名互异，
且**兜底表与手势表一一对应** —— 少写一条兜底会漏判）· `TheseBoxes_ReallyHaveNoDirectWiring`
（前提：这 9 个确实没有直接 `TextChanged` 接线，否则该归接线面）· 以及两条前提自检
（起点 ≠ 手势值；点「应用」之前一个都没落盘）。

**刻意不在这面里的一个框**：`TxtRuntimeUserPassword` —— 它**不由** `SaveToProject()` 读，
而是「设置口令」按钮读走（那一跳归运行时用户面）。

### 9.2 负控（5 支，`.workbuddy/tmp/s198-nc.py`）

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | 抄错一行（Author 那格改读 `TxtCompany` 的框） | 非空那面 | `TxtAuthor 驱动 '  张三  ' … Author 读到 '某公司'，期望 '张三'` |
| nc2 | 去掉 `Company` 那一行的 `Trim()` | 非空那面 | `… Company 读到 '  某公司  '，期望 '某公司'` |
| nc3 | 改掉 `ProjectVersion` 的兜底值（`1.0.0` → `0.0.1`） | 清空那面 | `清空后读到 '0.0.1'，期望 '1.0.0'` |
| nc4 | `Description` 的空分支从 `null` 改成 `""` | 清空那面 | `清空后读到 ''，期望 '(null)'` |
| nc5 | 兜底表里删掉一条（`TxtCompany`） | 表自检 | `Assert.Equal() Failure: Collections differ` |

5 支逐支独立日志（`.workbuddy/tmp/s198-nc{1..5}.log`）、每腿限时、开跑前扫残留注入，
还原后 2 个文件的 SHA256 逐字节相同。

### 9.3 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2252 / 315 / 789 / 8`（合计 **3630**）
＝ **3630 通过 / 0 失败**（阶段 197 为 3626，**＋4**）；
全量 App.UiTests **789 / 0**（连跑 2 次，38 / 38 s）；`UnusedBaseline` **不变 1109**；
**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

### 9.4 仍未做

`ProjectSettingsView` 的**保存路径**这一簇到此**两半都覆盖了**（15 数字 ＋ 9 文本）；
剩下的读点行为面在**别的视图**（配方见 §8.5）：`DeviceManagerView` 那 13 个的读点散在
**各个动作处理器**里（要连服务一起驱动 ⇒ 成本高）· `DownloadToHmiView` / 别的面板的
"点保存一次性读取"框。**逐读点的静态明细**（"哪个读点写进哪个字段"的表）仍未做。
