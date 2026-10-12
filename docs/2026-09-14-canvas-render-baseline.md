# DesignCanvas 渲染性能基线（2026-09-14）

> 目的：在动任何渲染代码之前，先拿到**可复现、可 A/B 对比**的量化基线。
> 没有这个数，任何「画布卡」的判断和优化都只能靠猜。

## 1. 测法

`UseHeadlessDrawing = true` 的 Avalonia headless 平台**不做合成遍历** ——
`InvalidateVisual()` + `ForceRenderTimerTick()` 下 `DesignCanvas.Render` 的调用次数恒为 **0**（实测确认）。
因此基线不用渲染定时器，改为：

1. `UiTestHost.Run(...)` 进真实 headless UI 线程；
2. 自行 `new RenderTargetBitmap(...)` 取一个真实 `DrawingContext`；
3. 直接调用 `canvas.Render(dc)`（这条路径不依赖布局通道 —— 渲染矩形完全由
   模型坐标 × 缩放算出，见 `DesignCanvas.RenderControl`：`rect = new Rect(control.X * z, ...)`）；
4. 用 `GC.GetAllocatedBytesForCurrentThread()` 取**精确分配字节数**（非 GC 后存活量），
   先热身 50 次、再量 200 次取均值；
5. 扣除「0 控件屏幕」的框架底噪（实测 **1,880 B/帧**）。

分配字节数在 .NET 里是确定性的、不受 GC 时机影响，因此改动前后可直接比较。

测试位置：`tests/HmiEditor.App.UiTests/CanvasRenderBaselineTests.cs`

## 2. 渲染基元单位成本（实测）

| 基元 | 分配 (B) |
|---|---|
| `new SolidColorBrush(Color)` | **616** |
| `new Pen(brush, 1.0)` | **1,264** |
| `new FormattedText(text, …, 14, brush)`（构造） | **384** |
| 同上，首次读 `.Width`（触发**惰性文本布局**） | **+3,004**（合计 3,388） |
| 复用已布局实例读 `.Width` | **0** |
| `context.DrawText(ft, p)`（已布局） | **2,604** |
| `new StreamGeometry()` + `Open/BeginFigure/LineTo/EndFigure/Dispose` | **480** |
| `Color.Parse("#0EA5E9")` | **0**（struct，无分配） |
| `context.FillRectangle / DrawRectangle / DrawLine / DrawGeometry` | **0** |
| `context.PushTransform / PushOpacity` + Dispose | **0** |

> 注：headless 的 no-op `DrawingContext` 在几何绘制上确实是零分配，所以上面的
> 几何类 0 B 是「框架不开销」，不是说真实 Skia 也不开销。但**我们自己 new 的对象**
> 与 `DrawText` 的开销是真实且可比的。

## 3. 单控件每帧分配普查（25 种控件全测）

| 控件类型 | B/控件/帧 | | 控件类型 | B/控件/帧 |
|---|---:|---|---|---:|
| **Keyboard** | **225,240** | | TabView | 10,189 |
| Tank | 23,892 | | Bar | 9,750 |
| Slider | 15,048 | | Label | 9,199 |
| Gauge | 13,046 | | Button | 9,196 |
| Arc | 13,026 | | TextArea | 9,134 |
| Pipe | 12,054 | | CheckBox | 9,081 |
| Dropdown | 10,864 | | Image | 9,047 |
| Valve | 8,437 | | Spinner | 8,007 |
| Knob | 3,762 | | Switch | 3,114 |
| Table | 2,986 | | Rectangle | 2,498 |
| Ellipse | 2,498 | | Polyline | 2,482 |
| Line | 1,882 | | Svg | 1,882 |
| Led | 1,234 | | | |

**全类型屏（25 控件）= 418,133 B/帧、0.574 ms/帧。**

## 4. 成本归因（单变量 A/B 实测，Label，20 个/帧）

| 场景 | B/控件/帧 | 增量 |
|---|---:|---:|
| 裸控件（无 BgColor / 无边框 / 无文本） | 7,106 | — |
| + BgColor | 7,707 | **+602** |
| + BorderWidth=2 | 8,981 | **+1,875** |
| 全开 | 9,151 | +2,046 |

增量与单位成本表**精确对上**：`+BgColor` 602 ≈ 一个 brush 616；
`+BorderWidth` 1,875 ≈ brush 616 + pen 1,264 = 1,880。

### 单控件每帧预算（Label 全开，实测 9,151 B）

| 项 | 现状 (B) | 归因 |
|---|---:|---|
| 背景 brush（`ParseColor` 每次 new） | 602 | **可消除** |
| 边框 brush + pen | 1,875 | **可消除** |
| 文本 brush（`DrawAlignedText` 内 new） | 616 | **可消除** |
| `FormattedText` 构造 + 惰性布局 | ~3,384 | **可消除**（复用实例读 Width = 0 B） |
| `context.DrawText`（框架） | 2,604 | 不可通过缓存消除 |
| **合计** | **9,151** | **理论上可降到 ~2,604（降 71%）** |

## 5. 根因（两条咽喉，各自单点）

| # | 位置 | 问题 | 证据 |
|---|---|---|---|
| **R1** | `DesignCanvas.DrawAlignedText`（:1256） | **所有文本渲染的唯一咽喉**，每次调用新建 `new SolidColorBrush(color)` + `new FormattedText(...)`，并读 `.Width/.Height` 触发惰性布局 → **6.6 KB/次文本** | 单位成本表；Label 预算逐项对上 |
| **R2** | `DesignCanvas.ParseColor`（:875）/ 各处 `new Pen(...)` | **无任何缓存**，每次取色都 new 一个 `SolidColorBrush`；`new Pen` 1,264 B | `ParseColor` 源码无字典缓存；`+BorderWidth` 增量 1,875 ≈ 616+1,264 |

**Keyboard 是 R1 的极端放大案例**：`RenderKeyboardPlaceholder`（:1639）循环里每帧新建
36 个 `FormattedText`（且每个都配一个 `new SolidColorBrush`，:1677），
36 × 6.6 KB ≈ 238 KB，与实测 225 KB/控件/帧吻合 —— 一个键盘控件每帧产生 225 KB 垃圾。

**Gauge/Knob/Arc/Pipe 的额外成本**来自 `DrawArcGeometry`（:1287）每次 new `StreamGeometry`（480 B/次）。

## 6. 结论

- 全类型 25 控件屏 **418 KB/帧**；按 60 fps 计约 **25 MB/s** 托管垃圾。
  一个 100 控件的真实工程页按 Label 单价外推约 **0.9 MB/帧 ≈ 54 MB/s**，
  Gen0 回收频率必然被顶高 —— 这是「画布操作发涩」的**直接解释**。
- 优化杠杆高度集中：**只改 R1 + R2 两个点**即可覆盖 72% 以上的分配，
  且这两点都是纯缓存、零行为变更。
- **DrawText 的 2,604 B/次是框架下限**，缓存 FormattedText 之后它成为新的主导项，
  后续若要再降只能改用 `DrawGlyphRun` 直接喂字形，属 P2。

## 7. 回归门禁（优化落地后启用）

优化完成后，`CanvasRenderBaselineTests.Baseline_PerControlType_AllocationCensus`
改为断言上限（取优化后实测值 +30% 余量），把「画布不会重新退化成每帧新建对象」钉进 CI。

已落地为 `CanvasRenderBaselineTests.Gate_CanvasRenderAllocation_StaysWithinBudget`，预算表：

```csharp
TypeBudgets[Tank]     = 12_000;    // 实测 8.8 KB
TypeBudgets[Keyboard] = 120_000;   // 实测 86.7 KB
DefaultPerControlBudget = 4_000;
FullScreenBudget        = 200_000;
```

门禁启用后已抓到一次 `Tank` 超预算（原因：Tank 每帧要画 `ScaleTop`/`ScaleBottom` 两段文本，
框架 `DrawText` 底价 2,604 × 2 ≈ 5.2 KB，所以单列预算）。

---

## 8. 优化落地后的实测对照（R1 + R2 已修）

实现：新增 `src/HmiEditor.App/Controls/RenderResourceCache.cs`（画刷 / 画笔 / 文本布局 / 弧几何
四类缓存，键全部取自不可变值），并把 `DesignCanvas` 内
`new SolidColorBrush` 71 处 → 0、`new Pen(` 41 处 → 3（剩 3 处虚线画笔，只在拖拽时渲染）、
`new FormattedText` 7 处 → 0（改走 `RenderResourceCache.Text`）。
`ImmutableSolidColorBrush` / `ImmutablePen` 是**不可变**的，可安全共享；
`SolidColorBrush` / `Pen` 是可变 `AvaloniaObject`，共享会互相污染，所以不能直接缓存。

| 项 | 优化前 | 优化后 | 降幅 |
|---|---|---|---|
| **全类型屏（25 控件）** | 418,133 B/帧 | **134,730 B/帧** | **−68%** |
| 同屏耗时 | 0.574 ms/帧 | **0.278 ms/帧** | **−52%** |
| Keyboard | 225,240 | 86,693 | −62% |
| Tank | 23,892 | 8,804 | −63% |
| Label / Button / Bar / TextArea | ≈ 9,150 | **2,804** | **−69%** |
| Slider | 15,048 | 3,280 | −78% |
| Gauge | 13,046 | 3,004 | −77% |
| Arc / Led / Knob / Line / Svg / Rectangle / Ellipse / Switch | 1,234 ~ 13,026 | **2** | ≈ 归零 |
| Polyline | 2,482 | 602 | −76% |
| Table | 2,986 | 490 | −84% |
| 框架底噪（0 控件） | 1,880 | **0** | 归零 |

`2,804 B/控件/帧` = 框架 `DrawText` 底价 2,604 + 少量残余 —— **已无余量可削**。
再往下只能改 `DrawGlyphRun` 直接喂字形（P2）。

---

## 9. R3：缓存实例的寿命陷阱（原为静默缺陷，已修）

### 现象

`GaugeTickRenderTests.RenderGauge_StillDrawsArcAndText_AlongsideTicks` 单独跑通过，
**同类多用例同进程跑就失败**，报「Gauge 数值文本没有被绘制」，
录到的节点从 `GeometryDrawing:8, DrawingGroup:2, GlyphRunDrawing:1` 掉成
`GeometryDrawing:8, DrawingGroup:1`（文本的 `PushTransform` 组连同 `GlyphRunDrawing` 一起消失）。

### 定位过程（决定性证据）

在**一个** `UiTestHost.Run` 里连录 20+ 次全部正常；改成**跨 Dispatch** 就必掉：

```
D#2 (第二次 Dispatch)
  画布(热缓存) → glyph=0                              ← 文本没画出来
  text=50 clear=False fresh=False → 抛 ObjectDisposedException: Cannot access a disposed object.
  text=52 clear=False fresh=True  → glyph=1           ← 现造 FormattedText 正常
  text=53 clear=True  fresh=False → glyph=1           ← 清缓存重取也正常
  画布(清缓存后) → glyph=1                            ← 画布恢复
```

### 根因

1. `RenderResourceCache` 是**进程级 `static`**，缓存了 `FormattedText`；
2. `FormattedText` 背后是**平台侧文本布局对象**，会随每次 headless Dispatch 结束（生产环境则是
   渲染后端重建 / 字体服务重载）被释放；
3. 下一次 Dispatch 复用该实例读 `.Width` 触发惰性布局 → `ObjectDisposedException`；
4. 而 `DrawAlignedText` 当时是个**裸 `catch { }`** → 异常被静默吞掉 → 这段文本**永久画不出来**，
   且现场零线索。

注意：**画刷 / 画笔 / 弧几何不受影响**（纯托管对象），实测 `GeometryDrawing:8` 与
`CountStrokedSegments`（要读 `geometry.Bounds`）在故障态下依然正确。
只有 `FormattedText` 这条线有寿命问题。

### 修法

`RenderResourceCache.InvalidateTexts()`（整批丢弃 + `TextInvalidations` 计数 + `Debug.WriteLine` 线索），
`DrawAlignedText` 改为「捕获 → 丢弃 → 重建重试一次」：

```csharp
try
{
    DrawAlignedTextCore(context, RenderResourceCache.Text(text, fontSize, brush), rect, hAlign, vAlign);
}
catch
{
    RenderResourceCache.InvalidateTexts();
    try { DrawAlignedTextCore(context, RenderResourceCache.Text(text, fontSize, brush), rect, hAlign, vAlign); }
    catch { /* 二次失败说明不是缓存陈旧所致；绘制路径不能把异常抛回 UI 线程 */ }
}
```

整批而不是按条目丢弃：这批实例同批创建、寿命一致，清一次就够，避免一帧内 36 个键位各抛一次异常。

回归测试：`RenderResourceCacheTests.TextIsStillDrawn_WhenCachedFormattedTextOutlivesItsDispatch`
（先一次 Dispatch 捂热缓存，再在**各自独立的 Dispatch** 里连续 3 轮断言 `GlyphRunDrawing` 存在）。

### 顺带记下：headless 的两个测量陷阱

| 陷阱 | 事实 |
|---|---|
| **`ArcTo` 是桩实现** | `UseHeadlessDrawing = true` 下 Avalonia 的 `ArcTo` 只记端点、不算弧。135°→405° 的几何 `Bounds` 实测是 `70.71 × 0.00`（就是两端点连成的弦，高度 0）。**不要用 `ArcTo` 几何的 `Bounds` 做断言。**（由 LVGL 折线版对比发现，详见 `docs/2026-09-14-arc-angle-convention.md`） |
| **`DrawingGroup.Open()` 只录顶层** | 数 `group.Children` 会把文本看成「只有一个 `DrawingGroup`」（文本在 `PushTransform` 的嵌套组里）。必须递归展开再看节点类型。 |

另：`Measure`/`PerCall` 这类「先热身再取计数器差值」的手法，热身会把待测的键捂热
（例如测「构造 vs 复用」时，热身已经把键建好了，两侧都变 0），
基线的「构造」侧必须**绕过缓存直接 `new`**。

