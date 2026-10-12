# 模拟正弦相位可复现化（2026-09-14）

## 1. 结论先行

| 项 | 结论 |
|---|---|
| 症状 | 导出产物**不满足「同源同物」**：同一份源码两次导出，`sim_refresh_bindings` 的相位值不同 |
| 根因 | `Math.Abs(key.GetHashCode()) % 360` —— .NET 字符串哈希**每进程随机加盐** |
| 修法 | 抽 `SimBinding.PhaseFor` / `PhaseForCell`（FNV-1a + lowbias32 收尾混淆），两处调用点改用它 |
| 跨进程验收 | 34 个入口产物 + manifest，**两个独立进程逐字节一致（0/36 文件不同）**；修复前为 **18/36 不同** |
| 新增守卫 | `SimBindingPhaseTests` 28 项（18 条黄金值 + 分布质量 + 单元格坐标 + 端到端产物断言） |
| 顺带发现 | **第二条独立非确定性来源**：`FaceplateExpander` 用 `Guid.NewGuid()` 生成实例 id → 只要项目有 Faceplate 实例，产物**依然不可复现**（19/34 产物跨进程不同）。本次未修，已登记 |

## 2. 根因

`src/HmiEditor.CodeGen/CodeGenerator.RefreshBindings.cs` 两处：

```csharp
// :165 控件值
var phase = Math.Abs(ctrl.Id.GetHashCode()) % 360;
// :289 表格单元格
var phase = Math.Abs((ctrl.Id + cell.Row * SimBinding.RowPhaseWeight + cell.Col * SimBinding.ColPhaseWeight).GetHashCode()) % 360;
```

`.NET` 出于哈希碰撞 DoS 防护，对字符串哈希启用 `UseRandomizedStringHashAlgorithm`：**同进程内相同字符串哈希相同，跨进程不同**。用它推导内容标识 → 产物每次导出都变。

附带歧义：`id + row*31 + col*17` 是**裸串拼接**，`("a1", 0, 1)` 与 `("a", 1, 1)` 会拼成同一个串。

后果不止「不好看」：**可复现构建、产物哈希比对、基于 diff 的回归门禁全部失效**。本次 partial 拆分的产物逐字节等价校验就是被它拖住，只能靠掩码绕过（`t + <PHASE>.0f`）。

## 3. 决定性实验

拿**同一份 HEAD 源码连跑两次**（两个独立进程），同样 20 行相位值各异 → **既有缺陷，与文件拆分无关**。

修复前后各写一份独立 harness（34 个入口、全 26 种控件 × 3 变体、4×3 绑定单元格、2 组报警、2 条趋势、4 种 `LogFullPolicy`、7 种缓动路径 + 关键帧、3 屏含 1 个公共图层），两个进程各跑一遍，**不掩码任何字段**：

| 语料 | 产物总数 | 跨进程 sha256 不同 | 含 manifest/phases 共 |
|---|---:|---:|---:|
| **修复后** | 34 | **0** | **0/36** |
| HEAD（修复前） | 34 | 16 | 18/36 |
| 修复后 + Faceplate 实例 | 34 | 19 | 20/36 |

修复前差异**全部落在** `GenerateFullProject` × 8 与 `GenerateSimulatorModule` × 8 两族 —— 只有它们发 `sim_refresh_bindings`：

```
<  float v = 50.0f + 45.0f * sinf(t + 119.0f * 0.017453f);
>  float v = 50.0f + 45.0f * sinf(t + 109.0f * 0.017453f);
<  float v = 100.0f + 90.0f * sinf(t + 3.0f * 0.017453f);
>  float v = 100.0f + 90.0f * sinf(t + 298.0f * 0.017453f);
```

长度也抖（316065 → 316070 字节），因为相位位数在 2↔3 之间跳。连**不同相位的个数**都不稳定：进程 A 44 个、进程 B 43 个。

## 4. 修法

`src/HmiEditor.Core/Design/SimBinding.cs` 从「只放常量」升级为「常量 + 相位推导的唯一实现」，删掉 `RowPhaseWeight` / `ColPhaseWeight`（不再需要，且原实现把两者相加成裸串有碰撞歧义）：

```csharp
public const int PhaseDegrees = 360;
private const uint FnvOffsetBasis = 2166136261u;
private const uint FnvPrime = 16777619u;

public static int PhaseFor(string? key)
{
    unchecked
    {
        var hash = FnvOffsetBasis;
        foreach (var ch in key ?? string.Empty)
        {
            hash = (hash ^ (byte)ch) * FnvPrime;
            hash = (hash ^ (byte)(ch >> 8)) * FnvPrime;   // UTF-16LE：低字节在前
        }
        // 收尾混淆（lowbias32）：消除短串等差规律，把变化扩散到高位
        hash ^= hash >> 16; hash *= 0x7feb352du;
        hash ^= hash >> 15; hash *= 0x846ca68bu;
        hash ^= hash >> 16;
        return (int)(hash % (uint)PhaseDegrees);
    }
}

public static int PhaseForCell(string? controlId, int row, int col)
    => PhaseFor($"{controlId}#{row}:{col}");     // 分隔符拼接，非数值相加
```

两处调用点：

```csharp
var phase = SimBinding.PhaseFor(ctrl.Id);
var phase = SimBinding.PhaseForCell(ctrl.Id, cell.Row, cell.Col);
```

**为什么不是裸 FNV-1a**：FNV-1a 对「只有末字节不同」的短串（`c0`/`c1`/`c2`… 或 `tbl#0:0`/`tbl#0:1`）会产生**等差相位**。这是实现完成后用数据自查发现的，量化指标是「相邻相位差的唯一值个数 / 50 个样本」：

| 指标 | 裸 FNV-1a | + lowbias32 |
|---|---:|---:|
| `c0..c7` | 6,287,12,189,18,195,24,201（偶数位等差 +6） | 311,112,133,204,314,29,152,44 |
| `tbl#0:0..4` | 294,111,**300**,117,202（隔项 +6） | 4,239,186,40,210 |
| 1000 个 id 覆盖值域 | 335/360 | **340/360** |
| 最大桶（理想 2.8） | 9 | 8 |
| **相邻相位差唯一值 /50** | **19** | **47** |

等差相位会让同屏多个绑定的模拟曲线**机械同步骤动**，一眼假 —— 所以这不是「可选优化」，是修复的一部分。

## 5. 黄金值

相位是纯函数、无运行时输入，所以用**跨进程稳定的常量**钉死它。任何依赖运行时的哈希实现（`GetHashCode`、`RuntimeHelpers.GetHashCode`、随机种子…）都会立刻变红：

| key | 相位 | key | 相位 |
|---|---:|---|---:|
| `lbl` | 315 | `c0` | 311 |
| `bar` | 216 | `c1` | 112 |
| `sld` | 342 | `c2` | 133 |
| `arc` | 101 | `a8f3c1d2` | 277 |
| `led` | 31 | `温度` | 45 |
| `sw` | 245 | `罐体A` | 142 |
| `cb` | 125 | `tbl#0:0` | 4 |
| `tbl#0:1` | 239 | `tbl#1:0` | 13 |
| `tbl#2:2` | 5 | `""` / `null` | 256 |

## 6. 验证

### 6.1 负向验证（「全绿」≠「有效」）

挑两条契约逐条破坏产品代码，确认**恰好**对应项变红，然后还原：

| # | 破坏动作 | 期望 | 实测 |
|---|---|---|---|
| NV-A | `PhaseFor` 换回 `Math.Abs(key.GetHashCode()) % 360` | 黄金值 Theory 变红 | **21 失败 / 7 通过** ✓ |
| NV-B | 删掉 lowbias32 收尾混淆 | 分布质量项变红 | **21 失败 / 7 通过**，`PhaseFor_AdjacentKeysProduceUncorrelatedPhases` 命中（19/50 < 40）✓ |
| 对照 | 两条都还原后 `git diff` 无残留 | 干净 | ✓（`SimBinding.cs` 恢复为 57+/5-） |

**NV-A 暴露的关键事实**：`SimulatorModule_IsByteIdenticalAcrossGenerations`（同进程连导两次逐字节相同）**在 NV-A 下依然通过** —— 同进程内盐相同。这证明「同进程字节一致」根本抓不住本缺陷，**只有黄金值能抓**。

**NV-B 暴露的次要事实**：`PhaseFor_LargeKeySetCoversMostOfTheRange` 在 NV-B 下**仍通过**（裸 FNV 335/360 ≥ 阈值 330）—— 覆盖度是弱守卫，「相邻差唯一值」才是真正卡住等差退化的那条。

### 6.2 跨进程端到端

见 §3 对照表。修复后：

```
units      = 34
phases     = 45 distinct / 736 occurrences
manifest.txt / phases.txt / 34 个产物：两个独立进程逐字节一致
```

### 6.3 全量测试

```
Core.Tests        1517    （基线 1489 + 本类 28）
HmiEditor.AI.Tests 237
App.UiTests         91
Comm.Tests          75
Device.Tests        63
Simulator.Tests     41
Hardware.Tests      22
────────────────────────
合计              2046    全绿
```

第一轮全量跑出 **1 项红**：`BuildPipelineServiceTests.CompileAsync_CollectsOutputFiles_AndTogglesUi`。定性为 **flaky，非本次改动引入**：

| 证据 | 结果 |
|---|---|
| 单独复跑该类 | 4/4 通过 |
| 单独复跑 Core.Tests 整个项目 | 1517/1517 通过 |
| 再跑一轮全量 | 2046/2046 通过 |
| 该用例的工程是否产生相位 | 否（单个无绑定的 Label）→ 与本次改动无交集 |

判别特征与既有 flaky 一致：**同一类里轮着红 + 单独复跑全绿 = 非确定性环境依赖**。该用例断言 `Assert.Equal(new[] { true, false }, h.Busy)` 这类**精确序列**，且会走外部工具链探测，在全量并行（7 个项目同时跑）下时序可能抖动。已记入遗留项，未在本轮修。


## 7. 顺带发现：第二条非确定性来源（未修）

`src/HmiEditor.CodeGen/FaceplateExpander.cs:233`：

```csharp
private static HmiControl CloneControl(HmiControl source)
{
    var clone = new HmiControl
    {
        Id = Guid.NewGuid().ToString("N")[..8],   // ← 随机 id
        ...
```

`FaceplateExpander` 在 `CodeGenerator.Simulator.cs:143` 与 `CodeGenerator.SharedLibrary.cs:142` 都会被构造并展开 `project.Templates`。克隆出来的 id **直接进 C 符号名**：

```
< static lv_obj_t* obj_f1b2b995;
< static uint8_t _shapebuf_f1b2b995[LV_CANVAS_BUF_SIZE(80, 80, 32, LV_DRAW_BUF_STRIDE_ALIGN)];
---
> static lv_obj_t* obj_235079a3;
> static uint8_t _shapebuf_235079a3[LV_CANVAS_BUF_SIZE(80, 80, 32, LV_DRAW_BUF_STRIDE_ALIGN)];
```

实测：语料里加 2 个 Faceplate 实例 → **19/34 产物跨进程不同**（修复相位也无济于事）。

**因此「导出可复现」目前只对「无 Faceplate 实例的项目」成立。**

建议修法（**未实施**，因为它会改变已生成代码里的符号名，属行为变更，需先决策）：把 id 从随机改为**确定性派生**，例如 `$"{instance.Id}_{templateControlIndex}"` 或 `$"{instance.Id}_c{seq}"`（`seq` 为该实例内的克隆序号）。既能保证同实例内唯一，又避开了跨实例冲突，且产物与输入一一对应。

## 8. 遗留

| 项 | 状态 |
|---|---|
| 统一两份 `GetPathCCode`（`AnimationConfig.GetPathCCode()` 与 `CodeGenerator.GetPathCCode(AnimationPathType)` 逐字相同） | 未开工 |
| 清 `LV_IMAGE_ALIGN_COVER` 不可达死代码（无害，非用户可见） | 未开工 |
| `VariableSimulator` 注入 `TimeProvider`（`Simulator_ClockMultiplier_*` 断言墙上时钟） | 未开工 |
| FaceplateExpander 随机 id（本文 §7） | **已登记**，建议方案见 §7 |
| `BuildPipelineServiceTests.CompileAsync_CollectsOutputFiles_AndTogglesUi` 全量并行下 flaky（本文 §6.3） | **已登记**，建议按"精确序列断言 + 外部工具链探测"这条机制聚类处理 |
