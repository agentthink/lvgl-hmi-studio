# 覆盖率工具链（先量基线，不当门槛）

登记表 **#33**、`docs/project-status.md` **阶段 36**。提交 `1dbadfd`（工具链）、`098558a`（口径修正 + 负控探针）。

> 时间戳说明：本次工作跨零点 —— 系统时钟在跑覆盖率时已到 `2026-09-20 00:03`，故本文与提交按 09-20 记。

## 1. 为什么**不**做成门槛

覆盖率是「看哪里没测」的**可见性工具**，不是判据。当门禁有两个坏处：

| 坏处 | 具体表现 |
|---|---|
| 新增代码天然拉低总量 | 阈值要么天天红、要么被迫定得毫无意义 |
| 逼出「为覆盖率而写」的测试 | 断言存在但不判任何东西，比没有测试更贵（要维护，还不报错） |

所以 `scripts/coverage.sh` 只负责**把事实量出来**并把基线登记进 `docs/project-status.md`：跑测试 → 拿到 cobertura → 汇总。测试没全过时**不给读数**（`rc != 0` 直接退出，避免把半截数据当基线）。

## 2. 为什么**必须**取并集

每个测试项目各产一份 `coverage.cobertura.xml`。同一份产品代码会被多个测试项目覆盖到（`Core` 被 `Core.Tests` 与 `Simulator.Tests` 都测），**只看单份报告会系统性低估**。`scripts/coverage-summary.py` 按「文件 + 行号」取并集（`hits` 取各报告最大值）。

实测口径差：并集 **20349** 行，而各报告自称覆盖行数之和是 **28774** —— 并集只会更少（同一行被多份报告重复算），这个不等式本身就是一道判据（见 §4）。

## 3. 落地

| 文件 | 作用 |
|---|---|
| `scripts/coverage.sh` | `dotnet test --collect:"XPlat Code Coverage"` 跑全量后汇总；按时间戳隔离每轮产物；`TMPDIR` 指到仓库外大分区 |
| `scripts/coverage-summary.py` | 并集汇总 + 按程序集 / 最低覆盖率文件排行 + 三道自检 |
| `tools/probe-coverage-summary.sh` | 三道自检的**正控 2 + 负控 3**，可复跑 |
| `Directory.Packages.props` | `coverlet.collector 10.0.1`，`PrivateAssets=all` 只声明一次 |
| 8 个测试项目 csproj | 各一行 `<PackageReference Include="coverlet.collector" />` |

三个非显然的约束：

- **`TMPDIR` 必须指到仓库外的大分区**。coverlet 收尾时**先写 `TMPDIR`**、再 attach 到 `--results-directory`；本机 `/tmp` 是 **10 MB tmpfs**，单份报告最大 **9.5 MB**，多项目并行必炸（实测 `Failed to create '/tmp/<guid>/coverage.cobertura.xml' because the disk was full`）。必须仓库外 —— `TestTempDir.NewIsolatedDir` 会拒收仓库内路径。
- **`obj/`·`bin/` 下的生成产物不算产品代码**。实测 `src/HmiEditor.App/obj/Release/net10.0/System.Text.RegularExpressions.Generator/…/RegexGenerator.g.cs` **425 行 / 0% 覆盖** —— 它不是「该测却没测」的行，算进分母会同时**稀释读数**、并让它**顶在「最低覆盖率文件」排行第一名**（纯噪音）。排除量**必须打印出来**，否则「读数变小了」会被误读成「覆盖率掉了」。
- **新程序集自动纳入**：门禁第 [4] 步是全量 `dotnet test HmiEditor.slnx` ⇒ 加测试项目**不必改门禁脚本**；覆盖率脚本同理（`rglob` 收所有报告）。

## 4. 判据与负控

三道自检的动机只有一条：**读数看起来正常但其实是错的**。本项目真踩过 —— `<line>` 的属性名写成 `hit`（实为 **`hits`**），既不抛异常也不报错，只是让**每一行**都算成未覆盖，读数变成「全 0%」而看起来完全正常。

| # | 自检 | 挡的是什么 |
|---|---|---|
| 1 | `<line>` 缺 `hits` 属性 → 直接 `SystemExit` | 解析口径与 cobertura 结构脱节（不给静默降级的机会） |
| 2 | 每份报告：根节点自称 `lines-covered` vs 我们按同一份 XML 重数 | 「它说覆盖了 1244 行，我们数出 0 行」——**这一定不是「代码没被覆盖」** |
| 3 | 汇总级：并集 ≤ 各报告自称之和；并集非空时自称之和不得为 0 | 合并口径错（并集不可能大于各报告之和） |

**探针**（`tools/probe-coverage-summary.sh`）：5 个 fixture、逐条断言退出码与输出片段。

| fixture | 期望 | 实测 |
|---|---|---|
| 正控1 正常汇总（自称 2 行 / 实际 2 行） | `rc=0` 且输出含 `\| 覆盖行 / 总行 \| 2 / 3 \|` | PASS |
| 负控1 自称 3 行但 `hits` 全 0 | `rc=1` 且含「自称覆盖 3 行，我们按同一份 XML 数出 0 行」 | PASS |
| 负控2 `<line>` 缺 `hits` 属性 | `rc=1` 且含「缺少 hits 属性」 | PASS |
| 负控3 数出的比自称的还多 | `rc=1` 且含「我们数出 2 行」 | PASS |
| 正控2 `obj/` 下的产物被排除且**被报告** | `rc=0` 且含「已排除」 | PASS |

### 负控的负控

把 `line.get("hits")` 注入成 `line.get("hit")`（断言**恰好命中一处**再改），探针 **4 失败 / 退出码 1**；还原后 `sha256` 与备份**逐字节一致**、复跑回到 5/5。

一个诚实的观察：注入后**只有负控2 仍 PASS** —— 因为注入让所有路径都抛那句话，负控2 的期望恰好被满足。**这说明单靠负控不能证明代码是好的，正控才在守这件事**（「所有用例都报同一个错」在只看负控时长得像成功）。

### 探针必须可复跑（这条是被假绿教出来的）

第一版探针把 fixture 建在 `/tmp` 里、分两次 Bash 调用跑。本沙箱 **`/tmp` 跨 Bash 调用不共享** ⇒ 第二次运行时 fixture 已蒸发，**5 条全部在「找不到报告」上退出 1** —— 看起来像「判据都命中了」，实际是假绿。改成 `mktemp -d` + `trap ... EXIT`（同一进程内建、用、清）后才有意义。

## 5. 基线读数

命令 `bash scripts/coverage.sh --no-build`，日志 `.workbuddy/tmp/coverage-run.log`（时刻 `2026-09-20T00:03:57+08:00`，`rc=0`，耗时 **36 s**，几乎不增总耗时）。

**20349 / 46162 = 44.1%**（8 份报告、408 个产品文件；另有 1 个生成产物文件 / 425 行被排除）。

| 程序集 | 覆盖 / 总行 | 行覆盖率 |
|---|---|---|
| `HmiEditor.CodeGen` | 2837 / 2989 | **94.9%** |
| `HmiEditor.Core` | 7203 / 7832 | **92.0%** |
| `Hmi.Hardware.Builtin` | 102 / 127 | 80.3% |
| `Hmi.Hardware.Contracts` | 52 / 68 | 76.5% |
| `Hmi.Hardware.Loader` | 152 / 206 | 73.8% |
| `HmiEditor.Device` | 1110 / 1559 | 71.2% |
| `HmiEditor.Simulator` | 104 / 214 | 48.6% |
| `HmiEditor.App` | 8463 / 29462 | 28.7% |
| `Hmi.Communication.Contracts` | 11 / 49 | 22.4% |
| `HmiEditor.AI` | 235 / 2317 | 10.1% |
| `Hmi.Communication.Builtin` | 80 / 1239 | 6.5% |
| `HmiEditor.Scripting` | 0 / 82 | 0.0% |
| `Hmi.Communication.Loader` | 0 / 18 | 0.0% |

一眼能看出来的（**这些不是缺陷，是「没测到」的事实**）：

- 生成链主干（`Core` + `CodeGen`）覆盖很高 —— 与「生成产物逐字节可复现」的既有纪律一致。
- `HmiEditor.App` 29462 行是**最大的未覆盖面**，但它是 Avalonia 视图层，大量行在 XAML 事件处理器里，UI 测试（255 条）打不到。
- `Hmi.Communication.Builtin` 6.5%：`ModbusRtuRuntimeDriver` / `ModbusTcpRuntimeDriver` / 六个 OPC UA 文件**整文件 0%**。
- `HmiEditor.AI` 10.1%：`AgentEngine` / `LayoutAnalyzer` / `AiOutputValidator` / `ClaudeProvider` 整文件 0%（AI 测试 315 条集中在 schema/enum 与模板侧）。

**读数有噪声，别拿它当最小刻度**：同一提交两次跑差 **5 行**（20354 vs 20349，≈0.01%）—— 涉及并行/计时路径的行偶尔命中不同。要判定「改动有没有影响覆盖率」，**5 行级别的差异没有信息量**。

## 6. 仍未闭环

| # | 项 | 说明 |
|---|---|---|
| ① | **阈值门禁** | 定阈值是产品决策（且见 §1 的理由，可能永远不该定） |
| ② | **分支覆盖率未看** | coverlet 报告里有 `branch-rate`，本脚本只汇总行 |
| ③ | `HmiEditor.App` 的 71% 未覆盖未分级 | 视图层里哪些是「真该测」、哪些是「DI 装配样板」，未逐类判定 |
| ④ | ~~整文件 0% 的清单未治理~~ | ✅ **已查证（2026-09-20）—— 见 §7 与 `docs/2026-09-20-dead-type-and-zero-coverage-triage.md`** |
| ⑤ | 覆盖率产物在 `.workbuddy/tmp/` | 每轮一个新时间戳目录，**不自动清理**；本地磁盘会慢慢涨（`.workbuddy/` 已被 gitignore） |

## 7. 0% 清单的排查结果（2026-09-20）

完整报告：`docs/2026-09-20-dead-type-and-zero-coverage-triage.md`（登记表 #33 ④、阶段 37）。三句话：

1. **94 个文件 / 16755 行整文件 0%**，分三个桶：无人引用 **1** 个 / 75 行、活但零测试 **45** / 7753、
   仅被"读源码文本"的守卫测试提到 **48** / 8927。
2. **4 个真死类型**（`HmiProperty`、`ResourceLibraryVersionInfo`、`LuaScriptValidator`、`LuaSnippets`）
   —— 后两个说明 **`HmiEditor.Scripting` 整个程序集 100% 是死代码**，而 App 的 csproj 仍引用着它。
   已由 **`DeadTypeGuardTests`**（相等判定 + 3 条元判据 + 2 组负控）锁住；**删不删是产品决策**。
3. **`MainWindow` 从未被任何测试构造过**（2370 + 368 行，`grep -rn "new MainWindow(" tests/` 零命中）
   —— App 那 71% 未覆盖里的大头就是这一层，**没有任何自动判据会红**。

排查工具 `tools/triage-zero-coverage.py` 自带正控；它自己踩过的 5 个误报源（文件名≠类型名、
分部类、扩展方法容器、嵌在字面量里的别语言代码、插值孔里的引用）全部记在那份报告 §2 ——
**其中"改用保留字面量的文本计数"这个方向会让守卫被自己的基线喂活（恒绿），已明确放弃。**
