# Dock 真实拖拽：可行性取证 + 回归网 + `ViewLocator.Build` 不 Detach 的实测依据

> 按顺序推进 `docs/2026-09-17-open-items-register.md` §4 第 26 项
> （Dock 真实拖拽路径未模拟 / `ViewLocator.Build` 不脱离旧 Parent）。
> 归档问题单：`docs/archive/2026-08/debug-dock-blank-after-drag.md`（**状态仍 OPEN**）。
> 日期：2026-09-17

---

## 0. 一句话结论

四项测量，其中**一项推翻了此前登记的假设**、**一项给出了反直觉的结论**：

| # | 此前登记的说法 | 本轮实测 |
|---|---|---|
| 1 | "在 headless 里模拟 DragDrop 序列是另一件事，**且不一定可行**" | **不成立** —— Dock.Avalonia 12.1 的 DnD **不走平台 `DragDrop` API**，而是**指针事件**驱动 ⇒ headless 的 `MouseDown/MouseMove/MouseUp` 就能驱动，实测面板真的换了 dock |
| 2 | "归档问题单 dock-blank-after-drag 的机制 = 重组后 `Context` 丢失 ⇒ 解析失败 ⇒ 永久空白" | **在 Dock 12.1 的标签拖拽路径上不复现** —— 把 `ViewLocator.CanResolve` 退回**原始缺陷判据**（只认 `Context is Control`），新写的拖拽用例**照样全绿** |
| 3 | "`Build` 不脱离旧 Parent …… 改它可能把偶发空白换成必崩" | 方向对、**机制要改**：把仍挂着的控件加到新父级**确实会抛**（所以"谁先摘"是硬约束）；但"先摘再挂"**可行**，那份"必崩"担心在这条路径上不成立 |
| 4 | —— | 新策略：**保持 `Build` 不 Detach**（Dock 自己完成了转移，且万一漏摘是**抛异常**而非静默空白），并用测试把上面两条测量钉住 |

---

## 1. 可行性取证：Dock 的拖拽到底怎么实现的

`2026-09-15-dock-view-resolution-contract.md` §7.1 把"真实拖拽未被模拟"列为遗留，
理由是"需要在 headless 里模拟 `DragDrop` 序列 —— 那是另一件事，且不一定可行"。
**先查实现再下结论**：

```bash
strings ~/.nuget/packages/dock.avalonia/12.1.0/lib/net10.0/Dock.Avalonia.dll | grep -iE "DragHandler|OnPointer|DoDragDrop"
# → DockDragContext / OnPointerPressed / OnPointerMoved / OnPointerReleased / _lastPointerPressedArgs
```

程序集里**没有**平台 `DragDrop` API 的痕迹，全是**指针事件**（这正是 `ToolTabStripItem`
按下后能被拖动的原因）。于是可推断：用 headless 的 `MouseDown → MouseMove(LeftMouseButton) → MouseUp`
应该能驱动它。

**实测确认**（探针跑完即删）：

```
tab=found tabCenter=27, 783
before: Toolbox@LeftLowerDock
after : Toolbox@CenterDocDock          ← 真的换了 dock
changed=True  toolboxViewPresent=True  toolboxDescendants=151
```

⇒ 那条"不一定可行"的假设**不成立**。这也说明：**先花半小时探路，比先花一轮做不做决定更省。**

## 2. 回归网：`DockDragRegressionUiTests`（3 条）

| 用例 | 作用 |
|---|---|
| `DraggingToolboxTabIntoAnotherToolDock_KeepsPanelPopulated` | 真按标签中心拖拽 → 断言工具箱**仍挂在可视树上且后代 > 0** |
| `AfterDrag_EveryToolStillResolves` | 拖拽重组后 12 个 Tool 逐个 `Build` 非 null |
| `PanelState_JudgeSelfCheck_DetectsBothAttachedAndDetached` | **判据双向自检**（见下） |

### 2.1 判据为什么看"挂没挂在可视树上"

归档问题单的现场证据是
`[ToolboxView] DETACHED from visual tree. WasParent=DeferredContentControl` 且**此后再无 attached**
—— 即"解析得出来、但没挂上去"。所以光断言 `Build(d) is not null` **打不到**这个缺陷，
必须看**附着状态 + 可视后代数**。

### 2.2 自检：拖拽真的发生了吗

第一版我只断言"拖完还有内容"—— **一次没被驱动的拖拽也会让它绿**（什么都没变 = 面板还在）。
所以补了"承载它的 dock 换了对象"的自检。有意思的是：第一版自检我写的是"必须与别的面板**共处一个 dock**"，
**红了** —— 实测 Dock 对这次放置的处置是**新建/切分出一个 dock**，不是加进目标 dock 的标签组。
于是自检改成"父 dock 引用变了"（这才是"拖拽真的执行了"的准确判据）。

### 2.3 一条**诚实的记录**：这条回归网没有被证明能在已知缺陷上变红

把 `ViewLocator.CanResolve` 退回**原始缺陷判据**（`dockable.Context is Control`）后，
两条拖拽用例**照样全绿** ⇒ **Dock 12.1 的标签拖拽不会清空 `Context`**，归档问题单假设 D 的机制
在这条路径上**不复现**（当年可能是别的路径、别的 Dock 版本，或与那次修复叠加的结果）。
因此：

- 它们目前是**回归网**（能挡住"将来变空白"），但**不是**"在已知缺陷上验证过的判据"；
- 为了不让读者以为它"必然能抓"，补了 §2 第 3 条**判据双向自检**：手工构造
  "解析得出来但没挂上树"的形态断言判据判为未附着（负例）+ 真挂进窗口的断言为已附着（正例）。
  该自检的**可失败性**也用负向验证证过（把判据改成恒真 → 负例立刻红）。

> 这正是本项目反复强调的：**"守卫绿了"与"守卫有能力红"是两件事**，
> 当后者无法用真实缺陷演示时，就用**判据自检**补上，并把差别写清楚。

## 3. `ViewLocator.Build` 不 Detach：三条实测

`docs/2026-09-15-dock-view-resolution-contract.md` §7.2 把它登记为**未验证项**。本轮测了：

| 测量 | 结果 |
|---|---|
| 把**仍挂着**的控件加到新父级 | **抛** `InvalidOperationException`：*The control … already has a visual parent ContentPresenter (Name = PART_ContentPresenter, Host = Window) while trying to add it as a child of Panel*（`Button` 与真实 `ToolboxView` 都验） |
| 先 `ContentControl.Content = null` 再挂到新父级 | **正常**：视觉父级变 `<null>`，随后 `p2=Panel`、无异常 ⇒ **"脱旧 Parent"技术上做得到** |
| 真实拖拽路径是否撞上第 1 条 | **没有**：`DockDragRegressionUiTests` 断言面板拖完仍附着且有内容 ⇒ Dock 自己完成了转移 |

结论：

1. **"谁负责先摘"是硬约束**（不摘就抛），不是整洁问题；
2. 但**当前不需要在 `Build` 里加 Detach** —— Dock 在真实路径上已经做对了；
3. 更重要的是**失效模式的方向**：万一将来漏摘，症状是**抛异常**（响亮、能定位），
   **不是**归档问题单里那种"静默空白"。所以"加 Detach"不是消除静默空白的必要手段。
4. 那份"改它可能把偶发空白换成必崩"的担心**方向对、机制要改**：真正的风险不是"不 Detach 会崩"，
   而是"**加** Detach 时如果摘不干净/摘错对象会崩"。测试把两条都钉住
   （`ViewLocatorReparentContractTests`），免得后人只看登记里那句"未脱离旧 Parent"就顺手加一个。

---

## 4. 负向验证（2 组，全部命中）

| NV | 注入 | 期望红的用例 |
|---|---|---|
| A | `PanelState` 的附着判据改成恒真 | `PanelState_JudgeSelfCheck_DetectsBothAttachedAndDetached`（负例那一半） |
| B | 退回原始缺陷判据 `CanResolve = Context is Control` | **不红** —— 见 §2.3，这正是"机制不复现"的证据（不是失败，是**结论**） |

> NV-B 是这轮唯一一条"注入后**没有**红"的记录，而它本身就是本轮的结论之一。
> 把它如实写出来，比把"注入无效"含糊过去更有价值 —— 否则下一个人会以为
> `DockViewResolutionUiTests.ContextCleared_StillResolves_ThroughFactoryFallback`
> 覆盖了真实拖拽路径（它覆盖的是**解析契约**，不是拖拽）。

---

## 5. 验证

| 项 | 结果 |
|---|---|
| `dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** |
| 全量测试（7 程序集） | **2636 通过 / 0 失败**（App.UiTests 186 → **192**；本轮基线 2630 ⇒ **+6**） |
| 负向验证 | **2 组**：A 命中（判据自检可失败）；B 按预期**不命中**并据此定论（见 §4） |
| `scripts/ci-gate.sh` | 见 `docs/project-status.md` 验证记录 |

---

## 6. 归档问题单的处置（诚实边界）

`docs/archive/2026-08/debug-dock-blank-after-drag.md` **仍标 OPEN**，本轮只往它追加一节复测记录。
理由：

1. 原文的复现步骤是"拖动**项目浏览器**到**工具箱** dock"。它的标签条实测只有 **1 px 高**
   （单面板 dock 不渲染可见标签项），所以我驱动的是**镜像方向**（拖工具箱到装项目浏览器的 dock）
   —— 两者都会让两个 Tool 共处一个 dock，走同一条重组路径，但**不是逐字复现**。
2. 镜像方向**没有**复现空白，且退回原始判据也不红 ⇒ 当年那个症状的成因**仍未定论**
   （可能已被 `ViewLocator` 那次修复顺带解决，也可能是别的拖拽路线：拖到**文档区**、
   拖出成**浮动窗口**、多屏等，这些路径本轮**未覆盖**）。

⇒ 因此：**不标 CLOSED**；登记表 §4 第 26 项收窄为"标签拖拽方向已覆盖并有回归网，
其余拖拽路线（文档区/浮动/多屏）未覆盖"，并新增后续候选。

---

## 7. 阶段 224：把"1 px 标签条"量清（覆盖边界的**确切原因**）

登记 `#26` 里那句「项目浏览器的标签条实测只有 **1 px 高**」当时只是一句观察。这一阶段把它量成**可判据的事实**：

| 事实 | 读数（headless，1280×800，`Settle` 排空惰性加载后） |
|---|---|
| 五条 `ToolDock` | `LeftUpper=[ProjectExplorer]`（**单工具**）· `LeftLower=[Toolbox,Faceplate]` · `RightDock=[Property,Layers,Assets,Navigation,AiChat,ResourceLibrary]` · `BottomDock=[AuditLog,CrossReference]` · `CenterDocDock=[Canvas]` |
| `ToolTabStrip` 高度 | 单工具那条 **1 px**；多工具的是 **34 px**（各 dock 宽度 216~839） |
| `ToolTabStripItem`（可拖把手） | **10 个**：多工具 dock 的每个工具一个（34×42~188）；**`ProjectExplorer` 一个都没有** |
| `ProjectExplorerView` 本身 | **附着且非空**（Bounds 406×216，可视子控件 17）——面板没问题，缺的只是**把手** |

⇒ 归档问题单原文那条"逐字方向"（拖**项目浏览器**到工具箱）**在无头环境里打不到的确切原因**是：
那条 dock 里**只有一个工具** ⇒ Dock 只给出一条 **1 px 高**的标签条、**且不创建标签项** ⇒ 没有可按下、
可拖的对象。**不是"拖不动"，是"没有东西可拖"**。镜像方向（拖多工具 dock 里的面板）覆盖了同一条重组路径。

**判据**（`DockSingleToolTabStripTests`，2 条）：① 单工具 dock 存在且是 `ProjectExplorer`；
② 它的标签项**不在**把手名单里；③ 存在 **1 px 高**的标签条（多工具的 ≥20 px，两边都钉）；
④ 多工具 dock 的把手**在**（否则本面就是"什么都没测"）；⑤ `ProjectExplorerView` 附着且有内容。
第二条判据还把"多工具 dock 的工具集合 == 把手集合"做成 `Assert.Equal`。

**负控 4 支**（`.workbuddy/tmp/s224-nc.py`）：把单工具判据的前提改成"6 个工具的 dock"（读数
`Collection: ["Property"]`）· 去掉惰性加载排空那一步（`Assert.Equal` 集合差）· **维度量错**（量 `Width`
得 216 而不是 `Height` 的 1 —— 这条正是我自己第一版犯的错）· 断言改成与现状相反。

**仍未做（产品决策）**：要让**单工具面板**也有可拖把手，得覆盖 Dock 的 `ToolDock`/`ToolTabStrip`
模板（第三方模板复制，脆弱）或给那条 dock 增加第二个工具（改默认布局）。两条都不是"判据"能定的事
⇒ 记在登记 `#26` 里，未擅自做。

---

## 8. 阶段 225：处置② 落地 —— 给那条 dock 加第二个工具，**原文方向终于可判**

用户对 `#26` 的覆盖边界选了**处置②**（而不是①覆盖 Dock 模板）：**给单工具那条 dock 加第二个工具**。

### 8.1 改了什么

把 **「资产管理」`Assets` 从右栏挪进左侧上区** ⇒ 左上是 `[ProjectExplorer, Assets]`（2 个工具），
右栏仍有 5 个工具。于是：

| | 阶段 224（改前） | 阶段 225（改后） |
|---|---|---|
| 左侧上区 | `[ProjectExplorer]` → 标签条 **1 px**、**无标签项** | `[ProjectExplorer, Assets]` → 标签条 **34 px**、**2 个把手** |
| 四条工具 dock | 一条单工具 | **全部 ≥2 个工具** |
| 每个面板的把手 | 项目浏览器**没有** | **全部都有**（判据 `Assert.Equal(allTools, tabItems)`） |
| 原文方向（拖项目浏览器 → 工具箱） | **打不到**（没有东西可拖） | **可判**（`DraggingProjectExplorerTabIntoTheToolboxDock_KeepsPanelPopulated`） |

⚠ **唯一保留的"单工具 dock"是中央画布视口**（`Canvas`；`CanClose=false`，它不是面板）——
在判据里**显式登记**并断言它确实是 `Canvas`，其它任何单工具 dock 都要红（负控 nc3 证明这条例外不是放水）。

### 8.2 判据的变化（条数不变：改判据 ＋ 补方向）

- `DockSingleToolTabStripTests`（阶段 224 建的）**改判新不变量**：没有单工具**面板** dock ·
  **每个工具都在把手名单里**（含 `ProjectExplorer`）· 四条工具 dock 的标签条高度都 ≥20 px（那条 1 px 的没了）·
  面板仍附着且有内容。
- `DockDragRegressionUiTests` **补一条**：`DraggingProjectExplorerTabIntoTheToolboxDock_KeepsPanelPopulated`
  —— 这就是归档问题单**原文的逐字方向**（拖**项目浏览器**到**工具箱** dock），断言被拖的面板仍附着、
  有内容、能解析、工具数不变（12）。`#26` 的覆盖不再靠"同形方向"顶替。

### 8.3 负控（5 支，`.workbuddy/tmp/s225-nc.py`）

把布局改回去（读数：不变量判据红 `仍有单工具 dock` ＋ 逐字方向红 `找不到面板 'ProjectExplorer' 的标签`）·
再制造一条单工具 dock（左下只留 Toolbox）· 判据里去掉"画布视口"那条例外 · **干脆不拖**
（`没有改变项目浏览器的承载 dock` —— 证明那条判据的拖拽自检不是空转）。

🩸 **nc4 第一版没咬**：我原本把落点改成"它自己那条 dock"，以为承载 dock 不会变 —— 但 Dock 对这类放置会
**新建/切分**一条 dock（阶段 161 就实测过），承载 dock 照样变了。⇒ 要让自检咬，必须让**拖拽真的不发生**。

### 8.4 读数

**门禁**：`GATE_EXIT=0` / **7 步** / 日志 **983 行**（非空 **947**）；负载 `11.85 / 11.64 / 8.80`；
脚本 **29**；路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2318 / 315 / 832 / 8`（合计 **3739**）
＝ **3739 通过 / 0 失败**（判据条数不变：**改了** 1 面 ＋ **补了** 1 条方向）；`MainWindow.axaml.cs` 仍 **2983**。

### 8.5 仍未做

1. 这是**产品/UX 改动**（资产管理与项目浏览器同区）——若更希望它挨着别的工具，改 `HmiDockFactory` 里
   那两处 `VisibleDockables` 即可（判据会跟着红，逼着同步）。
2. 处置①（覆盖 `ToolDock`/`ToolTabStrip` 模板让单工具 dock 也显示标签）**没做**：现在不再需要它。
