# Agent 工具 Schema 的枚举来源收口：7 组手抄字面量 → 单一来源 + 分层守卫

> 上一轮（`docs/2026-09-15-ai-prompt-schema-single-source.md`）收口的是 **Prompt 模板侧**的
> 枚举字面量（`{control_type_enum}` / `{parameter_type_enum}` 注入 + 模板正文的手抄清单）。
> 本轮收口的是**另一条并列的链路**：Agent 工具的 `input_schema`。
> 两条链路的坏法不同：Prompt 侧是"写入正文、发给模型"；工具侧是"**约束模型的输出**"。

---

## 0. 一句话结论

登记项（`MEMORY.md` §8）原文是「`ParseControlType` 的 `Enum.TryParse` 能解析 `Screen`
（降级路径可造非法根容器控件）」。查清后发现**前提是错的** —— `Screen` 是一条**策略**排除，
不是"非法性"。但顺着这条线查下去，牵出了**真问题**：

**枚举字面量在 Agent 工具链路上被手抄了 7 处，其中 5 处抄着产品侧不存在的值，
1 处产出的是非法 JSON（provider 静默降级为"无约束"）。**

现在全部改为从**同一枚举**注入，并加了两层守卫（静态 13 条 + 运行时 12 条）。

---

## 1. 归因：登记项的前提是错的

登记项假设「`Screen` 是非法根容器控件，`Enum.TryParse` 兜底路径能绕过校验造出来」。
实际语义不是这样：

| 事实 | 证据 |
| --- | --- |
| `HmiControlType.Screen` = **人类可拖入的容器控件**，不是页面 `HmiScreen` | `ToolboxView` 工具箱第 123 行把它列为可拖控件 |
| 它被映射到 `lv_obj_create` | `ControlMappingRegistry` |
| 项目要求它**必须**完整 | `ControlPipelineCompletenessTests` 要求每个枚举成员都有 create 函数 + 本地化名 |
| AI 侧排除它是一条**策略** | AI 建页面该走 `create_screen`，而不是往屏幕里塞一个 `Screen` 控件 |

**所以登记项的措辞（"非法性"）不成立**；但"AI 不该生成 Screen"这条策略本身是合理的，
它现在是 `AiSchemaEnums.GeneratableControlTypes` 里显式 `.Where(n => n != nameof(HmiControlType.Screen))`，
并且有守卫钉住（NV-a 就是拆这条策略）。

### 1.1 真问题：7 组手抄字面量

| # | 位置 | 手抄值 | 真值 / 后果 | 坏法 |
| --- | --- | --- | --- | --- |
| 1 | `AgentToolFactory.ControlTypeEnumList`（`.cs` 常量） | 21 项 | 可生成真值 **25** 项，漏 `Rectangle/Ellipse/Polyline/Arc` | 缺成员 |
| 2 | `add_control` / `add_template_control` 内联 schema | `"enum":[{{ControlTypeEnumList}}]`（CSV 直插） | **非法 JSON**：`[Button,Label,…]` 未加引号 | 语法坏 |
| 3 | `add_controls_batch` 的 `[[ENUM]]` | 括号账错位 | 换成自带方括号的注入后成 `[["Button",…]]` | 语法坏 |
| 4 | `create_alarm.type` | 含 `OnTrue/OnFalse` | `AlarmConditionType` 真值 8 项，**没有**这两个 | 值不存在 |
| 5 | `create_alarm.severity`（描述文本） | `Info/Warning/Error/Critical` | `AlarmSeverity` 真值 `Info/Warning/Critical/Emergency` | 值不存在 |
| 6 | `update_security.operation_mode` | `Designer/Runtime/Maintenance` | `OperationMode` 真值是 `Designer/Operator/Maintenance/ReadOnly`，**没有 `Runtime`** | 值不存在 |
| 7 | `create_connection.driver` | 含 `opcua` | 真实 Id 是 `opc-ua`；`NormalizeDriver` 只把 `_`→`-`，补不出中间那个连字符 | 值不存在 |
| 8 | `TemplateGenerationSystem.txt:7` | 18 项 | 可生成真值 25 项，漏 7 项；且该文件的 system **从不走替换**（见 §3.4） | 缺成员 |

另有一处**同源但不同坏法**：`ScriptGenerationSchema.json` 的 `category` 抄了
`[OnClick, OnValueChanged, Timer, Alarm]` —— 它与 `ScriptTemplate` 的 4 个静态模板
`Category` 恰好一致，但**零关联**（改一处不会带动另一处）。已同源化。

---

## 2. 三种坏法的后果不一样，要分开说

这是本轮最重要的认知：**"手抄枚举"不是一种缺陷，是三种**，危害递增。

| 坏法 | 机制 | 后果 | 隐蔽性 |
| --- | --- | --- | --- |
| **缺成员** | `enum` 在严格 `json_schema` 下是**硬约束** | 模型**永远**生成不出被漏掉的类型；而 System 提示可能已宣告支持它 ⇒ 一边说支持、一边拒绝 | 中：功能静默缺失，不是报错 |
| **语法坏**（非法 JSON） | provider 解析 `input_schema` 失败 | **整条 schema 的约束全丢** —— 不是少几个值，而是回到"无约束"。模型可能输出任意字符串 | **最高**：连校验都没了，且工具侧只会看到 `Enum.TryParse` 兜底 |
| **值不存在** | 工具实现里 `Enum.TryParse` / 字符串直接落库 | 产物侧兜底到默认值或存下一个非法字符串 ⇒ **静默坏数据**（例：建出一个匹配不到任何驱动的连接，而工具回报"成功"） | 高：没有任何异常 |

> 第 6 号（`operation_mode`）是"值不存在"的教科书案例：schema 的 `enum` 里有 `Runtime`，
> 产品枚举里没有，`Enum.TryParse` 兜底到默认值 ⇒ **用户以为自己改成了 Runtime，实际没改**。

---

## 3. 做法

### 3.1 单一来源：`AiSchemaEnums`（新建，`HmiEditor.AI`，public）

放在 AI 层而不是 App 层，是因为 `App → AI` 的依赖方向已经存在，
所以 App 侧的工具工厂能直接复用，**不必再抄一份**。

```csharp
public static IReadOnlyList<string> GeneratableControlTypes { get; } =
    Enum.GetNames<HmiControlType>().Where(n => n != nameof(HmiControlType.Screen)).ToArray();

public static bool TryParseGeneratableControlType(string? name, out HmiControlType type);   // 拒绝 Screen
public static HmiControlType ParseGeneratableControlTypeOrLabel(string? name);              // 降级到 Label

public static string JsonArray<TEnum>() where TEnum : struct, Enum;   // ["A", "B"]
public static string JsonArray(IEnumerable<string> names);
public static string Csv<TEnum>() where TEnum : struct, Enum;         // A,B

public static string GeneratableControlTypesJson { get; }   // 给 JSON 内插用
public static string GeneratableControlTypesCsv { get; }    // 给描述文本/错误提示用
```

`TryParse` 与 `GeneratableControlTypes` **共用同一条"排除 Screen"判据**，所以
「schema 里宣告什么」与「工具接受什么」不可能再分叉。

### 3.2 驱动 Id 单一来源：`Hmi.Communication.Builtin.BuiltinDriverCatalog`（新建）

```csharp
public static IReadOnlyList<ICommunicationDriver> CreateAll() =>
    [new ModbusRtuDriver(), new ModbusTcpDriver(), new OpcUaDriver(), new MqttDriver()];

public static IReadOnlyList<string> Ids { get; } = CreateAll().Select(d => d.Metadata.Id).ToArray();
```

- `CommunicationConfigView.axaml.cs` 里的内联 `new ICommunicationDriver[] { … }` 改为 `LoadBuiltinDrivers(BuiltinDriverCatalog.CreateAll())`
- `create_connection` 的描述文本与 schema 的 `enum` 都取自 `Ids`
- **并补上了此前完全缺失的校验**：`NormalizeDriver(driver)` 之后必须在 `Ids` 里，
  否则直接 `Failure` —— 在此之前模型给 `opcua` 会**成功**建出一条坏连接

### 3.3 脚本分类单一来源：`ScriptTemplate.Defaults` / `.Categories`

```csharp
public static IReadOnlyList<ScriptTemplate> Defaults { get; } = [OnClick, OnValueChanged, Timer, Alarm];
public static IReadOnlyList<string> Categories { get; } = Defaults.Select(t => t.Category).ToArray();
```

`ScriptGenerationSchema.json` 的 `category.enum` 改为 `{script_category_enum}`。

### 3.4 把没接上渲染链的两个洞补上

| 洞 | 现象 | 修法 |
| --- | --- | --- |
| `GetScriptSchema()` 直接 `LoadTemplate` | 它**不走** `RenderSchema`。一旦 schema 文件里出现占位符，`{xxx}` 会原样发给模型（`Replace` 对不存在的占位符是**静默无操作**） | 改为 `RenderSchema("ScriptGenerationSchema.json")` |
| `BuildTemplateMessages` 的 system 不注入 `{control_types}` | `TemplateGenerationSystem.txt` 因此**被迫**手抄 18 项清单 —— 手抄不是疏忽，是"不注入就没得用" | 加 `.Replace("{control_types}", BuildControlTypes())` |

> 第 2 条是**本轮 NV 基准暴露出来的**：我加了守卫之后，基准跑出 2 条红。
> 根因是我上一版把 `.Replace(...)` 加到了 `BuildScreenMessages`（那里本来就有），
> `BuildTemplateMessages` 没接上。详见 §5.1。

### 3.5 三处逐字同体的私有 `ParseControlType` 删除

`NlToScreenGenerator` / `NlToTemplateGenerator` 各有一份逐字相同的私有
`ParseControlType`（`Enum.TryParse` + 排除 `Screen` + 兜底 `Label`），
统一改为 `AiSchemaEnums.ParseGeneratableControlTypeOrLabel(c.Type)`。

### 3.6 `ControlsBatchSchema` 的括号账

`[[ENUM]]` 替换进来的是一整个 JSON 数组（`["Button", …]`，**自带方括号**），
所以 schema 文本必须写成 `"enum":[[ENUM]]`。
旧写法 `"enum":[[[ENUM]]]` 是配合一个**不带括号**的旧辅助函数 `ToJsonList` 才成立的；
换成自带括号的 `AiSchemaEnums` 之后就变成 `[["Button", …]]` —— 枚举里套数组。
这个错误**当时就被运行时守卫抓到**（`JsonReaderException: 'B' is an invalid start of a value`），
是"先有运行时守卫、后重构"的收益。

---

## 4. 守卫：两层 + Prompt 侧一层

### 4.1 静态：`tests/HmiEditor.AI.Tests/AiSchemaEnumContractTests.cs`（13 条）

扫描面：`src/**/*.cs` **与** `*.json`（跳过 `obj/`、`bin/`），
找出所有形如 `"enum"\s*:\s*\[` 的字面量，逐个断言"要么是产品枚举的全集，要么在登记表里"。

**关键：核心断言用 `SetEquals`（全集相等），不是子集。** 用子集的话，
"手抄 21 项、缺 4 项"这种**主要缺陷形态**恰好会被放过。

| 用例 | 作用 |
| --- | --- |
| `EveryLiteralEnum_InSource_IsEitherAProductEnum_OrRegistered` | 主守卫，`SetEquals` |
| `RegisteredNonProductLiterals_StillExistInSource` | 登记表**反向**：死登记要报 |
| `Scanner_ActuallyCoversCsFiles` | **扫描面自检**：证明扫描器真的在看 `.cs` |
| `GeneratableControlTypes_IsEnumMinusScreenContainer` | 策略钉住 |
| `GeneratableControlTypes_CompactForms_MatchTheList` | JSON/CSV 两种紧凑形式与清单一致 |
| `TryParseGeneratableControlType_AcceptsEveryGeneratableType` | schema 宣告的 25 项，工具必须都收 |
| `TryParseGeneratableControlType_Rejects` | 空/未知/Screen 三种拒绝 |
| `ParseGeneratableControlTypeOrLabel_FallsBackToLabel` | 降级路径 |
| `JsonArray_ForEverySchemaEnum_IsValidJsonMatchingTheEnum` | 产物本身是合法 JSON 且与枚举一致 |

登记表（`NonProductEnumLiterals`）当前 2 条：
`["final_answer","tool_call"]`（LlmMessage 角色的字面量，非产品枚举）、
模板分类 7 项（`ControlTemplate.Category` 是自由字符串，见 §7）。

### 4.2 运行时：`tests/HmiEditor.App.UiTests/AgentToolSchemaContractTests.cs`（12 条）

静态扫描看不到"工具工厂**实际产出**的 schema 长什么样"（内插、`Replace`、条件拼接都在运行时发生）。
这一层真的构造出工具表，逐个解析 `input_schema`：

| 用例 | 作用 |
| --- | --- |
| `EveryToolSchema_IsValidJsonObject_WithNonEmptyStringEnums` | **schema 必须是合法 JSON**，且每个 `enum` 非空、元素全是字符串 —— 专抓"语法坏"那一类 |
| `ControlTypeEnums_MatchGeneratableControlTypes` | 与 `AiSchemaEnums` 逐值对齐 |
| `ControlTypeEnums_DoNotOfferScreenContainer` | 策略钉住（含 `add_control` / `add_controls_batch` / `add_control_to_template`） |
| `CreateAlarm_Enums_MatchProductEnums` | severity + type |
| `UpdateSecurity_OperationModeEnum_MatchesProductEnum` | 抓出过 `Runtime` |
| `UpdateRuntime_LogLevelEnum_MatchesProductEnum` | |
| `CreateConnection_DriverEnum_MatchesBuiltinDriverCatalog` | |
| `DistinctSchemaEnums_AreNotAccidentallyIdentical` | 防止"复制粘贴时改错了属性名"导致两个不同字段共用一份 enum |

设计细节：`EnumOf(tool, property)` 找不到**唯一**一处就断言失败 ——
防止"断言了一个不存在的属性"变成恒绿。

### 4.3 Prompt 侧：`PromptTemplateContractTests`（15 条，本轮 +3）

本轮新增：

| 用例 | 作用 |
| --- | --- |
| `PromptTemplates_ListingControlTypes_UseThePlaceholder` | 模板正文里不得手抄「逗号/顿号/斜杠串起 ≥4 个名字」的清单。判据选"串起来 ≥4 个"而非"出现 ≥N 个"，避免误伤散文（例：`text: 文本内容（Label/Button 使用）`） |
| `TemplateSystemPrompt_ActuallyReceivesControlTypes` | **反向**：换成占位符之后，System 提示必须真的收到 25 个类型名（否则"漂移"只是换成了"泄漏占位符"） |
| `AllSchemas_CoversEveryGetSchemaMethod` | `AllSchemas()` 是手抄清单，用反射钉成双向，见 §6 |

已有的「占位符登记」两条本来就是双向的（`EveryPlaceholder_InEveryTemplate_IsRegistered` +
`EveryRegisteredPlaceholder_IsUsedBySomeTemplate`），本轮未改。

---

## 5. 负向验证（4 组，红集互不相同）

`.refactor-tmp/run-nv.sh` + `nv.py`，每组只做「只改这一件事的等价替换」，跑完还原。

| NV | 注入 | AI 侧红 | App 侧红 |
| --- | --- | --- | --- |
| 基准 | — | —（29 ✓） | —（12 ✓） |
| **a** | 拆掉"不生成 Screen"策略 | `ControlTypeNames_FollowEnumDeclarationOrder`、`BothSchemas_ExcludeScreenRootContainer`、`TryParseGeneratableControlType_AcceptsEveryGeneratableType`、`GeneratableControlTypes_IsEnumMinusScreenContainer` | `ControlTypeEnums_DoNotOfferScreenContainer` ×3 |
| **b** | `create_alarm.type` 换回手抄清单（`.cs` 字符串里的字面量） | `EveryLiteralEnum_InSource_IsEitherAProductEnum_OrRegistered` | `CreateAlarm_Enums_MatchProductEnums` |
| **c** | `GetScriptSchema` 绕过渲染链 | `AllSchemas_LeaveNoUnreplacedPlaceholder_AndAreValidJson` | 无 |
| **d** | 让扫描器只扫 `.json`（漏掉 `.cs`） | `RegisteredNonProductLiterals_StillExistInSource`、`Scanner_ActuallyCoversCsFiles` | 无 |
| 还原 | — | —（29 ✓） | —（12 ✓） |

四组红集**两两不重叠**，且每组都精确命中它该命中的那一层：

- a 命中「策略钉住」（静态 + 运行时都钉）
- b 命中「静态扫描面的 `.cs` 覆盖」（这正是上一轮的盲区）
- c 命中「运行时渲染链」（静态扫描完全看不到 `GetScriptSchema` 走没走 `RenderSchema`）
- d 命中「扫描面自检 + 登记表反向」—— 注意 `RegisteredNonProductLiterals_StillExistInSource`
  也红了，因为登记表里的 `["final_answer","tool_call"]` **只出现在 `.cs` 里**，
  扫描器不看 `.cs` 就误判"这条登记已失效"。**两把锁互为印证**。

### 5.1 踩坑一：那一次"基准全绿"是**假绿**

第一次跑完 NV，`PromptTemplateContractTests` 有 2 条红，而且**还原之后仍然红**。
一度以为是"假红"（mtime 那类），但比对盘上文件与备份**逐字节一致**，还原是忠实的。

真因在输出里：**基准那一次跑出 26 条，后续每次都是 28/29 条。**
差的那 2 条，恰好是我在启动 NV 之前最后加的两条守卫 —— 基准那次用的是**旧程序集**，
那两条守卫从未被执行过，所以"从没绿过"。

> **纪律**：`基准全绿` 本身需要证据 —— 至少要确认跑到的**用例数**与源码里的用例数一致。
> 只看"失败 0"是不够的：少跑了两条也是失败 0。
> 这次是 NV 的注入语义（"b 应该只红 1 条"）与实测（红 3 条）对不上，才把问题顶出来的。

### 5.2 踩坑二：harness 自己的盲区

NV-c 第一次跑的注入是「删掉 `RenderSchema` 里那一行」。结果**整段输出是空白** ——
因为 `run()` 里那条 `grep -E '^\s*失败 |失败:|已通过!'` 在**构建失败**时一行都匹配不到，
于是"构建挂了"被显示成"这一组没红"。

已修：`run()` 改成先取全部输出，若拿不到测试摘要就显式打印尾部 12 行并注明"多半是构建失败"。
同时 NV-c 的注入也换成**一定编得过**的形式（`GetScriptSchema` 直接 `LoadTemplate`），
因为它更贴近历史缺陷（"没接渲染链"）而不是"少了一个表达式"。

### 5.3 踩坑三：mtime（上一轮教训，本轮沿用）

`nv.py` 的 `put()` 用 `shutil.copyfile` + `os.utime(dst, None)`，
**不用** `copy2` / `cp -p` —— 保留 mtime 会让 MSBuild 判定"source 不比 dest 新"
从而跳过重编译/复制，跑出来的是上一轮破坏版的**假红**。

---

## 6. 守卫自身的盲区（本轮最该记住的一条）

上一轮的教训是「只扫 `*.json` ⇒ 漏掉藏在 `.cs` 字符串模板里的 21 项字面量」。
本轮把它变成规则：**任何扫描器/登记表都必须自带"我扫到了什么"的自检**。

| 盲区 | 修法 |
| --- | --- |
| 扫描器只扫 `.json` | `Scanner_ActuallyCoversCsFiles`：断言扫描结果里确实含 `.cs` 来源的命中 |
| 登记表可能留死条目 | `RegisteredNonProductLiterals_StillExistInSource`（反向） |
| `AllSchemas()` 是手抄清单，新增 `Get*Schema()` 会漏 | `AllSchemas_CoversEveryGetSchemaMethod`：反射枚举 `Get*Schema`，与清单**双向**比对 |
| `EnumOf(tool, property)` 可能断言到不存在的属性 ⇒ 恒绿 | 唯一性断言：命中数必须 == 1 |

---

## 7. 遗留（本轮不动，已登记）

- **`TemplateGenerationSchema.category` 硬约束了自由字段**：`ControlTemplate.Category`
  在产品侧是自由字符串（默认 `"通用"`），schema 却给了 7 项固定 `enum`。
  这是"**硬约束了一个自由字段**"，与"手抄枚举导致漂移"是两回事 ——
  建议去掉 `enum`（改成 `"type":"string"`），但会影响模型输出分布，需单独评估。
  已登记进 `NonProductEnumLiterals` 并写明理由。
- `TimeoutSeconds = 60` 对 Agent 多轮工具调用偏短。
- AI 助手 SSE 流式（`final_answer` / 并行 `tool_call` / 取消按钮）。
- Dock.Avalonia 面板空白（需要 GUI 冒烟）。
- `hmi_actions.c` 的报警/趋势/登录权限与凭据校验仍是 TODO。

---

## 8. 验证清单

| 项 | 结果 |
| --- | --- |
| `dotnet build HmiEditor.slnx` | 0 警告 / 0 错误 |
| `AiSchemaEnumContractTests` | 13 条全过 |
| `AgentToolSchemaContractTests` | 12 条全过 |
| `PromptTemplateContractTests` | 15 条全过 |
| `HmiEditor.AI.Tests` 全量 | 见提交信息 |
| 负向验证 | 4 组红集互不相同，还原后回到全绿 |
| `scripts/ci-gate.sh` | 全步通过 |
