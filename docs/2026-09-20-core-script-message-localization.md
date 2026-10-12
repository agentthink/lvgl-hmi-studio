# 脚本校验消息：从「拼好的中文成品」到「可本地化的键 + 参数」

> 2026-09-20。闭环的是**阶段 41「仍未闭环」第 ⑤ 条**（Core 侧中文校验消息未入本地化字典），
> 登记表 §12 #41-⑤ / `2026-09-17-open-items-register.md` #36 ⑤。
> 涉及链路：`HmiEditor.Core`（产出消息）→ `HmiEditor.App`（渲染）→ 两本字典 → 一条新守卫。

## 1. 问题

英文界面下，脚本编辑器的校验状态栏显示的是**本地化的前缀 + 中文正文**：

```
提示: 第 2 行: bit32 库 需要 Lua 5.2 或更高，设备目标未定、暂无法判定（设备若为 Lua 5.2 或更高则无此问题）
^^^^                     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
App 查字典得来（script.warn.prefix）              Core 侧的中文字面量，不随语言切换
```

阶段 41 把「运行前检查」的警告推到界面之后，这条遗留项被**放大**了 ——
以前只有语法错的全文一起中文化，现在新增的警告同样如此。

## 2. 归因：三件事叠在一起

先说结论：**这不是「一个字符串没搬进字典」，是消息的形状问题。**

### 2.1 形状问题（主因）

`ScriptError.Message` 是**已渲染的成品**：

```csharp
// 改造前
public class ScriptError
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = "";   // ← 已经拼好的一句话，本地化层无从下手
    public string Code { get; set; } = "";
}
```

本地化需要的是「模板 + 参数」，而这里只剩下一坨成品中文 —— **拆不出参数，也就翻译不了**。
对照项目里已有的同形状解法：`Hmi.Hardware.Contracts.ValidationIssue` 是
`(Severity, Code, Message, Args)`，`Message` 作回落、`Code` 推导键、`Args` 填模板。

### 2.2 两层拼接

产生点自己做了两层渲染（`LuaSyntaxValidator.BuildSyntaxError`）：

```csharp
Message = $"第 {line} 行: {ToChineseMessage(raw)}",
//         ^^^^^^^^^^^^^^   ^^^^^^^^^^^^^^^^^^^^^^^
//         外层：行号        内层：9 个分支的变体文案
```

外层能进字典，**内层 9 个分支不能**。只加 `Args` 而不动内层，英文界面会变成
`"Line 3: 代码不完整，可能缺少 end"` —— 只解决一半。

### 2.3 零守卫

`HardcodedLocalizationGuardTests` 的扫描面是 `AppRootRelative = "src/HmiEditor.App"`，
而且基线已经清零（`NoUnclassifiedHardcodedChinese` 断言 `Count == 0`）。
**Core 侧产出的中文完全在它视野之外** —— 新增一条 Core 文案，没有任何东西会红。

## 3. 修法

### 3.1 数据形状对齐 `ValidationIssue`

`ScriptError` 加 `Args`，并把 `Message` **正式定位为回落原文**（不是待废弃的旧字段）：

```csharp
public class ScriptError
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";              // 中文回落，界面查不到键时显示
    public IReadOnlyList<object>? Args { get; set; }       // 模板占位符实参
}
```

契约（写在类型注释里）：`Message` 必须**始终是一句能独立读懂的话** ——
字典缺键时它就是用户看到的东西。

### 3.2 Code 细分：从 5 个产生点到 19 个 Code

一个笼统的 `LUA_SYNTAX_ERROR` 装不下 14 种不同的用户可见说法。
细分之后每个 Code 各自对应一条字典模板，也各自可被分派（将来的快速修复、AI 提示都以它为准）。

| 族 | Code | 数量 |
|---|---|---|
| 模型级 | `EMPTY_NAME` / `EMPTY_CODE` / `NO_FUNCTION` | 3 |
| 语法 · 方言差异（已知误报） | `LUA_SYNTAX_OP_INTDIV` / `OP_BITAND` / `OP_BITOR` / `OP_BITNOT` / `OP_SHIFTRIGHT` / `ANGLE_ATTR_OR_SHIFTLEFT` | 6 |
| 语法 · 解析器报错分类 | `MISSING_END` / `UNFINISHED_LONG` / `UNFINISHED_STRING` / `UNEXPECTED_SYMBOL` / `MISSING_EQUALS` / `MISSING_NAME` / `EXPECTED` / `GENERIC` | 8 |
| 运行前检查 | `LUA_RUNTIME_SYMBOL_UNAVAILABLE` / `LUA_RUNTIME_SYMBOL_UNDETERMINED` | 2 |

**判族用前缀**（`LuaSyntaxValidator.IsSyntaxError` / `LuaDialect.IsRuntimeSymbolWarning`），
**不要**拿 Code 去 `==` 某个具体值 —— 那样一细分就静默失效。

两个刻意的命名决定：

- **方言 Code 按「运算符形态」命名，与版本解耦**（`OP_INTDIV` 而不是 `LUA53_INTDIV`）：
  版本信息走 `Args` 传，模板里不写死 —— 基准版本换了不必改 Code 名；
- **运行前检查的两态各一个 Code**：话术方向相反（一个说"确定没有"、一个说"暂时不知道"），
  合用一个就只能把区分塞进 `Args`，而 `Args` 是**填模板用的**、不是给程序分派用的。

配套：`Hints` 从 `(Marker, Hint)` 升级成 `LuaDialectHint(Marker, Code, Version, Fallback)` ——
4 个字段里有 2 个是字符串，元组写法一旦调换顺序编译器不会报错，只会在运行时静默失配。
方言提示那句「若目标设备的 Lua 版本更高，此处应视为误报」也收成了常量 `LuaDialect.HintTail`
（散在 6 处必然有一处漏，而漏掉就等于把误报说成了真错）。

### 3.3 行号交给界面

`ScriptError.Line` 本来就是结构化字段，UI 外面还叠了 `"错误: "` 前缀 ——
行号再写进 `Message` 是**三重冗余**，而且行号就有了两个来源。

改造后 `Message` 不含行号，由 `ExternalText.Describe` 统一加（键 `script.line.prefix`）。
这一条同时解决了 §2.2 的「外层模板」问题 —— 它本来就不该是数据的一部分。

### 3.4 渲染收口成唯一实现

`ProjectSettingsView.LocalizeIssue` 里那 6 行（查键 → 查不到回落 → 有 Args 走 Format）
和脚本这边要做的**完全一样**。抽成 `src/HmiEditor.App/Services/ExternalText.cs`：

```csharp
internal static class ExternalText
{
    internal const string ScriptIssueKeyPrefix = "script.issue.";
    internal static string ScriptIssueKey(string code) => ScriptIssueKeyPrefix + code.ToLowerInvariant();

    internal static string Resolve(string key, string fallback, IReadOnlyList<object>? args);

    /// 一条脚本校验信息（Errors / Warnings 共用）→ 用户可读文本（含行号）
    internal static string Describe(ScriptError error);
}
```

`ProjectSettingsView` 改为调它 —— 两份实现必然漂移，而漂移的表现正是
「守卫绿着、界面却是中文」。

### 3.5 字典

两本字典各新增 **20 条**：19 条 `script.issue.<code 小写>` + 1 条 `script.line.prefix`。
键名沿用 `hw.issue.*` 的约定（Code 小写、下划线保留）。

## 4. 判据

新增 `tests/HmiEditor.App.UiTests/ScriptIssueLocalizationGuardTests.cs`（7 条）：

| # | 判据 | 守什么 |
|---|---|---|
| ① | `EveryTriggeredCode_ResolvesInBothLanguages` | 键覆盖：触发的 + **豁免的** Code，其键两本字典都要有 |
| ② | `PlaceholderCount_MatchesArgsCount` | 模板占位符与 `Args` 恰好对应 |
| ③ | `ChineseTemplate_RendersToTheCoreFallback` | zh 模板渲染 == Core 回落原文（两份同步） |
| ④ | `Probe_CoversEveryDeclaredCode` | 声明过的 Code 必须都能真触发；**豁免表双向** |
| ⑤ | `UnreachableCodes_StillHaveWellFormedTemplates` | 豁免的 Code 也要守键与占位符 |
| ⑥⑦ | 两条负控（删键 / 改歪模板） | 每条都与正控同场 |

**触发面是真实的**：`Probe()` 跑 `ScriptEngineService.Validate` 与
`LuaDialect.DescribeHit`，不抄字面量清单。`DeclaredCodes` 用**反射**枚举
`LUA_SYNTAX_*` / `LUA_RUNTIME_SYMBOL_*` 常量 —— 手写清单会与实现分家，
分家之后守卫检查的是清单、不是代码（本项目登记过这一族失效模式："被自己的清单喂活"）。

**已发生目标（5.1）的那一态**（`LUA_RUNTIME_SYMBOL_UNAVAILABLE`）在设备目标确定前
不会经由 `Validate` 产生（`LuaDialect.Target` 恒为 `Unknown`），所以直接调 `DescribeHit(hit, Lua51)` 取。
它的字典键必须**提前就位** —— 目标一旦定下来就会用上。

## 5. 守卫抓出来的两个真问题

这条守卫第一次跑就红了 4 条，其中两条是**产品问题**、两条是**判据自己写错**。
如实记下来，因为这两类都不是靠"想"能提前想到的。

### 5.1 `'<eos>'` 的同族病：三个永不命中的分支

`Probe` 诊断输出显示，`MISSING_EQUALS` / `MISSING_NAME` / `EXPECTED` **一个都触发不出来**：

```
  LUA_SYNTAX_UNEXPECTED_SYMBOL  |  出现了意外的符号：unexpected symbol near '1'
  LUA_SYNTAX_UNEXPECTED_SYMBOL  |  出现了意外的符号：unexpected symbol near 'end'
  LUA_SYNTAX_UNEXPECTED_SYMBOL  |  出现了意外的符号：unexpected symbol near '='
  LUA_SYNTAX_UNEXPECTED_SYMBOL  |  出现了意外的符号：unexpected symbol near 'do'
```

即：`local a b = 1` / `local = 1` / `if true do end` 这三个本该报
`'=' expected` / `'<name>' expected` / `'then' expected` 的写法，
**MoonSharp 2.0.0 一律报 `unexpected symbol`**。

这和 `'<eos>'` 那次是**同一族**：查了一个这个解析器不产生的报错文本，
分支永远不命中，却带着自己的字典键"看起来有覆盖"地存在。

**处置：保留分支 + 显式豁免 + 双向判据。**

- 保留：这三类报错文本是 **Lua 参考实现的标准报错**（`lparser.c` 就是这么报的），
  换解析器或升版本就会出现；届时若没有分支，用户看到的是兜底文案；
- 显式豁免：登记进 `UnreachableOnCurrentParser`，**连预期实参个数一起登记**
  （`MISSING_EQUALS`=0、`MISSING_NAME`=0、`EXPECTED`=1）—— 豁免不等于可以不守；
- 双向判据：**能触发的 Code 不许留在豁免表里**。这一条把"豁免"从免死金牌
  变成会红的触发线。

### 5.2 判据自己的统计口径错了

`lua_runtime_symbol_undetermined` 的模板里 `{1}` **故意出现了两次**
（"需要 5.2 或更高…设备若为 5.2 或更高则无此问题"）。`string.Format` 完全允许，
但我最初按**出现次数**数占位符 ⇒ 数成 3 ≠ 实参 2，报了假红。

修正后按**不同索引**数，并且额外要求索引恰好是 `0..n-1`：

```csharp
return indexes.Count == argCount && indexes.SequenceEqual(Enumerable.Range(0, argCount));
```

后半句是顺手补的漏洞：只比个数会漏掉"有洞"—— `{0}` 与 `{2}` 两个占位符、两个实参，
个数对得上但 `{1}` 缺失，运行时抛 `FormatException`（那就从"显示字面量"升级成"崩"了）。

> 附带发现：`hw.issue` 那边同名的判据（`IssueTemplates_PlaceholderCountsMatchPluginArgs`）
> 也是按出现次数数的，只是它的模板里没有重复占位符所以从未暴露。~~**没有跟着改** ——
> 它现在是对的，且它的"决定表"是手写的另一套形状；统一属独立议题，记在 §7。~~
>
> **后续复核（2026-09-20，阶段 43）**：上面那句"它现在是对的"**被下一轮推翻了**。
> 数出现次数 ≠ 没问题 —— **运行期**的判据（`LocalizationService.Format`）用的就是这个口径，
> 而它**真的坏了**：那条重复占位符模板到了界面上是 `{0} 需要 {1} 或更高…` 这样的**花括号字面量**。
> 本轮的守卫没抓到它，原因是它只比对"模板 vs Args"、**没走运行期的 `Format`**（口径不同源）。
> 现已收口成唯一实现（运行期 + 四处守卫同源）并把"洞"补进判据 ⇒
> 专题 `docs/2026-09-20-placeholder-judgment-single-source.md`。"决定表"保留不动。

## 6. 负控与读数

### 6.1 负控

| # | 注入 | 结果 |
|---|---|---|
| ① | 把能触发的 `MISSING_END` 塞进豁免表 | **红 1/7**，点名的正是 `LUA_SYNTAX_MISSING_END` |
| ② | 字典删一个键 / 模板加 `{9}` / 模板改歪 | 由 `NegativeControl_*` 两条覆盖（正控同场） |

另外，**头两轮是真实的失败**，比事后注入更有说服力：

| 轮次 | 红了几条 | 红的是谁 | 原因 |
|---|---|---|---|
| 第 1 轮 | 4/6 | 三条判据 + 触发面自检 | ① 占位符统计口径错（假红）② 三个 Code 触发不出来（真问题，§5.1） |
| 第 2 轮 | 2/6 | 触发面自检 + 一条负控 | 负控自己的断言写错（拿裸键去匹配带后缀的描述串） |

### 6.2 读数

| 项 | 值 |
|---|---|
| Core.Tests（Lua 组） | **119 通过 / 0 失败**（改前同数 —— 只改了断言的表达方式） |
| Core.Tests（全量） | **2109 通过 / 0 失败** |
| App.UiTests（全量） | **262 通过 / 0 失败**（改前 255 + 新增 7） |
| 字典新增 | zh-CN / en-US 各 20 条 |
| 新增文件 | `src/HmiEditor.App/Services/ExternalText.cs`、`tests/HmiEditor.App.UiTests/ScriptIssueLocalizationGuardTests.cs` |

## 7. 仍未闭环

| # | 项 | 说明 |
|---|---|---|
| ① | **`hw.issue` 的占位符判据口径** | ~~仍是"按出现次数"（见 §5.2 附注）。现在没暴露 ≠ 永远不出问题~~ ⇒ ✅ **已闭环（2026-09-20，阶段 43）**：判据收成唯一实现 `Core/Services/LocalizationPlaceholders`（**运行期 `Format`** + 三处守卫同源），并补上"索引有洞"这一层。**当时这条低估了** —— 同样的口径在运行期，且已经让界面显示花括号字面量。⇒ `docs/2026-09-20-placeholder-judgment-single-source.md` |
| ② | **`Errors` 侧仍只显示首条** | `Warnings` 侧已有"首条 + 计数 + ToolTip 全部"，`Errors` 侧只换了渲染，未做计数（阶段 41 遗留，与本轮无关） |
| ③ | **其他 Core 文案未入字典** | 本轮只覆盖**脚本校验**这一条通道。`HmiEditor.Core` 里还有别的用户可见文案（如生成链路的提示），没有守卫、也没有盘点 |
| ④ | **目标 Lua 版本未定** | 与本轮无关（`LuaDialect.Target` 触发线承接）。但 `UNAVAILABLE` 那一态的文案**现在就有键**，正因为目标定了立刻要用 |
| ⑤ | 局部变量遮蔽 / 表键是已知假阳性 | 阶段 41 的取舍，未变 |
