# `HmiEditor.Scripting` 程序集去留取证

承接 `docs/2026-09-20-dead-type-and-zero-coverage-triage.md`（阶段 37）的「仍未闭环 ②」与
登记表 **#36**。本文**只取证、不改产品代码** —— 删除需拍板。

## 1. 结论摘要

> **⚠ 后续（2026-09-20 同日）：拍板「做强」⇒ 已按选项 A 执行，本节的结论已被取代。**
> 实施记录见 `docs/2026-09-20-lua-syntax-validation.md`：MoonSharp 真解析**搬进**
> `HmiEditor.Core/Services/LuaSyntaxValidator.cs`，`HmiEditor.Scripting` 程序集下架，
> `LuaSnippets` 删除（钩子名与当前契约对不上）。**本文保留作取证过程的历史快照** ——
> §2 的证据链、§3 的两代对照、§6 的四条顺带发现仍然有效。

**（当时写的）建议下架（选项 B）**，理由不是「它是死代码」，而是更硬的四条：

1. **唯一的引用是"引了但从不用"** —— 全仓**零** `using HmiEditor.Scripting`；
2. **它的功能已被 `HmiEditor.Core` 里一套活的实现覆盖**（模板、校验都有对应物）；
3. **它的模板内容已经过时** —— 钩子名与当前运行时契约**对不上**，接进去会生成跑不通的脚本；
4. **它的架构前提已被产品放弃** —— 现方向是「生成 C 代码 + 固件执行」，而它服务的是「编辑器内执行」。

但有一个**真实的反向理由**（见 §4 选项 A）：Core 的校验是**弱校验**（只查「代码里含 `function `」），
而它有 MoonSharp **真解析**。所以 A 不是荒谬选项 —— 只是**不该复用它的模板**。

## 2. 证据链

| # | 事实 | 证据 |
|---|---|---|
| ① | 程序集只有 **2 个类型** | `LuaScriptValidator.cs`（154 行）+ `LuaSnippets.cs`（140 行）+ csproj |
| ② | 依赖 **MoonSharp 2.0.0** | `HmiEditor.Scripting.csproj:11`；CPM 声明 `Directory.Packages.props:47` |
| ③ | **全仓只有两处引用，且都不使用它** | `src/HmiEditor.App/HmiEditor.App.csproj:28`（ProjectReference）+ `HmiEditor.slnx:14`（项目条目）。全仓 **0 条** `using HmiEditor.Scripting` |
| ④ | MoonSharp **只有一个使用者** | 全仓 `MoonSharp` 命中仅 CPM 声明 + 该 csproj ⇒ 下架即**整体退役依赖** |
| ⑤ | 产品的脚本功能**是活的，实现在 `HmiEditor.Core`** | 见 §3 |

### 2.1 活的脚本子系统在 `HmiEditor.Core`（与被取证程序集无关）

| 角色 | 位置 |
|---|---|
| 模型 | `Models/Scripting/HmiScript.cs`、`ScriptBinding.cs`、`ScriptTemplate.cs` |
| 服务 | `Services/IScriptEngine.cs`、`ScriptEngineService.cs`、`ScriptSandboxExecutor.cs` |
| UI | `src/HmiEditor.App/Views/ScriptEditorView.axaml.cs`（左右分栏 / 去抖校验 / 模板插入 / F5 校验 / F6 调试运行 / AI 生成） |
| 工程模型 | `HmiProject.ScriptBindings`（`HmiProject.cs:58`） |
| 引用分析 | `CrossReferenceAnalyzer.cs`（`ReferenceType.ScriptBinding`） |
| 代码生成 | `src/HmiEditor.CodeGen/Templates/lvgl_v9_script.scriban` |
| 测试 | `ScriptingTests.cs`、`Phase3IntegrationTests.cs`、`CrossReferenceTests.cs`、AI 侧 `LuaCompletionServiceTests.cs` 等 |

**`ScriptTemplate.Defaults` 还是「脚本分类清单的唯一来源」** —— AI 脚本生成的 JSON Schema `category` enum
由它派生（`PromptTemplateEngine.RenderSchema`），有守卫锁着。这一条排除「整个脚本功能可以删」的可能。

## 3. 两代实现逐个对照

`ScriptEditorView` 实际调用的是 **Core 那一列**：

| 能力 | Core（**活**，被 UI 调用） | `HmiEditor.Scripting`（死） |
|---|---|---|
| 语法校验 | `ScriptEngineService.Validate`：名称非空 + 内容非空 + **代码含 `"function "`**（三项弱判据） | `LuaScriptValidator.Validate`：**MoonSharp 真解析** + 行号提取 + 中文错误映射（「缺少 end」等） |
| 模板 | `ScriptTemplate.OnClick / OnValueChanged / Timer / Alarm`（英文注释、占位符 `VARIABLE_ID`） | `LuaSnippets.OnClick / …`（**同名 4 项**、中文注释、具体示例值） |
| 模板列举 | `ScriptEngineService.GetTemplates()` | `LuaSnippets.GetAll()` |
| 执行 | `ScriptSandboxExecutor`（自研简化解释器：赋值 + `if/then/else/end`，基于 `ExpressionEngine`，零外部依赖） | 无 |
| 钩子名 | `on_click` / `on_value_changed` / `on_timer` / `on_alarm` | `on_button_click` / `on_value_changed` / `on_timer_tick` / `on_alarm_triggered` |

**钩子名这一行是「下架」的决定性论据**：

- 生成模板 `lvgl_v9_script.scriban:72` 发射 `lua_getglobal(L, "{{binding.function_name}}")` ——
  **函数名是运行时要按名查找的真实契约**，不是注释里的空话；
- 当前契约是 `on_timer` / `on_alarm` 这组（AI 补全服务 `LuaCompletionService.cs:30`、
  AI 生成测试、`Phase3IntegrationTests.cs:395-396` 全部用它）；
- 而 `on_timer_tick` / `on_alarm_triggered` **全仓只出现在 `LuaSnippets.cs` 自己文件里**。

⇒ 把 `LuaSnippets` 接进去当模板用，会把「函数名对不上 `FunctionName`」的脚本插进用户代码里。

## 4. 三个选项

| 选项 | 做法 | 成本 | 收益 | 代价 / 风险 |
|---|---|---|---|---|
| **A. 接进去（只接校验器）** | 把 MoonSharp 解析下沉到 Core，`ScriptEngineService.Validate` 在现有 3 条弱判据外**增加真语法错误**；`ScriptEditorView` 无需改动（它已调 `_engine.Validate`） | 中：Core 新增一个 NuGet 依赖 | 用户从「必须含 function」升级为「第 12 行缺少 end」 | 破坏 Core 注释里写死的前提「**无 NLua 依赖**」；MoonSharp 支持的 Lua 方言与固件 `lv_lua` **未必一致** ⇒ 可能误报；`LuaSnippets` **不能一起接**（已过时） |
| **B. 整程序集下架（推荐）** | 删 §5 清单里的 5 处 | 低：纯删除 | 少一个 never-used 依赖；MoonSharp 整体退役；覆盖率 0% 清单里少 2 项 | 丢掉一份写好的真解析实现（含中文错误映射）；需同步守卫基线 |
| **C. 保留 + 标注** | 现状 + 在 csproj/类上加注释指向 #36 | 最低 | 无 | 死代码继续出现在每份覆盖率 0% 清单里，每次都要重新解释一遍 |

**推荐 B**，前提是接受「编辑器侧只做弱校验」这一现状（这是产品当前的事实选择，写在
`ScriptEngineService` 的注释里）。若哪天要把校验做强，再单独立一项，**从 A 重新评估**，
而不是现在为了"以后可能用得上"留着一份已经过时的实现。

## 5. 若选 B：精确影响面（5 处）

| # | 文件 | 动作 |
|---|---|---|
| 1 | `src/HmiEditor.Scripting/`（目录，3 个文件） | 删除 |
| 2 | `HmiEditor.slnx:14` | 删 `<Project …>` 条目 |
| 3 | `src/HmiEditor.App/HmiEditor.App.csproj:28` | 删 `<ProjectReference …>` |
| 4 | `Directory.Packages.props:47` | 删 `<PackageVersion Include="MoonSharp" …>` |
| 5 | `tests/HmiEditor.Core.Tests/DeadTypeGuardTests.cs:72-73` | `ExpectedDeadTypes` 删 `LuaScriptValidator` / `LuaSnippets` 两行 |

**验证与完成标准**：

1. `dotnet build HmiEditor.slnx` 通过（证明 App 确实不依赖它）；`src/HmiEditor.App/bin/**` 的
   `deps.json` 里不再含 `HmiEditor.Scripting`（重建后自然消失，**不要手改生成物**）；
2. `bash scripts/ci-gate.sh` **7 步全绿、`GATE_EXIT=0`、0 警告**；
3. 八程序集测试数**净减 0**（改小基线会让**主判据变红后转绿**，不是删除用例）；
   注意 `HmiEditor.Scripting` 不在八个测试程序集里，故**总数不变**，只应看到 `DeadTypeGuardTests` 由红转绿；
4. 覆盖率重跑：`HmiEditor.Scripting` 的 `0 / 82` 行从分母消失 ⇒ **总覆盖率会小幅上升**（分母变小）；
5. **先做负控**：只删基线两行、不改产品代码 ⇒ 守卫必须**变红并点名这两个类型**（证明判据在守这件事）。

## 6. 顺带发现的四条（各自独立，不阻塞本决策）

| # | 发现 | 位置 | 性质 |
|---|---|---|---|
| ① | **固件侧 `LV_USE_LUA` 未定义** | `firmware/t113/config/lv_conf.h` 里只有 `LV_USE_FS_STDIO`，无 `LV_USE_LUA` | 生成模板以 `#ifdef LV_USE_LUA` 为开关，未定义时退化到空实现 `void lua_script_init(void* L) { (void)L; }` ⇒ **脚本链路的"最后一跳"未通**（模板注释还写了「Requires: lv_lua_bridge.h」）。**这是链路缺口，需登记**。**⚠ 后续复核（2026-09-20）：本行低估了缺口** —— `LV_USE_LUA` **不是 LVGL 的配置项**（全 firmware 树零命中，`lv_lua_bridge` / `lv_binding_lua` 同样零命中）⇒ 不是"缺一个 `#define`"，而是**整条桥接从未存在过**。见 `docs/2026-09-20-lua-dialect-alignment.md` §2 |
| ② | `ScriptBinding.FunctionName` 的文档举例是 `on_button_click` | `ScriptBinding.cs:20` | 与 `ScriptTemplate` 的 `on_click` **不一致**（举例 vs 模板）；不影响运行，但会误导使用者 |
| ③ | 文档把「MoonSharp 校验」写成**现有能力** | `docs/2026-08-08-hmi-control-research-comparison.md:93-94` 与 `:315` | 若选 B，**这两处必须收口**（否则是假陈述）—— 同 2026-09-20 全库收口的同族问题 |
| ④ | 原始实现计划里它本该有 **5 个文件 + 1 个测试项目** | `docs/superpowers/plans/2026-07-29-hmi-editor-implementation.md:1895-1901` | 实际只落地 2 个（`LuaScriptEngine` / `ScriptingApi` / `ScriptTemplateProvider` 与 `tests/HmiEditor.Scripting.Tests` **从未建**）⇒ 它本身就是**半成品**，这降低了「丢掉完整实现」的顾虑 |

## 7. 决策结果（已闭环）

**当时的提问**：编辑器侧的 Lua 校验要不要做强？

**结果（2026-09-20 同日）**：**要** ⇒ 已按选项 A 执行。但**实施方式与本节最初设想的 A 不同**：
不是「Core 引用 Scripting」，而是**先搬家、再下架**。原因是条硬约束 ——
`HmiEditor.Scripting` 本身 `ProjectReference` 着 Core（它返回 Core 的 `ScriptValidationResult`），
Core 反引它就是**循环依赖**；所以只剩「把真解析搬进 Core」一条路。
`LuaSnippets` 则**删除而非搬迁**（钩子名过时，接进去会生成跑不通的脚本）。

实施记录：`docs/2026-09-20-lua-syntax-validation.md`（含 MoonSharp 方言实测表、
判据与负控、以及顺带抓到的两个「纸面上的校验」）。
