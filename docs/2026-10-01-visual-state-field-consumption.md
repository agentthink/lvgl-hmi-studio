# 视觉状态字段的**消费账**（阶段 202）—— `ControlVisualState` 的 7 个字段，谁消费了？

> 承阶段 201 的 `#91`（`Normal` 态三色**改了什么都不写**）。那一条问的是"**写侧**通不通"；
> 这一条接着问"**读了之后谁消费**" —— 面板给用户在状态里设的每一格，**预览**与**导出的 LVGL 代码**
> 各自认不认。

---

## 1. 为什么要有这一面

这块在阶段 202 之前 **一条判据都没有**：全仓 `grep LV_STATE_PRESSED tests/` 是**空的** ——
面板改了、预览变了，导出丢没丢**没人看着**。而"面板给了编辑口、导出却丢掉"正是本仓最恨的
"半生效"形状（`#91` 是它的镜像：写侧丢；这一面是**消费侧**丢）。

`ControlVisualState` 有 7 个可编辑字段（面板「视觉状态」那一片）：`BgColor` · `TextColor` ·
`BorderColor` · `ImageAssetId` · `Text` · `TextAlign` · `TextVAlign`。

## 2. 实测的账（7 个字段 × 两个消费方）

| 字段 | 预览（`ControlTextStyleResolver` / `DesignCanvas`） | 导出（`GenerateVisualStates`） | 结论 |
|---|---|---|---|
| `BgColor` | ✅ 状态优先 | ✅ `lv_obj_set_style_bg_color(..., LV_STATE_*)` | 真生效 |
| `TextColor` | ✅ | ✅ `lv_obj_set_style_text_color(...)` | 真生效 |
| `BorderColor` | ✅ | ✅ `lv_obj_set_style_border_color(...)` | 真生效 |
| `TextAlign` | ✅ | ✅ `lv_obj_set_style_text_align(...)`（`text_align` 是**样式**属性，能带选择器） | 真生效 |
| `ImageAssetId` | ✅ 状态条目优先 | ✅ `lv_image_set_src(...)`；**不在资源库中时用注释声明跳过** | 真生效（带声明） |
| `Text` | ✅ 状态条目优先 | ⚠ **只能声明**：`// State text: "…" (switch via event callback at runtime)` | **结构上表达不了**（LVGL 静态样式不含文本切换） |
| **`TextVAlign`** | ✅ 状态条目优先 | 🩸 **阶段 202 前连注释都没有** ⇒ 已补：`// State vertical align: Bottom (LVGL 无状态化 vertical-align，需运行时重定位回调)` | **结构上表达不了** |

### 2.1 `TextVAlign` 为什么结构上表达不了

LVGL **没有** vertical-align 样式属性。本仓在 **Normal** 态是这么绕过去的（`CodeGenerator.Screen.cs`）：

```c
lv_obj_set_height(o, LV_SIZE_CONTENT);
lv_obj_update_layout(o);
int32_t _h_xxx = lv_obj_get_height(o);
... （按测得高度重定位）
```

—— 那是**变换**（`lv_obj_align` 一族），**不是样式** ⇒ **带不了 `LV_STATE_*` 选择器**。
所以非 `Normal` 态的垂直对齐**只能**声明意图、由运行时回调实现。

## 3. 判据（8 条，`VisualStateCodeGenConsumptionTests`）

- `StyleCarriableFields_AreEmittedWithTheStateSelector`：四色/对齐真的带 `LV_STATE_PRESSED` 发射，
  且**状态里的色值**（`aa0001`/`aa0002`/`aa0003`）进了输出（不是控件默认色）。
- `StateImage_NotInAssetLibrary_IsSkippedWithADeclaration`：不在资源库 ⇒ **声明**跳过，不静默丢。
- `Text_IsDeclaredAsRuntimeOnly` / `VerticalAlign_IsDeclaredAsNotExpressibleAsAStyle`：
  两个"表达不了"的字段各留**声明注释**；后者还带**反面见证**（它**不是**以
  `lv_obj_set_style_*(..., LV_STATE_*)` 形式发射的）。
- `StateWithOnlyTextAlign_UsesTheModeMapping`：只设水平对齐 ⇒ `LV_TEXT_ALIGN_CENTER` + 状态选择器。
- `EmptyStateEntry_EmitsNothingButTheComment`：全空的状态条目**不发射**任何带 `LV_STATE_*` 的样式行
  （只剩声明注释）。
- `Preview_ResolvesStateFields_ThatCodeGenCannotExpress`：**预览侧**那三格（文本色/水平/垂直对齐）
  状态优先、`Normal` 态取控件值 —— 与"导出表达不了"形成**对照**，把"预览有效、导出只留声明"钉死。
- `NormalStateEntry_IsSkippedByCodeGen`：`Normal` 条目由"控件默认字段"那条路处理，
  生成器**跳过**它（与面板侧口径一致：`Normal` = 控件字段）。

## 4. 产品侧修的那一处

`GenerateVisualStates` 补 `TextVAlign` 的声明注释（3 行 + 说明）：

```csharp
if (st.TextVAlign.HasValue)
{
    Cmt(sb, $"    // State vertical align: {st.TextVAlign.Value} "
        + "(LVGL 无状态化 vertical-align，需运行时重定位回调)");
}
```

**这不等于"实现"** —— 它是把"静默丢失"变成"**声明的不支持**"（登记 `#92` 的剩余部分见 §6）。

## 5. 负控（6 支，`.workbuddy/tmp/s202-nc.py`）

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | 去掉 `TextVAlign` 的声明注释（回退产品修复） | 导出侧判据 | `Not found: "// State vertical align: Bottom"` |
| nc2 | `text_align` 发射丢掉状态选择器（`0`） | 只设对齐那条腿 | `Not found: "LV_STATE_PRESSED"` |
| nc3 | 去掉 `State text` 的声明注释 | 导出侧判据 | `Not found: "// State text: \"按下时的文本\""` |
| nc4 | 图片"不在库中"改成静默跳过 | 导出侧判据 | `Not found: "跳过：图片资源 img_pressed 不在 AssetLibrary 中"` |
| nc5 | `Normal` 条目不再跳过 | 导出侧判据 | `Sub-string found … lv_color_hex(0xABCDEF)` |
| nc6 | 预览侧 `VerticalAlign` 忽略状态 | 预览侧判据 | `Expected: Bottom / Actual: Top` |

6 支逐支独立日志（`.workbuddy/tmp/s202-nc{1..6}.log`）、每腿限时、开跑前扫残留注入，
还原后 2 个文件的 SHA256 逐字节相同。

## 6. 读数与仍未做

**读数**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 315 / 2260 / 798 / 8`（合计 **3647**）
＝ **3647 通过 / 0 失败**（阶段 201 为 3639，**＋8**）；`HmiEditor.Core.Tests` **2260 → 2268**；
`App.UiTests` **798 / 0**（连跑 39 / 42 s）；`UnusedBaseline` **不变 1109**；
`MainWindow.axaml.cs` 仍 **2983**。

**仍未做（登记 `#92` 的剩余部分）**：

1. **让`TextVAlign`/`Text` 在运行时真的生效**：需要生成运行时回调（LVGL `LV_EVENT_STATE_CHANGED` 或
   本仓的事件表）—— 是**新功能**（属产品决策，不在"软件侧可验证"这一类里）。
2. **面板侧要不要标明**：既然这两格结构上导不出去，按本仓"面板上有、其实不生效 ⇒ 灰显 + 悬停原因"
   （`ConfigFieldInventory` 那套）的口径，它们该在**状态编辑**那一栏被标明 —— 这是 UI 决策
   （`Text` 至少还有一句注释；`TextVAlign` 此前什么都没有）。
3. **`VisualState` 只覆盖 4 态**：`Normal`/`Pressed`/`Checked`/`Disabled`；别的（`Focused`/`Edited`…）
   没有口子 —— 要不要加是功能决策。

---

## 7. 阶段 208：把"导不出去"**在面板上说给用户听**（`#92` 的标注那半；阶段 215 续做 `#96`/`#97`）

阶段 202 的结论是：非 `Normal` 态的 **状态文本** 与 **垂直对齐** 在导出侧**结构上表达不了**
（LVGL 没有状态化 vertical-align、静态样式也切不了文本）⇒ 导出只留一句**声明注释**。
但那个声明**只在代码里**，用面板的人看不到 —— 于是"改了看着生效、导出丢了"这层误会还在。

这一阶段给那两格各挂一句**悬停说明**（本仓对"面板上有、其实不生效"的一贯口径：灰显/提示 ＋ 原因）：

| 控件 | 悬停键 | 文案（zh） |
|---|---|---|
| `VsText`（状态文本） | `prop.visual.state.export.hint.text` | ⚠ 非默认态的状态文本：导出只留一行声明注释（LVGL 静态样式表达不了文本切换，需运行时事件回调） |
| `VsTextVAlign`（垂直对齐） | `prop.visual.state.export.hint.valign` | ⚠ 非默认态的垂直对齐：导出只留一行声明注释（LVGL 没有状态化的 vertical-align，需运行时重定位回调） |

两个键同时进 **zh-CN 与 en-US** 两本字典（键集一致由既有的本地化守卫看着）。

**判据**（`PropertyPanelVisualStateColorTests.Panel_MarksTheFieldsThatCannotBeExportedPerState`，两向）：
① XAML 里那两格必须挂**对应**的 `ToolTip.Tip` 键（挂错键也红）；② 那个键在**两本字典**里都要有**非空**值
（否则界面上显示的是键名）。⚠ 判据**不加 `Lang` 常量** —— 该键只被 XAML 引用，加常量会抬高
「`Lang` 常量未被静态引用」那个只减不增的守卫读数。

**负控 5 支**（`.workbuddy/tmp/s208-nc.py`）：去掉 `VsText` 的提示 · 把 `VsText` 挂到 valign 那个键上 ·
**en-US 字典删键** · 键值改空串 · zh 字典把键改名。读数例
`VsText 没有挂「导出侧只留声明」的悬停说明（期望 ToolTip.Tip={res:Loc prop.visual.state.export.hint.text}）`。
🩸 其中 nc2 第一版注入**构建不过** —— 我把 XML 注释写进了**起始标签内部**（`<TextBox … ToolTip.Tip="…" <!-- … -->>`），
XML 不允许；去掉注释才成立。

### 7.1 顺带量到的两件事（免得后人重做）

1. **`ComboBox`/`CheckBox` 的"读点在动作处理器"＝ 0 条**：全 App 未接线的具名选择控件有 **43** 个有"读点"，
   但读点**全在初始化/填充方法**里（`InitializeComboBoxes` / `PopulateThemeControls` / `ShowRoleDetail` …），
   **一条都不在动作处理器**；而且那 43 条里多数是**对 `.Items` 的写**被我的启发式当成了"读"。
   ⇒ 「动作处理器读点」这张账**不需要**往选择控件扩面（阶段 207 的台账已覆盖该覆盖的）。
2. **模拟器侧不是按字段名消费的**：按字段名 grep `HmiEditor.Simulator`/`SimulatorProjectExporter` 会得到
   "50/58 个面板字段零命中"—— **假象**：模拟器工程里的屏幕/刷新代码是 `HmiEditor.CodeGen` 生成的
   （`CodeGenerator.Simulator.cs` / `sim_refresh_bindings`），字段名出现在**生成物**里。
   这与阶段 203 那条教训同一形状：**名字级 grep 既有假阳也有假阴**。
3. **扫描面边界已核**：`Views/` 之外（`MainWindow.axaml` / `App.axaml` / `Controls/`）**没有**具名
   `TextBox`/数字框/选择控件 ⇒ 「谁读」面与两张动作读点台账的扫描面**是完整的**（不必再扩）。

### 7.2 阶段 215/218 续做：同一张表扩到**九格**

阶段 208 立的判据（XAML 必须挂对应 `ToolTip.Tip` 键 ＋ 键在两本字典里非空）这一阶段**直接扩表**，
把 `#96`／`#97` 两处也标出来（口径相同：本仓对"面板上有、其实不生效"一律**说给用户听**）：

| 控件 | 悬停键 | 文案（zh） |
|---|---|---|
| `PropTextAlign` | `prop.text.align.export.hint` | ⚠ 导出现在只在「下拉框」上写水平对齐；其它控件上这一项仅预览生效（见登记 `#96`） |
| `PropFontSize` | `prop.font.size.export.hint` | ⚠ 只有「字体」指向已导入的字体资产时，字号才会写进设备；否则导出沿用主题默认字体（预览仍按此字号显示） |

**阶段 218 再续 5 行**（同一张表 4 → **9 行**），把登记 `#98` 的"死设置"也说给用户听：

| 控件 | 悬停键 | 文案（zh） |
|---|---|---|
| `ScreenDpi` | `prop.screen.dpi.export.hint` | ⚠ 目前尚未生效：DPI 既不写进导出代码，也不影响预览（登记 `#98`） |
| `SafeLeft` / `SafeTop` / `SafeRight` / `SafeBottom` | `prop.screen.safearea.export.hint` | ⚠ 目前尚未生效：安全区既不写进导出代码，也不影响预览（登记 `#98`） |

⇒ 这实现了 `#98` 的处置③（**面板提示**）；若要改行为（接进导出 / 接进预览 / 从面板撤掉），
换掉这五处即可（改完这张表会红）。

⇒ 这实现了 `#96`／`#97` 的**处置③（写死范围 ＋ 面板提示）**；若之后决定改**行为**
（`#96` 的①导出补齐 / ②统一可见性、`#97` 的②内置字号近似映射），把这两条提示换掉即可
（改完这张表会红）。

> ⚠ **后续（2026-10-01，阶段 230）**：这些**行为**后来都落地了（`#96` ① 阶段 219 / ② 阶段 220、
> `#97` ② 阶段 220、`#98` 阶段 227/228、`#92` 阶段 229），而 §7.2 这两条提示（以及 §7 立的两条）
> **还写着旧事实** —— 本阶段把值改对、并把判据从"键在/非空"升级成"**内容与当前行为一致**"。
> 上面两张表是**当时**的文案快照（历史），当前值以字典为准 ⇒
> `docs/2026-10-01-export-hint-fidelity.md`（含六条键的旧→新对账与负控 5 支）。
