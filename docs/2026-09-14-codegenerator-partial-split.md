# CodeGenerator 按域拆 partial（2026-09-14）

## 1. 背景

`src/HmiEditor.CodeGen/CodeGenerator.cs` 是 **3380 行、零 partial、零 region** 的单文件。它是设计器 → LVGL C 代码的**唯一出口**：主题、屏幕、控件、动作、动画、报警、趋势、绑定刷新、构建系统全部挤在一个类里。

| 指标 | 拆分前 |
|---|---:|
| 行数 | 3380 |
| 顶层成员数 | 80（含字段/属性/表达式体成员） |
| partial 分部 | 0 |
| `#region` | 0 |

这是 P2「三层重复」主线之外的第二类结构债：前三层（实现重复 → 抽共享类型；调用点分散 → 让「算一次」成为类型约束；字面量散落 → 抽声明发射器）解决的是**语义散落**，这一次解决的是**物理散落**。

## 2. 前置纪律：先盘点覆盖，再动刀

拆文件是纯机械操作，但编译器只能发现「符号丢失」，发现不了「发射语义变了」。所以第一步不是拆，是**盘点生成器自身的发射路径覆盖**。

**方法（API 锚点法）**：不从函数调用点判断覆盖（大量是内部调用），而是从每个顶层成员体内抽 **API 名**（`lv_*` / `hmi_*` / `sim_*` / `ft_*` / `LV_*` / `create_screen_*`），再看测试语料里是否出现。某成员发出的 API 在测试里一个都不出现 → 该路径零覆盖。

> 方法教训：v1 用「成员体内的字符串字面量」做锚点，抽出来的是**带 `{...}` 插值占位的模板文本**（如 `lv_obj_align(_ticklbl_{control.Id}_{i}, ...`），而测试断言的是**渲染后**产物（`lv_obj_align(_ticklbl_gauge_1_0, ...`），必然匹配不上 → 满屏假「全未命中」。改用 API 名后信噪比才可用。

盘出的真缺口集中在**条件分支**（某配置字段非空/大于零才发射）——发射器本身全部可达，是「唯一有分支的逻辑没被测过」：

| 零覆盖的发射路径（抽样） |
|---|
| `lv_label_set_long_mode`、`lv_spinner_create`/`set_anim_params`、`lv_tabview_add_tab`/`set_active` |
| `lv_dropdown_set_selected`、`lv_bar_set_mode`、`lv_slider_set_mode`、`lv_table_set_row_count` |
| `lv_arc_set_range`、`lv_obj_set_style_arc_opa`、`lv_textarea_set_max_length`/`password_mode` |
| `LV_IMAGE_ALIGN_COVER`、`lv_anim_set_path_cb`/`set_var`/`set_repeat_count` |
| `LV_CHART_TYPE_LINE`、`LV_CHART_AXIS_PRIMARY_X`/`PRIMARY_Y` |

据此补了 **117 项行为测试**（`ControlEmitterCoverageTests` 76 项 + `AnimationAndTrendEmitterTests` 41 项），并做了 **11 条单点破坏负向验证**（每条都能让 1 项以上测试变红）。

负向验证同时暴露/证实了两处事实：

1. **两份 `GetPathCCode` 同体实现** —— `AnimationConfig.GetPathCCode()`（全局路径用它）与 `CodeGenerator.GetPathCCode(AnimationPathType)`（关键帧注释路径用它）逐字相同。NV-8/NV-11 是一对对称验证，证明两份各自都被覆盖，将来统一时有网。
2. **`LV_IMAGE_ALIGN_COVER` 是死代码** —— 守卫条件里含该常量，但 `StretchModeKind` 只有 `Stretch`/`Contain`/`Center`/`Tile`，`StretchModeMap.Parse("cover")` 落默认 `Stretch`，故永不返回 `LV_IMAGE_ALIGN_COVER`。无 UI 暴露 Cover 选项，故**非用户可见缺陷**，属无害死代码。

## 3. 成员盘点：先换掉不可靠的扫描器

拆之前必须先拿到**准确的成员边界**。第一版扫描器（「缩进 4 且带块体」）漏掉了三类成员：

| 漏判类型 | 例子 | 漏判原因 |
|---|---|---|
| 自动属性 | `internal static string GeneratorStamp { get; } = BuildGeneratorStamp();` | 行尾是 `;` 不是 `{` |
| 单行表达式体成员 | `private static string Foo() => "x";` | 同深度立即闭合，无块体 |
| 特性行开头的成员 | `[Obsolete]` + 声明 | 起始行不以声明开头 |

改用**大括号深度状态机**（屏蔽注释/字符串/字符字面量里的 `{}`）后，成员数从错误的 65 收敛到真正的 **80**。扫描器自带三条断言，全部通过：

- 全文扫完后深度必须归 0
- 成员区间两两不重叠且有序
- 除类声明行/空行/类闭合括号外，**没有一行代码不被任何成员区间覆盖**

第二处坑：`wc -l` 报 3380，但文件以 `}\n\n` 结尾 —— **类闭合括号在第 3379 行**，3380 是尾部空行。零丢失校验器最初就栽在这个 off-by-one 上（把类闭合括号算进了「类体内非空行」，多出 1 行）。

## 4. 拆分结果

| 文件 | 行数 | 原区间 | 成员数 | 职责 |
|---|---:|---|---:|---|
| `CodeGenerator.cs` | 150 | 1–21 + 22–37 + 39–71 + 224–300 | 9 | 字段/属性/构造/`GeneratorStamp`、`GenerateFullProject`、`BuildFullProjectMain` |
| `CodeGenerator.Simulator.cs` | 171 | 73–222 | 1 | 模拟器（PC/SDL 导出）模块发射 |
| `CodeGenerator.SharedLibrary.cs` | 323 | 302–603 | 1 | 动态库（.so）模式发射 |
| `CodeGenerator.Theme.cs` | 69 | 605–652 | 1 | 主题样式初始化发射 |
| `CodeGenerator.RefreshBindings.cs` | 312 | 654–665 + 667–682 + 684–944 | 3 | 绑定值刷新（模拟器侧 / 设备侧 / 共享实现） |
| `CodeGenerator.Animation.cs` | 97 | 946–1021 | 1 | 动画发射（`lv_anim_t` + `apply_animations()`） |
| `CodeGenerator.Alarm.cs` | 282 | 1023–1051 + 1053–1283 | 2 | 报警分组聚合 + 报警管理代码 |
| `CodeGenerator.Trend.cs` | 242 | 1285–1505 | 1 | 趋势图与数据记录发射 |
| `CodeGenerator.Screen.cs` | 451 | 1507–1829 + 1831–1840 + 1842–1884 + 1886–1895 + 1897–1936 | 5 | 单屏创建、顶层包装、图层引用表、图层校验、自适应布局 |
| `CodeGenerator.Actions.cs` | 290 | 1985–2253 | 7 | 事件回调、动作体、标签读写/取反、事件注册、变换 |
| `CodeGenerator.ControlParameters.cs` | 560 | 2255–2793 | 16 | `GenerateControlParameters` 分发 + 15 个 `Emit*Parameters` |
| `CodeGenerator.Emitting.cs` | 98 | 1939–1982 + 2795–2826 | 7 | 浮点格式化、注释追加、布尔判定、绑定文本语句、C 字符串/格式串/注释转义 |
| `CodeGenerator.VisualStates.cs` | 103 | 2828–2909 | 1 | 状态化样式覆盖发射 |
| `CodeGenerator.Mappings.cs` | 158 | 2911–2980 + 3153–3218 | 8 | 枚举/值 → LVGL 常量映射与查询 |
| `CodeGenerator.SliderBarIndicators.cs` | 191 | 2982–3151 | 7 | 刻度数与刻度发射、knob/游标小三角、指针声明与值回调 |
| `CodeGenerator.BuildSystem.cs` | 180 | 3220–3378 | 10 | `BuildArtifact`、CMake/Makefile/Platform.ini、名称净化、编译开关宏 |

最大文件从 **3380 → 560 行**（`ControlParameters`），认知负荷降到约 1/6。

**分域依据**：
- `ControlParameters` 单独成域 —— 按控件族拆出的 15 个 `Emit*Parameters` 是天然聚合，占原文件 16%。
- `Emitting` 与 `Mappings` 分家 —— 前者是**产文本**的工具（转义/格式化/注释），后者是**查映射**的纯函数（枚举值 → 常量）。两者都是被广泛引用的叶子，混在一起会让「谁依赖谁」变模糊。
- `SliderBarIndicators` 独立 —— 刻度与指针三角共用 `AppendTriangle`（canvas 绘制），且都依赖 `_soMode` 选择字体，是一个内聚子系统。
- `Screen` 域收 `GenerateAdaptiveLayout` —— 它按 `display` 实际分辨率重算屏幕内控件布局，语义上属于屏幕而非工具。

## 5. 零丢失验证

### 5.1 行级审计（切分脚本自带）

| 项 | 行数 |
|---|---:|
| 原样搬走（逐字符原封不动） | 3355 |
| 改写 | 1（`public class CodeGenerator` → `public partial class CodeGenerator`） |
| 丢弃 | 24（23 处成员间空行 + 1 处类闭合括号，各分部自带 `}`） |
| **合计** | **3380** ✓ |

两处段落横幅（`// ── M4: 动作代码生成 ──`、`// ── 构建系统生成（…）──`）**正好落在域边界上**，跟随其后的成员一起搬。

### 5.2 独立校验器（只读磁盘，不复用切分脚本状态）

| 校验项 | 结果 |
|---|---|
| 非空行多重集完全相等（类体内 3081 行） | ✓ 无丢失、无重复 |
| 16 个分部大括号收支平衡（屏蔽字符串/注释后计数） | ✓ |
| 16 个分部体均为原文件的有序子序列（成员顺序/内容未被改写） | ✓ |

> 大括号计数必须先屏蔽字符串字面量 —— C 模板串里满是 `{{`/`}}`，朴素 `count('{') - count('}')` 一律报错。

### 5.3 产物逐字节等价（真正的行为证明）

写临时遥测 harness 驱动 **445 个用例**的全入口语料：

- **控件属性扫描**：`HmiControl` 每个可写属性 × 每个候选取值 → 一张含**全部 23 种控件类型**的屏幕（枚举取全量值，字符串走语义候选表如 `StretchMode` 的 `Fill/Contain/Center/Tile/Cover/None/''`）
- **屏幕属性扫描**：`HmiScreen` 每个可写属性 × 取值
- **项目级入口**：`OutputMode` × `BuildSystem` 扫描 + `CodeGenOptimization` × `OperationMode` × 防篡改 × 注释 扫描，每个组合跑 **11 个入口** —— `GenerateFullProject` / `GenerateSimulatorModule` / `GenerateSharedLibraryProject` / `GenerateThemeCode` / `GenerateLayerRefsTable` / `GenerateAlarmCode` / `MergeVariableAlarmGroups` / `GenerateTrendCode` / `GenerateAnimationCode` / `GenerateBuildSystem` / 每屏的 `GenerateScreenCode` + `GenerateScreenTopLevelWrapper`

富项目含：3 屏（含 1 个公共图层被引用）、23 种控件全量且带视觉状态/多动作/变换/绑定、7 种缓动路径动画 + 关键帧动画、2 组报警（含含 `*/` 与换行的恶意描述）、2 条趋势（含停用笔）、4 种 `LogFullPolicy` 数据记录器。

协议：**异常也当数据**（捕获为 `<EXCEPTION>类型: 消息`），保证「抛不抛、抛什么」一并纳入等价判定。

| 比对 | 结果 |
|---|---|
| 拆分版连跑两次（A vs B） | ✓ 一致 → 掩码已彻底消除非确定性 |
| HEAD 版 vs 拆分版（445 用例 sha256 清单 + 400 份全文本逐字节） | ✓ **完全一致** |

### 5.4 顺带发现的既有缺陷：导出产物不可复现

首次比对时 445 个用例里 **56 个项目级用例全部差 4 字节**，差异全在 `sim_refresh_bindings` 的模拟正弦相位：

```
-  float v = 50.0f + 45.0f * sinf(t + 157.0f * 0.017453f);
+  float v = 50.0f + 45.0f * sinf(t + 30.0f * 0.017453f);
```

根因在 `GenerateRefreshBindings`（两处，`CodeGenerator.RefreshBindings.cs:165` / `:289`）：

```csharp
var phase = Math.Abs(ctrl.Id.GetHashCode()) % 360;
```

**`.NET` 的 `string.GetHashCode()` 每进程随机加盐**，所以相位每次运行都变。决定性实验：拿**同一份 HEAD 源码连跑两次**，同样 20 行相位值不同（长度差 4 字节是因为相位位数 2↔3 抖动）→ **既有缺陷，与本次拆分无关**。

影响：导出代码**不满足「同源同物」**，破坏可复现构建、产物哈希比对、基于 diff 的回归门禁。已登记为独立议题（不在本次范围内）。为了完成等价校验，harness 在比对前把 `t + <digits>.0f` 掩成 `t + <PHASE>.0f`，其余字段仍要求逐字节相等 —— 掩码完整性由「同代码两次运行必须一致」这条对照自证。

## 6. 验证结果

| 验证项 | 结果 |
|---|---|
| `dotnet build src/HmiEditor.CodeGen` | **0 警告 0 错误** |
| `HmiEditor.Core.Tests` | **1489 项全绿**（基线 1372 + 本次新增 117） |
| `HmiEditor.AI.Tests` | 237 项 |
| `HmiEditor.App.UiTests` | 91 项 |
| `Hmi.Communication.Tests` | 75 项 |
| `HmiEditor.Device.Tests` | 63 项 |
| `HmiEditor.Simulator.Tests` | 41 项 |
| `Hmi.Hardware.Tests` | 22 项 |
| **合计** | **2018 项 0 失败** |

> 全量首跑 `Core.Tests` 出现 1 项失败，随后**连跑 3 次全绿（1489/1489）** → 时序 flaky，与本改动无关（同类计时器 flaky 见 `docs/2026-09-14-test-timer-flakiness.md`）。

**跨分部私有成员访问已验证**：`_templatesPath` / `_includeComments` / `_soMode` 被 4 个分部引用（`ControlParameters.cs:292`、`Mappings.cs:136`、`SharedLibrary.cs:33`、`Theme.cs:42` 等），partial 属同一个类，编译通过即证明可见性无误。

## 7. 格式约定

- 各分部文件头**原样复制完整 `using` 块**（不改内容 → diff 可审计）。`.editorconfig` 里 `IDE0005 = suggestion`，未使用的 using 不告警。
- 各分部文件头为 `namespace HmiEditor.CodeGen;` + `/// <summary>CodeGenerator 分部 —— <域说明>。</summary>` + `public partial class CodeGenerator`。
- 主分部保留原类文档注释；`public class` → `public partial class`。
- 切分点**全部落在成员之间的空行/注释处**，无一处落在成员内部；80 个成员的边界逐条 dump 核对过。

## 8. 后续（登记未开工）

| # | 议题 | 性质 |
|---|---|---|
| 1 | 统一两份 `GetPathCCode`（`AnimationConfig` 与 `CodeGenerator` 各一份同体实现） | 语义重复，已有对称测试网 |
| 2 | 修 `sim_refresh_bindings` 相位的 `GetHashCode()` 依赖（改用稳定哈希或按索引推导） | **导出不可复现**，见 §5.4 |
| 3 | 清理 `LV_IMAGE_ALIGN_COVER` 不可达死代码 | 无害，独立议题 |
| 4 | `VariableSimulator` 注入 `TimeProvider`（`Simulator_ClockMultiplier_*` 断言墙上时钟，轮询救不了） | 测试 flaky 根因 |

本次只做结构搬家，**未改任何发射语义**：所有成员体逐字符原封不动，产物在 445 用例上逐字节等价。
