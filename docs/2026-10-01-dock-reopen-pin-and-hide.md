# 「面板」菜单重开：藏起来的面板被插成两条、钉住的清不掉（阶段 163）

> **一句话**：阶段 162 的修法（`AddDockable` ＋ `IsOpen = true`）只在**"被 × 摘掉"**那条路上验过；
> 这套 Dock 里同一个"菜单里点一下某面板"其实有**三个入口**（**闭**／**藏**／**钉**），
> 各走各的落点，混着走都有用户看得见的脏状态：
> **藏**的会被插成**同一个 dock 里两条同名项**；**钉**的会**面板回来了、钉条条目留着不清**。
> 本阶段把三条入口分开判、分开修，各配一条判据。

---

## 1. 三个入口，三种落点（都是实测）

| 入口 | 触发 | 工具去哪了 | `FindDockable` 看得到？ | 工具数 |
|---|---|---|---|---|
| **闭** | 标签栏的 ×（`CloseDockable`） | **从布局里整个摘掉**（连 `HiddenDockables` 都不进），只剩 `Owner` 指针 | 否 | 12 → **11** |
| **藏** | 自动隐藏那条入口（`HideDockable`） | 进 `RootDock.HiddenDockables` | 否 | **12**（还在模型里） |
| **钉** | 点 **dock chrome 上的钉按钮** `PART_PinButton`（`DataContext` = 那张 `ToolDock`） | 进 `BottomPinnedDockables`（本例），**视图脱落**（钉条是折叠态） | 否 | **12**（还在模型里） |

三条入口的共同点：**`FindDockable` 都看不见它** ⇒ 全都要靠 `OpenDockable` 的回退那一跳。

---

## 2. 现场一：**藏**起来的面板被插成两条（判据先红、后修）

阶段 162 的 `TryRestoreClosedTool` 面对"藏在 `HiddenDockables` 里"的工具时走的是 `AddDockable`：

```
hiddenAfterHide=True  藏在哪=(无)          # 藏成功了（不在任何 dock 的可见列表里）
逐步：AddDockable → BottomDock×1           # 我们插了一条
      IsOpen=true → BottomDock×2           # ⚠ Dock 自己把它放回 owner，又插一条
      激活      → BottomDock×2
产品路径 OpenDockable → BottomDock×2  tools=12   # 同一个 dock 里两条同名项
```

⇒ **`IsOpen = true` 这一步就是重复的触发点**：工具还在 `HiddenDockables` 里时，Dock 自己会把它
放回原来的 dock，我们再 `AddDockable` 就是第二遍。

**修法**：`IsHidden(...)` 为真时**只置 `IsOpen = true`**（让 Dock 自己放回去），不 `AddDockable`。

---

## 3. 现场二：**钉**住的面板回来了，钉条条目留着（判据先红、后修）

用户**真的点** dock chrome 上那个 `PART_PinButton`（指针 `MouseDown`/`MouseUp`，`PART_PinButton`
只长在 **`ToolDock` 的 chrome** 上、**不在**标签栏里 —— 实测 `ToolTabStripItem` 内一个按钮都没有）：

```
点前 BottomDock×1  (Attached:True, 38)
点后 出现在=(无)  (Attached:False, 38)  tools=11  # 从可见 dock 移出、视图脱落（钉条折叠态）
     钉列表=B:[AuditLog]  IsDockablePinned=False  # ⚠ 这一行是"枚举口径的假象"，见 §5
```

修复前走产品路径 `OpenDockable`：面板**回来了**（`BottomDock×1`、视图附着、`tools=12`），
**但 `钉列表=[B]` 还留着** ⇒ 钉条/钉住高亮与 dock 里的面板**同时存在**，是用户看得见的脏状态。

**修法**：`IsDockablePinned(...)` 为真时走 **`UnpinDockable`** —— 实测它把工具放回原 dock
**并重新附着视图**（`BottomDock×1` ＋ 38 个可视后代）、**钉列表清空**。

⚠ 另一件事：`RestoreDockable`（"把藏起来的工具放回原 dock"）对**钉住**的工具**没用**
（实测：调用后仍在 `(无)`、钉列表不变、视图仍脱落）；它对**藏**起来的工具也只恢复成员关系、
**不重新附着视图**（阶段 162/163 实测 `(False,38)`）—— 所以"藏"那条用的是 `IsOpen = true`。

---

## 4. 修复后的三条入口（`HmiDockFactory.TryRestoreClosedTool`）

```
if (IsPinned(layout, tool))  UnpinDockable(tool);          // 钉：Dock 负责放回并重新附着
else if (IsHidden(layout, tool))  tool.IsOpen = true;      // 藏：只置 IsOpen，别自己插
else { AddDockable(home.Home, tool); tool.IsOpen = true; } // 闭：按老家挂回去
// 之后统一：按 `Owner`（真正落回去的那张 dock）激活
```

---

## 5. 自纠：`IsDockablePinned` 没有"漏判"，是我的枚举口径有盲区

中途我一度判定"**点 dock chrome 钉按钮之后 `IsDockablePinned` 返回 `false`**，所以只看 API 会漏"，
并据此加了一条"再扫四张钉列表"的判据分支。复核发现：**那是假象** ——

- 当时的读数来自 `ToolsOf(layout).Where(t => factory.IsDockablePinned(t, layout))`，
  而测试里的 `Walk` 只走 `VisibleDockables` ＋ `HiddenDockables` ＋ `PinnedDock`，
  **不走 `Left/Top/Right/BottomPinnedDockables` 四张列表** ⇒ 被钉走的工具**根本没被枚举到**，
  所以那个列表是空的（"钉住 = 空"）；
- 直接问 API（`factory.IsDockablePinned(audit, layout)`）在**两种钉法**下都返回 `true`。

处置：**删掉那条多余的"扫列表"分支**（无判据不加代码），把 `IsPinned` 收成一句 API 判定；
**同时把测试的 `Walk` 补上四张钉列表**（否则以后还会有人被同一个假象骗），
判据里的前提自检改成"钉住之后布局模型里的工具数**仍是 12**"（钉住只是移出可见区、不该丢工具）。

---

## 6. 判据与负控

`DockDragRegressionUiTests` **8 → 10 条**，新增：

| 判据 | 判什么 |
|---|---|
| `HiddenTool_ReopenedViaMenu_AppearsExactlyOnce` | `HideDockable` 之后**前提自检**（真进了 `HiddenDockables`、不在任何 dock 的可见列表里）⇒ 走**产品路径** `OpenDockable` ⇒ 工具在全部 dock 的 `VisibleDockables` 里**恰好出现 1 次**、已不在 `HiddenDockables`、**视图附着**、工具数 12 |
| `PinnedViaDockChrome_ReopenedViaMenu_ComesBackAndClearsThePin` | **真的点** `PART_PinButton`（指针手势）⇒ 前提自检（进了钉列表、移出全部 dock、视图脱落、工具数仍 12）⇒ `OpenDockable` ⇒ 恰好 1 条、**四张钉列表都空**、视图附着、工具数 12 |

**负控 6 支**（`.workbuddy/tmp/s163-nc.py`，逐支还原 SHA256 逐字节相同）：

| 控制 | 注入 | 实测红在哪 |
|---|---|---|
| nc1 | 「藏」也走 `AddDockable`（= 阶段 162 旧形态） | 出现 **2** 次（`BottomDock×2`）—— 重复插入 |
| nc2 | 「藏」那一支什么都不做（不置 `IsOpen`） | 出现 **0** 次（`(无)`）—— 没放回来 |
| nc3 | `IsHidden` 恒 `false`（⇒ 退回 nc1 形态） | 出现 2 次 —— 证明这条分支真的在起作用 |
| nc4 | 判据不驱动隐藏（去掉 `HideDockable`） | **前提自检**红（"隐藏路径没被驱动，本用例空转"） |
| nc5 | 去掉「钉」那一支（钉住的也走 `AddDockable`） | `钉条里还留着它（B）`—— 钉住状态没被撤销 |
| nc7 | 钉住手势不点（`PART_PinButton` 那一下去掉） | **前提自检**红（"钉住手势够不着，本用例空转"） |

⚠ nc6 曾想当"`IsDockablePinned` 只看 API 会漏"的负控，**实测是绿的**（不具区分度）——
它就是 §5 那场自纠的引信，故**没有**这一支。

---

## 7. 诚实边界

1. **自动隐藏（把工具放进 `HiddenDockables`）在本版产品里没有专门的按钮**：`IDockable` / 能力策略
   （`CanClose`/`CanPin`/`CanFloat`/`CanDrag`/`CanDrop`/`CanDockAsDocument`）里**没有 `CanHide`**，
   站点只为画布设了 `CanClose = false`。判据用**工厂公开 API** `HideDockable` 驱动它
   —— 用户侧最接近的手势是"钉住之后再解开"（钉条那条路），本阶段的**钉**判据走的就是真手势。
2. 判据不判"钉条/钉住按钮的图标状态是否随之更新"（headless 里没有像素比对）；
   判的是**模型状态**（四张钉列表、所在 dock、视图附着）。
3. **布局持久化仍然不含"藏/钉"状态**：`LayoutSerializer.CaptureRecursive/ApplyRecursive` 只走
   `VisibleDockables`（阶段 162 实测 `capturedAfterHide=False`）⇒ 藏起来/钉住的面板**重启后自然回到
   可见区**（不丢，但"钉住"这个用户设置不会保留）。这是**产品决策面**，已登记为 `#80`。
4. 判据只钉住"被钉走之后菜单那一跳"，**不判**"钉条上的条目本身点击是否可用"（那是另一条手势）。

---

## 8. 读数

- **全量 App.UiTests**：**652 通过 / 0 失败**（连跑 2 次，各 31 s；阶段 162 为 650，**＋2**）。
- 门禁见登记册 §79。
- **产品代码**：1 个文件（`src/HmiEditor.App/Services/HmiDockFactory.cs`）；
  **判据夹具**：`tests/HmiEditor.App.UiTests/DockDragRegressionUiTests.cs`（＋2 判据、`Walk` 补四张钉列表）。
