# Faceplate 展开：控件清单单一来源 + 克隆属性保真

> 2026-09-15。承接「控件类生成符号族收口」（`9e52e62`）与本文件登记的两条遗留。
> 触发点是 `GeneratePointerDeclarations` 不展开 Faceplate —— 查下去发现**同一份"控件清单"有 5 个消费者，4 个与 `create_screen_*` 不同步**，另有一处更严重的「克隆体丢属性」。

## 1. 归因：两种"控件清单"

| 清单 | 来源 | 谁在用 |
|---|---|---|
| **原始**控件 | `screen.Controls`（Faceplate 实例只是一个占位控件） | `GeneratePointerDeclarations`、`AppendPointerValueCallbacks`、`GenerateRefreshBindings`（两处） |
| **展开后**原子控件 | `FaceplateExpander.ExpandScreen(screen)` | `GenerateScreenCode`（→ `create_screen_*` 的每一个发射器） |

两者只在**工程里有 Faceplate 实例**时不同。于是凡是走"原始清单"的发射器，都会与"展开后清单"错位。

全仓遍历点（`grep "in screen.Controls"`）：

| 位置 | 用途 | 错位后果 |
|---|---|---|
| `CodeGenerator.Screen.cs:36` | `create_screen_*` 全部发射 | —— （基准） |
| `CodeGenerator.SliderBarIndicators.cs` `GeneratePointerDeclarations` | 文件级 static 指针声明 | **C 编译错误**（未声明的标识符） |
| 同上 `AppendPointerValueCallbacks` | 值回调定义 | **C 编译错误**（隐式函数声明） |
| `CodeGenerator.RefreshBindings.cs`（bound 收集） | 绑定刷新语句 | 克隆体绑定**静默不刷新**（功能缺失） |
| 同上（tableCells 收集） | 表格单元格绑定刷新 | 同上 |

失效条件：Faceplate 模板含 **Slider（`ShowPointer != false`）/ Bar（`ShowPointer == true`）**，或模板控件带变量绑定 / 表格单元格绑定。

## 2. 第二层缺陷（查第一层时撞出来的）：`CloneControl` 丢属性

`HmiControl` 有 **80+ 属性**，而旧 `FaceplateExpander.CloneControl` **手工逐个赋值**，只赋了 18 个。漏掉的（约 50 项）包括：

`ShowPointer` / `ShowTicks` / `MinValue` / `MaxValue` / `StepValue` / `MajorTickCount` / `IndicatorColor` /
`Rotation` / `RotationDirection` / `MirrorX` / `MirrorY` / `ImageAssetId` / `TextAlign` / `TextVAlign` /
`FontFamily` / `FontSize` / `ValueDisplayType` / `ValueScaleFactor` / `ValueDecimalPlaces` / `ValuePrefix` /
`ValueSuffix` / `Opacity` / `Cells` / `Points` / `SvgShapes` / `SvgViewBoxWidth` / `VisualStates` /
`Options` / `SelectedIndex` / `RowCount` / `ColumnCount` / `HeaderTexts` / `RowHeight` / `ColumnWidths` /
`TabTitles` / `StretchMode` / `Brightness` / `SpinSpeed` / `LineCap` / `KeyboardMode` / `TankShowScale` /
`TankTickCount` / `PipeDirection` / `PipeFlowDirection` / `PipeFlowSpeed` / `ValveType` …

**用户可见后果**：Faceplate 里放的 SVG / 表格 / 折线 / 图片 / 旋转控件、以及滑块的数值范围与指针刻度，
展开后**全部退化成默认值**（设计器里配好、产物里全无）。

同源的第二处：`CloneControl` 里手工 `new` 的集合元素只拷了部分字段 —— `VariableBindingData` 漏了
`Kind` / `Expression`（旧字段 `VariableBindingData.Expression` 是表达式绑定，漏了会静默失效）。

## 3. 做法

### 3.1 控件清单收口到唯一入口

新增 `src/HmiEditor.CodeGen/CodeGenerator.ControlEnumeration.cs`：

```csharp
internal static List<HmiControl> EnumerateRenderableControls(HmiScreen screen, FaceplateExpander? expander);
internal static IEnumerable<HmiControl> EnumerateRenderableControls(HmiProject project);
```

5 个消费者全部改为经此入口。约定：**任何遍历"项目里有哪些控件"的发射器都不得直接遍历 `project.Screens[].Controls`**。

> 展开每次都重新克隆（`ExpandScreen` 不改源数据），克隆 id 是**确定性派生**的
> （`{净化实例id}_{模板内序号}`），所以多处调用得到的符号名一致 —— 这正是"声明侧与使用侧能对上"的前提。

### 3.2 克隆改为「浅拷贝 + 集合重建」

| 层 | 改动 |
|---|---|
| `HmiControl` | 新增 `ShallowCopy()`（`MemberwiseClone`）—— **新增属性自动带上**，治好"手工列字段必然漂移" |
| 数据类 6 个 | `VariableBindingData` / `HmiAction` / `ParameterBinding` / `ControlVisualState` / `TableCellData` / `SvgShape` 各加 `ShallowCopy()`（`SvgShape` 额外克隆 `double[] Points`；`PointData` 是 record，列表拷贝即深拷贝） |
| `FaceplateExpander.CloneControl` | `source.ShallowCopy()` + 把 `Children`/`VariableBindings`/`Actions`/`ParameterBindings`/`VisualStates`/`Cells`/`Points`/`SvgShapes`/`Action` **换成独立实例**；`Id` 改派生值；`ParentId` 置 null（父子关系由 `Children` 重建） |

**为什么集合必须换新实例**：浅拷贝后副本与**模板本身**共享同一个集合，而展开随后会做参数替换
（`ApplyParameterSubstitution`）—— 共享会让第一个实例把占位符写进模板，于是第二个实例拿到第一个的值，
**且源工程被静默改脏**（与"产物可复现"直接冲突）。

## 4. 证据

### 4.1 守卫（3 项新增，Core 1563 → 1566）

| 守卫 | 类型 | 断什么 |
|---|---|---|
| `ControlSymbolSanitizationTests.ExpandedControls_HaveMatchingPointerDeclarationsAndCallbacks` | 集合级、族无关 | 每个 `_cur_*`/`_knobtri_*`/`_curtri_*` 赋值点**必须有同名文件级 static 声明**；每个 `_bar_*_val_cb`/`_slider_*_val_cb` 引用点**必须有同名函数定义** |
| `FaceplateExpanderTests.ExpandScreen_Clone_PreservesEveryTemplateProperty` | **反射版** | 克隆与模板控件逐属性相等（除 Id/X/Y/ParentId/Children）—— 新增属性自动被覆盖 |
| `FaceplateExpanderTests.ExpandScreen_TwoInstances_WithDifferentParameterValues_AreIndependent` | 行为 | 两实例绑不同实参互不影响，且模板本身不被改脏 |

语料同步补强：`ControlSymbolSanitizationTests` 加模板（Bar+Slider+绑定）+ 实例（id 含 `-`）；
`tools/ReproHarness` 的 Faceplate 模板加 Bar/Slider（含 ShowPointer/ShowTicks/非默认范围/Rotation/变量绑定）。

> **语料教训复现**：补强 harness 语料**之前**，带 Faceplate 的 A/B 也是 **0 差异** ——
> 旧模板只有 Label/Tank/Led（属性全默认、无绑定），三处修复一处都碰不到。`diff = 0` 先问"比到了吗"。

### 4.2 产物 A/B（同提交口径，`tools/ReproHarness`）

| 语料 | 结果 | 含义 |
|---|---|---|
| 无 Faceplate（24 单元） | **0/24 差异**，`hashes.txt` 全同 | 纯修复，对无 Faceplate 工程**逐字节零影响** |
| 含 2 个 Faceplate（24 单元） | **4/24 差异**，+702 行 | 恰好是携带克隆符号的 4 个单元：`codegen/{simulator_module,shared_library,full_project}.c` + `export/generated_hmi.c` |
| 同提交跨进程两次 | **0 差异** | 改动没引入新的非确定性 |

差异内容抽样（`simulator_module.c`）：

```
+static lv_obj_t* _cur_fp_pump_a_3 = NULL;        ← 声明补齐（修前完全没有 → 未声明的标识符）
+static lv_obj_t* _knobtri_fp_pump_a_4 = NULL;
+static void _bar_fp_pump_a_3_val_cb(lv_event_t* e) { ... }   ← 定义补齐（修前 → 隐式函数声明）
+    int x = (int)((v - -20) / 140.0f * 80 + 0.5f) - 10;
                 ^^^^^^ 修前是默认范围 0/100 —— 属性保真的证据
```

### 4.3 负向验证（5 组，每组单点破坏 → 恰好对应项红）

| # | 破坏 | 实测 |
|---|---|---|
| NV-1 | `GeneratePointerDeclarations` 退回原始清单 | 1 红（`ExpandedControls_…` 的**指针未声明**断言）/ 24 绿 |
| NV-2 | `AppendPointerValueCallbacks` 退回原始清单 | 1 红（同守卫的**回调未定义**断言，报 `_bar_fp_1_0_val_cb, _slider_fp_1_1_val_cb`）—— 两条断言各自被独立证明 |
| NV-3 | `bound` 收集退回原始清单 | 1 红（`SanitizedNames_ReachEveryEmitterFamily` 的绑定刷新断言）|
| NV-4 | `CloneControl` 丢 `ShowPointer`/`SvgShapes` | 2 红（属性保真守卫 + 正向语料断言连带）|
| NV-5 | `CloneControl` 共享 `VariableBindings` | 1 红（两实例独立守卫）|

还原后 `git diff` 与改动清单一致，无残留。

### 4.4 全量

**2095 / 2095 全绿**（Core 1566 / AI 237 / App.UiTests 91 / Comm 75 / Device 63 / Simulator 41 / Hardware 22）。
基线 2092 + 新增 3 = 2095。

## 5. 教训

1. **"同一份清单有多个消费者"要显式收口。** 5 个消费者里 4 个走错清单，靠人盯是盯不住的 ——
   收口成一个函数 + 一条集合级不变量，才能让"新增第 6 个消费者"时自动正确或被守卫拒绝。
2. **手工逐个赋值的拷贝函数是定时炸弹。** 80+ 属性手写 18 个，不会有任何编译/测试告警，
   只会在"用户把 SVG 放进 Faceplate"时静默出错。用 `MemberwiseClone` —— 让**新增字段自动被覆盖**，
   并用反射版守卫把这条性质钉住。
3. **浅拷贝之后，集合是要主动处理的**。`ShallowCopy` 让"漏处理集合"变成唯一风险点，
   所以守卫要专门盯"两实例参数互不串味 + 模板不被改脏"。
4. **`diff = 0` 先问"比到了吗"。** 补语料前，含 Faceplate 的 A/B 也是 0 差异 —— 因为模板里
   没有任何"会被修复影响"的控件。**度量工具与守卫的上限都由语料决定。**
5. **一组破坏只能证明它打算证明的那件事。** NV-1/NV-2 改的是同一个方法的两个部分，
   但分别打红守卫里的两条断言 —— 不做这两组，就不知道第二条断言是活的。

## 6. 本轮环境坑（会重复踩）

本机 `ulimit -v` 硬限 **4 GB**、`ulimit -u` 硬限 **4096**，而桌面应用已占用约 2870 个线程
→ `dotnet build` 反复 `OutOfMemoryException`（报在 MSBuild `Thread.StartCore`）。
可用的跑法（构建成功、全量 2095 全绿都靠它）：

```bash
env DOTNET_PROCESSOR_COUNT=1 DOTNET_gcServer=0 DOTNET_CLI_USE_MSBUILD_SERVER=0 \
  dotnet test <proj>.csproj -m:1 --no-restore \
  -p:UseSharedCompilation=false -p:RunAnalyzers=false
```

- **`DOTNET_PROCESSOR_COUNT=1` 是关键**（线程池规模随之下降，`Thread.StartCore` 不再 OOM）；
- **`--no-restore` 是关键**：restore 阶段加载 `NuGet.Build.Tasks` 需要额外 AS，
  表现为 `MSB4062 … The access code is invalid`（实为 mmap 失败，不是真的"访问码"错）；
- `HmiEditor.App`（Avalonia）单独构建最吃内存，是全链的瓶颈项目。
