# Faceplate 克隆 id 确定性化（2026-09-15）

## 1. 结论先行

| 项 | 结论 |
|---|---|
| 症状 | **只要项目里有 Faceplate 实例，导出产物就不可复现** —— 上一轮把模拟正弦相位改成稳定哈希之后剩下的第二条（也是最后一条已知的）非确定性来源 |
| 根因 | `FaceplateExpander.CloneControl` 用 `Guid.NewGuid().ToString("N")[..8]` 给克隆控件发 id，而该 id **直接进 C 符号名**（`obj_*` / `_shapebuf_*`） |
| 修法 | 克隆 id 改为**确定性派生**：`{净化后的实例id}_{模板内序号}`，子控件递归为 `{父克隆id}_{子序号}` |
| 跨进程验收 | 带 2 个 Faceplate 实例的语料：**20/36 → 0/36** 文件跨进程不同 |
| 语义等价 | 把修复前的随机 id 按序替换成新的派生 id 后，产物与修复后**逐字节等同（残差 0 行）** → 改动纯粹是重命名 |
| 新增守卫 | `FaceplateExpanderTests` +7 项（确定性 / 黄金派生规则 / 两实例不相交 / 子控件路径 / 嵌套路径 / 实例 id 净化 / 端到端可复现） |
| 顺带发现 | `obj_*` 的**声明点未净化**而其它引用点走 `CIdentifier` —— 净化不一致（见 §6，本次未修） |

## 2. 根因

`src/HmiEditor.CodeGen/FaceplateExpander.cs`：

```csharp
private static HmiControl CloneControl(HmiControl source)
{
    var clone = new HmiControl
    {
        Id = Guid.NewGuid().ToString("N")[..8],   // ← 每次调用都随机
        ...
```

`ExpandScreen` 在两条链路里都会被调用：

| 调用点 | 位置 |
|---|---|
| `GenerateSimulatorModule` | `CodeGenerator.Simulator.cs:143` |
| `GenerateSharedLibraryProject` | `CodeGenerator.SharedLibrary.cs:142` |

克隆 id 的直接后果（`CodeGenerator.Screen.cs:42` / `ControlParameters.cs:23` 等）：

```c
static lv_obj_t* obj_f1b2b995;
static uint8_t _shapebuf_f1b2b995[LV_CANVAS_BUF_SIZE(80, 80, 32, LV_DRAW_BUF_STRIDE_ALIGN)];
```

实测同一份源码在两个独立进程里，产物分别是 `obj_f1b2b995` 与 `obj_235079a3`。

## 3. 派生规则与它的理由

```csharp
// ExpandFaceplate 中：
var copy = CloneControl(templateControl, CIdentifier.ForControlSymbol(instance.Id), i);

// CloneControl 中：
Id = $"{idPrefix}_{index}";
...
clone.Children.Add(CloneControl(source.Children[j], clone.Id, j));
```

三条理由：

1. **必须工程内唯一**。发射器把 `obj_*` 声明为**文件作用域静态且不带屏幕前缀**（`Screen.cs:39` 注释原文："使控件指针在 create_screen_*() 和 apply_animations() 间共享"），重名即重复符号。以实例 id 为前缀 ⇒ 实例之间天然不撞；序号在模板内唯一 ⇒ 同一实例内部不撞。
2. **可追溯**。看到 `a8f3c1d2_1` 就知道是实例 `a8f3c1d2` 展开出的模板第 1 个控件；随机 id 完全查不出来源。
3. **不依赖任何运行时随机源** ⇒ 同源导出必得同物。

**为什么要净化前缀**：克隆 id 会原样进 `obj_{Id}`，实例 id 里的 `-` / 空格若原样带下来会生成非法 C，且与其它走 `CIdentifier.ForControlSymbol` 的引用点符号名对不上。用 `CIdentifier` 也顺带复用了全仓唯一实现。

**已知边界**：两个 Faceplate 实例 id 相同（或都为空 → 净化后都落到 `anim`）时派生出相同 id。此时该工程调发射器本身就会产出重复 `obj_*`（非法工程），不在此处兜底。

## 4. 验证

### 4.1 跨进程端到端

同一个 harness（34 个入口、26 种控件 × 3 变体、4×3 绑定单元格、2 组报警、2 条趋势、4 种 `LogFullPolicy`、7 种缓动路径 + 关键帧、3 屏含 1 公共图层），**不掩码任何字段**，两个独立进程各跑一遍：

| 语料 | 修复前 | 修复后 |
|---|---:|---:|
| 无 Faceplate 实例（回归） | 0/36 | **0/36** |
| 带 2 个 Faceplate 实例 | **20/36** | **0/36** |

### 4.2 语义等价（"差异纯粹是重命名"）

把修复前产物里的随机 id 按首次出现顺序替换成对应的派生 id，与修复后产物逐行比对：

```
修复前随机 id: ['f1b2b995', '621ea76f', '6888f688', '455c55e4']
修复后派生 id: ['fp_0_0',   'fp_0_1',   'fp_1_0',   'fp_1_1']
→ 残差 0 行（排除版本横幅）
```

即：**除了符号名，产物一个字符都没变**。这是"纯重命名"最硬的一条证据，也说明改动不会碰既有工程的行为。

### 4.3 负向验证

| # | 破坏动作 | 期望 | 实测 |
|---|---|---|---|
| NV-C | `Id` 换回 `Guid.NewGuid().ToString("N")[..8]` | 确定性 + 黄金值 + 端到端变红 | 5 红 / 12 绿 ✓ |
| NV-D | 子控件前缀不接 `clone.Id`（改传常量 `"x"`） | 仅子控件路径项变红 | 1 红 ✓ |
| NV-E | 实例前缀退化成常量 `"fixed"` | 两实例不相交项变红 | 5 红（含两条 NV-C 抓不到的）✓ |
| NV-F | 去掉 `CIdentifier.ForControlSymbol` 包装 | 净化项变红 | 1 红 ✓ |
| 对照 | 全部还原后 `git diff` 无残留 | 干净 | ✓ |

**NV-C 暴露的断言盲区**：`ExpandScreen_TwoInstances_ProduceDisjointIds` 在 NV-C 下**照样通过** —— 随机 id 天生不撞，那条测试守的是"前缀退化导致撞名"，必须靠 NV-E 才证明有效。**一组破坏只能证明它打算证明的那件事**。

### 4.4 全量测试

```
Core.Tests        1524    （上一轮 1517 + 本类新增 7）
HmiEditor.AI.Tests 237
App.UiTests         91
Comm.Tests          75
Device.Tests        63
Simulator.Tests     41
Hardware.Tests      22
────────────────────────
合计              2053    全绿
```

## 5. 至此"导出可复现"覆盖到哪

| 产物族 | 可复现 |
|---|---|
| `GenerateFullProject` / `GenerateSimulatorModule` | ✓（相位 + Faceplate 两处都修了） |
| `GenerateSharedLibraryProject` | ✓ |
| 屏幕 / 主题 / 图层引用表 / 报警 / 趋势 / 动画 / 构建系统 | ✓ |
| 含 Faceplate 实例的项目 | ✓（本次） |

> 注意 `// Generator: HmiEditor.CodeGen 1.0.0+<commit>` 横幅里嵌了 git 提交号 ——
> **跨提交**比对必然不同（这是版本戳，有意为之），**同一提交内**则稳定。
> 做产物 A/B 时若两边不在同一提交，先排除这一行，否则会把"我提交了"误读成"产物变了"。

## 6. 顺带发现：`obj_*` 的净化不一致（未修）

`obj_*` 符号名有两套构造方式并存：

| 位置 | 写法 | 净化 |
|---|---|---|
| `CodeGenerator.Screen.cs:42` `static lv_obj_t* obj_{control.Id};` | 裸 id | ✗ |
| `CodeGenerator.Actions.cs:246/247/253` `obj_{control.Id}` | 裸 id | ✗ |
| `CodeGenerator.ControlParameters.cs:23` `var o = $"obj_{control.Id}"` | 裸 id | ✗ |
| `CodeGenerator.RefreshBindings.cs:173/186` `obj_{ctrlId}`（`ctrlId = CIdentifier.ForControlSymbol(...)`） | 净化后 | ✓ |

于是 id 里含 `-`、空格等字符时，**声明**会生成非法 C（`obj_a-b`），而刷新绑定处引用的是 `obj_a_b` —— 两边对不上。设计器自动生成的是 8 位十六进制 id，正常路径踩不到，故非用户可见缺陷。本次只保证**克隆 id 自己不含需净化字符**，未去动这条既有不一致。

## 7. 遗留

| 项 | 状态 |
|---|---|
| `obj_*` 净化不一致（本文 §6） | **新登记** |
| `BuildPipelineServiceTests.CompileAsync_CollectsOutputFiles_AndTogglesUi` 全量并行下 flaky | 已登记（见 `2026-09-14-sim-phase-reproducibility.md` §6.3） |
| 统一两份 `GetPathCCode` | 未开工 |
| 清 `LV_IMAGE_ALIGN_COVER` 死代码 | 未开工 |
| `VariableSimulator` 注入 `TimeProvider` | 未开工 |
