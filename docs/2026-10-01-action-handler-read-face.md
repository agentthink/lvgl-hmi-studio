# 「读点在动作处理器」这一族：第二个视图 ＋ 全 App 台账（阶段 207）
## —— `DownloadToHmiView` 4 个框可驱动化 ＋ 修 `#95` ＋ 26 → 22 条的读数修正

> 承阶段 205/206（`docs/2026-10-01-device-input-read-face.md`）：`DeviceManagerView` 那 13 个框
> 是这一族的第一个样本 —— 读点**不在保存路径**上，而散在动作处理器里，于是"有没有判据"
> 与"有没有注入缝"绑在一起。这一阶段把**同一族**做第二遍，并把**全 App**的这类框做成一张台账。

---

## 1. `DownloadToHmiView`：4 个框 ＋ 两条缝

| 框 | 读点 | 缝 |
|---|---|---|
| `TxtHost` / `TxtTcpPort` / `TxtBaudRate` | `OnOpenCloseClicked` 里建通道（原先就地 `new SerialDeviceChannel/TcpDeviceChannel`） | 复用阶段 206 的 **`IDeviceChannelFactory`** |
| `TxtRemoteDir` | `OnDownloadClicked` 里当**部署目标目录** | 新增 **`IHmiDeployService`**（原先 `_deployService = new()`） |

产品侧：`IHmiDeployService` 进 `DeviceServiceAbstractions.cs`（`HmiDeployService` 原样 `implements`）；
视图字段换成接口 ＋ 加 `_channelFactory`；新增一条 **`internal`** 注入构造（与 `DeviceManagerView` 同因：
公开会多出一条公共构造，`WindowProductionPathUiTests` 的「双路径」元检查会要求改判 `TwoPaths`）。

判据（`DownloadToHmiInputReadFaceTests`，3 条）：假工厂记 `(host, port)` / `(port, baud)`；
假部署服务记 `remoteSourceDir`；**空白目标目录 ⇒ 回退默认**（见 §2）；另加二选一台账
（`TxtSoPath` 是只读展示框，指 §112 的 `DisplayOnly`）。

## 2. 🩸 写判据时撞出的缺陷 `#95`

原代码：

```csharp
var remoteDir = TxtRemoteDir.Text?.Trim() ?? HmiDeployService.DefaultSourceDir;
```

`??` 只在 **null** 时回退。而这一格在 XAML 里有默认文本 `/mnt/UDISK/hmi/src`，
**用户把它清空**时 `Text` 变成 `""`（**非 null**）⇒ `remoteDir = ""` ⇒ 传给部署服务空目录：
`mkdir -p ` 没有参数、上传目标为空路径 ⇒ **文件落到设备的当前目录**（静默走错地方）。

修法：口径改成"**空/空白都回退默认**"（与其它输入框"空白视为未填"一致）：

```csharp
var remoteDir = string.IsNullOrWhiteSpace(TxtRemoteDir.Text)
    ? HmiDeployService.DefaultSourceDir
    : TxtRemoteDir.Text.Trim();
```

判据就是上面那条"空白 ⇒ 收到默认目录"；**负控 nc3** 把修回退掉 ⇒ 它当场红。

> ⚠ 这是**第三类**"静默丢弃/静默走错"的形态（`#90` 是写侧丢、`#91` 是读侧丢、
> 这一条是**回退口径只认 null 不认空白**）—— 三者都是"判据写出来才看得见"的那一类。

## 3. 全 App 台账：`ActionHandlerReadInventoryTests`（22 条）

扫描口径（可复算）：`Views/**/*.axaml` 的具名 `TextBox`／数字框 →
**跳过接线的**（⚠ **两种形态都跳**：code-behind `+=` 与 XAML 属性式 —— 阶段 199 的教训）→
用「谁读」面那把尺子（`AppUnwiredInputReadFaceTests.HasReadSite`）判有没有读点 →
读点所在方法名落在 `On*Clicked|On*Async|On*Changed|On*Send|On*Login|…|Try*|Search|Append*|Generate*` 模式里 ⇒ 进台账。

| 组 | 条数 | 状态 |
|---|---|---|
| `DeviceManagerView` | 12 | ✅ driven（阶段 205/206） |
| `DownloadToHmiView` | 4 | ✅ driven（本阶段） |
| `AiIconDialog` | 4 | ⏳ registered（`AiPreviewManager` 是 sealed 且自己 new provider ⇒ 生成要走真实 LLM） |
| `TagMonitorView.TxtWriteValue` | 1 | ⏳ registered（要真设备/模拟器） |
| `BuildRunDialog.OutputLog` | 1 | ⏳ registered（只读日志框，扫到它是因为读 `Text.Length` 截断） |

三道核对：① 扫描 ↔ 台账**双向闭合**；② `driven` 的判据文件必须**真的点名**该框；
③ `registered` 的理由 ≥30 字；④ `registered` 的**读点确实在动作处理器里**（抽查，防止把能驱动的框
塞进登记表当遮羞布）。

### 3.1 读数修正：26 → **22**（我自己的普查口径错过两次）

- **`DeviceManagerView.TxtDeviceVersion`**：我在 Python 普查里把它算成"读点在 `OnDetectClicked`"——
  那是**我的正则错了**（匹配到名字后直接看后面是不是 `=`，于是把 `X.Text = …` 这种**赋值**也当成读）。
  它是**只写不读**的展示框（归 §112 的 `DisplayOnly`）。
- **三个 `SearchBox`**（`ToolboxView`/`ResourceLibraryView`/`FaceplateLibraryView`）与
  **四个 `PropertyPanelView` 色框**：它们是 **XAML 属性式接线** ⇒ 归阶段 199 的接线面与
  `AppXamlAttributeWiringFaceTests` 的登记表，不该出现在"**没接线**、读点在动作处理器"这张账里。
  扫描口径因此补上"属性式也跳"。

⇒ 这正是"**台账必须由机器扫描 ＋ 双向闭合**"的价值：口径错在哪一格，第一次跑就报出来。

## 4. 负控（6 支，`.workbuddy/tmp/s207-nc.py`）

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | TCP 不走通道工厂 | `ChannelInputs…` | `等不到：通道工厂收到 TCP 参数（已等 10000 ms）` |
| nc2 | 部署服务不注入 | `RemoteDir…` | `等不到：部署服务被调用` |
| nc3 | 回退 `#95`（空白不再回退默认） | `RemoteDir…` | `等不到：空目标目录回退到默认` |
| nc4 | 台账删掉一条（`TagMonitorView.TxtWriteValue`） | 台账核对 | `只有 5 条登记 —— 阶段 207 实测 6 条` |
| nc5 | 一条 driven 的判据不点名该框 | 指针核对 | `…TxtRemoteDir 说由 LibrarySearchBoxXamlWiringTests.cs 驱动，但那个判据里没有点名这个框` |
| nc6 | 一条 registered 的理由写短 | 理由核对 | `…TxtWriteValue 既没有判据、理由也写得太短（2 字）` |

🩸 其中 nc4/nc5 第一版注入**构建不过**（删条只删了第一行、注释把行尾的 `),` 一起注释掉了）——
"负控必须先能编译"这条纪律再次用上。6 支逐支独立日志、每腿限时、还原 SHA256 逐字节相同。

## 5. 读数与仍未做

**读数**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **702**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2280 / 315 / 812 / 8`（合计 **3681**）
＝ **3681 通过 / 0 失败**（阶段 206 为 3675，**＋6**）；`App.UiTests` **806 → 812**（连跑 37 / 40 s）；
`UnusedBaseline` **不变 1109**；**产品侧改动集中在 `DownloadToHmiView` ＋ 一条接口**。

**仍未做**：

1. **`AiIconDialog` 那 4 个框**：要驱动得补一条 AI 图像生成器的缝（`AiPreviewManager` 目前 sealed
   且自建 provider）—— 与 `#94` 同形的产品决策。
2. **`TagMonitorView.TxtWriteValue`**：要模拟器/设备侧的缝。
3. **`PropertyPanelView` 的调色对话框那条路**（`OnColorSwatchClicked`）：要能点开模态对话框的夹具。
4. 台账目前只覆盖 `Views/**/*.axaml` 里的 `TextBox` ＋ 数字框；`ComboBox`/`CheckBox` 的动作处理器读点
   没进这张账。

---

## 6. 阶段 209：`AiIconDialog` 4 个框（这一族的第三个视图）

台账里原本登记着"`AiIconDialog` 4 个框不可驱动"，原因是：对话框直接吃 `AiPreviewManager`
（**sealed**，且自己 `new` provider）⇒ 真正的读取点在 `ImageGenerator` 的单图生成与
`BatchIconGenerator` 的批量生成的**实参**里，没有缝就只能测"未配置 / 空输入"两条早退分支，
而跑真生成要走真 LLM。

### 6.1 产品侧：`IIconGenerationSource` 注入缝（行为零变化）

新增 `src/HmiEditor.App/Services/IconGenerationSource.cs`：

| 成员 | 默认实现（`PreviewManagerIconSource`） |
|---|---|
| `IsConfigured` / `HasIconGenerator` / `HasImageGenerator` | 原样读 `AiPreviewManager` 的三个能力位 |
| `GenerateImageAsync(prompt, w, h, ct)` | 转发到 `ImageGenerator` 的单图生成 |
| `GenerateIconsAsync(items, fixedVariant, ct)` | 原「`BatchIconGenerator` ＋ `FixedVariant` ＋ 批量生成」那段逻辑**搬进来** |

`AiIconDialog` 加一条 **`internal`** 注入构造（`source` 传 null ⇒ 包住真 manager）；
6 处 `_manager.X` 里与生成相关的 4 处改走 `_source`。⚠ 与设备侧两处同因：`internal` 不惊动
`WindowProductionPathUiTests` 的「双路径」元检查。

> 🩸 **顺带撞到一条文本级守卫的误伤**：`AiCancellationWiringTests` 用正则扫
> `Xxx.GenerateAsync(...)` 并要求实参里有 token —— 它**连注释一起扫**，于是我在类文档里写的
> 一句省略号假调用（`…GenerateBatchAsync(...)`）被当成"没传 token 的调用点"。
> 处置：把注释改成**不写带括号的调用字样**，并在注释里留一句提醒（本轮没去动那条守卫的口径）。

### 6.2 判据：4 个框全驱动，台账 **20 driven / 2 registered**

`AiIconDialogInputReadFaceTests`（3 条）：

| 框 | 怎么驱动 | 断言 |
|---|---|---|
| `ImageInputBox` | 切图片模式 → 写提示词（**带两侧空白**）→ 点生成 | 假源收到**裁剪后**的提示词 |
| `WidthBox` / `HeightBox` | 同上，两格写 320 / 240 | 假源收到的尺寸是 `(320, 240)` |
| `InputBox` | 批量模式 → 写两行「名字: 描述」 | 假源收到 2 条（名字解析正确）· `FixedVariant` 为 null（未选固定变体） |
| —（反面） | 空/全空白提示词 · `IsConfigured=false` | 假源**收不到**请求（早退分支） |

### 6.3 负控（5 支，`.workbuddy/tmp/s209-nc.py`）

单图不走生成源 · 批量不走生成源 · **尺寸两格写死 200×200**（读数 `Expected (320, 240) / Actual (200, 200)`）·
**提示词不再裁剪**（读数 `Assert.Null() Failure … Actual (200, 200)` —— 即"全空白提示词应当早退"那条腿先红）·
台账把 4 条挪回 `registered`。前两条读数形如 `等不到：假源收到单图生成请求（已等 10000 ms）`。

### 6.4 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **702 → 703**（新增 1 个产品文件）；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2280 / 315 / 816 / 8`
（合计 **3685**）＝ **3685 通过 / 0 失败**（阶段 208 为 3682，**＋3**）；
`App.UiTests` **813 → 816**（连跑 38 / 38 s）；`HmiEditor.AI.Tests` **315 / 0**；
`UnusedBaseline` **不变 1109**；`MainWindow.axaml.cs` 仍 **2983**。

### 6.5 仍未做

台账还剩 **2 条 registered**：`TagMonitorView.TxtWriteValue`（要模拟器/设备侧的缝）·
`BuildRunDialog.OutputLog`（只读日志框，扫到它是因为读 `Text.Length` 截断）。
另外台账只覆盖 `Views/**/*.axaml` 的 `TextBox` ＋ 数字框（选择控件的动作读点实测为 0，见 §7.1）。

---

## 7. 阶段 210：最后一个视图 ＋ 台账**收口**（21 条全 driven、`Registered` 归零）

台账里最后两条"registered"这一阶段都清掉了 —— 一条是**登记错了**，另一条**根本不是输入框**。

### 7.1 🩸 `TagMonitorView.TxtWriteValue`：登记错了（根本不需要产品缝）

它原先登记的理由是"要驱动得先有**模拟器/设备侧的注入缝**"。**这个理由是错的**：

- 视图的构造**本来就接收引擎** —— `TagMonitorView(RuntimeBindingEngine, Func<string,double?>?)`；
- 而 `RuntimeBindingEngine` **本来就吃** `IRuntimeValueSource`（`TryRead` / `TryWrite` / `IsConnected` /
  `Name`，`ValueSource` 是公开可设属性）。

⇒ 一个**假值源**就够了，一行产品代码都不用改。判据 `TagMonitorInputReadFaceTests`：
夹具 = 1 个连接 ＋ 1 个变量 ＋ 1 个 `ReadWrite` 绑定的控件（`BuildRuntimeTags()` 产出可写标签）；
走**生产序列**（宿主 `Wiring.Panels.cs` 建完视图就调一次 `RefreshRows()` —— 写入目标下拉是在那里
填上的，构造里不填）⇒ 驱动 `TxtWriteValue` = `12.5` → 点「写入」→ 断言假值源收到 `12.5`；
反面三条：**非法文本**（`abc`）· **空** · **源未连接** ⇒ 都**不写**。

> **教训（写进台账的棘轮消息里）**：登记的理由**必须验一遍**。台账原有的抽查只验证了
> "读点确实在动作处理器里"这个**形态**，查不出"理由本身是否成立" —— 这一条就这么错了一整个阶段。

### 7.2 `BuildRunDialog.OutputLog`：不是输入框 ⇒ 扫描口径收紧

它是 `IsReadOnly="True"` 的**日志框**（`AppendLog` 只往里追加；被扫到是因为它读了 `Text.Length`
做截断）。本轮把扫描口径改成**跳过 `IsReadOnly="True"` 的具名框**（用户改不了它们，也就不存在
"读点在动作处理器里却驱动不了"这回事），并加了一条**口径自检**：
`TagMonitorView.TxtWriteValue` 必须在面内、`BuildRunDialog.OutputLog` 必须在面外。

⚠ 注意与「谁读」面的口径**不冲突**：那一面问"有没有人读"，`OutputLog` 因为读 `Text.Length`
**确实**算"有读点"（归它的桶）；这一面问"用户输入有没有被驱动"，只读框不参与。

### 7.3 收口后的台账

**21 条全部 driven，`Registered` 为空**，判据里是棘轮：

```csharp
Assert.True(driven >= 21, "只有 N 条已驱动 —— 阶段 210 实测 21 条");
Assert.True(registered == 0, "有框被登记为「不可驱动」—— 先验一遍理由（阶段 210 的经验：有一条其实早就可驱动）…");
```

分组：`DeviceManagerView` 12（只读展示框已归 §112 的 `DisplayOnly`）· `DownloadToHmiView` 4 ·
`AiIconDialog` 4 · `TagMonitorView` 1。

### 7.4 负控（5 支，`.workbuddy/tmp/s210-nc.py`）

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | `TryWriteValue` 不再读输入框（写死 1.0） | 判据 | `Expected: 12.5 / Actual: 1` |
| nc2 | 去掉解析失败分支（非法文本也写 0） | 判据 | `Expected: 12.5 / Actual: 0` |
| nc3 | 台账把 `TxtWriteValue` 挪回 registered | 棘轮 | `只有 20 条已驱动 —— 阶段 210 实测 21 条` |
| nc4 | 扫描面不再跳过 `IsReadOnly` | 双向闭合 ＋ 口径自检 | `这些输入框的读点在动作处理器里，但台账里没有它们：BuildRunDialog.axaml::OutputLog` |
| nc5 | 台账里加回 `OutputLog` 条目 | 登记项核对 | `BuildRunDialog.axaml::OutputLog：扫描面里根本没有它 —— 登记已过期` |

5 支逐支独立日志（`.workbuddy/tmp/s210-nc*.log`）、每腿限时、开跑前扫残留注入，
还原后 2 个文件的 SHA256 逐字节相同。

### 7.5 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2280 / 315 / 817 / 8`（合计 **3686**）
＝ **3686 通过 / 0 失败**（阶段 209 为 3685，**＋1**）；`App.UiTests` **816 → 817**（连跑 43 / 35 s）；
`UnusedBaseline` **不变 1109**；**本阶段产品代码 0 行改动**（只动测试）。

### 7.6 仍未做

台账只覆盖 `Views/**/*.axaml` 里的 `TextBox` ＋ 数字框（选择控件的动作读点实测 **0** 条 ——
见专题 §7.1 的测量）；这一族的**成功路径**现在都有假实现驱动了，但**服务自身**的行为仍由各自的
单元面负责（设备侧 `HmiEditor.Device.Tests`、AI 侧 `HmiEditor.AI.Tests`）。
