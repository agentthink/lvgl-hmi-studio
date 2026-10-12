# HMI 设计页面 vs 实际运行页面不一致 — 根因诊断报告

**诊断范围**：`design-previews/`（设计稿 / HTML 预览）与 `src/HmiEditor.App/` + `sim-output-mock/`（实际运行产物）
**关键证据**：
- 设计稿：`design-previews/r1-ui-preview.jpg`
- 实际运行截图：`design-previews/r1-runtime-screenshot.png`

---

## 1. 执行摘要

| 维度 | 设计稿表现 | 实际运行表现 | 偏差等级 |
|---|---|---|---|
| **HMI Editor 桌面应用** | `r1-ui-preview.jpg`：工具箱、画布组件、属性面板、状态栏全部正常填充 | `r1-runtime-screenshot.png`：菜单/工具栏可见，但画布全黑、面板内容空白 | **严重** |
| **生成的 LVGL HMI 页面** | `faceplate-editor.html` / `.design`：800×480、Dark Industrial 主题、参数绑定 | `sim-output-mock/generated_hmi.c`：1024×600、硬编码颜色、无绑定 | **严重** |

**根因定位**：
1. **桌面端运行空白** —— `Dock.Avalonia` 的 `DeferredContentControl` 在 Dock 重组/拖拽后无法可靠重新挂载面板视图，`ViewLocator` / `HmiDockFactory` 的“工厂重建”策略尚未彻底修复该生命周期问题（对应未关闭的 `debug-dock-blank-after-drag.md`）。
2. **设计稿与生成代码脱节** —— 设计产物是静态 HTML/图片（`solo-design` skill 生成），并非结构化设计模型；代码生成器（`HmiEditor.CodeGen`）使用独立的硬编码主题/坐标/文本，未消费设计 tokens，导致分辨率、颜色、字体、数据绑定全面错位。

---

## 2. 实证对比：设计稿 vs 运行截图

![设计稿](design-previews/r1-ui-preview.jpg)
*设计稿 `r1-ui-preview.jpg`：左侧工具箱、中间画布（按钮/仪表/LED/进度条）、右侧属性面板、底部 Flash/RAM 状态均正常。*

![实际运行](design-previews/r1-runtime-screenshot.png)
*实际运行 `r1-runtime-screenshot.png`：仅保留菜单栏与工具栏，画布全黑，左侧面板“工具箱”内容空白，右侧属性面板无内容。与 `debug-dock-blank-after-drag.md` 描述的“拖动后工具箱变空白”症状一致，但默认启动即已呈现空白态。*

---

## 3. 系统性差异对比

### 3.1 布局结构

| 项目 | 设计稿 | 实际运行 |
|---|---|---|
| **顶层结构** | CSS Grid：`48px 1fr 32px` + 三列主区域 `260px 1fr 320px`（`faceplate-editor.html`） | `MainWindow.axaml` 使用 `Dock.Avalonia` 的 `DockControl`，由 `HmiDockFactory.CreateLayout()` 动态组装 |
| **画布尺寸** | `.hmi-screen { width: 800px; height: 480px; }`（`faceplate-editor.html:335-338`） | `HmiScreen { Width=1024, Height=600 }`（`MainWindow.axaml.cs:35-40`）；LVGL 屏幕同样硬编码 `1024×600`（`generated_hmi.c:117`） |
| **布局引擎** | CSS Flex/Grid，响应式 | Avalonia Dock + 绝对坐标的 `DesignCanvas`；LVGL 全部为 `lv_obj_set_pos` 绝对像素定位 |
| **问题** | 设计稿 800×480，运行/生成目标 1024×600，**分辨率/比例不匹配** | Dock 重组后子视图从视觉树脱离，无法重新挂载，导致面板空白 |

### 3.2 样式渲染

| 项目 | 设计稿 tokens（`colors_and_type.css`） | 实际运行 |
|---|---|---|
| 背景 | `#0D0E12` | `#0f172a` / `#1e293b`（`App.axaml`、`generated_hmi.c:118,293`） |
| 主色 | `#3B71F5`（indigo） | `#3b82f6` / `#16a34a` / `#dc2626` / `#2563eb` 等（按钮各自硬编码） |
| 面板 | `#16181F` | `#1a2332` |
| 字体 | `Inter`, `Noto Sans SC` 等系统字体 | Avalonia 默认字体；LVGL 使用 FreeType 加载 `asset_font_cjk.ttc` |
| **问题** | 设计 tokens 未被运行时/生成器消费 | 运行/生成代码使用另一套“Dark Industrial”硬编码色板，**视觉风格不一致** |

### 3.3 组件状态

| 项目 | 设计稿 | 实际运行 |
|---|---|---|
| 工具箱 | 20+ 控件项（按钮、滑条、仪表、LED 等） | `ToolboxView` 构造函数已填充 `_allItems`（`ToolboxView.axaml.cs:41-50`），但运行截图中列表空白，说明视图实例未正确挂入 Dock |
| 画布组件 | 确认按钮、仪表、LED、进度条等 | 默认屏幕 `Controls` 为空，应显示空态提示；截图中连空态提示都没有，表明 `DesignCanvasView` 本身未渲染 |
| 属性面板 | X/Y/宽度/高度/亮度/背景色/绑定变量 | `PropertyPanelView` 依赖 `DockLayoutState.SelectedControl`，但画布未渲染，无法产生选中状态 |
| 状态栏 | Flash 45%、RAM 62% 等 | `FlashBar/RamBar.Value=0`，目标显示默认字符串 |

### 3.4 数据绑定

| 项目 | 设计稿 | 实际运行 |
|---|---|---|
| **设计时** | HTML 中展示参数绑定表：`Title` → `电机 A`，`StatusVar` → `var_motor_a_status`（`faceplate-editor.html:879-911`） | `generated_hmi.c` 明确标注 `No bound variables`、`Runtime tags: 0`、`Script Dispatch Table: 0 scripts` |
| **运行时** | 静态占位值 | `lv_label_set_text(obj_lbl_temp, "温度：27.5 ℃")` 等全部硬编码字符串，**无动态数据链路** |
| **问题** | 设计稿表达“可绑定变量” | 代码生成器未生成任何绑定/变量读取逻辑，运行时数值不会更新 |

### 3.5 响应式断点 / 设备参数

| 项目 | 设计稿 | 实际运行 |
|---|---|---|
| 目标设备 | `deviceType: desktop`（`.design`）；状态栏显示 `STM32F429IG`（HTML） | `MainWindow.axaml.cs`：`HardwareTargetId = "allwinner-t113"`；`generated_hmi.c` header：`Target: allwinner-t113` |
| 分辨率 | 1440×900 设计视口，画布 800×480 | 窗口 1400×900（`MainWindow.axaml:9-10`），画布/屏幕 1024×600 |
| LVGL 版本 | HTML 显示 `LVGL 9.2` | `generated_hmi.c` header 写 `LVGL: 9.5`；`README.md`/`lv_conf.h` 写 v9.2 |
| **问题** | 目标平台、LVGL 版本、画布分辨率在三处关键文件中不一致 | 设计稿无法驱动生成器，生成器也未按设计稿参数输出 |

### 3.6 环境配置

| 项目 | 设计稿 | 实际运行 |
|---|---|---|
| 资源路径 | 相对路径/CSS CDN | `generated_hmi.c` / `generated_images.c` / `generated_fonts.c` 全部使用**绝对路径**：`A:/home/chen/Git/zhishu-hmi/sim-output-mock/assets/assets/...` |
| 字体加载 | 浏览器系统字体回退 | `lv_conf.h` 启用 `LV_USE_FREETYPE`，但 `generated_hmi.c` 中的 `apply_theme_styles()` **定义了却从未调用**，屏幕默认字体未切换到 CJK 字体 |
| 图片加载 | CSS `background-image` | `asset_bg` 在代码中仅有注释、未调用 `lv_image_set_src`，背景图实际未显示；其余图片通过 FS_STDIO 的 `A:` 路径加载，项目迁移即失效 |

---

## 4. 代码级关键缺陷

### 4.1 桌面端：Dock.Avalonia 生命周期缺陷（主要根因）

- **`debug-dock-blank-after-drag.md`** 已记录：`ToolboxView` 在 Dock 重组时 `DETACHED from visual tree. WasParent=DeferredContentControl`，之后从未重新 `ATTACHED`。
- **`src/HmiEditor.App/Services/HmiDockFactory.cs:87-366`**：`CreateLayout()` 创建所有 `Tool` 并把共享视图实例赋给 `Context`。
- **`src/HmiEditor.App/ViewLocator.cs:40-65`**：`Build()` 优先走工厂创建新实例，再注册到 `_activeViews`。理论上可规避旧实例重挂问题，但运行截图证明仍未彻底解决：
  - 工厂创建的新 `ToolboxView`/`DesignCanvasView` 可能未被正确设置为 `DeferredContentControl` 的 `Content`；
  - `RebuildAllPanels()`（`MainWindow.axaml.cs:444-507`）只刷新数据，不修复视觉树挂载失败；
  - `DesignCanvasView.RefreshForDock()`（`DesignCanvasView.axaml.cs:273-284`）调用 `InvalidateVisual()`，但如果视图根本不在视觉树中则无效。
- **结果**：启动后 Dock 框架已完成 `InitLayout` / `ApplyState`（`dock-init.log`），但面板内容区未渲染。

### 4.2 生成端：设计稿与 LVGL 代码完全脱节

- **颜色未消费设计 tokens**：`colors_and_type.css` 使用 `#0D0E12` / `#3B71F5`，`generated_hmi.c` 使用 `#0f172a` / `#16a34a` / `#dc2626` 等，未引用 tokens。
- **主题样式函数悬空**：`apply_theme_styles()`（`generated_hmi.c:49-61`）设置了 CJK 字体和文本颜色，但**没有任何调用方**。结果是：
  - 屏幕默认字体仍是 LVGL 内置 Montserrat，**中文字符可能显示为 tofu/方框**；
  - 屏幕背景色由各自 `lv_obj_set_style_bg_color(screen, ...)` 设置，与主题色不一致。
- **背景图未应用**：`asset_bg` 声明了路径，但 `create_screen_screen001_inner` 中仅有注释 `// Screen background image: asset_bg ...`，**无 `lv_image_set_src` 调用**。
- **表格 API 混用 / 数据被覆盖**：
  ```c
  lv_table_set_row_count(obj_tbl_lines, 4);
  lv_table_set_column_count(obj_tbl_lines, 3);
  lv_table_set_cell_value(obj_tbl_lines, 0, 0, "产线编号");
  ...
  lv_table_set_row_cnt(obj_tbl_lines, 5);        // 疑似旧 API / 拼写错误
  lv_table_set_column_count(obj_tbl_lines, 3);
  lv_table_set_cell_value(obj_tbl_lines, 0, 0, "Col1");  // 覆盖表头
  ```
  设计稿意图为“产线编号/状态/良品率”表格，运行时将显示 `Col1/Col2/Col3` 占位符，且 `lv_table_set_row_cnt` 在 LVGL 9.5 下可能无法编译。
- **Emoji/特殊符号**：`💾 保存设置到文件`、`◉ 预加热：已启用` 中的 `💾` 不在 CJK 字体中，运行时可能显示为空白/方框。
- **无数据绑定**：`generated_hmi.c` 中所有数值标签均为硬编码字符串（如 `"温度：27.5 ℃"`），未关联 `DockLayoutState` 或运行时变量。
- **资源路径硬编码**：`generated_images.c` 和 `generated_fonts.c` 使用绝对路径 `A:/home/chen/...`，工程目录迁移后资源加载必然失败。

---

## 5. 诊断结论

1. **HMI Editor 桌面应用运行空白**：
   - **直接原因**：`Dock.Avalonia` 的 `DeferredContentControl` 在 Dock 布局构建/重组时未能把面板视图重新挂回视觉树，导致工具箱、画布、属性面板等内容区空白。
   - **深层原因**：修复策略（`ViewLocator` 工厂重建 + `RebuildAllPanels` 双阶段刷新）未能覆盖启动时首次模板应用/ControlRecycling 的边界情况；同时设计稿只是静态 HTML/图片，无法作为 Dock 布局的运行时数据源，也未能提供默认演示数据。

2. **生成的 LVGL HMI 与设计稿不一致**：
   - **直接原因**：代码生成器未消费设计 tokens / `.design` 模型，独立使用硬编码颜色、坐标、文本。
   - **深层原因**：设计链路（`solo-design` skill）与代码生成链路（`HmiEditor.CodeGen`）之间**没有共享结构化模型**；设计稿是“可看图”，不是“可执行规范”。

3. **两类不一致的共同根因**：**设计-实现单向前导，缺乏单一事实源（Single Source of Truth）**。设计产物（HTML/JPG）与实现产物（Avalonia 视图 / LVGL C 代码）之间没有可验证、可同步的中间模型。

---

## 6. 修复建议（按优先级）

### 6.1 桌面端空白（高优先级）

1. **根治 Dock 生命周期**：
   - 在 `HmiDockFactory` / `ViewLocator` 中增加启动时自检：遍历 Dock 树，确保每个 `Tool.Context` 对应的视图已实际进入 `DockControl` 的视觉树；若未挂载，强制重建 `DeferredContentControl` 内容。
   - 参考 `debug-dock-blank-after-drag.md` 的 Next Steps：在 `DeferredContentControl` 级别加诊断日志，确认 `DETACHED` 后由谁负责重新挂载。
2. **避免构造期立即绑定 Dock**：`MainWindow.axaml.cs:288-289` 在构造函数中即设置 `DockControl.Factory/Layout`，可能与模板应用时序竞争。可改为在 `OnLoaded` 或 `DockControl.ApplyTemplate` 后绑定。
3. **提供默认演示项目**：启动时若 `CurrentScreen.Controls` 为空，可注入一组演示控件（与设计稿一致），避免用户看到“全黑画布”误以为程序崩溃。

### 6.2 设计-代码一致性（高优先级）

1. **建立结构化设计模型**：将 `.design` 从“HTML/图片元数据”升级为包含控件树、坐标、颜色、字体、绑定的 JSON Schema；代码生成器直接消费该 Schema。
2. **统一设计 tokens**：将 `colors_and_type.css` 中的 `--hmi-*` 变量映射到 `App.axaml` 的 `Hmi*Brush` 以及 LVGL 生成器的主题常量。
3. **修复 LVGL 生成器缺陷**：
   - 在 `create_screen_*` 开头调用 `apply_theme_styles()`；
   - 修正表格 API（`lv_table_set_row_cnt` → `lv_table_set_row_count`，移除重复的 `Col1/Col2/Col3` 覆盖）；
   - 为 `asset_bg` 实际创建 `lv_image` 并 `lv_image_set_src`；
   - 资源路径改为相对路径或打包为 C 数组，避免绝对路径；
   - 移除或替换 emoji，或增加 emoji 字体 fallback。
4. **实现运行时数据绑定**：从 `HmiScreen` 的变量/标签定义生成 `lv_label_set_text_fmt` + 定时刷新逻辑，替代硬编码字符串。
5. **统一目标参数**：将 LVGL 版本（9.2 vs 9.5）、目标硬件（STM32F429IG vs allwinner-t113）、画布分辨率（800×480 vs 1024×600）收敛到项目配置（`HmiProject` / `sim_config.h`）并保持一致。

---

## 7. 附录：关键文件清单

| 文件 | 说明 |
|---|---|
| `design-previews/r1-ui-preview.jpg` | R1 设计稿（预期效果） |
| `design-previews/r1-runtime-screenshot.png` | R1 实际运行截图（空白问题实证） |
| `debug-dock-blank-after-drag.md` | Dock 拖拽空白问题的调试记录 |
| `src/HmiEditor.App/MainWindow.axaml` | 主窗口布局、DockControl |
| `src/HmiEditor.App/MainWindow.axaml.cs` | 初始化、Dock 绑定、RebuildAllPanels |
| `src/HmiEditor.App/Services/HmiDockFactory.cs` | Dock 工厂、面板 Context 注入 |
| `src/HmiEditor.App/ViewLocator.cs` | Dock 内容视图定位器 |
| `src/HmiEditor.App/Views/ToolboxView.axaml.cs` | 工具箱视图生命周期 |
| `src/HmiEditor.App/Views/DesignCanvasView.axaml.cs` | 画布刷新逻辑 |
| `design-previews/hmi-editor-r2-preview/pages/faceplate-editor.html` | R2 HTML 设计稿 |
| `design-previews/hmi-editor-r2-preview/colors_and_type.css` | R2 设计 tokens |
| `sim-output-mock/generated_hmi.c` | LVGL 生成代码（含主题悬空、表格 API 等缺陷） |
| `sim-output-mock/generated_fonts.c` | 字体加载（绝对路径） |
| `sim-output-mock/generated_images.c` | 图片资源声明（绝对路径） |
| `sim-output-mock/lv_conf.h` | LVGL 配置 |
| `sim-output-mock/sim_config.h` | 模拟器分辨率 1024×600 |
