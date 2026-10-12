# 文本类控件的「文本 ＋ 对齐」都进导出（阶段 219）
## —— `#96` 的"导出补齐"那半 ＋ 更正一次量错的结论 ＋ 新登记 `#99`

> 这一阶段的起点是用户拍板的三处行为决策之一：**`#96` 导出补齐/统一可见性**。
> 做法先做**导出补齐**（本文），**统一可见性**与 `#97` 一起留下一阶段。

---

## 1. 🩸 先更正：阶段 212 的"Label 上 TextAlign 不发射"是**量错了**

阶段 212 建枚举取值账时，`TextAlign` 那一条的夹具是**一个没设 `Text` 的 Label**，
于是三个取值都被钉成 `null`（"不发射"），并据此登记了 `#96`（"导出只在 Dropdown / 值刷新路径读它"）。

**这是夹具缺陷**：导出把 `text_align` 写在

```csharp
if (!string.IsNullOrEmpty(control.Text))   // CodeGenerator.Screen.cs
{
    ...
    else if (control.Type == HmiControlType.Label)
    {
        sb.AppendLine($"    lv_label_set_text(...)");
        sb.AppendLine($"    lv_obj_set_style_text_align(..., {LvglTextAlign(control.TextAlign)}, 0);");
    }
}
```

里 —— **Label（有文本）与 Button（子 label）一直都在发射**。实测复核后：

| 控件 | 预览按 `TextAlign` 画？ | 导出发射 `text_align`？ | 阶段 219 的处置 |
|---|---|---|---|
| `Label`（有文本） | ✅（通用文本路径用 `ControlTextStyleResolver.HorizontalAlign`） | ✅ 早就发射 | 把枚举面的夹具补上 `Text`，三条样本改成真实期望 |
| `Button` | ✅（`RenderButton` 同上） | ✅ 发射在**子 label** 上 | 同上（不变量：别把 `lv_label_set_text` 传到 Button 本体） |
| `Dropdown` | —（自带渲染器） | ✅（`EmitDropdownParameters`） | 抽查一次 |
| **`CheckBox`** | ✅（`RenderCheckBox` 直接传 `control.TextAlign`） | ❌ **只设文本、不设对齐** | 🩸 **本阶段补齐** |
| **`TextArea`** | ✅（通用文本路径） | ❌ **连文本都不发**（另立 `#99`） | 🩸 **本阶段补齐（文本 ＋ 对齐）** |

⇒ 教训与阶段 214/217 同一条：**夹具不够，结论就是假的**；这次的形态是"缺一个前置条件字段
（`Text`），把一条本来就好的链量成了缺口"。

## 2. 产品改动（`CodeGenerator.Screen.cs` 的文本块）

```csharp
if (!string.IsNullOrEmpty(control.Text))
{
    if (control.Type == HmiControlType.CheckBox)
    {
        lv_checkbox_set_text(obj, "…");
        lv_obj_set_style_text_align(obj, LV_TEXT_ALIGN_*, 0);        // 🩸 阶段 219 补（#96）
    }
    else if (control.Type == HmiControlType.TextArea)
    {
        lv_textarea_set_text(obj, "…");                              // 🩸 阶段 219 补（#99）
        lv_obj_set_style_text_align(obj, LV_TEXT_ALIGN_*, 0);        //   顺带补对齐
    }
    else if (control.Type == HmiControlType.Button) { … }            // 已有
    else if (control.Type == HmiControlType.Label) { … }             // 已有
}
```

**没有文本时两者都不发**（保持不变）—— 免得留下"对齐了一个空对象"这种噪音。

## 3. 判据：`TextEmissionFaceTests`（16 条）

按**类型**逐个点名（枚举取值账按**字段**组织，一次只看一个类型，正好漏掉这种"按类型分叉"的缺口）：

| 组 | 条数 | 断言 |
|---|---|---|
| `EveryTextControl_ExportsItsText` | 4 | Label/Button/CheckBox/**TextArea** 各自的文本 API |
| `EveryTextControl_ExportsItsAlignment` | 4 | 三个在本体上、**Button 在子 label 上** |
| `NoText_ExportsNeitherTextNorAlignment` | 4 | 无文本 ⇒ 文本与对齐都不发 |
| `EveryAlignValue_IsExported` | 3 | Left/Center/Right → `LV_TEXT_ALIGN_*` |
| `Face_CoversTheTextBearingTypes` | 1 | Dropdown 抽查（options ＋ 对齐）＋ 五类名单自检 |

## 4. 负控（5 支，`.workbuddy/tmp/s219-nc.py`）

| 支 | 注入 | 读数 |
|---|---|---|
| nc1 | 去掉 CheckBox 的 `text_align` | `生成代码中未找到调用: lv_obj_set_style_text_align(obj_c1, LV_TEXT_ALIGN_RIGHT, 0);` |
| nc2 | 去掉 TextArea 的 `lv_textarea_set_text` | `生成代码中未找到调用: lv_textarea_set_text(obj_c1, "温度");` |
| nc3 | CheckBox 的对齐设到"子 label"（错误目标） | 同上第一条 |
| nc4 | TextArea 的文本改用 `lv_label_set_text`（错 API） | 同上第二条 |
| nc5 | 文本块门槛改成恒真（没文本也发射） | `System.NullReferenceException`（`EscapeCString(null)` 当场炸 —— 门槛不是装饰） |

🩸 nc3 的锚点第一版**命中 3 次**（那行文本在 Label/CheckBox/TextArea 三处都出现）⇒ 锚点必须带上
CheckBox 专属的注释行才唯一（负控脚本的"锚点唯一"这条纪律又一次用上）。

## 5. 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2307 / 315 / 817 / 8`（合计 **3713**）
＝ **3713 通过 / 0 失败**（阶段 218 为 3697，**＋16** ＝ 新面 16 条）；
`HmiEditor.Core.Tests` **2291 → 2307**；`UnusedBaseline` **不变 1109**。

## 6. 仍未做

1. ~~**`#96` 的"统一可见性"那半**：面板的「水平对齐」与「字号」两行**对所有控件类型**都显示
   （`PropertyPanelView.Parameters.cs` 的类型分发表里根本没有它们）⇒ 形状类控件上改了也没用。
   计划：把两行的可见性收敛到"文本类控件"（Label/Button/CheckBox/TextArea/Dropdown），
   并与 `#97`（字号的内置近似映射）**同一阶段**做 —— 那时字号对文本控件才有确定语义。~~
   ✅ **（2026-10-01，阶段 220 已完成 ⇒ 见 §7.2／§7.3）**：三行只对文本类控件显示，
   判据 `PropertyPanelTextRowsVisibilityTests`（1 条，5 类可见 / 5 类隐藏）。
   ⚠ 由此留下的**文案账**：那两格的悬停说明还写着旧事实（"只在 Dropdown 上写对齐" /
   "否则沿用主题默认字体"），**阶段 230** 一并清掉 ⇒ `docs/2026-10-01-export-hint-fidelity.md`。
2. `#97` 的内置字号近似映射（`FontSize` 无自定义字体资产时的导出）。
3. `#98` 的"接线"（`ScreenDpi` ＋ 安全区）。

---

## 7. 阶段 220：字号**内置近似映射**（`#97`）＋ 面板**统一可见性**（`#96` 的另一半）

### 7.1 `#97`：没有字体资产时，字号不再被静默丢掉

**此前**：`GetControlFontExpression` 在没有字体资产时返回 null（"用主题默认字体"）⇒ 面板把字号
从 14 调到 20，**预览变大、导出没变**（`#97`）。

**现在**：新增 `BuiltInFontSizeFor(control, theme)`（`CodeGenerator.Mappings.cs`）——
没有字体族、且字号与主题默认（14）**不同**时，就近取 LVGL 内置档：

| 请求字号 | 发射 | 说明 |
|---|---|---|
| ≤ 13（比主题默认小） | `#if LV_FONT_MONTSERRAT_12` ＋ `&lv_font_montserrat_12` | 内置集合是 **{12, 14, 16}**（`SimulatorProjectExporter.Configs.cs` 的 lv_conf 只启用这三档） |
| = 14（主题默认） | **不发射**（原样） | 沿用主题字体 |
| ≥ 15（比主题默认大） | `#if LV_FONT_MONTSERRAT_16` ＋ `&lv_font_montserrat_16` | 更细的字号落在 12/16 上（近似，不是精确） |
| 有字体族/资产 | 走原路：`#if LV_USE_FREETYPE` ＋ `&font_<id>_<size>` | **不会**被内置顶掉 |

⚠ 用 `#if LV_FONT_MONTSERRAT_NN` **包住**发射：设备端 lv_conf 没启用那一档时整段编译消失
（不会因为缺字号而编译失败）——与既有的 `#if LV_USE_FREETYPE` 同一手法。

### 7.2 `#96` 的另一半：面板**统一可见性**

「字体族 / 字号 / 水平对齐」三行此前**对所有控件类型都显示**（类型分发表里根本没有它们），
在形状/Led/Slider 这类**没有文本**的控件上改了既不进导出、预览也看不出效果 ⇒ 用户会以为"设置丢了"。
现在按**文本类控件**显示：**Label / Button / CheckBox / TextArea / Dropdown**（与生成器的文本块
＋ `EmitDropdownParameters` 一致），其余类型隐藏（标签与输入框同显同隐）。
为此给三个标签补了名字：`LblFontFamily` / `LblFontSize` / `LblTextAlign`。

### 7.3 判据（+8 条）

- `TextEmissionFaceTests` 追加 **7 条**：内置近似表（6/13 → 12；15/72 → 16）· 逐字版本
  （`#if LV_FONT_MONTSERRAT_16` ＋ `&lv_font_montserrat_16` ＋ `#endif`）· 主题默认（14）**不发射** ·
  有资产时走 FreeType 路且**不含** `montserrat`。
- 新面 `PropertyPanelTextRowsVisibilityTests`（**1 条**，App.UiTests）：5 种文本类控件 ⇒ 三行可见；
  Rectangle/Slider/Led/Image/Gauge ⇒ 三行隐藏（标签与输入框都查）。⚠ 用 `DataRootSandbox`
  ⇒ 必须挂 `[Collection("Language-global")]`（门禁里那条纪律当场红过一次，已补）。

### 7.4 负控（5 支，`.workbuddy/tmp/s220-nc.py`）

内置字号恒取 16（丢掉"小取 12"）· 去掉条件编译包裹 · 去掉"与主题一致就不发射"的早退 ·
**拆掉"有字体族就不兜底"的两道守卫** · 面板把文本类也隐藏。

> 🩸 nc4 前两版都**没咬**：这一条由**两道守卫共同保护** —— 调用侧是 `else if`（有资产时 `fontExpr`
> 非空、根本不进这条分支），`BuiltInFontSizeFor` 自己也对"有字体族"早退。**单拆任一道都不红**，
> 两道一起拆才咬得住（读数 `生成代码中不应出现（忽略排版）: montserrat`）。
> ⇒ 负控不只要求"注入能区分"，还要求"**拆到真正的守卫**"。

### 7.5 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2314 / 315 / 818 / 8`（合计 **3721**）
＝ **3721 通过 / 0 失败**（阶段 219 为 3713，**＋8**）；`HmiEditor.Core.Tests` **2307 → 2314**；
`App.UiTests` **817 → 818**；`UnusedBaseline` **不变 1109**。

### 7.6 仍未做

1. 内置字号的**精度**：只有 12/14/16 三档 ⇒ 20 会落成 16（近似）；要更准得让设备端 lv_conf 多启用几档
   （属**固件侧**的事，本仓改不了）。
2. `#98` 的**接线**（`ScreenDpi` ＋ 安全区）—— 用户已选"接线"，下一步做。
3. 面板可见性名单是**硬编码**的五个类型；若 `HmiControlType` 加了新的文本类控件，需要同步
   （`TextEmissionFaceTests.Face_CoversTheTextBearingTypes` 的名单自检是提醒，但不会自动红）。
