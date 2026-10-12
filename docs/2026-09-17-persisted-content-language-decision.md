# 落盘内容要不要跟着 UI 语言走？—— 登记表 §4 第 27c 项

> 日期：2026-09-17 ｜ 关联：`docs/2026-09-17-open-items-register.md` §4 第 27c/27 项、
> `docs/2026-09-17-localization-hardcoded-inventory.md` §9.5/§13.2/§14.3/§15.4

---

## 0. 结论：这不是一个待决的产品问题 —— **代码库早就回答过了，只是有 9 条没跟上**

原登记把它写成"**产品决策**：翻译会让生成物/工程文件内容随 UI 语言变化，等决策"。
复核后发现这个前提**不成立** —— App 里**已经有 9 处**在把"新建实体的默认名"走字典：

| 位置 | 代码 | 落盘去向 |
|---|---|---|
| `ScriptEditorView` | `Name = Loc.Format(Lang.ScriptNewDefault, _scripts.Count + 1)` | 工程文件的脚本名 |
| `TrendConfigView` | `Name = Loc.Format(Lang.TrendDefaultName, _trends.Count + 1)` | 工程文件的趋势名 |
| `AnimationEditorView` | `Name = Loc.Format(Lang.AnimDefaultName, _animationCounter)` | 工程文件的动画名 |
| `CommunicationConfigView` | `Name = Loc.Format("comm.conn.new", …)` / `("comm.slave.new", …)` | 工程文件的连接/从站名 |
| `FaceplateTemplateEditorView` | `Name = Loc[Lang.FaceplateEditorNewTemplate]`、`DisplayName = Loc.Format(Lang.FaceplateEditorParamDefaultName, …)` | 工程文件的模板名/参数显示名 |
| `SimulatorView` | `VariableName = Loc.Format(Lang.SimVarName, …)` | 工程文件的仿真变量名 |

也就是说：**"新建实体的默认名跟随 UI 语言"是本仓库既有的约定**，而这 9 条
（报警默认名、模板控件名、AI 资源名/分类/描述）只是**没跟上**。据此处置：
**补齐这 9 条，撤销"产品决策"的定性**。

## 1. 原理：**UI 跟随语言，已落盘的内容不因切语言而改写**

这两件事必须分开，合在一起才会觉得矛盾：

| | 行为 |
|---|---|
| **界面文案** | 每次渲染都从字典取 ⇒ 切语言**立即**变 |
| **新建实体的默认名** | 在**创建那一刻**取当前语言的文案，写进模型 ⇒ 之后**不变** |
| **已经落盘的内容** | 切语言**不动它**（那是用户的数据；只有用户能改） |

于是"中文界面里新建的脚本叫「脚本1」，切到英文后它仍然叫「脚本1」"是**正确**行为，
不是缺陷：名字是**用户数据**，界面才是**可切换的呈现**。这条约定在本仓库已经跑了很久
（上表 9 处），本项只是把落下的补齐。

## 2. 动手前查过的三件安全事项（都通过）

1. **有没有人按名字查实体？** 全仓搜索 `Name == "…"` / `Name.Equals("…")` / `First(… Name == …)`
   在 `src/HmiEditor.App` 与 `src/HmiEditor.Core` 里**一处都没有** ⇒ 本地化名字不会打断任何查找。
2. **控件名会不会变成生成代码里的标识符？** **不会**。`CodeGenerator` 里的 C 标识符一律来自
   **Id**（`CIdentifier.ScreenFactory(id)`、`"script_" + SanitizeIdentifierPublic(script.Id)`、
   `TAG_{Sanitize(variableId)}`）；`control.Name` 只出现在**注释**（`EscapeComment`）与
   `control.Text ?? control.Name` 的**文案兜底**里。所以中文/英文名字都不会污染生成物结构。
3. **`SubCategory` 是不是分组/过滤键？** 不是。全仓只有两处消费：`ResourceLibraryView`
   把它当**分类角标显示**（`Text = item.SubCategory`）、`PresetResourceLibrary` 构造时写值；
   **没有任何比较或分组**（搜索走的是 `Tags`，而预设的 Tags 本来就中英并列，如
   `["alarm", "critical", "emergency", "报警"]`）。所以本地化不会造成"分类裂成两组"。

## 3. 刻意**不做**的事：token + 显示映射

更彻底的做法是"存稳定 token、显示时映射"（`"ai-generated"` → 当前语言的标签）。
本轮**不做**，理由是查过之后它解决不了现有问题、却要动最多数据：

1. **没有按值查的地方**（见 §2.1、§2.3）⇒ token 化的收益只是"分类值看起来统一"，不是功能性的；
2. 真正的数据源是 `PresetResourceLibrary` 里**写死的中文**（`"报警-紧急"`、`SubCategory="报警"`…）
   以及**用户已落盘的资源库文件** ⇒ token 化必须**同时迁移预设与用户文件**，属独立的 schema 变更；
3. 这类改名一旦漏迁移，症状是"资源分类突然对不上"（静默），风险高于收益。

登记项里保留这条作为**将来可选项**，并写明触发条件（见 §4）。

## 4. 什么时候需要回来重新考虑

- 出现**按名字/分类查找**的代码（那时名字就成了键，不能再随语言变）；
- 资源库要做**跨语言共享**（多语言团队共用一个库文件时，中文分类值会显现为噪音）；
- 用户反馈"同一个库里中英名字混在一起看不懂" —— 那时再上 token + 映射，并补迁移脚本。

## 5. 本项的落地（同日完成）

| 位置 | 键 |
|---|---|
| `VariableManagerView`（报警默认名 / 兜底名） | `var.alarm.default.name` / `var.alarm.unnamed` |
| `MainWindow`（AI 资源分类 / 描述） | `resource.subcategory.ai` / `resource.desc.ai` |
| `AiIconDialog`（AI 图片资源名） | `ai.image.asset.name` |
| `TemplateManager`（模板控件名 ×4） | `tpl.startstop.name.start` / `.stop` / `tpl.motorind.name.led` / `.state` |

## 6. 顺带：守卫又抓出 3 条**被藏起来的用户可见文案**

落这 9 条时顺手审了一遍"所有被排除类别的字面量"（因为基线清零后，**排除类别成了唯一的过滤器**，
每一条都必须站得住）。审计脚本第一版**漏了 `;{}` 边界**，把两条聊天文案误报成"已被本地化豁免"；
用**保真**的重写逐条复核后，确认守卫的连坐豁免还有一个口子：

```csharp
_messages.Add(new ChatEntry
{
    RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
    Content = "图片生成失败，请重试或修改描述。"     // ← 继承了兄弟属性的豁免
});
```

对象初始化器里**兄弟属性**用了本地化 API，就把同级的 `Content` 一起豁免了（§9.1b 的同类，
只是这次藏在初始化器里）。于是修判据的两处边界：

| # | 边界 | 效果 |
|---|---|---|
| 1 | 语句头也以 `,` 为界 | 兄弟属性的豁免不再外溢 ⇒ 抓出 2 条聊天文案 |
| 2 | 本地化调用的**非首实参**若是字面量，**不**豁免 | 抓出 `Format("ai.gen.failed", "PNG 渲染失败")` 里的那句状态文案 |

两处都配了单元测试（`IsExcluded_DoesNotInheritASiblingPropertysExemption`、
`IsExcluded_CountsLocalizedCallArgumentsAfterTheFirst`）。第 2 条只对**本地化调用**生效 ——
一刀切会让多行 `throw new X(nameof(a), a, "中文")` 重新计入（实测踩到，已用 `LocalizationCall` 收窄）。

最终：App 层含中文字面量 **22 条，全部落在 4 个排除类别里，未分类 0**（详见
`docs/2026-09-17-localization-hardcoded-inventory.md` §17）。
