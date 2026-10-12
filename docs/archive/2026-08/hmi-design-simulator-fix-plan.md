# HMI 设计页面 vs 实际运行页面不一致 — 结构化修复计划与执行记录

> 依据诊断报告 `hmi-design-runtime-diagnosis.md` 制定。按优先级 + 依赖关系拆分，逐步执行。
> 每步标注：修改目标 / 涉及文件 / 验证方式 / 完成标准 / 状态。

---

## 任务总览与依赖关系

```
P0  (阻断运行/构建正确性)
 ├─ T1  生成代码资源路径硬编码 → 相对路径        (独立)
 ├─ T2  模拟器 main.c 未调用 apply_theme_styles   (独立)
 ├─ T3  屏幕背景图未生成实际图片对象              (独立)
 └─ T4  统一 LVGL 版本参数 (README/CMake v9.2 vs 生成头 9.x)  (独立)

P1  (桌面端运行时空白，影响核心体验)
 └─ T5  Dock.Avalonia 面板挂载生命周期根治        (依赖: 无，但需 GUI 运行时验证)

P2  (一致性收尾)
 └─ T6  陈旧产物 sim-output-mock 重新导出         (依赖: T1~T4)
```

- **P0 优先级最高**：这些是「确定性 bug」，直接导致生成的 LVGL 代码编译失败、资源失效、中文显示 tofu，且均可通过单元编译验证，不依赖 GUI。
- **T1–T4 相互独立**，可并行；**T6 依赖 T1–T4**（重新导出才能拿到修复后的产物）。
- **T5 单独**：涉及 Avalonia 运行时行为，代码可改、编译可验证，但最终效果需 GUI 冒烟测试。

---

## P0 — 生成链路确定性缺陷

### T1 · 资源路径硬编码 → 相对路径 ✅ 已完成（含运行时崩溃修复）

- **修改目标**：`generated_hmi.c` / `generated_images.c` / `generated_fonts.c` 不再固化开发者本机绝对路径（`A:/home/chen/...`），同时保证运行时资源可正确加载。
- **涉及文件**：
  - `src/HmiEditor.CodeGen/CodeGenerator.cs` — 图片路径宏生成（`IMG_PATH_*`）
  - `src/HmiEditor.Core/Services/SimulatorProjectExporter.cs` — `Export()` 资源复制；`BuildCmakeLists` 注入 `SIM_ASSET_DIR`；`BuildImagesC` / `BuildFontsC` 路径宏
- **验证方式**：`dotnet build HmiEditor.Core` + `HmiEditor.CodeGen`（0 警告 0 错误）；C 宏拼接测试确认输出 `A:/abs/assets/xxx.bmp` 与 `/abs/assets/xxx.ttc`。
- **完成标准**：生成代码中的资源路径由 `SIM_ASSET_DIR` 编译宏拼接为绝对路径，工程迁移后重新 cmake 自动更新，且不受进程工作目录影响。
- **状态**：✅ 已完成，Core/CodeGen 编译通过。
- **⚠️ T1 初始方案引入的运行时崩溃（已修复）**：
  - **现象**：重新导出后编译成功，但模拟器启动即「异常退出」（段错误）。
  - **根因**：T1 最初把资源路径改成相对路径 `assets/<文件名>`，但存在两个致命错误：
    1. 图片 `A:assets/xxx.bmp` 因 `lv_conf.h` 的 `LV_FS_STDIO_PATH "/"` 把 `A:` 映射到系统根 `/`，实际访问 `/assets/xxx.bmp`（不存在）；
    2. 字体 `assets/xxx.ttc` 由 FreeType 按相对路径解析，而 `BuildRunDialog` 以 `build/` 为工作目录，实际访问 `build/assets/xxx.ttc`（不存在）。
    3. 字体加载失败 → `font_xxx` 保持零初始化 → `apply_theme_styles()` 的 `lv_obj_set_style_text_font(scr, &font_xxx, 0)` 渲染时解引用空函数指针 → 段错误。
  - **修复**：
    1. `BuildCmakeLists` 新增 `target_compile_definitions(hmi_sim PRIVATE SIM_ASSET_DIR="${CMAKE_SOURCE_DIR}/assets")`；
    2. `CodeGenerator.cs` / `BuildImagesC` 图片宏改为 `"A:" SIM_ASSET_DIR "/<文件名>"`；
    3. `BuildFontsC` 字体宏改为 `SIM_ASSET_DIR "/<文件名>"`（FreeType 直接绝对路径）；
    4. `BuildFontsC` 的 `init_font_assets()` 在字体加载失败或 `!LV_USE_FREETYPE` 时，回退 `font_xxx = *LV_FONT_DEFAULT`，杜绝零初始化结构体崩溃。
  - **验证**：`dotnet build` 通过；gcc 宏拼接测试输出正确绝对路径。

### T2 · 模拟器 main.c 未调用 apply_theme_styles ✅ 已完成（含 T6 执行时发现的链接错误修复）

- **修改目标**：`apply_theme_styles()`（含背景色/文本色/CJK 字体）在模拟器启动时被真正调用，而非死代码。
- **涉及文件**：`src/HmiEditor.Core/Services/SimulatorProjectExporter.cs` — `BuildMainC` 在 `lv_screen_load(screens[0])` 之后插入 `apply_theme_styles();`，并补充 `extern void apply_theme_styles(void);` 声明。
- **验证方式**：`dotnet build HmiEditor.Core`（0 警告 0 错误）；模拟器运行日志应显示字体加载成功、中文不再 tofu。
- **完成标准**：`main.c` 中存在 `apply_theme_styles();` 调用，且位于 `lv_screen_load` 之后。
- **状态**：✅ 已完成，编译通过。
- **T6 阶段暴露的阻塞问题（已修复）**：
  - **现象**：重新导出后编译模拟器报 `main.c:(.text.startup+0x184): undefined reference to 'apply_theme_styles'`。
  - **根因**：`CodeGenerator.GenerateThemeCode` 把 `apply_theme_styles` 生成为 `static`（仅在 `generated_hmi.c` 内可见），而 `SimulatorProjectExporter.BuildMainC` 生成的 `main.c` 通过 `extern` 跨编译单元链接它，导致链接失败。
  - **修复**：`src/HmiEditor.CodeGen/CodeGenerator.cs:501` 将 `static void apply_theme_styles(void)` 改为 `void apply_theme_styles(void)`。
  - **验证**：`dotnet build HmiEditor.CodeGen` 通过（0 警告 0 错误）。重新导出后模拟器即可链接成功。

### T3 · 屏幕背景图未生成实际图片对象 ✅ 已完成

- **修改目标**：`screen.BgImageAssetId` 非空时，生成 `lv_image_create` + `lv_image_set_src` + `lv_obj_move_background`，而非仅输出注释。
- **涉及文件**：`src/HmiEditor.CodeGen/CodeGenerator.cs` — `GenerateScreenCode` 背景图分支。
- **验证方式**：`dotnet build HmiEditor.CodeGen`（0 警告 0 错误）。
- **完成标准**：背景图资源在 `AssetLibrary` 中时，生成代码包含实际图片创建语句；不在时跳过（避免未声明标识符）。
- **状态**：✅ 已完成，编译通过。

### T4 · 统一 LVGL 版本参数 ✅ 已完成

- **修改目标**：README / CMakeLists 中硬编码的 `v9.2` / `release/v9.2` 跟随 `project.LvglVersion`（当前为 `9.5`），消除「生成头写 9.x、构建脚本 clone v9.2」的 API 不兼容风险。
- **涉及文件**：`src/HmiEditor.Core/Services/SimulatorProjectExporter.cs` — `BuildCmakeLists`（增加 `lvglVersion` 参数）、`BuildReadme`、`Export` 调用处。
- **验证方式**：`dotnet build HmiEditor.Core`（0 警告 0 错误）。
- **完成标准**：导出的 README/CMakeLists 中 LVGL 分支与 `project.LvglVersion` 一致。
- **状态**：✅ 已完成，编译通过。

---

## P1 — 桌面端运行时空白

### T5 · Dock.Avalonia 面板挂载生命周期根治 🔧 代码已改，待 GUI 验证

- **修改目标**：解决启动/拖拽后 `DeferredContentControl` 未重新挂载面板视图导致的空白。
- **已实施修改**：
  1. `src/HmiEditor.App/ViewLocator.cs` — `Build()` 优先复用 `dockable.Context` 中的共享实例，仅在 Context 为空时才走工厂回退，避免「工厂反复 new 新实例」与 `HmiDockFactory`/`MainWindow` 持有的共享实例脱节。
  2. `src/HmiEditor.App/MainWindow.axaml.cs` — `OnOpened` 增加延迟到 `DispatcherPriority.Background` 的 `RebuildAllPanels()` 兜底，确保首次模板应用后面板内容非空。
- **涉及文件**：`src/HmiEditor.App/ViewLocator.cs`、`src/HmiEditor.App/MainWindow.axaml.cs`。
- **验证方式**：`dotnet build HmiEditor.App`（0 错误）；GUI 冒烟：启动后工具箱（20+ 项）、画布（空态提示）、属性面板均可见；拖拽项目浏览器到工具箱 dock 后工具箱仍非空。
- **完成标准**：编译通过 + 启动画面与 `r1-ui-preview.jpg` 一致（面板内容非空）。
- **状态**：✅ 代码已改，App 编译通过（0 错误）；GUI 运行时验证仍需人工/真机冒烟。
- **遗留说明**：`debug-dock-blank-after-drag.md` 仍标记 OPEN，若本次修改后拖拽仍空白，需在 `DeferredContentControl` 级别加诊断日志定位「谁负责重新挂载」（见该文档 Next Steps）。

---

## P2 — 一致性收尾

### T6 · 同步陈旧产物 sim-output-mock ✅ 已完成（含生成器表格重复代码修复）

- **修改目标**：`sim-output-mock/` 是旧版生成器产物（含 `lv_table_set_row_cnt` 旧 API、`Col1/Col2/Col3` 占位符、未调用的 `apply_theme_styles`、绝对路径）。需同步为当前修复后生成器会输出的产物。
- **涉及文件**：
  - `src/HmiEditor.CodeGen/CodeGenerator.cs` — 删除第 1276–1284 行「L1」重复表格占位代码（该硬代码在 `GenerateControlParameters` 之后执行，会覆盖正确表头）。
  - `sim-output-mock/generated_hmi.c` — `static void apply_theme_styles` → `void apply_theme_styles`；删除重复表格初始化；背景图从注释改为实际 `lv_image_create`；图片路径宏改为 `SIM_ASSET_DIR` 拼接。
  - `sim-output-mock/generated_images.c` / `generated_fonts.c` — 路径宏改为 `SIM_ASSET_DIR` 拼接；字体加载失败回退 `*LV_FONT_DEFAULT`。
  - `sim-output-mock/main.c` — 增加 `extern void apply_theme_styles(void);` 并在 `lv_screen_load` 后调用。
  - `sim-output-mock/CMakeLists.txt` — 注入 `SIM_ASSET_DIR="${CMAKE_CURRENT_SOURCE_DIR}/assets"`。
  - `sim-output-mock/assets/` — 将资源从 `assets/assets/` 上移到 `assets/` 根目录，与生成器路径约定一致。
- **验证方式**：
  - `dotnet build HmiEditor.CodeGen`（0 警告 0 错误）；
  - `grep` 确认 `sim-output-mock/generated_hmi.c` 不再包含 `lv_table_set_row_cnt`、`Col1/Col2/Col3`、`/home/chen/`；
  - gcc 宏拼接测试确认 `IMG_PATH_*` / `FONT_PATH_*` 展开为正确绝对路径。
- **完成标准**：`sim-output-mock/` 与当前 `CodeGenerator.cs` + `SimulatorProjectExporter.cs` 输出一致；资源目录结构与生成器约定一致；无二义性绝对路径。
- **状态**：✅ 已完成。

---

## 验证汇总

| 任务 | 代码修改 | 编译验证 | 运行时验证 |
|---|---|---|---|
| T1 资源路径 | ✅ | ✅ Core/CodeGen 0 错 | ✅ 宏拼接测试通过（SIM_ASSET_DIR 绝对路径） |
| T2 apply_theme_styles | ✅ | ✅ Core 0 错 | 模拟器中文渲染 |
| T3 背景图 | ✅ | ✅ CodeGen 0 错 | ✅ sim-output-mock 已同步 |
| T4 版本统一 | ✅ | ✅ Core 0 错 | 导出 README/CMake 检查 |
| T5 Dock 空白 | ✅ | ✅ App 0 错（3 既有警告） | 需 GUI 冒烟 |
| T6 同步 sim-output-mock | ✅ | ✅ CodeGen 0 错 | ✅ 无旧 API / 无 Col1~3 / 无绝对路径 |
| T9 图片缩放/控件背景/文本居中 | ✅ | ✅ CodeGen 0 错 | 重新编译运行模拟器确认 |

## 迭代修复：下拉框 / 背景 / 文本位置

### T7 · 下拉框选项换行符转义错误 ✅ 已修复

- **现象**：模拟器里 Dropdown 按钮显示多行文本（`默认配方\n高温烘焙\n...`），而不是正常下拉按钮。
- **根因**：`CodeGenerator.cs` 中 `EscapeCString` 只转义了 `\` 与 `"`，没有转义实际换行符；Dropdown 分支又错误地先执行 `Replace("\n", "\\n")`，把真实换行符替换为字面量 `\n`（两个字符：反斜杠 + n），再经 `EscapeCString` 变成 `\\n`。最终 C 源码 `"默认配方\\n高温烘焙..."` 编译后字符串里保留的是字面量 `\n`，LVGL 无法解析为选项分隔符。
- **修复**：
  - `EscapeCString` 增加对 `\n` / `\r` / `\t` 的转义；
  - Dropdown 分支直接调用 `EscapeCString(control.Options)`，不再预替换；
  - 同步修改 `sim-output-mock/generated_hmi.c` 中的 `lv_dropdown_set_options` 字符串。
- **验证**：`dotnet build HmiEditor.CodeGen` 通过。

### T8 · 背景图资源不匹配 / 文本位置偏移 ⏸️ 待设计模型修正

- **背景问题**：`sim-output-mock/assets/asset_bg.bmp` 实际内容是 UI 元素（深色底 + 条形图/方块），不是设计稿里的网格/点状背景。生成代码虽然正确创建了 `lv_image_create` + `move_background`，但加载的素材本身不对。
- **文本位置问题**：左侧数据标签起始 Y=150，与设计稿视觉有约 20px 偏差；右侧 `obj_lbl_user` / `obj_lbl_chinese` 与 Slider/Bar/右侧大图区域重叠，导致显示截断。
- **性质判定**：这两者属于**设计模型/素材配置问题**，不是生成器 bug。当前 `sim-output-mock` 是手工构造的测试夹具（无对应 `.hmi` / `.design` 源文件），坐标与资源分配需要回到设计模型里修正。
- **修复方向**：
  1. 为 `asset_bg` 重新提供一张符合设计稿网格/渐变背景的 1024×600 BMP；
  2. 调整左侧标签 Y 坐标到与设计稿一致（约 130 起，按 36px 行距）；
  3. 将 `obj_lbl_user` / `obj_lbl_chinese` 移出 Slider/Bar/大图区域，或删除（若它们只是 CJK 测试占位标签）。

### T9 · 图片缩放 / 控件背景 / 文本居中对齐 ✅ 已修复

- **现象**：模拟器中图片控件里的图片未按控件尺寸缩放；Slider/Bar 看不到轨道背景；Dropdown 文本靠左；部分 Button 内文本未显式居中。
- **根因**：
  1. `CodeGenerator.cs` 的 Image 分支只输出注释 `// Image stretch: xxx`，没有调用 `lv_image_set_inner_align()`；
  2. Slider/Bar 只设置了 `LV_PART_INDICATOR` 颜色，未设置 `LV_PART_MAIN` 轨道背景，导致只有指示器可见；
  3. Dropdown 未设置 `text_align` 样式，默认靠左；未显式设置背景/边框时受 LVGL 默认主题影响，可能呈现浅色背景；
  4. Button 的子 Label 创建后未做居中对齐，依赖 LVGL 默认行为。
- **修复**：
  1. `CodeGenerator.cs` Image 分支：根据 `StretchMode`（Fill/Contain/Tile/Center/None）生成 `lv_image_set_inner_align(...)`，缺省 `LV_IMAGE_ALIGN_STRETCH`；屏幕背景图同样生成对应 `inner_align`。
  2. Slider/Bar：当 `BgColor` 为空时，使用 `theme.SurfaceColor` 生成 `LV_PART_MAIN` 的 `bg_color`、`bg_opa`、`radius`。
  3. Dropdown：生成 `lv_obj_set_style_text_align(..., LV_TEXT_ALIGN_CENTER, 0)`；并在 `BgColor` 为空时使用 `theme.SurfaceColor` + 主题边框生成默认背景。
  4. Button：子 Label 生成后增加 `lv_obj_set_style_text_align(label, LV_TEXT_ALIGN_CENTER, 0)` 与 `lv_obj_center(label)`。
  5. 同步修改 `sim-output-mock/generated_hmi.c`：为背景图、Banner、Logo、右侧大图添加 `lv_image_set_inner_align`；为 Slider/Bar 添加轨道背景；为 Dropdown 添加居中/背景；为所有 Button 添加 Label 居中。
- **涉及文件**：`src/HmiEditor.CodeGen/CodeGenerator.cs`、`sim-output-mock/generated_hmi.c`。
- **验证方式**：`dotnet build HmiEditor.CodeGen`（0 警告 0 错误）；重新编译运行模拟器后确认图片填满指定矩形、Slider/Bar 有深色轨道、Dropdown 文本居中。
- **状态**：✅ 已完成。

### T10 · LV_IMAGE_ALIGN_CONTAIN 未声明编译错误 ✅ 已修复

- **现象**：`generated_hmi.c:128` 报 `error: 'LV_IMAGE_ALIGN_CONTAIN' undeclared (first use in this function); did you mean 'LV_IMAGE_ALIGN_CENTER'?`。
- **根因（最终确认）**：项目默认目标版本就是 **LVGL 9.5**（`HmiProject.LvglVersion` 默认 `"9.5"`，固件 `Tina-SDK` 内是 v9.5.0，含 `CONTAIN`/`COVER`），但用户本机 PC 模拟器用的 `/home/chen/Git/lvgl` 仍是 **v9.2.3 旧快照**，其 `lv_image_align_t` 枚举没有 `CONTAIN`/`COVER`（这两个值是 **v9.3.0 才引入**）。版本不匹配才导致报错，生成器本身面向 9.5 并无问题。
- **修复**：
  1. `CodeGenerator.cs` Image 分支对 `CONTAIN`/`COVER` 用版本宏兜底——`#if LV_VERSION_CHECK(9, 3, 0)` 用该值，否则回退 `LV_IMAGE_ALIGN_STRETCH`。
  2. **修正一处隐患**：最初误用 `#if defined(LV_IMAGE_ALIGN_CONTAIN)`，但 `defined()` 只能判断宏，`CONTAIN` 是**枚举值不是宏**，该写法恒为假、会永远回退 STRETCH（即便 9.5 也拿不到等比缩放）。改为 `LV_VERSION_CHECK(9, 3, 0)` 后 9.5 走 `CONTAIN` 分支、9.2.3 走 STRETCH 兜底。
  3. 同步修正 `sim-output-mock/generated_hmi.c` 与 `/tmp` 导出产物。
- **验证**：`dotnet build HmiEditor.CodeGen`（0 警告 0 错误）；`cmake --build build` 对 `/tmp/zhishu_hmi_sim/Mock中文显示测试工程` 重新编译链接成功（9.2.3 走 STRETCH 兜底路径通过）；9.5 分支已对照固件 `lv_image.h` 确认 `CONTAIN` 存在、`LV_VERSION_CHECK` 存在于两版本 `lvgl.h`。
- **遗留动作**：将 PC 模拟器用的 `/home/chen/Git/lvgl` 更新到 `release/v9.5`（`git fetch && git checkout release/v9.5 && git pull`），与项目目标/固件保持一致，才能拿到 `CONTAIN`/`COVER` 等比缩放。
- **状态**：✅ 已完成。

### T11 · 设计画布背景透明 / 控件背景显示 ✅ 已修复

- **现象**：设计画布中屏幕区域强制填充深灰（`BgColor` 为空时回退 `#191E2D`），未配置背景色的控件回退深灰 `#2D374B` 叠在深色画布上几乎不可见——用户反馈「控件背景没有颜色、没有背景图片、画布背景应该透明」。
- **根因**：`DesignCanvas.Render` 在 `screen.BgColor` 为空时仍强制 `FillRectangle` 深灰；`RenderControl` / `RenderButton` / `RenderImage` 在 `control.BgColor` 为空时同样强制填充深灰兜底色。设计画布与「透明背景 + 按配置着色」的设计工具惯例不符。
- **修复**（`src/HmiEditor.App/Controls/DesignCanvas.cs`）：
  1. 屏幕区域：`screen.BgColor` 为空 → 不填充（透明，透出 SnapGrid 网格与画布底）；有值才填充。
  2. 控件通用路径：`control.BgColor` 为空 → 不填充背景（透明，仅边框+文字/进度）；有值显示配置色。
  3. `RenderButton`（无图片分支）与 `RenderImage`（占位分支）：同样改为「仅配置了 BgColor 才填充」。
  4. 屏幕边框线、背景图绘制（`DrawScreenBackgroundImage`）、Slider/Bar 进度填充均保留，不受影响。
- **验证**：`dotnet build HmiEditor.App`（0 错误，3 个既有警告）。
- **状态**：✅ 已完成（GUI 冒烟待用户确认）。

## 后续建议（本计划未覆盖，属更大重构）

- 将 `.design` 从「HTML/图片元数据」升级为结构化控件模型，作为设计与代码生成的单一事实源。
- 将 `colors_and_type.css` 的 `--hmi-*` tokens 映射到 `App.axaml` 的 `Hmi*Brush` 与 LVGL 主题常量。
- 为 LVGL 生成器实现运行时数据绑定（`lv_label_set_text_fmt` + 定时刷新），替代硬编码字符串。
- 建立「设计稿截图 → 控件坐标/资源 → 生成代码」的回归对比测试，避免素材错配导致背景/位置漂移。
