# `DeviceManagerView` 的输入框**读点行为面**（阶段 205）
## —— 「谁读」账里最后一块：13 个框的读点（阶段 205 驱动 5 个 ⇒ 阶段 206 补注入缝后 13/13）

> 承「输入框『谁读』面」（阶段 196）与两个保存路径行为面（阶段 197/198）。这一面把读侧账里
> **最后一块没有任何行为判据**的地方做完：`DeviceManagerView` 的 13 个具名输入框。

---

## 1. 这 13 个框的读点长什么样

它们的读点**不在保存路径里**，而在**动作处理器**里（这是它和属性面板那批框的根本区别）：

| 处理器 | 读的框 | 做什么 |
|---|---|---|
| `OnOpenCloseClicked` | `TxtBaudRate` / `TxtHost` / `TxtTcpPort` | 构造 `SerialDeviceChannel` / `TcpDeviceChannel` 并**开通道** |
| `OnCheckUpdateClicked` | `TxtLocalVersion` | 版本比对（**先 `RequireChannel()`**） |
| `OnUpdateHmiClicked` | `TxtHmiSwu` | OTA 升级（**先 `RequireChannel()`**） |
| `OnFlashFwClicked` | `TxtFwSwu` | 固件刷写（**先 `RequireChannel()`**） |
| `OnGenerateClicked` | `TxtHmiLib` / `TxtOutputDir` / `TxtPkgVersion` | **纯本地**打包 USB 更新包（`UsbUpdatePackager`） |
| `OnFelFlashClicked` | `TxtFelTool` / `TxtFelImage` / `TxtFelArgs` | 调**外部工具** `sunxi-fel` 烧录 |
| `OnDetectClicked` / `OnCheckUpdateClicked` | `TxtDeviceVersion`（**写**，只读展示） | 回填设备版本 |

## 2. 分法：**有没有可观测的确定性分支**

判据（`tests/HmiEditor.App.UiTests/DeviceManagerInputReadFaceTests.cs`，4 条）按这条线分：

### 2.1 driven（5 个）

| 框 | 怎么驱动 | 断言什么（都不读本地化文案，除了"同一来源"的那两句校验提示） |
|---|---|---|
| `TxtHmiLib` / `TxtOutputDir` / `TxtPkgVersion` | 造一个假 `libhmi_ui.so` ＋ 临时输出目录，驱动三格 → 点「生成」→ 等结果 | **真的产出 `hmi-9.9.9.swu`**（版本来自 `TxtPkgVersion`、目录来自 `TxtOutputDir`）· 结果文本里带**产出路径** · 日志里有 `v9.9.9 → <outDir>` 那句（语言无关）；另加两条校验分支：空 `.so` ⇒ `DeviceLogUsbSelectSo`、空输出目录 ⇒ `DeviceLogUsbSelectOutDir` |
| `TxtHost` | 切到以太网分支（`CmbChannelType` index 1）＋ 主机写空白 → 点「打开」 | 日志出现 `DeviceLogEnterHost`（**没有读就不会有这句**）· 且**没有真去连**（按钮文案仍是「打开」） |
| `TxtFelImage` | 写一个不存在的镜像路径 → 点「烧录」 | 日志出现 `DeviceLogFelSelectImage` · 且**没跑到**「正在 FEL 烧录」那一步 |

> 那两句校验提示用的是 `AppLocalization.Service[Lang.X]` —— **与产品同一个来源**，不是把文案冻进判据。

### 2.2 registered（8 个，逐条写清为什么 headless 下不可驱动）

| 框 | 理由 |
|---|---|
| `TxtTcpPort` | 端口非法时**静默回退 23** ⇒ 读没读过来没有可观测差别；要判它得真连一次设备 |
| `TxtBaudRate` | 串口分支要求先选中一个**真串口**（`CmbPort` 由 `SerialPort.GetPortNames()` 填，容器里为空）⇒ 进不去那个分支 |
| `TxtLocalVersion` / `TxtHmiSwu` / `TxtFwSwu` | 三个处理器第一句都是 `RequireChannel()` ⇒ **没连设备时在读到它们之前就返回了** |
| `TxtFelTool` / `TxtFelArgs` | 只在**真的调起** `sunxi-fel`（外部进程）之后才用得上 |
| `TxtDeviceVersion` | **只读展示**框（`IsReadOnly="True"`）：由两个处理器回填，从来没人读它 —— 已在「谁读」面的 `DisplayOnly` 登记表里（阶段 196/197） |

## 3. 🩸 结构性观察：这 8 个框**缺注入缝**（阶段 206 已补，见 §6）

`DeviceManagerView` 把设备服务**直接 new 出来**：

```csharp
private readonly DeviceDetector   _detector      = new();
private readonly DeviceFlasher    _flasher       = new();
private readonly HmiUpdateService _updateService = new();
private readonly UsbUpdatePackager _packager     = new();
```

⇒ 那 8 个框的**成功路径**在 headless 下原理上不可驱动（会去碰串口/网口/外部进程）。
两条出路，都需要**产品决策**（已登记 `#94`）：

1. **加注入缝**（构造函数可选参数或 `internal` setter）：`DeviceFlasher` / `HmiUpdateService` /
   `DeviceDetector` 换成接口或可替换委托 ⇒ 那 8 个框就能像打包路径一样驱动（本仓已有先例：
   `IPropertyValidator` 注入、`TimeProvider` 注入）；
2. **保持缺口**：承认"成功路径只有真机跑得到"，把这条理由写进台账（本阶段做的就是这一半）。

⚠ 顺带量到的一件事：`OnFelFlashClicked` 里"镜像不存在"其实有**两道**闸（面板校验 + `DeviceFlasher`
自己的检查）—— 删掉面板那道（负控 nc5）后日志变成「正在 FEL 烧录」＋「错误: 镜像不存在」，说明
**面板那道是更早的一道**（判据钉的正是它）。

## 4. 负控（6 支，`.workbuddy/tmp/s205-nc.py`）

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | 打包时不再读版本框（写死 `"1.0.0"`） | 打包判据 | `没产出 …/hmi-9.9.9.swu；结果文本：… hmi-1.0.0.swu；日志尾：[USB包] 正在生成... v1.0.0 → …` |
| nc2 | 打包时不再读输出目录 | 打包判据 | `等不到：打包结果文本被写上（已等 10000 ms）` |
| nc3 | 删掉空 `.so` 的校验分支 | 打包判据 | `Collection: ["… 设备管理就绪", "… [USB包] 请选择输出目录（U盘/TF卡）"]` |
| nc4 | 删掉 TCP 分支的主机校验 | 主机判据 | `… [连接] 错误: 连接设备 :23 失败: The value cannot …`（⇒ 没有校验就直接去连了空主机） |
| nc5 | 删掉 FEL 镜像校验 | FEL 判据 | `… [FEL] 正在 FEL 烧录... | [FEL] 错误: 镜像不存在: …`（⇒ 面板那道闸被绕过） |
| nc6 | 台账里删掉一个 driven 框（`TxtHmiLib`） | 二选一台账 | `这些具名输入框既没被驱动、也没登记理由：TxtHmiLib` |

6 支逐支独立日志（`.workbuddy/tmp/s205-nc{1..6}.log`）、每腿限时、开跑前扫残留注入，
还原后 2 个文件的 SHA256 逐字节相同。

## 5. 读数与仍未做

**读数**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2280 / 315 / 802 / 8`（合计 **3671**）
＝ **3671 通过 / 0 失败**（阶段 204 为 3667，**＋4**）；`App.UiTests` **798 → 802**（连跑 38 / 39 s）；
`UnusedBaseline` **不变 1109**；**产品代码 0 行改动**。

**仍未做**：

1. ~~**`#94`**：那 8 个框要真驱动，得先给 `DeviceManagerView` 加注入缝（产品决策）。~~
   ⇒ **阶段 206 已补**（见 §6），13 个框全部可驱动。
2. **`TxtTcpPort` 的回退口径**（非法值静默用 23）：现在只是"登记为不可观测"，**要不要给它一个可见提示**
   是 UI 决策。
3. 本面只管 `DeviceManagerView`；**别的窗口/视图里"读点在动作处理器"的框**没有统一清点
   （`AiIconDialog` 的 `HeightBox`/`WidthBox`、`AiChatPanelView` 的几个框都可能同形）。

---

## 6. 阶段 206：注入缝补齐 ⇒ **13/13 全驱动**

按 `#94` 的第一条出路做：把那几个服务抽成接口，视图改成注入。

### 6.1 产品侧（行为零变化）

| 接口（`src/HmiEditor.Device/Abstractions/DeviceServiceAbstractions.cs`） | 原实现 |
|---|---|
| `IDeviceDetector` | `DeviceDetector`（原样 `implements`） |
| `IHmiUpdateService` | `HmiUpdateService` |
| `IDeviceFlasher` | `DeviceFlasher` |
| `IUsbUpdatePackager` | `UsbUpdatePackager` |
| `IDeviceChannelFactory` | **新增** `DeviceChannelFactory`（把视图原先就地 `new SerialDeviceChannel/TcpDeviceChannel` 那两行搬进来） |

`DeviceManagerView` 加了一条**注入构造**（`internal`，传 null ⇒ 真实实现）。

> ⚠ **为什么是 `internal` 而不是 `public`**：做成 public 会让该窗口多出一条公共构造，
> `WindowProductionPathUiTests` 的「双路径」元检查立刻要求本窗口改判成 `TwoPaths=true`，
> 而那条判据还要求**生产构造与设计器构造的字段形状不同** —— 本视图两条路装的是同一批服务，
> 形状必然一致 ⇒ 必然红。`internal` 是本仓既有先例（`ToolboxView` 的注释写着
> "internal 只为判据可调用"，靠 `HmiEditor.App.csproj` 的 `InternalsVisibleTo`）。
> 无参构造仍是**唯一的生产入口**（`TwoPaths=false` 依旧成立）—— 这条是**判据逼出来的设计**，
> 不是随手选的可见性。

### 6.2 判据侧：13 个框全部驱动，`Registered` 台账**清零**（棘轮）

| 框 | 用什么假实现驱动 | 断言 |
|---|---|---|
| `TxtHost` / `TxtTcpPort` | `FakeChannelFactory` 记 TCP 参数 | 工厂收到 `("10.0.0.5", 2323)`；非法端口 ⇒ **静默回退 23**（口径也被钉住） |
| `TxtBaudRate` | 同上（串口分支 ＋ 夹具自备端口项） | 工厂收到 `("COM_TEST", 9600)`；非法波特率 ⇒ 回退 `DefaultBaudRate`（115200） |
| `TxtLocalVersion` | `FakeUpdateService` 记 `CheckAsync` 的入参 | 收到 `"8.8.8"` |
| `TxtHmiSwu` | 同上，记 `UpdateAsync` | 收到那个 `.swu` 路径 |
| `TxtFwSwu` | `FakeFlasher` 记 `FlashAsync` 的 `OtaFlashOptions` | 收到那个固件包路径 |
| `TxtFelTool` / `TxtFelImage` / `TxtFelArgs` | 同上，记 `FelFlashAsync` | 三格原样进 `FelFlashOptions` |
| `TxtDeviceVersion`（只读展示） | `FakeDetector` / `FakeUpdateService` 返回版本 | 展示框被回填成假实现的版本（两处：检测 ＋ 版本比对） |

### 6.3 负控（7 支，`.workbuddy/tmp/s206-nc.py`）

TCP 不走工厂 · 串口不走工厂 · 更新服务不注入 · 刷写器不注入 · 检测器不注入 ·
台账塞回一条 `Registered`（棘轮红）· **端口回退值改掉（23 → 9999）**。
读数例：`等不到：更新服务收到本地版本（已等 10000 ms）` · `等不到：检测结果回填到只读展示框` ·
`有框被登记为「不可驱动」…：TxtTcpPort`。
7 支逐支独立日志（`.workbuddy/tmp/s206-nc*.log`）、每腿限时、开跑前扫残留注入，
还原后 2 个文件的 SHA256 逐字节相同。

### 6.4 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700 → 702**（新增 2 个产品文件）；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2280 / 315 / 806 / 8`
（合计 **3675**）＝ **3675 通过 / 0 失败**（阶段 205 为 3671，**＋4**）；
`App.UiTests` **802 → 806**（连跑 40 / 50 s）；`HmiEditor.Device.Tests` **96 / 0**；
`UnusedBaseline` **不变 1109**；`DeviceManagerView.axaml.cs` **552 行**；`MainWindow.axaml.cs` 仍 **2983**。

### 6.5 仍未做

`TxtTcpPort` 的**静默回退**要不要给用户一个可见提示（UI 决策）·
**别的窗口/视图里"读点在动作处理器"的框**没有统一清点（`AiIconDialog` 的 `HeightBox`/`WidthBox` 等）·
`DeviceManagerView` 的**成功路径**现在能驱动了，但 `DeviceFlasher`/`HmiUpdateService` **自身**的行为
仍由 `HmiEditor.Device.Tests` 的 96 条判据负责（本面只到"参数真的传到了服务"这一层）。
