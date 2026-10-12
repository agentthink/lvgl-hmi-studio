# 定时/自动导出（阶段 184，登记 `#87`）

> 报表能力这条线在 181~183 把"**怎么导**"做完了（四个格式 ＋ 时间范围 ＋ 列选择 ＋ 报表包 ＋ 报表设计器），
> 缺的只剩"**谁来按点触发**"。`#87` 从阶段 180 起就挂在登记表 §3（需先立功能）里，
> 本阶段按本仓"先量后立"的老规矩：**先读数、先定口径，落地可判的那一半（Core 排程内核）**。

---

## 1. 现状读数：要动它，先看清三件事

### 1.1 "怎么导"已经不用再设计

`ReportBundle`（文件名 ＋ 索引）· `ReportExport.WriteBundle/SaveBundleAsync`（一个数据源 × 四格式 ＋
`index.txt`）· 三个数据源的建表器与各自的 `Columns(...)` —— 定时导出要做的只是**按点调用它们**。

### 1.2 真正的坎：三个数据源**归属不同**

| 源 | 现在的持有者 | 组合根能不能直接拿到 |
|---|---|---|
| 审计 | `AuthenticationService.GetAuditLog()` | ✅ `MainWindow._authService` |
| 报警 | `AlarmEngine.GetAllRecords()` | ✅ `MainWindow._alarmEngine` |
| **历史** | **`DataLogger` 在 `TrendConfigView.StartRecording()` 里 `new`，随对话框生死** | ❌ **应用层没有持有者** |

⇒ "定时导出全部数据源"这件事，**卡在历史数据没有应用级归属**上（不是卡在导出本身）。
两条路：给历史数据找个应用级持有者，或让定时导出只导"当前能拿到的源"并**如实记下缺了谁**。
本阶段把这道坎**写清楚**，处置落在 185（见 §4）。

### 1.3 设置与时钟的既有形状（照抄，不另造）

- **设置**：应用级 `SettingsService`（`Dictionary<string, object?>` ＋ 阶段 182 补的"结构化值回落"）；
  面板是 `SettingsDialog`。
- **时钟**：本仓已有 `TimeProvider` 先例（`VariableSimulator`，判据用 `FakeTimeProvider`）；
  "**不读墙上时钟**"是阶段 176/178 反复用过的可判性手段。
- **定时器**：`TrendConfigView` 里已有一个 `System.Threading.Timer`（500 ms 刷图表）——
  即"应用内节拍"在本仓不是新机制。

## 2. 触发形态：三个候选与取舍

| 候选 | 主张 | 取舍 |
|---|---|---|
| **应用运行期内固定节拍**（本阶段定） | 开着编辑器时每 N 分钟导一份 | **唯一能在应用内实现**的形态；可注入时钟 ⇒ 判据能确定性地核对"到点没到点"，不必真等一个周期 |
| 退出/关闭工程时导一次 | 关工程顺手导 | 拖慢关闭；"没关就永远没导"；用户对"关闭时偷偷写文件"的容忍度低 |
| 外部定时任务（CLI／系统计划任务） | 由操作系统按点跑 | 需要一条**无界面命令行入口**（本仓没有），还要处理"同一工程被两个进程打开" ⇒ 单独的大工程，登记为后续 |

**明确不做**：常驻后台守护/服务、跨机器集中归档。

## 3. 排程口径（本阶段落地的 Core 内核 `AutoExportSchedule`）

纯函数，`now` 由调用方给 ⇒ 判据用固定时刻核对，不碰线程、不睡觉：

1. **起跑不立刻导**：第一次到期在 **锚点 + 一个间隔**（锚点＝服务启动时记一次 `now`），
   否则每次开应用都先导一份 —— "我什么都没干它就开始写文件"是用户最不想要的自动化；
2. **到期点算在内**（闭区间，与报表时间范围同口径）；
3. **错过的一律不补**：时钟被跳过头（休眠／改系统时间／应用挂起）时只说"现在导一次"，
   `NextDue` **直接跳到 now 之后的第一个到期点**；该类型**刻意不提供**"错过了几个点"这类 API ⇒
   调用方**写不出**补跑循环（比注释与自觉硬）；
4. **时钟回拨 ⇒ 不到期**：等墙上时钟追回来，而不是静默重复导一份"看起来更新"的文件；
5. 间隔 ≤ 0 ＝ **功能关闭**（不是"每毫秒导一次"）；时钟跳到极远时**饱和返回**、不抛。

## 4. 分片计划

| 片 | 内容 | 状态 |
|---|---|---|
| 184 | 定义批 ＋ Core 排程内核（`AutoExportSchedule` ＋ 判据 ＋ 负控） | ✅ 本阶段 |
| 185 | **数据源注册表**：按 `ReportSourceKeys` 注册 `Func<ReportTable>`；审计/报警在组合根注册、关闭时注销，**历史由 `TrendConfigView` 开记录时注册、窗口关闭（释放 logger）前注销**；`Missing` 给 186 用于写 `index.txt` 的 skipped | ✅ |
| 186 | `AutoExportService`（可注入时钟 ＋ 可注入导出动作；**每个到期点只跑一次**；失败不打断后续节拍；缺的源写进 `index.txt` 的 skipped）＋ 设置三项（开关/间隔/目录）＋ `WriteMultiBundle` ＋ 接线与生命周期 ＋ 端到端判据 | ✅ |
| 187 | **设置界面**（`SettingsDialog` 加「自动导出」一面：开关/间隔/目录 ＋ 目录选择器）＋ 收口（含一条新登记 `#88`） | ✅ |

## 5. 诚实边界（现在就说清楚，别等用户撞上）

- **应用没运行就不会导**（"应用内节拍"的代价）—— 这条要写进设置项的悬停说明；
- 导出目录**固定**：不做按日期分子目录、不做轮转/保留 N 份（后续候选）；
- **历史数据只有"正在记录"时才有**（§1.2 的归属问题）：没在记录 ⇒ 该源记进索引的 skipped，
  界面上不该假装它导了；
- 到点导的是**当前**数据（不是"上一周期的增量"）：报表栈本来就是"导当前所见"，
  增量要另一个数据模型（本阶段不碰）；
- 排程内核里那处"tick 整除而非浮点分钟"**没有行为判据**（浮点版在 `DateTimeOffset` 能达到的
  范围内几乎总是同结果）⇒ 属于"靠实现选择保证、靠注释讲清楚"的一类，如实标注。

## 6. 本阶段（184）的判据与负控

- Core `AutoExportScheduleTests` **10 条**：关闭档（0／负数）· 起跑锚点 ＋ 一个间隔 · 上次导出优先于锚点 ·
  **时钟回拨不到期** · **错过不补（`NextDue` 跳到未来点）** · **结构判据：没有任何"补跑"API** ·
  稳态连跑 6 个周期恰好 6 次 · 极远跳变饱和不抛 · `NextDue` 严格大于 now。
- **负控 4 支**（`.workbuddy/tmp/s184-nc.py`，逐支独立日志 ＋ 还原 SHA256 逐字节相同）：
  到期判定改严格大于 · 不跳到 now 之后 · 不看开关 · 去掉饱和兜底。


---

## 7. 阶段 185：数据源注册表（把 §1.2 那道归属坎处置掉）

`ReportSourceRegistry`（应用级，`App.ReportSources` 内联初始化）：

- `Register(key, Func<ReportTable>)` / `Unregister(key)` / `Clear()`；
- `Registered` / `Missing` —— **顺序一律按 `ReportSourceKeys.All`**（判据与索引都靠它稳定）；
- `TryBuild(key)` —— 没注册 ⇒ `null`（**正常状态，不是错误**）；委托在**锁外**调用
  （取数可能上千行，不能占着锁）；
- **未知键当场抛**：键会一路变成报表包文件名与偏好存储键，"写错一个字母"必须在这里红。

**接线**：审计（`_authService.GetAuditLog()`）与报警（`_alarmEngine.GetAllRecords()`）由
`ReportSourceWiring` 注册/注销，`MainWindow` 的生产构造与 `CleanupOnShutdown` 各**只留一行调用**；
`TrendConfigView.StartRecording()` 里注册历史（委托＝共用的 `BuildHistoryTable()`），
`OnClosed` 里**在 `_logger.Dispose()` 之前**注销。

> **为什么单开 `ReportSourceWiring`**：接线本身只有两三行，但"为什么在这里注册、委托体读的是哪一刻的
> 状态、谁负责注销"这段解释必须写在代码旁边 —— 而 `MainWindow.axaml.cs` 有**行数基线**
> （`SingleFileSizeGuardTests`，2997 行的第一次门禁红就是这么来的）。照本仓老做法：
> **逻辑与解释一起搬出去，调用点只留一行**；顺手把该文件一处三行摘要压成一行，最终**仍停在基线 2983**。

> ⚠ **注销与停记录是两件事**：停记录之后缓冲还在、仍可导出 ⇒ 不注销；只有窗口关掉
> （`_logger` 被释放）才注销。

**取数构造只有一处**：报表包导出与注册表都走 `BuildHistoryTable()` ——
两处各写一份的话，定时导出与手动导出会给出**不同的列**（列序一漂移就错位，且不报错）。

### 7.1 判据与负控

- Core `ReportSourceRegistryTests` **8 条**：注册/取数 · **委托每次现取**（不是快照）·
  没注册 ⇒ `null` ＋ 进 `Missing` · 两个清单都按声明顺序 · 重复注册＝覆盖 ＋ 重复注销不抛 ·
  未知键抛 · `Clear` · **并发注册/取数不抛且终态正确**。
- App `ReportSourceWiringGuardTests` **5 条**：**生产主窗口真的注册了审计/报警且取得到表**
  （行为面）· 注册只在生产构造、注销在关闭收口 · 历史源"开记录时注册 ＋ 释放前注销 ＋ 用共用构造" ·
  `BuildHistoryTable` 三处共用 · 每个文件注册个数恰好。
- **负控 5 支**（`.workbuddy/tmp/s185-nc.py`，逐支独立日志 ＋ 还原 SHA256 逐字节相同）：
  主窗口漏注册 · 释放早于注销 · 注册成一次性 lambda · 注册表不校验键 · `Missing` 报反。
- **诚实边界（写进判据注释）**：历史那条**只有源码级判据** —— 驱动"开记录"要点 `BtnToggleRecord`，
  而那一枚在 `HostButtonEffectUiTests` 里被明确声明为 `Never`（它启动真的采样定时器 ⇒ 读数会抖）。

---

## 8. 阶段 186：`AutoExportService`（`#87` 收口片）

### 8.1 服务（依赖全是回调 ⇒ 判据不睡觉、不碰真磁盘、不碰线程）

`AutoExportService`：`Now` / `Options` / `RegisteredSources` / `MissingSources` / `BuildSource` / `Write`
全部由组合根注入；`TickOnce()` 走一拍（返回"这一拍真的写盘了吗"）。**三条口径**：

1. **每个到期点只跑一次**：一旦认定"到点"，**先**把 `LastRun` 记成此刻 —— 无论写成功还是写失败。
   失败后不重跑这一拍（否则 30 秒的节拍会退化成"每 30 秒重试一次"的刷屏机），下一个到期点自然重试；
2. **没有源就不写空包**：一个数据源都取不到 ⇒ 报 `NoSources`，**不**产出只剩 `index.txt` 的目录；
3. **写盘异常不打断节拍**：捕获后报 `Failed`（带异常消息），定时器继续走 ——
   后台节拍里把异常抛到 Dispatcher 上只会静默杀掉整条链。

结论是**结构化**的（`AutoExportOutcome` ＋ 文件数 ＋ 诊断细节），文案由组合根按语言拼
（服务本身不认识语言；顺带避开"服务里写死中文"那条本地化基线）。`NotDue` 刻意**不发声**：
每 30 秒往状态栏写一句"还没到点"是骚扰。

### 8.2 设置三项

`SettingsService.AutoExport`：`Enabled`（默认 **false**）· `IntervalMinutes`（默认 30）· `Directory`（默认空）。
默认值刻意"不干活"：装完不会突然往磁盘写东西；目录留空时即便开了也只报"没配目录"而**不弹选择器**
（后台节拍里弹窗是最坏的行为）。`Enabled=false` 或间隔 ≤ 0 ⇒ 排程**根本不到期**（连"没配目录"都不报）。

### 8.3 写盘：单源与多源**共用同一份实现**

`ReportExport.WriteMultiBundle(directory, items, missingSources, missingSourceMessage, …)`，
而单源的 `WriteBundle` **只是它的一个元素** ⇒ 文件名、索引格式、跳过原因不可能分叉。
两个新口径：渲染不出来的格式**带上文件名**（多源时只写扩展名分不清是哪个源）；
**缺的源按 `missingSourceMessage` 记进 `skipped`**（"这次少导了什么"必须在产物里看得见）。

### 8.4 接线与生命周期

`AutoExportWiring` 注入真的那几个回调（设置、注册表、`WriteMultiBundle`、状态栏出口），
由 `ReportSourceWiring` 在**注册数据源之后** `Start()`、在**注销之前** `Stop()`
（先停表再撤源：反过来会让竞态中的那一拍读到"正在被撤走的"源）。
节拍用 `DispatcherTimer`（`TickOnce` 要读三个由 UI 线程维护的集合 ⇒ 在 UI 线程上取数最安全；
30 秒看一眼、分钟级才真的导一次，代价可忽略）。

> 放在 `AutoExportWiring`/`ReportSourceWiring` 这两个小文件里还有一条现实原因：
> `MainWindow.axaml.cs` 有**行数基线**（见 §7 那次门禁红）—— 接线类新增逻辑一律落在小文件里，
> 调用点只留一行。

### 8.5 判据与负控

| 层 | 判据 |
|---|---|
| Core `AutoExportServiceTests` **10 条** | 起跑不导 · **每到期点只跑一次** · **错过不补** · **失败不打断节拍**（且不重跑这一拍）· 关掉的功能连"到点"都不存在 · **开了但没配目录**不写盘 · **没源不写空包** · 取不到表的源不算数 · 缺源与时间戳原样交给写盘侧 · 停表不重置进度 |
| App `AutoExportGuardTests` **7 条** | 设置三项往返 ＋ 默认不干活 · 多源写盘（每源 × 四格式 ＋ 索引含缺源）· 单源与多源产物一致 · **端到端**（假时钟 ＋ 真写盘 ⇒ 目录里真有文件、索引里真有缺源）· 真接线起停（UI 线程 ＋ 起表时记锚点）· 接线源码契约（读注册表/设置、走多源写盘、**先停表再注销**）· 四种结论的文案在两本字典里且 `NotDue` 不发声 |

**负控 7 支**（`.workbuddy/tmp/s186-nc.py`，逐支独立日志 ＋ 还原 SHA256 逐字节相同）：
不消费到期点 · 写盘不兜异常 · 没有源也照样写 · 丢缺源 · 写盘侧忽略缺源 · 组合根不停表 · 设置默认开着。

### 8.6 诚实边界（本阶段明确**没有**做的）

- **设置界面还没做**：三项设置已经生效（服务读它），但 `SettingsDialog` 里还没有「自动导出」那一面
  ⇒ 现在要启用只能手改 `settings.json`（或等 187）。这条不许含糊：功能"能跑"与"用户能用"是两件事。
- 应用**没运行**就不会导（§5 第 1 条，仍然成立）。
- 目录**固定**：不按日期分子目录、不轮转、不保留 N 份（后续候选）。

---

## 9. 阶段 187：设置界面 ＋ 收口（`#87` 闭环）

### 9.1 落地

`SettingsDialog` 新增分类「自动导出」（导航项在 `PopulateCategoryList` 里加，面板 `PanelAutoExport`）：

| 控件 | 绑定 |
|---|---|
| `ChkAutoExport` | `IsCheckedChanged → SetEnabled` |
| `NumAutoExportInterval` | `ValueChanged → SetIntervalMinutes`（1…1440 分钟） |
| `TxtAutoExportDirectory` | `TextChanged → SetDirectory` |
| `BtnPickAutoExportDirectory` | 目录选择器；选到就写进输入框（`TextChanged` 会一起落进设置），取消/平台桩 ⇒ **什么都不做** |

面板里还印着一条**诚实边界**（`settings.autoexport.hint`）：自动导出只在编辑器运行期间工作；
当前不可用的数据源会如实记进 `index.txt`，不会假装导过。

> 为何键**没有**新加 `Lang` 常量：这一族的邻居（`settings.theme.nav` 等）本来就是
> XAML `{res:Loc}` / 运行时字面量取键 ⇒ 跟着邻居走，避免"为了消基线而造一堆只被自己引用的常量"。

### 9.2 🩸 本阶段量到的一件事：**程序化写 `TextBox.Text` 不触发 `TextChanged`**

给"目录"写行为判据时，第一版直接 `txt.Text = "…"` 然后断言设置变了 —— **红了**。
探针实测（临时用例，跑完即删）：`Text` 同步变成了新值，而 `TextChanged` 一次都没触发；
同一形状的既有接线 `TxtLvglDir` 也一样（`Simulator.LvglDir` 纹丝不动）。

⇒ 两条结论：

1. 判据要**按框架的做法**驱动：设完 `Text` 再 `RaiseEvent(new TextChangedEventArgs(TextBox.TextChangedEvent))`
   —— 与 `HostButtonEffectUiTests` 里"ToggleButton 先翻转 `IsChecked` 再引发 `Click`"**同一条纪律**
   （不补这一步，驱动的就不是生产路径）。接线缺失时补发也没有人接 ⇒ 判据依然有牙齿（负控 nc2 证明）。
2. **登记 `#88`**：`SettingsDialog` 里 `TxtLvglDir` / `TxtT113SdkRoot` / `TxtCanvasBgColor` /
   `TxtAccentColor` 这一族 TextBox→设置的接线**从来没有判据驱动过**（真实输入下它们工作，但测试面为零）。
   本阶段只给自己新加的那一枚补了判据，**没有**顺手把那一族都盖住 —— 如实登记，不假装覆盖。

### 9.3 判据与负控

- App `AutoExportSettingsUiTests` **4 条**：控件 ↔ 设置双向（载入 ＋ 三条回写 ＋ 闭环到 `Options().CanRun`）·
  XAML 契约（面板 ＋ 五个具名控件）· 接线源码契约（四条接线 ＋ 载入 ＋ 分类导航三条）· 键双语
  （**字面量键**）＋ 提示里必须写清"只在编辑器运行期间"。
- `HostButtonEffectUiTests` 的 `SettingsDialog` 宿主随之扩容：新增 2 个数据面分量（开关/间隔）、
  1 条 `Moves("ChkAutoExport")`、1 条 `Never("BtnPickAutoExportDirectory")`（它走平台桩，本层不点）。
- **负控 5 支**（`.workbuddy/tmp/s187-nc.py`，逐支独立日志 ＋ 还原 SHA256 逐字节相同）：
  开关不接线 · 目录不接线 · 分类导航不切面板 · zh 字典漏键 · 不载入初值。

### 9.4 `#87` 的**诚实边界**（闭环也要说清边界）

- 应用**没运行**就不会导（"应用内节拍"的代价；已写进设置面板的提示里）；
- 目录**固定**：不按日期分子目录、不轮转、不保留 N 份；
- 历史数据**只有正在记录时**才有（`DataLogger` 归属，见 §7）；没在记录 ⇒ 该源进 `index.txt` 的 skipped；
- 到点导的是**当前**数据，不是"上一周期的增量"。
