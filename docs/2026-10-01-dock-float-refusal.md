# 把面板标签拖到停靠区之外 ⇒ 面板消失：根因与修复（阶段 161，闭环归档问题单 `dock-blank-after-drag`）

> **一句话**：本仓的 Dock 工厂是**视图无关**的 `Dock.Model.Mvvm.Factory` —— 它会把"拖出停靠区"
> 实现成 `FloatDockable()`，造出一条 `DockWindow` 记录，但**没有任何东西给它建宿主窗口**
> （`IDockWindow.Host` 恒为 `null`）⇒ 那个面板的视图**从可视树上脱落且再也回不来**。
> 修复：**明确拒绝浮动**（覆写 `FloatDockable` 为空实现），拖到停靠区之外的处置变成"**面板留在原地**"。

---

## 1. 症状与可达性（为什么这是用户可见的）

`MainWindow.axaml` 里 Dock 只占主窗口的**第 3 行**：

```xml
<Grid Grid.Row="3">
    <dock:DockControl x:Name="DockControl" IsDockingEnabled="True" />
</Grid>
```

⇒ 上面是菜单/工具栏、下面是状态栏 —— **"停靠区之外"是用户随手就能把指针放到的地方**。
把任意面板的标签拖到那些区域并松开，Dock 的放置逻辑就判定为"浮动"。

**实测现场**（headless 夹具，`DockControl` 比窗口小，指针落在它之外）：

```
RootDock.Windows = 1（其中有 1 个面板）；承载 dock='LeftLowerDock'；视图 DETACHED、无任何可视后代
```

这正是 2026-08-03 归档问题单 `debug-dock-blank-after-drag` 里那句
`DETACHED from visual tree. WasParent=DeferredContentControl` 且此后再无 attached。

**为什么回不来**：浮动窗口没有被建出来（没有宿主窗口），界面里也没有"窗口"菜单；
而 `LayoutSerializer` 只持久化 `Id/Proportion/IsOpen`、**不含 `Windows`** ⇒ 重启才恢复默认。
⇒ 用户这一拖，那个面板就**在本会话里消失**了。

---

## 2. 根因（两半，缺一不可）

1. **工厂是视图无关的**：`HmiDockFactory : Dock.Model.Mvvm.Factory`（`src/HmiEditor.App/Services/HmiDockFactory.cs`）。
   `FloatDockable` 的父类实现做 `RemoveDockable` ＋ `AddWindow` ＋ `InitLayout` —— 它只动**模型**；
   把 `DockWindow.Host` 填成真窗口是 `Dock.Model.Avalonia` 那一层的事，本仓**既没引它、也没接那条线**
   （`grep -rn "HostWindows\|Dock.Model.Avalonia" src/` → **0 命中**）。
2. **"拖出"这条路径本身是可达的**（见 §1）。

⚠ 归档单当年的四个假设（A `DeferredContentControl` 销毁重建 / B Content 被置 null /
C `OnDockableActivated` 未重挂 / D `ViewLocator` 匹配失败）都**不是**根因 ——
根因是"浮出来的东西根本没有窗口可显示"。这也解释了 2026-09-17 那次复测为什么
"把 `ViewLocator.CanResolve` 退回原始缺陷判据仍全绿"：**那条路径与解析无关**。

---

## 3. 修复：拒绝浮动（而不是实现它）

```csharp
// HmiDockFactory
public override void FloatDockable(IDockable dockable)
{
    // 故意空实现：本仓没有宿主窗口那一层，浮出来的面板会从界面上消失。
    // 拖到停靠区之外的处置 = **面板留在原地**。不要改成 base.FloatDockable(dockable)。
}
```

**为什么不实现浮动**：那要引入 `Dock.Model.Avalonia` 的宿主窗口层，还要定
浮动窗口的创建/关闭/位置持久化/主题/多显示器语义 —— 是一**个新功能**，不是这条缺陷的修复；
而"拖出去面板就没了"是**必须马上止血**的。✅ 若将来要做浮动，把这条覆写去掉并补上宿主窗口层即可，
判据会立刻指出哪些断言要改（见 §4 的"契约"那条）。

---

## 4. 判据（`DockDragRegressionUiTests`）

| 用例 | 判什么 |
|---|---|
| `DraggingToolboxTabIntoAnotherToolDock_KeepsPanelPopulated` | （既有）拖进另一个工具 dock ⇒ 面板仍在（**镜像**了归档单的原始方向） |
| `DraggingToolboxTabIntoCenterArea_KeepsPanelPopulated` | **新**：拖进中心区（本仓中心区是 `ToolDock`，放置点取中心区那幅画布的中心）⇒ 面板仍在 |
| `DraggingToolboxTabOutsideDockControl_NeverLosesPanel` | **新**：拖到停靠区**之外** ⇒ ① 不建 `DockWindow` ② 面板**留在原地** ③ 仍挂在可视树上且非空；**结尾带前提自检**：同一夹具里拖进中心区必须真的改变承载 dock（否则整条用例空转） |
| `FloatDockable_IsRefused_AndPanelStaysWhereItWas` | **新**：直接调 `FloatDockable` ⇒ `RootDock.Windows` 保持 **0**、父 dock 不变、视图仍附着、12 个面板一个不少（钉住修好的契约） |
| `AfterDrag_EveryToolStillResolves` / `PanelState_JudgeSelfCheck_...` | （既有）解析链回归网 ＋ 判据双向自检 |

**负控 3 支**（`.workbuddy/tmp/s161-nc.py`，还原 SHA256 逐字节相同）：

| 控制 | 注入 | 实测 |
|---|---|---|
| nc1 | **把修复退回**（`base.FloatDockable`） | 拖到外面那条 ＋ 契约那条**都红** ⇒ 修复承重 |
| nc2 | 指针手势空转（`steps = 0`） | "拖进中心区没改变承载 dock"红 ⇒ 前提自检有效（不是空转恒绿） |
| nc3 | `PanelState` 恒判已附着 | 既有判据自检红 ⇒ 判据不恒绿 |

---

## 5. 覆盖边界（已如实写进登记 `#26`）

- **多屏/多窗口**：本仓**不存在**（浮动被拒 ⇒ 永远只有一个窗口）⇒ 这条路线**不是"未覆盖"，是"不可达"**。
- **归档单原文的逐字复现**（拖**项目浏览器**到工具箱 dock）：项目浏览器的标签条实测只有 **1 px 高**
  （单面板 dock 不渲染可见标签项）⇒ 指针打不到；仍由"拖工具箱到那个 dock"的**镜像**覆盖（同一条重组路径）。
- 判据只判"视图仍附着且可视后代 > 0"，**不判**布局比例/顺序是否理想（那是另一件事）。
