# 硬编码中文清单 + 只减不增守卫（登记表 §4 第 27 项）

> 按顺序推进 `docs/2026-09-17-open-items-register.md` §4 第 27 项
> （"运行时动态文案约 100+ 处未入字典；en-US 部分为精简翻译"）。
> 原始登记见 `docs/localization-summary.md` §五.1。
> 日期：2026-09-17

---

## 0. 一句话结论

**登记里点名的例子早已本地化 —— 又是失效登记。** `AnimationEditorView` 的缓动选项
（`AppLocalization.Service[Lang.AnimEasingLinear]` 等 7 项）与 `ToolboxView` 的控件显示名
（`Lang.TbScreen` 等）**都在用字典键**，所以"照登记表动手"会白跑一趟（这已经是本项目第 N 次踩到，
见登记表 §12）。

真实剩余量**反而与"约 100+ 处"接近**，只是分布完全不同：

| 面 | 实测 | 本轮处置 |
|---|---|---|
| `.axaml` 里 `Text=`/`Content=` 直接写死中文 | **14 条 / 2 个文件** | ✅ **本轮全部本地化**（splash 首屏 9 条 + AI 面板按钮 5 条） |
| `.cs` 里"该本地化"的中文字面量 | **226 条 / 27 个文件** | 已逐文件登记基线；**本轮不逐个翻译**（见 §8） |
| **根因：没有任何机制阻止继续写死中文** | —— | ✅ **本轮补上守卫**（只减不增，见 §5） |

> 换句话说：本轮交付的不是"翻译了多少"，而是**把一件永远做不完的事变成一件只会变少的事**。

---

## 1. 判据：分类式，不是 sink 式

第一版扫描器按"这一行是不是控件赋值"（sink 式）来找，得到 **98 条**。**这个数字是错的**：

```csharp
PopulateTaggedCombo(CboAlarmType, new (string, object)[]
{
    ("上限报警 (H)", AlarmConditionType.HighLimit),     // ← sink 在上一行
    ...
```

多行实参的 sink 在前一行，按行匹配会**整片漏掉**（`VariableManagerView` 的 27 条全漏）。
改成**分类式**：取出所有含中日韩字符的字符串字面量，扣掉明确排除的类别，其余全算 —— 得到 **226 条**。

**教训**：判据的形式决定了它看不看得见某类东西。sink 式看起来"更精确"，
代价是它**对跨行写法失明**，而失明的方向恰好是"漏报"（清单偏小 ⇒ 以为快做完了）。

## 2. 排除项与理由（逐条可审）

| 排除 | 理由 |
|---|---|
| `Debug.WriteLine` / `Console.` / `Trace.` | 调试输出，用户看不到 |
| 异常消息（`throw new …Exception("…")`） | 面向开发者；即便经 `ex.Message` 透出，也属"诊断信息不该翻译"（翻译后无法搜索） |
| 出现 `AppLocalization`/`LocService` 的行 | 那里的中文通常只是**传参** |
| `AgentToolFactory.cs` / `AgentSystemPrompt.cs` | **AI 提示词**。翻译它会改变模型行为，属独立议题（要与 prompt 契约一起评估），不该混进 UI 文案清理 |
| `DockEventRoutingDiagnostics.cs` | 内部诊断报告（菜单里手动触发的排障工具） |
| `Program.cs` / `AgentHost.cs` | 启动期日志与内部异常文案 |

## 3. 实测基线（2026-09-17，27 个文件 / 226 条）

> ⚠️ **本节是判据修正前（按行豁免）的旧基线，已被 §9 取代。** 保留它是因为 §9 的
> "量具修正"要跟旧数字对照才看得出改了什么。

| 条数 | 文件 |
|---|---|
| 38 | `Services/EditorCommandCatalog.cs` |
| 29 | `Views/DeviceManagerView.axaml.cs` |
| 27 | `Views/VariableManagerView.axaml.cs` |
| 24 | `MainWindow.axaml.cs` |
| 17 | `Views/DownloadToHmiView.axaml.cs` |
| 10 | `Views/AiChatPanelView.axaml.cs` |
| 8 | `Services/SimulatorBuildService.cs` |
| 6 / 6 | `Wiring.CanvasTools.cs` / `Views/BuildRunDialog.axaml.cs` |
| 4 / 4 | `Controls/DesignCanvas.Editing.cs` / `Views/AiIconDialog.axaml.cs` |
| 3 × 3 | `Services/AppThemeService.cs` / `Services/UiOperationGuard.cs` / `Views/ProjectExplorerView.axaml.cs` |
| 2 / 2 | `Controls/DesignCanvas.Input.cs` / `Views/PropertyPanelView.axaml.cs` |
| 1 × 12 | `Wiring.Canvas.cs`、`Wiring.DockAggregate.cs`、`Controls/TemplateManager.cs`、`Controls/RenderResourceCache.cs`、`Services/PreviewRuntimeService.cs`、`Views/FaceplateLibraryView.axaml.cs`、`Views/ScriptEditorView.axaml.cs`、`Views/UserManagerView.axaml.cs`、`Views/CommunicationConfigView.axaml.cs`、`Design/DefaultTexts.cs` 等 |

**主要性质**：以**日志面板文本**（Device/Download/BuildRun/SimulatorBuild ≈ 60）、
**撤销/操作名**（`Wiring.CanvasTools`、`DesignCanvas.Editing`）、
**下拉显示名**（`VariableManagerView` 的报警类型/级别）、**快捷键提示**（`EditorCommandCatalog`）
为主 —— 都是真实用户可见，但**不在首屏主路径上**，所以优先级低于 §4 那 14 条。

## 4. 本轮补齐的一批：14 条 `.axaml`（首屏 + AI 面板按钮）

**8 个新键 + 6 处复用既有键**（刻意不重复造键）：

| 位置 | 原文 | 键 |
|---|---|---|
| Splash 标题 ×2 | HMI 编辑器 | **复用** `app.title`（值完全相同） |
| Splash 副标题 | HMI 可视化编辑器 | 新 `splash.subtitle` |
| Splash 按钮 | 新建工程 / 打开工程 | 新 `splash.new.project` / `splash.open.project` |
| Splash 按钮说明 | 创建一个空白 HMI 工程 / 浏览并打开已有工程文件 | 新 `splash.new.project.hint` / `splash.open.project.hint` |
| Splash | 最近打开 | 新 `splash.recent` |
| Splash 状态 | 就绪 | **复用** `status.ready` |
| AI 面板 | 新会话 | 新 `ai.new.chat` |
| AI 面板 | 重试 | 新 `gen.retry` |
| AI 面板 | 取消 | **复用** `gen.cancel` |
| AI 面板 | 保存 SVG / 保存 PNG | **复用** `ai.image.save.svg` / `ai.image.save.png` |

**刻意不改用户可见措辞**：Splash 写的是"新建**工程**"，而字典里 `project.new` 是"新建**项目**" ——
本轮保留原文，只把它搬进字典（改了就是**未asked-for 的措辞变更**）。由此发现的术语不一致见 §8。

`SplashWindow.axaml` 原本没有 `xmlns:res`，本轮补上。

## 5. 守卫：`HardcodedLocalizationGuardTests`

| 用例 | 判据 |
|---|---|
| `HardcodedChinese_MatchesBaseline_OnlyDecreases` | 每文件条数与基线**相等** —— 涨了报"新增了硬编码中文"，降了报"必须同步改小基线"（**防清单与事实漂移**） |
| `TotalCount_IsReported_AndDoesNotExceedBaseline` | 总数只许减少（给人一眼能看懂的进度"仪表"） |
| `Scanner_ActuallyFindsHardcodedChinese` | 扫描面自检（文件数 ≥20、条数 ≥150），防扫描器失效后空跑恒绿 |
| `StripComments_RemovesCommentsButKeepsStringLiterals` | 行注释 / 块注释 / **XAML 注释**里的中文都不计入，字符串字面量保留 |
| `ExcludedStatement_MatchesTheJustifiedCategories` | 三类排除的**正例**与一类**负例**（普通控件赋值必须被算进来） |
| `IsExcluded_SeesThrowNewAcrossLines` | 多行 `throw new …Exception(` 的中文实参必须排除（§9 的漏网回归） |
| `IsExcluded_DoesNotLeakIntoTheNextStatement` | 已本地化语句**不豁免下一条语句**的中文 |
| `IsExcluded_CountsLiteralInsideACallEvenUnderLocalizedForeach` | 外层 `foreach` 含 `LocService` 也**不豁免**被 `new Item(...)` 包裹的中文实参 |

**为什么"相等"而不是"不超过"**：本项目的 `ConfigFieldInventory` 已经验证过这条 —— 清单一旦允许
"事实比记录少"，它就会慢慢变成一份过期抄写。相等判定逼着本地化的人**顺手把数字改小**，
于是这份基线**永远等于事实**，总量单调下降。

## 6. 负向验证（3 组，全部命中）

| NV | 注入 | 结果 |
|---|---|---|
| A | 在基线文件里新增一条硬编码中文 | ✅ 命中"**新增了硬编码中文**" |
| B | 本地化掉一条但**忘了改小基线** | ✅ 命中（相等判定） |
| C | 新建一个含硬编码中文的文件 | ✅ 命中"**出现了基线外的新文件**"（探针文件已删除） |

## 7. 验证

| 项 | 结果 |
|---|---|
| `dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** |
| 全量测试（7 程序集） | **2641 通过 / 0 失败**（App.UiTests 192 → **197**；本轮基线 2636 ⇒ **+5**） |
| 负向验证 | **3/3 命中** |
| 本地化字典 | zh-CN / en-US 各 **+8** 键（占位符数量一致，`LocalizationTests` 通过） |
| `scripts/ci-gate.sh` | 见 `docs/project-status.md` 验证记录 |

---

## 8. 范围边界与登记

1. **剩余 187 条**（§9.2 表）**不逐个翻译**：体量与风险都适合按文件分批（每批一个文件、
   一批一次提交）。守卫已经让总量单调，**不会再涨**。
   按性质排，下一批的性价比顺序是：日志面板正文（`DeviceManagerView` 29 +
   `DownloadToHmiView` 17 ≈ 46，标签已入字典、只剩正文）→ `VariableManagerView` 的下拉显示名 27 →
   `ConfigFieldInventory` 30（要先判断它是不是**开发者清单**，见 §9.3 的同类教训）。
2. **术语不一致（新发现，需产品决策）**：同一个概念在字典里有两种叫法 ——
   含「工程」的值 **30** 处、含「项目」的值 **33** 处（`project.new` = "新建项目"、
   `project.settings.title` = "项目设置"，而错误文案与文档多用「工程」）。
   统一会改约 60 处**用户可见措辞**，属产品决策；本轮**刻意未动**。
3. **en-US 校对**（登记原话"部分为精简翻译，未逐字校对"）仍未做：需要**母语级**判断，
   机器批量改只会把"精简"换成"另一种不地道"。建议随 §1 的分批一起做（每批译文人工过一遍）。
4. `src/HmiEditor.AI`（含 `LuaCompletionService` 的脚本模板中文）**不在本守卫扫描面内** ——
   那一层混着提示词与模板，需要与 prompt 契约一起评估。已登记。


---

## 9. 判据修正 + 命令目录收口（2026-09-17，第二批）

第一批把"清单只增不减"的机制立起来了，但它自己有一个**漏报缺陷**，本批修掉，
并顺手清掉守卫暴露出来的最大一块。

### 9.1 判据从"按行"改成"按语句"（修掉一个漏报 + 一个连坐豁免）

原排除规则是**按行**匹配关键词。实测有两个方向都错的后果：

**（a）漏报：跨行的异常消息被算成"待本地化"。**

```csharp
_ => throw new ArgumentOutOfRangeException(
    nameof(action), action, "画布右键菜单动作未映射到编辑器命令"),
```

`throw new` 在**上一行**，于是这条明确写着"异常消息面向开发者"的字符串被计入清单 ——
与 §2 自己声明的判据自相矛盾。（这正是 §1 里"按行匹配对跨行写法失明"的同一个病，
只是这次失明的方向是"把该排除的算进来了"。）

**（b）连坐豁免（更严重，方向是漏报）：整行含 `AppLocalization` 就整行豁免。**

```csharp
Log($"[提示] {AppLocalization.Service[Lang.DeviceConnectFirst]}");
```

正文 `device.connect.first` 早已入字典，段落标签 `[提示]` 却是**写死的中文**；
整行因为含 `AppLocalization` 被豁免 ⇒ **13 条用户可见文案被藏了起来**
（`DeviceManagerView` 6 条、`DownloadToHmiView` 7 条；`LogBox` 是设备面板上的可见
`ListBox`，不是调试输出）。英文界面下这些日志是**中英混排**的。

**新判据**：从字面量往回找**最近一个未闭合的 `(`**（不跨 `;`/`{`/`}`/`(`），
用该调用的"头"匹配排除词；找不到包裹调用时退回**整条语句前缀**（不跨语句，也不跨更外层调用）。
`Debug.WriteLine("…")`、多行 `throw new …(` 都能命中；`foreach (… LocService[…]) items.Add(new Item("中文"))`
里的中文实参**照样计入**。

### 9.2 新基线（判据修正后 + 本批本地化后：26 个文件 / 187 条）

> ⚠️ 其中 `ConfigFieldInventory` 的 30 条已在**第三批**清掉（见 §10）：现行基线见 §10.3。

| 条数 | 文件 |
|---|---|
| 29 | `Views/DeviceManagerView.axaml.cs` |
| 27 | `Views/VariableManagerView.axaml.cs` |
| 24 | `MainWindow.axaml.cs` |
| 17 | `Views/DownloadToHmiView.axaml.cs` |
| 8 | `Services/SimulatorBuildService.cs` / `Views/AiChatPanelView.axaml.cs` |
| 6 | `Wiring.CanvasTools.cs` / `Views/BuildRunDialog.axaml.cs` |
| 4 | `Controls/DesignCanvas.Editing.cs` / `Views/AiIconDialog.axaml.cs` / `Controls/TemplateManager.cs` |
| 3 | `Services/AppThemeService.cs` / `Services/UiOperationGuard.cs` / `Views/ProjectExplorerView.axaml.cs` |
| 2 | `Controls/DesignCanvas.Input.cs` |
| 1 × 10 | `Views/PropertyPanelView.axaml.cs`、`Wiring.Canvas.cs`、`Wiring.DockAggregate.cs`、`Services/PreviewRuntimeService.cs`、`Views/FaceplateLibraryView.axaml.cs`、`Views/ScriptEditorView.axaml.cs`、`Views/UserManagerView.axaml.cs`、`Views/CommunicationConfigView.axaml.cs`、`Design/DefaultTexts.cs` 等 |

**226 → 187**。拆开看（每一笔都可核）：

| 变化 | 说明 |
|---|---|
| `EditorCommandCatalog.cs` **38 → 0** | §9.3 |
| `DeviceManagerView` / `DownloadToHmiView` **±0** | 判据修正**暴露** +6 / +7（§9.1b），同批把 13 条标签入字典抵扣回去 |
| `PropertyPanelView` **2 → 1**、`AiChatPanelView` **10 → 8**、`RenderResourceCache` **1 → 0** | 判据修正（§9.1a）：跨行的异常/调试消息本就不该计入 |
| `TemplateManager` **1 → 4** | 判据修正**暴露** 3 个硬编码控件名（原来被"整行含 `AppLocalization`"连坐豁免）⇒ 见 §9.5 |

### 9.3 `EditorCommandCatalog`：删掉一份会撒谎的抄本，而不是"翻译它"

该文件的 38 条中文里，**26 条根本不是 UI 文案**：

- **25 条**是 `Bindings[]` 的手写 `Description`（`"Ctrl+小键盘+ 放大"` 之类）；
- **1 条**是 `FindConflicts` 的消息模板。

`Description` 的**唯一消费者**是 `FindConflicts()`，而它的**唯一消费者是一个测试**。
更关键的是：它的内容与 `Key` + `Modifiers` **完全重复**，是**手工维护的第二份事实** ——
改了键位忘了改文案，这个"冲突诊断"就会**静默说谎**，而它恰恰是用来判断"到底哪里撞了"的。

所以这一步**不是**把它翻译成英文，而是**删掉抄本**：

| 处置 | 内容 |
|---|---|
| 删除 | `ShortcutBinding.Description` 参数（25 处实参） |
| 新增 | `ShortcutBinding.Chord` —— **由 `Key`/`Modifiers` 推导**（`Ctrl+OemPlus`、`Esc` 等枚举名） |
| 改写 | `FindConflicts` 消息改成纯 ASCII 的开发者诊断（`"Ctrl+C is bound to Copy / CycleTheme"`），可搜索、不会被本地化 |
| 本地化 | 剩下 **11 条** `Describe(EditorCommand)` —— 它们是**用户可见**的：`MainWindow` 用它拼撤销/重做菜单项（`Loc["project.undo"] + " " + UndoDescription`） |

`Describe` 的 11 条按原措辞入字典（**刻意不改措辞**）：`cmd.desc.group`…`cmd.desc.distribute.v`。

### 9.4 `FindConflicts` 原来**没有能力变红**

`FindConflicts()` 只读静态 `Bindings`，而表里**本来就没有冲突** ——
`Bindings_HaveNoConflicts` 这条断言在分组逻辑写坏时**照样是绿的**（"守卫绿了 ≠ 守卫有能力红"）。
本批把核心逻辑抽成可注入的 `internal FindConflicts(IEnumerable<ShortcutBinding>)`，
并加了人造冲突的用例；负向验证里去掉 `.Where(g => g.Count() > 1)` 后，
新用例**和**旧用例**同时**报红（§9.6 NV-D），这才说明它有牙齿。

### 9.5 新登记的待决项：模板控件名要不要翻译

`TemplateManager` 里 `new() { Name = "启动", Text = AppLocalization.Service["tpl.startstop.btn.start"] }`
这种行：`Text`（按钮标题）已本地化，`Name`（控件名）还是中文。
判据修正后这 3 个名字（`启动`/`停止`/`电机状态`）**照实计入基线**，因为
`HmiControl.Name` 在项目树/属性面板里**用户可见**。

但**本批刻意不翻译它们**：控件名是**数据标识**（进生成代码、被脚本/绑定引用），
翻译它会让**生成物随 UI 语言变化** —— 属产品决策，与 §8.2 的术语统一、登记表 #20b 同族。
已登记为新待决项。

### 9.6 负向验证（5 组，全部命中**指名**用例）

每组都要求：注入后测试**确实跑起来了**（非编译失败），且**指名用例**出现在失败列表里；
还原后按 sha256 校验与注入前**逐字节一致**。

| NV | 注入 | 命中的用例 |
|---|---|---|
| A | 在基线文件里新增一条硬编码中文 | `HardcodedChinese_MatchesBaseline_OnlyDecreases`（+ 总数用例） |
| B | 从字典里删掉 `cmd.desc.group` | `EveryLangConstant_ResolvesInBothLanguages` |
| C | 去掉推导里的 `Shift` 修饰键 | `Chord_IsDerivedFromKeyAndModifiers` |
| D | 去掉 `FindConflicts` 的 `.Where(g => g.Count() > 1)` | `FindConflicts_ActuallyReportsAConflict`（+ **旧**用例 `Bindings_HaveNoConflicts`） |
| E | 把 `IsExcluded` 还原成"按行" | `IsExcluded_SeesThrowNewAcrossLines`（+ 基线用例） |

> NV-E 顺带证明了一件事：**基线数字是绑在判据上的** —— 判据一改回旧行为，记录的条数立刻不匹配。

### 9.7 新增守卫：`LangKeyCoverageTests`

补上一个此前完全没人看的洞：**`Lang` 常量 ↔ 字典键的闭合**。

`LocalizationService` 找不到键时**回退成键名本身**，所以"忘了往字典加一条"既不抛异常也不编译失败，
界面上只是安静地显示 `cmd.desc.align.left`。既有 `LocalizationTests` 只保证两本字典**彼此**键集一致 ——
**两本一起漏掉同一个键时，parity 依然是绿的**。

新用例用**反射枚举 `Lang` 的常量**（手写清单就是又一份会漂移的抄本）：
每个常量必须在 zh-CN / en-US 里都能解析出**非键名**的值；另有扫描器自检（≥1000 个常量）
与"键值唯一"用例。实测 **1782 个常量全部解析成功**。


---

## 10. 第三批：`ConfigFieldInventory` 的 30 条悬停说明（2026-09-17）

第二批留下的下一批候选里，`ConfigFieldInventory`（30 条）标着"要先判断它是不是**开发者清单**"
—— 按 §9.3 的教训先查消费点，结论是**用户可见**：

```
ConfigFieldInventory.NotWired[].Note
  → NotYetEffective.Mark(control, reason)          // SettingsDialog / ProjectSettingsView
  → ToolTip.SetTip(control, Loc.Format(Lang.SettingsNotEffectiveTip, reason))
```

即**灰显控件的悬停提示**（"尚未生效：命令栏显隐没有任何消费点……"）。这 30 条必须入字典。

### 10.1 处置：键**推导**出来，而不是手写

没有引入 `Note` 之外的任何新事实：键由条目自己的 `Type` + `Field` 推导。

| 之前 | 现在 |
|---|---|
| `string Note` 实参（30 条中文，写死在清单里） | 参数**删除**；新增 `NoteKey => $"cfg.note.{Key}"`（`Key` 已是 `类型名.字段名`） |

**为什么推导比手写键好**：手写键或手写文案都会多出一份"与 `Type`/`Field` 并行维护"的事实 ——
复制一条条目时最容易忘记改的就是它，而改错了（说明与字段对不上）**没有任何东西会报**。
推导之后，键只可能跟着那两个字段走。

### 10.2 顺带修掉一个潜在缺陷：语言会被冻结

`NoteKey` 返回**键**而不是解析好的文案，解析放在 `NotYetEffective.Mark` 里。这不是风格问题：
`ConfigFieldInventory` 是 `static` 字段，**静态初始化只跑一次** ——
若在清单里就 `AppLocalization.Service[…]` 解析成字符串，语言会冻结在**首次访问那一刻**，
之后切换语言，悬停提示不会跟着变。清单里因此只允许出现键。

新增两条守卫（`ConfigFieldInventoryTests`）：

| 用例 | 判据 |
|---|---|
| `EveryEntry_HasATooltipReasonInBothLanguages` | 每个条目的 `NoteKey` 在 zh-CN / en-US 里都必须解析出**非键名**的值（索引器找不到键时回退成键名，正好用来判定"字典里没有"）；带清单条数 ≥25 的自检 |
| `TooltipReasons_ArePassedAsKeys` | 扫两个面板的源码，`NotYetEffective.Mark(...)` 的实参**必须是 `entry.NoteKey`** —— 防止有人把解析好的文案塞回去（那会重新引入"冻结语言"） |

### 10.3 新基线：25 个文件 / 157 条

| 变化 | 说明 |
|---|---|
| `ConfigFieldInventory.cs` **30 → 0** | 30 条说明入字典（键 `cfg.note.<类型名>.<字段名>`） |

**187 → 157**。`Lang` 常量没有为这 30 个键各加一条 —— 键是推导出来的，
靠 `ConfigFieldInventoryTests` 的两条新守卫闭环（而不是靠清单）。

### 10.4 本批顺带更正的一处**自己的错误**（阶段 20 的"0 警告"）

阶段 20 在 `DeviceManagerView` / `DownloadToHmiView` 里加了个日志标签辅助方法叫 `Tag(string)`，
它**隐藏了继承来的 `Control.Tag`**（CS0108）。而阶段 20 的验收写着"0 警告 0 错误" ——
**那个读数是不可信的**：我用的是**增量构建**（`dotnet build`，两个项目此前已被定向测试编译过），
它根本不重新编译这两个文件，于是既没重新报错、也就没打印这条警告。

本批更正：

1. 辅助方法改名 `LogTag`（名字撞框架属性还会误导读者以为它在动 `Control.Tag`）；
2. **全量非增量构建**实测 0 警告：`dotnet build HmiEditor.slnx -c Release --no-incremental`；
3. **把判据补进门禁**，而不是只靠人眼：
   - `--no-incremental` 成为门禁构建步的一部分（否则这一步可能是一次 no-op）；
   - 构建输出**出现 `: warning <码>` 即判红**并打印去重后的警告清单。
   实测：把 `Tag(string)` 加回去 ⇒ 门禁 **exit 1** 并指名 `warning CS0108`；
   还原后逐字节一致。

> **教训**：`dotnet build` 是增量的，"这次没报"与"没有警告"是两件事。
> 一个只在**全量构建**时才出现的读数和只在**空队列**时才成立的断言一样，
> 都不能当作验收证据 —— 判据必须自己保证重跑一遍真的重算过。


---

## 11. 第四批：`UiOperationGuard` 的操作名（2026-09-17）

按 §8.1 的性价比顺序本该继续做设备日志正文，但**先发现了一处横切缺陷** ——
它同时落在好几个待办文件里，先修它能让后面的批次更干净。

### 11.1 那 3 条模板 + 12 个调用点

`UiOperationGuard.Run/RunAsync` 的第一个实参是"操作名"，它有两处去处：

```csharp
AppLog.Error($"{operation}失败", ex);                       // 落盘日志
report($"{operation}失败: {ex.Message}");                   // ← 用户可见
```

`report` 是视图状态行或**全局状态栏**（`UiSink`）—— 也就是说，一个英文界面下
失败提示会是 `"选择文件失败: Access denied"`。而 12 个调用点传的全是**中文字面量**：

| 调用点 | 操作名 |
|---|---|
| `ProjectExplorerView` ×3 | 重命名画面 / 删除画面 / 重命名控件 |
| `FaceplateLibraryView` | 删除模板 |
| `ScriptEditorView` | AI 生成脚本 |
| `UserManagerView` | 删除用户 |
| `DeviceManagerView` ×2 | 选择文件 / 选择目录 |
| `AiIconDialog` | 生成图标 |
| `DownloadToHmiView` | 选择 HMI 程序 |
| `PropertyPanelView` | 编辑表格单元格 |
| `AiChatPanelView` | 重试 Agent 任务 |

**处置**：参数改名 `operation` → `operationKey`，模板入字典
（`ui.op.failed` / `ui.op.failed.detail` / `ui.op.report.failed`），12 个调用点改传 `Lang.UiOp*`。

### 11.2 两条新守卫（源码契约 + 行为）

只把调用点改成键是不够的 —— **下一个调用点照样可以传中文字面量**，没有编译期约束。所以：

| 用例 | 判据 |
|---|---|
| `EveryCallSite_PassesALocalizationKey` | 扫全部 App 源码，`UiOperationGuard.Run/RunAsync` 的第一个实参必须以 `Lang.` 开头；带"≥10 处调用"的自检 |
| `RunAsync_ReportsTheLocalizedFailureMessage` | 真的失败一次，断言报出来的文案**等于"字典里的操作名 + 字典里的模板"**，且**不含键名**（索引器找不到键会回退成键名 ⇒ 用它判定"文案真的来自字典"） |
| `RunAsync_Cancellation_IsNotReportedAsFailure` | 顺手钉住既有语义：用户取消不是失败，不该弹提示 |

### 11.3 顺带修掉守卫自己的一个"写死阈值"

本批把硬编码总量降到 **142** 时，守卫的**自检**反而报红了：
`Scanner_ActuallyFindsHardcodedChinese` 里写着"条数 ≥ **150**"。
这正是"写死阈值"的典型结局 —— 它每本地化一批就得跟着改，而**改阈值这件事本身没人会注意到**。

改成**与基线挂钩**（`>= Baseline.Values.Sum() / 2`、文件数同理）：阈值随着真实进度一起往下走，
既不会顶破，也不会因为没人维护而失效。

### 11.4 新基线：18 个文件 / 142 条

| 变化 | 说明 |
|---|---|
| `Services/UiOperationGuard.cs` **3 → 0**、`Views/ProjectExplorerView.axaml.cs` **3 → 0**、`Views/FaceplateLibraryView.axaml.cs` **1 → 0**、`Views/ScriptEditorView.axaml.cs` **1 → 0**、`Views/UserManagerView.axaml.cs` **1 → 0**、`Views/PropertyPanelView.axaml.cs` **1 → 0** | 操作名入字典 |
| `DeviceManagerView` **29 → 27**、`DownloadToHmiView` **17 → 16**、`AiIconDialog` **4 → 3**、`AiChatPanelView` **8 → 7** | 同上（各减 1~2 条调用点文案） |

**157 → 142**，文件数 24 → **18**。


---

## 12. 第五批：设备日志面板正文（2026-09-17）

§8.1 里排的下一批。第四批先把横切的操作名清掉了，于是这一批**只剩日志正文** ——
两个文件的「连接」块是逐行重复的，一起做最省。

### 12.1 43 条，两个文件，一次清零

| 文件 | 之前 | 现在 |
|---|---|---|
| `Views/DeviceManagerView.axaml.cs` | 27 | **0** |
| `Views/DownloadToHmiView.axaml.cs` | 16 | **0** |

组成（都是**用户可见**：`LogBox` 是面板上的 `ListBox`，状态行也是用户直接看的）：

| 类别 | 例子 |
|---|---|
| 带段落标签的日志正文 | `[连接] 已刷新串口列表 ({n} 个)`、`[更新] 检查更新: 本地 v{local}`、`[USB包] 请选择输出目录（U盘/TF卡）` |
| `LogError` 的**段落名** | `LogError("连接", ex)` → `LogError(Lang.DeviceLogTagConnect, ex)` |
| 状态/汇总文案 | `zhishu-HMI 设备管理就绪`、`待部署产物: {n} 个源码文件`、`[下载] 源码 {n} 个，资源 {m} 个，HMI 程序(.so) {已部署/未部署}` |

新增 28 个键（含 2 个段落标签 `device.log.tag.deploy` / `device.log.tag.fel`）+ 28 个 `Lang` 常量；
`[部署] 已自动检测到…` **复用**既有 `download.so.auto.found`（同一句话，不再造第二个键）。

### 12.2 新增 `LogTagged` 辅助方法，并顺手补掉它的静默失败模式

两个视图各加一个：

```csharp
private void LogTagged(string tagKey, string messageKey, params object[] args) =>
    Log($"{LogTag(tagKey)} {AppLocalization.Service.Format(messageKey, args)}");
```

这样"标签 + 正文"两条都只能从字典来。但这里有个**静默失败模式**值得单独说：

`LocalizationService.Format` 在**占位符个数与实参个数不符**时**不抛异常** ——
它原样返回未格式化的字符串，只在 `Debug.WriteLine` 里留一句，而 `Debug` 在 **Release 下被编译掉**
⇒ 用户会在日志面板里看到 `已刷新串口列表 ({0} 个)`。编译器看不见、运行时不抛，
只有人盯着日志面板才可能发现。

所以补了一条静态判据 `LocalizedLogCallSiteTests`：扫全部 App 源码，把
`LogTagged(<tagKey>, <messageKey>, args…)` 的实参按**顶层逗号**切开，
断言 `实参个数 - 2 == 该键在字典里的占位符个数`。带"≥15 处调用被校验"的自检。

> 这条判据的**切分器自己也被测出来了**：`SplitTopLevelArguments_HandlesNestingAndStrings`
> 第一版把字符串实参整个丢掉（在 `inString` 分支里 `continue`，没把字符抄进缓冲），
> 于是 `F(x, y), "a,b", C.D` 只剩 `F(x, y), C.D`。**为判据写的判据**当场抓到了这个错。

### 12.3 门禁的"警告即失败"当场抓到我自己

本批给 `LogTagged` 的签名写成 `params object?[]`，而 `Format` 的参数是 `params object[]` ⇒
`CS8620`（可空性差异）。这个警告在**上一阶段刚加**的"构建步 `--no-incremental` + 警告即失败"
下立刻现形（并且是在我**只 build 一次**的输出里）。改成 `params object[]` 即消。

> 这正是上一阶段那条门禁改动的用处：如果没有它，这条警告会像 CS0108 那样躲在增量构建后面，
> 而文档里又会多一句没验证过的"0 警告"。

### 12.4 新基线：16 个文件 / 99 条

**142 → 99**，文件数 18 → **16**。按 §8.1 的顺序，下一批是 `VariableManagerView` 的下拉显示名（27）。


---

## 13. 第六批：`VariableManagerView` 的下拉显示名（2026-09-17）

27 条里其实混了**两类东西**，先分类再动手（这一步比翻译本身重要）：

| 类别 | 条数 | 处置 |
|---|---|---|
| **纯显示**：报警类型/级别下拉标签、报警列表里的短类型名、"未分组"虚拟分组标签、从站显示名、"已停用"后缀 | 25 | ✅ 入字典 |
| **落盘的数据标识**：`Name = $"报警{n}"`、`_selectedAlarm.Name = … "未命名报警"` | 2 | ❌ **刻意不翻译**（见 §13.2） |

### 13.1 25 条显示文案

新增 23 个键：报警类型 8（`var.alarm.type.*`）、级别 4（`var.alarm.severity.*`）、
报警列表短类型名 9（`var.alarm.short.*`，含兜底"未知"）、从站 `var.slave.station`、
"已停用"后缀 `var.alarm.disabled.suffix`。"未分组"**复用**既有 `var.picker.ungrouped`。

顺带把"虚拟未分组分组"的两处字面量一起清掉：它的 `Name` 本来也写着"未分组"，改成
**标签与 `Name` 同源**（先算一次本地化文案，两处都用它）—— 那个对象 `Id` 为空、不落盘，
名字纯粹是显示用的，所以跟随 UI 语言是对的。

### 13.2 那 2 条为什么不翻译：它们是**工程文件里的数据**

```csharp
Name = $"报警{_selectedVariable.Alarms.Count + 1}",          // 新增报警的默认名
_selectedAlarm.Name = … ? "未命名报警" : TxtAlarmName.Text…;  // 名字留空时的兜底
```

`VariableAlarmConfig.Name` 挂在持久化的 `Variable.Alarms` 上（模型注释就写着
"报警名称（如"温度过高""压力下限"）"）—— **翻译它会让工程文件的内容随 UI 语言变化**：
中文界面下新建一条报警存成"报警1"，切到英文再新建一条存成别的，同一个工程里两种语言的名字混在一起；
下次用别的语言打开，名字也不该跟着变。

这与 §9.5 登记的**模板控件名**是同一回事，所以并到同一条待决项 **§4 第 27c 项**
（"落盘标识符要不要跟着 UI 语言翻译"）。本轮**如实把它们留在基线里**
（`VariableManagerView` 27 → **2**），并在基线上写了注释说明为什么是 2 而不是 0 ——
**不假装清干净**。

### 13.3 新守卫：把"键 ↔ 枚举"的配对钉住（外加上一轮 NV 抓出的盲区）

本地化改造把"手写文案"换成"键"时，最容易犯的错是**抄串**：把 `HighLimit` 配上"下限报警"。
写串了**没有任何东西会报** —— 下拉项安静地显示错的类型名，而 Tag 仍然正确，
所以连保存下来的数据都是对的，事后翻数据也看不出来。

所以把配对提成 `internal` 的两个表（`AlarmTypeOptions` / `AlarmSeverityOptions`，
标签在这一层解析），配 `AlarmOptionLocalizationTests`：

| 用例 | 判据 |
|---|---|
| `AlarmTypeOptions_CoverEveryEnumMemberExactlyOnce`（级别同理） | 枚举成员与下拉项**一一对应** —— 枚举新增了成员却忘了加下拉项时，用户根本选不到 |
| `AlarmTypeOptions_PairTheRightLabelWithTheRightEnum`（级别同理） | 语义配对（人写的第二份副本，故意如此：它抓的正是"抄键时抄串了"） |
| `DropdownItems_ResolveLabelsThroughTheDictionary` | **标签确实由字典解析**：`标签 == 字典里的值` 且 `标签 != 键` |
| `EveryOptionLabel_ResolvesInBothLanguages_AndIsDistinct` | 两语言都能解析；同组标签两两不同（写串键或复制粘贴忘改会让两项显示成同一句话） |

> **`DropdownItems_ResolveLabelsThroughTheDictionary` 是负向验证逼出来的。**
> 第一版守卫只断言**配对表**（`AlarmTypeOptions[].LabelKey`），于是把
> `(AppLocalization.Service[o.LabelKey], …)` 改成 `(o.LabelKey, …)`（**把键直接当标签用**，
> 用户会在下拉里看到 `var.alarm.type.high`）**四条用例全绿**。
> 负向验证当场把这条盲区顶了出来，才补上"标签确实走了字典"的断言。
> 这是本项目第 N 次验证同一句话：**守卫绿了 ≠ 守卫有能力红**。

### 13.4 新基线：16 个文件 / 74 条

**99 → 74**（`VariableManagerView` 27 → 2，那 2 条见 §13.2）。
按文件排的下一批候选：`MainWindow`（24）、`AiChatPanelView`（7）、`SimulatorBuildService`（8）。


---

## 14. 第七批：`MainWindow`（2026-09-17）—— 顺带干掉一对**抄了两遍的模板**

24 条里 22 条入字典，2 条与 §13.2 同类（落盘元数据）刻意保留。

### 14.1 一个"同一句话写了两遍"的实例

`MainWindow` 里有个本地兜底：

```csharp
private void ReportOperationFailure(string operation, Exception ex)
{
    AppLog.Error($"{operation}失败", ex);
    ShowGlobalStatus($"{operation}失败: {ex.Message}");
}
```

而上一批刚给 `UiOperationGuard` 本地化的正是**逐字相同**的两条模板：

```csharp
AppLog.Error(Loc.Format(Lang.UiOpFailed, operation), ex);
report(Loc.Format(Lang.UiOpFailedDetail, operation, ex.Message));
```

**同一套措辞、两份实现** —— 本地化一处、漏另一处，几乎是必然（本批就是这么撞见的）。
所以顺手把模板收成唯一实现：

| 之前 | 现在 |
|---|---|
| 两处各写一份 `"{操作}失败"` / `"{操作}失败: {原因}"` | `UiOperationGuard.FailureSummary(operationKey)` / `FailureDetail(operationKey, reason)` —— **两处都调它** |
| `ReportOperationFailure(string operation, …)`（收文案） | `ReportOperationFailure(string operationKey, …)`（收**键**），9 个调用点改传 `Lang.UiOp*` |

顺手还消掉一条重复措辞：`StartWithOpenFileCore` 里的 `$"打开工程失败: {ex.Message}"`
现在直接是 `UiOperationGuard.FailureDetail(Lang.UiOpOpenProject, ex.Message)` —— **措辞逐字不变**，
也不需要新键。

### 14.2 22 条入字典的构成

| 类别 | 例子 | 键 |
|---|---|---|
| 操作名（9 个调用点 / 8 个键，"打开工程"复用） | 打开工程 / 新建工程 / 保存工程 / 导出代码 / 编译工程 / 下载到 HMI / 导出模拟器工程 / Dock 事件路由诊断 | `ui.op.*` |
| 状态提示 | `文件不存在: {0}`、`主题切换至：{0}` | `status.file.not.found`、`status.theme.switched` |
| **撤销描述**（撤销菜单项上直接可见） | 删除控件 / 删除 {0} 个控件 / 旋转 {0:0}° / 水平镜像 / 垂直镜像 / 重置变换 / 修改控件 {0} / 修改页面 {0} | `undo.*` |

撤消描述沿用了字典里既有的 `undo.` 命名（`undo.add`/`undo.delete`/`undo.modify`/`undo.move`）。

### 14.3 留下的 2 条：资源库的**落盘元数据**

```csharp
_resourceLibrary.CustomResources.Add(new HmiResourceItem
{
    SubCategory = "AI 生成",
    Description = "AI 生成的矢量资源",
    …
});
// 紧接着：_resourceLibraryStore?.Save(_resourceLibrary);
```

`HmiResourceItem` 的这两个字段会**存进资源库文件** ⇒ 与 §13.2 的报警名、§9.5 的模板控件名同类：
翻译它们会让"存下来的内容"随 UI 语言变化。一并归到 **§4 第 27c 项**，如实留在基线里
（`MainWindow` 24 → **2**，注释写明为什么是 2）。

> 这类字段其实有个更彻底的解法：**存稳定 token、显示时映射**（`"ai-generated"` → 当前语言的标签）。
> 但那要迁移已有的资源库文件，属独立议题，不在本轮。

### 14.4 契约守卫扩到第二个入口

`EveryCallSite_PassesALocalizationKey` 原本只扫 `UiOperationGuard.Run/RunAsync`；本批把
`ReportOperationFailure` 也纳入（正则加一条分支），于是"下一个调用点照样能传中文字面量"
这个洞在这两个入口上都被堵住。顺带把"方法声明里的形参"（`string operationKey`）排除掉，
并把自检从"≥10 处"提到"≥20 处传了 `Lang.*` 键的调用"。

另加一条 `FailureFormatters_UseTheSharedTemplatesAndResolveTheOperationName`：
钉住这两条共享模板**用的是哪两条键**（有人换模板或漏传原因参数 ⇒ 两处措辞再次分叉 ⇒ 红）。

### 14.5 新基线：16 个文件 / 52 条

**74 → 52**（`MainWindow` 24 → 2）。


---

## 15. 第八批：一批小文件（11 条）+ 补一个**规则漏网**（2026-09-17）

52 条里挑 1–2 条的小文件合并成一批，顺带撞见并修掉一个规则漏网 + 一处死数据。

### 15.1 规则漏网：诊断 sink 的定义不在调用点那一行

`PreviewRuntimeService` 的诊断出口默认写 `Debug.WriteLine`，而 **`MainWindow` 从不注入它** ⇒
应用里它只进 Debug，那条中文消息用户看不到。但排除规则是**按行**看关键词的，而 sink 写在
**属性初始化器**里（`public Action<string>? Log { get; set; } = msg => Debug.WriteLine(msg);`），
调用点那一行只有 `Log?.Invoke(...)` ⇒ 这条被判成"待本地化"（假阳性）。

处置**不是给诊断文案建字典键**（没人会看到它），而是两步：

| # | 动作 | 理由 |
|---|---|---|
| 1 | `Log` → **`DiagnosticLog`**（属性与 1 处注入点） | 让 sink 的**名字自己声明类别** —— 与 `Debug.`/`Console.`/`Trace.` 同类：凭"名字里写明的出口"判定，而不是靠"这一行长什么样" |
| 2 | 排除正则加 `DiagnosticLog` | 判据与实现重新一致 |

> 这与 §9.1 的多行 `throw new` 是**同一类**问题（判据是行局部的，而事实跨行）——
> 只是这次的修法不同：那边是让判据跨行，这边是让**代码自己说清楚**（改名比加规则更稳，
> 因为名字错了编译不会过，而规则漏了没人知道）。

### 15.2 一处死数据：三个主题说明**没有任何界面显示**

`ThemeInfo.Description` 的**唯一**消费者是一条测试（`Assert.False(IsNullOrWhiteSpace(...))`）——
设置对话框里只有三个**只显示名字**的切换按钮，`AvailableThemes` 在应用里只被 `SetThemeByName`
用来读 `Key`/`Name`。也就是说这三句人写的说明**谁都看不到**。

按 §9.3 的教训本该"删掉抄本"，但这里数据本身是有用的（不是重复的抄本），所以选**接上界面** ——
与登记表第 19 项（`ResolveColorDepthItem` 死代码**接上**而不是删掉）同一处置：

| # | 动作 |
|---|---|
| 1 | `ThemeInfo.Description` → **`DescriptionKey`**（静态表里只许放键：静态初始化只跑一次，放解析结果会**冻结语言**，与 `ConfigFieldInventory` 同一个坑） |
| 2 | 新增 `AppThemeService.DescribeTheme(key)` —— **显示时**解析（键 → 当前语言文案） |
| 3 | 设置对话框把三句说明挂成三个主题按钮的**悬停提示**；此前这份数据没人显示 |
| 4 | Core 测试从"断言有一串非空文本"改成**断言键能解析出来**（`DescribeTheme_ResolvesTheKey`：`文案 != 键` 且 `== 字典值`） |

### 15.3 其余 11 条

| 文件 | 内容 | 键 |
|---|---|---|
| `Wiring.Canvas.cs` / `Wiring.DockAggregate.cs` | 撤销描述 `删除控件 {name}` | `undo.delete.control.named` |
| `Controls/DesignCanvas.Input.cs` | 撤销描述 `调整控件大小` / `移动控件` | `undo.resize.control` / `undo.move.control` |
| `Views/CommunicationConfigView.axaml.cs` | 从站显示名 `{name}  · 站 {addr}` | `comm.slave.station` |
| `Design/DefaultTexts.cs` | 画布上"无图"占位字 `图片` | `default.image.placeholder` |
| `Services/AppThemeService.cs` | 三句主题说明 | `theme.desc.*` |
| `Views/AiIconDialog.axaml.cs` | 文件选择器的类型名 `SVG 矢量图` / `PNG 位图` | `ai.icon.filter.svg` / `.png` |

`DefaultTexts.ImagePlaceholder` 由 `const` 改成**属性**：`const` 在编译期就内联，运行时无法解析
（该文件自己的注释早就写着"如需多语言，后续逐个替换为 AppLocalization 资源 key"——本轮兑现它）。
纯符号性的 `"100%"`/`"0%"`/`"?"`/`"Tab1"` 仍留作 `const`：它们不进字典。

### 15.4 留下的 1 条：AI 图片的**资源名**

`Name = $"AI图片_{w}x{h}"` 会随资源条目**落盘**（`HmiResourceItem.Name`），与 §14.3 的
`SubCategory`/`Description`、§13.2 的报警名同类 ⇒ 继续留在基线里（`AiIconDialog` 3 → **1**），
归第 27c 项。

### 15.5 新基线：9 个文件 / 40 条

**52 → 40**，文件数 16 → **9**。剩下的 40 条里 **9 条**是第 27c 项的落盘数据
（`TemplateManager` 4、`MainWindow` 2、`VariableManagerView` 2、`AiIconDialog` 1），
其余 31 条分布在 `SimulatorBuildService`(8)、`AiChatPanelView`(7)、`Wiring.CanvasTools`(6)、
`BuildRunDialog`(6)、`DesignCanvas.Editing`(4)。


---

## 16. 第九批：剩下的 31 条（2026-09-17）—— 清完之后只剩 9 条**落盘数据**

### 16.1 31 条的去向

| 文件 | 条数 | 处置 |
|---|---|---|
| `Services/SimulatorBuildService.cs` | 8 | 7 条构建日志入字典 + **1 条是正则字面量**（见 §16.2） |
| `Views/AiChatPanelView.axaml.cs` | 7 | 2 条新键 + **4 条复用**既有键（`ai.image.save.svg/png`、`ai.icon.filter.svg/png`） |
| `Wiring.CanvasTools.cs` | 6 | 1 条新键（`undo.align.controls`）+ **5 条复用** `cmd.desc.*`（`统一控件尺寸`/`水平等距排列`/`垂直等距排列`/`组合控件`/`拆分组合` **逐字相同**） |
| `Views/BuildRunDialog.axaml.cs` | 6 | 6 条入字典 + 顺手修掉一个**本来就是歪的**日志框（见 §16.3） |
| `Controls/DesignCanvas.Editing.cs` | 4 | 撤销描述 4 条（`undo.paste.*` / `undo.duplicate.*`） |

**复用优先**：这一批 12 条（39%）直接命中既有键 —— `Wiring.CanvasTools` 的 5 条撤销描述与
`EditorCommandCatalog.Describe` 的文案**逐字相同**，造第二份键就又多一处会漂移的抄本。

### 16.2 又一个判据漏网（这次是**反方向**）：正则字面量

```csharp
private static readonly Regex ErrorLineRegex = new(
    @"(?:error:|错误:|fatal error|undefined reference|…|CMake Error)",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);
```

这条用来识别**编译器输出里的错误行**，含中文只是因为它同时要认中文输出 ——
它是**模式**不是文案，**翻译它会直接改变匹配行为**（英文编译器输出就再也认不出来了）。
但守卫按"含中文的字面量"分类，把它算成了待本地化。

新增排除项 **`Regex`**：语句里出现 `Regex` 的字面量是代码（模式/替换串），不是 UI 文案。

> 注意力：这与 §15.1 的 `DiagnosticLog`、§9.1 的多行 `throw new` 是同一族问题 ——
> **判据看的是"这一行长什么样"，而字符串的类别由它在代码里的角色决定**。
> 三次的修法各不相同：让判据跨行 / 让名字自己声明 / 认出承载它的类型。

### 16.3 顺手修掉一个"本来就是歪的"日志框

```csharp
AppendLog("┌────────────────────────────────────────────┐");
AppendLog("│ [已取消] 操作被用户中止                        │");   // ← 内容 47 列，边框 44 列
AppendLog("└────────────────────────────────────────────┘");
```

实测这条**内容行宽 47 列、上下边框只有 44 列** —— 右边框比上边框多出 3 列，**本来就是歪的**
（中文是双列字符，而 padding 是按"看起来差不多"手敲的空格）。而且文案一改就继续歪，没人会发现。

换成按**显示列宽**计算（CJK 2 列、其余 1 列）：

```csharp
internal static int DisplayWidth(string s) => s.Sum(c => c > 0x2E7F ? 2 : 1);
internal static string BoxedContentLine(string text, int inner = 44) => …
private void AppendBoxedLog(string text, int inner = 44) { … }
```

配 `BuildRunDialogBoxTests` 6 条：**框内的行宽 == 边框宽**（中文 / 英文 / 中英混排 / 空串各一）、
`DisplayWidth` 的列宽语义、以及**两语言的实际文案都装得进框**（否则 padding 变负、竖线错位）。

> 负向验证里把列宽换成 `text.Length` 后，中文那条立刻红 —— 证明"按列宽算"不是装饰，
> 而是这条判据能红的原因。

### 16.4 新基线：4 个文件 / 9 条 —— **全部是 §4 第 27c 项的落盘数据**

| 文件 | 条数 | 是什么 |
|---|---|---|
| `Controls/TemplateManager.cs` | 4 | 模板控件的 `Name = "启动"` 等（进生成代码） |
| `MainWindow.axaml.cs` | 2 | `HmiResourceItem.SubCategory`/`Description`（存资源库文件） |
| `Views/VariableManagerView.axaml.cs` | 2 | `VariableAlarmConfig.Name` 的默认值/兜底值（存工程文件） |
| `Views/AiIconDialog.axaml.cs` | 1 | `AI图片_{w}xH}`（存资源条目） |

**40 → 9**，文件数 9 → **4**。**扫描面上已经没有"该翻译却没翻译"的界面文案了** ——
剩下的 9 条全部是"**落盘内容**"，等 §4 第 27c 项的产品决策（以及"存稳定 token、显示时映射"
那套迁移方案）。守卫继续盯着这 9 条：加一条、改一处都会红。


---

## 17. 第十批：落盘默认名（9 条）+ 扫描面清零（2026-09-17）

### 17.1 定性被推翻：这不是"产品决策"，是**没跟上既有约定**

原登记把第 27c 项写成"**产品决策**：翻译会让落盘内容随 UI 语言变化，等决策"。
复核后发现前提不成立 —— App 里**已经有 9 处**把"新建实体的默认名"走字典：
`ScriptNewDefault`/`TrendDefaultName`/`AnimDefaultName`/`comm.conn.new`/`comm.slave.new`/
`FaceplateEditorNewTemplate`/`FaceplateEditorParamDefaultName`/`SimVarName`…
也就是说 **"新建实体的默认名跟随 UI 语言"是本仓库既有约定**，
这 9 条（报警默认名、模板控件名、AI 资源名/分类/描述）只是**没跟上**。

**原理**（两件事必须分开，合在一起才觉得矛盾）：

| | 行为 |
|---|---|
| 界面文案 | 每次渲染都从字典取 ⇒ 切语言**立即**变 |
| 新建实体的默认名 | **创建那一刻**取当前语言文案写进模型 ⇒ 之后不变 |
| 已落盘的内容 | 切语言**不动它**（那是用户数据，只有用户能改） |

于是"中文界面新建的脚本叫「脚本1」，切英文后仍叫「脚本1」"是**正确**行为，不是缺陷。

**动手前查过三件事（都通过）**：① 全仓没有 `Name == "…"` 形式的按名字查找；
② 控件名**不是**生成代码的标识符（C 标识符一律来自 Id：`CIdentifier.ScreenFactory(id)`、
`"script_" + SanitizeIdentifierPublic(script.Id)`、`TAG_{Sanitize(variableId)}`；
`control.Name` 只出现在注释与 `Text` 兜底里）；③ `SubCategory` **不是**分组/比较键
（只有一处 `Text = item.SubCategory` 显示，搜索走 `Tags`，而预设的 Tags 本来就中英并列）。
⇒ 本地化这 9 条不会打断任何查找、也不影响生成物结构。

决策全文见 `docs/2026-09-17-persisted-content-language-decision.md`
（含"刻意不做 token + 显示映射"的理由与将来重开讨论的触发条件）。

### 17.2 顺手又抓出 3 条**被藏起来的**用户可见文案

清完 9 条后基线变成"0"，于是**排除类别成了唯一的过滤器** ⇒ 我把 22 条被排除的字面量
逐条审了一遍。（审计脚本第一版漏了 `;{}` 边界，把两条聊天文案误报成"已被本地化豁免"；
用**保真**重写复核后才发现守卫本身还有个口子。）两处边界修正：

| # | 边界 | 抓出的东西 |
|---|---|---|
| 1 | 语句头也以 `,` 为界（兄弟属性不再连坐） | `Content = "图片生成失败，请重试或修改描述。"`、`Content = $"已生成 SVG 矢量图片 ({w}x{h})…"` |
| 2 | 本地化调用的**非首实参**若是字面量 ⇒ 不豁免 | `Format("ai.gen.failed", "PNG 渲染失败")` 里的状态文案 |

第 2 条只对**本地化调用**生效 —— 一刀切会让多行 `throw new X(nameof(a), a, "中文")` 重新计入
（实测踩到，已用 `LocalizationCall` 收窄）。两条都配了单元测试。

### 17.3 新基线：**扫描面为 0**

| | |
|---|---|
| App 层含中文字面量 | **22 条** |
| 其中**未分类** | **0** |
| 22 条的构成 | `Debug.WriteLine` 15、`throw new …Exception` 5、诊断 sink `DiagnosticLog` 1、正则模式 `Regex` 1 |

守卫也据此重写：**不再是"逐文件基线 + 相等判定"**，而是
① `NoUnclassifiedHardcodedChinese`（扫描面必须为 0，报错时列出文件与条数）；
② 自检改成 `Scanner_StillWorks_WhenExclusionsAreDisabled` —— **关掉排除类别仍能扫到 ≥15 条**。
之所以要改自检：扫描面清零后，自检不能再挂在"条数下限"上（0 的一半还是 0 ⇒ 空跑恒绿），
必须换成**独立于基线**的仪器检验。（原基线字典已删除 —— 清零之后它没有信息量。）
