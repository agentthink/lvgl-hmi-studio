# AI 提示词与 JSON Schema：枚举字面量 → 单一来源；顺带修掉未替换的 `{W}`/`{H}`

> 2026-09-15。承接记忆里登记的未开工项「`ScreenGenerationSystem.txt` 控件枚举与 Agent 侧不同步」。
> 查下去发现**漂移的不是提示词那一侧**，而是 JSON Schema —— 且不止控件类型一处。

## 1. 归因：先查清谁是漂移的那一侧，共有几处

`ScreenGenerationSystem.txt` 的「支持的控件类型」其实是个占位符 `{control_types}`，
由 `PromptTemplateEngine.BuildControlTypes()` 从 `HmiControlType` 枚举生成
（方法注释就写着"避免与代码枚举漂移"）。**提示词侧本来就是同源的。**

漂移在 JSON Schema 侧 —— 手写的枚举字面量，且**每一处都是同一根因**（"把产品侧枚举抄成字面量"）：

| 来源 | 清单怎么来 | 成员数 | 与产品侧枚举的关系 |
|---|---|---|---|
| `ScreenGenerationSystem.txt` 的 `{control_types}` | `Enum.GetNames<HmiControlType>()` 去 `Screen` | **25** | 同源（本来就没问题） |
| `ScreenGenerationSchema.json` → `controls[].type` | **硬编码字面量** | 22 | 缺 `Tank`、`Pipe`、`Valve` |
| `TemplateGenerationSchema.json` → `controls[].type` | **硬编码字面量** | 18 | 上述 3 个 + `Rectangle`、`Ellipse`、`Polyline`、`Arc` |
| `TemplateGenerationSchema.json` → `parameters[].type` | **硬编码字面量** | 5 | 字面量与 `ParameterType` 枚举**逐字相同**，但**无任何关联**（下次加枚举成员必漂移） |

**影响链**（用户可见）：`NlToScreenGenerator.GenerateAsync` 把 Schema 作为 `JsonSchema` 传给
provider → `OpenAIProvider` 走 `response_format.json_schema` → **严格模式下 `enum` 是硬约束**，
模型无法输出 enum 之外的值。于是：

- System 提示告诉模型「支持 Tank / Pipe / Valve」；
- Schema 的 enum 拒绝它们；
- ⇒ 用户描述"做一屏储罐和管道"时，模型只能退化成别的控件，或直接生成失败。

Template 生成路径更严重：连 `Rectangle` / `Ellipse` / `Polyline` / `Arc` 都被拒。

第 4 行是"**还没漂移但一定会漂移**"的形态 —— 当前值恰好对得上，所以**没有任何测试或错误会提醒你它已经不同步了**。

## 2. 第二处缺陷：与真占位符同形、但没人替换的花括号 token

写守卫时扫出 `ImageGenerationSystem.txt` 第 4 行：

```
- 输出严格为有效 XML: `<svg ... viewBox="0 0 {W} {H}" width="{W}" height="{H}">...</svg>`
```

`{W}` / `{H}` 是"这里填宽高"的示意写法，但：

1. 它与真正的模板占位符**同形**（花括号 + 标识符）；
2. `BuildImageMessages` 对 System **不做任何替换**（只对 user 替换 `{width}` / `{height}`）——
   所以模型收到的是字面量 `{W}`；
3. 模型有照抄 `viewBox="0 0 {W} {H}"` 产出**非法 SVG** 的风险。

顺带说明为什么这类问题一直没被发现：`Replace` 对不存在的占位符是**静默无操作**，
`LoadTemplate` 在文件缺失时也只返回空串 —— 占位符拼错/漏替换**不会有任何报错**，
只会把 `{xxx}` 原文泄漏给模型。

## 3. 做法：Schema 的 enum 也从枚举注入

与 `{control_types}` 同一范式 —— 模板里放占位符，引擎在返回前替换：

```csharp
/// <summary>可用控件类型名（排除 Screen 根容器）—— 唯一来源是 HmiControlType 枚举。</summary>
internal static IReadOnlyList<string> ControlTypeNames { get; } =
    Enum.GetNames<HmiControlType>()
        .Where(n => n != nameof(HmiControlType.Screen))
        .ToArray();

/// <summary>模板参数值类型名 —— 同一道理，唯一来源是 ParameterType 枚举。</summary>
internal static IReadOnlyList<string> ParameterTypeNames { get; } =
    Enum.GetNames<ParameterType>();

private string RenderSchema(string fileName)
    => LoadTemplate(fileName)
        .Replace("{control_type_enum}", ToJsonEnum(ControlTypeNames))
        .Replace("{parameter_type_enum}", ToJsonEnum(ParameterTypeNames));

/// <summary>枚举名清单 → JSON 字符串数组字面量。</summary>
private static string ToJsonEnum(IReadOnlyList<string> names)
    => "[" + string.Join(", ", names.Select(n => $"\"{n}\"")) + "]";

public string GetScreenSchema()   => RenderSchema("ScreenGenerationSchema.json");
public string GetTemplateSchema() => RenderSchema("TemplateGenerationSchema.json");
```

两条替换共用一个 `ToJsonEnum`（不是两个只差入参的同体方法）。占位符改成
`{control_type_enum}` / `{parameter_type_enum}`（**注意不能带引号**，替换后是 JSON 数组本身）。

同时：
- `BuildImageMessages` 对 System 也注入实际宽高，`ImageGenerationSystem.txt` 的 `{W}`/`{H}` 改
  `{width}`/`{height}` —— 模型看到的是真实数字，最不易抄错；
- 清掉 `BuildScreenMessages` 里的死变量 `var schema = LoadTemplate(...)`（加载后从未使用）；
- 新增 `KnownPlaceholders` 集合（11 个），登记模板允许出现的占位符。

**有意不加 `_comment` 之类说明字段到 Schema**：OpenAI strict 模式的 schema 校验很严，
未知关键字有被直接拒的风险。说明放在代码注释与本文件里。

## 4. 守卫：`PromptTemplateContractTests`（12 条）

按「模板部署 → 占位符双向登记 → 渲染无残留 → Schema 与枚举同源 → 顺序稳定 → 渲染后仍是合法 JSON」排布：

| # | 测试 | 钉住什么 |
|---|---|---|
| 1 | `TemplateDirectory_IsDeployedNextToTheAssembly` | 模板真的被 copy 到输出目录（≥10 个 `.txt`、≥3 个 `.json`） |
| 2 | `EveryPlaceholder_InEveryTemplate_IsRegistered` | 模板里出现的占位符都在 `KnownPlaceholders` 内 |
| 3 | `EveryRegisteredPlaceholder_IsUsedBySomeTemplate` | 反向：没有"登记了但没人用"的死条目 |
| 4 | `RenderedPrompts_LeaveNoUnreplacedPlaceholder` | 5 个 `Build*Messages` 的 system+user 都无残留占位符 |
| 5 | `ImagePrompt_InjectsConcreteCanvasSizeIntoBothSystemAndUser` | System 也拿到真实尺寸 |
| 6 | `ScreenSchema_ControlTypeEnum_MatchesHmiControlType` | Schema enum ≡ 枚举（去 Screen） |
| 7 | `TemplateSchema_ControlTypeEnum_MatchesHmiControlType` | 同上 |
| 8 | `TemplateSchema_ParameterTypeEnum_MatchesParameterType` | 参数类型 enum ≡ `ParameterType` |
| 9 | `BothSchemas_ExcludeScreenRootContainer` | `Screen` 不得作为可生成控件类型 |
| 10 | `ControlTypeNames_FollowEnumDeclarationOrder` | 顺序 = 枚举声明顺序（顺序不稳会让发给模型的 Schema 每次不同） |
| 11 | `ParameterTypeNames_MatchEnumExactly` | 参数类型清单逐字等于枚举、无增减 |
| 12 | `BothSchemas_AreValidJsonAfterRender` | 渲染后必须是合法 JSON（否则 provider 解析失败 → 静默降级为无约束） |

第 2 条的判据用 `\{[A-Za-z_][A-Za-z0-9_]*\}`，**刻意不用更宽的模式**：模板里含
JSON/SVG 示例（如 `{"screen": {...}}`），其花括号后跟引号或点号，不应被误判为占位符。

第 2/3 条天然构成**双向**约束：改了 Schema 里的占位符写法而无人在代码里替换 → 第 3 条红
（登记了却没人用）；在模板里新写一个 `{foo}` 而忘了登记 → 第 2 条红。

## 5. 负向验证（7 组，红集互不相同）

先做控件类型那一路（上一节收尾时跑的）：

| # | 破坏 | 期望红 | 实测 |
|---|---|---|---|
| NV-1 | `TemplateGenerationSchema` 的 enum 换回硬编码 18 个 | 1 条 | ✅ `TemplateSchema_ControlTypeEnum_MatchesHmiControlType`，失败信息直接列出差集 `[… "Table", "Svg", "Tank", "Pipe", "Valve", …]` vs `[… "Table", "Svg"]` |
| NV-2 | `ImageGenerationSystem.txt` 的 `{width}` 改回 `{W}` | 3 条 | ✅ 未登记 / 渲染残留 / System 未注入尺寸各一条；**死登记条不误红** |
| NV-3 | `KnownPlaceholders` 加一个没人用的 `nv_dead_entry` | 1 条 | ✅ 失败信息点名 `nv_dead_entry` |
| NV-4 | `ControlTypeNames` 末尾加 `.Reverse()` | 1 条 | ✅ **只有顺序契约条红**，两条同源一致性条**不红** |

NV-4 是这组里信息量最大的：它同时证明了两件事 ——
①顺序契约测试不是恒真；②"同源"设计确实不会产生"提示与 Schema 不一致"，
即便源头被改坏，两边也永远同步（只会被顺序契约抓到）。

再加参数类型那一路（本轮新增；NV 注入器是**每轮一次的临时脚本**，放 gitignore 的
`.refactor-tmp/`，用完即弃 —— 可复用的部分见 §5.1）：

| # | 破坏 | 实测红集 |
|---|---|---|
| NV-a | `ParameterTypeNames` 末尾加 `.Reverse().ToArray()` | 2 条：`TemplateSchema_ParameterTypeEnum_MatchesParameterType`、`ParameterTypeNames_MatchEnumExactly` |
| NV-b | Schema 的 `{parameter_type_enum}` 换回硬编码、并把 `Boolean` 写成 `Bool` | 2 条：`TemplateSchema_ParameterTypeEnum_MatchesParameterType`、`EveryRegisteredPlaceholder_IsUsedBySomeTemplate`（占位符没人用了） |
| NV-c | 登记笔误：`parameter_type_enum` → `parameter_type_enums` | 2 条：`EveryPlaceholder_InEveryTemplate_IsRegistered`、`EveryRegisteredPlaceholder_IsUsedBySomeTemplate` |

三组红集互不相同，且**控件类型那 4 条守卫全程不红** —— 非干扰已证。还原基准后 12/12 绿。

### 5.1 踩坑：NV 出现「假红」，根因是 mtime 被保留

第二轮 NV 时遇到一个**极难归因的现象**：还原基准后测试**仍然是红的**，且红的是
`ParameterTypeNames_MatchEnumExactly` 这种"只读代码、与 JSON 无关"的条 —— 按内容推断
根本不该红。

根因不在代码，在 NV 脚本自身：脚本用 `shutil.copy2` / `cp -p` 还原文件，
**`copy2` 保留 mtime**。MSBuild 的增量判据是"source 不比 dest 新就跳过"，
于是：

- 还原出来的源文件 mtime 比 `obj`/输出目录里的旧 ⇒ **跳过重编译** ⇒ 跑的是上一轮
  带 `.Reverse()` 的程序集；
- 内容文件同理 ⇒ 测试读到的是上一轮的破坏版 Schema。

表现就是"**已经还原了基准却仍然红**"，红集还是"不该红的那几条"。
判据：`stat` 对比源与 `bin/` 里同名文件的 `size` + `mtime`，dest 比 source 新即中招。

修法：NV 脚本一律用下面这个 `put()`（`copyfile` 不搬元数据 + `utime` 把 mtime 顶到当前），
替换掉所有 `shutil.copy2` / `cp -p`：

```python
def put(src: pathlib.Path, dst: pathlib.Path) -> None:
    """复制内容并强制刷新 mtime，确保 MSBuild 会重新复制/编译。"""
    shutil.copyfile(src, dst)
    os.utime(dst, None)
```

**先 `touch` 源文件再复跑一次即可区分真红/假红。** 本轮照此复跑后立刻 12/12 绿 ⇒ 假红确认。

教训：**改守卫自身、或改守卫读取的内容文件之后，必须让构建真正重跑过再下结论**；
"红得莫名其妙"优先怀疑构建产物陈旧，而不是先怀疑代码。

## 6. 遗留（本轮不动，已登记）

| 项 | 说明 |
|---|---|
| `TemplateGenerationSchema.category` 硬约束了一个**自由字段** | 枚举 `[通用/电机控制/温控/阀门/泵/仪表/自定义]` 在产品侧**没有对应枚举**（`ControlTemplate.Category` 是自由字符串，默认 `"通用"`），所以**无法同源化** —— 这是「硬约束了自由字段」，与「手抄枚举导致漂移」是两回事。风险：用户要"锅炉控制模板"时模型被迫选一个不贴切的分类。建议：去掉该 `enum`（或改为提示词里的软建议），但这是**产品行为变更**，需单独评估 |
| `NlToScreenGenerator.ParseControlType` 用 `Enum.TryParse`，`"Screen"` 能解析成功 | Schema 已排除 `Screen`，但 provider 降级路径（`json_object` / 无 `response_format`）时模型仍可能输出它，从而造出非法的"根容器控件"。需确认设计器侧对 `HmiControlType.Screen` 的处理后再定 |
| Schema 的属性集是通用的 | 只有 `x/y/width/height/text/bgColor/textColor/radius/variableBindingId`。`Tank` 的液位、`Pipe` 的流向等高级属性不在 AI 生成范围内 —— 现在"能生成"但只能得到通用属性，增强属后续 |
| `TimeoutSeconds=60` 对 Agent 偏短 | 既有登记 |

## 7. 验证清单

| 验证 | 结果 |
|---|---|
| `dotnet build HmiEditor.slnx` | ✅ 0 警告 0 错误 |
| `PromptTemplateContractTests`（12 条） | ✅ 全部通过 |
| 负向验证 NV-1 ~ NV-4（控件类型）+ NV-a ~ NV-c（参数类型） | ✅ 7/7 符合预期，红集互不相同 |
| 全量测试 | ✅ **2148 通过 / 0 失败**（AI 247 → 249；Core 1604 不变） |
| 测试基线 | 7 项目 **2146 → 2148** |

跑完守卫后的即时快照（新测试单独跑）：

```
已通过 ...PromptTemplateContractTests.ControlTypeNames_FollowEnumDeclarationOrder
已通过 ...PromptTemplateContractTests.BothSchemas_ExcludeScreenRootContainer
已通过 ...PromptTemplateContractTests.RenderedPrompts_LeaveNoUnreplacedPlaceholder
已通过 ...PromptTemplateContractTests.EveryPlaceholder_InEveryTemplate_IsRegistered
已通过 ...PromptTemplateContractTests.TemplateDirectory_IsDeployedNextToTheAssembly
已通过 ...PromptTemplateContractTests.TemplateSchema_ControlTypeEnum_MatchesHmiControlType
已通过 ...PromptTemplateContractTests.TemplateSchema_ParameterTypeEnum_MatchesParameterType
已通过 ...PromptTemplateContractTests.ParameterTypeNames_MatchEnumExactly
已通过 ...PromptTemplateContractTests.ImagePrompt_InjectsConcreteCanvasSizeIntoBothSystemAndUser
已通过 ...PromptTemplateContractTests.ScreenSchema_ControlTypeEnum_MatchesHmiControlType
已通过 ...PromptTemplateContractTests.BothSchemas_AreValidJsonAfterRender
已通过 ...PromptTemplateContractTests.EveryRegisteredPlaceholder_IsUsedBySomeTemplate
失败: 0，通过: 12，已跳过: 0，总计: 12
```
