# `obj_*` 符号名唯一构造（2026-09-15）

## 1. 结论先行

| 项 | 结论 |
|---|---|
| 症状 | 同一个 C 符号 `obj_<控件id>` 有**两套构造方式**：声明侧写裸 `control.Id`，引用侧走 `CIdentifier.ForControlSymbol` |
| 严重性 | **不是用户可见缺陷，是潜在缺陷**（哑弹）。设计器只产出 8 位十六进制 id，净化对合法标识符是**恒等**，所以正常路径踩不到；id 里含 `-`/空格时才会声明 `obj_a-b`（非法 C）而引用 `obj_a_b`（对不上） |
| 为什么还是得修 | 「同一个符号名有两套构造方式」正是 `CIdentifier` 要消灭的东西。而且不止声明/引用这一处，底下还埋着 `label_{id}`、`layer_{id}`、`create_screen_{id}`、`_buf_{id}` 一整族 |
| 修法 | 新增 `CIdentifier.ControlObject(id)` 作为 `obj_*` 的**唯一构造器**；30 处发射点全部改走它 |
| 语义等价 | 改动前后同一语料产物**逐字节相同（24/24 单元 0 差异）** —— 证明这是纯重构，零行为变化 |
| 跨进程可复现 | 无 Faceplate 语料 **0/24**、含 2 个 Faceplate 实例语料 **0/24** |
| 新增守卫 | `ControlObjectSymbolContractTests` **新建 6 项**；`CIdentifierTests` 新增 3 个方法（Theory 7 行 + 2 Fact = 9 个用例）—— 两类合计 **27 项** |
| 新增工具 | **`tools/ReproHarness`** —— 导出可复现性的度量仪（本次为了拿证据重建，之前那版丢在 `/tmp` 被清了） |
| 顺带修掉 | 改动过程中**自己撞出来的新不一致**：canvas 系三个发射器（`ProcessControlCodeGen` / `ShapeControlCodeGen` / `SvgToLvglConverter`）的 `obj_*` 赋值侧仍裸用 → 见 §5 |
| 顺带定位 | 一条**长期存在的 flaky 根因**：`/tmp` 是 10MB tmpfs，测试全量并行时被写满 → 见 §8 |

## 2. 归因：两个构造点，一个符号名

`obj_*` 是**文件作用域静态、不带屏幕前缀**（`CodeGenerator.Screen.cs:39` 注释原文：「使控件指针在 `create_screen_*()` 和 `apply_animations()` 间共享」），所以它天然是**跨函数、跨文件**的符号。而它的构造点散在 4 个文件里：

| 位置 | 原写法 | 净化 |
|---|---|---|
| `CodeGenerator.Screen.cs:42`（**声明**） | `$"static lv_obj_t* obj_{control.Id};"` | ✗ 裸 |
| `CodeGenerator.Actions.cs`（回调注册） | `$"obj_{control.Id}"` | ✗ 裸 |
| `CodeGenerator.ControlParameters.cs`（椭圆/组合内部） | `$"obj_{control.Id}"` | ✗ 裸 |
| `CodeGenerator.VisualStates.cs`（状态样式） | `$"obj_{control.Id}"` | ✗ 裸 |
| `CodeGenerator.RefreshBindings.cs`（18 处，**引用**） | `$"obj_{ctrlId}"`，`ctrlId = CIdentifier.ForControlSymbol(ctrl.Id)` | ✓ 净化 |

最刺眼的一行证据 —— `CodeGenerator.Actions.cs` 里**同一行**，回调名走净化、对象名走裸用：

```csharp
sb.AppendLine($"    lv_obj_add_event_cb(obj_{control.Id}, on_press_obj_{controlId}{actionSuffix}, LV_EVENT_PRESSED, NULL);");
//                                 ↑ 裸                                     ↑ 已净化（controlId 是净化值）
```

这就是「两条路径必须给出逐字符相同的名字」这条铁律被违反的样子。

## 3. 修法：唯一构造器

`src/HmiEditor.Core/CodeGen/CIdentifier.cs` 新增：

```csharp
/// <summary>
/// 控件对象指针的**完整符号名**：<c>obj_<b>&lt;净化id&gt;</b></c>。
/// <para><b>必须走这里，不要手写 <c>$"obj_{control.Id}"</c></b>：该符号由
/// <c>CodeGenerator.Screen.cs</c> **声明**、由 <c>RefreshBindings</c> /
/// <c>VisualStates</c> / <c>ControlParameters</c> 等**多处引用**，两条路径必须给出
/// 逐字符相同的名字。……</para>
/// </summary>
public static string ControlObject(string? controlId) => "obj_" + ForControlSymbol(controlId);
```

30 处调用点，两种替换形态：

| 形态 | 处数 | 例 |
|---|---:|---|
| 整串插值 → 直接调用 | 3 | `var o = CIdentifier.ControlObject(control.Id);` |
| 内嵌插值 → 内嵌调用 | 27 | `sb.AppendLine($"static lv_obj_t* {CIdentifier.ControlObject(control.Id)};");` |

涉及文件：`CodeGenerator.Screen.cs`(38 行)、`VisualStates.cs`(14)、`Actions.cs`(6)、`ControlParameters.cs`(2)。

**为什么不用「给 id 局部变量净化」这种更省事的做法**：那样 `obj_` 仍在多处被手写拼出来，等于把「两套构造方式」降级成「一套但散落」。改完之后 `obj_` 的字符串**只在 `CIdentifier.ControlObject` 里出现一次**。

## 4. 范围盘点（先估多了）

用行级正则先盘了一遍 `obj_` 家族，再按性质分类：

| 形态 | 处数 | 性质 | 本次处置 |
|---|---:|---|---|
| `obj_{ctrlId}`（RefreshBindings） | 18 | 已净化 ✓ | 不动 |
| `obj_{control.Id}` | 31 | **裸用**（含 1 处在 XML 注释里） | **改** |
| `obj_{controlId}`（Actions 回调名） | 6 | 已净化 ✓ | 不动 |
| `obj_{id}` / `obj_{Id}` | 27 + 2 | 注释与源码注释串 | 不动 |

「31 处」里有一处在注释里（`FaceplateExpander.cs:238`），实际改 30 处 —— 这就是为什么我说**正则给的初值一定要人工核**。

## 5. 关键：改动过程中自己撞出来的新不一致

改 `Screen.cs:42` 之后做了第二轮全量扫描，发现 **`obj_*` 的赋值侧还有三个发射器是裸的**：

| 发射器 | 产物中的符号 | 原 id 来源 |
|---|---|---|
| `ProcessControlCodeGen`（Tank/Pipe/Valve） | `obj_{id}`、`_buf_{id}`、`layer_{id}` | `var id = c.Id;`（6 处） |
| `ShapeControlCodeGen`（Ellipse） | `obj_{id}`、`_shapebuf_{id}` | `var id = c.Id;` |
| `SvgToLvglConverter`（Svg） | `obj_{id}`、`buf_{id}`、`layer_{id}` | `var id = control.Id;` |

**这是我这轮改动亲手造出来的**：改之前「声明裸 + 赋值裸」自洽（对非法 id 也只是自洽地产出非法 C）；改之后变成「声明净化 + 赋值裸」——同类缺陷换了个姿势，比原来更难发现。

所以这三处必须一起收：

- `obj_*`（跨函数的文件级 static）→ 一律从**原始 id** 现场构造：`CIdentifier.ControlObject(c.Id)`。
- `_buf_*` / `_shapebuf_*` / `buf_*`（也是文件级 static，声明在自己文件、引用在 `OpenCanvas`）→ 声明与引用两侧都走 `CIdentifier.ForControlSymbol`。
- `layer_*`（纯函数内局部）→ 由已净化的 `id` 参数派生，声明与引用同函数，天然自洽。

## 6. 验证

### 6.1 语义等价（「这是纯重构」）

同一语料（3 屏 / 80 控件 / 26 种控件 × 3 变体 / 4×3 表格绑定单元格 / 2 报警组 / 2 趋势 / 2 记录器 / 8 条动画 / 公共图层），`git stash` 切到改动前编译一次、改动后编译一次：

```
diff -r before/norm after/norm
→ 24/24 单元 0 差异（含 manifest）
```

即：**除了这次真的没改任何逻辑，产物一个字符都没动**。

### 6.2 跨进程可复现

| 语料 | 单元数 | 跨进程差异 |
|---|---:|---:|
| 无 Faceplate | 24 | **0** |
| 含 2 个 Faceplate 实例 | 24 | **0** |

（Faceplate 那条是上一轮 `fb84726` 的成果，这里作为回归。）

### 6.3 负向验证（「全绿」≠「守得住」）

| # | 破坏动作 | 期望 | 实测 |
|---|---|---|---|
| NV-H | 4 个常规发射器文件全部退回裸用 | 契约类变红、`CIdentifierTests` 全绿 | **3 红 / 22 绿** ✓ |
| NV-I | 3 个 canvas 发射器退回裸用（声明仍净化） | canvas 语料项变红 | **2 红 / 25 绿** ✓ |
| NV-J | 给模拟器产物注入 `DateTime.Now.Ticks` | harness 应报非确定性 | **3 个产物差异**（正是携带模拟器头部的 `simulator_module.c` / `full_project.c` / `export/generated_hmi.c`）✓ |
| 对照 | 全部还原后 `git diff` / `git status` 无残留 | 干净 | ✓ |

**NV-J 的意义**：`diff -r = 0` 本身可能是「什么都没比到」的假象。必须先用一个**已知非确定性**把仪器本身验证一遍，`0` 才有意义。这跟「守卫测试要先做负向验证」是同一条纪律，只是对象换成了度量工具。

NV-H 的精确命中：

```
[FAIL] ControlObjectSymbolContractTests.EmptyId_FallsBackToControlSymbolFallback
[FAIL] ControlObjectSymbolContractTests.DeclaredAndReferencedControlObjects_MatchExactly
[FAIL] ControlObjectSymbolContractTests.ControlIdWithIllegalCharacters_UsesSanitizedSymbolEverywhere
失败! - 失败: 3，通过: 22，总计: 25
```

NV-I 的精确命中（注意只红 2 条、`LegalCanvasIds_*` 仍绿 —— 合法 id 净化是恒等）：

```
[FAIL] ControlObjectSymbolContractTests.CanvasControlIdWithIllegalCharacters_UsesSanitizedSymbolInBufferAndObject
[FAIL] ControlObjectSymbolContractTests.DeclaredAndReferencedControlObjects_MatchExactly
失败! - 失败: 2，通过: 25，总计: 27
```

### 6.4 全量测试

```
Core.Tests        1539    （上一基线 1524 + 本轮 15）
HmiEditor.AI.Tests 237
App.UiTests         91
Comm.Tests          75
Device.Tests        63
Simulator.Tests     41
Hardware.Tests      22
────────────────────────
合计              2068    全绿
```

跑之前务必 `env TMPDIR=<大盘目录>`，否则会撞上 §8 那个 flaky。

## 7. 守卫测试：集合级不变量 + 语料覆盖

不逐行断言，而是断**两条集合级不变量**：

```csharp
private static readonly Regex DeclaredPattern =
    new(@"static\s+lv_obj_t\*\s+(obj_[A-Za-z0-9_]+)\s*;", RegexOptions.Compiled);
private static readonly Regex ReferencedPattern =
    new(@"\bobj_[A-Za-z0-9_]+", RegexOptions.Compiled);
```

| 不变量 | 含义 |
|---|---|
| 产物里的 `obj_*` 都是合法 C 标识符 | 没有「裸 id 漏进符号名」的痕迹 |
| **声明集合 == 引用集合** | 引用多一个 = 未定义符号；声明多一个 = 有发射点漏了 |

第二条在真实产物上是**精确成立**的（实测 87/87、91/91；本轮 canvas 语料同样成立）。

**本轮学到的最重要一条**：**集合级不变量只在语料覆盖到的地方有效**。第一版 fixture 只喂 `Label/Bar/Led/Slider` —— 那三个 canvas 发射器**永远不会被经过**，于是「声明==引用」照样绿。补了 `GenerateWithCanvasControlIds`（Tank/Pipe/Valve/Ellipse/Svg 五种）之后，NV-I 才打得红。

> 规则：**守卫的上限由语料决定，不由断言写法决定。** 加了一条新不变量，先问「什么语料能让它红」，再补语料。

## 8. 顺带定位：`/tmp` 是 10MB tmpfs → 长期 flaky 的根因

全量第一轮跑出 1 红：

```
[FAIL] HmiEditor.Core.Tests.MockImageLoadingTests.GeneratedImagesC_WindowsPath_ConvertedToUnix
```

定性过程：

| 证据 | 结果 |
|---|---|
| 单独复跑该类 | 18/18 通过 |
| 该用例与本次改动是否相关 | **否**（断言 `generated_images.c` 里的路径宏，与符号名无关） |
| `df -h /tmp` | **`tmpfs 10M 0 10M 0%`** |
| `Path.GetTempPath()` 落点 | `TMPDIR` 为空 → `/tmp` |
| 换 `TMPDIR` 到大盘复跑 | **全绿** |

即：测试把临时导出目录建在 `/tmp`，而 `/tmp` 只有 10MB，7 个测试项目并行时会被写满 → 随机某个用例读到截断/缺失的文件。

**处置**：本轮不改代码（改 `TMPDIR` 即可绕开），但这是 `BuildPipelineServiceTests.*` 之外**第二条**全量并行 flaky 的候选统一解释。建议后续在测试基类里把临时目录根改到仓库旁的大盘目录，或加一条 `TestTempDir` 自检。

> **后续（2026-09-15 落实、2026-09-16 验证）**：上面这条建议已执行 —— `TestTempDir` 成为测试临时目录唯一入口（`HMI_TEST_TMPDIR` → `TMPDIR` → `Path.GetTempPath()` → 仓库内 `.hmi-test-tmp/`），`MockImageLoadingTests` 这条路不再碰 `/tmp`。2026-09-16 以默认并行度连跑 6 次全量全绿（每次 2248/0），`project-status.md` 阻断项 9 因此关闭。

**本地跑测试的正确姿势**（⚠️ **已作废**，2026-09-15）：

> 下面这条在当天晚些时候的 `3327768` 里被彻底收口了。现在 `dotnet test HmiEditor.slnx` **直接跑**即可 ——
> `TestTempDir` 会自己探测可用根（`HMI_TEST_TMPDIR` → `TMPDIR` → `Path.GetTempPath()` → 仓库内 `.hmi-test-tmp/`），
> 不再依赖外部注入 `TMPDIR`。详见 `2026-09-15-test-tempdir-two-roots.md`。保留原文以免看起来像"曾经乱改过"。

```bash
mkdir -p ~/tmp-hmi-tests
env TMPDIR=~/tmp-hmi-tests dotnet test HmiEditor.slnx
```

## 9. 新增工具：`tools/ReproHarness`

之前做「导出可复现」验证用的 harness 是临时脚本，放在 `/tmp`（10MB tmpfs）里 —— **被清掉了**，本轮只能重建。这次做成仓库内的独立工具（照 `tools/SimExport` 的样，**不入 `HmiEditor.slnx`**，不影响构建/测试门禁）。

```
用法: repro-harness <outDir> [--faceplate] [--raw]

# 跨进程可复现（两次运行必须逐字节一致）
repro-harness ~/r1 ; repro-harness ~/r2
diff -r ~/r1/norm ~/r2/norm          # 期望无输出

# 跨提交等价（同一语料在改动前后各跑一次）
repro-harness ~/before ; repro-harness ~/after
diff -r ~/before/norm ~/after/norm   # 期望只差你打算改的东西
```

语料：全 26 种控件 × 3 变体、7 种 SVG 形状、4×3 表格绑定单元格、3 种绑定方向 + 表达式绑定、三态视觉样式、按变体分派的动作、2 组 × 3 条报警（8 种条件类型 / 4 级严重度全覆盖）、2 条趋势、2 个记录器（两种 `LogFullPolicy`）、7 种缓动路径 + 关键帧动画、3 屏含 1 个公共图层、2 个从站、可选 2 个 Faceplate 实例。全部 id 与时间戳**钉死**。

默认把产物横幅 `// Generator: HmiEditor.CodeGen <ver>+<sha> (git <short>)` 折叠成固定串再比对 —— 它**有意**嵌了构建时 git 提交号，跨提交比对必然全红（`--raw` 可关）。

## 10. 遗留

| 项 | 状态 |
|---|---|
| 残留裸族（`label_{id}` / `_h_{id}` / `_cur_{id}` / `_tick_{id}` / `_chart_{id}` / `_rec_{id}` / `_logbuf_{id}` / `_pts_{id}` …） | ✅ **已修**（2026-09-15，见 `2026-09-15-local-symbol-families.md`）。⚠️ 本节原判断「**局部**符号，声明与引用同源，**风险低**」**不准确** —— 实测这一族里大部分是文件级 static / 回调函数 / 跨编译单元符号，"同源"不成立；正是这条错误判断盖住了底下 3 条真缺陷（`_cur_` 被同名局部遮蔽 → 空指针；`_knobtri_`/`_curtri_` 创建侧裸拼；导出器启动屏重排静默失效） |
| `SimulatorProjectExporter.cs:76` 裸拼 `create_screen_{bootScreen.Id}` | ✅ **已修**（同上）。失效模式是**静默**的：id 含 `-` 时 `screens[0]` 不是启动屏，且编译/链接/测试全不留痕 |
| 测试临时目录根改到大盘（§8） | ✅ **已完成**（commit `3327768`，见 `2026-09-15-test-tempdir-two-roots.md`）。⚠️ §8 结尾「本地跑测试要 `env TMPDIR=…`」的做法**已作废** —— 现在直接 `dotnet test` 即可，两个根由 `TestTempDir` 自行探测 |
| `GeneratePointerDeclarations` 不展开 Faceplate（Faceplate 模板含 Slider/Bar 时，展开出的克隆控件赋值给未声明符号 → C 编译错误） | **新登记**（见 `2026-09-15-local-symbol-families.md` §9） |
| `AlarmEngineTests.Engine_TriggerThenReturn_CreatesReturnedRecord` 全量并行 flaky | **新登记**：用例内 `SpinWait.SpinUntil(…, 1000)` 硬编码超时，而同类其他用例已统一改 `TestWait.Until`（该类头注释明确要求）；单跑 3/3 绿、全量偶尔红 |
| `BuildPipelineServiceTests.CompileAsync_CollectsOutputFiles_AndTogglesUi` 全量并行 flaky | 已登记（见 `2026-09-14-sim-phase-reproducibility.md` §6.3） |
| 统一两份 `GetPathCCode` | 未开工 |
| 清 `LV_IMAGE_ALIGN_COVER` 死代码 | 未开工 |
| `VariableSimulator` 注入 `TimeProvider` | 未开工 |
