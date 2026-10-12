# XAML 属性式接线面（第三形态，阶段 199）—— 附一次勘误

> 本仓的接线有**三种形态**（选择族面从阶段 115 起就三种一起算）：
> ① **XAML 属性** `<TextBox TextChanged="OnX"/>` · ② **code-behind** `X.TextChanged += …` ·
> ③ **辅助函数注入**（`AttachChangeHandler(X)` 这类）。
> 阶段 189~198 建 TextBox / 数字框两个接线面时，**只扫了 ②** —— 这个缝里藏着 26 个具名 TextBox。

---

## 1. 🩸 勘误：26 个具名 TextBox 藏在那个缝里

实测（2026-10-01，阶段 199），`Views/**/*.axaml` + 对应 partial 全扫：

| 形态 | 具名 TextBox | 具名数字框（`NumericUpDown`/`fa:FANumberBox`） |
|---|---|---|
| ② code-behind `X.<事件> +=` | **57** | **45** |
| ① **XAML 属性 `<事件>="…"`** | **26** | **0** |
| ③ 辅助函数注入 | 9（`AttachChangeHandler`） | 2（`nud` / `fanumber`） |

⚠ **① 与 ② 互斥**：实测 26 条属性式里**没有一条**同时出现在 code-behind 里。
⇒ 阶段 189~198 那几张账里"这些框没有 `TextChanged` 接线"的说法，对其中 **26 个框是错的**。

**受影响并已同步更正的三处**：

1. `PropertyPanelTextBoxWiringTests` 的 20 条 `withoutWiring` 理由 —— 原文
   "由 `SaveProperties` 批量写回，没有 TextChanged 接线（另有 `PropertyPanelWiringTests` 逐编辑器驱动）"
   **两句都不准**：它们**有** `TextChanged` 接线（属性式），而 `PropertyPanelWiringTests` 枚举的编辑器族
   （`FANumberBox`/`ComboBox`/`CheckBox`）**不含 TextBox**。现在改写为"XAML 属性式接线 ⇒ 不在本面"。
2. **读侧面**（阶段 196）的分账：`91 / 122 / 118 / 4` → **`117 / 92 / 4`**
   （按形态拆开：code-behind 91 ＋ XAML 26 已接线 · 92 没接线但有读点 · 4 只读展示；**加起来 213**）。
3. **两个接线面（专题 §1/§6.1）的口径**：那些面是"**code-behind 形态**的接线面"——
   边界不是"这个框有没有接线"，而是"这个形态的接线有没有判据"；第三形态自成一面（本文件）。

## 2. 26 条逐条归类（3 条已驱动 / 23 条待驱动）

| 视图 | 控件 | 处理器 | 状态 |
|---|---|---|---|
| `FaceplateTemplateEditorView` | `TxtName` | `OnMetadataChanged` | ✅ 已驱动（`FaceplateTemplateEditorUiTests`：**真键输入** → 写回裁剪值；另有"清空 ⇒ 落本地化兜底名"） |
| `FaceplateTemplateEditorView` | `TxtCategory` / `TxtDescription` | `OnMetadataChanged` | ✅ 本面新驱动（写值 ＋ 补发事件） |
| `ToolboxView` / `ResourceLibraryView` / `FaceplateLibraryView` | `SearchBox` ×3 | `OnSearchTextChanged` | ⏳ 待驱动：补发事件后断言列表被过滤（三个视图各要夹具） |
| `PropertyPanelView` | 20 个（`PropName` / `PropText` / `PropTextColor` / `PropBgColor` / `PropBorderColor` / `IndicatorColorBox` / `ValuePrefixBox` / `ValueSuffixBox` / `OptionsBox` / `PlaceholderBox` / `HeaderBox` / `TabTitlesBox` / `ColWidthsBox` / `TxtActionValue` / `ScreenName` / `ScreenBgColor` / `VsBgColor` / `VsTextColor` / `VsBorderColor` / `VsText`） | `OnPropertyChanged` / `OnScreenPropertyChanged` / `OnVisualStateChanged` | ⏳ 待驱动：属性面板的编辑框按**控件类型显隐**，要逐类型搭夹具 |

**本面新驱动那两条量出的口径差**（值得记一笔）：`TxtName`/`TxtCategory` 是**裁剪后**写回（空 ⇒ 落本地化兜底），
而 `TxtDescription` **原样**写回（`_template.Description = TxtDescription.Text ?? ""`，没有 `Trim`）——
"元信息三个框"原来不是同一个口径。

## 3. 判据（4 条）

- `Face_MatchesTheRegistry`：**双向差集** —— 扫描到的每条属性式接线都要在登记表里
  （视图 / 控件名 / **处理器名**三元组对得上），登记表里每条也必须真的存在。
  新加一条属性式接线、摘掉一条、处理器改名 ⇒ 红。＋ 面自检（≥26 条 · ≥4 个视图）。
- `EveryWiring_IsDrivenOrHasAConcreteReason`：每条要么 `Driver` 指向的判据文件**存在且点名**这个框
  （"有判据驱动"不是空话），要么 `Note` 写清为什么还没驱动（太短 ⇒ 红）。＋ 读数自检（≥3 driven / ≥20 pending）。
- `Scanner_SelfTest`：属性式只在标签属性里 · `-->` 注释里的不算 · 空值不算 · **code-behind 的 `+=` 不算**
  （两个形态混起来会把 26 条算重）· 数字框那族单独扫。
- `FaceplateMetadataBoxes_WriteBackTrimmedValues`：本面补的两条驱动（见 §2 的口径差）。

⚠ **顺手修掉的一个隐身 bug**：扫描器原先不剥 XAML 注释 —— 自测里那句
`<!-- <TextBox x:Name="InProse" TextChanged="OnGhost"/> -->` 当场被算成一条**活接线**。
`ControlWiringFace` 现在统一 `StripXamlComments`（`NamedBoxes` 也一起剥）——
实测 `Views/` 里注释掉的具名控件是 **0 个**，所以剥了**不改任何读数**。

## 4. 负控（6 支，`.workbuddy/tmp/s199-nc.py`）

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | 摘掉 `PropName` 的属性式接线 | 双向差集 | `登记表里这些条目在 XAML 里对不上 … PropertyPanelView.axaml::PropName=OnPropertyChanged` |
| nc2 | 登记表里的处理器名漂了（`PropText` → `OnPropertyChanged_Stale`） | 双向差集 | 同上（登记 ≠ 扫描） |
| nc3 | 新增一条没登记的属性式接线（`GhostBox`） | 双向差集 | `这些属性式接线没有登记 … PropertyPanelView.axaml::GhostBox=OnPropertyChanged` |
| nc4 | `Driver` 指向不存在的判据文件 | 归类核对 | `TxtName 的 Driver 文件不存在：NoSuchJudgementTests.cs` |
| nc5 | 待驱动登记的 `Note` 清空 | 归类核对 | `ToolboxView.axaml::SearchBox 既没有驱动判据，也没写清理由（Note 太短）` |
| nc6 | 去掉 XAML 注释剥离 | 扫描器自测 | `Assert.Equal() Failure: Collections differ`（多出 `InProse`） |

6 支逐支独立日志（`.workbuddy/tmp/s199-nc{1..6}.log`）、每腿限时、开跑前扫残留注入，
还原后 4 个文件的 SHA256 逐字节相同。

🩸 **其中两条的第一版注入构建不过**（负控"必须能编译"这条纪律又用上）：Avalonia 的 XAML 编译器会校验
`TextChanged="不存在的方法"`（`AVLN3000`）⇒ ① "处理器改名"不能只改 XAML（要连带 17 处引用）；
② "新增接线"必须挂**已存在的**处理器。最后把 nc2 改成**登记表侧**的漂移、nc3 挂 `OnPropertyChanged` 才成立。

## 5. 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 793 / 8`（合计 **3634**）
＝ **3634 通过 / 0 失败**（阶段 198 为 3630，**＋4**）；
全量 App.UiTests **793 / 0**（连跑 2 次，38 / 43 s）；`UnusedBaseline` **不变 1109**；
**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

## 6. 仍未做

- **23 条待驱动的属性式接线**（3 个 `SearchBox` ＋ 属性面板 20 个）：登记表里逐条写了理由与做法；
  属性面板那 20 个要**逐控件类型**搭夹具（`OptionsBox`/`TabTitlesBox` 这类只在特定类型下可见）。
- **数字框族的属性式接线实测 0 条** —— 以后若有人这么写，本面的 `XamlWiredNumberBoxes` 会扫到，
  但登记表只覆盖 TextBox；真出现时要把数字框那一列也补上（现在扫描原语已就绪）。
- **`Views/` 之外**（`Controls/`、设计器）的 XAML 属性式接线不在扫描面内。
- 第三形态的**行为面**口径还没统一进两个接线面：现在它们是"code-behind 面"，
  要合并成"所有形态一个面"还是"一形态一面"，是一个**口径选择**（本阶段选了后者：一形态一面，理由是可分别量、可分别收口）。

---

## 7. 阶段 200：三条 `SearchBox` 收口（待驱动 23 → 20）

§2 那张表里最便宜的三条（三个库/工具箱的搜索框）已经驱动起来了。三条**形状完全相同**，
处理器都是 `OnSearchTextChanged` → 关键词过滤 → 把结果写回列表/画廊：

| 视图 | 过滤对象 | 读的界面 |
|---|---|---|
| `ToolboxView` | 静态工具箱目录（26 项） | `ToolboxList.ItemCount` ＋ `EmptyStateBorder.IsVisible` ＋ `ResultCountText.Text` |
| `ResourceLibraryView` | `_library.AllResources` | `Gallery.Children.Count`（`WrapPanel` 被重建） |
| `FaceplateLibraryView` | `_allTemplates` | `TemplateListBox.ItemCount` |

**三条共同的坑（写进判据的前提自检里）**：

1. 处理器首行都是 `if (Parent == null) return;` ⇒ 视图必须塞进 `Window` 并 `Show()`，否则写值也什么都不发生；
2. 程序化写 `TextBox.Text` **不触发** `TextChanged` ⇒ 用 `ControlWiringFace.Write`（它会补发事件）；
3. 断言**不读本地化文案**：工具箱搜的是**类型名**（`Slider` 与界面语言无关），
   库/模板夹具用自造的名字（`Alpha` / `通用` / `乙`）。

**每条判据的读数**（都带前提自检 + 空结果 + 清空回全量三拍）：

- `ToolboxSearchBox_FiltersTheToolList`：搜 `Slider` ⇒ 只剩那 1 条（目录 26 项、`ControlType` 逐条核对）·
  搜不存在的词 ⇒ `ItemCount == 0` ＋ 列表隐藏 ＋ 空状态出现 ＋ 计数文案清空 · 清空 ⇒ 回 26。
- `ResourceLibrarySearchBox_FiltersTheGallery`：灌 3 条自造资源 ⇒ 搜 `alp`（**大小写不敏感**）⇒ 1 张卡 ·
  搜 `乙`（走 `DisplayName`）⇒ 1 张 · 不存在的词 ⇒ 画廊空 · 清空 ⇒ 回 3 张。
- `FaceplateLibrarySearchBox_FiltersTheTemplateList`：3 个模板 ⇒ 搜 `alp` ⇒ 1 · 搜 `通用`（走 `Category`）⇒ 2 ·
  `"  beta  "` ⇒ **两侧空白被 Trim 吃掉** ⇒ 1 · 不存在的词 ⇒ 0 · 清空 ⇒ 回 3。

### 7.1 负控（5 支，`.workbuddy/tmp/s200-nc.py`）

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | 摘掉 `ToolboxView.axaml` 的 `TextChanged="OnSearchTextChanged"` | 面 ＋ 驱动 | 面：`登记表里这些条目在 XAML 里对不上：ToolboxView.axaml::SearchBox=OnSearchTextChanged` |
| nc2 | 处理器忽略关键词（`var filter = ""`） | **只有驱动**（面照旧绿） | `Expected: 1 / Actual: 26` |
| nc3 | `FaceplateLibraryView` 去掉 `.Trim()` | 驱动 | `Expected: 1 / Actual: 0`（`"  beta  "` 匹配不上） |
| nc4 | `ToolboxView` 空状态恒 `false` | 驱动 | `Assert.True() Failure`（空状态没出现） |
| nc5 | 登记里 `Driver` 指向**不点名该框**的判据 | 归类核对 | `ToolboxView.axaml::SearchBox 说由 ToolboxIconDictionaryTests.cs 驱动，但那个判据里没有点名这个框` |

> **nc2 是这一批里最有信息量的一支**：它把"面绿而驱动红"分开 —— 面管的是"**接线形态**有没有被登记/归类"，
> 驱动管的是"**这条接线干了什么**"。两者缺一不可（面绿不代表接线有用；驱动绿不代表没漏形态）。

5 支逐支独立日志（`.workbuddy/tmp/s200-nc{1..5}.log`）、每腿限时、开跑前扫残留注入，
还原后 4 个文件的 SHA256 逐字节相同。

### 7.2 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 796 / 8`（合计 **3637**）
＝ **3637 通过 / 0 失败**（阶段 199 为 3634，**＋3**）；
全量 App.UiTests **796 / 0**（连跑 2 次，36 / 36 s）；`UnusedBaseline` **不变 1109**；
**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

### 7.3 仍未做

- **属性面板那 20 条**（`PropName` 一族）：编辑框按**控件类型显隐**，要逐类型搭夹具 ——
  这是剩下的唯一一簇（登记表里逐条写了做法）。
- **数字框族的属性式接线实测 0 条**（扫描原语已就绪）。
- `Views/` 之外的属性式接线。

---

## 8. 阶段 201：属性面板那 20 条收口（`pending` 归零）＋ 逼出 `#91`

待驱动的最后一簇是属性面板的 20 个编辑框（`PropName` 一族，`OnPropertyChanged` /
`OnScreenPropertyChanged` / `OnVisualStateChanged`）。它们按**控件类型显隐** ⇒ 逐类型搭夹具会很贵。
这一阶段换了走法：**把既有的枚举驱动判据扩到 TextBox**，20 条一次性有了判据 —— 并且**当场红了**。

### 8.1 判据：枚举面扩到 TextBox

- `PropertyPanelWiringTests.EnumerateEditors`：`is FANumberBox or ComboBox or CheckBox` →
  **`or TextBox`**（面板逻辑树 91 → **94**：多出的 3 个里 2 个是**内嵌 `VariablePicker`** 的
  `SearchBox`/`DisplayField`——逐个进豁免清单并写明理由，1 个是 `ExpressionInput`）。
- `Drive` 加 `case TextBox`：写值 ＋ **补发 `TextChangedEvent`**（程序化赋值不发事件，阶段 187 实测）。
  候选序列：普通文本 → **合法十六进制**（`SanitizeColor` 会把非法色值退回去）→ 数字串 → 中性控件名 → 空。
- 计数下限 `≥ 60` → **`≥ 85`**：这条下限就是"枚举面别偷偷退回去"的守卫（负控 nc3 打的就是它）。

### 8.2 🩸 逼出的缺陷 `#91`：`Normal` 态的三色改了什么都不写

扩到 TextBox 之后，`VsTextColor` / `VsBorderColor` 立刻报**未覆盖**。追下去：

- 提示语写着「其它态留空 = 继承 `Normal`」，装载侧（`LoadVisualStateFields`）在 `Normal`
  也把三格当"控件当前色"填；
- 写回侧（`SaveVisualState`）的 `Normal` 分支**只写文本/对齐/图片**，三色**既不写控件、也不建状态条目**；
- 而 `OnVisualStateChanged` 末尾 `UpdateVisualStateSwatches()` 会把色块刷成新颜色
  ⇒ **用户看到"改成功了"的假象**，实际静默丢弃（其余三态走 `else` 分支写状态条目，正常）。

**修法**（产品侧 354 → 391 行）：`Normal` 分支补三行 `if (bg != null) control.BgColor = bg;`（`fg`/`bc` 同形）
＋ `SyncContentBox` 同步内容区同字段三框（与既有的 `PropText` 同步同形）；装载侧在 `Normal`
改成读 `control.BgColor/TextColor/BorderColor`（**写读对称**）。
⚠ 口径：**只在非空时写** —— `Normal` 态清空某一格 ≠ "把控件颜色清成 null"（"留空"只在其它态表示继承）。

**判据** `PropertyPanelVisualStateColorTests` 两条：① `Normal` 态写三色 ⇒ 落进控件 ＋ 内容区框同步
（＋ 清空不回退）；② 切到 `Pressed` ⇒ 写状态条目、**控件三色一字不动**，四条全清空 ⇒ 移除条目。
**负控 nc1** 回退这一修 ⇒ 新判据**红**，且 `PropertyPanelWiringTests` 的"未覆盖"也**红**
（`VsBorderColor, VsTextColor`）—— 这一支同时证明：**是"枚举面扩到 TextBox"这盏探照灯照到了 `#91`**。

### 8.3 收口口径：`enumerated@`

20 条登记从「待驱动」改成 **`enumerated@PropertyPanelWiringTests.cs`** —— 与阶段 118/195 的
`enumerated` 同一口径：**不逐条点名**（判据自己保证穷尽），核对是**指针级**的
（文件存在 ＋ 真的提到 `PropertyPanelView` ＋ 含 `uncovered` 字样）。
棘轮：**`driven == 26` / `pending == 0`** —— 新冒出来的属性式接线要么驱动起来，要么显式改这条下限并写清理由。

### 8.4 负控（6 支，`.workbuddy/tmp/s201-nc.py`）

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | 回退 `#91` 的修（`Normal` 分支不写三色） | 新判据 ＋ 枚举面 | 枚举面：`以下编辑器既没被任何场景驱动出模型变更 …：VsBorderColor, VsTextColor` |
| nc2 | 去掉内容区三框同步 | 新判据 | `Expected: "#123456"`（内容区框没跟着变） |
| nc3 | 枚举面回退（`.Where(...)` 去掉 `or TextBox`） | 枚举面（计数下限） | `逻辑树里只找到 71 个编辑器 …（预期 91 个左右）` |
| nc4 | 20 条之一挪回「待驱动」（理由写够长） | 棘轮 | `Assert.Equal() Failure: Expected 26 / Actual 25` |
| nc5 | `enumerated@` 指向不提 `uncovered` 的判据 | 指针级核对 | `PropText 说由 ToolboxIconDictionaryTests.cs **枚举驱动**，但那个判据里没有 PropertyPanelView / uncovered 字样` |
| nc6 | 三色改成**无条件写** | 新判据 | `Expected: "#123456" / Actual: null`（`Normal` 态清空被当成"清成 null"） |

6 支逐支独立日志（`.workbuddy/tmp/s201-nc{1..6}.log`）、每腿限时、开跑前扫残留注入，
还原后 3 个文件的 SHA256 逐字节相同。

### 8.5 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 315 / 2252 / 798 / 8`（合计 **3639**）
＝ **3639 通过 / 0 失败**（阶段 200 为 3637，**＋2**）；
全量 App.UiTests **798 / 0**（连跑 2 次，46 / 43 s）；`UnusedBaseline` **不变 1109**；
产品侧 `PropertyPanelView.VisualStates.cs` **354 → 391 行**；`MainWindow.axaml.cs` 仍 **2983**。

### 8.6 仍未做

- **数字框族的属性式接线实测 0 条**（`XamlWiredNumberBoxes` 已就绪，真出现时要补那一列）。
- **`Views/` 之外**（`Controls/`、设计器）的属性式接线不在扫描面内。
- **「视觉状态」另外那几项**（对齐/图片/文本的 `Normal` ↔ 其它态口径）还没成面 ——
  本阶段只把**颜色**这一族的口径钉死了。
