# 沙箱的「新路径」开销：全局串行，约 60 ms/文件

日期：2026-09-17
状态：已完成（阻断项 11 的根因；门禁第 3 步已按此调整）

本文件记录**一条环境事实**，以及它如何把一条既有 flaky（阻断项 11）变成必然。
结论全部可复跑：`bash tools/probe-fs-newpath-cost.sh`（纯 shell + git，**不碰 .NET**）。

## 1. 症状

`scripts/ci-gate.sh` 第 4 步（当时还是单次 `dotnet test HmiEditor.slnx`）三跑三红，
每次都是同一条、且只有这一条：

```
失败 GitWorkflowServiceTests.InitializeRepository_SecondCall_Idempotent [43 s]
   Assert.True() Failure  Expected: True  Actual: False
   at GitWorkflowServiceTests.cs:line 226      ← 第一次调用
```

用例本体只有三次调用（两次 `InitializeRepository` + 一次 `GetState`），
`finally` 里清目录。**它返回 false，而不是断言等超时** —— 但整条用例花了 43 s。
门禁里的耗散：**5 s ~ 6m42s**。

`InitializeRepository` 的三条 `return false` 路径：

| # | 位置 | 触发条件 |
|---|---|---|
| 1 | `!IsGitAvailable()` | `git --version` 超时或进程起不来 |
| 2 | `git init` 退出码 ≠ 0 | 命令本身失败 |
| 3 | 末行 `return IsRepository(dir)` | 收尾确认 `rev-parse` 失败/超时 |

一次调用共 fork **5 次** git（`--version`、`rev-parse`、`init`、`symbolic-ref`、`rev-parse`）。
**43 s ≈ 三次 15 s 窗口被撞穿**（`GitProcessWaitMs = 15000`）—— 这是唯一能解释该量级的算法。

## 2. 归因：不是产品，不是 git，不是文件系统

用**纯 bash** 复刻（`.refactor-tmp/probe_git_init_freshdir.sh` 的等价物）：

| 场景 | `git init` 耗时 |
|---|---|
| 全新目录（`/tmp`，tmpfs） | 1172–1250 ms |
| 全新目录（`/home`，**btrfs**） | 1157–1216 ms |
| 预建空目录（父目录固定） | 1171–1208 ms |
| **固定目录**（反复删 `.git` 重建） | **57–59 ms**（仅首轮 1214） |
| 全新目录 + `touch` 10 个空文件 | 551–596 ms |

同一目录换三种 spawn 方式，全都一样慢：

| 方式 | 耗时 |
|---|---|
| `ProcessStartInfo.WorkingDirectory` | p50 1228 ms |
| `git -C <新目录> init`（不设 WD） | p50 1217 ms |
| `bash -c "git init"`（WD = 新目录） | p50 1207 ms |

⇒ **与 .NET 无关、与工作目录怎么传无关、与谁 spawn 无关、与文件系统类型无关。**

### 2.1 单位是「首次出现的**文件**路径」，目录几乎免费

| 项 | 实测（`tools/probe-fs-newpath-cost.sh`） |
|---|---|
| 新目录（只 `mkdir`） | **1 ms** —— 目录创建**不收费** |
| 全新目录 + N 个新文件（N=1/2/5/10/20/40） | 62 / 125 / 321 / 646 / **1288** / **2538** ms ⇒ **线性 ≈ 62 ms/文件** |
| **旧目录** + 20 个从没出现过的文件名 | 1238 ms ⇒ 与「目录新旧」无关 |
| 同一批 20 个名字在**同一路径**上反复重建 | 首轮 1163 ms，之后 **2 ms** |
| 同一批 20 个名字换到**新父目录** | 1119 / 1135 / 1151 ms（**每轮都慢**）⇒ 缓存按**完整路径**，不是按文件名 |
| 深层新目录（4 级）末尾建 1 个文件 | 54–57 ms ⇒ 深度无关 |

规则：**某个文件路径第一次被创建时收约 60 ms，此后同一路径免费。** 目录创建几乎免费。

> **一次自纠（量测污染）**：首版把「新目录」记为 93–107 ms，属于**把 python3 进程启动
> （约 90 ms）算进了计时区间** —— 那一版的 `mkdir` 是经 `python3 -c "os.makedirs(...)"` 做的。
> 改用 shell 的 `mkdir` 后是 **1 ms**。分开量「进程启动」与「文件系统操作」是这里的关键；
> 否则会把 90 ms 的常量当成"目录创建贵"，并据此推出错误的修法方向。

`git init` 在全新目录里要造约 20 个文件 ⇒ 恒定 **1.2 s**（= 20 × 60 ms），与实测吻合。

### 2.2 更关键：这个开销是**全局串行**的

```
单进程          ：20 个新文件 1100 / 1110 / 1139 / 1140 / 1157 ms
8 进程并行（×3）：9639 / 9664 / 9560 ms      ← ≈ 单进程 × 8
```

`nproc=20`、loadavg 1.6 —— **20 核全程闲置**。吞吐恒为约 17 个新路径/秒，
**与并发度无关**。

在 8 路「新路径写入」负载下直接量外部命令（**0 失败**，只是慢）：

| 命令 | 延迟分布 |
|---|---|
| `git --version` ×25 | 常态 1–3 ms；尾部 57 / 1032 / 1130 / 1267 / 1336 / 1596 / 2113 / 3668 / 4290 / **4628** ms |
| `git init` ×8 | 2643 / 3527 / 3589 / 4602 / 8218 / 9582 / 9717 / **9941** ms |

⇒ 外部命令**没有坏**，它只是排在那条串行队列后面。

## 3. 与阻断项 11 的因果链

```
xUnit 默认并行度 = 核数（本机 20）
  → 2500+ 个用例同时新建临时目录与新文件
  → 灌满这条全局串行的「新路径」队列
  → 单个 git 子进程 1 ms → 4~10 s（并发度越高越靠后）
  → 撞穿 GitProcessWaitMs(15 s)  → InitializeRepository 返回 false
  → 用例红
```

这也解释了取证表里的几条「反直觉」观测：

| 观测 | 解释 |
|---|---|
| 定向 `--filter GitWorkflowServiceTests` 两次都 8/8 绿、7 s | 队列为空 |
| 整个 Core 程序集单跑有约 1/3 概率红 | 单程序集内部并行度就是 20 |
| 门禁里 7 宿主并行，该用例最惨到 6m42s | 队列最长 |
| `git` 直压 200 轮 0 失败、`/tmp` 峰值仅 116 KB | 队列不是磁盘、不是 git |
| 把探测窗口 3 s → 15 s 没用 | 撞穿的是 15 s 那一档（队列长到几十秒） |

### 3.1 两条**已验证不是解药**的方向

1. **调窗口** —— 15 s 照样撞穿；且撞穿时长随队列长度无上限。
2. **调 xUnit 并行度** —— 20 / 8 / 4 / 2 线程，墙钟 **84 / 83 / 90 / 88 s**（同量级）：

   | 档 | maxParallelThreads | 墙钟 | 该用例 |
   |---|---|---|---|
   | 1 | 默认（20） | 84 s | **红 [46 s]** |
   | 2 | 8 | 83 s | 绿 |
   | 3 | 4 | 90 s | 绿 |
   | 4 | 2 | 88 s | 绿 |
   | 5 | 默认（20） | 88 s | 绿 |

   墙钟不随并行度变化，正说明**吞吐由那条串行资源决定**；而总工作量固定时，
   并发度并不改变单个任务的期望排队时间 ⇒ 它**不是**杠杆。
   （顺带一个副作用认知：本机跑测试套件时 CPU 基本用不上。）

### 3.2 同族的第二个出口：排 ThreadPool 的定时器 + 固定余量

同一次门禁里还红了另一条：

```
失败 HmiEditor.Device.Tests.CommandFrameCollectorTests
     .FirstByteArrivingAfterSilentPeriod_IsNotMistakenForEmptyOutput [2 s]
   Expected: "ok:SLOW\n"  Actual: ""
```

它的延迟生产者是：

```csharp
_ = Task.Run(async () => { await Task.Delay(delayMs).ConfigureAwait(false); collector.Append(text); });
```

`Task.Delay` 的续体是 **ThreadPool 工作项**。套件里那些「往全新路径写文件」的阻塞 IO 会长时间
占住池线程（每个新文件 60 ms，且全局串行 ⇒ 一个正在写盘的池线程可能被占住数秒且会叠加）
⇒ 续体被推迟到秒级 ⇒ 越过该用例的 800 ms 首字节窗口 ⇒ 设备「起步慢」被判成「没输出」。

这与阻断项 11 **是同一个机制的两个出口**：

| 出口 | 队列 | 判据被撞穿的方式 |
|---|---|---|
| A（阻断项 11） | 那条全局串行的「新路径」资源 | git 子进程排到 4~10 s ⇒ 撞 `GitProcessWaitMs`(15 s) |
| B（本条） | ThreadPool（被新路径 IO 占住） | 定时器续体推迟到秒级 ⇒ 撞写死的 800 ms 余量 |

两者都表现为「命令/设备偶发不工作」，都不是产品缺陷，解药也都**不是**把窗口调大。
项目记忆里本就写下过这条规则 —— **`Task.Delay` 续体同样排 ThreadPool ⇒ 用专用 `Thread`；
扫 flaky 按写法特征**。本条正是「按写法特征扫」扫出来的第二例：生产者已改为专用线程，
并修掉该文件里「不依赖主机负载」这句与实现相反的自我描述。

> **改完一种病要再跑一轮 —— 同一个文件里有两种病。** 修掉 ThreadPool 那条之后，
> 同文件里的 `SilenceIsMeasuredFromLastDataChunk_NotFromFirst` 仍翻了一次：
> `Expected "line1\nline2\nline3\n"` / `Actual "line1\nline2\n"`（第三段被截）。
> 它的病**不是** ThreadPool，而是**给每段各起一个线程** ⇒ 段间的实际间隔 = 两次线程启动
> 抖动的**叠加**，而名义间隔 70 ms 对静默期 100 ms 只留 **30 ms** 余量。
> 修法：三段改由**同一个专用线程**按时间轴依次投递（间隔只受单次 `Sleep` 影响），
> 并把尺度改成 **0.6 × 静默期**（间隔 360 ms vs 静默 600 ms ⇒ 余量 240 ms，判别力不变）。
> ⇒ 一条"临时红"里可能同时藏着**队列**和**绝对余量**两种病，只改一种会以为没修好。

> 同族但**本轮未复现**、未改的一处：`NoData_ReturnsEmptyAfterFirstByteWindow_NotAfterCommandTimeout`
> 用 `Assert.InRange(sw.ElapsedMilliseconds, 150, 2000)` —— 上界 2000 ms 相对 200 ms 的窗口是
> 「靠绝对余量」。它要断言的真实语义是「不要等到整命令超时」，而不是「≤2 s」。
> 按「不改没有复现证据的判据」暂不动，登记在此。

## 4. 落地的修法

**让这 8 个用例在队列为空时跑** —— `scripts/ci-gate.sh` 第 3 步拆两段：

```bash
# 3a 大队（排除 spawn git 的用例）
dotnet test HmiEditor.slnx ... --filter 'FullyQualifiedName!~GitWorkflowServiceTests'
# 3b git 用例单独跑（队列为空）
dotnet test tests/HmiEditor.Core.Tests/HmiEditor.Core.Tests.csproj ... \
    --filter 'FullyQualifiedName~GitWorkflowServiceTests'
```

- **不放宽任何判据**：用例照跑、断言照旧，只是不再与大队争那条串行队列。
- 全仓库只有 `GitWorkflowServiceTests` 一个类 spawn git（已用全仓 grep 核对：
  `TestTempDir` 自身不跑 git，`GitProcessWaitWindowTests` 只做源码扫描）。
- 过滤器写法已单独验证：`!~` 在「0 匹配」的项目上不会报错（`Hmi.Hardware.Tests` 22/22 绿）；
  排除后 Core 恰为 **1892 = 1900 − 8**。

## 5. 验证

| 场景 | 结果 |
|---|---|
| `dotnet test` 只跑 git 那 8 条 | **8/8 绿 / 7 s** |
| `dotnet test` 排除 git 的 Core | **1892 绿 / 1m14s**（= 1900 − 8） |
| `!~` 过滤器在无匹配的小项目上 | **22/22 绿**，退出码 0 |
| `bash scripts/ci-gate.sh`（拆分后全跑） | 见验证记录 |

## 6. 教训

1. **「外部命令偶发超时」不能默认归因到宿主或 .NET。** 本轮先猜过「.NET 测试宿主的进程创建/等待被拖慢」，
   方向对了（并发），机制错了（不是 .NET 的锅）—— 直到**用纯 bash 复刻**才定性。
   **先直压工具本身、再用纯 shell 复刻操作序列**，是这一族问题最快的分界线。
2. **方差极大（5 s ~ 6m42s）本身就是线索。** 稳定的 1.2 s 是固定成本；可变的 5 s~6min 是**排队**。
   把「固定成本」和「排队」分开量，才看得见那条串行资源。
3. **测量要打到"单位"上。** 「新目录慢」和「新路径慢」看起来一样，`mkdir`-only 与
   「旧目录 + 新文件名」两档才把它分开 —— 而单位决定了修法（前者要改目录策略，后者只能避开队列）。
4. **别在窗口上花时间：先问「队列为什么长」。** 窗口是判据，队列是成因；
   把判据放宽只是让成因继续恶化而没人知道。
5. **修完一条要回头按「写法特征」扫同族。** 阻断项 11 与 `CommandFrameCollectorTests`
   是同一机制的两个出口，前者修完才在门禁里撞见后者 —— 扫法不是按测试名，而是按
   「固定延迟 + 固定余量」「`Task.Delay` 续体」「`WaitForExit(ms)`」这些写法特征。
6. **测试类的自我描述也要被验证。** `CommandFrameCollectorTests` 的手写注释写着
   「确定性……不依赖主机负载」，实现却是排 ThreadPool 的 `Task.Delay` —— 与阶段 10 收口的
   「A′：注释与实现相反」是同一类。**注释里的形容词（确定性/幂等/线程安全）必须能指到一段
   兑现它的实现**，否则它会在下一次排查里把人带错方向。
