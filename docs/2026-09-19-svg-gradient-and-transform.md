# P1-3 SVG 渐变与 transform：解析 → 几何 → CodeGen

> 对应 `docs/2026-09-18-mainstream-gap-check-and-optimization-plan.md` §6 的 P1-3 行。
> 背景事实（§7）：`SvgParser` 原先**跳过**渐变定义与 `transform`，导入含渐变的图标后画布与生成代码都会悄悄丢效果。
>
> 分段：**A 段**（解析/几何/判据，`a72ce8d`）、**B 段**（CodeGen 发射 + 固件开关，本文档主体，`6acc100`）、**C 段**（画布渲染，`94c9837`，见 §7）。

---

## 1. 结论先说

升级 SVG 的渐变与 transform 能升到什么程度，**不取决于编辑器，取决于 LVGL 软件渲染器**。核过固件源码后，可发的真渐变只有一条窄路，其余一律降级成纯色 —— 这不是偷懒，是渲染栈本身写不出来。

| 形状 / 情形 | 处理 | 理由（固件侧证据） |
|---|---|---|
| 轴对齐矩形 + 渐变填充 | **真渐变**（`lv_draw_rect` + `lv_grad_*`） | `lv_draw_sw_fill.c:129` 起 LINEAR/RADIAL/CONICAL 三条都实现了 |
| 圆（或 `rx==ry` 的椭圆）+ 渐变填充 | **真渐变**，发成 `radius = r` 的圆角矩形 | 同上；圆在 LVGL 里本就是圆角矩形 |
| 多边形 / Path + 渐变填充 | 降级纯色 | 填充走三角扇：`lv_draw_sw_triangle.c` 只有 `HOR`(145) / `VER`(158) 两个分支，且色带按**每个三角形自己的包围盒**生成（`color_map[y - tri_area.y1]`）—— 扇形会被拆成 N 份各自重来的渐变，语义本身就错 |
| 任意形状 + 渐变描边 | 降级纯色 | `lv_draw_line_dsc_t` **没有** `grad` 字段 |
| 含旋转 / 斜切的形状 + 渐变 | 降级纯色，矩形提升为多边形 | `lv_area_t` 是轴对齐表达，写不出倾斜矩形 |
| 椭圆（`rx != ry`）+ 径向渐变 | 降级纯色 | SVG 的径向在非均匀包围盒下是椭圆，`lv_grad_radial_init` 只能表达圆 |
| 任意形状 + `transform` | **烘焙进坐标**，不发运行时矩阵 | 两台渲染栈没有等价的矩阵概念 |

降级色取渐变的 **50% 插值**（`SvgGradientGeometry.SampleMidColor`），不是首停靠点 —— 多数图标把主色放中间，取首位会明显偏暗。

---

## 2. 固件侧事实（每条都能指到行）

这些都是**读源码读出来的**，不是按 LVGL 文档推断的：

| 事实 | 出处 |
|---|---|
| `LV_USE_DRAW_SW_COMPLEX_GRADIENTS` 默认 **0** | `lv_conf_internal.h:653` |
| `LV_GRADIENT_MAX_STOPS` 默认 **2** | `lv_conf_internal.h:1584` |
| LINEAR/RADIAL/CONICAL 的 setup/get_line/cleanup 全在 `#if LV_USE_DRAW_SW_COMPLEX_GRADIENTS` 内 | `lv_draw_sw_fill.c:126/181/222/304/329` |
| 该宏为 0 时，`grad_dir >= LV_GRAD_DIR_HOR` 仍成立（`:110`）⇒ 渐变**不报错、不空白**，只是被当成 HOR 一维色带横向画出来 | `lv_draw_sw_fill.c:110-124` |
| `lv_draw_rect_dsc_init` 会 `lv_memzero` 整个 dsc，并预置 `bg_opa = LV_OPA_COVER` 与白→黑两点 | `lv_draw_rect.c:41-58` |
| 四个 `lv_grad_*_init` **只写自己那几个字段**，结构体尾部还有一个 `void * state`（渲染器拿它挂预计算结果） | `lv_grad.c:50-110`、`lv_grad.h:88` |
| `lv_grad_init_stops` 的 `opa` 可传 NULL（填 `LV_OPA_COVER`） | `lv_grad.c:45` |
| 停靠点全部不透明 ⇒ `bg_cover = true`，走不带遮罩的快路 | `lv_draw_rect.c:194-202` |
| `lv_grad_linear_init` 的坐标经 `lv_pct_to_px(v, wdt/hgt)` 换算 ⇒ **必须相对绘制区域** | `lv_draw_sw_grad.c:473-476` |
| `..._get_line` 收到的是 `clipped - bg_coords`（区域相对量） | `lv_draw_sw_fill.c:112` |
| 径向的 `to` 点是「落在结束圆上的一点」，`r_end = \|end_extent - end\|`，且有 `LV_ASSERT(r_end != 0)` | `lv_grad.c:87-88`、`lv_draw_sw_grad.c:294-295` |
| `lv_grad_radial_set_focal(..., radius)` 的 radius=0 ⇒ `r_start = 0`（同心起点），正是 SVG 的 `fx/fy` 语义 | `lv_draw_sw_grad.c:293` |
| `lv_grad_*` 的**声明**不受该宏保护 ⇒ 宏为 0 时名字也存在于头文件，"未知 API"校验不该报 | `lv_grad.h:96-176`（无 `#if` 包裹） |

---

## 3. 三条必须守住的契约（回退即错）

### 3.1 端点相对绘制区域左上角

`lv_draw_sw_grad_linear_setup` 用 `lv_pct_to_px(start.x, wdt)` 把端点换算成区域相对量，而 `get_line` 拿到的也是区域相对量。两端不同源 ⇒ 渐变整体偏移一个区域原点。

发射时是 `g.X1 * sx - areaX`，不是绝对坐标。判据见 `SvgGradientCodeGenTests.LinearGradient_EndpointsAreRelativeToAreaOrigin`：形状从 `(0,0)` 挪到 `(20,10)`，发射出来的数字**必须不变**（改绝对坐标立刻变成 40/140）。

### 3.2 userSpaceOnUse 必须带上元素自己的 transform

SVG 规定 `gradientUnits="userSpaceOnUse"` 的坐标取自**引用它的那个元素的用户坐标系**，而元素自己的 `transform` 正是这个坐标系的一部分。漏掉它 ⇒ 形状移走、渐变留在原地。

`SvgGradientGeometry.Resolve(g, box, userTransform)` 里是 `M = userTransform · gradientTransform`。调用方传的是**烘焙前**的 `Transform` —— 烘焙会把矩阵清成 null，之后再传就没有这个信息了。

`objectBoundingBox` 不受影响：`box` 已经是变换之后的包围盒，跟着盒子走天然就对。

### 3.3 直接写 `dsc.bg_grad`，不要先造局部 `lv_grad_dsc_t`

`lv_draw_rect_dsc_init` 会 `lv_memzero` 整个 dsc；`lv_grad_linear_init` 之类只写 `dir`/`params`/`extend`，`lv_grad_init_stops` 只写 `stops`/`stops_count`。用未初始化的局部量再整体赋值，等于把 `void * state` 这个野指针搬进去。写进已被清零的 `dsc.bg_grad` 则天然是 NULL。

（本版本 LVGL 的 `state` 由 setup 先写后读、`cleanup` 配对，实测不会炸；但"某个字段这一版没人读"不是可以依赖的前提。）

---

## 4. 配置改动：改 `firmware/t113/config/`，不是 SDK 里的 `src-config`

打开 `LV_USE_DRAW_SW_COMPLEX_GRADIENTS 1`。

**这条改动落在 `firmware/t113/config/lv_conf.h`（仓库跟踪）。** 之前顺手改的 `firmware/Tina-Linux/package/hmi-runtime/src-config/lv_conf.h` 是**构建产物**，已改回：

- `firmware/t113/tina/build.sh:101-104` —— `deploy_package()` 每次构建先 `rm -rf "$dst/src-config"`，再从 `firmware/t113/config/.` 拷进去；
- `.gitignore:33/34` —— 整个 `firmware/Tina-Linux/`、`firmware/Tina-SDK/` 被忽略，改在那里既不入库、也活不过下一次构建；
- 两份内容本就不同（`t113/config` 是 WavePI-S6 板级档，`LV_MEM_SIZE 8MB`；SDK 那份是 16MB），不是同源副本。

---

## 5. 验证记录

> 引用任何一条"绿"之前先对时序：源文件 mtime vs 日志 mtime。

### 5.1 单元测试

| 项目 | 读数 | 日志 / 时间点 |
|---|---|---|
| Core（Debug，全量） | 2041 通过 / 0 失败 | 基线 2022 + 新增 19 |
| 门禁 Release：Core 大队 | **2033** 通过 | `.workbuddy/tmp/ci-gate-2026-09-19-svg-phaseB.log` |
| 门禁 Release：Core git 用例单独跑 | **8** 通过 | 同上（2033 + 8 = 2041，与 Debug 一致） |
| 门禁 Release：App.UiTests / AI / Device / Communication / Simulator / Hardware | 249 / 315 / 87 / 75 / 44 / 44 | 同上 |
| 门禁 | **7 步全过，`GATE_EXIT=0`** | 日志 mtime `2026-09-19 20:20:54` |

源文件 mtime：`SvgGradientGeometry.cs` 20:16:13、`SvgToLvglConverter.cs` 20:16:45、`GeneratedCodeValidator.cs` 20:13:07、`SvgGradientCodeGenTests.cs` 20:18:15 —— 全部早于日志 mtime，绿确实覆盖了改动。

### 5.2 负向控制（3 组，全部按预期精准命中，无附带）

| # | 故意改错 | 结果 |
|---|---|---|
| ① | `SvgGradientGeometry.Resolve` 忽略 `userTransform` | 恰好 1 红：`UserSpaceGradient_InTranslatedGroup_MovesWithShape`（73 通过 / 74） |
| ② | 渐变端点改画布绝对坐标（去掉 `- areaX/- areaY`） | 恰好 3 红：`...EndpointsAreRelativeToAreaOrigin`、`...OnCircle_EmitsRoundRectWithRadius`、`UserSpaceGradient_InTranslatedGroup_MovesWithShape` —— 与预测一致 |
| ③ | `TransformScale` 恒返回 1.0 | 恰好 1 红：`ScaledTransform_ScalesStrokeWidth` |

改动全部还原，全库 `grep NEGATIVE-CONTROL` 无残留。

### 5.3 `lv_conf.h` 的探针（门禁覆盖不到，只能这么证）

**门禁第 7 步不校验这份配置的内容。** `run_host_tests.sh` 的 include 路径里确实有 `-Iconfig`（即 `firmware/t113/config`），但这些 host 测试刻意不拉 `lvgl.h`（见 `hmi_log.h` 独立成文件的原因），所以没有任何一步会真去解析那份 `lv_conf.h`。

探针已固化成 **`firmware/t113/tests/check_lv_conf_flags.sh`**，由 `run_host_tests.sh` 的第 **[9/9]** 步调用（所以 `ci-gate.sh` 仍是 7 步 —— 第 [7] 步内部多了一段）。分两层：

| 层 | 判据 | 外部条件 |
|---|---|---|
| **[A] 文本守卫** | `config/lv_conf.h` 里 `#define LV_USE_DRAW_SW_COMPLEX_GRADIENTS 1` **恰好一处**、全文无 `#undef`；且 `LV_GRADIENT_MAX_STOPS`（未定义则按 LVGL 默认 2）必须等于 C# 侧 `SvgGradientGeometry.MaxStops` | 无，永远跑 |
| **[B] 预处理探针** | 用真 LVGL 头链译一个 `#if … #error` 的 TU，证宏**真被 LVGL 采纳**、且 `LV_GRADIENT_MAX_STOPS == MaxStops` | 需要 LVGL 源码。`host_test/lvgl` 与 `Tina-*/package/gui/lvgl` **都是 .gitignore 的本机副本**（`.gitignore:43` / `:33,34`），新克隆上没有 ⇒ 此时**明确打印「未验证」**，CI 可设 `CI_REQUIRE_FIRMWARE_TESTS=1` 把它升级为失败 |

**[B] 的 include 顺序本身就是判据的一部分**：`-Iconfig` 必须排在 `-I<lvgl>` **前面**。LVGL 目录自己带一份 `lv_conf.h`，且那份里该项**本来也就是 1** ⇒ 顺序写反时 `#include "lv_conf.h"` 命中那一份，正控照样"通过"、负控也不报错，整个探针退化成**恒真的假判据**。

读数（`bash firmware/t113/tests/run_host_tests.sh`，日志 `.workbuddy/tmp/host-tests-2026-09-19-lvconf.log`，mtime `20:35:11`，`rc=0`，9 步全过）：

| 情形 | [A] | [B] | 结论 |
|---|---|---|---|
| 正控（现状） | ✅ | ✅ 三条头链（`host_test/lvgl`、Tina-Linux、Tina-SDK）全过 | 宏 = 1，`MAX_STOPS` = 2 |
| 负控 ①：抠掉那一行 | ❌「恰好一处…实测 0 处」 | 未跑到 | 文本层有效 |
| 负控 ②：把该行包进 `#if 0` | **✅ 放过** | ❌ `#error tracked-config-lacks-flag` | **两层各自独立**，[B] 不是摆设 |
| 负控 ③：`LV_GRADIENT_MAX_STOPS 4` | ❌「与 `MaxStops=2` 不一致」 | 未跑到 | 跨语言同源判据有效 |

> 负控 ② 是唯一"只被 [B] 抓住"的构造：[A] 用的是**行首锚定**匹配（注释行以 ` * ` 或 `// ` 开头，永远命中不了 `^[[:space:]]*#`，所以不必先剥注释），但 `#if 0` 里的 `#define` 仍然从行首开始 ⇒ [A] 放过、只有预处理器知道它是死的。这正好界定两层的职责边界。
>
> **方法论注**：第一版手工探针两次都拿到 `rc=0`，表面看"正负控都成立"。真相是 ① `-I` 顺序写反（量到的是 LVGL 自带那份配置）；② `gcc … | tail` 取到的是 `tail` 的退出码。**负控给出"预期中的绿"时，先怀疑判据本身没在判** —— 这次两坑叠在一起，只看读数发现不了。

### 5.4 顺手被守卫抓到的一处

新测试初稿用了 `Assert.Contains("lv_area_t a = {40, 20, 140, 70};", code)` 这类写法，被 `TestAssertionStyleGuardTests.NoExactCallSubstringAssertionsOutsideAllowList` 判红（判据：字面量里同时含 `(` 与 `,` 就算"疑似精确调用文本"）。已全部改走 `CSource.AssertCode`。**_这条守卫替项目挡掉了一次真实回归风险，不是摆设。_**

---

## 6. 局限（已登记，勿当成 bug）

1. **`LV_GRADIENT_MAX_STOPS` 保持 2**（默认值）。超过 2 个停靠点时取**首尾**两点，不做重采样 —— 中间色会被丢掉。抬这个宏会让 `lv_grad_dsc_t` 每个停靠点多 `sizeof(lv_color_t)+1` 字节，`SvgGradientGeometry.MaxStops` 必须同步。
2. **椭圆径向按 x 方向近似**：`lv_grad_radial_init` 只能表达圆。非均匀包围盒的椭圆径向已按上表降级，但 `objectBoundingBox` 下非正方形包围盒的**矩形**径向仍是按 x 方向取半径。
3. ~~**C 段未开工**~~ —— ✅ **已闭环（2026-09-19 晚，`94c9837`）**，见 §7。它收掉的正是**两处方向性偏差**：① 轴对齐矩形与圆：设备发真渐变、画布原先只画 `Fill` 里那份 **50% 降级纯色**（`DesignCanvas.IndustrialRenderers.cs` 的 `RenderSvg` 只读 `Fill`/`Stroke`，整个 `src/HmiEditor.App/` 里 `FillGradient`/`StrokeGradient` **0 次引用**）；② 含 `transform` 的形状：设备已把 transform **烘进坐标**，画布原先**根本没应用**（`shape.Transform` 在 App 层 0 次引用）⇒ 位置与形状都不对；这是 A 段之后**新引入**的分叉（A 段之前两边一致地忽略 transform）。注意 ① 的方向是**画布更差**，不是反过来。

P1-3 的验收条件"含渐变 SVG 导入后画布与生成代码一致"**已按"绘制指令级"达成**，像素级仍未证（见 §7.4）。
4. ~~**门禁缺一步**~~ —— ✅ **已闭环（2026-09-19 晚）**：探针固化成 `firmware/t113/tests/check_lv_conf_flags.sh`，作为 `run_host_tests.sh` 的第 **[9/9]** 步（即 `ci-gate.sh` 第 [7] 步内部）。两层判据 + 3 组负控，详见 §5.3。**注意它在"新克隆/无 LVGL 源码"的机器上只能跑文本层**，探针层会明确报「未验证」。

---

## 7. C 段：画布渲染（`94c9837`）

本段的重点**不是**"给画布接个渐变刷"，而是**先把烘焙提取成唯一实现，再让画布消费同一份几何**。

生成侧的 `BakeTransform` / `TransformScale` / `TransformPoints` 原本是 `SvgToLvglConverter` 的 `private`。画布要用只有两条路：自己再写一份（⇒ 又一次"画布是斜的、设备是正的"），或者提取。选后者。

| 新增 `src/HmiEditor.Core/Services/SvgShapeBake.cs` | 由来 |
|---|---|
| `Transform(shape)` —— 把仿射烘进几何 | 原 `SvgToLvglConverter.BakeTransform` |
| `ApplyToPoints(pts, m)` | 原 `TransformPoints` |
| `Scale(m)` = √\|det\|（描边宽度因子） | 原 `TransformScale` |
| `SampleEllipse(...)`（32 段） | 原本是 `SvgToLvglConverter` 与 `ShapeControlCodeGen` **两份逐字相同**的私有副本 |

`SvgToLvglConverter` 604 → 465 行，行为不变（生成侧 103 条相关用例全绿）。

### 7.1 画布侧的判据点（全部取自 Core，不重实现）

| 点 | 用什么 | 为什么不能自己写 |
|---|---|---|
| 该不该画真渐变 | `SvgGradientGeometry.Decide` | 同一判据若分叉，连"该不该降级"都会两边不一致 |
| 端点 / 半径 | `Resolve(g, BoundingBox(烘焙后), 烘焙前的 Transform)` | 包围盒映射、gradientTransform 叠加顺序、userSpaceOnUse 三条语义都在里面 |
| 停靠点 | `NormalizeStops`，**< 2 个直接退回纯色** | 对应 C 侧 `lv_grad_init_stops` 的 `LV_ASSERT(num_stops > 1)` |
| 几何 | `SvgShapeBake.Transform` + Path 展平后 `ApplyToPoints` | 与设备同一份几何 |

两条最容易写错的：

- **判据必须在烘焙前判**：`SvgShapeBake.Transform` 会把 `Transform` 清成 null，之后再 `Decide` 就永远得到"轴对齐"，含旋转的矩形会被错误地按渐变画出来。
- **径向按单一半径建刷**（`RadiusX == RadiusY == Radius * sx`）：`lv_grad_radial_init` 只能表达圆、半径取 x 方向；画布若按 `sy` 另取一个，非正方形控件上当场不一致。

### 7.2 坐标空间（这条最容易静默画错）

画刷坐标必须与 `BuildShapeGeometry` 造出的几何点**同处一个空间**。

`RenderSvg` 是在 `context.PushTransform(Matrix.CreateTranslation(rect.X, rect.Y))` 里画的，而 `PushTransform` 在录制/合成里表现为**父级 `DrawingGroup.Transform`**、子节点坐标仍是局部的 ⇒ 画刷跟着几何一起被父级搬运，所以**不能**再加 `rect.X/rect.Y`（加了就是偏移一个控件原点）。

另：Avalonia 没有 WPF 那个 `MappingMode`，绝对/相对由 `RelativePoint` / `RelativeScalar` 的 `RelativeUnit` 表达。

### 7.3 验证

新增 `tests/HmiEditor.App.UiTests/SvgGradientCanvasTests.cs` **6 条**，走真实 `DesignCanvas.Render` + `CanvasRenderSpy` 录制绘制操作，断言**画刷类型 / 端点 / 几何包围盒**（不是读源码 grep）：线性（含坐标空间与两个停靠点颜色）、含旋转 ⇒ 降级纯色、描边渐变 ⇒ 恒纯色、`<g transform>` 矩形几何被搬走、`<g transform>` 里 Path 也被搬走、径向单一半径。

**负向验证 4 组，全部按预测精确命中、无附带**：

| # | 注入 | 红 |
|---|---|---|
| ① | 画布忽略渐变（恒纯色） | **2**（线性 + 径向两条） |
| ② | 不做仿射烘焙（`effective = 原形状`） | **1**（`GroupTransform_IsBakedIntoGeometry`） |
| ③ | Path 不应用仿射 | **1**（`GroupTransform_OnPath_IsAppliedAfterFlattening`） |
| ④ | 渐变端点多加一个控件原点偏移 | **1**（`Linear…InGeometrySpace`） |

④ 专门盯 §7.2 那条空间约定：往端点里注入"+控件原点"，**只有那一条**测试会红。注入用带 `assert(count == 1)` 的改写工具做，还原后与备份**逐字节一致**。

门禁：**7 步全绿、`GATE_EXIT=0`**，日志 `.workbuddy/tmp/ci-gate-2026-09-19-svg-phaseC.log`（mtime `2026-09-19 21:41:54`，晚于提交 `94c9837`）。构建 0 警告 0 错误；**App.UiTests 249 → 255**（+6 即本段新增）；Core 大队 2033 + git 用例 8 = 2041；七程序集合计 **2861**。

### 7.4 C 段自身的局限

1. ~~**本组测试是"绘制操作级"、不是像素级**~~ —— ✅ **已补（2026-09-19 晚，阶段 35）**：像素级判据落在新程序集 `tests/HmiEditor.RenderTests`，见 **§7.5**。本组（指令级）**保留**并继续作为主力：它快、稳、能逐项断言画刷类型与坐标。
2. 画布每帧为每个含渐变的形状新建 `LinearGradientBrush` / `RadialGradientBrush`（**不进** `RenderResourceCache`：端点随控件尺寸与缩放变化，缓存命中率低且易错）。分配开销未量化；`CanvasRenderBaselineTests` 里的画布不含渐变形状，故其基线未受影响。
3. 设备侧本身仍未实机验证（见 §6）。

---

## 7.5 像素级：把 §7.4 第 1 条补上（2026-09-19 晚，阶段 35）

补它的过程中**四条路里前三条是死的**，记下来省一次重走：

| # | 试法 | 结果 |
|---|---|---|
| ① | 现有宿主里直接 `RenderTargetBitmap.CopyPixels` | **无像素**：读回的是未初始化内存（`PixelFormat = Rgba8888`、前 16 字节 `12-C0-E4-F8-07-…` 重复、非零字节 648/1024 全是噪声，红色一个字节都没有） |
| ② | 另开一个 Skia 会话（不动现有宿主） | **起不来**：`AvaloniaHeadlessPlatform.Initialize → new Compositor → DefaultRenderLoop.Add` 抛 `InvalidOperationException: The calling thread cannot access this object` —— 渲染循环是**进程级静态单例**、绑死在第一个会话的 UI 线程上，同进程开不出第二套平台配置（**2026-09-21 更正**：这句的**机制描述与 Avalonia 12.1.0 的 IL 不符** —— `DefaultRenderLoop` 没有任何静态字段，`Add` 是当场读 `Dispatcher.UIThread`；线程归属判定来自 `Dispatcher._thread` 与 `s_uiThread`。**结论不变、理由换成**：「平台配置是进程级的、同进程换不了第二套」＋ `Dispatcher.s_uiThread` 的「谁先构造谁拿走」窗口；逐条 IL 证据见 `docs/2026-09-21-dockpanel-invocation-net.md` §8 的 #47） |
| ③ | 把现有宿主整体切 Skia | **既有用例成片红**：`PropertyPanelWiringTests` 撞上同一个线程亲和问题；`ViewSmokeUiTests` 报 `Could not create glyphTypeface. Font family: $Default`（no-op 绘制下看不见字体缺失）；执行被中断（257 条只跑了 104 条）⇒ 回退 |
| ④ | **单开一个测试程序集** | ✅ **可行** —— VSTest 为每个程序集起独立 testhost **进程**，天然拿到干净的 `DefaultRenderLoop`（**2026-09-21 更正**：「干净的 `DefaultRenderLoop`」这个说法基于上面那句已被推翻的机制。**结论不变、理由换成**：「平台配置是进程级的、同进程换不了第二套」＋ `Dispatcher.s_uiThread` 的「谁先构造谁拿走」窗口；逐条 IL 证据见 `docs/2026-09-21-dockpanel-invocation-net.md` §8 的 #47） |

于是新增 `tests/HmiEditor.RenderTests`（`.slnx` 里第 **8** 个测试项目）：`UseSkia()` + `UseHeadlessDrawing = false` + `WithInterFont()`。
**门禁不需要改** —— 第 [4] 步是全量 `dotnet test HmiEditor.slnx`，新程序集自动被收进去。

### 判据怎么设计才不绑死实现

像素断言最容易写成"断言精确色值"，而精确色值取决于 **Skia 在 sRGB 还是线性空间插值**（换个版本就可能变）。所以三条判据都做成**实现无关**的：

| 判据 | 断言什么 | 为什么这样选 |
|---|---|---|
| 线性 | 左端≈红 / 右端≈蓝（容差 40）；取样 5 点，R 逐点递减且 B 逐点递增；**中点 R≈B** | 方向 + 单调 + 中点等式。红→蓝渐变的中点**在任何插值空间下 R 都等于 B**（sRGB 空间是 128/128，线性空间是 188/188）—— 这是整套里唯一一条与实现无关的绝对判据 |
| 径向 | 圆心 R>230、距圆心 90 处 R<60 | 单调性，同样不依赖插值细节 |
| 旋转降级 | 形状内 3 个点**完全一致**（±3），且 R≈B、G≈0 | "整块均匀"就是降级的定义；只要画布画了渐变，均匀性立刻破 |

外加一条**元判据** `HostCapabilityTests.Rasterization_ProducesRealPixels`（填纯红、读回必须是红）：`UseHeadlessDrawing = true` 时读回的是噪声，那种情况下像素断言要么偶然通过、要么随机失败 —— **比没有判据更糟**。谁把后端配置改回 no-op，这条必须先红。

### 负向验证 3 组（各自恰好命中对应那一条、无附带）

| # | 注入 | 红 |
|---|---|---|
| ① | 交换渐变起止端点 | **1** —— `LinearGradient_PixelsRunMonotonicallyFromRedToBlue` |
| ② | 不判降级（旋转形状也画真渐变） | **1** —— `RotatedShape_FallsBackToUniformSolidColor` |
| ③ | 径向半径翻倍 | **1** —— `RadialGradient_CenterIsBrighterThanEdge` |

三组互不干扰 ⇒ 三条判据**各自在判**，不是"一条红带着全红"。注入用带"恰好命中一次"断言的脚本做，还原后 sha256 与备份**逐字节一致**。

**读数**：门禁 **7 步全绿、`GATE_EXIT=0`**，日志 `.workbuddy/tmp/ci-gate-2026-09-19-pixel.log`（mtime `2026-09-19 23:53:21`）；构建 **0 警告 / 0 错误**；**八程序集合计 2865**（上轮 2861 + 本程序集 4 条）；固件 host 9 步全过。

### 仍未闭环

- **设备侧（LVGL 真实产出）依旧无实机验证**（§6）："画布所见 == 设备所得"里，本组补的是**画布那一半**，**设备那一半**仍然没有证据。
- 像素判据只覆盖「线性 / 径向 / 降级」三态，`<g transform>` 的像素位置仍留在指令级那一组。

