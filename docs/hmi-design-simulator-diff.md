# HMI 设计页面 vs 模拟器实际运行页面显示不一致 — 根因诊断

**诊断日期**：2026-08-13
**诊断对象**：HMI Editor 设计画布（DesignCanvasView）与 PC 模拟器（hmi_sim / LVGL+SDL）实际运行画面
**相关文件**：
- 设计画布渲染：`src/HmiEditor.App/Controls/DesignCanvas.cs`
- 模拟器生成：`src/HmiEditor.CodeGen/CodeGenerator.cs`、`src/HmiEditor.Core/Services/SimulatorProjectExporter.cs`
- 控件映射：`src/HmiEditor.Core/Mapping/ControlMappingRegistry.cs`
- 预览值解析：`src/HmiEditor.Core/Design/PreviewValueResolver.cs`
- 自适应布局：`src/HmiEditor.Core/Services/AdaptiveLayoutCalculator.cs`
- 模拟器编译入口：`src/HmiEditor.App/Services/SimulatorBuildService.cs`、`src/HmiEditor.App/Views/BuildRunDialog.axaml.cs`

---

## 一、两条渲染链路全景

| | **设计页面（DesignCanvasView）** | **模拟器（hmi_sim）** |
|---|---|---|
| 渲染引擎 | Avalonia + Skia 自绘（`DesignCanvas.Render`） | LVGL 真实控件树（`CodeGenerator` 生成 → cmake 编译 → SDL 运行） |
| 数据源 | `HmiControl` 模型 + `PreviewValueResolver`/`VariableSimulator`（预览模式） | 同一 `HmiControl` 模型 → C 代码静态值 |
| 链接点 | 无 —— 两者各自独立解释模型 | 唯一共享输入是 CodeGenerator 生成的 `generated_hmi.c` |

**两条链路没有共享渲染契约**，这是所有差异的总根源。

---

## 二、逐维度差异

### 2.1 布局结构

- 设计画布：绝对坐标 × `ZoomLevel`，屏幕边界取自 `screen.Width/Height`（`DesignCanvas.cs` Render 方法，`screenRect = new Rect(0, 0, screen.Width * ZoomLevel, screen.Height * ZoomLevel)`）。
- LVGL：`lv_obj_set_pos/size` 同坐标语义，但**无缩放/裁剪概念**，窗口尺寸 = bootScreen 尺寸（`SimulatorProjectExporter.BuildMainC`：`lv_sdl_window_create(bootScreen.Width * cfg.scale, bootScreen.Height * cfg.scale)`）。

**结论**：坐标一致，但视觉比例受字体/边距差异放大，文字溢出时两者表现不同（Avalonia 不换行绘制，LVGL label 默认会 wrap）。

### 2.2 样式渲染（最大差异群）

| 属性 | 设计画布 | LVGL 生成代码 | 偏差 |
|---|---|---|---|
| **字号** | 固定 `12 * z`（文本统一 12px） | **完全不生成字号**，仅屏幕级 `lv_obj_set_style_text_font(scr, ...)` | **12px vs 主题默认 16px，文字明显更大** |
| **屏幕背景图** | 真实绘制 Tile/Center/Stretch + 透明度（`DrawScreenBackgroundImage`） | 只输出**注释** `// Screen background image: ... (resolve at build time)` | **模拟器背景图完全不显示** |
| 文本颜色 | 通用分支强制白色；Button 按 TextColor | 未设置 TextColor 时继承主题色 | 配色不同 |
| 字体 | `Typeface.Default`（系统 Noto Sans CJK） | 项目字体资产 / `LV_FONT_DEFAULT`（simsun_16_cjk） | 字形、字距不同 |
| 圆角/边框 | `Radius × z`、`BorderWidth × z` | 相同属性直译 | 基本一致 |
| **Button 子文本对齐** | 9 宫格对齐 + 4px padding | 仅在非默认对齐时生成 align | 默认居中基本一致，边缘对齐时 padding 语义不同 |

### 2.3 组件状态

- **编辑器预览模式**：`IsPreviewMode=true` 时由 `PreviewValueResolver` 驱动 —— LED 亮/灭、Gauge 弧线角度、Slider 进度都随绑定值动态变化。
- **模拟器**：`lv_slider_set_value(obj, control.Value, LV_ANIM_OFF)`、`lv_led_set_brightness` 等全部写入**静态值**。
- **控件形态映射错位**：
  - Gauge/Knob → `lv_arc`（`ControlMappingRegistry`）：设计画布是"270° 弧 + 中心数值文本"，LVGL arc 是圆环，**无中心文本、无仪表刻度**。
  - **Table 双段生成冲突**：`GenerateControlParameters` 正确输出 `lv_table_set_row_count/HeaderTexts`，但循环尾部遗留 L1 硬编码段又输出 `lv_table_set_row_cnt(5)` + `Col1/Col2/Col3` 覆盖。且 `row_cnt` 不在 `LvglApiCompatChecker` 替换表内 —— **LVGL 9 下要么编译失败，要么表头被 Col1/Col2/Col3 覆盖**。

### 2.4 数据绑定（核心缺陷）

- 编辑器预览：`MainWindow` 注入 `VariableSimulator`/Modbus 引擎 → `PreviewValueProvider`，Label 实时显示 `"27.50"`、Gauge 指针随值转动。
- 模拟器：`GenerateScreenCode` 对绑定只输出注释 `// Bound variable: xxx`；`BuildMainC` 的主循环只有 `lv_timer_handler()`，**没有任何标签更新逻辑**。

**结论：预览模式看到的动态值在模拟器里是死的静态文本**（甚至只有 `control.Text` 静态字符串）。

### 2.5 响应式断点 / 设备参数

- 编辑器：支持 `PreviewResolution` + `AdaptiveLayoutCalculator`（Fit/Fill letterbox 变换），且每次预览都重算。
- 模拟器：`adaptive_layout_<id>()` 用 `lv_display_get_*_resolution` 计算，而窗口尺寸恰等于 bootScreen 尺寸 → `sx=sy=1`，**自适应布局在模拟器中恒为无效**。

**结论：同一模型在两者中的"目标分辨率"语义不同，断点行为不可能一致。**

### 2.6 环境配置

- 图片路径：已修复为相对路径 `A:assets/<file>`，但依赖 FS_STDIO 驱动；设计画布从本地 `DockLayoutState.AssetLibrary` 直接读位图。**文件缺失时模拟器显示空白、画布显示占位**。
- 字体资源：模拟器通过 FreeType 加载（需 `LV_USE_FREETYPE` + 磁盘 TTF）；画布用系统字体。**字体文件缺失时模拟器回退 simsun 16 CJK，画布无感**。

---

## 三、诊断结论（按优先级）

1. **【最高】数据绑定断链**：绑定值只在编辑器预览模式渲染，模拟器生成的 C 代码无任何运行时刷新机制。动态效果"所见"与"所得"本质不同。
2. **【高】样式属性透传缺口**：`FontSize` 完全未生成、屏幕背景图仅注释不实现 —— 这两项直接导致模拟器画面与画布在文字大小、背景外观上显著不同。
3. **【高】控件映射失真**：Gauge/Knob 映射为 `lv_arc`（形态不同）、Table 存在硬编码覆盖 + 非法 API `lv_table_set_row_cnt`。
4. **【中】状态语义不一致**：预览模式用模拟值驱动（LED 亮灭、进度条），模拟器用静态 `control.Value`。
5. **【中】自适应断点语义漂移**：编辑器按 PreviewResolution 变换，模拟器窗口=设计分辨率导致缩放恒为 1。
6. **【低】渲染引擎/字体差异**：Skia 自绘近似 vs LVGL 真实控件树、系统字体 vs FreeType 字体，带来字形与边距细节偏差。

---

## 四、修复建议（按优先级）

1. **统一单一事实源**：为 `HmiControl` → LVGL 建立"完整属性透传表"，补上 `FontSize`（`lv_obj_set_style_text_font` + 字号换算）、屏幕背景图（`lv_image_set_src` + 布局模式）。
2. **打通模拟器数据刷新**：在 `BuildMainC` 主循环按 `RuntimeTagsCodeGen` 生成的 tag 表定时 `lv_label_set_text_fmt` / `lv_slider_set_value`（对齐预览模式语义）。
3. **修复 Table 双段冲突**：删除 `GenerateScreenCode` 循环尾部的遗留硬编码段（`lv_table_set_row_cnt(5)` + `Col1/Col2/Col3`）；把 `lv_table_set_row_cnt` 加入 `LvglApiCompatChecker` 替换表。
4. **统一自适应语义**：给 `adaptive_layout_<id>` 传入与编辑器一致的"目标分辨率"（模拟器窗口可设为目标分辨率而非 bootScreen 尺寸）。
5. **Gauge/Knob 专用生成器**：仿照 `ProcessControlCodeGen` 用 `lv_canvas` 绘制仪表盘，替代裸 `lv_arc`。

---

## 五、T24 修复记录（2026-08-13 晚间）

用户反馈「设计器与模拟器仍不一样、图片自动拉伸还有问题」，系统审计 DesignCanvas 渲染 vs CodeGenerator 生成后修复 5 个对接缺口：

| # | 缺口 | 设计器行为 | 模拟器修复前 | 修复 |
|---|------|-----------|-------------|------|
| 1 | **图片 StretchMode 未消费** | `TryDrawImage` 恒用 `DrawImage(bitmap, rect)`（Fill 拉伸） | 已正确映射 `LV_IMAGE_ALIGN_*` | `DesignCanvas.TryDrawImage` 增加 `stretchMode` 参数：Contain→等比居中、Center/None→原尺寸居中、Tile→平铺、Fill/null→拉伸（与 LVGL 语义一致） |
| 2 | **控件 BorderColor 未生成** | 边框色渲染 | 只生成 `border_width`，无 `border_color` | `GenerateControlParameters` 边框块补 `lv_obj_set_style_border_color`（有配置才生成） |
| 3 | **Line 控件完全不可见** | 左上→右下对角线，颜色=BgColor(默认白)，宽=BorderWidth(默认2) | 只 `lv_line_create` + pos/size，`lv_line` 默认 line_width=0 → 不可见 | 生成 `static lv_point_precise_t[]` + `lv_line_set_points` + `lv_obj_set_style_line_color` + `lv_obj_set_style_line_width` |
| 4 | **LineCap 未对接** | PenLineCap.Round/Square | 仅注释 | Round → `lv_obj_set_style_line_rounded(o, true, 0)`（注意 API 是 `line_rounded` 非 `line_round`） |
| 5 | **屏幕背景色被主题覆盖** | 用户设置 BgColor 生效 | `apply_theme_styles()` 无条件用主题 BackgroundColor 覆盖 | `GenerateThemeCode` 移除 bg_color 设置，背景色职责完全交给各屏 `create_screen_*`（已有"自定义优先、否则主题色"逻辑） |
| 6 | **CMake examples 开关失效** | — | `LV_CONF_BUILD_DISABLE_EXAMPLES` 在 LVGL 9.3+ 不生效（改名为 `CONFIG_LV_BUILD_EXAMPLES`），examples 被编译 | 两个开关都设置 + `option()` 兜底 |

**验证**：新增 4 个回归测试（ImageStretchModes / BorderColor / LineCap / ScreenBgColor），套件 1043/1043 全绿；合成工程（Contain/Center 图片 + 橙色边框按钮 + 圆头线条）导出编译 LVGL 9.5 成功，Xvfb 截图程序化验证：图片等比/居中渲染、按钮橙色边框 684px、线条 318px、屏幕背景 0x101418 全屏生效（修复前 0x131c31 主题色）。

**遗留待办（非本次范围）**：
- 数据绑定运行时刷新（模拟器静态值 vs 预览动态值）
- Gauge/Knob 形态（设计器 270° 弧 vs LVGL lv_arc 圆环）
- 自适应布局断点语义（模拟器窗口=设计分辨率，缩放恒 1）
- 存量 .hmi 中图片 StretchMode 仍为 None 的需在属性面板改 Fill

