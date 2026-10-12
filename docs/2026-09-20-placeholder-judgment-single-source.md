# 占位符判据唯一实现（阶段 43）

> 日期：2026-09-20 · 提交：`73cb31f`（7 files，**+338 / −65**）
> 上游：阶段 42 `docs/2026-09-20-core-script-message-localization.md`（同一条本地化链路的上一环）

## 0 一句话

"模板里的占位符与实参对不对得上"这个判据，在本仓库有**五处实现**（四份"模板 vs 实参" + 一份 zh/en 一致性）、
**两种口径**；而**运行期**用的那份是**错的那种**（数占位符*出现次数*）
⇒ 它把"同一个占位符出现多次"这种**合法模板**判成不匹配、走"原样返回"分支，
**用户在界面上看到 `{0}` / `{1}` 花括号字面量**。而三处测试守卫用的是另一套口径，
所以**守卫全绿、界面是花括号**。修法是把判据收成一份（运行期 + 三处守卫同源）并补上"洞"这一层。

## 1 问题：四份实现、两种口径

| # | 位置 | 判据实现 | 够不够 |
|---|---|---|---|
| ① | **运行期** `Core/Services/LocalizationService.Format`（两个重载各一份） | 数占位符**出现次数**，与 `args.Length` 比 | ❌ 重复占位符 ⇒ **假不符**（原样返回）；索引有洞 ⇒ **放行给 `string.Format`** ⇒ 抛 |
| ② | `App.UiTests/ScriptIssueLocalizationGuardTests`（阶段 42 新写） | 不同索引 + 恰好 `0..n-1` | ⚠️ 规则对，但**正则不认格式说明符** `{0:N2}` ⇒ 会漏看 |
| ③ | `App.UiTests/PluginTextLocalizationGuardTests`（插件 `hw.issue` 决定表） | 数**出现次数** | ❌ 同上（这类模板里恰好没有重复占位符，所以从未暴露） |
| ④ | `App.UiTests/LocalizedLogCallSiteTests`（日志调用点） | 数**出现次数** | ❌ 同上 |
| ⑤ | `Core.Tests/LocalizationTests`（zh/en 一致性） | 两边的**出现次数**相等 | ❌ 数量相等但索引不同（zh `{0}{1}` / en `{0}{2}`）⇒ 放过，而 en 那份运行期**抛** |

**这是"唯一实现"违约的教科书形态**：同一个概念在多处各写一遍，它们对"合法"的定义不同，
于是**守卫守的不是运行期的行为**。

### 1.1 先量事实（只读盘点，两本字典各 2285 键）

| 类别 | zh-CN | en-US | 说明 |
|---|---|---|---|
| 同一个占位符出现多次 | **1** | **1** | 唯一那条是阶段 42 加的 `script.issue.lua_runtime_symbol_undetermined`（`{1}` 有意用两遍） |
| 索引有洞 | 0 | 0 | 今天没有；修完之后三层判据都能抓（见 §5 负控） |
| 转义花括号 `{{` / `}}` | 0 | 0 | 今天没有；新判据按 .NET 语义**跳过**它们 |

> 盘点脚本：`.workbuddy/tmp/placeholder-audit.py`（只读）。
> **诚实结论**：今天除那条模板外，没有任何一条守卫因为口径不同而误判 —— 这不是"正在冒烟的火灾"，
> 而是①一个**已可达**的运行期缺陷（下面 1.2）、②一类**目前无人能抓**的缺陷（洞）、
> ③一个"改一处必须记得改四处"的稳定漂移源。

### 1.2 那条模板真的会到达界面

`LuaDialect.Target` 现在是 `Unknown`（目标版本未定，**这是当前设计状态**），
`CheckRuntimeSymbols` 对命中一律给 `Undetermined`，`ScriptEngineService` 经 `DescribeHit`
拿到 `(Code, Args)`：Code → `script.issue.lua_runtime_symbol_undetermined`，**Args 2 个**。
字典模板有 **3 处**占位符出现（`{0}`×1、`{1}`×2）⇒ 运行期判"3 ≠ 2" ⇒ 原样返回 ⇒
脚本编辑器里显示：

```
{0} 需要 {1} 或更高，设备目标未定、暂无法判定（设备若为 {1} 或更高则无此问题）
```

**修前证据**（判据先写、后修）：`.workbuddy/tmp/red-1.log`，`EXIT=1`

```
Assert.DoesNotContain() Failure: Sub-string found
String: "{0} 需要 {1} 或更高，设备目标未定、暂无法判定（设备若为 {1} 或更高则"
Found:  "{0}"
```

## 2 归因：为什么"守卫绿着、界面却是花括号"

三个阶段 42 的守卫本该抓到这条，没抓到，原因不是它不够严，而是**它比错了对象**：

| 层面 | 阶段 42 的守卫做了什么 | 缺了什么 |
|---|---|---|
| 比对 | "模板 vs Core 传的 `Args`" | 没走**运行期** `Format`（那一步才有"原样返回"这条分支） |
| 口径 | 自己那份（不同索引） | 与运行期的口径不一致 ⇒ 两边都"自洽"，合起来矛盾 |

> 教训（已进记忆）：**判据与被判对象的口径必须同源**。守卫自己实现一份"我认为正确的规则"，
> 在规则与运行期不一致时，绿是**假绿**。

## 3 修法

### 3.1 判据收口到 Core（唯一实现）

新增 `src/HmiEditor.Core/Services/LocalizationPlaceholders.cs`（`public static`）：

| 成员 | 职责 |
|---|---|
| `Indexes(template)` | 去重升序的占位符索引；认 `{0:N2}`；**跳过**转义花括号 `{{`/`}}` |
| `MatchesArgumentCount(template, argCount)` | 规则两条：不同索引个数 == 实参个数；索引**恰好** `0..n-1` |
| `DescribeIndexes(template)` | 诊断文本（`[{0}, {1}]` / `无占位符`），失败消息靠它区分"少一个"与"有洞" |

放在 **Core** 而不是测试工程：运行期要用它，守卫也必须用**同一个** —— 这才是本次修复的实质。

### 3.2 运行期改判据（顺带消掉同文件内的重复）

`LocalizationService.Format` 两个重载原本各带一段**逐字相同**的 8 行判断（判据的又一份拷贝）。
现收成一个 `FormatCore(key, culture, args)`，两个重载各一行转发。

**行为变化（只有两处，都是修缺陷）**：

| 情形 | 修前 | 修后 |
|---|---|---|
| 模板里同一占位符出现多次（合法） | 判"不符"⇒ **原样返回**（界面出现花括号） | 正常渲染 ✅ |
| 模板索引有洞 | 判"符"⇒ 交 `string.Format` ⇒ **抛 `FormatException`** | 判"不符"⇒ 原样返回（不抛） |

"不符就原样返回、不抛"是既有契约（见 `Format` 的 XML 注释）；本次只是把"不符"的定义修对。

### 3.3 四处守卫改为调用它

| 文件 | 改动 |
|---|---|
| `ScriptIssueLocalizationGuardTests` | 本地 `PlaceholdersMatch` 变成一行转发（注释写清"为什么必须同源"）；诊断串改 `DescribeIndexes` |
| `PluginTextLocalizationGuardTests` | 决定表里的"数出现次数"改为调判据（表本身保留 —— 它是"新增带数据规则必须登记"的显式声明） |
| `LocalizedLogCallSiteTests` | 同上；顺手删掉已无人用的 `Placeholder` 正则字段 |
| `Core.Tests/LocalizationTests` | zh/en 一致性从"数量相等"升级为"**索引序列相同**"（原用例 `ResourceFiles_PlaceholderCountMatches` 被它取代） |

## 4 判据

| 判据 | 在哪里 | 覆盖什么 | 负控 |
|---|---|---|---|
| 边界判定表（11 行） | `Core.Tests/LocalizationPlaceholdersTests` | 判据本身的每个方向；**每行注明"旧口径会怎样"** | 表内正反成对（重复/有洞/少给/多给/格式说明符/转义） |
| `Indexes` 去重升序 + 跳转义 | 同上 | 解析细节 | `Assert.Empty(Indexes("{{0}} 与 }} 与 {{"))` |
| 全库**形状**：索引必须连续 | `LocalizationTests.EveryTemplate_HasConsecutivePlaceholderIndexes` | 两本字典 2285×2 键 | 注入洞 ⇒ 红（§5） |
| 全库**渲染**：每条模板真走 `Format` | `LocalizationTests.Format_RendersEveryPlaceholderOfEveryTemplate` | 运行期行为（哨兵值探每个实参） | 同上 |
| 洞的契约 + 底层事实 | `LocalizationTests.Format_TemplateWithHole_ReturnsRawInsteadOfThrowing` | ①`string.Format` 对洞**会抛**（钉住"为什么严重"）②`Format` 不抛 | 自证 |
| **回归判据**（真数据） | `LocalizationTests.Format_RepeatedPlaceholder_RendersArguments` | 那条真模板必须渲染出实参 | **修前红**（§1.2） |

全库扫描类判据都带"下限自检"（`checkedTemplates >= 100`）—— 防枚举失效后恒绿。

## 5 负控：一个注入 ⇒ 三层判据同时点名

往两本字典**同一个键**注入一个洞（两侧同改，让"zh/en 索引一致性"按预期**保持绿**）：

```diff
- "hw.issue.resolution_exceeds_default": "画面「{0}」({1}x{2}) 超过本板默认分辨率 …"
+ "hw.issue.resolution_exceeds_default": "画面「{0}」({1}x{3}) 超过本板默认分辨率 …"   # zh 与 en 同改
```

读数：`Core_EXIT=1` / `App_EXIT=1`，红的是

| 判据 | 层的性质 | 点名的内容 |
|---|---|---|
| `EveryTemplate_HasConsecutivePlaceholderIndexes` | 全库形状 | `zh-CN hw.issue.resolution_exceeds_default: [{0}, {1}, {3}]` |
| `Format_RendersEveryPlaceholderOfEveryTemplate` | 运行期行为 | `… 里的 {0} 没有被渲染，Format 返回了：画面「{0}」({1}x{3}) …` |
| `PluginTextLocalizationGuardTests.IssueTemplates_PlaceholderCountsMatchPluginArgs` | 插件侧决定表 | `… 的模板占位符 [{0}, {1}, {3}] 与登记值 3 个实参对不上` |

zh/en 一致性判据**保持绿**（24 通过）—— 说明这两类判据是**独立**的，不是同一条被数了两遍。

还原后复跑：`已通过! 失败: 0，通过: 26`。

## 6 边界表当场抓出的两个错（都是我写的期望值错）

| 我写的 | 实际 | 真相 |
|---|---|---|
| `{{0}} 与 {1}` 配 1 实参 ⇒ 期望 `true` | `false` | 模板里**真占位符只有 `{1}`**，索引不从 0 起 ⇒ 判据拒它是对的（`string.Format` 也会抛） |
| `DescribeIndexes("{1}{0}")` ⇒ 期望 `[0, 1]` | `[{0}, {1}]` | **实现是对的、注释写错了**（沿用阶段 42 失败消息里 `[{0}, {1}]` 的写法） |

> 这两条正好演示了边界表的价值：它先纠正的是**我自己**。修正是改期望值与注释，**没有**动判据。

## 7 读数

| 项 | 值 |
|---|---|
| 提交 | `73cb31f`（7 files，**+338 / −65**），`2026-09-20T19:01:55+08:00` |
| 构建 | 门禁第 3 步（Release + **警告即失败**）：**0 警告 / 0 错误** |
| 判据（定向） | `LocalizationTests` **26 通过**；新增文件 `LocalizationPlaceholdersTests` 计入后 28 条 —— 首跑的 2 条红是**我自己的期望值错**（§6） |
| 修前红 | `.workbuddy/tmp/red-1.log`（`EXIT=1`，`Format` 返回花括号字面量） |
| 负控（注入洞） | **3 条红，跨三层**（全库形状 / 运行期行为 / 插件决定表）+ zh/en 一致性按预期**绿**；还原后 26 绿 |
| `Core.Tests` | **2115** = 2107 + 单独跑的第二段 8（阶段 42 的 2109 ⇒ **+6** ＝本轮新增 6 条判据） |
| `App.UiTests` | **262**（只改"怎么比"，**不加用例**） |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**；八程序集合计 **2946** ＝ 阶段 42 的 2940 **+6**，其余七个程序集**逐条一致** ⇒ 纯净增 |
| 门禁日志 | `.workbuddy/tmp/ci-gate-2026-09-20-placeholder.log`，mtime **`19:03:59`** ⇒ 晚于提交 **124 秒**（时序合法） |
| 过程记录 | 第一次门禁"失败"是**我自己的命令**错：fish 里没有 `$?`（应写 `$status`）⇒ 脚本跑完了却被判失败，改用 `bash -c` 重跑才拿到 `GATE_EXIT=0` |

## 8 仍未闭环

| # | 项 | 状态 |
|---|---|---|
| ① | `Errors` 侧仍只显示首条（阶段 41 遗留） | 未动（本轮只碰占位符判据） |
| ② | Core 侧其它用户可见文案未盘点（阶段 42 §7 ③） | 未动 |
| ③ | 目标 Lua 版本未定 ⇒ `LuaDialect.Target` 触发线 | 未动 |
| ④ | 局部变量遮蔽 / 表键 / 长字符串是已知假阳性 | 未动（阶段 41 取舍） |
| ⑤ | `CheckSuspiciousApis` 未搬（阶段 39 ④） | 未动 |
| ⑥ | `MainWindow` 从未被任何测试构造（登记表 #37） | 未动 |
| ⑦ | **新增**：判据只覆盖"占位符 vs 实参"，**不覆盖**"模板里写错了占位符语义"（例如把 `{0}` 写成 `{1}` 而实参正好给够 —— 编译期与运行期都过得去，只有人看得出） | 未闭环；要治得引入"命名占位符"或按参数字面量做语义校验，属独立议题 |
