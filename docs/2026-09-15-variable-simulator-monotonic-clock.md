# 变量模拟器：仿真时间轴改走单调时钟（TimeProvider 注入）

> 2026-09-15。承接 `docs/2026-09-14-test-timer-flakiness.md` 里登记的两条 flaky
> （`Simulator_ClockMultiplier_SpeedsUp/DeceleratesSimulatedTime`）。
> 那次只是把它从"定时器回调竞态"里摘出来，**根因（墙上时钟）留着没修**；本轮修根因。

## 1. 归因：仿真时间轴踩了墙上时钟

修前：

```csharp
private DateTime _startTime;
public double ElapsedSeconds => (DateTime.UtcNow - _startTime).TotalSeconds * ClockMultiplier;
// Start(): _startTime = DateTime.UtcNow;
```

`DateTime.UtcNow` 是**墙上时钟**，会被 NTP 校时前后跳动：

| 事件 | 后果 |
|---|---|
| NTP 向前校时 +1h | 仿真时间突然 +3600s → 正弦/斜坡/计数器波形**瞬间跳变** |
| NTP 向后校时 −N | 仿真时间**回卷甚至变负**（`elapsed < 0`）→ 相位倒着走 |

这和本项目固件侧那条契约（**LVGL tick 必须 `CLOCK_MONOTONIC`**，见 MEMORY §1）是**同一根因的第二处现场**：
固件侧当年就因为 `gettimeofday` 随 NTP 跳变出过问题，模拟器侧一直没同步修。

## 2. 同源的第二个缺陷：`Start()` 之前读到垃圾值

`_startTime` 默认是 `DateTime.MinValue`，而 `ElapsedSeconds` **没有任何"是否已启动"的守卫**
⇒ `new VariableSimulator()` 之后、`Start()` 之前读 `ElapsedSeconds`，得到的是

```
(DateTime.UtcNow - DateTime.MinValue).TotalSeconds ≈ 6.4e10 秒
```

即约 2000 年的时间跨度。`GetCurrentValues()` 是 `public` 且内部就读 `ElapsedSeconds`
⇒ 任何"没 Start 就取值"的调用（含测试）拿到的是相位完全随机的波形。

## 3. 做法

### 3.1 注入 `TimeProvider`，时间轴走 `GetTimestamp`

```csharp
private readonly TimeProvider _time;
private long _startTimestamp;
private volatile bool _hasStarted;

public VariableSimulator(TimeProvider? timeProvider = null)
{
    _time = timeProvider ?? TimeProvider.System;   // 参数可选 → 既有调用点零改动
    _tickTimer = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
}

public double ElapsedSeconds => _hasStarted
    ? _time.GetElapsedTime(_startTimestamp).TotalSeconds * ClockMultiplier
    : 0;

public void Start()
{
    _startTimestamp = _time.GetTimestamp();   // 先写基准
    _hasStarted = true;                       // volatile 写 = release：读到 true 的线程必然读到新基准
    ...
}
```

| 选择 | 理由 |
|---|---|
| `GetTimestamp()`/`GetElapsedTime()` 而非 `GetUtcNow()` | `TimeProvider.System` 下前者即 `Stopwatch` 的 **`CLOCK_MONOTONIC`**，NTP 免疫 —— 与固件侧契约一致 |
| `TimeProvider`（BCL，.NET 8+）而非 `ISystemClock`/自造接口 | `net10.0` 直接可用，**不需要引任何包**（本机 restore 受限，这条很关键） |
| 构造函数可选参数 | `SimulatorView` 等既有 9 处 `new VariableSimulator()` 一行都不用改 |
| **不动** `_tickTimer`（`System.Threading.Timer`） | tick 的"真实节拍"与"仿真时间轴"是两件事；`CreateTimer` 会顺带改 tick 语义，本轮不做 |

### 3.2 测试侧：`FakeTimeProvider` —— 单调时间与墙上时间**可分别前进**

`tests/HmiEditor.Simulator.Tests/FakeTimeProvider.cs`（新增）：

```csharp
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;
    private long _timestamp;

    public override DateTimeOffset GetUtcNow() => _utcNow;
    public override long GetTimestamp() => _timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;   // 1 单位 = 1 tick

    public void Advance(TimeSpan d)   { _timestamp += d.Ticks; _utcNow += d; }  // 正常流逝
    public void JumpWallClock(TimeSpan d) => _utcNow += d;                      // NTP 校时：单调轴不动
}
```

**`JumpWallClock` 就是"墙上时钟"这件事的负向验证载体**：只有实现真去读 `GetUtcNow()` 时它才有影响。

测试写法从"睡 120ms + 宽区间"改成"推时针 + 精确断言"：

| 测试 | 修前 | 修后 |
|---|---|---|
| `…ClockMultiplier_SpeedsUp` | `Thread.Sleep(120)` → `Assert.InRange(elapsed, 0.3, 0.7)` | `time.Advance(0.12s)` → `Assert.Equal(0.48, …, 9)` |
| `…ClockMultiplier_Decelerates` | `Thread.Sleep(120)` → `Assert.InRange(0.02, 0.11)` | `time.Advance(0.12s)` → `Assert.Equal(0.06, …, 9)` |
| `…LastTickElapsedMs_IsMeasured` | `Thread.Sleep(120)` + `Assert.True(x >= 0)`（**恒真**） | `SpinWait.SpinUntil(TickCount >= 1)` + `IsFinite` 断言 |

> `sim.Start(); sim.Stop();` 之后再推时针：既让 `Start()` 落下基准，又把真实 `Timer` 停掉，
> 于是**时间轴只剩假时钟一个驱动源**，断言是确定性的。

## 4. 证据

### 4.1 新增守卫（3 项，Simulator 41 → 44）

| 守卫 | 断什么 | 证明的契约 |
|---|---|---|
| `Simulator_ElapsedSeconds_IsMonotonic_UnaffectedByWallClockJumps` | `Advance(2s)` 后 = 2.0；`JumpWallClock(±小时级)` 后**仍 = 2.0 且 ≥ 0** | 时间轴与墙上时钟解耦（**回退到 `GetUtcNow` 就红**）|
| `Simulator_ElapsedSeconds_BeforeStart_IsZero` | `Start()` 之前推时针 3h，`ElapsedSeconds == 0` | 修掉 §2 的垃圾值 |
| `Simulator_Start_ResetsElapsedTimeline` | 推 5s → 5.0；再 `Start()` → **回到 0** | 基准确实被重置 |

### 4.2 负向验证（3 组，每组单点破坏 → 恰好 1 红，且**红的不是同一条断言**）

| # | 破坏（等价替换、保留基准） | 实测 |
|---|---|---|
| NV-1 | 基准 → `GetUtcNow().UtcTicks`，读取 → `FromTicks(UtcTicks - 基准)`（全走墙上时钟） | **1 红**：`…IsMonotonic_UnaffectedByWallClockJumps` |
| NV-2 | `ElapsedSeconds => _hasStarted` 改成 `=> true`（去掉 Start 前守卫） | **1 红**：`…BeforeStart_IsZero` |
| NV-3 | `Start()` 里删掉 `_startTimestamp = _time.GetTimestamp();` | **1 红**：`…Start_ResetsElapsedTimeline` |

> **NV-1 第一次写错了**：破坏串是 `(_time.GetUtcNow() - UnixEpoch).TotalSeconds` —— 顺手把**基准**也一起废了，
> 于是红了 2 条（单调性 + Start 重置），无法证明"单调性守卫单独有效"。
> 改成**保留基准的等价替换**后才得到干净的 1 红。
> 教训：**破坏必须是"只改这一件事"的等价替换**，否则一组破坏同时证明了N件事，等于什么都没证明。

### 4.3 全量

**2098 / 2098 全绿**（Core 1566 / AI 237 / App.UiTests 91 / Comm 75 / Device 63 / **Simulator 44** / Hardware 22）。
基线 2095 + 新增 3 = 2098。Release 全量构建 0 警告 0 错误。

## 5. 教训

1. **"根因不修、只修表象"会留债。** `2026-09-14-test-timer-flakiness` 那轮把这两条测试从竞态里摘出来，
   但墙上时钟留在原地 —— 测试稳了，**产品缺陷还在**（NTP 一跳波形就乱）。修测试不稳时要分清
   "测试写法问题"与"被测试的实现问题"。
2. **同一个根因会在不同层各出现一次。** 固件侧的 `CLOCK_MONOTONIC` 契约与这里的 `GetTimestamp`
   是同一条；查归因时应当主动去另一侧找"同类现场"。
3. **"没有守卫的默认值"就是缺陷。** `_startTime` 的默认 `DateTime.MinValue` 静默产出一个
   6.4e10 的合法 double —— 类型系统不会拦，只有断言能拦。
4. **假时钟要把两个时间轴分开。** 单调轴与墙上时间能**分别前进**，才谈得上"证明产品用的是哪一个"。

## 6. 本轮踩到的 API 坑

- **`TimeProvider.GetElapsedTime(long)` 与 `(long, long)` 在 .NET 10 里不是 `virtual`** ——
  重写直接 `CS0506`。正确姿势是覆写 `GetTimestamp()` + `TimestampFrequency`，
  让基类实现（`差值 / 频率`）自己算出正确结果；把频率定为 `TimeSpan.TicksPerSecond` 即可让单位就是 tick。
- `TimeProvider` 是 BCL 类型（`System` 命名空间，.NET 8+），`net10.0` 下**零包依赖** ——
  本机 restore 受限（见 MEMORY §9）时这点很重要。
