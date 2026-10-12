# 数值型字段的取值包络（阶段 213）
## —— 数字框给的每一段范围，边界上到底发射什么

> 承阶段 212 的**枚举**取值账（`docs/2026-10-01-enumerated-choice-emission.md`）：那一面管
> "下拉框给的每个**取值**"，这一面管"数字框给的每个**边界**"。

---

## 1. 口径：样本必须盖住面板声明的两个端点

每个字段的样本**必须包含** `PropertyPanelView.axaml` 里那一格声明的 `Minimum` 与（若有）`Maximum`
—— 面板改范围、或那一格改名 ⇒ **红**，逼着重新确认"新边界上发射什么"。再对每个样本**真跑一遍生成器**：
登记了片段必须逐字出现，登记 `null`（实测"不发射"）必须不发射（判**调用**，注释不算）。

## 2. 六个字段 / 二十三个阶段（样本行）

| 字段 | 面板那一格 | 样本与口径 |
|---|---|---|
| `Opacity` | `OpacityBox` [0, 100] | `-1 → opa(…, 0, 0)`（越下界钳 0）· `0 → opa(…, 0, 0)` · `50 → opa(…, 127, 0)`（百分比 ×2.55 四舍五入）· **`100 → 不发射`**（＝ LVGL 默认全不透明）· `101 → 不发射`（越上界静默当 100% ⇒ 这里钉住） |
| `Rotation` | `RotationBox` [0, 360] | `-10 → transform_rotation(…, -100, 0)`（负角照发）· `0 → 不发射` · `45 → 450`（**×10**，0.1° 单位）· `360 → 3600`（**不回绕**）· `361 → 3610`（**不钳位**，LVGL 运行时归一） |
| `Radius` | `PropRadius` [0, –] | `-5 → 不发射` · `0 → 不发射` · `12 → 12` · `999 → 999`（**无上限钳位**，面板也没写 Maximum） |
| `BorderWidth` | `PropBorderWidth` [0, –] | 同 `Radius` 形状（`<= 0` 不发射，无上限钳位） |
| `Brightness` | `BrightnessBox` [0, 255] | `0/128/255` 原样 · **`256 → 256`**（不钳 255，LVGL 运行时钳） |
| `FontSize` | `PropFontSize` [6, 72] | **无自定义字体资产**：`6`/`72` 都**不发射**（走主题默认字体）· **有** `FontFamily` 资产：`&font_myfont_6` / `&font_myfont_72`（尺寸进了**字体变量名**，且整段在 `#if LV_USE_FREETYPE` 里） |

## 3. 🩸 撞出来的 `#97`：`FontSize` 的预览/导出不一致

| 链 | 行为 |
|---|---|
| **面板** | 所有控件都给「字号」这一格（6..72） |
| **预览** | `var fontSz = (control.FontSize > 0 ? control.FontSize : DefaultFontSize) * z;` ⇒ **一律按它缩放** |
| **导出** | `GetControlFontExpression(...)`：**没有**自定义字体资产（`FontFamily` 指向可用字体）时返回 `null` ⇒ **不发射**，实际用主题默认字体 |

⇒ 用户把字号从 14 调到 30：**预览立刻变大、导出没变**。这与 `#96`（`TextAlign` / `ValueDisplayType`）
**同族**，都是"面板/预览/导出三条链不一致"。三个候选处置（**待产品决策**）：

1. **面板提示**：字号在"无自定义字体资产"时灰显 ＋ 悬停说明（照阶段 208 的做法）；
2. **导出补齐**：没有资产时改用 LVGL 内置字号的近似映射（`lv_font_montserrat_<n>`）——
   但内置字号只有有限几档，会有量化误差，需要明确口径；
3. **写死**：文档里明确"字号只在配了自定义字体资产时对导出有效"，面板不动。

## 4. 负控（5 支，`.workbuddy/tmp/s213-nc.py`）

| 支 | 注入 | 读数（失败正文） |
|---|---|---|
| nc1 | 面板把 `OpacityBox` 的 `Maximum` 改成 99 | `Opacity（面板 OpacityBox）：面板上界 99 没有被采样` |
| nc2 | `Opacity=50` 期望片段改成 128 | `Opacity=50（注入：改成 128）：期望 lv_obj_set_style_opa(obj_rc1, 128, 0);，实际没有` |
| nc3 | `Opacity=100` 登记成"应当发射" | `Opacity=100（注入：说它发射）：期望 …255…，实际没有` |
| nc4 | `Brightness=256` 期望改成 255（假设钳位） | `Brightness=256（注入：假设钳位）：期望 lv_led_set_brightness(obj_led1, 255);，实际没有` |
| nc5 | **代码侧**给 `Radius` 加一个上限钳位（`Math.Min(Radius, 100)`） | `Radius=999（**无上限钳位**…）：期望 lv_obj_set_style_radius(obj_rc3, 999, 0);，实际没有` |

🩸 nc1 第一版注入**构建不过**（往已有 `Maximum="100"` 的元素上又加了一个 `Maximum` ⇒ XAML 重复属性）；
改成替换原属性才成立。

## 5. 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2287 / 315 / 817 / 8`（合计 **3693**）
＝ **3693 通过 / 0 失败**（阶段 212 为 3690，**＋3**）；`HmiEditor.Core.Tests` **2284 → 2287**；
`UnusedBaseline` **不变 1109**；**本阶段产品代码 0 行改动**。

## 6. 仍未做

1. 本面覆盖 **6 个字段**（面板 31 个数字框里挑的高风险/有换算的几个）：其余（`ScreenWidth/Height`、
   `SafeLeft/Top/Right/Bottom`、`ScreenDpi`、`ScreenBgImageOpacity`、`PropX/Y/Width/Height`、
   `MinValue/MaxValue/StepValue/ValueScaleBox`、`SelectedIndexBox`、`MaxLengthBox`、`TickCountBox`、
   `RowCountBox/ColCountBox`、`RowHeightBox`、`SpinSpeedBox`、`TankTickCountBox`、`PipeFlowSpeedBox`）
   里，一部分阶段 203 已按各自重点判过，但**没有统一成"端点必采"的账**。
2. `#97` 的三选一待决策（`FontSize` 的预览/导出口径）。
3. `Opacity` 的"越上界静默当 100%"要不要给出可见提示（UI 决策）—— 与 `#95` 同类的"静默回退"问题。

---

## 7. 阶段 214：续做 8 个字段（同一把尺子）

阶段 213 立的口径不变，这一阶段把另一个 8 个字段并进来（**同一张表、同一面**，不新开面）：

| 字段 | 面板那一格 | 样本与口径 |
|---|---|---|
| `X` / `Y` | `PropX` / `PropY` [0, –] | **0 照发**（位置没有"默认即省略"一说）· 大值不钳（4096 照发） |
| `Width` / `Height` | `PropWidth` / `PropHeight` [10, –] | **0 也照发**（比面板下界还小 ⇒ 不钳位）· 面板下界 10 与 4096 都原样 |
| `SelectedIndex` | `SelectedIndexBox` [0, 10000] | 0 照发 · 越界（10000）**不按选项数校验**（由 LVGL 兜） |
| `MaxLength` | `MaxLengthBox` [0, 10000] | **0 ＝ 不限长度 ⇒ 不发射**；正常值/上界原样 |
| `SpinSpeed` | `SpinSpeedBox` [100, 10000] | `lv_spinner_set_anim_params(obj, N, 60)`（转角固定 60） |
| `ScreenBgImageOpacity` | `ScreenBgImageOpacity` [0, 100] | 0 ⇒ `opa(bg_img, 0, 0)` · 50 ⇒ `127`（×2.55 四舍五入）· **100 ⇒ 照发 `255`** |

> 🩸 **顺带钉住一处不对称**：**屏幕背景图** 100% ⇒ **照发 `lv_obj_set_style_opa(bg_img, 255, 0)`**，
> 而**控件** `Opacity` 100% ⇒ **不发射**（阶段 213 那条）。两者都是"100% ＝ 完全不透明"，
> 但一个发一个不发 —— 这是两段独立实现的历史差异，本表把**两种现状都钉住**（谁要统一，改完这里会红）。

样本行数：23 → **41**；字段数：7 条（6 个字段）→ **15 条（14 个字段）**。
负控补 5 支（`.workbuddy/tmp/s214-nc.py`）：面板改 `SpinSpeedBox` 下界（`面板下界 50 没有被采样`）·
`MaxLength=0` 登记成"应当发射" · `ScreenBgImageOpacity=100` 改成"不发射" · **代码侧**把 `lv_obj_set_pos`
改成"X==0 时省略" · `Width=0` 改成"不发射"。

---

## 8. 阶段 216：清尾（13 个字段 ＋ **死设置的新判法**）

### 8.1 进来的 13 个

| 字段 | 面板那一格 | 样本与口径 |
|---|---|---|
| `MinValue` | `MinValueBox` [−1e6, 1e6] | Slider：`set_range(obj, N, 100)`；**不校验 `min<=max`**（倒置照发 ⇒ 钉住） |
| `MaxValue` | `MaxValueBox` [−1e6, 1e6] | 同上（负上界照发） |
| `RowCount` / `ColumnCount` | `RowCountBox` / `ColCountBox` [1,100] | Table：`set_row_count` / `set_column_count` 端点原样 |
| `RowHeight` | `RowHeightBox` [0,10000] | **0 ⇒ 不发射**（用主题默认行高）；10000 ⇒ `style_min_height(…, 10000, LV_PART_ITEMS)` |
| `PipeFlowSpeed` | `PipeFlowSpeedBox` [0,10000] | **0 ⇒ 连注释都不写**；10000 ⇒ `// 流动动画周期: 10000ms（…）`（真动画由运行时创建） |
| `ScreenWidth` / `ScreenHeight` | `ScreenWidth` / `ScreenHeight` [1,4096] | `lv_obj_set_size(screen, W, H)`，**不钳位** |

### 8.2 🩸 死设置：判法不是"没发射某行"，而是**生成物逐字节相同**

五个字段（`ScreenDpi` ＋ `SafeLeft/Top/Right/Bottom`）写进 `project.ScreenDefaultsConfig`，但
**全仓 `src/` 里只有两个设置面板读写它们**：`HmiEditor.CodeGen` 一次都不读，预览画布也不读。
⇒ 用户改了、存了、**既不进导出也不进预览**（登记 `#98`）。

对这类字段，"断言某行不存在"是**空判**（它本来就不会有）。所以本面给它们加了新的判法：
**拿同一个夹具生成两次 —— 一次设该字段、一次不设 —— 要求生成物`逐字节相同`**。
负控 nc4 就是把这个假设打破（把 `SafeLeft` 的钩子改成真的去动屏幕宽）⇒ 判据当场红。

### 8.3 明确**没进**来的 4 个（避免重复记账 / 受样本类型所限）

| 字段 | 为什么没进 |
|---|---|
| `MajorTickCount`（`TickCountBox`）· `TankTickCount`（`TankTickCountBox`） | 它们的消费者在**过程控制/绘制回调**那条 C 代码路径上（screen 代码里连 `tick` 字样都没有）⇒ 按阶段 203 的指针表由 `ProcessControlCodeGenTests` 守着；**再纳进本面既重复又测不到**。 |
| `StepValue`（`StepValueBox`）· `ValueScaleFactor`（`ValueScaleBox`） | 这两格的面板下界是 **0.0001**，而本面的样本类型是 `int` ⇒ 表达不了下界（端点必采那条会立刻红）。要纳进来得把样本类型改成 `double`（**下一阶段的入口**）；量到的口径先记在这里：`StepValue=1` ⇒ 21 个刻度、`StepValue=1e6` ⇒ 2 个刻度（**刻度数有 21 的上限**）；`ValueScaleFactor` 在 screen 代码里只写一行 `// Value display: displayType=Auto, scale=X, decimals=2`（真正的换算在**值刷新**代码里）。 |

### 8.4 负控（5 支，`.workbuddy/tmp/s216-nc.py`）

面板改 `MinValueBox` 下界（`面板下界 -999999 没有被采样`）· `RowHeight=0` 登记成"应当发射" ·
`MinValue=1000000` 期望改成"会规范化顺序" · **把死设置 `SafeLeft` 的钩子改成真的动屏幕宽**
（`SafeLeft=0：本表说它没人消费，但生成物变了 —— 死设置被接线了？`）· `PipeFlowSpeed=0` 登记成"应当发射"。

### 8.5 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 315 / 2290 / 817 / 8`（合计 **3696**）
＝ **3696 通过 / 0 失败**（判据**条数不变**：仍是同一个面，样本行 41 → **65**、字段 14 → **29**）；
`UnusedBaseline` **不变 1109**；**产品代码 0 行改动**。

---

## 9. 阶段 217：样本类型 `int → double` ⇒ **数值面清空**（再没有字段走后门）

### 9.1 改了什么

- `Sample.Value` / `Field.Apply` / `ScreenHook` 从 `int` 改成 **`double`**（模型里这些字段本来就
  大多是 `double`：`MinValue`/`MaxValue`/`StepValue`/`ValueScaleFactor`/`RowHeight`/`Opacity`/
  `Rotation`/`X/Y/Width/Height`/`Screen.Width/Height`/`DpiScale`/`SafeAreaPadding.*`；少数 `int`
  的（`Radius`/`BorderWidth`/`FontSize`/`SelectedIndex`/`MaxLength`/`SpinCount`/`Brightness`/
  `PipeFlowSpeed`/`RowCount`/`ColumnCount`/`BgImageOpacity`）在 lambda 里显式 `(int)v`）。
- 面板范围解析改成 **`double.Parse(InvariantCulture)`**，端点比较用 double
  ⇒ `Minimum="0.0001"` / `Minimum="0.5"` 这类**十进制端点**现在**真的被要求**。
- **删掉了端点的"后门"**：阶段 216 给死设置开的 `if (f.Dead) continue;` 取消 —— 现在**每个字段**
  （含死设置 `ScreenDpi` 的 0.5）都必须采到面板声明的两个端点。
- 把阶段 216 因类型受限而推迟的两个字段收回来：`StepValue`（0.0001 / 1 / 1000000）与
  `ValueScaleFactor`（0.0001 / 2 / 1000000）。

### 9.2 新口径（量出来的）

| 字段 | 取值 | 发射 |
|---|---|---|
| `StepValue`（Slider ＋ ShowTicks，范围 0..100） | **0.0001** | 21 个刻度，**标签全是 `"0"`**（步长取整成 0）—— 刻度数有 **21 的上限** |
| 同上 | 1 | 21 个刻度（0..20） |
| 同上 | 1000000 | **只有 2 个刻度**（0 与 1000000） |
| `ValueScaleFactor`（Slider ＋ 变量绑定） | 0.0001 / 2 / 1000000 | screen 代码只写一行 `// Value display: displayType=Auto, scale=X, decimals=2`（**真换算在值刷新路径**） |

### 9.3 新增闭合判据：**面板里每个具名数字框都必须有出口**

`EveryPanelNumericBox_HasAnOutlet`：扫 `PropertyPanelView.axaml` 里所有具名 `fa:FANumberBox`/
`NumericUpDown`（阶段 217 实测 **31 个**），要求每个要么在本面表里，要么在 `Elsewhere` 登记
（目前只有两个：`TickCountBox` / `TankTickCountBox` ⇒ Gauge/Tank 的刻度数由
`ProcessControlCodeGenTests` 在**绘制回调路径**上守着，原因写在登记里）。
新增一个数字框、或改掉某一格的名字 ⇒ 红。

### 9.4 负控（6 支，`.workbuddy/tmp/s217-nc.py`）

`StepValueBox` 下界改 0.001（`面板下界 0.001 没有被采样`）· `ScreenDpi` 下界改 0.6（死设置也不例外）·
`StepValue=0.0001` 期望片段改错 · `ValueScaleFactor=0.0001` 期望改成 `scale=1` ·
`StepValue=1000000` 期望改成"21 个刻度" · **从本面删掉一个数字框也不登记出口**
（`这些面板数字框既不在本面、也没登记出口：ScreenHeight`）。

> 🩸 **nc3 第一版没咬住**：我把期望从 `_ticklbl_sl11_20, "0"` 改成 `_ticklbl_sl11_19, "0"` ——
> 结果**测试照样通过**，因为 21 个刻度里 `_19` 也存在、也标 `"0"`。这暴露出"期望片段必须**能区分**
> 该样本"这条要求：改成 `_ticklbl_sl11_20, "1"`（0.0001 时标签全是 "0"）才咬住。
> ⇒ 负控的注入值本身也要**挑能区分的那一个**，否则"绿"什么都证明不了。

### 9.5 读数

**门禁**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **943** —— 又一次
`gate-readings.py` 文档里写明的**结构性**挤行：标记行 独立 7 / 双份 1，合计仍是 9）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2291 / 315 / 817 / 8`（合计 **3697**）
＝ **3697 通过 / 0 失败**（阶段 216 为 3696，**＋1** ＝ 新加的闭合判据）；
`HmiEditor.Core.Tests` **2290 → 2291**；`UnusedBaseline` **不变 1109**；**产品代码 0 行改动**。

### 9.6 数值面到此清空（仍未做的东西）

- 面板 **31 个具名数字框**：29 个在本面（含 5 个死设置），2 个登记为"归过程控制面"
  （`TickCountBox`/`TankTickCountBox`，已有判据）⇒ **一个不落**。
- **仍未做**：本面只看**单字段**样本；字段**组合**（例如 `MinValue>MaxValue` ＋ `StepValue` 同时越界）
  没有成对覆盖 —— 那是下一层的账（要不要做属价值判断）。
