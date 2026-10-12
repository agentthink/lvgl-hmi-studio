# 导出侧悬停文案的**对账**（阶段 230）

> 起点：用户拍板"**先修陈旧悬停文案 ＋ 登记册 `⏳` 残句**，再开始登记册复核"。
> 这一阶段**不改行为、不加功能**：行为是阶段 219/220/227/228/229 改的，
> 本阶段只把**说给用户听的那句话**追上去，并给"追上去"这件事配一条会咬人的判据。

## 1. 事故形状：行为改了，说明没改

| 悬停键 | 面板上还写着（阶段 215/218 写的） | 当前行为（谁改的） |
|---|---|---|
| `prop.text.align.export.hint` | "导出现在**只在「下拉框」上**写水平对齐；其它控件上这一项仅预览生效（见登记 `#96`）" | 阶段 219 起 `Label` / `Button` / `CheckBox` / `TextArea`（有文本时）也发 `lv_obj_set_style_text_align`（`CodeGenerator.Screen.cs:240-260`），`Dropdown` 走参数 |
| `prop.font.size.export.hint` | "只有「字体」指向已导入的字体资产时，字号才会写进设备；**否则导出沿用主题默认字体**" | 阶段 220 起无可用资产且字号 ≠ 主题默认时，就近取内置 `montserrat_12` / `montserrat_16`（`BuiltInFontSizeFor`） |
| `prop.screen.safearea.export.hint` | 尾句"**DPI 缩放仍未进导出**（登记 `#98`）" | 阶段 228：DPI ⇒ 生成几何**整体换算**（`CDim`） |
| `prop.visual.state.export.hint.text` | "**导出只留一行声明注释**（LVGL 静态样式表达不了文本切换，需运行时事件回调）" | 阶段 229：能表达的类型生成 `LV_EVENT_STATE_CHANGED` 回调（含复位分支） |
| `prop.visual.state.export.hint.valign` | "**导出只留一行声明注释**（LVGL 没有状态化的 vertical-align，需运行时重定位回调）" | 阶段 229：`Label` 的状态垂直对齐生成运行时重定位回调 |

🩸 **共同点**：这五条都是"**面板上有、当时确实不生效**"那套口径（阶段 208 立的）留下的说明；
后来 `#96` / `#97` / `#98` / `#92` 相继改行为，**文案一字未动** ⇒ 用户在面板上读到的仍是旧事实。
`prop.screen.dpi.export.hint` 是同一张表里**唯一**跟着行为改过的一条（阶段 228 自己改的），
本阶段把它也**纳入规则表**（值不动），让六条键在同一张账上。

## 2. 为什么旧判据放它过去了

- `PropertyPanelVisualStateColorTests.Panel_MarksTheFieldsThatCannotBeExportedPerState`（阶段 208 立、215/218 扩表）两向核对的是
  **① XAML 挂了对应 `ToolTip.Tip` 键 ② 那个键在两本字典里非空** —— **不看内容** ⇒ 陈旧文案**全绿**。
- `EnUsMechanicalAuditTests`（阶段 226）判的是占位符 / CJK / 空值 / 两语相同 —— 也不看语义。

⇒ 缺口不是"没人判"，而是**判的是形状、不是事实**。这一段是阶段 115「面的边界 ≠ 没有缺口」的同族。

## 3. 本阶段改了什么

**改了 5 条键的值（zh ＋ en 共 10 处）**，全部按**当前生成器/面板代码**的口径重写；
`prop.screen.dpi.export.hint` 未改值（阶段 228 已正确），只进规则表。

| 键 | 新文案（zh）要点 |
|---|---|
| `prop.text.align.export.hint` | 点名文本类控件（`Label` / `Button` / `CheckBox` / `TextArea` ＋ `Dropdown`）；无文本控件上这一行不显示 |
| `prop.font.size.export.hint` | 三条规则：① 可用资产 ⇒ `font_族_号`（`#if LV_USE_FREETYPE`）② 无资产且 ≠ 主题默认 ⇒ 内置 `montserrat_{12,16}`（**近似**）③ = 主题默认 ⇒ 主题字体 |
| `prop.screen.safearea.export.hint` | 声明内容区 ＋ 越界控件**吸附**（夹位置、不改尺寸）；DPI 换算**指向**「屏幕 DPI」那一格 |
| `prop.visual.state.export.hint.text` | 能表达的类型 ⇒ `LV_EVENT_STATE_CHANGED` **回调** ＋ 回默认态的复位分支；其余类型仍只留注释 |
| `prop.visual.state.export.hint.valign` | `Label` ⇒ 回调（`LV_SIZE_CONTENT` ＋ 重定位）；其它类型 LVGL 没有状态化 vertical-align ⇒ 仍只留注释 |

## 4. 判据：从「键在、非空」升级到「内容与行为一致」

`PropertyPanelVisualStateColorTests.ExportHints_TrackTheCurrentBehavior`（**1 条**，App.UiTests）：

1. **双向规则**（每条键一条）：① **禁止**旧事实的**逐字原句**（锚点取自 git 历史，不是凭印象）；
   ② **必含**当前行为的锚词（与代码同词：`montserrat` / `LV_EVENT_STATE_CHANGED` / `吸附` / `语义数值` …）。
2. **两本字典都判**：`en-US` 也被扫（旧句的英文原句同样是锚点）—— 避免"只判中文、英文继续陈旧"。
3. **面自检**：悬停表（`HintBindings`）里的 6 个**不同**键 ↔ 规则表键集**一一对应**
   （加了悬停键却忘了配规则 ⇒ 红；规则表里留了已删的键 ⇒ 红）。
4. **元判据**：把旧文案喂给**同一个检查函数** ⇒ 必须报红。规则表若被改成空转（比如锚点写错、
   或语言分支永远取不到），这一条先红。

> 表本身（控件 → 键的**绑定**）仍由阶段 208 那条判据判 —— **同一条事实不两处判**：
> 老判据管"挂没挂对键"，新判据管"那句话对不对"。

## 5. 负控（5 支，`.workbuddy/tmp/s230-nc.py`）

每支把一条提示**改回旧事实**（逐字，直接写进字典文件），期望新判据红、逐字节还原：

| 支 | 注入 | 期望 |
|---|---|---|
| nc1 | zh `prop.text.align.export.hint` 改回"只在「下拉框」上写水平对齐" | 红 |
| nc2 | **en** `prop.font.size.export.hint` 改回"否则沿用主题默认字体" | 红 |
| nc3 | zh `prop.screen.safearea.export.hint` 改回"DPI 缩放仍未进导出" | 红 |
| nc4 | zh `prop.visual.state.export.hint.text` 改回"只留一行声明注释" | 红 |
| nc5 | **只**改 en `prop.visual.state.export.hint.valign`（zh 保持新的） | 红（两语都在面内） |

读数：**5/5 全部咬住**（`ExportHints_TrackTheCurrentBehavior [FAIL]`），每支还原后 sha256 逐字节相同；
日志 `.workbuddy/tmp/s230-nc1..5.log`。

## 6. 顺带：登记册 `⏳` 残句的清理

登记册 `#96` / `#97` / `#98` 三行的**状态列**里各留了一段上一版的
"`⏳ 待产品决策 …三选一`"块 —— 收口声明早在（阶段 219/220/227/228），残句没删 ⇒
**机械扫 `⏳` 仍会把这三条读成"仍待拍板"**（这正是用户看到的"待我拍板（`#96` 行为改动）"的来源）。
本阶段把它删净，并在原地留一行"何时删的、为什么删"。

⚠ **不动**的三处（都是**真开着**，不是残句）：`#61p` / `#61q` / `#61s` / `#61x`（各自的待决口径写在行内）、
台账里 `TagMonitorView` / `BuildRunDialog` 的覆盖缺口。

## 7. 读数（门禁）

**门禁**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `5.40 / 12.84 / 9.16`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`（合计 **3770**）
＝ **3770 通过 / 0 失败**（阶段 229 为 3769，**＋1**）；`App.UiTests` **832 → 833**；
`HmiEditor.Core.Tests` 仍 **2348**；**产品代码 0 行**（改的是字典取值）。

## 8. 仍未做（如实划界）

1. **母语级校对没做**：新文案只保证"与行为一致"，语气/大小写/缩写地道性仍归 `#27` 的边界。
2. **扫描面只有这 6 条键**：别的 `ToolTip.Tip`（如 `#98` 的两条已纳入，其余各处）没有
   "内容与行为一致"这一层；要扩面得先给那些键各写一条**可判的**事实锚点。
3. 提示里出现了 `#if LV_USE_FREETYPE`、`montserrat_12` 这类**实现细节** —— 这是"宁可说实话、
   不写成含糊承诺"的取舍；若产品要求面向现场的措辞，应另立一版文案（并同步本判据的锚词）。
