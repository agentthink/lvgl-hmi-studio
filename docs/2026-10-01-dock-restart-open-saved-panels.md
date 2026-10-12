# 重启「只打开保存的面板」（阶段 165，产品决策落地；闭环 `#80`）

> **产品决策原文**：「被 × 关掉的面板，下次启动只打开保存的面板。」
>
> **一句话**：盘面（`.layout.json`）新增一份「**保存时在可见布局里的工具 id**」（`VisibleToolIds`）；
> 启动时 `ApplyState` 覆盖完 `Proportion`/`IsOpen` 之后，**按这份清单裁剪** —— 不在清单里的工具
> 走 × 那条路（`CloseDockable`）**保持关闭**，用户可以随时从「面板」菜单再打开。
> **旧盘面**（这一版之前写下的文件，没有这份清单）**不裁剪**：`null` ＝"信息缺失"，保持"全部打开"。

---

## 1. 语义（三条，写死在这里）

| 状态 | 进不进 `VisibleToolIds` | 重启后 |
|---|---|---|
| 在可见布局里（正常打开） | ✅ | **打开** |
| 被 × 关掉（`CloseDockable`，从布局整个摘掉） | ❌ | **保持关闭**（面板菜单可找回） |
| 藏起来（`HiddenDockables`）/ 钉住（四张钉列表） | ❌ | **保持关闭**（同上；它们本来也不在可见树里） |
| 旧盘面（`VisibleToolIds == null`） | —— | **全部打开**（向后兼容，不把"不知道"当"一个都不要"） |

⚠ 最后一行是**很容易搞反**的地方：老用户升级后如果按"空集"处理，**所有面板都会消失**。
判据 `Restart_WithLegacyStateWithoutVisibleToolIds_OpensEveryPanel` 专门守它。

---

## 2. 实现（三处，`MainWindow` 行数不变）

1. `LayoutSerializer.CaptureState`：在原有 `DockableState`（Id/Proportion/IsOpen）之外，
   记下**可见树里的工具 id** → `LayoutState.VisibleToolIds`。
   `LayoutState` 新字段带 `null` 语义注释（缺信息 ≠ 空集）。
2. `HmiDockFactory.RestoreLayoutState(layout, state)`：**新的统一入口** —— 先
   `LayoutSerializer.ApplyState`，再按清单裁剪（`CloseDockable`，即 × 那条路 ⇒ 与"刚被关掉"
   同一个状态，`OpenDockable` 能挂回老家）。
3. `MainWindow` 构造期那一行 `LayoutSerializer.ApplyState(...)` 换成
   `_dockFactory.RestoreLayoutState(...)` —— **1 行换 1 行**，`MainWindow.axaml.cs` 仍是 **2983**
   （符合"单文件基线只许变小"的纪律；判据也搬进了工厂 ⇒ headless 够得着）。

---

## 3. 判据与负控

`DockDragRegressionUiTests` **12 → 13 条**（重写 1 条 ＋ 新增 1 条）：

| 判据 | 判什么 |
|---|---|
| `Restart_OpensExactlyTheSavedPanels` | 藏一个（`HideDockable`）＋ 关一个（`CloseDockable`）⇒ **前提自检**：盘面里**有**清单、清单里**没有**这两个、**有**画布（`CanClose = false`，一定在）⇒ 新工厂重建布局 ＋ `RestoreLayoutState` ⇒ **恰好 10 个**、id 无重复、这两个**不在任何 dock 的可见列表**、**也没被塞进 `HiddenDockables`**（裁剪走的必须是 × 那条路）、剩下 10 个**都在某个 dock 里**、附着视图**不少于初始布局**（⚠ 不是"每个都附着"：Dock 只实例化活动标签，非活动标签惰性 —— 初始布局实测 **5/10**）⇒ 恢复腿：`OpenDockable` 再打开 ⇒ **恰好 1 条**且视图附着 ⇒ **面板没丢** |
| `Restart_WithLegacyStateWithoutVisibleToolIds_OpensEveryPanel` | 把清单置 `null`（模拟旧文件）⇒ 重启后**12 个全打开**、被关掉的那个也回来 |

**负控 4 支**（`.workbuddy/tmp/s165-nc.py`，逐支独立日志 + 还原 SHA256 逐字节相同）：

| 控制 | 注入 | 实测红在哪 |
|---|---|---|
| nc1 | `RestoreLayoutState` **不裁剪** | `重启后应当只开 10 个面板，实测 12` |
| nc2 | `CaptureState` 不记清单（永远 `null`） | **前提自检**红（"盘面里没有「可见工具」清单"） |
| nc3 | 旧盘面语义搞反（`null` ⇒ 空集 ⇒ 全裁掉） | **向后兼容判据**红（"所有面板消失"） |
| nc4 | 裁剪改用 `HideDockable` | `裁剪走的不是 × 那条路`（`HiddenDockables` 那条腿） |

判据顺序上有个细节：**先判"落在哪个状态"再判数量** —— `HideDockable` 也把面板移出可见 dock，
但它仍在模型里（`Walk` 走 `HiddenDockables`）⇒ 数量会露馅成 12；先判 `HiddenDockables`
才能让"裁剪走错路"红得指名道姓（nc4 就是这条顺序的证据）。

---

## 4. 诚实边界

1. **裁剪在 `MainWindow` 构造期执行**（`DockControl` 绑定之前）——
   与 `ApplyState` 同一时刻，判据复刻的是同一顺序（`BuildLayout` → `RestoreLayoutState` → 挂窗口）。
2. 判据里的"重启"仍是**同进程内换一个工厂实例重建布局**，不是真重启进程：`Save`/`Load` 写的是
   用户配置目录下的固定文件（会污染开发机真实配置）⇒ 判据直接打 `CaptureState`/`ApplyState`/
   `RestoreLayoutState` 这条链（`VisibleToolIds` 的**落盘/读回**由 `JsonOptionsDefaults.Default`
   的既有属性序列化行为承担，本阶段没有单独判据）。
3. **画布永远在清单里**（`CanClose = false`，× 点不到）⇒ 不会出现"清单为空 ⇒ 全空布局"的常态。
   判据用 `capturedCanvas` 把这条**前提**钉住（万一将来有人给画布开了 `CanClose`，
   这条会先红，提醒"可能需要护栏"）。
4. 判据不判**每个**留下的面板都附着（见 §3 的惰性说明）；它判的是"不比初始布局更少"。
5. 藏起来的面板重启后回到**默认那一格**（不是它被藏之前的位置）——本阶段不判"格子"。
6. **工具 id 改名是这条裁剪的一个升级隐患**：清单按 id 匹配；若将来某个工具的 id 改了，
   用户手里那份**新格式**盘面里就是旧 id ⇒ 改名后的工具会被当成"不在清单里"而**保持关闭**
   （可从「面板」菜单找回，**不丢**）。`Proportion`/`IsOpen` 本来就是按 id 匹配的（同一族隐患）。
   ⚠ 现状**没有**改名，也没有针对性护栏（无判据不加代码）；记在这里是为了将来改名时能想到这一条。

---

## 5. 读数

- **全量 App.UiTests** 与**门禁**见登记册 §81 与 `docs/project-status.md` 阶段 165。
- **产品代码**：3 个文件（`LayoutSerializer` 采集清单、`HmiDockFactory` 新入口、`MainWindow` 1 行换 1 行）；
  **判据夹具**：`tests/HmiEditor.App.UiTests/DockDragRegressionUiTests.cs`（重写 1 条 ＋ 新增 1 条）。
