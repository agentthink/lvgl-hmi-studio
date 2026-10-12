# 布局持久化与重启：面板不会丢，但「藏 / 钉 / 闭」的设置不保留（阶段 164）

> **一句话**：登记 `#80` 原先写着"重启后回到可见区（**不丢**）"，那是**从源码推的**（`LayoutSerializer`
> 只走 `VisibleDockables`）。本阶段把它**量出来**：藏一个、关一个面板之后存盘，用**新工厂重建布局**
> （＝应用装载时同一条路径）并应用盘面状态 ⇒ **12 个面板全在、无重复、视图都附着**。
> 顺带在探路时撞到一颗**真的雷**：`ApplyState` **边枚举边应用**，遇到 `IsOpen = false` 的盘面项会抛
> `Collection was modified` —— 而它就是**启动期**那条路径（`MainWindow` 构造期、无 try/catch）。

---

## 1. 量到的现状（阶段 164 判据读数）

`DockDragRegressionUiTests.RestartAfterCloseOrHide_KeepsEveryPanel_NoDuplicates`：

```
存盘前 tools=11 位置=(无) / (无)          # 藏起来的 + 被 × 摘掉的都不在可见 dock 里
CaptureState：19 条；含 AuditLog(藏)=False；含 Toolbox(闭)=False   # ⚠ 两个入口的状态都没进盘面
重启后 tools=12（去重 12）
  AuditLog(藏)   → BottomDock×1 视图附着
  Toolbox(闭)    → LeftLowerDock×1 视图附着
```

⇒ **"不丢"成立**：重启后每个面板都回到某个 dock、视图真的重新附着（不是空白壳）、没有重复插入。
⇒ **代价**：**可见性设置不保留** —— 藏起来 / 关掉的面板重启后**都会自己回到可见区**。

⚠ 这份判据**不**把"关掉的面板重启后回来"当成要守的契约 —— 它只守**"不丢面板"**这条不变量；
"要不要记住藏/钉/闭"是登记 `#80` 的**产品决策**（见 §4）。

---

## 2. 探路时撞到的雷：`ApplyState` 边枚举边应用（**启动期异常**）

为 `#80` 试写"把藏起来的也存进去"那一版时，`ApplyState` 抛：

```
InvalidOperationException: Collection was modified; enumeration operation may not execute.
  at LayoutSerializer.ApplyRecursive(IDockable dockable, Dictionary`2 state)  (LayoutSerializer.cs:102)
```

**机制**：`ApplyRecursive` 遍历 `dock.VisibleDockables`，对每个子项调 `SetIsOpen(child, saved.IsOpen)` ——
而 `IsOpen = false` 会驱动 Dock **自己把那个 dockable 从这张列表里摘掉**（`true` 会插回）⇒
**枚举中改集合**。

**为什么这是真缺陷（不是探路副产品）**：
- `DockableState.IsOpen` 是 `bool`，`false` 是这个盘面格式**本来就允许**的值（手改 / 旧版本 / 将来实现
  `#80` 都会产生它）；
- 调用点在 **`MainWindow` 构造期**：`var savedState = LayoutSerializer.Load();
  LayoutSerializer.ApplyState(_dockLayout, savedState);`（`MainWindow.axaml.cs:325`），**没有 try/catch**
  ⇒ 一个这样的 `.layout.json` ＝ **启动期异常**。

**修法**（`LayoutSerializer.ApplyRecursive`）：应用前先 `dock.VisibleDockables.ToList()` **快照**子项列表。

**判据** `ApplyState_WithIsOpenFalseEntry_DoesNotThrow`：造一份只有
`{ Id = "AuditLog", Proportion = 0.25, IsOpen = false }` 的盘面 ⇒ ① **不抛异常**；
② 工具**不丢**（模型里仍是 12 个 —— "关掉" ≠ "丢掉"）；③ 没有重复；④ **这个标志真的被应用了**
（那一个不在任何 dock 的可见列表里 —— 否则"没抛异常"这条会恒绿）。

---

## 3. 判据与负控

`DockDragRegressionUiTests` **10 → 12 条**：

| 判据 | 判什么 |
|---|---|
| `RestartAfterCloseOrHide_KeepsEveryPanel_NoDuplicates` | 藏一个（`HideDockable`）＋ 关一个（`CloseDockable`）⇒ **前提自检**：藏真的进了 `HiddenDockables`、存盘前模型里 11 个（被摘掉的不在）、`CaptureState` **不含**这两个（`#80` 的机制）⇒ 新工厂重建布局 ＋ `ApplyState` ⇒ **12 个工具**、id 无重复、两个面板各出现 1 次且**视图附着** |
| `ApplyState_WithIsOpenFalseEntry_DoesNotThrow` | 见 §2（不抛异常 ＋ 不丢 ＋ 无重复 ＋ 标志真的生效） |

**负控 4 支**（`.workbuddy/tmp/s164-nc.py`，逐支独立日志 + 还原 SHA256 逐字节相同）：

| 控制 | 注入 | 实测红在哪 |
|---|---|---|
| nc1 | `CaptureRecursive` 也走 `HiddenDockables`（＝破坏 `#80` 的机制） | **前提自检**红（"`#80` 的前提不成立，赶紧复核"） |
| nc2 | 判据把"重启"做成**复用同一个布局** | `重启后布局里应当有 12 个工具` 红 |
| nc3 | `ApplyState` 应用时**误删一个可见工具**（模拟"状态即权威"） | 同上（证明这条不变量真的被判据守住） |
| nc4 | `ApplyRecursive` 退回"边枚举边应用"（去掉快照） | `IsOpen = false` 判据红（`启动期异常`） |

---

## 4. 与登记 `#80` 的关系

| `#80` 的两半 | 本阶段 |
|---|---|
| **"不丢"**（重启后面板还在） | ✅ **已量到并钉成判据**（§1）；机制（藏/钉/闭都不进盘面）由**前提自检**守住 |
| **"跨会话保留藏/钉/闭"** | **仍开着 · 产品决策**：要实现得先定语义（记住到什么程度？被 × 关掉的面板下次启动该不该回来？），并注意本阶段这层地基 —— 实现时必须先把状态写进盘面、再让 `ApplyState` **安全地**把它应用回来（§2 那颗雷现在已拆） |

---

## 5. 诚实边界

1. 判据里的"重启"是**同一进程内换一个工厂实例重建布局**（`BuildLayout()` 再走一遍），
   不是真的重启进程/重新读文件：`LayoutSerializer.Save/Load` 走的是用户配置目录下的固定文件
   （会污染开发机的真实配置）—— 所以判据直接打 `CaptureState`/`ApplyState` 这对纯函数。
2. 判据只判"面板都在、没有重复、视图附着"，**不判**比例（`Proportion`）是否被正确恢复
   —— 那是另一条腿（`Proportion` 的反射读写路径本阶段没量）。
3. 藏起来的那一个（`HiddenDockables`）在被 `Walk` 覆盖后才计入 12；本阶段没有断言
   "重启后它一定回到**原来那一格**"（它回到的是新建布局里默认的那一格）。

---

## 6. 读数

- **全量 App.UiTests**：见登记册 §80。
- **产品代码**：1 个文件（`src/HmiEditor.App/Services/LayoutSerializer.cs`，快照子项列表 ＋ 注释）；
  **判据夹具**：`tests/HmiEditor.App.UiTests/DockDragRegressionUiTests.cs`（＋2 判据）。
