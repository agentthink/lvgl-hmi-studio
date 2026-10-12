# 异常原文直通本地化模板 · 三分类与优先级（登记 #61 的定性）

> 阶段 83（2026-09-23）。**本阶段刻意不改任何行为** —— 它产出的是**一次证伪**与**一张分好类的清单**，
> 目的是拦住一条看起来便宜、实际是**假闭环**的路。
> 实测脚本：`.workbuddy/tmp/s83-catch-shape.py`、`.workbuddy/tmp/s83-producer.py`；
> 读数原文：`.workbuddy/tmp/s83-shape.txt`、`.workbuddy/tmp/s83-producer.txt`。

> ⚠ **阅读提示（阶段 85 加）**：本文档 §83–§84 的多处论证建立在**一个当时为真的前提**上 ——
> 「`ExternalText.FailureReason` 只在 `ex is HmiPackageException` 上分叉 ⇒ BCL 异常没有码」。
> 该前提**已被阶段 85（登记 #61e）推翻**：现在它分**三**支，BCL 类型**有码**。
> 受影响且**已加后记**的段落：§83.3 的静态证明、§84.3 第 1/2 条、§84.4 的依赖关系。
> ⇒ **凡读到「换过去一字不变 / 恒等于改名」，请按 §85 修正后再引用**；
> 本文档刻意**不改动**那些历史读数（它们是当时的实测记录，改了就毁掉了可核性）。

> ⚠ **阅读提示（阶段 86 加）**：§84.1–§84.3 与 §85.7-1 里「C 类**已经改期**、等 #61e 之后再动」的结论
> **已在本阶段执行完毕** —— #61b **已闭环**。⇒ 读 §84 的"改期"段请按 §86 修正：
> 那 9 站里 **8 站已迁**（第 9 站批量档归 #61a），站点面 37 → 29。
> 另外本阶段新增了一条判据（§86.4），它管的是那张面**看不见**的一半（"带上了路径"vs"三实参填 null"）。
> 本文档同样**不改动** §83/§84 的历史读数。

---

## 83.1 一句话结论

上一阶段（82）给 #61 写下的修法方向是「**把 `.Message` 换成 `ExternalText.FailureReason(ex)`**」。
阶段 83 实测**推翻了它**：那 37 处站点**清一色是宽捕获**，而 `FailureReason` 只在
`ex is HmiPackageException` 上分叉 ⇒ 换过去**只会落进回落分支**，**用户所见一字不变**，
而站点面守卫**绿着变空**。这是**改名式假闭环**。
⇒ 真收口在**产出方**（让 Core / AI / 插件抛带码异常），不在站点。

## 83.2 量的那一遍（口径与被测量）

口径 = 沿用阶段 82 的站点面（`.Format(` 首实参是字典键 且 后续实参含 `.Message`），
**多量两层**：① 该站点**往前最近的 `catch (`** 是什么类型；② `try` 块里出现的前 4 个 `X.Y(` 调用（产出方）。

⚠ **两点口径说明**：① 判据的语料在「阶段 82 补记」里已**收口到全 `src/`**（12 个项目 / 381 个 `.cs`，
见 `docs/2026-09-22-open-failure-file-path.md` §82.12），而本阶段这两个探针**只扫 `src/HmiEditor.App`**
—— 两边读数**一致**（Core / AI 各 **0** 处）⇒ **37 / 27 这个总数在两种口径下相同**；
② 表 3 的产出方是**启发式**（最近的 `try` + `X.Y(` 调用），只当**排期筛子**用，**不当证据**。

被测量：**37 站点 / 27 个 `(文件, 键)` 组合 / 24 个键**（与 J3 基线一致）。

### 表 1 · 捕获类型（决定性）

| 捕获形态 | 站点数 |
|---|---|
| `catch (Exception ex)` —— **宽捕获** | **36 / 37** |
| `catch (ArgumentException ex)` —— 具体类型 | 1（`Views/UserManagerView.axaml.cs:257` · `user.mgr.create.failed`） |

### 表 2 · 族规模（键 → 站点数）

| 键 | 站点 | 键 | 站点 |
|---|---|---|---|
| `ai.gen.failed` | **8**（3 文件） | `status.simulator.export.failed` | 2 |
| `asset.import.fail` | 3 | `gen.export.failed` | 2 |
| `audit.export.c.code.failed` | 2 | `compile.failed` | 2 |
| 其余 18 个键（`status.save.failed` / `status.reload.failed` / `status.verify.failed` / `audit.verify.code.failed` / `status.svg.apply.failed` / `buildrun.log.error` / `download.sharedlibrary.failed` / `comm.slave.var.import.failed` / `comm.slave.var.export.failed` / `comm.test.failed` / `font.export.fail` / `gen.import.failed` / `login.failed` / `recipe.clone.failed` / `sim.reg.read.failed` / `sim.reg.write.failed` / `user.mgr.create.failed` / `var.import.failed`） | **各 1** | | |

### 表 3 · 产出方热点（`try` 块里出现的调用，跨站点计数）

`GeneratedCodeValidator.Validate` **4** · `AppPaths.TempSubdir` 4 · `File.ReadAllTextAsync` 3 ·
`ExportConfig.ResolveOutputDirectory` 3 · `SvgParser.Parse` 2 · `CsvVariableImporter.Parse` 2 ·
另有 `Directory.CreateDirectory` 6 · `File.WriteAllTextAsync` 5 · `IO.StreamReader` / `IO.StreamWriter` 各 1。

## 83.3 静态证明：改名**恒等于零收益**（不是"收益小"）

三段都在**读代码**就能闭合，不需要实验：

1. `ExternalText.FailureReason` 只有一个分叉：`ex is HmiPackageException package ? Resolve(码→字典) : ex.Message`
   （`src/HmiEditor.App/Services/ExternalText.cs:138-141`）。
2. 站点侧变量是**宽捕获**（36/37）⇒ 站点自己**不知道**异常是什么 ⇒ 唯一能走码化分支的条件是
   **运行期恰好收到 `HmiPackageException`**。
3. 全仓 `HmiPackageService.Unpack` 的调用点**只有一个**：`MainWindow.axaml.cs:1034`（打开路径）
   —— **阶段 82 已经把它迁走了**；而 `Pack`（`MainWindow.axaml.cs:1227` / `:1362`）
   **不抛**码化异常（码只在 `Unpack` 的前置校验里，`HmiPackageService.cs:208-233` / `:348`）。

⇒ 这 37 处里**没有一处**今天能收到 `HmiPackageException` ⇒ 改名后**渲染结果与今天逐字节相同**，
面却从 37 掉到 0。**「面变小」不等于迁移成功**。

> ⚠ **后记（阶段 85）**：上面第 **1** 条的前提（"只有一个分叉"）**已不成立** ——
> 现在 `FailureReason` 分三支，第 2 支是 **BCL / 系统类型码**（登记 #61e）。
> ⇒ 第 2 条那个推论「唯一能走码化分支的条件是运行期恰好收到 `HmiPackageException`」
> **对 BCL 类型不再为真**：宽捕获收到的 `UnauthorizedAccessException` 之类**今天也会走码化分支**。
> ⚠ 但第 3 条与它上面的结论**仍然成立**：这 37 处**没有一处**因为 #61e 就自动变好 ——
> 它们改的是**渲染出口**，而站点仍然在往模板里塞 `ex.Message`（面本身没动）。
> ⇒ 「面变小 ≠ 迁移成功」这句**没有被推翻**，只是"改名"这个具体罪名**只对 `FailureReason`
> 走不到第 2 支的那些类型**还成立。详见 §85。

## 83.4 三分类（按"该不该本地化 / 该动谁"分）

⚠ **这张表的置信度分两档**：标【已读】的是我这轮**读了产出方代码**（或产出方名自明到无可争议）的；
标【待读】的**只有启发式产出方**（如表 3 里出现 `Task.Run` 这种无用读数）⇒ **不许拿它当理由表用**，
它只是**排期筛子**。这正是登记 #54 的教训：理由表里没人核过的字段会变成把缺陷合法化的字段。

### A 类 · 产出方是**本仓库**、语义明确 ⇒ **可码化**（真收口的候选）

| 键 | 站点 | 产出方（证据） | 置信度 |
|---|---|---|---|
| `status.verify.failed` / `audit.verify.code.failed` / `status.simulator.export.failed`(2) | 4 | `GeneratedCodeValidator.Validate` / `.HasErrors` + `Views.ValidationResultView` | 【已读】产出方名自明 |
| `status.svg.apply.failed` | 1 | `SvgParser.Parse` + `CanvasPanel.Refresh` | 【已读】同上 |
| `var.import.failed` / `gen.import.failed` / `comm.slave.var.import.failed` | 3 | `CsvVariableImporter.Parse` / `IO.StreamReader` | 【已读】同上 |
| `font.export.fail` | 1 | `FontSubsetAnalyzer.ExportCharsetFile` | 【已读】同上 |
| `ai.gen.failed` | 8（部分） | AI 服务族：`ImageGenerator.GenerateAsync` / `SvgRasterizer.RenderToBitmap` / `AiOrchestrator.ClassifyIntent` | 【已读】同上（同一键里**混着** IO 那几处，见 C 类） |
| `asset.import.fail` | 3（部分） | `SvgParser.Parse` / `Imaging.Bitmap` | 【已读】同上 |

### B 类 · 原因是**外部工具的输出** ⇒ **不该本地化**（原文有用，登记"不迁"）

| 键 | 站点 | 理由 |
|---|---|---|
| `compile.failed` | 2 | 实参来自构建流水线的**编译器 / Make 输出** |
| `buildrun.log.error` | 1 | 实参是**构建日志正文**（`SimulatorBuildService.BuildAsync` + `Task.Delay` 轮询） |
| `download.sharedlibrary.failed` | 1 | 下载/落盘外部产物 |

⇒ 这三条**登记为"不可本地化"**，理由可核（产出方是外部进程）；把它们的原文塞进字典反而是**信息损失**。

### C 类 · BCL / IO 原文 ⇒ **不该本地化，但该"带上对象"**（可立刻获得真实收益）

| 键 | 站点 | 现成产出方 |
|---|---|---|
| `status.save.failed` | 1 | 保存一脉（`MainWindow` 的 `Validate*` / `AutoFixIntegrityIssues` / `HmiPackageService.Pack`） |
| `status.reload.failed` | 1 | 【待读】 |
| `gen.export.failed` | 2 | `File.WriteAllTextAsync` / `IO.StreamWriter` |
| `audit.export.c.code.failed` | 2 | `Path.Combine` / `File.WriteAllTextAsync` |
| `comm.slave.var.export.failed` | 1 | `File.WriteAllTextAsync`（picker 之后） |
| `asset.import.fail` | 3（部分） | `IO.FileInfo` / `File.ReadAllTextAsync` |

**这一类是本阶段指出的最大收益面**：原因（IO 原文）本来就没法翻译，
但用户真正缺的是**"是哪个文件"** —— 而**阶段 82 刚建好的那条出口就是这个形状**：

```csharp
UiOperationGuard.FailureDetail(string operationKey, string? targetPath, Exception ex)
```

⇒ C 类不需要**任何新码表**，只要把那几处换成 3 实参出口；字典模板 `ui.op.failed.detail.file`
（`{0}失败: {1} —— {2}` / `{0} failed: {1} — {2}`）**已经存在**。

### 【待读】· 这轮**没有**读产出方，不许凭启发式定类

`recipe.clone.failed` · `login.failed` · `sim.reg.read.failed` · `sim.reg.write.failed` ·
`comm.test.failed` · `user.mgr.create.failed`（唯一的具体类型捕获）· `status.reload.failed`

## 83.5 本阶段**落地**了什么（零行为改动，两处"钉死"）

| 位置 | 改了什么 |
|---|---|
| `src/HmiEditor.App/Services/ExternalText.cs`（`FailureReason` 的文档注释） | 新增一段：**别把「改成调用本方法」当成「本地化」** + 上面那条静态证明 + 指向本文档 |
| `tests/HmiEditor.App.UiTests/ExceptionMessagePassthroughFaceTests.cs` | ① 类注释：原句「红了就该走 `FailureReason`」**已被实测推翻**，改为"面变小**不等于**迁移成功"；② **断言失败消息**里同一句错误指引改掉 —— 它会把下一个人**直接引向假闭环** |

**为什么不加新判据**：本项目纪律是「**做不出负控的判据不发布**」。
现有的 J3（双向相等判定）就是正确的绊线 —— 它守的是"**面不许静默变化**"，
而"面变小是不是真迁移"这件事**判据判不了**（要判它就得知道产出方有没有加码，
那是语义，不是形状）。⇒ 本阶段把它**从"待迁移清单"重新定性成"定量清单"**，
并把"变小不算成功"写进注释与失败消息，而**不是**造一条判不动的断言冒充守卫。

## 83.6 优先级（给下一阶段）

> 🩸 **本节第 1 条已于 2026-09-23（阶段 84）被实测推翻，原文保留以便对账。**
> 「**C 类 → 复用 `FailureDetail(操作名, 目标路径?, 异常)`**」这条**改期**：C 类的产出方是
> BCL / IO，而 **BCL 的 IO 原文自带目标路径** ⇒ 包一层是**冗余**（`SaveToPath` 那处还会重复
> 出现 `.tmp` 兄弟路径 ⇒ **负收益**）。正确的顺序是 **#61e（给 BCL 异常类型建码）先、#61b 后**。
> 见 **§84**。下面第 2 / 3 条不受影响。

1. ~~**C 类（7～9 站）→ 复用 `FailureDetail(操作名, 目标路径?, 异常)`**：零新码表、判据形状可复用阶段 82
   （成对断言：路径必须出现在文案里）。⚠ **前置必做**：逐处核「文件路径是否在作用域内、怎么取字符串」
   —— 已看到 `RecipeEditorView.axaml.cs:926` 的 `file` 是 picker 结果（`IStorageFile`，`if (file == null) return;` 在 `try` 之前），
   但**它到 `string` 的取法这轮没实测**（`IStorageFile.Path` 是 `Uri`）⇒ 属【待读】。~~（阶段 84 已做，
   结论是**改期**；上面那段「前置必做」的实测结果见 §84.2 表 4 —— 取法是 `file.Path.LocalPath`，
   真正的问题不是取法而是**值本来就到不了用户手上**。）
2. **A 类（14 站）→ 产出方码化**（一次一族，`GeneratedCodeValidator` 与 `SvgParser`/`CsvVariableImporter`
   是三个最干净的入口，因为它们本来就是**本仓库的结构化失败**，不是异常）。
   `ai.gen.failed` 虽然是最大族（8 站），但它**混着 IO 与 AI 服务**两类 ⇒ 要**先拆再动**。
3. **B 类 → 明确不迁**，把这 4 站的理由写进登记（可核：产出方是外部进程）。

## 83.7 门禁

| 项 | 读数 |
|---|---|
| 提交前 | `.workbuddy/tmp/s83-gate1.log`，mtime **10:19:55** ⇒ `GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误；`RLIMIT_AS 硬限: unlimited` |
| 计数 | 三段之和 = 全量 **3137** 通过 / 0 失败（`Core 2119` / `App.UiTests 439`）—— **与阶段 82 逐项相同**：本期只改注释，测试数**本就不该变** |
| 提交 | **`f286118`**（2 files，**+28 / −9**，`2026-09-23T10:21:49+08:00`）—— 两处注释级钉死（`ExternalText.cs` + 站点面守卫） |
| 提交后 | `.workbuddy/tmp/s83t-gate2.log`，mtime **10:23:08** ⇒ **晚于提交 1 分 19 秒** ✓（`GATE_EXIT=0` / 7 步全绿） |
| 对拍（gate1 vs gate2） | 读数**各 67 行**、**去行号口径 0 差异** —— 本期只改注释 ⇒ 读数**本就不该变** |
| 文档批提交 | **`05eb11a`**（4 files，**+274 / −2**，`2026-09-23T10:23:25+08:00`）—— 本专题 + 登记表 #61a-d + `project-status` 阶段 83 + 六跑表加注 |
| 第三次门禁 | `.workbuddy/tmp/s83t-gate3.log`，mtime **10:24:41** ⇒ **晚于文档批提交 1 分 16 秒** ✓（`GATE_EXIT=0` / 7 步全绿） |

⚠ **命名冲突（已如实标注）**：`s83-gate1.log` 这个路径**原先**是「阶段 82 补记」那条工作流的
提交前门禁日志（09:1x，**挂死在 App.UiTests 段**，见 `docs/2026-09-22-open-failure-file-path.md` 的六跑表）。
本阶段的门禁脚本以 `: > "$L"` **先截断**该文件 ⇒ **原内容不可恢复**；那份挂死现场**另存于**
`s83-gate1-hang.log`（09:29，未受影响）。⇒ 从本阶段起，本工作流的门禁日志改用 **`s83t-gate*`**
（t = triage）以免再撞名；引用旧名时请以上面的 mtime 为准。

## 83.8 未闭环

1. **#61 仍开着**，但已**重新定性**（从"一条可迁清单"→ 一个**三分类项目**）；
   登记表 §4 的 #61 行已按本文档改写，并新增 **#61a / #61b / #61c** 三个子项对应 A / C / B 类。
2. **A 类与 C 类的落地都没做**（本阶段刻意）。C 类的**路径取法**是它的第一个实测项。
3. **【待读】那 7 个键**的产出方没读 ⇒ 它们不在任何一类里，**不许**被当成"已分类"。
4. 表 3 是**启发式**（最近的 `try` + `X.Y(` 调用），`login.failed` 只读到 `Task.Run` ⇒
   这类读数**不能**用来定类；下阶段若用它排期，必须**先读代码复核**。

---

# 阶段 84 · #61b 的实测与改期（C 类「该带上对象」被推翻）

> 阶段编号接在 83 后面（本文档是专题，不按日期分节）。探针
> `tools/probe-bcl-io-messages.cs`（`dotnet run tools/probe-bcl-io-messages.cs`），
> 读数 `.workbuddy/tmp/s84-io-msg-final.txt`；临时 Bitmap 探针的形状见 §84.2 表 6 脚注。

## 84.1 一句话结论

**C 类「用 `FailureDetail(操作名, 目标路径?, 异常)` 把目标文件带给用户」这个动作，
在 C 类上不成立 —— 不是"收益小"，是可达范围内收益恒为 0。**
理由是实测出来的两条：① **BCL 的 IO 原文自带目标路径**（用户早就在原文里看到了）；
② 码化出口 `FailureReason` 对 BCL 异常**没有码** ⇒ 换过去是**改名**（阶段 83 已钉死的那种
假闭环）。⇒ **C 类改期**，且**依赖 #61e（给 BCL 异常类型建码）**；顺序反了就没有收益。

## 84.2 量的那一遍

### 表 4 · 逐站实测「目标路径在 catch 里拿不拿得到」（11 站，读现场代码）

| # | 站点 | 键 | catch 里能不能拿到路径 | 档 |
|---|---|---|---|---|
| 1 | `MainWindow.axaml.cs:1265` | `status.save.failed` | ✅ `path` 是 `SaveToPath` 的**参数** | ① 现成 |
| 2 | `MainWindow.axaml.cs:549` | `status.reload.failed` | ❌ **根本没有文件** —— 这是 Dock **布局重组**失败，try 里只有一次字典查表 + 两次 `Dispatcher.Post` | **分类被证伪** |
| 3 | `BuildPipelineService.cs:114` | `gen.export.failed` | ⚠ `targetDir` 在作用域（`:78`），但 **`outputPath` 声明在 try 内**（`:98`）⇒ 只能给**目录** | ①（降级为目录） |
| 4 | `BuildPipelineService.cs:115` | `audit.export.c.code.failed` | 同上 | ①（同） |
| 5 | `BuildPipelineService.cs:206` | `audit.export.c.code.failed` | ✅ `targetDir`（`:138` 声明在 **try 之外**） | ① 现成 |
| 6 | `RecipeEditorView.axaml.cs:926` | `gen.export.failed` | ✅ `file` 声明在 try **之前**（`:897`），`if (file == null) return;` 也在之前 ⇒ catch 里非空 | ① 现成 |
| 7 | `RecipeEditorView.axaml.cs:891` | `gen.import.failed` | ✅ `files` 在 try 之前（`:859`）⇒ 可重取 `files[0]` | ② 可重取 |
| 8 | `CommunicationConfigView.axaml.cs:1060` | `comm.slave.var.export.failed` | ⚠ `file` 声明在 **try 内**（`:1045`）⇒ 要拿必须多一个 `string? targetPath = null;` 在 try 外承接 | ③ 要改一处结构 |
| 9 | `AssetManagerView.axaml.cs:221` | `asset.import.fail` | ❌ `path` 在 **foreach 内**（`:186`）、catch 在圈外 ⇒ **批量档天然没有"单一路径"** | ③ 结构上取不到 |
| 10 | `AssetManagerView.axaml.cs:286` | `asset.import.fail` | ✅ `files` 在 try 之前（`:238`）⇒ 可重取 | ② 可重取 |
| 11 | `AssetManagerView.axaml.cs:357` | `asset.import.fail` | ✅ `files` 在 try 之前（`:317`）⇒ 可重取 | ② 可重取 |

**阶段 83 留的「第一个实测项」至此有答案了**：取法**不是**问题 —— `IStorageFile → string`
的仓库既定写法是 **`file.Path.LocalPath`**（`AssetManagerView:186/259/338`、`FontSubsetView:84`、
`VariableImportView:58`、`CommunicationConfigView:1001/1055`、`MainWindow:1161/1204/1587`，
另有 `TryGetLocalPath()` 在 `DeviceManagerView:465/477`、`DownloadToHmiView:163`）。
真正的问题是**值本来就到不了用户手上**（§84.3）。

⚠ 另外两条**分类修正**（实测，不是推断）：
- `status.reload.failed`（#2）**不是 IO** ⇒ 它不该留在 C 类（阶段 83 表里它本就标着【待读】）。
- `gen.import.failed`（#7）被阶段 83 归进 **A 类**，但它的产出方实测含 `IO.StreamReader`
  ⇒ **一个 catch 同时包着 IO 与模型调用**（`RecipeManager.ImportCsv`）⇒ **这族的 A/C 分档
  在"站点"这一层做不到**，只能在"产出方"那一层分。

### 表 5 · BCL / IO 异常原文实测（探针 6 场景）

| 场景 | 复刻的站点 | 异常 | `Message` | **自带目标路径?** |
|---|---|---|---|---|
| A `File.Create` 撞已存在的**目录**名 | `SaveToPath`（`MainWindow:1225`） | `UnauthorizedAccessException` | `Access to the path '/tmp/probe84/demo.hmi.tmp' is denied.` | **True** |
| B `File.WriteAllTextAsync` 目标目录不存在 | `BuildPipelineService:99/106`、`CommConfigView:1055` | `DirectoryNotFoundException` | `Could not find a part of the path '/tmp/probe84/nope/out.csv'.` | **True** |
| C `File.WriteAllText` 目标是目录 | 资产落盘一脉 | `UnauthorizedAccessException` | `Access to the path '…' is denied.` | **True** |
| D `File.ReadAllTextAsync` 目标是目录 | `AssetManagerView:260`、`VariableImportView:58` | `UnauthorizedAccessException` | 同上 | **True** |
| E `new StreamWriter(...)` 拿到的是目录 | `RecipeEditorView:919` | `UnauthorizedAccessException` | 同上 | **True** |
| F `Path.Combine` 含 `\0` 的路径 | `BuildPipelineService:98/105` | **不抛** | — | — |

**外加一行 culture 读数**：把 `CurrentUICulture` 切 `en-US` / `zh-CN`，场景 A 的消息
**一字不变**（都是英文）⇒ 这是登记 **#59** 的原形状：BCL 原文在中文界面下就是**中英混排**，
而且**没法翻译**（阶段 83「不该本地化」的定性成立）；但「**用户缺的是"是哪个文件"**」
**不成立** —— 路径本来就在原文里。

### 表 6 · 唯一非 `File.*` 家族的产出方（**headless 里不可驱动**）

`asset.import.fail` 的图片批量档（#9）产出方含 `new Avalonia.Media.Imaging.Bitmap(path)`。
阶段 84 在一个**临时**测试里量过它（跑完即删）：

| 输入 | 结果 |
|---|---|
| 文件不存在 / 内容是文本 / 空文件 / 截断 PNG / **目标是目录** | 全部**不抛**，`PixelSize` 恒 `(1,1)` |
| **真实的 4×4 PNG**（python 现造，74 B） | **也不抛**，`PixelSize` **仍 `(1,1)`** |

⇒ 真实 PNG 也是 `(1,1)` ⇒ **headless 的 `Bitmap` 解码器是桩**（不是"宽容"）⇒
**这档在 headless 里不可实测**（与阶段 82 的 `IStorageProvider` 同款处境：
不是测试基建欠缺，是渲染后端不实现）。真机上 Skia 会真解码、坏图会抛，
其消息**是否带路径未实测** —— 但**这一档结构上取不到对象**（#9），
**即使它不带路径，收益也拿不到**。

## 84.3 为什么是"收益恒为 0"（三条，各自可核）

1. **冗余**：原文已含路径 ⇒ 3 实参出口渲染出的 `{1}`（我们传的路径）与 `{2}`（原文）里
   会是**同一条路径**。最坏的是 `SaveToPath`：`{1}` = `…/demo.hmi`，而 `{2}` 里的是
   `…/demo.hmi.tmp` ⇒ 用户看到**两个不同的路径**，其中一个是**中间文件**。
   ⇒ 这比不带路径**更难读**。
2. **码化恒等于改名**：`ExternalText.FailureReason` 只在 `ex is HmiPackageException` 上分叉
   （阶段 83 已证），BCL 异常没有码 ⇒ 换过去**只落回落分支**，用户所见一字不变 ——
   正是阶段 83 钉死的「**收口 ≠ 改名**」。
3. **唯一可能受益的档拿不到对象**：见 §84.2 表 6。

> ⚠ **后记（阶段 85，登记 #61e 落地后）**：上面第 **1**、**2** 条**已经失效** ——
> 原因那半现在是**码化短句、不带路径**（7 个码全是 0 实参）⇒ 不再与原路径冗余；
> BCL 异常类型**已有码** ⇒ 换过去不再恒等于改名。第 **3** 条（表 6 那档 headless 不可实测）
> 与 #61e 无关，**仍然开着**。⇒ §84.4 那条「#61e 先、#61b 后」的依赖**已满足**，
> `#61b` 今天可以做了。落地面与实测读数见 §85。

## 84.4 结论与正确顺序

- **C 类不改代码，改期**（不是取消 —— 方向本身没错，是**顺序**错了）。
- **真方向 = 登记 #61e：给 BCL 异常类型建码**（`UnauthorizedAccessException` →
  「没有权限访问该文件」；`DirectoryNotFoundException` → 「路径中的目录不存在」），
  在 `ExternalText.FailureReason` 上开一档（或另建 `SystemFailureCodes`）。
  ⇒ **阶段 85 已按这条落地**（见 §85），且实测把码表从"猜两类"扩成**探针实测的 7 类**。
- **#61e 之后，C 类才有真实收益**：那时原因那一半是**本地化短句、不带路径**，
  「把路径作为实参带回来」才第一次让用户**同时**看到「哪个文件」+「为什么（他的语言）」。
  ⇒ **依赖关系：#61e 先、#61b 后**（前半已完成）。
- ⚠ **#61e 自己的判据方向**（留给下一阶段）：成对断言 —— ① zh 下渲染出中文、en 下渲染出英文，
  且**都不含 BCL 英文原文**；② **原文不许丢**（仍要落盘日志，否则排障信息损失）。
  单看①会在"根本没渲染出东西"时也成立（阶段 82 的成对断言教训）。
  ⇒ **阶段 85 就是照这两条建的判据**（`SystemIssueLocalizationGuardTests` +
  `SystemFailureRawMessageIsStillLoggedTests`），这里的方向被**原样执行、未改口径**。

## 84.5 本阶段**落地**了什么（零行为改动）

1. **`UiOperationGuard.FailureDetail(操作名, 目标路径, 异常)` 的注释新增一段**
   「但它**不是**"凡失败都该改用它"的万能出口」：写明 C 类的实测结论、为什么是负收益、
   以及**顺序（#61e 先）**。钉在这里是因为**下一个人看到这个出口的第一反应就是
   "C 类那几处也该用它"**。
2. **J3 站点面守卫（`ExceptionMessagePassthroughFaceTests`）的类注释新增三档处置段**：
   原文只写「其余…属另一个议题，已登记为 #61」—— 读者会以为 C 类**等着人去迁**。
   现在写明 A 类可码化 / **C 类改期** / B 类不迁，并指向本文档。
3. **探针进仓库**：`tools/probe-bcl-io-messages.cs`（`dotnet run` 即可复跑，
   符合仓库既有的 `tools/probe-*` 约定，例如被 `LuaDialectTests` 引用的
   `tools/probe-lua-dialect.lua`）。

**为什么不加新判据**（与阶段 83 同理）：唯一候选是「`FailureReason` 不许给 BCL 类型分叉」，
它看起来能守住 C 类的定性 —— 但它**会拦住 #61e 这个正确的改动** ⇒ 属
「**判据别量自己不该管的量**」（阶段 55 的教训）。而「变小的面算不算真迁移」判据判不了
（要判它得知道产出方有没有加码，那是语义不是形状）—— 这两句在阶段 83 已经立过，
本阶段实测只是**再次确认它们成立**。J3 面的双向相等判定继续守着「面不许静默变化」。

## 84.6 门禁

| 项 | 读数 |
|---|---|
| 提交前（第一次） | `.workbuddy/tmp/s84t-gate1.log`，mtime **10:54:25** ⇒ `GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| ⚠ **第一次作废、重跑** | 跑完后发现 `UiOperationGuard.cs` 注释里的计数写成「约 10 处」，而实测是 **9 站** ⇒ 就地把计数改准（**纯注释、零行为**）⇒ **重跑提交前门禁**。两次日志**都留存、不互相覆盖**（阶段 83 的撞名教训：`s83-gate1.log` 被 `: >` 静默截断）。 |
| 提交前（**有效的那次**） | `.workbuddy/tmp/s84t-gate1b.log`，mtime **10:55:56** ⇒ **晚于末次源码改动（`UiOperationGuard.cs` 10:54:39）共 1 分 17 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| 计数 | 三段之和 = 全量 **3137** 通过 / 0 失败（`Core 2119` / `App.UiTests 439`）—— **与阶段 82 / 83 逐项相同**：本期只改注释 + 加一个**未进任何 csproj** 的探针，测试数**本就不该变** |
| 对拍（gate1 vs gate1b） | **读数部分 0 差异** —— 唯一差异来自 gate1b 脚本多出的「末次源码改动」表头块，以及登记表在两次之间被编辑（文档不进编译/测试读数）。与"只改注释"一致 |
| 代码批提交 | **`b3e03a2`**（3 files，**+146 / −0**，`2026-09-23T10:56:12+08:00`）—— 两处注释钉死 + 探针进仓库 |
| 提交后 | `.workbuddy/tmp/s84t-gate2.log`，mtime **10:57:45** ⇒ **晚于提交 1 分 33 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| 对拍（gate1b vs gate2） | 唯一差异 = **路径卫生「已检查 640 → 641 个文件」** —— 这正是探针从**未跟踪**变**已跟踪**的**预期结果**（可解释，不是读数异常）；测试数 / 步骤结果逐项相同 |
| 文档批提交 | **`47fe2e8`**（3 files，**+263 / −3**，`2026-09-23T10:58:28+08:00`）—— 本专题 §84 + 登记表（#61b 改期 / 新增 #61e）+ `project-status` 阶段 84 |
| 第三次门禁 | `.workbuddy/tmp/s84t-gate3.log`，mtime **10:59:49** ⇒ **晚于文档批提交 1 分 21 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误；**工作树干净** |
| 对拍（gate2 vs gate3） | **读数 0 差异** —— 唯一差异是 gate2 运行期的工作树里有两条未提交的 `M docs/…`（gate3 时已提交、工作树干净） |

⚠ **一次自纠（登记在案）**：第一次提交时带了 `--no-verify`。事后核过 —— 本仓库
`.git/hooks/` 下**只有 `.sample`**、`core.hooksPath` 未设置 ⇒ **没有任何钩子**，
该旗标是**空操作**（不是"跳过了检查"）。但既然无钩子可跳，后续提交一律**不带**它。

## 84.7 未闭环

1. **#61b 改期**（依赖 #61e）—— 登记表已按此改写；新增 **#61e**。
2. **`status.reload.failed` 的分类被证伪** ⇒ 它应从 C 类移出，但**该归哪一档还没定**
   （它连"文件"都没有，可能根本不需要本地化改造）⇒ 留在 #61d【待读】。
3. **`gen.import.failed` / `comm.slave.var.import.failed` 的 A/C 混档**：一个 catch 同时包
   IO 与模型调用 ⇒ 「按站点分档」在这族做不到。要处置必须**先按产出方拆 catch**（属重构）。
4. **表 6 那档（`Bitmap` 解码）在 headless 里不可实测** ⇒ 真机行为**未验证**，
   不许当成"已确认不带路径"。

---

# 阶段 85 · #61e 落地（给 BCL 异常类型建码）

> 阶段 85（2026-09-23）。与 §83 / §84 的"零行为改动"**不同**：本阶段**有行为改动** ——
> 中英文界面下，BCL 抛出的失败原因那一半**从英文原文变成字典短句**。
> 探针：`tools/probe-bcl-exception-types.cs`（`dotnet run` 可复跑，不落 `obj/`）；
> 读数原文：`.workbuddy/tmp/s85-bcl-types.txt`。

## 85.1 一句话结论

`ExternalText.FailureReason` 从**一支分叉**（只认 `HmiPackageException`）改成**三支**：
包码异常 → 字典；**BCL / 系统类型码**（新增 7 个码）→ 字典；其余回落 `ex.Message`。
**原文没有丢** —— 界面改渲染码化短句，异常对象照旧落 `AppLog`（判据②钉住）。
⇒ §84.4 那条依赖（#61e 先、#61b 后）**前半已完成**。

## 85.2 量的那一遍（探针实测，不是猜）

### 表 7 · 本仓库 API 面实际抛哪些类型（20 场景）

| 异常类型 | 次数 | 消息自带目标路径 |
|---|---|---|
| `UnauthorizedAccessException` | 7 | **7/7 带** |
| `FileNotFoundException` | 6 | **6/6 带** |
| `DirectoryNotFoundException` | 5 | **5/5 带** |
| `IOException`（撞名 / 非空目录 / ENOSPC） | 3 | **3/3 带** |
| `ArgumentException`（路径含 `\0` / 空串） | 2 | 0/2 |
| `InvalidDataException`（`ZipArchive` 撞非 ZIP） | 1 | 0/1 |
| `PathTooLongException` | 1 | 1/1 |
| `JsonReaderException`（**派生自 `JsonException`**） | 1 | 0/1 |
| （**不抛**：`File.Move` 目标已存在 / `File.Delete` 不存在） | 2 | — |

### 表 8 · culture 切中英文：消息**一字不变**

```
[en-US] Could not find file '/tmp/probe85/ghost.txt'.
[zh-CN] Could not find file '/tmp/probe85/ghost.txt'.
```
（`Could not find a part of the path` / `Directory not empty :` / JSON 那句同样一字不变。）
⇒ 这就是 #59 的原形状：**BCL 原文不可翻译**，不码化就只能永远中英混排。

### 从读数里**直接**读出来的三条设计约束

1. `JsonReaderException` 派生自 `JsonException` ⇒ 码表里挂**基类**（挂派生类覆盖不到基类）。
2. `IOException` 是 4 个已实测类型的基类（`FileNotFound` / `DirectoryNotFound` /
   `PathTooLong` / `EndOfStream`）⇒ 它在码表里**必须排最后**，否则**静默**吃掉后代
   （没有编译错误、每个类型也都"取得到码"，只是**取到的是错的**）。
3. **路径分布**决定了 #61e 与 #61b 的分工：带路径的 5 类码化后**丢掉了目标文件**
   ⇒ 必须由 #61b 用三实参重载补回来；不带的 3 类本来就没有。

## 85.3 码表（`src/HmiEditor.App/Services/SystemFailureCodes.cs`）

7 码 / 7 条映射，**全部来自表 7 的实测**：

| 顺序 | 码 | 类型 |
|---|---|---|
| 1 | `UNAUTHORIZED_ACCESS` | `UnauthorizedAccessException` |
| 2 | `DIRECTORY_NOT_FOUND` | `DirectoryNotFoundException` |
| 3 | `FILE_NOT_FOUND` | `FileNotFoundException` |
| 4 | `PATH_TOO_LONG` | `PathTooLongException` |
| 5 | `INVALID_DATA` | `InvalidDataException` |
| 6 | `INVALID_JSON` | `JsonException` |
| **7** | `IO_ERROR` | `IOException`（**基类，必须最后**） |

**刻意不码化的两类**（写进类注释，防下一个人"顺手补上"）：

- **`ArgumentException`**：路径含 `\0` / 空串是**调用方的编程错误**，不是环境失败；
  而且它是很多类型的基类，收进来会**连带吞掉编程错误** ⇒ 让它在界面上刺眼反而是对的。
- **没实测到的类型**：不许凭"看着像 IO"进表 —— 那是把守卫的输入面换成**猜**。

## 85.4 判据

登记为 #61e 写下的判据方向是**成对**的（§84.4 末条），本阶段**原样执行、未改口径**：

| 判据 | 位置 | 形态 |
|---|---|---|
| ① **成对** | `SystemIssueLocalizationGuardTests` | 9 个**真实撞 BCL** 的场景 × `SwitchLanguage` 各渲染一遍 |
| ② **原文不许丢** | `SystemFailureRawMessageIsStillLoggedTests` | 换 `AppLog.Current` ⇒ 断言异常对象仍含原文 |

- **① 为什么必须成对**：单看"渲染出中文"在"字典缺键 ⇒ 回落"与"语言切换没生效 ⇒ 两次同一份"
  两种情形下也成立（阶段 82 的成对断言教训）。
- **② 为什么必须单独存在**：它是 #61e 的**唯一代价** —— 码化把原因换成短句，界面上的原文就没了；
  而判据①**看不见这件事**（把原文彻底丢掉，①照样全绿）。两侧都断言：界面**不含**原文 +
  日志**有**原文（只断一侧各有一种假通过）。

10 条判据的形状（8 + 2）：探针每场景真的抛 / 每类型取得到码且键两本字典都在 / **成对** /
两侧不含 `.NET` 原文记号 / **接线**（毒化消息不许冒头）/ 码表**有序性** / **双向闭包** /
**诚实边界**（`InvalidOperationException` 仍回落）/ **原文不丢** / **负控 7 组**。

**负控里最要紧的一组（NC4）**：把 BCL 原文当文案 ⇒ **成对判据看不见它** ——
`en` 侧本来就该是英文，脚本检查对它无话可说 ⇒ 这正是"泄漏判据不可被成对替代"的证据。
NC4 同时钉住泄漏判据的**两支**（整条透传 / 包壳带记号），并附一条反例：
"两侧都是原文"那种字面形状成对**只**靠"两侧相同"那一支才抓得到 ⇒ **它抓到原文是巧合**，
不是因为它在盯着原文。

## 85.5 字典与可达性

- 两本字典各 **+7 键**（`system.issue.<码小写>`），**全部 0 占位符** —— 与
  `FailureReason` 传 0 实参一致（多一个占位符会在界面上渲染出字面的 `{0}`）。
- **不加 `Lang` 常量** ⇒ `UnusedBaseline` **1123 不动**、`sync_localization.py` 的 `VALUE_MAP`
  不动、`KeptUnreachable` 不动。
- ⚠ 但**可达性必须新增一条机制 M10**：新键在源码里**没有整键字面量**
  （键是 `SystemIssueKeyPrefix + code.ToLowerInvariant()` 拼出来的）
  ⇒ 原九条机制**原理上都看不见它们**。
  **负控（NC-M10）实测**：摘掉 M10 ⇒ 那 7 个键被**逐条点名**判成不可达，
  且面下界同时报「实测 0 < 下界 7」。⇒ M10 **确实在承重**，不是装饰。

十面读数（`HMI_READINGS_OUT=.workbuddy/tmp/s85-readings-m10.txt`，绿）：

```
M1-Lang 常量 651 / M2-axaml res:Loc 1113 / M3-插件族派生 26 / M4-cfg.note 30 /
M5-脚本诊断码 19 / M6-源码字面量 385 / M7-枚举名派生 12 / M8-AI 诊断码 41 /
M9-包诊断码 7 / M10-系统诊断码 7
```

## 85.6 门禁

| 项 | 读数 |
|---|---|
| 提交前（gate1） | `.workbuddy/tmp/s85t-gate1.log`，mtime **11:37:28**；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误；路径卫生 641 |
| 代码批提交 | **`73e55b9`**（9 files，**+1106 / −50**，`2026-09-23T11:37:54+08:00`） |
| 提交后（gate2） | `.workbuddy/tmp/s85t-gate2.log`，mtime **11:39:10** ⇒ **晚于提交 1 分 16 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| 对拍 gate1 vs gate2 | **唯一差异 = 路径卫生「已检查 641 → 643 个文件」** —— 码表 + 新探针由未跟踪变已跟踪（+2，可解释） |
| 文档批提交 | **`d43dc56`**（5 files，**+394 / −3**，`2026-09-23T11:40:32+08:00`）—— 本专题 §85 + 后记 + 阅读提示 |
| 第三次门禁（gate3） | `.workbuddy/tmp/s85t-gate3.log`，mtime **11:41:51** ⇒ **晚于文档批提交 1 分 19 秒** ✓；7 步全绿 / 0 警告；**工作树干净** |
| 对拍 gate2 vs gate3 | **读数 0 差异**（路径卫生同为 643；八程序集测试数逐项相同） |
| 计数 | 八程序集之和 **3137** 通过 / 0 失败；`App.UiTests` 439 → **449**（+10 = 新增 8 + 2 条） |
| `UnusedBaseline` | **1123 不动**（"加键不加 `Lang` 常量"那条路的代价读数） |

完整表与其余数字见 `project-status.md` 阶段 85 §85.6（那里是最终口径）。

## 85.7 未闭环

1. **#61b 现在可以做**（前置已满足）—— 仍要逐站选**哪个路径**（`.tmp` 那档仍在）。
2. `ArgumentException` 那两个场景（含 `\0` / 空串）**故意不码化** ⇒ 界面仍是英文原文。
   要处置得先认定"这是编程错误、不该给终端用户看" —— 属**另一件事**（未登记）。
3. 表 7 里"不抛"的 2 个场景在 Linux 上不抛 —— 换平台（Windows）行为可能不同，**未验证**。
4. §84.7 的第 2 / 3 / 4 条**原样留着**（与 #61e 无关）。

---

# 阶段 86 · #61b 落地（C 类「带上对象」）

> 阶段 86（2026-09-23）。**本阶段动行为** —— 它是 #61b 的闭环节点：
> #61e（阶段 85）解除了它的两条改期理由（§84.3 / §85.7-1），本阶段按 §84.2 表 4 逐站迁完。

## 86.1 一句话结论

9 站里 **8 站**从 `<loc>.Format(键, ex.Message)` 迁到 `UiOperationGuard.FailureDetail(操作名, 目标路径, 异常)`；
站点面 **37 → 29**（组合 27 → 22）。第 9 站（`asset.import.fail` 的**批量档**）**结构上没有"那个文件"** ⇒ 归 #61a。
⚠ 并在同一阶段补上一条**新判据**：那张面**分不出**"带上了路径"与"三实参里填了 `null`" ——
后者一样能从面上消失、一样让用户看不到文件 ⇒ 那是**第二次假闭环**（第一次是 §83.3 钉的"改名式假闭环"）。
**空子经实测存在**（见 §86.4 的负控）。

## 86.2 量的那一遍（逐站，读现场代码）

站号沿用 §84.2 表 4。`档` 是阶段 84 量出的「路径在不在 catch 作用域内」：

| # | 站点 | 原键 | 新操作名 | 路径实参 | 档 |
|---|---|---|---|---|---|
| 1 | `MainWindow.axaml.cs:1265` | `status.save.failed` | `UiOpSaveProject` | `path`（`SaveToPath` 的参数，非 `.tmp`） | ① 现成 |
| 2 | `BuildPipelineService.cs:114` | `gen.export.failed` | `UiOpExportCode` | `targetDir`（`:78`，**try 外**） | ① 降级为**目录** |
| 3 | `BuildPipelineService.cs:115` | `audit.export.c.code.failed` | `UiOpExportCode` | `targetDir` | ① 降级为**目录** |
| 4 | `BuildPipelineService.cs:208` | `audit.export.c.code.failed` | `UiOpExportCode` | `targetDir`（**须提到 try 外**，原 `:138` 在 try 内） | ① + 改结构 |
| 5 | `RecipeEditorView.axaml.cs:927` | `gen.export.failed` | **`UiOpExportRecipe`** | `file.Path.LocalPath`（`file` 在 try **之前**） | ① 现成 |
| 6 | `CommunicationConfigView.axaml.cs:1062` | `comm.slave.var.export.failed` | `UiOpExportSlaveVars` | `targetPath`（**新加 try 外承接**，`file` 在 try 内） | ③ + 改结构 |
| 7 | `AssetManagerView.axaml.cs:287` | `asset.import.fail` | **`UiOpImportSvg`** | `files[0].Path.LocalPath`（`files` 在 try 外） | ② 可重取 |
| 8 | `AssetManagerView.axaml.cs:358` | `asset.import.fail` | **`UiOpImportFont`** | `files[0].Path.LocalPath` | ② 可重取 |
| — | `AssetManagerView.axaml.cs:222` | `asset.import.fail` | — | **不动**：批量档（循环 `added++`，多文件） | 归 **#61a** |

**三件逐处判断过的事（都不能照抄）**：

1. **一个键服务两个操作 ⇒ 拆。** `gen.export.failed` 同时是「导出代码」（`BuildPipelineService`）
   与「导出**配方 CSV**」（`RecipeEditorView`）——两个视图、两个操作，共用一条文案。
   `asset.import.fail` 同理：**三个不同操作**（批量图片 / SVG / 字体）。
   ⇒ 迁的时候不是"把键换掉"，而是先**把操作名分开**（拆出 3 个新操作名 + 保留批量档原地）。
2. **传哪个路径是真正的判断点。** `CompileAsync` 里 `outputPath` / `buildPath` 都声明在 try 内
   ⇒ **只能给目录**（诚实答案）；`SaveToPath` 给 `path` 而**不是** `path + ".tmp"`
   （阶段 84 记的那件事，见 §84.2 表 4 备注）。
3. **两处目标要在 try 外承接**（#6 / #4）。这不是风格问题 —— 编译器**当场**报
   `CS0103: 当前上下文中不存在名称"targetDir"`（实测：`BuildPipelineService.cs(206,77)`）。
   ⇒ 用外层承接变量（`string? targetDir = null;` + try 内赋值），**不是**把实参写成 `null`
   （那就把"带上对象"又丢了，见 §86.4）。

## 86.3 直通面 37 → 29（落盘读数）

判据 `ExceptionMessagePassthroughFaceTests`（`.workbuddy/tmp/s86-face1.log` 红 → `.workbuddy/tmp/s86-face2.log` 绿）：

| 项 | 前 | 后 | 差 |
|---|---|---|---|
| 站点 | 37 | **29** | −8 |
| (文件, 键) 组合 | 27 | **22** | −5 |

整条消失的 5 个组合：`MainWindow·status.save.failed`(1) / `BuildPipelineService·audit.export.c.code.failed`(2) /
`BuildPipelineService·gen.export.failed`(1) / `CommunicationConfigView·comm.slave.var.export.failed`(1) /
`RecipeEditorView·gen.export.failed`(1)；保留的 1 个是 `AssetManagerView·asset.import.fail` **3 → 1**（批量档）。

**下界跟着实测走（一处、有理由）**：`Face_IsNotDegenerate` 的 `sites.Count >= 30` 被这次迁移**撞穿**
（29 < 30）⇒ 改成 **`>= 20`**，并把理由写进注释：**刻意不贴着实测值** —— 口径失效时面会掉到**个位数**，
而"面变小"这一幕已由基线**双向**判定管住（少了必须同步基线、并写清是不是真迁移）；
贴着实测值只会让以后每次**正确**的迁移多红一条（噪声不是信号）。

## 86.4 新判据：直通面**看不见**的那一半

**为什么非有不可**：那张面数的是"还有多少站点是 `Format(键, ex.Message)` 形状"。
一个站点只要**不再那样写**就**从面上消失** —— 换成 `FailureDetail(op, path, ex)` 会消失，
换成 `FailureDetail(op, **null**, ex)` **一样**消失。而后者用户看到的文案里**没有文件**，
恰是 #61b 要修的东西 ⇒ 迁移就成了"面绿着、收益为零"的**第二次假闭环**。

**判据**：`UiOperationGuardContractTests.EveryDetailCallSite_PassesAConcreteTargetPath` ——
外部调用点必须**三实参**，且第二实参**不得**是 `null` / `default` 字面量（退回两实参重载同样点名）。

- **白名单为空，且这不是遗漏**：实测 `src/` **全部 11 处**外部调用点都是三实参 + 非空表达式
  （`MainWindow` ×4 / `BuildPipelineService` ×3 / `AssetManagerView` ×2 / `CommunicationConfigView` / `RecipeEditorView`）。
  唯一「结构上拿不到路径」的调用点（`MainWindow.OpenProject` 外层 catch —— 那一刻用户还没选文件）
  走的是**两实参重载**，**不经过本判据的面** ⇒ 不需要豁免表。
- **输入面剔掉定义面**（`Services/UiOperationGuard.cs`）：那里两实参重载被三实参重载**在内部调用**
 （`targetPath is null` 的实现分支），是**正确实现**。留在输入里就只能靠一张"文件白名单"豁免它 ——
  而**判据的输入面含定义面 ⇒ 判据被那张表养着**（登记 #55 的教训）。

**负控（5 组，合成语料）**：

| 组 | 注入 | 必须 |
|---|---|---|
| ① | `FailureDetail(Lang.X, null, ex)` | 点名「没有文件」那一支 |
| ② | `FailureDetail(Lang.X, ex)`（两实参） | 点名「不带文件」那一支（**与 ① 是不同的分支**） |
| ③ | 合规三实参 ×2 | **无话可说**（两个调用点都扫到，不只第一条） |
| ④ | **同一注入**，只改"它在哪个文件里" | 定义面里**不判**、别的文件里**必判** ⇒ 同时证明"排除真的生效" |
| ⑤ | 注释里写的反例 | **不算**违规（规则不许被自己的文档满足，登记 #48） |

③ 与 ④ 缺一不可：**只测"抓得到"等于没测假阳性** —— 一条永远返回违规的判据也能过 ①②。

**生产判据的负控（实测，成对；脚本 `.workbuddy/tmp/s86-nc-inject.py`）**：
往**真实源码**把 `MainWindow.axaml.cs:1265` 的 `path` 改成 `null`：

| 判据 | 注入后 | 读 |
|---|---|---|
| 新判据 `EveryDetailCallSite_…` | **红** | 点名 `src/HmiEditor.App/MainWindow.axaml.cs`：「目标路径实参写成了 null ⇒ 面绿着…」（`.workbuddy/tmp/s86-nc-run.log`，**只它一条红**，5 通过） |
| 直通面 `ExceptionMessagePassthroughFaceTests` | **3/3 全绿** | `.workbuddy/tmp/s86-nc-face.log` ⇒ 那个空子**实测存在**，新判据不是重复劳动 |

还原走**写回原文 + `sha256` 核对**（`2a09e91ae68b6607` 前后一致；**不用 `copy2`** —— 那会把 mtime 一起复制，
MSBuild 判"没变"⇒ 不重建 ⇒ 正控跑的是负控的二进制，本项目踩过）。

## 86.5 字典处置链（删键必须连常量一起走）

| 动作 | 对象 | 理由 |
|---|---|---|
| 删 4 键 ×2 本 | `status.save.failed` / `gen.export.failed` / `audit.export.c.code.failed` / `comm.slave.var.export.failed` | 全部使用点已迁走 |
| **连删 3 个 `Lang` 常量** | `GenExportFailed` / `StatusSaveFailed` / `AuditExportCCodeFailed` | 它们的全部使用点写的都是**字面量** ⇒ 常量本来只靠 **M6 桶**担保（与 #60 的 `StatusOpenFailed` 同一个形状） |
| 加 4 键 + 4 常量 | `UiOpExportRecipe` / `UiOpExportSlaveVars` / `UiOpImportSvg` / `UiOpImportFont` | **都有代码引用**（迁后的站点在读它们） |
| **不删** | `asset.import.fail` / `AssetImportFail` | 批量档（`:222`）还在用 |

读数：

| 项 | 前 | 后 |
|---|---|---|
| 两本字典键数 | 2189 | **2189（净 0）** |
| `Lang` 常量总数 | 1774 | **1775** |
| `UnusedBaseline` | 1123 | **1120** |

`UnusedConstants_ClassifiedByKeyMechanism` 的**落盘实测**（`HMI_READINGS_OUT=.workbuddy/tmp/s86-buckets.txt`）：

```
无代码引用的常量 1120 / 字典键 2189 / 有常量的键 1775 / 没有常量的键 414
M2 828  ·  M6 269（272 → 269）  ·  M3 18  ·  无任何机制 5
```

⇒ **M6 是唯一动的桶**（−3），与"删的 3 个常量都在 M6 桶里"逐项对得上 —— 这个数**是量出来的，不是推出来的**。

⚠ 两本字典的手术按**逐行**做，不做 `json.dump(sorted(...))` 重写（那会产生上千行 diff）：
`git diff --numstat` = 各文件 **4 / 4** 行，净 0。备份见 `.workbuddy/tmp/s86-dict-Strings.*.json`。

其余守卫**零漂移**（`LangConstantUsage` / `LocalizationKeyCoverage` / 可达性 11/11 绿）：
删的键与常量**同生共死** ⇒ 可达性面不动。

## 86.6 门禁

| 项 | 读数 |
|---|---|
| 提交前（gate1） | `.workbuddy/tmp/s86-gate1.log`，mtime **13:42:38**；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误；路径卫生 643 |
| 代码批提交 | **`9b4618f`**（13 files，+239 / −55，`2026-09-23T13:45:42+08:00`）|
| 提交后（gate2） | `.workbuddy/tmp/s86-gate2.log`，mtime **13:46:55** ⇒ **晚于提交 1 分 13 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| 对拍 gate1 vs gate2 | **读数 0 差异**（29 行读数逐项相同） |
| 文档批提交 | **`b9002ec`**（3 files，+280 / −3，`2026-09-23T13:48:10+08:00`）|
| 第三次门禁（gate3） | `.workbuddy/tmp/s86-gate3.log`，mtime **13:49:32** ⇒ **晚于文档批提交 1 分 22 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误；**工作树干净** |
| 对拍 gate2 vs gate3 | **读数 0 差异**（29 行读数逐项相同）|
| 计数 | 八程序集之和 **3139** 通过 / 0 失败；`App.UiTests` 449 → **451**（+2 = 本阶段两条新判据） |
| `UnusedBaseline` | 1123 → **1120**（见 §86.5） |

## 86.7 未闭环

1. **`asset.import.fail` 的批量档**（`AssetManagerView.axaml.cs:222`）**故意不动** ⇒ 归 **#61a**
   （产出方是 `SvgParser` / 图片解码，属"本仓库的结构化失败"，该走**码化**而不是带路径）。
   顺带：`Lang.AssetImportFail` 常量存在但**没人读**（仍在 `UnusedBaseline` 的 M6 桶里）—— 做 #61a 时一起处置。
2. **#61a（约 14 站）与 #61d（7 键）原样开着**；#61c 4 站登记为"不迁"（理由可核）。
3. **新判据的诚实边界**：它只断「第二实参不是 `null` 字面量」，**断不了"给的是不是那个对的路径"**
   （例如把 `.tmp` 当目标文件报）—— 那是逐处判断，源码面够不着。可驱动的那几站若要正面钉，
   得靠端到端（`OpenFailureFilePathUiTests` 那种形状），而本阶段迁的 8 站里**多数在 headless 里不可驱动**
   （文件选择器 / 批量导入）。
4. §85.7 的第 1 条**本阶段已完成**；第 2 / 3 条**原样留着**。



# 阶段 87 · #61a 第一刀（**量的那一遍把登记方向推翻了一半**：加码表 + 带码异常 → 返回值带码）

> 阶段 87（2026-09-23）。**本阶段动行为** —— 它是 #61a 的第一个闭环节点。
> ⚠ 与阶段 83 / 84 同型：**先量再改**，而这次量出来的东西**把登记里写的修法推翻了一半**。

## 87.1 一句话结论

`#61a` 登记的修法是「给产出方加**码表 + 带码异常**」。量的那一遍把它推翻了一半：
**A 类点名的四个产出方全是纯函数、都不抛异常**（失败是**返回值**）
⇒ 「带码异常」在它们的站点上**无处安放**。模板换成「**返回值带码**」：
一份「码 + 中文回落原文 + 实参」的记录体（`SvgParseError`），与 `ScriptError` /
`LuaSyntaxValidator` 那条路同形 —— 站点侧**一行不用改**（`Describe` 是它的渲染出口）。

但同一遍也量出**一条真的本仓库异常路径**：AI 侧把 `SvgParser` 的中文错误串
**包进自己抛的裸 `InvalidOperationException`**，再由 `ai.gen.failed` 的 8 处宽捕获直通 `ex.Message`。
⇒ 第一刀落在这里，直通面 **29 站点 / 22 组合 → 21 / 19**。

**一句话**：先证的伪让模板换了，先量出的那条异常路径给了第一刀的落点。

## 87.2 量的那一遍（逐产出方，读现场代码 + 改返回类型让编译器点名）

| 产出方 | 登记说的站点 | 实际形状（读代码） | 「带码异常」能用吗 |
|---|---|---|---|
| `SvgParser.Parse` | `status.svg.apply.failed` 1 + AI 侧 | 返回 `(SvgDocument?, string?)` —— **失败是返回值**，不抛 | ✗ 无处安放 |
| `GeneratedCodeValidator.Validate` | 4（`status.verify.failed` / `audit.verify.code.failed` / `status.simulator.export.failed` 2） | 纯函数，返回 findings 列表 | ✗ 同上 |
| `CsvVariableImporter.Parse` | 3（`var.import.failed` / `gen.import.failed` / `comm.slave.var.import.failed`） | 纯函数 | ✗ 同上 |
| `RecipeManager.ImportCsv` | `gen.import.failed`（与上一条同键混档） | 纯函数 | ✗ 同上 |
| **AI 生成器**（`IconGenerator` / `ImageGenerator`） | `ai.gen.failed` 8（1 + 2 + 5） | **本仓库抛的裸异常**：`throw new InvalidOperationException($"SVG 生成无效: {error ?? "无可用形状"}")` | **✓ 有落点** |

**「改返回类型」是这一遍的仪器，不是风格选择**：`SvgParser.Parse` 的
`(..., string?)` → `(..., SvgParseError?)` 之后，**编译器当场点出全部消费点** ——
实测 ~40 个调用点里只有 **4 处**把 `Error` 当字符串用（2 个 UI 站点 + 2 个 AI 生成器），
其余全是 `(doc, _)` 或 `Assert.Null/NotNull`。编译实测 **0 警告 0 错误**印证了这个读数
（若真有几十处消费点，这一步会红成一片）。

## 87.3 改了什么

| 位置 | 内容 |
|---|---|
| `Core/Services/SvgParseIssueCodes.cs`（新） | **4 码**（`public const string`，反射要求 `IsLiteral`）：`CONTENT_EMPTY` / `ROOT_NOT_SVG` / `XML_INVALID` / `NO_SHAPES` |
| `Core/Services/SvgParseError.cs`（新） | 记录体：`Code` + `Message`（中文回落原文）+ `Args`；`NoShapes` 静态实例 |
| `Core/Services/SvgParseException.cs`（新） | 需要穿过 AI 层返回契约时才包（携带 `Error`） |
| `Core/Services/SvgParser.cs` | `Parse` 返回类型换掉；三处失败各自给码。`XML_INVALID` 的 `Message` **保留 `XDocument.Parse` 的原消息**（含行列号）—— 原文**不进模板**但不许丢 |
| `AI/Generators/IconGenerator.cs:74`、`ImageGenerator.cs:63` | 裸 `InvalidOperationException` → `throw new SvgParseException(error ?? SvgParseError.NoShapes)` |
| `App/Services/ExternalText.cs` | `SvgIssueKeyPrefix` + `SvgIssueKey(code)` + `Describe(SvgParseError)`；`FailureReason` 从**三支**改**四支**（新增第 ② 支接 `SvgParseException`） |
| 2 个 UI 站点 | 返回值档：`Format("asset.svg.parse.fail", error is null ? 既有提示键 : Describe(error))` |
| 8 个 `ai.gen.failed` 站点 | 异常档：`Format("ai.gen.failed", ex.Message)` → `Format("ai.gen.failed", FailureReason(ex))` |

## 87.4 两条搬运方式，**同一套码**

同一个码可以走两条路，取决于失败**怎么从产出方出来**：

| 档 | 谁来搬 | 出口 | 本阶段站点 |
|---|---|---|---|
| **返回值档** | 产出方把 `SvgParseError` 当返回值交出来 | `ExternalText.Describe(error)` | 2（`Wiring.DockAggregate.cs:85` / `AssetManagerView.axaml.cs:267`） |
| **异常档** | 上层把它包成 `SvgParseException` 抛出 | `ExternalText.FailureReason(ex)` 第 ② 支 | 8（`AiGenerateDialogBase` 1 / `AiChatPanelView` 2 / `AiIconDialog` 5） |

⇒ **码表只有一份、字典键只有一份**（`svg.issue.*`，4 键 × 两本，全 0 占位符）。
两条路的存在不是重复：一条服务「纯函数返回值」，一条服务「异常穿透」。

## 87.5 判据（`SvgParseIssueLocalizationGuardTests`，7 条）

| 判据 | 盯什么 |
|---|---|
| `EveryFailure_RendersTheDictionaryTemplate_NeverTheChineseFallback` | **4 码 × 两条搬运 = 8 行**，渲染必须**等于字典值**、且不许出现回落原文（毒化：回落原文换成 `<<…未被使用>>` 记号） |
| `BothDictionaries_CarryEveryCode_AndAreActuallyPaired` | 缺键 / en 含中文 / 两侧一字不差 / 0 实参却有占位符 |
| `XmlInvalid_StillCarriesTheDotNetOriginal` | `.NET` 原文**不进界面但不许丢**（现场 catch 一次拿期望值，**不写死措辞**） |
| `DeclaredCodes_AndProducedCodes_AreTheSameSet` | **双向闭包**：死码 / 产出了但没声明 |
| `BareException_StillFallsBackToTheRawMessage_SoTheCodeIsWhatMatters` | **诚实边界**（与上一条成对）：没码的照旧回落 `ex.Message` |
| `AiGenerators_ThrowTheCodedException_NotABareBclOne` | 两个 AI 产出方必须抛带码异常、且不许再拼裸中文原文 |
| `NegativeControls_EachBreakIsCaughtByTheRightJudgment` | 4 组负控（见下） |

**为什么第 6 条非有不可**：`ai.gen.failed` 那 8 处与产出方是**成对**的 ——
站点换成 `FailureReason(ex)` 之后，`ex` 若退回裸 `InvalidOperationException`，
`FailureReason` 接不住、走第 ④ 支渲染中文原文，而**直通面那张面是绿的**（站点形状确实变了）
⇒ 又是「面绿着、收益为零」。本函数 + 泄漏判据合起来才把这条路钉死（负控见 §87.6-①）。

**4 组负控（合成语料，两两成对）**：`en` 改回中文 / 摘掉一个键 / 塞一个多余占位符；
双向闭包的两侧各报一次；产出方注入裸异常 ⇒ **两条都点名**、合规写法 ⇒ **无话可说**、
**语料里缺一个目标文件 ⇒ 必须报"找不到"**、注释里写的反例**不算**；
回落原文冒头 ⇒ 点名、字典值 ⇒ 绿。

⚠ 负控本身踩了 **4 次**才立住，两次是**我自己的合成语料写错**（把「缺文件」那一支绕过、
或剥完注释剩空代码让隔壁那一支先亮）—— 教训：**合成语料的形状必须与真实语料一字不差**
（`ProducerViolations` 把「语料里缺文件」也算违规，那是它的下界，不能省）。

## 87.6 生产负控（两重，实测）

脚本 `.workbuddy/tmp/s87-nc-inject.py`（默认干跑；`--write` 落盘；还原走**写回原文 + `sha256`**）。

**① `icon-bare` —— 把 `IconGenerator` 退回裸 `InvalidOperationException` + 中文原文模板**

| 判据 | 注入后 | 读 |
|---|---|---|
| 新判据 `AiGenerators_ThrowTheCodedException_…` | **红** | 同一文件**两条都点名**：「没有抛带码的 SvgParseException」+「又把裸中文原文模板拼进异常」 |
| 直通面 `ExceptionMessagePassthroughFaceTests` | **3/3 全绿** | ⇒ 「产出方退化」这件事**只有新判据看得见**，它不是重复劳动 |

还原 `sha256=bc734d2768af5660`（与注入前一致）。

**② `m11-drop` —— 摘掉可达性机制的 M11 取数**

| 判据 | 注入后 | 读 |
|---|---|---|
| `EveryDictionaryKey_IsReachableBySomeMechanism` | **红** | 逐条点名 4 个键：`svg.issue.content_empty` / `.no_shapes` / `.root_not_svg` / `.xml_invalid` |
| `MechanismFaces_AreNonEmpty` | **红** | 「M11-SVG 解析码：实测 0 < 下界 4」 |

还原 `sha256=3e698e384a7292f5` 一致。

⚠ **② 顺带证明了「新键只有 M11 一条机制」**：若 M6（源码字面量）或 M2 也覆盖它们，
摘掉 M11 之后那 4 个键**不会**掉下来。这正是本项目要求的
「新增机制要核**别的面没跟着涨**」——这里有比对比数字更强的证据。

## 87.7 字典与可达性

| 项 | 动作 | 理由 |
|---|---|---|
| 两本字典各 **+4 键** | `svg.issue.*` | 码 → 键由 `SvgIssueKeyPrefix + code.ToLowerInvariant()` 拼出 |
| `Lang` 常量 | **一个都不加** | 源码里**没有整键字面量** ⇒ 按 `LangKeyCoverageTests` 的口径不需要常量 ⇒ `UnusedBaseline` **1120 不动** |
| 可达性机制 | 新增 **M11**（与 M8/M9/M10 同形：反射枚举 `public const string`、复用同源出口 `DeclaredCodes()`、下界取**满值 4**） | 原十条机制**原理上都看不见**这些键 |

落盘读数（`HMI_READINGS_OUT`）：

```
M1 655 / M2 1113 / M3 26 / M4 30 / M5 19 / M6 381 / M7 12 / M8 41 / M9 7 / M10 7 / M11 4
```

⚠ **跨阶段对照要小心**：与阶段 85 的十面读数比，只有 **M1 651 → 655（+4）**、**M6 385 → 381（−4）**
动了 —— 这一对**归因于阶段 86（`9b4618f`，C 类文案带上目标对象 ⇒ 新增 4 个 `Lang` 常量）**，
与本阶段无关；M2–M5 / M7–M10 **逐项不动**，本阶段新增的 4 个键**没有让任何别的面涨**。

## 87.8 连带（三处，都是同一契约改动的正当结果，不是放宽判据）

| 位置 | 改了什么 | 为什么必须改 |
|---|---|---|
| `AI.Tests` 两处 | `Assert.ThrowsAsync<InvalidOperationException>` → `<SvgParseException>`，并**连码一起断** | 异常类型是**故意换的**；只断类型的话「抛个空码」照样绿 |
| `CoreVisibleTextInventoryGuardTests` | 新增 1 条通道（`Services/SvgParseError.cs`，1 条中文回落）；常数 61 → **62** | 新文件里那 1 条中文是 `svg.issue.no_shapes` 的**回落**，与既有 ⑤ 同类（中文串当参数进已本地化模板）。**守卫自己先点出来的**（`+ Services/SvgParseError.cs: 登记 0，实扫 1`） |
| `docs/2026-09-20-core-visible-text-inventory.md` | §0/§3/§5 活表 61 → 62、6 → 7 条通道；追加 §8；顺带修掉 §3 ⑤ 行的**过期消费方**（`App/Wiring.Canvas.cs` → `App/Wiring.DockAggregate.cs`，阶段 54「只留总线」之后文档停在旧值） | 守卫的命名就是「== 文档写的数」⇒ 两侧必须一起动；§6/§7 是阶段 44 快照，**不动** |

## 87.9 门禁

| 项 | 读数 |
|---|---|
| 首次提交前（gate1，**红**） | `.workbuddy/tmp/s87-gate1-red.log` —— 3 条失败，全部是上表那三处的**未修状态**（AI.Tests 2 + Core.Tests 1），**没有一条是逻辑缺陷** |
| 修完后再次提交前（gate1） | `.workbuddy/tmp/s87-gate1.log`，mtime **14:26:12**；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误；路径卫生 643 |
| 代码批提交 | **`f966f54`**（21 files，+773 / −48，`2026-09-23T14:26:31+08:00`）|
| 提交后（gate2） | `.workbuddy/tmp/s87-gate2.log`，mtime **14:27:50** ⇒ **晚于提交 1 分 19 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| 对拍 gate1 vs gate2 | **剥掉墙钟后 812 行逐项相同**，唯一差异 = 路径卫生「已检查 643 → **646** 个文件」（**+3** ＝ 本阶段新增的 3 个 `src/HmiEditor.Core/Services/*.cs`；该步扫 `git ls-files 'src/*' 'tools/*' 'scripts/*' 'firmware/*'`，**有意排除 `tests/`** ⇒ 第 4 个新文件是测试，不计入） |
| 文档批提交 | **`c4a6a3d`**（3 files，+275 / −2，`2026-09-23T14:29:38+08:00`）—— 本专题 §87 + 登记表（#61a 改部分闭环 / #61 补状态 / 新增 #61f）+ `project-status` |
| 第三次门禁（gate3） | `.workbuddy/tmp/s87-gate3.log`，mtime **14:30:55** ⇒ **晚于文档批提交 1 分 17 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误；**工作树干净** |
| 对拍 gate2 vs gate3 | **读数 0 差异（812 行逐项相同）** |
| 计数 | 八程序集之和 3139 → **3146**（**+7** ＝ 本阶段新判据 7 条，`App.UiTests` 451 → 458） |
| `UnusedBaseline` | **1120 不动**（本阶段不加 `Lang` 常量） |

## 87.10 未闭环

1. **其余 A 类产出方的「返回值带码」**：`GeneratedCodeValidator`（4 站）/
   `CsvVariableImporter`（3 站）/ `FontSubsetAnalyzer` / `RecipeManager`。
   模板已在本阶段立住（`SvgParseError` 的形状可照搬），但它们各自的失败**语义更杂**
   （校验问题清单 / CSV 行错误），要逐条决定「哪些失败该给码」——**不是机械照搬**。
2. **`asset.import.fail` 的批量档**（`AssetManagerView.axaml.cs:222`）仍在
   —— 它现在**既**没带对象（"那个文件"结构上不存在）**又**没码。
3. **`ai.gen.failed` 里混着的 IO 那几处**：那 8 处是宽捕获，SVG 之外的失败
   （落盘 / 渲染）仍走第 ④ 支回落原文。要收口得先**按产出方拆站**（阶段 83 就记了「先拆再动」）。
4. **#61d（7 键）原样开着**（先读代码再定类）；**#61b 残留**只有批量档那 1 处。
5. **新判据的诚实边界**：它只断「渲染 === 字典值」与「产出方抛带码异常」，
   **断不了翻译质量**（en 的值是不是好英文），也断不了「码选得对不对」
   （例如 `NO_SHAPES` 与 `CONTENT_EMPTY` 混淆）—— 那是逐处判断。


# 阶段 88 · #61f 第一刀（**量的那一遍再次证伪**：产出方的「返回值带码」在站点上无处安放，缺陷在**消费点**）

> 阶段 88（2026-09-23）。**本阶段动行为** —— #61f 的第一个闭环节点。
> ⚠ 与阶段 83 / 87 同型：**先量再改**，而这次量出来的东西**又**推翻了登记里写的修法。

## 88.1 一句话结论

`#61f` 登记的修法是「其余 A 类产出方逐个照搬**返回值带码**」。量的那一遍**第二次证伪**了这个前提：

- 四个产出方（`GeneratedCodeValidator` / `CsvVariableImporter` / `FontSubsetAnalyzer` / `RecipeManager`）
  在 Core 里 **`throw ` 逐文件 0 命中** ⇒ 「加码表 + 带码异常」无处安放（与阶段 87 同一结论）；
- 8 个站点逐站读现场：**5 站只可能 catch 到 IO**、2 站是**无目标路径的兜底 catch**、
  1 块（2 行）里含**唯一真落点** `SimulatorProjectExporter.cs:68` —— 而那处是**兜底不变量**。

但同一遍量出**第 9 件事，而且它是真的**：`CsvVariableImporter` 的返回值
`ImportResult.Errors` 是 **`List<string>` 的裸中文串**，而它的两个消费方
**一个只取条数、一个整块丢弃** ⇒ CSV 里有坏行时**坏行被静默丢掉**，
用户在两条路上都看不到「是哪几行」。

⇒ 第一刀的落点**不是**「给产出方加码」这件机械活，而是**补上那条到界面的路**；
码只是它的前提。

## 88.2 量的那一遍（8 站逐站，读现场代码）

| # | 站点 | 键 | `try` 里**真正可能抛**的 | 目标对象 | 定类 |
|---|---|---|---|---|---|
| 1 | `MainWindow.axaml.cs:1512` | `status.verify.failed` | `GenerateFullProject`（纯）/ `GeneratedCodeValidator.Validate`（纯）/ `new ValidationResultView(findings)`（UI 构造） | 无 | **兜底 catch** |
| 2 | `MainWindow.axaml.cs:1513` | `audit.verify.code.failed` | 同上（**同一块** catch） | 无 | 兜底 catch |
| 3 | `MainWindow.axaml.cs:1596` | `status.simulator.export.failed` | **`SimulatorProjectExporter.cs:68` 的 `InvalidOperationException`** ＋ `Export` 里的写盘 IO | `outputPath` ✓ | A 类真落点 **+ IO 混档** |
| 4 | `MainWindow.axaml.cs:1597` | `status.simulator.export.failed` | 同上（审计行，**同一个键**） | 同上 | 同上 |
| 5 | `VariableImportView.axaml.cs:103` | `var.import.failed` | `File.ReadAllTextAsync(_selectedFilePath)` | `_selectedFilePath` ✓ | **C 类（IO）** |
| 6 | `RecipeEditorView.axaml.cs:892` | `gen.import.failed` | `files[0].OpenReadAsync()` ＋ `StreamReader.ReadToEndAsync` | `files[0].Path.LocalPath` ✓ | **C 类（IO）** |
| 7 | `CommunicationConfigView.axaml.cs:1020` | `comm.slave.var.import.failed` | `File.ReadAllText(files[0].Path.LocalPath)` | `files[0].Path.LocalPath` ✓ | **C 类（IO）** |
| 8 | `FontSubsetView.axaml.cs:89` | `font.export.fail` | `FontSubsetAnalyzer.ExportCharsetFile` 里的 **`File.WriteAllText`** | `file.Path.LocalPath` ✓ | **C 类（IO）** |

**四条从读数里直接读出来的事实**：

1. **唯一真落点**：`SimulatorProjectExporter.cs:68`
   `throw new InvalidOperationException("生成代码中未找到 create_screen 函数,无法导出模拟器工程")`。
   它是**兜底不变量** —— `GeneratedCodeValidator.cs:251` **已把同一件事报成 Error 级 finding**
   （`生成代码缺失 create_screen 函数`，显示在 `ValidationResultView`），且 `MainWindow:1576`
   在导出前先查 `HasErrors`（`SimulatorBuildService.cs:167` 那条路**不调校验器** ⇒ 两路不对称正是它存在的理由）。
2. **那处不能改返回类型**：`Export` 有 **~75 个调用点**（几乎全在测试）
   ⇒ 阶段 87 那件仪器（「改返回类型让编译器点名」）在这里会炸。
3. **阶段 86 的一条分类被推翻**：`gen.import.failed` 当时记为「A 类混档（一个 catch 同时包 IO 与
   `RecipeManager.ImportCsv`）」—— 实测 `RecipeManager.ImportCsv` **不抛**（返回 count；
   `0` ⇒ 已有 `recipe.csv.import.invalid` 提示）⇒ 那个 catch **实际只看得见 IO**。
4. **`FontSubsetAnalyzer` 不是纯函数**：`ExportCharsetFile` 里 `File.WriteAllText` 真的写盘 ⇒
   它的站点归 C 类，不是 A 类。

### 表 9 · 真正的缺陷（`ImportResult.Errors` 在两条路上都到不了界面）

| 消费方 | 改动前的处置 | 用户看到的 |
|---|---|---|
| `VariableImportView.axaml.cs:95-96` | 只取 `Errors.Count`，拼「\| N 个警告」 | **「有几条」**，看不到「哪几行」 |
| `CommunicationConfigView.axaml.cs:1002` | **连 `Errors` 都不读**（只用 `result.Variables`） | **零提示** —— `added` / `skipped` 照旧，坏行静默消失 |

⚠ 而台账 `CoreVisibleTextInventoryGuardTests.Pending["Services/CsvVariableImporter.cs"] = 16`
的理由写着「两个消费方都只显示 `Errors.Count`」——**对第二个消费方不成立**。
这与 **#54 是同一个病**：豁免表里的「理由」没人去代码里核，它就变成一个
**能把缺陷合法化的字段**。本阶段把那条理由**改对**，不是把数字改小就完事。

## 88.3 改了什么

| 层 | 改动 | 关键点 |
|---|---|---|
| Core | 新增 `CsvImportIssueCodes`（**5 码**：`TOO_FEW_LINES` / `NO_HEADER_COLUMNS` / `ROW_MISSING_NAME` / `ROW_EMPTY_NAME` / `ROW_PARSE_FAILED`）＋ `CsvImportIssue`（码 / **行号** / 中文回落 / 实参） | 形状照 `ScriptError`：**行号是结构化字段、不进 `Message`**（两处都有必然有一处先过期） |
| Core | `ImportResult.Errors`：`List<string>` → `List<CsvImportIssue>`，5 个错误站点逐条发码 | 类型改动是**取证手段**（配 `CsvVariableImporterTests` 那条断言的改写） |
| App | `ExternalText.CsvIssueKey` ＋ `Describe(CsvImportIssue)`（行号前缀走 `Lang.CsvLinePrefix`） | **本族唯一的归一化点**，两个消费点都调它 |
| 字典 | 两本各 **+6 键**：`csv.issue.*` ×5 ＋ `csv.line.prefix` | `Lang.CsvLinePrefix` **有代码引用** ⇒ `UnusedBaseline` **1120 不动** |
| 消费点 | `VariableImportView` / `CommunicationConfigView`：状态行带**条数**、`ToolTip` 逐条 `Describe` 渲染 | 与 `ScriptEditorView` 处置 `ScriptError` 的做法**同形** |

**一处顺带修**：`ExternalText` 的类注释写「**五条**键约定」，而阶段 87 加了 `svg.issue.*`
却没补这份清单（实际已有 6 条）⇒ 本阶段补成「**七条**」并把 M12 那条理由写进去。

## 88.4 判据（`CsvImportIssueLocalizationGuardTests`，8 条）

| # | 判据 | 盯什么 |
|---|---|---|
| 1 | `EveryIssue_RendersTheDictionaryTemplate_NeverTheChineseFallback` | **毒化**回落原文：4 条真实失败经 `Describe` 渲染后必须**等于字典期望串**（含行号前缀） |
| 2 | `BothLanguages_RenderDistinctly_AndInTheirOwnScript` | 真的切语言：zh 那遍**必须含汉字**、en 那遍**一个汉字都没有**且两遍不同（挂 `Language-global` 串行 + `finally` 还原） |
| 3 | `BothDictionaries_CarryEveryCode_AndAreActuallyPaired` | 键齐 + en 不含中文 + 两侧不一字不差 + **占位符个数 == 产出方实测实参个数** |
| 4 | `DeclaredCodes_AndProducedCodes_AreTheSameSet` | **三态闭包**（见下） |
| 5 | `ProducerCarriesTheExceptionMessageWithoutLosingIt` | 兜底那一档不许变成免费通行证：码仍在用、`ex.Message` 仍是实参 |
| 6 | `ConsumersKeepTheCodedShape` | **源码面**：产出方 ≥5 处发码且不退回 `List<string>`；**两个消费方都必须同时出现 `.Errors` 与 `ExternalText.Describe`** |
| 7 | `FallbackMessages_StayUsableChinese` | 回落**自身可用**（非空、含汉字、不回落出码名、行号非负）—— 阶段 81 定下的要求 |
| 8 | `NegativeControls_EachBreakIsCaughtByTheRightJudgment` | 5 组内嵌负控（见 §88.6） |

### 三态闭包（本阶段对「声明了但产不出」的处置）

`ROW_PARSE_FAILED` 是行循环的 `catch (Exception ex)` 兜底 —— 逐行读代码：循环体里
**每个操作都不可能抛**（字符串按 `Length` 迭代、每个列索引都有 `idx < fields.Length` 前置保护、
数值解析走 `TryParse`、其余是属性赋值）⇒ 当前输入空间里**构造不出**真实触发。

处置 = **把判据的不完备明写出来**（`NotProducible` 表，逐条带理由），而不是把它塞进
`ProducedCodes` 造假，也不是删掉码（它防的是"将来往循环体里加一句会抛的代码"）。
⚠ 那张表是**双向**的：它列的码一旦真能产出 ⇒ **红**；塞一个没声明的码进去 ⇒ 也红。

**落盘读数**：`声明 5 个 / 真实产出 4 个 / 兜底 1 个`
（`NO_HEADER_COLUMNS, ROW_EMPTY_NAME, ROW_MISSING_NAME, TOO_FEW_LINES`）。

## 88.5 面读数与对拍（**本阶段的收益不在直通面上**）

| 面 | 阶段 87 | 阶段 88 | 归因 |
|---|---|---|---|
| `M1-Lang 常量` | 655 | **656** | 新增 `Lang.CsvLinePrefix`（有代码引用）—— **唯一**的一处数变 |
| `M12-CSV 导入码` | — | **5** | 本阶段新增机制（下界取满值 5） |
| 其余十面 | `M2 1113 / M3 26 / M4 30 / M5 19 / M6 381 / M7 12 / M8 41 / M9 7 / M10 7 / M11 4` | **逐项不动** | — |

**⚠ 直通面（`ExceptionMessagePassthroughFaceTests`）本阶段一动没动（21 站点 / 19 组合）** ——
这是**对的**，而且值得写下来：本项缺陷的形状不是「`ex.Message` 直通模板」，
而是「**失败原因根本到不了界面**」，所以它在**那张面上原理上看不见**。
⇒ 本阶段收益的证据只有三样：**新判据的读数**（§88.4 表 1 的渲染行）、
**消费点接线面**（判据 6）、**Core 中文台账**（下节）。
这与阶段 87 那条纪律是同一条：**判「裸换算不算收口」的标准是「产出方有没有码」，
不是「面有没有变小」** —— 反过来也成立：**面没变小不等于没做事**。

### 台账（`CoreVisibleTextInventoryGuardTests`）

| 项 | 阶段 87 | 阶段 88 |
|---|---|---|
| 已核实通道 | 7 条 | **8 条**（新增 `Services/CsvVariableImporter.cs`，**5 条**中文回落，消费标记落 `CsvIssueKey(issue.Code)`） |
| 通道条数之和（常数） | 62 | **67** |
| `Pending["Services/CsvVariableImporter.cs"]` | **16**（理由：「两个消费方都只显示 Errors.Count」） | **11**（理由改对：剩下的是 `BuildColumnMap` 的**同义词表**） |

## 88.6 生产负控（两重，实测）

| # | 注入 | 期望红 | 实测 |
|---|---|---|---|
| NC-A | 把 `CommunicationConfigView` 里那块「读 `Errors` + `Describe`」整块摘掉（= 缺陷本来的样子） | `ConsumersKeepTheCodedShape` | **红并逐条点名该文件**（「不再读 Errors」＋「没有再走 ExternalText.Describe」）；⚠ **主判据 `EveryIssue_Renders…` 仍绿** ⇒ 两条判据互补、空子真实存在 |
| NC-B | 摘掉可达性 `ReachableKeys()` 里的 M12 取数 | `EveryDictionaryKey_IsReachableBySomeMechanism` ＋ `MechanismFaces_AreNonEmpty` | **红 ×2**：逐条点名 **5 个** `csv.issue.*` 键 ＋ 面下界报「**M12-CSV 导入码：实测 0 < 下界 5**」 |

**还原**：写回原文（**不用 `copy2`** —— 那会连 mtime 一起复制、MSBuild 不重建 ⇒ 正控跑的是负控的二进制），
逐字节核对 `sha256`：`CommunicationConfigView.axaml.cs = 8800d9cce4b15b07`（前 / 后一致）、
`LocalizationKeyReachabilityGuardTests.cs = 65c4cc449e13728e`（前 / 后一致）；
`grep -rn "NC-A\|NC-B" src/ tests/` **零残留**。

**内嵌负控 5 组**（判据 8）：① en 值改回中文 ⇒ 点名；② 摘一个键 ⇒ 点名缺键；
③ 实参个数读错 ⇒ 点名占位符那一支；④ 三态闭包**三支各报一次**（死码 / 没声明 / 兜底表过期 / 兜底表塞没声明的码）；
⑤ 渲染泄漏（回落原文冒头）＋ 接线面三种坏法（产出方退回裸列表 / 消费方丢 `Errors` / 消费方丢渲染出口）
＋ **两处下界**（语料缺文件必须报"找不到"、注释里的反例不算 —— 后者合成语料必须**同时含合规写法**，
只喂注释会红在隔壁那一支；合规的产出方语料必须**真的有 ≥5 处发码站点**，否则红在"发码站点"上）。

## 88.7 门禁

| 项 | 读数 |
|---|---|
| 提交前（gate1） | `.workbuddy/tmp/s88-gate1.log`，mtime **15:00:37**；`EXIT=0` / 7 步全绿 / 0 警告 0 错误；路径卫生 646 |
| 代码批提交 | **`1dde2b5`**（13 files，+833 / −22，`2026-09-23T15:00:56+08:00`）⇒ 日志比提交**早 19 秒** ✓ |
| 提交后（gate2） | `.workbuddy/tmp/s88-gate2.log`，mtime **15:02:11** ⇒ **晚于提交 1 分 15 秒** ✓；`EXIT=0` / 7 步全绿 |
| 对拍 gate1 vs gate2（`.workbuddy/tmp/s88-diffgate.py`） | 剥墙钟 + ISO 时间戳、**排序**后 **828 / 829 行，唯一实质差异 = 路径卫生「已检查 646 → 648」**（**+2** ＝ 本阶段新增的两个 `src/HmiEditor.Core/Services/*.cs`；该步走 `git ls-files` ⇒ **提交前看不见未跟踪文件**，与阶段 87 的 643 → 646 是同一个形状）。另有一处空行差异（日志收尾） |
| 计数 | 八程序集之和 3146 → **3154**（**+8** ＝ 本阶段新判据 8 条，`App.UiTests` 458 → **466**） |
| `UnusedBaseline` | **1120 不动**（`Lang.CsvLinePrefix` 有代码引用） |
| 文档批提交 | **`b30ffa5`**（4 files，`2026-09-23T15:06:49+08:00`）—— 本专题 §88 + 台账镜像 §9 + `project-status` + 登记表 |
| 第三次门禁（gate3） | `.workbuddy/tmp/s88-gate3.log`，mtime **15:08:11** ⇒ **晚于文档批提交 1 分 22 秒** ✓；`EXIT=0` / 7 步全绿 / 0 警告 0 错误；路径卫生 **648**（与 gate2 相同：文档批没有新增 `src/` 文件） |
| 对拍 gate2 vs gate3 | **实质差异 0** —— **829 / 828 行，4 行差异全是空行**（unified diff 头 3 行 + 1 个空行，即日志收尾的空行差异） |
| 第四次门禁（gate4） | 回填批提交后补跑；**只记在 `.workbuddy/memory/2026-09-23.md`**，不回写本专题（与阶段 85 / 86 / 87 一致）。⚠ 本行**刻意不含 gate4 读数**（写下它的时候还没跑）⇒ 读数以日志为准；若与 gate3 不一致，日志里另有小节 |

## 88.8 未闭环

1. **`GeneratedCodeValidator` 的 23 条 findings 文案直显中文**：`ValidationResultView` 的
   `Text = finding.Message` —— 它**已过台账通道核实**（算"到界面"），但**没本地化**，
   也**不在直通面上**（那张面只收 `ex.Message`）⇒ 英文界面下**整个验证结果对话框是中文**。
   这是「产出方有返回值、但没码」的**第三个实例**，登记 **#61g**。
2. **`RecipeManager` / `FontSubsetAnalyzer` 本阶段定类但不迁**：前者失败已由 `count == 0`
   走 `recipe.csv.import.invalid` 提示（可见）；后者是 **IO**（`File.WriteAllText`）⇒ 归 C 类。
3. **`#61f` 量出的 5 站 IO 未迁**：`var.import.failed` / `gen.import.failed` /
   `comm.slave.var.import.failed` / `font.export.fail` / `status.simulator.export.failed`(2 行)
   —— 它们与 **#61b 的 C 类同形**（目标对象都在作用域）⇒ 可迁三实参模板
   `UiOperationGuard.FailureDetail(操作名, 目标路径, 异常)`，登记 **#61h**
   （本阶段刻意不动：一次只放开一个变量）。
4. **`SimulatorProjectExporter.cs:68` 判过的「不迁」**：它是兜底不变量（校验器已报同一件事）、
   且 `Export` 有 ~75 个调用点 ⇒ 不给它建码、也不改返回类型。与 #61c / #58 同类的**诚实边界**。
5. **新判据的诚实边界**：① 只断「渲染 === 字典期望串」与「消费点接线」，
   **断不了翻译质量**；② `ROW_PARSE_FAILED` 那一档的实参是 **BCL 原文（英文）**，
   英文界面下这一半照旧英文 —— 有意留的（原文是排障信息）；
   ③ 判据 6 是**源码面**证据，不是端到端 UI 断言（「用户真的看见了」没被断）。

---

# 阶段 89 · #61g 第一刀（**修法方向没被推翻，但登记里的「23 条」被拆成了三个数**）

> 阶段 89（2026-09-23）。**本阶段动行为** —— #61g 的第一个闭环节点。
> ⚠ 与阶段 87 / 88 同型：**先量再改**。这次量出来的东西没有推翻方向，但**推翻了登记里的计数口径**。

## 89.1 一句话结论

`#61g` 登记的修法（「建码 + `ExternalText` 出口 + 两本字典 + 一条可达机制」）**站得住** ——
与 #61a / #61f 不同，这次**不需要改方向**。但登记里那句「**23 条 findings 文案**」
把**三个不同的数**混成了一个，而它们各自决定了不同的工作量：

| 数 | 含义 | 为什么不能混 |
|---|---|---|
| **14** | `findings.Add(...)` **站点**数 | 站点 ≠ 文案：`ValidateRuntimeUsers` 里**一个站点**经 `switch` 发出 **5 个**码 |
| **18** | **消息**（文案模板）数 | 这才是要建码的那一档 |
| **23** | 该文件里的**中文字面量**数（台账口径） | = 18 条消息回落 **+ 5 条定位字面量**（`-` / `屏幕 …` / `生成代码` 也在内） |

⇒ 第一刀的落点是**两条通道**：`Message`（**18 码**）与 `Location`（**只有 4 种需要码**，
另 4 种与语言无关，见 §89.5）。

**并且量出一条与文件家族自身设计意图相矛盾的读数**：`RuntimeUserIssueKind` 的类注释
（`src/HmiEditor.Core/Security/RuntimeUserValidator.cs:10-12`）**明文写着**
「刻意只给枚举、不带文案：Core 侧的测试断言枚举（稳定），文案由 App 层映射」——
而 `GeneratedCodeValidator.ValidateRuntimeUsers` 恰恰在 Core 里把 `RuntimeUserIssueKind`
映射成 **4 条中文长句**。⇒ 修法有**现成答案**：那次映射**留着**，只是**映射到码而不是中文**。

## 89.2 量的那一遍（逐站点读现场代码）

| 子检查 | `findings.Add` 站点 | 发出的码 | 定位档 |
|---|---|---|---|
| `ValidateStructure` | 3 | `NO_SCREENS` / `DUPLICATE_SCREEN_NAME` / `DUPLICATE_CONTROL_ID` | `-`（中性）／屏幕 id 列表（中性）／`SCREEN` |
| `ValidateFaceplateParams` | 3 | `FACEPLATE_TEXT_UNRESOLVED` / `FACEPLATE_BINDING_UNRESOLVED` / `FACEPLATE_BINDING_LEGACY_UNRESOLVED` | `SCREEN_CONTROL` ×3 |
| `ValidateExpressions` | 1 | `EXPRESSION_SYNTAX` | `SCREEN_CONTROL_PROPERTY` |
| `ValidateDanglingReferences` | 1 | `DANGLING_REFERENCE` | **外来定位**（§89.5） |
| `ValidateRuntimeUsers` | 2 | 4 个 `RUNTIME_USER_*`（一个站点经 `switch`）＋ `RUNTIME_USER_TABLE_UNUSABLE` | `SecurityConfig.RuntimeUsers[..]`（中性） |
| `ValidateGeneratedCode` | 4 | `EMPTY_GENERATED_CODE` / `MISSING_CREATE_SCREEN` / `GENERATOR_WARNING_COMMENT` / `UNKNOWN_LVGL_API` | `-`（中性）×2 ＋ `GENERATED_CODE` ×2 |
| **合计** | **14** | **18** | 4 有码 ＋ 4 中性 ＋ 1 外来 |

**消费面**：全仓**只有一处**文本出口 —— `ValidationResultView.axaml.cs` 的
`Text = finding.Message` / `Text = finding.Location`（列表项）与 `CopyAll` 的插值串；
`MainWindow:1503` 把 findings 交给对话框、`:1578` **只用条数**（`HasErrors`），文本不外流。
⇒ 与 #61f 那次的形状**不同**：那里是「有人把它丢了」，这里是「有人把它**直显**了」。

## 89.3 改了什么

| 层 | 改动 | 关键点 |
|---|---|---|
| Core | 新增 `Services/GeneratedCodeIssueCodes.cs`（**18 消息码**）＋ 同文件的 `FindingLocationCodes`（**4 定位码**） | 分**两张**表：定位与消息的**生命周期不同**（定位还会被复制 / 筛选） |
| Core | `GeneratedCodeFinding` 增 `Code` / `LocationCode` / `Args` / `LocationArgs`；14 站点逐条补码与实参 | `Code` **刻意不给默认值** ⇒ 编译器把全部站点点出来（阶段 87 那条「改返回类型＝取证手段」）；实测**恰好 1 个错误**（1 处测试构造） |
| Core | `ValidateRuntimeUsers` 的 `switch` 从「产中文」改成产 `(code, message, args)` 三元组 | **映射留着、产物改成码**（§89.1）—— 顺带回到 `RuntimeUserIssueKind` 类注释写下的约定 |
| App | `ExternalText.GenIssueKey` / `GenLocationKey` ＋ `Describe(GeneratedCodeFinding)` / `DescribeLocation(...)` | **本族唯一的归一化点**：列表项与「复制全部」都调它 |
| 字典 | 两本各 **+21 键**（`gen.issue.*` ×17 ＋ `gen.loc.*` ×4；**2199 → 2220**） | 17 而不是 18：`GENERATOR_WARNING_COMMENT` **刻意无键**（§89.5） |
| 可达性 | 新增 **M13**（面 **21**，恰等于新增键数） | 取数复用新守卫的 `DeclaredCodes()` / `DeclaredLocationCodes()`，并按 `MessageNoKey` 摘掉那 1 个无键码 |

**回落中文串逐字不变** ⇒ Core 侧既有的 6 处 `Location` 断言与 2 处消息断言**一条都不用改**；
唯一要跟进的是那 1 处测试构造（`WindowProductionPathUiTests:138`）。

## 89.4 判据（`GenFindingLocalizationGuardTests`，11 条）

| # | 判据 | 盯什么 |
|---|---|---|
| 1 | `EveryFinding_RendersTheDictionaryTemplate_NeverTheChineseFallback` | **毒化**回落原文：17 条真实 finding 经 `Describe` 渲染后必须**等于字典期望串**（含实参） |
| 2 | `KeylessMessages_AreExactlyTheOnesWeDeclared` | 「刻意无键」那张表的**双向**闭合 ＋ 它必须真的产得出 |
| 3 | `EveryLocation_RendersTheDictionaryTemplate_OrIsLanguageNeutral` | 有码档渲染 === 字典；`LocationCode == ""` 档必须**直出**且**不含汉字** |
| 4 | `ForeignLocations_AreStillForeignAndStillUncoded` | 「外来定位」那张表的**双向**闭合（§89.5） |
| 5 | `BothLanguages_RenderDistinctly_AndOnlyArgsMayCarryForeignText` | 切语言：zh 那遍必须含汉字；en 那遍**剥掉实参后**不许剩汉字 |
| 6 | `BothDictionaries_CarryEveryCode_AndAreActuallyPaired` | 键齐 ＋ en 不含中文 ＋ 两侧不一字不差 ＋ **占位符个数 == 实测实参个数**（消息 17 码与定位 4 码**各走一遍**） |
| 7 | `DeclaredCodes_AndProducedCodes_AreTheSameSet` | **三态闭包**：消息码（声明 18 / 产出 17 / 兜底 1 / 无键 1）与定位码（声明 4 / 产出 4 / 兜底 0）**各走一遍** |
| 8 | `NotProducibleArm_IsStillLiveAndStillTakesTwoArgs` | 兜底那一档的「2 实参」**钉回源码**（窗口内仍有 `new object[] { issue.Index, issue.Username }` 且回落仍含汉字） |
| 9 | `FallbackMessages_StayUsableChinese` | 回落**自身可用**（非空 / 含汉字 / 不回落出码名） |
| 10 | `ConsumerKeepsTheCodedShape` | **源码面**：产出方 ≥18 处发消息码 / ≥7 处发定位码；出口两个键推导还在；消费方**走出口且四条直显形状全无** |
| 11 | `NegativeControls_EachBreakIsCaughtByTheRightJudgment` | 6 组内嵌负控（§89.6） |

**同源复用（不抄第二份）**：成对性 / 三态闭包 / 渲染泄漏 / 占位符计数四条纯函数**直接调**
`CsvImportIssueLocalizationGuardTests` 的那四个 —— 为此给 `PairingViolations` 加了一个
`keyFor` 可选参数（默认仍是 `csv.issue.`，**那里既有 7 处调用一字未动**）。

## 89.5 定位的**三档**，以及两处「判过的『不做』」

| 档 | 数量 | 处置 | 守住它的东西 |
|---|---|---|---|
| **有码** | 4 | 走 `gen.loc.*`：`SCREEN` / `SCREEN_CONTROL` / `SCREEN_CONTROL_PROPERTY` / `GENERATED_CODE` | 判据 3 ＋ 6 |
| **中性·直出** | 4 | `-` / 屏幕 id 列表 / `SecurityConfig.RuntimeUsers[..]` / `SecurityConfig.RuntimeUsers` —— **模型路径，翻了反而对不上代码** | 判据 3（不含汉字）＋ 三态闭包（声明了却产不出 ＝ 死码） |
| **外来定位** | 1 | `DANGLING_REFERENCE` 的串由 `CrossReferenceAnalyzer.FindDanglingReferences` 拼（形如 `[屏名] 控件 '名' (属性)`），**本刀刻意不吞** | `ForeignLocation` 表**双向**：一旦它开始发码、或串变得与语言无关 ⇒ 那一行过期 ⇒ 红 |

**刻意无键的那一档**：`GENERATOR_WARNING_COMMENT` —— 消息体就是产物 `generated_ui.c` 里
`// WARNING:` 注释的**原文**，翻译它等于**改写产物**（与 #61c 同类的「判过的『不迁』」）。
判据 2 双向守着：给它补了键 ⇒ 红；别的码缺键 ⇒ 也红。

## 89.6 生产负控（**三重**，实测）

| # | 注入 | 期望红 | 实测 |
|---|---|---|---|
| NC-B | 摘掉可达性 `ReachableKeys()` 的 M13 取数段（**保留下界行**） | 主判据 ＋ 元判据 | **红 ×2**：**21 个键**被逐条点名（`gen.issue.*` 17 ＋ `gen.loc.*` 4）＋「**M13-生成代码验证码：实测 0 < 下界 21**」；另 4 条绿 |
| NC-A1 | `ValidationResultView` 两条通道退回 `finding.Message` / `finding.Location`（**缺陷本来的样子**） | `ConsumerKeepsTheCodedShape` | **红 ×1**，4 条点名（两条「没走出口」＋两条「仍有直显站点」）；**另 10 条绿** |
| NC-A2 | `ExternalText` 的 `GenIssueKey(finding.Code)` 改成等价的局部变量写法（**标记串消失**） | `CoreVisibleTextInventoryGuardTests.VisibleChannels_EvidenceStillExists` | **红 ×1**，逐字点名「通道 `Services/GeneratedCodeValidator.cs` 声称经由 `src/HmiEditor.App/Services/ExternalText.cs` 显示，但那里已经没有标记：`GenIssueKey(finding.Code)`」；另 9 条绿 |

**还原**：写回原文（**不用 `copy2`** —— 它会把 mtime 一起复制 ⇒ MSBuild 不重建 ⇒ 正控跑的是负控的二进制），
逐字节核对 `sha256`（三处**前 / 后一致**）：
`LocalizationKeyReachabilityGuardTests.cs = d5411571840b1e3896bab15888b6cd05b6d5f9dc9df31efaa63cf911a4cedc4c`、
`ValidationResultView.axaml.cs = a34c2fd1318d548c0c203b83d06993994f0031327477b12b34016c5edc44f00f`、
`ExternalText.cs = 573aaf79e4c3da77058187f24823b4a75a1828f4182ba7a42676b67e09319065`；
还原后两条守卫类 **17/17 复跑全绿**。

⚠ **一条方法教训**：NC-A2 的第一版注入写成「把 `GenIssueKey` **改名**」⇒ **编译不过**（`CS0103`），
测试根本没跑到判据那一步。**负控必须是「能编译的坏形状」** —— 否则拿到的是 build 失败，不是判据红。

## 89.7 面读数与对拍（本阶段**也不在直通面上**）

| 面 | 阶段 88 | 阶段 89 | 归因 |
|---|---|---|---|
| `M13-生成代码验证码` | — | **21** | 本阶段新增机制（下界取满值 21 = 18 码 ＋ 4 码 − 1 个无键码） |
| 其余十二面 | — | **逐项不动** | `M1 656 / M2 1113 / M3 26 / M4 30 / M5 19 / M6 381 / M7 12 / M8 41 / M9 7 / M10 7 / M11 4 / M12 5` |

**⚠ 直通面（`ExceptionMessagePassthroughFaceTests`）本阶段一动没动** —— 与阶段 88 同一条理由：
本项缺陷的形状不是「`ex.Message` 直通模板」，而是「**界面那两条通道根本没经过字典**」，
所以它在那张面上**原理上看不见**。收益的证据是三样：**判据 1 / 3 的渲染读数**
（`gen.issue.*` 17 行 ＋ `gen.loc.*` 7 行 ＋ 中性 4 行，落盘 `.workbuddy/tmp/s89-readings.txt`）、
**接线面**（判据 10）、**新机制的面 21**。

### 台账（`CoreVisibleTextInventoryGuardTests`）

| 项 | 阶段 88 | 阶段 89 |
|---|---|---|
| 已核实通道 | 8 条 | **8 条（条数不变）** —— 通道 ① 的**消费方与标记**换了：`ValidationResultView.axaml.cs` ＋ `Text = finding.Message` → **`Services/ExternalText.cs` ＋ `GenIssueKey(finding.Code)`** |
| 通道条数之和（判据常数） | 67 | **67 不动**（`Services/GeneratedCodeValidator.cs` 仍是 **23**） |

⚠ 这一条值得单独写下来：**「本地化了一个文件」不等于「台账里的条数要变小」**。
那 23 条中文**按设计必须留在 Core**（它们是字典缺键时的**回落原文**），
变的是它们**到用户眼里的路径**，不是数量 —— 把 23 改小才是造假。

## 89.8 门禁

| 项 | 读数 |
|---|---|
| 门禁（gate1，**红**） | `.workbuddy/tmp/s89-gate1.log`，mtime **16:03:07**；`EXIT=1` —— `App.UiTests` **1 红 / 477**：`XamlResourceKeyClosureTests.PhantomKeyVictimButtons_ActuallyCarryTheirBackground`，异常是**平台初始化**处的 `InvalidOperationException: The calling thread cannot access this object because a different thread owns it`（`AvaloniaHeadlessPlatform.Initialize` → `DefaultRenderLoop.Add` → `VerifyAccess`）。**判为既有的 #47 族并发红**：① 同一条异常在本仓历史门禁日志里至少出现过 **3** 次（`ci-gate-46.log` / `ci-gate-76-post.log` / `s83-gate3.log`，那几次点名的是 `DispatcherOwnershipContractTests.CrossDispatch_…`）；② 该用例**单独复跑立刻绿**（`--filter …PhantomKeyVictimButtons…` ⇒ **1/1 通过**）；③ 本阶段的改动**不碰任何 UI 面**（新守卫是纯文件读取 ＋ 纯 Core 校验器调用）。⚠ 门禁第 5 步是 `dotnet test HmiEditor.slnx`（八个程序集**并行**）⇒ 正是那个窗口最大的跑法 |
| 门禁（gate1b，**绿**） | `.workbuddy/tmp/s89-gate1b.log`，mtime **16:04:51**；`EXIT=0` / **7 步全绿** / 0 警告 0 错误；路径卫生 **648** |
| 内容批提交 | **`eb7e4bb`**（11 files，+1319 / −38，`2026-09-23T16:05:20+08:00`）⇒ gate1b 比提交**早 29 秒** ✓ |
| 提交后（gate2） | `.workbuddy/tmp/s89-gate2.log`，mtime **16:06:40** ⇒ **晚于提交 1 分 20 秒** ✓；`EXIT=0` / 7 步全绿 |
| 对拍 gate1b vs gate2 | **828 / 828 行，5 行差异，唯一实质差异 = 路径卫生「已检查 648 → 649」**（**+1** ＝ 本阶段新增的 `src/HmiEditor.Core/Services/GeneratedCodeIssueCodes.cs`；该步走 `git ls-files` ⇒ **提交前看不见未跟踪文件**，与阶段 87 / 88 同一形状） |
| 计数 | 大队八项之和 3154 → **3165**（**+11** ＝ 本阶段新判据 11 条，`App.UiTests` 466 → **477**；**其余九项逐条一致** ⇒ 纯净增） |
| `UnusedBaseline` | **1120 不动**（本阶段**不加** `Lang` 常量 —— `gen.*` 键由 App 侧推导） |
| 文档批提交 | **`e676d20`**（4 files，**+328 / −4**，`2026-09-23T16:20:21+08:00`）—— 专题 §89 ＋ `project-status.md` §89 ＋ `2026-09-20-core-visible-text-inventory.md` §10 ＋ 登记表（#61g 改闭环 ＋ #61i/#61j/#61k/#61l 新开） |
| 第三次门禁（gate3） | `.workbuddy/tmp/s89-gate3.log`，mtime **16:22:01** ⇒ **晚于文档批提交 1 分 40 秒** ✓；`EXIT=0` / **7 步全绿** / 0 警告 0 错误；路径卫生 **649**（与 gate2 同）；大队八项之和 **3165**（与 gate2 同）；负载 `/proc/loadavg` `1.69 → 3.95`（起 `16:20:25` / 讫 `16:22:01`） |
| 对拍 gate2 vs gate3 | **实质差异 0** —— 剥掉墙钟（`已用时间 HH:MM:SS.ss` / `持续时间: N s` / 审计行内嵌的 `2026-09-23T…`）后做**多重集**差集（`.workbuddy/tmp/s89-cmp.py`）：gate2 侧 3 行、gate3 侧 1 行，全部是 dotnet 那三句「总共 1 个测试文件与指定模式相匹配。」的**换行方式**不同（内容各 **3** 次、完全相同）⇒ 本轮只改 `docs/`，门禁读数**本就不该动** |
| 第四次门禁（gate4） | 见 `.workbuddy/memory/2026-09-23.md`（与阶段 85 / 86 / 87 / 88 一致：回填之后的读数只记日志、不回写本专题） |

## 89.9 未闭环

1. **⚠ 阶段 91 已闭环（见 §91）** —— 当时这一条是开着的：**`EXPRESSION_SYNTAX` 的第二个实参仍是中文**：它来自 `ExpressionEngine` 的三条
   `FormatException`（`三元运算缺少 ':'` / `缺少右括号 ')'` / `意外的符号: … (位置 N)`），
   经 `{1}` 进模板 ⇒ 英文界面下那一半中英混排。**这是本阶段刻意留的诚实边界**
   （判据 5 的「剥掉实参后不许剩汉字」就是为它写的），登记 **#61i**。
   顺带量到一件事：这 3 条抛错文案**不在** `CoreVisibleTextInventoryGuardTests` 的台账里 ——
   它们被 `DevMessage`（`throw new`）那一类豁免挡住了。
2. **`CopyAll` 与两个品牌字面量**：`CopyAll` 拼的 `[Error] Structure: …` 里
   `[` / `]` 与 `Severity` / `Category` 的**枚举名**没走字典；`FindingCategory.Faceplate` → `"Faceplate"`、
   `LvglApi` → `"LVGL API"` 是两个**品牌字面量**（判过：不该翻）⇒ 前一半登记 **#61j**。
3. **产物注释的语言不一致**：`GENERATOR_WARNING_COMMENT` 回显的是 `CodeGenerator.Screen.cs` 写的
   `// WARNING:` 注释，而那里**自己就中英不一致**（3 条英文 ＋ 1 条中文）⇒ 登记 **#61k**。
4. **外来定位**：`DANGLING_REFERENCE` 的定位串由 `CrossReferenceAnalyzer` 拼，**同一串在两个地方露出**
   （交叉引用面板 ＋ 本对话框）⇒ 要一起收口，登记 **#61l**。
5. **新判据的诚实边界**：① 只断「渲染 === 字典期望串」与「消费点接线」，**断不了翻译质量**；
   ② 判据 10 是**源码面**证据，不是端到端 UI 断言；③ 「刻意无键」与「外来定位」两张表都是
   **判据的不完备的明写**，且都**双向** ⇒ 它们不会退化成永久豁免。
# 阶段 90 · #61h 落地（**直通面 21 → 15 站点**；并把阶段 84 反对用三实参重载的那条理由**反着钉住**）

## 90.1 一句话结论

`#61h` 的处置方向**没被推翻** —— 它本来就是照 `#61b` 的 C 类同形办的（迁到
`UiOperationGuard.FailureDetail(操作名, 目标路径, 异常)`）。这一刀真正的产出有两件：

1. **登记里的一句话被实测推翻**：原文写的是「**目标对象都在作用域**」（还并列了四个表达式）。
   逐站读现场代码后发现，**其中 2 站的取值表达式在作用域、被取的对象不在**
   （`CommunicationConfigView` 的 `files`、`MainWindow` 的 `outputPath` 都声明在 `try` **之内**）
   ⇒ 直接写表达式会**编译期 `CS0103`**。已就地更正登记（见 90.2 表）。
2. **补上一条此前没有任何判据管的推理**：阶段 84 反对在 BCL / IO 站点上用三实参重载的**唯一**理由，
   是「BCL 的 IO 原文自带目标路径」⇒ 再包一层「—— &lt;路径&gt;」路径会**显示两次**
   （其中一次还是 `.tmp` 兄弟路径）。阶段 85（#61e）把原因那半码化成 **0 实参短句**之后那条理由
   消失 —— 但「它消失了」一直只是**推理**，而**没有任何判据会拦住它回来**：
   直通面只认 `Format(键, ex.Message)` 那个形状（见到 `FailureDetail(op, path, ex)` 就当作"已迁"），
   `EveryDetailCallSite_PassesAConcreteTargetPath` 只认「有没有给路径」、**不认「给了几遍」**。
   ⇒ 本阶段把那条推理**反着钉住**（90.4 判据 ①）。

## 90.2 量的那一遍（逐站读现场代码）

| # | 站点（阶段 90 后的行号） | 迁移前的形状 | 目标对象在作用域吗 | 处置 |
|---|---|---|---|---|
| 1 | `VariableImportView.axaml.cs:112` | `Format("var.import.failed", ex.Message)` | ✓ `_selectedFilePath` 是**字段**（恒在作用域） | 直接传 |
| 2 | `RecipeEditorView.axaml.cs:892` | `Format("gen.import.failed", ex.Message)` | ✓ `files` 声明在 `try` **之前** | 直接传 `files[0].Path.LocalPath` |
| 3 | `CommunicationConfigView.axaml.cs:1037` | `Format("comm.slave.var.import.failed", ex.Message)` | ✗ `files` 声明在 `try` **之内** | **承接变量** `string? targetPath` 提到 `try` 外；且赋值与 `File.ReadAllText` **共用**它 |
| 4 | `FontSubsetView.axaml.cs:90` | `Format("font.export.fail", ex.Message)` | ✓ `file` 声明在 `try` 外 | 直接传 `file.Path.LocalPath`（该文件还缺 `using HmiEditor.App.Services;`，一并补上） |
| 5 | `MainWindow.axaml.cs:1600`（2 行） | `Format("status.simulator.export.failed", ex.Message)` **×2** | ✗ `outputPath` 声明在 `try` **之内** | **把目标目录提前到 `try` 外算好**（它只依赖已校验过的 `folder`）⇒ 连"承接变量 + 赋值"两行都不需要，行数**中性**（见 90.7） |

**第 5 站的两行是同一句话抄了两遍**（状态栏 ＋ 审计行）—— 迁的时候顺手收成一个局部变量
（`var failure = …`），否则"改一处漏一处"的形状会原样留下。

⚠ 第 3 站那处还有个细节不能照抄：承接变量若只用来渲染、而 `File.ReadAllText` 仍走
`files[0].Path.LocalPath`，报给用户的路径与**真正读过**的那一个就可能不是同一个 ⇒
本阶段让两者**共用** `targetPath`。

## 90.3 改了什么

| 层 | 改动 |
|---|---|
| 字典 | 两本各 **+4** 操作名键（`ui.op.import.variables` 导入变量 / `ui.op.import.recipe` 导入配方 / `ui.op.import.slave.vars` 导入子设备变量 / `ui.op.export.charset` 导出字符集） |
| 字典 | 两本各 **−5** 旧键（`var.import.failed` / `gen.import.failed` / `comm.slave.var.import.failed` / `font.export.fail` / `status.simulator.export.failed`）⇒ **2220 → 2219**（净 −1） |
| `Lang` | **+4** 常量（上面 4 个新键）；**−4** 常量（`VarImportFailed` / `GenImportFailed` / `FontExportFail` / `StatusSimulatorExportFailed`） |
| 站点 | 5 站 / 5 文件迁到三实参出口（表见 90.2）；`FontSubsetView` 补一个 using |

⚠ **删 5 个键只带走 4 个常量** —— `comm.slave.var.import.failed` 那条键**本来就没有常量**：
它的使用点写的是**字面量**（与 `StatusOpenFailed` / `GenExportFailed` 同一个形状）
⇒ 常量只能靠 M6 桶担保，而它连常量都没建。所以「字典 −5 而常量 −4」不矛盾。

## 90.4 判据（新判据 2 条 ＋ 3 处实测同步）

| # | 判据 / 同步 | 内容 |
|---|---|---|
| ① | **新增** `UiOperationGuardContractTests.PathAppearsExactlyOnce_AndEachHalfInItsOwnLanguage` | **真实** BCL 异常（`File.ReadAllText` 打在真不存在的文件上 —— **不手搓 `Message`**，手搓等于把前提自己写进夹具）＋ 真实路径过三实参出口 ⇒ ① 渲染里路径**恰好 1 次**；② 中英两语言下操作名与原因那半**各是自己的语言**（英文侧**一个汉字都不许有**，复用 `SystemIssueLocalizationGuardTests.HasCjk`）；③ 两语言的操作名/原因串**确实不同**（否则"各说自己的语言"是空话）。⚠ 该 test 内还有两条**前提重测**断言：`ex.Message` 真的自带路径、`CodeFor(ex) == FILE_NOT_FOUND` —— 它们变红说明**前提**变了，该去重新量，不是改断言。 |
| ② | **新增** `DuplicatePathViolations(…)` ＋ `PathOccurrences(…)`（纯函数） | 路径计数**不用正则**（路径里可能有 `.` / `(` / `[` / `+`；本机临时目录名里就出现过）⇒ 转义漏一个就把判据变成恒真或恒假。`render` **当参数传入** ⇒ 负控能喂旧形状。 |
| ③ | 同步 `ExceptionMessagePassthroughFaceTests` | 基线删 **5 行** ⇒ **19 → 14 组合 / 21 → 15 站点**；自检下界 `sites.Count >= 20` ⇒ **`>= 10`**（口径失效时面掉到个位数；下界刻意**不贴**实测值，免得每次**正确**的迁移都多红一条）。 |
| ④ | 同步 `LangConstantUsageGuardTests.UnusedBaseline` | **1120 → 1116**（**相等**判定，必须实测 —— 见 90.7）。 |
| ⑤ | `EveryDetailCallSite_PassesAConcreteTargetPath` | **未改**（用的是下界 `sites >= 10`）⇒ 加 5 处安全；本阶段实测它能把"填 `null`"精确点名（90.5 负控 ①）。 |

⚠ 新判据里还有一条**可读性修正**，是本阶段负控逼出来的：原来用 `Assert.Empty(zhBad)`，
而 xunit 会把长字符串**截断**成 `Collection: ["「导入变量失败: /home/…/u"···]` ——
「出现了 2 次」这个关键读数**还没被看到就没了**。⇒ 换成
`Assert.True(count == 0, "…：\n    " + string.Join("\n    ", bad))`。**红必须自带信息**。

## 90.5 生产负控（**三重**，实测；一律写回原文、`sha256` 前后逐字节一致）

| # | 注入 | 实测 |
|---|---|---|
| ① | 某站路径实参换 `null` 字面量（`VariableImportView` 的 `_selectedFilePath` → `null`） | `EveryDetailCallSite_PassesAConcreteTargetPath` **精确点名该文件**：「src/HmiEditor.App/Views/VariableImportView.axaml.cs：目标路径实参写成了 `null` ⇒ 面绿着，而用户看到的文案里**没有文件**」。⚠ 这是**能编译**的坏形状（`string?` 参数收 `null` 合法）—— 与阶段 89 记的「负控要挑破语义的注入、不要破语法」同一件事。 |
| ② | 三实参重载的**原因那半换回 `ex.Message`**（`ExternalText.FailureReason(ex)` → `ex.Message`） | 新判据报「路径出现了 **2 次**（应为 1 次）」，并把**旧形状的整条文案**打出来：`导入变量失败: <路径> —— Could not find file '<路径>'.` —— **阶段 84 反对的那一幕字面重现**。这正是本判据存在的全部理由。 |
| ③ | 某站回退成 `Format(键, ex.Message)` 旧形状（`FontSubsetView` 的 `lang.UiOpExportCharset` → `AppLocalization.Service.Format("font.export.fail", ex.Message)`） | 面判据报「**新增** src/HmiEditor.App/Views/FontSubsetView.axaml.cs · font.export.fail（1 处；基线里没有）」⇒ 「回退会被抓」在生产语料上被证明，而不只是靠合成语料。 |

**成对性**：三组的"另一侧"都是**同一条命令**在生产态上的读数 —— 17 / 17 通过
（`.workbuddy/tmp/s90-judges-run2.log` / `…run3.log`）。负控脚本 `.workbuddy/tmp/s90-nc.py`
只提供 `inject` / `restore` / `status` 三个动作，**还原用写回原文**（不是 `shutil.copy2` ——
那会把 mtime 一起带过去，MSBuild 不重建，下一次"正控"跑的其实是负控的二进制）。

## 90.6 面读数与基线同步（本项**确实**在直通面上 —— 与前四项不同）

| 面 | 阶段 89 | 阶段 90 |
|---|---|---|
| 直通面（站点） | 21 | **15**（−6：`status.simulator.export.failed` 2 处、其余 4 键各 1 处） |
| 直通面（`(文件,键)` 组合） | 19 | **14**（−5：5 个组合整条消失） |
| `UnusedBaseline` | 1120 | **1116**（−4） |
| 分桶 | M2 828 / M6 269 / M3 18 / 无机制 5 | **M2 828 / M6 265 / M3 18 / 无机制 5**（只有 M6 动） |
| 字典键（zh-CN） | 2220 | **2219** |

⚠ **`#61a` / `#61f` / `#61g` 三阶段都写过「面读数本阶段零变化」**（那三次改的是产出方或消费点，
不在那张面上）；本项**不一样** —— 它改的就是"站点写成什么形状"，所以这是 `#61b` 之后
**第二次**真的把那张面改小。同一张面在不同阶段是**证词**还是**目标**，取决于改的是哪一层。

## 90.7 字典与常量处置链（含一处被行数守卫逼出来的收口）

删键的老三步照走：**连 `Lang` 常量一起删**（`LangKeyCoverageTests` 的正向判定要求常量与键同生共死）
＋ 查 `scripts/sync_localization.py` 的 `VALUE_MAP`（实测**不含**这 5 个键 ⇒ 无需清理）
＋ 同步 `UnusedBaseline`。字典 JSON **非排序存放** ⇒ 逐行手术 ＋ `assert count == 1`，
不 `json.dump` 重写（那会产生千行级 diff）。

**行数守卫逼出来的一件事（本阶段唯一被守卫拦下的改动）**：`SingleFileSizeGuardTests`
对 `src/HmiEditor.App/MainWindow.axaml.cs` 是**相等**判定，且失败信息明写
「**涨了 = 治理回涨（先把文件改小）**」。第一版实现给该文件加了 **5 行**
（承接变量 2 行注释 ＋ 1 行声明 ＋ catch 里多 1 行注释），门禁当场红：
`行数 3018 ≠ 基线 3013`。**正确动作是把新增压回 0，不是把基线改大** ——
最终做法：**把目标目录提前到 `try` 外算好**（它只依赖已校验过的 `folder`），
于是"承接变量 + 赋值"两行都不需要，`try` 内那行是**搬**走的、
catch 里的说明注释收成**行尾**注释，净变化 **0**。
⚠ 这条不是格式洁癖：那个守卫的全部意义就是拦住"本地化/接线又把巨型单类喂回去"，
而**它分不出**哪次增长是正确性修复。

## 90.8 门禁

| 门禁（gate1，**红**） | `.workbuddy/tmp/s90-gate1.log`，mtime **16:44:00**；`EXIT=1` —— **唯一红**是 `HmiEditor.Core.Tests.SingleFileSizeGuardTests.File_LineCount_MatchesBaseline`：`src/HmiEditor.App/MainWindow.axaml.cs: 行数 3018 ≠ 基线 3013`。⚠ 这一条**不是**既有缺陷、也**不是**并发红 —— 是本阶段第一版实现真的加了 5 行（见 90.7），`App.UiTests 479 / 479`、其余程序集全绿 |
| 门禁（gate1b，**绿**） | `.workbuddy/tmp/s90-gate1b.log`，mtime **16:47:07**；`EXIT=0` / 7 步全部通过（门禁自报）/ **0 警告 0 错误**（第 3 步是"警告即失败"）；路径卫生 **649** |
| 内容批提交 | **`dbc7eb9`**（11 files changed, 265 insertions(+), 34 deletions(-)，`2026-09-23T16:47:38+08:00`）— feat(l10n): 5 站 IO 失败迁到「带目标路径」出口并删掉 5 条旧键（登记 #61h）⇒ gate1b（16:47:07）比提交**早 31 秒** ✓ |
| 提交后（gate2） | `.workbuddy/tmp/s90-gate2.log`，mtime **16:48:53** ⇒ **晚于提交 1 分 15 秒** ✓；`EXIT=0`；路径卫生 **649** |
| 对拍 gate1b vs gate2 | **0 行差异**（811 / 811 行；剥掉墙钟 —— `持续时间` / `已用时间` / 审计行内嵌的 ISO 时间戳 —— 后做**多重集**差集，两侧都为空）。路径卫生两跑都是 **649**（本批次**只改文件、没加文件**，而该步走 `git ls-files`）⇒ 一致是**应该**的（不像阶段 87–89 那样有「提交前看不见未跟踪文件」的差异） |
| 文档批提交 | **`90d1044`**（3 files changed, 252 insertions(+), 3 deletions(-)，`2026-09-23T16:50:07+08:00`）— docs(s90): 阶段 90 收口 —— 专题 §90 + `project-status.md` §90 + 登记表 #61h 改闭环；本批次**只改 `docs/`** |
| 文档批后（gate3，**绿**） | `.workbuddy/tmp/s90-gate3.log`，mtime **16:52:28** ⇒ **晚于文档批 2 分 21 秒** ✓；`EXIT=0` / 7 步全部通过 / **0 警告 0 错误**（第 3 步是「警告即失败」）；路径卫生 **649**；大队 **3167** |
| 对拍 gate2 vs gate3 | **实质差异 0**（811 / 812 行；剥墙钟后做**多重集**差集）。唯一差异是 dotnet 那句「总共 1 个测试文件与指定模式相匹配。」的**分行方式** —— gate2 在同一行里连写两遍、gate3 分成两行 ⇒ **内容多重集完全一致**。大队 **3167 不动**、8 个程序集**逐台不动**（本批次只改 `docs/`，一行代码没碰）|
| 计数 | 大队八项之和 3165 → **3167**（**+2** ＝ 本阶段新判据 2 条）。**逐程序集对拍**（`.workbuddy/tmp/s89-gate3.log` 对 `.workbuddy/tmp/s90-gate1b.log`）：`App.UiTests` 477 → **479**，**其余 7 项一字不动** ⇒ **纯净增** |
| `UnusedBaseline` | **1120 → 1116**（删掉 4 个只靠 M6 担保的常量）。⚠ 「删 5 键却只带走 4 常量」的理由见 90.3 |
| 第四次门禁（gate4） | 回填批提交后补跑；**只记在 `.workbuddy/memory/2026-09-23.md`**、**不回写本专题**（与阶段 85–89 一致）。⚠ 本行**刻意不含 gate4 读数**（写下它的时候还没跑）⇒ 读数以日志为准；若与 gate3 不一致，日志里另有小节 |

## 90.9 未闭环

1. **`#61h` 本身已闭环**，`#61` 家族里仍开着的是 `#61a`（其余 A 类产出方的「返回值带码」）、
   `#61f`（同上）、`#61i`（`EXPRESSION_SYNTAX` 的第二实参仍是中文）、`#61j`（`CopyAll` 的括号与枚举名、
   两个品牌字面量）、`#61k`（产物注释自身中英不一致）、`#61l`（`DANGLING_REFERENCE` 的外来定位串）。
2. **新判据的诚实边界**：
   - ① 它断的是**渲染结果**（路径出现次数 + 两语言各归各），**断不了**产出方将来会不会
     改成"带实参的短句" —— 那种改动**会**被它抓住（路径会变 2 次），但如果哪天码化短句
     **换了别的词而仍不带路径**，它照样绿（**这是对的**：那时候前提确实还没破）。
   - ② 它只覆盖**走三实参出口**的渲染；**直通面**上那些 `Format(键, ex.Message)` 站点
     （现 15 个）本就不在生产出口上，那份账由面判据管。
   - ③ `PathOccurrences` 数的是**非重叠**出现次数 ⇒ 路径是自身子串的退化情形
     （如 `/a/a`）理论上会少数；实测的临时目录名带随机后缀，不构成现实风险，
     但**这是判据的不完备的明写**。
3. **`status.verify.failed` / `audit.verify.code.failed` 那 2 站仍无目标路径**（内存操作为主）
   —— 维持"不在本项内"的原判断；它们要的是**另一种**处置（不是"带上对象"）。


# 阶段 91 · #61i 落地（解析器的语法错误改「码 + 回落 + 实参」；`Args[1]` 装**记录体**是本仓库**第一处嵌套实参**）

## 91.1 一句话结论

`#61i` 的处置方向**没被推翻**（码化 + 中文回落 + 实参），但**登记里的数字全错**：它写的是
「`ExpressionEngine` 抛的**三条**中文 `FormatException.Message`」，而逐处读代码量出来的是
**4 处抛错 ＋ 2 档新失败**：

| # | 抛错点 | 真实形状 | 界面上的症状 |
|---|---|---|---|
| ① | 三元缺 `:` | `FormatException("三元运算缺少 ':'")` | 中文裸串 ⇒ 英文界面下**中英混排** |
| ② | 括号未闭合 | `FormatException("缺少右括号 ')'")` | 同上 |
| ③ | 不认识的符号 | `FormatException($"意外的符号: {…} (位置 {…})")` | 同上（这档**带两个实参**） |
| ④ | **空表达式**（本阶段新量出） | `ArgumentNullException(nameof(expression))` | **.NET 英文**（`Value cannot be null. (Parameter 'expression')`） |
| ⑤ | **数字字面量形状不对**（新量出） | `double.Parse` 抛 `FormatException`（.NET 英文 `The input string '1.2.3' was not in a correct format.`） | 中英两个界面下**都是英文** |
| ⑥ | **输入结束**（接线时才量出） | tokenizer 把**未知字符**也降级成 `TokenType.EOF` ⇒ 与 ③ 从记号类型上分不开 | 合成一档会显示「意外的符号:  (位置 4)」（实参是空串） |

⇒ 码表是 **7 个码 / 7 个输入**（6 个可产出 ＋ 1 个兜底）。另有两件登记没数到的事：**第二个露出点**
`PropertyPanelView.Binding.cs`（属性面板里直显的红字）、以及**登记自己点名的第二件事**
（那几条回落被 `DevMessage` 豁免挡在 Core 台账之外）—— 两件都一并处置。

## 91.2 量的那一遍（读，不预设）

| 量的对象 | 读数 | 为什么重要 |
|---|---|---|
| `ExpressionEngine` 的 `throw` | **4 处**（登记写 3 处） | 登记是照**症状**抄的，不是照**代码**抄的 |
| 空表达式的可达性 | `GeneratedCodeValidator` 写的是 `binding.Expression ?? ""` ⇒ 「Kind = Expression 但表达式为空」的工程**真的**会走到 | 所以它不是"防御性代码"，是**第 4 档**（属性面板那条路有前置判空，走不到） |
| `Parser.Current` | `Tokenize` 末尾**无条件追加** `EOF` 哨兵 ⇒ 不会越界 | ⇒ 兜底码 `GENERIC` 是**真死码**：进 `NotProducible` 双向表，**不删**（防线要留） |
| tokenizer 的四个读取方法 | **一个 `throw` 都没有** | 这是兜底码"结构上到不了"的静态前提 |
| 露出点 | **3 处**：`GeneratedCodeValidator`（finding 的 `Args[1]`）／`PropertyPanelView.Binding`（**登记没数到**）／`CodeGenerator.Screen`（写进**产物注释**） | 第 3 处**必须保留中文回落** —— 那句注释的读者是看 C 代码的人、且产物要可复现，与 `GENERATOR_WARNING_COMMENT` 同一类"判过的不做"（#61k） |
| Core 台账 | 这几条回落**不在** `CoreVisibleTextInventoryGuardTests` 的台账里 —— 被 `DevMessage`（`throw new`）挡着 | **#54 同病**：豁免表里的理由没人去代码里核，它就成了**能把缺陷合法化的字段** |

⚠ **本族与 `LuaSyntaxValidator` 有一处关键差别，别照搬那套**：那个产出方拿到的只有 MoonSharp 的
**字符串** ⇒ 只能 `String.Contains` 反推码（脆弱，被版本差异咬过）。本族的产出方是**我们自己的
递归下降解析器**，它**当场就知道**是哪条规则失败的 ⇒ **码在抛错点直接给出，不做字符串反推**。

## 91.3 改了什么

| 层 | 改动 | 关键点 |
|---|---|---|
| Core（新 3 文件） | `ExpressionSyntaxIssueCodes`（**7 码**）／`ExpressionSyntaxError`（`Code` ＋ `Message`（中文回落）＋ `Args`；3 个静态成员 ＋ 4 个工厂）／`ExpressionSyntaxException`（`: base(error.Message)` ＋ `Error` 属性） | 照 `SvgParseError` / `SvgParseException` 的形状抄 |
| Core（`ExpressionEngine.cs`） | 4 处抛错改抛码化异常；`Compile` 的空表达式从 `ArgumentNullException` 改抛 `Empty`；`double.Parse` → `double.TryParse`（在**同处**给码，不靠捕获兜）；新增 `TryCompile(string, out ExpressionSyntaxError?)` | 消费入口是 `TryCompile`；`Compile` 抛只是因为它的返回契约是 AST |
| App（`ExternalText.cs`） | `ExprSyntaxKeyPrefix = "expr.syntax."` ＋ `ExprSyntaxKey(code)` ＋ `Describe(error)` ＋ `RenderNestedArgs(…)`（**白名单式类型分派**） | `Describe(GeneratedCodeFinding)` 改成**先渲嵌套实参再填参** |
| 站点 | ① `GeneratedCodeValidator`：`Args[1]` 装**记录体** ② `PropertyPanelView.Binding`：删掉 `?? AppLocalization.Service["msg.expr.error"]` 死分支 ③ `CodeGenerator.Screen`：**保留** `{exprError.Message}` | ③ 由判据**反向**钉住（接上 `ExternalText` ⇒ 红） |
| 字典 | 两本各 **+7**（`expr.syntax.*`）／**−1**（`msg.expr.error`）⇒ 2219 → **2225** | 删键三步照走（连 `Lang` 常量、查 `sync_localization.py` 的 `VALUE_MAP`、同步基线） |
| `Lang` | **−1** 常量（`MsgExprError`）⇒ **1775**；`UnusedBaseline` 1116 → **1115** | 分桶只有 **M6：265 → 264** 动（M2 828 / M3 18 / 无机制 5 不动） |
| 可达性 | 新增机制 **M14**（`expr.syntax.`，面 **7**） | 面与下界都不贴实测值（免得每次**正确**的新增都多红一条） |

## 91.4 判据（`ExpressionSyntaxIssueLocalizationGuardTests` **8 条** ＋ 1 条内嵌负控）

| # | 判据 | 挡什么 |
|---|---|---|
| ① | `DeclaredCodes_AndProducedCodes_AreTheSameSet` | **三态闭包**：7 个声明 = 6 个可产出 ＋ 1 个 `NotProducible`（**双向**，往哪边歪都红） |
| ② | `BothDictionaries_CarryEveryCode_AndAreActuallyPaired` | 两本字典的键集合相等、且每个码两本都在 |
| ③ | `EveryFailure_RendersTheDictionaryTemplate_NeverTheChineseFallback` | 7 个输入**真的过** `TryCompile`（不手搓记录体）＋ 两种语言下渲染 = 字典期望串 |
| ④ | `BothLanguages_RenderDistinctly_AndInTheirOwnScript` | 两语言渲染**确实不同**、且各归各的文字系统（否则是空话） |
| ⑤ | `FallbackMessages_StayUsableChinese` | 「回落自身必须可用」—— 字典缺键时也不能给用户一串码名 |
| ⑥ | `GenericArm_IsStillLiveAndStillCarriesTheRawMessage` | 兜底臂**还在**（防线没被删）且**原文当实参**保留（信息不丢） |
| ⑦ | `ExpressionSyntaxFinding_RendersTheNestedReason_NotTheRecordDump` | **本仓库第一处嵌套实参**：`Args[1]` 是记录体；渲染里**没有** `ExpressionSyntaxError {` 转储、**没有**码名；英文侧**一个汉字都没有** |
| ⑧ | `ConsumersKeepTheCodedShape` | **六个文件的源码面**（产出方 / 码表 / 记录 / 出口 / finding 产出 / 属性面板 / 产物注释）各有坏法，且**合规写法无话可说** |
| ⑨ | `NegativeControls_EachBreakIsTheRightJudgment` | NC1–NC4 内嵌负控：把每条规则自己打破一次，必须各自点名 |

既有守卫的同步：可达性 **M14**（面 7 ＋ 下界）／`GenFindingLocalizationGuardTests` 的实参渲染改走
`RenderArg`（记录体 ⇒ `Describe`）并把主判据**钉住语言**（`SwitchLanguage("zh-CN")`）／
`CoreVisibleTextInventoryGuardTests` 新增 **6 条通道**（总数 67 → **73**）＋ `DevMessage` **口径反转**／
`UnusedBaseline` **1115**。

### 台账口径反转（登记里点名的第二件事）

| | 旧口径 | 阶段 91 |
|---|---|---|
| `DevMessage` 正则 | `throw new|Exception\(` ＋ `UserFacingException = "HmiPackageException"` 白名单，把**面向用户的回落**从豁免里捞回来 | **只认 BCL 类型**（`(?:throw new\s+|new\s+)(?:<BCL 前缀>)Exception\b`），白名单**删除** |
| 失效方向 | 自有异常类型的回落被**静默豁免**（缺陷合法化） | 自有类型**自动计入台账**（多登记 ⇒ 报红） |

⇒ 那 6 条回落**第一次**被看见。**Core 里的中文一条没多**，涨的只是"被看见"。

### 本阶段被既有守卫拦下一次（值得留档）

`HmiEditor.Core.Tests.TestAssertionStyleGuardTests.NoExactCallSubstringAssertionsOutsideAllowList`
在 gate1 里点名我**新判据的第 672 行**（同一行里既有 `Assert.Contains(`、又有含 `(` 与 `,` 的字面量
= 「精确调用文本 ＋ 字符串包含」断言的形状）⇒ 改成**先把语料算成局部变量再断言**。
⚠ 这不是"门禁噪声"：那条守卫守的是"测试怎么写"（排版漂移会假红、文本出现在注释里会假绿）
—— **新判据自己踩上去**恰好说明它还在起作用。

## 91.5 生产负控（**三重**，实测；一律写回原文、`sha256` 前后逐字节一致）

| # | 注入（都是**能编译**的坏形状） | 实测 |
|---|---|---|
| NC-A | `Args: new object[] { Truncate(expr), error!.Message }` —— 实参从**记录体**退回字符串 | 失败 **2**，点名的正是 ⑦ `…NestedReason…` 与 ⑧ `ConsumersKeepTheCodedShape` |
| NC-B | `RenderNestedArgs` **整段跳过**（提前 `return args`） | 失败 **1**，点名 ⑦ ⇒ 「少了嵌套渲染」被抓住 |
| NC-C | `throw new FormatException("缺少右括号 ')'")` —— 解析器退回裸 BCL 异常 | 失败 **8**，点名 ③ `EveryFailure_…` 与 ⑧ |

注入 sha `79abd799…` / `1afffb41…` / `ff084aa3…`；还原 sha 与注入前**逐字节一致**
（`0cda4a26c68a` / `da7248c46c44` / `8b001d8ac6ed`）；正控 **9/9 绿**。

⚠ **脚本第一次跑（09:22）暴露了两个"读数是脚本的、不是判据的"毛病**，一并记下：

1. 带 `-l console;verbosity=detailed` 时尾部是「测试运行成功。／测试总数: N／通过数: N／失败数: N」，
   我原来的正则 `失败:\s*(\d+)` **匹配不到**（`失败数` 中间隔着一个 `数`）⇒ 三重重全被读成「失败 0」
   ⇒ **差点把"三重全红"记成"三重全不符合预期"**（读数是绿的、结论是反的）。
2. NC-B 的第一版坏形状（`is ExpressionSyntaxError` 不给模式变量）**编译不过** ⇒ 那一跑的红是
   **build-fail 冒充判据红**。

⇒ 负控判红的谓词必须**同时**要求「汇总行失败数 > 0、总数 == 期望条数、取到 `错误消息:`、
且**没有** `error CSxxxx`」，否则读到的不是判据。

## 91.6 面读数与基线（本项**不在**直通面上）

| 面 | 阶段 90 | 阶段 91 |
|---|---|---|
| 直通面（站点 / 组合） | 15 / 14 | **15 / 14（零变化）** |
| `UnusedBaseline` | 1116 | **1115** |
| 分桶 | M2 828 / M6 265 / M3 18 / 无机制 5 | **M2 828 / M6 264 / M3 18 / 无机制 5** |
| 字典键（zh-CN） | 2219 | **2225** |
| 有常量的键 | 1774 | **1775** |
| 可达机制 | 13 条 | **14 条**（+ M14 面 7） |

⚠ 「`expression_syntax` 那条 finding 的第二实参是中文」**不在直通面上** —— 那张面只收
`Format(ex.Message)` 那个**形状**，而这条 finding 走的是 `Args[1]` ⇒ 这是**第四例「面读数是证词、
不是目标」**（前三例见 #61a / #61f / #61g）。本项的收益证据是：新判据读数 ＋ **三处露出点**的接线面
＋ Core 台账**通道 +6**。

**读数出口**（`HMI_READINGS_OUT`，**必须绝对路径** —— 相对路径会落在 testhost 的工作目录下、
fail-soft 地什么都没写）：`.workbuddy/tmp/s91-readings.txt`。

## 91.7 门禁

| 跑 | 读数 |
|---|---|
| gate1（**红**） | `.workbuddy/tmp/s91-gate1.log` mtime **17:26:14**，`EXIT=1`：**唯一红** = `TestAssertionStyleGuardTests.NoExactCallSubstringAssertionsOutsideAllowList`（点名 `ExpressionSyntaxIssueLocalizationGuardTests.cs:672`，见 91.4）。⚠ **不是**既有缺陷、**不是**并发红 —— 是新判据自己的写法踩了既有守卫。同一跑里 `App.UiTests 488/488` 全绿、`Core.Tests 2118/2119` |
| gate1b（绿） | mtime **17:27:59**，`EXIT=0` / 7 步全过 / **0 警告 0 错误** / 路径卫生 **649** |
| 二次修补 | `ExpressionSyntaxIssueCodes.cs` 的类注释把守卫名写错（`ExpressionSyntaxIssueGuardTests` ＝ 少了一个词）⇒ 改对；**改完必须重跑门禁**（内容改动不能拿旧跑当证词） |
| gate1c（绿，提交前最终） | mtime **17:29:26**，`EXIT=0` / 7 步全过 / 0 警告 0 错误 / 路径卫生 **649** |
| 内容批 | **`f890863`**（17 files changed, 1246 insertions(+), 60 deletions(-)，`2026-09-23T17:30:13+08:00`）⇒ gate1c 比提交**早 47 秒** ✓ |
| gate2 | mtime **17:31:32** ⇒ **晚于提交 1 分 19 秒** ✓；`EXIT=0` / 7 步全过 |
| 对拍 gate1c vs gate2 | **实质差异 1 行 ＝ 路径卫生 649 → 652**，成因可核：该步走 `git ls-files`，内容批**新增 3 个文件**（`ExpressionSyntaxIssueCodes` / `ExpressionSyntaxError` / `ExpressionSyntaxException`）⇒ 提交前未跟踪、提交后被计入。其余 **28 行逐条同数**（剥墙钟后做多重集差集，两侧差集只剩这一行） |
| 文档批 | **`b08b502`**（3 files changed, 246 insertions(+), 3 deletions(-)，`2026-09-23T17:32:50+08:00`）⇒ 本批次**只改 `docs/`** |
| gate3（文档批后，绿） | `.workbuddy/tmp/s91-gate3.log` mtime **17:34:12** ⇒ **晚于文档批 1 分 22 秒** ✓；`EXIT=0` / 7 步全过 / 0 警告 0 错误 |
| 对拍 gate2 vs gate3 | **0 行差异**（剥墙钟后多重集，29 / 29；路径卫生两跑都是 **652**）⇒ 文档批不改任何代码路径 |
| 记忆批 | **`a734346`**（2 files changed, 132 insertions(+)，`2026-09-23T17:35:37+08:00`）：快照 `docs/archive/2026-09/memory-hmi-full-2026-09-23e.md`（`MEMORY.md` 7958 字符的逐字节副本）+ 析出 §6 |
| gate4 | 回填批之后补跑；**只记在** `.workbuddy/memory/2026-09-23.md`、**不回写本文档**（与阶段 85–90 一致） |
| 计数 | 大队八项之和 3167 → **3176**（**+9** ＝ 新判据 9 条）。**逐程序集对拍**：`App.UiTests` 479 → **488**，其余 7 项一字不动 ⇒ **纯净增** |

## 91.8 未闭环

1. `#61i` **已闭环**；`#61` 家族仍开着：`#61a`（其余 A 类产出方的「返回值带码」）/ `#61f`（同上）/
   `#61j`（`CopyAll` 的括号与枚举名 ＋ 两个品牌字面量）/ `#61k`（产物注释自身中英不一致）/
   `#61l`（`DANGLING_REFERENCE` 的外来定位串）。
2. **新判据的诚实边界**：
   - ① 它断的是「渲染 = 字典期望串」与「六个文件的形状」，**断不了翻译质量**；
   - ② ⑧ 是**源码面**证据，不是端到端 UI 断言；
   - ③ `NotProducible` 那条依赖一个**静态前提**（解析器里再没有别的 `throw`）—— 将来有人在
     tokenizer 里加一个 `throw`，⑥ 会红（**那正是要的**：兜底码那时候真的可达了，该搬出这张表）；
   - ④ `UNEXPECTED_END` 与 `UNEXPECTED_SYMBOL` 的分辨靠 `Tokenize` 末尾哨兵的 **`Value` 是空串**，
     哨兵表示一变 ③ 会红（前提变了 ⇒ 该重新量，不是改断言）。
3. `CodeGenerator.Screen` 那条路**刻意不接本地化**（产物注释的读者是看 C 代码的人、产物要可复现）
   —— 这条边界由 ⑧ **反向**钉住（接上 `ExternalText` ⇒ 红）。


# 阶段 92 · #61j 落地（「生成代码验证结果」这一族的显示文案收成**唯一映射**）

## 92.1 量出来的形状（读，不预设 —— 三件，登记只写了两件）

| # | 现场（阶段 92 读到的原文） | 为什么是缺陷 |
|---|---|---|
| ① | `ValidationResultView.CopyAll` 里 `$"[{f.Severity}] {f.Category}: {…}"` | `Severity` / `Category` 的**枚举名**被硬拼进用户可见文本 ⇒ 中文界面下复制出来是 `[Error] Structure: …`，而同一个 finding 在列表里显示的是「错误 / 结构」 |
| ② | 两个 `switch` 各有一条 `_ =>`：`_ => (UiPalette.SeverityInfo, AppLocalization.Service[Lang.FindingLevelInfo])`（级别）／`_ => AppLocalization.Service[Lang.FindingCategoryGeneration]`（分类） | **默认支承担真实语义** —— `Info` 与 `CodeGen` 这两档今天不是显式档，是靠 `_ =>` 活着；新增枚举取值会**静默落进**它 |
| ③ | `FindingCategory.Faceplate => "Faceplate"` / `FindingCategory.LvglApi => "LVGL API"` | 两个品牌字面量（**判过：不该翻**），但**没有任何判据**钉住这个「不翻」 |

补充读数（同一次量的）：`finding.category.*` 前缀下**只有 6 个键**（structure / binding / expression /
dangling / runtime.user / generation）—— **没有** faceplate / lvgl，而这是**对的**（品牌名不进字典）。
即 ③ 的正确处置**不是**补两个键，而是给「没有键」这件事立判据。

## 92.2 落地（四处改动 ＋ 一处顺带）

| 文件 | 改动 | 读数 |
|---|---|---|
| `src/HmiEditor.App/Services/FindingDisplayText.cs`（**新**） | `SeverityText(FindingSeverity)` / `CategoryText(FindingCategory)`（两个 `switch` **无默认支**）＋ 两个品牌常量 ＋ `CopyLine(GeneratedCodeFinding)` | 3163 字符（含 pragma 与注释后 3907） |
| `src/HmiEditor.App/Views/ValidationResultView.axaml.cs` | 徽章文案 / 分类列 / `CopyAll` **三处**改调映射；`SeverityColor` 单独留（无语言维度） ＋ 该文件也补 pragma | 5276 → **4520**（−756，三处独立实现整体消失） |
| `src/HmiEditor.Core/Resources/Strings.{zh-CN,en-US}.json` | 各 **+1** 键 `finding.copy.line`（**逐行手术**插入，不 `json.dump` 重写 —— 那会产出上千行 diff） | 87473 → 87520 / 117835 → 117882（各 +47） |
| `src/HmiEditor.App/Resources/Lang.cs` | **+1** 常量 `FindingCopyLine` | 133949 → 134100 |
| （顺带） | `AppLocalization.Service["gen.error"]` / `["gen.warning"]` → **早已存在却没人读**的 `Lang.GenError` / `Lang.GenWarning` | 这两个常量离开 `UnusedBaseline`：1115 → **1113**（见 92.6） |

复制行模板 `finding.copy.line` = `[{0}] {1}: {2} ({3})`，**两本刻意同值**：它是**结构模板**（方括号/冒号/圆括号），
翻的是**实参**（级别 / 分类 / 描述 / 定位）。⚠ 因此它**不能**进
`CsvImportIssueLocalizationGuardTests.PairingViolations` —— 那条共享规则里有一条「两本字典的值一字不差 ⇒
至少有一本没翻」，对结构模板是**假阳性**（阶段 92 实测：全字典本来就有 **38 个**同值键，
`ai.api.key = 'API Key'` / `data.type.bool = 'Bool'` / `cb.resolution.1024x600 = '1024x600'` 这一类）。
⇒ 本键的成对性由判据 ⑤ **双向**单独钉（缺键红、两本不同值也红）。

## 92.3 🩸 实测推翻预设：枚举 `switch` 的两档警告码必须分清（本阶段最值得记的一条）

**预设**：「不给默认支 ⇒ 新增枚举取值时编译器报 `CS8509` ⇒ 门禁红」。首写落地后**编译立刻冒出 3 条警告**，
且是 `CS8524` 不是 `CS8509` ⇒ 当场做探针（`.workbuddy/tmp/enumprobe/`，三值枚举 `Sev`）：

| 探针里的形状 | 警告码 | 消息里的举例 |
|---|---|---|
| 3/3 全覆盖、**无**默认支 | **CS8524** | 模式 `(Sev)3` ＝ **未命名值** |
| 2/3 缺一个、**无**默认支 | **CS8509** | 模式 `Sev.Info` ＝ **具名成员** |

⇒ CS8524 是**固定噪声**（枚举永远可能持有一个未声明的值，全覆盖也照报），CS8509 才是信号。
而门禁判红的口径是 `ci-gate.sh:198` 的 `grep -E ": (warning|警告) "`（**按行文本**判，与 `-warnaserror` 无关）：

| 做法 | 今天 | 将来新增具名取值 | 范围 |
|---|---|---|---|
| 给默认支 | 0 警告 | **静默**（用户看到假标签）| —— |
| 项目级 `NoWarn=CS8524` | 0 警告 | CS8509 仍在 | **全仓**（会把别处的 CS8509 也一起埋掉）|
| **文件级 `#pragma warning disable CS8524`**（采用） | **0 警告** | **CS8509 ⇒ 门禁红** | 文件级 |

⚠ 这是本仓库**第一处 `#pragma warning`**（`grep -rn "#pragma warning" src/ tests/` 此前零命中）⇒
抑制面**本身**也要有判据，否则它会变成一条静默的豁免：判据 ⑥ 要求「被抑制的码**恰好是 CS8524**」，
判据 ⑦ 要求「工程文件里不许出现 CS8509 / CS8524」（抗「升格成 `NoWarn`」）。

## 92.4 判据与各自挡什么（`FindingDisplayTextGuardTests` 8 条 ＋ 1 条内嵌负控）

| # | 判据 | 挡什么 |
|---|---|---|
| ① | `CopyLine_IsBuiltFromTheSameTextsAsTheUi_NotFromEnumNames` | 行 == 模板填四实参（**顺序也钉**）；开头就是界面上那两份文案；中文侧**零枚举名**；毒化 `Message` 后行**一字不变**（走字典渲染而不是 `finding.Message`）|
| ② | `EveryEnumValue_HasItsOwnDistinctText` | 非空 ＋ **两两不同** —— 「默认支承担真实语义」的可判形状，且**独立于编译器** |
| ③ | `BrandCategories_AreLanguageNeutral_AndAbsentFromTheDictionaries` | 品牌档不随语言变 ＋ 字典里没有它的键（**按值判，不猜键名**）＋ 键数恰好 = 枚举数 − 品牌数；非品牌 6 档**必须**随语言变 |
| ④ | `BothLanguages_RenderInTheirOwnScript` | zh 有汉字 / en 无汉字；非品牌档 zh ≠ en |
| ⑤ | `CopyLineTemplate_IsOneSkeleton_WithFourSlots` | 两本同值 ＋ 槽位恰好 `{0}…{3}` ＋ 模板**真的被映射引用**（找标识符，注释已剥 —— 登记 #48）|
| ⑥ | `MappingStaysSingleSourced` | 映射唯一（签名全语料恰 1 处）＋ 视图不再手拼 ＋ 两个 `switch` 无默认支 ＋ 抑制面只许 CS8524 |
| ⑦ | `Suppression_StaysNarrow_NeverSilencesCs8509` | `.csproj` / `.props` 里不许出现 CS8509 / CS8524（防项目级 `NoWarn`）|
| ⑧ | `NegativeControls_EachBreakIsCaughtByTheRightJudgment` | 5 组内嵌负控（枚举面 / 字典面 / 模板面 / 源码面 / **语料缺文件的下界**），出入参**全部合成** |

⚠ **⑧ 第一次写法定错了一次**（已改）：它的正控原来取**磁盘上的真语料 / 真字典 / 真枚举**，于是三重**外部**
负控会让它**跟着红** ⇒ 读数分不清「判据坏了」与「仓库被注入了」。改成**合成**出入参之后，外部负控的红集
才收敛成 92.5 里那种「恰好一条」的形状。真语料那一侧由 ①–⑦ 各自的 `[Fact]` 负责。

## 92.5 负控矩阵（判据级三重 ＋ 编译级一重）

| # | 注入（**能编译**的坏形状） | 编译 | 红集 | 还原 |
|---|---|---|---|---|
| NC-A | `CopyAll` 退回 `_findings.Select(f => $"[{f.Severity}] {f.Category}: {f.Message} ({f.Location})")` | 0 警告 0 错误 | **1**：⑥（点名 `f.Severity}]` ＋ 缺 `_findings.Select(FindingDisplayText.CopyLine)`）| 原文 sha256 `f86e2c3dca7f2259…`（4781 字符） |
| NC-B | 两本字典各补一行 `"finding.category.faceplate": "Faceplate",` | 0 / 0 | **1**：③（「品牌档被翻」）| zh 87520 → 87565 / en 117882 → 117927（各 +45）；en sha256 `2db8787142ecd16b…` |
| NC-C | 新增 `FindingSeverity.Fatal` **且**两个 `switch` 补 `_ =>`（把编译器哄好）| **0 警告** | **2**：② ＋ ⑥ | 映射 sha256 `dff185c29711d8e3…`（3907 字符）|
| NC-D | 只新增 `FindingSeverity.Fatal`（不给默认支）| **CS8509 ×2**（`FindingDisplayText.cs` ＋ `ValidationResultView.axaml.cs` 各一条，点名 `Fatal`）；**CS8524 = 0 行** | —— | 枚举原文 sha256 `0cda4a26c68af6a0…`（21429 字符），注入后 `acf5868c0cd75a39…` |

⇒ NC-C 与 NC-D 合起来才是完整证据：**编译器守「具名取值没接」、判据守「默认支吞掉了它」**，
两段各有一段覆盖、且 NC-C 证明「编译器被哄好之后 ② 仍能拦住」。
还原一律**写回原文**（刻意不用 `shutil.copy2`：mtime 一起复制会让 MSBuild 不重建 ⇒ 正控跑的是负控的二进制），
`sha256` 逐字节核对通过；负控脚本 `.workbuddy/tmp/s92-nc-tests.py` ＋ 运行器 `.workbuddy/tmp/s92-nc-run.sh`。

## 92.6 读数与对拍（`HMI_READINGS_OUT` **绝对路径**）

`.workbuddy/tmp/s92-readings-gate1b.txt` 落盘实测：

| 读数 | 阶段 91 | 阶段 92 |
|---|---|---|
| 无代码引用的常量 | 1115 | **1113** |
| 分桶 | M2 828 / M6 264 / M3 18 / 无机制 5 | **M2 828 / M6 262 / M3 18 / 无机制 5** |
| 有常量的键 / 没有常量的键 | 1775 / 450 | **1776 / 450** |
| 字典键 | 2225 | **2226** |

⚠ **顺带归位**：`LangConstantUsageGuardTests` 的类注释与基线注释里那两处分桶读数停在**阶段 89 的 M6 269**
（阶段 90/91/92 三次改动都没跟着改）—— 本阶段改成 262 并写明来由。这条与本项无关，但同属
「文档里的数字要能指到一条日志」这条纪律。

### 92.6b 读数对拍（gate1b ↔ gate2）与一处**既有**读数噪声

`.workbuddy/tmp/s92-diffreadings.py`（在同一条口径上再剥掉「驱动 N 轮 / N ms」）：两跑各 **790 行**，
多重集差集 **42 / 42**，逐条看**全部落在三类随机夹具量**上 —— ① 随机数据根名与随机 8 位十六进制前缀
（`ZZF80…`）；② `TestTempDir` 的临时路径；③ 包内 mtime 与 `GeneratedMSBuildEditorConfig` 的 mtime。

其中唯一「看起来像产品数字」的一处必须定性，不能当噪声放过去：

| 读数 | gate1b | gate2 | 文档里记的规范值 |
|---|---|---|---|
| `[S1] 原包 … 字符` | 10865 | **10866** | 10866（`…open-save-net.md` §4） |
| `[S1] … → S1 … 字符` | 25835 | **25836** | 25836 |
| `[保存] 文件 … → … B` | 3439 → 4396 | **3432 → 4380** | —— |

**归因（代码实证，不是猜）**：`HmiProject.CreatedAt` / `LastModifiedAt` 的默认值都是
`DateTimeOffset.Now`（`src/HmiEditor.Core/Models/HmiProject.cs:30,33`），而
`System.Text.Json` 序列化 `DateTimeOffset` 时**小数秒的尾零会被裁掉**（`.12` 与 `.1234567` 长度不同）
⇒ 同一份夹具序列化出来**长度天然随手跑时刻漂移**。

⇒ 这是**既有的夹具噪声**，与本项无关：gate1b 与 gate2 跑的是**同一份代码**（两跑都含 #61j 的改动）。
它只出现在**打印读数**里 —— 任何断言都不读它：幂等判据读的是**同一次运行内**的 `S1 == S2`（两跑都过，
`逐字节相同=True`）。登记下来的用处是给对拍工具加一类「该剥的量」：**除了墙钟，还要剥时间戳派生的数字**。

## 92.7 门禁

| 跑 | 读数 |
|---|---|
| gate1（**红**） | `.workbuddy/tmp/s92-gate1.log` mtime **18:24:37**，`EXIT=1`：**唯一红** = `LangConstantUsageGuardTests.UnusedLangConstants_MatchBaseline`（「1113 ≠ 基线 1115 … 请把 `UnusedBaseline` 改成 1113」）。⚠ 这条是**正面证据**（两个常量终于有人读了），不是噪声 |
| 修法 | 基线 → 1113 ＋ 追加阶段 92 段 ＋ 归位两处陈旧读数 |
| gate1b（绿） | mtime **18:26:50**，`EXIT=0` / 7 步全过 / **0 警告 0 错误** / 路径卫生 **652** / 大队八项 **3184** |
| 内容批 | **`53febee`**（7 files changed, 902 insertions(+), 29 deletions(-)，`2026-09-23T18:27:13+08:00`）⇒ gate1b 比提交**早 23 秒** ✓ |
| gate2（绿） | mtime **18:29:31**，`EXIT=0` / 7 步全过 / **0 警告 0 错误** / 路径卫生 **653** / 大队八项 **3184** |
| 对拍（gate1b ↔ gate2） | `.workbuddy/tmp/s91-diffgate.py`（剥「持续时间」多重集）：**只差 1 行** = 路径卫生 **652 → 653**。原因可核 —— 口径是 `git ls-files 'src/*' 'tools/*' 'scripts/*' 'firmware/*'`（`scripts/check-path-hygiene.sh:37`），而 gate1b 跑在内容批**之前**、`FindingDisplayText.cs` 当时还是**未跟踪**文件 ⇒ 提交后进清单 ⇒ +1。**不是回归** |
| 文档批 | **`f3788a4`**（3 files changed, 281 insertions(+), 2 deletions(-)，`2026-09-23T18:33:05+08:00`）—— 本批次**只改 `docs/`**（本专题 §92 ＋ `project-status` §92 ＋ 登记表 #61j 闭环）|
| gate3（文档批后，绿） | `.workbuddy/tmp/s92-gate3.log` mtime **18:34:46** ⇒ **晚于文档批 1 分 41 秒** ✓；7 步全过 / **0 警告 0 错误** / 路径卫生 **653** / 大队八项 **3184** |
| 对拍（gate2 ↔ gate3） | **0 行差异**（剥墙钟多重集，29 / 29）⇒ 文档批不动任何代码路径 |
| 记忆批 | **`fe6d714`**（2 files changed, 152 insertions(+)，`2026-09-23T18:37:57+08:00`）：快照 `docs/archive/2026-09/memory-hmi-full-2026-09-23f.md`（`MEMORY.md` 的逐字节副本，`sha256 3ab0b72d175d`）＋ 析出文档 §7 |
| 回填批 / gate4 | 回填批（本批次，只改 `docs/`）之后补跑 **gate4**；按阶段 85–91 的惯例，gate4 **只记** `.workbuddy/memory/2026-09-23.md`、**不回写本文档** |
| 计数 | 大队八项之和 3176 → **3184**（**+8** ＝ 新判据 8 条）。**逐程序集**：`App.UiTests` 488 → **496**，其余 7 项**一字不动** ⇒ **纯净增** |

## 92.8 诚实边界与未闭环

1. `#61j` **已闭环**；`#61` 家族仍开着 `#61a` / `#61f` / `#61k` / `#61l`。
2. 判据断的是「渲染 = 字典模板 ＋ 源码面形状」，**断不了翻译质量**；「视图里不许再出现第二处映射」
   只钉了 `FindingCategory.` 与手拼形状 —— 用**裸中文字面量**再拼一遍那种写法由既有的
   `HardcodedLocalizationGuardTests` 兜着（本文件刻意**不重复**那道墙）；剪贴板那一步不在判据面上。
3. **颜色映射留在视图**（`SeverityColor`）：它没有语言维度、不属于本项缺陷族；但它同样无默认支 ＋
   只抑制 CS8524，由 ⑥ 一起守。
4. 本项**不在**直通面上（那张面只收 `Format(ex.Message)`）⇒ 与 #61a / #61f / #61g / #61i 同属
   「**面读数是证词、不是目标**」（**第五例**）；收益证据 = 新判据读数 ＋ 视图接线面（−756 字符）
   ＋ 基线 −2。

## 93.1 量出来的形状（读，不预设 —— 登记里的两处表述被推翻）

登记 #61l 的原话有两处需要更正，都是**实测**推翻的：

| # | 登记里的表述 | 实测（阶段 93 逐站点读现场代码） |
|---|---|---|
| ① | 「**同一串在两个地方露出**：交叉引用面板（`{Binding Description}`）＋ 验证对话框」 | **不成立**。两个族是**两套独立措辞**：悬空族 `[屏名] 控件 '名' (属性)`（进 `GeneratedCodeFinding.Location`）／面板族 `控件 名 绑定变量 (方向)`（`ReferenceLocation.Description`）。**面板上根本没有定位串这条文本** |
| ② | 「两个消费点必须**一起收口**，否则同一个串会在两个界面上各翻一半」 | 真消费点只有**一个**：生成代码验证结果对话框。另两处站点是 `MainWindow.axaml.cs:1066,1213`（只取 `.Count` 拼状态栏文案）与 `:1252`（`Debug.WriteLine`，仅在 DEBUG 编译） |
| ③ | 「吞它等于把 `CrossReferenceAnalyzer` 的定位格式一起改」 | 站点确实在 `CrossReferenceAnalyzer`，但只需改那 **4 个 `result.Add` 站点**；`Description` 那 **9 条一个字都没动** —— 两个族在同一个文件里，却是**两条互不相干的出口** |

⇒ 结论：**不必等「两个界面一起收口」**，把码交出来就行。这也解释了本项为什么在阶段 89
被「刻意不吞」而搁置两轮 —— 当时的顾虑建立在一个不成立的前提上。
（⚠ 这正是本项目那条纪律的又一例：**登记写的是假设，读出来的才是形状**。）

量的那一遍**顺带**产出第三件事（**登记里没有**）：

`HmiAction.GetDescription()`（`src/HmiEditor.Core/Models/HmiAction.cs:195`，**13 条中文**）被
**两个方向**共用，而两个方向的语言要求**相反**：

| 方向 | 站点 | 要求 |
|---|---|---|
| 交叉引用面板 | `CrossReferenceAnalyzer.cs:150,167` ⇒ `ReferenceLocation.Description = $"动作 {action.GetDescription()}"` ⇒ `{Binding Description}` | **必须翻**（用户可见） |
| 产物注释 | `CodeGenerator.Actions.cs:57,64,73` ⇒ `Cmt(sb, $"/* {control.Name}: {action.GetDescription()} (press) */")` | **必须固定中文**（产物可复现，与 #61c / #61k 同类的「不迁」） |

⇒ 这才是**真的**「两个方向必须一起收口」，已**新开 #61m**（本刀刻意不碰 —— 它要动的是
`HmiAction` 的**族级出口拆分**，与定位串不是一件事）。

## 93.2 落地（6 处改动）

| 文件 | 改动 | 读数 |
|---|---|---|
| `src/HmiEditor.Core/Services/DanglingRefLocation.cs`（**新**） | 记录体「**定位码 ＋ 中文回落原文 ＋ 实参**」＋ 3 个工厂方法 `OfBinding` / `OfLegacyBinding` / `OfAction`。三段式与 `SvgParseError` / `ExpressionSyntaxError` / `CsvImportIssue` 同形 | 3 条中文字面量（回落原文**逐字**抄改动前的 4 个站点；第 4 个站点是同一文案的第二个站点） |
| `src/HmiEditor.Core/Services/GeneratedCodeIssueCodes.cs` | `FindingLocationCodes` 新增 `SCREEN_CONTROL_ACTION`（含**立档理由**：实参是数据、「动作」是**键自身的固定词**）；类注释「只有 4 种」→ **5 种** | +16 / −4 行（注释占多数） |
| `src/HmiEditor.Core/Services/CrossReferenceAnalyzer.cs` | `FindDanglingReferences` 返回 `List<(DanglingRefLocation Location, string BindingId)>`；4 个拼串站点改调 3 个工厂方法 | 含汉字的行 **76 → 72** |
| `src/HmiEditor.Core/Services/GeneratedCodeValidator.cs` | `ValidateDanglingReferences` 把 `loc.Code` / `loc.Args` 接进 `LocationCode` / `LocationArgs`（`loc.Message` 仍进 `Location` 作回落）；⚠ **顺带**改正一处文档漂移 | 485 → 488 行 |
| `src/HmiEditor.App/MainWindow.axaml.cs` | `Debug.WriteLine` 改走 `loc.Message` | 3013 → **3014** |
| 两本字典 | 各 **+1** 键 `gen.loc.screen_control_action`（zh `屏幕 {0} / 控件 {1} / 动作` / en `Screen {0} / Control {1} / Action`）—— **逐行手术**插入，不 `json.dump` 重写 | 各 +1 行；**2226 → 2227** |

**两处「为什么这么写」**：

1. **调试行为什么走回落原文而不是 `ExternalText`**：`Debug.WriteLine` 只在 DEBUG 编译，
   是给读日志的人看的诊断行，**不该随界面语言变**（兄弟行 `[Integrity]` / `[AlarmGroup]` 也仍是中文）；
   而 `loc.Message` 与阶段 93 之前这一行打印的东西**逐字相同** ⇒ 「用户所见」与「日志所见」两边都没变。
   ⚠ `MainWindow.axaml.cs` 有 **3013 行**的硬基线（`SingleFileSizeGuardTests`，相等判定）⇒
   第一版写了 4 行注释把文件顶到 3017（**gate1 红**），收口时压成 **1 行**、基线同步到 **3014**。
   涨的这 1 行是**注释**不是代码，且本表的纪律是「涨了 = 先把文件改小」⇒ 已在基线注释里写明来由。
2. **`SCREEN_CONTROL_ACTION` 为什么单立一档而不是复用 `SCREEN_CONTROL_PROPERTY`**：
   第三格装的是**位置名** —— 属性名是用户数据，而「动作」这个词自己要被翻译
   ⇒ 它必须落在**键的固定词**里。塞进 `SCREEN_CONTROL_PROPERTY` 的话，得再引入一层
   「嵌套实参也能翻」的机制去处理它（重复发明 `RenderNestedArgs` 那一套）。

## 93.3 判据：**删掉一整档** ＋ 立一条钉住新形状的

### 93.3a 删掉的是「外来定位」这一档（本项最值得记的一条）

阶段 89 给定位分了三档，其中第三档「**外来定位**」由一张 `ForeignLocation` 表 ＋
`ForeignLocationViolations` 双向判定 ＋ 一条 `[Fact]` ＋ 负控 NC4 一起守着，理由是
「串是别的模块拼的，本判据不断它的语言」。

#61l 闭环之后**这一档空了**。而**空表 ＋ 恒真判定**就是一条**没有负控的判据** ——
按本项目纪律**不发布**；留着它更糟：它会变成「把定位没码化合法化」的永久字段（登记 #54 的老病）。
⇒ 五件东西一起删：表 / 双向判定 / `[Fact]` / NC4 / `EveryLocation` 里的放行支。
`ForeignLocation` 这个名字只在墓碑注释里出现（**不写 `<c>` 引用**，避免「名字即引用」自满足）。

`EveryLocation_RendersTheDictionaryTemplate_OrIsLanguageNeutral` 因此变成**两档闭合**：
有码 ⇒ 必须渲染成字典模板；空码 ⇒ 必须直出且**不许含汉字**。没有第三种可能。

### 93.3b 新立的判据

| # | 判据 | 挡什么 |
|---|---|---|
| ① | `DanglingRefs_CarryACodedLocation_AndKeepTheOldFallbackFormat` | `DANGLING_REFERENCE` 的 `LocationCode` **非空** ＋ 落在悬空族那三档内 ＋ 实参个数与档位匹配 ＋ `Location`（回落原文）仍是**改动前的格式**（正则 `^\[…\] 控件 '…'( \(…\)\| 动作)?$`） |
| ② | 纯函数 `DanglingLocationViolations(IEnumerable<GeneratedCodeFinding>)` | ① 用的判定本体；负控直接调它（与 #61f / #61g 的「判据逻辑拆成纯函数」同形） |
| ③ | `WiringViolations` 新增 `LocationProducer` 档 | 源码面：`DanglingRefLocation.cs` 必须真的发那 3 个定位码；`GeneratedCodeValidator.cs` 必须真的写 `loc.Code` |

**① 为什么非有不可（本阶段判据设计上最关键的一点）**：这条定位的码**不由
`GeneratedCodeValidator` 自己拼**（来自 `DanglingRefLocation` 的三个工厂方法）⇒
`WiringViolations` 里那个 `FindingLocationCodes.` **文本计数对它看不见** ——
把 `loc.Code` 改回 `""`，那个计数**一个都不会掉**。少了 ①，这件事就只剩
「`SCREEN_CONTROL_ACTION` 变成死码」这一条**顺带**的间接信号（而 `SCREEN_CONTROL_PROPERTY` 因为
`EXPRESSION_SYNTAX` 也在用、`SCREEN_CONTROL` 因为 Faceplate 三档也在用，都**不会**掉）。
⇒ **判据的输入面必须落在行为上**，不能只靠「另一个判据会不会顺带发现」。

### 93.3c 夹具：四个站点必须**全部**铺满

`RealFindings()` 的 ④ 原来只造一个站点（联动模型的 `VariableBindings`）。**必须**铺满四个：

| 站点 | 走的工厂方法 | 产出的定位码 |
|---|---|---|
| `control.VariableBindings[].VariableId` 悬空 | `OfBinding` | `SCREEN_CONTROL_PROPERTY`（3 实参） |
| `control.VariableBindingId` 悬空（回退旧字段，且 `VariableBindings` 为空） | `OfLegacyBinding` | `SCREEN_CONTROL`（2 实参） |
| `control.Actions[].Target` 悬空（`ActionType` 属 6 个会写变量的取值） | `OfAction` | `SCREEN_CONTROL_ACTION`（2 实参） |
| `control.Action.Target` 悬空（回退旧字段，且 `Actions` 为空） | `OfAction` | 同上 |

不铺满会怎样：`DeclaredCodes_AndProducedCodes_AreTheSameSet` 的**定位码**闭包只认「真实产出的码」
⇒ `SCREEN_CONTROL` / `SCREEN_CONTROL_ACTION` 会被判成**死码**（那是判据在**正确报红**，
但读数会误导成「新码建了没发」）。⇒ 已在夹具处写明这条因果。

### 93.3d 台账（`CoreVisibleTextInventoryGuardTests`）—— 三处同步

| 位置 | 改动 | 为什么 |
|---|---|---|
| 通道 ② `Services/CrossReferenceAnalyzer.cs` | **13 → 9** | 4 条定位串搬走了（其中 3 条是去重后的文案） |
| **新通道** `Services/DanglingRefLocation.cs` | **3** ⇒ 消费方 `App/Services/ExternalText.cs`，标记 `DescribeLocation(GeneratedCodeFinding` | 与 `SvgParseError` / `CsvVariableImporter` / `ExpressionSyntaxError` 同形（回落原文经出口那一层） |
| `VisibleChannels_CountIsWhatTheDocSays` | **73 → 72** | 净 −1 = 搬走 4、落新 3 |

⚠ **这一条是本阶段被门禁拦了两次的地方，值得留档**：

1. 标记第一版写成 `DescribeLocation(finding)` —— 而 `ExternalText.cs` 里的签名是
   `DescribeLocation(GeneratedCodeFinding finding)` ⇒ **子串根本不存在** ⇒
   `VisibleChannels_EvidenceStillExists` **红**。修法是改成 `DescribeLocation(GeneratedCodeFinding`
   （含参数类型、对改名稳健）。**教训**：通道标记是**子串匹配**，必须拿**真实签名**核，
   不能照自己文档里的写法抄一遍。
2. 顺带归位：类注释里那句「**已核实**会显示在编辑器界面上的 **6 条通道（61 条文案）**」停在
   **阶段 44 定版那一刻** —— 之后阶段 87 / 88 / 89 / 91 / 93 各自新增过通道，**五次都没回头改**。
   已归位成 **10 条 / 72 条**（与 `VisibleChannels` 逐条相加一致）。
   这条与本项缺陷无关，同属「文档里的数字要能指到一条日志」这条纪律
   （阶段 92 归位的是 `LangConstantUsageGuardTests` 里停在阶段 89 的 **M6 269**，同款）。

## 93.4 负控（判据级 —— 交付 NC4 四支 ＋ NC5 新增两支）

| # | 注入（能编译的坏形状） | 红集 | 点名 |
|---|---|---|---|
| NC4-① | 把 `LocationCode = ""` / `LocationArgs = null`（**本刀之前的样子**） | `DanglingLocationViolations` | 「LocationCode 为空 ⇒ …中英混排」 |
| NC4-② | 换一个**不属于本族**的码（`GENERATED_CODE`） | 同上 | 「不在悬空族内」 |
| NC4-③ | 实参个数与档位不匹配（3 实参档位只给 2 个） | 同上 | 「模板要 3 个」 |
| NC4-④ | 回落原文改掉格式（`Location = "main.value"`） | 同上 | 「回落原文不再保留旧格式」 |
| NC5-③b | 定位产出方被掏空（`DanglingRefLocation.cs` 三个码一个都不发） | `WiringViolations` | 三个码名各点名 |
| NC5-③c | 调用方不再接线（`loc.Code` 丢掉） | `WiringViolations` | 「没把记录体的码接进 LocationCode」 |

⚠ **NC4-③ 的取样点第一次写法不牢**（已改）：原来取「第一条悬空 finding」，而它是 3 实参档还是
2 实参档**取决于产出顺序** ⇒ 若落到 2 实参档位，塞 2 个实参就**合法**了、这条负控会**静默恒真**。
改成按 `LocationCode == SCREEN_CONTROL_PROPERTY` **钉死取样**（本项目踩过「按位置取下标挑到不匹配那一档」的坑）。

⚠ 删掉的 NC4 是原来「外来定位表过期」那三支 —— 表没了，负控也一起走，不是漏掉。

## 93.5 读数与对拍（`HMI_READINGS_OUT` **绝对路径**）

`.workbuddy/tmp/s93-readings-gate1c.txt`（803 行）落盘实测：

| 读数 | 阶段 92 | 阶段 93 |
|---|---|---|
| 无代码引用的常量 | 1113 | **1113**（不动） |
| 分桶 | M2 828 / M6 262 / M3 18 / 无机制 5 | **同左，一字不动** |
| 字典键（zh-CN） | 2226 | **2227** |
| 有常量的键 / 没有常量的键 | 1776 / 450 | **1776 / 451** |
| 可达机制 **M13** 面 | 21 | **22** |
| 直通面 | 15 / 14 | **15 / 14**（**零变化**） |

⇒ 「字典 +1 而 `UnusedBaseline` **不动**」不矛盾：`gen.loc.*` 这一族的键靠
`ExternalText.GenLocationKey` **推导**、没有 `Lang` 常量，可达性由 **M13** 担保
（下界同步抬满 21 → 22）。已把这条写进 `LangConstantUsageGuardTests` 的基线注释
（「字典 +1 而基线不动」是**第四次**要在那里解释数字为什么动）。

⇒ 直通面第三次**零变化**：这是「**面读数是证词、不是目标**」的**第六例**
（前五例见 #61a / #61f / #61g / #61i / #61j）。本项**从来不在**那张面上 ——
它收的是「界面语言」这件事，不是「`ex.Message` 直通模板」的形状。

## 93.6 门禁

| 跑 | 读数 |
|---|---|
| gate1（**红**） | `.workbuddy/tmp/s93-gate1.log` mtime **18:53:29** ⇒ **3 红**，且三条都是**正面证据**：<br>① `SingleFileSizeGuardTests`：`MainWindow.axaml.cs` 行数 **3017 ≠ 基线 3013**（4 行注释，见 §93.2）；<br>② `CoreVisibleTextInventoryGuardTests.VisibleChannels_AreCoveredByTheInventory`：「通道 `CrossReferenceAnalyzer.cs` 登记 13 条，但该文件实扫只有 **9** 条」；<br>③ 同文件 `NoUnclassifiedCoreChinese`：「`+ Services/DanglingRefLocation.cs`: 登记 0，实扫 **3**」「`~ Services/CrossReferenceAnalyzer.cs`: 登记 13，实扫 **9**」 |
| 修法 | 注释 4 行 → 1 行、基线 3013 → **3014**；台账 13 → 9 ＋ 新通道 3；类注释 6/61 → 10/72 |
| gate1b（**红**） | mtime **18:54:53** ⇒ **2 红**：`VisibleChannels_CountIsWhatTheDocSays`（`Expected 73 / Actual 72`）＋ `VisibleChannels_EvidenceStillExists`（新通道的标记子串 `DescribeLocation(finding)` 在 `ExternalText.cs` 里**不存在**）|
| 修法 | 标记改成 `DescribeLocation(GeneratedCodeFinding`；总数 73 → **72** ＋ 来历注释补第三次「数字为什么动」 |
| gate1c（绿） | mtime **18:56:42**，`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **653** / 大队八项 **3184** |
| 内容批 | **`c8181f7`**（12 files changed, 291 insertions(+), 109 deletions(-)，`2026-09-23T18:58:32+08:00`）⇒ gate1c 比提交**早 1 分 50 秒** ✓ |
| gate2（内容批后，绿） | `.workbuddy/tmp/s93-gate2.log` mtime **18:59:52** ⇒ `EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **654** / 大队八项 **3184** |
| 对拍（gate1c ↔ gate2） | `.workbuddy/tmp/s91-diffgate.py`（剥「持续时间」多重集）：两跑各 **29 行**，**只差 1 行** = 路径卫生 **653 → 654**。原因可核 —— 口径是 `git ls-files 'src/*' 'tools/*' 'scripts/*' 'firmware/*'`（`scripts/check-path-hygiene.sh:37`），而 gate1c 跑在内容批**之前**、`DanglingRefLocation.cs` 当时还是**未跟踪**文件 ⇒ 提交后进清单 ⇒ +1。**不是回归**（与阶段 92 的 652 → 653 同一成因） |
| 文档批 | **`14222b0`**（4 files changed, 368 insertions(+), 7 deletions(-)，`2026-09-23T19:00:47+08:00`）—— 本批次**只改 `docs/`**（本专题 §93 ＋ `project-status` §93 ＋ 登记表 #61l 闭环 / #61 母项 / 新开 #61m ＋ 盘点文档 §11）|
| gate3（文档批后，绿） | `.workbuddy/tmp/s93-gate3.log` mtime **19:02:08** ⇒ **晚于文档批 1 分 21 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **654** / 大队八项 **3184** |
| 对拍（gate2 ↔ gate3） | `.workbuddy/tmp/s91-diffgate.py`（剥「持续时间」多重集）：两跑各 **29 行**、**差集 0 / 0**（逐行全同）⇒ 文档批**不动任何代码路径** |
| 记忆批 | **`c30e518`**（2 files changed, 139 insertions(+)，`2026-09-23T19:04:57+08:00`）：快照 `docs/archive/2026-09/memory-hmi-full-2026-09-23g.md`（`MEMORY.md` 的逐字节副本，`sha256 e7866b9f8582`）＋ 析出文档 §8；`MEMORY.md` 收在 **UTF-16 码元 7989 / 8000** |
| 回填批 / gate4 | 回填批（本批次，只改 `docs/`）之后补跑 **gate4**；按阶段 85–92 的惯例，gate4 **只记** `.workbuddy/memory/2026-09-23.md`、**不回写本文档** |
| 计数 | 大队八项 **3184**（**不动**：删 1 条 `[Fact]` ＋ 加 1 条，净 0）。**逐程序集**：`App.UiTests` **496** / `Core.Tests` **2119** / 其余六项一字不动 |

## 93.7 诚实边界与未闭环

1. `#61l` **已闭环**；`#61` 家族仍开着 `#61（本体）/ #61a / #61d / #61f / #61k`，并**新开 `#61m`**
   （`HmiAction.GetDescription()` 的两个方向，见 §93.1）。
2. 判据断的是「定位码在悬空族内 ＋ 实参个数对 ＋ 回落原文保格式 ＋ 源码面接线」，
   **断不了翻译质量**（en 的 `Screen {0} / Control {1} / Action` 好不好听不在判据面上）。
3. `MainWindow` 那行 `Debug.WriteLine` **不在**判据面上 —— 它是 DEBUG 专有、且是本刀刻意保留的
   中文诊断行（走回落原文）。它由**代码审查**兜着，不是自动判据。
4. `ReasonArg` 式的**嵌套渲染**本刀没碰：`DanglingRefLocation.Args` 里装的全是字符串
   （屏幕名 / 控件名 / 属性名），没有需要按用户语言渲染的记录体 ⇒ 不需要 `RenderNestedArgs` 那一支。
   ⚠ 若将来把 `HmiAction`（#61m）塞进定位实参，就会**需要**它 —— 那时要一起收。
5. `DeclaredCodes_AndProducedCodes_AreTheSameSet` 的定位码闭包现在**四档全被产出**
   （`SCREEN` / `SCREEN_CONTROL` / `SCREEN_CONTROL_PROPERTY` / `SCREEN_CONTROL_ACTION` / `GENERATED_CODE`），
   兜底集合仍**刻意为空** —— 定位码没有「结构上到不了」的档，别为了对称硬塞一条。

---

## 阶段 94（闭环 §4 **#61m**：动作展示名的出口拆成两条 —— 面板走字典 / 产物吃固定中文）— 2026-09-23

### 94.1 一句话结论

控件动作的展示名有**三个露出点**，改动前**全是 Core 拼的中文**（英文界面下三处都是中文，
其中动作 chip 对「无动作」还显示英文 Id `None`）；而同一个映射**另有一个语言要求相反的方向**
（产物注释**必须固定中文**）。⇒ 本阶段把出口**拆成两条**：界面侧新增
`src/HmiEditor.App/Services/ActionTextKeys.cs`（键推导的唯一实现，27 个键），
属性面板两处 ＋ 交叉引用面板一处全部改走它；Core 的中文**留在原地**
（产物侧直用、界面侧只当**回落**）。并把台账里两处**错归成 DATA** 的登记归位。

### 94.2 量的那一遍（读，不预设）—— 登记只写了**三条产出面里的一条**

登记 #61m 只点名了 `HmiAction.GetDescription()`（13 条）。逐处读代码后，
**同一个映射有三条产出面、共 60 条中文**：

| # | 产出面 | 中文 | 消费方向 | 登记里 |
|---|---|---|---|---|
| ① | `HmiAction.GetDescription()`（`Models/HmiAction.cs:195`） | **13** | 交叉引用面板（`CrossReferenceAnalyzer.cs:150,167` ⇒ `{Binding Description}`）＋ **产物注释**（`CodeGenerator.Actions.cs:57,64,73` 的 `Cmt(`） | ✅ 写了 |
| ② | `ActionTypeMeta.DisplayName`（`Design/ActionTypeMeta.cs:25`） | **12** | 属性面板动作 chip（`PropertyPanelView.Actions.cs:107`）＋ **产物两处**（`CodeGenerator.Actions.cs:282` 的权限门注释、`SecurityConfigCodeGen.cs:335` 的动作→权限表） | ❌ **没写**（同一个双方向病） |
| ③ | `ActionMeta.DisplayName`（`GetAvailableActions` 的 6 张表，`:105-192`） | **35** | 只进属性面板的动作下拉框（`PropertyPanelView.Actions.cs:43`）⇒ **单方向** | ❌ **没写** |

⇒ 三处都**无一条落盘**（落盘的是 `ActionType` / `Target` / `Value` 这些 Id），
所以「DATA：翻它会让工程文件随 UI 语言变」这个豁免理由**在代码里不成立**
⇒ 与登记 **#54 同病**（豁免表里的"理由"没人去代码里核，它就成了一个能把缺陷合法化的字段）。

**顺带量出的三件**（登记里没有）：

1. **同一概念两套措辞**：`SetBit` 在下拉里是 `"置位 (ON)"`、在 chip 里是 `"置位"`；
   `SetVariable` **在同一个方法的不同控件表里**既是 `"设置变量"` 又是 `"设置变量值"` ⇒ 与 #61j
   「同一族文案有两条独立实现」同病。
2. **chip 对「无动作」显示英文 Id**：`ActionTypeMeta.DisplayName` **没有 `"None"` 分支**
   ⇒ 兜底 `_ => actionType` 返回 `"None"` ⇒ 动作 chip 上是 `1.None:`（本刀顺带修正）。
3. **面板缺一个渲染点**：`ReferenceLocation` 是 Core DTO、`{Binding Description}` 直绑 ——
   Core 不引本地化 ⇒ **「翻了」无处生效**（这是本刀必须动视图层的原因，不是审美）。

### 94.3 处置：一份中文、两个方向、各走各的路

**关键决定：不新增 Core 记录体、不新增码表** —— 与 `RoleTextKeys`（阶段 66 闭环 #50）**同形**：
Core 给**稳定的 Id**，App 侧按 Id 推键（`Lang` 常量一对一）。理由与本项目既有口径一致：
Core 是库、不引 `Lang` / `LocalizationService`。

| 方向 | 谁读 | 措辞 |
|---|---|---|
| **生成产物** | `CodeGenerator.Actions.cs` ×5 站点、`SecurityConfigCodeGen.cs` ×1 | `HmiAction.GetDescription()` / `ActionTypeMeta.DisplayName` —— **冻结中文**（产物可复现性） |
| **编辑器界面** | 属性面板下拉 / 动作 chip / 交叉引用面板 | `ActionTextKeys.TypeName` / `.Describe` / `.DescribeReference` —— 走字典 |

⇒ 于是**同一件事有了两份存储**（Core 的中文 ＋ 字典的 zh）。按本项目反复量到的形状，
**"同一件事的两条消费路径不是多余一份，而是必然漂移的两份"** ⇒ 必须用**相等式**钉住：
zh 字典的 `action.type.*` 必须**逐字等于** `ActionTypeMeta.DisplayName` 的返回值（判据 J3）。

**措辞收敛**（本刀唯一一处用户可见变化）：下拉与 chip 合并成**一套**，取 Core 那一套
（与产物注释一致）⇒ 三处变短：`置位 (ON)` → `置位`、`复位 (OFF)` → `复位`、
`设置变量值` → `设置变量`。选这个方向而不是"取下拉的长措辞"，是因为长措辞改不动产物那一侧
（产物冻结），而两边不一致就没法用相等式钉住。

**落地（内容批 `f7bfe52`，13 files，+870 / −59）**：

| 层 | 文件 | 改动 |
|---|---|---|
| Core | `Models/HmiAction.cs` | `ActionMeta` **删掉 `DisplayName`**；6 张表 **−35 条中文**（条目数一条没少，见 §94.7 的核对）；`GetDescription()` 加文档注释标明两个方向 |
| Core | `Design/ActionTypeMeta.cs` | 类注释改正（原文写着"属性面板与代码生成共用"，拆完就不再成立）；`DisplayName` 标注"**只给产物用**" |
| Core | `Services/CrossReferenceAnalyzer.cs` | `ReferenceLocation` 新增 `HmiAction? Action`；动作那 2 处填它（`Description` **保留**为回落原文） |
| App | `Services/ActionTextKeys.cs` | **新**：`TypeName` / `Describe` / `DescribeReference` ＋ `MappedActionTypes`（13 个 Id） |
| App | `Views/PropertyPanelView.Actions.cs` | 下拉与 chip 两处改走出口 |
| App | `Views/ReferenceRow.cs` | **新**：交叉引用面板的行（App 侧渲染点） |
| App | `Views/CrossReferenceView.axaml(.cs)` | `x:DataType` 改 `views:ReferenceRow`；结果集合改行类型（**模板里五个绑定逐字未动**） |
| 资源 | `Resources/Lang.cs` | ＋**27** 常量（13 短名 ＋ 13 描述 ＋ 1 前缀） |
| 资源 | `Core/Resources/Strings.{zh-CN,en-US}.json` | 各 ＋**27** 键（2227 → **2254**） |
| 测试 | `App.UiTests/ActionTextLocalizationGuardTests.cs` | **新**：9 条判据（J1–J9） |
| 测试 | `Core.Tests/CoreVisibleTextInventoryGuardTests.cs` | 台账归位（见 §94.4） |

⚠ **`ReferenceLocation.Action` 挂的是对象、不是「码 ＋ 实参」**（与 #61l 的 `DanglingRefLocation` 不同）：
动作描述的实参组合**按类型而异**（0/1/2 个），用固定实参数组会让
`LocalizedFormatCallSiteTests` 的 C1（实参个数 == 占位符个数）**失去意义** ——
那条判据读的就是调用点上的**字面实参个数**。挂对象之后，App 侧按类型分派、
每个分支的实参个数都是**字面量**。

### 94.4 台账归位（两处，都是「归类」不是「数量」）

| 登记 | 改前 | 改后 | 为什么 |
|---|---|---|---|
| `Models/HmiAction.cs` | `Pending` = **48**（理由"DATA：落盘/进产物"） | `VisibleChannels` = **13**（消费标记 `ActionTextKeys.cs` 的 `Lang.ActionDescSwitchScreen`） | 相减的 **35** 条（下拉标签）已随 `ActionMeta.DisplayName` 拆走、进了字典 ⇒ **代码里实扫就是 13**。剩下的 13 条现在**只走回落路径**（正常路径走 `ActionTextKeys.Describe`）⇒ 与通道 ⑦（`SvgParseError.cs`）/ ⑨（`ExpressionSyntaxError.cs`）**同形**，所以消费标记落在**出口**那一层 |
| `Design/ActionTypeMeta.cs` | `Pending` = **12**（理由"动作元数据（图标/分组/展示名）"——**两处共用**的真相） | `Pending` = **12**（理由改成 **GEN**：消费方是**生成器**） | 面板那一侧已拆走 ⇒ 只剩 `CodeGenerator.Actions.cs` / `SecurityConfigCodeGen.cs` 两个产物写入点 ⇒ 与同栏的 `Services/ScreenLayerRefs.cs`（4 条）同类 |

⇒ `VisibleChannels` 总和 **72 → 85**（**+13**，是**归类归位**、不是新增文案：
同一个文件改动前实扫 **48**、改动后 **13**）。`VisibleChannels_CountIsWhatTheDocSays` 的常数随之改，
并写明这是**第四次**"这条注释要解释数字为什么动"（前三次：阶段 87/88 的"新造"、
阶段 91 的"第一次被看见"、阶段 93 的"搬迁"）。⇒ 盘点文档 §12。

⚠ **`VisibleChannels_EvidenceStillExists` 对通道 ②** 用的仍是 `{Binding Description}` ——
它现在指向 `ReferenceRow.Description`（App 渲染）而不是 Core 的 `Description`。
通道 ② 的 9 条里那 2 条动作描述**已不再直显**（走回落），另 7 条仍是直显 ⇒ 该行**数字仍为 9**，
但这是**下一刀**要收的（登记 #61n，见 §94.10）。

### 94.5 判据：J1–J9，以及为什么 J2/J3/J5/J7 是本刀的核心

| # | 判据 | 单独能看见 |
|---|---|---|
| **J1** | `MappedActionTypes` ↔ `ActionTypeMeta` 的公开 Id 常量**双向**一对一 | 新增动作类型漏建键 ⇒ 界面上静默显示英文 Id |
| **J2** | 每个 Id 的 `TypeName(id) != id` | **回落分支会让"漏映射"静默通过** —— 不加这条，J1 之外的口子看不出来 |
| **J3** | zh 字典 `action.type.*` **逐字等于** `ActionTypeMeta.DisplayName` | 界面与产物两套措辞漂移（**两侧都断言**：NC-B 改字典红、NC-C 改 Core 也红） |
| **J4** | **成对**：en 侧不许含汉字 **且** zh 侧确实含汉字 | 只断前一半会被「夹具/字典本身没有中文」满足 |
| **J5** | zh 下 `Describe(a)` **逐字等于** `a.GetDescription()` | 这是「**零用户可见变化**」的**证明**（不是声明）—— 若不等，等于顺手改了用户看到的东西 |
| **J6** | en 侧描述带得上实参、且无汉字；无目标时不出现 `{0}` 字面量 | C1 只管"实参个数对不对"，管不到"实参有没有被填进模板" |
| **J7** | 切到 en 后 Core 那两串中文**逐字不变**（＋ 下界：快照里确实有 12/13 条中文） | 产物可复现性：哪天有人"顺手统一"把 Core 也接字典，产物注释就会随 UI 语言变 |
| **J8** | **端到端**：真实工程 → `CrossReferenceAnalyzer` → `ReferenceRow` | 走真实接缝：分析器**有没有把动作挂到 `Action` 上**（挂漏 ⇒ 回落 ⇒ 英文侧红）＋ 渲染点真的读了字典 |
| **J9** | 表外类型回落成 Id（**诚实边界**） | 把"回落"与"漏映射"分开 —— 后者由 J2 抓 |

⚠ **不复写别人的规则**：占位符 ↔ 实参个数、两本字典占位符一致由 `LocalizedFormatCallSiteTests`
（C1/C3）管；App 层中文硬编码由 `HardcodedLocalizationGuardTests` 管。本文件只在注释里指过去。

### 94.6 负控（5 支，全在真仓库上，还原一律**写回原文** + `sha256` 逐字节核对）

`.workbuddy/tmp/s94-nc.py`（每条：注入 → `dotnet test`（**不带 `--no-build`**，否则测的是旧二进制）
→ 写回原文 → `sha256` 核对）：

| 负控 | 注入 | 红集 | 说明 |
|---|---|---|---|
| **NC-A** | `ActionTextKeys.TypeName` 删掉 `SetBit` 分支 | **3 红**：J2 / J3 / J4 | 防"漏映射静默通过"（J2 是专门为它写的） |
| **NC-B** | zh 字典 `action.type.set_bit` 改字 | **1 红**：J3 | 相等式（**字典侧**） |
| **NC-C** | Core `ActionTypeMeta.DisplayName` 改字 | **1 红**：J3 | 相等式（**Core 侧**）⇒ 两侧都断言，成立 |
| **NC-D** | `ReferenceRow.Description` 不走出口 | **1 红**：J8 | 端到端 |
| **NC-E** | `MappedActionTypes` 删掉 `None` | **1 红**：J1 | 双向（None 必须在内） |

5/5 符合预期；注入后**编译均未失败**（NC-A 删分支后 `_ => actionType` 兜底仍在 ⇒
红的是**判据**不是编译，符合"负控判红要能区分判据红与编译红"）；5 支**全部还原 `sha256` 一致**
（汇总 `.workbuddy/tmp/s94-nc-summary.log`，mtime **19:23:15**）。

### 94.7 🩸 两次被拦下（一次是**真红**、一次是**环境**）

#### 94.7a 锚点替换把 `Lang.HwIssueLvglVersionMismatch` 那行一并吞掉（真红，门禁抓到）

插入动作文案块时用「末尾那条 `const` 行」当锚点做替换，**锚点行自己没被写回**
⇒ `Lang.HwIssueLvglVersionMismatch` 整行消失（它上方的「硬件目标校验文案」注释头还在，
于是文件尾部多出一个**孤儿注释头**）。这条常量本来就**没有人读**（它的键
`hw.issue.lvgl_version_mismatch` 由 `PluginTextKeys.KeyFor(code)` 运行期**派生**，
可达性走机制 **M3**），所以**编译与键可达性守卫都不报**。

**抓到它的只有门禁那一条**：

```
无代码引用的 Lang 常量个数 1112 ≠ 基线 1113。
降了 = 清理了死常量，请把 UnusedBaseline 改成 1112。
```

⇒ `.workbuddy/tmp/s94-gate1.log`（mtime **19:24:36**）唯一红。
**处置：写回原文、基线不动**（`.workbuddy/tmp/s94-fix1.py`，干跑→落盘→回读校验；
判据是 `git diff Lang.cs` 相对 HEAD **只有 `+` 行**：`32 0`）。
⚠ 三种"顺手"处置都被否掉：**改基线成 1112**（把事故洗成"清理",下次真删死常量就看不出来）、
**留着不管**（下次谁读这条常量就编译不过）、**顺手把注释头也删掉**（越删越像有意为之）。
纪律复述：**门禁说"降了 = 清理了死常量"，但"降了"也可能是"被脚本吞了"** ——
先回答"是哪一条、为什么没的"，再决定动不动基线。

#### 94.7b gate2 第一跑 `No space left on device`（**环境**，不是回归）

内容批 `f7bfe52` 之后的第一跑 gate2（`.workbuddy/tmp/s94-gate2.log`，mtime **19:32:11**）
红在 `HmiEditor.Device.Tests.T113SoBuilderTests.ResolveContext_MissingSdk_ReportsMissingItems`：

```
System.IO.IOException : No space left on device : '/tmp/sdk-b3667e83d8234f4eb79e57bb5743675c/generated_hmi.c'
   at HmiEditor.Device.Tests.T113SoBuilderTests.ResolveContext_MissingSdk_ReportsMissingItems() … :267
```

**根因是环境容量**：本沙箱的 `/tmp` 是 **10 MB 的 tmpfs**（`mount` 实测
`tmpfs on /tmp type tmpfs … size=10240k`），而门禁第 [5] 步同时起 8 个 testhost、
其中若干个往 `/tmp` 写产物（SoBuilder 的 `generated_hmi.c`、openfail/opensave 的临时工程包…）
⇒ 偶发打满。**三条独立证据**：① 同一个提交前 2 分钟的 gate1b **全绿**（`s94-gate1b.log`，19:28:51）；
② 单独复跑该程序集 **13/13 全绿**（`s94-device-retry.log`，253 ms）；
③ 失败发生在**写入**而非断言，且 `/tmp` 在跑完后回到 **0 / 10 MB**。
⚠ 这三条**只**证明"与本次改动无关"，**不**证明"以后不会再犯" —— 见 §94.10 第 5 条。

### 94.8 读数与对拍（`HMI_READINGS_OUT` **绝对路径**）

`.workbuddy/tmp/s94-readings-gate1b.txt`（803 行，与阶段 93 同长）：

| 读数 | 阶段 93 | 阶段 94 |
|---|---|---|
| 无代码引用的常量 | 1113 | **1113**（不动；见 §94.7a —— 中途曾掉到 1112） |
| 分桶 | M2 828 / M6 262 / M3 18 / 无机制 5 | **同左，一字不动** |
| 字典键 | 2227 | **2254**（＋27） |
| 有常量的键 / 没有常量的键 | 1776 / 451 | **1803 / 451** |
| 可达机制 **M1-Lang 常量** 面 | 663 | **690**（＋27） |
| **M13 / M14** 面 | 22 / 7 | **22 / 7**（不动） |
| 直通面 | 15 / 14 | **15 / 14**（**零变化**） |
| 大队八项 | 3184 | **3193**（＋9 = 新增判据条数，**纯净增**） |
| 逐程序集 | `App.UiTests` 496 / `Core.Tests` 2119 | `App.UiTests` **505** / `Core.Tests` 2119 |
| 路径卫生 | 654 | **654** |

⇒ 直通面第四次零变化 ⇒ 「**面读数是证词、不是目标**」**第七例**
（前六例 #61a / #61f / #61g / #61i / #61j / #61l）。
本项**从来不在**那张面上（它收的是「界面语言」，与 `ex.Message` 直通模板无关）。

⇒ 「基线 1113 **不动**、字典 +27」也不矛盾，两者数的集合不同：那 27 个常量**都有代码引用**
（`ActionTextKeys.cs` 在读它们），不进本集。

### 94.9 门禁

| 跑 | 读数 |
|---|---|
| gate1（**红**） | `.workbuddy/tmp/s94-gate1.log` mtime **19:24:36** ⇒ **唯一红** = `LangConstantUsageGuardTests.UnusedLangConstants_MatchBaseline`（**1112 ≠ 1113**，见 §94.7a）⇒ **正面证据**（那一行确实没了，不是判据抖动） |
| 修法 | `Lang.HwIssueLvglVersionMismatch` 写回原文（`s94-fix1.py`，`git diff` 相对 HEAD 只剩 `+` 行）⇒ **基线不动** |
| gate1b（绿） | `.workbuddy/tmp/s94-gate1b.log` mtime **19:28:51** ⇒ `EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **654** / 大队八项 **3193** |
| 内容批 | **`f7bfe52`**（13 files，+870 / −59，`2026-09-23T19:31:03+08:00`）⇒ gate1b 比提交**早 2 分 12 秒** ✓ |
| gate2 第 1 跑（**红·环境**） | `.workbuddy/tmp/s94-gate2.log` mtime **19:32:11**（晚于提交 **1 分 8 秒**）⇒ `Device.Tests` **1 红**（`No space left on device`，见 §94.7b） |
| 修法（不碰代码） | 单跑 `T113SoBuilderTests` **13/13 绿**（`s94-device-retry.log`）⇒ 判环境；**同一条命令**重跑 gate2（`s94-gate2b.log`） |
| gate2（绿） | 见 §94.9b |
| 文档批 | 本批次**只改 `docs/`**（本专题 §94 ＋ `project-status` §94 ＋ 登记表 #61m 闭环 / #61 母项 / 新开 #61n #61o ＋ 盘点文档 §12） |
| gate3（文档批后） | 见 §94.9b |
| 对拍 | `s91-diffgate.py`（剥「持续时间」多重集）与 `s92-diffreadings.py`（再剥随机夹具名 / 临时路径 / 时间戳派生数字） |
| 记忆批 | `MEMORY.md` 快照 `docs/archive/2026-09/memory-hmi-full-2026-09-23h.md` ＋ 当日日志追加阶段 94 |
| 回填批 / gate4 | 回填批（只改 `docs/`）之后补跑 gate4；按阶段 85–93 惯例 **gate4 只记** `.workbuddy/memory/2026-09-23.md`、**不回写本文档** |

### 94.9b gate2 重跑与文档批后的门禁（回填时补写）


> **回填（本批，只改 `docs/`）** —— 以下各行由回填批补写（gate4 按惯例只记当日日志、不回写本文档）。

| 跑 | 读数 |
|---|---|
| gate2 重跑（绿） | `.workbuddy/tmp/s94-gate2b.log` mtime **19:33:52** ⇒ `EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **656** / 大队八项 **3193**（逐程序集 44 / 44 / 75 / 87 / 4 / 315 / 2119 / 505；独占段 8 ＋ 2） |
| 对拍 gate1b ↔ gate2 | `.workbuddy/tmp/s94-diffreadings.py`（**新脚本**：在 s92 的口径上再剥「驱动轮数 / 数据根计数 / 序列化长度」）：两跑各 **803 行**，剥「墙钟 ＋ 夹具派生量」后**只剩 2 / 2** —— 一条是 `[保存]` 的**收敛轮数**（1 vs 3 轮，由调度决定），一条是包长 **3433 → 3434 B**（§92.6b 的**时间戳派生长度**：`HmiProject.CreatedAt` 默认 `DateTimeOffset.Now` ＋ STJ 裁小数秒尾零）。⇒ **无产品数字漂移** |
| 门禁日志对拍 | `.workbuddy/tmp/s91-diffgate.py`（剥「持续时间」多重集）：两跑各 **29 行**、**差集 0 / 0** |
| 文档批 | **`50ffd54`**（4 files，+444 / −5，`2026-09-23T19:35:45+08:00`）—— **只改 `docs/`**（本专题 §94 ＋ `project-status` §94 ＋ 登记表 #61m 闭环 / #61 母项 / 新开 #61n #61o ＋ 盘点文档 §12） |
| gate3（文档批后，绿） | `.workbuddy/tmp/s94-gate3.log` mtime **19:38:22** ⇒ **晚于文档批 2 分 37 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **656** / 大队八项 **3193**（逐程序集逐条同数） |
| 对拍 gate2 ↔ gate3 | 日志 `s91-diffgate.py`：两跑各 **29 行**、**差集 0 / 0**；读数 `s94-diffreadings.py`：各 803 行、剥噪声后**只剩 1 / 1**（`[保存]` 收敛轮数与包长，已知噪声类）⇒ 文档批**不动任何代码路径** |
| 记忆批 | **`49a93b4`**（1 file，+77，`2026-09-23T19:38:31+08:00`）：快照 `docs/archive/2026-09/memory-hmi-full-2026-09-23h.md`（逐字节副本，`sha256 d719635f23e7`）；`MEMORY.md` 收在 **UTF-16 码元 7984 / 8000**（三条新契约靠**等量减重**腾出空间，本轮**没有**析出新章节）；当日日志 1069 → 1124 行 |
| 回填批 / gate4 | 回填批（本批次，**只改 `docs/`**）之后补跑 **gate4**；按阶段 85–93 惯例 **gate4 只记** `.workbuddy/memory/2026-09-23.md`、**不回写本文档** |

⚠ **路径卫生的 654 → 656 要能自己解释**（我第一版照阶段 92/93 的「内容批 +1」惯例写了 655，**错**）：
口径是 `git ls-files 'src/*' 'tools/*' 'scripts/*' 'firmware/*'`（`scripts/check-path-hygiene.sh:37`）——
**不含 `tests/`**。内容批的 3 个新文件里 `ActionTextLocalizationGuardTests.cs` 在 `tests/` 下 ⇒ 不进清单，
另两个（`App/Services/ActionTextKeys.cs` / `App/Views/ReferenceRow.cs`）才进 ⇒ **654 + 2 = 656**。
⚠ 记忆批的**快照在 `docs/` 下**、**不**进这条清单 ⇒ gate3 → gate4 **预期 656 → 656**（不是 +1）。

**阶段 94 的四个提交（内容 → 文档 → 记忆 → 回填）**：`f7bfe52`（`19:31:03`，13 files，+870 / −59）
→ `50ffd54`（`19:35:45`，4 files，+444 / −5）→ `49a93b4`（`19:38:31`，1 file，+77）→ 回填批（只改 `docs/`）；
四次门禁 gate1b / gate2 / gate3 / gate4 依次**晚于**对应的提交。

### 94.10 诚实边界与未闭环

1. **翻译质量不在判据面上**：en 的 `Set` / `Reset` / `Momentary On` 好不好用，判据断不了。
2. **未知动作类型仍回落成 Id**（J9 钉住）：这是**结构上中性**的兜底，与原口径一致。
3. **`CrossReferenceAnalyzer` 那 2 条 `$"动作 {…}"` 中文仍留在 Core**：它们是回落原文，
   与"回落必须留在 Core"的全局口径一致 ⇒ 通道 ② 的 9 条**一条没少**。
4. **`ReferenceRow` 的两个枚举仍是 `ToString()`**（`ActionTarget` / `Label`）⇒ 中文界面下也是英文
   ⇒ **新开 #61o**（与 #61j「枚举名硬拼」同族；本刀**刻意不动**，避免把两件事混进一个负控）。
5. **`/tmp` 只有 10 MB 这件事没有判据兜着**（§94.7b）：本刀只证明"与本次改动无关"，
   没有消除这个环境风险（门禁第 [5] 步的 8 路并行 + tmpfs 10 MB 仍可能偶发）。
   ⚠ **不**把 `TMPDIR` 指到别处 —— 那会同时改掉多个既有判据的运行环境
   （`TestTempDir` / 路径卫生 / 数据根回退），属"一次放开多个变量"，本刀刻意不做。
6. **语言切换依赖面板重建**：`MainWindow.OnLanguageChanged` 会 `RebuildAllPanels()`
   （新实例、空结果）⇒ 本层不需要 `INotifyPropertyChanged`，且"切语言后要重新搜索"
   与改动前**完全一致**。
7. **新开 #61n**：`CrossReferenceAnalyzer` 其余 **7 条**面板措辞仍未本地化
   （`控件 {0} 绑定到变量` / 屏幕引用 / 脚本引用 / 表达式引用）—— 它们与动作那 2 条走
   **同一个** `{Binding Description}`，本刀只收了动作那 2 条（**一次只放开一个变量**）。

## 阶段 95（闭环 §4 **#61n**：交叉引用面板剩下的 7 条描述改走字典 —— 分派依据第一次**不是** `ReferenceType`）— 2026-09-23

### 95.1 一句话结论

交叉引用面板的「描述」列以前**整列直显 Core 拼好的中文整句**（`CrossReferenceAnalyzer` 的 9 处
`Description = ...`）。阶段 94 只把其中**动作那 2 条**接到界面侧出口（`ActionTextKeys.DescribeReference`），
剩下 **5 种措辞 / 7 个站点**在英文界面下仍是中文。本阶段把**分派整体搬出 Core**：
新增 Core 侧 `ReferenceDescriptionKind`（种类 ＋ 实参字段）与 App 侧
`Services/ReferenceTextKeys.cs`（键推导**唯一实现**），`ReferenceRow.Description` 改成**只转发**；
Core 那 9 条中文**一条不动**（回落原文 ＋ 字典缺键时的出口）。

⇒ 面板描述列**零用户可见变化**（中文逐字不变），但英文界面下 7 条**第一次**跟着语言走。

### 95.2 量的那一遍（读，不预设）—— 登记里三处假设都不成立

登记 #61n 写的是「其余 7 条面板措辞未本地化 / 收法是按 `ReferenceType` 分派 / 其中一条是**表达式引用**」。
逐处读码后：

| # | 登记写的 | 代码里的实情 |
|---|---|---|
| ① | 「7 条」 | 是 **9 条里除动作那 2 条**（阶段 94 已接管）剩下的 —— 但站点是 **7 个**、措辞只有 **5 种**（屏幕 2 站同文、脚本执行 2 站同文） |
| ② | 「表达式引用」 | **代码里不存在产出点**：`grep -rn ExpressionVariable src/` 只有枚举声明那一行（`CrossReferenceAnalyzer.cs:19`）⇒ 另开 **#61p**，本刀**不删**该成员 |
| ③ | 「按 `ReferenceType` 分派」 | **做不到** —— 同一个 `VariableBinding` 下有两种措辞（联动模型「绑定变量 (方向)」/ 回退旧字段「绑定到变量」），同一个 `ActionTarget` 下也有两种（「动作 {0}」/「{0} → 切换到 {1}」）⇒ 类型**决定不了模板**，必须新增枚举 |

⚠ **第 ③ 条是本阶段唯一的结构性决定**：分派依据从「类型」改成「**描述种类**」。
若按类型分派，那两对措辞会被粘到同一份模板上 —— 表现为「有一半行永远显示错的那句」，
而且**编译、字典守卫、台账**三条全都不会响。

### 95.3 处置：Core 给「种类 ＋ 实参」，App 给模板（内容批 `ebca0a9`，12 files）

与 `RoleTextKeys`（阶段 66 / #50）**同形**：Core 是库、不引 `Lang` / `LocalizationService`
⇒ 跨语言的那一步只能在 App。

| 层 | 文件 | 改动 |
|---|---|---|
| Core | `Services/CrossReferenceAnalyzer.cs` | 新增 `public enum ReferenceDescriptionKind { Unset = 0, VariableBinding, LegacyVariableBinding, ActionDetail, ScreenReference, ScriptRun, ScriptBinding }`；`ReferenceLocation` ＋3 个字段（`DescriptionKind` / `BindingDirection` / `ScriptFunctionName`）；**9 个站点全部赋值**（屏幕/脚本各 2 站同文一起换）；**9 条中文回落一条不动** |
| App | `Services/ReferenceTextKeys.cs`（**新**） | `Describe(ReferenceLocation)` —— 7 支 `switch`、**无默认支**、文件级只禁 **CS8524**（不碰 CS8509）；`ActionDetail` 档转交 `ActionTextKeys.DescribeReference(action)`，拿不到 action 时回落 Core 原文 |
| App | `Views/ReferenceRow.cs` | `Description` 从「自己判 `Action` 非空」改成**只转发** `ReferenceTextKeys.Describe(_source)` ⇒ **分派只剩一处** |
| App | `Views/CrossReferenceView.axaml` | **只改注释**（说明分派已收敛）；模板五个绑定**逐字未动** |
| 字典 | `Resources/Lang.cs` ＋ `Strings.{zh-CN,en-US}.json` | ＋**5** 常量 / ＋**5** 键 × 两本（**2254 → 2259**） |

5 个键（zh 值**逐字等于** Core 那 5 种回落 —— 由判据 J2 的相等式钉住）：

| 键 | zh | en |
|---|---|---|
| `cross.ref.desc.binding` | 控件 {0} 绑定变量 ({1}) | Control {0} bound to variable ({1}) |
| `cross.ref.desc.legacy_binding` | 控件 {0} 绑定到变量 | Control {0} bound to variable |
| `cross.ref.desc.screen_switch` | {0} → 切换到 {1} | {0} → Switch to {1} |
| `cross.ref.desc.script_run` | {0} → 执行脚本 {1} | {0} → Run script {1} |
| `cross.ref.desc.script_binding` | 脚本绑定: {0} ({1}) | Script binding: {0} ({1}) |

⚠ **为什么挂「字段」而不是挂「拼好的实参」**：与阶段 94 的 `ReferenceLocation.Action` 同一个取舍 ——
实参个数按种类而异（1 / 2 个），每支的 `Format` **实参个数必须是字面量**，
否则 `LocalizedFormatCallSiteTests` 的 C1（实参个数 == 占位符个数）就失去意义。

⚠ **`Unset = 0` 排第一**：枚举字段忘赋值时落进它，而**不是**静默落进某个带真实语义的档
（阶段 92 的 #61j 实测过这类默认支会显示**假标签**）。`Unset` 的处置是**显式写出来**的诚实边界：
原样显示 Core 回落原文（中文）⇒ 英文界面下一眼可见；判据再从另一侧钉「真实分析器产出的行不许是 Unset」。

### 95.4 台账归位：通道 ② 的**数字没动、消费方换人**

| 项 | 阶段 94 | 阶段 95 |
|---|---|---|
| 通道 ②（`Services/CrossReferenceAnalyzer.cs`） | **9** 条 / 消费方 `App/Views/CrossReferenceView.axaml` / 标记 `{Binding Description}` | **9** 条 / 消费方 `App/Services/ReferenceTextKeys.cs` / 标记 `Lang.CrossRefDescScreenSwitch` |
| 通道数 / 条数之和 | 11 / **85** | 11 / **85**（**±0**） |

⇒ 这 9 条**一条没少**，但从「直显」变成「只作回落」—— 与阶段 89/93 对通道 ①/⑩ 做的**同一件事**。
守卫 `CoreVisibleTextInventoryGuardTests` 里那条 `VisibleChannels` 条目在**内容批**里就改了
（数字 9 不变 ⇒ `VisibleChannels_CountIsWhatTheDocSays` 的常数 **85 不动**）。

### 95.5 判据 `ReferenceDescriptionLocalizationGuardTests`（J1–J9，App.UiTests）

| # | 判据 | 单独能看见 |
|---|---|---|
| **J1** | 每种**有产出**的种类随语言变（下界 6） | 漏一支 / 走了回落 ⇒ 值不变 ⇒ 红 |
| **J2** | zh 渲染 **逐字等于** Core 回落（下界 6 行） | 「零用户可见变化」的**证明**；顺带钉「实参真的接上」 |
| **J3** | **成对**：en 无汉字 **且** zh 含汉字 | 只断前一半会被「字典本来就没中文」满足 |
| **J4** | **端到端**：真实工程 → `CrossReferenceAnalyzer` → `ReferenceRow`（无一行 `Unset` / en 下无汉字 / 实参被填进模板 / 不出现 `{0}`·`{1}`） | 分析器有没有把种类挂上 ＋ 渲染点真的读了字典 |
| **J5** | `ActionDetail` ⇔ `Action != null` **双向** | 一档矛盾状态（挂了种类却没挂对象） |
| **J6** | 切语言后 Core 的 9 条中文**逐字不变** | 谁把 Core 也接字典，产物/回落侧就随 UI 语言变 |
| **J7** | `Unset` 走回落（**诚实边界**） | 把「忘了填」与「漏映射」分开 |
| **J8** | 5 个键**两本字典都在且真翻**（双向） | 「键在但值是同一份中文/英文占位」 |
| **J9** | 7 个枚举成员名**不许与 `Lang` 常量同名** | 见 §95.7a —— 这条是**被门禁打出来之后补的** |

⚠ **不复写别人的规则**：占位符 ↔ 实参个数由 `LocalizedFormatCallSiteTests`（C1/C3）管、
App 层中文硬编码由 `HardcodedLocalizationGuardTests` 管 —— 本文件只在注释里指过去。
⚠ 夹具本身**自证过一次**：首跑 3 红，其中 `ScriptRun` 档**没有样本** ⇒ 判据在空转
（判据自己点名了「判据在空转」）⇒ 给宿主控件补第二个 `HmiAction.ExecuteScript` 动作后转绿。

### 95.6 负控 7 支（全在真仓库上，还原一律**写回原文** ＋ `sha256` 逐字节核对）

驱动脚本 `.workbuddy/tmp/s95-negative.py`（干跑 → 落盘 → 跑过滤后的 16 个用例 → 逐个还原）。
用例集 = `ReferenceDescriptionLocalizationGuardTests`（9 条）＋ `LocalizationRefreshUiTests`（7 条）：

| 支 | 注入（一句话） | 期望红集 | 实测（`.workbuddy/tmp/s95-nc-NC-*.log`） |
|---|---|---|---|
| NC-A | Core 站点**忘了把方向接进字段**（删 `BindingDirection = binding.Direction`） | J2 | ✅ **1 红**（J2） |
| NC-B | zh 模板漂移一个字（`脚本绑定:` → `脚本绑定`，冒号丢了） | J2 | ✅ **1 红**（J2） |
| NC-C | App 分支**写成回落**（`ScreenReference` 那支 → `location.Description`） | J1 / J3 / J4 | ✅ **3 红**，逐条相符 |
| NC-D | Core 站点**忘了填种类**（删 `LegacyVariableBinding` 那行） | J1 / J4 | ✅ **2 红**，逐条相符 |
| NC-E | en 值改回中文（`legacy_binding` 那一档**没翻**） | 首版只写 2 条 | ⚠ **实测 4 红**（＋J8 ＋J1）⇒ **改预期、不改判据** |
| NC-F | 枚举里加一个**与 `Lang` 常量同名**的成员（`ScreenSwitch`）—— J9 的唯一触发方式 | J9 | ✅ **1 红**；**伴随 1 条 CS8509 警告**（`ReferenceTextKeys` 缺这一支），**编译成功** |
| NC-G | 注掉一个切语言类的集合标注 ⇒ 「漏挂集合」那条静态判据点名 | 集合判据 | ✅ **1 红** |

⚠ **7 支全部能编译** —— 每一支都是「**跑起来**、被某条判据点名」，
不是「编译不过」（那等于什么也没证明；本项目的既有纪律：负控必须能编译，改名类注入不算数 —— 会先撞 `CS0103`）。
⚠ **NC-E 的教训**：「负控预期集」要按**实际红集**填并写清成因，不能凭直觉圈几条 ——
J8「两本同值 ⇒ 没翻」与 J1「zh == en ⇒ 不随语言变」在那一支上**也在正确地工作**，
把它们当成「多出来的红」去改判据，就是拿判据去适配坏法。

### 95.7 🩸 两次被拦下（都是「既有守卫抓到我」，不是我抓到自己）

**(a) 枚举成员名撞 `Lang` 常量名 ⇒ 那个零引用常量被**意外写活**。**
gate1 唯一红是 `LangConstantUsageGuardTests.UnusedLangConstants_MatchBaseline`：**1112 ≠ 基线 1113**，
给出的处置建议是「把基线改成 1112」。用 `.workbuddy/tmp/s95-find-culprit.py` 算出新加代码里
与 `Lang` 常量**同名**的标识符 ⇒ 抓到 `ReferenceDescriptionKind.ScreenSwitch` 撞
`Lang.ScreenSwitch = "screen.switch"`（该常量零使用点，键由 `ScreenManagerView.axaml` 的 `{res:Loc}` 担保、落 M2 桶）。
`LangConstantUsageGuardTests` 的引用面是「**任意标识符**」（`src/` ＋ `tests/`，`ReferenceFace.Code`）
⇒ 我在别处敲了这个名字，那条常量就被算成「有人读」。

处置：**改名** `ScreenSwitch` → `ScreenReference`（精确串替换 —— `replace_all` 会误伤
`Lang.CrossRefDescScreenSwitch`），**基线 1113 不动**；分桶复测回到 **1113 / M2 828**；
并新增 **J9** 把这类事故变成判据（反射取两侧名字集合，**不写名单**）。
⚠ 「把基线改成 1112」是错解 —— 那会把事故洗成「清理」，下次**真删死常量**就看不出来（同阶段 94 的教训）。

**(b) 切进程级全局语言的测试类漏挂集合 ⇒ 打红别人的用例。**
gate1 另两条红是 `EditorCommandCatalogTests.Describe_OfSelectionCommands_IsHumanReadable` /
`..._ComesFromTheDictionary_NotFromTheKeyName`（「Expected: Not "Ungroup"; Actual: "Ungroup"」）。
量化后确认：**与大队同进程 5/5 红、单独跑 5/5 绿** ⇒ 系统性竞态（我新增的判据切全局语言，
污染并发读者；en 字典 `cmd.desc.ungroup` 的英文值恰好与枚举名同形）。
⇒ **这是我走的弯路**：先按「不能与大队同进程」的既有模式改了 `scripts/ci-gate.sh`
（加第三类排除 ＋ 单独跑一段）—— 随后发现项目**已有**既定机制 `[Collection("Language-global")]`
（17 个类已挂）⇒ 正确解法是**补挂集合**，已 `git checkout -- scripts/ci-gate.sh` 回退，
并新增静态判据 `LocalizationRefreshUiTests.EveryTestClassThatSwitchesLanguage_IsInTheGlobalCollection`
（扫 50+ 文件，出现 `SwitchLanguage(` 的类其上一行必须是 `[Collection("Language-global")]`；
**单向**判据：挂而不切合法，不反判）。顺带补挂了三个阶段 94 及更早一直游离的类。
⚠ 复现 `Assert.True(cond, $"…{list[0]}…")` 的坑：**断言消息是立即求值的** ⇒ 绿的时候照样算，
空集合直接 `ArgumentOutOfRangeException`（把一条本该绿的判据变成红）。

### 95.8 读数与对拍（`HMI_READINGS_OUT` **绝对路径**）

| 读数 | 阶段 94 | 阶段 95 |
|---|---|---|
| 无代码引用的常量 | 1113 | **1113**（不动） |
| 分桶 | M2 828 / M6 262 / M3 18 / 无机制 5 | **同左，一字不动** |
| 字典键 | 2254 | **2259**（＋5） |
| 有常量的键 / 没有常量的键 | 1803 / 451 | **1808 / 451** |
| 可达机制 **M1-Lang 常量** 面 | 690 | **695**（＋5） |
| **M13 / M14** 面 | 22 / 7 | **22 / 7**（不动） |
| 直通面 | 15 / 14 | **15 / 14**（**零变化**） |
| 大队八项 | 3193 | **3203**（＋10 = 新增判据 9 条 ＋ 集合静态判据 1 条，**纯净增**） |
| 逐程序集 | `App.UiTests` 505 | `App.UiTests` **515**；其余七项一字不动 |
| 路径卫生 | 656 | **657**（＋1 见下） |

⇒ 直通面**第五次零变化** ⇒ 「**面读数是证词、不是目标**」**第八例**
（前七例 #61a / #61f / #61g / #61i / #61j / #61l / #61m）。
本项**从来不在**那张面上（它收的是「界面语言」，与 `ex.Message` 直通模板无关）。

⇒ 「基线 1113 **不动**、字典 +5」不矛盾：那 5 个常量**都有代码引用**（`ReferenceTextKeys.cs` 在读它们），
不进那个集合；M1 面 +5 与之一致。

⚠ **路径卫生 656 → 657 要能自己解释**（这是我第二次踩同一族）：口径是
`git ls-files 'src/*' 'tools/*' 'scripts/*' 'firmware/*'`（`scripts/check-path-hygiene.sh:37`）——
**不含 `tests/`**。内容批 12 个文件里只有 `App/Services/ReferenceTextKeys.cs`
是在 `src/` 下的**新增**文件 ⇒ **656 + 1 = 657**；
`tests/HmiEditor.App.UiTests/ReferenceDescriptionLocalizationGuardTests.cs` **不进清单**。

**提交前 / 提交后的对拍**：`s91-diffgate.py`（剥「持续时间」多重集）——
gate4 与 gate5 各 **29 行**、**差集只剩这一行**（656 vs 657，成因即上）；
读数 `s92-diffreadings.py` —— `s95-readings2.txt`（提交前）与 `s95-readings3.txt`（提交后）
各 10 行、**差集 0 / 0**。

### 95.9 门禁

| 跑 | 读数 |
|---|---|
| gate1（**红 3**） | `.workbuddy/tmp/s95-gate1.log` mtime **20:25** ⇒ 两条并发红（§95.7b）＋ 一条基线红（§95.7a） |
| 修法 ① / ② | 枚举改名 `ScreenReference` ＋ 补挂 `[Collection("Language-global")]`（含回退对 `ci-gate.sh` 的改动） |
| gate2（**红 1**） | `.workbuddy/tmp/s95-gate2.log` mtime **20:30** ⇒ `ReferenceDescriptionLocalizationGuardTests.ReferenceRows_RenderInEnglish_EndToEnd` 报「英文界面下交叉引用行仍是中文：Control TankLabel bound to variable (WriteOnly)」—— 文本其实是英文却被判含汉字：`Where` 与 `Select` **两次读属性**、两个切语言的类互相切 ⇒ **印证「补挂集合」才是对的**（不是把判据改松） |
| gate3（绿） | `.workbuddy/tmp/s95-gate3.log` mtime **20:32** ⇒ `EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **656** / 大队 **3203** |
| gate4（绿，提交前最终） | `.workbuddy/tmp/s95-gate4.log` mtime **20:35:51** ⇒ 同上读数（656） |
| 内容批 | **`ebca0a9`**（12 files，`2026-09-23T20:37:52+08:00`）⇒ gate4 比提交**早 2 分 01 秒** ✓ |
| gate5（绿，提交后） | `.workbuddy/tmp/s95-gate5.log` mtime **20:39:09** ⇒ **晚于提交 1 分 17 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **657** / 大队 **3203**（逐程序集 44 / 44 / 75 / 87 / 4 / 315 / 2119 / **515**） |
| gate6（绿·文档写入后、提交前） | `.workbuddy/tmp/s95-gate6.log` mtime **20:44:37** ⇒ 同上读数（657 / 3203）；对拍 gate5 ↔ gate6：各 **29 行**、**差集 0 / 0** ⇒ 文档批**不动任何代码路径** |
| 文档批 | **`5b30345`**（4 files，+346 / −5，`2026-09-23T20:44:50+08:00`）—— **只改 `docs/`** ⇒ ⚠ gate6 比该提交**早 13 秒**（跑在文档内容写入后、`git commit` 前）⇒ 按「引用绿之前先核时序」的纪律**不拿它当文档批的证词**，补跑 gate7 |
| gate7（绿·文档批后） | `.workbuddy/tmp/s95-gate7.log` mtime **20:46:05** ⇒ **晚于文档批** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **657** / 大队 **3203** |
| 对拍 gate6 ↔ gate7 | 日志 `s91-diffgate.py`：两跑各 **29 行**、**差集 0 / 0**（`git ls-files` 那条步不吃 `docs/`，故提交不改读数） |
| 记忆批 | **`6055173`**：快照 `docs/archive/2026-09/memory-hmi-full-2026-09-23i.md`（逐字节副本）＋ `MEMORY.md` 收在 **UTF-16 码元 7976 / 8000**（§4.3 整块**析出**到 `docs/2026-09-23-memory-detail-extract.md` **§9**，腾出的空间加三条新契约，**净 −8 码元**）；当日日志追加阶段 95 |
| gate8（回填批后补跑） | ⚠ **约定写法，本批尚未跑**：回填批之后补跑 **gate8**；按阶段 85–94 惯例它**只记** `.workbuddy/memory/2026-09-23.md`、**不回写本文档** |

**阶段 95 的三个提交（内容 → 文档 → 记忆）**：`ebca0a9`（`20:37:52`，12 files，+759 / −26）
→ `5b30345`（`20:44:50`，4 files，+346 / −5）→ **`6055173`**（记忆批）；
gate5 / gate7 依次**晚于**对应的提交（gate8 按上面那条约定处理）。⚠ **gate6 不在这条链上**（它比文档批早 13 秒），
把它列出来是为了说明「**文档内容跑绿 ≠ 文档批已验**」—— 那 13 秒里唯一发生的事是 `git add`。

### 95.10 诚实边界与未闭环

1. **翻译质量不在判据面上**：en 的 `Switch to` / `Run script` / `Script binding` 好不好用，判据断不了。
2. **`Unset` 仍回落成中文**（J7 钉住）：Core 的 `ReferenceLocation` 是**公开类型**、外部可以自己构造
   ⇒ 这是「让忘了填表现为看得见的中文」，不是缺陷。
3. **`BindingDirection` / `ReferenceType` / `ControlType` 仍是枚举名直显**（`WriteOnly` / `Label`）
   ⇒ **#61o 一族**，本刀**刻意不动**（否则「枚举名本地化」会分裂成两套机制 ＋ 违反「一次只放开一个变量」）。
4. **`ReferenceType.ExpressionVariable` 零产出点**（§95.2 ②）⇒ **新开 #61p**；本刀**不删**该成员
   （删它属另一件事，且要先量「有没有历史工程文件读它」）。
5. **`CrossReferenceAnalyzer` 那 5 种中文仍留在 Core**：它们是回落原文 ＋「不经界面」的消费者看到的值
   ⇒ 与「回落必须留在 Core」的全局口径一致。
6. **Core 侧没有为「种类」配判据**：本阶段的判据全在 App（渲染侧）。Core 侧只有
   `ReferenceDescriptionKind` 的**赋值完整性**由 J4 间接覆盖（端到端跑真实分析器）。
   ⚠ 新增一个 Core 站点而忘了赋值 ⇒ J4 会红；新增一个**枚举成员**而忘了接文案 ⇒ **CS8509**（编译期）。

## §96 交叉引用面板的三个枚举名 ＋ 控件类型显示名收敛（闭环 #61o，2026-09-23）

### 96.1 一句话结论

面板一行里**三列**都是英文枚举名（`VariableBinding` / `Label` / `WriteOnly`）—— 阶段 94/95
把这条边界写在 `Views/ReferenceRow.cs` 的类注释里（「一次只放开一个变量」），本阶段一次收掉；
收的过程中牵出**一处真分叉**：「控件类型 → 显示名」这个映射界面上写了两遍，而第 ② 份漏了
`Screen`，于是从工具箱拖「屏幕」建出来的控件默认名是英文 `Screen1`，而它在工具箱里叫「屏幕」。

### 96.2 量的那一遍（读，不预设）—— 登记只写了「两个枚举」

| # | 登记 / 直觉 | 实测 |
|---|---|---|
| 1 | 面板上是**两个**枚举（`ReferenceType` / `ControlType`） | **三个** —— `BindingDirection` 藏在 `Description` 模板 `cross.ref.desc.binding` 的 `{1}` 实参位（`Format` 收 `object[]`，装箱后 ToString） |
| 2 | 枚举成员数「4 ＋ 26」 | 三位合计 **33** 个成员（`ReferenceType` 4 / `HmiControlType` **26** / `BindingDirection` 3） |
| 3 | 控件类型的显示名**只有工具箱一份** | **两份手工表**，且第 ② 份**已分叉**：`ToolboxView.PopulateItems` 26 条全覆盖；`DesignCanvas.BuildControl` 的 `localizedName` **25 支 ＋ `_ => type.ToString()`**，**漏 `Screen`** |
| 4 | 方向键「找一组现成的复用」 | 两组都不合用：`prop.bind.*` 只有 2 条（**缺 `WriteOnly`**）；`tag.monitor.*` 3 条齐全但语义是「标签监视」 |
| 5 | 新类名叫 `ControlTypeNames` | 那个名字已被 `HmiEditor.AI.Inference.PromptTemplateEngine.ControlTypeNames` 占用（**AI schema 里可生成的类型 Id 清单**，不是显示名）⇒ 同名不同义 |

**取证两条**（都是读数，不是印象）：

* `Lang.Tb*` 引用总数 **77 ＝ 26（`TbCat*` 分类）＋ 26（工具箱控件名）＋ 25（画布）**
  ⇒ 控件名键的引用面**就是这两处**、别无他处；也顺带印证了第 ③ 条的「25」。
* 新标识符 × `Lang` 常量名撞车检查（`.workbuddy/tmp/s96-name-clash.py`）：候选 **33 ＋ 7** 个
  （33 个枚举成员 ＋ 4 个候选类名 ＋ 3 个方法名）**全 0 命中** ⇒ 命名安全
  （阶段 95 就吃过这个亏：枚举成员 `ScreenSwitch` 撞了 `Lang.ScreenSwitch`）。

**前置排查**（会不会被既有守卫拦）：行数基线表只守 `MainWindow.axaml.cs`（3014）＋ 两个
partial glob ⇒ 本刀碰的三个文件**都不在**表里；`ToolboxIconDictionaryTests` 读的是
`ControlIcon`（几何）而**不管 `DisplayName`** ⇒ 改显示名不动它。

### 96.3 处置（内容批 `301c11c`，10 files，+693 / −76）

| 改动 | 文件 | 说明 |
|---|---|---|
| 新建 `ControlTypeTextKeys.Describe(HmiControlType)` | `App/Services/ControlTypeTextKeys.cs` | 26 支、**无默认支**、文件级只禁 `CS8524`；键就是工具箱用了很久的 26 条 `tb.*` |
| 工具箱条目名改调出口 | `Views/ToolboxView.axaml.cs` | 26 行，**列对齐保持**（26 行全长不变） |
| 画布默认名改调出口 | `Controls/DesignCanvas.Editing.cs` | 25 支 switch → **一行调用**（−27 行）；原 `_ => type.ToString()` 随之消失 |
| 加 `TypeName` / `DirectionName`，`Describe` 的方向实参改走 `DirectionName` | `App/Services/ReferenceTextKeys.cs` | ⚠ 实参个数**仍是 2** ⇒ `LocalizedFormatCallSiteTests` 的 C1 不受影响 |
| 两个属性改走字典 | `Views/ReferenceRow.cs` | `ReferenceType` / `ControlType`；模板里五个绑定**逐字未动** |
| 7 个新常量 ＋ 两本字典各 7 键 | `Resources/Lang.cs`、`Strings.{zh-CN,en-US}.json` | 4 × `cross.ref.type.*` ＋ 3 × `cross.ref.dir.*` |

**方向为什么新建一族、不复用现成的**（三条一起看才成立）：

1. `prop.bind.*` 只有 2 条，而 `BindingDirection` 有 3 个成员 ⇒ 复用就得**补一条**；
2. 补进 `prop.bind.*` 或借用 `tag.monitor.*` 都会把常量**从「无代码引用」集合里拽出来**
   ⇒ `UnusedBaseline` **1113 → 1111**。那不是「清理」，而是「数字动了」—— 而本项目对基线的
   纪律是「**先查是哪一条、为什么没的，再决定动不动**」，这里代价**没必要付**；
3. `cross.ref.dir.*` 与同面板的 `cross.ref.desc.*` 同族，语义自洽（代价是三组同值键
   —— `prop.bind.*` / `tag.monitor.*` 早已同值，那是既有惯例，不是本刀引入的）。

**为什么 `ControlTypeTextKeys` 没有默认支**：`_ => type.ToString()` 这类兜底会把**新增控件
类型**静默渲染成英文枚举名（用户看到**假文案**，而编译面/字典面/台账面全不响 —— 阶段 92 的
#61j 实测过）。无默认支时新增具名成员 ⇒ **CS8509** 点名 ⇒ 门禁（按 `: warning ` 行判红）当场红。

### 96.4 判据：8 条新 ＋ 1 条**必须跟着改**的旧判据

`tests/HmiEditor.App.UiTests/ReferenceFieldLocalizationGuardTests.cs`（K1–K8）：

| # | 判据 | 钉什么 |
|---|---|---|
| K1 | `EveryControlType_HasItsOwnDictionaryName` | 26 个成员各有其名、**两两不同**（防「都落进同一支」） |
| K2 | `ControlTypeNames_ComeFromTheToolboxValueSet` | 名字来自字典 **`tb.*` 值集**（不写「枚举→键」映射 ⇒ 判据不依赖名单） |
| K3 | `ChineseControlTypeNames_AllCarryCjk` | zh 侧全含汉字 ⇒ 同时钉「没有再直显枚举名」 |
| K4 / K5 | `EveryReferenceType_…` / `EveryBindingDirection_ChangesWithLanguage` | 4 ＋ 3 个成员逐档随语言变 |
| K6 | `ReferenceRows_RenderAllThreeFields_InEnglish` | **端到端**：真实工程 → 真实分析器 → `ReferenceRow` 三列；en 无汉字 **且** zh 有 |
| K7 | `SingleOutlet_IsWiredFromAllThreeCallSites` | **源码面接线**：三个调用点都调出口，且工具箱/画布**不再**出现 `Lang.Tb*` 控件名索引（`Lang.TbCat*` 不在此列） |
| K8 | `SevenNewKeys_AreTranslatedInBothDictionaries` | 7 键两本都翻过、且 zh ≠ en |

⚠ **J2 必须改**（`ReferenceDescriptionLocalizationGuardTests.ChineseRendering_IsByteIdenticalToTheCoreFallback`）：
它逐行断言 `Describe(r) == r.Description`（zh 逐字等于 Core 回落）。方向是**唯一刻意改掉的
中文** —— Core 回落里嵌的是 `WriteOnly`，改动后渲染是「只写」⇒ **不可能再逐字相等**。
期望值因此改成 `ExpectedZh(r)`：模板与方向都**从字典取** ⇒ 两个来源都与被测出口
（`ReferenceTextKeys`）**独立**，仍然不是「拿被测物自己算的当期望」。

### 96.5 负控 3 条（全在真仓库上，还原一律**写回原文** ＋ `sha256` 逐字节核对）

| 负控 | 注入 | 编译 | 红集 |
|---|---|---|---|
| NC-1 | 删 `ControlTypeTextKeys` 的 `Tank` 支 | **能编译**，`CS8509 ×2`（点名 `Tank`） | K1/K2/K3/K6 ＋ **`LangConstantUsageGuardTests` 两条** |
| NC-2 | `ReferenceRow.ControlType` 回退 `ToString()` | 能编译 | K6 ＋ K7 |
| NC-3 | `DirectionName` 回退 `Direction.ToString()` | 能编译 | J2 ＋ K5 |

⚠ NC-1 多出的两条基线红**是在正确工作**：删一支 ⇒ `Lang.TbTank` 失去引用 ⇒
`UnusedBaseline` 1113 → **1114**，判据照实报警。⇒ 按纪律「**多出来的红若在正确工作，改预期、
不改判据**」，本表记的是**实际红集**。三条负控还原 `sha256` 前后一致（`s96-nc.log`）。

### 96.6 🩸 被拦下一次（既有守卫抓到我）

`CS0165`：K1 里 `string[] names;` 在 `InLanguage(..., () => { names = …; })` 的 lambda 内赋值
—— 编译器**不认**「lambda 一定跑过」（`InLanguage` 是普通方法）⇒ 必须先 `Array.Empty<string>()`。
与阶段 90 那条「承接变量：声明在 `try` 内 ⇒ catch 取不到」同族。

### 96.7 读数与对拍（`HMI_READINGS_OUT` **绝对路径**）

| 读数 | 阶段 95 | 本刀 | 归因 |
|---|---|---|---|
| **M1-Lang 常量面** | 695 | **702** | **＋7 ＝ 新增常量数**（逐字吻合） |
| 无代码引用的常量 | 1113 | **1113** | **一分未动** |
| M2 / M6 / M3 / 无任何机制 | 828 / 262 / 18 / 5 | **同** | 不动 |
| 字典键 / 有常量的键 | 2259 / 1808 | **2266 / 1815** | ＋7 / ＋7 |
| 直通面 / M6 面 | 15 · 14 / 373 | **同** | 不动 ⇒ **第九例「面读数是证词、不是目标」** |

⚠ **口径差异**：本刀这次跑了 `LangConstantUsageGuardTests`，它的分区（「无代码引用的 Lang
常量」10 行）在阶段 95 那份读数里**没有** ⇒ 差集里的 10 行是分区口径，不是本刀改动。
两版**共有的关键数**（1113 / 828 / 262 / 18 / 5）**完全一致**。

### 96.8 门禁（回填批：本节每个数字都能指到**一条日志 ＋ 一个时间点**）

**门禁链**（三次跑 / 三个提交；`EXIT` 一律取脚本回显）：

| # | 跑 / 提交 | 日志 · 时间 | 读数 |
|---|---|---|---|
| ① | gate1（绿·内容批提交前） | `.workbuddy/tmp/s96-gate1.log` mtime **21:15:17** | `EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **657** / 大队 **523**（逐程序集 44 / 44 / 75 / 87 / 4 / 315 / 2119 / **523**） |
| ② | **内容批 `301c11c`** | `21:16:31`，10 files，+693 / −76 | 代码在 **21:11–21:13** 落盘（`s96-apply.py` / `s96-build*.log` / `s96-nc.py`）⇒ ① 晚于落盘 ✓；提交本身不改工作树内容，故**不**以「晚于提交」为条件 |
| ③ | gate2（**红 1**·文档批后、记忆批前） | `.workbuddy/tmp/s96-gate2.log` mtime **21:18:58** | 卡在第 5 步：`DispatcherOwnershipContractTests.CrossDispatch_OnlyTheSessionThreadMayHoldTheUIThread`（**已知 #47 族并发红**，消息自点名「派发之外有人持有 `Dispatcher.UIThread`：ownerThread=5，sessionThread=26 … #47 的偷手」）；`App.UiTests` **522 / 523** |
| ④ | #47 单跑 ×3 | `.workbuddy/tmp/s96-47-run{1,2,3}.log` **21:19** | **3/3 绿**（311 / 311 / 307 ms） |
| ⑤ | **文档批 `ebe505f`** | `21:17:51`，4 files，+251 / −1 | 只改 `docs/` ⇒ 它之后**唯一**的绿是 ⑦（③ 红、④ 只覆盖单条用例） |
| ⑥ | **记忆批 `c063c0f`** | `21:19:20`，2 files，+121 | 快照 **23j** ＋ 析出 **§10 / §11**（原 §4.1 两条全文）；`MEMORY.md` 收在 **UTF-16 码元 7979 / 8000**（净 **+3**）—— 见 `.workbuddy/tmp/s96-mem2.py` |
| ⑦ | gate2b（绿·记忆批后） | `.workbuddy/tmp/s96-gate2b.log` mtime **21:20:33** | **晚于记忆批 1 分 13 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **658** / 大队 **523** |

⚠ **③ 那条红为什么不归因本次改动**（四条证据，与阶段 95 同族）：
① 异常正文**自点名机制**（`派发之外有人持有 Dispatcher.UIThread`）；
② 同一条用例在本仓历史门禁日志里红过 **3 次**（`ci-gate-76-post.log` 阶段 76 / `s83-gate3.log`
阶段 83 / `s95-gate8.log` 阶段 95）⇒ 本次是**第 4 次**（`grep -l "…CrossDispatch…[FAIL]" .workbuddy/tmp/*.log`）；
③ **单独复跑 3/3 绿**（④）；
④ 门禁第 5 步是 8 个程序集**并行**跑 ⇒ 正是「`s_uiThread` 清空 → 会话线程重建」窗口最大的跑法。
⇒ 按既定处置「**同一条命令重跑**」（⑦ 全绿），**修法不落地**（#47 是既有决策）。
⚠ 诚实边界：四条只说明「**与本次改动无关**」，**不**说明以后不再犯。

**门禁日志对拍**（`.workbuddy/tmp/s91-diffgate.py`，先剥 `持续时间:`）：

| 对 | 行数 | 差集 | 逐条归因 |
|---|---|---|---|
| `s95-gate8b` ↔ `s96-gate1` | 各 **29** | **3 / 3** | 两个扫描面各 ＋1 个 `.cs`（新文件 `ControlTypeTextKeys.cs`：`.Children` 那步 396 → **397**、「面板事件只经总线消费」127 → **128**）＋ `App.UiTests` 515 → **523**（＋8 ＝ 新判据条数，**纯净增**） |
| `s96-gate1` ↔ `s96-gate2b` | **29 / 28** | **3 / 2** | 实为**两处**：① 路径卫生 657 → **658** —— 成因可核：门禁第 2 步走 `git ls-files`（`scripts/check-path-hygiene.sh:37`）**只列已跟踪文件**，① 跑在新文件 `git add` 之前；② 已知的**换行拼接**噪声 —— 「总共 1 个测试文件与指定模式相匹配。」在某跑里两行被拼成一行（阶段 94 记过同一族，**文本拼接不是结果差异**） |

**读数对拍**（`.workbuddy/tmp/s92-diffreadings.py`，`s95-readings4.txt` ↔ `s96-readings1.txt`）：
**37 / 47** 行、左独有 **1** / 右独有 **11** ⇒ 其中 **10 行是口径差**（本刀多跑了
`LangConstantUsageGuardTests`，它那份「按担保机制分桶」分区在阶段 95 读数里没有，见 96.7）；
**两版共有的关键数 1113 / 828 / 262 / 18 / 5 完全一致**。

⚠ **本节只写到 ⑦（记忆批后）**：按阶段 85–95 惯例，**回填批之后补跑 gate3**，
它**只记** `.workbuddy/memory/2026-09-23.md`、**不回写本文档**（回填批再改本文件就会重开一轮）。

### 96.9 顺带修掉的一条（本刀最值得记的连带收益）

`DesignCanvas.BuildControl` 的 `localizedName` **落盘**（它就是新建控件的默认名）。
收敛前它对 `Screen` 落进 `_ => type.ToString()` ⇒ 从工具箱拖「屏幕」建出来的控件叫
**`Screen1`**，而同一个东西在工具箱里叫「屏幕」。收敛后两边同名。
⚠ 这是**用户可见的落盘变化**：中文界面下新建的容器控件默认名从 `Screen1` 变 **`屏幕1`**。
已登记为**顺带修复**（与 #61j / #61m 的「同一族文案两处实现必然漂移」同族）。

### 96.10 诚实边界与未闭环

1. 翻译质量不在判据面上；K2 只钉「来自 `tb.*` 值集」，不评价措辞好不好。
2. `ReferenceType.ExpressionVariable` **零产出**（#61p）—— 本刀仍给它建了键（分派表按枚举
   成员全建），使「将来开始产出」不会静默回落成英文名。
3. **新开 #61q**：`LocalizedFormatCallSiteTests` 的 `indexerSites >= 700` 下限与本刀收敛后的
   实测值**贴边**（收敛掉 51 处手工索引器）—— 本刀**没动**那条下界（它仍成立），但下次任何
   结构性减少都会撞穿它。**下次动它之前必须先量准真值**。
4. **新开 #61r**：`DesignCanvas` 的默认名**随界面语言变**（中文界面建控件得 `按钮1`、
   英文界面得 `Button1`）⇒ 同一份工程在两种语言下编辑会产生**不同的控件名**（工程文件
   不可复现）。本刀**只让它与工具箱一致**，没解决跨语言不确定性（那要决定「默认名固定用
   哪一种语言」，属另一件事）。

## §97 用户管理 / 模拟器的失败原因收口（#61d：**先量后定类**，只收第一档）— 2026-09-24

### 97.1 一句话结论

登记表把 **#61d** 那 7 个键统一记成「异常原文直通」、并特别注明「**待读，不许定类**」。
本刀逐站读了产出方之后，实测它们**不是一类，是三档**；本刀只收**第一档（真异常路径、
产出方在本仓库）**：4 个抛点换成**带码异常**（`UserAdminException` 两条语义 /
`SimulatorNotConnectedException` 一条语义，合计 4 个抛点），2 个视图的 **4 处**站点改走
`ExternalText.FailureReason(ex)`（其中 1 处**原本连 `catch` 都没有**，是顺手修掉的真缺陷）。
另外两档（**返回值无原因**档 / **死 catch** 档）**只登记、不动** —— 照实写在 §97.9。

### 97.2 量的那一遍（读，不预设）—— 登记写的「7 个键同类」不成立

| # | 键 · 站点 | 产出方实测（读代码） | 档 |
|---|---|---|---|
| 1 | `recipe.clone.failed` · `RecipeEditorView.axaml.cs` | `_recipeManager.CloneRecipe` → `AddRecipe` 返回 `bool`、**从不抛** ⇒ `catch (ArgumentException)` 是**宽捕获**，接不到任何真失败原因 | **②** |
| 2 | `login.failed` · `LoginDialog.axaml.cs` | `AuthenticationService.Login` 对**每一种**真失败（用户不存在 / 锁定 / 停用 / 过期 / 密码错）都 `return null` ⇒ 站点**根本拿不到原因**；只有两行 `ThrowIfNullOrWhiteSpace` 会抛 | **②** |
| 3 | `sim.reg.read.failed` · `SimulatorView.axaml.cs` | `CommunicationSimulator.ReadRegister` 抛**英文硬编码** `InvalidOperationException("Simulator not connected")` ⇒ 中文界面下日志/提示里混英文 | **①** |
| 4 | `sim.reg.write.failed` · `SimulatorView.axaml.cs` | 同上（`WriteRegister`，与 ③ 是**同一档码**的两个抛点） | **①** |
| 5 | `comm.test.failed` · `CommunicationConfigView.axaml.cs` | 驱动校验**本来就返回 issues**（`{0}` 现在拿的是 issue 文本）⇒ 原因到得了界面，**不属本族** | 排除 |
| 6 | `user.mgr.create.failed` · `UserManagerView.axaml.cs` | `CreateUser`（角色未注册）/ `SetUserPassword`（密码过短）抛**自建中文** `ArgumentException` ⇒ 中文界面下是**开发者口吻**、英文界面下照旧中文 | **①** |
| 7 | `status.reload.failed` · `MainWindow.axaml.cs` | `catch` 包的是 `Dispatcher.UIThread.Post` —— **入队不抛** ⇒ 死 catch；真正的重建异常在回调里**无人接** | **③** |

⚠ **顺手量出的一处真缺陷**：`UserManagerView` 里**第二个** `SetUserPassword` 站点（重置密码）
**一个 `catch` 都没有** ⇒ 密码过短时异常直接冒泡出事件处理器（界面无提示、可能连带掉窗）。
本刀与创建用户那一站**一起收口** —— 「同一产出方的两条路径一起收，不许只收一半」。

**判档依据**（不是启发式）：①②③ 的分界是「**产出方会不会给原因**」：
会抛（①）⇒ 给码 ＋ 换出口；只 `return`（②）⇒ 要的是「给失败档建码」，
**换出口是零收益**（站点拿不到原因，换什么都一样）；不会抛（③）⇒ 连那条 `catch` 都是摆设。

### 97.3 处置（内容批 `be8dbde`，18 files，＋1053 / −39）

| 改动 | 文件 | 说明 |
|---|---|---|
| 新码表 ＋ 记录体 ＋ 两个工厂 | `Core/Services/UserAdminIssue.cs` | `ROLE_NOT_REGISTERED` / `PASSWORD_TOO_SHORT`；值与其它族同约定：**不带键前缀** |
| 新带码异常 | `Core/Services/UserAdminException.cs` | **刻意继承 `Exception` 而非 `ArgumentException`** —— 见下面那条 |
| 新码表（单码） | `Simulator/SimulatorIssueCodes.cs` | 与异常**分居两类**：回落住异常内部、且是 `private const`，否则 M15 反射会把它当成**第二条码** |
| 新带码异常 | `Simulator/SimulatorNotConnectedException.cs` | `sealed`、`Code => NOT_CONNECTED`、`Message` ＝ 中文回落 |
| 两个抛点换类型 | `Core/Services/AuthenticationService.cs` | 角色未注册 / 密码长度不足；⚠ `ThrowIfNullOrWhiteSpace` 那两档**保持原样** |
| 两个抛点换类型 | `Simulator/CommunicationSimulator.cs` | 读 / 写各一处 |
| 两条新出口 ＋ `FailureReason` 两支 | `App/Services/ExternalText.cs` | `UserIssueKey` / `SimulatorIssueKey`（前缀 `user.issue.` / `sim.issue.`），键推导**单一来源** |
| 创建用户改出口 ＋ 重置密码**补 catch** | `App/Views/UserManagerView.axaml.cs` | 第二处是新接线（新键 `user.mgr.pwd.reset.failed`） |
| 两个站点改出口 | `App/Views/SimulatorView.axaml.cs` | ＋ `using HmiEditor.App.Services;`（第一次编译就是 `CS0103` 当场点出来的） |
| ＋1 常量 / 两本字典各 ＋4 键 | `Resources/Lang.cs`、`Strings.{zh-CN,en-US}.json` | 字典键 **2266 → 2270**（逐行插入，不重写 JSON） |

**为什么 `UserAdminException` 不继承 `ArgumentException`**（两条一起看）：
① 语义上它不是「调用方编程错误」（那两档是 `ThrowIfNullOrWhiteSpace`，仍保持 `ArgumentException`）；
② 继承它会让 `catch (ArgumentException)` **继续命中** ⇒「站点已收口」这件事在**编译面上**看不出来
（本项目登记过的失效模式）。换类型的代价是编译器**当场点出全部消费点**，这正是要的：
实测 2 个 `catch` 站点 ＋ **5 处** `Assert.Throws<ArgumentException>`（`SecurityConfigConsumptionTests`）
＋ **1 处** `Assert.Throws<InvalidOperationException>`（`CommunicationSimulatorTests`）。

**字典键的两处刻意不同**（别以为「两边该一致」）：
`user.issue.password_too_short` 与 `sim.issue.not_connected` 的 zh 值**逐字等于回落原文**
⇒「用户所见零变化」是设计目标；而 `user.issue.role_not_registered` 的 zh 值**刻意不同** ——
回落里那句「请先 RegisterRole 或修正 SecurityConfig.DefaultRoleId」是**对开发者说的**，
界面文案改成用户口吻 ⇒ 这是**用户可见变化**，由判据显式钉住。

### 97.4 判据：8 条新 ＋ 2 条**必须跟着改**的旧判据

`tests/HmiEditor.App.UiTests/UserAdminIssueLocalizationGuardTests.cs`：

| # | 判据 | 钉什么 |
|---|---|---|
| 1 | `EveryFailure_RendersTheDictionaryTemplate_InBothLanguages` | 主判据：4 条真实失败 × **两种语言**，渲染逐字等于「该语言字典模板填上真实参」 |
| 2 | `BothDictionaries_CarryEveryCode_AndAreActuallyPaired` | 两本字典齐 ＋ en 无汉字 ＋ zh ≠ en ＋ **占位符个数 == 实参个数** |
| 3 | `DeclaredCodes_AndProducedCodes_AreTheSameSet` | 码表**双向闭包**（声明了产不出 / 产出了没声明） |
| 4 | `Producers_ThrowTheCodedException_NotABareOne` | 两个产出方**不许退化**：带码抛出**恰好 2 ＋ 2 处**，且不许再拼裸中文/英文模板 |
| 5 | `Sites_RenderThroughTheCodedOutlet` | 两个视图各**恰好 2 处** `FailureReason(ex)`，且不许留 `, ex.Message)` |
| 6 | `BareException_StillFallsBackToTheRawMessage_…` | 诚实边界正面断言：**没码的照旧回落** ⇒ 码才是收益的唯一来源 |
| 7 | `PasswordFallbackMatchesZh_WhileRoleFallbackDeliberatelyDoesNot` | 上面那条「两处刻意不同」 |
| 8 | `NegativeControls_EachBreakIsCaughtByTheRightJudgment` | 5 组负控（纯函数注入，不动真仓库） |

**必须跟着改的两条**：

| 旧判据 | 为什么必须改 |
|---|---|
| `ExceptionMessagePassthroughFaceTests` 基线 | 3 站不再直通 `ex.Message` ⇒ 面 **15 站 / 14 组合 → 12 站 / 11 组合**。基线是**双向**的定量清单，不删这 3 行会报「已消失」 |
| `CommunicationSimulatorTests.CommSim_Disconnect_ThrowsOnRead` | 断言的是 `InvalidOperationException` ⇒ 换类型后**当场红**（这正是换类型的取证价值）。`AuthenticationService` 侧同理：5 处 `Assert.Throws<ArgumentException>` 一起改 |

**可达性机制新增 M15**（`LocalizationKeyReachabilityGuardTests`）：面 **3**（＝ 2 个用户管理码
＋ 1 个模拟器码），下界取**满值 3**；取数**复用**本判据的 `DeclaredCodes()` /
`DeclaredSimulatorCodes()`，不另抄一份反射。

### 97.5 负控 5 条（全在真仓库上，还原一律**写回原文** ＋ `sha256` 逐字节核对）

| 负控 | 注入 | 编译 | 红集 |
|---|---|---|---|
| NC-1 | `AuthenticationService` 密码档退回裸 `ArgumentException` ＋ 裸中文模板 | 能编译 | 5 红 / 3 绿：`Producers_…`（**两条**都点名）＋ 4 条走取数的判据（「逃逸」那一格点名 `抛了 ArgumentException`） |
| NC-2 | `CommunicationSimulator.ReadRegister` 退回英文硬编码 `InvalidOperationException` | 能编译 | 5 红 / 3 绿：`Producers_…`（「出现 1 次，应为 2」＋「又出现英文硬编码」）＋ 4 条取数判据 |
| NC-3 | `SimulatorView` 读取站点退回 `ex.Message` | 能编译 | 3 红 / 8 绿：`Sites_…`（**两条**：出现 1 次 ＋ 仍有 `, ex.Message)`）＋ `ExceptionMessagePassthroughFaceTests`（**新增** `sim.reg.read.failed`）＋ 负控自身 |
| NC-4 | en-US 字典摘掉 `sim.issue.not_connected` | 能编译 | 4 红 / 10 绿：主判据（**逐条点名**「字典里没有 … ⇒ 该语言下读到的中文回落原文」）＋ 成对性 ＋ 负控自身 ＋ `BothDictionaries_HaveTheSameKeySet` |
| NC-5 | 摘掉可达性 M15 取数段（**保留下界行**） | 能编译 | 2 红 / 4 绿：主判据**逐条点名 3 个键** ＋「`M15-用户/模拟器码：实测 0 < 下界 3`」 |

### 97.6 🩸 被拦下四次（三次我抓到我、一次我抓到我自己）

1. **主判据第一版比错了对象**：照 `SvgParse` 族的模板比「渲染 == **原始**字典模板」——
   那一族全是 0 实参所以碰巧对，本族带实参 ⇒ 永远对不上。改成
   `Expected(dict, key, args)`（与 `PackageIssue` / `ExpressionSyntaxIssue` 两族同形）。
2. **毒化探针在本族证明不了东西**（换掉探针的那条理由值得单记）：① `SimulatorNotConnectedException`
   的回落是**类内 `private const` ＋ `sealed`** ⇒ 毒化构造不出来（**不为测试开放生产 API**）；
   ② 更要紧：两档的 zh 字典值**刻意等于**回落原文 ⇒ 在中文那一遍里
   「渲染 == 回落」与「渲染 == 字典值」是**同一件事**，毒化探针恒真。
   ⇒ 换成**两种语言各渲染一遍**逐字比对：**en 那一遍才是关键见证**（en 值不含汉字、
   而回落必是中文 ⇒「en 渲染 == en 字典值」本身就排除了「回落」这条路）。
3. **负控残留把门禁跑在了注入态上**（本刀最该记的一条）：NC-5 注入后**漏了配对 `--restore`**，
   于是 `git add -A` ＋ 门禁跑的都是**注入版**（M15 被摘）。症状很好认 ——
   日志里 `M15-用户/模拟器码：实测 0 < 下界 3`，而那一跑本该是「绿基线」。
   ⇒ 规矩：**注入型负控脚本必须「一注一还」，且在 `git add` 之前核 `grep 注入标记`**；
   已把这条写进负控脚本的用法说明（`--inject` 之后**必跟** `--restore`）。
   ⚠ 附带收益：那一跑暴露了下面第 4 条，以及两条**真红**（见 97.6b）。
4. **由 ③ 顺带量出的一条 M6 过覆盖面**：NC-5 第一次跑时只点名了 **2** 个键
   （少 `sim.issue.not_connected`）—— 因为我自己写的**断言消息**里有这个键的**字符串字面量**，
   M6（源码字面量）把它判成了「有人取过」。M6 只剥**注释**、不区分「字面量在哪儿」
   ⇒ 改掉措辞后 NC-5 点名 **3** 个键。已登记为新开项（见 §97.9）。

**97.6b 由 ③ 那一跑（注入态）暴露的两条真红** —— 都是本刀改动的**真实后果**，不是噪声：

| 真红 | 症状 | 处置 |
|---|---|---|
| `HmiEditor.Simulator.Tests.CommunicationSimulatorTests.CommSim_Disconnect_ThrowsOnRead` | 断言 `InvalidOperationException`，实得 `SimulatorNotConnectedException` | 改**期望类型**（单跑 **15 / 15 绿**） |
| `HmiEditor.Core.Tests.CoreVisibleTextInventoryGuardTests.NoUnclassifiedCoreChinese` | 报 `+ Services/UserAdminIssue.cs: 登记 0，实扫 2` | 台账**加新通道** ＋ 总数 **85 → 87**（单跑 **10 / 10 绿**） |

⚠ 前者是**换类型的取证价值**的又一次实证（编译器/判据当场点名消费点）；
后者说明「**确实往 Core 里加了新的中文**」—— 与阶段 93/94 的「搬迁 / 归类」不同，
本刀加的是**真新增**，台账必须涨。

### 97.7 读数与对拍（`HMI_READINGS_OUT` **绝对路径**：`.workbuddy/tmp/s97-readings3.txt`）

| 读数 | 阶段 96 | 阶段 97 | 归因 |
|---|---|---|---|
| **M15-用户/模拟器码 面** | — | **3** | 本阶段新增机制（2 个用户管理码 ＋ 1 个模拟器码） |
| M1-Lang 常量面 | 702 | **703** | ＋1 ＝ 新增的 `Lang.UserMgrPwdResetFailed` |
| 字典键（zh）/ 有常量的键 / 没常量的键 | 2266 / 1815 / 451 | **2270 / 1816 / 454** | ＋4 / ＋1 / ＋3（4 个新键里只有 `user.mgr.pwd.reset.failed` 有常量，另 3 个键由出口**推导**） |
| 无代码引用的常量（`UnusedBaseline`） | 1113 | **1113** | **不动** —— 3 个码**没有** `Lang` 常量；新增的那 1 个常量**有代码引用** |
| 分桶 M2 / M6 / M3 / 无任何机制 | 828 / 262 / 18 / 5 | **同** | 不动 |
| 直通面 | **15 站 / 14 组合** | **12 站 / 11 组合** | −3 站（`sim.reg.read.failed` / `sim.reg.write.failed` / `user.mgr.create.failed` 改走出口）—— **本族第六次真的把它改小**（前五次见 #61b / #61h） |
| M6 面 | 373 | **373** | 不动（无新增/删除源码字面量；⚠ 但它的**口径**在本刀被量出了一处过覆盖，见 #61s） |
| 机制条数 | 14 | **15** | ＋M15 |

⚠ **一处纯措辞修正**（数字一字未变）：M6 分区里那句「残差键（**前六条**机制没覆盖）」
从阶段 91 起就陈旧（那时清单里已有 14 条）⇒ 本刀改成「**前面各条**机制没覆盖」；
另把可达性读数的标题从**写死**的「十二条机制的面」改成 `{byKind.Count}` **计算得出**
（连同判据里 **8 处**硬编码的机制条数一起处理，这一类陈旧从此不会再出现）。

**读数对拍**（`.workbuddy/tmp/s92-diffreadings.py`，`s96-readings1.txt` ↔ `s97-readings3.txt`）：
**47 / 59** 行、左独有 **10** / 右独有 **22**，逐条可归因 ——
左独有的 10 行里 **2 行是纯措辞**（残差键那句、机制条数标题），**8 行是数字/条目变化**
（M1 702→703、字典键 2266→2270、有常量 1815→1816、没常量 451→454、直通面 15·14→12·11、
以及被本刀摘掉的 3 条直通站点行）；右独有的 22 行 ＝ 上述变化的新值 ＋ 本判据新增的
**3 个分区**（抛出形状 / 失败原因 × 8 条渲染 / 码表 3 个）。

### 97.8 门禁（回填批：本节每个数字都能指到**一条日志 ＋ 一个时间点**）

| # | 跑 / 提交 | 日志 · 时间 | 读数 |
|---|---|---|---|
| ① | gate1（**红 1**·且跑在**注入态**上 —— 见 97.6 ③） | `s97-gate1.log` mtime **11:32:03** | 卡在第 5 步：`HmiEditor.Device.Tests.DeviceChannelTests.SlowFirstByte_…`（**非本轮**，见下）；**其余程序集全绿**：Simulator **44** / Core **2119** / App.UiTests **531** / AI 315 … ⇒ 本刀改动面**一条没红** |
| ② | gate2（绿·内容批提交**前**） | `s97-gate2.log` mtime **11:37:04** | `EXIT=0` / **7 步全过** / 0 警告 0 错误 / 路径卫生 **662** / 三段合计 **3229**（44 / 44 / 75 / 87 / 4 / 315 / 2119 / 531 ＋ 8 / 2） |
| ③ | **内容批 `be8dbde`** | `2026-09-24T11:37:24+08:00`（`git log --date=iso-strict`），18 files，＋1053 / −39 | 代码在 **11:2x–11:31** 落盘 ⇒ ①② 晚于落盘 ✓ |
| ④ | gate3（绿·**晚于提交**） | `s97-gate3.log` mtime **11:38:41** | **晚于提交 1 分 17 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **662** / 三段合计 **3229** |
| ⑤ | **文档批 `3081f9b`** | `2026-09-24T11:41:32+08:00`，4 files（**只改 `docs/`**），＋368 / −2 | 只改文档 ⇒ 它之后唯一的绿是 ⑦ |
| ⑥ | **记忆批 `2224151`** | `2026-09-24T11:43:22+08:00`，2 files，＋182 | 快照 **24k** ＋ 析出 **§12 / §13**；`MEMORY.md` 收在 **UTF-16 码元 7908 / 8000**（净 **−71**） |
| ⑦ | gate4（绿·记忆批后） | `s97-gate4.log` mtime **11:44:54** | **晚于记忆批 1 分 32 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **662** / 三段合计 **3229** |

**门禁日志对拍**（`.workbuddy/tmp/s91-diffgate.py`，`s96-gate2b` ↔ `s97-gate3`，各 **28** 行，**差集 3 / 3**，
三条**全部可归因、且都是纯净增**）：

| 差集行 | 左（阶段 96） | 右（阶段 97） | 归因 |
|---|---|---|---|
| `.Children` 扫描面 | 397 个 `.cs` | **401** | ＋4 ＝ 本刀新增的 4 个源文件（2 × Core ＋ 2 × Simulator） |
| 路径卫生 | 658 个文件 | **662** | 同上（该步走 `git ls-files`，口径**不含 `tests/`**） |
| `App.UiTests` | 523 | **531** | ＋8 ＝ 新判据的**8 条 fact** |

**关于 ① 的那条红**（四条证据 ＋ 一条反证，与阶段 8 / 95 / 96 的 #47 族同一套判定法）：

1. **该程序集本轮零改动** —— `be8dbde` 的 18 个文件里没有任何 `HmiEditor.Device` 文件；
2. 失败形状自述 = 期望 `ok:AAA` 实得**空串**，即「首字节窗口内没等到回包」；
3. **同一条用例单独复跑 5 / 5 绿**，稳定 **955 ms**（`.workbuddy/tmp/s97-dev{1..5}.log`）；
4. **同一条命令重跑两次全绿**（② / ④），且**同一程序集耗时从 38 s 掉到 3 s** ——
   这是「负载差异」的直接读数，不是推测。
⇒ 归因：门禁第 5 步是 8 个程序集**并行**跑，全量负载把 `LineServer` 的 600 ms 回包延迟
膨胀到**首字节窗口 5 000 ms 之上** ⇒ **正确的实现被判成「命令无输出」**。
⚠ 这是本仓**第二次**在门禁里看到这条用例假红：第一次是 2026-09-16（阶段 8），
那次的处置是把首字节窗口从 1000 ms 拉到 **5 000 ms**（`DeviceChannelTests.cs:135-146` 的注释
记着这件事）—— **这次 5 000 ms 也被吃掉了**（膨胀系数 > 8.3）。
⚠ **诚实边界**：这四条只说明「**与本次改动无关**」，**不**说明它以后不再犯；
本刀**未动**这条判据（改它属另一件事），也**不**把「重跑绿」当成修复。
**gate3 ↔ gate4 对拍**（`s91-diffgate.py`）：**各 28 行、差集 0 / 0** —— 记忆批只改 `docs/`
与不笔记（`.workbuddy/memory/` 按约定**不入库**）⇒ 门禁输出**逐行相同**。这既是「文档 / 记忆批
不动行为」的正面证据，也顺带说明门禁第 2 步的**路径卫生口径不含 `docs/`**（新增了一个
`docs/archive/…` 文件而读数仍是 662）。

⚠ 本节写到 ⑦（记忆批后）。按阶段 85–96 惯例，**回填批之后补跑 gate5**，它**只记**
`.workbuddy/memory/2026-09-24.md`、**不回写本文档**（回填批再改本文件就会重开一轮）。

### 97.9 诚实边界与未闭环

1. **本刀只收第一档**。另外两档照实留在这里，别把本判据读成「#61d 已经清完」：
   * **② 返回值无原因档**（`login.failed` / `recipe.clone.failed`）：产出方**只 `return null`**，
     站点**看不到原因** ⇒ 正确动作是「给失败档建码」（`LoginResult` / `CloneResult`），
     **不是**把 `ex.Message` 换成 `FailureReason(ex)`（那是零收益的改名）；
   * **③ 死 catch 档**（`status.reload.failed`）：`catch` 包的是 `Dispatcher.UIThread.Post`
     （入队不抛）⇒ 那条 `catch` 永不触发，而真正的重建异常在回调里**无人接**。
     这既是「文案不本地化」、也是「**异常被吞**」—— 后者更严重，处置方式不同
     （要挪 `catch` 进回调）。
2. **中文那一遍不具备见证力**（主判据的诚实边界）：`user.issue.password_too_short` 与
   `sim.issue.not_connected` 的 zh 字典值**刻意等于**回落原文 ⇒ 中文那一遍里
   「键存在」与「键缺失」渲染结果相同。**见证靠 en 那一遍**（en 值不含汉字、
   而回落必是中文），两本字典的 en 值由成对性判据双向钉住。
3. **新开 #61s**：M6（源码字面量，代理机制）会把**非注释的字符串字面量**也算成「有人取过」——
   本刀实测：自己写的**断言消息**里有 `sim.issue.not_connected` ⇒ 摘掉 M15 的负控里
   那个键**仍被判可达**。这类「过覆盖」只在主机制缺席时暴露（正是摘机制负控的场景）
   ⇒ 摘机制那类负控可能被它**遮蔽**。本刀只改掉自己的措辞，**没**动 M6 的语料口径。
4. **本刀没碰 `#61d` 之外的任何键**：`comm.test.failed` 被**排除**（原因本来就到得了界面）、
   `login.failed` / `recipe.clone.failed` / `status.reload.failed` 三个键的**文案一字未动**
   ⇒ 直通面从 15 站只掉到 **12 站**，不是掉到 8 站。
5. **翻译质量不在判据面上**：主判据只钉「渲染逐字等于字典模板 ＋ 真实实参」，
   不评价措辞好不好。

## §98 配方克隆失败档建码 ＋ 面板重建的失败处理搬进回调（#61t ② / #61u：**量出来后剩下的两档一起收**）— 2026-09-24

### 98.1 一句话结论

阶段 97 把 #61d 的 7 键**先量后定类**、只收掉第一档（真异常路径），把另外两档照实留在
§97.9 里。本阶段收掉那两档：

| 档 | 键 | 定性（阶段 98 读完产出方后） | 处置 |
|---|---|---|---|
| **② 返回值无原因** | `recipe.clone.failed` | ⚠ 比登记里写的更重：不只是"拿不到原因" —— `CloneRecipe` 的签名是 **`public Recipe`（声明非空）** 而失败档 `return null!`，**且丢掉了 `AddRecipe` 的 `bool`**（「Id 已存在」的守卫）⇒ 撞车时界面提示**「已克隆」（撒谎）**、源配方失效时界面**一声不响** | **返回值带码**：`RecipeIssueCodes` 2 码 ＋ `RecipeCloneResult` 两工厂 ＋ `ExternalText.Describe` ＋ 站点补上**原本不存在**的失败档分支 |
| **③ 死 catch** | `status.reload.failed` | ⚠ 确认是**两层病**：表层文案不本地化、**里层异常被吞**（重建失败 ⇒ 状态栏**永久停在「正在重载面板...」**） | **把重建连失败处理一起搬进回调**；出口 `Wiring.Panels.TryRebuildPanels` / `RebuildFailureText` |

⚠ **本刀不是"面变小"型收口**：直通面 **11 组合 / 12 站点 —— 条数一个没动**
（`status.reload.failed` 那一行**换了文件**：`MainWindow.axaml.cs` → `Wiring.Panels.cs`）。
收益全在「失败档从**撒谎/静默**变成**带码**」。⇒ **第十例「面读数是证词、不是目标」**
（前九例见 #61a / #61f / #61g / #61i / #61j / #61l / #61m / #61n / #61o）。

### 98.2 量的那一遍（读，不预设）—— 登记里的两处定性被改写

阶段 97 的 §97.9 只写了「产出方走 `return null`，站点看不到原因」，那是**推断**（只读了签名）。
本阶段按同一套纪律先**读现场**，两处都读出了**更重**的东西：

| 登记里的写法 | 实读产出方/站点后 |
|---|---|
| ② 「产出方只 `return null`、站点看不到原因」 | ① `CloneRecipe` 返回类型**声明非空**、失败档 `return null!` ⇒ **消费点连 `else` 都没写**；②它**丢掉了 `AddRecipe` 的 `bool`** —— 那个 `bool` 是作者专门为「Id 已存在」写的守卫（`Recipe.Id` = `Guid[..8]`，32 位）⇒ **撞车时报「已克隆」** |
| ③ 「那是一条**死 catch**，真异常在回调里无人接」 | 确认，且**补上后果**：重建失败时状态栏**永久停在「正在重载面板...」**（不是"显示原文"，是**什么都没有**）。⚠ 与 #61b 里 `status.reload.failed` 的**证伪记录**对着读：阶段 84 已量出它**不是 IO 失败**（那是 Dock **布局重组**失败）⇒ 别再按 IO 思路做 |
| #61t ① `login.failed` | ⚠ **本阶段没收它**，但量的过程中发现它另有一条**缝**：站点用 `IsNullOrEmpty` 而服务端是 `ArgumentException.ThrowIfNullOrWhiteSpace` ⇒ **纯空白输入会走异常路**、把 BCL 英文原文原样显示出来（中英界面下都是英文）。这条路**本身就是一条「英文原文直通」**。已顺手补上（校验对齐），但 `Login` 对**每一种**真失败仍 `return null` ⇒ 拆出 **#61v** |

### 98.3 处置（内容批 `549fb29`，15 files，**＋1470 / −35**）

**② Core 侧（新的码族）** —— 新增 `src/HmiEditor.Core/Services/RecipeIssue.cs`：

* `RecipeIssueCodes`：`SOURCE_MISSING` / `REJECTED`（**值不带键前缀**，前缀由 App 侧拼）；
* `RecipeCloneIssue`：三段式 `(Code, Message, Args)` —— `Message` 是**中文回落原文**，
  同时是排障信息载体（`SOURCE_MISSING` 那档带着「列表可能已过期」这种给作者的提示）；
* `RecipeCloneResult`：`Recipe?` 与 `Issue?` **恰好一个非空**，用 `Ok` / `Failed` 两个工厂保证。

`RecipeManager.CloneRecipe` 的签名从 `public Recipe` 换成 `RecipeCloneResult` ——
⚠ **改返回类型＝取证手段**：编译器当场把所有消费点摆出来（本刀只有 1 处，见下）。
`if (!AddRecipe(clone)) return RecipeCloneResult.Failed(RecipeCloneIssue.Rejected(clone.Id));`
—— **那个 `bool` 从此不再被丢**。

**② App 侧（出口 ＋ 站点）**：

* 出口：`ExternalText.RecipeIssueKeyPrefix = "recipe.issue."` ＋ `Describe(RecipeCloneIssue)`
  —— 与 `Describe(SvgParseError)` 同形（**返回值档**，不走 `FailureReason`）；
* 站点：`RecipeEditorView.OnCloneRecipe` 补上 `else if (result.Issue is { } issue)` 分支
  ——**改动前这里什么都没有**；
* `catch` 保留并写明理由：它包住的是 `RefreshGroups` / `RefreshRecipes` 两行 **UI 刷新**，
  产出方给不出码 ⇒ 换出口是**改名式假闭环**，**刻意留**（直通面那一行也是因此留着）。

**③ 面板重建**：

* `MainWindow.OnDockLayoutChanged` 只留「写 `正在重载面板` ＋ 入队」，
  新增 `PostPanelRebuild(bool rebuildAgain)` 把**重建与失败处理都放进 lambda**；
  ⚠ `MainWindow.axaml.cs` 有 **3014 行**的精确基线（`SingleFileSizeGuardTests`）
  ⇒ 改写**行数中性**（实测仍 3014）；
* 出口落在 `Wiring.Panels.cs`（**不在行数基线的覆盖范围里**）：
  `TryRebuildPanels(Action rebuild, Action<string> onError)` ＋ `RebuildFailureText(Exception)`。
  做成**静态 ＋ 两个显式依赖**是**为了判据**：它喂一个会抛的委托即可，
  **不必驱动 Dispatcher**（Avalonia headless 派发有既知死区，见 `docs/…headless-*`）；
* ⚠ **刻意保留 `ex.Message`**（见 98.9 第 2 条）。

**字典**：`recipe.issue.rejected` / `recipe.issue.source_missing` × 两本（2270 → **2272**），
按字母序插在 `recipe.group.list` 之后；逐行手术（`assert count == 1`）＋ JSON 校验 ＋
两本键集一致校验 + 回读 `sha256` 核对。

### 98.4 判据：12 条新 ＋ 3 条**必须跟着改**的旧判据

**新增 `tests/HmiEditor.App.UiTests/RecipeCloneIssueLocalizationGuardTests.cs`（8 条）**：

| # | 判据 | 钉住什么 |
|---|---|---|
| 1 | `EveryFailure_RendersTheDictionaryTemplate_InBothLanguages` | 主判据：**毒化 ＋ 两语言各渲染一遍**双见证 |
| 2 | `RealFailures_InEnglish_CarryNoChinese` | **收益的正面见证**：英文界面下不许再出现中文（用 `Cjk` 当尺子、不写死措辞） |
| 3 | `BothDictionaries_CarryEveryCode_AndAreActuallyPaired` | 齐 ＋ 成对 ＋ **占位符个数 == 实参个数** |
| 4 | `DeclaredCodes_AreEitherProducedOrExplicitlyNotProducible` | **三态闭包**（声明 = 真实产出 ∪ 明确产不出） |
| 5 | `Producer_ReturnsCodedResult_AndConsumesTheAddGuard` | 产出方形状：返回类型 / 无 `return null!` / **`AddRecipe` 的 bool 没被丢** / 两个工厂各恰 1 次 |
| 6 | `Site_RendersThroughTheCodedOutlet` | 站点恰好 1 处码化出口 ＋ 有失败档分支 ＋ 无旧形状 `if (clone != null)` |
| 7 | `IssueWithoutAKey_StillFallsBackToTheRawMessage` | 诚实边界正面断言：没码的照旧回落 |
| 8 | `NegativeControls_EachBreakIsCaughtByTheRightJudgment` | NC0–NC5 |

⚠ **主判据为什么是"毒化 ＋ 两语言"两条一起判**（本族与自己前面几族的分叉点）：

* **毒化**（`Poison` 塞进 `Message`）见「没走回落」—— 与 `SvgParseIssue…` 同形；
  ⚠ 本族不能靠"等于回落原文"来判（§97 那两档的**设计目标**是"用户所见零变化"），
  而本族的 zh 字典值与回落原文**措辞不同**（`克隆被拒绝：新配方 ID {0}…` vs
  `克隆被拒绝: 新 Id {0}…`）⇒ 两条尺子都能用，索性都上；
* **两语言期望串比对**（阶段 97 立的做法）见「实参真的被填进去了」——
  `LocalizationService.Format` 在**占位符与实参个数不符时不抛异常**，用户会看到字面的 `{0}`。

**新增 `tests/HmiEditor.App.UiTests/PanelRebuildFailureUiTests.cs`（4 条）**：

| # | 判据 | 钉住什么 |
|---|---|---|
| 1 | `OnFailure_ReturnsFalseAndWritesTheLocalizedReason_InBothLanguages` | **收益正面见证**：失败 ⇒ 返回 `false` ＋ 写出该语言的 `status.reload.failed` 填真实原因 |
| 2 | `OnSuccess_ReturnsTrueAndWritesNothing` | 让 `false` 那条路径的返回值**有意义**（永远返回 `false` 的实现会在这里红） |
| 3 | `ProductionPath_WiresTheRebuildThroughTheHelper` | **接线形状**：切片（`OnDockLayoutChanged` → `PostPanelRebuild` 末行）里必须有必需接线、**不许裸调**、**不许出现 `try`** |
| 4 | `NegativeControls_EachBreakIsCaughtByTheRightJudgment` | NC0–NC5（合成语料） |

⚠ 行为与形状**缺一不可**：行为判据只证明 helper 是对的，**证明不了生产路径在用它** ——
把 `PostPanelRebuild` 改回裸 `RebuildAllPanels()`，helper 还在、行为判据照样绿。
（这也正是 NC-p4 注入的东西。）

**改 3 条旧判据**：

| 判据 | 改动 | 为什么必须跟着改 |
|---|---|---|
| `ExceptionMessagePassthroughFaceTests` 基线 | `status.reload.failed` 那一行**换文件**（`MainWindow.axaml.cs` → `Wiring.Panels.cs`） | 面是**双向**的：站点搬了家，不换行就是"新增 1 ＋ 变少 1"两条红。⚠ 这一行**不许删** —— 那一处 `ex.Message` 是**有意留**的 |
| `LocalizationKeyReachabilityGuardTests` | 新增机制 **M16**（面 2）＋ 下界 | 键由前缀拼出、源码里**不出现整键字面量** ⇒ M6 原理上看不见（与 M8~M15 同一条理由） |
| `CoreVisibleTextInventoryGuardTests` | 新增通道 `Services/RecipeIssue.cs`（2 条）；通道 **12 → 13**、条数 **87 → 89** | 「新增一条未分类的界面文案 ⇒ 这里直接红」 |

### 98.5 负控 5 条（全在真仓库上，还原一律**写回原文** ＋ `sha256` 逐字节核对）

| NC | 注入（生产文件） | 该红的判据 | 实测 |
|---|---|---|---|
| p1 | `RecipeManager`：`if (!AddRecipe(clone)) …Rejected…` → 裸 `AddRecipe(clone);`（丢掉 bool） | `…Producer_ReturnsCodedResult_AndConsumesTheAddGuard` | **红 ✓** |
| p2 | `RecipeEditorView`：`ExternalText.Describe(issue)` → `issue.Message`（绕开码化出口） | `…Site_RendersThroughTheCodedOutlet` | **红 ✓** |
| p3 | `Strings.en-US.json`：删 `recipe.issue.source_missing` | `…BothDictionaries_CarryEveryCode_AndAreActuallyPaired`（＋ 渲染 3 条） | **红 ✓** |
| p4 | `MainWindow`：必需接线 → 裸 `RebuildAllPanels();`（**行数中性**，1 行换 1 行） | `PanelRebuildFailureUiTests.ProductionPath_WiresTheRebuildThroughTheHelper` | **红 ✓** |
| p5 | `Wiring.Panels`：`LocService.Format("status.reload.failed", ex.Message)` → `ExternalText.FailureReason(ex)`（"顺手统一"方向） | `ExceptionMessagePassthroughFaceTests.…MatchesBaseline` | **红 ✓** |

**5 支 5/5**，每支都**点名了该点的那条**；还原后 `sha256` 逐字节一致（
`ffc200f98647` / `b2304c823d9b` / `9944ad840263` / `13edd50ea785` / `8825b8efbb16`）、
复跑全绿。日志 `s98-nc/*.log`，汇总 `s98-nc-run.log`。

⚠ **每条注入都要求"能编译"**（改名先 `CS0103` 那种不算负控）：p2 与 p4 都是**能编译的**形状退化。

### 98.6 🩸 被拦下两次（一次我抓到我、一次是"判据在 UiTests、却跑 Core.Tests"）

1. **p1 第一次跑出了"绿"** —— 但那是**假绿**：过滤器写的
   `FullyQualifiedName~RecipeCloneIssueLocalizationGuardTests` 对应的**类在 `App.UiTests`**，
   而我把它跑在了 `Core.Tests` 上 ⇒ `dotnet test` 打印「**没有测试匹配**」并返回 **0** ⇒
   被记成"判据没接住"。**修法不是放宽判据，而是给负控脚本加一条**：
   `no_match()` 命中即判「**这个负控无效**」并拒绝给出结论。
   ⚠ 这与本项目既有教训同族：**「没有测试跑」与「测试通过」在退出码上分不出**。
2. **闭包判据第一次自己把自己打红**：初版把"工厂构造的 `REJECTED`"也算进 `produced`
   ⇒ `NotProducible ∩ produced ≠ ∅` ⇒ 「豁免过期」那条判据报红。
   修法是**拆开两维**：`Hit.ReachedByProducer`（闭包问"产出方真的会发这个码吗"）
   ≠ 「这个码的键与两本字典通不通」（渲染问的，工厂构造就够）。
   ⚠ 顺手补上**担保方**（登记 #49）："工厂确实挂在产出方的失败分支上"由
   `ProducerViolations` 里**两个工厂各恰好 1 次**钉住 —— 否则 `NotProducible`
   会把一个**真死码**合法化。

### 98.7 读数与对拍（`HMI_READINGS_OUT` **绝对路径**：`.workbuddy/tmp/s98-readings.txt`）

```
── 异常原文直通本地化模板 ──           站点 12 个 / (文件,键) 组合 11 个   （条数不变；status.reload.failed 那一行换文件）
── 无代码引用的 Lang 常量 ──           1113                                （不动）
   字典键（zh-CN）                     2272（2270 → +2）
   其中**没有**常量的键                 456（454 → +2）                     （新键没有 Lang 常量，键由出口推导）
── M6 面的注释敏感度 ──                残差键 378 / M6 面 373 / 注释洞收益 1  （全不动）
── 本地化键可达性（**16** 条机制的面）   M1 703（不动）· M16 面 2（新）· M6 373
── 配方克隆的码表 ──                   声明 2 / 真实产出 1 / 明确产不出 1；渲染见证 2（其中工厂构造 1）
── 面板重建失败的状态栏文案 ──          zh-CN ⇒ 重载失败: 面板重建炸了 / en-US ⇒ Reload failed: 面板重建炸了
```

⚠ **`en` 那一行故意把"没本地化的那一半"摆在读数里**：原因那半是 `ex.Message` 原文
（本读数里是我合成的中文异常；生产里通常是 BCL 英文）—— 这是**产出方给不出码的直接后果**，
**刻意**，见 98.9。

### 98.8 门禁（本节每个数字都能指到**一条日志 ＋ 一个时间点**）

| # | 批次 | 提交 / 时间 | 证据 |
|---|---|---|---|
| ① | 内容批 `549fb29` | `2026-09-24T12:29:23+08:00`，15 files（12 改 ＋ 3 新） | —— |
| ② | gate1（绿·预跑，同一棵树） | `s98-gate1.log` mtime **12:28:56** | 提交前跑；`EXIT=0` / 7 步全过 / 0 警告 0 错误 |
| ③ | **gate2（绿·内容批后）** | `s98-gate2.log` mtime **12:30:53** | **晚于内容批 1 分 30 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **663**（662 → +1 ＝ 新入库的 `RecipeIssue.cs`；该步走 `git ls-files`、口径**不含 `tests/`**）/ `.Children` 面 **402** / 三段合计 **3243**（＝ 阶段 97 的 3229 ＋ 本阶段新判据 **14**） |
| ④ | **文档批 `742e62a`** | `2026-09-24T12:35:37+08:00`，4 files（**只改 `docs/`**），＋346 / −6 | 只改文档 ⇒ 它之后唯一的绿是 ⑤ |
| ⑤ | **gate3（绿·文档批后）** | `s98-gate3.log` mtime **12:37:24** | **晚于文档批 1 分 47 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **663** / `.Children` 面 **402** / 三段合计 **3243** |
| ⑥ | **记忆批 `80d9c2f`** | `2026-09-24T12:41:17+08:00`（**amend 过一次**，见下），2 files，＋143 | 快照 **24l**（**7908 码元 = 原文**）＋ 析出 **§14**；`MEMORY.md` 收在 UTF-16 **7900 / 8000**（净 **−8**，余量 **100**） |
| ⑦ | **gate4（绿·记忆批后）** | `s98-gate4.log` mtime **12:42:44** | **晚于记忆批 1 分 27 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **663** / 三段合计 **3243** |

三段读数：44 / 44 / 75 / 87 / 4 / 315 / **2121** / **543** ＋ 独立段 8 ＋ 2。

**gate3 ↔ gate4 对拍**（顺序敏感差集 **42 行**，逐行看**全部是并行构建 / 测试的输出顺序**）：按**多重集**折算后只剩 **4 行同义文本**（`总共 1 个测试文件与指定模式相匹配。` 被行缓冲**拼成一行 vs 拆成两行**）⇒ **内容无差异** —— 与「记忆批只改 `docs/` 与不入库的笔记」一致。也顺带说明门禁第 2 步的**路径卫生口径不含 `docs/`**（新增 `docs/archive/…` 与改动 `…memory-detail-extract.md`，读数仍是 **663**）。

⚠ **记忆批的提交号变过一次**（`293e230` → `80d9c2f`）：落地后核余量发现只剩 **16 码元**（低于技能自定的 **≥60**）⇒ 再析出一条（§4.1「自治 / 观测 / 还原」整行）；同时浮出一处 **applier bug** 一并修掉 —— **快照原先写的是「裁剪之后」的中间态**（7643 码元），而真原文是 **7908** ⇒ 改成 `t0 = read(MEM)` 先存原文、快照只从 `t0` 取，并加断言 `units(快照) == before`。⚠ 那个 bug 的症状**本来是静默的**（文件在、sha 自洽、工作树干净）。⇒ 该批**唯一有效的那一跑是 ⑦**；`293e230` 那一版**没有门禁证据**（它对应的 gate4 在施工中被停掉，**不作数**）。

⚠ 本节写到 ⑦（记忆批后）。按阶段 85–97 惯例，**回填批之后补跑 gate5**，它**只记**`.workbuddy/memory/2026-09-24.md`，**不回写本文档**（回填批再改本文件就会重开一轮）。

### 98.9 诚实边界与未闭环

1. **`REJECTED` 没有"走真实产出路径"的见证**：它要靠 `Guid[..8]` 真撞上才发生
   —— 概率事件、**不可确定性复现**（主判据不许有间发性：一次绿等于零信息）。
   ⇒ 进 `NotProducible` 并写明理由；渲染见证由**工厂**提供；"工厂挂在失败分支上"
   另由 `ProducerViolations` 钉住。**那个码必须存在** —— 改动前它被丢掉后**报成功**。
2. **`status.reload.failed` 的原因那一半**（`ex.Message`）**没有本地化**，
   而且**不打算**本地化：产出方（面板重建内部的意外异常）**给不出码** ⇒ 换出口是
   阶段 83 钉死的**改名式假闭环**。本项收益是「**异常不再被吞**」。
   ⇒ 直通面那个基线上**照旧留着一行**（换文件、不删除），它就是这个结论的常驻证人。
3. **`login.failed`（#61t ①）仍开着** ⇒ 拆出 **#61v**。本阶段只补了调用方与服务端之间的
   **门槛缝**（`IsNullOrEmpty` → `IsNullOrWhiteSpace`），**不是**本项的闭环。
4. **不动就没人提醒的三处**（本阶段实测确认）：`en` 界面下"原因那一半"可能是中文
   （产出方回落）、面板重建失败的文案质量、以及 `#61s`（M6 过覆盖）照旧开着。
5. **翻译质量不在判据面上**：两条主判据只钉「渲染逐字等于字典模板 ＋ 真实实参」。

---

## §99 登录失败档建码（#61v：五种失败终于分家 —— 「用户不存在」与「密码错误」刻意**不**分）— 2026-09-24

### 99.1 一句话结论

阶段 98 把 `login.failed` 从 #61d 里拆出来时（**#61v**）只登记了一句话：「产出方对每一种真失败
都 `return null`」。本阶段读完产出方**与全部消费点**后落地：

| 面 | 改动 |
|---|---|
| **返回值带码** | `AuthenticationService.Login` 的签名 `string?` → **`LoginResult`**（会话 id 与失败原因**恰好一个非空**，`Ok` / `Failed` 两工厂保证） |
| **码表** | 新增 `LoginIssueCodes`（**5 码**：`BAD_CREDENTIALS` / `LOCKED` / `LOCKED_NOW` / `DISABLED` / `EXPIRED`）＋ `LoginIssue` 三段式（**码 ＋ 中文回落原文 ＋ 实参**）＋ **5 个工厂** |
| **出口** | `ExternalText.LoginIssueKey(code)`（前缀 `login.issue.`）＋ `Describe(LoginIssue)` —— 与 `svg.issue.` / `csv.issue.` / `recipe.issue.` 同形的**返回值档**，**不走** `FailureReason` |
| **站点** | `LoginDialog.OnLogin` 从「五档失败共用一句 `login.err`」改成**按码分派** |
| **退场** | `Lang.LoginErr` 常量与 `login.err` 键**一起**从两本字典删掉（`UnusedBaseline` 1113 → **1112**） |

**收益是用户可见的**：改动前被锁定的用户只看到「用户名或密码错误」（**不知道要等多久**）、
被禁用的也只看到同一句（**不知道找谁**）—— 而这三档的处置完全不同（重输 / 等一会儿 / 找管理员）。

⚠ **「用户不存在」与「密码错误」刻意共用一个码**：分开等于把界面变成**用户名枚举预言机**
（"这个用户名存在吗"）；且改动前两者本来就显示同一句 ⇒ 分开属**范围外**的行为变更。
区分仍留在**审计日志**里（`用户不存在: X` 与 `密码错误 (n/m)` 两串不同）。

⚠ **直通面 12 站 / 11 组合 —— 零变化**（`LoginDialog.axaml.cs · login.failed` 那一行**还在**）
⇒ **第十一例「面读数是证词、不是目标」**（前九例见 #61a / #61f / #61g / #61i / #61j / #61l /
#61m / #61n / #61o，第十例见阶段 98）。⚠ 本阶段这一例的**理由**与前面几例不同：那一行守的是
`catch (Exception ex)` 的**意外异常**档（`Format("login.failed", ex.Message)`），而本阶段收的是
**真失败**档 —— **同一个站点、两件事**，所以这张面**不该动、也不许动**。

### 99.2 量的那一遍（读，不预设）—— 登记里没写的三件事

| # | 登记里的写法 / 没写的 | 实读现场 |
|---|---|---|
| ① | 「产出方对每一种真失败都 `return null`」 | 确认。**但量出了第五档**：`FailedLoginAttempts` 顶到上限的那一次返回的还是同一个 `null` ⇒「**本次把账户顶到锁定**」（要告诉用户等多久）与「普通密码错」（重输）在界面上**同一副面孔**。而 `lockoutMin` 当时**只进日志**（`var lockoutMin = …` 写在 `if` 里面）⇒ 返回值要带它，必须**提到 `if` 外面**。 |
| ② | 「定方案前先读 `Login` 的**全部**消费点」（阶段 98 留的 ⚠） | 产品侧**不止一个**：`LoginDialog.OnLogin` 之外还有 **`BreakGlassLogin`** —— 同类型内部的 **unqualified 调用**（写的是 `Login(...)` 而不是 `_authService.Login(...)`），`grep "authService.Login"` **看不见**。它是**改返回类型之后编译器点出来的**（本项目既定的「改返回类型＝取证手段」，阶段 98 对 `CloneRecipe` 做过同一件事）。 |
| ③ | 「用户不存在要不要单独给码」（登记没问，量的那一遍必须自己回答） | **不给**（理由见 99.1 的 ⚠）—— 这是**产品判断**，不是实现细节，所以写在 `LoginIssueCodes` 的类注释里。 |

调用点规模：**产品侧 2 处 ＋ 测试 57 处**（其中 **33 处**只关心"登进去了没有" ⇒ 机械补
`.SessionId` 取值）。

### 99.3 处置（内容批 `04e6eef`，16 files，**＋1278 / −60**；14 改 ＋ 2 新）

**Core 侧**（新文件 `src/HmiEditor.Core/Services/LoginIssue.cs`）：

* `LoginIssueCodes`：5 个 `const string`，**值不带键前缀**（前缀由 App 侧拼）；
* `LoginIssue`：三段式 `(Code, Message, Args)` —— `Message` 是**中文回落原文**，读者是真人；
  ⚠ **工厂方法而不是公开构造**：`LOCKED` 恒 1 个实参、`DISABLED` 恒 0 个 —— 让调用方手写
  `new LoginIssue("LOCKED", msg, null)` 会**编译期通过、运行期显示字面的 `{0}`**，那正是判据要拦的形状；
* `LoginResult`：`SessionId?` 与 `Issue?` **恰好一个非空**，两工厂保证。⚠ 做成**返回类型**
  而不是多一个 `out` 参数的理由：`out` **可以被忽略**（`auth.Login(u, p);` 照样编译）⇒
  "忘填原因"这条老路换个写法又活了；换返回类型之后**编译器会当场点名每一个拿返回值当字符串用的
  消费点**（本刀点出的就是 `BreakGlassLogin`）。

`AuthenticationService.Login` 的五档改完是：`BadCredentials()`（**2 处共用**：用户不存在 ＋ 密码错）
/ `Locked(RemainingLockMinutes(user))` / `LockedNow(lockoutMin)` / `Disabled()` / `Expired()`；
**账户锁定顺序不变**（先判"锁定未到期"再判禁用 / 过期），新增 `RemainingLockMinutes` 私有辅助
（向上取整、**下限 1** —— `LockedUntilUtc` 为 `null`（永久锁定）在本仓库**不可达**，理由写在方法注释里）。

**App 侧**：`ExternalText` 加 `LoginIssueKeyPrefix` / `LoginIssueKey` / `Describe(LoginIssue)`
（**单一来源**：出口、`LoginIssueLocalizationGuardTests` 与可达机制的 **M17** 都调它）；
`LoginDialog` 改 `if (result.SessionId is { } sessionId) … else if (result.Issue is { } issue)
ShowError(ExternalText.Describe(issue));`。

**字典**：`login.issue.*` **−1 ＋5**（2272 → **2276**，`login.err` 两本各删 1、新 5 键 × 两本），
按字母序插在 `login.in.progress` 之后；逐行手术（`assert count == 1`）＋ JSON 校验 ＋
两本键集一致校验 ＋ 回读核对。

### 99.4 判据：8 条新（`LoginIssueLocalizationGuardTests`）＋ 2 条**必须跟着改**的旧判据

| # | 判据 | 钉住什么 |
|---|---|---|
| 1 | `EveryFailure_RendersTheDictionaryTemplate_InBothLanguages` | 主判据：**毒化 ＋ 两语言各渲染一遍**双见证 |
| 2 | `RealFailures_InEnglish_CarryNoChinese` | **收益的正面见证**：真跑产出方喂真失败，英文界面下不许再出现中文（用 CJK 当尺子、不写死措辞） |
| 3 | `BothDictionaries_CarryEveryCode_AndAreActuallyPaired` | 齐 ＋ 成对 ＋ **占位符个数 == 实参个数** |
| 4 | `DeclaredCodes_AreEitherProducedOrExplicitlyNotProducible` | **三态闭包**（声明 = 真实产出 ∪ 明确产不出） |
| 5 | `Producer_ReturnsCodedResult_AndFiresEveryCode` | 产出方形状：`BadCredentials(` **恰好 2 次**、其余四码**各恰好 1 次** |
| 6 | `Site_RendersThroughTheCodedOutlet` | 站点恰好 1 处码化出口 ＋ **不许再用旧键 `login.err`** |
| 7 | `UncodedIssue_FallsBack_AndCodedOnesDifferFromFallbackInEnglish` | 诚实边界正面断言（两半，见 99.6 ①） |
| 8 | `NegativeControls_EachBreakIsCaughtByTheRightJudgment` | 内嵌 **NC0–NC6**（NC0 **全合成正控**、NC6 **真语料对照**故意留在 NC0 之外） |

**其余 4 条旧判据也必须跟着改**：

| 判据 | 改动 | 为什么必须跟着改 |
|---|---|---|
| `LangConstantUsageGuardTests` | `UnusedBaseline` **1113 → 1112**（M6 桶 262 → 261） | `Lang.LoginErr` 与 `login.err` 一起退场 ⇒ 少一个零引用常量。⚠ 归位口径写在注释里（M2 828 / M3 18 / 无机制 5 **不动**） |
| `LocalizationKeyReachabilityGuardTests` | 新增机制 **M17**（面 5，下界 **5**）＋ 机制条数 16 → **17** | 键由前缀拼出、源码里**不出现整键字面量** ⇒ M6 原理上看不见（与 M8~M16 同一条理由） |
| `CoreVisibleTextInventoryGuardTests` | 新增通道 `Services/LoginIssue.cs`（5 条）；通道 13 → **14**、条数 89 → **94** | 「新增一条未分类的界面文案 ⇒ 这里直接红」 |
| `LocalizedFormatCallSiteTests` | 加**常驻读数**（`.Format` 306 / 下界 250、索引器 805 / 下界 700） | 见 99.10 ④（#61q 的"贴边"问题第一次被量准） |

### 99.5 负控

**生产负控 5 支 5/5**（`s99-negative.py`，日志 `s99-negative.log` **18:25:47**；
跑在**提交前的同一棵树**上 —— 事后逐个核对过：5 个文件的「原文」快照与 `HEAD` 里的对应文件
`sha256` **逐字节一致**）：

| NC | 注入（都能编译） | 期望的红 | 该轮**实测红集** |
|---|---|---|---|
| 1 | 站点回退到旧出口（写死 `login.err`） | 站点接线 ＋ 内嵌负控 | 3 条：`Site_RendersThroughTheCodedOutlet`、`NegativeControls…`、**`LocalizedFormatCallSiteTests.FormatAndIndexerCallSites…`**（`login.err` 已退场 ⇒ 调用点守卫报「该键不在字典里」，**正当**） |
| 2 | 产出方把「用户不存在」改发 `DISABLED` | 产出方形状 ＋ 主判据 ＋ 三态闭包 | 6 条（期望 3 条全中；另 3 条 `RealFailures…` / `EveryFailure…` / `BothDictionaries…` 喂的正是这条路，**同源**） |
| 3 | en 字典缺键（`login.issue.locked` 改名） | 两本字典齐备 ＋ 英文无中文 | 7 条（同源；`LocalizationKeyReachabilityGuardTests.BothDictionaries_HaveTheSameKeySet` 也点名到了） |
| 4 | 摘掉 M17 的取数 | `MechanismFaces_AreNonEmpty` ＋ 键可达性 | **恰好 2 条** |
| 5 | 新增一个没人读的 `Lang` 常量 | `UnusedLangConstants_MatchBaseline` | 3 条（连带 `UnusedConstants_ClassifiedByKeyMechanism` ＋ `LangKeyCoverageTests…`） |

⚠ **NC6（真语料对照）是特意留在 NC0（全合成正控）之外的** —— 所以生产负控里它会跟着红
（NC1/2/3 的红集里都有 `NegativeControls_EachBreakIsCaughtByTheRightJudgment`）。那是**设计**：
把真仓库当正控的话，真仓库一坏就分不清是"注入生效"还是"仓库真坏了"。

⚠ **一条必须记下来的脚本缺陷**：`s99-negative.py` 的 `failing()` 读的是**一路追加的同一个日志**
⇒ 第 2–5 轮打印的"红集"里**混着前几轮的红**。后果是**假绿的形状**：第 N 轮的"期望红命中"
可能来自第 N−1 轮的残留。⇒ 本阶段**从日志按轮切分重算**了 5 轮各自的红集（切分点 = 每轮的
`$ dotnet build` 分隔行），**5 轮各含其期望红**，结论不变；但**修批的 3 支一律每支一个独立日志**。

**修批负控 3 支 3/3**（`s99-fixnc.py`，日志 `s99-fixnc-run.log` **18:40:51**）：见 99.8。

### 99.6 🩸 被拦下（都在判据自己身上）

1. **判据第一版自己把自己判红**（`s99-t1.log` **18:20:00**，2 条红）：
   `Assert.NotEqual(渲染, 回落)` 用来判"字典真的在起作用" —— 而本族 zh 字典值与 Core 的中文
   回落**本来就是同一句话**（`bad_credentials` 逐字相同）⇒ **正确的实现被判红**。
   修法：① 断言**只对 en-US 成立**、不许对 zh 断言；② 改名成
   `UncodedIssue_FallsBack_AndCodedOnesDifferFromFallbackInEnglish` 并把这条理由写进注释。
   ⚠ **与阶段 97 §97.1 那条同源**（zh 值刻意等于回落的族里，**中文那一遍零见证力**）；
   判"字典在起作用"要靠主判据的**毒化**那一路。
2. **`VisibleChannels` 是数组**：写 `.Count` 会解析成 LINQ 扩展方法 → **方法组** → **CS8917**
   （编译期当场拦下，不是运行期）⇒ 用 `.Length`（详见 99.8）。

### 99.7 读数（`HMI_READINGS_OUT` **绝对路径**：`.workbuddy/tmp/s99-readings.txt`，**18:32:18**）

```
── 异常原文直通本地化模板 ──         站点 12 个 / (文件,键) 组合 11 个   （零变化；login.failed 仍在 LoginDialog.axaml.cs，语义换成"意外异常"档）
── 无代码引用的 Lang 常量 ──         1112（1113 → −1：Lang.LoginErr 退场）　　字典键（zh-CN） 2276（2272 → −1 +5）
   其中**没有**常量的键               461（456 → +5）
   ── 按机制分桶 ──                   M2 828 · M6 **261**（262 → −1）· M3 18 · 无机制 5（三条不动）
── M6 面的注释敏感度 ──              残差键 **377**（378 → −1）· M6 面 **372**（373 → −1）· 旧口径 373 · 注释洞收益 1（param.baud.rate）
── 本地化键可达性（**17** 条机制的面） M1 703（不动）· **M17-登录码 面 5（新）** · M16 2（不动）· M6 372
── 本地化调用点（C1 的两条面）         .Format 306（下界 250）· 索引器 **805**（下界 700，登记 #61q）· 合计 1111
── 登录产出方的形状 ──                 src/HmiEditor.Core/Services/AuthenticationService.cs
── 登录失败的码表 ──                   声明 5 / 真实产出 5 / 明确产不出 0：BAD_CREDENTIALS, DISABLED, EXPIRED, LOCKED, LOCKED_NOW
── 登录站点接线 ──                     src/HmiEditor.App/Views/LoginDialog.axaml.cs
── 登录失败 · 两本字典 ──               login.issue.bad_credentials / locked / locked_now / disabled / expired
── 英文界面下的渲染 ──                  六档（含"用户不存在"）全部拿到英文；中文界面下逐字等于 zh 字典模板
```

### 99.8 修批 `a670b78`（18:41:07）：台账归位行的通道数**漂了 1** —— 顺手补上「谁看着这些数字」

**怎么发现的**：归位盘点专题 §0 时，把「通道 / 条数」两组数与守卫自己的行首句对了一遍 ——
守卫写「**13 条通道**（94 条文案）」、盘点专题也写「13 条通道」，而 `VisibleChannels` 里
**实实在在是 14 条**。按提交逐个数（`git show <c>:<file>` ＋ 数 `new("` 条目）：

| 提交 | 阶段 | 表里真实通道数 | 守卫行首写的 | 归位链末段写的 | 盘点专题 §0 写的 |
|---|---|---|---|---|---|
| `301c11c` | 94（#61m） | **11** | 11 | —（无此句） | 11 |
| `be8dbde` | 97（#61d） | **12** | 11（**没跟上**） | — | 11（没跟上） |
| `549fb29` | 98（#61t） | **13** | **12**（那一行把"加了通道"写成"**通道数不变**"） | — | 11（仍没跟上） |
| `04e6eef` | 99（#61v） | **14** | 13（顺着上一行的 12 ＋1） | 13 / 94 | **13**（把阶段 98 的真实值补上，但没算自己） |

⇒ **根因**：那两个数字**两处手抄 ＋ 没有任何判据看着**。阶段 98 写错的那 1 个通道数一路传下来，
而且当时**两处互相矛盾**（盘点专题是对的、守卫是错的）也没人红。⚠ 顺带：**内容批的提交消息
也抄了这个漂过的数**（写「核心可见文案台账 12/89 → 13/94」）—— 真值是 **13 → 14**；提交消息
不可改，如实记在这里。

**处置**（只改两个文件，**不动生产代码**）：守卫文档注释归位（行首 **14 条通道（94 条文案）**、
归位链末段 **14 / 94**、阶段 98 那段改成 **13 / 89** 并写明这次纠正）＋ 盘点专题 §0 归位为
**94 条 / 14 条通道** ＋ 判据 `VisibleChannels_CountIsWhatTheDocSays` 从「只断言 94」扩成
「**三处**文档的**两个**数字都必须等于**算出来的**值」（本文件行首句、本文件归位链**末段**、
盘点专题 §0 摘要句；一律用**具名组**取数 —— 两处语序相反，按位置取会自己报假异常，
落地时真踩到一次；句子找不到时判据**自己报失效**，不静默放行）。

**负控 3 支 3/3**（`s99-fixnc-run.log` **18:40:51**，每支**独立日志**）：NC1 行首句 14→13 /
NC2 归位链末段 **14 / 94** → **13 / 94** / NC3 盘点专题 §0 94·14 → 89·13 —— 三支都只让
`VisibleChannels_CountIsWhatTheDocSays` 变红、错误消息点名到位；写回原文后 `sha256` 逐字节一致
（`00b5def8cb76` / `22b082b79427`），**还原后真的重建**（0 警告 0 错误）。

### 99.9 门禁（本节每个数字都能指到**一条日志 ＋ 一个时间点**）

| # | 批次 | 提交 / 时间 | 证据 |
|---|---|---|---|
| ① | **内容批 `04e6eef`** | `2026-09-24T18:30:11+08:00`，16 files（14 改 ＋ 2 新），＋1278 / −60 | —— |
| ② | gate1（绿·预跑，同一棵树） | `s99-gate1.log` mtime **18:29:52** | 提交前跑；`EXIT=0` / 7 步全过 / 0 警告 0 错误 |
| ③ | **gate2（绿·内容批后）** | `s99-gate2.log` mtime **18:31:40** | **晚于内容批 1 分 29 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **664**（663 → +1 ＝ 新入库的 `LoginIssue.cs`；该步走 `git ls-files`、口径**不含 `tests/`**）/ `.Children` 面 **403** / 三段合计 **3251**（＝ 阶段 98 的 3243 ＋ 本阶段新判据 **8**） |
| ④ | **修批 `a670b78`** | `2026-09-24T18:41:07+08:00`，2 files，＋72 / −6 | 台账归位 ＋ 判据（见 99.8） |
| ⑤ | **gate2b（绿·修批后）** | `s99-gate2b.log` mtime **18:42:53** | **晚于修批 1 分 46 秒** ✓；`EXIT=0` / 7 步全过 / 路径卫生 **664** / `.Children` 面 **403** / 三段合计 **3251** |
| ⑥ | **文档批 `91950a7`** | `2026-09-24T18:46:05+08:00`，4 files（**只改 `docs/`**），＋363 / −5 | 本专题 §99 ＋ `project-status` 阶段 99 ＋ 登记表 ＋ 盘点专题 §17 |
| ⑦ | **记忆批 `3d0c646`** | `2026-09-24T18:50:26+08:00`，2 files，＋135 | 快照 **24m**（其自身 **7900 码元 ＝ 原文**）＋ 析出 **§15**；`MEMORY.md` **7921 / 8000**（余量 **79**） |
| ⑧ | **gate3（绿·文档批后）** | `s99-gate3.log` mtime **18:47:32** | **晚于文档批 1 分 27 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **664** / `.Children` 面 **403** / 三段合计 **3251** |
| ⑨ | **gate4（绿·记忆批后）** | `s99-gate4.log` mtime **18:53:45** | **晚于记忆批 3 分 19 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **664** / `.Children` 面 **403** / 三段合计 **3251** |

**gate3 ⇄ gate4 对拍**（`.workbuddy/tmp/s99-diffgate.py`）：剥墙钟后**各 739 行**、多重集 **496 / 496**
**逐条完全一致** —— 记忆批只改**笔记与快照**（`.workbuddy/memory/` 按约定**不入库**；
`docs/archive/…` 那条快照虽入库，但门禁第 2 步**路径卫生口径不含 `docs/`** ⇒ 读数仍是 **664**）
⇒ 门禁输出**逐行相同**。这是「文档 / 记忆批不动行为」的正面证据。

⚠ 本节写到 **⑨（记忆批后）**。按阶段 85–98 惯例，**回填批之后补跑 gate5**，它**只记**
`.workbuddy/memory/2026-09-24.md`，**不回写本文档**（gate5 之后再改本文件就会重开一轮）。

三段读数：44 / 44 / 75 / 87 / 4 / 315 / 2121 / **551**（543 → ＋8）＋ 独立段 8 ＋ 2。

**gate2 ⇄ gate2b 对拍**：剥墙钟后**各 739 行**、多重集**逐条完全一致**（496 / 496，
✅ 多重集完全一致）—— 修批只改注释与既有判据内的断言，**不动任何计数**
（`2121` / `8` / `2` 三段一个没变），这正是"文档 + 判据断言"该有的样子。

**gate1 ⇄ gate2 对拍**：多重集差 **2 / 3**，逐条看**两条同义**（`总共 1 个测试文件与指定模式
相匹配。` 被行缓冲**拼成一行 vs 拆成两行**）＋ **一条真差异**：路径卫生 **663 → 664**
（内容批新入库 1 个 `src/` 文件 ⇒ `git ls-files` 那一步读数 +1）。⚠ 与登记表里那条
「`git ls-files` 步**内容批前后差 N 行**」同族。

### 99.10 诚实边界与未闭环

1. **`login.failed`（#61t ①）现在只剩"意外异常"档**，而且**刻意**留着 `ex.Message`
   （产出方给不出码 ⇒ 换出口是阶段 83 钉死的**改名式假闭环**）⇒ 直通面那一行是**换语义**
   不是变少，它就是这个结论的常驻证人。
2. **`LOCKED_NOW` 的复用场景没被真跑出来**：判据里它是**工厂构造**喂进去的（真实产出要
   攒够 `MaxLoginAttempts` 次失败 —— 可确定性构造，但代价与本刀无关）⇒ 与阶段 98 的
   `REJECTED` 不同，这里**没有**进 `NotProducible`（`Producer_ReturnsCodedResult_AndFiresEveryCode`
   数的是**抛点/调用点计数**，不是"跑过一遍"）。⇒ **口径差异如实记**，不假装两者同级。
3. **#61s（M6 过覆盖）本阶段又添一个见证**：NC4 摘掉 M17 后本该点名 **5** 个键，
   实际只点名 **4** 个 —— `login.issue.bad_credentials` 被**判据自己的断言消息**
   （`LoginIssueLocalizationGuardTests.cs` 里那句"应有 5：`login.issue.bad_credentials` / …"）
   **写活了**。这正是 #61s 说的"判据自己给键担保"。
4. **#61q（下界贴边）第一次量准**：`LocalizedFormatCallSiteTests` 加了**常驻读数** ——
   索引器站点实测 **805**、下界 **700**（差 105，**不贴边**）；`.Format` 侧 306 / 250。
   ⚠ 本刀**没抬下界**（抬它要重定口径），只把真值摆出来。
5. **翻译质量不在判据面上**（与 §98 同）：三条主判据只钉「渲染逐字等于字典模板 ＋ 真实实参」。
6. 家族仍开着 **#61（本体）/ #61a / #61f / #61k / #61p / #61r / #61s**；
   **#61v 本阶段闭环**、**#61t 的 ① 随之闭环**、**新开 #61w（本阶段即刻闭环，见 99.8）**。

## §100 配方 CSV 导入失败档建码（#61f **闭环**：7 处静默收口 —— 「成功导入 N 个配方」不再掩盖坏行）— 2026-09-24

### 100.1 一句话结论

`RecipeManager.ImportCsv` 改动前是**返回一个 `int`** 的宽签名，站点拿它做二值判断
（`if (count > 0)` 报「成功导入 N 个配方」、`else` 报「CSV 文件格式无效或数据为空」）。
这一句把 **7 处失败全盖住了**，其中 3 处**确定性丢数据**、2 处**「成功」是撒谎**：

| 用户看到的 | 实际发生的 |
|---|---|
| 「成功导入 5 个配方」 | CSV 第 7 行某一格写成了 `abc` ⇒ 那个配方项被跳过，**用户不知道少了什么** |
| 「成功导入 5 个配方」 | 表头第 4 列没写列名 ⇒ 那一列**整列的值**被跳过 |
| 「成功导入 5 个配方」 | 第 9 行列数不足 ⇒ **整行跳过**，连计数都没进 |
| 「成功导入 N 个配方」（N 里含它） | 新配方 ID 撞车、`AddRecipe` 返回 `false` ⇒ 没进表，**但 `count++` 照走** |
| 「CSV 文件格式无效或数据为空」 | 空文件与「表头只有两列」是**两件完全不同的事**，共用同一句 |

本阶段按 #61f / #61t② 已经立住的模板收口（**不新发明**）：

| 面 | 改动 |
|---|---|
| **返回值带码** | `RecipeManager.ImportCsv` 签名 `int` → **`RecipeImportResult`**（`Imported` ＋ `Issues`） |
| **码表** | 新增 `RecipeImportIssueCodes`（**6 码**：`TOO_FEW_LINES` / `NO_HEADER_COLUMNS` / `ROW_COLUMNS_MISSING` / `EMPTY_COLUMN_NAME` / `VALUE_NOT_A_NUMBER` / `RECIPE_NOT_APPLIED`）＋ `RecipeImportIssue` 三段式（**码 ＋ 中文回落原文 ＋ 实参 ＋ 行号**）＋ **6 个工厂** |
| **出口** | `ExternalText.RecipeImportIssueKey(code)`（前缀 `recipe.import.issue.`）＋ `Describe(RecipeImportIssue)` —— **返回值档**，与 `svg.issue.` / `csv.issue.` / `recipe.issue.` / `login.issue.` 同形，**不走** `FailureReason` |
| **站点** | `RecipeEditorView` 新增「CSV 问题条」：`ShowCsvIssues(result.Issues)` 逐条走出口渲染 |
| **新增键** | `recipe.csv.import.warnings`（状态行「\| {0} 个警告」）—— 让状态行**只放条数**、明细去问题条 |

**收益是用户可见的三条渠道**（没有一处"悄悄丢"）：状态行给**条数**、问题条给**哪一行 + 为什么**
（可见文本取前 5 条、超出补显式省略号）、ToolTip 给**全部**。

⚠ **直通面 12 站 / 11 组合 —— 零变化** ⇒ **第十二例「面读数是证词、不是目标」**
（前九例见 #61a / #61f / #61g / #61i / #61j / #61l / #61m / #61n / #61o，第十例见阶段 98、
第十一例见阶段 99）。本阶段这一例的**理由与第十一例同族**：本刀改的是**同一个文件里另一个方法**
（`ImportCsv`）的**真失败**档，而那张面上 `RecipeEditorView.axaml.cs · recipe.clone.failed` 那一行
守的是 `CloneRecipe` 之后 **UI 刷新**的意外异常 ⇒ **同一个文件、两件事**，那张面**不该动、也不许动**。

### 100.2 量的那一遍（读，不预设）—— 登记里列的两个产出方，一个该划掉、一个是真内容

#61f 的清单里剩两个产出方，量的那一遍把它们**分开处置**：

| 产出方 | 量的结论 |
|---|---|
| `FontSubsetAnalyzer` | **纯函数、零失败面** —— 只有 `ExportCharsetFile` 的 IO 能抛，而 `font.export.fail` 那一站**已在 #61h（阶段 90）收口** ⇒ 本项**量完即无处安放** ⇒ **划掉**（是"判过的不做"，不是"没做"） |
| `RecipeManager.ImportCsv` | **7 处静默** ⇒ **#61f 剩余的真内容**（本刀） |

**`ImportCsv` 的 7 处静默**（`src/HmiEditor.Core/Services/RecipeManager.cs`）：

| 档 | 位置 | 触发 | 后果 |
|---|---|---|---|
| S1 | `lines.Length < 2 → return 0` | 空 / 单行 | 与 S2 共用一句话，用户分不清 |
| S2 | `header.Length < 3 → return 0` | 表头列不足 | 同上 |
| S3 | `cols.Length < 3 → continue` | 坏行 | **静默跳行**（不入任何计数） |
| **S4** | `double.TryParse` 失败 | 值非数字（空 / `abc` / `1,5` / 带单位） | **该配方项静默丢弃**，站点照报「成功导入 N 个配方」 |
| S5 | `AddRecipe` 的 `bool` **被丢** | 新配方 `Id`（`Guid[..8]`）撞车 | 没进表但 `count++` ⇒ **计数多 1** |
| S6 | `UpdateRecipe` 的 `bool` 被丢 | 同上（路径上几乎不发生） | 同上 |
| S7 | 表头空列名 `continue` | 表头有空列 | 该列所有值静默丢弃 |

⚠ **S5 与 #61t② 的 `CloneRecipe` 完全同一形状**（**同一个文件**里 `AddRecipe` 的 `bool` 又一次被丢）
—— 这是"同族缺陷会在同一文件里成对出现"的又一个见证。

⚠ **放大器**：站点 `RecipeEditorView.axaml.cs` 的 `count > 0` 二分 ⇒
用户**没有任何渠道**知道坏行 / 坏值在哪。

**另查两件**（都不动，理由记下）：① `ValidateRecipe` 全仓**零生产消费点**（只有测试调它，
台账已记 `Services/RecipeManager.cs = 2`）⇒ 本刀**不动**；② `ExportCsv` 的列序
`RecipeName,RecipeVersion,GroupName,<var…>` 与 `ImportCsv` 硬编码的 `0 / 2 / 3+` **一致**
⇒ 列序不算缺陷（但 `ImportCsv` **不读表头名**，属登记类）。

⚠ **为什么不复用 `CsvImportIssueCodes`**（阶段 88 那一族）：它那 5 条措辞是**变量导入专属**
（`NO_HEADER_COLUMNS` 点名 `Name / DataType / Address`、`ROW_*_NAME` 指变量名、
`ROW_PARSE_FAILED` 是"抛异常兜底"）⇒ 复用要新加 3–4 码且措辞不符，反而不干净；
另立一族与**同文件**的 `RecipeCloneIssue` 同形。

### 100.3 处置（内容批 `30efa18`，14 files，**＋1529 / −42**；12 改 ＋ 2 新）

**Core 侧**（新文件 `src/HmiEditor.Core/Services/RecipeImportIssue.cs`）：

* `RecipeImportIssueCodes`：6 个 `const string`，**值不带键前缀**（前缀由 App 侧拼）；
* `RecipeImportIssue`：`(Code, Message, Args, Line)` —— `Message` 是**中文回落原文**，读者是真人；
  **行号是结构化字段**（与 `CsvImportIssue` 同一处置：行号**不进 `Message`**，由出口统一加前缀）；
* 6 个工厂而不是公开构造：每一码的**实参个数是固定的**（`ROW_COLUMNS_MISSING` 恒 0 个、
  `VALUE_NOT_A_NUMBER` 恒 2 个…）⇒ 让调用方手写 `new RecipeImportIssue("…", msg, null, 0)`
  会**编译期通过、运行期显示字面的 `{0}`**，那正是判据要拦的形状；
* `RecipeImportResult(int Imported, IReadOnlyList<RecipeImportIssue> Issues)` ＋ `HasIssues` ＋ `Empty()`。

`ImportCsv` 的 7 处改完是：S1 → `TooFewLines()`、S2 → `NoHeaderColumns(headerRaw)`、
S3 → `RowColumnsMissing(line)`、S4 → `ValueNotANumber(line, name, raw)`、
S7 → `EmptyColumnName(index)`、S5/S6 → **`bool` 不再丢**，失败走
`RecipeNotApplied(line, recipeName)` 并 `continue`（**不进 `Imported`**）。

⚠ **`Issues` 与 `Imported` 是两个独立计数**，改动后不再互相污染
（改动前 S5 会把"没进表"算成"进了"）。

**App 侧**：`ExternalText` 新增 `RecipeImportIssueKeyPrefix` / `RecipeImportIssueKey` /
`Describe(RecipeImportIssue)`（行号走 `Lang.CsvLinePrefix`，**仅 `Line > 0` 时**加前缀）；
`RecipeEditorView` 新增 `ShowCsvIssues`（唯一渲染出口）＋ `MaxInlineCsvIssues = 5`，
并且**每次导入开始前先清空上一次的明细**（否则旧问题会挂在新错误下面，看着像这次导入的）。

**字典**：两本各 **+7 键**（`recipe.import.issue.*` 6 ＋ `recipe.csv.import.warnings` 1），
`2276 → **2283**`；逐行手术（`assert count == 1`）＋ JSON 校验 ＋ 两本键集一致校验 ＋ 回读核对。

**测试 5 处必须跟着改**（`RecipeSerializerTests` 3 处 ＋ `Phase3IntegrationTests` 2 处）：
`int count = mgr.ImportCsv(csv)` → `var result = …; result.Imported`。
⚠ **这 5 处不是"找到的"，是编译器点出来的** —— 改返回类型＝取证手段（本项目既定做法），
连 `Assert.Equal(0, mgr.ImportCsv(""))` 那种"直接拿返回值当 `int`"都当场 `CS1503`。

### 100.4 判据：11 条新（`RecipeImportIssueLocalizationGuardTests`）—— **复用 M16 的四个纯函数，不写第三份拷贝**

| # | 判据 | 钉住什么 |
|---|---|---|
| 1 | `EveryIssue_RendersTheDictionaryTemplate_InBothLanguages` | 主判据：毒化 ＋ 两语言各渲染一遍双见证 |
| 2 | `RealIssues_InEnglish_CarryNoChinese` | 收益的正面见证：真跑产出方喂真问题，英文界面下不许出现 CJK（用 CJK 当尺子，**掩码实参**） |
| 3 | `BothDictionaries_CarryEveryCode_AndAreActuallyPaired` | 齐 ＋ 成对 ＋ **占位符个数 == 实参个数** |
| 4 | `DeclaredCodes_AreEitherProducedOrExplicitlyNotProducible` | **三态闭包**（声明 = 真实产出 ∪ 明确产不出） |
| 5 | `Producer_ReturnsCodedResult_AndConsumesTheApplyGuards` | 产出方形状：签名、`public int ImportCsv(` **不许存在**、**6 个工厂各恰好 1 次**、`issues.Add(RecipeImportIssue.` ≥ 6、**不许出现"整行只有裸入库调用"** |
| 6 | `Consumer_ShowsTheIssuesThroughTheCodedOutlet` | 站点：必须读 `result.Issues`、必须走 `ExternalText.Describe`、`ShowCsvIssues(result.Issues)` 恰好 1 次、**不许把 `.Message` 直接塞进文本框** |
| 7 | `IssueBar_IsNotNestedInAScrollViewer` | 问题条的 XAML 形状：本身是 `<Border`、其前**没有未闭合的 `<ScrollViewer`**（headless 布局死区，见下） |
| 8 | `EveryKeyPrefix_IsDocumentedInTheClassComment` | 前缀清单与 `ExternalText` 类注释**双向**一致 |
| 9 | `LinePrefixKey_MatchesTheLangConstant` | 行号那一跳的键常量与会话格式一致 |
| 10 | `IssueWithoutAKey_StillFallsBackToTheRawMessage` | 诚实边界正面断言（查不到键时回落中文原文，不吞） |
| 11 | `NegativeControls_EachBreakIsCaughtByTheRightJudgment` | 内嵌 **NC1–NC7**，含**全合成正控**与一条**"散文不算清单条目"**的抗性断言 |

**复用清单**（把 M16 判据的四个 `internal` 纯函数直接调过来，**不复制代码**）：
`RecipeCloneIssueLocalizationGuardTests.RenderRow` / `RenderViolations` / `PairingViolations` /
`ClosureViolations`。本类只写**确实不同**的那几件：行号前缀的期望串、
"丢掉 `bool`"的**形状签名**（`(?m)^[ \t]*(Add|Update)Recipe\(recipe\);[ \t]*$`）、
问题条的 `ScrollViewer` 位置、键前缀清单。

⚠ **同一个 `ShowCsvIssues` 被两条独立判据看着**（第 6 条看"接线在不在"、第 7 条看"放在哪"）——
这是**判据面互相独立**的正面例子，不是判据写多了。

### 100.5 负控：生产 **6 支 6/6**（真仓库注入 → 构建 → 点数 → 写回原文 → `sha256` → **重建复绿**）

跑在**提交前的同一棵树**上（`s100-nc-all2.log` **21:46:46**，早于内容批 `30efa18` 的 **21:48:08**），
`sha256` 逐例与 `HEAD` **逐字节一致**。每支都有**独立的"还原后复绿"一步**（重建再跑 = 0 红）。

| NC | 注入（都能编译） | 期望红 | 该轮实测红集 |
|---|---|---|---|
| A | `ImportCsv` 退回静默（摘掉 `VALUE_NOT_A_NUMBER` 那一处） | 6（**同根级联**） | **6**：产出方形状 / 三态闭包 / 成对性 / 主判据 / 英文无中文 / 内嵌负控的真语料正控 |
| B | 多写一处裸 `AddRecipe(recipe);`（丢掉 `bool`） | 恰 1 | **1**：`Producer_…ConsumesTheApplyGuards` |
| C | 拆掉 `ShowCsvIssues(result.Issues)` 的接线 | 2 | **2**：接线判据 ＋ 内嵌负控的真语料正控 |
| D | `value_not_a_number` 的 en 值抄回中文 | 3 | **3**：成对性 / 英文无中文 / 内嵌负控 NC1 正控 |
| E | `user.issue.` 的**清单条目**改成 `zzz.issue.` | 恰 1 | **1**：`EveryKeyPrefix_…`（🩸 第一轮**一条都没红**，见 100.6） |
| F | 问题条包进 `ScrollViewer` | 恰 1 | **1**：`IssueBar_IsNotNestedInAScrollViewer` |

⚠ **NC-A 的 6 条不是"判据写多了"**：一次注入只引入**一个根因**，而一个根因会在**多条互相独立**的
判据上同时现形（形状 / 闭包 / 成对性 / 渲染 / 语料正控各管一段）—— 这是判据面独立的**正面证据**。

### 100.6 🩸 被拦下（两处都在**判据自己身上**）

1. **量具自身的字面量落在了它自己量的字母表里**：`MaskArgs` 第一版用 `«实参»` 当掩码占位符 ——
   它**自带 CJK**，而这条判据的尺子正是「英文界面下不许出现 CJK」⇒ **凡是带实参的码全红**。
   **指纹**：红绿**恰好按"有没有实参"切分**。换成纯 ASCII 的 `<<ARG>>` 即绿。
   ⚠ 这是「静态守卫作者」技能里第十六种失败模式的**第一形态**（同族第二形态见 100.7 的 NC-E）。
2. **NC-E 第一轮**一条都没红** ⇒ 判据被打穿**：锚点是 `Contains(prefix)`，而 `ExternalText` 类注释的
   **散文旁白**（「第一例是上面 `<c>user.issue.</c>` 那一条」）**也算在"清单里"** ⇒
   把清单条目改坏之后，旁白照样满足判定。收紧成「**前缀 ＋ `&lt;`**」的**清单条目形状**后，
   本支**恰好点名那一条**。⚠ 副产品：这条收紧顺手让它**免疫"定义面"**
   （散文里写得出 `user.issue.`，但写不出 `user.issue.&lt;`）—— 判据的输入面**不含定义面**。

### 100.7 ⚠ 我的期望表错了 2 条（如实记，不是判据失效）

* **NC-A 不该期望台账判据红**：`NoUnclassifiedCoreChinese` 数的是**字面量**，而这次注入摘掉的
  是一个**调用点** —— 字面量还在 `RecipeImportIssue.cs` 里 ⇒ 它**结构性看不见**这次注入。
* **NC-D 不该期望主判据红**：`EveryIssue_Renders…` 的候选集取 **zh ∪ en 并集**
  ⇒「en 抄 zh」仍然匹配 zh 候选 ⇒ **它原理上测不到"没翻"**，这正是另外两条判据存在的理由。

⇒ 第一轮的读数**一条都没浪费**：A / C / D 三条"没红"是我的表写错，E 那条是真的打穿（已收紧）。

### 100.8 新判据的「第一次跑」＝ 一次免费的**全库体检**（本阶段点到两处历史遗漏）

| 判据 | 它点名的东西 | 与本判据的年龄差 |
|---|---|---|
| `CoreVisibleTextInventoryGuardTests.NoUnclassifiedCoreChinese` | `+ Services/RecipeImportIssue.cs: 登记 0，实扫 6`（**预期内**）**同时**点名 `Services/LoginIssue.cs`（**5 条**，阶段 99） | 比本判据**老 1 个阶段**（§16 当年只把自己那两次归位到 13 / 89，**没算自己那一跳**） |
| `EveryKeyPrefix_IsDocumentedInTheClassComment` | `sim.issue.` / `user.issue.` 未登记 ⇒ `ExternalText` 类注释里 **M15（阶段 97）的清单条目从来就没写** | 比本判据**老 2–3 个阶段**（不是本刀引入的） |

⇒ **值一节的理由**：新写的"清单 / 台账"类判据，第一次跑会把**比它老的**遗漏一起点出来 ——
这不是判据写错了，是**此前没有任何判据看着那两处**。两处都当场补齐（补进本刀的内容批）。

### 100.9 读数（`HMI_READINGS_OUT` **绝对路径**：`.workbuddy/tmp/s100-readings.txt`，**21:51:41**）

```
── 异常原文直通本地化模板 ──         站点 12 个 / (文件,键) 组合 11 个   （零变化）
── 无代码引用的 Lang 常量 ──         1112（不动）　　字典键（zh-CN） 2283（2276 → +7）
   其中**有**常量的键               1816（1815 → +1 ＝ recipe.csv.import.warnings）
   其中**没有**常量的键              467（461 → +6 ＝ 6 个 recipe.import.issue.*，键由前缀拼出）
   ── 按机制分桶 ──                   M2 828 · M6 261 · M3 18 · 无机制 5（四条全不动）
── M6 面的注释敏感度 ──              残差键 **377**（不动）· M6 面 **372**（不动）· 旧口径 373 · 注释洞收益 1
── 本地化键可达性（**18** 条机制的面） M1 704（703 → +1 ＝ 新增的 Lang.RecipeCsvImportWarnings）·
                                    **M18-配方导入码 面 6（新）** · M17 5 · M16 2 · M1 之外各条不动
── 本地化调用点（C1 的两条面）         .Format **308**（306 → +2）· 索引器 **805**（不动）· 合计 1113
── 配方 CSV 导入的码表 ──             声明 6 / 真实产出 5 / 明确产不出 1：产出 = EMPTY_COLUMN_NAME,
                                    NO_HEADER_COLUMNS, ROW_COLUMNS_MISSING, TOO_FEW_LINES,
                                    VALUE_NOT_A_NUMBER；产不出 = RECIPE_NOT_APPLIED
                                    渲染见证 6 条（其中工厂构造 1 条）
── 配方 CSV 导入产出方的形状 ──        src/HmiEditor.Core/Services/RecipeManager.cs
                                    （6 条工厂各 1 次 ＋ 不许裸语句形式的入库调用）
── 配方 CSV 导入站点的接线 ──          src/HmiEditor.App/Views/RecipeEditorView.axaml.cs
── 英文界面下的渲染 ──                 六档全部拿到英文（含行号前缀 "Line 2: …"）；
                                    中文界面下逐字等于 zh 字典模板
```

**`.Format` 那 +2 的解释**（必须能指到具体行）：① 新增的状态行
`Format(Lang.RecipeCsvImportWarnings, result.Issues.Count)`；
② `ExternalText.Describe(RecipeImportIssue)` 里的行号前缀
`Format(Lang.CsvLinePrefix, issue.Line, body)`。⇒ +2 不是噪声。

### 100.10 门禁（每个数字都能指到**一条日志 ＋ 一个时间点**）

| # | 批次 | 提交 / 时间 | 证据 |
|---|---|---|---|
| ① | 构建绿（同一棵树） | `s100-build.log` mtime **21:29:14** | 0 警告 / 0 错误 |
| ② | **全量预跑绿**（同一棵树） | `s100-fulltest1.log` mtime **21:36:00** | 8 程序集 **3262 通过 / 0 失败** |
| ③ | 判据收紧后复绿 | `s100-guard4.log` mtime **21:40:18** | 新判据 **11/11** 绿 |
| ④ | **生产负控 6/6** | `s100-nc-all2.log` mtime **21:46:46** | 见 100.5；每支 `sha256` 一致 ＋ 还原后复绿 |
| ⑤ | **内容批 `30efa18`** | `2026-09-24T21:48:08+08:00`，14 files，＋1529 / −42 | —— |
| ⑥ | **gate1（绿·内容批后）** | `s100-gate1.log` mtime **21:50:28**（21:48:11 启动） | **晚于内容批 3 秒启动、2 分 17 秒跑完** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **665**（664 → +1 ＝ 新入库的 `RecipeImportIssue.cs`；该步走 `git ls-files`、口径**不含 `tests/`**）/ `.Children` 面 **404**（403 → +1，同因）/ 三段合计 **3252**（3251 → ＋本阶段新判据 **11**） |
| ⑦ | **文档批 `93f7bb7`** | `2026-09-24T21:53:51+08:00`，4 files（**只改 `docs/`**），＋377 / −3 | 本专题 §100 ＋ `project-status` 阶段 100 ＋ 登记表 ＋ 盘点 §18 |
| ⑧ | **gate3（红 1 —— 既有竞态被暴露，不是本阶段引入）** | `s100-gate3.log` mtime **21:55:15** | `GATE3_EXIT=1`；第 5 步红 **1** 条：`ProjectFileOpenFailureUiTests.FailedOpen_LeavesNoWorkspaceDirectoryBehind` ⇒ 根因与处置见 §100.12 |
| ⑨ | **修批 `44f0c71`** | `2026-09-24T22:03:34+08:00`，5 files，＋124 / −0 | 见 §100.12：补两处集合标注 ＋ 新增静态判据（＋ 两文档补记） |
| ⑩ | **gate2b（绿·修批后）** | `s100-gate2b.log` mtime **22:04:57** | **晚于修批 1 分 23 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **665**（**不动** —— 修批只改 `tests/`，该步口径是 `src/`）/ `.Children` 面 **404**（不动，同因）/ 三段合计 **3253**（3252 ＋ 新增的静态判据 **1**） |
| ⑪ | **记忆批 `4cfa9b3`** | `2026-09-24T22:06:12+08:00`，2 files，＋150 | 快照 **24n**（其自身 **7922 码元 ＝ 原文**）＋ 析出 **§16**（80 行）；`MEMORY.md` **7931 / 8000**（余量 **69**） |
| ⑫ | **gate4（绿·记忆批后）** | `s100-gate4.log` mtime **22:07:42** | **晚于记忆批 1 分 30 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **665** / `.Children` 面 **404** / 三段合计 **3253** |

**gate2b ⇄ gate4 对拍**（剥墙钟后）：**各 776 行**，多重集差 **3 行**且**全是同一句**
（`总共 1 个测试文件与指定模式相匹配。` —— 行缓冲把它**拼成一行 vs 拆成两行**，阶段 99 记过同族）
⇒ **实质逐条一致** —— 记忆批只改笔记与快照（`.workbuddy/memory/` 按约定**不入库**、
`docs/archive/…` 那条快照虽入库但**不在路径卫生口径里**）⇒ 门禁输出逐行相同。

⚠ **gate1 ⇄ gate2b 的两个差**都能归因：路径卫生 **665 → 665**（不动）、`.Children` 面 **404 → 404**（不动）、
三段合计 **3252 → 3253**（＋1 ＝ 修批新增的静态判据）；另 **gate3 那一次的 1 条红**已在 ⑧ 记明。

⚠ 本节写到 **⑫（记忆批后）**。按阶段 85–99 惯例，**回填批之后补跑 gate5**，
它**只记** `.workbuddy/memory/2026-09-24.md`，**不回写本文档**（gate5 之后再改本文件就会重开一轮）。

⚠ 本阶段把"预跑"与"门禁"合成一步（①②③④ 都在**提交前同一棵树**上，⑤ 之后 3 秒即起 gate1）
—— 因为**负控与全量都跑在提交前**，而 gate1 紧接提交 ⇒ 时序上不存在"拿修复之前的绿当验证"的空间。

**三段读数**：44 / 44 / 75 / 87 / 4 / 315 / **2121** / **562**（551 → ＋11）＋ 独立段 **8** ＋ **2**。

### 100.11 诚实边界与未闭环

1. **`RECIPE_NOT_APPLIED` 的"真跑"见证是工厂构造的**：真产出它需要 `Guid[..8]` 撞车
   （可确定性构造，代价与本刀无关）⇒ 进 `NotProducible` 一档如实记（与阶段 98 的 `REJECTED` 同级口径）。
   ⚠ 本刀的**产出方形状判据**数的是**工厂调用点计数**（6 个各 1 次），**不是**"跑过一遍"。
2. **`ImportCsv` 不读表头名**（前三列是**位置**约定的），`ExportCsv` 也照同一顺序写 ⇒
   列序**一致**所以不算缺陷，但"按表头名取列"是**新功能**，不在本项范围。
3. **`ValidateRecipe` 零生产消费点**：本刀**不动**（它是"死代码"议题，不是"原因到不了界面"议题）。
4. **翻译质量不在判据面上**（与 §98 / §99 同）：三条主判据只钉「渲染逐字等于字典模板 ＋ 真实实参」。
5. **提交消息里有一处写错了**（提交消息不可改，如实记）：内容批消息写「`Lang` +1
   （`csv.line.prefix` 常量）」—— 真值是 **`Lang.RecipeCsvImportWarnings`**
   （新增的状态行键），而 `Lang.CsvLinePrefix` **阶段 88 就有**（`Lang.cs:634`）、本刀只是复用。
   ⇒ 读数里那两处 `+1`（M1 面 / 有常量的键）**以读数文件为准**，不以提交消息为准。
6. **#61f 本阶段闭环 —— 闭环的是「产出方」那一半**：清单里的四个产出方**全部落位** ——
   `GeneratedCodeValidator`（→ 拆出 #61g，阶段 89 已闭环）/ `CsvVariableImporter`（阶段 88 第一刀）/
   `FontSubsetAnalyzer`（本阶段**量完即划掉**，理由见 100.2）/ `RecipeManager`（`CloneRecipe` 阶段 98
   ＋ `ImportCsv` 阶段 100）。
   ⚠ **#61a 仍开着**：它清单里还有 **4 个键**留在直通面上、本阶段**逐条未动** ——
   `status.verify.failed` / `audit.verify.code.failed` / `status.svg.apply.failed` /
   `asset.import.fail`（批量档）。⚠ **本阶段没有替它们的形状下结论**：
   它们是否与本刀收掉的那一族同类，要下一阶段**先量的那一遍**读了现场代码才作数
   （#61 家族的一贯做法 —— 量的那一遍经常推翻一半预设）。
   家族仍开着 **#61（本体）/ #61a / #61k / #61p / #61r / #61s**。

### 100.12 修批：门禁红了一次 —— **不是本阶段引入，是既有竞态被暴露**（`DataRootSandbox` 的集合标注漏挂）

**现场**：`s100-gate3.log`（**21:55:15**，`GATE3_EXIT=1`）—— 唯一一条红：
`HmiEditor.App.UiTests.ProjectFileOpenFailureUiTests.FailedOpen_LeavesNoWorkspaceDirectoryBehind`。
错误消息：
「当前数据根「…/.hmi-test-tmp/**opensave-save-data**-31652aa0d2c84d529e3a645f338238f6」
不在扫描面的候选集合里（候选 = …一长串**别的**用例的沙箱根…）」。

⚠ 这条站出现在 `App.UiTests`，与本刀改的 `RecipeManager` / `ExternalText` / `RecipeEditorView`
**没有调用关系**；且同一条命令在 `s100-gate1.log`（**21:50:28**，同一棵代码树）里**全绿**。

**根因（读代码确证，不是猜）**：`DataRootSandbox` 的构造 / 析构**直接**
`Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, …)`，而 `AppPaths.DataRoot` 读**同一个
进程级变量** ⇒ **两个测试类并行**时，A 在断言里读到的 `AppPaths.DataRoot` 是 **B 的**沙箱根。

**为什么此前一直没撞**：11 个用 `DataRootSandbox` 的类里有 **9 个**都挂了
`[Collection("Language-global")]`（`DisableParallelization = true` ⇒ **独占**）⇒ 它们彼此串行；
而 `ProjectFileOpenFailureUiTests` / `ProjectFileOpenSaveRoundTripUiTests`
**从建立那天（阶段 79 / 80）就漏挂** ⇒ 它们与**所有**集合并行 ⇒ 撞上任何沙箱类。

⚠ **为什么不能当"低频噪声"放过**：反方向的后果更坏 —— 用例**读到别人的数据根却照样通过**
⇒ **假绿**。低频的是**复现率**，不是**后果**。

**复现率（先量再修）**：

| 口径 | 读数 |
|---|---|
| 门禁命令（`dotnet test HmiEditor.slnx … --filter …`） | **1 次红**（21:55:15）＋ 随后 **3 跑 0 复现**（`s100-flake-g1/2/3.log`） |
| 单独 `App.UiTests.csproj` 全量 | **3 跑 0 复现**（`s100-flake-r1/2/3.log`）；**564** = 大队 **562** ＋ 独占段 **2**，与门禁口径自洽 |

⇒ **低频**。所以修法的见证**不是"重跑绿"**，而是 ① 根因（读代码）＋ ② **静态判据 + 负控**。

**处置**：

1. 两个类补 `[Collection("Language-global")]`（并留一行注释指向下面那条判据）；
2. 新增静态判据 `LocalizationRefreshUiTests.EveryTestClassThatUsesDataRootSandbox_IsInTheGlobalCollection`
   —— 与既有那条「切语言的类必须挂集合」**同族、复用同一个 `HasCollectionAttribute` 纯函数**
   （**不写第二份**），只有匹配串不同（`new DataRootSandbox` ↔ `SwitchLanguage(`）；
   消费者个数的下界取 **8**（实测 **11**，**刻意不贴边** —— 又加一个消费者是正常增长、不该红）。
   ⚠ **为什么必须单独一条**：两条判据守的是**两个不相干的全局量**（语言 / 数据根）⇒
   「切语言没挂集合」那条**原理上看不见**「改数据根没挂集合」。
3. **为什么是 `Language-global` 而不是新建集合名**：它是 `DisableParallelization = true`（**独占**）
   ⇒ 挂进来就与**所有**其他类串行；而既有先例 `AppDataRoot-global` **不解决问题** ——
   xunit 里**不同集合之间照样并行**。⚠ 把 9 个沙箱类挪去新集合也**不可行**：它们**同时**在切语言。

**修批负控 2 支 2/2**（`s100-fixnc-A.log` / `-B.log`，**每支独立日志**；注入态构建通过、
写回原文 `sha256` 逐字节一致、**还原后重建再跑 = 0 红**）：

| NC | 注入 | 该轮**实测红集** |
|---|---|---|
| A | 摘掉 `ProjectFileOpenFailureUiTests` 的集合标注 | **恰好 1 条**：新判据。⚠ 同时是**两条判据互相独立**的正面证据 —— **既有的那条没红** |
| B | 把新判据的匹配串改坏 | **恰好 1 条**：新判据（**扫描面自检**报「只找到 0 个用 DataRootSandbox 的源文件（下界 8）」） |

⚠ 门禁表随之更正：**gate3 = 红 1 ⇒ 修批后由 gate2b 复绿**（§100.10 那行原写"绿"的依据是
唯一的 gate1 读数，`gate3` 那次是**新红**，如实改记）。

---
## §101 #61a 尾巴 4 站收口（2 站真迁移 / 2 站「诚实边界」；并**实测出一个判据盲区**）— 2026-09-24

### 101.1 一句话结论

阶段 100 收口时明确留下的尾巴（`status.verify.failed` / `audit.verify.code.failed` /
`status.svg.apply.failed` / `asset.import.fail`）本阶段收口。**量的那一遍把一个旧登记直接推翻**
（`asset.import.fail` 的「一次导入多个文件 ⇒ 那个文件结构上不存在」），四个键分属两类：

- **两站真迁移** —— 走 `UiOperationGuard.FailureDetail(操作名, 目标路径, 异常)`；
- **两站登记「诚实边界」** —— 那个 `try` 里**没有产出方** ⇒ 换出口只会落回落分支
  （与阶段 98 的 #61t / #61u 同一条边界，**不是**改名式假闭环：换过去用户所见**真的**一字不变）。

直通面 **12 站 / 11 组合 → 10 站 / 9 组合**。并在迁移过程中**实测出一条判据盲区**
（承接变量档当时无人守）⇒ 同阶段补第二条判据。

### 101.2 量的那一遍（读，不预设）—— 4 个键分属两类，且**一处旧登记被实测推翻**

| 站点 | `try` 体内真 IO？ | 产出方有码？ | 判定 |
|---|---|---|---|
| `status.verify.failed` ＋ `audit.verify.code.failed`（**同一个** `catch`） | **无** | **否** | **诚实边界**（只加注释） |
| `status.svg.apply.failed`（`Wiring.DockAggregate.cs`） | **有**（第一行就是读盘） | **是**（BCL 类型，`SystemFailureCodes` 7 码） | **真迁移** |
| `asset.import.fail`（`AssetManagerView` 图片**批量档**） | **有**（`new FileInfo(path).Length`） | **是** | ⚠ **旧登记被推翻** ⇒ **真迁移** |

**逐条证据（读现场代码，不是推断）**：

1. **`status.verify.failed` / `audit.verify.code.failed`（`MainWindow.axaml.cs:1487-1516`）**
   那个 `try` 的体是：`new CodeGenerator()` / `GenerateFullProject(...)` /
   `GeneratedCodeValidator.Validate(...)` / `findings.Count(...)` /
   `new Views.ValidationResultView(findings)` / `dialog.ShowDialog(this)` / 两行状态写。
   产出方侧实测：
   `grep -rn -i "throw" src/HmiEditor.CodeGen/*.cs` ⇒ **只有 2 处**
   （`VisualBindingCodeGen.cs:37/38` 的 `ArgumentNullException.ThrowIfNull` 是**内部入参**；
   `:62` 的 `NotSupportedException` 是「视觉属性漏登记」= **程序员错误**档）；
   `src/HmiEditor.CodeGen/*.cs` 的 `File.` / `Directory.` / `FileInfo` / `Process.` 命中 **0 条**；
   `GeneratedCodeValidator.cs` **零 `throw`、零 IO**。
   ⇒ 用户可触发的失败**一条都没有**，能落到这个 `catch` 的只剩 UI 构造 / 弹窗级的意外异常。
   ⇒ 这两条**有意留** `ex.Message`：换出口会和 #61u 一样落进回落分支、用户所见一字不变。

2. **`status.svg.apply.failed`（`Wiring.DockAggregate.cs:71-98`）**
   那个 `try` 的**第一行**是 `var svgText = await System.IO.File.ReadAllTextAsync(asset.FilePath);`
   —— **真 IO**（文件在"导入素材"与"应用"之间被删、或被改了权限）。
   而同一 `try` 里紧随其后的 `SvgParser.Parse` **早就**走码化出口（阶段 87，`ExternalText.Describe`）
   ⇒ 这一站的漏网之处正是那行读盘。

3. **`asset.import.fail`（`AssetManagerView.OnImportImage`，`axaml.cs:180-223`）**
   旧登记写「一次导入多个文件，「那个文件」结构上就不存在」—— **实测不成立**：
   `var path = file.Path.LocalPath;` **就在 `foreach` 里**。
   而且**同一个文件**里 SVG / 字体两档阶段 86 就已经迁了
   （`:291` `FailureDetail(Lang.UiOpImportSvg, files[0].Path.LocalPath, ex)`、
   `:362` 同款 `UiOpImportFont`）—— 只剩**图片**这一档，形状与那两档一模一样。
   ⚠ 真失败源是 `new System.IO.FileInfo(path).Length`（BCL IO）；
   `CopyToAssetDirectory` 内部已有 `catch { return sourcePath; }`（静默吞），
   尺寸解析另有内层 `catch`（`new Bitmap(path)` 那一档，注释写明"解析失败时保持 0"）。

### 101.3 处置（内容批 `d150048`，10 files，**＋65 / −19**）

1. `Wiring.DockAggregate.cs` → `UiOperationGuard.FailureDetail(Lang.UiOpApplySvg, asset.FilePath, ex)`；
2. `AssetManagerView.axaml.cs` → 承接变量 `string? currentPath = null;` 提到 `try` 外
   （`foreach` 里 `currentPath = path;` 必须在任何可能抛出的语句**之前**）
   ＋ `FailureDetail(Lang.UiOpImportImage, currentPath, ex)`；
3. `MainWindow.axaml.cs` → 两个 `status.verify.*` 站点**登记「诚实边界」**（行为一字不改）；
4. `Lang.cs` ＋2 操作名常量（`UiOpApplySvg` / `UiOpImportImage`，**都有代码引用**）；
5. 两本字典 ＋2 键（`ui.op.apply.svg` / `ui.op.import.image`）；
6. 两处**会被改「撒谎」**的既有注释：`ExternalText.FailureReason` 的 doc
   （「仍留着的那 1 处是 `asset.import.fail` 的批量档」）与
   `UiOperationGuard.FailureDetail` 的 doc（「第 9 站 …归 #61a」）。

### 101.4 字典处置链（与阶段 82 的 `StatusOpenFailed` **同形**）

那两个键的全部使用点写的都是**字面量**（`"asset.import.fail"` / `"status.svg.apply.failed"`）
⇒ 两个 `Lang` 常量本来就没有代码引用、只能靠 **M6 桶**担保。迁移后字面量消失 ⇒ 键变**不可达**
⇒ 处置链一步都不能少：

| 动作 | 对象 |
|---|---|
| 删键 | 两本字典各 −2（`asset.import.fail` / `status.svg.apply.failed`） |
| **连删常量** | `Lang.AssetImportFail` / `Lang.StatusSvgApplyFailed`（`LangKeyCoverageTests` 要求同生共死） |
| 同步基线 | `LangConstantUsageGuardTests.UnusedBaseline` **1112 → 1110** |
| 清工具 | `scripts/sync_localization.py` 的 `VALUE_MAP` —— 查过，这两个键**不在里面**（零动作） |
| 差异表 | `KeptUnreachable` —— 查过，这两个键**不在里面**（零动作） |

### 101.5 🩸 gate1 第一次红：**行数守卫**（给 `MainWindow.axaml.cs` 加注释 ⇒ 治理回涨）

`SingleFileSizeGuardTests.File_LineCount_MatchesBaseline` 把 `MainWindow.axaml.cs` 钉在
**3014** 行（**相等判定**），纪律写死「涨了 = 治理回涨（先把文件改小）」——
阶段 93 也撞过同一条（当时 +1，涨的是**注释**）。

我按 §101.2 给那两个站点写注释（**8 行**）⇒ 中间态 **3014 + 8 = 3022** ⇒ 门禁第 5 步红。

处置 = **行数中性置换**（净 0，基线一字未动）：

1. 站点注释 **8 行 → 4 行**（推理全文不在代码里，在面测试类注释与本节）；
2. 同一文件里 `RunDockEventRoutingDiagnostic` 上挂着**两个 `<summary>`**
   （`:946-949` 与 `:950-953`）—— 前者描述的是**诊断代码还在主窗口里**的旧三步流程，
   后者明说「实现见 `Services.DockEventRoutingDiagnostics`」。方法体实测只是一行委派
   ⇒ 前者**过期**，且同一成员挂两个 `<summary>` 本身是**畸形的文档注释** ⇒ 删掉那 **4 行**。

⇒ **3022 − 4 − 4 = 3014**。这次置换落在内容批那一个提交里
（`git show --stat d150048 -- src/HmiEditor.App/MainWindow.axaml.cs` ⇒ **4 insertions / 4 deletions**，
**净 0** ⇒ 与「基线不动」自洽）。

⚠ 副作用值得记：**在被钉住的文件里加注释是不可行的**，除非同时腾出等量行数 ——
所以那两个站点的推理**故意**不写全在站点上。

#### 101.5.1 🩸 证据缺口：那次红的日志**没了**（同路径被绿跑覆盖）⇒ 补一支可复算的负控

第一次红跑与后来的绿跑**用了同一个路径** `.workbuddy/tmp/s101-gate1.log` ⇒ **绿覆盖红**
（同族前例：**阶段 83 的撞名覆盖** —— `s83-gate1.log` 被 `: >` 静默截断，
`project-status.md` **§83.7** 有如实标注）。⚠ 本阶段这一处的形态**更隐蔽**：不是「被截断成空文件」，
而是**绿跑把红跑的结果整个换掉** —— 站在当下看不出任何异常，只有回头看 mtime 与内容，
才发现那次红已经不在。

⇒ 本节的 **3022 是「算术自洽的中间态」，不是可复算的读数**（它从未落盘；能指向的两端
`3014` 与 `3014 + 8` 都在）。纪律就是纪律：**不引用指不回日志的时间点**。改为补一支
**每支独立日志**的负控，把这条守卫的**活性**重新钉住：

| 支 | 注入 | 日志（时间点） | 实测读数 |
|---|---|---|---|
| 注入 | `MainWindow.axaml.cs` 末尾**追加一个空行**（+1 行，语义中性） | `s101-line-nc-inject.log`（**2026-09-25 00:05:15**） | **EXIT=1**：`…MainWindow.axaml.cs: 行数 3015 ≠ 基线 3014`；`失败: 1，通过: 3，总计: 4` |
| 还原 | 写回**原文**（不是 `cp -p`） | `s101-line-nc-restore.log`（**00:05:16**） | **EXIT=0**：`通过: 4`；`sha256` 逐字节一致 = **True** |

⚠ 守卫是**运行时 `File.ReadAllLines` 读文件**（不是读编译产物）⇒ 这一支**不需要重建**，
`--no-build` 即可造出两份互不覆盖的读数 —— 这是它便宜到「没有理由不补」的原因。

### 101.6 🩸 判据盲区：**承接变量一直是 null，两条既有判据都看不见**（真仓库注入实证）

`ExternalText.FailureReason` 的类注释里写着：

> ⚠ 但站点面**分不出**"带了路径"与"三实参里填了 `null`"（后者用户一样看不到文件）
> ⇒ 那件事另有一条判据盯着：
> `UiOperationGuardContractTests.EveryDetailCallSite_PassesAConcreteTargetPath`。

**逐处读那条判据的实现（`ConcretePathViolations`）**：它只判两件事 ——
`args.Count < 3`、以及 `args[1] is "null" or "default"`。**都只认字面量**。

⇒ 而阶段 86 / 90 迁出来的站点里，有一档的第二个实参是**承接变量**
（`string? X = null;` 提到 `try` 外、循环里再赋值）—— 那种写法下
**「有没有真的赋上」面完全看不见**。**真仓库注入实测**（负控 NC-A）：

| NC | 注入 | 结果 |
|---|---|---|
| **A** | `AssetManagerView` 的 `currentPath = path;` → `= null;`（行数中性） | 构建 **EXIT=0** / `App.UiTests`（`FullyQualifiedName~HmiEditor.App.UiTests`）**EXIT=0、红 0 条** |

⇒ 也就是那句话**说过头了**：当时那条判据只覆盖了「字面量 `null`」那一半。
（原文已在判据批里更正为「那件事有**两条**判据盯着」并写明各自管哪一半。）

### 101.7 判据批：第二条判据 `EveryConveyedPathVariable_IsGivenARealValue`

`UiOperationGuardContractTests` 新增两条（该文件 :265 起，**复用**既有 `DetailCallSite`
与 `LocalizedLogCallSiteTests.SplitTopLevelArguments`，不写第二份扫描）：

1. **纯函数** `ReviewConveyedPathVariables(sources)` → `(Sites, Violations)`。
   规则（**位置式，不做作用域分析**）：对每个 `FailureDetail(op, X, ex)`，若 `X` 是**裸标识符**、
   且同文件里在**它之前**有 `string? X = null;` ⇒ 要求声明与调用点之间至少有**一次赋成真值**的
   赋值（`X = …`，右侧不是 `null` / `default`）。
   - 为什么用**位置**而不是作用域：同文件可以有两个同名局部
     （`CommunicationConfigView` 就有两处 `targetPath`）⇒ 位置规则会把第二个方法那次赋值
     **排除在第一个站点之外**，正是想要的；真正的作用域解析要做到 Roslyn 那个量级，与收益不成比例。
   - ⚠ **它看不见什么**（判据必须自陈）：对**字段**（`_selectedFilePath`）与**参数**（`filePath`）
     不适用 —— 它们没有 `= null` 声明，本判据**不入面**（那是对的）。
   - ⚠ **不剔定义面**：筛子挂在"有 `string? X = null;` 声明"上，`UiOperationGuard.cs` 里的形参
     `targetPath` 恰好没有这种声明 ⇒ 天然不入面（与 `ConcretePathViolations` 必须剔定义面不同）。
2. **生产 Fact** `EveryConveyedPathVariable_IsGivenARealValue`：跑全产品源码面
   （`AppSourceScanner.ScanAllProductSources()`），自检 `Sites >= 3`
   （**实测 4 处**：`BuildPipelineService.targetDir` / `AssetManagerView.currentPath` /
   `CommunicationConfigView.targetPath` ×2；下界刻意不贴边）。
3. **负控 Fact** `ReviewConveyedPathVariableViolations_FlagsEachInjectedShape`（**6 组**合成语料）：
   ① 声明后一次赋值都没有 ⇒ 点名；② 只赋 `null` ⇒ 点名；③ 赋成真值 ⇒ 无话可说；
   ④ 赋值在**调用点之后** ⇒ 仍点名（钉「位置在起作用」）；⑤ 注释里的反例 ⇒ 不算；
   ⑥ 声明在**调用点之后**（同文件两个同名局部）⇒ 不误报。

🩸 **负控 ② 当场抓出判据自己的一个正则洞**：第一版写的是
`\s*=(?!=)\s*(?!null\b|default\b)` —— `=` 后面那个 `\s*` **可以匹配空串**，
于是前瞻看到的是那个**空格**、把 `p = null;` 判成"赋了真值"⇒ ② 红、① 反而绿。
修法 = 前瞻**紧贴** `=`：`\s*=(?!=)(?!\s*(?:null|default)\b)`。
⚠ 这条洞**只有负控 ② 抓得到**：生产面 4 处赋的都是真值 ⇒ 主判据在洞存在时**照样全绿**。

### 101.8 生产负控（**每支一份独立日志**；注入态构建通过、写回原文 `sha256` 逐字节一致、**还原后重建再跑复绿**）

| NC | 注入 | 该轮**实测红集** |
|---|---|---|
| **A** | `currentPath = path;` → `= null;` | **0 红**（= 盲区实证；见 101.6） |
| **D** | 同 A 的注入，跑**含新判据**的树 | **恰好 1 条**：`EveryConveyedPathVariable_IsGivenARealValue` |

⇒ A 与 D 是**同一注入**、只改跑集 ⇒ 补集关系成立：**除新判据外，跑集里没人看得见这一档**（既有那条并集判据在两种跑集下都不红）。

**红集是「逐条」与「汇总」两处互证过的**（不是只看跑器的打印）：
`s101-nc-D-test.log` 里 `[xUnit.net …] …EveryConveyedPathVariable_IsGivenARealValue [FAIL]`
一行、汇总行 `失败! - 失败: 1，通过: 566，总计: 567`；A 侧汇总 `失败: 0，总计: 566`
（A 的过滤只排掉**主判据一条**、保留 6 组合成负控 ⇒ 567 − 1 = 566，数值自洽）。

#### 101.8.1 🩸 顺带修掉「负控跑器」自己的两个缺陷（都在**证据怎么存 / 怎么读**上，**不在被测对象上**）

| # | 缺陷 | 症状 | 修法 |
|---|---|---|---|
| ① | 注入态与还原态**共用同一个日志路径** | 还原后的**绿覆盖**注入态的红 | 每支另开 `…-restore-{build,test}.log` |
| ② | 收红集按「`[FAIL]` 在**前**」写正则 | xunit 实为**用例名在前** ⇒ **一条都收不到** ⇒
打出「测试 `EXIT=1`」＋「红 **0** 条」这种**自相矛盾**的读数 | 改正则 ＋ 加「**逐条红集合 ↔ 汇总行失败计数**」交叉核对，不等即抛 |

⚠ ② 的要害不是「改正则」而是**加互证**：**「收不到」不许被当成「没有红」**。
修完拿**四份真实日志**回测（正控 4/4：A / A-restore / D-restore 各 0 条，D 恰 1 条且名字正确）
＋**四组伪造日志**负控（汇总 1 条无 `[FAIL]` / 汇总 0 条有 `[FAIL]` / 无汇总行 ⇒ 各自必抛；
用例名里带「失败」字样 ⇒ 不抛且名字正确）＝ **8/8 全过**。
⇒ NC-D 的结论从「跑器说 1 条」升级为「**原始日志逐条可复算**」。

### 101.9 读数（`HMI_READINGS_OUT` **绝对路径** `.workbuddy/tmp/s101-readings-{before,after}.txt`，两段全绿）

改前取自 **2026-09-24 23:37:11**（`…-before.txt`，`E1=0 E2=0`，大队 **33 s** ＋ 停靠 1 s；
脚本 `…-before.sh` 写于 23:36:30 ⇒ 落在**内容批改动之前**），改后取自 **23:44:32**
（`…-after.txt`，`E1=0 E2=0`，大队 **30 s** ＋ 停靠 1 s；晚于内容批提交 23:42:31）。

| 量 | 改前 | 改后 | 说明 |
|---|---|---|---|
| **直通面** | **12 站 / 11 组合** | **10 站 / 9 组合** | 本阶段唯一"该动"的量 |
| `.Format` 站点 / 索引器 / 合计 | 308 / 805 / 1113 | **306** / 805 / **1111** | 减的正是两站 |
| 无代码引用的常量（`UnusedBaseline`） | 1112 | **1110** | −2 = 两个死常量退场 |
| 分桶 M2 / M3 / **M6** | 828 / 18 / **261** | 828 / 18 / **259** | 两个键的担保来自 M6 |
| **M1 面** / M6 面 / 残差键 | 704 / 372 / 377 | **706** / **370** / **375** | ＋2 = 两个**有代码引用**的新常量 |
| 字典键（zh-CN） | 2283 | **2283** | −2 ＋2 ＝ **净 0** |
| 有常量的键 / 无常量的键 | 1816 / 467 | **1816 / 467** | 同上，净 0 |
| 可达机制 | 18 条 | **18 条** | 本阶段**不新增机制**（两个新键走 M1） |
| `App.UiTests`（读数跑本身） | 563 ＋ 2 | **563 ＋ 2** | 两次读数跑都只覆盖**内容批**，判据批的 +2 见 §101.10 |

⚠ 「字典净 0 而 `UnusedBaseline` −2」「`UnusedBaseline` −2 而 M1 面 ＋2」都**不矛盾**：
两者数的集合不同（一条数"无代码引用的常量"，一条数"有代码引用的常量"）——
与阶段 82 / 86 / 90 的同类说明同一形状。

### 101.10 门禁（**每一次绿都晚于**它要证明的那次提交）

| 时刻 | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 23:42:31 | — | **内容批** `d150048` | 10 files，**＋65 / −19** |
| **23:43:39** | `s101-gate1.log` | 内容批门禁（7 步） | **EXIT=0**；`Core.Tests` **2121** / `App.UiTests` **563＋2** |
| 00:02:48 | — | **判据批** `43cb79d` | 2 files，**＋184 / −2** |
| **00:04:15** | `s101-judge-gate.log` | 判据批门禁（7 步） | **EXIT=0**；2121 / **565＋2**（该类 8 → 10） |
| 00:07:49 | — | **文档批** `6f2a87d` | 3 files，**＋389 / −3** |
| **00:08:53** | `s101-gate3.log` | 文档批门禁（7 步） | **EXIT=0**；2121 / 565＋2 |
| 00:11:15 | — | **记忆批** `4d895ff` | 2 files，**＋172 / 0** |
| **00:12:22** | `s101-gate4.log` | 记忆批门禁（7 步） | **EXIT=0**；2121 / 565＋2 |

⇒ `App.UiTests`（**门禁口径**：第 5 步排除停靠面板、第 6 步单独跑）**563 → 565**
（+2 = 判据批新增两条）。与 §101.9 的读数口径（**两侧都是 563＋2**）**不矛盾**：
读数跑与门禁第 5 步**同口径**，只是两次读数跑都只覆盖**内容批**。

⚠ 文档批 / 记忆批**只改 `.md`**（记忆批另改 **gitignore** 的 `.workbuddy/memory/`）
⇒ 它们的门禁读数与判据批**逐项相同**是**预期**，不是「改动没生效」。

✅ **四次门禁都在各自提交之后运行**（23:43:39 > 23:42:31；00:04:15 > 00:02:48；
00:08:53 > 00:07:49；00:12:22 > 00:11:15）—— **不是**「把修复之前那次绿当成修复的验证」。
⚠ 内容批那次还有一次**红**，但它**没有**可引用的日志（见 §101.5.1）。

| 00:12:54 | — | **回填批** `d429411` | 2 files，**＋21 / −13** |
| **00:13:54** | `s101-gate5.log` | 回填批门禁（7 步） | ⚠ **EXIT=1** —— **环境型红**，见 §101.10.1 |
| **00:15:29** | `s101-gate5b.log` | 同上，**另开日志**复跑 | **EXIT=0**；2121 / 565＋2 |

#### 101.10.1 🩸 gate5 那次红是**环境型**（`/tmp` 10 MB tmpfs 写满），不是本批改动、也不是代码缺陷

```
System.IO.IOException : No space left on device :
  '/tmp/sdk-409d489d1efa4c0cb9872500acca6e18/generated_hmi.c'
   at HmiEditor.Device.Tests.T113SoBuilderTests.ResolveContext_MissingSdk_ReportsMissingItems()
```

**归因四件套**（四条都要，缺一条不许下结论）：

| # | 取证 | 实测 |
|---|---|---|
| ① | 该程序集本轮**零改动** | `git show --stat d429411` ⇒ **只有 2 份 `.md`** |
| ② | 失败**自述** | 「`No space left on device`」＋ 栈顶 `File.WriteToFile` ⇒ 卡在**写盘**、不在断言 |
| ③ | **单跑 N/N 绿 ＋ 耗时** | `ResolveContext_MissingSdk…` **5 跑 5 绿**（21 / 21 / 18 / 21 / 22 ms） |
| ④ | 同命令**另开日志**复跑 | `s101-gate5b.log` **EXIT=0**、7 步全绿（2121 / 565＋2） |

⚠ 机制：沙箱 `/tmp` 是 **10 MB tmpfs**，设备测试那一支写 `generated_hmi.c` 时**瞬时写满**；
**重跑即绿**（本项目登记过的环境型随机红，技能 `dotnet-test-tempdir-hygiene`）。
⚠ **别挪 `TMPDIR`、也别改测试** —— 那等于把一个环境现象固化成代码改动。
⚠ 照实记：**gate5 ＝ 红（环境型）＋ gate5b ＝ 绿**，两跑**各一份日志** ——
本阶段刚在 §101.5.1 吃过「同路径覆盖」的教训，这里就不再犯第二次。

> **自指边界（如实标注）**：**补记批**（把上面这几行写上去的那一批）本身只改**本节**，
> 它的门禁 ＝ `s101-gate6.log`，读数记在**当日日志与收口清单**、**不回写本节** ——
> 否则每一批都要为「记录上一批的那一行」再开一批。
> **本节的最后一行，永远比它自己的那一跑早一次。**

### 101.11 诚实边界与未闭环

1. **两个 `status.verify.*` 站点仍是 `ex.Message`**（有意留）⇒ 直通面**不可能**降到 0。
   这不是"没做完"：`SystemFailureCodes.CodeFor` 对它们的异常类型返回 `null` ⇒
   换出口后渲染的是**回落分支**，用户所见一字不变。要收就**先有产出方**。
2. **新判据看不见的那一档**（已在判据 doc 里自陈）：字段 / 参数形态的目标路径；
   以及"承接变量被赋成**另一个也可能为 null 的变量**"（本判据只看右侧不是字面量
   `null`/`default`）。
3. **顺手发现、未定性的一条**（**登记不等于定性**）：`audit.verify.code.failed` 的渲染结果会经
   `AuthenticationService.RecordAudit` → `AppendAudit` 进审计日志，而审计日志**可落盘**
   （`project.settings.audit.path`，空 = 内存环形缓冲）⇒ 落盘的那条明细是**本地化文案**。
   本阶段**未替它定性**（审计记录按"当时读者的语言"存是否算缺陷，是产品判断，不是本族的口径）
   ⇒ 只在此登记，不动手。
4. **#61 家族**：本阶段收口 #61a 的**站点那一半**；仍开着的是
   ⚠ **阶段 102 已更新：本体 6 站 → 3 站，且 `buildrun.log.error` 被重新定档** ⇒
   见 §102.7 / §102.8。**本段（含下面两句）是阶段 101 当时的读数留档**，不改成新数字 ——
   改了 `6 ＋ 4 ＝ 10` 这条对账就指不回 §101.9 的 after 读数了：
   `#61（本体，还剩 6 站）` / `#61k` / `#61p` / `#61r` / `#61s`。
   ⚠ 口径必须与 §101.9 的直通面 **10 站**对齐（**少一多一都不许**）：
   - 本体**还剩 6 站** ＝ `compile.failed` ×2 / `download.sharedlibrary.failed` /
     `buildrun.log.error` / `comm.test.failed` / `login.failed`；
   - 另 **4 站**是**刻意保留**的「诚实边界」＝ `status.verify.failed` /
     `audit.verify.code.failed`（本阶段登记）/ `recipe.clone.failed` /
     `status.reload.failed`（阶段 98 登记，即 #61t / #61u）。
   （6 ＋ 4 ＝ 10 站，与 §101.9 的 after 读数**逐条对得上**。）

---

## §102 #61 本体 6 站：3 站真迁移 / 2 站「诚实边界」/ 1 站**被重新定档**；并新开一条面 #61x — 2026-09-25

**范围**（用户显式选定）：`compile.failed`×2 / `download.sharedlibrary.failed` **迁到**三实参出口；
`comm.test.failed` / `login.failed` / `buildrun.log.error` **只登记、不硬迁**；顺带给
「拿到 issues 列表却直接渲染回落原文」立一条**新面**（#61x）。

### 102.1 一句话结论

**六站不是一类、是三档**（读现场代码量出来的，不是按登记表抄的）：**三站真迁移**
（产出方是 BCL / IO，码在阶段 85 已就位）／**两站诚实边界**（换出口只会落回落分支）／
**一站把开工前的判断推翻了**（`buildrun.log.error` 属 A 类异常档、可码化）。
直通面 **10 站 / 9 组合 → 7 站 / 7 组合**；并交付一条**新面**（#61x，实测 1 站 / 1 文件）。

### 102.2 量的那一遍（读，不预设）

口径 = `ExceptionMessagePassthroughFaceTests.Baseline`（阶段 101 后 **9 组合 / 10 站点**）。

| 站 | 键 | `try` 内真 IO | 产出方给原因吗 | 判定 |
|---|---|---|---|---|
| `BuildPipelineService.CompileAsync:206/207` | `compile.failed` ×2 | 有（`CreateDirectory` ＋ 2× `WriteAllTextAsync`） | 有（BCL 7 码，阶段 85） | **真迁移** |
| `BuildPipelineService.EnsureSharedLibraryArtifactsAsync` | `download.sharedlibrary.failed` | 有（`CreateDirectory` ＋ 2× `WriteAllText`） | 有（同上） | **真迁移** |
| `CommunicationConfigView:1142` | `comm.test.failed` | ❌ `driver.ValidateConnection` **返回** `ValidationResult`（四个内置驱动该接口**零 `throw`**） | 否 | 诚实边界 |
| `LoginDialog:84` | `login.failed` | ❌ `AuthenticationService` 零 `File.` / `Directory.`；`:55` 门槛已与 `ThrowIfNullOrWhiteSpace` 对齐 | 否 | 诚实边界 |
| `BuildRunDialog:101` | `buildrun.log.error` | **有** | **有**（产出方**真抛**中文 `InvalidOperationException`） | ⚠ **A 类异常档 · 可码化**（本阶段**不动**） |

🩸 **最后一行推翻了本阶段开工前的判断** —— 开工时把它与上面两条同档（"外部工具输出、原文有用、
不迁"）。读现场后实测：

```csharp
// src/HmiEditor.App/Services/SimulatorBuildService.cs
:102  throw new InvalidOperationException($"LVGL 源码无效: {lvglDir}（缺少 lvgl.h 或 CMakeLists.txt）");
:105  throw new InvalidOperationException("未找到 cmake，请安装 CMake 后重试（Linux: sudo apt install cmake）");
:188  return new SimulatorBuildResult(true,  exe,  log.ToString(), errors);
:200  return new SimulatorBuildResult(false, null, log.ToString(), distinctErrors);
```

⇒ **编译失败走「返回值」**（编译器输出进 `SimulatorBuildResult.Log` / `errors`），而
`BuildRunDialog:101` 那个 `catch` 接的是**真抛**的那两条前置校验 —— 产出方**在本仓库**、
语义明确、消息本来就是中文 ⇒ 它是 **A 类异常档**，要的是「给产出方建码」，
**不是**「登记为不迁」。⚠ 按「一次只放开一个变量」，本阶段**不折进来**（另开一刀）。

### 102.3 三站真迁移（逐站）

**(a) `compile.failed` × 2 —— 补漏两行，不是新设计**

同一个 `catch` 里**审计那一半阶段 86 就迁完了**（`:208` 已是
`FailureDetail(Lang.UiOpExportCode, targetDir, ex)`），漏的只有两行状态栏：

```csharp
catch (Exception ex)
{
    SetCompileBarValue(0);
    // 状态栏用「编译工程」（与工具栏命令、MainWindow 的同一操作 catch 同名）；
    // 审计沿用「导出代码」（与成功行 audit.export.c.code 同口径）—— 两个通道各按自己那半的措辞。
    var reason = UiOperationGuard.FailureDetail(Lang.UiOpCompileProject, targetDir, ex);
    SetCompileStatusText(reason);
    SetStatus(reason);
    AuditExport(UiOperationGuard.FailureDetail(Lang.UiOpExportCode, targetDir, ex), false);
}
```

- 承接变量 `targetDir` **阶段 86 就已提到 `try` 外** ⇒ 本刀**零新增**（同文件 `:114` 是先例）。
- **没有**把审计那一半也改成「编译工程」：两个通道各自与它那一半的既有口径对齐
  （状态栏 ↔ 工具栏命令 / `MainWindow` 同一操作的 `catch`；审计 ↔ 成功行 `audit.export.c.code`）。

**(b) `download.sharedlibrary.failed` —— 新提承接变量 ＋ 新拆操作名**

```csharp
// 目标目录在 try 内才确定 ⇒ 外层承接变量交给 catch（阶段 102：#61「带上对象」）
string? dir = null;
try
{ … dir = AppPaths.TempSubdir("download"); Directory.CreateDirectory(dir); … }
catch (Exception ex)
{
    // 承接变量可能仍为 null（TempSubdir 自己就抛了）⇒ 出口自动回落 2 实参版
    SetStatus(UiOperationGuard.FailureDetail(Lang.UiOpPrepareSharedLib, dir, ex));
    return false;
}
```

- `string? dir = null;` 必须写在 `try` **外**（写在里面 `catch` 取不到 —— `CS0103` 当场报；
  **不许写 `null` 糊过去**，那会同时踩红 `EveryDetailCallSite_PassesAConcreteTargetPath`）。
  出口对 `null` 路径**自动回落 2 实参版**，所以「`TempSubdir` 自己就抛」那一档不会渲染出空路径。
- 操作名**新拆** `UiOpPrepareSharedLib`（「准备共享库」）：这一站做的是「下载**前**准备 .so 产物」，
  与 `UiOpDownloadHmi`（下载动作本体）是**两个操作** —— 共用会让一个键服务多个语义
  （阶段 86 拆 `UiOpExportCode` / `UiOpExportRecipe` 同一条理由）。

⚠ 这两族**不是改名式假闭环**：BCL 类型有码（`SystemFailureCodes`，阶段 85）⇒ 原因那半由
**0 实参短句**渲染 ⇒ 迁过去用户**同时**读到「哪个文件」＋「为什么（他的语言）」
（与阶段 86 / 90 / 101 同一判定）。

### 102.4 字典处置链（预算得准，实测逐项吻合）

两个键的**使用点写的是字面量**（不是 `Lang.X`）⇒ 两个常量**零代码引用**、只靠 M6 桶担保
⇒ 迁走后键不可达：

| # | 动作 | 实测 |
|---|---|---|
| ① | 删 2 键：`compile.failed` / `download.sharedlibrary.failed`（两本字典） | 字典 2283 → **2282**、有常量的键 1816 → **1815** |
| ② | **连 `Lang` 常量一起删**：`CompileFailed` / `DownloadSharedLibFail` | `UnusedBaseline` 1110 → **1108** |
| ③ | 同步分桶 | **M6 259 → 257**（M2 828 / M3 18 / 无机制 5 不动） |
| ④ | 清 `sync_localization.py` `VALUE_MAP` | 两键不在其中 ⇒ 无动作 |
| ⑤ | 查 `KeptUnreachable` | 两键不在其中 ⇒ 无动作 |
| ⑥ | 加 1 键 `ui.op.prepare.shared.lib` ＋ 1 个操作名常量 | M1 面 706 → **707** |

⚠ `UiOpPrepareSharedLib` **有代码引用** ⇒ 它**不进** `UnusedBaseline`（那个集合装的是
「**无**代码引用的常量」）—— 与阶段 99 同一条区分：「字典净 −1 而 `UnusedBaseline` −2」
「`UnusedBaseline` −2 而 M1 面 ＋1」**都不矛盾**，它们数的集合不同。

### 102.5 新面 #61x —— 「拿到 issues 列表却直接渲染回落原文」

**为什么必须新开一条面（不是重复劳动）**：直通面的谓词里**有 `Exception`**
（`.Format(键, …, ex.Message)`）⇒ 「**记录体**（有 `Code`）的 `Message` 被当成文案直显」
这一类**原理上看不见**。**面的边界 ≠ 没有缺口** ⇒ 要收得**先立判据**。

**缺口**：`CommunicationConfigView.OnTestConnection`

```csharp
string.Join(", ", result.Issues.Select(i => i.Message))
```

产出方 `Hmi.Communication.Contracts.ValidationIssue(Severity, Code, Message)` **给了码**，
而消费点**只读 `Message`** —— 那是 Core 的**中文回落** ⇒ **英文界面下这半照旧中文**
（中文界面下字典值与回落原文**按设计逐字相同**，所以只在英文下看得见）。

**同族其余八处都走出口**（`ExternalText.Resolve(<族>IssueKey(code), issue.Message, issue.Args)`）：
`Describe` 的 SVG / AI / 配方 / 配方导入 / CSV / 登录**六族**，加
`ProjectSettingsView.LocalizeIssue`（硬件族）与 `UserAdminException` 的 `base(issue.Message)`。

⚠ **通讯族与兄弟族不同形**：它**没有 `Args`**，码也是**大写诊断码**
（`MISSING_ENDPOINT` / `INVALID_PORT` / `UNUSUAL_BAUD` …）而不是「码 → 字典键」直接对应
⇒ **收口要动记录形状**（补 `Args` ＋ 建 `comm.issue.*` 码表 ＋ 出口），是**独立的一刀**
⇒ 本阶段**只定量、只交付判据**。

**面的口径**（三处取舍）：① 语料 = `src/` 全部产品源、**先剥注释**（否则规则会被自己的文档满足）；
② 站点 = 同一**语句**内既有 `.Issues` 又有 `.Message`（按 `;` 切 —— 链式调用会跨行），
并**排除产出侧** `.Issues.Add(` / `.Issues.Clear(`（前瞻用 `[ 	]`、**不跨行**）；③ 基线**双向**。

🩸 **它看不见什么（自陈，别当成覆盖到了）**：把 `.Issues` 存进**中转变量**再读
（`var issues = r.Issues;` 然后 `issues[0].Message`）、或 `foreach` 拆开写 ⇒ **不是站点**。
这是**位置式**规则的通病（与 `EveryConveyedPathVariable…` 同一条边界）。
**NC5 把这一档显式钉成「看不见」** —— 免得下一个人以为它被盖住了。

### 102.6 新面的生产负控（真仓库注入 · 每支独立日志）

注入的是**能编译**的坏形状：把 `ProjectSettingsView` 里一处**合规**的 `LocalizeIssue(i)`
换成 `i.Message`（`sha256` 前 `a64278a8…`）。

| 支 | 日志 | 时刻 | 该轮实测 |
|---|---|---|---|
| 注入态**构建** | `s102-nc-build.log` | 16:29:22 | **0 警告 / 0 错误**（坏形状必须先能编译） |
| 注入态 · **新面** | `s102-nc-inject.log` | 16:29:23 | **红恰好 1 条**（`失败: 1，通过: 2，总计: 3`），点名「**新增** …/ProjectSettingsView.axaml.cs」；读数 **2 站 / 2 文件** |
| 注入态 · **直通面** | `s102-nc-inject-face.log` | 16:29:24 | **3/3 全绿** ⇒ **盲区实证** |
| 还原态 · 构建 | `s102-nc-rebuild.log` | 16:29:33 | 0 警告 / 0 错误 |
| 还原态 · 两面 | `s102-nc-restore.log` / `…-restore-face.log` | 16:29:35 / 16:29:36 | **都 3/3 绿** |
| 还原一致性 | — | — | 写回**原文内容**（**不是** `copy2`，那会带 `mtime`）；工作树**干净** ⇒ 现值可复算 |

⚠ 「注入 ⇒ **只有**新面红 / **直通面仍全绿**」这一对才是新面的**价值证明**：它覆盖的是
**别人原理上看不见**的东西，不是重复劳动。

### 102.7 读数（`HMI_READINGS_OUT` **绝对路径**；两段 `E1=0 E2=0`）

改前 `.workbuddy/tmp/s102-readings-before.txt`（**birth 16:21:30 / mtime 16:22:03**）；
改后 `…-after.txt`（**birth 16:25:04 / mtime 16:25:37**）。

| 量 | 改前 | 改后 | 说明 |
|---|---|---|---|
| **直通面** | **10 站 / 9 组合** | **7 站 / 7 组合** | 本阶段唯一"该动"的量 |
| `.Format` 站点 / 索引器 / 合计 | 306 / 805 / 1111 | **303** / 805 / **1108** | 减的正是三站 |
| 无代码引用的常量（`UnusedBaseline`） | 1110 | **1108** | −2 = 两个死常量退场 |
| 分桶 M2 / M3 / **M6** | 828 / 18 / **259** | 828 / 18 / **257** | 两个键的担保来自 M6 |
| **M1 面** / M6 面 / 残差键 | 706 / **370** / **375** | **707** / **368** / **373** | ＋1 = 一个**有代码引用**的新常量；M6 面 −2 |
| 字典键（zh-CN） / 有常量 / 无常量 | 2283 / 1816 / 467 | **2282** / **1815** / 467 | −2 ＋1 |
| 可达机制 | 18 条 | **18 条**（本阶段**不新增机制**） | 新键走 M1 |
| **新面 #61x** | —（**不存在**） | **1 站 / 1 文件** | 本阶段新开 |
| `App.UiTests`（读数跑口径） | 565 ＋ 2 | 565 ＋ 2 | 两次读数跑都只覆盖**内容批**；判据批的 +3 见 §102.8 |

⚠ **「读数晚于什么」要写准**：两段都是**工作树读数** ⇒ 只要求晚于**改动落盘**。
内容批落盘 = **16:23:01–16:23:09**（`git` 提交是 16:26:09，在其后）——

- 改前 16:22:03 **早于**内容批落盘 ✔（读的是"改之前"）
- 改后 16:25:37 **晚于**内容批落盘 ✔ ⇒ 读的确实是改后状态

🩸 但**改后读数早于内容批提交 32 秒** ⇒ **不能**照抄阶段 101 那句「晚于提交」的写法。
**照实记成「晚于改动落盘 / 早于提交」** —— 提交不是被读的对象，工作树才是。

### 102.8 门禁（每一次绿都晚于它要证明的那次提交；**已回填到 `gate4`**）

| 时刻 | 日志（birth / mtime） | 批次 / 提交 | 结果 |
|---|---|---|---|
| 16:26:09 | — | **内容批** `d2f23a9` | 6 files，**＋24 / −17** |
| **16:26:16 / 16:27:39** | `s102-gate1.log` | 内容批门禁（7 步） | **7 步全绿**、0 警告 0 错误；`Core.Tests` **2121** / `App.UiTests` **565** ＋ 2 |
| 16:30:03 | — | **判据批** `c7ae688` | 2 files，**＋250** |
| **16:30:03 / 16:31:05** | `s102-gate2.log` | 判据批门禁（7 步） | **7 步全绿**；2121 / **568** ＋ 2 |
| **16:31:16 / 16:32:21** | `s102-gate2b.log` | 同上，**另开日志复跑** | **7 步全绿**；2121 / 568 ＋ 2 |
| 16:39:46 | — | **文档批** `a7d5693` | 4 files，**＋405 / −4**（⚠ **含一行源码注释**） |
| **16:39:50 / 16:41:08** | `s102-gate3.log` | 文档批门禁（7 步） | **7 步全绿**、0 警告 0 错误；2121 / 568 ＋ 2 |
| 16:43:40 | — | **记忆批** `bf87c2a` | 2 files，**＋139** |
| **16:43:43 / 16:44:46** | `s102-gate4.log` | 记忆批门禁（7 步） | **7 步全绿**、0 警告 0 错误；2121 / 568 ＋ 2 |

🩸 **`gate2` 的 birth 与判据批提交落在同一秒**（16:30:03.271 vs 16:30:03）⇒
「每一次绿都必须晚于它要证明的那次提交」**不够干净**。**处置 = 另开一份日志、同一条命令复跑**
（`s102-gate2b.log`，晚于提交 **1 分 13 秒**，同样全绿）。
⚠ **不是**把 `gate2` 那一条时间点说成"有效" —— 记下来的是**两条日志、两个时刻**。

🩸 **另有一次「红」值得记**：改后读数**第一次跑 `E1=1`**，红的是
`ExceptionMessagePassthroughFaceTests.Face_IsNotDegenerate`（面从 10 站掉到 7 站，
撞穿自检下界 `>= 10`）⇒ ⚠ **面掉下来时先红的是自检、不是主判据**。
处置 = **把下界往下放一档**（10 → **5**）并同步理由文字，**不删**自检（它是「口径失效」的探测器）；
⚠ 下界**刻意不贴实测值**（7 的约 2/3），贴了会让以后**每一次正确**的迁移都多红一条。

> **自指边界（如实标注）**：**回填批**（把上面 `gate3` / `gate4` 两行写上去的那一批）
> **自己还要过一遍门禁** ⇒ 它的读数（`s102-gate5.log`）记在**当日日志与收口清单**、**不回写本节**
> —— 否则每一批都要为「记录上一批的那一行」再开一批。
> **本节的最后一行，永远比它自己的那一跑早一次。**
> ⚠ 文档批还**顺带改了一行源码注释**（`ExceptionMessagePassthroughFaceTests` 类注释里 #61c 那个
> bullet 已被本阶段推翻）⇒ 那一批**不是纯 `.md`**；但注释**不进 IL** ⇒ 实测它的门禁读数
> 与判据批**逐项相同**（`gate3` / `gate4` 都是 2121 / 568 ＋ 2）—— 所以上一版写的
> 「不能按"逐项相同"预期」**被实测否定**，就地改掉（留着会变成一条凭推测写的纪律）。

### 102.9 诚实边界与未闭环

1. `comm.test.failed` / `login.failed` **有意留** `ex.Message`：前者产出方
   `driver.ValidateConnection` **返回** `ValidationResult`（四个内置驱动该接口零 `throw`），
   后者那个 `try` 里**没有任何文件 IO** ⇒ 换出口只会落**回落分支**、用户所见**一字不变**
   （与 #61t / #61u 同一条边界）。
2. 🩸 **本阶段更正了一处既有定性**（见 §102.10）：`buildrun.log.error` 从「B 类 · 不迁」
   改成 **A 类异常档**。**未闭环**：要给它建码（产出方在 App 层、两条前置校验，语义明确）。
3. **同一 `catch` 的第二条露出点**：`BuildRunDialog:102`
   `ShowErrors(new[]{ ex.Message })` —— 走 `ShowErrors` 而非 `.Format` ⇒ **不在直通面上**，
   本阶段**只登记、不动手**（它不是同一族，但**同源**）。
4. **#61x 本体未收口**：通讯族的收口要**动记录形状** ⇒ 独立的一刀；本阶段交付的是**判据**。
5. **新面看不见的那一档**（已自陈、NC5 钉住）：中转变量 / `foreach` 拆开写。
6. **家族**：`#61（本体，还剩 3 站）` / `#61k` / `#61p` / `#61r` / `#61s` / **新增 `#61x`**。
   ⚠ 口径必须与直通面 **7 站**对齐（**少一多一都不许**）：
   - 本体**还剩 3 站** ＝ `buildrun.log.error` / `comm.test.failed` / `login.failed`；
   - 另 **4 站**是**刻意保留**的「诚实边界」＝ `status.verify.failed` /
     `audit.verify.code.failed` / `recipe.clone.failed` / `status.reload.failed`。
   （3 ＋ 4 ＝ 7 站，与 §102.7 的 after 读数**逐条对得上**。）

### 102.10 ⚠ 更正：登记表 §4 #61c 那一行的定性

原 #61c 写的是「**#61 的 B 类：原因是**外部工具输出** ⇒ 登记为"不迁"（4 站）**：
`compile.failed`(2) / `buildrun.log.error` / `download.sharedlibrary.failed`；实参分别来自
**编译器·Make 输出、构建日志正文、外部产物下载**」。

**这个归因在代码里不成立**，逐条取证：

| 原名目 | 实际产出方 | 更正后的档 |
|---|---|---|
| `compile.failed` × 2 | `BuildPipelineService.CompileAsync` 的 `try`：`CreateDirectory` ＋ 2× `File.WriteAllTextAsync` ⇒ **本方法的 IO**；编译器输出走 `result` / 日志，**不抛** | **C 类 · 已迁**（阶段 102） |
| `download.sharedlibrary.failed` | 同文件 `EnsureSharedLibraryArtifactsAsync`：`Directory.CreateDirectory` ＋ 2× `File.WriteAllText` | **C 类 · 已迁**（阶段 102） |
| `buildrun.log.error` | `SimulatorBuildService` **真抛**中文 `InvalidOperationException`（`:102` / `:105` 两条前置校验）；可执行体的编译失败走 `SimulatorBuildResult` **返回值** | **A 类异常档 · 可码化**（本阶段不动） |

⇒ **更正后：本项点名的四个名目，没有一个还留在「外部工具输出」那一档。**

⚠ **留一个问题给下一个人**：本阶段**没有**重新扫全库去找**真正的**「外部工具输出直通文案」
站点 —— 若那种站点今天确实**不存在**，那么「B 类」应当从三分类里**删掉**，
而不是留一个空档继续被引用。

⚠ 别处引用「与 #61c 同类（判过的不做）」（#61k / #61m 行、
`src/HmiEditor.Core/Services/GeneratedCodeIssueCodes.cs:114`）说的是**同一类「不迁」的动机**
（产物可复现 ⇒ 翻译等于改写产物），**不是**同一类**成因** ⇒ 那些措辞**不受本更正影响**；
但**读的时候别把它们当成「B 类还有站点」的证据**。

## §103 模拟器编译 / 启动失败档建码（`SimulatorBuildException` / `sim.build.issue.*`），并给「B 类」下**删除**定论 — 2026-09-26

### 103.1 本刀吃哪两条

阶段 102（§102.10）留下三条未闭环：① `buildrun.log.error` 要**给产出方建码**（A 类异常档）；
② **#61x 本体**（通讯族要动记录形状）；③「**真正的 B 类站点今天还在不在**」。
本阶段吃掉 ① 与 ③；② 独立一刀 ⇒ **§104**。

### 103.2 量的那一遍（只读，动手前）

改动前跑 23 条相关守卫 **23/23 全绿**（`.workbuddy/tmp/s103-probe.log`，`EXIT=0`）。
`SimulatorBuildService` 里**该建码的抛出 2 处**：

| 位置 | 回落原文（逐字保留在抛点） |
|---|---|
| `SimulatorBuildService.cs:102` | `LVGL 源码无效: {lvglDir}（缺少 lvgl.h 或 CMakeLists.txt）` |
| `SimulatorBuildService.cs:105` | `未找到 cmake，请安装 CMake 后重试（Linux: sudo apt install cmake）` |

- `:97-99` 三行 `ArgumentNullException.ThrowIfNull`（`project` / `workDir` / `lvglDir`）**刻意不码化**
  —— 那是**调用方编程错误**，不是用户能撞到的运行条件（与 `SystemFailureCodes` 不码
  `ArgumentException` 家族、`UserAdminIssueCodes` 保留 `ThrowIfNullOrWhiteSpace` 同一条边界）。
- `BuildRunDialog.axaml.cs:139`「无法启动模拟器进程（`Process.Start` 返回 `null`）」是第三处候选，
  它挂在**同一个** `catch` 的下游 ⇒ 一并收口。

### 103.3 产出方：**换异常类型**作取证手段

新增 `App/Services/SimulatorBuildIssueCodes.cs`（3 码，**只放码** —— 回落原文若也写成常量，
反射会把它当成一条**假码**、机制面虚高 1）与 `App/Services/SimulatorBuildException.cs`（`Code` + `Args`）。

| 码 | 实参 | 为什么是这些实参 |
|---|---|---|
| `LVGL_SOURCE_INVALID` | 1（目录） | 「是哪个目录」只有产出方知道，而用户多半是配置里填错了路径 |
| `CMAKE_NOT_FOUND` | 0 | 用户要知道的是「装一个 CMake」，不需要知道我们去哪些目录找过 |
| `PROCESS_START_FAILED` | 0 | 原文只有六个字，连可执行文件都没带（它就在 `SimulatorBuildResult.ExecutablePath` 里） |

🩸 **新类型刻意「不继承」旧基类**（`Exception` 而非 `InvalidOperationException`）——
否则老 `catch` 继续静默命中、**收口在编译面看不见**；换基类的代价正是「编译器当场点出全部消费点」。

🩸 **回落原文逐字留在抛点、不搬进工厂**：本族是「**码表与产出方同在 `HmiEditor.App`**」的
**第一族**，而 `HardcodedLocalizationGuardTests` 对 `src/HmiEditor.App` **零容忍**、
但它**本来就豁免** `throw new …Exception("…")` 那一类 ⇒ 不必给扫描器开文件级后门。
**这是扫描面造成的，不是风格选择。**

### 103.4 消费方：一处原因、两条露出点

`BuildRunDialog` 只有**一个** `catch (Exception ex)`，但它有两个露出点 ——
`AppendLog(Format(Lang.BuildrunLogError, …))`（**在直通面上**）与 `ShowErrors(new[] { … })`（**不在**）。
本刀改成**只取一次原因**，两处共用：

```csharp
catch (Exception ex)
{
    var reason = ExternalText.FailureReason(ex);
    AppendLog(AppLocalization.Service.Format(Lang.BuildrunLogError, reason));
    ShowErrors(new[] { reason });
    Finish(AppLocalization.Service["buildrun.status.failed"]);
}
```

### 103.5 出口与字典

- `ExternalText` 新增 `SimulatorBuildIssueKeyPrefix = "sim.build.issue."` 与
  `SimulatorBuildIssueKey(code)`；`FailureReason` 新增 **②‴ 支**（位置在 ②″
  `SimulatorNotConnectedException` 之后、④ `SystemFailureCodes` 之前）。
  ⚠ 前缀**单开一族**，**不是**既有 `sim.issue.`（M15，模拟器**未连接**）的兄弟 —— **一条前缀一张码表**。
- 两本字典各 **+3** 键：**2282 → 2285**。zh 面向用户措辞与回落原文**刻意不同**
  （回落是作者口吻 ＋ 排障细节）。

### 103.6 新判据类（6 条 ＋ 内嵌负控 NC0–NC5）

`tests/HmiEditor.App.UiTests/SimulatorBuildIssueLocalizationGuardTests.cs`：

- **复用** M16/M18 的四个纯函数（`RenderRow` / `RenderViolations` / `PairingViolations` /
  `ClosureViolations`），不抄第三份。
- **三态闭包**读数：声明 **3** 个码 / 真实产出 **2** 个 / 明确产不出 **1** 个
  （`PROCESS_START_FAILED` 进 `NotProducible` —— `Process.Start` 返回 `null` 是文档允许、
  实测撞不到的分支）；渲染见证 **3** 条（2 条真产出 ＋ 1 条手工构造）。
  ⇒ 「它确实挂在产出方的失败分支上」由 `ProducerViolations` 的**每条码恰好 1 次**担保，
  否则 `NotProducible` 会把一条**真死码**合法化（登记 #49）。
- **毒化法**：把回落原文换成 `Poison = "<<中文回落原文未被使用>>"` 再渲染 ⇒
  证明「字典在决定文案」。⚠ **不能**拿真原文比对：zh 字典值与回落原文**刻意不同**。
- `MaskArgs`：实参是**用户数据**（目录路径），拿 CJK 扫渲染串会误判 ⇒ 先掩成 `<<ARG>>`；
  第二半扫 en 的**模板本体**（M18 第一版拿 `«实参»` 当掩码、它自带 CJK ⇒ 带实参的码全红）。
- 🩸 **面看不见缺口**（新守卫顺手补的那一条）：直通面的谓词只认 `.Format(键, … .Message)` ⇒
  同源的 `ShowErrors(new[] { ex.Message })` **原理上不在它的视野里** ⇒
  在新守卫里用**形状签名**钉住（NC4-③ 有负控钉「看不见」）。

### 103.7 🩸 定论：「B 类（外部工具输出）」从三分类里**删掉**

它不是「暂时空着」，而是**结构上不可能有成员**：那张面的谓词要求一个 `catch` 到的**异常**，
而外部工具（cmake / gcc / git / 交叉编译器）的失败**一律走返回值** ——

| 外部工具 | 失败怎么回来 | 出处 |
|---|---|---|
| cmake configure / build | `int` 退出码，错误行进 `SimulatorBuildResult.Errors` | `SimulatorBuildService.RunProcessAsync` |
| T113 / RK3506 构建 | 读 `result.Error` | `BuildPipelineService:313` |
| git | `(ExitCode, stdout, stderr)` 三元组 | `GitWorkflowService` |

退出码与 stderr 是**数据**、不是异常 ⇒ 这一档在面上**永远不会有站点**。
⇒ 三分类收窄为**两类**：**A 类**（本仓库产出方，可码化）与 **C 类**（BCL / IO 原文，`#61e`
之后同样可码化，但必须走带目标路径的三实参出口）。

### 103.8 读数

| 量 | 前 | 后 | 说明 |
|---|---|---|---|
| **直通面**（`ExceptionMessagePassthroughFaceTests`） | 7 组合 / 7 站点 | **6 组合 / 6 站点** | 本族**第八次**真的把它改小 |
| 可达机制条数 | 18 | **19** | 新增 **M19** |
| M19-模拟器编译码 | —（不存在） | **3** | ＝ 码数 |
| M1 / M6 / M7 面 | 707 / 368 / 12 | **不动** | 本族无 `Lang` 常量 |
| 两本字典键数 | 2282 | **2285**（+3） | 键集一致 |
| `UnusedBaseline` | 1108 | **1108（不动）** | 键靠推导、不进本集 |
| `.Format` 站点 | 303 | **303** | 站点还在，只是实参换了（不是删站点） |
| `App.UiTests`（大队） | 568 | **575** | ＋7 ＝ 6 条判据 ＋ 1 条负控 |

⚠ **本节的机制面 / 字典读数取自两处，其中一处不是"当天的日志"**：字典键数由
`git show <提交>:…Strings.zh-CN.json` **静态统计**（可复跑）得出；M19 面 3、
`M1 / M6 / M7` 面取自 `.workbuddy/tmp/s104-readings.log`（阶段 104 的读数跑）——
这几个量在 103 → 104 之间**同值**（两个阶段都没加 `Lang` 常量），
但**阶段 103 当时没有留下自己的读数日志** ⇒ 如实标注，**别写成"103 当天的读数"**。
`App.UiTests` 的 575 则确实来自 `s103-gate1.log`（当天）。

### 103.9 门禁（每一次绿都晚于它要证明的那次提交）

| 时刻（birth / mtime） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 14:50:20 | — | **内容批**（含判据）`a53b680` | 11 files，**＋1022 / −15** |
| **14:50:30 / 14:51:46** | `s103-gate1.log` | 内容批门禁（7 步） | **7 步全绿**、0 警告 0 错误；`Core.Tests` **2121** / `App.UiTests` **575** ＋ 2 |
| 15:01:25 | — | **文档批**（s103 ＋ s104 **合批**）`320c28d` | 3 files，**＋432 / −3** |
| **15:01:29 / 15:02:35** | `s104-gate2.log` | 文档批门禁（7 步） | **7 步全绿**、0 警告 0 错误；`Core.Tests` **2121** / `App.UiTests` **579** ＋ 2；路径卫生 **667** |
| 15:09:23 | — | **记忆批**（同一次合批）`99f6fde` | 2 files，**＋179**；`MEMORY.md` 7943 → **7961** 码元（快照 26q） |
| **15:09:26 / 15:10:41** | `s104-gate4.log` | 记忆批门禁（7 步） | **7 步全绿**（尾行「🎉 门禁全部通过（7 步）」）；2121 / **579** ＋ 2；⚠ 尾部 `GATE_EXIT` 行**没写进去**（收尾那半句在 fish 里 `$?` 直接报错）⇒ 见下一行 |
| **15:10:53 / 15:11:59** | `s104-gate4b.log` | 同上、**另开日志复跑** | `GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；2121 / 579 ＋ 2；路径卫生 **667** |

⚠ **本阶段把"判据"折进了内容批**：新守卫如果不跟内容一起提交，`IssueMessagePassthroughFaceTests`
的基线会先变红（面从 7 站掉到 6 站）⇒ 门禁必红。这与阶段 100 的批次切分一致
（那一批也是「新记录体 ＋ 键推导 ＋ 字典 ＋ 新守卫 ＋ 可达机制」同一个内容批）。
🩸 `s103-gate1.log` 的 **birth 14:50:30 晚于提交 14:50:20（10 秒）** ——
判据是**日志晚于它所证明的那次提交**，不是晚于"同一条消息里的某个动作"。
（上一版这里犯过一次：把"晚于改动落盘"抄成了"晚于提交"；两者不是一回事，见 §102.7。）

⚠ **本表由回填批补到 `gate4b`**（文档批 / 记忆批 ＋ 两个 `gate4*`）。
🩸 按**自指边界**：回填批自己也要过门禁 ⇒ 它的读数（`s104-gate5.log`）记在**当日日志与收口清单**、
**不回写本节** —— 否则回填批每写一行，都要再为「记录上一批」新开一批。

### 103.10 诚实边界与未闭环

1. `ShowErrors(new[] { reason })` 走的是**另一个出口**，本阶段只把它**收成同一次取值**，
   **没有**给它单独立判据（那半张面在 `IssueMessagePassthroughFaceTests` 的谓词之外 —— 这正是
   §103.6 那条**形状签名判据**要覆盖的东西）。
2. `ArgumentNullException.ThrowIfNull` 三行**刻意不码化**（调用方编程错误档）。
3. **#61x 本体未收口** ⇒ 下一刀就是 **§104**。
4. **家族**：`#61（本体）` / `#61k` / `#61p` / `#61r` / `#61s` / `#61x`。
   直通面 6 站 ＝ 本体 **2 站**（`comm.test.failed` / `login.failed`）＋ 4 站**刻意保留**的
   诚实边界（`status.verify.failed` / `audit.verify.code.failed` / `recipe.clone.failed` /
   `status.reload.failed`）。

## §104 通讯族校验失败档收口（契约补 `Args` ＋ `comm.issue.*` ＋ 视图出口），#61x 那条面**收官为零容忍** — 2026-09-26

### 104.1 本刀吃哪一条

阶段 102（§102.9 第 4 条 / §102.10）留下：**#61x 本体未收口** —— 通讯族的
`ValidationIssue` **没有 `Args`**、消费点也**不读 `Code`**（直接 `i.Message`）⇒ 界面侧拿不到
"该查哪个键"，收口要**动记录形状**。

### 104.2 量的那一遍：🩸 **单行正则会漏码**

按 `issues.Add(new(ValidationSeverity.…, "…", …))` **单行**扫，只数到 **7** 条码 ——
`OpcUaDriver` 的 `INVALID_ENDPOINT` 是**多行** `issues.Add`（码与文案分两行），**被漏掉**。
按「**真触发**」（拿两份参数真跑每个驱动的 `ValidateConnection`）重数 ⇒ **9 条码 / 4 个驱动**：

| 驱动 | 码 | 严重度 | 实参 |
|---|---|---|---|
| `modbus-tcp` | `MISSING_HOST` / `INVALID_PORT` | Error | 0 / 0 |
| `modbus-rtu` | `INVALID_SLAVE_ID` / `MISSING_PORT` / **`UNUSUAL_BAUD`** | Error ×2 ＋ **Warning** | 0 / 0 / **1** |
| `opc-ua` | `MISSING_ENDPOINT` / `INVALID_ENDPOINT` | Error | 0 / 0 |
| `mqtt` | `MISSING_BROKER` / `MISSING_CLIENT_ID` / `INVALID_PORT` | Error | 0 / 0 / 0 |

⇒ 这就是"量的那一遍"存在的理由：**形状规则（grep / 正则）量不出语义面**。

### 104.3 契约：补 `Args`

`Hmi.Communication.Contracts/ValidationResult.cs` 的
`ValidationIssue(Severity, Code, Message)` 补 `IReadOnlyList<object>? Args = null`
（可省略 ⇒ **既有 8 个构造点一处没动**）⇒ 与 `Hmi.Hardware.Contracts.ValidationIssue`
**同形**。类注释写清两件事：「**原先为什么没有它**」（消费点不读 `Code`、也没有实参，
所以"缺参数 / 缺翻译"一直不可见）与「补上之后两族同形」。

### 104.4 键推导与消费点

- `PluginTextKeys.CommIssueKey(code) => "comm.issue." + code.ToLowerInvariant()`，与硬件族的
  `IssueKey`（`hw.issue.`）**并列**，**不抽**「带前缀参数」的方法：前缀一旦成为调用方的
  自由变量，**写错不会有任何编译期反馈** —— 只会静默回落驱动中文，正是这套机制要防的症状。
  类注释的「约定」清单 **四条 → 五条**。
- `CommunicationConfigView.LocalizeIssue(ValidationIssue)` 薄封装（与
  `ProjectSettingsView.LocalizeIssue` 同一形状）⇒ 站点改 `.Select(LocalizeIssue)`。
- 两本字典各 **+9**（**2285 → 2294**，键集一致）：zh 措辞与驱动回落原文**刻意不同**
  （回落是驱动作者口吻，字典面向用户），`comm.issue.unusual_baud` 另外把常用波特率列进模板。

### 104.5 判据（三条面，全部复用既有纯函数，不抄第三份）

| 判据 | 担保什么 |
|---|---|
| `CommIssueRegistry_MatchesWhatTheDriversActuallyProduce` | 登记表 × **触发面** 双向：驱动多一个码没登记 ⇒ 红；表里留着**再也产不出**的码 ⇒ 也红 |
| `CommIssueTemplates_MatchTheArgumentCountsTheDriversPass` | 占位符个数与**产出方真传的 `Args`** 一致（**同源**）；⚠ 与硬件族那条**刻意不同形**（那里只收 Code、靠手写"决定表"）—— **同族第二例要分档** |
| `PluginIssueOutlet_IsSingle_AndViewsDoNotSpellThePrefix` | 两个插件族的薄封装都必须调**它那一族的**键推导 ＋ 唯一出口 `ExternalText.Resolve` ＋ **不许自拼前缀字面量**（**4 支负控**，含"注释里的违规写法不算"） |

`CommIssueRegistry` 是**显式登记表**而不是"反射驱动里的字符串"：本族的码住在**插件程序集**
（`Hmi.Communication.Builtin`），App 侧拿不到它们的声明面 —— 只能真触发一遍把码收上来；
而"触发面"天生会漏（换个人写探针就可能漏一条规则）⇒ 用登记表**双向**夹住它。
⚠ 这是同族**第三态为空**的情形（9 条全部产得出）⇒ **没有** `NotProducible` 那一栏。

### 104.6 面收官：从「定量清单」升格「零容忍」

`IssueMessagePassthroughFaceTests`：**基线清空**、`Diff` 加 `baseline` 参数、
自检里的 `sites.Count >= 1` 换成 `Assert.Empty(sites)` ⇒
**1 站 / 1 文件 → 0 站 / 0 文件**，本面从「定量清单」升格为「**零容忍**」。
⚠ NC6 原先靠真语料触发"已消失"那一半，**基线清空后再也触发不到** ⇒ 改喂**合成基线**
（与语料同一个理由：判据的输入当参数传入，负控才不必去改共享状态）。

🩸 **本阶段负控写错两次**（都是**我自己写的语料**有缺陷，不是产品缺陷 —— 与阶段 103 的
`Corpus(body)` 同一个坑的第二次）：
① 「把违规写法全注释掉 ⇒ 零违规」**期望写反了** —— 剥注释之后那份语料**确实什么都没调**，
   **该报**；真正的负控应当是"**代码正确**的语料 ＋ 注释里写着前缀字面量" ⇒ 零违规；
② 合成基线那一支喂的是 `new[] { Path_ }`（站点数**恰好等于**基线 1）⇒ 触发不到"已消失"，
   必须喂**空**站点集。

### 104.7 可达机制 M20 ＋ 两处**清单欠账**

- 新增 **M20**（`comm.issue.`，下界 **9**，取数**复用**触发面）；机制条数 **19 → 20**。
- 🩸 **顺带发现并补了两处清单欠账**：
  ① 阶段 103 的 **M19 只进了下界表、没进类注释的「机制清单」**（清单停在 M18）；
  ② 同一处清单里 **M7 的 `<item>` 起始行**不知何时丢了，留下一个**孤立的 `</item>`**。
  两处都在 `LocalizationKeyReachabilityGuardTests` 的类注释里 ⇒ 一并补回。
  ⚠ 「清单」本身没有判据盯着（`MechanismFaces_AreNonEmpty` 只看面非空）⇒ 这类欠账
  **只能靠下一次改动顺手核对**；本条如实登记为**已知的判据盲区**。✅ **阶段 106 已闭环**：新增 `Mechanisms_AreDeclaredInAllThreePlaces`（清单 / 下界表 / 取数**三条来源集合双向相等** ＋ 空集守卫 ＋ id 形状自检 ＋ 重复条目单列，**负控 4/4**）—— 见登记 **#63** / `docs/2026-09-26-mechanism-declaration-guard.md`。

### 104.8 读数

| 量 | 前 | 后 | 说明 |
|---|---|---|---|
| **#61x 面**（`IssueMessagePassthroughFaceTests`） | 1 站 / 1 文件 | **0 站 / 0 文件** | **收官** ⇒ 升格零容忍 |
| **直通面**（`ExceptionMessagePassthroughFaceTests`） | 6 组合 / 6 站点 | **6 组合 / 6 站点** | **不动** —— 本刀收的是**另一条面**（#61x），不是它 |
| 可达机制条数 | 19 | **20** | 新增 **M20** |
| M20-通讯校验码 | —（不存在） | **9** | ＝ 码数 |
| M3-插件族派生 | 26 | **35** | ＋9（`comm-issue` 直接进 M3 的 `DerivedKeys`） |
| M1 / M6 / M7 面 | 707 / 368 / 12 | **不动** | 本族无 `Lang` 常量 |
| 两本字典键数 | 2285 | **2294**（+9） | 键集一致 |
| `UnusedBaseline` | 1108 | **1108（不动）** | ⚠ 本族**不加 `Lang` 常量** ⇒ 不进本集 |
| `App.UiTests`（大队） | 575 | **579** | ＋4 ＝ 3 条判据 ＋ 1 条负控 |

⚠ 本节的读数取自 `.workbuddy/tmp/s104-readings.log`（`--logger "console;verbosity=detailed"`
的读数跑，含 `JudgeReadings` 输出）与 `s104-gate1.log`（大队计数）。
`App.UiTests` 579 来自门禁日志；`575` 来自 `s103-gate1.log`。

### 104.9 门禁

| 时刻（birth / mtime） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 14:58:19 | — | **内容批**（含判据）`a8ac78f` | 9 files，**＋434 / −36** |
| **14:58:22 / 14:59:01** | `s104-gate1.log` | 内容批门禁（7 步） | **7 步全绿**、0 警告 0 错误；`Core.Tests` **2121** / `App.UiTests` **579** ＋ 2 |
| 15:01:25 | — | **文档批**（s103 ＋ s104 **合批**）`320c28d` | 3 files，**＋432 / −3** |
| **15:01:29 / 15:02:35** | `s104-gate2.log` | 文档批门禁（7 步） | **7 步全绿**、0 警告 0 错误；`Core.Tests` **2121** / `App.UiTests` **579** ＋ 2；路径卫生 **667** |
| 15:09:23 | — | **记忆批**（同一次合批）`99f6fde` | 2 files，**＋179**；`MEMORY.md` 7943 → **7961** 码元（快照 26q） |
| **15:09:26 / 15:10:41** | `s104-gate4.log` | 记忆批门禁（7 步） | **7 步全绿**（尾行「🎉 门禁全部通过（7 步）」）；2121 / **579** ＋ 2；⚠ 尾部 `GATE_EXIT` 行**没写进去**（收尾那半句在 fish 里 `$?` 直接报错）⇒ 见下一行 |
| **15:10:53 / 15:11:59** | `s104-gate4b.log` | 同上、**另开日志复跑** | `GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；2121 / 579 ＋ 2；路径卫生 **667** |

⚠ 提交 14:58:19 → 日志 birth **14:58:22**，**晚 3 秒**（与 §102 的 `gate2` 那种"同一秒"不同
—— 同一秒时要另开日志复跑）。

⚠ **本表由回填批补到 `gate4b`**（文档批 / 记忆批 ＋ 两个 `gate4*`）。
🩸 按**自指边界**：回填批自己也要过门禁 ⇒ 它的读数（`s104-gate5.log`）记在**当日日志与收口清单**、
**不回写本节** —— 否则回填批每写一行，都要再为「记录上一批」新开一批。

🩸 **提交信息里的一处数字错（如实登记、不回改）**：记忆批的提交信息写「40 条刀口」，
实测是 **41**（基线 11 ＋ 补充 30）。**刻意不 `--amend`** —— amend 会刷新 committer date，
而 `gate4`（15:09:26）/ `gate4b`（15:10:53）两跑的 birth 都**早于**它 ⇒ 一旦 amend，
这两条**时序证词当场作废**（正是「引用绿之前先核时序」要防的那种）。

### 104.10 诚实边界与未闭环

1. **不是改名式假闭环**：判「算不算收口」的标准是**产出方有没有码**，而不是"面有没有变小"。
   同一批里通讯族的 9 条码第一次有了「码 → 字典键」的对应，且那对应由守卫**双向**夹住。
2. 本族**没有**新增 `Lang` 常量（键靠 `PluginTextKeys.CommIssueKey` 推导）⇒
   `UnusedBaseline` **1108 不动**；也**没有**新增 `ExternalText.*KeyPrefix` 常量 ⇒
   前缀清单下界仍 **15**（插件族的键推导住在 `PluginTextKeys`，覆盖由
   `PluginTextLocalizationGuardTests` 担保）。
3. **通讯族的另一站没动**：`comm.test.failed` 走同一个 `catch (Exception ex)`，但它的产出方
   `ValidateConnection` **返回** `ValidationResult`、**零 `throw`** ⇒ 属「诚实边界」
   （阶段 102 已判）⇒ 换出口只落回落分支、用户所见一字不变。
4. **家族**：`#61（本体，2 站 ＝ `comm.test.failed` / `login.failed`）` / `#61k` / `#61p` /
   `#61r` / `#61s`；**`#61x` 已收口**。
