# LVGL 兼容性修复变更日志

- 日期：2026-08-06
- 范围：HmiEditor 代码生成器、HmiEditor.Core 模型、HmiEditor.App 画布/属性面板、firmware 配置说明
- 涉及提交：`f54d8cd`（feat(control): 控件值显示/全参数配置/LVGL 文档对齐/图片导入/SVG 渲染修复）及后续增量

---

## 1. LVGL 版本升级：9.2 → 9.5

将项目默认 LVGL 版本从 9.2 升级至最新稳定版 **v9.5.0**（2026-02-18 发布）。

| 位置 | 变更 |
|---|---|
| `HmiProject.LvglVersion` 默认值 | `"9.2"` → `"9.5"` |
| `MainWindow` 新建项目默认值（2 处） | `"9.2"` → `"9.5"` |
| 项目设置对话框版本下拉 | 扩充为 `8.3 / 9.0 / 9.1 / 9.2 / 9.3 / 9.4 / 9.5` |
| 生成代码头部注释 | `// LVGL: 9.5` |

> 说明：`firmware/t113` 仓库内仅含 `lv_conf.h` 配置（无版本宏），LVGL 库本体由外部 Tina SDK 提供。固件侧 LVGL 升级需在 SDK 中更新 lvgl 软件包，不属于本仓库范围。

## 2. 代码生成 API 修复（对齐 LVGL 9.x 官方规范）

依据官方文档（docs.lvgl.io/9.x 及 v9.2/v9.3 源码头文件）修正生成代码：

| 控件 | 原输出（错误） | 修正后 | 依据 |
|---|---|---|---|
| CheckBox 文本 | `lv_label_set_text` | `lv_checkbox_set_text` | CheckBox 专属 API |
| Table 列数 | `lv_table_set_col_count`（v8 名） | `lv_table_set_column_count` | 9.x 官方命名 |
| Switch/CheckBox 勾选 | 未生成 | `lv_obj_add_state(obj, LV_STATE_CHECKED)` | 勾选由状态位表达 |
| Led 颜色 | 仅亮度 | 增加 `lv_led_set_color`（取 BgColor） | 官方 API |
| TabView 激活页 | 仅 add_tab | 增加 `lv_tabview_set_active` | 官方 API |
| Spinner | — | 确认 v9 单参创建 + `lv_spinner_set_anim_params` | v9 破坏性变更 |
| 文本输出 | 未转义 | `EscapeCString`（转义引号/反斜杠） | 防 C 字符串损坏 |

## 3. 新增 LVGL 标准行为配置（模型 + 生成 + 面板 + 预览）

| 配置项 | 生成代码 | 适用控件 |
|---|---|---|
| `IndicatorColor` 指示器颜色 | `lv_obj_set_style_bg_color(obj, c, LV_PART_INDICATOR)` | Slider/Bar/Switch/Gauge/Knob |
| `Opacity` 不透明度 | `lv_obj_set_style_opa(obj, v, 0)` | 通用 |
| `IsVisible=false` 隐藏 | `lv_obj_add_flag(obj, LV_OBJ_FLAG_HIDDEN)` | 通用 |
| `LabelLongMode` 长文本模式 | `lv_label_set_long_mode`（WRAP/DOTS/SCROLL/SCROLL_CIRCULAR/CLIP） | Label |
| `ValueMode` 数值模式 | `lv_slider_set_mode` / `lv_bar_set_mode`（NORMAL/SYMMETRICAL/RANGE） | Slider/Bar |

## 4. LVGL 废弃 API 兼容性检查器（新）

新增 `LvglApiCompatChecker`，在 `CodeGenerator.GenerateFullProject` 收尾处自动执行，仅对 9.x 项目生效（8.x 项目跳过）。

- **自动替换**：`lv_table_set_col_cnt`、`lv_table_add_cell_ctrl`、`lv_tabview_rename_tab`、`lv_dropdown_set_selected_show_highlight`、`lv_slider_set_left_value`、`LV_ZOOM_IMAGE_NONE`、`LV_TABLE_CELL_CTRL_MERGE`、v8 `lv_img_*` 系列 → 对应 9.x API。
- **提示警告**（插入生成代码注释头，含替代方案）：`lv_bar_set_anim_time`、`lv_switch_set_anim_time`、`lv_textarea_set_cursor_blink_time`、`lv_line_set_point_count`、`lv_image_set_scale_mode`、`lv_tabview_set_tab_bar_buttons_show`、`lv_checkbox_set_checked`、`lv_canvas_draw_*`、`lv_slider_set_knob_area`、`lv_spinner_create` 多参写法。
- 所有处理记录以 `/* ── LVGL 兼容性检查 ── */` 注释头写入生成文件顶部。

## 5. SVG 渲染修复（画布）

| Bug | 根因 | 修复 |
|---|---|---|
| SVG 图形不跟随控件移动 | `BuildShapeGeometry` 未加控件位置偏移 | `RenderSvg` 以 `PushTransform(rect.X, rect.Y)` 整体平移到控件位置 |
| 缩放时 SVG 尺寸以 z² 放大 | `sx/sy` 已含缩放因子 z，坐标又乘一次 `z` | 去掉重复乘 z（坐标/圆角/描边宽度同步修正） |

## 6. 验证

- **单元测试**：新增 `LvglApiCompatCheckerTests` 4 用例（自动替换 / 提示输出 / 8.x 跳过 / 无问题原样返回），全部通过。
- **全量测试**：`dotnet test HmiEditor.slnx` — 5 个测试项目共 **1145 个用例全部通过**，0 失败 0 跳过。
- **编译**：全解决方案 0 错误。

## 7. 遗留事项

- 固件侧（Tina SDK）LVGL 库升级需在 SDK 中操作，升级后建议实机回归验证生成代码编译与运行。
- 运行时（hmi_runtime）的 LVGL API 调用若存在 v8 遗留写法，需按第 4 节清单人工核对迁移。

---
# LVGL 控件全面检查与优化变更日志

- 日期：2026-08-08
- 范围：固件设置模块（`firmware/t113/app/settings/`）、代码生成器（`HmiEditor.CodeGen`）
- 目标：审查并修复 LVGL 控件的性能/内存泄漏/功能缺陷，补全功能，补充测试与文档

## A. 代码生成器修复（`CodeGenerator.cs` + `Templates/`）

### A1. 修复趋势图多曲线空指针崩溃（严重）
- **问题**：`GenerateTrendCode` 对多 pen 趋势生成 `lv_chart_set_next_value(_chart_X, NULL, values[0])`。LVGL 的该 API 接受 `lv_chart_series_t*`，传 `NULL` 会在内部解引用空指针 → 运行时崩溃；且只推入 `values[0]`，无法展示多条曲线。
- **修复**：
  1. 为每个图表声明 series 指针数组 `static lv_chart_series_t* _series_<id>[N]`；
  2. `trend_init` 中把每个启用 pen 的 `lv_chart_add_series` 返回值存入数组；
  3. `trend_update` 遍历启用 pen，`lv_chart_set_next_value(_chart, _series[i], values[i])`；
  4. 无启用 pen 时生成 `(void)values; (void)count;` 消参，避免编译告警。
- 位置：`CodeGenerator.cs` `GenerateTrendCode`。

### A2. 修复报警模板 `{{forindex}}` 语法错误（`lvgl_v9_alarm.scriban`）
- **问题**：`alarm_check_*` 内 `(_alarm_configs + {{forindex}})`，Scriban 正确的迭代变量是 `{{for.index}}`；且嵌套循环内该写法无法得到全局索引，会产生越界/错位或模板解析失败。
- **修复**：改为按 `config_id` 查找 —— 新增 `_alarm_find_config(uint16_t id)` 线性扫描配置表（表以 sentinel `{0}` 结尾），`alarm_check_*` 通过自身 `config_id` 定位，`cfg==NULL` 时安全跳过，彻底消除位置索引耦合。

### A3. 报警 / 趋势生成器接入主生成流程（补齐功能断点）
- **问题**：`GenerateAlarmCode` / `GenerateTrendCode` 是**孤立公开方法**，`GenerateFullProject` 与 `GenerateSharedLibraryProject` 两个导出主流程均未调用它们，导致用户编辑的 `AlarmGroups` / `TrendConfigs` 即使配置了也不会出现在导出的 C 代码中（功能断点）。
- **修复**：在两个导出主流程的动画代码之后接入二者——
  - `GenerateFullProject`（第 100 行后）：`GenerateAlarmCode(project.AlarmGroups)` + `GenerateTrendCode(project.TrendConfigs, Array.Empty<DataLogConfig>())`；
  - `GenerateSharedLibraryProject`（第 214 行后）：同样的两行。
  - `DataLogConfig` 未暴露于 `HmiProject`，故日志器列表传空（趋势 `chart.pens` 数据源已足够生成 `trend_update`）。
  - 空配置时两个生成器返回占位注释，不产生额外 C 符号，兼容既有空工程导出。
- 演示产物：`docs/generated-samples/generated_hmi.c`（含 1 屏 + 2 曲线趋势图 + 1 报警的 `.so` 模式样例，展示接入后的真实输出）。

### A4. 报警 / 趋势引擎加编译期可控日志（方便排查）
- **问题**：生成的报警状态机 (`check_alarm_*` / `alarm_engine_evaluate`) 和趋势环形缓冲 (`trend_update_*` / `datalog_sample_*`) 是黑盒，出问题只能靠源码回溯。
- **修复**：为生成代码添加**编译期开关**的两套 trace 宏：
  - 报警：`ALARM_TRACE_ENABLE`（默认 0）+ `ALARM_TRACE(fmt,...)`，在 Inactive→Active / Active→Acknowledged / Active→Returned / Acknowledged→Returned 四个状态迁移处各打一条日志，包含 `alarm_id`、`val`、旧/新状态、阈值、severity、tick。
  - 趋势：`TREND_TRACE_ENABLE`（默认 0）+ `TREND_TRACE(fmt,...)`，在 `_chart == NULL` 提前返回、环形缓冲第一次回绕（满环切换 OverwriteOldest 策略）、`StopWhenFull` 停止时各打一条日志。
  - 两套宏均要求 `#include <stdio.h>`（已在生成段顶部补全）；格式串与实参经 `gcc -fsyntax-only` 校验确认完全匹配。
  - 排查时只需在生成代码顶部加 `#define ALARM_TRACE_ENABLE 1`（或 `TREND_TRACE_ENABLE 1`）即可开启详细日志，生产环境默认关闭零开销。

### A5. 补全报警确认功能 + 修正 C 浮点字面量可移植性
- **补全 `alarm_acknowledge`**（此前为空实现 `return 0;`）：
  - 每条报警在 `check_alarm_*` 内赋值 `r->config_id`（按启用顺序 0..N-1）；
  - 生成 `_alarm_lookup[]` 注册表（`{config_id, &_rec_<id>}`，sentinel `{0xFFFF,NULL}`）；
  - `alarm_acknowledge(config_id)` 遍历注册表，匹配且 `state==1`（Active）时置 `state=2`、`acknowledged_at=lv_tick_get()` 并返回 1；未激活/未找到返回 0，均带 `ALARM_TRACE`。
- **修正 C 浮点字面量**：`{threshold}f`/`{expected}f` 此前生成非标准的 `80f`（严格 gcc 报「整数常量 f 后缀无效」），改用 `F()` 辅助生成带小数点的 `80.0f`/`0.0f`（`InvariantCulture`，`0.###`），消除跨编译器可移植性隐患。
- **测试**：`TrendAndAlarmCodeGenTests` 新增 2 用例（注册表 + config_id 生成；浮点字面量带小数点），`dotnet test` → **878 用例全部通过**。
- **验证**：开启 trace 的报警/趋势代码段经 `gcc -std=gnu99 -Wall -Wextra -fsyntax-only` 编译 **0 error**。

### A6. 实现死区防抖 + 延时触发（补全报警条件语义）
模型 `AlarmCondition.Deadband` / `DelayMs` 此前在生成 C 代码中未实现（只有裸阈值判定）。已补全：
- **死区防抖**：判定拆分为 `triggered`（进入）与 `cleared`（解除）两个独立条件——
  - HighLimit：触发 `val >= T`，解除 `val <= T - Deadband`；
  - LowLimit：触发 `val <= T`，解除 `val >= T + Deadband`；
  - Deviation：触发 `|val-E| > T`，解除 `|val-E| <= T - Deadband`；
  - BitTrigger：无死区，`cleared = !triggered`。
  - 效果：激活越过阈值即可，解除必须回落穿越死区，避免在阈值附近抖动静触发/静解除。
- **延时触发**：`alarm_record_t` 新增 `pending_since`。当 `DelayMs>0` 且条件满足时先进入挂起，持续满足达到 `DelayMs` 才真正置 Active；期间条件未持续满足则取消挂起（`ALARM_TRACE` 记录）。`DelayMs=0` 直接触发。
- **测试**：`TrendAndAlarmCodeGenTests` 新增 3 用例（HighLimit 死区 cleared、LowLimit 死区 cleared、延时挂起/取消/激活），`dotnet test` → **881 用例全部通过**（新增 16 例/总计）。
- **验证**：含 3 种条件（High+死区+延时、Low+死区、Bit）的报警代码段经 gcc `-fsyntax-only` 编译 **0 error**，`pending_since`/`cleared` 生成逻辑符合预期。

### A7. 补全报警引擎生命周期 API（init / active_count）
- **问题**：报警引擎只有 `alarm_engine_evaluate`（求值）与 `alarm_acknowledge`（确认），缺少冷启动/热重载时的**复位入口**和查询活动报警数的**只读接口**，引擎无法完整闭环。
- **修复**：
  - 新增 `alarm_engine_init(void)`：用 `memset` 逐个清零所有 `_rec_<id>`（含 `pending_since`/状态/时间戳），供启动或重载时调用，避免上一周期残留状态影响新一批次判定；
  - 新增 `uint16_t alarm_get_active_count(void)`：遍历各记录统计 `state==1`（Active）数量，供 UI 报警灯/汇总条显示；
  - 报警段补 `#include <string.h>`（`memset`）。
- **测试**：`TrendAndAlarmCodeGenTests` 新增 1 用例（init + active_count 生成），`dotnet test` → **882 用例全部通过**（新增 17 例/总计）。
- **验证**：含这些 API 的报警代码段经 gcc `-fsyntax-only` 编译 **0 error**。

### A8. 边界保护 + 趋势数据链路可观测性
- **`alarm_engine_evaluate` 越界保护**：
  - 此前直接 `check_alarm(var_values[varIndex])`，`var_values==NULL` 或 `var_count < 启用报警数` 时读越界内存（C 未定义行为）。
  - 修复：函数开头 `if (var_values == NULL || var_count <= 0) return;`；循环内加 `if (varIndex < var_count)` 守卫，越界报警本次跳过，保持上次状态。
- **趋势 `trend_update` NULL 保护 + 数据点 trace**：
  - 新增 `values == NULL` 守卫（有启用 pen 时），避免 `values[i]` 解引用崩溃；同时刷新保留空表（不挂起）。
  - 新增 `TREND_TRACE` 在每帧推送时打印 `n` 与 `values[0]`，方便排查数据链路断点。
- **趋势 `datalog_sample` 空输入守卫**：
  - 新增 `if (values == NULL || count == 0) return;`，无有效数据不入环形缓冲。
- **测试**：`TrendAndAlarmCodeGenTests` 新增 2 用例（趋势 null 守卫 + 数据点 trace；报警 evaluate 越界守卫），`dotnet test` → **884 用例全部通过**（新增 19 例/总计）。
- **验证**：含这些改动的报警/趋势代码段经 gcc `-fsyntax-only` 编译 **0 error**。

### A9. 趋势引擎补全 CSV 数据导出
- **问题**：数据记录有环形缓冲，但嵌入式 HMI 通常需要把记录导出到文件/串口/网络，此前只有采样入口，没有导出 API。
- **修复**：
  - 每个 Logger 生成 `datalog_export_csv_{id}(void (*write_fn)(const char*))`：
    - 按时间顺序回放（满环时从最旧写起，未满时从头写起）；
    - 表头：`timestamp_ms` + 绑定的 `VariableIds`（未绑定时退化为 `v0/v1`）；
    - 每行 `timestamp_ms,v0,v1,...`（`snprintf` + callback 逐块写出）；
    - 跳过 `is_valid == 0` 样本（兼容 StopWhenFull 停止采样后的空位）。
  - 导出函数调用示例：`datalog_export_csv_log1([](const char* p) { serial_write(p, strlen(p)); });`。
- **测试**：`TrendAndAlarmCodeGenTests` 新增 1 用例（CSV 导出函数签名 + 表头 + 时间回放逻辑），`dotnet test` → **885 用例全部通过**（新增 20 例/总计）。
- **验证**：含 CSV 导出的报警/趋势代码段经 gcc `-fsyntax-only` 编译 **0 error**。

### A10. HmiProject 聚合 DataLogConfigs（补齐主流程数据类型断点）
- **问题**：`HmiProject` 有 `AlarmGroups`/`TrendConfigs`/`AnimationConfigs` 等聚合，却**没有 `DataLogConfigs`**。此前主流程调用 `GenerateTrendCode(project.TrendConfigs, Array.Empty<DataLogConfig>())`，logger 永远为空，导致环形缓冲与 CSV 导出（A9）代码**进不了主产物**。
- **修复**：
  - `HmiProject` 新增 `public ObservableCollection<DataLogConfig> DataLogConfigs { get; set; } = new();`（已 `using HmiEditor.Core.Models.Trend`）；
  - `GenerateFullProject` 与 `GenerateSharedLibraryProject` 均改为 `GenerateTrendCode(project.TrendConfigs, project.DataLogConfigs)`。
- **测试**：`TrendAndAlarmCodeGenTests` 新增 1 用例（`.so` 主流程注入 `DataLogConfigs` 后输出 `datalog_sample`/`datalog_export_csv`/变量表头），`dotnet test` → **886 用例全部通过**（新增 21 例/总计）。
- **验证**：`.so` 主产物 `docs/generated-samples/generated_hmi.c` 经该接入重新生成后含 `datalog_export_csv_log1`、`datalog_sample_log1` 与 `,v_temp` 表头。

### A11. 生成器 `.teardown` 从空函数改为状态清理（与固件泄漏修复互补）
- **问题**：`.teardown = NULL`（原生成器），固件 `hmi_ui_unload` 调用无操作。上一轮修复了固件侧的 `lv_obj_delete` 屏幕实例，但 **`.so` 内文件级 static 缓存（如 `_chart_{id}`）未复位**——重载后 `trend_update`/报警引擎仍持有已销毁对象的悬空指针，触发未定义行为。
- **修复**：
  - 生成器新增 `static void hmi_ui_teardown(void)`：
    - 有报警：调用 `alarm_engine_init()` 复位所有记录（含延时挂起）；
    - 有趋势图：把 `_chart_{id} = NULL` 清缓存指针；
    - 无资源：生成 `(void)0` 空操作。
  - `.teardown = hmi_ui_teardown`（绑定生成函数）。
- **验证**：`dotnet test` → **888 用例全部通过**（新增 23 例/总计）；teardown 片段 gcc `-fsyntax-only` **0 error**。

### A12. `hmi_ui_api_t` 升级 ABI V2：暴露报警/趋势引擎接口（修复"死代码"断点）
- **问题**：报警/趋势/数据记录引擎代码虽在 `.so` 内生成，但 `hmi_ui_api_t` 只暴露 `init`/`teardown`——固件**无任何调用入口**，整套引擎在真实运行中是不可执行的死代码。
- **修复**：
  - `hmi_ui_api.h` ABI 版本 `1u → 2u`，结构体新增 5 个可选回调（NULL=无）：
    - `alarm_evaluate(var_values, var_count)`、`alarm_acknowledge(config_id)`→`uint8_t`、`alarm_active_count()`；
    - `trend_tick(values, count)`；
    - `datalog_export_csv(log_idx, write_fn)`。
  - 生成器 `.so` 按配置绑定：报警存在→`alarm_engine_evaluate`/`alarm_acknowledge`/`alarm_get_active_count`；趋势存在→`trend_engine_tick`；有 logger→新增 `hmi_datalog_export_csv(int, void(*)(const char*))` 分发器（按 `DataLogConfigs` 顺序路由到各 `datalog_export_csv_{id}`）。
  - 修复过程中实证发现并纠正：`alarm_acknowledge` 返回类型曾在 ABI 声明为 `int`、生成代码为 `uint8_t` 不一致（gcc 捕获 `-Wincompatible-pointer-types`），统一为 `uint8_t`。
- **验证**：`dotnet test` → **889 用例全部通过**（新增 24 例/总计）；生成 `.so` 全码 + `hmi_ui_api.h` 真实头 gcc `-fsyntax-only`，ABI 相关错误 **0**；`screen_manager.c` 依赖新头编译 **0 error**。
- **遗留**：`host_test` 的 LVGL 配置不含 `lv_font_default`（生成器字体出处），该 gcc 报错为既有兼容问题、与本次无关，另记 D 节。

### A13. 宿主 main.c 周期接线报警/趋势引擎（打通引擎驱动链路）
- **问题**：上一轮 ABIV2 暴露了接口，但 `main.c` 主循环从未调用 `.alarm_evaluate`/`.trend_tick`——引擎仍不可触发。
- **修复**：新增 `static void hmi_engine_poll(void)`（main.c 前）：
  - 取 `hmi_ui_get_current_api()`，若 NULL 或未绑定 `alarm_evaluate`/`trend_tick`（旧 .so）直接返回（兼容 V1）；
  - 用静态 `float snap[TAG_COUNT]` 取变量快照（`var_raw_read(i)`）；
  - 调用 `api->alarm_evaluate(snap, TAG_COUNT)` 与 `api->trend_tick(snap, min(TAG_COUNT, 255))`；
  - 趋势 count 转 `uint8_t`（LVGL chart 环形槽数限制）。
  - 主循环末尾调用（在 `sys_settings_gate_poll` 之后、`usleep(5000)` 之前）。
- **验证**：`gcc -fsyntax-only` 编译 `app/main.c`（含完整 include 环境）**0 error**；`screen_manager.c` 依赖 V2 ABI 头 **0 error**；`host_test` `make` **0 错误**。

### A14. 修复 `lv_font_default`（既有 LVGL 9.5 兼容断点）
- **问题**：生成器 `GenerateThemeCode` 用 `&lv_font_default`（LVGL 8.x 符号），LVGL 9.5 不再提供该符号、统一用宏 `LV_FONT_DEFAULT`。这导致生成的 `.so` 代码用真实 LVGL 9.5 头语法验证时 **1 error**（`'lv_font_default' undeclared`）。
- **修复**：主题生成代码将 `&lv_font_default` 改为 `LV_FONT_DEFAULT`（LVGL 9.x 标准宏，由 `lv_conf.h` 配置默认字体）。
- **验证**：
  - `dotnet test` — **890 用例全部通过**（含新增 25 例/总计）。
  - **里程碑**：生成的 `.so` 全码（含屏幕/主题/趋势/报警/ABI V2 字段/CSV 导出）用 `host_test` 的**真实 LVGL 9.5 头** `gcc -fsyntax-only` 编译 **首次 0 error**——此前被 `lv_font_default` 阻塞。
  - 新增 `GenerateSharedLibraryProject_ThemeUsesLvFontDefaultMacro` 防止回归。

## B. 固件设置模块生命周期优化（`firmware/t113/app/settings/`）

> 背景：`sys_settings.c` 的 `show_tab()` 用 `lv_obj_clean(s_content)` 重建内容区，各模块持有的静态 `lv_obj_t*` 指针随之失效；若任何回调在这些对象重建前访问，即构成 use-after-free。同时触摸校准的捕获层/十字挂在**屏幕上**而非内容区，切 tab 不会自动清理，会遗留常驻覆盖层并卡死状态机。

### B1. 模块静态指针在重建前统一置 NULL
在 `set_net.c / set_serial.c / set_sound.c / set_display.c / set_storage.c / set_maint.c` 的 `set_*_create()` 入口，将所属静态 `lv_obj_t*` 清为 `NULL`，确保重建窗口期无悬空指针可被回调访问：
- `set_net`：`s_mode_dd/s_dhcp_sw/s_ta_{ip,mask,gw,dns}/s_status_lbl`；`s_kb` 常驻屏幕单独保留；
- `set_serial`：`s_dd_{dev,baud,data,stop,parity}/s_result_lbl`；
- `set_sound`：`s_slider/s_val_lbl/s_buzz_sw`；`set_display`：`s_slider/s_dd_blank/s_val_lbl`；
- `set_storage`/`set_maint`：`s_lbl`。

### B2. 触摸校准状态机泄漏 / UAF 修复（`set_touch_cal.c` + `sys_settings.c`）
- **问题**：
  1. `s_grab`（全屏捕获层）/`s_cross`（十字）挂在屏幕上，`lv_obj_clean(s_content)` 不清除 → 切 tab 后常驻覆盖层；
  2. `s_hint` 是内容区子对象，切 tab 被删除，但仍在跑校准时会执行 `lv_label_set_text(s_hint, ...)` → UAF；
  3. `s_running` 不重置 → 下次进入触摸页卡死无法重启。
- **修复**：
  - 新增 `set_touch_cal_abort()`：若在运行，置 `s_running=0`、删除捕获层/十字（`lv_obj_delete_async`）、置空 `s_hint`；
  - `sys_settings.c` `show_tab()`：离开触摸页时调用校准终止；`sys_settings_hide()` 退出设置页时同时收起网络键盘并终止校准；
  - `finish_cal`/`grab_click_cb` 中对 `s_hint`/`s_cross` 增加判空保护。

### B3. msgbox 回调自关闭安全性核实
审查 `set_maint.c` / `set_storage.c` 的确认弹窗：`dlg_btn_cb` 在回调内调用 `lv_msgbox_close(mbox)`。核实 LVGL 9.5 源码 `lv_msgbox.c` 内部使用 `lv_obj_delete_async` 延迟删除，**不存在 use-after-free**，故保留 `lv_msgbox_close`（语义正确的 API），并补充注释说明安全性依据。

### B4. 固件 OTA 热加载屏幕对象泄漏修复（`screen_manager.c`）
- **问题**：`hmi_ui_unload()` 在 `dlclose` 前仅将 `s_screens[i]` 指针置 NULL，**从不销毁已创建的屏幕 LVGL 对象**。每次 OTA 热加载（`hmi_ui_load` → `hmi_screens_init` → 重新 `create`）都在 LVGL 对象堆累积上一代全部屏幕/控件，长期升级内存持续泄漏。
- **修复**：`hmi_ui_unload()` 在 `dlclose` **之前**遍历 `s_screens[]`，对非 NULL 实例调用 `lv_obj_delete()` 并置空。顺序正确性：`lv_obj_delete` 仅操作宿主 LVGL 堆节点、不回调 `.so` 内代码，故须在 `dlclose` 前完成（`dlclose` 后函数指针已失效）。
- **验证**：`screen_manager.c` 以 LVGL 9.5 头参与 `gcc -fsyntax-only` 编译 **0 error**；`host_test` `make` 无回归。

## C. 测试补充（`HmiEditor.Core.Tests`）

新增 `TrendAndAlarmCodeGenTests.cs`（共 21 用例）：
- 趋势图：多 pen 保存各 series 且不再传 `NULL`；单 pen 正常；禁用 pen 被排除；无启用 pen 不崩溃且正确消参；空输入返回占位注释。
- 报警：多组报警生成独立检查函数；禁用报警被省略；空输入返回占位注释。
- 主流程接入：`GenerateFullProject` / `GenerateSharedLibraryProject` 在配置了趋势/报警/数据记录器时发出修复片段；空工程不产生额外符号。
- 确认功能：`alarm_acknowledge` 注册表 + config_id 生成；浮点字面量带小数点（`80.0f`）。
- 死区防抖：HighLimit / LowLimit 的 `cleared` 解除条件表达式；延时触发 `pending_since` 挂起/取消/激活。
- 生命周期：`alarm_engine_init` 复位 + `alarm_get_active_count` 计数生成。
- 边界保护：`alarm_engine_evaluate` 越界守卫；趋势 `trend_update` / `datalog_sample` 的 NULL 守卫 + 数据点 trace。
- 数据导出：`datalog_export_csv` 回调签名 + 表头（绑定变量名）+ 时间顺序回放 + `is_valid` 过滤。
- 数据类型断点：`HmiProject.DataLogConfigs` 注入主流程后 CSV/采样代码进入输出。
- 状态清理：`.teardown = hmi_ui_teardown` 绑定生成函数；含报警/趋势时清空 `_chart_{id}` 并调用 `alarm_engine_init()`；空项目生成 `(void)0` 空操作。
- ABI V2 接口：`.so` 绑定 `alarm_evaluate`/`alarm_acknowledge`/`alarm_active_count`/`trend_tick`/`datalog_export_csv` 并生成 CSV 分发器。
- 字体兼容：`lv_obj_set_style_text_font` 改用 `LV_FONT_DEFAULT`（LVGL 9.x 标准宏），`.so` 全码首次用真实 LVGL 9.5 头 gcc 语法验证 **0 error**。

**验证结果**：
- `dotnet test` — `HmiEditor.Core.Tests`: **890 用例全部通过**（含新增 25 例）。
- `host_test`（`firmware/t113/host_test`）`make` 编译 **0 错误**，`app/settings/` 全部模块（含本次改动）通过 LVGL 9.5 编译。
- **里程碑**：`.so` 全码（含主题/屏幕/报警/趋势/CSV/ABI V2）经 `host_test` 真实 LVGL 9.5 头 `gcc -fsyntax-only` 编译 **0 error**（A14 修复前被 `lv_font_default` 阻塞 1 error）。

## D. 遗留与建议
- ~~屏幕对象在 `.so` 卸载时无 `lv_obj_delete` 清理~~（**已解决**：见 A11+B4）。链路为**进程级 OTA**（替换 `/mnt/UDISK/hmi/libhmi_ui.so` 后 `killall hmi_runtime` 重启，进程退出 OS 回收全部堆），当前无进程内累积泄漏；`hmi_ui_unload` 在真实主循环不触发，B4+A11 是为未来**进程内热重载**预留的防御性正确性修复（届时 dlclose 前由固件 `lv_obj_delete` 屏幕 + `.so` `hmi_ui_teardown` 清内部状态）。
- `set_net.c` 跨 tab 常驻键盘对象长期占内存（有意防重建泄漏），如内存敏感可改为仅在进入网络页时惰性创建、离开时销毁。
- ~~**`lv_font_default` 与 `host_test` 的 LVGL 9.5 配置不兼容**~~（**已解决**：见 A14）。改用 `LV_FONT_DEFAULT` 宏后，`.so` 全码首次通过真实 LVGL 9.5 头语法验证。
- ~~**ABI V2 已暴露报警/趋势接口，但宿主侧尚无调用点**~~（**已解决**：见 A13）。`main.c` 主循环已调用 `hmi_engine_poll()` 周期喂变量快照，驱动 `.alarm_evaluate`/`.trend_tick`；对旧 V1 .so 向后兼容（指针 NULL 时静默跳过）。

