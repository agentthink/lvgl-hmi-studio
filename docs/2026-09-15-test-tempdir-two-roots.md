# 测试临时目录：两个根，两种需求

日期：2026-09-15
状态：已完成（待提交）

## 1. 症状与归因

全量 `dotnet test HmiEditor.slnx` 会**随机**红几项，失败项每次都不一样，报错多为
`IOException: disk was full` 或 `No space left on device`；单独复跑该测试类立刻全绿。

按根因分类，其实是**三件事**叠在一起，被当成了一个"偶发"：

| # | 根因 | 表现 |
|---|---|---|
| A | 本机 `/tmp` 是 **10 MB tmpfs**，而导出/编译类测试会写出整套模拟器工程（数百 KB） | 全量并行（7 个测试宿主）时被写满 |
| B | `TestTempDir` 早已存在且能规避 A，但**只有一部分测试用它** | 14 个文件、**190 处**绕过 `Path.GetTempPath()` / `Directory.CreateTempSubdirectory()` / `Path.GetTempFileName()` |
| C | 其中一批测试的语义是「**从给定目录向上搜索**」，临时目录落点本身会影响结论 | 起点在仓库内 → 搜索逃逸进真实仓库 |

### A 的量化

```
$ df -h /tmp
tmpfs  10M  0  10M  0%  /tmp
```

`Directory.CreateTempSubdirectory()` 是**最隐蔽的那个绕过点** —— `grep Path.GetTempPath`
抓不到它，而它内部就是 `Path.GetTempPath()`。`BuildPipelineServiceTests` 那条 flaky 的真凶就是它。

### C 的硬证据

`HmiEditor.Device.Tests/T113SoBuilderTests` 里，当临时目录落在仓库内时：

```
Actual: "/home/chen/Git/zhishu-hmi/firmware/Tina-SDK"
```

测试本来断言"配置无效 ⇒ `FindSdkRoot` 返回 null"，结果**向上走出临时目录**，在仓库里找到了
被 git 追踪的 `firmware/Tina-SDK`。这与"`git` 类测试不能落在工作树里"是同一个失效模式。

### C 的反向教训（重要）

第一版整改把 T113 那 12 个用例**全部**迁到隔离根，结果第 9/10 轮全量 **12 红**：

```
InvalidOperationException: 找不到可用的「隔离型」测试临时目录
  - /tmp/：No space left on device
```

因为隔离根本来也去挤 10 MB 的 `/tmp`。第 1 轮（全部落在仓库内）**只有那 3 个**真做向上搜索的用例红
—— 说明"整类都是向上搜索"的判断过宽。**只有 3 个用例要位置，其余 9 个只要空间。**

## 2. 设计：两个根

`tests/HmiEditor.TestSupport/TestTempDir.cs`（从 `tests/HmiEditor.Core.Tests/Support/` 迁出，扩成共享库）

| | 空间根 | 隔离根 |
|---|---|---|
| API | `Root` / `NewDir` / `NewFilePath` / `DeleteDir` / `DeleteFile` | `IsolatedRoot` / `NewIsolatedDir` |
| 要什么 | **空间** | **位置**（必须在仓库树之外） |
| 探测阈值 | `RequiredBytes = 16 MB` | `RequiredBytesIsolated = 256 KB` |
| 候选顺序 | `HMI_TEST_TMPDIR` → `TMPDIR` → `Path.GetTempPath()` → **仓库内 `.hmi-test-tmp/`** | `HMI_TEST_TMPDIR` → `TMPDIR` → `Path.GetTempPath()` → `/var/tmp`（**仅当已存在**）→ **仓库同级 `<repo>.test-isolated-tmp/`** |
| 落在仓库内时 | 允许（兜底） | **直接拒收**，且全部落空时报错而**不静默回退** |

两个设计要点：

1. **隔离根不能静默回退到仓库内** —— 回退会让上层测试失去隔离语义而**假绿**（正是要防的那件事），
   所以宁可硬失败并列出每个候选的拒绝理由。
2. **隔离根需要「同盘 + 大空间 + 确定在仓库外」的兜底**。系统临时目录常常就是小 tmpfs；
   没有兜底时，一次瞬时占满会把"临时目录不够用"升级成 12 个硬失败。`<repo>.test-isolated-tmp/`
   与仓库同盘（继承仓库所在卷容量）且是仓库的**同级**而非子目录 —— 向上搜索不会进入仓库。
   顺序上排在系统临时目录**之后**：它只是补救，不是默认。
3. `/var/tmp` 只在**已存在**时作为候选 —— 以 root 跑测试时 `Directory.CreateDirectory` 会把它
   凭空建出来，属于不可接受的副作用。

## 3. 改动清单

- 新建 `tests/HmiEditor.TestSupport/`（`TestTempDir.cs` + `csproj`，`IsTestProject=false`，不入 `IsPackable`）。
- `HmiEditor.slnx` 加入该工程；三个测试工程加 `ProjectReference` +
  `<Using Include="HmiEditor.TestSupport" />` 全局注入。
- 删除旧的 `tests/HmiEditor.Core.Tests/Support/TestTempDir.cs`。
- **190 处**机械迁移，另含几处人工处理：
  - `BuildPipelineServiceTests` 4 处 `Directory.CreateTempSubdirectory(...)`（**flaky 真凶**）。
  - `SimulatorFixVerification` 删掉硬编码的 `/tmp/zhishu_verify/` 写盘段落（那段 `code` 只被
    `CSource.AssertCode` 断言，本就**不需要落盘**）。
  - `AppThemeServiceTests` 9 处 + 给原本**没有** `try/finally` 的 `SetThemeByName_UnknownName_KeepsCurrent` 补上。
  - `HmiPackageServiceTests`：`Unpack(ms, Path.GetTempPath())` → `NewDir("hmi-unpack-invalid")`。
  - `SwuPackageBuilderTests` 2 处裸 `File.Delete` → `DeleteFile`。
  - `Delete` 更名为 `DeleteDir`，新增 `NewFilePath`（**不创建文件**）/ `DeleteFile`。
- 白名单（**故意不复用**）：`GitWorkflowServiceTests` 自建 `ResolveTempRoot`（检查
  `IsInsideGitWorkTree`，回退 `/tmp`、`/var/tmp`），`CleanupDir` 带 Windows 句柄重试 —— 它是历史实现，
  语义与 `NewIsolatedDir` 等价但更保守，不强行合并。

## 4. 守卫

`tests/HmiEditor.Core.Tests/TestTempDirUsageGuardTests.cs`（2 项）：

1. `NoTestUsesSystemTempDirectoryDirectly` —— 扫 `tests/` 全目录（跳过 `/obj/`、`/bin/`、纯注释行），
   禁止 `Path.GetTempPath()` / `Directory.CreateTempSubdirectory(` / `Path.GetTempFileName()`。
2. `AllowedExceptions_PointToExistingFiles` —— 白名单**本身也断言**：指向的文件必须存在。
   防"守卫静默退化"（白名单文件被改名/删除后守卫继续全绿，实际放行了别处）。

白名单 3 条，各带理由：`TestTempDir.cs`（回退链候选，正是要调用它）、
`GitWorkflowServiceTests.cs`（真实执行 `git init`）、守卫自身（把被禁模式作为**数据**列出）。

范围有意收窄：`AssetItem.FilePath = "/tmp/fake.bmp"` 这类**从不读写**的假路径字面量不在拦截范围 ——
拦它们会产生几十处无收益改动，稀释守卫信号。

## 5. 负向验证

| # | 破坏动作 | 实测 |
|---|---|---|
| NV-A | `AuditLogStoreTests.NewTempFile()` 改回 `Path.GetTempPath()` | 守卫红，**只报 1 处**且行号精确（`AuditLogStoreTests.cs:14`） |
| NV-B | 白名单加入不存在的 `NvBogusFileThatDoesNotExist.cs` | `AllowedExceptions_PointToExistingFiles` 红，消息精确 |
| NV-C | 临时把「仓库同级兜底」提到候选**最前** | 删掉该目录后重跑 63/63 全绿、目录被重建 → 兜底可用 |
| 对照 | 三处全部还原 | 守卫复跑 2/2 绿 |

**一次失败的验证姿势（记录下来）**：最初想用 `TMPDIR=/proc/nope-xyz` 毒化环境来证明回退链，
结果**先把 MSBuild 自己打崩**（`MSBuildApp.BuildProject` → `Directory.CreateTempSubdirectoryCore`）
—— MSBuild 同样依赖 `Path.GetTempPath()`。**不能用毒化 `TMPDIR` 的方式做这类负向验证。**

## 6. 验收

`env -u TMPDIR -u HMI_TEST_TMPDIR dotnet test HmiEditor.slnx`（**不带** `TMPDIR`，即修复前会随机红的条件）：

| 轮 | 结果 |
|---|---|
| 1（全在仓库内） | Core 1541 ✅ / Device **3 红**（向上搜索逃逸） |
| 2 | **1 红**（日志丢块，无消息） |
| 5–8 | 全绿 |
| 9 | **12 红**（隔离根挤 10 MB `/tmp` → ENOSPC） |
| 11、12 | 全绿 |
| 13 | **1 红**（`IsKitRoot_WithMarker_ReturnsTrue`）→ 触发重新归类 |
| 14 | 1 红（`DeviceChannelTests.ConcurrentCommands_AreSerialized_NoCrossTalk`，见下） |
| **15、16** | **2070 / 2070 全绿（连续两轮）** |

测试项分布：Core 1541 / AI 237 / App.UiTests 91 / Comm 75 / Device 63 / Simulator 41 / Hardware 22。

第 14 轮那条红与本次改动无关：`DeviceChannelTests.cs` 未在改动列表内，是 TCP loopback 并发用例，
全量并行时偶发，单独复跑 4/4 绿。已作为**已知非临时目录类间歇失败**记入项目记忆。

## 7. 教训

1. **"偶发红"要先按根因分类**。这里三条根因（空间不足 / 绕过唯一入口 / 落点影响语义）症状都是"随机红"，
   混在一起会误判成"环境问题，忍了"。
2. **语义敏感项和空间敏感项要分开处理**。一刀切"全部用最安全的那个"会把只需空间的用例推到更脆弱的位置
   （10 MB `/tmp`），从"偶发红"变成"必然红"。
3. **隐蔽绕过点靠 grep 抓不全**。`Directory.CreateTempSubdirectory()` 不含 `Path.GetTempPath` 字样。
   收口唯一入口之后，**用守卫测试把它固化**，而不是靠人工纪律 —— 这次的不变量正是这样丢掉的。
4. **白名单要有反向断言**。否则删掉被豁免的文件、换个新文件重犯，守卫照样全绿。
5. **负向验证的注入点不能选在依赖链上游**。毒化 `TMPDIR` 打崩的是 MSBuild 自己，不是被测逻辑。
