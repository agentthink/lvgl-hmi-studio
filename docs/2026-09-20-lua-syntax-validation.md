# 编辑器侧 Lua 校验做强：把 MoonSharp 真解析搬进 Core

承接 `docs/2026-09-20-scripting-assembly-triage.md`（阶段 38 取证）—— 那里的决策点是
「编辑器侧 Lua 校验要不要做强」，**本轮拍板：做强**。本文件记录实施方式、实测数据与已知边界。

登记表 **#36 ②**。相关：`docs/2026-09-20-coverage-tooling.md`（阶段 36）、
`docs/2026-09-20-dead-type-and-zero-coverage-triage.md`（阶段 37）。

## 1. 做了什么（一句话）

`ScriptEngineService.Validate` 从「查代码里含不含 `function `」升级为 **MoonSharp 真语法解析**，
错误带**行号 + 中文说明 + 方言提示**；`HmiEditor.Scripting` 程序集随之下架，
其中唯一有价值的部分（真解析）**迁入 `HmiEditor.Core`**。

**UI 零改动** —— `ScriptEditorView` 一直调 `_engine.Validate(script)` 并把 `Errors[0].Message`
显示在校验状态行，所以行号与中文说明**自动出现在界面上**。

## 2. 为什么只能「搬家」，不能「让 Core 引用它」

我最初想的选项 A 是「Core 引用 `HmiEditor.Scripting`」——**不成立**：

`HmiEditor.Scripting.csproj` 里 `ProjectReference` 着 `HmiEditor.Core`
（因为 `LuaScriptValidator` 返回 Core 的 `ScriptValidationResult` / `ScriptError`）
⇒ Core 反过来引用它就是**循环依赖**。

所以只剩一条路：**把真解析搬进 Core**。这同时解决了另一个问题 ——
程序集名 `HmiEditor.Scripting` 一直在暗示「脚本功能在这里」，而实际活的脚本子系统在
`HmiEditor.Core`（见 triage 文档 §2.1）。名字消失，误解的根也跟着消失。

## 3. MoonSharp 方言实测（实施前做的，决定了措辞）

探针：`.workbuddy/tmp/moonsharp-probe/probe.cs`（MoonSharp 2.0.0，25 个用例）。
**MoonSharp 实现的是 Lua 5.2**：

| 写法 | 结果 |
|---|---|
| `a // 2`（整除）、`1 & 3`、`1 \| 3`、`1 ~ 3`、`1 << 2`、`8 >> 2`（位运算） | ❌ `unexpected symbol` |
| `local x <const> = 1` / `<close>`（Lua 5.4 属性） | ❌ `unexpected symbol near '<'` |
| `goto` + `::label::` | ✅（5.2 已有） |
| `bit32.band`、`table.pack` | ✅（5.2 标准库） |
| `0x1p4` 十六进制浮点 | ✅ |
| `function f(...) local a, b = ... end` 可变参数 | ✅ |
| 中文标识符 `local 变量 = 1` | ✅（MoonSharp 未按 C locale 限制） |
| `math.type`、`utf8.len` 等 5.3 库**语法** | ✅（语法层只查语法，不查库是否存在） |

⇒ **方言差异是真实的**：设备端若用 Lua 5.3+，用户写 `a & b` 会出现
**编辑器报错、设备能跑**的误报。处理方式见 §4 的 `LuaDialect.Hints`。

顺带实测确认了「剥注释」的必要性：

| 判据 | `-- function ghost()\nlocal a = 1` |
|---|---|
| 裸 `Contains("function ")` | **True（被骗过）** |
| 剥注释后再查 | False（如实） |

## 4. 实现

### `Core/Services/LuaSyntaxValidator.cs`（新增，static）

| 成员 | 作用 |
|---|---|
| `Validate(string?)` | `new Script().LoadString(code)` —— **只解析不执行**；捕获 `SyntaxErrorException` |
| `HasFunctionDefinition(string?)` | 剥注释后查 `\bfunction\b` |
| `StripComments(string)` | 剥行注释与块注释（`--[[ ]]` / `--[==[ ]==]`），**换行一律保留**（行号不漂移），字符串字面量原样保留 |

方言文案不在这里 —— 已收进 `LuaDialect.Hints`（**单一事实源**）：原来是本类里的私有
`DialectHints`，6 条文案各自写死「Lua 5.2」；现在全部**从基准派生**，见
`docs/2026-09-20-lua-dialect-alignment.md` §6。

`ScriptEngineService.Validate` 现在是三段（**语法错误放最前**，因为 UI 只显示 `Errors[0]`，
而它最具体）：

1. `EMPTY_NAME`（保留）
2. `EMPTY_CODE`（保留）
3. 非空时：**`LUA_SYNTAX_ERROR`（新，带行号）** → 然后 `NO_FUNCTION`（判据升级，见下）

`NO_FUNCTION` 的**判据升级**：从 `Contains("function ")` 换成 `HasFunctionDefinition`
（剥注释）。它是**契约检查**而不是风格建议 —— 运行时 `dispatch_*` 靠
`lua_getglobal(L, "{{binding.function_name}}")` 按名查找，没有函数体的脚本在设备上必然静默失败。

### 错误消息形态

```
第 3 行: 此写法属于 Lua 5.3 的按位与 &（编辑器校验基于 Lua 5.2）；若目标设备的 Lua 版本更高，此处应视为误报
第 5 行: 代码不完整，可能缺少 end
第 2 行: 字符串没有闭合（缺少结尾的引号）
```

## 5. 两个「纸面上的校验」——顺手抓到的

搬的过程中发现旧实现有两处**从未生效**的代码，两者都是「形状正常、永远不命中」：

| # | 缺陷 | 根因 |
|---|---|---|
| ① | **`ScriptError.Line` 恒为 0** | `ExtractLineNumber` 拿 `ex.Message` 去匹配 `\((\d+),\d+\)`，但**坐标只在 `ex.DecoratedMessage` 里**（`chunk_1:(3,14): …`）。`Message` 只有 `unexpected symbol near '&'` ⇒ 正则永远不命中 |
| ② | **「代码不完整，可能缺少 end」提示从未触发** | 该映射查的是 `'<eos>'`，而 MoonSharp 2.0.0 实测报 **`'<eof>'`** |

两条都已修：① 改解析 `DecoratedMessage`（并加兜底：格式变了就取总行数，**不谎报第 0 行**）；
② 两个标记都收。

> 这条与阶段 36 的 `hit` / `hits` 是**同族**：判据不报错、也不抛异常，
> 只是**永远返回一个看起来正常的坏值**。

## 6. 判据与负控

`tests/HmiEditor.Core.Tests/LuaSyntaxValidatorTests.cs`，**31 条**（Theory 展开后）：

| 组 | 内容 |
|---|---|
| 真解析 | 合法通过 / **缺 end 必须报出行号**（`Line > 0` + 消息含「第 N 行」）/ 字符串未闭合 / 空内容视为合法（职责在调用方） |
| **方言钉桩** | `//` `&` `\|` `>>` 必须被判错，**且消息必须含「Lua 5.3」与「Lua 5.2」**；`<const>` 同理。5.2 特性（`goto`/`bit32`/`table.pack`/`0x1p4`/可变参数）必须通过 |
| 剥注释 | 行注释、两种块注释、**行数不变**、字符串里的 `--` 不被误删 |
| 函数定义 | 真定义 True；**行注释与块注释里的假函数必须 False** |
| 集成 | **元判据**：`ScriptTemplate.Defaults` 的 4 个内置模板必须全部通过真语法校验（否则用户点一下「插入模板」就看到红字） |
| **负控** | 取真实模板、抠掉最后一个 `end` ⇒ 必须红；**同场正控**：未破坏的模板必须过（否则"会红"没有信息量） |

**现有用例零破坏**：`ScriptingTests` 的 4 条 + `Phase3IntegrationTests` 的
`ScriptEngineService_RegisterValidateBind_Integration` 全部照原样通过；
`HmiEditor.Core.Tests` 全量 **2076 条**通过。

## 7. 影响面与验证（实际执行的）

| # | 改动 | 验证 |
|---|---|---|
| 1 | 新增 `Core/Services/LuaSyntaxValidator.cs` | 31 条测试 |
| 2 | `ScriptEngineService.Validate` 升级 + 类注释更新 | 2076 条通过 |
| 3 | `Core.csproj` 加 `PackageReference MoonSharp` | 见下 |
| 4 | 删 `src/HmiEditor.Scripting/`（3 个跟踪文件，`git rm`） | 构建产物列表里不再有它 |
| 5 | `HmiEditor.slnx` 删项目条目 | 同上 |
| 6 | `App.csproj` 删 `ProjectReference` | **App 的 `deps.json` 里 `HmiEditor.Scripting` 命中数 = 0**；`MoonSharp.Interpreter.dll` 随引用链进 App 输出 |
| 7 | `DeadTypeGuardTests.ExpectedDeadTypes` 4 项 → 2 项 | 见 §8 |
| 8 | `Directory.Packages.props:47`（`MoonSharp 2.0.0`） | **保留** —— 现在由 Core 消费 |

- `dotnet build HmiEditor.slnx`：**0 警告 / 0 错误**；
- `bash scripts/ci-gate.sh`：见 §9 读数。

⚠ **沙箱残留**：`src/HmiEditor.Scripting/` 目录里还剩未跟踪的 `bin/`、`obj/`
（`git rm` 只删被跟踪文件；批量删除守卫按后代文件数计阈值，`rm -rf` 会被拦）。
它们是**惰性的**（不 track、不参与构建、`.gitignore` 覆盖）⇒ 请在你的终端手动删，
或留到下次会话（沙箱挂载随命名空间销毁）。

## 8. 守卫基线同步

`DeadTypeGuardTests` 是**相等判定**（多了红、少了也红），所以基线必须同步：
`ExpectedDeadTypes` 从 4 项改为 **2 项**（`HmiProperty`、`ResourceLibraryVersionInfo`）。

- `LuaScriptValidator` **合法消失** —— 它变成了活代码（搬进 Core 的那个 `LuaSyntaxValidator`，
  类型名随之变化，且不再"只出现在声明行"）；
- `LuaSnippets` **删除而不是搬迁** —— 它的钩子名（`on_button_click` / `on_timer_tick` /
  `on_alarm_triggered`）与当前运行时契约（`on_click` / `on_timer` / `on_alarm`）**对不上**，
  接进去会生成 `dispatch_*` 找不到函数的脚本；模板这件事已由活的 `ScriptTemplate` 承担。

## 9. 读数

- `dotnet build HmiEditor.slnx` ⇒ **0 警告 / 0 错误**；
- `HmiEditor.Core.Tests` ⇒ **2076 通过 / 0 失败**；
- 新增测试类 `LuaSyntaxValidatorTests` ⇒ **31 通过**；
- 门禁 `bash scripts/ci-gate.sh` ⇒ 见下方「门禁读数」小节。

### 门禁读数

**7 步全绿、`GATE_EXIT=0`**；构建步 **0 警告 / 0 错误**。
日志 `.workbuddy/tmp/ci-gate-2026-09-20-luasyntax-post.log`，
**mtime `2026-09-20 17:17:13`，晚于提交 `5bc3fb0` 的 `17:12:33`**。

| 测试程序集 | 通过 |
|---|---|
| `HmiEditor.Core.Tests` | **2068**（大队，已排除跑 `git` 的 8 条）+ **8**（队列为空时单独跑）= **2076** |
| `HmiEditor.AI.Tests` | 315 |
| `HmiEditor.App.UiTests` | 255 |
| `HmiEditor.Device.Tests` | 87 |
| `Hmi.Communication.Tests` | 75 |
| `Hmi.Hardware.Tests` | 44 |
| `HmiEditor.Simulator.Tests` | 44 |
| `HmiEditor.RenderTests` | 4 |
| **八程序集合计** | **2900** |

**+31 是纯净增**：阶段 37 的 **2869** → 本轮 **2900**，差额恰好是新测试类
`LuaSyntaxValidatorTests` 的 31 条（Theory 展开后），其余各项逐条一致。
门禁**一行未改** —— 第 [5] 步是全量 `dotnet test HmiEditor.slnx`，新测试类自动纳入。

> **为什么重跑了一次**：本轮第一次门禁跑在提交**之前** —— 日志 mtime `17:12:10`，
> 比提交 `5bc3fb0` 的 `17:12:33` **早 23 秒**。按本项目惯例（引用「绿」之前先核对时序），
> 那份读数对「提交后的代码」没有说服力 ⇒ 提交后重跑一次，上表才是有效读数。

### 覆盖率（未作为判据）

`HmiEditor.Scripting` 的 `0 / 82` 行随下架从分母消失。按阶段 36 的口径**必须按「文件 + 行号」
取并集**才能读数，且**覆盖率刻意不进门槛**（见 `docs/2026-09-20-coverage-tooling.md`），
本轮**未重跑覆盖率** —— 它不构成任何验收判据，重跑只为更新一个不设阈值的读数，
留到下次需要时一并做。

## 10. 已知边界与后续（都不是缺陷，是**知情选择**）

| # | 项 | 说明 |
|---|---|---|
| ① | **方言误报**：Lua 5.3+ 的运算符会被判错 | MoonSharp 只到 5.2（库的硬边界），而设备侧的 Lua **至今没有一个可对齐的目标**。已用 `LuaDialect.Hints` 把报错改写成可读说明，文案**从基准派生**（不许再写死），并**钉桩**（`Dialect_*`）。**⚠ 本节结论已被 `docs/2026-09-20-lua-dialect-alignment.md` 扩展**：除"误报"这个方向外，还有更危险的**假阴性**方向（编辑器放行、设备不可用），且其中大半编辑器**原理上看不见** —— 那里有实测表与触发线 |
| ② | **未搬的三个方法** | 原 `LuaScriptValidator` 还有 `GetUndefinedGlobals` / `CheckSuspiciousApis` / `IsBuiltIn`。**刻意不搬**：`GetUndefinedGlobals` 是正则启发式（会把 `t.x = 1`、`M.f = …` 误报）；`CheckSuspiciousApis`（`hmi_xxx` 拼写检查）**有价值但接入需要 UI 支持多条消息**（现在只显示 `Errors[0]`）⇒ 登记为**后续**，不是遗漏 |
| ③ | **字符串里的假函数** | `local s = "function foo()"` 会被 `HasFunctionDefinition` 算作"有函数定义"（本方法不改写字符串内容）。危害小：运行时 `lua_getglobal` 找不到函数会自己报错。已写进方法注释 |
| ④ | **Core 侧校验消息未入本地化字典** | `ScriptEngineService` 里的 `"脚本名称不能为空"` 等是**中文硬编码**，而 `ScriptEditorView` 把它们直接显示给用户 ⇒ 英文界面下是"本地化的前缀 + 中文正文"。**这是既有问题**（本轮未引入、也未扩大），但既然 Core 已经带 `Strings.zh-CN.json` / `Strings.en-US.json` 两本字典，**这是顺手该修的地方** ⇒ 登记 |
| ⑤ | ~~固件 `LV_USE_LUA` 未定义~~ | **该表述已作废（2026-09-20 同日更正）**：`LV_USE_LUA` **不是 LVGL 的配置项** —— 全 firmware 树（含 LVGL 源码）搜它**零命中**，`lv_lua_bridge` / `lv_binding_lua` 同样零命中 ⇒ 不是"开关没打开、缺个 `#define`"，而是**整条桥接从未存在过**。准确说法与证据见 `docs/2026-09-20-lua-dialect-alignment.md` §2 |

> **后续复核（2026-09-19，阶段 41）**：② 里"接入需要 UI 支持多条消息"这个**前置条件解开了一半** ——
> `ScriptValidationResult` 现在有独立的 **`Warnings` 通道**（不判红），`ScriptEditorView` 会显示警告的
> **首条 + 计数**并把全部挂 `ToolTip`。`CheckSuspiciousApis` 属"运行前检查"要治的**同一类**问题，
> 它缺的通道已经有了；真正未决的是**接入设计**（拼写可疑 ≠ 设备上一定跑不了 —— 直接报会制造误报）。
> ③（字符串里的假函数）与 ④（Core 侧中文消息）**原样开着**。
>
> 另：本专题 §5 / §6 说的"**UI 零改动**"指的是**阶段 39 那一次改动**不需要动 UI ——
> 阶段 41 为了让新发现能显示出来**动了** `ScriptEditorView`（新增能力的代价，不是对 §5 的否定）。
