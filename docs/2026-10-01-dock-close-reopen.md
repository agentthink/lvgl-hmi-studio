# 关闭面板之后从「面板」菜单再也打不开（阶段 162，新登记 `#79`）

> **一句话**：标签栏的 × 走 `Dock.CloseDockable`，它把工具**从布局里整个摘掉**
> —— 既不在 `VisibleDockables`、也没进任何 `HiddenDockables`（本版 Dock 只有 `IRootDock` 有那个列表）。
> 于是「面板 → 某面板」那条 `OpenDockTool` 用 `FindDockable` **找不到它**、**点了没反应**；
> 而本仓**没有"重置布局"**可点 ⇒ 用户在**本会话里丢了一个面板**，只能重启。
> 修复：记住每个工具的**老家**（创建布局时的承载 dock），"再打开"时把它挂回去 —— 实测视图**能重新附着**。

---

## 1. 现场（判据先红、后修）

`DockDragRegressionUiTests.CloseThenReopenViaMenu_RestoresThePanel` 的实测读数（修复前）：

```
before=(Attached:True, 38)                        # 面板正常
afterClose=(Attached:False, 38)                   # 关掉：视图从可视树脱落（预期的）
visibleAfterClose=False                           # 不在任何可见 dock 里（预期的）
hiddenAfterClose=False / hiddenIn=<none>          # ⚠ 也没进 HiddenDockables
ownerAfterClose=BottomDock                        # 只剩 Owner 指针还指着老家
foundNull=True                                    # ⚠ FindDockable 找不到它 ⇒ 菜单点了没反应
tools=11                                          # 12 → 11：工具从布局里真的少了一个
```

⇒ **可达性**：Dock 的标签栏对 `CanClose == true` 的工具渲染 × 按钮（本仓只有画布显式设了
`CanClose = false`，等于作者预期别的面板可关）；判据里 `audit.CanClose` 也当场读数。

---

## 2. 修复：记住"老家"，再打开时挂回去

1. `HmiDockFactory` 在 `CreateLayout()` 末尾遍历布局，把 **工具 id → (工具实例, 承载 dock)** 记进
   `_toolHomes`（每次重建布局时刷新，永远对应最新那张布局）。
2. 新增 `TryRestoreClosedTool(layout, id)`：按老家 `AddDockable(...)` ＋ `IsOpen = true` ＋ 激活。
   ⚠ **这条只对"闭"成立** —— 阶段 163 量到另外两个入口（藏 / 钉）不能这么走，见 §5 第 2 条与
   `docs/2026-10-01-dock-reopen-pin-and-hide.md`。
   ⚠ 两条护栏：工具**还在布局里**时返回 `false`（调用方该走 `FindDockable` 那条）；
   传进来的**不是它那张布局**时也返回 `false`（`_toolHomes` 里的老家不在这张布局的可见树里）。
3. 「面板」菜单那一跳收成 **`HmiDockFactory.OpenDockable(layout, id)`**：先 `FindDockable` →
   有就置顶激活；**没有就回退到挂回老家**。
   ⚠ 这段逻辑**从 `MainWindow.OpenDockTool` 移进工厂**，一举两得：判据**够得着产品路径**
   （`MainWindow` 的私有方法在 headless 里打不到），而且 `MainWindow.axaml.cs` **变小**
   （2992 → **2983**，符合本仓"单文件基线只许变小"的纪律）。

---

## 3. 判据与负控

| 判据 | 判什么 |
|---|---|
| `CloseThenReopenViaMenu_RestoresThePanel` | 前置：`CanClose == true`、关掉后**真的**不在布局里（`FindDockable` 找不到、工具数 11）⇒ 再走**产品路径** `OpenDockable` ⇒ 工具**回到某个 dock** 且**视图重新附着、可视后代 > 0**、工具数回到 12 |
| `MainWindowMenu_GoesThroughTheFactoryOpenDockable` | **接线契约**（源码）：菜单那一跳必须走 `_dockFactory.OpenDockable(`，且内联的 `FindDockable(` 已不在 `MainWindow` |

**负控 4 支**（`.workbuddy/tmp/s162-nc.py`，还原 SHA256 逐字节相同）：

| 控制 | 注入 | 实测 |
|---|---|---|
| nc1 | `TryRestoreClosedTool` 不真的挂回去（去掉 `AddDockable`） | 再打开判据红 |
| nc2 | `CreateLayout` 不登记"老家" | 再打开判据红（回退查不到老家） |
| nc3 | `OpenDockable` 不做回退（= 改动前那条"点了没反应"） | 再打开判据红 |
| nc4 | `MainWindow.OpenDockTool` 改回内联 `FindDockable` | **接线契约**红 |

---

## 4. 与阶段 161 那一刀的关系（同一族）

两刀是同一族缺陷：**"这套 Dock 集成里有会静默失效的路径"**。

| | 阶段 161（浮动） | 阶段 162（关闭） |
|---|---|---|
| 触发 | 拖到停靠区**之外** | 点标签栏的 × |
| 现状 | 工具进了 `RootDock.Windows`，**没有宿主窗口** ⇒ 视图脱落 | 工具**从布局摘掉**、连 `HiddenDockables` 都不进 |
| 恢复路径 | 无（没有浮动窗口 UI） | 无（没有"重置布局"；菜单 `FindDockable` 找不到） |
| 处置 | **拒绝浮动**（面板留在原地） | **记住老家、再打开时挂回去** |
| 为什么处置不同 | 浮动本仓没实现（要做是新功能） | "再打开"是**已有功能**、只是被绕过 ⇒ **补齐它** |

⚠ 共同的教训：**"关掉/拖走的那个东西去哪了"必须有人负责** —— 要么明确拒绝、要么明确挂回来；
两者都不做时，症状是"面板凭空消失"，而**菜单点了没反应**（`try/catch` 还把异常吞了）。

---

## 5. 诚实边界

1. 判据只判"回到某个 dock 且视图重新附着"，**不判**它回到**原来那一格**的布局比例/顺序是否理想
   （老家是 `BottomDock`，挂回去就是进 `BottomDock`）。
2. **钉住（pin）/ 自动隐藏** 那条路本阶段**没覆盖**（下一阶段补齐：见
   `docs/2026-10-01-dock-reopen-pin-and-hide.md`）—— ⚠ 而且**本阶段这个修法在那条路上是错的**：
   同一个"菜单里点一下"其实有**三个入口**（闭 / 藏 / 钉），本阶段的 `AddDockable` 修法只对"闭"成立，
   "藏"会被插成**两条同名项**、"钉"会**留下钉条条目**。这里保留当时的结论与这个教训。
3. `_toolHomes` 持有工具实例的强引用（与工厂已有的 12 个 `_*Tool` 字段同寿命）；
   它**每次 `CreateLayout()` 重建**，因此不会指向旧布局的工具。
