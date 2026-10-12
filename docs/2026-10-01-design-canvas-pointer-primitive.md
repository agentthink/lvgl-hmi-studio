# 设计：给第五层加「指针序列」原语（`DesignCanvasView` 的面）—— 阶段 143

> **本阶段是设计 + 可行性验证，不是实现。** 用户拍板"给 `DesignCanvasView` 设计新原语"。
> 交付：① 现有四类手势为什么盖不住它（实测）；② 新原语的 API 形状与框架接触点；
> ③ **可行性已用编译 spike 验证**（指针事件在 headless 下可构造）；④ 断言设计、成本、风险、
> 分阶段落地顺序与负控预案。**登记为 `#77`**（见登记表 §4），本阶段**不动产品、不动判据**。

---

## 1. 为什么现有四类手势盖不住（实测）

| 现有手势 | 驱动方式 | 它能驱动的控件 |
|---|---|---|
| `Moves(name, …)`（按钮族） | `RaiseEvent(Button.ClickEvent)`；`ToggleButton` 先翻转 `IsChecked` | 具名 `Button` / `CheckBox` / `ToggleButton` |
| `SelMoves(name, index, …)`（选择族） | `SelectingItemsControl.SelectedIndex = index` | `ListBox` / `ComboBox` |
| `SelMovesPath(name, path, …)` | 树路径 DFS 后置 `SelectedItem` | `TreeView` |
| `SelInert(...)` | 同上（只声明 `Inert`） | 同上 |

`DesignCanvasView` 的面**一个都不落**：

- 它的「选择」不是 `SelectingItemsControl.SelectionChanged`，而是画布**自己的**事件
  `CanvasLayer.SelectionChanged += (_, controls) => …`（`DesignCanvasView.axaml.cs:148`）
  —— 静态面守卫按**事件名**匹配，把它算成 1 个接线控件（普查表那一行：
  `("DesignCanvasView", 1), // 画布（选择来自画布自身，不是控件事件）`），但**没有任何手势能驱动它**。
- 它的编辑动作是**指针序列**：`OnPointerPressed` / `OnPointerMoved` / `OnPointerReleased`
  （`DesignCanvas.Input.cs:87 / 278 / 401`）—— 拖动移动控件、框选、绘制、平移。
- ⚠ 全仓测试**从未驱动过**画布的指针交互：现有画布测试只碰 `DesignCanvas.Render`
  （`CanvasRenderSpy` / `CanvasRenderBaselineTests` / `ArcControlRenderTests`）。

## 2. 画布的面（实测清单）

| 面 | 内容 | 现在的可判性 |
|---|---|---|
| 具名 Button 族（**5**） | `PreviewToggleButton`（`IsCheckedChanged` 接线）＋ 软键盘 `KbdShiftBtn` / `KbdSpaceBtn` / `KbdBackspaceBtn` / `KbdDoneBtn` | 可判：按钮族本来就能驱动；但**软键盘是"输入面"，不是数据面**（见 §4 ②） |
| 自定义选择（**1**） | `CanvasLayer.SelectionChanged`（画布自己的选中集合） | **不可判**（本设计要解决的） |
| 指针语义（未计数） | 拖动移动 / 框选 / 绘制 / 平移 / 预览模式点击 | **不可判** |

## 3. 原语设计：`PointerPath`（指针序列）

### 3.1 API 形状

```csharp
/// 指针序列的一步：按下 / 移动 / 抬起；坐标是**目标控件的局部坐标**
/// （与 `PointerEventArgs.GetPosition(control)` 同义 ⇒ 不依赖窗口位置、可预期）。
internal enum PointerStepKind { Press, Move, Release }
internal readonly record struct PointerStep(PointerStepKind Kind, double X, double Y);

// ClickSpec 新增一个可选字段（与 SelectIndex / SelectPath 并列，互斥）
public IReadOnlyList<PointerStep>? PointerPath { get; init; }

// 工厂（命名与 SelMoves/SelMovesPath 同族）：一条"拖"= 按下 → 若干移动 → 抬起
private static ClickSpec Drags(string name, (double X, double Y) from, (double X, double Y) to,
                               string why, params (string, string)[] expect);
```

### 3.2 驱动实现（框架里唯一的执行点）

`RunGesture` 增一个分支（与 `SelectIndex` / `SelectPath` 并列）：

1. `Control<Control>(window, name)` 取目标控件（复用既有的两条查找路：NameScope → 逻辑树）。
2. `rootVisual` 取 `TopLevel.GetTopLevel(control)`（指针事件的 root 参数）。
3. 事件构造（**spike 已实测可编译**）：
   ```csharp
   var pointer = new Pointer(id: 1, PointerType.Mouse, isPrimary: true);
   var pressed = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
   var args = new PointerPressedEventArgs(control, pointer, rootVisual, pos, timestamp, pressed,
                                          KeyModifiers.None, clickCount: 1);
   control.RaiseEvent(args);
   ```
   `Move` / `Release` 用同族的 `PointerEventArgs` / `PointerReleasedEventArgs`（**待验**：两者的 ctor
   参数表不同，spike 只验了 press —— 这一步是落地时的第一个 TODO）。
4. **每步之间 `Pump()`**（布局与 `InvalidateVisual` 都要走一次消息循环，否则命中测试拿不到几何）。
5. 抬起后再 `Pump()`，然后照常读全分量、跑 `Check`。

### 3.3 必须同步改的三处（否则判据自己会红）

| 处 | 改什么 | 为什么 |
|---|---|---|
| `SelectDeclarations()` 的过滤 | 加 `|| PointerPath is not null` | 否则静态守卫把"接线了却没分类"报在 `CanvasLayer` 上（本层靠声明认面） |
| `ClickSpec` 的唯一性键 | 现在是 `(名字, 选择手势下标)` ⇒ 加"指针路径" | 同一条路径驱动两次会撞键（选择族当年就是这么加的） |
| `Check` 的 `moved` 计数 | 不用改 | 它只看"声明的分量变了几个"，与手势种类无关 |

## 4. 断言设计（**模型侧**优先）

画布的数据面几乎全在模型里（这正是它值得进本层的原因）：

```csharp
Num("控件数",   c => c.Depot.Project.Screens[0].Controls.Count),   // 绘制/删除 才有变化
Num("控件X",    c => (int)c.Depot.Project.Screens[0].Controls[0].X),  // 拖动移动
Num("选中数",   c => ((Views.DesignCanvasView)c.Window.Content!).Canvas.SelectedControls.Count),
```

三条腿（**按风险从低到高**，建议分两次落地）：

| # | 腿 | 手势 | 期望 | 风险 |
|---|---|---|---|---|
| ① | **点中控件 ⇒ 选中** | 在控件矩形内 press＋release | 选中数 0 → **1** | **低**（不碰拖动状态机） |
| ② | **拖动移动控件** | 控件内 press → move(+50,0) → release | 控件X 10 → **60** | 中（依赖 `_dragStartBounds` 与命中测试） |
| ③ | 空画布 + 绘制工具 ⇒ 新建控件 | `DockLayoutState.SetDrawTool(Button)` + press/move/release | 控件数 0 → 1 | **高**（依赖工具状态机与最小尺寸阈值） |

⚠ 软键盘那 4 个按钮**不建议**当数据面：它们只把字符写进 `KbdTargetLabel`（输入面），
要判就得引入"输入面"口径 —— 与本层"读数据面"的主旨不符（可记 `Inert` 并写清理由）。

## 5. 成本、风险与负控预案

**成本（估）**：框架 3 处（§3.3）＋ `HostSpec`/`ClickSpec` 字段 + 工厂 ＋ 一条腿的表与夹具
＋ 静态守卫的 census 行删除与口径链 + 文档。**约等于本层"加一个普通宿主"的 1.5–2 倍**
（普通宿主是 1 倍，因为不用碰框架）。

**风险**：
1. `PointerEventArgs` / `PointerReleasedEventArgs` 的 ctor 与 press 不同（落地第一步就要验，spike 只验了 press）。
2. headless 下的**命中测试**依赖布局：必须 `Window.Show()` ＋ `Pump()`；若控件矩形为 0，`HitTestControl` 会落空。
3. 拖动可能走**手势识别**（阈值）⇒ 移动步要够大（建议 +50px）且分多步。
4. 画布的 `InvalidateVisual` 在 headless 不渲染 ⇒ 判据**只能读模型**（这也正是本设计的主张）。

**负控预案（与现有各宿主同形）**：
① 摘 `CanvasLayer.SelectionChanged` 的转发 ⇒ ①那条腿红（"声明的分量一个都没动"）；
② 把拖动写入改成常量（`X = _dragStartBounds.X`）⇒ ②那条腿红（"控件X 期望 60、实际 10"）；
③ 删声明 ⇒ **静态守卫红、运行期绿**（面归静态守卫管 —— 阶段 142 已复证）。

## 6. 结论与建议

- **可行性**：指针事件在 headless 下**可构造**（spike 编译通过，签名见 §3.2）⇒ 原语**没有原理性障碍**；
  剩下的是工程细节（§5 风险 1–3）。
- **建议顺序**：先落 **①点中选中**（低风险、能立刻把 `CanvasLayer` 从"不可判"变成"可判"），
  再加 **②拖动移动**；**③绘制**单独立项（它牵动工具状态机，价值/成本比最低）。
- **收益**：面外最大的"不可判"资产（画布指针语义）进来的同时，本层第一次拥有**指针类手势**
  —— 之后 `DeviceManagerView` / `SimulatorView` 等若也有指针交互，可直接复用。
- **触发条件**：谁要落这个原语，按 §3.2 的 TODO 先验 move/release 的 ctor，再按 §4 的①→②顺序加腿。

---

## 补记（阶段 144）：① 已落地，且设计有两处要更正

**已落地**：原语 `Drags(...)`（`ClickSpec.PointerPath` ＋ `PointerStep`）＋ 框架 3 处同步 ＋
第 19 个宿主 `DesignCanvasView` 的**第一条指针腿**（点中控件 ⇒ 选中数 0 → 1）。
负控 nc144-1 把 `RaisePointer` 改成空转 ⇒ 那条腿红 ⇒ **新原语确实承重**。

🩸 **更正 1（§3.2 的实现路径错了）**：手工构造 `PointerPressedEventArgs` **跑不起来** ——
那个 8 参 ctor 不设 `RoutedEvent`，`RaiseEvent` 当场抛
`ArgumentException: Cannot raise an event whose RoutedEvent is null`（带 `RoutedEvent` 的重载**不存在**）。
**正解**：`Avalonia.Headless` 的 `MouseDown/MouseMove/MouseUp`（本仓 `HeadlessInputAndDialogUiTests` 已有先例），
坐标用 `TranslatePoint` 从**控件局部**换算到**根窗口**（Headless 扩展吃的是根坐标）。

🩸 **更正 2（§5 风险 1 作废）**：「move/release 的 ctor 待验」这条 TODO 不再需要 ——
改走 Headless 扩展后不经手那些 ctor。**教训**：spike"编译通过"只证明签名存在，
`RoutedEvent` 为 null 是**运行期**才炸的 —— **可行性的第一层不等于跑得起来**。

**仍开着**：② 拖动位移控件、③ 绘制新建（§4 的风险排序与负控预案不变）。

---

## 补记（阶段 144）：① 已落地，且设计有两处要更正

**已落地**：原语 `Drags(...)`（`ClickSpec.PointerPath` ＋ `PointerStep`）＋ 框架 3 处同步 ＋
第 19 个宿主 `DesignCanvasView` 的**第一条指针腿**（点中控件 ⇒ 选中数 0 → 1）。
负控 nc144-1 把 `RaisePointer` 改成空转 ⇒ 那条腿红 ⇒ **新原语确实承重**。

🩸 **更正 1（§3.2 的实现路径错了）**：手工构造 `PointerPressedEventArgs` **跑不起来** ——
那个 8 参 ctor 不设 `RoutedEvent`，`RaiseEvent` 当场抛
`ArgumentException: Cannot raise an event whose RoutedEvent is null`（带 `RoutedEvent` 的重载**不存在**）。
**正解**：`Avalonia.Headless` 的 `MouseDown/MouseMove/MouseUp`（本仓 `HeadlessInputAndDialogUiTests` 已有先例），
坐标用 `TranslatePoint` 从**控件局部**换算到**根窗口**（Headless 扩展吃的是根坐标）。

🩸 **更正 2（§5 风险 1 作废）**：「move/release 的 ctor 待验」这条 TODO 不再需要 ——
改走 Headless 扩展后不经手那些 ctor。**教训**：spike「编译通过」只证明签名存在，
`RoutedEvent` 为 null 是**运行期**才炸的 —— **可行性的第一层不等于跑得起来**。

**仍开着**：② 拖动位移控件、③ 绘制新建（§4 的风险排序与负控预案不变）。

---

## 补记（阶段 145）：② 也已落地

加了一条**拖动位移**腿：`Drags("CanvasLayer", (60,60) → (110,60))` ⇒ `目标控件X` **40 → 90**
（`DragMode.Move` 支的 `SnapToGrid(_dragStartBounds.X + dx)`；夹具 `SnapStep = 5`、dx = +50 ⇒ 恰好落栅格）。
负控：① 把 `RaisePointer` 的 `Move` 支变空转 ⇒ 红「期望 90、实际 40」；② 把产品侧那句写入换成常量 ⇒ 同一句红。

⚠ 顺带落地了 §3.3 预言的第三处改动：**唯一性键加"指针路径"**（点一下 / 拖一段同名同下标，
只按 `(名字, 下标)` 算会误报"登记了两次"）。

⚠ 新增一条**本视图特有的边界**：`OnPointerPressed` 的**空白支**会起 `DispatcherTimer`（长按框选）
⇒ 表里**不放**"点空白取消选中"那条腿（本层老规矩：完成时刻不由 `Pump` 决定的都不点）。

**仍开着**：③ 绘制新建（牵动绘图工具状态机，§4 里风险最高）。

---

## 补记（阶段 146）：③ 绘制新建也落地 ⇒ 本专题全部落地，`#77` 闭环

第三条腿 = `Arranged(Drags("CanvasLayer",(400,200)→(500,260)), 布景设 DrawTool=Button)` ⇒
**控件数 2 → 3**、「末位控件类型」Button、「末位控件X」**90 → 400**
（`CommitDraw` → `AddControlAt` **直接写 `screen.Controls`**，不靠总线）。

🩸 **本专题要补的第二条边界（落地时撞出来的）**：**指针坐标是"视口坐标"** ——
我一度把夹具改成"另起一块空屏 + 一个目标控件"（想让绘制从 0 数起），结果**三条指针腿全废**：
画布会把屏幕**适配进自己的视口**（缩放/平移），换屏之后原先成立的 `(60,60)` 不再落在控件上。
⇒ 夹具退回 `WindowDepot.Screen`，绘制按 `2 → 3` 判。
**推论**：给这一层写指针腿时，坐标要按**夹具那块屏 + 视口**一起算，换屏等于换坐标系。

🩸 另一条小边界：`末位控件X` 这类"取最后一个"的探针，语义要按夹具数清楚 ——
夹具里 `Controls[^1]` 正好是被拖动的那个控件 ⇒ 拖动腿会把这一格一起动掉（必须声明）。

**三条腿全部落地**：①点中选中 / ②拖动位移 / ③绘制新建。负控 **8 支**（阶段 144/145/146 累计）
逐支红在点名的那条腿上、还原 SHA256 逐字节相同。
