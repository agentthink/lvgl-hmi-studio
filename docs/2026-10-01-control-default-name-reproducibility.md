# 控件默认名的可复现性 —— 工程里存中性名、界面按语言显示别名（阶段 159，闭环登记 #61r）

> **一句话**：画布新建控件的**持久化名字**不再随界面语言变（`Button1` / `TextArea2` …，与
> `HmiControlType` 枚举同名）；界面上照旧显示本地化别名（`按钮1`），而「显示什么就写回什么」
> 是一个**不动点**。

---

## 1. 缺陷（改动前）

`DesignCanvas.BuildControl` 把**显示名**当成了**标识符**：

```csharp
var localizedName = Services.ControlTypeTextKeys.Describe(type);   // 「按钮」/「Button」
var uniqueName = $"{localizedName}{index}";                        // ⇒ 按钮1 / Button1
```

而这个名字会

1. **写进工程文件**（`HmiControl.Name`），
2. **进交叉引用 / 绑定 / 生成代码**（控件名 → 符号那条链，见
   `docs/2026-09-15-control-object-symbol-contract.md`）。

⇒ 同一份工程**在中文界面下编辑一次、英文界面下编辑一次，会得到两份不同的工程**（乃至两套符号）。
这与「同一工程两次导出应逐字节一致」是同一条纪律，只不过对象是**工程文件自身**。

⚠ 与它相邻但**不同**的一件事：控件的 `Text`（默认显示文案，如按钮上那个「按钮」）**仍然按语言** ——
那是给操作员看的、用户可以改的**显示文案**，不是标识符。本阶段把这两者明确分开。

---

## 2. 口径（用户拍板）

| 名字 | 谁产生 | 存进工程？ | 界面显示 |
|---|---|---|---|
| `Button1`（**中性名**） | `ControlNameDefaults.UniqueName` | 是 | 翻成本地化别名 |
| `报警灯`（用户改的名） | 用户 | 是 | **原样** |
| `按钮1`（旧工程里按语言建的名） | 旧版本 | 是（读入时保留） | 原样；**写回时归一到 `Button1`**（迁移） |

**为什么不加 `IsAutoNamed` 标志位**：要动 schema、要迁移、要兼容旧包；而"默认名形状"
（枚举名前缀 ＋ 纯数字）本身可判。代价是手工把控件命名成 `Button1` 时也会被当成默认名。
这个代价是**已知边界**，写在 `ControlNameDefaults` 的类注释里。

---

## 3. 单一来源：`ControlNameDefaults`

| 成员 | 作用 |
|---|---|
| `NeutralPrefix(type)` | 中性前缀 = 枚举名（与语言无关） |
| `UniqueName(type, existing)` | 生成不与现有名字冲突的中性名（序号从 1 起） |
| `Display(control)` / `Display(name, type)` | 界面显示名：默认名形状 → 本地化别名；其余原样 |
| `ToPersisted(shown, type)` | **写回**：显示名 → 持久化名（`Display` 的左逆）；旧工程由此迁移 |

于是三条不变式：

1. **跨语言**：`UniqueName` 与语言无关（判据 J1）；
2. **左逆**：`ToPersisted(Display(n, t), t) == n`（中性名 n，26 个类型逐个走，判据 J2）；
3. **显示不动**：旧名 `按钮1` 迁移成 `Button1` 前后，界面显示**一字不变**（判据 J4）。

---

## 4. 显示面清单（改动点）

| 文件 | 处 | 显示的什么 |
|---|---|---|
| `Views/PropertyPanelView.axaml.cs` | 2 | 面板标题行 ＋ 名称输入框 |
| `Views/PropertyPanelView.Changes.cs` | 1 | **写回**（`ToPersisted`） |
| `Views/ReferenceRow.cs` | 1 | 交叉引用面板的「控件名」列 |
| `Views/ProjectExplorerView.axaml.cs` | 3 | 工程树节点 ＋ 改名输入框预填 ＋ 改名完成提示 |
| `Controls/UndoRedoManager.cs` | 4 | 撤销/重做描述（移动/新增/删除/修改） |
| `Wiring.DockAggregate.cs` | 2 | 删除快照描述 ＋ 预览态状态栏 |
| `MainWindow.axaml.cs` | 1 | 画布属性变更的撤销描述 |
| `Views/AnimationEditorView.axaml.cs` | 1 | 动画「目标控件」下拉（选中靠 `Id`，显示才是名字） |
| `Controls/DesignCanvas.cs` | 1 | 控件未设 `Text` 时的回落标签 |
| `Controls/DesignCanvas.Renderers.cs` | 4 | 同上（多处渲染支） |
| `Controls/DesignCanvas.IndustrialRenderers.cs` | 3 | 过程图形位号文字 |

⚠ **不改**的地方（如实记为边界）：`VariablePicker.RecommendationContextName`（AI 上下文，
是**数据**不是给人看的标签）、`MainWindow.axaml.cs:462` 的按名查控件（标识符语义）、
`PropertyPanelView.Binding.cs` 里把名字交给绑定引擎的那些点。

---

## 5. 判据（8 条，`tests/HmiEditor.App.UiTests/ControlDefaultNameTests.cs`）

| # | 判据 | 判的是哪一跳 |
|---|---|---|
| J1 | `DefaultNames_AreLanguageIndependent_AndNeutral` | 走**生产路径**（`DesignCanvas.AddControlAt`）在 zh / en 下各建 3 个控件 ⇒ 名字**逐字相同**且是中性的（本项的核心） |
| J2 | `UniqueName_SkipsTakenIndices_AndIgnoresLanguage` | 序号跳空位（`Button1`/`Button3` 已占 ⇒ `Button2`）、与语言无关 |
| J3 | `Display_IsLocalizedAlias_AndToPersistedIsItsLeftInverse` | 别名随语言变（`按钮1` ↔ `Button1`）；**左逆**在 26 个类型 × 2 种语言下逐个成立 |
| J4 | `LegacyLocalizedNames_KeepTheirDisplay_ButMigrateOnWrite` | 旧工程：显示不变、写回归一 |
| J5 | `UserNamedAndShapeMismatched_AreNeverAliased` | 用户改的名、前缀不属于该类型、非纯数字、序号 0 ⇒ **都不翻译** |
| J6 | `CrossReferenceRow_ShowsAlias_ForDefaultNames` | 交叉引用行跟着语言走 |
| J7 | `PropertyPanel_ShowsLocalizedAlias_ForDefaultNames` | 面板显示别名；两轮显示后**工程里的名字一字未动** |
| J8 | `DisplaySurface_RoutesThroughControlNameDefaults` ＋ `PropertyPanel_NameWrite_GoesThroughToPersisted` | 显示面/写回点的**源码契约** |

⚠ J8 的两条是**清单式**守卫：新加一个「显示控件名」的地方它**不会**自动发现（源码上看不出
某个 `.Name` 是给人看的还是当标识符用的），它的作用是**已列的地方被改回去时当场红**。

---

## 6. 负控 5 支（`.workbuddy/tmp/s159-nc.py`，还原 SHA256 逐字节相同）

| 控制 | 注入 | 实测 |
|---|---|---|
| nc1 | `BuildControl` 的默认名改回**本地化**（= 改动前的形状） | J1 红 |
| nc2 | `Display` 直接返回原样 | J3 / J7 红 |
| nc3 | `ToPersisted` 变成恒等 | J3 红 |
| nc4 | `TrySplit` 允许序号 0 | J5 红 |
| nc5 | 面板 Name 写回改回直写（不过 `ToPersisted`） | J8 红 |

---

## 7. 诚实边界（都在代码注释里点名）

1. **手工**把控件命名成 `Button1` ⇒ 会被当成默认名（显示成 `按钮1`）。要根治得加
   `IsAutoNamed` 标志位 —— 代价见 §2。
2. 旧工程里的 `按钮1` 会**在写回时**迁移成 `Button1`（显示不变、用户看不出来）；
   如果用户刻意想要一个叫「按钮1」的名字，那它会被迁移 —— 已知代价。
3. ~~**属性面板的文本框写回路径在 headless 下驱动不动**：实测同一装配下把 `PropText.Text`
   改成「诊断文案」，`control.Text` **也没变**（不是本项特有的问题；仓库自己的
   `PropertyPanelWiringTests` 也刻意只驱动数字框 / 下拉 / 复选框）⇒ 写回那一半用**源码契约**
   判（J8）＋ 单元级左逆（J3），而不是假装驱动了它。~~
   🩸 **阶段 160 更正：这条结论是"量的方法不够"造成的错觉。** 真相是 **Avalonia 12 里程序化赋值
   `TextBox.Text` 不触发 `TextChanged`**（本仓 `PropertyPanelUiTests.TypeInto` 的注释早就写着这一点），
   而**真实键盘输入**（`box.Focus()` ＋ `SelectAll()` ＋ `window.KeyTextInput(text)`）完全驱动得动。
   阶段 160 已补行为判据 `PropertyPanel_TypedAlias_WritesBackNeutralName`：敲 `按钮2` ⇒ 落盘
   `Button2`、敲 `报警灯` ⇒ 原样、敲 `Button9` ⇒ 原样。源码契约那两条**留着当守卫**，不再当"唯一证据"。
4. 控件默认**显示文案** `Text` 仍按语言 —— 那是有意为之（给操作员看的、可改），与标识符分开。
5. 清单式显示面守卫不会自动覆盖新显示点（见 J8 的说明）。
