# 枚举型选择字段的取值账（阶段 212）
## —— 「面板给出的每个取值都必须有确定的导出语义」

> 承阶段 211 的逐字段索引（`docs/2026-10-01-control-field-judge-index.md`）：那张表回答
> "**哪个字段**有判据看着"；这一张回答"**每个取值**呢"。

---

## 1. 为什么单独一张账

这类字段的坏法都是**静默**的：面板多给一个选项、或者枚举加一个成员，生成器那边落进 `default`
分支 —— 用户改了半天导出没变，而**没有任何判据会红**。阶段 203 就抓到过一次：
`KeyboardMode` 的 `Default` 被发成 `LV_KEYBOARD_MODE_TEXT_UPPER`（语义错位）。

所以这一面做两件事：

1. **双向闭合** —— 把"面板真的会给出的取值集合"与"本表登记的取值"对齐：
   - 字符串型（`LineCap` / `KeyboardMode` / `StretchMode`）：从**面板 XAML** 里读那个 `ComboBox` 的
     `ComboBoxItem Tag`（用户能点的就是这些）；
   - 枚举型（`TextAlign` / `TextVAlign` / `ValueDisplayType`）：读**枚举成员**。
   多一个/少一个 ⇒ 红。
2. **每个取值真跑一遍生成器** —— 登记的期望片段必须**逐字出现**；登记为 `null`（本阶段实测"不发射"）
   则必须**不发射**。判"不发射"用 `CSource.ParseCalls`（跳过注释），所以
   `LineCap` 那行 `// Line cap: Butt (LVGL 仅支持 Round/平头，Square 以平头表达)` 的注释不会误判。

## 2. 六个字段 / 十八个取值

| 字段 | 取值来源 | 取值 | 发射口径 |
|---|---|---|---|
| `LineCap` | `LineCapCombo`（XAML） | Butt / Round / Square | Round ⇒ `lv_obj_set_style_line_rounded(obj, true, 0)`；**Butt 与 Square 都落"平头"** —— LVGL 没有方形线帽，生成器**显式写注释**说明 ⇒ 这是**已声明**的口径，不是缺口 |
| `KeyboardMode` | `KeyboardModeCombo`（XAML） | Default / Number / Special | `LV_KEYBOARD_MODE_TEXT_LOWER` / `_NUMBER` / `_SPECIAL` |
| `StretchMode` | `StretchCombo`（XAML） | None / Fill / Contain | `LV_IMAGE_ALIGN_CENTER` / `_STRETCH` / `_CONTAIN`（Contain 带 LVGL < 9.3 回退行） |
| `TextAlign` | 枚举 `TextAlignMode` | Left / Center / Right | 🩸 **Label 上三个都不发射**（见 §3） |
| `TextVAlign` | 枚举 `TextVAlignMode` | Top / Middle / Bottom | Top ⇒ `lv_obj_set_y(obj, 20)`；Bottom ⇒ 按内容高度重定位 `lv_obj_set_y(obj, 20 + 40 - _h)`；Middle ⇒ LVGL 默认，不发射 |
| `ValueDisplayType` | 枚举 `ValueDisplayType` | Auto / Bool / Int / Float / String | 🩸 **无变量绑定的 Label 上都不发射**（由值刷新路径消费） |

## 3. 🩸 撞出来的 `#96`：同一个字段，三条链三种口径

| 链 | `TextAlign` | `ValueDisplayType` |
|---|---|---|
| **面板** | 对**所有**控件都显示「水平对齐」这一行（`PropertyPanelView.axaml` 第 8 行） | 有专门的 `ValueDisplayTypeCombo` |
| **预览** | 只在 **CheckBox 文本**路径读它（`DesignCanvas.Renderers.cs` 的 `DrawAlignedText(..., control.TextAlign, control.TextVAlign)`） | 预览不显示值格式 |
| **导出** | 只在 **Dropdown**（`EmitDropdownParameters`）与**视觉状态**的 `lv_obj_set_align` 里读它 | 只在**值刷新**路径读它 |

⇒ 在 Label 上改这两个字段：**预览/面板看着生效，导出没有**。

**处置**：本阶段把现状**钉进取值账**（这两个字段的取值全部登记为 `Fragment = null` ＋ 理由），
所以：① 现状不再"没人知道"；② 真要改口径（下表三选一），改完这张表**会红**，逼着更新。
三选一（**待产品决策**）：

1. **导出补齐**：给文本类控件补 `lv_obj_set_style_text_align(obj, LV_TEXT_ALIGN_*, 0)`；
2. **统一可见性**：预览与导出都收敛到"只对真正支持的控件显示该行"；
3. **写死＋提示**：明确"这两行只对 X 类型有意义"，并在面板上灰显 ＋ 悬停说明（照阶段 208 的做法）。

## 4. 负控（5 支，`.workbuddy/tmp/s212-nc.py`）

| 支 | 注入 | 读数（失败正文） |
|---|---|---|
| nc1 | 面板给 `LineCapCombo` 加一个选项 | `LineCap：面板/枚举给了取值 Extra，本表没登记它的导出语义` |
| nc2 | 面板给 `LineCapCombo` 删一个选项 | `LineCap：本表登记了 Round，但面板/枚举已经不给这个取值了` |
| nc3 | 表里把 `KeyboardMode=Default` 的期望片段改错 | `KeyboardMode=Default：期望发射 …TEXT_UPPER…，实际没有` |
| nc4 | 表里把 `LineCap=Round` 改成"不发射" | `LineCap=Round：本表说不发射，但代码里出现了 lv_obj_set_style_line_rounded` |
| nc5 | 表里把 `TextAlign=Left` 登记成"应当发射" | `TextAlign=Left：期望发射 lv_obj_set_style_text_align(obj_lb1, LV_TEXT_ALIGN_LEFT, 0);，实际没有` |

🩸 nc1 第一次注入**锚点写错**（我按本地化键猜了 `ComboBoxItem` 的 `Content`，实际是字面量
`Content="Round"`）—— 先 `grep` 真实文本再写锚点这条纪律又一次用上。

## 5. 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2284 / 315 / 817 / 8`（合计 **3690**）
＝ **3690 通过 / 0 失败**（阶段 211 为 3687，**＋3**）；`HmiEditor.Core.Tests` **2281 → 2284**；
`UnusedBaseline` **不变 1109**；**本阶段产品代码 0 行改动**。

## 6. 仍未做

1. 本面只覆盖**枚举型**选择字段（6 个）；**数值型**字段的**取值包络**（钳位/边界/零值）仍散在
   `ControlEmitterCoverageTests` 的各条判据里，没有统一成"每个合法取值一行"的账。
2. `#96` 的三选一待决策（面板/预览/导出口径统一）。
3. `TextVAlign=Middle` 的"不发射"是**对的**（LVGL 默认），但它与"面板给了 3 个选项"之间
   同样存在第 2 条口径问题的一个**良性**版本：中值不是"没实现"，而是"默认即中"——本表用
   `Fragment = null` ＋ 表里口径说明区分了这两种情况。
