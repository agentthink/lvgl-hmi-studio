# 弧线角度约定：设计器与 LVGL 曾竖直镜像（2026-09-14 已修复）

触发原因：给 `RenderResourceCacheTests` 补弧几何断言时，发现 `ArcTo` 版与 LVGL 折线版的包围盒差了 30%（70.71 vs 99.97），顺藤摸出约定不一致。

---

## 结论

| 渲染点 | 修复前 | 修复后 |
|---|---|---|
| `ArcLayout`（常量 + 映射） | 注释写「逆时针为正」（**错**） | 统一为 LVGL 约定：0°=3 点钟、**顺时针为正**，映射集中在 `ArcLayout.PointAt` |
| **Arc 控件**（`RenderArcShape` → 折线弧） | `cy − r·sin` → **竖直镜像** | `cy + r·sin` → 与固件一致 |
| **Knob 指针** | `PointerStart = −135` → 起点在 10:30，比固件偏 90° | `PointerStart = StartAngle = 135` → 起点 7:30，与 `lv_arc` 指示弧重合 |
| **Gauge**（`DrawArcGeometry` + `GaugeTickLayout`） | 本来就对（`+sin`） | 不变（改为统一走 `PointAt`） |
| `RenderSpinner` | 本来就对（`+sin`） | 不变 |

**一句话：`DesignCanvas` 里曾同时存在两套互相镜像的弧角度约定，且两套的注释都声称自己是"LVGL 那一套"。现在统一到 `ArcLayout.PointAt` 一处。**

---

## 证据

### 1. LVGL v9 是「顺时针为正」（源码铁证）

`firmware/Tina-Linux/build/hmi-runtime/src/lvgl/src/misc/lv_math.c:52`

```c
int32_t lv_trigo_sin(int16_t angle)
{
    ...
    if(angle < 90)                          ret =  sin0_90_table[angle];
    else if(angle >= 90  && angle < 180)  { angle = 180 - angle; ret =  sin0_90_table[angle]; }
    else if(angle >= 180 && angle < 270)  { angle = angle - 180; ret = -sin0_90_table[angle]; }
    else /*angle >= 270*/                 { angle = 360 - angle; ret = -sin0_90_table[angle]; }
```
→ `lv_trigo_sin(90) = +32767`、`lv_trigo_sin(270) = −32768`。

`.../src/draw/lv_draw_arc.c:115`

```c
area->y1 = y + ((lv_trigo_sin(start_angle_int)       * rin) >> LV_TRIGO_SHIFT) - extra_area;
area->x2 = x + ((lv_trigo_sin(start_angle_int + 90)  * rout) >> LV_TRIGO_SHIFT) + extra_area;
```

即角度→坐标是：

```
x = cx + r·sin(θ + 90) = cx + r·cos θ
y = cy + r·sin(θ)
```

θ=90° → `y = cy + r` = **最下**；θ=270° → `y = cy − r` = **最上**。
屏幕 y 向下 ⇒ **θ 递增 = 视觉顺时针**。

### 2. 生成器也按顺时针理解

`src/HmiEditor.CodeGen/CodeGenerator.cs:2584`

```csharp
// lv_arc 默认 0° 在 3 点钟方向，rotation=135 使弧线起始于左下 135°，
// bg_angles(0,270) 定义 270° 背景弧
lv_arc_set_rotation(o, 135);
lv_arc_set_bg_angles(o, 0, 270);
```

注释自己写的「起始于**左下** 135°」正是 `+sin` 约定下的结果。Arc 控件同理：
`EmitArcParameters` 发的是 `lv_arc_set_rotation(o, 0)` + `bg_angles(MinValue, MaxValue)`，
角度值与设计器完全相同 —— 所以两边只有符号约定可能出分歧，而它确实分歧了。

### 3. 数值实测（修复前）

半径 50、135° 起扫 270°：

| 几何 | Bounds | 覆盖的极点 |
|---|---|---|
| 生产折线版（`−sin`，错） | `99.97 × 85.36`，`y ∈ [14.6, 100]` | 左 / 下 / 右（弧口朝**上**） |
| `ArcTo` 版（`+sin`） | 同尺寸，`y ∈ [0, 85.4]` | 左 / 上 / 右（弧口朝**下**） |

### 4. 视觉实测（Skia 真渲染，修复前 vs 后）

用 Skia + headless 会话把设计器的 Arc / Gauge / Knob 渲成 PNG（手法见文末）：

- **修复前**：Arc 控件灰轨道自 3:00 起**逆时针**走（3:00→12:00→9:00→6:00，缺口在右下），
  绿指示弧落在**右上**。
- **修复后**：灰轨道自 3:00 起**顺时针**走（缺口在右上），绿指示弧落在**右下**
  —— 与固件 `bg_angles(0,270)` + `set_value` 完全一致。

---

## 修了什么

| 文件 | 改动 |
|---|---|
| `src/HmiEditor.Core/Design/ArcLayout.cs` | 文档纠正；新增 `PointAt(centerX, centerY, radius, angleDegrees)` 作为**全仓唯一的角度映射**；`PointerStart` 改 `= StartAngle`；`PointerDefault` 改 `= StartAngle + 0.6 * SweepAngle`（跟着语义算，不再写死）；`DefaultActiveSweep` 同样改成表达式 |
| `src/HmiEditor.Core/Design/GaugeTickLayout.cs` | 刻度端点改走 `ArcLayout.PointAt`（消掉本地 cos/sin） |
| `src/HmiEditor.App/Controls/RenderResourceCache.cs` | `BuildLvglArc` → `BuildArcPolyline`，坐标翻正为 `cy + r·sin`；`Arc()` 的参数 `lvglConvention` → `asPolyline`（现在两个分支**同一套角度约定**，只是折线近似 vs `ArcTo` 精确弧） |
| `src/HmiEditor.App/Controls/DesignCanvas.cs` | `DrawLvglArcGeometry` → `DrawArcPolyline`（3 处调用同步改名）；`RenderKnob` 指针改走 `PointAt`；两处错误注释纠正 |

回归守卫（**已做负向验证：只把符号翻回去，App 侧 4/4 红、Core 侧 7/13 红**）：

| 测试 | 位置 | 断言 |
|---|---|---|
| `PointAt_NinetyDegrees_IsAtTheBottom_SoAnglesIncreaseClockwise` | Core | 90° → `y = +r`（屏幕下方）⇒ 顺时针为正 |
| `PointAt_MatchesLvglClockwiseMapping` | Core | 0/45/90/180/270/315 六个角度的精确坐标 |
| `Sweep_RunsFrom135To405_WhichIsTheUpperThreeQuarters` | Core | 起点左下、终点右下 ⇒ 下方 90° 留空 |
| `PointerStart_CoincidesWithTheArcStart` | Core | 指针起点 = 弧起点（135°） |
| `LvglArc_LeavesTheBottomOpen` | App | 折线弧 `Bounds.Y == 0`（触顶）、下沿只到 85.36（底部留空） |
| `ArcTrack_ZeroToNinety_DrawsInTheBottomRightQuadrant` | App | 0..90° 轨道画在**右下**象限（镜像会变右上） |
| `ArcIndicator_SweepsClockwiseFromThreeOClock` | App | 0..135° 指示弧整个在**下半**；并反向断言"不存在整个在上半的弧" |
| `KnobPointer_AtMinimumValue_PointsDownLeft` | App | Value=Min 时指针朝**左下** |

顺带把 `RenderResourceCacheTests` 里那条「两种约定包围盒相同」的错断言换掉了 ——
它比的是 `ArcTo` 几何的 `Bounds`，而 headless 下 `ArcTo` 是桩（见下），
量的是桩、不是渲染。

---

## 附一：headless 下的测量陷阱

| 陷阱 | 事实 |
|---|---|
| **`ArcTo` 是桩实现** | `UseHeadlessDrawing = true` 时 Avalonia 的 `ArcTo` 只记端点、不算弧。135°→405° 的几何 `Bounds` 实测 `70.71 × 0.00` —— 就是两端点连成的弦，高度 0。**不能用 `ArcTo` 几何的 `Bounds` 做断言。**折线版是纯托管 `StreamGeometry`，`Bounds` 真实可用，所以弧相关的行为测试都走折线版。 |
| **`DrawingGroup.Open()` 只录顶层** | 数 `group.Children` 会把文本看成「只有一个 `DrawingGroup`」（glyph 在 `PushTransform` 嵌套组里），必须递归展开再看节点类型。 |
| **画布自身会画 260×260 的描边矩形** | 录绘制操作时它会混进"有画笔无填充"的集合里，按尺寸过滤时必须排除（本页的测试用「象限精确匹配」或尺寸窗口绕开）。 |

## 附二：把设计器渲成 PNG 的手法（30 分钟踩坑换来的，别丢）

headless 默认是桩绘制，出不了图。要真渲染得开一个 **Skia + `UseHeadlessDrawing=false`** 的会话：

```csharp
public static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .UseSkia()                                                  // 需要 Avalonia.Skia（App 引 Desktop 即传递进来）
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

var session = HeadlessUnitTestSession.StartNew(typeof(SkiaTestAppBuilder));
session.Dispatch(() =>
{
    var canvas = new DesignCanvas { Screen = screen, ZoomLevel = 1.0 };

    var rtb = new RenderTargetBitmap(new PixelSize(640, 220), new Vector(96, 96));
    using (var dc = rtb.CreateDrawingContext())   // ← 关键：走这条，别用 rtb.Render(canvas)
        canvas.Render(dc);

    rtb.Save("/home/chen/.hmi-test-tmp/out.png");
}, CancellationToken.None).GetAwaiter().GetResult();
```

两个坑：

1. **`rtb.Render(canvas)` 出白板** —— 即使 `canvas.Measure/Arrange` 过、`Bounds` 正常也全是白的。
   必须 `rtb.CreateDrawingContext()` + 直接 `canvas.Render(dc)`（`DesignCanvas.Render`
   不依赖布局通道，渲染矩形完全由模型坐标 × 缩放算出）。
2. 一个进程里开两个 `HeadlessUnitTestSession`（一个桩绘制、一个 Skia）**未验证是否安全**，
   所以这套手法当时只作为一次性探针用完即删；要长期用建议单独开一个测试项目，
   别混在 `HmiEditor.App.UiTests` 里。
