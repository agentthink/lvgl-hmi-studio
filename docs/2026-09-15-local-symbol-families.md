# 控件类生成符号族收口（2026-09-15）

> 承接 `2026-09-15-control-object-symbol-contract.md`（上一轮只收口了 `obj_*`）。
> 本轮把剩下的一整族收口，并**推翻**上一轮遗留表里的一条判断。

## 1. 结论先行

| 项 | 结论 |
|---|---|
| 上一轮的判断 | §10 把 `label_{id}` / `_h_{id}` / `_cur_{id}` / `_tick_{id}` / `_chart_{id}` / `_rec_{id}` / `_logbuf_{id}` / `_pts_{id}` 归为「**局部**符号，声明与引用同源，**风险低**」 |
| 本轮实测 | **该判断不准确。** 这一族里大部分是**文件级 static / 回调函数 / 跨编译单元符号**，「同源」不成立。按「谁能看到这个符号」重新分类后：跨编译单元 6 处、跨文件 24 处、跨函数约 40 处，真正「函数内局部」的只有约 41 处 |
| 真缺陷 1 | `_cur_<id>` 被**函数内同名局部遮蔽** → 文件级 static 恒为 NULL → 模拟器绑定刷新 `lv_obj_align(_cur_x, …)` 空指针解引用。**不是潜在缺陷，是会崩的真 bug**（§3） |
| 真缺陷 2 | `_knobtri_` / `_curtri_` **创建侧裸拼、引用侧净化** —— 这是上一轮修 `obj_*` 时**亲手造出来的**同类缺陷换了姿势（§4） |
| 真缺陷 3 | `SimulatorProjectExporter.cs:76` 裸拼 `create_screen_{Id}` → id 含 `-`/空格时**启动屏重排静默失效**（§5） |
| 盘点 | 全仓 **116 处 / 33 组**把 `.Id` 直接拼进标识符（脚本统计，非估算） |
| 修法 | `CIdentifier` 新增通用构造器 `Prefixed(prefix, id)` + **18 个具名构造器**；前缀字面量收敛进 `CIdentifier`（§6） |
| 语义等价 | 同一提交口径下用 `tools/ReproHarness` 对拍：**24 个单元里只有 1 行差异**，正是 `_cur_` 遮蔽修复那一处；其余为逐字节纯重构 |
| 新增守卫 | `ControlSymbolSanitizationTests`（新建 4 项）+ `CIdentifierTests`（新增 7 个用例） |
| 顺带挖出 | `TrendAndAlarmCodeGenTests` 有 **4 处断言把非法 C 标识符写成了期望值**，长期"绿"是因为生成器也裸拼 → 收口后变红（§7） |
| 既有遗漏（新登记） | `GeneratePointerDeclarations` 不展开 Faceplate（§9） |

## 2. 归因：116 处按「谁能看到这个符号」分类

盘点的意义不在于数数，而在于**每一类对应一种失效模式**，且「有没有东西守得住它」各不相同：

| 类 | 跨域范围 | 处数 | 失效模式 | 原守卫 |
|---|---|---:|---|---|
| **A** | 跨编译单元（`CodeGenerator` ↔ `SimulatorProjectExporter`） | 6 | 名字对不上 → 启动屏重排静默失效、链接期未定义符号 | 无 |
| **B** | 跨文件（`CodeGenerator.*` partial ↔ `SharedLibrary` ↔ `Simulator`） | 24 | 声明 / 引用错位 → C 编译错误 | 无 |
| **C** | 跨函数（文件级 static + 回调函数 + 值变化回调） | ~40 | 同上；且**可能被同名局部遮蔽** | 无 |
| **D** | 函数内局部（声明与引用同一发射块） | ~41 | 仅非法 id 下产出非法 C | — |
| **E** | 非符号位（调试字符串 / 注释 / 模型字段） | 7 | 无 | — |

关键：**"同源、风险低"只对 D 类成立**。上一轮遗留表把 A/B/C 一起按 D 描述，于是三条真缺陷都被盖住了。

扫描脚本（`.scan/`，已删）：`idscan.py` 分组统计 → `dump.py` 列 116 处明细 → `family.py` 统计族跨文件分布 → `verify.py` 复查残留。

## 3. 真缺陷 1：`_cur_` 被函数内同名局部遮蔽

`Bar` 控件 `ShowPointer = true` 时，游标对象由**三处**共同参与：

| 角色 | 位置 | 原写法 |
|---|---|---|
| **声明**（文件级 static） | `SliderBarIndicators.GeneratePointerDeclarations:116` | `static lv_obj_t* _cur_{cid} = NULL;` |
| **创建/赋值**（`create_screen_*` 内） | `ControlParameters.EmitBarParameters:237` | `lv_obj_t* _cur_{control.Id} = lv_obj_create(o);` |
| **引用**（刷新） | `RefreshBindings:209` / `AppendPointerValueCallbacks:164` | `lv_obj_align(_cur_{cid}, …)` |

问题就在中间那行：函数内 `lv_obj_t* _cur_x = …` 在 C 里是**一个新的自动变量**，它**遮蔽**了同名文件级 static。后果分两条路：

| 引用路径 | 有无 NULL 保护 | 实际后果 |
|---|---|---|
| `sim_refresh_bindings`（`RefreshBindings:209`） | **无** `if` 保护（相邻的 `_curtri_` 同样无） | `lv_obj_align(NULL, …)` → **空指针解引用** |
| `_bar_<id>_val_cb`（`AppendPointerValueCallbacks:164`） | 有 `if (_cur_{cid})` | 不崩，但 static 恒 NULL → **游标永不跟随** |

> 注意 `_curtri_` 的创建是 `AppendCursorPointerTriangle` **通过参数接收已构造的名字**、在同一个函数里赋值，从没写过 `lv_obj_t*` 类型前缀，所以它没被遮蔽 —— 于是同一段代码里两个兄弟符号一个崩一个不崩。这类"只崩一半"最难归因。

**修法**：去掉类型前缀，改成赋值（并留注释说明为什么不能加）：

```csharp
// ⚠️ 这里**不能**写 `lv_obj_t* _cur_x = ...`：同名文件级 static 由
// GeneratePointerDeclarations 声明（条件与本分支完全相同：Bar && ShowPointer），
// 再加类型前缀会**遮蔽**它，于是 static 永远是 NULL，而 sim_refresh_bindings
// 里 `lv_obj_align(_cur_x, ...)` 没有空指针保护 → 空指针解引用。
var cur = CIdentifier.Prefixed("_cur_", control.Id);
sb.AppendLine($"    {cur} = lv_obj_create({o});");
```

**为什么这是本轮唯一的产物变化**：修好之后 static 才真的被赋上值，`RefreshBindings` 的 `lv_obj_align(_cur_x, …)` 才会作用到真实对象上。改动前它在解引用 NULL。见 §8.1 的 A/B 结果 —— 24 个单元里唯一的一行差异就是这个。

## 4. 真缺陷 2：`_knobtri_` / `_curtri_` 的两套构造

同一对符号在四个地方被拼出来，**创建侧与引用侧用了两种写法**：

| 位置 | 原写法 | 净化 |
|---|---|---|
| `SliderBarIndicators:84`（Slider 三角创建） | `"_knobtri_" + control.Id` | ✗ 裸 |
| `SliderBarIndicators:97`（Bar 三角创建） | `"_curtri_" + control.Id` | ✗ 裸 |
| `SliderBarIndicators:113/117`（声明） | `_knobtri_{cid}`，`cid = ForControlSymbol(c.Id)` | ✓ |
| `SliderBarIndicators:151/164/165`（值变化回调） | `_knobtri_{cid}` / `_cur_{cid}` / `_curtri_{cid}` | ✓ |
| `RefreshBindings:210/226`（绑定刷新） | `Prefixed("_curtri_", ctrl.Id)` / `Prefixed("_knobtri_", ctrl.Id)` | ✓ |

即：**创建侧产 `_knobtri_a-b`（非法 C），其余三侧产 `_knobtri_a_b`**，非法 id 下符号直接对不上。这是上一轮修 `obj_*` 时漏掉的同族 —— 当时只扫了 `obj_` 前缀。

## 5. 真缺陷 3：跨编译单元的 `create_screen_*`

这条是 A 类唯一的实例，也是**唯一"不报错、只静默失效"**的一条：

```csharp
// SimulatorProjectExporter.ScreenFuncRegex
@"(?:static\s+)?lv_obj_t\*[ ]+(create_screen_[A-Za-z0-9_]+)\s*\(void\)"
```

`ExtractScreenFunctions` 用这条正则**从生成代码里提取**函数名，`ScreenLayerRefs.CollectReferencedLayerIds` 同样返回已净化的名字；导出器再用 `bootFuncName` 去 `.IndexOf(...)` 找启动屏并把它排到 `screens[0]`。

而 `bootFuncName` 原来是裸拼的：

```csharp
var bootFuncName = $"create_screen_{bootScreen.Id}";   // ✗
```

注意正则的字符类 **`[A-Za-z0-9_]+` 不含中文、不含 `-`/空格** —— 所以提取到的名字天然是净化后的形态。启动屏 id 一旦含 `-`，`bootFuncName` 就永远匹配不上 → **`screens[0]` 变成第一个屏幕而非启动屏，且不报任何错**（§8.2 NV-3 就是这个失效模式的实证）。

修法：`var bootFuncName = CIdentifier.ScreenFactory(bootScreen.Id);`

## 6. 修法：一个通用构造器 + 18 个具名构造器

`CIdentifier` 新增（前缀字面量只出现在这一个文件里）：

```csharp
public static string Prefixed(string prefix, string? id) => prefix + ForControlSymbol(id);

// 屏幕 / 图层（跨文件 + 跨编译单元）
ScreenFactory / ScreenFactoryInner / AdaptiveLayoutFn / ScreenObject / LayerObject
// 报警（文件级 static + 回调）
AlarmRecord / AlarmCheckFn
// 数据记录器（文件级 static + 接口函数）
LogBuffer / LogWriteIndex / LogCount / LogFullFlag
DataLogInitFn / DataLogSampleFn / DataLogExportCsvFn
// 趋势图
ChartObject / ChartSeries / TrendInitFn / TrendUpdateFn
```

`ControlObject` 相应改为委托 `Prefixed("obj_", id)`。

**前缀纪律**（写进 `CIdentifier` 注释）：前缀字面量应只在本文件出现。唯一的例外是**函数内局部、且只在一个发射块内出现**的前缀（`label_` / `_h_` / `_lbl_` / `_tick_` / `_ticklbl_` / `_gauge_lbl_` / `_pts_` / `_cur_` / `_knobtri_` / `_curtri_`），这类允许在调用点直接写 `CIdentifier.Prefixed("前缀_", id)` —— 不存在"两处各自拼接"的风险。这与 §2 的 D 类正好对应。

**顺带清掉的一处非必要字符串拼接**：`CodeGenerator.Trend.cs` 里原先混着 `"..." + logger.Id + "..."` 与插值两种写法（同样进的是 C 字符串字面量，不是符号名），统一成纯插值。`TREND_TRACE("log-%s …", "lg-1", 16)` 里的原始 id 是**有意保留**的调试串。

## 7. 顺带挖出：4 处测试**锁定了缺陷行为**

全量第 17 轮 4 红，全部落在 `TrendAndAlarmCodeGenTests`：

```
[FAIL] GenerateAlarmCode_MultipleGroups_GeneratesCheckFunctions
       Not found: "check_alarm_alarm-1"
[FAIL] GenerateAlarmCode_DisabledAlarm_IsOmitted
       Not found: "check_alarm_enabled-alarm"
[FAIL] GenerateFullProject_WithTrendsAndAlarms_EmitsFixedSnippets
       Not found: "check_alarm_injected-alarm"
[FAIL] GenerateSharedLibraryProject_WithTrendsAndAlarms_EmitsFixedSnippets
       Not found: "check_alarm_injected-alarm2"
```

这 4 条断言的语料 id 都含 `-`，期望值是 `check_alarm_alarm-1` —— **在 C 里那是减法表达式 `check_alarm_alarm - 1`，作为函数定义直接语法错误**。它们一直"绿"，只是因为当时生成器也裸拼。

**定性方式**（不是靠猜）：临时 dump 测试打印实际产物，拿到对照

```
[ALARM] static void check_alarm_alarm_1(float val);      ← 净化后
[CID]   AlarmCheckFn(alarm-1) = check_alarm_alarm_1      ← 构造器一致
[TREND] static data_log_sample_t _logbuf_lg_1[16];
[TREND] static lv_obj_t* _chart_tr_1 = NULL;
[TREND] void datalog_export_csv_lg_1(void (*write_fn)(const char*));
[TREND] if (!_logfull_lg_1) TREND_TRACE("log-%s ring filled (%d records)", "lg-1", 16);  ← 字符串字面量保留原始 id，正确
```

**处置**：断言改走 `CIdentifier` 同一构造器 —— **保留非法 id 语料**（顺带覆盖净化），同时不会把缺陷写进期望：

```csharp
Assert.Contains(CIdentifier.AlarmCheckFn("alarm-1"), code);
// 禁用项无论净化与否都不应出现（"check_alarm_disabled" 是两种形态的公共前缀）
Assert.DoesNotContain("check_alarm_disabled", code);
```

> 顺带一提：`Assert.DoesNotContain("check_alarm_disabled-alarm")` 这种写法**同时**有两种失效 —— 净化前它靠"名字里带 `-`"侥幸通过；净化后它靠"名字里带 `_`"侥幸通过。改成公共前缀 `check_alarm_disabled` 才是真的在断言"禁用项没被生成"。**否定断言里的字面量必须选两种形态的公共部分。**

同时全仓扫了一遍 `"(前缀_…)-"` 形态的字符串字面量，除本轮新加的 `ControlObjectSymbolContractTests` 否定断言（`AssertNoText("obj_lbl-1")`）外无残留。

## 8. 验证

### 8.1 语义等价（「这是纯重构 + 一处真修」）

同一提交口径（`git stash push -- src/` 到 HEAD `3327768` 导出一次 → `pop` → 再导出一次），`tools/ReproHarness` 同一语料：

```
diff -r ab/before/norm ab/after/norm
→ 24 个单元中，差异行数 25（= 1 处 diff × 5 个含该片段的产物）
   唯一的差异：
   - lv_obj_t* _cur_bar_2 = lv_obj_create(obj_bar_2);
   + _cur_bar_2 = lv_obj_create(obj_bar_2);
```

即：**除遮蔽修复外，一个符号名都没变**。这正是"纯重构"该有的证据形态 —— 如果 diff 为空反而说明遮蔽没修上。

### 8.2 负向验证（「全绿」≠「守得住」）

| # | 破坏动作 | 实测 |
|---|---|---|
| NV-4 | `_cur_` 局部声明加回 `lv_obj_t* ` 前缀（重引入遮蔽） | `FileScopeStatics_AreNeverShadowedByIndentedLocals` **恰好 1 红**，精确报 `_cur_bar_3`，其余 3 条保持绿 ✓ |
| NV-1 | `Prefixed` 改为 `prefix + id`（全族不净化） | **20 红**：`CIdentifierTests` 13 行 + `ControlObjectSymbolContractTests` 3 + `ControlSymbolSanitizationTests` 4 ✓ |
| NV-2 | 只把 `Trend.cs` 的 `_logbuf_` 退回裸拼（**单点破坏**） | `IllegalIds_NeverLeakIntoIdentifierPosition` **恰好 1 红**，消息给出 `…static data_log_sample_t _logbuf_lg-1[256]; static volatile ui…` ✓ |
| NV-3 | 导出器 `bootFuncName` 退回 `$"create_screen_{bootScreen.Id}"` | `Exporter_PlacesBootScreenFirst_EvenWithIllegalIdCharacters` **恰好 1 红**：`Expected "create_screen_scr_main_1"` / `Actual "create_screen_scr_aux_2"` —— **启动屏重排失效，`screens[0]` 变成辅助屏** ✓ |
| NV-5 | `_knobtri_` 创建侧退回裸拼 | `IllegalIds_NeverLeakIntoIdentifierPosition` **恰好 1 红**，报 `_tribuf__knobtri_sld 4` / `_knobtri_sld 4` ✓ |
| 对照 | 五处全部还原 | `grep "NV-.*注入"` 无输出；符号三测试类 **48/48 绿**；`diff -r after after2` **0 行** ✓ |

**NV-3 的意义**：这是唯一一个「不报错、只静默失效」的失效模式。启动屏顺序错了在编译、链接、单元测试里都不留痕迹，只能靠一条专门的断言守住 —— 所以这条守卫的价值高于其余四条。

### 8.3 全量测试

```
Core.Tests          1562    （1539 基线 + 本轮新增 23）
HmiEditor.AI.Tests   237
App.UiTests           91
Comm.Tests            75
Device.Tests          63
Simulator.Tests       41
Hardware.Tests        22
──────────────────────────
合计                2091
```

跑法（**不带** `TMPDIR` 覆盖 —— 顺带验收上一轮临时目录收口的成果）：

```bash
env -u TMPDIR -u HMI_TEST_TMPDIR dotnet test HmiEditor.slnx --nologo -v q
```

验收轮次：

| 轮 | 结果 |
|---|---|
| #17 | Core **4 红**，全在 `TrendAndAlarmCodeGenTests`（把非法标识符写成期望值 → §7） |
| #18 | 修掉那 4 红后全绿，但 `AlarmEngineTests.Engine_TriggerThenReturn_CreatesReturnedRecord` 偶发 1 红 |
| #19 | 2091/2091 **全绿** |
| #20 | 2091/2091 **全绿** |

`AlarmEngineTests.Engine_TriggerThenReturn_CreatesReturnedRecord` 的定性（按"失败机制"而非"用例名"）：

| 证据 | 结果 |
|---|---|
| 该用例性质 | **纯内存状态机 + 定时器**，与代码生成毫无关系 |
| 单独复跑该类 ×3 | **3/3 绿**（380 / 331 / 379 ms） |
| 失败点 | 用例内 `SpinWait.SpinUntil(…, 1000)` **硬编码超时**，而同类其他用例早已统一改成 `TestWait.Until`（该类头注释明确要求） |
| 结论 | 全量并行（7 个测试宿主抢 CPU）时被推迟 → 时序 flaky，非产品缺陷。已登记待修 |

## 9. 既有遗漏（新登记，本轮未修）

`SliderBarIndicators.GeneratePointerDeclarations:107` 遍历的是 `project.Screens` 的**原始**控件，而 `CodeGenerator.Screen.cs` 的 `create_screen_*` 用的是 `expander.ExpandScreen(screen)` **展开后**的控件。

后果：**Faceplate 模板里若含 Slider / Bar 且 `ShowPointer` 打开**，展开出的克隆控件会走 `EmitBarParameters` 发射 `_cur_<cloneId> = lv_obj_create(o);`（赋值形态，无类型），而 `GeneratePointerDeclarations` 从没见过这个克隆 id → **赋值给一个从未声明的符号 → C 编译错误 `'_cur_xxx' undeclared`**。

正确修法不是「在 `GeneratePointerDeclarations` 里也调一次 `ExpandScreen`」—— 那会把展开逻辑变成两份（两份就必须永远同步，正是 `CIdentifier` 要消灭的模式）。应当让 `create_screen_*` 把它**实际渲染的控件集合**暴露出来（或抽一个 `EnumerateRenderableControls(project)` 供两侧共用），`GeneratePointerDeclarations` 消费它。

## 10. 教训

1. **「声明与引用同源」是个需要验证的判断，不是可以目测的结论。** 上一轮把 9 个前缀族一眼扫成"同源"，实际里面混着跨编译单元、跨文件、跨函数三种。判据应该只有一个：**这个符号在几个不同的代码块里被提及**。
2. **同一个符号名的"创建侧"最容易被漏。** `obj_*` 那轮漏了 canvas 三个发射器的赋值侧，这轮又漏了 `_knobtri_`/`_curtri_` 的创建侧 —— 两次都是**声明侧改了、赋值侧没改**。收口时要按「声明 / 创建 / 引用」三个角色分别扫，而不是按前缀扫。
3. **遮蔽是"半崩"缺陷。** 同一段代码里两个兄弟符号（`_cur_` 与 `_curtri_`）一个被遮蔽一个没有，导致一个空指针一个正常。加一条**与 id 合法性无关**的集合级守卫（`FileScopeStatics_AreNeverShadowedByIndentedLocals`）比逐个断言有效得多。
4. **测试可以锁定缺陷。** 4 处 `Assert.Contains(<非法 C 标识符>)` 长期为绿，收口后必然变红。碰到"改动后既有测试红了"，先判断**是产物错了还是期望错了** —— 这轮是期望错了，而且错得有价值（它证明净化确实生效）。
5. **否定断言里的字面量要选公共前缀。** 见 §7 末。
6. **能静默失效的那一条最值得写断言。** 启动屏重排错了，编译/链接/其他测试全绿。
7. **A/B 要看"该变的变了、不该变的没变"。** 这轮的 24 单元 1 行差异比"0 差异"更有说服力 —— `0` 可能意味着遮蔽根本没修上。
