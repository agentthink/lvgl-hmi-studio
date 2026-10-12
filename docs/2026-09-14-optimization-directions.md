# zhishu-hmi 优化方向（2026-09-14）

> 审查日期：2026-09-14
> 审查基线：`main` @ `9f14135`，工作区干净（`git status` 无输出）
> 方法：全量构建 + 全量测试 + 固件 host 测试 + 静态扫描 + 关键路径抽样复核
> 前置文档：`docs/project-status.md`（状态基线）、`docs/2026-09-13-project-review-and-optimization.md`（上轮全面审查）
> 说明：本文所有数字均为本次现场实测，标注了可复现命令；**不重复上轮已闭环的项**。

---

## 1. 实测基线（2026-09-14）

| 检查项 | 命令 | 结果 |
|---|---|---|
| 构建 | `dotnet build HmiEditor.slnx -c Release` | ✅ **0 错误 / 0 警告**（13.06s，全 14 项目） |
| 全量测试 | `dotnet test HmiEditor.slnx -c Release --no-build` | ✅ **1788/1788**（Core 1288、AI 237、Communication 75、Device 63、App.UiTests 62、Simulator 41、Hardware 22） |
| 固件 host 测试 | `bash firmware/t113/tests/run_host_tests.sh` | ✅ **162 项全过**（通讯/变量表 70 + 触摸 16/16/15 + 板级 18/18/9） |
| Shell 语法 | `bash -n` × 20 个受跟踪脚本 | ✅ 20/20 通过 |
| 脚本副作用守卫 | build.sh 不得 `sed -i` 改写源码 | ✅ 通过 |

**关于 3 个测试失败的澄清（重要）**：本机直接跑 `dotnet test` 时 Core.Tests 有 3 项失败，
报 `Failed to create '/tmp/hmi-font-test-*/main.c' ... because the disk was full`。
本机 `/tmp` 是 **10MB tmpfs**，属环境限制而非代码缺陷——
改用可用 TMPDIR 后相关测试 **55/55 全过**。但这条暴露了一个真实脆弱点，见 §3-D4。

**顺带提醒**：`/home` 已用 **91%**（1.4T / 1.5T，剩 136G）；`src` + `tests` 的 bin/obj 占 **3.6G**。

---

## 2. 结论速览

上轮审查（09-13）的阶段 0（让门禁真能失败）、阶段 1（闭环补齐，软件侧）、阶段 2（生成器/UI/通信治理）
**主体已闭环**：`MainWindow` 从 5023 行治理到 2928 行、两条绑定刷新路径合并且有对拍测试、
`CSource` 结构化断言迁移 281 处、headless UI 测试能力落地、通信层并发整改。

剩下的问题不再是「能不能跑」，而是四类**尚未被任何治理轮次触碰**的结构性缺口：

| # | 根因类别 | 一句话 | 严重度 | 是否被上轮提及 |
|---|---|---|---|---|
| A | 生成链无中间表示（IR） | Core 内嵌第二套 C 生成器，宏定义与 CodeGen 逐字重复 | 高 | 部分（只说 canvas 三份） |
| B | UI 单类膨胀 + 渲染路径每帧分配 | `DesignCanvas` 4222 行零 partial；71 处 `new SolidColorBrush` 全内联 | 中高 | 否 |
| C | 工程化可复现性配置缺失 | 无 `global.json` / `.editorconfig` / 中央包版本 / `README` | 中 | 否 |
| D | 验证盲区与老账 | Renderer 死代码、9 处源码 grep 测试、14 个根目录产物未清 | 中 | 是（未做） |

---

## 3. 根因分类与证据

### A 类：生成链缺中间表示 → 同一规则 2~4 处平行实现

| 现象 | 证据（本次实测） |
|---|---|
| **Core 里内嵌第二套 C 代码生成器** | `src/HmiEditor.Core/Services/SimulatorProjectExporter.cs`：**1312 行**、**596 处** `AppendLine`、**122 处** `lv_` 字符串发射 |
| **资源宏定义逐字重复** | `SimulatorProjectExporter.cs:1069,1153` vs `src/HmiEditor.CodeGen/CodeGenerator.cs:154,377`：`#define IMG_PATH_{id} "A:" SIM_ASSET_DIR "/{file}"` 两处**字符串完全相同** |
| 重复已造成过实际缺陷 | `CodeGenerator.cs:118` 注释即一例踩坑记录（「导致后续控件引用 `IMG_PATH_xxx` 时出现"未声明标识符"编译错误」） |
| 同一控件最多四份平行实现 | 设计器 `DesignCanvas.RenderXxx` + `CodeGenerator` + `SvgToLvglConverter` / `ProcessControlCodeGen` / `ShapeControlCodeGen` |

**根因**：`HmiEditor.Core` 不引用 `HmiEditor.CodeGen`（依赖方向本身是**正确**的，
经实测 Core/CodeGen/Simulator/Renderer/Scripting **均无 `using Avalonia`**，无反向依赖），
但两者之间**没有共享的中间表示**，于是 sim 工程导出只能复制一份字符串模板。
上轮修的 canvas API / 缓冲尺寸缺陷，正是这类复制品的必然产物。

### B 类：UI 单类膨胀 + 渲染路径每帧分配

| 现象 | 证据 |
|---|---|
| **DesignCanvas 是当前唯一的巨型单类** | `src/HmiEditor.App/Controls/DesignCanvas.cs` = **4222 行，零 partial 拆分**（对比：`MainWindow` 已治理至 2928 行 + 6 个 `Wiring.*.cs`） |
| **每帧重分配画笔，全部内联无缓存** | `new SolidColorBrush` **71 处**、`new Pen(` **41 处**，散落在渲染路径（363 / 397 / 723 / 763 / 1346 等） |
| 重绘触发密集 | `InvalidateVisual()` **41 处**（含拖拽/选中/属性变更路径） |
| 其他热点文件 | `CodeGenerator.cs` 3438 行（非 partial）、`PropertyPanelView.axaml.cs` 2804 行 |

**根因**：immediate-mode 自绘 + 笔刷未缓存；DesignCanvas 在 MainWindow 的 7 步治理中一次都没被碰过。
上轮审查把「画布性能热点（`HitTestControl` LINQ 全排序、`ParseColor` 每帧分配）」列为**待复现**，本次确认该类模式仍存在。

### C 类：工程化可复现性配置缺失

| 缺什么 | 现状（实测） |
|---|---|
| SDK 版本固定 | ❌ 无 `global.json` |
| 代码风格强制 | ❌ 无 `.editorconfig`；无 `TreatWarningsAsErrors` / `EnableNETAnalyzers` / `AnalysisLevel` |
| 包版本单一来源 | ❌ 无 `Directory.Packages.props`；`src/Directory.Build.props:8` 显式 `ManagePackageVersionsCentrally=false`；`Microsoft.NET.Test.Sdk 17.12.0` 在 **7 个**测试 csproj 里重复声明 |
| 构建入口文档 | ❌ 无根 `README.md`、无 `LICENSE` |
| 构建入口唯一性 | ⚠ `build/*.ps1`（5 个，硬编码 `C:\Program Files\dotnet\dotnet.exe`）与 `scripts/ci-gate.sh` **双轨**，谁是真入口不明确 |

### D 类：验证盲区与老账

| 项 | 证据 | 状态 |
|---|---|---|
| **D1 Renderer 是死代码** | `src/HmiEditor.Renderer/DesignSurfaceRenderer.cs` = **13 行**空 `Render()`；`HmiEditor.App.csproj` 引用它但**无任何实例化**，**零测试** | 上轮 P2-3 已列，未做 |
| **D2 源码 grep 式测试残留** | **9 个**测试文件用 `File.ReadAllText` 读产品源码做断言（`PropertyPanelPersistenceGuardTests` 读 `DesignCanvas.cs`、`SimulatorLoopTests`、`MockFontLoadingTests`、`MockProjectEndToEndTests`、`SecurityConfigConsumptionTests`、`AppThemeServiceTests`、`DeviceLogicTests`、`HmiDeployServiceTests`、`MockImageLoadingTests`） | 反模式：重构即碎、行为不变也碎 |
| **D3 第二大生成器零直接覆盖** | `SimulatorProjectExporter`（1312 行）无专门测试类，仅被 Mock 类测试间接触达 —— 与 A 类叠加即高风险 | 未做 |
| **D4 临时目录假设** | 3 个测试把 10KB+ 文件写 `Path.GetTempPath()`，tmpfs 环境直接误报失败 | 本次新发现 |
| **D5 仓库卫生** | 根目录 **16 个**被追踪的临时产物：5 个诊断 md + 9 张 png + 2 个 RS485 调试脚本；另有 2 个被 `.gitignore` 忽略却同样散落在根目录的文件（`debug-dock-blank-after-drag.md`、`inspect_dock.csx`） | 上轮 P2-3 已列，**本轮已归档**（见 §4.1） |
| **D6 产品闭环（软性缺口）** | `firmware/t113/app/runtime/hmi_actions.c`：报警/趋势/登录**已有实现**（按候选屏名跳转），但**权限校验与真实凭据校验是明写的 TODO**（`:6`、`:47`）；且依赖屏幕命名约定（`alarm`/`报警列表` 等），屏名不匹配时**只打日志不提示用户** | 部分实现 |
| **D7 设计-代码单一事实源缺失** | `design-previews/` 仅 2 个 `.design` + 1 个 `colors_and_type.css`，**编辑器不消费**它们；设计预览与编辑器模型无共享 | 上轮已列 |
| **D8 Dock 拖拽后面板空白** | `debug-dock-blank-after-drag.md` 自标 **OPEN**，fix-plan T5 仅编译通过、GUI 未验证 | 未解 |
| **D9 AI 无流式响应** | `src/HmiEditor.AI` grep `stream` **0 命中**（会话记忆已在：`AgentEngine.cs:43` `priorHistory` + `IProgress<AgentRunStep> stepProgress`） | 待续 |

---

## 4. 优化方向（按投入产出排序）

### P0 —— 低风险高收益，可合并为一次提交

| # | 事项 | 涉及文件/模块 | 验证与完成标准 |
|---|---|---|---|
| P0-1 | **仓库卫生**：14 个根目录产物归档或移除 | `git rm` + 引用这些路径的文档（`designer-hardcode-audit.md` 等） | `git ls-files \| grep -E '^[^/]+\.(png\|md)$'` 为空；`git status` 干净 |
| P0-2 | **删 Renderer 死代码**（或明确接上它） | `src/HmiEditor.Renderer/`、`HmiEditor.App.csproj`、`HmiEditor.slnx` | 解决方案仍 **0 错 0 警**、1788 测试不变 |
| P0-3 | **补工程配置**：`global.json`（钉 SDK）+ `.editorconfig` + `Directory.Packages.props`（中央版本） | 根目录 + `src/Directory.Build.props` | 构建零警告；改一个包版本只需改 1 个文件；`Microsoft.NET.Test.Sdk` 重复声明从 7 → 1 |
| P0-4 | **清掉临时目录脆弱点**：3 个测试固定使用项目内可控临时目录，或把 `IOException` 转成明确的「磁盘空间不足」信号 | `SimulatorLoopTests`、`MockFontLoadingTests`、`MockProjectEndToEndTests` | 在 10MB tmpfs 下跑也给出可读原因，而非伪装成断言失败 |
| P0-5 | **补根 `README.md`**（构建/测试/烧录/门禁入口） | 根目录 | 新同学按 README 能跑通 `scripts/ci-gate.sh` |

**为什么先做这组**：全部零行为风险，且其中 P0-3 是后续所有治理的复现性前提——
没有 `global.json` 和中央包版本，任何「在我机器上能过」都无法证伪。

### 4.1 P0 执行结果（2026-09-14 当日完成）

| # | 事项 | 结果 |
|---|---|---|
| P0-1 | 根目录产物归档 | 16 个被跟踪 + 2 个未跟踪 → `docs/archive/2026-08/`（附 README 说明来历），git 识别为 **rename**、历史保留；`.gitignore` 加 `!docs/archive/**` 使归档区内的 `*.csx` / `debug-*.md` 保留追踪；根目录只剩工程入口 4 个文件 |
| P0-2 | 删除 Renderer 死代码 | `HmiEditor.Renderer` 已删，并从 `HmiEditor.slnx` 与 App 的 `ProjectReference` 移除；**src 项目 14 → 13** |
| P0-3 | 工程配置 | `global.json`（SDK 10.0.112）+ `.editorconfig` + 根 `Directory.Packages.props`（16 个包单点声明，**36 处** Version 属性从 13 个 csproj 移除）；`ManagePackageVersionsCentrally=true` 实测生效 |
| P0-4 | 测试临时目录 | 新增 `Support/TestTempDir.cs`（逐个真实探测 + 安全清理）；44 处 `finally` 裸 `Directory.Delete` 收口；**本机 1785/1788 → 1788/1788** |
| P0-5 | 根 README | 已补：结构 / 环境 / 构建 / 测试 / 门禁 / 固件入口 / 常见问题 / 工程约定 |

**提交**：`579cf4a chore(repo): 归档根目录产物、删除 Renderer 死代码、补工程化基线`、
`3138865 test/fix: 测试临时目录健壮性、门禁脚本可移植性、状态文档`（均**未推送**）。

**验证**：`dotnet build -c Release` 0 错 0 警；`dotnet test` 在 Release 与 Debug 下均 **1788/1788**；
`CI_REQUIRE_FIRMWARE_TESTS=1 bash scripts/ci-gate.sh` **4 步全过**（脚本语法 20/20、构建、全量测试、固件 host 162）。

### 4.2 执行过程中新发现（P0 之外）

| 发现 | 证据 | 处置 |
|---|---|---|
| **测试会污染真实仓库（高危）** | `GitWorkflowServiceTests` 会真实执行 `git add/commit`。首次跑验收时把 `TMPDIR` 指到了仓库内，git 向上发现父仓库 `.git`，**在项目仓库里生成了一条 message 为 `x` 的提交** | 已用 `git reset --soft` 撤销；测试已加固为显式避开 git 工作树。**结论：这类测试的临时目录必须在任何 git 工作树之外** |
| **测试存在间歇性失败** | 两次 `verify.sh` 分别失败在 `MockImageLoadingTests` 与 `AlarmEngineTests`，而单独跑 `dotnet test` 均 1788/1788 | 根因未定位，建议单开一轮排查（怀疑静态状态泄漏或并发时序） |
| **`finally` 中的裸 `Directory.Delete`** | 44 处。.NET 中 `finally` 抛出的异常会替换原始异常，磁盘紧张时最易触发，会掩盖真实失败原因 | 已收口为 `TestTempDir.Delete` |
| **`ci-gate.sh` 存在静默假门禁** | 第 29 行 `mapfile < <(...)` 依赖 `/dev/fd`，受限环境下报错后仍以「0 个脚本」判定通过 | 已改为临时文件收集 + 非空校验 |
| **`verify.sh` 日志路径不一致** | 第 22 行已用 `${TMPDIR:-/tmp}`，但第 24/55/90 行硬编码 `/tmp/*.log`；本机 `/tmp` 仅 10 MB，LVGL 全量编译日志会撑爆它 | 已统一为 `$TMP_ROOT`，并新增临时目录可用空间前置检查 |

**关于端到端验收的如实说明**：`scripts/verify.sh` 本次在本机**未跑通**（退出码 1），
但已逐步骤手工复现证明**生成链本身正常** —— `SimExport` 完整编译 LVGL 9.5 并链接出 `hmi_sim`
（日志末尾 `✅ 编译成功!`），`--selftest` 命中 PASS。失败由三者叠加造成：
① 本机 `/tmp` 只有 10 MB；② 本次会话的 WorkBuddy CLI 注入了 `safe-delete` shim，
它拦截 `rm -rf` 时自身写报告文件失败（stderr 可见 `safe-delete-common.sh: 行 41`）；
③ 上述测试间歇性失败。**在普通终端复跑不受 ② 影响**，且 ① 已由空间前置检查转化成明确指引。

### P1 —— 结构治理，每项独立一轮

| # | 事项 | 涉及文件/模块 | 验证与完成标准 |
|---|---|---|---|
| P1-1 | **画布性能专项**：Brush/Pen 提为字段或按色缓存，主题变更时统一失效（顺带复核 `HitTestControl` 每帧 LINQ 与 `ParseColor` 分配） | `src/HmiEditor.App/Controls/DesignCanvas.cs` | 先测基线（`GC.GetAllocatedBytesForCurrentThread` 或 profiling），拖拽 100 控件时渲染路径零 `new Brush/Pen`；截图回归一致 |
| P1-2 | **按域拆 DesignCanvas**（只加 partial，不改行为，复制 MainWindow 的套路） | `DesignCanvas.Render/Gauge/Tank/Interaction/HitTest.cs` | 测试零改动通过；`DesignCanvas.cs` 行数下降且**明确说明类本身大小未变** |
| P1-3 | **消灭源码 grep 式测试**：9 个文件逐步换行为测试（项目已有 `ControlTextStyleResolver` 成功路径可复制） | 上述 9 个测试文件 | `grep -rl "File.ReadAllText" tests/` 只命中 Support 工具 |
| P1-4 | **给 `SimulatorProjectExporter` 补专门测试类** | `tests/HmiEditor.Core.Tests/SimulatorProjectExporterTests.cs` | 覆盖：宏生成、资源复制、main.c 装配、缺失资产回退、路径转义 |

### P2 —— 架构级，需单独排期

| # | 事项 | 涉及文件/模块 | 验证与完成标准 |
|---|---|---|---|
| P2-1 | **抽 IR / 统一发射器**（A 类根因）：① 先收「C 字面量 + 标识符 + 格式串」为唯一入口；② 再抽 `IControlEmitter`（创建/刷新/参数三段），收敛模拟器、设备、导出三套实现；③ `IMG_PATH_`/`FONT_PATH_` 宏生成上移为共享函数 | `SimulatorProjectExporter.cs`、`CodeGenerator.cs`、新 `HmiEditor.CodeGen.Abstractions`（或下沉契约到 Core） | 把 `RefreshBindingsParityTests` 的对拍模式扩展到「宏与控件发射器」；生成输出对既有 Golden 工程**逐字节不变** |
| P2-2 | **权限/凭据闭环**：`hmi_login` 接入 `UserRole` 模型，凭据真实校验 | `firmware/t113/app/runtime/hmi_actions.c`、`hmi_actions.h`、生成器的动作回调 | **「接入 `UserRole`」已落实（2026-09-16）**：权限模型下沉设备链路 —— 编辑器生成 `generated_security.h`，动作回调带权限门，host 测试 34 条断言覆盖「未登录拒绝受限动作」+「受限动作被短路」；「设计器可配角色权限」是既有能力（`UserManagerView` 的 9 个权限位复选框 + `ProjectSettingsView` 的运行模式下拉）。见 `docs/2026-09-16-permission-model-sinking.md`。**遗留**：真实凭据校验（需凭据存储 + 哈希下发 + 登录屏契约） —— **已完成（2026-09-16）**：运行时用户表进工程 + 哈希下发 + 登录屏取值（`lv_textarea_get_text`）+ 设备端 SHA-256 校验与失败锁定。见 `docs/2026-09-16-runtime-credential-verification.md`（P2-2 全部关闭） |
| P2-3 | **设计-代码单一事实源**：`.design` 升级为结构化模型，`colors_and_type.css` tokens 映射进 `PaletteDefaults` | `design-previews/`、`src/HmiEditor.Core/Design/PaletteDefaults.cs` | 设计预览的色板改动能单向流到编辑器，且由测试锁定（无裸 hex 残留） |
| P2-4 | **Dock 拖拽空白收尾** | `src/HmiEditor.App/ViewLocator.cs`、`Services/DockEventRoutingDiagnostics.cs` | 用 headless UI 测试复现「拖拽后工具箱为空」并钉住 |
| P2-5 | **AI 流式响应**：`final_answer` 逐步返回（SSE）+ 并行 tool_call | `src/HmiEditor.AI/Inference/*`、`Agent/AgentEngine.cs` | 长任务过程中 UI 有逐步轨迹；取消可中断（`ChatEntry.IsRunning` + CTS 已有） |

---

## 5. 建议的下一步

- **只做一件事**：P0-1 + P0-2（半天内、零风险，立刻让仓库与上轮审查结论对齐）。
- **想要结构性收益**：**P1-1 画布性能专项**——这是当前唯一一处有明确性能风险、且从未被任何治理轮次触碰的模块。
  建议先量基线再改，避免「凭感觉优化」。
- **不建议现在做**：P2-1（抽 IR）虽然根因级别最高，但它会同时改动生成器两条主路径 + 导出器，
  在 P1-4 补上 `SimulatorProjectExporter` 的测试网之前动手，回归半径过大。

---

## 6. 本次审查的置信度与未覆盖项

- **已亲自复现**：构建 0/0、1788 测试、162 项固件 host 测试、20 个脚本 `bash -n`、
  宏定义重复（`SimulatorProjectExporter:1069,1153` vs `CodeGenerator:154,377`）、
  DesignCanvas 行数与分配计数、Renderer 死代码与引用关系、14 个根目录产物清单、
  9 个源码 grep 测试文件清单、包版本重复 7 次、`hmi_actions.c` 实现现状、AI 无流式命中 0。
- **未覆盖**：T113 交叉编译链整编与实机验证、RS485/触摸硬件链路、LVGL 版本升级矩阵、
  AI Provider 联网行为与密钥安全、硬件工程电气正确性。
- **来自辅助探索、建议二次复现**：`MainWindow`/`PropertyPanelView` 的逐行方法清单、
  画布 `HitTestControl` LINQ 与 `ParseColor` 的具体分配量（需 profiling 而非静态计数）。
