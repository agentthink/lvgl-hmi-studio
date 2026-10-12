# zhishu-hmi 全面审查与优化方向

> 审查日期：2026-09-13
> 审查基线：`main` @ `96509fe`（工作区仅 1 个未跟踪文件 `hardware/hmi-lceda-pro/LCEDA-PLUGIN-FEASIBILITY.md`）
> 审查范围：`src/`（14 项目 / 372 文件）、`tests/`（6 项目 / 113 文件）、`firmware/t113/`、`hardware/`、`tools/`、`scripts/`、`.workflow/`、`docs/`
> 方法：全量构建 + 全量测试 + 端到端验收脚本实跑 + 静态扫描 + 关键路径人工复核 + 与 LVGL 9.5 真实头文件交叉验证
> 说明：本文所有结论均标注了可复现证据；凡未亲自复现、来自辅助审查的条目均注明「待复现」。

> **阶段 0 执行结果（2026-09-13 更新）**：§6 阶段 0 的五项 P0 已全部完成，出口条件达成——
> `LVGL_DIR=~/Git/lvgl bash scripts/verify.sh` **退出码 0**（含真实 LVGL 9.5 CMake 编译 + 300 帧 selftest + CTest），
> `bash scripts/ci-gate.sh` 通过，全量测试 **1610/1610**，固件 host 测试 **70/70**。
> 执行过程中给 Golden 工程补上了 canvas 控件覆盖，随即又暴露出 3 类此前完全隐藏的生成缺陷
> （`lv_draw_line`/`lv_draw_arc` 在 LVGL 9.x 只接受 2 参数、过程控件更新函数被嵌套进函数体、
> `main.c` 软键盘使用 `lv_obj_set_hidden`/`lv_obj_set_click_focusable` 两个已移除 API），均已修复。
> 详见 `docs/project-status.md`。下文保留审查时的原始判断，未回改。
>
> **阶段 1 执行结果（软件可验证部分）**：§6 阶段 1 的第 9/10/8 项已完成——
> 新增 `BindingSemantics` 统一设计器/模拟器/固件的值显示与判真语义（生成代码真正输出缩放、
> 小数位、前后缀；单位作为后缀兜底进入设计器预览）、生成代码头加入生成器版本 + git commit、
> 编辑器新增 `AppLog`/`FileLogger` 与三层全局异常处理并把 MainWindow 关键操作改为可见失败提示。
> 第 6/7 项（触摸量程、BoardProfile）需 T113 交叉链与实机校准，另行推进。
> 全量测试 **1625/1625**、`verify.sh` 与 `ci-gate.sh` 均通过。
>
> **阶段 2 执行结果（部分）**：§6 阶段 2 的第 11 项前半已完成——模板里重复的两条绑定刷新路径
> （`GenerateSimRefreshBindings` / `GenerateDeviceRefreshBindings`）已合并为单一参数化发射器
> `GenerateRefreshBindings(project, tags, deviceMode)`，控件分支从两份变一份且生成输出逐字节不变；
> 新增 `RefreshBindingsParityTests` 对拍锁定两路径不再分叉。
> 后半（把 459 行的 `GenerateControlParameters` 按控件族拆分）**已尝试并回退**：
> case 体内含大量生成 C 的 `{{`/`}}` 字符串字面量，基于正则/括号计数的机械提取无法可靠区分
> 字符串内外，两次都破坏语法；应改用 Roslyn 语法树，作为独立重构轮次完成。
> 全量测试 **1629/1629**、`verify.sh` 与 `ci-gate.sh` 均通过。
>
> **阶段 2 后续：通信层整改（审查 §4 P1-5）已完成**——`TcpDeviceChannel` / `SerialDeviceChannel`
> 整帧串行化、超时统一为 `DeviceChannelException`（调用方取消仍抛 `OperationCanceledException`）、
> 读循环可取消与 CTS 释放、文件传输独占闸门；`ModbusTcpRuntimeDriver` 整段事务串行化、
> `async/await` 取代 `ContinueWith(t => t.Result)`、连接超时可取消、异步读显式超时、短帧协议校验。
> 新增 9 项 loopback 回归测试（短帧/超时/并发不串响应/并发必串行/取消语义/关闭不挂死）。
> 全量测试 **1638/1638**。

---

## 1. 执行摘要

项目**功能完成度与底层编码质量都不差**：编译 0 错误 0 警告、1600 个测试全绿、生成器广泛使用了中央序列化配置、C 字符串转义在多数路径已收敛、固件 ABI 有版本号且有明确的 host/`.so` 契约。

但审查发现的问题是**结构性且成体系的**，集中在三处：

1. **质量门禁是「纸面绿色」**。CI 门禁文件根本没有入库；唯一入库的流水线用 `| tail` 吞掉退出码，测试失败也是绿的；端到端脚本 `scripts/verify.sh` 在本机以退出码 2 失败（真实 LVGL 编译不过）。也就是说，**当前没有任何自动化机制能拦住一次破坏性的提交**。
2. **生成的 C 代码没有被真实编译过**。canvas 类控件（SVG/Tank/Pipe/Valve/Ellipse）调用了 LVGL 9.5 中**不存在**的两个 API，且像素缓冲少分配 25%；模拟器 `main.c` 的 SDL 头文件路径在标准 LVGL 检出下不存在。这三处叠加意味着「设计器里能画、模拟器里编不过、实机上要么编不过要么写越界」。
3. **UI 层是「静态事件总线 + 手工赋值 + 巨型 code-behind」**。引用了 `CommunityToolkit.Mvvm` 但全仓 0 处使用；`MainWindow.axaml.cs` 4933 行含 Git 客户端、编译下载流水线、Modbus 运行时和 239 行生产环境诊断代码；51 处 `async void` 中 18 处无 `try/catch`，且应用**没有全局异常处理器**——用户侧一次未处理异常就是无日志崩溃。

此外还有一个直接的产品缺陷：**触摸量程与屏参不一致**（固件按 1024×600，DTS 声明触摸控制器为 800×480），屏幕右侧/底部约 22% 区域触摸不可达。

**总体判断**：项目当前处于「功能面广、但闭环验证缺失」的状态。下一步投入产出比最高的不是新功能，而是**让门禁真的能失败、让生成的 C 真的被编译、把已生成的资产从 Mock 换成真工程**。这三件事做完，才能安全地进入功能扩展。

---

## 2. 实测基线（可复现）

| 检查项 | 命令 | 结果 |
|---|---|---|
| 编译 | `dotnet build HmiEditor.slnx --no-restore -warnaserror` | ✅ 0 error / 0 代码 warning（12 个 NU1900 为沙箱 NuGet 缓存只读，非代码问题） |
| 全量测试 | `dotnet test HmiEditor.slnx --no-build --no-restore` | ✅ **1600/1600** 通过（Core 1171、AI 237、Communication 70、Device 59、Simulator 41、Hardware 22） |
| 固件主机侧测试 | `bash firmware/t113/tests/run_host_tests.sh` | ✅ **70/70** 通过（零硬件依赖） |
| 端到端验收 | `LVGL_DIR=~/Git/lvgl bash scripts/verify.sh` | ❌ **退出码 2**：模拟器工程导出成功，真实 LVGL C 编译失败 |
| Shell 语法 | `bash -n <script>` | ❌ `hmi_ota.sh`、`container/bingpi-m2-build/docker-entrypoint.sh` 语法错误 |
| 分析器 | `Directory.Build.props` / `.editorconfig` | ❌ 未启用 `EnableNETAnalyzers`、无 `.editorconfig`、无 `TreatWarningsAsErrors` |

**规模画像**（全仓 936 个受跟踪文件，下表为主要代码/测试目录）：

| 模块 | 文件 | 行数 | 备注 |
|---|---:|---:|---|
| `HmiEditor.App` | 88 | 38,410 | 无 ViewModel；`MainWindow.axaml.cs` 4933 行、`DesignCanvas.cs` 4222 行 |
| `HmiEditor.Core` | 105 | 14,584 | 单一事实源、表达式、报警/趋势/配方模型 |
| `HmiEditor.CodeGen` | 10 | 5,028 | `CodeGenerator.cs` 3322 行（56 方法，前 8 个占 61% 行数） |
| `HmiEditor.AI` | 31 | 4,394 | LLM Provider、Agent 工具 |
| `HmiEditor.Device` | 13 | 2,730 | 下发/升级通道 |
| `Hmi.Communication.*` | 20 | 2,616 | Modbus/MQTT/OPC UA 驱动 |
| 其余（Simulator/Scripting/Renderer/Hardware） | 15 | 1,681 | `HmiEditor.Renderer` 仅 13 行占位 |
| `tests/` | 113 | 26,239 | 1421 个 `[Fact]/[Theory]` |

---

## 3. P0 —— 阻断发布 / 真实产品验证

### P0-1 CI 质量门禁无效（已复现）

- `.workflow/csharp-gate.yml`（唯一的 C# build+test 门禁）**未被 git 跟踪**，因为 `.gitignore:69` 忽略了整个 `.workflow/`。`git ls-files .workflow` 只有 `devkit-check.yml`。→ 这条流水线从未在仓库中生效。
- 唯一被跟踪的 `.workflow/devkit-check.yml`：
  - `:25-26` 两处 `pip install ... || true`；
  - `:27` `python -m pytest -q tests/ | tail -5`、`:29` `python scan_csharp.py | tail -10` —— 管道退出码取自 `tail`，且脚本无 `pipefail`。→ **测试失败流水线仍然全绿**。
- 两条流水线都不包含固件编译、`bash -n`、生成代码静态校验或 `scripts/verify.sh`。

**修复**：`.gitignore` 改为只忽略 `.workflow/` 下的本地文件（或删掉该行并入库 `csharp-gate.yml`）；流水线首行加 `set -eo pipefail`，去掉 `| tail` 与 `|| true`；把下面 P0-4/P0-5/P0-7 的检查项接入。

### P0-2 设备端 OTA 脚本语法错误，OTA 完全不可用（已复现）

- `firmware/t113/tina/package/hmi-runtime/files/hmi_ota.sh:69` 与 `:84`：
  ```sh
  /*) [ -f "$SOURCE" ] || { echo "..."; exit 1; } SWU_FILE="$SOURCE" ;;
  ```
  `}` 后缺少 `;` 分隔，`bash -n` 直接报语法错误（"未预期的记号 SWU_FILE=..."）。
- 该脚本由 `app/settings/set_maint.c:44` 调用。→ 维护页触发的固件升级路径整体不可用。

**修复**：改为 `|| { echo "..."; exit 1; }; SWU_FILE="$SOURCE" ;;`，并把 `bash -n` 加入 CI。

### P0-3 构建容器入口脚本语法错误（已复现）

- `container/bingpi-m2-build/docker-entrypoint.sh:14` 引号未闭合（`echo "工作目录: $PWD`），`bash -n` 在 `:26` 报 EOF。→ 容器入口不可用。
- 附带：`docker-compose.yml` 把 Tina SDK 挂载为 `:ro`，但构建需要写入；挂载的 `out` 路径与 `build.sh` 实际输出不一致；`build.sh:26` 的 lunch 名与 `firmware/t113/tina/build.sh:48` 冲突。

**修复**：补齐引号并做一次容器实跑验证；统一 lunch/挂载路径。

### P0-4 模拟器 C 编译在标准 LVGL 检出下失败（已复现）

`LVGL_DIR=~/Git/lvgl bash scripts/verify.sh` 失败于：

```
main.c:13: fatal error: include/lvgl/drivers/sdl/lv_sdl_window.h: 没有那个文件或目录
```

- 生成器硬编码 `src/HmiEditor.Core/Services/SimulatorProjectExporter.cs:192-194`：
  ```c
  #include "include/lvgl/drivers/sdl/lv_sdl_window.h"
  ```
- 标准 LVGL 9.5 中该头文件位于 `src/drivers/sdl/lv_sdl_window.h`（`lvgl/CMakeLists.txt` 通过 `os_desktop.cmake:216` 把 `${LVGL_ROOT_DIR}` 暴露为 public include，所以正确写法是 `"src/drivers/sdl/lv_sdl_window.h"` 或裸 `"lv_sdl_window.h"` 配合 include 路径）。
- 我实测：`#include "src/drivers/sdl/lv_sdl_window.h"` 语法检查通过，`"lv_sdl_window.h"` 与生成路径均失败。

**后果**：`docs/project-status.md` 中「真实 CMake 编译 + 300 帧 selftest + CTest」的门禁在本机不成立；生成代码长期未经真实编译。

**修复**：改为 `${LVGL_ROOT_DIR}` 相对路径；并在 CI 中固定一份 LVGL 9.5 检出，把 `clang -fsyntax-only` 作为硬门禁。

### P0-5 canvas 类控件：调用不存在的 API + 缓冲越界（已复现）

与 `firmware/t113/host_test/lvgl`（9.5.0）和 `~/Git/lvgl`（9.5.0）头文件逐一核对：

| 生成代码 | 事实 | 位置 |
|---|---|---|
| `lv_canvas_set_px_opa(...)` | LVGL 9.5 **不存在**（正确 API：`lv_canvas_set_px(obj,x,y,color,opa)`，`lv_canvas.h:89`） | `SvgToLvglConverter.cs:39`、`ProcessControlCodeGen.cs:54`、`ShapeControlCodeGen.cs:54` |
| `lv_draw_polygon` / `lv_draw_polygon_dsc_init` | LVGL 9.5 **不存在**（存在 `lv_draw_triangle`，`lv_draw_triangle.h:65`） | `SvgToLvglConverter.cs:245-250`、`ProcessControlCodeGen.cs:126-130`、`ShapeControlCodeGen.cs:68-73` |
| `static lv_color_t buf[w*h]` | `lv_color_t` 是 **3 字节**（`lv_color.h:109-113`），而缓冲格式为 `LV_COLOR_FORMAT_ARGB8888`（4 B/px）→ **少分配 25%** | `SvgToLvglConverter.cs:24`、`ProcessControlCodeGen.cs:25`、`ShapeControlCodeGen.cs:22` |

**后果**：SVG 导入图形、Tank、Pipe、Valve、Ellipse 全部**无法编译**；一旦修好 API 而不同时修缓冲，就会变成静态区/堆越界写。`GeneratedCodeValidator.cs:275-278` 还把这两个不存在的 API 列入了白名单，因此生成前校验给出**假阴性**。

**修复**：① 缓冲改用 `LV_CANVAS_BUF_SIZE(w,h,bpp,stride)` / `LV_DRAW_BUF_DEFINE`；② 用 `lv_canvas_set_px` + `lv_draw_triangle` 三角扇替换多边形；③ 白名单改为从真实头文件抽取符号，而非手写列表。

### P0-6 C 生成的字面量与格式串硬化不一致（已复现）

- **格式串注入**：变量单位被直接插进 printf 格式串（`CodeGenerator.cs:714`、`:899`）：
  ```c
  lv_label_set_text_fmt(obj_x, "%.1f%.1f", ...)   /* 单位="%" 时 */
  ```
  百分比单位是工业 HMI 最常见的单位之一；生成结果轻则显示错乱，重则（单位含 `%n`）造成实机内存写。`EscapeCString`（`:2739-2744`）只处理 `\ " \n \r \t`，**不处理 `%`**。
- **未转义的用户文本**：
  - `CodeGenerator.cs:437` 直接把 `screen.Name` 写入 C 字符串（屏幕名是自由文本，默认 `"Screen 1"`）；
  - `CodeGenerator.cs:2088`、`:2134` 把动作目标名写入 `hmi_screen_switch("...")` / `hmi_run_script("...")`；
  - 报警名进入 `ALARM_TRACE` 字符串（`:1175-1215`）；
  - `RuntimeTagsCodeGen.cs:63` 完全不转义（同文件 `:185` 却有 `Escape`）；
  - `ScriptDispatchCodeGen.cs:98-111` 不处理换行与 `*/`，而脚本名被放进块注释（`:65`）。
- **区域性缺陷**：`RuntimeTagsCodeGen.cs:283` 用当前区域性格式化浮点，且极小值会产出非法的 `0f`——这正是 `CodeGenerator.CFloat`（`:1977-1984`）已修复、但标签表路径仍在用的旧缺陷。

**修复**：收敛为**唯一**的 C 字面量/标识符/格式串生成器（InvariantCulture、转义 `%`、`*/`、引号、反斜杠、换行），并加「含引号/百分号/中文的工程名与变量名」回归测试。

### P0-7 触摸量程与屏参不一致，约 22% 区域触摸不可达（已复现）

- `firmware/t113/app/drivers/touch_evdev.c:21-26` 硬编码 `HMI_TOUCH_MAX_X=1024 / Y=600`，全仓**无任何编译期覆盖**（我检索了 tina/app/config/scripts/tests/container）。
- 已跟踪的板级 DTS（`bingpi_m2/.../board.dts:1083-1086`）明确：`ctp_screen_max_x = 0x320 (800)`、`ctp_screen_max_y = 0x1e0 (480)`，注释写明「触摸配置保留 800×480」。
- 现有换算 `x = raw * LCD_WIDTH / HMI_TOUCH_MAX_X` 在分母取 1024 时退化为 1:1，触控器最大只报 800 → 屏幕右侧 224px、底部 120px 不可点。
- 文档之间也自相矛盾：`firmware/t113/tina/dts/LCD_RGB24_1024x600.md:210` 说要改成 1024×600，`docs/wavepi-s6-migration-plan.md:97` 说要改成 `0x400/0x258`，而 DTS 保留 800×480。

**修复**：把触摸量程并入板级配置（见 P1-4），用真实触摸坐标范围实测校准，并补一条「触摸四角可达」的实机验收项。

### P0-8 编辑器无全局异常处理，崩溃无痕迹（已复核）

- `src/HmiEditor.App/App.axaml.cs` / `Program.cs` 中**没有** `DispatcherUnhandledException`、`AppDomain.UnhandledException`、`TaskScheduler.UnobservedTaskException` 任何一处。
- 51 处 `async void` 中 **18 处无 try/catch**（辅助审查定位，例如 `MainWindow.axaml.cs:2230` `NewProject`、`:2313` `SaveProject`、`DeviceManagerView.axaml.cs` 6 处、`ProjectExplorerView.axaml.cs:477/493/531` 等）。
- 全仓无日志抽象（`ILogger` 仅出现在两个 Loader 项目），163 个 `catch` 中约 101 个既不记日志也不重抛；`ModbusRuntimeSource.cs` 的 7 处 `catch { return false; }` 会把通讯故障完全吞掉。

**修复**：入口挂全局异常处理器（记录 + 友好提示 + 可选上报）；给 18 处 `async void` 补 `try/catch`；引入最小日志抽象（文件 + 控制台）。

---

## 4. P1 —— 架构与跨端契约

### P1-1 生成的 C 与设计器/模拟器语义不一致

| 语义 | 设计器 | PC 模拟器 | 固件 `.so` |
|---|---|---|---|
| 数值格式化 | `PreviewValueResolver.FormatDisplayValue`（支持小数位/缩放/前后缀） | `CodeGenerator.cs:714` 硬编码 `%.1f` | `:899` 硬编码 `%.1f` |
| `ValueScaleFactor / ValueDecimalPlaces / Prefix / Suffix` | 生效 | **只写成注释**（`:1804-1819`） | **只写成注释** |
| 表达式绑定 | `ExpressionBindingEvaluator` 求值 | **显式跳过**（`:652`、`:828`） | **显式跳过** |
| 布尔判定 | 非零即 ON（`PreviewValueResolver.cs:142/151/182`） | `> 0.5f`（`:760`） | `> 0.5f`（`:881/889`） |

→ 同一个工程在设计器显示 `12.34`、设备显示 `12.3`；`0.3` 在设计器是 ON、在设备是 OFF；表达式绑定只在设计器能用（且 `ExpressionEngine.ToCExpression` 生成的 `hmi_get_variable_value` 在固件中不存在，结果被写进注释）。

**修复**：在 Core 抽出一份 `BindingSemantics`（缩放/小数位/单位/前后缀/布尔阈值/表达式），三条路径都调用它并各自生成对应 C 片段；表达式无法下沉时，生成前必须报 Error 而不是静默注释。

### P1-2 生成器结构：巨型方法与双份实现

- `GenerateControlParameters` **461 行**单方法处理所有控件类型（`CodeGenerator.cs:2278-2738`）；前 8 个方法占文件 61% 行数；**921 处** `StringBuilder.Append/AppendLine` + 507 处插值，无中间表示。
- **同一控件的刷新逻辑写了两遍**：`GenerateSimRefreshBindings`（`:639-797`）与 `GenerateDeviceRefreshBindings`（`:813-972`）对 Label/Bar/Slider/Gauge/Knob/Arc/Led/Switch/CheckBox/Table 逐类型各写一套；两者守卫条件都不同（设备路径有 `if (obj_x)`，模拟器路径没有；`_cur_/_curtri_/_knobtri_` 的 `lv_obj_align` 落在守卫之外）。
- canvas 发射器**三份**（`SvgToLvglConverter` / `ProcessControlCodeGen` / `ShapeControlCodeGen`）各自复制 `Hex/Num/AppendPointsArray`；再加上设计器 `DesignCanvas.RenderGauge/RenderTank/...`，同一控件最多四份平行实现——P0-5 的缺陷正是这种复制的必然产物。

**修复**：抽 `IControlEmitter` + 统一的 `RefreshBindingEmitter(valueSource)`（模拟器传正弦波源、设备传 `get_tag_value`）；控件元数据表驱动，设计器/模拟器/固件共用。

### P1-3 UI 层：MVVM 依赖是装饰，code-behind 是上帝对象

- `HmiEditor.App.csproj` 引用 `CommunityToolkit.Mvvm 8.4.0` 与 `Dock.Model.Mvvm`，但全仓 `ObservableObject` / `RelayCommand` / `[ObservableProperty]` **0 处**；1365 个 XAML 绑定里 1282 个（94%）只是本地化索引器。
- `MainWindow.axaml.cs` 4933 行 / ~433 方法：`WireEvents` 单方法约 995 行；内含 Git 客户端（`:4136-4380`）、编译/下载流水线（`:2547-2906`）、Modbus 预览运行时（`:3565-3830`）、快捷键 switch（`:3112`，163 行），以及 **239 行生产环境事件路由诊断** `RunDockEventRoutingDiagnostic`（`:1863`）。
- 面板间通信依赖 `DockLayoutState` 的 30+ 个进程级静态事件（并且源码注释明确要求不要用 `DataContext`）。
- 可清晰切分的边界：`ProjectFileService`、`BuildOrchestrator`、`PreviewRuntimeController`、`GitPanelController`、`ControlFactory`、`PropertyBinder`。

**修复**：先做「提取服务」而非「全套 MVVM」——把 Git、编译下载、预览运行时、诊断代码移出 `MainWindow`；诊断代码改为开关式（默认关闭）；否则删除 `CommunityToolkit.Mvvm` 依赖以免误导后来者。

### P1-4 板级配置散落，无单一 BoardProfile

- 板级选择是编译期宏 `HMI_BOARD_WAVEPI`，`#if` 散落在 `app/runtime/runtime_config.h:39-59`、`app/settings/set_serial.c:27-33`、`app/drivers/sunxifb.h:30-33`、`app/comm/modbus_rtu.c`。
- 同一份信息在 `firmware/t113/firmware.json`（IDE 侧，已含 `boards[].debugUart/rs485Uart/rs485Mode/rs485GpioPin`）里也有一份，但固件构建不从它生成。
- `firmware/t113/tina/build.sh:71-72` 用 `sed -i` 直接改**被跟踪**的 `runtime_config.h` → 每次构建污染工作树、不可复现；`check_sdk_config()`（`:148-169`）缺 `CONFIG_PACKAGE_hmi-runtime=y` 也只警告不失败，会静默产出无 HMI 的固件。

**修复**：建立单一 `BoardProfile`（显示/触摸/调试串口/RS485 串口+方向 GPIO/内存策略/资源路径），由 `firmware.json` 生成 C 头文件与启动脚本；构建脚本不再就地改写源文件；关键 SDK 配置缺失时硬失败。

### P1-5 通信与设备层：并发与生命周期（辅助审查，建议复现）

- `ModbusTcpRuntimeDriver`：`connectTask.Wait(...)` 同步阻塞并泄漏未观察任务；注释声称锁内并发安全，但 I/O 在 `Task.Run` 中脱离锁执行 → 同一流上读写交错；`ContinueWith(t => t.Result)` 把异常包成 `AggregateException`；`response[2+i*2]` 无长度校验，短帧抛 `IndexOutOfRangeException` 而非协议错误；`IsConnected` 依赖会过期的 `TcpClient.Connected`。
- 通道层：`SerialDeviceChannel` / `TcpDeviceChannel` 的完成判定只靠 300ms 静默（慢输出会被截断）；`EnsureOpen` 直接抛错、**无自动重连**；`CloseAsync` 取消 CTS 但从不 `Dispose`；TCP 读循环无 CancellationToken → 半开连接永久阻塞；超时抛 `OperationCanceledException` 而非文档约定的 `DeviceChannelException`。
  - **2026-09-16 已修**（"完成判定"这一项）：判据收成唯一实现 `CommandFrameCollector`，改为"从**最后一段数据**到达时刻起算静默"，并把首字节宽限拆成独立窗口（`FirstByteTimeoutMs`，且不小于静默期）；两个通道不再各持一份收帧代码。详见 `docs/2026-09-16-channel-frame-completion-contract.md`。其余各项见本节的"修复"段。
- 两块 `_hasBase64` 缓存无同步。

**修复**：用 `SemaphoreSlim` 包住整帧请求/响应；`await ConnectAsync`；读循环传 CT；统一异常类型；加自动重连与 `IAsyncDisposable`；补短帧/断线/并发回归测试。

### P1-6 验证能力不足

- `GeneratedCodeValidator` 只查：空项目、id 重复、`{{}}` 残留、表达式语法、悬空引用、是否含 `create_screen_`、白名单外的 `lv_*`。**不查**：可编译性、转义、缓冲尺寸、声明/引用一致性、格式串。
- 白名单手写且已过期（把不存在的 API 当合法；缺 `lv_table_set_column_count`，而 `LvglApiCompatChecker` 恰好会改写它）。
- 测试以「生成代码包含某字符串」为主：Core.Tests 全部约 2860 个断言中 `Assert.Contains` **875 个（约 30%）**，其中约 37% 直接断言生成的 C 符号；此外 `PropertyPanelPersistenceGuardTests` 直接 `File.ReadAllText` 读 `DesignCanvas.cs` 源码再 grep——重构即碎、行为不变也碎。
- **没有任何测试真正编译生成的 C**（`T113SoBuilderTests` 用假 gcc shell 脚本）。无覆盖率工具、无 Avalonia.Headless UI 测试。

**修复**：CI 增加 `clang -fsyntax-only -I <lvgl> <生成的 C>`；断言从「字符串包含」逐步迁移到「编译通过 + 关键语义」；白名单从头文件抽取。

---

## 5. P2 —— 工程质量与仓库卫生

### P2-1 文档与真实状态漂移

- `docs/project-status.md:10,16` 写「1601/1601」，`:26` 写「1600/1600」，实测 **1600**；HEAD 记录为 `e1d61fd`，实际 `96509fe`；「验证记录」声称 `scripts/verify.sh` 通过，实测**退出码 2**。
- `docs/development-plan.md:25-30` 的 Phase 0 全未勾选，而 `project-status.md:17` 声称已完成。
- `firmware/t113/docs/test-report.md:13` 写 host 测试「50/50」，实测 **70/70**；`:67,80` 写自启脚本 `S99hmiruntime`，实际是 `S97hmiruntime`（`version-info.md:91` 也写错）。
- `firmware/t113/README.md:10,27,29,33,50` 仍是 LVGL 8 / 4MB 的过期描述。
- 版本三处漂移：`hmi-runtime/Makefile:19` PKG_VERSION `1.0.0` vs `firmware.json:35` `1.1.0`；LV_MEM `firmware.json` 4MB vs `config/lv_conf.h:39` 8MB vs `S97hmiruntime:31` 16MB；工具链前缀 `arm-linux-gnueabihf-` vs `arm-openwrt-linux-`。

**修复**：把 `project-status.md` 改为**由脚本生成**（构建/测试/verify 的真实输出），禁止手写数字；文档中「已完成」必须附可复现命令。

### P2-2 出厂生成物是测试夹具

- `firmware/t113/generated/screens.c:6` 头部写明 `Project: Mock中文显示测试工程 v2.0.0-test-cn-img`，且无生成器版本 / git commit（违反 `docs/development-plan.md:9` 自身要求）。
- 意味着「已部署固件」里的 UI 实际是 Mock 工程，不能作为任何功能验收证据。

**修复**：把 Mock 生成物移出 `firmware/t113/generated/`（或标注为 fixture），入库前用真实工程重新生成并记录生成器版本 + commit。

### P2-3 仓库与制品卫生

- 根目录有 16 个**已跟踪**的临时产物：`sim-*.png` ×7、`rs485_*`、`debug-dock-blank-after-drag.md`、`designer-hardcode-audit.md`、`hmi-design-*.md`、`simulator-stretch-mode-diagnosis.md`、`overview.md`（名为 overview 实为单个 TASK 报告）。
- `.trae-html-share-packages/*.zip`（1.3MB）已入库且未被忽略。
- 被忽略但仍占磁盘：`.dbg/` 207MB、`sim-output/` 82MB、`sim-output-mock/` 72MB（合计 361MB）；`firmware/Tina-Linux` 26GB + `firmware/Tina-SDK` 30GB = **56GB**。
- 无根 `README.md`、无 `.editorconfig`、无 `LICENSE`。
- 死代码：`src/HmiEditor.Renderer`（13 行空 `Render()`，仅被 App 引用，无实例化）；`LvglApiWriter.cs`（仅测试引用）；`controls` 里若干 `RenderXxxPlaceholder`。
- 陈旧脚本：`build/*.ps1`（Windows PowerShell + NativeAOT 时代的 `publish-linux-arm64.ps1`）已不被任何 CI/文档（除 release notes）引用，与 `scripts/verify.sh` 双轨；`targets/` 空目录。
- SDK 双树混乱：`mq_r` 配置在 `firmware/Tina-Linux`、`bingpi_m2` 在 `firmware/Tina-SDK`，14 个文件被手工强跟踪。

**修复**：根目录产物移入 `docs/`（或 `docs/assets/`）并从 git 移除；忽略 `.trae-html-share-packages/`；补 `README.md`（构建/测试/烧录入口）、`.editorconfig`、`TreatWarningsAsErrors`；删除死代码与陈旧 `build.ps1`；统一 SDK 目录约定并在文档中说明。

### P2-4 明文凭据

- `firmware/t113/app/runtime/runtime_config.h:79-83`：MQTT `hmi/hmi123` + `192.168.100.10`。
- `firmware/t113/scripts/deploy.sh:17-18`：`root/tina`。
- 编辑器侧 `LlmConfigStore` 把 API Key 明文写入用户目录（已注释说明，可接受，但建议接 OS keyring）。
- 配置目录命名也不统一：`App.axaml.cs` 用 `%APPDATA%/zhishu-hmi`，`LlmConfigStore.cs:23` 用 `%APPDATA%/HmiEditor`，临时目录另有 `HmiEditor`/`zhishu-hmi` 混用。

**修复**：凭据改为首次启动生成 + 环境变量/配置文件覆盖，默认口令必须强制修改；统一配置目录常量。

---

## 6. 优化路线图（按投入产出排序）

### 阶段 0：让「绿」变成真的（1–2 天，P0）

1. 修 `hmi_ota.sh:69,84` 与 `docker-entrypoint.sh:14` 语法错误；CI 加 `bash -n` 全量脚本扫描。
2. 让门禁真能失败：入库 `csharp-gate.yml`、去 `| tail`/`|| true`、加 `pipefail`；CI 接入 `run_host_tests.sh`（已有 70 例，零硬件依赖）。
3. 修模拟器 SDL 头路径（`SimulatorProjectExporter.cs:192-194`）+ `SimulatorBuildService.cs:167/242` 的相对路径 -> `Path.GetFullPath`（相对 `workDir` 时 CMake source dir 会被拼成两层）。
4. 修 canvas 缓冲尺寸与两个不存在的 API（`SvgToLvglConverter`/`ProcessControlCodeGen`/`ShapeControlCodeGen`）。
5. 统一 C 字面量/格式串硬化（`%`、引号、`*/`、InvariantCulture），并覆盖 `screen.Name`、动作目标、报警名、`RuntimeTagsCodeGen.cs:63/283`。

**出口条件**：`verify.sh` 退出码 0；CI 能在故意引入的缺陷上变红；含引号/百分号/中文名的工程可编译。

### 阶段 1：把闭环补上（1 周，P0/P1）

6. 触摸量程并入板级配置并实机校准四角可达。
7. `BoardProfile` 单一来源 + 构建脚本去 `sed -i` 副作用 + SDK 关键配置硬失败。
8. 把 Mock 生成物换成真实工程，生成物记录版本 + commit。
9. 提取 `BindingSemantics`，让值显示配置与表达式绑定真正进入生成代码。
10. 编辑器全局异常处理 + 18 处 `async void` 补 `try/catch` + 最小日志抽象。

### 阶段 2：结构与可维护性（2–4 周，P1）

11. 合并两条 refresh-bindings 生成路径，抽 `IControlEmitter`；按控件族拆分 `CodeGenerator`。
12. `MainWindow` 服务化拆解（Git / BuildOrchestrator / PreviewRuntime / 诊断代码），删除或真正启用 `CommunityToolkit.Mvvm`。
13. 通信层并发/重连/生命周期整改 + 短帧与断线回归测试。
14. 测试策略升级：真编译门禁、白名单头文件化、源码 grep 式测试替换为行为测试；引入覆盖率。

### 阶段 3：工程化与发布（持续，P2）

15. 仓库卫生（根目录产物归档、README/editorconfig、死代码与陈旧脚本清理、SDK 目录约定）。
16. 版本/ABI/工具链单一来源 + 生成 manifest（镜像 SHA256、IDE 版本、CodeGen ABI、板级配置）+ ABI 比对测试。
17. 文档由脚本生成状态数字；合并过期文档与硬件双基线。

---

## 7. 本次审查的置信度与未覆盖项

- **已亲自复现**：构建、全量测试、固件 host 测试、`verify.sh` 失败、`bash -n` 失败、CI 未跟踪、canvas API/缓冲、`lv_color_t` 尺寸、格式串 `%`、未转义屏幕名、触摸量程、`build.sh` 的 `sed -i`、Mock 生成物、配置目录不一致、无全局异常处理、死代码 Renderer。
- **来自辅助审查、建议二次复现**：`ModbusTcpRuntimeDriver` 并发细节、`SerialDeviceChannel` 生命周期、`MainWindow`/`DesignCanvas` 逐行方法清单、画布性能热点（`HitTestControl` 的 LINQ 全排序、`ParseColor` 每帧分配）、`.dbg`/`sim-output` 具体体积以外的磁盘项、部分固件文档行号。
- **未覆盖**：T113 交叉编译链下的固件整编与实机验证、RS485/触摸硬件链路、LVGL 版本升级矩阵、AI Provider 的联网行为与密钥安全、硬件工程的电气正确性（仅确认目录与跟踪状态）。

> 建议把阶段 0 的五项作为下一次提交的**唯一目标**，并在此之前不要合并任何新控件或 AI 功能——当前生成链路存在「编译不过」与「写越界」两类缺陷，任何在既有缺陷之上叠加的功能都会放大回归成本。
