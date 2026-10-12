# Dock 视图解析契约：`Match` 与 `Build` 判据错位 → 12 处工厂兜底成为死代码

> 对应规划文档 `docs/2026-09-14-optimization-directions.md` 的 **D8 / P2-4**
> （「Dock 拖拽空白收尾」；完成标准原文：*用 headless UI 测试复现「拖拽后工具箱为空」并钉住*）。
>
> 归档问题单：`docs/archive/2026-08/debug-dock-blank-after-drag.md`（**状态仍为 OPEN**）。
> 本轮**没有**模拟真实拖拽，也没有宣称修好了拖拽；修的是一个**可判定的逻辑缺陷**，
> 并把这条链路第一次纳入自动化覆盖。范围边界见 §7。

---

## 0. 一句话结论

`ViewLocator.Match` 只认「`Context` 已经是 `Control`」，而「`Context` 还不是 `Control`」
恰恰是 `Build` 里**工厂兜底分支的前置条件** ⇒ `Match` 返回 false 时 Avalonia 不会调用 `Build`
⇒ **12 处 `RegisterFactory` 全是按构造不可达的死代码**。

一旦 Dock 重组把 `dockable.Context` 抹掉（归档问题单的**假设 D**），该面板
**既没有共享实例、也没有工厂兜底**，永久空白。修法是把两者收口到**同一条判据**
（`ViewLocator.CanResolve`），并第一次给这条链路加上 headless 守卫。

---

## 1. 归因

### 1.1 判据错位（真缺陷，已修）

```csharp
// 修前
public Control? Build(object? data)
{
    if (data is not IDockable dockable) return null;
    if (dockable.Context is Control ctxControl)  instance = ctxControl;   // ① 优先复用共享实例
    else if (_factories.TryGetValue(dockable.Id, out var f)) instance = f();  // ② 工厂兜底
    else return null;
    ...
}

public bool Match(object? data) => data is IDockable { Context: Control };   // ← 与 ② 矛盾
```

`IDataTemplate` 的契约是：**`Match` 为假时框架根本不会调用 `Build`**。
于是分支 ② 只可能在「`Context` 是 `Control`」时被求值 —— 而那时分支 ① 已经命中，
② 永远走不到。**12 处 `RegisterFactory` 一次都不会执行。**

这解释了归档问题单里那句「Dock 重组后 `Context` 变化导致 `Match` 返回 false，Content 变为 null」
（假设 D）为何至今成立：解析器在 `Context` 丢失时**直接放弃**，把"我还能用工厂兜底"这件事
藏在了永远到不了的分支里。

### 1.2 两处「注释与代码不符」（同一次排查顺出来的）

| 位置 | 注释声称 | 代码实际 |
| --- | --- | --- |
| `ViewLocator` 头注释 | 「1. **纯工厂模式** —— 每次 `Build` 创建新实例」 | 早已改成 **Context 优先**（注释是上一版方案的残留） |
| `HmiDockFactory` 头注释 | 「2. `ViewLocator.Build` 时**可靠脱离旧 Parent**」 | `ViewLocator.Build` 里**没有任何脱离 Parent 的动作** |

第二条已按事实改写，并把「共享实例可能仍挂在旧 `DeferredContentControl` 上」列为**未验证项**
（见 §7），而不是偷偷补一句 `Detach()` —— 在无法复现拖拽路径的前提下改这个行为，
很可能把"偶发空白"换成"必崩"。

### 1.3 覆盖缺口

| 层 | 之前 | 现在 |
| --- | --- | --- |
| 孤立视图能否构造+布局 | `ViewSmokeUiTests`（15 个视图逐个跑） | 不变 |
| **Dock 链路**（`ViewLocator` + `HmiDockFactory` + `DockControl`） | **零覆盖** | `DockViewResolutionUiTests`（6 条） |
| 手动诊断 | `Services/DockEventRoutingDiagnostics.cs`（菜单手动触发） | 不变 |

`grep -rn '[Dd]ock' tests/HmiEditor.App.UiTests/` 此前只命中一句注释 —— 这就是为什么
`Match` 写错这么久没人发现。

---

## 2. 做法

### 2.1 单一判据：`ViewLocator.CanResolve`

```csharp
/// <summary>能否为该 dockable 产出内容视图 —— Match 与 Build 的**唯一**判据。</summary>
public static bool CanResolve(IDockable dockable) =>
    dockable.Context is Control || _factories.ContainsKey(dockable.Id);

public bool Match(object? data) => data is IDockable dockable && CanResolve(dockable);

public Control? Build(object? data)
{
    if (data is not IDockable dockable) return null;
    if (!CanResolve(dockable)) return null;            // 与 Match 同一判据
    var instance = dockable.Context as Control ?? _factories[dockable.Id]();
    ...
}
```

> **不变量：`Match(d) == (Build(d) is not null)`。** 有专门的元守卫钉住它（§3.2）。

另外补了 `RegisteredFactoryIds`（已注册的 ID，升序）供守卫与诊断使用。

### 2.2 收口注册键空间（结构上不可能分叉）

`HmiDockFactory` 里原本是**两套手写的键空间**：

```csharp
toolboxTool.Id = "Toolbox";                    // 一套
...
ViewLocator.RegisterFactory("Toolbox", ...);   // 另一套
```

12 处**当前恰好一致**，但没有任何约束阻止它们分叉，而分叉的表现是
「静默回退到工厂、或干脆空白」——不会报错。改为直接传该 tool 自己的 `Id`：

```csharp
ViewLocator.RegisterFactory(toolboxTool.Id, () => { ... });
```

12 处一次性替换（脚本按「最近的 `x.Id = "<同一字面量>"`」配对，逐条断言命中唯一）。

> 这与本项目既有的「让"算一次"成为类型约束」同源：**不是靠守卫提醒，而是让写错成为不可能**。
> 守卫（§3.3）仍保留作为第二道锁 —— 万一将来有人又手写一个字面量。

---

## 3. 守卫：`tests/HmiEditor.App.UiTests/DockViewResolutionUiTests.cs`（6 条）

在真实 headless 会话里**真跑一次** `HmiDockFactory.CreateLayout()`（与
`MainWindow.InitializeDocking` 同序：构造 12 个视图 → `CreateLayout` → `InitLayout`），
再遍历整棵树做断言。两侧集合**全部从真实布局与真实注册表推导**，测试里不手抄任何 ID 清单。

| 用例 | 作用 |
| --- | --- |
| `Walker_ActuallyFindsEveryToolDockable` | **走查器自检**：遍历写错（漏子树/去重键用错）会让下面全部变空跑恒绿；这条先证明"真的走到了那 12 个面板" |
| `Match_AgreesWithBuild_ForEveryDockable` | **元守卫**：逐一断言两判据一致（见 §3.2 的两种状态） |
| `EveryToolDockable_ResolvesToANonNullView` | 每个**叶子**面板都必须解析出非空视图 |
| `RegisteredFactoryIds_MatchLayoutDockableIds` | 键空间**双向**相等（正向：面板不能没工厂；反向：不能有死注册） |
| `ContextCleared_StillResolves_ThroughFactoryFallback` | **归档问题单假设 D 的复现 + 钉住**：把 `Context` 抹掉后必须仍能解析 |
| `DockControl_ActuallyMaterializesPanelViews` | **端到端**：真把 `DockControl` 挂进窗口，跑布局，断言面板视图确实物化进了可视树 |

### 3.1 两个"断言写宽/写窄"的自我纠正

1. **`EveryDockable_...` 一开始要求所有 dockable 都可解析 —— 断言写宽了。**
   实测解析不出的是 `Root`/`Main`/`LeftDock`/`LeftUpperDock`/`LeftLowerDock`/`Center`/`CenterDocDock`/
   `BottomDock`/`RightDock` 这 **9 个容器**：它们的 `Context` 为空、也没注册工厂，
   由 Dock 自己的控件模板渲染 chrome。改为只断言叶子（`Tool`）。
2. **端到端一条一开始物化不出任何视图 —— 环境假设不对。**
   `DeferredContentPresenter` 是**惰性**的，headless 下没有真实渲染循环，可视树里只剩 Dock 自己的 chrome
   （`descendants=6`：Grid / DockCommandBarHost / DeferredContentControl / DeferredContentPresenter /
   ManagedWindowLayer / DockSelectorOverlay）。补上 `Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded)`
   + `Measure`/`Arrange` 三轮后内容才落进可视树。

### 3.2 元守卫为什么必须在**两种状态**下各查一次

第一版只在"刚建好的布局"上校验 `Match == (Build is not null)`，结果**打不到产品侧**：
新布局里 12 个面板的 `Context` 都非空，此时无论哪版判据都通过 ——
实测把 `Match` 退回只认 `Context: Control`，那一版元守卫**照样绿**。

`Context` 缺失才是工厂兜底分支唯一有意义的状态：

```csharp
Check(d, "原始状态");
d.Context = null;          // 模拟 Dock 重组后 Context 丢失
Check(d, "Context 缺失");
```

另注意求值顺序：**必须先 `Match` 再 `Build`** —— `Build` 会把 `Context` 填上，
之后 `Match` 必然为真，顺序反了就成恒绿。

### 3.3 元守卫与 `ContextCleared` 是**互补**的，不是重复

两者分别覆盖两种不同失效模式，NV 各有一组单独命中：

| 失效模式 | 机制 | 唯一能抓到的守卫 |
| --- | --- | --- |
| **判据分叉** | `Match` 与 `Build` 各用一套判据 ⇒ 有分支不可达 | `Match_AgreesWithBuild_ForEveryDockable` |
| **能力丧失** | 两判据一致，但这条判据本身不再认识"有工厂就能兜底" ⇒ 该面板谁也解析不出 | `ContextCleared_StillResolves_ThroughFactoryFallback` |

---

## 4. 负向验证（4 组，红集两两不同）

`.refactor-tmp/run-nv.sh` + `nv.py`；注入器一律 `copyfile` + `os.utime`
（**不用** `copy2`/`cp -p`，避免 mtime 造成的**假红**）；harness 拿不到测试摘要时显式打印尾部输出
（避免构建失败被 `grep` 吞成空白、误读成"这组没红"）。

| NV | 注入 | 红的用例 | 条数 |
| --- | --- | --- | --- |
| 基准 | — | — | 0（6/6 通过） |
| **a** | `CanResolve` 退回只认 `Context is Control`（**复现本轮修掉的缺陷**） | `ContextCleared_StillResolves_ThroughFactoryFallback` | 1 |
| **b** | 注册键分叉（`toolboxTool.Id` → 字面量 `"ToolboxX"`） | `ContextCleared…`、`RegisteredFactoryIds_MatchLayoutDockableIds` | 2 |
| **c** | 只注入一个死注册（`"GhostPanel"`） | `RegisteredFactoryIds_MatchLayoutDockableIds` | 1 |
| **d** | `Match` 恒真（容器也"匹配"，却没有内容可产出） | `Match_AgreesWithBuild_ForEveryDockable` | 1 |
| 还原 | — | — | 0（6/6 通过） |

四组集合两两不同；且 a 与 d **精确对应 §3.3 的两种失效模式**。
基准行的用例数（6）与源码里的用例数一致 —— 这是上一轮的教训：**「失败 0」不等于「全绿」**。

---

## 5. 顺带纠正的两处既有记录（不实措辞）

| 记录 | 原文 | 实测 |
| --- | --- | --- |
| D6（`hmi_actions.c` 权限/凭据） | 「**明写的 TODO**（`:6`、`:47`）」 | `grep -c TODO hmi_actions.c` = **0**。原文是「权限校验**留待后续**接入 `UserRole`」「真实凭据校验**留待后续**接入 `UserRole`/`Permission` 模型」—— 是**声明式延后**，不是 TODO 标记 |
| D8（Dock 空白） | 「fix-plan T5 仅编译通过、GUI 未验证」 | 属实，但**低估了**：不只是"未验证"，而是解析器里有一处**按构造不可达**的分支 |

D6 的措辞已在 `MEMORY.md` 与本节一并更正；**功能缺口本身依然存在**（§7）。

---

## 6. 验证清单

| 项 | 结果 |
| --- | --- |
| `dotnet build HmiEditor.slnx` | 0 警告 / 0 错误 |
| `DockViewResolutionUiTests` | 6/6 |
| 负向验证 | 4 组红集两两不同，还原后 6/6 |
| 全量测试 | **2183 通过 / 0 失败**（7 项目：44+22+75+63+266+1604+109；`App.UiTests` 103 → 109） |
| `scripts/ci-gate.sh` | 5 步全过（**独占运行**，不与 NV 并行 —— 上一轮并行导致结果不可信） |

---

## 7. 遗留（本轮**不做**，已登记）

1. **真实拖拽路径仍未模拟。** 本轮钉住的是解析契约，以及归档证据指认的那个机制
   （`Context` 丢失 ⇒ 解析失败 ⇒ 永久空白）。Dock 自己的 DnD（指针事件 →
   `DockControl` 拖拽处理器 → 重组）**没有**被驱动过。要真正复现"拖拽后工具箱为空"，
   需要在 headless 里模拟 `DragDrop` 序列 —— 那是**另一件事**，且不一定可行。
2. **`ViewLocator.Build` 不脱离旧 Parent。** 共享实例可能仍挂在旧
   `DeferredContentControl` 上。`HmiDockFactory` 头注释曾声称 Build 会"可靠脱离旧 Parent"，
   这是**不实**的（已更正为未验证项）。修它风险高：无法复现拖拽路径时，把偶发空白换成必崩的概率不低。
3. **`docs/archive/2026-08/debug-dock-blank-after-drag.md` 仍是 OPEN。** 本轮不动它的状态 ——
   真实拖拽未被复现，标 CLOSED 是不诚实的。
4. **D6 的功能缺口**（`hmi_login` 接入 `UserRole` + 真实凭据校验）仍未做，见规划文档 P2-2。
5. **D9 AI 流式（SSE + 并行 tool_call）** 未做；侦察同时发现两条取消链路的缺口：
   面板底部的「取消」按钮语义是**放弃预览**（`OnAiCancelRequested` 只回退预览屏幕），
   与 `_agentCts` 是两套；非 Agent 的 `Generate*Async` 调用**没有传 `CancellationToken`**。
