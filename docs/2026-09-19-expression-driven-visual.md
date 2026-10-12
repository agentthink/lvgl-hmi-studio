# 表达式驱动视觉（P1-1）—— 设计时即时生效 + 三端同一套语义

- 日期：2026-09-19
- 登记表项：`docs/2026-09-17-open-items-register.md` §3 第 15 项（"表达式不驱动视觉"）
- 计划出处：`docs/2026-09-18-mainstream-gap-check-and-optimization-plan.md` §6 P1-1
  （08-02 报告 I1 自留的"后续可扩展**表达式驱动颜色/可见性**"）
- 工作区读数：构建 **0 警告 / 0 错误**；`bash scripts/ci-gate.sh` **7 步全绿**（2026-09-19）
- 固件侧改动：**0**（生成产物只用既有 LVGL API 与既有取值函数，见 §6）

---

## 1. 问题：绑定只驱动"值"，视觉属性是"写死的"

接入前，`VariableBindingData` 只被值通路消费（`Text` / `Value`），颜色与可见性在三处各自为政：

| 位置 | 原行为 |
|---|---|
| 设计器 | `DesignCanvas` 与其 Renderers **直读** `control.BgColor` / `control.TextColor` / `control.IsVisible` |
| 生成代码 | 只在**初始化**时静态发一次颜色/可见性，运行期不变 |
| 模型 | `ExpressionEngine` 能编译 `tag>100 ? 16711680 : 65280`，但**没有任何消费点** |

`tools/ReproHarness/Program.cs` 里那条 `TextColor` 表达式绑定正好是症状的化石：
**模型接受、无人消费**（写成 `"#ff0000"` 也照样通过，因为它只是躺在 JSON 里）。

对标（WinCC / Ignition）的做法是：**属性级表达式**，设计时求值、运行期由生成代码周期判定。

---

## 2. 三个关键设计决策（都是"不做就会有坑"型的）

### 2.1 颜色走**数值**，不走 hex 字符串

生成的 C 是静态类型语言。下面这行**编译不过**：

```c
lv_obj_set_style_text_color(obj, cond ? "#ff0000" : "#00ff00", 0);
/*                                 int        vs  const char*   ⇒ 两分支类型不同 */
```

若设计器走字符串、设备走数值，两端必然分叉（就是 `BindingSemantics` 诞生前
"设计器显示 12.34、设备显示 12.3"那一类漂移）。因此统一约定：

> 表达式返回 **24 位 RGB 数值（0xRRGGBB）**。
> 设计时 `BindingVisualProperty.ColorFromNumber(v)` → `#RRGGBB`；
> 生成期 `lv_color_hex((uint32_t)(v))` —— **同一套数值规则**（高位与负数都按位截断）。

`ColorFromNumber` 对 `NaN` / `Infinity` 返回 `null`，由调用方回退**静态配置色**，
不让一个坏表达式把控件变成黑色。

### 2.2 复用**既有**取值机制，不新增运行时函数

`ExpressionEngine.ToCExpression()` 的默认变量映射是 `hmi_get_variable_value("name")` ——
**这个函数在产物里从未有过定义**（只出现在注释与脚本模板里）。项目铁律是
"每个 `extern` 声明都必须有定义"，照抄默认映射等于现场造一个链接期未定义符号。

做法：给 AST 求值加带 mapper 的重载，把变量接到**已经存在**的取值机制：

| 端 | 值来源 |
|---|---|
| 设备 | `get_tag_value(TAG_<variable>)`（标签表机制，原样复用） |
| 模拟器 | 局部正弦波变量 `float _bxv_<name> = … sinf(t + …)`（与变量绑定同口径） |

`ToCExpression()` 无参重载保留原语义（脚本模板 / 诊断沿用），**没有**删改既有行为。

### 2.3 视觉属性**不**走 `VariableBindingId` 旧字段回退

`PreviewValueResolver.ResolveBindingResult` 有一条历史回退：找不到绑定条目时用
`VariableBindingId` / `PrimaryBindingVariable`（旧字段）。这条回退只承载
"**主显示绑定**"。视觉属性若也吃它，控件配了旧字段后 `TextColor` 会把**显示数值**当颜色用
（`12.34` → `#000C…`），是静默错色。

因此新增 `bool allowLegacyFallback = true` 参数，视觉求值一律传 `false`：

```csharp
private string? EffectiveVisualColor(HmiControl control, string propertyName, string? staticValue)
    => IsPreviewMode
        ? PreviewValueResolver.ResolveVisualColor(control, PreviewValueProvider, propertyName) ?? staticValue
        : staticValue;

private bool EffectiveVisible(HmiControl control)
    => IsPreviewMode
        ? PreviewValueResolver.ResolveVisualVisibility(control, PreviewValueProvider) ?? control.IsVisible
        : control.IsVisible;
```

`?? staticValue` / `?? control.IsVisible` 是"**预览隔离契约**"：
非预览（编辑态）一律用静态设计值，解析不出来就让调用方回退 —— 编辑态永远不显示取值结果。

---

## 3. 单一事实源：注册表 + 唯一发射点

`src/HmiEditor.Core/Design/BindingVisualProperty.cs`（**新增**）

```csharp
public enum BindingVisualKind { Color, Visibility }

public sealed record BindingVisualProperty(string PropertyName, BindingVisualKind Kind)
{
    public static readonly BindingVisualProperty TextColor = new(nameof(HmiControl.TextColor), BindingVisualKind.Color);
    public static readonly BindingVisualProperty BgColor   = new(nameof(HmiControl.BgColor),   BindingVisualKind.Color);
    public static readonly BindingVisualProperty IsVisible = new(nameof(HmiControl.IsVisible), BindingVisualKind.Visibility);

    public static IReadOnlyList<BindingVisualProperty> All { get; } = new[] { TextColor, BgColor, IsVisible };

    public static BindingVisualProperty? Find(string? propertyName);   // Ordinal，与 PropertyName 同口径
    public static bool IsVisual(string? propertyName);
    public static string? ColorFromNumber(double value);
}
```

`src/HmiEditor.CodeGen/VisualBindingCodeGen.cs`（**新增**，`internal static`）——
"属性名 → LVGL 调用"的**唯一实现**，两端共用：

| 属性 | 发射的 C |
|---|---|
| `TextColor` | `lv_obj_set_style_text_color(obj, lv_color_hex((uint32_t)(v)), 0);` |
| `BgColor` | `lv_obj_set_style_bg_color(…);` **+** `lv_obj_set_style_bg_opa(obj, LV_OPA_COVER, 0);` |
| `IsVisible` | `if (v != 0) lv_obj_clear_flag(obj, LV_OBJ_FLAG_HIDDEN); else lv_obj_add_flag(obj, LV_OBJ_FLAG_HIDDEN);` |
| 未登记 | `throw new NotSupportedException(…)` |

两条都是踩过才写的：

- **`BgColor` 必须同时设 `bg_opa`**：Label 之类默认 `bg_opa = 0`，只设颜色在设备上
  "颜色生效了但看不见"（与既有初始化发射同口径）；
- **未登记就抛**：注册表加了条目却忘了派发 → 产物"看着对但不生效"，
  比编译错误难查得多。宁可当场炸。

---

## 4. 接入点

| 文件 | 改动 | 角色 |
|---|---|---|
| `Core/Design/BindingVisualProperty.cs` | 新增 | 注册表（唯一事实源） |
| `Core/Design/PreviewValueResolver.cs` | `allowLegacyFallback` 参数 + `ResolveVisualValue/Color/Visibility` | 设计时求值入口 |
| `Core/Services/ExpressionEngine.cs` | `ToCExpression(Func<string,string>)`（abstract）+ 无参重载 + 静态重载 | 变量映射可注入 |
| `CodeGen/VisualBindingCodeGen.cs` | 新增 | 刷新语句唯一实现 |
| `CodeGen/CodeGenerator.RefreshBindings.cs` | +185 行 | 视觉/值两路分流 + 初值 + 周期刷新 |
| `App/Controls/DesignCanvas.cs` | `EffectiveVisualColor` / `EffectiveVisible` 两个接缝 | 画布侧唯一入口 |
| `App/Controls/DesignCanvas.Renderers.cs` | **26 处**颜色直读改走接缝 | 各控件渲染器 |
| `App/Views/PropertyPanelView.axaml` | 「目标属性」下拉 + 本地化键 ×5 ×2 语言 | 绑定来源可选属性 |
| `App/Views/PropertyPanelView.Binding.cs` | `CurrentBindingPropertyName` / `FindReadBinding` / `SelectTargetProperty` / `OnTargetPropertyChanged` | 多属性绑定 |

### 4.1 生成期：两路分流 + 两种发射时机

`CodeGenerator.RefreshBindings.cs` 的收集阶段拆成 `visualTargets` / `valueTargets`：

- 去重键从 `ctrlId` 改为 **`ctrlId + ":" + propertyName`**（视觉再加 `visual:` 前缀），
  否则同一控件的第二条绑定会被静默丢掉；
- 表达式绑定**去掉了历史上的 `continue`**，改为两步校验：语法能否编译 +
  **设备端**引用的每个变量都在标签表里（模拟器不需要，变量退化为正弦波）；
- 视觉刷新单独成"第 5 段"：设备端套 `if (obj_x) { … }` 空指针守卫
  （设备端控件可能尚未创建），`VisualBindingCodeGen.EmitRefresh` 单点派发；
- `BuildValueDeclaration` 决定取值表达式：设备 `get_tag_value(TAG_x)`、
  模拟器 `BuildSimExpressionDeclaration` 造局部正弦波（`CIdentifier.Prefixed("_bxv_", name)`）；
- `ContainsUnresolvedPlaceholder` 拦住还留着 `{{arg}}` 的 Faceplate 表达式 —— 发出去就是错的 C。

### 4.2 属性面板：目标属性下拉

`TargetPropertyCombo` 4 项（Tag）：`Text` / `TextColor` / `BgColor` / `IsVisible`。
绑定条目本就按 `PropertyName` 分开存放，此前**两处写入都硬编码 `"Text"`**，
所以同一控件想同时绑 `Text` 与 `TextColor` 只能靠手改工程 JSON。

现在 `CurrentBindingPropertyName` 从下拉读，`GetOrCreateReadBinding` / `FindReadBinding` /
新增条目的 `PropertyName` 全部按它取值。两条顺序契约（写反就出问题）：

1. `LoadVariableBinding` **先** `SelectTargetProperty(第一条读绑定的 PropertyName)` **再** `FindReadBinding`
   ——反了就是拿默认属性的绑定去填另一个属性的界面；
2. `OnTargetPropertyChanged` 回填该属性**自己**的绑定（否则切成 TextColor 时输入框里还留着
   Text 的表达式，用户一改就写错条目）。

---

## 5. 顺带修掉的两个**真实缺陷**

不是 P1-1 的目标，是被测试逼出来的：

| # | 缺陷 | 根因 | 修法 |
|---|---|---|---|
| 1 | **预览横幅会毁掉整块画布渲染** | 横幅是画布上**唯一**的裸 `DrawText` 调用（其它文本都走 `DrawAlignedText` 的容错重试）。headless 下字体 shaping 抛异常 ⇒ 异常从绘制路径抛回 UI 线程 ⇒ 整个画布空白 | 抽 `DrawPreviewBanner` + `DrawPreviewBannerCore`，套与 `DrawAlignedText` 同一条"捕获 → `RenderResourceCache.InvalidateTexts()` → 重试一次 → 再捕获"路径 |
| 2 | **选变量时 `PropertyName` 仍写死 `"Text"`** | 上轮只改了 `GetOrCreateReadBinding`，`OnVariableSelected` 的新增分支漏了 | 改为 `CurrentBindingPropertyName` |

缺陷 1 的定位过程值得记：6 条 UI 测试全崩在 `CmapFormat4Table.TryGetGlyph`，
初判是"CJK 字体 shaping"——**换 `Rectangle` 无文本控件、换 `en-US` 语言都没解决**。
改用判别实验：跑未被改动的 `GaugeTickRenderTests` / `ArcControlRenderTests` → **11/11 通过**
⇒ 排除"环境坏了 / 字体坏了"，收窄到"只有预览横幅会崩"。
**教训：先做判别实验确认是不是既有行为，别急着给测试打补丁绕过。**

---

## 6. 为什么固件侧零改动

生成产物只用了三类符号，全部已存在：

- `lv_obj_set_style_text_color` / `lv_obj_set_style_bg_color` / `lv_obj_set_style_bg_opa` /
  `lv_obj_clear_flag` / `lv_obj_add_flag` —— LVGL v9 公开 API；
- `lv_color_hex` —— LVGL 宏；
- `get_tag_value(TAG_*)` —— 既有标签表取值函数（**没有**新增 extern）。

所以 `firmware/` 目录本工作区**零改动**，门禁第 [7] 步固件 host 测试仍 **8 步全部通过**。

---

## 7. 验证

### 7.1 新增测试（**+46**）

| 文件 | 条数 | 锁什么 |
|---|---|---|
| `tests/HmiEditor.Core.Tests/BindingVisualPropertyTests.cs` | **21** | 注册表；数值→颜色换算（含 `-1 → #FFFFFF`、`33488896 → #FF0000` 截断）；设计时求值；非预览回退**null**；旧字段**不**回退；语法错回退；值属性不被视觉通路吞掉 |
| `tests/HmiEditor.Core.Tests/VisualBindingCodeGenTests.cs` | **13** | 两端都发射；值来源复用既有机制（`get_tag_value` / `_bxv_` 正弦波）；两端对拍；负控 4 条（值属性 / 语法错 / `{{arg}}` / 空表达式都不进视觉通路） |
| `tests/HmiEditor.App.UiTests/VisualBindingCanvasTests.cs` | **6** | 用 `CanvasRenderSpy` 录 `DesignCanvas.Render` 的绘制操作，断言填充画刷颜色 —— "设计时即时生效"的唯一硬证据 |
| `tests/HmiEditor.App.UiTests/PropertyPanelTargetPropertyTests.cs` | **6** | 下拉项 ↔ Core 注册表同源；表达式/变量写入落在**当前目标属性**（其中一条走**真实键盘输入**，见 §7.4）；同控件多属性共存且各自回填；负控：切下拉**不改模型**；下拉外的属性名回落值属性 |

`VisualBindingCanvasTests` 用 `HmiControlType.Rectangle`（无文本）刻意避开
`FormattedText` 字体 shaping；`FillColors` 的过滤条件必须是接口 **`ISolidColorBrush`**
而不是 `SolidColorBrush` —— 共享缓存返回的是不可变画刷实现，写窄了会静默返回空集合。

### 7.2 门禁（2026-09-19，`bash scripts/ci-gate.sh` **7 步全绿**）

| 步 | 读数 |
|---|---|
| [1] `bash -n` | 24 个脚本语法正确；构建脚本未就地改源码 |
| [2] 路径卫生 | 无硬编码开发机家目录（623 个文件） |
| [3] 构建（警告即失败） | **0 警告 / 0 错误** |
| [4] 出包 / 预算工具 | 出包自检通过（6 载荷，固件 1.1.0 / ABI 3） |
| [5] 大队测试 | Simulator 44 / Hardware 44 / Communication 75 / Device 87 / AI 315 / **App.UiTests 249** / **Core 1959** |
| [6] git 用例单独跑 | 8/8（648 ms，队列为空） |
| [7] 固件 host 测试 | **8 步全部通过** |

两段之和 = 全量 **2781** 项（登记表 09-18 快照为 2716；P1-1 直接贡献 **+46**，
其余来自 09-18 之后已提交的 P0-1~P0-5 轮次）。
日志：`.workbuddy/tmp/ci-gate-2026-09-19-expander.log` —— 跑在**四个提交全部落定、
且 §7.4 的真实键盘用例改写之后**的树上（日志 mtime `2026-09-19 16:51:26`，
末行自报 `🎉 门禁全部通过（7 步）`），因此上表读数对应的就是本节描述的树，而不是中间态。
更早一次 `.workbuddy/tmp/ci-gate-2026-09-19-postcommit.log`（mtime `2026-09-19 16:29:51`）
验证的是四个提交本身；两次读数只差 [2] 由 **621 → 623**（新增文件所致）。
判据是**改动文件的 mtime 早于日志 mtime**：`PropertyPanelTargetPropertyTests.cs` 16:49:28、
专题 16:49:57、`project-status.md` 16:50:05、登记表 16:50:09，全部早于 16:51:26。

### 7.3 负向验证

| # | 手段 | 结果 |
|---|---|---|
| 1 | 临时摘掉 `PropertyPanelWiringTests` 里 `TargetPropertyCombo` 的豁免 | ✅ **红**，且精确点名：`…要么它没接线（改了不保存），要么需要补一个能显示它的场景：TargetPropertyCombo`；恢复后 2/2 绿 |
| 2 | 门禁第 [5] 步（第一次跑） | ✅ **红**：`LangConstantUsageGuardTests` 报"无静态引用的 Lang 常量 1272 ≠ 基线 1267" —— 我把 5 个本地化键又加了一份 `Lang.*` 常量，而 XAML 走**键字符串**解析、C# 侧没人读。处置：**删掉这 5 个常量**（不是改基线） |
| 3 | `TestAssertionStyleGuardTests` | ✅ 拦住 8 处裸 `Assert.Contains("lv_…")`，改走 `CSource.AssertCode`（9 处） |
| 4 | 把 `expandBindingSection` 改成 `false`（即不展开折叠区） | ✅ **红**，报的是那条明确守卫 `控件不在可视树里，键盘输入送不到（折叠 Expander 的内容在 headless 下不会 attach）` —— 证明 §7.4 的真实键盘路径**真的**依赖"Show 前展开"，不是断言被静默跳过；恢复后 6/6 绿 |

第 2 条是这轮最有价值的一条：**守卫在我"觉得加了更规整"的地方判了红**，
而正确处置是删代码、不是放宽判据。

### 7.4 附：判别实验 —— 折叠 `Expander` 的内容为什么不 attach

这条不是 P1-1 的目标，是复核"上一版遗留的那条未覆盖"时挖出来的，
但它把一个**只能靠猜的宿主行为变成了一条可执行的规矩**，值得单独记。

**起点**：上一版 `PropertyPanelTargetPropertyTests` 无法用真实键盘输入表达式框，
只能"赋值 + 手动触发 `TextChanged`"。当时的记录是"headless 下 `Focus()` 请求被拒"，
并挂了一条待复核（可能是真实可用性缺陷）。

**判别实验**（同一个测试宿主内放 baseline 对照）：

| 探针 | `Focus()` | 结论 |
|---|---|---|
| ① 全新窗口里的裸 `TextBox` | **True** | headless **支持**焦点，不是平台不支持 |
| ② 面板里 `Expander` **外**的 `PropName` | **True** | 面板整体也没问题 |
| ③ `Expander` **内**的 `ExpressionInput` | **False** | 问题特定于这一处 |

再 dump 得到决定性两行：`ExpressionInput.IsAttachedToVisualTree() == false`、
`TopLevel.GetTopLevel(box) != window` —— **它压根没进可视树**。
（`IsEffectivelyVisible == true` 是假象：没挂树就没有祖先可判"不可见"。）

往上一层看 `Expander` 自己：`attached=True`、`链深=13`、`Bounds=(0,926,982,48)`、
`视觉后代=10` —— **高度 48 正是折叠态**，内容子树从未被创建。

**决定性对照（两种展开时机）**：

| 时机 | Expander Bounds 高 | 视觉后代 | 含 `ExpressionInput` | `Focus()` |
|---|---|---|---|---|
| `Show()` **之前** `IsExpanded = true` | **288** | **96** | **True** | **True** |
| `Show()` **之后** `IsExpanded = true` | 48 | 10 | False | False |

后一种情形下，`ApplyTemplate()` + `InvalidateMeasure()` + 强制 `Measure/Arrange` +
多轮 `Pump` **全都救不回来**。

**结论与规矩**：

- 这是 **headless 宿主的行为，不是产品缺陷** —— 情形 A 证明内容能 attach、能聚焦、
  能收键盘输入（`box.Text` 变成 `temp > 200`），而该面板在真实应用里是已发布功能。
- **规矩**：任何要驱动"折叠 `Expander` 内控件"的 UI 测试，**必须在 `window.Show()` 之前
  把 Expander 展开**。为此 `PropertyPanelTargetPropertyTests.Host` 增加
  `expandBindingSection` 参数，并在 `TypeInto` 里用
  `Assert.True(box.IsAttachedToVisualTree(), "控件不在可视树里，键盘输入送不到")` 守住 ——
  否则将来有人重构掉那个参数，测试会**静默**退化成"敲字没反应"（这一点由 §7.3 第 4 组负向验证证实）。
- 于是 §8 原第 6 条那条局限**消失**：表达式框的真实键盘路径现在真的被覆盖了。

---

## 8. 未覆盖 / 局限（如实转登记）

| # | 限制 | 说明 |
|---|---|---|
| 1 | 只支持 **3 个**视觉属性 | `IndicatorColor` / `BorderColor` / `Opacity` / 文本对齐等未纳入。扩展点是把条目加进 `BindingVisualProperty.All` **并**在 `VisualBindingCodeGen` 里派发（漏了会当场抛） |
| 2 | 颜色只有 **24 位 RGB** | 无 alpha、无主题变量（`PaletteDefaults` 的语义色不能直接当表达式值用） |
| 3 | 下拉只列 **4 项** | 旧工程里 `PropertyName = "Value"`（Slider/Knob 值通路）之类**不在**下拉里 ⇒ 回落显示成值属性。行为已由测试锁住（`UnknownPropertyName_FallsBackToValueProperty_AndStillShowsItsVariable`），但**用户无法在下拉里选到它** —— 属已知局限 |
| 4 | 设备端刷新开销**未实测** | 每次刷新对每个视觉绑定调一次 `get_tag_value`。高频轮询周期（`ScanIntervalMs=100`）下的实际开销没有实机数据 |
| 5 | **无实机验证** | 全部证据来自生成代码文本 + headless 渲染 + 固件 host 测试；真实 LVGL 上的观感（尤其 `bg_opa` 覆盖面）未在设备上确认 |

> 上一版这里还有第 6 条"表达式框的真实键盘路径未在 UI 测试里覆盖 / 折叠展开后能否聚焦待复核"。
> 该条**已判明并消除** —— 见 §7.4：不是产品缺陷，是 headless 宿主下 `Expander` 只在
> "首次布局前就展开"时才创建内容子树；按那条规矩改完，真实键盘路径已被覆盖。

---

## 9. 复现命令

```bash
# 定向
dotnet test tests/HmiEditor.Core.Tests/HmiEditor.Core.Tests.csproj -c Release \
  --filter "FullyQualifiedName~BindingVisualPropertyTests|FullyQualifiedName~VisualBindingCodeGenTests"
dotnet test tests/HmiEditor.App.UiTests/HmiEditor.App.UiTests.csproj -c Release \
  --filter "FullyQualifiedName~VisualBindingCanvasTests|FullyQualifiedName~PropertyPanelTargetPropertyTests"

# 全量门禁
bash scripts/ci-gate.sh
```
