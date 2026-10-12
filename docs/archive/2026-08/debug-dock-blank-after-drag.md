# Debug Session: dock-blank-after-drag

**Status**: [CLOSED]（2026-10-01，阶段 161 —— 根因与修复见 `docs/2026-10-01-dock-float-refusal.md`）  
**Created**: 2026-08-03  
**Symptom**: 拖动项目浏览器到工具箱 dock 后，工具箱内容变空白  
**Repro**: 启动应用 → 拖动项目浏览器标签到工具箱 dock 区域 → 工具箱变空白  
**Environment**: Linux / Avalonia 12 / Dock.Avalonia  

## Hypotheses

### A: DeferredContentControl 销毁重建导致 View 脱离后未重新附着
- Dock.Avalonia 的 DeferredContentControl 在 Dock 重组时销毁，
  原 ToolboxView 实例从视觉树分离（DETACHED），
  但新 DeferredContentControl 未用相同 Content 重新附着，导致永久空白。
- **证据**：日志 Line 16 `DETACHED from visual tree. WasParent=DeferredContentControl`

### B: Dock 重组后 DeferredContentControl 的 Content 被置 null
- Dock.Avalonia 在 insert 操作中对同一 dock 组的 Tool 执行内容重新分配，
  过程中把原 ToolboxView 的 Content 置 null 而未恢复。

### C: DockFactory OnDockableActivated 未触发 Re-attach
- 虽然 OnDockableActivated 被调用，但未执行任何视觉树重新附着操作。

### D: ViewLocator 在 Dock 重组时返回错误的 Content
- ViewLocator 基于 IDockable.Context 匹配，重组后 Context 变化导致 Match 返回 false，
  Content 变为 null。

## Evidence Log

### Pre-fix Evidence (2026-08-03)

```
[ToolboxView] ATTACHED to visual tree. Parent=DeferredContentControl
[ToolboxView] LayoutUpdated: Att=True Vis=True Bounds=518.4x1527.2 Parent=DeferredContentControl ItemsSrcNull=False Items=19 Tpls=4
... (14 more LayoutUpdated, all normal) ...
[ToolboxView] DETACHED from visual tree. WasParent=DeferredContentControl
(no more events — View never re-attached)
```

**关键发现**：ToolboxView 在 Dock 重组时从 DeferredContentControl 分离后，从未重新附着。
这解释了为什么"不管布局变化"的 ItemsSource 绑定方案无效 —— View 实例根本不在视觉树中。

## Next Steps

1. ✅ 收集 pre-fix 证据
2. [ ] 启动 Debug Server 并添加 DeferredContentControl 级别的诊断
3. [ ] 确定 DETACHED 后谁负责重新挂载 Content
4. [ ] 实施最小修复
5. [ ] 验证 post-fix 证据
6. [ ] 清理调试代码

---

## 复测记录（2026-09-17，状态**仍 OPEN**）

**做了什么**：`docs/2026-09-17-dock-real-drag-feasibility-and-reparent-evidence.md`

1. **推翻了"headless 不一定可行"的假设**：Dock.Avalonia 12.1 的 DnD 是**指针事件**驱动
   （`OnPointerPressed/Moved/Released` + `DockDragContext`），**不是**平台 `DragDrop` API ⇒
   headless 的 `MouseDown/MouseMove/MouseUp` 可以驱动。实测拖工具箱标签后
   `Toolbox@LeftLowerDock → Toolbox@CenterDocDock`，视图仍有 100+ 可视后代。
2. **新增回归网** `DockDragRegressionUiTests`（3 条）：真实拖拽后工具箱**仍挂在可视树上且非空**
   （判据看**附着状态 + 可视后代数**，因为本问题单的现场签名正是
   `DETACHED from visual tree` —— 光断言"解析出非 null"打不到它）+ 12 个面板解析链仍可用
   + **判据双向自检**。
3. **假设 D 在这条路径上不复现**：把 `ViewLocator.CanResolve` 退回**原始缺陷判据**
   （只认 `Context is Control`）后，两条拖拽用例**照样全绿** ⇒ Dock 12.1 的标签拖拽**不会清空
   `Context`**。因此这两条目前是**回归网**，**不是**"在已知缺陷上验证过会红"的判据
   （自检补上了这一点）。

**为什么仍标 OPEN**：

- 原文的复现是"拖动**项目浏览器**到**工具箱** dock"，而项目浏览器的标签条实测只有 **1 px 高**
  （单面板 dock 不渲染可见标签项）⇒ 本轮驱动的是**镜像方向**（拖工具箱到装项目浏览器的 dock），
  走的同一条重组路径但**不是逐字复现**；
- 镜像方向未复现空白，且退回原始判据也不红 ⇒ **当年症状的成因仍未定论**；
- **未覆盖的拖拽路线**：拖到**文档区**、拖出成**浮动窗口**、多屏/多窗口重组。

**下一步候选**：把上述未覆盖路线各写一条指针拖拽用例（Dock 的 `DockDragContext`
应同样可驱动），以及"拖成浮动窗口后再停靠回来"这条最容易丢内容的路径。

---

## 结案（2026-10-01，阶段 161，状态 **CLOSED**）

**根因**（不是上面四个假设中的任何一个）：本仓的 Dock 工厂是**视图无关**的
`Dock.Model.Mvvm.Factory` ⇒ "拖出停靠区"被实现成 `FloatDockable()`，它只动**模型**
（`RootDock.Windows` 多一条、面板从原 dock 摘走），而**没有任何东西给那条 `DockWindow` 建宿主窗口**
（实测 `IDockWindow.Host` 恒为 `null`；`grep -rn "HostWindows\|Dock.Model.Avalonia" src/` → 0 命中）
⇒ 面板视图 DETACHED 且**再也回不来**（没有窗口可显示、界面里也没有"窗口"菜单，
而布局持久化只存 `Id/Proportion/IsOpen`、不含 `Windows`）。

**为什么当年没查到**：症状被记成"拖 A 到 B 之后 B 变空白"，而真正的触发条件是
"**放置落点不在任何 dock 上**"（= 浮动），与"拖到哪个 dock"无关；
且它与 `ViewLocator` 的解析链**无关** —— 这解释了 2026-09-17 那次复测里
"退回原始缺陷判据仍全绿"。

**可达性**：`MainWindow.axaml` 里 `DockControl` 只占主窗口**第 3 行**（上面菜单/工具栏、下面状态栏）
⇒ 把标签拖到那些区域就是"落在停靠区之外" —— **用户随手可达**。

**修复**：`HmiDockFactory.FloatDockable` **覆写成空实现**（明确拒绝浮动）⇒
拖到停靠区之外的处置变成"**面板留在原地**"。判据 `DockDragRegressionUiTests` 新增 3 条
（拖到中心区 / 拖到停靠区之外 / `FloatDockable` 契约），负控 3 支（含"把修复退回 ⇒ 两条判据红"）。
**未覆盖范围的结论**：多屏/多窗口在本仓**不可达**（浮动被拒 ⇒ 永远单窗口）；
原文的逐字方向仍打不到（项目浏览器标签条 1 px 高），由镜像方向覆盖。
专题 ⇒ `docs/2026-10-01-dock-float-refusal.md`。
