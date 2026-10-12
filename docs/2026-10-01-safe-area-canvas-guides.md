# 安全区 ⇒ 画布参考线（阶段 221）
## —— `#98` 的"接线"（先接**预览**这条链）

> 用户对 `#98`（`ScreenDpi` ＋ 安全区）拍的是"**接线**"。这一阶段先接**风险最小、又看得见效果**的一条：
> **安全区 ⇒ 画布上的虚线参考线**。`DpiScale` 的接线单独做 —— 它要接进画布坐标换算与渲染比例，
> 牵动命中测试，不是同一个量级（下面 §5 写清为什么）。

---

## 1. 此前是什么状态

阶段 216 量出：`ScreenDefaultsConfig.SafeAreaPadding` / `DpiScale` **全仓只有两个设置面板读写**
（`PropertyPanelView` / `ProjectSettingsView`），`HmiEditor.CodeGen` 零引用、预览画布零引用
⇒ 用户改了、存了，**既不进导出也不进预览**（登记 `#98`）；阶段 218 给它们挂上了"尚未生效"的提示。
现在开始真正接线。

## 2. 这一阶段接了什么

| 层 | 改动 |
|---|---|
| `DesignCanvas` | 新增 `SafePaddingProperty`（`Thickness`，默认全零）· 公开 `SafeAreaGuides()`（**画布坐标**＝模型 × `ZoomLevel` 的四条线段，脏数据会归一）· `DrawSafeAreaGuides()` 画虚线（`DashStyle(6,4)`，`DesignPalette.Success` 60% 透明度）· `internal int SafeGuidesDrawnCount`（**渲染期计数**，供判据） |
| `DesignCanvasView` | `SetScreen(HmiScreen screen, HmiProject? project = null)` —— 传工程时顺带 `ApplyProjectScreenDefaults(project)`，把 `SafeAreaPadding` 转成 `Thickness` 交给画布 |
| `MainWindow` | 三处 `CanvasPanel.SetScreen(...)` 改为传 `_currentProject`（**行数不变**，见 §4） |

**只影响显示**：参考线不改任何控件几何、不进导出 —— 与 `#98` 的处置①（进导出）是两回事，
后者仍需单独决定（要不要按安全区做内容内缩）。

## 3. 判据（5 条，`DesignCanvasSafeAreaTests`）

| 条 | 断言 |
|---|---|
| `NoPadding_MeansNoGuides` | 全零内边距 ⇒ 空集合 |
| `Padding_ProducesFourGuidesInCanvasCoordinates` | 内边距 (10,20,30,40)、缩放 2 ⇒ 四条线的端点逐点对上（含 `To.X > From.X` / `To.Y > From.Y` 的**方向**判） |
| `InvertedPadding_IsNormalised` | 脏数据（左边距比屏幕还宽）⇒ **不能**画出反向线段 |
| `Render_DrawsTheGuides` | **真的走一遍渲染**（headless 用 `RenderTargetBitmap` 强制），渲染期计数 ＝ 4；清空内边距再渲染 ＝ 0 |
| `ProjectSafeArea_ReachesTheCanvas` | `SetScreen(screen, project)` 把工程的 (8,12,16,20) 带进画布；换成没有安全区的工程 ⇒ 归零（不残留） |

⚠ `Render_DrawsTheGuides` 一开始**没红也没绿对**：headless 下 `InvalidateVisual()` ＋ `Pump()`
**不会**真的跑 `Render`（UI 测试宿主不产像素）⇒ 必须用 `RenderTargetBitmap` 取一个真实
`DrawingContext`（与既有 `CanvasRenderBaselineTests` 同一手法）。

## 4. 🩸 门禁当场红了一次：`MainWindow.axaml.cs` 行数 2983 → 2985

第一版我在 `MainWindow` 的两处调用点后面各加了一行 `ApplyProjectScreenDefaults(...)` ⇒
**单文件行数治理守卫**（`SingleFileSizeGuardTests`，基线 2983）立刻红：
「涨了 = 治理回涨（先把文件改小）」。

处置不是改基线，而是**把接线挪进 `SetScreen` 的可选参数**：
`SetScreen(screen, project = null)` ⇒ `MainWindow` 只是把实参从 `screen` 改成 `screen, _currentProject`
（**行数不变**），而且接线点从两处收敛成一处。⇒ 这条守卫逼出来的设计比原来更干净。

## 5. 为什么 `DpiScale` 没在这一阶段接

`DpiScale`（0.5..3）要"看得见效果"，就得进**渲染比例与坐标换算**（`DesignCanvas.Coordinates`
与各处 `var z = ZoomLevel`）—— 而成对的渲染/命中也必须一起缩放，否则**拖拽会错位**。
那是一次覆盖面较大的替换（画布三个 partial 里 18 处 `ZoomLevel`），必须配"拖 N 像素 ⇒ 模型位移
= N / 有效缩放"这类判据，属于独立一阶段。**这一阶段不碰它**，登记里也如实写着。

## 6. 负控（5 支，`.workbuddy/tmp/s221-nc.py`）

| 支 | 注入 | 期望红 |
|---|---|---|
| nc1 | 渲染里去掉 `DrawSafeAreaGuides(context)` | `Render_DrawsTheGuides` |
| nc2 | 几何忘了乘缩放（`z = 1.0`） | `Padding_ProducesFourGuidesInCanvasCoordinates` |
| nc3 | 去掉脏数据归一（`r<l` / `b<t` 交换） | `InvertedPadding_IsNormalised` |
| nc4 | 画布恒返回空（不看 `SafePadding`） | 几何判据 |
| nc5 | 宿主不读工程的 `SafeAreaPadding` | `ProjectSafeArea_ReachesTheCanvas` |

> 🩸 **nc3 第一版没咬** —— 它暴露出**判据本身太弱**：我原来写的是
> `To.X >= From.X || To.Y >= From.Y`（**或**），而反向线段 `(900,500)→(800,500)` 的 Y 相等 ⇒ 或为真 ⇒ 放过。
> 改成**逐条判方向**（上边指向右、右边指向下）才咬住。⇒ 负控不只校验实现，也会**校验判据**。

## 7. 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2314 / 315 / 823 / 8`（合计 **3726**）
＝ **3726 通过 / 0 失败**（阶段 220 为 3721，**＋5**）；`App.UiTests` **818 → 823**；
`MainWindow.axaml.cs` **仍 2983**（守卫要求）；`UnusedBaseline` **不变 1109**。

## 8. 仍未做

1. **`DpiScale` 的接线**（进渲染比例 ＋ 坐标换算 ＋ 拖拽判据）—— 独立一阶段。
2. **`#98` 的处置①（进导出）**：安全区要不要影响**生成几何**（内容内缩/控件吸附）仍是产品决策；
   本阶段只接了预览。
3. 参考线的**可见性开关/颜色**没做成可配置（现在恒为 60% 透明度的成功色虚线）。

---

## 9. 阶段 222：`DpiScale` 也接上了（`#98` 的后半）—— 有效缩放 = 用户缩放 × DPI

阶段 221 刻意没碰 `DpiScale`（它要接进渲染比例与坐标换算，牵动命中测试）。这一阶段把它接了。

### 9.1 做法

- `DesignCanvas` 新增 `DpiScaleProperty`（`double`，**默认 1.0**）与
  **`EffectiveZoom` ＝ `ZoomLevel × DpiScale`**（DPI ≤ 0 的脏数据按 1 处理，绝不让换算出 `Infinity`）。
- 把画布三个 partial 里**渲染与坐标换算**用到的 `ZoomLevel` **25 处**换成 `EffectiveZoom`
  （`DesignCanvas.cs` 的屏幕矩形/自适应变换/两处渲染器/安全区参考线 · `Editing.cs` 的
  `Coordinates` 与两处 · `Input.cs` 的命中/框选/坐标换算 · `Renderers.cs` 三处）；
  **保留**"用户缩放"本身的 4 处（属性注册/访问器/`AffectsRender` 列表）与 Ctrl+滚轮那 3 处
  （那是**用户**缩放的计算与写回 —— DPI 不该被滚轮改掉）。
- 新增两个公开包装 `ViewportToModel` / `ModelToScreen`（内部命中测试走的是同一个 `Coordinates`），
  让判据能直接断言这条链。
- 宿主：`ApplyProjectScreenDefaults` 顺带把工程的 `ScreenDefaultsConfig.DpiScale` 交给画布
  （默认 1.0 ⇒ 既有工程与既有判据一行都不变）。

### 9.2 判据（7 条，`DesignCanvasDpiScaleTests`）

默认 1.0 时一切照旧（含 `EffectiveZoom = ZoomLevel`）· `EffectiveZoom` 相乘 · **坐标换算成对缩放**
（DPI 2× ⇒ 视口 (200,100) 对模型 (100,50)；叠平移也逐点对）· 非正 DPI 按 1 且换算值**有限** ·
**渲染侧几何也认它**（安全区参考线按 `EffectiveZoom` 算：10 → 20、(800-10) → 1580）· 宿主接线（含换工程归零）。

> 为什么"渲染侧"要单独一条：DPI 只改命中不改渲染（或反过来）的症状是**画得对、拖拽错位**，
> 静态看代码看不出来。把两侧各钉一条，才叫"接线接全了"。

### 9.3 负控（5 支，`.workbuddy/tmp/s222-nc.py`）

命中侧退回只用用户缩放（`Coordinates`）· 渲染侧退回只用用户缩放（安全区几何）· 去掉非正 DPI 兜底 ·
宿主不传工程的 DPI · `EffectiveZoom` 忘乘用户缩放 —— 五支各自咬住对应那条判据。

### 9.4 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2314 / 315 / 830 / 8`（合计 **3733**）
＝ **3733 通过 / 0 失败**（阶段 221 为 3726，**＋7**）；`App.UiTests` **823 → 830**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` **不变 1109**。

### 9.5 仍未做

1. **`#98` 的"进导出"**（安全区是否影响生成几何/内容内缩、DPI 是否影响字号与尺寸换算）——
   **产品决策**，未动。
2. 参考线样式（颜色/线宽/开关）不可配置。
3. DPI 目前只影响**画布**；**预览窗口/模拟器**是否也要按它缩放，未评估。
