# ADR：UI 层是否采用 MVVM（2026-09-13）

- 状态：**已决定（不采用 MVVM 框架；移除误导性依赖）**
- 背景来源：`docs/2026-09-13-project-review-and-optimization.md` §4 P1-3

## 背景

2026-09-13 全面审查发现：`HmiEditor.App` 声明了 `CommunityToolkit.Mvvm 8.4.0`，
但全仓 **0 处**使用 `ObservableObject` / `RelayCommand` / `[ObservableProperty]`。
实际架构是：

- **巨型 code-behind**：`MainWindow.axaml.cs`（审查时 4933 行，现 4434 行）、
  `DesignCanvas.cs` 4222 行、`PropertyPanelView.axaml.cs` 2799 行；无 ViewModel 类。
- **静态事件总线**：面板间通信走 `DockLayoutState` 的 30+ 个进程级静态事件；
  `MainWindow.axaml.cs` 源码注释明确要求"不要把面板数据放在 DataContext 上靠继承传给 View"。
- **手工赋值**：XAML 里 1365 个绑定中 1282 个（94%）只是本地化索引器
  `{Binding [key], Source={x:Static res:AppLocalization.Service}}`；真实数据绑定约 83 个。
- `Dock.Model.Mvvm` 是**真正在用**的：`ToolboxView` / `ResourceLibraryView` /
  `FaceplateLibraryView` 以 `Dock.Model.Mvvm.Controls.Tool` 为基类，`HmiDockFactory` 依赖它。

即：依赖声明与实际架构不一致，会误导后来者。

## 决策

1. **移除 `CommunityToolkit.Mvvm` 依赖**。它当前没有任何代码依赖，保留只会制造"本项目用 MVVM"的错觉。
2. **保留 `Dock.Model.Mvvm`**（Dock 面板基类所需，与 MVVM 框架选型无关）。
3. **本轮不做全面 MVVM 化**，改为"按服务增量抽取"降低巨型 code-behind。

## 为什么不做全面 MVVM 化

| 维度 | 判断 |
|---|---|
| 收益 | 可测试性、可维护性提升（但需要先把 UI 逻辑抽成可测单元才有意义） |
| 成本 | `MainWindow` 4434 行 / ~430 方法 + `DesignCanvas` 4222 行需重写为 VM + 绑定；面板间 30+ 静态事件要改为状态容器 |
| 风险 | 决策时**没有任何 UI 自动化测试**（无 Avalonia.Headless、无 UI 冒烟），重写等于在无网保护下走钢丝。**该前置条件已于同日补齐（见文末补充）** |
| 替代路径 | 已完成的"服务抽取"（`DockEventRoutingDiagnostics`、`GitDialogService`、`BuildPipelineService`）每次都能用编译 + 1648 项测试兜底，风险可控 |

结论：**先补 UI 测试能力，再考虑 MVVM**；否则是一次高风险、低可验证性的大爆炸重构。

## 后果

- 巨型 code-behind 仍然存在，`MainWindow` 仍是维护热点。缓解手段是持续做"服务抽取"，
  优先摘出高内聚、低耦合的块（下一步候选：`WireEvents` 995 行按域拆到 partial 文件、
  Modbus 预览运行时、诊断菜单）。
- 若日后引入 MVVM，需要先满足**反转触发条件**（见下），并作为独立重构轮次立项。

## 反转触发条件（满足其一再重新评估）

1. 引入 `Avalonia.Headless` + 无头 UI 测试，能对面板数据流做断言；
2. 出现第三个需要与主窗口共享同一份 UI 状态的独立窗口/面板（静态事件总线开始明显不够用）；
3. `MainWindow` 服务抽取已做完，剩余量主要是"状态 ↔ 视图同步"这类 MVVM 的强项。

## 补充：反转触发条件 ① 已达成（2026-09-13）

`tests/HmiEditor.App.UiTests` 已引入 `Avalonia.Headless`（30 项测试，随 `dotnet test HmiEditor.slnx`
与 `scripts/ci-gate.sh` 一起跑，无需显示器）：

- 15 个 UserControl 视图的装载 + 布局冒烟（XAML 资源解析、模板缺失、构造期异常）；
- 真实鼠标/键盘输入模拟（命中测试、`Click`、输入进获焦控件）；
- `AppDialogs.ConfirmAsync` 的确定/取消分支与文案；
- `PropertyPanelView` 的属性写回、事件通知与校验器拒绝路径。

因此**本 ADR 的"为什么不做"里唯一不可验证性论据不再成立**。但需要明确两点：

1. 触发条件 ① 的语义是"**重新评估**"，不是"立即迁移"。本 ADR 的决策（不引入 MVVM 框架）
   在本次补充时**依然有效**，未作修改。
2. 后续若推进 MVVM，应按第 3 条触发条件判断切入点：先看 `MainWindow` 剩余体量是否
   已经主要是"状态 ↔ 视图同步"——如果仍是事件编排与命令路由，MVVM 带来的收益有限，
   继续做"按服务抽取 + 补 UI 测试"性价比更高。

同时，第 2 条触发条件（出现第三个需与主窗口共享 UI 状态的独立窗口/面板）尚未达成。
