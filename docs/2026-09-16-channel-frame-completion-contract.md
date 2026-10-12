# 通道收帧判据：一条命令的响应从哪开始、到哪结束

> **结论**：`TcpDeviceChannel` / `SerialDeviceChannel` 原来各自持有一份**逐字相同**的收帧代码，
> 判据是"从**写入时刻**起算静默 `SilentTimeoutMs`"。"写入时刻"不等于"设备吐出首字节的时刻"，
> 于是产生两个失效模式：**① 假空响应**（设备只是起步慢，被判成"本命令无输出"，返回空串）、
> **② 串帧**（那条迟到的回包落进下一条命令的窗口）。修法是把判据收成**唯一实现**
> `CommandFrameCollector`，并把"帧内静默"与"首字节宽限"拆成两条互斥判据。

## 1. 症状

| 现场 | 表现 |
|---|---|
| `DeviceChannelTests.ConcurrentCommands_AreSerialized_NoCrossTalk`（2026-09-16 门禁首跑红） | `Expected: ["ok:AAA", "ok:BBB"]`，`Actual: ["", "ok:AAA\nok:BBB"]` —— 一条拿到空串、另一条拿到两行。单独复跑 3/3 绿、门禁第二次跑也绿 ⇒ 与负载相关 |
| 真实设备 | 设备起步稍慢（或主机满载）⇒ 命令返回空串；下一条命令拿到上一条的响应 ⇒ **响应张冠李戴**，上层按错内容解析（文件大小校验、base64 探测都会误判） |

两条症状是**同一个根因的两个后果**，所以它们总是一起出现。

## 2. 根因：判据有三处错

原实现（两个通道各自一份）：

```csharp
lock (_sync) { _rxBuffer.Clear(); }
await _stream.WriteAsync(payload, ct);          // 写入时刻
var idleSince = Environment.TickCount64;        // ← 静默从**写入时刻**起算
while (true)
{
    lock (_sync)
    {
        if (_dataIncoming) idleSince = Environment.TickCount64;              // 有数据就刷新
        else if (Environment.TickCount64 - idleSince >= SilentTimeoutMs)     // 静默到点 ⇒ 结束
        { output = _rxBuffer.ToString(); break; }
    }
    await Task.Delay(50, ct);
}
```

读循环侧：

```csharp
lock (_sync) { _rxBuffer.Append(...); }
_dataIncoming = true;
if (stream.DataAvailable == false)
{
    await Task.Delay(SilentTimeoutMs, ct);       // ← 生产者**延迟**一个静默期
    if (!ct.IsCancellationRequested && stream.DataAvailable == false)
        _dataIncoming = false;                   // ← 才把"有数据"置回 false
}
```

| # | 错 | 后果 |
|---|---|---|
| 1 | **起算点**：`idleSince` 从写入时刻起算，而不是从"最后一段数据到达"起算 | 设备起步 = shell 调度 + 进程抢占 + 主机负载，**天然慢于帧内行间隙**。起步 >300ms 就被判"无输出" |
| 2 | **窗口混用**：`SilentTimeoutMs` 一个值同时充当"帧内数据间隙容忍"与"响应起步宽限" | 想调宽起步容忍就会连带放松帧内间隙（反之亦然）。实测需要的是两个不同的量级（300ms vs 1s） |
| 3 | **状态跨帧共享**：`_dataIncoming` 是通道级字段，且由生产者**延迟**翻转 | 消费侧的起算与生产者的翻转不是同一个时刻；上一帧的残留状态会影响下一帧的判定 |

失效链：首字节晚于 `SilentTimeoutMs` → 消费侧判"静默已到"→ **返回空串**（错 1）；
那条迟到的回包才到达 → 落进**当前缓冲**（此时下一条命令可能已 clear 过、正在等）→ **下一条拿到两行**（错 3）。

### 2.1 溯源：这是 2026-08-20 那次修复的未完成部分

`docs/code-error-audit-2026-08-20.md` §2"串口/TCP 通道：无输出命令空等满超时"原文：

> **⚠️ 修复过程中的关键教训**：首版直接把条件改成 `!_dataIncoming` 会引入**回显延迟竞态**
> （命令刚发出、回显未达时 `_dataIncoming` 仍为 false → 立即误判空输出返回）。
> 最终采用 `Environment.TickCount64` 记录 idle 起始时刻的方案，两者兼顾。
> **验证**：……（**无自动化测试，人工核对逻辑**）。

也就是说：**这个竞态从未被解决**，只是把"立即误判"改成了"300ms 后误判"，并把窗口从 0ms 挪到
`SilentTimeoutMs`。当时没有自动化测试，所以挪动窗口等于把缺陷藏了起来 —— 直到 2026-09-13 的审查
（`docs/2026-09-13-project-review-and-optimization.md` §P1-5）以"完成判定只靠 300ms 静默（慢输出会被
截断）"记了一笔，2026-09-16 才由门禁首跑把它顶出来。

**教训**：修竞态时"让窗口变大"不等于"消除了竞态"；判据必须建立在一个**语义上正确的时刻**上
（这里是"最后一段数据的到达时刻"），而不是"把错误的时刻加上一个容差"。

## 3. 修法

### 3.1 收成唯一实现：`CommandFrameCollector`

职责边界（这是关键）：

| 角色 | 做什么 | **不**做什么 |
|---|---|---|
| 生产者（各通道读循环） | `Append(text)`：把字节追加进当前帧、打时间戳 | 不判定帧结束、不翻任何"收数据中"标志 |
| 消费者（`SendCommandCoreAsync`） | `SendAsync(writeCommand, silentMs, firstByteMs, ct)`：开帧 → 写 → 等本帧结束 | 不自己维护缓冲 |

结束判据（两条互斥）：

1. **收到过数据** ⇒ 从**最后一段数据到达时刻**起算静默 `silentMs` 即结束 —— 生产者每次
   `Append` 都刷新 `LastDataTicks`，所以"帧内多段数据"天然落在同一帧；
2. **一字节未到** ⇒ 从写入时刻起算过 `firstByteMs` 即判"本命令无输出"，返回空串 ——
   保留"设备不回显时不空等满超时"的原设计意图（`SilentTimeoutMs` 初值 300ms 时最多等 1s）。

```csharp
var effectiveFirstByte = Math.Max(firstByteMs, silentMs);   // 下界，见 3.2
Frame frame;
lock (_sync) { frame = _current = new Frame(); }            // 开帧
await writeCommand(ct);
var firstByteDeadline = Environment.TickCount64 + effectiveFirstByte;
while (true)
{
    ct.ThrowIfCancellationRequested();
    lock (_sync)
    {
        if (frame.SawData)
        {
            if (Environment.TickCount64 - frame.LastDataTicks >= silentMs)
                return frame.Buffer.ToString();
        }
        else if (Environment.TickCount64 >= firstByteDeadline)
            return string.Empty;
    }
    await Task.Delay(PollIntervalMs, ct);
}
```

### 3.2 首字节窗口不得小于静默期

```csharp
var effectiveFirstByte = Math.Max(firstByteMs, silentMs);
```

理由：有数据时我们都容忍 `silentMs` 的输出间隙，那么"连第一个字节都还没出来"这个**更慢**的阶段
就更不该被更早判死。否则判据自相矛盾（容忍 300ms 的行间隙，却不容忍 100ms 的起步）。
`FirstByteTimeoutMs` 默认 1000ms —— 比 300ms 的静默期宽，但不至于拖慢"设备真不回显"的场景。

### 3.3 每帧独立

`SawData` / `LastDataTicks` 挂在**帧实例**上，开新帧时换新实例。上一帧的残留状态不会让本帧被误认为
"已经收到数据"，也不会污染本帧的静默起算点。旧实现里这个状态是一个跨帧共享的 `volatile bool`，
还被生产者延迟翻转。

### 3.4 顺带收掉 `SanitizeOutput` 的第二份实现

两个通道原来各有一份逐字相同的输出清洗（去 CR / 尾部控制字符 / busybox 提示符行），
收进同文件的 `CommandOutput.Sanitize`。它不涉及收帧判据，但属于同一类"改一处漏一处"的重复。

## 4. 测试与守卫

| 测试 | 判据 | 备注 |
|---|---|---|
| `CommandFrameCollectorTests`（6） | 帧内数据在静默后返回 / **首字节晚于静默期不误判为空** / 静默从最后一段起算（三段数据累计超静默期）/ 无数据在首字节窗口后返回空而非空等满超时 / 首字节窗口下界 = 静默期 / 取消传播 | 纯确定性，不碰网络 |
| `DeviceChannelTests.SlowFirstByte_WithinGraceWindow_…` | 服务器延迟回包 600ms（> 默认静默期 300ms）仍拿到 `ok:AAA` | **对旧实现必红**（确定性复现，不靠负载去撞） |
| `DeviceChannelTests.NoReply_ReturnsEmptyAfterFirstByteWindow_…` | 完全静默的服务器 ⇒ 返回空串且耗时 < 10s | 固化"不空等满超时" |
| `DeviceChannelTests.ConcurrentCommands_…` | 回包延迟 400ms + 显式断言**不存在串帧形态**（一条同时含两条回包 / 有空串） | 原用例只断言排序后的集合，看不出"两行粘一起" |
| `ChannelFramingContractTests`（4） | ① 两个通道都 `new CommandFrameCollector()` / `_frames.SendAsync(` / `_frames.Append(` / `CommandOutput.Sanitize(`；② **目录级穷举**扫 `Channels/*.cs` 不得再出现 `idleSince` / `_dataIncoming` / `_rxBuffer`；③ 判据自检（把历史写法喂进来必须命中） | 守卫成对：一条证"转发了"，一条证"没有第二份"。只写前者的话把实现复制回来不会红 |

`LineServer` 新增 `replyDelayMs` 档位 —— 把"设备起步慢"从"靠全量并行的负载去撞"变成**可构造输入**。
这是本轮把 flaky 转成确定性回归的关键手段。

## 5. 负向验证（4 组）

| 组 | 注入 | 期望变红 | 结果 |
|---|---|---|---|
| NV-1 | `effectiveFirstByte = silentMs`（首字节窗口退化为静默期 = 历史行为） | `SlowFirstByte_…`、`FirstByteArrivingAfterSilentPeriod_…`（+2 条连带） | 红 4 条，失败块含期望文本 |
| NV-2 | `Append` 里只在**第一段**更新 `LastDataTicks` | `SilenceIsMeasuredFromLastDataChunk_NotFromFirst` | 红 1 条 |
| NV-3 | `effectiveFirstByte = firstByteMs`（下界丢失） | `FirstByteWindow_IsNeverShorterThanSilentPeriod` | 红 1 条 |
| NV-4 | 在 `TcpDeviceChannel` 读循环里插回 `var idleSince = …` | `ChannelsDirectory_HasExactlyOneFramingImplementation` | 红 1 条 |

**NV-2 的构造踩过一次坑**：最初的用例是"两段数据间隔 40ms、静默期 120ms"，
而 `if (!SawData)` 这种注入在**内容上**与正确实现在该参数下**完全等价**
（间隔 < 静默期 ⇒ 两者都在第二段到达后才返回）—— 于是注入后测试仍然绿，负向验证报"期望用例未变红"。
真正能区分的是让**多段数据的累计时长超过静默期**（0 / 60 / 130ms，静默期 100ms）：
错误实现会在第一段 +100ms 处返回，第三段被截断。

> **通用教训**：负向验证红了才说明用例有区分度；**没红要先怀疑"注入与缺陷不等价"**，
> 而不是直接认定守卫有效。

## 6. 验证与证据

| 项 | 结果 |
|---|---|
| `dotnet test tests/HmiEditor.Device.Tests` | **76/76 通过**（新增 12 条） |
| 全量测试 | 7 项目 **2332/0**（基线 2320 + 12） |
| `scripts/ci-gate.sh` | 5 步全过 |
| 负向验证 | 4/4 组命中，且期望文本落在各自失败块内 |

## 7. 遗留

1. **极端迟到（> `FirstByteTimeoutMs`）仍会串帧**：若设备在首字节窗口到点、下一条命令已开始之后
   才吐出上一条的响应，任何"静默期"类判据都无法区分它属于哪条命令。彻底解决需要**协议级手段**
   （给每条命令加哨兵 `echo __HMI_DONE_<n>__` 并按哨兵收帧，或要求设备回序列号）。
   影响面：`DeviceFileTransfer` 的多命令路径（`cat > …` / 分块 `writeLine` / `HMIUPLOAD` / `wc -c < …`）
   都会连带受益，但也都要跟着改。当前判断是"设备行为异常"级别，暂不动。
2. `FirstByteTimeoutMs` 是**独立的**属性（不随 `SilentTimeoutMs` 缩放）。设备起步特别慢时
   （如冷启动后的 busybox），需要调用方同时调大两个值。
