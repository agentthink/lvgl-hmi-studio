# AI 诊断文案的本地化收口（阶段 70，2026-09-22）

> 登记表 §4 #27 的「⑫ `src/HmiEditor.AI` 不在扫描面」那一半。
> 相关：`docs/2026-09-18-plugin-text-and-lang-usage-guards.md`（同族的插件侧）、
> `docs/2026-09-20-core-script-message-localization.md`（同族的 Core 侧）。

---

## 1 一句话结论

`src/HmiEditor.AI` 产出的**诊断文案**（AI 输出校验 + 布局建议）此前是**没接字典的字面量**，
而它们经**三条路径**直接显示给用户 ⇒ 英文界面下「本地化的前缀 + 中文正文」。
本阶段把这 41 条接上字典（两本各 +41 行）、给它们一套**可反射枚举的结构化码**、
并新增第 **八** 条键可达机制 **M8** 把它们纳入守卫。**AI 层没有一行判定逻辑被改动**。

---

## 2 缺口是怎么量出来的

「不在扫描面」这句是个**程序化事实**，不是缺陷本身 —— 缺陷是它导致的**可见后果**。
先把三条后果路径钉死（全部实测，不是推断）：

| # | 消费点 | 代码位置 | 显示在哪儿 |
|---|---|---|---|
| ① | `AiChatPanelView` 聊天条目 `Detail` | `AiChatPanelView.axaml.cs` 4 处 | AI 助手面板，校验失败时逐条列问题 |
| ② | 生成对话框状态行（取前 5 条） | `AiGenerateDialogBase.cs:99-103` | `AiTemplateDialog` / `AiScriptDialog` 等 |
| ③ | 属性面板「AI 建议」列表 | `PropertyPanelView.Ai.cs:59-65` | 屏幕级布局问题（可一键修复那些） |

三处的**同一行**里，前缀都是本地化的（`Lang.AiErrValidationScreen`、`"ai.gen.failed"`、
`Lang.PropIssueAutofix`），只有正文是写死的中文 —— 这正是「中英混排」的典型形状。

**数量**（去重后的码，不是出现次数）：

| 来源 | 构造点 | 唯一码 |
|---|---|---|
| `AiOutputValidator`（屏幕/控件/模板/脚本校验） | 30 处 | 27 |
| `AiOrchestrator`（意图路由早退） | 2 处 | 2 |
| `LayoutAnalyzer`（布局分析） | 9 处 | 12 |
| **合计** | **41** | **41** |

（`AiOutputValidator` 的 30 → 27 是三个码各出现两次：类型/尺寸/背景色在模板路径与屏幕路径共用。）

---

## 3 为什么不能只「把字面量搬进字典」

搬进字典要回答一个**定位问题**：这 41 条各自的键是什么？两条现成的路都不通：

- **用中文原文当键**：改一个字的措辞就换了一个键，而且中文既做不了反射枚举、
  也过不了「键必须是稳定标识符」这一关；
- **在调用点直接写整键字面量**（`"ai.issue.screen_empty"`）：违反本仓文案三层的约定
  （Code 只做键推导/分派），且让 AI 程序集依赖 App 层的键命名 —— 依赖方向是 App → AI，反了。

所以走**第三条**，与 Lua 脚本校验（M5）同形：

```
程序集声明稳定的「码」  →  App 侧按前缀推导「键」  →  字典存「模板」  →  ExternalText 渲染
SCREEN_EMPTY              ai.issue.screen_empty      {0}/{1} 占位符      Resolve(key, fallback, args)
```

码是「哪一类问题」，键是「在哪本字典里」—— 改键前缀不该动到 AI 程序集。

---

## 4 落地清单

| 文件 | 改动 |
|---|---|
| `src/HmiEditor.AI/AiIssueCodes.cs` | **新建**。41 个 `public const string`（必须是 `const` 才能被反射枚举）。布局类与校验类同用 `ai.issue.` 一个前缀，前缀分组只体现在码名上 |
| `src/HmiEditor.AI/Validation/AiOutputValidator.cs` | 30 个构造点改走 `AiValidationIssue.Of(severity, code, 回落模板, args…)` |
| `src/HmiEditor.AI/AiOrchestrator.cs` | 2 个构造点同上 |
| `src/HmiEditor.AI/Models/LayoutAnalysisResult.cs` | `LayoutIssue` 增 `Code` / `Args` |
| `src/HmiEditor.AI/Services/LayoutAnalyzer.cs` | 9 个 `MakeIssue` 调用点带码 + 实参；`Message` 改由 `string.Format(回落模板, args)` 渲染 |
| `src/HmiEditor.App/Services/ExternalText.cs` | 加 `AiIssueKeyPrefix` / `AiIssueKey(code)`，以及两个 `Describe` 重载（`AiValidationIssue` / `LayoutIssue`） |
| `HmiEditor.App` 那三处消费点 | 改调 `ExternalText.Describe(...)` |
| `Strings.zh-CN.json` / `Strings.en-US.json` | 各 **+41 行**（2134 → 2175） |

### 4.1 一个刻意的设计：`Message` 由模板渲染，不由调用点手写

```csharp
internal static AiValidationIssue Of(
    AiValidationSeverity severity, string code, string fallbackTemplate, params object[] args)
    => new(severity, code,
           args.Length == 0 ? fallbackTemplate : string.Format(fallbackTemplate, args),
           args.Length == 0 ? null : args);
```

`Message`（中文回落）与 `Args`（实参）**是同一句话的两个版本**。让调用点各写一遍必然有一处先过期，
所以由模板 + 实参渲染 —— 于是判据「zh 字典模板渲染后 == 回落原文」比的是**同源的**两份文本。

### 4.2 数值型实参先格式化（否则两侧不可比）

`LayoutAnalyzer` 原来的写法是插值里带格式说明符（`{c.X:F0}`、`{ratio:F1}`），
而字典模板只能写 `{1}`。所以格式说明符留在调用点、值以字符串形态进实参：

```csharp
private static string Fmt(double v) => v.ToString("F0", CultureInfo.CurrentCulture);
private static string Fmt1(double v) => v.ToString("F1", CultureInfo.CurrentCulture);
```

`Fmt` / `Fmt1` 与被替换掉的说明符必须完全等价（含 culture）—— 不等价就会让
「zh 渲染 == 回落原文」这条判据把**格式差异**报成文案差异。

---

## 5 判据（`AiIssueLocalizationGuardTests`，7 条）

| 判据 | 守什么 | 输入 |
|---|---|---|
| `EveryProducedCode_ResolvesInBothLanguages` | 每个真产出的码，其键在**两本字典**都能解析 | 触发面 |
| `PlaceholderCount_MatchesArgsCount` | 模板占位符个数 == 实参个数（`Format` 不符时**不抛异常**，用户会看到 `{1}` 字面量） | 触发面 |
| `ChineseTemplate_RendersToTheAiFallback` | zh 模板渲染 == AI 层回落原文（**自动**比对，不手写清单） | 触发面 |
| `ProducedCodes_AndDeclaredCodesAreTheSameSet` | **双向**：产出集合 ⟷ 反射枚举的声明集合 | 触发面 + 反射 |
| `NegativeControl_RemovingAKey_IsCaught` | 键覆盖判据活着（+ 三条正控同场） | — |
| `NegativeControl_BreakingATemplate_IsCaught` | 占位符判据与一致性判据**各自独立**命中；键覆盖判据**看不见**这两种破坏 | — |
| `NegativeControl_CodeSetDrift_IsCaughtInBothDirections` | 双向判据的**两个方向**各自可独立命中 | — |

### 5.1 双向判据挡的是两件不同的事

| 方向 | 什么情况 | 为什么必须有人看见 |
|---|---|---|
| 产出了但没声明 | 调用点写**字面量码**（`Of(sev, "some_literal", …)`） | 能编译、能运行、**键不会进字典** ⇒ 界面回落中文 |
| 声明了但产出不到 | 死码，或触发面漏了样本 | 死码与它的字典键都永远不会被用到，而「看起来有覆盖」会让人以为测到了（登记 #49 是同一个坑的常量版） |

两个方向的**修法完全不同**，所以失败消息分开写。

### 5.2 41 条全都真触发（没有豁免表）

两条编排码看起来要连 LLM，其实都在「还没轮到调模型」时返回：

- `IMAGE_GENERATOR_MISSING`：`GenerateImageAsync` 里 `_imageGenerator is null` 的早退；
- `INTENT_UNSUPPORTED`：意图分类落到 `Unknown`（传入空输入即可）。

所以只要把三个生成器构造出来就行（假 provider + `new PromptTemplateEngine()`）。
断言用的 provider **被真调用就抛** —— 那说明守卫的假设错了，直接在原地暴露比在
「码集合对不上」里暴露信息量大得多。

---

## 6 机制侧：键可达性新增 M8

`LocalizationKeyReachabilityGuardTests` 从**七条**机制扩到**八条**：

```
M8-AI 诊断码   面 41
```

取数方式**复用** `AiIssueLocalizationGuardTests.DeclaredCodes()`（反射 `AiIssueCodes`），
不另抄一份 —— 抄两份必然漂移，而漂移的表现是「一个守卫绿、另一个红」，看不出哪边对。

机制面读数（`HMI_READINGS_OUT` 落盘，门禁 minimal logger 下也拿得到）：

| 机制 | 面 | 与阶段 66 对比 |
|---|---|---|
| M1-Lang 常量 | 649 | 同 |
| M2-axaml res:Loc | 1113 | 同 |
| M3-插件族派生 | 26 | 同 |
| M4-cfg.note 清单 | 30 | 同 |
| M5-脚本诊断码 | 19 | 同 |
| **M8-AI 诊断码** | **41** | **新增** |
| M6-源码字面量 | 387 | **同** |
| M7-枚举名派生 | 12 | 同 |

**M6 面没涨是刻意的正面证据**：它说明新键**没有**被「源码整键字面量」这条代理机制顺带覆盖
（AI 层写的是码不是键），也就是说这 41 个键**只靠 M8 活着** —— M8 一旦失效，主判据立刻红。
下界也按满值登记（41），因为每少一个码，双向判据会先点名。

---

## 7 端到端负控（3/3）

注入的是**产品代码与字典**（不是判据内部函数），所以同时证明「守卫真的在调这些判据」。

| 场景 | 注入 | 期望红在 | 实测 |
|---|---|---|---|
| `nc1` | `AiIssueCodes` 加一个没人产出的码 | 「声明了但产出不到」+ 点名 `ZZZ_UNUSED_PROBE` | 红 **2** 条 ✓ |
| `nc2` | 一处调用点换成字面量码 `"CONTROL_OVERLAP_PROBE"` | 「产出了但没声明」+ 点名它 | 红 **7** 条 ✓ |
| `nc3` | zh 字典删掉一条 `ai.issue.*` 键 | 「键在字典里查不到」（缺 zh-CN） | 红 **5** 条 ✓ |

- 判红谓词匹配测试**自己的汇总行**（`失败: N`，N>0）**且**日志里取到 `错误消息:`
  —— 不写成「stdout 里没有『已通过!』」，那会把编译失败也算成红。
- 还原一律**写回原文**，不用 `shutil.copy2`（它连 mtime 一起复制回旧值 ⇒ MSBuild 判定无需重建
  ⇒ 正控跑的是负控的二进制，**假红**）。还原后三个文件与存档**逐字节一致**（`cmp` 无差异），
  注入痕迹 grep 计数为 **0**。
- 工具：`.workbuddy/tmp/nc70.py`（注入/还原）+ `.workbuddy/tmp/nc70-run.sh`（驱动 + 判定）。

---

## 8 诚实边界（4 条）

1. **只覆盖「AI 诊断」这一族文案。** `HmiEditor.AI` 里还有两类中文**刻意不本地化**：
   提示词模板（`Inference/PromptTemplates/*.txt`、`AgentSystemPrompt.cs`、`AgentTools`）与
   意图关键词表（`ClassifyIntent` 里的匹配词）—— 翻译它们会**改变模型行为 / 识别结果**，
   属独立议题。（`HardcodedLocalizationGuardTests` 的排除项里对 `AgentSystemPrompt.cs` /
    `AgentToolFactory.cs` 已有同样的判断。）
2. **异常消息仍会被透到界面。** `AiGenerateDialogBase` 的 `catch (Exception ex)` 走
   `Format("ai.gen.failed", ex.Message)`，而 `LlmHttpProviderBase` / `OpenAIProvider` /
   `AgentEngine` 抛的是中文。这与 App 层既有的「异常消息面向开发者，不翻译
   （翻译后无法搜索）」同口径，本阶段**未动** —— 它是一个已知的、有意的边界，不是遗漏。
3. **`Message`（中文回落）保留在 AI 层。** 除了与 `ScriptError.Message` /
   `ValidationIssue.Message` 保持同形，还有个硬理由：`HmiEditor.AI.Tests` 有 **15+ 处**
   `i.Message.Contains("…")` 断言，删掉它是破坏性变更。代价是 AI 层仍有中文 ——
   但它们都**带结构化的码**，不再是散落的字面量。
4. **枚举名没本地化。** `INTENT_UNSUPPORTED` 的实参是意图枚举，中英两版都会显示
   `'Unknown'` —— 那是类型名不是文案，本阶段不动。

---

## 9 门禁与测试数

| 时刻 | 日志（mtime） | 结果 |
|---|---|---|
| 提交前 14:38:44→14:39:57（73 s） | `ci-gate-70-pre.log`（**14:39:57**） | **7 步全绿 / `GATE_EXIT=0`**、0 警告 0 错误 |

逐程序集（大队）：Simulator 44 / Hardware 44 / Communication 75 / Device 87 / Render 4 /
AI 315 / Core 2116 / **App.UiTests 394 → 401（+7）**；
另两段独占 **8 + 2** 未变 ⇒ 大队 **3079 → 3086**、总 **3089 → 3096**。
`HmiEditor.AI.Tests` 315 项**未变**（类型改造没破坏既有断言）—— 这一点在本阶段单独先验过一次。
