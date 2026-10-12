# 应用路径单一来源：16 处各自解析 → `AppPaths` 两个根

> 2026-09-15（第十轮）。承接第九轮的**验证途中发现**：那条间歇失败
> （`DeployAsync_RuntimeConfigText_UploadsCommConfig`）的根因不是"测试写得不好"，
> 而是**产品代码往系统临时目录写固定文件名**；而守卫的扫描面只到 `tests/`，
> 产品侧的同类写法一直没有守卫。
>
> 摸清后发现这不是一行，是**一族 16 处**。

## 1. 归因：三类语义混在两个（其实是三个）根里

修前，产品侧有 **10 处** `Path.Combine(Path.GetTempPath(), "固定名")`：

| # | 位置 | 名字 | 真实语义 |
|---|---|---|---|
| 1 | `Device/Build/T113SoBuilder.cs:340` | `zhishu_hmi_so_build` | 交叉编译工作目录 |
| 2 | `Device/Updates/HmiDeployService.cs:124` | `zhishu_hmi_comm_config.ini` | **中转文件** |
| 3 | `App/MainWindow.axaml.cs:694` | `HmiEditor/ai-icons` | **用户产物** |
| 4 | `App/MainWindow.axaml.cs:1249` | `hmi-editor/<name>-<guid>` | 工程资源工作目录 |
| 5 | `App/MainWindow.axaml.cs:1511` | `zhishu_hmi_sim/<name>` | 模拟器工作目录 |
| 6 | `App/Views/DownloadToHmiView.axaml.cs:111` | `zhishu_hmi_download/libhmi_ui.so` | **读侧候选** |
| 7 | `App/Views/AiIconDialog.axaml.cs:218` | `HmiEditor/ai-icons` | **用户产物** |
| 8 | `App/Views/AiIconDialog.axaml.cs:449` | `HmiEditor/ai-images` | **用户产物** |
| 9 | `App/Services/BuildPipelineService.cs:140` | `zhishu_hmi_compile` | 编译输出目录 |
| 10 | `App/Services/BuildPipelineService.cs:242` | `zhishu_hmi_download` | 下载暂存目录 |

**把三种语义塞进了同一个"系统临时目录"**，于是各自出各自的错：

| 语义 | 错在哪 |
|---|---|
| **用户产物**（3、7、8） | 系统清理临时目录时**资产直接消失**。原注释写着"使用系统临时目录兜底，**保证生成结果不丢失**"——恰好放在了**会丢**的地方。**意图与实现相悖** |
| **中转文件**（2） | 固定名 + 共享目录 ⇒ 两个实例互相覆盖；且**从不删除** |
| **工作目录**（1、4、5、9、10） | 固定名落在共享目录；本机 `/tmp` 只有 10 MB tmpfs，写满即 `IOException` |

另有 **6 处**各自解析应用数据根（`SpecialFolder.ApplicationData` + 一个目录名），
而且目录名**分了两种**：`zhishu-hmi`（主题/设置/日志/布局/窗口几何/资源库）
与 `HmiEditor`（LLM 配置、审计日志）。

### 1.1 决定性实验：把 tmpfs 填满

`/tmp` 是 10 MB tmpfs。填满后单跑那条用例：

```
System.IO.IOException : No space left on device : '/tmp/zhishu_hmi_comm_config.ini'
  at HmiEditor.Device.Updates.HmiDeployService.DeployAsync ... HmiDeployService.cs:line 125
```

必红；删掉填充文件必绿。**全量跑 3 次红 1 次**（单独复跑全绿）与之吻合 ——
"看着像 flaky" 的东西被变成了"已确证"。

### 1.2 为什么长期没被发现：两个缺口叠出来

1. **守卫的扫描面只到 `tests/`**。`TestTempDirUsageGuardTests` 用
   `FindTestsRoot()` 取扫描根（当年它自己也是因为"14 文件 190 处绕过"才被写出来的）。
   产品侧的同类写法**从来没被任何守卫覆盖**。
2. **写侧与读侧各写一份固定路径**。`BuildPipelineService` 写
   `zhishu_hmi_download`，`DownloadToHmiView` 读同一串字面量 —— 典型的两处判据分叉，
   改一处不改另一处就静默失效。

第三条是**观察到的**而非推测：测试侧的违规数是 **0**，产品侧却有 10 处。
"守卫全绿 ≠ 不变量成立"在这里得到了最直接的一次印证。

## 2. 设计：`Core/Services/AppPaths.cs`，两个根 + 四个装点

```csharp
public static string DataRoot                 // 持久：%APPDATA%/zhishu-hmi（HMI_APP_DATA_ROOT 可覆盖）
public static string TempRoot                 // 可清理：DataRoot/temp
public static string AssetDir(string kind)    // 持久产物：DataRoot/assets/<kind>（建）
public static string TempPath(params string[] parts)      // 只拼，不建（读侧）
public static string TempSubdir(params string[] parts)    // 拼 + 建（写侧）
public static string NewTempSubdir(string prefix)         // 唯一子目录（建）
public static string NewTempFilePath(string prefix, string ext = "")  // 唯一文件名（不建）
```

三条设计决定：

**① 落点在 `Core`，代价是给 `HmiEditor.Device` 加一条 `→ Core` 引用。**
`Device` 原是**零依赖叶子**（只有 `System.IO.Ports`），10 处里有 2 处在它里面。
可选的三个落点：`Core`（Device 得加引用）、`Device`（App 已引用它，但把"应用路径策略"
放进"设备通讯"层语义反了）、新建一个共享叶子程序集（为一个类加一个工程，过重）。
选 `Core`：它是唯一的根（自身不引用任何项目）⇒ **无环**，且 App 早已引用它。

**② 临时根**有意**不用** `Path.GetTempPath()`，而是 `DataRoot/temp`。**
中间产物要的是**空间 + 可预测**，不是"系统会替我清理"。这样 `src/` 全域可以立一条
最干净的不变量：**零出现系统临时目录 API**（`AppPaths` 自己的兜底链也**不含**它 ——
`ApplicationData → LocalApplicationData → UserProfile`，全空就显式报错，
而不是退回"当前目录"把文件写进工作树）。

**③ 有意不做缓存。** 用 `Lazy<string>` 缓存会让"先访问 `AppPaths`、后改环境变量"的
调用方静默拿到旧值 —— 测试里这是**必然**踩的坑。不缓存时
`Environment.GetFolderPath` 的开销可忽略（运行时内部已缓存）。

### 2.1 命名约定：为什么 `TempRoot` 里允许固定名

`TempRoot` 是**应用私有**根，跨应用不会撞名；但**同一应用的两个实例**仍会撞。所以：

| 用途 | API | 名字 |
|---|---|---|
| 跨次复用（增量编译、模拟器工作目录） | `TempSubdir` | **稳定名** |
| 每次调用独立（工程工作目录） | `NewTempSubdir` | `<prefix>-<guid>` |
| 中转文件（下发到设备） | `NewTempFilePath` + 调用方**用完即删** | `<prefix>-<guid><ext>` |
| 用户产物 | `AssetDir` | 稳定名，且在**持久**根下 |

## 3. 16 处映射

| 原写法 | 现写法 |
|---|---|
| `GetTempPath()/zhishu_hmi_so_build` | `AppPaths.TempSubdir("so-build")` |
| `GetTempPath()/zhishu_hmi_comm_config.ini` | `AppPaths.NewTempFilePath("comm-config", ".ini")` + `finally` 删除 |
| `GetTempPath()/HmiEditor/ai-icons`（×2） | `AppPaths.AssetDir("ai-icons")` |
| `GetTempPath()/HmiEditor/ai-images` | `AppPaths.AssetDir("ai-images")` |
| `GetTempPath()/hmi-editor/<name>-<guid>` | `AppPaths.NewTempSubdir(name)` |
| `GetTempPath()/zhishu_hmi_sim/<name>` | `AppPaths.TempSubdir("sim", safeName)` |
| `GetTempPath()/zhishu_hmi_download/libhmi_ui.so`（读） | `AppPaths.TempPath("download", "libhmi_ui.so")` |
| `GetTempPath()/zhishu_hmi_compile`（写） | `AppPaths.TempSubdir("compile")` |
| `GetTempPath()/zhishu_hmi_download`（写） | `AppPaths.TempSubdir("download")` |
| `ApplicationData/zhishu-hmi`（×4：App 主题设置、资源库、日志、布局、窗口几何） | `AppPaths.DataRoot` |

写侧与读侧从此由 `TempSubdir("download")` / `TempPath("download", …)` **同一句话**推导，
分叉在结构上不再可能。

## 4. 测试进程必须重定向应用数据根

收口的副作用：产品默认根从 `/tmp` 变成 `%APPDATA%/zhishu-hmi`，
**测试会把中间产物写进开发者真实的配置目录**。

做法：`tests/_shared/TestAppDataRootSetup.cs` 用 `[ModuleInitializer]` 把
`HMI_APP_DATA_ROOT` 指到仓库内 `.hmi-test-tmp/appdata/`，由
`tests/Directory.Build.props` 统一 `Compile Include` 进**每个测试程序集**。

- 用模块初始化器：必须在任何被测代码首次读 `AppPaths` **之前**生效，
  放在测试类里不可靠（xunit 执行顺序不保证）。
- **排除 `HmiEditor.TestSupport`**：它是辅助库不是测试程序集，
  带上会触发 `CA2255`（该分析器只在测试/应用项目放行）。
- 外部已设置该环境变量时**不覆盖**（CI / 人工排查可显式指定）。

## 5. 守卫（静态扫描 `src/`）

新增 `tests/HmiEditor.Core.Tests/AppPathsSingleSourceGuardTests.cs`，
补齐原守卫缺失的那一侧。**本轮落 4 条，后续两轮各增补**（第十二轮后共 7 条）：

| 用例 | 断言 | 轮次 |
|---|---|---|
| `ProductCode_DoesNotUseSystemTempDirectoryAtAll` | `src/` 零出现 `Path.GetTempPath()` / `CreateTempSubdirectory(` / `GetTempFileName()`（跳过注释行） | 10 |
| `AppDataRoot_IsResolvedOnlyByAppPaths` | `SpecialFolder.ApplicationData` / `LocalApplicationData` 只允许在 `AppPaths.cs` + 例外表（**现为空**） | 10 |
| `AppDataRootAllowList_IsEmpty_AndAnyEntryPointsToARealFile` | 例外表必须为空；万一有条目，其指向的文件必须存在（防"守卫静默退化"）。第十二轮由 `DataRootAllowList_PointsToExistingFiles` 扩展而来 | 10 → 12 |
| `Scanner_ActuallyCoversTheSourceTree` | 扫描面自检：≥100 个 `.cs`，且 7 个关键文件必须在面内 | 10 |
| `TempRoot_IsNotComposedOutsideItsSingleSource` | 外部不得读 `TempRoot` 自己拼路径（会绕过会话层 ⇒ 唯一名产物变无主） | 11 |
| `SessionCleanup_IsWiredIntoTheApplication` | 释放 / 孤儿清理必须都有产品侧调用点（防"实现了没人调"） | 11 |
| `LegacyDataFolderName_IsNotRecomposedOutsideItsSingleSource` | 历史目录名字面量 `"HmiEditor"` 不得在 `AppPaths.cs` 之外被重新拼出来 | 12 |

**新守卫也被老守卫拦了一次**：它把被禁模式作为**数据**列出，
于是 `TestTempDirUsageGuardTests` 判它违规（3 处）。按既有机制加白名单并写明理由 ——
这恰好演示了"守卫自身把被禁模式作为数据"这条例外是**必要**的。

## 6. 负向验证（7 组，红集两两不同）

| 组 | 注入（只改这一件事的等价替换） | 红 |
|---|---|---|
| a | `AppPaths` 不再认环境变量覆盖 | 2（`DataRoot_Honours…`、`DataRoot_IsRedirected…`） |
| b | 部署中转文件退回「系统临时目录 + 固定名」 | 3（产品侧守卫 1 + Device 2：`RuntimeConfigText_UploadsCommConfig`、`Concurrent…DistinctStagingFiles`） |
| c | `NewTempFilePath` 去掉 guid（唯一名 → 固定名） | 2（`NewTempFilePath_IsUnique…`、`Concurrent…DistinctStagingFiles`） |
| d | 中转文件不再用完即删 | 1（`DeployAsync_RuntimeConfigText_UploadsCommConfig`） |
| e | 日志目录退回自行解析 `ApplicationData`（**行为完全相同**） | 1（`AppDataRoot_IsResolvedOnlyByAppPaths`） |
| f | **填满 `/tmp` 后单跑**（修复态 vs 旧实现态） | 见 §6.1 |
| g | 去掉测试进程的根重定向 | 1（`DataRoot_IsRedirectedInTestProcess`） |

- **b 与 c 各自红的集合不同**：b 让"固定名 + 系统临时目录"同时违反静态不变量与
  并发唯一性；c 只违反唯一性。两条都命中 `Concurrent…DistinctStagingFiles`，但另一条
  各不相同 ⇒ 守卫覆盖的是两件独立的事。
- **e 是纯"单一来源"破坏**（行为一字不差）⇒ 只有静态守卫能发现它。这正是"接线性
  性质用静态扫描、运行时性质用行为测试"的分工。
- 还原后重建 OK，全量复跑全绿。

### 6.1 NV-f：把 tmpfs 填满（本轮最关键的一组）

用**真实满盘**代替"看代码推断"，验证修复是否真的解耦：

| 状态 | `/tmp` 填到 100% 后单跑该用例 |
|---|---|
| 修复态（中转文件落 `AppPaths.TempRoot`） | **绿**（1 passed） |
| 旧实现态（注入 b） | **红** `System.IO.IOException : No space left on device : '/tmp/zhishu_hmi_comm_config.ini'` |

**两次纪律教训，都是实测踩出来的：**

1. **构建必须在填满 `/tmp` 之前做完**（脚本里用 `--no-build` 单跑）。MSBuild 自己也用
   `/tmp`，满盘时崩的是**构建工具**而不是被测逻辑 —— 同 `dotnet-test-tempdir-hygiene`
   里"禁止毒化 `TMPDIR`"的道理。
2. **日志/重定向目标不能写在满盘上**。第一次跑把 `> /tmp/nv-f-test.log` 写在被填满的
   `/tmp` 上，shell 重定向直接失败，现象是 `dotnet test` "已中止（核心已转储）" ——
   看起来像**测试宿主崩了**，实际是**日志写不进去**。把日志改写到仓库内后立刻得到干净结果。
   ⇒ "进程 abort" 不等于"被测逻辑有问题"，先看**是谁的 IO 失败了**。

## 7. 验证

- `dotnet build`：**0 警告 0 错误**（含一次 `--no-incremental` 全量重建）。
- `HmiEditor.Core.Tests`：1604 → **1618**（+14：`AppPathsTests` 10 条 + 守卫 4 条）。
- `HmiEditor.Device.Tests`：63 → **64**（+1：并发唯一性）。
- 全量：**2214 通过 / 0 失败**（7 项目：44 + 22 + 75 + 64 + 272 + 1618 + 119；基线 2199，**+15**）。

## 8. 遗留

1. ~~**应用数据根还留着第二个目录名 `HmiEditor`**（`LlmConfigStore` 的
   `ai.config.json`、`MainWindow` 的 `audit.log.jsonl`）。
   **本轮有意不迁**：改根会让用户已配置的 LLM 端点/密钥失效、审计历史读不到 ——
   必须先有迁移策略（首次启动时从旧目录搬过来 + 保留回退）。两处已在守卫白名单里带理由登记。~~
   **✅ 2026-09-15（第十二轮）已解决**：历史位置收进 `AppPaths.LegacyDataRoot` /
   `LegacyDataFile`（**读回退、写不回退**），守卫例外表**清空并断言为空**。
   两处的迁移策略按数据形态分别设计（覆盖式配置 vs append-only 日志）——
   专题见 [`docs/2026-09-15-app-data-root-migration.md`](2026-09-15-app-data-root-migration.md)。
2. **`TempRoot` 没有自动清理策略**。它是应用私有、可整目录手工删除的，但
   `TempSubdir("so-build")` / `"compile"` 这类稳定名目录会留东西。
   "启动时清理"会破坏增量编译复用，需要按目录分类的策略（如 `sim`/`compile` 保留、
   `so-build` 每次清理）。
3. **`TempSubdir` 的稳定名在同一应用的两个实例间仍会共享**（`so-build`、`compile`、
   `download`、`sim/<name>`）。本轮修掉了"跨应用共享的系统临时目录"这一层，
   多实例隔离需要再加一层（按进程/会话分目录），属独立议题。
4. `DeviceChannelTests.ConcurrentCommands_…` 那条老间歇失败走本机 TCP 回显 + 时序，
   与本题**机制不同**（属 ThreadPool/时序族），未统一。
