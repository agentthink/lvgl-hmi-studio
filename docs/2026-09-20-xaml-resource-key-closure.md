# XAML 资源键闭合：一个从未被定义过的画刷键 + 4 条判据（阶段 47）— 2026-09-20

> 承接阶段 46 专题 `docs/2026-09-20-mainwindow-construction-net.md` §5 挂起的那句
> 「把 `{StaticResource …}` 的键名写错，**既不编译报错也不在解析期抛**，原因未查证」。
> 本轮把它查清并按结论立判据。提交 `f0cda82`；登记表 **#38**。

## 0. 一句话

`HmiWarningDeepBrush` 这个键在**本仓从来没被定义过**（`git log --all -S` 全历史只有 3 处引用、
**零处定义**），三处按钮的背景因此一直回落到 Fluent 暗色主题的默认底 `#0FFFFFFF` ——
编译不报、运行不抛、全仓 3000 条判据没有一条会红。本轮补齐该键（2 行），并立 **4 条**「资源键闭合」判据，
负控 **8/8**。

## 1. 先定性：排掉两种"工具口径"，再谈机制

阶段 46 的负控 N3 在 `MainWindow.axaml` 上把 `{StaticResource HmiBgPanelBrush}` 写成
`…BrushTYPO`，结果**三条判据全绿**。当时只写到"原因未查证"。本轮按"先排工具、再排机制"的顺序：

| 假设 | 怎么排 | 结果 |
|---|---|---|
| ① 负控脚本自己没看 warning | 直接跑 `dotnet build -c Release`，数 `warning`/`error`/含 `StaticResource` 的行 | 退出码 **0**、warning **0**、error **0**、命中行 **0** ⇒ 不是漏 warning |
| ② 增量构建没真重建（改的没生效） | 比对 `src/HmiEditor.App/bin/Release/net10.0/HmiEditor.App.dll` 的 mtime | `20:16:15 → 20:39:33` **确实重建** ⇒ 不是跳过 |
| ③ 确实是静默取空 | 见 §6（用真实控件属性读回实测值） | **成立**，且比"静默"更细：有两种形态 |

注入串唯一性由脚本先断言（把锚点从出现 3 次的单行改成出现 1 次的两行），排除"改错地方"。

## 2. 再查历史：这不是"曾经有后来删了"

```
git log --all --oneline -S'HmiWarningDeep'   →  5d7b05f / 6f1cf67
```

两个提交里这个串**只以引用的形式出现**（`+ Background="{StaticResource HmiWarningDeepBrush}"`），
没有任何一处 `x:Key=`。全仓（排除 `firmware/`、`build/`）grep 同样只有那 3 处引用。
⇒ **幽灵键是从一开始就没定义过**，不是迁移时丢的。

## 3. 面：47 个 `.axaml` / 121 个 `.cs`，差集只有 2 个

按"引用侧 `{StaticResource|DynamicResource}` + `.cs` 的 `Resources["…"]`"与"定义侧 `x:Key`"两边盘点：

| 口径 | 数 |
|---|---|
| 引用点 | **2792**（xaml 2788 / cs 4） |
| 引用去重键 | **136** |
| 定义键 `x:Key` | **185**（集中在 `App.axaml` + `Assets/Icons.axaml`） |
| `ResourceInclude` | 2（Dock 预设 `Default.axaml` 来自 NuGet 包；`Assets/Icons.axaml` 在仓库内） |

差集 2 个，**一个是假缺口、一个是真缺口**：

| 键 | 引用处 | 判定 |
|---|---|---|
| `IconXxx` | `App.axaml:20` | **误报** —— 它在 **XML 注释**里（`<!-- … （使用 {StaticResource IconXxx} 引用…） -->`）。不剥注释的盘点会把它报成"引用了从未定义" |
| `HmiWarningDeepBrush` | `CommunicationConfigView.axaml:71`、`:257`、`VariableManagerView.axaml:352` | **真缺口** —— 全工程无 `x:Key`，且调色板里没有这个家族的成员 |

调色板查证（`App.axaml`）：

```
HmiWarning  #f59e0b (Color @39)  →  HmiWarningBrush    (SolidColorBrush @86)
HmiDanger   #ef4444 (Color @40)  →  HmiDangerBrush
HmiDangerDeep #4a1c1c (Color @56) →  HmiDangerDeepBrush (@100)
HmiWarningDeep / HmiWarningDeepBrush  →  **不存在**
```

`App.axaml:53` 的注释把这一族的用途写死了：

```
<!-- 按钮状态色（深底）: 用于成功/中性/危险动作按钮，确保与文字色高对比 -->
<Color x:Key="HmiSuccessDeep">#065f46</Color>   <!-- 成功动作按钮背景 -->
<Color x:Key="HmiNeutralDeep">#4b5563</Color>   <!-- 中性/取消按钮背景 -->
<Color x:Key="HmiDangerDeep">#4a1c1c</Color>    <!-- 危险动作按钮背景 -->
```

而三处引用是 `Background="{StaticResource HmiWarningDeepBrush}" Foreground="{StaticResource HmiTextPrimaryBrush}"`
—— **正是这一族的用法**。所以缺陷是"家族少了一个成员"，而不是"引用写错了名字"。

## 4. 修法：补家族成员（2 行），不是改指向

| 候选 | 内容 | 代价 | 判断 |
|---|---|---|---|
| **(a) 补成员** ✅ | `HmiWarningDeep = #92400e`（amber-800）+ `HmiWarningDeepBrush` | 纯增量 2 行，**三个调用点零改动** | 采纳 |
| (b) 改指 `HmiWarningBrush` | 3 处改成既有的亮橙 `#f59e0b` | 动 3 个调用点的外观 | 否决 |

否决 (b) 的算式：`#f59e0b` 对 `#e2e8f0` 的相对亮度是 0.439 vs 0.802 ⇒ 对比度 **≈1.7:1**
（浅色字压在亮橙底上，基本读不清）；而 Deep 家族的取值逻辑是"成功取 emerald-800、中性取 gray-600"这一档。
`#92400e` 对 `#e2e8f0` 是 **≈5.8:1**（过 WCAG AA 的 4.5:1），也与 `HmiSuccessDeep`（emerald-800）同一步长。

> **这是本轮唯一一个"设计决定"**，其余都是取证或机械收口。选 amber-800 的依据是"同族同一步长 + 对比度过线"，
> 不是审美。若哪天设计给出别的值，改这一处即可（另外 3 个引用点是按名字引用的）。

## 5. 判据（4 条，`tests/HmiEditor.App.UiTests/XamlResourceKeyClosureTests.cs`）

| # | 判据 | 类型 | 守什么 |
|---|---|---|---|
| 1 | `Scanner_SeesBothSides` | 面自检 | `axaml ≥45` / `cs ≥110` / 引用键 `≥120` / 定义键 `≥170`，且**代码侧那条腿非空**（`cs` 引用 ≥3）—— 面变窄时下面两条会变空跑 |
| 2 | `RuntimeLookup_IsUsableAsEvidence` | 证据腿的自检 | 已知键必须解析到、**幽灵键必须解析不到**（若后者恒真，第 3 条就成了假门） |
| 3 | `EveryReferencedResourceKey_IsDefinedOrResolvable` | 主判据 | 每个引用键必须满足"**静态已定义** ∪ **运行期可解析**"，否则点名 `键 ← 文件:行(面)` |
| 4 | `PhantomKeyVictimButtons_ActuallyCarryTheirBackground` | 行为 | 3 个受害控件的 `Background` **真的是**那个画刷（走真实控件属性，失败消息带实测值） |

**两条腿的分工**（缺一条都会有假红/假绿）：

- **静态** `x:Key`（`ScanAppXaml` 递归 `src/HmiEditor.App`，含 `Assets/Icons.axaml`）—— 接住视图自己的
  `<UserControl.Resources>`（这类键在 App 作用域**运行期查不到**，只有静态这条腿能兜）；
- **运行期** `Application.Current.Resources.TryGetResource` —— 与产品自己的取值口径
  `App/Theme.cs` 的 `Theme.Brush` **同源**，接住 Dock 预设（NuGet 包，静态面外）。

第 4 条与第 3 条不是影子关系：第 3 条只证明"键存在"，不证明"这个引用点确实取到了值"
（引用可能落在未实例化的模板里、或被后面的 setter 覆盖）。负控 N6 专门隔离这一点（见 §7）。

**剥注释是前提**：引用侧先过 `SourceScanner.StripXmlComments`（本轮抽出的唯一实现，
**保长度 + 保行数**）。不剥的话 `App.axaml:20` 那个示例键会把判据染成假红。

## 6. 运行期到底会怎样 —— 两种形态（都实测，别再笼统说"静默"）

把 `HmiWarningDeep` / `HmiWarningDeepBrush` 分别破坏，读回真实控件属性与异常：

| 缺失落在哪 | 表现 | 证据 |
|---|---|---|
| **控件的样式属性**（`Background="{StaticResource X}"`） | **静默跳过赋值** ⇒ 属性回落到主题默认值。实测三个按钮的 `Background` 全是 `#0FFFFFFF`（Fluent 暗色主题的默认按钮底），**不抛、不报，只是颜色错了** | N8（把画刷定义键改名，精确复现修复前状态）：`实测 BtnAddSlave=#0fffffff、BtnBatchAddSlaveVars=#0fffffff、BtnValidateIntegrity=#0fffffff` |
| **资源字典里对象的属性**（`App.axaml` 中 `HmiWarningDeepBrush` 的 `Color="{StaticResource HmiWarningDeep}"`） | 解析被**推迟**（`DeferredResourceReference`），字典里"看着有值"；谁去**读那个属性**谁才吃到异常：`KeyNotFoundException : Static resource 'HmiWarningDeep' not found.` | N2 |

两种形态都**在编译期完全不可见**。第一种最危险（运行期也没有任何信号）；第二种只是把异常挪到了读取点
—— 而读取点可能在千里之外的 UI 代码里（本例里是 `Theme.Brush("HmiWarningDeepBrush")` 的调用方）。
这也解释了阶段 46 的 N3：`MainWindow.axaml` 的 `CommandBar.Background` 写错之后三条判据全绿 —— 属**形态 1**。

## 7. 负控（8/8，逐条断言"只红这些"）

脚本 `.workbuddy/tmp/negative-control-reskeys.py`：每个注入先断言锚点在该文件**恰好命中 1 次**，
跑完立刻还原并逐字节 `sha256` 核对（结束后再整体核对一次）。

| 注入 | 期望红 | 实测红 | 点名 |
|---|---|---|---|
| N1 `AlarmListView.axaml` 的 `Background` 换成幽灵键 | 主判据 | 主判据 | `HmiGhostBrushZZZ ← Views/AlarmListView.axaml:94(xaml)` |
| N2 `App.axaml` 把 `x:Key="HmiWarningDeep"` 改名 | 主判据 + 行为 | 主判据 + 行为 | `HmiWarningDeep ← App.axaml:102(xaml)`；行为侧 `KeyNotFoundException: Static resource 'HmiWarningDeep' not found.` |
| N3 **单行** XML 注释里放 `{StaticResource HmiGhostCommentZZZ}` | **∅（必须仍绿）** | ∅ | —— |
| N4 **跨行** XML 注释里放 `{StaticResource HmiGhostMultilineZZZ}` | **∅（必须仍绿）** | ∅ | 按行剥注释的实现会在这里假红 |
| N5 `ProjectSettingsView.axaml.cs` 的 `Resources["HmiWarningBrush"]` 换成幽灵键 | 主判据 | 主判据 | `HmiGhostCsZZZ ← Views/ProjectSettingsView.axaml.cs:298(cs)`（证明代码那条腿在判） |
| N6 把 `BtnAddSlave` 的背景换成**存在但语义错**的 `HmiBgPanelBrush` | **只**行为判据 | 只行为判据 | `BtnAddSlave: Background=#ff1a2332 ≠ #ff92400e`（隔离第 4 条） |
| N7 往 `App.axaml` 塞一个与幽灵探针**同名**的键 | **只**第 2 条 | 只第 2 条 | `不存在的键却解析成功 ⇒ '解析不到'这条证据恒真`（证明它不是空断言） |
| N8 复现修复前状态（画刷定义键改名） | 主判据 + 行为 | 主判据 + 行为 | 见 §6 的 `#0FFFFFFF` 读数 |

N3/N4 是**"必须仍绿"**的两条：判据不能对注释里的示例假红（否则下一个人会去删注释）。
N6/N7 是**隔离注入**：分别证明第 4 条不是第 3 条的影子、第 2 条不是空断言。
还原后 4 个被动过的文件 `sha256` 逐字节一致、复跑全绿。

## 8. 读数

| 项 | 值 |
|---|---|
| 扫描面 | **47** 个 `.axaml` / **121** 个 `.cs`（`src/HmiEditor.App`，排除 `bin`/`obj`） |
| 引用 / 定义 | 引用点 **2792**（xaml 2788 / cs 4）、去重键 **136**、定义键 **185** |
| 产品修复 | `App.axaml` +2 行（`HmiWarningDeep` / `HmiWarningDeepBrush`） |
| `App.UiTests` | 270 → **274**（+4） |
| 构建 | **0 警告 / 0 错误** |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**；八程序集 44+44+75+87+4+315+2116+274 = **2959**（+8 单独 git 段 = **2967**），比阶段 46 的 2963 **净增 4** |
| 时序 | 提交 `f0cda82` **20:56:07**；门禁日志 `.workbuddy/tmp/ci-gate-2026-09-20-reskeys.log` mtime **20:57:08** ⇒ 晚于提交 **61 秒** |

## 9. 仍未闭环

① **只查"键能不能解析"，不查"解析出来的类型对不对"** —— `{StaticResource X}` 取到一个 `Color`
而目标属性要 `IBrush` 之类的不匹配，在 Avalonia 里同样是静默的；判据要按属性类型逐点推，成本与收益本轮不划算。

> **↑ 上面这一整段已于 2026-09-22（阶段 69）被实测推翻，留档、勿再引用。** 复核结论：
>
> ① 举的那个例子**不成立** —— `StaticResourceExtension.ProvideValue` 把**目标属性的 `PropertyType`**
> 一路传给 `ColorToBrushConverter.Convert(value, targetType)`，而那个转换器的全部内容就是
> 「`targetType == typeof(IBrush)` 且值是 `Color` ⇒ 包成 `ImmutableSolidColorBrush`」；
> 注入 `Background="{StaticResource HmiAccent}"` 实测**全绿**。`Color → IBrush` 是被**专门支持**的用法。
>
> ② 真正的三类失效**各有拦截点，没有一类落在「本判据该管却没管」的缝里**（都实测）：
>
> | XAML 失效形态 | 真实拦截点 | 实测证据 |
> |---|---|---|
> | **缺失键**（幽灵键） | 唯一真静默的一类；由本文件那一族的**静态面**守 | 注入幽灵键 ⇒ 键闭合判据红、另两族绿 |
> | **类型不匹配**（`StreamGeometry` 塞给 `IBrush`） | **构造期** `InvalidCastException`，**带行号** | `AlarmListView.axaml:line 88`；`WindowProductionPathUiTests` 红 |
> | **绑定路径 typo** | **编译期** `error AVLN2000` | 注入即 build-fail（因编译绑定默认开着） |
>
> ⇒ 把它记成「遗留」是**定性错了**，不是「没做完」。复核过程顺带量出的**真问题** ——
> 编译绑定开关 `AvaloniaUseCompiledBindingsByDefault` 全仓 **零显式声明**、而包内两个文件
> 给了**相反**的默认值（`build/Avalonia.props:8`=true / `build/AvaloniaBuildTasks.targets:5`=false，
> 靠 `.props` 先导入才今天是 true）—— 已作为**新项 #56** 登记并**当轮闭环**，见
> `docs/project-status.md` 阶段 69。

② **26 个 `ChromeWindow`（`: Window`）视图不在 `ViewSmokeUiTests` 的清单口径内** —— 它的口径是
`typeof(UserControl).IsAssignableFrom(t)`，收 15 个 UserControl 视图；`CommunicationConfigView` /
`VariableManagerView` / `AlarmListView` / `TrendConfigView` 等 26 个窗口视图**一个都没被构造过**
（3 个受害控件恰好全在其中）。顺带更正那一层的注释：它接得住"缺模板/缺 `ControlTheme` ⇒ **布局期抛异常**"，
接不住"缺一个 `Brush` ⇒ **静默**"，"XAML 里的 `{StaticResource ...}` 名字写错"这句是高估了自己。
要不要立"窗口视图冒烟"另开一项（本轮不动，避免把两件事混进同一个提交）。

③ **"定义但从未被引用" 50 个键**（`ContextMenuBackgroundBrush`、`Dark`、`HmiCat*Brush`、`IconDraw*` …）
本轮只做反向对照，**未见得是死键**（可能存在动态引用路径），未处理。

④ 运行期那条腿的前提是"App 资源已加载" —— 只在 headless 宿主里验证过（第 2 条判据就是这个前提的自检）。
