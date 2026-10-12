# 菜单点击行为判据：62 个具名菜单项全部归类，48 个真点（阶段 49）

> 提交 `c2aa5f6` | 2026-09-20 | 登记表 #37 遗留① / #39 的正面回答
> 判据 `tests/HmiEditor.App.UiTests/MainWindowMenuInvocationUiTests.cs`（3 条）

## 0. 一句话

把“点一个菜单项之后到底会发生什么”变成可判：**62 个具名菜单项全部被分到恰好一个桶里**
（弹窗 18 / 静默 28 / 条件 2 / 跳过 14），其中 **48 个真点**，判据是主窗口
`Window.OwnedWindows` 的**增量与类型**；面由双向差集守卫锁死，**新增菜单项没分类即红**。

## 1. 起因：一条零信息量的“静态绊线”

阶段 46 立了 `MainWindow` 的 3 条判据，其中菜单只验了 **1 条**（语言切换真的换了文案）。
其余 60 余个只有“名字被代码引用过”这类静态绊线 —— 它能证明 `MenuX.Click += …` 这行
**存在**，却证明不了**点下去会怎样**，更证明不了**接的是正确的处理器**。

真正的坑正好落在这个盲区里：`DeviceManagerView` 的构造函数曾在 `InitializeComponent()`
期间抛 NRE（XAML 的 `SelectedIndex="0"` 同步触发 `SelectionChanged`，而处理器读的
`SerialPanel`/`BaudPanel`/`TcpPanel` 声明在**后面**，那一刻全是 `null`）⇒
「设备管理」菜单**点了没反应**，自 `2adb1f8`（2026-08-11）起长期存在。
任何静态守卫都抓不到它 —— 阶段 48 是**碰巧**构造这个窗口才发现的。

## 2. 前置探针：三个问题的实测答案

在动手写判据之前先把“能不能测”问清楚（一次性探针，跑完删）。

| 问题 | 实测答案 | 对判据的意义 |
|---|---|---|
| `DataRootSandbox` 之外还需要什么？ | 不需要 —— headless 里 `new MainWindow(settings)` 就能建主窗口 | 面可选 |
| `dialog.ShowDialog(this)` 会阻塞 UI 线程吗？ | **不阻塞**。`RaiseEvent` 立即返回（8 次点击耗时 17–87 ms），对话框由返回的 Task 驱动 | 可以“点一下再看” |
| 打开的窗口进得了 `owner.OwnedWindows` 吗？ | **进**。8/8 次都是 `0 → 1`，类型正确、`IsVisible=True`；`Close()` 后掉回 `0` | **这就是判据的锚点** |
| `Application.Current.ApplicationLifetime` 可用吗？ | **不可用**（headless 里是 `null`）⇒ 没有全局窗口清单（Avalonia 12 的窗口清单只有 `ClassicDesktopStyleApplicationLifetime.Windows`/`.MainWindow`，实例级只有 `Window.OwnedWindows`） | 但上面那条说明**不需要**它 |

> 所以判据没有退化成“点了没抛异常”。它断言的是**强度**：增量恰好 1、类型精确匹配、
> 窗口可见。接错处理器（A 菜单接到 B 的处理器）会被“类型不符”直接抓住。

## 3. 为什么不能“遍历全部 62 个”

62 个菜单项里 **14 个**会碰外部世界：编译/构建（起构建进程）、烧录下载（ADB）、
`git init/commit/history/refresh`（起 git 进程）、新建/打开/保存工程（读写磁盘）、
导出代码与模拟器工程（写盘）、校验生成代码、以及 `MenuExit`（**关掉主窗口**，
后续点击全废）。

一发散就发现：**这 14 个的“危险”从 `.Click +=` 那一行看不出来** —— 它们形如
`MenuNewProject.Click += (_, _) => NewProject();`，危险在共享命令方法体内。
任何“按源码分类自动决定点不点”的做法都会把 `NewProject()` 归进“内部状态”。

⇒ 面必须是**显式分桶**，且分桶的**完整性**由守卫保证（见 §5）。

## 4. 四个桶

| 桶 | 数 | 断言 |
|---|---|---|
| **弹窗** | 18 | `OwnedWindows` 增量**恰好 1**，且新窗口类型 == 期望类型、`IsVisible` |
| **静默** | 28 | 增量**恰好 0** |
| **条件** | 2 | 增量 ∈ {0,1}；为 1 时类型必须相符（理由必填） |
| **跳过** | 14 | 不点，**理由必填**（≥10 字符） |

**条件桶为什么存在**（而不是硬塞进另两个桶）：

| 菜单项 | 为什么是条件 | 当前读数 |
|---|---|---|
| `MenuCreateTemplate` | `CreateTemplateFromSelection()` 在**无选中控件**时只写状态栏提示就 return | 0 |
| `MenuFaceplateEditor` | 有选中项或 `Templates.Count > 0` 才开编辑器 | 1（默认工程有预置模板） |

把这两个硬塞进“弹窗”桶会得到一条**依赖初始状态**的断言（哪天预置模板被清空就假红）；
硬塞进“静默”桶则把“它其实会弹窗”这件事记丢了。所以给它们第三个桶 + **必填理由**。

**跳过桶的 14 条**（理由写进代码，`MenuBucketTable_IsNotEmptiedAndIsSelfConsistent`
逐条断言理由长度）：

`MenuBuildRun` / `MenuCompile` / `MenuDownload` / `MenuExit` / `MenuExportCode` /
`MenuGitCommit` / `MenuGitHistory` / `MenuGitInit` / `MenuGitRefresh` /
`MenuNewProject` / `MenuOpenProject` / `MenuSaveProject` / `MenuSimulatorExport` /
`MenuValidateCode`

## 5. 三条判据与它们各自的守卫

### ① `EveryNamedMenuItem_IsClassified` —— 面守卫（双向差集）

窗口上**每个**具名菜单项必须被分到恰好一个桶里；桶里**每个**名字也必须真的在窗口上。

- 增量方向（窗口有、表里没有）⇒ 抓**新加菜单项没分类**，测试红并点名，**强制**有人判断它属于哪个桶；
- 反向（表里有、窗口没有）⇒ 抓**菜单项被删或改名**后分类表腐烂。

这是阶段 48「并集覆盖守卫」的同族形状（`EveryViewType_IsInSomeSmokeSurface`）：
**面超过一个时，各自的面自检不够，要加一条并集/全集守卫**。

### ② `MenuBucketTable_IsNotEmptiedAndIsSelfConsistent` —— 空集 + 表卫生

桶不能被掏空（表 ≥ 60 行、弹窗 ≥ 18、跳过 ≥ 12）—— 否则行为判据会在“表删空”之后
**永远为真**。外加两条：条件/跳过桶**理由必填且 ≥ 10 字符**；期望类型必须是 `Window` 派生。

> 理由长度的门槛只对**条件桶与跳过桶**生效。弹窗桶/静默桶的行为已经被精确断言，
> 理由只是注释 —— 对它们设门槛只会逼出凑字数（首跑就撞上：`MenuSaveProject` 的理由
> “保存工程：写磁盘”8 字符被卡，而那本来是一句清楚的话）。

### ③ `EveryClassifiedMenuItem_BehavesAsDeclared` —— 行为主判据

一条用例走完 48 个（而不是每个菜单一条 `Theory`）：建主窗口是这一步里最贵的动作，
逐条建会把这文件的开销抬到几十秒。失败逐项汇总、点名到具体菜单项，实测整条 **2 s**。

覆盖的边界条件（都在断言里）：

- 一次点击弹出 **> 1** 个窗口 ⇒ 红（防“某菜单偷偷多开”）；
- 关闭对话框后 `OwnedWindows` **没回到原值** ⇒ 红（抓泄漏）；
- 点击后**主窗口被关掉** ⇒ 红 + 立即中止（否则后续点击全废，会掩盖原因）。

## 6. 负控 8/8（含 1 条正控）

| 注入 | 期望 | 实测 |
|---|---|---|
| N1 复原 `DeviceManagerView` 构造期 NRE（去掉 null 前置检查） | 行为条红并点名 `MenuDeviceManager` | ✅ 红 1 条 |
| N2 **摘掉** `MenuDeviceManager` 的接线 | 行为条红，而**面守卫仍绿** | ✅ 红 1 条；`EveryNamedMenuItem_IsClassified` 仍绿 |
| N3 把它接到 `AlarmConfig` 的处理器（接错处理器） | 红并点名「弹出的窗口类型是 `AlarmConfigView`」 | ✅ 红 1 条 |
| N4 让静默项 `MenuThemeDark` 弹个窗 | 静默桶“增量必须为 0”被抓 | ✅ 红 1 条 |
| N5 分类表键改错一个字母 | 面守卫**双向**点名（missing + stale） | ✅ 红 2 条 |
| N6 **正控**：人口下界从 60 抬到 200 | 红（证明这条断言真的在执行） | ✅ 红 1 条 |
| N7 只改一行注释（无关改动） | **3/3 全绿**（不误报） | ✅ 全绿 |
| N8 把静默项 `MenuUndo` 标成弹窗 | 红（分类表的判定方向也生效） | ✅ 红 1 条 |

还原后 4 个文件 `sha256` **逐字节一致**。

**N2 与 N3 是这一轮最有价值的两个对照**：

- **N2** 证明「面守卫」与「行为判据」**互不替代** —— 摘掉接线后菜单项**还在树上**
  （面守卫绿），只有真点才能发现它已经不弹窗了；
- **N3** 是“接错处理器”的判据。这是阶段 46 遗留① 的原话里的诉求
  （“不证明接了**正确的**处理器”），而它**不需要任何源码解析**：期望表里写了类型，
  弹出的类型不符就红。

### 脚本自身的两个坑（都在负控首跑暴露）

1. **N6 的命中词写错**：注入把断言文案改成了“负控：下界抬到 200”，而 needle 还写着
   原文案“人口下界不成立” ⇒ 判为“未达预期”，**但判据本身是对的**（红了、点名了正确的用例）。
2. **N7 的 `failed=-1`**：xUnit 在全绿时**根本不打印「失败数」那一行**，解析器把它当成
   “找不到 ⇒ -1” ⇒ 判为未达预期。修法：缺失即视为 0。

两条都是**判读脚本的缺陷**，不是被测判据的缺陷 —— 但如果不看输出直接采纳“6/8”，
就会误判成两个真问题。

## 7. 本层**不**抓什么（写进类注释）

- **不做真实命中测试**（不点像素）⇒ 菜单**是否可见 / 能否展开**不在面内。
  这与 `HeadlessInputAndDialogUiTests` 的分工不同：那里是真实命中测试路径。
- **只覆盖 `Menu*`**。工具栏与面板内的 `Btn*`（129 个注册点里的另一大块）未覆盖 ——
  其中同样有 10 个是弹窗入口（`BtnUserManager` / `BtnAlarmConfig` / `BtnTrendConfig` /
  `BtnScriptEditor` / `BtnRecipeEditor` / `BtnVariableManager` / `BtnVariableImport` /
  `BtnScreenManager` / `BtnStyleEditor` / `BtnSimulator`）。
  **（2026-09-21 阶段 51 补记）主窗口工具栏那 65 个已覆盖**，专题
  `docs/2026-09-21-mainwindow-toolbar-invocation-net.md` —— 落实时发现**形状不同族**：
  65 个里有 **44** 个是 `CommandBar.PrimaryCommands` **集合属性**里的 `CommandBarButton`，
  **不进逻辑树** ⇒ 面必须换成 NameScope（按名 `FindControl`）。
  **剩下的**是主窗口**停靠面板内部**的 `Btn*`（`LayerPanelView` / `FaceplateLibraryView` /
  `PropertyPanelView` …）—— 登记表 #41。
- **跳过桶的 14 个行为无判据**（理由是它们唯一的凭据）。
- **不做“新加弹窗入口必须进表”的源码级保证**：靠的是面守卫在**运行期**发现
  “窗口上多了个没分类的菜单项”。如果有人把新入口挂在已有菜单项上（换个处理器），
  面守卫不会红，只有类型断言会红 —— 这恰好是 N3 抓的形状。

## 8. 读数

| 指标 | 值 |
|---|---|
| 新增用例 | **3**（`App.UiTests` 308 → **311**） |
| 八程序集合计 | 3001 → **3004** |
| 本判据耗时 | 面守卫 ~1.0 s + 表卫生 6 ms + 行为 2 s |
| 构建 | **0 警告 0 错误** |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**，日志 `.workbuddy/tmp/ci-gate-2026-09-20-menunet.log` mtime `22:15:49` 晚于提交 `c2aa5f6` 的 `22:14:36`（**73 s**） |
| 产品代码改动 | **0 行** —— 判据建起来就全绿（阶段 48 修掉的三个缺陷之后，接线本身是好的） |

## 9. 沙箱残留（✅ 已闭环 2026-09-22，阶段 74）

一次性探针 `tests/HmiEditor.App.UiTests/_ZZMenuInvocationProbe.cs` 当时**删不掉**：沙箱把
“会话内新出现的文件路径”做成**逐文件 bind-mount**（`/proc/mounts` 里能看到那一条），
于是 `rm` 报 `EBUSY`、`os.rename` 报 `EBUSY`、`umount` 反说“未挂载”；safe-delete shim 对
**文件**目标的回收站路径也算错了（`<file>/.Trash-0` ⇒ `NotADirectory` ⇒ fail-closed）。
当时的降级处置：把内容改写成**不含任何 `[Fact]`** 的说明性文件（对测试发现与门禁**完全惰性**），
并在 `.gitignore` 里加一条精确规则挡住它。

**阶段 74 已收口**：文件本身已删除、`.gitignore` 里那 3 行注释 + 1 行规则也已一并删掉。
**⚠ 定性更正**：上面那段「删不掉」是**产生它的那个会话内**的事实，**不是永久残留** ——
后续会话复查 `/proc/mounts`，那条 bind-mount 条目**已不存在**（`grep -c` = 0），`os.remove`
直接成功 ⇒ 它的生命周期就是那个会话，跨会话自动消失，不需要人工介入。
原文件内容（1406 字节、纯注释、无 `[Fact]`）早已落到正式判据
`MainWindowMenuInvocationUiTests` 与本专题，删除它**不影响任何判据**。

## 10. 仍未闭环

| # | 项 | 说明 |
|---|---|---|
| 1 | **`Btn*` 面未覆盖** | 另有 65 个具名按钮的 `.Click`，其中 10 个是弹窗入口；形状与菜单同族，可以直接套。**（2026-09-20 阶段 50 补记）** 其中「**宿主对话框内部**」那一部分已做掉：6 个宿主 / 57 个具名按钮 / 46 个真点，专题 `docs/2026-09-20-dialog-inner-invocation-net.md`。**（2026-09-21 阶段 51 补记）** 「**主窗口工具栏**」那一部分也已做掉：65 个具名按钮 / **58** 个真点，专题 `docs/2026-09-21-mainwindow-toolbar-invocation-net.md` —— **注意「形状同族可直接套」这条预判是错的**：那 65 个里有 44 个是 `CommandBar.PrimaryCommands` 集合属性里的 `CommandBarButton`，**不进逻辑树**，面必须换成 NameScope。**仍开的只剩**主窗口**停靠面板内部**的 `Btn*`（`LayerPanelView` / `FaceplateLibraryView` / `PropertyPanelView` …），登记表 #41 |
| 2 | **跳过桶的 14 项无行为判据** | 需要的是“不给真实文件系统 / 外部进程”的替身（`StorageProvider` 桩 + 构建服务桩），不是判据形状问题 |
| 3 | **条件桶的两项依赖初始状态** | `MenuCreateTemplate` / `MenuFaceplateEditor` 的当前读数来自“默认工程无选中 / 有预置模板”——要变成无条件断言需先在画布上放控件，属“主动构造状态”能力 |
| 4 | **不做真实命中测试** | 菜单可见性 / 展开 / 快捷键（`KeyDown += OnWindowKeyDown` 那条）不在面内 |
| 5 | **`MenuSimulator` 之外的窗口生命周期** | 本层只判“弹出来了、关掉了、OwnedWindows 回到原值”，不判关窗后资源释放是否干净（与登记表 #37 遗留② 同族） |
