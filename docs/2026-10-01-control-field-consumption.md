# 控件属性（面板字段）的**导出消费账**（阶段 203）
## —— 一次「差点重复记账」的复盘 ＋ 9 处零输出断言补齐 ＋ 修 `#93`

> 阶段 202 建了「视觉状态字段的消费账」。这一阶段想把同一套做法推到**属性面板的全部控件字段**
> （`SaveProperties` 写回的 **58 个** `HmiControl` 字段）。**先查账，发现这一面早就建过了** ——
> 本文先记这次复盘（它比新增的判据更值钱），再记真正补上的东西。

---

## 1. 🩸 复盘：我差点把一张已有的账重记一遍

### 1.1 两条**都不可靠**的静态测量

| 做法 | 结果 | 为什么错 |
|---|---|---|
| 按字段名 grep `src/HmiEditor.CodeGen/` | 58 个字段里只有 **`RotationDirection`** 零命中 | **假阴**：`Rotation`/`RotationDirection` 是经 `HmiControl.EffectiveRotation()` **间接**消费的，名字根本不出现 |
| 按 LVGL API 名 grep `tests/` | `lv_bar_set_range` / `lv_gauge` / `lv_tabview_set_tab_text` / `lv_img_set_zoom` / `lv_obj_set_style_transform_angle` 都是 **0 个测试文件** | **口径错**：那些 API 名没出现，**不等于**那一族没有判据（`ControlEmitterCoverageTests` 用的是别的调用名/别的写法断言的） |

⇒ 两条教训：**① 名字级 grep 既有假阳也有假阴**（注释里的名字、间接消费）；
**② "某个 API 名在测试里 0 命中"不能推出"这一族零判据"** —— 必须先看**已有的面**。

### 1.2 已有的面：`ControlEmitterCoverageTests`（631 行）

它建面时用的方法跟我正准备用的一模一样 —— 它自己的类文档写着：

> 「用**「API 锚点」法**（从每个顶层成员体内抽 `lv_*`/`hmi_*`/`LV_*` 名，再看测试语料里是否出现）扫出：
> `lv_label_set_long_mode`、`lv_spinner_set_anim_params`、`lv_tabview_*`、`lv_dropdown_set_selected`、
> `lv_bar_set_mode`、`lv_table_set_row_count`、`lv_arc_set_range` … 在测试里**一次都没出现**。」

它已经覆盖：Label 长文本 · Spinner · TabView · Dropdown · Image 版本兜底 · Bar/Slider 的 `ValueMode` ·
Table 行列数 · Arc 角度/退化区间 · TextArea · 通用字号/字重 · 自适应布局 · 可见性/不透明度 · 指示器颜色。

**处置：停手。** 删掉我刚写的新文件（其中 8 条与它重复），只把**它确实没覆盖**的补进去。
判定口径写死成一句可复算的话：**在那个文件里 grep 目标 API 名，0 命中才算"零输出断言"**。

## 2. 补上的 9 处（＋ 1 处修正），共 16 条判据

| 补的发射路径 | 为什么此前是零断言 | 判据读数（示例） |
|---|---|---|
| `lv_slider_set_range` | 该文件只测了 `lv_slider_set_mode` | `lv_slider_set_range(obj_sl1, 10, 200);` |
| `lv_bar_set_range` | 同上 | `lv_bar_set_range(obj_b1, 0, 100);` |
| **Bar 的游标对象** | LVGL 的 `lv_bar` 没有原生游标，本仓自建圆点 + `LV_EVENT_VALUE_CHANGED` 回调 | `_cur_b1 = lv_obj_create(obj_b1);` |
| `lv_led_set_brightness` | 整个 Led 族没进这个文件 | `lv_led_set_brightness(obj_l1, 7);` |
| **Gauge 的主刻度** | LVGL 无弧刻度 API ⇒ 以注释**声明**段数（`Math.Max(2, …)` 钳制） | `// Gauge 主刻度: 6 段` |
| Knob 的可见 knob | Knob 与 Gauge 共用弧参数，但分道在 `LV_PART_KNOB` | `lv_obj_set_style_opa(obj_k1, LV_OPA_COVER, LV_PART_KNOB);` |
| **`lv_keyboard_set_mode`** | 🩸 此前**只发一行注释**（见 §3 的 `#93`） | `lv_keyboard_set_mode(obj_kb1, LV_KEYBOARD_MODE_NUMBER);` |
| `lv_obj_set_style_line_rounded` | Line 族没进这个文件；`Square` 以注释声明 | `lv_obj_set_style_line_rounded(obj_ln1, true, 0);` |
| Polyline 顶点数组 | 同上（`lv_line_set_points` 在本文件 0 命中） | `lv_line_set_points(obj_pl1, _pts_pl1, 3);` |
| Table 列宽/行高 | 该文件只测了行列数 | `lv_table_set_column_width(obj_tb1, 1, LV_COORD_MAX);`（`auto` ⇒ 自适应）· `lv_obj_set_style_min_height(obj_tb1, 24, LV_PART_ITEMS);` |

外加两处**反面见证**（不设就不该发射）：`Bar.ShowPointer` 未设 ⇒ 无游标对象；`RowHeight = 0` ⇒ 无行高样式；
`KeyboardMode` 为空 ⇒ 无 `set_mode` 调用。

## 3. 修 `#93`：Keyboard 控件的「键盘模式」面板能设、导出不生效

`EmitKeyboardParameters` 此前是：

```csharp
if (!string.IsNullOrEmpty(control.KeyboardMode))
    Cmt(sb, $"    // Keyboard mode: {control.KeyboardMode}");   // ← 只有注释
```

而面板给"键盘模式"提供 `Default` / `Number` / `Special` 三档；`lv_keyboard_create` 之后**没有任何**
`lv_keyboard_set_mode` ⇒ **用户改了不生效**（LVGL 默认 `TEXT_LOWER`）。
这与 `#92`（垂直对齐）**不同**：`#92` 是结构上表达不了；这一格 LVGL **有** API，本仓模拟器侧
（`SimulatorProjectExporter.MainC.cs`）也早在用那两个宏 ⇒ **能表达，缺的只是发射**。

修法（`CodeGenerator.ControlParameters.cs`）：

```csharp
var mode = control.KeyboardMode.Trim().ToLowerInvariant() switch
{
    "number"  => "LV_KEYBOARD_MODE_NUMBER",
    "special" => "LV_KEYBOARD_MODE_SPECIAL",
    _         => "LV_KEYBOARD_MODE_TEXT_LOWER",   // Default / Text / 未知 ⇒ LVGL 的默认模式
};
sb.AppendLine($"    lv_keyboard_set_mode({o}, {mode});   /* Keyboard mode: {control.KeyboardMode} */");
```

判据：4 条 `[InlineData]`（Number / Special / Default / 空）+ 无模式时不发射。

## 4. 顺带的一条**文档漂移**勘误（判据照不到的那一类）

`AppendTransform` 的文档注释此前写着：

> 「镜像：LVGL 9 对象级 `transform_scale_x/y` 负值（镜像）**存在绘制区域缺陷**（实验确认整个对象不渲染，
> layer 变换路径与 matrix 路径均受影响），**故输出注释声明**，固件侧需在渲染矩阵层实现。」

—— 但**代码从来没有那样做过**：它一直照发 `lv_obj_set_style_transform_scale_x/y(..., -256, 0)`，
而且 `TransformFeatureTests` 有**三条判据**（`Gen_MirrorX_EmitsScaleXNegative` /
`Gen_MirrorY_…` / `Gen_MirrorBoth_…`）钉的正是"真发射"。⇒ **注释过时**，以代码与判据为准，已改。

**这条值得单独记一笔**：它是一条**判据永远照不到**的不一致（注释不参与行为）——
只有"顺着一条面把每个发射路径读一遍"这种人工核对才会撞上。这也解释了它为什么能活到今天。

## 5. 负控（6 支，`.workbuddy/tmp/s203-nc.py`）

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | 回退 `#93`（键盘模式回到只发注释） | `Keyboard_Mode_…` | `生成代码中未找到调用: lv_keyboard_set_mode(obj_kb1, LV_KEYBOARD_MODE_SPECIAL);` |
| nc2 | 键盘模式映射写错（`Number` 也映射成 `TEXT_LOWER`） | `Keyboard_Mode_…` | `未找到调用: lv_keyboard_set_mode(obj_kb1, LV_KEYBOARD_MODE_NUMBER);` |
| nc3 | Bar 不再发射 `lv_bar_set_range` | `Bar_RangeAndValue_…` | `未找到调用: lv_bar_set_range(obj_b1, 0, 100);` |
| nc4 | Bar 的游标对象不再发射（`if (false)`） | `Bar_ShowPointer_…` | `未找到（忽略排版）: _cur_b1 = lv_obj_create(obj_b1);` |
| nc5 | Gauge 主刻度不再声明 | `Gauge_ArcRangeAndMajorTicks_…` | `未找到（忽略排版）: // Gauge 主刻度: 2 段` |
| nc6 | Table 行高不再发射 | `Table_ColumnWidthsAndRowHeight_…` | `未找到调用: lv_obj_set_style_min_height(obj_tb1, 24, LV_PART_ITEMS);` |

6 支逐支独立日志（`.workbuddy/tmp/s203-nc{1..6}.log`）、每腿限时、开跑前扫残留注入，
还原后 2 个文件的 SHA256 逐字节相同。

## 6. 读数与仍未做

**读数**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2276 / 315 / 798 / 8`（合计 **3663**）
＝ **3663 通过 / 0 失败**（阶段 202 为 3647，**＋16**）；`HmiEditor.Core.Tests` **2268 → 2284**；
`App.UiTests` **798 / 0**（连跑 37 / 35 s）；`UnusedBaseline` **不变 1109**；`MainWindow.axaml.cs` 仍 **2983**。

**仍未做**：

1. **面板字段 → 导出产物**还没有一张**逐字段**的表（本阶段只补了"零输出断言"的那些发射路径）：
   58 个字段里大多数由**已有判据**看着，但"哪个字段由哪条判据看着"没有一处点名（属结构性账）。
2. **`#92` 的剩余部分**照旧（运行时回调 / 面板标明）。
3. **别的消费方**：本轮只覆盖 `HmiEditor.CodeGen`（**模拟器**侧 `SimulatorProjectExporter` 只被顺带提到）
   —— 面板字段在模拟器里的消费账没建。
