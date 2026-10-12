# 项目状态基线

> 本文件由开发计划维护，禁止使用历史文档数据替代现场验证。
> 所有数字必须来自可复现命令的真实输出，不得手写。
>
> **下面的头部快照保留为历史基线（截至 2026-09-17），不随每一轮改写；
> 「最新现场读数」以文件末尾最新的「阶段 N」小节为准。**

- 记录日期：2026-09-17
- 分支：main
- HEAD：**2de6023**（功能状态提交 = `b374529` 收帧判据时序 + `8d7e44d` 门禁第 3 步拆两段 +
  **`2de6023` 端到端验收复跑**；其后的纯 docs 对齐提交不再回填）
  —— 阶段 11（LLM 流式）、阶段 12（配置诚实性收口二）、阶段 13（登记表全量复核 + 本地化即时刷新）、
  阶段 14（`ProjectSettingsView` 运行时用户表行为测试）、阶段 15（`ProjectSettingsView` 配置往返保真）、
  阶段 16（登记表 §4 第 18～20 项：编辑器与判据同口径）、
  阶段 17（登记表 §4 第 22～25 项）、阶段 18（Dock 真实拖拽可行性 + 回归网）、
  阶段 19（本地化：硬编码中文清单 + 只减不增守卫）落在其后的提交/工作区，
  本文件如实记录各阶段读数
- 工作区：干净
- 解决方案：HmiEditor.slnx
- 构建：通过，0 Error / 0 代码 Warning
- 测试：**2686 项**（Core **1915**、AI **315**、**App.UiTests 227（headless UI）**、
  Device 76、Communication 75、Simulator 44、Hardware 34）。
  起源基线 2535 ⇒ **+151**：阶段 11（LLM 流式响应）**+42**
  （见 `docs/2026-09-17-llm-streaming-sse.md`）、阶段 12（配置诚实性收口二）**+18**
  （见 `docs/2026-09-17-config-honesty-export-gate-and-ai-tool.md`）、
  阶段 13（登记表复核 + 本地化即时刷新）**+6**
  （见 `docs/2026-09-17-open-items-register.md`）、
  阶段 14（`ProjectSettingsView` 运行时用户表行为测试）**+13**
  （见 `docs/2026-09-17-projectsettings-runtime-user-ui-tests.md`）、
  阶段 15（`ProjectSettingsView` 配置往返保真）**+10**
  （见 `docs/2026-09-17-projectsettings-roundtrip-fidelity.md`）、
  阶段 16（表外 LVGL 版本 / 死代码回退 / 口令字节数）**+4**
  （见 `docs/2026-09-17-config-editor-value-normalization.md`）、
  阶段 17（超时默认值 / 差值判据 / 撤销一条错登记 / host 编译警告）**+2**
  （见 `docs/2026-09-17-timeout-flaky-judgment-and-stale-registration.md`）、
  阶段 18（Dock 真实拖拽可行性 + 回归网 + `Build` 不 Detach 的实测依据）**+6**
  （见 `docs/2026-09-17-dock-real-drag-feasibility-and-reparent-evidence.md`）、
  阶段 19（本地化清单 + 只减不增守卫 + 首批 14 条 XAML）**+5**、
  阶段 20（本地化判据修正：修掉守卫自身的漏报 + 命令目录收口 + Lang 键闭合守卫）**+9**、
  阶段 21（`ConfigFieldInventory` 悬停说明入字典 + 门禁改为警告即失败）**+2**、
  阶段 22（`UiOperationGuard` 操作名改成键 + 源码契约守卫）**+3**、
  阶段 23（设备日志面板正文 43 条清零 + `LogTagged` 实参个数判据）**+2**、
  阶段 24（`VariableManagerView` 显示名 25 条入字典 + 键↔枚举配对守卫）**+6**、
  阶段 25（`MainWindow` 22 条入字典 + 失败模板收成唯一实现）**+1**、
  阶段 26（一批小文件 11 条 + 诊断 sink 规则漏网 + 主题说明接上界面）**+3**、
  阶段 27（最后 31 条清完 + 正则漏网 + 日志框按列宽补齐）**+6**、
  阶段 28（第 27c 项定性推翻 + 落盘默认名 9 条 + 扫描面清零）**+1**
  （见 `docs/2026-09-17-persisted-content-language-decision.md`）、
  阶段 29（版本标记四者对应 + 修掉 LVGL 目标不兼容）**+12**
  （见 `docs/2026-09-17-version-marker-correspondence.md`）
- 固件 host 测试：通过，**8 步全部通过**（`firmware/t113/tests/run_host_tests.sh`）。
  阶段 10 **固件侧零改动** ⇒ 步数与各步断言数同阶段 9（通讯与变量表 70；触摸归一化 16/16/15；
  板级配置 18/18/9；密码学原语 40；会话与权限 40；凭据校验 41；会话超时与审计 56；
  运行时参数下沉 8×4 场景，合计 **371 项**），明细见「阶段 9」段
- 端到端验收：**2026-09-17 复跑通过**。`TMPDIR=$HOME/.cache/zhishu-hmi-verify \
  LVGL_DIR=~/Git/lvgl bash scripts/verify.sh` **连跑两轮退出码 0**（第 1 轮 392 s、第 2 轮 223 s）。
  逐段读数：大队 **2527 项全绿**（Simulator 44 / Hardware 22 / Communication 75 / Device 76 /
  AI 276 / App.UiTests 142 / Core 1892）+ git 用例 **8/8**（9 s）+ Golden 工程
  **2 screens / 36 controls** + 真实 LVGL 9.5 CMake 全量编译 → `hmi_sim` →
  **300 帧 selftest PASS** → **CTest passed**。
  脚本本轮改为 **7 步**：第 3 步拆两段（与 `ci-gate.sh` 同源）、新增 `CONFIG`
  （默认 **Release**，与门禁对齐；`CONFIG=Debug` 可复现旧行为）、第 7 步补生成产物断言。
  **「最后成功 2026-09-13 ⇒ 本项待复验」的标注就此撤销**；三个独立阻塞的归因见
  `docs/2026-09-17-verify-end-to-end-revalidation.md`（其中「`App.UiTests` 在 Debug
  全量并发下 testhost 崩溃」**未修、本轮只绕开**；同日复验后该判据收窄为「**仅带 `--blame-hang` 时复现**」、
  且「方案 A 修法有效」的结论**已撤回**，登记为当前阻断项第 12 条；
  同轮另修掉一处**构建期**缺陷：Avalonia 资源目录里 `/Assets/Icons.axaml` 重复键
  （同一个 `.axaml` 被同时声明为 `AvaloniaResource` 与默认 glob 的 `AvaloniaXaml`），
  它才是 Release 门禁那 12 条失败的根因，与并发无关）
- 质量门禁：`scripts/ci-gate.sh` **6 步**（`bash -n` 全量脚本 / 路径卫生 / Release 构建 /
  大队测试 / spawn git 的用例单独跑 / 固件 host 测试）。**末行会自报步数**，改步数时以它为准。**第 3 步已拆两段**（大队 + spawn git 的用例单独跑）——
  阻断项 11 已定性为**环境**（本沙箱对「首次出现的**文件**路径」收约 60 ms 且全局串行），
  并据此修复，**判据一条未放宽**；取证见 `docs/2026-09-17-sandbox-newpath-serialization.md`。
- 发布状态：未达到发布门禁

## 复现命令

```bash
# 快速门禁（本地与 CI 共用，失败即返回非 0）
bash scripts/ci-gate.sh

# 端到端验收（需要真实 LVGL 源码；默认 CONFIG=Release，与门禁对齐）
# 本机 /tmp 仅 10 MB，必须把临时根指到有空间的目录（脚本自带早失败提示会拦住你）
TMPDIR=$HOME/.cache/zhishu-hmi-verify LVGL_DIR=~/Git/lvgl bash scripts/verify.sh

# 覆盖率（**不是门禁**，见阶段 36）：跑全量并汇总，产物在 .workbuddy/tmp/coverage/<时间戳>/
# 脚本自己把 coverlet 的 TMPDIR 指到仓库外大分区（单份报告最大 9.5 MB > 10 MB 的 /tmp）
bash scripts/coverage.sh                # 自己构建
bash scripts/coverage.sh --no-build     # 已构建过，36 s

# 汇总脚本自身的判据（正控 2 + 负控 3），改动 coverage-summary.py 后跑一遍
bash tools/probe-coverage-summary.sh
```

## 阶段 0（P0）完成情况 — 2026-09-13

已消除「纸面绿色」的根因：门禁能失败、生成代码被真实编译。

1. **脚本语法**：修复 `hmi_ota.sh`（缺 `;` 导致 OTA 完全不可用）与 `container/bingpi-m2-build/docker-entrypoint.sh`（引号未闭合）；`bash -n` 全量 18 个脚本已纳入门禁。
2. **CI 真实化**：`.gitignore` 不再忽略 `.workflow/`，`csharp-gate.yml` 可入库并改调 `ci-gate.sh`；`devkit-check.yml` 去掉 `|| true`、加 `set -euo pipefail`。
3. **模拟器可编译**：修正 LVGL SDL 头路径（`src/drivers/sdl/...`）与 `SimulatorBuildService`/`SimExport` 的相对输出路径（`Path.GetFullPath`，旧行为会拼成 `sim/sim`）。
4. **canvas 控件可用**：缓冲改为 `LV_CANVAS_BUF_SIZE` 按字节分配（旧写法少 25%，越界写）；替换 LVGL 9.x 不存在的 `lv_canvas_set_px_opa` / `lv_draw_polygon`；`lv_draw_line`/`lv_draw_arc` 改为 2 参数 dsc 形态；过程控件更新函数移出函数体（原先嵌套定义，GCC 报"存储类无效"）。
5. **字面量/格式串硬化**：`%`→`%%` 转义（单位/前后缀）、`InvariantCulture` 浮点、屏幕名/动作目标/报警名/脚本名/注释统一转义。
6. **Golden 夹具补 canvas 覆盖**：新增 SVG/Tank/Pipe/Valve/Ellipse 与 `%` 单位绑定，使端到端 C 编译真正覆盖 canvas 与格式串路径（此前完全无覆盖，故上述缺陷长期隐藏）。

## 阶段 1（软件可验证部分）完成情况 — 2026-09-13

原阶段 1 的第 6/7 项（触摸量程、BoardProfile）需 T113 交叉链与实机校准，另行推进；本轮完成可验证的三项：

1. **BindingSemantics 三端统一**（审查 P1-1，`src/HmiEditor.Core/Design/BindingSemantics.cs`）：
   设计器预览、PC 模拟器、固件 `.so` 生成代码共用同一套「缩放 → 小数位 → 前后缀 → 判真」规则。
   生成代码不再把 `ValueDisplayType/ValueScaleFactor/ValueDecimalPlaces/ValuePrefix/ValueSuffix`
   只写成注释，而是真正输出（含缩放乘法与格式串）；变量单位作为后缀兜底也进入设计器预览。
   **行为变更**：默认小数位由生成代码硬编码的 1 位改为控件配置的 `ValueDecimalPlaces`（默认 2），
   与设计器一致；布尔判定由 `> 0.5f` 统一为"非零即 ON"（与 `ValueDisplayType.Bool` 文档语义一致）。
2. **生成物追溯戳**：生成代码头部新增 `// Generator: HmiEditor.CodeGen <version> (git <commit>)`；
   commit 在构建期由 MSBuild 目标注入程序集元数据（`HmiEditor.CodeGen.csproj`，无 git 时退化为 `unknown`）。
3. **编辑器稳健性**：新增最小日志抽象 `HmiEditor.Core.Diagnostics.AppLog` + `FileLogger`
   （`%APPDATA%/zhishu-hmi/logs/app-yyyyMMdd.log`）；`Program` 安装三层全局异常处理
   （Dispatcher UI 线程 / AppDomain / TaskScheduler），UI 线程异常记录后 `Handled=true` 不再直接崩溃；
   `MainWindow` 的 8 个关键操作（新建/打开/保存工程、导出代码、编译、下载、导出模拟器、Dock 诊断）
   改为 `async void` 壳 + `async Task` 内核，失败时落盘日志并在状态栏提示。

## 阶段 2（生成器结构治理，进行中）— 2026-09-13

1. **两条绑定刷新路径已合并**：`GenerateSimRefreshBindings`（PC 模拟器，正弦波）与
   `GenerateDeviceRefreshBindings`（实机 `.so`，读 `get_tag_value`）原本各自维护一份控件分支，
   已实际造成行为漂移。现统一为 `GenerateRefreshBindings(project, tags, deviceMode)`，
   只有三处差异参数化：值来源、空指针守卫、变量过滤与 timer 外壳。
   行数 350 → 284，控件分支从两份变一份；**生成输出保持逐字节不变**（合并后 1625 项测试零改动通过）。
   新增 `RefreshBindingsParityTests`（4 项）对拍锁定：两路径的控件处理器集合一致、
   值源/守卫/外壳差异符合预期、Label 与布尔语义共用。
2. **`GenerateControlParameters` 按控件族拆分：本轮未完成，已回退。**
   该 459 行 switch 的 case 体里含大量生成 C 用的字符串字面量（`{{`/`}}`、`{o}`），
   基于正则/括号计数的机械提取无法区分字符串内外的花括号，两次尝试均破坏语法；
   已回退到可编译状态。正确做法是改用 Roslyn 语法树（`Microsoft.CodeAnalysis.CSharp`）
   做按 case 分段的提取，属于独立的、纯重构型改动，建议单开一轮完成。

3. **通信层并发/超时/生命周期整改（审查 P1-5）已完成**：
   - `TcpDeviceChannel` / `SerialDeviceChannel`：新增 `SemaphoreSlim` **整帧串行化**
     （"清空缓冲 → 写入 → 等待静默"是不可分割的临界区，旧实现并发命令会在同一流上交错收发）；
     命令超时统一抛 `DeviceChannelException`，**调用方取消保持 `OperationCanceledException`**
     （便于上层区分"用户取消"与"设备无响应"）；TCP 读循环加 `CancellationToken` 并在
     `CloseAsync`/`Dispose` 取消+释放（旧实现半开连接会让 `ReadAsync` 永久阻塞）；
     文件传输独占闸门（内部走无闸门的 core 实现，避免自死锁）。
   - `ModbusTcpRuntimeDriver`：用 `SemaphoreSlim` 把"分配事务号 → 写请求 → 读完整响应"
     **整段串行化**（旧实现锁只保护事务号，I/O 在锁外的 `Task.Run` 中执行，并发请求互相串响应）；
     `ContinueWith(t => t.Result)`（会把异常包成 `AggregateException`）改为 `async/await`；
     `connectTask.Wait(ms)` 改为可取消的 `ConnectAsync(..., cts.Token)`（不再泄漏未观察任务）；
     异步读补显式超时（`ReadAsync` 不再受 `Socket.ReadTimeout` 约束）；
     寄存器/线圈**短帧校验**（旧实现越界抛 `IndexOutOfRangeException`，现为协议错误）。
   - 回归测试：`ModbusTcpRuntimeDriverTests` +5（短帧×2/超时/并发不串响应/并发必串行），
     `DeviceChannelTests` +4（并发不串、超时异常类型、调用方取消语义、关闭不挂死）。
     全部用 loopback 假从站/假服务器，可在 CI 运行，不依赖真实设备。

4. **`MainWindow` 结构治理（第一步）**：把 239 行 Dock 事件路由诊断代码
   （`RunDockEventRoutingDiagnosticCore` + 反射式 `IsEventSubscribed`）迁出主窗口，
   成为独立服务 `src/HmiEditor.App/Services/DockEventRoutingDiagnostics.cs`
   （宿主依赖——状态栏写入、Dock 重建、对话框所有者——显式参数化，行为不变）。
   `MainWindow.axaml.cs` 5023 → 4771 行。
   后续可继续摘出 Git 面板逻辑与编译/下载流水线。

5. **`GenerateControlParameters` 拆分完成**（上次回退项）：459 → **94 行**，按控件族拆出 15 个
   `EmitXxxParameters` 方法。上次失败原因是「正则 + 朴素括号计数」无法区分字符串字面量内外的花括号；
   本次改为**词法感知**扫描（跳过普通/逐字/插值/原始字符串、字符字面量、行/块注释），
   并修正了「方法在 switch 之后仍有代码」与「方法收尾 `}` 位置」两个拼接错误。
   全量测试零改动通过 → 生成输出逐字节不变。
6. **测试策略升级**：
   - 抽 `HmiEditor.Core.Design.ControlTextStyleResolver`（文本色/对齐的「状态样式优先」规则）为纯函数，
     用 `ControlTextStyleResolverTests`（6 项行为测试）替代原先读 `DesignCanvas.cs` 源码 grep 的 3 个测试
     —— 源码 grep 断言重构即碎、行为不变也会碎，且验证不了真实行为；
   - 新增 `GeneratedCodeSemanticTests`：对「全控件族工程」断言生成代码**零未知 LVGL API、零 Error 级问题**，
     替代字符串包含式断言。该测试立刻暴露出 `GeneratedCodeValidator` 白名单**漏了约 40 个合法 API**
     （长期是噪音、会被用户忽略），已逐条对照 LVGL 9.5 头文件补齐。
7. **`MainWindow` 结构治理（第二步）**：Git 提交/历史两个对话框（约 160 行 UI 构造）迁出为
   `Services/GitDialogService.cs`（owner/工程目录/状态栏/i18n 参数化，守卫判断留在主窗口）。
   `MainWindow.axaml.cs` 累计 5023 → **4636 行**。
   编译/下载流水线（约 350 行）仍在主窗口内，见待办。

7. **`MainWindow` 结构治理（第二步完成）**：
   - Git 提交/历史两个对话框（约 160 行 UI 构造）迁出为 `Services/GitDialogService.cs`；
   - **编译/导出流水线**（`ExportCodeCore` / `CompileProjectCore` / `DownloadToHmiCore` 前半段 +
     `BuildDeviceSharedLibraryAsync` + 三个产物状态字段，约 280 行）迁出为
     `Services/BuildPipelineService.cs`：宿主以回调注入 UI/i18n/工具链依赖，
     **产物状态由服务独占**（原先散落在窗口字段）；
   - 新增 4 项行为测试 `BuildPipelineServiceTests`：用临时目录 + 宿主回调桩驱动，
     断言生成文件落位、产物清单存在、进度 0→100、置忙/解锁下载、审计回调，以及
     「已有 .so 产物时复用」与「SharedLibrary 模式无需重建」。
   - `MainWindow.axaml.cs` 累计 **5023 → 4434 行**（-12%）。

8. **触摸量程（审查 P0-7）已修复**：`touch_evdev.c` 原先就地兜底硬编码
   `HMI_TOUCH_MAX_X/Y = 1024×600`，而板级 DTS 声明 GT911 上报 **800×480**，
   二者混淆导致 1024×600 屏右侧 224px、底部 120px 触摸不可达。修复：
   - 量程移入 `app/runtime/runtime_config.h` 按 `HMI_BOARD_WAVEPI` 给出
     （WavePI = 800×480，与板级 DTS `ctp_screen_max_x/y` 一致；mq_r 保守保留 1024×600）；
     保留 `-DHMI_TOUCH_MAX_X/Y=...` 编译期覆盖；`touch_evdev.c` 缺失时 `#error` 硬失败。
   - 坐标归一化抽为纯函数 `app/drivers/touch_scale.h`（int64 中间量 + 边界夹取），
     新增主机侧测试 `tests/touch_scale_test.c`（14 项），并把
     「旧错误量程下右/下缘不可达」固化为回归护栏。
   - `run_host_tests.sh` 分别以默认板级 / `HMI_BOARD_WAVEPI=0` / `-D` 覆盖三种配置各跑一遍，
     实测板级取值与覆盖优先级。
   - 消除文档矛盾：`tina/dts/README.md`、`LCD_RGB24_1024x600.md`、`version-info.md`、
     `wavepi-s6-migration-plan.md` 四处的触摸量程说明统一到「以 dts 为准、改 dts 必须同步改宏」。
   - **未做**：实机四角校准（需硬件）。落地手段为 `/etc/hmi/touch_cal.conf` 的 `sx/sy`
     线性补偿（无需重编固件），已在文档中说明。

9. **板级配置单一来源（阶段 1 第 7 项）已完成**：
   - 新增 `app/runtime/board_profile.h` 作为板级差异唯一事实源（触摸设备与量程、串口分配、
     RS485 模式与方向 GPIO、G2D 能力、串口设置页列表、板名）；`runtime_config.h` 改为包含它，
     只保留与板无关的运行参数（宏名不变，既有 include 者无需改动）。
   - `set_serial.c` 的两处 `#if HMI_BOARD_WAVEPI` 分支改为使用板级宏，
     并新增「stty 列表条数 == 菜单条数 / 默认项在范围内」等跨字段不变量测试。
   - **消除构建副作用**：`tina/build.sh` 不再用 `sed -i` 改写被 git 跟踪的
     `runtime_config.h`（此前每次构建都污染工作区、切板后源码与产物不一致）；
     改为导出 `HMI_BOARD_WAVEPI`，由 `hmi-runtime/Makefile` 以
     `-DHMI_BOARD_WAVEPI=$(HMI_BOARD_WAVEPI)` 编译期注入，并在构建时提示默认值漂移。
   - **门禁升级**：`check_sdk_config` 对 `CONFIG_PACKAGE_hmi-runtime=y` 从"警告"改为
     **硬失败**（缺它打出的镜像不含 HMI 运行时 = 交付不可用）；`ci-gate.sh` 新增静态检查
     「构建脚本不得 `sed -i` 改写应用源码」，防止该副作用回归。
   - 验证：`touch_evdev.c` / `set_serial.c` 在 WavePI 与 mq_r 两种配置下
     `gcc -fsyntax-only -Wall -Wextra` 均通过；新增 `board_profile_test.c`
     （45 项断言，三配置各跑一遍）。

10. **校验体系自洽性 + 虚假依赖清理（纯软件侧）**：
    - 移除 `CommunityToolkit.Mvvm` 依赖 —— 全仓 0 处使用，仅声明会误导"本项目用 MVVM"；
      `Dock.Model.Mvvm` 保留（Dock 面板基类必需）。决策与"为何不做全面 MVVM 化"
      （含反转触发条件）记入 `docs/adr/ADR-2026-09-13-ui-mvvm-decision.md`。
    - 新增 LVGL API 对照表 `src/HmiEditor.Core/Resources/lvgl-api-names.txt`
      （3611 行 / 3607 个名字，由 `scripts/gen-lvgl-api-list.sh` 从 LVGL 头文件派生，
      含 `lv_api_map_*` 里的**对象式兼容别名**）与 `ValidatorSelfConsistencyTests`（3 项）：
      ① 白名单每个名字必须真实存在；② 白名单不得与 `LvglApiCompatChecker.RemovedApis` 重叠；
      ③ 对照表规模下限，防止被截断后让断言失效。已用「注入假条目 → 测试失败 → 还原」
      自测过守卫有效性。
    - 核实结论：`lv_chart_set_range` / `lv_scr_load` 并非陈旧条目，而是 LVGL 无条件提供的
      兼容别名（`lvgl.h` 直接包含 `lv_api_map_v8.h` / `lv_api_map_v9_1.h`），因此保留。

11. **`MainWindow` 结构治理第三/四步（纯软件侧）**：
    - 第三步：`WireEvents`（992 行）按域拆到 5 个 partial 文件——
      `Wiring.Canvas.cs`(309) / `Wiring.Commands.cs`(161) / `Wiring.Panels.cs`(301) /
      `Wiring.Faceplate.cs`(127) / `Wiring.DockAggregate.cs`(280)；`MainWindow.axaml.cs`
      中只留一个 5 行的 `WireEvents()` 骨架，事件订阅的域边界一目了然。
    - 第四步：Modbus 预览运行时（7 个方法 + 6 个状态字段，246 行）整体平移到
      `MainWindow.Preview.cs`。**这是纯搬家，不改行为**，测试零改动通过（1651/1651）。
      未做「抽成独立服务」的原因写在类注释里：需要约 10 个回调、`_previewValues` 被
      脚本调试路径直接读取、`_modbusDriver` 在关停时释放，而当前**零 UI 测试**——
      在没有 UI 测试能力前做接口化只会把回归风险从编译期挪到运行期。
    - `MainWindow` 累计 5023 → 3207 行（-36%）。

12. **视图层 `async void` 处理器用户可见化（纯软件侧）**：
    - 问题：UI 事件处理器签名必须是 `void`，所以异步处理器只能写成 `async void`。
      这类方法的异常无法被调用方捕获，只会上抛给同步上下文 —— 全局 Dispatcher 处理器
      能保证进程不崩，但用户看到的是**「点了没反应」**。治理前全仓 51 处 `async void`
      中 16 处没有任何 try/catch。
    - 方案：新增 `src/HmiEditor.App/Services/UiOperationGuard.cs`，把「事件回调 → 异步操作」
      统一收口，保证四条性质（均有行为测试覆盖）：
      ① 异常**绝不外泄**给调用方；② 失败**必落盘日志**（可运维）；
      ③ 失败**必回调界面**（用户可见，不再"点了没反应"）；④ **用户取消不算失败**（不误报）。
    - 推荐写法：处理器保持同步 `void`，异步体交给守卫，顺带把 `async void` 一起消灭：
      ```csharp
      private void OnBrowseSoClicked(object? sender, RoutedEventArgs e) =>
          UiOperationGuard.Run("选择 HMI 程序", async () => { ...await 选择器... }, Log);
      ```
    - 界面出口分两级：视图有自己的状态行（日志面板 / `ShowStatus`）就传 `onError`；
      属性面板、图元库等**没有状态行的 UserControl** 传 `null`，由 `UiOperationGuard.UiSink`
      回落到主窗口状态栏（`MainWindow.ShowGlobalStatus`）。
    - 结果：`async void` **51 → 35 处**，其中**未受保护 16 → 0**（35 处全部自带 try/catch
      或走守卫）；新增 10 项行为测试（含「本地状态行优先于全局出口」「提示自身抛错被吞掉」
      「取消静默」）。全局 Dispatcher 处理器明确降级为**最后一道防线**。
    - 未做：把 35 处已有的 try/catch 也统一迁到守卫（无用户可见收益，纯风格改动，
      风险大于收益）。

13. **生成代码断言从「字符串包含」升级到「结构匹配」（纯软件侧）**：
    - 问题：`Assert.Contains("lv_obj_set_pos(obj_c1, 10, 20)", code)` 把断言绑死在生成器的
      **排版细节**上 —— 改缩进/换行/空格会让正确的代码测试变红（假失败）；反过来，
      同样的文本出现在**注释或字符串字面量**里又会让断言变绿（假通过）。全仓共 211 处。
    - 方案：新增 `tests/HmiEditor.Core.Tests/Support/CSource.cs` —— 词法感知地解析 C 代码里
      的**调用点**（函数名 + 实参序列），跳过注释与字符串/字符字面量，展开嵌套调用
      （`lv_color_hex(...)` 之类），并排除 `if`/`while`/`sizeof` 等关键字。
      三个断言入口：
      ① `AssertCall/AssertNoCall` —— 期望文本本身是一次调用，按「函数名 + 实参」匹配；
      ② `AssertText/AssertNoText` —— 非调用（声明、片段）退化为**排版无关**的包含匹配；
      ③ `AssertCode/AssertNoCode` —— 智能分派，迁移时逐条替换不必分类。
    - 迁移：**281 处**精确调用文本断言改为 `CSource.Assert*`（覆盖 20 个测试文件）。
      全仓该类断言 **211 → 3 处**，剩余 3 处分别是工具自身的文档示例/自测，
      另 1 处是 `PluginLoadingTests` 中对手写插件样例的断言（非生成器输出）。
    - 防复发：`TestAssertionStyleGuardTests` 扫描 `tests/**`，出现「字面量含 `(` 与 `,`
      的 `Assert.Contains/DoesNotContain`」即失败（带明确例外清单）。
      已用「注入 1 处违规 → 守卫失败 → 删除」自测过守卫有效。
    - 自测：`CSourceHelperTests`（26 项）覆盖解析与匹配边界 —— 排版无关必须通过、
      实参/对象/计数/字符串差异必须失败、注释与字符串里的同名文本不算调用、
      括号不配平不抛异常、关键字不误判为调用、`Normalize` 保留转义序列。
    - 说明：这是**唯一**一处用源码扫描做断言的守卫 —— 它守的是「测试怎么写」，
      产品行为一律用行为测试。

14. **补齐 headless UI 测试能力（第 4 项，纯软件侧）**：
    - 问题：此前全部 UI 逻辑只能靠"把状态抽成纯类再测纯类"来覆盖
      （`CanvasDragPreviewStateTests` 等就是为此而抽的），**真实可视树、XAML 资源解析、
      事件订阅、布局、用户输入零覆盖** —— `{StaticResource}` 名字写错、控件名对不上、
      `ValueChanged` 忘订阅，只有在人工点到那个面板时才会暴露。这也是 MVVM ADR 里
      写明的反转前置条件（"先补 UI 测试能力"）。
    - 方案：新增 `tests/HmiEditor.App.UiTests`，用 `Avalonia.Headless` +
      `HeadlessUnitTestSession`（**不依赖 xunit v3**，与其余测试项目同用 xunit 2.9.3），
      以真实 `App` 为宿主（App.axaml 的主题/图标资源、本地化、ViewLocator 全部走生产路径），
      并提供 `UiTestHost.Run/RunAsync/Pump` 三个入口把 UI 线程与渲染定时器封装起来。
    - 覆盖（30 项）：
      ① **视图装载冒烟**：自动枚举 `HmiEditor.App.Views*` 下 15 个 UserControl 视图，
        逐个构造 → 挂窗口 → Show → Measure/Arrange，断言可视树非空且存在具名控件
        （XAML 没加载/资源缺失会直接失败）；另有清单规模守卫防止枚举失效后空跑。
      ② **真实输入模拟**：鼠标点击命中按钮触发 `Click`、点击空白不触发、键盘输入进入
        获焦 TextBox —— 这是后续所有交互式 UI 测试的地基。
      ③ **`AppDialogs` 确认对话框**：真正弹窗 + 模拟点击"确定/取消"，断言返回值与提示文案
        （删除画面/模板/用户等不可逆操作唯一拦截点，此前完全无测试）。
      ④ **`PropertyPanelView` 属性写回**：绑定控件后编辑器被正确填充；**真实键盘输入**
        改名 → 写回模型并发 `PropertyChanged`；数字框改值 → 写回；重复绑定不误写上一个控件；
        校验器拒绝后保留原值。
    - 顺带发现的两处**行为差异**（已用测试钉住现状，未改行为，见"当前阻断项"第 8 条）：
      Avalonia 12 中程序化赋值 `TextBox.Text` 不触发 `TextChanged`；
      `X/Y/Width/Height` 数值写回不经过可插拔 `IPropertyValidator`。
    - 效果：MVVM ADR 的反转触发条件 ① 达成 —— 现在做 UI 层重构已有回归网可兜底。

15. **预览运行时服务化（第 1 项的收尾）**：
    - 背景：上一步只把 Modbus 预览运行时**平移到** `MainWindow.Preview.cs`（305 行），
      当时明确写了「服务化的前置条件是先有 UI 测试」。第 14 条补齐 UI 测试能力后补做本步。
    - 问题：5 个状态字段（`_simulator`/`_bindingEngine`/`_previewValues`/`_modbusDriver`/
      `_actionExecutor`）被四处直接读写 —— 脚本调试（读快照）、标签监视器（借绑定引擎）、
      画布事件（启停预览 + 执行动作）、关停清理（释放 Timer 与设备连接），
      「谁负责释放」没有单一答案，连接泄漏只能靠人肉审查。
    - 方案：新增 `src/HmiEditor.App/Services/PreviewRuntimeService.cs`（406 行）。全部宿主依赖
      走 11 个回调（与 `BuildPipelineService` 同一套路）；**所有权边界就是 `Dispose()`**：
      仿真器 Timer 与真实设备连接都由它释放，`MainWindow.CleanupOnShutdown` 只需
      `_previewRuntime.Dispose()`。`DriverFactory` 是留给测试的注入点（默认仍创建真实驱动）。
    - 行为保持：方法体逐行照搬，包含 `Stop()` 只摘值提供器、`Start()` 内的注册顺序、
      Int 走正弦分支等既有细节；`MainWindow.Preview.cs` 删除。
      两处**有意的轻微硬化**（均不改变可观测行为）：
      ① `ValuesUpdated` 订阅改为 `-=` 后 `+=`（重复 Start 不再叠加回调）；
      ② `Stop()` 同时摘掉 type/unit 提供器（原来只摘 value 提供器，会在预览结束后
         残留指向 MainWindow 的闭包）。
    - 新增 **22 项行为测试**：按 DataType 注册哪些信号、只写绑定不参与仿真、表达式绑定展开、
      值源选择（真实设备 / 虚拟从站）、连接失败与驱动工厂抛异常时的安全回退、
      切换设备时旧连接被释放、提供器注入与摘除、**Stop/Dispose 的释放语义**（含重复 Dispose）、
      动作执行、绑定引擎复用。
    - 顺带修正一处**文档与实现不符**：旧注释写「Int → 方波 0-100」，实现中整数走的是正弦分支；
      已更正注释并以测试钉住实际行为（不改行为，作为独立发现记录）。
    - 代价说明：`MainWindow.axaml.cs` 本次**没有变小**（3220 → 3242 行，多出的是服务装配的
      回调块）。这一步买到的是**所有权的可验证性**与 22 项回归，而不是行数下降；
      行数下降要靠后续把回调收窄（例如画布提供器整体下沉为一个适配器）。

16. **修正 UI 测试暴露的两处行为差异（第 1 项"继续修正"）**：
    - ① **不再依赖 `TextChanged` 的事件时机**：Avalonia 12 中程序化赋值 `TextBox.Text`
      不触发 `TextChanged`（只有用户输入触发）。`FaceplateTemplateEditorView` 原先把
      "空名字 → 新模板默认名、空分类 → 通用"的归一化**只**写在处理器里，于是"装载模板后
      是否已归一化"取决于用户有没有碰过输入框 —— 语义不确定。现在抽
      `ApplyMetadataToTemplate()`，**装载路径显式调用**，并加 `_isLoadingMetadata` 守卫，
      使行为与框架事件时机无关（顺带把名字/分类统一 trim）。
    - ② **属性写回统一经过可插拔校验器**：`PropertyPanelView.SaveProperties` 原先
      只有 8 处走 `ValidateAndWrite`，`X/Y/Width/Height` 等 **51 处直写**只过静态
      `ClampPosition/ClampSize`，导致 `IPropertyValidator` 变成"部分属性有效"，插件作者
      无法预测规则何时被调用。现已全部改为 `ValidateAndWrite<T>`（泛型化，站点更短）。
      **有意保留的例外**：程序化命令（重置旋转/镜像、选择图片/SVG 资源、切换绑定模式、
      清空动作）仍直写模型 —— 用户点了按钮就是明确指令，让插件规则静默吞掉命令会变成
      "点了没反应"；该区分已写入方法注释。
    - 验证：`App.UiTests` 从 30 → **38** 项，其中 4 项针对②（拒绝数值改动仍保留原值 /
      默认校验器下仍钳制越界值 / "拒绝一切"的校验器能挡住所有编辑器改动 /
      程序化命令不被校验器否决），5 项针对①（装载即归一化 / 有效值不被装载改动 /
      真实键盘输入 trim 后写回 / 清空名字回落默认 / 两个实例互不串状态）。

17. **快捷键/右键菜单命令层去重 + `MainWindow` 继续变小（第 2 项）**：
    - 问题：`OnWindowKeyDown`（163 行 `switch`）与 `HandleCanvasContextAction`（78 行 `switch`）
      覆盖的操作**完全相同**（复制/粘贴/复制一份/删除/组合/拆分/6 向对齐/等距/统一尺寸
      共 15 个），是同一份业务逻辑的两份实现 —— 并且已经漂移。
    - 方案：新增 `src/HmiEditor.App/Services/EditorCommandCatalog.cs`（170 行）：
      命令枚举 + 快捷键绑定表 + 解析器 + 右键菜单映射 + 提示文案。`MainWindow` 只保留
      **一个** `ExecuteEditorCommand`（两条输入路径共用）和 5 行的 `HandleCanvasContextAction`。
      绑定表让三件原先做不到的事成为可断言的不变量：
      ① 每个绑定必须解析到预期命令；② **同一按键组合不得绑定到两个命令**（原先写重了
         只会静默命中第一个分支）；③ 右键菜单动作必须全部有对应命令，且与快捷键操作集一致。
    - **修饰键改为严格相等匹配**，这是有意的收紧：原 `when` 条件 `!ctrl` 会放行 `Alt+Delete`，
      无条件分支 `case Key.Escape:` 会放行 `Ctrl+Escape`；现在只认表里列出的组合
      （`Ctrl+Escape` 在 Windows 上还有系统含义，不该被吞）。
    - **顺带修掉两个既有缺陷**（都是"把两份重复实现摊平成数据后"才暴露出来的）：
      ① **Ctrl+Shift+V 实际执行的是粘贴**：`case Key.V when ctrl && !alt`（粘贴）排在
         `case Key.V when ctrl && shift`（垂直等距排列）之前，前者条件更宽，于是后者永远不可达
         —— 文档承诺的快捷键静默失效。现在两个组合各自解析到 `Paste` / `DistributeV`。
      ② **系统变量永远被解析成 `Float`**：`ResolveVariableDataType` 用
         `bindingId.Contains('.')` 判定 UDT 字段并直接返回默认值，而系统变量 Id 形如
         `sys.time.cur` **自带点号**，于是"系统变量"分支是死代码；绑定系统变量的控件在设计器里
         按数值格式显示。现按真实类型解析（`VariableBindingInfo`，Core）。
    - 同时把"绑定 Id → 类型/单位"的解析从 `MainWindow` 私有方法移到 Core
      （`src/HmiEditor.Core/Services/VariableBindingInfo.cs`，65 行），只依赖 `VariableManager`：
      预览运行时不再需要两个宿主回调（构造块相应变短），且该规则可在 Core 直接测试。
    - 结果：`MainWindow.axaml.cs` 3242 → **3086 行**（本步 -156）；新增
      `EditorCommandCatalogTests` 16 项 + `VariableBindingInfoTests` 11 项。
    - 未做：把 `ExecuteEditorCommand` 的 130 行也搬出 `MainWindow` —— 需要约 10 个宿主回调，
      构造期装配的开销会吃掉大部分行数收益（`PreviewRuntimeService` 已验证过这个临界点），
      优先级低于其它候选。

18. **绑定解析的"点号"语义修正 + 画布工具栏按域拆出（本轮优化）**：
    - **功能缺陷（已修）**：`VariableManager.ResolveBinding` 把"含点号"等同于"UDT 字段"，
      点号分支内三处 `return null` 使字面 Id/名匹配永远不可达。而 PLC/CSV 导入的变量名
      本身就常带点号（`Motor.Speed`、`DB1.DBD0`、`Line1.Temp`），这类绑定会**静默解析不到
      变量**：不进运行时标签表、预览恒空、监视器无行。现改为 UDT 字段解析失败后继续回退到
      字面 Id/名匹配（UDT 命中时仍优先）。回归测试直接断言"修复前标签表为 0、修复后为 1"。
    - **结构（已做）**：`SetAlignmentGroupVisible` / `WireAlignmentButtons` / `WireDrawToolbar`
      （145 行工具栏装配）移入 `Wiring.CanvasTools.cs` partial。**说明清楚**：partial 拆分
      减少的是 `MainWindow.axaml.cs` 这个文件（3086 → 2928 行），并不减少 `MainWindow` 类本身；
      真正的类级缩减仍需服务化（代价见第 17 条"未做"）。
    - 累计：`MainWindow.axaml.cs` 5023 → **2928 行**。

## 阶段 3（仓库卫生与工程化基线）— 2026-09-14

依据 `docs/2026-09-14-optimization-directions.md` 的 P0 清单执行，**全部为零行为风险改动**。

1. **根目录产物归档**：根目录原先躺着 16 个被 git 跟踪的一次性产物（5 个诊断 md、
   9 张验证截图、2 个 RS485 调试脚本），另有 2 个被 `.gitignore` 忽略但同样堆在根目录的文件
   （`debug-dock-blank-after-drag.md`、`inspect_dock.csx`）。全部 `git mv` 到
   `docs/archive/2026-08/` 并补 `README.md` 说明来历与当前状态，git 识别为 **rename**（历史保留）。
   - `.gitignore` 增加 `!docs/archive/**`：归档区内即使命中既有的 `*.csx` / `debug-*.md`
     本地调试规则也保留追踪，否则会出现「看起来归档了、实际没入库」。
   - 项目内唯一受影响的引用（`docs/consistency-review-2026-08-19.md` 两处）已更新为新路径；
     归档目录内的 md 与截图同目录，相对引用不变。
   - 根目录现只剩 `.gitattributes`、`.gitignore`、`HmiEditor.slnx`、`README.md`。

2. **删除死代码 `HmiEditor.Renderer`**：13 行空实现（`DesignSurfaceRenderer.Render()` 仅有注释，
   原文标注「Phase 0 占位」），全仓除 `HmiEditor.App.csproj` 的 `ProjectReference` 与
   `HmiEditor.slnx` 条目外**无任何实例化**、零测试。已删除项目并移除上述两处引用。
   **src 项目数 14 → 13**。

3. **补齐可复现性配置**：
   - `global.json` 固定 SDK **10.0.112**（`rollForward: latestFeature`，不允许预览版）；
   - `.editorconfig`：全仓风格统一，**所有规则均为 silent/suggestion**——任何 warning 级规则
     都可能经已启用的 .NET 分析器变成构建警告，破坏「0 警告」基线；
   - **中央包版本管理（CPM）**：新增根 `Directory.Packages.props`，**16 个包**的版本在此单点声明；
     **36 处** `PackageReference Version=` 从 13 个 csproj 中移除；`src/Directory.Build.props`
     不再声明 `ManagePackageVersionsCentrally`（避免与根配置冲突）。
     实测 `dotnet msbuild -getProperty:ManagePackageVersionsCentrally` 返回 `true`，构建 0 错 0 警。
     此前 `Microsoft.NET.Test.Sdk 17.12.0` 重复声明 **7** 次，现在只有 1 处。

4. **测试临时目录健壮性（本机 1785/1788 → 1788/1788）**：
   - **问题**：3 个导出类测试（`SimulatorLoopTests` / `MockFontLoadingTests` /
     `MockProjectEndToEndTests`）直接用 `Path.GetTempPath()`。当 `TMPDIR` 指向小容量 tmpfs
     （本机 `/tmp` 仅 **10 MB**）时，写出模拟器工程会以 `IOException: disk was full` 失败，
     堆栈看起来像产品缺陷，实际是环境问题。
   - **方案**：新增 `tests/HmiEditor.Core.Tests/Support/TestTempDir.cs`，按
     `HMI_TEST_TMPDIR` → `TMPDIR` → `Path.GetTempPath()` → 仓库内 `.hmi-test-tmp/`（已 gitignore）
     的顺序**逐个真实探测**（写入 16 MB 后删除），取第一个真正写得进去的目录；
     显式指定 `HMI_TEST_TMPDIR` 时**不静默降级**，失败即报错。
   - **顺带修掉一个会掩盖失败的缺陷**：3 个文件中共 **44 处**
     `finally { Directory.Delete(dir, recursive: true); }` 是裸调用 —— .NET 中 `finally` 抛出的异常会
     **替换掉**原始异常，磁盘紧张时正是最易触发路径。统一改为 `TestTempDir.Delete(dir)`（清理失败不影响结论）。

5. **`scripts/ci-gate.sh` 去掉 `/dev/fd` 依赖**：第 1 步原用
   `mapfile -t SCRIPTS < <(git ls-files '*.sh')`，进程替换在受限沙箱 / 精简容器中不可用，
   会**静默拿到空清单**，使「脚本语法检查」变成永远通过的假门禁
   （实测报 `/dev/fd/63: 没有那个文件或目录`，且因 `set -e` 之外的执行路径继续往下走）。
   改为临时文件收集清单 + 计数器，并显式要求清单非空（`未找到任何 .sh 脚本` 即失败）。
   这与本文件「门禁必须能失败」的设计原则一致。

6. **补根 `README.md`**：项目定位、仓库结构、环境要求、构建/测试/门禁/固件入口、
   常见问题（TMPDIR 空间、`/dev/fd` 受限环境）、文档索引、工程约定（包版本单一来源、
   设计常量单一来源、生成代码断言用 `CSource`、测试临时目录用 `TestTempDir`）。

## 阶段 4（权限模型下沉到设备链路）— 2026-09-16

依据 `docs/2026-09-14-optimization-directions.md` 的 **P2-2 前半**。此前编辑器侧的权限模型
（`Permission` / `UserRole` / `SecurityConfig`）只活在编辑器进程内，而固件 `hmi_actions.c`
只有一个布尔 `s_logged_in` ⇒ **设备端没有任何动作受权限约束**（`hmi_login()` 的语义是"点亮一个布尔量"）。

1. **三个单一来源**（新增）：`Core/CodeGen/ActionPermissionMap.cs`（动作 → 所需权限）、
   `PermissionMacros.cs`（枚举 → C 宏名/字面量）、`SecurityConfigCodeGen.cs`（生成头整体内容）。
   三者都带"为什么必须收口"的类注释。
2. **生成物 `generated_security.h`**：运行模式 + `HMI_SEC_ENFORCE` + 权限位 + 角色表 +
   动作→权限注释表 —— **没有一个手写数值**，全由枚举/模型算出。由导出器与生成代码同批落盘（产物清单 +1）。
3. **设备端消费**：`hmi_actions.{h,c}` 用 `__has_include` 优先引用、缺省回退"设计模式：不门控"
   （与 `comm_core.c` 消费 `generated_comm.h` 同套路）；新增 `hmi_login_as_role` / `hmi_has_permission` /
   `hmi_require_permission` / `hmi_current_role` / `hmi_current_level`；未登录语义只在
   `hmi_has_permission` 折算一处。**设备端不得出现手写权限位定义**（静态守卫钉住，判据本身另有正反例）。
4. **动作回调权限门**：发射器在 `OperationMode != Designer` 时给受管动作发
   `if (!hmi_require_permission(HMI_PERM_…)) return;`。**默认（设计模式）不发射** ⇒
   历史导出产物逐字节不变，既有对拍/回归测试零改动。Login/Logout 恒不门控（否则"权限不够就登不出去"）。
5. **验收**：C# **37 条** + 固件 host **34 条断言**（接进 `run_host_tests.sh` 第 4 步，`ci-gate.sh` 覆盖）；
   负向验证 6 组。专题见 `docs/2026-09-16-permission-model-sinking.md`。

遗留：真实凭据校验（P2-2 后半）；~~模拟器侧 4 个动作 API + `hmi_require_permission` 只有 `extern` 无定义~~
—— **已修（阶段 5，2026-09-16），且实测缺口比登记的大**（4 → 7 个符号，另含 `var_raw_read` / `var_raw_write`）。

## 阶段 5（导出模拟器工程链接自洽 + 判定策略下沉）— 2026-09-16

承接阶段 4 遗留 2。实测缺口比登记的大：**7 个宿主符号**只有 `extern` 没有定义 ⇒ 导出工程
`cmake --build` 能编译、到**链接期**才炸（`undefined reference`），而所有单元测试全绿。
顺带扯出两个同族缺陷。

1. **链接契约穷举守卫**（`SimulatorLinkContractTests`）：从生成代码**反推**"它要求宿主提供什么"
   （扫全部 `extern` 函数声明），再要求导出工程里每个都有**定义** —— 新增宿主 API 时天然被纳入，
   不靠维护清单。带扫描面自检 + 判据正反例。
2. **`main.c` 补「PC 模拟器宿主运行时」段**：`var_raw_read` / `var_raw_write`（一块 RAM 表顶上
   `variable_table.c`）+ `hmi_show_alarms` / `hmi_show_trends` / `hmi_login` / `hmi_logout` /
   `hmi_require_permission`；`#define SIM_TAG_COUNT` 与 `generated_hmi.c` 的 `TAG_COUNT`
   **同源且必须相等**（偏小 ⇒ 越界读被静默吞成 `0.0f`），无标签时兜底 `1`（长度 0 的数组不合法）。
3. **判定策略下沉**：`hmi_perm_allows(logged_in, perm_mask, bits)` 由生成头以 `static inline`
   提供（**唯一实现**，含"未登录折算查看画面"这条语义决定）。固件 `hmi_has_permission` 与模拟器
   `hmi_require_permission` 都只剩转发 ⇒ 不可能分叉（分叉表现是"预览里点得动、设备上点不动"）。
   `static inline` 不产生外部符号、不引入链接依赖；包在 `#if HMI_SEC_ENFORCE` 里，
   于是"用了权限位的代码必须被编译掉"这条**对调用侧同样成立**。新增
   `HMI_SEC_DEFAULT_ROLE_MASK`（模拟器没有角色表，掩码由生成器算好）。
4. **导航候选屏名单一来源** `Core/CodeGen/ScreenNavTargets.cs`：固件那三份 `candidates[]`
   是它的孪生副本（逐项 + **顺序**守卫）；模拟器在**生成期**解析出屏名直呼，不再运行时猜。
   解析顺序 = 候选顺序（与固件 `try_switch` 一致），精确匹配（与 `strcmp` 一致）。
5. **同族缺陷（阶段 4 遗留里没有的）**：`NormalizeActionValue` 用 `"0.########"` ⇒ 整数值产出
   `1` + `f` = **`1f`（非法 C 浮点常量）** ⇒ 任何带「设置变量」动作的工程**编译期**就失败
   （`error: 整数常量的 'f' 后缀无效`）。已改 `"0.0########"` + `double.IsFinite` 挡 `NaN`/`Infinity`
   （否则产出 `NaNf`）。**既有测试把 `100f` 写进了期望值**，等于把 bug 钉成了规格 —— 一并更正。
6. **验收**：全量 **2320/0**（7 程序集）；`ci-gate.sh` 5 步全过（固件 host 34/34）；
   符号级链接探针**正例**（宿主符号段为空）+ **负例**（剥掉 7 个定义 ⇒ 恰好点出那 7 个）；
   编译器级负例（`1f` → gcc 报错）；负向验证 **6 组全部命中**。
   专题见 `docs/2026-09-16-simulator-export-link-selfcontained.md`。

## 阶段 6（通道收帧判据：唯一实现 + 首字节宽限窗口）— 2026-09-16

承接阻断项 10（门禁首跑复发的 `DeviceChannelTests.ConcurrentCommands_…`）。根因在**产品侧**：
`TcpDeviceChannel` / `SerialDeviceChannel` 各自持有一份**逐字相同**的收帧代码，判据是
"从**写入时刻**起算静默 `SilentTimeoutMs`"——而"写入时刻"不等于"设备吐出首字节的时刻"。

1. **失效模式**：① **假空响应** —— 设备起步（shell 调度 / 进程抢占 / 主机满载）慢过 300 ms
   即被判"本命令无输出"返回空串；② **串帧** —— 那条迟到的回包落进下一条命令的窗口，
   表现为 `["", "ok:AAA\nok:BBB"]`（一条拿空串、另一条拿两行）。**两个症状同源**，故总是同时出现。
2. **溯源**：这是 `docs/code-error-audit-2026-08-20.md` §2 那次修复的**未完成部分** —— 当时为修
   "无输出空等满超时"，首版改动会引入"回显延迟竞态"，最终选了 `TickCount64` 从写入时刻起算。
   即：竞态从未解决，只是把误判窗口从 0 ms 挪到 300 ms；且当时**无自动化测试**（原文自述
   "人工核对逻辑"）。2026-09-13 审查曾以"完成判定只靠 300 ms 静默（慢输出会被截断）"记过一笔。
3. **修法**：收成唯一实现 `Channels/CommandFrameCollector.cs`。生产者（读循环）只 `Append` + 打时间戳，
   **不判定帧结束**（旧实现由它延迟翻转 `_dataIncoming`，起算点与消费侧不一致）；消费侧开帧 → 写 → 等。
   结束判据拆成两条**互斥**的：收到过数据 ⇒ 从**最后一段数据**到达时刻起算静默；一字节未到 ⇒
   首字节宽限窗口（`FirstByteTimeoutMs`，默认 1000 ms，且 `Math.Max(firstByte, silentMs)` 保证不小于
   静默期）到点返回空。`SawData`/`LastDataTicks` 挂在**帧实例**上（每帧独立），不再是跨帧共享、
   被生产者延迟翻转的 `volatile bool`。
4. **同族清理**：两个通道各持一份的 `SanitizeOutput` 收进 `CommandOutput.Sanitize`。
5. **测试侧**：`LineServer` 新增 `replyDelayMs` 档位 —— 把"设备起步慢"从"靠全量并行的负载去撞"
   改成**可构造输入**，这是把 flaky 转成确定性回归的关键。新增 `CommandFrameCollectorTests`(6)、
   `DeviceChannelTests` +2（首字节 600 ms 仍拿到回包 / 无回复时在首字节窗口后返回空而非空等满超时）、
   原并发用例改为 `replyDelayMs: 400` 并**显式断言不存在串帧形态**、`ChannelFramingContractTests`(4：
   守卫成对"转发 + 没有第二份"、目录级穷举禁止第二份实现、判据自检)。Device 程序集 64 → **76**。
6. **验收**：全量 **2332/0**（7 程序集）；`ci-gate.sh` 5 步全过（固件 host 34/34）；负向验证
   **4/4 组命中**（NV-2 首轮因"注入与缺陷不等价"未红，改用"多段数据累计时长超静默期"的构造才有效）。
   专题见 `docs/2026-09-16-channel-frame-completion-contract.md`。

## 阶段 6（运行时凭据校验）— 2026-09-16

承接阶段 4 遗留 1（P2-2 后半）。落地前的症状不是"功能没做"，而是**功能看起来做了**：
登录屏有用户名框 + 密码框、有登录按钮，按下去却**无论填什么都能进** ——
因为 `hmi_login()` 第一步就是切到登录屏（自己切自己），两个输入框从来没被读过一次。

按数据流拆成四环定位，发现**环环皆缺**（此前只被笼统记作"凭据校验未做"）：

| 环 | 缺口 |
|---|---|
| ① 凭据来源 | 运行时用户表不存在。编辑器自己的凭据在进程内字典里、不落盘，**且不该复用** —— 跨安全边界复用会让"拿到固件的人"能离线爆破编辑器口令 |
| ② 下发 | `SecurityConfigCodeGen` 只有角色表 |
| ③ 设备端校验 | `hmi_login_as_role(user, role_id)` **无密码参数**；固件全树 grep `sha256`/`mbedtls` 零命中 |
| ④ 登录屏取值 | `Login` 动作无参；生成代码里**从未出现过** `lv_textarea_get_text` |

1. **三刀切法**，顺序不可换：①运行时用户表进工程 + 下发 → ②登录屏取值 + `Login` 动作拆分 →
   ③设备端真校验。**第 2 刀不能单独合入** —— 生成代码开始提交凭据、而设备端还没有表可查时，
   唯一不退化的选择是"密码非空就放行"，那比现状更糟。
2. **跨语言算法契约**：`h0 = SHA256(salt ‖ utf8(pwd))` 迭代 `HMI_SEC_HASH_ITERATIONS` 次，
   存盘格式 `Base64Salt:Base64Hash`。两侧实现（`RuntimePasswordHasher.cs` / `hmi_crypto.c`）
   向**同一组外部真值**（Python `hashlib` 独立算）收敛 —— 否则"两侧一起错"不会红，
   而那是跨语言契约最危险的失效形态（所有账户都登不上，看不出哪边错）。
   迭代次数**刻意不存进格式**（只是常量），调强度不会出现"表里旧轮数、代码按新轮数算"。
3. **固件端四条判据**：① 口令长度不足**不计入**失败计数（它没进密码空间、连哈希都没跑，
   不构成一次猜测；计入只会把锁定变成 DoS 手段）② 锁定用单调 `lv_tick_get()`，
   `0` 作"未锁定"哨兵（墙钟遇 NTP 校时会让"锁 15 分钟"变成锁 3 分钟或 40 分钟）
   ③ `hmi_login` 按有无用户表**语义分流**（有表则只跳屏、不授权 —— 否则点一下按钮就已经进入会话）
   ④ 校验通过后仍走唯一接缝 `hmi_login_as_role`（角色→权限只有一条路）。
4. **模拟器端不做凭据校验，但也不是"抄一份"**（抄一份会长出第二份实现并漂移 ⇒"预览能登、设备登不上"）：
   把「用户 → 权限掩码」在**生成期**算好内联进 `main.c`，用 `--login <用户名>` 选择预览身份；
   `hmi_login_submit` 在模拟器里有定义但一律拒绝并说清原因（不静默放行、不假装校验过）。
   掩码单一来源 = `SecurityConfigCodeGen.BuiltInRoles` + `MaskHex`（避免两处各推一遍角色表）。
5. **链接契约**：生成的 `generated_hmi.c` 新增对 `hmi_login_submit` 的引用 ⇒
   `SimulatorLinkContractTests` 的穷举守卫（从生成代码反推宿主依赖）自动纳入它 ——
   漏给模拟器补定义会在单测阶段变红，而不是等 `cmake` 链接期。
6. **诚实定位**：这套东西防的是**误操作**，不是攻击 —— 单轮 SHA-256 + 1000 次迭代，
   拿到固件 dump 后离线爆破弱口令仍是分钟级。抗离线爆破需要 PBKDF2 / Argon2 级 KDF，
   本轮**刻意不做**（车间 HMI 的主威胁模型是"操作员越权改配方"，不是"攻击者物理接触设备"）。
7. **验收**：全量 7 程序集 **2397 / 0**（Core 1779 / AI 272 / App.UiTests 129 / Device 76 /
   Communication 75 / Simulator 44 / Hardware 22，基线 2332 ⇒ **+65**）；`ci-gate.sh` **5 步全过**
   （含固件 host 6 步：`hmi_crypto_test` **40/40**、`hmi_actions_credential_test` **41/41**）；
   负向验证 **4/4 命中**（交换实参顺序 / 模拟器无条件授权 / 模拟器漏定义 / 固件长度计入计数）。
   > 门禁**第一次跑红了一条与本轮无关的** `GitWorkflowServiceTests.InitializeRepository_SecondCall_Idempotent`
   > （单跑 8/8 绿、4 s ⇒ flaky），已登记为阻断项 11；第二次跑全绿。
   专题见 `docs/2026-09-16-runtime-credential-verification.md`。

## 阶段 7（运行时用户表的编辑器 UI）— 2026-09-16

承接阶段 6 遗留 2：凭据链路已通，但**没有任何界面能配账户** —— `SecurityConfig.RuntimeUsers`
只能手工编辑工程 JSON，而手填的哈希要先算 `Base64(SHA256 迭代 1000 次(salt ‖ utf8(pwd)))`，
实际上等于配不了。

1. **落点在工程设置 → 安全设置面板**（不新开对话框）：用户表随工程保存、属工程级配置，
   而该面板已有操作模式 / 会话 / 密码策略 / 锁定策略 —— 用户表正是这些策略的**作用对象**，
   也复用了对话框既有的确定/应用/取消保存链。**刻意不混进 `UserManagerView`**：那是编辑器
   **自身**账户（`AuthenticationService` 的 `UserProfile`，进程内、不落工程），两者是
   安全边界关系，共用界面等于诱导用户以为「这里建的用户设备上也能登」。
2. **草稿语义：两层拷贝缺一不可**。用户表是集合、绑不到单个控件上 ⇒ 引入
   `_runtimeUsersDraft`；`BindRuntimeUsers` 深拷贝一次（否则编辑期就在改模型，点取消改动已生效
   ——「取消了却没取消掉」比不做还糟），`SaveRuntimeUsers` **再拷一层**（否则模型与草稿共享
   对象引用，点完确定再接着编辑仍会改到模型）。`RuntimeUser.Clone()` 放模型里而不是让调用方
   手写字段拷贝 —— 手写版本在本类新增字段后仍能编译，只是静默漏拷一个字段。
3. **密码点按即哈希**：界面只出现「已设密码 / 未设密码」，明文只在输入框里存在到点下按钮为止；
   新增用户**刻意不给默认口令**（预填默认口令比"暂时登不上"危险得多，默认口令几乎不会被改）。
   长度下限取当前面板里的 `PasswordMinLen` 而非模型里的旧值。
4. **校验器与固件同源**（`Core/Security/RuntimeUserValidator.cs`，返回**结构化问题**、不产文案
   ⇒ Core 侧断言枚举、文案由 App 层映射）：四条规则 —— 空用户名 / 重名（线性查表**首个胜出**，
   后面的改了没反应）/ 角色表外（`hmi_role_lookup` 返回 -1 ⇒ 口令对了也登不上）/ 已启用但无密码。
   比对一律 `StringComparison.Ordinal`（固件是 `strcmp`）：用 `OrdinalIgnoreCase` 会把 `Op`/`op`
   误报成重名，或放过 `ROLE-OPERATOR` 这种表外角色。**「停用且无密码」不算问题**——
   停用账户在设备端直接拒绝，算成问题只会让"留着以后启用"的账户一直飘红字。
5. **预置角色清单收敛到 `UserRole.BuiltInAll`**：`SecurityConfigCodeGen.BuiltInRoles` 改为
   **纯转发**（唯一清单在 Models 层，因为 Security 层与 App 层也要读它）。新增转发守卫：
   谁改成手写清单且内容不一致就红；并附带钉住「表达式体 ⇒ 每次返回新实例」
   （收敛成共享静态字段会让调用方的改动污染下一次调用）。
6. **顺带修掉阶段 6 的一处错误注释**：`SecurityConfigCodeGen` 里说空用户名条目
   「设备端任何输入都匹配不到它（死条目）」是**错的** —— `hmi_login_submit` 只挡 `user == NULL`
   不挡空串，`hmi_user_lookup("")` 会以 `strcmp("", "") == 0` **命中**它。也就是说空用户名条目
   比正常条目**更容易**被命中（仍需过口令校验）。注释按实际行为重写。
   ⇒ 把没验证过的行为写进注释比不写更糟：它会被后来人当事实用。
7. **验收**：全量 7 程序集 **2420 / 0**（Core **1802** / AI 272 / App.UiTests 129 / Device 76 /
   Communication 75 / Simulator 44 / Hardware 22，基线 2397 ⇒ **+23**）；`ci-gate.sh` **5 步全过**；
   `dotnet build src/HmiEditor.App -c Release` **0 警告 0 错误**；负向验证 **5/5 命中**；
   本地化 zh-CN / en-US 各 **+25** 键（2028 → 2053，**锚点插入不重排文件** —— 现有
   `Strings.*.json` 整体不是字典序，排序写回会产出几千行假 diff）。
   专题见 `docs/2026-09-16-runtime-user-editor-ui.md`。

**已知口径差异（登记不修）**：`PasswordMinLength` 的模型语义是**字符数**、编辑器 UI 也按字符数判，
而固件 `hmi_login_submit` 用 `strlen`（**UTF-8 字节数**）。偏差方向是设备端更宽松
（字节数 ≥ 字符数），不会造成「编辑器放行、设备拒绝」这种危险方向；实际影响是中文密码在设备端
比预期更容易满足下限。真要统一应统一到**字节数**（固件是判据的最终执行者）。

## 阶段 8（会话超时端到端 + 审计落盘）— 2026-09-16

承接阶段 7 末尾盘点出的缺口。症状不是"功能没做"，而是**UI 在撒谎**：安全设置面板里
`SessionTimeoutMin`（默认 30 分钟）/ `AutoLogoutEnabled`（默认开）/ `LockScreenId` 三项
**模型有值、界面能配、默认就是开的**，而生成头里**从来没有对应宏**、固件也没有任何会话超时实现
⇒ 用户配了等于没配，且因为默认就是开的，**所有工程都受影响**。

1. **设备端语义照抄编辑器**（权威来源是 `AuthenticationService.CheckPermission` 已实现的那一份）：
   ① 「有人在问权限」本身就算活动 ② 判据**严格大于** ③ 超时 ⇒ 清会话 + 跳锁屏 + **拒绝本次判定**。
   活动打点**复用已有接缝**（`hmi_require_permission` → `hmi_has_permission` 入口），不新增
   `lv_indev` 钩子 —— 编辑器侧本来就没有输入事件钩子，它的"活动"定义就是"有人在问权限"，
   **同构而非妥协**。落地前先查清一个风险：`hmi_has_permission` 若被**周期性**调用，活动时间会被
   刷成"永久在线"、超时永不触发 —— 实测它只被 `hmi_require_permission` 调用（生成代码只发射后者）。
2. **判定入口的顺序也是契约**：`评估超时 → 打点 → 判定`。两条顺序各对应一个真缺陷 ——
   评估跑到判定之后 ⇒ 超时那一刻的操作先被放行再登出（**把这次操作送出去了**）；
   打点跑到评估之前 ⇒ `touch` 先把 deadline 推到 now+阈值、评估必然算"未超时"（**超时永不触发**）。
   第二条更隐蔽：两行都在、都能编译、单看任一行都合理。两侧都补了判据（C# 比文本先后 + C 测行为）。
3. **`LockScreenId` 生成期解析**（新增 `Core/CodeGen/LockScreenTargets.cs` 唯一来源）：它是屏幕 **Id**，
   而设备端 `hmi_screen_switch` 按**名字**匹配。四分支：命中 → 用该屏名字 / 配了但屏不存在 → 回退登录屏 /
   未配 → 回退登录屏 / 生成时无屏幕清单 → 空串（不切屏）。回退目标选**登录屏**而非模型注释里的
   "系统默认锁屏（黑屏+时间）"：超时的语义是"这个人已不被信任"，重新获得信任的唯一途径就是登录；
   顺带让**不配 `LockScreenId` 的工程也有正确行为**。解析不出来**不是错误**：会话照样被清，
   丢的只是"屏幕自己跳走"这个视觉提示。
4. **派生开关下沉到生成头**（拒绝第二份判定）：`HMI_SEC_SESSION_ENABLED` 由生成头给出，
   固件与模拟器**读同一个名字**。若模拟器自己再推一遍就长出第二份判定，漂移表现是
   "预览会跳锁屏、设备不会"。刻意放在该段**末尾**：`HMI_SEC_ENFORCE` 可被 `-D` 覆盖，
   只有那一行求值才反映最终值。
5. **审计落盘**：`/mnt/UDISK/hmi/audit.log`（唯一持久可写分区），超 256 KB 滚成 `.1`（只保一代）；
   五类事件（会话建立 / 登录被拒 / 锁定 / 超时 / 登出）**先写串口再写文件**（落盘失败时这条记录
   不会凭空消失）。两个字段**都刻意不下发**、理由各不相同：`EnableAuditLog` 是**编辑器侧**开关
   （编辑器语义是"认证事件始终记录、不受它影响"）⇒ 设备端只有认证类审计、**没有可作用的对象**，
   发一个不生效的宏是误导；`AuditLogStoragePath` 是**编辑器本机**路径，发给设备即成**跨机路径**
   （用户填 `D:\logs\audit.log` 在设备上必然静默写不进去）。时间戳用**墙钟**、超时判定用**单调 tick**，
   两件事不能混（NTP 校时会让"锁 15 分钟"变成锁 3 分钟，项目里已有一次墙钟回卷的教训）。
6. **验收**：全量 7 程序集 **2438 / 0**（Core **1820** / AI 272 / App.UiTests 129 / Device 76 /
   Communication 75 / Simulator 44 / Hardware 22，基线 2420 ⇒ **+18**）；`ci-gate.sh` **5 步全过**
   （固件 host **7 步**全过，新增 `hmi_actions_session_test` **56/56**）；负向验证 **9/9 命中**。
   专题见 `docs/2026-09-16-session-timeout-and-audit.md`。

**顺带修正两处记错的事实**（方向都是**乐观的**，即"以为只差消费"实际是"链路根本没接"）：
① `docs/2026-09-16-runtime-credential-verification.md` §9 表格里「那五个字段已下发到设备端、
尚未消费」—— 只对 `EnableTamperDetection` 成立，其余四个**从来没下发**；
② `CodeGenerator.BuildSystem.cs` 里「固件代码可通过 `#if HMI_TAMPER_DETECTION` 条件编译启用
Flash 扇区 CRC 校验」是一句**从未实现的行为**（全库 grep 固件零引用），已改写为
「当前状态：下发了但零消费」+ 要闭环得先定的三件事（属**新功能**而非补缺口）。

**一条方法论教训（本轮踩到，值得独立记一笔）**：**定向 filter 跑得再绿，也替代不了一次全量门禁。**
本轮负向验证的 C# 侧用的是 `--filter`（只跑 3 个类），于是**新写的**
`Assert.Contains("int main(int argc, char** argv)", …)` 逃过了全仓扫描守卫
`TestAssertionStyleGuardTests`（判据是"字面量同时含 `(` 与 `,`"），直到全量门禁首跑才被点名。

## 阶段 9（RuntimeConfig 字段下沉：10 个字段里只有 1 个走到设备）— 2026-09-16

承接阶段 8 末尾提出的手法（「盘点到设备端落地情况」找出会话超时那个洞），本轮把同一手法扩大到
**全部工程配置**，选中 `RuntimeConfig`（名字即"运行时"、10 个字段、与已闭环的 `SecurityConfig` 同族）。
症状与阶段 8 同类但更严重：**10 个字段在工程设置面板里全部有完整 UI**（`ProjectSettingsView.axaml.cs:359-367`
/ `473-482`），其中 5 个还能被 AI 工具 `update_runtime` 改（`AgentToolFactory.cs:894`），
而**只有 `BootScreenId` 一个真的走到了设备**（`CodeGenerator.BuildSystem.cs` 下发的两个宏都来自
`SecurityConfig`，一个都不来自 `RuntimeConfig`；固件的 `runtime_config.h` 只是**名字撞车**）。

1. **按根因分三类，只做能做的那一类**（用户选定的切法：低成本真闭环）：
   **A 描述本身错**（`WatchdogTimeoutMs` 承诺的"MCU 喂狗复位"在 T113 Linux 用户态**没有对应物**；
   `EnableHmiReboot` 指向的系统变量 `$SYS.Reboot` **在编辑器里根本不存在**——系统变量是 `$SYS$XXX` 形式）；
   **B 有现成固件位点**（`ScanIntervalMs` / `LogLevel`：固件里已有 `HMI_POLL_INTERVAL_MS` 与一套日志宏，
   只是硬编码）；**C 需先立功能**（其他 5 个：设备端连机制骨架都没有 —— 无统一心跳帧、无任务池、
   无崩溃转储、`/rom` 是只读 squashfs）。
2. **B 类真闭环**（新增 `Core/CodeGen/RuntimeConfigCodeGen.cs` → `generated_runtime.h`，经
   `__has_include` + `#ifndef` 缺省回退消费）：`ScanIntervalMs` → `HMI_POLL_INTERVAL_MS`
   （非法值挡回 100 并留诊断注释：0/负值会让采集线程忙等烧 CPU）、`LogLevel` → `HMI_LOG_LEVEL`
   + 5 个 `HMI_LOG_LVL_*` 常量。**两个可配值都用 `#ifndef` 包住**——host 测试靠 `-D` 跑三种门槛，
   不包就只能测一种，等于没测门槛。
3. **回退分支"提不提供数值"是语义决定，两个值刻意不同**：轮询周期**必须**回退到 100
   （采集循环无条件要用这个数字，没有就编不过）；级别常量**不给**
   （门槛关闭时 `HMI_LOG_AT` 走的是**丢弃 `lvl` 实参**的 `#else` 分支 ⇒ 给了就是**在设备端另立一份
   级别定义**，正是本轮要消灭的东西）。靠的是 C 标准"替换列表里没有的实参连展开都不发生"这条细节。
4. **日志宏独立成文件 `hmi_log.h`**（**刻意不 include lvgl.h**）：原宏挂在聚合头 `hmi_runtime.h` 后面，
   而聚合头拉进 `lvgl.h`、host 测试没有 LVGL ⇒ 门槛链路在主机上**编不过**，闭环只能退化成"注释声称生效"。
   两档用法写进头注释：`HMI_LOG(...)` **无条件**（启动横幅 / FATAL / 操作审计，被静音等于关掉现场排查
   能力）；`HMI_LOG_DEBUG/INFO/WARN/ERR` 按门槛裁剪，语义是「输出严重度 **>=** 配置值」。
5. **A 类修正为"把错描述改对"而不是删字段**（删会破坏老工程 JSON 兼容），并把
   「已闭环 3 / 未下发 5 / 描述错 2」三张清单**直接写进 `RuntimeConfig` 类注释**，
   逐字段标注「未下发 + 要闭环得先做什么」。这是本项目第 3、4 例同类问题（阶段 7、阶段 8 各修过一处
   "把没验证过的行为写进注释"）。顺带标注一条**不改**的重复：`RuntimeConfig.ScanIntervalMs` 与
   `VariableManager.DefaultScanIntervalMs` 语义重复（都默认 100、都叫"扫描周期"、都零消费），
   合并属产品决策。
6. **有意的行为变更**：`LogLevel` 的默认值（Warning）此前只是纸面上的，接上后立刻生效 ⇒
   固件里 19 处硬编码的 `HMI_LOG("DBG: …")`（`main.c` 12 + `drivers/lcd_sunxifb.c` 7）改用
   `HMI_LOG_DEBUG`，**默认不再输出**。不是回归，是"默认值终于生效"的必然结果。
7. **验收**：`RuntimeConfigCodeGenTests` **31/31**；全量 **2469/2469**（Core **1851** = 1820 + 31；
   AI 272 / App.UiTests 129 / Device 76 / Communication 75 / Simulator 44 / Hardware 22，全部 0 失败）；
   `ci-gate.sh` **5 步全过**（固件 host **7 步 → 8 步全过**，新增第 8 步 4 场景 × 8 判据）；
   负向验证 **8/8 命中**（7 组 C# + 1 组 C；首轮 1/8、次轮 7/8，两次都定位到**扫描器自身**的判据缺陷，非守卫无效）。
   专题见 `docs/2026-09-16-runtime-config-sinking.md`。

**一条方法论教训（本轮第二轮踩到，与阶段 8 那条同为"判据自身有洞"）**：**负向验证的「扫描器」
自己会坏，且坏法与"守卫无效"完全无法区分。** 首轮 8 条注入只报 1/8 命中 —— 而目标用例其实都红了。
第一层根因是块头判据 `用例名 and '[FAIL]'` 命中的是 **xUnit 末尾的汇总行**（没有错误消息、没有堆栈）；
修完变成 7/8，第二层根因是块头判据 `失败\s+(\S+)\s*\[` **匹配不上 Theory 的块头**（`(bad: 2147483647)`
**参数里带空格**），而 `[Fact]` 名字里没空格 ⇒ 只测 `[Fact]` 的扫描器会一直是绿的。
两处修完加**扫描器双向自检**（绿日志不得判出块 / xUnit 汇总行不得判出块 / 真块必须判出 /
**Theory 块头必须识别**），第三处顺手把 C 侧那条 needle 从 `FAIL` 换成本注入真正打破的判据名
（`FAIL` 太松：任何一条判据失败都含它）。

**顺带记准一条之前记错的数字**：`update_runtime` 能改的是 **5** 个参数（不是侦察时记的 4 个），
其中 `watchdog_timeout_ms` / `enable_hmi_reboot` 是**空转的**—— AI 照着工具描述改一个死键，
用户看到的却是"已更新运行时配置"。已记入遗留，建议下一步要么删参数、要么把"当前不生效"写进描述。

## 阶段 10（「UI 在撒谎」五类根因的系统性收口）— 2026-09-16

承接阶段 8/9 反复撞见的同一症状：**面板上有控件、模型里有值、界面能配，但画布/产物/设备上什么都不变**。
阶段 9 只在一个类（`RuntimeConfig`）上做过这件事，本轮把它扩到**全部工程配置 + 应用级设置**，
按根因分五类**一次收口**（用户选定的切法是「全量 A+B+C+D+E」，不分批）：

| 类 | 形态 | 本轮处置 |
|---|---|---|
| **A** | 描述本身错 | `ExportConfig.GenerateDebugSymbols` 许诺的 J-Link/ST-Link 调试（T113 是 ARM Linux，工具链是 `openixSuit`/ADB）⇒ 文档重写为真实行为 |
| **A′** | 注释与实现相反 | `CodeGenerator.BuildSystem.cs` 那句「固件可通过 `#if HMI_TAMPER_DETECTION` 条件编译启用 Flash 扇区 CRC」**从未实现**（固件全库零引用）⇒ 改写为「下发了但零消费」+ 要闭环得先定的三件事 |
| **B** | 同一操作多套语义 | 「新建屏幕」有 **5 处各写各的**默认宽高（800×480 / 1024×600 / 工程默认 / 无）⇒ 收成唯一工厂 `Core/Services/ScreenFactory.cs` |
| **C** | 重复建模（两套都死） | 编辑器偏好同时活在 `EditorConfig` 与 `SettingsService`，**两套都没有消费点**（网格 8 / 撤销 50 / 标尺 / 实时预览…） |
| **D** | 下发了但无消费 | 应用级 + 工程级共 **30 个**字段面板上有控件、点下去没有后果 |
| **E** | 同一语义多处建模 | 色深四处建模，其中两处**同名枚举**（CS0104 歧义引用）；面板候选项另手写一份字符串数组；设备 JSON 解析又一份 switch |

1. **A / A′：改描述，不删字段。** 删字段会破坏老工程 JSON 兼容。两处改成
   「只影响构建配置」/「当前下发了但零消费」，并把 `GenerateDebugSymbols` 真的接上 `-g`
   （`GenerateCMake` / `GenerateMakefile` / `GeneratePlatformIni` 三处）；默认 false ⇒ **产物逐字节不变**。
2. **B：`ScreenFactory` 单点兜底。** 优先级契约 **显式参数 > 工程默认 > 1024×600**，
   并把「没给」与「给了 800」区分开 —— `AiScreen.Width/Height` 由 `int = 800/480` 改成
   **`int?`**（`null` = 用下游默认）。这是本轮唯一一处**面向 AI 层的契约变更**：
   过去 AI 不填尺寸时被静默当成 800×480，工程默认值根本轮不到。`src/` 下 **7 处** `new HmiScreen` 全改走工厂。
3. **C：应用级设置成为唯一权威。** `EditorConfig` 降级为**只读兼容载体**
   （只为老 `project.hmi` 无损往返，类注释写明「不要再往上加字段」）；工程设置面板里的
   「编辑器设置」分类整块移除（代码 + XAML −95 行 + 11 处 handler + 2 个失效方法）。
4. **C 的落地：四条编辑器偏好真正接线，默认行为逐像素不变。**

| 偏好 | 应用级键 | 接到 | 出厂默认（= 接线前的硬编码） |
|---|---|---|---|
| 网格点间距 | `Interaction.GridSize` | `SnapGrid.GridSize` | 10 |
| 显示网格 | `Interaction.ShowGrid` | `SnapGrid.IsVisible` | true |
| 吸附步长 | `Interaction.SnapStep`（新增） | `DesignCanvas.SnapStep` | 5 |
| 吸附开关 | `Interaction.EnableSnapToGrid` | `DesignCanvas.SnapEnabled` | true |

   改设置走 `SettingChanged` **即时生效**，不必重启。
5. **网格那一块比侦察结论更严重。** 侦察判的是「C 类 = 一套死一套活」，实测是
   「**两套都死 + 三处建模 + 三个不同数字**」：编辑器设置说 8、画布 XAML 硬编码 `GridSize="10"`、
   吸附阈值写死 5。本轮把**点阵（10px）与吸附（5px，二分格）明确为两个数**
   （合并会连带改掉拖拽精度），点距唯一来源 = `SettingsService.Interaction.GridSize`，XAML 不再写死。
6. **`SettingsService` 曾经「重启即回默认」（本轮最隐蔽的缺陷）。** 反序列化
   `Dictionary<string, object?>` 从 `System.Text.Json` 拿到的是 **`JsonElement`**，
   而 `Get<T>` 用 `value is T` 判等 ⇒ **磁盘上每一个值都被判成类型不匹配、退回默认值**。
   更坏的是**打开一次设置对话框会把默认值灌回控件 → 触发 `ValueChanged` → 写回文件**，
   把用户存的值抹掉（用户视角是「设置存不进去」，实际是被自己打开对话框抹了）。
   修法：`Dictionary<string, JsonElement>` + `Materialize()` + `Get<T>` 折算。
   同时加 `SchemaVersion`（= 2）+ 一次性迁移：把 v1 那个**从无消费点**的 `GridSize = 8`
   对齐到画布真实值 10，但**用户真的改过的值保持不动**。
   迁移判据踩了一个 C# 类型推断的坑：`int : long : double` 三元链的公共类型是 **`double`**，
   整数会被装箱成 `double` 从而让 `is int` 判据**静默失效** ⇒ 必须写成早期返回。
7. **D：受管清单 + 三向守卫。** 登记 30 个「面板上有、其实不生效」的字段（应用级 14 + 工程级 16），
   配 `NotYetEffective` 标注（灰显 + 悬停给出原因）与图例。守卫三向：
   清单 ↔ **字段真实存在** ↔ **白名单外零消费**，外加清单 ↔ 面板标注
   （**键 + 控件名 + XAML 里真有该 `x:Name`** 双向一致）。两条判据定得很小心：
   **「写入面」≠「消费面」**（`AgentToolFactory` 把 AI 传来的值写进工程配置但没人读 ⇒ 算未接线）；
   **`Control = null` 是第三态** —— 键没人读、但控件真生效（`Theme.AccentColor` 写的是
   `AppThemeService.CustomAccent`），不区分会把能用的控件灰掉。维护方式是**接线后删条目**。
8. **E：色深收口。** `ImageOptimizer.ColorDepth` 改名 `ImageColorDepth` 消歧义（14 处）；
   面板候选项改为 `Enum.GetNames(typeof(ColorDepth))`；设备 JSON 的三个手写 switch
   （色深 / 显示接口 / 通讯接口）全改 `Enum.TryParse + Enum.IsDefined`
   —— 只 `TryParse` 不够（`"99"`、`"7"` 也返回 true）。
9. **验收**：全量 7 程序集 **2535 项**（Core **1900** = 1851 + 49、AI 276 = 272 + 4、
   **App.UiTests 142** = 129 + 13、Device 76、Communication 75、Simulator 44、Hardware 22；
   基线 2469 ⇒ **+66**）；负向验证 **5/5 命中**（3 组见专题、2 组为新增的 git 窗口守卫）；
   固件 host **8 步全过**（本轮固件零改动）。
   专题见 `docs/2026-09-16-config-honesty-and-editor-preferences.md`。

**门禁结果（要说清楚，因为它是红的）**：`ci-gate.sh` 第 1~3 步全过（脚本语法 21 / 路径卫生 /
Release 构建 **0 警告 0 错误**）；**第 4 步全量测试红 1 条** —— 三次重跑都是同一条、且只有这一条：
`GitWorkflowServiceTests.InitializeRepository_SecondCall_Idempotent`，即**阻断项 11**
（阶段 6/7/8 各复发过一次的既有 flaky）。**它不是本轮引入的**：整个 Core 程序集单跑
**1900/1900 全绿**（1m11s，实测一次）与红一次（1899/1900）交替，
定向 filter `GitWorkflowServiceTests` 两次都 8/8 绿。第 5 步（固件 host）因第 4 步失败未由门禁执行，
已单独跑：**8 步全部通过**。

**顺带修掉一条本轮新撞出来的门禁假红**：`ModbusTcpRuntimeDriverTests.ReadCoils_UnpacksBits`
报「通讯超时(2000ms)」—— 那个 2000 ms 是**测试自己的 helper** 塞的
（`CreateConnectedDriver`），不是产品默认值，属于「判据用过窄的绝对余量」的又一例
（与阶段 8 修过的 `DeviceChannelTests.SlowFirstByte_…` 同族）。正例窗口统一拉到 10 s，
需要「必须在超时后报错」的负例（`UnresponsiveDevice_TimesOut_…` 设 300 ms）各自覆写、不受影响；
改后**连续两跑 Communication 75/75 绿**。

**一条诚实的记录（做了，但没解决它指向的那个问题）**：本轮顺手把「等 git 进程」的窗口收成单一来源
（`GitWorkflowService.IsGitAvailable()` 原为 3 s、`RunGit()` 为 15 s ⇒ 同为 `GitProcessWaitMs`），
并加结构守卫 `GitProcessWaitWindowTests`（6 项）+ 负向验证 2/2。
**它没有解决阻断项 11** —— 但顺着它把根因查到了底（取证表见阻断项 11、命令见验证记录）：
**`git` 本身完全健康**（`--version` 0.8 ms、`rev-parse` 0.9 ms、`init` 60 ms，200 轮 0 失败，
`/tmp` 峰值 116 KB / 10 MB ⇒ 磁盘假设排除），失败出在 **.NET 测试宿主内部**的进程创建/等待
被拖到秒级甚至分钟级。因此这条改动静止于「一致性修正」，**不是**该 flaky 的解药；
并且**不构成行为回退** —— `GetState` 在同一路径上紧接着就会调 `IsRepository`（本来就是 15 s 窗口），
UI 的最坏阻塞没有变化。

## 阶段 11（LLM 流式响应：SSE 打通到界面）— 2026-09-17

承接 `docs/2026-09-14-optimization-directions.md` 的 **P2-5 / D9**（原文：「AI 无流式响应 ——
`src/HmiEditor.AI` grep `stream` **0 命中**」；完成标准：*长任务过程中 UI 有逐步轨迹；取消可中断*）。
专题见 `docs/2026-09-17-llm-streaming-sse.md`。

症状不是"少个开关"，而是**接口形状决定了写不出来**：`ILlmProvider` 只有
`Task<LlmResponse> ChatAsync(...)`，返回完整响应、中途没有任何出口。Agent 多步任务至少还有
工具步骤在动，而**单次最长调用**（生成屏幕/模板/脚本/图片，复杂画面 30~120 s）全程只有一条
静止的「正在生成, 请稍候…」。

1. **能力接口而非主接口成员**：新增 `IStreamingLlmProvider`（独立接口 + `ChatStreamAsync`）。
   流式是**能力**不是**契约** —— 加进 `ILlmProvider` 会让每个实现（含测试替身、将来的本地模型）
   都被迫提供一份"假装流式"的桩，而调用方**无法区分真流式与桩**。独立接口让能力可探测，
   不支持就回退 `ChatAsync`，**行为与今天完全一致**。
2. **切帧与语义分离**：新增 `SseReader`（`data:`/`event:`/空行分帧/注释/多行 data/CRLF）。
   两个 Provider 各写一份切帧代码的必然结局是分叉 —— 项目已有多处同类教训（阶段 6 收帧判据、
   阶段 9 回退数值）。刻意容忍三种现实：中转不补帧尾空行、心跳注释行、无冒号的坏字段行
   （**一行坏数据不该让整次生成失败**）。
3. **单一调用入口** `LlmChat.WithProgressAsync`：把「支不支持流式 → 走哪条路 → 非流式补一次回调」
   三条判据收成一处。**不变量：只要传了观察者，无论 Provider 支不支持流式，都至少收到一次回调**
   —— 没有它，调用方就得为"真流式/假流式"写两套 UI 逻辑。首版我在 `AgentEngine` 里写了一份私有副本，
   随第 4 步的生成器接线一并收口（否则就是"同一判据两份实现"的现场）。
4. **两个 Provider 真流式**：OpenAI 走 `stream: true` + `stream_options.include_usage`
   （不加它 token 用量在流式下**凭空消失**）；Claude 走 `stream: true`，**结构化输出时正文取
   `input_json_delta` 而非 text block** —— 取错会让生成结果恒为空且不报错，测试专门钉住。
   OpenAI 侧沿用既有三层结构模式降级；**降级不会重复吐字**因为 HTTP 状态先于正文到达
   （见第 8 条的自纠）。
5. **接通全部调用路径**：5 个生成器 + `AiOrchestrator` + `AiPreviewManager` 四件套 +
   `AgentEngine`/`AgentHost` 全部加可选 `IProgress<string>? textProgress`（放 `ct` 之后，
   **不破坏既有位置参数调用**）。
6. **界面**：`ChatEntry` 新增 `LivePreview`/`StreamingStatus`/`IsStreaming`，运行中气泡显示
   **尾部 600 字**实时预览 + 「已接收 N 字」。三个刻意的取舍：预览只留尾部（一轮可能吐几十 KB）、
   节流重绘 120 ms（`ChatEntry` 无 INPC，刷新靠重挂 `ItemsSource`）、工具步骤到达即撤掉预览
   （那意味着该轮输出已解析完，原始 JSON 继续挂着会与步骤卡片重复）。
7. **App 层接线守卫**：`textProgress` 与 `CancellationToken` 同样是**可选参数** —— 漏传照常编译、
   照常通过行为测试，后果是"界面全静止"的静默失效。`AiStreamingWiringTests` 静态扫源码，
   断言 4 条意图分支 + Agent 调用都必须接上观察者，且 `AiPreviewManager` 不得半路丢弃它
   （与既有的 `AiCancellationWiringTests` 同一套路）。
8. **一处自纠：负向验证暴露出我自己写的死代码。** 首轮 NV 注入「去掉 OpenAI 降级时的 `emitted`
   检查」**没能让任何用例变红** —— 追下去发现那不是测试不够，而是
   `catch (...) when (IsRequestShapeError(ex) && !emitted)` **按构造不可达**：
   `StreamOnceAsync` 在响应体开始读取之后不可能再抛 `LlmProviderException`
   （非 2xx 在正文之前抛；正文读取期是 `IOException`/`OperationCanceledException`，不带状态码）。
   我最初的类注释把它写成了"防止正文重复"的**主动机制** —— 一句**没被验证过的行为声明**，
   正是本项目反复吃过亏的那一类。处置：**删掉不可达分支**（与 `ViewLocator` 死工厂分支同一把尺子），
   把"流中途失败"改成**真实可达**的用户可见行为（OpenAI `{"error":…}` 帧 / Claude `error` 事件
   → 抛错、**保留已吐正文**、不重试），用 NV-D / NV-E 钉住；注释改写为真实机制。
9. **验收**：全量 7 程序集 **2577 / 0**（Core 1900 / AI **313** / App.UiTests **147** /
   Device 76 / Communication 75 / Simulator 44 / Hardware 22；基线 2535 ⇒ **+42**）；
   `ci-gate.sh` **6 步全过**（含固件 host 8 步）；负向验证 **6/6 命中**（1 组接线 + 5 组行为）；
   本地化 zh-CN / en-US 各 +2 键、占位符数量一致。

**范围边界（本轮不做，已登记）**：并行 tool_call（P2-5 后半，属独立架构改动，与流式无耦合）；
非 Agent 路径的"逐字打字机"排版（预览是尾部窗口，正式正文仍整段替换）；
`IconGenerator`/批量图标未接观察者（生成器已支持该参数，接线属一行机械改动）；
无真实网关联调（全部用注入 `HttpMessageHandler` / 自造 SSE 流，真实网关的分片粒度、
心跳间隔、代理缓冲只能靠现场验证）。

## 阶段 12（配置诚实性收口二：导出闸门 + AI 工具描述）— 2026-09-17

承接阶段 10（「UI 在撒谎」五类根因）与 `docs/2026-09-16-runtime-user-editor-ui.md` §7.3、
`docs/2026-09-16-runtime-config-sinking.md` §9 登记的两条遗留。专题见
`docs/2026-09-17-config-honesty-export-gate-and-ai-tool.md`。

阶段 10 把「面板上有控件、其实不生效」的 30 个字段登记成数据并加了灰显 —— 本轮补的是
**同一类缺陷另外两个没被覆盖的入口**：

1. **缺陷一：运行时用户表的校验没接进导出闸门。** `RuntimeUserValidator`（阶段 7）四条规则写好了、
   **唯一消费点是设置面板的提示**；导出路径完全不知道它存在，而 `MainWindow` 的导出/模拟器导出
   **已经有** `GeneratedCodeValidator` + `HasErrors` 闸门。后果不是"少个账户"而是**设备不可用**：
   设备端 `hmi_actions.c` 在 `HMI_SEC_USER_COUNT > 0` 时**刻意不授权**（否则"点一下登录"就以默认角色
   进入会话、凭据校验被绕过）⇒ **表非空但没有一条可用**时任何人都建立不了会话、
   所有权限门**永久拒绝**（现场表现「点了没反应」，归因成本极高）。修法：新增
   `FindingCategory.RuntimeUser` + `ValidateRuntimeUsers`，**直接复用** `RuntimeUserValidator`
   （一条规则都没重写）；定级逐条对应设备端真实后果 —— `UnknownRole`/`EnabledWithoutPassword`
   以及**整表 `CountUsable == 0`** 为 Error（拦导出），`EmptyUsername`/`DuplicateUsername` 为 Warning；
   **表为空时刻意什么都不报**（那是"没配运行时用户表"的既有形态，报错会拦下所有老工程）。
2. **缺陷二：AI 工具在广告没有执行路径的参数。** `update_runtime` 的描述与 schema advertised 了
   `watchdog_timeout_ms` / `enable_hmi_reboot`，而这两个 `RuntimeConfig` 字段的**唯一落点是写入面**
   （AI 工具 + 设置面板），**没有任何读取方** ⇒ 模型"照着描述改一个死键"，回给用户的却是
   「已更新运行时配置」。这是"UI 在撒谎"的同族，只是**撒谎的界面换成了 AI 工具描述**。
   处置选**拿掉参数**（参数是工具接口、不是持久化数据，拿掉不破坏老工程 JSON；
   字段与面板控件原样保留），描述里用中文说明"这两项不可改"但**不出现 snake_case 参数名**。
3. **守卫：判据从"真实消费"推导，不抄清单。** `UpdateRuntimeSchema_AdvertisesOnlyRuntimeConfigFieldsThatAreActuallyRead`
   对 `RuntimeConfig` 每个属性扫产品源码（白名单＝写入面/清单/声明文件），断言"广告的参数 ∩ 无读取方 = ∅"，
   并断言描述里不出现死参数名。另有两条自检：探测器必须真的算出那两个死字段；`ToSnakeCase` 有独立正反例。
   **首版守卫依赖 `ConfigFieldInventory.NotWired` 反推，测试立刻红了 —— 而且红得有价值**：
   `EnableHmiReboot` **有意不在**那份清单里（见其类注释：这一类要的是补执行路径、不是标未生效），
   所以首版即使实现正确也**抓不到本轮缺陷的一半**。改为从真实消费推导后，两个字段都在集合里，
   且将来任何新死字段会被自动纳入。
4. **一条刻意的守卫设计**：`GeneratedCodeValidator` 的定级 `switch` 有 `_ => Warning` 兜底 ⇒
   新增一种 `RuntimeUserIssueKind` 时会**静默变成 Warning（不拦导出）**。新增
   `EveryIssueKind_HasAnExplicitlyDecidedSeverity` 用显式"期望定级表"把这件事逼成**必须做的决定**，
   并反向检查表里没有已删除的类型。
5. **验收**：全量 7 程序集 **2595 / 0**（Core 1900 → **1912**、App.UiTests 147 → **153**；
   本轮基线 2577 ⇒ **+18**）；负向验证 **7/7 命中**（4 组导出闸门 + 3 组工具守卫）；
   端到端验收 `verify.sh` **退出码 0**（纯 C# 改动、未触及生成链，属回归确认）；
   本地化 zh-CN / en-US 各 +1 键。

**范围边界（本轮不做，已登记）**：`EnableHmiReboot` 的面板控件仍未标注"不生效"
（那是清单的**既有设计决定**，不是本轮遗漏；要加标注得先改清单的分类口径）；
两个死字段本身没删（删会破坏老工程 JSON 兼容），补执行路径仍是未开工的新功能；
`ScanIntervalMs` 与 `VariableManager.DefaultScanIntervalMs` 语义重复合并属产品决策；
用户表校验**不覆盖口令强度**（只能看到哈希、看不到长度，判不了 —— 不伪装成已校验）。

## 阶段 13（登记表全量复核 + 本地化即时刷新）— 2026-09-17

承接 `docs/2026-09-17-config-honesty-export-gate-and-ai-tool.md` 末尾的判断
（「登记的遗留项已大面积过期，下一轮应先做登记表全量复核」）。专题与**权威登记表**见
`docs/2026-09-17-open-items-register.md`。

1. **复核方法**：把 `docs/` 下**全部**「遗留/未做/待办/未开工/范围边界/后续」章节机械抽出
   （33 个文档 / 36 个章节 / 22.5 KB），**逐条**用现场证据（grep / 读码 / 跑测）核对代码现状 ——
   **不看文档自己的状态标记，只看代码**。
2. **复核结果：17 条早已闭环却仍写着"未开工/待做"**。例：`CodeGenerator` 按域拆分（现 150 行 + 12 partial）、
   两份 `GetPathCCode` 统一（`AnimationPathHelper` 唯一实现）、`sim_refresh_bindings` 的 `GetHashCode()`
   依赖（0 命中）、`LV_IMAGE_ALIGN_COVER` 死代码（0 命中）、`VariableSimulator` 的 `TimeProvider`
   注入（已用单调轴 + 测试用 `FakeTimeProvider`）、`GeneratePointerDeclarations` 不展开 Faceplate
   （已走 `EnumerateRenderableControls`）、`ParseControlType` 的 `"Screen"`（函数已不存在，
   `AiSchemaEnums` 显式拒绝）、`HmiDeployService` 固定临时文件名、`run_host_tests.sh` 产物清理、
   `AlarmEngineTests` 的 `SpinWait`、`IMG_PATH_`/`FONT_PATH_` 宏重复（`CResourceDeclarations` 唯一实现）等。
   **结论：判断某项是否还开着，唯一可靠来源是代码，不是登记表。**
3. **从复核出的真缺口里实施一项：语言切换即时刷新**（`docs/localization-summary.md` §五.2 登记项）。
   实测比原登记更严重：不只是"`LocalizationService` 没实现 INPC" —— **Avalonia 的索引器绑定
   根本不订阅 INPC**，所以"补上 INPC"修不好它。全仓 **1287 处**
   `{Binding [key], Source={x:Static res:AppLocalization.Service}}` 实为**不可观察**；
   `MainWindow.OnLanguageChanged` 靠**重建 Dock 面板**兜底，于是**菜单栏/工具栏切语言后一直是旧语言**。
   修法：新增 `LocExtension`（`{res:Loc key}`）把绑定改成"绑到会通知的 `LocalizedString.Value`"，
   1287 处 / 45 个文件一次性迁移；`AppLocalization.Get` 改为**按 key 缓存实例**
   （顺带修掉一个"订阅只增不减"的潜在泄漏 —— `LocalizedString` 构造时订阅静态事件，
   在 `DataTemplate` 里每个实例化行加一个）。
4. **先在同一个 headless 宿主里做对照实验，再改产品代码**：
   旧索引器绑定切语言后**不变**、既有 `LocalizedString.Value` 旁路**会变** ——
   后者证明宿主本身能观察到刷新，否则"不变"可能只是测试写错了（把"宿主没泵"误判成"绑定不刷新"）。
   这条对照实验同时被固化成测试，作为迁移理由的存档。
5. **迁移前提先验证再动手**：模式**完全统一**（无 `StringFormat`/`Mode` 等附加参数）、
   1099 个键全部匹配 `[a-zA-Z0-9._-]+`、45 个文件均已声明 `xmlns:res` ——
   脚本替换时断言"替换数 == 1287"与"残留 == 0"。
6. **不在文档里留"没被验证过的机制声明"**：`MainWindow.OnLanguageChanged` 的原注释称
   "XAML 索引器绑定不会自动更新，需手动重建" —— 迁移后这句不再是**这个**机制了，
   注释改写为"XAML 绑定已自动刷新；保留重建是为了覆盖**代码里读资源再赋值**的文本
   （状态栏、窗口标题、勾选）"。
7. **验收**：全量 7 程序集 **2601 / 0**（App.UiTests 153 → **159**；本轮基线 2595 ⇒ **+6**）；
   负向验证 **3/3 命中**（XAML 退回旧写法 / `LocExtension` 绑回索引器 / 破坏 key 缓存）；
   `dotnet build` **0 警告 0 错误**（Avalonia 在构建期编译 XAML，1287 处扩展解析均经构建验证）。

**范围边界（本轮不做，已登记于权威登记表）**：需产品决策的 8 项（`TemplateGenerationSchema.category`
硬约束自由字段、批量图标是否保留半成品、`ScanIntervalMs` 语义重复、`EnableHmiReboot` 面板标注、
旧数据目录是否清理、`TempRoot` 清理策略、工程目录合并、多实例隔离）；需先立功能的 6 项
（5 个未下发字段、`EnableTamperDetection`、两个死字段、并行 tool_call、AI schema 高级属性、极端迟到串帧）；
软件侧仍开着的 12 项（`ProjectSettingsView` 无行为级 UI 测试、字符数/字节数口径、测试判据绝对余量、
Windows 平台耦合等）；环境/本地残留 4 项。

## 阶段 14（`ProjectSettingsView` 行为级 UI 测试）— 2026-09-17

承接权威登记表 `docs/2026-09-17-open-items-register.md` §4 第 16 项
（软件侧仍开着里最有价值的一项）。专题见 `docs/2026-09-17-projectsettings-runtime-user-ui-tests.md`。

1. **补基建**：新增 `ProjectSettingsRuntimeUserUiTests`（**13 项**行为测试），真的把面板
   `Show()` 出来、按真实控件走完装载→编辑→取消/确定/应用 的完整生命周期；
   "草稿 vs 模型"用**引用比较**断言（`RuntimeUserList` 各项的 `Tag` 就是草稿实例）。
   两个踩过的点写进注释：整个用例体必须跑在 `UiTestHost.Run(...)` 里（控件要在 headless UI
   线程上构造，否则 `ChromeWindow` 报 *Unable to locate 'Avalonia.Platform.IWindowingPlatform'*）；
   触发用 `RaiseEvent(Button.ClickEvent)` 而非坐标点击（测接线与语义；命中测试/布局是另一条轴）。
2. **抓出真缺陷 #1（零可用被显示成常规提示）**：`RefreshRuntimeUserIssues` 的注释写着
   *"空表与『表非空但一条都进不去』**要分开说**"*，但那个分支**不存在** —— 四条规则都不覆盖
   "账户被停用"，于是全停用的表返回零问题、落进"正常"分支，显示成 **「共 0 个可用账户」+ 常规色**；
   而该形态的后果是**设备端谁都登不上、权限门永久拒绝**，阶段 12 已判为**导出级 Error**
   ⇒ **面板把阻断级状态显示成了普通说明**。修法：新增分支 + 新文案 + `SetRuntimeUserIssues(text, isProblem)`
   （**文案与颜色一起变**；只改文案不改色仍会被扫一眼当成说明）。
3. **抓出真缺陷 #2（装载后提示一直是「用户表为空」）**：`BindRuntimeUsers()` 在构造期
   `InitializeRuntimeUserSection()` **之后**才跑（那时草稿还是空的、提示已被写成
   "表为空、设备不校验凭据"），却**不重算提示** ⇒ 打开一个**已配账户**的工程，提示区显示
   *「用户表为空 —— 设备端不校验凭据，按默认角色直接授权」* —— **两句话都与事实相反**。
   它**只在装载那一刻错**，此后动一下任一控件就被后续调用纠正 ⇒ **人工走查必然漏掉**
   （人工路径是"打开→看一眼→点两下试试"，点完错误就消失了）。修法：`BindRuntimeUsers()` 末尾补
   `RefreshRuntimeUserIssues()`，并把"为什么必须在这里重算"（构造顺序 + 只在装载时错）写进注释，
   避免后人当成冗余调用删掉。
4. **负向验证 5/5 命中**：A/B 分别复现上述两个缺陷（证明确实修好）；C/D/E 钉住该面板赖以成立的
   三条不变量（第一层拷贝 / 第二层拷贝 / `OnOkClicked` 的 `_hasChanges` 闸门）。
5. **一条可复用的判据**：两条缺陷都不是"功能缺失"，而是**实现与它自己的注释/意图不符**。
   只要一个方法的注释在用「要分开说 / 必须 / 刻意」这类词描述行为，就该有一条测试去证明它真的存在。
6. **验收**：全量 7 程序集 **2614 / 0**（App.UiTests 159 → **172**；本轮基线 2601 ⇒ **+13**）；
   `dotnet build` **0 警告 0 错误**；本地化 zh-CN / en-US 各 +1 键。

**范围边界**：只覆盖**运行时用户表**这一块（该面板共 6 个分类、1183 行代码）。其余 5 个分类
（导出/显示/通讯/运行时/基本）目前仍只有静态源码扫描守卫（`ConfigFieldInventoryTests` /
`EditorPreferenceWiringTests`）—— **已在阶段 15 闭环**。

## 阶段 15（`ProjectSettingsView` 配置往返保真）— 2026-09-17

承接阶段 14 的范围边界与权威登记表 §4 第 17 项。专题见
`docs/2026-09-17-projectsettings-roundtrip-fidelity.md`。

1. **判据选"整个工程 JSON 逐字节不变"**：该面板有约 **50 个**"一个控件对一个字段"的绑定，
   逐字段手写断言有两个必然的坑 —— **漏字段**（写到第 30 个就累了，而漏掉的那个正好是将来出问题的
   地方）与**遮差异**（只断言"我关心的字段对"，把"顺手改了别的字段"放过去）。改成
   `ProjectSerializer.Serialize` 在操作前后比对，**任何**字段变化都红（含以后新增的字段），
   失败时按行输出差异、直接点名是哪个字段。配套一条互补用例："改一处 ⇒ 恰好一行 diff"。
2. **抓出真缺陷：硬件型号被静默绑到另一块板。** `FindHardwareTargetItem` 在无匹配时回退到
   **第一项**（原意是"避免 `SelectedItem = null`"），而 `SaveToProject` 会把这个回退结果**持久化** ——
   于是 `HardwareTargetId == null`（或一个**已失效**的旧 Id）时，**打开设置点一下「应用」**
   就被绑到清单第一项（本机 `allwinner-t113`）。而 `null` 在本项目里是**显式支持**的状态：
   生成物 `// Target: generic`、状态栏回落 generic、`ValidateHardwareTarget` 取不到目标就**隐藏校验行**
   （本文件自己写着"无匹配（旧工程保存了失效 id）时返回 null"）。后果四条：产物注释变板子、
   校验对象变成用户从未选过的板、**失效 Id 被换成另一块板**（比"未指定"更糟）、
   无操作的设置访问产生 git diff。修法：无匹配返回 `null`（下拉保持未选中），三处自洽。
3. **同时钉住一条历史缺陷**：`SaveToProject` 的注释记载 *"曾经的实现是无条件 `foreach` 覆盖，
   导致每次打开工程设置点确定都会静默重置用户逐屏手工调过的尺寸"* —— 现在有回归锁。
4. **钉住三条 null/归一语义**：清空可空文本 ⇒ `null`（不是空串）；未选屏 ⇒ `null`（不是第一块屏）；
   `ProjectVersion` / `OutputDirectory` 的 null ⇒ 默认值属**有意归一**（模型上是非空属性，
   保留 null 会让 `ResolveOutputDirectory` 返回 null、导出对话框拿不到默认目录），用测试钉住防"误修"。
5. **两个测试侧的坑**（写进注释省下一次重踩）：这些数字框是 `FluentAvalonia` 的 **`FANumberBox`**
   而非 `NumericUpDown`（`FindControl<NumericUpDown>` 会抛）；比对必须用**同一个工程实例**
   （首版拿"新建的等价工程"当基准，把每次生成都不同的屏幕 `id` / `createdAt` 全算成了差异）。
6. **验收**：全量 7 程序集 **2624 / 0**（App.UiTests 172 → **182**；本轮基线 2614 ⇒ **+10**）；
   负向验证 **4/4 命中**（A 复现本轮缺陷 / B 复现历史缺陷 / C、D 钉住两条 null 语义）；
   `dotnet build` **0 警告 0 错误**。

**范围边界**：`CmbLvglVersion` 的**表外版本**同族问题（登记表 §4 第 18 项）与
`ResolveColorDepthItem` 死代码（第 19 项）**已在阶段 16 闭环**；面板的布局/命中测试仍不在本阶段判据内。

## 阶段 16（登记表 §4 第 18～20 项：配置编辑器与判据同口径）— 2026-09-17

按顺序推进权威登记表 §4「软件侧可验证」里仍开着的第 18～20 项。专题见
`docs/2026-09-17-config-editor-value-normalization.md`。三项看起来互不相干，根因是同一条：
**编辑器的判断与实际判据（下游消费者 / 设备端）不同口径**，且全都表现为"界面看着正常、结果不对"。

1. **#18 表外 LVGL 版本被静默归一**：`LvglVersion` **不是纯展示字段** ——
   `LvglApiCompatChecker` 按 9.x/8.x **分支改写生成代码**、导出器按它填 CMake 分支名与依赖表。
   候选表固定 `8.3…9.5`；工程存了表外版本（更老工程写的 `8.2`、或手改过 JSON）时装载 `SelectedItem`
   变 null、保存又归一成 `"9.5"` ⇒ **打开工程设置点一下「应用/确定」就把 8.x 工程悄悄升成 9.x**，
   生成代码的 API 兼容改写随之改变语义，且没有任何提示。修法：表外版本也进候选
   （`BuildLvglVersionItems`）⇒ 下拉**显示工程里那个版本**、原样往返；只有用户显式选别的版本才变更。
   与阶段 15 的"失效硬件型号 → 未指定而不绑到另一块板"是同一把尺子：**不静默改用户/历史留下的值**。
2. **#19 `ResolveColorDepthItem` 是死代码，而它防的坑是活的**：它的文档写明"回退到第一项而不是留
   `SelectedItem = null` —— 后者会让下一次保存把无效值静默改写成默认值"，但它**从未被调用过**
   （装载走裸赋值），于是实际行为正是它想防的那个：存了无效色深时**下拉显示空白**、
   而保存仍经 `NormalizeColorDepth(null)` 写入一个值 ⇒ 界面与结果不一致。
   **先证明"有没有可观测缺陷"再动手**：`ColorDepth` 的首成员恰好就是 `NormalizeColorDepth(null)`
   返回的 `RGB565`，两条路径落盘值相同 ⇒ 不是"数值被改错"，而是**"界面在撒谎" + 死代码**。
   修法选**把 helper 接上**（它的意图是对的）：死代码变活、文档声明被兑现、下拉不再空白。
3. **#20 口令下限的字符数 vs UTF-8 字节数**：UI 用 `password.Length`、模型注释写"（字符数）"、
   固件用 `strlen`（**字节**）⇒ 下限 8 时"电机启动"4 个字 = 12 字节：**设备端放行、编辑器却报"至少 8 位"**。
   **往字节统一**（与登记表原建议一致），并说明为何**不改设备端**：设备端已有凭据是按字节放行过的，
   改成按字符会把 3 字 CJK 口令（9 字节 ≥ 8）判为过短 ⇒ **现场用户直接登不上**（等于锁门）。
   在"弱一点但文档写清楚"与"可能锁住现有用户"之间选前者。**三处一起改**：判据
   （`Encoding.UTF8.GetByteCount`）、模型注释（`（字符数）` → `（UTF-8 字节数）` + CJK 后果 + 为何不改设备端）、
   用户文案（`至少 {0} 位` → `至少 {0} 个字节（一个汉字算 3 字节）`，zh-CN / en-US 各 2 条）——
   **只改判据不改文案，用户仍会按"位"理解，反而更容易配错**。
4. **验收**：全量 7 程序集 **2628 / 0**（App.UiTests 182 → **186**；本轮基线 2624 ⇒ **+4**）；
   负向验证 **3/3 命中**（A 表外版本不进候选 / B 色深退回裸赋值 / C 口令退回 `password.Length`）；
   `dotnet build` **0 警告 0 错误**。

**范围边界**：把口令下限在**字符**意义上收紧（改设备端按 UTF-8 码点计数）属**产品决策 + 凭据迁移**，
已登记为登记表 §4 第 20b 项；§4 其余仍开着项按顺序在后续轮次处理
（其中 `TimeoutSeconds` 默认值、测试判据绝对余量、POSIX 路径耦合、firmware `const` 警告
**已在阶段 17 闭环或撤销**）。

## 阶段 17（登记表 §4 第 22～25 项）— 2026-09-17

按顺序推进权威登记表 §4「软件侧可验证」里仍开着的第 22～25 项
（第 21 项「用户表校验不覆盖口令强度」是**刻意限制**，跳过）。专题见
`docs/2026-09-17-timeout-flaky-judgment-and-stale-registration.md`。
这一批里**只有两项需要改代码**、且性质完全不同：

1. **#22 真缺陷：默认超时覆盖不住最长的单次生成。** `TimeoutSeconds`（默认 **60**）直接用作
   `HttpClient.Timeout` = **整个响应读完为止**的上限（含流式读完整条 SSE 流）；而 Agent 单次请求
   `MaxTokens = 8192`，按慢速中转/本地模型 **~45 token/s** 估算需 **≈182 s** ⇒
   生成会在**一半左右被掐断**，用户看到"AI 生成失败" —— 与模型质量无关，**换个慢模型/慢中转就复发**。
   而该字段**没有设置界面**（`LlmConfigStore` 不持久化、AI 面板无输入框）⇒ 默认值是唯一杠杆。
   修法：默认 **60 → 240 s**（留约 30% 余量）；判据不是"等于 240"（那种测试只会让人改两处），
   而是**从算式推出下界**（`8192 / 45 ≈ 182`）—— 于是改回 60 会红，并在失败信息里直接给出
   算式与失效模式。
2. **#23 判据脆弱（非产品缺陷）**：原 `InRange(elapsed, 150, 2000)` 的上界是**绝对余量**。
   本沙箱存在全局串行的资源竞争、定时器续体可被推迟到秒级，同族判据已被撞穿几条
   （同文件相邻用例只断言**下界** —— 下界对负载天然安全，上界不是）。
   修法：改成**两次不同窗口的差值**判据：两次测量承受同一份负载，共同项在差值里抵消 ——
   正确实现 ≈ **1000 ms**（窗口差）、空等固定超时 ≈ **0 ms**，于是**不需要**在负载与灵敏度之间折中。
3. **#24 登记有误 → 撤销。** 原登记称 `ConfigCodeGenConsumptionTests` 的 POSIX 字面量
   "在 Windows 上断言会挂"。复核不成立：断言是 `Assert.Equal(Path.Combine(dir, outDir), resolved)`，
   而 SUT 就是 `IsPathRooted(x) ? x : Path.Combine(dir, x)` —— **两侧是同一个表达式**，
   任何平台都恒等；绝对路径那条在 Windows 上 `IsPathRooted("/opt/output")` 同样为 `true`。
   ⇒ 撤销该条（残留只是"夹具只用了 POSIX 形状字面量"的**覆盖** nuance，不是失败风险）。
   **这一条值得单记**：登记表是判断的产物，**判断本身也会写错** ——
   所以"按登记表动手"之前必须先用代码证伪一次。
4. **#25 真警告：`host_main.c` 的 4 条 `-Wdiscarded-qualifiers`。** 该文件不被
   `run_host_tests.sh` 编译，要按它自己的 Makefile 复现（`make objs/host/host_main.o`）⇒ 4 条（`:83-86`）。
   根因：`LV_FONT_DECLARE(x)` 展开为 **`extern const lv_font_t x;`**（内置字体是 const），
   而 `g_font_cn_*` 是**非 const** 的 `lv_font_t*` —— **但不是本文件的疏忽**：这四个全局同时被
   **设备侧** `app/main.c` 定义、被共享头与 `set_ta_font(lv_obj_t*, lv_font_t*)` 使用，
   设备上装的是 FreeType 动态创建的可变字体。全仓 const 化要动 **7 个固件文件（含设备运行时）**
   去换 4 条 **host 工具**警告，不成比例，且漏改会**编不过设备端**。
   修法：只在这 4 处回退赋值上做**显式转换**并写清安全论证（只喂给 LVGL 只读字体 API、从不经其写入），
   重编 **0 警告**、产物正常（22832 B）。
5. **验收**：全量 7 程序集 **2630 / 0**（AI 313 → **315**；本轮基线 2628 ⇒ **+2**）；
   负向验证 **2/2 命中**（#24 无可注入行为、#25 是编译期警告 ⇒ 不产生 NV 组）；
   `dotnet build` **0 警告 0 错误**；`host_main.c` 单文件重编 **0 警告**。

**范围边界**：`g_font_cn_*` 的**全仓 const 正确性**（设备侧定义 + 3 个使用点 + `set_ta_font` 签名）
本轮用局部转换替代，属独立固件重构（需真机回归）；`TimeoutSeconds` **仍无可配置入口**，
给设置界面属产品决策；§4 其余仍开着项（运行时动态文案、en-US 校对、实机相关项）按顺序在后续轮次处理
（其中 Dock 真实拖拽 / `ViewLocator` 旧 Parent **已在阶段 18 部分闭环**）。

## 阶段 18（Dock 真实拖拽：可行性取证 + 回归网）— 2026-09-17

按顺序推进权威登记表 §4 第 26 项。专题见
`docs/2026-09-17-dock-real-drag-feasibility-and-reparent-evidence.md`。
这一项**部分闭环**，过程中**推翻了一条登记假设**、得到一条**反直觉**结论：

1. **推翻"headless 不一定可行"**：先查实现再下结论 —— `Dock.Avalonia 12.1` 的 DnD
   **不走平台 `DragDrop` API**，而是**指针事件**驱动（`OnPointerPressed/Moved/Released` +
   `DockDragContext`，`ToolTabStripItem` 是把手）。于是 headless 的
   `MouseDown/MouseMove(LeftMouseButton)/MouseUp` 就能驱动：实测拖工具箱标签后
   `Toolbox@LeftLowerDock → Toolbox@CenterDocDock`，视图仍有 100+ 可视后代。
   ⇒ **先花半小时探路，比先花一轮纠结"做不做"更省。**
2. **新增回归网** `DockDragRegressionUiTests`（3 条）：真实拖拽后工具箱**仍挂在可视树上且非空**
   （判据看**附着状态 + 可视后代数** —— 归档问题单的现场签名正是 `DETACHED from visual tree`，
   光断言 `Build(...) is not null` 打不到它）+ 12 个面板解析链仍可用 + **判据双向自检**
   （自检可失败性也证过：判据改恒真 ⇒ 负例立刻红）。
3. **自检本身也纠错过一次**：第一版自检要求"拖完必须与别的面板共处一个 dock"，**红了** ——
   实测 Dock 对这次放置的处置是**新建/切分出一个 dock**，不是加进目标标签组；
   于是改成"父 dock 引用变了"。另外第一版只断言"拖完还有内容"，而**一次没被驱动的拖拽也会绿**
   —— 必须补自检。
4. **一条诚实的记录**：把 `ViewLocator.CanResolve` 退回**原始缺陷判据**后，两条拖拽用例
   **照样全绿** ⇒ Dock 12.1 的标签拖拽**不会清空 `Context`**，归档问题单假设 D 的机制在这条
   路径上**不复现**。所以它们是**回归网**而非"在已知缺陷上验证过会红"的判据 ——
   这正是要补判据自检的原因（**"守卫绿了"与"守卫有能力红"是两件事**）。
5. **`ViewLocator.Build` 不 Detach：三条实测**（把登记里那句"未验证项"变成结论）：
   ① 把**仍挂着**的控件加到新父级**会抛**（`already has a visual parent …`，`Button` 与真实
   `ToolboxView` 都验）⇒"谁先摘"是**硬约束**；② 先 `ContentControl.Content = null` 再挂**可行**；
   ③ 真实拖拽路径**没有**撞上① ⇒ Dock 自己完成了转移。⇒ **保持现状**并把①②钉住
   （`ViewLocatorReparentContractTests`）。另一层理由：**万一漏摘，症状是抛异常（响亮），
   不是静默空白** ⇒"加 Detach"并不是消除静默空白的必要手段，真正的风险是"**加** Detach 时
   摘不干净/摘错对象"（登记里"可能换成必崩"的担心方向对、机制要改）。
6. **归档问题单仍标 OPEN（诚实边界）**：原文复现是"拖**项目浏览器**到**工具箱** dock"，
   而项目浏览器的标签条实测只有 **1 px 高**（单面板 dock 不渲染可见标签项）⇒ 驱动的是**镜像方向**，
   不是逐字复现；镜像方向未复现空白 ⇒ 当年症状成因**仍未定论**。已往问题单追加复测记录，
   并登记**未覆盖路线**（文档区 / 浮动窗口 / 多屏重组）。
7. **验收**：全量 7 程序集 **2636 / 0**（App.UiTests 186 → **192**；本轮基线 2630 ⇒ **+6**）；
   负向验证**两组**：A 判据改恒真 ⇒ 命中；**B 退回原始缺陷判据 ⇒ 按预期不命中**，
   并据此得出第 4 条结论（不是失败，是**结论**）。

**范围边界**：归档问题单保持 OPEN，其余拖拽路线（拖到文档区 / 拖出成浮动窗口 / 多屏重组）
未覆盖 —— 已登记为后续候选（Dock 的 `DockDragContext` 应同样可驱动指针路径）。

## 阶段 19（本地化：硬编码中文清单 + 只减不增守卫）— 2026-09-17

按顺序推进权威登记表 §4 第 27 项。专题见
`docs/2026-09-17-localization-hardcoded-inventory.md`。这一项**部分闭环**，
且**又一次撞见失效登记**：

1. **登记里点名的举例早已本地化**：`AnimationEditorView` 的缓动选项（`Lang.AnimEasingLinear` 等 7 项）
   与 `ToolboxView` 的控件显示名（`Lang.TbScreen` 等）**都在用字典键** ⇒ 照登记表动手会白跑。
2. **真实剩余量接近"约 100+"，但分布完全不同**：`.axaml` 只 **14 条 / 2 文件**（本轮已全部本地化），
   而 `.cs` 有 **226 条 / 27 文件** —— 以**日志面板文本**（Device/Download/BuildRun/SimulatorBuild ≈60）、
   **撤销/操作名**、**下拉显示名**（报警类型/级别）、**快捷键提示**为主（均已逐文件登记基线）。
3. **判据的形式决定它看不看得见东西**：第一版扫描器按"这一行是不是控件赋值"（sink 式）只得到
   **98 条**，真实是 **226 条** —— 多行实参（
   `PopulateTaggedCombo(CboAlarmType, new (string,object)[] { ("上限报警 (H)", …) })`）
   的 sink 在**上一行**，按行匹配整片漏掉（`VariableManagerView` 的 27 条全漏）。
   改成**分类式**（取所有含中文的字面量，扣掉明确排除的类别）才准；**失明方向是"漏报"**，
   会让人以为快做完了。
4. **根因才是重点**：登记了这么多轮，**没有任何机制阻止新功能继续写死中文** ⇒ 清单永远追不上。
   本轮补上 `HardcodedLocalizationGuardTests`：**每文件条数与基线相等**（涨了报"新增了硬编码中文"、
   降了报"必须同步改小基线"）+ 总数只减 + **扫描器自检**（文件数/条数下限、注释剥离、排除项正负例）。
   "相等"而非"不超过"是刻意的（照搬 `ConfigFieldInventory` 的教训：允许事实比记录少，
   清单就会慢慢变成过期抄写）。**交付的不是"翻译了多少"，而是把一件永远做不完的事变成只会变少的事。**
5. **本轮补齐的一批：14 条 `.axaml`（首屏 + AI 面板按钮）**，**8 个新键 + 6 处复用既有键**
   （`app.title`、`status.ready`、`gen.cancel`、`ai.image.save.svg/png`），刻意不重复造键；
   **刻意不改用户可见措辞**（Splash 写"新建**工程**"、字典 `project.new` 是"新建**项目**" ——
   保留原文只搬进字典，改了就是未 asked-for 的措辞变更）。`SplashWindow.axaml` 补上 `xmlns:res`。
6. **新发现（产品决策）**：同一概念字典里含「工程」**30** 处 vs 含「项目」**33** 处 ——
   统一会改约 60 处**用户可见措辞**，已登记为登记表 §4 第 27b 项，本轮**刻意未动**。
7. **验收**：全量 7 程序集 **2641 / 0**（App.UiTests 192 → **197**；本轮基线 2636 ⇒ **+5**）；
   负向验证 **3/3 命中**（基线内新增 / 减少但未同步基线 / 新文件出现硬编码）；
   本地化字典 zh-CN / en-US 各 **+8** 键（占位符数量一致，`LocalizationTests` 通过）；
   `dotnet build` **0 警告 0 错误**。

**范围边界**：剩余 **226 条**按文件分批做（守卫已让总量单调）；**en-US 逐字校对**需要母语级判断，
建议随分批一起做；`src/HmiEditor.AI`（含 `LuaCompletionService` 的脚本模板中文）**不在扫描面内** ——
那一层混着提示词与模板，需与 prompt 契约一起评估。

## 阶段 20（本地化第二批：修掉守卫自己的漏报 + 命令目录收口）— 2026-09-17

继续推进权威登记表 §4 第 27 项。专题见 `docs/2026-09-17-localization-hardcoded-inventory.md` §9。

**上一阶段立的守卫自己有缺陷，而且缺陷方向是"漏报"。** 三件事：

1. **判据从"按行"改成"按语句"**：原规则按行匹配排除词，两个方向都错 ——
   （a）`throw new ArgumentOutOfRangeException(` 换行后，参数里的中文被算成待本地化
   （与"异常消息不翻译"的判据自相矛盾）；（b）**更严重**：`Log($"[提示] {AppLocalization.Service[…] }")`
   这类日志行，正文入了字典、**段落标签 `[提示]` 是写死的中文**，却因整行含 `AppLocalization`
   被整片豁免 ⇒ **13 条用户可见文案被藏起来了**（`DeviceManagerView` 6 + `DownloadToHmiView` 7；
   `LogBox` 是设备面板上可见的 `ListBox`，不是调试输出）——英文界面下这些日志**中英混排**。
   新判据按"包住字面量的那个调用"判定（不跨 `;`/`{`/`}`/`(`），那 13 条标签本批入字典
   （`Lang.DeviceLogTag*`）。
2. **`EditorCommandCatalog` 38 条 → 0**：其中 **26 条根本不是 UI 文案** —— 25 条是 `Bindings[]`
   的手写 `Description`（唯一消费者是 `FindConflicts()`，而它唯一被一个测试消费），1 条是
   `FindConflicts` 的消息模板。`Description` 与 `Key`+`Modifiers` **完全重复**，是手工维护的
   第二份事实 ⇒ 改了键位忘了改文案，这个"冲突诊断"就会**静默说谎**，而它恰恰是用来判断
   "到底哪里撞了"的。处置**不是翻译而是删掉抄本**：新增由 `Key`/`Modifiers` 推导的 `Chord`
   （`Ctrl+OemPlus`/`Esc` 等枚举名，可搜索且不会被本地化），冲突消息改成纯 ASCII 开发者诊断；
   剩下 **11 条 `Describe(command)` 是用户可见的**（`MainWindow` 用它拼撤销/重做菜单项：
   `Loc["project.undo"] + " " + UndoDescription`）⇒ 按**原措辞**入字典（`cmd.desc.*`）。
3. **`FindConflicts` 原来没有能力变红**：它只读静态 `Bindings`，而表里本来就没有冲突 ⇒
   `Bindings_HaveNoConflicts` 在分组逻辑写坏时**照样绿**。抽成可注入的
   `internal FindConflicts(IEnumerable<ShortcutBinding>)` + 人造冲突用例；负向验证里去掉了
   `.Where(g => g.Count() > 1)` 后**新旧用例同时报红**，才说明它有牙齿。
4. **新守卫 `LangKeyCoverageTests`**（3 条）：补上"`Lang` 常量 ↔ 字典键闭合"这个此前没人看的洞 ——
   `LocalizationService` 找不到键时**回退成键名本身**，"忘了加一条"既不抛异常也不编译失败，
   界面上只是安静地显示 `cmd.desc.align.left`；而既有 `LocalizationTests` 只保证两本字典
   **彼此**键集一致，**两本一起漏掉同一个键时 parity 依然绿**。反射枚举 **1782 个**常量逐个验证
   （另有扫描器自检 ≥1000 与"键值唯一"用例）。

**基线 226 → 187**（26 个文件），每一笔都写在专题 §9.2 的表里。其中 `TemplateManager` **1 → 4**
是**量具修正、不是新增代码**：3 个硬编码控件名原来被"整行含 `AppLocalization`"连坐豁免。
`HmiControl.Name` 在项目树/属性面板**用户可见**故如实计入，但"控件名要不要跟着 UI 语言翻译"
是产品决策（翻译会让**生成物随 UI 语言变化**）⇒ 已登记为 §4 第 **27c** 项，本批刻意未动。

**负向验证 5/5 全部命中指名用例**，且每组先确认"测试确实跑起来了"（非编译失败），
还原后按 sha256 校验与注入前**逐字节一致**。NV-E（把 `IsExcluded` 还原成"按行"）顺带证明
**基线数字是绑在判据上的**：判据一改回旧行为，记录的条数立刻不匹配。

> **本批最该记住的一条**：守卫绿了 ≠ 守卫有能力红。上一阶段交付的守卫本身就在漏报，
> 而且漏的是**用户可见**的文案 —— 一个"总是绿"的守卫比没有守卫更危险。

## 阶段 21（`ConfigFieldInventory` 悬停说明入字典 + 门禁改为"警告即失败"）— 2026-09-17

继续推进登记表 §4 第 27 项第三批。专题见 `docs/2026-09-17-localization-hardcoded-inventory.md` §10。

**先查消费点再动手**（复用阶段 20 的教训）：`ConfigFieldInventory.NotWired[].Note`
→ `NotYetEffective.Mark(control, reason)` → `ToolTip.SetTip(…)`，即**灰显控件的悬停提示**
（"尚未生效：命令栏显隐没有任何消费点……"）—— 用户可见，30 条必须入字典。

1. **键推导而非手写**：删掉 `string Note` 实参，新增 `NoteKey => $"cfg.note.{Key}"`
   （`Key` 已是 `类型名.字段名`）。手写文案或手写键都会多出一份"与 `Type`/`Field` 并行维护"的事实 ——
   复制条目时最容易忘记改的就是它，改错了（说明与字段对不上）**没有任何东西会报**。
2. **顺带修掉一个潜在缺陷**：清单是 `static` 字段、静态初始化只跑一次 ——
   若在清单里就 `AppLocalization.Service[…]` 解析成字符串，语言会**冻结在首次访问那一刻**，
   之后切换语言悬停提示不跟着变。解析改到显示时；并由新守卫
   `TooltipReasons_ArePassedAsKeys` 扫两个面板的源码，钉住"实参必须是 `entry.NoteKey`"。
3. **新守卫 2 条**：`EveryEntry_HasATooltipReasonInBothLanguages`（每个 `NoteKey` 在两本字典里
   都必须解析出**非键名**的值；索引器找不到键会回退成键名，正好用来判定"字典里没有"；
   带"清单 ≥25 条"自检）+ `TooltipReasons_ArePassedAsKeys`。
4. **基线 187 → 157**（25 个文件）。

**并且更正了本会话自己的一处错误**：阶段 20 为日志标签加了个辅助方法 `Tag(string)`，
它**隐藏了继承来的 `Control.Tag`**（CS0108）—— 而阶段 20 的验收写着"0 警告 0 错误"，
那个读数**不可信**：用的是**增量构建**，那两个文件此前已被定向测试编译过，
构建是 no-op，**警告不会被重新打印**。更正三件事：

| # | 更正 |
|---|---|
| 1 | 辅助方法改名 `LogTag`（撞框架属性还会误导读者以为它在动 `Control.Tag`） |
| 2 | **全量非增量构建**实测 0 警告：`dotnet build HmiEditor.slnx -c Release --no-incremental` |
| 3 | **判据补进门禁**（不只靠人眼）：构建步加 `--no-incremental`（否则可能是 no-op），且输出出现 `: warning <码>` **即判红**并打印去重后的警告清单 |

门禁修复的负向验证：把 `Tag(string)` 加回去 ⇒ **exit 1** 且指名
`warning CS0108: "DeviceManagerView.Tag(string)" 隐藏继承的成员 "Control.Tag"`；
还原后逐字节一致（`cmp`）。**顺带纠错一次**：第一版注入写成了语法错误（两行方法签名挤在一行），
门禁因 `error CS1525` 而红 —— 那不是"警告判红"的证据，重做后才是。

> **教训**：`dotnet build` 是增量的，"这次没报"与"没有警告"是两件事。
> 一个只在**全量构建**时才出现的读数，和只在**空队列**时才成立的断言一样，
> 都不能当验收证据 —— 判据必须自己保证重跑时真的重算过。

## 阶段 22（`UiOperationGuard` 操作名入字典：一处横切缺陷）— 2026-09-17

继续推进登记表 §4 第 27 项第四批。专题见
`docs/2026-09-17-localization-hardcoded-inventory.md` §11。按 §8.1 的顺序本该继续做设备日志正文，
但**先撞见一处横切缺陷**，先修它能让后面的批次更干净。

**缺陷**：`UiOperationGuard.Run/RunAsync` 的第一个实参（操作名）有两处去处 ——
`AppLog.Error($"{operation}失败", ex)`（落盘）与 `report($"{operation}失败: {ex.Message}")`
（视图状态行或**全局状态栏**）。也就是说英文界面下失败提示是
`"选择文件失败: Access denied"`。而 **12 个调用点传的全是中文字面量**：
重命名画面 / 删除画面 / 重命名控件 / 删除模板 / AI 生成脚本 / 删除用户 / 选择文件 / 选择目录 /
生成图标 / 选择 HMI 程序 / 编辑表格单元格 / 重试 Agent 任务。

1. **参数语义改成键**：`operation` → `operationKey`；3 条模板入字典
   （`ui.op.failed` / `ui.op.failed.detail` / `ui.op.report.failed`）；12 个调用点改传 `Lang.UiOp*`。
2. **两条新守卫**（只改调用点不够 —— 下一个调用点照样能传字面量，没有编译期约束）：
   `EveryCallSite_PassesALocalizationKey`（扫全部 App 源码，第一个实参必须以 `Lang.` 开头，
   带"≥10 处调用"自检）+ `RunAsync_ReportsTheLocalizedFailureMessage`（真失败一次，断言报出的
   文案等于"字典里的操作名 + 字典里的模板"且**不含键名**）；另加
   `RunAsync_Cancellation_IsNotReportedAsFailure` 钉住既有语义（取消 ≠ 失败）。
3. **顺带修掉守卫自己的一个"写死阈值"**：本批把总量降到 142 时**自检反而报红** ——
   `Scanner_ActuallyFindsHardcodedChinese` 写着"条数 ≥ **150**"。这正是写死阈值的典型结局：
   每本地化一批就得改它，而**改阈值这件事本身没人会注意到**。改成与基线挂钩
   （`>= Baseline.Values.Sum() / 2`，文件数同理），阈值随真实进度一起下走。
4. **基线 157 → 142**（24 → **18** 个文件）。

> **这一步的价值不在 15 条文案**，而在于它把一个"谁都可以继续写死中文"的横切入口，
> 变成了**有源码契约看守的键参数**。

## 阶段 23（设备日志面板正文 43 条清零 + `LogTagged` 实参个数判据）— 2026-09-17

继续推进登记表 §4 第 27 项第五批。专题见
`docs/2026-09-17-localization-hardcoded-inventory.md` §12。

第四批把横切的操作名清掉后，这一批**只剩日志正文**，两个文件的「连接」块逐行重复，一起做最省：

| 文件 | 之前 | 现在 |
|---|---|---|
| `Views/DeviceManagerView.axaml.cs` | 27 | **0** |
| `Views/DownloadToHmiView.axaml.cs` | 16 | **0** |

新增 28 个键（含段落标签 `device.log.tag.deploy` / `device.log.tag.fel`）+ 28 个 `Lang` 常量；
`[部署] 已自动检测到预编译 HMI 程序: {candidate}` **复用**既有 `download.so.auto.found`
（同一句话不造第二个键）。两个视图各加 `LogTagged(tagKey, messageKey, args…)`，
让"段落标签 + 正文"都只能从字典来；`LogError` 的段落名也从字面量改成 `Lang.DeviceLogTag*` 键。

**顺带补掉新辅助方法的静默失败模式**：`LocalizationService.Format` 在"占位符个数 ≠ 实参个数"时
**不抛异常**，只原样返回未格式化文本 + 一句 `Debug.WriteLine`，而 `Debug` 在 **Release 下被编译掉**
⇒ 用户会在日志面板里看到 `已刷新串口列表 ({0} 个)`。编译器看不见、运行时不抛、只有人盯着才可能发现。
新增 `LocalizedLogCallSiteTests`：扫全部 App 源码，把 `LogTagged(...)` 的实参按**顶层逗号**切开，
断言 `实参个数 - 2 == 字典里该键的占位符个数`，带"≥15 处调用被校验"的自检。

**两处自我纠错**：

| # | 事 |
|---|---|
| 1 | **为判据写的判据当场抓到自己的错**：`SplitTopLevelArguments` 第一版把字符串实参整个丢掉（`inString` 分支里 `continue`，没把字符抄进缓冲）⇒ `F(x, y), "a,b", C.D` 只剩 `F(x, y), C.D`；为切分器写的那条单元测试先于主判据报红 |
| 2 | **上一阶段刚加的"警告即失败"当场抓到我**：`LogTagged` 签名写成 `params object?[]` 而 `Format` 是 `params object[]` ⇒ `CS8620`，在 `--no-incremental` 那次构建里立刻现形 ⇒ 改成 `params object[]` |

> 第 2 条正是上一阶段那条门禁改动的用处：没有它，这条警告会像 CS0108 一样躲在增量构建后面，
> 而文档里又会多一句没验证过的"0 警告"。

**基线 142 → 99**（18 → **16** 个文件）。按 §8.1 的顺序，下一批是 `VariableManagerView` 的下拉显示名（27）。

## 阶段 24（`VariableManagerView` 显示名入字典：先分类再动手）— 2026-09-17

继续推进登记表 §4 第 27 项第六批。专题见
`docs/2026-09-17-localization-hardcoded-inventory.md` §13。

**27 条里混了两类东西，先分类再动手**（这一步比翻译本身重要）：

| 类别 | 条数 | 处置 |
|---|---|---|
| **纯显示**：报警类型/级别下拉标签、报警列表短类型名、"未分组"虚拟分组标签、从站显示名、"已停用"后缀 | 25 | ✅ 入字典（新增 23 键；"未分组"复用既有 `var.picker.ungrouped`） |
| **落盘的数据标识**：`Name = $"报警{n}"`、`_selectedAlarm.Name = … "未命名报警"` | 2 | ❌ **刻意不翻译** |

那 2 条的理由：`VariableAlarmConfig.Name` 挂在持久化的 `Variable.Alarms` 上（模型注释就写着
"报警名称（如"温度过高""压力下限"）"）⇒ 翻译它会让**工程文件内容随 UI 语言变化**
（中文界面新建存"报警1"、英文界面存别的，同一工程里两种语言的名字混在一起）。
与阶段 20 发现的模板控件名是同一回事 ⇒ 并到 §4 第 27c 项（该条改名为"**落盘标识符**要不要
跟着 UI 语言翻译"），并**如实把它们留在基线里**（`VariableManagerView` 27 → **2**），
基线上写注释说明**为什么是 2 而不是 0** —— 不假装清干净。

顺带把"虚拟未分组分组"的两处字面量一起清掉：让标签与 `Name` 同源（那个对象 `Id` 为空、
不落盘，名字纯粹显示用）。

**新守卫：把"键 ↔ 枚举"的配对钉住。** 本地化把"手写文案"换成"键"时最容易**抄串**
（`HighLimit` 配上"下限报警"），而写串了**没有任何东西会报**：下拉安静地显示错的类型名，
Tag 仍正确，连保存的数据都是对的、事后翻数据也看不出来。于是把配对提成 `internal` 的两个表
（标签在这一层解析），配 `AlarmOptionLocalizationTests` 六条：枚举**一一对应**
（枚举新增成员却忘加下拉项 ⇒ 用户选不到）、**语义配对**、**标签确实由字典解析**、
标签两语言可解析且组内互不相同。

> **其中"标签确实由字典解析"是负向验证逼出来的**：第一版守卫只断言配对表，于是把
> `(AppLocalization.Service[o.LabelKey], …)` 改成 `(o.LabelKey, …)`（**把键直接当标签用**，
> 用户会在下拉里看到 `var.alarm.type.high`）**四条用例全绿** —— 负向验证当场顶出这条盲区，
> 才补上那条断言。又一次：**守卫绿了 ≠ 守卫有能力红**。

**基线 99 → 74**（16 个文件）。

## 阶段 25（`MainWindow` 22 条入字典 + 失败模板收成唯一实现）— 2026-09-17

继续推进登记表 §4 第 27 项第七批。专题见
`docs/2026-09-17-localization-hardcoded-inventory.md` §14。

**先撞见的又是一处重复实现**：`MainWindow.ReportOperationFailure(string operation, Exception ex)`
里写着与 `UiOperationGuard` **逐字相同**的两条模板 —— `$"{operation}失败"` /
`$"{operation}失败: {ex.Message}"`，而上一阶段刚给后者做完本地化。**同一套措辞、两份实现**，
本地化一处漏另一处几乎是必然。所以顺手把模板收成唯一实现：

| 之前 | 现在 |
|---|---|
| 两处各写一份 `"{操作}失败"` / `"{操作}失败: {原因}"` | `UiOperationGuard.FailureSummary(operationKey)` / `FailureDetail(operationKey, reason)`，**两处都调它** |
| `ReportOperationFailure(string operation, …)`（收文案） | 收**键**，9 个调用点改传 `Lang.UiOp*` |

顺带消掉一条重复措辞：`StartWithOpenFileCore` 的 `$"打开工程失败: {ex.Message}"` 现在直接是
`FailureDetail(Lang.UiOpOpenProject, ex.Message)` —— **措辞逐字不变**，也不需要新键。

**22 条入字典**：8 个操作键（打开/新建/保存工程、导出代码、编译工程、下载到 HMI、
导出模拟器工程、Dock 事件路由诊断）+ `status.file.not.found` + `status.theme.switched`
+ 8 个**撤销描述**键（删除控件/删除 {0} 个控件/旋转 {0:0}°/水平镜像/垂直镜像/重置变换/
修改控件 {0}/修改页面 {0}，撤销菜单项上直接可见）。撤消描述沿用字典既有的 `undo.` 命名。

**留下 2 条**：`HmiResourceItem` 的 `SubCategory`/`Description`（`"AI 生成"` /
`"AI 生成的矢量资源"`，`_resourceLibraryStore.Save` 会**存进资源库文件**）⇒ 与 §13.2 的报警名、
§9.5 的模板控件名同类（落盘内容不该随 UI 语言变化），并入 **§4 第 27c 项**
（该条改名为"**落盘内容**要不要跟着 UI 语言翻译"），如实留在基线里
（`MainWindow` 24 → **2**，注释写明为什么是 2）。**更彻底的解法**（存稳定 token、显示时映射）
需迁移既有文件，属独立议题，已写进登记项。

**契约守卫扩到第二个入口**：`EveryCallSite_PassesALocalizationKey` 原本只扫
`UiOperationGuard.Run/RunAsync`，本批把 `ReportOperationFailure` 也纳入（正则加分支），
于是"下一个调用点照样能传中文字面量"这个洞在两个入口上都被堵住；顺带排除"方法声明里的形参"，
自检从"≥10 处"提到"≥20 处传了 `Lang.*` 键的调用"。另加
`FailureFormatters_UseTheSharedTemplatesAndResolveTheOperationName`，钉住这两条共享模板
**用的是哪两条键**（有人换模板或漏传原因参数 ⇒ 两处措辞再次分叉 ⇒ 红）。

**基线 74 → 52**（16 个文件）。

## 阶段 26（一批小文件 11 条 + 诊断 sink 规则漏网 + 主题说明接上界面）— 2026-09-17

继续推进登记表 §4 第 27 项第八批。专题见
`docs/2026-09-17-localization-hardcoded-inventory.md` §15。

**① 规则漏网（假阳性）**：`PreviewRuntimeService` 的诊断出口默认写 `Debug.WriteLine`，
而 `MainWindow` **从不注入它** ⇒ 那条中文消息用户看不到；但排除规则**按行**看关键词，
而 sink 写在**属性初始化器**里，调用点那行只有 `Log?.Invoke(...)` ⇒ 被判成"待本地化"。
处置不是给诊断文案建字典键（没人看得到），而是：`Log` → **`DiagnosticLog`**（让 sink 的名字
**自己声明类别**，与 `Debug.`/`Console.`/`Trace.` 同类）+ 排除正则加 `DiagnosticLog`。
这与阶段 20 的多行 `throw new` 是**同一类**（判据行局部、事实跨行），但修法不同：
那边让判据跨行，这边让**代码自己说清楚** —— 改名比加规则稳，因为名字错了编译不过，
规则漏了没人知道。

**② 一处死数据接上界面**：`ThemeInfo.Description` 的**唯一**消费者是一条
`Assert.False(IsNullOrWhiteSpace(...))` —— 设置对话框只有三个**只显示名字**的主题按钮，
应用里没人读它。按阶段 21 的教训本该删抄本，但这份数据本身有用（不是重复抄本）
⇒ 选**接上界面**（与登记表第 19 项把 `ResolveColorDepthItem` 死代码接上同一处置）：
`Description` → **`DescriptionKey`**（静态表里只许放键 —— 放解析结果会**冻结语言**，
与 `ConfigFieldInventory` 同一个坑）、新增 `AppThemeService.DescribeTheme(key)` **显示时**解析、
挂成三个主题按钮的**悬停提示**；Core 测试从"断言有一串非空文本"改成**断言键能解析**
（`DescribeTheme_ResolvesTheKey`：`文案 != 键` 且 `== 字典值`）。

**③ 其余 11 条**：撤销描述 3、从站显示名 1、画布占位字 1（`DefaultTexts.ImagePlaceholder`
由 `const` 改成**属性** —— const 编译期内联、运行时无法解析，该文件注释早就写着"如需多语言，
后续逐个替换为 AppLocalization 资源 key"，本轮兑现）、主题说明 3、AI 图标文件选择器类型名 2。
`DefaultTexts` 里纯符号性的 `"100%"`/`"?"`/`"Tab1"` 仍留 `const`（不进字典）。

**留下 1 条**：`AiIconDialog` 的 `Name = $"AI图片_{w}x{h}"` 会随资源条目**落盘** ⇒ 归第 27c 项。

**基线 52 → 40**（16 → **9** 个文件；剩下 40 条里 **9 条**是第 27c 项的落盘数据）。

## 阶段 27（最后 31 条清完：扫描面只剩"落盘数据"）— 2026-09-17

继续推进登记表 §4 第 27 项第九批。专题见
`docs/2026-09-17-localization-hardcoded-inventory.md` §16。

**31 条的去向**：`SimulatorBuildService` 8、`AiChatPanelView` 7、`Wiring.CanvasTools` 6、
`BuildRunDialog` 6、`DesignCanvas.Editing` 4。**复用优先**：**12 条（39%）直接命中既有键** ——
`Wiring.CanvasTools` 的 5 条撤销描述（`统一控件尺寸`/`水平等距排列`/`垂直等距排列`/`组合控件`/`拆分组合`）
与 `EditorCommandCatalog.Describe` 的文案**逐字相同**，造第二份键就又多一处会漂移的抄本；
`AiChatPanelView` 的 4 条同理（保存对话框标题与文件类型名与 `AiIconDialog` 是同一句话）。

**又一个判据漏网（反方向）**：`ErrorLineRegex` 是**正则模式**（识别编译器输出里的错误行），
含中文只是因为它同时要认中文输出 —— **翻译它会直接改变匹配行为**（英文编译器输出就再也认不出来），
而守卫把"含中文的字面量"一律算成待本地化 ⇒ 新增排除项 **`Regex`**。
这与阶段 26 的 `DiagnosticLog`、阶段 20 的多行 `throw new` 是同一族：
**判据看"这一行长什么样"，而字符串的类别由它在代码里的角色决定**；
三次修法各不相同（让判据跨行 / 让名字自己声明 / 认出承载它的类型）。

**顺手修掉一个"本来就是歪的"日志框**：`BuildRunDialog` 的取消框内容行实测 **47 列**、
上下边框只有 **44 列**（中文是双列字符、padding 却是手敲空格）⇒ 右边框一直多出 3 列，
而且文案一改继续歪、没人会发现。改成按**显示列宽**补齐（`DisplayWidth`：CJK 2 列、其余 1 列；
`BoxedContentLine`/`AppendBoxedLog`），配 `BuildRunDialogBoxTests` **6 条**：
框内行宽 == 边框宽（中文/英文/中英混排/空串）、列宽语义、**两语言的实际文案都装得进框**。
负向验证把列宽换成 `text.Length` ⇒ 中文那条立刻红，证明"按列宽算"是判据能红的原因。

**新基线 40 → 9（4 个文件）—— 全部是 §4 第 27c 项的落盘数据**（模板控件名 4、资源库元数据 2、
报警名默认值 2、AI 图片资源名 1）。**扫描面上已经没有"该翻译却没翻译"的界面文案了**；
剩 9 条等"落盘内容要不要跟着语言走"的产品决策（含"存稳定 token、显示时映射"的迁移方案）。
守卫继续盯着：这 9 条加一条、改一处都会红。

## 阶段 28（第 27c 项定性被推翻：落盘默认名跟随 UI 语言，扫描面清零）— 2026-09-17

推进登记表 §4 第 27c 项。专题：`docs/2026-09-17-persisted-content-language-decision.md`
与 `docs/2026-09-17-localization-hardcoded-inventory.md` §17。

**定性被推翻**：原登记把它写成"**产品决策**：翻译会让落盘内容随 UI 语言变化，等决策"。
复核后发现前提不成立 —— **App 里已经有 9 处**把"新建实体的默认名"走字典
（`ScriptNewDefault`、`TrendDefaultName`、`AnimDefaultName`、`comm.conn.new`、`comm.slave.new`、
`FaceplateEditorNewTemplate`、`FaceplateEditorParamDefaultName`、`SimVarName`…）。
**既有约定就是"新建实体的默认名跟随 UI 语言"**，那 9 条（报警默认名、模板控件名、
AI 资源名/分类/描述）只是**没跟上** ⇒ 补齐，撤销"产品决策"的定性。

**原理**（三件事分开看就不矛盾）：界面文案每次渲染取字典（切语言立即变）；
新建实体的默认名在**创建那一刻**取当前语言写进模型（之后不变）；已落盘内容切语言**不动它**
（用户数据）。所以"中文界面新建的脚本叫「脚本1」，切英文后仍叫「脚本1」"是**正确**行为。

**动手前查过三件安全事项（都通过）**：① 全仓没有 `Name == "…"` 形式的按名字查找；
② 控件名**不是**生成代码的标识符（C 标识符一律来自 Id：`CIdentifier.ScreenFactory(id)`、
`"script_" + SanitizeIdentifierPublic(script.Id)`、`TAG_{Sanitize(variableId)}`；
`control.Name` 只进注释与 `Text` 兜底）；③ `SubCategory` **不是**分组/比较键
（只有一处 `Text = item.SubCategory` 显示，搜索走 `Tags`，而预设 Tags 本来就中英并列）。
⇒ 本地化这 9 条不会打断任何查找、也不影响生成物结构。**刻意不做** token + 显示映射，
理由与将来重开讨论的触发条件都写在决策文档里。

**顺带又抓出 3 条被藏起来的用户可见文案**：基线清零后**排除类别成了唯一的过滤器**，
于是把 22 条被排除的字面量逐条审了一遍，两处判据边界因此修正 ——
① 语句头也以 `,` 为界（**兄弟属性不再连坐**：`new ChatEntry { RoleDisplay = Loc[…], Content = "中文" }`
里的 `Content` 曾继承兄弟的豁免）⇒ 抓出 2 条聊天文案；② 本地化调用的**非首实参**若是字面量
**不豁免** ⇒ 抓出 `Format("ai.gen.failed", "PNG 渲染失败")` 里的状态文案。
第 ② 条只对**本地化调用**生效（一刀切会让多行 `throw new X(nameof(a), a, "中文")` 重新计入 ——
实测踩到，已用 `LocalizationCall` 收窄）。两条都配了单元测试。

**（诚实记录）**：第一次写的审计脚本**漏了 `;{}` 边界**，把两条聊天文案误报成"已被本地化豁免"，
差点据此收工；用**保真**重写复核后才发现守卫本身还有口子 —— **审计工具的判据必须和被测对象
用同一套判据**，否则审计本身就是假证据。

**守卫据清零重写**：不再是"逐文件基线 + 相等判定"（清零后没有信息量，基线字典已删），改为
① `NoUnclassifiedHardcodedChinese`（扫描面必须为 0，报错列出文件与条数）；
② 自检换成 `Scanner_StillWorks_WhenExclusionsAreDisabled`（关掉排除仍能扫到 ≥15 条）——
清零后"扫到多少条"不再是进度表，自检必须**独立于基线**。

**新基线：扫描面 = 0**（App 层 22 条中文字面量全部落在 4 个排除类别里：
`Debug.WriteLine` 15、异常消息 5、诊断 sink 1、正则模式 1）。

## 阶段 29（版本标记四者对应 + 修掉一处 LVGL 目标不兼容）— 2026-09-17

推进登记表 §4 第 29 项（`Phase 0/5` 清单）。专题见
`docs/2026-09-17-version-marker-correspondence.md`。

**四个标记都在，但"对应"只是注释里的约稿。** `hmi_runtime.h` 里明写着
"与 `docs/version-info.md` 同步"、"与 `AllwinnerT113Target.Metadata.Id` 对齐" ——
两处当时都恰好一致，靠的是人记得；`Phase 0` 要的"四者对应关系"没有任何东西去比。
本轮把四组对应变成**可执行判据**（全部文本解析，不需交叉编译）：

| 判据 | 对应关系 |
|---|---|
| 版本串 == "MAJOR.MINOR.PATCH" | 同一头文件内自洽（bump 数字忘改串的经典漂移） |
| `firmware/t113/docs/version-info.md` == 该串 | **文档 ↔ 代码** |
| `HMI_TARGET_ID` == `AllwinnerT113Target.Metadata.Id` | **固件 ↔ 编辑器目标插件** |
| `hmi_ui_api.h` 的 ABI/LVGL == 目标插件声明 | **固件 ABI ↔ 编辑器** |
| 固件要求的 LVGL 版本 ∈ 编辑器版本清单 | **固件 ↔ 工程设置** |

**顺带修掉一处真不兼容。** 编辑器允许把工程 LVGL 版本选成 **8.3**，而 T113 运行时是 **9.5**：
`LvglApiCompatChecker` **只在工程为 9.x 时**才把 v8 老 API 改写成 v9 写法，选 8.3 就**跳过改写**
⇒ 生成的 C 里留着 9.x 已改名/移除的符号，**板上编译不过**；而生成的 `.so` 仍写
`lvgl_version = HMI_UI_LVGL_VERSION`（9.5）⇒ 运行时**会接受**它，**声明与实际不符**。
处置：把"板子运行时自带的 LVGL 版本 / ABI"写进硬件目标契约（`LvglConfig` 两个**可省略**字段，
省略即"未声明"）+ 契约层共享规则 `TargetValidation.CheckProjectLvglVersion`，两块内置目标都在
`ValidateProject` 里调用，主版本不一致报 **Error**（"确定不能用"，不是"可能有问题"）。
**RK3506 刻意留空** —— 仓库里没有该板固件可对照，**填一个没验证过的版本号比留空更糟**；
未声明的目标自动跳过判定。

顺带把"编辑器认得的 LVGL 版本清单"从 `ProjectSettingsView` 的**局部数组**收进
`LvglApiCompatChecker.SupportedLvglVersions`（版本兼容知识所在层），并让 App 侧显示校验结果时
**先按 `hw.issue.<code>` 查字典**、查不到再回落插件原文 —— 新规则因此有中英文案
（**新发现**：插件程序集里的用户可见文案不在本地化守卫扫描面内，已登记为 §4 第 27d 项）。

**复核后勾掉**：Phase 5 的"CI 增加 firmware host tests 与模拟器导出验证步骤"早已落地
（`ci-gate.sh` 第 4 步 + `verify.sh`）。

**一条诚实记录**：全量并行那一次跑出 **1 个失败**（Core 1914/1915），但**没抓到用例名**；
随后 `Core.Tests` 单跑 **两次全绿（1915/1915）**、`--filter GitWorkflowServiceTests` **8/8 绿（94 ms）**
⇒ 与**阻断项 11**（`GitWorkflowServiceTests` 的 load-sensitive flaky）签名一致，
且门禁本来就把这 8 个用例**单独跑**（第 3 步后半）以规避它。

## 阶段 30（登记表 §4 第 29 项剩余三项：一键出包 / 资源预算实测 / 用户文档）— 2026-09-18

按顺序推进登记表 §4 第 29 项此前登记为"仍开着"的三个子项。专题见
`docs/2026-09-18-release-packaging.md`、`docs/2026-09-18-resource-budget-measurement.md`、
`docs/user-guide.md`、`docs/troubleshooting.md`。

**① 编辑器侧一键出包**：`bash scripts/package-release.sh`（真实逻辑 `tools/ReleasePackager`，
可单测）＝ `dotnet publish` → 随包固件文档 / `UPGRADE.md` / `manifest.json`（版本清单 + 逐文件
sha256）/ `SHA256SUMS` → tar.gz；`--verify <包目录>` 用**同一套校验**复核。两处判断写进理由：
发布用 RID（可移植发布 **574 MB** vs `linux-x64` **35 MB**）；包内版本清单**接到阶段 29 的四组对应**
（固件侧读 `hmi_runtime.h`/`hmi_ui_api.h`、编辑器侧读 `AllwinnerT113Target`，经同一个
`FirmwareVersionMarkers`）。实测：96 个载荷 / 34889 KB / tar.gz 生成；篡改包内一个文件后
`--verify` 退出码 **1** 并点名 `docs/version-info.md`（3 项）。判据 `ReleasePackageTests` 7 条
（含 3 条可失败性负例）。

**② 资源预算"实测 vs 预算"——第一次比对就推翻声明值**：T113 的 `FlashSizeBytes` 原为
**16 MiB**，而板上是 **W25N01GV（1 Gbit = 128 MiB）**；实测整包 **54.8 MiB**
（`Tina-SDK/out/t113-mq_r/tina_t113-mq_r_uart1.img`，占 128 MiB 的 42.8%）、分区表固定分区
**90.9 MiB** ⇒ UDISK 剩余 **29.1 MiB**、`libhmi_ui.so` 21.6 KiB。三条独立证据（BOM 型号 /
真实整包体积 / 分区表）写进 `AllwinnerT113Target` 注释，同族错值（`firmware/t113/README.md`、
`version-info.md`）一并改正。**更早一步的真缺陷**：仪表盘 `UpdateResourceDashboard` **根本没用
目标预算**，而是写死 `8MB Flash / 64MB RAM`（注释还写着"可从 HardwareTarget 获取实际值"），
T113 占用率因此显示成真值 **8 倍** ⇒ 改为取 `IHardwareTarget.Resources`，判读收敛到
`ResourceBudget`（70% / 90% / >100% / 未声明=Unknown，阈值即原仪表盘配色的两个数），
目标缺失时不显示百分比。判据：BOM/README 容量文本 ↔ 常量、阈值语义 7 组、未声明≠通过、
分区表解析 4 条、仪表盘不得写回硬编码分母；真实读数 `bash scripts/resource-budget.sh`。
**负向验证 6 组全部命中**（资源预算 5 组：改常量 / 改文档容量文本 / 改阈值 / 写回硬编码分母 /
去掉剩余钳位；文档守卫 1 组：把文档里的脚本名换成不存在的）。

**③ 用户文档**：`docs/user-guide.md`（348 行）+ `docs/troubleshooting.md`（197 行），面向现场
工程师、每条关键说法标注事实源；固件烧录/升级/回滚只引用 `firmware/t113/docs/*`，不发明步骤。
守卫 `UserGuideDocTests`：仓库路径存在性（含单层通配）、markdown 相对链接、`bash scripts/*.sh`
存在且**有可执行位**、快捷键必须在 App 源码里找得到，外加"扫到条数下限"自检。
**顺带修掉两处 stale 文档**：README 与 troubleshooting 把 `ci-gate.sh` 的 `/dev/fd/63` 当成
**现行**问题（讲解已废弃的 `mapfile` 变通），而脚本早已改成 `mktemp` + 非空校验；
troubleshooting 的"还没有一键出发布包脚本"同步改正。

**门禁**：`ci-gate.sh` 新增第 [2b] 步 —— 构建两个新工具（它们**不在 slnx 里**，不构建就会
静默腐烂）并跑 `ReleasePackager --self-check`（临时目录装配最小包 + 全套校验，秒级）。

**门禁顺带抓出的测试竞态（已修）**：第一次全量并行时 `App.UiTests` 红 1 条 ——
`UiOperationGuardContractTests` 前后两次读同一字典键，拿到 "Save project失败" 与
"保存工程失败" **两种语言**的结果。根因不是产品缺陷，而是 `LocalizationRefreshUiTests`
会切换 `AppLocalization.Service` 的**全局当前语言**，而它没有独占运行 ⇒ 与并发读字典的用例
共享了可变全局状态。处置与既有 `AppDataRoot-global` 同一解法：新增
`Language-global`（`DisableParallelization = true`）集合并把该类放进去；随后该类单独跑 3 次
**230/230 全绿**。
**环境记录**：本沙箱下门禁第 [2] 步会被 `NU1900`（NuGet 审计缓存位于只读的
`~/.local/share/NuGet/http-cache`）判红 —— 与代码无关；关掉 NuGet 审计即可跑通：
`NuGetAudit=false bash scripts/ci-gate.sh`（试过 `NUGET_HTTP_CACHE_PATH` 指向可写目录，**在门禁里无效**）。

**仍开着（如实转独立议题）**：RAM 预算实测（需在跑的设备）、RK3506 的资源预算无任何证据
（已在插件注释标注"未经实测核对"）、`ResourceEstimator` 系数 ↔ 实测体积校准、
Windows 发布包与包签名。

## 阶段 31（P1-1：表达式驱动颜色/可见性）— 2026-09-19

承接 09-18 计划 §6 的 P1-1（也是登记表 §3 第 15 项，08-02 报告 I1 自留的"后续可扩展"）。
专题见 **`docs/2026-09-19-expression-driven-visual.md`**。

**一句话**：绑定从"只驱动值（Text/Value）"扩展到"可驱动视觉（TextColor / BgColor / IsVisible）"，
设计时**即时生效**、生成代码**周期判定**，两端由同一张注册表与同一个发射器产出。

**三个设计决策（都是不做就会有坑型的）**：

1. **颜色走数值不走 hex 字符串** —— C 里三元 `cond ? "#ff0000" : "#00ff00"` 两分支类型不同
   （`int` vs `const char*`）**编译不过**；统一约定表达式返回 **24 位 RGB 数值**，
   设计时 `ColorFromNumber`、生成期 `lv_color_hex((uint32_t)(v))`，同一套截断规则
   （`-1 → #FFFFFF`、`33488896 → #FF0000` 已有测试锁定）。
2. **复用既有取值机制、不新增 extern** —— `ExpressionEngine` 默认映射产出的
   `hmi_get_variable_value("…")` **在产物里从未有定义**（只在注释/脚本模板里），
   照抄等于造链接期未定义符号。改为给 `ToCExpression` 加带 mapper 的重载，
   把变量接到**既有**通道：设备 `get_tag_value(TAG_x)`、模拟器局部正弦波 `_bxv_<name>`。
3. **视觉属性不走 `VariableBindingId` 旧字段回退** —— 那条回退只承载"主显示绑定"；
   视觉若吃它，控件配了旧字段后 `TextColor` 会把**显示数值**当颜色用。新增
   `allowLegacyFallback` 参数，视觉求值一律传 `false`。

**唯一事实源**：`Core/Design/BindingVisualProperty.cs`（属性名 → 值域 → 应用方式）
+ `CodeGen/VisualBindingCodeGen.cs`（唯一的刷新语句发射点）。未登记的属性**当场抛**
`NotSupportedException` —— 注册表加了条目却忘了派发属于"产物看着对但不生效"，比编译错误难查。
`BgColor` 必须**同时**发 `lv_obj_set_style_bg_opa(…, LV_OPA_COVER, 0)`（Label 默认 `bg_opa=0`，
只设颜色在设备上看不见）。

**接入面**：`PreviewValueResolver`（设计时求值 + 预览隔离契约）、`ExpressionEngine`（变量映射可注入）、
`CodeGenerator.RefreshBindings.cs`（+185 行：视觉/值两路分流，去重键从 `ctrlId` 改为
`ctrlId:propertyName`，第 5 段视觉刷新，设备端套 `if (obj_x) { }` 守卫，
`ContainsUnresolvedPlaceholder` 拦住残留 `{{arg}}`）、`DesignCanvas.cs`（画布侧唯二接缝
`EffectiveVisualColor` / `EffectiveVisible`）、`DesignCanvas.Renderers.cs`（**26 处**颜色直读改走接缝）、
`PropertyPanelView`（「目标属性」下拉 4 项 + 本地化键 ×5 ×2 语言）。

**顺带修掉两个真缺陷**：

1. **预览横幅会毁掉整块画布渲染** —— 它是画布上**唯一**的裸 `DrawText`（其它文本都走
   `DrawAlignedText` 的容错重试），headless 下字体 shaping 抛异常直接从绘制路径抛回 UI 线程
   ⇒ 整块画布空白。抽 `DrawPreviewBanner` 套同一条"捕获 → 失效文本缓存 → 重试一次 → 再捕获"路径。
   定位靠**判别实验**：未被改动的 `GaugeTickRenderTests`/`ArcControlRenderTests` 11/11 绿
   ⇒ 排除环境/字体，收窄到"只有横幅会崩"。
2. **选变量时 `PropertyName` 仍写死 `"Text"`** —— 上轮只改了 `GetOrCreateReadBinding`，
   `OnVariableSelected` 的新增分支漏了。

**固件侧零改动**：产物只用既有 LVGL API（`lv_obj_set_style_*` / `lv_obj_clear_flag`）、
`lv_color_hex` 与既有 `get_tag_value(TAG_*)` ⇒ `firmware/` 工作区无改动，门禁第 [7] 步仍 8 步全过。

**测试 +46**：`BindingVisualPropertyTests` **21**、`VisualBindingCodeGenTests` **13**、
`VisualBindingCanvasTests` **6**（`CanvasRenderSpy` 录绘制操作断言填充色，是"设计时即时生效"的唯一硬证据）、
`PropertyPanelTargetPropertyTests` **6**（含负控"切下拉不改模型"）。

**门禁读数（2026-09-19 现场，`bash scripts/ci-gate.sh`）**：**7 步全绿**。
[1] 24 脚本语法 ✅ / [2] 路径卫生 621 文件 ✅ / [3] 构建 **0 警告 0 错误** /
[4] 出包自检 ✅ / [5] 大队 Simulator 44、Hardware 44、Communication 75、Device 87、AI 315、
**App.UiTests 249**、**Core 1959** / [6] git 用例 **8/8**（648 ms） / [7] 固件 host **8 步全过**。
两段之和 = 全量 **2781**（登记表 09-18 快照 2716；P1-1 直接贡献 +46）。
日志 `.workbuddy/tmp/ci-gate-2026-09-19-postcommit.log` —— 本阶段的 4 个提交
（`3bbdcd4` core / `8cdd9ea` codegen / `340ed20` ui / `bd2ea1f` docs）**全部落定之后的复跑**，
日志 mtime `2026-09-19 16:29:51`。

**负向验证 4 组**：① 临时摘掉 `PropertyPanelWiringTests` 里 `TargetPropertyCombo` 的豁免
⇒ **红**且精确点名该控件，恢复后 2/2 绿；② 门禁第 [5] 步**第一次跑就红**
（`LangConstantUsageGuardTests`：无静态引用的 Lang 常量 1272 ≠ 基线 1267）——
我给 5 个本地化键又加了一份 `Lang.*` 常量，而 XAML 走**键字符串**解析、C# 侧没人读
⇒ 正确处置是**删掉这 5 个常量**，不是改基线；③ `TestAssertionStyleGuardTests` 拦住
8 处裸 `Assert.Contains("lv_…")`，改走 `CSource.AssertCode`（共 9 处）；
④ 把 `PropertyPanelTargetPropertyTests` 的 `expandBindingSection` 改成 `false`
⇒ **红**并报出明确守卫文案，证明真实键盘路径真的依赖"Show 前展开"（见下）。

**顺带判明一条长期挂着"待复核"的宿主行为（专题 §7.4）**：表达式框此前拿不到焦点，
一度怀疑是真实可用性缺陷。判别实验（同宿主内放 baseline 对照）证明**不是**：
headless 支持焦点（裸 `TextBox` 与 `Expander` 外的 `PropName` 都能 `Focus()`），
真正的根因是 **`Expander` 在 headless 下只在"首次布局之前就展开"时才创建内容子树** ——
`Show()` 之后再设 `IsExpanded`，内容**永不 attach**（`attached=false`、
`TopLevel` 不是同一个窗口、Expander 高度停在折叠态 48、视觉后代恒 10），
且 `ApplyTemplate()` + `InvalidateMeasure()` + 强制 `Measure/Arrange` + 多轮 Pump **全都救不回来**；
`Show()` 前展开则高度 288 / 后代 96 / `Focus()=True` / 真实键盘输入拿到 `temp > 200`。
**规矩**：驱动"折叠 `Expander` 内控件"的 UI 测试必须在 `Show()` 前展开，
且用 `Assert.True(box.IsAttachedToVisualTree(), …)` 守住，否则会静默退化成"敲字没反应"。
⇒ 由此把上一版 `PropertyPanelTargetPropertyTests` 里"赋值 + 手动触发 `TextChanged`"的
降级手段换回**真实键盘路径**，那条"未覆盖"局限随之消失。
改写后**复跑门禁仍 7 步全绿**（`.workbuddy/tmp/ci-gate-2026-09-19-expander.log`，
mtime `2026-09-19 16:51:26`，`App.UiTests 249` 全过）——
四个改动文件的 mtime 均早于该日志（16:49:28 / 16:49:57 / 16:50:05 / 16:50:09），
所以这条"绿"确实跑在改动之后；上面 621 那个读数是更早一次 postcommit 复跑的值，
本次为 **623**（新增文件所致）。

**仍开着（如实转登记）**：仅 3 个视觉属性（`IndicatorColor`/`BorderColor`/`Opacity`/对齐未纳入）；
颜色只有 24 位 RGB（无 alpha / 无主题变量）；属性下拉只列 4 项 ⇒ 旧工程里 `PropertyName = "Value"`
之类**用户选不到**（行为已锁，属已知局限）；设备端刷新开销无实机数据；**无实机验证**。

## 阶段 32（P1-3 B 段：SVG 渐变的 CodeGen 发射 + 固件开关）— 2026-09-19

专题：`docs/2026-09-19-svg-gradient-and-transform.md`。A 段（解析/几何/判据）见 `a72ce8d`。

**做出来的能力边界**（不是"支持渐变"四个字，是按渲染栈分流的表）：轴对齐矩形与圆发**真渐变**（`lv_draw_rect` + `lv_grad_*`，圆走 `radius = r` 的圆角矩形）；多边形/Path 的填充、一切渐变**描边**、含旋转的形状、`rx != ry` 的椭圆径向一律降级成 **50% 处采样的纯色**。理由是核过固件：三角扇只有 `HOR`(145)/`VER`(158) 两个分支且色带按**每个三角形自己的包围盒**生成（`color_map[y - tri_area.y]`），`lv_draw_line_dsc_t` 根本没有 `grad` 字段。transform 一律**烘进坐标**。

**三处按源码定下来的契约**：① 渐变端点相对**绘制区域左上角**（`lv_draw_sw_grad.c:473-476` 用区域宽高换算，`get_line` 拿区域相对量）；② `gradientUnits="userSpaceOnUse"` 必须带上**元素自己的 transform**（否则 `<g transform>` 里的形状移走、渐变留原地），判据用烘焙前的矩阵；③ 直接写 `dsc.bg_grad`，**不造局部 `lv_grad_dsc_t`** —— 四个 `lv_grad_*_init` 只写自己那几个字段，结构体尾部还有个 `void * state`（渲染器句柄），整体赋值等于搬进去一个野指针。

**顺手纠正一处会活不过下一次构建的改动**：`LV_USE_DRAW_SW_COMPLEX_GRADIENTS` 最初加在 `firmware/Tina-Linux/package/hmi-runtime/src-config/lv_conf.h`。该目录是 `tina/build.sh:101-104` 每次 `rm -rf` 后从 `firmware/t113/config/.` 拷进去的**产物**，且 `firmware/Tina-*` 整个被 `.gitignore:33/34` 忽略 ⇒ 改了既不入库也活不过一次构建。现已落在跟踪的 `firmware/t113/config/lv_conf.h`，两份 SDK 副本改回原样。

**读数**：Core 新增 **19** 条（`SvgGradientCodeGenTests`），Debug 全量 **2041 通过 / 0 失败**（基线 2022）。门禁 **7 步全绿、`GATE_EXIT=0`**，日志 `.workbuddy/tmp/ci-gate-2026-09-19-svg-phaseB.log` mtime `2026-09-19 20:20:54`；Core 大队 **2033** + git 用例 **8** = 2041（与 Debug 一致），App.UiTests **249**。四个改动文件 mtime（20:13:07 / 20:16:13 / 20:16:45 / 20:18:15）均早于该日志 ⇒ 这条绿确实跑在改动之后。

**负向验证 3 组，全部按预期精准命中、无附带**：① `Resolve` 忽略 `userTransform` ⇒ 恰好 1 红（`UserSpaceGradient_InTranslatedGroup_MovesWithShape`，73/74）；② 端点改画布绝对坐标 ⇒ **恰好 3 红**，与预测完全一致（`...EndpointsAreRelativeToAreaOrigin`、`...OnCircle_EmitsRoundRectWithRadius`、`UserSpaceGradient_...`）；③ `TransformScale` 恒返回 1.0 ⇒ 恰好 1 红（`ScaledTransform_ScalesStrokeWidth`）。恢复后 `grep NEGATIVE-CONTROL` 无残留。

**`lv_conf.h` 只能靠探针证**：门禁第 [7] 步虽然 include 了 `-Iconfig`，但 host 测试刻意不拉 `lvgl.h`（见 `hmi_log.h` 独立成文件的原因）⇒ **没有任何一步会解析那份 `lv_conf.h`**。改用真 LVGL 头链的预处理探针（`gcc -fsyntax-only -DLV_CONF_INCLUDE_SIMPLE -I <lvgl> -I <config>` 译一个 `#if … #error` 的 TU）：正控静默、负控（`grep -v` 掉该行）报 `#error tracked-config-lacks-flag` —— 探针本身不空转。**建议把这条收进门禁**（登记表新增项）。

**仍开着（如实转登记）**：① `LV_GRADIENT_MAX_STOPS` 保持默认 2，>2 个停靠点取首尾、**不重采样**（中间色丢失）；② 椭圆径向按 x 方向近似；③ **C 段未开工**（`DesignCanvas` 渲染侧还没接渐变刷）⇒ **"画布所见"与"设备所得"当前仍不一致**，P1-3 原定验收条件尚未达成；④ ~~门禁缺一条配置探针步骤~~ —— ✅ **见阶段 33 闭环**；⑤ **无实机验证**。

## 阶段 33（把 lv_conf 探针收进门禁 + 判明画布侧的两处偏差）— 2026-09-19

承接阶段 32 的「未闭环 ④」。**本轮只动门禁与文档，不动生成链。**

**新增 `firmware/t113/tests/check_lv_conf_flags.sh`**，作为 `run_host_tests.sh` 的第 **[9/9]** 步（所以 `ci-gate.sh` 仍是 7 步 —— 第 [7] 步内部多了一段）。守卫对象是 `firmware/t113/config/lv_conf.h`：这份配置**此前不在任何编译路径的判据里**，host 测试虽然都带 `-Iconfig`，但刻意不拉 `lvgl.h`（见 `hmi_log.h` 独立成文件的原因）⇒ **没有任何一步会真去解析它**。而 `LV_USE_DRAW_SW_COMPLEX_GRADIENTS` 默认 0，被删掉时渐变**不报错、不空白**，只是被当成 HOR 一维色带画出来（方向与形状都不对）—— 最难发现的那种坏法。

两层判据。**[A] 文本守卫**（无外部依赖，永远跑）：行首锚定「恰好一处 `#define LV_USE_DRAW_SW_COMPLEX_GRADIENTS 1`、全文无 `#undef`」，外加一条**跨语言同源判据** —— `LV_GRADIENT_MAX_STOPS`（未定义则按 LVGL 默认 2）必须等于 C# 侧 `SvgGradientGeometry.MaxStops`；脚本从 `.cs` 里读，**读不到就判红**（守卫不得静默降级）。**[B] 预处理探针**：用真 LVGL 头链译一个 `#if … #error` 的 TU，证宏**真被 LVGL 采纳**。仓库里**没有入库的 LVGL 源码**（`host_test/lvgl` 与 `Tina-*/package/gui/lvgl` 都被 `.gitignore` 忽略，`.gitignore:43` / `:33,34`）⇒ 头链缺席时**明确打印「未验证」**，CI 可设 `CI_REQUIRE_FIRMWARE_TESTS=1` 升级为失败。

**include 顺序是判据的一部分**：`-Iconfig` 必须排在 `-I<lvgl>` 之前。LVGL 目录自己带一份 `lv_conf.h`，且那份里该项**本来就是 1** ⇒ 顺序写反时 `#include "lv_conf.h"` 命中那一份，正控照过、负控亦不报错，探针退化成**恒真的假判据**。第一版正是这么写的，且退出码还被 `gcc … | tail` 吞掉，两次读数都是 `rc=0` —— **负控给出「预期中的绿」时，先怀疑判据本身没在判**。

**负向验证 3 组，全部精准命中，各自落在该落的那一层**：

| # | 构造 | [A] 文本层 | [B] 探针层 |
|---|---|---|---|
| ① | 抠掉那一行 | ❌「恰好一处…实测 0 处」 | 未跑到 |
| ② | 把该行包进 `#if 0`（仍从行首开始） | **✅ 放过** | ❌ `#error tracked-config-lacks-flag` |
| ③ | `LV_GRADIENT_MAX_STOPS 4` | ❌「与 `MaxStops=2` 不一致」 | 未跑到 |

②是唯一「只被 [B] 抓住」的构造，正好界定两层各自的职责。三组都是临时改文件、跑完 `git checkout --` 还原，还原后配置文件在工作区为 0 项。

**顺带判明 C 段的两处偏差（读代码，不是推测）**：`DesignCanvas.IndustrialRenderers.cs:47` 的 `RenderSvg` 只读 `Fill`/`Stroke`，整个 `src/HmiEditor.App/` 里 `FillGradient` / `StrokeGradient` / `shape.Transform` **0 次引用** ⇒ ① **轴对齐矩形与圆**：设备发真渐变、画布只画 50% 降级纯色；② **含 `transform` 的形状**：设备已把 transform 烘进坐标、**画布完全没应用**（`BuildShapeGeometry` 只用 `Points`）—— 这是 A 段之后**新引入**的分叉（A 段之前两边一致地忽略 transform），C 段必须一并收掉。**专题 §6 第 3 条（当时编号 §6.3）与登记表原先把第 ① 条的方向写反了**（写成"画布会画真渐变"），已按代码改正。

**读数**：`bash firmware/t113/tests/run_host_tests.sh` **9 步全过、`rc=0`**，日志 `.workbuddy/tmp/host-tests-2026-09-19-lvconf.log`（mtime `2026-09-19 20:35:11`）；探针层三条头链（`host_test/lvgl`、Tina-Linux、Tina-SDK）全部证到宏 = 1、`MAX_STOPS` = 2。`bash scripts/check-path-hygiene.sh`（626 个文件）通过；`bash -n` 通过。

**仍未闭环**：~~**C 段**~~ —— ✅ **见阶段 34**；阶段 32 的 ①②③⑤ 四项原样转登记。

## 阶段 34（P1-3 C 段：画布接 SVG 渐变与 transform）— 2026-09-19

提交 **`94c9837`**。**先做提取、再接画布** —— 本段的重点不是"给画布接个渐变刷"。

### 提取：烘焙成为唯一实现

生成侧的 `BakeTransform` / `TransformScale` / `TransformPoints` 原本是 `SvgToLvglConverter` 的 `private`；画布要用就只能自己再写一份（⇒ 又一次"画布是斜的、设备是正的"），而判据 `SvgAffine.IsAxisAligned` 一旦分叉，连"该不该降级成纯色"都会两边不一致。新增 `Core/Services/SvgShapeBake.cs`（`Transform` / `ApplyToPoints` / `Scale` / `SampleEllipse`），顺带合并了 `SvgToLvglConverter` 与 `ShapeControlCodeGen` 里**两份逐字相同**的 `SampleEllipse` 私有副本。`SvgToLvglConverter` 604 → 465 行，行为不变（用带「恰好一处」断言的重写脚本做，生成侧 103 条相关用例全绿）。

### 画布：判据与几何都取自 Core

渐变判据 `Decide`（**必须在烘焙前判** —— 烘焙会把 `Transform` 清成 null）；端点/半径 `Resolve(g, BoundingBox(烘焙后), 烘焙前的 Transform)`；停靠点 `NormalizeStops`（<2 个退回纯色，对应 C 侧 `LV_ASSERT(num_stops > 1)`）；几何 `SvgShapeBake.Transform` + Path 展平后 `ApplyToPoints`。**径向按单一半径建刷**（`RadiusX == RadiusY == Radius * sx`）—— `lv_grad_radial_init` 只能表达圆、半径取 x 方向，画布若按 `sy` 另取一个，非正方形控件上当场不一致。

**坐标空间**：画刷必须与 `BuildShapeGeometry` 造的几何点同空间。`PushTransform` 在录制/合成里表现为父级 `DrawingGroup.Transform`、子节点坐标仍是局部的 ⇒ **不能**加 `rect.X/rect.Y`。（Avalonia 无 WPF 的 `MappingMode`，绝对/相对由 `RelativePoint` / `RelativeScalar` 的 `RelativeUnit` 表达。）

### 验证

新增 `SvgGradientCanvasTests` **6 条**（真实 `DesignCanvas.Render` + `CanvasRenderSpy` 录制绘制操作，断言画刷类型/端点/几何包围盒，不是源码 grep）。**负控 4 组按预测精确命中**：① 忽略渐变 ⇒ **2** 红；② 不烘焙 ⇒ **1** 红；③ Path 不应用仿射 ⇒ **1** 红；④ 端点多加控件原点偏移 ⇒ **1** 红（④ 专盯坐标空间，只有那一条会红）。注入用带 `assert(count==1)` 的工具做，还原后逐字节一致。

门禁 **7 步全绿、`GATE_EXIT=0`**，日志 `.workbuddy/tmp/ci-gate-2026-09-19-svg-phaseC.log`（mtime `2026-09-19 21:41:54`，晚于提交）；构建 0 警告 0 错误；**App.UiTests 249 → 255**；Core 大队 2033 + git 用例 8 = 2041；七程序集合计 **2861**。

### 仍未闭环

① ~~**像素级未证**~~ —— ✅ **见阶段 35**（已补）；② 画布每帧为每个渐变形状新建画刷、开销未量化（`CanvasRenderBaselineTests` 的画布不含渐变，基线未受影响）；③ 阶段 32 的 ①②③⑤ 四项原样（`MAX_STOPS=2` / 椭圆径向按 x 近似 / 图案·滤镜·嵌入位图·文本未解析 / **无实机验证**）。

## 阶段 35（像素级验证：补上 C 段那条"只到绘制指令级"的局限）— 2026-09-19

承接阶段 34 的「仍未闭环 ①」。**本轮只加测试基建与判据，不动产品代码**（唯一的 src 改动是还原负控注入，净 diff 为 0）。

### 四条路里前三条是死的（这是本段的主要产出）

| # | 试法 | 结果 |
|---|---|---|
| ① | 现有宿主里 `RenderTargetBitmap.CopyPixels` | **无像素**：读回未初始化内存（`PixelFormat = Rgba8888`、前 16 字节 `12-C0-E4-F8-07-…` 重复、非零 648/1024 全是噪声）。⇒ 阶段 34 那句"不产生像素"**成立**，且现在是有读数的结论 |
| ② | 另开一个 Skia 会话（不动现有宿主） | **起不来**：`AvaloniaHeadlessPlatform.Initialize → new Compositor → DefaultRenderLoop.Add` 抛 `InvalidOperationException: The calling thread cannot access this object`。**渲染循环是进程级静态单例**、绑死在第一个会话的 UI 线程上（**2026-09-21 更正**：该机制描述已被 IL 推翻。**结论不变、理由换成**：「平台配置是进程级的、同进程换不了第二套」＋ `Dispatcher.s_uiThread` 的「谁先构造谁拿走」窗口；逐条 IL 证据见 `docs/2026-09-21-dockpanel-invocation-net.md` §8 的 #47） |
| ③ | 把现有宿主整体切 Skia | **既有用例成片红**：`PropertyPanelWiringTests` 撞同一线程亲和；`ViewSmokeUiTests` 报 `Could not create glyphTypeface. Font family: $Default`（no-op 绘制下看不见字体缺失）；执行被中断（257 条只跑 104 条）⇒ 回退。**收益（一条像素断言）与风险（255 条既有用例换新失败面）严重不成比例** |
| ④ | **单开一个测试程序集** | ✅ 可行 —— VSTest 为每个程序集起独立 testhost **进程** ⇒ 干净的 `DefaultRenderLoop`（**2026-09-21 更正**：「干净的 `DefaultRenderLoop`」这个说法基于上面那句已被推翻的机制。**结论不变、理由换成**：「平台配置是进程级的、同进程换不了第二套」＋ `Dispatcher.s_uiThread` 的「谁先构造谁拿走」窗口；逐条 IL 证据见 `docs/2026-09-21-dockpanel-invocation-net.md` §8 的 #47） |

### 落地

新增 `tests/HmiEditor.RenderTests`（`.slnx` 第 **8** 个测试项目）：`UseSkia()` + `UseHeadlessDrawing = false` + `WithInterFont()`。**门禁一行未改** —— 第 [4] 步是全量 `dotnet test HmiEditor.slnx`，新程序集自动纳入。

判据 4 条：三条业务 + 一条**元判据**（`HostCapabilityTests`：填纯红、读回必须是红）。元判据守的不是产品行为而是**上面三条判据的有效性** —— `UseHeadlessDrawing = true` 时读回的是噪声，那种情况下像素断言要么偶然通过、要么随机失败，**比没有判据更糟**；谁把后端配置改回 no-op，它必须先红。

**判据刻意做成实现无关**：不断言精确插值色值（那取决于 Skia 在 sRGB 还是线性空间插值，换版本就可能变），而是断言方向 + 单调 + **红→蓝的中点必然 R==B**（sRGB 空间 128/128、线性空间 188/188，但等式恒成立 —— 整套里唯一与实现无关的绝对判据）。

### 负向验证 3 组（各自恰好命中对应那一条、无附带）

| # | 注入 | 红 |
|---|---|---|
| ① | 交换渐变起止端点 | **1** —— `LinearGradient_PixelsRunMonotonicallyFromRedToBlue` |
| ② | 不判降级（旋转形状也画真渐变） | **1** —— `RotatedShape_FallsBackToUniformSolidColor` |
| ③ | 径向半径翻倍 | **1** —— `RadialGradient_CenterIsBrighterThanEdge` |

三组互不干扰 ⇒ 三条判据各自在判。注入用带"恰好命中一次"断言的脚本，还原后 sha256 与备份**逐字节一致**、`git diff` 为空。

### 读数

门禁 **7 步全绿、`GATE_EXIT=0`**，日志 `.workbuddy/tmp/ci-gate-2026-09-19-pixel.log`（mtime `2026-09-19 23:53:21`）；构建 **0 警告 / 0 错误**；**八程序集合计 2865**（Core 2033 + git 8 + AI 315 + App.UiTests 255 + Device 87 + Communication 75 + Simulator 44 + Hardware 44 + **RenderTests 4**；上轮 2861 ⇒ +4）；固件 host 9 步全过。

### 仍未闭环

① **设备侧（LVGL 真实产出）依旧无实机验证** —— "画布所见 == 设备所得"里本段补的是**画布那一半**，设备那一半仍无证据；② 像素判据只覆盖「线性 / 径向 / 降级」三态，`<g transform>` 的像素位置仍留在指令级那组；③ 阶段 32 的 ①②③⑤ 四项原样。

## 阶段 36（覆盖率工具链：先量基线，不当门槛）— 2026-09-20

闭环登记表 **#33**。两个提交：`1dbadfd`（工具链）、`098558a`（口径修正 + 负控探针）。**本轮不动产品代码**。

### 为什么不是门禁

覆盖率是"看哪里没测"的**可见性工具**，不是判据。当门禁有两个坏处：① 新增代码天然拉低总量 ⇒ 阈值要么天天红、要么被迫定得毫无意义；② 逼出"为覆盖率而写"的测试 —— 断言存在但不判任何东西，比没有测试更贵（要维护，还不报错）。所以 `scripts/coverage.sh` 只负责把事实量出来，基线登记在本文件。

### 三个非显然的约束

| 约束 | 事实 |
|---|---|
| **必须取并集** | 同一份产品代码被多个测试项目覆盖（`Core` 被 `Core.Tests` 与 `Simulator.Tests` 都测），只看单份报告**系统性低估**。实测：并集 **20349** vs 各报告自称之和 **28774**（并集只会更少） |
| **`TMPDIR` 必须指到仓库外大分区** | coverlet 收尾**先写 `TMPDIR`** 再 attach；本机 `/tmp` 是 **10 MB tmpfs**，单份报告最大 **9.5 MB** ⇒ 多项目并行必炸（实测 `Failed to create … disk was full`）。仓内路径又被 `TestTempDir.NewIsolatedDir` 拒收 |
| **`obj/`·`bin/` 的生成产物不是产品代码** | 实测 `obj/Release/…/RegexGenerator.g.cs` **425 行 / 0% 覆盖** ⇒ 既稀释读数、又顶在"最低覆盖率文件"第一名。已排除，且**排除量必须打印**（否则"读数变小"会被误读成"覆盖率掉了"） |

门禁**一行未改**：第 [4] 步是全量 `dotnet test HmiEditor.slnx`，新增测试程序集自动纳入。

### 判据与负控

三道自检的动机只有一条：**读数看起来正常但其实是错的**。本项目真踩过 —— `<line>` 属性名写成 `hit`（实为 **`hits`**），既不抛异常也不报错，只是让**每一行**都算未覆盖，读数变成"全 0%"而看起来完全正常。

| # | 自检 | 挡的是什么 |
|---|---|---|
| 1 | `<line>` 缺 `hits` → `SystemExit` | 解析口径与 cobertura 结构脱节（不给静默降级的机会） |
| 2 | 每份报告：根节点自称 `lines-covered` vs 按同一份 XML 重数 | "它说覆盖了 1244 行、我们数出 0 行"——**这一定不是"代码没被覆盖"** |
| 3 | 汇总级：并集 ≤ 各报告自称之和 | 合并口径错（并集不可能大于各报告之和） |

`tools/probe-coverage-summary.sh`（**新增**，可复跑）：5 个 fixture、逐条断言退出码与输出片段 ⇒ **5/5 PASS**。

**负控的负控**：注入 `hits`→`hit`（断言恰好命中一处再改）后探针 **4 失败 / 退出码 1**，还原后 sha256 逐字节一致、复跑回到 5/5。**注入后只有「负控2」仍 PASS** —— 因为注入让所有路径都抛那句话，它的期望恰好被满足 ⇒ **单靠负控不能证明代码是好的，正控才在守这件事**。

**探针必须可复跑**（这条是被假绿教出来的）：第一版把 fixture 建在 `/tmp`、分两次 Bash 调用跑；本沙箱 `/tmp` **跨 Bash 调用不共享** ⇒ 第二次 fixture 已蒸发，**5 条全部在"找不到报告"上退出 1**，看起来像"判据都命中了"。改成 `mktemp -d` + `trap … EXIT` 后才有意义。

### 读数

命令 `bash scripts/coverage.sh --no-build`，日志 `.workbuddy/tmp/coverage-run.log`（`2026-09-20T00:03:57+08:00`，`rc=0`，耗时 **36 s** —— 几乎不增总耗时）。**20349 / 46162 = 44.1%**（8 份报告、408 个产品文件）。

门禁 **7 步全绿、`GATE_EXIT=0`**，日志 `.workbuddy/tmp/ci-gate-2026-09-20-coverage.log`（mtime `2026-09-20 00:06:00`，**晚于**提交 `1dbadfd` 的 `00:04:47`）⇒ 加收集器后测试数不变、行为未动：**0 警告 / 0 错误**，**八程序集合计 2865**（44 + 44 + 75 + 87 + 315 + 4 + 2033 + 255 + 8；与阶段 35 逐项一致）。

| 程序集 | 覆盖率 | 备注 |
|---|---|---|
| `HmiEditor.CodeGen` / `HmiEditor.Core` | 94.9% / 92.0% | 生成链主干，与"产物逐字节可复现"的既有纪律一致 |
| `HmiEditor.Device` | 71.2% | |
| `HmiEditor.App` | 28.7%（8463 / 29462） | **最大的未覆盖面**：Avalonia 视图层，大量行在 XAML 事件处理器里，255 条 UI 测试打不到 |
| `HmiEditor.AI` | 10.1% | 315 条 AI 测试集中在 schema/enum 与模板侧；`AgentEngine`/`LayoutAnalyzer`/`AiOutputValidator`/`ClaudeProvider` **整文件 0%** |
| `Hmi.Communication.Builtin` | 6.5% | `ModbusRtu`/`ModbusTcp` 驱动 + 六个 OPC UA 文件**整文件 0%** |

**读数有噪声**：同一提交两次跑差 **5 行**（20354 vs 20349，≈0.01%）—— 涉及并行/计时路径的行偶尔命中不同。⇒ 判定"改动有没有影响覆盖率"时，**5 行级别的差异没有信息量**。

### 仍未闭环

① 阈值门禁（是产品决策，且见上"为什么不是门禁"）；② 分支覆盖率未看（`branch-rate` 在报告里，脚本只汇总行）；③ `App` 的 71% 未覆盖**未分级**（视图层里哪些"真该测"、哪些是 DI 装配样板）；④ 整文件 0% 的清单**未逐个查证**（是"已知未测"还是死代码）；⑤ 覆盖率产物每轮一个新时间戳目录、**不自动清理**（在 `.workbuddy/` 内，已 gitignore）。

## 阶段 37（0% 覆盖清单的排查：4 个死类型 + 一条被证伪的判据）— 2026-09-20

承接阶段 36 的「仍未闭环 ④：整文件 0% 的清单未逐个查证」。提交 **`ee0ac10`**（守卫）+ 文档。**不改产品代码**。

### 先把读数变成可查证的事实

新增 `tools/triage-zero-coverage.py`：按「有没有人引用它」给 0% 文件分桶，**自带正控**（已知被引用的类型必须报出引用数 ≥ 1，否则说明扫描口径坏了而不是"没人用"）。

**94 个文件 / 16755 行** 整文件 0%，三个桶：

| 桶 | 文件 | 行数 | 性质 |
|---|---|---|---|
| 无人引用 | **1** | 75 | 死代码候选 |
| 活但零测试 | 45 | 7753 | 活代码，没有任何测试执行它 |
| 仅被"读源码文本"的守卫测试提到 | 48 | 8927 | 测试**提到**它，从未执行 |

### 主要产出：排查工具自己踩的 5 个误报源

第一版按「**外部引用为 0**」判死活，给出 **104 个**候选，几乎全是误报（`ClaudeResponse`/`Parser`/`TokenType` 这类文件内私有类型就在自己声明那一行当返回类型用着）。收敛到「**全仓出现次数 == 1**」后仍踩了 4 个坑：

| # | 误报源 | 实例 | 决定性证据 | 处理 |
|---|---|---|---|---|
| ① | `X.axaml.cs` 的 `stem` 是 **`X.axaml`**（文件名 ≠ 类型名） | `AnimationEditorView` 判"无人引用" | 被 `Wiring.Panels.cs:234` `new` 着 | 名称**从文件内容解析** |
| ② | **分部类文件** `类名.区域.cs` | 被当成名为 `Wiring.Canvas` 的类型 | 同目录 `Wiring.*.cs` 是一组 partial | 同上 |
| ③ | **扩展方法容器**（用的时候写 `x.IfNullOrEmpty(…)`，类名不出现） | `StringIfEmpty`/`EnumParseExtensions` 类名各只出现 1 次 | `IfNullOrEmpty` 用了 **30** 处、`ParseOrDefault` **5** 处 | 扩展方法名纳入判活 |
| ④ | **字面量里嵌别的语言** | `timespec` 被当成 C# 类型 | 来自 `sb.AppendLine("struct timespec ts;")` | `MaskLiterals` |
| ⑤ | **引用在插值孔里** | `LvglMacros` 判死（全仓仅此一处引用） | `$"…{LvglMacros.LabelLongMode(x)}…"` | 抹字面量**但保留 `{…}` 孔** |

**一条走不通的方向（同样要记）**：为修 ⑤ 试过"改用保留字面量的文本计数" ⇒ 4 个死类型**全部被判活** ——
因为**守卫自己的基线里就写着这些名字**（`"HmiProperty",` 是字面量，各 +1）。**判据被自己的清单喂活 = 恒绿。**

> 与阶段 36 同族的教训：判据的失败模式不止"恒真 / 恒假"，还有"**被自己的数据喂活**"。

### 最大的那一块：`MainWindow` 从未被任何测试构造过

| 事实 | 证据 |
|---|---|
| `MainWindow.axaml.cs` **2370 行**，在**每一份**报告里都是 `0/2370` | 逐报告查过（含 App.UiTests 那份） |
| `MainWindow.axaml` **368 行**，同样 0% | 同上 |
| **`grep -rn "new MainWindow(" tests/` 零命中** | 没有任何测试构造过主窗口 |

对比 App 的覆盖是**高度集中**的：`PropertyPanelView.axaml` 878/883、`ProjectSettingsView.axaml.cs` 624/688、`App.axaml` 570/570 —— 255 条 UI 测试驱动的是"被 wiring 直接实例化的视图"，**主窗口那一层整个在网外**，而它就是 App 那 71% 未覆盖里的大头。⇒ 登记表 **#37**。

### 4 个真死类型（成员级核对过）

| 类型 | 位置 | 行数 |
|---|---|---|
| `HmiProperty` | `src/HmiEditor.Core/Models/HmiProperty.cs` | 9 |
| `ResourceLibraryVersionInfo` | `src/HmiEditor.Core/Models/ResourceLibraryVersionInfo.cs` | 22 |
| `LuaScriptValidator` | `src/HmiEditor.Scripting/LuaScriptValidator.cs` | 154 |
| `LuaSnippets` | `src/HmiEditor.Scripting/LuaSnippets.cs` | 140 |

后两个就是 **`HmiEditor.Scripting` 程序集的全部内容**（该程序集覆盖 0/82 行），而 `HmiEditor.App.csproj` 仍 `ProjectReference` 着它 —— **一个被引用的程序集里 100% 的代码没人用**。**不擅自删**，登记表 **#36**。

**已取证（2026-09-20 补，专题 `docs/2026-09-20-scripting-assembly-triage.md`）** —— 关键修正是**这不是「脚本功能要不要做」**：脚本功能是**活的**，模型（`HmiScript`/`ScriptBinding`/`ScriptTemplate`）+ 服务 + `ScriptEditorView` UI + `lvgl_v9_script.scriban` 代码生成全在 `HmiEditor.Core` / CodeGen 里；死的只是这份**编辑器内 MoonSharp 实现**（全仓 **0 条** `using HmiEditor.Scripting`，唯一引用是 App 里「引了从不用」的 ProjectReference）。**建议整程序集下架**（MoonSharp 全仓只有它一个使用者 ⇒ 依赖可整体退役；影响面 **5 处**已列全）。**唯一反向理由**：Core 的校验是**弱校验**（只查代码含 `function `），而它是 MoonSharp **真解析 + 中文错误 + 行号** ⇒ 决策点只有一句：**编辑器侧 Lua 校验要不要做强**。**另发现**：固件 `firmware/t113/config/lv_conf.h` 里 `LV_USE_LUA` **未定义**，而生成模板以 `#ifdef LV_USE_LUA` 为开关（未定义时退化为空实现）⇒ **脚本链路"最后一跳"未通**，需登记。

### 判据：`DeadTypeGuardTests`（相等判定 + 3 条元判据）

配套 `SourceScanner.MaskLiterals`（新增）：在既有"剥注释"状态机上加"抹字面量内容、**保留插值孔**"，维持原不变量（输出与输入**长度、行数相同**）。

三条元判据守"上面的判据本身有没有坏"：语料非空（`.cs` > 300、`.axaml` > 20 ⇒ 防"声明集合空 ⇒ 死类型恒空 ⇒ 恒绿"）、字面量里的 C 代码不算声明、误报源③⑤的类型必须判活且活代码不许进清单。

**负控 2 组**（各自精确命中、无附带）：① 基线里删掉 `LuaSnippets` 一行 ⇒ 红并点名；② 往 `HmiProperty.cs` 追加 `internal class ZzzDeadProbe` ⇒ 红并点名。**还原**：① 用 sha256 逐字节（该测试文件当时未入库，`git checkout` 救不了）、② 用 `git status --short`；还原后复跑 **4/4**。

### 读数

门禁 **7 步全绿、`GATE_EXIT=0`**，日志 `.workbuddy/tmp/ci-gate-2026-09-20-deadtype.log`（mtime `2026-09-20 16:06:04`，**晚于**提交 `ee0ac10` 的 `16:05:02`）；构建 **0 警告 / 0 错误**；**八程序集合计 2869**（阶段 36 的 2865 **+4** = 新增的 4 条守卫，其余各项逐条一致 ⇒ 纯净增）。门禁**一行未改** —— 第 [4] 步是全量 `dotnet test HmiEditor.slnx`。

### 仍未闭环

① **4 个死类型删不删** + ② **`HmiEditor.Scripting` 整程序集去留** —— 两项已合并取证（专题 `docs/2026-09-20-scripting-assembly-triage.md`）：建议**整体下架**（影响面 5 处已列全），决策点收敛成一句「编辑器侧 Lua 校验要不要做强」；③ `MainWindow` 那一层要不要进网（**需先立测试能力**）；④ 扫描的已知边界：`$"""…"""` 原始插值串整体抹掉（孔规则不同）可能漏判 —— 相等判定会把漏判立刻报出来，不会静默；⑤ 判据只对 `src/` 生效（测试类天然"只出现在声明行"，xUnit 靠反射发现）；⑥ **新增（取证顺带）**：固件 `LV_USE_LUA` 未定义 ⇒ 生成的脚本桥接退化为空实现（`void lua_script_init(void* L) { (void)L; }`）⇒ **脚本功能端到端未通**，需单独立项。**（2026-09-20 同日更正，见阶段 40）：这里的"未定义"被说轻了 —— `LV_USE_LUA` 根本不是 LVGL 的配置项，全 firmware 树零命中。**

## 阶段 38（`HmiEditor.Scripting` 去留取证：一次被名字带偏的定性）— 2026-09-20

承接阶段 37 的「仍未闭环 ①②」（4 个死类型 + `HmiEditor.Scripting` 整程序集）。**只取证、不改产品代码**。提交 `c289b1d`（文档 + 3 处全库收口）。

### 定性被推翻：不是「脚本功能要不要做」

初始判断是「脚本功能是预留没做」——**错的**。取证发现**产品的脚本功能是活的**，实现在
`HmiEditor.Core` 与 `HmiEditor.CodeGen`：

| 角色 | 位置 |
|---|---|
| 模型 | `Models/Scripting/HmiScript.cs`、`ScriptBinding.cs`、`ScriptTemplate.cs` |
| 服务 | `Services/IScriptEngine.cs`、`ScriptEngineService.cs`、`ScriptSandboxExecutor.cs` |
| UI | `src/HmiEditor.App/Views/ScriptEditorView.axaml.cs` |
| 代码生成 | `src/HmiEditor.CodeGen/Templates/lvgl_v9_script.scriban` |

死的只是那份**编辑器内 MoonSharp 实现**。且 `ScriptTemplate.Defaults` 还是「脚本分类清单的唯一来源」
（AI 生成脚本的 JSON Schema `category` enum 由它派生，有守卫锁着）⇒ **不能整个删脚本功能**。

> 教训：**定性不能靠名字**。`LuaScriptValidator` 这个名字本身就是误导源 —— 它听起来像"产品在用的校验器"，
> 实际全仓 **0 条** `using HmiEditor.Scripting`。

### 决定性论据：钩子名对不上

|lua 生成模板 `lvgl_v9_script.scriban:72` 发射 `lua_getglobal(L, "{{binding.function_name}}")` —— 函数名是**运行时要按名查找的真实契约**：

| | 活契约（Core） | 死的 `LuaSnippets` |
|---|---|---|
| 钩子名 | `on_click` / `on_value_changed` / `on_timer` / `on_alarm` | `on_button_click` / `on_value_changed` / `on_timer_tick` / `on_alarm_triggered` |

`on_timer_tick` / `on_alarm_triggered` **全仓只出现在 `LuaSnippets.cs` 自己文件里** ⇒ 把它当模板接进去，
会把"函数名对不上"的脚本插进用户代码，设备上 `dispatch_*` 找不到函数、**静默失败**。

### 决策点收敛成一句

三个选项（A 接进去 / B 整程序集下架 / C 保留标注）里当时**推荐 B**，但留下一个**真实的反向理由**：
Core 的校验是**弱校验**（只查代码里含 `function `），而它有 MoonSharp **真解析**。
⇒ 决策点只剩一句：**编辑器侧 Lua 校验要不要做强**。

专题 `docs/2026-09-20-scripting-assembly-triage.md`（含 5 条证据链、两代实现逐个对照、
若选 B 的 5 处精确影响面、4 条顺带发现）。

### 全库收口（3 处 MoonSharp 旧陈述）

`docs/2026-08-08-hmi-control-research-comparison.md`（2 处）、`docs/2026-07-30-hmi-optimization-dev-plan.md`（1 处）
—— 原文**保留作历史快照**，各补「后续复核（2026-09-20）」段。**注意：本轮之后这批陈述又失效了一次，见阶段 39。**

---

## 阶段 39（编辑器侧 Lua 校验做强：MoonSharp 真解析搬进 Core）— 2026-09-20

承接阶段 38 的决策点。**拍板：做强。** 提交 `5bc3fb0`（10 files changed, **+478/−331**）。

### 实施方式的硬约束：只能「搬家」

我最初设想的选项 A 是「Core 引用 `HmiEditor.Scripting`」——**不成立**：
`HmiEditor.Scripting.csproj` 本身 `ProjectReference` 着 `HmiEditor.Core`（因为
`LuaScriptValidator` 返回 Core 的 `ScriptValidationResult`）⇒ Core 反引即**循环依赖**。
⇒ 唯一可行路径是**把真解析搬进 Core**，然后整个程序集下架。

### 做了什么

| # | 改动 | 验证 |
|---|---|---|
| 1 | 新增 `Core/Services/LuaSyntaxValidator.cs`（`static`；`Validate` / `HasFunctionDefinition` / `StripComments` / `DialectHints`） | **31 条**测试 |
| 2 | `ScriptEngineService.Validate` 三段重排：**语法错误放最前**（UI 只显示 `Errors[0]`，它最具体），`NO_FUNCTION` 判据升级为**剥注释后查 `\bfunction\b`** | 现有 2076 条全过 |
| 3 | `Core.csproj` 加 `PackageReference MoonSharp`（Core 从「零 NuGet 依赖」变为有一个） | — |
| 4 | `src/HmiEditor.Scripting/` 删（3 个跟踪文件走 `git rm`）；`HmiEditor.slnx` 删条目；`App.csproj` 删 `ProjectReference` | App 的 `deps.json` 里 `HmiEditor.Scripting` 命中 **0** |
| 5 | `Directory.Packages.props:47` 的 `MoonSharp 2.0.0` **保留** | 现在由 Core 消费（原计划"整体退役依赖"不再适用） |
| 6 | `DeadTypeGuardTests.ExpectedDeadTypes` **4 项 → 2 项** | 相等判定，见下 |

> **后续复核（2026-09-20 同日，见阶段 40）**：上表第 1 行的 `DialectHints` 已从
> `LuaSyntaxValidator` 的私有成员**收进 `LuaDialect.Hints`**（单一事实源，6 条文案改为从基准派生）；
> 第 6 行的基线进一步 **2 项 → 0 项**（两个死类型经确认后删除）。**下表以阶段 40 为准。**

**UI 零改动** —— `ScriptEditorView` 一直调 `_engine.Validate(script)` 并把 `Errors[0].Message`
显示在校验状态行 ⇒ 行号与中文说明**自动出现在界面上**。

`LuaSnippets` **删除而非搬迁** —— 它的钩子名与当前契约对不上（见阶段 38），
且"模板"这件事已由活的 `ScriptTemplate` 承担。

### MoonSharp 方言实测（实施前做的，决定了措辞）

探针 `.workbuddy/tmp/moonsharp-probe/probe.cs`（MoonSharp 2.0.0，**25 个用例**）：
**MoonSharp 实现的是 Lua 5.2**。

| 写法 | 结果 |
|---|---|
| `a // 2`、`1 & 3`、`1 << 2`、`8 >> 2`（5.3 位运算与整除） | ❌ `unexpected symbol` |
| `local x <const> = 1`（5.4 属性） | ❌ `unexpected symbol near '<'` |
| `goto` + `::label::`、`bit32.band`、`table.pack`、`0x1p4`、可变参数、中文标识符 | ✅ |

⇒ **方言差异是真实的**：设备端若用 Lua 5.3+，用户写 `a & b` 会「编辑器报错、设备能跑」的**误报**。
处理方式：`DialectHints` 把报错改写成可读方言说明（明说是 5.2 基准、若设备版本更高应视为误报），
并在测试里**钉桩**（必须判错 **且**消息必须同时含「Lua 5.3」「Lua 5.2」）。

### 顺手抓到的两个「纸面上的校验」

搬的过程中发现旧实现有**两处从未生效**的代码 —— 形状正常、不抛异常，只是**永远返回一个看起来正常的坏值**：

| # | 缺陷 | 根因 |
|---|---|---|
| ① | **`ScriptError.Line` 恒为 0** | `ExtractLineNumber` 拿 `ex.Message` 匹配 `\((\d+),\d+\)`，但**坐标只在 `ex.DecoratedMessage`**（`chunk_1:(3,14): …`）⇒ 正则永远不命中 |
| ② | **「代码不完整，可能缺少 end」从未触发** | 该映射查 `'<eos>'`，而 MoonSharp 2.0.0 实测报 **`'<eof>'`** |

两条都已修（① 改解析 `DecoratedMessage` + 兜底取总行数、**不谎报第 0 行**；② 两个标记都收）。
**与阶段 36 的 `hit`/`hits` 是同族** —— 判据不报错、不抛异常，只是永远返回坏值。

### 判据与负控

`tests/HmiEditor.Core.Tests/LuaSyntaxValidatorTests.cs`，**31 条**（Theory 展开后）：

| 组 | 内容 |
|---|---|
| 真解析 | 合法通过 / **缺 `end` 必须报出行号**（`Line > 0` + 消息含「第 N 行」）/ 字符串未闭合 / 空内容视为合法（职责在调用方） |
| **方言钉桩** | `//` `&` `\|` `>>` 必须判错且消息含方言说明；5.2 特性必须通过 |
| 剥注释 | 行注释、两种块注释、**行数不变**、字符串里的 `--` 不被误删 |
| 函数定义 | 真定义 True；**行注释与块注释里的假函数必须 False** |
| 集成 | **元判据**：`ScriptTemplate.Defaults` 的 4 个内置模板必须全部通过真语法校验（否则用户点一下「插入模板」就看到红字） |
| **负控** | 取真实模板、抠掉最后一个 `end` ⇒ 必须红；**同场正控**：未破坏的模板必须过 |

### 读数

`dotnet build HmiEditor.slnx` **0 警告 / 0 错误**；`HmiEditor.Core.Tests` **2076 通过 / 0 失败**；
新增测试类 **31 通过**；App 的 `deps.json` 里 `HmiEditor.Scripting` 命中 **0**。

门禁 **7 步全绿、`GATE_EXIT=0`**，构建步 **0 警告 / 0 错误**，日志
`.workbuddy/tmp/ci-gate-2026-09-20-luasyntax-post.log`（mtime **`2026-09-20 17:17:13`**，
**晚于**提交 `5bc3fb0` 的 **`17:12:33`**）。**八程序集合计 2900**：

| 程序集 | 通过 |
|---|---|
| Core | **2068** + **8**（跑 `git` 的用例队列为空时单独跑）= **2076** |
| AI | 315 |
| App.UiTests | 255 |
| Device | 87 |
| Communication | 75 |
| Hardware | 44 |
| Simulator | 44 |
| RenderTests | 4 |

阶段 37 是 **2869** ⇒ 本轮 **+31**，差额**恰好**是新测试类 `LuaSyntaxValidatorTests` 的 31 条
（Theory 展开后），其余各项逐条一致 ⇒ **纯净增**。门禁**一行未改**（第 [5] 步是全量 `dotnet test`）。

> **为什么重跑**：第一次门禁跑在提交**之前**（日志 mtime `17:12:10`，比提交**早 23 秒**）——
> 按本项目惯例，那种时序的「绿」不能作为提交后代码的依据。

### 已知边界（都是**知情选择**，不是缺陷）

| # | 项 | 说明 |
|---|---|---|
| ① | **方言误报** | ~~MoonSharp 只到 5.2，而固件 `LV_USE_LUA` 至今未定义（无 Lua 可对齐）⇒ 目标版本确定后必须重新对齐这一组测试与提示文案~~ ⇒ **已由阶段 40 扩展**：除"误报"外还有**更危险的假阴性**方向（编辑器放行、设备不可用），且其中 5 条编辑器**原理上看不见**。见 `docs/2026-09-20-lua-dialect-alignment.md` |
| ② | **未搬的三个方法** | 原 `LuaScriptValidator` 还有 `GetUndefinedGlobals` / `CheckSuspiciousApis` / `IsBuiltIn`。**刻意不搬**：前两个是正则启发式（会误报 `t.x = 1`）；`CheckSuspiciousApis` 有价值但**接入需要 UI 支持多条消息**（现在只显示 `Errors[0]`）⇒ 登记为后续 |
| ③ | **字符串里的假函数** | `local s = "function foo()"` 会被算作"有函数定义"（不改写字符串内容）。危害小：运行时 `lua_getglobal` 找不到函数会自己报错。已写进方法注释 |
| ④ | ~~固件 `LV_USE_LUA` 未定义~~ | **该表述已作废（阶段 40 更正）**：`LV_USE_LUA` **不是 LVGL 的配置项**，全 firmware 树零命中（`lv_lua_bridge` / `lv_binding_lua` 同样零命中）⇒ 不是"缺一个 `#define`"，而是**整条桥接从未存在过** |
| ⑤ | Core 侧中文校验消息未入本地化字典 | **既有问题**（本轮未引入也未扩大），但 Core 已带两本字典 ⇒ 顺手该修 |

专题 `docs/2026-09-20-lua-syntax-validation.md`。

### ⚠ 沙箱残留（非仓库缺陷）

`src/HmiEditor.Scripting/` 目录里还剩**未跟踪**的 `bin/`、`obj/`（`git rm` 只删被跟踪文件；
批量删除守卫按**后代文件数**计阈值 ⇒ `rm -rf` 会被拦，实测）。它们**惰性**（不 track、不参与构建、
`.gitignore` 覆盖）⇒ 需在沙箱外的终端手动删，或留到下次会话（挂载随命名空间销毁）。

## 阶段 40（Lua 方言对齐 + 最后两个死类型确认删除）— 2026-09-20

承接阶段 39 的两条「仍未闭环」：**方言对齐**（已知边界 ①）与**余下两个死类型**（阶段 37 遗留）。
提交 **`4720c0c`**。

### 一、方言对齐：**结论口径要改**

上一轮写的是"目标固件 Lua 版本确定后必须重新对齐"。本轮取证发现这句话有两处不准：

| # | 事实 | 证据（**零命中也是证据**） |
|---|---|---|
| ① | **`LV_USE_LUA` 不是 LVGL 的配置项** | `rg --no-ignore -c 'LV_USE_LUA' firmware/` ⇒ **零命中**（含 `firmware/t113/host_test/lvgl` 这份 LVGL 源码）。`lv_lua_bridge` / `lv_binding_lua` 同样**零命中**；两份 `lv_conf.h` 里 `lua` 也零命中 |
| ② | **Tina SDK 自带 Lua 5.1.5** | `firmware/Tina-SDK/package/utils/lua`；源码 `lua.h`：`LUA_RELEASE "Lua 5.1.5"` / `LUA_VERSION_NUM 501` |
| ③ | **有一个 x86-64 host 二进制可以真跑** | `firmware/Tina-SDK/out/t113-mq_r/compile_dir/target/host/lua-5.1.5/src/lua`（`ELF 64-bit LSB pie executable, x86-64`） |

⇒ ① 的含义不是"缺一个 `#define`"，而是**整条桥接从未存在过**（加了宏也编译不过，因为
`lv_lua_bridge.h` 不存在）。⇒ ③ 的含义是**"设备侧会发生什么"不必等固件开工就能测**。

### 探针：`compile` 与 `run` 必须分开报

`tools/probe-lua-dialect.lua` + `.sh`（入库，**刻意不进 ci-gate** —— 它需要一个"设备侧 Lua"
解释器，CI 里没有；进门槛只会逼出一个跳过分支，那种判据等于没有）：

| 列 | 做法 | 谁看得到 |
|---|---|---|
| `compile` | `loadstring` 只做词法 / 语法分析 | **编辑器的真解析等价于这一列** |
| `run` | `pcall` 真执行 | 库 / 字段不存在**只在这一列**暴露 |

⇒ **`compile=OK` 而 `run=ERR` 的写法，编辑器无论怎么对齐版本都看不见。** 探针自带两条正控，
不过就 `exit 2` 报 `PROBE_BROKEN`，不会静默当"全通过"。

### 实测：6 条假阴性，其中 5 条编辑器看不见

`bash tools/probe-lua-dialect.sh` 真实输出（12 用例，`compile_ok=7 compile_err=5 run_ok=2 run_err=5`）：

| 写法 | 5.1.5 `compile` | 5.1.5 `run` | 基准（5.2） | 性质 |
|---|---|---|---|---|
| `goto done ::done::` | ❌ `'=' expected near 'done'` | — | ✅ | **假阴性**（解析层） |
| `bit32.band(1, 3)` | ✅ | ❌ `attempt to index global 'bit32'` | ✅ | **假阴性（编辑器看不见）** |
| `table.pack(1, 2)` | ✅ | ❌ | ✅ | 同上 |
| `table.unpack({1, 2})` | ✅ | ❌（5.1 里 `unpack` 是全局函数） | ✅ | 同上 |
| `math.type(1)` | ✅ | ❌ | ✅ | 同上 |
| `utf8.len("x")` | ✅ | ❌ | ✅ | 同上 |
| `local a = 7 // 2` / `1 & 3` / `x <const> = 1` | ❌ | — | ❌ | 一致（都拒）⇒ 不是差异 |
| `local 变量 = 1` | ❌ | — | ✅ | 假阴性，但**成因不是版本**（见下） |

**两个方向危害不对称**：假阳性（编辑器拒、设备能跑）用户会来抱怨但设备不出事；
**假阴性（编辑器放行、设备炸）——用户在编辑器里看到一片绿，到设备上才发现**。
上一轮**只登记了假阳性**，漏了整个危险方向。这是本轮最实质的收获。

### `中文标识符`：实测是假阴性，但**不许**塞进版本模型

`local 变量 = 1`：编辑器通过、5.1.5 词法层就拒。依据是**读 SDK 那份 5.1.5 源码**：
`llex.c` 用**裸 `isalpha`** 按字节判、全仓**没有 `setlocale`**（只有暴露给脚本的 `os.setlocale`）
⇒ 进程停在 `"C"` locale。**成因是 locale / 构建配置，不是版本特性** ⇒
在 `LuaDialect.Features` 里它的 `Minimum` 记 **`null`**，并有判据守着不许给它编一个版本充数。
⇒ **`LuaDialect.FalseNegatives(t)` 给出的是下界，不是完备清单**（这句写进了代码注释与测试）。

### 交付与判据

| # | 交付物 | 说明 |
|---|---|---|
| 1 | `src/HmiEditor.Core/Services/LuaDialect.cs`（新） | 单一事实源：`ParserBaseline`（= 5.2，MoonSharp 硬边界）/ `Target`（= Unknown）/ `Features`（10 条）/ `FalseNegatives` / `FalsePositives` / `Hints` |
| 2 | `LuaSyntaxValidator` | 6 条方言文案**从基准派生**（原来 6 处全写死「Lua 5.2」） |
| 3 | `tools/probe-lua-dialect.{lua,sh}`（新） | 探针入库 + 可复现命令；**示例与事实表逐字一致**（有判据） |
| 4 | `tests/…/LuaDialectTests.cs`（新，**8 条**） | 含 2 条元判据（文案派生 / 表与探针不漂移）、1 条正控边界、1 条**触发线** |
| 5 | `LuaSyntaxValidatorTests` | 版本标签改从 `LuaDialect` 取；`Dialect_Lua52Features_AreAccepted` **改名**为 `Dialect_BaselineAccepts_These_WhichSaysNothingAboutTheDevice`（原名把"基准放行"读成了"设备可用"） |

**关键设计：把"待对齐"做成一条会红的触发线。**
`Target = Unknown` 由判据 `Target_IsStillUnknown_AndDecidingItTriggersRealWork` 钉着 ——
**它红了不代表有 bug，代表有人确定了目标版本**，那时要做完整对齐（重跑探针 → 更新 `Features` →
处置假阴性 → 改钉桩与本专题）。理由：**写在文档里的待办没人读，红色的测试有人修。**

**三条路径**（取决于目标是什么，说明"对齐版本"不是完整答案）：

| 若目标是 | 编辑器该有的行为 | 需要什么 |
|---|---|---|
| **5.1（SDK 自带那条）** | 把 6 条假阴性**报出来**而不是放行 | 设备版本信息 + UI 支持多条消息（现在只显示 `Errors[0]`） |
| **≥ 5.2** | 文案从"可能是误报"改成"与本编辑器一致" | 只改 `LuaDialect` 与文案 |
| **任意** | `Runtime` 那 5 条（库 / 字段缺失）**语法校验原理上无能为力** | **独立机制**：库白名单 / 运行前检查 —— 这是新功能 |

### 二、两个死类型：三层确认后删除

| 层 | 做法 | 结果 |
|---|---|---|
| 1 | 成员级：所有成员有没有被用到 | 只有 `class` 声明那一行；无 `new X` / `List<X>` / `X[]` |
| 2 | 字符串层：反射 / 序列化按名查找？ | 全仓**零**字符串命中 |
| 3 | **`--no-ignore` 全仓（含 `bin/` `obj/`）** | 只有自身声明 + 守卫注释 ⇒ 构建产物里也没有 |

⇒ `git rm` `HmiProperty`(9 行) / `ResourceLibraryVersionInfo`(22 行)；
`ExpectedDeadTypes` **2 项 → 0 项**。

**空基线为什么不是恒绿**（这一步最容易想歪）：基线清空后判据反而更强 ——
语义变成「**`src/` 里不允许存在无人引用的类型**」。它不恒绿的依据是三条**元判据**在守
"扫描面有没有塌"：语料规模下限（实测 `.cs` **601** / `.axaml` **47**）、声明两条路都要出结果、
活类型与三个误报源类型必须判活。**负控 4 直接证明了这件事**（见下）。

### 负向验证 4/4（同场正控）

| # | 注入 | 结果 |
|---|---|---|
| 1 | `Target` 改成 `Lua51` | ✅ 触发线**红** |
| 2 | 把一条 hint 的 `{v}` 写死成「Lua 5.1」 | ✅ 派生判据**红** |
| 3 | 把探针里 `local a = 7 // 2` 改成 `local a = 7 / 2` | ✅ 漂移判据**红** |
| 4 | 往 `src/` 已有文件追加一个没人引用的类型 | ✅ 死类型守卫**红并点名** |

负控 4 的失败输出（判据不仅红，还**点名 + 给实况**）：

```
新增（代码里有、基线没有）= [ZzzNegCtlDeadType]
    ZzzNegCtlDeadType   被声明=True  出现次数=1  判定=死
  语料：.cs 601 个、.axaml 47 个、声明到的类型 614 个
```

**还原**：1/2/3 注入的是同一份 `LuaDialect.cs`，用 `sha256sum` 逐字节核对
（`9a8c0f5c…beffc57` 两侧一致）；同场正控复跑三类 **43/43 绿**。

### 读数

`dotnet build HmiEditor.slnx -c Release -warnaserror` ⇒ **0 警告 / 0 错误**；
`HmiEditor.Core.Tests` ⇒ **2076 → 2084**（**+8** = 新增 `LuaDialectTests`，纯净增）；
三类测试（方言 / 校验 / 死类型）⇒ **35 → 43**。

门禁 **7 步全绿、`GATE_EXIT=0`**，构建步 **0 警告 / 0 错误**，日志
`.workbuddy/tmp/ci-gate-2026-09-20-luadialect.log`（mtime **`17:33:09`**，
**晚于**提交 `4720c0c` 的 **`17:32:20`**）。**八程序集合计 2908**：

| 程序集 | 通过 |
|---|---|
| Core | **2076**（大队）+ **8**（跑 `git` 的用例）= **2084** |
| AI | 315 |
| App.UiTests | 255 |
| Device | 87 |
| Communication | 75 |
| Hardware | 44 |
| Simulator | 44 |
| RenderTests | 4 |

阶段 39 是 **2900** ⇒ 本轮 **+8**，差额**恰好**是新测试类 `LuaDialectTests` 的 8 条，
其余各项逐条一致 ⇒ **纯净增**。门禁**一行未改**。

### 仍未闭环

① **目标版本未定** —— 由 `Target = Unknown` + 触发线承接；固件要真上 Lua，
得先把 `lv_binding_lua` 之类弄进树里（`LV_USE_LUA` 连 LVGL 的配置项都不是）；
② **`Runtime` 类假阴性原理上无解**（语法校验看不见库缺失）⇒ 需独立机制，~~**未做**~~；
   → **后续复核（2026-09-19，阶段 41）**：**已做** —— 版本敏感符号扫描 + `Warnings` 通道，
   见本文件「阶段 41」与 `docs/2026-09-20-lua-dialect-alignment.md` §12；
③ **`中文标识符`** 只登记了事实，**未实测 5.2/5.3/5.4 上的行为**（`Minimum` 记 `null`）；
④ **编辑器只显示 `Errors[0]`** ⇒ 要报多条假阴性必须先改 UI（与 ① 同一前置条件）；
   → **后续复核（2026-09-19）**：**警告侧**已能显示首条 + 计数 + `ToolTip`（阶段 41）；
   `Errors` 侧仍只显示首条；
⑤ 覆盖率未重跑（**不是门禁**，同阶段 39 口径）。

专题 `docs/2026-09-20-lua-dialect-alignment.md`。

## 阶段 41（Lua 运行前检查：给 `Runtime` 类假阴性一个独立机制）— 2026-09-19

承接阶段 40 的「仍未闭环 ②」：`Runtime` 类假阴性（库 / 字段缺失）**语法校验原理上无能为力** ——
`bit32.band(1, 3)` 是**合法 Lua 表达式**，缺的是运行时的库，真解析无论换成哪个版本都看不见它。
本轮拍板做这个独立机制。

### 拍板与取舍：为什么不是"库白名单"

原话是"库白名单 / 运行前检查"。**严格白名单没做**，做的是**版本敏感符号扫描**（差异表）：

| 做法 | 问题 |
|---|---|
| 穷举设备侧全局表 + 静态判定"哪些标识符是全局引用" | Lua 是动态语言：局部变量、`_ENV` 改写、设备侧注入的 `hmi_read` / `hmi_write` / `hmi_set_control_property` 都不在标准库里，`function on_click()` 里的名字也不在 ⇒ 误报多到用户直接无视警告，**等于没有** |
| **差异表**（采用） | 只报"确实差的那些"，噪声可控 |

代价明说：**差异表是下界，不是完备清单**。缺口由判据接住 —— `RuntimeSymbols` 与
`Features` 里 `Stage == Runtime` 的条目**双向断言一致**（名字 + `Minimum` 都钉），
而 `Features` 本身的完备性由探针 `tools/probe-lua-dialect.lua` 承接（同阶段 40 §5 的口径）。

### 交付

| # | 交付物 | 说明 |
|---|---|---|
| 1 | `LuaDialect.RuntimeSymbols`（**5 条**） | `bit32` / `table.pack` / `table.unpack` / `math.type` / `utf8`，每条带 `Minimum` |
| 2 | `LuaDialect.CheckRuntimeSymbols(code, target)` | 剥注释（复用 `StripComments`，行号不漂移）→ **遮蔽字符串字面量** → 逐符号正则扫描 ⇒ `(符号, 行, 列, 判定)`，按行排序 |
| 3 | `LuaSymbolStatus` | `Undetermined`（目标未定，**不能下结论**）/ `Unavailable`（目标已定且确实没有） |
| 4 | `LuaDialect.ExplainHit(hit, target)` | 中文提示，版本标签由 `LabelOf` 派生（与 `Hints` 同一条纪律）；**刻意写短**（要塞进状态栏一行） |
| 5 | **`ScriptValidationResult.Warnings`（新通道）** | 见下节 |
| 6 | `ScriptEngineService.Validate` 第三段 | 只在前两关都过时跑（语法没过时不跑 —— 那时该看的是语法错） |
| 7 | `ScriptEditorView` | 警告走**黄色**（新增 `UiPalette.ValidationWarn`）、显示首条 + 计数、全部挂 `ToolTip`；`QuickSave` 不再把校验结论覆盖成绿色 |

### 关键前提：先得有"警告"这条通道

`ScriptValidationResult` 原来只有 `IsValid` + `Errors`。设备侧目标版本**至今未定**
（`Target = Unknown`），同一个 `bit32` 在 5.2 上完全正常、在 5.1 上必炸 ⇒
写进 `Errors` 就是**把"待定"当成"错误"**：用户会看到满屏红色、却在设备上跑得起来的脚本，
然后学会整片忽略校验。⇒ 新增独立通道，`IsValid` **只看 `Errors`**，警告永远不影响它。

### 判据与负向验证

新增 `tests/…/LuaRuntimeCheckTests.cs` **25 条**。要紧的几条：**两表漂移**双向断言；
`Symbol` 形态只许"标识符 / 标识符.标识符"（它是被扫描的字面符号，不是正则）；
正控 `table.insert` / `math.floor` / `string.len` **都不报**（`table` / `math` 库**本身** 5.1 就有，
敏感的只是新增**字段**）；反假阳性（字符串里 / 注释里 / `x.bit32` / `x:bit32()` / `bit32x` 都不算引用）；
目标已定且够高 ⇒ 零命中（挡"永远全报"的坏实现）；目标 = 5.1 ⇒ 全判 `Unavailable`
且消息**不含"未定"**；反向：未定的消息**不许**说"不存在"；
**`EngineValidate_RuntimeSymbol_IsWarningNotError`**（`IsValid == true` + `Errors` 空 +
`Warnings` 有一条 —— 本组最要紧）；语法错时不跑运行前检查；4 个内置模板零警告（元判据）。

**负控 4/4**（注入 → 只红预期那几条 → 立刻还原）：

| # | 注入 | 结果 |
|---|---|---|
| 1 | `result.Warnings.Add` → `result.Errors.Add` | ✅ 红 **2/25**，恰好是警告语义那两条 |
| 2 | 去掉字符串遮蔽 | ✅ 红 **1/25**：`StringLiteral_IsNotAReference` |
| 3 | 去掉可用性收敛（`if (target != Unknown && Minimum <= target) continue`） | ✅ 红 **1/25**：`TargetHighEnough_ReportsNothing` |
| 4 | 往扫描表塞一条 `Features` 里没有的符号 | ✅ 红 **1/25**：`RuntimeSymbols_MatchRuntimeFeatures_BothWays` |

### 目标一旦确定，这一层自动收敛

`CheckRuntimeSymbols` 的行为**由 `Target` 一个字段决定**：未定 ⇒ 每处都报（判定 `Undetermined`，
文案附"若设备版本 ≥ N 则无此问题"）；已定且可用 ⇒ **不报**；已定且不可用 ⇒ 报 `Unavailable`
并点名目标版本。⇒ 与阶段 40 的触发线（`Target_IsStillUnknown_AndDecidingItTriggersRealWork`）
是**同一处开关**，不需要第二处去改。

### 验证记录

| 项 | 读数 |
|---|---|
| 构建（门禁第 3 步，Release + **警告即失败**） | **0 警告 / 0 错误** |
| `Core.Tests` Lua 相关 | 94 → **119**（**+25**）；**119 通过 / 0 失败 / 0 警告** |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**；八程序集合计 **2933** = 阶段 40 的 2908 **+25**，其余六个程序集**逐条一致** ⇒ **纯净增** |
| 提交 | **`93db3d9`**（10 files changed，**+648 / −13**，`17:48:26`） |
| 门禁日志 | `.workbuddy/tmp/ci-gate-2026-09-19-luaruntime.log`，mtime **`17:49:27`** ⇒ **晚于提交 61 秒**（时序合法） |

### 仍未闭环

① **「某版本起没有了的」这种反向约束**（5.1 的全局 `unpack` 在 5.2 被移进 `table`）——
`Minimum` 只表达"从某版本起才有"，这个模型表达不了 ⇒ 本表**刻意不收**，硬塞会造一个说谎的模型；
② 局部变量遮蔽（`local bit32 = {}`）与表键（`{ bit32 = 1 }`）是已知**假阳性** ——
因为是提示、不判红，取舍是"宁可多报不可漏报"；
③ 长字符串 `[[bit32.band]]` 未遮蔽（`StripComments` 本身也不处理长字符串）⇒ 多报，同上；
④ `CheckSuspiciousApis` 未搬（阶段 39 ④）；
⑤ `ExplainHit` 的中文文案未进本地化字典（不随 UI 语言切换）。
**→ 后续复核（2026-09-20）**：**已闭环**，见阶段 42 与
`docs/2026-09-20-core-script-message-localization.md`。原文保留作历史快照。

专题 `docs/2026-09-20-lua-dialect-alignment.md` §12。

## 阶段 42（脚本校验消息本地化：Core 侧中文入字典 + 首条守卫）— 2026-09-20

闭环阶段 41「仍未闭环 ⑤」：**Core 侧脚本校验的中文消息不随 UI 语言切换**。
阶段 41 刚把警告推到界面上，等于把这条遗留项**放大**了 —— 英文界面下整行都是中英混排。

### 归因：不是「少搬一个字符串」，是消息的形状问题

| # | 根因 | 证据 |
|---|---|---|
| A | `ScriptError.Message` 是**已渲染的成品**（行号、符号名、变体全烘焙进去），本地化层拆不出参数 | `ScriptError` 只有 `Line/Column/Message/Code`，没有 `Args` |
| B | **两层拼接**：产生点自己拼「第 N 行: 」+ 内层 9 个分支变体；外层能进字典、**内层不能** | `BuildSyntaxError` 的 `$"第 {line} 行: {ToChineseMessage(raw)}"` |
| C | **零守卫**：`HardcodedLocalizationGuardTests` 只扫 `src/HmiEditor.App`（基线已清零），Core 侧没有东西会红 | 该守卫的 `AppRootRelative` 常量 |

### 交付

| # | 交付物 | 说明 |
|---|---|---|
| 1 | `ScriptError.Args` | 形状对齐 `Hmi.Hardware.Contracts.ValidationIssue`（`Code` 推键 / `Message` 回落 / `Args` 填模板），全项目只此一种做法 |
| 2 | **Code 细分：5 个产生点 → 19 个 Code** | 3 模型级 + 14 语法族（6 方言 + 8 解析器分类）+ 2 运行前检查；判族走前缀 `IsSyntaxError` / `IsRuntimeSymbolWarning` |
| 3 | `LuaDialectHint` record（`Hints` 升级） | 4 字段（Marker/Code/Version/Fallback）用元组太容易错位；方言提示的尾巴收成 `HintTail` 常量 |
| 4 | **行号交给界面** | `Message` 不再含行号 —— `Line` 本来就是结构化字段，UI 外面还叠了 `"错误: "` 前缀 ⇒ 三重冗余 |
| 5 | `ExternalText`（新，`src/HmiEditor.App/Services/`） | 查模板 → 填参数 → 查不到回落，**唯一实现**；`ProjectSettingsView.LocalizeIssue` 改为调它 |
| 6 | `ScriptEditorView` | Errors / Warnings 都走 `ExternalText.Describe`（含行号），ToolTip 同一套渲染 |
| 7 | 两本字典各 **+20 条** | 19 条 `script.issue.<code 小写>` + `script.line.prefix` |
| 8 | **`ScriptIssueLocalizationGuardTests`（新，7 条）** | 见下 |

### 两处刻意的命名取舍

- **方言 Code 按运算符形态命名**（`LUA_SYNTAX_OP_INTDIV`，不是 `LUA53_INTDIV`）：版本信息走 `Args` 传、
  模板里不写死 ⇒ 基准版本换了不必改 Code 名；
- **运行前检查两态各一个 Code**：话术方向相反（一个说「确定没有」、一个说「暂时不知道」），
  合用一个就只能把区分塞进 `Args`，而 `Args` 是**填模板用的**、不是给程序分派用的。

### 判据

**触发面是真的**：`Probe()` 跑 `ScriptEngineService.Validate` 与 `LuaDialect.DescribeHit`，不抄字面量。
`DeclaredCodes` 用**反射**枚举 `LUA_SYNTAX_*` / `LUA_RUNTIME_SYMBOL_*` 常量 —— 手写清单会与实现分家，
分家之后守卫检查的是清单、不是代码。

已定目标（5.1）那一态（`LUA_RUNTIME_SYMBOL_UNAVAILABLE`）在设备目标确定前不会经由 `Validate` 产生
（`Target` 恒为 `Unknown`），直接调 `DescribeHit(hit, Lua51)` 取 —— 它的字典键必须**提前就位**。

### 真实失败两轮（比事后注入更有说服力）

| 轮 | 红 | 原因 |
|---|---|---|
| 1 | **4/6** | ① 判据自己的占位符统计口径错（假红）② **三个 Code 一个都触发不出来**（真问题，见下） |
| 2 | **2/6** | 一条负控自己的断言写错（拿裸键去匹配带后缀的描述串） |

**注入负控**：把能触发的 `MISSING_END` 塞进豁免表 ⇒ **红 1/7**，点名的正是
`LUA_SYNTAX_MISSING_END`，其余 6 条不受影响 ⇒ 干净命中。

### 守卫抓出来的真问题：`'<eos>'` 的同族病又出现一次

`MISSING_EQUALS` / `MISSING_NAME` / `EXPECTED` 三个 Code **一个都触发不出来**。实测
MoonSharp 2.0.0 对 `local a b = 1` / `local = 1` / `if true do end` **一律报 `unexpected symbol near 'x'`**，
而这三个分支查的是 `'=' expected` / `'<name>' expected` / `expected`。

⇒ 与 `'<eos>'` 那次完全同族：**查了一个这个解析器不产生的报错文本，分支永远不命中，
却带着字典键「看起来有覆盖」地活着**。

处置：**保留分支**（这三类是 Lua 参考实现的标准报错，换解析器就会出现）+ **显式豁免**
（`UnreachableOnCurrentParser`，连预期实参个数一起登记）+ **双向判据**（能触发的 Code 不许留在豁免表里）
⇒ 豁免从「免死金牌」变成**会红的触发线**。

### 验证记录

| 项 | 读数 |
|---|---|
| 构建（门禁第 3 步，Release + **警告即失败**） | **0 警告 / 0 错误** |
| `Core.Tests` | **2109** 通过 / 0 失败（2101 + 单独跑的第二段 8）；Lua 组 **119** —— 与改前**同数**（只改了断言的表达方式，不是放宽） |
| `App.UiTests` | 255 → **262**（**+7**）通过 / 0 失败 |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**；八程序集合计 **2940** = 阶段 41 的 2933 **+7**，其余七个程序集**逐条一致** ⇒ **纯净增** |
| 提交 | **`1eec98a`**（15 files changed，**+797 / −69**，`18:49:26`） |
| 门禁日志 | `.workbuddy/tmp/ci-gate-2026-09-20-scriptmsg.log`，mtime **`18:50:27`** ⇒ **晚于提交 61 秒**（时序合法） |

### 仍未闭环

① ~~`hw.issue` 的占位符判据仍按**出现次数**数（它的模板里没有重复占位符所以从未暴露）—— 统一属独立议题~~ ⇒ ✅ **已闭环（阶段 43）**：判据收成唯一实现 `Core/Services/LocalizationPlaceholders`（**运行期 `Format` 与四处守卫同源**）。**当时这条低估了** —— 同样的口径在**运行期**，且已经让界面显示花括号字面量（见阶段 43）；
② `Errors` 侧仍只显示首条（阶段 41 遗留；本轮只换了渲染，未加计数）；
③ **`HmiEditor.Core` 里其他用户可见文案未盘点** —— 本轮只覆盖脚本校验这一条通道，其余仍无守卫；
④ 目标 Lua 版本未定（与本轮无关；但 `UNAVAILABLE` 那一态的文案**现在就有键**）。

专题 `docs/2026-09-20-core-script-message-localization.md`。

## 阶段 43（占位符判据唯一实现：修掉"重复占位符模板在界面上显示花括号字面量"）— 2026-09-20

闭环阶段 42「仍未闭环 ①」。**它不是"某处少改一个字符串"，而是判据分家**：
"模板里的占位符与实参对不对得上"这**一个**概念在本仓库有**五处实现、两种口径**，
而**运行期**那份用的是**错的那种**（数占位符**出现次数**）。

### 归因：五处实现、两种口径

| # | 位置 | 口径 | 结论 |
|---|---|---|---|
| ① | **运行期** `Core/Services/LocalizationService.Format`（两个重载**各一份**） | 数**出现次数** | ❌ 重复占位符 ⇒ 假"不符" ⇒ **原样返回**；洞 ⇒ 放行给 `string.Format` ⇒ **抛** |
| ② | `App.UiTests/ScriptIssueLocalizationGuardTests`（阶段 42 新写） | 不同索引 + 恰好 `0..n-1` | ⚠️ 规则对，但正则不认 `{0:N2}` ⇒ 漏看 |
| ③ | `App.UiTests/PluginTextLocalizationGuardTests` | 数**出现次数** | ❌ 同上 |
| ④ | `App.UiTests/LocalizedLogCallSiteTests` | 数**出现次数** | ❌ 同上 |
| ⑤ | `Core.Tests/LocalizationTests`（zh/en 一致性） | 两边**出现次数**相等 | ❌ 数量相等而索引不同 ⇒ 放过，而运行期**抛** |

**为什么阶段 42 的守卫没抓到**：它比对的是"模板 vs `Args`"，**没走运行期的 `Format`** ——
**守卫与被守的对象用两套规则，绿就是假绿**。

### 事实（只读盘点两本字典，各 2285 键）

| 类别 | zh-CN | en-US |
|---|---|---|
| 同一占位符出现多次 | **1** | **1**（`script.issue.lua_runtime_symbol_undetermined`，阶段 42 新加的） |
| 索引有洞 | 0 | 0 |
| 转义花括号 `{{` / `}}` | 0 | 0 |

### 交付

| # | 交付物 | 说明 |
|---|---|---|
| 1 | `Core/Services/LocalizationPlaceholders.cs`（新） | **唯一判据**：`Indexes` / `MatchesArgumentCount` / `DescribeIndexes`；规则＝不同索引数 == 实参数 **且** 索引恰好 `0..n-1`；认 `{0:N2}`、跳转义花括号 |
| 2 | `LocalizationService.Format` | 两个重载收成一个 `FormatCore`（顺手消掉同文件内重复的两段 8 行判断），改调该判据 |
| 3 | 三处守卫 | 改为调用它（`ScriptIssue` 的本地实现变一行转发；`PluginText` 保留决定表；`LocalizedLogCallSite` 删掉已无人用的正则字段） |
| 4 | zh/en 一致性判据 | 从"数量相等"升级为"**索引序列相同**" |
| 5 | 新判据 6 条 | 边界判定表（11 行，**每行注明旧口径会怎样**）+ 全库**形状** + 全库**渲染**（真走 `Format`）+ 洞的契约 + **回归判据** |

**行为变化（只有两处，都是修缺陷）**：同一占位符出现多次 ⇒ 由"原样返回"变**正常渲染**；
索引有洞 ⇒ 由"**抛 `FormatException`**"变"原样返回"（"不符就原样返回、不抛"是既有契约）。

### 判据与负控

**修前红（先判据、后修）**：`.workbuddy/tmp/red-1.log`（`EXIT=1`）——
`Format` 返回的正是 `"{0} 需要 {1} 或更高，设备目标未定、暂无法判定（设备若为 {1} 或更高则无此问题）"`。

**端到端负控**：往两本字典**同一个键**注入一个洞（两侧同改）⇒ **三层判据同时点名同一个键**：
`EveryTemplate_HasConsecutivePlaceholderIndexes`（全库形状）、`Format_RendersEveryPlaceholderOfEveryTemplate`
（运行期行为）、`PluginTextLocalizationGuardTests.IssueTemplates_PlaceholderCountsMatchPluginArgs`（插件侧决定表）；
`zh/en 一致性`按预期**保持绿**（证明两类判据独立）。还原后复跑 26 绿。

**边界表当场纠正的是我自己**：`{{0}} 与 {1}` 那一行期望值写错（真占位符只有 `{1}` ⇒ 判据拒它是对的）、
`DescribeIndexes` 的注释与实现不符 —— 修的是期望值与注释，**没动判据**。

### 验证记录

| 项 | 读数 |
|---|---|
| 提交 | **`73cb31f`**（7 files changed，**+338 / −65**，`19:01:55`） |
| 构建（门禁第 3 步，Release + 警告即失败） | **0 警告 / 0 错误** |
| `Core.Tests` | **2115** = 2107 + 单独跑的第二段 8（阶段 42 的 2109 ⇒ **+6** ＝本轮新增 6 条判据） |
| `App.UiTests` | **262**（只改判据里怎么比，**不加用例**） |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**；八程序集合计 **2946** = 阶段 42 的 2940 **+6**，其余七个程序集**逐条一致** ⇒ **纯净增** |
| 门禁日志 | `.workbuddy/tmp/ci-gate-2026-09-20-placeholder.log`，mtime **`19:03:59`** ⇒ **晚于提交 124 秒**（时序合法） |

**一条过程记录**：第一次门禁调用"失败"是**我自己的命令**写错 —— fish 里没有 `$?`（应写 `$status`），
脚本本身跑完了、日志末尾是 `🎉 门禁全部通过（7 步）`。改用 `bash -c` 重跑才拿到 `GATE_EXIT=0`。

### 仍未闭环

① `Errors` 侧仍只显示首条（阶段 41 遗留）；~~② Core 侧其他用户可见文案未盘点（阶段 42 §7 ③）~~
⇒ ✅ **已闭环（阶段 44）**：盘出 1070 条中文字面量，排除 405、**已核实到界面 61 条 / 6 条通道**、待定性 604；
③ 目标 Lua 版本未定；④ 局部变量遮蔽 / 表键是已知假阳性；⑤ `CheckSuspiciousApis` 未搬；
⑥ `MainWindow` 从未被测试构造（登记表 #37）；
⑦ **本轮新增**：判据只覆盖"占位符 vs 实参"，**不覆盖**"模板里占位符语义写错"（`{0}` 写成 `{1}` 而实参给够 ⇒
编译期/运行期都过得去，只有人看得出）。

专题 `docs/2026-09-20-placeholder-judgment-single-source.md`。

## 阶段 44（Core 侧用户可见文案盘点：先分类再计数 + 只减不增守卫）— 2026-09-20

闭环阶段 42 §7 ③ / 阶段 43 §8 ②。App 层的中文硬编码守卫早就有（阶段 19），扫描面只有
`src/HmiEditor.App`；**`HmiEditor.Core` 里同样有文案直接显示在编辑器界面上，却完全在守卫之外**。

### 归因：同一套规则搬到 Core 会长出 1000 条假阳性

App 守卫是「分类式」的（剥注释 → 取含 CJK 字面量 → 扣排除类别 → 其余算待本地化）。
这套规则在 App 里成立，在 Core 里不成立 —— Core 是库，中文字面量绝大多数不是界面文案。
所以判定出发点从「它是中文」改成「**这条字面量从哪个出口走**」。

### 盘点结果（`src/HmiEditor.Core`，48 个文件 / 1070 条）

| 类别 | 条数 | 依据 |
|---|---|---|
| 生成产物文本 | 331 | `CodeGen/`、`SimulatorProjectExporter*`、`*CodeGen.cs`，或接收者是 `sb` 的写入调用 |
| 日志 / 调试出口 | 57 | `Log?.Invoke` / `Debug.WriteLine` / `Console.` / `Trace.` |
| 开发者消息 | 17 | `throw new` / `Exception(` |
| **已核实到界面** | **61** | 6 条通道，每条附消费方文件 + 显示标记 |
| 待定性 | 604 | 逐文件登记，注释写明归类与证据 |

**六条通道**：`GeneratedCodeValidator`(23) / `CrossReferenceAnalyzer`(13) / `AuthenticationService`(12, 审计面板) /
`ScriptSandboxExecutor`(7) / `SvgParser`(3) / `GitWorkflowService`(3)。
其中后两条与 App 守卫那条「非首实参不豁免」同形：消息进了**已本地化模板的实参位** ⇒ 英文界面下中英混排。

### 两个反例比结论更值钱（先入为主被判据推翻）

| 文件 | 直觉 | 实测 |
|---|---|---|
| `Design/DefaultPropertyValidator.cs`(10) | 属性面板红字，显然可见 | ❌ `PropertyPanelView.Changes.cs:61` 只 `Debug.WriteLine` 后 `return`，界面**什么都不显示** |
| `Services/CsvVariableImporter.cs`(16) | 导入失败原因，显然可见 | ❌ 两个消费方**都只显示 `Errors.Count`**，消息文本没人读 |

⇒ 「结论要能指到一次 grep，不能靠名字推断」在本轮再次生效。

### 交付

| # | 交付物 | 说明 |
|---|---|---|
| 1 | `tests/HmiEditor.Core.Tests/CoreVisibleTextInventoryGuardTests.cs`（新，9 条） | 四条结构性排除类别 + 两桶基线（`VisibleChannels` 61 / `Pending` 604） |
| 2 | **通道证据防腐** | 每条通道登记「消费方文件 + 显示标记」，判据检查两者都还在 ⇒ "它会到界面"不会腐烂成无据之言 |
| 3 | 剥注释复用 `SourceScanner.StripComments` | 不在新守卫里再抄一份从简实现 |
| 4 | 专题 `docs/2026-09-20-core-visible-text-inventory.md` | 盘点表 + 通道证据 + 反例 + 负控 + 仍未闭环 |

### 判据与负控

**负控（一次注入 ⇒ 两条独立判据各自点名）**：往 `SvgParser.cs` 注入一条新文案 +
把 `ValidationResultView.axaml.cs` 的 `Text = finding.Message` 改掉 ⇒ **2 红 7 绿**：

| 红的是 | 点名 |
|---|---|
| `NoUnclassifiedCoreChinese` | `~ Services/SvgParser.cs: 登记 3，实扫 4` |
| `VisibleChannels_EvidenceStillExists` | `通道 Services/GeneratedCodeValidator.cs 声称经由 …ValidationResultView.axaml.cs 显示，但那里已经没有标记：Text = finding.Message` |

其余 7 条保持绿 ⇒ 两类判据互相独立。还原后 9 绿。

**一次真实的自我修正**：`GeneratedWrite` 最初写成裸 `AppendLine`，首跑自检用例红 ——
它会把 **CSV 导出列名**（`DataLogger` / `RecipeManager` / `CsvVariableImporter` 都在用 `AppendLine`）一并放过，
而**排除是漏报方向**。收紧为「接收者必须是 `sb`」后基线**一条没变**。

### 验证记录

| 项 | 读数 |
|---|---|
| 提交 | **`e71fc1b`**（1 file changed，**+396**，`19:14:07`） |
| 构建（门禁第 3 步，Release + 警告即失败） | **0 警告 / 0 错误** |
| `Core.Tests` | 主段 2107 → **2116**（**+9** ＝本轮新增判据数），独立段仍 8 |
| `App.UiTests` | **262**（未动 App 测试） |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**；八程序集合计 2946 → **2955**，**除 Core.Tests 外七项逐条一致** ⇒ 纯净增 |
| 门禁日志 | `.workbuddy/tmp/ci-gate-2026-09-20-coretext.log`，mtime **`19:15:04`** ⇒ 晚于提交 **57 秒**（时序合法） |

### 仍未闭环

① **61 条真缺口未本地化**（本轮只盘点 + 立守卫）；② 604 条里 `DATA` 那一类「要不要跟 UI 语言」是产品决策
（同 App 守卫的落盘标识，登记表 §4 第 27c 项）；③ 两个守卫**各有一份扫描基础设施** —— Core 侧已改用
`SourceScanner`，App 侧仍是自己的从简实现，合并有行为风险（更稳的剥注释可能让 App 基线变红），需单独一轮先量差异；
④ 通道证据只覆盖**六条已知**通道，新增链路的取证责任仍在人；⑤ `ScriptSandboxExecutor` 的 `[Sandbox]` 前缀仍硬编码；
⑥ **`ActionExecutor` 的失败消息链路是断的** —— `PreviewRuntimeService` 只取 `bool`，用户看不到任何动作失败原因，
这是功能缺口而不只是文案问题。

**（2026-09-20 阶段 45 补）** ①中「消息被塞进**已本地化模板的实参位**」那一族**已闭环** ——
`SvgParser` / `GitWorkflowService` 两条通道正是这个形态（`Format("asset.svg.parse.fail", error ?? …)`），
阶段 45 的 `C1`/`C2` 判据已接管这一类；①**其余部分（Code + Args + 字典化）仍未做**。
③两个扫描基础设施仍未合并 —— 阶段 45 又给 App 那份加了 `.axaml` 面，合并成本略升。

专题 `docs/2026-09-20-core-visible-text-inventory.md`。


## 阶段 45（本地化键「占位符 ↔ 消费方式」闭环：33 处调用点 + 17 处 XAML 引用 + 4 条判据）— 2026-09-20

承接阶段 44 §7 ①。**`LocalizationService.Format` 在"占位符与实参对不上"时不抛异常、原样返回模板**
（只留一句 `Debug.WriteLine`，Release 下被编译掉）⇒ 这类错误编译器看不见、运行期不报错，
只能靠静态判据挡。本轮把**三种消费方式两两之间**的关系全部钉死。

### 归因：一族的三个出口，其中最危险的一个是「最自然的修法」

| 类 | 根因 | 处数 | 界面表现 |
|---|---|---|---|
| **C1-a** | 模板缺占位符（22 个键） | 29 | 实参被**静默丢弃**：`导出失败` 从不说是为什么、`写入成功` 不显示值、`运行中` 连计时都不显示 |
| **C1-b** | 索引器取含占位符的模板（4 个键） | 4 | 界面**直显 `{0}`**（`已删除类型: {0}`、`打开失败: {0}`） |
| **C1-c** | 实参比占位符多 1 个（1 个键） | 1 | 多出的实参被丢弃（`var.udt.added.field` 丢了 `dataType`） |
| **C2** | XAML 无参引用**含占位符**的模板 | 3 | 界面直显 `产物目录：{0}` / `目标: {0}` / `{0} 项` |
| **C4-a** | XAML 键名拼写漂移（漏层级点号） | 13 | 界面直显键名 `prop.param.valvetype.ball` |
| **C4-b** | XAML 引用了真不存在的键 | 1 | 同上（`prop.param.valve.type.globe` 截止阀） |

### 「同一键被两种消费方式引用」= 补占位符本身会制造回归

本地化键有两种消费方式，**实参数天然不同**：代码侧 `Format(key, args…)` 是 0…n；
XAML 侧 `{res:Loc key}` 经 `LocExtension → LocalizedString.Value` 取的是**原始模板**，**恒为 0**。

实测 4 个键**同时**被两边引用（形态完全一致：**XAML 给初始值、代码随后用带参版本覆盖同一个控件**）：
`var.list.title`(`LblTitle`) / `var.udt.title`(`LblUdtTitle`) / `faceplate.contains.controls`(`TxtControlsSummary`)
/ `sim.running`(`RunTimeText`)。⇒ 对它们，「给模板补 `{0}`」**是制造回归**：切语言时
`LocalizedString` 的通知会把 `运行中 {0}` 推回控件，而它不在运行态就再也不会被覆盖。
正确修法是**另立 0 占位符的兄弟键**（`var.list.title.count` / `var.udt.title.count` /
`faceplate.contains.controls.count` / `sim.running.elapsed` / `status.target.label`）。

同类但**语义不同**的一处：`tb.search.count` 挂在 `AutomationProperties.Name`（无障碍名）上 ——
无障碍名本就该是标签而不是计数，所以它要的是标签键而不是兄弟计数键。

### C4 的 13 处不是"缺文案"，是"引用写错"

正确键**早已存在**（`prop.param.tank.showscale` / `prop.param.pipe.direction`(+`.horizontal`/`.vertical`) /
`prop.param.pipe.flowdirection`(+…) / `prop.param.pipe.flowspeed` / `prop.param.valve.type`(+…)），
只是 XAML 漏了层级点号。⇒ 若按"键不存在就补文案"处置，会**造出 13 组同义重复键**，此后两边各改各的。

### 交付

| # | 交付物 | 说明 |
|---|---|---|
| 1 | `tests/HmiEditor.App.UiTests/LocalizedFormatCallSiteTests.cs`（新，298 行，5 条） | C1 调用点实参数 / C2 XAML 无参引用 / C3 两语言占位符一致 / C4 XAML 键存在性 + 1 条自检 |
| 2 | `AppSourceScanner.ScanAppXaml()` | 本地化键有两种消费方式，只扫 `.cs` 会**整类漏掉**第 2 种 —— 这正是 C2 的来源 |
| 3 | 判据全部**复用**既有实现 | `LocalizationPlaceholders`（与运行期同源）/ `SplitTopLevelArguments` / `LoadDictionary` / 反射取 `Lang` 声明 |
| 4 | 专题 `docs/2026-09-20-localization-placeholder-closure.md` | 归因 + 回归风险 + 拼写漂移 + 判据 + 负控 + 仍未闭环 |

### 验证记录

| 项 | 读数 |
|---|---|
| 提交 | **`82c36e0`**（12 files changed，**+399 / −66**，`19:43:46`） |
| 判据红→绿 | C1 **33 → 0**、C2 **3 → 0**、C4 **14 → 0**、C3 恒 0 |
| 字典 | zh/en 各 **2285 → 2294**（+9，键集一致；19 处模板补占位符；脚本回读校验"其余键逐条不变"） |
| `App.UiTests` | 262 → **267**（+5 ＝本轮新增判据数） |
| 负控 | 3 处**互相独立**的注入 ⇒ **C1/C2/C4 各自点名**（`var.saved`@VariableManagerView:464 / `sim.running`@SimulatorView.axaml:252 / `tb.controls.listX`@ToolboxView.axaml:40,76），C3 与自检仍绿；还原后 4 文件 **sha256 逐文件一致**、复跑 5/5 绿 |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**；八程序集合计 2947 → **2952**（+8 单独段 = 2960），比阶段 44 的 2955 **净增 5**、其余七项逐条一致；构建 **0 警告 / 0 错误** |
| 门禁日志 | `.workbuddy/tmp/ci-gate-2026-09-20-locclosure.log`，mtime **`19:44:35`** ⇒ 晚于提交 **49 秒**（时序合法） |

### 仍未闭环

① 占位符**语义**写错（位置占位符 ⇒ 交换实参两边都看不见），根治要命名占位符 —— 独立议题；
② `ExternalText.Resolve(key, fallback, args)` 的唯一调用点（`ProjectSettingsView.axaml.cs:315`）
实参是 `issue.Args` 数组，**静态不可校验**；③ 本文件只断言**占位符** parity，**键集** parity 仍由
`Core.Tests/LocalizationTests` 负责（有意分工，但没写在同一处）；④ `C1` 只校验实参**个数**，
不校验类型/来源；⑤ `Lang.cs` 里 `CompileOutput`/`TbSearchCount` 这类常量现在**只被 XAML 以字面量键使用** ——
`C4` 能挡住键消失，挡不住常量与 XAML 各自漂移。

## 阶段 46（主窗口进入测试网：构造 + 装配 3 条判据）— 2026-09-20

登记表 **#37** 的收口。专题 `docs/2026-09-20-mainwindow-construction-net.md`；提交 `c11a392`。

### 做了什么

`MainWindow`（2370 行代码 + 368 行 XAML）此前**从未被任何测试构造过**
（`grep -rn "new MainWindow(" tests/` 零命中），覆盖率里整层 `0/N`。不做它的理由曾被写进注释
（`DataRootMigrationWiringTests`："行为测试要构造整个 MainWindow 才够得着，**成本高且脆弱**"）
—— 本轮先量成本：headless 里构造一次 **928 ms**，该理由不再成立。

构造需要的三样隔离都接上了：数据根（`HMI_APP_DATA_ROOT`；`AppPaths` **有意不缓存**，所以测试里改
来得及）、`SettingsService` 的持久化路径（构造参数）、本地化单例（放进 `DisableParallelization`
集合，用完还原）。关闭路径本来就干净：`OnClosing` 持久化几何/布局，`OnClosed → CleanupOnShutdown`
停自动保存 Timer 并释放仿真器与 `AiPreviewManager`。

### 判据（3 条，`tests/HmiEditor.App.UiTests/MainWindowConstructionTests.cs`）

| # | 判据 | 类型 | 守什么 |
|---|---|---|---|
| 1 | `MainWindow_ConstructsAndLaysOut` | 行为 | 构造 + `Show` + 布局 + `Close` 全通 |
| 2 | `EveryNamedControl_IsTouchedByTheCodeBehind` | 静态绊线 | XAML 里 **143** 个具名控件必须都被 code-behind 真正用到（成员访问/实参）—— 守"加了控件忘了接线"与"接线被删"（两者都**能编译通过**、且无运行期症状） |
| 3 | `MainWindow_LanguageMenuItem_ActuallySwitchesTheUi` | 行为 | 点"English"菜单项 ⇒ 界面文案当场变英文 |

判据 2 是**绊线不是证明**：只问"有没有被碰过"，不问"接的是不是对的处理函数"。

### 两处被实测推翻的口径

| 原本想当然 | 实测 | 处置 |
|---|---|---|
| 按文件名 glob `MainWindow*.axaml.cs` 盘点接线 | 报"127 个具名控件里 **100** 个零引用" —— 实际 `MainWindow` 由 **7** 个 partial 构成（含 6 个 `Wiring.*.cs`）；改按**声明**取面后 100 → **0** | 判据按 `partial class MainWindow` 取面，并带自检（partial ≥ 6、具名控件 ≥ 140） |
| "每个具名控件都能在活树里找到" | 可视树缺 **95**、逻辑树缺 **46**：菜单项只在逻辑树（弹层未打开），工具栏按钮因 `CommandBar` **动态溢出**两棵树都可能不在 —— 是模板与溢出机制的产物，不是装配缺陷 | **整条不发**（假红会训练人忽略判据） |

### 负控（3/3，各自独立命中）

| 注入 | 期望红 | 实测红 | 判据点名 |
|---|---|---|---|
| N1 掏空语言菜单处理器（删掉 `SwitchLanguage("en-US")`） | 只有 3 | 只有 3 | `Expected: "en-US" Actual: "zh-CN"` |
| N2 删掉 `MenuZoomReset.Click += …` 一整行 | 只有 2 | 只有 2 | `从未被使用：MenuZoomReset` |
| N3 构造器里漏掉 `_settingsService = settingsService;` | 1 + 3 | 1 + 3 | `NullReferenceException @ InitializeAutoSave()` `MainWindow.axaml.cs:1300` |

"只有 X" 是**断言**出来的（脚本逐条比对红集合）。还原后 3 文件 `sha256` 逐字节一致、复跑全绿。
**一条没有发布的判据**："同一进程连续构造两轮都成功"（本可守 `Close()` 是否把资源放干净）——
找不到能**单独**隔离它的最小注入，按"每条判据都要有自己的负控"不发，改记在专题 §3。

### 读数

| 项 | 值 |
|---|---|
| 构造后规模 | 可视树 **315** 控件 / 其中具名 **84**；逻辑树 **165**；XAML 具名 **143**；partial **7** |
| `App.UiTests` | 267 → **270**（+3 ＝本轮新增判据数） |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**；八程序集合计 **2955**（+8 单独段 = **2963**），比阶段 45 的 2960 **净增 3**、其余逐条一致；构建 **0 警告 / 0 错误** |
| 门禁日志 | `.workbuddy/tmp/ci-gate-2026-09-20-mainwin.log`，mtime **`20:13:38`** ⇒ 晚于提交 **51 秒**（时序合法） |

### 仍未闭环

① 只挑了**一条**菜单做行为验证（语言），其余 60 余个菜单项只有静态绊线，不证明"接了正确的处理器"；
② `Close()` 释放是否干净**无判据**（见上）；③ 文件打开/保存路径未覆盖（需 `.hmi` 工程夹具 +
`StorageProvider` 替身）；④ 一次失败的注入尝试暴露一个**未解现象**：把 `{StaticResource …}` 的键名
写错，**既不编译报错也不在解析期抛**（三条判据全绿）—— 本轮未查证。
⇒ **已查证并在阶段 47 收口**：确实是静默，且查出**两种形态**（落在控件样式属性上 = 静默跳过赋值、
回落主题默认；落在资源字典内对象的属性上 = 推迟到读取时才抛 `KeyNotFoundException`）。
顺着它抓出一个**真缺口**：`HmiWarningDeepBrush` 全历史从未被定义过，3 处按钮背景一直回落成默认灰。

## 阶段 47（XAML 资源键闭合：一个从未被定义过的画刷键 + 4 条判据）— 2026-09-20

阶段 46 挂起的那条"未解现象"的收口。专题 `docs/2026-09-20-xaml-resource-key-closure.md`；提交 `f0cda82`。

### 做了什么

**先定性**（排工具口径）：`dotnet build -c Release` 退出码 **0 / warning 0 / error 0 / 含 `StaticResource` 的行 0**，
且 `App.dll` mtime 变化 ⇒ **确实重建** ⇒ 不是漏 warning、不是增量跳过。
**再查历史**：`git log --all -S'HmiWarningDeep'` 只查到 **2 个提交、全是引用、零处定义** ⇒ 幽灵键不是迁移丢的。

**真缺口**：`App.axaml` 的「按钮状态色（深底）」家族有 `SuccessDeep` / `NeutralDeep` / `DangerDeep`，
**没有 Warning 成员**，而 3 处引用（`CommunicationConfigView.axaml:71`、`:257`、`VariableManagerView.axaml:352`）
的用法正是这一族的用法（`Background=… Foreground=HmiTextPrimaryBrush`）⇒ 补
`HmiWarningDeep = #92400e`（amber-800）+ `HmiWarningDeepBrush`，**2 行、三个调用点零改动**。
否决"改指既有 `HmiWarningBrush`（#f59e0b）"：那对 `HmiTextPrimaryBrush` 的对比度只有 **≈1.7:1**，
而 Deep 家族的取值逻辑是"与文字色高对比"（`#92400e` ⇒ **≈5.8:1**，且与 `HmiSuccessDeep` 的 emerald-800 同一步长）。

### 判据（4 条，`tests/HmiEditor.App.UiTests/XamlResourceKeyClosureTests.cs`）

| # | 判据 | 类型 | 守什么 |
|---|---|---|---|
| 1 | `Scanner_SeesBothSides` | 面自检 | `axaml ≥45` / `cs ≥110` / 引用键 `≥120` / 定义键 `≥170` / 代码侧引用 `≥3` —— 面变窄时下面两条变空跑 |
| 2 | `RuntimeLookup_IsUsableAsEvidence` | 证据腿自检 | 已知键必须解析到、**幽灵键必须解析不到**（后者恒真 ⇒ 第 3 条是假门） |
| 3 | `EveryReferencedResourceKey_IsDefinedOrResolvable` | 主判据 | 每个引用键须满足「静态已定义 ∪ 运行期可解析」，否则点名 `键 ← 文件:行(面)` |
| 4 | `PhantomKeyVictimButtons_ActuallyCarryTheirBackground` | 行为 | 3 个受害控件的 `Background` 真的是那个画刷（读真实控件属性，失败消息带实测值） |

**两条腿**：静态 `x:Key`（含 `Assets/Icons.axaml`）兜住视图本地资源字典；
运行期 `Application.Current.Resources.TryGetResource`（与产品 `App/Theme.cs` 的 `Theme.Brush` **同源**）
兜住 Dock 预设（NuGet 包，静态面外）。**引用侧覆盖** `.axaml` 的 `{StaticResource|DynamicResource}`
与 `.cs` 的 `Resources["…"]` / `FindResource` / `TryFindResource`。

**判据先剥注释**：`SourceScanner.StripXmlComments` 从 `LocalizedFormatCallSiteTests` 里的私有正则
抽成唯一实现，并改成**保长度 + 保行数**（旧实现把注释里的换行也换成空格 ⇒ 行数塌掉）。
不剥注释的话 `App.axaml:20` 注释里的示例键 `IconXxx` 会被报成"引用了从未定义"的**假缺口**（本轮实测踩到）。

### 运行期两种形态（都实测，别再笼统说"静默"）

| 缺失落在哪 | 表现 | 证据 |
|---|---|---|
| **控件的样式属性**（`Background="{StaticResource X}"`） | **静默跳过赋值** ⇒ 回落主题默认。三个按钮实测全是 `#0FFFFFFF`（Fluent 暗色默认按钮底），**不抛、不报，只是颜色错了** | N8（复现修复前状态）失败消息带实测值 |
| **资源字典内对象的属性**（`HmiWarningDeepBrush.Color="{StaticResource HmiWarningDeep}"`） | 解析被**推迟**，字典里"看着有值"；谁**读那个属性**谁才吃到 `KeyNotFoundException : Static resource 'HmiWarningDeep' not found.` | N2 |

两者**编译期都完全不可见**。阶段 46 的 N3（`MainWindow.axaml` 的 `CommandBar.Background` 写错 ⇒ 三判据全绿）
属**形态 1**。

### 负控（8/8，逐条断言"只红这些"）

| 注入 | 期望 | 实测 |
|---|---|---|
| N1 `AlarmListView.axaml` 引用幽灵键 | 主判据 | ✅ 点名 `HmiGhostBrushZZZ ← Views/AlarmListView.axaml:94` |
| N2 `App.axaml` 改 `x:Key="HmiWarningDeep"` | 主判据 + 行为 | ✅ 两份都点名 |
| N3 **单行**注释里放引用 | **∅（必须仍绿）** | ✅ ∅ |
| N4 **跨行**注释里放引用 | **∅（必须仍绿）** | ✅ ∅（按行剥注释会在这里假红） |
| N5 `.cs` 的 `Resources["…"]` 换幽灵键 | 主判据 | ✅ 点名 `…ProjectSettingsView.axaml.cs:298(cs)` |
| N6 换成**存在但语义错**的画刷 | **只**行为判据 | ✅ `Background=#ff1a2332 ≠ #ff92400e` |
| N7 塞一个与幽灵探针同名的键 | **只**第 2 条 | ✅ 证明"解析不到"不是空断言 |
| N8 复现修复前状态 | 主判据 + 行为 | ✅ 见上表 `#0FFFFFFF` |

还原后 4 个被动过的文件 `sha256` 逐字节一致、复跑全绿。

### 读数

| 项 | 值 |
|---|---|
| 扫描面 | **47** 个 `.axaml` / **121** 个 `.cs`；引用点 **2792**（xaml 2788 / cs 4）、去重键 **136**、定义键 **185** |
| 产品修复 | `App.axaml` **+2 行** |
| `App.UiTests` | 270 → **274** |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**；八程序集合计 **2959**（+8 单独段 = **2967**），比阶段 46 的 2963 **净增 4**；构建 **0 警告 / 0 错误** |
| 门禁日志 | `.workbuddy/tmp/ci-gate-2026-09-20-reskeys.log`，mtime **`20:57:08`** ⇒ 晚于提交 `f0cda82`（**20:56:07**）**61 秒** |

### 仍未闭环

① 只查"键能不能解析"，不查"解析出来的**类型对不对**"（`{StaticResource X}` 取到 `Color` 而属性要 `IBrush`，
同样静默）；② ~~**26 个 `ChromeWindow`（`: Window`）视图不在 `ViewSmokeUiTests` 的清单口径内**~~ ⇒
✅ **已闭环（2026-09-20，阶段 48）**：实测是 **28** 个（登记里的 26 来自
`grep "class .*: *ChromeWindow"`，漏掉经泛型基类 `AiGenerateDialogBase<TResult> : ChromeWindow` 的
`AiTemplateDialog`/`AiScriptDialog`）—— 28 个 + `SplashWindow` + 3 个嵌套对话框 = **32** 个窗口
全部进网，详见下节；那层注释"接得住 XAML 里 `{StaticResource}` 名字写错"的说法也已更正
（它接得住"缺模板 ⇒ 布局期抛"，接不住"缺 `Brush` ⇒ **静默**"）；
③ "定义但从未被引用" **50** 个键只做了反向对照，未见得是死键（可能有动态引用路径），未处理；
④ 运行期那条腿的前提（App 资源已加载）只在 headless 宿主里被验证过。


## 阶段 48（窗口视图冒烟网：32 个窗口进网，首跑抓到 3 个 NRE + 1 个面外类型）— 2026-09-20

阶段 47 遗留 ② 的收口。专题 `docs/2026-09-20-window-view-smoke-net.md`；提交 `8e9add4`。

### 做了什么

**先纠正登记里的数字**：`grep "class .*: *ChromeWindow"` 漏掉经泛型基类继承的
（`AiGenerateDialogBase<TResult> : ChromeWindow` 里的 `\w+` 撞上 `<TResult>` 就匹配不上）
⇒ 按类型关系反射数是 **28** 不是 26。**面从"按名字 grep"改成"按类型关系反射"**，
同一个错法在计数与取面上是同一个错。

**新增 `WindowViewSmokeUiTests`（34 条）**：面 = `HmiEditor.App.Views` 下非抽象、非开放泛型、
是 `Window`、有无参构造 —— 实测 **32** 个（28 个 `ChromeWindow` 子类 + `SplashWindow`
+ 3 个代码里造的 private 嵌套对话框）。逐窗口构造 → `Show()` → 布局 → `Close()`；
`ChromeWindow` 专属再判**外壳契约**（`Content is Border` & `Child is Grid` & 根 Grid 行数 ≥ 2）
与**八向缩放手柄 = 8**。

**新增面覆盖守卫 `EveryViewType_IsInSomeSmokeSurface`**（本轮新增的守卫形状）：
把本层与 `ViewSmokeUiTests` 的 UserControl 面**并起来**，要求收走 `Views` 下**所有**
可构造的 `Control` 派生类 + 空集守卫（`≥48`，防命名空间改名 ⇒ 差集恒真）。
**首跑就抓到第三类视图形状**：`TrendChartPanel`（`internal class … : Control` 的自绘趋势面板，
两个面都不收），进豁免表 `ReachedByAHost`，并由宿主 `TrendConfigView` 的用例**作证**
它真的出现在可视树里（豁免不是注释）。

### 首跑抓到的真缺陷（取证在修复之前；首跑 30 通过 / 4 失败）

| # | 缺陷 | 根因 | 修法 |
|---|---|---|---|
| 1 | **`DeviceManagerView` 生产路径构造即崩**（P1） | XAML 里 `CmbChannelType` 的 `SelectedIndex="0"` 在 `InitializeComponent()` **期间**就同步触发 `OnChannelTypeChanged`，而它读的 `SerialPanel`/`BaudPanel`/`TcpPanel` 声明在**后面**、那一刻全是 `null` ⇒ NRE 从 `new DeviceManagerView()` 抛出，`Wiring.Panels.cs` 的 `ShowDialog` 走不到 ⇒「设备管理」点了没反应。**无 try/catch 兜底，全仓没有任何判据构造过这个窗口**；`git log -S` 显示该行自 `2adb1f8`（2026-08-11）起就在 ⇒ 长期存在，不是回归 | 处理器加 null 前置检查 + 抽 `ApplyChannelPanels()` 供构造期落初始状态（不再依赖"XAML 默认值碰巧对得上"） |
| 2 | `AlarmListView.OnClosed` NRE | 无参构造是「仅用于 XAML 设计器」那条（`_engine = null!`），退订代码直接解引用 ⇒ 关掉设计器预览即崩 | 退订加空值前置 |
| 3 | `SimulatorView.OnClosed` NRE | 同上（两个模拟器 + 两个 `DispatcherTimer` 全 `null!`） | 收尾加空值前置 |
| 4 | `TrendChartPanel` 不在任何面里 | 两个面各自守自己的清单，谁都不问「还有没有第三类」 | 进 `ReachedByAHost` + 宿主用例作证 |

> 缺陷 1/2/3 的注入点后来成了负控 N1/N2/N3（**拿真实缺陷当负控**，取证在修复之前）。

### 本层**不**抓什么（写进类注释，避免重犯阶段 47 的高估）

`{StaticResource}` 键缺失不是本层的职责 —— 阶段 47 已实测它是**静默**的，构造/布局都不抛。
**负控 N10 就是这条边界的证据**：往 `VariableImportView.axaml` 的按钮上注入
`BorderBrush="{StaticResource NoSuchBorderBrushInjected}"` ⇒ 本层 **34 全绿**、
静态层 `XamlResourceKeyClosureTests` **红 1 条**。两层的分工不是"更保险"，是各自可判的那一类不同。

### 负控（10/10，逐条断言"红哪些"）

| 注入 | 期望 | 实测 |
|---|---|---|
| N1 / N2 / N3 复原上述三个真缺陷 | 各红 1 条并点名该窗口 | ✅ 3/3 |
| N4 `CommunicationConfigView` 根 Grid `36,*,Auto → 36` | 只红行数那条 | ✅ 1 条 |
| N5 `VariableImportView` 外层多包一层 `Grid` | 只红外壳那条 | ✅ 1 条（消息带 `Content=Grid`） |
| N6 删两个角手柄 `AddGrip` | 只红手柄那条，**且恰好 28 条** | ✅ **28 失败 / 6 通过** |
| N7 面口径 `Window → ChromeWindow` | 两条守卫红、用例 34→30 | ✅ |
| N8 豁免键名拼错 | orphan + 作证红 | ✅ 2 条 |
| N9 豁免宿主填错 | 宿主用例的可视树断言红 | ✅ 1 条 |
| N10 **必须仍绿**：幽灵 `{StaticResource}` | 本层 ∅ | ✅ 绿；对照静态层红 |

**N6 最有说服力**：28 恰好等于面里 `ChromeWindow` 的个数 ⇒ 同时证明手柄判据对**每一个**
`ChromeWindow` 生效、对 4 个非 `ChromeWindow` 窗口不生效。
脚本自身踩的坑也记进专题：第一版用 `replace(new, old, 1)` **反向还原**，
N1/N6 的 `new` 是空串 ⇒ `replace("", old, 1)` 把 `old` 插到文件开头、写坏源码，
后续 9 条全部"编译失败"；改成**写回注入前的字节快照**后 10/10 通过。

### 顺带重构（唯一实现）

`MainWindowConstructionTests` 的 55 行私有 `Sandbox` 抽成共用 `internal sealed class DataRootSandbox`
（带 `prefix`）。窗口的 `Opened`/`Closing` 会经 `WindowGeometryService` 读写 `windows.json`，
两个消费者各抄一份等于把「先还原环境变量、再删目录」这条顺序约定抄两遍。

### 读数

| 项 | 值 |
|---|---|
| 面 | 窗口 **32** / UserControl **15** / 并集人口 **48** |
| 产品修复 | 3 个文件 `+52 / −15` |
| `App.UiTests` | 274 → **308**（+34） |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**；八程序集合计 **2993**（+8 单独段 = **3001**），比阶段 47 的 2967 **净增 34**；构建 **0 警告 / 0 错误** |
| 门禁日志 | `.workbuddy/tmp/ci-gate-2026-09-20-winview.log`，mtime **`21:43:05`** ⇒ 晚于提交 `8e9add4`（**`21:42:08`**）**57 秒** |

### 仍未闭环

① 本层走**无参构造** ⇒ 5 个类型（`AiTemplateDialog`/`AiScriptDialog`/`SplashWindow`/
`AlarmListView`/`SimulatorView`）走的是**设计器路径**，**事件接线仍未覆盖**
（要覆盖"生产构造 + 依赖注入"那一层，得为 32 个窗口各准备服务对象）；
② 3 个嵌套对话框不保证宿主真的用了它们；③ `SplashWindow` 只判"能构造 + 能布局 + 有具名控件"；
④ 阶段 47 的三条遗留原样留着。


## 阶段 49（菜单点击行为判据：62 个具名菜单项全部归类，48 个真点）— 2026-09-20

登记表 **#37 遗留①**（“其余 60 余个菜单项只有静态绊线”）的正面回答。专题
`docs/2026-09-20-menu-invocation-net.md`；提交 `c2aa5f6`。**产品代码改动 0 行**。

### 做了什么

**先探针后判据**（一次性探针，跑完删）—— 三个问题的实测答案决定了判据形状：

| 问题 | 答案 | 意义 |
|---|---|---|
| `ShowDialog(this)` 会阻塞 UI 线程吗 | **不阻塞**（8 次点击 17–87 ms 立即返回） | 可以“点一下再看” |
| 打开的窗口进得了 `owner.OwnedWindows` 吗 | **进**，8/8 次 `0 → 1` 类型正确、`IsVisible=True`，`Close()` 后掉回 0 | **判据的锚点** |
| `ApplicationLifetime` 可用吗 | **不可用**（headless 里为 `null`）⇒ 无全局窗口清单 | 但上面那条说明不需要它 |

**新增 `MainWindowMenuInvocationUiTests`（3 条）**：把 62 个具名菜单项**全部**分到恰好一个桶，
逐项真点，断言 `OwnedWindows` 的**增量与类型**。

| 桶 | 数 | 断言 |
|---|---|---|
| 弹窗 | 18 | 增量**恰好 1**，新窗口类型 == 期望类型、`IsVisible` |
| 静默 | 28 | 增量**恰好 0** |
| 条件 | 2 | 增量 ∈ {0,1}，为 1 时类型须相符（**理由必填**） |
| 跳过 | 14 | 不点（起外部进程 / 读写工程文件 / 关主窗口），**理由必填** |

**条件桶为什么不能省**：`MenuCreateTemplate`（无选中就 return）与 `MenuFaceplateEditor`
（依赖模板集合非空）的读数**依赖初始状态** —— 硬塞进“弹窗”桶会得到一条哪天预置模板被清空
就假红的断言，硬塞进“静默”桶则把“它其实会弹窗”记丢了。

**三条判据各有守卫**：① 面守卫走**双向差集**（窗口有表里没有 ⇒ 新菜单项没分类，强制判断；
表里有窗口没有 ⇒ 菜单项被删/改名后表腐烂），同阶段 48 的全集守卫形状；
② 空集守卫（表 ≥ 60、弹窗 ≥ 18、跳过 ≥ 12）+ 表卫生（条件/跳过桶理由必填）；
③ 行为主判据一条走完 48 个（逐条 `Theory` 建窗口会把开销抬到几十秒，实测整条 2 s），
并覆盖三个边界：一次点击弹 > 1 个窗口、关闭后 `OwnedWindows` 没回落、点击把主窗口关掉。

> 理由长度的门槛**只对条件桶与跳过桶**生效 —— 弹窗/静默桶的行为已被精确断言，理由只是注释；
> 对它们设门槛只会逼出凑字数（首跑就撞上：`MenuSaveProject` 的“保存工程：写磁盘”8 字符被卡）。

### 负控（8/8，含 1 条正控）

| 注入 | 期望 | 实测 |
|---|---|---|
| N1 复原 `DeviceManagerView` 构造期 NRE | 红并点名 `MenuDeviceManager` | ✅ 红 1 条 |
| N2 **摘掉** `MenuDeviceManager` 的接线 | 行为条红，面守卫**仍绿** | ✅ 红 1 条；面守卫绿 |
| N3 接错处理器（改接 `AlarmConfig` 的） | 红并点名「弹出的窗口类型是 `AlarmConfigView`」 | ✅ 红 1 条 |
| N4 让静默项 `MenuThemeDark` 弹窗 | 静默桶增量 ≠ 0 被抓 | ✅ 红 1 条 |
| N5 分类表键改错一个字母 | 面守卫**双向**点名 | ✅ 红 2 条 |
| N6 **正控**：人口下界 60 → 200 | 红 | ✅ 红 1 条 |
| N7 只改一行注释 | **3/3 全绿** | ✅ 全绿 |
| N8 把静默项 `MenuUndo` 标成弹窗 | 红（判定方向也生效） | ✅ 红 1 条 |

还原后 4 个文件 `sha256` **逐字节一致**。**N2 与 N3 是最有价值的两条**：N2 证明面守卫与
行为判据**互不替代**（摘掉接线后菜单项还在树上，只有真点才看得出），N3 就是“接了**正确的**
处理器”那条判据 —— 它**不需要任何源码解析**，期望类型不符即红。

> **脚本自身两个坑**（都在负控首跑暴露，判据本身是对的）：N6 的命中词没跟着注入改的文案走；
> N7 的 `failed=-1` 是因为 **xUnit 全绿时不打印「失败数」那一行**。不看输出直接采纳“6/8”
> 会把这两个判读缺陷误判成两个真问题。

### 读数

| 指标 | 值 |
|---|---|
| 新增用例 | **3**（`App.UiTests` 308 → **311**） |
| 八程序集合计 | 3001 → **3004** |
| 构建 | **0 警告 / 0 错误** |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**，日志 `.workbuddy/tmp/ci-gate-2026-09-20-menunet.log` mtime **`22:15:49`** ⇒ 晚于提交 `c2aa5f6`（**`22:14:36`**）**73 秒** |
| 产品修复 | **0 行** —— 判据建起来就全绿（阶段 48 修掉的三个缺陷之后，接线本身是好的） |

### 仍未闭环

① 工具栏与面板内的 **`Btn*` 面未覆盖**（65 个具名按钮，其中 10 个是弹窗入口，形状同族可直接套）；
② **跳过桶的 14 项无行为判据**（需要 `StorageProvider` 桩 + 构建服务桩，不是判据形状问题）；
③ 条件桶两项**依赖初始状态**（要变无条件需先在画布放控件）；④ 不做**真实命中测试** ⇒
菜单可见性/展开/快捷键不在面内；⑤ 不判关窗后**资源释放是否干净**。

### 沙箱残留（✅ 已闭环 2026-09-22，阶段 74）

一次性探针 `tests/HmiEditor.App.UiTests/_ZZMenuInvocationProbe.cs` 当时删不掉：沙箱把会话内
新出现的路径做成**逐文件 bind-mount**（`/proc/mounts` 里能看到那条），`rm`/`os.rename` 报
`EBUSY`、`umount` 反说“未挂载”；safe-delete shim 对**文件**目标的回收站路径也算错
（`<file>/.Trash-0` ⇒ `NotADirectory` ⇒ fail-closed）。当时的降级处置是改写成**无 `[Fact]`**
的说明性文件（对测试发现与门禁惰性）并在 `.gitignore` 加精确规则挡住。

**阶段 74 已收口**：文件本身 + `.gitignore` 里那 3 行注释与 1 行规则均已删除。
**⚠ 定性更正**：那段「删不掉」是**当时会话内**的事实、**不是永久残留** —— 后续会话复查
`/proc/mounts` 该条目已为 0，`os.remove` 直接成功 ⇒ 生命周期 = 产生它的那个会话，
跨会话自动消失，**无需人工介入**。原文件内容早已落到 `MainWindowMenuInvocationUiTests`。


## 阶段 50（宿主对话框内部按钮：57 个具名按钮全部归类，46 个真点）— 2026-09-20

登记表 **#39** 的「**事件接线一列仍未覆盖**」那一半。专题
`docs/2026-09-20-dialog-inner-invocation-net.md`；提交 `ddeb131`。**产品代码改动 0 行**。

### 做了什么

三层网之间正好漏了一段：阶段 48 的窗口冒烟网证明「32 个窗口打得开」（但走**设计器构造路径**，
不接 Click）；阶段 49 的菜单判据证明「点菜单 → 宿主被弹出来」。**宿主打开之后**里面几十个按钮
的接线没人点过。本层补的正是这一段：

```
主窗口菜单 RaiseEvent → 宿主出现在 window.OwnedWindows（阶段 49 已验证同步且不阻塞）
  → 枚举宿主内具名 Button（按名字去重 + 断言同名必同实例）
  → 按**表里声明的顺序**逐个 RaiseEvent(Button.ClickEvent)
  → 断言**宿主自身** OwnedWindows 的增量与类型
```

**新增 `DialogInnerInvocationUiTests`（7 条 = 6 个宿主各一条 `Theory` + 1 条表卫生 `Fact`）**，
整条 **2 秒**。

| 宿主 | 菜单 | 具名按钮 | 真点 | 弹窗 |
|---|---|---|---|---|
| `RecipeEditorView` | `MenuRecipeEditor` | 13 | 12 | `BtnAddGroup` → `TextInputDialog` |
| `UserManagerView` | `MenuUserManager` | 9 | 8 | — |
| `StyleEditorView` | `MenuStyleEditor` | 1 | **0** | — |
| `ScriptEditorView` | `MenuScriptEditor` | 13 | 11 | `BtnAiGenerate` → `AiScriptDialog` |
| `DeviceManagerView` | `MenuDeviceManager` | 15 | 10 | — |
| `AnimationEditorView` | `MenuAnimationEditor` | 6 | 5 | `BtnDeleteAnimation` → `ConfirmDeleteDialog` |
| **合计** | | **57** | **46** | **3** |

桶：**弹窗 3 / 静默 43 / 不点 11**（不点必填理由 ≥10 字符）。面走**双向差集**
（表里有宿主没有 ⇒ 控件被删改名；宿主有表里没有 ⇒ 新增按钮没分类），另加空集守卫、
理由门槛（**只对不点桶**，弹窗/静默的行为已被精确断言）、「同名必须是同一实例」、
「不点的名字必须在某个 `.axaml` 里真的出现」（防表腐烂）。

### 两处形状是**实测**出来的，不是设计出来的

| 现象 | 根因 | 处置 |
|---|---|---|
| 首跑 `AnimationEditorView.BtnDeleteAnimation` 报「预期弹 1 个，实际增量 0」（探针里是 +1） | 首跑按**字母序**点 ⇒ 它成了第一个被点的；而 `OnDeleteAnimation` 首行是 `if (_currentAnimation == null) return;` | **把顺序钉进表里**：`BtnNewAnimation` 排在它之前 ⇒ 变回精确断言「必须弹 1 个 `ConfirmDeleteDialog`」。**数组顺序 = 被测语义** |
| `StyleEditorView` 只有 **1** 个具名控件（`BtnTitleClose`，且是关窗）⇒ `clicked == 0` | 该视图内容**全部在 code-behind 里构建**、没有 `x:Name`；首跑被「面自检 ≥3」判成假红 | 进 `NoClickableHosts` 并写明理由（且断言「该宿主按钮必须全是 `Never`」）；**不悄悄移出清单、不放宽下界** |

**没单列「条件桶」**：本层每宿主都在固定初始状态 + 固定顺序下点击，唯一沾条件的
`BtnDeleteAnimation` 已被顺序钉死成弹窗桶；加一个没人用的桶只会多一条恒真的守卫。

### 负控（10/10，含 2 条正控 + 1 条不误报）

| 注入 | 打哪条断言 | 实测 |
|---|---|---|
| N1 摘掉 `BtnAddGroup` 接线 | 弹窗桶增量必须为 1 | ✅ 红 1 条；**面守卫仍绿** |
| N2 把 `BtnAddGroup` 接到「弹一个裸 `Window`」 | 类型必须相符 | ✅ 红 1 条，点名实际类型 |
| N3 让 `BtnPlayPreview` 弹窗 | 静默桶增量必须为 0 | ✅ 红 1 条 |
| N4 **交换** `BtnNewAnimation`/`BtnDeleteAnimation` 顺序 | 顺序语义 | ✅ 红 1 条 |
| N5 分类表里把一个按钮改名 | 面差集 missing | ✅ 红 1 条（missing + extra 同时点名） |
| N10 产品 XAML **新增**一个具名按钮 | 面差集 extra | ✅ 红 1 条 |
| N9 `Never` 桶里名字改错 | 防表腐烂 | ✅ 红 2 条 |
| N6 **正控**：`clicked < 1` 下界抬到 99 | 恒真守卫 | ✅ 红 **5** 条（除 `StyleEditorView` 外全部宿主） |
| N7 **正控**：`NoClickableHosts` 理由改到 10 字符以下 | 理由门槛 | ✅ 红 2 条 |
| N8 只改一行注释 | — | ✅ **7/7 全绿** |

还原后 4 个文件 `sha256` **逐字节一致**。**N1 与 N2 与阶段 49 同构且有同样价值**：
N1 摘掉接线后按钮**还在逻辑树上** ⇒ 面守卫完全看不见，只有真点才抓得到（两层互不替代）；
N2 就是「接了**正确的**处理器」那条判据 —— 期望类型不符即红，**不需要任何源码解析**。

> **脚本自身两处缺陷**（都在负控首跑暴露，判据本身是对的）：N5 的注入设计错 —— 改 `.axaml` 的
> `x:Name` 会打断 code-behind 对生成字段的引用 ⇒ 编译失败被判「不成立」；N7 的 needle
> **凭记忆写**（实际消息是 `的「没有可点按钮」理由太短`，中间隔着字）。
> **同一个教训第三次出现** ⇒ 已给脚本加「逐条保存注入后的完整输出」到
> `.workbuddy/tmp/nc-inner/<ID>.log`。不看输出直接采纳「7/9」会把两个脚本缺陷当成两个判据缺陷。

### 顺带关闭的一条

`AnimationEditorView` 的 private 嵌套 `ConfirmDeleteDialog`：登记表 #39 里写着
「**不保证宿主视图真的用了它们**」—— 本层点 `BtnDeleteAnimation` 弹出的**就是**它，
对 3 个嵌套对话框中的这 1 个给出了正面证据（`RecipeEditorView` 的 `TextInputDialog` 同理）。

### 读数

| 指标 | 值 |
|---|---|
| 新增用例 | **7**（`App.UiTests` 311 → **318**） |
| 八程序集合计 | 3004 → **3011**（净增 **7**，纯净增） |
| 面 | **57** 个具名按钮 / **46** 个真点 / 3 弹窗 / 43 静默 / 11 不点 |
| 负控 | **10/10**，还原 4 个文件 `sha256` 逐字节一致 |
| 构建 | **0 警告 / 0 错误** |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**，日志 `.workbuddy/tmp/ci-gate-2026-09-20-dialoginner.log` mtime **`23:50:28`** ⇒ 晚于提交 `ddeb131`（**`23:49:06`**）**82 秒** |
| 产品修复 | **0 行** —— 判据建起来就全绿 |

### 仍未闭环

① **阶段 49 遗留①（工具栏 / 停靠面板内的 `Btn*`）仍未覆盖** —— 那是主窗口里停靠的
**面板**，与本层的「宿主对话框内部」是**两个不同的面**，本层**不等于**收掉了那一项；
② 其余 **26 个窗口**的事件接线未覆盖（本层只覆盖「能从菜单用生产路径打开、且内部有具名按钮」
的 6 个宿主）；③ **造状态能力缺失**（选中一行 / 填一个输入框再确认）⇒ 只能覆盖「不弹」，
覆盖不到「弹什么」；④ 4 个依赖 `StorageProvider` 桩的按钮只覆盖到「不弹」。

### 新登记（环境 / 平台）

登记表 **#40**：**headless 下 `ScrollViewer` + `TextWrapping.Wrap` 的 `TextBlock` 会让布局
不收敛、testhost 挂死**（`ScriptEditorView.BtnHelp` ⇒ `ShowLuaHelp()`）。阶梯剥离已定位到
「一包 `ScrollViewer` 即挂」（裸 `TextBlock` / ＋`Wrap` / ＋`LineHeight` 全过），
排除文本长度 / 字体回退链 / owner 类型三条嫌疑；`EXIT=124` ⇒ 是 hang 不是 OOM。
`BtnHelp` 因此进 `Never` 桶并写明取证现场。


## 阶段 51（主窗口工具栏 `Btn*`：65 个具名按钮全部归类，58 个真点）— 2026-09-21

阶段 49 遗留① 里**主窗口自己那 65 个工具栏按钮**那一半（阶段 50 §仍未闭环 ① 点名的那项）。
专题 `docs/2026-09-21-mainwindow-toolbar-invocation-net.md`；提交 `90ad636`。
**产品代码改动 0 行** —— 判据建起来就全绿。

### 做了什么

阶段 46 的静态绊线只问"名字有没有被碰过"，不问"接的是不是正确的处理器"。
而工具栏里**有 10 个与菜单项共用同一个处理器委托**（`BtnUserManager.Click += openUserManager`
与 `MenuUserManager.Click += openUserManager` 是**同一个 lambda**）⇒ 接到别的窗口上、
或干脆摘掉，静态绊线照样绿。

```
构造 MainWindow（生产构造路径）→ 按表里名字逐个 FindControl 解析实例
  → 按表里**声明的顺序**逐个 RaiseEvent(Button.ClickEvent)
  → 断言主窗口 Window.OwnedWindows 的**增量与类型**
```

**新增 `MainWindowToolbarInvocationUiTests`（3 条 = 行为主判据 + XAML 声明面 + 桶表卫生）**，
整条 **2 秒**。桶：**弹窗 11 / 静默 47 / 不点 7**，真点 **58**。

| 组 | 个数 | 桶 |
|---|---|---|
| 多选对齐/尺寸/组合 | 11 | 静默（无选中即 no-op，且初始 `IsVisible=false`） |
| 剪贴板/删除 | 4 | 静默 |
| 旋转/镜像/复位 | 5 | 静默 |
| 撤销/重做 | 2 | 静默 |
| 绘图工具 `BtnTool*`（`ToggleButton`） | 7 | 静默 |
| 缩放 | 2 | 静默 |
| **弹窗入口** | **11** | **弹窗**（前 10 个与菜单共用处理器；`BtnSettings` 工具栏独有） |
| 起进程 / 写盘 / 换工程 / 推设备 | 7 | **不点**（理由必填） |
| 改状态（14 个 `BtnQuick*` + `BtnNewScreen` + `BtnReloadPanels`） | 16 | 静默（排在最后） |

### 核心发现：面选错了，不是表写错了

首跑用「遍历逻辑树」收面 ⇒ 65 里只找到 **21**，报「44 个按钮找不到」。
用 `.workbuddy/tmp/btn-ancestry.py` 解析 65 个的祖先链立刻定性：

| 直接父元素 | 元素类型 | 个数 | 在逻辑树上？ |
|---|---|---|---|
| `CommandBar.PrimaryCommands` | `CommandBarButton` | **44** | **否** |
| `StackPanel` | `Button` | 14 | 是 |
| `StackPanel` | `ToggleButton` | 7 | 是 |

`CommandBar.PrimaryCommands` 是**集合属性**，不是 `LogicalChildren`；Avalonia 只在控件模板里
把它渲染成可视子级。而 `x:Name` 是 XAML 加载时按**词法作用域**注册进窗口 NameScope 的，
**与渲染无关** ⇒ 主面改成按名 `FindControl`。

**这解释了为什么"声明面"那条独立判据必须留着**：首跑时它是**绿的**，
正是它把"我表写错了"排除掉、定性成"面选错了"。

### 三个面的实测覆盖（同一份日志）

| 面 | 取法 | 实测 | 角色 |
|---|---|---|---|
| **NameScope** | `window.FindControl<Control>(name)` | **65 / 65** | **主路**（真正拿去点的面） |
| XAML 声明 | `MainWindow.axaml` 正则 `x:Name="Btn…"` | 65 | 守**登记完整性**（双向差集） |
| 逻辑树 | `GetLogicalDescendants()` | **21** | 只作读数 |
| 可视树 | `GetVisualDescendants()` | **32** | 只作读数 |

44 个缺口的去向（把主面换回逻辑树后逐个标注）：**11 个**「只有可视树里有」=
`BtnUndo`/`BtnRedo`/`BtnZoomIn/Out`/`BtnNewProject`/`BtnOpenProject`/`BtnSaveProject`/
`BtnExportCode`/`BtnCompile`/`BtnBuildRun`/`BtnDownloadHmi` ⇒ 1024×600 下渲染出来了；
**33 个**「两个树里都没有」（含 `BtnUserManager`、`BtnReloadPanels`、对齐/剪贴板/变换整组）。
**机制只到"未验证"**（推测是 `CommandBar` 溢出收纳区未展开时不生成内容）；
本层**刻意不区分**"未实例化"与"挂在独立弹层根上"—— 按名取实例不依赖树，这正是选 NameScope 的收益。

### 负控 8/8（6 红 + 2 条**期望绿**的对照）

| 注入 | 期望 | 实测 |
|---|---|---|
| T1 摘掉 `BtnUserManager` 接线 | 红 | ✅ 点名「预期弹出 1 个 UserManagerView，实际增量 0」 |
| T2 把它接到 `SettingsDialog` | 红 | ✅ 点名「弹出的窗口类型是 SettingsDialog」 |
| **T3 交换 `BtnCopy`/`BtnPaste` 顺序** | **绿** | ✅ 全绿（钉住本层**观测边界**，见下） |
| T4 表里多登记一个不存在的名字 | 红 | ✅ 主面 missing + **声明面同时红** |
| T5 `found`/`clicked`/表长 三处下界抬到 99 | 红 | ✅ 逼出 **65 / 58 / 65** 与完整读数 |
| T6 XAML **新增**一个具名按钮 | 红 | ✅ 主面 extra + **声明面同时红** |
| T7 主面换回遍历逻辑树 | 红 | ✅ 44 个点名并标注各自在哪棵树 |
| T8 只改一行注释 | **绿** | ✅ 全绿 |

还原后 4 个文件 `sha256` 逐字节一致。**T1** 摘掉接线后按钮**还在**（NameScope 照样解析得到）
⇒ 只有真点才抓得到；**T2** 就是"接了**正确的**处理器"那条判据，**不需要任何源码解析**。

**T3 的价值是钉边界**：交换 `Copy`/`Paste` 后内部状态确实变了（`Paste` 先跑、剪贴板空），
但本层锚点是 `OwnedWindows` ⇒ **本层管接线、不管状态**。阶段 50 宿主层顺序敏感、
本层不敏感 —— 这个差别是**量出来的**，写进专题免得后人以为本层覆盖了顺序。

### 一条方法学缺陷被这条正控逼出来

`Assert.True(cond, message)` 的 message **只在红的时候打印** ⇒ 判据全绿时
「面解析出几个、真点了几个」**拿不到**，首跑绿了之后那只是推断、不是证据。
T5 把下界抬到 99 之后，失败消息里同时带出三个精确数字与逐按钮读数
（含「面：按名解析 65；逻辑树已渲染 21；可视树 32」）。
⇒ 已登记候选改进 **#42**（`ITestOutputHelper` + `--logger console;verbosity=detailed`）。

### 读数

| 指标 | 值 |
|---|---|
| 新增用例 | **3**（`App.UiTests` 318 → **321**） |
| 两段之和（全量） | 3011 → **3014**（净增 **3**，纯净增） |
| 面 | **65** 个具名按钮 / **58** 个真点 / 11 弹窗 / 47 静默 / 7 不点 |
| 负控 | **8/8**，还原 4 个文件 `sha256` 逐字节一致 |
| 构建 | **0 警告 / 0 错误** |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**，日志 `.workbuddy/tmp/ci-gate-2026-09-21-toolbar.log` mtime **`10:07:47`** ⇒ 晚于提交 `90ad636`（**`10:06:43`**）**64 秒** |
| 产品修复 | **0 行** —— 判据建起来就全绿 |

### 仍未闭环

① **停靠面板内部的 `Btn*` 仍未覆盖**（`PropertyPanelView` / `LayerPanelView` /
`FaceplateLibraryView` / `ProjectExplorerView` / `TagMonitorView` …）—— 那是"主窗口里停靠的
**面板**"，与"主窗口自己的工具栏"是**两个不同的面**，本层**不等于**收掉了阶段 49 遗留① 的全部；
② #42（绿判据无读数）；③ **造状态能力缺失** ⇒ 47 个静默只证明了"不弹窗"、
**没证明"做对了事"**（本层只覆盖了无选中那条 no-op 分支）；
④ `BtnSettings` 弹 `SettingsDialog`（14 处 `ScrollViewer`）**没有**触发 #40 的挂死 ——
这是「触发条件未定」的又一条反证，**不能**据此推出 #40 不存在。


## 阶段 52（停靠面板内部 `Btn*`：22 个具名按钮全部归类，18 个真点）— 2026-09-21

阶段 49 遗留① 里**主窗口里停靠的面板**那一半（阶段 51 §仍未闭环 ① 点名的那项，登记表 #41）。
专题 `docs/2026-09-21-dockpanel-invocation-net.md`；提交 `d1b2269` + `939ff37`。
**判据侧产品代码改动 0 行**，但顺带查出**两个真实缺陷**（#43 / #44，只登记未修）。

### 做了什么

面板是**第三种容器**：既不在主窗口的 NameScope 里（每个 `UserControl` 有自己的），
也不是"从菜单 `ShowDialog` 出来"的。取实例必须走 `panel.FindControl`，而"打开"面板的唯一
生产路径是点 12 个 `MenuPanelXxx` 菜单项（`DeferredContentPresenter` 惰性 ⇒ 默认布局只渲染 5/12）。

```
构造 MainWindow（生产构造路径）→ 点 12 个面板菜单项把面板全部 Build 出来
  → 按表里名字逐个 panel.FindControl 解析实例
  → 按桶逐个 RaiseEvent(Button.ClickEvent)
  → 断言主窗口 Window.OwnedWindows 的增量与类型 / 或"必然抛什么"
```

**新增 `DockPanelInvocationUiTests`（2 条 = 三段断言的主判据 + 桶表卫生）**，整条 **2 秒**。
桶：**弹窗 2 / 静默 15 / 已登记缺陷 1 / 不点 4**，真点 **18**。

| 桶 | 个数 | 判据 | 成员 |
|---|---|---|---|
| 弹窗 | 2 | 新增窗口里**含期望类型** | `Assets.BtnAiIcon`→`AiIconDialog`；`Faceplate.BtnAiTemplate`→`AiTemplateDialog` |
| 静默 | 15 | 无新窗口**且**无异常 | Layers 4 / Navigation 3 / Property 3 / ProjectExplorer 1 / Faceplate 3 / Assets 1 |
| 已登记缺陷 | 1 | **必然抛** `ArgumentNullException` | `Property.BtnAutoFix`（#44 的回归守卫） |
| 不点 | 4 | 只核对能解析到实例 | 3 个 `BtnImport*` + `BtnChooseScreenBgImage`（走 `IStorageProvider`，headless 下是平台桩） |

### 面取证（4 轮，全部实测）

| 轮 | 问题 | 读数 | 结论 |
|---|---|---|---|
| 1 | 默认布局几个面板可用？ | **5/12**；面板内 `Btn*` 仅 6 个；主窗口 `FindControl` **0 命中** | 面板有**独立 NameScope** |
| 2 | 生产路径能全打开吗？ | 点 12 个菜单项 ⇒ **12/12**；`Btn*` **22** 个；面板 `FindControl` **22/22**；**7 个不在可视树里** | 取实例走面板自己的 NameScope |
| 3 | 22 个点下去各做什么？ | **19 静默 / 1 抛 / 2 弹窗**；`AiIcon` +1、`AiTemplate` **+4** | 桶由实测定 |
| 4 | 4 从哪来？ | 反射数订阅者 + 框架语义实验 | **双接线(2) × 双路订阅(2) = 4** |

第 4 轮的框架语义实验（一次性探针，已删）直接在 Avalonia 12 上量：`Click += H` 两次 ⇒ H 执行
**2 次**；`Click += H` 与 `AddHandler(ClickEvent, H)` 混用同样 **2 次** ⇒ **Avalonia 不去重**。

### 顺带查出的两个真实缺陷（只登记，本轮不改）

- **#43 双接线**：`FaceplateLibraryView` 的 4 个按钮 XAML `Click=` 与 code-behind `.Click +=`
  同时接同一 handler ⇒ handler 执行 2 次；叠加"实例直订 + 静态聚合"双路订阅 ⇒
  `BtnAiTemplate` **点一次弹 4 个**对话框。全仓扫出 **8 处**（3 文件，逐个复核无假阳性）。
  对照 `Assets.BtnAiIcon`：路由结构相同但**单接线**，且有 `_suppressAiIconEvent` 防重入 ⇒ 只弹 1 个
  —— `MainWindow.axaml.cs:697` 的注释证明作者知道这个双路，只给 AiIcon 打了补丁。
- **#44 `BtnAutoFix` 必崩**：`PropertyPanelView.Ai.cs:120` 的 `_currentControl!` 压掉了编译期警告，
  null 照传给宿主，`MainWindow.axaml.cs:2307` 的 `TryGetValue(null)` 抛 `ArgumentNullException`。
  触发条件：有屏幕级分析结果（按钮可见）但未选中控件。

### 测试隔离约束（不写下来下一个人必踩）

首跑 4 条用例里 **2 条红**，红在**构造布局阶段**：`InvalidOperationException: … already has a
visual parent …`。根因不是被测代码 —— `ViewLocator._activeViews` 与 Dock layout 的 `Context`
都是**进程级**的，同进程里第二次「建窗 + 打开面板」会拿到**已挂在旧窗口树上**的视图实例。
阶段 51 的工具栏用例从不打开面板（惰性 ⇒ 面板根本没 Build）所以没撞上。
⇒ 本类**只有一条建窗用例**，三段断言共用它；用例开头 `ResetStaticDockBus()` 清空静态事件订阅。

### 负控 9/9

7 红 + 1 条**期望绿**的对照（只改注释）+ 1 条**期望红的产品注入**；逐条按 `sha256` 逐字节还原。
最值钱的是 **N9**：删掉那 4 行重复接线后，读数里 `BtnAiTemplate` 的弹窗数 **4 → 2**
—— 把"双接线是成因之一"从推断变成实测（剩下 2 个是双路订阅，即 #43 的另一半）。

### 测试那一步多了一段：本类单独跑（登记表 #45）

跑门禁才发现本类与大队**同进程**时**宿主僵死**：零输出、零托管异常、零 CPU，`--blame-hang` 的
原文是「指定的**非活动**时间(90 秒)已过」（VSTest 那句"测试主机进程崩溃"是宿主不响应时的**兜底
话术**，不是真崩）。取证三档：**全量 4/4 挂**（blame 报告**单独点名**本类）→ **同一命令剔掉它
321/321 绿 / 20 s** → **单独跑 3/3 绿**。

`scripts/ci-gate.sh` 测试那一步把它单独成段跑（与那 8 个 git 用例同形，**不放宽任何断言** —— 它照跑、
断言照旧）：**大队段 `App.UiTests` 321 + 新段 2 = 323**，三段之和 = 全量；**门禁步数不变（仍 7 步）**。
机制线索：headless 会话是**进程级单例、绑死在第一个会话的 UI 线程上**（另一个渲染工程就是为此走
独立 testhost 进程）。**根因未定** ⇒ 登记表 **#45**。

**落地后复跑门禁**（已提交的树，`fd288e5`）：**`GATE_EXIT=0` / 7 步全绿（步数不变）**；
大队段 `App.UiTests` **321**（23 s）+ 新段 **2**（1 s）= **323**；两段 git 用例 **8**（460 ms）。
日志 `.workbuddy/tmp/ci-gate-2026-09-21-dockpanel5.log`，mtime **`11:26:18`** 晚于提交
`fd288e5` 的 **`11:25:23`**（墙钟 50 s）。

### 仍未闭环

- **#43 / #44 待修**（判据已就位）。修 #43 后要同步改负控 N9（它注入的正是那 4 行）；
  修 #44 后要把 `BtnAutoFix` 从"已登记缺陷"桶移进静默桶（不移会红，红信息里有指引）。
- **#45 同进程僵死**：已用「独占进程」规避，**根因未定**（见上）。待查方向：并发用例对同一条
  UI 线程 / 渲染定时器的争用；根治可试"把一次打开的面板数降到 3~5 个"或"该段挪进独立 testhost"。
- 阶段 49 遗留②（跳过桶 14 项无行为判据）、#39 构造路径那半（其余 26 个窗口）、
  #42（绿判据无读数，现影响四层）—— 状态不变。
- 建立判据时踩到：`LangConstantUsageGuardTests` 是"名字即引用"扫描，**注释里的独立词也算** ——
  类注释写 `<c>MenuPanel*</c>` 会让未引用数 1267→1266（`MenuPanel` 恰好是同名 Lang 常量）。
  改注释而不是放宽基线，保住那条守卫的判别力。

## 阶段 53（修 #43 前半：12 处双接线全删 + 一个自带自检的静态守卫；顺带挖出 #46）— 2026-09-21

阶段 52 留下的 #43 是"判据已就位、待修"。本轮按"修了判据自己会说话"的顺序动手，
**阶段 52 的三处写法被实测推翻或修正**：

| 阶段 52 的写法 | 阶段 53 的实测 |
|---|---|
| 全仓 **8 处**双接线 | **12 处** —— 口径放宽到「任意 `x:Name` 控件的任意事件」后多出 4 处**非按钮**事件（`SearchBox.TextChanged` ×2、`TemplateListBox.SelectionChanged`、`CboActionTarget.SelectionChanged`）。两侧 handler **全部同名** ⇒ 删 code-behind 那半与保留 XAML 那半等价 |
| "去掉双接线后仍弹 2 个 ⇒ 给 AI 模板补**防重入**" | 那 2 个来自"视图转发 + 宿主直订"的**双路消费**，且**同样的问题还有 26 处**（⇒ 登记表 #46）；这条选了**删直订**而不是补标志 |
| "`Popup` 桶只判类型不判数量" | 双接线删掉后**数量是确定的** ⇒ 改为**判数量**（多一个窗口就指向多一路订阅） |

### 改了什么

- **删 12 处 code-behind 重复接线**：`AiChatPanelView.NewChatBtn`、`FaceplateLibraryView` 的
  `SearchBox` + `TemplateListBox` + 4 个按钮、`PropertyPanelView.CboActionTarget`、
  `ResourceLibraryView.SearchBox`、`SettingsDialog` 的 3 个主题按钮。
- **删 `Wiring.Faceplate.cs` 里 `FaceplatePanel.AiTemplateRequested` 的直订** ——
  视图构造函数已把该事件转发到静态总线，宿主订总线那一路即可。
- **补一处副本漂移**：`Wiring.DockAggregate.cs` 的 `ExplorerSelectionChanged` 漏了
  `SetTransformButtonsVisible(control != null)`（一直靠实例直订那一份兜着）
  ——「唯一实现」的实证：**两份副本不会一直等价**。
- **新增静态守卫** `scripts/check-xaml-duplicate-wiring.py`（口径：任意具名控件的任意事件属性
  ⇄ code-behind 对同一控件同一事件 `+=`；带 `--selftest` 三档自检 —— 只做第一档是**半个**负控）。
  接进 `ci-gate.sh` 的 [1d] 段，**不新增门禁步数（仍 7 步）**；python3 依赖 **fail-closed**
  （找不到就判失败，不静默跳过）。

### 判据

`DockPanelInvocationUiTests` 的 `Popup` 桶加 `ExpectCount`（`record BtnSpec` 第 6 个参数）。
**提供信息的是红过的那一次**（`Faceplate.BtnAiTemplate +2`）；修成 `+1` 之后靠"曾经红过"支撑。

⚠ 这条判据成立的**前提是本类独占进程**（#45 的规避）：静态事件总线的订阅从不解绑，
订阅者个数会随"本进程此前建过几个窗口 / 面板实例"增长。

### 验证读数

| 项 | 读数 | 出处 |
|---|---|---|
| 静态扫描（修前 → 修后） | **12 处 / 5 文件 → 0 处** | `check-xaml-duplicate-wiring.py` |
| 该判据自检 | **3/3** | 同上 `--selftest` |
| 行为判据（修前 / 修后） | `Faceplate.BtnAiTemplate` **+2（红）→ +1（绿）** | `.workbuddy/tmp/t-43-2.log` / `…-43-3.log` |
| 门禁（提交前 / 提交后） | **7 步全绿 / `GATE_EXIT=0`** ×2 | `.workbuddy/tmp/ci-gate-43.log` / `…-43-post.log` |
| 时序核对 | 日志 mtime **`12:14:47`** 晚于提交 `9216509` 的 **`12:13:49`** | 同上 |
| 测试计数 | 大队 `App.UiTests` **321** + 独占段 **2** = **323**（三段之和 = 全量） | 同上 |

提交 **`9216509`**（10 files changed, **+249 −32**）。

### 顺带挖出的更大一层（#46）

`BtnAiTemplate` 在双接线删掉后**仍弹 2 个** ⇒ 顺着订阅者往下挖，发现这不是"漏删"、是**有意的设计**：
视图在构造函数里把事件转发到静态总线，宿主又直订视图，两路都活。三处注释为证
（`Wiring.Canvas.cs:135` 写着"**双路订阅**：实例事件（初始实例）+ DockLayoutState 聚合事件
（Dock 重建后新实例）"、`:46-47` 紧邻两行分别直订与订总线、`Wiring.DockAggregate.cs:41` 原文写着
"**通过防重入标志避免双触发**"）—— **但全仓只有 `AiIconRequested` 一处真做了标志**，
其余 **26 处全是纯双触发**。

实测 **27 处**，总线侧全都有订阅（删任一路都不掉链）。其中 **5 处非幂等**、会写进项目数据：
`AiChat.ApplyRequested`（`_currentProject.Screens.Add(screen)` **无去重** ⇒ 同屏加入工程**两次**）、
`Faceplate.InstantiateRequested`（双击模板 ⇒ 画布上**两个**实例，各进一次撤销栈）等。

处置路线 **A（删宿主直订、只留总线，倾向）** / **B（保留双路 + 补 26 个防重入标志）**
见登记表 #46 与专题 §9.5 —— **需拍板**。

### 仍未闭环

- **#44 未修**（`Property.BtnAutoFix` 必崩，判据刻意断言"当前会抛"）。
- **#46 只修了 1 / 27 处**（其中 5 处非幂等）。
- **负控 N9 已过期**：它注入的正是被删掉的那 4 行；重做时应改成对 `ExpectCount` 的正控。
- #46 那一族的**静态守卫还没有**（要等修完才能开零基线）。
- #45（同进程僵死）根因未定、阶段 49 遗留②、#39 构造路径那半、#42（绿判据无读数）—— 状态不变。

> **时效**：上面这一节是**阶段 53 当时**的记录。「#44 未修 / #46 只修了 1/27 / 这一族的静态守卫还没有」
> 三条遗留**已于阶段 54 处理**，见下一节。

## 阶段 54（闭环 #46：删 28 处宿主直订、只留总线；顺带闭环 #44）— 2026-09-21

### 拍板

阶段 53 把 #46 摆上桌时给了 A/B 两条路线（A 删宿主直订只留总线 / B 保留双路 + 补 26 个防重入标志），
**健选 A**：

> `A. 删宿主直订、只留总线`

### 口径又放宽一次：26 → 28 处

| 文件 | 处数 | 明细 |
|---|---|---|
| `Wiring.Faceplate.cs` | 10 | Faceplate 4（Instantiate / Create / Edit / Delete）+ `ZoomLevelChanged` 1 + AiChat 5 |
| `Wiring.Canvas.cs` | 16 | Toolbox 1 / ResourceLibrary 1 / Canvas 2 / Property 2 / Layers 2 / Assets 4 / Navigation 2 / Explorer 2 |
| `Wiring.Panels.cs` | 2 | `PreviewModeChanged` / `PreviewControlClicked` |
| **合计** | **28** | 保留**单路 1 处** `AiChatPanel.ConfigChanged`（视图没有转发它） |

上一版登记的 27 处里「Canvas 3」只算了 `DesignCanvasView` **既 invoke 实例事件、又 raise 总线**的那三个
（`ZoomLevelChanged` / `PreviewControlClicked` / `PreviewModeChanged`）；**漏了**
`CanvasPanel.Canvas.SelectionChanged` 与 `ControlPropertyChanged` —— 宿主直接订阅**内层 `DesignCanvas`**
的事件，而视图也对同一个 `CanvasLayer` 订阅过 ⇒ **同一个事件的两个订阅者**，照样跑两遍。

### 改了什么

- 28 处实例直订**全删**；视图侧转发**一处未动**；删掉随之成为死代码的实例侧包装
  `OnCanvasSelectionChanged` / `OnPropertyPanelChanged`。
- **保留 4 个 `_suppress*` 标志** —— 顺带**更正阶段 53 的一句错话**：「只有 `AiIconRequested` 一处真做了
  防重入标志」是错的，实际**有 4 处**（`MainWindow.axaml.cs:144-147` 的 `_suppressSelectionEvent` /
  `_suppressPropertyEvent` / `_suppressLayerEvent` / `_suppressAiIconEvent`），但**只在总线侧被检查**
  ⇒ 对实例直订那一侧无效、**挡不住双路**（这才是「标志压第二次」这个设计失败的确切原因）。
  而且它们防的是**处理器自身引发的回环**（`BindControl` → `PropertyChanged` 等）
  ⇒ 删掉会把递归重新打开，**必须保留**。
- `MainWindow.axaml.cs` 行数保持 **3013**（未动 `SingleFileSizeGuardTests` 基线表）。

### 新静态守卫

`scripts/check-dock-bus-only-subscription.py`，接进 `ci-gate.sh` 的 `[1e]` 段（**不新增 banner ⇒ 仍 7 步**）。
判据形态 = 「接收者是不是面板视图」**由声明推导**（`private FaceplateLibraryView FaceplatePanel => …`），
**不猜名字后缀** —— 首版用「标识符以 `Panel` 结尾」实测**误报 4 处**（`NumLeftPanel` / `NumRightPanel` /
`NumBottomPanel` 是 `NumericUpDown`、`ChartPanel` 是 `TrendChartPanel`）。另有第二条规则
（`GetActiveView<…>(…)!.X +=`，基线 0）。fail-closed 两层：python3 依赖（复用 `[1d]` 的 `command -v`）
+ `MIN_VIEW_MEMBERS` 下限（防声明解析失效后判据**恒绿**）。`--selftest` **8 档**（3 红 + 5 绿，含白名单、
剥注释与字符串字面量、名字碰巧同缀的非视图控件）。

### 顺带闭环 #44

`BtnAutoFix` 在无选中控件时仍会发出 null（`_currentControl!` 一个字没动），但**唯一剩下的消费方带 null 守卫**
（`PropertyPanelView` 构造里的转发器 `if (control != null)`）⇒ 崩溃消失 —— 此前会抛的正是那条
**没有守卫的实例直订**。判据自带的「修好后请移入 Silent 桶」提示**正好生效**：已移桶，并把 `Why` 改写成
「**不许把实例直订加回来**」。**残留**：无选中控件时现在是**静默无操作**，与「屏幕级建议走
`ShowAiSuggestions(CurrentScreen)`」的意图仍未对齐（该路径已无本层可见症状）。

### 验证读数

| 判据 | 读数 | 证据 |
|---|---|---|
| 新守卫自检 | **8/8** | `python3 scripts/check-dock-bus-only-subscription.py --selftest` |
| 新守卫扫描 | **0 违规**（扫 121 个 `.cs`；识别 24 个视图成员；白名单命中 1 条） | `.workbuddy/tmp/ci-gate-46-post.log` |
| 行为判据 | `DockPanelInvocationUiTests` **2/2 绿**；`BtnAiTemplate` **+1**、`BtnAiIcon` **+1** | `.workbuddy/tmp/dock-46c.log` |
| 门禁（提交后复跑） | **7 步全绿 / `GATE_EXIT=0`** | `.workbuddy/tmp/ci-gate-46-post.log`，mtime **`13:14:47`** 晚于提交 **`5cabdbb`** 的 **`13:13:54`** |
| 测试计数 | 大队 8 程序集合计 **3006**（`Core.Tests` 2116 + `App.UiTests` 321 + 其余 569）+ 独占两段 **8 + 2** = **3016** | 同上 |
| 构建 | **0 警告 / 0 错误** | 同上 |

**被既有判据拦下的两处**（首次门禁红，改完才绿 —— 这正是守卫该干的事）：
① `CoreVisibleTextInventoryGuardTests`：SvgParser 通道的**消费方路径**随代码搬家
（`Wiring.Canvas.cs` → `Wiring.DockAggregate.cs`，标记 `asset.svg.parse.fail`）；
② `SingleFileSizeGuardTests`：新加的注释把 `MainWindow.axaml.cs` 顶到 **3020 ≠ 基线 3013**，
处置是**把注释压回 1 行让行数回到 3013**，**没有**去调大基线。

**新开 #47**（本阶段门禁复跑产出）：`DockViewResolutionUiTests.Match_AgreesWithBuild_ForEveryDockable`
间歇红 —— 抛在**平台初始化**那一步（`Dispatcher.VerifyAccess()`），阶段 54 门禁第 1 次命中、
随后**按原命令连跑 3 次 = 0/3**；同一次连跑里 `T113SoBuilderTests` 也红过 1 次（与本次改动零交集）
⇒ 与 #45 指向**同一个进程级单例**。根因未定、暂不动。

## 阶段 55（#47 机制定位：Dispatcher 归属窗口 + 哨兵判据 + 全库更正一句错话）— 2026-09-21

**起点**：阶段 54 门禁复跑产出 #47（`DockViewResolutionUiTests` 间歇红，抛在**平台初始化**那一步），
当时状态是「根因未定、暂不动」。本阶段**先量复现率再谈修法**，然后改用**反编译核实**而不是继续猜。

### 55.1 复现率（门禁大队原命令，一次不多一次不少）

| 批次 | 命中 |
|---|---|
| 阶段 54 门禁第 1 次 | **1**（历史唯一一次） |
| 阶段 54 连跑 3 次 | 0/3 |
| 本阶段连跑 10 次 | **0/10** |
| **合计** | **0/13** |

⇒ 低于 1/13，**不足以验证任何修法**。因此本阶段**不改隔离级别、不加预热** ——
那类改动在没有验证手段时就是赌。日志：`.workbuddy/tmp/repro47/run-{1..10}.log`。

### 55.2 机制：逐条 IL 核实（Avalonia 12.1.0，`ikdasm`）

| # | 事实 | 证据 |
|---|---|---|
| ① | `StartNew(Type)` 的隔离级别是 `PerTest`（枚举值 1）⇒ `EnsureIsolatedApplication` **每次派发**都 `EnterScope` + `ResetBeforeUnitTests` + `SetupUnsafe`（＝重建平台、新 `Compositor`） | `HeadlessUnitTestSession.<StartNew>b__0`；`AvaloniaTestIsolationLevel` 只有 `PerAssembly=0` / `PerTest=1` |
| ② | `ResetGlobalState()` 把 `s_uiThread` / `s_currentThreadDispatcher` / `s_dispatchers` 清空；**派发收尾时又重置一次** | `Dispatcher::ResetGlobalState`；**实测**读数 `ambientUIThread=s_uiThread=null（窗口开着）` |
| ③ | `Dispatcher..ctor`（持 `s_globalLock`）做的是 `if (s_uiThread == null) s_uiThread = this;` | `Dispatcher::.ctor` IL_0094–009c |
| ④ | `CheckAccess()` = `ReferenceEquals(Thread.CurrentThread, _thread)` | `Dispatcher::CheckAccess` IL |
| ⑤ | `DefaultRenderLoop` **无任何静态字段**；全程序集唯一的构造点是 `RenderLoop.FromTimer`；`Add` 当场读 `Dispatcher.UIThread` 再 `VerifyAccess()` | 字段表 + `newobj DefaultRenderLoop::.ctor` 仅 1 处 |

⇒ **只要任何线程在 ①→③ 的窗口里读一次 `Dispatcher.UIThread`，它就抢下 `s_uiThread`；
会话线程随后的 `Add` 必然抛「different thread owns it」** —— 与实测异常消息逐字吻合。

### 55.3 已排除的候选（负结果也记）

派发体内 `await` 续体逃逸到 worker 线程（那会把 Avalonia 调用带到别的线程上）。
两条探针（`Task.Yield` / `Task.Delay`）**都回到派发线程** ⇒ **排除**，并留作契约：
`DispatchContinuationContractTests`（2 条）。

### 55.4 哨兵与诊断

| 产出 | 作用 |
|---|---|
| `DispatcherOwnershipContractTests`（3 条） | #47 的**廉价哨兵**：派发线程必须拥有 `Dispatcher.UIThread`、跨派发必然换实例、诊断读数有分辨力。比 `DockViewResolutionUiTests`（要建 12 个面板）便宜得多，红了自带现场 |
| `UiTestHost` 失败现场自证 | 平台初始化失败时抛出的消息附上现场（派发序号 / 调用线程 / `s_uiThread` 归属）；**判据照旧红**，只让下一次红得有信息 |
| 窄签名判定 | 只认 Avalonia 那三句消息，其它异常原样抛出（诊断层不许吞掉真实失败） |

⚠ **一条自己踩到的陷阱**：`Dispatcher.UIThread` 的 getter 在 `s_uiThread == null` 时**当场构造**一个
`Dispatcher` 并抢走 `s_uiThread` ⇒ **「读一下环境状态」的诊断代码自己就会制造这个缺陷**。
处置：诊断只反射读内部 `TryGetUIThread()`（不构造、不清空），并加一条判据钉住
（读数里出现 `checkAccessOnCallingThread=True` 即判红）。

### 55.5 全库更正一句错话（6 处 md + 3 处 cs）

原句「渲染循环（`DefaultRenderLoop`）是**进程级静态单例**、绑死在第一个会话的 UI 线程上」出现在
`docs/2026-09-19-svg-gradient-and-transform.md`（2 处）、`docs/2026-09-17-open-items-register.md`、
`docs/project-status.md`（2 处）、`docs/2026-09-21-dockpanel-invocation-net.md` §10.6，
以及 `SvgGradientCanvasTests.cs` / `DockPanelInvocationUiTests.cs` / `RenderTestHost.cs` 的注释。
它还是 **2026-09-19 那条决策（Skia 渲染测试单开程序集）的依据**。
经 IL 核实**不成立**（见 55.2 ⑤）⇒ 每处补更正标注：**结论不变、理由换成**
「平台配置是进程级的、同进程换不了第二套」＋ `s_uiThread` 的「先到先得」窗口。历史不删。

### 55.6 验证记录（提交**前**门禁）

日志 `.workbuddy/tmp/ci-gate-55.log`，mtime **`17:04:54`**（本阶段代码与文档均**尚未提交**，
所以这条读数只覆盖「工作区内容」，**不能**当成提交后状态的验证 —— 提交后复跑见 55.8）。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误**（`[3]`） |
| `[2]` 段三条子检查 | ① 无硬编码家目录（635 文件）② 无重复接线（40 个有事件声明的 `.axaml`）③ **面板事件只经总线消费**（121 个 `.cs`；识别 24 个视图成员；白名单命中 `AiChatPanel.ConfigChanged`） |
| 测试计数 | 大队 8 程序集合计 **3011**（`Core.Tests` 2116 + `App.UiTests` **326** + `AI.Tests` 315 + 其余 254）+ `[6]` 独占两段 **8 + 2** = **3021** |
| 新判据落点 | `App.UiTests` **321 → 326**（`+5` = `DispatcherOwnershipContractTests` 3 + `DispatchContinuationContractTests` 2），全部包含在大队那一段里 |
| 固件 host | ✅ 全部通过（`[7]`） |
| 复现率 | **0/13**（见 55.1）—— 本阶段**未改任何修法**，只加哨兵与诊断 |

### 55.7 遗留

抢 `s_uiThread` 的「**先到者**」仍未确证。候选修法（`PerTest` → `PerAssembly` 隔离、
给 `UiTestHost` 加 `[ModuleInitializer]` 预热会话）**都必须先有可复现信号才动手** ——
反向同理：「连续多次不红」同样不构成修好的证据。

### 55.8 提交后门禁复跑（时序干净的那条）

日志 `.workbuddy/tmp/ci-gate-55-post.log`，mtime **`17:06:58`**，晚于代码提交 `bf35e30`（`17:05:34`）
与文档提交 `88bb0f4`（`17:05:38`）—— 这条才覆盖「提交后状态」。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误**（`[3]`，`已用时间 00:00:21`） |
| 测试计数 | 大队 8 程序集 **3011**（44 / 44 / 75 / 87 / 315 / 4 / 2116 / **326**）+ `[6]` 独占两段 **8 + 2** = **3021** —— 与 55.6 **逐程序集相同** |
| 日志体量 | **838 行**，与提交前那次同形 |

**⚠ 一个会被误读成「这次没跑全」的读数**：本次墙钟 **1 分 17 秒**，提交前那次 **5 分 27 秒**。
差异**全部**来自 `[3] dotnet build` —— 提交前那次因测试源码刚被改过而走**全量构建**
（`已用时间 00:04:27.27`），本次是**增量空构建**（`00:00:21.94`）。
两次逐行 `diff` 出的 304 行差异**全是并行输出的顺序与毫秒/秒级耗时数字**，
没有任何一条是「段缺失、段为空、程序集少一个」；`[2]` 的三条子检查、`[4]`/`[5]`/`[6]`/`[7]`
的段数与测试计数**逐项相同** ⇒ **不是跳过、不是短路**。
（记这一条是因为「墙钟变 1/4」在别的场合正是「步骤被跳过」的症状，不该靠感觉放过。）

## 阶段 56（#47 从「机制推断」到「实测复现 + 修法已验证但未落地」）— 2026-09-21

**起点**：阶段 55 把机制查到 IL 层，但**从未实测复现**（0/13）⇒ 卡在「没有可复现信号，任何修法都无法验证」。
本阶段把信号造出来、用它验了一个修法，并把判据从「编码实现细节」改成能**点名偷手**的形式。

### 56.1 造信号：人为制造「偷手」

探针（临时，跑完即删）：开一条**非会话线程高频读 `Dispatcher.UIThread`**（那个 getter 在
`s_uiThread == null` 时会当场构造并抢走它），同时主线程连跑派发。**墙钟固定 5s**，
并带一个**正控**：派发体内计数器 `bodies`（防"派发快到不像话"型自欺）。

| 隔离级别 | 偷手读数 | 派发次数 | 墙钟 | **跑到的派发体** | 失败 |
|---|---|---|---|---|---|
| **PerTest**（现状） | 986,175,686 | 114,367 | 5000 ms | **221** | **114,146 = 99.81%** |
| **PerAssembly**（候选修法） | 560,548,259 | 2,000,000（撞上限） | 2260 ms | **2,000,000** | **0** |

- **机制至此实测成立**（此前只是 IL 推断）：窗外的那一次读一定会把 `s_uiThread` 抢走，
  会话线程随后的 `Add` 必然抛 `different thread owns it`。失败现场逐字为
  `callingThread=5, sessionThread=13, ownerThread=19（**非会话线程＝偷手**）`。
- `bodies=221` 正好 = `114367 − 114146` ⇒ **失败的派发死在平台初始化、根本没进派发体**。
  顺带提醒：这也解释了「11.4 万次派发只花 5 秒」——那是因为**失败的派发很便宜**，别读成"派发很快"。
- ⇒ #47 的「偶发」**不是窗口窄，而是真实套件里几乎没有线程在派发之外去碰 Avalonia**。

### 56.2 修法已验证，但**本阶段不落地**

反射转储确认够得着：`StartNew(Type, AvaloniaTestIsolationLevel)`、
`AvaloniaTestIsolationLevel { PerAssembly, PerTest }`、`GetOrStartForAssembly(Assembly)`、
`AvaloniaTestIsolationAttribute` 全都存在。把 `UiTestHost` 的会话改成 `PerAssembly`：

| 项 | 读数 |
|---|---|
| 竞态探针 | **0/2,000,000**（见上表） |
| `App.UiTests` 全量（`!~DockPanelInvocationUiTests`） | **326 项 / 324 绿 / 2 红**；日志 `.workbuddy/tmp/suite-perAssembly.log` |

两条红**都是标定问题、不是产品回归**，但**只有一条解释清了**：

1. ✅ `CrossDispatch_GlobalDispatcherStateIsResetEachTime` —— `Assert.NotSame(first, second)`
   断的正是 **PerTest 的实现细节**（每次派发换实例）；per-assembly 下当然是同一实例。
   ⇒ 这条**本来就该改**，已改写为**隔离级别无关**的
   `CrossDispatch_OnlyTheSessionThreadMayHoldTheUIThread`（见 56.3）。
2. ⚠️ `CanvasRenderBaselineTests.Gate_CanvasRenderAllocation_StaysWithinBudget` —— per-assembly 下
   每帧分配**系统性上抬**（`Slider 4221 > 4000`、`TabView 5757 > 4000`、`Tank 13067 > 12000`），
   而该判据**已有 50 轮 warmup**、且是**在同一个派发内**取
   `GC.GetAllocatedBytesForCurrentThread` 差值 ⇒ 「一次性分配被挪进测量窗口」解释不成立。
   机制未定 ⇒ **不调预算、不改判据**（「解释不清就别动基线」）。

⇒ **per-assembly 是把窗口关掉的正解（0/200 万），但落地前必须先解释上面第 2 条。**
所以本阶段的产出是「信号 + 判据 + 证据」，**不是**「改隔离级别」。

### 56.3 判据升级：从「编码实现细节」改成「点名偷手」

| 变更 | 前 | 后 |
|---|---|---|
| 跨派发契约 | `Assert.NotSame`（断 PerTest 实现细节，换隔离级别即红） | `CrossDispatch_OnlyTheSessionThreadMayHoldTheUIThread`：持有者必须是 `null` 或**会话线程**；隔离级别无关，且是**偷手报警器** |
| 诊断 | `ownerThread=19`（要人去比对） | `ownerThread=19（**非会话线程＝偷手**）`，并新增 `sessionThread=` 读数 |
| 读数口 | 无 | `UiTestHost.AmbientUIThreadOwnerId()` / `SessionThreadId`（只读、**不构造** Dispatcher） |

**负控 1/1**：人为让一条非会话线程读一次 `Dispatcher.UIThread` ⇒
`thiefThread=19, sessionThread=13, 读出 ownerThread=19, 判定=非会话线程＝偷手` ✓
（`.workbuddy/tmp/negctl47.log`）⇒ 新判据的**输入信号是真的**，不是恒绿的门。

### 56.4 读数

| 项 | 读数 |
|---|---|
| 判据 | `DispatcherOwnershipContractTests` **3/3** ＋ `DispatchContinuationContractTests` **2/2** = **5/5 绿**（PerTest） |
| 隔离级别 | **保持 `PerTest`** ⇒ 线上行为**零变化** |
| 探针 | 已删除；复现配方与两侧读数保留在本节 |

### 56.5 遗留（给下一轮）

① **解释** `CanvasRenderBaselineTests` 在 per-assembly 下的系统性上抬（三条越线：+5.5% / +44% / +9%）。
   做不到就别动 —— 否则是拿一个**未解释的读数**换掉一个能抓「每帧新建画刷」的判据。
   ⇒ **阶段 57 已闭环**（成因定位到框架 `DrawText` 的**环境相关成本**；仍不落地，理由见 57.4）。
② 解释后可落地：`UiTestHost` **一行**改 `PerAssembly`，再跑门禁 ×2 确认。
   ⇒ **阶段 57 判定：不落地**（判据会变成环境相关，见 57.4）。
③ **真偷手仍未抓获**（0/13 是「没有偷手」还是「偷手很稀」尚未分离）。但抓手已就位：
   此后 #47 若再红，消息里直接带 `sessionThread` 与 `ownerThread（非会话线程＝偷手）`，
   **一眼可判**是外部偷手还是内部 bug。

### 56.6 门禁（提交**前**）

日志 `.workbuddy/tmp/ci-gate-56.log`，mtime **`17:40:28`**（本阶段代码与文档均**尚未提交** ⇒
这条只覆盖「工作区内容」，**不作为提交后验证**；提交后复跑见 56.7）。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误**（`[3]`） |
| `[2]` 段三条子检查 | 硬编码家目录（635 文件）/ 无重复接线（40 个 `.axaml`）/ 面板事件只经总线（121 个 `.cs`）—— 全绿 |
| 测试计数 | 大队 **3011**（`Core.Tests` 2116 + `App.UiTests` **326** + `AI.Tests` 315 + 其余 254）+ `[6]` **8 + 2** = **3021** —— **与阶段 55 同数**（本阶段**没有增删用例**，只是把一条判据改了名字与判据形状） |
| 墙钟 | **1 分 45 秒**（又是增量空构建，成因见 55.8） |

### 56.7 提交后门禁复跑（时序干净的那条）

日志 `.workbuddy/tmp/ci-gate-56-post.log`，mtime **`17:42:10`**，晚于代码提交 `3e56e40` 与文档提交 `e0357d1`。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误** |
| 测试计数 | 大队 **3011**（44 / 44 / 75 / 87 / 4 / 315 / **2116** / **326**）+ `[6]` **8 + 2** = **3021** —— 与 56.6 **逐程序集相同** |
| 日志体量 | **838 行**（与阶段 55 那两份同形） |
| 墙钟 | **54 秒**（增量空构建；同一成因已记在 55.8，别读成"步骤被跳过"） |

## 阶段 57（解释阶段 56 的三条越线：上抬全在框架 `DrawText` 的环境相关成本上；隔离级别、缓存、并行**三罪皆无**）— 2026-09-21

### 57.1 一句话结论

阶段 56 的三条越线**不是** `PerAssembly` 造成的，也**不是**我们的渲染代码回退 ——
它是「**同一个进程里先跑过整套 UI 用例**」这个条件本身造成的：
框架 `dc.DrawText(ft, p)` 的单次分配会从 **2,604 B 涨到 3,100~3,856 B**，
且**与文本内容无关、按字形数线性放大**（斜率 416 → 932 B/字形）。
`PerTest` 之所以看不见，是因为它**每次派发都重建平台**（55.2 那条链），
把抬升态一起清掉了 —— 于是「`PerAssembly` + 整队」才第一次把这个读数暴露出来。

⇒ **落不落地 `PerAssembly` 的答案由此确定：不落地**（判据会变成环境相关，见 57.4）。本阶段**不改产品代码、不改判据阈值、隔离级别保持 `PerTest`**。

### 57.2 四个假设，逐个做判别实验（全部否证，除了最后一条只解释三成）

| # | 假设 | 判别实验（命令 → 日志） | 结果 |
|---|---|---|---|
| A | 隔离级别本身 | `PerAssembly` 下**只**跑 `CanvasRenderBaselineTests` → `render-perasm-alone.log` | **否**：与 `PerTest` 单独跑**逐字节相同**（Button 2,804 / Slider 3,280 / Gauge 3,004 / TabView 3,635） |
| B | 共享静态缓存被污染 / 被清空 | 测量前先画 **600 条互异文本**灌缓存 → `probe-pollute600.log` | **否**：读数一格未动（2,804 / 3,280 / 3,004 / 3,635 / 8,809）；`invalΔ` 全程 **0** |
| C | 集合并行 | `-p:ParallelizeTestCollections=false` → `serial-suite.log` | ⚠ **这一刀是空转，结论作废（2026-09-21 阶段 59 更正）**：`-p:` 是 **MSBuild 属性**，而 xunit 的并行度只从 `xunit.runner.json` / `.runsettings` 读 —— 全仓没有任何构建文件把它转发给 xunit（`grep` 过 `.csproj` / `Directory.Build.props`，零命中）⇒ 那次「串行」跑的其实**还是并行**。正确开关是 `.runsettings` 的 `<xUnit><ParallelizeTestCollections>`，阶段 59 实测有效（派发并发度 **20 → 1**）。**所以本条不是"否证"，是"从未执行"**；至于并发到底影不影响这条读数，见阶段 59 |
| D | 测试平台未按生产入口兜底字体 | `TestAppBuilder` 加 `FontManagerOptions{DefaultFamilyName="Noto Sans CJK SC"}` → `fontpinned-suite.log` | **部分解释（约三成）**：CJK 3,465 → 3,100、ASCII10 11,146 → 9,063，越线 **4 类 → 1 类**；但**单类基线一格未动**（2,604 / 5,932），说明它改的是整队侧的不确定性，不是"解析到哪个字体" |

三个关键读数（同一次实验里同时取到，排除"缓存命中与否"这条歧义）：

- **入口缓存几乎是空的**：`counts=(5, 4, 3, 2) inval=0`；整轮 `texts` 只从 3 长到 51。
- **缓存全程命中**：逐类型 `invalΔ=0`，`texts` 计数平稳 ⇒ **没有一次 `InvalidateTexts()`**。
- **底噪恒为 0 B/帧**（0 控件屏），两处一样 ⇒ 差分口径没问题。

### 57.3 最小复现：同代码、同 `FormattedText` 实例，只换进程环境

`Baseline_UnitCost_OfDrawingContextOps` 里同一段代码，四个环境下并列（`N=2000`，热身 200）：

| 环境 | `new FormattedText` | `DrawText`（2 字） | `DrawText`（10 字） | 斜率 |
|---|---|---|---|---|
| 单类（`PerTest` **或** `PerAssembly`） | 384 | **2,604** | **5,932** | 416 B/字形 |
| 整队同进程（未兜底字体） | 384 | **3,465** | **11,146** | 932 B/字形 |
| 整队同进程（兜底字体） | 384 | 3,100 | 9,063 | 745 B/字形 |
| 整队同进程（兜底字体 + 串行） | 384 | 3,276 | 9,091 | 727 B/字形 |

同一次实验里，**其余绘图操作四个环境全部为 0**：
`FillRectangle` / `DrawRectangle` / `DrawLine` / `DrawGeometry` / `PushTransform` / `PushOpacity`。
`复用实例连续读 .Width` 也全是 0（布局已缓存）。

> **口径说明（可复现性）**：本节的判别实验**全部在 Debug 下做**（Release 全量并发会撞登记表第 27 项
> 那个 testhost 挂死，Debug 单类/整队可跑完）；阶段 56 那次越线是 **Release**。
> 两者可比：同一「整队同进程」场景 Slider **Release 4,221 / Debug 4,149**（差 **1.7%**）；
> 单类场景两种配置都是 **2,804 / 3,280 / 3,004 / 3,635**。⇒ 本节的**差值**结论与配置无关。

**形状证据**（25 类型整表，单类 vs 整队）：

| 类型 | 单类 | 整队 | Δ |
|---|---|---|---|
| Switch / Knob / Led / Line / Svg / Rectangle / Ellipse / Arc | 2 | 2 | **0** |
| Table / Polyline | 490 / 602 | 490 / 602 | **0** |
| Button / Label / Bar | 2,804 | 3,701 / 3,697 / 3,725 | **+893 ~ +921** |
| CheckBox / Image / Pipe / Valve | 2,716 / 2,748 | 3,643 / 3,664 / 3,818 / 3,846 | **+930 ~ +1,100** |
| Slider / Gauge / TabView / Tank | 3,280 / 3,004 / 3,635 / 8,807 | 4,149 / 4,075 / 6,022 / 13,545 | — |
| Keyboard（39 键文本） | 86,749 | 111,089 | **+24,340** |
| 全类型屏 25 控件 | 135,114 | 179,598 | **+44,484** |

⇒ **凡画文本的都上抬、凡不画文本的一格不动**；TabView（+2,122）与 Tank（+4,260）恰好是同单位的 1×、2× 倍数。
这条形状把「我们的渲染路径回退」彻底排除：回退不会只在文本上、按字形数线性地体现。

### 57.4 为什么不落地（判据会变成环境相关）

即使把字体按生产入口兜底，`PerAssembly` 下仍有 **1 类越线**（TabView 4,921~4,924 > 4,000）；
未兜底时是 **4 类**（Slider / Gauge / TabView / Tank）。
这条判据的语义是「**渲染路径上不许再出现每帧新建画刷/画笔/文本布局**」，
它的读数里**必须**是框架不可压缩的那部分占大头（普通控件实测 2,804 ≈ 框架 `DrawText` 底价 2,604）。
一旦进程环境能让框架那部分**翻倍**，同一个提交就会**今天绿、明天红**：

- 放宽容差 ⇒ 拿一个**未解释的读数**换掉一个能抓真实回退的判据；
- 收紧阈值 ⇒ 在整队环境下必然红。

⇒ 结论：**保持 `PerTest`**（同时它也是 #47 的现有行为），本阶段不动 `UiTestHost` 的隔离级别，
`CanvasRenderBaselineTests` 的预算一个字不改。

### 57.5 留下的唯一新增：一条「环境探针」

`Baseline_UnitCost_OfDrawingContextOps` 末尾新增一张表（**只打印、不断言**），
把同一环境下 `CJK / ASCII(2 字) / ASCII(10 字)` 的 `new FormattedText` / `DrawText` / `Width` 三个读数并排。
它的用途是**识别当前进程处在哪一态**：把两次运行的同名表格对齐看即可（单类 2,604 vs 整队 3,465）。
**刻意不做成判据** —— 它测的是环境，不是我们的代码；拿它断言只会得到一个随运行方式变红的玩具。

### 57.6 仍未闭环（诚实边界）

- **抬升态的具体字段没定位到**。已排除：隔离级别、缓存污染/失效、集合并行、测试平台字体未兜底。
  「翻倍」发生在框架的**字形/文本布局构建**这一段（`new FormattedText` 不变、`DrawText` 与「首次读 `.Width`」同时 +980 B），
  下一步要反编译 Avalonia 12.1.0 的 `DrawingContext.DrawText(FormattedText, Point)` → `TextLayout` 那条链，
  像 55.2 处理 `Dispatcher` 那样逐条 IL 核实 —— 本轮**没有做**，不写成"已定位"。
- **测试平台与生产入口的字体配置仍不一致**（生产在 Linux 上设 `FontManagerOptions.DefaultFamilyName`，测试不设）。
  实测它只解释约三成，且**在 `PerTest`（我们实际运行的形态）下单类基线一格未动** ⇒
  它是「让测试更贴近生产」的**待评估项**，不是一个已验证的修法；要落地需另立证据。
- 另两件与本节无关的旧账仍在：`CanvasRenderBaselineTests` 的**绿判据无读数**（登记表 #42）、
  DockPanel 宿主死锁需独占进程（#45）。

### 57.7 门禁（提交**前**）

日志 `.workbuddy/tmp/ci-gate-57.log`，mtime **`18:00:07`**（本阶段代码与文档均**尚未提交** ⇒
这条只覆盖「工作区内容」，**不作为提交后验证**；提交后复跑见 57.8）。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误**（`[3]`） |
| `[2]` 段三条子检查 | 硬编码家目录（635 文件）/ 无重复接线（40 个 `.axaml`）/ 面板事件只经总线（121 个 `.cs`，识别 24 个视图成员，白名单 1 条：`AiChatPanel.ConfigChanged`）—— 全绿 |
| 测试计数 | 大队 **3011**（44 / 44 / 75 / 87 / 4 / 315 / **2116** / **326**）+ `[6]` **8 + 2** = **3021** —— **与阶段 55 / 56 逐程序集同数**（本阶段**没有增删用例**，只往既有用例里加了一段**只打印不断言**的探针） |
| 墙钟 | **1 分 4 秒**（增量空构建；同一成因见 55.8，别读成"步骤被跳过"） |
| 隔离级别 | 门禁跑的是 **`PerTest`**（现状不变）⇒ 本阶段读数与阶段 55 / 56 **同口径**，可直接比 |

### 57.8 提交后门禁复跑（时序干净的那条）

日志 `.workbuddy/tmp/ci-gate-57-post.log`，mtime **`18:01:24`**，晚于代码提交 `527c82e`（`18:00:25`）
与文档提交 `f799678`（`18:00:29`）。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误** |
| 测试计数 | 大队 **3011**（44 / 44 / 75 / 87 / 4 / 315 / **2116** / **326**）+ `[6]` **8 + 2** = **3021** —— 与 57.7 **逐程序集相同** |
| 墙钟 | **52 秒**（增量空构建；成因见 55.8） |
| 日志体量 | **837 行**（提交前那份也是 837） |

**两次 diff 核查**（按 55.8 立下的习惯，不靠感觉）：

- 提交前 vs 提交后（归一化掉耗时数字后）：差异**只有三类** ——
  ① 并行构建/测试的**输出顺序**；② `[3]` 的 `已用时间`（20.87 s → 10.89 s，增量程度不同）；
  ③ `AUDIT` 行的**秒数**。**无一处是段缺失或计数变化**。
- 阶段 55 / 56 记的是 **838 行**，本次两份都是 **837 行**。逐段对账：
  `[1]` 3 / `[2]` 5 / `[3]` 31 / `[4]` 29 / `[5]` 33 / `[6]` 10 / `[7]` **719 → 718** ——
  差的那 **1 行全在 `[7]` 固件 host 测试的输出段**（该段是固件侧测试的打印，**不参与判据**，
  判据是它的 banner 与退出码）。⇒ 不是"步骤被跳过"。

## 阶段 58（定位阶段 57 遗留①：抬升是「字体族名累计个数」的确定性阶梯，根因是 headless 字体桩「未命中却算命中」）— 2026-09-21

### 58.1 一句话结论

阶段 57 遗留的「抬升态的具体字段」**定位到了**，而且它不是一个「翻倍」，是一条**确定性阶梯**：

> `dc.DrawText(ft, p)` 的单次分配 = **`2,604 + 416 × N`** 字节，
> `N` = **本平台实例内解析过的、此前没见过的字体族名个数**（与画的是哪段文本无关）。

根因在 IL 里，不在我们的代码里：headless 的 `FontManager` 由 `HeadlessFontManagerStub` 支撑，
它的 `TryCreateGlyphTypeface(familyName, …)` **完全不看 `familyName`**，
一律返回同一份 4 字形 `BareMinimum.ttf` 的 `HeadlessPlatformTypeface`，**却返回 `true`**。
⇒ 每次「请求一个没见过的族名」都被当成**成功命中**登记进管理器，文本绘制成本**永久** +416 B/次。

⇒ 阶段 56 的越线由此解释干净：`PerAssembly` 下平台只建一次，**前面跑过的测试每碰一个没见过的族名，
就把后面用例的基线抬一档**，抬多抬少取决于跑了哪些用例 —— 这就是那三条越线的「间歇」来源。

### 58.2 判别实验：先立空白对照，再逐个加变量

全部在**全新进程**里、只跑这两个用例（Debug；口径说明同 57.3：Debug 与 Release 的单类基线逐字节相同）。
每个读数取**同一进程内的 `PerCall`（200 次热身 + 2000 次测量）**：

| 步骤 | 变量 | `DrawText(Typeface.Default)` |
|---|---|---|
| 空白 C2..C6 | 无（连字体都不读） | **2,604**（逐次完全相同） |
| 变量①′ | 解析 `Typeface.Default` **自身** | 2,604（不动） |
| 变量④ | 只 `new` 6 个不同 `FontFamily`，**一次都不解析** | 2,604（不动） |
| 变量① | **解析** 1 个没见过的族名 | 3,020 |
| 变量③ | 再 2 / 3 / 4 / 5 / 6 个不同族名 | 3,436 / 3,852 / 4,268 / 4,684 / 5,100 |
| 变量③ | 第 7 个 | **5,516** |
| **负控** | **同一个**族名**重复解析 ×6** | **5,516（纹丝不动）** |
| 变量⑤ | 解析**已在表里**的族（逐个，共 9 个） | 5,516（不动） |
| 变量② | **画**那段文本本身 | 2,812（与族表无关） |

- 空白 C1 = 2,755：那是**整个进程内第一次**测量的热身伪影（与字体无关），
  所以后续每次读数都取**两次的较小值**把它排掉。C2 起逐次相同 ⇒ 「重复测量会漂」这个怀疑被否掉。
- 变量④ 与变量① 的对照是关键：**构造族对象不算，解析才算** ⇒ 变量是「管理器里被登记的名字个数」。
- 负控把「解析**次数**」与「**不同**族名**个数**」分开：重复同一个名字 6 次，读数一格不动。
- 变量② 说明抬升只作用在 `Typeface.Default` 这条路径上（`2,812 < 3,020`），
  不是「绘制本身变贵」，而是「**解析 Default 这条路径变贵**」。

**数字指到哪条日志**（本阶段全部实验日志在 `.workbuddy/tmp/`，可逐条复核）：

| 日志 | mtime | 内容 |
|---|---|---|
| `verdict58-5.log` | `18:36:13` | 变量①′ / ④ / ① / ③（7 档阶梯）/ 负控 / ⑤ / ② 的全部读数 |
| `verdict58-6.log` | `18:37:31` | A/B 对称二连（A 出口 5,516、**B 入口 2,604**） |
| `guard58-negctl2.log` | `18:42:02` | **负控**：切 `PerAssembly` ⇒ 红在第三条（2,274 / 3,938 / 3,938） |
| `guard58-stab{1,2,3}.log` | `18:39:47/53/59` | 新守卫在 `PerTest` 下**连跑 3 次全绿** |
| `uitests58-full.log` | `18:42:32` | 整个 `App.UiTests` 程序集 **329/329 绿**（含新守卫，读数 2,274 / 3,961 / 2,274） |


### 58.3 跨派发会清零，跨用例不会 —— 阶段 56 的间歇性由此闭合

**A/B 对称二连**（同一个测试类里两份**完全相同**的实验，各含「入口读数 + 全套变量」）：

| | A | B |
|---|---|---|
| 入口（尚未解析任何新族） | 2,755（首次热身伪影） | **2,604** ← 回到干净值 |
| 出口 | **5,516** | **5,516** |

⇒ `PerTest` 隔离**每次派发**都重建平台（`EnterScope` + `ResetBeforeUnitTests` + `SetupUnsafe`，见 55.1），
**字体表随之清零** ⇒ 每个用例的文本起点固定 ⇒ 预算判据稳定。
⇒ 反过来，`PerAssembly` 下平台只建一次，族名**跨用例累计** ⇒ 起点随执行序漂。
⇒ 这也把 57.2 的第一行解释圆了：那时「`PerAssembly` **只**跑 `CanvasRenderBaselineTests`」与 `PerTest`
逐字节相同，正是因为那一次**没有任何别的用例贡献新族名**。

**负控（已实测）**：把 `UiTestHost` 的会话切成 `AvaloniaTestIsolationLevel.PerAssembly`，
跨派发起点停在 **3,938**（= 2,604 + 416×4，正是新守卫塞进去的 4 个探针族名）⇒ 新守卫变红在最后一条断言。

### 58.4 IL 核实（Avalonia 12.1.0 / `Avalonia.Headless.dll`，`ikdasm`）

`HeadlessFontManagerStub.TryCreateGlyphTypeface(string familyName, FontStyle, FontWeight, FontStretch, out IPlatformTypeface)`：

```
IL_0000:  ldstr      "resm:Avalonia.Headless.BareMinimum.ttf?assembly=Avalonia.Headless"
IL_0005:  newobj     Uri::.ctor(string)
IL_001a:  newobj     StandardAssetLoader::.ctor(Assembly)
IL_0020:  callvirt   StandardAssetLoader::Open(Uri, Uri)     // ← 那份 ttf 的 Stream
IL_0027:  ldarg.s    platformTypeface
IL_0029:  ldloc.1                                            // Stream
IL_002a:  ldarg.1                                            // ← 参数 1 = familyName
IL_002b:  newobj     HeadlessPlatformTypeface::.ctor(Stream, string)
IL_0031:  ldc.i4.1                                           // ← 恒返回 true
```

`familyName` **只**被当成一个字符串塞进新对象，**不参与任何查找**；同一类型的
`TryGetFamilyTypefaces` 则恒返回 `false`。另外 `GetInstalledFontFamilyNames` 返回的
**只有** `_defaultFamilyName` 一个元素 —— 而实测 `FontManager.Current.SystemFonts` 会随请求**变长**
（3 → 9 条，内容就是被请求过的那些名字 + `BareMinimum` / `Default`）：
⇒ 「命中」与「未命中」的**返回码完全一样**，只有留在管理器里的名字不同。

### 58.5 落地：一条前置条件判据（不放宽预算、不改隔离级别）

`CanvasRenderBaselineTests` 新增
`Precondition_PerDispatchPlatformReset_KeepsTextBudgetsComparable` ——
**一次用例、三次派发**，与执行顺序无关：

1. 派发①：量干净起点；
2. 派发②：**正控** —— 解析 4 个全新族名后，断言成本确实被抬高（≥ 100 B）。
   若哪天这条不成立，说明机制变了，后半段就不再说明任何问题，必须先重新确认机制；
3. 派发③：**被守的不变量** —— 起点必须回到干净值。

- 断言的是**关系**，不是 `416` 这个数：断言常数＝把自己钉死在 Avalonia 12.1.0 上；
  断言「会被抬高」+「跨派发回到同一起点」则只依赖被守的性质本身。
- **负控已实测**（`PerAssembly` ⇒ 红在 ③），失败消息里带三个读数。
- 同时把阶段 58 的环境探针**定稿**进 `Baseline_UnitCost_OfDrawingContextOps`：
  多一行「显式族」读数 + 一段 `Typeface.Default` 解析诊断（`family=` / `glyphCount=` / `'标'` / `'A'`），
  并写明**测试里所有文本都是 4 字形 `.notdef`**（`'标'`、`'A'` 都查不到）。

### 58.6 对 57.6 两条遗留的处置

- 「抬升态的具体字段」→ **已定位**（58.2 的阶梯 + 58.4 的 IL）。
- 「测试平台字体配置与生产入口不一致」→ **不落地**（关闭该待评估项）。理由：
  桩**忽略族名**，把 `DefaultFamilyName` 设成 `Noto Sans CJK SC` 只会改变**被登记的第一个名字**，
  既不能让测试渲染出真字，也**阻止不了**阶梯增长 ⇒ 它不是修法，连「让测试更贴近生产」都做不到。

### 58.7 仍未闭环（诚实边界）

- **阶段 57 记的整队读数对不上整数档**。整队曾记 3,465 / 3,856；本次阶梯的整数档是
  3,020（k=1）/ 3,436（k=2）/ 3,852（k=3 —— 这个对得很准）。3,856 与 3,852 只差 4，
  但 3,465 对不上任何一档（离 3,436 差 29）⇒ 那时可能**还叠着别的贡献**，
  而**本轮没有复现到那个态**（今天整队跑同一条读数就是干净的 2,604）⇒ 不写成「已完全解释」。
- **`416` 这个常量具体由哪个字段产生**（哪张 `Dictionary`/`List` 在每次绘制时被重放）——**未定位**。
  已确定的是它**按族数线性、按绘制次数恒定**，且与画的文本无关。
  再往下走要反编译 `FontManager` / `SystemFontCollection` 那条查找链 —— 本轮**没有做**。
- 另两件与本节无关的旧账仍在：`CanvasRenderBaselineTests` 的**绿判据无读数**（登记表 #42）、
  DockPanel 宿主死锁需独占进程（#45）。

### 58.8 门禁第一次跑：**红在新判据自己的绝对断言上**（这条红留档，它又证了一次 57 的结论）

日志 `.workbuddy/tmp/ci-gate-58.log`（`GATE_EXIT=1`）。`[1]`~`[4]` 全绿、构建 0 警告 0 错误，
**唯一一条失败就是本阶段新增的守卫**：

```
跨派发没有回到干净起点（干净 2,232，抬高后 3,938，下一次派发起点 2,274 B/次）
```

读法：重置**确实发生了**（2,274 远不是抬高态的 3,938），红的只是我写的那条 `afterReset <= clean`
被 **42 字节**的漂移踩掉。而「干净起点」本身在**单独跑（2,604）**与**整队跑（2,232）**之间就不一样。
⇒ **绝对字节数不能跨上下文比较** —— 这正是阶段 57 的结论本身，判据写成绝对值就是自己踩回去。
**处置**：把第③条断言改成**只用本次运行自己的两个数**的相对形式 ——
「重置后那一次**离干净比离抬高更近**」（`afterReset - clean < inflated - afterReset`），
并按登记表 #42 的教训让三个读数**无条件打印**（不再只在失败消息里出现）。
⇒ 这条红不是白跑的：它把「判据该写成什么形状」从推测变成了实测。

### 58.9 门禁第二次跑（7 步全绿，提交**前**）

日志 `.workbuddy/tmp/ci-gate-58b.log`，mtime **`18:43:35`**（本阶段代码与文档均**尚未提交** ⇒
这条只覆盖「工作区内容」，**不作为提交后验证**；提交后复跑见 58.10）。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误**（`[3]`） |
| 测试计数 | 大队 **3012**（44 / 44 / 75 / 87 / 4 / 315 / **2116** / **327**）+ `[6]` **8 + 2** = **3022** |
| 与阶段 57 比 | **3021 → 3022**，**差 1 = 本阶段新增的那条守卫**（`App.UiTests` 大队 326 → 327），其余逐程序集同数 |
| 墙钟 | **52 秒**（增量空构建；同一成因见 55.8） |
| 行数 | **837 行**（与阶段 57 两份相同） |
| 隔离级别 | 门禁跑的是 **`PerTest`**（本阶段**未改**）⇒ 与阶段 55 / 56 / 57 **同口径** |
| 新守卫读数 | 大队里是**绿**的（`[5]` 段默认 verbosity 不打每条用例输出，故日志里没有那三行；单跑 `App.UiTests` 程序集时读到 **干净 2,274 / 抬高后 3,961 / 重置后再量 2,274**，抬高 1,687） |

### 58.10 提交后门禁复跑（时序干净的那条）

日志 `.workbuddy/tmp/ci-gate-58-post.log`，mtime **`18:45:12`**，晚于代码提交 `a9c9885`（`18:44:12`）
与文档提交 `4a7de92`（`18:44:18`）⇒ 这条**覆盖的是提交后的树**。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误** |
| 测试计数 | 大队 **3012**（44 / 44 / 75 / 87 / 4 / 315 / **2116** / **327**）+ `[6]` **8 + 2** = **3022** —— 与 58.9 **逐程序集相同** |
| 墙钟 | **51 秒**（增量空构建；成因见 55.8） |
| 行数 | **837 行**（与 58.9 相同） |

**两份日志的比对（按 55.8 立下的习惯，不靠感觉）**：把 `已用时间` / `持续时间` / `总时间` /
时间戳这些**非确定性字段归一化**之后，`ci-gate-58b.log`（提交前）与 `ci-gate-58-post.log`（提交后）
**逐行 diff 只剩并行构建/测试的输出顺序** —— 用「两边各取一次 `sorted()` 再比」验证：
`838 行 vs 838 行，排序后完全相等 = True`。⇒ **无段缺失、无计数变化、无内容变化**，
两份是同一次门禁的两次等价执行。

**⚠ 时序声明（照 55.8 那条纪律，不靠"看起来没事"）**：本阶段的第 3、4 笔提交
`fe0bc79`（`18:45:46`）与 `e06b397` **晚于**上面这次门禁（`18:45:12`），但它们**只改 `docs/project-status.md`**
（即本节自身）—— `docs/` 不是门禁任何一步的输入，构建/测试/静态检查都不读它。
⇒ 上面这份读数覆盖的是 `a9c9885 + 4a7de92` 那棵树，与"最终树"在**所有被编译的输入**上逐字节相同。
这条写出来而不是省略：本项目恰好栽在"把早于改动的绿当成改动的验证"上过一次（2026-09-17），
所以每次引用一条绿都要能说清它到底覆盖了哪棵树。


## 阶段 59（#45 的竞态前提：同一条程序集里最多 20 个派发并发重建同一套平台 —— 落地派发闸门，顺带更正阶段 57 的一处空转证据）— 2026-09-21

### 59.1 一句话结论

`HeadlessUnitTestSession.DispatchCore` 是在**调用线程**上直接执行 `EnsureIsolatedApplication()`，
其实现是 `AvaloniaLocator.EnterScope()` + `Dispatcher.ResetBeforeUnitTests()` + `AppBuilder.SetupUnsafe()`
—— 三行**全是进程级操作，全程无锁、无重入判定**（IL 核实）。
而 xunit 默认让不同 collection 并行 ⇒ 实测同一条 `App.UiTests` 程序集里
**同一时刻最多有 20 个派发在飞**、195 次派发里 **149 次**是在别的派发未结束时进入的。

⇒ 这不是"更快"，是**同时存在 20 个各自以为独占 UI 线程的线程**，并互相把对方的平台拆掉重建。
⇒ **落地派发闸门**（只串行化"要动平台的那一刻"，不关掉 xunit 的集合并行）：**实测零代价**
（程序集 19~22 s → 18~21 s），并发度 **20 → 1**。

### 59.2 怎么量的：直接量**前提**，不去复现偶发挂死

复现偶发挂死要等 90 秒超时、还只拿到"挂/不挂"一个比特。这里换成量它的**前提条件**：
任一时刻最多几个派发在飞。这个量是**确定性**的。

`UiTestHost` 新增一个**默认关闭**的诊断（设 `HMI_DIAG_OUT=<文件>`，进程退出时写汇总；
不设时只多两次 `Interlocked` 自增，行为与读数都不变）。读数：

| 配置 | `dispatches` | `maxInFlight` | `overlapEvents` |
|---|---|---|---|
| 默认（集合并行，**闸门未加**） | 195 | **20** | **149** |
| `--settings serial.runsettings`（强制串行） | 195 | **1** | **0** |
| **加闸门后**（集合并行照旧） | 196 | **1** | **0** |

- 第一行与第二行是**同一套代码、同一条命令**，唯一变量是 xunit 是否并行
  （`.workbuddy/tmp/diag-parallel.txt` / `diag-serial.txt`）⇒ 计数器既**敏感**（20 vs 1）又**可分**
  （`maxInFlight=1` 与 `overlapEvents=0` 同时成立）。
- 第一行还留了第一次重叠的现场：`线程 [11, 21, 19, 28, 22] 同时在飞` ⇒ 确实是**不同测试的线程**。
- `firstOverlap` 那行是本机 **20 核**（`nproc`=20）的自然结果：xunit 起 20 个 worker。

### 59.3 更正阶段 57 的一条证据：那一刀**从未执行**

57.2 的表里「C 集合并行」用的是 `-p:ParallelizeTestCollections=false`。
`-p:` 是 **MSBuild 属性**，而 xunit 的并行度只从 `xunit.runner.json` / `.runsettings` 的
`<xUnit><ParallelizeTestCollections>` 读 —— 全仓**没有任何构建文件**把它转发给 xunit
（`grep` 过所有 `.csproj` 与 `Directory.Build.props`，零命中）⇒ 那次"串行"跑**其实还是并行**。

⇒ 该条**不是"否证"，是"从未执行"**，已在 57.2 就地标注作废。
正确开关是 `.runsettings`，本阶段实测有效（20 → 1）。
**这个更正本身比它的结论值钱**：它说明「实验跑了」不等于「自变量真的被改了」——
按本项目的老规矩，**一次只放开一个变量**之外还要**验证变量真的动了**（本例：拿并发度读数自证）。

### 59.4 落地的形状：闸门在 `UiTestHost` 里，而不是关掉并行

两种写法可选：

| 写法 | 代价 | 漏掉的后果 |
|---|---|---|
| 给每个用 headless 的测试类加 `[Collection("headless")]` | 要改 ~30 个类 | **漏一个就静默退回竞态**（而且新加的类谁也不会记得加） |
| **在 `UiTestHost.Run/RunAsync` 里包一层闸门**（本次采用） | 改 1 个文件 | 忘不掉 —— 入口只有一个 |

闸门本身的三条设计：

1. **带 2 分钟超时**：万一自锁，抛一条**点名**的异常（`DescribeSessionState()` 现场），
   而不是让宿主静默僵死到 90 秒非活动超时 —— 把"挂死"变成"失败"。
2. **同步路径支持同线程重入**（`[ThreadStatic]` 深度）：派发体里再调 `Run` 不该自锁。
3. **异步路径只做普通互斥**（续体可能换线程）：已核实两个使用点
   （`DispatchContinuationContractTests` / `HeadlessInputAndDialogUiTests`）都不在派发体里再调 `RunAsync`。

**代价实测为零**：程序集整跑 **18~21 s**（闸门未加时 19~22 s），连跑 3 次读数一致
（`gate-stab{1,2,3}.txt` 全部 `maxInFlight=1`）。

### 59.5 新增守卫：让它**自己制造**并发条件

`HeadlessDispatchContractTests.ConcurrentDispatchRequests_AreNeverInFlightTogether`
（放在 `DispatcherOwnershipContractTests.cs` 里 —— 同类"会话契约哨兵"）：

起 `max(4, ProcessorCount)` 个线程**同时**调 `Run`，然后三条断言：

1. **计数器活着**：这 N 次请求必须真的被数到（否则下面两条是"计数器死了所以永远绿"）；
2. **平台重建从不重叠**：`MaxConcurrentDispatches == 1`；
3. ②的另一种表述：`DispatchOverlapEvents` 不增长（失败信息直接指向"重叠了多少次"）。

**负控（已实测）**：临时摘掉闸门跑同一条判据 ⇒ **红在 ②**，报
`本进程历史最大值 20`（`dispatchguard-negctl.log`）。恢复闸门后同一判据绿。

### 59.6 关于 #45 本身：**本阶段没能复现挂死（0/5），所以不撤规避**

用**门禁 `[5]` 那一条原命令**、只去掉「排除 `DockPanelInvocationUiTests`」这一项，
跑 **5 次**：**5/5 绿**（`rate59-p{1..5}.log`，每次 App.UiTests 段 25~31 s ⇒ 那一类确实跑完并全绿）。

对照阶段 52 记的是「全量 **4/4 挂**」。⇒ 现在**复现不出来**了。可能原因有两个，本轮**没有定论**：

- 阶段 53 / 54 删掉 12 处双接线（#43）与 28 处宿主直订（#46）后，
  那一类里每次交互的处理器执行次数减半 —— 曾把 UI 线程占满数秒的那部分负载可能就此消失；
- 或者它本来就与负载/顺序强相关，5 次不够。

**⇒ 处置：独占进程的规避照旧保留。** 理由是纪律而不是偷懒：0/5 在统计上什么都说明不了
（若真实概率 p=0.5，5 次全绿的概率仍有 3%；rule of three 只把 p 上界压到 0.6）。
撤销规避要另立一次复现率研究。**但机制层面已经变了**：并发重建平台这条竞态既然被闸门堵上，
"同进程僵死"就少了一条最可疑的成因 —— 这条留给下一次专门做复现率的人当起点。

### 59.7 仍未闭环

- **#45 根因仍未确证**（本轮只证到"竞态前提存在且已消除"＋"当前 0/5 复现不出"），
  独占进程规避**保留**。
- **#47 的"偷手"仍未抓到**：并发派发被消除后，`s_uiThread` 的窗口还在（那是 `PerTest` 逐派发重置本身造成的），
  但"谁能落进窗口"这条候选（并发派发把 `SetupUnsafe` 摊到多个线程）已被闸门堵掉。若 #47 再犯，
  排查起点可以缩到"窗口期里还有谁在派发之外碰 Avalonia"。
- 阶段 58 的两条（`416` 的具体字段、整队 3,465 对不上整数档）不变。

### 59.8 门禁（提交**前**）

日志 `.workbuddy/tmp/ci-gate-59.log`，mtime **`19:13:19`**（代码与文档均**尚未提交** ⇒
这条只覆盖「工作区内容」，**不作为提交后验证**）。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误**（`[3]`） |
| 测试计数 | 大队 **3013**（44 / 44 / 75 / 87 / 4 / 315 / **2116** / **328**）+ `[6]` **8 + 2** = **3023** |
| 与阶段 58 比 | **3022 → 3023**，**差 1 = 本阶段新增的派发守卫**（`App.UiTests` 大队 327 → 328），其余逐程序集同数 |
| 行数 | **837 行**（与阶段 57 / 58 相同） |
| 墙钟 | **1 分 13 秒 —— 这条当时判为「不作数」，理由已更正**：原始理由写的是「与编辑文档并行跑、脚本抢 CPU」（文档不进构建输入）。**阶段 59 补记的三步对比否掉了这个理由**：58b / 58-post 的 `[3]` 构建各是 **10.84 / 10.91 s**（增量空构建），而这一跑的 `[3]` 是 **28.49 s**（代码刚改、真重建）⇒ `73 − 51 = 22 s` 与构建多出的 **~17.6 s** 同量级，**墙钟差异主要来自构建，不是 CPU 争用**。⇒ 新立一条：**墙钟差异先按各步骤读数归因，再怀疑争用**（并同时记 `/proc/loadavg`，见 59.11）。干净的那次见 59.9 |
| 隔离级别 | 门禁跑的是 **`PerTest`**（本阶段**未改**）+ **闸门生效**（`maxInFlight` 恒 1） |

### 59.9 门禁（提交**后**）

日志 `.workbuddy/tmp/ci-gate-59-post.log`，mtime **`19:17:21`**，晚于代码提交 `5189961`（`19:14:13`）
与文档提交 `e942106`（`19:14:25`）⇒ 这条覆盖的是**提交后的树**，可以当验证用。
（启动时刻约 `19:14:37`，即提交完成后 12 秒 —— 这一段刻意**不并行做别的事**，
理由正是下面那条异常。）

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误**（`[3]`，增量 10.67 s） |
| 测试计数 | 大队 **3013**（44 / 44 / 75 / 87 / 4 / 315 / **2116** / **328**）+ `[6]` **8 + 2** = **3023** —— 与 59.8 **逐程序集相同** |
| 行数 | **837 行**（与阶段 57 / 58 / 59.8 相同） |
| 墙钟 | **172 秒**（前三次：51 / 51 / 73 秒）—— **差异有确切成因，见 59.10** |

### 59.10 提交后那一跑的 UI 段异常（141 秒）：读数本身有效，但**这一条不能当"闸门变慢"**

同一棵树上，`[5]` 段 **`HmiEditor.App.UiTests` 的持续时间是 `2 m 21 s`**，而其余
**7 个程序集与 59.8 逐项逐毫秒相同**：

| 程序集 | 59.8（提交前） | 59.9（提交后） |
|---|---|---|
| `Simulator.Tests` | 44 条 / 150 ms | 44 条 / 150 ms |
| `Hmi.Hardware.Tests` | 44 条 / 226 ms | 44 条 / 226 ms |
| `Hmi.Communication.Tests` | 75 条 / 2 s | 75 条 / 2 s |
| `Device.Tests` | 87 条 / 4 s | 87 条 / 4 s |
| `RenderTests` | 4 条 / 8 s | 4 条 / 8 s |
| `AI.Tests` | 315 条 / 9 s | 315 条 / 9 s |
| `Core.Tests` | 2116 条 / 16 s | 2116 条 / 16 s |
| **`App.UiTests`** | **328 条 / 24 s** | **328 条 / 2 m 21 s** |

**由这张表可以直接推出一件事**：这**不是**"机器整体变慢"。若是 CPU 被抢、内存压力换页或磁盘抖动，
`Core.Tests` 那 2116 条不可能一条不差地仍是 16 s、`RenderTests` 仍是 8 s。
⇒ 慢的只有 `App.UiTests` **这一个进程内部**，而且**全部 328 条照旧全绿**（0 失败、无跳过）。

**这条异常是本项目历史上前所未有的**：把 `.workbuddy/tmp/` 里 56 份历史门禁日志的同一个读数
排开，UI 段的**取值范围是 13~31 秒、上界 31 秒**（`[5]` 段最长的三次：55 的 31 s、55-post 的
29 s、56 的 28 s）；`2 m 21 s` 是列表里唯一一次越出该范围约 **4.5 倍**的。

**⚠ 处置定调：不撤任何结论、不放宽任何断言 —— 但也不把"闸门零代价"这句话当成已被推翻。**
理由（纪律，不是偏好）：闸门确实在 59.8 那一跑里存在（`maxInFlight` 恒 1、UI 段 24 s），
也就是说**加了闸门之后也跑出过正常值**；单次异常既不能证明闸门有害，也不能证明无害。
按 55.8 立的规矩，**对间歇现象"有一次异常"和"有一次正常"同样是零信息**，
所以这里既不回滚也不结案，只做两件事：量复现率（59.11）、把现场留下来（59.12）。

### 59.11 复现率：同一条命令连跑 6 + 6 + 30 次，**0/42 复现**

用 59.10 那条**原样命令**（`dotnet test HmiEditor.slnx -c Release --no-build --no-restore` +
与门禁 `[5]` 相同的 filter），分三批（6 / 6 / 30）：

| 批次 | UI 段读数 | 墙钟 | 备注 |
|---|---|---|---|
| `rate59s-1..6.log` | **27 / 33 / 27 / 30 / 30 / 36 s** | 33~46 s | 每次都同时记了负载 |
| `rate60-1..6.log`（带 TRX 与诊断） | **21 / 21 / 21 / 22 / 22 / 22 s** | 22~25 s | `maxInFlight` 恒 1、`overlapEvents` 恒 0 |
| `rate60b-1..30.log`（同上 + `MemAvailable` 采样） | **20~23 s** | 22~25 s | **30/30 全绿**；`MemAvailable` 最小 **53.4 GB** |

**0/42。** 一个必须写清楚的**方法漏洞**（这不是读数问题，是记法问题）：
上面两批的墙钟（22~46 s）**互相不可比，也不能和 58.9 / 59.8 的 51 / 73 s 比**，
因为**负载不同**且**门禁日志里从来没记过负载**。第一批的负载从 `1.89 2.57 2.61` 一路爬到
`5.09 3.57 2.98`（我自己的连跑把负载推上去），同一批里 6 次的 UI 段就随之从 27 s 飘到 36 s。
⇒ 新立一条记法：**凡引用墙钟，同时记 `/proc/loadavg`；没有负载读数的墙钟只能同批内部比较。**

**复现率的上界（照 55.8 的算法，不自己估）**：**51 份历史门禁日志**（有 UI 读数的）
＋本阶段 **42 次补跑** = **93 次观测、1 次异常** ⇒ `p̂ ≈ 1.1%`，
rule of three 给 95% 上界 **`p ≤ 3/93 ≈ 3.2%`**。
**口径声明**：这是把「门禁里的 `[5]`」与「程序集单跑」**合并**算的上界；单看补跑的 42 次全绿，
rule of three 只压到 `p ≤ 7.1%`。两种条件严格说不该混，这里混的理由是：要判断的是
**"会不会再犯"**而不是"在哪种条件下更易犯"，合并给的是更宽（更保守）的上界。
⇒ 无论哪种算法都落在**个位数百分点**，**靠"再跑几轮"抓不到它** —— 这与 #45 的处置
（59.6：不靠复现、改量前提）是同一条经验。

### 59.12 把现场留下来：诊断从"一个总时长"扩成"能区分的两段"

59.10 那次之所以至今无法归因，不是因为难，而是因为**当时没有读数**：手头只有一个 `2 m 21 s`。
所以本阶段给 `UiTestHost` 里那个**默认关闭**的诊断（设 `HMI_DIAG_OUT=<文件>`）补了两个量：

| 新读数 | 回答什么 |
|---|---|
| `maxGateWaitMs`（＋是第几次派发） | **在闸门外排队**排了多久 ⇒ 指向闸门/线程池调度 |
| `maxBodyMs`（＋是第几次派发） | **某一个派发体自己**跑了多久 ⇒ 指向被派发的那段代码或框架 |

```csharp
// UiTestHost.cs：只在 DiagEnabled（设了 HMI_DIAG_OUT）时执行，
// 不设时派发路径与加诊断之前逐指令等价（并发计数那几条判据正是靠这个才可信）
private static readonly bool DiagEnabled =
    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HMI_DIAG_OUT"));
```

**新读数的基线已量到**（`ui60-1..6.trx` / `ui60b-1..30.trx` + 对应 `diag60*.txt`，程序集单跑）：

| 读数 | 6 次（`rate60`） | 36 次（`rate60` + `rate60b`） |
|---|---|---|
| 程序集墙钟 | **22~25 s** | **22~25 s** |
| `maxGateWaitMs` | **3,176 ~ 3,911 ms** | **3,404 ~ 5,885 ms** |
| `maxBodyMs` | **3,036 ~ 3,540 ms** | **3,144 ~ 5,751 ms** |
| 最慢用例（TRX） | `RenderResourceCacheTests.TextIsStillDrawn_WhenCachedFormattedTextOutlivesItsDispatch` **4.7~5.2 s**（6/6 次都在榜首或次席） | 同上（后 30 次未逐条统计榜首） |

⇒ 下次再出现 141 秒那种量级，`maxGateWaitMs` / `maxBodyMs` 会**当场指出是"排队"还是"某个体"**，
并且给出是第几次派发 —— 配 `--logger trx` 就能指名到用例。
**这条工具是"为下一次留的证据"，不是本次的结论。**

### 59.13 由 59.10 的表顺手排除的两条假设

| 假设 | 判据 | 结论 |
|---|---|---|
| 机器整体 CPU 竞争（别的进程在抢） | 同一次 `[5]` 里其余 7 个程序集**逐项逐毫秒相同** | **排除**（整体竞争不可能只慢一个进程） |
| 内存压力 / 换页 | `rate60b` 那批每跑采样 `MemAvailable` 最小值 = **54.4 GB**（共 64 GB） | **排除**（远未接近压力区） |
| 那次窗口里还有别的测试批在跑 | `stat` 全部阶段 59 测量日志：最后一份在 `19:12:00`，门禁 `19:14:37` 才启动 | **排除**（无重叠） |

⇒ 剩下的候选只能是**`App.UiTests` 进程内部**的东西。本阶段能立刻用 IL 一步否掉的一条见 59.14。

### 59.14 顺手否掉一条假设：渲染定时器「按平台泄漏线程」（IL 核实，**不成立**）

**假设（本来是最像的一条）**：`AvaloniaHeadlessPlatform` 按 `ShouldRenderOnUIThread()` 二选一定时器；
其中 `SleepLoopRenderTimer` 的 `LoopProc` 是一条**独立后台线程**
（IL：`new Thread(LoopProc)` + `IsBackground = true` + `_wakeEvent.WaitOne(帧间隔)`，
`_wakeEvent` 是 `AutoResetEvent`），而它停下来的唯一路径是 `Tick` 被置 null
（`set_Tick` 里 `_stopped = true` + `_wakeEvent.Set()`）。`PerTest` 是**逐派发重建平台** ⇒
若旧平台没人把 `Tick` 置 null，216 次派发就是 216 条常驻线程在按 60 fps 醒来 ——
这恰好能解释"同一次 `[5]` 里只有 `App.UiTests` 一个进程变慢"。

**判据（IL 一行定案）**：`AvaloniaHeadlessPlatformOptions::.ctor`

```il
IL_0001:  ldc.i4.s   60        // Fps = 60
IL_0003:  stfld      <Fps>k__BackingField
IL_0009:  ldc.i4.1             // ShouldRenderOnUIThread = true  ← 关键
IL_000a:  stfld      <ShouldRenderOnUIThread>k__BackingField
IL_0010:  ldc.i4.1             // UseHeadlessDrawing = true
IL_0011:  stfld      <UseHeadlessDrawing>k__BackingField
```

`AvaloniaHeadlessPlatform` 里那个分支的另一半（`IL_0024` 起）才是 `SleepLoopRenderTimer`；
`ShouldRenderOnUIThread` 为 true 时走的是 **`HeadlessRenderTimer`**，它的 `StartCore` 建的是
`DispatcherTimer(DispatcherPriority::UiThreadRender)` + `Interval = 1/Fps` + 一个 `_forceTick` 委托
—— **是挂在 Dispatcher 上的计时器，不是独立线程**。

**核实仓库侧**：全仓 `AvaloniaHeadlessPlatformOptions` 只有两处构造
（`UiTestHost.cs:491` 与 `RenderTestHost.cs:48`），**都只设 `UseHeadlessDrawing`**，
没有任何地方设 `ShouldRenderOnUIThread` 或 `Fps`（grep 零命中）⇒ 全走默认 `true`。

⇒ **假设不成立，不量为准**（省掉一次"加线程计数、重建、跑 30 轮"的测量）。
**但它把下一个该查的点露出来了**：`HeadlessRenderTimer` 那枚 `DispatcherTimer` 是
**注册在 Dispatcher 上**的；`PerTest` 每次重置 Dispatcher 之后，旧平台留下的这些计时器
归谁、还会不会被新 Dispatcher 触发 —— 这是候选，**不是本阶段的结论**。

### 59.15 本补记的两笔提交与门禁（附：一次「负载更高反而更快」的实证）

| 项 | 提交**前**（`ci-gate-59b.log`，mtime `19:40:03`） | 提交**后**（`ci-gate-59b-post.log`，mtime `19:41:27`） |
|---|---|---|
| 覆盖的树 | 工作区（改动**尚未**提交） | `4d3ad98` + `32ddd5f` 之后 |
| 门禁 | **7 步全绿** / `GATE_EXIT=0` | **7 步全绿** / `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误** | **0 警告 / 0 错误** |
| `[3]` 构建耗时 | **38.87 s**（真重建，代码刚改） | **11.22 s**（增量） |
| 计数 | 大队 **3013**（44/44/75/87/4/315/2116/**328**）+ `[6]` 8 + 2 = **3023** | 同（2116 / 328 / 8 / 2 逐项相同） |
| `App.UiTests` 段 | 29 s | 27 s |
| 墙钟 | **89 s**，负载 `1.80 → 5.91` | **58 s**，负载 `4.43 → 5.94` |
| 行数 | 837 | 837 |

**计数没变是对的**：本补记只加诊断、不加用例（`App.UiTests` 大队仍是 328）。

**这张表自己就是一条证据**：提交后那次是在**更高的负载**下跑的（跑前 `4.43` vs `1.80`），
墙钟却**更短**（58 s vs 89 s）—— 因为 `[3]` 从 **38.87 s** 的真重建变成了 **11.22 s** 的增量
（差 27.65 s，与墙钟差的 31 s 同量级）。⇒ **墙钟差异先按各步骤读数归因**：
只看总量，会把"构建多跑了 28 秒"读成"机器忙/争用"（这正是 59.8 当初犯的错，已就地更正）。

**⚠ 时序声明**：代码提交 `4d3ad98`（`19:40:23`）与文档提交 `32ddd5f`（`19:40:26`）
**早于**提交后门禁（`19:41:27`）⇒ 该读数覆盖的正是最终树（唯一例外是本节所在的这第三笔提交，
它只改 `docs/project-status.md`；`docs/` 不是门禁任何一步的输入 —— 与 58.10 同一条声明）。


## 阶段 60（#47 的「偷手」抓到了：`UiTestHost.Pump` 在派发之外**按调用线程**取 Dispatcher，实测把 `s_uiThread` 抢给调用线程 —— 已把它纳入派发闸门）— 2026-09-21

### 60.1 一句话结论

阶段 59 把「并发派发」这条偷手路径堵掉之后，59.7 记下的下一步是
**「窗口期里还有谁在派发之外碰 Avalonia」**。本阶段按这条线查，抓到一条**确定性**的：

`AvaloniaHeadlessPlatform.ForceRenderTimerTick`（`UiTestHost.Pump` 调的就是它）这条路径会
**按调用线程取 Dispatcher** —— IL 普查显示它的支路里有 `Dispatcher::get_CurrentDispatcher()`，
而 `DispatcherTimer` 的**三个构造函数**也都读它，`HeadlessRenderTimer.StartCore` 正是用单参构造
建的 `DispatcherTimer`。于是：

> **实测读数（探针，2026-09-21）：在派发之外调一次 `Pump(1)`，
> `s_uiThread` 的归属立刻从 `null` 变成调用线程**
> —— `调用线程=13 / 会话线程=7 / before=null / after=13`。

而 `Pump` **原先不在派发闸门里**（闸门只护 `Run<T>` / `Run` / `RunAsync`），
仓库里又有**几十处**在 `Run` 返回**之后**才调它（也就是跑在 xunit 工作线程上）。
⇒ 这正是 #47 那条「偶发红、抛点在平台初始化」的**偷手动作**：它若落在**在飞派发**的窗口里
（`ResetBeforeUnitTests()` 把 `s_uiThread` 清成 `null` 之后、会话线程第一次读它之前），
会话线程随后的 `VerifyAccess()` 就会抛 "different thread owns it"。

⇒ **修法：把 `Pump` 纳入同一把闸门**（会话线程上直接原地推进，见 60.4）。
新判据 `Pump_NeverRunsWhileADispatchIsInFlight` 自己制造并发条件，负控实测 **12/12 次并发**转红。

### 60.2 怎么找到它的：先把「谁会按调用线程取 Dispatcher」普查成一张表

不靠猜「哪一行看起来可疑」，而是把框架侧**所有**取 Dispatcher 的调用点列出来，再与
「仓库在派发之外会碰到的入口」求交集。

普查口径：在 `Avalonia.Base` 与 `Avalonia.Headless` 的 IL 里搜
`Dispatcher::get_UIThread` / `get_CurrentDispatcher`（这两个 getter 在 `s_uiThread == null` 时
**当场构造**一个 Dispatcher 并抢走它）。**结果：61 个调用点，落在 50 个「文件 × 方法 × 取法」组合里。**

与本仓库相关的关键几处：

| 调用点 | 取法 | 与本仓库的关系 |
|---|---|---|
| `AvaloniaHeadlessPlatform.ForceRenderTimerTick` | `get_CurrentDispatcher` | **`UiTestHost.Pump` 调的就是它** ← 本次抓到的 |
| `DispatcherTimer` 的**三个**构造函数 | `get_CurrentDispatcher` | `HeadlessRenderTimer.StartCore` 用**单参**构造建它 ⇒ 建定时器时就取 |
| `DefaultRenderLoop.Add` / `Remove` | `get_UIThread` | #47 的**受害点**（会话线程在这一步抛 `VerifyAccess`）—— 阶段 56 已核实 |
| `HeadlessWindowImpl.Show` / `Hide` / `Activate` / `Resize` | `get_UIThread` | 仓库里 `window.Show()` / `Close()` 都在派发体内 ⇒ 安全 |
| `LayoutManager.ExecuteLayoutPass` / `InvalidateMeasure` / `InvalidateArrange` | `get_UIThread` | 同上 |
| `ValueStore` / 绑定观察者（6 处） | `get_UIThread` | 属性变更路径，同上 |
| `HeadlessUnitTestSession.<DispatchCore>b0` | `get_UIThread` | **正常路径**：会话线程在派发体里自己取 —— 这正是"想要的状态" |
| `AvaloniaSynchronizationContext`（3 个构造）/ `MediaContext.get_Instance` | `get_CurrentDispatcher` / `get_UIThread` | 未在仓库测试里直接触达，仅登记 |

⇒ 「框架里到处都在按调用线程取 Dispatcher」这件事本身说明：**只能靠"谁在派发之外碰它"来收口**，
不可能靠"改框架"。普查把候选缩到 `Pump` 一条 —— 因为其余入口在仓库里都只出现在派发体内部。

### 60.3 判据是确定性的：不赌复现，直接读归属

#47 是偶发红（阶段 54 记 1 命中 / 之后连跑 3 次 0/3），赌它复现没有出路。这里换成一步就能定的读数：

```csharp
UiTestHost.Run(() => { });                          // 先把平台建起来（s_renderTimer 才有值）
var before = UiTestHost.AmbientUIThreadOwnerId();   // 只读反射，不构造 Dispatcher
UiTestHost.Pump(1);                                 // ← 嫌疑动作：在派发之外碰平台
var after = UiTestHost.AmbientUIThreadOwnerId();
```

读数（探针一次性打印，随后**删除**该探针，换成 60.5 的正式判据）：

```
[探针读数] 调用线程=13 / 会话线程=7 / Pump 前归属=null / Pump 后归属=13
```

`Pump 后归属 = 13 = 调用线程` ⇒ **抢手动作确定存在**，不是推断。
（这也是「只读探针」的价值：`AmbientUIThreadOwnerId()` 走内部 `TryGetUIThread()`，**只读不构造** ——
若改用 `Dispatcher.UIThread` 的 getter 去观测，观测动作自己就会抢走它、把要观测的缺陷造出来。）

### 60.4 修法：`Pump` 纳闸，但**会话线程上要旁路**

```csharp
public static void Pump(int ticks = 3)
{
    if (Environment.CurrentManagedThreadId == SessionThreadId)
    {
        TickRenderTimer(ticks);      // 会话线程：本来就在派发体里，不是"并发"
        return;
    }

    EnterGate();
    try
    {
        // 拿到闸门后这里必然是 0。它 > 0 只可能是"闸门没护住这条路径"——判据就看这个读数。
        if (Volatile.Read(ref _inFlight) > 0)
            Interlocked.Increment(ref _pumpDuringDispatch);
        TickRenderTimer(ticks);
    }
    finally { ExitGate(); }
}
```

**为什么不能"一律纳闸"**：绝大多数 `Pump` 调用点在派发体**里面**（会话线程上）。
那种情形下闸门正被**别的**线程（派发的调用者）持有 ⇒ 会话线程去 `Wait` 必然**自锁**。
而会话线程上「按调用线程取 Dispatcher」取到的正好是**会话线程自己** —— 不但无害，还是想要的状态，
所以那条路直接原地推进即可。

**判据怎么读"我在不在会话线程上"而不踩坑**：用 `UiTestHost.SessionThreadId`（每次派发时写下的线程 id，
一个普通 int），**不能**用 `Dispatcher.UIThread.CheckAccess()` —— 读那个 getter 本身就是犯案动作。

### 60.5 新判据与负控

```csharp
// HeadlessDispatchContractTests.Pump_NeverRunsWhileADispatchIsInFlight
var dispatch = Task.Run(() => UiTestHost.Run(() => Thread.Sleep(400)));  // 窗口敞开 400 ms
Thread.Sleep(80);
for (var i = 0; i < 12; i++) { UiTestHost.Pump(1); Thread.Sleep(10); }
// 断言：PumpDuringDispatchEvents 不增长（用 WhenAny 等待，不用 Wait/Result —— 门禁警告即失败）
```

| 形态 | 读数 | 结果 |
|---|---|---|
| 闸门在位（正控） | 并发事件 **0** | **绿**（2/2 共 948 ms） |
| 摘掉 `Pump` 上的闸门（负控） | 并发事件 **12 / 12** | **红**，现场 `dispatch#1, sessionCreated=True, sessionThread=17, callingThread=20` |

⇒ 判据有分辨力（不是"恒绿"），而且它**自己制造**并发条件（不依赖 xunit 恰好并行）。

### 60.6 代价：实测为零

| 项 | 读数 |
|---|---|
| 整个 `App.UiTests` 程序集 | **331/331 绿**（= 全程序集 330 条 + 本阶段新增 1 条判据）、**22 秒** |
| 加闸前的同一读数基线（阶段 59 补记，36 次） | **20~23 秒** |
| 墙钟（跑前负载 `3.36 2.20 1.73`） | 24 秒 |

⇒ 闸门只是把「推进渲染定时器」与「重建平台」排开，两者本来就在同一条 UI 线程上竞争，
所以**没有可测量的代价**（同 59.4 的结论：闸门 ≠ 关并行）。

### 60.7 仍未闭环

- **#47 的偶发红本身仍未复现**：本阶段只证到"又堵掉一条偷手路径"，**没证到"红不再出现"**。
  照 59.6 的纪律：不靠复现，靠把候选一条条排掉 —— 本阶段排掉的是「`Pump` 与在飞派发并发」。
- 普查表里**未在仓库测试里触达**的几处（`AvaloniaSynchronizationContext` 三个构造、
  `MediaContext.get_Instance`、`GestureRecognizers` / `TransformTrackingHelper`）**未逐条验证**：
  它们只在特定代码路径上才会走到 ⇒ 留作"下次红了的候选清单"。
- **让窗口不存在的根因修法未做**：本阶段做的是"把偷手排队"，不是"让窗口消失"
  （窗口是 `PerTest` 逐派发重置造成的，见 59.7）。

### 60.8 门禁（提交**前**）

日志 `.workbuddy/tmp/ci-gate-60.log`，mtime **`20:14:16`**（代码与文档均**尚未提交**
⇒ 只覆盖"工作区内容"，**不作为提交后验证**）。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误**（`[3]` 已用时间 **34.09 s**，真重建） |
| 测试计数 | 大队 **3014**（44 / 44 / 75 / 87 / 4 / 315 / **2116** / **329**）+ `[6]` **8 + 2** = **3024** |
| 与阶段 59 补记比 | **3023 → 3024**，**差 1 = 本阶段新增的那条判据**（`App.UiTests` 大队 328 → 329），其余逐程序集同数 |
| `App.UiTests` 段 | **23 s**（与阶段 59 补记那 36 次基线的 20~23 s 同档） |
| 行数 | **837 行** |
| 墙钟 / 负载 | **81 s**，负载 `2.96 → 5.85`（按 59.11 新立的记法，墙钟必须带负载） |

### 60.9 门禁（提交**后**）

日志 `.workbuddy/tmp/ci-gate-60-post.log`，mtime **`20:18:58`**，
**晚于**两笔提交 `24e31af`（`20:15:21`）/ `12b467e`（`20:15:24`）⇒ **本条才是提交后验证**
（60.8 那条只覆盖工作区内容）。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误**（`[3]` 已用时间 **15.80 s**，增量空构建） |
| 测试计数 | 大队 **3014**（44 / 44 / 75 / 4 / 87 / 315 / **2116** / **329**）+ `[6]` **8 + 2** = **3024** |
| 与 60.8（提交前）比 | **逐程序集逐条同数**（3014 + 10 = 3024，失败全 0）；只有耗时微动：UI **23 → 22 s**、Core **12 → 11 s**、Render 4 → 3 s |
| `App.UiTests` 段 | **22 s**（与 60.6 单跑读数、59 补记那 36 次的 20~23 s 基线同档） |
| 行数 | **837 行**（与 60.8 同） |
| 墙钟 / 负载 | **57 s**，负载 `1.07 → 6.64` |

⇒ **提交后无回归**：计数、失败数、行数、警告数四项与提交前**完全一致**；
唯一差异是耗时 —— 本次 `[3]` 是**增量空构建 15.80 s**、提交前是**真重建 34.09 s**，
墙钟 57 s 对 81 s（差 24 s）与构建差的 18.3 s 同量级。
按 59.11 / 坑 20 附 A 的记法：**墙钟差异先按各步骤读数归因，再怀疑争用** —— 这里归因到构建，
且提交前那次负载还更高（`2.96 → 5.85` 对 `1.07 → 6.64` 的跑前值 2.96 vs 1.07）⇒ 不存在"负载解释"。

---

## 阶段 61（#42 闭环：给「这一族判据绿的时候没有读数」做一条**读数出口** —— 两条通道，其中一条在门禁的 minimal logger 下也拿得到）— 2026-09-21

### 61.1 一句话结论

登记 #42 的原话：`Assert.True(cond, message)` 的 message **只在红的时候打印** ⇒
阶段 49/50/51/52 四层的动作面判据**全绿**时，「面解析出几个、真点了几个、各自在哪棵树里」
拿不到；要拿只能**临时注入一个正控把下界抬高**，逼它转红。

本阶段加一个统一出口 `JudgeReadings`，**同一份内容走两条通道**：

| 通道 | 打开方式 | 可见性 |
|---|---|---|
| `ITestOutputHelper` | 判据类构造函数注入 | `--logger "console;verbosity=detailed"` 可见（**门禁的 minimal 下不可见**） |
| 文件 | `HMI_READINGS_OUT=<路径>` | **追加落盘** ⇒ 门禁的 minimal logger 下**也拿得到**（沿用 `HMI_DIAG_OUT` 同款约定） |

**默认零副作用**：两条都不接时直接返回（不取时刻、不分配、不开文件）——
与 `UiTestHost.DiagEnabled` 同一条理由：**观测量不许改变被测对象**。
**刻意不做成断言**（#42 的原文）：读数只是**证词**；把「65 / 58」这类数字写进断言，
下一个人就会**为改数字而改测试**。

### 61.2 收益：全绿、**没开任何正控**，数字自己落下来

命令：`HMI_READINGS_OUT=$PWD/.workbuddy/tmp/readings-demo.txt dotnet test …App.UiTests -c Release
--filter "…MainWindowMenuInvocationUiTests|…MainWindowToolbarInvocationUiTests"` ⇒ **6 条全绿 / 11 s**。

落盘内容（节选，原文见日志）：

```
=== 主窗口菜单项（点击行为面） ===
    面：菜单树上具名 MenuItem 62 个；表内 62 个（计划点 48 / 不点 14）
    桶分布：Conditional 2 / Popup 18 / Silent 28 / Skipped 14
    实测：真点 48 个；红项 0 个
=== 主窗口工具栏（NameScope 按名解析面） ===
    面：按名解析 65 个；逻辑树已渲染 21 个，可视树 32 个
    BtnUserManager → +1：UserManagerView
    BtnNewProject（跳过）
    …
```

⇒ 阶段 51 那条「65 个解析、58 个真点」从**推断**变成**读数**，且不再需要注入正控。
第二条通道也实测过：`--logger "console;verbosity=detailed"` 下同一段文本出现在该用例的输出里。

### 61.3 判据与负控

`JudgeReadingsTests`（**7 条**）：

| 用例 | 性质 | 结果 |
|---|---|---|
| `Write_LandsInTheFileWithAScopeHeader` | 正控 | 文件出现 `=== scope ===` 与两行内容 |
| `Write_WithNoReadings_LeavesNoEmptyBlockBehind` | **负控（真分支）** | 空读数 ⇒ **不建文件**（否则读数文件堆满只有标题的空块） |
| `Write_WithNeitherSink_IsSilentAndSafe` | **负控** | 未给 output、未给路径 ⇒ 不抛，且临时目录里**找不到本用例的标记文字** —— **这是门禁里实际走的那条路**。⚠ 2026-09-21（阶段 62）更正：本条原判据比的是「临时目录文件数前后不变」，那是对**共享目录**做快照比较（判据自身不自治）⇒ 并发下随机红（门禁实测 `1423 → 1424`）；已改为按**内容标记**找，见 62.3 |
| `Write_AppendsRatherThanTruncates` | 正控 | 两段都在且顺序正确（"覆盖"是这条出口最容易被写成的错形态） |
| `Write_ToAnUnwritablePath_DoesNotThrow` | **fail-soft** | 父目录不存在 ⇒ 不抛（读数不许变成新的失败源，否则"门禁红"这件事失真） |
| `BothSinks_ReceiveTheSameText` | 一致性 | output 收到的文本 == 文件里的文本（不同源比没有读数更坏） |
| `EveryActionSurfaceJudge_HandsItsReadingsToTheSink` | **静态守卫** | 面 = 目录里**所有** `*InvocationUiTests.cs`（枚举，不写死 4 个名字） |

静态守卫的**负控**（把 `DialogInnerInvocationUiTests` 的出口调用注释掉）：

```
摘除前：DialogInnerInvocationUiTests.cs 真实调用 1 处
摘除后：真实调用 0 处 / 注释掉 1 处
→ 红：这些动作面判据类没有把读数交给出口（登记表 #42）：DialogInnerInvocationUiTests.cs
      ⇒ 并打印出修复模板（JudgeReadings.Write(_out, "<scope>", readings) + 注入 ITestOutputHelper）
还原后：真实调用 1 处 / 注释掉 0 处
```

⇒ 守卫**有分辨力**、**点名到文件**，而不是只报"有一处没接"。

三条设计取舍：

- **面用枚举而不是写死名单**：这四层是逐个长出来的，第五个一定会再来一个 ——
  写死名单等于把「新增判据忘了接出口」重新变成静默失效。另设「面 ≥ 4」的防退化断言
  （面为空时"全部满足"是白给的）。
- **匹配前先剥注释**（`SourceScanner.StripComments`）：本文件与那四个类的注释里都写着
  `JudgeReadings.Write`，不剥注释，规则会被**自己的文档**判为已满足。
- **落盘路径做成参数**（`Write(…, string? filePath)`）：否则「未设环境变量时不落盘」是**同义反复**
  （不设变量自然不知道往哪儿写），测不出任何东西。做成参数后，
  「空读数不建文件」「不可写路径不抛」才是能确定性复现的真分支。

### 61.4 新补的一处缺口：菜单那条**连红的时候都没有读数**

`MainWindowMenuInvocationUiTests` 此前只有一个 `errors` 列表 ——
面解析几个、真点几个、按桶各几个，**一个都没落下来**（工具条 / 对话框内 / 停靠面板三层都有
`readings`，只有它没有）。本阶段先补三行读数（面 / 桶分布 / 实测真点），再接出口：

```
    面：菜单树上具名 MenuItem 62 个；表内 62 个（计划点 48 / 不点 14）
    桶分布：Conditional 2 / Popup 18 / Silent 28 / Skipped 14
    实测：真点 48 个；红项 0 个
```

### 61.5 四个接出口的类

| 类 | 情形 |
|---|---|
| `MainWindowMenuInvocationUiTests` | **新补读数**（此前连红时都没有） |
| `MainWindowToolbarInvocationUiTests` | 已有 `readings`，接出口 |
| `DialogInnerInvocationUiTests` | 已有 `readings`，接出口（scope 带 `hostTypeName`，Theory 各参数分开落盘） |
| `DockPanelInvocationUiTests` | **两处**：三段读数（行为 / 枚举 / NameScope）+ 桶分布（后者本来只在那条 Assert 的 message 里） |

### 61.6 代价

| 项 | 读数 |
|---|---|
| 整个 `App.UiTests`（排除独占类） | **336/336 绿**（= 329 + 本阶段 7 条）、**21 s**（墙钟 25 s，跑前负载 `2.96 2.85 2.41`） |
| `DockPanelInvocationUiTests` 单跑 | **2/2 绿**、1 s |
| 阶段 60 提交后门禁的同一读数 | `App.UiTests` 大队 **329 条 / 22 s** |

⇒ 默认路径（门禁走的那条）只多一次 `lines.Count == 0` 判断 ⇒ **代价为 0**。

### 61.7 仍未闭环

- **只覆盖「动作面」这一族**：其它"其实也在绿的时候需要读数"的判据（基准/预算类、静态守卫类）
  **未接**。它们各有各的读数口（如 `CanvasRenderBaselineTests` 已用 `ITestOutputHelper`），
  本阶段**刻意不统一** —— 没有证据说统一比各自为政好，而且 `JudgeReadings` 的面越宽，
  它自己的判据越难写。
- **`HMI_READINGS_OUT` 未接进门禁脚本**：门禁默认仍**不落**读数文件。要用时自己带环境变量跑
  （`HMI_READINGS_OUT=<路径> ./scripts/ci-gate.sh`）。**刻意**：门禁的默认配置应保持
  "只判、不写额外产物"，否则会多出一个**会被当成证据**的中间文件。
  脚本侧已核实它会一路传下去（`scripts/ci-gate.sh` 直接调 `dotnet test`、全文无 `env`/`export`
  ⇒ 环境继承），且**本条已端到端跑过** —— 见 61.9：提交后门禁带着它跑，读数文件**由门禁自己写出**
  （181 行 / 10 个块）。

### 61.8 门禁（提交**前**）

日志 `.workbuddy/tmp/ci-gate-61.log`，mtime **`20:26:21`**（代码与文档**均尚未提交**
⇒ 只覆盖"工作区内容"，**不作为提交后验证**）。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误**（`[3]` 已用时间 **10.92 s**，增量空构建） |
| 测试计数 | 大队 **3021**（44 / 44 / 75 / 4 / 87 / 315 / **2116** / **336**）+ `[6]` **8 + 2** = **3031** |
| 与阶段 60 提交后比 | **3024 → 3031，差 +7 = 本阶段新增的那 7 条判据**（`App.UiTests` 大队 **329 → 336**），其余**逐程序集逐条同数**、失败全 0 |
| `App.UiTests` 段 | **21 s**（与 59 补记那 36 次的 20~23 s 基线、60.6 的 22 s 同档） |
| 行数 | **837 行**（与 60.8 / 60.9 同） |
| 墙钟 / 负载 | **50 s**，负载 `2.95 → 4.45` |

### 61.9 门禁（提交**后**，**并且带着读数出口跑**）

日志 `.workbuddy/tmp/ci-gate-61-post.log`，mtime **`20:28:07`**，
**晚于**两笔提交 `36d0204`（`20:27:04`）/ `499dc5c`（`20:27:10`）⇒ **本条才是提交后验证**。

这一次**额外**带了 `HMI_READINGS_OUT` 跑，一次拿两件事：

<table>
<tr><th>项</th><th>读数</th></tr>
<tr><td>门禁</td><td><b>7 步全绿</b>（banner 数 = 7）/ <code>GATE_EXIT=0</code></td></tr>
<tr><td>构建</td><td><b>0 警告 / 0 错误</b>（<code>[3]</code> 已用时间 <b>10.66 s</b>，增量空构建）</td></tr>
<tr><td>测试计数</td><td>大队 <b>3021</b>（44 / 44 / 75 / 4 / 87 / 315 / <b>2116</b> / <b>336</b>）+ <code>[6]</code> <b>8 + 2</b> = <b>3031</b></td></tr>
<tr><td>与 61.8（提交前）比</td><td><b>逐程序集逐条同数</b>（3021 + 10 = 3031，失败全 0）—— 只有耗时微动（UI 21 → 25 s、Core 12 → 13 s）</td></tr>
<tr><td>行数</td><td><b>837 行</b></td></tr>
<tr><td>墙钟 / 负载</td><td><b>53 s</b>，负载 <code>2.90 → 7.25</code></td></tr>
</table>

**第二条证据：读数文件是门禁自己写出来的。**
`.workbuddy/tmp/readings-61-gate.txt` = **181 行 / 7,579 字节 / 10 个块**：

```
=== StyleEditorView 内部按钮（宿主对话框内按名解析面） ===
=== ScriptEditorView 内部按钮（宿主对话框内按名解析面） ===
=== UserManagerView 内部按钮（宿主对话框内按名解析面） ===
=== DeviceManagerView 内部按钮（宿主对话框内按名解析面） ===
=== RecipeEditorView 内部按钮（宿主对话框内按名解析面） ===
=== AnimationEditorView 内部按钮（宿主对话框内按名解析面） ===
=== 主窗口菜单项（点击行为面） ===
=== 主窗口工具栏（NameScope 按名解析面） ===
=== 停靠面板内按钮（行为 + 枚举 + NameScope 三面） ===
=== 停靠面板内按钮（桶分布） ===
```

前 8 个块来自 `[5]`、后 2 个来自 `[6]` ⇒ **环境变量确实穿过 `ci-gate.sh` 传到了测试进程**，
而且**在门禁的 `verbosity=minimal` 下照样拿到了读数** —— 这正是 #42 要的东西。

⚠ **一次跑不能把两件事分开说**：这一次同时「带了读数出口（多写 7.6 KB / 10 次追加）」且
「跑后负载更高（`7.25` 对 `4.45`）」，所以 UI 段 21 → 25 s 归因到哪一项**本条读数不足以判定**。
按 59.11 / 坑 20 附 A 的记法如实记着，不下结论；**判据相关的读数（计数、失败数、行数、警告数）
与提交前完全一致**，那才是"无回归"的判据。



## 阶段 62（#35 的字典侧：一个字典键「能不能被任何机制取到」此前没人查 —— 建七条机制的可达性判据，逐条取证后从两本字典清掉 19 个陈旧键）— 2026-09-21

### 62.1 一句话结论

登记 #35（「1267 个 `Lang` 常量无静态引用」）有**两半**，此前只锁了一半：

| 半边 | 守卫 | 此前状态 |
|---|---|---|
| 常量没人读（`Lang.X` 无人引用） | `LangConstantUsageGuardTests`（只减不增） | 已有 |
| **字典键没人查**（键在字典里，但**没有任何机制能取到它**） | —— | **此前没有任何守卫** |

`LangKeyCoverageTests` 查的是**常量→字典**（正向）、`PluginTextLocalizationGuardTests` 查**派生键→字典**（正向）、
`LocalizedFormatCallSiteTests` 查**占位符**、`XamlResourceKeyClosureTests` 查 **axaml 引用键有没有定义** ——
**谁也不查「字典里这个键到底能不能被任何机制取到」**。于是「常量名与字典键都被按陈旧的名字写成对」的条目
可以一直躺在两本字典里，而且**四个守卫全绿**。

本阶段补上第二半：`LocalizationKeyReachabilityGuardTests`（**5 条**，新文件）。

### 62.2 判据与七条机制

**主判据**：字典键必须落在**七条可达机制的并集**里，或进 `KeptUnreachable`（差异表，每条带理由）。
两侧都做相等判定 ⇒ 表里的键一旦**变得可达**、或**从字典里消失**，本判据也红（防过期抄写）。

| 机制 | 取法 | 面 | 下界 |
|---|---|---|---|
| M1 | `Lang` 常量值（反射） | 1915 | 1800 |
| M2 | `.axaml` 里的 `{res:Loc K}` | 1113 | 900 |
| M3 | 插件族派生（**复用** `PluginTextLocalizationGuardTests.DerivedKeys()`，不另抄一份规则） | 26 | 25 |
| M4 | `ConfigFieldInventory.NotWired[].NoteKey`（`cfg.note.类型.字段`） | 30 | 20 |
| M5 | 脚本诊断码（`*Code` 常量反射 + Core 源码里的 `Code = "…"` 字面量） | 19 | 15 |
| M7 | 枚举名小写派生（`reslib.scenario.*` / `reslib.type.*`） | 12 | 12 |
| M6 | 源码里的**整键字面量**（代理指标，**排在最后**） | 113 | 100 |

读数来自 `HMI_READINGS_OUT` 落盘（阶段 61 的出口，本阶段直接用上了）。

三条设计要点：

- **M7 是首跑逼出来的**：首跑报 **31** 个不可达键，其中 **12** 个是
  `reslib.scenario.*` / `reslib.type.*` —— 视图里是
  `$"reslib.type.{t.ToString().ToLowerInvariant()}"` 这种**模板拼接**，M6（整键字面量扫描）
  **原理上**看不见。⇒ 补 M7，31 降到 **19**。这正是「表不全 ⇒ 报红」想要的失效方向。
- **M6 排在最后**并明写它是代理：M6 只对「前面各条没覆盖到」的键做子串匹配
  （省掉 2000+ 个键 × 6 MB 语料的纯浪费，并集不变）。若把 M6 排在 M7 前面，
  那 12 个键会先被 M6 判成不可达、再无谓扫一遍语料，最后靠 M7 补回来 ——
  并集不变，但**读数会带上假的来源标记**。
- **元判据**：七条机制的面各自 ≥ 下界（防某条机制悄悄退化成空集、主判据跟着变成「只靠 M6」）；
  另有双向自检（已知活的 7 个键必判活；已清理的陈旧键不许被任何机制**凭空产出**）
  与「两本字典键集合必须一致」。

### 62.3 顺带修掉阶段 61 判据自身的一处**非自治断言**

提交前第一次门禁（`ci-gate-62.log`，mtime `20:50:05`）**红在阶段 61 的判据上**，不是本阶段的新代码：

```
失败 HmiEditor.App.UiTests.JudgeReadingsTests.Write_WithNeitherSink_IsSilentAndSafe
   Assert.Equal() Failure: Values differ
Expected: 1423
Actual:   1424
```

原判据是 `Assert.Equal(before, Directory.GetFiles(TestTempDir.Root).Length)` ——
而 `TestTempDir.Root` 是**整个测试进程共享、被并行写入**的目录，前后快照比较**不自治**：
并行用例随时往里加文件 ⇒ 随机红。**单跑全绿、进门禁就红**，正是这个形状。

⇒ 改成**按内容找标记**：那段文字只有本用例会写，并行用例造不出它，所以并发再大也不会误红；
而默认路径一旦真的落了盘，它必然被找到。扫描面**刻意不递归** ——
`TestTempDir.Root` 会按 `HMI_TEST_TMPDIR → TMPDIR → Path.GetTempPath() → 仓库 .hmi-test-tmp/`
逐个探测，递归遍历在别的机器上可能走进整个 `/tmp`。

**判据改了就重做负控**（`.workbuddy/tmp/negctl62b.py`）：给实现注入一个「默认落盘路径」
（落在 `TestTempDir.Root` 顶层）⇒ **红并报出泄漏文件名**，还原后整个类 **7/7 绿**、泄漏文件已清。

### 62.4 19 个不可达键：逐个取证后**全部**清掉

处置原则：这些键**要么**是陈旧残留（界面已改用别的键）**要么**是该显示却没接线的文案 ——
两种情况都要处置，不许默默留着。逐个取证后这 19 个**全部属于前者**，
从两本字典各删 19 行（共 **38 行、0 新增**；键数 2294 → **2275**，两本仍然同集合）。

| 族 | 键数 | 取证（两面以上） |
|---|---|---|
| `app.splash.*` | 9 | **改名前的老键族**。活着的是 `splash.*`（6 个 `Lang` 常量一一对应：`SplashSubtitle` / `SplashNewProject` / `SplashNewProjectHint` / `SplashOpenProject` / `SplashOpenProjectHint` / `SplashRecent`）；错误文案另有两处替代 —— `status.file.not.found`（`MainWindow.axaml.cs:998`）、`status.open.failed`（`:1168`）；退出按钮是**纯图标**（`HmiCloseGlyph` 的 `Path`），没有文案 |
| `comm.slave.list` / `.empty` | 2 | 树里**子设备是连接的子节点**、单一表头是 `comm.conn.list`；既无独立的「子设备」列表头，也没有空态元素（连 `comm.conn.empty` 都不存在） |
| `reslib.tt.builtin` / `.custom` / `.place` + `reslib.place.title` + `reslib.version` | 5 | `ResourceLibraryView` 用代码搭卡片：**无 `ToolTip`、无版本徽标、无表头**；放置靠双击/拖拽（`ResourceLibraryView.axaml.cs` 全文无 `ToolTip.SetTip`，`.axaml` 里只有 `reslib.search.hint` 一处） |
| `prop.param.pipe.direction.diagonal` / `.flowdirection.both` / `valve.type.check` | 3 | **死枚举值**，两面独立取证：① `PropertyPanelView.axaml` 里没有对应的 `ComboBoxItem Tag`；② 渲染/代码生成 `switch` 里没有对应的 `case` —— `PipeDirection` 只认 `"Vertical"`、`PipeFlowDirection` 只认 `"Reverse"`、valve 只认 ball/gate/butterfly/globe |

**取证面**：全库（排除点目录与 `firmware/`）里，除两本字典与本守卫自身外，这 19 个键**零出现**；
另外单独核过**没有**按这些前缀做的键拼接（只有 M7 那两处 `$"reslib.{…}"` 模板）。

⚠ **取证时踩到的环境坑（值得记住）**：`firmware/` 下挂着**整套 Tina Linux SDK**
（`Tina-Linux` + `Tina-SDK`，含 `qtwebengine` / `chromium` / 工具链）——
实测 **1,743,815 个文件**。对仓库根做全量 `os.walk` + 逐个 `open()` 会被沙箱 **SIGTERM（退出码 137）**，
且**输出为 0 字节**（Python 块缓冲没来得及 flush，看起来像「什么都没干」）。
真正的元凶还有一层：点目录（`.nuget` 3900 / `.Trash-0` 435 / `.hmi-test-tmp` 1495）里
有沙箱挂住的 bind-mount，`open()` 会卡死。⇒ 分析面必须是
`src/ + tests/ + docs/ + tools/ …`，**排除点目录与 firmware**。

### 62.5 一条陷阱：判据被**自己的文档**满足了

`LangConstantUsageGuardTests` 收集 `src/`+`tests/` 里**所有**标识符（`[A-Za-z_][A-Za-z0-9_]*`，
**含字符串字面量与注释**），常量名只要作为单词出现就算「被引用」。本守卫的类注释里引了
P0-5 缺陷的那个**真实常量名**作例子 ⇒ 未引用常量数立刻从 **1267 掉到 1266**，
门禁报 `无静态引用的 Lang 常量个数 1266 ≠ 基线 1267`。

**这是「名字即引用」类判据的通病**（`LangConstantUsageGuardTests` 自己的注释里已经写过一次：
它失败信息里会列常量名，所以它必须把自己排除出扫描面）。处置：注释里**只写字典键（小写）**、
不写常量名；并把这条陷阱写进本文件顶部，免得下一个人重踩。

### 62.6 负控与代价

负控 4 条 + 正控 1 条（`.workbuddy/tmp/negctl62.py`，**5/5 成立**）：

| # | 注入 | 应当红在 |
|---|---|---|
| ① | 两本字典各加一个没人读的键 | 主判据（**并点名该键**） |
| ② | 把**可达**键写进 `KeptUnreachable` | 反过期判据 |
| ③ | 把**不存在**的键写进 `KeptUnreachable` | 反过期判据（走另一个分支） |
| ④ | 只往 `zh-CN` 加键（`en-US` 不加） | 两本字典一致性判据 |
| 正控 | 全部还原后跑整类 | **5/5 绿** |

⚠ 负控脚本自己踩过一个坑、也已修：还原**不能用** `shutil.copy2` —— 它连 **mtime** 一起复制，
还原后源文件比上次编译产物还「旧」⇒ MSBuild 判定无需重建 ⇒ 正控实际跑的是**负控那次的二进制**，
于是正控**假红**。改成「写回原文」（写完 mtime = 现在）即消除。

| 项 | 读数 |
|---|---|
| 新增判据 | 5 条（`App.UiTests` 大队 336 → **341**） |
| 新增机制的实现代价 | M1–M7 各扫一遍 `src/`+`tests/`；M1/M2 走已有扫描面，M6 只对**未覆盖**的键做子串匹配 |
| 单跑本类 | **5/5 绿**、`413 ms`（整类跑） |
| 字典 | 各 2294 → **2275** 键、两本同集合；删除 **38 行 / 0 新增** |

### 62.7 仍未闭环

- **#35 的常量侧没动**：本阶段只做字典侧。1267 个无静态引用的常量里，
  有多少是「键是死的」、有多少是「键活但常量没人读」（如 `param.*` 家族，运行时推导）——
  **仍未逐个判定**（`LangConstantUsageGuardTests` 的注释已声明那是代理指标）。
- **`KeptUnreachable` 当前为空**：19 个键全部判为陈旧并删除 ⇒ 表里没有豁免项。
  表**留着**（判据两侧都在，负控②③证明它有效），给将来「确实该保留的不可达键」用。
  **不给空表硬塞条目** —— 「进表等于把缺陷合法化」，没理由的条目不进。
- **M6 是代理**：拼接出来的键它看不见（M7 只覆盖「前缀→枚举」两处）。
  新增一处别的形态的模板拼接时，主判据会**红在那些新键上**（这正是想要的失效方向），
  维护者需补一行到 `EnumDerivedPrefixes` 或新增机制。
- **取证面不含 `firmware/`**：M1–M7 只扫 `src/`+`tests/`。若某个键只被固件侧引用，本判据判不出来
  （现阶段固件不消费字典键）。

### 62.8 门禁（提交**前**）

**第一次跑是红的**（`ci-gate-62.log`，mtime `20:50:05`）—— 红在 62.3 那条非自治断言上，
它的价值在于**留下了复现读数** `Expected: 1423 / Actual: 1424`。
修掉并重做负控后重跑：

日志 `.workbuddy/tmp/ci-gate-62b.log`，mtime **`20:52:37`**，
**早于**提交 `fab58e1`（`20:53:08`）⇒ 只覆盖「工作区内容」，**不作为提交后验证**。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 构建 | **0 警告 / 0 错误** |
| 测试计数 | 大队 **3026**（44 / 44 / 75 / 87 / 4 / 315 / **2116** / **341**）+ `[6]` **8 + 2** = **3036** |
| 与阶段 61 提交前比 | **3031 → 3036，差 +5 = 本阶段新增的那 5 条判据**（`App.UiTests` 大队 **336 → 341**），其余**逐程序集逐条同数**、失败全 0 |
| `App.UiTests` 段 | **25 s** |
| 行数 | **837 行** |
| 墙钟 / 负载 | 负载 `5.30 → 6.75`（跑前读数，见 59.11） |

### 62.9 门禁（提交**后**，**并且带着读数出口跑**）

日志 `.workbuddy/tmp/ci-gate-62-post.log`，mtime **`20:56:31`**，
**晚于**两笔提交 `fab58e1`（`20:53:08`）/ `69a57be`（`20:55:32`）⇒ **本条才是提交后验证**。

这一次**额外**带了 `HMI_READINGS_OUT` 跑，一次拿两件事：

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0` |
| 测试计数 | 大队 **3026**（44 / 44 / 75 / 87 / 4 / 315 / **2116** / **341**）+ `[6]` **8 + 2** = **3036** |
| 与 62.8（提交前）比 | **逐程序集逐条同数**（3026 + 10 = 3036，失败全 0）⇒ 无回归 |
| 行数 | **837 行** |
| 墙钟 / 负载 | 负载 `3.48 → 3.99`（**跑前**读数，见 59.11） |

**第二条证据：读数文件是门禁自己写出来的。**
`.workbuddy/tmp/readings-62-gate.txt` = **189 行 / 7,912 字节 / 11 个块**
（阶段 61 是 181 行 / 7,579 字节 / 10 个块 ⇒ **+1 个块 = 本阶段新接的那条面读数**，字节 +333）：

```
=== 本地化键可达性（七条机制的面） ===
    M1-Lang 常量         面  1915
    M2-axaml res:Loc   面  1113
    M3-插件族派生           面    26
    M4-cfg.note 清单     面    30
    M5-脚本诊断码           面    19
    M6-源码字面量           面   113
    M7-枚举名派生           面    12
=== StyleEditorView 内部按钮（宿主对话框内按名解析面） ===
…
```

⇒ 环境变量**再次**穿过 `ci-gate.sh` 传到测试进程，新判据的七条面读数**在门禁的
`verbosity=minimal` 下也拿得到**。这也顺带说明阶段 61 那条出口是可扩展的：
本阶段新建的判据类只要接一行 `JudgeReadings.Write(_out, …)` 就进了同一条通道。

   **已不成立（2026-09-17 复核）**：阶段 0 的修复早已按主题提交，工作区干净。
   **未闭环项的唯一权威登记表是 `docs/2026-09-17-open-items-register.md`**
   （阶段 13 全量复核产出：17 条原登记为"未开工"的实测早已闭环；仍开着的项按
   "需产品决策 / 需先立功能 / 软件侧可验证 / 环境残留" 四类分组并附证据）。
   下面各条保留作为历史脉络，**状态以该登记表为准**。
2. 需要补齐模拟器和固件动态绑定、报警、趋势闭环（阶段 1/2）；**权限**已下沉到设备链路（阶段 4，2026-09-16）；**真实凭据校验**已闭环（阶段 6，2026-09-16）；**导出模拟器工程的链接自洽**已闭环（阶段 5，2026-09-16，见 `docs/2026-09-16-simulator-export-link-selfcontained.md`）；**运行时用户表已有编辑器 UI**（阶段 7，2026-09-16，见 `docs/2026-09-16-runtime-user-editor-ui.md`）；**会话超时 + 审计落盘**已闭环（阶段 8，2026-09-16，见 `docs/2026-09-16-session-timeout-and-audit.md`）；`RuntimeConfig` 的 10 个字段里已有 3 个真到设备（阶段 9，2026-09-16，见 `docs/2026-09-16-runtime-config-sinking.md`，另 5 个未下发、2 个描述本身错已修）；**AI 流式响应**已闭环（阶段 11，2026-09-17，见 `docs/2026-09-17-llm-streaming-sse.md`；**遗留**：并行 tool_call —— 属独立架构改动，未做）；**运行时用户表的导出前拦截**与**AI 工具不再广告死字段**已闭环（阶段 12，2026-09-17，见 `docs/2026-09-17-config-honesty-export-gate-and-ai-tool.md`；**遗留**：两个死字段的补执行路径仍是新功能、`EnableHmiReboot` 面板控件未标注系清单既有设计决定）。
3. 需要完成 WavePI-S6 触摸**实机四角校准**（量程默认值已按板级 DTS 修正为 800×480，仍需实机确认边缘）、RS485、长稳和回滚验收。
4. ~~视图 `async void` 处理器缺少用户可见提示~~ —— 已完成：引入 `UiOperationGuard` 统一收口，仓库内 `async void` 从 51 处降到 35 处，**未受保护的数量为 0**（35 处全部自带 try/catch 或走守卫）。仍保留的 35 处由各自 try/catch 兜底；全局 Dispatcher 处理器降级为最后一道防线。
5. BoardProfile 单一来源（原阶段 1 第 7 项）待 T113 交叉链验证；触摸量程已先行板级化。
6. `MainWindow` 已完成七步治理（Dock 诊断、Git 对话框、编译导出流水线、`WireEvents` 分域、预览运行时平移、预览运行时服务化、**快捷键/右键菜单命令层去重**）；`MainWindow.axaml.cs` 累计 5023 → **2928 行**（另有 6 个 `Wiring.*.cs` partial 承载工具栏等按域代码），另有 `PreviewRuntimeService`(406) / `EditorCommandCatalog`(170) 等独立文件。MVVM 化决策已记入 ADR（**暂不采用**）。~~先补 UI 测试能力~~ —— **已具备**（第 14 条），MVVM ADR 的反转触发条件 ① 已达成。
7. ~~生成代码测试仍有大量字符串包含式断言未迁移到语义断言~~ —— 已完成：新增结构化断言工具 `CSource` 并迁移 **281 处**「精确调用文本 + 字符串包含」断言；全仓该类断言从 **211 处降到 3 处**（均为工具自身/示例），并有 `TestAssertionStyleGuardTests` 守卫防止复发。
8. ~~UI 测试暴露的两处行为差异~~ —— **均已修正**（详见第 16 条）：
   ① Avalonia 12 中程序化赋值 `TextBox.Text` 不触发 `TextChanged`：已把依赖该时机的
      "空值归一化"改为装载路径显式调用（`FaceplateTemplateEditorView`），并补装载期守卫；
   ② `PropertyPanelView` 的 `X/Y/Width/Height` 绕过可插拔 `IPropertyValidator`：
      `SaveProperties` 中 **51 处**直写全部改为走 `ValidateAndWrite`（含数值/枚举/字符串），
      仅保留"程序化命令"（重置变换、选资源、切绑定模式）直写，并在代码注释中写明理由。

9. ~~测试间歇性失败（2026-09-14 登记）~~ —— **已关闭（2026-09-16），两条根因均已定位并修复**。
   - `AlarmEngineTests.Engine_ClearReturnedRecords_RemovesOnlyReturned`：原用例用 `Thread.Sleep(100)`
     等**定时器回调**，而 `System.Threading.Timer` 回调排在 ThreadPool 上，全量并行（7 个测试宿主
     抢 CPU）时延迟可以是间隔的任意倍 ⇒ 固定等待**必然**随机红。已统一改 `TestWait.Until`（等**产品
     状态**：「2 条 Active → 转为 Returned」）；该类现为 **13 处 `TestWait.Until` / 0 处 `Thread.Sleep`**。
   - `MockImageLoadingTests.GeneratedImagesC_WindowsPath_ConvertedToUnix`：真凶是**本机 `/tmp` 仅
     10 MB tmpfs**，全量并行被写满 ⇒ 从系统临时目录取路径的用例随机红。已由 `TestTempDir` 统一收口
     （需空间时自动回退仓库旁 `.hmi-test-tmp/`）；且该用例本身**纯同步**（导出 → 读回 → 包含断言），
     无时序依赖。
   - **证据**：2026-09-16 以默认并行度连跑 **6 次全量，6/6 全绿（每次 2248 通过 / 0 失败）**。历史登记的
     其余定时器类 flaky（`DataLoggerTests.Logger_StartStop_UpdatesSampleCount`、
     `AlarmEngineTests.Engine_DelayedTrigger_OnlyFiresAfterDelay`、
     `BuildPipelineServiceTests.CompileAsync_CollectsOutputFiles_AndTogglesUi`、
     `DeviceChannelTests.ConcurrentCommands_…`）一并带入 soak，**同样零复现**。
     机制与修法见 `docs/2026-09-14-test-timer-flakiness.md`（含「漏网的 3 处内联 `SpinWait`」补修）与
     `docs/2026-09-15-control-object-symbol-contract.md` §8（`/tmp` tmpfs 真凶）。
   - 保留提示：`GitWorkflowServiceTests` 会真实执行 `git add/commit`，其临时目录**必须位于任何 git
     工作树之外**（已加固）—— 一旦落在仓库内，git 会向上发现父仓库 `.git` 并把提交写进项目仓库。

10. ~~**`DeviceChannelTests.ConcurrentCommands_AreSerialized_NoCrossTalk` 复发**（2026-09-16 登记）~~ ——
    **同轮关闭**（阶段 6）。第 9 条曾把它列入"soak 零复现"的名单，门禁第一次跑时它红了：
    `Expected: ["ok:AAA", "ok:BBB"]  Actual: ["", "ok:AAA\nok:BBB"]`（第一条命令拿到空串，
    第二条拿到两行）。单独复跑 **3/3 全绿**、门禁第二次跑也绿 ⇒ 与负载相关。
    根因：两个通道**各自**持有一份逐字相同的收帧代码，判据是"从**写入时刻**起算静默
    `SilentTimeoutMs`"—— 设备起步慢过 300 ms 就被判"无输出"返回空串，迟到的回包落进第二条的窗口。
    这是**产品侧**健壮性问题（设备慢时同样会串响应），不只是测试问题。
    已修：判据收成唯一实现 `CommandFrameCollector` + 首字节独立宽限窗口；测试侧把"设备起步慢"
    从"靠负载去撞"改成**可构造输入**（`LineServer.replyDelayMs`）。专题见
    `docs/2026-09-16-channel-frame-completion-contract.md`。

11. **`GitWorkflowServiceTests.InitializeRepository_SecondCall_Idempotent` 全量并行时偶发红**（2026-09-16 登记）——
    阶段 6 门禁第一次跑时它红了，失败点在**第一次** `InitializeRepository(dir)` 返回 false
    （即 `git init` 本身失败，**不是**幂等逻辑），该用例在门禁里耗时 45 s、单跑 4 s。
    单独复跑 `GitWorkflowServiceTests` **8/8 全绿** ⇒ 与负载相关，判为 flaky，**本轮未修**
    （不在范围内）。待查方向：`RunGit` 的进程超时 / 负载下 `git` 启动失败；临时目录落点问题
    已由第 9 条提示排除（`TMPDIR` 在工作树内会污染真实仓库，已加固）。
    **2026-09-16 第 20 轮（阶段 8）门禁再次复发**：同一失败点、门禁里 39 s（单跑 4 s）——
    这是第 **3** 次（阶段 6 首跑、阶段 7 期间、阶段 8 首跑），三次都伴随"全量并行 + 该用例真实执行
    `git`"，**判为 load-sensitive flaky 已无疑义**；仍**未修**（不在本轮范围），
    但下次门禁再红可直接按本结论跳过归因。

    **2026-09-16 第 21 轮（阶段 10）取证，结论要修正 —— 它不是「只在门禁里红」**：

    | 场景 | 结果 |
    |---|---|
    | `ci-gate.sh` 三跑（7 宿主并行） | **3/3 红**；同一条、且每次只有这一条；耗时 **6m42s / 5s / 50s** |
    | 整个 Core 程序集**单跑**（无并行宿主） | **绿 / 红(35s) / 绿**（四次单跑里两次红，另一次 1899/1900） |
    | 定向 `--filter GitWorkflowServiceTests` | 两次都 **8/8 绿**（6 s） |

    即**单程序集约有 1/3 的复现率**，「load-sensitive」不足以描述它。已排除的：
    磁盘（`/tmp` 采样见验证记录）、`TMPDIR` 落点、`ulimit`、`git` 是否为包装脚本
    （`/usr/bin/git` 是真 ELF）、`SourceScanner` 是否偷跑 git（纯文件系统向上找根）。
    **失败点恒为 `InitializeRepository` 的第一个守卫**（`GitWorkflowServiceTests.cs:226`，
    已逐行核对是**第一次**调用），耗时方差极大（5 s ~ 6m42s），把探测窗口 3 s → 15 s
    与 `RunGit` 对齐也**没有**解决它 ⇒ **根因不在 git，而在 .NET 测试宿主的进程创建/等待**：
    xUnit 并行 + CPU 竞争下 `Process.Start` / `WaitForExit(ms)` 会被拖到秒级甚至分钟级
    （同一次门禁里 Core 程序集整体 7m29s，而单跑 1m12s），窗口怎么调都会被吃穿。
    **2026-09-17 定性：根因已找到，而且不在产品里。** `git init` 在**全新临时目录**里恒定约 1.2 s，
    而本沙箱对**首次出现的文件路径**收约 **60 ms**（**目录创建几乎免费**，实测 1 ms ——
    首版把 python3 进程启动 90 ms 误算成目录开销，已纠正），且该开销**全局串行**
    （8 进程各建 20 个文件 = 8 × 单进程时间，20 核全程闲置；tmpfs 与 btrfs 表现相同）。
    `git init` 要造约 20 个新路径 ⇒ 恒定 1.2 s；大队把队列灌满时，同一个 git 子进程从 1 ms
    涨到 **4~10 s**（0 失败，只是排队）⇒ 撞穿 `GitProcessWaitMs`(15 s) ⇒
    `InitializeRepository` 返回 false。**43 s ≈ 三次 15 s 窗口被撞穿**。
    纯 bash 可复现：`bash tools/probe-fs-newpath-cost.sh`（不碰 .NET、不碰被测代码）。

    已验证**不是**解药的两条（别再花时间）：调窗口（15 s 照样撞穿）；调 xUnit 并行度
    （20 / 8 / 4 / 2 线程墙钟 **84 / 83 / 90 / 88 s**，同量级 —— 吞吐由那条串行资源决定，
    并行度换不到任何吞吐，也就换不到更短的排队）。

    **2026-09-17 已修**：`ci-gate.sh` 第 3 步拆两段，让 spawn git 的那 8 个用例在**队列为空**时
    单独跑（单独跑实测 **8/8 绿 / 7 s**；排除后 Core 恰为 **1892 = 1900 − 8**）。
    **判据一条没放宽** —— 用例照跑、断言照旧，只是不再与大队争那条串行队列。
    取证与教训见 `docs/2026-09-17-sandbox-newpath-serialization.md`。

12. **`HmiEditor.App.UiTests` 在 Debug 全量里 testhost 崩溃/挂死**（2026-09-17 登记；同日**两次改写判据**，仍未修）——
    ⚠️ **判据第三次修订（2026-09-17 晚，以此为当前判据）**：既**不是**「仅带 `--blame-hang` 才复现」，也**不是**配置问题，而是——
    **同一条 Debug 全量命令（无 blame、方案A关）连跑两轮 ⇒ 一次 `App.UiTests` 142/142 绿（1 m 56 s）、一次挂死**；
    更早还挂死过一次（17 min 无输出）。而**单独跑该项目恒定全绿、只要 10~20 s**。
    ⇒ 定性：**「进全量就出事」的间歇缺陷，复现率 ≈ 2/3**；带 blame 那两次的崩溃（98/142、37/142）是
    **另一种呈现**，不是**原因**（`--blame-hang` 会改运行环境，本来就不该拿来比因果）。
    **挂死现场签名（首次抓到）**：全树 **209 个线程卡在 `futex_wait`**、其余在 `poll` / `epoll_wait` /
    `anon_pipe_read`，**全程零 CPU**（loadavg 3.16 / 20 核；App.UiTests 的 testhost `State: S`、26 线程）
    ⇒ **僵死（死锁）而非空转**；且挂死那次**其余 6 个程序集全绿**、只有 `App.UiTests` 从未报出汇总行
    ⇒ 瓶颈在这个程序集内部，不在别的程序集。
    ⚠️ **判据正控已做扎实（同一晚）**：`xunit.runner.json` **确实被读取** —— `diagnosticMessages:true`
    让输出 **239→317 行**、`methodDisplay:"method"` 让带类名的用例名 **142→0**（两个互相独立的可观测变化）。
    ⇒ 本节旧文那条「值写成非法类型不报错 ⇒ 没被读取」的**推论作废**（xUnit 对坏值宽容，与读不读无关），
    而「**方案 A 无效**」的结论**反而站稳**（配置当时确实生效着，崩溃照旧）。
    详见专题 §6.2 / §6.3。以下为**首次**取证（内容仍有效，但要按上面的收窄读：
    那次「15 m 44 s / 21 m 39 s 不再推进」的另一半原因已在阻断项 11 一族里排掉了）。
    端到端验收复跑时暴露：Debug 下 `dotnet test HmiEditor.slnx`（与大队并发）会在该项目上**不再推进**
    （两轮分别跑满 **15 m 44 s / 21 m 39 s** 且日志无新输出，人工终止）；加 `--blame-hang` 后
    **187 s 内**即中止，报告**「测试主机进程崩溃」**、通过 **92/142**，并给出「崩溃时运行的测试」清单
    ——**9 条，分属 9 个不同测试类**，且**无托管异常、无堆栈** ⇒ 原生崩溃或栈溢出。
    四次对照（一次只放开一个变量）：

    | 条件 | 结果 |
    |---|---|
    | Debug · 单独跑该项目 | **142/142 绿，12.5 s** |
    | Debug · 走 slnx、只匹配该项目 | **EXIT=0，24 s** |
    | Debug · 全量与大队并发 | **EXIT=1，187 s，testhost 崩溃** |
    | Release · 全量与大队并发 | **142/142 绿，1 m 20 s**（verify 连跑两轮均绿） |

    那 9 条并发测试共享 `UiTestHost` 的**单例** headless 会话（`Lazy<HeadlessUnitTestSession>`，
    全部 `Dispatch` 到同一个 UI 线程），而 xUnit 默认并行跑不同测试类 ⇒ 判定为
    **「多个测试类并发使用单例 headless 会话」模式下的宿主稳定性问题**。
    ⚠️ **Release 不崩不等于安全**（很可能只是时序不同）；这也正是 `verify.sh` 默认改
    **Release** 与门禁对齐的第二个理由。
    ~~**修法已实测有效（2026-09-17，未落地）**~~ —— **该结论已撤回**。同日复验里把方案 A **真落地**后再跑
    Debug 全量：**仍然崩**（EXIT=1 / 307 s / 37/142）；而同一天的**清洁树、不带 blame** 基线反而**绿**
    （EXIT=0 / 107 s）。那两次「绿」的**正控也没过** —— 把 `xunit.runner.json` 的值故意写成字符串，
    `dotnet test` **没有报错** ⇒ 连「runner 真读过这个文件」都未被证明（xUnit 自己**不会**自动拷贝它：
    回退 csproj 声明后 `bin/` 里那份不再出现，已核）。
    **所以两件事都待定**：① 崩溃是否只在带 `--blame-hang` 时出现；② 方案 A 是否真被读取。
    **已回退方案 A**（不留没有证据的改动），留待下面 2×2 有结论再决定。
    下一步（分离变量的 2×2，每格至少两跑，并记录机器负载 —— 本轮 107 s vs 336 s 的墙钟差说明负载也进了画面）：

    | # | `--blame-hang` | 方案 A | 预期要回答 |
    |---|---|---|---|
    | 1 | 否 | 否 | 清洁基线（本轮已得：EXIT=0 / 107 s，1 次） |
    | 2 | 否 | 是 | 方案 A 在无 blame 下是否被读取且无害 |
    | 3 | 是 | 否 | 崩溃是否可复现（本轮已得 2/2 崩） |
    | 4 | 是 | 是 | 方案 A 能否消掉 blame 下的崩溃 |

    ⚠️ **上面那张 2×2 表已作废（2026-09-17 晚）** —— 它的两个前提都不成立：
    「无 blame 就绿」后来**挂死了两次**；「正控没过」也被推翻（文件确实被读取）。
    正确的问题不是 blame 与否，而是**复现率**（先量率、再修、再同法复量 —— 没有率的「修好了」都是巧合）。

    **修法（2026-09-17 晚，验证中）**：不再赌运行时配置文件，改用 xUnit **编译期**就生效的集合语义 ——
    `UiTestHost.cs` 新增 `[CollectionDefinition(HeadlessUiCollection.Name, DisableParallelization = true)]`，
    并给**13 个**用到 `UiTestHost` 的测试类各加一行 `[Collection(HeadlessUiCollection.Name)]`
    （仓库已有同款惯用法：`LlmConfigStoreTests.cs` 的 `AppDataRoot-global`）。
    **机制假说**：13 个类各占一个 xUnit worker、**同步阻塞**在 `Dispatch(...).GetAwaiter().GetResult()` 上，
    跨程序集的负载把线程池/调度压住 ⇒ UI 线程的续体排不上 ⇒ 死锁（与「209 个 futex_wait、零 CPU」相符）。
    **代价≈0**：串行化后单独跑仍是 **142/142 绿 / 持续时间 10 s / 墙钟 13 s**（修法前 12.5~20 s）——
    因为 142 项本来就都在**同一个 UI 线程**上串行，类级并行只重叠了非 UI 部分（这也解释了
    「方案 A 让集合串行」为什么量不出墙钟变化）。
    **验证结果（2026-09-17 晚）：修法没能消除挂死 ⇒ 已回退。** 同一命令连跑 4 轮：
    **H1 挂死**（607 s）、**H2** 1 条 Device flaky（`App.UiTests` 仍 **142/142 绿 / 1 m 54 s**）、
    **H3 / H4 全绿**（**142/142 / 1 m 58 s / 2 m**）。修法前 ≈ **2/3** 失败、修法后 **1/4** ——
    **N=4 不足以声称有效**（若 p=2/3，4 跑里至少 1 次失败的概率是 98.8%，而只观察到 1 次）。
    ⚠️ **H1 的签名还变了**：现场取证显示 **`App.UiTests` 的 testhost 已经消失**（只剩 wrapper 在 `do_wait`，
    没有那 209 个 `futex_wait`）⇒ 从「进程还在但僵死」变成「进程没了、也没人报出结果」。
    按与「方案 A」同一把尺子**已回退**（不留没有证据的改动），并**收回半句**机制假说：
    「13 个类并发进同一个单例 headless 会话」不足以解释它。
    **判别实验（已出结果）：触发条件不需要「别的测试程序集」在场。** 单跑 `App.UiTests`
    （本来恒定绿）+ **6 个外部进程持续造新路径**（组合里没有别的测试程序集）⇒ **S1 / S2 两次全挂死**
    （120 s / 124 s 判定，输出都停在 **0 字节**），而对照组（无风暴）**exit=0 / 20 s**。
    现场：全局 wchan 直方图**只有 ~28 个线程**、`pgrep -f HmiEditor.App.UiTests` **零命中**、
    进程表里只有 `dotnet test` 而**没有 `vstest.console` / `testhost`** ⇒ **测试宿主根本没起来**。
    ⚠️ **但这与全量那两次不是同一签名**（那两次 testhost 在、**209 个 `futex_wait`**、输出 2195 B）
    ⇒ 两条**不合并**：本沙箱至少存在一个**与 headless 会话无关**的失败入口
    （串行的「新路径」资源被占满 ⇒ 连测试宿主都起不来，阻断项 11 同族）；
    而「全量并发下的 `App.UiTests` 间歇失败」**成因仍未定位**（不再追：见下一条）。
    6 个 worker 是**人为饱和** ⇒ 只证明这条资源的**充分性**，不等于全量那两次就是这个原因。
    **总判据（不变）：「Debug 全量在此沙箱不可作为判据」都成立** ⇒ 门禁与端到端验收继续只认 **Release**；
    在沙箱内解读任何 Debug 并发读数时，先问「这条串行资源当时空着吗」。

    **同轮另登记一条新 flaky（非本轮引入）**：`HmiEditor.Core.Tests.AlarmEngineTests`
    `.Engine_TriggerThenReturn_CreatesReturnedRecord` 在 Debug 全量下红过一次
    （`Expected: Returned / Actual: Active`，Core **1891/1892**）⇒ 属**时序/负载**敏感
    （报警「返回」态在断言时尚未落地）。**单独复跑若绿也不能就此结案** —— 同一条纪律：先量复现率。

    **若日后要取原生栈**：`--blame-hang-dump-type full` + `dotnet-dump analyze`。
    **可复用的排查姿势**：无 blame 时 testhost 崩溃**表现为「挂死」**（`dotnet test` 会一直等），
    加 `--blame-hang --blame-hang-timeout 90s --blame-hang-dump-type none` 即可当场点名。
    ⚠️ 但本轮教训是：**blame 自己就是混淆变量** —— 「带 blame 崩 / 不带 blame 绿」**不等于**
    「并发 vs 配置」的因果，两侧必须是**同一条命令**才能比较（见专题 §6.2、§7）。
    现场见 `docs/2026-09-17-verify-end-to-end-revalidation.md`。
    同族第二例（同一次门禁里的 `CommandFrameCollectorTests…`，排 ThreadPool 的定时器
    撞固定余量）已一并修掉，见该专题 §3.2。

## 阶段 63（闭环 #48：「引用」= **代码引用** —— 注释侧 0 收益（保险）、字面量侧 5 例（活着的）；顺带量出 #49）— 2026-09-21

### 63.1 一句话结论

`LangConstantUsageGuardTests` 问的是「有没有人**用**这个常量」，但它数的是「这个名字在
`src/`+`tests/` 里**出现过**」—— **注释与字符串字面量也算**。于是**在注释里写一个常量名，
就能把基线悄悄压低一位**（阶段 62 亲身踩到：新守卫的类注释里引了一个真实常量名作例子 ⇒
门禁报 `1266 ≠ 1267`；**降的方向恰好是「看起来更好」的那边**，所以只有门禁会抓）。

本轮把口径改成「**代码**引用」：`ReferenceFace{ Raw, NoComments, Code }` 三档显式枚举，
生产判据走 `Code`。**实测：注释侧收益为 0（纯保险）**——全仓注释里那 9 处 `Lang.X` 提到的常量
**全都另有代码引用**，唯一那个受害常量已在阶段 62 用「把词改掉」救回；
**真正活着的那一面是字面量侧，5 例。** 基线 `1267 → **1272**`。顺带查出键可达性判据的
**M1 可被死常量担保** ⇒ 新登记 **#49**。

### 63.2 为什么它必须排在 #35 常量侧**之前**

#35 常量侧要做的是「按名字逐个查证 1200+ 个无引用常量」。**取证动作必然要在注释或测试里
写出常量名** —— 而在这个坑修掉之前，**写一次就把那条常量「用活」一次**：基线会随取证动作
自己漂移，「清掉了几个」永远说不清。顺序只能是**先修判据、再取证**。

一句话口径：**在 #48 修掉之前，#35 常量侧的每一个读数都带着一个自我污染项。**

### 63.3 三档口径

| 档 | 取法 | 用途 |
|---|---|---|
| `Raw` | 原文 | 阶段 63 之前的旧口径，**只留作对照** |
| `NoComments` | `.cs` → `StripComments`；`.axaml` → `StripXmlComments` | 中间档（单调性对照） |
| `Code` | `.cs` → `MaskLiterals`（剥注释 **+** 抹字面量内容、**保留 `{…}` 插值孔**）；`.axaml` → `StripXmlComments` | **生产判据** |

第三档刻意用 `MaskLiterals` 而不是 `StripComments`：`$"…{Lang.X}…"` 这种引用**只在孔里**，
整串抹掉会把活常量判成死的（`SourceScanner` 的类注释里记着这次踩坑）。反向的坑也留着记号：
`.axaml` 走 XML 注释剥离（两种语言同一条道理，只是注释语法不同）。

### 63.4 读数：三档，以及 5 个具名实例

| 档 | 标识符 | 无代码引用常量 |
|---|---|---|
| `Raw`（旧口径） | **20487** | **1267** |
| `NoComments` | **19521** | **1267** |
| `Code`（新口径） | **15806** | **1272** |

剥注释一次抹掉 **966** 个标识符，**没有一个是 `Lang` 常量名** ⇒ **注释侧 0 收益**。
如实记一句：**这一档是保险，不是修一个活着的缺陷。** 说「修好了」不诚实，
说「关掉了一个复发入口」才准确。

字面量侧那 5 例（逐个取证）：

| 常量 | 唯一「引用」来源 | 性质 |
|---|---|---|
| `AssetSvgNoShapesHint` | `LocalizedFormatCallSiteTests.cs:279` | 在 `const string src = """…"""` **原始字符串**里 —— 合成源码**夹具**，不是代码 |
| `ProjectSettingsCategoryEditor` | `EditorPreferenceWiringTests.cs:122` | `Assert.DoesNotContain("…", code)` —— **守卫自己的反断言**把常量写成了「有人用」 |
| `LoginBtn` | `Core.Tests` 里控件 `Name = "LoginBtn"` | 控件名，与 `Lang.LoginBtn` 无关 |
| `RecipeName` / `RecipeVersion` | `RecipeManager.cs:199` `sb.Append("RecipeName,RecipeVersion,GroupName")` | CSV 表头字面量 |

第 1 例**先被当成扫描器缺陷查过**：若那处 `Lang.AssetSvgNoShapesHint` **不在**字符串里，
就是 `MaskLiterals` 误抹了真引用 —— 那就必须停手。读回 270–295 行确认它在
`const string src = """…"""` 里面 ⇒ 抹掉它是**正确**行为，不是缺陷。

另附分类读数（`#35` 常量侧要用）：这 5 个里 **4 个的字典键是活的**
（`login.btn` 由 `LoginDialog.axaml:81` 的 `{res:Loc}` 与 `UserManagerView.axaml.cs:524` 的字符串键消费；
`recipe.name`/`recipe.version` 由 `RecipeEditorView.axaml:214/263`；
`asset.svg.no.shapes.hint` 由 `Wiring.DockAggregate.cs:81`）⇒ 属「死常量 + 活键」；
第 5 个 `project.settings.category.editor` **常量与键都是死的** ⇒ 见 63.7 / #49。

### 63.5 新判据：语义（**两侧都断言**）+ 单调性

- `ReferenceFaces_IgnoreProseAndLiteralText` —— 一段合成片段（注释提及 / 字面量提及 / 代码引用 /
  插值孔引用各一处），**同时断言两侧**：`Raw` 面**确实**被那两处非引用提及满足（**这就是 #48 的病根**，
  写出来判据才自带解释），`Code` 面不会。只断言后者 ⇒ 「两档其实都看不见」时照样绿 ＝ **假绿**。
  片段里的名字是**故意不存在的常量名**（`FakeProseOnly`/`FakeLiteralOnly`/`FakeCodeRef`/`FakeInterpHole`）——
  用**具名**常量会重演 #48：判据自己的文档把常量写活。
- `NarrowingFaces_AreMonotone` —— ① 三档**只许变窄**（某档反而变大 ⇒ 剥的过程在**造**标识符，
  而方向是「看起来更干净」，最危险的一种）；② 三档必须**逐级真的变窄**（否则收紧根本没生效、
  判据静默退化）。两条都带读数（`20487 > 19521 > 15806`）。

合成片段的判据**比拿语料当判据强**：真实语料今天在注释侧**恰好是 0 例**，
「拿语料当判据」等于没有负控 —— 而「有一次绿等于零信息」。

### 63.6 负控 5/5：同一处提及，**三种写法**

注入点统一是 `UiOperationGuard.cs` 类体第一行；被测常量统一 `Lang.AiAgentMode`
（取自 `Raw` 面清单第一个 ⇒ 注释、字面量、代码三处**都不出现**，注入才是唯一变量）。

| 注入 | 反应 | 说明 |
|---|---|---|
| `m1-comment` 注释提及 | **绿 4/4** | 注释不算引用 ✅ |
| `m2-literal` 字面量提及 | **绿 4/4** | 字面量不算引用 ✅ |
| `m3-code` **代码引用**同一常量 | **红** `1271 ≠ 1272` | 代码引用照样算 ✅（证明不是「扫描变瞎」） |
| `m4-raw-face` 生产判据改回旧口径 | **红** `1267 ≠ 1272` | 旧口径确实被散文/字面量满足 ✅ |
| `m5-passthrough` `ForFace` 直通 | **红 3 条** | 语义判据 + 单调性同时炸 ✅ |

`m1`/`m2`/`m3` 三档合起来才说明问题：**同一处提及换一种写法，判据的反应就必须不同** ——
只有 `m1`/`m2` 是绿的话，「扫描器变瞎」也能给出同样的读数。

**还原校验**：逐条用**注入前后 SHA256 比对**证明**逐字节还原**。
（第一次写成 `git diff --stat` 比 HEAD ⇒ 报了假「非空」—— 本阶段对守卫的改动本来就还没提交。
**教训：还原校验的基准必须是「注入前的内容」，不是「仓库里的版本」。**）

### 63.7 顺带产出 #49：键可达性判据的 M1 可被**死常量**担保

查字面量侧第 5 例时发现：`project.settings.category.editor` 这个**键也死了**，可它居然通过了
阶段 62 的键可达性判据。追下去是一条**结构性的洞** —— **M1 = `Lang` 常量的值（反射枚举）**
只问「常量还在不在」，**不问常量有没有人读** ⇒ **死常量给死键做担保**，
字典侧那句「已闭环」实际是**模掉「常量存在」的闭环**。

三条独立取证（见 #49 行）：① `ProjectSettingsView.axaml.cs:325-330` 的分类清单**恰好 6 项**
（basic/display/comm/runtime/export/security），**独缺 `editor`**，而同族的另外 6 个常量
**就在这 6 行里被代码引用**（干净的 7 vs 6 对照）；② 全仓该键只出现在两本字典、`Lang.cs:53`、
`scripts/sync_localization.py:48`（**补键工具自己的映射表，不是消费者**）；
③ `EditorPreferenceWiringTests` 的 `ProjectSettingsView_HasNoEditorSettingsCategory`
**明确断言**这个分类已被废掉。

修法方向已写明（把 M1 收紧成「有代码引用的常量的值」，即与 #48 这条口径**组合**），
但**本阶段刻意不修**：属独立改动，且收紧后会**一次性 evict 一批键**，每条都要像阶段 62 那样
按 ≥2 个独立面取证后才能删 —— 那正是 #35 常量侧的同一件工作。

### 63.8 门禁（提交**前**）

日志 `.workbuddy/tmp/ci-gate-63.log`，mtime **`21:13:42`**，**早于本阶段提交** ⇒ 只覆盖工作区、
**不作为提交后验证**。

| 项 | 读数 | 证据 |
|---|---|---|
| 门禁 | **7 步全绿**（banner 数 = 7）/ `GATE_EXIT=0`；**0 警告 / 0 错误**；**837 行** | 同上 |
| 测试计数 | 大队 **3028** + `[6]` **8 + 2** = **3038**（阶段 62 的 3036 **+2** = 本阶段新增的 2 条判据） | 同上 |
| 逐程序集 | Simulator 44 / Hardware 44 / Communication 75 / Device 87 / AI 315 / Render 4 / Core **2116** / `App.UiTests` **343** | 阶段 62 是 341 ⇒ **+2**，其余逐条同数 |
| 墙钟 / 负载 | 负载 `2.25 → 3.86`（**跑前**读数，见 59.11） | 同上 |
| 新守卫单跑 | **4/4 绿**、`778 ms` | `.workbuddy/tmp/guard63.log` |

### 63.9 仍未闭环

- **#49 未修**（M1 组合收紧）；与 #35 常量侧是同一件工作，应一起做。
- **常量侧仍未逐个判死**：入口清单以 **1272** 为准，且要分三类看
  （死常量+活键 / 死常量+死键 / 活常量），**不能批量删**。
- `Raw`/`NoComments` 两档**刻意留在代码里**（单调性判据要用），但它们**不受基线约束**。

### 63.10 门禁（提交**后**，带着读数出口跑）

日志 `.workbuddy/tmp/ci-gate-63-post.log`，mtime **`21:15:17`**，**晚于**两笔提交
`3d2755f`（`21:14:12`）/ `57572e3` ⇒ **本条才是提交后验证**。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿** / `GATE_EXIT=0`；**0 警告 / 0 错误**；**837 行** |
| 测试计数 | 大队 **3028** + `[6]` **8 + 2** = **3038** |
| 与 63.8（提交前）比 | **逐程序集逐条同数**（44 / 44 / 75 / 87 / 4 / 315 / 2116 / 343，再 +8 +2，失败全 0）⇒ 无回归 |
| 墙钟 / 负载 | 负载 `4.51 → 5.71`（**跑前**读数，见 59.11） |
| 读数文件 | `.workbuddy/tmp/readings-63-gate.txt` = **189 行 / 7,912 字节 / 11 个块** |

**读数文件与阶段 62 逐项同数**（189 / 7,912 / 11）—— 本阶段**没有新增读数块**，而且是**刻意**的：
#42 那条出口在阶段 61 就写明「其它族（基准/预算类、**静态守卫类**）刻意不统一接出口」，
本判据属静态守卫族。另外，本阶段新加的三档读数（`20487 > 19521 > 15806`）**本身是被断言守住的**
—— 阶段 61 的原则是「读数只是证词、刻意不做成断言」，这里是一个**有理由的例外**：
三档若不变窄，就说明口径收紧**根本没生效**（或剥的过程在造标识符），属**退化**，必须红。

## 阶段 64（闭环 #49：M1 只认**代码引用** + M6 排除**定义面** —— 151 个键失去全部担保，143 删 / 8 保留）— 2026-09-21

### 64.1 一句话结论

键可达性判据的两条机制**各自都能被「定义」满足**：**M1** 原先枚举 `Lang` **全部**常量值（不问常量有没有代码引用）⇒ **死常量给死键做担保**（#49）；**M6**（源码整键字面量）的语料里留着 `Lang.cs`，而它**按定义写着每一个键**（`public const string X = "the.key";`）⇒ 对「任何有常量的键」**恒真**。两条一起收紧（M1 = `CodeReferencedConstants()`；M6 语料排除 `Lang.cs`）后，**151 个字典键**当场失去全部担保。逐个取证后：**143 删**（两本字典 + 对应 `Lang` 常量 + 补键工具映射表条目）、**8 保留**（进差异表并写明理由）。七条机制的**面一条没动**。

### 64.2 为什么两条必须一起收：自我满足只是搬了家

只收 M1 的时候，实测被释放的键**一个都没**被判不可达 —— 它们全部改由 M6 担保（M6 面 **113 → 539**）。这不是「另外 539 个键被发现」，而是 `Lang.cs` 作为语料把每一个键又写了一遍。⇒ **判据的输入面不能包含「定义面」**：定义面证明的是「这个键存在」，不是「有人在查它」。排除 `Lang.cs` 后 M6 面落到 **388**，151 个键终于无处可躲。

### 64.3 取证：151 个键凭什么可以删

| 面 | 读数 | 手段 |
|---|---|---|
| 全仓整键出现点（排除两本字典 / `Lang.cs` / 本守卫） | **21 处**，全部落在 `docs/`（7 处）与 `scripts/sync_localization.py` 的**补键映射表**（14 处，其中 1 处是函数 docstring 的举例）—— **没有一处是消费者** | `git grep -F -o -f <151 个键>` |
| 前缀**拼接**证据（`"prefix."` / `prefix.{`，逐键的每一个点分前缀都查） | **5 个**：4 个 `param.*`（`PluginTextKeys` 拼 `"param." + CamelToDotted(fieldKey)`）、1 个 `tb.template.lib`（**假阳性**：测试拼的是 `tb.{ToLowerInvariant()}`，与带点的键无关） | 逐键前缀扫描（`.workbuddy/tmp/disp64.py`） |
| 同值活键（改名残留的最强证据） | **60 个** | 两本字典对拍 |
| zh 值 == 键名（TODO 占位） | **0 个** —— 它们都不是「加了没翻」的新键 | 两本字典 |

改名方向高度一致：`win.* → dock.title.*`、`project.settings.editor.* → settings.display/interaction.*`、`menu.file.* → project.*`、`panel.property.* → prop.*`、`tpl.*.cat → fp.cat.*`。
另有一类是**值形态直接作废**：`device.image.file.filter` / `device.so.file.filter` / `device.swu.file.filter` / `device.fw.image.missing` —— 字典里是 `'共享库 (*.so)|*.so|所有文件|*.*'` 这种 **Windows 管道串**，而代码现在是 `PickFileInto(Lang.DeviceSwuPath, new[] { "*.swu" }, …)`（模式数组）⇒ 格式换了，老键用不上。
`tb.template.lib` 单独说：`Lang.TbTemplateLib` 常量在，`HmiControlType` 里**没有** `TemplateLib` 成员（测试那条 `tb.{ToLowerInvariant()}` 拼的是 `templatelib`），全仓无人引用。

### 64.4 处置链：删键必须连常量一起删

`LangKeyCoverageTests.EveryLangConstant_ResolvesInBothLanguages` 要求**每个 `Lang` 常量都能在两本字典里解析到**；而 151 个键**每个都恰好有一个常量**（`Lang.cs` 共 **1915** 个常量 ⇒ 1915 − **1272** 无引用 = **643**，正好等于 M1 收紧后的实测面 **643** —— 三处读数自洽）。⇒ 删键不删常量，那条正向判据立刻红。
补键工具 `scripts/sync_localization.py` 是**双向**同步器（字典缺键 → 按 `VALUE_MAP` 补；字典多键 → **反向生成常量**）⇒ 映射表里指向被删键的 **13 条**也必须一起清掉，否则工具会把它们加回来。

### 64.5 落盘：一个「默认只看不改」的脚本

`.workbuddy/tmp/evict64.py`（`--write` 才落盘；先整份备份到 `.workbuddy/tmp/backup64-20260921-213442/` 并逐字节校验；一律**整行删除**，不做前缀替换；写后逐条回读）。

| 项 | 读数 |
|---|---|
| 两本字典 | 各 **2275 → 2132** 键、仍同集合；**各 −143 行 / +0 行**（纯删行，无重排 ⇒ 千行 diff 里没有一处改写） |
| `Lang.cs` | 常量 **1915 → 1772**；另跑了「段头后紧跟段头/闭括号」检查（**0 命中** ⇒ 没留下空段头） |
| 补键表 | `VALUE_MAP` **−13 行** |
| 残留 | **0**（对两本字典、`Lang.cs`、补键表逐条回读断言） |

**一处必须记下的自纠**：脚本第一版把「键数必须减少」写成了 `len(parsed) == len(原)` —— **写成「必须不变」**，于是落盘第一步就被自己的断言拦住。**这次是好事**（断言在 `write_text` 求值阶段抛出，文件一个字节没动），但它说明：**自己加的校验本身也要被质疑**。改后是 `len(parsed) == 原 − 要删的键数`（多删 / 少删 / 文件里有重复键都会现形）。

### 64.6 八条保留：两条都是**既有结论**，不是「没空查」

`KeptUnreachable` 从**空表**变成 8 条，每条带理由：

| 键 | 理由 | 依据 |
|---|---|---|
| `param.ip.address` · `param.tcp.port` · `param.mqtt.port` · `param.node.id.format` | 配置字段的 Key **由插件声明**，而插件可由**外部程序集**加载（`src/Hmi.Hardware.Loader/PluginLoadContext : AssemblyLoadContext`）⇒ 仓库内**不可枚举**。内置 Modbus TCP 的字段已改名为 `host` | `docs/2026-09-18-plugin-text-and-lang-usage-guards.md` 的既定结论：**「只增不删」：陈旧键（`param.baud.rate` 等 5 个）保留 —— 字段 Key 来自插件** |
| `role.operator` · `role.engineer` · `role.administrator` · `role.maintenance` | 角色名现在走**模型硬编码**（`UserRole.BuiltIn*` 的 `Name`/`Description`），用户管理里的角色下拉直接绑 `UserRole` 对象（`UserManagerView.axaml:141/213`，**无 ItemTemplate**）⇒ 键没有消费点。它们**不是残骸**，属「界面该显示却没接线」，键值已备好（zh 管理员 / en Administrator） | `UserRole.cs` + `UserManagerView.axaml(.cs)` |

⇒ 新登记 **#50**（角色名未接线）与 **#51**（插件兜底键**无法被任何判据证明是活的**）。差异表的**反向判定**（表内条目若变得可达、或从字典里消失 ⇒ 红）会把这两条**自清理**地推着走 —— 谁给角色名接了线，谁就得删掉那 4 条。

### 64.7 两处「下界必须跟着口径走」

本阶段有**两处过期下界**被自己撞穿，症状相同、成因相同：**下界是按旧口径标的，口径改了没同步**。

| 位置 | 旧下界 | 撞穿时的原文 | 新下界 | 依据 |
|---|---|---|---|---|
| 键可达性 · 元判据 M1 | `1800` | `M1-Lang 常量：实测 643 < 下界 1800` | **600** | 收紧后 M1 = **有代码引用**的常量值（1915 常量 − 1272 无引用 = 643）。旧下界守的是「枚举全部常量值」那个已经不存在的机制 |
| 常量使用面 · 自检 | `1800` | `Lang 常量只扫到 1772` | **1500** | 驱逐 143 个死常量后总数 1915 → 1772 |

### 64.8 判据与读数

七条机制的面**一条没动**（被驱逐的键全部落在没有任何机制覆盖的区域 ⇒ **可达面零损伤**）：

```
    M1-Lang 常量         面   643
    M2-axaml res:Loc   面  1113
    M3-插件族派生           面    26
    M4-cfg.note 清单     面    30
    M5-脚本诊断码           面    19
    M6-源码字面量           面   388
    M7-枚举名派生           面    12
```

（证据 `readings-64-evicted.txt`，走 `HMI_READINGS_OUT` 出口；驱逐**前后**这七个数逐项相同。）

| 判据 | 读数 | 证据 |
|---|---|---|
| 键可达性守卫（5 条） | **5/5 绿** | `.workbuddy/tmp/after-evict-2.log`（五组共 22/22） |
| 常量使用面（4 条） | **4/4 绿**，基线 **1272 → 1129** | `.workbuddy/tmp/after-evict-1.log`（改基线前那次红给出实测 `1129`）；`after-evict-2.log` |
| 常量 ↔ 字典闭合（3 条） | **3/3 绿** | 同上 |
| 格式调用点的键数下界 | `≥ 2000` 仍成立（实测 **2132**） | 同上 |
| 七条面读数 | 643 / 1113 / 26 / 30 / 19 / 388 / 12 | `.workbuddy/tmp/readings-64-evicted.txt` |

### 64.9 仍未闭环

- **#35 常量侧的入口清单从 1272 降到 1129，但剩下那 1129 个仍未逐个判死**：本阶段判死的是**键也是死的**那 143 个（判据是**键可达性**，不是常量本身）。**方向别搞反** —— 本阶段做的是 `#49`，不是「常量逐个判死」；后者的三类（死常量+活键 / 死常量+死键 / 活常量）仍要按名字查。
- 新登记 **#50 / #51**（见 64.6）。
- `KeptUnreachable` 的 8 条是**判据的例外**，不是豁免：它们仍在字典里、仍不可达，反向判定会一直盯着。

### 64.10 门禁（提交**前**）

日志 `.workbuddy/tmp/gate64.log`（mtime **`21:37:50`**），**7 步全绿 / `GATE_EXIT=0`**；**0 警告 / 0 错误**；**838 行**。

| 项 | 读数 |
|---|---|
| 测试计数 | 大队 **3028** + `[6]` **8 + 2** = **3038** |
| 与阶段 63 比 | **逐程序集逐条同数**（44 / 44 / 75 / 87 / 4 / 315 / 2116 / 343，再 +8 +2，失败全 0） |
| 说明 | 本阶段**不新增任何用例**（只改判据口径 + 字典数据 + 两个基线读数）⇒ 计数不动**正是预期** |

### 64.11 门禁（提交**后**，带读数出口跑）

日志 `.workbuddy/tmp/ci-gate-64-post.log`（mtime **`21:41:58`**），**晚于**两笔提交
`64380da`（`21:40:32`）/ `103b9ed`（`21:40:35`，即 `HEAD`）⇒ **本条才是提交后验证**。

| 项 | 读数 |
|---|---|
| 门禁 | **7 步全绿** / `GATE_EXIT=0`；**0 警告 / 0 错误** |
| 测试计数 | 大队 **3028** + `[6]` **8 + 2** = **3038** |
| 与 64.10（提交前）比 | **逐程序集逐条同数**（44 / 44 / 75 / 87 / 4 / 315 / 2116 / 343，再 +8 +2，失败全 0）⇒ 无回归 |
| 读数文件 | `.workbuddy/tmp/readings-64-gate.txt` = **189 行 / 7,912 字节 / 11 个块** |

**与阶段 63 的门禁读数文件逐行 diff 只差 2 行**，而这 2 行正是本阶段改的那两条机制：

```
2c2
<     M1-Lang 常量         面  1915
---
>     M1-Lang 常量         面   643
7c7
<     M6-源码字面量           面   113
---
>     M6-源码字面量           面   388
```

这条对比比 64.8 更硬：**其余 187 行逐字节相同** —— 停靠面板 / 菜单 / 工具栏那些行为面读数**一个数都没动**，
说明本阶段动的确实只有字典侧，且**可达面零损伤**。（两行的涨跌方向别读反：M1 **降**是口径收紧；
M6 **涨**不是「发现了更多」，而是 M1 释放出来的键落到了这条**残差**机制上 —— M6 只对前面机制没覆盖到的键做扫描，
中间读数 539 的来历见 64.2。）

## 阶段 65（#35 的可动性判定：给 1129 个「无代码引用的常量」量出**构成**；顺带收掉 M6 的**注释洞** —— 死键被「为它写的文档」养着）— 2026-09-21

### 65.1 一句话结论

`UnusedBaseline = 1129` 这句注释读起来像一份**待清零的清理清单**，但**结构上到不了 0**。
本阶段给出构成（按「担保其键的机制」分桶）：**M2（axaml `{res:Loc}`）828 / M6（源码字面量）274 /
M3（插件族派生）18 / 无任何机制 9**。前三桶的键**全是活的**，只是消费方式不是"代码读常量"
⇒ 这些常量是**键空间镜像**。顺带把 M6 的语料从"原文"改成"**剥注释**"，
收益 **1 例**，而这一例的形状值得单记：**死键的唯一"活"证据，是两条为它写的文档注释**，
其中一条就在 `LangConstantUsageGuardTests` 自己的类注释第 18 行。

### 65.2 为什么要先量构成

`UnusedBaseline` 是**相等判定**（1129）。它两侧的含义完全不同：
① 涨 = 新增了没人读的常量（P0-5 那类"按陈旧名字加常量"的入口，**该红**）；
② 降 = 清理了死常量（注释要求同步改小基线）。
可 1129 里到底有多少是"可清理的"、多少是"**结构上必须存在的**"，此前**没人量过** ——
而"要不要清"这个判断直接决定基线该不该被当成清理清单（#35 一直是这个悬案）。
⇒ 先量事实，再谈处置。

### 65.3 读数：四桶（新判据 `UnusedConstants_ClassifiedByKeyMechanism`）

口径：常量值就是字典键 ⇒ 查**担保这个键的机制**（机制映射复用键可达性守卫的
`ReachableKeys()`，开成接缝 `MechanismMap()`，**不另抄一份机制**）。

| 桶（担保其键的机制） | 常量数 | 例 | 判定 |
|---|---|---|---|
| **M2**（`.axaml` 的 `{res:Loc K}`） | **828** | `ai.agent.mode` · `canvas.ctx.align` | 键活在 XAML 里；常量是**键空间镜像** |
| **M6**（源码整键字面量） | **274** | `ai.agent.not.ready` · `ai.agent.rolled.back` | 键活在源码字面量里（多为测试断言/取值） |
| **M3**（插件族派生） | **18** | `driver.modbus.rtu` · `driver.mqtt` | 键**运行时拼**出来，**没法**写成常量 |
| **无任何机制** | **9** | `param.baud.rate` · `param.ip.address` … | = **`KeptUnreachable` 差异表**，交叉核对过 |

1129 = 828 + 274 + 18 + 9（判据断言**分类完备**：各桶之和 = 总数）。
另有一处**反向缺口**：字典 **2132** 键里有常量的是 **1772**、**没有常量的是 360**。

⇒ 结论：这 1129 个**不能**按"清理清单"理解 —— 删掉它们，
`LangKeyCoverageTests`（常量→字典正向）与补键工具**都会把名字再要回来**。
基线的真实语义是「**新增一个没人读的常量即红**」，**不是**「这个数该降到 0」。

### 65.4 顺带量出的陷阱：补键工具与这条基线**方向相反**（登记 #52）

`scripts/sync_localization.py` 第 2 步：给「字典里有、`Lang.cs` 里没有」的键**生成常量**。
**干跑实测**（`--dry-run`，脚本**不带该参数就会直接写盘**）：

| 项 | 读数 |
|---|---|
| `Lang.cs` 常量 / 两本字典键 | 1772 / 2132（两本同集合） |
| `Lang.cs` 有但 JSON 缺失 | **0**（正向不变量成立） |
| **JSON 有但 `Lang.cs` 无常量** | **360** ⇒ 跑一次就生成 360 个新常量 |
| 补 JSON 缺失值 | 0（没有需要填的） |

360 个新常量**必然**落进 `UnusedBaseline`（1129 → ≈1489）⇒ 判据红 ⇒ 下一个人"顺手改大基线"，
这条判据就地失效。**两个方向必须选一个**（常量集合与键集合同构 / 常量集合只装"代码真正用到的键"），
本阶段**不替这个决策**，登记为 #52。

### 65.5 收紧 M6：语料**剥注释**（只剥注释，不抹字面量）

M6 的语料此前是 `src/`+`tests/` 的**原文**（不剥注释）—— 于是在注释里写一句键名，
就等于"有人取过它"。这与 **#48**（常量侧「名字即引用」）是**同一个病**，
常量侧阶段 63 已收，键侧收在这条。

| 掩码选择 | 常量侧（看「代码引用」） | 键侧 M6（看「字面量本身」） |
|---|---|---|
| 剥注释 | ✅ | ✅ |
| 抹字面量内容 | ✅（否则字面量提及算引用） | ❌ **绝不能**（M6 找的就是字面量，抹掉等于删掉这条机制） |

⇒ 可复用规矩：**掩码要按机制选**，不是"越严越好"。

### 65.6 收益 1 例 —— 但那 1 例正是「文档把死键写活」

收紧后暴露 **1** 个键：`param.baud.rate`。取证（`git grep`）：

| 出现点 | 形态 |
|---|---|
| `src/HmiEditor.App/Views/CommunicationConfigView.axaml.cs:242` | **XML 文档注释**（说明字段 key → 标签键的推导） |
| `tests/HmiEditor.App.UiTests/LangConstantUsageGuardTests.cs:18` | **XML 文档注释**（P0-5 写下的那条说明，**恰好拿这个键当例子**） |

**两处都是文档注释、没有任何消费者。** 也就是说：**为了记录这个缺陷而写下的文档，
正是维持它"可达"的唯一证据** —— 与 #48（守卫的反断言把常量写活）同一个形状，
这次落在**键**侧。⇒ 不收这条，本阶段为它写的新说明**也会**把它写活（同一形状的又一次）。
它也解释了一个旧事实：阶段 62 记下的「陈旧键（`param.baud.rate` 等 **5** 个）保留」里，
另外 4 个早就在差异表里，**唯独它此前进不来** —— 不是因为有人用它，是因为有人**写它**。

### 65.7 处置：按既定「只增不删」登记（差异表 8 → 9）

`param.baud.rate` 就是这个标本本身（字典里错写成 `param.baud.rate`，字段 Key 实为 `baud`；
阶段 62 的既定结论是「字段 Key 来自外部插件 ⇒ 只增不删」）⇒ 登记进 `KeptUnreachable`（第 9 条），
与另外 4 个 `param.*` 同桶。**交叉核对判据**（见 65.8）正是在这一步同时转红的 ——
它自动把这条差异点了出来，逼着两侧账目对齐。

### 65.8 判据与读数

新增 2 条用例（本地化相关四组 17 → **19**），并在两边各加一条**同源接缝**：

| 新增 | 作用 |
|---|---|
| `UnusedConstants_ClassifiedByKeyMechanism`（3 断言 + 读数） | 分桶读数；断言：机制面非空 / **分类完备** / **无机制桶 ⇄ `KeptUnreachable` 双向一致**（两侧数的是同一批例外） |
| `M6Face_WithoutComments_IsReported`（1 断言 + 读数） | 两条口径的读数 + **单调性**（剥注释只许让面变小） |
| `MechanismMap()` / `DictionaryKeysForReading()` / `KeptUnreachableKeys` | 键可达性守卫开出的**同源**接缝（机制、字典路径、差异表都不抄第二份） |

**七条机制的面**（读数 `.workbuddy/tmp/readings-65-final.txt`）：

```
    M1-Lang 常量         面   643      ← 不动
    M2-axaml res:Loc   面  1113      ← 不动
    M3-插件族派生           面    26      ← 不动
    M4-cfg.note 清单     面    30      ← 不动
    M5-脚本诊断码           面    19      ← 不动
    M6-源码字面量           面   387      ← 388 → 387（本轮唯一动的那个数）
    M7-枚举名派生           面    12      ← 不动
M6 面（判据口径：剥注释）387 / M6 面（旧口径：原文，对照）388 / 注释洞收益 1
```

**负控**（收紧先于处置，两红线都点名同一个键）：

| 步骤 | 实测 | 证据 |
|---|---|---|
| 只收紧、不登记 | **11 条里 2 红**：主判据 `EveryDictionaryKey_IsReachableBySomeMechanism` 红并点名 `param.baud.rate`；交叉核对判据红并报「常量侧 **9** 个 vs 差异表 **8** 个」 | `.workbuddy/tmp/buckets65-red.log` |
| 登记后 | **19/19 绿**（本地化四组）；再扩到六组 **35/35 绿** | `buckets65-green.log` / `buckets65-final.log` |

### 65.9 仍未闭环

- **#35 的残余**：本阶段判的是**可动性**（哪些桶结构上必须存在），**不是**"逐个判定死了"。
  M6 桶那 **274** 个常量里，是否还有"键的唯一边据是测试断言里的一句字面量"这类可收的，
  仍未逐个查 —— 但**方向已明确**（不是清理清单）。
- **#52 的方向 A/B**（常量集合与键集合是否同构）仍开着，但**手滑入口已拆**（见 65.10）。

### 65.10 决策落地：#52 选「只堵陷阱，不动基线」

`#52` 是**决策项**（A 同构 / B 只装代码用到的键），本轮**不选其中任何一个**，
只把"顺手跑一次工具就顶穿判据"这条路径拆掉 —— 最小改动、零基线变动：

| 动作 | 内容 |
|---|---|
| ① 默认值 | `sync_localization.py` 改成 **默认只干跑**：`write = '--write' in sys.argv` / `dry_run = not write`（旧的 `--dry-run` 保留为等价写法，老命令行不被改坏） |
| ② 常量闸 | 「生成常量」**另外**要显式 `--add-consts`（`if add_consts:` 包住整段生成 ⇒ 不开闸时**连 `Lang.cs` 的写入分支都进不去**，而 `Lang.cs` 的写入本来就在 `if new_consts:` 里） |
| ③ 新判据 | `SyncTool_DefaultsToDryRunAndGatesConstantGeneration`（静态守卫，**守的是另一条判据的有效性**）—— 锚点落在**赋值表达式**（`write = '--write' in sys.argv` 这种）而不是旗标字符串：工具的文件头注释里就写着 `--write` / `--add-consts`，锚在旗标上等于让**工具的文档**满足这条判据（本阶段刚收掉的同一个病） |
| ④ 通知 | 跳过生成时打一行 `UnusedBaseline 从 1129 顶到 ≈1489；先读登记 #52` —— 把"为什么不该顺手跑"留在现场 |

**负控 2/2，各自独立命中**（`nc65.py` 注入 → 跑 → 还原，**逐字节校验**）：

| 注入 | 守卫红在 | 证据 |
|---|---|---|
| N1：`dry_run = not write` → 旧的 `dry_run = '--dry-run' in sys.argv`（默认又会写盘） | 「落盘开关/干跑绑定」缺失 | `.workbuddy/tmp/nc65-N1.log` |
| N2：`if add_consts:` → `if True:`（拆掉常量闸） | 「闸没被用在生成段上」 | `.workbuddy/tmp/nc65-N2.log` |
| 还原 | `sha256 bc8e6c50…` + `cmp` 无差异；复跑 **6/6 绿** | 同上 |

**三种模式实测**（都不写盘，工作树只动被编辑的两个文件）：

```
[1] 无参数            → 干跑；打印「⏭️ 跳过『生成 Lang.cs 常量定义』（360 个键在 Lang.cs 里没有常量）」
[2] 只给 --add-consts → 仍是干跑（生成 360 个，仅预览）
[3] 0 个 JSON 缺失值  ⇒ 无参数模式不会改写 JSON
```

### 65.11 门禁

见「验证记录」表末的 **阶段 65** 各行（提交前 / 提交后）。

## 阶段 66（闭环 #50：角色名接线的产品决策落地 —— App 层键推导，模型与生成产物零改动；顺带抓出 #53「两个 `SelectionChanged` 从未接线」与 #54「守卫把界面文案错归成 DATA」）— 2026-09-21

### 66.1 一句话结论

阶段 64 报出的 #50（`role.operator` / `role.engineer` / `role.administrator` 三个键在字典里、
翻好了、**没有任何消费点**）本阶段闭环。**接线方式是 App 层键推导**（新增 `RoleTextKeys`，
仿既有 `PluginTextKeys`）：模型 `UserRole` 与生成产物**一个字节都没改**，8 个显示点接上字典。
顺带清退 `role.maintenance`（字典各 −1 行 + 常量 + 补键表 + 差异表）。
**两个顺带产出比主线更值**：**#53** —— `UserList`/`RoleList` 的 `SelectionChanged`
**自首个提交 `1fc6701`（2026-07-30）起就从未接线**，整块角色管理静默失效（它是 #50 的**前提**，
不接线连症状都观测不到）；**#54** —— 既有守卫把 `UserRole.cs` 里 3 条中文描述按
「DATA：落盘角色表输入」**豁免**了，而这个归类**在代码里不成立**。

### 66.2 病根：症状比登记的更难看 —— 界面显示的是**类型全名**

登记 #50 写的是「下拉绑 `UserRole` 对象、无 `ItemTemplate`」。本阶段补一条**读数**（不是推断）：
从视觉树里读到的原文是 **`HmiEditor.Core.Models.User.UserRole`** —— Avalonia 找不到
`DataTemplate` 时兜底渲染 `ToString()`，拿到的是**类型全名**，**不是「英文名」**。
⇒ 这个症状不可能是「翻译漏了」，只能是「**从来没接过线**」。角色列表那栏则是中英混排
（`Administrator (Lv.15)` + 中文描述）。

### 66.3 先验定性：名字与描述**可以**走字典

接字典之前必须先证「这不是落盘数据」（否则重演 §4 第 27c 项那类错）。三面取证：

| 面 | 取证 | 结论 |
|---|---|---|
| 生成头 | `HMI_SEC_ROLES_INIT` 只发 `Id` / `Name` / `Level` / 掩码四列（`firmware/t113/tests/fixtures/generated_security.h:54-57`） | **不随 UI 语言变** |
| 工程 JSON | `SecurityConfig` **没有** `Roles` 字段 | 角色名/描述**既不下发也不落盘** |
| 界面 | `RoleNameBox` 是 `IsReadOnly="True"`、`RoleDescText` 是只读 `TextBlock`、`OnSaveRole` 只写 `Level`/`Permissions` | 名字/描述是**纯展示** |

⇒ 走字典；模型 `Name` 原样保留给生成头与调试日志。

### 66.4 接线：`RoleTextKeys`（唯一键推导）+ 8 个显示点

新增 `src/HmiEditor.App/Services/RoleTextKeys.cs`，表**手写**且用模型的 `.Id`（Id 字符串不出现第二遍）。
**下拉条目改成 `ComboBoxItem{Content, Tag}`**（Avalonia 无 `ItemTemplate` 时只能渲染 `ToString()`）——
两个下拉**各建一批**（`ComboBoxItem` 是控件，共享实例会在第二处抛 "already has a visual parent"）；
按 Id 选中（`SelectRoleById`），不能直接赋模型对象；`OnDetailChanged` / `OnAddUser` 改读 `(… as ComboBoxItem)?.Tag`。

| 显示点 | 处置 |
|---|---|
| 两个角色下拉 + 工程设置 `CmbRuntimeUserRole` | `ComboBoxItem{Content=DisplayNameWithLevel(r), Tag=r.Id}` |
| 用户列表副行 / 运行时用户列表副行 | 角色 Id → 名字 |
| 角色列表 + 角色详情两处 | 名字与描述走字典 |
| `OnSaveRole` 两处回执 `{0}` | 走字典 |
| **生成头 name 列 / `AuthenticationService` 调试日志** | **刻意不动**（产物契约 / 不是 UI） |

### 66.5 顺带抓出 #53：两个 `SelectionChanged` **从未接线**

构造 `UserManagerView` 时实测 `RoleNameBox.Text` 在选中角色后**仍是空串** ⇒ 追下去：
`UserList` / `RoleList` 的 `SelectionChanged` 从**首个提交 `1fc6701`** 起就从未接线
（XAML 无属性、代码无 `+=`、`ChromeWindow` 不做任何自动接线）。取证：

| 事实 | 读数 |
|---|---|
| `_selectedRole` 的赋值点 | **唯一**一处在 `OnRoleSelectionChanged`（`.axaml.cs:447`） |
| 以 `_selectedRole == null` 早退的动作 | `OnSaveRole`(`:486`)、`BtnSaveRole` 启停(`:493`)、`UpdateRoleUI`(`:498`) ⇒ 等级滑杆 + 9 个权限勾选 + 保存**点了没反应** |
| `_selectedUser` 的赋值点 | 两处：`:176`（处理器内）/ `:265`（`OnAddUser`）⇒ 选中既有用户后改名/改角色/删除/启停/重置口令/解锁**全部静默 `return`** |

**它是 #50 的前提**：不接线，角色详情那两处本地化根本观测不到。
处置**只改 XAML 两处**（不碰 code-behind —— 避免「XAML 声明 + 代码 `+=`」的双路形态，那条已有门禁 `[1d]`）。

### 66.6 顺带纠正 #54：豁免表里的「理由」没人核，就成了一张合法化凭证

`CoreVisibleTextInventoryGuardTests.Pending["Models/User/UserRole.cs"] = 3` 的理由是
「DATA：落盘角色表输入」—— 按 66.3 的取证**不成立** ⇒ 那 3 条是**该翻而没翻的界面文案**，
已搬进字典并从 `Pending` 表移除。**与 #48 同类教训**：豁免表里的理由必须能被代码核，
否则它只是一个**能把缺陷合法化的字段**。

### 66.7 判据（`RoleNameLocalizationTests`，6 条）+ 负控 8/8

| # | 用例 | 判什么 |
|---|---|---|
| ① | `RoleDisplaySurface_IsReported` | **读数**（不是断言） |
| ② | `BuiltInRoles_AndRoleTextMap_AreOneToOne` | `BuiltInAll` ↔ 映射表**双向**差集；表外 Id 推不出键 |
| ③ | `RoleKeyFamily_InBothDictionaries_MatchesRoleTextMap` | 两本字典 `role.*` == 文案表双向（**钉死 `role.maintenance` 不许回来**） |
| ④ | `DisplayedRoleText_ComesFromDictionary_NotFromModelName` | `== 字典值` 且 `≠ 键名` 且 `≠ 模型 Name` —— **缺一条就会被蒙过** |
| ⑤ | `RoleSurfaces_RenderLocalizedText` | 端到端（条目是 `ComboBoxItem`、选中框渲染的是那句话、角色列表三项） |
| ⑥ | `SelectingUserOrRole_FillsTheDetailPane` | #53：选 0 → **换 1**，详情必须跟着换 |

**④ 三条一起的理由**：`LocalizationService.GetString` 对**不存在的键原样返回键名** ⇒
只断言「等于字典值」会**自指**；只断言「不等于模型英文名」会被「随便换个字符串」蒙过。

**负控 8/8**（N1 删映射表行 / N2 加孤儿 / N3 字典加幽灵键 / N4 字典删键 / N5 值改成模型名 /
N6 条目改回 `r.Name` / N7 删 RoleList 接线 / N8 删 UserList 接线）—— **且证明红的是断言、不是编译器**：
脚本第一版把「红」判成「stdout 里没有『已通过!』」，这正是**阶段 21 踩过的坑**
（注入写成语法错误 ⇒ 红在 `CS1525`，**那不是判据的证据**）；改成必须匹配 `失败: N`
**并且**取到 `错误消息:` 那一行，`build-fail` / `unknown` 一律**不算数**。逐条 SHA256 还原校验。

**负控顺带顶出判据自身一处顺序缺陷**：角色那半边先 `Assert.Equal(名字, RoleNameBox.Text)` 再判空
⇒ 没接线时只报泛泛的 `Strings differ`（N7），而用户那半边顺序相反、报错**点名了 #53**（N8）
⇒ 把「填没填」挪到「填得对不对」之前。

### 66.8 读数对拍与门禁

| 面 | 改前 | 改后 |
|---|---|---|
| 两个下拉的条目 | `HmiEditor.Core.Models.User.UserRole` | `ComboBoxItem(Content="操作员 (Lv.3)", Tag=role-operator)` |
| `NewRoleCombo` 选中框 | `"HmiEditor.Core.Models.User.UserRole"` | `"操作员 (Lv.3)"` |
| `DetailRoleCombo` 选中框 | （视觉树里没有 TextBlock） | `"管理员 (Lv.15)"` |
| 角色列表 | `Administrator (Lv.15)` + 中文描述 | `管理员 (Lv.15) \| 管理员 — 完全权限` |
| 角色详情区 | `""` / `""` | `"管理员"` / `"管理员 — 完全权限"` |
| 差异表 `role.*` | 4（表 9） | **0**（表 **5**） |
| 常量「无任何机制」桶 | 9 | **5** |
| M1 面 / M6 残差键 | 643 / 396 | **649** / **392** |

**交叉核对自动转绿**：`KeptUnreachable_EntriesAreStillUnreachableAndStillPresent` 与常量侧
「无机制桶 ⇄ 差异表」两侧**一致地变小**，**未手改任何断言**。

### 66.9 遗留

- 生成头 `HMI_SEC_ROLES_INIT` 的 name 列仍是模型的 `Name`（**产物契约**）——
  将来要让它跟随界面语言，必须**同时**改 golden 与固件侧。
- #35 常量侧「逐个判定死」仍未做（本阶段只让「无任何机制」桶 9 → 5）。


## 阶段 67（闭环 #55：一条「量到了自己不该管的东西」的判据 —— `samples[^1]` 撞上 `DataLogger` 自己的定时器，load-sensitive 假红）— 2026-09-22

### 67.1 一句话结论

`Phase3IntegrationTests.cs:480` 的 `samples[^1].Values[0] > 100.0` 把「**日志确实记下了 150**」
与「**其后没有任何别的采样**」捆在一起，而后者由 `DataLogger` 自己的 1000ms 定时器决定 ——
**判据量到了自己不该管的量**，于是它会随机红而代码并没有错。改成「**存在**一条 > 100 的采样」，
原意完整保留、去掉的只是「顺序」。

### 67.2 病根（读代码坐实，不是猜）

`DataLogger.Start()` 走 `_sampleTimer.Change(0, SampleIntervalMs)`（默认 **1000ms**）⇒
**当场排一个「立即到期」的回调**；`OnSampleTick` 只塞**全 0 的占位采样**
（`DataLogger.cs:232`，注释自己写着「实际数据由外部注入」）。回调由**线程池**派发 ⇒
派发**及时** ⇒ 占位排在 150 之前、`[^1]` 是 150（绿）；派发被**拖到** `AddSample(150)` 之后
⇒ `[^1]` 是 0.0（**红**）。负载越高越容易红。

| 修前读数 | 值 |
|---|---|
| 门禁**并行**跑当天 | **4 次红 1 次**（绿、绿、**红**、绿；第四次是**同代码**再跑） |
| 串行全量 `Core.Tests` | **5/5 绿**（2124 项） |
| 带 filter 单跑 | **3/3 绿** |
| 失败那次**用例自身**耗时 | **22 ms** ⇒ 不是「用例变慢」，正是回调派发被拖后 |

**与当时那次改动无关**：`Core.Tests` 源码在绿与红两次之间**一个字节没改**。

### 67.3 修法 A（拍的板）

```csharp
Assert.True(samples.Any(s => s.Values[0] > 100.0),
    "记录的值应超过报警阈值：注入的 150 应出现在采样里"
    + "（别改回 samples[^1] —— DataLogger 自己的定时器会追加全 0 的占位采样，可能排在它后面）");
```

**修法 B（先 `logger.Stop()`）不采用**：已排队的回调仍会跑（`OnSampleTick` 只检查 `_disposed`），
窗口能变窄、关不死。

### 67.4 负控 4/4

| # | 注入 | 期望 | 实测 |
|---|---|---|---|
| NC1 | 末尾追加一条全 0 占位采样（**精确复现 load-sensitive 场景**） | **绿** | 绿 ✓ |
| NC1b | **同一注入** + **旧写法** `samples[^1]` | **红** | 红 ✓（点名「记录的值应超过报警阈值」） |
| NC2 | 注入值 150.0 → 50.0（低于阈值） | 红 | 红 ✓ |
| NC3 | 注入值 150.0 → **100.0**（恰好等于阈值） | 红 | 红 ✓ |

**NC1 + NC1b 这一对才是 #55 的复现器**（同代码、只差一行写法，新绿旧红）⇒ NC1 的绿是「真修好」，
不是「注入太软」。**NC2 / NC3** 是「判据变弱」的探测器。

⚠ **脚本第一版撞上了阶段 21 那个坑**：NC1b 只替换断言行，残留的 `+ "…"` 拼成 `, + "…"`
⇒ **编译失败** —— 那不是判据红的证据。改成**整块**替换后才算数。逐条还原校验 **SHA256**。

### 67.5 诚实边界

修后 3× 全量 `Core.Tests`（2124）+ 2× 门禁全绿，**不能单独**证明「不再随机红」——
**修前串行也是 5/5 绿**（间歇缺陷的「有一次绿」等于零信息）。依据是**机制**：
触发条件（占位采样排在最后）已影响不到结果，而 NC1/NC1b 把这一点做成了**可执行的事实**。

### 67.6 元教训

`samples[^1]` 是**看起来更严格**的写法（"最后一条也必须是 150"），而它偷偷把一个
**生产代码的调度行为**当成了判据的输入 ⇒ 判据随机红、代码没错。与「口径 / 可动性 /
掩码按机制选」同源：**写断言前先问它管的是哪一件事**。

⚠ 顺带留一个**未处理**的问题：`DataLogger` 这个「全 0 占位采样」本身是不是产品缺陷
（外部不注入时日志里会混进 0 点）—— 属产品语义，不在 #55 范围内。

### 67.7 门禁

提交前 `gate55-pre.log`（mtime `10:27:13`）、提交后 `gate55-post.log`（mtime **`10:28:53`**，
晚于提交 **`10:27:32`**）：均 **7 步全绿 / `GATE_EXIT=0`**、0 警告 0 错误、
**3037 + 8 + 2 = 3047**（与阶段 66 **逐程序集逐条同数** —— 本次只改一条断言，不动用例数）。


## 阶段 68（闭环 #39 的「构造路径」那一半：给 30 个窗口的生产构造路径建运行期消费者 —— 判据形状先按「字段 null → 非空」写，被一条真读数打回，改成**形状签名**）— 2026-09-22

### 68.1 一句话结论

登记 #39 的「构造路径」那一半：`WindowViewSmokeUiTests`（阶段 48）按「有无参构造」取面 ⇒
29 个窗口里 **27 个被统一走到设计器路径**（字段写 `_x = null!`、不订事件、不装数据），
而用户点菜单走的是**生产路径** —— 它在运行期判据里长期**没有任何消费者**。
本轮补上：新增 `WindowProductionPathUiTests`（33 条），逐窗口真跑
**构造 → Show → 布局 → 关窗**，并把每个窗口的**生产入口**钉在表里。

### 68.2 判据形状被一条真读数打回（本阶段最有价值的一步）

第 2 条判据（「真的走了生产路径」）第一版是：同一类型各构造一份（设计器 / 生产），
比较**自有字段里 `null` 的个数**，要求生产侧**至少填上一个**。

首跑 31 红 / 1 通过，其中唯一一条**不是**环境问题的红是：

```text
ValidationResultView 的生产路径与设计器路径在自有字段上完全一致
（两边都还有 0 个 null 字段，一个都没被填上）；自有字段总数 8
```

追下去发现**判据错、代码对**：`ValidationResultView` 的设计器构造把依赖字段赋成
`Array.Empty<GeneratedCodeFinding>()` —— **非空，但是空的**。只看「null → 非空」这一维，
两条构造路径**看起来完全一样**，判据就会对**真实存在的那条窗口静默放过**。

**修法**：把签名从「是不是 null」升级成**形状**（四档：`null` / `count:N` 集合元素个数 /
`str:空`·`str:非空` / `非空`），要求两份的签名**至少有一处不同**。改完该窗口的读数是：

```text
形状签名变化 1 处：_findings=count:0 → _findings=count:1
```

⇒ 「空集合」与「null」**是两档**，`0 → 1` 才算一次真变化。全量 30 条里唯一两个
「签名变化：无」的是 `TwoPaths: false` 的两条（`DeviceManagerView` / `StylePresetPicker`）——
它们没有第二条构造，第 2 条判据**按设计不对它们生效**。

### 68.3 另一处收紧：签名在 `Show()` 之前读

生产实例的签名刻意在 `Show()` **之前**取。`Show`/布局会顺带初始化一批惰性字段，
那是**渲染**的功劳、不是**构造**的，读进去会把判据泡软（「生产构造装了依赖」会被
「窗口打开过」顶替）。两份签名于是都在**构造刚结束**时取，是同一口径的对比。

### 68.4 表与代码同源（三层判据的第 3 层）

| 判据 | 形状 |
|---|---|
| `Depot_SuppliesEveryDependency` | 夹具自检：12 个依赖逐个断言非空 + 六项数据集合的元素个数**不含 0**（夹具里藏一个 null/空集合 ⇒ 生产路径会退化成「设计器路径 + 一次赋值」，那种绿是假绿） |
| `ProductionPath_ConstructsShowsLaysOutCloses_AndStartsWithRealDependencies` | 逐窗口一条用例（30 条 Theory）；报错**点名窗口**并打印两侧完整签名 |
| `EveryConstructibleWindow_IsOnTheProductionPathTable` | 面守卫，**两侧都做双向差集**：面里的窗口必须「在表里」或「在豁免表里写明理由」；表里不许留着已改名的类型；豁免表不许有过期条目；另查 Key 撞车 |
| `TwoPathsFlag_AgreesWithTheDeclaredConstructors` | 元检查：`TwoPaths` 的判定必须与真实构造签名一致（没有这条，「表说是两路径其实只有一个构造」会让判据静默少测一半） |

### 68.5 负控 4/4

| # | 注入 | 期望红在哪条 | 实测 |
|---|---|---|---|
| NC1 | 表里删掉 `ValidationResultView` 这条 | 面守卫（「这个窗口的生产路径没有任何运行期消费者」） | **红** ✓，日志含 `ValidationResultView` |
| NC2 | 豁免表塞一条不存在的类型 | 面守卫的**过期**检查 | **红** ✓，日志含 `不存在的窗口` |
| NC3 | **把该窗口的 `Build` 换成无参构造**（即真的走了设计器路径） | 判据二（形状签名无变化） | **红** ✓，日志含 `形状签名` |
| NC4 | `DeviceManagerView` 的 `TwoPaths` 由 false 改成 true | 元检查 | **红** ✓，日志含 `DeviceManagerView` |

**基线（未注入）四条命令全绿** —— 负控的「正控」那一半。**NC3 是本阶段的核心那条**：
它证明判据二真的能抓住「窗口没走生产路径」，而不是靠环境或构造失败变红。
逐条还原后 **SHA256 与原文逐字节相同**；还原用「写回原文」，**不用** `shutil.copy2`
（它会连 mtime 一起复制 ⇒ MSBuild 判定无需重建 ⇒ 正控实际跑的是负控那次的二进制）。

### 68.6 诚实边界

① 「签名不同」证明的是**构造路径不同**，**不是**「用户看到的界面正确」—— 后者由
那一族按钮判据（菜单 / 工具条 / 对话框内 / 停靠面板，四层 170 个按钮）覆盖。
② 本层**不点按钮**、**不判** `{StaticResource}` 类型匹配。
③ 3 个 private 嵌套对话框够不着（`RecipeEditorView` 的 `TextInputDialog`/`ConfirmDialog`、
`AnimationEditorView` 的 `ConfirmDeleteDialog`），豁免表里写明理由，并由
`WindowViewSmokeUiTests` 的 `ReachedByAHost` 反向核实「宿主真的构造了它们」。
④ 判据用的是**反射取自有字段**（遇到非 `HmiEditor.*` 基类即停）：Avalonia 基类字段两边
本就一样，放进来会把读数淹掉。

### 68.7 门禁

`bash scripts/ci-gate.sh` **7 步全绿 / `GATE_EXIT=0`**、0 警告 0 错误。
八程序集合计 **3070**（阶段 67 是 **3037** ⇒ **+33**，恰为本轮新增判据类的用例数；
其余七个程序集逐条同数），另两段独占 **8 + 2** 未变 ⇒ **3047 → 3080，纯净增**。

## 阶段 69（复核 #38 遗留① 时**推翻它的定性**，并顺带量出真问题 —— 全仓绑定的编译期校验挂在一条**自相矛盾**的包内默认值上；登记为 #56 并当轮闭环）— 2026-09-22

### 69.1 一句话结论

登记 #38 遗留①（「只查键能不能解析，不查**类型对不对**」）原文断言那类不匹配
「同样静默」。**实测不成立** —— 三类 XAML 失效**各有拦截点**，没有一类落在缝里。
复核过程顺带量到的**真问题**是另一个：决定「XAML 里的 `{Binding}` 走不走编译期校验」的那个属性
`AvaloniaUseCompiledBindingsByDefault`，**全仓一处都没显式声明**，
实际值来自 Avalonia 12.1.0 包内**两个相反**的默认，靠导入顺序才今天是 `true`。
本轮把它变成**显式契约**（`src/Directory.Build.props`）+ 4 条判据 + 负控 4/4。

### 69.2 #38① 的定性更正（三类都实测过）

| XAML 失效形态 | 真实拦截点 | 实测证据 |
|---|---|---|
| **缺失键**（幽灵键） | **唯一真静默**的一类；由 `XamlResourceKeyClosureTests` 的静态面守 | 注入幽灵键 ⇒ 键闭合判据红、另两族绿 |
| **类型不匹配**（`StreamGeometry` → `IBrush` 属性） | **构造期** `InvalidCastException`，**带行号** | `System.InvalidCastException : Specified cast is not valid.` @ `AlarmListView.axaml:line 88`（栈顶 `CompiledAvaloniaXaml.XamlDynamicSetters.<>XamlDynamicSetter_4`） |
| **绑定路径 typo**（`DataTemplate` 内绑不存在的属性） | **编译期** `error AVLN2000` | 注入 ⇒ build-fail |

原文举的那个例子（`{StaticResource X}` 取到 `Color` 而属性要 `IBrush`）**根本不是缺陷**：
`StaticResourceExtension.ProvideValue` 把**目标属性的 `PropertyType`** 一路传给
`ColorToBrushConverter.Convert(value, targetType)`，而那个转换器的全部内容**就是**为这种转换写的
（`targetType == typeof(IBrush)` 且值是 `Color` ⇒ 包成 `ImmutableSolidColorBrush`；立即赋值与延迟赋值
两条路径**都调它**）。注入 `Background="{StaticResource HmiAccent}"` 实测**全绿**。

IL 里那条「吞异常」的代码**是真的存在**（`DelayedBinding/ClrPropertyValueEntry::Apply`，
Avalonia.Markup 12.1.0，IL 6786-6843：`catch (Exception) { Logger.TryGet(Error, "Property").Log(…) }`），
**只是开了编译绑定之后本仓够不着它** —— XAML 编译器为控件属性生成的是硬转型 setter，类型不对**直接抛**。

⇒ 把它记成「遗留」是**定性错了**，不是「没做完」。

### 69.3 真问题（新登记 #56）：校验挂在隐含默认上

包内**两个文件给了相反的值**，都以「未设置时才生效」为条件：

```text
build/Avalonia.props:8              -> true    （.props 先于 .targets 导入 ⇒ 目前实际生效）
build/AvaloniaBuildTasks.targets:5  -> false   （条件已不满足 ⇒ 被上面压住）
```

⇒ 今天之所以是 `true`，靠的是**导入顺序**这一个巧合。包一升级改了任一处，校验就没了 ——
而且**失去的方式是静默的**：`{Binding}` 退回反射绑定，编译期 0 警告，
运行期的失败出口 `Avalonia.Logging.Logger.Sink` 全仓又无人接（`grep ILogSink src/ tests/` 零命中）。

**实测三态**（`.workbuddy/tmp/exp-cb.py` 注入台；命令是 `dotnet build App.csproj`，
证词读 `obj/**/*.GeneratedMSBuildEditorConfig.editorconfig` 里的 `build_property.*`）：

| 态 | `src/Directory.Build.props` | 绑定 typo 注入 | MSBuild 证词 |
|---|---|---|---|
| 原始（无声明） | 无 | build-fail（`AVLN2000`） | `true`（靠包内默认） |
| 注入 | `false` | **`BUILD_EXIT=0`、0 警告 0 错误** | **`false`**（mtime 13:07:48，dll 13:07:52） |
| 还原 | 无 | build-fail | `true`（mtime 13:08:38） |

产物侧佐证：`false` 下 `{Binding}` 编译成 `Avalonia.Markup.Xaml.MarkupExtensions.ReflectionBindingExtension`
（`typeref` 里没有 `CompiledBinding`）。⚠ **反向提醒**：`CompiledAvaloniaXaml.XamlDynamicSetters`
**不能**当编译绑定生效的证据 —— 实测设成 `false` 后它**照旧存在**（那是 XAML 预编译的产物，与本开关无关）。

### 69.4 落地：显式声明 + 4 条判据

`src/Directory.Build.props` 加一行（带上「不要删」的理由与实测数字）：

```xml
<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
```

`tests/HmiEditor.App.UiTests/XamlCompiledBindingGuardTests.cs`（4 条，面从 `.axaml` 反推不写死项目名）：

| 判据 | 守什么 |
|---|---|
| `EveryXamlProject_DeclaresCompiledBindingsOnItsNearestDirectoryBuildProps` | 每个含 `.axaml` 的项目，**向上找最近的 `Directory.Build.props`**（= MSBuild 的查找规则），必须有**无条件**、**唯一**、值为 `true` 的显式声明 |
| `MsBuildTestimony_AgreesWithTheDeclaration` | **传导**：MSBuild 自己落盘的 `build_property.*` 必须与声明一致（**非推断**证据）。面限定到含 XAML 的项目（其余 11 个不引用 Avalonia，没有这个属性是**不适用**、不是失败）；`obj/` 不存在时 fail-soft |
| `CommentStripping_IsWhatKeepsProseFromSatisfyingTheSearch` | 自检：证明「找得到」不是被**自己的散文**满足的 |
| `ThePropertyHasExactlyOneDeclarationSite_AndNothingReroutesBuildPropsLookup` | 唯一出处 + 不许有 `ImportDirectoryBuildProps` / `DirectoryBuildPropsPath` 改道 |

**本轮最有价值的两个实测更正**（都是判据自己首跑打回来的）：

1. 🩸 **`XDocument.Parse` 天然免疫注释** —— 注释里的 `<Foo>false</Foo>` **不是元素**，
   元素面读不到它。所以「剥注释」在**元素面**（判据一）是**冗余保险**，
   真正**必需**它的是**子串面**（判据四的 `text.Contains`）：实测同一段合成片段，
   不剥注释命中 **4** 次（一个元素算开闭各一次）、剥注释后 **2** 次。
   判据三把这两侧**都**断言（#48 的教训），并且最初那一版断言是**写反的**（以为元素面会被散文骗）。
2. 🩸 **`dotnet test` 会先 build** ⇒ 「改了声明但没重建」这个场景**测不出来**（build 顺手把证词刷新了）。
   要测它得 `--no-build`。本轮负控因此统一走「注入 → 重建 → 测」，而这恰好证明了**传导是真的**。

同时**更正了 `XamlLoadLogUiTests` 的定位**（它的文件头原写「#38① 的收口方式」）：
它不是收口，是一张**探测网**（编译期那一半由本阶段这 4 条守）——
它的真实价值是 ① 第一次把 `MainWindow` 铺进 XAML 冒烟面（它在命名空间 `HmiEditor.App`，
不在 `WindowViewSmokeUiTests.WindowCatalog()` 的 `HmiEditor.App.Views` 口径里）、
② 把「Avalonia 的错误出口存在且可接」钉成自检、③ 覆盖编译期看不见的运行期路径。
**产品代码 0 行改动**（只动了构建文件与测试）。

### 69.5 负控 4/4（`.workbuddy/tmp/nc69.py` + `nc69-run.sh`）

| # | 注入 | 期望红在哪 | 实测 |
|---|---|---|---|
| NC1 | props 的显式值改 `false` | 判据一（「必须是 true」）+ 判据二（证词 `false`） | **2 红 ✓**（两条消息各自点名） |
| NC2 | **删掉**显式声明 | **只有**判据一（「没有显式声明」） | **1 红 ✓** —— 判据二**绿**（包内默认回到 true） |
| NC3 | `HmiEditor.App.csproj` 里**再声明一次**（值 `false`） | 判据四（第二出处）+ 判据二（后设 ⇒ 盖掉 props） | **2 红 ✓** |
| NC4 | `HmiEditor.App.csproj` 里加 `DirectoryBuildPropsPath` | **只有**判据四（改道） | **1 红 ✓** |

**NC1 与 NC2 合起来才说明问题**：NC2 下**只有**判据一红 —— 也就是「隐含的 true」与
「显式的 true」在**判据二眼里完全一样**（证词都是 true）⇒ 两维**互补、都不可替代**。
每次注入的红集合都精确等于预期（「只红这些」），判据三全程绿。
逐条还原后 **SHA256 逐字节一致**（`nc1`/`nc2` 还原到同一份 props、`nc3`/`nc4` 到同一份 csproj）；
还原用**写回原文**，**不用** `shutil.copy2`（它连 mtime 一起复制 ⇒ MSBuild 判定无需重建）。

### 69.6 诚实边界

① 本阶段**没有修任何产品缺陷** —— 它是「把一条隐含契约显式化 + 建判据」。`false` 那个态
**本来就没在发生**（今天实际值是 `true`），所以这条判据的价值是**防未来**（升级 / 误改）。
② 判据二在 `obj/` 不存在时 fail-soft（干净克隆 + 未构建时会跳过）—— 门禁与正常开发都先构建，
所以它**在被用到的地方**都有牙齿，但「跳过」这件事写进读数，不假装判过。
③ 判据四的 NC4 用的是**指向同一份文件**的改道（行为等价）——
它证明的是「判据四会命中这个写法」；**真改道会让构建本身崩掉**（丢了隐式 using 等），
所以没法用真改道来做负控，这一点如实标注。
④ 本层**不判**任何单个绑定对不对（那是编译器的事），只保证**编译器仍然在管**。

### 69.7 门禁与提交

提交 **`bc46863`**（`build(xaml)`，3 files，**+967**）→ **`355da4b`**（`docs(阶段69)`，3 files，+256/−1）。

| 时刻 | 日志 | 结果 |
|---|---|---|
| 提交前 13:13:31→13:14:37（65 s） | `ci-gate-2026-09-22-phase69.log`（mtime **13:14:37**） | **7 步全绿 / `GATE_EXIT=0`**、0 警告 0 错误 |
| 提交后 13:17:56→13:18:54（58 s） | `ci-gate-2026-09-22-phase69-post.log`（mtime **13:18:54**） | **7 步全绿 / `GATE_EXIT=0`**；与提交前**逐程序集逐条同数** |

**时序成立**：提交后日志 mtime **13:18:54** 晚于 HEAD 提交时间 **13:17:50**。

大队 **3070 → 3079（+9）**：其中 **+4** 是本阶段新增的编译绑定守卫；
另 **+5** 是上一轮（#38① 复核）建起的 `XamlLoadLogUiTests`，**它此前从未进过全量门禁**，
本阶段首次纳入（不是本阶段写的，但在本阶段第一次被门禁守到）。
另两段独占 **8 + 2** 未变 ⇒ **3080 → 3089**。
逐程序集：Simulator 44 / Hardware 44 / Communication 75 / Device 87 / Render 4 / AI 315 /
**Core 2116** / **App.UiTests 394**（外加独占段 Core 8、App.UiTests 2）—— 两次门禁完全一致。

## 阶段 70（闭环 #27 的「`src/HmiEditor.AI` 不在扫描面」那一半：41 条 AI 诊断文案接字典，键可达机制新增 **M8**）— 2026-09-22

### 70.1 一句话结论

`src/HmiEditor.AI` 产出的诊断文案（AI 输出校验 + 布局建议）此前是**没接字典的字面量**，
而它们经**三条路径**直接显示给用户 ⇒ 英文界面下「本地化的前缀 + 中文正文」。
本阶段把这 **41 条**接上字典（两本各 +41 行）、给它们一套**可反射枚举的结构化码**、
并新增第八条键可达机制 **M8** 把它们纳入守卫。**AI 层的判定逻辑一行未改。**

### 70.2 起点：把「程序化事实」翻译成「缺陷」

登记表写的是「`src/HmiEditor.AI` 不在扫描面（提示词/Lua 模板）」—— 那是**程序化事实**，
不是缺陷本身。先量后果，量出三条**用户可见**路径：

| # | 消费点 | 显示在哪儿 |
|---|---|---|
| ① | `AiChatPanelView.axaml.cs` 4 处 `Detail` 拼接 | AI 助手面板聊天条目（校验失败逐条列问题） |
| ② | `AiGenerateDialogBase.cs:99-103`（取前 5 条） | AI 模板/脚本生成对话框的状态行 |
| ③ | `PropertyPanelView.Ai.cs:59-65` | 属性面板「AI 建议」列表（`LayoutAnalyzer` 布局问题） |

三处**同一行里**前缀都已本地化（`Lang.AiErrValidationScreen` / `"ai.gen.failed"` /
`Lang.PropIssueAutofix`），只有正文是写死中文。

**数量**：`AiOutputValidator` 30 个构造点（27 个唯一码）+ `AiOrchestrator` 2 +
`LayoutAnalyzer` 9 个调用点（12 个唯一码）= **41 条**。

### 70.3 为什么不能在调用点直接写整键字面量

键要有**稳定标识**。用中文原文当键 ⇒ 改一个字的措辞就换键、且反射枚举不到；
在 AI 程序集写 `"ai.issue.screen_empty"` ⇒ 违反「Code 只做键推导/分派」的分层，
且让 AI 依赖 App 的键命名（依赖方向是 App → AI）。⇒ 走与 M5（Lua 脚本校验）**同形**的第三条路：

```
AiIssueCodes 声明「码」 → ExternalText.AiIssueKey 推导「键」 → 字典存「模板」 → Resolve 渲染
```

### 70.4 落地

| 文件 | 改动 |
|---|---|
| `src/HmiEditor.AI/AiIssueCodes.cs` | **新建**，41 个 `public const string`（必须 `const` 才能反射枚举） |
| `AiOutputValidator.cs` / `AiOrchestrator.cs` | 32 个构造点改走 `AiValidationIssue.Of(severity, code, 回落模板, args…)` |
| `Models/LayoutAnalysisResult.cs` / `Services/LayoutAnalyzer.cs` | `LayoutIssue` 增 `Code`/`Args`；`Message` 改由模板 + 实参渲染 |
| `App/Services/ExternalText.cs` | 加 `AiIssueKeyPrefix`/`AiIssueKey()` + 两个 `Describe` 重载 |
| 那三处消费点 | 改调 `ExternalText.Describe(...)` |
| `Strings.zh-CN.json` / `Strings.en-US.json` | 各 **+41 行**（2134 → 2175；插入序追加，没有重排） |

**`Message` 由模板渲染、不由调用点手写**：`Message` 与 `Args` 是同一句话的两个版本，
各写一遍必然有一处先过期 ⇒ 由 `string.Format(回落模板, args)` 生成，
于是「zh 模板渲染 == 回落原文」这条判据比的是**同源的**两份文本。
**数值型实参先格式化**（`Fmt`/`Fmt1` 对应原来的 `{c.X:F0}`/`{ratio:F1}`）——
字典模板只能写 `{1}`，格式说明符只能留在调用点，两侧数值形态不一致会把**格式差异**报成文案差异。

### 70.5 判据：`AiIssueLocalizationGuardTests`（7 条）

| 判据 | 守什么 |
|---|---|
| `EveryProducedCode_ResolvesInBothLanguages` | 每个真产出的码，键在**两本字典**都能解析 |
| `PlaceholderCount_MatchesArgsCount` | 占位符个数 == 实参个数（`Format` 不符时**不抛异常**，用户看到 `{1}` 字面量） |
| `ChineseTemplate_RendersToTheAiFallback` | zh 渲染 == 回落原文（**自动**比对，不手写清单） |
| `ProducedCodes_AndDeclaredCodesAreTheSameSet` | **双向**：产出集合 ⟷ 反射枚举的声明集合 |
| 三条 `NegativeControl_*` | 见 70.7 |

**全 41 条真触发、无豁免表**：两条编排码看似要连 LLM，其实都在「还没轮到调模型」时返回
（`_imageGenerator is null` 的早退 / 意图落到 `Unknown`）⇒ 只要把三个生成器构造出来即可。
断言用的假 provider **被真调用就抛**。

**双向判据挡的是两件不同的事**：「产出了没声明」= 调用点写了**字面量码**（能编译能运行、
键不会进字典）；「声明了没产出」= **死码**或样本漏了（登记 #49 是同一个坑的常量版）。
修法不同 ⇒ 失败消息分开写。

### 70.6 机制侧：键可达性 **七条 → 八条**

`LocalizationKeyReachabilityGuardTests` 加 **M8-AI 诊断码**（面 **41**），取数方式**复用**
`AiIssueLocalizationGuardTests.DeclaredCodes()`（不另抄一份反射）。下界按满值登记。

| 机制 | 面 | 与阶段 66 对比 |
|---|---|---|
| M1-Lang 常量 | 649 | 同 |
| M2-axaml res:Loc | 1113 | 同 |
| M3-插件族派生 | 26 | 同 |
| M4-cfg.note 清单 | 30 | 同 |
| M5-脚本诊断码 | 19 | 同 |
| **M8-AI 诊断码** | **41** | **新增** |
| M6-源码字面量 | 387 | **同** |
| M7-枚举名派生 | 12 | 同 |

🩸 **M6 面没涨是刻意的正面证据**：这 41 个键**没有被**「源码整键字面量」这条代理机制顺带覆盖
（AI 层写的是**码**不是键）⇒ 它们**只靠 M8 活着**，M8 一失效主判据立刻红。
⇒ 固定动作：**新增机制时顺手确认别的机制的面没有跟着涨** —— 涨了意味着新键有了隐形担保。

### 70.7 端到端负控 3/3（`nc70.py` + `nc70-run.sh`，注入的是产品代码/字典）

| 场景 | 注入 | 期望红在 | 实测 |
|---|---|---|---|
| `nc1` | `AiIssueCodes` 加一个没人产出的码 | 「声明了但产出不到」+ 点名 `ZZZ_UNUSED_PROBE` | 红 **2** 条 ✓ |
| `nc2` | 一处调用点换成字面量码 `"CONTROL_OVERLAP_PROBE"` | 「产出了但没声明」+ 点名它 | 红 **7** 条 ✓ |
| `nc3` | zh 字典删掉一条 `ai.issue.*` 键 | 键覆盖（缺 zh-CN） | 红 **5** 条 ✓ |

判红谓词匹配测试**自己的汇总行**（`失败: N`，N>0）**且**取到 `错误消息:`。
还原**写回原文**（不用 `shutil.copy2`），三个文件与存档 `cmp` **逐字节一致**、注入痕迹 grep 计数 **0**。
注入用**不存在的名字**（用真名会重演 #48：判据里的字面量反过来把自己喂饱）。

### 70.8 诚实边界（4 条）

① **只覆盖「AI 诊断」这一族**：提示词模板（`Inference/PromptTemplates/*.txt`、`AgentSystemPrompt.cs`、
`AgentTools`）与**意图关键词表**（`ClassifyIntent` 的匹配词）**刻意不本地化** ——
翻译会改变模型行为与识别结果，属独立议题。
② **异常消息仍会透到界面**：`AiGenerateDialogBase` 的 `catch (Exception ex)` 走
`Format("ai.gen.failed", ex.Message)`，而 `LlmHttpProviderBase`/`OpenAIProvider`/`AgentEngine`
抛的是中文 —— 与 App 层既有的「异常消息面向开发者、不翻译」同口径，**未动**。
③ **`Message`（中文回落）保留在 AI 层**：`HmiEditor.AI.Tests` 有 **15+ 处**
`i.Message.Contains("…")` 断言，删它是破坏性变更；也与 `ScriptError.Message`/`ValidationIssue.Message` 同形。
④ **枚举名没本地化**：`INTENT_UNSUPPORTED` 的实参是意图枚举，中英两版都显示 `'Unknown'`。

### 70.9 门禁与提交

| 时刻 | 日志（mtime） | 结果 |
|---|---|---|
| 提交前 14:38:44→14:39:57（73 s） | `.workbuddy/tmp/ci-gate-70-pre.log`（**14:39:57**） | **7 步全绿 / `GATE_EXIT=0`**、0 警告 0 错误 |

大队 **3079 → 3086（+7）**（= 新守卫 7 条）；独占 **8 + 2** 不变 ⇒ 总 **3089 → 3096**。
逐程序集：Simulator 44 / Hardware 44 / Communication 75 / Device 87 / Render 4 / AI 315 /
Core 2116 / **App.UiTests 394 → 401**。
`HmiEditor.AI.Tests` **315 项未变** —— 改完 AI 层时先单独跑过一次，专门验「类型改造有没有破坏既有断言」。
专题：`docs/2026-09-22-ai-issue-localization.md`。

## 阶段 71（闭环 #38 遗留③「定义但从未被引用」的 50 个键：21 条几何的双份 + 引用面 3→5 类 + 定义侧反向守卫）— 2026-09-22

### 71.1 一句话结论

登记表那句「50 个」**是准的**，但它**不是 50 个死键**：**21 个是真缺陷**
（绘图/快捷图标的几何在 `Assets/Icons.axaml` 与 `MainWindow.axaml` **各存一份、逐字相同**）、
**26 个是引用面漏了两种调用形状**、**3 个是合法例外** ⇒ 修完之后孤儿 **50 → 3**、
**真死资源 0 个**。

### 71.2 主缺陷

`grep -rn "IconDraw\|IconQuick" src/ tests/ | grep -v Assets/Icons.axaml` ⇒ **0 命中**；
而 `MainWindow.axaml` 里有 **21** 处 `<Path Data="几何字面量"/>`，逐条与 `Icons.axaml`
的键**几何全等**。成因写在当地注释里（`PathIcon` 的 `Fill` 会把描边图标渲染色块/空白
⇒ 换 `Path` + `Stroke`）—— 诊断对、**结论跳错一步**：「换控件类型」与「必须内联几何」是两件事。

**为什么 3000+ 条判据没有一条看得见**：`XamlResourceKeyClosureTests` 只走 **引用 ⇒ 定义**；
反向「定义 ⇒ 有人引用」从建类起**一直是空的**。

### 71.3 修法与唯一技术风险的实测

21 处改走 `{StaticResource IconXxx}`（脚本程序化取几何建映射、默认干跑、回读校验）。
**零视觉变化**。唯一技术风险是「会不会重演 Fill 问题」—— 由行为判据实测：
生产构造路径下 21 个 `Path.Data` 与字典取到的对象**引用相等**、`Stroke` 非空、`Stretch=Uniform`
（读数：21 / 21 / 21 实例 / 0 条不符）。判据**不写「名字 → 图标键」的表**，取面用结构
（`<Path>` 挂在绘图 `ToggleButton` 或 `drw-qbtn` 的 `Button` 上）。

### 71.4 引用面 3 类 → 5 类

新增 ③ `SetOrUpdate*(resources, "K", …)`（22 处，⚠ 只证明「被管理的资源」，读者在包外）
与 ④ `Theme.Brush("K")` / `Color("K")`（26 处）。⑤ 主题变体字典键**原理上不可枚举** ⇒ 进差异表。

### 71.5 判据（新增 4 条）

`EveryDefinedResourceKey_IsReferencedOrExplicitlyExempt` /
`UnreferencedExemptions_AreStillUnreferencedAndStillDefined`（双向过期检查）/
`ReverseClosureFace_IsNotDegenerate`（定义面、交集、**五类形状各自下界**）/
`DrawingToolbarIcons_AreTheIconDictionaryInstances`（行为）。
差异表只有 **3** 条：`Light`、`Dark`、`DockToolChromeIconBrush`。

### 71.6 端到端负控 5/5

nc1 内联几何回退 / nc2 差异表塞可达键 / nc3 塞不存在的键 / nc4 删掉真实引用 /
nc5 把 `theme` 形状正则写坏 —— 各自判红并命中期望子串（红 2/1/1/2/2 条）。

### 71.7 顺带发现

① 🩸 **类注释里有一条已被推翻的断言**（「类型不匹配同样静默」—— 阶段 69 已推翻，
登记表改了、类注释没改）⇒ 一并修正。教训：**定性被推翻时要连它散落过的每一处一起改**。
② `IconResetTransform` 与 `IconReloadPanels` **共用同一份几何**（两个不同动作同一字形），
**未改**（视觉/产品决策）—— 它说明「同一份几何」还有一种**合法**形态。

### 71.8 诚实边界（4 条）

① 只说「引用面取不到」，不说「没人读」；② 反向判据的面只到 `src/HmiEditor.App` 的 `.axaml`
（当前完整**靠事实不靠机制**）；③ 引用相等只在 21 个工具栏图标上断言；④ 未做像素级比对。

### 71.9 门禁与提交

提交 **`a395695`**（`fix(xaml)`，3 files，**+280/−27**）→ **`0540d45`**（`docs(阶段71)`，3 files）。

| 时机 | 日志（mtime） | 结果 |
|---|---|---|
| 提交前 →14:56:24 | `.workbuddy/tmp/ci-gate-71-pre.log`（**14:56:24**） | **7 步全绿 / `GATE_EXIT=0`**、0 警告 0 错误 |
| 提交后 14:56:5x→14:57:52 | `.workbuddy/tmp/ci-gate-71-post.log`（**14:57:52**） | 同上，且与提交前**逐程序集逐条同数** |

大队 **3086 → 3090（+4）**（= 新增判据 4 条）；独占 **8 + 2** 不变 ⇒ 总 **3096 → 3100**。
逐程序集：Simulator 44 / Hardware 44 / Communication 75 / Device 87 / Render 4 / AI 315 /
Core 2116 / **App.UiTests 401 → 405**。
HEAD 提交时间 `0540d45` = **14:56:50** ⇒ 提交后日志 mtime 晚于它，**时序成立**。
**产品代码只改了 21 个 `Data=` 属性**（同一份几何改走资源引用，零视觉变化）。
端到端负控 **5/5**；注入痕迹 grep **0**；工作树提交后为空。

专题：`docs/2026-09-22-xaml-resource-key-reverse-closure.md`。

## 阶段 72（工具箱图标 ↔ 图标字典的单一来源：同一个控件的两套几何，**4 条当时就已经分叉**）— 2026-09-22

### 72.1 一句话结论

同一个控件类型的图标在两处各画一份：**工具箱**走 `ToolboxView.ControlIcon` 里一张
C# switch 表（几何是字符串字面量 + `StreamGeometry.Parse`），**主窗口绘图工具栏**走
`Assets/Icons.axaml`（65 条资源键）。两套命名空间、两套扫描面 ⇒ **没有任何判据看得见它们的关系**。
实测 26 个类型里 **19 个两边同名同义**（`IconQuick<T>` 14 / `IconDraw<T>` 5），
其中 **15 个逐字相同、4 个已经分叉** —— **用户在同一个屏幕上能同时看到**那 4 条画得不一样。

### 72.2 主缺陷：比阶段 71 那批更危险

阶段 71 修的 21 条是「同一份几何两处存放、**逐字相同**」—— 只会在**下一次**改动时分叉。
这 4 条**当时就已经分叉**，是当下可见的视觉不一致，且同样静默（编译期零警告、运行期不抛）：

| 类型 | 工具箱（修前） | 字典 `IconDraw*` |
|---|---|---|
| `Rectangle` | `M5,5 L19,5 L19,19 L5,19 Z` | `M4,4 L20,4 L20,20 L4,20 Z` |
| `Ellipse` | `M12,5 A7,7 0 1 0 12,19 …` | `M12,4 A8,8 0 1 0 12,20 …` |
| `Polyline` | `M5,17 L10,9 L14,13 L19,5` | `M4,17 L9,9 L13,13 L20,5` |
| `Arc` | `M5,12 A7,7 0 1 0 12,5 M12,5 L17,5` | `M5,13 A7,7 0 1 0 12,6 M12,6 L17,6` |

`IconDraw` 那 5 个里只有 `IconDrawLine` 没分叉。
⚠ **两个口径别混**：首轮探针按「几何串在字典里**存不存在**」判，得 16 / 4 / 6
（`Ellipse` 的圆恰好逐字等于 `IconQuickLed`，被那个口径误收进「逐字相同」）；
判据按**镜像键**判，得 19 / 7。所以 `16 + 4 ≠ 19` 不是笔误。

### 72.3 修法

4 条几何对齐到字典侧（`git diff` 逐字为证）。选「以字典为准」：字典是**共享**的那一份
（65 条键，工具箱只是 26 条的子集），改动面更小、也更符合单一来源的方向。
`ControlIcon` 由 `private static` 改 `internal static`，**只为判据可调用**
（`InternalsVisibleTo` 早已存在），不是产品代码的构造 API —— 已写进当地注释。

### 72.4 判据（`ToolboxIconDictionaryTests`，4 条）+ 两张差异表

| # | 判据 | 守什么 |
|---|---|---|
| 一 | `EveryToolboxIcon_MatchesItsDictionaryIcon` | 19 个镜像键逐个**逐字一致**；失败消息打出两边几何 + `Bounds` |
| 二 | `ToolboxTypesWithoutDictionaryMirror_AreStillTheKnownSet` | 枚举有、字典没有的 7 个；**双向**（补了镜像键却不接工具箱 ⇒ 红） |
| 三 | `MirrorKeyDerivation_IsUnambiguous` | 不许同类型同时有 `IconDraw<T>` 与 `IconQuick<T>`（推导会静默取前者） |
| 四 | `DictionaryMirrorKeys_WithoutAControlType_AreStillTheKnownSet` | **反方向**：字典镜像族键后缀必须是枚举成员（21 个里 2 个不是：`IconDrawSelect`/`IconDrawText`） |

机制＝**按命名约定推导**（`IconDraw<枚举名>` / `IconQuick<枚举名>`），不写硬表 ——
表会过期、约定不会。代价是「名字对不上就看不见」，所以两张差异表（7 条 + 2 条）
每条必须写**为什么**，且**双向**判定（变得可达 或 从字典消失 都红）。

判据一的三条面自检按「**更上游的输入面在前**」排：字典几何条目 ≥60（实测 65）→
`textMissing` 为空 → 镜像数 ≥19。顺序是**负控 case 7 逼出来的**：正则写坏时三条都会命，
先命中的必须是最上游那条，否则下游断言永远见不到光（那就是死断言）。

### 72.5 🩸 两个坑

1. **`resources[key]` 索引器不查 `MergedDictionaries`**：`Icons.axaml` 靠
   `ResourceInclude` 合并进来，`TryGetResource` 查得到、索引器返回 `null`
   ⇒ 对 null 取成员直接 NRE。首跑 3 条判据一起红、栈都指向同一行。
   **规矩：只认 `TryGetResource` 的 `out` 值。**
2. 🩸 **`StreamGeometry.ToString()` 返回的是类型名**（`Avalonia.Media.StreamGeometry`），
   **不是**路径数据 ⇒ 首版那一维比较是**恒真的假绿**，是**读数**（19 行最后一列全一样）
   暴露出来的。Avalonia **没有**公开的「几何 → 路径文本」API
   （`PathGeometry.CreateFromGeometry` / `Geometry.GetFlattenedGeometry` 在 `Avalonia.Base`
   里都不存在）—— 这也正是阶段 71 那条只能用 **`Path.Data` 引用相等**的原因。
   修法：字典侧几何文本改从 `Icons.axaml` **源文件**抽（唯一一处偏离「只取运行期」，
   理由写在类注释与专题里），运行期维只到 `Bounds`、**不是内容相等**。

### 72.6 端到端负控 9/9（`.workbuddy/tmp/neg72.py`）

| # | 注入 | 期望命中 | 实测 |
|---|---|---|---|
| 1 | 工具箱 `Rectangle` 改回收缩版 | 判据一（`Bounds` 维） | 红 ✓（失败 1） |
| 2 | 字典补 `IconQuickTank` | 判据二差异表 stale | 红 ✓ |
| 3 | 字典删 `IconQuickButton` | 判据一面自检 | 红 ✓（失败 2） |
| 4 | 改 `IconDrawRectangle` 几何 | 判据一 | 红 ✓ |
| 5 | 字典补 `IconDrawButton` | 判据三（歧义） | 红 ✓ |
| 6 | ★ 工具箱 `Rectangle` 换成**同矩形不同起点/绕向** | 判据一（**文本维**） | 红 ✓ |
| 7 | 把判据自己的抽取正则写坏 | 判据一（最上游面自检） | 红 ✓ |
| 8 | 字典 `IconDrawSelect` → `IconDrawZzz` | 判据四 undocumented | 红 ✓ |
| 9 | 差异表塞**后缀已对上**枚举成员的键 | 判据四 stale | 红 ✓ |

**case 6 分量最重**：注入后两处 `Bounds` **都是 `4, 4, 16, 16`**（完全相同），只有写法不同 ——
首版（`ToString()` 那一维）会**放过**它；现在判红，失败消息里两行 `Bounds` 一字不差。
这才证明文本维是活的，不是又一个假绿。

判红谓词匹配测试**自己的汇总行**（`失败: N`，N>0）**且**取到 `错误消息:`；
还原**写回原文**（**禁 `shutil.copy2`** —— 它连 mtime 一起复制 ⇒ MSBuild 判定无需重建
⇒ 正控跑的是负控的二进制）；收尾后三个文件与备份**逐字节 `cmp` 一致**、注入痕迹 grep **0**。

### 72.7 诚实边界（4 条）

1. **字典侧几何文本取自源文件**，不是运行期对象（Avalonia 无「几何 → 文本」API）；
   运行期维只到 `Bounds`，**不是内容相等**。⇒ 若源文本被别处覆盖成不同值，
   那条覆盖由阶段 71 的定义侧判据兜，不由本判据兜。
2. **只覆盖「按命名约定对得上」的 19 个**。`ControlIcon` 的 `_ =>` 兜底返回一个矩形
   ⇒ 枚举新增成员却漏了 `switch` case 不会让工具箱自己报错，只由判据一/二间接兜住。
3. **判据四只保证「有人改动时会红」**，不保证 `IconDrawSelect` / `IconDrawText` 是活的
   （引用面由阶段 71 管）。
4. **像素级比对仍未做**（沿用阶段 71 边界 #4），但范围缩小：这 19 条**文本相同**
   ⇒ 同一个 `Parse` 路径 ⇒ 渲染必然相同；阶段 71 那 21 条 `Path` 仍只有引用相等 +
   `Stroke`/`Stretch` 非空。

### 72.8 门禁与提交

提交 **`046a398`**（`fix(toolbox)`，2 files，**+377/−5**）→ **`ccee01a`**（`docs(阶段72)`，3 files，**+408**）。

| 时机 | 日志（mtime） | 结果 |
|---|---|---|
| 提交前 →15:15:46 | `.workbuddy/tmp/ci-gate-72-pre.log`（**15:15:46**） | **7 步全绿 / `GATE_EXIT=0`**、0 警告 0 错误 |
| 提交后 15:16:15→15:17:22 | `.workbuddy/tmp/ci-gate-72-post.log`（**15:17:22**） | 同上，且与提交前**逐程序集逐条同数** |

大队 **3090 → 3094（+4）**（= 新增判据 4 条）；独占 **8 + 2** 不变 ⇒ 总 **3100 → 3104**。
逐程序集：Simulator 44 / Hardware 44 / Communication 75 / Device 87 / Render 4 / AI 315 /
Core 2116 / **App.UiTests 405 → 409**。
HEAD 提交时间 `ccee01a` = **15:16:15** ⇒ 提交后日志 mtime 晚于它，**时序成立**。
**产品代码只改了 4 条几何字符串**（对齐到字典，无新增/删除控件）。

「逐条同数」的取法：两次日志的行**必须先剥掉墙钟「持续时间: … - 」再比** —— 否则 diff
只会指出墙钟不同（首次对比就撞在这上面）。⚠ 本沙箱的 shell **不支持进程替换**
（`/dev/fd` 不存在，与 `ci-gate.sh` 第 1 步注释里那条是同一个限制）⇒ 对比要落临时文件，
别写 `<(...)`。

端到端负控 **9/9**；`src/` + `tests/` 注入痕迹 grep **0**
（全仓 13 条命中都在 `docs/`，是负控记录本身）；工作树提交后为空。

专题：`docs/2026-09-22-toolbox-icon-single-source.md`；登记表 **§29**。

## 阶段 73（把登记表 §5 #40 从「触发条件未定」推到定论：headless 下 `ScrollViewer`+`Wrap` 不收敛、**真机不挂**）— 2026-09-22

### 73.1 一句话结论

`ScriptEditorView.BtnHelp` 在 headless 下挂死（`EXIT=124`）的**触发条件已定论**：
**「`ScrollViewer` 的垂直方向处于可滚动模式」+「内容是 `TextWrapping.Wrap` 的多行 `TextBlock`」，
在首次布局（`Show()` 内）定不下来**。三条否证把范围钉死，且**真平台（X11/Skia）下不挂**
⇒ 是 Avalonia.Headless 的限制，**不是产品缺陷**，产品代码不为此改渲染方式。
专题 `docs/2026-09-22-headless-scrollviewer-wrap-hang.md`（11 节）。

### 73.2 判别实验：一次只改一个变量，**退出码即结论**

工具 `.workbuddy/tmp/e73.py`（每 case 从**备份原文**出发、只注入一处、`assert count == 1`）；
判据是外部 `timeout 120` 的退出码（**124 = 挂死**，0 = 收敛）。

| # | 注入（只改这一处） | 退出码 |
|---|---|---|
| E0 | 无（原产品代码） | **124**（4m1s） |
| E1 | `HorizontalScrollBarVisibility = Disabled` | **124** ← **否证登记表原假设** |
| 2 | 帮助文本换成 `"短文本"` | 0（682 ms） |
| 3 | `VerticalScrollBarVisibility = Disabled` | 0（660 ms） |
| 4 | `TextWrapping.Wrap → NoWrap` | 0（694 ms） |
| 5 | `LineHeight 20 → 20.0`（**等价改写＝负控**） | **124** ← 排除「改一行就收敛」 |
| 6 | `ScrollViewer` → `Border` | 0（674 ms） |
| 7 | `VerticalScrollBarVisibility = Hidden` | **124** |
| 8 | `Height 420 → 900` | **124** |

case 5 是这次设计的关键：它**不改变任何布局语义**却照样挂 ⇒ 上面那些「0」不是"改了文件"带来的。

### 73.3 两个前置读数（**我事先都估错了**）

挂死路上必须先量两个量，否则 case 8 无法解读（探针 `ZzProbe73GeoUiTests`，取证后已删）：

```
① Window{480×420} -> Bounds=0,0,480,420        ← headless 尊重 Window.Height
① Window{480×900} -> Bounds=0,0,480,900
② 窗 420: ScrollViewer(Disabled) Bounds=448×348  Viewport=448×348
           TextBlock Bounds=448×348  DesiredSize=273×340    Button Bounds=384,360,64,28
② 窗 900: ScrollViewer Bounds=448×828  TextBlock DesiredSize=273×820
```

🩸 **帮助文本在 headless 下根本不高**：源文本 29 行，但 `DesiredSize` 只有 **340px** ——
headless 的 fallback 字体把 CJK 也按极窄字形渲染（≈5px/字符），54 字符的最长行只要 **273px
（＜ 448 可用宽）** ⇒ `Wrap` 在 420 高的窗里**一次都没生效**。
⇒ **内容 340 ＜ 视口 348，压根不需要滚动** ⇒ case 8 的挂死自洽：
「内容溢出 ⇒ 出现滚动条」这条链在 E0 里**从未发生过**，自然不可能是触发条件。

### 73.4 定论与逐条排除

| 维度 | 结论 | 证据 |
|---|---|---|
| 垂直可滚动 | **必要条件**（唯一开关） | case 3（Disabled⇒0）vs E0（Auto⇒124） |
| `Wrap` | **必要条件** | case 4（NoWrap⇒0） |
| 多行内容 | **必要条件** | case 2（一行⇒0） |
| 是否溢出视口 | **无关** | case 8（视口 828 ≫ 内容 340，仍 124） |
| 滚动条是否可见 | **无关** | case 7（`Hidden` 仍 124） |
| 水平侧 | **无关** | E1（水平 `Disabled` 仍 124） |
| 挂在哪一步 | **`Show()` 内首次布局** | 最小复现落盘停在「即将 `w.Show()`」 |

**最小复现**（`Probe2`/`Probe3`，同一棵树 `Border(16) → ScrollViewer → TextBlock(Wrap)`，
两侧只差一个变量）：垂直 `Auto` ⇒ **124**（停在「即将 `w.Show()`」）；垂直 `Disabled` ⇒ **0**（走完）。
⇒ 复现面从「一包 ScrollViewer 就挂」压到「**垂直可滚动那一个开关**」，且与 `ShowDialog` / owner / 本视图无关。

**否证登记表原假设**：它猜的是「水平滚动默认 `Auto` ⇒ 无限宽度测量 ⇒ `Wrap` 换行宽度定不下来」，
E1 直接把水平设成 `Disabled`（宽度约束回到 448）**照样挂**。
它当时引的"反证"（`SettingsDialog` 14 处等 `ScrollViewer` 全没挂）也反着用了 —— 那些读数全在**同一个
headless 平台**上，证明的是「ScrollViewer + **非 Wrap** 内容不挂」，正好是 §73.4 `Wrap` 那条的必要性证据。

### 73.5 真平台对照：X11 / Skia 下**不挂**（登记表遗留①）

file-based app 探针（`.workbuddy/tmp/probe73-x11/probe.cs`，取证后已删）在 `xvfb-run` 里跑真实
X11 + Skia，两侧各 2 轮 ⇒ **4/4 `EXIT=0`**。🩸 两个坑（都不是本议题的）：

1. **`xvfb-run` 必须自己带 `-extension GLX`**。用 `--server-args` 覆盖默认参数就不带它，而本容器
   有 NVIDIA 用户态库却**没有** `/dev/nvidia*` ⇒ Xvfb 一开就 `核心已转储`（`EXIT=1`）。
   **只设 `LIBGL_ALWAYS_SOFTWARE=1` 不管用**（技能 `sandbox-gui-app-screenshot` 里有记）。
2. **首轮对照侧 `EXIT=134`（`free(): invalid pointer`）是环境噪声，不是"Disabled 会崩"**：
   系统 `libharfbuzz.so.0` 与 Avalonia 自带那份 `libHarfBuzzSharp.so` 符号介入冲突。
   按 `LD_PRELOAD=<harfbuzzsharp.nativeassets.linux 8.3.1.3>/runtimes/linux-x64/native/libHarfBuzzSharp.so`
   预加载**自带那份**之后 4/4 全绿。⚠ 没这步就会得出「Auto 不崩、Disabled 崩」的**假结论**
   ⇒ 「一次只放开一个变量」之外还得「**先把噪声排掉再下结论**」。

### 73.6 被否证的修法，与实际落地

**`Wrap → NoWrap` 不可行**（case 4 已证它收敛，最诱人）：

| 语言 | 总长 / 行数 | 最长行 | 估算宽（半角前进 7.15px） | 可用 448px |
|---|---|---|---|---|
| zh-CN | 575 / 29 | 54（48 ASCII + 6 CJK） | **421px** | 勉强（余量 27px） |
| **en-US** | 986 / 29 | **93（全 ASCII）** | **665px** | ✗ **超 217px** |

⇒ 改 `NoWrap` 会让**英文用户必须横向滚动**读帮助 ⇒ 产品退化。单看 zh-CN 或 headless 的 273px 都会误判。

**落地**：产品代码把「造窗」与「显示窗」拆开（**纯重构**，`ShowLuaHelp()` 行为不变）：

```csharp
internal static Window CreateLuaHelpDialog() { …构造 + 关闭按钮接线 + return helpDialog… }
private void ShowLuaHelp() => CreateLuaHelpDialog().ShowDialog(this);
```

新判据 `LuaHelpDialogUiTests`（**2 条**，走 `CreateLuaHelpDialog()`、**只构造不 Show** ⇒ 不碰会挂死那条路径）：
① `HelpDialog_TitleAndBodyComeFromTheDictionary`（标题/正文各自走对键 + 文案不能空 + 不能等于键名）；
② `HelpDialog_HasExactlyOneButtonLabelledClose`（**按文案找**、不按 `grid.Children[1]` 下标，恰好一个）。
遍历用**自写的 `Content`/`Child`/`Children` 递归** —— `GetVisualDescendants()` 要求已 attach（要 `Show()`，
正是会挂死那一步），`GetLogicalDescendants()` 又枚举不到 `Panel.Children`。

`DialogInnerInvocationUiTests` 的 `BtnHelp` **仍在 `Never` 桶**（点它就走 `ShowDialog`），
但理由从「触发条件未定」改成定论 + 三条否证 + 真机不挂 + 「内容已由另一条判据覆盖」。

### 73.7 端到端负控 5/5（`.workbuddy/tmp/neg73.py`）

| case | 注入 | 结果 |
|---|---|---|
| 1 | 标题串键（`ScriptHelpTitle` → `GenClose`） | 红 ✓（失败 1） |
| 2 | 正文硬编码，不再取字典 | 红 ✓（失败 1） |
| 3 | 关闭按钮文案换成别的键 | 红 ✓（失败 1） |
| 4 | 再加一个同文案的关闭按钮 | 红 ✓（失败 1，`Single` 抓到） |
| 5 | 砍断内容递归（`Window` 不再下钻） | 红 ✓（**失败 2** ⇒ 两条判据都依赖递归） |
| — | 还原后复跑 | **绿 ✓** |

判红谓词取测试**自己的汇总行**（`失败: N`，N>0）**且**要求出现 `错误消息:`；认不出归 `unknown`，
与 `build-fail` 一样**不算数**。还原**写回备份原文**（不用 `shutil.copy2`）。
噪声排除：case 4 首版注入 `CLOSE_BTN + CLOSE_BTN` 缺逗号 ⇒ 会变成 `build-fail(不算数)`，
改成在 ScrollViewer 之后**插入**一个新按钮（逗号天然合法）。

### 73.8 诚实边界（5 条）

1. ~~**判据只覆盖"内容"，不覆盖"行为"**：关闭接线是 `closeBtn.Click += (_, _) => helpDialog.Close()`，
   而「点了真的会关窗」只能把窗**真显示出来**才观察得到 —— 那正是会挂死的一步。
   ⇒ 产品按 `grid.Children[1]` 下标认按钮这个脆弱点**仍然是空的**（插控件+不改下标 ⇒ 接线静默失效，无判据会红）。~~
   ⇒ ✅ **已闭环（阶段 75），且这条边界本身被实测推翻**：「`Show()` 是观察接线的必要条件」是**未经实测的推断**。
   详见下面「阶段 75」。
2. `NoWrap` 的否证用的是**估算宽度**（Consolas 半角前进按 0.55em ≈ 7.15px），非真实渲染量；
   但方向很硬：en-US 最长行 93 个 ASCII 要压进 448px 需每字符 ≤4.8px，等宽字体不可能。
3. **真平台对照只到「不挂」**，没验滚动条行为/换行结果；且探针用的是**编造的多行文本**
   （686 字符 / 29 行，贴近形状），不是真文案。
4. **触发条件停在"哪些开关能翻"，没落到 Avalonia 源码级**：没定位 `ScrollViewer.MeasureOverride`
   与 `TextLayout` 之间究竟是哪一步不收敛。本阶段给的是可复现边界与否证，不是根因代码位置。
5. **headless 的字体度量不能外推真机**（测试文本在 headless 下全是 `.notdef` 字形）；
   §73.6 那张宽度表是按**真机字体**另算的。

### 73.9 门禁与提交

提交 **`11f45fc`**（`test(script-editor)`，4 files，**+148/−8**）。

| 时机 | 日志（mtime） | 结果 |
|---|---|---|
| 提交前 17:09:32 | `.workbuddy/tmp/ci-gate-73-pre.log`（**17:09:32**） | **7 步全绿 / `GATE_EXIT=0`**、0 警告 0 错误 |
| 提交后 17:09:49→17:10:51 | `.workbuddy/tmp/ci-gate-73-post.log`（**17:10:51**） | 同上，且与提交前**逐程序集逐条同数** |

大队 **3094 → 3096（+2）**（= 新增判据 2 条）；独占 **8 + 2** 不变 ⇒ 总 **3104 → 3106**。
逐程序集：Simulator 44 / Hardware 44 / Communication 75 / Device 87 / Render 4 / AI 315 /
Core 2116 / **App.UiTests 409 → 411**。
HEAD 提交时间 `11f45fc` = **17:09:49** ⇒ 提交后日志 mtime 晚于它，**时序成立**。

🩸 **首跑门禁判红一条**：`LangConstantUsageGuardTests.UnusedLangConstants_MatchBaseline`
（`失败: 1`，411 条里 410 通过）。原因是新判据读了 `Lang.ScriptLuaHelpText`，而产品代码那一行
用的是**字面量** `"script.lua.help.text"` ⇒ 该常量从"没人读"变成"有人读" ⇒ 计数 1125 → 1124，
**基线必须同步**（守卫的失败消息自己就写着「降了 = 清理了死常量，请把 `UnusedBaseline` 改成 1124」）。
这正是该基线「**精确相等判定 ⇒ 反向也红**」该有的反应，不是误报。
处置：产品改用常量 + 基线 **1125 → 1124** + 注释记下分桶 **M6 274 → 273**
（M2 828 / M3 18 / 无机制 5 都不动）。改后先单跑四条本地化守卫 **17/17 绿**，再重跑门禁。

「逐条同数」的取法：两次日志的行**必须先剥掉墙钟「持续时间: … - 」再 `sort`** —— 否则 diff
只会指出墙钟不同。⚠ 本沙箱的 shell **不支持进程替换**（`/dev/fd` 不存在）⇒ 对比要落临时文件
（`.workbuddy/tmp/cmp73-{a,b}.txt`），别写 `<(...)`。

端到端负控 **5/5**；`src/` + `tests/` 注入痕迹 grep **0**；工作树提交后只剩本轮的 `docs/`。

专题：`docs/2026-09-22-headless-scrollviewer-wrap-hang.md`（11 节）；登记表 **§5 #40**（改写）+ **§30**（实施记录）。

## 阶段 74（闭环登记表 §5 **#32** —— 顺带**更正定性**：那个「删不掉」的文件不是永久残留）— 2026-09-22

### 74.1 起点与选点

惯例不变：先扫登记表「仍开着」两节（§4 软件侧可验证 / §5 环境·本地残留），挑**唯一有实体残留**的项。
扫描脚本 `.workbuddy/tmp/scan74.py`（只读）。§4 里的项要么已闭环、要么只有断言没有实体；
§5 里 **#32** 是唯一一处「仓库里真的躺着一个本该删掉的东西」——原始处置写着
「**下次在沙箱外删掉文件本身 + `.gitignore` 里那条注释与规则**」。

### 74.2 事实侦察（先验真，不预设）

| 读什么 | 读数 |
|---|---|
| 文件在不在 | **在**：`tests/HmiEditor.App.UiTests/_ZZMenuInvocationProbe.cs`，**1406 字节** |
| 为什么 `git status` 不报它 | `git check-ignore -v` ⇒ **`.gitignore:98`**；第 95~98 行 = **3 行说明注释 + 1 行精确规则** |
| 文件是什么 | `internal static class MenuInvocationProbeNotes`，**纯注释、无 `[Fact]`**，自带一句「下次在沙箱外直接删掉即可」 |
| 全仓引用 | 仅 **3 处文档**提到它，`src/` + `tests/` **零代码引用** |

### 74.3 🩸 关键更正：把「删不掉」的**生命周期**量出来

原 #32 把「删不掉」写成一条**环境限制**（沙箱把会话内新出现的文件路径做成逐文件 bind-mount
⇒ `rm`/`os.rename` 报 `EBUSY`、`umount` 反说「未挂载」）。本轮**直接实测**：

- `grep -c` `/proc/mounts` 里那条 bind-mount 条目 ⇒ **0**（条目已不存在）；
- `os.remove(文件)` ⇒ **直接成功**，文件随即消失，工作树 `git status` 无异常。

⇒ **更正后的定性**：那条绑定是**产生它的那个会话内**的产物，**跨会话自动消失**，
**不是永久残留、不需要人工介入**。「沙箱把路径做成 bind-mount」这个机制描述**没错**，
但**它的作用域被原登记写宽了**：从「沙箱的固有行为」纠成「沙箱在单个会话内的临时行为」。
当时「无 `[Fact]` 说明性文件 + `.gitignore` 精确规则」的**降级处置依然是有效过渡**
（保证那个会话内既不影响测试发现、也不污染 `git status`），只是**不必**再当长期待办。

### 74.4 处置

1. 删文件本身（`os.remove`，已成功）。
2. 删 `.gitignore` 第 95~98 行（3 行注释 + 1 行规则）—— 规则本来就挡住了它，**删了才看得出干净**。
3. 回填 3 处文档，并把 #32 从「待人工清」改成 **✅ 已闭环 + 定性更正**：
   `docs/2026-09-20-menu-invocation-net.md` §9、登记表 §5 #32（+ 新增 **§31**）、本文件阶段 49 的「沙箱残留」小节。

### 74.5 验证

| 项 | 结果 |
|---|---|
| 构建 | **0 警告 0 错误** |
| 测试计数 | 与阶段 73 收尾 **逐程序集逐条同数**（大队 **3096** / 独占 **8 + 2** / 总 **3106** / `App.UiTests` **411**）⇒ 删除**行为中性**（原文件无 `[Fact]`） |
| 代码影响面 | **0 行产品代码、0 条判据** —— 纯清理 + 一次定性更正 |
| 门禁 | 见 74.6 |

### 74.6 门禁与提交

实质变更提交 **`07c0642`**（`chore(gitignore)`）。

| 时机 | 日志（mtime） | 结果 |
|---|---|---|
| 提交前 17:26:01→17:27:10 | `.workbuddy/tmp/ci-gate-74-pre.log`（**17:27:10**） | **7 步全绿 / `GATE_EXIT=0`**、0 警告 0 错误 |
| 提交后 17:27:24→17:28:21 | `.workbuddy/tmp/ci-gate-74-post.log`（**17:28:21**） | 同上，且与提交前**逐程序集逐条同数** |

逐程序集：Simulator 44 / Hardware 44 / Communication 75 / Device 87 / Render 4 / AI 315 /
Core 2116 / **App.UiTests 411**（与阶段 73 收尾完全一致）。
HEAD 提交时间 `07c0642` = **17:27:24** ⇒ 提交后日志 mtime **17:28:21** 晚于它（**57 s**），**时序成立**。

「逐条同数」的取法（比阶段 73 多剥一层）：日志里**有两类墙钟**，只剥「持续时间:」不够 ——
固件 host 测试还会打 `已用时间 00:00:0x` 与 `[hmi] AUDIT 2026-09-22T17:27:09 …` 的**逐行时间戳**。
完整式：`grep -v "持续时间:" | grep -v "^已用时间" | sed -E 's/[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}/TS/g' | sort`
⇒ 两侧各 **824 行**、**diff 0**。

⚠ 本轮 `echo "GATE_EXIT=$?"` 在 fish 里**取不到退出码**（`$? 不是退出代码，请使用 $status`），
而这个 `$?` 又包在 `bash -c` 之外 ⇒ 退出码那行拿不到真实值。判绿改用**日志末行**
「🎉 门禁全部通过（7 步）」+ 两次日志的 `diff 0` 双重确认（比单看退出码更硬）。

登记表 **§31**（实施记录）；专题回填见 `docs/2026-09-20-menu-invocation-net.md` §9。

## 阶段 75（**推翻**阶段 73 的边界①：headless 里「不 Show 也能判接线」，顺带摘掉 `Children[1]` 那个位置下标）— 2026-09-22

### 75.1 起点：拿**自己上一轮写下的**断言当待验命题

阶段 73 的 73.8 边界 1 写的是：「关闭接线要"真显示窗"才观察得到 —— 那正是会挂死的一步
⇒ 产品按 `grid.Children[1]` 下标认按钮这个脆弱点**仍然是空的**」。

这句话里藏了一个**从未实测过的因果**：「观察接线 ⇒ 必须真的把窗显示出来」。
阶段 73 全篇都在讲「先量事实再下结论」，而这条恰恰是**推断**（那条路径会挂死 ⇒ 于是"观察不到"）。本轮先量它。

### 75.2 三组探针读数（一次就够）

探针 `ZzCloseProbeUiTests`（**取证后已删**，不进仓库）：

| 探针 | 问的是 | 读数 |
|---|---|---|
| P1 | 从未 `Show()` 过的 `Window.Close()` 会怎样？ | **不抛**；`Closed` **会触发**（`closed=1`）；`IsVisible=False` |
| P2 | 未 attach 到可视树的按钮上 `RaiseEvent(Button.ClickEvent)`？ | **会**触发直接挂上去的 handler（`hits=1`） |
| P3a | **内置负控**：产品那个窗**只构造、不点** | `closed=0` ✓（否则下面的计数恒真） |
| P3b | 点产品那个**写着"关闭"的按钮** | `closed=1` ✓ |

⇒ **`Show()` 不是观察接线的必要条件**。且 `Closed` **只有** `Close()` 会触发
⇒ 「那条 handler 到底有没有调 `Close()`」有**精确等价物**，不是近似代理。

### 75.3 产品侧的真问题：那个下标是**静默**失效面

```csharp
if (helpDialog.Content is Border border && border.Child is Grid grid &&
    grid.Children[1] is Button closeBtn)     // ← 位置下标
{
    closeBtn.Click += (_, _) => helpDialog.Close();
}
```

三种形态都会坏，**前两种不抛不报**：

| 注入（只改一处） | 旧代码的行为 |
|---|---|
| 往那个 Grid **最前面**插一个控件 | `Children[1]` 变成 `ScrollViewer` ⇒ `is Button` 失配 ⇒ **整段 if 跳过** ⇒ 关闭按钮点不动，**无异常无警告** |
| 在 `ScrollViewer` 与关闭按钮**之间**插一个 `Button` | `Children[1]` 变成那个新按钮 ⇒ `Close()` 接到**错的按钮**上，真关闭按钮**点不动** |
| 把 `Children[1]` 写成 `Children[0]` | 同第一条形态 |

处置 = **不再按位置找**：按钮先建成局部变量、留住引用，接线时直接用那个实例；顺带去掉
`Content is Border` / `Child is Grid` 两层运行期类型导航 —— 将来把 `Border` 换成别的容器也不会再静默断线。

### 75.4 判据（第 3 条）与负控 / 正控 / 配对实验

`LuaHelpDialogUiTests.HelpDialog_CloseButtonIsActuallyWiredToClose`：构造（**不 Show**）→ 内置负控
`closed == 0` → 按**文案**找到那个按钮（守卫**不与被守对象同源**）→ `RaiseEvent(Click)` → `Assert.Equal(1, closed)`。

**负控 4/4 + 正控 1**（每处注入只改一处、`assert count == 1`、还原写回原文）：

| 用例 | 注入 | 期望 | 实测 |
|---|---|---|---|
| NC1 | 摘掉 `closeBtn.Click += …` | RED | **RED**（红在 `LuaHelpDialogUiTests.cs:152`，现场 `Expected: 1 Actual: 0`） |
| NC2 | `grid.Children[1]` → `[0]` | RED | **RED** |
| NC3a | `Children` 最前面插入一个 `Button` | RED | **RED** |
| NC3b | 在 `closeBtn` 之前插入一个 `Button`（`Close` 接到错按钮） | RED | **RED** |
| **PC1** | 定位改写 `Children[^1]`（**仍是同一个按钮**） | **GREEN** | **GREEN** ⇒ 上面 4 条红不是"改了文件就红"的伪影 |

**配对实验**（同一注入、只差新旧代码）—— 这才是「修法真的消除了那一维」的证据：

| 注入 | 旧代码 | 重构后 |
|---|---|---|
| `Children` 最前面插一个 `Button` | **RED** | **GREEN** |
| 在 `closeBtn` 之前插一个 `Button` | **RED** | **GREEN** |
| 摘掉接线 | **RED**（NC1） | **RED**（NCF）⇐ 判据没有因此变恒绿 |

🩸 **负控谓词本轮连踩三坑**（都写下来，下一个人可以直接绕开）：
① `-v q` 下**没有** `错误消息:` ⇒ 按纪律只能算 `unknown`（不算数）⇒ 改用 `--logger "console;verbosity=detailed"`；
② 切到 detailed 后**汇总行变了**（`测试总数:` / `通过数:` / `失败数:`，0 失败时**不打印**失败数行）
⇒ 原先匹配 `失败: N` 的正则全部落空，4 条全成 `unknown(无汇总行)`；
③ xunit 把 `Actual` **右对齐**成 `Actual:   0`（多空格）⇒ 谓词写成 `"Actual: 0" in txt` 会**自己造出假 unknown**。
三处修正后才 4/4 真判红（`失败数=1` + `错误消息:` + `Expected:\s*1` / `Actual:\s*0` + 堆栈点名 `cs:line 152`）。

### 75.5 验证

| 项 | 结果 |
|---|---|
| 构建 | **0 警告 0 错误** |
| `LuaHelpDialogUiTests` | **3/3 通过**（387 ms，无挂死）；**未重构代码上先跑绿、重构后再跑绿**（两代都绿 ⇒ 判据不依赖重构） |
| 负控 / 正控 / 配对 | 见 75.4 |
| 还原 | 两个脚本跑完 `sha256` **逐字节一致**（旧代码 `939454362fee` / 重构后 `6a725a980711`） |
| 门禁 | 见 75.6 |

### 75.6 门禁与提交

实质变更提交 **`07c487d`**（`fix(script-editor)`，3 files：产品 1 + 测试 2）。

| 时机 | 日志（mtime） | 结果 |
|---|---|---|
| 提交前（1m8s） | `.workbuddy/tmp/ci-gate-75-pre.log`（**18:04:31**） | **7 步全绿 / `GATE_EXIT=0`**、0 警告 0 错误 |
| 提交后（1m0s） | `.workbuddy/tmp/ci-gate-75-post.log`（**18:05:44**） | 同上，且与提交前**逐程序集逐条同数** |

逐程序集：Simulator 44 / Hardware 44 / Communication 75 / Device 87 / Render 4 / AI 315 /
Core 2116 / **`App.UiTests` 411 → 412**（+1 = 新增那条判据）。
大队 **3096 → 3097**、独占 **8 + 2** 不变 ⇒ 总 **3106 → 3107**。
HEAD 提交时间 `07c487d` = **18:04:40** ⇒ 提交后日志 mtime（18:05:44）晚于它，**时序成立**。

🩸 **「逐条同数」又多一类要剥的东西：NuGet 还原输出。** 首比差 **28 行**，逐类查完全是**构建态**噪声：

| 类 | 形态 | 为什么不算差异 |
|---|---|---|
| ① 还原明细 ×14 | `已还原 …csproj (用时 N 毫秒)` | 提交前刚跑过 `dotnet build` ⇒ 两边的还原态本就不同 |
| ② 还原汇总 ×1 | 「7 个项目(共 21 个)是最新的，无法还原。」vs「所有项目均是最新的，无法还原。」 | 同一件事的**两种措辞** |
| ③ 换行 ×1 | `总共 1 个测试文件与指定模式相匹配。` pre 是 3 行、post 挤成 1 行 | 日志换行位置不同，行数不是内容 |

⇒ 完整式（在原有三类墙上再加第四类）：
`sed -E 's/<ISO>/TS/g' | grep -v "持续时间:" | grep -v "^已用时间" | grep -vE "已还原|无法还原" | sed 's/总共 1 个测试文件与指定模式相匹配。/\n&/g' | sed 's/[[:space:]]*$//' | grep -v '^$' | sort`
⇒ 两侧各 **784 行**、**diff 0**。
**剥的每一类都在上表里写明了理由**，不是"剥到 diff 空为止"。

### 75.7 仍未闭环（诚实边界）

① 判据证明的是「接线通到 `Close()`」，**不是**「真显示出来后窗确实消失了」——后者要走 `ShowDialog`，即挂死那一步。
② **测不出「重复接线」**：`Close()` 在已关闭的窗上幂等 ⇒ 接两遍也只关一次、`Closed` 只响一次
（#43 那一族之所以可见，是因为「重复会开 4 个窗」；对这个窗，重复接线的用户可见后果本来就是零）。
③ 判据是**行为**的、不是源码 grep —— 这是刻意的：本轮给产品加的那段注释里**就写着** `grid.Children[1]`，
任何「源码里不许出现这个子串」式判据都会被它自己满足（#48 的教训）。
④ 这次只覆盖了**这一个**手搓对话框。「代码构造的控件树 + 按下标/按运行期类型找控件」这类写法
**没有全仓扫描过** —— 若还有同类，得先有一条能看见它们的判据。


## 阶段 76（把阶段 75 的**边界④**收口：全仓清点「按下标找控件」+ 落地一条**两级判据**，顺带摘掉 `GitDialogService` 的 3 处同型接线）— 2026-09-22

### 76.1 起点：上一轮**自己写下**的判据缺口

阶段 75 的 75.7 边界④ 原文：「这次只覆盖了**这一个**手搓对话框。『代码构造的控件树 + 按下标/按运行期
类型找控件』这类写法**没有全仓扫描过** —— 若还有同类，得先有一条能看见它们的判据。」

这句话**自带验收标准**：做完之后必须有**一条能看见这类写法**的判据，而不只是"又修了一个"。

### 76.2 全仓清点：首版**漏了一整类**

| 版本 | 扫描口径 | 命中 |
|---|---|---|
| v1 | 只认 `Children[<整数>]` | 8 处 |
| v2 | ＋ `Children.First()/Last()/ElementAt()`（F2）＋ **经局部别名的下标**（F3） | **10 处** |

🩸 v1 漏的是**经局部别名**的下标 —— `ShowCommitDialog` 里下标落在
`var buttons = panel.Children.OfType<StackPanel>().First().Children;` 那个**别名**上，不在 `Children` 后面。

🩸 **F2 的正则第一版连它自己的主要形态都没命中**：模式写成 `OfType<[^>]*>` 后直接接 `.First(`，
而真实文本是 `OfType<StackPanel>()` ＋ `.First()` —— **少了那对空实参括号** ⇒ 那条别名声明行
**一次也没被 F2 捕获**。发现方式是**逐字读真实行**，不是靠自检（自检夹具是我自己写的，
我写夹具时同样想不到漏括号）。**判据"看着对"与"命中"是两件事。**

### 76.3 分类与修法

分类键＝命中所在**语句**是否还做 `+=`/`-=` 或硬转 `(T)`：

| 类 | 站点 | 数 | 用户可见后果 |
|---|---|---|---|
| **wire 接线** | `Services/GitDialogService.cs` 的 `:92`/`:93`/`:168` | **3** | **功能静默断线** |
| **read 读取** | `AlarmConfigView:544`、`AnimationEditorView:466/469/472/476/480`、`RecipeEditorView:831` | **7** | 该行文本**静默不刷新** |

接线类**全在 `GitDialogService`**，且 `ShowCommitDialog` 比阶段 75 那处**更重**（多绕一层
`OfType<StackPanel>().First()`）：往外层面板插一个 `StackPanel` ⇒ 「取消/提交」**悄悄互换**；
`Content` 换成别的容器 ⇒ 整段 `if` **静默跳过**（两个按钮都点不动）。

**修法**：两处都把按钮建成**局部变量**、接线用实例，并去掉 `dialog.Content is StackPanel …` 那层导航；
顺带删掉修完后变成死的 `using System.Linq;`（`OfType/First` 是该文件唯一 Linq 用法）。
**只改构造方式**：控件树形状、按钮顺序、文案、点击行为**逐字不变**。

### 76.4 判据 `scripts/check-index-based-control-lookup.py`（两级 + 双向）

| 设计点 | 做法 |
|---|---|
| **两级** | **wire 硬判为 0**（`WIRE_ALLOWED` 刻意留空 —— 要豁免必须先改脚本，那一步强制写理由）；read 走**基线表** |
| **双向** | 表外新增 ⇒ 红；**表内条目在代码里消失 ⇒ 也红**（「接线后必须删差异表行」的同一条纪律） |
| **fail-closed** | 双向判定**本身就是**正则失效的兜底（基线 7 条非空 ⇒ 正则一坏，表内集体「消失」直接红）；另加 `MIN_SCANNED_FILES = 300`（实测 **379**） |
| **剥注释** | 复用 dock-bus 守卫同源的 `strip_noise`（注释 ＋ 字面量 ⇒ 等长空白）—— 本脚本 docstring 与产品注释里**都写着** `Children[2]`（#48 的教训） |
| **别名** | 声明式 `var X = …Children…` **与两步赋值** `X = …Children…` **都认**（后者是同一个洞的另一种写法，本轮发现后**当场堵掉**，**没有**写进「诚实边界」） |
| **范围** | **刻意收窄**：只判 `.Children` 上的**位置**定位。`Items.OfType<ListBoxItem>()`（`ListBoxItem` 是该集合**固有**的容器类型，不是猜）、`Content is X`/`.Child is X` 容器导航、`Children.Count/Add`、对象初始化器都**不判** |

### 76.5 验证

| 项 | 结果 |
|---|---|
| 构建 | **0 警告 0 错误** |
| 判据自检 | **11/11**（5 档该红：3 档 wire 形态 ＋ 双向两方向；6 档该绿） |
| 守卫（真实仓库） | **绿**：扫 **379** 个 `.cs`、wire **0** / read **7**（已登记） |
| 负控 / 正控 | **4/4 ＋ 还原复绿**，逐条 `sha256` 逐字节一致 |
| 门禁 | 见 76.6 |

**负控 / 正控**（注入全在**真实文件**上；还原**写回原文**，不用 `shutil.copy2` —— 那条路会把 mtime 一起复制走）：

| 档 | 注入 | 期望 | 实测 |
|---|---|---|---|
| NC1 | `((Button)p.Children[2]).Click += …`（F1 wire） | RED | **RED**，点名 `AlarmListView.axaml.cs:286  p.Children[2]` |
| NC2 | `var row = p.Children.OfType<StackPanel>().First().Children;` ＋ `((Button)row[1]).Click += …`（F2/F3 wire） | RED | **RED**，点名 `row[1]` ⇒ **别名追认真的在工作** |
| NC3 | 把**已登记**的 read 下标挪一位（`sp.Children[1]` → `[2]`） | RED | **RED**：同时报 `[read/新增]` **与** `[read/失效]` ⇒ **双向都真的在工作** |
| **PC1** | 合法写法 `var b = new Button(); b.Click += …` | GREEN | **GREEN** ⇒ 上面三条红不是「改了文件就红」的伪影 |
| 还原后 | 复跑 | GREEN | **GREEN**，两文件 `sha256` 逐字节一致，`git status -- src/` 只剩本轮真实改动 |

### 76.6 门禁与提交

| 时机 | 日志（mtime） | 结果 |
|---|---|---|
| 提交前 | `.workbuddy/tmp/ci-gate-76-pre.log`（**19:23:29**） | **7 步全绿 / `GATE_EXIT=0`**、0 警告 0 错误 |
| 提交后 | `.workbuddy/tmp/ci-gate-76-post.log`（**19:24:52**） | ⚠ **`App.UiTests` 411/412**，红 1 条 = **#47 已知偶发红**（见下） |

实质变更提交 **`69157fb`**（`fix(guards)`，3 files：产品 1 ＋ 脚本 2 —— 新守卫 ＋ 门禁接 `[1f]`）。
`69157fb` 提交时间 **19:24:00** ⇒ 提交前日志（19:23:29）**早于**它，**时序成立**。

逐程序集（提交前）：Simulator 44 / Hardware 44 / Communication 75 / Device 87 / Render 4 / AI 315 /
Core 2116 / **`App.UiTests` 412** ⇒ 大队 **3097**、独占 **8 + 2** ⇒ 总 **3107**。
**与阶段 75 收尾逐程序集逐条同数**（本轮 **0 条新测试** —— 新判据是 Python 脚本，不进测试计数）。

**跨阶段逐条同数**：把 `ci-gate-75-post.log` 与 `ci-gate-76-pre.log` 按 75.6 那条剥噪声公式处理后，
两侧 **784 行逐条相同**，只多 **2 行** —— ① 本轮新增的 `[1f]` 那行输出；
② 本轮日志收尾多写的 `GATE_EXIT=0` ⇒ **行为中性**。

🩸 **提交后那次红了 1 条 —— 是 #47 的又一次命中，不是本阶段的回归**：

① **同一份内容、同一条命令**：提交不改字节，19:23:29 那次大队 **3097 全绿**、19:24:52 那次 **411/412** ⇒ **非确定性**；
② 失败用例是 `DispatcherOwnershipContractTests.CrossDispatch_OnlyTheSessionThreadMayHoldTheUIThread`，
错误消息**逐字给出偷手现场**：`派发之外有人持有 Dispatcher.UIThread：ownerThread=35，sessionThread=25 …
（非会话线程＝偷手）` ⇒ 与 **#47b 的机制描述吻合**；
③ **与阶段 76 无共同面**：本轮只动 `GitDialogService` 的对话框构造 ＋ 两个脚本，headless 派发面**零改动**；
④ 复跑读数：该用例单跑 **5/5 绿**（3 项 / ~310 ms）、整个 `App.UiTests` 程序集 **3/3 绿**（**414 项** = 大队 412 ＋ 独占 2）。
⚠ **按纪律，这 4 次绿不构成「已好转」的证据**（#47 单跑本来就恒绿）⇒ 复现率更新为 **2/23 量级**，
已回填**登记表 #47 行**（那是权威处）。

### 76.7 仍未闭环（诚实边界）

① **read 类 7 处只登记、未消除** —— 结构一变就**静默停止刷新**。消除途径＝三份视图的列表项改成
`ItemTemplate` ＋ 绑定（实测这三份视图 **`ItemTemplate` 命中 0**，列表项都是 `CreateXItem()` ＋ `Items.Add()`
**手工构造**）。届时基线那 7 行**必须逐条删除**（双向判定会强制这件事发生）。
② **判据是源码形状、不是行为**：它保证「以后不再用位置找控件」，**不保证**「已改成持引用的那两处
在真机上真能关窗」（那仍要走 `ShowDialog`，即阶段 73 的挂死路径）。
③ **两维没有判据**（刻意）：`Content is X`/`.Child is X` **容器类型导航**、以及「**按运行期类型读取**」
（`is`/`as`/`OfType<T>`）。阶段 75 修的那处缺陷有**两个必要条件**（`b.Child is Grid g` **与** `g.Children[1]`），
本判据覆盖的是**后者**那一维 ⇒ 抓得到那处缺陷，但**不等于**覆盖了整个「容器类型导航」族。
④ **别名追认只到单层**：二次转手（`xs = a.Children; ys = xs; ys[0]`）不追 —— 真实仓库里不存在这种写法，
但它是**已知的绕过路径**，且**这一维没有负控** ⇒ 不声称被覆盖。

登记表 **§33** 是本节的长版（含全部读数与每条基线的理由）。

## 阶段 77（闭环 #57：三份视图的列表行改成「构造时捕获控件」，判据基线**清空** + 新增**行为**判据层）— 2026-09-22

### 77.1 起点：上一阶段自己立的台账

阶段 76（本文件上一节 / 登记表 §33.7 ① 与 §33.8）把「按下标找控件」全仓清成
**接线 0 处 + 读取 7 处**，并明确写下：读取类**只被登记、没有消除**，「结构变化时它们会
**静默停止刷新**」；消除途径＝列表项改成 `ItemTemplate` + 绑定，「属独立改造」；
「届时基线那 7 行**必须逐条删除**（双向判定会强制这件事发生）」。本阶段做这件事。

### 77.2 先复核真实形状（读，不预设）

7 处是**同一个形状**：在 `ListBox.Items` 里按 `Tag == 模型` 找到那一行，再**按位置**
钻进它的视觉树取某个子控件，改它的文本/颜色。只见于三个构造点、三个视图：

| 视图 | 行 | 构造点 | 那处读点在做的事 |
|---|---|---|---|
| `AlarmConfigView` | 组项 | `CreateGroupListItem` | 改任一字段后刷新组项的「报警计数」文本 |
| `AnimationEditorView` | 动画项 | `CreateAnimationItem` | `SyncAnimationFromUi` 刷新名称 / `[触发]` / 属性名 + 状态圆点 |
| `RecipeEditorView` | 参数项 | `CreateParamItem` | `SyncParamEdit` 刷新参数名标签 |

`AnimationEditorView` 那处最重：要按 `stack.Children[0]` → `topRow.Children[1|2]` 两层下标、
外加三个 `Count >=` 守卫，才拿到四个控件（含一个 `Border`）。

### 77.3 修法：**不是** ItemTemplate，而是「行类型在构造时捕获控件」

取舍：`ItemTemplate` + 绑定要动 3 份 `.axaml`、引入 INPC 项模型、并处理选中态与重建，
**行为面会变**（项被重建 ⇒ 选中态/滚动位置可能跳）。而要消灭的只是**位置依赖**，
不是「就地更新」这个做法本身 ⇒ 取最小且结构化的改法：每个视图加一个私有 `ListBoxItem`
子类（`AlarmGroupRow` / `AnimationRow` / `RecipeParamRow`），行内控件**在构造时**捕获成只读属性，
构造函数里同时设 `Tag`。

**关键取舍是「`Tag` 语义不变」**：三份视图里共有 **20 余处** `Tag is X` / `Tag == model`
读点（选择判定、增删后重选、`OfType<ListBoxItem>` 遍历）。把 `Tag` 换成「行载体」会连带改这 20 余处；
子类化让 `Tag` 与 `Content` 一字不动，改的只有那 7 处 ⇒ 净差异 3 文件 **+90 / −48**。

刷新点于是从「两层下标 + 三个守卫」变成 `Items.OfType<AnimationRow>()` +
`if (row.Animation != _currentAnimation) continue;` + 直接写属性。

### 77.4 判据侧：基线**清空**（两级都硬判 0）—— 并把丢掉的 fail-closed **换承担者**

删掉基线 7 行之后读数：**接线 0 处、读取 0 处**（扫 **379** 个 `.cs`）。
但这里有一个**本阶段最值得抄走的一步**：

> 此前「基线表非空」**顺便是正则失效的探测器** —— 正则一坏，表内 7 条集体「消失」⇒ 立刻判红。
> 表清空之后，正则坏掉就是 **0 命中 ⇒ 绿**，判据退化成**永远通过**的假门禁。

⇒ 新增 `meta_positive_control()`：**每次运行**都在最小临时树上验证三档已知坏形态
（F1 下标 / F2 `First()` / F3 别名下标）**真的能被命中**，少一档直接判失败。
它比原来那条**更强**：原来只护 read 类，这条**两类都护**，且不再依赖「表非空」。
`--selftest` 同步 11 → **12 档**（新增档＝元判据自身的负控：把 F1 换成永不命中的正则 ⇒ 必须报红）。
`scripts/ci-gate.sh` 的 `[1f]` 注释同步；**banner 步数不变**（仍 **7** 步，`[1f]` 折在已有步骤里）。

### 77.5 行为判据层（新增 `ListRowInPlaceRefreshUiTests`，3 条）

**理由**：形状判据**不保证**「改成持引用的那些行真的还刷新」，而这一步踩坏的方式**恰好是静默的**
（行类型没装进 `Items` / 捕获的控件不是显示那个字段的控件 / 刷新循环用错了匹配条件 ——
三种都不抛、不报错，只是那一行停在旧值）。

| 用例 | 断言 |
|---|---|
| `AnimationRow_TextsFollowTheEditedField` | 改动画名 ⇒ 那一行文本**换掉**（含新值且不再含旧值），模型与输入框同步变 |
| `RecipeParamRow_LabelFollowsTheEditedField` | 改参数名 ⇒ 参数行标签跟着换（三级联动 分组→配方→参数） |
| `FrameworkFact_ProgrammaticTextIsSilent_TypingIsNot` | 钉住两条框架事实（见下） |

断言的取数方式**刻意与位置无关**：从行里取文本走
`Content.GetVisualDescendants().OfType<TextBlock>()`（按类型收、不看顺序）。
再收紧一格：**恰好一个** TextBlock 变了（`Before.Zip(After).Count(≠)` == 1）—— 只断言「含新值」的话，
「把 `NameText` 与 `TriggerText` 接反」这种注入**测不出来**。

**两条框架事实（都会影响以后每一个 UI 判据，实测取得）：**

1. 🩸 **程序化设 `TextBox.Text` 不触发 `TextChanged`**（Avalonia 12.1.0；实测裸 `TextBox`
   **未接树 / 已 `Show`** 各一组，均 **0 次**）⇒ 用它驱动「编辑 → 自动保存」那条链，整链**根本不会跑**，
   判据会以「红」给出一个**与产品无关的假结论**（本阶段第一版就是这么红的：两条用例都停在「模型没变」）。
   正解＝**真实输入**：`Show` → `Focus` → `UiTestHost.Pump` → `KeyTextInput`（仓库已有先例，
   `HeadlessInputAndDialogUiTests` / `PropertyPanelUiTests`）。顺带说明：产品里那些
   `TxtX.Text = model.X` 装载代码**本身不会**引发回写，`_isUpdating` 那类守卫真正挡的是
   `ComboBox.SelectedIndex` / `CheckBox.IsChecked` 这些**会**程序化派发的事件。
2. **`KeyTextInput` 在光标处插入、不是追加**（实测：已有 `程序化` 时输入得到 `真实输入程序化`）
   ⇒ 要断言确定文本就先清空（清空用 1 里的程序化赋值，正因为它静默）。

**另一条判据自身的坑**：`SelectedIndex = 0` 在**已经预选中第 0 行**时**不产生变更** ⇒
`SelectionChanged` 不派发 ⇒ `_selectedParam` 一直是 `null` ⇒ 编辑静默无效。正解＝先置 `-1` 再置 `0`。

### 77.6 负控 / 正控（4 档 + 前后正控）

全在**真实文件**上注入，还原用**写回原文**（不用 `shutil.copy2` —— 那条路会把 mtime 一起复制走
⇒ MSBuild 不重建 ⇒ 正控跑的是负控的二进制）。

| 档 | 注入 | 判据侧（形状） | 行为侧 |
|---|---|---|---|
| NC1 | `((Button)p.Children[2]).Click += …` | **RED**，点名 `AlarmConfigView.axaml.cs:125` | — |
| NC2 | `sp.Children[1] is TextBlock t`（read，**基线已空**） | **RED**，`[read/新增] …sp.Children[1] ×1` | — |
| NC3 | 摘掉 `row.NameText.Text = …` 那一行 | **GREEN** | **RED**（失败 1） |
| NC4 | `new AnimationRow(…, triggerText, nameText, …)` 接错控件 | **GREEN** | **RED**（失败 1） |
| 正控 | 注入前 / 全部还原后各跑一次 | GREEN | GREEN |

NC3 / NC4 那两行正是**两层互补**的证据：**形状绿而行为红** —— 「没有下标」不等于「刷新是对的」。
每档还原后 `sha256` 与注入前**逐字节一致**。脚本 `.workbuddy/tmp/nc77.py`（读作即用，跑完即弃）。

### 77.7 门禁与逐程序集读数

| 项 | 读数 |
|---|---|
| 提交前 | `.workbuddy/tmp/ci-gate-77-pre.log`，**19:45:23**（晚于全部源码改动） |
| 提交后 | `.workbuddy/tmp/ci-gate-77-post.log`，**19:47:04**（晚于提交 `5d27325` 的 **19:46:01**） |
| 结论 | **两次都是 7 步全绿 / `GATE_EXIT=0` / 0 警告 0 错误** |
| banner 步数 | **7**（`[1f]` 折入已有步骤 ⇒ 文档里「门禁 7 步」不过期） |
| `App.UiTests` | **412 → 415**，正好 **+3** = 本轮新判据；提交前后**同数**（415/415） |
| 其余程序集 | Core 2116 / AI 315 / Device 87 / Comm 75 / Sim 44 / HW 44 / Render 4 —— **一个数未动** |
| 大队合计 | **3100**（阶段 76 是 3097） |
| 负载 | `loadavg` 提交前 `4.23 3.75 2.79`、提交后 `4.16 3.83 2.88`（引墙钟时的必备读数） |

**跨阶段逐条同数**：按 75.6 那条剥噪声公式（再补一条：**AUDIT 行自带 ISO 墙钟，整条剥离**，
只保留「有多少条 AUDIT」这个事实；用 `Counter` 而非 `set`，**重数也是读数**），
把 `ci-gate-76-pre.log` 与 `ci-gate-77-post.log` 处理后各 **789 行**，逐条比只差 **3 条**，
三条**各自都能指到本轮的某个改动**、且都是一次性差值：

| diff | 76 阶段（绿） | 77 阶段（绿） | 归因 |
|---|---|---|---|
| `[1f]` 输出 | `读取 7 处（已登记）` | `读取 0 处；两级都硬判为 0，基线已空置` | 本轮主目标 |
| 家目录扫描 | `已检查 637 个文件` | `638 个文件` | ⚠ **原归因「+1 = 新增 `ListRowInPlaceRefreshUiTests.cs`」是错的**（阶段 78 复核后更正）：该脚本扫描面是 `git ls-files 'src/*' 'tools/*' 'scripts/*' 'firmware/*'`，**`tests/` 本来就排除**。这 +1 的真来源是**阶段 76** 的新判据脚本 `scripts/check-index-based-control-lookup.py`（提交前未跟踪、提交后才进 `git ls-files`）⇒ 一条**上一阶段留下的**差异。详见 78.8 |
| `App.UiTests` | `通过 412 / 总计 412` | `415 / 415` | **+3** = 本轮 3 条判据 |

其余 **786 行逐字相同** ⇒ **行为中性**。
⚠ 对照基线**必须**取阶段 76 的**提交前**日志：`ci-gate-76-post.log` 是那次 **#47 偶发红**的运行
（只有 94 行、还夹着 `GATE_EXIT=1`），拿它当基线，diff 会被「红/绿」本身淹没、读不出本轮的东西。


### 77.8 仍未闭环（诚实边界）

① **`AlarmConfigView` 那处没有行为判据**：它刷新的 `group.Alarms.Count` 在该编辑路径上
   **不可能变过**（`OnAddAlarm` / `OnDeleteAlarm` 各自都调了 `RefreshGroupList()`）⇒ 只做结构改造、
   无行为可断言。**刻意没删**：`AgentToolFactory` 能直接往 `group.Alarms` 里加报警，
   万一那个路径正开着面板，这行是一处（偶然的）修复。
② **只断言「文本」**：状态圆点那种**颜色**没进判据（颜色断言要把具体色值抄进判据）。
③ **不覆盖「项被重建后旧引用是否还在表里」**：三处刷新都是**当次**从 `Items` 现取行，重建即换新实例。
④ **别名仍只追单层**（§33.7 ④ 不变）；**容器类型导航 / 按运行期类型读取**两维当时记为「仍无判据」（§33.7 ③）
   —— ⚠ **该定性已被阶段 78 更正并收口**：两维**本就不该硬判**（实测全是合法用法），真缺口是「位置写入」。
   见 78.3 / 78.10 ③。

登记表 **§34** 是本节的长版。


## 阶段 78（收口 76.7 ③ / 77.8 ④「容器类型导航 / 按运行期类型读取两维无判据」—— 结论是一次**定性更正**：两维本就不该硬判，真正缺的是「位置写入」）— 2026-09-22

### 78.1 一句话结论

上一轮把「容器类型导航」与「按运行期类型读取」记成**缺口**。本阶段先按规矩**去清点真实形状**，
结果反过来：这两维**不该硬判**（3 处类型导航全部是「取 ContentControl 的**唯一**子元素」，
语义上不存在「第几个」）。真正缺的是**位置写入**那一半 —— `Children[2]`（读）与 `RemoveAt(2)`（写）
是**同一个假设**，而阶段 76 只守了读的那一半。落地三件：修掉 `PropertyPanelView.Faceplate` 的
动态区清理（改「持引用的容器」）、判据新增形态 **F4**（元判据 3 档 → **4 档**、`--selftest` 12 → **14 档**）、
新增 3 条**行为**判据。

### 78.2 起点：上一轮自己写下的诚实边界

`76.7 ③` / `77.8 ④` / 登记表 `§33.7 ③` 三处写着同一句：「容器类型导航 / 按运行期类型读取两维**仍无判据**」。
本阶段就是去收口它 —— 但**先量形状，不先写判据**（阶段 76 的首版口径正因为没量而漏了一整类）。

### 78.3 先清点真实形状（读，不预设）—— 两条定性更正

| 维度 | 实测 | 取证 | 结论 |
|---|---|---|---|
| 容器类型导航 | **3 处** | `ChromeWindow.cs:121`（`Content is not Border shell \|\| shell.Child is not Grid root`）、`AnimationEditorView.axaml.cs:876`（`b.Child is TextBlock tb`）、`RecipeEditorView.axaml.cs:1176`（`titleBar.Child is TextBlock tb2`） | **不该硬判**：全是「取 ContentControl 的**唯一**子元素」，`Border.Child` / `Window.Content` 语义上只有一个子元素 ⇒ 不存在「第几个」 |
| 按运行期类型读取 | **20+ 处** | `Items.OfType<ComboBoxItem>()`、`is` / `as` 取值 | **不该硬判**：大量且合法，与「位置」无关 |
| **位置写入** | **2 处** | `TrendConfigView.axaml.cs:684/693/694` 是 `IndexOf` → `RemoveAt(idx)` → `Insert(idx,…)`（**按身份**找完再放回 ⇒ 合法）；`PropertyPanelView.Faceplate.cs:79-80` 是 `while (Count > 2) FaceplateParamPanel.Children.RemoveAt(2);` | **真缺陷**，见 78.4 |

⇒ 阶段 76 定的「四个维度」应更正为**两个**：`.Children` 上的**位置读取**与**位置写入**。
登记的那两维**不是缺口**；本阶段已把 `§33.7 ③` / `§34.7 ④` 两处边界指针改成指向 §35 的更正。

### 78.4 主缺陷与修法（本阶段唯一的产品改动）

`FaceplateParamPanel` 在 XAML 里**恰好 2 个**静态子元素（`FaceplateTemplateName` / `FaceplateTemplateDesc`），
而它的动态参数区清理是**按位置删**：

```csharp
// 修前：字面 2 = 把「前 2 个是固定保留区」编码成了位置契约
while (FaceplateParamPanel.Children.Count > 2) FaceplateParamPanel.Children.RemoveAt(2);
```

往静态区加一个元素（表头 / 提示 / 分隔线）就会被这段清理**静默删掉**（不抛、不报）。
修法是**持引用的动态容器**：

```xml
<StackPanel x:Name="FaceplateParamHost" Spacing="6"/>
```
```csharp
FaceplateParamHost.Children.Clear();   // 两处 Add 一并改道
```

⇒ 静态区怎么长都不受影响。改动面：`PropertyPanelView.axaml` +3、`PropertyPanelView.Faceplate.cs` 14 行。

### 78.5 判据侧：形态 F4 + 元判据第 4 档 + 自检 14 档

```python
F4 = re.compile(_CONTAINER + r"[ \t]*\.[ \t]*(RemoveAt|Insert)[ \t]*\([ \t]*(\d+)[ \t]*(?:,|\))")
```

**只认字面整数**：`RemoveAt(i)` / `Insert(i, …)`（`IndexOf` 的返回值）是「按身份找完再放回」，
刻意不判 —— 自检里有 `_T_OK_BYIDENTITY` 一档守住这条。

🩸 **「清空基线顺手拆掉 fail-closed」的坑在这一层又出现一次**：非空基线此前兼着
「正则改坏 ⇒ 表内条目集体消失 ⇒ 红」这条保险；阶段 77 换的承担者是每次运行的
`meta_positive_control()`，但它当时只验 **3 档**（F1/F2/F3）。F4 加进形状集合而不给元判据加档 ⇒
**F4 正则坏掉时元判据照样全绿**（假门禁）。本阶段把它补成**四档**。

| 项 | 阶段 77 | 阶段 78 |
|---|---|---|
| 硬判形态 | F1/F2（接线）+ F3（读取） | **+ F4（位置写入）** |
| 元判据每次运行的验证档数 | 3 | **4** |
| `--selftest` 档数 | 12 | **14**（+F4 该红 + 按身份该绿） |

顺带修掉自检自身的一处内部矛盾：`selftest()` 的 docstring 写「11 档」、模块 docstring 写「12 档」。

### 78.6 行为判据层（新增 `FaceplateParamRefreshUiTests`，3 条）

形状判据看不见**变量下标**的等价改写 ⇒ 这一层必须由行为兜（78.7 的 NC2 实测了这对）。

| 用例 | 断言 |
|---|---|
| `AThirdStaticChild_SurvivesARefresh`（主判据） | 往静态区注入 `Border{Name="StaticProbe78"}` ⇒ 刷新后探针**存活** + 两个表头仍在 + 动态区恰好 2 条 |
| `Rebind_DoesNotAccumulateDynamicEditors` | 连绑两次 ⇒ 动态区仍 2 条（不累积）、探针仍在 |
| `TemplateWithoutParameters_PutsTheHintInTheDynamicHost` | 零参数 ⇒ 提示进动态区、静态区恒 3 |

**探针自检前置**（阶段 77 立的纪律）：先断言这次绑定**真的走进了** faceplate 分支
（`FaceplateParamExpander.IsVisible` + 标题含「泵模板」），否则「刷新根本没发生」会以
「静态区没被动过」的**假绿**呈现。

🩸 **首跑 3/3 全红，红在我自己的期望上**：`Expected: 2/3, Actual: 3/4` —— 漏算了新加的
`FaceplateParamHost` **自己也是子元素**。改成 `private const int StaticChildCount = 3;` 后 3/3 绿。
比判据本身更值得记的是这条错：**往被断言的结构里加元素时，期望值必须重新数一遍**。

### 78.7 负控 / 正控（4 档，`.workbuddy/tmp/nc78.py`）

注入一律**整块回退**（清理语句 + 两个 `Add` 的目标一起改回去）—— 只改一行会让 `Count > 2`
的语义与原来不同 ⇒ 负控失真。还原用**写回原文**（不用 `shutil.copy2`），两侧 `sha256` 与注入前一致。

| 档 | 注入 | 形状判据 | 行为判据 |
|---|---|---|---|
| NC1 | 整块回退成**字面下标**版（`RemoveAt(2)`） | **RED**，点名 `FaceplateParamPanel.Children.RemoveAt(2)` | — |
| NC2 | 整块回退成**变量下标**版（`for (i = Count-1; i >= 2; i--) RemoveAt(i)`） | **GREEN** | **RED**（3/3 失败） |
| NC3 | 把 F4 正则改坏（`importlib` 直接调 `meta_positive_control()`） | **RED**，点名 F4 | — |
| 正控 | 全部还原后 | GREEN | GREEN |

**NC2 是本阶段最有价值的一条**：它把「形状判据的已知绕过路径」从**推断**变成**实测**
（形状绿而行为红）⇒ 两层互补成立，不再只是设计意图。脚本退出码 0、「✅ 全部通过（4/4）」。

### 78.8 门禁与逐程序集读数

| 项 | 读数 |
|---|---|
| 提交前 | `.workbuddy/tmp/ci-gate-78-pre.log`，**20:11:43**（晚于全部源码改动） |
| 提交 | `47b749e`，**20:12:07**（5 files，+279 / −31） |
| 提交后 | `.workbuddy/tmp/ci-gate-78-post.log`，birth **20:12:12** / mtime **20:13:35** ⇒ **晚于提交** |
| 结论 | 两次都是 **7 步全绿 / `GATE_EXIT=0` / 0 警告 0 错误** |
| banner 步数 | **7**（`[1f]` 折入已有步骤 ⇒ 文档里「门禁 7 步」不过期；`[1b]/[1d]/[1e]/[1f]` 是无编号子步） |
| `App.UiTests` | **415 → 418**，正好 **+3** = 本轮 3 条行为判据 |
| 其余程序集 | Core 2116 / AI 315 / Device 87 / Comm 75 / Sim 44 / HW 44 / Render 4 —— **一个数未动** |
| 大队合计 | **3103**（阶段 77 是 3100） |
| 独占两段 | Core 8 / App.UiTests 2（与阶段 77 同数） |
| 负载 | `loadavg` 提交前 `2.82 2.21 1.90` → `5.38 3.51 2.41` |

**跨阶段逐条同数**（基线取阶段 77 的**提交前**日志，两侧各 **786 行**）：

| diff | 阶段 77（绿） | 阶段 78（绿） | 归因 |
|---|---|---|---|
| `[1f]` 输出 | `…找控件…；接线 0 处、读取 0 处…` | `…**定位**控件…；位置读取 + 位置写入…` | 本轮主目标 |
| `App.UiTests` | `415 / 415` | `418 / 418` | **+3** = 本轮 3 条判据 |

其余 **784 行逐字相同** ⇒ **行为中性**。
⚠ 本轮把 **NuGet restore 的状态行**也加进剥噪声公式：同一棵树可能打印 9 条
「已还原 X（用时 N 毫秒）」也可能只打印 3 条「所有项目均是最新的」⇒ 不剥掉会把 diff 淹没
（首次对比时它单独贡献了 **12 条假差异**）。

🩸 **顺带更正阶段 77 的一条归因**：77.7 把「家目录扫描 `637 → 638`」记成
「**+1 = 新增 `ListRowInPlaceRefreshUiTests.cs`**」。**这条是错的**。`check-path-hygiene.sh`
的扫描面是 `git ls-files 'src/*' 'tools/*' 'scripts/*' 'firmware/*'`（脚本第 12–14 行写明
**`tests/` 有意排除**）⇒ 测试文件根本不在面里。复核六个提交的**匹配数**：

| 提交 | 匹配 | 本笔新增的受扫文件 |
|---|---|---|
| `661073d`（阶段 75 文档） | 638 | — |
| `69157fb`（**阶段 76** 修复） | **639** | **`scripts/check-index-based-control-lookup.py`** |
| `5d27325`（阶段 77 修复） | 639 | —（本轮只加测试文件） |
| `47b749e`（阶段 78 修复） | 639 | —（同上） |

⇒ 那 **+1 发生在阶段 76**（新判据脚本**提交前未被跟踪、提交后才进 `git ls-files`**），
是一条**上一阶段留下的**差异，与本轮改动无关。门禁打印 **638** 而非 639 的原因也量到了：
一个非 ASCII 路径（`firmware/t113/T113固件烧录调试检查清单.md`）被 git 加了引号 ⇒
**逃过扩展名过滤**、又被脚本的 `[ -f "$f" ]` 判成非普通文件而跳过 ⇒ **639 − 1 = 638**。
⇒ 对拍脚本的注释里已补上这条方法论：**差异必须归因到「本阶段引入了什么」**。

### 78.9 ⚠ 环境残留（非仓库缺陷）：一次无法复现的 `a.out`

提交后门禁窗口里，仓库根出现过 `a.out`（ELF64-x86-64 **relocatable**、544 B、无符号表、
无 `.comment`、段全空；`git check-ignore` 判**未忽略** —— 即 `git add -A` 会误收）。

| 步 | 做法 | 结果 |
|---|---|---|
| 1 | 把它挪出仓库根（`.workbuddy/tmp/a.out.bak`） | — |
| 2 | **逐行打时标**整条重跑门禁（`.workbuddy/tmp/repro78.sh` → `ci-gate-78-repro.log`，**20:47:45**、7 步全绿） | **未复现** ⇒ 不是门禁产的 |
| 3 | 逐点排除门禁内**全部** gcc 调用点 | `run_host_tests.sh`：产物全在 `/tmp` + `trap cleanup`，且它 `cd` 到 `firmware/t113`（**结构上**写不到仓库根）；`check_lv_conf_flags.sh`：`-fsyntax-only` + `mktemp -d`；`ReleasePackager --self-check`：单独复跑不产；`BudgetCheck`：门禁只构建不运行；`SimExport` / `T113KitBuilder`：门禁不运行 |
| 4 | git 钩子（`.git/hooks/` 只有 `*.sample`、`core.hooksPath` 未设） | 排除 |

⇒ **生产者未定位**，最可能是沙箱/宿主环境的产物。**刻意不加判据**：不可复现 ⇒ 做不出负控，
而「做不出负控的判据不发布」是既有纪律。文件已挪出仓库根。

### 78.10 仍未闭环（诚实边界）

① **F4 只认字面整数**：`RemoveAt(i)` 这类**变量下标**仍是**已知绕过路径**，靠
   `FaceplateParamRefreshUiTests` 兜（NC2 已实测「形状绿 / 行为红」这对）。判据看不见什么，已写进脚本 docstring。
② **`TrendConfigView` 那 3 处没进判据**：它们是 `IndexOf` → `RemoveAt/Insert` 的**按身份**用法，刻意不判。
③ **「类型导航 3 处不判」是人工清点的结论**：判据**不会**在将来新增一处**真错**的 `Content is X` 时报红
   （该形状与合法用法无法用正则分开）。**知情选择，不是遗漏**。
④ **行为判据只锚在 faceplate 一个入口**：将来新增的动态区不会自动获得保护。
⑤ **`§33.7 ③` 的更正依赖人工复核**：判据侧对此无自动信号。

登记表 **§35** 是本节的长版。


## 阶段 79（闭环 §4 #37 ③「文件打开/保存路径未覆盖」—— 主窗口的**打开/保存两条路径第一次接到磁盘上**：5 条行为判据 + 5 组负控）— 2026-09-22

### 79.1 一句话结论

`MainWindow` 早有「能构造 / 接线闭合 / 点了有反应」三层判据，但**没有一条碰过磁盘**：
登记表 §4 #37 遗留 ③ 写着「文件打开/保存路径未覆盖」，而工具栏判据里
`Skip("BtnSaveProject", "保存工程：写工程文件到磁盘")` 把保存**明确跳过**。
本阶段把两条路径接到真文件上：新增 `ProjectFileOpenSaveRoundTripUiTests`（**5 条**，
隔离目录里真打包、真写盘、真回读），**产品代码 0 行改动**（判据建起来即 5/5 绿）。
5 组负控里 **NC5 是决定性的**：在「保存把工程写成空壳」的注入下，
既有的 `MainWindowToolbarInvocationUiTests` **3/3 照样绿** —— 把「旧那一层原理上看不见保存路径」
从推断变成了实测。专题 `docs/2026-09-22-project-file-open-save-net.md`。

### 79.2 起点：登记表里唯一一处「本体没覆盖」

| 层 | 判据 | 覆盖到哪 |
|---|---|---|
| 构造 / 布局 | `MainWindowConstructionUiTests`（阶段 46） | 能建起来 |
| 接线闭合（143 具名控件） | `MainWindowWiringUiTests`（阶段 46） | 名字被碰过 |
| 点了有反应 | 菜单 48 / 工具栏 / 对话框 57 | `OwnedWindows` 增量与类型 |
| **真读写磁盘** | **无** | — |

不是「漏了一两个用例」，而是这一层**被显式跳过**（那句 `Skip` 就在工具栏判据里）。

### 79.3 先量真实形状（读，不预设）—— 四条必须先量清的事

#### 79.3.1 两条真接缝 vs「选择器外壳」

| 入口 | 位置 | 性质 |
|---|---|---|
| `StartWithOpenFile(string filePath)` | `MainWindow.axaml.cs:988` | **唯一不弹选择器**的打开入口（splash「最近工程」用它） |
| `SaveProjectCore()` 后半段 | `:1178` | `_currentHmiPath is not null` ⇒ **不弹框**直接 `SaveToPath` |

`OpenProjectCore`（:1136）/ `SaveProjectCore` 前半段是**选择器外壳**。
🩸 而 headless 下的选择器是**静默空返回**（不抛、不弹窗、状态栏不变、文件不动）⇒
`OwnedWindows` 在两种分支下**都是 0** ⇒「没弹窗」**不可判**，只能当读数。

#### 79.3.2 🩸 别把「轮数」当「时间预算」（本阶段最大的方法论教训）

`StartWithOpenFile` 是 `async void`，headless 靠 `Dispatcher.UIThread.RunJobs` 清队驱动。
第一版写「清 90 轮」——**90 轮 ≈ 几毫秒**，池线程还没被调度 ⇒
把「还没跑完」误读成「卡住」，白查两轮。改**墙钟**（清队 + `Thread.Sleep(1)` 到谓词成立）：

| 动作 | 轮数 | 墙钟 |
|---|---|---|
| 打开（解包 / 装面板 / 切屏） | 28 | **114 ms** |
| 保存（已知路径） | 2 | 7 ms |
| 打开不存在的路径 | 1 | 0 ms |

#### 79.3.3 终态谓词不能用 `_currentProject`

它在 `OpenHmiFile:1044` 就被赋值，而 `SetUnsavedChanges(false)` 在 `:1062`、`Title` 在 `:1063`
⇒ 用 `_currentProject` 会读到**中途态**（本条判据的第一版就据此误判过「打开没落地」）。
改用 `Title` 含工程名。

#### 79.3.4 两条**有意**行为 ⇒ 判据不能用「逐字节等价」

| 行为 | 位置 | 实测 |
|---|---|---|
| 打开注入 **4 个预置模板**（按 Id/Name 去重） | `InitializeTemplateManager`（:790-806） | `project.json` **10866 → 25836** 字符 |
| 保存补上原包没有的 `safeAreaPadding {left/top/right/bottom/isZero}` | 序列化 schema | +123 字符 |

⇒「打开 → 原样保存 → 逐字节等价」**必然红、且红在有意的行为上**；改用更强的**幂等**判据（S1 == S2）。

### 79.4 判据层：`ProjectFileOpenSaveRoundTripUiTests`（5 条）

| 用例 | 断言的可分辨内容 |
|---|---|
| `Open_LoadsPackageContentIntoTheWindow` | 标题含包内工程名 + 状态栏 `控件: 2 \| 屏幕: 2`（与**从 ZIP 直读**的真值比）+ 状态栏指向包内第一屏 |
| `SaveWithKnownPath_WritesBackToTheSameFileWithoutPicking` | 哨兵 mtime 被改 + 状态栏 `已保存: demo.hmi` + 无 `.tmp` 残留 |
| `EditThenSave_ChangeSurvivesOnDiskAndNothingElseChanges` | 点 `BtnQuickButton` 加控件 → 保存 → **直接解包磁盘**：首屏控件 +1、其余一项不变 |
| `SaveIsIdempotent_AcrossOpenSaveCycles` | 两窗口两轮：S1/S2 逐字节相同，且第二轮**真的写了盘**（防「平凡幂等」） |
| `OpenMissingPath_ReportsThePathAndKeepsCurrentProject` | 状态栏 == `文件不存在: <那个路径>` + `Title` 不变 + 不弹窗 |

夹具 2 屏 / 首屏 2 具名控件 / 1 变量 / 非默认作者与版本 —— **刻意不用 `new HmiProject()`**，
否则「打开真的装了内容」与「打开只是没崩」分不开。
隔离目录必须 `TestTempDir.NewIsolatedDir`：打开/保存会走 `RefreshGitState` → **起 git 进程**做
`rev-parse`，落在仓库里会向上发现父仓库 `.git`。

### 79.5 负控：5 组（`.workbuddy/tmp/nc79/`）

注入作用于**真实产品文件**；还原用**写回原文**（不用 `shutil.copy2`），逐次 `sha256` 校验一致。

| 档 | 注入 | 结果 |
|---|---|---|
| NC1 | `SaveToPath` 里给 `Description` 追加**随机 8 字符** | **只红** `SaveIsIdempotent` |
| NC2 | 状态栏换成 `status.save.failed` | **只红** `SaveWithKnownPath` |
| NC3 | `projectJson` 序列化成空壳 | 红 `EditThenSave`（+ 附带幂等：空壳的 `createdAt/lastModifiedAt` 不同） |
| NC4 | 不存在路径时不报路径 | **只红** `OpenMissingPath`（两条断言同时命中） |
| **NC5** | **在 NC3 注入下跑既有 `MainWindowToolbarInvocationUiTests`** | **3/3 照样绿** ⇒ 旧层看不见保存路径（实测） |

🩸 **NC1 前两版注入无效**：① `if (A && B && true)` 是**恒等变换**；② 加固定后缀 ⇒ **两侧同加、自收敛**。
第三版改成「每次保存追加随机 8 字符」才打中幂等
⇒ 负控绿有两个来源（判据有盲区 / 注入没生效），**必须先排掉后者**。

### 79.6 判据质量的三处加固

| 加固 | 原因 |
|---|---|
| `SetSentinel(path)` 打哨兵后**回读自检** | 时间戳精度/取整会让 `LastWriteTimeUtc != sentinel` **恒真** ⇒ 假绿（判据的输入面自证） |
| **先判 `Screens.Count == 0` 再取 `[0]`** | NC3 实测：空壳让 `after.Screens[0]` 抛裸 `ArgumentOutOfRangeException`，把可读消息换成了异常 |
| `JudgeReadings.Write` **前移到断言之前**（5 处） | `Assert.True` 的红路径会跳过其后的一切 ⇒ 消息里写「见读数」而读数根本没落盘 |

顺带把工具栏判据里那句 `Skip` 的**理由**改准：它跳过的**只是这一层**（该窗口从没绑定过工程文件路径
⇒ `_currentHmiPath` 为空 ⇒ 点了会走选择器分支，而 headless 选择器静默返回空），
并指向本类的真判据。

### 79.7 门禁与逐程序集读数

本阶段的门禁**跑了多次**，全部列出来（中途一次红：79.8 的环境侧，**红日志已被后一次绿的覆盖**，
现场只留在本节的引用里）：

| # | 日志 | 时刻 | 结果 | 为什么算 / 不算 |
|---|---|---|---|---|
| 1 | `ci-gate-79-pre.log`（已被覆盖） | 22:19:52–22:21:09 | 绿 | ❌ **早于最后一次源码改动（22:21:24）** ⇒ 不算数 |
| 2 | `ci-gate-79-pre.log`（已被覆盖） | 22:23:5x–22:25 | ⚠ **红** | 环境侧 `No space left on device`（见 79.8） |
| 3 | `ci-gate-79-pre.log`（已被覆盖） | 22:27–22:28 | 绿 | 中途绿，**不是**最终那次 |
| 4 | `ci-gate-79-pre.log` | **22:37:27–22:38:49** | **绿** | ✅ **提交前**（源码最后改动 22:21:24 ⇒ 晚 16 分钟） |
| 5 | `ci-gate-79-post.log` | **22:39:15–22:40:25** | **绿** | ✅ **提交后**（提交 `f19b61a` 于 **22:39:12** ⇒ 时序成立） |
| 6 | `ci-gate-79-head.log` | **22:43:06–22:44:27** | **绿** | ✅ **文档提交 `9247829` 之后**（HEAD） |

第 6 次与第 5 次**逐条对拍**：两侧各 **786 行**、**0 条差异** ⇒
「文档提交不动行为」是**量出来的**，不是推理出来的（这也顺带证明 `docs/` 不在门禁任何步骤的扫描面内）。

| 项 | 读数 |
|---|---|
| 提交 | `f19b61a`（测试，**22:39:12**）+ `9247829`（文档，4 files **+527 / −1**） |
| 三次门禁 | 均 **7 步全绿 / `GATE_EXIT=0` / 0 警告 0 错误** |
| `App.UiTests` | **418 → 423**，正好 **+5** = 本轮 5 条行为判据 |
| 其余程序集 | Core 2116 / AI 315 / Device 87 / Comm 75 / Sim 44 / HW 44 / Render 4 —— **一个数未动** |
| 大队合计 | **3108**（阶段 78 是 3103） |
| 独占两段 | Core 8 / App.UiTests 2（与阶段 78 同数） |
| `loadavg` | 提交前 `3.23 5.06 4.83` |

**跨阶段逐条同数**（基线取 `ci-gate-78-pre.log`，两侧各 **786 行**）：

| diff | 阶段 78（绿） | 阶段 79（绿） | 归因 |
|---|---|---|---|
| `App.UiTests` | `418 / 418` | `423 / 423` | **+5** = 本轮 5 条判据 |

其余 **785 行逐字相同** ⇒ **行为中性**。

🩸 **核时序只能用 mtime**：`>` 截断**不更新 `birth`** —— 这个日志的 `birth` 停在 `22:24:02`
（首次创建），而它实际是 22:37:27–22:38:49 那次跑出来的。第 1 行的「不算数」正是这条规则的用例：
它比源码改动早 15 秒，却被写进过总结。

### 79.8 ⚠ 环境发现（非仓库缺陷）：沙箱的 `/tmp` 是 **10 MB** tmpfs，门禁会间歇 `ENOSPC`

**现象**：本阶段第 2 次提交前门禁红，红在

```
HmiEditor.Device.Tests.T113SoBuilderTests.ResolveContext_MissingSdk_ReportsMissingItems [FAIL]
  System.IO.IOException : No space left on device : '/tmp/sdk-<guid>/generated_hmi.c'
```

而那个用例写的是 `File.WriteAllText(src, "int x;\n")` —— **7 字节**。
⇒ 不是"这个文件大"，是**那一刻 /tmp 没有空间**。

**取证链（每一步都有读数）**：

| 步 | 做法 | 读数 |
|---|---|---|
| 1 | `grep " /tmp " /proc/self/mountinfo` | `/tmp` 被一层 **`tmpfs … size=10240k`** 覆盖（另有 `mode=755,uid=1000`）；`/dev` 同样是 10240k |
| 2 | **实写探针**（`dd` 到 `/tmp`） | 写 8 MB 后 80%；再写 8 MB **只落地 2 MB** ⇒ 10240 KB 处 100% 满，`rm` 后回 0% ⇒ 10 MB 是**真上限**，跨调用稳定（mountid 未变） |
| 3 | 同一条门禁命令**原样重跑** | **绿**（`GATE_EXIT=0`）⇒ 与代码无关 |
| 4 | 采样器（0.02 s）跑 3 轮门禁，记 `/tmp` 峰值 | 1164 / **10240** / 384 KB ⇒ **有 1 轮顶满**，但**没有一次红** |
| 5 | 采样时**同时记门禁日志字节偏移**，事后按偏移对齐到步 | 峰值落在 **`[5] dotnet test (Release) —— 大队`**；另两轮峰值 356 / 328 KB |

⇒ 机制落地：**门禁的测试步会把 10 MB 的 tmpfs 瞬时写满**；抢不到空间的那次写就 ENOSPC ⇒ 红。
**红率**：本阶段 5 次门禁里 1 次红（≈1/5）。

🩸 **采样频率决定你能不能看见这个尖峰**：第一版采样器是 **0.1 s**，那一轮只读到 **328 KB**，
当时据此写下「容量解释站不住」的结论 —— 而 0.02 s 的采样器在 6 轮里抓到 **2 次顶满**。
**低频采样给出的"没事"不是读数，是盲区。**

**生产者仍未定位**：加过**触发式快照**（可用 < 2 MB 时立刻 dump `ls -la /tmp` + `du -sk /tmp/*`），
但那一轮峰值只有 328 KB、**没触发** ⇒ 只知道"在测试步"，点不到具体目录。
**刻意不加判据**：不可按需复现 ⇒ 做不出负控，而「做不出负控的判据不发布」是既有纪律。
**也没有做规避**：把 `TMPDIR` 挪到别的文件系统会**改变被测环境**（正是「一次只放开一个变量」的反面），
而当前红率低、且红的形态可一眼识别（`No space left on device`）。

**判据（给下一次遇到它的人）**：门禁红若含 `No space left on device`，
先 `df -k /tmp`（应看到 `size=10240k`），再**原样重跑一次**；重跑绿即环境侧，不要去看代码。

### 79.9 仍未闭环（诚实边界）

① 判据只覆盖**这两条入口**（`StartWithOpenFile` + `BtnSaveProject`）；菜单里的「另存为 / 导出」等
   仍只有静态绊线。
② **「打开后工程被标脏」是产品行为问题**（`_hasUnsavedChanges = true`、标题带 `*`）。
   已取证触发链：`OpenHmiFile:1060 LoadProjectIntoPanels` → `NavigationFlowView.LoadProject` →
   静态总线 `RaiseNavGraphChanged` → `WireDockAggregateEvents` → `SetUnsavedChanges(true)`；
   且「只清队**不推进渲染**」也照样发生 ⇒ 它必然发生在 `:1062` 的 `SetUnsavedChanges(false)`
   **之后**。**刻意不断言**（拿它当断言会把缺陷固化），另行登记。
③ 判据强度上限是**幂等 + 内容清单**（79.3.4 的两条有意行为否掉了「逐字节等价」）；
   若将来模板注入改成"按需"、`safeAreaPadding` 不再补，可考虑升级。
④ `OpenMissingPath` 只覆盖「路径不存在」；**权限不足 / 包损坏**（ZIP 坏、`project.json` 缺）两条
   分支未覆盖。


## 阶段 80（闭环 §4 #37 边界④「权限不足 / 包损坏」—— **并修掉判据自己一条不成立的前提**：3 条 App 判据 + 1 条 Core 判据 + 6 组负控）— 2026-09-22

### 80.1 一句话结论

阶段 79 收尾时**自己写下**边界④（「`OpenMissingPath` 只覆盖『路径不存在』；**权限不足 / 包损坏**未覆盖」），
本阶段闭环它：`ProjectFileOpenFailureUiTests`（**3 条 App 层**）+
`HmiPackageServiceTests.Unpack_BrokenPackage_LeavesNoTargetDirectoryBehind`（**1 条 Core 层**），
负控 **6 组**。过程中修掉**两个**东西 —— ① 一个**真缺陷**（失败的打开会在进程退出前一直攒空工作目录）；
② **判据自己的一条前提**：第一版的「会话临时根归本用例独占」在**大队并发下必然不成立**，
被门禁第 [5] 步**按设计**抓到（就是那条前提自证断言自己报的红）。
「权限不足」那半如实标为**不可复现**（`id -u=0` ⇒ `chmod 000` 之后 root 照样打开）⇒ 转登记 **#58**，
**刻意不写一条永远绿的装饰判据**。专题 `docs/2026-09-22-project-file-open-failure-net.md`；登记表 §37。

### 80.2 起点：阶段 79 自己写下的边界④

| 层 | 判据 | 覆盖到哪 |
|---|---|---|
| 构造 / 布局 / 接线 / 点了有反应 | 阶段 46–55 各层 | — |
| 真读写磁盘（**成功**路径） | 阶段 79 `ProjectFileOpenSaveRoundTripUiTests`（5 条） | 能打开、能保存 |
| 真读写磁盘（**失败**路径） | **本阶段 3 + 1 条** | 包坏了 / 路径不存在 / 旧格式坏了 ⇒ 报失败且**什么都不变** |

要害：`_currentProject` / `_currentHmiPath` / `RecentProjects.Record` / `Title` 的赋值**全在解包之后**
（`MainWindow.axaml.cs:1044/1046/1050/1063`）⇒ 半路异常**天然**污染不到它们。
但那是**当前实现的巧合**，不是写在任何地方的不变量 ⇒ 判据钉的正是这条。

### 80.3 先量真实形状（9 条坏输入 × 冷/热窗口，**只读探针**）

探针**只写读数、不断言**（走 `JudgeReadings`），源码留档 `.workbuddy/tmp/probe80/ZZProbe80UiTests.cs.bak`。
9 条坏输入的实测文案与命中检查点见登记表 §37.3 与专题 §2。两条方法论/事实值得在这里记：

| 读数 | 含义 |
|---|---|
| ⑥（旧格式非 JSON）**不抛 `JsonException`** | `ProjectSerializer.Deserialize` **吞掉异常返回 `null`** ⇒ 走 `status.open.parse.failed`。**"抛异常"与"返回 null"是两条不同路径**，凭代码形状猜不出来 |
| ⑨（`chmod 000`）**打开成功** | `id -u=0` ⇒ root 绕 DAC ⇒ 「权限不足」这档在本沙箱**不可复现**（不写装饰判据） |
| 旧格式打开成功 ⇒ `_currentHmiPath = null` | **有意行为**（`SaveProjectCore` 注释原文："新工程 / legacy JSON（未绑定文件路径）→ 需选择保存位置"）—— 先读注释再看形状，省掉一条假登记 |
| 成功打开留 2 个会话目录，其一是**空的** | 空的那个是 `_assetDirectory` —— 资产导入的**写目标**，不是扫描源 ⇒ 指着空目录是对的 |

### 80.4 判据形状：**同一不变量的两层**

| # | 用例 | 层 | 依赖共享状态吗 |
|---|---|---|---|
| 1 | `CorruptInputs_ReportFailureAndChangeNothingTheUserCanSee` | App | 是（窗口 + 最近工程文件） |
| 2 | `MissingPath_ReportsThePathAndChangesNothingTheUserCanSee` | App | 是 |
| 3 | `FailedOpen_LeavesNoWorkspaceDirectoryBehind` | App | **是**（会话根来自进程级环境变量）⇒ 靠 80.6 的两条归属机制自治 |
| 4 | `Unpack_BrokenPackage_LeavesNoTargetDirectoryBehind` | **Core** | **否** —— 目标目录由判据自己造、独占；另带**正控**（好包**必须**建出目录，否则"坏包不建"可由"永远不建"满足） |

🩸 原因那一半**刻意不钉措辞**（①⑤ 把 .NET 原始英文异常消息摆进了用户可见状态栏 ⇒ 转 #59）；
只断言**结构** `打开工程失败: <非空原因>`，模板与操作名从字典取。
另有一条**可判的性质**（非措辞）：**5 条包损坏的原因两两不同**（实测 5/5）⇒
没有某种"一网打尽"的 catch 把不同损坏形态收敛成同一句话。

分工**不是推断**，是负控实测：回退**调用点** ⇒ 只有 App 层红；回退 **`Unpack`** ⇒ **两层都红**
（App 层引用 Core，反之不然）。

### 80.5 顺带修掉的真缺陷：失败的打开留下工作区目录

`OpenHmiFile` 的 ZIP 分支原来**先**建解包目标目录、**才** `File.OpenRead` + `Unpack`；
包一坏，该目录一个文件都没解出来 ⇒ **空目录**，而会话目录的兜底释放在**进程退出** ⇒
一次会话反复打开坏包会一路攒（探针实测：**5 次失败 = 5 个空目录**）。

| 改动 | 内容 |
|---|---|
| `AppPaths` | 拆成 `NewTempSubdirPath(prefix)`（**只给路径、不创建**）与 `NewTempSubdir(prefix)`（= 前者 + `CreateDirectory`）；路径拼法**仍只有一处** |
| `MainWindow` | `CreateWorkspaceDirectory(project, bool create = true)` 加开关；解包落点走 `create: false` |

**为什么"不建"比"建了再删"好**：`Unpack` 在**全部前置校验**通过之后才 `Directory.CreateDirectory(assetDir)`
⇒ 五种失败**一个目录都不建** —— 修在"从没建过"那一侧，少一个破坏性机制（删除器）就少一份要自带守卫的东西。
复跑同一份探针：冷窗口 `0→1→…→5` ⇒ **`0 ⇒ 0`**；热窗口（①~⑦）`1→…→6` ⇒ **`1 ⇒ 1`**；⑨ 成功打开仍 `1 → 2`（未变）。

### 80.6 🩸 判据自己的前提：大队并发下**必然不成立**

第一版工作区判据自证「会话临时根必须落在我的沙箱里」，单跑绿、三条一起跑绿，
**进门禁第 [5] 步红**（大队 426 条并行），错误消息逐字：

> `会话临时根「/…/opensave-idem-data-e4656…/temp/.sessions/3506-…」不在本用例的沙箱「/…/openfail-workspace-data-3881…」下 ⇒ 它会与并行用例共享，前后比较不再自治，这条判据不能用`

**这条红是判据按设计自己报的** ⇒ 归类是"判据的前提不成立"，**不是被测对象有问题**。根因：

| 我以为 | 实际 |
|---|---|
| `DataRootSandbox` 把数据根重定向到"本用例独占"的目录 | 它改的是**进程级**环境变量 `HMI_APP_DATA_ROOT`；`AppPaths.DataRoot` **不缓存**（每次读）⇒ 并行用例各自在构造/释放时抢着改写 |
| 于是 body 期间 `SessionTempRoot` 是我的 | body 期间它是"**最后一个**构造沙箱的用例"的（实测我这边是 `opensave-idem-data-*`） |
| 会话 id 进程级 + 根是我的 ⇒ 目录必然落在我沙箱里 | 会话 id 确实是进程级的，但**根会漂** ⇒ 我建的目录落进**别人**的沙箱（别人释放沙箱时一并删掉） |

**处置**：删掉那条前提断言（拦不住后面 7 次打开里的漂移），改成两条**不依赖独占**的归属机制 ——
① 扫描面**覆盖所有可能的数据根**（沙箱数据根一律从"构造那一刻的数据根"的**同级**派生 ⇒ 全部平铺在
**同一父目录**下，枚举它即可）；② 归属靠**唯一工程名**（只有本用例会拿 `ZZF80<8 位 hex>` 当工程名）。
比较窗口从"整场前后快照"压到**单次打开的前后**，断言**只查"新增"方向**（"变少"不归它管）。
⚠ **没消除的边界**：目录落在别人沙箱里、而那个沙箱恰在两次扫描之间被释放时，真泄漏会**漏报**
（判据变弱、**不假红**）；同一不变量的契约面由 80.4 的第 4 条（**不依赖任何共享状态**）兜住。

**门禁比我先发现的另外两件事**（都在"我已本地跑绿"之后，同属"变更点不在被测路径上却落在落点上"）：
① `[3] 构建（警告即失败）` 拦下 4 条 `CS8620` —— **元组元素的可空性不协变**
（`(string, string?)[]` 形参配 `new[] { (Key, Json) }` 推不出宽化）⇒ 改 `params` 形参；
② `SingleFileSizeGuardTests` 拦下 `MainWindow.axaml.cs 3024 ≠ 3013` ⇒ 按登记表 §26.5 的既有裁定
**压回行数、不调大基线**，于是**重做修法**为"不提前建"（只改 1 行调用点 + 工厂 1 行三元）⇒ 回到 3013。

### 80.7 负控 6 组：三处自我纠错

| 注入 | 打在哪条腿 | 判红谓词 | 读数 |
|---|---|---|---|
| NC1 记最近工程提前 | 最近工程腿 | 「最近工程列表被改了」 | 失败 1 / 通过 2 |
| NC2 失败时也认下 `_currentHmiPath` | 保存目标腿 | 「写回原好包」 | 失败 1 / 通过 2 |
| NC3 只报操作名、丢原因 | 形状腿 | 「失败文案的形状不对」 | 失败 1 / 通过 2 |
| NC4 解包落点改回 `create: true` | 工作区腿 | 「失败的打开留下了工作区目录」 | 失败 1 / 通过 2 |
| NC5 原因传空串 | 原因腿 | 「失败文案里没有原因」 | 失败 1 / 通过 2 |
| NC6 `Unpack` 的 `CreateDirectory` 提前 | **被调方腿** | Core「目标目录被建出来了」+ App「失败的打开留下了工作区目录」 | Core 1/0；App 1/2 |

**三处自我纠错**：① NC2 是我的**期望串**漏了断言消息里的 markdown 记号；
② NC3 暴露**判据自己的诊断缺口**（第一版把"形状不对"归成"标记仍在 ⇒ 点了没反应"，把人指错地方），
拆开驱动谓词与形状断言后准确命中，并**因此补出 NC5**；③ NC1 **锚点失效**被判 `anchor-error`
而**不是**"通过" —— 若驱动器只判"红不红"，这一例会静默变成永远绿的空转。
> `失败 N` 只说明"有一处红"，**不说明红在你想验的那句上**。

判据本体改过之后**前五组重跑**（注入不变、判据是新版）⇒ 6 组读数出自同一版本。

### 80.8 门禁读数与环境

| 步 | 内容 | 读数 |
|---|---|---|
| [1] | `bash -n` 脚本语法检查 | 通过 |
| [2] | 路径卫生（无硬编码开发机家目录） | 通过 |
| [3] | `dotnet build`（Release，**警告即失败**） | **0 警告 / 0 错误**（23.09 s） |
| [4] | 出包 / 预算实测工具 | 通过 |
| [5] | `dotnet test`（Release）大队 | Simulator 44 / Hardware 44 / Communication 75 / Device 87 / RenderTests 4 / AI 315 / **Core 2118** / **App.UiTests 426** 全绿 |
| [6] | 需独占 / 空队列的用例单独跑 | Core 8 / App.UiTests 2 全绿 |
| [7] | 固件 host 测试（9 段） | 全过 |

逐程序集计数：**Core 2117 → 2118**（+1 = 80.4 第 4 条判据），**App.UiTests 426 不变**
（本阶段三条判据在阶段 79 之后就已计入）。**三次红各在不同的步上**：`[3]` 构建基线（CS8620）、
`[5]` 大队并发（判据前提）、以及 `SingleFileSizeGuardTests`（行数基线，在第 [5] 步之内）。

| 项 | 读数 |
|---|---|
| 提交前门禁 | `.workbuddy/tmp/gate80-pre.log`，**23:36:31**（`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误） |
| 提交 | `1013eaf`（代码 6 files，+910 / −6）+ `8044977`（文档 3 files，+721 / −2），**2026-09-22T23:36:49+08:00** |
| 提交后门禁 | `.workbuddy/tmp/gate80-post.log`，**23:38:12** ⇒ **晚于提交** ✓（`GATE_EXIT=0` / 7 步全绿） |
| 提交前后对拍 | 逐程序集汇总行**剥掉墙钟**后 **10 行逐条同数**（`diff` 为空）；两次构建都是 0 警告 / 0 错误 |

**环境读数（本轮实测）**：`/tmp` = tmpfs **10240 KB**、当前 0% 使用，而 `TestTempDir` 的
空间探测阈值是 **16 MB** ⇒ 探测**必然失败**、恒回退到仓库内 `.hmi-test-tmp/`；
`TMPDIR` **为空**；`RLIMIT_AS` hard/soft 都是 **unlimited**（本轮无 4 GiB 限制，与"沙箱限制随会话变"一致）；
`.hmi-test-tmp/` 下 **897** 个目录（23:27 判据读数）/ **900**（23:5x 手工 `ls -d */`）/ **2902** 个条目
⇒ 残留量**随测试运行继续增长**（没有回收机制）；本判据只做浅层枚举，4 条用例合计 **2 s**，耗时可忽略。
bind-mount 寿命再量一次：同一会话内 `rm -f`（unlink）**≤15 分钟**成功，
而约 20 分钟时 `sed -i`（写临时文件 + **rename 覆盖**）仍报 **EBUSY**、就地 `write_text` 成功
⇒ **「删得掉」与「rename 覆盖得了」不是同一件事**（登记表 §5 #32 的定性钉细）。

### 80.9 新增登记与仍未闭环

新增 **#59**（失败原因是 .NET 原始英文异常消息、且不含文件名、⑥⑦ 两条不同根因同句；修法方向 =
`Unpack` 各失败给**诊断码**、App 层做"码 + 参数"→ 字典映射，Core 层不引本地化）与 **#58**
（「权限不足」档环境不可复现）。⚠ **编号避让**：`#57` 已被 §34（阶段 77）在会话内使用 ⇒ 本轮用 **#59**。

仍未闭环：**#59 未修**；**#58 不可复现**；**解包中途失败**（校验通过之后 `ExtractToFile` 抛 IO）
仍留半填充目录（要覆盖它得走"建了再删" + 给删除器自带守卫，本阶段按"少一个破坏性机制"取舍）；
**App 层工作区判据的毫秒级漏报窗口**；**「打开后工程被标脏」**（探针两次读数不一致 ⇒ 时序相关，
**不据此下结论**）；**`.hmi-test-tmp` 残留无回收机制**（仅记读数）。

## 阶段 81（闭环 §4 **#59**「打开失败的原因那一半是 .NET 原始英文异常消息」—— 诊断码 + 字典 + 机制 **M9**；并修掉 Core 中文台账守卫一处**误分类**）— 2026-09-22

### 81.1 一句话结论

阶段 80 的探针把 9 条坏输入的状态栏原文逐字读了出来，其中**原因那一段有三个毛病**：①⑤ 把 .NET 的
**原始英文异常消息**（① 是 BCL 的 `End of Central Directory record could not be found.`，
⑤ 连 `LineNumber` / `BytePositionInLine` 一起抖出来）透传给用户；文案**不含文件名**；
⑥⑦ 两条不同根因**同一句话**。本阶段闭环**第一个**（码化），另两个如实转 **#60**。

落地：Core 交「**码 + 实参 + 中文回落原文**」三件东西（7 个诊断码 + `HmiPackageException`，
`Unpack` 的七条前置校验失败全部码化），App 侧把「码 ⇒ `package.issue.<码小写>` ⇒ 字典」收成唯一出口
（`ExternalText.FailureReason`）；两本字典各 +7 键；新增判据 **5 条 + 1 条自检**（含 4 组内嵌负控与同场正控）；
可达机制新增 **M9**（面 7）。专题 `docs/2026-09-22-package-failure-codes.md`；登记表 §38。

### 81.2 与阶段 80 的分工（同一片区域的两半）

| | 阶段 80（§37） | 阶段 81（本阶段） |
|---|---|---|
| 缺口的哪一半 | 「**报失败且什么都不变**」—— 失败路径的行为面 | 「**原因**」—— 那条消息本身 |
| 落在哪层 | App 层（判据 3 条）/ Core 层（判据 1 条） | 码化在 Core、渲染在 App（判据 5 条 + 1 自检） |
| 产品改动 | 一个**真缺陷**（失败的打开攒空工作目录） | 七条失败路径的**异常类型与文案来源** |
| 差集 | — | 阶段 80 的 6 组负控**全部重跑**（见 81.6） |

### 81.3 为什么是「码 + 实参 + 中文回落」而不是「让 Core 查字典」

Core 是库、**不引本地化**（依赖方向 App → Core）⇒ 它不可能自己查字典。照抄既有约定（阶段 70 的
`AiIssueCodes`、`LuaSyntaxValidator` 的 `LUA_SYNTAX_*`）：**产出方给码 + 实参 + 中文回落，App 层查字典**。
四条设计决定各自有实测理由：

1. 码必须是 `public const string` —— 守卫靠**反射**枚举它做「声明的码 ↔ 真抛得出的码」**双向差集**，
   反射要 `IsLiteral && FieldType == typeof(string)`；
2. **回落原文留在 Core** —— 字典缺键时宁可显示中文原文，也不显示一句丢掉全部信息的"操作失败"；
3. `HmiPackageException` 只能继承 `Exception` —— `InvalidDataException` 在 .NET 里是 **sealed**
   （实测 `CS0509`，且全仓**没有**任何 `catch (InvalidDataException)` 落在解包路径上）；
   `Assert.Throws<T>` 是**精确**匹配 ⇒ 既有 `Unpack_NonZip_Throws` 必须改断言；
4. `OpenZip` / `ReadJsonEntry` **各包一层** —— ① 的异常是 BCL 抛的、⑤ 的是 `System.Text.Json` 抛的，
   **不是我们抛的**；`ReadJsonEntry` 还要区分「条目不存在 ⇒ null」与「内容坏 JSON ⇒ 码化异常」。

**两条刻意不码化的路径**（写在类注释里，是诚实边界不是遗漏）：`targetAssetDirectory` 为空白
（编程错误，仍是 `ArgumentException`）、`Directory.CreateDirectory` **之后**的写盘失败
（没有修法也没有判据 —— 码化它等于给一条没人验的分支假造"已覆盖"的外观）。

### 81.4 判据：输入全部来自**真实触发**

`PackageIssueLocalizationGuardTests` 的核心约束是**不写"预期 Code"清单**（那等于把实现抄一遍，
抄出来一旦与实现分家，守卫检查的就是清单而不是代码）⇒ `Probe()` 真跑 `HmiPackageService.Unpack`
（7 条坏包 × `MemoryStream`）把 `Code` / `Args` / `Message` 读回来。5 条判据 + 1 条自检：

| # | 钉的东西 | 为什么不能省 |
|---|---|---|
| 1 | 每个真码的键在两本字典里都能解析 | 缺键 ⇒ 英文界面显示中文 |
| 2 | 模板占位符个数 == `Args` 个数 | `Format` 不符时**不抛异常、只原样返回** ⇒ 用户看到 `{1}` 字面量 |
| 3 | zh 模板渲染 == Core 回落原文（**逐字**） | "两份文案同步"的自动判据 |
| 4 | 渲染结果不含 .NET 原文**记号** | **#59 的反回归本身**（`Central Directory` / `is an invalid start of` / `LineNumber` / `BytePositionInLine` / `System.Text.Json`，五个记号都是阶段 80 探针实测到的原文，不是猜的） |
| 5 | `FailureReason` **真的查了字典** | zh 值与回落原文按设计**逐字相同** ⇒「渲染出来是中文」无法区分"查到字典"与"回落原文" ⇒ 用**毒化回落** `<<回落未被使用>>` 分辨 |
| 自检 | 声明的码都能触发 + 触发的都是码化异常 | 死码 / 漏登记双向 |

**M9 为什么必须有独立机制**：新键的模板**带占位符**，而 M6 的「整键字面量」扫描**原理上看不见**它们
（源码里只有 `PackageIssueKeyPrefix = "package.issue."` 这个**前缀**）；且 `Unpack` 在 Core 层，
而 `HardcodedLocalizationGuardTests` 的扫描面只有 `src/HmiEditor.App`。取数方式**复用**
`PackageIssueLocalizationGuardTests.DeclaredCodes()`（同 M8 的理由：抄两份必然漂移，
而漂移的表现是"一个守卫绿、另一个红"）。

### 81.5 `Hit` 的 `!` 前缀约定（判红才看得见的那条腿）

读数里 `Code` 以 `!` 开头表示"抛出来的**不是**码化异常"（值是 `!` + 异常类型名）。它不是码 ⇒
任何按码查字典的判据都会红，而失败消息里**能直接看见真实类型名** —— 比"静默漏掉一条"强得多。

### 81.6 三处负控：本阶段的 5 组 + **M9 自己的 1 组** + 阶段 80 六组**重跑**

本阶段 5 组（`nc81.py`，逐条还原 SHA256 一致）：NC1 改回 `ex.Message`（App 渲染腿）/
**NC2 非 ZIP 改回抛 `InvalidDataException`（Core 码化腿）**/ NC3 字典删键（字典腿）/
NC4 码与文案错配（一致性腿）/ NC5 造死码（自检腿）—— 判红谓词逐条命中。

⚠ **NC2 暴露「顺序本身是判据的一部分」**：自检第一版把「声明了触发不到」写在「抛出了非码化异常」
**前面** ⇒ 注入 NC2 时先命中的是"`ZIP_UNREADABLE` 触发不到"，方向对、**指认不准**
（真正原因在上游：那条路径不再码化了）⇒ 驱动判 `wrong-leg`。修法是把「抛出了非码化异常」提前，
**并把顺序写进注释**。

**M9 自己的负控**（`nc81c.py`）：摘掉 M9 那一块 ⇒ **2 条判据红**，错误消息里 **7/7** 个
`package.issue.*` 键被逐条点名 ⇒ 同时证明 ① M9 是**承重**的；② 那 7 条键**没有**被 M1–M8 隐形担保
（否则摘掉 M9 它们仍会被别的机制捞住、判据不红）—— 后者正是 **#49** 那个病。

**阶段 80 的六组重跑**（产品换了异常类型、判据文件也动过 ⇒ 必须重验）：`nc80.py` **5/5**
（NC1 最近工程腿 / NC2 保存目标腿 / NC3 形状腿 / NC4 工作区腿 / NC5 原因腿，各「失败 1 / 通过 2」，
期望串逐条命中）、`nc80b.py` 的 NC6 **两层都要红**（Core 腿「目标目录被建出来了」失败 1 / 通过 0；
App 腿「失败的打开留下了工作区目录」失败 1 / 通过 2），还原全部逐字节一致。
落盘 `.workbuddy/tmp/s81-nc80-rerun.txt`（`00:17:08` / `00:18:49`）。

### 81.7 🩸 门禁比我先发现的：Core 中文台账守卫的 **DevMessage 误分类**

**第一次提交前门禁就红了**（在我"本地已跑绿"之后）：`Core.Tests` 2117/2118，
`CoreVisibleTextInventoryGuardTests.NoUnclassifiedCoreChinese` 报
`+ Serialization/HmiPackageService.cs: 登记 0，实扫 2`。

不猜、先量 —— 复刻守卫的扫描口径逐条打印判定，量出真实形状：那个文件里**8 条同族「回落文案」，
只因写法不同被分到两个类别**：写成 `ReadJsonEntry(…, "中文")` 的**实参位** ⇒ 计入（2 条）；
写成 `?? throw new HmiPackageException(CODE, "中文")` ⇒ 被 `DevMessage` 的 `throw new` **豁免**（6 条）。
**病根**：`DevMessage` 那一类的理由写着「面向开发者，翻译后反而无法搜索」，而
`HmiPackageException.Message` **按设计是面向用户的回落文案** ⇒ 逻辑上不属于这一类。后果不是"少算几条"，
而是**台账数字会随纯重构漂移**（谁把 `throw` 抽成 helper，数字就自己变）。

**处置**（按守卫自己的文档「若确认某条属于上面的排除类别，应改进分类规则而不是调数字」）：
新增优先级**更高**的规则 `UserFacingException`（`HmiPackageException`）—— 命中它**先判计入**，
再走原来的四类豁免；台账登记 **7**（另一条 `throw new ArgumentException("资产目录不能为空")`
是**编程错误**，照旧留在豁免里）。新增自检 `IsExcluded_DoesNotSwallowUserFacingExceptionFallbacks`，
**两侧都断言**（`HmiPackageException` 的回落文案写在 `throw` 里、写进 `new` 里两种写法都要计入；
`ArgumentException` / `InvalidOperationException` 照旧豁免 —— 否则这条规则会被"看着像异常就放过"满足）。
**副作用面已量化**：全仓引用该类型的只有 `HmiPackageService.cs` ⇒ 只有那一个文件的计数会变。

该规则的负控 **5 组**（`nc81b.py`）—— 含一对**成对腿**：**NC-B**（把 `throw` 抽成 helper
= **正是造成误分类的那个形状** + 新规则）必**绿**、**NC-B'**（**同一注入** + 回退新规则）必**红**
（台账 `登记 7，实扫 3`）⇒ 证明新规则堵的是那个漂移洞，而不是"换个数字凑对"。
NC-A（整块回退 ⇒ 红 `登记 7，实扫 2`）/ NC-C（注入第 8 条回落 ⇒ 红 `实扫 8`）/
NC-D（注入一条开发者消息中文 ⇒ **必须仍绿**）补齐四条腿。还原全部逐字节一致。

⚠ **驱动器自身的一处纠错**：第一版判红谓词写成「stdout 里没有 `已通过!`」⇒ 三个真红被判成 `unknown`
（实际红行的形状是 `  失败 <用例名>`，**没有冒号**；带冒号的是汇总行 `失败!  - 失败: N`）。
按本项目纪律**认不出必须算 `unknown`、不算成立** ⇒ 修好解析口径后**重跑**，5/5 才是证据。

### 81.8 九条机制的面（实测）

M1 **650** / M2 1113 / M3 26 / M4 30 / M5 19 / M6 **386** / M7 12 / M8 41 / **M9 7**。
M6 配套：残差键 **391** / 判据口径 **386** / 旧口径（原文语料）**387** / 注释洞收益 **1**（`param.baud.rate`）。

⚠ 上一轮记过一句「HEAD 对照：M1 649 / M6 387」，本轮复查发现**那份 HEAD 读数从未落盘**
（`HMI_READINGS_OUT` 当时用了**相对**路径 ⇒ 文件没生成）⇒ **那个对照不作数，不再引用**。
替代取证是 M9 自己的负控（81.6）：摘掉 M9 后那 7 条键被逐条点名 ⇒ 它们**只**由 M9 担保 ——
这比"两个面的数字比对"更直接。

### 81.9 门禁与验证

| 项 | 读数 |
|---|---|
| 提交前**第一次** | `s81-gate1.log`，**00:07:54** ⇒ **红**（Core 2117/2118，即 81.7） |
| 提交前**第二次** | `s81-gate1b.log`，**00:13:37** ⇒ **7 步全绿 / `GATE_EXIT=0`**、0 警告 0 错误 |
| 提交 | **`7fed841`**（13 files，**+820 / −30**，`2026-09-23T00:14:31+08:00`） |
| 提交后 | `s81-gate2.log`，**00:15:50** ⇒ **晚于提交** ✓（`GATE_EXIT=0` / 7 步全绿） |
| 对拍 | 两次汇总**各 26 行**（`cmp81-pre.txt` vs `cmp81-post.txt`）：**25 行逐字相同**，唯一差异是路径卫生扫描面 `已检查 638 个文件 → 640` —— 该步按 `git ls-files 'src/*'` 取面（`tests/` 有意排除），提交把**两个新的 `src/*.cs`** 纳入面内 ⇒ **差异完全解释** |

逐程序集（两次相同）：Hardware 44 / Simulator 44 / Communication 75 / Device 87 / RenderTests 4 /
AI 315 / **Core 2119** / **App.UiTests 433**；独占段 Core 8 + App.UiTests 2；固件 host 测试 9 段全过。
阶段 80 → 81 的计数变化两条都**正好等于新增判据条数**：Core 2118 → **2119**（+1 =
台账守卫新自检）、App.UiTests 426 → **433**（+7 = `PackageIssueLocalizationGuardTests`）。
`MainWindow.axaml.cs` 行数 **3013**（两处改动净 0 行，基线不动）。

### 81.10 未闭环

**#60**（新登记）打开失败文案**不含文件名**，且两条打开路径的失败模板**本来就分叉**
（`ui.op.failed.detail` 的 `打开工程失败: {1}` vs `status.open.failed` 的 `打开失败: {0}`）；
⑥⑦ 两条旧格式失败仍是同一句话（属同一粒度问题、且在 App 层不在本阶段 Core 码化面内）；
未码化的两条路径仍回落 `ex.Message`；**端到端可见文案没有再单独读一遍** ——
本阶段给的是「字典模板 + 实测实参」的渲染值，阶段 80 那条端到端判据仍**只断言结构**（刻意）；
「解包**中途**失败留半填充目录」（`CreateDirectory` 之后）仍无修法无判据。

## 阶段 82（闭环 §4 **#60**：打开失败的文案带上文件名 + 两条分叉的模板**合模** —— 并量出第三件 **60-C**「1168 绕过码化出口」）— 2026-09-23

### 82.1 一句话结论

登记时写的 #60 是**两件**，量的那一遍量出**三件**：**60-A** 两条模板都**没有文件名槽位**；
**60-B** 两条打开路径的模板**本来就分叉**（zh 下 7/7 措辞不同）；**60-C**（本阶段新发现）
`MainWindow.axaml.cs:1168` **绕过**阶段 81 刚建好的码化出口、把 `ex.Message` 直接交给模板
⇒ **英文界面下原因那一半 7/7 中英混排**。第三件正是阶段 81 那次收口**漏掉的一处**。
三件一起收：**一个 3 实参出口 + 键删净 + 4 条判据 + 3 组整块回退负控**。

### 82.2 量与设计：两个先验判断被**实测**推翻，一条边界被**编译器**封死

| # | 先验判断 | 实测 | 为什么错 |
|---|---|---|---|
| 1 | 2 实参的 `FORMAT_VERSION_UNSUPPORTED` 会在 1168 路上原样吐 `{0}/{1}` | **0 条泄漏** | Core 的回落原文是**插值好**的（`HmiPackageService.cs:217`）⇒ 真后果是「**永远显示中文**」，不是「漏占位符」 |
| 2 | 阶段 80 那条端到端判据的断言面必须跟着改 | **首跑即绿** | 它钉的是**结构**（前缀 + 原因非空），本次修法**保留了前缀** ⇒ 对「文案里有没有文件名」完全不敏感（负控 NC-A 复现） |
| — | `IStorageProvider` 可以做个桩把「选文件」驱动起来 | **不可实现** | 整族被标成「非客户端可实现」（`CS0535` + 编译器原文），替代基类 `NoopStorageProvider` 是 `internal`（`CS0122`）⇒ **硬边界，不是基建欠缺** |

### 82.3 处置（产品 5 文件，**+36 / −8**）

| 文件 | 改动 | numstat（增/删） |
|---|---|---|
| `Core/Resources/Strings.zh-CN.json` / `Strings.en-US.json` | **−1** `status.open.failed`、**+1** `ui.op.failed.detail.file`（`{0}失败: {1} —— {2}` / `{0} failed: {1} — {2}`） | 1 / 1 各 |
| `App/Resources/Lang.cs` | **−`StatusOpenFailed`**、**+`UiOpFailedDetailFile`** | 1 / 1 |
| `App/Services/UiOperationGuard.cs` | 新增 3 实参出口 `FailureDetail(操作名, 目标路径?, 异常)`（路径 `null` 时回落 2 实参版） | **28 / 0** |
| `App/MainWindow.axaml.cs` | 1008 / 1168 改走新出口；991 的外层 catch 补路径；`ReportOperationFailure` 增可选 `targetPath` | 5 / 5（**净 0 行**，仍 **3013**） |

**文件名独立占一个实参**（不拼进原因串）：拼进去会绕过 `LocalizedFormatCallSiteTests` 的 **C1**
（`.Format` 实参个数 == 模板占位符个数）⇒ 那条判据就**再也看不见它**。
**删键五步同生共死** —— `status.open.failed` 原先**只靠 M6** 担保（1168 用的是**字面量**、不是常量）：
① 1168 的字面量消失 → ② 两本字典各删 1 键 → ③ `Lang` 常量删掉 → ④ `UnusedBaseline` **1124 → 1123**
→ ⑤ 分桶 **M6 273 → 272**。⚠ 新增的 `UiOpFailedDetailFile` **有代码引用** ⇒ 不进该基线，
所以可达性面 **M1 650 → 651**、**M6 386 → 385** 而常量基线反而降 1 —— 不矛盾：
前者数「**有**代码引用的常量」，后者数「**无**代码引用的常量」。

`MainWindow` 净 0 行是**被逼出来的**：`SingleFileSizeGuardTests` 是**相等判定**（`tests/HmiEditor.Core.Tests/SingleFileSizeGuardTests.cs:36` = 3013），
而 §26.5 的既有裁定是「**压回行数、不调大基线**」。第一版把设计说明写成它的 `<param>` 块（**+9 行**）
⇒ 直接撞线；改成把可选参数的语义**下沉到出口那层**（`UiOperationGuard` 没有行数守卫）后归零。

### 82.4 判据 4 条（两文件）

| # | 判据 | 形状 | 单独能看见 |
|---|---|---|---|
| **J1** | `OpenFailureFilePathUiTests.EveryBadPackage_ShowsTheTargetFilePath_EndToEnd` | **端到端**（路径 1）：7 条真实坏包落成真实文件 → `StartWithOpenFile` → 状态栏**逐字比对** | 60-A + 60-B |
| **J2** | `…Outlet_KeepsPathAndLocalizedReason_InBothLanguages` | **出口 × 双语**：7 码 × {zh,en} 逐字比对 + en 下**不含汉字** + `ex.Message` **必须含汉字**（**成对**断言） | 60-A + 60-C（**路径 2 唯一的替代证据**） |
| **J4** | `…DivergentOpenFailureTemplate_IsGone` | 结构：`status.open.failed` 不得出现在字典或 `Lang` | 60-B 不得回流 |
| **J3** | `ExceptionMessagePassthroughFaceTests`（3 条：面 / 自检 / 负控） | **源码面**：**37 站点 / 27 组合**，**双向**相等判定（新增即红、**减少也必须同步**）；面＝**全 `src/`**（12 项目 / 381 `.cs`，2026-09-23 收口，见 §82.12） | 60-C 的**根因**，且**不依赖任何行为入口** |

**期望值一律不从被测对象来**：J1 的期望只用**字典模板 + `string.Format`** 拼（不调 `FailureDetail`，
用被测对象算期望等于把判据写成恒真）；驱动前把状态栏置成**唯一标记**（每次"上弦"），
否则「状态栏里有这个路径」可能只是上一轮的残留。J2 的**成对**断言是必需的：
只断「en 渲染不含汉字」会被「**夹具本身就没中文**」满足。

### 82.5 负控：三组**整块回退**，感应集合**两两不同**

| 注入 | J1 | J2 | J4 | J3 | **阶段 80 端到端** |
|---|---|---|---|---|---|
| **NC-A** 路径缺失（两条打开路径改回不带 `targetPath` 的出口） | **红** | 绿 | 绿 | 绿 | **绿** |
| **NC-B** 绕过码化出口（出口把 `FailureReason(ex)` 换成 `ex.Message`） | 绿 | **红** | 绿 | **红** | 绿 |
| **NC-C** 合模回流（`status.open.failed` 装回两本字典 + `Lang`） | 绿 | 绿 | **红**（+ 常量基线红） | 绿 | 绿 |

还原逐字节一致（`sha256`）。**两条最重的证据都是"反面"读出来的**：
**NC-A 下阶段 80 端到端照样绿** ⇒ 实测证明「**旧那一层原理上看不见 60-A**」（与阶段 79 的 NC5 同款构造）；
**NC-B 下 J1 照样绿** ⇒ zh 下 `ex.Message` 与 zh 字典值**按设计逐字相同**（阶段 81 的第三条判据钉的就是这件事）
⇒ **端到端判据在 zh 界面上无法区分「走了字典」与「回落了原文」** —— 60-C 必须由 J2（英文）与 J3（源码面）分别守。

### 82.6 🩸 两处「判据被**自己的负控**修好」（本阶段的方法学产出）

1. **J3 的 NC3 抓出判定口径的洞**：第一版用「首实参是不是**双引号包起来**的」判「是不是键」
   ⇒ 把 `string.Format("{0}", ex.Message)` 里的 `"{0}"` **当成了字典键**、污染了面。
   修法不是"再写一条更严的判定"，而是**复用** `LocalizedFormatCallSiteTests` 的形状规则 `LooksLikeKey`
   （本阶段把它从 `private` 开放为 `internal`）—— **另写一份会对同一个字面量给出不同结论**。
2. **夹具只此一份**：`PackageIssueLocalizationGuardTests.BadPackages()` 同样开放为 `internal`，
   理由是**另抄一份的后果是两条判据在测两批不同的坏包**。
3. **基线刻意不逐条编理由**：J3 的 27 条若逐条写"为什么还没迁"，就是**伪造精度**
   （参见 #54 的教训：豁免表里没人核的"理由"会把缺陷合法化）。这里的数字化**可复跑** ——
   面一涨就红，红了就该走 `FailureReason` 而不是往表里加一行。

### 82.7 读数（实测，非估）

| 项 | 值 |
|---|---|
| 站点面 | **迁移前 38 站点 / 28 组合 → 迁移后 37 / 27**，差集**恰好一条**（`MainWindow.axaml.cs:1168 · status.open.failed`），反方向 **0 条** |
| 交叉核对 | python 探针 37/27 与 C# 守卫的 37/27 **逐条吻合**（探针未排除 `obj/**/*.g.cs` 这 9 个生成文件，读数不变 ⇒ 生成物里不含这类站点） |
| 机制面 | M1 **650 → 651** / M6 **386 → 385**；其余不动：M2 1113 / M3 26 / M4 30 / M5 19 / M7 12 / M8 41 / M9 7 |
| M6 配套 | 残差键 **391 → 390** / 判据口径 **386 → 385** / 旧口径 **387 → 386** / 注释洞收益 **1**（`param.baud.rate`） |
| 常量基线 | `UnusedBaseline` **1124 → 1123**；分桶 M2 828 / **M6 273 → 272** / M3 18 |
| 字典 | 各 **2182**（两本集合相等）；`status.open.failed` **0 残留**、`ui.op.failed.detail.file` 两本都在 |
| 行数 | `MainWindow.axaml.cs` **3013**（净 0 行，基线不动） |

### 82.8 门禁与验证

| 项 | 读数 |
|---|---|
| 提交前 | `.workbuddy/tmp/s82-gate1.log`，**00:44:31** ⇒ `GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| 时序（提交前） | 门禁 mtime **晚于**最后一次源码改动（`Lang.cs` + 两本字典 `00:42:37`）**1 分 54 秒** ⇒ 覆盖了本阶段**全部**源码改动（不是"改完之前跑的那一次"） |
| 提交 | **`d764e23`**（10 files，**+614 / −16**，`2026-09-23T01:06:25+08:00`）—— 代码 + 判据一批 |
| 提交后 | `.workbuddy/tmp/s82-gate2.log`，**01:07:48** ⇒ **晚于提交 1 分 23 秒** ✓（`GATE_EXIT=0` / 7 步全绿） |
| 对拍 | 两次汇总**各 67 行**读数（剥墙钟 + `sort` + 逐行 diff，带行号与去行号两口径）⇒ **0 差异**。比阶段 81 更干净（那次 1 行差异来自路径卫生扫描面随提交变化；本次两跑都在同一批源码上） |
| 文档批提交 | **`802d1b7`**（3 files，**+504 / −1**，`2026-09-23T01:08:45+08:00`）—— 登记表 §39 + 本文件 + 专题文档 |
| 第三次门禁 | `.workbuddy/tmp/s82-gate3.log`，**01:10:05** ⇒ **晚于文档批提交 1 分 20 秒** ✓（`GATE_EXIT=0` / 7 步全绿） |
| 对拍（gate2 vs gate3） | 读数**各 67 行**、**去行号口径 0 差异**；带行号口径 **66/67** 逐字相同，唯一差异是 `[5]` 段在 gate3 日志里整体行号 **+1**（日志多一行）⇒ **读数内容一字未变** —— 「纯文档提交不该动任何读数」的验证 |

逐程序集（两次相同）：Hardware 44 / Simulator 44 / Communication 75 / Device 87 / RenderTests 4 /
AI 315 / **Core 2119** / **App.UiTests 439**；独占段 Core 8 + App.UiTests 2；固件 host 测试全过。
三段之和 = 全量 **3137** 通过 / 0 失败。计数变化**正好等于新增判据条数**：App.UiTests 433 → **439**（+6）。
`RLIMIT_AS 硬限: unlimited`（§5 的「新路径资源」阻断项本次不存在）。
专题文档 `docs/2026-09-22-open-failure-file-path.md`（含 82.1 缺口形状 / 82.8 负控矩阵 / 82.9 硬边界与两次编译错误原文）。

### 82.9 未闭环

1. **新增登记 #61**：异常原文直通模板**还有 37 处 / 27 组合**（保存 / 导出 / 编译 / 下载 / AI 生成 /
   资源导入 / 登录 / 审计校验…），判据已就位（**双向**相等判定），本阶段**刻意只迁 1 处**（1168）——
   一次只放开一个变量，且剩下的是 **27 个文件的改动面**。迁移方向明确且便宜：`.Message` → `ExternalText.FailureReason(ex)`。
2. **⑥⑦ 仍是同一句话**（旧格式非 JSON / 内容是 JSON `null`）—— 根因不同、措辞相同；
   修法要区分 `ProjectSerializer.Deserialize` 的两种 `null`（属 **Core API 改动**），不在本阶段面内。
3. **`OpenProject` 的外层 catch 走不带路径的模板**是**正确**表达（选择器可能还没返回，结构上拿不到路径），
   **不是残余** —— 已写在出口的注释里。
4. ✅ **已闭环（2026-09-23，见 §82.12）**：J3 的面原只覆盖 `src/HmiEditor.App`（Core / AI 的
   `Format(键, ex.Message)` 不在面内；当时无已知缺口，但那是**面的边界**）⇒ 面改从 **`src/` 全量**取
   （12 项目 / 381 `.cs`），并新增自检「面必须覆盖每一个含 .cs 的项目」。实测**基线不变**（仍 37/27）；
   负控（面收窄回 App）⇒ 自检红、基线绿。

### 82.12 补记（2026-09-23）：J3 面收口到全 `src/` + 长期笔记压缩

- **J3 面收口**：`AppSourceScanner.ScanAllProductSources()`（枚举唯一走 `SourceScanner.ProductSources()`）
  + 路径改仓库相对 + 自检「面覆盖每一个含 .cs 的项目」（与另一条枚举器对拍）。
  产品代码 0 行；实测 Core / AI 各 0 处 ⇒ 基线一字未动（37/27）；负控见 `.workbuddy/tmp/nc-j3-face.sh`。
  详 `docs/2026-09-22-open-failure-file-path.md` §82.12。
- **长期笔记压缩**：`.workbuddy/memory/MEMORY.md` **7929 → 7129 字符**（余量 71 → 871；上限 8000 **按字符**）；
  细节析出 `docs/2026-09-23-memory-detail-extract.md`，快照 `docs/archive/2026-09/memory-hmi-full-2026-09-23.md`；
  工具 `.workbuddy/tmp/compact-memory-20260923.py`（默认干跑、`--write` 落盘、整行前缀 assert 唯一、回读校验）。
- 🩸 **顺带修掉一个判据自伤（门禁挂死两次的根因）**：门禁跑到 `App.UiTests` 段时宿主
  futex 阻塞、零输出、`utime` 不涨；`--blame-hang --blame-hang-dump-type none` 点名
  `JudgeReadingsTests.Write_WithNeitherSink_IsSilentAndSafe`。根因**不在被测代码**：
  `TMPDIR` / `HMI_TEST_TMPDIR` 都没设 ⇒ `TestTempDir.Root` 就是 `/tmp`，而 `/tmp` 顶层有
  **211** 个 FIFO/socket（`clr-debug-pipe-*` / `MSBuild*` / `dotnet-diagnostic-*`）；
  `Directory.EnumerateFiles` 把它们当文件返回，`File.ReadAllText` 打开 FIFO **永久阻塞**。
  修法：该用例只读**非空普通文件**（`new FileInfo(f).Length == 0 ⇒ skip`；`stat` 不打开文件、FIFO 的
  `st_size=0`，而空文件含不了 marker ⇒ 语义无损）。负控：往 `/tmp` 顶层放一个非空含 marker 的文件 ⇒
  该条**红**；删掉 ⇒ **7/7 绿 / 60 ms / 不再挂**（`.workbuddy/tmp/jr-fifo-nc.sh`）。
  教训写进技能 `dotnet-test-tempdir-hygiene`。⚠ 门禁的"大队"那一跑没有 per-test 超时 ⇒
  这类挂死只能靠手工 blame-hang 定位（留作下一阶段候选）。
- **门禁六跑**：`s83-gate1/2` **挂死**（App.UiTests 段）⇒ `s83-gate3` 不挂但撞上 **#47 已知间歇红**
  （`DispatcherOwnershipContractTests.CrossDispatch_OnlyTheSessionThreadMayHoldTheUIThread`，≈2/23）
  ⇒ `s83-gate4` **7 步全绿 / `GATE_EXIT=0`**、0 警告 0 错误、三段之和 = 全量 **3137**
  （大队 `App.UiTests` **439**；独占段 Core 8 + App.UiTests 2）⇒ `s83-gate5` 因**运行中改了一行注释**
  自行作废（本项目的规矩：门禁必须晚于最后一次改动）⇒ `s83-gate6` 在**冻结树**上复跑
  **7 步全绿 / `GATE_EXIT=0`**，逐程序集读数与 gate4 相同。挂死现场另存
  `s83-gate1-hang.log`，点名用的 blame 日志 `s83-uitests-blame.log`。
- **提交与提交后门禁**：代码批 **`f1e0f20`**（3 files，**+94 / −36**，`2026-09-23T09:59:28+08:00`）；
  提交后 `s83-gate8.log`（mtime **10:00:37** ⇒ 晚于提交 **1 分 09 秒**）**7 步全绿 / `GATE_EXIT=0`**；
  与冻结树那次（gate7）**剥墙钟 + 排序后逐字相同**（各 **67 行**读数、**0 差异**，
  `.workbuddy/tmp/s83-cmp78.py`；带行号与去行号两口径都是 0）。
- **文档批 + 第三次门禁（验「纯文档提交不改任何读数」）**：**`d24bfe2`**（5 files，**+226 / −7**，
  `10:00:52`）⇒ `s83-gate9.log`（mtime **10:01:55** ⇒ 晚于提交 **1 分 03 秒**）
  **7 步全绿 / `GATE_EXIT=0`**，与 gate8 **剥墙钟 + 排序后逐字相同**（各 **67 行**、**0 差异**）。
- **门禁挂死防护（本轮追加，`scripts/ci-gate.sh`）**：三段测试都挂上
  `--blame-hang --blame-hang-timeout ${TEST_HANG_TIMEOUT:-180s} --blame-hang-dump-type none`
  ⇒ 某用例**静默**超过阈值时 **中止并点名**（「发生崩溃时运行的测试: …」）、退出码非 0，
  不再无限等 —— 本轮那两次挂死若早有它，就是 3 分钟拿到人名，而不是人肉 blame。
  **180s 是量出来的**：267 条样本里最慢单测 **15 s**
  （`HeadlessDispatchContractTests.Pump_NeverRunsWhileADispatchIsInFlight`；渲染类 5~6 s）⇒ 12 倍余量。
  🩸 `--blame-hang-dump-type none` **不能省**：默认会写 dump，而沙箱 `/tmp` 可能只有 10 MB
  ⇒ 把「挂死」换成另一种红、**还是门禁自己造成的**。
  **负控成对**（`.workbuddy/tmp/s83-hangtimeout-nc.sh`；注入 `Thread.Sleep(Timeout.Infinite)`
  的临时用例，跑完即删、工作树干净）：旧口径（原命令 + `timeout 90`）⇒ **`A_EXIT=124`、日志零人名**；
  新口径（`TEST_HANG_TIMEOUT=30s bash scripts/ci-gate.sh`）⇒ **`B_EXIT=1` 且点名
  `NcGateHangScratchTests.HangForever`**。
  **不引入读数噪声**：加装后 `s83-gate10` 与 gate9 **剥墙钟 + 排序后 0 差异**（各 67 行），
  gate10 本身 **7 步全绿 / `GATE_EXIT=0`**。教训写进技能 `dotnet-test-host-hang-triage`（坑 23）。

## 阶段 83（**先证伪再定性**：#61 的修法方向被实测推翻 —— 那 37 处全是「宽捕获」，改名**恒等于零收益**；拆成四个子项并排出优先级）— 2026-09-23

### 83.1 一句话结论

上一阶段给 #61 写下的修法方向是「把 `.Message` 换成 `ExternalText.FailureReason(ex)`」。
本阶段实测**推翻了它**：那 37 处**清一色是宽捕获**（`catch (Exception ex)` **36/37**），
而 `FailureReason` 只在 `ex is HmiPackageException` 上分叉 ⇒ 换过去**只会落进回落分支**：
**用户所见一字不变，面却绿着变空** —— 这是**改名式假闭环**。
⇒ 真收口在**产出方**。本阶段**刻意不改任何行为**，产出的是**一次证伪 + 一张分好类的清单**（含优先级）。

### 83.2 量的那一遍

口径 = 沿用阶段 82 的站点面，**多量两层**：① 往前最近的 `catch (` 类型；② `try` 块里出现的前 4 个
`X.Y(` 调用（产出方）。被测量 = **37 站点 / 27 个 `(文件, 键)` 组合 / 24 个键**（与 J3 基线一致）。
脚本 `.workbuddy/tmp/s83-catch-shape.py` / `s83-producer.py`，读数 `s83-shape.txt` / `s83-producer.txt`。

| 捕获形态 | 站点数 |
|---|---|
| `catch (Exception ex)` —— **宽捕获** | **36 / 37** |
| `catch (ArgumentException ex)` —— 具体类型 | 1（`UserManagerView.axaml.cs:257`） |

族规模：`ai.gen.failed` **8**（3 文件）/ `asset.import.fail` 3 / `audit.export.c.code.failed` 2 /
`compile.failed` 2 / `gen.export.failed` 2 / `status.simulator.export.failed` 2 / **其余 18 键各 1**。
产出方热点（跨站点计数）：`GeneratedCodeValidator.Validate` **4** · `AppPaths.TempSubdir` 4 ·
`File.ReadAllTextAsync` 3 · `ExportConfig.ResolveOutputDirectory` 3 · `Directory.CreateDirectory` 6 ·
`File.WriteAllTextAsync` 5 · `SvgParser.Parse` 2 · `CsvVariableImporter.Parse` 2。

⚠ 口径说明：判据语料在 §82.12 已**收口到全 `src/`**（12 项目 / 381 `.cs`），而本阶段探针**只扫 App**
—— 两边读数**一致**（Core / AI 各 **0** 处）⇒ 37/27 在两种口径下相同。

### 83.3 静态证明：改名**恒等于零收益**（不是"收益小"，是**零**）

三段都在**读代码**就能闭合，不需要实验：① `ExternalText.FailureReason` 只有一个分叉
（`ex is HmiPackageException ? 查字典 : ex.Message`，`ExternalText.cs:138-141`）；
② 站点侧变量是**宽捕获**（36/37）⇒ 唯一能走码化分支的条件是**运行期恰好收到 `HmiPackageException`**；
③ 全仓 `HmiPackageService.Unpack` 的调用点**只有一个**（`MainWindow.axaml.cs:1034`，打开路径，
**阶段 82 已迁走**），而 `Pack`（`:1227` / `:1362`）**不抛**码化异常
（码只在 `Unpack` 的前置校验里，`HmiPackageService.cs:208-233` / `:348`）。
⇒ 这 37 处里**没有一处**今天能收到 `HmiPackageException` ⇒ 改名后**渲染结果与今天逐字节相同**，
面却 **37 → 0**。**「面变小」不等于迁移成功。**

### 83.4 三分类与优先级

⚠ 置信度分两档：标【已读】的是这轮**读了产出方代码**（或产出方名自明到无可争议）的；
标【待读】的**只有启发式产出方** ⇒ **不许当理由表用**（登记 #54 的教训）。

| 类 | 内容 | 站点 | 该动谁 |
|---|---|---|---|
| **A** | 产出方是**本仓库**、语义明确（`GeneratedCodeValidator` / `SvgParser` / `CsvVariableImporter` / `FontSubsetAnalyzer` / AI 服务族） | ~14 | **加码表 + 带码异常**；站点侧**一行不用改** |
| **C** | **BCL / IO 原文** —— 不该本地化，但**该"带上对象"** | ~9 | **复用阶段 82 的出口** `UiOperationGuard.FailureDetail(操作名, 目标路径?, 异常)` + 已存在的 `ui.op.failed.detail.file` ⇒ **零新码表** |
| **B** | 原因是**外部工具输出**（`compile.failed` / `buildrun.log.error` / `download.sharedlibrary.failed`） | 4 | **登记为"不迁"**，理由可核（产出方是外部进程，原文**有用**） |
| **【待读】** | `recipe.clone.failed` / `login.failed` / `sim.reg.read/write.failed` / `comm.test.failed` / `user.mgr.create.failed` / `status.reload.failed` | 7 | **不许定类**；拿启发式排期前**必须先读代码复核** |

**优先级**：C 类（零新码表、判据形状可复用）→ A 类（一次一族；`GeneratedCodeValidator` 与
`SvgParser` / `CsvVariableImporter` 最干净，因为它们本就是**本仓库的结构化失败**而不是异常）→ B 类不迁。
⚠ `ai.gen.failed` 虽是最大族（8 站），但**混着 IO 与 AI 服务两类** ⇒ **先拆再动**。

### 83.5 本阶段落地了什么（**零行为改动**，两处"钉死"）

| 位置 | 改了什么 |
|---|---|
| `App/Services/ExternalText.cs`（`FailureReason` 文档注释） | 新增「**别把『改成调用本方法』当成『本地化』**」+ 上面那条静态证明 + 指向专题 |
| `tests/HmiEditor.App.UiTests/ExceptionMessagePassthroughFaceTests.cs` | ① 类注释：原句「红了就该走 `FailureReason`」**已被实测推翻**，改为"面变小**不等于**迁移成功"；② **断言失败消息**里同一句错误指引改掉 —— 它会把下一个人**直接引向假闭环** |

**为什么不加新判据**：纪律是「**做不出负控的判据不发布**」。现有 J3（双向相等判定）就是正确的绊线 ——
它守"**面不许静默变化**"；而"面变小是不是真迁移"**判据判不了**（要判它得知道产出方有没有加码，
那是**语义**不是**形状**）。⇒ 本阶段把 J3 从**"待迁移清单"重新定性成"定量清单"**，
把"变小不算成功"写进注释与失败消息，而**不是**造一条判不动的断言冒充守卫。

### 83.6 门禁与验证

| 项 | 读数 |
|---|---|
| 提交前 | `.workbuddy/tmp/s83-gate1.log`，mtime **10:19:55** ⇒ `GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| 计数 | 三段之和 = 全量 **3137** 通过 / 0 失败（`Core 2119` / `App.UiTests 439`）—— **与阶段 82 逐项相同**：本期只改注释，测试数**本就不该变** |
| 提交 | **`f286118`**（2 files，**+28 / −9**，`2026-09-23T10:21:49+08:00`）—— 两处注释级钉死 |
| 提交后 | `.workbuddy/tmp/s83t-gate2.log`，mtime **10:23:08** ⇒ **晚于提交 1 分 19 秒** ✓（`GATE_EXIT=0` / 7 步全绿） |
| 对拍（gate1 vs gate2） | 读数**各 67 行**、**去行号口径 0 差异** —— 只改注释 ⇒ 读数**本就不该变** |
| 文档批提交 | **`05eb11a`**（4 files，**+274 / −2**，`2026-09-23T10:23:25+08:00`） |
| 第三次门禁 | `.workbuddy/tmp/s83t-gate3.log`，mtime **10:24:41** ⇒ **晚于文档批提交 1 分 16 秒** ✓（`GATE_EXIT=0` / 7 步全绿） |

专题 `docs/2026-09-23-exception-passthrough-triage.md`（两张表 / 静态证明 / 三分类 /
"C 类的第一个实测项 = 路径怎么取字符串"）。

### 83.7 🩸 证据完整性：一次**撞名覆盖**（如实标注，不掩盖）

`s83-gate1.log` 这个路径**原先**是「阶段 82 补记」那条工作流的提交前门禁日志（09:1x，
**挂死在 App.UiTests 段**，被 `docs/2026-09-22-open-failure-file-path.md` 的"六跑表"引用）。
本阶段的门禁脚本用 `: > "$L"` **先截断**同名文件 ⇒ **原内容不可恢复**（挂死现场另存于
`s83-gate1-hang.log`，09:29，**未受影响**，该表引用的现场仍可核）。
⇒ 处置：① 那道表的第 1 行已就地加注（指向 `s83-gate1-hang.log`）；② 本工作流后续门禁改用
**`s83t-gate*`**（t = triage）避免再撞名；③ 教训 —— **两个工作流并行时，日志名要带工作流前缀**，
而门禁脚本的 `: > "$L"` 是**静默截断**，撞名时不会报错。

### 83.8 未闭环

1. **#61 仍开着**，但已重新定性；登记表 §4 新增 **#61a（A 类）/ #61b（C 类）/ #61c（B 类不迁）/
   #61d（待读）** 四个子项。
2. **A 类与 C 类的落地都没做**（本阶段刻意）；C 类的**路径取法**是它的第一个实测项
   （`RecipeEditorView.axaml.cs:926` 的 `file` 是 `IStorageFile`，其 `Path` 是 `Uri` ⇒ **取法未实测，不许预设**）。
3. **【待读】那 7 个键**没读产出方 ⇒ 它们**不在任何一类里**，不许被当成"已分类"。
4. 表 3 是**启发式**（`login.failed` 只读到 `Task.Run`）⇒ 不能用来定类。

## 阶段 84（**先量再动**：#61b「C 类该带上对象」在量完现场后**被推翻** —— BCL 的 IO 原文**自带目标路径**，再包一层是冗余/负收益 ⇒ **改期**，依赖 #61e）— 2026-09-23

> 承接阶段 83（它把 #61 拆成 #61a/#61b/#61c/#61d 并排出优先级：**C 类打头**）。
> 专题 `docs/2026-09-23-exception-passthrough-triage.md` §84。

### 84.1 一句话结论

从 #61b 开工，**量完就把这个动作本身推翻了**：C 类（**9 站**）用
`FailureDetail(操作名, 目标路径?, 异常)` 把目标文件带给用户，**在可达范围内收益恒为 0** ——
① 产出方全是 BCL / IO，而 **BCL 的 IO 原文自带目标路径**（用户早就在原文里看到了）；
② 码化出口 `FailureReason` 对 BCL 异常**没有码** ⇒ 换过去**正是阶段 83 钉死的「改名式假闭环」**。
⇒ **C 类不改代码，改期**，且**依赖 #61e**（给 BCL 异常类型建码）—— **顺序反了就是零收益**。

### 84.2 量的那一遍

#### (a) 逐处读现场：目标路径在不在 `catch` 的作用域内（11 站实测）

| 档 | 站数 | 站点（实测形状） |
|---|---|---|
| ① 现成在作用域 | **5** | `MainWindow:1265`（`path` 是 `SaveToPath` 的**参数**）/ `BuildPipelineService:114`·`115`·`206`（`targetDir` 在 try 之外；⚠ `outputPath` **在 try 内** ⇒ 只能给**目录**）/ `RecipeEditorView:926`（`file` 声明在 try **之前**，`if (file == null) return;` 也在之前） |
| ② 可重取 | **3** | `RecipeEditorView:891`、`AssetManagerView:286`·`357`（`files` 在 try 之外、`path` 在 try 内 ⇒ 可重取 `files[0]`） |
| ③ 结构上取不到 | **2** | `CommunicationConfigView:1060`（`file` 在 try 内，要拿得在 try 外多一个局部承接）/ `AssetManagerView:221`（`path` 在 **foreach 内**、catch 在圈外 ⇒ **批量档天然没有"单一路径"**） |
| **分类被证伪** | **1** | `MainWindow:549`（`status.reload.failed`）—— 它**连文件都没有**：那是 Dock **布局重组**失败（try 里只有一次字典查表 + 两次 `Dispatcher.Post`） |

⇒ 阶段 83 留的「**第一个实测项**」（`IStorageFile` 怎么取字符串）**答案不是问题**：
仓库既定写法是 **`file.Path.LocalPath`**（`AssetManagerView:186/259/338`、`FontSubsetView:84`、
`VariableImportView:58`、`CommunicationConfigView:1001/1055`、`MainWindow:1161/1204/1587` 等 8+ 处先例）。
真正的问题是**值本来就到不了用户手上**（84.3）。

另两条**分类修正**：`gen.import.failed` 被阶段 83 归进 A 类，但它的 `catch` **同时包着 IO
（`IO.StreamReader`）与模型调用（`RecipeManager.ImportCsv`）** ⇒ **A/C 分档在"站点"这一层做不到**，
只能在"产出方"那一层分。

#### (b) 探针实测 BCL 的 IO 原文（`tools/probe-bcl-io-messages.cs`，6 场景）

| 场景 | 异常 | `Message` | **自带目标路径?** |
|---|---|---|---|
| `File.Create` 撞已存在的**目录**名（= `SaveToPath` 的 `path + ".tmp"`） | `UnauthorizedAccessException` | `Access to the path '/tmp/probe84/demo.hmi.tmp' is denied.` | **True** |
| `File.WriteAllTextAsync` 目标目录不存在 | `DirectoryNotFoundException` | `Could not find a part of the path '/tmp/probe84/nope/out.csv'.` | **True** |
| `WriteAllText` / `ReadAllTextAsync` / `new StreamWriter` 目标是目录 | `UnauthorizedAccessException` | `Access to the path '…' is denied.` | **True** |
| `Path.Combine` 含 `\0` 的路径 | **不抛** | — | — |
| `CurrentUICulture` 切 `zh-CN` / `en-US` | — | **消息一字不变**（都是英文） | ⇒ 这就是 **#59** 的原形状 |

#### (c) 唯一非 `File.*` 家族的产出方**在 headless 里不可实测**

`asset.import.fail` 的图片批量档产出方含 `new Avalonia.Media.Imaging.Bitmap(path)`。
临时测试实测（跑完即删）：**文件不存在 / 内容是文本 / 空文件 / 截断 PNG / 目标是目录 / 乃至真实的
4×4 PNG（python 现造 74 B）—— 全部不抛，`PixelSize` 恒 `(1,1)`**。真实 PNG 也是 `(1,1)`
⇒ **headless 的解码器是桩**（不是"宽容"）⇒ 该档**不可实测**；
且它**结构上拿不到单一路径**（③档）⇒ **即使真机上它不带路径，收益也拿不到**。

### 84.3 为什么是"收益恒为 0"（三条，各自可核）

1. **冗余**：原文已含路径 ⇒ 3 实参出口的 `{1}` 与 `{2}` 里是同一条路径。最坏的是 `SaveToPath`：
   `{1}` = `…/demo.hmi`，而 `{2}` 里的是 `…/demo.hmi.tmp` ⇒ 用户看到**两个不同的路径**，
   其中一个是**中间文件** ⇒ **比不带更难读**。
2. **码化恒等于改名**：`FailureReason` 只在 `ex is HmiPackageException` 上分叉（阶段 83 已证），
   BCL 异常没有码 ⇒ 换过去**只落回落分支**，用户所见一字不变。
3. **唯一可能受益的档拿不到对象**（见 84.2(c)）。

### 84.4 本阶段落地了什么（**零行为改动**）

1. **`UiOperationGuard.FailureDetail(3 实参)` 的注释新增「但它不是万能出口」段** —— 钉在这里是因为
   **下一个人看到这个出口的第一反应就是"C 类那几处也该用它"**；段里写明实测结论、为什么负收益、
   以及**顺序（#61e 先）**。
2. **J3 站点面守卫的类注释新增三档处置段**：原文只写「其余…属另一个议题，已登记为 #61」——
   读者会以为 C 类**等着人去迁**。现在写明 A 可码化 / **C 改期** / B 不迁。
3. **探针进仓库**：`tools/probe-bcl-io-messages.cs`（`dotnet run tools/probe-bcl-io-messages.cs`
   即可复跑；用 .NET 10 file-based app ⇒ **不需要 csproj、不往仓库落 `obj/`**，
   符合既有 `tools/probe-*` 约定，如被 `LuaDialectTests` 引用的 `tools/probe-lua-dialect.lua`）。

**为什么不加新判据**：唯一候选是「`FailureReason` 不许给 BCL 类型分叉」—— 它**会拦住 #61e
这个正确的改动** ⇒ 属「**判据别量自己不该管的量**」（阶段 55 的教训）⇒ **主动否掉**。
而「变小的面算不算真迁移」判据判不了（要判它得知道产出方有没有加码 —— 那是语义不是形状）。
J3 的双向相等判定继续守「**面不许静默变化**」。

### 84.5 门禁与验证

| 项 | 读数 |
|---|---|
| 提交前（第一次，**作废**） | `.workbuddy/tmp/s84t-gate1.log`，mtime **10:54:25** ⇒ `GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| 为什么作废 | 跑完后发现 `UiOperationGuard.cs` 注释里的计数写成「约 10 处」，实测是 **9 站** ⇒ 就地改准（**纯注释**）⇒ **重跑**。两次日志**都留存、不覆盖**（阶段 83 的撞名教训） |
| 提交前（**有效**） | `.workbuddy/tmp/s84t-gate1b.log`，mtime **10:55:56** ⇒ **晚于末次源码改动 `UiOperationGuard.cs` 10:54:39 共 1 分 17 秒** ✓ / `GATE_EXIT=0` |
| 计数 | 三段之和 = 全量 **3137** 通过 / 0 失败（`Core 2119` / `App.UiTests 439`）—— **与阶段 82 / 83 逐项相同**：只改注释 + 加一个**未进任何 csproj** 的探针，测试数**本就不该变** |
| 对拍 gate1 vs gate1b | **读数部分 0 差异**（差异只在 gate1b 多出的表头块 + 登记表在两次之间被编辑） |
| 代码批提交 | **`b3e03a2`**（3 files，**+146 / −0**，`2026-09-23T10:56:12+08:00`） |
| 提交后 | `.workbuddy/tmp/s84t-gate2.log`，mtime **10:57:45** ⇒ **晚于提交 1 分 33 秒** ✓ / `GATE_EXIT=0` / 7 步全绿 / 0 警告 |
| 对拍 gate1b vs gate2 | 唯一差异 = **路径卫生「已检查 640 → 641 个文件」** —— 这正是探针从"未跟踪"变"已跟踪"的**预期结果**（可解释，非读数异常）；测试数/步骤结果逐项相同 |
| 文档批提交 | **`47fe2e8`**（3 files，**+263 / −3**，`2026-09-23T10:58:28+08:00`）—— 专题 §84 + 登记表（#61b 改期 / 新增 #61e）+ 本节 |
| 第三次门禁 | `.workbuddy/tmp/s84t-gate3.log`，mtime **10:59:49** ⇒ **晚于文档批提交 1 分 21 秒** ✓ / `GATE_EXIT=0` / 7 步全绿 / 0 警告；**工作树干净** |
| 对拍 gate2 vs gate3 | **读数 0 差异** —— 唯一差异是 gate2 运行期工作树里有两条未提交的 `M docs/…`（gate3 时已提交） |

⚠ **一次自纠（登记在案）**：第一次提交带了 `--no-verify`。事后核过 —— 本仓库 `.git/hooks/`
下**只有 `.sample`**、`core.hooksPath` 未设置 ⇒ **没有任何钩子**，该旗标是**空操作**
（不是"跳过了检查"）。既然无钩子可跳，后续提交**一律不带**它。

### 84.6 未闭环

1. **#61b 改期**（依赖 #61e）—— 登记表已按此改写；**新增 #61e**「给 BCL 异常类型建码」= 真收口方向。
2. **`status.reload.failed` 的分类被证伪** ⇒ 从 C 类移出、退回 **#61d【待读】**（该归哪一档还没定）。
3. **`gen.import.failed` / `comm.slave.var.import.failed` 的 A/C 混档**：一个 `catch` 同时包 IO 与模型调用
   ⇒ 「按站点分档」做不到；要处置必须**先拆 `catch`**（属重构，不是本项）。
4. **84.2(c) 那档（`Bitmap` 解码）在 headless 里不可实测** ⇒ 真机行为**未验证**，
   不许当成"已确认不带路径"。

---

## 阶段 85（闭环 §4 **#61e**：给 BCL 异常**类型**建码 —— 中英混排的原因那一半终于可翻译；判据**成对**落地，并给新机制 M10 做出负控）— 2026-09-23

> 承接阶段 84（它把 #61b「C 类带上对象」**改期**，并反推出前置项 **#61e**）。
> **本阶段有行为改动**（与 83 / 84 的"零改动"不同）。专题 `docs/2026-09-23-exception-passthrough-triage.md` §85。

### 85.1 一句话结论

`ExternalText.FailureReason` 从**一支分叉**（只认 `HmiPackageException`）改成**三支**：
① 包码异常 → 字典；② **BCL / 系统类型码**（新增 **7** 码）→ 字典；③ 其余回落 `ex.Message`。
**原文没丢** —— 界面改渲染码化短句，异常对象照旧落 `AppLog`（判据②钉住）。
⇒ 阶段 84 那条「`#61e` 先、`#61b` 后」的依赖，**前半已完成**；且 #61b 的前置**不再是"没有码"**，
只剩"逐站选哪个路径"（`.tmp` 那档）。

### 85.2 量的那一遍（**先量再动**，全部来自探针实测）

探针 `tools/probe-bcl-exception-types.cs`（20 场景；`.NET 10` file-based app，`dotnet run` 即可复跑、
不往仓库落 `obj/`），读数 `.workbuddy/tmp/s85-bcl-types.txt`：

| 异常类型 | 次数 | 消息自带目标路径 |
|---|---|---|
| `UnauthorizedAccessException` | 7 | **7/7 带** |
| `FileNotFoundException` | 6 | **6/6 带** |
| `DirectoryNotFoundException` | 5 | **5/5 带** |
| `IOException`（撞名 / 非空目录 / ENOSPC） | 3 | **3/3 带** |
| `ArgumentException`（路径含 `\0` / 空串） | 2 | 0/2 |
| `InvalidDataException`（`ZipArchive` 撞非 ZIP） | 1 | 0/1 |
| `PathTooLongException` | 1 | 1/1 |
| `JsonReaderException`（**派生自 `JsonException`**） | 1 | 0/1 |
| （**不抛**：`File.Move` 目标已存在 / `File.Delete` 不存在） | 2 | — |

**culture 对照**：把 `CurrentUICulture` 切 en/zh，四类消息（`Could not find file` /
`Could not find a part of the path` / `Directory not empty` / JSON 那句）**全部一字不变**
⇒ 这是 #59 的原形状：**BCL 原文不可翻译**，不码化就只能永远中英混排。

**从读数里直接读出的三条设计约束**（都不是推测）：

1. `JsonReaderException` 派生自 `JsonException` ⇒ 码表里挂**基类**（挂派生类覆盖不到基类）。
2. `IOException` 是 4 个已实测类型的基类 ⇒ 它在码表里**必须排最后**，否则**静默**吃掉后代
   —— 没有编译错误、每个类型也都"取得到码"，只是**取到的是错的**。
3. **路径分布**决定了 #61e 与 #61b 的分工：带路径的 5 类码化后**丢掉了目标文件** ⇒ 必须由
   #61b 用三实参重载补回来；不带的 3 类本来就没有。

### 85.3 码表 / 字典 / 可达性

- `src/HmiEditor.App/Services/SystemFailureCodes.cs`（新建）：7 码 / 7 条映射，**全部来自上表实测**；
  `CodeFor` 的遍历**只有一份**（可传表 ⇒ 负控能用乱序表验"顺序真的在起作用"）。
- **刻意不码化两类**（写进类注释，防下一个人"顺手补上"）：`ArgumentException`（路径含 `\0` /
  空串是**调用方的编程错误**，且它是很多类型的基类 ⇒ 收进来会连带吞掉编程错误）；
  **没实测到的类型**（凭"看着像 IO"进表 = 把守卫的输入面换成猜）。
- 两本字典各 **+7 键**（`system.issue.<码小写>`），**全部 0 占位符** —— 与 `FailureReason`
  传 0 实参一致（多一个占位符会在界面上渲染出字面的 `{0}`）。
- **不加 `Lang` 常量** ⇒ `UnusedBaseline` **1123 不动**、`sync_localization.py` 的 `VALUE_MAP`
  不动、`KeptUnreachable` 不动。
- ⚠ 但**可达性必须新增一条机制 M10**：新键在源码里**没有整键字面量**
  （键是 `SystemIssueKeyPrefix + code.ToLowerInvariant()` 拼出来的）⇒ 原九条机制**原理上都看不见它们**。

### 85.4 判据（登记原方向，**原样执行、未改口径**）

| 判据 | 位置 | 形态 |
|---|---|---|
| ① **成对** | `SystemIssueLocalizationGuardTests`（8 条） | 9 个**真实撞 BCL** 的场景 × `SwitchLanguage` 各渲染一遍 |
| ② **原文不许丢** | `SystemFailureRawMessageIsStillLoggedTests`（2 条） | 换 `AppLog.Current` ⇒ 断言异常对象仍含原文 |

- **① 为什么必须成对**：单看"渲染出中文"在"字典缺键 ⇒ 回落"与"语言切换没生效 ⇒ 两次同一份"
  两种情形下也成立（阶段 82 的成对断言教训）。
- **② 为什么必须单独存在**：它是 #61e 的**唯一代价** —— 码化把原因换成短句，界面上的原文就没了；
  而判据①**看不见这件事**（把原文彻底丢掉，①照样全绿）。两侧都断言：界面**不含**原文 +
  日志**有**原文。
- 判据清单：探针每场景真的抛 / 每类型取得到码且键两本字典都在 / **成对** / 两侧不含 `.NET` 原文记号
  （**两支**：整条透传 + 包壳带记号）/ **接线**（毒化消息不许冒头）/ 码表**有序性** / **双向闭包** /
  **诚实边界**（`InvalidOperationException` 仍回落）/ **原文不丢** / **负控 7 组**。
- **负控里最要紧的一组（NC4）**：把 BCL 原文当文案 ⇒ **成对判据看不见它**（`en` 侧本来就该是英文，
  脚本检查对它无话可说）⇒ 这是"泄漏判据不可被成对替代"的证据。NC4 顺带钉了一条反例：
  "两侧都是原文"那种字面形状成对**只**靠"两侧相同"那一支才抓得到 ⇒ **它抓到原文是巧合**。

### 85.5 两条判据施工期自纠（登记在案）

1. **NC4 第一版把 `raw` 当 zh 侧传进成对判据** ⇒ 断言语义写歪（实际报的是"zh 渲染的不是中文"，
   不是"成对看不见这条破坏"）。处置：**先把泄漏判据从测试体里抽成纯函数**（`LeakViolations`），
   负控才可能"注入同一形状、只改一个变量"地验它 —— 原来那版只能去断言记号表本身，
   **那等于没验判据**。改后 NC4 同时钉住判据两支 + 一条"成对只靠巧合抓到"的反例。
2. **M10 的负控**（做不出负控的判据不发布）：脚本 `.workbuddy/tmp/s85-nc10.py`（默认干跑，
   `--break` / `--restore`，还原走**写回原文 + 核 sha256**，不用 `copy2`）注入后实测：
   那 7 个键被**逐条点名**判成不可达 + 面下界报 `实测 0 < 下界 7`，`TEST_EXIT=1`；
   还原后 `sha256 = ba199470…` 与备份**逐字节一致**。⇒ **M10 确实在承重**，不是装饰。

### 85.6 门禁

| 项 | 读数 |
|---|---|
| 提交前（gate1） | `.workbuddy/tmp/s85t-gate1.log`，mtime **11:37:28**；`GATE_EXIT=0` / **7 步全绿** / **0 警告 0 错误**；路径卫生 641 |
| 代码批提交 | **`73e55b9`**（9 files，**+1106 / −50**，`2026-09-23T11:37:54+08:00`）—— 码表 + 守卫×2 + 字典×2 + 探针 + 两处注释同步 |
| 提交后（gate2） | `.workbuddy/tmp/s85t-gate2.log`，mtime **11:39:10** ⇒ **晚于提交 1 分 16 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| 对拍 gate1 vs gate2 | **唯一差异 = 路径卫生「已检查 641 → 643 个文件」** —— 正是码表与新探针从**未跟踪**变**已跟踪**的预期结果（+2，可解释）；断言的测试数 / 步骤结果**逐项相同** |
| 文档批提交 | **`d43dc56`**（5 files，**+394 / −3**，`2026-09-23T11:40:32+08:00`）—— 专题 §85 + §83.3 / §84.3 后记 + 顶部阅读提示 / 登记表 / 本节 / `MEMORY.md` 快照归档 |
| 第三次门禁（gate3） | `.workbuddy/tmp/s85t-gate3.log`，mtime **11:41:51** ⇒ **晚于文档批提交 1 分 19 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误；**工作树干净** |
| 对拍 gate2 vs gate3 | **读数 0 差异**（路径卫生同为 643、八程序集测试数逐项相同）—— 文档不进编译 / 测试读数 |
| 计数 | 八程序集之和 = **3137** 通过 / 0 失败；`App.UiTests` **439 → 449**（**+10** = 新增的 8 + 2 条），`Core` **2119 不变**（本阶段没动 Core 测试） |
| `UnusedBaseline` | **1123 不动**（`LangConstantUsageGuardTests` 绿 ⇒ 实测 == 基线）—— 本阶段**没加 `Lang` 常量**，这是"加键不加常量"那条路的代价读数 |

### 85.7 未闭环

1. **#61b 现在可以做**（前置已满足）—— 逐站仍要选**哪个路径**（失败点常落在 `path + ".tmp"`）。
2. `ArgumentException` 那两个场景（含 `\0` / 空串）**故意不码化** ⇒ 界面仍是英文原文。
   要处置得先认定"这是编程错误、不该给终端用户看" —— 属**另一件事**（未登记）。
3. 表 7 里"不抛"的 2 个场景在 Linux 上不抛 —— 换平台（Windows）行为可能不同，**未验证**。
4. 84.7 的 2 / 3 / 4 条**原样留着**（与 #61e 无关）。

## 阶段 86（闭环 §4 **#61b**：C 类「带上对象」9 站里迁 8 站 —— 直通面 37 → 29，并补上那张面**看不见**的一半）— 2026-09-23

> 承接阶段 85（它落地 #61e，解除了 #61b 的两条改期理由）。
> **本阶段有行为改动** —— 是 #61b 的闭环节点。专题 `docs/2026-09-23-exception-passthrough-triage.md` §86。

### 86.1 一句话结论

9 站里 **8 站**从 `<loc>.Format(键, ex.Message)` 迁到
`UiOperationGuard.FailureDetail(操作名, 目标路径, 异常)`；站点面 **37 → 29**（组合 27 → 22）。
第 9 站（`asset.import.fail` 的**批量档**，多文件 ⇒ "那个文件"结构上不存在）**故意不动** ⇒ 归 #61a。
⚠ 顺带补一条**新判据**：那张面**分不出**"带上了路径"与"三实参里填了 `null`" —— 后者一样消失在面上、
一样让用户看不到文件 ⇒ **第二次假闭环**。**空子经实测存在**（86.4 的负控）。

### 86.2 逐站与三件"不能照抄"的判断

| # | 站点 | 原键 | 新操作名 | 路径实参 | 档 |
|---|---|---|---|---|---|
| 1 | `MainWindow.axaml.cs:1265` | `status.save.failed` | `UiOpSaveProject` | `path`（非 `.tmp`） | ① 现成 |
| 2 | `BuildPipelineService.cs:114` | `gen.export.failed` | `UiOpExportCode` | `targetDir`（`:78`，try 外） | ① 降级为**目录** |
| 3 | `BuildPipelineService.cs:115` | `audit.export.c.code.failed` | `UiOpExportCode` | `targetDir` | ① 降级为**目录** |
| 4 | `BuildPipelineService.cs:208` | `audit.export.c.code.failed` | `UiOpExportCode` | `targetDir`（**须提到 try 外**） | ① + 改结构 |
| 5 | `RecipeEditorView.axaml.cs:927` | `gen.export.failed` | **`UiOpExportRecipe`** | `file.Path.LocalPath` | ① 现成 |
| 6 | `CommunicationConfigView.axaml.cs:1062` | `comm.slave.var.export.failed` | `UiOpExportSlaveVars` | `targetPath`（**新加承接**） | ③ + 改结构 |
| 7 | `AssetManagerView.axaml.cs:287` | `asset.import.fail` | **`UiOpImportSvg`** | `files[0].Path.LocalPath` | ② 可重取 |
| 8 | `AssetManagerView.axaml.cs:358` | `asset.import.fail` | **`UiOpImportFont`** | `files[0].Path.LocalPath` | ② 可重取 |
| — | `AssetManagerView.axaml.cs:222` | `asset.import.fail` | — | **不动**（批量档） | 归 **#61a** |

1. **一个键服务两个操作 ⇒ 先拆操作名。** `gen.export.failed` 同时是「导出代码」与「导出**配方 CSV**」
   （两个视图）；`asset.import.fail` 同时是**三个**操作（批量图片 / SVG / 字体）。
2. **传哪个路径是判断点。** `CompileAsync` 里 `outputPath` / `buildPath` 都声明在 `try` 内 ⇒ **只能给目录**；
   `SaveToPath` 给 `path` 而**不是** `path + ".tmp"`。
3. **两处目标要在 try 外承接**（#4 / #6）—— 不是风格问题：编译器**当场**报
   `error CS0103: 当前上下文中不存在名称"targetDir"`（实测 `BuildPipelineService.cs(206,77)`，见 `.workbuddy/tmp/s86-build1.log`）。
   处置是**外层承接变量**（`string? targetDir = null;` + try 内赋值），**不是**把实参写成 `null`。

### 86.3 直通面 37 → 29

| 项 | 前 | 后 |
|---|---|---|
| 站点 | 37 | **29** |
| (文件, 键) 组合 | 27 | **22** |

整条消失 5 个组合；保留的 `AssetManagerView·asset.import.fail` **3 → 1**。
**下界跟着实测走（一处）**：`sites.Count >= 30` 被 29 撞穿 ⇒ 改 **`>= 20`** 并把理由写进注释 ——
**刻意不贴着实测值**（口径失效会掉到个位数，而"面变小"已由基线**双向**判定管住；
贴着实测值只会让以后每次**正确**的迁移多红一条）。

### 86.4 新判据：直通面看不见的那一半

判据 `UiOperationGuardContractTests.EveryDetailCallSite_PassesAConcreteTargetPath` ——
外部调用点必须**三实参**，第二实参**不得**是 `null` / `default`；**白名单为空**（实测 11 处外部调用点
全是三实参非空；唯一「结构上拿不到路径」的调用点走**两实参重载**，不经过本判据的面）。
**输入面剔掉定义面**（`Services/UiOperationGuard.cs` 里两实参重载被三实参重载在内部调用，是正确实现）——
留在输入里就只能靠一张文件白名单豁免它 ⇒ **判据会被那张表养着**（#55 的教训）。

**负控两重**：

| 层 | 内容 | 读数 |
|---|---|---|
| 纯函数 5 组（合成语料） | ① null → "没有文件"支 ② 两实参 → "不带文件"支 ③ 合规 → **无话可说** ④ **同一注入只改"在哪个文件里"** → 定义面不判 / 别处必判 ⑤ 注释里的反例不算 | 全绿 |
| **生产判据成对**（`.workbuddy/tmp/s86-nc-inject.py`） | 往真实源码把 `:1265` 的 `path` 改成 `null` | 新判据**红**（只它一条，5 通过）；**直通面 3/3 仍绿** ⇒ 空子实测存在 |
| 还原 | **写回原文 + `sha256` 核对**（`2a09e91ae68b6607` 前后一致；**不用 `copy2`**） | ✓ |

### 86.5 字典处置链（删键必须连常量一起走）

删 4 键 ×2 本 + **连删 3 个只靠 M6 桶担保的常量**（`GenExportFailed` / `StatusSaveFailed` /
`AuditExportCCodeFailed` —— 它们的使用点写的都是**字面量**，与 #60 的 `StatusOpenFailed` 同形状）；
加 4 键 + 4 个**有代码引用**的操作名常量。`asset.import.fail` / `AssetImportFail` **不删**（批量档还在用）。

| 项 | 前 | 后 |
|---|---|---|
| 两本字典键数 | 2189 | **2189（净 0）** |
| `Lang` 常量总数 | 1774 | **1775** |
| `UnusedBaseline` | 1123 | **1120** |

分桶**落盘实测**（`HMI_READINGS_OUT=.workbuddy/tmp/s86-buckets.txt`）：

```
无代码引用的常量 1120 / 字典键 2189 / 有常量的键 1775 / 没有常量的键 414
M2 828  ·  M6 269（272 → 269）  ·  M3 18  ·  无任何机制 5
```

⇒ **M6 是唯一动的桶**（−3），与"删的 3 个常量都在 M6 桶里"逐项对得上 —— **量出来的，不是推出来的**。
字典手术按**逐行**做（不做 `json.dump(sorted(...))` 重写）：`git diff --numstat` = 各文件 **4 / 4** 行。
其余守卫零漂移（删的键与常量同生共死 ⇒ `LocalizationKeyCoverage` / 可达性 11/11 绿）。

### 86.6 门禁

| 项 | 读数 |
|---|---|
| 提交前（gate1） | `.workbuddy/tmp/s86-gate1.log`，mtime **13:42:38**；`GATE_EXIT=0` / **7 步全绿** / **0 警告 0 错误**；路径卫生 643 |
| 代码批提交 | **`9b4618f`**（13 files，**+239 / −55**，`2026-09-23T13:45:42+08:00`）—— 8 站迁移 + 4 个操作名常量 + 面/基线/下界同步 + 2 条新判据 + 三处连带注释 |
| 提交后（gate2） | `.workbuddy/tmp/s86-gate2.log`，mtime **13:46:55** ⇒ **晚于提交 1 分 13 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| 对拍 gate1 vs gate2 | **读数 0 差异**（29 行读数逐项相同：路径卫生同为 643、八程序集测试数逐项相同） |
| 文档批提交 | **`b9002ec`**（3 files，**+280 / −3**，`2026-09-23T13:48:10+08:00`）—— 专题 §86 + 阅读提示 + 登记表 + 本节 |
| 第三次门禁（gate3） | `.workbuddy/tmp/s86-gate3.log`，mtime **13:49:32** ⇒ **晚于文档批提交 1 分 22 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误；**工作树干净** |
| 对拍 gate2 vs gate3 | **读数 0 差异**（29 行读数逐项相同）—— 文档不进编译 / 测试读数 |
| 计数 | 八程序集之和 = **3139** 通过 / 0 失败；`App.UiTests` **449 → 451**（**+2** = 本阶段两条新判据），`Core` 2119 不变 |
| `UnusedBaseline` | 1123 → **1120**（见 86.5） |

### 86.7 未闭环

1. **`asset.import.fail` 批量档**（`AssetManagerView.axaml.cs:222`）归 **#61a**；顺带 `Lang.AssetImportFail`
   常量存在但**没人读**（仍在 M6 桶里）—— 做 #61a 时一起处置。
2. **#61a（约 14 站）/ #61d（7 键）原样开着**；#61c 4 站登记"不迁"。
3. **新判据的诚实边界**：只断"第二实参不是 `null` 字面量"，**断不了"给的是不是那个对的路径"**
   （例如把 `.tmp` 当目标文件报）—— 那是逐处判断，源码面够不着；本阶段迁的 8 站里多数在 headless
   里**不可驱动**（文件选择器 / 批量导入），端到端正面钉的成本高于收益。
4. 85.7 的第 1 条**本阶段已完成**；第 2 / 3 条**原样留着**。

## 阶段 87（**先量再改，量出的结果把 #61a 的登记方向推翻了一半**：加码表 + 带码异常 → **返回值带码**；第一刀落在 AI/SVG 那条真异常路径上，直通面 29 → 21）— 2026-09-23

> 承接阶段 86（它闭环 #61b，把 C 类 8 站迁到带目标路径的三实参出口）。
> **本阶段有行为改动** —— 它是 **#61a** 的第一个闭环节点。专题 `docs/2026-09-23-exception-passthrough-triage.md` §87。

### 87.1 一句话结论

`#61a` 登记的修法是「给产出方加**码表 + 带码异常**」。量的那一遍（逐产出方读现场代码）把它推翻了一半：
**A 类点名的四个产出方全是纯函数、都不抛异常** —— `SvgParser.Parse` 返回 `(SvgDocument?, string?)`、
`GeneratedCodeValidator.Validate` / `CsvVariableImporter.Parse` / `RecipeManager.ImportCsv` 同样把失败做成
**返回值** ⇒ 「带码异常」在它们的站点上**无处安放**（站点 catch 到的是 IO / UI 的异常，不是它们的）。

⇒ 模板换成「**返回值带码**」：一份「码 + 中文回落原文 + 实参」的记录体（`SvgParseError`），
与 `ScriptError` / `LuaSyntaxValidator` 那条路**同形**；需要穿过 AI 层返回契约时才包成异常。

但同一遍也量出**一条真的本仓库异常路径**：`IconGenerator.cs:74` 与 `ImageGenerator.cs:63` 把 `SvgParser`
的中文错误串**包进本仓库抛的裸 `InvalidOperationException`**（`"SVG 生成无效: …"`），
再由 `ai.gen.failed` 的 8 处宽捕获直通 `ex.Message`。**第一刀落在这里** ——
直通面 **29 站点 / 22 组合 → 21 / 19**（−8，恰好是那 8 处，**无其他漂移**）。

### 87.2 「改返回类型」是这一遍的仪器

`SvgParser.Parse` 从 `(…, string?)` 换成 `(…, SvgParseError?)` 之后，**编译器当场点出全部消费点**：
实测 ~40 个调用点里只有 **4 处**把 `Error` 当字符串用（2 个 UI 站点 + 2 个 AI 生成器），
其余全是 `(doc, _)` 或 `Assert.Null/NotNull`。编译实测 **0 警告 0 错误**印证了这个读数 ——
若真有几十处消费点，这一步会红成一片。**这不是风格选择，是取证手段**。

### 87.3 两条搬运方式，同一套码

| 档 | 谁来搬 | 出口 | 本阶段站点 |
|---|---|---|---|
| **返回值档** | 产出方把 `SvgParseError` 当返回值交出来 | `ExternalText.Describe(error)` | 2（`Wiring.DockAggregate.cs:85` / `AssetManagerView.axaml.cs:267`） |
| **异常档** | 上层包成 `SvgParseException` 抛出 | `ExternalText.FailureReason(ex)` **第 ② 支** | 8（`AiGenerateDialogBase` 1 / `AiChatPanelView` 2 / `AiIconDialog` 5） |

⇒ **码表一份、字典键一份**（`svg.issue.*`，4 键 × 两本，全 0 占位符）。
`FailureReason` 由**三支**改**四支**（新增第 ② 支）。

### 87.4 判据（`SvgParseIssueLocalizationGuardTests`，7 条）

4 码 × 两条搬运 = **8 行渲染必须等于字典值**（毒化回落原文）、两本字典成对性（缺键 / en 含中文 /
两侧一字不差 / 0 实参却有占位符）、`XML_INVALID` **保留 .NET 原文但不进模板**（现场 catch 取期望值，
**不写死措辞**）、**双向闭包**（死码 / 产出了但没声明）、**诚实边界**（没码的照旧回落 `ex.Message`）、
**产出方不许退化**（两个 AI 生成器必须抛带码异常、不许再拼裸中文原文）、4 组负控。

`AiGenerators_ThrowTheCodedException_NotABareBclOne` **非有不可**：那 8 站与产出方是**成对**的 ——
站点换成 `FailureReason(ex)` 之后，`ex` 若退回裸 `InvalidOperationException`，`FailureReason` 接不住、
走第 ④ 支渲染中文原文，而**直通面那张面是绿的**（站点形状确实变了）⇒ 又是「面绿着、收益为零」。

### 87.5 生产负控（两重，实测；脚本 `.workbuddy/tmp/s87-nc-inject.py`）

| 注入 | 新判据 / 可达性 | 直通面 | 读 |
|---|---|---|---|
| ① `icon-bare`：`IconGenerator` 退回裸异常 + 中文原文模板 | **红**（同一文件**两条都点名**） | **3/3 全绿** | 「产出方退化」**只有新判据看得见**，不是重复劳动 |
| ② `m11-drop`：摘掉可达性机制 M11 的取数 | **红 ×2**：逐条点名 4 个 `svg.issue.*` 键 + 面下界报「实测 0 < 下界 4」 | — | M11 在承重 |

还原走**写回原文 + `sha256`** 核对（`bc734d2768af5660` / `3e698e384a7292f5` 前后一致；**不用 `copy2`**）。

⚠ ② 顺带**独立证明**了「本阶段新增的 4 个键只有 M11 一条机制」：若 M6 / M2 也覆盖它们，
摘掉 M11 之后那 4 个键**不会**掉下来。

### 87.6 字典与可达性（读数）

| 项 | 动作 / 读数 |
|---|---|
| 两本字典 | 各 **+4 键**（`svg.issue.*`）；键由 `SvgIssueKeyPrefix + code.ToLowerInvariant()` 拼出 |
| `Lang` 常量 | **一个都不加**（源码里没有整键字面量）⇒ `UnusedBaseline` **1120 不动** |
| 可达性 | 新增 **M11**（与 M8/M9/M10 同形；下界取**满值 4**） |
| 十一面读数 | `M1 655 / M2 1113 / M3 26 / M4 30 / M5 19 / M6 381 / M7 12 / M8 41 / M9 7 / M10 7 / M11 4` |

⚠ **跨阶段对照**：与阶段 85 的十面比，只有 **M1 651 → 655（+4）** / **M6 385 → 381（−4）** 动了 ——
这一对**归因于阶段 86（`9b4618f`，C 类文案带上目标对象 ⇒ 新增 4 个 `Lang` 常量）**，与本阶段无关；
其余八面**逐项不动**。

### 87.7 连带（三处，都是同一契约改动的正当结果，不是放宽判据）

| 位置 | 改了什么 | 为什么必须改 |
|---|---|---|
| `AI.Tests` 两处 | `Assert.ThrowsAsync<InvalidOperationException>` → `<SvgParseException>`，并**连码一起断** | 异常类型是**故意换的**；只断类型的话「抛个空码」照样绿 |
| `CoreVisibleTextInventoryGuardTests` | 新增 1 条通道（`Services/SvgParseError.cs`，1 条中文回落）；常数 61 → **62** | 那 1 条中文是 `svg.issue.no_shapes` 的**回落**，与既有 ⑤ 同类。**守卫自己先点出来的**（`+ Services/SvgParseError.cs: 登记 0，实扫 1`） |
| `docs/2026-09-20-core-visible-text-inventory.md` | 活表 §0/§3/§5 改 62 / 7 条通道 + 追加 §8；顺带修掉 §3 ⑤ 行的**过期消费方**（`App/Wiring.Canvas.cs` → `App/Wiring.DockAggregate.cs`，阶段 54 之后文档停在旧值） | 守卫的命名就是「== 文档写的数」⇒ 两侧必须一起动；§6/§7 是阶段 44 快照，**不动** |

### 87.8 门禁

| 项 | 读数 |
|---|---|
| 首次提交前（gate1，**红**） | `.workbuddy/tmp/s87-gate1-red.log` —— 3 条失败，全是 87.7 那三处的**未修状态**（`AI.Tests` 2 + `Core.Tests` 1），**没有一条是逻辑缺陷** |
| 修完后提交前（gate1） | `.workbuddy/tmp/s87-gate1.log`，mtime **14:26:12**；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误；路径卫生 643 |
| 代码批提交 | **`f966f54`**（21 files，**+773 / −48**，`2026-09-23T14:26:31+08:00`） |
| 提交后（gate2） | `.workbuddy/tmp/s87-gate2.log`，mtime **14:27:50** ⇒ **晚于提交 1 分 19 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| 对拍 gate1 vs gate2 | **剥掉墙钟后 812 行逐项相同**；唯一差异 = 路径卫生「643 → **646** 个文件」（**+3** ＝ 新增的 3 个 `src/HmiEditor.Core/Services/*.cs`；该步扫 `git ls-files 'src/*' 'tools/*' 'scripts/*' 'firmware/*'`，**有意排除 `tests/`** ⇒ 第 4 个新文件是测试，不计入） |
| 文档批提交 | **`c4a6a3d`**（3 files，**+275 / −2**，`2026-09-23T14:29:38+08:00`）—— 专题 §87 + 登记表 + 本章节 |
| 第三次门禁（gate3） | `.workbuddy/tmp/s87-gate3.log`，mtime **14:30:55** ⇒ **晚于文档批提交 1 分 17 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误；**工作树干净** |
| 对拍 gate2 vs gate3 | **读数 0 差异（812 行逐项相同）** |
| 计数 | 八程序集之和 **3139 → 3146**（**+7** ＝ 本阶段新判据 7 条；`App.UiTests` **451 → 458**） |

### 87.9 未闭环

1. **其余 A 类产出方的「返回值带码」**：`GeneratedCodeValidator`（4 站）/ `CsvVariableImporter`（3 站）/
   `FontSubsetAnalyzer` / `RecipeManager`。模板已立（`SvgParseError` 的形状可照搬），但它们各自的失败
   **语义更杂**（校验问题清单 / CSV 行错误）⇒ 要逐条决定「哪些失败该给码」，**不是机械照搬** ⇒ 拆成 **#61f**。
2. **`asset.import.fail` 的批量档**（`AssetManagerView.axaml.cs:222`）仍在 —— 它现在**既**没带对象
   （"那个文件"结构上不存在）**又**没码。
3. **`ai.gen.failed` 里混着的 IO 那几处**：那 8 站是宽捕获，SVG 之外的失败（落盘 / 渲染）仍走第 ④ 支回落原文。
   要收口得先**按产出方拆站**（阶段 83 就记了「先拆再动」）。
4. **#61d（7 键）原样开着**（先读代码再定类）；**#61b 残留**只有批量档那 1 处。
5. **新判据的诚实边界**：只断「渲染 === 字典值」与「产出方抛带码异常」，**断不了翻译质量**（en 的值是不是好英文），
## 阶段 88（闭环 §4 **#61f** 第一刀：量的那一遍**第二次证伪**「给产出方加码」；缺陷在**消费点** —— CSV 坏行此前静默丢掉）— 2026-09-23

### 88.1 一句话结论

`#61f` 登记的修法是「其余 A 类产出方逐个照搬**返回值带码**」。量的那一遍**第二次证伪**了这个前提：
四个产出方（`GeneratedCodeValidator` / `CsvVariableImporter` / `FontSubsetAnalyzer` / `RecipeManager`）
在 Core 里 **`throw ` 逐文件 0 命中**；8 个站点里 **5 站只可能 catch 到 IO**、2 站是**无目标路径的兜底 catch**、
1 块（2 行）里含**唯一真落点** `SimulatorProjectExporter.cs:68` —— 而那处是**兜底不变量**
（`GeneratedCodeValidator.cs:251` 已把同一件事报成 Error 级 finding；`Export` 有 ~75 个调用点 ⇒ 不改返回类型）。

**但同一遍量出一件真的**：`CsvVariableImporter` 的返回值 `ImportResult.Errors` 是 **`List<string>` 裸中文串**，
而两个消费方**一个只取条数、一个整块丢弃** ⇒ CSV 里有坏行时**坏行静默丢掉**，
用户在两条路上都看不到「是哪几行」。⇒ 第一刀落在**补上那条到界面的路**上，码只是它的前提。

### 88.2 8 站逐站（读现场代码）

| # | 站点 | 键 | `try` 里真正可能抛的 | 目标对象 | 定类 |
|---|---|---|---|---|---|
| 1 / 2 | `MainWindow:1512 / 1513` | `status.verify.failed` / `audit.verify.code.failed` | 两个纯函数 + `new ValidationResultView(...)`（UI 构造） | 无 | **兜底 catch** |
| 3 / 4 | `MainWindow:1596 / 1597` | `status.simulator.export.failed` ×2 | **`SimulatorProjectExporter.cs:68`** ＋ 写盘 IO | `outputPath` ✓ | A 类真落点 **+ IO 混档** |
| 5 | `VariableImportView:103` | `var.import.failed` | `File.ReadAllTextAsync` | `_selectedFilePath` ✓ | **C 类（IO）** |
| 6 | `RecipeEditorView:892` | `gen.import.failed` | `OpenReadAsync` + `ReadToEndAsync` | `files[0].Path.LocalPath` ✓ | **C 类（IO）** |
| 7 | `CommunicationConfigView:1020` | `comm.slave.var.import.failed` | `File.ReadAllText` | 同上 ✓ | **C 类（IO）** |
| 8 | `FontSubsetView:89` | `font.export.fail` | `ExportCharsetFile` 里的 `File.WriteAllText` | `file.Path.LocalPath` ✓ | **C 类（IO）** |

**推翻的两条旧判断**：① 阶段 86 记的「`gen.import.failed` 属 A 类混档」**不成立** ——
`RecipeManager.ImportCsv` 不抛（返回 count，`0` ⇒ 已有 `recipe.csv.import.invalid` 提示）；
② `FontSubsetAnalyzer` **不是纯函数**（`ExportCharsetFile` 真的写盘）⇒ 归 C 类。

### 88.3 改动（表 10）

| 层 | 改动 |
|---|---|
| Core | `CsvImportIssueCodes`（**5 码**）+ `CsvImportIssue`（码 / **行号** / 中文回落 / 实参，形状照 `ScriptError`：**行号不进 `Message`**）；`ImportResult.Errors` `List<string>` → `List<CsvImportIssue>` |
| App | `ExternalText.CsvIssueKey` + `Describe(CsvImportIssue)`（行号前缀走 `Lang.CsvLinePrefix`）；两本字典各 **+6 键** |
| 消费点 | `VariableImportView` / `CommunicationConfigView`：状态行带**条数**、`ToolTip` 逐条 `Describe` 渲染（与 `ScriptEditorView` 处置 `ScriptError` **同形**） |
| 顺带 | `ExternalText` 类注释「**五条**键约定」→「**七条**」（阶段 87 加了 `svg.issue.*` 却没补这份清单） |

### 88.4 判据（`CsvImportIssueLocalizationGuardTests`，8 条）与三态闭包

主判据（毒化回落原文，4 条真实失败经 `Describe` 必须**等于字典期望串含行号前缀**）／
**双语**（zh 必含汉字、en 零汉字且两遍不同，挂 `Language-global` + `finally` 还原）／
成对性（键齐 + en 不含中文 + 两侧不一字不差 + **占位符个数 == 产出方实测实参个数**）／
三态闭包／兜底档不许变通行证（码仍在用 + `ex.Message` 仍是实参）／
**接线面**（产出方 ≥5 处发码且不退回 `List<string>`；**两个消费方都必须同时出现 `.Errors` 与 `ExternalText.Describe`**）／
回落自身可用／5 组内嵌负控。

**三态闭包**：`ROW_PARSE_FAILED` 是行循环的兜底 —— 逐行读代码，循环体里每个操作都不可能抛
（按 `Length` 迭代、`idx < fields.Length` 前置保护、`TryParse`、属性赋值）⇒ **构造不出真实触发**。
处置 = 进 `NotProducible` 表并写明理由（**不塞进 `ProducedCodes` 造假、也不删码**），且那张表**双向**：
一旦真能产出 ⇒ 红；塞一个没声明的码 ⇒ 也红。落盘读数 **声明 5 / 真实产出 4 / 兜底 1**。

### 88.5 面读数与对拍（**收益不在直通面上**）

| 面 | 阶段 87 | 阶段 88 |
|---|---|---|
| `M1-Lang 常量` | 655 | **656**（新增 `Lang.CsvLinePrefix`，有代码引用） |
| `M12-CSV 导入码` | — | **5**（本阶段新增机制，下界取满值） |
| 其余十面 | `M2 1113 / M3 26 / M4 30 / M5 19 / M6 381 / M7 12 / M8 41 / M9 7 / M10 7 / M11 4` | **逐项不动** |

⚠ **直通面（`ExceptionMessagePassthroughFaceTests`）本阶段一动没动（21 站点 / 19 组合）** ——
这是**对的**：本项缺陷的形状不是「`ex.Message` 直通模板」，而是「失败原因根本到不了界面」
⇒ **它在那张面上原理上看不见**。收益证据只有三样：新判据读数、消费点接线面、Core 中文台账
（通道 7 → **8**、条数 62 → **67**、`Pending["Services/CsvVariableImporter.cs"]` 16 → **11**
—— 顺带把那条**与代码不符的理由**改对了：原来写「两个消费方都只显示 `Errors.Count`」，
对 `CommunicationConfigView` 不成立，#54 同病）。

### 88.6 生产负控（两重，实测）

| 注入 | 期望红 | 实测 |
|---|---|---|
| NC-A：摘掉 `CommunicationConfigView` 里读 `Errors` + `Describe` 那一整块 | `ConsumersKeepTheCodedShape` | **红并逐条点名该文件**；⚠ **主判据仍绿** ⇒ 两条互补、空子真实存在 |
| NC-B：摘掉可达性 M12 取数 | 主判据 + 面下界 | **红 ×2**：逐条点名 **5 个** `csv.issue.*` 键 ＋ 「**M12-CSV 导入码：实测 0 < 下界 5**」 |

还原用**写回原文**（不用 `copy2`），逐字节核对 `sha256` 前后一致
（`CommunicationConfigView.axaml.cs = 8800d9cce4b15b07`、`LocalizationKeyReachabilityGuardTests.cs = 65c4cc449e13728e`），
`grep -rn "NC-A\|NC-B" src/ tests/` **零残留**。

### 88.7 门禁

| 项 | 读数 |
|---|---|
| gate1（提交前） | `.workbuddy/tmp/s88-gate1.log` mtime **15:00:37**；`EXIT=0` / 7 步全绿 / 0 警告 0 错误；路径卫生 646 |
| 代码批 | **`1dde2b5`**（13 files，+833 / −22，`15:00:56`）⇒ 日志比提交**早 19 秒** ✓ |
| gate2（提交后） | `.workbuddy/tmp/s88-gate2.log` mtime **15:02:11** ⇒ **晚于提交 1 分 15 秒** ✓ |
| 对拍（`.workbuddy/tmp/s88-diffgate.py`） | 剥墙钟 + ISO 时间戳、**排序**后 828 / 829 行，**唯一实质差异 = 路径卫生 646 → 648**（+2 ＝ 两个新 `src/…/Services/*.cs`；该步走 `git ls-files` ⇒ 提交前看不见未跟踪文件） |
| 计数 | 八程序集 3146 → **3154**（**+8** ＝ 新判据，`App.UiTests` 458 → **466**） |
| `UnusedBaseline` | **1120 不动** |
| 文档批 | **`b30ffa5`**（4 files，`15:06:49`）＝ 专题 §88 + `docs/2026-09-20-core-visible-text-inventory.md` §9 + 本文件 + 登记表 |
| gate3（文档批后） | `.workbuddy/tmp/s88-gate3.log` mtime **15:08:11** ⇒ **晚于文档批 1 分 22 秒** ✓；`EXIT=0` / 7 步全绿 / 0 警告 0 错误；路径卫生 **648**（与 gate2 同） |
| 对拍 gate2 vs gate3 | **实质差异 0** —— 829 / 828 行，**4 行差异全是空行**（`@@ -1 +0,0 @@` 处一行，日志收尾） |

⚠ **第四次门禁（gate4，回填批之后）只记在** `.workbuddy/memory/2026-09-23.md`、**不回写本文件**
（与阶段 85 / 86 / 87 的收口方式一致：`docs/` 里每个数字都停在「最后一次**内容**改动」那一跑上）。
⇒ 本表**刻意不含 gate4 读数**（写下这一行时它还没跑）；若它与 gate3 读数不一致，日志里会另起小节说明。

### 88.8 未闭环

1. **#61g（新）**：`GeneratedCodeValidator` 的 **23 条 findings 文案直显中文**
   （`ValidationResultView` 的 `Text = finding.Message`）—— 已过通道核实、但**没本地化**，
   也**不在直通面上** ⇒ 两条既有守卫**都抓不到它**。第三例「产出方有返回值但没码」，规模最大。
2. **#61h（新）**：#61f 量出的 **5 站 IO 未迁**（`var.import.failed` / `gen.import.failed` /
   `comm.slave.var.import.failed` / `font.export.fail` / `status.simulator.export.failed` ×2 行）——
   与 **#61b 的 C 类同形**（对象都在作用域）⇒ 可迁 `FailureDetail(操作名, 目标路径, 异常)`；
   **本阶段刻意不动**（一次只放开一个变量）。
3. **`SimulatorProjectExporter.cs:68` 判过的「不迁」**：兜底不变量 + ~75 个调用点 ⇒ 不建码、不改返回类型
   （与 #61c / #58 同类的诚实边界）。
4. **新判据的诚实边界**：只断「渲染 === 字典期望串」与「消费点接线」，**断不了翻译质量**；
   `ROW_PARSE_FAILED` 的实参是 **BCL 原文（英文）**（有意留的排障信息）；
   接线面是**源码面**证据，不是端到端 UI 断言。

## 阶段 89（闭环 §4 **#61g** 第一刀：生成代码验证的**两条通道**码化 —— 量与改都落在 Message 18 码 + Location 4 码上）— 2026-09-23

### 89.1 一句话结论

`#61g` 登记的修法**站得住**（与 #61a / #61f 不同，这次没被推翻），但登记里那句「23 条 findings 文案」
把**三个数**混成了一个：**14** 个 `findings.Add` **站点** / **18** 条**消息码** / **23** 条**中文字面量**
（= 18 条消息回落 ＋ **5** 条定位字面量）。落点因此是**两条通道**：`Message`（18 码）与
`Location`（**只有 4 种需要码**，另 4 种是模型路径、与语言无关）。

顺带量出一条**与文件家族自身设计意图相矛盾**的读数：`RuntimeUserIssueKind` 的类注释
（`src/HmiEditor.Core/Security/RuntimeUserValidator.cs:10-12`）明文写着「刻意只给枚举、不带文案：
文案由 App 层映射」，而 `GeneratedCodeValidator.ValidateRuntimeUsers` 恰在 Core 里把该枚举
映射成 **4 条中文长句** ⇒ 修法有**现成答案**：映射留着，改成**映射到码**。

### 89.2 改动（表 11）

| 层 | 改动 | 关键点 |
|---|---|---|
| Core | `Services/GeneratedCodeIssueCodes.cs`（新）：**18 消息码** ＋ `FindingLocationCodes`：**4 定位码** | **两张表**：定位与消息生命周期不同（定位会被复制/筛选） |
| Core | `GeneratedCodeFinding` 增 `Code` / `LocationCode` / `Args` / `LocationArgs`；14 站点逐条补码 | `Code` **不给默认值** ⇒ 编译器点名全部站点（阶段 87 那条「改返回类型＝取证手段」）；实测**恰好 1 个错误** |
| Core | `ValidateRuntimeUsers` 的 `switch`：产中文 → 产 `(code, message, args)` | 映射留着、产物改成码（§89.1） |
| App | `ExternalText.GenIssueKey` / `GenLocationKey` ＋ `Describe(GeneratedCodeFinding)` / `DescribeLocation(...)` | **本族唯一归一化点**：列表项与「复制全部」都调它 |
| 字典 | 两本各 **+21 键**（`gen.issue.*` ×17 ＋ `gen.loc.*` ×4）⇒ **2199 → 2220** | 17 不是 18：`GENERATOR_WARNING_COMMENT` 刻意无键 |
| 可达性 | 新增 **M13**（面 **21**，恰等于新增键数） | 取数复用新守卫的 `DeclaredCodes()` / `DeclaredLocationCodes()` ＋ 按 `MessageNoKey` 摘掉无键码 |

**回落中文串逐字不变** ⇒ Core 侧既有 6 处 `Location` 断言与 2 处消息断言**一条未改**；
唯一跟进的是 1 处测试构造（`WindowProductionPathUiTests:138`）。

### 89.3 判据（`GenFindingLocalizationGuardTests`，11 条）

毒化渲染 === 字典模板（17 条真实 finding，含实参）／「刻意无键」档**双向**闭合且仍产得出／
定位按**三档**分治（有码 4 · 中性直出 4 · 外来 1，外来那张表**双向**）／双语渲染
（**剥掉实参后**不许剩汉字 —— 为 `EXPRESSION_SYNTAX` 的中文实参留的诚实边界）／
两本字典成对（占位符个数 == 实测实参个数，消息 17 码与定位 4 码**各走一遍**）／
**三态闭包**（消息：声明 18 / 产出 17 / 兜底 1 / 无键 1；定位：声明 4 / 产出 4 / 兜底 0）／
兜底档的「2 实参」**钉回源码**／回落可用性／**源码面接线**（产出方 ≥18 处消息码 ＋ ≥7 处定位码、
出口两个键推导在、消费方四条直显形状全无）／**6 组内嵌负控**。

**同源复用**：成对性 / 三态闭包 / 渲染泄漏 / 占位符计数**直接调**
`CsvImportIssueLocalizationGuardTests` 那四个纯函数（为 `PairingViolations` 加 `keyFor` 可选参数，
默认仍是 `csv.issue.`，那里**既有 7 处调用一字未动**）。

### 89.4 生产负控（**三重**，实测）

| # | 注入 | 实测 |
|---|---|---|
| NC-B | 摘掉可达性 M13 取数段（保留下界行） | **红 ×2**：**21 个键**逐条点名 ＋「M13-生成代码验证码：实测 0 < 下界 21」；另 4 条绿 |
| NC-A1 | `ValidationResultView` 两条通道退回裸串 | **红 ×1**（`ConsumerKeepsTheCodedShape`，4 条点名）；另 10 条绿 |
| NC-A2 | `ExternalText` 的 `GenIssueKey(finding.Code)` 改成等价局部变量写法 | **红 ×1**（台账 `VisibleChannels_EvidenceStillExists`，逐字点名）；另 9 条绿 |

还原一律**写回原文**（不用 `copy2`：mtime 会一起被复制 ⇒ MSBuild 不重建 ⇒ 正控跑的是负控的二进制），
三处 `sha256` 前 / 后一致，还原后守卫类 **17/17 复跑全绿**。
⚠ 教训：NC-A2 第一版写成「给 `GenIssueKey` 改名」⇒ **编译不过**，测试根本没跑到判据 —— **负控必须是能编译的坏形状**。

### 89.5 面读数与台账（**本阶段也不在直通面上**）

- **M13 面 = 21**（新增）；其余十二面**逐项不动**（`M1 656 / M2 1113 / M3 26 / M4 30 / M5 19 /
  M6 381 / M7 12 / M8 41 / M9 7 / M10 7 / M11 4 / M12 5`）。
- **直通面（`ExceptionMessagePassthroughFaceTests`）一动没动** —— 与阶段 88 同理：本项缺陷是
  「界面那两条通道根本没经过字典」，在只收 `ex.Message` 的那张面上**原理上看不见**。
- 台账：**通道仍是 8 条 / 条数之和仍是 67** —— 变的只有通道 ① 的**消费方与标记**
  （`ValidationResultView.axaml.cs` ＋ `Text = finding.Message` → `Services/ExternalText.cs` ＋ `GenIssueKey(finding.Code)`）。
  ⚠ 「本地化了一个文件」**不等于**「台账条数要变小」：那 23 条中文按设计是**字典缺键时的回落**、必须留在 Core。

### 89.6 门禁

| 项 | 读数 |
|---|---|
| gate1（**红**） | `.workbuddy/tmp/s89-gate1.log` mtime **16:03:07**，`EXIT=1`：`App.UiTests` 1 红 / 477 —— `XamlResourceKeyClosureTests.PhantomKeyVictimButtons_ActuallyCarryTheirBackground`，异常在**平台初始化**（`InvalidOperationException: The calling thread cannot access this object because a different thread owns it`）。判为既有 **#47 族**并发红：同一异常在本仓历史门禁日志出现 ≥3 次 ＋ 该用例**单独复跑立刻绿** ＋ 本阶段不碰 UI 面；门禁第 5 步是八个程序集**并行**跑 slnx，正是窗口最大的跑法 |
| gate1b（绿） | `.workbuddy/tmp/s89-gate1b.log` mtime **16:04:51**，`EXIT=0` / 7 步全绿 / 0 警告 0 错误 / 路径卫生 648 |
| 内容批 | **`eb7e4bb`**（11 files，+1319 / −38，`2026-09-23T16:05:20+08:00`）⇒ gate1b 早 29 秒 ✓ |
| gate2 | `.workbuddy/tmp/s89-gate2.log` mtime **16:06:40** ⇒ 晚于提交 **1 分 20 秒** ✓；7 步全绿 |
| 对拍 gate1b vs gate2 | **828 / 828 行**，唯一实质差异 = 路径卫生 **648 → 649**（+1 ＝ 新增的 `GeneratedCodeIssueCodes.cs`；该步走 `git ls-files` ⇒ 提交前看不见未跟踪文件） |
| 计数 | 大队八项之和 3154 → **3165**（**+11**；`App.UiTests` 466 → **477**，其余九项逐条一致 ⇒ 纯净增） |
| `UnusedBaseline` | **1120 不动**（不加 `Lang` 常量） |
| 文档批 | **`e676d20`**（4 files，+328 / −4，`16:20:21`）＝ 专题 §89 + 本文件 §89 + `docs/2026-09-20-core-visible-text-inventory.md` §10 + 登记表（**#61g 改闭环** ＋ **#61i / #61j / #61k / #61l** 新开） |
| gate3（文档批后） | `.workbuddy/tmp/s89-gate3.log` mtime **16:22:01** ⇒ **晚于文档批 1 分 40 秒** ✓；`EXIT=0` / 7 步全绿 / 0 警告 0 错误；路径卫生 **649**（与 gate2 同）；大队八项之和 **3165**（与 gate2 同）；负载 `/proc/loadavg` `1.69 → 3.95`（起 `16:20:25` / 讫 `16:22:01`） |
| 对拍 gate2 vs gate3 | **实质差异 0** —— 剥掉墙钟（`已用时间` / `持续时间` / 审计行内嵌的 ISO 时间戳）后做**多重集**差集：只剩 dotnet 那三句「总共 1 个测试文件与指定模式相匹配。」的**换行方式**不同（内容各 **3** 次、完全相同）⇒ 本轮只改 `docs/`，门禁读数**本就不该动** |

⚠ **第四次门禁（gate4，回填批之后）只记在** `.workbuddy/memory/2026-09-23.md`、**不回写本文件**
（与阶段 85 / 86 / 87 / 88 的收口方式一致：`docs/` 里每个数字都停在「最后一次**内容**改动」那一跑上）。
⇒ 本表**刻意不含 gate4 读数**（写下这一行时它还没跑）；若它与 gate3 读数不一致，日志里会另起小节说明。

### 89.7 未闭环

**#61i**（`EXPRESSION_SYNTAX` 的中文实参：`ExpressionEngine` 的 3 条 `FormatException`，且被 `DevMessage` 豁免挡在台账外）／
**#61j**（`CopyAll` 的枚举名与两个品牌字面量 `Faceplate` / `LVGL API`）／
**#61k**（`// WARNING:` 产物注释自身中英不一致：3 英 1 中）／
**#61l**（`DANGLING_REFERENCE` 的外来定位：同一串在交叉引用面板与本对话框两处露出）。

## 验证记录

| 日期 | 命令/场景 | 结果 | 证据 |
|---|---|---|---|
| 2026-09-13 | `dotnet build HmiEditor.slnx` | 通过，0 错误、0 代码警告 | 本地实测 |
| 2026-09-13 | `dotnet test HmiEditor.slnx` | 1788/1788 通过（Core 1288、App.UiTests 62） | 本地实测 |
| 2026-09-13 | `firmware/t113/tests/run_host_tests.sh` | 162 项全过（70 + 触摸 16/16/15 + 板级 18/18/9） | 本地实测 |
| 2026-09-13 | `LVGL_DIR=~/Git/lvgl bash scripts/verify.sh` | **退出码 0**（真实 LVGL CMake 编译 + selftest + CTest） | 本地实测 |
| 2026-09-13 | `bash scripts/ci-gate.sh` | 通过（语法/构建/测试/固件 host 测试） | 本地实测 |
| 2026-09-13 | 新增 `GeneratedCodeEscapingTests` | 11 项回归全绿（转义/格式串/canvas/作用域/追溯戳） | tests/HmiEditor.Core.Tests/GeneratedCodeEscapingTests.cs |
| 2026-09-13 | 新增 `BindingSemanticsTests` | 14 个用例全绿（三端语义一致：缩放/小数位/前后缀/判真） | tests/HmiEditor.Core.Tests/BindingSemanticsTests.cs |
| 2026-09-13 | 新增 `RefreshBindingsParityTests` | 4 项对拍全绿（模拟器与设备刷新路径不再分叉） | tests/HmiEditor.Core.Tests/RefreshBindingsParityTests.cs |
| 2026-09-13 | 发起 `GenerateControlParameters` 拆分 | 回退：机械提取破坏语法，待改用 Roslyn | 见本节第 2 条 |
| 2026-09-13 | 新增 `ModbusTcpRuntimeDriverTests`（+5） | 全绿：短帧×2 / 超时 / 并发不串响应 / 并发必串行 | tests/Hmi.Communication.Tests/ModbusTcpRuntimeDriverTests.cs |
| 2026-09-13 | 新增 `DeviceChannelTests`（+4） | 全绿：并发不串 / 超时异常类型 / 调用方取消 / 关闭不挂死 | tests/HmiEditor.Device.Tests/DeviceChannelTests.cs |
| 2026-09-13 | `GenerateControlParameters` 词法感知拆分 | 459 → 94 行 + 15 个方法；测试零改动通过（输出不变） | src/HmiEditor.CodeGen/CodeGenerator.cs |
| 2026-09-13 | 行为测试替代源码 grep | +6 `ControlTextStyleResolverTests`，-3 源码 grep 测试 | tests/HmiEditor.Core.Tests/ControlTextStyleResolverTests.cs |
| 2026-09-13 | 新增 `GeneratedCodeSemanticTests` | 3 项：全控件族工程零未知 API / 零 Error；并据此补齐校验器白名单 ~40 项 | tests/HmiEditor.Core.Tests/GeneratedCodeSemanticTests.cs |
| 2026-09-13 | 新增 `BuildPipelineServiceTests`（+4） | 全绿：导出落盘/产物清单/进度与置忙/复用已有 .so 产物 | tests/HmiEditor.Core.Tests/BuildPipelineServiceTests.cs |
| 2026-09-13 | `MainWindow` 编译导出流水线服务化 | 5023 → 4434 行；产物状态移入 `BuildPipelineService` | src/HmiEditor.App/Services/BuildPipelineService.cs |
| 2026-09-13 | 板级配置单一来源 | `board_profile.h` + 编译期 `-D` 注入；`sed -i` 副作用消除并有门禁守卫 | firmware/t113/app/runtime/board_profile.h、scripts/ci-gate.sh |
| 2026-09-13 | 新增 `ValidatorSelfConsistencyTests`（+3） | 全绿；并自测「注入假条目 → 失败 → 还原」验证守卫有效 | tests/HmiEditor.Core.Tests/ValidatorSelfConsistencyTests.cs |
| 2026-09-13 | 移除未使用依赖 `CommunityToolkit.Mvvm` | 全仓 0 使用；保留在用的 `Dock.Model.Mvvm`；决策入 ADR | docs/adr/ADR-2026-09-13-ui-mvvm-decision.md |
| 2026-09-13 | `WireEvents` 按域拆分 | 992 行 → 5 个 partial 文件 + 5 行骨架；构建/测试不变 | src/HmiEditor.App/Wiring.*.cs |
| 2026-09-13 | Modbus 预览运行时平移 | 246 行移至 partial 文件，`MainWindow` 3454 → 3207 行；1651/1651 通过 | src/HmiEditor.App/MainWindow.Preview.cs |
| 2026-09-13 | `scripts/verify.sh` + `scripts/ci-gate.sh` 复跑 | 两个门禁均退出码 0（真实 LVGL 编译 + 4 步门禁） | 本地实测 |
| 2026-09-13 | 新增 `UiOperationGuardTests`（+10） | 全绿：异常不外泄/落盘+界面双报/取消静默/本地状态行优先于全局出口/提示自身抛错被吞掉 | tests/HmiEditor.Core.Tests/UiOperationGuardTests.cs |
| 2026-09-13 | `async void` 处理器用户可见化 | `async void` 51 → 35；未受保护数量 **16 → 0**（词法扫描统计） | src/HmiEditor.App/Services/UiOperationGuard.cs |
| 2026-09-13 | 新增 `CSource` 结构化断言工具 | 解析 C 调用点（跳过注释/字符串、含嵌套调用），排版无关且不吃注释里的同名文本 | tests/HmiEditor.Core.Tests/Support/CSource.cs |
| 2026-09-13 | 迁移精确调用文本断言 | **281 处** `Assert.Contains("f(a, b)", code)` → `CSource.AssertCode/AssertCall`；全仓该类断言 211 → 3 处 | tests/HmiEditor.Core.Tests/*CodeGenTests.cs 等 20 个文件 |
| 2026-09-13 | 新增 `CSourceHelperTests`（+26） | 全绿：22 项解析/匹配边界 + 守卫自测 | tests/HmiEditor.Core.Tests/CSourceHelperTests.cs |
| 2026-09-13 | 新增 `TestAssertionStyleGuardTests`（+2） | 守卫禁止新增脆弱断言；已用「注入 1 处违规 → 守卫失败 → 删除」自测有效 | tests/HmiEditor.Core.Tests/TestAssertionStyleGuardTests.cs |
| 2026-09-13 | 引入 `Avalonia.Headless` UI 测试能力 | 新增测试项目（30 项）：15 个 UserControl 视图装载+布局、真实鼠标/键盘模拟、确认对话框确定/取消、属性面板写回 | tests/HmiEditor.App.UiTests/ |
| 2026-09-13 | UI 测试发现的行为差异 | `TextBox.Text` 程序化赋值在 Avalonia 12 **不触发** `TextChanged`（测试改用真实键盘输入）；X/Y/Width/Height 数值写回**不经过**可插拔 `IPropertyValidator`（已记录待办） | docs/project-status.md 第 14 条 |
| 2026-09-13 | 预览运行时抽为 `PreviewRuntimeService` | 新增 22 项行为测试（信号注册/值源选择与回退/提供器注入与摘除/**释放语义**/动作执行） | src/HmiEditor.App/Services/PreviewRuntimeService.cs |
| 2026-09-13 | 修正文档与实现不符 | 旧注释称「Int → 方波」，实现中整数走正弦分支；已更正注释并写入测试（不改行为） | PreviewRuntimeService.RegisterSimVariable |
| 2026-09-13 | 属性写回统一过校验器 | `SaveProperties` 51 处直写改为 `ValidateAndWrite<T>`；新增 4 项 UI 测试（拒绝数值改动/默认仍钳制/全属性不变量/命令例外） | src/HmiEditor.App/Views/PropertyPanelView.axaml.cs |
| 2026-09-13 | 元信息归一化不再依赖事件时机 | 装载路径显式 `ApplyMetadataToTemplate()` + `_isLoadingMetadata` 守卫；新增 5 项 UI 测试 | src/HmiEditor.App/Views/FaceplateTemplateEditorView.axaml.cs |
| 2026-09-13 | 快捷键/右键菜单命令层去重 | 两份 switch（163+78 行）→ 唯一分发器；`MainWindow` 3242 → 3086 行 | src/HmiEditor.App/Services/EditorCommandCatalog.cs |
| 2026-09-13 | 新增 `EditorCommandCatalogTests`（+16） | 全绿：零冲突、逐条解析、修饰键严格匹配、文本编辑场景、菜单动作全覆盖 | tests/HmiEditor.App.UiTests/EditorCommandCatalogTests.cs |
| 2026-09-13 | 修复 Ctrl+Shift+V 被 Paste 抢占 | 原 `switch` 分支顺序使"垂直等距排列"快捷键实际执行粘贴；现由绑定表解析为 `DistributeV` | EditorCommandCatalog.Bindings |
| 2026-09-13 | 修复系统变量类型解析 | `sys.*` 带点号被误判为 UDT 字段 → 恒返回 Float；现按真实类型解析 | src/HmiEditor.Core/Services/VariableBindingInfo.cs |
| 2026-09-13 | 新增 `VariableBindingInfoTests`（+11） | 全绿：变量 Id/按名回退/UDT 字段/系统变量/未知回落 | tests/HmiEditor.Core.Tests/VariableBindingInfoTests.cs |
| 2026-09-13 | 修复带点号变量名的绑定解析 | 点号分支失败后回退字面 Id/名匹配；回归测试证明修复前标签表为 0 | src/HmiEditor.Core/Models/VariableManager.cs |
| 2026-09-13 | 新增 `VariableBindingResolutionTests`（+6） | 全绿：UDT 字段优先、字段缺失回退、无实例回退、未知仍为 null、下游类型与标签表 | tests/HmiEditor.Core.Tests/VariableBindingResolutionTests.cs |
| 2026-09-13 | 画布工具栏装配按域拆出 | `MainWindow.axaml.cs` 3086 → **2928 行**；3 个方法移入 `Wiring.CanvasTools.cs`（145 行，逐行未改） | src/HmiEditor.App/Wiring.CanvasTools.cs |
| 2026-09-14 | `dotnet build HmiEditor.slnx -c Release` | 通过，**0 错误 / 0 警告**（13 项目，11.9s） | 本地实测 |
| 2026-09-14 | `dotnet test HmiEditor.slnx -c Release --no-build` | **1788/1788** 通过（Core 1288、AI 237、Communication 75、Device 63、App.UiTests 62、Simulator 41、Hardware 22） | 本地实测 |
| 2026-09-14 | `CI_REQUIRE_FIRMWARE_TESTS=1 bash scripts/ci-gate.sh` | **4 步全过**（脚本语法 20/20、构建、全量测试、固件 host 162） | 本地实测 |
| 2026-09-14 | 根目录产物归档 | 16 个被跟踪 + 2 个未跟踪 → `docs/archive/2026-08/`（含 README）；根目录仅剩工程入口 4 个文件 | git 识别为 rename |
| 2026-09-14 | 删除 `HmiEditor.Renderer` | src 项目 **14 → 13**；构建 0 错 0 警、测试数不变 | 本地实测 |
| 2026-09-14 | 中央包版本管理（CPM） | 16 个包单点声明；**36 处** Version 属性移除；`ManagePackageVersionsCentrally=true` 实测生效 | Directory.Packages.props |
| 2026-09-14 | 测试临时目录健壮性 | 本机 tmpfs 仅 10 MB 时从 1785/1788 → **1788/1788**（自动落到 `.hmi-test-tmp/`）；44 处裸 `Directory.Delete` 收口 | tests/HmiEditor.Core.Tests/Support/TestTempDir.cs |
| 2026-09-14 | `ci-gate.sh` 去 `/dev/fd` 依赖 | 受限环境下不再静默空跑；清单为空即失败（原来报 `/dev/fd/63` 后继续以 0 个脚本通过） | scripts/ci-gate.sh |
| 2026-09-14 | `LVGL_DIR=~/Git/lvgl bash scripts/verify.sh` | **本机未跑通（退出码 1）**；已逐步骤手工复现证明生成链正常（LVGL 9.5 全量编译 + `hmi_sim` 链接成功 + `--selftest` 命中 PASS）。失败为三者叠加：本机 `/tmp` 仅 10 MB、本次会话 WorkBuddy CLI 的 `safe-delete` shim 拦截 `rm -rf` 时自身写报告失败、测试间歇性失败 | 见 `docs/2026-09-14-optimization-directions.md` §4.2 |
| 2026-09-14 | 测试污染真实仓库（已修复） | `GitWorkflowServiceTests` 在 `TMPDIR` 位于仓库内时，其真实 `git commit` 落到项目仓库（生成一条 message 为 `x` 的提交）。已 `git reset --soft` 撤销，并加固测试为显式避开 git 工作树 | tests/HmiEditor.Core.Tests/GitWorkflowServiceTests.cs |
| 2026-09-14 | 测试间歇性失败（已关闭） | 两次 `verify.sh` 分别失败在 `MockImageLoadingTests`（磁盘满）与 `AlarmEngineTests`；单独跑 `dotnet test`（Release/Debug）均 1788/1788。**2026-09-16 定位并修复**：定时器回调固定等待 → `TestWait.Until`；`/tmp` 10 MB tmpfs 写满 → `TestTempDir` | 见当前阻断项第 9 条 |
| 2026-09-16 | 连跑 6 次全量（默认并行度）验证 flaky 是否已消失 | **6/6 全绿**，每次 2248 通过 / 0 失败 ⇒ 关闭阻断项 9 | 本地实测 |
| 2026-09-16 | 权限模型下沉：C# 权限/生成头/守卫测试 | **37/37**（`ActionPermissionMapTests` 13 + `SecurityConfigCodeGenTests` 24） | tests/HmiEditor.Core.Tests/ |
| 2026-09-16 | 固件 host 测试 `firmware/t113/tests/run_host_tests.sh` | 4 步全过；第 4 步「会话与权限」**34 通过 / 0 失败** | firmware/t113/tests/hmi_actions_permission_test.c |
| 2026-09-16 | 权限下沉负向验证 6 组（注入 → 期望红 → 还原） | 6/6 命中；**2 组首轮暴露真缺口并已修**（映射语义无独立期望表；守卫判据漏 `#  define` 风格） | `docs/2026-09-16-permission-model-sinking.md` §7 |
| 2026-09-16 | 全量测试 + `ci-gate.sh` | 7 个测试程序集全绿（Core **1679**，合计 **2285**）；门禁 5 步通过 | 本地实测 |
| 2026-09-16 | 链接契约穷举守卫 `SimulatorLinkContractTests`（+4） | 4/4 绿；**实测缺口 7 个宿主符号**（登记时只知 5 个） | tests/HmiEditor.Core.Tests/SimulatorLinkContractTests.cs |
| 2026-09-16 | 符号级链接探针**正例**（只编译不链接 + `nm` 比对） | 未定义 68 个；扣掉 libc/libm/lv_*/SDL_* 后**为空** ⇒ 导出工程自洽 | `.refactor-tmp/link-probe.sh`（一次性） |
| 2026-09-16 | 符号级链接探针**负例**（剥掉那 7 个定义，保留调用点） | 未定义 74 个；扣完**恰好**剩下 `hmi_login` `hmi_logout` `hmi_require_permission` `hmi_show_alarms` `hmi_show_trends` `var_raw_read` `var_raw_write` ⇒ 判据有效 | 同上（`strip-host-symbols.py`） |
| 2026-09-16 | **编译器级**负例：导出的 `generated_hmi.c` 改回 `1f` | `error: 整数常量的 'f' 后缀无效` ⇒ 该缺陷真实存在且是编译期失败 | `float-literal-neg.py`（一次性） |
| 2026-09-16 | 判定策略下沉 + 导航候选名单一来源：新增测试 | `SecurityConfigCodeGenTests` 24 → 36（+12）；新增 `ScreenNavTargetsTests` 9 + `ActionCallbacksCodeGenTests` +12 | tests/HmiEditor.Core.Tests/ |
| 2026-09-16 | 阶段 5 负向验证 6 组（注入 → 期望红且点名 → 还原） | **6/6 命中**；其中 c 组一次注入同时红两条守卫（"转发" + "没有第二份"） | docs/2026-09-16-simulator-export-link-selfcontained.md §7 |
| 2026-09-16 | 全量测试（7 个程序集） | **2320 通过 / 0 失败**（Core **1714**、AI 272、App.UiTests 129、Communication 75、Device 64、Simulator 44、Hardware 22） | 本地实测 |
| 2026-09-16 | `bash scripts/ci-gate.sh` | **5 步全过**（脚本语法 → 路径卫生 → 构建 → 全量测试 → 固件 host **34/34**） | 本地实测 |
| 2026-09-16 | 通道收帧判据收成唯一实现（TCP + 串口共用 `CommandFrameCollector`） | Device 程序集 64 → **76**；新增 `CommandFrameCollectorTests`(6) / `ChannelFramingContractTests`(4) / `DeviceChannelTests` +2（原并发用例改为 `replyDelayMs: 400` + 显式断言无串帧形态） | src/HmiEditor.Device/Channels/CommandFrameCollector.cs |
| 2026-09-16 | 收帧负向验证 4 组（注入 → 期望红且点名 → 还原） | **4/4 命中**；NV-2 首轮因"注入与缺陷不等价"未红，改用"多段数据累计时长超静默期"的构造后有效 | `docs/2026-09-16-channel-frame-completion-contract.md` §5 |
| 2026-09-16 | 全量测试（7 个程序集） | **2332 通过 / 0 失败**（Core **1714**、AI 272、App.UiTests 129、Device **76**、Communication 75、Simulator 44、Hardware 22） | 本地实测 |
| 2026-09-16 | `bash scripts/ci-gate.sh`（阶段 6 复跑） | **5 步全过**（固件 host 34/34） | 本地实测 |
| 2026-09-16 | `dotnet build src/HmiEditor.App -c Release`（阶段 7） | **0 警告 / 0 错误**（含 XAML 编译） | 本地实测 |
| 2026-09-16 | 新增 `RuntimeUserValidatorTests`（20）+ 角色清单转发守卫（1） | 定向 `RuntimeUserValidatorTests` / `SecurityConfigCodeGenTests` **66/66** | tests/HmiEditor.Core.Tests/ |
| 2026-09-16 | 本地化键同步 | zh-CN / en-US 各 **+25**（2028 → 2053）；`LocalizationTests` **22/22**（键数、占位符数量一致） | `Strings.zh-CN.json` / `Strings.en-US.json` |
| 2026-09-16 | 全量测试（7 个程序集，阶段 7） | **2420 通过 / 0 失败**（Core **1802**、AI 272、App.UiTests 129、Device 76、Communication 75、Simulator 44、Hardware 22） | 本地实测 |
| 2026-09-16 | `bash scripts/ci-gate.sh`（阶段 7） | **5 步全过** | 本地实测 |
| 2026-09-16 | 阶段 7 负向验证 5 组（注入 → 期望红**且点名用例** → 还原） | **5/5 命中**，还原后复跑回全绿。判据比上一轮更硬：要求期望用例名落在**失败行**上（行尾 `[FAIL]`）—— 只判退出码会被「别的用例碰巧红了」骗过去 | `docs/2026-09-16-runtime-user-editor-ui.md` §6 |

| 2026-09-16 | 新增 `LockScreenTargetsTests`(8) / `SimulatorSessionContractTests`(6) / `SecurityConfigCodeGenTests` +4 | 定向 **83/83** 全绿 | tests/HmiEditor.Core.Tests/ |
| 2026-09-16 | 固件 host 新增第 7 步 `hmi_actions_session_test` | **56 通过 / 0 失败**（断言数由 fixture 的 `HMI_SEC_SESSION_TIMEOUT_MIN` 算出，不写死 30）；`run_host_tests.sh` 6 步 → **7 步** | firmware/t113/tests/hmi_actions_session_test.c |
| 2026-09-16 | 阶段 8 负向验证 9 组（注入 → 期望红**且点名用例** → 还原） | **9/9 命中**；但**首轮 3/8 未捕获**（注入与缺陷不等价 / needle 写错 / 判据挑弱），逐条定性后修判据再复跑全中 —— 详见专题 §7 | `docs/2026-09-16-session-timeout-and-audit.md` §7 |
| 2026-09-16 | 单跑 `DeviceChannelTests` 取证 | **6/6 绿 / 3 s**（门禁里同一程序集 37 s）⇒ 确认下面的假红为**负载相关**而非回归 | 本地实测 |
| 2026-09-16 | 门禁**首跑**（阶段 8）红 3 条 | ① `TestAssertionStyleGuardTests` 抓到**新写的** `Assert.Contains("int main(int argc, char** argv)", …)`（字面量含 `(` 与 `,`）—— 定向 filter 没覆盖到该全仓扫描守卫 ⇒ 改 `CSource.AssertText`；② `DeviceChannelTests.SlowFirstByte_…` 假红（首字节窗口 1000 ms 与回包延迟 600 ms 只差 400 ms，余量被负载吃掉）⇒ 显式 `FirstByteTimeoutMs = 5000` 拉开比例，同族 `ConcurrentCommands_…` 一并对齐；③ `GitWorkflowServiceTests.…Idempotent` 第 3 次复发 | 见「当前阻断项」第 11 条 |
| 2026-09-16 | 全量测试（7 个程序集，阶段 8） | **2438 通过 / 0 失败**（Core **1820**、AI 272、App.UiTests 129、Device 76、Communication 75、Simulator 44、Hardware 22，基线 2420 ⇒ **+18**） | 本地实测 |
| 2026-09-16 | `bash scripts/ci-gate.sh`（阶段 8） | **5 步全过**（固件 host **7 步**全过：会话 56/56、凭据 41/41、crypto 40/40…） | 本地实测 |
| 2026-09-16 | 新增 `RuntimeConfigCodeGenTests`（31 项） | 定向 **31/31** 全绿 | tests/HmiEditor.Core.Tests/RuntimeConfigCodeGenTests.cs |
| 2026-09-16 | 固件 host 新增第 8 步 `hmi_log_test` | 4 场景 × 8 判据全过；`run_host_tests.sh` **7 步 → 8 步** | firmware/t113/tests/hmi_log_test.c |
| 2026-09-16 | 阶段 9 负向验证 8 组（注入 → 期望红**且 needle 落在该用例自己的失败块里** → 还原） | **8/8 命中**，还原后两侧复跑全绿。**但走了两轮，两次都是扫描器判据缺陷**：① 块头判据 `用例名 and '[FAIL]'` 命中的是 **xUnit 末尾汇总行**（无错误消息/堆栈）⇒ 1/8；② 改成 `失败\s+(\S+)\s*\[` 后**匹配不上 Theory 带空格的块头** `(bad: 2147483647)`（`[Fact]` 侥幸通过）⇒ 7/8。修完加扫描器双向自检（含 Theory 块头回归钉子）后全中 | `docs/2026-09-16-runtime-config-sinking.md` §8.1 |
| 2026-09-16 | 全量测试（7 个程序集，阶段 9） | **2469 通过 / 0 失败**（Core **1851**、AI 272、App.UiTests 129、Device 76、Communication 75、Simulator 44、Hardware 22；基线 2438 ⇒ **+31**）。**阻断项 11 与上轮那条 TCP 假红本轮均未复发** | 本地实测 |
| 2026-09-16 | `bash scripts/ci-gate.sh`（阶段 9） | **5 步全过**：脚本语法 21 / 路径卫生（579 文件）/ 构建 **0 警告 0 错误** / 全量 2469 / 固件 host **8 步全过**（第 8 步 4 场景 × 8 判据） | 本地实测 |
| 2026-09-16 | 单跑 `ModbusTcpRuntimeDriverTests`（改窗前取证） | **15/15 绿 / 4 s** ⇒ 门禁里的「通讯超时(2000ms)」是负载相关而非回归 | 本地实测 |
| 2026-09-16 | 单跑整个 `HmiEditor.Core.Tests`（阶段 10，4 次） | **1900/1900 绿 ×2**、**1899/1900 红 ×2**（同一条：`GitWorkflowServiceTests.…Idempotent`，35 s / 1m12s）⇒ **单程序集约 1/3 复现率** | 本地实测 |
| 2026-09-16 | `/tmp` 占用采样（跑 Core 程序集期间，1 s 粒度 ×34） | 已用峰值 **116 KB**、可用最小 **10124 KB**（总量 10 MB）⇒ **磁盘假设排除**；残留 3 项（`j9BP50` / `MSBuildTemp*`） | `.refactor-tmp/probe_tmp_usage.sh` |
| 2026-09-16 | `git` 直接压测（bash，不经 .NET） | `--version` 200 次 **152 ms**（0.8 ms/次）、`rev-parse` 50 次 **47 ms**、`init` 50 次 **4829 ms**（含每轮 `rm -rf .git`，单次 **60 ms**）、`init+rev-parse` 200 轮 **0 失败** ⇒ **git 本身健康** | 本地实测 |
| 2026-09-16 | 阶段 10 负向验证 5 组（3 组见专题 + 2 组 git 窗口守卫） | **5/5 命中**；git 窗口守卫两组还原后 SHA256 **逐字节一致**（`4e3face51cc7`） | `.refactor-tmp/negative_verify_phase10*.py` |
| 2026-09-16 | 全量测试（7 个程序集，阶段 10） | Core **1900**（1851 + 49）、AI 276（272 + 4）、**App.UiTests 142**（129 + 13）、Device 76、Communication 75、Simulator 44、Hardware 22 = **2535 项**；基线 2469 ⇒ **+66** | 本地实测 |
| 2026-09-16 | `bash scripts/ci-gate.sh`（阶段 10）**三跑** | 第 1~3 步全过（构建 **0 警告 0 错误**）；**第 4 步红 1 条**（阻断项 11 —— 三跑同一条、耗时 **6m42s / 5s / 50s**）；第 5 步未由门禁执行 | 本地实测 |
| 2026-09-16 | `firmware/t113/tests/run_host_tests.sh`（阶段 10 单独补跑第 5 步） | **8 步全部通过**（本轮固件零改动；第 8 步 4 场景 × 8 判据全 PASS） | 本地实测 |
| 2026-09-16 | `bash scripts/ci-gate.sh`（阶段 10）重跑（修 Modbus 窗口后） | **Communication 75/75 绿**（连续两跑）；第 4 步仍红 1 条 = 阻断项 11 | 本地实测 |
| 2026-09-17 | `bash tools/probe-fs-newpath-cost.sh`（**新工具**，纯 shell、不碰 .NET） | 新**文件**路径 ≈**60 ms**、**目录创建仅 1 ms**；N=1/2/5/10/20/40 新文件 → 62/125/321/646/**1288**/**2538** ms（线性）；**8 进程并行 ≈ 8 × 单进程** ⇒ 该开销**全局串行**；负载下 `git --version` p50 **3717** ms / max **6084** ms（**0 失败**） | 本地实测 |
| 2026-09-17 | xUnit `maxParallelThreads` 对照（Core 程序集 ×5） | 默认(20) / 8 / 4 / 2 / 默认(20) → 墙钟 **84 / 83 / 90 / 88 / 88 s**（同量级）⇒ **并行度不是杠杆**（吞吐由那条串行资源决定） | `.refactor-tmp/probe_parallelism_vs_walltime.sh` |
| 2026-09-17 | 测试过滤器写法验证 | `!~` 在「0 匹配」的项目上不报错（Hardware **22/22 绿**）；`~GitWorkflowServiceTests` 单独跑 **8/8 绿 / 7 s**；排除后 Core 恰为 **1892 = 1900 − 8** | 本地实测 |
| 2026-09-17 | `bash scripts/ci-gate.sh`（第 3 步拆分后）**连跑两轮** | **两轮退出码 0**：第 1 轮 **134 s**（大队 2527 全绿 + git 8 条 7 s + 固件 host 8 步）、第 2 轮 **156 s**（同）。**阻断项 11 未复发** | 本地实测 |
| 2026-09-17 | 拆分前那一跑（同一次门禁）暴露**同族第二例** | `CommandFrameCollectorTests.FirstByteArrivingAfterSilentPeriod_…` 红（`Expected "ok:SLOW\n" Actual ""`）⇒ 生产者 `Task.Delay` 续体排 ThreadPool；修后第二轮又暴露**第三例** `SilenceIsMeasuredFromLastDataChunk_…`（`"line1\nline2\n"` 少一段）⇒ 三段各起线程、间隔抖动叠加 vs 30 ms 余量。**两条均已修**（见专题 §3.2） | 本地实测 |
| 2026-09-17 | `scripts/verify.sh`（原样 **Debug**）复跑 ×2 | 两轮**均未完成**（**15 m 44 s / 21 m 39 s** 后人工终止），日志停在第 [3] 步、golden 目录 mtime 不变 ⇒ 第 [6] 步从未执行；第 1 轮红 1 条 = 阻断项 11 的 `…SecondCall_Idempotent`（该用例 **1 m 31 s** ≈ 六次撞穿 15 s 窗口） | 本地实测 |
| 2026-09-17 | `dotnet test tests/HmiEditor.App.UiTests/…`（Debug）**单独跑** / 走 slnx **只匹配它** | **142/142 绿 / 12.5 s** / **EXIT=0 / 24 s** ⇒ 卡住与 slnx 路径无关、与该程序集本身无关 | 本地实测 |
| 2026-09-17 | 同上但**全量与大队并发** + `--blame-hang --blame-hang-timeout 90s` | **EXIT=1 / 187 s**：报告「**测试主机进程崩溃**」、通过 **92/142**、「崩溃时运行的测试」**9 条（分属 9 个不同测试类）**、**无托管异常无堆栈** ⇒ 登记为阻断项 12 | 本地实测 |
| 2026-09-17 | 第 3 步拆分在 `verify.sh` 里的效果（Debug，第 1 轮 → 第 2 轮） | Core **2 m 3 s → 1 m 27 s**、Device **1 m 35 s → 48 s**、Communication **1 m 19 s → 26 s**；大队 **2527 全绿** | 本地实测 |
| 2026-09-17 | `scripts/verify.sh`（改后 **Release**）**连跑两轮** | **两轮退出码 0**：第 1 轮（无产物断言）**392 s**、第 2 轮（含产物断言）**223 s**。逐段：大队 **2527 全绿** + git **8/8 / 9 s** + Golden **2 screens / 36 controls** + 真实 LVGL 9.5 CMake 全量编译 → `hmi_sim`（1286896 B）→ selftest **PASS** → CTest **passed** | 本地实测 |
| 2026-09-17 | 生成产物断言缺口（`verify.sh` 原只断言 3 个文件） | 实际产物 **11 个**；已补 `generated_security.h` / `generated_comm.h` / `generated_runtime.h` / `sim_config.h` 四条断言（三条独立下沉链 + sim_config）。改动后先在**现成产物**上验等价（两个目录 **8/8** 通过），再跑第 2 轮端到端确认 | 本地实测 |
| 2026-09-17 | 阻断项 12 修法实验（**第一次**：只往输出目录放 `xunit.runner.json` = `{"parallelizeTestCollections": false}`，不改仓库文件） | 当时读数：`App.UiTests` **142/142 绿 / 1 m 56 s**、全量 **173 s / EXIT=0**（并行时同命令 **187 s / EXIT=1 崩溃**）⇒ 曾判「假设成立、代价 +35 s」。**该结论已撤回** —— 同日复验无法复现，见下面三行 | 本地实测 |
| 2026-09-17 | 阻断项 12 复验：清洁树 Debug 全量，**带** `--blame-hang --blame-hang-timeout 180s` | **EXIT=1 / 336 s**：testhost 崩溃、`App.UiTests` **98/142**；同轮其余 6 个程序集全绿（Core 1892 / AI 276 / Device 76 / Communication 75 / Simulator 44 / Hardware 22） | `.refactor-tmp/planA-run.log` |
| 2026-09-17 | 同上但**已落地方案 A**（`xunit.runner.json` + csproj 声明，`bin/` 里确有该文件 116 B） | **EXIT=1 / 307 s**：**仍然崩**（**37/142**）⇒ **方案 A 未消除崩溃**，当日回退 | 同上 |
| 2026-09-17 | 判据正控：把 `xunit.runner.json` 的值故意写成字符串 `"definitely-not-a-bool"` | **EXIT=0 / 40 s，没有报错** ⇒ 无法证明 runner 读过该文件（拷贝生效 ≠ 被读取）。xUnit **不会**自动拷贝它 —— 回退 csproj 声明后 `bin/` 里那份不再出现（已核） | 同上 |
| 2026-09-17 | 清洁树 Debug 全量，**不带** blame（与上一行同一条命令、只去掉 blame） | **EXIT=0 / 107 s** ⇒ 「Debug 全量并发必崩」**不成立**；崩溃与 `--blame-hang` 纠缠（机器负载未受控，待 2×2 分离，见阻断项 12） | `.refactor-tmp/finalize.log` |
| 2026-09-17 | **Avalonia 资源目录重复键**（Release 门禁 12 条 `ArgumentException … Key: /Assets/Icons.axaml`） | 根因＝同一个 `.axaml` 既被显式声明为 `AvaloniaResource`、又被默认 glob（`**\*.axaml`）收成 `AvaloniaXaml`，而 `GenerateAvaloniaResources` 会把 `@(AvaloniaXaml)` 折进 `@(AvaloniaResource)` ⇒ 中间产物 `obj/<cfg>/net10.0/Avalonia/resources` 索引里出现**两条同路径条目**。**读数**：解析出 49 条、`/Assets/Icons.axaml` **2 条**（off/size 完全相同）、`size 之和比载荷恰多 9707`；改后 **49 → 48 条、无重复、载荷 865358 不变** | `.refactor-tmp/parse_avares_catalog.py` |
| 2026-09-17 | Icons 修复后的回归 | `dotnet build HmiEditor.slnx -c Release` **0 警告 0 错误**；Release 全量测试 **EXIT=0 / 121 s**（含 142 项 UI 测试，覆盖 `App.axaml` 的资源装载） | `.refactor-tmp/finalize.log`（C2/C4，19:48） |
| 2026-09-17 | Icons 修复后第一次**干净树**门禁（**归属更正**：此前引用的那条 252 s 跑在 19:38、**早于本修复落地 10 分钟**且当时方案 A 仍落地 ⇒ 不算本修复的证据） | `bash scripts/ci-gate.sh` **EXIT=0 / 330 s**，末行「🎉 门禁全部通过（**6 步**）」；逐段：**22** 个脚本语法 / 路径卫生 **587** 文件 / Release 构建 **0 警告 0 错误** / 大队 **2527 全绿**（Simulator 44 / Hardware 22 / Communication 75 / Device 76 / AI 276 / **App.UiTests 142** / Core 1892）/ git 用例 **8/8**（8 s）/ 固件 host **8 步全过** | `.refactor-tmp/gate-check.log`（20:38） |
| 2026-09-17 | **判据正控**：只换 `bin/Debug/net10.0/xunit.runner.json`，同一条命令（App.UiTests 单跑，detailed） | `{}` 基线 239 行 / 带类名用例名 142 → `diagnosticMessages:true` **317 行**（+78）→ `methodDisplay:"method"` **带类名用例名 0** ⇒ `xunit.runner.json` **确实被读取**；`parallelizeTestCollections:false` 墙钟 21 s vs 基线 20 s（**没变慢**，因为 142 项本来就在同一 UI 线程上串行） | `.refactor-tmp/hangprobe.log`（P 段）+ `hangprobe/p0~p3.txt` |
| 2026-09-17 | Debug 全量（无 blame，方案A关）**第 1 跑** | **exit=1 / 176 s**：`App.UiTests` **142/142 绿 / 1 m 56 s**；红的只有 Core 的 `AlarmEngineTests.Engine_TriggerThenReturn_CreatesReturnedRecord`（`Expected: Returned / Actual: Active`，Core **1891/1892**） | `.refactor-tmp/hangprobe/r1.txt` |
| 2026-09-17 | 同上**第 2 跑** | **挂死**（264 s 时判定：连续 90 s 无输出，输出停在 2195 B）；6/7 程序集全绿（Core 1892/1892），**`App.UiTests` 从未报出**；现场：**209 线程 `futex_wait`、零 CPU、testhost `State: S` / 26 线程** ⇒ **僵死** | `.refactor-tmp/hangprobe.log`（R2 + 现场取证） |
| 2026-09-17 | 集合修法（13 类 + `CollectionDefinition`）**冒烟**：App.UiTests 单跑 | **142/142 绿 / 持续时间 10 s / 墙钟 13 s**（0 警告 0 错误）⇒ 串行化代价≈0 | 本地实测 |
| 2026-09-17 | 集合修法的**对照实验**（同一命令 × 4，Debug 全量，无 blame） | **H1 挂死**（607 s；现场：`App.UiTests` 的 **testhost 已消失**、只剩 wrapper 在 `do_wait`，**没有** R2 那 209 个 `futex_wait` ⇒ **不是同一签名**）；**H2** exit=1 / 176 s（`App.UiTests` 仍 **142/142 绿 / 1 m 54 s**，红的是 Device 的 `CommandFrameCollectorTests.…FirstByteWindow…`，`Assert.InRange(…, 150, 2000)`）；**H3** exit=0 / 185 s（**142/142 绿 / 1 m 58 s**）；**H4** exit=0 / 185 s（**142/142 绿 / 2 m**） | `.refactor-tmp/hangrate2.log` + `hangrate2/h1~h4.txt` |
| 2026-09-17 | 集合修法的**判定** | 修法前 ≈ **2/3** 失败（3 次观测）、修法后 **1/4** ⇒ **N=4 不足以声称有效**（若 p=2/3，4 跑里至少 1 次失败的概率是 **98.8%**，而只观察到 1 次）。按「不留没有证据的改动」**已回退**（`git checkout -- tests/HmiEditor.App.UiTests/`） | 同上 |
| 2026-09-17 | **判别实验**（`.refactor-tmp/storm-test.sh`）：**单跑** `App.UiTests` + 6 个外部进程持续造新路径（这个组合里**没有别的测试程序集**、也没有别的 testhost） | 对照组（无风暴）**exit=0 / 20 s**；**S1 挂死**（120 s 判定、输出停在 **0 字节**、`exit=99 / 282 s`）；**S2 挂死**（124 s 判定、输出停在 **0 字节**）⇒ **2/2 全挂**。现场（S1）：全局 wchan 直方图**只有 ~28 个线程**（9 `futex_wait` / 6 `anon_pipe_read` / 4 `do_wait` …）、`pgrep -f HmiEditor.App.UiTests` **零命中**、进程表里只有 `dotnet test` 而没有 `vstest.console` / `testhost` ⇒ **测试宿主根本没起来** | `.refactor-tmp/storm.log` + `storm-out/` |
| 2026-09-17 | 判别实验的**判定** | **触发条件不需要「别的测试程序集」在场** ⇒ 「13 个类并发进单例 headless 会话」**不足以**解释它；本沙箱至少存在一个**与 headless 会话无关**的失败入口：**串行的新路径资源被占满 ⇒ 连测试宿主都起不来**（阻断项 11 同族）。⚠️ 但 S1/S2 的**签名与全量那两次不同**（那两次 testhost 在、209 `futex_wait`、输出 2195 B）⇒ 二者**不合并**；且 6 个 worker 是**人为饱和**，只证明这条资源的**充分性**，不等于全量那两次就是这个原因 | 同上 |
| 2026-09-17 | **阶段 11**：`dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误**（7.98 s） | 本地实测 |
| 2026-09-17 | **阶段 11**：全量测试（7 程序集） | **2577 通过 / 0 失败**（Core 1900 / **AI 313** / **App.UiTests 147** / Device 76 / Communication 75 / Simulator 44 / Hardware 22；基线 2535 ⇒ **+42**） | 本地实测 |
| 2026-09-17 | **阶段 11**：新增 `SseReaderTests`(14) / `ProviderStreamingTests`(14) / `LlmChatTests`(5) / `AgentEngineStreamingTests`(4) / `AiStreamingWiringTests`(5) | 定向 `HmiEditor.AI.Tests` **313/313**；`HmiEditor.App.UiTests` **147/147** | tests/HmiEditor.AI.Tests、tests/HmiEditor.App.UiTests |
| 2026-09-17 | **阶段 11** 负向验证 **6 组**（注入 → 期望红**且点名用例** → 还原） | **6/6 命中**：A 转发丢观察者 / B Claude 正文取错来源 / C 引擎不走流式 / D OpenAI 吞 error 帧 / E Claude 忽略 error 事件 / F SSE 不按空行分帧 | `docs/2026-09-17-llm-streaming-sse.md` §4 |
| 2026-09-17 | **阶段 11** 自纠：首轮 NV 注入「去掉 `emitted` 检查」**未变红** | 追出**自己新写的死代码**（`IsRequestShapeError(ex) && !emitted` 按构造不可达）⇒ 删掉不可达分支，把"流中途失败"改成真实可达行为并用 NV-D/E 钉住；注释同步改为真实机制 | 同上 §4.1 |
| 2026-09-17 | **阶段 11**：`bash scripts/ci-gate.sh` | **6 步全过**（末行「🎉 门禁全部通过（6 步）」；含固件 host 8 步） | 本地实测 |
| 2026-09-17 | **阶段 11**：`scripts/verify.sh`（真实 LVGL 端到端） | **退出码 0**：Golden 2 screens / 36 controls → CMake 全量编译 → `hmi_sim` → selftest **Passed** → CTest **1/1 passed** | 本地实测 |
| 2026-09-17 | **阶段 12**：`dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** | 本地实测 |
| 2026-09-17 | **阶段 12**：全量测试（7 程序集） | **2595 通过 / 0 失败**（Core 1900 → **1912**、App.UiTests 147 → **153**、AI 313、Device 76 / Communication 75 / Simulator 44 / Hardware 22；本轮基线 2577 ⇒ **+18**） | 本地实测 |
| 2026-09-17 | **阶段 12**：新增 `GeneratedCodeValidatorRuntimeUserTests`(12) + `AgentToolSchemaContractTests`(+6) | 定向 **12/12** 与 **18/18** 全绿 | tests/HmiEditor.Core.Tests、tests/HmiEditor.App.UiTests |
| 2026-09-17 | **阶段 12** 负向验证 **7 组**（注入 → 期望红**且点名用例** → 还原） | **7/7 命中**：A 导出不校验用户表 / B 去掉整表体检 / C 角色表外降级为警告 / D 启用无密码降级为警告 / E 工具重新广告 `watchdog_timeout_ms` / F 描述写回死参数名 / G 破坏 `ToSnakeCase` 映射 | `docs/2026-09-17-config-honesty-export-gate-and-ai-tool.md` §4 |
| 2026-09-17 | **阶段 12** 自纠：首版工具守卫依赖 `ConfigFieldInventory.NotWired` 反推死字段，**测试立刻红** | 追出 `EnableHmiReboot` **有意不在**那份清单里（清单类注释写明"这一类要的是补执行路径、不是标未生效"）⇒ 首版即使实现正确也**抓不到本轮缺陷的一半**；改为**从真实消费推导**（白名单外扫 `.字段名`）后两个字段都在集合里，且将来新死字段自动纳入 | 同上 §4.1 |
| 2026-09-17 | **阶段 12**：`bash scripts/ci-gate.sh` | **6 步全过**（末行「🎉 门禁全部通过（6 步）」；含固件 host 8 步） | 本地实测 |
| 2026-09-17 | **阶段 13**：登记表全量复核（机械抽出 33 文档 / 36 章节 / 22.5 KB 遗留条目，逐条核对代码） | **17 条**原登记为"未开工/待做"的实测早已闭环（逐条附 grep/读码证据）；仍开着的按四类分组 | `docs/2026-09-17-open-items-register.md` |
| 2026-09-17 | **阶段 13**：`dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误**（Avalonia 构建期编译 XAML ⇒ 1287 处 `{res:Loc}` 扩展解析均经构建验证） | 本地实测 |
| 2026-09-17 | **阶段 13**：全量测试（7 程序集） | **2601 通过 / 0 失败**（App.UiTests 153 → **159**、Core 1912、AI 313、Device 76 / Communication 75 / Simulator 44 / Hardware 22；本轮基线 2595 ⇒ **+6**） | 本地实测 |
| 2026-09-17 | **阶段 13**：新增 `LocalizationRefreshUiTests`(6) | 含**对照组**（旧索引器绑定确不刷新）与**回归守卫**（不得写回旧写法）；定向 **6/6** | tests/HmiEditor.App.UiTests |
| 2026-09-17 | **阶段 13** 负向验证 **3 组**（注入 → 期望红**且点名用例** → 还原） | **3/3 命中**：A XAML 退回索引器绑定 / B `LocExtension` 绑回索引器路径 / C 破坏按 key 缓存 | 同上 |
| 2026-09-17 | **阶段 13**：本地化迁移 | 脚本替换 **1287 处 / 45 文件**，断言"替换数 == 1287 且残留 == 0" | src/HmiEditor.App/**/*.axaml |
| 2026-09-17 | **阶段 13**：`bash scripts/ci-gate.sh` | **6 步全过**（末行「🎉 门禁全部通过（6 步）」；含固件 host 8 步） | 本地实测 |
| 2026-09-17 | **阶段 14**：`dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** | 本地实测 |
| 2026-09-17 | **阶段 14**：全量测试（7 程序集） | **2614 通过 / 0 失败**（App.UiTests 159 → **172**、Core 1912、AI 313、Device 76 / Communication 75 / Simulator 44 / Hardware 22；本轮基线 2601 ⇒ **+13**） | 本地实测 |
| 2026-09-17 | **阶段 14**：新增 `ProjectSettingsRuntimeUserUiTests`（13 项行为测试，含窗口 Show/Close 生命周期） | 定向 **13/13** 全绿 | tests/HmiEditor.App.UiTests |
| 2026-09-17 | **阶段 14**：写测试**抓出 2 个真缺陷**并修复 | ① 「表非空但零可用」显示成常规提示「共 0 个可用账户」（而该形态是导出级 Error；方法注释写着"要分开说"但分支不存在）；② `BindRuntimeUsers` 不重算提示 ⇒ 打开已配账户的工程显示"用户表为空、设备不校验凭据"（**两句话都与事实相反**，且只在装载那一刻错 ⇒ 人工验证必然漏掉） | `docs/2026-09-17-projectsettings-runtime-user-ui-tests.md` §3/§4 |
| 2026-09-17 | **阶段 14** 负向验证 **5 组**（注入 → 期望红**且点名用例** → 还原） | **5/5 命中**：A/B 分别复现上述两个缺陷；C 去掉第二层拷贝 / D 去掉第一层拷贝 / E 去掉「无改动不写模型」闸门 | 同上 §5 |
| 2026-09-17 | **阶段 14**：`bash scripts/ci-gate.sh` | **6 步全过**（末行「🎉 门禁全部通过（6 步）」；含固件 host 8 步） | 本地实测 |
| 2026-09-17 | **阶段 15**：`dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** | 本地实测 |
| 2026-09-17 | **阶段 15**：全量测试（7 程序集） | **2624 通过 / 0 失败**（App.UiTests 172 → **182**、Core 1912、AI 313、Device 76 / Communication 75 / Simulator 44 / Hardware 22；本轮基线 2614 ⇒ **+10**） | 本地实测 |
| 2026-09-17 | **阶段 15**：新增 `ProjectSettingsRoundTripUiTests`（10 项，判据=工程 JSON 操作前后逐字节比对） | 定向 **10/10** 全绿 | tests/HmiEditor.App.UiTests |
| 2026-09-17 | **阶段 15**：往返测试**抓出 1 个真缺陷**并修复 | `FindHardwareTargetItem` 无匹配回退第一项 + `SaveToProject` 持久化回退结果 ⇒ `HardwareTargetId == null` / 失效 Id 时**打开+应用就被绑到清单第一项**（产物 `// Target:` 从 generic 变成那块板、校验对象被换、失效 Id 被换成另一块板） | `docs/2026-09-17-projectsettings-roundtrip-fidelity.md` §3 |
| 2026-09-17 | **阶段 15** 负向验证 **4 组**（注入 → 期望红**且点名用例** → 还原） | **4/4 命中**：A 复现本轮型号缺陷 / B 恢复历史缺陷（应用时无条件覆盖已有屏幕尺寸）/ C 清空文本写成空串 / D 未选屏回退第一块屏 | 同上 §5 |
| 2026-09-17 | **阶段 15**：`bash scripts/ci-gate.sh` | **6 步全过**（末行「🎉 门禁全部通过（6 步）」；含固件 host 8 步） | 本地实测 |
| 2026-09-17 | **阶段 16**：`dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** | 本地实测 |
| 2026-09-17 | **阶段 16**：全量测试（7 程序集） | **2628 通过 / 0 失败**（App.UiTests 182 → **186**、Core 1912、AI 313、Device 76 / Communication 75 / Simulator 44 / Hardware 22；本轮基线 2624 ⇒ **+4**） | 本地实测 |
| 2026-09-17 | **阶段 16**：登记表 §4 第 18～20 项闭环 | ① 表外 LVGL 版本进候选 ⇒ 原样往返（不再打开即被升成 9.5）；② `ResolveColorDepthItem` 接上（死代码变活、下拉不再"空白却仍落盘一个值"）；③ 口令下限按 UTF-8 字节数判（与固件 `strlen` 同口径），模型注释与 zh/en 各 2 条文案一起改 | `docs/2026-09-17-config-editor-value-normalization.md` |
| 2026-09-17 | **阶段 16** 负向验证 **3 组**（注入 → 期望红**且点名用例** → 还原） | **3/3 命中**：A 表外版本不进候选 / B 色深退回裸赋值 / C 口令退回 `password.Length` | 同上 §5 |
| 2026-09-17 | **阶段 16**：`bash scripts/ci-gate.sh` | **6 步全过**（末行「🎉 门禁全部通过（6 步）」；含固件 host 8 步） | 本地实测 |
| 2026-09-17 | **阶段 17**：`dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** | 本地实测 |
| 2026-09-17 | **阶段 17**：全量测试（7 程序集） | **2630 通过 / 0 失败**（AI 313 → **315**、Core 1912、App.UiTests 186、Device 76 / Communication 75 / Simulator 44 / Hardware 22；本轮基线 2628 ⇒ **+2**） | 本地实测 |
| 2026-09-17 | **阶段 17**：`host_main.c` 单文件重编（`make objs/host/host_main.o`，Makefile 原参数） | 修前 **4 条** `-Wdiscarded-qualifiers`（`:83-86`）→ 修后 **0 警告**；产物 `objs/host/host_main.o` **22832 B** | 本地实测 |
| 2026-09-17 | **阶段 17**：登记表 §4 第 22～25 项处置 | ① 默认超时 **60 → 240 s** 并用算式钉住（**真缺陷**：慢中转下 8192 token 生成被掐断）；② `InRange(…,150,2000)` 绝对余量 → **两次不同窗口的差值**判据；③ **撤销一条不成立的登记**（#24：断言与 SUT 是同一个 `Path.Combine` 表达式，任何平台恒等）；④ #25 4 条 host 警告 → **0** | `docs/2026-09-17-timeout-flaky-judgment-and-stale-registration.md` |
| 2026-09-17 | **阶段 17** 负向验证 **2 组**（注入 → 期望红**且点名用例** → 还原） | **2/2 命中**：A 超时默认值退回 60 / B 首帧窗口不再跟随参数（#24 无可注入行为、#25 是编译期警告 ⇒ 不产生 NV 组） | 同上 §5 |
| 2026-09-17 | **阶段 17**：`bash scripts/ci-gate.sh` | **6 步全过**（末行「🎉 门禁全部通过（6 步）」；含固件 host 8 步） | 本地实测 |
| 2026-09-17 | **阶段 18**：Dock 拖拽可行性探针（临时文件，跑完即删） | Dock.Avalonia 12.1 的 DnD 是**指针事件**驱动（`OnPointerPressed/Moved/Released` + `DockDragContext`），**不是**平台 `DragDrop` API ⇒ headless 可驱动。实测 `Toolbox@LeftLowerDock → Toolbox@CenterDocDock`，`toolboxDescendants=151` | 本地实测 |
| 2026-09-17 | **阶段 18**：`dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** | 本地实测 |
| 2026-09-17 | **阶段 18**：全量测试（7 程序集） | **2636 通过 / 0 失败**（App.UiTests 186 → **192**、Core 1912、AI 315、Device 76 / Communication 75 / Simulator 44 / Hardware 22；本轮基线 2630 ⇒ **+6**） | 本地实测 |
| 2026-09-17 | **阶段 18**：新增 `DockDragRegressionUiTests`(3) + `ViewLocatorReparentContractTests`(2 用例/3 断言点) | 定向 **6/6** 全绿；含**判据双向自检** | tests/HmiEditor.App.UiTests |
| 2026-09-17 | **阶段 18** 负向验证（2 组） | A `PanelState` 判据改恒真 ⇒ **命中**自检负例；**B 退回原始缺陷判据 `CanResolve = Context is Control` ⇒ 按预期不命中**（拖拽用例照样绿）⇒ 据此定性"假设 D 在标签拖拽路径上不复现"，如实登记 | `docs/2026-09-17-dock-real-drag-feasibility-and-reparent-evidence.md` §4 |
| 2026-09-17 | **阶段 18**：`bash scripts/ci-gate.sh` | **6 步全过**（末行「🎉 门禁全部通过（6 步）」；含固件 host 8 步） | 本地实测 |
| 2026-09-17 | **阶段 19**：本地化硬编码清单（分类式扫描，剥离行/块/XAML 注释） | `.axaml` **14 条 / 2 文件**、`.cs` **226 条 / 27 文件**；登记里点名的举例（工具箱显示名 / 缓动选项）**早已本地化** ⇒ 失效登记 | `docs/2026-09-17-localization-hardcoded-inventory.md` §3 |
| 2026-09-17 | **阶段 19**：新增 `HardcodedLocalizationGuardTests`（5 条，含扫描器自检） | 每文件条数与基线**相等**判定（涨=新增硬编码 / 降=必须同步改小基线）+ 总数只减 + 注释剥离自检 + 排除项正负例 | tests/HmiEditor.App.UiTests |
| 2026-09-17 | **阶段 19**：首批本地化 14 条 `.axaml`（Splash 9 + AI 面板 5） | **8 个新键 + 6 处复用既有键**（`app.title`/`status.ready`/`gen.cancel`/`ai.image.save.svg/png`）；两文件硬编码中文清零；`SplashWindow.axaml` 补 `xmlns:res` | src/HmiEditor.App/Views/SplashWindow.axaml、AiChatPanelView.axaml |
| 2026-09-17 | **阶段 19**：`dotnet build` + 全量测试（7 程序集） | **0 警告 / 0 错误**；**2641 通过 / 0 失败**（App.UiTests 192 → **197**；本轮基线 2636 ⇒ **+5**） | 本地实测 |
| 2026-09-17 | **阶段 19** 负向验证 **3 组**（注入 → 期望红 → 还原） | **3/3 命中**：A 基线文件内新增硬编码 / B 本地化掉一条但忘了改小基线 / C 新建含硬编码的文件（探针已删） | 同上 §6 |
| 2026-09-17 | **阶段 19**：`bash scripts/ci-gate.sh` | **6 步全过**（末行「🎉 门禁全部通过（6 步）」；含固件 host 8 步） | 本地实测 |
| 2026-09-17 | **阶段 20**：判据从"按行豁免"改成"按语句/调用" | 两个方向都错被修掉：跨行 `throw new …(` 的中文**不再**计入；`Log($"[提示] {AppLocalization…}")` 的写死标签**改为计入**（旧规则整行豁免 ⇒ **藏了 13 条用户可见文案**，`LogBox` 是设备面板可见 `ListBox`） | `docs/2026-09-17-localization-hardcoded-inventory.md` §9.1 |
| 2026-09-17 | **阶段 20**：`EditorCommandCatalog` 38 → **0** | 删掉手写 `Description`（25 处实参，与 `Key`+`Modifiers` 重复 ⇒ 会静默说谎），新增**推导**的 `Chord`；冲突消息改纯 ASCII；11 条 `Describe` 入字典（**用户可见**：撤销/重做菜单项） | src/HmiEditor.App/Services/EditorCommandCatalog.cs |
| 2026-09-17 | **阶段 20**：`FindConflicts` 抽成可注入 `internal` 重载 | 原实现只读静态表（表里本无冲突）⇒ "无冲突"断言**没有能力红**；新增人造冲突用例 | tests/HmiEditor.App.UiTests/EditorCommandCatalogTests.cs |
| 2026-09-17 | **阶段 20**：新增 `LangKeyCoverageTests`（3 条，反射枚举常量） | 实测 **1782 个 `Lang` 常量全部解析成功**；补上"两本字典一起漏掉同一个键时 parity 依然绿"的洞（索引器找不到键会**回退成键名**） | tests/HmiEditor.App.UiTests/LangKeyCoverageTests.cs |
| 2026-09-17 | **阶段 20**：基线重算（判据修正后 + 本批本地化后） | **226 → 187**（26 个文件）；`TemplateManager` 1 → **4** 为量具修正（3 个控件名不再被连坐豁免）⇒ 新登记 §4 第 27c 项 | 同上 §9.2 |
| 2026-09-17 | **阶段 20**：`dotnet build` + 全量测试（7 程序集） | ~~**0 警告 / 0 错误**~~ **该读数无效**（增量构建的 no-op，见阶段 21 的更正）；**2650 通过 / 0 失败**（App.UiTests 197 → **206**；Core 1912 / AI 315 / Device 76 / Communication 75 / Simulator 44 / Hardware 22；本轮基线 2641 ⇒ **+9**） | 本地实测 |
| 2026-09-17 | **阶段 20** 负向验证 **5 组**（注入 → 期望**指名**用例红 → 还原） | **5/5 命中**：A 新增硬编码 / B 删字典键 / C 去掉推导里的 `Shift` / D 去掉冲突过滤（新旧用例**同时**红）/ E 判据还原成按行。每组先确认测试**确实执行**（防编译失败冒充"有能力红"），还原后 sha256 **逐字节一致** | 同上 §9.6 |
| 2026-09-17 | **阶段 20**：`bash scripts/ci-gate.sh` | **6 步全过**（末行「🎉 门禁全部通过（6 步）」；含固件 host 8 步） | 本地实测 |
| 2026-09-17 | **阶段 21**：`ConfigFieldInventory` 30 条悬停说明入字典 | 键**推导**自条目自身（`cfg.note.<类型名>.<字段名>`），删掉 `Note` 实参；消费点证据：`NotWired[].Note → NotYetEffective.Mark → ToolTip.SetTip`（**用户可见**） | `docs/2026-09-17-localization-hardcoded-inventory.md` §10 |
| 2026-09-17 | **阶段 21**：修掉"静态字段提前解析 ⇒ 语言被冻结"的隐患 | `NoteKey` 返回**键**、解析放在 `NotYetEffective.Mark`（清单是 `static`，静态初始化只跑一次） | src/HmiEditor.App/Controls/NotYetEffective.cs |
| 2026-09-17 | **阶段 21**：新增守卫 2 条 | `EveryEntry_HasATooltipReasonInBothLanguages`（两语言 + ≥25 条自检）、`TooltipReasons_ArePassedAsKeys`（扫源码钉住实参是键） | tests/HmiEditor.App.UiTests/ConfigFieldInventoryTests.cs |
| 2026-09-17 | **阶段 21**：基线重算 | **187 → 157**（25 个文件；`ConfigFieldInventory` 30 → 0） | 同上 §10.3 |
| 2026-09-17 | **阶段 21**：`dotnet build HmiEditor.slnx -c Release --no-incremental` | **0 警告 / 0 错误**（这才是"0 警告"的有效读数；增量构建下的读数为无效） | 本地实测 |
| 2026-09-17 | **阶段 21**：门禁改为**警告即失败** | 构建步加 `--no-incremental` + `: warning <码>` 判红并打印去重清单 | scripts/ci-gate.sh |
| 2026-09-17 | **阶段 21** 负向验证 **3 组** | **3/3 命中指名用例**：A 删 `cfg.note.*` 键 ⇒ 悬停说明守卫红；B 调用点由 `NoteKey` 改成 `Key` ⇒ 键传递守卫红；C 加回 `Tag(string)` ⇒ **门禁 exit 1** 且指名 CS0108（还原后 `cmp` 逐字节一致） | 同上 §10.4 |
| 2026-09-17 | **阶段 21**：全量测试（7 程序集） | **2652 通过 / 0 失败**（App.UiTests 206 → **208**；本轮基线 2650 ⇒ **+2**） | 本地实测 |
| 2026-09-17 | **阶段 21**：`bash scripts/ci-gate.sh` | **6 步全过**（末行「🎉 门禁全部通过（6 步）」；含固件 host 8 步；构建步已为"警告即失败"） | 本地实测 |
| 2026-09-17 | **阶段 22**：`UiOperationGuard` 的操作名由文案改成**键** | 消费点证据：`report($"{operation}失败: …")` → 视图状态行/**全局状态栏**（**用户可见**）；3 条模板入字典，12 个调用点改传 `Lang.UiOp*` | `docs/2026-09-17-localization-hardcoded-inventory.md` §11 |
| 2026-09-17 | **阶段 22**：新增守卫 3 条 | `EveryCallSite_PassesALocalizationKey`（源码契约，带"≥10 处调用"自检）、`RunAsync_ReportsTheLocalizedFailureMessage`（断言等于字典拼装结果且不含键名）、`RunAsync_Cancellation_IsNotReportedAsFailure` | tests/HmiEditor.App.UiTests/UiOperationGuardContractTests.cs |
| 2026-09-17 | **阶段 22**：修掉守卫自检里的**写死阈值** | 总量降到 142 时自检"条数 ≥150"反而报红 ⇒ 改成**与基线挂钩**（`>= Baseline.Values.Sum()/2`），阈值随进度一起下走 | tests/HmiEditor.App.UiTests/HardcodedLocalizationGuardTests.cs |
| 2026-09-17 | **阶段 22**：基线重算 | **157 → 142**（24 → **18** 个文件）：`UiOperationGuard` 3→0、`ProjectExplorerView` 3→0、`FaceplateLibraryView`/`ScriptEditorView`/`UserManagerView`/`PropertyPanelView` 各 1→0、`DeviceManagerView` 29→27、`DownloadToHmiView` 17→16、`AiIconDialog` 4→3、`AiChatPanelView` 8→7 | 同上 §11.4 |
| 2026-09-17 | **阶段 22** 负向验证 **3 组** | **3/3 命中指名用例**：A 调用点改回中文字面量 ⇒ 源码契约守卫红（并连带本地化守卫红）；B 模板键换成占位符数不符的键 ⇒ 行为守卫红；C 删 `ui.op.pick.hmi` 字典键 ⇒ `Lang` 键闭合守卫红 | 同上 §11.2 |
| 2026-09-17 | **阶段 22**：全量测试（7 程序集） | **2655 通过 / 0 失败**（App.UiTests 208 → **211**；本轮基线 2652 ⇒ **+3**） | 本地实测 |
| 2026-09-17 | **阶段 22**：`bash scripts/ci-gate.sh` | **6 步全过**（末行「🎉 门禁全部通过（6 步）」；构建步为"`--no-incremental` + 警告即失败"） | 本地实测 |
| 2026-09-17 | **阶段 23**：设备日志正文 43 条清零 | `DeviceManagerView` 27 → **0**、`DownloadToHmiView` 16 → **0**；新增 28 键 + 28 常量；`LogTagged(tagKey, messageKey, args…)` 辅助方法；`LogError` 段落名改 `Lang.DeviceLogTag*` 键；复用 `download.so.auto.found` | `docs/2026-09-17-localization-hardcoded-inventory.md` §12 |
| 2026-09-17 | **阶段 23**：新增 `LocalizedLogCallSiteTests`（2 条） | 实参个数 ↔ 字典占位符个数（`Format` 不符时**不抛异常**、Release 下 `Debug` 被编译掉 ⇒ 用户看到 `{0}`）；`SplitTopLevelArguments` 单测（**先于主判据抓到切分器丢字符串实参的错**） | tests/HmiEditor.App.UiTests/LocalizedLogCallSiteTests.cs |
| 2026-09-17 | **阶段 23**：门禁"警告即失败"**当场抓到自己的 `CS8620`** | `params object?[]` → `params object[]`；若没有上一阶段的 `--no-incremental` + 警告判红，它会像 CS0108 一样躲在增量构建后面 | src/HmiEditor.App/Views/{DeviceManagerView,DownloadToHmiView}.axaml.cs |
| 2026-09-17 | **阶段 23**：基线重算 | **142 → 99**（18 → **16** 个文件） | 同上 §12.4 |
| 2026-09-17 | **阶段 23** 负向验证 **2 组** | **2/2 命中指名用例**：A 给 `LogTagged` 去掉一个实参 ⇒ 占位符个数判据红；B 把一条日志改回中文字面量 ⇒ 本地化守卫红（并连带总数判据） | 同上 §12.2 |
| 2026-09-17 | **阶段 23**：全量测试（7 程序集） | **2657 通过 / 0 失败**（App.UiTests 211 → **213**；本轮基线 2655 ⇒ **+2**） | 本地实测 |
| 2026-09-17 | **阶段 23**：`dotnet build --no-incremental` + `bash scripts/ci-gate.sh` | **0 警告 / 0 错误**；**6 步全过**（末行「🎉 门禁全部通过（6 步）」） | 本地实测 |
| 2026-09-17 | **阶段 24**：`VariableManagerView` 先分类再动手 | **纯显示 25 条**入字典（新增 23 键，复用 `var.picker.ungrouped`）；**落盘数据标识 2 条刻意保留**（`VariableAlarmConfig.Name` 的默认值/兜底值，属第 27c 项） | `docs/2026-09-17-localization-hardcoded-inventory.md` §13 |
| 2026-09-17 | **阶段 24**：把"键 ↔ 枚举"配对提成 `internal` 表 + 新增 `AlarmOptionLocalizationTests`（6 条） | 覆盖一一对应 / 语义配对 / **标签确实由字典解析** / 两语言可解析且组内互不相同 | src/HmiEditor.App/Views/VariableManagerView.axaml.cs、tests/HmiEditor.App.UiTests/AlarmOptionLocalizationTests.cs |
| 2026-09-17 | **阶段 24**：基线重算 | **99 → 74**（`VariableManagerView` 27 → **2**，基线里写明为什么是 2 而不是 0） | 同上 §13.4 |
| 2026-09-17 | **阶段 24** 负向验证 **4 组** | **4/4 命中指名用例**：A 交换两个键的配对 ⇒ 语义配对红；B 删掉一个枚举的下拉项 ⇒ 覆盖面红；C 两个级别指向同一键 ⇒ 重复标签红；D **把键直接当标签用** ⇒ 第一版**全绿**（盲区），补断言后命中 | 同上 §13.3 |
| 2026-09-17 | **阶段 24**：全量测试（7 程序集） | **2663 通过 / 0 失败**（App.UiTests 213 → **219**；本轮基线 2657 ⇒ **+6**） | 本地实测 |
| 2026-09-17 | **阶段 24**：`dotnet build --no-incremental` + `bash scripts/ci-gate.sh` | **0 警告 / 0 错误**；**6 步全过**（末行「🎉 门禁全部通过（6 步）」） | 本地实测 |
| 2026-09-17 | **阶段 25**：`MainWindow` 22 条入字典 | 8 个操作键 + `status.file.not.found` + `status.theme.switched` + 8 个**撤销描述**键（撤销菜单项可见）；沿用字典既有 `undo.` 命名 | `docs/2026-09-17-localization-hardcoded-inventory.md` §14 |
| 2026-09-17 | **阶段 25**：**失败模板收成唯一实现** | `MainWindow.ReportOperationFailure` 与 `UiOperationGuard` 各写了一份逐字相同的 `"{操作}失败"`/`"{操作}失败: {原因}"` ⇒ 抽成 `FailureSummary`/`FailureDetail`，两处都调它；`ReportOperationFailure` 改收**键**（9 个调用点）；`$"打开工程失败: {ex.Message}"` 直接复用 `FailureDetail`（措辞逐字不变、不新增键） | src/HmiEditor.App/Services/UiOperationGuard.cs、MainWindow.axaml.cs |
| 2026-09-17 | **阶段 25**：契约守卫扩到第二个入口 | `EveryCallSite_PassesALocalizationKey` 纳入 `ReportOperationFailure`（排除方法声明的形参；自检 ≥20 处 `Lang.*`）；新增 `FailureFormatters_UseTheSharedTemplatesAndResolveTheOperationName` | tests/HmiEditor.App.UiTests/UiOperationGuardContractTests.cs |
| 2026-09-17 | **阶段 25**：留下的 2 条与基线 | `HmiResourceItem.SubCategory/Description` 会**存进资源库文件** ⇒ 与报警名/模板控件名同类，并入第 27c 项；基线 **74 → 52**（`MainWindow` 24 → **2**） | 同上 §14.3 |
| 2026-09-17 | **阶段 25** 负向验证 **3 组** | **3/3 命中指名用例**：A `ReportOperationFailure` 调用点改回中文字面量 ⇒ 契约守卫红（并连带本地化守卫）；B 共享模板换成占位符数不符的键 ⇒ 模板契约红（并连带行为用例）；C 撤销描述改回中文字面量 ⇒ 本地化守卫红 | 同上 §14.4 |
| 2026-09-17 | **阶段 25**：全量测试（7 程序集） | **2664 通过 / 0 失败**（App.UiTests 219 → **220**；本轮基线 2663 ⇒ **+1**） | 本地实测 |
| 2026-09-17 | **阶段 25**：`dotnet build --no-incremental` + `bash scripts/ci-gate.sh` | **0 警告 / 0 错误**；**6 步全过**（末行「🎉 门禁全部通过（6 步）」） | 本地实测 |
| 2026-09-17 | **阶段 26**：补一个**判据漏网**（假阳性） | `PreviewRuntimeService` 的诊断 sink 写在**属性初始化器**里 ⇒ 按行判据看不见 ⇒ 那条中文消息被判成"待本地化"；处置：改名 `Log` → **`DiagnosticLog`**（名字自己声明类别）+ 排除正则加它（**不**给诊断文案建字典键） | `docs/2026-09-17-localization-hardcoded-inventory.md` §15.1 |
| 2026-09-17 | **阶段 26**：把一处**死数据接上界面** | `ThemeInfo.Description` 唯一消费者是一条"断言非空"的测试、没有任何界面显示它 ⇒ 改 `DescriptionKey` + `DescribeTheme()`（**显示时**解析，避免静态表冻结语言）+ 挂成主题按钮**悬停提示**；Core 测试改成断言键能解析 | src/HmiEditor.App/Services/AppThemeService.cs、Views/SettingsDialog.axaml.cs、tests/HmiEditor.Core.Tests/AppThemeServiceTests.cs |
| 2026-09-17 | **阶段 26**：11 条小文件文案入字典 | 撤销描述 3、从站显示名 1、画布占位字 1（`DefaultTexts.ImagePlaceholder` **const → 属性**）、主题说明 3、AI 图标文件类型名 2 | 同上 §15.3 |
| 2026-09-17 | **阶段 26**：基线重算 | **52 → 40**（16 → **9** 个文件；剩下 40 条里 9 条是第 27c 项的落盘数据） | 同上 §15.5 |
| 2026-09-17 | **阶段 26** 负向验证 **3 组** | **3/3 命中指名用例**：A 去掉排除正则里的 `DiagnosticLog` ⇒ 本地化守卫红（证明规则才是让它通过的原因）；B `DescribeTheme` 改成原样返回键 ⇒ 主题说明用例红；C 画布占位字改回中文字面量 ⇒ 本地化守卫红 | 同上 §15.1 |
| 2026-09-17 | **阶段 26**：全量测试（7 程序集） | **2667 通过 / 0 失败**（Core 1912 → **1915**；本轮基线 2664 ⇒ **+3**） | 本地实测 |
| 2026-09-17 | **阶段 26**：`dotnet build --no-incremental` + `bash scripts/ci-gate.sh` | **0 警告 / 0 错误**；**6 步全过**（末行「🎉 门禁全部通过（6 步）」） | 本地实测 |
| 2026-09-17 | **阶段 27**：最后 31 条清完（40 → **9**） | `SimulatorBuildService`/`AiChatPanelView`/`Wiring.CanvasTools`/`BuildRunDialog`/`DesignCanvas.Editing`；**12 条复用既有键**（`cmd.desc.*` 5 条、`ai.image.save.*`/`ai.icon.filter.*` 4 条等） | `docs/2026-09-17-localization-hardcoded-inventory.md` §16.1 |
| 2026-09-17 | **阶段 27**：补一个**判据漏网**（正则字面量被误判） | `ErrorLineRegex` 是**模式**不是文案，翻译它会**改变匹配行为** ⇒ 排除项加 `Regex`；与 `DiagnosticLog`、多行 `throw new` 同族（判据看"行长什么样"，类别由**角色**决定） | 同上 §16.2 |
| 2026-09-17 | **阶段 27**：修掉一个**本来就是歪的**日志框 + 新增 6 条框宽断言 | 取消框内容行实测 **47 列** vs 边框 **44 列** ⇒ 按**显示列宽**补齐（`DisplayWidth`/`BoxedContentLine`/`AppendBoxedLog`）；`BuildRunDialogBoxTests` 断言"框内行宽 == 边框宽"与"两语言文案都装得进框" | src/HmiEditor.App/Views/BuildRunDialog.axaml.cs、tests/HmiEditor.App.UiTests/BuildRunDialogBoxTests.cs |
| 2026-09-17 | **阶段 27**：基线重算 —— **扫描面上已无待翻译界面文案** | **40 → 9**（9 → **4** 个文件），剩余 9 条**全部**是第 27c 项的落盘数据 | 同上 §16.4 |
| 2026-09-17 | **阶段 27** 负向验证 **3 组** | **3/3 命中指名用例**：A 去掉排除项 `Regex` ⇒ 本地化守卫红（证明规则才是让它通过的原因）；B 构建日志改回中文字面量 ⇒ 守卫红；C 框宽改用 `text.Length` ⇒ 中文那条红（证明按列宽算是必要的） | 同上 §16.3 |
| 2026-09-17 | **阶段 27**：全量测试（7 程序集） | **2673 通过 / 0 失败**（App.UiTests 220 → **226**；本轮基线 2667 ⇒ **+6**） | 本地实测 |
| 2026-09-17 | **阶段 27**：`dotnet build --no-incremental` + `bash scripts/ci-gate.sh` | **0 警告 / 0 错误**；**6 步全过**（末行「🎉 门禁全部通过（6 步）」） | 本地实测 |
| 2026-09-17 | **阶段 28**：**推翻第 27c 项的定性** | App 里**已有 9 处**把"新建实体的默认名"走字典（脚本/趋势/动画/连接/从站/模板/参数/仿真变量）⇒ 既有约定就是"默认名跟随 UI 语言"，这 9 条只是没跟上 ⇒ 补齐、撤销"产品决策"定性 | `docs/2026-09-17-persisted-content-language-decision.md` |
| 2026-09-17 | **阶段 28**：落盘默认名 9 条入字典 | `var.alarm.default.name`/`var.alarm.unnamed`、`resource.subcategory.ai`/`resource.desc.ai`、`ai.image.asset.name`、`tpl.startstop.name.*`/`tpl.motorind.name.*` | 同上 §5 |
| 2026-09-17 | **阶段 28**：审全部被排除字面量 ⇒ 修 2 处判据边界，抓出 **3 条被藏起来的用户可见文案** | ① 语句头以 `,` 为界（**兄弟属性不再连坐**）⇒ 2 条聊天文案；② 本地化调用的**非首实参**字面量**不豁免** ⇒ `Format("ai.gen.failed", "PNG 渲染失败")`；②只对本地化调用生效（否则多行 `throw new` 重新计入） | `docs/2026-09-17-localization-hardcoded-inventory.md` §17.2 |
| 2026-09-17 | **阶段 28**：守卫据"清零"重写 | 删掉逐文件基线字典；`NoUnclassifiedHardcodedChinese`（必须为 0）+ 自检换成 `Scanner_StillWorks_WhenExclusionsAreDisabled`（独立于基线） | tests/HmiEditor.App.UiTests/HardcodedLocalizationGuardTests.cs |
| 2026-09-17 | **阶段 28** 负向验证 **3 组** | **3/3 命中指名用例**：A 去掉 `,` 边界 ⇒ 兄弟属性连坐用例红；B 关掉"非首实参"规则 ⇒ 该用例红；C 模板控件名改回中文字面量 ⇒ `NoUnclassifiedHardcodedChinese` 红 | 同上 §17.2 |
| 2026-09-17 | **阶段 28**：全量测试（7 程序集） | **2674 通过 / 0 失败**（App.UiTests 226 → **227**；本轮基线 2673 ⇒ **+1**） | 本地实测 |
| 2026-09-17 | **阶段 28**：`dotnet build --no-incremental` + `bash scripts/ci-gate.sh` | **0 警告 / 0 错误**；**6 步全过**（末行「🎉 门禁全部通过（6 步）」） | 本地实测 |
| 2026-09-17 | **阶段 29**：版本标记**四者对应**变成可执行判据（新增 `TargetContractVersionTests` 12 条） | 版本串自洽 / 文档 ↔ 头文件 / `HMI_TARGET_ID` ↔ 目标插件 / ABI·LVGL ↔ 目标声明 / 固件要求 ∈ 编辑器版本清单；全部**文本解析**，不需交叉编译 | `docs/2026-09-17-version-marker-correspondence.md` |
| 2026-09-17 | **阶段 29**：修掉一处**真不兼容**（工程 LVGL 8.3 vs 板子 9.5） | `LvglConfig` 增加**可省略**的 `LvglVersion`/`AbiVersion`（省略=未声明；RK3506 刻意留空：仓库里无该板固件可对照）；新增契约层 `TargetValidation.CheckProjectLvglVersion`；两块内置目标在 `ValidateProject` 里调用，主版本不一致报 **Error** | src/Hmi.Hardware.Contracts/LvglConfig.cs、TargetValidation.cs、src/Hmi.Hardware.Builtin/*.cs |
| 2026-09-17 | **阶段 29**：版本清单单一来源 + 校验文案按 `Code` 本地化 | `SupportedLvglVersions` 从视图局部数组收进 `LvglApiCompatChecker`；App 侧显示校验结果时先查 `hw.issue.<code>`、查不到回落插件原文（**新发现**：插件文案不在本地化守卫扫描面内 ⇒ §4 第 27d 项） | src/HmiEditor.CodeGen/LvglApiCompatChecker.cs、src/HmiEditor.App/Views/ProjectSettingsView.axaml.cs |
| 2026-09-17 | **阶段 29**：复核勾掉 Phase 5 的"CI firmware host + 模拟器导出验证步骤" | 早已落地：`ci-gate.sh` 第 4 步跑固件 host 8 步；`verify.sh` 做 Golden 导出 + 真实 LVGL 编译 | 同上 §7 |
| 2026-09-17 | **阶段 29** 负向验证 **5 组** | **5/5 命中指名用例**：A `HMI_UI_ABI_VERSION` 3→4；B 版本串改 `1.1.1`（**连带**文档守卫）；C `HMI_TARGET_ID` 改名；D 文档版本改 `v1.0.9`；E 版本清单删掉 `9.5`（**连带**目标声明守卫）—— B/E 显示判据是**互锁**的 | 同上 §6 |
| 2026-09-17 | **阶段 29**：全量测试（7 程序集） | **2686 通过**；**1 次 load-sensitive 失败**（Core 1914/1915，**未抓到用例名**）⇒ 单跑 Core 两次 **1915/1915 绿**、`--filter GitWorkflowServiceTests` **8/8 绿（94 ms）** ⇒ 与阻断项 11 签名一致（跨程序集并行时的 flaky；门禁已将其单独跑） | 本地实测 |
| 2026-09-17 | **阶段 29**：`dotnet build --no-incremental` + `bash scripts/ci-gate.sh` | **0 警告 / 0 错误**（构建步"警告即失败"当场抓到我自己新测试里的 `xUnit2031` 并修掉）；**6 步全过** | 本地实测 |

| 2026-09-19 | **阶段 32（P1-3 B 段）**：新增 `SvgGradientCodeGenTests` | **+19**；Debug 全量 **2041 通过 / 0 失败**（基线 2022） | `tests/HmiEditor.Core.Tests/SvgGradientCodeGenTests.cs` |
| 2026-09-19 | **阶段 32**：负向验证 **3 组** | **3/3 精准命中、无附带**：① `Resolve` 忽略 `userTransform` ⇒ 1 红；② 端点改**画布绝对坐标** ⇒ **恰好 3 红**（与预测完全一致）；③ `TransformScale` 恒返回 1.0 ⇒ 1 红。恢复后 `grep NEGATIVE-CONTROL` 无残留 | `docs/2026-09-19-svg-gradient-and-transform.md` §5.2 |
| 2026-09-19 | **阶段 32**：`bash scripts/ci-gate.sh` | **7 步全过、`GATE_EXIT=0`**；Core 大队 **2033** + git 用例 **8**（= 2041，与 Debug 一致）、App.UiTests **249** | `.workbuddy/tmp/ci-gate-2026-09-19-svg-phaseB.log`（mtime `20:20:54`；四个改动文件 mtime ≤ `20:18:15`） |
| 2026-09-19 | **阶段 32**：`lv_conf.h` 探针（门禁覆盖不到这一份） | 真 LVGL 头链预处理：正控静默 / 负控（`grep -v` 掉该行）报 `#error tracked-config-lacks-flag` ⇒ 探针本身不空转 | 同专题 §5.3 |
| 2026-09-21 | 阶段 64：驱逐 143 个不可达字典键（两本字典 + `Lang.cs` + 补键表，连同 Lang 常量一起删） | 字典各 **2275 → 2132** 键 / 两本同集合 / **纯删行 −143 +0**；`Lang.cs` 常量 **1915 → 1772**；`VALUE_MAP` **−13 行**；回读残留 **0** | `.workbuddy/tmp/evict64.py`；备份 `.workbuddy/tmp/backup64-20260921-213442/` |
| 2026-09-21 | 本地化四组判据（键可达性 / 常量使用面 / 常量↔字典 / 格式调用点） | **22/22 绿** + **17/17 绿**；`UnusedBaseline` **1272 → 1129**；七条面 643 / 1113 / 26 / 30 / 19 / 388 / 12（驱逐前后逐项相同） | `.workbuddy/tmp/after-evict-2.log`、`after-evict-3.log`、`readings-64-evicted.txt` |
| 2026-09-21 | `bash scripts/ci-gate.sh`（阶段 64，提交**前**） | **7 步全绿 / `GATE_EXIT=0`**；0 警告 / 0 错误；**3028 + 8 + 2 = 3038**（与阶段 63 逐程序集逐条同数） | `.workbuddy/tmp/gate64.log`（mtime `21:37:50`） |
| 2026-09-21 | `bash scripts/ci-gate.sh`（阶段 64，提交**后**，`HEAD=103b9ed`，带 `HMI_READINGS_OUT`） | **7 步全绿 / `GATE_EXIT=0`**；0 警告 / 0 错误；**3028 + 8 + 2 = 3038**；读数文件 189 行 / 7,912 字节 / 11 块 —— 与阶段 63 的门禁读数**逐行 diff 只差 2 行**（M1 `1915→643`、M6 `113→388`，即本阶段所改的两条机制） | `.workbuddy/tmp/ci-gate-64-post.log`（mtime `21:41:58`）、`readings-64-gate.txt` |
| 2026-09-21 | 阶段 65：给 `UnusedBaseline` 那 1129 个常量按「担保其键的机制」分桶（新判据 `UnusedConstants_ClassifiedByKeyMechanism`） | **M2 828 / M6 274 / M3 18 / 无机制 9**（9 = `KeptUnreachable` 差异表，两侧交叉核对一致）；字典 **2132** 键中有常量 **1772**、**无常量 360** | `.workbuddy/tmp/readings-65-final.txt`（3 块）；反向缺口与工具干跑见 `sync65-dryrun.log` |
| 2026-09-21 | 阶段 65：收紧 M6 的语料 —— **剥注释**（只剥注释、不抹字面量，新判据 `M6Face_WithoutComments_IsReported`） | M6 面 **388 → 387**，其余六条面一个数没动；收益 **1 例** = `param.baud.rate`（两处出现点**全是文档注释**，其中一处就是 `LangConstantUsageGuardTests` 的类注释）；负控：只收紧不登记 ⇒ **2 红**（主判据点名该键 + 交叉核对报「常量侧 9 vs 差异表 8」），登记后 **19/19** 绿、扩到六组 **35/35** 绿 | `.workbuddy/tmp/buckets65-red.log`、`buckets65-green.log`、`buckets65-final.log` |
| 2026-09-21 | `bash scripts/ci-gate.sh`（阶段 65，提交**前**） | **7 步全绿 / `GATE_EXIT=0`**；0 警告 / 0 错误；**3030 + 8 + 2 = 3040**（`App.UiTests` **343 → 345**，+2 = 本轮新增两条用例；其余程序集逐条同数） | `.workbuddy/tmp/gate65-pre.log`（mtime `21:59:22`） |
| 2026-09-21 | `bash scripts/ci-gate.sh`（阶段 65，提交**后**，`HEAD=b11f936`，带 `HMI_READINGS_OUT`） | **7 步全绿 / `GATE_EXIT=0`**；0 警告 / 0 错误；**3030 + 8 + 2 = 3040**；读数文件 **8,911 字节 / 13 块**（阶段 64 是 11 块，+2 = 本轮新增两条读数）—— 与阶段 64 门禁读数**逐行 diff**：**新增 16 行（2 块）+ 仅 M6 一面 `388 → 387`**，其余逐字节相同 ⇒ 可达面零损伤 | `.workbuddy/tmp/gate65-post.log`（mtime `22:00:54`）、`readings-65-gate-post.txt` |
| 2026-09-21 | #52 落地：`scripts/sync_localization.py` 改成**默认干跑** + 「生成常量」加 `--add-consts` 闸；新增静态守卫 `SyncTool_DefaultsToDryRunAndGatesConstantGeneration` | 三种模式实测（无参数 ⇒ 干跑且跳过生成；只给 `--add-consts` ⇒ 仍干跑，仅预览 360 个；工作树只动被编辑的两个文件）；**负控 2/2 各自独立命中**（N1 退回旧形状 / N2 拆闸），还原 `sha256 bc8e6c50…` + `cmp` 无差异，复跑 **6/6 绿** | `.workbuddy/tmp/nc65.py`、`nc65-N1.log`、`nc65-N2.log`、`guard65-sync2.log` |
| 2026-09-21 | `bash scripts/ci-gate.sh`（阶段 65 的 #52 落地，提交**前**） | **7 步全绿 / `GATE_EXIT=0`**；0 警告 / 0 错误；**3031 + 8 + 2 = 3041**（`App.UiTests` **345 → 346**，+1 = 本轮新增的静态守卫；其余程序集逐条同数） | `.workbuddy/tmp/gate65b-pre.log`（mtime `22:14:31`） |
| 2026-09-21 | `bash scripts/ci-gate.sh`（阶段 65 的 #52 落地，提交**后**，`HEAD=a167473`） | **7 步全绿 / `GATE_EXIT=0`**；**3031 + 8 + 2 = 3041**；读数文件与**提交前那次逐行内容相同、只有块顺序不同**（`sort` 后 `diff` 无差异）—— 读数按 scope **追加**，而块的先后由测试派发顺序决定 ⇒ **同一次提交前后两次跑，块顺序不必一致**，比较时要先排序（阶段 64/65 的"跨阶段逐行 diff"恰好没撞上这个，因为那两次的派发顺序一致） | `.workbuddy/tmp/gate65b-post.log`（mtime `22:15:49`） |
| 2026-09-21 | 阶段 66：闭环 #50 —— `RoleTextKeys`（App 层唯一键推导）+ 8 个显示点接线；两本下拉条目改成 `ComboBoxItem{Content,Tag}` | 读数对拍（改前 → 改后）：条目 `HmiEditor.Core.Models.User.UserRole` → `ComboBoxItem(Content="操作员 (Lv.3)", Tag=role-operator)`；`NewRoleCombo` 选中框 `"HmiEditor.Core.Models.User.UserRole"` → `"操作员 (Lv.3)"`；`DetailRoleCombo` 选中框（无 TextBlock）→ `"管理员 (Lv.15)"`；角色列表 → `管理员 (Lv.15) \| 管理员 — 完全权限`；详情区 `""`/`""` → `"管理员"`/`"管理员 — 完全权限"` | `.workbuddy/tmp/readings-66-gate-pre.txt`（**16 块**，阶段 65 是 13 块，+3 = 本轮新增三条读数；`readings-66-final.txt` 是那三条各自单跑，3 块） |
| 2026-09-21 | 阶段 66：清退 `role.maintenance`（字典各 −1 行 + `Lang` 常量 + 补键表 + 差异表）、新增 3 个 `role.*.desc` 键 | 字典 `role.*` 家族 **4 → 6**（名 3 + 描述 3）；差异表 **9 → 5**、其中 `role.*` **4 → 0**；常量「无任何机制」桶 **9 → 5**；`UnusedBaseline` **1129 → 1125**；M1 面 **643 → 649**、M6 残差键 **396 → 392**（其余六条面一个数没动） | `.workbuddy/tmp/readings-66-before.txt`（1 块）、`readings-66-final.txt`（3 块）；手术脚本 `.workbuddy/tmp/dict66.py`（默认干跑 + `--write` + 备份 + 回读 6 项校验全绿）；备份 `.workbuddy/tmp/backup66/` |
| 2026-09-21 | 阶段 66：判据 `RoleNameLocalizationTests`（6 条）**负控 8/8** | **8/8 全是「断言命中」**（不是编译失败）：N1/N2 → 同源双向；N3/N4 → 键家族双向；N5 → 显示值来自字典；N6 → UI 端到端；N7/N8 → #53 接线（**报错点名 #53**）。逐条还原校验 **SHA256**；判据 6/6 绿、`dotnet build` **0 警告 0 错误** | `.workbuddy/tmp/negctl66.py`（严格版：必须匹配 `失败: N` + `错误消息:`，`build-fail` 判为**不算数**）、`negctl66c.log` |
| 2026-09-21 | `bash scripts/ci-gate.sh`（阶段 66，提交**前**，带 `HMI_READINGS_OUT`） | **7 步全绿 / `GATE_EXIT=0`**；0 警告 / 0 错误；**3037 + 8 + 2 = 3047**（`App.UiTests` **346 → 352**，+6 = 本轮新增六条判据；其余七个程序集逐条同数）；读数文件 **16 块** | `.workbuddy/tmp/gate66-pre.log`（mtime `22:46:18`）、`readings-66-gate-pre.txt` |
| 2026-09-21 | `bash scripts/ci-gate.sh`（阶段 66，提交**后**，`HEAD=3572ac0`，带 `HMI_READINGS_OUT`） | **7 步全绿 / `GATE_EXIT=0`**；0 警告 / 0 错误；**3037 + 8 + 2 = 3047**（与提交前那次**逐程序集逐条同数**）；读数文件 **16 块**，与提交前那次**排序后逐行完全相同**（`diff` 无输出） | `.workbuddy/tmp/gate66-post.log`（mtime `22:48:55`，**晚于**提交 `22:47:58`）、`readings-66-gate-post.txt` |
| 2026-09-21 | `bash scripts/ci-gate.sh`（阶段 66 **追加确认跑**，`HEAD=3c0f569`，docs-only 提交之后） | ⚠ **红了 1 项**：`Core.Tests` **2115/2116**，失败用例 `Phase3IntegrationTests.FullWorkflow_LoginConfigureAlarmTriggerLogAcknowledgeAudit_Integration`（「记录的值应超过报警阈值」，`:480`）。**判为 load-sensitive 假红、与本次改动无关** —— 该程序集源码在绿与红两次之间**未改一个字节**；串行全量 `Core.Tests` **5/5 绿**（2124）、带 filter 单跑 **3/3 绿**；失败那次用例自身只跑 **22 ms** ⇒ 是回调派发被拖后，不是用例变慢。已登记 **#55** | `.workbuddy/tmp/gate66-final.log`（mtime `22:51:29`）、`rate66.log`、`core-rate-{1..5}.log`、`p3-{1..3}.log` |
| 2026-09-22 | 修法 A 落地：`Phase3IntegrationTests.cs:480` 断言不再读 `samples[^1]`，改成「存在一条 > 100 的采样」 | 修改点 1 处（+13 / −2）；**负控 4/4 全部符合预期**：NC1（末尾追加全 0 占位）⇒ **绿**、NC1b（同一注入 + 旧写法）⇒ **红** （这一对才是 #55 的复现器）、NC2（150→50）/ NC3（150→**100** 边界）⇒ 仍红（证明判据没被改弱）；逐条还原校验 **SHA256** | `.workbuddy/tmp/nc55.py`、`nc55b.log`（4/4）；修前证据 `gate66-final.log`、`rate66.log`、`core-rate-{1..5}.log` |
| 2026-09-22 | 全量 `Core.Tests` 跑 **3 遍**（修法 A 之后） | **3/3 全绿**，每遍 **2124** 项；⚠ 这批绿**不单独构成**「不再随机红」的证据（修前串行也是 5/5 绿），依据是机制 + NC1/NC1b | `.workbuddy/tmp/core55-run{1,2,3}.log` |
| 2026-09-22 | `bash scripts/ci-gate.sh`（阶段 67，提交**前**） | **7 步全绿 / `GATE_EXIT=0`**；0 警告 / 0 错误；**3037 + 8 + 2 = 3047**（与阶段 66 逐程序集逐条同数） | `.workbuddy/tmp/gate55-pre.log`（mtime `10:27:13`） |
| 2026-09-22 | `bash scripts/ci-gate.sh`（阶段 67，提交**后**，`HEAD=9ae7299`） | **7 步全绿 / `GATE_EXIT=0`**；**3037 + 8 + 2 = 3047**；日志 mtime **`10:28:53`** 晚于提交 **`10:27:32`** ⇒ 时序成立 | `.workbuddy/tmp/gate55-post.log`（mtime `10:28:53`） |
| 2026-09-22 | 阶段 68：新增 `WindowProductionPathUiTests`（33 条）+ 夹具 `WindowDepot`，逐窗口真跑生产构造路径 | `dotnet test --filter FullyQualifiedName~WindowProductionPathUiTests` ⇒ **33/33 绿**（30 条 Theory + 3 条 Fact）；读数 127 行 / 31 块；`ValidationResultView` 实测 `_findings=count:0 → count:1`（旧口径 `null → 非空` 在这里是「两边 0 个 null 字段」⇒ 判据错、代码对） | `.workbuddy/tmp/winprod-test2.log`、`readings-68-pre.txt`（曾用名 `winprod-readings.txt`） |
| 2026-09-22 | 阶段 68：判据形状由「自有字段 null → 非空」升级为**形状签名**（`null` / `count:N` / `str:空·非空` / `非空`），且两份签名都在 `Show()` **之前**读 | 全量 30 条里**只有** `TwoPaths: false` 的两条报「签名变化：无」，其余 28 条各自有真变化 ⇒ 判据不再空转 | 同上读数文件；`TwoPaths` 两条 = `DeviceManagerView` / `StylePresetPicker` |
| 2026-09-22 | 阶段 68：**负控 4/4** | NC1（表里删一条）/ NC2（豁免表塞不存在的类型）/ **NC3（把 `Build` 换成无参构造 ⇒ 判据二红，日志含 `形状签名`）** / NC4（`TwoPaths` 标错）—— 全部**判红并命中期望子串**；基线（未注入）四条命令**全绿**；逐条还原 **SHA256 与原文逐字节相同** | `.workbuddy/tmp/nc-window-production-path.py`、`nc-window-production-path.log` |
| 2026-09-22 | `bash scripts/ci-gate.sh`（阶段 68，提交**前**） | **7 步全绿 / `GATE_EXIT=0`**；0 警告 / 0 错误；八程序集 **3070**（阶段 67 的 3037 **+33** = 新增判据类）+ 独占 **8 + 2** ⇒ **3047 → 3080** | `.workbuddy/tmp/gate68-pre.log`、`readings-68-gate-pre.txt`（跑之前 `loadavg` 已记在日志首行） |
| 2026-09-22 | `bash scripts/ci-gate.sh`（阶段 68，提交**后**，`HEAD=5e41472`） | **7 步全绿 / `GATE_EXIT=0`**；八程序集合计 **3080**（与提交前那次**逐程序集同数** —— 两笔提交都不改用例数）；日志 mtime **`12:42:45`** 晚于提交 **`12:41:45`** ⇒ 时序成立 | `.workbuddy/tmp/gate68-post.log`（mtime `12:42:45`）、`readings-68-gate-post.txt` |

## 变更记录

- 2026-09-20：创建工业化开发计划与状态基线（历史）。
- 2026-09-13：完成审查报告阶段 0（P0）修复，恢复真实质量门禁；数字改为现场实测值。
- 2026-09-13：完成阶段 1 软件可验证部分（BindingSemantics 三端统一、生成物追溯戳、编辑器全局异常处理与日志）。
- 2026-09-13：阶段 2 生成器治理——合并模拟器/设备两条绑定刷新路径并加对拍测试；`GenerateControlParameters` 拆分因机械提取不安全而回退，待用 Roslyn 完成。
- 2026-09-13：完成通信层并发/超时/生命周期整改（审查 P1-5），新增 9 项 loopback 回归测试。
- 2026-09-13：`MainWindow` 结构治理第一步——Dock 事件路由诊断（239 行）拆为独立服务。
- 2026-09-13：完成阶段 2 三项——`GenerateControlParameters` 词法感知拆分、Git 对话框服务化、测试策略升级（行为测试 + 生成代码语义断言，并修正校验器白名单）。
- 2026-09-13：完成 `MainWindow` 治理第二步——编译/导出流水线抽为 `BuildPipelineService`（产物状态一并移出），补 4 项行为测试；主窗口累计 5023 → 4434 行。
- 2026-09-13：修复审查最后一个 P0——触摸量程按板级 DTS 归一（800×480），抽纯函数 + 主机侧测试；消除 4 处文档矛盾。
- 2026-09-13：完成阶段 1 第 7 项——板级配置单一来源 `board_profile.h`；构建脚本去 `sed -i` 副作用（并加门禁）；SDK 关键配置缺失改为硬失败。
- 2026-09-13：纯软件侧——移除未使用依赖 `CommunityToolkit.Mvvm`（MVVM 决策入 ADR）；新增 LVGL API 对照表与校验器自洽性测试。
- 2026-09-13：`MainWindow` 治理第三/四步——`WireEvents` 992 行按域拆为 5 个 partial 文件；Modbus 预览运行时（246 行）纯平移至 `MainWindow.Preview.cs`（零行为改动，测试零改动通过）；主窗口累计 5023 → 3207 行。
- 2026-09-13：视图层 `async void` 用户可见化——新增 `UiOperationGuard`（异常必落盘 + 必回调界面，取消静默），迁移 16 处未受保护处理器，`async void` 51 → 35 且未受保护为 0；无本地状态行的视图回落到主窗口状态栏（`UiSink`）。
- 2026-09-13：生成代码测试断言升级——新增 `CSource`（按调用点结构匹配，排版无关且不误吃注释/字符串），迁移 281 处精确调用文本断言（全仓 211 → 3 处）；新增测试策略守卫 `TestAssertionStyleGuardTests`（含注入式自测）。
- 2026-09-13：补齐 UI 测试能力——新增 headless UI 测试项目 `HmiEditor.App.UiTests`（30 项）：视图装载/布局冒烟、真实鼠标与键盘模拟、`AppDialogs` 确认对话框分支、`PropertyPanelView` 属性写回与校验；MVVM ADR 的反转触发条件 ① 达成。
- 2026-09-13：预览运行时服务化——`MainWindow.Preview.cs`（305 行）抽为 `PreviewRuntimeService`（406 行），仿真器 Timer/真实设备连接/动作执行器的所有权与释放集中到一处，关停只调 `Dispose()`；新增 22 项行为测试（含释放语义与失败回退）。
- 2026-09-13：修正 UI 测试暴露的两处行为差异——① 元信息归一化改为装载时显式执行，不再依赖 `TextChanged` 在程序化赋值时是否触发；② 属性面板 51 处编辑器写回统一经可插拔校验器（保留程序化命令的直写例外）；新增 9 项 UI 测试。
- 2026-09-13：`MainWindow` 继续变小——快捷键（163 行）与画布右键菜单（78 行）两份重复 switch 合并为唯一命令分发器（`MainWindow` 3242 → 3086 行）；顺带修复两个既有缺陷：Ctrl+Shift+V 被 Paste 分支抢占、系统变量因 Id 带点号恒被解析为 Float。
- 2026-09-13：修复带点号变量名（PLC 符号名/CSV 导入）绑定解析不到变量导致不进标签表、预览恒空的问题；画布工具栏装配按域拆出（`MainWindow` 3086 → 2928 行）。
- 2026-09-14：**阶段 3（仓库卫生与工程化基线）**——根目录 18 个一次性产物归档到 `docs/archive/2026-08/`；删除死代码 `HmiEditor.Renderer`（src 14 → 13）；补 `global.json` / `.editorconfig` / 中央包版本管理（36 处版本属性收敛到 1 处）；测试临时目录加可用性探测与安全清理（本机 1785/1788 → 1788/1788，44 处裸 `Directory.Delete` 收口）；`scripts/ci-gate.sh` 去掉 `/dev/fd` 依赖（消除静默假门禁）；补根 `README.md`。全量门禁 4 步通过。
- 2026-09-16：关闭「**测试间歇性失败**」（2026-09-14 登记）——两条根因（定时器回调用固定 `Thread.Sleep` 等 / 本机 `/tmp` 仅 10 MB tmpfs 被写满）均已修复；判据为默认并行度**连跑 6 次全量、6/6 全绿**。见 `docs/2026-09-14-test-timer-flakiness.md`。
- 2026-09-16：**阶段 4（权限模型下沉到设备链路）**——编辑器侧的 `Permission` / `UserRole` / `SecurityConfig` 下沉为生成头 `generated_security.h`，固件 `hmi_actions.{h,c}` 接上会话与角色、动作回调带权限门；三个单一来源（动作→所需权限、枚举→C 宏名、生成头内容）收口；默认设计模式不发射门 ⇒ 历史导出产物逐字节不变。C# 37 条 + 固件 host 34 条断言，负向验证 6 组。见 `docs/2026-09-16-permission-model-sinking.md`。
- 2026-09-16：**阶段 5（导出模拟器工程链接自洽 + 判定策略下沉）**——补上 7 个只有 `extern` 没有定义的宿主符号（`var_raw_read` / `var_raw_write` / `hmi_show_alarms` / `hmi_show_trends` / `hmi_login` / `hmi_logout` / `hmi_require_permission`），新增**穷举式**链接契约守卫（从生成代码反推宿主依赖）；权限判定策略以 `static inline` 下沉进生成头，固件与模拟器都只剩转发；导航候选屏名收进 `ScreenNavTargets` 单一来源（固件孪生守卫）。顺带修掉一个**同类既有缺陷**：`NormalizeActionValue` 会把整数值发射成 `1f`（非法 C 浮点常量）⇒ 带「设置变量」动作的工程编译期即失败，而既有测试把 `100f` 写进了期望值。证据含 `nm` 符号级探针正/负例与 gcc 编译器级负例。见 `docs/2026-09-16-simulator-export-link-selfcontained.md`。
- 2026-09-16：**阶段 6（通道收帧判据：唯一实现 + 首字节宽限窗口）**——`TcpDeviceChannel` / `SerialDeviceChannel` 原来各持一份**逐字相同**的收帧代码，判据"从**写入时刻**起算静默"把"设备起步慢"误判成"命令无输出"（返回空串），迟到的回包再落进下一条命令的窗口（响应张冠李戴；门禁首跑表现为 `["", "ok:AAA\nok:BBB"]`）。修法：判据收成唯一实现 `CommandFrameCollector`（生产者只追加 + 打时间戳、**不判帧结束**；帧内静默从**最后一段数据**起算；首字节拆成独立宽限窗口且不小于静默期；帧状态每帧独立），并收掉两份重复的 `SanitizeOutput`。测试侧 `LineServer` 新增 `replyDelayMs`，把 flaky 转成**可构造输入**的确定性回归。溯源发现这是 `code-error-audit-2026-08-20` 那次修复的未完成部分（竞态未解决，只把误判窗口从 0 ms 挪到 300 ms）。负向验证 4 组。见 `docs/2026-09-16-channel-frame-completion-contract.md`。
- 2026-09-16：**阶段 7（运行时用户表的编辑器 UI）**——阶段 6 把凭据链路打通了，却**没有任何界面能配账户**（`SecurityConfig.RuntimeUsers` 只能手改工程 JSON，而手填哈希要先算 `Base64(SHA256×1000(salt‖pwd))`，等于配不了）。本轮把编辑区放进**工程设置 → 安全设置面板**（不新开对话框：用户表是工程级配置，且该面板已有密码策略/锁定策略等它的作用对象）；核心设计是**草稿语义的两层拷贝**（编辑期改副本 ⇒「取消」真的能取消；写回时再拷一层 ⇒ 模型不与草稿共享引用，否则点完确定再编辑仍会改到模型）。密码**点按即哈希**、界面只显示「已设 / 未设」；校验规则四条（空用户名 / 重名 / 角色表外 / 启用无密码）收进 `RuntimeUserValidator`，判据与固件 `strcmp` 同源（`StringComparison.Ordinal` —— 放宽成 `OrdinalIgnoreCase` 会把 `Op`/`op` 误报成重名、或放过 `ROLE-OPERATOR` 这种表外角色）。预置角色清单收敛到 `UserRole.BuiltInAll`（`SecurityConfigCodeGen.BuiltInRoles` 改为纯转发）并加守卫。顺带修掉阶段 6 的一处**错误注释**：空用户名条目并非「死条目」—— `hmi_login_submit` 不挡空串，`hmi_user_lookup("")` 会以 `strcmp("", "") == 0` 命中它。见 `docs/2026-09-16-runtime-user-editor-ui.md`。
- 2026-09-16：**阶段 8（会话超时端到端 + 审计落盘）**——盘安全设置面板 11 项时发现 `SessionTimeoutMin` / `AutoLogoutEnabled` / `LockScreenId` **UI 能配、默认就是开的、生成头里从来没有对应宏、固件也无实现** ⇒ **界面在撒谎**，且因为默认开而影响所有工程。设备端语义**照抄**编辑器 `AuthenticationService.CheckPermission`（「有人在问权限」本身就算活动 / 判据严格大于 / 超时即清会话+跳锁屏+拒绝本次判定），活动打点**复用已有接缝**（编辑器本来就没有输入事件钩子 ⇒ 这是同构而非妥协）。**判定入口的顺序**（评估超时 → 打点 → 判定）也立成契约 —— 两条顺序各对应一个真缺陷（评估跑到判定之后 = 把这次操作送出去；打点跑到评估之前 = 超时永不触发），而第二条两侧原本都没人守。`LockScreenId` 是 Id 而设备端按名字切屏 ⇒ 新增 `LockScreenTargets` 做**生成期解析**（回退目标是**登录屏**，顺带让不配它的工程也有正确行为）；派生开关 `HMI_SEC_SESSION_ENABLED` **下沉进生成头**，拒绝模拟器长出第二份判定。审计落 `/mnt/UDISK/hmi/audit.log`（超 256 KB 滚一代、先写串口再写文件），而 `EnableAuditLog` / `AuditLogStoragePath` **刻意不下发**（前者在设备端没有可作用的对象，后者是编辑器本机路径 ⇒ 发给设备必静默失败）；时间戳用墙钟、超时判定用单调 tick。顺带修正两处**方向乐观**的记错：四个字段「已下发未消费」实为**从来没下发**；`BuildSystem.cs` 那句「固件可通过 `#if HMI_TAMPER_DETECTION`…」是**从未实现的行为**。负向验证 9 组（首轮 3/8 未捕获，其中藏着一个**真漏洞**：`expire` 必须先于 `touch` 没人守）；全量 **2438/0**、门禁 5 步全过。见 `docs/2026-09-16-session-timeout-and-audit.md`。
- 2026-09-16：**阶段 9（RuntimeConfig 字段下沉）**——把阶段 8 的盘点手法扩大到全部工程配置：`RuntimeConfig` 的 **10 个字段全部有 UI**（5 个还能被 AI 工具 `update_runtime` 改），而**只有 `BootScreenId` 一个真的走到了设备**（`BuildSystem.cs` 下发的两个宏都来自 `SecurityConfig`；固件的 `runtime_config.h` 只是名字撞车）。按根因分三类：**A 描述本身错**（`WatchdogTimeoutMs` 的"MCU 喂狗复位"在 T113 Linux 用户态没有对应物；`EnableHmiReboot` 指向的系统变量 `$SYS.Reboot` 在编辑器里根本不存在）、**B 有现成固件位点**、**C 需先立功能**（无统一心跳帧/任务池/崩溃转储）。本轮做 B 类真闭环：新增 `RuntimeConfigCodeGen` → `generated_runtime.h`，`ScanIntervalMs` → `HMI_POLL_INTERVAL_MS`（非法值挡回 100 + 诊断注释）、`LogLevel` → `HMI_LOG_LEVEL` + 5 个级别常量，固件经 `__has_include` + `#ifndef` 缺省回退消费（老工程仍可编译、行为逐字节不变）。两个值**刻意**只有轮询周期提供回退数值 —— 级别常量在门槛关闭时整段被编译掉，给了就是**在设备端另立一份级别定义**。日志宏**独立成文件** `hmi_log.h`（刻意不 include lvgl.h）：原来的聚合头拉进 LVGL、host 测试没有 LVGL ⇒ 门槛链路在主机上根本编不过。**有意行为变更**：默认 Warning 生效后，19 处硬编码 `HMI_LOG("DBG: …")` 改用 `HMI_LOG_DEBUG`、默认静音。A 类**改描述不删字段**，并把「已闭环 3 / 未下发 5 / 描述错 2」三张清单写进类注释。**教训**：负向验证的扫描器自己会坏（先抓错 xUnit 汇总行、后匹配不上 Theory 带空格的块头），且坏法与"守卫无效"无法区分 ⇒ 必须给扫描器加双向自检。见 `docs/2026-09-16-runtime-config-sinking.md`。
- 2026-09-16：**阶段 10（「UI 在撒谎」五类根因的系统性收口）**——把阶段 9 只在 `RuntimeConfig` 一个类上做过的事扩到**全部工程配置 + 应用级设置**，按根因分五类一次收口。**A 描述本身错**（`GenerateDebugSymbols` 许诺的 J-Link/ST-Link 调试在 T113 上没有对应物）与 **A′ 注释与实现相反**（「固件可通过 `#if HMI_TAMPER_DETECTION` 启用 Flash CRC」**从未实现**，固件全库零引用）都**改描述不删字段**（删会破坏老工程 JSON 兼容），并把 `-g` 真的接上三处构建产物（默认 false ⇒ 产物逐字节不变）。**B 同一操作多套语义**：「新建屏幕」有 5 处各写各的默认宽高 ⇒ 收成唯一工厂 `ScreenFactory`（优先级：显式参数 > 工程默认 > 1024×600），并把 `AiScreen.Width/Height` 由 `int = 800/480` 改成 **`int?`** —— 过去 AI 不填尺寸会被静默当成 800×480、工程默认值根本轮不到。**C 重复建模（两套都死）**：编辑器偏好同时活在 `EditorConfig` 与 `SettingsService` 且**都没有消费点** ⇒ `EditorConfig` 降级为只读兼容载体，面板「编辑器设置」分类整块删除（XAML −95 行 + 11 处 handler + 2 个失效方法），四条偏好真正接线且**出厂默认等于原硬编码**（点距 10 / 显示网格 true / 吸附步长 5 / 吸附开关 true）⇒ 默认行为逐像素不变、改设置经 `SettingChanged` 即时生效。顺带发现网格那一块比侦察结论更严重：**两套都死 + 三处建模 + 三个不同数字**（编辑器设置 8 / 画布 XAML 10 / 吸附阈值 5），已明确「**点阵 10px 与吸附 5px 是两个数**」（合并会连带改掉拖拽精度）。**C 最隐蔽的缺陷**：`SettingsService` 反序列化 `object` 拿到的是 `JsonElement`，而 `Get<T>` 用 `is T` 判等 ⇒ **磁盘上每一个值都被判成类型不匹配、退回默认值**；更坏的是**打开一次设置对话框会把默认值灌回控件 → 触发 `ValueChanged` → 写回文件抹掉用户存的值**。修法 + `SchemaVersion`（=2）一次性迁移：把 v1 那个**从无消费点**的 `GridSize = 8` 对齐到画布真实值 10、用户真改过的保持不动（踩了一个坑：`int : long : double` 三元链的公共类型是 `double`，整数被装箱成 `double` ⇒ `is int` 判据静默失效，必须写成早期返回）。**D 下发了但无消费**：登记 30 个「面板上有、其实不生效」的字段（应用级 14 + 工程级 16）+ `NotYetEffective` 灰显标注，配**三向守卫**（清单 ↔ 字段真实存在 ↔ 白名单外零消费，外加清单 ↔ 面板标注的键与控件名双向一致）；两条判据定得很小心 ——「**写入面**」≠「**消费面**」（`AgentToolFactory` 把 AI 的值写进工程配置但无人读 ⇒ 算未接线）、`Control = null` 是**第三态**（键没人读但控件真生效，如 `Theme.AccentColor` 写的是 `AppThemeService.CustomAccent`），不区分会把能用的控件灰掉。**E 同一语义多处建模**：色深四处建模、其中两处**同名枚举**（CS0104）、面板候选项另手写一份、设备 JSON 又一份 switch ⇒ 改名消歧义 + `Enum.GetNames` + `Enum.TryParse + IsDefined`（只 `TryParse` 不够，`"99"` 也返回 true）。验收：全量 7 程序集 **2535 项**（Core 1900 / AI 276 / App.UiTests 142 / Device 76 / Communication 75 / Simulator 44 / Hardware 22；基线 2469 ⇒ **+66**）、负向验证 **5/5**、固件 host **8 步全过**。**门禁第 4 步红 1 条 = 阻断项 11**（既有 flaky，非本轮引入：Core 单跑 1900/1900 与 1899/1900 交替、定向 8/8 绿）；本轮顺带修掉一条**新撞出来的**假红（`ModbusTcpRuntimeDriverTests` 的 2000 ms 窗口是**测试自己的 helper** 塞的，属「判据用过窄的绝对余量」，与阶段 8 修过的 `SlowFirstByte_…` 同族），并把阻断项 11 的根因查到「**git 本身健康**（200 轮 0 失败、`init` 60 ms）而 **.NET 宿主的进程创建/等待被拖到秒级**」，窗口调大无效。见 `docs/2026-09-16-config-honesty-and-editor-preferences.md`。
- 2026-09-17：**阻断项 11 定性为「环境」并据此修复**——门禁第 4 步三跑三红的 `GitWorkflowServiceTests.InitializeRepository_SecondCall_Idempotent` 追到了根：**`git` 本身完全健康、`/tmp` 峰值仅 116 KB、与 .NET / spawn 方式 / 文件系统类型全都无关**；用**纯 bash 复刻**（把 `git init` 换成 `touch`、把 .NET 换成 shell）证明本沙箱对**首次出现的文件路径**收约 **60 ms**、且该开销**全局串行**（8 进程各建 20 个文件 ≈ 8 × 单进程墙钟，20 核全程闲置）⇒ `git init` 在新建目录恒定 1.2 s、大队灌满队列时涨到 4~10 s ⇒ 撞穿 `GitProcessWaitMs`(15 s) ⇒ 判据返回 false（该用例 43 s ≈ 三次 15 s 窗口被撞穿）。**已验证不是解药的两条**：调窗口（15 s 照样撞穿）、调 xUnit 并行度（20 / 8 / 4 / 2 → 墙钟 84 / 83 / 90 / 88 s 同量级 —— 吞吐由那条串行资源决定）。**修法**：门禁第 3 步拆两段，让 spawn git 的 8 个用例在**队列为空**时单独跑（8/8 绿 / 7 s），**判据一条未放宽**（排除后 Core 恰为 1892 = 1900 − 8）。同一次门禁还撞出**同族第二、三例**（`CommandFrameCollectorTests`）：① 延迟生产者用 `Task.Delay` ⇒ 续体排 ThreadPool、被「往新路径写文件」的阻塞 IO 占住 ⇒ 推迟过 800 ms 首字节窗口；② 三段数据各起一个线程 ⇒ 段间间隔 = 两次线程启动抖动**叠加**，而名义间隔 70 ms 对静默 100 ms 只留 30 ms 余量 ⇒ 改同一线程按时间轴依次投递 + 尺度取 **0.6 × 静默期**（余量 240 ms）。两条「确定性单元测试」的自我描述里都写着「不依赖主机负载」，实现却排 ThreadPool —— 属**描述与实现相反**的又一例。**一次自纠**：首版把「新目录」记为 93~107 ms，实为把 python3 进程启动 90 ms 算进了计时区间，改用 shell 的 `mkdir` 后是 **1 ms** ⇒ 收费的是**文件**、不是目录。新工具 `tools/probe-fs-newpath-cost.sh`（纯 shell，任何人都能重跑这个环境结论）；专题 `docs/2026-09-17-sandbox-newpath-serialization.md`。门禁拆分后**连跑两轮退出码 0**（134 s / 156 s）。
- 2026-09-17：**端到端验收复跑通过，「本项待复验」标注撤销**——`scripts/verify.sh`（Golden 工程导出 → 真实 LVGL CMake 编译 → 300 帧 selftest → CTest）自 2026-09-13 之后未复跑，本轮复跑暴露**三个各自独立**的阻塞：① 第 [3] 步把大队灌进那条串行队列（承接阻断项 11，同源修法已搬进来：Debug 下 Core **2 m 3 s → 1 m 27 s**、Device **1 m 35 s → 48 s**、Communication **1 m 19 s → 26 s**）；② **主阻塞**：`HmiEditor.App.UiTests` 在 **Debug 全量并发**下 **testhost 崩溃**（两轮分别跑满 **15 m 44 s / 21 m 39 s** 且日志无新输出 ⇒ 无 blame 时崩溃表现为「挂死」；同一命令加 `--blame-hang --blame-hang-timeout 90s` 后 **187 s** 即点名「测试主机进程崩溃」、通过 **92/142**、并列出「崩溃时运行的测试」**9 条（分属 9 个不同测试类）**、**无托管异常无堆栈**；对照实验：单独跑 **142/142 绿 / 12.5 s**、走 slnx 只匹配它 **EXIT=0 / 24 s**、Release 全量两轮全绿 ⇒ 判为「多个测试类并发使用单例 headless 会话」下的宿主稳定性问题，**未修、本轮只绕开**，登记**阻断项 12**）；③ 两套门禁跑不同 Configuration（门禁 Release、verify 原为 Debug）——同一个程序集一个配置全绿、另一个必崩，这个不一致因此显性化。**修法**：verify 第 3 步拆两段 + 步骤编号统一 `[n/7]` + 新增 `CONFIG`（默认 **Release**，与门禁对齐）+ 第 [7] 步补 4 条生成产物断言（`generated_security.h` / `generated_comm.h` / `generated_runtime.h` / `sim_config.h` —— 三条独立下沉链 + sim_config；原先只断言 3 个文件，而导出器实际产 **11 个**）；另修正 `ci-gate.sh` 注释里一个已自纠过的旧数字（`~95 ms/目录`）。**验证**：Release **连跑两轮退出码 0**（第 1 轮 392 s、第 2 轮 223 s），逐段读数一致 —— 大队 **2527 项全绿**、git 用例 **8/8 / 9 s**、Golden 工程 **2 screens / 36 controls**、真实 LVGL 9.5 CMake 全量编译 → `hmi_sim`(1286896 B) → selftest **PASS** → CTest **passed**。专题 `docs/2026-09-17-verify-end-to-end-revalidation.md`。
- 2026-09-17：**阻断项 12 的修法实验（已验证，未落地）**——只往输出目录放一个运行时配置 `xunit.runner.json` = `{"parallelizeTestCollections": false}`（**不改仓库任何文件**）后跑 Debug 全量：`App.UiTests` **142/142 绿 / 1 m 56 s**、全量 **173 s / EXIT=0**（同一条命令在并行时是 **187 s / EXIT=1 崩溃**）⇒ 根因确认为「多个测试类并发使用单例 headless 会话」，**修法有效**，代价也量化：该程序集 **1 m 20 s → 1 m 56 s**（全量约 **+35 s**）。**未落地**：门禁与端到端验收都跑 Release、Release 下不崩 ⇒ 不影响任何自动化流程，属「用约 35 秒换一类隐患的确定性」的团队共享成本，待维护者拍板（方案 A 程序集级 runner json + `CopyToOutputDirectory`，方案 B 给用 `UiTestHost` 的类加同一 `[Collection]`）。专题 §6。
- 2026-09-17：**撤回上一条的「修法有效」**——同日复验把方案 A **真落地**后，Debug 全量**仍然崩**（EXIT=1 / 307 s / 37/142）；而同一天的**清洁树、不带 blame** 基线反而**绿**（EXIT=0 / 107 s —— 与崩溃那次是同一条命令，只去掉 `--blame-hang`）。那两次「绿」的**正控也没过**：把 `xunit.runner.json` 的值故意写成字符串，`dotnet test` **没有报错** ⇒ 连「runner 真读过这个文件」都未被证明（xUnit **不会**自动拷贝它 —— 回退 csproj 声明后 `bin/` 里那份就没了，已核）。**方案 A 已回退**（不留没有证据的改动），阻断项 12 的判据收窄为「**仅带 `--blame-hang` 时复现**」，下一步做 blame × 方案 A 的 2×2（每格两跑 + 记录机器负载）。**教训**：`--blame-hang` 自己是**混淆变量**，「带 blame 崩 / 不带 blame 绿」**不是**「并发 vs 配置」的因果 —— 两侧必须同一条命令；而上一版结论没做正控就写「已验证」，这笔账是要还的。见专题 §6.2 / §6.3 与「当前阻断项」第 12 条。
- 2026-09-17：**修掉 Avalonia 资源目录重复键**（Release 门禁那 12 条 `ArgumentException: An item with the same key has already been added. Key: /Assets/Icons.axaml`）——`HmiEditor.App.csproj` 的 `<AvaloniaResource Include="Assets\Icons.axaml" />` 与 Avalonia 12.1.0 的默认 glob `<AvaloniaXaml Include="**\*.axaml" ... />`（只判 `EnableDefaultItems`；这个版本里**没有** `EnableDefaultAvaloniaItems` 这种东西）把**同一个文件声明成了两种 item**；而 `AvaloniaBuildTasks.targets` 的 `GenerateAvaloniaResources` 会把 `@(AvaloniaXaml)` 折进 `@(AvaloniaResource)`，于是同一条路径在 `@(AvaloniaResource)` 里出现两次，中间产物 `obj/<cfg>/net10.0/Avalonia/resources` 的索引区里就有两条 `/Assets/Icons.axaml`（实测 49 条里有 2 条，`off`/`size` 完全相同，且 `size` 之和比载荷恰多 9707 字节 —— 载荷去重、索引不去重），而运行时 `Avalonia.Platform.Internal.AssemblyDescriptor` 用 `index.ToDictionary(GetPathRooted, …)` 装这份索引 ⇒ 重复键抛异常、`AppBuilder` 起不来（`StandardRuntimePlatformServices.Register ← SetupUnsafe`，表现为 UI 测试成片失败）。**平时不炸**是因为 XAML 编译步骤会用编译条目（`Build:` / `Populate:` / `NamespaceInfo:`）盖掉原始条目；一旦增量构建把这一步错开（例如单独 `dotnet build src/HmiEditor.App/HmiEditor.App.csproj`），程序集里就会留下带重复键的原始目录。**修法**：删掉那条冗余声明（全仓仅此一处；`App.axaml` 里的 `ResourceInclude Source="avares://HmiEditor.App/Assets/Icons.axaml"` 由编译期解析成编译条目，不依赖原始条目）。**验证**：索引 **49 → 48 条、重复消失、载荷 865358 不变**；`dotnet build -c Release` 0 警告 0 错误；Release 全量 **EXIT=0 / 121 s**；门禁 **EXIT=0 / 330 s**（**归属更正**：初稿写的 `252 s` 出自 `planA-run.log:1255` 的 E3 —— 那一跑在 **19:38**、当时**方案 A 仍落地**、且**比本修复落地早 10 分钟**，只能说明「当时门禁恰好是绿的」，不是本修复修好了门禁；干净树门禁读数见「验证记录」）。判据工具 `.refactor-tmp/parse_avares_catalog.py`（自带 4 条自校验，能逐条列出索引）。同轮顺带把 `TestResults/` 补进 `.gitignore`（VSTest 缺省输出目录，跑一次 blame 就多出一个未跟踪目录，干扰「工作区是否干净」的判断）。
- 2026-09-17：**更正一处文档归属错误，并补上真正的证据**——`bash scripts/ci-gate.sh` 的 **EXIT=0 / 252 s** 被写进了三处文档，其中两处（`docs/2026-09-17-avalonia-resource-duplicate-key.md` 的验证表、本文件的验证记录）把它当成「Avalonia 资源目录重复键修复」的验证。按 **mtime + git** 回查：那一跑出自 `.refactor-tmp/planA-run.log:1255`（**19:38** 跑完，当时**方案 A 仍落地在仓库里**），而本修复落地在 **19:48**、提交在 **20:03** ⇒ **它比修复早 10 分钟**，对一批**间歇出现**的失败毫无说服力。本轮在该修复的**干净树**（HEAD `97d122f`）上补跑门禁：**EXIT=0 / 330 s**（20:38，`.refactor-tmp/gate-check.log`，末行「🎉 门禁全部通过（**6 步**）」；大队 2527 全绿含 `App.UiTests 142` + git 8/8 + 固件 host 8 步）。顺带发现基线头把门禁步数写成 **5 步** —— 第 3 步拆两段之后实际是 **6 步**，而门禁**末行自己就会打印步数**，一并更正。**教训**：从同一批日志里顺手抄读数时，必须先核对**时间顺序** —— 「有一次绿」不能当作间歇缺陷的修复证据。
- 2026-09-17：**阻断项 12 的判据第二次改写 + 正控反转 + 修法换向**——① **正控反转**：`xunit.runner.json` **确实被读取**（`diagnosticMessages:true` ⇒ 输出 239→**317** 行；`methodDisplay:"method"` ⇒ 带类名用例名 **142→0**，两个独立可观测变化）⇒ 此前「值写成非法类型不报错 ⇒ 没被读取」的推论作废，但「方案 A 无效」的**结论**反而站稳（配置当时确实生效着）。② **判据**从「仅带 `--blame-hang` 才复现」改为「**进 Debug 全量就间歇出事，复现率 ≈ 2/3**」：同一条无 blame 命令连跑两轮 → 一次 `App.UiTests` **142/142 绿 / 1 m 56 s**、一次**挂死**；单独跑该项目恒定全绿（10~20 s）。**挂死现场签名**（首次抓到）：**209 线程 `futex_wait`、零 CPU、testhost `State: S`** ⇒ **僵死不是空转**；挂死那次其余 6 个程序集全绿、只有 `App.UiTests` 不报汇总。③ **修法换向**：不再赌运行时配置文件，改用 xUnit 编译期生效的集合语义（`[CollectionDefinition(..., DisableParallelization = true)]` + 13 个类加 `[Collection(...)]`，仓库已有同款惯用法）；**代价≈0**（单跑仍 142/142 / 10 s —— 142 项本来就都在同一 UI 线程上串行）。**2×2（blame × 方案A）作废**（两个前提都不成立）；正确的问题是**复现率**。另登记**新 flaky**：`AlarmEngineTests.Engine_TriggerThenReturn_CreatesReturnedRecord`（`Expected: Returned / Actual: Active`）。见专题 §6.3 / §6.4 / §7。
- 2026-09-17：**集合修法实验的结论：证据不足，已回退**——把 13 个用 `UiTestHost` 的测试类收进同一个 xUnit 集合（`[CollectionDefinition(..., DisableParallelization = true)]`）后，同一命令连跑 4 轮：**H1 挂死**（607 s；现场签名**变了**：`App.UiTests` 的 testhost **已消失**、只剩 wrapper `do_wait`，**没有**修法前 R2 那 209 个 `futex_wait`）／**H2** 1 条 Device flaky（`App.UiTests` 仍 **142/142 绿 / 1 m 54 s**）／**H3、H4 全绿**（**142/142 / 1 m 58 s / 2 m**）。修法前 ≈ **2/3** 失败、修法后 **1/4** ⇒ **N=4 不足以声称有效**（p=2/3 时 4 跑里至少 1 次失败的概率 98.8%，却只观察到 1 次）。**已回退**（不回退就等于把「方案 A」那笔账再记一遍），机制假说**收回半句**：「多个测试类并发使用单例 headless 会话」不足以解释它。同轮另逮到一条 **Device** 侧 flaky：`CommandFrameCollectorTests.…FirstByteWindow…` 的 `Assert.InRange(…, 150, 2000)`（绝对余量上界那一族，见专题 §8 遗留）。
- 2026-09-17：**判别实验：挂死不需要「别的测试程序集」在场**——单跑 `App.UiTests`（本来恒定绿 10~20 s）+ **6 个外部进程持续造新路径**（`.refactor-tmp/storm-test.sh`；这个组合里**没有别的测试程序集、也没有别的 testhost**）：对照组（无风暴）**exit=0 / 20 s**，而 **S1、S2 两次全挂死**（120 s / 124 s 判定，输出都停在 **0 字节**）。现场（S1）与全量那两次**完全不同**：全局 wchan 直方图**只有 ~28 个线程**（9 `futex_wait` / 6 `anon_pipe_read` / 4 `do_wait` …）、`pgrep -f HmiEditor.App.UiTests` **零命中**（「testhost 的线程名 + wchan」整节为空）、进程表里只有 `dotnet test` 而**没有 `vstest.console` / `testhost`** ⇒ **测试宿主根本没起来**。⇒ 本沙箱至少存在一个**与 headless 会话无关**的失败入口：**串行的新路径资源被占满 ⇒ 连测试宿主都起不来**（阻断项 11 同族）。两条**边界**必须一起记住：① S1/S2 与全量那两次（testhost 在、209 `futex_wait`、输出 2195 B）**不是同一签名** ⇒ 不合并、全量那条成因**仍未定位**；② 6 个 worker 是**人为饱和**，只证明这条资源的**充分性**，不等于全量那两次就是这个原因。另：`0 字节` 这个读数本身**可能只是块缓冲**（重定向到文件的 stdout 是块缓冲），单独不能证明「一步没走」—— 起作用的是「无 testhost + 全局 ~28 线程」。⇒ **不变的一条**：本沙箱里 **Debug 全量不可作为判据，只认 Release**。
- 2026-09-17：**阶段 11（LLM 流式响应：SSE 打通到界面）**——承接 P2-5 / D9（「AI 无流式响应 —— grep `stream` **0 命中**」）。症状不是"少个开关"，而是**接口形状决定了写不出来**：`ILlmProvider.ChatAsync` 返回完整响应、中途没有任何出口；Agent 多步任务至少还有工具步骤在动，而**单次最长调用**（生成屏幕/模板/脚本/图片，复杂画面 30~120 s）全程只有一条静止的「正在生成, 请稍候…」。修法分四层：**① 能力接口**（新增 `IStreamingLlmProvider`，独立于主接口 —— 流式是能力不是契约，加进 `ILlmProvider` 会逼每个实现提供"假装流式"的桩且调用方无法分辨真伪；不支持就回退，行为与今天完全一致）；**② 切帧与语义分离**（新增 `SseReader` 统一 OpenAI/Claude 的行语法，两处各写一份必然分叉 —— 阶段 6 收帧判据、阶段 9 回退数值都栽在这里）；**③ 单一调用入口**（`LlmChat.WithProgressAsync` 收「支不支持流式 → 走哪条路 → 非流式补一次回调」三条判据，**不变量：传了观察者就至少收到一次回调**，否则调用方得为真/假流式写两套 UI；首版我在 `AgentEngine` 里写了一份私有副本，随生成器接线一并收口）；**④ 两个 Provider 真流式 + 全路径接线**（OpenAI `stream:true` + `stream_options.include_usage`，不加它 token 读数在流式下**凭空消失**；Claude **结构化输出时正文取 `input_json_delta` 而非 text block** —— 取错会让结果恒为空且不报错，测试专门钉住；5 生成器 + `AiOrchestrator` + `AiPreviewManager` + `AgentEngine`/`AgentHost` 全部加可选 `IProgress<string>? textProgress`，放 `ct` 之后以**不破坏既有位置参数调用**）。界面用 `ChatEntry.LivePreview`（**尾部 600 字** + 「已接收 N 字」，节流 120 ms 重绘，工具步骤到达即撤下）。**关键一条自纠**：首轮负向验证注入「去掉降级时的 `emitted` 检查」**没能让任何用例变红** —— 追下去发现那是**我自己新写的死代码**（`IsRequestShapeError(ex) && !emitted` 按构造不可达：非 2xx 在正文之前抛，正文读取期只抛 `IOException`/`OperationCanceledException`），而我的类注释把它写成了"防止正文重复"的**主动机制**，正是本项目反复吃过亏的「没被验证过的行为声明」；**删掉不可达分支**（与 `ViewLocator` 死工厂分支同一把尺子），把"流中途失败"改成**真实可达**行为（`{"error":…}` 帧 / `error` 事件 → 抛错、保留已吐正文、不重试）并用 NV-D/E 钉住。验收：**2577/0**（基线 2535 ⇒ **+42**：AI 276→**313**、App.UiTests 142→**147**）、`ci-gate.sh` **6 步全过**、负向验证 **6/6 命中**、本地化 +2 键。范围边界：**并行 tool_call 未做**（属独立架构改动，与流式无耦合）、图标路径未接观察者、无真实网关联调。见 `docs/2026-09-17-llm-streaming-sse.md`。
- 2026-09-17：**阶段 12（配置诚实性收口二：导出闸门 + AI 工具描述）**——阶段 10 把「面板上有控件、其实不生效」的 30 个字段登记成数据并加了灰显；本轮补**同一类缺陷另外两个没被覆盖的入口**。**缺陷一：运行时用户表的校验没接进导出闸门**——`RuntimeUserValidator`（阶段 7）四条规则写好了、**唯一消费点是设置面板的提示**，而导出路径完全不知道它存在（`MainWindow` 的导出/模拟器导出**已经有** `GeneratedCodeValidator` + `HasErrors` 闸门）。后果不是"少个账户"而是**设备不可用**：设备端 `hmi_actions.c` 在 `HMI_SEC_USER_COUNT > 0` 时**刻意不授权**（否则"点一下登录"就以默认角色进入会话、凭据校验被完全绕过）⇒ **表非空但没有一条可用**时任何人建立不了会话、所有权限门**永久拒绝**（现场表现「HMI 点了没反应」，而归因成本极高——面板上那几行红字在导出后就被忘了）。修法：新增 `FindingCategory.RuntimeUser` + `ValidateRuntimeUsers`，**直接复用** `RuntimeUserValidator`（一条规则都没重写，重写会长出"面板说没事、导出说有事"的分叉）；定级**逐条对应设备端真实后果**——`UnknownRole`（`hmi_role_lookup` 返回 -1）/`EnabledWithoutPassword`（哈希解析失败即拒绝）以及**整表 `CountUsable == 0`** 为 Error（拦导出），`EmptyUsername`/`DuplicateUsername` 为 Warning；**表为空时刻意什么都不报**（那是"没配运行时用户表"的既有形态，报错会拦下所有老工程）。**缺陷二：AI 工具在广告没有执行路径的参数**——`update_runtime` 的描述与 schema advertised 了 `watchdog_timeout_ms` / `enable_hmi_reboot`，而这两个 `RuntimeConfig` 字段的**唯一落点是写入面**（AI 工具 + 设置面板）、**没有任何读取方** ⇒ 模型"照着描述改一个死键"，回给用户的却是「已更新运行时配置」。这是"UI 在撒谎"的同族，只是**撒谎的界面换成了 AI 工具描述**；处置选**拿掉参数**（参数是工具接口、不是持久化数据，拿掉不破坏老工程 JSON；字段与面板控件原样保留），描述里用中文说明"这两项不可改"但**不出现 snake_case 参数名**（后者会继续诱导模型）。**守卫：判据从"真实消费"推导，不抄清单**——对 `RuntimeConfig` 每个属性扫产品源码（白名单＝写入面/清单/声明文件），断言"广告的参数 ∩ 无读取方 = ∅"。**首版守卫依赖 `ConfigFieldInventory.NotWired` 反推，测试立刻红了，而且红得有价值**：`EnableHmiReboot` **有意不在**那份清单里（清单类注释写明"只被 AI 工具写入的字段……需要的是补执行路径，不是标未生效，故不入表"）⇒ **首版即使实现正确也抓不到本轮缺陷的一半**；改为从真实消费推导后两个字段都在集合里，且将来任何新死字段会被自动纳入——再次印证**判据的来源比判据本身更重要**。另加一条刻意设计的守卫：`GeneratedCodeValidator` 的定级 `switch` 有 `_ => Warning` 兜底 ⇒ 新增一种 `RuntimeUserIssueKind` 会**静默变成 Warning（不拦导出）**，故用显式"期望定级表"把定级逼成**必须做的决定**并反向检查表里没有已删除类型。验收：全量 **2595/0**（本轮基线 2577 ⇒ **+18**：Core 1900→**1912**、App.UiTests 147→**153**）、负向验证 **7/7 命中**（4 组导出闸门 + 3 组工具守卫）、端到端验收 `verify.sh` **退出码 0**（纯 C# 改动未触及生成链，属回归确认）、本地化 +1 键。范围边界：`EnableHmiReboot` 的面板控件仍未标注（**清单既有设计决定**，不是本轮遗漏）、两个死字段本身没删（删会破坏老工程 JSON）、用户表校验**不覆盖口令强度**（只能看到哈希、看不到长度，判不了——不伪装成已校验）。见 `docs/2026-09-17-config-honesty-export-gate-and-ai-tool.md`。
- 2026-09-17：**阶段 13（登记表全量复核 + 本地化即时刷新）**——承接上一轮末尾的判断（「登记的遗留项已大面积过期，下一轮应先做登记表全量复核」）。**方法**：把 `docs/` 下**全部**「遗留/未做/待办/未开工/范围边界/后续」章节机械抽出（33 个文档 / 36 个章节 / 22.5 KB），**逐条**用现场证据（grep / 读码 / 跑测）核对代码现状 —— **不看文档自己的状态标记，只看代码**。**复核结果：17 条早已闭环却仍写着"未开工/待做"**：`CodeGenerator` 按域拆分（现 150 行 + 12 个 partial）、两份 `GetPathCCode` 统一（`AnimationPathHelper` 唯一实现，`AnimationPathType.cs:46` 注释记载"曾有三份"）、`sim_refresh_bindings` 的 `GetHashCode()` 依赖（0 命中）、`LV_IMAGE_ALIGN_COVER` 死代码（0 命中）、`VariableSimulator` 的 `TimeProvider` 注入（`:22/:52` 已走单调轴，测试用 `FakeTimeProvider`）、`GeneratePointerDeclarations`/`AppendPointerValueCallbacks` 已走 `EnumerateRenderableControls`（展开后清单）、`ParseControlType` 的 `"Screen"`（函数已不存在，`AiSchemaEnums.cs:51` 显式拒绝）、`HmiDeployService` 固定临时文件名、`run_host_tests.sh` 产物清理（`:37-46` 有清理清单）、`AlarmEngineTests` 的 `SpinWait`（零残留）、`IMG_PATH_`/`FONT_PATH_` 宏重复（`CResourceDeclarations` 唯一实现）等 ⇒ **判断某项是否还开着，唯一可靠来源是代码，不是登记表**；产出权威登记表 `docs/2026-09-17-open-items-register.md`（已闭环 17 条 + 仍开着按"需产品决策 / 需先立功能 / 软件侧可验证 / 环境残留"四类分组，每条附证据）。**从复核出的真缺口里实施一项：语言切换即时刷新**（`docs/localization-summary.md` §五.2）。实测**比原登记更严重**：不只是"`LocalizationService` 没实现 INPC"——**Avalonia 的索引器绑定根本不订阅 INPC**（索引器节点不可观察），所以"补上 INPC"修不好它；全仓 **1287 处** `{Binding [key], Source={x:Static res:AppLocalization.Service}}` 实为不可观察，而 `MainWindow.OnLanguageChanged` 靠**重建 Dock 面板**兜底 ⇒ **菜单栏/工具栏切语言后一直是旧语言**。修法：新增 `LocExtension`（`{res:Loc key}`）把绑定改成"绑到会通知的 `LocalizedString.Value`"，1287 处 / 45 文件一次性迁移（迁移前先验证三个前提：模式完全统一无附加参数、1099 个键全部匹配 `[a-zA-Z0-9._-]+`、所有文件已声明 `xmlns:res`；脚本断言"替换数 == 1287 且残留 == 0"）；`AppLocalization.Get` 改为**按 key 缓存实例**，顺带修掉一个"订阅只增不减"的潜在泄漏（`LocalizedString` 构造时订阅静态事件，在 `DataTemplate` 里每个实例化行加一个，缓存后上界 = 键数量）。**关键方法**：先在**同一个 headless 宿主**里做对照实验再改产品代码——旧索引器绑定切语言后**不变**、既有 `LocalizedString.Value` 旁路**会变**（后者证明宿主本身能观察到刷新，否则"不变"可能只是宿主没泵，把测试问题误判成产品问题）；该对照实验固化成测试作为迁移理由的存档。同时修掉 `MainWindow.OnLanguageChanged` 的**失真注释**（原文称"XAML 索引器绑定不会自动更新，需手动重建"——迁移后不再是这个机制，改为"XAML 已自动刷新；保留重建是为了覆盖代码里读资源再赋值的状态栏/标题/勾选"）。验收：全量 **2601/0**（本轮基线 2595 ⇒ **+6**：App.UiTests 153→**159**）、负向验证 **3/3 命中**、`dotnet build` **0 警告 0 错误**（Avalonia 构建期编译 XAML ⇒ 1287 处扩展解析均经构建验证）、`ci-gate.sh` **6 步全过**。范围边界：需产品决策 8 项、需先立功能 6 项、软件侧仍开着 12 项、环境残留 4 项，全部登记在权威登记表。另更正 `docs/localization-summary.md` §五.2 的失真记录。见 `docs/2026-09-17-open-items-register.md`。
- 2026-09-17：**阶段 14（`ProjectSettingsView` 行为级 UI 测试：补基建，顺带抓出两个真缺陷）**——承接权威登记表 §4 第 16 项（软件侧仍开着里最有价值的一项），原始登记见 `docs/2026-09-16-runtime-user-editor-ui.md` §7.1（"App 层没有 ProjectSettingsView 的测试基建，草稿语义/两层拷贝仍只有人工验证"）。**补基建**：新增 `ProjectSettingsRuntimeUserUiTests`（**13 项**行为测试），真的把面板 `Show()` 出来、按真实控件走完装载→编辑→取消/确定/应用 的完整生命周期；"草稿 vs 模型"用**引用比较**断言（`RuntimeUserList` 各项的 `Tag` 就是草稿实例）。两个踩过的点写进注释：整个用例体必须跑在 `UiTestHost.Run(...)` 里（控件要在 headless UI 线程上构造，否则 `ChromeWindow` 报 *Unable to locate 'Avalonia.Platform.IWindowingPlatform'* —— 首版把构造放在 `Run` 外，12 个用例全红）；触发用 `RaiseEvent(Button.ClickEvent)` 而非坐标点击（测**接线与语义**；命中测试/布局是另一条轴，已由 `HeadlessInputUiTests`/`ViewSmokeUiTests` 覆盖，且用户表在 `ScrollViewer` 内，坐标点击会引入"窗口够不够高"这种无关条件）。**基建立刻抓出两个真缺陷**，且两个都**只在装载/特定形态下错**、人工点两下就被纠正 ⇒ 此前多轮人工验证必然漏掉。**缺陷 #1（零可用被显示成常规提示）**：`RefreshRuntimeUserIssues` 的注释写着"空表与『表非空但一条都进不去』**要分开说**"，但那个分支**不存在** —— 四条规则都不覆盖"账户被停用"，于是全停用的表返回零问题、落进"正常"分支，显示成「共 0 个可用账户」+ **常规色**；而该形态的后果是**设备端谁都登不上、所有权限门永久拒绝**，阶段 12 已把它判为**导出级 Error** ⇒ **面板把阻断级状态显示成了普通说明**（数字确实是 0，但它在"正常"分支、常规色，读起来像"暂时没有账户"）。修法：新增分支 + 新文案 `project.settings.security.users.table.none.usable`（说清"N 条但一条可用都没有 + 后果 + 会被导出拦下"）+ 抽出 `SetRuntimeUserIssues(text, isProblem)`——**文案与颜色一起变**，只改文案不改色用户扫一眼仍会当成说明；四条规则产出的问题行也一并改为警示色。**缺陷 #2（装载后提示一直是「用户表为空」）**：构造顺序是 `InitializeRuntimeUserSection()`（此刻草稿还是空的 ⇒ 提示被写成"用户表为空 —— 设备端不校验凭据"）→ `BindToProject()` → `BindRuntimeUsers()`（草稿在这里才被填上），而 `BindRuntimeUsers()` **不重算提示** ⇒ 打开一个**已配账户**的工程，提示区显示"用户表为空 —— 设备端不校验凭据，按默认角色直接授权"，**两句话都与事实相反**（表不是空的、设备端**会**校验凭据）。它**只在装载那一刻错**：此后动一下任一控件都被后续调用纠正，而人工走查的路径正是"打开→看一眼→点两下试试"—— 点完错误就消失了，只有"打开后**不动任何控件**直接读提示区"才看得见，而这恰是自动化测试的默认行为。修法：`BindRuntimeUsers()` 末尾补 `RefreshRuntimeUserIssues()`，并把"为什么必须在这里重算"（构造顺序 + 只在装载时错）写进注释，避免后人当成冗余调用删掉。**负向验证 5/5 命中**：A/B 分别复现上述两个缺陷（证明确实修好），C/D/E 钉住该面板赖以成立的**三条不变量**（第一层拷贝 / 第二层拷贝 / `OnOkClicked` 的 `_hasChanges` 闸门）。**一条可复用的判据**：两条缺陷都不是"功能缺失"，而是**实现与它自己的注释/意图不符** —— 只要一个方法的注释在用「要分开说 / 必须 / 刻意」这类词描述行为，就该有一条测试去证明它真的存在。验收：全量 **2614/0**（本轮基线 2601 ⇒ **+13**：App.UiTests 159→**172**）、`dotnet build` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**、本地化 +1 键。范围边界：只覆盖运行时用户表（该面板共 6 个分类、1183 行），其余 5 个分类仍只有静态源码扫描守卫，已登记为登记表 §4 第 17 项（基建已就位，扩展成本比本轮低得多）。同时更正 `docs/2026-09-16-runtime-user-editor-ui.md` §7 与权威登记表的对应条目。见 `docs/2026-09-17-projectsettings-runtime-user-ui-tests.md`。
- 2026-09-17：**阶段 15（`ProjectSettingsView` 配置往返保真：把行为网铺到其余 5 个分类）**——承接阶段 14 的范围边界与权威登记表 §4 第 17 项。**判据选"整个工程 JSON 逐字节不变"**：该面板有约 **50 个**"一个控件对一个字段"的绑定，逐字段手写断言有两个必然的坑 —— **漏字段**（写到第 30 个就累了，而漏掉的那个正好是将来出问题的地方）与**遮差异**（只断言"我关心的字段对"，把"顺手改了别的字段"放过去）；改成 `ProjectSerializer.Serialize` 在操作前后比对，**任何**字段变化都红（含以后新增的字段，不必回来补断言），失败时按行输出差异、直接点名字段（形如 `仅改动后有: "hardwareTargetId": "allwinner-t113",`）；配套一条互补用例"改一处 ⇒ 恰好一行 diff"（证明"改了的动了"而不只是"没改的没动"）。**当场抓出真缺陷：硬件型号被静默绑到另一块板。** `FindHardwareTargetItem` 在无匹配时回退到**第一项**（原意是"避免 `SelectedItem = null`"），而 `SaveToProject` 会把这个回退结果**持久化** —— 于是 `HardwareTargetId == null`（或一个**已失效**的旧 Id）时，**打开工程设置点一下「应用」**就被绑到清单第一项（本机 `allwinner-t113`）。而 `null` 在本项目里是**显式支持**的状态，四条证据：`CodeGenerator.Simulator/SharedLibrary` 写 `// Target: {… ?? "generic"}`、`MainWindow` 状态栏回落 generic、`GetSelectedHardwareTarget` 注释写着"无匹配（旧工程保存了失效 id）时返回 null"、`ValidateHardwareTarget` 取不到目标就**隐藏校验行**。后果四条：① 产物注释从 `generic` 变成那块板；② 兼容性校验对象变成用户从未选过的板；③ **失效 Id 被换成另一块板**（比"未指定"更糟 —— 用户以为自己的选择还在）；④ 无操作的设置访问产生 git diff。修法：无匹配返回 `null`（下拉保持未选中、保存写 null、校验行隐藏），三处自洽；用户显式选择型号后行为与以前完全相同。**同时钉住一条历史缺陷**：`SaveToProject` 注释记载的"曾经的实现是无条件 `foreach` 覆盖，导致每次打开工程设置点确定都会静默重置用户逐屏手工调过的尺寸"——现在有回归锁。**并钉住三条 null/归一语义**：清空可空文本 ⇒ `null`（不是空串，否则下游 `IsNullOrWhiteSpace` 判定与 JSON 都多出一段无意义内容）；未选屏 ⇒ `null`（不是第一块屏）；`ProjectVersion` / `OutputDirectory` 的 null ⇒ 默认值属**有意归一**（模型上是非空属性、默认 `1.0.0` / `./export`，保留 null 会让 `ResolveOutputDirectory` 返回 null、导出对话框拿不到默认目录），用测试钉住防"误修"。**两个测试侧的坑**（写进注释省下一次重踩）：这些数字框是 `FluentAvalonia` 的 **`FANumberBox`** 而非 `NumericUpDown`（`FindControl<NumericUpDown>` 会抛 *Expected control … but it was …FANumberBox*；仓库已有先例 `PropertyPanelUiTests` 用 `Find<FANumberBox>`）；比对必须用**同一个工程实例**（首版拿"新建的等价工程"当基准，把每次生成都不同的屏幕 `id` 与 `createdAt`/`lastModifiedAt` 全算成了差异）。验收：全量 **2624/0**（本轮基线 2614 ⇒ **+10**：App.UiTests 172→**182**）、负向验证 **4/4 命中**（A 复现本轮缺陷 / B 复现历史缺陷 / C、D 钉住两条 null 语义）、`dotnet build` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**。范围边界：`CmbLvglVersion` 的**表外版本**同族问题仍在（固定列表 `8.3…9.5`，工程存表外版本时保存归一成 `9.5`）——要先定产品语义"该保留还是报错"，已登记为登记表 §4 第 18 项；`ResolveColorDepthItem` 是死代码但**行为等价**（`ColorDepth` 首成员就是 `NormalizeColorDepth(null)` 返回的 `RGB565`）、无可观测缺陷，按"不留没有证据的改动"本轮**未删**（登记为第 19 项）。见 `docs/2026-09-17-projectsettings-roundtrip-fidelity.md`。
- 2026-09-17：**阶段 16（登记表 §4 第 18～20 项：配置编辑器与判据同口径）**——按顺序推进权威登记表 §4「软件侧可验证」里仍开着的第 18～20 项。三项看起来互不相干，根因是同一条：**编辑器的判断与实际判据（下游消费者 / 设备端）不同口径**，且全都表现为"界面看着正常、结果不对"。**#18 表外 LVGL 版本被静默归一**：`LvglVersion` **不是纯展示字段** —— `LvglApiCompatChecker` 按 9.x/8.x **分支改写生成代码**、导出器按它填 CMake 分支名与依赖表；候选表固定 `8.3…9.5`，工程存了表外版本（更老工程写的 `8.2`、或手改过 JSON）时装载 `SelectedItem` 变 null、保存又归一成 `"9.5"` ⇒ **打开工程设置点一下「应用/确定」就把 8.x 工程悄悄升成 9.x**（生成代码的 API 兼容改写随之改变语义，且无任何提示）。修法：表外版本也进候选（`BuildLvglVersionItems`）⇒ 下拉显示工程里那个版本、原样往返，只有用户显式选别的版本才变更 —— 与阶段 15 的"失效硬件型号 → 未指定而不绑到另一块板"同一把尺子：**不静默改用户/历史留下的值**。**#19 `ResolveColorDepthItem` 是死代码，而它防的坑是活的**：它的文档写明"回退到第一项而不是留 `SelectedItem = null` —— 后者会让下一次保存把无效值静默改写成默认值"，但它**从未被调用过**（装载走裸赋值），于是实际行为正是它想防的那个：存了无效色深时**下拉显示空白**、而保存仍经 `NormalizeColorDepth(null)` 写入一个值 ⇒ 界面与结果不一致。**先证明"有没有可观测缺陷"再动手**：`ColorDepth` 首成员恰好就是 `NormalizeColorDepth(null)` 返回的 `RGB565`，两条路径落盘值相同 ⇒ 不是"数值被改错"，而是**"界面在撒谎" + 死代码**；修法选**把 helper 接上**（意图是对的，且枚举重排时"按候选表选一个"比"硬编码常量兜底"更自洽）—— 死代码变活、文档声明被兑现。**#20 口令下限的字符数 vs UTF-8 字节数**：UI 用 `password.Length`（UTF-16 码元 ≈ 字符数）、模型注释写"（字符数）"、固件用 `strlen`（**字节**）⇒ 下限 8 时"电机启动"4 个字 = 12 字节：**设备端放行、编辑器却报"至少 8 位"**。**往字节统一**（与登记表原建议一致），并说明为何**不改设备端**：设备端已有凭据是按字节放行过的，改成按字符会把 3 字 CJK 口令（9 字节 ≥ 8）判为过短 ⇒ **现场用户直接登不上**（等于锁门）；在"弱一点但文档写清楚"与"可能锁住现有用户"之间选前者。**三处一起改**：判据（`Encoding.UTF8.GetByteCount`）、模型注释（`（字符数）` → `（UTF-8 字节数）` + CJK 后果 + 为何不改设备端）、用户文案（`至少 {0} 位` → `至少 {0} 个字节（一个汉字算 3 字节）`，zh-CN / en-US 各 2 条）—— **只改判据不改文案，用户仍会按"位"理解，反而更容易配错**。验收：全量 **2628/0**（本轮基线 2624 ⇒ **+4**：App.UiTests 182→**186**）、负向验证 **3/3 命中**（A 表外版本不进候选 / B 色深退回裸赋值 / C 口令退回 `password.Length`）、`dotnet build` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**。范围边界：把口令下限在**字符**意义上收紧（改设备端按 UTF-8 码点计数）属**产品决策 + 凭据迁移**，已登记为登记表 §4 第 20b 项；§4 其余仍开着项按顺序在后续轮次处理。见 `docs/2026-09-17-config-editor-value-normalization.md`。
- 2026-09-17：**阶段 17（登记表 §4 第 22～25 项）**——按顺序推进权威登记表 §4「软件侧可验证」里仍开着的第 22～25 项（第 21 项「用户表校验不覆盖口令强度」是**刻意限制**，跳过）。这一批里**只有两项需要改代码**、且性质完全不同。**#22 真缺陷：默认超时覆盖不住最长的单次生成**——`TimeoutSeconds`（默认 **60**）直接用作 `HttpClient.Timeout`，即**整个响应读完为止**的上限（含流式读完整条 SSE 流）；而 Agent 单次请求 `MaxTokens = 8192`，按慢速中转/本地模型 **~45 token/s** 估算需 **≈182 s** ⇒ 生成会在**一半左右被掐断**，用户看到"AI 生成失败"——与模型质量无关，**换个慢模型/慢中转就复发**；而该字段**没有设置界面**（`LlmConfigStore` 不持久化、AI 面板无输入框）⇒ 默认值是唯一杠杆。修法：默认 **60 → 240 s**（留约 30% 余量）；判据刻意不是"等于 240"（那种测试只会让人改两处），而是**从算式推出下界**（`8192 / 45 ≈ 182`）——于是改回 60 会红，并在失败信息里直接给出算式与失效模式；把 `MaxTokens` 调大时该判据仍成立（读的是同一组常量）。**#23 判据脆弱（非产品缺陷）**：原 `InRange(elapsed, 150, 2000)` 的上界是**绝对余量**；本沙箱存在全局串行的资源竞争、定时器续体可被推迟到秒级，同族判据已被撞穿几条（同文件相邻用例只断言**下界**——下界对负载天然安全，上界不是）。修法：改成**两次不同窗口的差值**判据——两次测量承受同一份负载，共同项在差值里抵消：正确实现 ≈ **1000 ms**（窗口差）、空等固定超时 ≈ **0 ms**，于是**不需要**在负载与灵敏度之间折中。**#24 登记有误 → 撤销**：原登记称 `ConfigCodeGenConsumptionTests` 的 POSIX 字面量"在 Windows 上断言会挂"；复核不成立——断言是 `Assert.Equal(Path.Combine(dir, outDir), resolved)`，而 SUT 就是 `IsPathRooted(x) ? x : Path.Combine(dir, x)`，**两侧是同一个表达式**，任何平台恒等；绝对路径那条在 Windows 上 `IsPathRooted("/opt/output")` 同样为 `true`。⇒ 撤销该条（残留只是"夹具只用了 POSIX 形状字面量"的**覆盖** nuance，不是失败风险）。**这一条值得单记**：登记表是判断的产物，**判断本身也会写错** —— 所以"按登记表动手"之前必须先用代码证伪一次。**#25 真警告：`host_main.c` 的 4 条 `-Wdiscarded-qualifiers`**——该文件不被 `run_host_tests.sh` 编译，要按它自己的 Makefile 复现（`make objs/host/host_main.o`）⇒ 4 条（`:83-86`）；根因是 `LV_FONT_DECLARE(x)` 展开为 **`extern const lv_font_t x;`**（内置字体是 const），而 `g_font_cn_*` 是**非 const** 的 `lv_font_t*` —— **但不是本文件的疏忽**：这四个全局同时被**设备侧** `app/main.c` 定义、被共享头与 `set_ta_font(lv_obj_t*, lv_font_t*)` 使用，设备上装的是 FreeType 动态创建的可变字体；全仓 const 化要动 **7 个固件文件（含设备运行时）** 去换 4 条 **host 工具**警告，不成比例，且漏改会**编不过设备端** ⇒ 只在这 4 处回退赋值上做**显式转换**并写清安全论证（只喂给 LVGL 只读字体 API、从不经其写入），重编 **0 警告**、产物正常（22832 B）。验收：全量 **2630/0**（本轮基线 2628 ⇒ **+2**：AI 313→**315**）、负向验证 **2/2 命中**（#24 无可注入行为、#25 是编译期警告 ⇒ 不产生 NV 组）、`dotnet build` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**。范围边界：`g_font_cn_*` 的全仓 const 正确性本轮用局部转换替代（属独立固件重构，需真机回归）；`TimeoutSeconds` 仍无可配置入口（给设置界面属产品决策）；§4 其余仍开着项（Dock 真实拖拽 / `ViewLocator` 旧 Parent、运行时动态文案、en-US 校对、实机相关项）按顺序在后续轮次处理。见 `docs/2026-09-17-timeout-flaky-judgment-and-stale-registration.md`。
- 2026-09-17：**阶段 18（Dock 真实拖拽：可行性取证 + 回归网）**——按顺序推进权威登记表 §4 第 26 项（Dock 真实拖拽路径未模拟 / `ViewLocator.Build` 不脱离旧 Parent / 归档问题单仍 OPEN）。**先查实现再下结论，推翻了登记里的假设**：`2026-09-15-dock-view-resolution-contract.md` §7.1 把"真实拖拽未被模拟"归因为"需要在 headless 里模拟 `DragDrop` 序列 —— 那是另一件事，**且不一定可行**"；实测 `Dock.Avalonia 12.1` 的 DnD **不走平台 `DragDrop` API**，而是**指针事件**驱动（程序集里是 `OnPointerPressed` / `OnPointerMoved` / `OnPointerReleased` + `DockDragContext`，`ToolTabStripItem` 是把手）⇒ headless 的 `MouseDown/MouseMove(LeftMouseButton)/MouseUp` 就能驱动：探针实测拖工具箱标签后 `Toolbox@LeftLowerDock → Toolbox@CenterDocDock`、`toolboxDescendants=151`。**新增回归网** `DockDragRegressionUiTests`（3 条）：真实按标签中心拖拽后工具箱**仍挂在可视树上且非空**（判据刻意看**附着状态 + 可视后代数**——归档问题单的现场签名正是 `[ToolboxView] DETACHED from visual tree` 且此后再无 attached，光断言 `Build(...) is not null` **打不到**它）+ 拖拽后 12 个 Tool 逐个 `Build` 非 null + **判据双向自检**（负例：手工构造"解析得出来但没挂上树"⇒ 必须判为未附着；正例：真挂进窗口 ⇒ 必须判为已附着；该自检的**可失败性**也证过：判据改恒真 ⇒ 负例立刻红）。**自检本身也纠错过一次**：第一版自检要求"拖完必须与别的面板共处一个 dock"，**红了** —— 实测 Dock 对这次放置的处置是**新建/切分出一个 dock**，不是加进目标标签组，于是改成"父 dock 引用变了"；另外第一版只断言"拖完还有内容"，而**一次没被驱动的拖拽也会绿**，必须补自检。**一条诚实的记录**：把 `ViewLocator.CanResolve` 退回**原始缺陷判据**（只认 `Context is Control`）后，两条拖拽用例**照样全绿** ⇒ Dock 12.1 的标签拖拽**不会清空 `Context`**，归档问题单**假设 D 的机制在这条路径上不复现** ⇒ 它们是**回归网**而非"在已知缺陷上验证过会红"的判据，这正是补判据自检的原因（**"守卫绿了"与"守卫有能力红"是两件事**）；如实登记，免得下一个人以为 `DockViewResolutionUiTests.ContextCleared_…` 覆盖了真实拖拽路径（它覆盖的是**解析契约**）。**`ViewLocator.Build` 不 Detach：三条实测**把登记里那句"未验证项"变成结论——① 把**仍挂着**的控件加到新父级**会抛**（`InvalidOperationException: … already has a visual parent ContentPresenter …`，`Button` 与真实 `ToolboxView` 都验）⇒"谁先摘"是**硬约束**；② 先 `ContentControl.Content = null` 再挂**可行**（视觉父级变 null、无异常）⇒"脱旧 Parent"技术上做得到；③ 真实拖拽路径**没有**撞上① ⇒ Dock 自己完成了转移 ⇒ **保持现状**并把①②钉住（`ViewLocatorReparentContractTests`）。另一层重要理由：**万一漏摘，症状是抛异常（响亮、能定位），不是归档问题单里那种静默空白** ⇒"加 Detach"并不是消除静默空白的必要手段，真正的风险是"**加** Detach 时摘不干净/摘错对象"——登记里"可能把偶发空白换成必崩"的担心**方向对、机制要改**。**归档问题单仍标 OPEN（诚实边界）**：原文复现是"拖**项目浏览器**到**工具箱** dock"，而项目浏览器的标签条实测只有 **1 px 高**（单面板 dock 不渲染可见标签项）⇒ 本轮驱动的是**镜像方向**（走同一条重组路径但**不是逐字复现**），镜像方向未复现空白 ⇒ 当年症状成因**仍未定论**；已往问题单追加复测记录，并登记**未覆盖路线**（拖到文档区 / 拖出成浮动窗口 / 多屏重组）。验收：全量 **2636/0**（本轮基线 2630 ⇒ **+6**：App.UiTests 186→**192**）、`dotnet build` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**；负向验证两组（A 判据恒真 ⇒ 命中；**B 退回原始缺陷判据 ⇒ 按预期不命中**并据此定性 —— 不是失败，是结论）。见 `docs/2026-09-17-dock-real-drag-feasibility-and-reparent-evidence.md`。
- 2026-09-17：**阶段 19（本地化：硬编码中文清单 + 只减不增守卫）**——按顺序推进权威登记表 §4 第 27 项。**又是失效登记**：登记里点名的举例（`AnimationEditorView` 的缓动选项 `Lang.AnimEasingLinear`、`ToolboxView` 的控件显示名 `Lang.TbScreen`）**早已在用字典键** ⇒ 照登记表动手会白跑。**真实剩余量接近"约 100+"但分布完全不同**：`.axaml` 只 **14 条 / 2 文件**（本轮全部本地化），`.cs` 有 **226 条 / 27 文件**（以日志面板文本 ≈60、撤销/操作名、下拉显示名、快捷键提示为主，已逐文件登记基线）。**判据的形式决定它看不看得见东西**：第一版扫描器按"这一行是不是控件赋值"（sink 式）只得到 **98 条**，真实是 **226** —— 因为多行实参（`PopulateTaggedCombo(CboAlarmType, new (string,object)[] { ("上限报警 (H)", …) })`）的 sink 在**上一行**，按行匹配整片漏掉（`VariableManagerView` 的 27 条全漏）；改成**分类式**（取所有含中文的字面量、扣掉明确排除类别）才准，而**失明方向是"漏报"** ⇒ 会让人以为快做完了。**根因才是重点**：登记了这么多轮，**没有任何机制阻止新功能继续写死中文** ⇒ 清单永远追不上；本轮补上 `HardcodedLocalizationGuardTests`（每文件条数与基线**相等**判定——涨了报"新增了硬编码中文"、降了报"必须同步改小基线"；总数只减；外加扫描器自检：文件数/条数下限、行/块/**XAML 注释**剥离、排除项正负例），"相等"而非"不超过"是照搬 `ConfigFieldInventory` 的教训（允许事实比记录少，清单就会慢慢变成过期抄写）——**交付的不是"翻译了多少"，而是把一件永远做不完的事变成只会变少的事**。**首批本地化 14 条 `.axaml`**（Splash 首屏 9 + AI 面板按钮 5），**8 个新键 + 6 处复用既有键**（`app.title`/`status.ready`/`gen.cancel`/`ai.image.save.svg/png`），刻意不重复造键；**刻意不改用户可见措辞**（Splash 写"新建**工程**"、字典 `project.new` 是"新建**项目**"——保留原文只搬进字典，改了就是未 asked-for 的措辞变更），`SplashWindow.axaml` 补上 `xmlns:res`。**新发现（产品决策）**：同一概念字典里含「工程」**30** 处 vs 含「项目」**33** 处，统一会改约 60 处用户可见措辞 ⇒ 登记为 §4 第 27b 项，本轮刻意未动。验收：全量 **2641/0**（本轮基线 2636 ⇒ **+5**：App.UiTests 192→**197**）、负向验证 **3/3 命中**、字典 zh-CN/en-US 各 **+8** 键（占位符一致）、`dotnet build` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**。范围边界：剩余 **226 条**按文件分批（总量已单调）；en-US 逐字校对需母语级判断，随分批做；`src/HmiEditor.AI`（脚本模板中文）不在扫描面内，需与 prompt 契约一起评估。见 `docs/2026-09-17-localization-hardcoded-inventory.md`。
- 2026-09-17：**阶段 20（本地化第二批：修掉守卫自己的漏报 + 命令目录收口）**——继续推进权威登记表 §4 第 27 项。**上一阶段立的守卫自己有缺陷，且缺陷方向是"漏报"**（清单偏小 ⇒ 让人以为快做完了），本批先修它。**判据从"按行"改成"按语句"**：原规则按行匹配排除词，两个方向都错 ——（a）`throw new ArgumentOutOfRangeException(` 换行后参数里的中文被算成待本地化（与"异常消息不翻译"自相矛盾）；（b）**更严重**：`Log($"[提示] {AppLocalization.Service[…] }")` 这类日志行，正文入了字典、**段落标签 `[提示]` 是写死的中文**，却因整行含 `AppLocalization` 被**整片豁免** ⇒ **13 条用户可见文案被藏起来了**（`DeviceManagerView` 6 + `DownloadToHmiView` 7；`LogBox` 是设备面板上可见的 `ListBox`，不是调试输出），英文界面下这些日志**中英混排**；新判据按"包住字面量的那个调用"判定（不跨 `;`/`{`/`}`/`(`，找不到包裹调用才退回语句前缀，避免外层 `foreach (… LocService[…]) items.Add(new Item("中文"))` 连坐豁免），那 13 条标签本批入字典（`Lang.DeviceLogTag*`）。**`EditorCommandCatalog` 38 → 0**：其中 **26 条根本不是 UI 文案** —— 25 条是 `Bindings[]` 的手写 `Description`（唯一消费者 `FindConflicts()`，而它唯一被一个测试消费），1 条是 `FindConflicts` 的消息模板；`Description` 与 `Key`+`Modifiers` **完全重复**，是手工维护的第二份事实 ⇒ 改了键位忘了改文案，这个"冲突诊断"就会**静默说谎**，而它恰恰是用来判断"到底哪里撞了"的 ⇒ 处置**不是翻译而是删掉抄本**：新增由 `Key`/`Modifiers` **推导**的 `Chord`（`Ctrl+OemPlus`/`Esc` 等枚举名，可搜索且不会被本地化），冲突消息改纯 ASCII；剩下 **11 条 `Describe(command)` 是用户可见的**（`MainWindow` 用它拼撤销/重做菜单项 `Loc["project.undo"] + " " + UndoDescription`）⇒ 按**原措辞**入字典（`cmd.desc.*`）。**`FindConflicts` 原来没有能力变红**：只读静态 `Bindings` 而表里本无冲突 ⇒ `Bindings_HaveNoConflicts` 在分组逻辑写坏时**照样绿**（守卫绿了 ≠ 守卫有能力红）⇒ 抽成可注入的 `internal FindConflicts(IEnumerable<ShortcutBinding>)` + 人造冲突用例。**新守卫 `LangKeyCoverageTests`**（3 条）补上"`Lang` 常量 ↔ 字典键闭合"这个此前没人看的洞：索引器找不到键时**回退成键名本身**，"忘了加一条"既不抛异常也不编译失败、界面上只是安静显示 `cmd.desc.align.left`；而既有 `LocalizationTests` 只保证两本字典**彼此**键集一致，**两本一起漏掉同一个键时 parity 依然绿** ⇒ 反射枚举 **1782 个**常量逐个验证（+ 扫描器自检 ≥1000 + 键值唯一）。**基线 226 → 187**（26 文件），每笔见专题 §9.2；其中 `TemplateManager` **1 → 4** 是**量具修正、不是新增代码**（3 个硬编码控件名原来被连坐豁免），`HmiControl.Name` 用户可见故如实计入，但"控件名要不要翻译"是产品决策（翻译会让**生成物随 UI 语言变化**）⇒ 登记为 §4 第 **27c** 项、本批刻意未动。验收：全量 **2650/0**（本轮基线 2641 ⇒ **+9**：App.UiTests 197→**206**）、负向验证 **5/5 命中指名用例**（A 新增硬编码 / B 删字典键 / C 去掉推导里的 `Shift` / D 去掉冲突过滤（**新旧用例同时红**）/ E 判据还原成按行；每组先确认测试**确实执行**以防编译失败冒充"有能力红"，还原后 sha256 **逐字节一致**）、字典 zh-CN/en-US 各 **+18** 键、`dotnet build` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**。**本批最该记住的**：一个"总是绿"的守卫比没有守卫更危险，因为它会让人停止检查。见 `docs/2026-09-17-localization-hardcoded-inventory.md` §9。
- 2026-09-17：**阶段 21（`ConfigFieldInventory` 悬停说明入字典 + 门禁改为"警告即失败"）**——继续推进权威登记表 §4 第 27 项第三批。**先查消费点再动手**（复用阶段 20 的教训）：`ConfigFieldInventory.NotWired[].Note` → `NotYetEffective.Mark(control, reason)` → `ToolTip.SetTip(…)`，即**灰显控件的悬停提示**（"尚未生效：命令栏显隐没有任何消费点……"）——**用户可见**，30 条必须入字典。**键推导而非手写**：删掉 `string Note` 实参，新增 `NoteKey => $"cfg.note.{Key}"`（`Key` 已是 `类型名.字段名`）——手写文案或手写键都会多出一份"与 `Type`/`Field` 并行维护"的事实，复制条目时最容易忘记改的就是它，改错了（说明与字段对不上）**没有任何东西会报**。**顺带修掉一个潜在缺陷**：清单是 `static` 字段、静态初始化只跑一次 —— 若在清单里就 `AppLocalization.Service[…]` 解析成字符串，语言会**冻结在首次访问那一刻**，之后切换语言悬停提示不跟着变 ⇒ 解析改到显示时，并由新守卫 `TooltipReasons_ArePassedAsKeys` 扫两个面板源码钉住"实参必须是 `entry.NoteKey`"；另一条 `EveryEntry_HasATooltipReasonInBothLanguages` 要求每个 `NoteKey` 在两本字典里都解析出**非键名**的值（索引器找不到键会回退成键名，正好用来判定"字典里没有"，带"清单 ≥25 条"自检）。**基线 187 → 157**（25 个文件）。**并且更正了本会话自己的一处错误**：阶段 20 为日志标签加了个辅助方法 `Tag(string)`，它**隐藏了继承来的 `Control.Tag`**（CS0108）—— 而阶段 20 的验收写着"0 警告 0 错误"，那个读数**不可信**：用的是**增量构建**，那两个文件此前已被定向测试编译过，构建是 no-op，**警告不会被重新打印**。更正三件事：① 辅助方法改名 `LogTag`（撞框架属性还会误导读者以为它在动 `Control.Tag`）；② **全量非增量构建**实测 0 警告；③ **把判据补进门禁**（不只靠人眼）——构建步加 `--no-incremental`（否则这一步可能是一次 no-op），且构建输出出现 `: warning <码>` **即判红**并打印去重后的警告清单。门禁修复的负向验证：把 `Tag(string)` 加回去 ⇒ **exit 1** 且指名 `warning CS0108`，还原后 `cmp` 逐字节一致；**并顺带纠错一次** —— 第一版注入写成语法错误（两行方法签名挤在一行），门禁因 `error CS1525` 而红，那不是"警告判红"的证据，重做后才是。验收：全量 **2652/0**（本轮基线 2650 ⇒ **+2**：App.UiTests 206→**208**）、负向验证 **3/3 命中指名用例**、字典 zh-CN/en-US 各 **+30** 键、`dotnet build --no-incremental` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**。**教训**：`dotnet build` 是增量的，"这次没报"与"没有警告"是两件事——只在**全量构建**时才成立的读数，和只在**空队列**时才成立的断言一样，都不能当验收证据。见 `docs/2026-09-17-localization-hardcoded-inventory.md` §10。
- 2026-09-17：**阶段 22（`UiOperationGuard` 操作名入字典：一处横切缺陷）**——继续推进权威登记表 §4 第 27 项第四批。按 §8.1 的顺序本该继续做设备日志正文，但**先撞见一处横切缺陷**：`UiOperationGuard.Run/RunAsync` 的第一个实参（操作名）会进**用户可见**的失败提示 —— `AppLog.Error($"{operation}失败", ex)` 落盘，而 `report($"{operation}失败: {ex.Message}")` 落到视图状态行或**全局状态栏** ⇒ 英文界面下的失败提示是 `"选择文件失败: Access denied"`；而 **12 个调用点传的全是中文字面量**（重命名画面/删除画面/重命名控件/删除模板/AI 生成脚本/删除用户/选择文件/选择目录/生成图标/选择 HMI 程序/编辑表格单元格/重试 Agent 任务）。**处置**：参数语义改成键（`operation` → `operationKey`），3 条模板入字典（`ui.op.failed`/`ui.op.failed.detail`/`ui.op.report.failed`），12 个调用点改传 `Lang.UiOp*`。**只改调用点是不够的** —— 下一个调用点照样能传字面量、没有编译期约束，所以补两条新守卫：`EveryCallSite_PassesALocalizationKey`（扫全部 App 源码，第一个实参必须以 `Lang.` 开头，带"≥10 处调用"自检）+ `RunAsync_ReportsTheLocalizedFailureMessage`（真失败一次，断言报出的文案**等于"字典里的操作名 + 字典里的模板"**且**不含键名** —— 索引器找不到键会回退成键名，正好用它判定"真来自字典"）；另加 `RunAsync_Cancellation_IsNotReportedAsFailure` 钉住既有语义（取消 ≠ 失败）。**顺带修掉守卫自己的一个"写死阈值"**：本批把总量降到 142 时**自检反而报红** —— `Scanner_ActuallyFindsHardcodedChinese` 写着"条数 ≥ **150**"，这正是写死阈值的典型结局（每本地化一批就得改它，而"改了阈值"这件事本身没人会注意到）⇒ 改成**与基线挂钩**（`>= Baseline.Values.Sum() / 2`，文件数同理），阈值随真实进度一起下走。**基线 157 → 142**（24 → **18** 个文件）。验收：全量 **2655/0**（本轮基线 2652 ⇒ **+3**：App.UiTests 208→**211**）、负向验证 **3/3 命中指名用例**（A 调用点改回中文字面量 ⇒ 源码契约守卫红并连带本地化守卫红；B 模板键换成占位符数不符的键 ⇒ 行为守卫红；C 删 `ui.op.pick.hmi` 字典键 ⇒ `Lang` 键闭合守卫红）、字典 zh-CN/en-US 各 **+15** 键、`dotnet build --no-incremental` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**。**这一步的价值不在 15 条文案**，而在于它把一个"谁都可以继续写死中文"的横切入口，变成了**有源码契约看守的键参数**。见 `docs/2026-09-17-localization-hardcoded-inventory.md` §11。
- 2026-09-17：**阶段 23（设备日志面板正文 43 条清零 + `LogTagged` 实参个数判据）**——继续推进权威登记表 §4 第 27 项第五批。第四批把横切的操作名清掉后，这一批**只剩日志正文**，且两个文件的「连接」块逐行重复，一起做最省：`Views/DeviceManagerView.axaml.cs` 27 → **0**、`Views/DownloadToHmiView.axaml.cs` 16 → **0**（`LogBox` 是面板上的 `ListBox`、状态行也是用户直接看的，都属用户可见）。新增 28 个键（含段落标签 `device.log.tag.deploy` / `device.log.tag.fel`）+ 28 个 `Lang` 常量；`[部署] 已自动检测到预编译 HMI 程序: {candidate}` **复用**既有 `download.so.auto.found`（同一句话不造第二个键）；两个视图各加 `LogTagged(tagKey, messageKey, args…)` 让"段落标签 + 正文"都只能从字典来，`LogError` 的段落名也从字面量改成 `Lang.DeviceLogTag*` 键。**顺带补掉新辅助方法的静默失败模式**：`LocalizationService.Format` 在"占位符个数 ≠ 实参个数"时**不抛异常**，只原样返回未格式化文本 + 一句 `Debug.WriteLine`，而 `Debug` 在 **Release 下被编译掉** ⇒ 用户会在日志面板里看到 `已刷新串口列表 ({0} 个)`，编译器看不见、运行时不抛、只有人盯着才可能发现；新增 `LocalizedLogCallSiteTests`（扫全部 App 源码，把 `LogTagged(...)` 的实参按**顶层逗号**切开，断言 `实参个数 - 2 == 字典里该键的占位符个数`，带"≥15 处调用被校验"自检）。**两处自我纠错**：① **为判据写的判据当场抓到自己的错** —— `SplitTopLevelArguments` 第一版把字符串实参整个丢掉（`inString` 分支里 `continue`、没把字符抄进缓冲）⇒ `F(x, y), "a,b", C.D` 只剩 `F(x, y), C.D`，为切分器写的那条单元测试先于主判据报红；② **上一阶段刚加的"警告即失败"当场抓到我** —— `LogTagged` 签名写成 `params object?[]` 而 `Format` 是 `params object[]` ⇒ `CS8620`，在 `--no-incremental` 那次构建里立刻现形 ⇒ 改成 `params object[]`；若没有上一阶段那条门禁改动，它会像 CS0108 一样躲在增量构建后面，而文档里又会多一句没验证过的"0 警告"。**基线 142 → 99**（18 → **16** 个文件）。验收：全量 **2657/0**（本轮基线 2655 ⇒ **+2**：App.UiTests 211→**213**）、负向验证 **2/2 命中指名用例**（A 给 `LogTagged` 去掉一个实参 ⇒ 占位符个数判据红；B 一条日志改回中文字面量 ⇒ 本地化守卫红并连带总数判据）、字典 zh-CN/en-US 各 **+28** 键、`dotnet build --no-incremental` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**。按专题 §8.1 的顺序，下一批是 `VariableManagerView` 的下拉显示名（27）。见 `docs/2026-09-17-localization-hardcoded-inventory.md` §12。
- 2026-09-17：**阶段 24（`VariableManagerView` 显示名入字典：先分类再动手）**——继续推进权威登记表 §4 第 27 项第六批。**27 条里混了两类东西，先分类再动手**（这一步比翻译本身重要）：**纯显示 25 条**（报警类型/级别下拉标签、报警列表短类型名、"未分组"虚拟分组标签、从站显示名、"已停用"后缀）入字典，新增 23 键、"未分组"复用既有 `var.picker.ungrouped`，顺带把"虚拟未分组分组"的两处字面量清掉（标签与 `Name` 同源，该对象 Id 为空、不落盘，名字纯显示用）；**落盘的数据标识 2 条刻意不翻译** —— `Name = $"报警{n}"` 与 `_selectedAlarm.Name = … "未命名报警"`，因为 `VariableAlarmConfig.Name` 挂在持久化的 `Variable.Alarms` 上（模型注释就写着"报警名称（如"温度过高""压力下限"）"）⇒ 翻译它会让**工程文件内容随 UI 语言变化**（中文界面新建存"报警1"、英文界面存别的，同一工程里两种语言的名字混在一起）；这与阶段 20 发现的模板控件名是同一回事 ⇒ 并入 §4 第 27c 项并把该条改名为"**落盘标识符**要不要跟着 UI 语言翻译"，同时**如实把它们留在基线里**（`VariableManagerView` 27 → **2**）并在基线上写注释说明**为什么是 2 而不是 0** —— 不假装清干净。**新守卫：把"键 ↔ 枚举"的配对钉住** —— 本地化把"手写文案"换成"键"时最容易**抄串**（`HighLimit` 配上"下限报警"），而写串了**没有任何东西会报**（下拉安静地显示错的类型名，Tag 仍正确，连保存的数据都是对的、事后翻数据也看不出来）；于是把配对提成 `internal` 的两个表（标签在这一层解析），配 `AlarmOptionLocalizationTests` 六条：枚举**一一对应**（枚举新增成员却忘加下拉项 ⇒ 用户选不到）、**语义配对**、**标签确实由字典解析**、标签两语言可解析且组内互不相同。**其中"标签确实由字典解析"是负向验证逼出来的**：第一版守卫只断言配对表，于是把 `(AppLocalization.Service[o.LabelKey], …)` 改成 `(o.LabelKey, …)`（**把键直接当标签用**，用户会看到 `var.alarm.type.high`）**四条用例全绿** ⇒ 负向验证当场顶出这条盲区，才补上那条断言。**基线 99 → 74**（16 个文件）。验收：全量 **2663/0**（本轮基线 2657 ⇒ **+6**：App.UiTests 213→**219**）、负向验证 **4/4 命中指名用例**（A 交换配对 ⇒ 语义配对红；B 删枚举下拉项 ⇒ 覆盖面红；C 两级指向同一键 ⇒ 重复标签红；D 把键当标签用 ⇒ 第一版全绿、补断言后命中）、字典 zh-CN/en-US 各 **+23** 键、`dotnet build --no-incremental` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**。见 `docs/2026-09-17-localization-hardcoded-inventory.md` §13。
- 2026-09-17：**阶段 25（`MainWindow` 22 条入字典 + 失败模板收成唯一实现）**——继续推进权威登记表 §4 第 27 项第七批。**先撞见的又是一处重复实现**：`MainWindow.ReportOperationFailure(string operation, Exception ex)` 里写着与 `UiOperationGuard` **逐字相同**的两条模板（`$"{operation}失败"` / `$"{operation}失败: {ex.Message}"`），而上一阶段刚给后者做完本地化 —— **同一套措辞、两份实现**，本地化一处漏另一处几乎是必然 ⇒ 顺手把模板收成唯一实现：`UiOperationGuard.FailureSummary(operationKey)` / `FailureDetail(operationKey, reason)`，两处都调它；`ReportOperationFailure` 改收**键**，9 个调用点改传 `Lang.UiOp*`。顺带消掉一条重复措辞：`StartWithOpenFileCore` 的 `$"打开工程失败: {ex.Message}"` 现在直接是 `FailureDetail(Lang.UiOpOpenProject, ex.Message)` —— **措辞逐字不变**、也不需要新键。**22 条入字典**：8 个操作键（打开/新建/保存工程、导出代码、编译工程、下载到 HMI、导出模拟器工程、Dock 事件路由诊断）+ `status.file.not.found` + `status.theme.switched` + 8 个**撤销描述**键（删除控件/删除 {0} 个控件/旋转 {0:0}°/水平镜像/垂直镜像/重置变换/修改控件 {0}/修改页面 {0}，撤销菜单项上直接可见），撤消描述沿用字典既有的 `undo.` 命名。**留下 2 条**：`HmiResourceItem` 的 `SubCategory`/`Description`（`"AI 生成"`/`"AI 生成的矢量资源"`，`_resourceLibraryStore.Save` 会**存进资源库文件**）⇒ 与阶段 24 的报警名、阶段 20 的模板控件名同类（落盘内容不该随 UI 语言变化），并入 **§4 第 27c 项**（该条改名为"**落盘内容**要不要跟着 UI 语言翻译"），如实留在基线里（`MainWindow` 24 → **2**，注释写明为什么是 2）；**更彻底的解法**（存稳定 token、显示时映射，需迁移既有文件）已写进登记项。**契约守卫扩到第二个入口**：`EveryCallSite_PassesALocalizationKey` 原本只扫 `UiOperationGuard.Run/RunAsync`，本批把 `ReportOperationFailure` 也纳入（正则加分支），于是"下一个调用点照样能传中文字面量"这个洞在两个入口上都被堵住；顺带排除"方法声明里的形参"，自检从"≥10 处"提到"≥20 处传了 `Lang.*` 键的调用"；另加 `FailureFormatters_UseTheSharedTemplatesAndResolveTheOperationName` 钉住共享模板用的是哪两条键。**基线 74 → 52**（16 个文件）。验收：全量 **2664/0**（本轮基线 2663 ⇒ **+1**：App.UiTests 219→**220**）、负向验证 **3/3 命中指名用例**（A `ReportOperationFailure` 调用点改回中文字面量 ⇒ 契约守卫红并连带本地化守卫；B 共享模板换成占位符数不符的键 ⇒ 模板契约红并连带行为用例；C 撤销描述改回中文字面量 ⇒ 本地化守卫红）、字典 zh-CN/en-US 各 **+18** 键、`dotnet build --no-incremental` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**。见 `docs/2026-09-17-localization-hardcoded-inventory.md` §14。
- 2026-09-17：**阶段 26（一批小文件 11 条 + 诊断 sink 规则漏网 + 主题说明接上界面）**——继续推进权威登记表 §4 第 27 项第八批。**① 补一个判据漏网（假阳性）**：`PreviewRuntimeService` 的诊断出口默认写 `Debug.WriteLine`、而 `MainWindow` **从不注入它** ⇒ 那条中文消息用户看不到，但排除规则**按行**看关键词、sink 却写在**属性初始化器**里（调用点那行只有 `Log?.Invoke(...)`）⇒ 被判成"待本地化"；处置**不是**给诊断文案建字典键（没人看得到），而是 `Log` → **`DiagnosticLog`**（让 sink 的名字**自己声明类别**，与 `Debug.`/`Console.`/`Trace.` 同类）+ 排除正则加它。这与阶段 20 的多行 `throw new` 是**同一类**（判据行局部、事实跨行），但修法不同：那边让判据跨行，这边让**代码自己说清楚** —— 改名比加规则稳，因为名字错了编译不过、规则漏了没人知道。**② 把一处死数据接上界面**：`ThemeInfo.Description` 的**唯一**消费者是一条 `Assert.False(IsNullOrWhiteSpace(...))`，设置对话框只有三个**只显示名字**的主题按钮、应用里没人读它；按阶段 21 的教训本该删抄本，但这份数据本身有用（不是重复抄本）⇒ 选**接上界面**（与登记表第 19 项把 `ResolveColorDepthItem` 死代码接上同一处置）：`Description` → **`DescriptionKey`**（静态表里只许放键 —— 放解析结果会**冻结语言**，与 `ConfigFieldInventory` 同一个坑）、新增 `AppThemeService.DescribeTheme(key)` **显示时**解析、挂成三个主题按钮的**悬停提示**，Core 测试从"断言有一串非空文本"改成**断言键能解析**（`DescribeTheme_ResolvesTheKey`）。**③ 其余 11 条**：撤销描述 3（`Wiring.Canvas`/`Wiring.DockAggregate`/`DesignCanvas.Input`）、从站显示名 1、画布占位字 1（`DefaultTexts.ImagePlaceholder` 由 `const` 改成**属性** —— const 编译期内联、运行时无法解析，该文件注释早就写着"如需多语言，后续逐个替换为 AppLocalization 资源 key"，本轮兑现）、主题说明 3、AI 图标文件选择器类型名 2；`DefaultTexts` 里纯符号性的 `"100%"`/`"?"`/`"Tab1"` 仍留 `const`。**留下 1 条**：`AiIconDialog` 的 `Name = $"AI图片_{w}x{h}"` 会随资源条目**落盘** ⇒ 归第 27c 项。**基线 52 → 40**（16 → **9** 个文件；剩下 40 条里 **9 条**是第 27c 项的落盘数据）。验收：全量 **2667/0**（本轮基线 2664 ⇒ **+3**：Core 1912→**1915**）、负向验证 **3/3 命中指名用例**（A 去掉排除正则里的 `DiagnosticLog` ⇒ 本地化守卫红，证明规则才是让它通过的原因；B `DescribeTheme` 改成原样返回键 ⇒ 主题说明用例红；C 画布占位字改回中文字面量 ⇒ 本地化守卫红）、字典 zh-CN/en-US 各 **+10** 键、`dotnet build --no-incremental` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**。见 `docs/2026-09-17-localization-hardcoded-inventory.md` §15。
- 2026-09-17：**阶段 27（最后 31 条清完：扫描面只剩"落盘数据"）**——继续推进权威登记表 §4 第 27 项第九批。**31 条的去向**：`SimulatorBuildService` 8、`AiChatPanelView` 7、`Wiring.CanvasTools` 6、`BuildRunDialog` 6、`DesignCanvas.Editing` 4；**复用优先** —— **12 条（39%）直接命中既有键**（`Wiring.CanvasTools` 的 5 条撤销描述与 `EditorCommandCatalog.Describe` 的文案**逐字相同**，造第二份键就又多一处会漂移的抄本；`AiChatPanelView` 的 4 条与 `AiIconDialog` 是同一句话）。**又一个判据漏网（反方向）**：`ErrorLineRegex` 是**正则模式**（识别编译器输出里的错误行），含中文只是因为它同时要认中文输出 —— **翻译它会直接改变匹配行为**（英文编译器输出就再也认不出来），而守卫把"含中文的字面量"一律算成待本地化 ⇒ 新增排除项 **`Regex`**；这与阶段 26 的 `DiagnosticLog`、阶段 20 的多行 `throw new` 是同一族（**判据看"这一行长什么样"，而字符串的类别由它在代码里的角色决定**），三次修法各不相同（让判据跨行 / 让名字自己声明 / 认出承载它的类型）。**顺手修掉一个"本来就是歪的"日志框**：`BuildRunDialog` 的取消框内容行实测 **47 列**、上下边框只有 **44 列**（中文双列而 padding 是手敲空格）⇒ 右边框一直多出 3 列、文案一改继续歪；改成按**显示列宽**补齐（`DisplayWidth`：CJK 2 列其余 1 列；`BoxedContentLine`/`AppendBoxedLog`），配 `BuildRunDialogBoxTests` **6 条**（框内行宽 == 边框宽（中文/英文/混排/空串）、列宽语义、**两语言文案都装得进框**）；负向验证把列宽换成 `text.Length` ⇒ 中文那条立刻红，证明"按列宽算"是判据能红的原因。**新基线 40 → 9（4 个文件）—— 全部是 §4 第 27c 项的落盘数据**（模板控件名 4、资源库元数据 2、报警名默认值 2、AI 图片资源名 1）：**扫描面上已经没有"该翻译却没翻译"的界面文案了**，剩 9 条等"落盘内容要不要跟着语言走"的产品决策（含"存稳定 token、显示时映射"的迁移方案），守卫继续盯着（加一条、改一处都会红）。验收：全量 **2673/0**（本轮基线 2667 ⇒ **+6**：App.UiTests 220→**226**）、负向验证 **3/3 命中指名用例**、字典 zh-CN/en-US 各 **+22** 键、`dotnet build --no-incremental` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**。见 `docs/2026-09-17-localization-hardcoded-inventory.md` §16。
- 2026-09-17：**阶段 28（第 27c 项定性被推翻：落盘默认名跟随 UI 语言，扫描面清零）**——推进权威登记表 §4 第 27c 项。**定性被推翻**：原登记写成"**产品决策**：翻译会让落盘内容随 UI 语言变化，等决策"，复核后发现前提不成立 —— **App 里已经有 9 处**把"新建实体的默认名"走字典（`ScriptNewDefault`/`TrendDefaultName`/`AnimDefaultName`/`comm.conn.new`/`comm.slave.new`/`FaceplateEditorNewTemplate`/`FaceplateEditorParamDefaultName`/`SimVarName`…）⇒ **既有约定就是"新建实体的默认名跟随 UI 语言"**，那 9 条（报警默认名、模板控件名、AI 资源名/分类/描述）只是**没跟上** ⇒ 补齐并撤销"产品决策"定性。**原理**：界面文案每次渲染取字典（切语言立即变）；新建实体的默认名在**创建那一刻**取当前语言写进模型（之后不变）；已落盘内容切语言**不动它**（用户数据）—— 所以"中文界面新建的脚本叫「脚本1」，切英文后仍叫「脚本1」"是**正确**行为。**动手前查过三件安全事项（都通过）**：① 全仓没有 `Name == "…"` 形式的按名字查找；② 控件名**不是**生成代码标识符（C 标识符一律来自 Id：`CIdentifier.ScreenFactory(id)`、`"script_" + SanitizeIdentifierPublic(script.Id)`、`TAG_{Sanitize(variableId)}`，`control.Name` 只进注释与 `Text` 兜底）；③ `SubCategory` **不是**分组/比较键（只有一处 `Text = item.SubCategory` 显示，搜索走 `Tags`，而预设 Tags 本就中英并列）；**刻意不做** token + 显示映射，理由与重开触发条件写在决策文档里。**顺带又抓出 3 条被藏起来的用户可见文案**：基线清零后**排除类别成了唯一过滤器**，于是把 22 条被排除字面量逐条审计，修掉两处判据边界 —— ① 语句头也以 `,` 为界（**兄弟属性不再连坐**：`new ChatEntry { RoleDisplay = Loc[…], Content = "中文" }` 里的 `Content` 曾继承兄弟的豁免）⇒ 2 条聊天文案；② 本地化调用的**非首实参**若是字面量**不豁免** ⇒ `Format("ai.gen.failed", "PNG 渲染失败")` 里的状态文案（该条只对本地化调用生效，一刀切会让多行 `throw new X(nameof(a), a, "中文")` 重新计入 —— 实测踩到，已用 `LocalizationCall` 收窄）；两条都配了单元测试。**诚实记录**：第一次写的审计脚本**漏了 `;{}` 边界**，把两条聊天文案误报成"已被本地化豁免"，差点据此收工；用**保真**重写复核后才发现守卫本身还有口子 —— **审计工具的判据必须和被测对象用同一套判据**，否则审计本身就是假证据。**守卫据清零重写**：删掉逐文件基线字典（清零后没有信息量），改为 ① `NoUnclassifiedHardcodedChinese`（必须为 0，报错列出文件与条数）② 自检换成 `Scanner_StillWorks_WhenExclusionsAreDisabled`（关掉排除仍能扫到 ≥15 条，**独立于基线**）。**新基线：扫描面 = 0**（App 层 22 条中文字面量全部落在 4 个排除类别：`Debug.WriteLine` 15、异常消息 5、诊断 sink 1、正则模式 1）。验收：全量 **2674/0**（本轮基线 2673 ⇒ **+1**：App.UiTests 226→**227**）、负向验证 **3/3 命中指名用例**、字典 zh-CN/en-US 各 **+12** 键、`dotnet build --no-incremental` **0 警告 0 错误**、`ci-gate.sh` **6 步全过**。见 `docs/2026-09-17-persisted-content-language-decision.md` 与 `docs/2026-09-17-localization-hardcoded-inventory.md` §17。
- 2026-09-17：**阶段 29（版本标记四者对应 + 修掉一处 LVGL 目标不兼容）**——推进权威登记表 §4 第 29 项（`Phase 0/5` 清单）。**四个标记都在，但"对应"只是注释里的约稿**：`hmi_runtime.h` 明写着"与 `docs/version-info.md` 同步"、"与 `AllwinnerT113Target.Metadata.Id` 对齐"，两处当时都恰好一致、靠的是人记得，而 Phase 0 要的"四者对应关系"没有任何东西去比。本轮把四组对应变成**可执行判据**（全部文本解析、不需交叉编译）：① 版本串 == "MAJOR.MINOR.PATCH"（bump 数字忘改串的经典漂移）；② `firmware/t113/docs/version-info.md` == 该串（**文档 ↔ 代码**）；③ `HMI_TARGET_ID` == `AllwinnerT113Target.Metadata.Id`（**固件 ↔ 编辑器目标插件**）；④ `hmi_ui_api.h` 的 `HMI_UI_ABI_VERSION`/`HMI_UI_LVGL_VERSION` == 目标插件声明 + 固件要求的 LVGL 版本 ∈ 编辑器版本清单 + 新建工程默认值 ∈ 清单（**固件 ↔ 工程设置**）。**顺带修掉一处真不兼容**：编辑器允许把工程 LVGL 版本选成 **8.3** 而 T113 运行时是 **9.5** —— `LvglApiCompatChecker` **只在工程为 9.x 时**才把 v8 老 API 改写成 v9 写法，选 8.3 就**跳过改写** ⇒ 生成的 C 里留着 9.x 已改名/移除的符号、**板上编译不过**；更糟的是生成的 `.so` 仍写 `lvgl_version = HMI_UI_LVGL_VERSION`（9.5）⇒ 运行时**会接受**它、**声明与实际不符**。处置：把"板子运行时自带的 LVGL 版本 / ABI"写进硬件目标契约（`LvglConfig` 两个**可省略**字段，省略即"未声明"），新增契约层共享规则 `TargetValidation.CheckProjectLvglVersion`，两块内置目标都在 `ValidateProject` 里调用，主版本不一致报 **Error**（"确定不能用"，不是"可能有问题"）；**RK3506 刻意留空**（仓库里没有该板固件可对照，**填一个没验证过的版本号比留空更糟**），未声明的目标自动跳过判定。顺带把"编辑器认得的 LVGL 版本清单"从 `ProjectSettingsView` 的**局部数组**收进 `LvglApiCompatChecker.SupportedLvglVersions`（版本兼容知识所在层），并让 App 侧显示校验结果时**先按 `hw.issue.<code>` 查字典**、查不到再回落插件原文 ⇒ 新规则因此有中英文案，同时**新发现**：插件程序集（`Hmi.Hardware.Builtin`）里的用户可见文案**不在本地化守卫扫描面内**（守卫只扫 `src/HmiEditor.App`），已登记为 §4 第 27d 项。**复核后勾掉** Phase 5 的"CI 增加 firmware host tests 与模拟器导出验证步骤"（`ci-gate.sh` 第 4 步 + `verify.sh` 早已落地）。**一条诚实记录**：全量并行那一次跑出 **1 个失败**（Core 1914/1915）但**没抓到用例名**；随后 `Core.Tests` 单跑**两次全绿（1915/1915）**、`--filter GitWorkflowServiceTests` **8/8 绿（94 ms）** ⇒ 与**阻断项 11**（load-sensitive flaky）签名一致，而门禁本来就把这 8 个用例单独跑以规避它。验收：全量 **2686**（本轮基线 2674 ⇒ **+12**：Hardware 22→**34**）、负向验证 **5/5 命中指名用例**（A ABI 3→4；B 版本串改 `1.1.1` **连带**文档守卫；C `HMI_TARGET_ID` 改名；D 文档版本改 `v1.0.9`；E 版本清单删掉 `9.5` **连带**目标声明守卫 —— B/E 显示判据**互锁**）、`dotnet build --no-incremental` **0 警告 0 错误**（构建步"警告即失败"当场抓到我自己新测试里的 `xUnit2031` 并修掉）、`ci-gate.sh` **6 步全过**。见 `docs/2026-09-17-version-marker-correspondence.md`。
- 2026-09-20：**阶段 38（`HmiEditor.Scripting` 去留取证：一次被名字带偏的定性）**——承接阶段 37 的 4 个死类型与整程序集去留。**只取证、不改产品代码**。**定性被推翻**：这不是「脚本功能要不要做」——脚本功能是**活的**（模型 / 服务 / `ScriptEditorView` / `lvgl_v9_script.scriban` 全在 `HmiEditor.Core` 与 `HmiEditor.CodeGen`），死的只是那份**编辑器内 MoonSharp 实现**（全仓 **0 条** `using HmiEditor.Scripting`，唯一引用是 App 里「引了从不用」的 `ProjectReference`）。**决定性论据**：生成模板发射 `lua_getglobal(L, "{{binding.function_name}}")` 是运行时按名查找的**真实契约**，而死的 `LuaSnippets` 钩子名（`on_timer_tick` / `on_alarm_triggered`）与当前契约（`on_timer` / `on_alarm`）**对不上**，接进去会生成 `dispatch_*` 找不到函数的脚本。**决策点收敛成一句**：编辑器侧 Lua 校验要不要做强。专题 `docs/2026-09-20-scripting-assembly-triage.md`。**教训：定性不能靠名字** —— `LuaScriptValidator` 这个名字本身就把方向带偏了。
- 2026-09-20：**阶段 39（编辑器侧 Lua 校验做强：MoonSharp 真解析搬进 Core）**——承接阶段 38 的决策点，**拍板：做强**。**实施方式的硬约束**：选项 A 最初设想为「Core 引用 `HmiEditor.Scripting`」——**不成立**（该程序集本身 `ProjectReference` 着 Core ⇒ 反引即**循环依赖**）⇒ 唯一路径是**把真解析搬进 Core**（`Core/Services/LuaSyntaxValidator.cs`），`ScriptEngineService.Validate` 从「查代码含 `function `」升级为**真解析**（错误带**行号 + 中文 + 方言提示**，**UI 零改动**即在状态行显示）；`HmiEditor.Scripting` **整程序集下架**，`LuaSnippets` **删除而非搬迁**。**实施前先实测方言**（MoonSharp 2.0.0 = **Lua 5.2**，25 个用例）：`//` `&` `<<` `>>` 与 `<const>` 被判错、`goto`/`bit32`/`0x1p4`/可变参数/中文标识符通过 ⇒ 用 `DialectHints` 把报错改写成可读方言说明并**测试钉桩**（目标固件 Lua 版本确定后必须重新对齐）。**顺手抓到两处「纸面上的校验」**：`ScriptError.Line` **恒为 0**（拿 `Message` 找坐标，坐标只在 `DecoratedMessage` 里）、「缺少 end」映射查 `'<eos>'` 而实测报 `'<eof>'` ⇒ **两条从未生效**。新增 **31 条**测试（含元判据「4 个内置模板必须通过真语法校验」+ 负控与正控同场）；`DeadTypeGuardTests` 基线 4 → **2 项**。专题 `docs/2026-09-20-lua-syntax-validation.md`。
- 2026-09-20：**全库收口：MoonSharp 旧陈述第二次失效**——阶段 38 刚给 `docs/2026-08-08-hmi-control-research-comparison.md`（2 处）与 `docs/2026-07-30-hmi-optimization-dev-plan.md`（1 处）补了「MoonSharp 校验从未接入产品」的复核段，**阶段 39 一实施它立刻又失效**（现在真接进去了）⇒ 三处各再补「再复核」段，原文一律保留作历史快照。**教训**：同一事实在同一天内被两次反转时，**收口段的措辞要写清它描述的是哪一刻的状态**，否则下一个人无法判断哪一版还有效。

- 2026-09-20：**阶段 40（Lua 方言对齐 + 最后两个死类型确认删除）**——承接阶段 39 的两条「仍未闭环」。**方言对齐的结论口径被改**：① `LV_USE_LUA` **不是 LVGL 的配置项**（全 firmware 树零命中，`lv_lua_bridge` / `lv_binding_lua` 同）⇒ 不是"开关没打开、缺个 `#define`"，而是**整条桥接从未存在过**；② Tina SDK 自带 **Lua 5.1.5**（含源码先建 x86-64 host 二进制）⇒ **"设备侧会发生什么"不必等固件开工就能测**。探针 `tools/probe-lua-dialect.{lua,sh}` 把 `compile`（= 编辑器真解析那一列）与 `run` 分开报，测出 **6 条假阴性**（`goto`/`bit32`/`table.pack`/`table.unpack`/`math.type`/`utf8`），其中 **5 条编译器放行、编辑器原理上看不见** —— 上一轮只登记了假阳性，**漏了整个危险方向**。交付 `LuaDialect`（单一事实源：基准 = MoonSharp 的 Lua 5.2 / 目标 = Unknown / 10 条事实 / 偏差计算 / 文案从基准派生）+ `LuaDialectTests` **8 条**（含元判据 2 条、正控边界 1 条、**触发线** 1 条：目标一被确定就对不齐 ⇒ 红）；**把"待对齐"做成会红的触发线**（写在文档里的待办没人读，红色的测试有人修）。`中文标识符` 实测也是假阴性，但成因是 locale / 构建配置 ⇒ `Minimum` 记 `null`，**不许塞进版本模型**，故假阴性集合是**下界**。**两个死类型**经三层确认（成员级 / 字符串 / **`--no-ignore` 含 `bin`·`obj`**）后 `git rm`；`DeadTypeGuardTests` 基线 **2 → 0**，语义变为「`src/` 里不允许存在无人引用的类型」。**负向验证 4/4**（含"空基线仍会红并点名"），同场正控 43/43 绿。提交 **`4720c0c`**；专题 `docs/2026-09-20-lua-dialect-alignment.md`。
- 2026-09-20：**方言对齐的连带更正**——阶段 37/38/39 里「固件 `LV_USE_LUA` 未定义」的表述**一律作废**（详见上条 ①）。三处已就地补注（阶段 37「仍未闭环⑥」、阶段 38 专题 §6①、阶段 39「已知边界」表 ④⑤），原文保留作历史快照。
- 2026-09-21：**阶段 63（闭环 #48：「引用」= 代码引用；顺带量出 #49）**——`LangConstantUsageGuardTests` 问的是「有没有人**用**这个常量」，数的却是「这个名字在 `src/`+`tests/` 里**出现过**」——**注释与字符串字面量也算**，于是**在注释里写一个常量名就能把基线悄悄压低一位**（阶段 62 亲身踩到，而**降的方向恰好是「看起来更好」的那边**）。本轮引入 `ReferenceFace{ Raw, NoComments, Code }` 三档显式口径，生产判据走 `Code`：`.cs` 用 `MaskLiterals`（剥注释 **+** 抹字面量内容、**保留 `{…}` 插值孔**）、`.axaml` 用 `StripXmlComments`。**实测三档** `20487/1267` → `19521/1267` → `15806/1272` ⇒ **注释侧收益为 0（纯保险：全仓注释里那 9 处 `Lang.X` 提到的常量全都另有代码引用，唯一那个受害常量已在阶段 62 用「把词改掉」救回）**，**真正活着的是字面量侧 5 例**（`AssetSvgNoShapesHint` 在 `const string src = """…"""` 的**合成源码夹具**里 / `ProjectSettingsCategoryEditor` 被**守卫自己的反断言** `Assert.DoesNotContain` 写成了「有人用」/ `LoginBtn` 是控件名 / `RecipeName`·`RecipeVersion` 是 CSV 表头）。基线 **1267 → 1272**。新增 2 条判据：合成语义判据（**两侧都断言** —— `Raw` 面**确实**被散文/字面量满足＝病根本身；片段用**故意不存在的常量名**，用具名会重演 #48）＋三档单调性（只许变窄 ＋ 必须逐级真的变窄）。**负控 5/5**：同一处提及注入**三种写法**（注释绿 4/4 / 字面量绿 4/4 / **代码引用红 `1271≠1272`**）＋旧口径红 `1267≠1272` ＋ 直通红 3 条；逐条 SHA256 证明**逐字节还原**（第一次拿 `git diff` 比 HEAD 报了假「非空」⇒ **还原校验的基准必须是「注入前的内容」**）。顺带删掉从未使用的字段 `ConstantDecl`。**顺带产出 #49**：键可达性判据的 **M1（`Lang` 常量 → 键）只问「常量还在不在」，不问「常量有没有人读」⇒ 死常量给死键做担保**，specimen = `project.settings.category.editor`（`ProjectSettingsView.axaml.cs:325-330` 的分类清单**恰好 6 项、独缺 `editor`**，而同族另外 6 个常量**就在那 6 行里被代码引用**；该键全仓只出现在两本字典与**补键工具自己的映射表**里）。见 `docs/project-status.md` 阶段 63、登记表 §20。

- 2026-09-20：**阶段 41（Lua 运行前检查：给 `Runtime` 类假阴性一个独立机制）**——承接阶段 40「仍未闭环 ②」。`bit32.band(1, 3)` 是**合法 Lua 表达式**，缺的是运行时的库 ⇒ 真解析无论换成哪个版本都看不见，**必须有独立机制**。**拍板取舍**：做**版本敏感符号扫描**（差异表）而**不是严格白名单** —— "穷举设备侧全局表 + 静态判定哪些标识符是全局引用"在 Lua 这种动态语言上误报会多到用户直接无视警告（局部变量、`_ENV` 改写、设备侧注入的 `hmi_read` / `hmi_write`、`function on_click()` 的名字都不在标准库里），**等于没有**；代价是差异表只是**下界**，缺口由"`RuntimeSymbols` ↔ `Features` 的 Runtime 项双向断言"接住。**关键前提是新增 `ScriptValidationResult.Warnings` 通道**：设备侧目标版本至今未定，同一个 `bit32` 在 5.2 上正常、5.1 上必炸 ⇒ 写进 `Errors` 就是**把"待定"当成"错误"**，用户会看到满屏红色、却在设备上跑得起来的脚本，然后学会整片忽略校验。交付 `LuaDialect.RuntimeSymbols`（5 条）+ `CheckRuntimeSymbols`（剥注释 → **遮蔽字符串字面量** → 逐符号扫描）+ `LuaSymbolStatus`（`Undetermined` / `Unavailable`）+ `ExplainHit`；新增 `UiPalette.ValidationWarn`（黄）；`ScriptEditorView` 显示首条 + 计数 + `ToolTip`，`QuickSave` 不再把校验结论覆盖成绿。`CheckRuntimeSymbols` 的行为**由 `Target` 一个字段决定**（未定 ⇒ 照报；已定且可用 ⇒ 不报；已定且不可用 ⇒ 报并点名目标版本）⇒ 与阶段 40 的触发线是**同一处开关**，不需要第二处去改。新增 `LuaRuntimeCheckTests` **25 条**（含两表漂移元判据、正控「`table.insert` / `math.floor` / `string.len` 都不报」、反假阳性、4 个内置模板零警告），**负控 4/4**（警告写成 Errors / 去掉字符串遮蔽 / 去掉可用性收敛 / 表漂移 —— 注入即红且只红预期那几条）。提交 **`93db3d9`**（+648 / −13）；门禁 **7 步全绿 / `GATE_EXIT=0`**、八程序集 **2908 → 2933**（+25，纯净增），日志 mtime **`17:49:27`** 晚于提交 **`17:48:26`**。专题 `docs/2026-09-20-lua-dialect-alignment.md` §12。

> **说明**：本「变更记录」自**阶段 30** 起未再逐阶段追加（阶段 30～37 缺条目），当前权威脉络以各「`## 阶段 N（…）`」章节与「验证记录」表为准。上四条（38 / 39 / 40 / 41）是补录，不代表该列表已恢复逐阶段维护。
## 阶段 90（闭环 §4 **#61h**：C 类漏网的 5 站 IO 全部迁到三实参出口 —— **直通面 21 → 15 站点**，并把阶段 84 反对那条重载的**推理反着钉住**）— 2026-09-23

### 90.1 一句话结论

`#61h` 的处置方向**没被推翻**（与 #61a / #61f 两次"量的那一遍把登记推翻"不同），
但**登记里的一句话被实测推翻**：原文写「**目标对象都在作用域**」（并列了四个表达式），
而逐站读现场代码后 —— **5 站里有 2 站**的取值表达式在作用域、**被取的对象不在**：
`CommunicationConfigView` 的 `files`（选择器返回值）与 `MainWindow` 的 `outputPath`
都声明在 `try` **之内** ⇒ 直接写会**编译期 `CS0103`**。已就地更正登记（专题 §90.1）。

这一刀更值得记的是**补上了一条此前没有任何判据管的推理**：阶段 84 反对在 BCL / IO 站点上用
`FailureDetail(操作名, 路径, 异常)` 的**唯一**理由是「BCL 的 IO 原文自带目标路径 ⇒ 再包一层
「—— &lt;路径&gt;」路径会**显示两次**」；阶段 85（#61e）把原因那半码化成 **0 实参短句**之后那条理由
消失 —— 但「它消失了」一直只是**推理**，而**没有任何判据会拦住它回来**：
直通面只认 `Format(键, ex.Message)` 那个**形状**（见到 `FailureDetail(op, path, ex)` 就当作"已迁"），
「带上对象」那条判据只认「有没有给路径」、**不认「给了几遍」**。

### 90.2 改动（表 12）

| 层 | 改动 | 关键点 |
|---|---|---|
| 站点 | 5 站 / 5 文件迁到 `UiOperationGuard.FailureDetail(操作名, 目标路径, 异常)` | 逐站落点见表 13；`FontSubsetView` 还缺一个 `using` |
| 字典 | 两本各 **+4** 操作名键 ＝ `ui.op.import.variables` / `ui.op.import.recipe` / `ui.op.import.slave.vars` / `ui.op.export.charset` | 4 个都是「导入/导出**那个东西**」的操作名 |
| 字典 | 两本各 **−5** 旧键 ⇒ **2220 → 2219** | −5 键只带走 **−4 常量**，见 90.5 |
| `Lang` | **+4** / **−4** 常量；`UnusedBaseline` **1120 → 1116** | **相等**判定，必须实测（实测读数见 90.5） |

| # | 站点（阶段 90 后行号） | 目标对象在作用域吗 | 处置 |
|---|---|---|---|
| 1 | `VariableImportView.axaml.cs:112` | ✓ `_selectedFilePath` 是**字段** | 直接传 |
| 2 | `RecipeEditorView.axaml.cs:892` | ✓ `files` 声明在 `try` **之前** | 直接传 |
| 3 | `CommunicationConfigView.axaml.cs:1037` | ✗ `files` 在 `try` **之内** | **承接变量** `string? targetPath` 提到 `try` 外，且与 `File.ReadAllText` **共用** |
| 4 | `FontSubsetView.axaml.cs:90` | ✓ `file` 在 `try` 外 | 直接传（并补 `using HmiEditor.App.Services;`） |
| 5 | `MainWindow.axaml.cs:1600`（原 2 行） | ✗ `outputPath` 在 `try` **之内** | **把目标目录提前到 `try` 外算好** ⇒ 连承接变量都不需要，行数**中性**（见 90.6） |

**第 5 站那两行是同一句话抄了两遍**（状态栏 ＋ 审计行）—— 顺手收成一个局部变量
（`var failure = …`），否则"改一处漏一处"的形状会原样留下。

### 90.3 判据（新增 2 条 ＋ 3 处实测同步）

| # | 判据 / 同步 | 内容 |
|---|---|---|
| ① | **新增** `UiOperationGuardContractTests.PathAppearsExactlyOnce_AndEachHalfInItsOwnLanguage` | **真实** BCL 异常（`File.ReadAllText` 打在真不存在的文件上，**不手搓 `Message`**）＋ 真实路径过三实参出口 ⇒ 路径**恰好 1 次**；中英两语言下操作名与原因那半**各是自己的语言**（英文侧**一个汉字都不许有**，复用 `SystemIssueLocalizationGuardTests.HasCjk`）；两语言的两半**确实不同**（否则是空话）。内含两条**前提重测**断言（`ex.Message` 真带路径、码是 `FILE_NOT_FOUND`）—— 它们变红说明**前提**变了，该重新量，不是改断言 |
| ② | **新增** `DuplicatePathViolations(…)` ＋ `PathOccurrences(…)`（纯函数） | 计数**不用正则**（路径里有 `.` / `(` / `[` / `+`）；`render` 当参数传入 ⇒ 负控能喂旧形状 |
| ③ | 同步直通面基线 | 删 **5 行** ⇒ **19 → 14 组合 / 21 → 15 站点**；自检下界 `>= 20` ⇒ **`>= 10`** |
| ④ | 同步 `UnusedBaseline` | **1120 → 1116** |
| ⑤ | `EveryDetailCallSite_PassesAConcreteTargetPath` | **未改**（用下界）⇒ 加 5 处安全；本阶段实测它能把"填 `null`"精确点名 |

⚠ 新判据里还有一条**被负控逼出来的可读性修正**：`Assert.Empty(bad)` 会被 xunit 把长串**截断**
（实测 `Collection: ["「导入变量失败: /home/…/u"···]`）⇒ 「出现了 2 次」这个关键读数**还没被看到就没了**；
改成 `Assert.True(count == 0, … + string.Join(…))` 拼全文。**红必须自带信息**。

### 90.4 生产负控（**三重**，实测）

| # | 注入 | 实测 |
|---|---|---|
| ① | 某站路径实参换 `null` 字面量 | 「带上对象」判据**精确点名该文件**（`…axaml.cs：目标路径实参写成了 \`null\``）。⚠ 是**能编译**的坏形状 |
| ② | 三实参重载的原因那半换回 `ex.Message` | 新判据报「路径出现了 **2 次**」并把**旧形状的整条文案**打出来：`导入变量失败: <路径> —— Could not find file '<路径>'.` —— 阶段 84 反对的那一幕**字面重现** |
| ③ | 某站回退成 `Format(键, ex.Message)` | 面判据报「**新增** src/HmiEditor.App/Views/FontSubsetView.axaml.cs · font.export.fail」⇒ 「回退会被抓」**在生产语料上**被证明 |

还原一律**写回原文**（不用 `copy2`：mtime 一起被复制 ⇒ MSBuild 不重建 ⇒ 正控跑的是负控的二进制），
三处 `sha256` 前 / 后一致，还原后三个守卫类 **17/17 复跑全绿**。

### 90.5 面读数与字典/常量（**本项确实在直通面上**）

| 面 | 阶段 89 | 阶段 90 |
|---|---|---|
| 直通面（站点 / 组合） | 21 / 19 | **15 / 14** |
| `UnusedBaseline` | 1120 | **1116** |
| 分桶 | M2 828 / M6 269 / M3 18 / 无机制 5 | **M2 828 / M6 265 / M3 18 / 无机制 5**（只有 M6 动） |
| 字典键（zh-CN） | 2220 | **2219** |

⚠ **`#61a` / `#61f` / `#61g` 三阶段都写过「面读数本阶段零变化」**；本项**不一样** ——
它改的就是"站点写成什么形状"，所以这是 `#61b` 之后**第二次**真的把那张面改小。
同一张面在不同阶段是**证词**还是**目标**，取决于改的是哪一层。

**删键的老三步照走**（连 `Lang` 常量一起删 ＋ 查 `sync_localization.py` 的 `VALUE_MAP`（实测不含这 5 键）
＋ 同步 `UnusedBaseline`）；字典 JSON **非排序存放** ⇒ 逐行手术 ＋ `assert count == 1`，不 `json.dump` 重写。

### 90.6 行数守卫逼出来的一次收口（本阶段唯一被守卫拦下的改动）

`SingleFileSizeGuardTests` 对 `src/HmiEditor.App/MainWindow.axaml.cs` 是**相等**判定，
失败信息明写「**涨了 = 治理回涨（先把文件改小）**」。第一版实现给该文件加了 **5 行**
（承接变量 2 行注释 ＋ 1 行声明 ＋ catch 里多 1 行注释）⇒ 门禁当场红：`行数 3018 ≠ 基线 3013`。
**正确动作是把新增压回 0，不是把基线改大** —— 最终把**目标目录提前到 `try` 外算好**
（它只依赖已校验过的 `folder`），于是"承接变量 ＋ 赋值"两行都不需要、`try` 内那行是**搬**走的、
catch 里的说明注释收成**行尾**注释，净变化 **0**。
⚠ 这不是格式洁癖：那个守卫的全部意义就是拦住"本地化/接线又把巨型单类喂回去"，而**它分不出**
哪次增长是正确性修复 —— 所以遇上它时**唯一**诚实的动作是让改动中性。

### 90.7 门禁

| gate1（**红**） | `.workbuddy/tmp/s90-gate1.log` mtime **16:44:00**，`EXIT=1`：**唯一红** = `HmiEditor.Core.Tests.SingleFileSizeGuardTests.File_LineCount_MatchesBaseline`（`MainWindow.axaml.cs 行数 3018 ≠ 基线 3013`）。⚠ **不是**既有缺陷、也**不是**并发红 —— 是本阶段第一版实现真的加了 5 行（见 90.6）；同一跑里 `App.UiTests 479 / 479` 全绿 |
| gate1b（绿） | `.workbuddy/tmp/s90-gate1b.log` mtime **16:47:07**，`EXIT=0` / 7 步全部通过（门禁自报）/ **0 警告 0 错误**（第 3 步是"警告即失败"）/ 路径卫生 **649** |
| 内容批 | **`dbc7eb9`**（11 files changed, 265 insertions(+), 34 deletions(-)，`2026-09-23T16:47:38+08:00`）⇒ gate1b（16:47:07）比提交**早 31 秒** ✓ |
| gate2 | `.workbuddy/tmp/s90-gate2.log` mtime **16:48:53** ⇒ **晚于提交 1 分 15 秒** ✓；`EXIT=0` / 路径卫生 **649** |
| 文档批 | **`90d1044`**（3 files changed, 252 insertions(+), 3 deletions(-)，`2026-09-23T16:50:07+08:00`）⇒ 本批次**只改 `docs/`** |
| gate3（文档批后，绿） | `.workbuddy/tmp/s90-gate3.log` mtime **16:52:28** ⇒ **晚于文档批 2 分 21 秒** ✓；`EXIT=0` / 7 步全部通过 / **0 警告 0 错误** / 路径卫生 **649** / 大队 **3167** |
| 对拍 gate2 vs gate3 | **实质差异 0**（811 / 812；唯一差异是 dotnet「总共 1 个测试文件与指定模式相匹配。」那句的**分行方式**，多重集一致）⇒ 大队 3167、8 台逐台不动 |
| 对拍 gate1b vs gate2 | **0 行差异**（811 / 811；剥墙钟后做**多重集**差集，两侧都空）；路径卫生两跑都是 **649**（本批只改文件、没加文件，该步走 `git ls-files`） |
| 计数 | 大队八项之和 3165 → **3167**（**+2** ＝ 新判据 2 条）。**逐程序集对拍**：`App.UiTests` 477 → **479**，其余 7 项一字不动 ⇒ **纯净增** |
| `UnusedBaseline` | **1120 → 1116** |

⚠ **第四次门禁（gate4，回填批之后）只记在** `.workbuddy/memory/2026-09-23.md`、**不回写本文件**
（与阶段 85–89 一致：`docs/` 里每个数字都停在「最后一次**内容**改动」那一跑上）。

### 90.8 未闭环

1. `#61h` **已闭环**；`#61` 家族仍开着：#61a（其余 A 类产出方的「返回值带码」）/ #61f（同上）/
   #61i（`EXPRESSION_SYNTAX` 的第二实参仍是中文）/ #61j（`CopyAll` 的括号与枚举名 ＋ 两个品牌字面量）/
   #61k（产物注释自身中英不一致）/ #61l（`DANGLING_REFERENCE` 的外来定位串）。
2. **新判据的诚实边界**：① 它断的是**渲染结果**（路径次数 ＋ 两语言各归各），拦得住"原因那半又带上路径"
   （路径会变 2 次），但若将来码化短句**换词而仍不带路径**，它照样绿 —— **这是对的**（那时前提确实没破）；
   ② 它只覆盖**走三实参出口**的渲染，直通面上那 15 个 `Format(键, ex.Message)` 站点由面判据管；
   ③ `PathOccurrences` 数**非重叠**出现次数 ⇒ 路径是自身子串的退化情形（如 `/a/a`）理论上会少数，
   实测的临时目录名带随机后缀、不构成现实风险，但**这是判据不完备的明写**。
3. `status.verify.failed` / `audit.verify.code.failed` 那 2 站**仍无目标路径**（内存操作为主）
   —— 维持"不在本项内"的原判断；它们要的是**另一种**处置。


## 阶段 91（闭环 §4 **#61i**：解析器的语法错误改「码 + 回落 + 实参」—— **`Args[1]` 装记录体是本仓库第一处嵌套实参**；并反转 Core 台账的 `DevMessage` 豁免口径）— 2026-09-23

### 91.1 一句话结论

`#61i` 的处置方向**没被推翻**（码化 ＋ 中文回落 ＋ 实参），但**登记里的数字全错**：
它写的是「`ExpressionEngine` 抛的**三条**中文 `FormatException.Message`」，逐处读代码量出来的却是
**4 处抛错 ＋ 2 档新失败**（空表达式 `ArgumentNullException`、`double.Parse` 的英文 `FormatException`），
接线时又量出**第 6 档**（输入结束 ⇒ `UNEXPECTED_END`）⇒ 码表是 **7 个码 / 7 个输入**。

两件登记没数到的事一并处置：**第二个露出点** `PropertyPanelView.Binding.cs`（属性面板红字）、
以及登记点名的 `DevMessage` 豁免链（**#54 同病**）。

### 91.2 改动（表 14）

| 层 | 改动 | 关键点 |
|---|---|---|
| Core（新 3 文件） | `ExpressionSyntaxIssueCodes`（7 码）/ `ExpressionSyntaxError`（码 ＋ 中文回落 ＋ 实参）/ `ExpressionSyntaxException` | 照 `SvgParseError` / `SvgParseException` 形状 |
| Core（`ExpressionEngine.cs`） | 4 处抛错改码化；空表达式改码；`double.Parse` → `double.TryParse`；新增 `TryCompile(out ExpressionSyntaxError?)` | 码在**抛错点**直接给出 —— 与 `LuaSyntaxValidator` 的 `Contains` 反推**相反**（那是别人给的字符串） |
| App（`ExternalText.cs`） | `ExprSyntaxKey(code)` ＋ `Describe(error)` ＋ `RenderNestedArgs(…)`（白名单式分派） | gen 族填参**前**先渲嵌套实参 |
| 站点 | ① finding 的 `Args[1]` 装**记录体** ② 属性面板删死分支（`msg.expr.error`） ③ 产物注释**保留中文回落** | ③ 由判据**反向**钉住 |
| 字典 / 常量 | 两本 **+7 / −1**（2219 → **2225**）；`Lang` **−1**；`UnusedBaseline` 1116 → **1115** | 分桶只有 **M6 265 → 264** 动 |
| 可达性 | 新增 **M14**（`expr.syntax.`，面 **7**） | 下界与面都不贴实测值 |

### 91.3 判据（`ExpressionSyntaxIssueLocalizationGuardTests` 8 条 ＋ 1 条内嵌负控）

三态闭包（7 = 6 可产出 ＋ 1 `NotProducible`）／两本字典成对／**7 个输入真实过 `TryCompile`** ＋ 双语渲染 =
字典期望串／两语言确实不同／回落自身可用／兜底臂带原文／**嵌套实参不许出记录体转储**／
**六个文件的源码面**（合规写法必须无话可说）。

既有守卫同步：可达性 M14／`GenFindingLocalizationGuardTests` 走 `RenderArg` ＋ 钉语言／
`CoreVisibleTextInventoryGuardTests` **+6 条通道**（67 → **73**）＋ `DevMessage` 口径反转／`UnusedBaseline` 1115。

⚠ **本阶段被既有守卫拦下一次**：`TestAssertionStyleGuardTests` 点名新判据第 672 行
（「精确调用文本 ＋ 字符串包含」断言）⇒ 改成**先落局部变量再断言**。那条守卫守的是"测试怎么写"
（排版漂移假红、文本出现在注释里假绿），**新判据自己踩上去**说明它还在起作用。

### 91.4 生产负控（三重，实测；写回原文、`sha256` 逐字节一致）

| # | 注入（能编译的坏形状） | 实测 |
|---|---|---|
| NC-A | 实参从记录体退回字符串（`error!.Message`） | 失败 **2**：⑦ ＋ ⑧ |
| NC-B | `RenderNestedArgs` 整段跳过（提前 `return args`） | 失败 **1**：⑦ |
| NC-C | 解析器退回裸 `FormatException` | 失败 **8**：③ ＋ ⑧ |

还原后正控 **9/9 绿**；注入 sha `79abd799…` / `1afffb41…` / `ff084aa3…`，
还原 sha 与注入前一致（`0cda4a26c68a` / `da7248c46c44` / `8b001d8ac6ed`）。

⚠ **脚本第一次跑暴露了两个"读数属于脚本、不属于判据"的毛病**（已记入专题 §91.5）：
① `detailed` logger 下的汇总行是「失败数: N」而我的正则写的是 `失败: N` ⇒ **差点把三重全红读成
"三重都不符合预期"**；② NC-B 第一版坏形状**编译不过** ⇒ 那次红是 build-fail 冒充判据红。

### 91.5 面读数与基线（本项**不在**直通面上）

| 面 | 阶段 90 | 阶段 91 |
|---|---|---|
| 直通面（站点 / 组合） | 15 / 14 | **15 / 14（零变化）** |
| `UnusedBaseline` | 1116 | **1115** |
| 分桶 | M2 828 / M6 265 / M3 18 / 无机制 5 | **M2 828 / M6 264 / M3 18 / 无机制 5** |
| 字典键 / 有常量的键 | 2219 / 1774 | **2225 / 1775** |
| 可达机制 | 13 条 | **14 条**（+ M14 面 7） |

⇒ **第四例「面读数是证词、不是目标」**（前三例 #61a / #61f / #61g）：那条 finding 的中文实参走
`Args[1]`，**不在** `Format(ex.Message)` 那张面上。

### 91.6 门禁

| gate1（**红**） | `.workbuddy/tmp/s91-gate1.log` mtime **17:26:14**，`EXIT=1`：唯一红 = `TestAssertionStyleGuardTests.NoExactCallSubstringAssertionsOutsideAllowList`（点名新判据 `ExpressionSyntaxIssueLocalizationGuardTests.cs:672`）。同一跑 `App.UiTests 488/488` 全绿 / `Core.Tests 2118/2119` |
| gate1b（绿） | mtime **17:27:59**，`EXIT=0` / 7 步全过 / **0 警告 0 错误** / 路径卫生 **649** |
| 二次修补 | `ExpressionSyntaxIssueCodes.cs` 注释里的守卫名写错 ⇒ 改对后**重跑**（内容改动不拿旧跑当证词） |
| gate1c（绿） | mtime **17:29:26**，`EXIT=0` / 7 步全过 / 0 警告 0 错误 / 路径卫生 **649** |
| 内容批 | **`f890863`**（17 files changed, 1246 insertions(+), 60 deletions(-)，`2026-09-23T17:30:13+08:00`）⇒ gate1c 比提交**早 47 秒** ✓ |
| gate2 | mtime **17:31:32** ⇒ 晚于提交 **1 分 19 秒** ✓；`EXIT=0` |
| 对拍 gate1c vs gate2 | 实质差异 **1 行**：路径卫生 **649 → 652**（该步走 `git ls-files`，内容批新增 3 个文件 ⇒ 提交前未跟踪）。其余 **28 行逐条同数**（剥墙钟后多重集） |
| 文档批 | **`b08b502`**（3 files changed, 246 insertions(+), 3 deletions(-)，`2026-09-23T17:32:50+08:00`）⇒ 只改 `docs/` |
| gate3（文档批后，绿） | `.workbuddy/tmp/s91-gate3.log` mtime **17:34:12** ⇒ 晚于文档批 **1 分 22 秒** ✓；`EXIT=0` / 7 步全过 / 0 警告 0 错误 |
| 对拍 gate2 vs gate3 | **0 行差异**（剥墙钟后多重集，29 / 29；路径卫生两跑都是 **652**） |
| 记忆批 | **`a734346`**（2 files changed, 132 insertions(+)，`2026-09-23T17:35:37+08:00`）：快照 `…23e.md` + 析出 §6 |
| 计数 | 大队八项之和 3167 → **3176**（**+9** ＝ 新判据 9 条）；逐程序集：`App.UiTests` 479 → **488**，其余 7 项一字不动 ⇒ **纯净增** |

⚠ **第四次门禁（gate4，回填批之后）只记在** `.workbuddy/memory/2026-09-23.md`、**不回写本文件**
（与阶段 85–90 一致：`docs/` 里每个数字都停在「最后一次**内容**改动」那一跑上）。

### 91.7 未闭环

1. `#61i` **已闭环**；`#61` 家族仍开着：#61a / #61f / #61j / #61k / #61l。
2. 新判据的诚实边界：① 断的是「渲染 = 字典期望串」与「六个文件的形状」，**断不了翻译质量**；
   ② ⑧ 是源码面证据，不是端到端 UI 断言；③ `NotProducible` 依赖一个**静态前提**（解析器里再没有
   别的 `throw`）—— 将来有人加了 `throw`，兜底臂那条会红（**那正是要的**）；④ `UNEXPECTED_END`
   与 `UNEXPECTED_SYMBOL` 的分辨靠 `Tokenize` 末尾哨兵的 `Value` 是空串，哨兵表示一变就会红。
3. `CodeGenerator.Screen` 那条路**刻意不接本地化**（产物注释的读者是看 C 代码的人、产物要可复现），
   由判据**反向**钉住。


## 阶段 92（闭环 §4 **#61j**：「生成代码验证结果」的显示文案收成**唯一映射**—— 复制行不再插值枚举名；品牌档第一次有判据）— 2026-09-23

### 92.1 一句话结论

登记写的是「`CopyAll` 的括号与枚举名 ＋ 两个品牌字面量」，量的那一遍（**读，不预设**）量出的是**三件**：

| # | 形状 | 为什么是缺陷 |
|---|---|---|
| ① | **同一族文案有两条独立实现** —— 徽章/分类列走字典，`CopyAll` 拼 `$"[{f.Severity}] {f.Category}: …"` **直接插值枚举名** | 中文界面下界面上写着「错误／结构」，复制出去却是 `Error／Structure` ⇒ **同一条 finding 在两处语言不同**（本项缺陷的本体） |
| ② | 两个 `switch` 的**默认支承担真实语义** —— `_ => Info`（级别）／`_ => CodeGeneration`（分类） | 新增一个枚举取值会**静默落进**默认支 ⇒ 用户看到一条**假标签**。今天 `Info` 与 `CodeGen` 正是这样活着的（由 `_ =>` 承担，不是显式档） |
| ③ | 两个**品牌字面量零判据** —— `Faceplate` / `LVGL API` 硬编码 | 「判过的不做」没有东西钉住 ⇒ 下次有人「顺手翻译」没人拦（与 #61k 的 `MessageNoKey` 同类，但那一族有双向表、这一族没有） |

`finding.category.*` 下**只有 6 个键**（缺 faceplate / lvgl）—— 而它们**刻意不该有**（品牌名不是翻译对象）。

### 92.2 改动（表 15）

| 层 | 改动 | 关键点 |
|---|---|---|
| App（新 1 文件） | `Services/FindingDisplayText.cs`：`SeverityText` / `CategoryText` / `CopyLine` ＋ 两个品牌常量 | 两个 `switch` **刻意无默认支**；品牌档**不查字典** |
| App（`ValidationResultView`） | 徽章 / 分类列 / 复制行**三处**改调该映射；`CopyAll` 的骨架走字典 | 视图里那三处独立实现**整体消失**（5276 → 4520 字符） |
| 顺带 | `AppLocalization.Service["gen.error"]` / `["gen.warning"]` 改回**早已存在却没人读**的 `Lang.GenError` / `Lang.GenWarning` | 这两个常量阶段 89 就建了，使用点一直写字面量（只能靠 M6 桶担保） |
| 字典 / 常量 | 两本 **+1**（`finding.copy.line`，4 槽、**两本刻意同值**）；`Lang` **+1**；`UnusedBaseline` 1115 → **1113** | 分桶只有 **M6 264 → 262** 动 |
| 抑制面 | 两处枚举 `switch` 各加一条 `#pragma warning disable CS8524` | **只抑制噪声、保留信号**（见 92.3） |

### 92.3 一处**实测推翻预设**：枚举 `switch` 的两档警告码必须分清

本项原打算「不给默认支 ⇒ 新增枚举取值时编译器报 `CS8509` ⇒ 门禁红」。首写落地后**编译立刻冒出 3 条警告**，
探针（`.workbuddy/tmp/enumprobe/`，`Sev` 三值枚举）实测把两档码钉开：

| 形状 | 警告码 | 消息里的举例 |
|---|---|---|
| 3/3 全覆盖、**无**默认支 | **CS8524** | 模式 `(Sev)3` ＝ **未命名值**（枚举永远可能持有一个未声明值 ⇒ **固定噪声**） |
| 2/3 缺一个、**无**默认支 | **CS8509** | 模式 `Sev.Info` ＝ **具名成员**（这才是「新增枚举取值没接文案」的信号） |

而门禁判红的口径是 `ci-gate.sh:198` 的 `grep -E ": (warning|警告) "`（**按行文本**判、与 `-warnaserror` 无关）
⇒ 想让「今天 0 警告」与「将来新增取值必红」两全，三条路里只有一条：

| 做法 | 今天 | 将来新增具名取值 |
|---|---|---|
| 给默认支 | 0 警告 | **静默**（用户看到假标签）|
| 项目级 `NoWarn=CS8524` | 0 警告 | CS8509 仍在，但**范围过宽**（全仓）|
| **文件级 `#pragma warning disable CS8524`**（本阶段采用） | **0 警告** | **CS8509 ⇒ 门禁红** ✓ |

⚠ 这是本仓库**第一处 `#pragma warning`**（`grep` 实测 `src/`+`tests/` 此前零先例）⇒ 抑制面本身也进了判据：
被抑制的码**恰好是 CS8524**、全仓不许出现对 CS8509 的抑制、工程文件里不许出现这两个码（抗「升格成 `NoWarn`」）。

### 92.4 判据（`FindingDisplayTextGuardTests` **8 条** ＋ 1 条内嵌负控）

| # | 判据 | 守什么 |
|---|---|---|
| ① | `CopyLine_IsBuiltFromTheSameTextsAsTheUi_NotFromEnumNames` | **同源**：复制行 == 字典模板填上（级别／分类／描述／定位）四个实参；中文侧**一个枚举名都不许出现**（缺陷签名）；再把 `Message` 换成毒化记号 ⇒ 行**一字不变**（证明走的是字典渲染，不是 `finding.Message` 直出） |
| ② | `EveryEnumValue_HasItsOwnDistinctText` | 枚举每个取值**非空 ＋ 两两不同**（重复文案＝「默认支承担真实语义」的可判形状） |
| ③ | `BrandCategories_AreLanguageNeutral_AndAbsentFromTheDictionaries` | 品牌档**双向**：不随语言变、字典里没有它的键；其余 6 档**必须**随语言变 |
| ④ | `BothLanguages_RenderInTheirOwnScript` | 中文侧有汉字／英文侧无汉字；非品牌档 zh ≠ en |
| ⑤ | `CopyLineTemplate_IsOneSkeleton_WithFourSlots` | 模板两本**同值** ＋ 槽位恰好 `{0}…{3}`（缺键／不同值／槽位不对各点名） |
| ⑥ | `MappingStaysSingleSourced` | **源码面**：映射唯一、视图不再自己拼、两个 `switch` 无默认支、抑制面只许 CS8524 |
| ⑦ | `Suppression_StaysNarrow_NeverSilencesCs8509` | 工程文件（`.csproj` / `.props`）里不许出现 CS8509 / CS8524 |
| ⑧ | `NegativeControls_EachBreakIsCaughtByTheRightJudgment` | 5 组内嵌负控（**全部合成出入参**）|

⚠ **⑧ 的第一次写法定错了一次**（本轮实测）：它的正控原来取**磁盘上的真语料/真字典/真枚举**，于是三重**外部**
负控（把仓库临时改成坏形状）会让它**跟着红** ⇒ 读数分不清「判据坏了」与「仓库被注入了」。改成**合成**语料后，
三重外部负控的红集收敛成「恰好一条/一条/两条」（见 92.5）。这与 s91 那本守卫的做法一致。

### 92.5 负控（**判据级三重 ＋ 编译级一重**，实测；写回原文、`sha256` 逐字节一致）

| # | 注入（**都能编译**的坏形状） | 编译 | 红集（恰好） |
|---|---|---|---|
| NC-A | 视图 `CopyAll` 退回手拼枚举名（缺陷本来的样子） | 0 警告 0 错误 | **1**：⑥（点名 `f.Severity}]` ＋ 缺 `CopyLine`） |
| NC-B | 给品牌档补字典键（两本各 +1） | 0 / 0 | **1**：③ |
| NC-C | 新增 `FindingSeverity.Fatal` **且**给两个 `switch` 补 `_ =>` | **0 警告** ← 关键 | **2**：② ＋ ⑥ |
| NC-D（编译级） | 只新增 `FindingSeverity.Fatal`（不给默认支） | **CS8509 ×2**（两个文件各一条，点名 `Fatal`）/ **CS8524 = 0 行** | —— |

⇒ NC-C 与 NC-D 合起来才是这件事的完整证据：**编译器信号（CS8509）与判据信号（②）各守一段**，
而 NC-C 证明「把编译器哄好之后，② 仍然能拦住」。
还原一律**写回原文**（不用 `shutil.copy2`：mtime 一起复制会让 MSBuild 不重建），`sha256` 逐字节核对通过。

### 92.6 面读数与基线

| 面 | 阶段 91 | 阶段 92 |
|---|---|---|
| 直通面（站点 / 组合） | 15 / 14 | **15 / 14（零变化）** |
| `UnusedBaseline` | 1115 | **1113** |
| 分桶 | M2 828 / M6 264 / M3 18 / 无机制 5 | **M2 828 / M6 262 / M3 18 / 无机制 5** |
| 字典键 / 有常量的键 / 没有常量的键 | 2225 / 1775 / 450 | **2226 / 1776 / 450** |
| 可达机制 | 14 条 | **14 条（无新增 —— 本项不发码，只收显示映射）** |
| `App.UiTests` | 488 | **496（+8，恰为新判据条数）** |
| 大队八项之和 | 3176 | **3184（+8 纯净增，其余 7 项一字不动）** |
| 路径卫生 | 652 | **652** |

⇒ 本项与 #61a / #61f / #61g / #61i 同属「**那张面原理上看不见**」的一族（它的形状是「同一件事有两条实现」，
不是 `Format(ex.Message)`）⇒ **第五例「面读数是证词、不是目标」**。
收益的证据落在**新判据读数 ＋ 视图接线面**（`ValidationResultView` 那三处独立实现整体消失）**＋ 基线 −2** 上。

⚠ **读数对拍（gate1b ↔ gate2，各 790 行）另抓到一类「该剥的噪声」**：`[S1] 原包 … 字符` 与 `[保存] 文件 … B`
两跑不同（**10865 / 10866**、**3432 → 4380** vs **3439 → 4396**）。归因是**夹具本身**：
`HmiProject.CreatedAt` / `LastModifiedAt` 的默认值都是 `DateTimeOffset.Now`
（`src/HmiEditor.Core/Models/HmiProject.cs:30,33`），而 STJ 序列化 `DateTimeOffset` 会**裁掉小数秒尾零**
⇒ 同一份夹具序列化出来长度随手跑时刻漂移。**既有噪声、与本项无关**（两跑是同一份代码），
且**任何断言都不读它**（幂等判据读的是**同一次运行内**的 `S1 == S2`，两跑都过）。
⇒ 对拍工具的口径要记一条：**除了墙钟，还要剥时间戳派生的数字**。

### 92.7 门禁

| 跑 | 读数 |
|---|---|
| gate1（**红**） | `.workbuddy/tmp/s92-gate1.log` mtime **18:24:37**，`EXIT=1`：**唯一红** = `LangConstantUsageGuardTests.UnusedLangConstants_MatchBaseline`（「1113 ≠ 基线 1115 … 降了 = 清理了死常量，请把 `UnusedBaseline` 改成 1113」）。⚠ 这条**不是**噪声：它是「`GenError` / `GenWarning` 终于有人读了」的**正面证据**，基线按它自己指的动作改 |
| 修法 | 基线 1115 → 1113 ＋ 追加阶段 92 那一段 ＋ **顺带归位两处停在阶段 89 的陈旧读数**（类注释与基线注释里的 **M6 269**，阶段 90/91/92 三次改动都没跟着改） |
| gate1b（绿） | mtime **18:26:50**，`EXIT=0` / 7 步全过 / **0 警告 0 错误** / 路径卫生 **652** / 大队八项 **3184** |
| 内容批 | **`53febee`**（7 files changed, 902 insertions(+), 29 deletions(-)，`2026-09-23T18:27:13+08:00`）⇒ gate1b 比提交**早 23 秒** ✓ |
| 读数出口 | `HMI_READINGS_OUT=/home/chen/Git/zhishu-hmi/.workbuddy/tmp/s92-readings-gate1b.txt`（**绝对路径**）⇒ 落盘拿到分桶与键数（本文件 92.6 的数字取自它） |
| gate2（绿） | mtime **18:29:31**，`EXIT=0` / 7 步全过 / **0 警告 0 错误** / 路径卫生 **653** / 大队八项 **3184**（比 gate1b 晚 **2 分 41 秒**，在提交**之后** ✓） |
| 对拍 gate1b ↔ gate2 | 剥墙钟多重集：**只差 1 行** = 路径卫生 **652 → 653**。口径 `git ls-files 'src/*' …`，而 gate1b 跑在内容批**之前** ⇒ `FindingDisplayText.cs` 当时未跟踪、提交后进清单 ⇒ **不是回归** |
| 文档批 | **`f3788a4`**（3 files changed, 281 insertions(+), 2 deletions(-)，`2026-09-23T18:33:05+08:00`）—— **只改 `docs/`**（本文件 §92 ＋ 专题 §92 ＋ 登记表 #61j 闭环 / #61 母项追加） |
| gate3（文档批后，绿） | `.workbuddy/tmp/s92-gate3.log` mtime **18:34:46** ⇒ **晚于文档批 1 分 41 秒** ✓；7 步全过 / **0 警告 0 错误** / 路径卫生 **653** / 大队八项 **3184** |
| 对拍 gate2 ↔ gate3 | **0 行差异**（剥墙钟多重集，29 / 29）⇒ 文档批不动任何代码路径 |
| 记忆批 | **`fe6d714`**（2 files changed, 152 insertions(+)，`2026-09-23T18:37:57+08:00`）：`MEMORY.md` 快照 `…memory-hmi-full-2026-09-23f.md`（逐字节副本，`sha256 3ab0b72d175d`）＋ 析出 §7 |
| 回填批 / gate4 | 回填批（只改 `docs/`）之后补跑 **gate4**；按阶段 85–91 惯例**只记** `.workbuddy/memory/2026-09-23.md`、**不回写本文件** |
| gate1b ↔ gate2 | **只差 1 行** = 路径卫生 **652 → 653**（口径 `git ls-files 'src/*' …`；gate1b 跑在内容批**之前** ⇒ `FindingDisplayText.cs` 当时未跟踪）⇒ **不是回归** |

### 92.8 未闭环

1. `#61j` **已闭环**；`#61` 家族仍开着：`#61a` / `#61f` / `#61k` / `#61l`。
2. 本判据的诚实边界：① 断的是「渲染 = 字典模板 ＋ 视图/映射的形状」，**断不了翻译质量**；
   ② 「视图里不许再冒出第二处映射」只钉了 `FindingCategory.` 与手拼形状 —— 视图里再用**裸中文字面量**
   拼一遍由既有的 `HardcodedLocalizationGuardTests` 兜着（本文件**不重复**那道墙）；
   ③ 剪贴板那一步（用户是否真点了「复制」）**不在**判据面上。
3. 复制行模板 `finding.copy.line` 两本**刻意同值**（结构模板，翻的是实参）—— 它是全字典**第 39 个**同值键
   （另 38 个：`ai.api.key` / `data.type.bool` / `cb.resolution.1024x600` 这类技术名与字面量，阶段 92 实测）
   ⇒ 它**不能**走 `CsvImportIssueLocalizationGuardTests.PairingViolations`（那条规则会把「同值」判成「没翻」），
   由 ⑤ **双向**单独钉（缺键 ⇒ 红、两本不同值 ⇒ 也红）。
4. 颜色映射（`SeverityColor`）**留在视图**：它没有语言维度、不属于本项缺陷族；它的 `switch` 同样无默认支 ＋ 只抑制 CS8524，
   由 ⑥ 一起守。

## 阶段 93（闭环 §4 **#61l**：悬空引用的定位串改成 Core 记录体 —— 英文界面下不再中英混排）— 2026-09-23

### 93.1 一句话结论

`CrossReferenceAnalyzer.FindDanglingReferences` 以前把 findings 的**定位**直接拼成中文串
（`[屏名] 控件 '名' (属性)`）返回，调用方给 `LocationCode` **留空**、原样直出
⇒ 英文界面下这一半是中英混排。本阶段把它改成 **Core 记录体 `DanglingRefLocation`**
（「定位码 ＋ 中文回落原文 ＋ 实参」三段式，与 `SvgParseError` / `ExpressionSyntaxError` /
`CsvImportIssue` 同形），正常路径走 `gen.loc.*`。**登记里的两处表述被量的那一遍推翻**（见 93.2）。

### 93.2 🩸 实测推翻登记的两处表述（本阶段最该记的一条）

| 登记原话 | 实测 |
|---|---|
| 「**同一串在两个地方露出**：交叉引用面板 ＋ 验证对话框」 | **不成立**。两族是**两套独立措辞**：悬空族 `[屏名] 控件 '名' (属性)` ／面板族 `控件 名 绑定变量 (方向)`（`ReferenceLocation.Description`）。面板上**根本没有定位串这条文本** |
| 「两个消费点必须**一起收口**」 | 真消费点只有**一个**（验证对话框）；另两处是 `MainWindow.axaml.cs:1066,1213` 只取 `.Count` ＋ `:1252` 的 `Debug.WriteLine` |

⇒ 本项在阶段 89 被「刻意不吞」搁置两轮，**顾虑建立在一个不成立的前提上**。
纪律复述：**登记写的是假设，读出来的才是形状**。

量的那一遍**顺带**产出第三件（登记里没有）：`HmiAction.GetDescription()`（`HmiAction.cs:195`，13 条中文）
被**两个方向**共用且要求相反 —— 面板 `CrossReferenceAnalyzer.cs:150,167` **必须翻**、
产物注释 `CodeGenerator.Actions.cs:57,64,73` 的 `Cmt(sb, …)` **必须固定中文**
⇒ **新开 #61m**（要动的是族级出口拆分，本刀刻意不碰）。

### 93.3 改动（表 6）

| 文件 | 改动 | 读数 |
|---|---|---|
| `src/HmiEditor.Core/Services/DanglingRefLocation.cs`（**新**） | 记录体（`Code` / `Message` / `Args`）＋ 3 个工厂方法 `OfBinding` / `OfLegacyBinding` / `OfAction`；回落原文**逐字**抄改动前的 4 个站点 | 3 条中文字面量 |
| `src/HmiEditor.Core/Services/GeneratedCodeIssueCodes.cs` | `FindingLocationCodes` ＋ `SCREEN_CONTROL_ACTION`（**立档理由**：实参是数据、「动作」是键自身的固定词）；「只有 4 种」→ **5 种** | +16 / −4 |
| `src/HmiEditor.Core/Services/CrossReferenceAnalyzer.cs` | `FindDanglingReferences` 返回 `List<(DanglingRefLocation, string)>`；4 个拼串站点改调工厂方法 | 含汉字行 **76 → 72** |
| `src/HmiEditor.Core/Services/GeneratedCodeValidator.cs` | `loc.Code` / `loc.Args` 接进 `LocationCode` / `LocationArgs`；⚠ 顺带改正类注释里一张**从来不存在的** `NeutralLocation` 表 | 485 → 488 行 |
| `src/HmiEditor.App/MainWindow.axaml.cs` | `Debug.WriteLine` 走 `loc.Message`（调试行不随界面语言变；与改动前**逐字相同**）| 3013 → **3014** |
| 两本字典 | 各 +1 键 `gen.loc.screen_control_action`（逐行手术插入） | **2226 → 2227**，各 +1 行 |

⚠ `MainWindow.axaml.cs` 有 **3013 行**硬基线（相等判定）⇒ 第一版 4 行注释把它顶到 3017（**gate1 红**），
收口时压成 **1 行**、基线同步 **3014**（涨的是注释不是代码，已在基线注释写明）。

### 93.4 判据：**删掉一整档** ＋ 立一条钉住新形状的

**删**：「外来定位」那一档 —— `ForeignLocation` 表 ＋ `ForeignLocationViolations` ＋
`[Fact] ForeignLocations_AreStillForeignAndStillUncoded` ＋ 负控 NC4 ＋ `EveryLocation` 的放行支，
**五件一起删**。#61l 闭环后它**空了**，而「空表 ＋ 恒真判定」是没有负控的判据（按纪律不发布），
留着更会变成「把定位没码化合法化」的永久字段（登记 #54 的老病）。
⇒ `EveryLocation_…` 变成**两档闭合**：有码走字典 / 空码直出且不许含汉字。

**立**：`DanglingRefs_CarryACodedLocation_AndKeepTheOldFallbackFormat` ＋ 纯函数
`DanglingLocationViolations` ＋ 源码面 `WiringViolations` 新增 `LocationProducer` 档。
⚠ **为什么非立不可**：这条定位的码**不由 `GeneratedCodeValidator` 自己拼** ⇒
`WiringViolations` 里那个 `FindingLocationCodes.` **文本计数对它看不见**（把 `loc.Code` 改回 `""`
计数一个都不掉）；而 `SCREEN_CONTROL_PROPERTY`（`EXPRESSION_SYNTAX` 也在用）与 `SCREEN_CONTROL`
（Faceplate 三档也在用）**都不会变死码** ⇒ 只靠「三态闭包顺带发现」**兜不住**。

**夹具铺满四站点**：`VariableBindings` / `VariableBindingId` / `Actions` / `Action` 各一
⇒ 三个定位码全部被真实产出（否则 `SCREEN_CONTROL` / `SCREEN_CONTROL_ACTION` 会被闭包判**死码**）。

**台账三处同步**：通道 ② `CrossReferenceAnalyzer.cs` **13 → 9**；新通道
`DanglingRefLocation.cs` **3**（消费方 `App/Services/ExternalText.cs`，标记 `DescribeLocation(GeneratedCodeFinding`）；
总数 **73 → 72**。⚠ 顺带归位类注释里那句停在**阶段 44**的「**6 条通道（61 条文案）**」→ **10 / 72**
（阶段 87/88/89/91/93 五次新增通道都没回头改；同款归位见阶段 92 的 M6 269）。

### 93.5 负控（判据级 —— NC4 四支 ＋ NC5 新增两支）

| # | 注入 | 红集点名 |
|---|---|---|
| NC4-① | `LocationCode = ""` / `LocationArgs = null`（**本刀之前的样子**） | 「LocationCode 为空 ⇒ …中英混排」 |
| NC4-② | 换**不属本族**的码（`GENERATED_CODE`） | 「不在悬空族内」 |
| NC4-③ | 实参个数与档位不匹配（3 实参档只给 2 个） | 「模板要 3 个」 |
| NC4-④ | 回落原文改格式（`Location = "main.value"`） | 「回落原文不再保留旧格式」 |
| NC5-③b | 定位产出方掏空（三个码一个都不发） | 三个码名各点名 |
| NC5-③c | 调用方不接线（`loc.Code` 丢掉） | 「没把记录体的码接进 LocationCode」 |

⚠ NC4-③ 取样点第一次写法定错（取「第一条悬空 finding」，档位随产出顺序漂）⇒ 改成按
`LocationCode == SCREEN_CONTROL_PROPERTY` **钉死**（本项目踩过「按下标挑到不匹配那一档」的坑）。

### 93.6 面读数与基线

| 读数 | 阶段 92 | 阶段 93 |
|---|---|---|
| 无代码引用的常量 | 1113 | **1113**（不动） |
| 分桶 | M2 828 / M6 262 / M3 18 / 无机制 5 | **同左，一字不动** |
| 字典键 | 2226 | **2227** |
| 有常量的键 / 没有常量的键 | 1776 / 450 | **1776 / 451** |
| 可达机制 **M13** 面 | 21 | **22**（下界同步抬满） |
| 直通面 | 15 / 14 | **15 / 14**（**零变化**） |
| 大队八项 | 3184 | **3184**（删 1 Fact ＋ 加 1 Fact，净 0） |

⇒ 「字典 +1 而基线**不动**」不矛盾：`gen.loc.*` 靠 `GenLocationKey` **推导**、没有 `Lang` 常量，
可达性由 M13 担保 ⇒ 已写进 `LangConstantUsageGuardTests` 的基线注释（**第四次**在那里解释数字为什么动）。

⇒ 直通面第三次零变化 ⇒ 「**面读数是证词、不是目标**」**第六例**（前五例 #61a / #61f / #61g / #61i / #61j）。
本项**从来不在**那张面上（它收的是「界面语言」，不是「`ex.Message` 直通模板」）。

### 93.7 门禁

| 跑 | 读数 |
|---|---|
| gate1（**红**） | mtime **18:53:29** ⇒ 3 红且**全是正面证据**：`SingleFileSizeGuardTests`（3017 ≠ 3013）＋ 台账 ×2（通道 13 vs 实扫 9；新文件登记 0 vs 实扫 3） |
| gate1b（**红**） | mtime **18:54:53** ⇒ 2 红：`VisibleChannels_CountIsWhatTheDocSays`（73 vs 72）＋ `VisibleChannels_EvidenceStillExists`（标记子串 `DescribeLocation(finding)` 在 `ExternalText.cs` **不存在**）|
| gate1c（绿） | mtime **18:56:42** / `EXIT=0` / 7 步全过 / **0 警告 0 错误** / 路径卫生 **653** / 大队八项 **3184** |
| 内容批 | **`c8181f7`**（12 files，+291 / −109，`2026-09-23T18:58:32+08:00`）⇒ gate1c 比提交**早 1 分 50 秒** ✓ |
| gate2（内容批后，绿） | `.workbuddy/tmp/s93-gate2.log` mtime **18:59:52** ⇒ `EXIT=0` / 7 步全过 / **0 警告 0 错误** / 路径卫生 **654** / 大队八项 **3184** |
| 对拍 gate1c ↔ gate2 | **只差 1 行** = 路径卫生 **653 → 654**（新文件 `DanglingRefLocation.cs` 提交后进 `git ls-files`）⇒ **不是回归** |
| 文档批 | **`14222b0`**（4 files，+368 / −7，`2026-09-23T19:00:47+08:00`）—— **只改 `docs/`**（本文件 §93 ＋ 专题 §93 ＋ 登记表 #61l 闭环 / #61 母项 / 新开 #61m ＋ 盘点文档 §11）|
| gate3（文档批后，绿） | `.workbuddy/tmp/s93-gate3.log` mtime **19:02:08** ⇒ **晚于文档批 1 分 21 秒** ✓；`EXIT=0` / 7 步全过 / **0 警告 0 错误** / 路径卫生 **654** / 大队八项 **3184** |
| 对拍 gate2 ↔ gate3 | **差集 0 / 0**（剥墙钟多重集，29 / 29）⇒ 文档批不动任何代码路径 |
| 记忆批 | **`c30e518`**（2 files，+139，`2026-09-23T19:04:57+08:00`）：`MEMORY.md` 快照 `…memory-hmi-full-2026-09-23g.md`（逐字节副本，`sha256 e7866b9f8582`）＋ 析出 §8；码元 **7989 / 8000** |
| 回填批 / gate4 | 回填批（只改 `docs/`）之后补跑 **gate4**；按阶段 85–92 惯例**只记** `.workbuddy/memory/2026-09-23.md`、**不回写本文件** |

### 93.8 未闭环

1. `#61l` 已闭环；家族仍开着 `#61（本体）/ #61a / #61d / #61f / #61k`，**新开 #61m**。
2. 判据断「码在族内 ＋ 实参个数 ＋ 回落保格式 ＋ 源码面接线」，**断不了翻译质量**。
3. `MainWindow` 那行 DEBUG 专有的中文诊断行**不在**判据面上（刻意保留、走回落原文），由代码审查兜着。
4. 本刀的实参全是字符串（屏幕名/控件名/属性名）⇒ 不需要 `RenderNestedArgs` 那支嵌套渲染；
   ⚠ 若将来把 `HmiAction`（#61m）塞进定位实参就会**需要**，那时要一起收。

---

## 阶段 94（闭环 §4 **#61m**：动作展示名的出口拆成两条 —— 面板走字典 / 产物吃固定中文）— 2026-09-23

### 94.1 一句话结论

控件动作的展示名有**三个露出点**，改动前**全是 Core 拼的中文**（英文界面下三处都是中文，
动作 chip 对「无动作」还显示英文 Id `None`）；而同一个映射**另有一个语言要求相反的方向**
（产物注释**必须固定中文**，产物可复现性）。本阶段把出口**拆成两条**：界面侧新增
`App/Services/ActionTextKeys.cs`（键推导的唯一实现，**27 个键**），属性面板两处 ＋ 交叉引用面板一处
全部改走它；Core 的中文**留在原地**（产物侧直用、界面侧只当**回落**）。

### 94.2 量的那一遍（读，不预设）—— 登记只写了**三条产出面里的一条**

登记 #61m 只点名 `HmiAction.GetDescription()`（13 条）。逐处读码后，**同一个映射共 60 条中文 / 3 条产出面**：

| # | 产出面 | 中文 | 消费方向 | 登记里 |
|---|---|---|---|---|
| ① | `HmiAction.GetDescription()`（`Models/HmiAction.cs:195`） | **13** | 交叉引用面板（`{Binding Description}`）＋ **产物注释**（`CodeGenerator.Actions.cs:57,64,73` 的 `Cmt(`） | ✅ 写了 |
| ② | `ActionTypeMeta.DisplayName`（`Design/ActionTypeMeta.cs:25`） | **12** | 动作 chip ＋ **产物两处**（权限门注释、动作→权限表） | ❌ 没写（同一个双方向病） |
| ③ | `ActionMeta.DisplayName`（`GetAvailableActions` 的 6 张表） | **35** | 只进动作下拉框 ⇒ **单方向** | ❌ 没写 |

⇒ 三处**无一条落盘**（落盘的是 `ActionType` / `Target` / `Value` 这些 Id）⇒ 台账里「DATA：落盘/进产物」
这个豁免理由**在代码里不成立** ⇒ 与 **#54** 同病（豁免表的理由没人去代码里核）。

顺带量出三件（登记里没有）：① 同一概念两套措辞（`置位 (ON)` / `置位`；`设置变量` / `设置变量值`，
后者**在同一个方法的不同控件表里**）；② chip 对「无动作」**显示英文 Id** `None`（`DisplayName`
没有 `None` 分支、兜底返回原 Id）；③ `ReferenceLocation` 是 Core DTO、`{Binding Description}` 直绑
⇒ **面板缺一个渲染点**（Core 不引本地化，这是本刀必须动视图层的原因，不是审美）。

### 94.3 处置：一份中文、两个方向、各走各的路（内容批 `f7bfe52`，13 files，+870 / −59）

**关键决定：不新增 Core 记录体、不新增码表** —— 与 `RoleTextKeys`（阶段 66 闭环 #50）**同形**：
Core 给稳定的 **Id**，App 侧按 Id 推键。理由：Core 是库、不引 `Lang` / `LocalizationService`。

| 方向 | 谁读 | 措辞 |
|---|---|---|
| **生成产物** | `CodeGenerator.Actions.cs` ×5、`SecurityConfigCodeGen.cs` ×1 | Core 那两份 —— **冻结中文** |
| **编辑器界面** | 动作下拉 / 动作 chip / 交叉引用面板 | `ActionTextKeys.TypeName` / `.Describe` / `.DescribeReference` —— 走字典 |

⇒ 于是「同一件事」有了**两份存储**。按本项目反复量到的形状，**两条消费路径不是多余一份、而是必然漂移的两份**
⇒ 用**相等式**钉住（判据 J3：zh 字典 `action.type.*` 逐字等于 `ActionTypeMeta.DisplayName`）。

| 文件 | 改动 |
|---|---|
| `Core/Models/HmiAction.cs` | `ActionMeta` **删掉 `DisplayName`**；6 张表 **−35 条中文**（条目数一条没少：`new("` 计数与 tag 序列逐条相等）；`GetDescription()` 加文档注释标明两个方向 |
| `Core/Design/ActionTypeMeta.cs` | 类注释改正（原句"属性面板与代码生成共用"已不成立）；`DisplayName` 标注"**只给产物用**" |
| `Core/Services/CrossReferenceAnalyzer.cs` | `ReferenceLocation` 新增 `HmiAction? Action`；动作那 2 处填它（`Description` **保留**为回落原文） |
| `App/Services/ActionTextKeys.cs`（**新**） | `TypeName` / `Describe` / `DescribeReference` ＋ `MappedActionTypes`（13 个 Id） |
| `App/Views/PropertyPanelView.Actions.cs` | 下拉与 chip 两处改走出口 |
| `App/Views/ReferenceRow.cs`（**新**） | 交叉引用面板的行（App 侧渲染点；模板里五个绑定**逐字未动**） |
| `App/Views/CrossReferenceView.axaml(.cs)` | `x:DataType` 改 `views:ReferenceRow`；结果集合改行类型 |
| `Resources/Lang.cs` | ＋**27** 常量（13 短名 ＋ 13 描述 ＋ 1 前缀） |
| `Core/Resources/Strings.{zh-CN,en-US}.json` | 各 ＋**27** 键（**2227 → 2254**） |
| `App.UiTests/ActionTextLocalizationGuardTests.cs`（**新**） | 9 条判据 J1–J9 |
| `Core.Tests/CoreVisibleTextInventoryGuardTests.cs` | 台账归位（见 94.4） |

⚠ **`ReferenceLocation.Action` 挂对象、不挂「码 ＋ 实参」**（与 #61l 的 `DanglingRefLocation` 不同）：
动作描述的实参组合**按类型而异**（0/1/2 个），固定实参数组会让 `LocalizedFormatCallSiteTests`
的 C1（实参个数 == 占位符个数）**失去意义** —— 那条判据读的正是调用点上的**字面实参个数**。

**措辞收敛**（本刀唯一一处用户可见变化）：下拉与 chip 合并成**一套**、取 Core 那一套（与产物注释一致）
⇒ `置位 (ON)` → `置位`、`复位 (OFF)` → `复位`、`设置变量值` → `设置变量`。
选这个方向是因为长措辞**改不动产物那一侧**（产物冻结），两边不一致就没法用相等式钉住。

### 94.4 台账归位（两处，都是「归类」不是「数量」）

| 登记 | 改前 | 改后 | 为什么 |
|---|---|---|---|
| `Models/HmiAction.cs` | `Pending` = **48**（理由"DATA：落盘/进产物"） | `VisibleChannels` = **13**（标记 `ActionTextKeys.cs` 的 `Lang.ActionDescSwitchScreen`） | 相减的 35 条已随 `ActionMeta.DisplayName` 进字典 ⇒ **实扫就是 13**；剩下 13 条只走**回落**（与通道 ⑦/⑨ 同形）⇒ 消费标记落在**出口**那一层 |
| `Design/ActionTypeMeta.cs` | `Pending` = 12（理由"动作元数据（图标/分组/展示名）"） | `Pending` = 12（理由改 **GEN**：消费方是**生成器**） | 面板侧已拆走 ⇒ 只剩两个产物写入点 ⇒ 与 `Services/ScreenLayerRefs.cs`（4 条）同类 |

⇒ `VisibleChannels` 合计 **72 → 85**（**+13**，归类归位；同一文件实扫 48 → 13）、通道 **10 → 11**；
`VisibleChannels_CountIsWhatTheDocSays` 常数随之改，并写明这是**第四次**解释"数字为什么动"
（前三次：87/88 的"新造"、91 的"第一次被看见"、93 的"搬迁"）。

### 94.5 判据 `ActionTextLocalizationGuardTests`（J1–J9）

| # | 判据 | 单独能看见 |
|---|---|---|
| **J1** | `MappedActionTypes` ↔ `ActionTypeMeta` 公开 Id 常量**双向**一对一 | 新增动作类型漏建键 ⇒ 界面静默显示英文 Id |
| **J2** | 每个 Id 的 `TypeName(id) != id` | **回落分支会让"漏映射"静默通过** |
| **J3** | zh 字典 `action.type.*` **逐字等于** `ActionTypeMeta.DisplayName` | 界面与产物两套措辞漂移（**两侧都断言**） |
| **J4** | **成对**：en 不含汉字 **且** zh 确实含汉字 | 只断前一半会被"字典本来就没中文"满足 |
| **J5** | zh 下 `Describe(a)` **逐字等于** `a.GetDescription()` | 「**零用户可见变化**」的**证明**（不是声明） |
| **J6** | en 侧带得上实参、无汉字；无目标时不出现 `{0}` 字面量 | C1 管不到"实参有没有被填进模板" |
| **J7** | 切 en 后 Core 那两串中文**逐字不变**（＋下界 12/13 条） | 产物可复现性：谁把 Core 也接字典，产物注释就随 UI 语言变 |
| **J8** | **端到端**：真实工程 → `CrossReferenceAnalyzer` → `ReferenceRow` | 分析器有没有把动作挂到 `Action` 上 ＋ 渲染点真的读了字典 |
| **J9** | 表外类型回落成 Id（**诚实边界**） | 把"回落"与"漏映射"分开（后者 J2 抓） |

⚠ **不复写别人的规则**：占位符 ↔ 实参个数由 `LocalizedFormatCallSiteTests`（C1/C3）管、
App 层中文硬编码由 `HardcodedLocalizationGuardTests` 管 —— 本文件只在注释里指过去。

**负控 5 支（全在真仓库上，还原一律写回原文 ＋ `sha256` 逐字节核对）**：
NC-A（删 `SetBit` 分支）⇒ **3 红** J2/J3/J4；NC-B（zh 字典改字）⇒ **1 红** J3；
NC-C（Core 产物表改字）⇒ **1 红** J3；NC-D（`ReferenceRow` 不走出口）⇒ **1 红** J8；
NC-E（`MappedActionTypes` 删 `None`）⇒ **1 红** J1。**5/5 符合预期**，注入后**编译均未失败**
（红的是**判据**不是编译）；汇总 `.workbuddy/tmp/s94-nc-summary.log`（19:23:15）。

### 94.6 🩸 两次被拦下（一次真红、一次环境）

**(a) 锚点替换把 `Lang.HwIssueLvglVersionMismatch` 整行吞掉**（`.workbuddy/tmp/s94-gate1.log`，19:24:36）：
插入动作文案块时拿"末尾那条 `const` 行"当锚点做替换、**锚点行自己没被写回** ⇒ 常量消失
（上方的「硬件目标校验文案」注释头还在，留下一个**孤儿注释头**）。这条常量**本来就没人读**
（键 `hw.issue.lvgl_version_mismatch` 由 `PluginTextKeys.KeyFor(code)` 运行期派生 ⇒ 走机制 **M3**），
所以**编译**与**键可达性**两条守卫都不报 —— **抓到它的只有门禁那一条**
（`1112 ≠ 基线 1113`，且给出的处置建议是"把基线改成 1112"）。
⇒ **写回原文、基线不动**（`s94-fix1.py`：干跑 → 落盘 → 回读校验；判据 `git diff` 相对 HEAD **只有 `+` 行** = `32 0`）。
⚠ 三种"顺手"处置都被否掉：**改基线成 1112**（把事故洗成"清理"，下次真删死常量就看不出来）、
**留着不管**（下次谁读这条常量就编译不过）、**连注释头一起删**（越删越像有意为之）。
纪律：**"降了 = 清理了死常量"是建议，不是结论** —— 先回答"是哪一条、为什么没的"，再决定动不动基线。

**(b) gate2 第一跑 `No space left on device`（环境，不是回归）**：内容批之后的 gate2
（`s94-gate2.log`，19:32:11，晚于提交 1 分 8 秒）红在
`Device.Tests.T113SoBuilderTests.ResolveContext_MissingSdk_ReportsMissingItems`，异常是
`System.IO.IOException : No space left on device : '/tmp/sdk-<guid>/generated_hmi.c'`。
**根因是环境容量**：本沙箱 `/tmp` 是 **10 MB 的 tmpfs**（`mount` 实测 `size=10240k`），
而门禁第 [5] 步同时起 8 个 testhost、其中若干个往 `/tmp` 写产物 ⇒ 偶发打满。
**三条证据**：① 前 2 分钟的 gate1b **同代码全绿**；② 单跑该程序集 **13/13 全绿**（253 ms）；
③ 失败在**写入**而非断言。⚠ 这三条只证明"与本次改动无关"，**不**证明"以后不再犯"（见 94.8 第 6 条）。

### 94.7 读数与基线

| 读数 | 阶段 93 | 阶段 94 |
|---|---|---|
| 无代码引用的常量 | 1113 | **1113**（不动；中途曾掉到 1112，见 94.6a） |
| 分桶 | M2 828 / M6 262 / M3 18 / 无机制 5 | **同左，一字不动** |
| 字典键 | 2227 | **2254**（＋27） |
| 有常量的键 / 没有常量的键 | 1776 / 451 | **1803 / 451** |
| 机制面 **M1-Lang 常量** | 663 | **690**（＋27）；**M13 22 / M14 7 不动** |
| 直通面 | 15 / 14 | **15 / 14**（**零变化**） |
| 大队八项 | 3184 | **3193**（＋9 = 新增判据条数，**纯净增**） |
| 逐程序集 | `App.UiTests` 496 / `Core.Tests` 2119 | `App.UiTests` **505** / `Core.Tests` 2119 |
| 路径卫生 | 654 | **654** |

⇒ 直通面第四次零变化 ⇒ 「**面读数是证词、不是目标**」**第七例**
（前六例 #61a / #61f / #61g / #61i / #61j / #61l）。本项**从来不在**那张面上。
⇒ 「基线 1113 不动、字典 +27」不矛盾：那 27 个常量**都有代码引用**（`ActionTextKeys.cs` 在读），不进本集。

### 94.8 未闭环

1. `#61m` 已闭环；家族仍开着 `#61（本体）/ #61a / #61d / #61f / #61k`，**新开 #61n / #61o**。
2. 翻译质量不在判据面上（en 的 `Set` / `Reset` / `Momentary On` 好不好用断不了）。
3. 未知动作类型仍回落成 Id（J9 钉住，结构上中性）。
4. `CrossReferenceAnalyzer` 那 2 条 `$"动作 {…}"` 中文**仍留在 Core**（回落原文，与全局口径一致）
   ⇒ 通道 ② 的 **9 条一条没少**；另 **7 条**面板措辞未本地化 ⇒ **新开 #61n**。
5. `ReferenceRow` 的两个枚举仍是 `ToString()`（`ActionTarget` / `Label`）⇒ **新开 #61o**（本刀刻意不动）。
6. **`/tmp` 只有 10 MB 这件事没有判据兜着**（94.6b）：刻意**不**把 `TMPDIR` 指到别处 ——
   那会同时改掉多个既有判据的运行环境（`TestTempDir` / 路径卫生 / 数据根回退），属"一次放开多个变量"。

### 94.9 门禁

| 跑 | 读数 |
|---|---|
| gate1（**红**） | `.workbuddy/tmp/s94-gate1.log` mtime **19:24:36** ⇒ **唯一红** = `LangConstantUsageGuardTests.UnusedLangConstants_MatchBaseline`（**1112 ≠ 1113**）—— **正面证据**（那一行确实被脚本吞了，见 94.6a） |
| 修法 | `Lang.HwIssueLvglVersionMismatch` **写回原文**（`s94-fix1.py`；判据 `git diff Lang.cs` 相对 HEAD 只剩 `+` 行 = `32 0`）⇒ **基线不动** |
| gate1b（绿） | `.workbuddy/tmp/s94-gate1b.log` mtime **19:28:51** ⇒ `EXIT=0` / 7 步全过 / 0 警告 0 错误 / 路径卫生 **654** / 大队八项 **3193** |
| 内容批 | **`f7bfe52`**（13 files，+870 / −59，`19:31:03`）⇒ gate1b 比提交**早 2 分 12 秒** ✓ |
| gate2 第 1 跑（**红·环境**） | `s94-gate2.log` mtime **19:32:11** ⇒ `Device.Tests` 1 红（`No space left on device`，见 94.6b） |
| 修法（不碰代码） | 单跑 `T113SoBuilderTests` **13/13 绿**（`s94-device-retry.log`）⇒ 判环境；**同一条命令**重跑 |
| gate2 重跑（绿） | `s94-gate2b.log` mtime **19:33:52** ⇒ `EXIT=0` / 7 步全过 / 0 警告 0 错误 / 路径卫生 **656** / 大队八项 **3193** |
| 对拍 gate1b ↔ gate2 | 日志 `s91-diffgate.py` **差集 0 / 0**（29 / 29）；读数 `s94-diffreadings.py` 剥噪声后 **2 / 2**（全是已知噪声类） |
| 文档批 | **`50ffd54`**（4 files，+444 / −5，`19:35:45`）—— **只改 `docs/`** |
| gate3（绿） | `s94-gate3.log` mtime **19:38:22** ⇒ **晚于文档批 2 分 37 秒** ✓；7 步全过 / 路径卫生 **656** / 大队八项 **3193** |
| 对拍 gate2 ↔ gate3 | 日志 **差集 0 / 0**；读数 **1 / 1**（已知噪声类）⇒ 文档批不动任何代码路径 |
| 记忆批 | **`49a93b4`**（1 file，+77，`19:38:31`）：快照 **23h**（`sha256 d719635f23e7`）＋ `MEMORY.md` **码元 7984 / 8000** |
| 回填批 / gate4 | 回填批（只改 `docs/`）之后补跑 **gate4**；⚠ 记忆批的**快照在 `docs/` 下**、**不**进路径卫生清单 ⇒ gate3 → gate4 预期 **656 → 656**（内容批那 **+2** 见 94.9 的注）；按惯例 gate4 **只记** `.workbuddy/memory/2026-09-23.md`、**不回写本文件** |

⚠ **`654 → 656` 的成因**（第一版照阶段 92/93 的「内容批 +1」惯例写了 655，**错**）：口径是
`git ls-files 'src/*' 'tools/*' 'scripts/*' 'firmware/*'`（`scripts/check-path-hygiene.sh:37`）**不含 `tests/`** ⇒
内容批的 3 个新文件只有 2 个（`App/Services/ActionTextKeys.cs` / `App/Views/ReferenceRow.cs`）进清单。
教训与 §4 那条同源：**「惯例」不是证据**，读数要么来自日志、要么自己去核口径。

## 阶段 95（闭环 §4 **#61n**：交叉引用面板剩下的 7 条描述改走字典 —— 分派依据第一次**不是** `ReferenceType`）— 2026-09-23

### 95.1 一句话结论

面板「描述」列以前直显 Core 拼好的中文整句（`CrossReferenceAnalyzer` 的 9 处 `Description`）。
阶段 94 只接了动作那 2 条，剩下 **5 种措辞 / 7 个站点**英文界面下仍是中文。本阶段把**分派搬出 Core**：
Core 新增 `ReferenceDescriptionKind`（种类 ＋ 3 个实参字段、9 站全赋值），App 新增
`Services/ReferenceTextKeys.cs`（键推导唯一实现），`ReferenceRow.Description` 改成**只转发**；
Core 的 9 条中文**一条不动**（回落原文）。⇒ 中文界面**零可见变化**、英文下 7 条第一次跟着语言走。

### 95.2 量的那一遍纠正登记三处假设

| # | 登记写的 | 实情 |
|---|---|---|
| ① | 「7 条」 | 9 条里除动作 2 条（阶段 94 已接管）剩下的 —— 站点 **7** 个、**措辞只有 5 种** |
| ② | 「表达式引用」 | **零产出点**（`grep ExpressionVariable src/` 只命中枚举声明）⇒ 另开 **#61p** |
| ③ | 「按 `ReferenceType` 分派」 | **做不到**：同类型下有两种措辞 ⇒ 必须新增枚举（本刀唯一结构性决定） |

### 95.3 处置（内容批 `ebca0a9`，12 files）

| 层 | 改动 |
|---|---|
| Core `Services/CrossReferenceAnalyzer.cs` | `enum ReferenceDescriptionKind { Unset = 0, VariableBinding, LegacyVariableBinding, ActionDetail, ScreenReference, ScriptRun, ScriptBinding }`；`ReferenceLocation` ＋`DescriptionKind`/`BindingDirection`/`ScriptFunctionName`；9 站赋值；**9 条中文不动** |
| App `Services/ReferenceTextKeys.cs`（**新**） | `Describe(ReferenceLocation)`：7 支 `switch`、**无默认支**、文件级只禁 **CS8524**；`ActionDetail` 转交 `ActionTextKeys.DescribeReference` |
| App `Views/ReferenceRow.cs` | `Description` 只转发 ⇒ **分派只剩一处** |
| App `Views/CrossReferenceView.axaml` | 只改注释；模板五个绑定**逐字未动** |
| 字典 | `Lang` ＋5 常量 / `Strings.{zh-CN,en-US}` ＋5 键（**2254 → 2259**） |

⚠ 每支 `Format` 的**实参个数必须是字面量**（C1 读的就是它）；`Unset = 0` 承「忘填」⇒ 显式诚实边界。

### 95.4 台账归位（通道 ② 换消费方，条数不动）

通道 ② 仍 **9** 条，消费方 `CrossReferenceView.axaml` 的 `{Binding Description}` →
出口层 `App/Services/ReferenceTextKeys.cs` 的 `Lang.CrossRefDescScreenSwitch`；
通道数 / 条数和 **11 / 85 不动**（守卫常数也不动）。与阶段 89/93 对通道 ①/⑩ 做的同一件事。

### 95.5 判据 `ReferenceDescriptionLocalizationGuardTests`（J1–J9）

J1 每种有产出的种类随语言变（下界 6）；**J2 zh 逐字等于 Core 回落**（「零可见变化」的证明 ＋ 钉实参接上）；
J3 成对（en 无汉字 且 zh 有汉字）；J4 **端到端**（无 `Unset` 行 / 实参被填 / 无 `{0}`·`{1}`）；
J5 `ActionDetail` ⇔ `Action != null` 双向；J6 Core 9 条跨语言冻结；J7 `Unset` 走回落；
J8 5 键两本在且真翻；**J9 枚举成员名不许与 `Lang` 常量同名**（见 95.6a 后补）。
⚠ 夹具首跑 3 红 —— `ScriptRun` 档无样本 ⇒ **判据自己点名「在空转」**，补样本后转绿。
**生产负控 7 支（NC-A…NC-G）7/7 符合预期、全部能编译**（驱动 `.workbuddy/tmp/s95-negative.py`，
还原一律写回原文 ＋ `sha256` 逐字节核对）：NC-A 忘接方向 ⇒ 1 红；NC-B zh 漂移一字 ⇒ 1 红；
NC-C App 支写成回落 ⇒ 3 红；NC-D 忘填种类 ⇒ 2 红；NC-E en 没翻 ⇒ **4 红**（比首版预期多 2，
按实际红集改预期、不改判据）；NC-F 枚举成员撞 `Lang` 常量名 ⇒ 1 红（**伴随 1 条 CS8509 警告**、编译仍成功）；
NC-G 注掉集合标注 ⇒ 1 红。

### 95.6 🩸 两次被拦下（都是既有守卫抓到我）

**(a) 枚举成员名撞 `Lang` 常量名 ⇒ 零引用常量被**意外写活**。** gate1 唯一红
`UnusedLangConstants_MatchBaseline`（**1112 ≠ 1113**）：`ReferenceDescriptionKind.ScreenSwitch`
撞 `Lang.ScreenSwitch`（零使用点、键由 `ScreenManagerView.axaml` 的 `{res:Loc}` 担保、落 M2 桶）——
引用面是「**任意标识符**」⇒ 别处敲这个名字就算「有人读」。处置：**改名** `ScreenReference`
（精确串替换），**基线不动**，复测回 **1113 / M2 828**；新增 **J9** 防复发。
⚠「把基线改成 1112」是错解（会把事故洗成「清理」，同阶段 94 的教训）。

**(b) 切全局语言的测试类漏挂集合 ⇒ 打红别人的用例。** `EditorCommandCatalogTests` 两条红；
量化：**与大队同进程 5/5 红 / 单独跑 5/5 绿** ⇒ 我新增的判据切进程级全局语言、污染并发读者。
⚠ **我走的弯路**：先改了 `scripts/ci-gate.sh`（加排除 ＋ 单独跑一段），随后发现项目**已有**既定机制
`[Collection("Language-global")]`（17 类已挂）⇒ 已 `git checkout` 回退，改用集合，
并新增静态判据 `LocalizationRefreshUiTests.EveryTestClassThatSwitchesLanguage_IsInTheGlobalCollection`
（**单向**：挂而不切合法）；顺带补挂阶段 94 及更早游离的 3 个类。
⚠ `Assert.True(cond, $"…{list[0]}…")` 的**消息是立即求值的** ⇒ 绿时照样算 ⇒ 空集合越界报错。

### 95.7 读数与基线

| 读数 | 阶段 94 | 阶段 95 |
|---|---|---|
| 无代码引用的常量 | 1113 | **1113**（不动） |
| 分桶 | M2 828 / M6 262 / M3 18 / 无机制 5 | **同左，一字不动** |
| 字典键 | 2254 | **2259**（＋5） |
| 有常量的键 / 没有常量的键 | 1803 / 451 | **1808 / 451** |
| 可达机制 **M1-Lang 常量** 面 | 690 | **695**（＋5）；**M13 22 / M14 7 不动** |
| 直通面 | 15 / 14 | **15 / 14**（**零变化**） |
| 大队八项 | 3193 | **3203**（＋10 = 判据 9 ＋ 集合静态判据 1，**纯净增**） |
| 逐程序集 | `App.UiTests` 505 / `Core.Tests` 2119 | `App.UiTests` **515** / `Core.Tests` 2119 |
| 路径卫生 | 656 | **657**（＋1：口径 `git ls-files src/tools/scripts/firmware`，**不含 `tests/`** ⇒ 内容批只 1 个新文件进清单） |

⇒ 直通面**第五次零变化** ⇒ 「**面读数是证词、不是目标**」**第八例**（前七例见 §94.7 与 #61 母项）。

### 95.8 未闭环

1. 翻译质量不在判据面上；`Unset` 仍回落中文（J7 钉住；Core 的 `ReferenceLocation` 是公开类型）。
2. `BindingDirection` / `ReferenceType` / `ControlType` 仍是枚举名直显（`WriteOnly` / `Label`）
   ⇒ **#61o 一族**，本刀刻意不动。
3. `ReferenceType.ExpressionVariable` **零产出点** ⇒ **新开 #61p**，本刀不删该成员。
4. Core 那 5 种中文仍在（回落原文）；Core 侧没有独立判据，赋值完整性由 J4 端到端覆盖、
   漏接文案由 **CS8509** 编译期覆盖。

### 95.9 门禁

| 跑 | 读数 |
|---|---|
| gate1（**红 3**） | `s95-gate1.log` mtime **20:25** ⇒ 并发红 2（95.6b）＋ 基线红 1（95.6a） |
| 修法 | 枚举改名 `ScreenReference` ＋ 补挂 `[Collection("Language-global")]`（并回退对 `ci-gate.sh` 的改动） |
| gate2（**红 1**） | `s95-gate2.log` mtime **20:30** ⇒ `ReferenceRows_RenderInEnglish_EndToEnd`（疑似英文却含汉字的假红：`Where`/`Select` **两次读属性** × 两个切语言的类互相切）⇒ 印证「补挂集合」才对 |
| gate3（绿） | `s95-gate3.log` mtime **20:32** ⇒ `EXIT=0` / 7 步全过 / 0 警告 0 错误 / 路径卫生 **656** / 大队 **3203** |
| gate4（绿，提交前） | `s95-gate4.log` mtime **20:35:51** ⇒ 同上 |
| 内容批 | **`ebca0a9`**（12 files，`2026-09-23T20:37:52+08:00`）⇒ gate4 比提交**早 2 分 01 秒** ✓ |
| gate5（绿，提交后） | `s95-gate5.log` mtime **20:39:09** ⇒ **晚于提交 1 分 17 秒** ✓；7 步全过 / 0 警告 0 错误 / 路径卫生 **657** / 大队 **3203**（逐程序集 44 / 44 / 75 / 87 / 4 / 315 / 2119 / **515**） |
| gate6（绿·文档写入后、提交前） | `s95-gate6.log` mtime **20:44:37**；对拍 gate5 ↔ gate6 **差集 0 / 0** ⇒ 文档批不动任何代码路径；⚠ **但比文档批提交早 13 秒** ⇒ 不作文档批的证词，补跑 gate7 |
| 文档批 | **`5b30345`**（4 files，+346 / −5，`20:44:50`）—— 只改 `docs/`（专题 §95 ＋ 本文件阶段 95 ＋ 盘点 §13 ＋ 登记表 `#61n`/`#61p`） |
| gate7（绿·文档批后） | `s95-gate7.log` mtime **20:46:05** ⇒ **晚于文档批** ✓；7 步全过 / 0 警告 0 错误 / 路径卫生 **657** / 大队 **3203** |
| 对拍 gate6 ↔ gate7 | 各 **29 行**、**差集 0 / 0**（路径卫生那条步口径不含 `docs/` ⇒ 提交不改读数） |
| 记忆批 | **`6055173`**：快照 **23i**；`MEMORY.md` **码元 7976 / 8000**（§4.3 **析出** ⇒ 析出文档 §9，腾出空间加三条契约，净 −8 码元） |
| gate8（回填批后补跑） | ⚠ **约定写法，本批尚未跑**：回填批之后补跑 **gate8**；按惯例它只记当日日志、不回写本文件 |
| 对拍 gate4 ↔ gate5 | 日志 `s91-diffgate.py`：各 **29 行**、**差集只剩 1 行** = 路径卫生 `656 → 657`（成因＝内容批的 1 个新 `src/` 文件被 `git ls-files` 计入，见 95.7）；读数 `s92-diffreadings.py`：提交前后各 10 行、**差集 0 / 0** |

## 阶段 96（闭环 §4 **#61o**：面板剩下三列枚举名走字典 ＋ 控件类型显示名收敛到**一处**）— 2026-09-23

### 96.1 一句话结论

交叉引用面板一行里另外三列（`ReferenceType` / `HmiControlType` / `Description` 里的
`BindingDirection`）不再直显英文枚举名；顺带把「控件类型 → 显示名」的**两份手工表**收敛成
一处，并修掉其中已分叉的那一份（`DesignCanvas` 漏 `Screen` ⇒ 拖「屏幕」建出来的控件默认名是
英文 `Screen1`）。专题 ⇒ `docs/2026-09-23-exception-passthrough-triage.md` §96。

### 96.2 量的那一遍（登记只写了两个枚举，实为三个 ＋ 一处真分叉）

* 登记 #61o 只提 `ReferenceType` / `ControlType`；实测一行里可见的是**三个** ——
  `BindingDirection` 藏在 `Description` 模板的 `{1}` 实参位（`Format` 收 `object[]` 装箱后
  ToString）。三位合计 **33** 个枚举成员（4 / 26 / 3）。
* 「控件类型 → 显示名」**写了两遍**且第 ② 份**已分叉**：`ToolboxView.PopulateItems` 26 条
  全覆盖；`DesignCanvas.BuildControl` 的 `localizedName` **25 支 ＋ `_ => type.ToString()`**，
  **漏 `Screen`**。
* 取证：`Lang.Tb*` 引用总数 **77 ＝ 26（分类）＋ 26（工具箱）＋ 25（画布）** ⇒ 引用面就是这两处。
* 方向键两组都不可直接复用：`prop.bind.*` 缺 `WriteOnly`；`tag.monitor.*` 语义属「标签监视」。
* 前置排查：行数基线只守 `MainWindow.axaml.cs` ＋ 两个 partial glob ⇒ 三个目标文件都不在表里；
  33 ＋ 7 个候选标识符与 `Lang` 常量名**零撞车**。

### 96.3 处置（内容批 `301c11c`，10 files，＋693 / −76）

新建 `App/Services/ControlTypeTextKeys.cs`（26 支、**无默认支**、只禁 CS8524，键＝工具箱的
`tb.*`）；工具箱 26 行与画布的 25 支 switch 都改调它（画布 −27 行）；`ReferenceTextKeys` 加
`TypeName` / `DirectionName` 并让 `Describe` 的方向实参走后者（**实参个数仍是 2** ⇒ C1 不动）；
`ReferenceRow` 两个属性改走字典；7 个新常量 ＋ 两本字典各 7 键。

⚠ **方向刻意新建 `cross.ref.dir.*` 而不复用**：`prop.bind.*` 只有 2 条（缺 `WriteOnly`），
补齐或借用 `tag.monitor.*` 都会把常量从「无代码引用」集合里拽出来 ⇒ `UnusedBaseline` −2；
本刀选择**不动基线**。⚠ 类名不叫 `ControlTypeNames` —— 那个名字已被 AI 层的
`PromptTemplateEngine.ControlTypeNames` 占用（语义是「可生成的类型 Id 清单」）。

### 96.4 判据：8 条新（`ReferenceFieldLocalizationGuardTests` K1–K8）＋ 1 条**必须跟着改**的旧判据

K1 26 个成员各有其名且两两不同 / K2 名字来自字典 `tb.*` **值集**（不写映射 ⇒ 不依赖名单）/
K3 zh 全含汉字 / K4·K5 两个枚举逐档随语言变 / **K6 端到端三列** / **K7 单一出口的源码面接线**
（三个调用点都调它，且工具箱·画布不再出现 `Lang.Tb*` 控件名索引）/ K8 7 键两本都翻过。

⚠ **J2 必须改**：`ChineseRendering_IsByteIdenticalToTheCoreFallback` 原先逐行断言
`Describe(r) == r.Description`，而方向是**唯一刻意改掉的中文**（Core 回落嵌 `WriteOnly`）
⇒ 期望改成 `ExpectedZh(r)`（模板与方向都从**字典**取 ⇒ 与被测出口独立）。

### 96.5 负控 3 条（全在真仓库上，还原写回原文 ＋ `sha256` 一致）

| 负控 | 注入 | 编译 | 红集 |
|---|---|---|---|
| NC-1 | 删 `ControlTypeTextKeys` 的 `Tank` 支 | 能编译，`CS8509 ×2`（点名 Tank） | K1/K2/K3/K6 ＋ **基线两条** |
| NC-2 | `ReferenceRow.ControlType` 回退 `ToString()` | 能编译 | K6 ＋ K7 |
| NC-3 | `DirectionName` 回退 `ToString()` | 能编译 | J2 ＋ K5 |

⚠ NC-1 的基线两条（1113 → 1114）**在正确工作** ⇒ 按纪律**改预期、不改判据**。

### 96.6 🩸 被拦下（`CS0165`）

K1 里 `string[] names;` 在 lambda 内赋值 ⇒ 编译器不认「lambda 一定跑过」，须先
`Array.Empty<string>()`（与阶段 90 的「承接变量不能声明在 `try` 内」同族）。

### 96.7 读数与基线

| 读数 | 阶段 95 | 阶段 96 | 归因 |
|---|---|---|---|
| **M1-Lang 常量面** | 695 | **702** | ＋7 ＝ 新增常量数 |
| 无代码引用的常量（`UnusedBaseline`） | 1113 | **1113** | **不动** |
| M2 / M6 / M3 / 无任何机制 | 828 / 262 / 18 / 5 | **同** | 不动 |
| 字典键（zh）/ 有常量的键 | 2259 / 1808 | **2266 / 1815** | ＋7 / ＋7 |
| M6 面 / 直通面 | 373 / 15·14 | **同** | 不动 ⇒ **第九例「面读数是证词、不是目标」** |
| M13 / M14 | 22 / 7 | **22 / 7** | 不动 |

⚠ 口径差异：本刀读数跑了 `LangConstantUsageGuardTests`（阶段 95 那份没跑）⇒ 多出一个分区；
**两版共有的关键数完全一致**。

### 96.8 门禁（回填批）

| # | 跑 / 提交 | 日志 · 时间 | 读数 |
|---|---|---|---|
| ① | gate1（绿·内容批提交前） | `s96-gate1.log` mtime **21:15:17** | `EXIT=0` / 7 步全过 / 0 警告 0 错误 / 路径卫生 **657** / 大队 **523**（44 / 44 / 75 / 87 / 4 / 315 / 2119 / **523**） |
| ② | 内容批 **`301c11c`** | `21:16:31`，10 files，+693 / −76 | 代码 **21:11–21:13** 落盘 ⇒ ① 晚于落盘 ✓ |
| ③ | gate2（**红 1**） | `s96-gate2.log` mtime **21:18:58** | 第 5 步 `DispatcherOwnershipContractTests.CrossDispatch_…`（**已知 #47 族并发红**，本仓历史**第 4 次**：76 / 83 / 95 / 96 阶段）；`App.UiTests` 522 / 523 |
| ④ | #47 单跑 ×3 | `s96-47-run{1,2,3}.log` **21:19** | **3/3 绿**（311 / 311 / 307 ms） |
| ⑤ | 文档批 **`ebe505f`** | `21:17:51`，4 files，+251 / −1 | 只改 `docs/` ⇒ 其后的绿只有 ⑦ |
| ⑥ | 记忆批 **`c063c0f`** | `21:19:20`，2 files，+121 | 快照 **23j** ＋ 析出 §10 / §11；`MEMORY.md` **码元 7979 / 8000**（净 +3） |
| ⑦ | gate2b（绿·记忆批后） | `s96-gate2b.log` mtime **21:20:33** | 晚于记忆批 **1 分 13 秒** ✓；7 步全过 / 0 警告 0 错误 / 路径卫生 **658** / 大队 **523** |

日志对拍 `s91-diffgate.py`：`s95-gate8b` ↔ `s96-gate1` 各 29 行、**差集 3 / 3**（两个扫描面各 ＋1 个
`.cs` ＝ 新文件；`App.UiTests` 515 → **523** ＝ ＋8 新判据，纯净增）；`s96-gate1` ↔ `s96-gate2b`
**29 / 28** 行、**3 / 2** ⇒ 两处：路径卫生 657 → **658**（`git ls-files` 只列**已跟踪**文件，① 跑在
`git add` 之前）＋ 已知的**换行拼接**噪声（阶段 94 同族）。读数对拍 `s92-diffreadings.py`：
37 / 47 行、**1 / 11** ⇒ 10 行是**口径差**（本刀多跑 `LangConstantUsageGuardTests` 的分区），
共有键数 1113 / 828 / 262 / 18 / 5 一致。

⚠ 本节只写到 ⑦；按惯例**回填批之后补跑 gate3**，只记当日日志、不回写本文件。

### 96.9 未闭环

1. 翻译质量不在判据面上。
2. `ReferenceType.ExpressionVariable` 零产出（**#61p**）—— 本刀仍为它建了键。
3. **新开 #61q**：C1 的 `indexerSites >= 700` 下界与本刀收敛后的实测值贴边（本刀没动它，
   它仍成立）⇒ 下次结构性减少前**必须先量准真值**。
4. **新开 #61r**：`DesignCanvas` 的默认名随界面语言变 ⇒ 同一工程两种语言下编辑产生不同
   控件名（工程文件不可复现）。本刀只让它与工具箱一致，没解决跨语言不确定性。

## 阶段 97（#61d：**先量后定类**，只收第一档 —— 用户管理 / 模拟器的失败原因改走带码异常）— 2026-09-24

### 97.1 一句话结论

登记表把 #61d 那 **7 个键**统一记成「异常原文直通」并注明「**待读，不许定类**」。本刀逐站读了
产出方，实测它们**是三档不是一类**，只收**第一档**：4 个抛点换**带码异常**（用户管理 2 条语义
＋ 模拟器 1 条语义）、2 个视图的 **4 处**站点改走 `ExternalText.FailureReason(ex)`
（其中 1 处**原本连 `catch` 都没有**）。另外两档（返回值无原因 / 死 catch）只登记、不动。
专题 ⇒ `docs/2026-09-23-exception-passthrough-triage.md` §97。

### 97.2 量的那一遍（登记写的「7 个键同类」不成立）

| 档 | 键 | 产出方实测 | 本刀 |
|---|---|---|---|
| **①** | `sim.reg.read.failed` / `sim.reg.write.failed` / `user.mgr.create.failed` | 会抛（前两个抛**英文硬编码**、第三个抛**自建中文** `ArgumentException`） | **收** |
| **②** | `login.failed` / `recipe.clone.failed` | 只 `return null` / 返回 `bool`、**从不抛** ⇒ 站点拿不到原因 | 登记 **#61t** |
| **③** | `status.reload.failed` | `catch` 包的是 `Dispatcher.UIThread.Post` ⇒ **入队不抛**，死 catch | 登记 **#61u** |
| 排除 | `comm.test.failed` | 驱动校验**本来就返回 issues**，原因到得了界面 | 不动 |

⚠ 判档依据不是启发式，是「**产出方会不会给原因**」：会抛 ⇒ 给码 ＋ 换出口；
只 `return` ⇒ 要的是「给失败档建码」，换出口是**零收益的改名**；不会抛 ⇒ 连 `catch` 都是摆设。

### 97.3 处置（内容批 `be8dbde`，18 files，＋1053 / −39）

新码表 ＋ 记录体 `Core/Services/UserAdminIssue.cs`（`ROLE_NOT_REGISTERED` / `PASSWORD_TOO_SHORT`）；
新带码异常 `UserAdminException` —— **刻意不继承 `ArgumentException`**：继承它会让
`catch (ArgumentException)` 继续静默命中 ⇒「站点已收口」在**编译面上**看不出来；换类型的代价是
编译器**当场点出全部消费点**（实测 2 个 catch 站点 ＋ 5 处 `Assert.Throws<ArgumentException>`
＋ 1 处 `Assert.Throws<InvalidOperationException>`），这正是要的。模拟器侧同样新建
`SimulatorIssueCodes`（单码）＋ `SimulatorNotConnectedException`（`sealed`，回落是 `private const`，
否则 M15 反射会把它当成**第二条码**）。`ExternalText` 加 `UserIssueKey` / `SimulatorIssueKey`
两条出口 ＋ `FailureReason` 两支。两本字典各 ＋4（2266 → **2270**）、`Lang` ＋1
（`UserMgrPwdResetFailed`）。

⚠ **顺手修掉一处真缺陷**：`UserManagerView` 里重置密码那个 `SetUserPassword` 站点
**一个 `catch` 都没有** ⇒ 密码过短时异常直接冒泡出事件处理器。已与创建用户那一站**一起收口**
（「同一产出方的两条路径一起收，不许只收一半」）。
⚠ **两处字典值刻意不同**：`user.issue.password_too_short` / `sim.issue.not_connected` 的 zh 值
**逐字等于回落原文**（用户所见零变化是设计目标）；`user.issue.role_not_registered` 的 zh 值
**刻意不同**（回落那句是对开发者说的）⇒ 由判据显式钉住。

### 97.4 判据：8 条新（`UserAdminIssueLocalizationGuardTests`）＋ 2 条必须跟着改的旧判据

主判据 `EveryFailure_RendersTheDictionaryTemplate_InBothLanguages` 用**两种语言各渲染一遍、
逐字比对「该语言字典模板 ＋ 真实实参」**，**替代毒化探针** —— ① 回落是 `sealed` 类里的
`private const`，毒化构造不出来（**不为测试开放生产 API**）；② 更要紧：两档的 zh 字典值刻意等于
回落 ⇒ 中文那一遍的毒化探针**恒真**，**en 那一遍才是关键见证**（en 值不含汉字、回落必是中文）。
其余 7 条：成对性（含占位符数 == 实参数）/ 码表双向闭包 / 产出方不许退化 / 站点恰好 2 处 /
**没码照旧回落**（诚实边界正面断言）/ 两处刻意不同 / 5 组负控。
旧判据两条：`ExceptionMessagePassthroughFaceTests` 基线 **15 站 · 14 组合 → 12 · 11**；
`CommunicationSimulatorTests` 与 `SecurityConfigConsumptionTests` 的期望类型。
可达性新增 **M15**（面 **3**，取数**复用**本判据的 `DeclaredCodes()` / `DeclaredSimulatorCodes()`）。

### 97.5 负控 5 条（真仓库，还原写回原文 ＋ `sha256` 一致）

NC-1 裸异常 / NC-2 英文硬编码回来 / NC-3 站点退回 `ex.Message` / NC-4 摘 en 键 / NC-5 摘 M15 取数段
⇒ 红集分别 **5 / 5 / 3 / 4 / 2**，全部符合预期。

### 97.6 🩸 被拦下四次

① 主判据第一版照 `SvgParse` 族比「**原始**模板」（那族 0 实参才碰巧对）；② 毒化探针在本族
**恒真**（见 97.4）；③ **负控残留把门禁跑在了注入态上** —— NC-5 注入后漏了配对 `--restore`，
`git add -A` ＋ 门禁跑的都是注入版（症状：日志里 `M15… 实测 0 < 下界 3`）⇒ 规矩：注入型负控
**一注一还**，且 `git add` 之前核 `grep 注入标记`；④ 由 ③ 顺带量出 **M6 过覆盖**（自己写的断言
消息里的字符串字面量被 M6 当成「有人取过」）⇒ 新开 **#61s**。

⚠ ③ 那一跑（虽是注入态）暴露了**两条真红**，都是本刀改动的**真实后果**：
`CommunicationSimulatorTests.CommSim_Disconnect_ThrowsOnRead`（期望类型，改后 15 / 15 绿）＋
`CoreVisibleTextInventoryGuardTests`（`Services/UserAdminIssue.cs` 登记 0 实扫 2 ⇒ 加通道、
总数 **85 → 87**，改后 10 / 10 绿）。后者说明本刀往 Core 加的**是真新增中文**
（与阶段 93/94 的「搬迁 / 归类」不同）。

### 97.7 读数与基线

| 读数 | 阶段 96 | 阶段 97 | 归因 |
|---|---|---|---|
| **M15-用户/模拟器码 面** | — | **3** | 本阶段新增机制 |
| M1-Lang 常量面 | 702 | **703** | ＋1 ＝ `Lang.UserMgrPwdResetFailed` |
| 字典键 / 有常量 / 没常量 | 2266 / 1815 / 451 | **2270 / 1816 / 454** | ＋4 / ＋1 / ＋3 |
| 无代码引用的常量（`UnusedBaseline`） | 1113 | **1113** | **不动**（3 个码无 `Lang` 常量；新增的 1 个常量有引用） |
| M2 / M6 / M3 / 无任何机制 | 828 / 262 / 18 / 5 | **同** | 不动 |
| **直通面** | **15 站 / 14 组合** | **12 站 / 11 组合** | −3 站（**本族第六次真的把它改小**） |
| M6 面 | 373 | **373** | 不动 |
| 机制条数 | 14 | **15** | ＋M15 |

⚠ 两处**纯措辞**修正（数字一字未变）：M6 分区那句「残差键（**前六条**机制没覆盖）」自阶段 91
起就陈旧 ⇒ 改成「**前面各条**机制没覆盖」；可达性读数标题从**写死**的「十二条机制的面」改成
`{byKind.Count}` **计算得出**（连判据里 **8 处**硬编码的机制条数一起处理）。
读数对拍 `s92-diffreadings.py`：`s96-readings1.txt` ↔ `s97-readings3.txt` **47 / 59** 行、
左独有 **10** / 右独有 **22**，逐条可归因（10 行里 **2 行是纯措辞**、8 行是数字/条目变化；
22 行含本判据新增的 **3 个分区**）。

### 97.8 门禁

| # | 跑 / 提交 | 日志 · 时间 | 读数 |
|---|---|---|---|
| ① | gate1（**红 1**·且跑在**注入态**上） | `s97-gate1.log` mtime **11:32:03** | 卡在第 5 步 `DeviceChannelTests.SlowFirstByte_…`（**非本轮**）；**其余程序集全绿**（Simulator 44 / Core 2119 / App.UiTests 531） |
| ② | gate2（绿·内容批提交**前**） | `s97-gate2.log` mtime **11:37:04** | `EXIT=0` / 7 步全过 / 0 警告 0 错误 / 路径卫生 **662** / 三段合计 **3229** |
| ③ | **内容批 `be8dbde`** | `2026-09-24T11:37:24+08:00`，18 files，＋1053 / −39 | 代码 **11:2x–11:31** 落盘 ⇒ ①② 晚于落盘 ✓ |
| ④ | gate3（绿·**晚于提交**） | `s97-gate3.log` mtime **11:38:41** | **晚于提交 1 分 17 秒** ✓；`EXIT=0` / 7 步全过 / 0 警告 0 错误 / 路径卫生 **662** / 三段合计 **3229** |
| ⑤ | **文档批 `3081f9b`** | `2026-09-24T11:41:32+08:00`，4 files（**只改 `docs/`**），＋368 / −2 | 只改文档 ⇒ 它之后唯一的绿是 ⑦ |
| ⑥ | **记忆批 `2224151`** | `2026-09-24T11:43:22+08:00`，2 files，＋182 | 快照 **24k** ＋ 析出 **§12 / §13**；`MEMORY.md` 收在 **UTF-16 码元 7908 / 8000**（净 **−71**） |
| ⑦ | gate4（绿·记忆批后） | `s97-gate4.log` mtime **11:44:54** | **晚于记忆批 1 分 32 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **662** / 三段合计 **3229** |

日志对拍 `s91-diffgate.py`（`s96-gate2b` ↔ `s97-gate3`，各 28 行、**差集 3 / 3**，三条**全是纯净增**）：
`.Children` 扫描面 397 → **401**、路径卫生 658 → **662**（都 ＝ 新增 4 个源文件；
该步走 `git ls-files`，口径**不含 `tests/`**）、`App.UiTests` 523 → **531**（＋8 ＝ 新判据 fact 数）。

⚠ **① 那条红的归因**（四条证据）：该程序集**本轮零改动**（18 个文件里没有 `HmiEditor.Device`）／
失败形状是「期望 `ok:AAA` 实得空串」／**单跑 5 / 5 绿**且稳定 **955 ms**／
同一条命令重跑两次全绿且**同程序集耗时 38 s → 3 s**。⇒ 门禁第 5 步 8 个程序集**并行**，
全量负载把 `LineServer` 的 600 ms 回包延迟膨胀到**首字节窗口 5 000 ms 之上**，
**正确实现被判成「命令无输出」**。⚠ 这是本仓**第二次**看到这条假红：第一次 2026-09-16（阶段 8）
已把窗口从 1000 ms 拉到 5000 ms —— **这次 5000 ms 也被吃掉了**（膨胀系数 > 8.3）。
本刀**未动**该判据（属另一件事）。
**gate3 ↔ gate4 对拍**（`s91-diffgate.py`）：**各 28 行、差集 0 / 0** —— 记忆批只改 `docs/` 与笔记
（`.workbuddy/memory/` 不入库）⇒ 门禁输出逐行相同（也说明第 2 步**路径卫生口径不含 `docs/`**）。

⚠ 本节写到 ⑦。按惯例**回填批之后补跑 gate5**，只记当日日志、不回写本文件。

### 97.9 未闭环

1. **#61d 只收了第一档**：另两档已分出去 —— **#61t**（返回值无原因：`login.failed` /
   `recipe.clone.failed`，要给失败档建码）/ **#61u**（死 catch：`status.reload.failed`，
   `catch` 包 `Post` 入队不抛，真异常在回调里无人接 —— 这里既有「文案不本地化」也有
   「**异常被吞**」，后者更严重）。
2. **新开 #61s**：M6（源码字面量，代理机制）把**非注释的字符串字面量**也算成「有人取过」
   ⇒ 摘机制那类负控可能被它**遮蔽**。本刀只改掉自己的措辞，**没**动 M6 的语料口径。
3. **中文那一遍不具备见证力**（见 97.4）—— 见证靠 en 那一遍。
4. **翻译质量不在判据面上**。

## 阶段 98（闭环 §4 **#61t 的 ②** ＋ **#61u**：配方克隆失败档建码 ＋ 面板重建的失败处理搬进回调）— 2026-09-24

### 98.1 一句话结论

阶段 97 把 #61d 的 7 键**先量后定类**、只收第一档，另两档按实登记（§97.9）。本阶段把那两档**一起收掉**：

| 档 | 键 | 行 | 处置 |
|---|---|---|---|
| **② 返回值无原因** | `recipe.clone.failed` | ② | **返回值带码**：新 `RecipeIssueCodes`（2 码）＋ `RecipeCloneResult` ＋ `ExternalText.Describe`；站点补上**原本不存在**的失败档分支 |
| **③ 死 catch** | `status.reload.failed` | ③ | 把**重建连失败处理一起搬进回调**；出口 `Wiring.Panels.TryRebuildPanels` / `RebuildFailureText` |

⚠ **本刀不是"面变小"型收口**：直通面 **11 组合 / 12 站点 —— 条数一个没动**（`status.reload.failed`
那一行**换了文件**）⇒ **第十例「面读数是证词、不是目标」**。收益全在「失败档从**撒谎/静默**变成**带码**」。
专题 ⇒ `docs/2026-09-23-exception-passthrough-triage.md` §98。

### 98.2 量的那一遍：登记里的两处定性都被**改写**（都是更重）

| 登记（阶段 97 的推断） | 实读产出方/站点后（阶段 98） |
|---|---|
| ② 「产出方只 `return null` ⇒ 拿不到原因」 | `CloneRecipe` 签名是 **`public Recipe`（声明非空）** 而失败档 `return null!` ⇒ 消费点 `if (clone != null)` **连 `else` 都没写**（失败时界面**一声不响**）；**且它丢掉了 `AddRecipe` 的 `bool`**（「Id 已存在」的守卫，`Recipe.Id` = `Guid[..8]`）⇒ **撞车时界面提示「已克隆」（撒谎）** |
| ③ 「死 catch，真异常在回调里无人接」 | 确认，并补上后果：重建失败时状态栏**永久停在「正在重载面板...」**（不是"显示原文"，是**什么都没有**）。⚠ 与 #61b 里它的**证伪记录**一致 —— 那是 Dock **布局重组**失败，**不是 IO** |
| ① `login.failed` | 本阶段**没收**，但量出一条**缝**：站点 `IsNullOrEmpty` vs 服务端 `ArgumentException.ThrowIfNullOrWhiteSpace` ⇒ **纯空白输入走异常路**、把 BCL 英文原文原样显示（中英界面下都是英文）。已顺手对齐门槛 ⇒ 拆出 **#61v** |

### 98.3 处置（内容批 `549fb29`，15 files，＋1470 / −35）

* **Core**：新 `Services/RecipeIssue.cs` —— `RecipeIssueCodes`（`SOURCE_MISSING` / `REJECTED`，
  值**不带键前缀**）＋ `RecipeCloneIssue(Code, Message, Args)` 三段式 ＋
  `RecipeCloneResult`（`Recipe?` / `Issue?` **恰好一个非空**，两个工厂）。
  `CloneRecipe` 换签名 —— ⚠ **改返回类型＝取证手段**，编译器当场点出全部消费点（本刀 1 处）。
* **App**：出口 `ExternalText.RecipeIssueKeyPrefix = "recipe.issue."` ＋ `Describe(RecipeCloneIssue)`
  （**返回值档**，与 `Describe(SvgParseError)` 同形，**不走** `FailureReason`）；
  站点补 `else if (result.Issue is { } issue)`。
* **③**：`MainWindow.OnDockLayoutChanged` 只留「写 `正在重载面板` ＋ 入队」，
  新增 `PostPanelRebuild` 把**重建与失败处理都放进 lambda**；`MainWindow.axaml.cs` **行数中性
  （仍 3014**，该文件有精确行数基线）；出口落在 `Wiring.Panels.cs`（不在行数基线覆盖内）。
  做成**静态 ＋ 两个显式依赖**是为了判据：喂一个会抛的委托即可，**不必驱动 Dispatcher**
  （Avalonia headless 派发有既知死区）。
* **字典**：`recipe.issue.rejected` / `recipe.issue.source_missing` × 两本（2270 → **2272**）。
* ⚠ **两处刻意保留 `ex.Message`**（`recipe.clone.failed` 的兜底 catch / `status.reload.failed`）：
  产出方给不出码 ⇒ 换出口是**改名式假闭环**；这两项的收益分别是「**说清为什么**」与
  「**异常不再被吞**」，不是换措辞。

### 98.4 判据：12 条新（8 ＋ 4）＋ 3 条必须跟着改的旧判据

* `RecipeCloneIssueLocalizationGuardTests`（**8 条**）：主判据 = **毒化 ＋ 两语言各渲染一遍**
  **双见证**（毒化见「没走回落」，期望串比对见「实参真的被填」—— `Format` 在个数不符时
  **不抛异常**）；`RealFailures_InEnglish_CarryNoChinese`（收益正面见证）；成对性；**三态闭包**；
  产出方形状（含「`AddRecipe` 的 bool 没被丢」的**计数**判据 ＋ 两工厂各恰 1 次 = `NotProducible`
  的**担保方**，登记 #49）；站点接线；诚实边界正面断言；NC。
* `PanelRebuildFailureUiTests`（**4 条**）：helper **行为**（失败 ⇒ false ＋ 写出该语言的
  `status.reload.failed` 填真实原因；成功不写）＋ **接线形状**（切片里必须有必需接线、
  **不许裸调**、**不许 `try`**；锚点找不到即红）。
  ⚠ 行为与形状**缺一不可**：行为只证明 helper 对，**证明不了生产路径在用它**。
* 旧判据 3 条：直通面基线**换文件**（不是变少）；可达机制新增 **M16**（面 2）；
  Core 台账新增通道 `Services/RecipeIssue.cs`（2 条）⇒ 通道 **12 → 13** / 条数 **87 → 89**。
  ⚠ 顺带**补上阶段 97 欠的传播**：`VisibleChannels` 常数当年改成了 87，但盘点专题的 §0 / §3 / §5
  **都还停在 85 / 11** ⇒ 一起归位（专题 §16）。

### 98.5 负控 5 条（真仓库，还原**写回原文** ＋ `sha256` 逐字节核对）

p1 丢 bool / p2 绕开码化出口 / p3 摘 en 键 / p4 裸调重建 / p5 "顺手统一"成 `FailureReason(ex)`
⇒ 全部**点名了该点的那条**，**5 支 5/5**；还原后 `sha256` 一致（`ffc200f98647` / `b2304c823d9b` /
`9944ad840263` / `13edd50ea785` / `8825b8efbb16`）＋ 复跑绿。

### 98.6 🩸 被拦下两次

1. **p1 第一次跑出假绿**：过滤器对应的类在 `App.UiTests`，却跑在 `Core.Tests` 上 ⇒
   `dotnet test` 打印「**没有测试匹配**」并返回 **0** ⇒ 被记成"判据没接住"。
   修法是给负控脚本加 `no_match()` 判「**这个负控无效**」并拒绝给结论 —— 与既有教训同族：
   **「没有测试跑」与「测试通过」在退出码上分不出**。
2. **闭包判据自己把自己打红**：初版把工厂构造的 `REJECTED` 也算进 `produced` ⇒
   `NotProducible ∩ produced ≠ ∅`。修法 = 拆开两维（`Hit.ReachedByProducer`），
   并补上"工厂挂在产出方失败分支上"的担保判据。

### 98.7 读数与对拍（`HMI_READINGS_OUT` **绝对路径**：`.workbuddy/tmp/s98-readings.txt`）

```
直通面           站点 12 / 组合 11      （条数不变；status.reload.failed 那一行换文件）
UnusedBaseline   1113                  （不动）
字典键           2272（2270 → +2）；其中**没有** Lang 常量的键 454 → 456
M6 面            373（残差键 378 / 注释洞收益 1）
M1 面            703（不动）；可达机制 **15 → 16** 条，**M16 面 2**（新）
码表             声明 2 / 真实产出 1 / 明确产不出 1；渲染见证 2（其中工厂构造 1）
面板重建失败文案  zh ⇒ 重载失败: 面板重建炸了 ／ en ⇒ Reload failed: 面板重建炸了
```

⚠ 最后那一行故意把"**没本地化的那一半**"摆在读数里：原因那半是 `ex.Message` 原文
（读数里是我合成的中文异常；生产里通常是 BCL 英文）—— **产出方给不出码的直接后果，刻意**。

### 98.8 门禁（本节每个数字都能指到**一条日志 ＋ 一个时间点**）

| # | 批次 / 跑 | 时间 | 证据 |
|---|---|---|---|
| ① | **内容批 `549fb29`** | `2026-09-24T12:29:23+08:00`，15 files（12 改 ＋ 3 新），**＋1470 / −35** | —— |
| ② | gate1（绿·提交前预跑，同一棵树） | `s98-gate1.log` mtime **12:28:56** | `EXIT=0` / 7 步全过 / 0 警告 0 错误 |
| ③ | **gate2（绿·内容批后）** | `s98-gate2.log` mtime **12:30:53** | **晚于内容批 1 分 30 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **663** / `.Children` 面 **402** / 三段合计 **3243** |
| ④ | **文档批 `742e62a`** | `2026-09-24T12:35:37+08:00`，4 files（**只改 `docs/`**），＋346 / −6 | 只改文档 ⇒ 它之后唯一的绿是 ⑤ |
| ⑤ | **gate3（绿·文档批后）** | `s98-gate3.log` mtime **12:37:24** | **晚于文档批 1 分 47 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **663** / `.Children` 面 **402** / 三段合计 **3243** |
| ⑥ | **记忆批 `80d9c2f`** | `2026-09-24T12:41:17+08:00`（**amend 过一次**，见下），2 files，＋143 | 快照 **24l**（**7908 码元 = 原文**）＋ 析出 **§14**；`MEMORY.md` 收在 UTF-16 **7900 / 8000**（净 **−8**，余量 **100**） |
| ⑦ | **gate4（绿·记忆批后）** | `s98-gate4.log` mtime **12:42:44** | **晚于记忆批 1 分 27 秒** ✓；`EXIT=0` / **7 步全过** / **0 警告 0 错误** / 路径卫生 **663** / 三段合计 **3243** |

⚠ ① 与 ③ 的先后是**刻意的**：gate1 是提交前在同一棵树上的预跑，gate2 才是"**晚于它要证明的那个提交**"的
那一跑（本项目栽过一次「门禁读数比它要证明的提交早」的坑）。

**gate3 ↔ gate4 对拍**：顺序敏感差集 **42 行**全部是并行构建 / 测试的**输出顺序**，按**多重集**折算只剩 4 行同义文本（同一句被行缓冲拼成一行 vs 拆成两行）⇒ **内容无差异**；也说明第 2 步**路径卫生口径不含 `docs/`**（读数仍 **663**）。

⚠ **记忆批提交号变过一次**（`293e230` → `80d9c2f`）：核余量只剩 16 码元（低于技能自定的 ≥60）⇒ 再析出一条 ＋ 修掉一处 applier bug（**快照取到了裁剪后的中间态**，7643 ≠ 真原文 7908；已加断言 `units(快照) == before`）⇒ 该批**唯一有效的那一跑是 ⑦**，`293e230` 那一版无门禁证据。

⚠ 本节写到 ⑦。按惯例**回填批之后补跑 gate5**，只记当日日志、不回写本文件。

### 98.9 未闭环

1. **`REJECTED` 没有"走真实产出路径"的见证**（`Guid[..8]` 撞车是概率事件、不可确定性复现）
   ⇒ 进 `NotProducible` 写明理由；渲染见证由工厂提供；"工厂挂在失败分支上"另由形状判据钉住。
2. **`status.reload.failed` 的原因那一半不本地化、且不打算本地化**（产出方给不出码）；
   直通面基线上**照旧留着一行**，它就是这条结论的常驻证人。
3. **`login.failed`（#61t ①）仍开着** ⇒ 新开 **#61v**（要给 `LoginResult` 建码）。

---

## 阶段 99（闭环 **#61v**：登录失败档建码 `LoginResult` / `login.issue.*` ＋ `login.err` 退场）— 2026-09-24

### 99.1 一句话结论

`AuthenticationService.Login` 原先对**五种完全不同的真失败**（用户不存在 / 锁定 / 禁用 / 过期 /
密码错）一律 `return null` ⇒ 界面只能说一句通用的「用户名或密码错误」：**被锁定的不知道要等多久、
被禁用的不知道找谁**。本阶段改成**返回值带码**：`LoginResult`（会话 id 与失败原因**恰好一个非空**）
＋ `LoginIssueCodes`（5 码）＋ `LoginIssue` 三段式（码 ＋ 中文回落 ＋ 实参，**5 个工厂**）＋
出口 `ExternalText.Describe(LoginIssue)`；站点按码分派。`Lang.LoginErr` 与 `login.err` 键**一起退场**。

⚠ **「用户不存在」与「密码错误」刻意共用一个码**（分开＝用户名枚举预言机，且改动前本来就同一句）；
区分留在**审计**里。⚠ **直通面 12 站 / 11 组合零变化** ⇒ **第十一例「面读数是证词、不是目标」**
（那一行守的是 `catch` 的**意外异常**档，本刀收的是**真失败**档 —— 同一个站点、两件事）。

### 99.2 量的那一遍（读，不预设 —— 登记里没写的三件事）

① 量出**第五档**：「本次把账户顶到锁定」与「普通密码错」改动前返回同一个 `null`，而 `lockoutMin`
当时**只进日志** ⇒ 返回值要带它，得**提到 `if` 外面**；② 产品侧消费点**不止一个** ——
`BreakGlassLogin` 是**同类型内部的 unqualified 调用**，`grep "authService.Login"` **看不见**，
是**改返回类型之后编译器点出来的**（既定的取证手段）；③ 「用户不存在要不要给码」是**产品判断**，
答案写进 `LoginIssueCodes` 类注释。规模：产品侧 2 处 ＋ 测试 **57** 处（33 处补 `.SessionId`）。

### 99.3 处置（内容批 `04e6eef`，16 files，＋1278 / −60）

新 `Core/Services/LoginIssue.cs`（94 行）＋ `App/Services/ExternalText.cs`（前缀 / 键推导 / `Describe`）
＋ `LoginDialog` 按码分派 ＋ `AuthenticationService` 五档各得其所（锁定顺序不变，新增
`RemainingLockMinutes`：向上取整、下限 1，理由写在注释里）。字典 **−1 ＋5**（2272 → **2276**）。

### 99.4 判据与读数

新 `LoginIssueLocalizationGuardTests` **8 条**（毒化 ＋ 两语言双见证 / 英文无中文 / 成对性与占位符 /
三态闭包 / **产出方调用点计数**（`BadCredentials` 恰 2、其余各 1）/ 站点恰好 1 处码化出口且
**不许再用旧键** / 无码回落 / 内嵌 NC0–NC6）；改 4 条旧判据（`UnusedBaseline` **1113 → 1112**、
可达机制新增 **M17**（面 5）、台账通道 13 → **14** / 条数 89 → **94**、
`LocalizedFormatCallSiteTests` 加常驻读数）。

读数：机制 **17** 条 · M1 703（不动）· M17 **5** · M6 面 372（373 → −1）· 残差键 377（378 → −1）·
直通面 12 / 11（不动）· C1：`.Format` 306 / 索引器 **805**（下界 700，见 99.8）· 三段合计 **3251**
（3243 ＋ 8）。

### 99.5 负控与门禁

生产负控 **5 支 5/5**（跑在**提交前同一棵树**上；事后核对 5 个文件的「原文」快照与 `HEAD`
`sha256` **逐字节一致**）＋ 修批 **3 支 3/3**。

⚠ **两个工具坑**：① `s99-negative.py` 的 `failing()` 读**一路追加的同一日志** ⇒ 第 2–5 轮打印的
"红集"里**混着前几轮的红**（**假绿的形状**）⇒ 本阶段**按轮切分重算**了 5 轮各自的红集，结论不变，
修批的 3 支改**每支一个独立日志**；② 判据第一版用 `Assert.NotEqual(渲染, 回落)` 判"字典在起作用"，
而本族 zh 值与回落**本来就是同一句话** ⇒ **正确的实现被判红**（与阶段 97 §97.1 同源：
**中文那一遍零见证力**）；修法＝断言只对 en-US 成立。

| # | 批次 | 时间 | 证据 |
|---|---|---|---|
| ① | **内容批 `04e6eef`** | `18:30:11`，16 files，＋1278 / −60 | —— |
| ② | gate1（绿·预跑） | `s99-gate1.log` **18:29:52** | `EXIT=0` / 7 步 / 0 警告 0 错误 |
| ③ | **gate2（绿·内容批后）** | `s99-gate2.log` **18:31:40** | 晚于提交 **1 分 29 秒** ✓ / 路径卫生 **664**（663 → +1）/ `.Children` 面 **403** / 三段合计 **3251** |
| ④ | **修批 `a670b78`** | `18:41:07`，2 files，＋72 / −6 | 见 99.8 |
| ⑤ | **gate2b（绿·修批后）** | `s99-gate2b.log` **18:42:53** | 晚于修批 **1 分 46 秒** ✓ / 与 gate2 **多重集逐条完全一致**（496 / 496）|
| ⑥ | **文档批 `91950a7`** | `18:46:05`，4 files（只改 `docs/`），＋363 / −5 | 专题 §99 ＋ 本节 ＋ 登记表 ＋ 盘点 §17 |
| ⑦ | **记忆批 `3d0c646`** | `18:50:26`，2 files，＋135 | 快照 **24m**（7900 码元 ＝ 原文）＋ 析出 §15；`MEMORY.md` **7921 / 8000**（余量 **79**） |
| ⑧ | **gate3（绿·文档批后）** | `s99-gate3.log` **18:47:32** | 晚于文档批 **1 分 27 秒** ✓ / `EXIT=0` / 7 步 / 0 警告 0 错误 / 路径卫生 **664** / 三段合计 **3251** |
| ⑨ | **gate4（绿·记忆批后）** | `s99-gate4.log` **18:53:45** | 晚于记忆批 **3 分 19 秒** ✓ / 与 gate3 **多重集逐条完全一致**（496 / 496）|

⚠ **gate1 ⇄ gate2**：多重集差 2 / 3 —— 两条同义（行缓冲把同一句**拼成一行 vs 拆成两行**）＋
**一条真差异**（路径卫生 663 → 664 ＝ 内容批新入库 1 个 `src/` 文件；那一步走 `git ls-files`，
口径**不含 `tests/`** —— 本批新入库的 2 个文件里只有 `LoginIssue.cs` 被它数到）。

⚠ **gate3 ⇄ gate4**：剥墙钟后**各 739 行**、多重集 **496 / 496** **逐条完全一致** —— 记忆批只改
笔记与快照 ⇒ `.workbuddy/memory/` 按约定不入库、`docs/` 不在路径卫生口径里 ⇒ 输出**逐行相同**。

### 99.8 修批 `a670b78`：台账归位行的通道数**漂了 1**（顺手补上"谁看着这些数字"）

**发现**：归位盘点专题 §0 时把两组数与守卫自己的行首句对了一遍 —— 守卫与专题都写 13 条通道，
而 `VisibleChannels` 里**实实在在是 14 条**。按提交逐个数：阶段 94 = 11、**97 = 12**、**98 = 13**、
**99 = 14**；守卫那条「归位链」的通道数**从阶段 98 起就少 1**（阶段 98 把"加了通道"写成
「**通道数不变**、条数 +2」），而**没有任何判据看着这两个数字**（当时两处互相矛盾也没人红）。
⚠ **内容批的提交消息也抄了这个漂过的数**（写 12/89 → 13/94），真值是 **13 → 14**；提交消息不可改，
如实记在专题 §99.8。

**处置**：守卫文档注释归位（行首 **14 条通道（94 条文案）**、归位链末段 **14 / 94**、阶段 98 段改
**13 / 89**）＋ 盘点专题 §0 归位（**94 条 / 14 条通道**）＋ 判据 `VisibleChannels_CountIsWhatTheDocSays`
扩成「**三处**文档的两个数字都必须等于**算出来的**值」（**具名组**取数 —— 两处语序相反，
按位置取会自己报假异常，落地时真踩到一次）。**负控 3 支 3/3**，写回原文 `sha256` 逐字节一致。

### 99.9 未闭环

1. `login.failed`（#61t ①）现只剩**意外异常**档，且**刻意**留 `ex.Message`（产出方给不出码）
   ⇒ 直通面那一行是**换语义**不是变少，它就是这个结论的常驻证人。
2. `LOCKED_NOW` **没有"真跑过一遍"的见证**（判据喂的是工厂构造）—— 与阶段 98 的 `REJECTED` 不同，
   本刀**没有**把它写进 `NotProducible`：那一族的闭包问的是**产出方调用点计数**、不是"跑过没有"。
   ⇒ 口径差异**如实记**，不假装两者同级。
3. **#61s（M6 过覆盖）又添一个见证**：NC4 摘掉 M17 后本该点名 **5** 个键、实际只点名 **4** 个 ——
   `login.issue.bad_credentials` 被**判据自己的断言消息**写活了。
4. **#61q（下界贴边）第一次量准**：索引器站点实测 **805** / 下界 **700**（差 105，不贴边）；
   本刀**没抬下界**（抬它要重定口径），只把真值摆出来。
5. 家族仍开着 **#61 / #61a / #61f / #61k / #61p / #61r / #61s**；**#61v 闭环**、**#61t ① 随之闭环**、
   **新开 #61w（本阶段即刻闭环）**。

## 阶段 100（闭环 **#61f**：配方 CSV 导入失败档建码 `RecipeImportResult` / `recipe.import.issue.*`）— 2026-09-24

### 100.1 一句话结论

`RecipeManager.ImportCsv` 改动前返回一个 `int`，站点用 `if (count > 0)` 把它盖成
「成功导入 N 个配方」⇒ **7 处失败全被盖住**，其中 3 处**确定性丢数据**（坏行 / 非数字的值 /
表头空列名）、2 处**「成功」是撒谎**（`AddRecipe` / `UpdateRecipe` 的 `bool` 被丢 ⇒ 没进表
但 `count++`）。本阶段按 #61f / #61t② 已立住的模板收口：`ImportCsv` 签名 `int` →
**`RecipeImportResult`**（`Imported` ＋ `Issues`）＋ `RecipeImportIssueCodes`（**6 码**）＋
`RecipeImportIssue` 三段式（码 ＋ 中文回落 ＋ 实参 ＋ **行号**）＋ 出口
`ExternalText.Describe(RecipeImportIssue)`；站点新增「CSV 问题条」（状态行给**条数**、
问题条给**哪一行 + 为什么**、ToolTip 给**全部**）。

⚠ **直通面 12 站 / 11 组合零变化** ⇒ **第十二例「面读数是证词、不是目标」**
（那一行守的是同文件另一个方法 `CloneRecipe` 之后的 **UI 刷新**异常 —— 同一个文件、两件事）。

### 100.2 量的那一遍（登记里列的两个产出方：一个该划掉、一个是真内容）

`FontSubsetAnalyzer` = **纯函数、零失败面**（只有 `ExportCharsetFile` 的 IO 能抛，而那一站
已在 #61h 阶段 90 收口）⇒ **量完即无处安放，划掉**；`RecipeManager.ImportCsv` = **7 处静默**
⇒ 真内容。⚠ **S5（`AddRecipe` 的 `bool` 被丢）与阶段 98 的 `CloneRecipe` 完全同一形状**
（同一个文件里第二次）。

### 100.3 处置（内容批 `30efa18`，14 files，＋1529 / −42；12 改 ＋ 2 新）

新 `Core/Services/RecipeImportIssue.cs` ＋ `App/Services/ExternalText.cs`（前缀 / 键推导 /
`Describe`）＋ `RecipeEditorView` 的 `ShowCsvIssues` ＋ 字典两本各 **+7 键**
（`recipe.import.issue.*` 6 ＋ `recipe.csv.import.warnings` 1，**2276 → 2283**）。
⚠ 测试 5 处（`RecipeSerializerTests` 3 ＋ `Phase3IntegrationTests` 2）**是编译器点出来的**
（改返回类型＝取证手段）。

### 100.4 判据与读数

新 `RecipeImportIssueLocalizationGuardTests` **11 条** —— **复用 M16 判据的四个纯函数**
（`RenderRow` / `RenderViolations` / `PairingViolations` / `ClosureViolations`），
**不写第三份拷贝**；只写确实不同的几件（行号前缀期望串、丢掉 `bool` 的形状签名、
问题条的 `ScrollViewer` 位置、键前缀清单）。可达机制 **M17 → M18**（面 6）。

读数：直通面 **12 / 11**（不动）· 字典键 2276 → **2283** · 有常量的键 1815 → **1816** ·
无常量的键 461 → **467** · M1 面 703 → **704** · `.Format` 306 → **308**（新增状态行 ＋
行号前缀各 1）· 索引器 805（不动）· M6 面 372 / 残差键 377 / `UnusedBaseline` 1112 **全不动** ·
机制 **18** 条 · 三段合计 **3252**（3251 ＋ 11）。

### 100.5 负控与门禁

生产负控 **6 支 6/6**（`s100-nc-all2.log` **21:46:46**，跑在**提交前同一棵树**上，
`sha256` 逐例与 `HEAD` 逐字节一致，每支都有独立的「还原后复绿」一步）。

| # | 批次 | 时间 | 证据 |
|---|---|---|---|
| ① | **内容批 `30efa18`** | `21:48:08`，14 files，＋1529 / −42 | —— |
| ② | 全量预跑绿（同一棵树） | `s100-fulltest1.log` **21:36:00** | 8 程序集 **3262 通过 / 0 失败** |
| ③ | **gate1（绿·内容批后）** | `s100-gate1.log` **21:50:28** | 晚于提交 **3 秒启动** ✓ / `EXIT=0` / 7 步 / 0 警告 0 错误 / 路径卫生 **665**（664 → +1）/ `.Children` 面 **404**（403 → +1）/ 三段合计 **3252** |
| ④ | **文档批 `93f7bb7`** | `21:53:51`，4 files（**只改 `docs/`**），＋377 / −3 | 专题 §100 ＋ 本节 ＋ 登记表 ＋ 盘点 §18 |
| ⑤ | **gate3（红 1 —— 既有竞态被暴露，非本阶段引入）** | `s100-gate3.log` **21:55:15** | `GATE3_EXIT=1` / 7 步里第 5 步红 **1** 条：`ProjectFileOpenFailureUiTests.FailedOpen_LeavesNoWorkspaceDirectoryBehind`（`DataRootSandbox` 的集合标注漏挂，见专题 §100.12） |
| ⑥ | **修批 `44f0c71`** | `22:03:34`，5 files，＋124 / −0 | 补两处集合标注 ＋ 新增静态判据（见 100.7 第 6 条） |
| ⑦ | **gate2b（绿·修批后）** | `s100-gate2b.log` **22:04:57** | 晚于修批 **1 分 23 秒** ✓ / `EXIT=0` / 7 步 / 0 警告 0 错误 / 路径卫生 **665**（不动）/ `.Children` 面 **404**（不动）/ 三段合计 **3253**（3252 ＋ 静态判据 1） |
| ⑧ | **记忆批 `4cfa9b3`** | `22:06:12`，2 files，＋150 | 快照 **24n**（7922 码元 ＝ 原文）＋ 析出 **§16**；`MEMORY.md` **7931 / 8000**（余量 **69**） |
| ⑨ | **gate4（绿·记忆批后）** | `s100-gate4.log` **22:07:42** | 晚于记忆批 **1 分 30 秒** ✓ / `EXIT=0` / 7 步 / 0 警告 0 错误 / 与 gate2b **多重集仅差 3 行**（全是同一句的行缓冲拼接差）|

⚠ 两处**判据自己身上**的坑：① `MaskArgs` 第一版用 `«实参»` 当掩码 —— 它**自带 CJK**，
而尺子正是「英文界面不许出现 CJK」⇒ **凡是带实参的码全红**（换成 `<<ARG>>` 即绿）；
② 生产负控 E 第一轮**一条都没红** ⇒ 锚点 `Contains(prefix)` 把类注释的**散文旁白**也算成
清单条目 ⇒ 收紧成「**前缀 ＋ `&lt;`**」的清单条目形状后恰好点名那一条。
⚠ 另**如实记**两条"我的期望表写错了"（NC-A 摘调用点不动字面量 ⇒ 台账判据结构性看不见；
NC-D 主判据取 zh ∪ en 并集 ⇒ 原理上测不到"没翻"）。

### 100.6 新判据的「第一次跑」＝ 一次免费的**全库体检**

本阶段两个新判据各自点名了一处**比它们老 1–3 个阶段**的历史遗漏：
`CoreVisibleTextInventoryGuardTests` 点出 `Services/LoginIssue.cs`（**5 条**，阶段 99 —— §16
当年只把自己那两次归位到 13 / 89、**没算自己那一跳**）；
`EveryKeyPrefix_IsDocumentedInTheClassComment` 点出 `ExternalText` 类注释里 **M15（阶段 97）
的清单条目从来就没写**。两处都在本刀的内容批里补齐。

### 100.7 未闭环

1. **`RECIPE_NOT_APPLIED` 只有工厂构造的见证**（真产出要 `Guid[..8]` 撞车）⇒ 口径差异如实记
   （与阶段 98 的 `REJECTED` 同级）。
2. **`ImportCsv` 不读表头名**（前三列是**位置**约定）；`ExportCsv` 列序一致 ⇒ 不算缺陷，
   "按表头名取列"属新功能。
3. **`ValidateRecipe` 零生产消费点** —— 本刀**不动**（那是"死代码"议题，不是本议题）。
4. **提交消息有一处写错**（不可改）：写成「`Lang` +1（`csv.line.prefix`）」—— 真值是
   **`Lang.RecipeCsvImportWarnings`**；`Lang.CsvLinePrefix` 阶段 88 就有。数字**以读数文件为准**。
5. **#61f 闭环（闭环的是产出方那一半）**；**#61a 仍开着** —— 它清单里还有 **4 个键**留在
   直通面上、本阶段**逐条未动、也没有替它们的形状下结论**（下一阶段先量的那一遍再定）。
   家族仍开着 **#61 / #61a / #61k / #61p / #61r / #61s**。专题 ⇒ §100。
6. **gate3 那次红不是本阶段引入的**（专题 §100.12）：`DataRootSandbox` 的构造/析构改进程级
   `AppPaths.DataRoot`，而 `ProjectFileOpenFailureUiTests` / `ProjectFileOpenSaveRoundTripUiTests`
   **从阶段 79/80 起就漏挂** `[Collection("Language-global")]` ⇒ 与**所有**集合并行 ⇒ 撞上任何沙箱类。
   处置 = 补齐两处标注 ＋ **新增静态判据**（同族、复用 `HasCollectionAttribute` 纯函数，
   下界 **8** / 实测 **11**）＋ 负控 **2 支 2/2**（A 摘标注 ⇒ 恰好 1 条红，且**既有的那条没红**
   ⇒ 两条判据互相独立）。
   ⚠ 复现率：门禁命令 **1 次红 ＋ 随后 3 跑 0 复现**、单独项目 **3 跑 0 复现** ⇒ **低频**；
   修法的见证是**根因（读代码）＋ 静态判据 ＋ 负控**，**不是"重跑绿"**。
   ⚠ 反方向的后果更坏：用例**读到别人的数据根却照样通过** ⇒ **假绿**。

## 阶段 101（#61a 尾巴 4 站收口：2 站真迁移 / 2 站「诚实边界」；并**实测出一条判据盲区**，同阶段补第二条判据）— 2026-09-24

### 101.1 一句话结论

阶段 100 明确留下的尾巴（4 个键）本阶段收口。**量的那一遍把一个旧登记直接推翻**
（`asset.import.fail` 的「一次导入多个文件 ⇒ 那个文件结构上不存在」）：那个 `path` 变量
**就在 `foreach` 里**，且同文件 SVG / 字体两档**阶段 86 早就迁了** ⇒ **真迁移**。
四个键分属两类：**2 站真迁移**（`status.svg.apply.failed` / `asset.import.fail`，走
`UiOperationGuard.FailureDetail(操作名, 目标路径, 异常)`）＋ **2 站登记「诚实边界」**
（`status.verify.failed` / `audit.verify.code.failed` —— 那个 `try` 体内**没有产出方**）。

**直通面 12 站 / 11 组合 → 10 站 / 9 组合。**

⚠ 并在迁移过程中**实测出一条判据盲区**：`FailureDetail` 的第二个实参是**承接变量**
（`string? X = null;` 提到 `try` 外、循环里再赋值）时，「有没有真的赋上」**面完全看不见**
—— 真仓库注入（`currentPath = path;` → `= null;`）＋**排除**新判据的跑集 ⇒ **0 红**。
既有那条 `EveryDetailCallSite_PassesAConcreteTargetPath` **只认字面量** `null` / `default`。

### 101.2 量的那一遍（读现场代码，不预设）

| 站点 | `try` 体内真 IO？ | 产出方有码？ | 判定 |
|---|---|---|---|
| `status.verify.failed` ＋ `audit.verify.code.failed`（**同一个** `catch`） | **无** | **否** | **诚实边界**（只加注释） |
| `status.svg.apply.failed` | **有**（第一行 `ReadAllTextAsync`） | **是**（BCL，M10 七码） | **真迁移** |
| `asset.import.fail`（图片**批量档**） | **有**（`new FileInfo(path).Length`） | **是** | ⚠ 旧登记**被实测推翻** ⇒ **真迁移** |

证据：`grep -rn -i "throw" src/HmiEditor.CodeGen/*.cs` ⇒ 只有 **2 处**（内部入参
`ArgumentNullException.ThrowIfNull` ＋「未登记视觉属性」的 `NotSupportedException` =
**程序员错误档**）；CodeGen 目录 `File.` / `Directory.` / `FileInfo` / `Process.` 命中 **0**；
`GeneratedCodeValidator` **零 `throw`、零 IO**。

### 101.3 处置（内容批 `d150048`，10 files，＋65 / −19）

两站迁三实参出口；`Lang` ＋2 操作名常量（`UiOpApplySvg` / `UiOpImportImage`，**都有代码引用**）；
字典两本各删 2 加 2（**净 0**：删 `asset.import.fail` / `status.svg.apply.failed`，加
`ui.op.apply.svg` / `ui.op.import.image`）⇒ 键数 **2283 不变**；两处会被改「撒谎」的既有注释更正
（`ExternalText.FailureReason` / `UiOperationGuard.FailureDetail` 的 doc）。

⚠ **行数守卫 gate1 第一次红**：给 `MainWindow.axaml.cs` 加 8 行注释 ⇒ 中间态
**3014 + 8 = 3022** ⇒ `SingleFileSizeGuardTests` 点名。处置 = **行数中性置换**
（站点注释 8 → 4 行 ＋ 删掉 `RunDockEventRoutingDiagnostic` 上**重复且过期**的第 2 个
`<summary>` 4 行）⇒ **3022 − 8 = 3014**，**基线一字未动**。

### 101.4 判据批（`43cb79d`，2 files，＋184 / −2）

新纯函数 `ReviewConveyedPathVariables` ＋ 生产 Fact `EveryConveyedPathVariable_IsGivenARealValue`
（扫描面自检 `Sites >= 3`，**实测 4 处**：`BuildPipelineService.targetDir` /
`AssetManagerView.currentPath` / `CommunicationConfigView.targetPath` ×2）＋ 负控 Fact
（**6 组合成语料**，含「赋值在**调用点之后**」与「同文件两个同名局部」两档）。规则是**位置式**
（不做作用域解析：`CommunicationConfigView` 就有两处 `targetPath`）。**复用**既有
`DetailCallSite` 与 `SplitTopLevelArguments`，**不写第二份扫描**。

🩸 **负控 ② 当场抓出判据自己的一个正则洞**：`\s*=(?!=)\s*(?!null\b|default\b)` 里
`=` 后的 `\s*` **可以匹配空串** ⇒ 前瞻看到的是那个**空格**、把 `p = null;` 判成「赋了真值」
（症状：② 红、① 反而绿）。修法 = 前瞻**紧贴** `=`：`\s*=(?!=)(?!\s*(?:null|default)\b)`。
⚠ 这条洞**只有负控 ② 抓得到** —— 生产面 4 处赋的都是真值。

### 101.5 生产负控（**每支独立日志**）

| NC | 注入 | 跑集 | 该轮**实测红集** |
|---|---|---|---|
| **A** | `currentPath = path;` → `= null;` | **排除**新判据 | **0 红** ⇒ **盲区实证** |
| **D** | 同 A 的注入 | **含**新判据 | **恰好 1 条**：新判据（汇总 `失败: 1，通过: 566，总计: 567`） |

**四次门禁都晚于**它们要证明的那次提交（`s101-gate1.log` **23:43:39** > `d150048` **23:42:31**；
`s101-judge-gate.log` **00:04:15** > `43cb79d` **00:02:48**；`s101-gate3.log` **00:08:53**
> `6f2a87d` **00:07:49**；`s101-gate4.log` **00:12:22** > `4d895ff` **00:11:15**），
**7 步全绿**（`Core.Tests` 2121 / `App.UiTests` 565 ＋ 2；⚠ **gate5 第一次是红** —— **环境型**：`/tmp` 10 MB tmpfs 写满 ⇒ 复跑 `s101-gate5b.log` 绿，归因四件套见专题 §101.10.1）。

🩸 **顺手修掉「负控跑器」自己的两个缺陷**（都在**证据怎么存 / 怎么读**上，不在被测对象上）：
① 注入态与还原态**共用日志路径** ⇒ 绿**覆盖**红；② 收红集按「`[FAIL]` 在**前**」写
（xunit 实为**用例名在前**）⇒ **一条都收不到**，于是打出「`EXIT=1`」＋「红 **0** 条」这种
**自相矛盾**的读数。② 的修法是**加互证**（逐条红集合 ↔ 汇总行失败计数，不等即抛）——
**「收不到」不许被当成「没有红」**。

🩸 **本阶段还踩到同一族的又一处**（可查到的前例 = **阶段 83 的撞名覆盖**，见本文档 §83.7）：
内容批那次 gate1 的**第一次红**日志被后来的绿跑
**覆盖**（同一个 `s101-gate1.log`）⇒ 那个读数（3022）**不可复算** ⇒ 文档**不引用**它的时间点，
改补一支**每支独立日志**的可复算负控：`s101-line-nc-inject.log` **00:05:15**（末尾追加 1 空行
⇒ `行数 3015 ≠ 基线 3014` 红 / `失败: 1，通过: 3，总计: 4`）、`…-restore.log` **00:05:16** 绿
（`sha256` 逐字节一致 = **True**）。守卫是**运行时读文件** ⇒ 这一支 `--no-build` 即可，无需重建。

### 101.6 读数（**改前 23:37 / 改后 23:44**，两段 `E1=0 E2=0`）

| 量 | 改前 | 改后 |
|---|---|---|
| 直通面 | 12 站 / 11 组合 | **10 站 / 9 组合** |
| `.Format` 站点 / 索引器 / 合计 | 308 / 805 / 1113 | **306** / 805 / **1111** |
| `UnusedBaseline` | 1112 | **1110** |
| 分桶 M2 / M3 / **M6** | 828 / 18 / **261** | 828 / 18 / **259** |
| M1 面 / M6 面 / 残差键 | 704 / 372 / 377 | **706** / **370** / **375** |
| 字典键（zh-CN） / 有常量 / 无常量 | 2283 / 1816 / 467 | **2283** / 1816 / 467（**净 0**） |
| 可达机制 | 18 条 | **18 条**（本阶段**不新增机制**） |
| `App.UiTests`（**门禁口径**） | 563 ＋ 2 | **565** ＋ 2（该类 8 → 10） |

⚠ 「字典净 0 而 `UnusedBaseline` −2」「`UnusedBaseline` −2 而 M1 面 ＋2」都**不矛盾**：
两者数的集合不同（**无**代码引用的常量 ↔ **有**代码引用的常量）。

### 101.7 诚实边界与未闭环

1. 两个 `status.verify.*` 站点**有意留** `ex.Message` ⇒ 直通面**不可能**降到 0。
   `SystemFailureCodes.CodeFor` 对它们的异常类型返回 `null` ⇒ 换出口只落**回落分支**、
   用户所见**一字不变**。**要收就先有产出方。**
2. 新判据**看不见**的档（已在其 doc 里自陈）：**字段 / 参数形态**的目标路径；以及
   「承接变量被赋成**另一个也可能为 null 的变量**」（本判据只看右侧不是字面量
   `null` / `default`）。
3. **顺手发现、未定性**：`audit.verify.code.failed` 的渲染结果经
   `AuthenticationService.RecordAudit` → `AppendAudit` 进审计日志，而审计日志**可落盘**
   （`project.settings.audit.path`，空 = 内存环形缓冲）⇒ 落盘的那条明细是**本地化文案**。
   「审计记录按**当时读者的语言**存」是否算缺陷 = **产品判断**，本阶段**只登记、不动手**。
4. **家族**：`#61（本体，还剩 6 站）` / `#61k` / `#61p` / `#61r` / `#61s`。
   ⚠ 口径与直通面 **10 站**对齐：本体 6 站（`compile.failed`×2 /
   `download.sharedlibrary.failed` / `buildrun.log.error` / `comm.test.failed` /
   `login.failed`）＋ **4 站刻意保留的诚实边界**（`status.verify.failed` /
   `audit.verify.code.failed` / `recipe.clone.failed` / `status.reload.failed`）。
5. 专题 ⇒ `docs/2026-09-23-exception-passthrough-triage.md` §101。

---

## 阶段 102（#61 本体 6 站的三档判定：3 站真迁移 / 2 站「诚实边界」/ 1 站**被重新定档**；并新开一条面 #61x）— 2026-09-25

**范围**（用户显式选定）：`compile.failed`×2 / `download.sharedlibrary.failed` 迁到三实参出口；
`comm.test.failed` / `login.failed` / `buildrun.log.error` **只登记不硬迁**；
顺带给「拿到 issues 列表却直接渲染回落原文」立一条**新面**。专题 ⇒
`docs/2026-09-23-exception-passthrough-triage.md` §102。

### 102.1 量的那一遍（读现场代码，不预设）

口径 = `ExceptionMessagePassthroughFaceTests.Baseline`（阶段 101 后 9 组合 / 10 站点）。
**六站不是一类、是三档**：

| 站 | 键 | `try` 内真 IO | 产出方给原因吗 | 判定 |
|---|---|---|---|---|
| `BuildPipelineService.CompileAsync:206/207` | `compile.failed` ×2 | 有 | 有（BCL 7 码） | **真迁移** |
| `BuildPipelineService.EnsureSharedLibraryArtifactsAsync` | `download.sharedlibrary.failed` | 有 | 有（同上） | **真迁移** |
| `CommunicationConfigView:1142` | `comm.test.failed` | ❌（`ValidateConnection` **返回** `ValidationResult`，零 `throw`） | 否 | 诚实边界 |
| `LoginDialog:84` | `login.failed` | ❌（`AuthenticationService` 零 `File.` / `Directory.`） | 否 | 诚实边界 |
| `BuildRunDialog:101` | `buildrun.log.error` | **有** | **有**（**真抛**中文 `InvalidOperationException`） | ⚠ **A 类异常档 · 可码化**（本阶段不动） |

🩸 最后一行**推翻了开工前的判断**（开工时它与上面两条同档）：`SimulatorBuildService.cs:102` /
`:105` 两条前置校验**真抛**（「LVGL 源码无效: …」/「未找到 cmake…」），而**编译失败走
`SimulatorBuildResult` 返回值**（编译器输出进 `Log` / `errors`）⇒ 那个 `catch` 接的是
**本仓库产出方**的异常 ⇒ **A 类**，要的是「给产出方建码」。

### 102.2 三站真迁移

- **`compile.failed` × 2**：同一个 `catch` 里**审计那一半阶段 86 就迁完了**（`:208` 已是
  `FailureDetail(Lang.UiOpExportCode, targetDir, ex)`），漏的只有**两行状态栏**；承接变量
  `targetDir` 阶段 86 已提到 `try` 外 ⇒ **零新增**。状态栏用 `UiOpCompileProject`
  （与工具栏命令、`MainWindow` 同一操作的 `catch` 同名），**审计沿用** `UiOpExportCode`
  （与成功行 `audit.export.c.code` 同口径）—— 两个通道各按自己那半的措辞。
- **`download.sharedlibrary.failed`**：`try` 内 `CreateDirectory` ＋ 2× `WriteAllText` ⇒ 真 IO；
  新提**承接变量** `string? dir = null;`（写在 `try` **外** —— 写里面 `catch` 取不到会 `CS0103`；
  **不许写 `null` 糊过去**，那会踩红 `EveryDetailCallSite_PassesAConcreteTargetPath`），
  出口对 `null` 路径自动回落 2 实参版；操作名**新拆** `UiOpPrepareSharedLib`
  （「下载**前**准备 .so 产物」与「下载动作本体」是两个操作）。
- ⚠ 两族**不是改名式假闭环**：BCL 类型有码（阶段 85）⇒ 原因那半由 **0 实参短句**渲染 ⇒
  用户**同时**读到「哪个文件」＋「为什么（他的语言）」（与阶段 86 / 90 / 101 同一判定）。

### 102.3 字典处置链（预算得准，实测逐项吻合）

删 2 键（`compile.failed` / `download.sharedlibrary.failed`，使用点写的是**字面量**）＋
**连删 2 个只靠 M6 桶担保的死常量**（`CompileFailed` / `DownloadSharedLibFail`）＋
加 1 键（`ui.op.prepare.shared.lib`）＋ 1 个**有代码引用**的操作名常量
（⇒ **不进** `UnusedBaseline`）。`sync_localization.py` 的 `VALUE_MAP` 与 `KeptUnreachable`
两处**两键都不在**，无动作。

### 102.4 新面 #61x（`IssueMessagePassthroughFaceTests`，3 条 Fact）

**为什么必须新开**：直通面的谓词里**有 `Exception`** ⇒ 「**记录体**（有 `Code`）的 `Message`
被当成文案直显」这一类**原理上看不见**。缺口 = `CommunicationConfigView.OnTestConnection` 的
`string.Join(", ", result.Issues.Select(i => i.Message))`；同族其余**八处**都走
`ExternalText.Resolve(<族>IssueKey(code), issue.Message, issue.Args)`。
⚠ **通讯族不同形**（`ValidationIssue` **没有 `Args`**、码是**大写诊断码**而不是直接对应的字典键）
⇒ **收口要动记录形状**，是独立的一刀 ⇒ 本阶段**只定量、只交付判据**。

**面的口径三取舍**：① `src/` 全部产品源、**先剥注释**；② 同一**语句**内既有 `.Issues`
又有 `.Message`（按 `;` 切 —— 链式调用跨行），**排除产出侧** `.Issues.Add(` / `.Issues.Clear(`
（前瞻用 `[ 	]`、**不跨行**）；③ 基线**双向**。
🩸 **自陈盲区**：中转变量 / `foreach` 拆开写 ⇒ 看不见（位置式规则的通病），
**NC5 把这一档显式钉成「看不见」**。

### 102.5 生产负控（真仓库注入，**每支独立日志**）

注入**能编译**的坏形状（`ProjectSettingsView` 的 `LocalizeIssue(i)` → `i.Message`）：
注入态构建 **0 警告 0 错误**；新面**恰好 1 条红**（`失败: 1，通过: 2，总计: 3`）并点名
「**新增** …/ProjectSettingsView.axaml.cs」、而**直通面 3/3 仍绿**（**盲区实证**）；
还原**写回原文内容**（不是 `copy2`）、工作树干净（`sha256 a64278a8…`）、双面复绿。

### 102.6 读数（`HMI_READINGS_OUT` **绝对路径**，两段 `E1=0 E2=0`）

| 量 | 改前 | 改后 |
|---|---|---|
| 直通面 | 10 站 / 9 组合 | **7 站 / 7 组合** |
| `.Format` 站点 / 索引器 / 合计 | 306 / 805 / 1111 | **303** / 805 / **1108** |
| `UnusedBaseline` | 1110 | **1108** |
| 分桶 M2 / M3 / **M6** | 828 / 18 / **259** | 828 / 18 / **257** |
| M1 面 / M6 面 / 残差键 | 706 / 370 / 375 | **707** / **368** / **373** |
| 字典键（zh-CN） / 有常量 / 无常量 | 2283 / 1816 / 467 | **2282** / **1815** / 467（−2 ＋1） |
| 可达机制 | 18 条 | **18 条**（本阶段**不新增机制**） |
| **新面 #61x** | —（**不存在**） | **1 站 / 1 文件** |
| `App.UiTests`（**门禁口径**） | 565 ＋ 2 | **568** ＋ 2（+3 = 新类 3 条 Fact） |

⚠ **「读数晚于什么」写准**：两段都是**工作树读数** ⇒ 只要求晚于**改动落盘**
（内容批落盘 16:23:01–16:23:09）：改前 16:22:03 **早于**落盘 ✔、改后 16:25:37 **晚于**落盘 ✔。
🩸 但改后读数**早于内容批提交（16:26:09）32 秒** ⇒ **不能**照抄阶段 101 那句「晚于提交」的写法，
**照实记成「晚于改动落盘 / 早于提交」**。

### 102.7 门禁（每一次绿都晚于它要证明的那次提交；**已回填到 `gate4`**）

| 时刻（birth / mtime） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 16:26:16 / 16:27:39 | `s102-gate1.log` | 内容批 `d2f23a9`（16:26:09，6 files，＋24 / −17） | **7 步全绿**；`Core.Tests` **2121** / `App.UiTests` **565** ＋ 2 |
| 16:30:03 / 16:31:05 | `s102-gate2.log` | 判据批 `c7ae688`（16:30:03，2 files，＋250） | **7 步全绿**；2121 / **568** ＋ 2 |
| 16:31:16 / 16:32:21 | `s102-gate2b.log` | 同上，**另开日志复跑** | **7 步全绿**；2121 / 568 ＋ 2 |
| 16:39:50 / 16:41:08 | `s102-gate3.log` | 文档批 `a7d5693`（16:39:46，4 files，＋405 / −4，⚠ **含一行源码注释**） | **7 步全绿**、0 警告 0 错误；2121 / 568 ＋ 2 |
| 16:43:43 / 16:44:46 | `s102-gate4.log` | 记忆批 `bf87c2a`（16:43:40，2 files，＋139） | **7 步全绿**、0 警告 0 错误；2121 / 568 ＋ 2 |

🩸 `gate2` 的 birth 与判据批提交**落在同一秒**（16:30:03.271 vs 16:30:03）⇒ 纪律上「不够干净」
⇒ **处置 = 另开一份日志、同一条命令复跑**（`s102-gate2b.log`，晚于提交 **1 分 13 秒**，
同样全绿）。⚠ 记下来的是**两条日志、两个时刻**，不是把 `gate2` 那条说成"有效"。

🩸 **另有一次「红」值得记**：改后读数**第一次跑 `E1=1`**，红的是
`Face_IsNotDegenerate`（面 10 → 7 站，撞穿自检下界 `>= 10`）⇒
⚠ **面掉下来时先红的是自检、不是主判据**；处置 = 下界**往下放一档**（10 → **5**）＋ 同步理由文字，
**不删**自检，且下界**刻意不贴实测值**（7 的约 2/3）—— 贴了会让以后每一次**正确**的迁移多红一条。

> **自指边界（如实标注）**：**回填批**（把 `gate3` / `gate4` 两行写上去的那一批）自己也要过门禁
> ⇒ 它的读数（`s102-gate5.log`）记在**当日日志与收口清单**、**不回写本节**（同阶段 101）。
> ⚠ 文档批顺带改了**一行源码注释** ⇒ 那一批**不是纯 `.md`**；实测注释**不进 IL** ⇒
> `gate3` / `gate4` 的读数与判据批**逐项相同**（2121 / 568 ＋ 2）。

### 102.8 诚实边界与未闭环（＋ 一处既有定性被更正）

1. `comm.test.failed` / `login.failed` **有意留**（换出口只落回落分支、所见一字不变）。
2. 🩸 **`buildrun.log.error` 从「B 类 · 不迁」改成「A 类异常档 · 可码化」**
   —— 登记表 §4 的 **#61c 行已就地更正**（原登记点名 4 站，
   **没有一站真的属「外部工具输出」**）：`compile.failed`×2 与 `download.sharedlibrary.failed`
   捕的是**本方法的 IO**（C 类，本阶段已迁）、`buildrun.log.error` 是真抛异常（A 类）。
   ⚠ **留给下一个人**：本阶段**没有**重新扫全库去找**真正的**「外部工具输出直通文案」站点 ——
   若那种站点今天确实不存在，「B 类」应从三分类里**删掉**，而不是留个空档继续被引用。
   ⚠ 别处「与 #61c 同类（判过的不做）」（#61k / #61m、`GeneratedCodeIssueCodes.cs:114`）
   说的是同一类**动机**（产物可复现），不是同一类**成因** ⇒ 不受本更正影响。
3. **同一 `catch` 的第二条露出点**：`BuildRunDialog:102 ShowErrors(new[]{ ex.Message })` ——
   走 `ShowErrors` 而非 `.Format` ⇒ **不在直通面上**，本阶段只登记。
4. **#61x 本体未收口**（要动记录形状）；**新面看不见的那一档**已自陈（NC5 钉住）。
5. **家族**：`#61（本体，还剩 3 站 ＝ `buildrun.log.error` / `comm.test.failed` / `login.failed`）`
   / `#61k` / `#61p` / `#61r` / `#61s` / **新增 `#61x`**；＋ **4 站刻意保留的诚实边界**
   （`status.verify.failed` / `audit.verify.code.failed` / `recipe.clone.failed` /
   `status.reload.failed`）⇒ **3 ＋ 4 ＝ 7 站**，与 102.6 的 after 读数**逐条对得上**。
6. 专题 ⇒ `docs/2026-09-23-exception-passthrough-triage.md` §102。

## 阶段 103（`buildrun.log.error` 建码：`SimulatorBuildException` / `sim.build.issue.*`；并给「B 类」下**删除**定论）— 2026-09-26

**范围**：阶段 102 留下三条未闭环（① `buildrun.log.error` 建码 / ② #61x 本体 / ③「B 类还在不在」）
⇒ 本阶段吃 **① ＋ ③**，② 独立一刀（阶段 104）。

### 103.1 量的那一遍（只读）

改动前 23 条相关守卫 **23/23 全绿**（`.workbuddy/tmp/s103-probe.log`）。
`SimulatorBuildService` 该建码的抛出 **2 处**（`:102` / `:105`）；`:97-99` 三行
`ArgumentNullException.ThrowIfNull` **刻意不码化**（调用方编程错误）；
`BuildRunDialog.axaml.cs:139` 为第三处候选、与前者同一个 `catch`。

### 103.2 改法

| 层 | 文件 | 改了什么 |
|---|---|---|
| 码表 | `App/Services/SimulatorBuildIssueCodes.cs`（新） | 3 码（`LVGL_SOURCE_INVALID` / `CMAKE_NOT_FOUND` / `PROCESS_START_FAILED`），**只放码** |
| 异常 | `App/Services/SimulatorBuildException.cs`（新） | `Code` + `Args`，**刻意不继承** `InvalidOperationException`（否则老 `catch` 静默命中、编译面看不见） |
| 产出方 | `SimulatorBuildService.cs` / `BuildRunDialog.axaml.cs` | 两处前置校验 ＋ 进程启动换带码异常；中文回落**逐字留在抛点** |
| 出口 | `ExternalText.cs` | `sim.build.issue.` 前缀 ＋ `FailureReason` 新增 **②‴ 支** |
| 消费点 | `BuildRunDialog.axaml.cs` | 一处原因（`FailureReason(ex)`）、两条露出点共用 |
| 字典 | `Strings.zh-CN.json` / `Strings.en-US.json` | 各 **+3**（2282 → **2285**） |

🩸 **本族是「码表与产出方同在 `HmiEditor.App`」的第一族** ⇒ 中文回落原文留在抛点而不搬进工厂，
理由是**扫描面**：`HardcodedLocalizationGuardTests` 对 `src/HmiEditor.App` 零容忍，
但它本来就豁免 `throw new …Exception("…")` 那一类 ⇒ 不必开文件级后门。
**这是扫描面造成的，不是风格选择。**

### 103.3 判据

`tests/HmiEditor.App.UiTests/SimulatorBuildIssueLocalizationGuardTests.cs`（**6 条 ＋ 负控 NC0–NC5**，
13/13 全绿）：三态闭包（声明 **3** / 真实产出 **2** / 明确产不出 **1**）、毒化法两语言渲染、
`MaskArgs` 实参掩码、`ProducerViolations`（每条码**恰好 1 次** ⇒ 担保 `NotProducible` 不把死码合法化）、
以及一条钉「同源第二露出点」的**形状签名**判据。
🩸 新守卫第一版**负控语料有缺陷**：`Corpus(body)` 把同一段 body 喂给**两个目标文件** ⇒
每条码在两份语料里各出现一次 ⇒「恰好 1 次」把**正控自己**打红 ⇒ 拆成
`producerBody` / `siteBody`（真实形状正是「码在产出方、渲出口在站点」）。

### 103.4 🩸 定论：删掉「B 类」

原三分类 **A 类**（本仓库产出方）/ **B 类**（外部工具输出）/ **C 类**（BCL·IO）。
B 类**结构上不可能有成员** —— 那张面的谓词要求一个 `catch` 到的**异常**，而外部工具
（cmake / gcc / git / 交叉编译器）的失败**一律走返回值**：
`RunProcessAsync` 的 `int` 退出码 ＋ `SimulatorBuildResult.Errors`、
`BuildPipelineService:313` 读 `result.Error`、`GitWorkflowService` 的 `(ExitCode, stdout, stderr)`。
⇒ 三分类收窄为**两类**。定论写进 `ExceptionMessagePassthroughFaceTests` 的 #61c 条目与代码注释。

### 103.5 读数

| 量 | 前 | 后 |
|---|---|---|
| 直通面 | 7 组合 / 7 站点 | **6 组合 / 6 站点** |
| 可达机制条数 / M19 面 | 18 / — | **19** / **3** |
| M1 / M6 / M7 面 | 707 / 368 / 12 | 不动 |
| 两本字典键数 | 2282 | **2285**（+3） |
| `UnusedBaseline` / `.Format` 站点 | 1108 / 303 | 1108 / 303（不动） |
| `Core.Tests` / `App.UiTests` | 2121 / 568 | 2121 / **575**（+7） |

### 103.6 门禁

| 时刻（birth / mtime） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 14:50:20 | — | 内容批（**含判据**）`a53b680` | 11 files，＋1022 / −15 |
| **14:50:30 / 14:51:46** | `s103-gate1.log` | 门禁（7 步） | **7 步全绿**、0 警告 0 错误；2121 / **575** ＋ 2 |
| 15:01:25 | — | 文档批（s103 ＋ s104 合批）`320c28d` | 3 files，＋432 / −3 |
| **15:01:29 / 15:02:35** | `s104-gate2.log` | 门禁（7 步） | **7 步全绿**、0 警告 0 错误；2121 / 579 ＋ 2；路径卫生 **667** |
| 15:09:23 | — | 记忆批（同一次合批）`99f6fde` | 2 files，＋179；`MEMORY.md` 7943 → **7961** 码元 |
| **15:10:53 / 15:11:59** | `s104-gate4b.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**；2121 / 579 ＋ 2（`s104-gate4.log` 尾行缺 `GATE_EXIT` ⇒ 另开日志复跑） |

⚠ **本表由回填批补到 `gate4b`**（文档批 / 记忆批 ＋ 两个 `gate4*`）。
🩸 按**自指边界**：回填批自己也要过门禁 ⇒ 它的读数（`s104-gate5.log`）记在**当日日志与收口清单**、
**不回写本节** —— 否则回填批每写一行，都要再为「记录上一批」新开一批。

⚠ 判据折进内容批的理由：新守卫若不随内容提交，`IssueMessagePassthroughFaceTests` 的基线会先红
（面从 7 站掉到 6 站）⇒ 门禁必红。

### 103.7 诚实边界

1. `ShowErrors(new[] { reason })` 是**同源的第二露出点**，只收成同一次取值，未单独立判据。
2. `ThrowIfNull` 三行**刻意不码化**。
3. **#61x 本体未收口** ⇒ 阶段 104。
4. 直通面 6 站 ＝ 本体 **2 站**（`comm.test.failed` / `login.failed`）＋ 4 站**刻意保留**的诚实边界。

## 阶段 104（闭环 **#61x**：通讯族校验失败档收口 —— 契约补 `Args` ＋ `comm.issue.*` ＋ 视图出口；那条面**收官为零容忍**）— 2026-09-26

**范围**：阶段 103 剩下的那条 —— **#61x 本体**（通讯族的记录体与兄弟族不同形、收口要动记录形状）。

### 104.1 量的那一遍：🩸 单行正则**漏了一条码**

按 `issues.Add(new(ValidationSeverity.…, "…", …))` 单行扫只数到 **7** 条；
`OpcUaDriver` 的 `INVALID_ENDPOINT` 是**多行** `issues.Add`、被漏掉。
按「**真触发**」（真跑四个驱动的 `ValidateConnection`）重数 ⇒ **9 条码 / 4 个驱动**：

| 驱动 | 码 | 严重度 | 实参 |
|---|---|---|---|
| `modbus-tcp` | `MISSING_HOST` / `INVALID_PORT` | Error | 0 |
| `modbus-rtu` | `INVALID_SLAVE_ID` / `MISSING_PORT` / **`UNUSUAL_BAUD`** | Error×2 ＋ **Warning** | 0 / 0 / **1** |
| `opc-ua` | `MISSING_ENDPOINT` / `INVALID_ENDPOINT` | Error | 0 |
| `mqtt` | `MISSING_BROKER` / `MISSING_CLIENT_ID` / `INVALID_PORT` | Error | 0 |

### 104.2 改法

| 层 | 文件 | 改了什么 |
|---|---|---|
| 契约 | `Hmi.Communication.Contracts/ValidationResult.cs` | `ValidationIssue` 补 `Args`（可省略 ⇒ 既有 **8 个构造点**一处没动）⇒ 与硬件族**同形** |
| 产出方 | `ModbusRtuDriver.cs` | `UNUSUAL_BAUD` 补上本族**唯一**的实参 `baud` |
| 键推导 | `PluginTextKeys.cs` | `CommIssueKey(code)`（`comm.issue.`）；与 `IssueKey`（`hw.issue.`）**并列**、**不抽**带前缀参数的方法 |
| 消费点 | `CommunicationConfigView.axaml.cs` | 薄封装 `LocalizeIssue`（与 `ProjectSettingsView.LocalizeIssue` 同形），站点改 `.Select(LocalizeIssue)` |
| 字典 | 两本 | 各 **+9**（2285 → **2294**，键集一致） |

🩸 **前缀为什么不能当参数**：前缀一旦成为调用方的自由变量，**写错不会有任何编译期反馈** ——
只会静默回落驱动中文，正是这套机制要防的症状。

### 104.3 判据（3 条 ＋ 4 支负控）

- **登记表 × 触发面 双向**（`CommIssueRegistry_MatchesWhatTheDriversActuallyProduce`）：
  本族的码住在**插件程序集**，App 侧拿不到声明面 ⇒ 只能真触发；而触发面天生会漏
  ⇒ 用显式登记表**双向**夹住（多一个没登记 ⇒ 红；留着产不出的 ⇒ 也红）。
- **占位符个数与产出方同源**（`CommIssueTemplates_MatchTheArgumentCountsTheDriversPass`）：
  直接读驱动真传的 `Args.Count`；⚠ 与硬件族那条**刻意不同形**（那里只收 Code、靠手写"决定表"）
  ⇒ **同族第二例要分档**。
- **出口面**（`PluginIssueOutlet_IsSingle_AndViewsDoNotSpellThePrefix`）：薄封装必须调**它那一族的**
  键推导 ＋ 唯一出口 `ExternalText.Resolve` ＋ **不许自拼前缀字面量**。
- 面收官：`IssueMessagePassthroughFaceTests` **基线清空**、`Diff` 加 `baseline` 参数、
  自检 `sites.Count >= 1` → `Assert.Empty(sites)` ⇒ **1 站 → 0 站**，升格**零容忍**
  （NC6 改喂**合成基线**钉住"已消失"那一半）。

🩸 **负控写错两次**（都是我自己的语料有缺陷，**不是**产品缺陷 —— 与阶段 103 同一个坑的第二次）：
①「全注释掉 ⇒ 零违规」**期望写反**（剥注释后那份语料确实什么都没调、**该报**）；
② 合成基线那支喂的站点集**恰好等于**基线 1 ⇒ 触发不到"已消失"，必须喂**空**。

### 104.4 可达机制 M20 ＋ 两处**清单欠账**

新增 **M20**（`comm.issue.`，下界 9，取数复用触发面）；机制条数 **19 → 20**。
🩸 顺带补了两处欠账：① 阶段 103 的 **M19 只进了下界表、没进类注释的机制清单**；
② 同一处清单里 **M7 的 `<item>` 起始行**丢了、留下一个孤立 `</item>`。
⚠ 那张清单**没有判据盯着**（`MechanismFaces_AreNonEmpty` 只看面非空）⇒ 如实登记为**已知判据盲区**。✅ **阶段 106 已立判据**（清单 / 下界表 / 取数三条来源集合双向相等 ＋ 空集守卫，登记 **#63** / §106）。

### 104.5 读数

| 量 | 前 | 后 |
|---|---|---|
| **#61x 面** | 1 站 / 1 文件 | **0 站 / 0 文件**（收官） |
| 直通面 | 6 组合 / 6 站点 | 6 组合 / 6 站点（**另一条面**） |
| 可达机制条数 / M20 面 | 19 / — | **20** / **9** |
| M3-插件族派生 | 26 | **35**（+9） |
| M1 / M6 / M7 面 | 707 / 368 / 12 | 不动 |
| 两本字典键数 | 2285 | **2294**（+9） |
| `UnusedBaseline` | 1108 | 1108（不动 —— 本族**不加 `Lang` 常量**） |
| `Core.Tests` / `App.UiTests` | 2121 / 575 | 2121 / **579**（+4） |

### 104.6 门禁

| 时刻（birth / mtime） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 14:58:19 | — | 内容批（**含判据**）`a8ac78f` | 9 files，＋434 / −36 |
| **14:58:22 / 14:59:01** | `s104-gate1.log` | 门禁（7 步） | **7 步全绿**、0 警告 0 错误；2121 / **579** ＋ 2 |
| 15:01:25 | — | 文档批（s103 ＋ s104 合批）`320c28d` | 3 files，＋432 / −3 |
| **15:01:29 / 15:02:35** | `s104-gate2.log` | 门禁（7 步） | **7 步全绿**、0 警告 0 错误；2121 / 579 ＋ 2；路径卫生 **667** |
| 15:09:23 | — | 记忆批（同一次合批）`99f6fde` | 2 files，＋179；`MEMORY.md` 7943 → **7961** 码元 |
| **15:10:53 / 15:11:59** | `s104-gate4b.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**；2121 / 579 ＋ 2（`s104-gate4.log` 尾行缺 `GATE_EXIT` ⇒ 另开日志复跑） |

⚠ **本表由回填批补到 `gate4b`**（文档批 / 记忆批 ＋ 两个 `gate4*`）。
🩸 按**自指边界**：回填批自己也要过门禁 ⇒ 它的读数（`s104-gate5.log`）记在**当日日志与收口清单**、
**不回写本节** —— 否则回填批每写一行，都要再为「记录上一批」新开一批。

### 104.7 诚实边界

1. **不是改名式假闭环**：标准是**产出方有没有码**，不是"面有没有变小" ——
   同一批里 9 条码第一次有了「码 → 字典键」的对应，且由守卫**双向**夹住。
2. 本族**无**新增 `Lang` 常量、**无**新 `ExternalText.*KeyPrefix` ⇒
   `UnusedBaseline` 1108 不动、前缀清单下界仍 15。
3. 通讯族另一站 `comm.test.failed` **没动**（产出方零 `throw` ⇒ 诚实边界，阶段 102 已判）。
4. 家族：`#61（本体 2 站）/ #61k / #61p / #61r / #61s`；**#61x 已收口**。

## 阶段 105（闭环 **#62**：解包「中途失败」收口 —— 2 码 ＋ 回滚自己建的目录 ＋ 判据）— 2026-09-26

**范围**：登记表里那条**软件侧、有修法、只差动手**的实项 —— 阶段 80 §8.6 / §38.10 **第 2 条**
「解包**中途**失败留半填充目录，无修法无判据」。它在表里挂了 **25 个阶段**、**连号都没有**；
本阶段补号 **#62** 并**即刻闭环**（缺口是老缺口，号是补的 —— 写在 #62 那一行里）。
专题 ⇒ `docs/2026-09-26-unpack-midfailure-rollback.md`

### 105.1 量的那一遍：探针**实测**（不是"看着像缺口"）

只读探针把落点定在 `Directory.CreateDirectory(assetDir)` **之后**，五种输入各跑一遍
⇒ 缺口是真的，而且是**两半**：

| 场景 | 抛什么 | 留下什么 |
|---|---|---|
| **A** `reslib.json` 坏 JSON | `System.Text.Json.JsonException`（带 `LineNumber` / `BytePositionInLine` —— **#59 同病**） | `<目标>/assets/` |
| **B** `assets.json` 指向 `assets/` | `System.UnauthorizedAccessException`（BCL 英文原文直通状态栏） | `<目标>/assets/` |
| C / D / E（对照 ＋ 正控） | 不抛 | C 有资产真落盘；D / E 空 |

🩸 **B 的机制**：`Path.GetFileName("assets/")` 是**空串** ⇒ `Path.Combine(assetDir, "")`
返回 `assetDir` **本身** ⇒ `ExtractToFile` 的目标是一个**已存在的目录**。

### 105.2 改法（四层）

| 层 | 文件 | 改了什么 |
|---|---|---|
| 码表 | `HmiPackageIssueCodes.cs` | **＋2 码**：`RESLIB_INVALID`（0 实参）/ `ASSET_EXTRACT_FAILED`（1 实参 = **包内条目名**） |
| 异常 | `HmiPackageException.cs` | **＋带 `inner` 的重载**（原因不进用户文案，但留住"哪个文件 / 哪个路径 / 哪个字节偏移"） |
| 产出方 | `HmiPackageService.Unpack` | 中途失败**回滚自己建出来的目录**（先子后父 / 只删自己建的 / 吞删除异常）＋ 码化 |
| 字典 | 两本 | **＋2 键**（2294 → **2296**）；zh 模板与 Core 回落**逐字**相同 |

⚠ 宽捕获（`catch (Exception)`）是**有意**的：「界面上不该出现 .NET 英文原文」这条不变量
现在对 `Unpack` 的**全部**失败档成立。仍**刻意不码化**的一条：
`targetAssetDirectory` 空白 ⇒ `ArgumentException`（**调用方的编程错误**）。

### 105.3 判据

| 判据 | 钉什么 |
|---|---|
| `Unpack_MidFailure_LeavesNoTargetDirectoryBehind` | 码正确 ＋ 失败后目标目录**不存在** |
| 同上 · **正控 ①②** | 好包**必须**建出目录 ＋ 带资产的包**必须**真落盘（**堵死平凡解**） |
| `PackageIssueLocalizationGuardTests`（夹具 7 → **9**） | 触发面 ＋2 例；下界 `>= 9`；**每条一个独立落点** |
| `LocalizationKeyReachabilityGuardTests` | M9 下界 **7 → 9** |

🩸 **正控必须成对**：单断言「坏包不留痕迹」可以由「从不建目录」满足 ⇒ 判据就成了装饰。
🩸 **独立落点**：9 例共用一个落点时，中途失败留下的 `<目标>/assets/` 会污染**后来者**的读数。

### 105.4 读数

| 量 | 前 | 后 |
|---|---|---|
| 码数 / **M9 面** | 7 / 7 | **9 / 9** |
| 两本字典键数 | 2294 | **2296**（**量出来的**，不是"上一阶段 +2"推的） |
| Core 台账 `VisibleChannels`（**判据钉的就是这张**） | 15 / 100 | **15 / 100（不动）** |
| Core 台账 `Pending["Serialization/HmiPackageService.cs"]` | 7 | **9** |
| `UnusedBaseline` | 1108 | 1108（不动 —— 本族**不加** `Lang` 常量） |
| `Core.Tests` / `App.UiTests` | 2121 / 579 | **2122** / 579（App 侧**无**新 `[Fact]`） |

### 105.5 门禁

| 时刻（birth / mtime） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 15:35:06 | — | 内容批（含码表 / 回滚 / 判据 / **登记号 #62**）`0e5757c` | 12 files，**＋302 / −48** |
| **15:35:10 / 15:36:28** | `s105-gate1.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；44 / 44 / 75 / 87 / 4 / 315 / **2122** / **579** ＋ 8 ＋ 2；路径卫生 **667** |
| 15:37:40 | — | 文档批（专题 ＋ 状态表 ＋ §81.10 钉闭环）`a74e74f` | 3 files，**＋206 / −1** |
| **15:37:43 / 15:38:47** | `s105-gate2.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；2122 / 579 ＋ 8 ＋ 2；路径卫生 **667** |
| 15:40:09 | — | 记忆批（析出 §20 ＋ 快照 26r）`4d238bc` | 2 files，**＋174**；`MEMORY.md` 7961 → **7877** 码元（余量 **123**） |
| **15:40:13 / 15:41:17** | `s105-gate3.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；2122 / 579 ＋ 8 ＋ 2；路径卫生 **667** |

⚠ **本表由回填批补到 `gate3`**（文档批 ＋ 记忆批各自的提交与门禁）。
🩸 按**自指边界**：回填批自己也要过门禁 ⇒ 它那一跑（`s105-gate4.log`）的读数记在**当日日志**、
**不回写本表**（否则回填批每写一行，都要再为「记录上一批」新开一批）。

⚠ **#62 的登记表行放在内容批、不是文档批**：`CoreVisibleTextInventoryGuardTests` 的归位链
那句正则要求括号里**只有 `#id`**（`阶段 \d+（#[0-9a-z]+）为 \*\*(?<c>\d+) / (?<n>\d+)\*\*`）
⇒ **号必须先存在**，否则那一批自相矛盾。

### 105.6 诚实边界 / 未闭环

1. `targetAssetDirectory` 空白 ⇒ `ArgumentException`（**编程错误**，界面上不该有对应文案）——**刻意留白**。
2. 图库解析只做「坏 JSON」这一档：探针**只实测到** `JsonException`
   ⇒ **不为没见过的异常类型造码**（M8 / M10 / M11 同款纪律）。
3. 探针桩 `tests/HmiEditor.Core.Tests/ProbeMidUnpackFailureTests.cs`（714 字节、纯注释、**未跟踪**）
   本会话删不掉（逐文件 bind-mount，登记 #32 同现象）⇒ **下次会话可直接删**（**阶段 106 开工时已删**：`/proc/mounts` 命中 **0**、`rm` 成功、`git status --short` 随即干净）。
4. 🩸 本阶段真踩一脚：把 `Pending` 桶当成 `VisibleChannels` 那条链去改数字 ⇒
   `Assert.Equal(100, sum)` **当场红**（`s105-core.log`：Expected 102 / Actual 100）
   ⇒ 已改回，并在守卫与盘点专题 §0 各留一条注（这正是"**判据盯着的数字要先量它数的是哪张表**"）。
5. ⚠ **本阶段**没有**把「软件侧」清空**：还开着 **#39**（32 个窗口的事件接线夹具）与
   **#45**（Dock 面板用例与大队同进程僵死、**根因未定**）两条软件侧项；
   **#61p / #61q / #61r / #61s / #61k** 属口径 / 产品决定，**#58** 与 §5 属环境限制。
   原先那句「最后一条软件侧可验证的实项」是**过强的说法**，本批已更正（见本节开头的**范围**那句）。

## 阶段 106（2026-09-26）—— 「机制清单」有判据夹住了（登记 #63 闭环）

> 范围：闭合阶段 104 §104.4 / 专题 §104.7 **自己如实登记**的那条**已知判据盲区**
> ——「加机制要同时改**下界表**与**类注释的机制清单**」这句纪律，此前**没有任何东西盯着**。
> **产品代码 0 行改动**。专题 ⇒ `docs/2026-09-26-mechanism-declaration-guard.md`。

### 106.1 缺口：三条来源，四处漏法里三处静默

一个机制要在**三处**各写一遍：① 类注释的**机制清单**（`///   <item><b>M19</b> …`）；
② `MechanismFaces_AreNonEmpty` 的**下界表**；③ `ReachableKeys()` 的**取数**。

| 漏法 | 阶段 106 之前 |
|---|---|
| 加机制只改 ③ ⇒ 那机制**没有下界**，退化成空集也没人管 | **完全静默** |
| 加机制改 ③② 忘 ① ⇒ 清单少一条（**M19 就是这样**） | **完全静默**（阶段 104 手工补的那次**不会响**） |
| 清单 id 写错 / 写多 | **完全静默** |
| 加机制改 ③② 忘 ② ⇒ 面 0 < 下界 | 已被 `MechanismFaces_AreNonEmpty` 抓到 |

### 106.2 改法

| 改 | 内容 |
|---|---|
| 提字段 | 下界表从**局部变量**提成 `private static readonly (string Kind, int Min)[] LowerBounds` —— 局部变量看不见，「清单 ↔ 下界表」这条比对**在同一个类里**也做不到（数值语义不动） |
| 新判据 | `Mechanisms_AreDeclaredInAllThreePlaces`：三处集合**双向**相等（①↔② 按 **id**、②↔③ 按 **kind**） |
| 三条加固 | **空集守卫**（任一处 ≥ 20，否则差集恒真）＋ **id 形状自检**（否则比对静默错位）＋ **清单重复条目**单列（`Distinct` 会把重复吃掉、差集看不出来） |

🩸 **②↔③ 的反方向是这次真正补上的那一半**：此前「取数造了一个下界表里没有的 kind」
完全静默 —— 那条机制**没有下界**，退化成空集也没人管；反方向（表有、取数没有）早被面 0 抓到。

### 106.3 负控 4/4

| # | 注入（都能编译） | 实测 |
|---|---|---|
| NC1 | 清单里 M19 的 id 改名成 M29 | **红 ✓** 点名「下界表里有 M19」＋「清单里有 M29」 |
| NC2 | 清单里追加不存在的 M21 | **红 ✓** 点名 M21 |
| NC3 | 取数里注入下界表没有的 kind | **红 ✓** 点名「取数有、下界表没有」 |
| NC4 | 把清单读取的**正则改坏** | **红 ✓**（空集守卫「有一处取空了」） |

还原写回原文、SHA256 逐字节相同。🩸 **首跑四支全红却点名 0/4** —— 用了 `-v q`
把断言消息吞了：**「红」不足以证明判据在响**（也可能是编译失败）⇒ 改成详细输出复跑才到位。

### 106.4 读数

| 量 | 前 | 后 |
|---|---|---|
| 该类用例数 | 6 | **7** |
| `App.UiTests` | 579 | **580**（+1 = 新判据） |
| 三处来源的机制条数 | 20 / 20 / 20 | 20 / 20 / 20（一致） |
| 两本字典键数 / `UnusedBaseline` / `Core.Tests` | 2296 / 1108 / 2122 | **全不动** |

### 106.5 门禁

| 时刻（birth / mtime） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 15:53:57 | — | 内容批（判据 ＋ 字段提升 ＋ 类注释）`a71a427` | 1 file，**＋199 / −99** |
| **15:54:00 / 15:55:26** | `s106-gate1.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；44 / 44 / 75 / 87 / 4 / 315 / **2122** / **580** ＋ 8 ＋ 2；路径卫生 **667** |
| 15:56:53 | — | 文档批（专题 ＋ 状态表 ＋ 登记 **#63** ＋ 三处钉闭环）`4103b67` | 3 改 ＋ 1 新，**＋71 / −3** |
| 15:56:56 / 15:57:59 | `s106-gate2.log` | 门禁（7 步） | ⚠ **`GATE_EXIT=1`** —— **环境性红**（见下），唯一失败项是 `/tmp` 写不进去 |
| **15:58:43 / 15:59:51** | `s106-gate2b.log` | 门禁（7 步，**同代码复跑**） | `GATE_EXIT=0` / **7 步全绿**；2122 / 580 ＋ 8 ＋ 2；路径卫生 **667** |
| 16:00:32 | — | 记忆批（两条教训 ＋ 快照 26s）`452fcc8` | 1 file（快照 26s）；`MEMORY.md` 7877 → **7931** 码元（余量 69） |
| **16:00:36 / 16:01:46** | `s106-gate3.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**；2122 / 580 ＋ 8 ＋ 2；路径卫生 **667** |

🩸 **`gate2` 那一跑是环境性红、不是本批改动**：唯一失败项
`HmiEditor.Device.Tests.T113SoBuilderTests.ResolveContext_MissingSdk_ReportsMissingItems`，
而**异常消息本身**就写着原因 —— `System.IO.IOException : No space left on device :
'/tmp/sdk-…/generated_hmi.c'`（**不是断言不符**）。本沙箱 `/tmp` 是 **10 MB 的 tmpfs**
（`df -h /tmp` ⇒ `tmpfs 10M`），而 `TestTempDir.NewIsolatedDir` 落在系统临时目录。
三条证词：① 同代码 `gate1` 全绿（87/87）；② `gate2b` 复跑全绿；③ 该程序集**单独复跑**
87/87 绿（`s106-dev1.log`）。⇒ 按既定纪律「**全量随机红先怀疑磁盘/临时目录容量**」判为环境限制。

⚠ **但不据此把 `gate2` 一笔勾销**：两行读数都留、红的那次**不删**。已登记进登记表 §5 **#33**。

⚠ **自指边界**：本表由**回填批**补到 `gate3`；回填批**自己**那一跑（`s106-gate4.log`）
的读数记在**当日日志**、**不回写**本表（否则回填批每写一行，都要再为「记录上一批」新开一批）。

### 106.6 诚实边界 / 未闭环

1. 判据读**工作树里的本文件源码** ⇒ 改了没落盘看不见（项目既定「读数读工作树」同口径）。
2. 清单条目里的**人话**（阶段号 / 码表名 / 下界为什么取这个数）**判据不读** ⇒
   它抓的是**集合**，不是**说法**。
3. 空集守卫的下界 **20** ＝ 当前条数：**删**机制时它会先红，那是**期望行为**。
4. 本判据**不覆盖**「下界数值是否仍合理」—— 由各族三态闭包判据盯着。
5. ⚠ **本阶段也没有把「软件侧」清空**：还开着 **#39**（32 个窗口的事件接线夹具）与
   **#45**（Dock 面板用例与大队同进程僵死、**根因未定**）；**#61p / #61q / #61r / #61s / #61k**
   属口径 / 产品决定，**#58** 与 §5 属环境限制。

## 阶段 107（2026-09-26）—— 宿主内部按钮的「可观测效果」判据（第五层动作面，登记 #39）

> 范围：给**不弹窗**的按钮建运行期判据 —— 四层动作面判据判的都是「弹窗的层数 / 类型」，
> 而面板里绝大多数按钮根本不弹窗。**产品代码 0 行改动**。
> 专题 ⇒ `docs/2026-09-26-host-button-effect-net.md`；判据 ⇒
> `tests/HmiEditor.App.UiTests/HostButtonEffectUiTests.cs`（627 行，新增）。

### 107.1 缺口：三种坏情况与「正常」不可分辨

| 情况 | 四层判据（菜单 / 工具条 / 对话框内 / 停靠面板） |
|---|---|
| 接线正常、改数据 | 静默 → 绿 |
| **接线被删掉** | 静默 → **绿** |
| **接到了另一个处理器上** | 静默 → **绿** |
| 接了线但什么都没做 | 静默 → 绿 |

四份文件的「不抓什么」里都**各自明示**了不判副作用。⇒ #39 的那一半就在这里。

### 107.2 量的那一遍（三轮口径修正，第四版才与判据同口径）

| 版 | 分母 | 错在哪 |
|---|---|---|
| 1 | `X.Click +=` 的接收方 | **分子错**：只认大写开头的标识符 ⇒ 漏 `FindControl` 后的小写局部量 |
| 2 | 带事件属性的元素 | **分母错**：漏掉「显示型控件」与「代码里按名找控件再接线」 |
| 3 | `x:Name` 落在可交互类型上 | 分母混着 `ComboBox` / `TextBox` / `ListBox`，与判据口径不一致 |
| **4（定稿）** | `x:Name` 落在 **Avalonia 的 Button 面**上（含 `CheckBox`） | — |

🩸 第 1 版那个错**在第二版就报出了相反的方向**：仪器误差 **13 个**（只被生成字段 / 词边界
访问，第二版却算成残余）⇒「残余 60%」里有一部分是**仪器自己的**。
定稿口径的理由：判据用 `OfType<Button>()`，而 `CheckBox : ToggleButton : Button`
⇒ 分母必须含 `CheckBox`（第 3 版那个「32 个」正是漏了它）。

**读数（口径 4）**：面 **307** 个具名按钮（43 个 `.axaml`）／五层提过 **161**（52%）／
**残余 146**（47%，27 个视图）。残余前五：`SettingsDialog` 25、`ProjectSettingsView` 18、
`CommunicationConfigView` 14、`TrendConfigView` 11、`PropertyPanelView` 10。

⚠ **「残余」不等于「没有任何判据碰过」**：分子只数**这一族（动作面）判据**提过的名字 ——
`ProjectSettingsView` 另有往返保真判据盯着它 ⇒ 残余的意思是「不在动作面这一族的覆盖面里」。

### 107.3 改法：判「点一下之后数据面变成什么」

| 层 | 内容 |
|---|---|
| ① 可观测效果 | 分量从**两处**读：**模型**（集合计数）证明「数据真的变了」，**窗口控件**（`ListBox.Items.Count`）证明「界面真的刷新了」。逐条按钮声明点击后哪些分量**等于什么值** |
| ② 面双向差集 | 宿主里每个具名 `Button` 必须被分类（少了 / 多了都红） |
| ③ 表卫生 | 桶自洽、理由写得出、`Expect` 的分量名真的存在、宿主类型真的存在 |

**断言是双向的**：① 声明的分量必须**等于**声明值（不是「变了」—— 接错处理器时读数照样变）；
② **没声明的数据面分量必须一字不变**（抓「副作用外溢」）；③ 另有一条自检：
`Moves` 的声明**至少要有一条真的动了**（防「抄当时读到的值」那种恒真声明）。

桶：`Moves` 11 / `Inert` **17** / `Never` 3。⚠ 此处原写「`Inert` 14」是**手抄错的**（把 `VariableManagerView` 那一行的数当成了总和；逐宿主 14＋3＋0）—— 阶段 108 更正，见 §108.4。

### 107.4 🩸 `CheckBox` 必须先翻转 `IsChecked` 再引发 `Click`

对普通 `Button` 就是 `RaiseEvent(Button.ClickEvent)`；对 `ToggleButton` 系（含 `CheckBox`）
**必须先翻转**，因为框架里 `ToggleButton.OnClick` 干的就是这两件事。只引发 `Click` 会得到
**假阴性** —— `IsCheckedChanged="…"` 这种接线**一次都跑不到**。见证者是
`ScreenManagerView.ChkPublic`（XAML 里**没有** `Click=`）：补上翻转之后读数才从
「公开屏 0 → 0」变成「**0 → 1**」。这是本层与四层判据的一处**真实能力差异**。

### 107.5 读数

| 宿主 | 面 | Moves | Inert | Never |
|---|---|---|---|---|
| `VariableManagerView` | 20 | 5 | 14 | 1 |
| `AlarmConfigView` | 7 | 3 | 3 | 1 |
| `ScreenManagerView` | 4 | 3 | 0 | 1 |
| **合计** | **31** | **11** | **17** | **3** |

⚠ **合计行的 `Inert` 原写 14、阶段 108 更正为 17**（逐宿主 14＋3＋0；原数是把`VariableManagerView` 那一行当总和手抄的）。

该类用例 **4**（3 Theory ＋ 1 表卫生）；`App.UiTests` 580 → **584**；动作面判据 **4 层 → 5 层**；
`Core.Tests` 2122 / 两本字典 2296 / `UnusedBaseline` 1108 **全不动**。

### 107.6 负控 4/4（判红 ＋ 点名，缺一不可）

| # | 注入（都能编译） | 期望子串 | 实测 |
|---|---|---|---|
| NC1 | `BtnAddVariable` 接到 `DeleteVariable()`（接错处理器） | `分量「变量」期望 3、实际 2` | **红 ✓** |
| NC2 | 注释掉 `BtnAddScreen.Click += …`（摘掉接线） | `分量「屏幕」期望 3、实际 2` | **红 ✓** |
| NC3 | 表里塞一个不存在的控件名（面守卫） | `找不到这些按钮：BtnNotExist` | **红 ✓** |
| NC4 | 删掉 `BtnDeleteGroup` 的 `("报警","1")` 声明（未声明分量外溢） | `**未声明**的分量「报警」从 2 变成了 1` | **红 ✓** |

逐支一个日志（`s107-nc1..4.log`）、逐支**写回原文**并断言 SHA256 与注入前相同
（**不用** `shutil.copy2`：它会连 mtime 一起复制 ⇒ MSBuild 判定无需重建 ⇒
「还原后的复跑」实际跑的是负控那次的二进制）。
🩸 **NC3 的副作用值得记住**：注入的名字在 `Inert` 桶 ⇒ **表卫生那支照样绿**，
只有 Theory 那支红 —— 这正是「面守卫在运行期、卫生检查在静态」两层分工的实证。

### 107.7 门禁

| 时刻（birth / mtime） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 16:30:09 | — | 内容批（判据 **627** 行）`ef6e22c` | 1 file，**＋627**；产品代码 0 行 |
| **16:30:33 / 16:31:56** | `s107-gate1.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；44 / 44 / 75 / 87 / 4 / 315 / **2122** / **584** ＋ 8 ＋ 2；路径卫生 **667** |
| 16:33:45 | — | 文档批（专题 ＋ 状态表 §107 ＋ 登记 **#64** ＋ #39 补记）`6f75935` | 3 files，**＋342 / −1** |
| 16:33:45.202 / 16:34:50 | `s107-gate2.log` | 门禁（7 步） | `GATE_EXIT=0`；⚠ **birth 与提交同秒 ⇒ 分不清先后，不作证据** |
| **16:35:01 / 16:36:04** | `s107-gate2b.log` | 门禁（**复跑**，同一代码） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；2122 / **584** ＋ 8 ＋ 2；路径卫生 **667** ⇒ **晚于提交 76 秒，这条才算证据** |
| 16:40:28 | — | 记忆批（析出 §21 ＋ 快照 26t）`ca542d6` | 2 files，**＋137**；`MEMORY.md` 7931 → **7950** 码元（余量 **50**） |
| **16:40:32 / 16:41:50** | `s107-gate3.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；2122 / **584** ＋ 8 ＋ 2；路径卫生 **667** |

时序（提交 → 门禁 birth）：`ef6e22c` **16:30:26 → 16:30:33**（晚 7 秒）／`6f75935`
**16:33:45 → 16:33:45.202**（🩸 **同秒，分不清先后** ⇒ 复跑取 `16:35:01`，晚 **76 秒**）／
`ca542d6` **16:40:28 → 16:40:32.486**（晚 4.5 秒）。
🩸 **教训**：`git` 的时间戳只到**秒**、`stat` 的 birth 有**纳秒** —— 两者落在同一秒里时
**无法判定先后** ⇒ 那一跑不构成「门禁验证了这次提交」的证据，重跑一次取明确晚于提交的 birth。
🩸 按**自指边界**：回填批自己那一跑（`s107-gate4.log`）的读数记在**当日日志**、**不回写本表**
（否则回填批每写一行，都要再为「记录上一批」新开一批）。

### 107.8 ⚠ 顺带量出**登记 #64**（产品代码里的真缺陷）

首跑（`s107-run2.log`）里 `VariableManagerView` 那支当场红了 4 条，全指向同一件事：
点了 `BtnAddVariable` 之后 `BtnAddAlarm` 是空操作（报警列表 0 → 0）。追下去 ⇒
`SelectListItemByTag(ListBox, tag)` 拿 `lbi.Tag?.ToString()` 去比 **Id**，而 `Tag` 里装的是
**模型对象**（`Variable` / `VariableGroup` / `VariableAlarmConfig` / `UdtDefinition`
**都没覆写 `ToString()`**）⇒ **6 处调用恒为空操作**，「新建后自动选中」这条注释声明的行为
**不存在**。⚠ **不把它固化成期望**：表里那几条 `Inert` 的 `Why` 明写「修好后这条会红 ⇒
届时改成 Moves」——「**修好就红**」是设计要求，不是脆弱。

同批另记一条**不同根因**的交互事实（**未判为缺陷**）：`AlarmConfigView.OnAddAlarm` 末尾的
`RefreshGroupList()` 重建左列表时把右列表切回第一个组 ⇒ 刚新建的报警立刻「选不上」，
紧接着点「删除报警」删不动。

### 107.9 诚实边界 / 未闭环

1. 本层只声称「行为与声明一致」，**不声称「这条接线是对的」** —— `Inert` 里混着缺陷现场。
2. 面只覆盖 **3 宿主 / 31 按钮**；Button 面整体 **307**、五层提过 161 ⇒ 残余 **146**。
3. 不判跨层副作用（写盘 / 起进程 / `IStorageProvider`）。
4. `Voice` 分量（状态栏文案）只进读数、不进断言 —— 断言它等于把本地化文案冻结。
5. **数组顺序 = 点击顺序 = 被测语义**，改顺序等于改语义。
6. `TrendConfigView` / `SimulatorView` / `AiChatPanelView` **刻意没进首批**：它们带 500 ms
   刷新定时器或 `DataLogger`（真写盘）⇒ 进面之前要先确认「短命定时器会不会跨用例留痕」。
7. ⚠ **本阶段也没有把「软件侧」清空**：还开着 **#39**（本阶段只覆盖它的一半里的一刀）与
   **#45**（Dock 面板用例与大队同进程僵死、**根因未定**）；**#61p / #61q / #61r / #61s / #61k**
   属口径 / 产品决定，**#58** 与 §5 属环境限制。

## 阶段 108（2026-09-26）—— 修登记 #64：列表选中按 `Id` 比（`IIdentifiedModel` 契约）

> 范围：**产品代码**修一个真缺陷 —— `VariableManagerView.SelectListItemByTag` 拿
> `Tag?.ToString()` 去比 `Id`，而 `Tag` 里装的是**模型对象**、这四个模型都没覆写 `ToString()`
> ⇒ 6 处调用全部空操作（「新建后自动选中」声明的行为不存在）。
> 专题 ⇒ `docs/2026-09-26-list-selection-id-contract.md`。**这条缺陷是判据自己抓出来的，不是人肉 review 出来的。**

### 108.1 口径（三条路，只选一条）

| 路 | 做法 | 判 |
|---|---|---|
| **①（选）** | 改 **helper 的比对一端**：`Tag` 是模型 ⇒ 按 **`Id`** 比；给模型加**显式契约接口** | ✅ 无类型分支、无静默回落 |
| ② | 改 6 个调用点：传对象、按**引用同一性**比 | ❌ `RefreshGroupList` 给「未分组」**每次刷新都新建** `VariableGroup{Id=""}` ⇒ 新实例 ≠ 旧实例，反而漏 |
| ③ | 按类型 `switch` / 反射反推 `Id` | ❌ 新增列表 Tag 类型时**静默退回 `ToString()`** —— 正是本缺陷的形状 |

### 108.2 改动（7 files，**＋90 / −27**）

| 文件 | 改了什么 |
|---|---|
| `src/HmiEditor.Core/Models/IIdentifiedModel.cs`（新，23 行） | 契约接口；注释写明「别退化成 `switch` / 反射」 |
| `Variable.cs` / `VariableGroup.cs` / `Alarm/VariableAlarmConfig.cs` / `VariableUdt.cs` | 各 **1 行**（实现接口） |
| `src/HmiEditor.App/Views/VariableManagerView.axaml.cs`（＋7 / −2） | 判据改 `{ Tag: IIdentifiedModel model } && model.Id == tag`；`<remarks>` 记下两条**别这么改** |
| `tests/…/HostButtonEffectUiTests.cs`（＋56 / −21） | 表按**实测**改 ＋ 加 3 个「选中项」分量 |

⚠ `SelectComboBoxByTag` 与 `CreateTaggedListItem` **一行没动**。

### 108.3 判据侧：先跑、让它点名，再改表

| 按钮 | 阶段 107 | 阶段 108 |
|---|---|---|
| `BtnAddAlarm` / `BtnDeleteAlarm` / `BtnDuplicateVariable` / `BtnDeleteVariable` | `Inert` | **`Moves`**（「修好就红」按预案转档） |
| `BtnSaveVar` / `BtnSaveAlarm` / `ChkAlarmEnabled` | `Inert`（`Why` 归因 #64） | `Inert`（**`Why` 更正**：真实成因是**点击顺序**） |

新加 3 个分量 `变量选中` / `报警选中` / `UDT选中`（helper `SelectedTagIs<T>`），把修复**直接**钉住 ——
`UdtList` 那条选择路径原先**零覆盖**（`BtnAddUdt` 直接给 `_selectedUdt` 赋值，绕过列表选中）。

| 宿主 | 面 | Moves | Inert | Never |
|---|---|---|---|---|
| `VariableManagerView` | 20 | **9** | 10 | 1 |
| `AlarmConfigView` | 7 | 3 | 3 | 1 |
| `ScreenManagerView` | 4 | 3 | 0 | 1 |
| **合计** | **31** | **15** | **13** | **3** |

### 108.4 更正一：阶段 107 的「`Inert` 合计 14」是**手抄错的**

逐宿主是 **14 + 3 + 0 = 17**；阶段 107 把 `VariableManagerView` **那一行**的数当成了总和，
错的数字传到 **4 处**（§107.3 / §107.5 / 专题 107.5 / 登记 #39），本批一并改 17。
🩸 **分档数字只许从判据的输出行抄，不许手算** —— 判据一直在印
`宿主 3 个；按钮 31 条（Moves 15 / Inert 13 / Never 3）`，⚠ 但**绿跑默认不落日志**
（`ITestOutputHelper` 只在失败时印）⇒ 要抄得显式加 `--logger "console;verbosity=detailed"`。
与「归位行的数字必上判据」（#61w）同族：**手抄的数字没有判据盯着**。

### 108.5 更正二：`Inert` 的 `Why` 是**免证的**

阶段 107 有 3 条（`BtnSaveVar` / `BtnSaveAlarm` / `ChkAlarmEnabled`）把成因错记成 #64，
真实成因是**点击顺序**（选中项在更早一步被显式置空：`axaml.cs:509` / `:439` / 根本无接线）。
没响的原因：**没有任何判据会去打 `Inert` 的 `Why`**（表卫生只校验长度 ≥ 10）。
⚠ `Moves` 的 `Why` 会被真值校验间接逼着写对，**`Inert` 的不会** ⇒ 写理由时必须真读一遍处理器。

### 108.6 负控 3/3（判红 ＋ 点名）

| # | 注入（能编译） | 期望子串 | 实测 |
|---|---|---|---|
| NC1 | 比对**退回缺陷形状**（`Tag.ToString()` 比 `Id`） | `分量「报警列表」期望 1、实际 0` | **红 ✓** |
| NC2 | 摘掉契约（`Variable` 不再实现 `IIdentifiedModel`） | `分量「变量选中」期望 1、实际 0` | **红 ✓** |
| NC3 | 把声明值**写反**（「变量选中」1 → 0） | `分量「变量选中」期望 0、实际 1` | **红 ✓** |

NC1 最要紧：**证明 #64 再回归会被抓住**（阶段 107 时这一族对它全绿）。
逐支一个日志（`s108-nc1..3.log`）、逐支**写回原文**并断言 SHA256 与注入前逐字节相同。

### 108.7 门禁

| 时刻（birth / mtime） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 16:49:46 | — | 内容批（产品代码 ＋ 表）`0fa8855` | 7 files，**＋90 / −27** |
| **16:49:49 / 16:50:56** | `s108-gate1.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；44 / 44 / 75 / 87 / 4 / 315 / **2122** / **584** ＋ 8 ＋ 2；路径卫生 **668**（＋1 ＝ 新接口文件）/ 407 个 `.cs` |
| 16:52:22 | — | 文档批（专题 ＋ §108 ＋ **更正 107 手抄错** ＋ 登记 #64 闭环）`06a796f` | 4 files，**＋256 / −5** |
| **16:52:25 / 16:53:30** | `s108-gate2.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；2122 / **584** ＋ 8 ＋ 2；路径卫生 **668** |
| 16:55:45 | — | 记忆批（两条新教训 ＋ 析出 §22 ＋ 快照 26u）`a348c87` | 2 files，**＋153**；`MEMORY.md` 7950 → **7961** 码元（余量 **39**） |
| **16:55:48 / 16:56:51** | `s108-gate3.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；2122 / **584** ＋ 8 ＋ 2；路径卫生 **668** |

时序（提交 → 门禁 birth）：`0fa8855` **16:49:46 → 16:49:49.587**（晚 3.6 秒）／
`06a796f` **16:52:22 → 16:52:25.535**（晚 3.5 秒）／`a348c87` **16:55:45 → 16:55:48.689**（晚 3.7 秒）。
🚦 三跑**都清晰**，没像阶段 107 那样撞上同秒 —— 因为这次**提交后立刻取一次 birth**
（那一课已进记忆 §4.1）。
🩸 按**自指边界**：回填批自己那一跑（`s108-gate4.log`）的读数记在**当日日志**、**不回写本表**
（否则回填批每写一行，都要再为「记录上一批」新开一批）。

### 108.8 诚实边界 / 未闭环

1. 契约只挂**这 4 个**模型（`VariableManagerView` 列表用到的全部 Tag 类型）；`UdtField` 无 `Id`
   （键是 `Name`）且没被 `SelectListItemByTag` 用过 ⇒ 本次不挂。
2. `BtnSaveVar` / `BtnSaveAlarm` 的 `Inert` 理由是「**未被观测**」而**不是**「没动作」：
   本层分量里没有「报警启用位 / 需确认位」。
3. 表里其余 `Inert` 的 `Why` 本批顺带读过一遍，但复核是**人工的、不是判据** ⇒ 仍各自免证。
4. **#64 闭环，软件侧仍未清空**：**#39**（残余 146）、**#45**（根因未定）仍开着。

---

## 阶段 109（2026-09-27）—— 第五层的第一个「设置面」宿主：`SettingsDialog`（登记 #39 继续收残余）

> 专题 ⇒ `docs/2026-09-27-button-effect-setup-face.md`。**产品代码 0 行改动**
> （表在测试项目里，数据面读的是夹具已有的两个服务）。

### 109.1 换数据面形状，验证判据骨架可复用

阶段 107 的三处宿主，数据面全是**模型集合计数**＋**窗口控件条目数**。
本阶段换成**服务对象上的属性**（`SettingsService` 20 个设置项 ＋ `AppThemeService.Current`）：

| 维度 | 前三处（107） | `SettingsDialog`（109） |
|---|---|---|
| 数据面在哪 | 模型集合 | **服务对象** |
| 一次点击改几个分量 | 常 2~3 个 | **恰好 1 个** |
| 面里 `CheckBox` 占比 | 3 / 31 | **19 / 26** |
| 依赖 | 模型 ＋ 工程 | 两个服务（**夹具里已有**，没加一行） |
| 有状态栏吗 | 有 | **没有**（对话框） |

⇒ **判据骨架一行没动**（`Probe` / `Bucket` / `ClickSpec` / `Check` / 面守卫全原样），
换的只是 Probe 的读法。这是本阶段要证明的那件事。

### 109.2 读数与口径（🩸 探针是**名字级**的）

| 量 | 阶段 107 / 108 | 阶段 109 | 差 |
|---|---|---|---|
| 面（逐视图去重求和） | 307 | **307** | 0 |
| 五层提过名字的 | 161（52%） | **194（63%）** | ＋33 |
| 残余 | 146（47%） | **113（36%）** | **−33** |
| 残余视图数 | 27 | **25** | −2 |

🩸 **但 −33 里只有 25 个是真覆盖。** 探针判「提过」用的是
`f'"{名字}"' in 守卫源码` —— 只看**名字字符串**、不看视图上下文。
本轮把 `BtnApply` / `BtnCancel` / `BtnOk` / `ChkShowGrid` 这些**跨视图重名**的按钮写进表后，
**别的视图里同名的那些也一起从残余里消失**，而它们**并没有**被第五层覆盖（面是逐宿主收的）。

逐视图差额：`SettingsDialog` 25 → **0**（真覆盖）／`ProjectSettingsView` 18 → 15
（`BtnApply`、`BtnCancel`、`BtnOk`）／`LoginDialog` 2 → 1、`FaceplateTemplateEditorView` 2 → 1、
`VariableImportView` 4 → 3、`StylePresetPicker` 1 → 0（全是 `BtnCancel`）／
`TrendConfigView` 11 → 10（`ChkShowGrid`）。

⇒ **真覆盖 25 个名字、8 个位置是口径顺带。** 该偏差**修不掉**（一个名字无法归因到视图），
它是这条探针的固有性质 —— 别把 −33 读成「33 个按钮被收了」。

⚠ 顺带更正一处读法：探针打印的 `Views/SettingsDialog.axaml（25）` 是**残余数**、不是面。
该视图面是 **26**（多出的 `BtnTitleClose` 早被 `DialogInnerInvocationUiTests` 提过）。
运行期实测面也是 **26**，一致。

### 109.3 表（26 条）与 Probe

桶：`Moves` **22**（3 个主题按钮 ＋ 19 个 `CheckBox`）／`Inert` **1**（`BtnApply`）／
`Never` **3**（`BtnOk` / `BtnCancel` / `BtnTitleClose`，都走 `Close()`）。

Probe **20 个**，**全部数据面、无 Voice 分量** —— 本层第一次跑就抛在
`SettingsDialog` 里**没有 `StatusText` 具名控件**（它是对话框、不是面板）。
`Control<T>` 找不到就抛是**刻意不静默**（否则控件改名会让 Probe 恒读到同一个值）。

夹具初态：**除 `AutoHidePanels` 为 `false` 外全 `true`**；主题默认 **`Dark`**。

### 109.4 🩸 顺序在本宿主上是**真的语义**

三个主题按钮共用 `OnThemePresetClicked`（读 `e.Source(Button).Tag` 去 `SetTheme`）。
初态 `Current == Dark` ⇒ 若把 `BtnThemeDark` 排**第一**，它写进去的是**已经是** `Dark` 的值
⇒ 命中判据那条「**声明成 `Moves` 但声明的分量一个都没动**」的自检。
⇒ 顺序定成 `Light → Dark → Ocean`，每一步都真的改变 `Current`。

同类但含义不同的：19 个复选框的期望值取决于**夹具默认值** ⇒ 哪天 `UserSettings()` 默认翻了，
这 19 条当场变红 —— 那是**对的**（把默认值的变化变成可见事件）。

### 109.5 负控 4/4

| # | 注入（都能编译） | 实测点名 |
|---|---|---|
| NC1 | `ChkAnimations` 处理器**接到别的设置项**（`SetEnableAnimations` → `SetShowGrid`） | `分量「交互·动画」期望 0、实际 1` ＋ `**未声明**的分量「交互·网格」从 1 变成了 0` **红 ✓** |
| NC2 | **摘掉** `ChkCtxMoveLayer` 的接线 | `分量「浏览器·移层」期望 0、实际 1` **红 ✓** |
| NC3 | 声明值**写反**（`BtnThemeLight` 期望 `Light` → `Ocean`） | `分量「主题」期望 Ocean、实际 Light（点击前 Dark）` **红 ✓** |
| NC4 | 处理器**写死常量**（`SetAutoHidePanels(false)`） | `分量「布局·自动隐藏」期望 1、实际 0` ＋ `Moves 却一个都没动` **红 ✓** |

- NC1 是**核心**：接错处理器 ⇒ 一个分量没变、另一个被误改，**两条都报**；
  「变了就行」的判据在这一支下**全绿**。
- NC4 钉的是**真实缺陷形状**（处理器不读控件状态），不是笔误形状。

🩸 **负控脚本自己的缺陷（本轮抓到并修掉）**：写回原文**不能**用 `shutil.copy2` ——
它把**原 mtime 一起复制回来** ⇒ 还原后的源码比上次编译产物还旧 ⇒ MSBuild 判定
「没变、不用重建」⇒ **下一支负控读到上一支的陈旧程序集**。
症状：NC3 的日志里混着 **NC2 的 `ChkCtxMoveLayer` 红**。
修法：`p.write_bytes(原字节)` ＋ `os.utime(p, None)`；复跑后 NC3 只剩自己那一条。
⚠ 之所以看得出来，是因为**逐支读了日志**、没只看汇总行。

### 109.6 门禁

| 时刻（提交 / 日志 birth） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 11:45:57 | — | 内容批（表）`34bcb65` | 1 file，**＋127 / −0** |
| **11:46:02.240** | `s109-gate1.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；44 / 44 / 75 / 87 / 4 / 315 / **2122** / **585** ＋ 8 ＋ 2；路径卫生 **407** 个 `.cs`（不变） |
| 11:48:40 | — | 文档批（专题 ＋ §109 ＋ 登记 #39 ＋ 107 专题）`f672840` | 4 files，**＋315 / −1** |
| **11:48:44.024** | `s109-gate2.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；读数与上一跑**逐项一致** |
| 11:51:57 | — | 记忆批（两条新教训 ＋ 析出 §23 ＋ 快照 `27a`）`7e0aeaa` | 2 files，**＋199**；`MEMORY.md` 7961 → **7967** 码元 |
| **11:52:01.762** | `s109-gate3.log` | 门禁 → 🩸 **红（环境）** | `GATE_EXIT=1`。红点是 `HmiEditor.Device.Tests.T113SoBuilderTests.ResolveContext_MissingSdk_ReportsMissingItems` 抛 **`System.IO.IOException : No space left on device : '/tmp/sdk-…/generated_hmi.c'`** |
| **11:53:31.252** | `s109-gate3b.log` | 门禁 → **复跑** | `GATE_EXIT=0` / **7 步全绿**（849 行完整跑完）、0 警告 0 错误；读数与 gate1 / gate2 逐项一致 |

时序：`34bcb65` **11:45:57 → 11:46:02.240**（晚 **5.2 s**）／
`f672840` **11:48:40 → 11:48:44.024**（晚 **4.0 s**）／
`7e0aeaa` **11:51:57 → 11:52:01.762**（晚 **4.7 s**）。🚦 三处均**清晰**（提交后立刻取 birth，没撞同秒）。

🩸 **gate3 首跑那支红要如实留档，且它的归属是「环境」不是「被测对象」**：
就地量了 `df -h /tmp` ⇒ 本机 `/tmp` 是 **10 MB 的 tmpfs**（`/` 尚有 179 G 可用），
大队把 8 个测试程序集**并行**跑，瞬时写爆 ⇒ 与代码 / 文档无关。
判据是「**根因直指路径**（`No space left on device` 后面跟着具体文件）＋
**同一条命令复跑即绿**」——**不是**「有一次绿就算过」（那是本项目记过的错法）。
⚠ 这条属**环境限制**，未做任何处置（改 `TMPDIR` 会改变门禁语义，影响全仓 ⇒ 另开一刀才动）。

⚠ **`App.UiTests` 584 → 585（＋1）**：本轮**没新增用例方法**，多出的 1 是
`HostKeys()` 这个 `MemberData` 的**数据行**（＋1 个宿主）。
与阶段 107 的 ＋4（3 Theory ＋ 1 表卫生）形状不同 —— 记下来，免得下次对不上账。

⚠ **记忆批的提交面 ＝ 析出文档 ＋ 快照**（`.workbuddy/` 整体被 `.gitignore` 忽略
⇒ `MEMORY.md` 与当日日志**进不了提交**）。

🩸 按**自指边界**：回填批自己那一跑（`s109-gate4.log`）的读数记在**当日日志**、
**不回写本表** —— 否则回填批每写一行，都要再为「记录上一批」新开一批。

### 109.7 诚实边界

1. **本层不声称「接线是对的」**，只声称「行为与声明一致」。
2. 🩸 **这 26 条里 6 条的字段是「面板上有、其实不生效」**（`ChkAutoHide` / `ChkAnimations` /
   `ChkShowRulers` / `ChkLivePreview` / `ChkToolTips` / `ChkPreviewOverlay`，
   都在 `ConfigFieldInventory` 的 `AppSurface` 清单里）。表记成 `Moves` **是对的** ——
   判的是「接线真把值写进了 `SettingsService`」，与「下游消不消费」**正交**。
   ⚠ 反过来：**本层绿 ≠ 这个设置项有效**。混起来就会读出一个假的闭环。
3. **探针残余是名字级**（§109.2）⇒ −33 里 8 个是口径顺带。
4. `BtnApply` 的 `Inert` 记的是「**处理器跑了但什么都不做**」、不是「没接线」；
   `Inert` 的 `Why` **仍免证**（阶段 108 的教训）。
5. **仍剩 25 个视图 / 113 个名字**，最大的是 `ProjectSettingsView`(15)、
   `CommunicationConfigView`(14)、`PropertyPanelView`(10)、`TrendConfigView`(10)。
   `TrendConfigView` / `SimulatorView` / `AiChatPanelView` 带 500 ms 定时器或 `DataLogger`
   ⇒ 仍**刻意没进**。**软件侧仍未清空**（#39、#45）。

## 阶段 110（2026-09-27）—— 第五层的第二个「设置面」宿主：`ProjectSettingsView`（登记 #39 继续收残余）

> 前置：阶段 107 专题（本层的形状与三处宿主）、阶段 109 专题（第一种设置面 `SettingsDialog`）。
> 本阶段**产品代码 0 行改动**，只动判据侧一个文件（**＋103 行**）。

### 110.1 要回答的是「值是不是当场落进模型」

阶段 109 证明的是「Probe 能读服务对象、骨架可复用」；本阶段回答另一半：
**界面上那个控件的值，是不是当场就落进模型**。`ProjectSettingsView` 里有一条**标脏**路径
（10 个 `ChkXxx` 走 `AttachChangeHandler`，只写 private `_hasChanges`，值到 `SaveToProject()`
才被 `ChkXxx.IsChecked ?? 默认` 读走）与一条**草稿**路径（用户表改的是 `_runtimeUsersDraft`，
`SecurityConfig.RuntimeUsers` 只在 `ApplyInternal()` 里被整体替换）。

| 维度 | 109 `SettingsDialog` | 110 `ProjectSettingsView` |
|---|---|---|
| 数据面在哪 | 服务对象属性 | **工程模型**（`HmiProject` 的 5 个子配置）＋ 窗口列表 |
| 一次点击改几个分量 | 恰好 1 个 | 10 个复选框**各自 0 个**；`BtnApply` 一次改 **11 个** |
| 「写」是否立即发生 | 是 | 🩸 **不是**（两条延迟路径） |
| 面 / `CheckBox` | 26 / 19 | **19 / 11** |
| 窗口里有状态栏吗 | 没有 | 没有 |
| 夹具改动 | 0 行 | **0 行** |

⇒ 判据骨架**仍然一行没动**（`Probe` / `Bucket` / `ClickSpec` / `Check` / `CollectNamedButtons` /
`BucketTables_*`），新增的只有 Probe 读法与**桶的处置口径**。

### 110.2 读数（−15，🩸 **全部是真覆盖**、顺带 0）

| 量 | 阶段 109 | 阶段 110 | 差 |
|---|---|---|---|
| 面（逐视图去重求和） | 307 | **307** | 0 |
| 五层判据提过名字的 | 194（63%） | **209（68%）** | ＋15 |
| 残余 | 113（36%） | **98（31%）** | **−15** |
| 残余涉及视图 | 25 | **24** | −1 |

逐视图只有一格动：`ProjectSettingsView` **15 → 0**，其余 23 个视图**一格未动**。
🩸 **顺带为 0** —— 这 15 个名字**每一个都只在 `ProjectSettingsView` 里具名**
（逐个查过全库 43 个 `.axaml`）⇒ 不存在阶段 109 那种「同名按钮被顺带计入」的偏差。
对照：阶段 109 的 −33 里真覆盖 25、顺带 8。⚠ **顺带为 0 是运气**（这批名字恰好不跨视图重名），
不是口径变准了。探针 ⇒ `.workbuddy/tmp/s107-face4.py`；差额 ⇒ `.workbuddy/tmp/s110-cover-diff.py`。

⚠ 读法再钉一次：探针打印的 `Views/Foo.axaml（N）` 是**残余数、不是面**。本视图**面 19 / 残余 15 → 0**
（差 4 ＝ `BtnOk` / `BtnCancel` / `BtnTitleClose` / `BtnApply` 早在 107～109 就被别的宿主提过）。
运行期面实测也是 **19**，与探针一致。

### 110.3 表（19 条 / Probe 14 个）

| 桶 | 条数 | 明细 |
|---|---|---|
| `Moves` | **3** | `BtnAddRuntimeUser` / `BtnApply` / `BtnRemoveRuntimeUser` |
| `Inert` | **13** | 10 个「只标脏」复选框 ＋ `ChkRuntimeUserEnabled` ＋ `BtnSetRuntimeUserPassword` ＋ `BtnApplySizeToAll` |
| `Never` | **3** | `BtnOk` / `BtnCancel` / `BtnTitleClose` |

Probe **14 个**（10 个 bool ＋ `用户草稿列表` ＋ `用户·模型` ＋ `屏宽` / `屏高`），
**无 Voice 分量**（本宿主没有 `StatusText` 具名控件 —— 与 `SettingsDialog` 同因）。
全表读数：**宿主 5 个；按钮 76 条（Moves 40 / Inert 27 / Never 9）**（权威行从 `--logger detailed` 抄；
逐宿主 19＋7＋4＋26＋20＝76，桶逐项相加亦相符）。
⚠ **原文写「宿主 6 个」，阶段 111 据实测更正为 5** —— 同句的逐项清单**本来就是五项**（19＋7＋4＋26＋20）；
依据：阶段 111 的判据权威行是「宿主 6 个」（本轮只**新增 1 个**宿主）⇒ 本轮之前为 5。
登记表 `#39` 与阶段 110 的提交信息同错，更正与归因详见专题 §111.8。

夹具初态：`写合并=1 崩溃转储=1 重启=1 调试符号=0 注释=1 合并资源=1 LGPL=1 审计=1 自动登出=1 防篡改=0
用户草稿列表=0 用户·模型=0 屏宽=1024 屏高=600`。
🩸 这 10 个 bool 的期望值**直接取决于 `HmiProject` 的默认值** ⇒ 默认哪天翻了，这 10 条应当变红
（与阶段 109 那 19 条同理：把默认值变化变成可见事件）。

### 110.4 🩸 两段式声明：10 条 `Inert` ＋ 1 条 `Moves` 合起来才是一个完整命题

| 段 | 声明 | 说出的事 |
|---|---|---|
| ① | 10 条 `Inert`（各自 0 个分量动） | 「点下去**确实什么都没写**」—— 事实，**不是**「没接线」 |
| ② | `BtnApply` 一条 `Moves`（10 个 bool 全翻 ＋ `用户·模型` 0 → 1） | 「值**真的被读走**了 ⇒ `_hasChanges` 不是空转」 |

单看任一半都不成立：只有 ① 分不清「标脏」与「线断了」；只有 ② 说不出「不点应用就不落盘」。
反向有实测：**NC3** 给其中一个复选框额外接一条写模型的线 ⇒ ① 当场变红（见 §110.6）。

### 110.5 🩸 三条 `Inert` 的成色各不相同

| 按钮 | 真实成因 | 类别 |
|---|---|---|
| `ChkRuntimeUserEnabled` | 处理器**真跑了**，但改的是**草稿对象**；本层两个用户表出口（列表计数 / 模型计数）都不反映**单个条目的启用位** | 「未被观测」（与 107 的 `BtnSaveAlarm` 同形） |
| `BtnSetRuntimeUserPassword` | 处理器的**第一个分支**就是「`password.Length == 0` ⇒ 只写提示行并 `return`」；夹具里没人往输入框填过东西 | 处理器提前 `return` |
| `BtnApplySizeToAll` | ⚠ **不是没接线**：`foreach (var screen in _project.Screens)` 确实跑了，但夹具屏幕用**同一份** `ScreenDefaultsConfig` 建 ⇒ 写进去的是**同一个数** | 「写入同值」 |

### 110.6 负控 4/4（判红 ＋ 逐支点名 ＋ 写回原文）

| # | 注入（都能编译） | 实测点名 |
|---|---|---|
| NC1 | **两段式后半**断掉：`SaveToProject` 不再读 `ChkMergeResources` | `BtnApply：分量「导出·合并资源」期望 0、实际 1（点击前 1）` **红 ✓** |
| NC2 | **草稿语义**断掉：`SaveToProject` 不再把草稿写回模型 | `BtnApply：分量「用户·模型」期望 1、实际 0（点击前 0）` **红 ✓** |
| NC3 | 给「只标脏」的复选框**额外接一条写模型的线** | `ChkMergeResources：**未声明**的分量「导出·合并资源」从 1 变成了 0` **红 ✓** |
| NC4 | 声明值**写反**（`BtnApply` 的 `导出·调试符号` `1` → `0`） | `BtnApply：分量「导出·调试符号」期望 0、实际 1（点击前 0）` **红 ✓** |

逐支一个日志（`s110-nc1..4.log`）、逐支写回原文并断言 SHA256 与注入前逐字节相同
（产品代码 `a64278a89206f0e9…` 三支一致、判据文件 `9bdcf4a1244a5df8…`），
还原后 `os.utime(p, None)` **刷新 mtime**（阶段 109 的 `copy2` 坑已进模板）。

### 110.7 门禁

| 时刻（提交 / 日志 birth） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 13:06:50 | — | 内容批（本表）`ef9c3f1` | 1 file，**＋103 / −0** |
| **13:06:58.439** | `s110-gate1.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**（849 行完整跑完）、**0 警告 0 错误**；44 / 44 / 75 / 87 / 4 / 315 / **2122** / **586** ＋ 8 ＋ 2；路径卫生「已检查 **668** 个文件」/ **407** 个 `.cs`（**不变** —— 本轮没新增 `.cs`） |
| 13:09:38 | — | 文档批（本专题 ＋ 状态表 §110 ＋ 登记 #39 ＋ 改正 109 专题）`767de87` | 4 files，**＋312 / −1**（本专题 192 行） |
| **13:09:41.701** | `s110-gate2.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**（849 行）；读数与 gate1 **逐项一致** |
| 13:12:16 | — | 记忆批（两条新教训 ＋ 析出 §24 ＋ 快照 `27b`）`2542234` | 2 files，**＋147**（析出 ＋70 行、快照 77 行）；`MEMORY.md` 7967 → **7948** 码元 |
| **13:12:19.463** | `s110-gate3.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**（849 行）；读数与 gate1 / gate2 **逐项一致** |

时序：`ef9c3f1` **13:06:50 → 13:06:58.439**（晚 **8.4 s**）／
`767de87` **13:09:38 → 13:09:41.701**（晚 **3.7 s**）／
`2542234` **13:12:16 → 13:12:19.463**（晚 **3.3 s**）。🚦 三处均**清晰**（提交后立刻取 birth，没撞同秒）。

⚠ **`App.UiTests` 585 → 586（＋1）**：本轮**没新增用例方法**，多出的 1 是
`HostKeys()` 这个 `MemberData` 的**数据行**（＋1 个宿主）—— 与阶段 109 的 ＋1 同形。
其余读数**全不动**：`Core.Tests` 2122、两本字典 2296、`UnusedBaseline` 1108。
⚠ **记忆批的提交面 ＝ 析出 ＋ 快照**（`.workbuddy/` 整体被 `.gitignore` 忽略 ⇒
`MEMORY.md` 与当日日志**进不了提交**）。

🩸 按**自指边界**：回填批自己那一跑（`s110-gate4.log`）的读数只记在**当日日志**、
**不回写本表** —— 否则回填批每写一行，都要再为「记录上一批」新开一批。

### 110.8 诚实边界 / 未闭环

1. **本层不声称「接线是对的」**，只声称「行为与声明一致」。
2. **探针的「残余」是名字级**（§110.2）⇒ 本轮顺带为 0 是**运气**，不是口径变准。
3. **`Inert` 的 `Why` 仍免证** —— §110.5 那三条成色是**手工读处理器**得出的，写错没有任何东西会响。
4. 🩸 **「取消能整体回滚」只被 NC2 钉住一半**：若有人把用户表从「草稿」改成**直接改模型**，
   只要它顺带改的分量不是已声明的那几个，**本层看不见**。要真钉住，得加一条
   「点 `BtnCancel` 后模型一字不变」的分量 —— 但 `BtnCancel` 会关窗、本层没有「点完还能继续读」的机制 ⇒ **仍缺**。
5. **仍剩 24 个视图 / 98 个名字**，最大的是 `CommunicationConfigView`(14) / `PropertyPanelView`(10) /
   `TrendConfigView`(10) / `UserManagerView`(9)。`TrendConfigView` / `SimulatorView` / `AiChatPanelView`
   带 500 ms 刷新定时器或 `DataLogger`（真写盘）⇒ 仍**刻意没进**。
6. **软件侧仍未清空**：**#39**（本阶段又收了一刀）、**#45**（根因未定）仍开着。

## 阶段 111（2026-09-27）—— 第五层的**第六个**宿主：`CommunicationConfigView` ＋ 「布景」原语（登记 #39 继续收残余）

⚠ 序数**原写「第七个」是错的**：阶段 111 自己的 §111.8 结论是「本轮之前 5 个、只新增 1 个」⇒ 它是**第六个**，第七个是阶段 112 的 `TrendConfigView`。更正依据与成因 ⇒ `docs/2026-09-27-button-effect-trend-face.md` §112.8。

> 前置：阶段 107 专题（本层的形状与首批三处宿主）、阶段 109 专题（第一种设置面）、
> 阶段 110 专题（第二种设置面 ＋ 两段式声明）。
> 本阶段**产品代码 0 行改动**，只动判据侧一个文件（**＋289 / −6**）；夹具 `WindowDepot` **一行没动**。

### 111.1 要回答的是「处理器第一行读输入框」的那一类按钮

前六个宿主的数据面形状换过三轮，但有一个共同点：**点下去要用的现场，夹具里已经躺好了**
（`BtnDeleteAlarm` 的前提是「上一步刚加了一条报警并被选中」—— 那是**上一步点击**造成的）。

`CommunicationConfigView` 不是：它的多数处理器**第一行就读文本输入框**，
或者看 `ListBox` 的选中项 —— 而**选中是 `ListBox` 的行为、不是按钮的行为**，本层够不到。

| 维度 | 107～110 六个宿主 | 111 |
|---|---|---|
| 处理器第一行读什么 | 模型 / 选中项（点击造成） | **输入框**（没人填过）／**列表选中**（没有按钮能装） |
| 面 / `CheckBox` | 20 / 7 / 4 / 26 / 19 | **15 / 2** |
| 需要新机制吗 | 不需要 | 🆕 **需要（布景）** |

⇒ 不加机制，15 条里 **11 条**只能记成「输入框是空的 ⇒ 提前 return」那种 `Inert` ——
它绿着，但**把 `Click +=` 整行删掉也照样绿** ⇒ 对这一层是零信息。

### 111.2 面：运行期 **15** vs 静态残余 **14** —— 顺带覆盖的**镜像**

| 量 | 本视图 |
|---|---|
| 静态探针给的残余 | **14** |
| 判据跑出来的运行期面 | **15** |

差的那个是 `BtnTitleClose`：它在此视图里**确实是**运行期面的一员，但静态探针把**所有**提过
`"BtnTitleClose"` 的视图都算作已覆盖 —— 而这个具名出现在 **28 个** `.axaml` 里（`ChromeWindow` 统一命名）。

🩸 **这是阶段 109「顺带覆盖」的镜像**：109 让降幅**高估**（−33 里只有 25 真覆盖），本轮让降幅**低估**（掉 14、真覆盖 15）。
两个方向同一性质：**探针分不清「哪个格子是哪个视图的」**。
顺带量化（`.workbuddy/tmp/s111-facecheck.py`，只读）：面 **307** 个格子 / **244** 个不同名字 /
**22 个名字跨视图重名** / **63 个格子靠重名顺带**。

### 111.3 🆕 布景：`ClickSpec.Arrange` / `Arranged(...)`

`ClickSpec` 加一个 init-only 属性 `Action<HostContext>? Arrange`，用 `Arranged(spec, c => {…})` 挂上；
跑的顺序是 `布景 → Pump → before → Gesture → Pump → after → Check` ⇒ 布景造成的变化**不进断言**。
读数里为每条带布景的声明另起一行（`BtnSaveEditSlaveVar 布景 → 变量选中=1`）。

⚠ **它同时是一条新的作弊面**（把效果搬进布景 ⇒ 本层瞎了而表面全绿）⇒ **自带守卫**：
布景前后各读一次全分量，**布景只许动输入面分量**（`Probe.IsInputFace`，新增 `InputNum` 工厂）。
分量于是有三档：**数据面**（`Num`/`Str`，布景改动即红）／**Voice**（只进读数）／🆕**输入面**（可被布景改）。

本表只标**一个**输入面分量 `变量选中`（`BtnSaveEditSlaveVar` 的前提，且本层没有按钮能重新装上它）；
「树选中」**刻意没标** —— 本表不需要布景动它，于是它留在严格一侧。

### 111.4 表（15 条 / Probe 12 个）

| 桶 | 条数 | 明细 |
|---|---|---|
| `Moves` | **10** | `BtnAddConnection` / `ChkConnEnabled` / `BtnAddSlave` / `BtnSaveSlave` / `BtnAddSlaveVar`★ / `BtnSaveEditSlaveVar`★ / `BtnDeleteSlaveVar` / `BtnBatchAddSlaveVars` / `BtnApplyBatchVars`★ / `BtnDeleteSelection` |
| `Inert` | **1** | `ChkSlaveEnabled` |
| `Never` | **4** | `BtnImportSlaveVars` / `BtnExportSlaveVars` / `BtnTestConnection` / `BtnTitleClose` |

（★ = 带布景）Probe 12 个（11 数据面 ＋ 1 Voice）；夹具初态
`连接=0 从站=0 变量=2 树根=0 树选中=0 从站变量列表=0 变量选中=0 连接启用=-1 从站启用=-1 从站首变量地址=无从站 批量面板=关 状态栏=空`。

三条布景的**布景行**读数：`BtnAddSlaveVar`「（没动任何分量）」／`BtnSaveEditSlaveVar`「变量选中=1」／
`BtnApplyBatchVars`「（没动任何分量）」⇒ 三者**都没有动数据面**。
四条 `Never` 是**三种**理由：平台桩（Import / Export）、🆕**读数会抖**（`BtnTestConnection`）、关窗（`BtnTitleClose`）。
`BtnBatchAddSlaveVars` 只切一个 `Border.IsVisible` ⇒ 新加分量 `批量面板` 把它变成可观测的 `Moves`。

### 111.5 🩸 第一次真红：`从站启用` 漏声明 ⇒ 顺手把**两义读数**改成三档

首跑 7 条红 1 条，点名 `BtnAddSlave：**未声明**的分量「从站启用」从 0 变成了 1`（新子设备默认 `IsEnabled=true`）——
**判据按设计工作**。但成因那侧才是收获：`SlaveEnabled` 当时写的是两档，
**`0` 同时意味着「没有子设备」与「子设备被停用」** ⇒ 读数有两义。
处置：`连接启用` / `从站启用` 都改成**三档**（`-1` = 这一档还不存在），代价是 `BtnAddConnection`
与 `BtnAddSlave` / `BtnDeleteSelection` 各多一条声明。**理由与 `从站首变量地址` 的「无从站 / 无变量」相同。**
🩸 教训：**多付一条声明的钱，比留一个会误导下一轮的读数便宜。**

### 111.6 负控 6/6（判红 ＋ 逐支点名 ＋ 写回原文）

| # | 注入（都能编译） | 实测点名 |
|---|---|---|
| NC1 | 接错处理器（`BtnAddConnection` → `OnDeleteSelection`） | `BtnAddConnection：分量「连接」期望 1、实际 0（点击前 0）` **红 ✓** |
| NC2 | 摘掉接线（注释掉 `BtnAddSlave.Click += …`） | `BtnAddSlave：分量「从站」期望 1、实际 0（点击前 0）` **红 ✓** |
| NC3 | 面守卫（表里塞不存在的名字 `BtnNotExist`） | `表里登记了但宿主里找不到这些按钮：BtnNotExist` **红 ✓** |
| NC4 | 未声明分量外溢（删掉「从站启用」声明） | `BtnAddSlave：**未声明**的分量「从站启用」从 -1 变成了 1` **红 ✓** |
| 🆕 NC5 | **布景守卫**（布景直接 `Variables.Add(…)`） | `BtnAddSlaveVar：布景改动了**输入面以外**的分量：变量` **红 ✓** |
| NC6 | 声明值写反（`ChkConnEnabled` 的 `连接启用` `0` → `1`） | `ChkConnEnabled：分量「连接启用」期望 1、实际 0（点击前 1）` **红 ✓** |

逐支一个日志（`s111-nc1..6.log`）、逐支写回原文字符串并断言 SHA256 逐字节相同、还原后 `os.utime` 刷新 mtime。
🆕 NC5 是新机制的负控 —— 按「做不出负控的判据不发布」，布景守卫没有它就**不该上路**。

### 111.7 门禁

| 时刻（提交 / 日志 birth） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 13:41:47 | — | 内容批 `753eb70` | 1 file，＋289 / −6 |
| **13:41:57.180** | `s111-gate1.log` | 门禁（7 步） | 7 步全绿（848 行）、0 警告 0 错误（⚠ 退出码未留档，见下） |
| 13:45:56 | — | 文档批 `bb97583` | 5 files，＋457 / −3 |
| **13:46:07.916** | `s111-gate2.log` | 门禁（7 步） | `GATE_EXIT=0` / 7 步全绿（849 行）、读数与 gate1 逐项一致 |
| 13:52:29 | — | 记忆批 `353ad89` | 2 files，＋129 / −0 |
| **13:52:38.163** | `s111-gate3.log` | 门禁（7 步） | 7 步全绿（848 行）、0 警告 0 错误（⚠ 退出码未留档） |
| **13:54:29.186** | `s111-gate3b.log` | 门禁（7 步，补 gate3 退出码） | `GATE_EXIT=0` / 7 步全绿 |
| — | — | 回填批（本节） | 见当日日志（自指边界） |

时序：`753eb70` 13:41:47 → 13:41:57.180（晚 10.2 s）；
`bb97583` 13:45:56 → 13:46:07.916（晚 11.9 s）；
`353ad89` 13:52:29 → 13:52:38.163（晚 9.2 s）。🚦 三条链都清晰。

🩸 **流程瑕疵**：`gate1` / `gate3` 退出码**未留档**（「半吊子 bash」：`$?` 仍由 fish 解析 ⇒ 报错不追加）。处置：`gate3` 那跑后树**没动** ⇒ 另开 `gate3b` 原地复跑补证；`gate1` 那跑后树**已走** ⇒ 不许冒充，改用**包含关系**（`gate2` 在包含内容批改动的树上跑的、`GATE_EXIT=0`）。
⚠ **848 行 vs 849 行**：差的是 `GATE_EXIT=0` 打在日志内 / 外。**统一口径**（`.workbuddy/tmp/gate-diff.py`）
对拍：`s110-gate4` vs `gate1` **749 vs 749、差集 1/1**（唯一那条＝`App.UiTests` 586 → 587，已归因）；
`gate2` vs `gate1`、`gate3` vs `gate3b` 均 **0/0 完全一致**。
⚠ **更正**：文档批曾写「多重集 838 vs 838 完全一致」—— 那是**另一套归一**的数（跨口径不可比），且漏掉了那 1 条真差异。
⚠ **`App.UiTests` 586 → 587**：仍是 `HostKeys()` 的**数据行**（＋1 个宿主）。
其余读数全不动（`44 / 44 / 75 / 87 / 4 / 315 / 2122 / 587 ＋ 8 ＋ 2`、`Core.Tests` 2122、两本字典 2296、`UnusedBaseline` 1108）。
🩸 回填时新得两条教训（退出码没落日志的**马甲形态** ＋ **报一致要报口径**）⇒ 析出 §25.7。

### 111.8 ⚠ 更正：第五层宿主数 **6 → 5**

阶段 110 在**三处**写下「全表：宿主 **6** 个」：登记表 `#39`、状态表 §110.3、**提交信息 `ef9c3f1`**。
实测：本阶段判据权威行是「宿主 **6** 个」（本轮只新增 1 个宿主）⇒ **本轮之前是 5 个**。
🩸 它是**自相矛盾**而非算错 —— 同句的逐项清单 `19＋7＋4＋26＋20＝76` **本来就是五项**。
成因**推测**为「邻近借用」：同一张表的上一个事实是阶段 50 那句「已对 **6 个宿主**收口」（那是 `DialogInner` 那一层）。
处置：登记表与状态表**就地更正**（带 ⚠ 注记）；提交信息不可改 ⇒ 只在专题里点名，给后来查 `ef9c3f1` 的人一个落点。
🩸 教训：**手写的合计数必须与同句的逐项清单相加核对**（清单是数出来的，合计是抄来的）。

### 111.9 诚实边界 / 未闭环

1. **本层不声称「接线是对的」**，只声称「行为与声明一致」。
2. 🩸 **`Probe.IsInputFace` 是声明、没有判据验证它标得对不对** —— 判据只能证明「布景没动**未声明为输入面**的分量」，
   不能证明「输入面的划分合理」；误标一条数据面分量即放行。这条盲区**自陈**。
3. **探针残余是名字级**（§111.2）⇒ 本轮残余掉 14、真覆盖 **15** ⇒ 读数**低估 1**。**残余掉了几个 ≠ 收了几个按钮。**
4. **两条新增自陈盲区**：`TxtConnName.LostFocus → PersistConnectionName` 不在按钮面内；
   `BtnDeleteSelection` 是**三分派器**、而「一个按钮名只能有一条声明」⇒ 本层天然只覆盖**一条支路**
   （这是**表形状的结构性边界**，不是本宿主特有）。
5. **「取消后模型一字不变」仍无判据**（阶段 110 §110.8 第 4 条）—— 本轮没动它。
6. **仍剩 23 个视图 / 84 个名字**，最大的是 `PropertyPanelView`(10) / `TrendConfigView`(10) /
   `UserManagerView`(9) / `AiChatPanelView`(8)。带 500 ms 定时器或 `DataLogger` 的三个仍**刻意没进**。
7. **软件侧仍未清空**：**#39**（本阶段又收了一刀）、**#45**（根因未定）仍开着。

## 阶段 112（2026-09-27）—— 第五层的**第七个**宿主：`TrendConfigView`（登记 #39 继续收残余）

> 前置：阶段 107 专题（本层的形状与首批三处宿主）、阶段 109（第一种设置面）、
> 阶段 110（第二种设置面 ＋ 两段式声明）、阶段 111（第一种「输入面驱动」＋ 布景原语）。
> 本阶段**产品代码 0 行改动**，只动判据侧一个文件（**＋191 / −1**）；夹具 `WindowDepot` **一行没动**
> （`Trends` 早在阶段 68 就躺好了）。

### 112.1 为什么是它：第一个「一个处理器都没被调用」的宿主

前六个宿主点下去**至少会走进某个处理器**（接错了是另一回事）。这一个不是：
**12 个具名按钮里有 6 个的事件订阅整段缺失**。三条依据：XAML 里只有两个 `On*` 事件属性；
code-behind 的 `+=` 只有 5 行（全是 `Click`）；那 6 个处理器**全仓零引用**。
对照 `ScreenManagerView` / `PropertyPanelView` 都在用 `IsCheckedChanged="OnPublicChanged"`
⇒ **模式存在，是这个视图丢了订阅**。用户可见后果与处置 ⇒ 新登记 **#65**（本层只记事实，不修）。

### 112.2 🩸 量的那一遍：初版措辞是**过头话** —— 「面看不见缺口」的又一例

初版宿主注释写「补上订阅之后这 6 条会当场变红」。**不成立**：原来的 4 个探针读的全是
「**条数**」（趋势数 / 笔数 / 两个列表长度），而那两个处理器写的是**布尔位**
（`ShowLegend` / `ShowPoints` …）⇒「处理器写了模型」与「处理器没接线」在读数上**完全同形**，
判据原理上看不见区别。⇒ 那 6 条 `Inert` 在补探针之前是**零信息**的。
处置：**先补探针、再谈红不红**。顺序反了，补完订阅判据照样绿 ⇒ **假闭环**。

### 112.3 本批做了什么

- 🆕 **6 个「显示位」探针**（表里 Probe 4 → **10**）：`关图例趋势` / `关网格趋势` /
  `关X轴趋势` / `关Y轴趋势` / `开显示点笔` / `停用笔`。三条设计决定：① 数「**关着的 / 停用的**」
  ⇒ 初值天然全 0（夹具与新建的默认值一致；反过来数会让 `BtnAddTrend` 多出四条无关声明）；
  ② **全工程累计**（`_currentTrend` 是私有字段，且 AddTrend/DeleteTrend 会换人）；
  ③ **一条声明都不读** ⇒ 一动就报「未声明分量外溢」。
- 🔀 两条笔 `Inert` **挪到 `BtnAddPen` 之后**：`OnPenPropertyChanged` 首行判 `_currentPen == null`，
  而装上它的唯一活路就是 `BtnAddPen` ⇒ 它们是「未接线 ＋ 前提」的**合取**，
  顺序在这里**为负控服务**（排到表尾会被 `BtnDeletePen` 清空 ⇒ 负控 NC7 失效）。
- ⚠ 把「修好就红」改成**带前提**的说法，并给 `Bucket.Inert` 的「④ 事件从未订阅」补上同一条前提。

### 112.4 表与读数

| 项 | 读数 |
|---|---|
| `TrendConfigView` | 面 **12** / 声明 **12**（`Moves` 4 / `Inert` 6 / `Never` 2）/ Probe **10** |
| 全表 | 宿主 **7** 个；按钮 **103** 条（`Moves` 54 / `Inert` 34 / `Never` 15），带布景 3 条 |
| 定向测试 | **8/8 通过**，8 条 `Moves` / 6 条 `Inert` 与声明**逐条相等**（首次加宿主首跑即全绿） |
| 负控 | **8/8**，每支只红 1 个用例（`通过数 7 / 失败数 1`） |
| 残余面 | **84 格 / 76 名字 / 23 视图 → 74 格 / 66 名字 / 22 视图** |

`Never` 两条：`BtnToggleRecord`（**读数会抖** —— `DataLogger` ＋ 500 ms 定时器；
⚠ 本视图在阶段 111 §111.9 被列入「带定时器 ⇒ 刻意没进」，本轮解禁的依据是那个定时器的回调体判
`_isRecording`，而记录入口恰是不点的 `BtnToggleRecord` ⇒ **不点就不会 `new DataLogger`、不会写盘**）、
`BtnTitleClose`（会关窗）。

🩸 **残余只掉 10，而运行期真覆盖 12** —— 差的 2 个用 **`git show HEAD:` 的五个守卫文件**逐名字核实：
`BtnTitleClose`（第三层 `DialogInnerInvocationUiTests` ＋ 第五层旧宿主）、
`ChkShowGrid`（第五层旧宿主 `SettingsDialog` 那条 `Interaction.SetShowGrid`）。
⇒ **名字级口径的顺带**，两个方向都要能解释（阶段 109 那次是让降幅**高估**）。

### 112.5 负控 8/8：后三支是本阶段**主张自己的证伪器**

前五支沿用剧本（接错处理器 / 摘接线 / 面守卫 / 未声明分量外溢 / 声明值写反）。
后三支钉的是本阶段**特有**的两句话：

- **NC6** 给 `ChkShowLegend` 补订阅 ⇒ `**未声明**的分量「关图例趋势」从 0 变成了 1`；
- **NC7** 给 `PenShowPointsCheck` 补订阅 ⇒ `**未声明**的分量「开显示点笔」从 0 变成了 1`；
  ⚠ 这两支同时钉住 **「先补探针、再补订阅」这个顺序** —— 注入只加订阅、不动探针，
  能点红完全是因为 6 个探针已在位；**在量的那一遍之前，同样的注入是绿的**。
- **NC8** 拿新探针做**布景** ⇒ `布景改动了**输入面以外**的分量：关图例趋势`
  （证明新探针确实进了布景守卫的读数面）。

### 112.6 🩸 顺带更正：阶段 111 的**序数错**（`第七个` → 应为**第六个**）

阶段 111 自己 §111.8 的结论是「本轮之前 5 个、只新增 1 个」⇒ 它是**第六个**，本阶段才是第七个。
同一个错序数传了 **4 处**：专题标题、`project-status` §111 标题、登记表 #39 单元格（**3 处活文档
已就地更正 ＋ ⚠ 注**）、析出文档 `2026-09-23-memory-detail-extract.md` §25 标题
（**已落地快照 ⇒ 不手改**，同 §111.8 对不可改提交信息的处置）。
成因与 §111.8 那条**同源**：手写的**序数**从邻近事实借了数字 —— 这次连邻近事实都不用找，
正确答案就在同一份文档往下 20 行。🩸 教训扩一条：**合计要相加核对，序数要现场数**；
⚠ **摘要 / 交接笔记里带过来的序数同样不算数**（本轮一开始也误记成「第八个」，是现数 `Hosts`
逐项清单 ＋ 运行期表卫生读数才纠正的）。

### 112.7 门禁（四跑；读数 ⇒ 专题 §112.7）

| 批次 | 提交 | 日志 | 结果 |
|---|---|---|---|
| 内容批 | `5ef0394` | `s112-gate1.log` | `GATE_EXIT=0` / 7 步全绿（849 行） |
| 文档批 | `3949c32` | `s112-gate2.log` | `GATE_EXIT=0` / 7 步全绿（849 行） |
| 记忆批 | `9f260b5` | `s112-gate3.log` | `GATE_EXIT=0` / 7 步全绿（849 行） |
| 回填批 | 见日志 | `s112-gate4.log` | **自指边界** ⇒ 只记当日日志、不回写本表 |

读数三跑一致：`44 / 44 / 75 / 87 / 4 / 315 / 2122 / 588 ＋ 8 ＋ 2`，0 警告 0 错误；
对拍（统一口径）`gate2`/`gate3` vs `gate1` 均 **0 差**，与阶段 111 `gate4` 只差 **1 行**
（`App.UiTests` **587 → 588**，＝新增宿主的 `HostKeys()` 数据行）。
⚠ **流程瑕疵已登记**：`gate1` / `gate2` 跑的过程中 `docs/` 在动 —— 七步里**没有一步读 `docs/`**
（逐条核过）＋ 代码测试全程未变 ⇒ 读数成立，另有 `gate2` 的包含关系兜底。规矩：**先跑完门禁再动别的批次**。

### 112.8 诚实边界 / 未闭环

1. **本层不声称「接线是对的」**，只声称「行为与声明一致」。新注脚：**这个「一致」的强度取决于
   探针看得见什么** —— 6 条 `Inert` 从零信息变成有信息，靠的是**加探针**而非加断言。
2. 🩸 **`Probe.IsInputFace` 是声明、没有判据验证（**阶段 111 §111.9 第 2 条，本轮未动**）；
   本宿主**没有布景** ⇒ 这条盲区在本宿主上无新面。
3. **探针残余是名字级** ⇒ 真覆盖 **12**、残余掉 **10**（§112.4 已给出那 2 个的名字与出处）。
4. **面外**：本视图里三处**代码构造**的控件天然不在按钮面内（两个列表项内部的 `chkEnabled`
   —— 它**自己**接了 `IsCheckedChanged`、是活的；颜色与时间窗口两个下拉的 `ComboBoxItem`）。
   不是缺陷，是**面外**。
5. **「取消后模型一字不变」仍无判据**（阶段 110 §110.8 第 4 条）—— 本宿主没有 `BtnCancel` / `BtnOk`
   （它是 `ChromeWindow`，只有标题栏的 X）⇒ 不新增同类。
6. **软件侧仍未清空**：**#39**（本阶段又收一刀）、**#45**（根因未定）、**#65**（本阶段新开）仍开着。
7. **仍剩 22 个视图 / 66 个名字**，最大的是 `PropertyPanelView`(10) / `UserManagerView`(9) /
   `AiChatPanelView`(8) / `AiIconDialog`(7)。⚠ `AiChatPanelView` / `SimulatorView` 仍带刷新定时器
   或真写盘 ⇒ 仍**刻意没进**（要按 §112.4 那条「不点它会不会写盘」的口径逐个核过再说）。

---

## 阶段 113（2026-09-27）—— 第五层的**第八个**宿主：`UserManagerView`（一次由判据逼出来的**产品修复**）

> 前置：阶段 107（本层形状）、109（第一种设置面）、110（第二种设置面 ＋ 两段式声明）、
> 111（第一种「输入面驱动」＋ 布景原语）、112（「④ 事件从未订阅」首次可证伪）。
> ⚠ **本阶段与前六个宿主形态不同**：前六个一律「产品代码 0 行改动」；本次**先建判据取证、
> 再修产品**（8 个按钮补 `Click=`），产品侧 **＋14 / −8**、判据侧 **＋272 / −0**。
> 专题 ⇒ `docs/2026-09-27-button-effect-user-manager-face.md`。

### 113.1 为什么是它 —— 以及量的那一遍的结论为什么是错的

残余面 **74 格 / 66 名字 / 22 视图**，`UserManagerView` 残余 **9 / 面 18** 且那 9 个名字看似一族
（9 枚 `Perm*` 权限复选框），`ChromeWindow` ＋ 夹具 `d.Auth` 现成 ⇒ 零新机制。
**但真读代码后发现，真正的发现在那 9 个名字之外**：同面的 **8 个按钮也零接线**。
它们在**名字级口径**上被第三层逐个登记成 `Quiet` 过 ⇒ **永远不出现在残余里**。
⇒ 🩸「**提过名字就永久豁免**」：残余探针按名字去重，一个缺口只要被任何一层提过一次名字，
残余读数里就再也看不见它。**名字级口径只能排优先级，不能判「覆盖完了」。**

### 113.2 🩸 核心发现：整窗 10 条处理器无订阅点（四路取证）

① `.axaml` 全文只有 **4** 个事件属性（`PointerPressed` ＋ `Click=OnTitleCloseClicked` ＋
两个 `SelectionChanged`）；② code-behind 全文只有 **1** 句 `+=`（运行时新建的重置口令框里的
`confirmBtn`，不是这一页任何一枚）；③ `git log --all -S'Click="OnAddUser"'` **全部历史零命中**，
且引入本文件的 `1fc6701` 里 `.axaml` **一个事件属性都没有** ⇒ **不是删掉的，是生来没接**；
④ **运行期**：第五层首跑时 4 条 `Moves` 声明**逐条**报「声明的分量一个都没动」。
用户可见症状：**这个对话框「能看不能改」**（能刷详情面板 —— 那两个 `SelectionChanged` 是接了的；
增删启停 / 解锁 / 重置口令 / 存角色 / 刷日志 / 清日志一个都不响应）。

### 113.3 处置：修 8 条、不修 2 条

**修**的依据：处理器体**一直被维护**（阶段 97 `#61d` 刚给两处 `catch` 换过带码异常，
`UserAdminIssueLocalizationGuardTests` 把本视图当**活的**消费点在守）；`AuthenticationService`
整条 API 都是为这条路径写的；补接线是机械、可回滚的。
**不修**的两处：9 枚复选框（`OnPermissionChanged` **本身是空壳** —— 首行守卫之后一行都没有，
接上只会把「看不出来」变成「看着像有事发生」；值本就在保存时被 `GetPermissionsFromUI()` 读走）、
`OnDetailChanged`（要先定两条产品口径，且它不是按钮面 ⇒ 本层对它零信息）。

### 113.4 表与读数

| 项 | 读数 |
|---|---|
| `UserManagerView` | 面 **18** / 声明 **18**（`Moves` 5 / `Inert` 10 / `Never` 3）/ Probe **17** / 带布景 12 条 |
| 全表 | 宿主 **7 → 8**；按钮 **103 → 121**（`Moves` 54→59 / `Inert` 34→44 / `Never` 15→18），带布景 3 → **15** |
| 定向 | **9/9 通过**（首跑红 ⇒ 修接线 ⇒ 修正声明 ⇒ 绿） |
| 残余面 | **74 格 / 66 名字 / 22 视图 → 65 格 / 57 名字 / 21 视图**（覆盖 178 → **187** / 244 ＝ 76.6%） |

三条探针设计决定：① 权限位给**一枚具体**（`开放管理用户`）＋ **一枚伞**（`权限位合计` 读**掩码本身**
—— 换成「开着的角色数」会漏掉「拨掉一位」）；② `标题尾`（`ShowStatus` 写的是窗口标题后缀，
本宿主没有 `StatusText`）—— 没它 `BtnRefreshLog` 只能是**零信息**的 `Inert`；
③ 审计日志**拆服务侧 / 面板侧两枚**。两处**顺序即语义**：`BtnRefreshLog` 排第一（只有它看得见
「标题尾 空 → 非空」）、`PermManageUsers` 紧邻在 `BtnSaveRole` 之前（保存读界面当前状态）。

### 113.5 🩸 首跑打脸的两处（按实测修正声明）

① **我搜错了地方**：开工前判「用户管理一条审计都不落」（依据 `RecordAudit(` 全仓只有 MainWindow 在用），
实测 `BtnAddUser` 审计 0→1、`BtnToggleEnabled` 1→2 —— 因为审计有**两级入口**，私有
`AppendAudit(...)` 才是 `CreateUser` / `SetUserEnabled` / `DeleteUser` / `ChangeUserRole` 走的那个。
⇒ 教训：**量「谁在写」要搜写入点（`_auditLog.Add` / 私有 helper），不是搜公开入口名。**
② 于是 `BtnClearLog` 从 `Inert`(③) **转 `Moves`**（`审计面板` 2 → 0 而 `审计条数` 2 → 2）——
这一对分量正好把源码注释「清空**仅指 UI 显示**」钉成可证伪的。
③ 顺带量出两处**绕过服务层**的差分：`OnUnlockUser` / `OnSaveRole` 直接改模型 ⇒ **不落审计**，
而 `BtnToggleEnabled` 改的同样是用户却落 2 条（双向口径把它变成活的）。

### 113.6 负控 10/10（含**第一支「必须保持绿」的反证档**）

NC1 摘接线 / NC2 接错处理器 / NC3 面守卫 / NC4 未声明外溢 / NC5 声明值写反 /
**NC6** 9 枚复选框补订阅 **＋ 补处理器体** ⇒ 9 条 `Inert` 报红 /
**PC1** 9 枚复选框**只补订阅** ⇒ **必须仍然绿**（证明「处理器本身是空壳」；
没有它 NC6 的红有两种解释而分不出来）/ **NC8** 夹具上锁 ⇒ ③ 写入同值报红 /
**NC9** 删掉 `_auditEntries.Clear()` ⇒ 声明值报红 / **NC10** 拿新探针做布景 ⇒ 布景守卫报红。

### 113.7 门禁（四跑）

| 批次 | 提交 | 日志 | 结果 |
|---|---|---|---|
| 内容批（2 提交） | `1a63b56` ＋ `96d7204` | `s113-gate1.log` | `GATE_EXIT=0` / 7 步全绿 |
| 文档批 | `3fa44ed` | `s113-gate2.log`（第 1 跑·**红 1**）＋ `s113-gate2b.log`（复跑） | `GATE_EXIT=1` → **复跑 `GATE_EXIT=0`** / 7 步全绿 |
| 记忆批 | `3b69467` | `s113-gate3.log` | `GATE_EXIT=0` / 7 步全绿 |
| 回填批 | 见日志 | `s113-gate4.log` | **自指边界** ⇒ 只记当日日志、不回写本表 |

内容批读数：`44 / 44 / 75 / 87 / 4 / 315 / 2122 / **589** ＋ 8 ＋ 2`，0 警告 0 错误；
路径卫生各计数与阶段 112 **一字不差**（668 文件 / 40 个有事件声明的 `.axaml` / 130 `.cs` / 407 `.cs`）。
对拍 vs 阶段 112 `gate4`：**真差异 2 行** ＝ `App.UiTests 588 → 589`（＝新增宿主的 `HostKeys()` 数据行）。
🩸 本阶段踩到并修掉一个**流程瑕疵**：第一次跑门禁时退出码只 `echo` 到终端、**没落进日志**
（阶段 112 起日志末行本该是 `GATE_EXIT=0`）⇒ 该次读数**作废重跑**（`s113-gate.sh` 已固化）。
⇒ 教训：**日志要自足** —— 没有退出码的日志不能当「绿」的证据。

**文档批 / 记忆批读数**：两跑都是 7 步全绿、`0 警告 0 错误`、逐程序集逐条同数
（`44 / 44 / 75 / 87 / 4 / 315 / 2122 / 589 ＋ 8 ＋ 2`），路径卫生与内容批**一字不差**
（668 文件 / 130 `.cs` / 407 `.cs`）。**对拍**：`gate2b` vs `gate1` **真差异 0 行**；
`gate3` vs `gate1` **真差异 4 行 —— 全是控制台换行伪差**（同一句「总共 1 个测试文件与指定模式相匹配。」
被 stdout 交错成两行 / 合并成一行，`gate2b` 侧也一样是 4 行）⇒ **非内容差异**。

#### 113.7.1 🩸 门禁里 `#47` 又命中一次 —— 本次能给出**结构面排除**

- **事实**：`gate1` **绿**、`gate2` **红**，而两批之间 `src/` 与 `tests/` **一字未变**（只动 `docs/`）
  ⇒ **同一份二进制、两个结果** ⇒ 差异只可能来自**非确定性**（与阶段 76 那次同形）。
- **红点**：`DispatcherOwnershipContractTests.CrossDispatch_OnlyTheSessionThreadMayHoldTheUIThread`，
  现场 `ownerThread=36 / sessionThread=23` —— 第三个线程持有 `s_uiThread`，正是 `#47` 的「偷手」。
- **复现率（实测，不凭「以前红过」）**：照抄门禁第 `[5]` 步原命令连跑 **6 次全绿**
  （`589/589`，负载 ≈5.2）⇒ 本阶段 **8 次观测 1 红（≈12.5%）**，与 09-23 记的 ≈2/23 同量级
  ⇒ **没看出本阶段把它抬高了**。
- **结构面排除（本次新增的取证手段）**：本阶段 `src/` 侧只改了 `UserManagerView.axaml`，
  该文件**与它的 code-behind 一处 `Dispatcher` 都没有**（新增的判据里也没有任何线程原语）
  ⇒ **原理上不可能是那个「先到者」** —— 抢手动作必须读 `Dispatcher.UIThread` /
  `get_CurrentDispatcher` 才能在窗口里抢走 `s_uiThread`。
  ⇒ 可复用判据：**「本阶段改动面 ∩ 那两个 getter ＝ ∅」⇒ 这条红可直接判为既存偶发**，
  不必逐次做 A/B（本例 N=8 的 A/B 本来也没有分辨力）。
- **处置**：按已知偶发红登记（追加到 `#47` 行）＋ 复跑取绿读数（`s113-gate2b.log`，`GATE_EXIT=0`）。
  ⚠ **不主张修好** —— 登记行自带纪律「连续多次不红**同样不构成**修好的证据」。
- ⚠ **流程纪律**：第 1 跑那个**红读数本身有效**（它证明 `#47` 命中），不能因为复跑绿了就把它删掉
  ⇒ **红读数与绿读数都要记**。

### 113.8 诚实边界 / 未闭环

1. ⚠ **`PC1` 这一档没有判据兜底**：它证明的是一句对**现有实现**的解释，
   实现一改它就**静默失效**（要收它得让判据去查「处理器体是不是空的」—— 那是另一条源码面）。
2. ⚠ **`Inert` 的 `Why` 仍免证**（阶段 110 / 112 同一条）。
3. **面外**：三个 `ListBox` 的行内控件与 `DetailDisplayName` / `DetailRoleCombo` 不是按钮。
4. **仍未闭环**：`#39`（又收一刀）、`#45`、`#65`（阶段 112 新开，**仍未修**）、
   **`#66`（本阶段新开：8 条已修 ＋ 2 条待决）**。
5. **仍剩 21 个视图 / 57 个名字**；最大的 `PropertyPanelView`(10) / `AiChatPanelView`(8) 都是
   `UserControl` ⇒ 要给本层加「非窗口宿主」的装配机制才能进。

---

## 阶段 114（2026-09-27）—— 收口登记 `#65`：`TrendConfigView` 补回 10 处订阅（由**补订阅本身**撞出一条回归）

> 前置：阶段 107（本层形状）、112（第七个宿主 `TrendConfigView`：第一次出现「④ 事件从未订阅」，
> 并为此补了 6 个显示位探针）、113（第八个宿主，先取证再修）。
> ⚠ **本阶段形态与前两个都不同**：112 **只记事实**（产品 0 行）；113 **由判据逼出产品修复**；
> 本次是**收口一个已登记项**，而收口动作**自己撞出一条产品回归** —— 判据第一次以**回归网**身份兑现。
> 专题 ⇒ `docs/2026-09-27-trend-config-event-wiring.md`。

### 114.1 选它的理由**不在残余列表里**

阶段 113 末残余 **65 格 / 57 名字 / 21 视图**。摆在面前两条路：继续加宿主（收残余），
或修已登记的真缺陷。🩸 `TrendConfigView` **不在残余里**（阶段 112 建宿主时它的 12 个名字就都进了
判据文件 ⇒ 名字级口径算它「已覆盖」），但登记 `#65` **仍开着** ⇒ 先修它。
⇒ 教训：**「照残余读数排优先级」必须叠一层「登记表里还开着哪些真缺陷」**，否则真缺陷永远在列表外。
（这是阶段 113「提过名字就永久豁免」的**另一面用法**。）

### 114.2 🩸 量的那一遍：登记把账记小了（6 个控件 → **6 条处理器 × 10 个控件**）

登记写「6 个控件」「修法是补 4 处订阅」。逐行读 XAML 与 code-behind 后实测是 **10 个控件**，
漏的两条是 **`OnPenColorChanged`@`PenColorCombo`** 与 **`OnTimeWindowChanged`@`TimeWindowCombo`**
（两个 `ComboBox`）。它们**上一轮看不见** —— 因为登记表与第五层的面**都只看 `Button` 族**。
🩸 **面的盲区原样变成了登记的盲区**：这比「面的边界 ≠ 没有缺口」更难受的一档 ——
盲区经手**写进了账本**。处置：清点按**处理器**为单位（6 条各自找挂点），不按「面上看得见的控件」。

### 114.3 处置与首跑：先撞出的是**回归**，不是「六条变红」

产品侧（`d010634`）：XAML 补 10 处订阅（**行数中性 343 → 343**）；两处处理器签名从裸 `EventArgs`
抬到 `Avalonia.Interactivity.RoutedEventArgs`（否则 `AVLN3000` —— `IsCheckedChanged` 的事件类型是
`EventHandler<RoutedEventArgs>`，与 `Click` 不是同一个）。

判据侧（`6ce6894`）首跑 **12 条错**，前 4 条**不是**预期的那 6 条：

| 现场 | 分量读数 | 说明 |
|---|---|---|
| **初态**（构造函数里那一遍 `UpdateTrendDisplay()` 就够了） | `关网格趋势` / `关X轴趋势` / `关Y轴趋势` **0 → 1** | **打开这个对话框本身**就把用户第一条趋势的网格 / X 轴 / Y 轴改成「关」 |
| `BtnAddTrend` | `关X轴趋势` / `关Y轴趋势` **1 → 2** | 新趋势被写成 X / Y 关 |

根因：`UpdateTrendDisplay()`（模型 → 控件）**一枚一枚**写那 4 个复选框，而 `OnDisplayOptionChanged`
**一次读全部 4 枚** ⇒ 写到某一枚就触发 ⇒ 把「**控制件当时的残值**」当用户意图写回模型 ⇒
随后几枚从**已被写坏**的模型刷回来（值不再变化 ⇒ 不再触发）⇒ **错误被静默锁死**。
⚠ 被写坏的是**哪几位取决于残值**（＝上一条趋势的值），**形状不固定** ⇒ 只能靠守卫兜。

### 114.4 修法与读数

修法**照作者已有的模式**：`UpdatePenEditor()` 早就有 `_suppressPenEvents`（连 `PenColorCombo.SelectedIndex`
都在窗口内），`UpdateTrendDisplay()` 漏了同款 `_suppressDisplayEvents`。补上后复跑 **9/9 通过**
（初态四个显示位全 0，`BtnAddTrend` 污染消失）。

表：6 条 `Inert` → `Moves`（每处值从处理器体推得，不是抄读数）；`BtnDeletePen` **2 → 4 个分量**
（＋`开显示点笔`=0、`停用笔`=0 —— 它删的正是前两条刚改过的那支笔，两对分量一起把
「`_currentPen` 指的是哪一支」钉死）。全表：宿主 **8**；按钮 **121**；`Moves` 59 → **65** /
`Inert` 44 → **38** / `Never` 18；`TrendConfigView` 桶 **`Moves` 10 / `Inert` 0 / `Never` 2**。
🩸 **残余面 65 格 / 57 名字 / 21 视图 —— 与阶段 113 末一字不变**：名字级口径里它**从来不在残余里**
⇒ 修好也不动。「提过名字就永久豁免」**第三次**现身（109 让它**高估**降幅、111 让它**低估**残余、
本次**完全无反应**）。

### 114.5 负控 8/8：两种红的形状 ＋ 一支**证伪面**

NC9 摘抑噪守卫 ⇒ 红（`BtnAddTrend` / `BtnDeleteTrend` 那一对）；NC10–NC15 逐枚摘订阅 ⇒ 红；
**NC16 摘 4 处面外订阅 ⇒ 绿 9/9**（`PC1` 档：钉「本层看不见它」，不是缺陷）。
🩸 **本阶段新发现的读日志规矩**：**同一处理器管多枚控件时，摘掉非末位那枚的订阅，红点落在「后一枚」
那一步**（后一枚触发处理器时把前一枚的陈旧控件值一起写进模型）⇒ 归因到的是**检测点、不是动手点**，
照日志直接归因会归错；**末位那枚**没有后一枚替它翻账 ⇒ 走判据里另一条自检
「**声明成 `Moves`，但声明的分量一个都没动**」⇒ **点名自己**。
⇒ 两档合起来才完整：**非末位靠外溢、末位靠空声明自检**。

### 114.6 门禁（四跑）

| 批次 | 提交 | 日志 | 结果 |
|---|---|---|---|
| 内容批（2 提交） | `d010634` ＋ `6ce6894` | `s114-gate1.log` | `GATE_EXIT=0` / 7 步全绿 |
| 文档批 | `1477756` | `s114-gate2.log` | `GATE_EXIT=0` / 7 步全绿 |
| 记忆批 | `bd7e4a3` | `s114-gate3.log` | `GATE_EXIT=0` / 7 步全绿 |
| 回填批 | 见日志 | `s114-gate4.log` | **自指边界** ⇒ 只记当日日志、不回写本表 |

**三跑读数逐条同数**：`44 / 44 / 75 / 87 / 4 / 315 / 2122 / 589 ＋ 8 ＋ 2`，**0 警告 0 错误**；
路径卫生 **668 / 40 / 130 / 407** 三跑一字不差。**对拍**（`s114-cmp.py`，剥墙钟后按**多重集**比）：
`gate2` vs `gate1`、`gate3` vs `gate1` **真差异都是 0 行**（849 行对 849 行）；逐行口径的差异**全是
MSBuild 并行构建的清单重排** ⇒ 非内容差异。🩸 三跑在同一份源码上（其间只提交 `docs/`）
⇒ 「纯文档提交不该动读数」得到验证。其中「**无重复接线**」那条与本阶段最相关：本轮往 XAML 加了
10 处订阅，该守卫扫 **40 个 `.axaml`** 全过 ⇒ **没有把同一个事件接到两处**。

### 114.7 诚实边界 / 未闭环

1. ⚠ **4 处订阅判据看不见**（两个 `ListBox` ＋ 两个 `ComboBox`）：面只数 `Button` 族 ⇒
   **谁删掉它们，门禁与判据都不会红**（NC16 把「看不见」钉成事实，但看不见就是看不见）。
   要收它得给本层加「非 `Button` 面」的读数机制。
2. 🩸 **`OnTimeWindowChanged` 是 6 条处理器里唯一没有抑噪守卫的**，且它的程序性写入点落在抑噪窗口
   **之外** ⇒ **同形未发**（今天写入值恰好等于模型值所以无害，但形状与上面那条回归一模一样）；
   另：`TimeWindowMs` 若**不在**那 4 个选项里（如工程文件里是 `45000`）⇒ 循环一个都不匹配 ⇒
   下拉**保持上一条的选中项** ⇒ **控件显示的值不是模型的值**，且用户点同一项**不会再触发事件**去纠正
   ⇒ 新登记 **`#67`**（本阶段**不动手**：超出 `#65` 范围）。
3. ⚠ **`Inert` 的 `Why` 仍免证**（阶段 110 / 112 / 113 同一条）。
4. **仍未闭环**：`#39`（本阶段又收一刀）、`#45`、`#66`（2 条待决）、**`#67`（本阶段新开）**。
5. **仍剩 21 个视图 / 57 个名字**；最大的 `PropertyPanelView`(10) / `AiChatPanelView`(8) 都是
   `UserControl` ⇒ 要给本层加「非窗口宿主」的装配机制才能进。

## 阶段 115（2026-09-27）—— 第五层把面从 `Button` 族扩到 `SelectionChanged` 一族（＋首次引入**静态守卫**）

> 前置：阶段 107（本层形状）、111（布景原语）、112（第四种成色「事件从未订阅」）、113（先取证再修）、
> 114（收口 `#65`，并留下两条尾巴：`#65` 尾巴④「4 处订阅判据看不见」与 `#67`「`TimeWindowCombo` 不在面内」）。
> ⚠ **本阶段不加宿主**（仍是 **8** 个）：改的是**面本身**。前面七刀的路径是「加一个宿主、收一刀残余」，
> 收的是**名字级残余**（`#39` 那本账）；本阶段收的是**判据形状的缺口** ——
> 两条路的账**不通用**，本阶段结束名字级残余**一格没动**（见 §115.7 第 8 条）。
> 专题 ⇒ `docs/2026-09-27-button-effect-selection-face.md`。

### 115.1 为什么是它：上一轮那两条尾巴是**同一个根**

`#65` 尾巴④ 与 `#67` 说的都是「不在面内 ⇒ 接与不接本层都零信息」，根都是「面只有 `Button` 族」。
于是本阶段把面扩到「**具名 且 订阅了 `SelectionChanged`** 的非 `Button` 控件」。
🩸 阶段 114 那句话（「**面的盲区原样变成了登记的盲区**」）在这里得到正面回应：
**先补面，再谈登记表**；面不补，账本里的空白永远填不上。

### 115.2 面：三个口径，别混；第二档手势 `-1`

| 口径 | 数 |
|---|---|
| 选择族**声明条数** | **49** |
| **按宿主去重**的控件数（⬅ 与静态面对齐） | **45** |
| 全仓按**名字**去重 | **42**（`AlarmList` / `GroupList` / `CategoryList` 等跨宿主同名） |
| 面外（`Views/` 下非宿主视图） | **16 / 64** → 🩸 **17 文件 / 65 控件**（阶段 116 修正；`Views/` 共 44 个 `.axaml`） |

全表：宿主 **8**；声明 **170 条**（`Moves` 97 / `Inert` 55 / `Never` 18），带布景 **17** 条。

🩸 **三个口径我自己先混了一次**：先按「31 ＋ 14 ＝ 45 控件 / 49 条声明」改下界那句话，
再看到守卫报「45 / 45」，两个数看着矛盾 —— 其实一个数**控件**、一个数**声明**。
处置不是改数字，而是**让混淆结构上不可能**：守卫逐宿主读数行同时打印两个数
（`面 19 / 声明 20 条（按宿主去重的控件 19 个）`），汇总行三口径并列，
且 `SelectDeclarations()` 的 xmldoc 把 **49 / 45 / 42** 都写死在里面。

🆕 **手势第二档 `-1` = 清空选中**（`ListBox` / `ComboBox` 走 `SelectedIndex = -1`；
`TreeView` 必须显式 `SelectedItem = null`）。为什么非要它：`RuntimeUserList` 全程只有一条条目
（拨同一下标不引发事件）、`CommTree` 的选中只能靠根节点下标驱动。⚠ **诚实边界**：
`-1` **不是**鼠标手势（单选列表上点已选项不会取消选中），它是**程序性**清空，生产里走得到
（删掉最后一条 ⇒ `SelectRuntimeUser(null)` / `ShowEmptyState`）。表卫生的合法域相应放宽到 `-1 或 ≥ 0`。

### 115.3 🆕 静态守卫：新增 `SelectionFaceClassificationGuardTests`（753 行 / 4 条）

「宿主里每个带 `SelectionChanged` 接线的具名非 `Button` 控件都必须被分类」**运行期算不出来**
（不暴露订阅者名单，「拨一下什么都没变」与「没接线」同形）⇒ 只能静态守。

| # | 判据 | 守什么 |
|---|---|---|
| 一 | `EveryWiredSelectControl_InAHost_IsClassifiedInTheRuntimeTable` | 面内**双向闭合**：新控件静默滑进面 / 声明腐烂或订阅被摘，**都红** |
| 二 | `WiredSelectControls_OutsideTheHosts_MatchTheRegisteredCensus` | 面外**点名**（按「文件 ＋ 个数」登记） |
| 三 | `TheScanner_SeesAllThreeWiringForms_AndOnlyThose` | 扫描器自检（合成语料，**两侧**都断言） |
| 四 | `TheHelperResolution_IsAliveOnTheRealCorpus` | 载体识别在**真语料**上活着 |

三种接线形态（判据三两侧钉住）：**XAML 元素属性 9 个** / **code-behind `+=` 22 个** /
**辅助函数注入 15 个** —— 相加 46 ≠ 45，`CmbHardwareTarget` 同时被直接接线与注入（去重算 1）。

🩸 **「守卫与被守对象同源」这条铁律的本体，本阶段量到了**：守卫本可以去 grep 那 2500 行里的
`SelMoves("X"` 字面量。我用 Python 那样数了一遍得 **50**，真值是 **49** ——
多的那一行是 **`SelectDeclarations()` 自己的 xmldoc 里那句 `SelMoves("X"`**：
**注释把计数喂饱了**。⇒ 唯一出口 `internal static SelectDeclarations()`，守卫只读它。

🩸 **辅助函数识别必须带类型**：`AttachChangeHandler<T>(T ctl)` 是个**通用变更跟踪器**
（四分支分别订阅 `ValueChanged` / `TextChanged` / `IsCheckedChanged` / `SelectionChanged`，本体只写
`_hasChanges = true`）。不带类型 ⇒ 同一次调用的另外 35 个实参里 **25 个**（16 数字框 ＋ 9 文本框）
变成假缺口（另 10 个 `CheckBox` 属 `Button` 族、被本面排除）。**我最初写的是 35**（把复选框算进去了），
是负控 nc33 **实测点名 25 个**才改对的；三处已按实测更正。

### 115.4 🩸 「面的边界」**第四形态**：处理器**不读**那个控件

| 形态 | 内容 | 出处 |
|---|---|---|
| ① 谓词里没有的东西看不见 | 直通面按 `Exception` 取形 ⇒ 无异常那类原理上看不见 | §27.2 / §18.2① |
| ② 探针没读处理器写的字段 | 声明「选中项」⇒ 摘掉订阅照样绿 | 阶段 115 `SelMoves` 🩸 |
| ③ 探针读错了量 | 读「条数」而处理器写「布尔位」 | §112.3 |
| ④ **处理器不读那个控件** | 拨它确实引发事件，但处理器不看它 ⇒ 原理上无差别 | **§115.4（本阶段）** |

现场：`CommunicationConfigView.CboBatchVarType` 与 `CboBatchVarRegType` 订阅**同一个**
`UpdateBatchPreview()`，而它**只读后者**。⇒ 记 `SelInert`（如实写「处理器不读这个控件」），
并用**反向负控 nc26**（摘掉订阅必须**照样绿**）把「看不见」钉成事实 ——
这里「绿」比「红」信息量大：它证明那条 `SelInert` 记的是**事实**、不是漏判。

### 115.5 顺带修出的产品缺陷：`ScreenManagerView` 重入（🆕 登记 `#68`）

`SelMoves("ScreenList", 1)`（拨到第二块屏）**首跑当场**抛 `ArgumentOutOfRangeException`。
根因：`OnSelectionChanged()` 里那句 `CboAdaptiveMode.SelectedIndex = targetIndex` 写在
`_isUpdating` 窗口**外面**（与同文件 `OnPublicChanged` 的两句刚好相反）⇒ 真触发
`OnAdaptiveModeChanged` → `RefreshList()` → `ScreenList.Items.Clear()`，而那一下正落在
**`ScreenList` 自己**的选中变化处理**中途** ⇒ 重入清空。
修法（`bfbce65`，产品侧 +16 / −2）：① 首行补 `if (_isUpdating) return;`；② 把那句挪进程序性同步段。
触发路径是普通用户操作（在屏幕列表里点第二块屏）—— 它一直躺在那里，只是**没有判据去看那个控件**。

另有一处**先插桩、再改声明**的取证（`CommTree`）：首跑红时**没有**直接改期望，而是往
`OnTreeSelectionChanged` 插一行日志 ⇒ **7 条记录、没有一条来自那次清空** ⇒ 根因是
`RefreshConnectionList()` 在 `_isUpdatingTree` 窗口里 `Items.Clear()` 重建 ⇒ `SelectedItem` 属性与
Avalonia 内部选择模型**错位** ⇒ 写 `null` 不引发事件。处置：清空那条只登记 `树选中`（**登记事实、
不声称接线**），见证改成「回选 ＋ 布景写 `TxtConnName`」那一对。
🩸 教训：顺序反了就会把**宿主的行为**误判成「我写错了期望」，然后改掉一个本来正确的期望。

### 115.6 门禁（四跑）

| 批次 | 提交 | 日志 | 结果 |
|---|---|---|---|
| 内容批（**3** 提交） | `bfbce65` ＋ `4541d6c` ＋ `d34a3af` | `s115-gate1c.log`（⚠ 见下：`s115-gate1.log` 那次红是**已登记的环境随机红**） | `GATE_EXIT=0` / 7 步全绿 |
| 文档批 | `64da65c` | `s115-gate2.log` | `GATE_EXIT=0` / 7 步全绿 |
| 记忆批 | `2b34a37` | `s115-gate3b.log` | `GATE_EXIT=0` / 7 步全绿 |
| 回填批 | 见日志 | `s115-gate4.log` | **自指边界** ⇒ 只记当日日志、不回写本表 |

**三跑读数逐条同数**：`44 / 44 / 75 / 87 / 4 / 315 / 2122 / **593** ＋ 8 ＋ 2`（合计 **3294** 通过 / 0 失败），
**0 警告 0 错误**；路径卫生 **668 / 40 / 130 / 407** 三跑一字不差（668 个文件；40 个有事件声明的 `.axaml`；
130 个 `.cs` 的面板事件总线口径；407 个 `.cs` 的位置读取/写入口径）。
⚠ 三跑都在**同一份源码**上（其间只提交 `docs/` 与 `.workbuddy/memory/`）⇒ 这 0 差异正是
「**纯文档 / 记忆提交不该动任何读数**」的验证。

🩸 **内容批的第一跑是红的**，而且是一次「已登记的环境随机红」—— 这条单独说，因为下一次撞上它的人
多半会去查 git 身份、配置、磁盘（本阶段就是这么绕了一圈）：`s115-gate1.log`（mtime `20:20:39`）
第 [6] 步红在 `GitWorkflowServiceTests.Commit_ThenHistory_ShowsEntry`，**耗时 50 s ≈ 3 × 15 s**
（`GitProcessWaitMs` 被撞穿），而断言落在**下游** —— `Assert.NotEmpty() Failure: Collection was empty`，
因为 `InitializeRepository` / `Commit` 的返回值**被测试丢弃** ⇒ 撞窗不报自己、只在下游显出「历史是空的」。
**归因证据三条**：① 该用例单独跑 **5/5 绿 / 每次 241 ms**（`s115-gitprobe1.log`）；
② 同一次门禁的步 5 里**同一程序集 2122/2122 全绿**；③ 本阶段改动全在 App / UiTests，与 Core / git 无因果。
**对拍**：重跑时那一批 **551 ms**（`s115-gate1c`），**同一棵树、同一条命令，差 91 倍**；
两次门禁的步 5 读数几乎相同 ⇒ 慢的**不是门禁自身的负载**（是宿主侧那条全局串行的「新路径收费」）。
⇒ **不修**（D 类环境红；窗口调大与调并行度都被 2026-09-17 那轮实测否掉过），
把**指纹**写进 `docs/troubleshooting.md`，并留一条具体两行改法给下一个人。

**对拍**（`.workbuddy/tmp/s115-cmp.py`；先剥墙钟 —— 时间戳**只替换成 `<T>`、不删整行**，
否则会连「这行存在」的信息一起抹掉；再按**多重集**比）：

| 对 | 多重集差异 | 逐行差异 | 归因 |
|---|---|---|---|
| `gate2` vs `gate1c` | **0 行**（812 对 812） | 有 | MSBuild **并行构建的清单重排**（逐行口径只用来分类差异） |
| `gate3b` vs `gate1c` | **0 行**（812 对 812） | 有 | 同上 |

🩸 **本阶段给对拍脚本加了两条「结构性归一」，不加会得到假的 4 行差异。** 步 5 是 8 个项目**并行**
`dotnet test`，它们往**同一个管道**写 ⇒ 偶尔两条固定标记行（`总共 1 个测试文件与指定模式相匹配。`）
被**挤成一行**，粘连点附近还会多 / 少一个空行。不归一时的形态是
「A 独有 **2 行** ＋ B 独有 **1 行**（两条挤在一起）＋ B 独有 **1 个空行**」—— 一读就知道是**并发写者**
的事，不是内容差。处置：① 在**固定标记行前补换行**把它拆回去（只拆结构）；② **空行整体丢弃**
（门禁脚本自己一行空白都不产出，日志里的空行全是工具打的；实测 gate1c 37 个、gate2 38 个）。
⚠ **这不是「放宽对拍」**：真丢了内容，**非空行照样对不上** —— 与「逐行 diff 会把 MSBuild 清单重排
报成差异」同族，规矩仍是**先看多重集差，再看逐行差**。


### 115.7 诚实边界 / 未闭环

1. 🩸 **那 14 条 `SelInert` 是零信息声明**（`ProjectSettingsView` 里「只标脏」的下拉，
   与既有 10 个复选框 `Inert` **同源**：同一个 `AttachChangeHandler` 的 `cmb` 分支）。
   它们的作用是**把静态面闭合**，**不验证行为** —— `_hasChanges` 是 private，
   唯一判它的 `OnOkClicked` 挂在**点了会关窗**的 `BtnOk` 上，而同族的 `BtnApply` 是**无条件**
   `ApplyInternal()` ⇒ 「拨下拉 ⇒ 点 Apply ⇒ 模型变」**见证不到接线在不在**。
   要让它们有信息得走布景 ＋ 给 `BtnApply` 补 14 个模型落点（登记为后续项）。
2. ⚠ **面外 17 个视图 / 65 个控件仍无判据**（阶段 116 修正）：普查表只保证「变动被点名」。
   `PropertyPanelView`（本面 **31**，阶段 114 残余里记 **10** —— 同视图、两口径）是下一刀最该去的地方，
   但它要 `UserControl` 宿主装配机制（与 §114.7 第 5 条同一件事）。
3. ⚠ **已知盲区：跨文件注入** —— 辅助函数的调用点与定义点只在同一个 `.axaml.cs` 内解析。
   实测至少有一处面外站点：`CboResolutionPreview.SelectionChanged += …` 在 `Wiring.Commands.cs`。
4. ⚠ **无 `x:Name` 却接线的元素**不进面（面是「**具名** 且 接线」）；面外实测 **0 个**，写进了读数行。
   另：**动态接线**（反射 / `AddHandler(…, 事件对象)` / `DataTemplate` 绑定驱动）不进面 ——
   实测本仓 `AddHandler` 全部是 Pointer 事件。
5. **`#67` 只被部分兜住**：`TimeWindowCombo` 的**订阅**现在在面内了（摘掉会红），
   但「`OnTimeWindowChanged` 缺抑噪守卫」与「表外 `TimeWindowMs` ⇒ 下拉显示的不是模型值」判不了 ⇒ **仍开着**。
6. **`#66` 的 `OnDetailChanged` 仍零信息**（它挂的是 `TextChanged`，不在本面内）。
7. ⚠ **`Inert` / `SelInert` 的 `Why` 仍免证**、`Probe.IsInputFace` 的取值表仍免证（阶段 110 起的同一条）。
8. 🩸 **名字级残余本阶段一格没动**：仍是 **65 格 / 57 名字 / 21 视图**（阶段 113 末读数）。
   本面与那个面**不可比**（一个数「接线控件」、一个数「具名控件名字」）—— 这个口径警告已写进专题 §115.5。
9. **仍未闭环**：`#39`、`#45`、`#66`（2 条待决）、`#67`；🆕 **`#68`（已修）** ——
   登记它的理由是「它证明本面能抓到**真缺陷**」，不是「它还没修」。

**负控 16 支全过**：nc20–nc26（运行期摘订阅，5 红 ＋ **nc26 必须绿**）、
nc27–nc33（静态守卫 7 支，逐支只红在期望那条腿上）、**nc34 / nc35（补做，见下）**。

🩸 **补做批的起因**：内容批写完后发现注释里三处引用 `负控 NC18 / NC19 / NC20`，但 `.workbuddy/tmp/` 里**找不到这三支的驱动与日志**（`grep -rn "NC1[89]"` 只命中写入该注释的补丁脚本），且行 1806 引的 `NC20` 在 `s115-nc.py` 里是 `SlaveVarList`、**编号撞车** ⇒ 按「每个数字都要能指到一条日志 ＋ 一个时间点」，那两条断言当时**没有证据**。补做结果：**nc34** 摘 `TrendList` 接线 ⇒ `TEST_EXIT=1`、红集 = `TrendConfigView.TrendList` ＋ 下游三条；**nc35** 摘 `TimeWindowCombo` 接线 ⇒ 只红它自己一条；面外宿主两支都为空、`TrendConfigView.axaml` 逐字节还原。⇒ 结论成立、编号改对（提交 `d34a3af`，+3 / −3 只动注释）；第三处**去掉编号**改写成设计理由。
🩸 规矩：**引用「负控 Nx」之前先 `ls` 那个日志 —— 编号不是证据，日志才是。**
🩸 其中**七支静态负控里 `HostButtonEffectUiTests` 全绿**（`通过数` 12 / 11，红的全在守卫类）——
这就是「为什么非要有静态守卫」的直接证据。复原沿用「**跑批前先取基线快照 → 逐支复原 → 逐字节校验**」
（⚠ 脚本第一版把快照取在了注入**之后**，干跑一轮污染了 4 个文件；已修并加了「注入必须真的改变文件」的断言）。

## 阶段 116（2026-09-27）—— 修正阶段 115 的静态守卫：给「扫描面」立契约

> ⚠ **这一刀是修缺陷，不是扩面。** 阶段 115 收口读数「面外 **16** 文件 / **64** 控件」**是错的**，真值 **17 / 65**。

| 项 | 阶段 115 自称 | 真值 | 差因 |
|---|---|---|---|
| `Views/` 下 `.axaml` | 43 | **44** | `Views/Editors/VariablePicker.axaml` —— `Views/` 下唯一子目录文件 |
| 面外视图文件 | 16 | **17** | 同上 |
| 面外接线控件 | 64 | **65** | `Editors/VariablePicker.VarTree : TreeView`（code-behind `+=`） |

**根因三类**：① **枚举口径** —— `Directory.EnumerateFiles(ViewsDirectory, "*.axaml")` 少了
`SearchOption.AllDirectories`，子目录文件**根本没进 `actual`** ⇒ 普查表的双向闭合两个方向都是空的、全绿；
② **范围自陈** —— 「面外 = `Views/` 下非宿主视图」是散文，`Views/` 之外的 3 个 `.axaml` 一字未提；
③ **同仓不一致** —— `SourceScanner.Enumerate`（`tests/HmiEditor.TestSupport`）**本来就是递归的**。

**修法**：App 项目里**每个** `.axaml` 必须被两张表之一认领 —— `Views/` 递归下的归 `OutsideFaceCensus`
（**键改「相对 `Views/` 的路径」**），`Views/` 之外的逐文件归**新增的** `OutsideViewsAxaml`；
枚举收成唯一出口 `ViewAxamlFiles()`；**新增判据五** `EveryAxamlInTheAppProject_IsInsideTheDeclaredScanFace`。

**修后读数**（`s116-readings.log`）：

```
App 项目共 47 个 .axaml：Views/ 递归下 44 个（扫描面） ＋ Views/ 之外 3 个（逐文件登记）；登记表 3 条
面外共 17 个视图文件 / 65 个接线控件；普查表 17 条 / 65 个
扫描面：Views/ 下**递归**枚举到 44 个 .axaml（直系 43 ＋ 子目录 1）
Editors/VariablePicker：接线控件 1（XAML 0 / code 1 / 注入 0）  →  VarTree
```

判据类 **5 条**：4 条原样（判据一 / 三 / 四读数一字未变）＋ 判据五新增。

**负控四支**（源码注入型，都编译得过；`BUILD_EXIT=0 / TEST_EXIT=1`，每支 `失败: 1，通过: 4`）：

| 负控 | 注入 | 红在哪 | 错误消息（摘） |
|---|---|---|---|
| `nc36` | 摘掉 `SearchOption.AllDirectories`（**＝把阶段 115 的缺陷态注回去**） | 判据二 | `Editors/VariablePicker: 普查表里有它，但它在 Views/ 下要么不存在…陈账要清` |
| `nc37` | 摘掉普查表里 `Editors/VariablePicker` 那一行 | 判据二 | `Editors/VariablePicker: 面外视图里出现了 1 个接线控件，但它不在普查表里` |
| `nc38` | 摘掉 `OutsideViewsAxaml` 里 `MainWindow.axaml` 那一条 | 判据五 | `src/HmiEditor.App/MainWindow.axaml: Views/ 之外出现了新的 .axaml，但它不在 OutsideViewsAxaml 里` |
| `nc39` | 加一条指向不存在文件的条目 | 判据五 | `src/HmiEditor.App/ProbeS116.axaml: OutsideViewsAxaml 里有它，但它在 src/HmiEditor.App 下要么不存在…陈账要清` |

🩸 **两个「自己的解析器也要有见证」的实例**（同一天）：
① 键取 `Path.GetRelativePath(dir, file)` 返回的是**带扩展名**的路径 ⇒ 漏掉去 `.axaml` 那一步，
**16 条登记项全部对不上**（首跑即红）—— 一种错法伪造出「全表漂移」的假象，第一反应会去怀疑表、而错的是**键**；
② 负控脚本的红集解析器写 `startswith("失败 ")`，而 xunit 那行是 `失败!  - 失败: 1，…`（带感叹号）
⇒ 四支**明明全红**却被报成「**没红**」⇒ 判红必须取 `错误消息:` 并**指到哪条腿**。

🩸 **定性（写进守卫 xmldoc 与登记 #69）**：这是「面的边界 ≠ 没有缺口」的**第五形态 ——
账本的覆盖范围自己没被判据盯着**。前四形态都还能自陈「我看不见什么」，这一形态连**自陈的底数都是错的**。

**仍未闭环**：`#39`、`#45`、`#66`（2 条待决）、`#67`；🆕 **`#70`**（回填批门禁撞出的 `AlarmEngineTests` 间歇，整程序集复跑 **12 跑 1 红**，**不是本阶段引入的**，见登记表第 70 项）；面外 **17 视图 / 65 控件**仍无判据
（`PropertyPanelView` 31 是下一刀，要 `UserControl` 宿主装配）；跨文件注入仍是已知盲区
（`CboResolutionPreview` 的宿主 `MainWindow.axaml` 现由判据五逐文件登记、**不声称个数**）。

**门禁（四跑）**：内容批 `5aa9f90` → `s116-gate1.log` `GATE_EXIT=0`；文档批 `cc96df5` → `s116-gate2.log` **红**
（步 [5] `HmiEditor.Device.Tests` `失败: 1，通过: 86`，`System.IO.IOException: No space left on device :
'/tmp/sdk-…/generated_hmi.c'` —— **已登记的环境随机红 `#33`**：沙箱 `/tmp` 是 10 M tmpfs）→
复跑 `s116-gate2b.log` `GATE_EXIT=0`；记忆批 `c8550df` → `s116-gate3.log` `GATE_EXIT=0`；回填批见当日日志（**自指边界**）。

**三跑读数逐条同数**：`44 / 44 / 75 / 87 / 4 / 315 / 2122 / 594` ＋ `8` ＋ `2`（**3294** 通过 / 0 失败）、
**0 警告 0 错误**、路径卫生 **668 / 40 / 130 / 407**；**对拍多重集 0 行**（812 对 812，逐行差全是 MSBuild 并行构建的清单重排）。
⚠ 三跑的树不同（`gate1` 在内容批上，`gate2b` / `gate3` 之间只加 `docs/` ＋ `.workbuddy/memory/`）⇒ 验的是
「**纯文档 / 记忆提交不动读数**」，与阶段 115 那三跑（同一棵树、验**可复现**）**不是同一条性质**。

## 阶段 117（2026-09-27）—— 收口登记 `#70`：报警引擎「入口抓快照」⇒ 变量值改成就地读

**起点**：阶段 116 的**回填批门禁**红出来的那条间歇（`AlarmEngineTests.Engine_TriggerThenReturn_CreatesReturnedRecord`
红在 `:298`；`HmiEditor.Core.Tests` 整程序集复跑 **12 跑 1 红**）。阶段 116 只做到「登记 ＋ 证伪一条机制」，
「复现器 / 修法 / 负控」三件事留在原地。

**三件事**

| # | 事 | 结果 |
|---|---|---|
| ① | 复现器做扎实 | 放弃「时序碰运气」：**反射直调 private `EvaluateCondition`** ⇒ 无并发、无等待 ⇒ 修前 / 修后 / 注回缺陷三态都有可对照读数 |
| ② | 修法 | `EvaluateAll` 去掉入口快照 ⇒ `EvaluateCondition` **每配置就地读**（`lock (_lock)` 内 `TryGetValue`）；`AlarmEngine.cs` 390 → **401** 行（+11） |
| ③ | 负控 | `nc40`（机制式：注回 `HEAD` ⇒ 缺陷在场、`NC40_EXIT=1`、逐字节还原）＋ **判据的生产负控**（注回真文件 ⇒ 三条腿逐条点名、其余 5 条仍绿） |

**机制（一句话）**：`System.Threading.Timer` 回调排在 ThreadPool、可被抢占任意时长 ⇒ 拿着**旧值**的那一轮
可能落到「值已改回、记录已 `Returned`」**之后** ⇒ 那一刻 `activeRecord` 为 null、而旧值判出的 `isTriggered`
仍为真 ⇒ **新建一条 `Active` 记录**（同一报警两条）⇒ `GetAllRecords()` 按 `OccurredAt` 降序 ⇒ `First()`
取到新建的那条 ⇒ `:298` 间歇红。

⚠ **更正登记时的通路**：原写「走 `AlarmEngine.cs:304-311` 把已 `Returned` 的**翻回** `Active`」——
实测那段是**死支**（上面的查询条件 `r.State != AlarmState.Returned` 已排除 Returned），
真实行为是**新建**（记录数 **1 → 2**）。⇒ 同一处「**承「忘填」的枚举值**」家族之外，
又见一次「**猜出来的机制**」必须被探针复核。

**确定性读数**（探针 `ZzProbeS117`，取证完即删；日志 `s117-probe-*.log`）

| 步骤 | 记录数 | `First()` |
|---|---|---|
| 值＝100 评估一次 | 1 | `Active` |
| 值＝0 评估一次 | 1 | `Returned`（同一条） |
| **又持旧值 100** | **2** | **新建的 `Active`** ← 与 `:298` 一字不差 |

连喂 5 次「持旧值」只留 1 条 ⇒ 新建的必要条件是「**已 Returned ＋ 持旧值**」同时在场。

**判据**：`AlarmEngineStaleValueGuardTests`（`Core.Tests` **+6**）

| 腿 | 判什么 | 性质 |
|---|---|---|
| 签名腿 | `EvaluateCondition` 形参表不得出现 `Dictionary<string, double>` | 不许有 |
| 入口腿 | `EvaluateAll` 方法体**零出现** `_lastVariableValues`（**不枚举快照写法** —— 枚举是「面只数形状」，换个等价写法就消失） | 不许有 |
| 就地读腿 | `EvaluateCondition` 方法体**必须**出现 `_lastVariableValues` | **必须有** |

🩸 **三条腿为什么必须互相配重**：前两条都是「不许有」型，**最省事的「改法」是把就地读删掉**
（引擎根本不再读值 ⇒ 前两条反而更绿）⇒ 没有第三条，这个判据守不住它声称的东西。
这是本仓「**面到 0 ⇒ 改性质**」的**镜像**：那条讲「面看不见缺口」，这条讲「**删了功能也能变绿**」。

**输入面刻意只有那两个方法**（铁律③：输入面不得含「定义面」）：文件里
`UpdateVariableValues(Dictionary<string, double> values)` 是**写入口**、字段声明是**定义面** ——
拿整个文件当输入面，它们自己就会被判违规。

**生产负控**（`s117-guard-nc.log`）：把缺陷态注回**真文件** ⇒ `TEST_EXIT=1`、`失败: 1，通过: 5`、
红消息里三条腿**逐条点名**、其余 5 条（扫描面自检 ＋ 内嵌负控 3 ＋ 正控 1）**仍绿**
（否则只是「判据总会红」）；还原后 SHA 与 `git diff` 指纹**双双回到注入前**。

**门禁（三跑 ＋ 回填批自己那一跑）**

| 批 | 提交 | 时刻 | 门禁日志 | birth / mtime | 差 |
|---|---|---|---|---|---|
| 内容 | `1e58a7d` | 22:23:40 | `s117-gate1.log` | 22:32:30.366 / 22:35:12.159 | ＋8 分 50 秒 |
| 文档 | `003b41a` | 22:26:28 | `s117-gate2.log` | 22:35:31.862 / 22:37:11.870 | ＋9 分 04 秒 |
| 记忆 | `f4c9bbd` | 22:28:50 | `s117-gate3.log` | 22:37:11.887 / 22:39:12.861 | ＋8 分 22 秒 |
| 回填 | 见当日日志 | — | `s117-gate4.log` | — | **自指边界**（只记日志、不回写本表） |

**三跑读数逐项一致**：`GATE_EXIT=0` / **7 步全绿** / **849 行**；步 5 逐程序集
`44 / 44 / 75 / 87 / 4 / 315 / 2128 / 594`（`Core.Tests` **2122 → 2128**，+6 ＝ 新判据）
＋ 步 6 独占 `8` ＋ `2`；构建 **0 警告 0 错误**；路径卫生 **668 / 40 / 130 / 407**（脚本 **28** 个）。
**对拍**（`.workbuddy/tmp/gate-diff.py`，先剥墙钟）：`gate1 → gate2` 真差异 **0 行**（逐行位移 24 行 ＝ 重排）、
`gate1 → gate3` 真差异 **0 行**（位移 36 行）⇒ **纯文档 / 记忆提交不动读数**。

**修后配对读数**：`HmiEditor.Core.Tests` 整程序集 **12 跑 0 红**（每跑 `2136 / 2136`；
`s117-soak-summary.txt` ＋ `s117-soak-r1..r12.log`）—— 与登记时的 **12 跑 1 红**同程序集、同 N。
⚠ **不作为「修好了」的证据**（本仓纪律：对间歇缺陷「有一次绿」零信息，12 跑零红亦然）：
证据是**确定性**探针三态 ＋ 判据的生产负控。

**仍未闭环**：`#39`、`#45`、`#66`（2 条待决）、`#67`；面外 **17 视图 / 65 控件**仍无判据
（`PropertyPanelView` 31 是下一刀，要 `UserControl` 宿主装配）；跨文件注入仍是已知盲区。


## 阶段 118（2026-09-28）—— 第五层加第 9 个宿主 `AlarmListView`，并**更正**「面外最大 ＝ 最该扩」

**起点**：阶段 117 收口后剩下四个未闭环项，其中 `#66②` / `#67` 都要**产品口径**（不该由我拍板）、
`#45` 根因未定且已有规避（**0/5 复现率在统计上无意义**）⇒ 只剩 `#39`，且 `#39` 内部还要再选一刀。

**🩸 本阶段真正的产出：推翻登记表里放了两个阶段的一句话**

登记表 `#39` 与选择族专题都写着「`PropertyPanelView` 是面外最大的一处，**也是往后最该扩的地方**」。

| | 判断 | 实测 |
|---|---|---|
| 「最大」 | ✅ 成立 | 31 个接线控件，**是第二名的近四倍** |
| 「最该扩」 | ❌ **推不出来** | ① 数据面**已被 `PropertyPanelWiringTests` 判过**（逻辑树 70 个编辑器 × 26 个类型场景 ＋ 1 个屏幕场景，`uncovered == 0`）⇒ 扩它主要是**重复记账**；② `SaveProperties()` 是**批量写**（拨一个下拉连带写 **6** 个模型字段）⇒ 与本层「未声明的分量一字不变」**正面冲突**；③ 真实成本是 `15 ＋ 31 ＝ 46 条`（普查表只记了后一半） |

⇒ 处置**不是**扩它、**也不是**改表假装它不该判，而是把「**本表只回答有几个、不回答该不该扩**」
写进守卫类注释。**账本的排序口径 ≠ 覆盖缺口的大小。**

**🩸 顺带记一次仪器骗人（两版都被自己推翻）**

| 版 | 做法 | 结论 | 为什么错 |
|---|---|---|---|
| v1 | 按**字面量点名**量「哪些控件零覆盖」 | 20/31 ＋ 10/15 零覆盖 | `PropertyPanelWiringTests` 是**枚举驱动**的、从不提控件名 ⇒ **假阴性** |
| v2 | 改按 `new X(` 量 | 所有视图都「有行为判据」 | `WindowProductionPathUiTests` 造了 **33** 个窗口 ⇒ **构造 ≠ 驱动**（**假阳性**） |

⇒ 改成三条**可证**依据，而不是「个数排序」。与阶段 115 那两条（`Path.GetRelativePath` 带扩展名 /
`startswith("失败 ")` 少一个感叹号）同源：**先确认你在读的东西真的是你要的那个信号**。

**装配**（`HostButtonEffectUiTests`，**+167 行**，产品代码 0 行改动）

| 桶 | 条数 | 明细 |
|---|---|---|
| `Moves` | 1（带布景） | `BtnAcknowledge`：`已确认数 0→1` / `活动数 2→1` / `选中下标 → -1` |
| `SelMoves` | 4 | 两个筛选下拉各驱动两次（唯一性键是 `(名字, 手势下标)`） |
| `Moves` | 1 | `BtnClearReturned`：`引擎条数 3→2` / `已恢复数 1→0` / `列表条数 3→2` |
| `Never` | 1 | `BtnTitleClose` |

🩸 三个刻意的细节：① **`选中下标 → -1` 不是凑数**（`RefreshData()` 走 `Items.Clear()` 重建整列 ⇒
选中被丢掉，这是该行为的**唯一**见证）；② **造数顺序是判据的一部分**（列表按 `OccurredAt` 降序 ⇒
最后造的排最前；`BtnAcknowledge` 只确认**选中项**、`AcknowledgeAlarm` 只对 `State == Active` 返回 true
⇒ 必须让一条 `Active` 落到 `index 0`，否则静默 flaky）；③ **必须轮询、不能 `Sleep`**
（`UpdateVariableValues` 只写字典、不触发评估；记录由定时器造）⇒ 本地写了 `WaitUntil`（超时即抛）。

**读数：四个等式各对上一次**

| 口径 | 阶段 115 | 阶段 118 | 校验 |
|---|---|---|---|
| 选择族声明条数 | 49 | **53** | `+4` |
| 按宿主去重控件数 | 45 | **47** | `+2` |
| 全仓按名字去重 | 42 | **44** | `+2` |
| 面外 | 17 文件 / 65 控件 | **16 / 63** | `−1 / −2` |
| 全表按钮声明 | 170（97/55/18），布景 17 | **177**（103/55/19），布景 **18** | `+7` |

⚠ `177` 是**按钮**表、`53` 是**选择族**表，两张表别混。
🩸 **面外读数三期以来第一次真的动了**（阶段 115/116 两次名字级/接线级残余都「一字不变」）。

**负控四支**（`.workbuddy/tmp/s118-neg.py`；默认干跑、`--apply` 才落盘；每支 `try/finally` 还原）

| 支 | 注入 | 失败/通过 | 红的腿 |
|---|---|---|---|
| `N1` | 摘 `CmbStateFilter.SelectionChanged +=` | 2 / 13 | 判据一「被分类了却找不到接线」 |
| `N2` | 删**两条** `CmbSeverityFilter` 声明 | 1 / 14 | 判据一「接线了却没被分类」 |
| `N3` | `已确认数` 期望 `1 → 0` | 1 / 14 | 运行期 Theory |
| `N4` | 普查表 `AnimationEditorView` 记 `8 → 9` | 1 / 14 | 判据二「普查表记的是 9 个，实际 8 个」 |

🩸 `N2` **必须删两条** —— 只删一条时 `declaredNames` 里仍有那个名字 ⇒ 判据一**不红**；
这正好把「**声明条数**」与「**按宿主去重的控件数**」的差别变成**可执行的事实**（＝ 上表那三个数为什么不能混）。
`N4` 补的是**本轮还没见过它红**的那条腿（阶段 116 的 `nc36`–`nc39` 打的都是「文件不在表里 / 表里有它但文件没了」）。
还原后 md5 与基线**逐字节一致**。

**门禁（五跑）**

| 批 | 提交 | 时刻 | 门禁日志（birth / mtime） | 结果 |
|---|---|---|---|---|
| 内容批（**提交前**） | —（工作树＝`b06d7d7` 里那 2 个测试文件） | — | `s118-gate1.log`（11:54:28.684 / 11:57:01.187） | `GATE_EXIT=0` / **7 步全绿** |
| 内容批（提交后） | **`b06d7d7`**（2 files，+206/−22） | 11:58:42 | `s118-gate2.log`（11:58:52.861 / 12:00:03.451） | `GATE_EXIT=0` / 7 步全绿 |
| 文档批 | **`95467d4`**（4 files，+422/−13） | 12:00:14 | `s118-gate3.log`（12:00:14.214 / 12:01:28.030） | `GATE_EXIT=0` / 7 步全绿 |
| 记忆批 | **`c30fcd9`**（2 files，+147） | 12:02:49 | `s118-gate4.log`（12:02:49.234 / 12:04:01.169） | `GATE_EXIT=0` / 7 步全绿 |
| 回填批 | 见当日日志 | — | `s118-gate5.log` | **自指边界** ⇒ 只记当日日志、**不回写本表** |

**四跑读数逐项一致**：`GATE_EXIT=0` / **7 步全绿**（其中固件 host 测试报 **9 步全部通过**）；
**849 行**（非空 **811** —— `gate1` / `gate2`；**812** —— `gate3` / `gate4`；
差的这 1 行就是那条被并发写者**挤成一行**的固定标记行，**结构性归一后四跑一律 812**）；
步 5 逐程序集 `44 / 44 / 75 / 87 / 4 / 315 / 2128 / 595`（大队合计 **3292**）
＋ 步 6 独占 `8` ＋ `2`（合计 **3302** 通过 / 0 失败，**10 个程序集**）；
**0 警告 0 错误**；路径卫生 **668 个文件** / 重复接线扫 **40** 个 `.axaml` /
面板总线扫 **130** 个 `.cs`（识别 **24** 个视图成员）/ 位置写入扫 **407** 个 `.cs`（接线 **0**、读取 **0**）；
脚本 **28** 个。

**时序**（⚠ 本仓规矩：**读数读的是工作树 ⇒ 只要求晚于「改动落盘」，不是晚于「提交」**）：
`gate1` birth **11:54:28.684** 晚于内容批两文件落盘 ——
`SelectionFaceClassificationGuardTests.cs` **11:53:58.239**（差 **30.4 s**）、
`HostButtonEffectUiTests.cs` **11:53:42.164**（差 **46.5 s**）；
`gate3` birth **12:00:14.214** 晚于文档批最后一次落盘（`project-status.md` **12:00:08.261**）**5.95 s**；
`gate4` birth **12:02:49.234** 晚于记忆批最后一次落盘（`2026-09-23-memory-detail-extract.md` **12:02:16.013**）**33.2 s**；
`gate2` birth **11:58:52.861** 晚于内容批提交 `b06d7d7`（**11:58:42**）**10.9 s**。
⚠ `gate3` / `gate4` 的 birth 与各自提交**同秒**（`git` 的提交时刻只精确到**秒**）⇒
那两条腿**不可辨先后**，立论改用「**改动落盘**」那条腿（上面已给毫秒差）。

**对拍**（`gate-diff.py`，**先剥墙钟**；再按 §115.11 立的**结构性归一**「拆回被挤的标记行 ＋ 丢空行」）：

| 对 | 剥墙钟后原样多重集差 | 归一行数 | 归一后真差异 |
|---|---|---|---|
| `gate1 → gate2` | **0 行** | 812 对 812 | **0 行** |
| `gate1 → gate3` | 4 行（−2 / +2） | 812 对 812 | **0 行** |
| `gate1 → gate4` | 4 行（−2 / +2） | 812 对 812 | **0 行** |

那 4 行**正是 §115.11 记的那条形态**：并发写同一管道把固定标记行
`总共 1 个测试文件与指定模式相匹配。` **挤成一行**（另多一个空行）。
`gate1` / `gate2` 是「独立 8 次 ＋ 双份 1 次」、`gate3` / `gate4` 是「独立 10 次」
（**合计标记都是 10 个，两边一致**）。
⚠ **这不是「放宽对拍」**：真丢了内容，非空行照样对不上。
⚠ **三跑的树不是同一棵**（`gate1` 在内容批工作树上、`gate2` 在内容批提交后、
`gate3` / `gate4` 分别在文档批 / 记忆批提交后）⇒
这 0 差异验证的是「**纯文档 / 记忆提交不该动任何读数**」，与阶段 115 那三跑（**同一棵树**、
验的是**可复现**）是**两条不同的性质**，别混着引。

（读数与对拍的采集脚本、以及它在第一版里读错的两个数字 ⇒ 见专题
`docs/2026-09-28-button-effect-alarm-list-face.md` §118.10.1。）

**仍未闭环**：`#45`、`#66`（2 条待决）、`#67`；面外 **16 视图 / 63 控件**仍无行为判据
（`PropertyPanelView` 31 **不是**下一刀，「最大」≠「最该扩」，见上）；跨文件注入仍是已知盲区。

## 阶段 119（2026-09-28）—— 收口工具链收进 `scripts/`（并挂进门禁步 [1]）

**一句话**：把收口这套量具从 `.workbuddy/tmp/` 的一次性脚本收进版本库，各带 `--selftest`
并**挂进门禁** ⇒ 「上一阶段那条 0 行差异」从此**事后可复算**。**产品代码 0 行改动**。
专题 ⇒ `docs/2026-09-28-closeout-toolchain-promotion.md`。

**收编 4 件**：`scripts/gate-run.sh`（跑门禁 ＋ 退出码落进日志；3 档自检）、
`scripts/gate-diff.py`（跨版本对拍：剥墙钟 → 多重集差 → 逐行位移 → 结构性归一；4 档）、
`scripts/gate-readings.py`（读数摘要，含「关键项匹配不到就报警」；3 档）、
`scripts/face-residual.py`（五层动作面残余探针；3 档）。
四个自检挂在门禁**步 [1] 之内**（刻意**不**新开 `banner` ⇒ **步数仍是 7**，
保住与 18 份历史日志的可比性；只有行数 +4）。

**读数（内容批两跑逐项一致）**：`GATE_EXIT=0` / **7 步全绿**；**853 行**（非空 **816**）；
脚本 **29** 个；路径卫生 **672** 个文件；标记行独立 10 次（无被挤者）；
步 5 逐程序集 `44 / 44 / 75 / 87 / 4 / 315 / 2128 / 595` ＋ 步 6 独占 `8` ＋ `2` ＝ **3302**。

⚠ **旧读数当场作废**（§40.6 第 4 条那句推测落成事实）：行数 **849 → 853**、
脚本数 **28 → 29**、路径卫生 **668 → 672**、逐程序集合计 **3302 → 3302**（不变）、步数 **7 → 7**（不变）。

**三条新教训**：

1. 🩸 **负控写假了也会「过」**：注入写成 `if False:` 而落在 `if/elif` 结构上 ⇒ 条件假时
   **`elif` 照样求值**，判据根本没被摘掉 ⇒ 负控**假绿**。负控必须摘**整块**，
   而且要**验证判据真的不响了**（不是「跑完没报错」）。
2. 🩸 **路径卫生的面取决于「是否被 git 跟踪」**（`git ls-files` 收集清单）⇒ 未跟踪的新文件
   是**盲区**（实测同一份注入：未跟踪绿 668 / `git add` 后红 669）。⇒ 那条「内容批门禁跑在
   **代码落盘之后、提交之前**」的纪律里，对**新文件**而言「落盘」＝**进索引**；
   本阶段据此把 `git add scripts/` 放在门禁跑**之前**，否则那一步对 4 个新脚本是**空转**。
3. 🩸 **两个「63」不是同一个 63**：残余探针（**按钮类 · 名字级**）63 格 / 55 名字 / 20 视图，
   与守卫普查表（**选择类 · 接线控件级**）16 文件 / 63 个**巧合同数** ⇒
   **引数字必须带来源工具名**（与「门禁 7 步 vs host 9 步」同族）。

**门禁（四跑）**：提交前 `s119-gate1.log`（birth **13:02:03.102**）＋ 提交后 `s119-gate2.log`
（**13:03:32.310**）＋ 文档批 `s119-gate3.log`（**13:05:52.151**）＋ 记忆批 `s119-gate4.log`
（**13:12:16.127**）；四跑 `GATE_EXIT=0` / 7 步全绿、**逐项读数相同**。
提交 **`a17a79d`**（5 files，+709）/ **`ec9c21b`**（3 files，+246/−2）/ **`a9424b0`**（2 files，+161）；
birth 晚于被证明那一刻：**＋4.3 s / ＋10.2 s / ＋5.1 s**。
对拍：`gate1 → gate2` **0 行**、`gate3 → gate4` **0 行**（两者归一行数都是 **816 对 816**）。

**仍未闭环**：`#45`、`#66`（2 条待决）、`#67`；面外 **16 视图 / 63 选择控件**（守卫口径）
仍无行为判据；跨文件注入仍是已知盲区。⚠ 本阶段**关掉**了登记表 §40.6 第 4 条。

## 阶段 120（2026-09-28）—— 全面检查后的四批修复（内存安全 → 静默失败 → 判据网 → 文档）

**一句话**：一次独立全面审计（门禁 + 端到端 + 登记表复核 + 四路源码扫描）找出并修掉了
**4 级 14 项真实缺陷**，并补齐了 **3 条判据/隔离**（LVGL user_data 守卫、三个静态守卫的
自检进门禁、`verify.sh` 与 `ci-gate.sh` 的隔离对齐）。**工作树状态，未提交**。
专题与逐项判据见登记表 §42。

**第 1 级 · 内存安全（固件）**：`mqtt_client.c` `build_subscribe` 的 `vh[8]` 栈越界
（默认主题每次订阅越界 ≥9 字节；判据用 `-fstack-protector-all` + 23 条 host 断言）；
`soft_keyboard.c` 把栈局部动作地址交给 LVGL（新守卫能点名修复前的 `:342`）；
`hmi_tags_adopt` 分配失败后 `g_count` 与 `s_cache` 不一致 ⇒ 堆越界（注入失败 + 负控）。

**第 2 级 · 崩溃/数据丢失**：`DataLogger` 的 `MaxRecords<=0` 会在定时器线程抛异常终止进程；
图库 `Load()` 读失败后退出时无条件 `Save` ⇒ 静默清空自定义资源；`AlarmConfigView` 的
字段处理器**全历史零订阅**（"能看不能改"，且默认选中发生在订阅之前 ⇒ 右列恒空）；
生成 C 的几何小数随区域设置漂移（de-DE 下 `12,5`）。

**第 3/4 级 · 静默失败与进程/安全**：RTU 写回显被读解析器校验 ⇒ **RTU 写全部静默失败**；
RTU 驱动 `expectedFrameLength` 收下没用；OPC UA 的 `connectTask.Wait(ms)` 反模式；
取消子进程不杀（cmake/gcc/sunfel 继续写共享目录）；部署命令把文件名原样插进 shell；
预览 `TryRead` 逐帧同步发 Modbus 请求冻结 UI（改成后台泵 + 快照）。

**读数（工作树，未提交）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；**967 行**（非空 **930**）；
脚本 **29**；路径卫生 **675**（新增 8 个文件后；`git add` 前为 672 —— 3 个落在 `src/`·`scripts/` 扫描面内，5 个在 `tests/`）；
步 5 逐程序集 `44 / 44 / 4 / 94 / 76 / 2135 / 315 / 602`（合计 **3314**）＋ 步 6 独占 `8` ＋ `2`
＝ **3324 通过 / 0 失败**（较阶段 119 的 3302 **+22**，全部是本阶段新增判据）；加固件 host `[9/9]`。
步 [1] 多一行：`✅ LVGL event user_data 全部是稳定存储（扫 31 个 .c / 26 处接线）`。

**文档就地更正**：README 的 `src/` 13→12（去掉已下架的 `Scripting`）、`tests/` 7→8、
`TestTempDir` 路径；`troubleshooting.md` 的 host 8→9 步与门禁步号 [4]→[7]；
登记表 §4 #30 的行数基线 3014→**2994**（图库逻辑析出到 `MainWindow.ResourceLibrary.cs`）；
「10 个程序集」更正为 **8 个程序集 / 10 次 `dotnet test` 调用**。

**仍未闭环**：`#45`、`#66②`、`#67`（产品口径）；面外 **16 视图 / 63 选择控件**无行为判据；
跨文件注入与「未跟踪新文件不进路径卫生扫描面」仍是已知盲区。

**端到端验收（两跑，红绿都记）**：`.dbg/verify-tier6.log` **`VERIFY_EXIT=1`**（1 红 = **#47 已知间歇红**
`DispatcherOwnershipContractTests.CrossDispatch_…`，单跑 3/3 绿；本阶段改动面与两个 `Dispatcher`
getter 无交集 ⇒ 判为既存偶发）；`.dbg/verify-tier6b.log` **`VERIFY_EXIT=0`** ——
真实 LVGL 9.5 CMake 编译 → `hmi_sim` → 300 帧 selftest → CTest 全通过，停靠面板类按独占段跑 2/2。
⚠ `scripts/verify.sh` 本阶段起与 `ci-gate.sh` 的隔离策略对齐（排除 `DockPanelInvocationUiTests` +
`--blame-hang`）—— 修的是两份门禁的**分歧**，不是这次的红。

## 阶段 121（2026-09-28）—— 审计剩余项的硬化（固件协议/数值 + OPC UA 畸形帧 + 位偏移）

**一句话**：把阶段 120 全面审计里**刻意留在优先级之外**的剩余缺陷再收一批，逐项配负控。
产品代码按「固件侧 / C# 侧」两块，专题见登记表 §43。

- **固件**：`regs_to_float("U32")` 有符号位移导致高位置 1 时读数变负；Modbus RTU 读响应的
  `bc` 未与期望核对（"bc 偏小但 CRC 合法"⇒ 未初始化字节进变量缓存），并删掉每轮读都打的临时
  RX 诊断；`mqtt close_sock` 加锁 + `SO_SNDTIMEO`（并如实写明 poll↔publish 的 fd 竞争仍在）；
  触摸校准时分母为零/非有限值直接拒绝（旧行为是写 inf/nan 而设备端拒用 ⇒ 界面谎报成功）；
  `sunxifb` 24bpp 显式拒绝（旧代码按 4 字节/像素拷贝 ⇒ 越界写帧缓冲）。
- **C#**：位报警 `BitOffset` 限 0..63（C# 位移按 64 取模 ⇒ offset=64 会静默测 bit 0），
  两个编辑器输入同步钳位；`CommunicationRuntimeSource` 的死 catch 补上
  `SimulatorNotConnectedException`；`OpcUaBinaryReader.Ensure` 的 `offset+count` 溢出改为减法判据；
  `OpcUaMessageLayer.ReadFrame` 对 `messageSize` 8..11 的畸形帧点名为帧异常。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；**976 行**（非空 **939**）；
脚本 29；路径卫生 **675**；步 5 逐程序集 `44 / 44 / 4 / 94 / 78 / 2138 / 315 / 602`（合计 **3319**）
＋ 步 6 独占 `8` ＋ `2` ＝ **3329 通过 / 0 失败**（阶段 120 为 3324，**+5**）；
固件 host `[9/9]`（通讯与变量表 124 条）。

**负控 5/5**（逐支点名到期望那条腿、还原 SHA256 逐字节一致）：U32 符号 / 读字节数校验 /
OPC UA `Ensure` / OPC UA 帧头长度 / 位偏移范围。

**仍未闭环**（低危 / 需实机 / 需产品口径）：`TemplateManager` 毫秒命名、`HmiPackageService`
缺失资源不报警、MQTT poll↔publish fd 竞争、`sunxifb` 24bpp 未实现、`main.c` 信号处理器、
`touch_evdev` read 错误、启动期探针日志；以及 `#45`、`#66②`、`#67`、面外 16 视图 / 63 选择控件、
两条扫描盲区。

## 阶段 122（2026-09-28）—— 审计剩余项收口（直到没有"可软件验证"的余项）

**一句话**：把审计清单里最后一批可软件验证的项收完 —— 命名唯一性、缺失资产可见性、
判据网 fail-closed（4 处）、静默跳过、固件收尾（轮询日志 / 触摸读错误 / 信号处理器）、
DeviceFlasher 补测、MQTT fd 竞争收口、24bpp 决策。专题与逐项判据见登记表 §44。

- **命名 / 可见性**：`TemplateManager` 面板块默认名改进程内单调序号（旧毫秒后缀同毫秒重名）；
  `HmiPackageService` 的缺失资产从"两边静默 `continue`"改成**打包侧记进 manifest + 解包侧
  合并返回 + App 状态栏提示**（`package.assets.missing`）。
- **判据网 fail-closed**：`check-xaml-duplicate-wiring` 加 `MIN_SCANNED_AXAML`；
  `check-dock-bus-only-subscription` 的 `MIN_VIEW_MEMBERS` 从"只在非 `--json` 分支"提到分支之前；
  `check-path-hygiene.sh` 加扫描面下限 + **`--selftest`**（并挂进门禁步 [1] 的自检循环）；
  `TestAssertionStyleGuardTests` 加枚举面下限；`GitWorkflowServiceTests` 的 5 处静默
  `if (!GitAvailable()) return;` 改成 `RequireGit()`（缺 CLI 判红）。
- **固件收尾**：`hmi_var_poll_once` 的每 100ms 无条件 stderr 收进 `HMI_VAR_DEBUG`；
  `touch_evdev` 读循环认 `EAGAIN` 与真错误（真错误关闭输入并抬起，不再拿陈旧坐标上报）；
  `main.c` 的 SIGUSR1/2 处理器只置标志、动作移到主循环（原来在信号上下文里调 LVGL/malloc/fprintf）。
- **MQTT**：把**全部** socket 访问收敛到 `s_lock`，且每个 syscall 在锁内重新读 `s_sock`
  ⇒ 消除"旧 fd 被 close 后继续用"的窗口；`process_rx()` 与 `do_disconnect()` 一律在锁外。
  🔁 更正阶段 121 源码注释里那句"poll↔publish 的 fd 竞争未消除"。
- **24bpp**：决定**不实现**，维持"只认 32bpp + 显式拒绝"，理由（无实机可验证 3 字节路径、
  旧缺陷是越界写而非缺功能）写进源码注释。
- **DeviceFlasher 补测**：假 `sunxi-fel` 脚本验证参数拼装（`{image}` 替换、含空格路径作为
  一个实参）、退出码判定与输出收集；**负控**：退回拼 shell 字符串 ⇒ 断言红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；**980 行**（非空 **942**）；
脚本 29；路径卫生 **675**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2140 / 315 / 602`（合计 **3323**）
＋ 步 6 独占 `8` ＋ `2` ＝ **3333 通过 / 0 失败**（阶段 121 为 3329，**+4**）；固件 host `[9/9]`。
`SingleFileSizeGuardTests` 基线随两处析出改为 **2992**（3014 → 2994 → 2992，两次都是变小）。

**不可在本环境闭环的余项（逐条写明理由）**：`#45`（根因未定、复现率低，纪律上无可复现信号
就不动隔离级别；已用独占进程规避）、`#66②`/`#67`（**产品口径**）、面外 16 视图 / 63 选择控件
（大规模扩面，且"按个数挑下一刀"的口径已被阶段 118 推翻）、`sunxifb` 24bpp 实机像素验证 /
RAM 预算 / RK3506 预算（**需硬件**）、跨文件注入与"未跟踪新文件不进路径卫生扫描面"（判据口径问题，
各自的代价已在登记表里论证）。

## 阶段 123（2026-09-28）—— 盲区/脆弱判据收口（路径卫生两条腿 + 三处墙钟断言）

**一句话**：关掉已登记的两个"判据自己的洞"之一（未跟踪新文件不进路径卫生扫描面），
并把审计点名的三处墙钟脆弱断言改成差值/比值；`#67①` 复核后判定"无可观察差异、不动产品代码"。
专题与逐条推理见登记表 §45。

- **路径卫生取数改为两条腿**：`git ls-files` ∪ `git ls-files --others --exclude-standard`。
  阶段 119 的 N4 实测过这个盲区（同一注入：未跟踪绿 / `git add` 后红），当时只能靠纪律兜；
  现在**未跟踪的新文件当场被红**。顺带修一个隐蔽取数缺陷：git 默认输出会给含空格/非 ASCII 的
  名字加引号转义，使后缀过滤与 `[ -f ]` 失配 ⇒ 改用 `-z` + `tr`，取数与计数口径统一（675 是准的）。
  `--selftest` 扩到 **7 档**（含临时 git 仓库的"未跟踪必须在面内 / 未跟踪脏文件必须红 / 全干净必须绿"）；
  真实未跟踪脏文件实测被点名。
- **三处墙钟断言 → 差值/比值**（都配产品侧负控，逐支点名到期望那条腿）：
  `CommunicationSimulatorTests` 的 `InRange(30,200)` → 两次延迟的**差值**；
  `CommandFrameCollectorTests` 的 `>=400ms` → 两次静默期的**差值**；
  `ModbusTcpRuntimeDriverTests` 的 `>=250ms` → 单个 vs 两个并发的**比值**。
  ⚠ 这三处的**失效方向不同**：第一处原会**假红**，后两处原会**假绿**（负载高时坏实现也能越过下界）。
- **`#67①` 结论**：`OnTimeWindowChanged` 缺抑噪守卫 ＋ 程序性写入在窗口外，但逐行复核后
  **今天没有任何可观察差异**（赋值只在"选项等于模型值"时发生 ⇒ 空写；不匹配时根本不赋值）
  ⇒ 按"做不出负控的判据不发布"**不改产品代码**，只登记推理与触发条件。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；**984 行**（非空 **947**）；
脚本 29；路径卫生 **675**（已跟踪 ∪ 未跟踪）；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2140 / 315 / 602`
（合计 **3323**）＋ 步 6 独占 `8` ＋ `2` ＝ **3333 通过 / 0 失败**；固件 host `[9/9]`。

**仍待用户拍板**：`#66②`（用户详情面板改动静默丢失）与 `#67②`（表外时间窗显示不诚实），
两条都已在登记表 §45.4 给出推荐项与备选。

## 阶段 124（2026-09-28）—— 两条产品口径落地（用户详情面板接线 ＋ 表外时间窗显示）

**一句话**：把阶段 123 交给用户拍板的两条口径按**推荐项**落地 —— `#66②` 接上详情面板的两处编辑
（显示名失焦提交 / 角色选中提交，立即落盘 ＋ 写审计），`#67②` 给表外时间窗补一个「自定义(N ms)」项；
两条各配行为判据与**产品侧负控**。逐条推理与读数见登记表 §46。

- **`#66②` 详情面板**：`OnDetailChanged` 此前**全历史零引用**（`DetailDisplayName.LostFocus` 与
  `DetailRoleCombo.SelectionChanged` 都没订）⇒ 改显示名/角色**静默丢失**。现在：
  `AuthenticationService.UpdateUserDisplayName`（改模型 ＋ 写审计，同值不写）、
  新的 `AuditAction.UserProfileChanged`（＋ 审计表的显示名，否则界面显示英文标识符）；
  `OnDetailChanged` 重写 —— **角色先读、显示名后读、两者都在 `RefreshUserList()` 之前**
  （那次刷新会把下拉拨回模型里的角色，先刷新就读不到），两处提示合并成一句。
  ⚠ `ShowUserDetail` 的 `_isUpdatingDetail` 守卫是**未证的余量**（两条臂都是差量判定，
  今天拿不出红腿；它挡的是"把 `_selectedUser = user` 挪到 `ShowUserDetail` 之后"这一类顺序变动）。
- **`#67②` 时间窗口**：追加/移除「自定义(N ms)」项并选中；`TimeWindowOptions()` 由 `static readonly`
  数组改成**方法**（顺带修掉"选项文案被静态初始化冻结在首次语言"的同族缺陷）；
  `OnTimeWindowChanged` 加 `_suppressDisplayEvents` 守卫、读 `Tag` 不读下标。
- **判据**：`UserProfileEditUiTests`（3 条：落盘+审计、幂等、反向"换选中不许写"）＋
  `TrendTimeWindowUiTests`（2 条：表外值显示诚实、标签不冻结）＋ 第五层 1 条选择族声明
  （`DetailRoleCombo` 拨角色 ⇒ 审计数 2→3，位置排在 `BtnClearLog` 之后）。
  🩸 焦点驱动走**真迁移**而非 `RaiseEvent`：落点必须是**当前选项卡里**的控件，
  否则 `Focus()` 返回 false、焦点没走、`LostFocus` 不触发 ⇒ 判据**静默变成"改名没生效"**（首跑实测踩到）。
- **负控 4 条**（全部注入产品代码、逐条点名到期望的腿、SHA256 还原）：摘两条订阅 ⇒ 两条行为判据红；
  摘 `UpdateUserDisplayName` 的同值提前返回 ⇒ 幂等那条红；去掉「追加自定义项」⇒ 时间窗口①红；
  选项表退回 `static readonly` ⇒ 时间窗口②红。
- **被门禁点名后归位**：新写的审计详情让 `CoreVisibleTextInventoryGuardTests` 报
  `Services/AuthenticationService.cs: 登记 12，实扫 13` ⇒ 台账 12 → 13 ＋ 归位链末段 ＋
  盘点专题 §0/§19（链正则的标签字符类放宽到含圈码，免得为迁就正则去改登记号写法）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**
（非空 **947**）；脚本 **29**；路径卫生 **675**（已跟踪 ∪ 未跟踪）；
步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2140 / 315 / 607`（合计 **3328**）＋ 步 6 独占 `8` ＋ `2`
＝ **3338 通过 / 0 失败**（阶段 123 为 3333，**+5**）；固件 host `[9/9]`。

**`#66②`/`#67②` 至此闭环**（两条都从"产品口径待定"变成"已落地 ＋ 有判据 ＋ 有负控"）。
仍不可在本环境闭环的余项不变：`#45`（根因未定 / 低复现，用独占进程规避）、面外 16 视图 / 63 选择控件、
`sunxifb` 24bpp 实机像素验证与 RAM/RK3506 预算（**需硬件**）、跨文件注入。

## 阶段 125（2026-09-28）—— 关掉「跨文件注入」盲区（枚举的语义单位从文件改成 partial class）

**一句话**：静态选择族守卫的「跨文件注入」盲区从阶段 115 起就写在"诚实边界"里，本阶段把它
**量清并关掉** —— 顺带发现那句诊断本身是错的：那条接线**不是**辅助函数注入，只是**换了个文件**。
真正错的是**枚举的语义单位**（"文件"而不是"同一个类的全部文件"），这是「面的边界 ≠ 没有缺口」的
**第六形态**。逐条读数、推理与负控见登记表 §47。

- **量清**：`MainWindow` 的代码分在 **8 个** partial 文件里，面内接线**只有 1 条**
  （`CboResolutionPreview`，订在 `Wiring.Commands.cs:79`），而 `MainWindow.axaml` 有 143 个具名控件、
  **0** 处 XAML 属性形态。`PropertyPanelView`（11 文件）/`DesignCanvas`（5 文件）是另外两个多文件
  partial class。
- **落地**：新增 `partial class` 索引（`PartialClassFilesByClass`）＋ `CodeFilesFor` ＋
  `ScanHelperInjectionAcross`（把"一个载体找调用点"抽成 `CallSites`，在**每份载体 × 每份语料**上求并集
  ⇒ 载体与调用点可分居两文件；`ScanHelperInjection` 的对外语义一字不变，判据三自检照旧）；
  三处调用点（判据一 `ScanHost`、判据二普查、判据五）统一走它；判据五的账本从
  `(File, Why)` 升级成 **`(File, WiredSelects, Why)`** 并用同一套机制实测个数、两侧都断言。
- **不制造假红**：扩面后 `Views/` 普查表**一格都没变**（16 视图 / 63 控件仍是真值）——
  `PropertyPanelView.Faceplate.cs` 里那条 `combo.SelectionChanged += …` 是循环局部量、不在
  `x:Name` 表里，两种口径都正确地把它挡在面外。**扩面 ≠ 放宽判据。**
- **判据 ＋ 负控**：新增合成语料判据 1 条（载体在文件 A、调用点在文件 B；含"单文件口径必须看不见"
  与"实参不在 `x:Name` 表里必须为空"两侧）；产品侧负控 3 条 —— ① 退回旧口径 ⇒ `记的是 1，实测 0`；
  ② 跨文件**直接**订阅多一条 ⇒ `1 → 2`；③ 跨文件**注入** ＋ 新具名 `ComboBox` ⇒ `1 → 2`，
  且读数行**点名**多出来的 `CboNcProbe`。⚠ ①证"这个数真的来自跨文件解析"、②③证"多一条会被点名"，
  互补的两半；③ 还在**真语料**上跑通了跨文件注入那条路径。
- **仍然不判**（边界比上一版小）：间接接线（处理器体里再订另一个控件）、反射/`AddHandler`/`DataTemplate`
  动态接线；`Views/` 之外只做**静态账本**（不驱动 `MainWindow` 的按钮 —— 那要把它建成第十个宿主，
  属另一件事，且阶段 118 已推翻"按个数挑下一刀"的口径）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **675**（已跟踪 ∪ 未跟踪）；步 5 逐程序集
`44 / 44 / 4 / 96 / 78 / 2140 / 315 / 608`（合计 **3329**）＋ 步 6 独占 `8` ＋ `2`
＝ **3339 通过 / 0 失败**（阶段 124 为 3338，**+1**）；固件 host `[9/9]`。

**阶段 124 那段里列的余项「跨文件注入」在本阶段闭环**（它已从"判据口径问题"移出未闭环清单）。
仍不可在本环境闭环的余项：#45（根因未定/低复现，用独占进程规避）、面外 16 视图 / 63 选择控件
（大规模扩面，需先点一个视图）、`sunxifb` 24bpp 实机像素验证与 RAM/RK3506 预算（**需硬件**）。

## 阶段 126（2026-09-28）—— 第五层加第 10 个宿主（首个 `UserControl` 宿主）＋ 一类装配机制

**一句话**：按用户拍板的方向扩第五层面，**先量候选再挑**（阶段 118 明确推翻过「最大 ≠ 最该扩」）
⇒ 选中 `CrossReferenceView`（1 按钮 ＋ 1 下拉，零驱动）；它顺带逼出「宿主可以是被装配进来的
`UserControl`」这类机制，并在动面时抓到两处**没有判据看着的数字漂移**。逐条见登记表 §48。

- **怎么挑的**（不是按个数）：候选表逐项量了「具名按钮 / 接线选择 / 基类」；选中它的四条判据 ——
  数据面明确可读（`Search()` 亲手写结果集合）／**零驱动**（`FindBtn`/`TargetTypeCombo`/`Search()`
  在全部测试里 `grep` 零命中）／写入面窄（只重建自己的集合）／代价最小（3 条声明）。
  ❌ 明确排除 `PropertyPanelView`（阶段 118 三条实测）与 `DeviceManagerView`（15 个具名按钮）。
- **装配机制（面外 9/16 是 `UserControl`）**：`Control<T>` 加「逻辑树按 `Name` 找」第二条路；
  🩸 `window.FindControl` 对装配进来的 UserControl **不是返回 null 而是抛**
  `Could not find parent name scope`（首跑探针一行读数都没打出来就整趟抛）⇒ 兜得很窄、
  只表示"这条路找不到"；宿主类型断言由「必须是 `Window`」放宽成「`Window` 或 `UserControl`」。
  ⚠ 放宽的是**装配方式**、不是判据强度：具名 Button 双向差集 / 未声明分量外溢 / 布景守卫 /
  `Clicks.Length >= 3`、`live >= 2` 一条都没松。
- **第 10 个宿主**：3 条声明全部 `Moves` —— ① `FindBtn`（布景填关键词 ⇒ 结果 0→2）／
  ② 换类型 Screen（结果 2→0）／③ 拨回 Variable（0→2）；②③ 合起来才证明**按 Tag 分派**真的生效。
  夹具 `SeedCrossReferenceFixture` 铺恰好 2 条对「液位」的绑定（**只动 `Project`、不动
  `depot.Variables`**），不铺就是「0→0」的零信息断言。
- **负控 5 条**（四种失败模式各一条腿，注入产品代码 + SHA256 还原）：拆装配机制 ⇒ 新宿主整趟抛；
  摘按钮接线 ⇒ 结果 0；摘选择接线 ⇒ 期望 0 实际 2；拆 Tag 分派 ⇒ 同上；升旗后忘删普查表行 ⇒
  判据二「陈账要清」。
- 🩸 **抓到的两处数字漂移**：① 面内三口径散文写的是阶段 118 的 **53/47/44**，本轮 `git stash`
  回 HEAD 复测是 **57/51/48**（自阶段 120 起少算 4 个控件；加第 10 个宿主后 **59/52/49**）——
  这三处**没有任何判据看着**，只能靠"动面就重数"；② 我自己第一稿把「面外 `UserControl` 个数」
  写成 12，实测是 **9**（7 个 `ChromeWindow`）⇒ 同一族教训「凭印象写数」。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **675**（已跟踪 ∪ 未跟踪）；步 5 逐程序集
`44 / 44 / 4 / 78 / 96 / 2140 / 315 / 609`（合计 **3330**）＋ 步 6 独占 `8` ＋ `2`
＝ **3340 通过 / 0 失败**（阶段 125 为 3339，**+1**）；固件 host `[9/9]`。

**面外还剩 15 个视图 / 62 个接线控件**（普查表 16/63 → 15/62）。仍不可在本环境闭环的余项不变：
`#45`、面外那 15 个视图（需逐个按同一套判据挑）、`sunxifb` 24bpp 实机像素验证与 RAM/RK3506 预算（**需硬件**）。

## 阶段 127（2026-09-28）—— 第五层加第 11 个宿主（第一个"接线写在 XAML 属性里"的新宿主）

**一句话**：继续按同一套判据扩面；先把剩下 14 个面外视图按「选择处理器**写了什么**」分了 A/B/C 三档
（这一步改变了候选排序），挑中 A 档的 `DownloadToHmiView`；过程中撞出本层的一条**机制边界**。
逐条见登记表 §49。

- **分档读数（本阶段最有价值的一条）**：面外 14 个视图里 —— A 档「写了面板内部状态」只有
  `DownloadToHmiView`（＋ `PropertyPanelView`，理由见 §48.1）；**B 档 5 个**（`ProjectExplorerView` /
  `VariableBrowserView` / `AssetManagerView` / `LayerPanelView` / `FaceplateLibraryView`）的选择处理器
  **只 `Invoke` 一个事件**、面板自己不写任何可读状态；C 档 `Editors/VariablePicker`（子目录 + 树叶子）。
  ⇒ 🩸 **「接线完整」（普查表数的那个）与「数据面可读」（本层要的那个）是两件事**：判 B 档得先造一条
  **不是产品代码**的观测通道（把事件记到窗口上），那正是「布景守卫」防的那类作弊面的近亲 ⇒ 本阶段不碰，
  把分档留在表里（下一个人不必再体检一遍）。
- **第 11 个宿主 7 条声明**（2 `Moves` ＋ 1 `Inert` ＋ 4 `Never`）：`CmbChannelType` 换类型 ⇒
  `SerialPanel`/`TcpPanel` 两条可见性读数**一起**翻（两条一起声明不是凑数 —— NC-2 实测「只改一半」
  只让第二条红）；`BtnRefreshPorts` 记 `Inert`（理由两层，见下）；四个 `Never`（两个 `async void`
  需实机通道 + 文件选择器平台桩 + 关窗）。🆕 它是**第一个 XAML 属性接线的新宿主**，与第 10 个
  （code-behind `+=`）构成形态对照。
- 🩸 **撞出的机制边界**：`Log()` 走 `Dispatcher.UIThread.Post`，而本层的 `Pump` **只推渲染定时器、
  不抽派发队列**（`UiTestHost.TickRenderTimer`）⇒ 首跑读数里「日志条数」**恒为 0**，连 ctor 那条
  都进不来。⇒ 写进表与探针注释：**凡"把效果 Post 出去"的处理器本层原理上读不到**（「面的边界」
  第一形态在本层机制上的实例，**不是缺陷、不新增登记项**）。这也是"下一个扩面的人必须先问的一句"。
- **负控 2 条**：NC-1 删 XAML 里的 `SelectionChanged=` ⇒ 两条面板读数一起红；
  NC-2 处理器只改一半 ⇒ 只有第二条红（证明"两条一起声明"承重）。
  ⚠ 普查表那条腿（升格后忘删 ⇒「陈账要清」）阶段 126 的 NC-5 已打过 ⇒ 本阶段**刻意省掉这次重复注入**。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **675**（已跟踪 ∪ 未跟踪）；步 5 逐程序集
`44 / 44 / 4 / 78 / 96 / 2140 / 315 / 610`（合计 **3331**）＋ 步 6 独占 `8` ＋ `2`
＝ **3341 通过 / 0 失败**（阶段 126 为 3340，**+1**）；固件 host `[9/9]`。
**面**：11 宿主 / 61 条声明 / 53 个控件 / 50 个名字；面外 **14 个视图 / 61 个接线控件**。

**面外还剩 14 个视图 / 61 个接线控件**，其中 A 档已清零（B 档 5 个要人造观测面、C 档 1 个要两条机制、
其余是 `PropertyPanelView` 那 31 个与"零选择接线"的视图）。仍不可在本环境闭环的余项不变：
`#45`、B/C 档所需的两类机制、`sunxifb` 24bpp 实机像素验证与 RAM/RK3506 预算（**需硬件**）。

## 阶段 128（2026-09-28）—— 第五层加第 12 个宿主 ＋「事件观测层」首次落地（含一次弯路撤回）

**一句话**：按用户拍板实现"夹具观测事件"这一层（`EventObserved`）并落到一个**活的**视图
（`LayerPanelView`）；中途第一版挑中的 `VariableBrowserView` 被查出是**死面板** ⇒ 撤回、并据此
给"挑宿主"补上**第 ⑤ 条判据**、登记 **`#71`**。逐条见登记表 §50。

- 🩸 **弯路（本阶段最值钱的产出）**：第一版挑 `VariableBrowserView`（B 档、字符串载荷、看着最便宜），
  判据全绿、读数漂亮 —— 回头核对才发现 **`new VariableBrowserView()` 全仓零命中**、
  它**不在 `HmiDockFactory` 注册的面板里**、它的 `VariableSelected` / `VariableDoubleClicked`
  **两个事件都零订阅** ⇒ 用户到不了的面板。**判据全绿也可能是"判了一个用户到不了的东西"**。
  ⇒ 撤回升格 ＋ **第 ⑤ 条判据：这个视图在生产里真的会被构造出来吗**（`grep -rn "new XxxView(" src/`
  ＋ 在不在 `HmiDockFactory` 那批里）。这与阶段 118 的「构造 ≠ 驱动」是同一族的**下一层**。
- **事件观测层**：载荷存 `HostContext.Observed`（**一趟一个**，不挂静态字段）；**首次读取时**才挂载
  （`Probe.Read` 保持幂等、runner 不必认识它）；**声明里必须点名判的是哪一跳**。顺带补了
  `TreeSelectedIndex`（`TreeView` **没有** `SelectedIndex` —— 与 `ListBox`/`ComboBox` 不同）。
  ⇒ 它**不放松判据**：读到的仍是"处理器写出去的东西"，只是落点从控件属性换成事件参数。
- **第 12 个宿主 `LayerPanelView`**（5 条声明全部 `Moves`）：① 选中 ⇒ 事件载荷（`选中图层` 空→状态灯）；
  ②③ `BtnMoveUp`/`BtnMoveDown` ⇒ 模型 `LayerIndex` 0↔1；④⑤ `BtnLockAll`/`BtnUnlockAll` ⇒ `锁定数` 0↔1。
  ⚠ ②③ **依赖 ① 留下的选中**（表的顺序是前提）；行内两个小按钮是代码构造的、**无名字** ⇒ 不进本面。
- **控制 4 条**：NC-1 摘 XAML 的 `SelectionChanged=` ⇒ 红在**载荷腿**；NC-2 摘 `BtnLockAll` 的
  `Click=` ⇒ 红在 `锁定数`；NC-3 把**观测层本身**拆掉 ⇒ 红在载荷腿（⇒ 观测层承重）；
  **PC1（期望绿）** 摘掉构造函数里往 `DockLayoutState` 的转发 ⇒ **13/13 全绿** ⇒ 把"判不到那一跳"
  从注释里的声明变成**实测事实**。🩸 NC-1 第一版注入（把处理器名改名成不存在的）让 **12/12 全红** ——
  XAML 编译期 `AVLN3000`，而增量 `dotnet test` 没重跑预编译 XAML；负控注入点要落在
  "**合法但语义不同**"的形状上，否则红点不指向期望那条腿。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **675**（已跟踪 ∪ 未跟踪）；步 5 逐程序集
`44 / 44 / 4 / 78 / 96 / 2140 / 315 / 611`（合计 **3332**）＋ 步 6 独占 `8` ＋ `2`
＝ **3342 通过 / 0 失败**（阶段 127 为 3341，**+1**）；固件 host `[9/9]`。
**面**：12 宿主 / 62 条声明 / 54 个控件 / 51 个名字；面外 **13 视图 / 60 控件**。

**下一步要建的两条机制**（都不是"再挑一个视图"）：① **选中嵌套子节点**的手势（解
`ProjectExplorerView` 与 C 档的 `VariablePicker` 那一半）；② 静态守卫按**子目录**定位 `.axaml`
（解 C 档）。另：`AssetManagerView` 只差**铺资产夹具**。仍不可闭环的余项不变：`#45`、
`#71`（需产品口径：接上还是删掉死面板）、`sunxifb` 24bpp 与 RAM/RK3506 预算（**需硬件**）。

## 阶段 129（2026-09-28）—— 第五层加第 13 个宿主 ＋「树里的一条路径」手势

**一句话**：按用户拍板建**选中嵌套子节点**的手势（`SelectPath` / `SelMovesPath`），并用它把
`ProjectExplorerView` 升格成第 13 个宿主；过程中撞出**一条判据口径边界**（静态总线跨测试存活），
登记 `#72`。逐条见登记表 §51。

- **为什么要这条手势**：`SelectIndex` 只能选**根节点**，而 `ProjectExplorerView` 的根是
  `CONFIG_ROOT` / `SCREENS_ROOT` 这类**分组**（处理器只走 else 支、抬 null），真数据在 `Items[i].Items[j]`
  ⇒ 没有它，"按 `Tag` 的**类型**分派"那一半**原理上够不到**（C 档的 `VariablePicker` 同样卡这里）。
  落地：`ClickSpec.SelectPath`（与 `SelectIndex` 互斥，有自检）＋ `SelMovesPath` ＋ `Gesture` 逐级走
  `TreeViewItem.Items`；唯一性键扩成 `(名字, 下标, 路径)`。🩸 **越界一律抛**、不静默（"拨了但什么都没发生"
  与"处理器不干活"在读数上同形）。
- **第 13 个宿主**（3 条声明全 `Moves`）：① `[1,0]`（`Tag` 是 `HmiScreen`）⇒ else 支抬 **null**；
  ② `[1,0,0]`（`Tag` 是 `HmiControl`）⇒ 抬控件 **＋ `BtnDelete.IsVisible` 跟着翻**（两条一起声明不是凑数）；
  ③ `BtnDelete` ⇒ 抬 `DeleteRequested`（⚠ 依赖 ② 留下的选中，表的顺序是前提）。
- 🩸 **判据口径边界（`#72`）**：配置支（`CONFIG:<key>` → `ConfigNodeActivated`）**单跑绿、全量红**，
  红消息 `Cannot show a window with a closed owner.` —— 链路是 `DockLayoutState.RaiseConfigNodeActivated`
  → `Wiring.DockAggregate.cs:300` → `MainWindow.OpenConfigWindow` → `ShowModalDialog`；产品侧不退订是对的，
  但测试进程里先跑的主窗口用例把窗口关了、**订阅还在** ⇒ 那条声明"红不红取决于别的测试跑没跑"
  ⇒ **不能发布**。处置：该支不声明 ＋ 注释写明原因；三条可选修法（测试侧清陈旧订阅 / 独立进程 /
  认下边界）写进 `#72`，本层**不动产品**。**PC2 实测**：摘掉构造函数里三处总线转发 ⇒ **614/614 全绿**
  ⇒ 本宿主判据**不依赖总线**。
- **控制 4 条**：NC-1 摘 XAML `SelectionChanged=` ⇒ 载荷腿 ＋ 可见性腿一起红；NC-2 删 `Click=` ⇒
  `删除请求` 红；NC-3 让路径手势不兑现 ⇒ 路径腿 ＋ 载荷腿红；**PC2（期望绿）** 见上。
  ⚠ NC-2 的第一版注入（改名）又被 `--no-incremental` 的 `AVLN3000` 拦住 ⇒ 阶段 128 那条教训成立。
- 🩸 一处**我自己的凭印象写数**：初值 `删除按钮` 实测**隐藏**（XAML 写死 `IsVisible="False"`），
  我第一稿注释写的是"初值可见" —— 读数把它顶回来了（本会话第三次同族）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **675**（已跟踪 ∪ 未跟踪）；步 5 逐程序集
`44 / 44 / 4 / 96 / 78 / 2140 / 315 / 612`（合计 **3333**）＋ 步 6 独占 `8` ＋ `2`
＝ **3343 通过 / 0 失败**（阶段 128 为 3342，**+1**）；固件 host `[9/9]`。
**面**：13 宿主 / 64 条声明 / 55 个控件 / 52 个名字；面外 **12 视图 / 59 控件**。

**下一步的两条路**：① **静态守卫按子目录定位 `.axaml`**（解 C 档 `VariablePicker`）；
② **铺 `AssetManagerView` 的资产夹具**（解 B 档剩下的一个）。仍不可闭环的余项：`#45`、
`#71`（死面板：接上还是删掉）、`#72`（总线跨测试存活：测试侧清订阅 / 独立进程 / 认下边界）、
`sunxifb` 24bpp 与 RAM/RK3506 预算（**需硬件**）。

## 阶段 130（2026-09-30）—— 补上审计清单里漏掉的两处「轮询等待」，并把量具收成一份

**一句话**：回查发现阶段 123 的目标里点名了**五处**墙钟/轮询脆弱点，而当时只动了**三处** ——
本阶段把漏掉的 `VariableSimulatorTests` / `PreviewRuntimeServiceTests` 收掉，顺手把「等待」这件
量具从 `Core.Tests` 搬进共享的 `HmiEditor.TestSupport`；3 条正控实测超时并红。逐条见登记表 §52。

- 🩸 **先认账**：§45.2 的表只有三行，而审计点名五处 ⇒「改了几条」不等于「清单有几条」，
  点名清单必须**逐条对账**（本仓"数字要能被重数"那条纪律的又一次实例）。
- **量具收成一份**：`TestWait` 从 `tests/HmiEditor.Core.Tests/Support/` 搬到
  `tests/HmiEditor.TestSupport/`（namespace 跟着改）—— 原先只有 `Core.Tests` 用得到，
  别的程序集各自手写同一件事（`SpinWait.SpinUntil(...)`、`while (…) Thread.Sleep(5)`）。
  `Core.Tests` 本就有 `<Using Include="HmiEditor.TestSupport" />` ⇒ 4 个使用点一行不用改。
  ⚠ `TestSupport` 因此**第一次有包依赖**（xunit，断言失败要抛 `TrueException`）—— 它只被
  4 个**测试**程序集引用、无产品项目引用 ⇒ 不会把 xunit 带进产物。`Simulator.Tests` 此前连
  `TestSupport` 都没引（这正是它手写轮询的直接原因）⇒ 补 `ProjectReference` + `<Using>`。
- **四处处置**（判据强度只升不降）：3 处 `SpinWait.SpinUntil(…, 5000)` → `TestWait.Until(...)`
  （**同一条等待、同一个 5s 预算**，只是不再各写一份）；`PreviewRuntimeServiceTests` 那处
  **手写 deadline 轮询**（`while` + 自建 3s）→ `TestWait.Until`（**预算 3s → 5s，强度上升**，
  且失败消息点名"在等什么"）；另删掉 `Simulator_StartStop_Works` 里一句**与断言无关**的
  `Thread.Sleep(50)`（前后两条断言判的都是 `IsRunning`）。
- **正控 3 条**（注入产品代码 + SHA256 还原）：PC-A 摘 `ValuesUpdated?.Invoke` ⇒
  `等待超时（5000ms）：ValuesUpdated 事件应在 5s 内触发`（实测 5.15s）；PC-B `OnTick` 首行
  `return` ⇒ 两处等 `TickCount` 的判据**一起**红（5.13s / 10.12s）；PC-C 摘 `InvalidateCanvas()`
  ⇒ `等待超时（5000ms）：仿真值更新后应请求画布重绘`（5.28s）。
- **两处刻意边界**：① 需断言"某件事在 T 毫秒内**没有**发生"时轮询给不出同步点，要靠**探针事件**
  （`TestWait` 的 xmldoc 早写明）；② 本阶段**不**扫全仓所有 `Thread.Sleep` —— 有些是**制造刺激的节拍**，
  不是"等待产品状态"（与 `TestAssertionStyleGuardTests` 的边界一致）。

**§45（阶段 123 目标）至此逐条闭环**：(1) 路径卫生未跟踪盲区 —— 本阶段重验（7 档自检 ＋ 真实未跟踪
脏文件被点名后复绿）；(2) 五处墙钟/轮询 —— 阶段 123 三处（差值/比值）＋ 本阶段两处 ＋ 3 条正控；
(3) `#67①` 复核 —— §45.3 只登记不动产品；(4) `#66②`/`#67②` 的**带推荐项**问题已交给用户（§45.4），
用户取了两条推荐项 ⇒ 阶段 124 落地闭环（§46）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **988 行**（非空 **951**）；
脚本 **29**；路径卫生 **675**（已跟踪 ∪ 未跟踪）；步 5 逐程序集
`44 / 44 / 4 / 96 / 78 / 2140 / 315 / 612`（合计 **3333**）＋ 步 6 独占 `8` ＋ `2`
＝ **3343 通过 / 0 失败**（与阶段 129 **同数** —— 本阶段只换等待写法、不增删用例）；固件 host `[9/9]`。

## 阶段 131（2026-09-30）—— 第五层加第 14 个宿主（首个**子目录**宿主 `Editors/VariablePicker`）

**一句话**：把 C 档唯一的那个视图收进面 —— 先给静态守卫补上"按 `Views/` 相对路径定位 `.axaml`"
（子目录宿主此前一升格就"文件不存在"），再用阶段 129 的 `SelMovesPath` 驱动它**两层树**里的叶子。
逐条见登记表 §53。

- **守卫口径**：宿主名 = 「相对 `Views/` 的路径」（子目录用 `.` 分隔），一个字符串**干两件事** ——
  `Assembly.GetType("HmiEditor.App.Views." + 名字)` 与 `.`→`/` 后的文件路径。新增
  `RelativeViewPath` / `SimpleTypeName`（前者给路径、后者给 `partial class` 索引的类名）；
  判据二的宿主集合也要 `.Replace('.', '/')` 才能与普查表的键对齐（**第一跑就撞上了这处**，
  不统一会把"已升格的视图"误报成陈账）。
- **第 14 个宿主 4 条声明（全 `Moves`）**：① `BrowseBtn` ⇒ 建树（`LoadVariables()` 只存管理器、
  **不建树**）＋ 开弹层；②③ `SelMovesPath(VarTree, [1,0]/[1,1])` ⇒ 显示框/弹层/载荷**三条腿一起**、
  两条合起来才钉住"写的是被选中的那一个"；④ `ClearBtn` ⇒ 显示框与载荷一起回到"无"。
- 🩸 **本阶段我自己犯的两处「凭印象写」**（都被读数顶回来，已更正并留路标）：
  ① **"ASCII 名排在最前"是错的** —— 实测（ICU zh-CN）是 温度 < 液位 < NcAlpha < NcBravo，
  Han 在 Latin **之前**；⇒ 改用**组键锚定**（`null` 组键恒排第一 ⇒ 自建组恒在 `[1]`），
  换文化时断言会**红**而不是静默错位；
  ② **接线形态写反了** —— 我在声明里写"XAML 属性接线"，而 `VariablePicker.axaml` 里根本没有那条属性，
  它订在**构造函数**里（同族的 `VariableBrowserView` 才是 XAML 那个形态）。
  另有一处口径修正：探针从"整棵树的叶子数"改成"**己方那两条**叶子"（树里还含 17 条**产品系统变量**，
  数总数等于把宿主钉在系统变量表的条数上）。
- **负控 3 条**：NC-1 退回"不支持子目录"的旧口径 ⇒ 判据一红
  （`扫描失败 —— FileNotFoundException: 宿主 Editors.VariablePicker 的 …Editors.VariablePicker.axaml 不存在`）
  ⇒ 路径口径**承重**；NC-2 摘构造函数里 `VarTree.SelectionChanged +=` ⇒ 两条 `SelMovesPath` 的三条腿一起红；
  NC-3 摘构造函数里 `BrowseBtn.Click +=` ⇒ 弹层 ＋ 己方叶子数一起红。
  ⚠ `ClearBtn`（④）**刻意没单独注入**：与 NC-3 是同一条 ctor 里、同一种接线，重复注入只是凑数。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **675**（已跟踪 ∪ 未跟踪）；步 5 逐程序集
`44 / 44 / 4 / 96 / 78 / 2140 / 315 / 613`（合计 **3334**）＋ 步 6 独占 `8` ＋ `2`
＝ **3344 通过 / 0 失败**（阶段 130 为 3343，**+1**）；固件 host `[9/9]`。
**面**：14 宿主 / 66 条声明 / 56 个控件 / 53 个名字；面外 **11 视图 / 58 控件**。

**C 档至此清零**（子目录 + 树叶子两条机制都建起来了）。下一步仍是两条纯软件路径：
① 铺 `AssetManagerView` 的资产夹具；② `FaceplateLibraryView`（4 按钮 ＋ 1 个 ListBox，载荷待查）。
仍不可闭环的余项：`#45`、`#71`（死面板口径）、`#72`（静态总线跨测试存活）、
`sunxifb` 24bpp 与 RAM/RK3506 预算（**需硬件**）。

## 阶段 132（2026-09-30）—— 第五层加第 15 个宿主（`AssetManagerView`，B 档剩下的一个）

**一句话**：收 B 档剩下的两个之一。它的选择处理器**只抬 `AssetSelected`**（事件型数据面 ⇒ 用阶段 128
的 `EventObserved`），而 `depot.Assets` 原本是**空的** ⇒ 主要工作变成**铺资产夹具**，
顺带把"混排列表"（分组标题 ＋ 资源行）这条边界钉住。逐条见登记表 §54。

- **第 15 个宿主 8 条声明**（3 `Moves` ＋ 1 `Inert` ＋ 4 `Never`）：①②③ `AssetList` 三个下标各拨一次
  —— **下标 0 是分组标题**（`as AssetRow` 得 null ⇒ 载荷 `(null)`）、1/2 是两张图 ⇒
  三条合起来才说明"下标 → 身份"的映射来自列表自己；④ `BtnRefresh` = `Inert`
  （`Refresh()` 重建同一份列表并按 Id 恢复选中 ⇒ 数据面不动；状态行走字典 ⇒ 如实写明
  "摘掉 `Click=` 本层也不会红"）；⑤–⑦ 三个导入按钮 = `Never`（`async void` ＋ 文件选择器平台桩）；
  ⑧ `BtnAiIcon` = `Never`（🩸 只抬一个事件、本可判，但它经构造函数转发到**静态总线** ⇒ `#72` 第二实例）。
- **夹具** `SeedAssetFixture`：铺 2 张图 ＋ 1 个字体（`AssetType` 枚举序 Image, Font, Language, Svg
  ⇒ 下标就是身份）。不铺这一层，列表里连一行都没有 ⇒ 三条选择声明全成「null → null」的零信息断言。
- **负控 2 条**：NC-1 摘 `.axaml` 里 `AssetList` 的 `SelectionChanged=` ⇒ 三条选择声明的载荷腿一起红；
  NC-2 把"标题也当资源"（`?? _items.OfType<AssetRow>().FirstOrDefault()`）⇒ **只有下标 0 那条**红
  ⇒ 「下标 → 身份」这条腿承重。⚠ `EventObserved` 观测层**刻意不再注入**（阶段 128 的 NC-3 已证明过一次）。
- 🩸 **一处旁证（不是判据依据）**：为把"`BtnAiIcon` 踩 `#72`"从推断变成实测，临时把它声明成 `Moves`
  ＋ 加观测探针 ⇒ **全量 UI 套件 10 分钟没跑完**（超时被杀）。与 `#72` 机理一致，但**不是干净的红**、
  也没再跑一轮区分"是不是别的 flake"⇒ 只当旁证记着（`#72` 行末已补记）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **675**（已跟踪 ∪ 未跟踪）；步 5 逐程序集
`44 / 44 / 4 / 96 / 78 / 2140 / 315 / 614`（合计 **3335**）＋ 步 6 独占 `8` ＋ `2`
＝ **3345 通过 / 0 失败**（阶段 131 为 3344，**+1**）；固件 host `[9/9]`。
**面**：15 宿主 / 69 条声明 / 57 个控件 / 54 个名字；面外 **10 视图 / 57 控件**
（⚠ 两者恰好相等是巧合，别当成口径）。

**B 档只剩 `FaceplateLibraryView`**（4 按钮 ＋ 1 个 ListBox，载荷与写入面待实测）。
仍不可闭环的余项：`#45`、`#71`（死面板口径）、`#72`（静态总线跨测试存活，现已**两个实例**）、
`PropertyPanelView`（阶段 118 三条实测否过）、面外零选择接线的视图、
`sunxifb` 24bpp 与 RAM/RK3506 预算（**需硬件**）。

## 阶段 133（2026-09-30）—— 第五层加第 16 个宿主（`FaceplateLibraryView`）**B 档收口**

**一句话**：B 档最后一个。它的**按钮面几乎判不了**（四条全是平台桩或"经静态总线去别的窗口开对话框"），
能判的只有选择族；过程中撞到「静态共享状态**覆盖夹具数据**」这一新形态，处置是**换一种断言**
（从"断言是哪一个"换成断言"**载荷随选中**"）。逐条见登记表 §55。

- **第 16 个宿主 6 条声明**（2 `Moves` ＋ 4 `Never`）：①② `TemplateListBox` 两项各拨一次 ——
  处理器**首行 `if (Parent == null) return;`**（本层第一次遇到"处理器依赖**已装配**"的形状，
  本宿主把它装进 `Window` 并 `Show()` 才跑得起来）；③–⑥ 四个按钮全 `Never`：
  `BtnNewTemplate`/`BtnEditTemplate`/`BtnAiTemplate` 都是"只抬事件、但构造函数转发到**静态总线**"
  ⇒ 落到 `CreateTemplateFromSelection()` / `OpenTemplateEditor()` / `AiTemplateDialog.ShowDialog`
  （`#72` 那一族的第三/四/五个实例），`BtnDeleteTemplate` 走 `ConfirmAsync` 平台桩。
- 🩸 **新形态：共享状态不只"打到死窗口"，还会覆盖夹具数据**。首跑全量当场红：期望 `NcFaceA/NcFaceB`、
  实测载荷是 **`电机面板` / `启动/停止按钮组`**、`模板条数=4`（夹具只给 2 条）—— 根因是本视图
  `OnAttachedToVisualTree` 会从**静态** `DockLayoutState.Templates` 自初始化（产品行为：ViewLocator
  每次 Build 新实例）⇒ 夹具在 `Build` 里 `LoadTemplates` 的两条在装配时被共享状态覆盖。
  🩸 与 `#72` 同一族的**另一个形态**：`#72` 是"陈旧订阅打到已关闭窗口"（异常/挂死），
  这里是"**静态共享数据**悄悄换成别人的数据"（读数静默变样）。不新增登记项（同一条账）。
- **处置（强度不降）**：`EventObserved` 加 `isVoice` 可选参数（载荷只进读数）＋ 另立**一致性探针**
  `载荷随选中`（比"事件载荷"与"列表当前选中项的名字"），报 `无载荷/一致/不一致` 三档 ——
  与 `ProjectSettingsView.ModelRoleFollowsCombo` 同形（既有先例，不是新发明）。
  **NC-1** 摘 `.axaml` 的 `SelectionChanged=` ⇒ `期望 一致、实际 无载荷`；
  **NC-2** 恒定抬第一个 ⇒ `期望 一致、实际 不一致`。
- ⚠ 本宿主是本层**最薄**的一个（`live = 2`，正好压在 `live >= 2` 下界）：它的价值是
  "四个具名按钮全部被分类 ＋ 选择族有行为判据"，不是"有 4 条能动"。
- **B 档至此清零**（A 档清零于 s127、C 档清零于 s131）；面外只剩 9 个视图 / 56 个接线控件，
  其中 `PropertyPanelView` 31 个（阶段 118 三条实测否过），其余是零选择接线或已写清代价的视图。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **675**（已跟踪 ∪ 未跟踪）；步 5 逐程序集
`44 / 44 / 4 / 96 / 78 / 2140 / 315 / 615`（合计 **3336**）＋ 步 6 独占 `8` ＋ `2`
＝ **3346 通过 / 0 失败**（阶段 132 为 3345，**+1**）；固件 host `[9/9]`。
**面**：16 宿主 / 71 条声明 / 58 个控件 / 55 个名字；面外 **9 视图 / 56 控件**。

**下一步可选**：① 处理 `#71`（`VariableBrowserView` 死面板：接上还是删掉）；
② 处理 `#72`（静态总线/静态数据跨测试存活：测试侧清订阅 / 独立进程 / 认下边界——现已 5 个实例）；
③ 继续扩面（`AnimationEditorView` 8 个选择 + 7 按钮，但它是批量写的对话框，需先量）；
④ 收口汇总。

## 阶段 134（2026-09-30）—— 处置 `#72`：测试侧清静态总线订阅，五条"抬总线"的腿变可判

**一句话**：`#72` 的三条修法里选了**修法①「测试侧清陈旧订阅」**（`StaticBusHygiene.Silence()`，反射清空
`DockLayoutState` 的**全部**委托型静态字段），并在每条宿主跑前调用 ⇒ 五条此前只能记 `Never` 的腿
改记 `Moves`，全量套件 617/617 绿；量具自带确定性自检 + 负控。逐条见登记表 §56。

- **为什么用反射、为什么放测试侧**：给产品加 `ResetForTests()` 要把 **45 个**事件逐个赋 null
  ⇒ "抄一份清单"（加新事件忘了抄就静默漏掉）；按"静态 ＋ 委托类型"筛 ⇒ **新增事件自动在面内**。
  产品侧**不需要**退订（主窗口活到进程结束）⇒ 这是**测试进程卫生**，不动产品。
  **安全前提写成机制**：本类只被 `HostButtonEffectUiTests` 调用，而它挂在
  `[Collection("Language-global")]`（`DisableParallelization`）⇒ 清总线时**没有别的集合在跑**。
  ⚠ 刻意**不**清数据（`DockLayoutState.Templates` 是属性不是事件）——那是另一个形态，由宿主侧的
  **一致性探针**绕开（s133），本阶段不声称解决。
- **效果（五条腿 `Never` → `Moves`）**：`ProjectExplorerView` 配置支（`配置项` 空→`project_settings`，
  `删除按钮` 保持隐藏、`选中控件` 不动 —— 提前 return 的可见后果）；`AssetManagerView.BtnAiIcon`；
  `FaceplateLibraryView` 的 `BtnNewTemplate`/`BtnAiTemplate`（请求载荷 空→「已抬」）与
  `BtnEditTemplate`（载荷 → 当时选中的模板名，由**一致性探针** `编辑载荷随选中` 断言）。
  ⚠ 仍记 `Never` 的两类**与 `#72` 无关**：文件选择器平台桩（三个导入）、`ConfirmAsync` 平台桩。
- **判据 + 负控**：新增 `StaticBusHygieneTests`（**2 条**）——装一个**会抛的**陈旧订阅 ⇒ 未清空时抬事件
  **必须抛**、清空之后**不许再抬**、且清空**不能**屏蔽"之后新装的"订阅；**NC（确定性）**：把 `Silence()`
  改成空转 ⇒ 自检 **2/2 红**（红消息就是那条陈旧订阅抛的）。**正向证据**：清总线在位 ⇒ 那五条腿
  **可以发布**（阶段 134 之前它们根本发不出去）。
- ⚠ **旁证（不是判据依据）**：摘掉宿主跑前的 `Silence()` ⇒ 全量套件 **400 秒内没跑完**（挂住，不是
  干净地红；与 s132 那次旁证同形）；**过滤跑不出那个顺序**（实测 21/21 绿）⇒ 真正的 NC 做在**量具自检**上。
- **诚实边界**：静态**数据**不在本修法内；清总线的安全依赖"独占集合"这条机制；反射依赖"事件是
  field-like"（当前 45 个全是，但**没有判据守着**，属自陈边界）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **675**（已跟踪 ∪ 未跟踪）；步 5 逐程序集
`44 / 44 / 4 / 96 / 78 / 2140 / 315 / 617`（合计 **3338**）＋ 步 6 独占 `8` ＋ `2`
＝ **3348 通过 / 0 失败**（阶段 133 为 3346，**+2**）；固件 host `[9/9]`。
**面**：16 宿主 / 72 条声明 / 58 个控件 / 55 个名字；面外 **9 视图 / 56 控件**。

**下一步可选**：① `#71`（`VariableBrowserView` 死面板：接上还是删掉）；② 继续扩面
（`AnimationEditorView` 8 选择 + 7 按钮，但它是批量写对话框，需先量）；③ 收口汇总
（s125→s134 的成果、`#71`/`#72` 的现状、剩余 9 视图 / 56 控件的构成）。

## 阶段 135（2026-09-30）—— 把「视图可达性」从人肉纪律变成机器守卫（登记 `#71` / 新开 `#73`）

**一句话**：`#71` 的产品口径（接上还是删掉）仍归用户；本阶段把那次事故的教训做成**判据** ——
新增 `UnreachableViewGuardTests`（每个视图都必须有构造点，否则进登记账本，两侧都断言），
首跑就带出**第二个**死视图（`PrototypeCanvas`，新登记 `#73`）。逐条见登记表 §57。

- **守卫形态**：`src/HmiEditor.App/**/*.axaml` 里每个带 `x:Class` 的视图（46 个 —— 🩸 **阶段 137 就地更正：实测 45**，原文那个数把 `App` 也数进去了；`ViewsByClassName()` 的口径是「带 `x:Class` 的 `.axaml` **减去 `App`**」，删掉两个死视图之后是 **43**。见登记表 §59.3③）都必须在产品代码里
  有构造点 —— `.cs` 的 `new X(…)` / `new Ns.X(…)`，**或** `.axaml` 里被当标签用
  （`<editors:VariablePicker/>` 那种）。**两侧都断言**：新出现到不了的视图 ⇒ 红；
  账本里有而它已能构造 ⇒ 红（陈账要清，否则账本变永久豁免）。账本每条**必须**写
  **理由 ＋ 触发条件**（没有触发条件的账本行＝永久豁免）。
- **fail-closed 三层**：视图数 ≥40、产品源文件 ≥100、"认得出构造点"的视图 ≥40
  —— 扫描面或识别逻辑一塌，三条各自报红。
- **抹噪声**：`.cs` 走 `SourceScanner.MaskLiterals`（注释与字面量内容都抹）、`.axaml` 走
  `SourceScanner.StripXmlComments` ⇒ "注释/字符串里提到过"不算构造点；这四档反控与三档正控
  都在 `TheScanner_SeesBothConstructionForms_AndOnlyThose` 的合成语料里钉着。
- **账本两条**：`VariableBrowserView`（`#71`，被 `Editors/VariablePicker` 取代）与
  `PrototypeCanvas`（**`#73`，守卫首跑带出来的第二个**：零构造、零事件接线、只剩一个本地化键提到它，
  被 `DesignCanvas`/`DesignCanvasView` 取代）。🩸 这正说明守卫值得存在：`#71` 当年是**人肉**发现
  （还是在把它升格成宿主、判据全绿之后才回头核对），而这条守卫**首跑**就把两个都摊开了。
- **负控 2 条**：NC-1 给 `VariableBrowserView` 加一个构造点 ⇒ 红「账本里有它，但它**现在有构造点**了
  —— 陈账要清」；NC-2 拆掉扫描器的 XAML 标签那一路 ⇒ 红并**点名** `VariablePicker`
  （"只经 XAML 构造"这条形态必须留在扫描器里，否则它会被误判成死面板）。
- 🩸 **顺带记一次"判据网工作"**：第一版自检里我写了 `Path.GetTempPath()` ⇒ 门禁步 [5] 被
  `TestTempDirUsageGuardTests` 当场拦下（本机 `/tmp` 仅 10 MB），改用 `TestTempDir.NewDir` 后复绿
  （`GATE_EXIT=1 → 0`）。不是新发现，但它演示了「**量具自己也要被量**」。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **675**（已跟踪 ∪ 未跟踪）；步 5 逐程序集
`44 / 44 / 4 / 78 / 96 / 2140 / 315 / 619`（合计 **3340**）＋ 步 6 独占 `8` ＋ `2`
＝ **3350 通过 / 0 失败**（阶段 134 为 3348，**+2**）；固件 host `[9/9]`。
**面**（第五层本阶段未动）：16 宿主 / 72 条声明 / 58 个控件 / 55 个名字；面外 9 视图 / 56 控件。

**下一步可选**：① 给 `#71`/`#73` 一个产品口径（接上 or 删掉 —— 两条现在都有守卫盯着，
接上会自动逼账本更新）；② 继续扩面（`AnimationEditorView` 8 选择 + 7 按钮，先量它是不是批量写）；
③ 收口汇总（s125→s135 的成果与剩余清单）。

---

## 阶段 136（2026-10-01）—— 第五层加第 17 个宿主（`AnimationEditorView`）＋ 判据逼出的产品修复（`#74`）

**一句话**：把面外**剩下的最大一块选择族**（8 个带 `SelectionChanged` 接线的控件，全在一个文件里、
此前一条判据都没有）升格成第 17 个宿主；表格**首跑**就把一条产品缺陷顶出来（`#74`：
关键帧缓动下拉的收尾重选是**死代码**），产品侧同批修 5 行代码（＋14 −5，其中 10 行是注释）。逐条见登记表 §58。

- **挑它的理由**：**面覆盖**（8 条零判据的选择接线），不是「按个数随手挑一刀」（阶段 118 否过后者）。
  三条判据对上：写入面窄（只写 `_currentAnimation` 一个对象，不是 `PropertyPanelView` 那种批量写）／
  零驱动（`grep` 零命中）／可达（`HmiDockFactory` 真构造，阶段 135 的守卫机器盯着）。
- **表 16 条**（`Moves` **12** / `Inert` **1** / `Never` **3**）＋ 12 枚探针，其中两枚是本阶段新加的：
  `列表行数`（模型条数对筛选**不敏感** ⇒ 少了它「筛选重建了列表」与「没接线」同形）、
  `启用位`（`ChkEnabled` 属按钮面、必须分类，而面里原先没有探针能看见 `IsEnabled` 被写）。
- 🩸 **首跑红 ⇒ 产品修复 `#74`**：`SyncKeyframeFromUi()` 尾部「选中排序后的关键帧」的循环比对的
  `_selectedKeyframe`，被它自己上面那句 `RefreshKeyframeList()`（`Items.Clear()` ⇒ `SelectionChanged`
  ⇒ `OnKeyframeSelectionChanged` 的 else 分支）**置成了 null** ⇒ 循环永远不命中（死代码）。
  症状：**改一次关键帧缓动函数，面板当场收起、选中丢失**（模型写对了、界面丢了）。
  修法：开头抓局部引用 `var keyframe = _selectedKeyframe;`，三处写入＋尾部循环都改比它（代码 5 行）；
  判据侧把「面板仍显示 ＋ 选中仍是 0」写成期望值 ⇒ **撤掉修复就红**（nc136-3）。
  这是本层第三次由判据逼出产品修复（阶段 113 `#66`、阶段 114 `#65` 回归之后）。
- **两条顺序性都是首跑逼出来的**：筛选腿必须排在 `BtnNewAnimation` **之后**（筛空列表会连选中
  一起清掉 ⇒ `_currentAnimation` 变 null），而三条「改夹具那条动画」的腿必须排在它**之前**
  （新建会把当前动画换成新那条）。
- **负控 4/4**：摘接线（`CmbAnimationProperty` 那条 `+=` 删掉）／接错处理器
  （`BtnAddKeyframe` 改接 `OnNewAnimation`）／撤掉 `#74` 的修复（比回 `_selectedKeyframe`）／
  摘一条选择族声明（`SelInert("CmbTargetControl", …)`）。前三支各 1 红 17 绿、红在点名的那条腿上；
  **第四支静态守卫红、运行期 18/18 全绿** —— 又一次直接证明「运行期判不出『接线了却没分类』」。
  还原走备份、SHA256 逐字节相同。
- 🩸 **顺带一次事故（量具的纪律）**：第一版负控脚本用 `git checkout -- <文件>` 还原，把工作树里
  **未提交**的本阶段改动（视图修复 ＋ 整张宿主表）一起抹掉了；第二版改成备份还原 ＋ 末尾 SHA256 核对。
  `git checkout` 的语义是「回到 HEAD」，不是「回到跑这一支之前」。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **675**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2140 / 315 / 620`（合计 **3341**）
＋ 步 6 独占 `8` ＋ `2` ＝ **3351 通过 / 0 失败**（阶段 135 为 3350，**+1**）；固件 host `[9/9]`。
**面**：宿主 **17**、声明 **81**、按宿主去重控件 **66**、全仓按名字去重 **63**（阶段 135 是 16 / 72 / 58 / 55）；
**面外**：**8 视图 / 48 控件**（阶段 135 是 9 / 56）。**产品代码**：`AnimationEditorView.axaml.cs` ＋14 −5（其中 10 行是注释）。

**下一步可选**：① 给 `#71`/`#73` 一个产品口径（接上 or 删掉）；② 面外剩下的 8 个视图里
`PropertyPanelView`(31) / `RecipeEditorView`(5) / `SimulatorView`(5) 已各有写清代价的理由，
其余是零选择接线；③ `#72` 的**静态数据**那一半（订阅已清、共享的 `Templates` 等数据仍在）；
④ 收口汇总（s125→s136）。

---

## 阶段 137（2026-10-01）—— 删掉两个死视图（`#71` / `#73`）＋ 修好可达性守卫"视图已删"那一档的报错

**一句话**：用户拍板把两个"用户到不了"的视图**都删掉** —— `VariableBrowserView`（`#71`）与
`PrototypeCanvas`（`#73`）；文件、文案键、账本**三层一起动**，并且删的过程里撞出那条新守卫
（阶段 135 建的）**报错不准**的一档（"视图已删"被报成"现在有构造点"）⇒ 修好并给它补了自检。
逐条见登记表 §59。

- **为什么删**：`PrototypeCanvas` 是硬编码玩具（英文按钮字面量、写死坐标、`Brushes.DodgerBlue`、
  **不读项目模型**），生产画布是 `DesignCanvasView`；`VariableBrowserView` 不是玩具，但用途已被
  内联的 `Editors/VariablePicker` 覆盖，且它那半个卖点（拖放到属性，`HmiVariableBinding`）
  **全仓零消费者**（从未接上过）。ADR 把它列为计划特性 ⇒ 放弃那条路线必须由用户拍板。
- **三层一起动**：① **文件** —— `git rm` 4 个（两视图各 `.axaml` ＋ `.axaml.cs`）；
  ② **文案键** —— 删 7 个**独占**键 ＋ 对应 7 个 `Lang` 常量 ＋ 两本字典各 7 条 ＋ 补键工具 1 条映射；
  ③ **账本** —— `UnreachableViewGuardTests.Unreachable` 两行删掉（**现存 0 条**）。
  ⚠ 保留：`browser.search.hint` / `browser.select.var`（与 `VariablePicker` 共用）、
  `canvas.drag.hint`（与 `DesignCanvasView` 共用）、`win.prototype.canvas`（还活着，见 `#75`）。
- **`UnusedBaseline` 1108 → 1104**：7 个里只有 **4** 个本来在"无代码引用"这一集
  （另 3 个有代码引用）—— 「删了几个」要按**本集口径**数，不是按删掉的常量总数数。
- 🩸 **顺带修好守卫自己的一个报错**：账本清空前后，反方向只有"现在有构造点"一句；而这次的真实
  情形是**视图被删**，那句话会把人指去找一个**根本不存在的构造点**。对账逻辑抽成 `Reconcile(...)`，
  **三档分开报**（未登记 / 已接上 / **已删**），并补自检 `TheLedgerReconciliation_SeesAllThreeCases`
  （三档反控 ＋ 一档正控）—— 理由：**账本清空之后，反方向在真实仓库里没有活见证**。
- **新登记 `#75`**（纯文案遗留）：菜单 `MenuPanelCanvas` 的标题键 `win.prototype.canvas` 显示
  「原型画布」，而它打开的是**设计画布**（面板标题走 `dock.title.canvas` ＝「设计画布」）。
  刻意不顺手改：用户可见文案 ＋ 改键名要动两本字典，属产品口径。
- 🩸 **顺带更正旧散文**：§57 / 阶段 135 写的「`x:Class` 视图 **46** 个」实测是 **45**
  （`ViewsByClassName()` ＝ 带 `x:Class` 的 `.axaml` 减去 `App`）；现在 **43**。
- **负控 4/4**：① 账本加回一条已删视图 ⇒ 红在**新的第三档**文案；② 只把 `browser.ready` 加回两本字典
  ⇒ 可达性守卫点名；③ 常量加回而字典条目已删 ⇒ `LangKeyCoverageTests` 红；④ `UnusedBaseline`
  改回 1108 ⇒ 基线不匹配红（降了必须改基线）。还原 SHA256 逐字节相同。
- **面下界随视图数一起降**：`ViewSmokeUiTests` 15 → **13**、`WindowViewSmokeUiTests`
  48 → **46**（32 窗口 + 13 UserControl + 1 豁免）；面外普查 8 视图 48 控件 → **7 / 47**。
- 🩸 **量具咬到自己一次（新形状）**：负控脚本把 `Lang.cs` 路径存进 shell 变量 **`LANG`**，而它是
  **已导出的环境变量** ⇒ `dotnet` 继承到非法 locale，**ICU 初始化当场段错误**（4 支各崩 3 次、
  日志空，手敲同一句却全绿）。改名 `LANGCS` 后 4/4 通过 —— 踩的是**环境变量命名空间**，
  没有任何判据盯着，只能靠"手敲能过、脚本里崩"这种对拍发现。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **671**（阶段 136 是 675，**−4 就是删掉的 4 个文件**）；步 5 逐程序集
`44 / 44 / 4 / 96 / 78 / 2140 / 315 / 619`（合计 **3340**）＋ 步 6 独占 `8` ＋ `2` ＝
**3350 通过 / 0 失败**（阶段 136 是 3351，**−1**）；固件 host `[9/9]`。
**视图可达性**：`视图 43 个 / 有构造点 43 个 / 到不了 0 个（账本 0 条）`。
**面**：17 宿主 / 81 声明 / 66 控件 / 63 名字（未动）；**面外 7 视图 / 47 控件**；`.axaml` 总数 **45**。

**下一步可选**：① `#75` 那栏文案（要不要把「原型画布」改成「画布」）；② `#72` 的静态**数据**那一半；
③ 面外剩下 7 个视图（`PropertyPanelView` 31 / `RecipeEditorView` 5 / `SimulatorView` 5 已各有写清代价的理由）；
④ 收口汇总（s125→s137）。

---

## 阶段 138（2026-10-01）—— `#75` 收尾：菜单那一栏「原型画布」→「设计画布」，键名也改对

**一句话**：阶段 137 删掉 `PrototypeCanvas` 之后，它留下的唯一一处名字还挂在「面板」菜单上
（显示「原型画布」、点开的却是设计画布）⇒ 用户拍板改；键 `win.prototype.canvas` →
**`win.design.canvas`**、常量 `WinPrototypeCanvas` → **`WinDesignCanvas`**、
两本字典的值 → 「设计画布」/「Design Canvas」，四处一起动（各 1 行）。逐条见登记表 §60。

- **新键名落在 `win.*` 家族**（与 `win.project.explorer` / `win.faceplate.lib` 同形），值就是那个面板
  自己的名字 ⇒ "点到的"与"读到的"终于是同一个词。
- ⚠ **没把菜单直接指向 `dock.title.canvas`**：本仓「面板」菜单用**每个面板自己的标题键**，
  与 dock 工具标题键是**两套并行键族**，其中已有 **5 对**文案本来就不同（图层/层管理、资源管理/资源、
  导航流程/导航流、AI 配置/AI 助手…）。统一那 5 处是另一件事、且是产品口径。
- 🩸 **判据边界（写清楚）**：**没有任何判据判"这一栏该叫什么"** —— 判据管的是键的**存在性 + 可达性**
  （常量↔字典 / 两本字典同键集 / 字典键必须能被某机制取到 / 无代码引用常量的分桶要与差异表一致）。
  文案只能靠人读；本阶段把它变成可执行事实的是那条**正控**。
- **负控 3 支 ＋ 正控 1 支**（还原 SHA256 逐字节相同）：
  ① 只改一半（XAML 新键、字典留旧键）⇒ `LangKeyCoverageTests`（常量指的键没了）＋
  `EveryDictionaryKey_IsReachableBySomeMechanism`（旧键再无机制）**2 腿红**；
  ② 只改 `en-US`（两本不同步）⇒ `BothDictionaries_HaveTheSameKeySet` ＋ 常量解析 **2 腿红**；
  ③ 菜单改指 `dock.title.canvas`（存在且可达的另一个键）⇒ 新键失去**唯一**机制 ⇒
  `UnusedConstants_ClassifiedByKeyMechanism` ＋ 可达性守卫 **2 腿红**
  （⚠ 这一支我起初当"正控"写，实测红 ⇒ 更正为负控：它证明"键必须真的有机制在取"是承重的）；
  ④ **正控（必须绿）**：只改文案的值 ⇒ **23/23 全绿** ⇒ 判据不替产品选名字。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **671**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2140 / 315 / 619`（合计 **3340**）
＋ 步 6 独占 `8` ＋ `2` ＝ **3350 通过 / 0 失败**（与阶段 137 同 —— 本阶段只改文案、不动判据）；
固件 host `[9/9]`。**产品代码**：4 个文件各 1 行。`UnusedBaseline` **1104 不变**
（`WinDesignCanvas` 与它前身一样没有代码引用 ⇒ 改名不改变本集大小）。

**下一步可选**：① `#72` 的静态**数据**那一半（`DockLayoutState.Templates` 等共享数据跨测试污染）；
② 那 5 对"菜单名 ≠ 面板名"的文案（要不要统一，纯产品口径）；③ 面外剩下 7 个视图；
④ 收口汇总 s125→s138。

---

## 阶段 139（2026-10-01）—— `#72` 的数据那一半：静态共享数据清基线机制（含一次**自我更正**）

**一句话**：给"静态共享数据"建了测试卫生机制（`StaticBusHygiene.ResetSharedData()` ＋ **8 个字段全 Reset**
＋ 逐个分类 ＋ fail-closed ＋ 5 条自测），`FaceplateLibraryView` 宿主**恢复字面断言**（阶段 133 的回避撤回）。
🩸 本阶段还包含一次**自我更正**：中途"清数据 ⇒ 全量跑崩/挂"的观测是**环境噪声**，据此开的 `#76` 已**撤回**。
逐条见登记表 §61。

- **机制**：反射枚举 `DockLayoutState` 的全部静态字段；委托型归 `Silence()`（阶段 134），
  其余**必须在 `SharedDataPolicy` 里分类**（`Reset`/`Keep` ＋ 理由）——**没分类就抛**（fail-closed）。
  当前 **8 个全 `Reset`**：基线 = 「产品**打开工程之前**」的状态，每个视图都必须在它下面能构造。
- **自测 2 → 5 条**：真清回基线 ＋ `Keep` 档一动不动 ＋ 分类表两侧闭合 ＋ 未分类必抛。
  🆕 那条 fact **按分类表现算期望**（不写死「只有 Templates」）——政策一改 fact 不跟着改。
  🩸 数据 fact 的**第一步必须是 `Silence()`**（漏了它 `SetDrawTool` 会当场抬 `DrawToolChanged`，
  打到别的测试留下的主窗口 → 崩）。
- **治本兑现**：`选中模板` / `模板条数` / `编辑请求` 三条**恢复字面断言**（`NcFaceA` / `2` / `NcFaceB`）；
  两条一致性探针保留（判的是另一件事）。
- 🩸 **自我更正（§61.4）**：中途量到「8 个全清 ⇒ 全量跑崩/挂」并归因到 `HmiDockFactory.cs:284/305`
  两处裸读 `CurrentScreen`（当时开了 `#76`）。收尾复测**推翻**：8 个全清 ＋ 字面断言，
  **全量连跑 3 次全绿**（624 / 31–33 s）；那 6 连败是**同一 session 里前面被 600 s 超时杀掉的 testhost
  把进程环境搞坏了**。复看两处代码也都**判了空**（`LayerPanelView.Refresh()` 首行就 return、
  `ProjectExplorerView._currentScreen` 每个使用点都有守卫）⇒ **`#76` 撤销**。
  教训：**挂死/超时类的观测在写成结论之前必须换干净进程状态复测**；单变量探针要"能跑完"才可信。
- **负控 3 支 ＋ 正控 1 支**：① 分类表删掉 `Templates` ⇒ 分类守卫点名 ＋ fail-closed 抛（2 腿红）；
  ② `ResetSharedData` 早退 ⇒ `Assert.Equal() Failure`；③ **撤掉调用点 ⇒ 全量跑红在
  `模板条数 期望 2、实际 4`（阶段 133 的症状）** ⇒ 字面断言确实由这次清基线撑着；
  ④ 正控连清两次 ⇒ 全绿。还原 SHA256 逐字节相同。
- **产品代码 0 行**（只动测试侧三个文件）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **671**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2140 / 315 / 622`（合计 **3343**）
＋ 步 6 独占 `8` ＋ `2` ＝ **3353 通过 / 0 失败**（阶段 138 为 3350，**+3**）；固件 host `[9/9]`。
全量 App.UiTests **624 通过 / 0 失败**、**连跑 3 次 31 / 31 / 33 s 全绿**。
**面**：17 宿主 / 81 声明 / 66 控件 / 63 名字（未动）；**面外 7 视图 / 47 控件**（未动）。

**下一步可选**：① 那 5 对「菜单名 ≠ 面板名」的文案（纯产品口径）；② 面外剩下 7 个视图；
③ 收口汇总 s125→s139；④ 把"宿主跑前清共享数据"这条纪律写进新宿主的选型/模板注释里。

---

## 阶段 140（2026-10-01）—— 把「菜单名 ≠ 面板名」的 5 对文案统一

**一句话**：`#75` 收尾时发现面板名字有**两套键族**（dock 标签页 `dock.title.*` / 「面板」菜单用视图自己的
标题键），其中 **5 对文案不同** ⇒ 用户拍板统一。**只动两本字典的值 ＋ `MainWindow.axaml` 1 行**（共 5 行），
不需要新常量。逐条见登记表 §62。

- **三对"选一个名字"**：`dock.title.layers` 层管理 → **图层**、`dock.title.navigation` 导航流 → **导航流程**、
  `dock.title.assets` 资源 → **资源管理 / Asset Manager**（前三者的视图侧/菜单侧文案与之一致）。
- **一对"拆开"**：菜单 `MenuPanelAiChat` 原本用 `ai.config.title`，可那个键其实是视图**内部配置折叠区**的
  Expander Header（`AiChatPanelView.axaml:11`）⇒ 菜单改用 `dock.title.aichat`「AI 助手」，
  `ai.config.title` 留给折叠区（拆完语义反而更准）。画布那对阶段 138 已一致。
- 🩸 **判据边界**：**文案取值本身没有判据**（正控只改值 ⇒ 624/624 全绿）；但**键的存在性有人判**
  （把菜单指向不存在的键 ⇒ 1 红 / 623 绿，红在 `LocalizedFormatCallSiteTests.XamlReferencedKeys_ExistInBothDictionaries`）。
  🩸 **顺带记一次误读**：我第一版负控的过滤器**漏了这个类**，于是把一条**已存在的守卫**误读成"盲区"、
  差点新写一条重复守卫 ⇒ 教训：**负控的过滤器必须覆盖"可能抓到它的那条腿"**，拿不准就用全量跑（32 s）；
  "没红"在过滤器太窄时**不构成盲区的证据**。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **985 行**（非空 **948**）；
脚本 **29**；路径卫生 **671**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2140 / 315 / 622`（合计 **3343**）
＋ 步 6 独占 `8` ＋ `2` ＝ **3353 通过 / 0 失败**（与阶段 139 同）；固件 host `[9/9]`。
**产品代码**：4 个文件 5 行。**面**：17 宿主 / 81 声明 / 66 控件 / 63 名字（未动）。

**下一步可选**：① 面外剩下 7 个视图；② 收口汇总 s125→s140；③ 把「宿主跑前清共享数据 ＋ 清订阅」
写进挑宿主/建表的纪律里；④ `#72` 里那两条旧尾巴（`#45` DockPanelInvocationUiTests 挂死、
以及硬件类项）。

---

## 阶段 141（2026-10-01）—— 第五层「事件接线」收口汇总 **s125 → s140**

**一句话**：不新增判据、不改产品，把 16 个阶段的成果与教训收成一份 ——
**`docs/2026-10-01-fifth-layer-arc-s125-s140-closeout.md`**（登记表 §63）。

- **面**：宿主 **9 → 17**；选择族声明 **57 → 81**、按宿主去重 **51 → 66**、全仓名字 **48 → 63**；
  面外 **16 视图 / 63 控件 → 7 / 47**；全量用例 **3348（s134 实测）→ 3353**；路径卫生 **675 → 671**。
- **产品改动**：16 个阶段里只有 4 个阶段动过产品，合计 ~370 行（其中 **344 行是删除**）：
  `#74` 死代码修复／删两个死视图／两处文案与键名。
- **登记项账**：`#71` `#73` `#74` `#75` 闭环；`#72` 两半都闭环（写入点仍在产品侧，已写清）；
  `#76` 撤回（误诊）。
- **教训 11 条**：其中三条是"量具自己的纪律"（失败的宿主腿会崩挂／挂死类观测必须换干净进程状态复测／
  负控过滤器要覆盖可能抓到它的腿），另有 `LANG` 环境变量、`git checkout` 抹工作、备份被污染、
  `Assert.Same` 比装箱枚举假红、"注释把计数喂饱了"等。
- **剩余清单**：面外 7 个视图（`PropertyPanelView` 31 已写清代价）、`#72` 的写入点、`#45`、
  硬件类项（需实机）。
- **建议下一步**：① 面外那 4 个**零选择接线**的视图（便宜，能把面外从 7 降到 3）；
  ② `#45`（让 `DockPanelInvocationUiTests` 能在大队里跑）；③ 把"宿主跑前清共享数据 ＋ 清订阅"
  写进挑宿主/建表的纪律。

**读数**：本阶段**只加文档**（产品与判据 0 行改动）⇒ 门禁读数与阶段 140 相同（`GATE_EXIT=0` /
7 步全绿 / 3353 通过 / 0 失败）。

---

## 阶段 142（2026-10-01）—— 第五层加第 18 个宿主（`TagMonitorView`）；面外 7 → 6 视图

**一句话**：用户拍板"拓面"。先把面外 7 个视图的**真实面**量出来（我上一轮凭印象说"4 个零选择接线"
是**不准**的：普查表里它们各写着 1–5 条），再挑**代价最小**的一个 —— `TagMonitorView`
（**4 个具名按钮 ＋ 1 条接线下拉**）升格成第 18 个宿主。逐条见登记表 §64。

- **它是本层第一个「终端消费者」宿主**：前面 17 个多多少少"只抬事件"（要 `EventObserved` 才能判），
  而它**不抬任何事件** —— 处理器直接写**列表 / 摘要 / 引擎** ⇒ 判的是「按钮 → 处理器 → 数据」这一跳。
- **表 6 条**（`Moves` 4 / `Inert` 1 / `Never` 1）＋ 7 枚探针，其中最有价值的一格是**模型侧**的
  「引擎值」：`BtnWrite` 布景填 42 ⇒ `WriteValue` ⇒ `ReadValue` 回读 **10 → 42**（判数据，不判界面文案）。
- **夹具**：往 `Project.Screens[0]` 加 2 个带绑定控件（`液位` 可写 / `温度` 只读）＋ 桩值源 ＋
  `BuildRuntimeTags()`；**刻意不 `RefreshRows()`**（视图构造函数也不调）⇒ 第一条 `BtnRefresh`
  才有可判效果（0 → 2 行）。
  🩸 首跑踩坑：变量必须挂 **`Project.VariableManager`**（不是 `depot.Variables`）——
  `RuntimeBindingEngine` 解析不到变量会**静默不建标签**（首跑「标签数 0 → 0」就是这个）。
- **负控 3 支**（还原 SHA256 逐字节相同）：① 摘 `BtnRefresh` 接线 ⇒ 红「声明的分量一个都没动」；
  ② 把它接成 `{ BuildRuntimeTags(); RenderRows(); }` ⇒ 红「标签数 期望 2、实际 0」；
  ③ 删掉 `CmbDirectionFilter` 的**两条**选择声明 ⇒ **静态守卫红、运行期 19/19 绿**
  （🩸 只删一条**不**红 —— 复证阶段 118 的教训：`declaredNames` 里还有那个名字）。
- **面**：宿主 **17 → 18**；选择族声明 **81 → 83**、按宿主去重 **66 → 67**、全仓名字 **63 → 64**；
  **面外 7 视图 / 47 控件 → 6 / 46**；`App.UiTests` **624 → 625**。**产品代码 0 行**。
- **下一批候选（已量）**：`SimulatorView`(9 按钮/5 选择)、`ScriptEditorView`(14/3)、
  `RecipeEditorView`(14/5)、`DeviceManagerView`(15/1)；`DesignCanvasView`(5/1) 是指针语义、另议；
  `PropertyPanelView`(15/**31**) **不扩**（批量写；已由 `PropertyPanelWiringTests` 覆盖）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；脚本 **29**；路径卫生 **671**；
步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2140 / 315 / 623`（合计 **3344**）＋ 步 6 独占 `8` ＋ `2`
＝ **3354 通过 / 0 失败**（阶段 140 为 3353，**+1**）；固件 host `[9/9]`；
全量 App.UiTests **625 通过 / 0 失败**（连跑 2 次 31 / 32 s）。

**下一步可选**：① `SimulatorView` 那一批（按"按钮数最少"继续）；② `DesignCanvasView` 的指针语义
要不要进本层（需要新原语）；③ `#45`；④ 把"宿主跑前清共享数据 ＋ 清订阅"写进挑宿主的纪律。

---

## 阶段 143（2026-10-01）—— `DesignCanvasView` 指针序列原语：**设计与可行性验证**

**一句话**：用户拍板"给 `DesignCanvasView` 设计新原语"。本阶段**只做设计与验证**（产品 0 行、判据 0 行）：
交付专题 `docs/2026-10-01-design-canvas-pointer-primitive.md`，并把实现登记为 **`#77`**。

- **为什么现有四类手势盖不住**（实测）：画布的"选择"是**画布自己的**事件
  （`DesignCanvasView.axaml.cs:148` `CanvasLayer.SelectionChanged`）—— 静态面守卫按事件名把它算成
  1 个接线控件（普查表注释就写着「选择来自画布自身，不是控件事件」），但**没有任何手势能驱动它**；
  编辑动作全在 `DesignCanvas.Input.cs:87/278/401` 的指针处理器里。⚠ 全仓测试**从未**驱动过画布指针交互
  （现有画布测试只碰 `DesignCanvas.Render`）。
- **原语设计 `PointerPath`**：`PointerStep(Press/Move/Release, x, y)`（控件局部坐标）＋
  `ClickSpec.PointerPath` ＋ `Drags(...)` 工厂；`RunGesture` 加一个分支、**每步 `Pump()`**；
  **必须同步改 3 处**（`SelectDeclarations` 的过滤加 `PointerPath`、`ClickSpec` 唯一性键加"指针路径"、
  `Check` 不用改）。
- **可行性已验**（临时 spike，跑完即删）：headless 下指针事件**可构造** ——
  `new Pointer(1, PointerType.Mouse, true)` ＋ `PointerPressedEventArgs` 的 8 参 ctor，**编译通过**
  ⇒ 没有原理性障碍；剩下是工程细节（move/release 的 ctor 待验、headless 命中测试依赖 `Show()+Pump()`、
  拖动阈值）。
- **建议分两步落**：① **点中控件 ⇒ 选中**（低风险，能把 `CanvasLayer` 从"不可判"变成"可判"）；
  ② **拖动位移控件**；③ 绘制（牵动绘图工具状态机）**单独立项**。
- **负控预案**：① 摘 `CanvasLayer.SelectionChanged` 转发 ⇒ ①腿红；② 把拖动写入改成常量 ⇒ ②腿红
  （`控件X 期望 60、实际 10`）；③ 删声明 ⇒ **静态守卫红、运行期绿**。

**读数**：本阶段只加文档 ⇒ 与阶段 142 相同：`GATE_EXIT=0` / 7 步全绿 / **3354 通过 / 0 失败**；
全量 App.UiTests **625 / 0**。

---

## 阶段 144（2026-10-01）—— `Drags` 指针序列原语落地 ＋ 第 19 个宿主 `DesignCanvasView`

**一句话**：`#77` 的兑现 —— 用户拍板"实现 ①点中选中"。本阶段给第五层加了**第五类手势**
（`Drags`：按下 → 移动 → 抬起，控件局部坐标），并把 `DesignCanvasView` 升格为**第 19 个宿主**，
第一条腿就是新原语本身（**点中控件 ⇒ 选中数 0 → 1**）。逐条见登记表 §66。

- **框架 4 处**：`ClickSpec.PointerPath` ＋ `PointerStep(Kind, X, Y)`；工厂 `Drags(...)`；
  `Gesture` 的指针分支（**每步 `Pump(2)`**）；**同步三处**（`SelectDeclarations` 导出、`selects` 计数、
  三种手势互斥自检）。
- 🩸 **落地实测（专题的两处更正）**：① **手工构造 `PointerPressedEventArgs` 跑不起来** ——
  8 参 ctor 不设 `RoutedEvent` ⇒ `RaiseEvent` 抛 `Cannot raise an event whose RoutedEvent is null`
  （带 `RoutedEvent` 的重载不存在）⇒ **正解是 `Avalonia.Headless` 的 `MouseDown/MouseMove/MouseUp`**
  ＋ `TranslatePoint` 换算根窗口坐标；② 专题里"move/release 的 ctor 待验"**作废**。
  **教训**：spike"编译通过"只证明签名存在 —— `RoutedEvent` 为 null 是**运行期**才炸的。
- **新宿主**：5 个具名 Button 族（预览开关 ＋ 软键盘 4 键）＋ 1 个指针控制（`CanvasLayer`）；
  声明 6 条 —— ① 指针点中 ⇒ 选中数 0→1；② 预览开关 ⇒ 预览模式 0→1 ＋ **选中数 1→0**（`ClearSelection`）；
  ③–⑥ 软键盘 4 键如实记 `Inert`（**三种不同成色**：写入同值 / 提前 return ×2 / 未被观测）。
  夹具加一个 **40,40 / 120×60** 的控件（原来那个没宽高 ⇒ 命中矩形 0×0）＋ `ZoomLevel = 1.0`。
- **负控 3 支**（还原 SHA256 逐字节相同）：① 把 `RaisePointer` 变空转 ⇒ **原语承重**（那条腿红）；
  ② 摘视图的 `SelectionChanged` 转发 ⇒ 红；③ 删唯一那条指针声明 ⇒ **静态守卫红、运行期 20/20 绿**。
- **面**：宿主 **18 → 19**；选择族声明 **83 → 84**、按宿主去重 **67 → 68**、名字 **64 → 65**；
  **面外 6 视图 / 46 控件 → 5 / 45**；`App.UiTests` **625 → 626**。**产品代码 0 行**。
- **仍开着**：`#77` 的 ② 拖动位移控件、③ 绘制新建（专题 §4 的风险排序）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；脚本 **29**；路径卫生 **671**；
步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2140 / 315 / 624`（合计 **3345**）＋ 步 6 独占 `8` ＋ `2`
＝ **3355 通过 / 0 失败**（阶段 142 为 3354，**+1**）；固件 host `[9/9]`；全量 App.UiTests **626 / 0**。

---

## 阶段 144（2026-10-01）—— `Drags` 指针序列原语落地 ＋ 第 19 个宿主 `DesignCanvasView`

**一句话**：`#77` 的兑现 —— 用户拍板「实现 ①点中选中」。本阶段给第五层加了**第五类手势**
（`Drags`：按下 → 移动 → 抬起，控件局部坐标），并把 `DesignCanvasView` 升格为**第 19 个宿主**，
第一条腿就是新原语本身（**点中控件 ⇒ 选中数 0 → 1**）。逐条见登记表 §66。

- **框架 4 处**：`ClickSpec.PointerPath` ＋ `PointerStep(Kind, X, Y)`；工厂 `Drags(...)`；
  `Gesture` 的指针分支（**每步 `Pump(2)`**）；**同步三处**（`SelectDeclarations` 导出、`selects` 计数、
  三种手势互斥自检）。
- 🩸 **落地实测（专题的两处更正）**：① **手工构造 `PointerPressedEventArgs` 跑不起来** ——
  8 参 ctor 不设 `RoutedEvent` ⇒ `RaiseEvent` 抛 `Cannot raise an event whose RoutedEvent is null`
  （带 `RoutedEvent` 的重载不存在）⇒ **正解是 `Avalonia.Headless` 的 `MouseDown/MouseMove/MouseUp`**
  ＋ `TranslatePoint` 换算根窗口坐标；② 专题里「move/release 的 ctor 待验」**作废**。
  **教训**：spike「编译通过」只证明签名存在 —— `RoutedEvent` 为 null 是**运行期**才炸的。
- **新宿主**：5 个具名 Button 族（预览开关 ＋ 软键盘 4 键）＋ 1 个指针控制（`CanvasLayer`）；
  声明 6 条 —— ① 指针点中 ⇒ 选中数 0→1；② 预览开关 ⇒ 预览模式 0→1 ＋ **选中数 1→0**（`ClearSelection`）；
  ③–⑥ 软键盘 4 键如实记 `Inert`（**三种不同成色**：写入同值 / 提前 return ×2 / 未被观测）。
  夹具加一个 **40,40 / 120×60** 的控件（原来那个没宽高 ⇒ 命中矩形 0×0）＋ `ZoomLevel = 1.0`。
- **负控 3 支**（还原 SHA256 逐字节相同）：① 把 `RaisePointer` 变空转 ⇒ **原语承重**（那条腿红）；
  ② 摘视图的 `SelectionChanged` 转发 ⇒ 红；③ 删唯一那条指针声明 ⇒ **静态守卫红、运行期 20/20 绿**。
- **面**：宿主 **18 → 19**；选择族声明 **83 → 84**、按宿主去重 **67 → 68**、名字 **64 → 65**；
  **面外 6 视图 / 46 控件 → 5 / 45**；`App.UiTests` **625 → 626**。**产品代码 0 行**。
- **仍开着**：`#77` 的 ② 拖动位移控件、③ 绘制新建（专题 §4 的风险排序）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；脚本 **29**；路径卫生 **671**；
步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2140 / 315 / 624`（合计 **3345**）＋ 步 6 独占 `8` ＋ `2`
＝ **3355 通过 / 0 失败**（阶段 142 为 3354，**+1**）；固件 host `[9/9]`；全量 App.UiTests **626 / 0**。

---

## 阶段 145（2026-10-01）—— `#77` 的第 ② 条指针腿：拖动位移控件

**一句话**：承接阶段 144，用新原语加一条**拖动位移**腿 —— `Drags("CanvasLayer",(60,60)→(110,60))`
⇒ **目标控件X 40 → 90**（`DragMode.Move` 支的 `SnapToGrid(_dragStartBounds.X + dx)`；
夹具 `SnapStep = 5`、dx=+50 ⇒ 恰好落栅格）。逐条见登记表 §67。

- 读数三行（实测）：`初态 X=40` → `点一下 ⇒ 选中数 1, X=40` → **`拖一段 ⇒ X=90`**。
- ⚠ **刻意不按空白收尾**：`OnPointerPressed` 的空白支走 `StartLongPressSelection` —— 会起
  **`DispatcherTimer`**（长按框选），完成时刻不由 `Pump` 决定 ⇒ 表里没有"点空白取消选中"那条腿。
- 🩸 **顺带修掉一处框架自检**：两条指针腿同名同下标 ⇒ 撞上「以同一个手势登记了两次」；
  这正是阶段 143 设计 §3.3 预言的第三处改动 —— **唯一性键要含指针路径**（键从三元组变四元组）。
- **负控 2 支**（还原 SHA256 逐字节相同）：① 把 `RaisePointer` 的 `Move` 支变空转 ⇒
  红「目标控件X 期望 90、实际 40」；② 把产品侧那句 `SnapToGrid(...)` 换成常量（**两处** ——
  多选与单选各一处，只改一处 `count == 2` 会当场打脸）⇒ 同一句红。
- **产品代码 0 行**。**仍开着**：`#77` 的 ③ 绘制新建。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；脚本 **29**；路径卫生 **671**；
步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2140 / 315 / 624`（合计 **3345**）＋ 步 6 独占 `8` ＋ `2`
＝ **3355 通过 / 0 失败**（与阶段 144 相同 —— 本阶段只加一条腿）；固件 host `[9/9]`；
全量 App.UiTests **626 通过 / 0 失败**（连跑 2 次 32 / 34 s）。

---

## 阶段 146（2026-10-01）—— `#77` 的第 ③ 条指针腿：绘制新建（**`#77` 闭环**）

**一句话**：收尾 `#77` 的最难一条腿（专题 §4 风险排序里最高）：布景把 `Canvas.DrawTool` 设成 `Button`，
指针 (400,200) → (500,260) ⇒ 画布 `CommitDraw` → `AddControlAt` **直接写 `screen.Controls`** ⇒
**控件数 2 → 3**、「末位控件X」**90 → 400**。逐条见登记表 §68；`#77` **整项闭环**。

- 读数（实测）：`初态 控件数=2 X=40` → `点一下 X=40` → `拖一段 X=90` → **`画一段 控件数=3 末位X=400`**。
- ⚠ 布景走**视图的** `Canvas.DrawTool`（不是 `DockLayoutState.SetDrawTool` —— 那条路要靠静态总线
  订阅者把值推给画布，而总线在宿主跑前已被清）；这里判的是**画布这一跳**。
- 🩸 **落地撞到两件事（都写进注释）**：① 想"另起一块空屏"让绘制从 0 数起 ⇒ **三条指针腿全废**
  —— 画布会把屏幕**适配进自己的视口**，换屏等于换坐标系 ⇒ 退回 `WindowDepot.Screen`、按 `2 → 3` 判；
  ② 拖动腿必须**补声明** `末位控件X`（夹具里 `Controls[^1]` 正好是被拖的那个控件）。
- **负控 3 支**（还原 SHA256 逐字节相同）：① `AddControlAt` 不写模型 ⇒ 红「控件数 期望 3、实际 2」；
  ② 新控件 X 写常量 ⇒ 红「末位控件X 期望 400、实际 0」；③ 撤掉 `DrawTool` 布景 ⇒ 红（空白按下改走长按框选）。
- **产品代码 0 行**。**`#77` 闭环**（①②③ 三条腿都落；负控累计 8 支）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；脚本 **29**；路径卫生 **671**；
步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2140 / 315 / 624`（合计 **3345**）＋ 步 6 独占 `8` ＋ `2`
＝ **3355 通过 / 0 失败**（与阶段 144/145 相同 —— 只加腿，theory 用例数不变）；固件 host `[9/9]`；
全量 App.UiTests **626 通过 / 0 失败**（连跑 2 次 32 / 33 s）。

---

## 阶段 147（2026-10-01）—— 把"加一个宿主"的纪律与本轮教训收成一处

**一句话**：用户拍板"写纪律 ＋ 集中本轮教训"。**只加文档**（产品 0 行、判据 0 行）：
`docs/2026-10-01-fifth-layer-host-checklist-and-lessons.md`（160 行）；登记表 §69。

- **纪律七步**：① 量面（别靠印象；"零接线"≠"便宜"）→ ② 挑宿主（五条判据，含"批量写者不扩"）
  → ③ 建夹具（`SeedXxxFixture`、前置要成立、**别换屏**）→ ④ 建表（三个桶＋ `Inert` 三种成色＋
  顺序语义＋唯一性键＋断言优先读模型）→ ⑤ 卫生（`Silence` ＋ `ResetSharedData`；清完之后走视图自己的属性）
  → ⑥ 负控（2–3 支，逐支红在点名的腿上）→ ⑦ 收口（门禁 → §＋状态节 → 分提交）。
- **教训 17 条**（每条带实测出处）：最值钱的五条是 **挂死类观测要换干净进程状态复测**（s139 撤回 `#76`）、
  **单变量探针要能跑完**、**失败宿主腿会崩/挂而非干净红**（三次）、**拼接后必须看 diff 量级**（s142 截掉 600 行）、
  **spike 编译通过 ≠ 跑得起来**（s144）；另有指针坐标是视口坐标、探针语义、负控过滤器、备份污染、
  装箱枚举 `Assert.Same` 假红、`LANG` 环境变量、`git checkout` 语义、注释喂饱计数等。

**读数**：只加文档 ⇒ 与阶段 146 相同：`GATE_EXIT=0` / 7 步全绿 / **3355 通过 / 0 失败**；
全量 App.UiTests **626 / 0**。

---

## 阶段 148（2026-10-01）—— `#45` 规避**退役**：`DockPanelInvocationUiTests` 回到大队

**一句话**：用户拍板"定位 `DockPanelInvocationUiTests` 挂死"。**先复测再找根因** —— 那条
"单独成段跑"的规避是**阶段 52** 加的，之后仓库里落了 `#46`（宿主不再直订面板事件）与 `#47`
（Dispatcher 哨兵）两个可能的修复，**却没人回头复测**。复测结论：**已不可复现** ⇒ 撤销排除，
门禁少一次 `dotnet test` 调用。逐条见登记表 §70。

- **复测五跑全绿**：本类单跑 2/2（1 s）／`App.UiTests` 全量**含本类 626/626**（连跑 4 次，30–34 s）／
  **门禁同款 solution 级**含本类 `EXIT=0` 全绿。当年 A 档（4/4 僵死）已不可复现。
- **怀疑根因（写成假设）**：`#46`（`5cabdbb`：宿主不再直订面板视图事件，唯一消费路径 = 静态总线）＋
  `#47`（`bf35e30`：Dispatcher 归属哨兵）—— 两者都在规避之后落地。⚠ 当年没有 dump/调用栈 ⇒
  **不声称"根因已找到"**，只声称"规避的条件已消失"。
- **处置**：`ci-gate.sh` 删掉 `!~DockPanelInvocationUiTests` 与它单独那段（banner/文案改"两段"，
  原处写明退役理由）；`gate-readings.py` 的程序集条数 10 → **9**（含**自检档**：合成日志 10 → 9 行、
  合计 3302 → 3300）。
- **读数（门禁连跑 2 次全绿）**：`GATE_EXIT=0` / 7 步 / **980 行**（非空 **943 / 944**）/ 脚本 **29** /
  路径卫生 **671**；逐程序集 `44 / 44 / 4 / 96 / 78 / 2140 / 315 / 626 / 8`（合计 **3355**，
  与改前**完全相同** —— 只是少了一次调用）。
- 🩸 **教训**：**"规避"必须写清复测触发条件**（"哪些改动可能让它消失"），否则会像这条一样
  被仓库多背 12 个阶段；**复测比找根因便宜**（5 跑 3 分钟 vs 无 dump 的死锁考古）。

---

## 阶段 149（2026-10-01）—— 第五层加第 20 个宿主（`SimulatorView`）；面外 5 → 4 视图

**一句话**：按检查单先量面再选（`SimulatorView` 9 按钮 + 数据面厚，优于 `DeviceManagerView` 的 15 按钮），
升格为第 20 个宿主；表 **15 条声明**（Moves 8 / Inert 3 生效 / Never 3）＋ 5 条选择族。逐条见登记表 §71。

- **夹具关键口径**：`SimulatorView` 构造期**自己注册一个演示变量**（Sine、已选中、写过日志）
  ⇒ 全表期望都是「1 → 2」的**增量**，不是「0 → 1」（首跑把原期望全顶红了）。
- **静止性**：两个 `DispatcherTimer` 只在 `OnToggleSimulation` 里启动（构造里只创建）⇒ `BtnStartStop` 记 `Never`。
- 🩸 **首跑顶出来的两处口径错误**（都按实测改了并写明）：
  ① `BtnWriteRegister` 原按「写成功 ⇒ 寄存器日志 +1」声明 ⇒ **一个分量都没动**（`AppendLog` 写进已非空的
  `LogTextBlock`；寄存器日志只由 `RegisterWritten` 事件驱动）⇒ 如实转 **`Inert`** 并写明成色①；
  ② `BtnReset` 的 `Clear()` 之后**仍剩 1 个变量**（构造期演示变量被重建）⇒ 期望 2→1。
  另有 `ClockMultiplierCombo` index 1 的 Tag 就是 1（**写入同值**的现场）、`VariableList` 清空选中**不清面板**。
- **负控 3 支**（还原 SHA256 逐字节相同）：① 摘 `BtnAddVariable` 接线 ⇒ 红「声明的分量一个都没动」；
  ② 「模拟器变量数」期望写成 99 ⇒ 红「期望 99、实际 2」；③ 删 `SignalTypeCombo` 声明 ⇒
  **静态守卫红**（点名「接线了却没被分类」）**而运行期 21/21 绿**。
- **面**：宿主 **19 → 20**；选择族声明 **84 → 92**、按宿主去重 **68 → 73**、名字 **65 → 69**；
  **面外 5 视图 / 45 控件 → 4 / 40**；`App.UiTests` **626 → 627**。**产品代码 0 行**。
- 教训清单补两条：**写中文散文的 C# 字符串别用 ASCII 引号**（本 session 撞 5 次）、
  **探针读法要与控件类型对上**（`TextBlock` ≠ `TextBox`，`Control<T>` 的报错当场点名）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；
脚本 **29**；路径卫生 **671**；步 5 逐程序集 `44/44/4/96/78/2140/315/627/8`（合计 **3346**）
＋ 步 6 独占 `8` ＝ **3356 通过 / 0 失败**（阶段 148 为 3355，**+1**）；固件 host `[9/9]`；
全量 App.UiTests **627 / 0**（连跑 2 次 32/32 s）。

---

## 阶段 150（2026-10-01）—— 第五层加第 21 个宿主（`DeviceManagerView`）；面外 4 → 3 视图

**一句话**：按检查单挑**未知量最少**的面外视图 —— 15 个具名按钮里 14 个是平台桩（⇒ `Never`），
真正可判的只有 **1 个按钮 ＋ 1 个下拉**；17 条声明**首跑即绿**。逐条见登记表 §72。

- **主判据**：`CmbChannelType`（XAML 属性接线）⇒ `ApplyChannelPanels()` **三个面板一次性翻**
  （串口/波特率 显示→隐藏、TCP 隐藏→显示），拨回再全复原 ⇒ 双向见证。
- 🩸 **`BtnRefreshPorts` 如实记 `Inert`（成色① 未被观测）**：它的两处落点都不在读数上 ——
  ① `RefreshPorts()` 读**真实串口名**（无头沙箱没设备 ⇒ 项数/下标不动）；
  ② 日志走 **`Dispatcher.UIThread.Post`**，而本层 `Pump` **不排水 `Post`** ⇒ 日志条数也不动。
  要判它得先有「值源桩」或「排水 Post」的原语（登记为后续可选，不是缺陷）。
- **负控 2 支**（还原 SHA256 逐字节相同）：① 摘 `SelectionChanged=` 接线 ⇒ 红「声明的分量一个都没动」；
  ② 删两条选择族声明 ⇒ **静态守卫红、运行期绿**。
- **面**：宿主 **20 → 21**；选择族声明 **92 → 94**、按宿主去重 **73 → 74**、**按名字去重仍是 69**
  （`CmbChannelType` 与别的视图重名 —— 三口径差别的活例子）；**面外 4 视图 / 40 控件 → 3 / 39**；
  `App.UiTests` **627 → 628**。**产品代码 0 行**。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；脚本 **29**；路径卫生 **671**；
步 5 逐程序集 `44/44/4/96/78/2140/315/628/8`（合计 **3347**）＋ 步 6 独占 `8`
＝ **3357 通过 / 0 失败**（阶段 149 为 3356，**+1**）；固件 host `[9/9]`；全量 App.UiTests **628 / 0**。

**剩余面外**：`ScriptEditorView`(14 按钮/3 选择)、`RecipeEditorView`(14/5)（`PropertyPanelView` 已判定不扩）。

---

## 阶段 151（2026-10-01）—— 追 `nc150-2` 的「用例总数少 10」异常：查清了一半，登记 `#78`

**一句话**：上一阶段那个"运行期只有 12 用例"的异常**复现了、也量清了边界**，但**根因未查明** ——
如实登记为 **`#78`**，并把 `nc150-2` 的"运行期那半"标成**不作数**（既不写成绿、也不写成红）。

- **复现与读数**（三次一致）：删掉 `DeviceManagerView` 那两条 `SelMoves("CmbChannelType", …)` 后 ——
  过滤跑（`~HostButtonEffectUiTests`）**22 → 12** 用例（2 红 / 10 绿）；全量跑（带 `--blame-hang`）
  **628 → 618**（3 红 / 615 绿），**32 s 正常结束、无中止、无挂死**；同时**静态守卫红**（1 红 / 5 绿，**这条是预期的**）。
- **排除掉一半**：`--list-tests` 的**发现集在两态完全相同**（我 grep `^\s+HmiEditor` 两态都是 618）
  ⇒ **不是发现阶段的差异**。⚠ 但这个 grep 计数本身**不可信**（两态都被截成同一个数），
  所以**不能排除**发现差异 —— 这是留给下一次的第一件事。
- **下一步（写在 `#78` 里）**：① 用 `--list-tests` 的**不 grep 全量**输出两态逐行对拍；
  ② 对拍 VSTest 的 `.trx` / `--diag`，看那 10 个用例是**被跳过、没上报，还是被合并**。
- ⚠ 在查明之前：**`nc150-2` 的运行期那半不作数**；本阶段的宿主与守卫本身是绿的
  （`GATE_EXIT=0` / 3357 通过 / 0 失败，与阶段 150 相同 —— 本次只改文档）。

**读数**：本阶段**只加文档**（产品 0 行、判据 0 行）⇒ 与阶段 150 相同：`GATE_EXIT=0` / 7 步全绿 /
**3357 通过 / 0 失败**；全量 App.UiTests **628 / 0**。

---

## 阶段 152（2026-10-01）—— `#78` 第一步：**实测**发现集两态完全相同

**一句话**：把上一阶段"疑似不是发现差异"升级成**实测** —— 用**不带 grep 的全量** `--list-tests`
逐行对拍两态（干净 / 删掉那两条声明），**行数都是 618、双向差集都为空** ⇒ **发现阶段没有任何差异**；
异常被缩到**运行/报告口径**这一侧。`#78` 仍需 `.trx`/`--diag` 才能定性。

- **顺带发现一个更基础的口径差**：干净态的**运行总计是 628**，而同一棵树的 `--list-tests`
  只数出 **618** —— 这 10 的差**与注入无关、本来就存在**（"总计"与"发现"两个口径不同）。
  ⇒ 先把这个差搞清楚，再谈注入态那 10 个。
- 本阶段**只加文档**（产品 0 行、判据 0 行）⇒ 读数与阶段 150/151 相同：
  `GATE_EXIT=0` / 7 步全绿 / **3357 通过 / 0 失败**；全量 App.UiTests **628 / 0**。

---

## 阶段 153（2026-10-01）—— 查到 `#78` 里那个"本来就存在的 10"：`--list-tests` **不列全 theory 行**

**一句话**：那个"运行总计 628 vs `--list-tests` 619"的差**查到了** —— 是 `--list-tests` 自己的
**口径陷阱**：它只打印 **11** 条 `HostButtons_HaveTheDeclaredEffect`，而宿主有 **21** 个（21 − 11 = **10**）。

- ⇒ **`--list-tests` 不能当作"发现集"的普查**（至少对 theory 不能）；阶段 152 那句"发现集两态完全相同"
  **只说明两态被截断的方式相同**，**不能**排除发现差异 ⇒ `#78` 仍然开着（下一步只剩 `.trx`/`--diag`）。
- 🩸 这是本 session **第三次**撞到「**量具本身的口径**」这一类：① 负控过滤器漏类（把已存在的守卫误读成盲区）；
  ② `git checkout` 的语义（把未提交的工作抹掉）；③ 本次（把 `--list-tests` 当成发现集普查）。
- 本阶段**只加文档**（产品 0 行、判据 0 行）⇒ 读数与阶段 150–152 相同：
  `GATE_EXIT=0` / 7 步全绿 / **3357 通过 / 0 失败**；全量 App.UiTests **628 / 0**。

---

## 阶段 154（2026-10-01）—— `#78`：`.trx` 给出决定性读数（注入态**确实少跑 10 个宿主 case**）

**一句话**：换**结构化**量具（`--logger "trx"`）后定案 —— 注入态 `<UnitTestResult>` 共 **618** 条，
其中 `HostButtons_HaveTheDeclaredEffect` **只有 11 条**（干净态 21）⇒ **确实少执行了 10 个宿主 case**，
**不是**报告口径的问题。

- ⚠ **第三个量具也被证伪**：`--logger "console;verbosity=detailed"` 的逐宿主读数块**两态都只有 11 个**
  ⇒ console 日志同样**不能当普查**（与 `--list-tests` 同族）。
- ⇒ `#78` 的下一步非常具体：**从 `.trx` 列出注入态那 11 个宿主的名字**，与 `Hosts` 顺序对照 ——
  若是「`DeviceManagerView` 之后的 10 个都没跑」，那就是「**某个宿主 case 失败 ⇒ 同 theory 的后续
  case 不再执行**」这一族（本 session 第 4 次撞到同族现象）。
- 🩸 本阶段又攒下一条「量具口径」教训（console 日志/`--list-tests` 都不列全）—— 待补进检查单
  （下一件按顺序要做的事）。
- 本阶段**只加文档**（产品 0 行、判据 0 行）⇒ 读数与阶段 150–153 相同：
  `GATE_EXIT=0` / 7 步全绿 / **3357 通过 / 0 失败**；全量 App.UiTests **628 / 0**。

---

## 阶段 155（2026-10-01）—— `#78` 定案：注入态跑的是数组**下标 0–10**，**尾部 10 个整批未执行**

**一句话**：从 `.trx` 解析出那 11 个宿主名并定案 —— 注入态执行的是 `Hosts` **下标 0–10**，
未执行的 **10 个恰恰是下标 11–20**（`LayerPanelView` 起的整批尾部，**含被改的 `DeviceManagerView`**）
⇒ 那趟是**在第 12 个 case 处断掉、尾部整批被丢**；**被改的那个宿主自己根本没跑**，所以它**不是**断点。

- ~~**机制定性**：不是"报告口径"（阶段 154 已用 `.trx` 排除）、也不是"发现差异"（阶段 152/153 已排除），
  而是**运行到第 12 个 case 时整批尾部未执行** —— 与「失败 case 吞掉后续 case」这一族同形
  （本 session 第 4 次；前三次在 s134/s137/s139）。~~
  🩸 **阶段 156 更正：本节这条"机制定性"是错的** —— 那 10 个 case **从来不存在于那棵树里**
  （那次负控注入过宽，`Hosts` 被从 21 削到 11 ⇒ 618 是**算出来的**）。见下节。
- **最后一条待办（很窄）**：看那份 `.trx` 里 **4 条 `Failed` ＋ 3 条 `Error`** 的错误文本，
  确认第 12 个 case 是**抛异常**还是**挂住后被中止**。
  **（阶段 156 已办：那几条失败的文本就是答案 —— 点名的是 `DownloadToHmiView`、栈帧 `:3440`。）**
- ⚠ 本阶段仍**只加文档**（产品 0 行、判据 0 行）；读数与阶段 150–154 相同：
  `GATE_EXIT=0` / 7 步全绿 / **3357 通过 / 0 失败**；全量 App.UiTests **628 / 0**。

---

## 阶段 156（2026-10-01）—— `#78` **闭环**：真因是**负控注入"过宽"**，不是运行期异常（s151–s155 追的东西不存在）

**一句话**：把 `#78` 里的复现命令**原样做一遍**（只删 `DeviceManagerView` 那两条
`SelMoves("CmbChannelType", …)`）⇒ `.trx` **628** 条、`HostButtons_HaveTheDeclaredEffect` **21** 条
（`DeviceManagerView` **跑了**、红在「Moves 0 ⇒ 面太薄」）、**3 红 / 625 绿** —— **那个"618 / 11 个 case"不复现**；
再顺着 s151 那份日志里**一直没人读**的两条反证（失败文本点名的是 `DownloadToHmiView`；
栈帧 `:3440` vs HEAD `:4648`）反推出真实注入态，并在 HEAD 上**逐项复现**：删 1-based
**2589–3797 行** ⇒ **618 / 3 红 / 615 绿 / 11 个宿主 case**，失败三件套与旧日志**同名同序**。
⇒ 那 10 个 case 是**被从 `Hosts` 数组里删掉的**，不是被运行期丢掉的。

- **忠实注入（登记里的复现命令；`dotnet test tests/HmiEditor.App.UiTests -c Release --no-build`
  ＋ `--logger "trx"` ＋ `--blame-hang`）**：trx **628** 条（`Passed 625 / Failed 3`）、theory **21** 条、
  失败宿主 = `DeviceManagerView`（「只真点了 1 个按钮 —— 面太薄」，因为它的两条 Moves 声明被删）
  ⇒ **异常不复现**。
- **过宽注入（旧日志的真身，已在 HEAD 上复现）**：删 `HostButtonEffectUiTests.cs` 的
  **1-based 2589–3797 行**（= 文件里**第一条** `SelMoves("CmbChannelType"` 到**最后一条**的收尾行；
  4706 → 3498 行），构建 **0 警告 0 错误** ⇒ trx **618** 条（`615 / 3`）、theory **11** 条、
  失败三件套 = 守卫普查 ＋ `DownloadToHmiView` 那条腿 ＋ 自洽 Fact；断言落 **`:3439`**（旧日志 `:3440`）。
- **真因**：那一刀把 `Hosts` 从 **21 削到 11**（下标 11–20 的 `LayerPanelView`…`DeviceManagerView` 连块消失），
  并把 `DeviceManagerView` 剩下的 **15 条声明并进了 `DownloadToHmiView`**（旧日志那句
  「声明 15 条（Moves 0 / Inert 1 / Never 14）」的来源就是它）⇒ **theory case 数恒等于 `Hosts.Length`**，
  `628 − 10 = 618` 是**算出来的**、不是丢出来的。
- **反证一直在旧日志里**：s151 那份日志的 Fact 失败文本点的是 **`DownloadToHmiView`**、
  栈帧 **`:3440`**（HEAD `:4648` ⇒ 文件少 ≈1208 行）、`DownloadToHmiView` 的声明数正好是
  **`DeviceManagerView` 的尾部 15 条** —— s152/s153 去追"发现集"、s154/s155 只从 `.trx` **数条数**，
  没人读那几条失败的**文本**与**行号**。
- **留下规矩**（已写进 `docs/2026-10-01-fifth-layer-host-checklist-and-lessons.md` §6 / §7 / §8 教训 20·21）：
  **负控注入之后、读任何读数之前，先量"被注入那棵树"的形状** —— `Hosts.Length` / 用例总数 /
  失败点名的对象 / **栈帧行号与 HEAD 对得上吗**；四条里任一不符 ⇒ **注入错了**，不是产品/框架的问题。
  ⚠ 配套更正 `nc150-2` 那一行的"运行期那半"：忠实注入下**没有总数异常**（21 个宿主 case 全部执行）。
- 本阶段**只加文档**（产品 0 行、判据 0 行）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；
脚本 **29**；路径卫生 **671**；步 5 逐程序集 `44/44/4/96/78/2140/315/628/8`（合计 **3357**）
＝ **3357 通过 / 0 失败**（与阶段 150–155 相同）；全量 App.UiTests **628 / 0**。
**负控读数**（两份 `.trx` 留在 `.workbuddy/tmp/`）：`s156-faithful.trx` = **628 / 3 红**、
`s156-overbroad.trx` = **618 / 3 红**。

---

## 阶段 157（2026-10-01）—— 第五层加第 22 个宿主 `ScriptEditorView`（面外 3 → 2 视图）

**一句话**：面外只剩 `PropertyPanelView`（已判定不扩）、`ScriptEditorView`(3 选择)、`RecipeEditorView`(5 选择)
⇒ 挑**选择控件少、且编辑动作直接写模型**的 `ScriptEditorView`（`RecipeEditorView` 的新建/克隆/删除
全在 `dialog.Closed` 里，本层驱动不到对话框 ⇒ 那 14 个按钮会一起退化成 `Never`）。
**19 条声明**（`Moves` 13 / `Inert` 3 / `Never` 3）落地后**首跑只有 6 处不符**，逐条对读数改完即绿。

- **面**：14 个具名 Button 族（13 个 `Btn*` ＋ `ChkEnabled`）＋ 3 个接线选择控件
  （`ScriptList` / `CmbCategoryFilter` / `CboCategory`）⇒ **19 条声明、真点/真拨 14 条**。
  夹具补第 2 条脚本（`报警联动脚本`，分类刻意不同：`Alarm`、描述 8 字）＋ 给第 0 条补 5 字描述
  ⇒ 选择/筛选/描述三类腿都有**非本地化**的见证。
- 🩸 **它是第一个"自己就是 `Window`"的宿主**：三个字段处理器首行的 `Parent == null` 挡不掉
  —— `Window` 塞不进别的 `Window.Content`（前 21 个里凡依赖 `Parent` 的都是 `UserControl`，靠
  `Window.Content` 装配出来）⇒ `CboCategory` / `ChkEnabled` 两条如实记 **`Inert`（前提不满足）**，
  并由新分量 **`有Parent`**（初态 **0**）把前提**当场量出来**。**nc5** 实测：只拿掉那一条
  `Parent == null`，`ChkEnabled` 那条 `Inert` 当场变红 ⇒ 承重。
  ⚠ 同一前提带来一条好性质：`TxtCodeEditor.Text = …` 触发的**去抖定时器一次都没被点起来**
  ⇒ 本宿主是**静止**的（没有"完成时刻不由 `Pump` 决定"的腿）。
- 🩸 **落地时量出两条不对称**（都进了声明）：`SelectScript(...)` **只写面板、不写 `SelectedItem`**
  ⇒ ① 删除腿：面板写着 `开机脚本`、列表**停在 -1**；② 新建腿：面板显示新脚本、列表选中的
  却是**第一条**（下标 0）。两处都靠"清空时有没有选中项"这条实测规则解释得通（五次读数一致）。
- **负控 5 支**（逐支独立日志 + 还原 SHA256 逐字节相同 + 收尾重建）：nc1 摘接线 ⇒ 红
  「声明成 Moves，但声明的分量一个都没动」；nc2 面守卫（删**两条**声明）⇒ **静态守卫红**、
  运行期**绿**；nc3 只删一条 ⇒ 静态守卫**仍绿**（"只删一条不红"的又一次实测）；
  nc4 声明写反 ⇒ 红「期望 808、实际 807」；nc5 拆前提 ⇒ 红「未声明的分量 报警脚本启用 开 → 关」。
- **面外**：3 视图 / 39 控件 → **2 视图 / 36 控件**；`OutsideFaceCensus` 里删掉那一行。
  **三口径**：声明 **94 → 99**、按宿主去重 **74 → 77**、按名字去重 **69 → 72**
  （⚠ 重数脚本**先在 `git show HEAD:` 上复现出 94 / 74 / 69** 才采信）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；
脚本 **29**；路径卫生 **671**；步 5 逐程序集 `44/44/4/96/78/2140/315/629/8`（合计 **3348**）
＋ 步 6 独占 `8` ＝ **3358 通过 / 0 失败**（阶段 150–156 为 3357，**+1**）；固件 host `[9/9]`；
全量 App.UiTests **629 / 0**（连跑 2 次，30 / 31 s）。**产品代码 0 行**。

---

## 阶段 158（2026-10-01）—— 第五层加第 23 个宿主 `RecipeEditorView`：**面外最后一块真面收口**

**一句话**：面外只剩它（5 选择）与刻意不扩的 `PropertyPanelView`(31)。它与第 22 个宿主**形状互补** ——
**选择多（5 条）而按钮大多走对话框**：14 个具名按钮里 8 个（新增/克隆/删除分组与配方、
导入导出 CSV、关窗）本层驱动不到 ⇒ `Never`；真正能判的是**参数编辑那一块**（加/删参数、
两条排序、保存、两个参数下拉）—— 全都**直接写模型**。表 **23 条声明**（`Moves` 14 / `Inert` 1 /
`Never` 8），真点/真拨 15 条。

- **面**：14 个具名 Button 族 ＋ 5 个接线选择控件（`GroupList` / `RecipeList` / `ParamList` /
  `CmbParamDataType` / `CmbParamVariable`）。夹具 `SeedRecipeFixture` 铺 **2 分组 / 3 配方**，
  A2 三条参数的**默认值长度刻意不同**（`120` / `5` / `7`）⇒「换一条参数」有**处理器写的**见证。
  ⚠ 名字全 ASCII：管理器的两个查询都按 `Name` 排序，而 `OrderBy` 用**当前区域性**比较器。
- 🩸 **落地量出一条产品行为**：`OnMoveParam` 末尾那段"按 `Tag` 把移动后的参数选回来"是**死代码** ——
  它前面那句 `RefreshParamList` 的 `Items.Clear()` 会先把 `_selectedParam` 清成 null。净效果：
  排序腿之后「参数下标」→ **-1**、「参数面板可见」→ **0**。两格**如实钉死**；
  **负控 nc5**（把那一行挪到清空之后）⇒ 排序两条腿共 7 处不符 ⇒ 这两格承重。
  ⚠ 由此的排表纪律：每条 `Move` 腿之后必须**重新选一次**参数（⑤/⑥/⑧ 三条腿就是为此排的）。
- **另外三条性质**：① 状态反馈写的是窗口 **`Title`**（`ShowStatus`）⇒ 分量「状态条」按**分隔符**判；
  ② `ChkEnabled` **零订阅**（两处写、一处读）⇒ `Inert` 成色④；③ `RefreshRecipes()` 末尾
  `ClearEditPanel()` ⇒ **换分组顺带清编辑面板**（几条腿的期望都算了这层「清-装」的净效果）。
- **负控 5 支**（逐支独立日志 + 还原 SHA256 逐字节相同 + 收尾重建）：nc1 摘 `CmbParamVariable`
  接线 ⇒ 红「备用变量 期望 已绑、实际 未绑」；nc2 面守卫删 `CmbParamDataType` 声明 ⇒
  **静态守卫红、运行期绿**；nc3 只删一条 ⇒ 静态守卫**仍绿**；nc4 声明写反 ⇒ 期望/实际对不上；
  nc5 产品行为类（救活死代码）⇒ 排序两腿红 ⇒ §74.3 承重。
- **面外收口**：2 视图 / 36 控件 → **1 视图 / 31 控件**（只剩刻意不扩的 `PropertyPanelView`）。
  **三口径**：声明 **99 → 108**、按宿主去重 **77 → 82**、按名字去重 **72 → 76**
  （＋9 / ＋5 / ＋4 —— 差的 4 个里就有 `GroupList`，它在别的宿主里已声明过）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；
脚本 **29**；路径卫生 **671**；步 5 逐程序集 `44/44/4/96/78/2140/315/630/8`（合计 **3349**）
＋ 步 6 独占 `8` ＝ **3359 通过 / 0 失败**（阶段 157 为 3358，**+1**）；固件 host `[9/9]`；
全量 App.UiTests **630 / 0**（连跑 2 次，32 / 31 s）。**产品代码 0 行**。

---

## 阶段 159（2026-10-01）—— 闭环 `#61r`：控件默认名与界面语言解耦（**工程存中性名、界面显示本地化别名**）

**一句话**：第五层面收口后，登记表里唯一"真实、软件侧可验证、有明确用户可见症状"的开放项就是它 ——
`DesignCanvas.BuildControl` 把**显示名**当**标识符**（`Name = 「按钮」+ 序号`）⇒ 同一份工程在 zh / en
下编辑得到**两份工程**（名字还进生成代码符号）。用户拍板走「**工程里存中性名 ＋ 界面按语言显示别名**」。

- **单一来源** `ControlNameDefaults`（新文件）：`NeutralPrefix` / `UniqueName` / `Display` / `ToPersisted`。
  三条不变式各有判据：① 跨语言（zh / en 下**逐字相同**，且是中性的 `Button1` / `TextArea2`）；
  ② **左逆** `ToPersisted(Display(n,t),t) == n`（26 个类型 × 2 种语言逐个走）；
  ③ **显示不动**：旧工程里的 `按钮1` 写回归一成 `Button1`，界面**一字不变**（一次迁移）。
- **显示面**：11 个文件 / **23 处 `Display`** ＋ **2 处 `ToPersisted`** —— 属性面板（标题行 ＋ 名称框）、
  交叉引用「控件名」列、工程树节点 ＋ 改名预填/提示、撤销重做描述（4 处）、删除快照描述 ＋
  预览状态栏、画布属性变更撤销描述、动画「目标控件」下拉、画布未设 `Text` 时的回落标签（8 处渲染支）。
  ⚠ 如实记为**边界**：AI 上下文（`RecommendationContextName`）、按名查控件、交给绑定引擎的那些点**保持原样**。
- **判据 9 条**（`ControlDefaultNameTests`）：J1 走**生产路径**（`AddControlAt`）在 zh / en 下各建 3 个控件
  逐字对拍（本项核心）；J8 是**清单式**源码契约守卫（显示面/写回点被改回去时当场红，新显示点要人肉加表）。
- 🩸 **同阶段被仓库自己的守卫抓了一次**：新测试类切全局语言却没挂 `[Collection("Language-global")]`
  ⇒ `LocalizationRefreshUiTests.EveryTestClassThatSwitchesLanguage_IsInTheGlobalCollection` 当场红
  （那条守卫第一次在**新写的类**上兑现）。补挂即绿。
- 🩸 **门禁第一次红在"文件不许长大"那条守卫上**：`SingleFileSizeGuardTests` 钉着
  `MainWindow.axaml.cs` 的**行数基线 2992**，而本刀在那处把一次调用拆成了两行 ⇒ 2993 ⇒ 第 5 步红。
  处置：**把那次调用压回一行**，行数不动、**不改任何基线**。教训与「动面就重数」同族 ——
  **改产品代码也会撞上这类守卫**，先查有没有文件被钉着行数。
- **负控 5 支**（逐支独立日志 + 还原 SHA256 逐字节相同 + 收尾重建）：nc1 默认名改回本地化 ⇒ J1 红；
  nc2 `Display` 返回原样 ⇒ J3/J7 红；nc3 `ToPersisted` 恒等 ⇒ J3 红；nc4 允许序号 0 ⇒ J5 红；
  nc5 面板写回直写 ⇒ J8 红。
- **诚实边界**（都写进 `ControlNameDefaults` 类注释）：手工把控件命名成 `Button1` 会被当成默认名；
  控件默认**显示文案** `Text` 仍按语言（有意 —— 给操作员看、可改，与标识符分开）；
  ~~属性面板的文本框写回路径在本 headless 装配下**驱动不动**（实测 `PropText` 同理）⇒ 那一半用源码契约 ＋ 单元左逆判。~~
  🩸 **阶段 160 更正**：那是**程序化赋值**不触发 `TextChanged`（Avalonia 12 的已知行为，本仓
  `PropertyPanelUiTests.TypeInto` 的注释早就写着），**真实键盘输入驱动得动** ⇒ 阶段 160 已把它升级成
  行为判据 `PropertyPanel_TypedAlias_WritesBackNeutralName`（敲 `按钮2` ⇒ 落盘 `Button2`）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；
脚本 **29**；路径卫生 **672**（＋1 ＝ 新增专题文档）；步 5 逐程序集 `44/44/4/78/96/2140/315/639/8`
（合计 **3368**）＋ 步 6 独占 `8` ＝ **3368 通过 / 0 失败**（阶段 158 为 3359，**+9**）；固件 host `[9/9]`；
全量 App.UiTests **639 / 0**（连跑 2 次，31 / 31 s）。专题 ⇒ `docs/2026-10-01-control-default-name-reproducibility.md`。

---

## 阶段 160（2026-10-01）—— 控件名/类型的**显示面**收口：补 `#61o` 的剩余面 ＋ 补上 159 漏掉的两处

**一句话**：阶段 159 的显示面守卫是「**有没有**」式的（文件里有一次正确调用就绿）⇒ `MainWindow.axaml.cs`
里**另外两处**（多选/单选状态栏）直显**中性名 ＋ 英文枚举名**照样绿灯。本阶段先发现再补齐，并把两个守卫
都升级成**逐文件计数**；同时把 `#61o`（阶段 96 只枚举到三个露出点）剩下的 5 处类型显示补上。

- **类型显示（5 处）**：属性面板标题行（已命名/未命名两档）、工程树控件节点、动画「目标控件」下拉、
  画布 `Text` 为空时的回落标签（2 处渲染支）、面单编辑器「控件摘要」类型列表 —— 全部改走
  `ControlTypeTextKeys.Describe` ⇒ 中文界面下 `按钮1 (Button)` 变 `按钮1 (按钮)`。
- **名字显示（2 处）**：`MainWindow` 的多选/单选状态栏从 `first.Name`/`first.Type` 改成
  `ControlNameDefaults.Display(first)` ＋ `ControlTypeTextKeys.Describe(first.Type)` ⇒ `按钮1 (按钮)`。
- **守卫升级**：两张表 —— **名字显示** 10 文件 / 24 处（`MainWindow.axaml.cs` **3**）、
  **类型显示** 10 文件 / 36 处（`ToolboxView.axaml.cs` **26** 是原始消费者）。负控 **nc2**（把状态栏
  改回去 ⇒ 计数守卫红）就是"这条升级能抓住阶段 159 的漏点"的实证。
- 🩸 **两次自我更正**：① 阶段 159 那句"属性面板文本框写回**驱动不动**"是**量的方法不够** ——
  真相是 **Avalonia 12 里程序化赋值 `TextBox.Text` 不触发 `TextChanged`**（本仓
  `PropertyPanelUiTests.TypeInto` 的注释早就写着），**真实键盘输入驱动得动** ⇒ 补行为判据
  `PropertyPanel_TypedAlias_WritesBackNeutralName`（敲 `按钮2` ⇒ 落盘 `Button2`、敲 `报警灯` ⇒ 原样）；
  ② 「有没有」式守卫会漏同一文件里的其它显示点（见上）。
- **既有判据被改掉一条**：`PropertyPanelUiTests.BindControl_PopulatesEditorsFromModel` 原本断言标题行含
  `control.Type.ToString()`（英文枚举名）—— 那正是本次修掉的缺陷，改成断言含本地化类型显示名。
- **新增判据 5 条**：`ControlTypeDisplayTests` J1（26 个类型两语言两两不同 ＋ 中文名 ≠ 英文枚举名，
  实测同名 **0** 个）/ J2（类型显示面计数）/ J3（属性面板标题行，行为）/ J4（工程树节点，行为）＋
  `RenameWritePoints_GoThroughToPersisted`（两个改名写回点）。
- **负控 5 支**（逐支独立日志 + 还原 SHA256 逐字节相同）：属性面板类型改回枚举名 / **状态栏改回
  `first.Name`/`first.Type`** / 面单摘要改回 `ToString()` / `Describe` 返回枚举名 / 改名绕过 `ToPersisted`。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；
脚本 **29**；路径卫生 **672**；步 5 逐程序集 `44/44/4/96/78/2140/315/645/8`（合计 **3374**）
＋ 步 6 独占 `8` ＝ **3374 通过 / 0 失败**（阶段 159 为 3368，**+6**）；固件 host `[9/9]`；
全量 App.UiTests **645 / 0**（连跑 2 次，34 / 31 s）。专题 ⇒ `docs/2026-10-01-control-type-display-localization.md`。

---

## 阶段 161（2026-10-01）—— 归档问题单 `dock-blank-after-drag` **结案**：拖到停靠区之外 ⇒ 面板消失（闭环 `#26`）

**一句话**：`#26` 的最后一条尾巴是"其余拖拽路线未覆盖 ＋ 原问题单症状成因仍未定论"。把
「拖到**中心区**」与「拖到 **`DockControl` 之外**」补成指针判据后，**第二条当场复现了归档单的现场签名**
（`RootDock.Windows = 1` 而视图 DETACHED、无任何可视后代）—— 根因是**本仓工厂视图无关**。

- **根因**：`HmiDockFactory : Dock.Model.Mvvm.Factory`（视图无关）⇒ `FloatDockable` 只动**模型**
  （摘面板 ＋ `AddWindow`），而给 `DockWindow` 建真窗口是 `Dock.Model.Avalonia` 那一层的事 ——
  本仓**既没引它也没接那条线**（`grep -rn "HostWindows\|Dock.Model.Avalonia" src/` → **0 命中**，
  实测 `IDockWindow.Host` 恒 `null`）。⚠ 归档单原来四个假设（A/B/C/D）**都不是**根因；
  这也解释了 2026-09-17 那次复测"退回原始缺陷判据仍全绿"—— **它与 `ViewLocator` 解析链无关**。
- **可达性**：`MainWindow.axaml` 里 `DockControl` 只占主窗口**第 3 行**（上面菜单/工具栏、下面状态栏）
  ⇒ 把标签拖到那些区域就是"落在停靠区之外"，**用户随手可达**；而布局持久化只存
  `Id/Proportion/IsOpen`、不含 `Windows` ⇒ 面板**在本会话里消失**（重启才回来）。
- **修复**：`HmiDockFactory.FloatDockable` **覆写成空实现**（明确拒绝浮动）⇒ 落在停靠区之外的处置
  变成"**面板留在原地**"。⚠ 实现浮动（宿主窗口层 ＋ 位置/关闭/主题/多显示器语义）是**新功能**，
  不是这条缺陷的修复；契约判据会把将来做浮动时要改的断言当场指出来。
- **判据**：`DockDragRegressionUiTests` **3 → 6 条** —— 新增「拖到中心区」（本仓中心区是 `ToolDock`，
  放置点取中心区那幅画布的中心）、「拖到停靠区之外」（**结尾带前提自检**：同一夹具里拖进中心区必须
  真的改变承载 dock，否则整条用例空转不作数）、「`FloatDockable` 契约」（`Windows` 保持 0、
  父 dock 不变、视图仍附着、12 个面板不少）。
- **负控 3 支**（逐支独立日志 + 还原 SHA256 逐字节相同）：nc1 **把修复退回**（`base.FloatDockable`）
  ⇒ 外面那条 ＋ 契约那条**都红**（修复承重）；nc2 指针手势空转（`steps = 0`）⇒ 前提自检红；
  nc3 `PanelState` 恒判已附着 ⇒ 既有判据自检红。
- **归档单** `docs/archive/2026-08/debug-dock-blank-after-drag.md`：**OPEN → CLOSED**（附结案节）。
- ⚠ **覆盖边界**：多屏/多窗口在本仓**不可达**（浮动被拒 ⇒ 永远单窗口）；归档单原文的逐字方向
  （拖**项目浏览器**到工具箱）仍打不到（它的标签条只有 **1 px** 高），由镜像方向覆盖。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；
脚本 **29**；路径卫生 **672**；步 5 逐程序集 `44/44/4/96/78/2140/315/648/8`（合计 **3377**）
＋ 步 6 独占 `8` ＝ **3377 通过 / 0 失败**（阶段 160 为 3374，**+3**）；固件 host `[9/9]`；
全量 App.UiTests **648 / 0**（连跑 2 次，35 / 31 s）。专题 ⇒ `docs/2026-10-01-dock-float-refusal.md`。

---

## 阶段 162（2026-10-01）—— 新登记 `#79`：**关闭面板之后从「面板」菜单再也打不开**（当场闭环）

**一句话**：上一刀的**直接后续** —— 既然"这套 Dock 集成里有会静默失效的路径"，就顺着往下量，
量到"关闭"这条**更常见**的路径上也有一个：标签栏的 × 走 `CloseDockable`，把工具**从布局里整个摘掉**
（既不在 `VisibleDockables`、也没进任何 `HiddenDockables`，只留 `Owner` 指针）⇒ `OpenDockTool`
用 `FindDockable` **找不到它**、**菜单点了没反应**；本仓又没有"重置布局"可点 ⇒ 用户**在本会话里丢一个面板**。

- **现场读数**（修复前）：`afterClose=(False,38)` / `visibleAfterClose=False` /
  `hiddenAfterClose=False` / `ownerAfterClose=BottomDock` / `foundNull=True` / `tools=11`。
  ⚠ 可达性：Dock 标签栏对 `CanClose==true` 的工具渲染 ×（本仓只有画布显式设 `false`），判据里读数。
- **修复（三处）**：① `CreateLayout()` 末尾登记 **工具 id → (实例, 承载 dock)** 的"老家"表
  （每次重建布局刷新）；② 新增 `TryRestoreClosedTool(layout, id)` —— 按老家 `AddDockable` ＋
  `IsOpen` ＋ 激活，两条护栏（还在布局里 / 换过布局 ⇒ `false`）；③ 「面板」菜单那一跳收成
  **`HmiDockFactory.OpenDockable(layout, id)`**（先找、没有就挂回老家），并把逻辑**从
  `MainWindow.OpenDockTool` 移进工厂** ⇒ 判据**够得着产品路径**，且 `MainWindow.axaml.cs`
  **2992 → 2983**（单文件基线同步改小，符合"只许变小"）。
- **判据** `DockDragRegressionUiTests` **6 → 8 条**：`CloseThenReopenViaMenu_RestoresThePanel`
  （前提自检：关掉后**真的**不在布局里、工具数 11；修后必须回到某个 dock 且**视图重新附着**、工具数回 12）
  ＋ 接线契约 `MainWindowMenu_GoesThroughTheFactoryOpenDockable`（源码级）。
- **负控 4 支**（逐支独立日志 + 还原 SHA256 逐字节相同）：去掉 `AddDockable` / 不登记老家 /
  不做回退 / 菜单改回内联 `FindDockable` ⇒ 逐支红在期望那条腿。
- **与 `#26` 同一族的两种处置**：浮动（东西进了没有宿主窗口的 `DockWindow`）⇒ **拒绝它**；
  关闭（东西从布局摘掉）⇒ **记住老家挂回去**。共同教训：**"关掉/拖走的东西去哪了"必须有人负责**
  —— 要么明确拒绝、要么明确挂回来；两者都不做时症状是"面板凭空消失**且菜单点了没反应**"。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；
脚本 **29**；路径卫生 **672**；步 5 逐程序集 `44/44/4/96/78/2140/315/650/8`（合计 **3379**）
＋ 步 6 独占 `8` ＝ **3379 通过 / 0 失败**（阶段 161 为 3377，**+2**）；固件 host `[9/9]`；
全量 App.UiTests **650 / 0**（连跑 2 次，32 / 31 s）。专题 ⇒ `docs/2026-10-01-dock-close-reopen.md`。

---

## 阶段 163（2026-10-01）—— 新登记 `#80`：**「面板」菜单重开的三个入口**（藏 / 钉 / 闭）

**一句话**：上一刀（§78）只在**"被 × 摘掉"**那条路上验过，可"菜单里点一下某面板"其实有**三个入口** ——
**闭**（× 摘掉）/ **藏**（进 `HiddenDockables`）/ **钉**（进 `BottomPinnedDockables`），三者落点不同、
`FindDockable` **都看不见**；后两条混用上一刀的 `AddDockable` 修法会留下用户看得见的脏状态：
**藏**的被插成**同一个 dock 里两条同名项**，**钉**的**面板回来了、钉条条目清不掉**。

- **现场读数（修复前）**
  - **藏**：`AddDockable → BottomDock×1`，`IsOpen=true → BottomDock×2`（⚠ `IsOpen` 这一步 Dock 自己
    就把它放回 owner ⇒ 上一刀的 `AddDockable` 是**第二遍**）；产品路径 `OpenDockable → BottomDock×2`。
  - **钉**：**真的点** dock chrome 上的 `PART_PinButton`（`DataContext` = 那张 `ToolDock`；**标签栏里
    没有这个按钮** —— 实测 `ToolTabStripItem` 内 0 个按钮）⇒ 工具移出可见 dock、视图脱落、
    `tools=11`、`钉列表=B:[AuditLog]`；再走产品路径 ⇒ 面板回来了但 `钉列表=[B]` **留着**（脏状态）。
    另测 `RestoreDockable` 对**钉住**的工具没作用（仍在 `(无)`、钉列表不变、视图仍脱落）。
- **修复（`HmiDockFactory.TryRestoreClosedTool` 三条入口分开走）**：**钉** ⇒ `UnpinDockable`（实测：放回
  原 dock **并重新附着视图**、钉列表清空）；**藏** ⇒ 只置 `IsOpen = true`；**闭** ⇒ 按老家 `AddDockable`；
  之后统一按 `Owner`（真正落回去的那张 dock）激活。
- **自纠（诚实记录）**：中途判定"点钉按钮后 `IsDockablePinned` 返回 `false` ⇒ 只看 API 会漏"并加了
  "再扫四张钉列表"的分支 —— 复核发现那是**枚举口径的假象**（测试的 `Walk` **不走四张钉列表**，
  被钉走的工具没被枚举到）。处置：**删掉多余分支**（无判据不加代码）＋ **`Walk` 补上四张钉列表**，
  判据前提改成"钉住后布局模型里的工具数**仍是 12**"。⇒ 没有"`IsDockablePinned` 漏判"这条负控（实测绿、不具区分度）。
- **判据** `DockDragRegressionUiTests` **8 → 10 条**：`HiddenTool_ReopenedViaMenu_AppearsExactlyOnce`
  （**恰好 1 条** ＋ 不在 `HiddenDockables` ＋ 视图附着）＋ `PinnedViaDockChrome_ReopenedViaMenu_
  ComesBackAndClearsThePin`（**真手势**点击 ⇒ 四张钉列表都空 ＋ 恰好 1 条 ＋ 视图附着）。
- **负控 6 支**（逐支独立日志 + 还原 SHA256 逐字节相同）：nc1 「藏」也 `AddDockable` ⇒ **2 次** ·
  nc2 「藏」不置 `IsOpen` ⇒ **0 次** · nc3 `IsHidden` 恒 `false` ⇒ 退回 nc1 形态 · nc4 判据不驱动隐藏 ⇒
  **前提自检**红 · nc5 去掉「钉」那一支 ⇒ `钉条里还留着它（B）` · nc7 钉住手势不点 ⇒ 前提自检红。
- **新登记 `#80`（产品决策）**：`LayoutSerializer` 只走 `VisibleDockables` ⇒ 「藏 / 钉」状态**不进持久化**，
  钉住的面板**重启后回到可见区**（不丢，但设置不保留）；要闭环先定"跨会话保留（改盘面格式 ＋ 迁移）"
  还是"明确钉住不跨会话"。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；
脚本 **29**；路径卫生 **672**；步 5 逐程序集 `44/44/4/78/96/2140/315/652/8`（合计 **3381**）
＋ 步 6 独占 `8` ＝ **3381 通过 / 0 失败**（阶段 162 为 3379，**+2**）；固件 host `[9/9]`；
全量 App.UiTests **652 / 0**（连跑 2 次，各 31 s）。专题 ⇒ `docs/2026-10-01-dock-reopen-pin-and-hide.md`。

---

## 阶段 164（2026-10-01）—— 把 `#80` 的「**不丢**」量成判据；顺带拆掉一颗**启动期雷**（`ApplyState`）

**一句话**：`#80` 里"藏 / 钉状态不进持久化 ⇒ 重启后面板**不丢**（只回到可见区）"这句此前**是从源码推的**；
本阶段把它量出来并钉成判据，同时把探路时撞到的一颗真雷修掉 ——
`LayoutSerializer.ApplyState` **边枚举 `VisibleDockables` 边应用状态**，遇到盘面里 `IsOpen = false`
的项时 `SetIsOpen` 会驱动 Dock 把该 dockable 从这张列表里摘掉 ⇒ `Collection was modified` 异常；
而调用点在 **`MainWindow` 构造期、没有 try/catch** ⇒ 一个这样的 `.layout.json` 就是**启动期异常**。

- **量到的现状**（`RestartAfterCloseOrHide_KeepsEveryPanel_NoDuplicates`）：藏一个（`HideDockable`）＋
  关一个（`CloseDockable`）⇒ 存盘前 `tools=11`、`CaptureState` **不含**这两个（前提自检）⇒
  **新工厂重建布局** ＋ `ApplyState` ⇒ `tools=12`（去重 12）、两个面板各 1 条且**视图附着**。
  ⇒ **"不丢"成立**；代价是可见性设置不保留（关掉的面板重启后**会自己回来**）。
- **修法**：`ApplyRecursive` 应用前先 `dock.VisibleDockables.ToList()` **快照**。
  判据 `ApplyState_WithIsOpenFalseEntry_DoesNotThrow`：不抛异常 ＋ 工具不丢（12 个）＋ 无重复 ＋
  **标志真的生效**（那一个不在任何 dock 的可见列表里 —— 否则"没抛异常"会恒绿）。
- **判据** `DockDragRegressionUiTests` **10 → 12 条**；**负控 4 支**（逐支独立日志 + 还原 SHA256 逐字节相同）：
  nc1 `CaptureRecursive` 也走 `HiddenDockables` ⇒ 前提自检红 · nc2 判据"重启"复用同一布局 ⇒ 不变量红 ·
  nc3 `ApplyState` 误删一个可见工具 ⇒ 不变量红 · nc4 退回"边枚举边应用" ⇒ `IsOpen=false` 判据红。
- **偶发登记**：门禁首跑时 `DispatcherOwnershipContractTests.CrossDispatch_…`（`#47` 哨兵）红过一次
  （`ownerThread=5, sessionThread=33`），同一棵树**下一跑全绿**、该程序集单独连跑 4 次也全绿
  ⇒ 按**判据脆弱**新登记 **`#81`**（假说：xUnit 跨类并行 ⇒ 进程级 `Dispatcher` 归属被别的会话线程合法持有）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；
脚本 **29**；路径卫生 **672**；步 5 逐程序集 `44/44/4/78/96/2140/315/654/8`（合计 **3383**）
＋ 步 6 独占 `8` ＝ **3383 通过 / 0 失败**（阶段 163 为 3381，**+2**）；固件 host `[9/9]`；
全量 App.UiTests **654 / 0**（连跑 2 次，31 / 32 s）。专题 ⇒ `docs/2026-10-01-dock-persistence-restart.md`。

---

## 阶段 165（2026-10-01）—— 产品决策落地：重启「**只打开保存的面板**」（`#80` 闭环）

**决策原文**：「被 × 关掉的面板，下次启动只打开保存的面板。」

**一句话**：盘面新增一份「**保存时在可见布局里的工具 id**」（`LayoutState.VisibleToolIds`）；
启动时 `HmiDockFactory.RestoreLayoutState` 先 `ApplyState`、再按这份清单裁剪 —— 不在清单里的工具走
× 那条路（`CloseDockable`）**保持关闭**（可从「面板」菜单再打开），**旧盘面**（字段缺失）**不裁剪**
（`null` ＝ 信息缺失 ≠ 空集，否则老用户升级后所有面板消失）。

- **语义**：可见 ⇒ 打开；被 × 关掉 / 藏起来 / 钉住 ⇒ 都不在可见树里 ⇒ **保持关闭**（同一条规则，
  都能从那菜单找回）；旧盘面 ⇒ **全部打开**。
- **实现（三处，`MainWindow` 行数不变）**：① `CaptureState` 记下可见工具 id（字段带 `null` 语义注释）；
  ② 新增 `HmiDockFactory.RestoreLayoutState(layout, state)`（先 `ApplyState`、再 `CloseDockable` 裁剪）；
  ③ `MainWindow` 那一行 `LayoutSerializer.ApplyState(...)` → `_dockFactory.RestoreLayoutState(...)`
  —— **1 行换 1 行**，`MainWindow.axaml.cs` 仍 **2983**。
- **判据**（`DockDragRegressionUiTests` **12 → 13 条**）：`Restart_OpensExactlyTheSavedPanels`
  （前提自检：盘面里有清单、没有藏/闭那两个、有画布；重启后**恰好 10 个**、id 无重复、这两个不在任何 dock
  的可见列表、**也没进 `HiddenDockables`**、留下的都在 dock 里、附着视图不少于初始布局 —— ⚠ 非活动标签惰性，
  初始实测 **5/10**；恢复腿：`OpenDockable` ⇒ 恰好 1 条 ＋ 视图附着）＋
  `Restart_WithLegacyStateWithoutVisibleToolIds_OpensEveryPanel`（旧盘面 ⇒ 12 个全打开）。
- **负控 4 支**（`.workbuddy/tmp/s165-nc.py`，逐支还原 SHA256 逐字节相同）：nc1 不裁剪 · nc2 采集不记清单 ⇒
  前提自检红 · nc3 旧盘面当空集 ⇒ 向后兼容判据红 · nc4 裁剪改用 `HideDockable` ⇒ 状态那条腿红。
- **`#80` 闭环**：产品决策落地，「不丢」由恢复腿继续守着（面板菜单能找回）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；
脚本 **29**；路径卫生 **672**；步 5 逐程序集 `44/44/4/96/78/2140/315/655/8`（合计 **3384**）
＋ 步 6 独占 `8` ＝ **3384 通过 / 0 失败**（阶段 164 为 3383，**+1**）；固件 host `[9/9]`；
全量 App.UiTests **655 / 0**（连跑 2 次，33 / 31 s）。专题 ⇒ `docs/2026-10-01-dock-restart-open-saved-panels.md`。

---

## 阶段 166（2026-10-01）—— `#81` 那条假阳查清了：**`#47` 哨兵原先在闸门之外读数**（`#81` 闭环，新登记 `#82`）

**一句话**：阶段 164 门禁首跑红过一次「跨派发不许有第三方持有 `s_uiThread`」（`ownerThread=5, sessionThread=33`）。
本阶段把机制**确定性**量了出来：`UiTestHost.Pump` 有一条**文档里明写的合法路径**（派发之外调它时
**按调用线程**取 Dispatcher），而**一次派发结束后 `s_uiThread` 是 `null`**（PerTest 隔离的 scope 退出时
又重置一遍 ⇒「窗口大部分时间是开着的」）⇒ 后台线程**只做一次合法的 `Pump(1)`**，读数就变成
`owner=19, session=7` —— **现场没有任何非法访问**。旧哨兵在**闸门之外**读数，于是把这件事读成了"偷手"。

- **修法**：新增 `UiTestHost.ObserveAmbientOwnerOutsideDispatch()` —— **持闸门**派发两次、**仍持闸门时**读归属：
  别人的派发/`Pump` 挤不进来 ⇒ 还能被读到的只剩"**绕过闸门**碰 Avalonia 的线程"（`#47` 真正要抓的偷手）。
  哨兵改用它；消息措辞从「派发之外的」改成「绕过闸门的」。**检测力不降，假阳没了。**
- **判据**：`CrossDispatchRead_IsNotDisturbedByOtherThreadsLegalPlatformTouches`（后台合法 `Pump` 干扰下连读
  **40** 次，一次都不许报假阳）。**负控 2 支**（`.workbuddy/tmp/s166-nc.py`，逐支还原 SHA256 逐字节相同；
  连跑 2 次）：nc1 闸门外先读（= 旧哨兵形态）· nc2 派发＋读数都做了但**没持外层闸门** ⇒ 两支都红。
- **两个踩过的坑（都写进专题）**：① 「假阳形状一定出现」**不可断言**（依赖窗口开着，完整套件里不稳定
  —— 第一版判据偶发红 `owner=48, session=48`）⇒ 判据只断言修法那一半；② 为强开窗口去反射调
  `Dispatcher.ResetBeforeUnitTests()` 是**进程级**操作，跨类并行下等于给别人添乱 ⇒ **删掉**，不在判据里做进程级操作。
- **新登记 `#82`（未定论的窄窗）**：`Pump` 的「已在派发体内」判定用的是**全局** `SessionThreadId`
  （= 最后一次派发的线程）⇒ 理论上那条窄窗存在；**构造判据失败**（窗口卡不住）⇒ 按「无判据不加代码」只登记。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；
脚本 **29**；路径卫生 **672**；步 5 逐程序集 `44/44/4/96/78/2140/315/656/8`（合计 **3385**）
＋ 步 6 独占 `8` ＝ **3385 通过 / 0 失败**（阶段 165 为 3384，**+1**）；固件 host `[9/9]`；
全量 App.UiTests **656 / 0**（连跑 2 次，各 31 s）。专题 ⇒ `docs/2026-09-21-dockpanel-invocation-net.md` §11。

---

## 阶段 167（2026-10-01）—— 复核 `#82`：**前提不成立**（登记的窄窗够不着）；顺带量到跳过支路是必需的

**一句话**：阶段 166 登记 `#82` 时怀疑「`Pump` 的『已在派发体内』判定用的是全局 `SessionThreadId`
⇒ 本线程恰好等于上一次派发的线程时会跳过闸门」。本阶段先量**前提**：
`HeadlessUnitTestSession` 有一个**专用会话线程** —— 从 **3 个不同调用线程**各派发若干次，
派发体**全部**跑在同一个线程上（`t13×11`），而 `SessionThreadId` 记的就是它 ⇒
**别的线程不可能等于它** ⇒ 那条窄窗**没有入口**。`#82` 按「复核后不成立（前提不成立）」结案。

- **判据** `HeadlessDispatchContractTests.PumpGateSkip_IsReachableOnlyFromTheDedicatedSessionThread`：
  ① 派发体线程集合只有 1 个（从 ≥3 个不同调用线程派发）· ② 非派发线程的 `Pump` **一次都不许**走跳过支路 ·
  ③ **正控**：派发体内 `Pump` 必须走跳过支路（否则计数器是死的、②是假绿）。
- **顺带量到**：那条跳过支路**不是优化而是必需** —— 派发体在**持有闸门**时调 `Pump`，
  若它去抢闸门就是抢自己握着的锁；s167 负控实测：把判定改成「恒不跳过」⇒ 立刻自锁
  （`派发闸门等待超时（疑似自锁）：sessionThread=13, callingThread=13`，2 分钟后才抛）。
- **又一次「量具口径」教训**：判据第一版用**全局**计数 delta，在完整套件里立刻红
  （`非派发线程的 Pump 跳过了闸门 107 次`）—— 看着像窄窗复现，其实是**别的测试类**在派发体里
  **合法**跳过被算进了我的 delta ⇒ 改用**线程局部**计数（新增 `PumpUngatedEventsOnCurrentThread`），
  全局计数只当诊断。
- **负控 2 支**（`.workbuddy/tmp/s167-nc.py`，逐支还原 SHA256 逐字节相同）：nc1 跳过判定恒 `true` ⇒
  ② 红 · nc2 跳过支路照旧但不记账 ⇒ ③ 正控红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；步 5 逐程序集 `44/44/4/96/78/2140/315/657/8`（合计 **3386**）＋ 步 6 独占 `8`
＝ **3386 通过 / 0 失败**（阶段 166 为 3385，**+1**）；固件 host `[9/9]`；
全量 App.UiTests **657 / 0**（连跑 2 次，32 / 34 s；阶段 166 为 656，＋1）；
专题 ⇒ `docs/2026-09-21-dockpanel-invocation-net.md` §11.5。

---

## 阶段 168（2026-10-01）—— 口令下限的**单位**：`#20b` 复核不成立，顺路闭环 `#83`

**一句话**：按「按顺序推进」量 `#20b`（设备端按字符数判口令下限），量出来它的**前提**就不成立 ——
设备端那处 `strlen` 的源码注释逐字写着「**这是省算力，不是密码策略执行点**」，还刻意不计入失败计数
（避免锁定变成 DoS 面）⇒ 收紧它零安全收益、只会拒绝合法旧口令。**但顺手量出了一个真缺陷**：
`AuthenticationService.SetUserPassword` 仍按 **UTF-16 码元**判下限，而模型语义/编辑器/设备端都是**字节**
⇒ 下限 6 时 `"电机"`（6 字节/2 码元）在「工程设置 → 运行时用户」被**接受**、在「用户管理 → 重置密码」
被**拒绝**；而它抛的文案写「个字符/characters」、参数却是字节数 ⇒ 对 CJK 用户**双重对不上**。

- **修复**：① `SetUserPassword` 改 `Encoding.UTF8.GetByteCount` 并把**字节数**作为 `actual` 传下去；
  ② 回落原文 ＋ 两本字典按字节说（zh 值与回落一字不差，既有判据钉着）；③ 编辑器那句与代码相反的
  `<summary>`（「长度按**字符数**判」）更正。
- **判据**：Core ＋2（字节口径能设能登 —— 含前提自检：字节 6/码元 2；过短的仍拒且 `Args` 两个数都是字节）
  ＋ App ＋2（文案必须写字节、不许写字符，含回落前提自检；两处执行点源码契约，**去注释后**判）。
- **负控 3 支**（`.workbuddy/tmp/s168-nc.py`，逐支还原 SHA256 逐字节相同）：退回码元 / 文案改回「字符」/
  编辑器退回码元 ⇒ 逐支红在期望那条腿。
- **登记**：`#20b` 结案为「复核后不成立（登记有误：把省算力短路当成了策略执行点）」；
  新登记 `#83`（**当场闭环**，本阶段这个真缺陷）与 `#84`（**仍开着 · 产品决策**：要不要把下限的
  语义从字节改成字符 —— 落点在设置口令那一处，收紧不锁门；设备端那处短路保持字节）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；脚本 **29**；路径卫生 **672**；
步 5 逐程序集 `44/44/4/78/96/2142/315/659/8`（合计 **3390**）＋ 步 6 独占 `8` ＝ **3390 通过 / 0 失败**
（阶段 167 为 3386，**+4**）；固件 host `[9/9]`；全量 App.UiTests **659 / 0**（连跑 2 次）。
专题 ⇒ `docs/2026-10-01-password-length-unit.md`。

---

## 阶段 169（2026-10-01）—— 术语与 en 的**可判那半边**（`#27b` 决策备齐；`#27` 客观缺口收口）

**一句话**：按「按顺序推进」量 `#27b`（术语统一）与 `#27`（en-US 校对）—— 两条都是产品决策，
但各自有**不需要决策**的可判部分：① 一句话里**不许出现两个名字**（实测 zh 字典里恰好 1 条踩线，
已修）；② en 值**不许是中文**（实测恰好 1 条，是**语言自名** `menu.lang.zhCN = 中文`，刻意如此）。

- **`#27b` 读数刷新**（登记册原写「工程 30 vs 项目 33」已过期）：**含「工程」44 条 / 含「项目」30 条 /
  同句混用 1 条（已修）**；影响面**只动字典**（产品源码用户可见中文字面量扫描面 = 0，en 侧只有一个词
  `project`）⇒ 决策很便宜。两选项：**A 统一到「工程」**（改 30 条，与 `HMI 工程包 (.hmi)` / `工程模式` /
  全仓文档一致）· **B 统一到「项目」**（改 44 条，但要把文件类型名改成「HMI 项目包」）。**等一句话拍板**。
- **`#27` 客观缺口收口**：en 含汉字的键**恰好 1 条**（语言自名）；en 逐字等于 zh 的 **38** 条**逐条看过**，
  全不含汉字（技术记号，既有惯例）⇒ **机器能判的缺口 = 0**。剩余是**翻译质量**（需母语级判断），如实留开。
- **判据** 新增 `TerminologyConsistencyGuardTests` **3 条**：一句话一个名字（含前提自检）·
  **反向配重**（防"统一后扫描面归零"的假绿）· en 不许含汉字（白名单**双向**）。
- **负控 2 支**（`.workbuddy/tmp/s169-nc.py`，逐支还原 SHA256 逐字节相同）：文案改回混用 ⇒ 判据一红 ·
  en 塞中文 ⇒ 判据二红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；脚本 **29**；路径卫生 **672**；
步 5 逐程序集 `44/44/4/96/78/2142/315/662/8`（合计 **3393**）＋ 步 6 独占 `8` ＝ **3393 通过 / 0 失败**
（阶段 168 为 3390，**+3**）；固件 host `[9/9]`；全量 App.UiTests **662 / 0**（连跑 2 次）。
专题 ⇒ `docs/2026-10-01-terminology-and-en-decisions.md`。

---

## 阶段 170（2026-10-01）—— 术语统一**落地**（`#27b` 选项 A：统一到「工程」）；`#84` 结案（保持字节）

**决策**（用户拍板）：**`#27b` 统一到「工程」** · **`#84` 口令下限保持「字节」** · 下一条方向做**报表能力**。

- **`#27b` 落地**：zh 字典 **30 条**「项目」→「工程」（含「工程」的键 **44 → 73**，含「项目」的键 **30 → 0**）；
  与字典**配对**的回落原文同步 2 条（`AiOutputValidator` / `GeneratedCodeValidator` ——
  判据钉着"字典渲染 == 回落原文"逐字相同，它们必须跟着改）。
- **守卫收紧**（`TerminologyConsistencyGuardTests`）：`ChineseValues_UseOnlyTheUnifiedTerm`（弃用词不得回流，
  白名单**双向**）· `ChineseValues_StillUseTheUnifiedTerm`（**反向配重**：含「工程」的键 ≥ 20，
  防"清空文案"式假绿）· en 不许是中文（不变）。
- **负控 3 支**（`.workbuddy/tmp/s170-nc.py`，逐支还原 SHA256 逐字节相同）：过期白名单 ⇒ 判据一红 ·
  zh 里「工程」全清 ⇒ 反向配重红 · AI 配对回落改回「项目」⇒ `AiIssueLocalizationGuardTests` 红；
  阶段 169 那 2 支复跑仍全绿。
- **自我更正 ＋ 新登记 `#85`**：决策文档原写"只动字典"是按**本地化扫描面**（App 层 = 0）说的，
  而 `src/HmiEditor.AI` **不在那个扫描面内** ⇒ AI 侧 **15 行**（工具描述/结果/提示词/回落）
  **没跟着统一**，如实登记（"先要判据再动"）；**生成产物注释 3 行**刻意不动（改字节＝改产物）。
- **`#84` 结案**：保持字节（现状即契约）；口径由阶段 168 的 4 条判据守住。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；步 5 逐程序集
`44/44/4/78/96/2142/315/662/8`（合计 **3393**）＋ 步 6 独占 `8` ＝ **3393 通过 / 0 失败**；
固件 host `[9/9]`；全量 App.UiTests **662 / 0**（连跑 2 次）、Core.Tests **2150 / 0**。
专题 ⇒ `docs/2026-10-01-terminology-and-en-decisions.md`。

---

## 阶段 171（2026-10-01）—— **报表能力**：现状读数、选型与 MVP 定义（定义批，未动产品代码）

**决策**（用户拍板）：下一条方向 = **A 报表能力**。本阶段做**动手前的定义**（不写产品代码）：

- **现状读数（实测）**：**配方 CSV 是唯一的 CSV 导出**（`RecipeManager.ExportCsv():198`，带转义、与导入可往返）；
  **审计日志导不出**（面板只有刷新/过滤）；报警/历史编辑器侧无导出；**设备侧 CSV 零命中、无打印栈**；
  菜单只有"导出 C 代码/导出模拟器工程"；**没有任何 PDF/打印依赖**。
- **选型结论：零新依赖** —— 约束是中央包版本管理、离线可用、产物可逐字节判、许可干净、确定性。
  对比后选 **CSV（数据）＋ 自包含 HTML（人看/打印，浏览器打印）**，模板用极小占位符替换、HTML 内联样式零外链。
- **MVP 边界**：`ReportTable`（标题＋列＋行，数据与呈现分离）→ `ReportExporter.ToCsv`（RFC4180、
  `\r\n`、UTF-8 BOM）与 `ToHtml`（自包含、时间由调用方传入）；**第一条数据源 = 审计日志**；面板加导出按钮走
  `UiOperationGuard`；列名/状态文案入字典。**不做** PDF/xlsx · 定时报表 · 邮件/上传 · 设备侧打印 · 报表设计器。
- **判据计划**：CSV 转义（正控：无特殊字符不加引号）· 确定性（逐字节）· HTML 自包含（正控：内联 style 在）·
  审计列序与列名双语对拍 · 端到端两次导出逐字节相同 · 源码契约（走 `UiOperationGuard`）；负控 4 支。
- **诚实边界**：报表来自**编辑器**数据（不是设备现场班次报表）；打印交给浏览器/系统；不做 xlsx/PDF；不做跨时区换算。

**读数（工作树）**：本阶段**未动产品代码与判据**（只有文档 ＋ 登记册），**仍按纪律复跑了一次门禁**
（文档改动本身也可能踩到文档面守卫）：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；脚本 **29**；
路径卫生 **672**；步 5 逐程序集 `44/44/4/96/78/2142/315/662/8`（合计 **3393**）＋ 步 6 独占 `8`
＝ **3393 通过 / 0 失败**（与阶段 170 同）。
专题 ⇒ `docs/2026-10-01-reporting-capability-mvp.md`；登记册 §87（状态：定义已就位，实施按专题 §7 四步走）。

---

## 阶段 172（2026-10-01）—— **报表能力 MVP 第一片**：审计日志 CSV ＋ 自包含 HTML（零新依赖）

**一句话**：按阶段 171 的选型（零新依赖：CSV ＋ 自包含 HTML）落地第一片 —— 报表导出器 ＋ 审计数据源 ＋
面板按钮，全程可判、产物逐字节确定。

- **Core**：`Reporting/ReportTable.cs` · `ReportExporter.cs`（`ToCsv` RFC4180 转义/CRLF、`ToCsvBytes`
  UTF-8 BOM、`ToHtml` 自包含零外链、`FormatUtc` UTC+`Z`；**不读时钟/环境**）· `AuditLogReport.cs`
  （列序固定，列名/结果全走 `localize` ⇒ **Core 不认识语言**）。
- **App**：`Services/AuditReportText.cs`（Core 键 → `Lang` 常量映射，"Core 给键、App 推键"同形）＋
  审计面板「导出报表」按钮与处理函数（走 `UiOperationGuard.Run(Lang.UiOpExportAuditReport, …)`、
  按扩展名判 CSV/HTML、**导出当前列表显示的那些**）。
- **两处对定义稿的更正**：① 列集合按**真实字段**定（`AuditLogEntry` 没有角色字段 ⇒ 时间/用户/操作/结果/来源/详情）；
  ② 列名与结果词**复用**既有键 ⇒ 新增字典键只有 **9 个**；⚠ 副作用（正面）：复用让 **8 个此前的死 `Lang` 常量变活**
  ⇒ `UnusedBaseline` 按纪律 **1104 → 1097**（注释逐个点名）。
- **判据 13 条**（Core 9 ＋ App 4，含正控与源码契约）＋ **负控 5 支**（去转义 / HTML 塞外链 /
  时间退回 `DateTime.Now` / 映射回落成键名 / 绕过 `UiOperationGuard`），逐支还原 SHA256 逐字节相同。
- **仍未做**（第二片候选）：报警与历史数据报表 · 范围/列选择对话框 · 定时导出 · PDF/xlsx（仍"不做"）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；脚本 **29**；路径卫生 **676**；
步 5 逐程序集 `44/44/4/96/78/2151/315/666/8`（合计 **3406**）＋ 步 6 独占 `8` ＝ **3406 通过 / 0 失败**
（阶段 171 为 3393，**+13**）；固件 host `[9/9]`；全量 App.UiTests **666 / 0**（连跑 2 次）。
专题 ⇒ `docs/2026-10-01-reporting-capability-mvp.md` §8。

---

## 阶段 173（2026-10-01）—— **PDF / xlsx 导出**（产品拍板"要做"）：xlsx 手写、pdf 走 SkiaSharp

**一句话**：阶段 171 把 PDF/xlsx 写成"明确不做"，产品随后拍板**要做** ⇒ 按"先量再选"重挑路线：
**xlsx 手写 SpreadsheetML（零依赖）**、**pdf 用 SkiaSharp 的 PDF 后端**（MIT，且**本来就在依赖闭包里** ——
Avalonia 的 Skia 栈）。

- **量到的**：探针实测 `typeface=Noto Sans CJK JP`、`审/计/报/表` 字形 id 非 0、`%PDF-1.4`、含 `/ToUnicode`、
  **固定 creation 时两次导出逐字节相同**（11.5 KB / 一行文本）；`project.assets.json` 里已有 `SkiaSharp/3.119.4`。
- **xlsx**（`Core/Reporting/XlsxReportWriter.cs`）：zip ＋ 手拼 XML；冻结首行、自动筛选、按内容估列宽、
  数值单元格自动识别（其余 `inlineStr`）；**产物逐字节确定**（条目时间戳写死、顺序固定）。刻意不做公式/多 sheet/图表。
- **pdf**（`App/Services/PdfReportWriter.cs`）：A4、表头底纹、分页 ＋ "第 x/y 页"页脚、超宽裁剪；
  字体先按族名匹配、再退"能画『审』的系统字体"，**都没有就明确提示**（新增键），不生成豆腐块。
- **UI**：导出格式 **2 → 4 种**（csv/xlsx/pdf/html，默认 CSV），四种过滤名；新增 3 个字典键。
- **判据**：Core `XlsxReportWriterTests` 6 条 ＋ App `PdfReportWriterTests` 7 条 ＋ **四格式契约** 1 条。
- **负控 5 支**（`.workbuddy/tmp/s173-nc.py`，逐支还原 SHA256 逐字节相同）：xlsx 不转义 · xlsx 时间戳改成现在 ·
  pdf 不画单元格文本 · pdf 去掉分页 · pdf creation 读时钟。⚠ **后两条第一版没抓住**（只比两次调用抓不到
  时钟注入：同一秒内字节相同）⇒ 补**结构断言**（zip 条目时间戳不许是"现在"、PDF `/CreationDate` 必须是传入值）后才红。
- **两处不能按直觉断言的地方**（写进判据注释）：Skia 把字体流放进**压缩对象流** ⇒ raw 扫描看不到 `/FontFile2`，
  字体嵌入改由**体积下限**钉；子集大小**不随字形数线性增长**（4→60 个汉字只 +7%）⇒ 不拿增长当判据。
- **顺带修掉一处"环境把门禁判红"的坑**：加显式 `PackageReference Include="SkiaSharp"` 后构建日志冒出
  **42 条 NU1900**（NuGet 审计取不到漏洞数据：本机 HTTP 缓存只读）。根因不是那条引用，而是 **restore 的警告
  会写进 `project.assets.json` 并在之后的每次构建重放**；把 `NUGET_HTTP_CACHE_PATH` 指到工作区内 +
  `dotnet restore --force` 后构建日志 0 警告 ✓ ⇒ `scripts/ci-gate.sh` 的 restore 前已把它指到
  `obj/nuget-http-cache`（**不关审计、不改工程配置**）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **678**（＋2 新文件）；步 5 逐程序集 `44/44/4/96/78/2157/315/674/8`（合计 **3420**）
＋ 步 6 独占 `8` ＝ **3420 通过 / 0 失败**（阶段 172 为 3406，**+14**）；固件 host `[9/9]`；
全量 App.UiTests **674 / 0**（连跑 2 次，43 / 38 s）。专题 ⇒ `docs/2026-10-01-reporting-capability-mvp.md` §9。

---

## 阶段 174（2026-10-01）—— 报表第二片：**报警记录报表** ＋ 写盘逻辑收成**公共出口**

**一句话**：加一条数据源（报警记录）＋ 把四格式写盘从审计面板里提成 `ReportExport`（唯一一处）——
否则第二个报表面板必然抄第二份，而"同一实现两份必然漂移"是本仓反复踩过的坑。

- **公共出口** `App/Services/ReportExport.cs`：`Render`（**纯函数**：扩展名 → 字节；未知回落 CSV；
  **PDF 缺字体给提示不产豆腐块**）、`SaveAsync`（选格式 ＋ 写盘 ＋ 回报）、`FileTypes()`；
  顺带把**格式词**从 `audit.*` 提成 `report.*`（4 个过滤名 ＋ `report.export.ok` ＋ `report.export.no.font`）。
- **报警报表** `Core/Reporting/AlarmReport.cs`：列序 **时间/报警名称/级别/状态/触发值/确认人**
  （列名复用既有词，只新增「状态」）；**级别与状态走键**（不许直显枚举名，既有教训 `#61o`）；
  触发值**原始字符串优先**；面板 `AlarmListView` 加按钮，导出的是 `FilteredRecords()`
  （顺带抽出该方法，让"显示"与"导出"共用同一筛选口径）。
- **判据**：Core `AlarmReportTests` 5 条 ＋ App `AlarmReportExportGuardTests` 5 条（含公共出口四格式、
  两面板都走公共出口且**面板里没有写出器**的源码契约）＋ 原四格式契约改打在公共出口上。
- **负控 4 支**（`.workbuddy/tmp/s174-nc.py`，逐支还原 SHA256 逐字节相同）：级别直显枚举 ·
  列名回落键名 · PDF 缺字体照样产字节 · 未知扩展名不回落 CSV。
- **顺带撞上两处守卫（都按守卫说的改，没有放宽）**：① **按钮效果普查表**要求给新按钮声明效果
  ⇒ 按既有形状声明 `Never`（点了开文件选择器，headless 无对话框），理由里写明"接线由源码契约判据覆盖"；
  ② **断言风格守卫**禁止"精确调用文本 ＋ 字符串包含" ⇒ 改成不含括号的片段断言（`CSource` 只在 Core.Tests 里）。
- `UnusedBaseline` **1097 → 1098**（报警面板的按钮常量只出现在 axaml 花括号字面量里，与阶段 172 同形状）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **681**（＋3）；步 5 逐程序集 `44/44/4/96/78/2162/315/679/8`（合计 **3430**）
＋ 步 6 独占 `8` ＝ **3430 通过 / 0 失败**（阶段 173 为 3420，**+10**）；全量 App.UiTests **679 / 0**（连跑 2 次）。
专题 ⇒ `docs/2026-10-01-reporting-capability-mvp.md` §10。

---

## 阶段 175（2026-10-01）—— 报表第三片：**历史数据（趋势采样）报表**

**一句话**：这一片与前两片的形状不同 —— 列是**动态的**（时间 ＋ 每个变量一列），而"变量与值的对齐"
是这条数据源唯一的真风险点（对齐错了报表不会报错，只会静静给出错的数据）。

- **Core `HistoryReport`**：列 = 时间 ＋ 每个变量一列；变量列名用**变量 Id 原文**（数据标识符不翻）；
  值按采样顺序对齐、**短了留空而不是补 0**（0 与"没采到"必须可区分）；数值走不变文化；
  **行数封顶**（默认取**最近** 5000 条）。
- **App `HistoryReportText`**：只有标题与"时间"列名两个可翻译项（变量列名原样）。
- **`TrendConfigView`**：加"导出报表"按钮（走公共出口 `ReportExport.SaveAsync`）；
  **列名取 `_logger?.Config.VariableIds`**（采样时的顺序，不是当前画笔 —— 后者会让列与值错位）；
  顺带把"画笔 → 变量 Id 顺序"抽成 `EnabledVariableIds()`，开记录与导报表**共用一处**。
- **判据**：Core `HistoryReportTests` 6 条 ＋ App `HistoryReportExportGuardTests` 3 条
  （含"整张表不出现 `\"0\"`"的反向断言、截断保留最近 ＋ 不封顶正控、接线契约）。
- **负控 4 支**（`.workbuddy/tmp/s175-nc.py`，逐支还原 SHA256 逐字节相同）：缺值补 0 · 数值用当前文化 ·
  截断保留最早的 · 时间列名回落成键名。
- **顺带**：按钮效果普查表第三次撞上（声明 `Never`）；🩸 **踩坑**：第一次拿 `Never("BtnTitleClose"` 当锚点，
  它在 **5 个宿主**里都有 ⇒ 声明插进了 `SettingsDialog`（那个宿主立刻报"声明了不存在的按钮"）——
  教训写进登记：普查表的锚点必须**先定位宿主段**。`UnusedBaseline` **1098 → 1099**。
  **新登记 `#86`**：`DataLogger.ExportCsv()` 产品侧零调用（设备侧 CSV 是生成固件里另一条路）⇒ 只登记三种归宿。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **683**（＋2）；步 5 逐程序集 `44/44/4/96/78/315/2168/682/8`（合计 **3439**）
＋ 步 6 独占 `8` ＝ **3439 通过 / 0 失败**（阶段 174 为 3430，**+9**）；全量 App.UiTests **682 / 0**（连跑 2 次）。

---

## 阶段 176（2026-10-01）—— 把 `DataLogger.ExportCsv()` **接活**（用户选「接」）

**一句话**：先量后接 —— 量出两件决定性的事，接的位置因此和登记设想的**不一样**：
① 设备侧时间基是 `lv_tick_get()`（**开机时长**，`CodeGenerator.Trend.cs:92`）⇒ 编辑器做不出逐字镜像
⇒ 表头写 `timestamp_utc_ms`（写明口径、不冒充 `timestamp_ms`），**列序**与设备一致；
② `DataLogConfig.CsvDelimiter` **只被那个死方法用**，而生成的设备 CSV 把它**硬编码成逗号**
（实测：配置 `;` 也照写 `,`）⇒ 那才是真正「该接」的线，**顺手修掉一处真缺陷**。

- **接法**：① 设备 CSV 改用配置分隔符（表头/变量列/`%.3f` 行；空则回落逗号）；
  ② `ExportCsv()` 重写成设备列序（`timestamp_utc_ms` ＋ 变量 Id、`F3`、行尾 `\n`）＋
  新增 `ExportCsvBytes()`（UTF-8 BOM；**空数据仍是空数组**）；
  ③ 进产品：`ReportTable.CsvOverride` ＋ `ReportExport` 的 CSV 分支**优先用它**（其余三格式仍走报表列）＋
  趋势面板传 `_logger?.ExportCsvBytes()` —— 这就是「**让编辑器报表走它**」。
- **判据**：Core `DataLoggerCsvTests` 5 条（含**用固定时刻核对 UTC epoch 毫秒**）＋ CodeGen 分隔符 3 档
  （非逗号时**反向断言** ＋ 前提自检）＋ App「CSV 覆盖接缝」1 条（**正控**：不带覆盖仍是通用 CSV；
  xlsx 不受覆盖影响）＋ 接线契约。
- **负控 4 支**（`.workbuddy/tmp/s176-nc.py`，逐支还原 SHA256 逐字节相同）：设备 CSV 退回硬编码逗号 ·
  导出器忽略覆盖 · 表头冒充 `timestamp_ms` · 空数据也吐 BOM。
- 🩸 **门禁第一次跑红了两条**（既有 `DataLoggerTests` 断言旧表头 `Timestamp` ＋ 断言风格守卫）——
  原因是我只跑了 Debug 的**过滤子集**没跑全量 Core ⇒ 两条都按守卫与新契约修好，教训写进专题 §12.4。
- **`#86` 闭环**（处置＝接）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **683**；步 5 逐程序集 `44/44/4/96/78/2176/315/683/8`（合计 **3448**）
＝ **3448 通过 / 0 失败**（阶段 175 为 3439，**+9**）；全量 App.UiTests **683 / 0**（连跑 2 次）。
专题 ⇒ `docs/2026-10-01-reporting-capability-mvp.md` §12。

---

## 阶段 177（2026-10-01）—— 报表第四片：**列选择**（导出前勾列，三面板共用一处）

**一句话**：前三片只有"全列导出"；本片加"导出前选列"，并把这件事也收成一处 ——
Core 给纯投影、App 给纯接缝 ＋ 一个对话框、三个面板共用同一个入口。

- **Core `ReportTable.WithColumns(keys)`**：列按调用方顺序保留、行跟着投影；**未知列键忽略**（旧配置的
  列名不该把用户挡在门外）；重复键只取一次；**一个都不剩 ⇒ 抛**；`CsvOverride` **原样带过**。
- **App**：`NeedsColumnChoice`（只有一列就不弹）＋ `ApplyColumnSelection`（纯接缝）＋
  `ChooseColumnsAsync`（UI 入口；无宿主视作取消）；新对话框 `ReportColumnDialog`（默认全选、
  全选/全不选、**零勾选时确定不可点**、结果只回列键、顺序沿用原表）；三面板导出前都过它，**取消即中止**。
- **判据**：Core 5 条 ＋ App 5 条（含**三面板接线契约**与对话框文案双语）。
- **负控 4 支**（`.workbuddy/tmp/s177-nc.py`，逐支还原 SHA256 逐字节相同）：列序被忽略 · 接缝不投影 ·
  报警面板漏跳 · `NeedsColumnChoice` 恒 true。
- **守卫教的**：新窗口自动进两道普查面 —— `WindowViewSmokeUiTests` 指出外壳不符 `ChromeWindow` 契约
  （要求 Content 是 Border、其 Child 是 Grid；不符时 `InjectResizeGrips` 静默 return、窗口拖不动边）
  ⇒ 按既有外壳重写；生产路径表加一条；门禁又抓 `CS0108`（`ChromeWindow` 本身就有标题栏那两个处理器，
  我重复声明了）⇒ 删掉。`UnusedBaseline` **1099 → 1102**（同一形状第四次）。
- **🩸 负控脚本三条坑**（本阶段真踩到）：基线快照要**开跑前取一次**（每支现取会把注入当基线 ⇒ 越跑越脏，
  工作树真留下过注入）· 每支要 `try/finally` 还原（脚本自己抛 `KeyError` 时还原走不到）·
  **不要接 `head`**（SIGPIPE 杀在还原之前）—— 另：注入必须**保持可编译**。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **685**；步 5 逐程序集 `44/44/4/96/78/315/2181/690/8`（合计 **3460**）
＝ **3460 通过 / 0 失败**（阶段 176 为 3448，**+12**）；全量 App.UiTests **690 / 0**（连跑 2 次）。
专题 ⇒ `docs/2026-10-01-reporting-capability-mvp.md` §13。

---

## 阶段 178（2026-10-01）—— 报表第五片：**时间范围**（闭区间、UTC、先过滤再封顶）

**一句话**：列选完了，这一片管"哪些行"——五个快捷档（全部/最近 1 小时/24 小时/7 天/30 天），
在三个数据源**各自的时间字段**上过滤；顺序上：**选范围 → 选列 → 保存**。

- **Core `ReportTimeRange`**：两个可空端点的**闭区间**、一律 **UTC**、`from > to` **抛**；
  `ReportRangeOptions.Compute(choice, now)` 是**纯映射**（now 由调用方传 ⇒ 判据用固定时刻核对）。
- **三个数据源**：`AuditLogReport`（`Timestamp`）/`AlarmReport`（`OccurredAt`）/`HistoryReport`（`Timestamp`）
  各加可空 `range`；**历史数据先按范围过滤、再封顶**（反过来用户选 7 天却只看到"最近 N 条里的那些"且不报错）。
- **App**：`ReportRangeDialog`（五档单选 ＋ 确定/取消，结果只回**档位**）；
  `ReportExport.ChooseRangeAsync`；三个面板都先问范围再选列。
- **判据**：Core 5 条 ＋ App 4 条（含**范围真的传给了各自数据源**的接线契约）。
- **负控 4 支**（`.workbuddy/tmp/s178-nc.py`，逐支还原 SHA256 逐字节相同）：开区间 · 跨度算错 ·
  先封顶再过滤 · 面板丢掉范围。
- **🩸 负控证明判据太弱**：nc3 第一次**没红** —— 我给"先过滤再封顶"挑的场景是"范围只覆盖**最早**那几条"，
  两种顺序结果完全相同 ⇒ 判据恒绿。换成"范围覆盖**超出前 maxRows 条之外**的数据"（最近 5 条里取最近 3 条）
  立刻可区分。教训：**判据的场景必须让两种实现产出不同读数**。
- **守卫逼出的更好设计**：`WindowProductionPathUiTests` 报"生产构造与设计器构造字段形状完全一致"
  —— 我第一版把档位按钮放进 `static` 字典（实例间共享状态）⇒ 改成**实例字段**（0 → 5 条、可观测地不同）。
  `UnusedBaseline` **1102 → 1109**（同一形状第五次）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **689**（＋4）；步 5 逐程序集 `44/44/4/78/96/315/2186/696/8`（合计 **3471**）
＝ **3471 通过 / 0 失败**（阶段 177 为 3460，**+11**）；全量 App.UiTests **696 / 0**（连跑 2 次）。

---

## 阶段 179（2026-10-01）—— 报表第六片：**报表包导出**（一个数据源 × 四格式 ＋ `index.txt`）

**一句话**：前面都是"一份一份地导"，这一片管"同一批数据一次出全"（留档/交付）——
每份报表面板加一个「导出报表包」，一次写出 CSV/Excel/PDF/HTML 并附索引。

- **Core `ReportBundle`**：文件名 `<kind>-<yyyyMMdd-HHmmss><ext>`（非法字符与空白清成 `-`、全非法回落 `report`、
  中文保留）＋ 索引文本（`file\trows` 逐行、末尾 `skipped` 段、行尾 `\n`）—— 时间由调用方给 ⇒ 确定可判。
- **App `ReportExport.WriteBundle`**（纯 IO ＋ 纯渲染，不弹选择器）：每个扩展名走同一套 `Render`；
  **渲染不出来的格式记进 `Skipped` 而不是静默丢**（典型：缺字体 ⇒ PDF 跳过）；最后写 `index.txt`。
  `SaveBundleAsync` = 目录选择器 ＋ `WriteBundle` ＋ 一句话；出错交 `UiOperationGuard`。
- **三个面板**：各加按钮；与「导出报表」分工明确 —— 那条先问**范围/列**（精细挑一份），
  这条**全列、全时段、四格式**（留档/交付），不弹对话框。
- **判据**：Core 3 条 ＋ App 4 条（缺字体时"三种照写 ＋ PDF 记进索引 ＋ **反向断言没有 PDF 文件**"·
  有字体时四格式齐全且两次打包**逐字节相同**· 键双语· **三面板接线契约**）。
- **负控 4 支**（`.workbuddy/tmp/s179-nc.py`，逐支还原 SHA256 逐字节相同）：文件名不清洗 ·
  索引不写 skipped · 缺字体静默跳过 · 报警面板漏接线。
- **🩸 本阶段踩的坑（已登记）**：我用 heredoc 脚本按"缩进字符串"改 C#，把三处 `OnExportReport`
  **劈成两半**（一次还留下重复方法头），且因 `dotnet build -v q` 只看尾行而误以为构建干净 ⇒
  改成**先 read 看清上下文、再用 edit 工具精确替换**，并记规矩：**改完立刻看编译/测试退出码，不要只看输出尾行**。
- `UnusedBaseline` **1109 → 1110**（同一形状第六次）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **690**；步 5 逐程序集 `44/44/4/96/78/2189/315/700/8`（合计 **3478**）
＝ **3478 通过 / 0 失败**（阶段 178 为 3471，**+7**）；全量 App.UiTests **700 / 0**（连跑 2 次）。

## 阶段 180（2026-10-01）—— 报表第五片的补齐：**自定义起止日期**

**一句话**：阶段 178 的快捷档只覆盖"最近 N"，这一片让用户能挑**两个具体日期**（要"上个月那一段"的
真实需求），并把"Core 给键、App 推键"补上 —— 顺带把五个死常量救活。

- **口径四处**：① 右端是**整天**（结束日最后一刻 ＝ 次日 00:00 减 1 tick）—— 当成 00:00 会静默给空表；
  ② **偏移留住**（日期选择器给的是带偏移时刻，"10-01"在东八区与 UTC 差 8 小时）；
  ③ 两端可空 ＝ 该侧不设限，**两端都空 ⇒ Core 抛 ＋ UI 确定键不可点**（双保险）；
  ④ 旧的 `Compute(choice, now)` 遇 `Custom` **抛**（回落"全部"是最坏形状）。
- **对话框刻意不读时钟**：不预填默认日期 —— 预填就要在这里读一次 `Now`，于是"当前时刻"有了第二个来源
  （阶段 178 定的口径是范围只由调用方用自己的 now 算）。代价是"空自定义"可达 ⇒ 用按钮禁用挡住。
- **`ReportRangeText`**（Core 给键、App 推键，同 `AuditReportText`）：六个档位 ＋ 从/到标签都由 `Lang` 常量供给
  ⇒ `UnusedBaseline` **1110 → 1105**（本集**第一次降**；标题/提示仍走 `{res:Loc}`，因为那里要语言切换能刷新）。
- **判据**：Core `ReportRangeOptionsTests` 8 条（整天口径 · 偏移留住 · 单侧 · 两种抛 · 档位重载拒绝 Custom ·
  每档可算）＋ App 新增 2 条（映射与 Core 键逐档相等 · 对话框生产路径四条腿）。
- **负控 6 支**（`.workbuddy/tmp/s180-nc.py`，逐支还原 SHA256 逐字节相同）：右端丢整天 · 左端丢偏移 ·
  档位重载不拒绝 Custom · App 映射串档 · 确定键不挡空自定义 · `Selected()` 不带出日期。
- **🩸 又踩了同一条坑**：`throw new ArgumentException("…（要"全部"就选那一档）")` 里嵌的 ASCII 双引号
  把字符串截断（Core `CS1003`），而我第一次 `dotnet build -v q` 只看尾行 ⇒ 差点又误判"构建干净"。
  处置：改成 `【全部】`，并重申**改完立刻看退出码**。
- **新登记 `#87`**：定时/自动导出（设置项 ＋ 可注入时钟 ＋"到期只跑一次"幂等判据，天生易 flaky ⇒ 单独设计）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **691**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 315 / 2197 / 702 / 8`（合计 **3488**）
＝ **3488 通过 / 0 失败**（阶段 179 为 3478，**+10**：Core +8、App +2）；
全量 App.UiTests **702 / 0**（连跑 2 次，39 / 32 s）；`UnusedBaseline` **1110 → 1105**（本集第一次降）。

## 阶段 181（2026-10-01）—— 报表**设计器**（定义批 ＋ 模板内核）：模板是**覆盖层**，不是白名单

**一句话**：用户拍板做报表设计器；本阶段先量清楚"能设计什么"（只有勾子集，顺序/列名/隐藏都不保存），
定下"模板＝覆盖层"的口径，并落地可判的那一半（Core 纯逻辑）。

- **现状读数**：缺口四件 —— **顺序 / 列名 / 隐藏 / 持久化**（前三件纯逻辑，第四件是存储）。
  历史报表的列是**动态的**（变量 Id）⇒ 任何存下来的列清单必须容忍列的新增/删除。
- **选型取舍**：列布局模板（做）· 排序/分组/小计（不做：要动行模型与数值口径）·
  表达式列（不做：要表达式引擎→列→类型推断四件一起动）；模板引擎仍不做（阶段 172 的结论继续成立）。
- **口径（本阶段最重要的一条）**：模板是**覆盖层**而不是白名单 —— 否则"用户以后新加一支画笔"
  会在报表里**永远静默消失**。三条意图：顺序（列到的在前、**没列到的按原序接在后面**）· 改名（列键永不变）·
  显式隐藏。配套：`null`/空模板恒等 · 未知键忽略 · 重复键第一条生效 · **全隐藏抛** · `CsvOverride` 不套模板。
- **落地**：`Core/Reporting/ReportTemplate.cs`（`ReportTemplateEntry` ＋ `ReportTemplate`）＋
  `ReportTable.ApplyTemplate(...)`（纯函数，与 `WithColumns` 并列）。App 侧留 182/183 两片。
- **判据**：Core `ReportTemplateTests` **10 条**（含"没列到的接在后面不丢"的正控与"套用不改原表"）＋
  **负控 4 支**（白名单 · 忽略隐藏 · 改名连列键一起改 · 空模板不恒等）。
- **🩸 自查抓到的判据笔误**：第一版把"只改名不重排"写进期望，而模板列的是"顺序＋改名"一件事 ⇒
  期望值当场红，改成 `[b,a,c]`。教训：**期望值也要按已定口径推一遍，别凭直觉写**。
- **待你拍板（182 第一件事）**：模板存**应用级设置**还是**工程文件**？（证据对比见
  `docs/2026-10-01-report-designer.md` §4：应用级有面板布局的先例 `#80`、零接线改动；
  工程文件强类型但要动 schema，而模板描述的是运行数据的视图。）

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **692**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2207 / 315 / 702 / 8`（合计 **3498**）
＝ **3498 通过 / 0 失败**（阶段 180 为 3488，**+10**）；全量 App.UiTests **702 / 0**（连跑 2 次，35 / 37 s）；
`UnusedBaseline` **不变 1105**（本阶段没加 Lang 常量）。

## 阶段 182（2026-10-01）—— 报表模板的**持久化**（用户拍板：应用级设置）

**一句话**：模板内核（181）之后把"存哪儿"拍板落地 —— 选**应用级设置**（同面板布局 `#80` 同族：
它是操作者偏好，换工程后仍生效，且三面板零接线改动）；顺手补上"结构化偏好"那条一直缺的读取路径。

- **Core `ReportSourceKeys`**：数据源稳定标识（`audit`/`alarm`/`history` ＋ `All`/`IsKnown`）——
  报表包文件名、模板存储键、界面/判据列举**共用同一批字符串**（三处传错都不会报错，只能靠判据）。
- **App `SettingsService.Coerce<T>`**：新增"复杂类型按目标类型反序列化"的回落 —— `Materialize` 对嵌套对象
  刻意保留 `JsonElement`，原先几支只认基元 ⇒ 报表模板这种形状的值会**存得进去、重启就没了**
  （同 `Materialize` 注释里记的那次同族，只是这次落在"结构化偏好"上）。
- **App `ReportTemplateSettings`**：`Get`/`Set`/`Clear`/`Configured`；空模板**不落盘**（＝删除）；
  未知数据源键忽略；脏形状退回默认（不打穿启动）。三面板的 bundle `kind` 改用常量。
- **判据**：Core **7 条**（往返 · 基元与结构化同存的正控 · 空模板＝删除 · 未知源 · 脏形状 · 键稳定）＋
  App **3 条**（**每个面板只用自己那个键** · 键真的传进 `SaveBundleAsync` · 键与面板表对齐）＋ **负控 4 支**。
- **🩸 判据太弱的第二例**：「空模板＝删除」第一版只断言 `Get` 返回 `null` —— 而"没有这一项"与
  "存了个空数组"在读回时被同等对待，注入"空模板照存"照样绿 ⇒ 改成**核对偏好文件的形状**才判得出。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **693**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 315 / 2214 / 705 / 8`（合计 **3508**）
＝ **3508 通过 / 0 失败**（阶段 181 为 3498，**+10**）；全量 App.UiTests **705 / 0**（连跑 2 次，36 / 43 s）；
`UnusedBaseline` **不变 1105**（本阶段没加 Lang 常量）。

## 阶段 183（2026-10-01）—— 报表**设计器**（窗口 ＋ 接线 ＋ 套用点）

**一句话**：模板从"能存"变成"能用" —— 新增设计器对话框（勾选/改名/↑↓/恢复默认），三个面板各加入口，
「导出报表」建表后自动套模板；报表包**刻意不套**（留档＝全列全时段）。

- **🩸 本片最关键的一处**：设计器**不能**拿"导出视图"（`ApplyTemplate`）当输入 —— 隐藏的列已经不在里面，
  用户隐藏一列之后**再也勾不回来**。新增 `ReportTemplate.Spread`（设计器视图：**隐藏列留在原位**、
  勾回来回到原列名）与之配套，两条判据（Core ＋ App）＋ 负控 nc6 钉住。
- **列清单只有一个来源**：三个数据源各加 `XxxReport.Columns(...)`，`From(...)` 复用它；
  `ReportColumnUniverseTests` **5 条**逐源对拍（漂移不报错，只能靠判据）。
- **界面两条口径**：全不勾 ⇒ 确定不可点（模板全隐藏会让导出抛）；一键未动 ⇒ 交回**空模板**
  （＝删除，磁盘上不留空壳）。
- **套用点正好一处且只在「导出报表」**：模板改过的列名会带进列选择对话框（用户仍可临时取消几列）；
  报表包不套（判据 `TemplateIsAppliedOnlyOnTheReportPath` 要求每个面板里 `ApplyTemplate` 恰好出现 1 次）。
- **判据**：Core ＋10（列清单 5 ＋ `Spread` 5）· App ＋10（设计器 8 ＋ 自动收面新增窗口 2）· 守卫表三处随之更新；
  **负控 7 支**逐支按预期红。
- **🩸 记账时差点自己骗自己（`#48` 第三次现身）**：新窗口的"键在字典里"判据第一版用 `Lang.ReportDesigner*`
  **常量名**，把四个死常量"写活"⇒ `UnusedBaseline` 恰好不变、差集判据失去意义。改成**字面量键**后如实涨 4。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **695**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2224 / 315 / 715 / 8`（合计 **3528**）
＝ **3528 通过 / 0 失败**（阶段 182 为 3508，**+20**：Core +10、App +10，后者含**自动收面新增窗口 2**）；
全量 App.UiTests **715 / 0**（连跑 2 次，33 / 39 s）；`UnusedBaseline` **1105 → 1109**。

## 阶段 184（2026-10-01）—— **定时/自动导出**（`#87`）：定义批 ＋ 排程内核

**一句话**：报表"怎么导"已经做完（181~183），缺的是"谁按点触发"。本阶段先量清楚、定下触发形态，
并落地可判的那一半（不碰线程与计时器的 Core 排程内核）。

- **读数发现的真坎**：审计与报警的数据源在组合根（`MainWindow._authService` / `_alarmEngine`），
  而**历史数据（`DataLogger`）在 `TrendConfigView.StartRecording()` 里 `new`、随对话框生死** ——
  应用层没有持有者 ⇒ 定时导出"全源"卡在这（处置落在 185 的数据源注册表）。
- **触发形态定案**：**应用运行期内固定节拍**（另两个候选：退出时导一次 / 外部计划任务，
  取舍见专题 §2）。明确不做常驻守护与跨机器归档。
- **Core `AutoExportSchedule`**（纯函数，`now` 由调用方给）：起跑不立刻导（第一次＝锚点 ＋ 一间隔）·
  到期点算在内 · **错过的一律不补**（`NextDue` 跳到未来点；**刻意不提供"错过了几个点"的 API**
  ⇒ 调用方写不出补跑循环）· 时钟回拨不到期 · 间隔 ≤ 0 ＝ 关闭 · 极远跳变饱和不抛。
- **判据**：Core **10 条**（含结构判据与稳态连跑）＋ **负控 4 支**逐支按预期红。
- **如实标注**：内核用 tick 整除而非浮点分钟算步数 —— 这一条**没有行为判据**（浮点版在可达范围内
  几乎总是同结果），属"靠实现选择保证、靠注释讲清楚"的一类。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **696**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2234 / 315 / 715 / 8`（合计 **3538**）
＝ **3538 通过 / 0 失败**（阶段 183 为 3528，**+10**）；全量 App.UiTests **715 / 0**（连跑 2 次，34 / 35 s）；
`UnusedBaseline` **不变 1109**（本阶段没加 Lang 常量）。

## 阶段 185（2026-10-01）—— **运行期数据源注册表**（`#87` 第二片）

**一句话**：把阶段 184 量到的那道坎处置掉 —— 历史数据（`DataLogger`）原本随趋势对话框生死、
应用层拿不到；现在三个面板/服务把"取一张表"的委托注册进 `App.ReportSources`，定时导出只认这张表。

- **`ReportSourceRegistry`**：`Register/Unregister/Clear` ＋ `Registered/Missing`（顺序按声明）＋
  `TryBuild`（没注册 ⇒ `null`，**正常状态不是错误**；委托**锁外**调用：取数可能上千行）；
  **未知键当场抛**（键会一路变成报表包文件名与偏好键）。
- **接线**：`MainWindow` 生产构造注册审计/报警、关闭时注销；`TrendConfigView.StartRecording()`
  注册历史、`OnClosed` **在释放 `_logger` 之前**注销（顺序反了会偶发拿到已释放的 logger）。
- **两条口径**：**注销 ≠ 停记录**（停记录后缓冲还在、仍可导出）· **取数构造只有一处**
  （报表包与注册表共用 `BuildHistoryTable()` —— 两处各写一份会让定时导的列与手动导的列漂移，且不报错）。
- **判据**：Core **8 条**（含"委托每次现取"与并发注册/取数不抛）＋ App **5 条**（含**行为面**：
  生产主窗口注册的委托真的取得到表）＋ **负控 5 支**。
- **诚实边界**：历史那条只有源码级判据（驱动"开记录"要点 `BtnToggleRecord`，而它在
  `HostButtonEffectUiTests` 里被声明为 `Never` —— 会启动真的采样定时器 ⇒ 读数会抖）。
- **🩸 本阶段门禁第一次红**：并发判据里的 `writer.Wait()` 触发 `warning xUnit1031`（阻塞式等待），
  而门禁"警告即失败" ⇒ `GATE_EXIT=1`（步 3 就停）。修法：`async Task` ＋ `await`。
  重申规矩：`dotnet test` 的**尾行**看不见构建段警告 —— 改完先看 `: warning `，别只看尾行。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **698**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2242 / 315 / 720 / 8`（合计 **3551**）
＝ **3551 通过 / 0 失败**（阶段 184 为 3538，**+13**）；全量 App.UiTests **720 / 0**（连跑 2 次，33 / 35 s）；
`MainWindow.axaml.cs` **仍停在 2983**（第一次门禁红就是它涨到 2997 ⇒ 把接线搬进 `ReportSourceWiring`，
调用点各留一行）；`UnusedBaseline` 不变（本阶段没加 Lang 常量）。

## 阶段 186（2026-10-01）—— **定时/自动导出服务**（`#87` 收口片）

**一句话**：把"到点没到点"与"写哪些源、写到哪"接起来 —— 机制全部就位，只剩设置界面（187）。

- **`AutoExportService`**（依赖全是回调 ⇒ 判据不睡觉、不碰真磁盘、不碰线程）三条口径：
  **每到期点只跑一次**（先消费到期点，失败不重跑这一拍）· **没源不写空包** · **写盘异常不打断节拍**。
  结论结构化，文案由组合根按语言拼；`NotDue` 刻意不发声（每 30 秒往状态栏写一句是骚扰）。
- **设置三项**：`Enabled`（默认 **false**）· `IntervalMinutes`（默认 30）· `Directory`（默认空）——
  默认刻意"不干活"；目录空时**不弹选择器**；关掉或间隔 ≤ 0 ⇒ 排程连"到点"都不存在。
- **`WriteMultiBundle`**：多源 × 四格式 ＋ 索引；**单源的 `WriteBundle` 只是它的一个元素**
  （产物不可能分叉）；渲染失败**带文件名**、**缺的源进 skipped**（"这次少导了什么"在产物里看得见）。
- **接线**：`AutoExportWiring` ＋ `DispatcherTimer`，由 `ReportSourceWiring` 在**注册之后** Start、
  **注销之前** Stop（先停表再撤源）。
- **判据**：Core **10 条** ＋ App **7 条**（含**端到端**：假时钟 ＋ 真写盘 ⇒ 目录里真有文件、索引里真有缺源）＋
  **负控 7 支**。
- **诚实边界**：**设置界面还没做**（187）⇒ 现在启用只能手改 `settings.json`；应用没运行就不会导；目录固定不轮转。
- **🩸 门禁抓到的真缺陷**：`index.txt` 里"缺的源"那行我第一版用**索引器**读带 `{0}` 的模板 ⇒
  用户会看到字面量 `{0}`。`LocalizedFormatCallSiteTests` 当场点名；处置是把"模板字符串"改成**函数**
  （`Func<string,string>`，由调用方 `Format` 出整句），并加负控 nc8 把索引器那版注回去验证守卫会红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 727 / 8`（合计 **3568**）
＝ **3568 通过 / 0 失败**（阶段 185 为 3551，**+17**）；全量 App.UiTests **727 / 0**（连跑 2 次，35 / 34 s）；
`UnusedBaseline` 不变 1109；`MainWindow.axaml.cs` 仍停在 2983。

## 阶段 187（2026-10-01）—— 自动导出的**设置面**（`#87` 闭环 ＋ 新登记 `#88`）

**一句话**：`#87` 从阶段 180 起挂了三轮，这一片把"用户能用"补上 —— `SettingsDialog` 加「自动导出」一面；
顺带量到一条**既有测试面为零**的接线族并如实登记。

- **设置面**：`ChkAutoExport`（开关）· `NumAutoExportInterval`（1…1440 分钟）· `TxtAutoExportDirectory` ＋
  目录选择器；面板里印着**诚实边界**（只在编辑器运行期间工作；不可用的源如实进 `index.txt`）。
- **🩸 量到一件事**：程序化写 `TextBox.Text` **不触发** `TextChanged`（探针实测；既有 `TxtLvglDir` 同样）⇒
  判据要按框架的做法补发 `TextChangedEvent`（同 ToggleButton 那条纪律），否则驱动的不是生产路径。
- **新登记 `#88`**：`SettingsDialog` 里 `TxtLvglDir` / `TxtT113SdkRoot` / `TxtCanvasBgColor` / `TxtAccentColor`
  这一族 TextBox→设置的接线**从来没有判据驱动过**（真实输入下工作，测试面为零）⇒ 要么逐条补，
  要么收成一个"驱动器"（双向差集）。本阶段只给自己新加的那一枚补了判据，**没有**假装盖住那一族。
- **`#87` 闭环**，诚实边界四条写进专题 §9.4：应用没运行就不会导 · 目录固定不轮转 ·
  历史只有正在记录时才有 · 导的是当前数据不是增量。
- **判据**：App **4 条**（双向接线 ＋ XAML/源码契约 ＋ 键双语）＋ `HostButtonEffectUiTests` 宿主扩容 ＋
  **负控 5 支**。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **943**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 731 / 8`（合计 **3572**）
＝ **3572 通过 / 0 失败**（阶段 186 为 3568，**+4**）；全量 App.UiTests **731 / 0**（连跑 2 次，37 / 37 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）。

## 阶段 188（2026-10-01）—— `SettingsDialog` 的 **TextBox 驱动面**（`#88` 闭环）

**一句话**：阶段 187 自己量到并登记的缺口（程序化写 `TextBox.Text` 不触发 `TextChanged` ⇒
"输入框→设置"那一族接线从没被判据驱动过），这一片照本仓"一份面 ＋ 双向差集"收成一个驱动器。

- **驱动口径**：写文本后**按框架的做法补发 `TextChangedEvent`**，再**读设置**断言
  —— 与 `HostButtonEffectUiTests` 里"ToggleButton 先翻转 `IsChecked` 再引发 `Click`"同一条纪律。
  补发不会让判据恒真：接线缺失时补发没有人接（负控 nc1 证明）。
- **面**：7 个框 / 9 条手势 —— 强调色三条（合法写 · **非法不动** · **留空恢复默认**）、
  画布底色两条（合法写 · 非法不动）、四个目录（写 ＋ **裁剪**，其中自动导出目录**刻意不裁剪**）。
  每条手势先切到所在分类面（面板没显示 ⇒ 当场红）。
- **双向差集**：XAML 里每个具名 `TextBox`、代码里每条 `TextChanged` 接线都必须在表里（两个方向都判）。
- **判据**：App 3 条 ＋ **负控 5 支**（摘接线 · 去 `Trim` · 十六进制不校验 · 去"留空恢复默认" ·
  XAML 新增没人驱动的框）。
- **未覆盖（如实留开）**：其它视图的同类接线；推广成跨视图的一份面是后续候选。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 734 / 8`（合计 **3575**）
＝ **3575 通过 / 0 失败**（阶段 187 为 3572，**+3**）；全量 App.UiTests **734 / 0**（连跑 2 次，37 / 33 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）。

## 阶段 189（2026-10-01）—— TextBox 接线**普查面**（`#88` 那一族推广成跨视图的账）

**一句话**：阶段 188 只驱动了 `SettingsDialog` 那 7 条；这一片把"还剩多少同类接线没人管"量清楚，
并把"新增一条没人管的接线"变成红。

- **实测**：`Views/` 下 **153 个具名 `TextBox`**，其中 **57 条**接了 `TextChanged` ——
  **面 A**（本文件具名框 ∩ 接线，按名可驱动）**11 文件 / 48 条**；**面 B**（接了线但按名找不到，
  全是代码里 `new` 出来的）**4 文件 / 9 条**。其余 96 个没接线**不是缺口**（走"点保存"一次性读取）。
- **判据** `AppTextBoxWiringCensusTests` 4 条：面 A 双向差集 · 面 B 双向差集 ·
  **"已驱动"不许是空话**（只许一个 `driven`，且驱动判据必须覆盖该文件每个具名框）·
  **每条接线恰好进一个面**（`A+B == 接线总数`）。
- 🩸 第 4 条第一版写错：断言两个面的**文件集**不相交 —— 被 `ProjectSettingsView`（2 条具名 ＋ 1 条按名找不到）
  当场红。**"不重叠"是接线级的性质，不是文件级的**。
- **负控 4 支**：表外新增接线 · 表内摘一条接线 · 新增代码创建的输入框接线 · 驱动判据把一个框名写歪。
- **仍未做**：面 A 其余 41 条、面 B 9 条逐条驱动（配方见专题 §6.3：建宿主窗口 → 每条手势给
  "从模型读值"的委托 → 写文本 ＋ **补发 `TextChangedEvent`** → 状态改 `driven` 并指向判据类）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 315 / 2252 / 738 / 8`（合计 **3579**）
＝ **3579 通过 / 0 失败**（阶段 188 为 3575，**+4**）；全量 App.UiTests **738 / 0**（连跑 2 次，37 / 36 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）。

## 阶段 190（2026-10-01）—— 面 A 的三个「全框接线」视图**升级成 driven**

**一句话**：阶段 189 把这一族推广成一张账（11 文件 / 48 条，只有 `SettingsDialog` 是 `driven`）；
这一片把其中**三个「每个具名框都接了线」**的视图（`AlarmConfigView` 7 框 / `AnimationEditorView` 9 框 /
`AuditLogView` 1 框）真正驱动起来 —— 共 **6 条判据 / 26 条手势**，**0 行产品代码改动**。

- **先答口径再挑文件**：第 3 条判据要求"驱动表覆盖该文件的**每一个**具名框"（比"覆盖每条接线"更严）。
  全仓只有 4 个文件满足「具名框全部接了线」（＋阶段 188 的 `SettingsDialog`）⇒ 升级这三个**不需要放宽判据**；
  其余 7 个文件都有"没接线的具名框"（`PropertyPanelView` 21 框只接 1），它们的口径本阶段**刻意不定**。
- **共享原语** `TextBoxWiringFace`（`Find` / `Write` ＝ 写文本 ＋ **补发 `TextChangedEvent`** /
  `AssertFullyDriven` ＝ 单文件双向差集 ＋ 两个扫描原语）；**普查面与 `SettingsTextBoxWiringTests` 同步改用它**
  ⇒ 扫描正则只剩一处实现。
- **三个驱动面**（都走生产构造 ＋ 每条手势前有前提自检）：报警 10 条手势（名称/描述**不裁剪** ·
  解析拒绝**不动** · 位偏移**钳到 0…63**）· 动画 12 条（描述 **`Trim`** · 时长**下界 0** ·
  关键帧时间**钳到 0…100**）· 审计 4 条（过滤**裁剪** · 空集 · **清空回到全量**）。
- 🩸 **量到一件事**：阶段 120 的 `AlarmConfigFieldEditUiTests` 是靠 `ChkEnabled.IsChecked = false`
  那一跳把 `OnAlarmFieldChanged` 叫起来的（复选框程序化赋值**会**触发 `IsCheckedChanged`，
  `TextBox.Text` 不会）⇒ 逐个 TextBox 的 7 条接线**仍然零驱动**：单独摘掉 `TxtThreshold.TextChanged`，
  既有判据不会红（负控 nc1 实测）。
- **普查面第 3 条推广**成 `EveryDrivenFile_HasADriverTestThatCoversItsTextBoxes`：状态只许
  `driven`/`census`（防拼错被静默跳过）· 每个 `driven` 行必须给出**存在**的驱动判据文件且覆盖其每个具名框 ·
  `census` 行**不许**写驱动判据。
- **负控 5 支**：摘报警表内接线 · 摘动画表内接线 · 摘审计唯一那条接线 · **保留接线**只去掉 `Trim()`
  （打行为、不打"在不在"）· `driven` 行指向不存在的判据文件。
- **仍未做**：面 A 剩 **7 文件 / 24 条**（🩸 阶段 191 更正：原写 31 条是手抄错，逐项 `4+1+2+1+6+3+7 = 24`）、
  面 B **9 条**；面 A 剩下这些文件**都有"没接线的具名框"** ⇒ 先定"没接线的框算不算已驱动"的口径，
  再按专题 §6.3 配方逐个扩（**该口径阶段 191 已定**：见下）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 744 / 8`（合计 **3585**）
＝ **3585 通过 / 0 失败**（阶段 189 为 3579，**+6**）；全量 App.UiTests **744 / 0**（连跑 2 次，37 / 36 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；`MainWindow.axaml.cs` 仍停在 **2983**（本阶段 0 行产品代码改动）。

## 阶段 191（2026-10-01）—— 面 A 的**三态分账** ＋ `SimulatorView` 升级 ＋ 真缺陷 `#89`

**一句话**：阶段 190 结尾留了"面 A 剩下的文件都有没接线的具名框 ⇒ 先定口径"；这一阶段把口径定成两条
（**没接线要显式登记**、**驱动不了要标 `blocked`**），顺着它把 `SimulatorView` 升级成 `driven`
（＋7 条），并**量到一条真缺陷**（脚本编辑器的字段编辑不落模型，登记 `#89`）。

- **口径一 `withoutWiring`**：`AssertFullyDriven` 加一张**登记表**（名字 → 理由）—— 每个具名框要么在
  驱动面里、要么在登记表里（**"既没驱动也没登记"当场红**）；登记表自己双向判（名字写歪 ⇒ 红 ·
  登记了但其实接了线 ⇒ 红 · 两处都登记 ⇒ 红）。既不硬塞（假账），也不丢账。
- **`SimulatorView`（7 条接线 ＋ 2 个没接线的框）→ driven**：6 条数值写 ＋ 1 条**解析拒绝**（`abc` 不动）＋
  `ManualValueInput` 一条（**前提先量出来**：走生产处理器把信号切到 `Manual`，并断言信号位与 `IsEnabled`）；
  `RegAddressInput`/`RegValueInput` 进登记表（"读/写寄存器"按钮点击时一次性读取）。
- **口径二 `blocked`**：`ScriptEditorView` 三个字段处理器首行判 `Parent == null`，而它是 `Window`。
  阶段 157 只在**直接 `Show()`** 的宿主里量过并记成"夹具限制"；本阶段按**生产形状**（`ShowDialog(owner)`）量出
  **`Owner` 有、`Parent` 恒 null、驱动三个框模型一字不动** ⇒ **生产里打字也不落模型**（改名/改描述/改代码
  都不进 `HmiScript`；`ScriptListChanged` 不触发 ⇒ 不标脏、不触发自动保存；去抖语法校验不启动；
  **「调试运行」跑旧代码**）。旁证：`InsertTemplate` / AI 生成两条按钮路径**自己显式写模型** —— 绕守卫的补丁。
  **登记 `#89`**（修法：去掉三处 `Parent == null` ＋ 把 `PopulateEditPanel` 里 `TxtCodeEditor.Text` 那句
  包进 `_isUpdating`；波及面：第五层宿主两条 `Inert` → `Moves`，本阶段的取证判据要改成驱动面）。
- **判据 4 条**：`SimulatorTextBoxWiringTests` 2（8 手势 ＋ 双向差集含登记表）·
  `ScriptEditorTextBoxWiringTests` 2（前提取证 ＋ "前提不满足版"双向差集）；普查面第 3 条扩成**三态**
  （`driven` / `blocked` / `census`，各自下限自检 ＋ "判据必须覆盖每个具名框"）。
- **负控 5 支**：摘 `SimulatorView` 表内接线 · 新增没接线也没登记的具名框 · 登记表塞一个其实接了线的框 ·
  **摘掉 `Parent == null` 守卫**（`blocked` 承重的直接证据，读数 `开机脚本 → 改过名`）·
  把只普查的文件标成 `driven` 并指向不含它框名的判据。
- **仍未做**：面 A 剩 **5 文件 / 14 条**（新口径之后**都可做**了，差各自夹具与读面）· `ScriptEditorView`
  3 条等 `#89` 修好 · 面 B **9 条**。
- 🩸 **顺带更正阶段 190 一处手抄错的合计数**（专题 §7.5 与登记册 §106.6 两处活文档就地改）：
  "面 A 剩 7 文件 / **31** 条" → **24** 条（`4+1+2+1+6+3+7 = 24`）。同族教训：**合计数要现算**。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2252 / 315 / 748 / 8`（合计 **3589**）
＝ **3589 通过 / 0 失败**（阶段 190 为 3585，**+4**）；全量 App.UiTests **748 / 0**（连跑 2 次，38 / 39 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；`MainWindow.axaml.cs` 仍停在 **2983**（本阶段 0 行产品代码改动）。

## 阶段 192（2026-10-01）—— 修 `#89`：脚本编辑器的字段编辑**真的落模型**了（并逼出第二层）

**一句话**：阶段 191 取证的那条真缺陷（三个字段处理器首行 `Parent == null`，生产形状下 `Parent` 恒 null
⇒ 打字不落模型）这一片修掉了；**修好第一层才露出第二层** —— "改一个字就丢当前脚本"也在同一片里修掉。

- **第一层**：`OnScriptFieldChanged` / `OnCategoryChanged` / `OnCodeTextChanged` 三个条件去掉
  `|| Parent == null`（防重入改由 `_isUpdating` ＋ `_currentScript` 负责）；**程序化回填全部收进
  `_isUpdating` 窗口** —— `PopulateEditPanel` 里 `TxtCodeEditor.Text`（原来在 `_isUpdating = false`
  **之后**）、`InsertTemplate`、AI 生成后的面板刷新。⚠ 阶段 157 与阶段 187 对"程序化赋值会不会触发
  `TextChanged`"两说相反且未复测 ⇒ 窗口包裹按"**会**"写，两种框架行为下都安全。
- 🩸 **第二层**：`OnScriptFieldChanged` → `RefreshScriptList()` → `Items.Clear()` **引发一次
  `SelectionChanged`** ⇒ `OnScriptSelectionChanged` 的 `else` 把 `_currentScript` 置 null、收起面板；
  而原来的"恢复选中"看的正是 `_currentScript` ⇒ **改一个字就丢当前脚本、后面每个字都不落模型**。
  这一层在修第一层之前**走不到**（处理器被守卫整个挡掉）。修法：**重建窗口** `_rebuildingScriptList`
  （`try/finally`）＋ 失败分支**显式清空并收起面板**（把原来靠 `Items.Clear()` 副作用实现的行为显式化）。
- **判据侧**：`ScriptEditorTextBoxWiringTests` 从"前提取证"改成**驱动面**（名称/描述/代码 3 条手势
  ＋ 断言 `ScriptListChanged` 真抬；`Parent == null` 仍断言当根因现场）· 第五层宿主两条 `Inert`
  （`ChkEnabled` / `CboCategory`）→ `Moves`/`SelMoves`，**期望值一个字没改** · 普查表
  `ScriptEditorView.axaml` `blocked` → `driven`（**6 driven / 0 blocked**）。
- ⚠ `blocked` 归零 ⇒ 那一档没有运行期覆盖 ⇒ 把「状态 × 判据文件」的组合规则抽成纯函数
  `StatusAllowsDriver` ＋ **自测** `StatusAllowsDriver_SelfTest`（7 条断言）。
- **负控 5 支**：守卫注回 · 去掉重建窗口（读数：第二个字段 `''` —— 第二层的直接证据）· 驱动面框名写歪 ·
  产品侧不写 `Code` · `driven` 行改成 `blocked` 且判据不存在（证明 0 行那一档仍咬人）。
- **仍未做**：面 A 剩 **5 文件 / 14 条** · 面 B **9 条**；另如实记一条边界
  （`OnScriptSelectionChanged` 的 `SelectedIndex = 0` 兜底分支没有单独判据）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 749 / 8`（合计 **3590**）
＝ **3590 通过 / 0 失败**（阶段 191 为 3589，**+1**）；全量 App.UiTests **749 / 0**（连跑 2 次，38 / 37 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；产品侧 `ScriptEditorView.axaml.cs` **838 → 901** 行
（+63，几乎全是注释）；`MainWindow.axaml.cs` 仍停在 **2983**。

## 阶段 193（2026-10-01）—— 面 A **收口**：最后 5 个文件 / 14 条接线全部 driven

**一句话**：`#89` 修完（阶段 192）之后面 A 只剩 5 个文件 / 14 条；这一片把它们全部升级成 `driven`
⇒ **11 个文件 / 48 条接线一个不剩**（`blocked` 0 · `census` 0）。**产品代码 0 行改动**。

- **五个驱动面**（每个都先做"够得着"的前提，再逐条驱动、读模型）：
  `Editors/VariablePicker`（1 条 / 4 手势，读变量树两层叶子；过滤 **Trim**）·
  `CommunicationConfigView`（4 / 8，读那句预览的空/非空；闸门①条数 1…500、闸门②前缀 **Trim**）·
  `ProjectSettingsView`（2 / 2，读**草稿**里的 `RuntimeUser`；用户名 **Trim**、显示名**不裁剪**）·
  `RecipeEditorView`（6 / 8，读 `RecipeItem`；单位**不裁剪**、**解析失败 ⇒ 抬到 `double.MinValue`/`MaxValue`**）·
  `PropertyPanelView`（1 / 5，读 `VariableBindingData`；表达式**不裁剪**、编译失败/空白**一个字段都不写** ——
  那个框出厂 `IsVisible="False"`，得先把 `BindingKindCombo` 拨到「表达式」才够得着）。
- **口径表补第三张**：`AssertFullyDriven` 加 `anonymousWired`（**面 B 认账**）——
  `ProjectSettingsView` 那条按名找不到的接线（`AttachChangeHandler<T>` 里的泛型参数 `txt`）
  如实进表；双向判（按名找得到的框不许往里塞 / 说有这么条接线而代码里没有也红）。
  ⚠ 顺带**更正面 B 的描述**：两种形态 ＝「代码里 `new` 出来的框」＋「**辅助函数注入**」。
- 🩸 **三条"顺序/前提"教训**（都当场被红抓出来）：① `TxtBatchCount` 的 XAML 初值是 **10** 不是 0
  （闸门分辨要求先摆合法值，并把初值写成前提自检）；② `PropertyPanelView` 切到表达式源那一跳
  **本身**就建了一条空绑定（`Assert.Empty` → `Assert.Single` ＋ `Assert.Null`）；
  ③ `ProjectSettingsView` 那两条接线写的是**草稿**（读草稿 ＋ 加"工程没动"的反面见证）。
- **判据 10 条**（5 文件 × ［驱动 ＋ 单文件双向差集］）＋ 普查表 5 行 → `driven`（下限 6 → **11**）。
- **负控 6 支**：`VariablePicker` 去裁剪 · `RecipeEditorView` 摘表内接线 · `CommunicationConfigView`
  去条数上界 · `ProjectSettingsView` 去用户名裁剪 · `PropertyPanelView` 给表达式加裁剪 ·
  普查面一行退回 `census`（面 A 下限红）。
- **仍未做**：**面 B 9 条**（`FaceplateTemplateEditorView` 6 · `ProjectSettingsView` 1 ·
  `StyleEditorView` 1 · `UserManagerView` 1）—— `anonymousWired` 只是认账、不是驱动；
  ⚠ 其中 `ProjectSettingsView` 那条其实够得着，卡在"私有 `_hasChanges` 没有探针"，
  要收它得先给"面板被标脏"造一个见证（标题 `*` / `BtnApply` 使能）。**（阶段 194 已收口：见下。）**

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 759 / 8`（合计 **3600**）
＝ **3600 通过 / 0 失败**（阶段 192 为 3590，**+10**）；全量 App.UiTests **759 / 0**（连跑 2 次，38 / 33 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

## 阶段 194（2026-10-01）—— 面 B 收口：9 条「按名找不到」的接线**各找各的入口**

**一句话**：面 A 收口（阶段 193）之后账上只剩面 B；这一片给 4 个文件**各一个不同的入口**，
把 9 条接线全部驱动起来 ⇒ 普查面两个面都不再有"只普查"的行。**产品代码 0 行改动**。

- **四个入口**（都不按名字找控件）：`FaceplateTemplateEditorView`（6 条，卡片 `Border(Tag=index)` →
  `Grid` → **按 (行,列)** 取框，`Assert.Single` 把坐标契约钉住；驱动完点「保存」读
  `template.Parameters[0]`）· `StyleEditorView`（1 条辅助函数线 × **十个**颜色框：树序 ＋
  `PlaceholderText == "#RRGGBB"`，断言恰好 10；读 `project.Theme`——`_theme` 就是它本人）·
  `UserManagerView`（1 条，从 `view.OwnedWindows` 钻进「重置口令」对话框，取其中唯一的
  `TextBox`/`Button`；读确认按钮使能态——这条线没有模型落点）· `ProjectSettingsView`（1 条，
  泛型参数 `txt` ⇒ 用 `TxtAuthor` **代表驱动**，见证是"点确定之后工程真的被保存"）。
- **口径补全**：`TextBoxWiringFace` 新增 `AssertAnonymousFace(file, identifiers, floor)` ——
  面 B 的"认账"也**双向**（新接线不认账红 / 认了代码里没有红 / **把具名框往里塞**也红）；
  普查面面 B 表加 `Driver` 列，给了判据的行必须在判据里**逐条点名**接线标识
  （面 A 用"每个具名框"核对，面 B 没有具名框，**改用接线标识**核对）。
- 🩸 **顺带更正阶段 193 的一句话**：那 9 个框原先登记为「**没有** TextChanged 接线」——不准确，
  它们是**辅助函数注入**（`AttachChangeHandler(TxtAuthor)`），只是没有**直接**写法；
  理由改成「辅助函数注入（面 B 那条线），由 `TxtAuthor` 代表见证」。
- **判据 8 条**：Faceplate 3 · Style 2 · UserManager 2 · ProjectSettings ＋1（`AuthorEdit_...`）。
- **负控 6 支**：Faceplate 摘描述接线 · Style 不再接线 · UserManager 摘口令框接线 ·
  ProjectSettings 的 `txt` 空转 · Faceplate 新增没认账的接线 · 普查面面 B 条数写错。
  🩸 其中 nc4 第一版注入**构建不过**（注释掉 `else if` 的分支体）⇒ 改成"接线还在但空转"才成立。
- **仍未做（"收口"不等于"全仓输入控件都驱动了"）**：普查面只算**接了线**的框（`Views/` 下
  153 个具名 `TextBox` 里 **96 个没有** `TextChanged`，走"点保存/应用"，**不是**缺口）；
  面外还有别的输入控件族（`NumericUpDown.ValueChanged` 等）与 `Views/` 之外的输入。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 767 / 8`（合计 **3608**）
＝ **3608 通过 / 0 失败**（阶段 193 为 3600，**+8**）；全量 App.UiTests **767 / 0**（连跑 2 次，38 / 37 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

## 阶段 195（2026-10-01）—— **数字框接线面**：照 TextBox 那一族再做一遍 ＋ 修 `#90`

**一句话**：TextBox 那一族收口（阶段 194）之后，这一片把**同一套做法**搬到**数字框族**
（`NumericUpDown` / `fa:FANumberBox` 的 `ValueChanged`）；新判据**首跑就逼出一条真缺陷**（`#90`，同批修掉）。

- **实测与分账**：`Views/` 下 **60 个具名数字框**，**45 条**接了 `ValueChanged` ＝ 面 A **43 条**
  （4 文件）＋ 面 B **2 条**（都在 `ProjectSettingsView` 的 `AttachChangeHandler` 里）：
  `PropertyPanelView` 31 条按 **`enumerated`**（既有枚举驱动判据覆盖，**不重复记账**，核对是**指针级**的）·
  `SettingsDialog` 9 · `ScreenManagerView` 2 · `ProjectSettingsView` 1 按 `perBox` 逐条驱动。
- **两个族的驱动纪律相反**：TextBox 程序化赋值**不触发** `TextChanged`（要补发事件）；
  数字框程序化赋值**会触发** `ValueChanged`（写值即可）。⚠ 写**同值**不发事件 ⇒ 手势值必须与起点不同。
- 🩸 **新判据逼出的真缺陷 `#90`**：`ProjectSettingsView.AttachChangeHandler` 第一支是
  `ctl is NumericUpDown`，而该视图 **16 个数字框全是 `fa:FANumberBox`**（探针实测：基类
  `TemplatedControl`、`Value` 是 `double?` —— **不是** `NumericUpDown` 的子类）⇒ 16 次调用
  **一次都没挂上** ⇒ `_hasChanges` 恒 false ⇒ 改数字后点「确定」被 `if (_hasChanges)` 挡掉、
  **静默丢弃**（点「应用」才生效）。**修法 2 行**（补一支 `FANumberBox`）；副作用如实同步：
  该文件面 B 标识 1 → **2**。
- 🩸 **同阶段踩到一条假绿**：`ScreenManagerView` 的手势第一版写 `1024/600` —— 而那是
  `ScreenFactory` 的兜底尺寸 ⇒ 写同值不发事件、断言照样通过，**负控 nc2 摘掉接线时它没红**
  （只有"扫描面下限"红了）⇒ 换成 `1280/720` ＋ 三条前提自检（起点是兜底值 / 手势值≠起点 / 框回填模型值）。
- **共享工具改名**：`TextBoxWiringFace` → **`ControlWiringFace`**（16 个调用点机械改名一起进本阶段）——
  它现在管两个族，差集逻辑抽成一处 `AssertBidirectional`；新增数字框原语
  （`NamedNumberBoxes` / `WiredByValueChanged` / `WriteNumber` / `NumberValue` /
  `AssertAnonymousNumberFace` / `AssertFullyDrivenNumberBoxes`）。⚠ 两种数字框类型要**按运行时类型分派**
  （`FindControl<NumericUpDown>` 对 `FANumberBox` 会抛）。
- **判据 11 条**：普查面 5 ＋ 三个驱动面各 2（SettingsDialog / ScreenManager / ProjectSettings）。
- **负控 5 支**：去掉 `FANumberBox` 分支（复现 `#90`，读数 `Expected 12 / Actual 8`）·
  摘 `NumScreenHeight` 接线（`驱动 720 后读到 600`）· 摘 `NumGridSize` 接线 ·
  去掉面板占比的 `/100.0`（`读到 '25'，期望 '0.25'`）· `enumerated` 行指向不存在的判据。
- **仍未做**：别的输入控件族（`SelectionChanged` 已在选择族面内 / `IsCheckedChanged` 第五层覆盖一部分 /
  `RangeBase.ValueChanged` 本仓未用）· `Views/` 之外的输入 · "谁读那 96 个 TextBox / 17 个数字框"没有一张账。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 778 / 8`（合计 **3619**）
＝ **3619 通过 / 0 失败**（阶段 194 为 3608，**+11**）；全量 App.UiTests **778 / 0**（连跑 2 次，40 / 36 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；产品侧 `ProjectSettingsView.axaml.cs` **1285 → 1299** 行
（+14）；`MainWindow.axaml.cs` 仍停在 **2983**。

## 阶段 196（2026-10-01）—— 输入框**「谁读」面**：把"没接线"的框的读侧账建起来

**一句话**：两个接线面只算**接了线**的框（写侧）；这一片补**读侧** —— 没接线的框"不是缺口"的
前提是**有人读它**（只被回填、没人读的输入框，用户打字之后什么都不会发生，`#90` 那一类形状）。

- **实测与分账**（`Views/**/*.axaml` 里 **213 个具名输入框**，按"文件＋名字"计；91 ＋ 118 ＋ 4 ＝ 213）：
  **91 已接线**（归两个接线面）· **118 没接线但有读点**（由"点保存/应用"一次性读取 ——
  读点就是"不是缺口"的证据）· **4 只读展示**（登记表：`TxtDeviceVersion` / `TxtSoPath` /
  `DisplayField` / `RoleNameBox`，都 `IsReadOnly`）· **0 个「没人读」**（这一格要被守住）。
- **判据形状**：`HasReadSite(strippedCode, name)` **纯函数** —— `X.member` 后面不是赋值（`==` 算读、
  `+=` 算写）· **裸名**算用（实参 / 转发属性 `=> X`）· ⚠ **写侧助手函数的实参不算读**
  （`AttachChangeHandler(X)` 只标脏、不读值）。三条断言：① 没接线的框必须有读点或在登记表里
  （＋ 扫描面自检 213 / 91 / 118）· ② 登记表**反向**判（真存在 / 真没读点 / 没接线）·
  ③ **量具自测** 9 条（读与写的六种形状）。
- 🩸 **本阶段踩到的**：把"裸名算读"收窄成"写侧助手实参不算读"时**忘了同步自测里那条断言** ⇒
  干净树上 `ReadDetection_SelfTest` 当场红 —— **这正是量具自测存在的理由**（红在自测，
  而不是某条业务判据莫名其妙地红）。
- **负控 5 支**：`TxtAuthor` 的读点换常量（点名 `ProjectSettingsView.axaml::TxtAuthor`）·
  新增只有写没人读的具名框（点名 `NcUnreadBox`）· 登记表名字写歪 · 登记表塞一个其实有读点的框 ·
  读点判定把"赋值"改成恒假（**量具自测**红）。🩸 nc5 第一版注入构建不过（只锚了一行）⇒ 锚两行才成立。
- **仍未做**：读点的**粒度**（只判"有没有人读"，不判"读得对不对"）· 别的输入族与 `Views/` 之外的输入 ·
  `IsReadOnly` 没被判据核对（4 条登记靠人写明）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2252 / 315 / 781 / 8`（合计 **3622**）
＝ **3622 通过 / 0 失败**（阶段 195 为 3619，**+3**）；全量 App.UiTests **781 / 0**（连跑 2 次，40 / 38 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

## 阶段 197（2026-10-01）—— 读点的**行为面**第一刀：`ProjectSettingsView` 的 15 个「保存时读取」框

**一句话**：阶段 196 的读侧面是**结构性**的账（"有没有人读"）；这一片往下一层推 ——
**读得对不对**，先拿最大的那一簇开刀。

- **判据形状**：`ProjectSettingsView` 里 **15 个**「点应用/确定时一次性读取」的数字框
  **逐个驱动成互不相同的值** → 点一次「应用」（`SaveToProject()` 一次性读走全部 15 个）→
  **逐字段**断言模型里是"自己那个框"的值。抄错一行 ⇒ 那一格读到**别的框**的值 ⇒ 红
  （nc1 实测 `读到 '2000'，期望 '8000'`）。
- **三条前提自检**：① 15 个框回填的都是模型值 · ② 手势值 ≠ 起点值（否则"抄对了"与"压根没抄"
  在读数上同形 —— 阶段 195 在 `ScreenManagerView` 上踩过这条**假绿**）· ③ 点「应用」**之前**
  一个都没落盘（落了盘说明它其实有接线、该归接线面）。
- **另两条**：表内互异自检（手势值 / 字段路径 / 框名各自互异）＋ "这 15 个确实没直接接线"
  （用扫描再核一遍，否则与接线面**重复记账**）。
- **§112 登记表补 XAML 依据**：4 个「只读展示」框原先只是一句话；现在判据要求每个登记在 XAML 里
  真的带 `IsReadOnly="True"`（否则"只读展示"这个理由就是空话 —— 一个**可编辑**、又没人读的输入框
  恰恰是 §112 要抓的缺口）。nc3 去掉 `RoleNameBox` 的 `IsReadOnly` ⇒ 当场红。
- **负控 5 支**：抄错一行 · 保存时写常量 · 去掉 `IsReadOnly` · 登记表塞一个没登记的框 ·
  表里两行手势值改成相同（表自检红）。
- **仍未做**：其余视图的读点行为面（配方：找保存路径 → 逐框驱动互异值 → 点一次保存 → 逐字段断言；
  `DeviceManagerView` 那 13 个的读点散在**各个动作处理器**里，要连服务一起驱动 ⇒ 成本高）·
  逐读点的静态明细（"哪个读点写进哪个字段"的表）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 785 / 8`（合计 **3626**）
＝ **3626 通过 / 0 失败**（阶段 196 为 3622，**+4**）；全量 App.UiTests **785 / 0**（连跑 2 次，38 / 37 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

## 阶段 198（2026-10-01）—— 读点行为面**第二刀**：`ProjectSettingsView` 的 9 个文本输入框

**一句话**：阶段 197 做的是这个视图的 **15 个数字框**；这一片补**同一个保存路径的另一半**
（9 个文本输入框），并多验一条口径 —— **空值落哪**。

- **判据形状（两条口径，两次「应用」）**：① **非空**：9 个框各带两侧空白的互异值 → 点「应用」⇒
  每格读到**裁剪后**的原值；② **清空**：9 个框都写空串（起点刻意全非空）→ 点「应用」⇒
  **6 个落 `null`** ＋ **3 个落各自的兜底**（`ProjectVersion → "1.0.0"` ·
  `OutputDirectory → "./export"` · `DefaultRoleId → "role-operator"`）。那三条兜底**只有"清空"这一支
  才看得见** —— 接线面照不到（这些框根本没接线），结构面也照不到。
- **其余三条**：表内自检（9 条文本 / 字段路径 / 框名互异 ＋ **兜底表与手势表一一对应**）·
  前提自检（起点 ≠ 手势值 / 点「应用」前一个都没落盘）· 前提判据（这 9 个确实没有直接
  `TextChanged` 接线，否则该归接线面）。**刻意不在本面**：`TxtRuntimeUserPassword`
  （由「设置口令」按钮读走，归运行时用户面）。
- **负控 5 支**：抄错一行（`Author 读到 '某公司'，期望 '张三'`）· 去掉 `Trim()`
  （`Company 读到 '  某公司  '，期望 '某公司'`）· 改兜底值（`读到 '0.0.1'，期望 '1.0.0'`）·
  空分支 `null` → `""`（`读到 ''，期望 '(null)'`）· 兜底表删一条（表自检红）。
- **仍未做**：`ProjectSettingsView` 的保存路径这一簇**两半都覆盖了**；剩下的读点行为面在**别的视图**
  （`DeviceManagerView` 那 13 个的读点散在**各个动作处理器**里，要连服务一起驱动 ⇒ 成本高）·
  逐读点的静态明细。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2252 / 315 / 789 / 8`（合计 **3630**）
＝ **3630 通过 / 0 失败**（阶段 197 为 3626，**+4**）；全量 App.UiTests **789 / 0**（连跑 2 次，38 / 38 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

## 阶段 199（2026-10-01）—— **XAML 属性式接线面（第三形态）** ＋ 一次勘误

**一句话**：接线有三种形态（① XAML 属性 · ② code-behind `+=` · ③ 辅助函数注入）；选择族面从阶段 115 起
三种一起算，而阶段 189~198 建 TextBox / 数字框两个面时**只扫了 ②** —— 这一片把 ① 量出来、单独成面，
并**更正三处账**。

- 🩸 **勘误**：实测具名 TextBox 里 ② **57 条** · ① **26 条** · ③ 9 条；具名数字框 ② 45 · ① **0** · ③ 2。
  ⚠ ①与②**互斥**（26 条里没有一条同时在 ② 里）⇒ 之前"这些框没有 `TextChanged` 接线"的说法对其中
  **26 个框是错的**。**三处已同步更正**：① `PropertyPanelTextBoxWiringTests` 的 20 条理由
  （原文"没有接线（另有 PropertyPanelWiringTests 逐编辑器驱动）"—— 两句都不准，那一面枚举的编辑器族**不含** TextBox）；
  ② 读侧面分账 `91/122/118/4` → **`117/92/4`**（code-behind 91 ＋ XAML 26 / 读点 92 / 只读展示 4 ＝ 213）；
  ③ 两个接线面的口径明确为"**code-behind 形态**的面"。
- **26 条逐条归类**（3 已驱动 / 23 待驱动）：`FaceplateTemplateEditorView` 3（`TxtName` 由既有
  `FaceplateTemplateEditorUiTests` 真键输入驱动；`TxtCategory`/`TxtDescription` 本面新驱动 ——
  顺带量出两条**不同口径**：名字/分类**裁剪**后写回、说明**原样**写回）·
  三个库/工具箱的 `SearchBox` 3（待驱动：补发事件后断言列表被过滤）·
  `PropertyPanelView` 20（待驱动：编辑框按**控件类型显隐**，要逐类型搭夹具）。
- **4 条判据**：双向差集（视图/控件名/**处理器名**三元组）· 归类核对（`Driver` 必须存在且**点名**该框，
  或 `Note` 写清理由）· 扫描器自测（两形态互斥 / 注释里的不算 / 空值不算）· 本面两条驱动。
- ⚠ **顺手修掉一个隐身 bug**：扫描器原先**不剥 XAML 注释** —— 自测里 `<!-- … TextChanged="OnGhost" … -->`
  被当成活接线；现在统一 `StripXamlComments`（`NamedBoxes` 也一起剥），实测注释掉的具名控件 **0 个** ⇒ 读数不变。
- **负控 6 支**：摘接线 · 登记表处理器名漂移 · 新增没登记的接线 · `Driver` 指向不存在判据 ·
  待驱动 `Note` 清空 · 去掉注释剥离。🩸 其中两条第一版注入**构建不过**
  （Avalonia 的 XAML 编译器校验 `TextChanged="不存在的方法"` ⇒ `AVLN3000`）⇒ "处理器改名"不能只改 XAML、
  "新增接线"必须挂**已存在的**处理器。
- **仍未做**：23 条待驱动的属性式接线（登记表里逐条写了做法）· 数字框族的属性式实测 0 条
  （原语已就绪）· `Views/` 之外的属性式接线 · 三形态要不要合成一个面（本阶段选"一形态一面"）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 793 / 8`（合计 **3634**）
＝ **3634 通过 / 0 失败**（阶段 198 为 3630，**+4**）；全量 App.UiTests **793 / 0**（连跑 2 次，38 / 43 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

## 阶段 200（2026-10-01）—— 属性式接线**收口第一刀**：三个库/工具箱的 `SearchBox`

**一句话**：阶段 199 的登记表里 26 条属性式接线有 23 条待驱动；这一片把**最便宜的三条**
（`ToolboxView` / `ResourceLibraryView` / `FaceplateLibraryView` 的 `SearchBox`）驱动起来，**23 → 20**。

- **三条形状相同**：`TextChanged="OnSearchTextChanged"` → 关键词过滤 → 结果写回列表/画廊。
  **共同的坑**（写进判据的前提自检）：① 处理器首行都是 `if (Parent == null) return;` ⇒
  **必须放进 `Window` 并 `Show()`**；② 程序化写 `Text` 不发事件 ⇒ 用 `ControlWiringFace.Write`
  （**补发** `TextChangedEvent`）；③ 断言**不读本地化文案**（工具箱搜**类型名** `Slider`；
  库/模板用自造夹具名 `Alpha`/`通用`/`乙`）。
- **每条三拍**：过滤命中 · **空结果**（列表空 ＋ 空状态 ＋ 计数文案清空 / 画廊空）· **清空回全量**；
  读数例：工具箱目录 26 项、搜 `Slider` 只剩那 1 条并逐条核对 `ControlType`；
  资源库灌 3 条 ⇒ `alp`（大小写不敏感）1 张 · `乙`（走 `DisplayName`）1 张；
  模板库 3 个 ⇒ `通用`（走 `Category`）2 条 · `"  beta  "`（**Trim**）1 条。
- **负控 5 支**：摘掉属性式接线（**面 ＋ 驱动双红**）· 处理器忽略关键词（**面绿、驱动红**：
  `Expected 1 / Actual 26`）· 去掉 `.Trim()`（`Expected 1 / Actual 0`）· 空状态恒 false ·
  `Driver` 指向**不点名该框**的判据。
  > **nc2 最有信息量**：面管"**接线形态**有没有被登记/归类"，驱动管"**这条接线干了什么**"。
- **仍未做**：属性面板那 20 条（编辑框按**控件类型显隐** ⇒ 要逐类型搭夹具）· 数字框族的属性式实测 0 条 ·
  `Views/` 之外的属性式接线。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2252 / 315 / 796 / 8`（合计 **3637**）
＝ **3637 通过 / 0 失败**（阶段 199 为 3634，**+3**）；全量 App.UiTests **796 / 0**（连跑 2 次，36 / 36 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

## 阶段 201（2026-10-01）—— 属性式接线**收口第二刀**：属性面板 20 条 ＋ 修 `#91`

**一句话**：属性式接线的最后一簇（属性面板 20 个编辑框）不再逐类型搭夹具，而是把既有的**枚举驱动**
判据扩到 TextBox —— 20 条一次性有了判据，**并当场逼出一条真缺陷 `#91`**。

- **判据**：`PropertyPanelWiringTests.EnumerateEditors` 的过滤加上 **`or TextBox`**（逻辑树 91 → **94**；
  多出的 3 个里 2 个是**内嵌 `VariablePicker`** 的 `SearchBox`/`DisplayField` ⇒ 进豁免清单并写明理由，
  1 个是 `ExpressionInput`）· `Drive` 加 `case TextBox`（写值 ＋ **补发 `TextChangedEvent`**，候选序列
  普通文本 → **合法十六进制** → 数字串 → 中性控件名 → 空）· 计数下限 `≥ 60` → **`≥ 85`**。
- 🩸 **逼出的 `#91`**：`VsBgColor`/`VsTextColor`/`VsBorderColor` 在 **`Normal` 态改了什么都不写**
  （装载侧把三格当"控件当前色"填、写回侧只写文本/对齐/图片，而色块还会被刷成新颜色
  ⇒ **"改成功了"的假象**；其余三态正常）。**修法**（354 → **391 行**）：`Normal` 分支补三行写回
  （**只在非空时**写）＋ `SyncContentBox` 同步内容区三个同字段框；装载侧在 `Normal` 改成读控件三色
  （**写读对称**）。**判据** `PropertyPanelVisualStateColorTests` 两条（`Normal` 写回 ＋ 内容区同步；
  切到 `Pressed` ⇒ 写状态条目、**控件三色一字不动**、四条全清空 ⇒ 移除条目）。
- **收口口径**：20 条登记改成 **`enumerated@PropertyPanelWiringTests.cs`**（与阶段 118/195 的
  `enumerated` 同口径：不逐条点名，核对是**指针级**的：文件存在 ＋ 提到 `PropertyPanelView` ＋ 含 `uncovered`）。
  棘轮：**`driven == 26` / `pending == 0`**。
- **负控 6 支**：回退 `#91` 的修（新判据红 **＋** 枚举面红：`VsBorderColor, VsTextColor` 未覆盖）·
  去掉内容区同步 · 枚举面回退（`只找到 71 个编辑器`）· 一条登记挪回待驱动（`Expected 26 / Actual 25`）·
  `enumerated@` 指向不提 `uncovered` 的判据 · 三色无条件写（`Expected "#123456" / Actual null`）。
- **仍未做**：数字框族的属性式接线实测 0 条 · `Views/` 之外的属性式接线 ·
  「视觉状态」的对齐/图片/文本那几项 `Normal` ↔ 其它态口径还没成面。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 315 / 2252 / 798 / 8`（合计 **3639**）
＝ **3639 通过 / 0 失败**（阶段 200 为 3637，**+2**）；全量 App.UiTests **798 / 0**（连跑 2 次，46 / 43 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；产品侧 `PropertyPanelView.VisualStates.cs`
**354 → 391 行**；`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 202（2026-10-01）—— **视觉状态字段的消费账**：7 个字段 × 预览/导出

**一句话**：阶段 201 的 `#91` 修的是**写侧**（`Normal` 态三色改了什么都不写）；这一片问**消费侧**：
面板给用户在状态里设的每一格，**预览**与**导出的 LVGL 代码**各自认不认。

- **起因**：这块**一条判据都没有** —— 全仓 `grep LV_STATE_PRESSED tests/` 是**空的**：
  面板改了、预览变了，导出丢没丢没人看着。
- **实测的账**：四色 ＋ `TextAlign` 作为**样式**真发射（`lv_obj_set_style_*(..., LV_STATE_*)`）·
  `ImageAssetId` 发射（**不在资源库中时用注释声明跳过**）· **`Text`** 只发注释
  （`// State text: … (switch via event callback at runtime)`）·
  🩸 **`TextVAlign` 此前连注释都没有**（面板给了口、预览照状态渲染、导出**静默丢**）。
- **根因（结构性）**：LVGL **没有** vertical-align 样式属性；本仓 Normal 态那条路是
  `lv_obj_set_height(LV_SIZE_CONTENT)` + `lv_obj_update_layout` + `lv_obj_get_height` 的**重定位变换**
  （`CodeGenerator.Screen.cs` 的 Label 分支）——而**变换带不了 `LV_STATE_*` 选择器**。
- **产品侧修的那一处**：`GenerateVisualStates` 补 `TextVAlign` 的**声明注释**（把"静默丢失"变成
  "声明的不支持"）。
- **判据 8 条**（新文件 `VisualStateCodeGenConsumptionTests`）：发射矩阵（含"状态里的色值真的进了输出"）·
  图片"不在库中"必须**声明**跳过 · `Text`/`TextVAlign` 的声明 ＋ **反面见证**（不是以样式选择器形式发射）·
  只设对齐时走 `LV_TEXT_ALIGN_*` 映射 · 全空条目**不发射**任何 `LV_STATE_*` 样式行 ·
  **预览侧对照**（文本色/水平/垂直对齐状态优先、`Normal` 取控件值）· `Normal` 条目被生成器**跳过**。
- **负控 6 支**：去掉 `TextVAlign` 声明（`Not found: "// State vertical align: Bottom"`）·
  `text_align` 丢状态选择器（`Not found: "LV_STATE_PRESSED"`）· 去掉 `State text` 声明 ·
  图片改成静默跳过 · `Normal` 条目不再跳过（`Sub-string found … 0xABCDEF`）·
  预览侧 `VerticalAlign` 忽略状态（`Expected: Bottom / Actual: Top`）。
- **仍未做**（登记 `#92` 剩余部分，需产品决策）：让 `TextVAlign`/`Text` 在运行时真生效
  （生成 `LV_EVENT_STATE_CHANGED` 回调 —— **新功能**）· 按"面板上有、其实不生效 ⇒ 灰显 + 悬停原因"
  的口径在面板上**标明**这两格（UI 决策）· `VisualState` 只覆盖 4 态，要不要加别的态是功能决策。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 315 / 2260 / 798 / 8`（合计 **3647**）
＝ **3647 通过 / 0 失败**（阶段 201 为 3639，**+8**）；`HmiEditor.Core.Tests` **2260 → 2268**；
`App.UiTests` **798 / 0**（连跑 39 / 42 s）；`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；
`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 203（2026-10-01）—— **控件属性（面板字段）的导出消费账**：一次「差点重复记账」的复盘 ＋ 修 `#93`

**一句话**：本想给属性面板全部 **58 个** `HmiControl` 字段建一张"面板 → 导出产物"的消费账，
**先查账发现这一面早就建过了**（`ControlEmitterCoverageTests`，631 行，用的是同样的「API 锚点法」）
⇒ **停手、只补它没覆盖的**，并顺带修掉一条真缺口 `#93`。

- 🩸 **复盘（比新增判据更值钱）**：两条静态测量**都不可靠** ——
  ① 按**字段名** grep CodeGen ⇒ 只有 `RotationDirection` 零命中，而它是经 `EffectiveRotation()`
  **间接**消费的（**假阴**）；② 按 **LVGL API 名** grep tests ⇒ 几个 API 名 0 命中，
  但**"某 API 名 0 命中"不能推出"那一族零判据"**（**口径错**）。
  ⇒ 名字级 grep 既有假阳也有假阴；**必须先看已有的面**。判定口径写死为"在那个文件里 grep 目标 API 名，
  0 命中才算零输出断言"。我刚写的新文件（8 条与它重复）**已删**。
- **补上 9 处（＋2 处反面见证，共 16 条判据）**：`lv_slider_set_range` · `lv_bar_set_range` ·
  **Bar 游标对象**（`_cur_*` + 事件回调跟随）· `lv_led_set_brightness` · **Gauge 主刻度**
  （LVGL 无弧刻度 API ⇒ 注释声明 + `Math.Max(2,…)` 钳制）· Knob 可见 knob · **`lv_keyboard_set_mode`** ·
  Line 的 `LineCap`（Round 发射 / Square 声明）· Polyline 顶点数组 · Table 列宽/行高。
- 🩸 **修 `#93`**：`EmitKeyboardParameters` 此前**只发一行注释** ⇒ 面板的"键盘模式"
  （`Default`/`Number`/`Special`）改了不生效；LVGL **有** `lv_keyboard_set_mode`、模拟器侧也早在用那两个宏
  ⇒ 补齐（与 `#92` 的"结构上表达不了"不同）。
- **顺带一条判据照不到的文档漂移**：`AppendTransform` 的注释写"镜像存在绘制缺陷 ⇒ **只输出注释声明**"，
  而代码**一直照发** `transform_scale_x/y = -256`（`TransformFeatureTests` 三条判据钉的就是真发射）
  ⇒ **注释过时**，已改。
- **负控 6 支**：回退 `#93` · 键盘模式映射写错 · Bar 不发射 range · Bar 不生成游标 · Gauge 不声明主刻度 ·
  Table 不发射行高（读数形如 `未找到调用: lv_keyboard_set_mode(obj_kb1, LV_KEYBOARD_MODE_NUMBER);`）。
- **仍未做**：逐字段的"面板字段 → 导出产物"表 · `#92` 的剩余部分 · **模拟器侧**的消费账。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2276 / 315 / 798 / 8`（合计 **3663**）
＝ **3663 通过 / 0 失败**（阶段 202 为 3647，**+16**）；`HmiEditor.Core.Tests` **2268 → 2284**；
`App.UiTests` **798 / 0**（连跑 37 / 35 s）；`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；
`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 204（2026-10-01）—— **控件发射器 → 判据点**：把"有判据看着"写成可核对的指针

**一句话**：阶段 203 的两条机械代理（按字段名 grep、按 API 名 grep）**都不可靠**；这一片换一层做账 ——
在**发射器粒度**上指名判据，并要求那条判据里**逐字存在**一段断言原文（`Snippet`）⇒
"只有一行注释的发射器"（`#93` 的 Keyboard）**不可能**通过。

- **三道核对（4 条判据）**：① 表 ↔ `ControlParameters.cs` 里**实际定义**的每个 `Emit*Parameters`
  **双向闭合**；② 每条 `判据文件 + 断言片段` 必须**逐字命中**（片段 ≥12 字符）；
  ③ 面板写回的 **58 个** `HmiControl` 字段，**第一个真实读取点**（跳过注释）所在的方法必须落在
  `Emitters` ∪ `SharedConsumers` ∪ `IndirectFields` 里；④ 共用/间接登记自身要真
  （方法在指名文件里找得到、判据含 Snippet；`IndirectFields` 还要"生成器里确实没有直接读取"）。
- **账的内容**：**19 条发射器指针**（15 个 `Emit*Parameters` ＋ `WriteStyleCalls`/`WriteTextCall`/
  `AppendTransform`/`GenerateVisualStates`）· **11 条共用消费点** · **2 条间接消费**
  （`Rotation`/`RotationDirection` 经 `EffectiveRotation()`）。
- 🩸 **建账第一天就抓到一个靠印象绝不会知道的消费点**：核对 ③ 第一次跑就报
  `Name → GenerateActionCallbacks`（`HmiControl.Name` 被读进动作回调的注释里）⇒ 补进 `SharedConsumers`；
  同理被逐个量出来的 `Value`/`Points`/`Cells`/`IsVisible` 进了 `NonPanelFields` 名单。
- **负控 6 支**：删发射器登记 · Snippet 指向不存在的断言 · 删共用消费点 · 删间接登记 ·
  间接登记塞一条其实有直接读取的字段 · **删掉判据文件里那句断言**（证明指针绑的是**真断言**：
  判据被掏空时指针当场失效）。
- **仍未做**：颗粒度是"发射器/消费点"而非"每字段一条判据"（58 行 × 判据名的**逐字段**表仍未建）·
  **模拟器侧**（`SimulatorProjectExporter`）的消费点不在表里 · `#92` 的剩余部分。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2280 / 315 / 798 / 8`（合计 **3667**）
＝ **3667 通过 / 0 失败**（阶段 203 为 3663，**+4**）；`HmiEditor.Core.Tests` **2284 → 2288**；
`App.UiTests` **798 / 0**（连跑 41 / 34 s）；`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；
**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

## 阶段 205（2026-10-01）—— **`DeviceManagerView` 的输入框读点行为面**：5 个驱动、8 个缺注入缝

**一句话**：读侧账里**最后一块没有行为判据**的地方做完了 —— 该视图 13 个具名输入框的读点
**全在动作处理器**里（与属性面板那批"保存路径读点"根本不同），按"有没有可观测的确定性分支"分两半。

- **driven（5 个）**：`TxtHmiLib`/`TxtOutputDir`/`TxtPkgVersion` 走 `UsbUpdatePackager`（**纯本地**打包）
  ⇒ 驱动三格 → 点「生成」→ 断言**真的产出 `hmi-9.9.9.swu`**（版本/目录都来自那两个框）＋ 结果文本带
  **产出路径** ＋ 日志里 `v9.9.9 → <outDir>`（语言无关）＋ 两条校验分支（空 `.so` / 空输出目录）；
  `TxtHost`（TCP 分支"主机为空"校验）· `TxtFelImage`（镜像不存在校验）—— 后两条证明**读点被到达**。
- **registered（8 个）**：`TxtTcpPort`（非法值**静默回退 23** ⇒ 不可观测）· `TxtBaudRate`（要真串口）·
  `TxtLocalVersion`/`TxtHmiSwu`/`TxtFwSwu`（**先 `RequireChannel()`**）· `TxtFelTool`/`TxtFelArgs`
  （要真的调起 `sunxi-fel`）· `TxtDeviceVersion`（只读展示，指 §112 登记）。台账**二选一**：
  13 个框要么被驱动、要么登记理由（太短 ⇒ 红）。
- 🩸 **结构性观察 ⇒ 新登记 `#94`**：视图把服务**直接 `new`**（`DeviceDetector`/`DeviceFlasher`/
  `HmiUpdateService`/`UsbUpdatePackager`）**没有注入缝** ⇒ 那 8 个框的成功路径原理上不可驱动。
  两条出路：**加注入缝**（本仓已有 `IPropertyValidator`/`TimeProvider` 先例）或**明确"只有真机跑得到"**。
  顺带量到：`OnFelFlashClicked` 的"镜像不存在"有**两道**闸（面板 ＋ `DeviceFlasher`），
  负控 nc5 证明**面板那道更早**。
- **负控 6 支**：打包不读版本框（`没产出 …/hmi-9.9.9.swu；… hmi-1.0.0.swu`）· 打包不读输出目录
  （`等不到：打包结果文本被写上`）· 删空 `.so` 校验 · 删主机校验（`[连接] 错误: 连接设备 :23 失败`）·
  删 FEL 镜像校验（`[FEL] 正在 FEL 烧录...`）· 台账漏一个 driven 框。
- **仍未做**：`#94`（注入缝）· `TxtTcpPort` 的静默回退要不要给可见提示（UI 决策）·
  别处"读点在动作处理器"的框没有统一清点（`AiIconDialog` 的 `HeightBox`/`WidthBox` 等可能同形）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2280 / 315 / 802 / 8`（合计 **3671**）
＝ **3671 通过 / 0 失败**（阶段 204 为 3667，**+4**）；`App.UiTests` **798 → 802**（连跑 38 / 39 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；**产品代码 0 行改动**（`MainWindow.axaml.cs` 仍 2983）。

## 阶段 206（2026-10-01）—— **给 `DeviceManagerView` 加注入缝**：`#94` 闭环，13 个输入框全部可驱动

**一句话**：按 `#94` 的第一条出路做 —— 把设备服务抽成接口、视图改成注入，那 8 个"只有真机才可观测"
的输入框变成**可用假实现驱动**，台账 `Registered` **清零**（棘轮）。

- **产品侧（行为零变化）**：新增 `src/HmiEditor.Device/Abstractions/DeviceServiceAbstractions.cs`
  （`IDeviceDetector` / `IHmiUpdateService` / `IDeviceFlasher` / `IUsbUpdatePackager` /
  **`IDeviceChannelFactory`**，只声明视图真正用到的成员）；四个原实现类原样 `implements`；
  新增默认通道工厂 `DeviceChannelFactory`（把视图原先那两行就地 `new SerialDeviceChannel/TcpDeviceChannel`
  搬进去）；`DeviceManagerView` 加一条 **`internal`** 注入构造（传 null ⇒ 真实实现）。
- ⚠ **`internal` 是判据逼出来的**：做成 `public` 会让该窗口多一条公共构造，
  `WindowProductionPathUiTests` 的「双路径」元检查立刻要求改判 `TwoPaths=true`，而它又要求
  **生产构造与设计器构造的字段形状不同** —— 本视图两条路装同一批服务，形状必然一致 ⇒ 必然红。
  `internal` 是本仓既有先例（`ToolboxView`：靠 `InternalsVisibleTo` 给判据用）。
- **判据侧**：`FakeChannelFactory` 记 TCP/串口参数（含**非法值回退口径**：端口 ⇒ 23、波特率 ⇒ 115200）·
  `FakeUpdateService` 记本地版本与 `.swu` · `FakeFlasher` 记固件包与 FEL 三格 ·
  `FakeDetector`/`FakeUpdateService` 的版本回填**只读展示框** `TxtDeviceVersion`；
  台账变成"**13 个框全部 driven**、`Registered` 必须为空"（棘轮 `Assert.Equal(13, Driven.Length)`
  ＋ `Registered.Count == 0`）。
- **负控 7 支**：TCP 不走工厂 · 串口不走工厂 · 更新服务不注入 · 刷写器不注入 · 检测器不注入 ·
  台账塞回一条 `Registered` · **端口回退值改掉（23 → 9999）**。读数例
  `等不到：更新服务收到本地版本（已等 10000 ms）` · `等不到：检测结果回填到只读展示框` ·
  `有框被登记为「不可驱动」…：TxtTcpPort`。
- **仍未做**：`TxtTcpPort` 的静默回退要不要给可见提示（UI 决策）· 别处"读点在动作处理器"的框没有统一清点 ·
  服务**自身**的行为仍由 `HmiEditor.Device.Tests` 的 96 条判据负责（本面只到"参数真的传到了服务"这一层）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700 → 702**（新增 2 个产品文件）；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2280 / 315 / 806 / 8`
（合计 **3675**）＝ **3675 通过 / 0 失败**（阶段 205 为 3671，**+4**）；
`App.UiTests` **802 → 806**（连跑 40 / 50 s）；`HmiEditor.Device.Tests` **96 / 0**；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；`DeviceManagerView.axaml.cs` **552 行**；
`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 207（2026-10-01）—— 「读点在动作处理器」**第二刀 ＋ 全 App 台账**：`DownloadToHmiView` 4 框 ＋ 修 `#95`

**一句话**：`DeviceManagerView` 那一族做第二遍（`DownloadToHmiView` 的 4 个框），并把**全 App**
"读点在动作处理器"的输入框做成一张**可核对台账**（22 条：16 driven / 6 registered）——
写判据时撞出一条真缺陷 `#95`。

- **产品侧**：复用阶段 206 的 `IDeviceChannelFactory`；**新增 `IHmiDeployService`**（原先
  `_deployService = new()`，`HmiDeployService` 原样 `implements`）；视图加一条 **`internal`** 注入构造
  （与 `DeviceManagerView` 同因：公开会多出一条公共构造，触发 `WindowProductionPathUiTests` 的双路径元检查）。
- 🩸 **`#95`**：`var remoteDir = TxtRemoteDir.Text?.Trim() ?? DefaultSourceDir;` —— `??` **只认 null**；
  用户**清空**这一格时 `Text` 是 `""`（非 null）⇒ 部署服务收到**空目录** ⇒ `mkdir -p ` 没参数、
  文件落到设备当前目录（静默走错地方）。修法：**空/空白都回退默认**；判据
  `RemoteDir_ReachesTheDeployService`（正常值原样到达 ＋ 空白 ⇒ 收到默认）；**负控 nc3** 回退修 ⇒ 当场红。
  ⚠ 这是"静默丢弃/走错"的**第三类形态**（`#90` 写侧丢 · `#91` 读侧丢 · 本条**回退只认 null 不认空白**）。
- **全 App 台账** `ActionHandlerReadInventoryTests`（22 条）：扫描口径 = 具名 TextBox/数字框 →
  **跳过两种形态的接线**（code-behind `+=` 与 **XAML 属性式**）→ 用「谁读」面那把尺子判读点 →
  读点所在方法名落在动作处理器模式里进账。覆盖 `DeviceManagerView` 12 ✅ · `DownloadToHmiView` 4 ✅ ·
  `AiIconDialog` 4 ⏳ · `TagMonitorView` 1 ⏳ · `BuildRunDialog` 1 ⏳；三道路核对（双向闭合 / 指针点名 /
  理由长度）＋ 抽查"登记项的读点确实在动作处理器里"。
- **读数修正 26 → 22**（我自己的普查口径错过两次）：`DeviceManagerView.TxtDeviceVersion` 被我的正则
  误判成"读"（把 `X.Text = …` 这种**赋值**也算读，实际是只写不读的展示框）；三个 `SearchBox` ＋
  四个 `PropertyPanelView` 色框是 **XAML 属性式接线** ⇒ 归阶段 199 的接线面。
- **负控 6 支**：TCP 不走工厂 · 部署服务不注入 · **回退 `#95`** · 台账删一条 · driven 判据不点名 ·
  registered 理由写短。🩸 nc4/nc5 第一版注入构建不过（删条只删一行、注释吃掉行尾 `),`）。
- **仍未做**：`AiIconDialog` 4 框（要 AI 图像生成器的缝）· `TagMonitorView`（要模拟器/设备侧缝）·
  调色对话框路线 · 台账只覆盖 TextBox ＋ 数字框。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **702**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2280 / 315 / 812 / 8`（合计 **3681**）
＝ **3681 通过 / 0 失败**（阶段 206 为 3675，**+6**）；`App.UiTests` **806 → 812**（连跑 37 / 40 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 208（2026-10-01）—— 把"导不出去"**说给用户听**：`#92` 的标注那半

**一句话**：阶段 202 量出非 `Normal` 态的**状态文本**与**垂直对齐**在导出侧结构上表达不了
（导出只留一句声明注释）—— 但那声明**只在代码里**，用面板的人看不到。这一片给那两格各挂一句
**悬停说明**（本仓对"面板上有、其实不生效"的一贯口径），并加两向判据。

- **做法**：`VsText` → `ToolTip.Tip="{res:Loc prop.visual.state.export.hint.text}"`；
  `VsTextVAlign` → `…hint.valign`；两个键同时进 **zh-CN 与 en-US** 字典。
  ⚠ **不加 `Lang` 常量**（该键只被 XAML 引用；加常量会抬高「`Lang` 常量未被静态引用」那个只减不增的守卫读数）。
- **判据（两向）**：XAML 那两格必须挂**对应**的键（挂错键也红）＋ 键在两本字典里都要**非空**
  （否则界面显示键名）。
- **负控 5 支**：去掉提示 · 挂错键 · **en-US 删键** · 值改空串 · zh 改名。读数例
  `VsText 没有挂「导出侧只留声明」的悬停说明（期望 ToolTip.Tip={res:Loc prop.visual.state.export.hint.text}）`。
  🩸 nc2 第一版注入**构建不过**（XML 注释写进了起始标签内部 —— XML 不允许）。
- **顺带量到的三件事（免得后人重做）**：① `ComboBox`/`CheckBox` 的"动作处理器读点"＝ **0 条**
  （43 个未接线选择控件的"读点"全在初始化/填充方法里，且多数是"对 `.Items` 的写"被启发式当成读）
  ⇒ 「动作处理器读点」台账不需要往选择控件扩面；② **模拟器侧不是按字段名消费的**
  （按字段名 grep 得"50/58 零命中"是**假象**：屏幕/刷新代码由 `HmiEditor.CodeGen` 生成）；
  ③ **扫描面边界已核**：`Views/` 之外没有具名输入控件 ⇒ 两次普查的扫描面是完整的。
- **仍未做**：让 `TextVAlign`/`Text` 在运行时**真生效**（生成 `LV_EVENT_STATE_CHANGED` 回调 —— **新功能**）·
  `VisualState` 只覆盖 4 态，要不要加别的态是功能决策。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **943**）；
⚠ **标记行：独立 7 次 / 双份 1 次**（合计仍是 9）—— 这是 `gate-readings.py` 文档里写明的**结构性**现象
（并发写同一管道时两行被挤成一行），不是内容差；脚本 **29**；路径卫生 **702**；
步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2280 / 315 / 813 / 8`（合计 **3682**）
＝ **3682 通过 / 0 失败**（阶段 207 为 3681，**+1**）；`App.UiTests` **812 → 813**（连跑 42 / 44 s）；
`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 209（2026-10-01）—— `AiIconDialog` 的**生成源注入缝**：这一族第三个视图，台账 20/2

**一句话**：按 `#94`/`#95` 同一套路，把 `AiIconDialog` 的 AI 生成路径抽成 `IIconGenerationSource`
（默认实现逐字转发）⇒ 它那 4 个输入框全部可驱动；「读点在动作处理器」台账变成 **20 driven / 2 registered**。

- **产品侧（行为零变化）**：新增 `src/HmiEditor.App/Services/IconGenerationSource.cs`（接口 ＋
  `PreviewManagerIconSource`）；`AiIconDialog` 加一条 **`internal`** 注入构造，生成相关的 4 处
  `_manager.X` 改走 `_source`（⚠ `internal` 与设备侧同因：不惊动 `WindowProductionPathUiTests` 的双路径元检查）。
- **判据**（`AiIconDialogInputReadFaceTests` 3 条）：`ImageInputBox` 提示词**带空白 ⇒ 假源收到裁剪后**的 ·
  `WidthBox`/`HeightBox` ⇒ 假源收到 `(320, 240)` · `InputBox` 两行条目 ⇒ 假源收到 2 条（名字解析）＋
  `FixedVariant` 为 null · 反面：全空白提示词与 `IsConfigured=false` **都收不到请求**。
- **负控 5 支**：单图/批量不走生成源（`等不到：假源收到…请求`）· 尺寸写死 200×200
  （`Expected (320, 240) / Actual (200, 200)`）· 提示词不再裁剪（`Assert.Null() Failure … Actual (200, 200)`）·
  台账挪回 registered。
- 🩸 **顺带撞到一条文本级守卫的误伤**：`AiCancellationWiringTests` 正则在**注释**里也匹配
  `Xxx.GenerateAsync(...)` —— 我在类文档里写的省略号假调用被当成"没传 token 的调用点"；
  处置是注释不写带括号的调用字样并留提醒（没动守卫口径）。
- **仍未做**：台账剩 2 条 registered（`TagMonitorView.TxtWriteValue` 要模拟器/设备侧的缝；
  `BuildRunDialog.OutputLog` 是只读日志框）· 台账只覆盖 TextBox ＋ 数字框。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **702 → 703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2280 / 315 / 816 / 8`（合计 **3685**）
＝ **3685 通过 / 0 失败**（阶段 208 为 3682，**+3**）；`App.UiTests` **813 → 816**（连跑 38 / 38 s）；
`HmiEditor.AI.Tests` **315 / 0**；`UnusedBaseline` 不变 1109（本阶段没加 Lang 常量）；
`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 210（2026-10-01）—— 「读点在动作处理器」这一族**收口**：最后一条 ＋ 台账 21 条全 driven

**一句话**：最后一条 registered（`TagMonitorView.TxtWriteValue`）其实**根本不需要产品缝** ——
它原先的登记理由写错了；而另一条（`BuildRunDialog.OutputLog`）**根本不是输入框**。
两处清完后台账 **21 条全 driven、`Registered` 归零**（棘轮）。

- 🩸 **登记错了**：`TxtWriteValue` 原写"要模拟器/设备侧的注入缝"——但视图构造**本来就接收
  `RuntimeBindingEngine`**、引擎**本来就吃** `IRuntimeValueSource` ⇒ 一个假值源就够，**0 行产品改动**。
  判据 `TagMonitorInputReadFaceTests`：走生产序列（宿主建完视图调 `RefreshRows()`）驱动 `12.5` ⇒
  假值源收到；反面三条（非法文本 / 空 / 源未连接）都不写。
  **教训写进棘轮消息**：登记的理由**必须验一遍**（原抽查只验"读点是否在动作处理器里"这个形态）。
- **`OutputLog` 不是输入框**：`IsReadOnly="True"` 的日志框 ⇒ 扫描口径**跳过只读框**，并加口径自检
  （`TxtWriteValue` 在面内、`OutputLog` 在面外）。与「谁读」面口径不冲突（那一面问"有没有人读"）。
- **收口**：21 条全 driven（DeviceManager 12 ＋ DownloadToHmi 4 ＋ AiIcon 4 ＋ TagMonitor 1），
  `Registered` 为空，棘轮 `driven >= 21 && registered == 0`。
- **负控 5 支**：不再读输入框（`Expected 12.5 / Actual 1`）· 去掉解析失败分支（`Actual 0`）·
  台账挪回 registered（`只有 20 条已驱动`）· 扫描面不跳只读（`…但台账里没有它们：OutputLog`）·
  加回 `OutputLog` 条目（`扫描面里根本没有它 —— 登记已过期`）。
- **仍未做**：台账只覆盖 TextBox ＋ 数字框（选择控件的动作读点实测 0 条）· 服务自身行为由各自单元面负责。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2280 / 315 / 817 / 8`（合计 **3686**）
＝ **3686 通过 / 0 失败**（阶段 209 为 3685，**+1**）；`App.UiTests` **816 → 817**（连跑 43 / 35 s）；
`UnusedBaseline` 不变 1109；**本阶段产品代码 0 行改动**；`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 211（2026-10-01）—— **逐字段索引**：58 个面板字段 → 消费点 → 判据文件

**一句话**：把阶段 203（"每个字段都被读了"）与阶段 204（"每个发射器都有判据"）**接起来**，
细化到**字段粒度**：58 行表格（字段 → 消费点 → 判据文件），并把这张表做成**文档 ↔ 代码双向闭合**的判据。

- **产物**：`docs/2026-10-01-control-field-judge-index.md`（58 行；`Rotation`/`RotationDirection` 走间接
  `EffectiveRotation()`）。
- **判据三道**（`ControlEmitterJudgePointersTests.ControlFieldJudgeIndex_MatchesTheDoc`）：
  ① 每个面板字段都解析得出判据（不许 `—`/`(未登记)`）；② 文档 ↔ 代码**双向闭合**（新加面板字段、
  或把某字段的消费点/判据换掉 ⇒ 红）；③ 指名的判据文件必须真实存在；另有面自检"≥58 行"。
- **负控 5 支**：文档删一行 · 加一行假字段 · 改判据文件名 · 改**消费点**名 · 发射器指针表里指向不存在的文件。
  读数例 `1 行在文档里没有（改了执行侧就得同步 …）` / `1 行在文档里但对不上执行侧` /
  `索引指向了不存在的判据文件：GhostTests.cs`。
- **仍未做**：本表只到"哪条判据"，不保证覆盖该字段的**每一种取值**；`Consumer` 列是**第一个**读取点。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2281 / 315 / 817 / 8`（合计 **3687**）
＝ **3687 通过 / 0 失败**（阶段 210 为 3686，**+1**）；`HmiEditor.Core.Tests` **2280 → 2281**；
`UnusedBaseline` 不变 1109；**本阶段产品代码 0 行改动**；`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 212（2026-10-01）—— **枚举型选择字段的取值账**：每个取值都必须有确定的导出语义

**一句话**：阶段 211 的逐字段索引回答"**哪个字段**有判据看着"，这一阶段回答"**每个取值**呢" ——
对 6 个枚举型选择字段（18 个取值）把"面板真的会给出的取值集合"与"登记的取值"做双向闭合，
并对每个取值**真跑一遍生成器**断言发射；顺带撞出 `#96`。

- **判据三道**（`EnumeratedChoiceEmissionTests`）：① 双向闭合（面板加一个选项 / 枚举加一个成员 ⇒ 红）；
  ② 每个取值真跑生成器（登记了片段必须逐字出现；登记 `null` 必须**不发射**，用 `CSource.ParseCalls`
  判调用 —— `LineCap` 那行"以平头表达"的注释因此不误判）；③ 面自检（字段 ≥6、取值行 ≥18）。
- **覆盖**：`LineCap`（Butt≡Square 都落平头，生成器**显式写注释**说明 —— 已声明口径）/ `KeyboardMode`
  （Default 仍是 `TEXT_LOWER`，阶段 203 修的语义错位）/ `StretchMode`（Contain 带 <9.3 回退）/
  `TextAlign` / `TextVAlign`（Top/Bottom 走"高度自适应＋重定位"）/ `ValueDisplayType`。
- 🩸 **新增 `#96`**：`TextAlign` / `ValueDisplayType` 有**三条不一致的链**（面板对所有控件都给那两行 ·
  预览只在 CheckBox 文本路径读 · 导出只在 Dropdown / 值刷新路径读）⇒ Label 上"改了看着生效、导出没有"。
  **待产品决策**（三选一），本阶段把现状**钉进取值账**（`Fragment = null` ＋ 理由），改口径时会红。
- **负控 5 支**：面板加选项 · 删选项 · `KeyboardMode=Default` 期望片段改错 · `LineCap=Round` 改成"不发射" ·
  `TextAlign=Left` 登记成"应当发射"。读数例 `LineCap：面板/枚举给了取值 Extra，本表没登记它的导出语义` ·
  `KeyboardMode=Default：期望发射 …TEXT_UPPER…，实际没有`。
- **仍未做**：只覆盖**枚举型**字段（6 个）；数值型字段的**取值包络**仍散在各自判据里没有统一成账；
  `#96` 的三选一待决策。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2284 / 315 / 817 / 8`（合计 **3690**）
＝ **3690 通过 / 0 失败**（阶段 211 为 3687，**+3**）；`HmiEditor.Core.Tests` **2281 → 2284**；
`UnusedBaseline` 不变 1109；**本阶段产品代码 0 行改动**；`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 213（2026-10-01）—— **数值型字段的取值包络**：数字框的每个边界发射什么

**一句话**：承阶段 212 的枚举取值账（"下拉框给的每个取值"），这一阶段管"数字框给的每一段**范围**" ——
样本**必须盖住面板声明的 `Minimum`/`Maximum`**（面板改范围 ⇒ 红），并对每个样本真跑一遍生成器。

- **判据三道**（`NumericFieldEnvelopeTests`）：① 端点覆盖（读 `PropertyPanelView.axaml` 那一格的
  Min/Max，样本必须包含它们）；② 每个样本真跑生成器（登记片段逐字出现；登记 `null` 必须不发射，
  用 `CSource.ParseCalls` 判调用）；③ 面自检（字段 ≥6、样本行 ≥23）。
- **覆盖 6 字段 / 23 行**，钉住的口径：`Opacity` 越下界钳 0 · 50 ⇒ `opa(127)`（×2.55 四舍五入）·
  **100 与越上界都不发射**；`Rotation` ×10 · **不回绕**（360 ⇒ 3600）· **不钳位**（361 ⇒ 3610）；
  `Radius`/`BorderWidth` `<= 0` 不发射且**无上限钳位**；`Brightness` **原样透传不钳 255**；
  `FontSize` 无自定义字体资产 ⇒ **不发射**，有资产 ⇒ `&font_<family>_<size>`（在 `#if LV_USE_FREETYPE` 里）。
- 🩸 **新增 `#97`**：`FontSize` 的**预览/导出不一致**（预览一律按字号缩放；导出在没有自定义字体资产时
  不发射 ⇒ "14 → 30"预览变大、导出没变），与 `#96` 同族；三选一待决策。
- **负控 5 支**：面板改 `OpacityBox` 上限（`面板上界 99 没有被采样`）· `Opacity=50` 期望改 128 ·
  `Opacity=100` 登记成"应当发射" · `Brightness=256` 期望改成 255 · **代码侧**给 `Radius` 加 `Math.Min(…,100)`
  上限钳位（`Radius=999：期望 …999…，实际没有`）。🩸 nc1 第一版注入构建不过（XAML 重复 `Maximum` 属性）。
- **仍未做**：面板还有 25 个数字框没进这张账；`#97` 待决策；`Opacity` 的"越上界静默当 100%"要不要提示。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2287 / 315 / 817 / 8`（合计 **3693**）
＝ **3693 通过 / 0 失败**（阶段 212 为 3690，**+3**）；`HmiEditor.Core.Tests` **2284 → 2287**；
`UnusedBaseline` 不变 1109；**本阶段产品代码 0 行改动**；`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 214（2026-10-01）—— 数值端点账**续做**：同一张表再来 8 个字段（样本 23 → 41）

**一句话**：口径不变（样本必盖面板声明的 Min/Max ＋ 逐样本真跑生成器），把 `X`/`Y`/`Width`/`Height`/
`SelectedIndex`/`MaxLength`/`SpinSpeed`/`ScreenBgImageOpacity` 并进**同一面**。

- **新钉住**：`X`/`Y` **0 照发**（位置没有「默认即省略」）· `Width`/`Height` **0 也照发**（低于面板下界不钳）·
  `SelectedIndex` 越界不校验（由 LVGL 兜）· `MaxLength` **0 ＝ 不限 ⇒ 不发射** ·
  `SpinSpeed` `lv_spinner_set_anim_params(obj, N, 60)` · `ScreenBgImageOpacity` ×2.55（100 ⇒ 255）。
- 🩸 **钉住一处不对称**：**屏幕背景图** 100% ⇒ 照发 `opa(bg_img, 255, 0)`，而**控件 `Opacity`** 100%
  ⇒ **不发射**（阶段 213 那条）—— 两段独立实现的历史差异；两种现状都钉住，谁要统一就会红。
- 规模：字段 7 条（6 个字段）→ **15 条（14 个字段）**；样本行 **23 → 41**（测试条数不变，同一面加行）。
- **负控 5 支**：面板改 `SpinSpeedBox` 下界 · `MaxLength=0` 登记成「应当发射」 · `ScreenBgImageOpacity=100`
  改成「不发射」 · **代码侧**把 `lv_obj_set_pos` 改成「X==0 时省略」 · `Width=0` 改成「不发射」。
- 🩸 **本阶段的 docs 事故**：往登记册追加 §130 时脚本写错目标 —— **登记册整个文件被项目状态的内容覆盖**
  并已提交（`50a9061`）；发现于阶段 215（`next(... l.startswith("| 96 |"))` 抛 `StopIteration`），
  靠 `git checkout c966280 -- docs/…register.md` ＋ 重放 §130/§131 恢复。
- **仍未做**：面板还有 17 个数字框没进账（纳进来前要先查是否与阶段 203 的判据重复记账）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2287 / 315 / 817 / 8`（合计 **3693**）
＝ **3693 通过 / 0 失败**；`HmiEditor.Core.Tests` **2287**；`UnusedBaseline` 不变 1109；产品代码 0 行改动。

## 阶段 215（2026-10-01）—— 把 `#96`/`#97` 的"导不出去"**写进面板** ＋ 两份大文档的身份守卫

**一句话**：阶段 208 立的口径（"面板上有、其实不生效 ⇒ 说给用户听"）这一阶段**扩表**到四处，
把 `#96`（`TextAlign` 只有 Dropdown 写进导出）与 `#97`（字号只有配了自定义字体资产才写进导出）
也标出来；并补一条**身份守卫**盯住阶段 214 那次"登记册被项目状态覆盖"的事故形状。

- **面板**：`PropTextAlign` → `prop.text.align.export.hint`；`PropFontSize` → `prop.font.size.export.hint`
  （两键同时进 zh-CN/en-US）。判据仍是阶段 208 那条两向判据（XAML 必须挂**对应**键 ＋ 键在两本字典里
  **非空**），**表从 2 行扩到 4 行**（同一条事实不两处判）。
- 🩸 **事故与恢复（如实记）**：阶段 214 的 docs 提交里我把「往登记册追加 §130」写成
  `p.write_text(项目状态的内容 + §130)` ⇒ 登记册被覆盖且已提交（`50a9061`）。
  发现：阶段 215 想给 `#96`/`#97` 补注时 `StopIteration` ⇒ 查文件头发现抬头变成了「项目状态基线」。
  恢复：`git checkout c966280 -- docs/2026-09-17-open-items-register.md` ＋ 重放 §130/§131。
- **新判据** `DocsIdentityGuardTests`（3 条）：① 登记册必须以「未闭环项登记表」开头 ＋ 含 `| 1 |` 与
  `| 97 |` 行 ＋ 条目行 ≥90；② 项目状态必须以「项目状态基线」开头 ＋ 含最新阶段小节 ＋ 阶段小节 ≥100；
  ③ **两份文档内容不得相同**、不得以对方的抬头开头、都不得被截断（>500 KB）。
- **负控 5 支**：去掉字号的提示 · 把水平对齐挂到字号那个键上 · en-US 删键 · zh 值改空串 ·
  **把阶段 208 那格（`VsText`）的提示删掉**（证明老行仍被这张表管着）。
- **仍未做**：`#96`/`#97` 的**行为**改动仍待决策；两处提示是**静态**的（"当前工程有没有字体资产"
  这种运行时事实没有进文案）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2290 / 315 / 817 / 8`（合计 **3696**）
＝ **3696 通过 / 0 失败**（阶段 214 为 3693，**＋3** ＝ 新加的 `DocsIdentityGuardTests` 三条）；
`HmiEditor.Core.Tests` **2287 → 2290**；`UnusedBaseline` 不变 1109；
产品代码只多了两处 `ToolTip.Tip` 与两条本地化键。
`DocsIdentityGuardTests` 的负控 `.workbuddy/tmp/s215b-nc.py`（3 支）：让登记册以「项目状态基线」开头
（读数 `…以「项目状态基线」开头 —— 它被 docs/project-status.md 的内容覆盖了`）· 删掉 `| 97 |` 行
（`…里找不到最新条目行（阶段 215 时是 | 97 |）`）· 把项目状态截断（`只剩 400033 字节 —— 可能被截断`）。

## 阶段 216（2026-10-01）—— 数值端点账**清尾**：13 个字段 ＋ **死设置的新判法**（`#98`）

**一句话**：按「先完成 1」把剩下的数字框清完 —— 能进的 13 个进表；进不来的 4 个**逐条写明原因**；
五个「存了没人用」的死设置换了一种**更强的判法**（生成物逐字节相同），并登记 `#98`。

- **进来的 13 个**：`MinValue`/`MaxValue`（Slider 范围；**不校验 `min<=max`**，倒置照发）·
  `RowCount`/`ColumnCount`/`RowHeight`（Table；RowHeight=0 ⇒ 不发射）· `PipeFlowSpeed`（0 ⇒ 连注释都不写）·
  `ScreenWidth`/`ScreenHeight`（`lv_obj_set_size(screen, W, H)`，不钳位）· 五个死设置。
- 🩸 **死设置的新判法**（`ScreenDpi` ＋ 安全区四个）：它们写进 `ScreenDefaultsConfig`，但
  **全仓只有两个设置面板读写**，`CodeGen` 与预览都不读 ⇒ 判"某行不存在"是空判；
  改成**同夹具生成两次（设/不设）要求逐字节相同**，负控 nc4（把钩子接成真的动屏幕宽）当场红 ⇒ 登记 `#98`。
- **没进来的 4 个（写明原因）**：`MajorTickCount`/`TankTickCount` 的消费者在**过程控制/绘制回调**
  那条 C 代码路径（已由阶段 203 的指针表 + `ProcessControlCodeGenTests` 守着，再纳进来是**重复记账**）；
  `StepValue`/`ValueScaleFactor` 的面板下界是 **0.0001**，本面样本类型是 `int` ⇒ 要纳进来得先把样本
  改成 `double`（下一阶段的入口）；其口径先记进专题（`StepValue=1` ⇒ 21 刻度、刻度数上限 21；
  `ValueScaleFactor` 在 screen 代码里只写一行注释，真换算在值刷新路径）。
- **负控 5 支**：面板改 `MinValueBox` 下界 · `RowHeight=0` 登记成「应当发射」 · `MinValue=1000000`
  期望改成「会规范化顺序」 · **死设置钩子真的动屏幕宽** · `PipeFlowSpeed=0` 登记成「应当发射」。
- **仍未做**：`StepValue`/`ValueScaleFactor`（要把样本类型改成 `double`）· `#98` 三选一待决策。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 315 / 2290 / 817 / 8`（合计 **3696**）
＝ **3696 通过 / 0 失败**（判据条数不变：同一面样本行 41 → **65**、字段 14 → **29**）；
`UnusedBaseline` 不变 1109；**产品代码 0 行改动**；`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 217（2026-10-01）—— 样本类型 `int → double`：**数值面清空**（再没有字段走后门）

**一句话**：把数值端点面的样本类型从 `int` 改成 `double` ⇒ 连 `0.0001`（`StepValue`/`ValueScaleFactor`）
与 `0.5`（`ScreenDpi`）这样的十进制端点也能端点必采；**取消阶段 216 给死设置开的端点后门**；
再加一条闭合判据把"面板 31 个具名数字框一个不落"钉住。

- **改了什么**：`Sample.Value`/`Field.Apply`/`ScreenHook` 改 `double`（`int` 字段在 lambda 里显式
  `(int)v`）；面板范围解析改 `double.Parse(InvariantCulture)`；死设置的端点后门删除；
  收回阶段 216 推迟的 `StepValue`/`ValueScaleFactor`。
- **量到的新口径**：`StepValue=0.0001` ⇒ 21 个刻度**全标 `"0"`**（步长取整成 0；**刻度数上限 21**）·
  `=1` ⇒ 21 个刻度（0..20）· `=1000000` ⇒ **2** 个刻度；`ValueScaleFactor` 在 screen 代码里只写一行
  `// Value display: … scale=X …`（真换算在值刷新路径）。
- **新闭合判据** `EveryPanelNumericBox_HasAnOutlet`：面板里每个具名数字框都要有出口 —— 本面 **29** 个
  ＋ `Elsewhere` 登记 **2** 个（`TickCountBox`/`TankTickCountBox` 归 `ProcessControlCodeGenTests`
  的绘制回调路径）⇒ **数值面清空，一个不落**。
- 🩸 **一条教训**：nc3 第一版注入（把期望从 `_ticklbl_sl11_20,"0"` 改成 `_ticklbl_sl11_19,"0"`）
  **没咬住**（21 个刻度里 `_19` 也存在、也标 "0"）⇒ 负控的注入值必须**挑能区分该样本的那一个**，
  否则"绿"什么都证明不了。
- **仍未做**：本面只看单字段样本；字段**组合**（`MinValue>MaxValue` ＋ `StepValue` 越界）没有成对覆盖。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **943**，结构性挤行：
标记行 独立 7 / 双份 1，合计仍 9）；脚本 **29**；路径卫生 **703**；
步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2291 / 315 / 817 / 8`（合计 **3697**）＝ **3697 通过 / 0 失败**
（阶段 216 为 3696，**+1** ＝ 新闭合判据）；`HmiEditor.Core.Tests` **2290 → 2291**；
`UnusedBaseline` 不变 1109；**产品代码 0 行改动**；`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 218（2026-10-01）—— 把 `#98` 的"尚未生效"**写进面板**（提示表 4 → 9 行）

**一句话**：顺序里的第 2 项 —— 按阶段 208/215 的口径，把登记 `#98`（`ScreenDpi` ＋ 安全区四个是
"存了没人用"的死设置）也挂上悬停说明；**同一张提示表从 4 行扩到 9 行**，不新开面。

- **面板**：`ScreenDpi` → `prop.screen.dpi.export.hint`；`SafeLeft/SafeTop/SafeRight/SafeBottom` →
  `prop.screen.safearea.export.hint`（两键同时进 zh-CN/en-US）。判据仍是同一张两向表
  （XAML 挂**对应**键 ＋ 两本字典**非空**）。
- **负控 5 支**：去掉 DPI 的提示 · `SafeLeft` 挂到 DPI 那个键上 · en-US 删键 · zh 值改空串 ·
  去掉 `SafeBottom` 的提示。
- **仍未做**：`#96`/`#97`/`#98` 的**行为**改动仍待决策 —— 三处的"提示"那半现在都做完了
  （顺序里的第 2 项到此为止；再往下是第 3 项：登记册老项 `#26`/`#45`/`#27`）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2291 / 315 / 817 / 8`（合计 **3697**）
＝ **3697 通过 / 0 失败**（判据条数不变：同一张表加行）；`UnusedBaseline` 不变 1109；
产品代码只多了五处 `ToolTip.Tip` 与两条本地化键。

## 阶段 219（2026-10-01）—— 落实 `#96`「**导出补齐**」：文本类控件的文本/对齐都进导出（＋更正一次量错的结论 ＋`#99`）

**一句话**：用户拍板的三处行为决策里先做 `#96` 的**导出补齐**那半 —— 补上 `CheckBox` 的对齐与
`TextArea` 的文本/对齐；过程中**更正了阶段 212 一条量错的结论**（"Label 上 TextAlign 不发射"
其实是夹具没设 `Text`），并新登记 `#99`（TextArea 的 `Text` 从不进导出）。

- 🩸 **更正**：阶段 212 的 `TextAlign` 样本用了一个**没设 `Text`** 的 Label ⇒ 三条被钉成"不发射"，
  并据此写了 `#96`。实测：Label（有文本）/Button（子 label）/Dropdown **一直发射**；真实缺口是
  CheckBox（只设文本不设对齐）· TextArea（连文本都不发）· 面板可见性没统一。`#96` 行已按此更正，
  枚举面的夹具补上 `Text`、三条样本改成真实期望。
- **产品改动**（`CodeGenerator.Screen.cs` 文本块）：`CheckBox` 加 `text_align`；
  `TextArea` 加 `lv_textarea_set_text` ＋ `text_align`（**无文本时两者都不发**）。
- **新判据** `TextEmissionFaceTests`（16 条）：四类文本控件 × {文本,对齐} × {有无文本} ＋ 三个对齐取值
  ＋ Dropdown 抽查 ＋ 名单自检。
- **负控 5 支**：去掉 CheckBox 对齐 · 去掉 TextArea 文本 · CheckBox 对齐设到子 label · 文本用错 API ·
  门槛改恒真（`System.NullReferenceException` —— `EscapeCString(null)` 当场炸）。🩸 nc3 锚点第一版
  命中 3 次（同一行文本在三处出现）⇒ 锚点要带专属注释才唯一。
- **仍未做（顺序里的后续）**：`#96` 的**统一可见性** ＋ `#97` 的内置字号近似映射（同一阶段做）·
  `#98` 的接线。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2307 / 315 / 817 / 8`（合计 **3713**）
＝ **3713 通过 / 0 失败**（阶段 218 为 3697，**+16**）；`HmiEditor.Core.Tests` **2291 → 2307**；
`UnusedBaseline` 不变 1109；`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 220（2026-10-01）—— 字号**内置近似映射**（`#97`）＋ 面板**统一可见性**（`#96` 的另一半）

**一句话**：用户拍板的三处行为决策里第二处 `#97`（内置字号近似）与 `#96` 的"统一可见性"那半同阶段落地 ——
字号不再被静默丢掉，面板的字体/字号/对齐三行只对文本类控件显示。

- **`#97`**：新增 `BuiltInFontSizeFor`：无字体族且字号 ≠ 主题默认（14）⇒ 就近取
  `lv_font_montserrat_{12,16}`（小取 12、大取 16），用 `#if LV_FONT_MONTSERRAT_NN` 包住
  （设备没启用该档就整段消失 ⇒ 安全）；有字体族/资产走 FreeType 路、**不被顶掉**。
  ⚠ 内置只有 **{12,14,16}** 三档 ⇒ 20 会落成 16（近似，要更准得动固件侧 lv_conf）。
- **`#96` 统一可见性**：`PropFontFamily`/`PropFontSize`/`PropTextAlign`（＋ 三个标签，本阶段补名字）
  只对 **Label/Button/CheckBox/TextArea/Dropdown** 显示，其余类型隐藏（此前对所有类型都显示 ⇒
  形状类控件上改了既不进导出、预览也没效果）。
- **判据 +8**：`TextEmissionFaceTests` 追加 7 条（内置近似表 / 逐字条件编译 / 主题默认不发射 /
  有资产不被顶掉）· 新面 `PropertyPanelTextRowsVisibilityTests` 1 条（5 类可见 / 5 类隐藏，
  ⚠ 挂 `[Collection("Language-global")]` —— 门禁当场红过一次已补）。
- **负控 5 支**：内置恒取 16 · 去掉条件编译包裹 · 去掉"与主题一致"早退 · **拆两道守卫** · 面板全隐藏。
  🩸 nc4 前两版**没咬**：这条腿由**两道守卫共同保护**（调用侧 `else if` ＋ 兜底里的字体族早退），
  单拆任一道都不红，两道一起拆才咬（`生成代码中不应出现（忽略排版）: montserrat`）。
- **仍未做**：内置字号精度（固件侧）· `#98` 的**接线**（下一步）· 可见性名单硬编码五类。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2314 / 315 / 818 / 8`（合计 **3721**）
＝ **3721 通过 / 0 失败**（阶段 219 为 3713，**+8**）；`HmiEditor.Core.Tests` **2307 → 2314**；
`App.UiTests` **817 → 818**；`UnusedBaseline` 不变 1109；`MainWindow.axaml.cs` 仍 **2983**。

## 阶段 221（2026-10-01）—— 安全区 ⇒ 画布参考线：`#98` 的"接线"（先接预览）

**一句话**：用户对 `#98` 拍的是"接线" —— 这一阶段先接**风险最小且看得见效果**的一条：
**安全区 ⇒ 画布虚线参考线**（只影响显示、不动几何）；`DpiScale` 的接线单独做（要动坐标换算与命中）。

- **画布**：`SafePaddingProperty`（`Thickness`）· 公开 `SafeAreaGuides()`（画布坐标＝模型×缩放，
  脏数据归一）· `DrawSafeAreaGuides()` 虚线 · `SafeGuidesDrawnCount`（渲染期计数，供判据）。
- **宿主**：`DesignCanvasView.SetScreen(screen, project = null)` 一次到位；`MainWindow` 三处调用改为
  传 `_currentProject`（**行数不变** —— 见下）。
- 🩸 **门禁当场红过一次**：第一版在 `MainWindow` 两处各加一行 ⇒ `SingleFileSizeGuardTests`
  （基线 2983）报"治理回涨"。处置不是改基线，而是**把接线挪进 `SetScreen` 的可选参数**
  ⇒ 行数不变、接线点还从两处收敛成一处。
- **判据 5 条**：无内边距即无参考线 · 几何逐点（含**方向**判）· 脏数据归一 · **真渲染**计数 4/0
  （headless 必须用 `RenderTargetBitmap` 强制跑 `Render`）· 工程 → 画布的宿主接线（含换成无安全区的
  工程要归零）。
- **负控 5 支**：去掉绘制 · 忘乘缩放 · 去掉归一 · 画布恒空 · 宿主不读工程。
  🩸 **nc3 第一版没咬 ⇒ 暴露判据太弱**（原来用"或"判方向，反向线段也放过）⇒ 改成逐条判方向才咬。
- **仍未做**：`DpiScale` 接线（独立阶段）· `#98` 的"进导出"仍是产品决策 · 参考线样式不可配置。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2314 / 315 / 823 / 8`（合计 **3726**）
＝ **3726 通过 / 0 失败**（阶段 220 为 3721，**+5**）；`App.UiTests` **818 → 823**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 222（2026-10-01）—— `DpiScale` 也接上：有效缩放 ＝ 用户缩放 × DPI（`#98` 的后半）

**一句话**：阶段 221 只接了安全区（参考线）；这一阶段把 **`DpiScale`** 接进画布的**有效缩放**，
并让渲染与命中走**同一个**缩放（拖拽不错位）。

- **画布**：`DpiScaleProperty`（默认 1.0）＋ `EffectiveZoom = ZoomLevel × DpiScale`
  （DPI ≤ 0 按 1 处理，绝不出 `Infinity`）；三个 partial 里渲染/坐标换算的 `ZoomLevel` **25 处**
  换成 `EffectiveZoom`（保留用户缩放本身的 4 处与 Ctrl+滚轮 3 处）；新增公开 `ViewportToModel` /
  `ModelToScreen` 便于判据直接断言那条链。
- **宿主**：`ApplyProjectScreenDefaults` 顺带带上工程的 `DpiScale`（默认 1.0 ⇒ 既有工程/判据零变化）。
- **判据 7 条**（`DesignCanvasDpiScaleTests`）：默认照旧 · 乘法 · **坐标换算成对缩放**（叠平移也逐点）·
  非正 DPI 按 1 且有限 · **渲染侧几何也认它**（安全区参考线 10 → 20、790 → 1580）· 宿主接线（换工程归零）。
  两侧各钉一条的理由：只改一边的症状是"画得对、拖拽错位"，静态读代码看不出来。
- **负控 5 支**：命中侧退回 · 渲染侧退回 · 去掉非正兜底 · 宿主不传 · `EffectiveZoom` 忘乘用户缩放。
- **仍未做**：`#98` 的"**进导出**"（安全区/DPI 是否影响生成几何）仍是产品决策 · 参考线样式不可配置 ·
  DPI 目前只影响画布（预览窗口/模拟器未评估）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2314 / 315 / 830 / 8`（合计 **3733**）
＝ **3733 通过 / 0 失败**（阶段 221 为 3726，**+7**）；`App.UiTests` **823 → 830**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 223（2026-10-01）—— 门禁的**负载记账**与**诊断现场**（落实 `#45` 留下的两条规矩）

**一句话**：`#45` 的僵死那半阶段 148 已退役；这一阶段处理它**仍未闭环**的另一半 ——
那次「UI 段 141 秒」的事后无从归因 ⇒ 把"墙钟必配负载"落地到门禁，并把诊断现场验一遍。

- **落地**：`ci-gate.sh` 三处记 `负载 loadavg(开始/测试前/测试后)`；`gate-readings.py` 显示它
  （**可选**项，老日志不红）。
- **现场自检**（`HMI_DIAG_OUT` 两段读数）：小跑 6 条 ⇒ `maxBodyMs=3043（第 #10 次派发）`；
  `App.UiTests` 全量 830 条 ⇒ `maxGateWaitMs=4390（第 #205）`/`maxBodyMs=3501（第 #203）`，
  `maxInFlight=1`、`overlapEvents=0`（闸门仍压并发在 1）。两段**分得开**且都带派发序号 ⇒ 可指名到用例。
- **判据 4 条**（`GateLoadavgBookkeepingTests`）：三处标记 · 读数脚本会显示 · **端到端**合成日志跑真脚本 ·
  负载不许进必检项。🩸 ① 端到端当场抓到我的 bug（`LOADAVG.findall(text)` 变量名不存在 ⇒ NameError）；
  ② 新判据第一版用了 `Path.GetTempPath()` ⇒ `TestTempDirUsageGuardTests` 红，**连续三跑 GATE_EXIT=1**。
- **三连跑基线**：测试前负载 `8.91 / 10.47 / 15.44` ⇒ UI 段墙钟 **43 / 41 / 40 s**，全绿。
- **仍未做**：141 秒成因仍未证（现场已齐，等复发）· 阶段 59 的"面板数 12 → 3~5"没做
  （不可复现时改产品＝没有证据时动手）· 负载读数只进门禁。

**读数（工作树）**：`GATE_EXIT=0`（三连跑）/ **7 步** / 日志 **983 行**（非空 **947**）；脚本 **29**；
路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2318 / 315 / 830 / 8`（合计 **3737**）
＝ **3737 通过 / 0 失败**；`HmiEditor.Core.Tests` **2314 → 2318**；`MainWindow.axaml.cs` 仍 **2983**；
`UnusedBaseline` 不变 1109。

## 阶段 224（2026-10-01）—— 把「1 px 标签条」量清：`#26` 覆盖边界的**确切原因**（产品 0 行改动）

**一句话**：登记 `#26` 里那句「项目浏览器的标签条实测只有 1 px 高」当时只是观察；这一阶段把它量成
**可判据的事实**，并写清归档问题单原文那条"逐字方向"**为什么打不到**。

- **量到的现场**：五条 `ToolDock` 中 `LeftUpper=[ProjectExplorer]` 是**单工具** ⇒ `ToolTabStrip` 只有
  **1 px 高**、**且不创建 `ToolTabStripItem`**（多工具的是 34 px，把手共 10 个）；
  `ProjectExplorerView` 本身**附着且非空**（406×216，可视子控件 17）。
- **结论**：原文（拖**项目浏览器**到工具箱）打不到的确切原因是"**没有东西可拖**"（不是"拖不动"）；
  镜像方向（拖多工具 dock 里的面板）覆盖同一条重组路径。
- **判据 2 条**（`DockSingleToolTabStripTests`）：单工具 dock 存在且是 `ProjectExplorer` · 它**不在**把手名单 ·
  存在 1 px 标签条（多工具 ≥20 px 也钉，两边都判）· 多工具把手**在**（否则是空判）· 视图附着且有内容；
  另一条把"多工具工具集合 == 把手集合"做成 `Assert.Equal`。
- **负控 4 支**：改单工具前提（`Collection: ["Property"]`）· 去掉惰性加载排空 · **维度量错**
  （量 `Width` 得 216 而非 `Height` 的 1 —— 我第一版正是这么错的，判据当场红）· 断言反着写。
- **仍未做（产品决策）**：让单工具面板也有把手，得覆盖 Dock 的 `ToolDock`/`ToolTabStrip` 模板
  （第三方模板复制，脆弱）或给那条 dock 加第二个工具（改默认布局）—— 未擅自做。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **983 行**（非空 **947**）；负载
`开始 11.74 / 测试前 10.09 / 测试后 6.73`（阶段 223 起随日志记录）；脚本 **29**；路径卫生 **703**；
步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2318 / 315 / 832 / 8`（合计 **3739**）＝ **3739 通过 / 0 失败**
（阶段 223 为 3737，**+2**）；`App.UiTests` **830 → 832**；`MainWindow.axaml.cs` 仍 **2983**；
**产品代码 0 行改动**。

## 阶段 225（2026-10-01）—— `#26` 处置②：**给那条 dock 加第二个工具**（原文方向第一次可判）

**一句话**：用户选了处置② —— 把「资产管理」从右栏挪进左侧上区，于是**四条工具 dock 全部 ≥2 个工具、
每个面板都有可拖把手**，归档问题单**原文的逐字方向**（拖项目浏览器 → 工具箱 dock）**第一次可判**。

- **改动**：`HmiDockFactory` 里 `LeftUpperDock = [ProjectExplorer, Assets]`（原 `[ProjectExplorer]`）；
  `RightDock` 仍有 5 个工具。单工具 dock 的标签条只有 1 px 且不创建标签项（阶段 224 量清）⇒ 现在没有了。
- **判据**：① `DockSingleToolTabStripTests` 改判**新不变量**（无单工具**面板** dock ·
  `Assert.Equal(allTools, tabItems)` ⇒ 每个工具都有把手 · 标签条都 ≥20 px ·
  唯一例外**中央画布视口**显式登记并断言它是 `Canvas`）；② `DockDragRegressionUiTests` 补
  `DraggingProjectExplorerTabIntoTheToolboxDock_KeepsPanelPopulated`（**原文逐字方向**，面板仍附着/有内容/能解析/工具数 12）。
  ⇒ 判据**条数不变**（改 1 面 ＋ 补 1 条）。
- **负控 5 支**：布局改回去（不变量红 ＋ 逐字方向红 `找不到面板 'ProjectExplorer' 的标签`）·
  再制造一条单工具 dock · 去掉"画布视口"例外 · **干脆不拖**（`没有改变项目浏览器的承载 dock`）。
  🩸 nc4 第一版没咬：以为"落回自己那条 dock"不会换承载 —— 但 Dock 会**新建/切分** dock（阶段 161 已实测）。
- **仍未做**：布局选择可再调（改两处 `VisibleDockables`，判据会跟着红）· 处置①（覆盖 Dock 模板）没做、也不再需要。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **983 行**（非空 **947**）；负载 `11.85 / 11.64 / 8.80`；
脚本 **29**；路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2318 / 315 / 832 / 8`（合计 **3739**）
＝ **3739 通过 / 0 失败**；`App.UiTests` **832**（改 1 面 ＋ 补 1 条 ⇒ 总数不变）；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 226（2026-10-01）—— en-US 的**机械核对**：机器能挑的挑完 ＋ 给母语级校对的清单（`#27`）

**一句话**：`#27`（en-US 部分为精简翻译）里**机器能判**的那一半做完；逐字校对需要母语级判断，
不冒充 —— 交付"判据 ＋ 清单"。

- **判据 6 条**（`EnUsMechanicalAuditTests`，扫 **2363** 条键）：① 占位符（含 `{0:F2}`）两语一致
  （实测 **0** 条不一致 ⇒ 防新增的棘轮）② 英文不许有 CJK（**1** 条合法例外 `menu.lang.zhCN = "中文"`，
  逐条登记，登记过期也红）③ 取值不得空/空白（**0**）④ 两语取值相同的键必须逐条登记（**38** 条全部登记：
  技术名词/单位/格式模板/语言名）⑤ 「英文偏长」（≥40 且 >3× 中文）清单 **127** 条、只减不增，
  前 15 条打进测试输出 ⑥ 面自检（≥2000 条 ＋ 键集一致）。
- **交给人的清单**（专题 §2）：偏长 127 条（前 15 含 `system.issue.io_error` 5.7×、`cfg.note.*` 5.5~5.6×）·
  相同 38 条（抽查是否该翻）· 风格三项（语气/人称、大小写惯例、缩写地道性）。
- **负控 5 支**：`status.git.info` 去掉 `{0}` · `ai.model` 改中文 · 从登记表删一条 ·
  `ai.endpoint` 改空串 · 偏长基线压到 100。
- **仍未做**：逐字校对本身（术语统一、语气、大小写、缩写）—— 需母语级判断。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **983 行**（非空 **947**）；负载 `8.63 / 13.81 / 8.97`；
脚本 **29**；路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2324 / 315 / 832 / 8`（合计 **3745**）
＝ **3745 通过 / 0 失败**（阶段 225 为 3739，**+6**）；`MainWindow.axaml.cs` 仍 **2983**；
**产品/字典内容 0 行改动**；`UnusedBaseline` 不变 1109。

## 阶段 227（2026-10-01）—— 屏幕安全区 ⇒ **生成几何**：内容内缩 ＋ 控件吸附（`#98` 进导出第一半）

**一句话**：用户选"进导出"；这一阶段让 `SafeAreaPadding` 真正影响**生成物** —— 按安全区声明内容区、
把**越界控件吸附**进去（位置夹、尺寸不动），**默认值零影响**（逐字节）。DPI 的字号/尺寸换算下一阶段。

- **语义**（依据面板标签「安全区域 (异形屏避让)」）：内容区 ＝ 安全区；只夹**越界**控件的位置；
  区内控件一个字节不动；比内容区还大的贴左上；**屏幕尺寸/背景不动**；**不做整体平移**。
- **生成物**：开头 `// 安全区（异形屏避让）：内容区 (10, 20)–(770, 440)，越界控件会被吸附进来` ＋
  每次吸附一行 `// 安全区吸附：(5, 5) → (10, 20)`。
- **地基**：无工程 / 全零内边距 ⇒ 与从前**逐字节相同**，且生成物里**连"安全区"三个字都没有**。
- **判据 6 条**（`ScreenSafeAreaExportTests`）：零影响（含绝对断言）· 两方向吸附 ＋ 注释 ＋ 头注释 ·
  区内控件（位置**与尺寸**）不动 · 超大控件贴左上 · 屏幕尺寸不内缩 · 注释条数 ＝ 越界控件数。
- **负控 5 支**：吸附关掉 · 无条件发头注释 · 去掉吸附注释 · 尺寸一起夹 · 屏幕尺寸也内缩。
  🩸 两条"负控检验判据"的教训：① 「两次生成相等」对**两边都变了**是瞎的 ⇒ 补绝对断言；
  ② 只判**位置**的断言会被"改尺寸"的注入绕过 ⇒ 补尺寸断言。
- **面板提示同步**：安全区那条从"尚未生效"改成"导出现在会把越界控件吸附进安全区"；
  **DPI 那条仍写"尚未生效"**（等下一阶段）。另外 `X`/`Y` 的"首个读取点"变成新 helper
  `SafeClampPosition`（阶段 204 的消费账当场红 ⇒ 已登记进指针表与逐字段索引）。
- **仍未做**：DPI 的字号/尺寸换算 · 吸附只覆盖普通控件位置（Polyline 点 / Table 列宽 / Canvas 内绘制未做）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **983 行**（非空 **947**）；负载 `7.54 / 9.07 / 5.83`；
脚本 **29**；路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2330 / 315 / 832 / 8`（合计 **3751**）
＝ **3751 通过 / 0 失败**（阶段 226 为 3745，**+6**）；`HmiEditor.Core.Tests` **2324 → 2330**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 228（2026-10-01）—— 屏幕 DPI ⇒ **几何整体换算**：`#98` 进导出第二半 ⇒ **收口**

**一句话**：`DpiScale ≠ 1` 时把生成物里的**几何量**（坐标/尺寸/屏幕尺寸/折线点/字号）整体乘它；
**语义数值**（值比例、阈值、时长）不乘；默认 1.0 ⇒ **逐字节零影响**（既有测试套全绿就是证据）。

- **单一闸门**：新 `CDim(double)` ＝ `CNumber(value × _dpiScale)` 用于几何站点；`CNumber` 留给语义数值
  （`// Value display: … scale=2 …` 那句**不**被换算，判据钉住）。
- **字号**：内置档位与**字体资产变体名**都换算（`font_x_20` → `font_x_30`）；内置档位是"换算后再比较"。
- **注释 vs 代码**：注释坐标用**设计空间**（与编辑器一致），代码用**设备空间**——注释里写明，判据覆盖。
- **判据 10 条**（`DpiScaleExportTests`）＋ 负控 5 支。🩸 nc2 第一版构建失败（改法让变量成了未使用 ⇒
  本仓"警告即错误"）⇒ 改成"乘 1.0"。
- **`#98` 收口**：安全区（阶段 227）＋ DPI（本阶段）两半都进了导出，共 **16 条判据 ＋ 10 支负控**。
  文案也同步：面板两条提示都改成"已生效"的措辞。
- **仍未做（如实划界）**：自适应分辨率那条路径没叠加 DPI（口径未定）· 符号/表达式型尺寸
  （表格列宽 `LV_COORD_MAX`、过程图形画布绘制）没换算。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **983 行**（非空 **947**）；负载 `5.86 / 7.81 / 9.01`；
脚本 **29**；路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2340 / 315 / 832 / 8`（合计 **3761**）
＝ **3761 通过 / 0 失败**（阶段 227 为 3751，**+10**）；`HmiEditor.Core.Tests` **2330 → 2340**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 229（2026-10-01）—— 状态文本/垂直对齐的**运行时回调**：`#92` 功能那半 ⇒ **收口**（新功能）

**一句话**：面板上给非 Normal 态设的 `Text`/`TextVAlign` 以前只有一行注释（LVGL 静态样式表达不了），
现在生成 `LV_EVENT_STATE_CHANGED` 回调 —— 这些设置在设备上**真的生效**。

- **机制**：控件建好时注册；回调里 `lv_obj_get_state(obj)` 分支 ⇒ 状态 `Text` 按类型用各自 API
  （`Label`/`CheckBox`/`TextArea` 本体，`Button` 走子 label 0 ＋ 空指针保护），状态 `TextVAlign` 对
  `Label` 用「`LV_SIZE_CONTENT` ＋ 重定位」（与 Normal 态同一手法）；**必有复位分支**写回基础文本/对齐
  （否则"按下改的字"会一直留着）。**顺序**：回调定义（文件级）排在 `create_screen_*` **之前**、
  注册在其后 —— 判据钉住（C 里不能先用后定义）。
- **规则**：做不到的组合（`Slider` 的状态文本、非 Label 的状态垂直对齐）**保持注释、不注册空转回调**；
  无覆盖 ⇒ **零影响**（逐字节）。
- **判据 8 条**（`VisualStateRuntimeTextTests`）：零影响 · `TextAlign` 仍不发回调 · Label 状态文本
  （回调＋注册＋复位）· 多状态 `else if` 链 · 状态垂直对齐（重定位且该分支不换文本）·
  CheckBox/Button 各自 API · 做不到的组合不注册 · **定义在工厂前、注册在其后**。
  另把阶段 202 的两条「声明」判据升级成「注释 ＋ 注册都在」。
- **负控 5 支**：不注册 · 去掉复位分支 · 无覆盖也生成 · 状态位写错 · Button 不取子 label。
- **仍未做（如实划界）**：设备侧没跑过（判据只到生成物）· 非 Label 的状态垂直对齐仍是注释。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **983 行**（非空 **947**）；负载 `8.88 / 14.36 / 13.97`；
脚本 **29**；路径卫生 **704**（＋1 新源文件）；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 832 / 8`
（合计 **3769**）＝ **3769 通过 / 0 失败**（阶段 228 为 3761，**+8**）；`HmiEditor.Core.Tests` **2340 → 2348**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 230（2026-10-01）—— 导出侧悬停文案的**对账**：提示追上行为 ＋ 登记册旧 `⏳` 残句清理

**一句话**：用户问"待我拍板（`#96` 行为改动）是什么" ⇒ 查证结论是**登记册 `#96` 行状态列里没删的旧
`⏳` 块**（三条处置早在阶段 215/219/220 落地、本条已收口）被机械扫描读出来的。用户据此拍板：
**先修陈旧悬停文案与 `⏳` 残句，再开始登记册复核**。本阶段**不改行为**——把阶段 219/220/227/228/229
改过的行为，**说给用户听的那句话**追上去；产品代码 **0 行**（改的是字典取值）。

- **事故形状**：五条导出悬停说明停在阶段 215/218（"只在 Dropdown 上写水平对齐" / "否则沿用主题默认字体" /
  "DPI 仍未进导出" / "状态文本只留声明注释"），而行为已由 `#96`/`#97`/`#98`/`#92` 改过；
  旧判据（`Panel_MarksTheFieldsThatCannotBeExportedPerState`）只查"**键在、非空**" ⇒ 陈旧**全绿**。
- **改了什么**：五条键的值按当前生成器代码重写（zh ＋ en 共 **10** 处）；第六条
  `prop.screen.dpi.export.hint`（阶段 228 已自我更新）值不动、纳入规则表。
- **判据 1 条**（`PropertyPanelVisualStateColorTests.ExportHints_TrackTheCurrentBehavior`）：
  每条键**双向规则** —— ① 禁止旧事实的**逐字原句** ② 必含当前行为的**锚词**；**两本字典都判**；
  **面自检**（悬停表 6 个不同键 ↔ 规则表键集一一对应）；**元判据**（旧文案喂进同一检查函数必须报红）。
  绑定那半仍由阶段 208 的判据判（一条事实不两处判）。
- **负控 5 支**（`.workbuddy/tmp/s230-nc.py`）：zh 水平对齐 / **en** 字号 / zh 安全区 / zh 状态文本 /
  **只改 en** 的状态垂直对齐 ⇒ **5/5** 咬住，逐支还原 sha256 逐字节相同。
- **登记册 `⏳` 残句**：`#96`/`#97`/`#98` 三行状态列里各一段旧"待产品决策 … 三选一"块删除，原地留
  "何时删/为什么删"；`#61p`/`#61q`/`#61s`/`#61x` 的 `⏳` 是**真开着**、台账两条是覆盖缺口，**都不动**。
- **仍未做（如实划界）**：母语级校对仍归 `#27` · 这一层只覆盖这 6 条导出悬停键 ·
  提示里含 `#if LV_USE_FREETYPE` 这类实现细节（"宁可说实话"的取舍）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `5.40 / 12.84 / 9.16`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（阶段 229 为 3769，**+1**）；`App.UiTests` **832 → 833**；
`HmiEditor.Core.Tests` 仍 **2348**；`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 231（2026-10-01）—— 登记册 §2 #1–#12 **全量复核**（分批复核第一批）：3 条收口 ＋ 1 条失效关闭 ＋ 新登记 #100

**一句话**：按用户提议"像 `#26` 那样逐条拿当前代码核验原话是否还成立"，把**最老、且唯一没有状态列的**
§2「仍开着 —— 需产品决策」全表核了一遍；**产品/测试 0 行改动**，产出是账本（就地写证据）与一条真缺口。

- **结论 12 条**：8 条**仍成立**（全部就地写代码锚点，下一次复核不必重做）· 3 条**收口** ·
  1 条**失效关闭**。
  · 收口：`#6` 清理策略已落地（`AppPaths` 两层生命周期 ＋ 保守清理）· `#7` 前提已变（解包/资产目录
    都成了会话级、随进程释放）· `#8` **判过的不做**（可撞名的中转一律唯一名，需要复用的稳定目录
    刻意共享）—— 三条性质不同，不写成一句"过时了"。
  · 失效关闭：`#9`"仅 CSV、无报表模板/打印" ⇒ 模板/设计器（181/182）、PDF/xlsx（173）、自动导出
    （184~187）、可打印的自包含 HTML 全都在，原话三半全不成立。
  · 仍成立里 `#12` 等的是"明确不做"的一句话，`#4`/`#5` 等的是标注/清理口径 —— 都是产品决策。
- **新登记 `#100`（真缺口）**：早期编号**按节独立** ⇒ `#1`–`#21` 与 `#27`–`#33` 共 **28 个重复 id**
  （`#9` ＝ 临时文件名/报表能力/未下发字段 三义；`#15` 同样三义）。这正是用户那句"挑一批 `#1`–`#25`"
  无法唯一定位的原因；给了两种改法（加节前缀 / 把 §1 降格成无编号归档），等维护者定口径。
- **方法学**：成立也要写"**卡在谁身上**"的锚点；本阶段不加判据（核的是账本，不是代码）。
- **仍未做**：§1（21 条"已闭环"声明）/ §3（12 条"需先立功能"）/ §4 的 `#15`–`#39` 尚未逐条复核 ——
  按同一方法分批，下一批建议 §3（与 §2 同老、同样没有状态列）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `2.01 / 3.69 / 5.96`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230 同）；**产品/测试 0 行改动**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 232（2026-10-01）—— 登记册 §3 #9–#19 ＋ #87 **全量复核**（分批复核第二批）：0 条关闭、10 条仍成立、2 条闭环声明复核无误

**一句话**：按同一方法核 **§3「仍开着 —— 需先立功能」全表（12 行）**；**产品/测试 0 行改动**，
产出是每条的"为什么还开着"锚点 ＋ 一条**账本边界**的发现。

- **结论**：**10 条仍成立**（`#9`~`#14`、`#16`~`#19`）· **2 条闭环声明复核无误**（`#15` 表达式驱动视觉：
  `VisualBindingCodeGen` ＋ `BindingSemantics` 在仓；`#87` 自动导出：`AutoExportSchedule` /
  `AutoExportService` / `AutoExportWiring` 在仓）· **0 条关闭、0 条失效** —— 与第一批（§2：3 收口 ＋ 1 关闭）
  正好对照。如实记：**"复核"不等于"能关掉"**。
- **一条真发现（账本边界）**：`#10`（`EnableTamperDetection` 下发了但零消费）在 `ConfigFieldInventory`
  里**根本不在表内** —— 那张账的面是**编辑器 → 生成物**（发宏就算"有消费点"），而 `#10` 的缺口在
  **生成物 → 设备**。⇒ 跨过生成物边界的字段是那份"未生效"清单的盲区，已写进 `#10` 行内。
- **顺带更正旧读数**：`#19` 行内"1540 行 / 15 partial" ⇒ 实测 `SimulatorProjectExporter` **1599 行 / 5 partial**、
  `CodeGenerator.cs` **150 行 / 17 partial**（"先 P0-3"半步已完成，剩下的正是"两套实现收敛"本体）。
- **仍未做**：§1（21 条"已闭环"声明）· §4 的 `#15`–`#39` · §5 环境残留 —— 下一批建议 §1。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `2.81 / 8.91 / 7.91`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230/231 同）；**产品/测试 0 行改动**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 233（2026-10-01）—— 登记册 §1 #1–#21 **全量复核**（分批复核第三批）：21 条结论全成立、4 条读数失真已更正

**一句话**：第三批核 **§1「已闭环」全 21 行** —— 前两批核"仍开着"的原话，这一批核**闭环声明本身**
（当年的证据/读数今天还成不成立、结论有没有被后续改动推翻）。**产品/测试 0 行改动**。

- **21 条结论全部成立**；**4 条读数已失真**（就地更正）：`#1` partial **12 → 17**（且行内"12 个"
  与后面列的 16 个名字**自相矛盾**）· `#13` 调用点 30+ → **43** · `#18` `new Pen(` **2 → 3**
  （`new SolidColorBrush` 仍 0）· `#19` **4299 → 4512 行**、文件在 `Controls/` 不在 `Views/`。
  另有 `#5` 文件挪位（`Core/Services` → `src/HmiEditor.Simulator/`）、`#7` 行号漂移。
- **🩸 主要产出是 `#20` 的"结论对、证据错"**：原句"**无一读产品源码**"实测不成立 —— **91 个**
  测试文件读产品源码/用 `SourceScanner`（其中 **86** 个用 `File.ReadAllText`）；正确口径是
  "**行为判据**不靠源码 grep，**静态守卫**读源码是另一回事"，兜底 `TestAssertionStyleGuardTests`。
  ⇒ **"已闭环"的读数也会烂**；复核对象必须包括**证据句本身**。
- **仍未做**：§4 的 `#15`–`#39`（"软件侧可验证"主体、`#26` 那类"部分闭环"最集中处）与 §5 环境残留 ——
  下一批建议 §4。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `1.71 / 2.11 / 2.77`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230~232 同）；**产品/测试 0 行改动**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 234（2026-10-01）—— 登记册 §4 #15–#27c **全量复核**（分批复核第四批）：18 条仍成立、1 条收口、**4 行结构坏行修好**

**一句话**：第四批核 **§4「软件侧可验证」前半段 23 行**（状态口径最杂：已闭环 / 复核后不成立 /
部分闭环 / 仍开着 / 刻意限制）。**产品/测试 0 行改动**。

- **结论**：18 条**仍成立**（含三条"复核后不成立"的判定未被推翻）· 1 条**收口** —— `#26`（原写"部分闭环"：
  阶段 161 已把剩余两条拖拽路线补判并查到根因、**明确拒绝浮动**，阶段 224/225 修掉"单工具 dock 无把手"
  ⇒ 行尾那两项都不是缺口；回归网 **3 → 6 → 14** 条）· 2 条**仍开着**（`#85` AI 侧术语待判据、`#27` 剩
  母语级校对）· 1 条**刻意限制**（`#21`）· 1 处读数更正（`#27b` 含「工程」键 73 → **75**）。
- **🩸 真发现：§4 有 4 行 markdown 结构是坏的（已修）** —— 本批加了一道新量具：**按格解析全表**
  与表头列数对拍。`#61j`/`#89` 的代码段里**竖线未转义**（渲染时状态格被切开）；`#76`/`#78`
  **状态文字粘在「项」格里**（整行只有 2 格，`#78` 还少了行尾竖线）⇒ 按格读会以为"这条没写状态"。
  修完复扫：条目表 **0 处列数不匹配**。教训：**"看起来像一行"与"是一行"是两回事**。
- **仍未做**：§4 剩余（`#28` 起，含 `#41`–`#79`/`#86`–`#99`）与 §5 环境残留 —— 下一批建议 §4 `#28`–`#39`。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `3.48 / 10.24 / 6.76`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230~233 同）；**产品/测试 0 行改动**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 235（2026-10-01）—— 登记册 §4 #28–#39 **全量复核**（分批复核第五批）：8 条仍成立、2 条仍部分闭环、1 条需实机

**一句话**：第五批核 §4 的 `#28`–`#39` 十二行 —— 其中 `#28` 是**唯一"需实机"项**，`#29`/`#35`/`#37`/`#39`
是大口径项，其余是"拆分/守卫/工具链"类已闭环项。**产品/测试 0 行改动**。

- **✅ 仍成立（8）**：`#29` 四组版本对应判据在仓 · `#30` `SingleFileSizeGuardTests` 基线
  `MainWindow.axaml.cs = 2983`（＝实测）＋ 两个分部上限 500/700 · `#31` `PropertyPanelView*.cs` **11** 个 ·
  `#32` 5 partial ＝ **1599** 行 · `#33` `coverage.sh`/`coverage-summary.py` ＋ **8** 个项目 coverlet ·
  `#34` `DesignTokens` ＋ `DesignTokenCatalogTests` · `#36` `DeadTypeGuardTests` ＋ `Scripting` 已下架 ·
  `#38` `{StaticResource}` 键扫描 ＋ `XamlCompiledBindingGuardTests`。
- **✅ 仍成立、读数更正**：`#37`（`WindowProductionPathUiTests` ＋ `MainWindowConstructionTests` 在仓；
  `MainWindow.axaml.cs` 现 **2983** 行，行内 2370 是当时读数）。
- **⚠ 仍部分闭环**：`#35` `UnusedBaseline` **1267 → 1109**（阶段 62~65 清理）；剩"常量侧逐个判定死"＋`#52`。
- **⚠ 仍开着**：`#28`（**需实机**，本仓无法机械闭环）· `#39`（构造路径那半已闭环，剩"事件接线"第五层
  残余、宿主 17 个）。
- **方法学收敛**：五批下来复核的实际动作只有四种 —— **找锚点 / 对数 / 分半 / 认边界**；本批把四种都用上了。
- **仍未做**：§4 剩余（`#41`–`#79`、`#86`–`#99`）与 §5 环境残留 —— 下一批建议 §4 `#41`–`#79`。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `2.57 / 9.96 / 8.44`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230~234 同）；**产品/测试 0 行改动**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 236（2026-10-01）—— 登记册 §4 #41–#56 **全量复核**（分批复核第六批）：12 条仍成立、1 条收口（状态列已过期）

**一句话**：第六批核 §4 的 `#41`–`#56` 十七行。⚠ **范围比上批预告的小** —— `#41`–`#79` 一段量比预估大
（`#61`–`#61x` 一个系列就 24 行），故拆两次。**产品/测试 0 行改动**。

- **✅ 仍成立（12）**：`#41` 工具栏动作网 · `#42` `JudgeReadings` · `#44` null 守卫 · `#46` 总线唯一性守卫
  进闸 · `#48` `ReferenceFace` 三档 · `#49` M1 收紧 · `#50` `RoleTextKeys` · `#52` sync 工具默认干跑＋闸＋
  守卫 · `#53` 详情面板判据 · `#54` `CoreVisibleTextInventoryGuardTests` · `#55` `samples.Any(...)` ·
  `#56` 显式 `AvaloniaUseCompiledBindingsByDefault=true`。
- **⚠ 仍开着/部分闭环（4）**：`#43`（**小**：负控 N9 已过期；27 处视图事件层已由 `#46` 闭环）·
  `#47`（已知偶发，按纪律**不主张修好**）· `#47b`（机制与负控已实测，剩 `416` 来源未定位）·
  `#51`（判据的诚实边界 `KeptUnreachable`）。
- **🩸 真发现：`#45` 的状态列已过期** —— 它写着"已用「独占进程」规避"，而**阶段 148 就撤销了那条排除**
  （`ci-gate.sh:369-378`：本类单跑 2/2 绿、`App.UiTests` 全量含它 626/626 绿、门禁同款 solution 级 EXIT=0），
  根因疑似被 `#46`(`5cabdbb`)/`#47`(`bf35e30`) 带走，现在与大队同跑、由 `--blame-hang` 兜底 ⇒ 本批就地
  改成"收口（阶段 148 撤销排除）＋ 根因疑似被带走"（撤销排除 ≠ 证明根因修好，仍如实划界）。
- **方法学（新一类）**：前面几批量到"读数过期/证据句假/行结构坏"，这一次是**处置过期** ——
  **"当时怎么绕过去的"要单独核一遍**，因为它写在代码里（门禁脚本），最容易被后续改动悄悄搬走。
- **仍未做**：§4 的 `#58`–`#79`（含 `#61`–`#61x` 系列）、`#86`–`#99` 与 §5 —— 下一批建议 §4 `#58`–`#70`。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `3.54 / 5.19 / 4.29`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230~235 同）；**产品/测试 0 行改动**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 237（2026-10-01）—— 登记册 §4 #58–#61j **全量复核**（分批复核第七批）：10 条仍成立、`#58` 环境前提已变

**一句话**：第七批核 §4 的 `#58`–`#61` ＋ `#61a`–`#61j`（14 行，`#61` 子系列做到 j）。**产品/测试 0 行改动**。

- **✅ 仍成立（10）**：`#59`（包码现 9 个）· `#60`（`FailureDetail(key, targetPath, ex)` 三实参）·
  `#61b`（9 站全闭，键只剩历史注释）· `#61c`（三分类收窄为 A/C）· `#61d` · `#61e`（`SystemFailureCodes`
  7 码）· `#61g`（`GeneratedCodeIssueCodes.*` 在用）· `#61h`（`VariableImportView:112`）· `#61i`
  （`ExprSyntaxKeyPrefix`）· `#61j`（`CopyAll` 走 `FindingDisplayText.CopyLine`）。
- **⚠ 部分闭环（1）**：`#61a`（两条路径判据在仓，剩余归 `#61f`）；**仍开着（2）**：`#61`（直通面基线
  现 **6 站**）· `#61f`（四条 A 类产出方的「返回值带码」）。
- **🩸 真发现：`#58` 的环境前提变了（新的一类）** —— 它写着"本沙箱 `id -u=0` ⇒ root 绕过权限 ⇒ 不可复现"；
  实测本会话 `id -u` = **1000**，`chmod 000` 后读**失败** ⇒ 成因不再成立，边界④这半**可以试着补判据**了。
  本批只改状态列为"前提已变、需重测"，**不擅自补判据**（"做不出负控的判据不发布"是 `#58` 自己的口径）。
  ⇒ 方法学：**"环境事实"也会过期**（§149 读数 / §149`#20` 证据句 / §150 结构 / §152 处置 / 本批＝
  复核者脚下的沙箱身份）。
- **顺带**：修掉上一批在 `#61j` 留下的重复措辞；按格解析复扫条目表 **0 处列数不匹配**。
- **仍未做**：`#61k`–`#61x`（14 行）· `#62`–`#79` · `#86`–`#99` · §5 —— 下一批建议 §4 `#61k`–`#61x`。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `1.66 / 6.53 / 5.94`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230~236 同）；**产品/测试 0 行改动**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 238（2026-10-01）—— 登记册 §4 #61k–#61x **全量复核**（分批复核第八批）：`#61` 子系列收完、3 条收口

**一句话**：第八批把 **`#61` 子系列收完**（`#61k`–`#61x`，14 行）。**产品/测试 0 行改动**。

- **✅ 仍成立（8）**：`#61l` `DanglingRefLocation` · `#61m` `ReferenceTextKeys`/`ActionTextKeys` ·
  `#61n` `Describe` 唯一分派 · `#61o`（`ReferenceRow` 返回**本地化字符串** ⇒ 直显枚举名确已消除）·
  `#61r` `ControlNameDefaults` · `#61u` `PostPanelRebuild` · `#61v` `LoginIssue`/`LoginResult` ·
  `#61w` 台账 **14 通道 / 94 条**。
- **⏳ 仍开着（3）**：`#61k`（判过的不做，`MessageNoKey` 双向表守）· `#61p`（`ExpressionVariable` 仍零产出）·
  `#61s`（M6 语料口径未定）。
- **⇒ 收口（3）**：`#61q`（本批复测 **索引器 848 / 下界 700**、`.Format` **310 / 250** ⇒ "贴边"消失）·
  `#61t`、`#61x`（见下）。
- **🩸 真发现：长状态列的"头部"才是被扫到的那一句** —— `#61t` 头部写"部分闭环"、尾部写"**本行整项闭环**"；
  `#61x` 头部写"仍开着"、尾部含"**✅ 已闭环（阶段 104）**"且面已升格零容忍。两行的闭环都补记在**尾部**
  而头部没改 ⇒ 扫表读到的是"仍开着"。与 §152 `#45`（处置过期）同族，这次过期的是**同一格里的位置**：
  状态格越长，"头部＝结论、尾部＝历史"的隐含约定越容易破。
- **仍未做**：`#62`–`#79`（约 18 行）· `#86`–`#99`（14 行）· §5 —— 下一批建议 §4 `#62`–`#79`。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `5.73 / 5.29 / 4.74`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230~237 同）；**产品/测试 0 行改动**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 239（2026-10-01）—— 登记册 §4 #62–#70 **全量复核**（分批复核第九批）：7 条仍成立、2 条收口

**一句话**：第九批核 §4 的 `#62`–`#70`（9 行）。**产品/测试 0 行改动**。

- **✅ 仍成立（7）**：`#62` `HmiPackageException` 带 inner 重载 · `#63` `Mechanisms_AreDeclaredInAllThreePlaces` ·
  `#64` `IIdentifiedModel` · `#65`（`ChkShow*` ×4 ＋ 6 条按钮订阅 ＝ 10 处）· `#68` `_isUpdating` 守卫 ·
  `#69` `ViewAxamlFiles()` 带 `AllDirectories` · `#70` `EvaluateCondition` 就地读。
- **⇒ 收口（2）**：`#66`（② 由**阶段 124** 落地：`OnDetailChanged` 接上并立即落盘＋写审计，
  `UserProfileEditUiTests` ＋ `UpdateUserDisplayName`；只剩 ① 9 枚 `Perm*` 刻意不接）· `#67`
  （② 「表外值 ⇒ 自定义（N ms）」已落地；① 抑噪守卫已补，且**代码注释里就写着**复核结论
  「#67①：今天无可观察差异，补守卫是为一致性」）。
- **🩸 真发现：闭环记在"别的章节 / 代码里"，登记行没更新** —— `#66②` 收在 §46（阶段 124）、
  `#67①` 只收在**代码注释**里，而 §4 两行仍分别写"2 条待决"/"仍开着 · 需先定产品口径"。
  与 §152（处置过期）、§154（头部过期）同族，根子是 **"闭环记在哪儿"没有约定**。
  ⇒ 复核纪律再加一条：**除状态列外，还要查"别的章节/代码里有没有替它收口"**（本批正是靠这条收掉两行）。
- **仍未做**：`#71`–`#79`（9 行）· `#86`–`#99`（14 行）· §5 —— 下一批建议 §4 `#71`–`#79`。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `2.63 / 8.00 / 6.00`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230~238 同）；**产品/测试 0 行改动**；
`MainWindow.axaml.cs` 仍 **2983**；`UnusedBaseline` 不变 1109。

## 阶段 240（2026-10-01）—— 判据**按提交哈希 flaky** 的根因与修法（第九批的门禁当场挡下，登记 #101）

**一句话**：第九批（`#62`–`#70`）的 docs 写好后跑门禁得 `GATE_EXIT=1` —— `Core.Tests` 2348 项里红 1 条，
消息是「生成代码里出现了非法 C 浮点常量：**580599f**」。`580599f` 是**上一阶段的提交哈希** ⇒
与 docs 改动无关，是一颗早就埋着、被"换了个提交"撞响的雷。

- **根因链**：① 生成物头部有**追溯戳** `HmiEditor.CodeGen <版本> (git <短哈希>)`
  （`CodeGenerator.GeneratorStamp`，故意留着便于现场反查）；② 那条浮点判据扫的是**生成物原文**（注释也算）；
  ③ 短哈希字符集含 `f` ⇒ 哈希末段为「数字 ＋ f」时被正则当成非法字面量。⚠ 判据**自检**（`legal >= 8`）
  与扫描同源，**发现不了**假阳。
- **修法**：不改生成物头（那是取证手段），而是统一**输入口径** —— 新增 `CSource.WithoutTrivia(code)`
  （复用 `TrySkipTrivia`：注释 ＋ 字符串/字符字面量换成空格），判据改扫它；与既有「注释不算代码」同一口径。
- **负控 3 支**：nc1 旧写法 ＋ 哈希形状的戳 ⇒ **红**（证明红的就是它）· nc2 新写法 ＋ 代码里真有 `1f` ⇒
  **仍红**（证明没放松判据）· pc1 正控 ⇒ **绿**；逐支还原 `sha256` 逐字节相同。
- **同类面粗筛**：`tests/` 里 30 个文件用 `Regex`，14 处扫 `code`/`gen`/`text`；逐条看与"能被哈希命中的
  宽松形态"无关 ⇒ **不另立新项**，但要守纪律：**扫生成物的判据要么走 `WithoutTrivia`，要么写明为什么不用**。
- **仍未做**：把这条纪律做成守卫（要先定"哪些判据算扫生成物"）；第九批剩余 `#71`–`#79`/`#86`–`#99`/§5。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `2.63 / 8.00 / 6.00`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**；本阶段**产品代码 0 行**（改的是测试与支持类）。

## 阶段 241（2026-10-01）—— 登记册 §4 #71–#79 **全量复核**（分批复核第十批）：§4 全表核完，4 行"头部过期"就地修好

**一句话**：第十批核 §4 的 `#71`–`#79`（9 行）⇒ **§4 的表全部核完**（`#15`–`#79`）。**产品/测试 0 行改动**。

- **✅ 已完成 / 仍成立（4）**：`#74`（阶段 136 闭环）· `#76`（**撤回结论**成立）· `#78`（阶段 156 闭环）·
  `#79`（阶段 162 闭环：`TryRestoreClosedTool:493`）。
- **⇒ 收口（4）**：`#71`/`#73`（阶段 137 用户拍板**删掉**：文件 0 命中、`Unreachable` 账本空）·
  `#75`（阶段 138 改键名与文案：`Lang.WinDesignCanvas`）· `#77`（阶段 144/145/146 三条指针腿 ⇒ **整项闭环**）。
- **⚠ 仍部分闭环（1）**：`#72`（`StaticBusHygiene` 机制已建，**宿主侧隔离仍未兑现**）。
- **🩸 本批的处置：把"头部过期"从发现变成修好** —— §154 第一次量到 2 行，本批同形状又命中 **4 行**
  （`#71`/`#73`/`#75`/`#77`）⇒ 不是个别现象（§4 后期状态格越长，"头部＝结论、尾部＝历史"越容易破）。
  已**就地改写这四行头部**为 `✅ **已闭环（阶段 XXX）** —— …`，原头部文字原样保留在后面（历史/证据不删）。
  顺带修掉 `#72` 尾部一处**失效指针**（「或 `#76` 补守卫」——`#76` 是误诊并撤回的那条）。
- **仍未做**：`#86`–`#99`（14 行）与 §5（环境/本地残留 8 行）—— 下一批建议 §4 `#86`–`#99`。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `4.59 / 9.80 / 6.84`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230~240 同）；**产品/测试 0 行改动**。

## 阶段 242（2026-10-01）—— 登记册 §4 #88–#99 **全量复核**（分批复核第十一批）：§4 逐行核完，补回两条"没有行"的条目

**一句话**：第十一批核 §4 的 `#88`–`#99`（12 行）⇒ **§4 的表逐行核完**（`#15`–`#101`）。
**产品/测试 0 行改动**。

- **✅ 仍成立 / 已完成（8）**：`#88` `SettingsTextBoxWiringTests`（7 框 9 手势）· `#89` `ScriptEditorView._isUpdating`
  窗口 · `#90` `FANumberBox` 支（`ProjectSettingsView.axaml.cs:787`）· `#91` `Normal` 三行 ＋ `SyncContentBox`
  （`:311-318`）· `#93` `lv_keyboard_set_mode`（`ControlParameters.cs:481`）· `#94` `DeviceManagerInputReadFaceTests`
  （13 框台账清零）· `#95` `IsNullOrWhiteSpace` 回退（`DownloadToHmiView:308`）· `#99` `TextEmissionFaceTests` 16 条。
- **⇒ 收口（4）**：`#92`（**头部过期**：阶段 229 的功能那半已做完）· `#96`/`#97`/`#98`（阶段 219/220/227/228
  落地，阶段 230 已核）。
- **🩸 真发现（新一类）：表里"少行"** —— 按"§4 的 id 是否齐全"对拍，量出 **`#57` 与 `#86` 在 §4 表里根本没有行**：
  `#57`（位置定位控件）只活在 §34、`#86`（`DataLogger.ExportCsv()` 零调用）只活在 §91 章内的小表里
  ⇒ 按表扫会**整条漏掉**。本批把两条**补回 §4 表**并注明原位置；另两个缺号 `#40`/`#87` 各有归属
  （§5 / §3），**不是缺陷**。
- **顺带**：承接 §157，把 `#92`/`#96`/`#97`/`#98` 四行的过期头部改成 `✅ 已闭环（阶段 XXX） —— …`。
- **仍未做**：**只剩 §5**（环境/本地残留 8 行）—— 性质不同（多是本沙箱的**环境事实**），核法要改成
  "当前会话还成不成立"。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `2.92 / 7.00 / 6.23`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230~241 同）；**产品/测试 0 行改动**。

## 阶段 243（2026-10-01）—— 登记册 §5 **全表复核**（第十二批，最后一批）⇒ **§1–§5 全表核完**

**一句话**：最后一批核 §5「环境 / 本地残留」（8 行）—— 性质不同，核法改成"**当前会话还成不成立**"：
**该跑的跑**（`du` / `ls` / `df` / `touch` 试探 / 门禁日志 grep），**该拒绝的拒绝**（不去跑 `rm -rf`）。
**产品/测试 0 行改动**。

- **✅ 仍成立（3）**：`#27`（门禁与验收只认 Release 的政策未变；Debug 全量并发**未复测**）· `#28`（`.dbg`
  实测 **209 MB**）· `#29`（家目录两个残留都在，`MockHmiAssets` **22 MB**）；**闭环无误（1）**：`#32`
  （探针文件与 `.gitignore` 规则都无）；**未复测（1）**：`#30`（沙箱删除守卫 —— 复测有破坏性）。
- **⇒ 收口（3）**：`#31`（**前提已变**：`ci-gate.sh:270` 自己把 `NUGET_HTTP_CACHE_PATH` 指到工作区 ⇒
  本轮多次门禁 `NU1900` **零命中**；行内"该变量在门禁里无效"已过期，只读挂载仍在但不在门禁路径上）·
  `#33`（**前提已变**：`/tmp` 现 **32 GB** tmpfs，不再 10 MB）· `#40`（`Children[1]` 已修，
  `CreateLuaHelpDialog` ＋ `LuaHelpDialogUiTests`；门禁两级守卫实测**读取 0 / 写入 0**）。
- **十二批复核收口**：查到的"过期"一共 **8 类** —— 读数 / 证据句 / 行结构 / 处置 / 头部 / **环境前提** /
  编号冲突（`#100`）/ **缺行**（`#57`/`#86`）。方法论最终形态：**找锚点 / 对数 / 分半 / 认边界 / 解结构 / 重跑环境**。
- **复核 ≠ 清零**：仍开着的是产品决策、判据的诚实边界（`#51`/`#61k`/`#61s`）、需实机（`#28`）、
  部分闭环（`#35`/`#39`/`#72`）与账本结构（`#100`）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `2.46 / 10.64 / 7.00`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230~242 同）；**产品/测试 0 行改动**。

## 阶段 244（2026-10-01）—— `#35` 常量侧「逐个判定死」的**收口论证**（十二批之后第一件"清仍开着"）

**一句话**：复核结束后从"仍开着"清单挑第一条。`#35` 行写"仍开着：① 常量侧仍未『逐个判定死』"。
本阶段**不改代码**：证明 **① 已由判据做完**，并把头部改成闭环口径（`②` 的 `#52` 早已在批⑤闭环）。

- **论证（三条断言 ⇒ 不存在"悄悄死掉"的常量）**：① `LangKeyCoverageTests`：每个常量的值都在两本字典里；
  ② `LocalizationKeyReachabilityGuardTests`：每个字典键**要么被机制取到、要么在 `KeptUnreachable` 里带理由**；
  ③ `UnusedConstants_ClassifiedByKeyMechanism`（`LangConstantUsageGuardTests:328`）：1109 个无引用常量**按担保机制分桶**
  ＋ **双向交叉核对**（「无任何机制」集合 **≡** `KeptUnreachableKeys`，`SequenceEqual`），另有断言"例外仍不可达且仍在字典里"。
  ⇒ 对任一常量：要么键由活机制担保，要么被逐条登记为例外，**没有第三种**。
- **复测读数**：无引用常量 **1109** · 字典键 **2363**（有常量 1869 / 无常量 494）· 分桶 **M2 832 / M6 254 / M3 18 / 无机制 5**（＝1109）。
- **为什么不是"把待办说成已办"**：判据给的是**逐条归桶 ＋ 双向集合相等**，不是"数量在降"；前 1104 个的键活着
  （常量是键空间镜像，删掉会被 `LangKeyCoverageTests`/补键工具要回来），后 5 个是带理由的例外 ⇒
  基线 1109 的正确语义是**棘轮**、不是清理清单（行内已有这半句，头部没跟着改，本批一并改）。
- **下一项**：`#72`（宿主侧隔离）或 `#100`（编号体系，需维护者定口径）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `1.28 / 1.40 / 2.09`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230~243 同）；**产品/测试 0 行改动**。

## 阶段 245（2026-10-01）—— `#72` **收口**："宿主侧尚未兑现"是**更正前的旧结论**（🩸「更正记在隔壁」）

**一句话**：复核清单里第二条。`#72` 行尾写着"机制已建、**宿主侧尚未兑现**……仍只能用一致性探针"。
本阶段核查：**这句话不成立** —— 它停在阶段 139 中途的误判上，而更正只写进了隔壁 `#76` 与代码注释。
**产品/测试 0 行改动**。

- **两半都已兑现**：① **订阅**（阶段 134）：`StaticBusHygiene.Silence()` 每条宿主跑前清掉陈旧订阅
  （`HostButtonEffectUiTests.cs:4787`），三条腿由 `Never` → `Moves`；② **数据**（阶段 139）：
  `ResetSharedData()` 把 **8 个静态属性全清**（`SharedDataPolicy` 逐个分类、没分类就抛），
  `FaceplateLibraryView` 宿主**三条断言恢复字面名**（`:3135-3160`、`§61.3` 兑现表）。
  ⚠ 两条一致性探针**保留是对的**（判的是"载荷随选中"，与字面断言是两件事）。
- **🩸 新形态：「更正记在隔壁」** —— 阶段 139 中途的"全清会崩/挂"被收尾复测**推翻**（全 8 清 ＋ 字面断言
  ⇒ 全量连跑 3 次全绿 624/31·31·33 s），更正写进了 `#76`"已撤回"、`StaticBusHygiene` 类注释与 `§61.4`……
  **唯独 `#72` 自己没改**。与 §155（闭环记在别的章节）同族，但被"寄存在别处"的是**对另一行的撤销**。
  本会话旁证：十余次门禁（每次含 `HostButtonEffectUiTests` 全套）连续全绿。
- **下一项**：`#100`（编号体系，需你定口径：① 加节前缀 / ② §1 降格成无编号归档）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `4.97 / 4.69 / 5.57`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 833 / 8`
（合计 **3770**）＝ **3770 通过 / 0 失败**（与阶段 230~244 同）；**产品/测试 0 行改动**。

## 阶段 246（2026-10-01）—— 登记册**编号口径落地**（用户拍板选项①，闭环 `#100`）＋ 新守卫 `RegisterNumberingGuardTests`

**一句话**：把"按节独立"的早期编号改成**带节前缀**并同步引用，并给这条口径立守卫
（含"裸竖线切开格子"那类结构病）。**产品代码 0 行**（改的是登记册 ＋ 一个测试类）。

- **改了什么**：§1（21 行）→ `§1-#1`…；§2（12）→ `§2-#1`…；§3（9–19，11 行）→ `§3-#9`…（`87` 保持裸号）；
  §5（27–33，7 行）→ `§5-#27`…（`40` 保持裸号）；**§4 不动**（全局裸号 `#15`…`#101`）。
  ⇒ **裸写 `#X` 一律指 §4**，口径写进册首「编号口径」块。
- **引用同步**：显式 `§N #X` **20 处** ＋ 册内按上下文判定的裸引用 **18 处**；`DocsIdentityGuardTests` 的
  「第 1 行」锚点改成节前缀行。残余：跨文档裸引用粗筛 **261 处**（多为误报或本就指 §4，未逐条清理 ⇒ §162.5）。
- **守卫 `RegisterNumberingGuardTests`（4 条）**：全册 id 唯一 · §4 裸号 · 前缀必须是本表前缀 ·
  **行格数必须与表头一致**（§150 那类结构病）；＋ 面自检。**负控 5/5**：去前缀 / 造重复号 / 错前缀 /
  删表标题 / **塞裸竖线** —— 🩸 最后这支不是假想：本阶段写 `#100` 状态格时**亲手写进一个裸竖线**，
  被新判据当场抓住才补的第 ④ 条。
- **仍未做**：跨文档裸引用逐条判定（语义活，不能用脚本一把梭）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `2.97 / 9.15 / 5.89`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2352 / 315 / 833 / 8`（合计 **3774**）＝ **3774 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2348 → 2352**。

## 阶段 247（2026-10-01）—— `#39` **残余面实测**（只测量、不改产品）：构造路径无残余；按钮名字级残余 50 格 / 42 名字 / 15 视图

**一句话**：把 `#39` 的残余面**量出来** —— 跑三层行为面守卫 ＋ 选择族静态守卫 ＋ `scripts/face-residual.py`，
读数经 `HMI_READINGS_OUT` 落盘（不手抄）。**产品/测试 0 行改动**。

- **构造路径那一半：无残余** —— `WindowProductionPathUiTests` **33** 条数据行 × `WindowDepot`（逐窗口读
  可视/具名控件、空字段 5→3、形状签名变化…）＋ `WindowViewSmokeUiTests` **32** 个窗口。
- **事件接线那一半（当前读数）**：第三层（对话框内部）6 宿主 / **46** 条声明（StyleEditor 0：整面在跳过表里）·
  第二层（停靠面板）**22** 个按钮（Popup 2 / Silent 16 / Broken 0 / Never 4）· 第五层 **23 宿主 / 330 条**
  （Moves 199 / Inert 65 / Never 66）· 选择族面内 **23 宿主 / 82 控件 / 108 条**，**面外仅 `PropertyPanelView`**
  （1 视图 / 31 控件，阶段 118 判不扩）；扫描面 45 个 `.axaml`（Views/ 递归）＋ Views/ 外 3 个。
- **Button 名字级残余**：面 329 格 / 254 名字 / 45 视图 ⇒ **残余 50 格 / 42 名字 / 15 视图**，覆盖 **83.5%**。
  逐按钮点名核对后最"干净"的候选：**`AiChatPanelView`（8 个按钮全部无人点名）** · `TableCellEditorDialog`（3/3）·
  `AuditLogView`（2/4）。
- **🩸 三条口径纪律**：① 引数字带来源工具名（两个"面外"不同口径，曾巧合同为 `63`）；② **残余个数 ≠ 缺口大小**
  （`PropertyPanelView` 残余最多但判定不扩）；③ 名字级只能排优先级，说"判过"要回声明表点名核对。
- **仍未做**：扩不扩要逐条读按钮处理器才定；下一刀建议 `TableCellEditorDialog`（3 按钮）或 `AiChatPanelView`（8 按钮）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `1.77 / 2.87 / 3.27`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2352 / 315 / 833 / 8`（合计 **3774**）＝ **3774 通过 / 0 失败**
（与阶段 246 同）；**产品/测试 0 行改动**。

## 阶段 248（2026-10-01）—— `#39` 残余候选的**处理器逐条读 ＋ 去留判定**（只读不写产品）

**一句话**：承接阶段 247 的残余清单，把两条"最干净"的候选（`TableCellEditorDialog` 3 按钮 · `AiChatPanelView` 8 按钮）
**逐条读处理器**，回答"进第五层还是判过的不做"。本阶段**不改代码**。

- **`AiChatPanelView` ⇒ 可进第五层**（**7 判 ＋ 3 Never**）：`PreviewBtn`/`ApplyBtn`/`CancelBtn` 各抬一个事件
  （并转发 `DockLayoutState`）= Moves；`NewChatBtn` 清 `_messages` = Moves；`AgentModeToggle` **无处理器** = Inert；
  `SaveConfigBtn`（写 LLM 配置）/`SendBtn`（async 真发请求）/`StopBtn`（需在途、默认隐藏）记 **Never** 并写理由。
  ⚠ 可行性关键：第五层的 `WindowDepot` **已持有** `Ai = new AiPreviewManager()`（`WindowDepot.cs:62`）⇒ 无需新假件。
- **`TableCellEditorDialog` ⇒ 可进第三层**（`DialogInnerInvocationUiTests`）：`OkBtn` 写模型两字段
  （`HeaderTexts` ＋ `Cells`）= Moves · `CancelBtn` 关闭且模型不动 = Inert · `InsertVarBtn` 只写 `_focused.Text` =
  窗口控件面；宿主路径是产品里现成的 `PropertyPanelView.EditCellsBtn → ShowDialog`（`PropertyPanelView.Parameters.cs:263`）。
  代价：夹具要造 **Table 控件**（`RowCount`/`ColumnCount`/`HeaderTexts`/`Cells`）。
- **`PropertyPanelView` 仍是"判过的不做"（第五层口径）** —— 若因上一条把它加进**第三层**，那是另一口径，不算推翻。
- 其余 12 个残余视图**本批未读**；**下一刀建议 `AiChatPanelView`**（收益/成本比最好）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `5.98 / 6.07 / 7.34`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 315 / 2352 / 833 / 8`（合计 **3774**）＝ **3774 通过 / 0 失败**
（与阶段 246/247 同）；**产品/测试 0 行改动**。

## 阶段 249（2026-10-01）—— 第五层**第 24 个宿主 `AiChatPanelView`**（`#39` 残余面实装第一格）

**一句话**：把阶段 247/248 量出并判定为"可进第五层"的 `AiChatPanelView` 装进去 ——
**8 条声明（4 Moves / 3 Inert / 1 Never）**，负控 **4/4**，并顺手修掉一段**过期散文献**。

- **判定与实测**：`SendBtn`（布景输入框 ⇒ 未配置分支本地加两条消息）会话条数 **0→2**、`SaveConfigBtn`
  （ApiKey 留空 ⇒ 不落盘）抬 `ConfigChanged`、`CancelBtn` 抬 `CancelRequested`、`NewChatBtn` 清空消息
  **2→0**（4 条 Moves）；`PreviewBtn`/`ApplyBtn` 因 `PendingScreen` 是 `private set`（造不出前置状态）记
  **Inert**、`AgentModeToggle` 无处理器记 **Inert**、`StopBtn`（`IsVisible=False` ＋ 无在途）记 **Never**。
- **🩸 §164 的预测要更正**：它把 `PreviewBtn`/`ApplyBtn` 记成 Moves（只读"事件会不会抬"、漏了首行前置判空）
  ⇒ 实装后只能是 Inert。**条数一致（7 判 ＋ 1 Never）、桶不同** —— 又一次"先量后写"的活证据。
- **读数**：`宿主按钮效果/表` 宿主 **23 → 24**、按钮 **330 → 338**（Moves 203 / Inert 68 / Never 67）、带布景 25；
  `face-residual.py`（自检 3/3）残余 **50 → 38 格 / 42 → 34 名字 / 15 → 14 视图**、覆盖 **83.5% → 86.6%**。
- **顺手修好过期散文献**：类注释里"十七个宿主 / 8 个非宿主视图 / 48 个接线控件 / 面内 66 / 面外 48"
  一路没跟着改 ⇒ 换成今天的实测数（面内 23 宿主 / 82 控件、面外 1 视图 / 31 控件）＋"这是散文读数、没有判据看着"。
- **负控 4/4**（`.workbuddy/tmp/s249-nc.py`，逐支还原 sha256 逐字节相同）：摘布景 ⇒ Moves 空转红 ·
  少声明"输入长度" ⇒ 未声明分量外溢红 · 删 `StopBtn` 的 Never ⇒ 面守卫红 · 期望改 m2 ⇒ 声明值不符红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `6.64 / 7.29 / 7.06`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2352 / 315 / 834 / 8`（合计 **3775**）＝ **3775 通过 / 0 失败**；
`HmiEditor.App.UiTests` **833 → 834**（＋1 个宿主 theory）。

## 阶段 250（2026-10-01）—— `TableCellEditorDialog` 的按钮判据（`#39` 残余面第 2 格）＋ 探针语料修正

**一句话**：给它立**专题判据类** `TableCellEditorDialogUiTests`（5 条：1 面 ＋ 4 腿），并把该判据收进
`face-residual.py` 的语料 —— 否则"判过了却还被记成残余"。负控 **4/4**。

- **🩸 §164 的「进第三层」不成立**：`DialogInnerInvocationUiTests` 的宿主定义是「从**主窗口菜单**打开的
  `Window`」，而本对话框由**主窗口内嵌面板** `PropertyPanelView.EditCellsBtn → ShowDialog(owner)` 打开
  ⇒ 塞不进去 ⇒ 按同一口径**另立判据类**（走生产构造路径 ＋ `Show()`）。
- **5 条判据与实测**：面（具名 Button 恰好 `CancelBtn`/`InsertVarBtn`/`OkBtn` 双向差集）· `InsertVarBtn`
  （① 无焦点 ⇒ 空操作；② 聚焦后**只有那一格**写成 `{v1}`、模型不动）· `OkBtn`（第 0 行 ⇒ `HeaderTexts`
  `温度,压力`→**`温度,压力,备注`**；第 1 行起 ⇒ `Cells` 1→**2**：(1,0)=静态文本 / (1,1)={v1}；空串跳过；关闭）·
  `OkBtn` 截尾（`温度,压力`→**`温度`**）· `CancelBtn`（模型**一字不变** ＋ 关闭）。
- **探针语料修正**：`face-residual.py` 原先只扫五层 ⇒ 收进本判据后口径成"五层 ＋ 专题对话框判据"，
  并顺手修掉文件头三个**过期数字**（307/244/43、63/55/20、16 文件/63 控件 ⇒ **329/254/45、36/32/13、1/31**）。
  ⇒ 残余 **38 → 36 格 / 34 → 32 名字 / 14 → 13 视图**；覆盖 **86.6% → 87.4%**（自检 3/3 仍全过）。
- **负控 4/4**（`.workbuddy/tmp/s250-nc.py`，两个产品文件逐支还原 sha256 逐字节相同）：摘 `OkBtn` 的
  `Click=` ⇒ 写回腿红 · `CancelBtn` 偷写模型 ⇒ 红 · `InsertVarBtn` 空操作 ⇒ 红 · 加一个具名按钮 ⇒ 面红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `14.21 / 13.74 / 9.99`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2352 / 315 / 839 / 8`（合计 **3780**）＝ **3780 通过 / 0 失败**；
`HmiEditor.App.UiTests` **834 → 839**（＋5）。

## 阶段 251（2026-10-01）—— 第五层第 25 个宿主 `AuditLogView` ＋ 🩸 查明「第四层面的**前缀盲区**」

**一句话**：把 12 个停靠面板逐个过了前缀 ⇒ 发现第四层（`DockPanelInvocationUiTests`）的面**按 `Btn*` 前缀筛**，
有 **6 个**面板视图的动作控件不在它面内；本阶段补上 `AuditLogView`（**第 25 个宿主**）并立守卫把这份清单钉住。

- **🩸 盲区（12 个面板实测）**：非 `Btn*` 动作控件只在 6 个面板视图里 —— `DesignCanvasView` 5 ·
  `CrossReferenceView` 1 · `AiChatPanelView` 8 · `AuditLogView` 4 · `PropertyPanelView` 10 · `ToolboxView` 1。
  前三个已由第五层托底；`PropertyPanelView` 判过不做；**`ToolboxView.ClearSearchButton` 仍是真缺口**（下一刀）。
- **第 25 个宿主 `AuditLogView`**（4 条声明）：`ExportBundleBtn` **Inert**（空日志 ⇒ `SaveBundleAsync` 首行
  `rowCount == 0` 提前返回，不碰文件夹选择器）· `RefreshBtn` **Moves**（布景往沙箱写一条 JSONL ＋
  `LoadAuditLogFromFile` —— **只进内存、不抬 `AuditLogged`** ⇒ 日志条数 **0→1**）· `ExportReportBtn`/`DesignerBtn`
  **Never**（弹 `ReportRangeDialog`/`ReportDesignerDialog`，模态等用户）。
  🩸 布景**不能**用 `RecordAudit`：那会抬事件、面板自己就把条目加进去 ⇒ 等于「把效果搬进布景」。
- **新守卫** `DockPanelsOutsideTheBtnPrefixedFace_MustBeHostedOrExplicitlyWaived`：4 个视图必须在宿主表里、
  2 个豁免（Property/Toolbox）写进清单，**反向也判**（豁免项若已在宿主表里 ⇒ 红）。
- **读数**：宿主 **24 → 25**；按钮 **338 → 342**（Moves 204 / Inert 69 / Never 69）；带布景 26；
  名字级残余 **36 → 32 格 / 32 → 28 名字 / 13 → 12 视图**、覆盖 **87.4% → 89.0%**。
- **负控 4/4**（`.workbuddy/tmp/s251-nc.py`，逐支还原 sha256 逐字节相同）：摘布景 ⇒ Moves 空转红 ·
  `ExportBundleBtn` 写错名 ⇒ 面红 · 期望值改成 2 ⇒ 红 · 盲区守卫记错 ⇒ 红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **945**）；负载 `7.73 / 7.73 / 5.69`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2352 / 315 / 841 / 8`（合计 **3782**）＝ **3782 通过 / 0 失败**；
`HmiEditor.App.UiTests` **839 → 841**（＋2）。

## 阶段 252（2026-10-01）—— `ToolboxView` 搜索／清除**专题判据**（`#39` 残余面最后一块「两层都够不到」收口）

**一句话**：第四层按 `Btn*` 前缀筛、第五层又要求每宿主 ≥3 条声明（它只有 1 个按钮）⇒ 唯一落点是**专题判据**。
立 `ToolboxSearchClearUiTests`（3 条），并把该判据收进 `face-residual.py` 语料。负控 **4/4**。

- **3 条判据**：面（具名 Button 恰好 `ClearSearchButton`）· 搜索方向（写字 ⇒ 过滤 26→1 ＋ 按钮显形）·
  点它（框清空 ＋ 事件到达后列表恢复 **26/26** ＋ 按钮自己隐藏）。
- **🩸 一并量出的驱动纪律**：程序化写 `TextBox.Text` **不触发** `TextChanged` —— 本判据在同一 headless 会话用
  **裸 `TextBox`** 实测：程序化 `set = 0` / `Clear() = 0` / 手动补发 = **1**（与阶段 187 同结论）⇒ 驱动必须走
  `ControlWiringFace.Write`（写文本 ＋ 补发）。第一版判据就栽在这里。
  ⚠ 由此划界：`OnClearSearchClick` 只做 `SearchBox.Clear()`，「列表恢复」那一半依赖框架抬事件 ⇒ 本判据
  **分开断言**（`Clear()` 必判、「恢复」补发后再判），**不把 headless 这一档当产品缺陷**（同 `#40` 纪律）。
- **§167.1 清单全部收口**：4 个视图进第五层 · `PropertyPanelView` 判过不做 · `ToolboxView` 进专题判据
  ⇒ **不再有「两层都够不到」的动作控件**。
- **读数**：残余 **32 → 31 格 / 28 → 27 名字 / 12 → 11 视图**，覆盖 **89.0% → 89.4%**（探针自检 3/3）。
- **负控 4/4**（`.workbuddy/tmp/s252-nc.py`，两个产品文件逐支还原 sha256 逐字节相同）：摘 `Click=` ⇒ 红 ·
  处理器空操作 ⇒ 红 · 按钮恒隐藏 ⇒ 红 · 筛选恒不生效 ⇒ 红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `6.80 / 12.86 / 10.23`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2352 / 315 / 844 / 8`（合计 **3785**）＝ **3785 通过 / 0 失败**；
`HmiEditor.App.UiTests` **841 → 844**（＋3）。

## 阶段 253（2026-10-01）—— `AiIconDialog` 按钮面**专题判据**（`#39` 残余面里最大的一块）

**一句话**：它由**停靠面板**的 `BtnAiIcon` 打开（第四层只判"弹没弹"）、又进不了第三层
（那层要求宿主是"从主菜单打开的窗口"）⇒ 第三张专题判据。**4 条判据 ＋ 8 个控件的声明表**，负控 **4/4**。

- **8 个具名动作控件的桶**：`ImageModeBtn`/`IconModeBtn`/`CancelBtn` = **Moves**（模式切换：面板可见性 ＋
  Toggle 选中态 ＋ 窗口标题 ＋ 清空状态行；⚠「初始点 `IconModeBtn` = 提前 return」也如实判）·
  `SaveSvgBtn`/`SavePngBtn`/`AddToLibraryBtn` = **Inert**（首行判 `_pendingImageSvg` ⇒ 无结果时提前 return；
  真保存走平台桩）· `GenerateBtn` = **Never**（效果已由 `AiIconDialogInputReadFaceTests` 用假生成源判过，
  不重复记账）· `BtnTitleClose` = **Never**（与 Cancel 同后果）。
- **实测**：图片模式：`IconPanel` 可见 **False** / `ImagePanel` **True**、`Title=`AI 生成图片``、状态行清空；
  取消：`Closed` 触发 **1** 次、`IsVisible=False`、`Results 0`。
- **探针语料**：收录本判据后残余 **31 → 23 格 / 27 → 21 名字 / 11 → 8 视图**，覆盖 **89.4% → 91.7%**。
  ⚠ 掉的**不是** 7 格 —— 名字级口径下**别处同名的** `CancelBtn`/`GenerateBtn`/`BtnTitleClose` 一起消失
  （"补一个判据掉几格" ≠ "那个视图有几格"，已写进探针文件头）。
- **负控 4/4**（`.workbuddy/tmp/s253-nc.py`，产品文件逐支还原 sha256 逐字节相同）：`OnImageModeClicked` 空操作 ⇒ 红 ·
  标题不写 ⇒ 红 · `OnCancelClicked` 提前 return ⇒ 红 · 面板可见性写反 ⇒ 红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `6.91 / 9.60 / 7.17`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2352 / 315 / 848 / 8`（合计 **3789**）＝ **3789 通过 / 0 失败**；
`HmiEditor.App.UiTests` **844 → 848**（＋4）。

## 阶段 254（2026-10-01）—— `VariableImportView` 按钮面专题判据（5 条）＋ 🩸 **为判据加的一条注入缝**（产品侧）

**一句话**：它由**项目树配置节点** `variable_import` 打开（第三/四层都够不到）⇒ 第四张专题判据。
但这一张**动了产品代码**：原先 `_selectedFilePath` 只能由平台选择器写入，headless 下解析/导入两条腿**原理上不可达**。

- **🩸 加缝**：给 `VariableImportView` 加 **internal 注入缝** `Func<Task<string?>>`（把"平台选择器"这一步换掉；
  **传 null 与加缝前逐字相同**），与阶段 209 给 `AiIconDialog` 加 `IIconGenerationSource` 同处置。
  ⚠ `internal` ⇒ 不多出公共构造、不惊动 `WindowProductionPathUiTests` 的「双路径」元检查。
  缝只替掉**平台**那一步；`ApplySelectedFile(path)` 是两条路径共用的产品代码。
- **5 条判据与实测**：面（5 个具名 Button 双向差集）· `BtnSelectFile`（缝给 CSV ⇒ `FilePathText`=`vars.csv`、
  `BtnParse.IsEnabled` False→**True**）· `BtnParse`（预览 0→**2**、`BtnImport.IsEnabled`→True、**模型仍 0**）·
  `BtnImport`（模型 1→**2**、同名「温度」**跳过**、原有 `Address` 不被覆盖、导完自己关掉）· `BtnCancel`（`Closed` 1 次）。
  `BtnTitleClose` = Never（与 Cancel 同后果）。
- **探针语料**：收录本判据 ⇒ 残余 **23 → 20 格 / 21 → 18 名字 / 8 → 7 视图**，覆盖 **91.7% → 92.9%**（自检 3/3）。
- **负控 4/4**（`.workbuddy/tmp/s254-nc.py`，产品文件逐支还原 sha256 逐字节相同）：缝不落界面 ⇒ 红 ·
  解析不填预览 ⇒ 红 · 导入首行 return ⇒ 红 · 摘 `BtnImport` 接线 ⇒ 红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `8.62 / 12.08 / 10.59`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2352 / 315 / 853 / 8`（合计 **3794**）＝ **3794 通过 / 0 失败**；
`HmiEditor.App.UiTests` **848 → 853**（＋5）。

## 阶段 255（2026-10-01）—— `ValidationResultView` 按钮面专题判据（3 条）—— **第一次判剪贴板那条出口**

**一句话**：它由「生成代码 → 验证」链在 `MainWindow` 里 `ShowModalDialog(...)` 打开 ⇒ 第五张专题判据。
与既有判据分工清楚：`FindingDisplayTextGuardTests` 判**文案**（不点按钮、不读剪贴板），本判据判**点下去真的发生什么**。

- **3 条判据与实测**：面（3 个具名 Button 双向差集）· `BtnCopyAll`（提示行 → 字典 `finding.copied` ＝
  `已复制到剪贴板`；**剪贴板文本 88 字符 / 2 行**）· `BtnClose`（`Closed` 1 次、`IsVisible=false`）。
  `BtnTitleClose` = Never（与 Close 同后果）。期望文本用**产品自己的** `FindingDisplayText.CopyLine` 合成（同源）。
- **🩸 两条 API 细节**（都实测）：① Avalonia 12 读剪贴板是 `ClipboardExtensions.TryGetTextAsync()`
  （在 `Avalonia.Input.Platform` 命名空间；`IClipboard` 上**没有** `GetTextAsync`）；② `CopyAll` 的 `SetTextAsync`
  是 **fire-and-forget** ⇒ 读剪贴板必须**轮询等待**，且**不能阻塞式等待**（headless 同线程会死锁）。
- **探针语料**：收录本判据 ⇒ 残余 **20 → 16 格 / 18 → 16 名字 / 7 → 6 视图**，覆盖 **92.9% → 93.7%**（自检 3/3）。
  ⚠ `BtnClose` 多个对话框同名 ⇒ 一格判据清掉多格（名字级口径）。
- **负控 4/4**（`.workbuddy/tmp/s255-nc.py`，产品文件逐支还原 sha256 逐字节相同）：摘 `BtnCopyAll` 接线 ⇒ 红 ·
  不写提示行 ⇒ 红 · 不交给剪贴板 ⇒ 红 · 摘 `BtnClose` 接线 ⇒ 红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `5.82 / 6.56 / 5.58`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2352 / 315 / 856 / 8`（合计 **3797**）＝ **3797 通过 / 0 失败**；
`HmiEditor.App.UiTests` **853 → 856**（＋3）。

## 阶段 256（2026-10-01）—— `ReportColumnDialog`（报表选列）按钮面专题判据（5 条）—— 判的是**返回值契约**

**一句话**：它是导出报表链弹的第二个模态窗（`ReportExport.ChooseColumnsAsync` → `ShowDialog<...>`）
⇒ 第六张专题判据。这个对话框对调用方的**契约就是返回值**（`Close(SelectedKeys())` vs `Close(null)`），
所以两条结果腿**与生产路径逐字同形**走 `ShowDialog`，断言**返回值本身**。

- **5 条判据与实测**：面（5 个具名 Button 双向差集）· `BtnSelectNone`（勾选 **0/3**、`SelectedKeys()` 空、
  **`BtnOk.IsEnabled` True→False** —— 联动）· `BtnSelectAll`（勾选 3/3、`SelectedKeys()` = `time, value, unit`，
  顺序沿用原表、`BtnOk` 恢复）· **OK ⇒ `ShowDialog` 返回 3 个键** · **取消 ⇒ 返回 `null`**
  （⚠ `null` 与「空集」必须可区分 —— 调用方判的就是 `keys is null ⇒ 取消`）。
  `BtnTitleClose` = Never（与 Cancel 同后果）。
- **探针语料**：收录本判据 ⇒ 残余 **16 → 14 格 / 16 → 14 名字 / 6 → 5 视图**，覆盖 **93.7% → 94.5%**（自检 3/3）。
- **负控 4/4**（`.workbuddy/tmp/s256-nc.py`，产品文件逐支还原 sha256 逐字节相同）：`SetAll` 空操作 ⇒ 红 ·
  去掉 `BtnOk` 的「至少一列」联动 ⇒ 红 · OK 返回 `null` ⇒ 红 · 取消返回勾选结果 ⇒ 红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `5.66 / 4.62 / 3.98`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2352 / 315 / 861 / 8`（合计 **3802**）＝ **3802 通过 / 0 失败**；
`HmiEditor.App.UiTests` **856 → 861**（＋5）。

## 阶段 257（2026-10-01）—— `LoginDialog` 按钮面专题判据（5 条）—— 用**沙箱里的真认证服务**判那条链

**一句话**：它由**应用启动链**打开的模态窗 ⇒ 第七张专题判据。与既有判据分工清楚：
`LoginIssueLocalizationGuardTests` 是**源码扫描面**（不点按钮），本判据判**点 `BtnLogin` 真的发生什么**。
⚠ 用沙箱里**真的** `AuthenticationService`（`CreateUser` 造用户、**不 mock 服务**）。

- **5 条判据与实测**：面（3 个具名 Button 双向差集）· `BtnLogin` **纯空白** ⇒ 早退：错误行
  `请输入用户名和密码`、`SessionId=null`、窗不关（`#61t` 对齐过的门槛）· `BtnLogin` **密码错** ⇒
  错误行 `用户名或密码错误`（按 `LoginIssue` 的**码**分派，与「没填」那档不同）· `BtnLogin` **正确凭据** ⇒
  `SessionId` 有值、`Closed` 1 次、窗关 · `BtnCancel` ⇒ `Closed` 1 次。`BtnTitleClose` = Never。
- **探针语料**：收录本判据 ⇒ 残余 **14 → 13 格 / 14 → 13 名字 / 5 → 4 视图**，覆盖 **94.5% → 94.9%**（自检 3/3）。
- **负控 4/4**（`.workbuddy/tmp/s257-nc.py`，产品文件逐支还原 sha256 逐字节相同）：去掉空白早退档 ⇒ 红 ·
  失败档不写提示行 ⇒ 红 · 成功档不记 `SessionId` ⇒ 红 · 摘 `BtnLogin` 接线 ⇒ 红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `5.03 / 10.13 / 9.36`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2352 / 315 / 866 / 8`（合计 **3807**）＝ **3807 通过 / 0 失败**；
`HmiEditor.App.UiTests` **861 → 866**（＋5）。

## 阶段 258（2026-10-01）—— `FontSubsetView` ＋ `FaceplateTemplateEditorView` 按钮面（一次收两格）—— 残余**只剩两格**

**一句话**：两张专题判据（第八/第九次），其中 `FontSubsetView` **又加了一条注入缝**（第三次加缝：§170/§254/本节）。
残余从 **13 格 / 4 视图** 降到 **11 格 / 2 视图**、覆盖 **95.7%**。

- **🩸 `FontSubsetView` 加缝**：`ExportCharset()` 的落点来自 `SaveFilePickerAsync`，headless 桩返回 null ⇒
  `if (file == null) return;` 早退 ⇒ **「导出的文件里写了什么」原理上够不到**。加 **internal 缝**
  （`Func<Task<string?>>`，替掉「平台另存为」；传 null 与加缝前逐字相同）＋ 抽出 `WriteCharset(path)`
  让两条路径共用。⇒ 实测：沙箱文件 **16 字节 / 4 个字符**（每字符一行、UTF-8 无 BOM），内容与
  `FontSubsetAnalyzer.Analyze` **同源**；提示行 `` → `已导出 4 个字符 → charset.txt`。
- **`FontSubsetView`（3 按钮）**：`BtnExportCharset` Moves · `BtnClose` Moves（`Closed` 1 次）· `BtnTitleClose` Never。
- **`FaceplateTemplateEditorView`（5 按钮）**：`BtnAddParam` Moves（2→3，**并自动选中**）·
  `BtnRemoveParam` **Inert ＋ Moves**（裸点 no-op；加参数后 3→2 —— **顺序即语义**）· `BtnSave` Moves
  （参数同步回模板 2→3 ＋ **版本 3→4** ＋ `Result` ＋ 关窗）· `BtnCancel` Moves（`Result=null` ＋ 关窗）·
  `BtnTitleClose` Never。
- **探针语料**：收录两个判据 ⇒ 残余 **13 → 11 格 / 13 → 11 名字 / 4 → 2 视图**，覆盖 **94.9% → 95.7%**（自检 3/3）。
  **只剩** `PropertyPanelView`（10，判过的不做）与 `BuildRunDialog.BtnStop`（1）。
- **负控 4/4**（`.workbuddy/tmp/s258-nc.py`，两个产品文件逐支还原 sha256 逐字节相同）：缝不落盘 ⇒ 红 ·
  写文件不更新提示行 ⇒ 红 · 加参数不选中 ⇒ 红 · 保存不同步参数 ⇒ 红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `9.72 / 7.93 / 5.67`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2352 / 315 / 873 / 8`（合计 **3814**）＝ **3814 通过 / 0 失败**；
`HmiEditor.App.UiTests` **866 → 873**（＋7）。

## 阶段 259（2026-10-01）—— `BuildRunDialog` 按钮面（3 条）—— 🏁 **`#39` 残余面收口：只剩「判过的不做」那一格**

**一句话**：最后一格（第十张专题判据）。这个窗的 ctor 挂着 `Loaded += ... RunBuildAndSimulatorAsync()`
⇒ **一显示就真跑构建**（生成代码 → CMake → 编译器 ＋ 杀 `hmi_sim` 进程）⇒ 判据必须**只构造、不 `Show()`**
（与 `LuaHelpDialogUiTests` 同处置：未 Show 也能 `RaiseEvent` 驱动 XAML 挂上的处理器）。

- **3 条判据**：面（3 个具名 Button 双向差集）· **只构造不 Show**（`BtnStop`/`BtnClose` 都 `IsEnabled=False`
  ＋ 工作目录没被创建 ⇒ 「没显示就没跑构建」，这条读数同时是**自检**）· `BtnClose`（`Closed` 1 次）。
- **`BtnStop` 如实记 Never**：语义是「有在途模拟器进程 ⇒ `Kill(entireProcessTree)`，否则 `_cts.Cancel()`」，
  而「在途」只能由**真构建**产生（一显示就跑）⇒ 本判据不点它，只把**面闭合到它**。
  `BtnTitleClose` 同样 Never（与 `BtnClose` 同后果）。
- **🏁 收口**：残余 **11 → 10 格 / 11 → 10 名字 / 2 → 1 视图**，覆盖 **95.7% → 96.1%**（自检 3/3）。
  **唯一剩下的是 `PropertyPanelView`（10 格）** —— 阶段 118 判定**不扩**（数据面已由 `PropertyPanelWiringTests`
  判过、写入是批量的）。⇒ 探针能看到的 **254 个名字里 244 个有落点**，余下 10 个是**有意不覆盖**的一格。
- **负控 3/3**（`.workbuddy/tmp/s259-nc.py`，产品文件逐支还原 sha256 逐字节相同）：摘 `BtnClose` 接线 ⇒ 红 ·
  `BtnStop` 初值改成可用 ⇒ 红 · 加一个具名按钮 ⇒ 面红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `14.62 / 11.74 / 13.61`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2352 / 315 / 876 / 8`（合计 **3817**）＝ **3817 通过 / 0 失败**；
`HmiEditor.App.UiTests` **873 → 876**（＋3）。

## 阶段 260（2026-10-01）—— 两次战役的**方法论收成**：新增专题手册（`#39` 残余面收口后）

**一句话**：阶段 230~246 的**登记册十二批复核** ＋ 247~259 的**行为面残余收口**（86.6% → 96.1%），
两条线的流程与陷阱散在一百多个阶段记录里 ⇒ 收成一篇可复用的手册
**`docs/2026-10-01-face-coverage-and-register-review-playbook.md`**。本阶段**产品/测试 0 行改动**。

- **战役一（复核）**：六步法（找锚点/对数/分半/认边界/解结构/重跑环境）· **八类失真** ＋ 衍生的
  「更正记在隔壁」· 处置纪律三条。
- **战役二（面覆盖）**：三层结构 · **第四层 `Btn*` 前缀盲区**（6 个面板视图 / 29 个控件在它面外）·
  口径纪律三条（引数字带工具名 · 残余≠缺口 · 名字级只排优先级）· **判据形状库**（八种模板，含注入缝的判准）·
  **陷阱清单**（八条，每条都付过一次门禁红）· 常用命令 ＋ 负控脚本三条纪律 · 最终账（244/254 = 96.1%）。
- **边界**：这套方法**测不出**什么（产品的"该不该"、真机、判据自身口径、以及**首跑绿 ≠ 判据有效**）。
- **为什么单独成篇**：账是**结论**，手册是**方法** —— 查"某条结论还成立吗"翻 §146~§175；
  问"遇到一张新面该怎么判"看这本手册。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `3.42 / 4.21 / 5.39`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2352 / 315 / 876 / 8`（合计 **3817**）＝ **3817 通过 / 0 失败**；
**产品/测试 0 行改动**。


## 阶段 261（2026-10-01）—— **用户拍板记录与处置**：封账 4 件 · 发布门槛 4 件 · 立项 2 件 · 量化 1 件

**一句话**：把「需要拍板」的条目一次清完 —— 收口 4 条（`#39`/`#51`/`#61k`/`#85`）· 4 条改为**发布前必须做**
门槛（`#27`/`#28`/`#47`/`#47b`）· 2 条**立项**（`§2-#11` ISA-18.2 全量、`§2-#12` 远程访问/Web）·
1 条**量化**（`§2-#10` 长期归档 ⇒ 出容量量化表）。**产品/测试 0 行改动**。

- **封账**：`#39` 收口（残余面 244/254 ＝ 96.1%，唯一剩下的 `PropertyPanelView` 是「判过的不做」）·
  `#51`（插件兜底键：判据的不完备，明写在 `KeptUnreachable`）· `#61k`（产物注释回显，翻译它等于改写产物）·
  `#85`（AI 侧提示词刻意不本地化）。
- **发布门槛**：新增 **§0.1 发布前必须做** —— 对标出「机械可判的那半」与「只能人工/实机的半」
  （`#27` 母语者校对 · `#28` 实机回归 · `#47`+`#47b` 间歇红根因）。
- **立项**：`§2-#11`（ISA-18.2 全量：字段 ＋ 联动工作流 ＋ 报表）· `§2-#12`（远程访问/Web：撤销「明确不做」）
  —— 两条的方案文档排下一阶段。
- **量化（`§2-#10`）**：出 `docs/2026-10-01-historian-capacity-worksheet.md` —— 公式（**行数 vs 值数**）＋
  四个档 ＋ 三方案匹配度 ＋ 待补的现场参数表 ＋ 选型后可立的四条判据面。
  🩸 **含一次自查**：第一版把「值数」当行数乘行宽 ⇒ 容量放大 N 倍、结论反了（把「大现场可行」误判成「不可行」）；
  脚本重算后更正为 **13 MB / 1.06 GB / 41 GB / 1.62 TB**，真正的上界是 512 通道 × 100 ms × 1 年那一档。
- **仍未拍板**：`§2-#1`/`§2-#2`/`§2-#3`/`§2-#4`/`§2-#5`（选项已在对话里给出）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `5.48 / 10.51 / 7.32`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2352 / 315 / 876 / 8`（合计 **3817**）＝ **3817 通过 / 0 失败**；
**产品/测试 0 行改动**。


## 阶段 262（2026-10-01）—— `#61s` 落地：M6 的「收窄口径」变成**可复跑的读数 ＋ 负控**（1 条新判据）

**一句话**：按用户拍板（**判定口径不变**：认所有非注释字面量；**给收窄加一条负控**）落地 —— 
`SourceCorpus(stripComments: true, narrowToExecutable: true)` ＋ 行级近似的 `MaskAssertionMessageLiterals`
＋ 新判据 `M6Face_JudgedCorpusVersusNarrowedCorpus_IsReported`（4 条断言）。

- **实测（这就是本阶段的价值）**：残差 371 / M6 面 **判定 366 ＝ 收窄 366 ⇒ 收窄在残差上的代价 0** ——
  今天**没有任何「只靠断言消息活着」的残差键**，收窄口径**不改变任何判定**（M6 只起"减少误报"作用）。
- **唯一失去的键是假见证**：`sim.running.stats` 由 M1 真覆盖（`SimulatorView.axaml.cs:559` 引用
  `Lang.SimRunningStats`），那处断言字面量只是重复提及。
- **旧见证的边界被钉住**：`login.issue.bad_credentials`（阶段 97 NC-5 / 阶段 99 NC4）在收窄语料里
  **仍找得到** —— 它的断言消息**跨行**，行级近似抓不到；这条边界写成**会响的断言**，收窄一变精确就红。
- **负控 3/3**（`.workbuddy/tmp/s262-nc.py`，三个文件逐支还原 sha256 逐字节相同）：掩码空操作 ⇒ 红 ·
  摘掉假见证字面量 ⇒ 红（`Assert.Contains` 失败）· 把跨行消息并到 `Assert.` 行 ⇒ 红（收窄变精确）。

**读数（工作树，第 2 跑）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `4.84 / 5.47 / 4.68`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2352 / 315 / 877 / 8`（合计 **3818**）＝ **3818 通过 / 0 失败**；
`HmiEditor.App.UiTests` **876 → 877**（＋1）。

**⚠ 第 1 跑是红的（已知偶发 `#47`，双读数都记）**：`XamlResourceKeyClosureTests.PhantomKeyVictimButtons_ActuallyCarryTheirBackground` 死在 **headless 平台初始化**（现场 `dispatch#72, sessionThread=33, callingThread=23, ambientUIThread=ownerThread=30（非会话线程＝偷手）`，`hash=46908529`）；按 `#47` 自带的判定纪律 —— 本阶段改动面（`LocalizationKeyReachabilityGuardTests.cs` ＋ docs）与 `Dispatcher.UIThread`/`get_CurrentDispatcher` 那两个 getter **交集为空** ⇒ 可直接判为**既存偶发**，复跑取绿（本行读数即第 2 跑）。红日志留档 `.dbg/gate-s262-red1.log`。


## 阶段 263（2026-10-01）—— `§2-#12` 远程访问/Web：**立项与选型提案**（＋ 更正一条失真证据句）

**一句话**：用户拍板"立项"后出的方案文档 `docs/2026-10-01-remote-access-proposal.md`；
出方案前的盘查顺手抓到一条**证据句失真**（本册第 4 次）。本阶段**产品/测试 0 行改动**。

- **🩸 更正**：原行写「全仓 grep 0 命中任何 HTTP/WebSocket/远程通路」，实测设备侧**早有 MQTT 通路** ——
  `mqtt_client.c`（3.1.1 客户端，QoS0/32 主题/RX 512 B/重连 5 s）· `variable_table.c:453` **变量变化上行**
  （~1 s 扫描、值变化才发、主题 `<prefix>/val/<变量>`、payload `%g`）· `comm_core.c` 运行时下发
  `comm_config.ini` · OTA 走 HTTP · 编辑器侧 `MqttDriver.cs`。⇒ 缺的是**远程端 / 契约 / 安全 / 双向写与审计**，
  不是"没有通路"。
- **提案要点**：能力盘点（上行/认证与角色/审计/升级四块可复用）· 三档架构（A 复用 MQTT 上行＋远程订阅端
  **推荐** · B 设备侧 HTTP/WS 网关 · C 云与多站点）· 安全模型五行（传输/授权/认证/审计/失效，逐行带判据面）·
  六个里程碑（逐条带判据面）· 待拍板四问（只读还是可写 · 局域网还是云 · TLS 终结在哪 · 远程端形态）。
- **第一刀定为 M1**（主题/payload 契约 ＋ 两端解析器）：三档都要 · 今天就能机械判 · 顺手补上
  "payload 只有 `%g`（无时间戳/质量位）"的缺口 ⇒ 排进下一阶段落地。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **982 行**（非空 **946**）；负载 `3.26 / 11.76 / 8.12`；
脚本 **29**；路径卫生 **704**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2352 / 315 / 877 / 8`（合计 **3818**）＝ **3818 通过 / 0 失败**；
**产品/测试 0 行改动**。


## 阶段 264（2026-10-01）—— `§2-#12` 的 **M1 落地**：主题/payload **契约 v1** ＋ 跨语言同源样例判据

**一句话**：远程访问任何档都要的第一块 —— 契约。设备侧 `mqtt_contract.{h,c}`（纯函数）＋ 上行发布接入
（`variable_table.c` 不再就地拼串，并给 payload 补上**时间戳/时间基/质量位**）＋ C# 侧对偶实现
（`MqttValueContract`：格式化 ＋ 解析）＋ **跨语言同源样例守卫**（C# 判据直接读 C 测试文件的样例表逐字段比对）。

- **契约 v1**：主题 `<prefix>/val/<变量名>`（prefix 可含 `/`，变量名不许含 `/`）；
  payload `v=<值>;t=<毫秒>;tb=<utc|boot>;q=<0|1>`；值走 `%.6g` 口径（C# 用 `G6` + `E→e` 归一）。
  🩸 **`tb=` 必须写进契约**：设备无 RTC 只有开机时长（`CLOCK_MONOTONIC`），不声明时间基远程端会把
  "开机 3 秒"当成 1970/2026 —— 与 `DataLogger.cs:190-199` 的 CSV 时间基问题是同一个坑。
- **三条返回约定**：`0` 成功 / `-1` 参数非法 / `-2` 容量不足且**一个字节都不写**（哨兵判据钉住）。
- **兼容**：C# 解析器把改造前的**裸数值**（`%g`）判成 `IsLegacy`（能读、但明确"没有时间戳"）；未知键忽略。
- **实测**：C host **9/9 步全过**（新增契约用例）· C# 侧 **20 条全过** · 同源样例 **5 条逐字段相等** ·
  **负控 4/4**（C 侧样例改数 ⇒ 红 · 时间基 token 空 ⇒ 红 · C# 侧表改字段 ⇒ 同源守卫红 · 容量不足写半截 ⇒ 哨兵红）。
- **顺手**：`JudgeReadings`（`#42` 的读数出口）从 `App.UiTests` 搬进 `TestSupport`（三个测试程序集一份实现），
  否则 Core.Tests 要用只能抄第二份；`JudgeReadingsTests` 7 条仍全过。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1027 行**（非空 **991**）；负载 `5.86 / 7.60 / 5.78`；
脚本 **29**；路径卫生 **707**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2372 / 315 / 877 / 8`（合计 **3838**）＝ **3838 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2352 → 2372**（＋20）。


## 阶段 265（2026-10-01）—— `§2-#11` ISA-18.2：**落地方案与三段切片**（＋ 更正一条口径）

**一句话**：用户拍板"ISA-18.2 全量"后出的方案文档 `docs/2026-10-01-isa18-alarm-model-proposal.md`；
出方案前的盘查更正了本行的一句口径（本册「证据句失真」类第 5 次）。本阶段**产品/测试 0 行改动**。

- **🩸 更正**：原行"没有…也没有联动工作流模型"的**后半句不成立** —— `AlarmEngine.cs:10-14` 已实现
  四态迁移（`Inactive → Active → Acknowledged → Returned` ＋ 两条确认路径），`AcknowledgeAlarm`
  只允许在 `Active` 上调；设备侧 `CodeGenerator.Alarm.cs:176-185` 同款 state 0/1/2/3。
- **真缺口四件**：㊀ 职责字段（原因/响应/后果）· ㊁ 优先级与升级 · ㊂ 搁置/抑制 · ㊃ KPI。
- **三段切片**：㊀（**建议先做**）模型可空三字段 ＋ **两处消费**（面板写回 ＋ 新增主报警表导出
  `AlarmPhilosophyReport`），判据四面包（JSON 往返 / 报表逐字节含空值策略 / 面板字段面 / 旧工程兼容）；
  ㊁ `AlarmPriority` 纯函数（优先级 ≠ 严重度）＋ **不改生成协议**；㊂ 状态迁移矩阵 ＋ KPI 纯函数 ＋ 进报告框架。
- **迁移**：三字段可空 ⇒ 旧工程 JSON 直接可读（无需迁移脚本）；空值策略 = 留空列、不写 `null`/`N/A`。
- **待拍板三条**：职责字段必填与否 · 优先级方向与 severity 映射 · 搁置是否要权限＋审计（建议都要）。
- **估算**：㊀ 1.5–2 人日 · ㊁ 2 人日 · ㊂ 3–4 人日 ⇒ 切片 ㊀ 排下一阶段落地。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1027 行**（非空 **991**）；负载 `3.96 / 5.75 / 5.31`；
脚本 **29**；路径卫生 **707**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2372 / 315 / 877 / 8`（合计 **3838**）＝ **3838 通过 / 0 失败**；
**产品/测试 0 行改动**。


## 阶段 266（2026-10-01）—— `§2-#11` **切片㊀ 落地**：ISA-18.2 职责字段 ＋ 主报警表导出 ＋ 面板三框

**一句话**：按阶段 265 的方案落第一片 —— 模型加可空三字段、**两处消费**都接上（面板 ＋ 新报表），
四个判据面全覆盖，负控 4/4。

- **模型**：`AlarmConfig.Cause/Response/Consequence`（`string?`，可空 ⇒ 老工程直接可读）。
- **消费①**：`AlarmConfigView` 三个输入框（`TxtAlarmCause`/`TxtAlarmResponse`/`TxtAlarmConsequence`）＋
  ISA-18.2 分节；订阅/回填/写回三处都接；驱动面 +3（floor 7→10）、普查面 7→10、字段编辑判据 +3 断言。
- **消费②**：新报表 `AlarmPhilosophyReport`（主报警表）：名称/级别/原因/响应/后果/关联控件；
  **级别列复用** `AlarmReport.SeverityKey`；**停用的报警也进表**（工程文档口径）；
  空值策略＝**留空列**（不写 `null`/`N/A`/`—`）。
- **判据 6+1 条**（Core 6 ＋ UI 3 处）：列序 · 空值策略 · 去空白 · 枚举映射复用 · CSV 导出 ·
  JSON 往返 ＋ 老工程（键缺失）仍可读 · 面板写回。
- **字典**：两语言各 +6 键（按块内 key 序**文本插入**）。
- **🩸 两个坑**：①`json.dumps(sorted(...))` 整文件重写会把两本字典各 2165 行打乱（该文件按特性分块、
  不是全局有序）⇒ 改成块内定位插入；②面板新框必须走 `ControlWiringFace.Write`（程序化 `.Text=` 不触发
  `TextChanged` ⇒ 处理器会把三框读成空串写回 —— 阶段 187/252 记过的同一条纪律）。
- **负控 4/4**：空值策略改成 `N/A` ⇒ 红 · 列序对调 ⇒ 红 · 级别直显枚举名 ⇒ 红 · 面板不写回 Cause ⇒ 红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1027 行**（非空 **991**）；负载 `6.56 / 10.21 / 8.09`；
脚本 **29**；路径卫生 **708**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2378 / 315 / 877 / 8`（合计 **3844**）＝ **3844 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2372 → 2378**（＋6）。


## 阶段 267（2026-10-01）—— `§2-#11` **切片㊁ 落地**：优先级 ＝ f(严重度, 允许响应时间)

**一句话**：把 ISA-18.2 的「**优先级 ≠ 严重度**」锁成**派生量** —— 模型加 `AllowedResponseMs`（可空）＋
纯函数 `AlarmPriority`（1 = 最高；Emergency ≤60s⇒P1 / Critical ≤5min⇒P2 / Warning ≤30min⇒P3 / Info⇒P4）
＋ 主报警表新增**优先级列**（`P1`…`P4`，语言无关故不进字典）。

- **两个设计决定**：① 优先级是**派生量、不是存字段**（存下来会出现"改了严重度、优先级没跟着改"的陈旧值，
  与 `AlarmEngineStaleValueGuardTests` 记的是同一种病）；② `AllowedResponseMs ≤ 0` 按"立即"处理
  （工程数据可能被手改坏 —— 坏数据不许变成"不急"）。
- **判据 6 条**：表驱动 11 行（含每档阈值与 +1ms 边界）· **优先级≠严重度**（同严重度不同响应时间必须不同）·
  默认回退 · 非正数按立即 · **单调性**（更严的严重度不得拿到更小的紧急度）· `Label` 边界与越界抛。
- **负控 3/3**：优先级忽略响应时间 ⇒ 红 · 映射反转 ⇒ 红 · 报表去掉优先级列 ⇒ 红。
- **下一步（切片㊂）**：搁置/抑制（`Shelve` ＋ 到期解除）＋ KPI（报警率/驻留/抖动/Top10），
  判据是**状态迁移矩阵**（非法迁移逐条拒）；方案 §2 已写。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1027 行**（非空 **991**）；负载 `12.32 / 10.16 / 12.47`；
脚本 **29**；路径卫生 **709**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2394 / 315 / 877 / 8`（合计 **3860**）＝ **3860 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2378 → 2394**（＋16）。


## 阶段 268（2026-10-01）—— `§2-#11` **切片㈢ 的可算半**：报警系统 KPI（率/驻留/抖动/排行）

**一句话**：切片㈢ 有两半 —— 搁置/抑制要改**报警状态模型**（设备生成代码只有 state 0/1/2/3 ⇒ 必须先立设备契约），
KPI 只读记录、不动状态机 ⇒ **先做 KPI**：纯函数 `AlarmKpi` ＋ 报表 `AlarmKpiReport`（走既有报告框架）。

- **口径（写死并判据化）**：报警率 = 条数/窗口小时（窗口≤0 ⇒ 0）· 平均驻留**只算已返回的**、
  一条都没返回 ⇒ `null`（未返回按 0 计会把"还挂着"美化成"很短"）· 抖动 = 同一报警在窗口内 ≥2 次
  （**闭区间**、每个报警只算一次）· 排行降序＋同值按名称序 ⇒ **确定性**· 算不出来用 `—`
  （与"用户未填"的**留空列**区分：两种"空"不是一回事）。
- **判据 6 条**；**负控 3/3**（率不除窗口 ⇒ 红 · 未返回按 0 计入驻留 ⇒ 红 · 抖动忽略窗口 ⇒ 红）。
- **下一步**：搁置/抑制要先出**设备契约**（`shelved` 状态 ＋ 主题/字段），再改编辑器与设备两侧；
  方案 §3 已写明这条顺序。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1027 行**（非空 **991**）；负载 `5.99 / 13.25 / 9.01`；
脚本 **29**；路径卫生 **711**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2400 / 315 / 877 / 8`（合计 **3866**）＝ **3866 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2394 → 2400**（＋6）。


## 阶段 269（2026-10-01 夜）—— **夜间推进总账**（阶段 262~269，8 阶段 / 48 提交 / 负控 17 处全咬）

**一句话**：把用户拍板的三件事全部落地并各自带判据与负控 —— `#61s`（判据口径）· `§2-#12` 远程访问
（立项方案 ＋ M1 契约落地）· `§2-#11` ISA-18.2（落地方案 ＋ 切片㊀㊁ 与切片㈢ 的可算半落地）。
本阶段**产品/测试 0 行改动**。

| 阶段 | 内容 | 门禁 |
|---|---|---|
| 262 | `#61s`：M6 收窄口径 → 可复跑读数 ＋ 负控（实测收窄在残差上代价 **0**） | 绿（第 2 跑） |
| 263 | `§2-#12` 立项方案 ＋ 🩸 更正失真证据句（设备侧早有 MQTT 通路） | 绿 |
| 264 | `§2-#12` M1：主题/payload **契约 v1** ＋ **跨语言同源样例守卫**（C 5 条 ／ C# 20 条） | 绿 |
| 265 | `§2-#11` 落地方案 ＋ 🩸 更正失真口径（状态机早已实现） | 绿 |
| 266 | `§2-#11` 切片㊀：职责字段 ＋ 面板与主报警表两处消费（字典 +6 键） | 绿 |
| 267 | `§2-#11` 切片㊁：优先级 ＝ f(严重度, 允许响应时间)（字典 +1 键） | 绿 |
| 268 | `§2-#11` 切片㈢ 可算半：报警 KPI ＋ KPI 报表（字典 +8 键） | 绿 |
| 269 | 本总账 | 绿 |

- **负控 17/17 全部咬住**（每支逐字节还原 sha256）：`#61s` 3 · 契约 4 · 切片㊀ 4 · 切片㊁ 3 · KPI 3。
- **两次门禁首跑红**都按纪律处理：262 撞**已知偶发 `#47`**（按 `#47` 自带判定纪律判为既存偶发，红绿两读数都留档）；
  266 被 `RegisterNumberingGuardTests` 抓到登记册里**未转义的 `|`**（行被切成 5 格）。
- **待拍板 9 条**：`§2-#12` 四问（只读/可写 · 局域网/云 · TLS 终结在哪 · 远程端形态）· `§2-#11` 三问
  （字段必填与否 · 优先级方向 · 搁置权限＋审计）· 原有 `§2-#1`~`§2-#5` 五条 · `§2-#10` 现场参数。
- **下一步顺序**：① 搁置/抑制（**先出设备契约**，状态 4→5 档影响 `CodeGenerator.Alarm.cs`）·
  ② `§2-#12` M2 报警/状态上行（契约先行 ＋ 假 broker 主机测试）· ③ M3 远程只读端（待定形态）·
  ④ 切片㊁ 的面板半边（`AllowedResponseMs` 输入框）。
- **没做的**：真机相关（`#28` 发布门槛）· 搁置/抑制（契约先行）· `§2-#1`~`#5`（等拍板）· 未 `push`。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1027 行**（非空 **991**）；负载 `5.99 / 13.25 / 9.01`；
脚本 **29**；路径卫生 **711**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2400 / 315 / 877 / 8`（合计 **3866**）＝ **3866 通过 / 0 失败**；
**产品/测试 0 行改动**。


## 阶段 270（2026-10-01）—— **拍板记录与下一步重排**（搁置上设备 · 安全全套 · M2 沿用 M1 形状）

**一句话**：把"下一步"的四条拍板写入两份方案与登记册，并按新范围重排顺序。本阶段**产品/测试 0 行改动**。

- **A1 = ②（搁置设备侧也做）**：报警状态 **4 档 → 5 档** ⇒ `CodeGenerator.Alarm.cs` 的 `state 0/1/2/3`、
  设备运行时、契约三处同步；**契约先行**。
- **A2A3 = 要全套**：搁置要**权限 ＋ 审计 ＋ 硬上限**（复用设备角色模型；设备 `hmi_log` ＋ 编辑器 `AuditLogView`
  双落点；上限写死并判据化）。
- **B = 沿用 M1 形状**：M2 用 `<prefix>/alarm/<id>`、`<prefix>/status` ＋ `key=value;` —— 与 M1 同一张
  **跨语言同源样例表**范式，不另立站点层级、不换 JSON。
- **C2 = ③ 全可写**：`§2-#12` 的 **§4 安全模型全套变成必经**（主题 ACL · 认证 · 审计 · 命令 TTL ＋ 幂等 id），
  M4/M5 从"可选"变"必做"。
- **重排后的顺序**：① M2 契约（不依赖待定项，先做）② 搁置的设备契约（5 档编码/迁移表/权限/审计/上限）
  ③ M4 安全设施 ④ 切片㊁ 的面板半边 ⑤ M3 远程端（等 `C1` 形态）。
- **仍未拍板**：`C1` 形态 · `C3` 是否上云 · `C4` TLS 终结（已建议 broker 终结）· `D1` 字段必填与否 ·
  `§2-#1`~`#5` 五条 · `§2-#10` 现场参数。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1027 行**（非空 **991**）；负载 `5.99 / 13.25 / 9.01`；
脚本 **29**；路径卫生 **711**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2400 / 315 / 877 / 8`（合计 **3866**）＝ **3866 通过 / 0 失败**；
**产品/测试 0 行改动**。


## 阶段 271（2026-10-01）—— **拍板（C1/C3/C4/D1）＋ D1 落地**：职责文档完备率

**一句话**：四条拍板落账；其中 `D1=③` 当场落地（KPI 加"职责文档完备率"，判据 2 条 ＋ 负控 3/3）。
`C4=②`（设备侧终结 TLS）带来一个**新工作项**：mbedTLS 评估。

- **拍板**：`C1=①` 远程端做 **Web**（MQTT over WebSocket）· `C3=③` 先局域网、云后置 ·
  `C4=②` **设备侧终结 TLS**（⚠ 与建议相反 ⇒ 新增 **mbedTLS 评估**：体积/依赖/证书预置与轮换，
  两条路线＝改 `mqtt_client.c` 传输层 vs 本机终止代理）· `D1=③` 职责字段保持可空但**度量**它。
- **D1 落地**：`AlarmKpi.DocumentationCompleteness`（**三个字段都非空白**才算填了；空集 ⇒ **0**，
  不是 NaN 也不是全完备）＋ `AlarmKpiReport` 第 **6** 行（`alarm.kpi.documented.ratio`，显示 `50%（1/2）`，
  没传定义 ⇒ `—`）＋ 字典两语言各 +1 键。
- **三种"空"的口径明确区分**：用户未填 ⇒ **留空列**；算不出来 ⇒ **`—`**；空集完备率 ⇒ **`0`**。
- **负控 3/3**：`&&`→`||` ⇒ 红 · 空集返回 1 ⇒ 红 · 报表不给 (已填/总数) ⇒ 红。
- **`§2-#10` 仍在等数值**（N/T/D/卡/CoV/掉电）—— 参数名已给、数未给，选型未落定。
- **更新后的顺序**：① M2 契约 ② 搁置的设备契约 ③ **mbedTLS 评估** ④ M4 安全设施 ⑤ 切片㊁ 面板半边 · M3 Web 端。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1027 行**（非空 **991**）；负载 `8.25 / 8.34 / 6.52`；
脚本 **29**；路径卫生 **711**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2402 / 315 / 877 / 8`（合计 **3868**）＝ **3868 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2400 → 2402**（＋2）。


## 阶段 272（2026-10-01）—— `§2-#12` **M2 落地**：报警/状态上行契约（沿用 M1 形状）

**一句话**：`<prefix>/alarm/<报警 ID>`、`<prefix>/status` ＋ `st=..;sev=..;t=..;tb=..` / `on=..;t=..;tb=..`；
设备侧四个纯函数 ＋ C# 对偶 ＋ **两张跨语言同源样例表守卫**。

- **三个写死口径**：① `st=0`（Inactive）**不可发布**（不是状态迁移）；② **遗嘱与"主动离线"逐字相同**（`on=0`，
  设备侧忽略时间戳直接输出常量）；③ 状态用 `int` ＋ `ShelvedState` 常量、**不绑 `AlarmState` 枚举**
  —— 搁置（A1=②）落地时不必改契约，也就没有"设备先升级、远程端不认识"的时序问题。
- **🩸 状态 4 = 保留给搁置**：契约先留位，实现另立阶段。
- **同源接缝**：抽出 `MqttPayloadFields` 让三份解析器共用切分。
  🩸 它最初的"必须含 `;`"把遗嘱 `on=0`（单个 k=v）打挂了 ⇒ 把"裸数值"的处理**退回各解析器**，
  切分器只管切分（**一条规则放在错误的层会连累所有用户**）。
- **判据**：C 侧 2 组样例表 ＋ C# **22** 条；**负控 4/4**（C 侧样例改数 ⇒ 红 · 解析不查范围 ⇒ 红 ·
  遗嘱常量不一致 ⇒ 红 · 遗嘱要求时间基 ⇒ 红）。
- **下一步**：① 搁置的设备契约（5 档编码与迁移表，契约留位已就绪）② mbedTLS 评估（C4=②）③ M4 安全设施。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1068 行**（非空 **1032**）；负载 `6.33 / 8.29 / 7.07`；
脚本 **29**；路径卫生 **714**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2427 / 315 / 877 / 8`（合计 **3893**）＝ **3893 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2402 → 2427**（＋25）。


## 阶段 273（2026-10-01）—— 报警**搁置的设备契约**（`§2-#11` 切片㈢ 另一半的前置）

**一句话**：A1=② ⇒ 搁置要上设备；M2 已留位 `st=4`。本阶段出**契约**（不写实现）：
`docs/2026-10-01-alarm-shelving-contract.md` —— 状态 5 档 · 迁移矩阵（5x6）· 拒绝按码（7 个）·
权限点 `alarm.shelve` · 审计双落点 · 硬上限 1 min…8 h · 影响清单 8 处 · 验收规格 7 条 · 实现顺序三步。

- **写死的语义**：搁置是**停止播报**（不是删报警）· **必须带到期时间** · 搁置期间**不携带底层条件**、
  解除/到期时**重新发布真实状态** · 将来若要远程看底层条件就加**可选** `cond=`（M2 解析器忽略未知键 ⇒ 兼容）。
- **最要紧的一条**：**搁置中不允许确认** —— 否则操作员以为"我已处理"，实际报警被静音；
  解锁顺序必须是**先解除、再确认**。
- **拒绝按码**：`not-active`/`returned`/`already-acked`/`shelved`/`already-shelved`/
  `duration-out-of-range`/`forbidden`（与 `#61v` 同款：界面按码分派文案）。
- **权限与审计**：权限点 `alarm.shelve` **两侧都判**、未授权也留痕；审计**双落点**，且**到期自动解除也要写**
  （否则看不出"报警为什么突然又开始响"）。
- **验收规格**：迁移矩阵逐格 · 到期解除＋审计 · 权限＋审计 · 时长上下界（含边界）· `st=4` 往返 ·
  生成代码逐字节 · **负控三支**。
- **实现顺序**：① 编辑器模型与引擎 ② 生成器与设备运行时 ③ 远程端展示（随 M3）；审计与权限随 ①② 同步。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1068 行**（非空 **1032**）；负载 `6.33 / 8.29 / 7.07`；
脚本 **29**；路径卫生 **715**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2427 / 315 / 877 / 8`（合计 **3893**）＝ **3893 通过 / 0 失败**；
**产品/测试 0 行改动**。


## 阶段 274（2026-10-01）—— 搁置**编辑器侧落地**（契约 §10 的第 ①）＋ 顺手修掉一个真竞态

**一句话**：`AlarmState.Shelved = 4` · `AlarmRecord` +7 审计字段 · `AlarmShelvePolicy`（纯函数）·
`AlarmEngine` +`Shelve`/`Unshelve`/`ExpireShelves`/`TryAcknowledgeAlarm`（带 8 个码）·
播报面不含搁置中 · 报表 +shelved · 字典两语言各 +9 键 · 判据 **14** 条 · 负控 **4/4**。

- **契约最要紧那条落地了**：**搁置中不允许确认**（`CanAcknowledge` 拒 `shelved`）—— 否则操作员以为
  "我已处理"而报警被静音；解锁顺序必须是先解除、再确认。
- **解除回到哪个状态**：重新求值一次 —— 条件仍满足 ⇒ `Active`；已解除 ⇒ `Returned`；
  **变量还没初值 ⇒ 保守回 `Active`**（宁可再响一次，不可静默吞掉）。
- **🩸 顺手抓到并修掉一个真竞态**：`Start()` 的 `Change(0,…)` 让定时回调立刻跑，与手工评估交错 ⇒
  同一报警**出现两条记录**（`#70` 同类竞态的另一个出口）⇒ 建记录改为**锁内二次确认**、事件在锁外发；
  并新增确定性入口 `EvaluateOnce()`（手工入口不要求 `IsRunning`）。
- **两条如实记的边界**：① 未授权的留痕目前只在**引擎日志**，写进 `AuditLogView` 的接线是下一片；
  ② 英文码文案第一次把 `EnUsMechanicalAuditTests` 的长句计数顶到 128 ⇒ 按该判据自带纪律**改短**（不抬基线）。
- **负控 4/4**：搁置中允许确认 ⇒ 红 · 到期不解除 ⇒ 红 · 未授权放行 ⇒ 红 · 时长不检查 ⇒ 红。
- **下一步**：① 搁置的设备侧（`CodeGenerator.Alarm.cs` ＋ `hmi_actions.c`，契约 §10 第 ②）
  ② 审计接 `AuditLogView` ③ mbedTLS 评估（C4=②）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1068 行**（非空 **1032**）；负载 `11.02 / 8.89 / 6.76`；
脚本 **29**；路径卫生 **716**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2441 / 315 / 877 / 8`（合计 **3907**）＝ **3907 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2427 → 2441**（＋14）。


## 阶段 275（2026-10-01）—— 搁置权限点 `alarm.shelve` 落地（契约设备侧第 ② 步之半）

**一句话**：让"搁置"这件事在权限模型里有独立的一位 —— `Permission.ShelveAlarm = 1 << 9`（**与确认分开**）·
`Permission.All` 位宽同步提到 10 位 · 生成头 `HMI_PERM_SHELVE_ALARM 0x200u`（角色掩码同步）·
`UserManagerView` 第 **10** 枚权限复选框 · 字典 +1 键 · 第五层面同步（19 按钮 / 22 条声明）。

- **为什么单独一位**：搁置会让报警**停止播报**（"把报警静音"的权力）⇒ 现场要能按角色/班次收放，
  而不是"能确认就能静音"。默认预置角色里它与确认同权（Operator/Engineer），项目可按需收紧。
- **🩸 三个"忘了就静默出错"的地方，都被判据抓住**：① `Permission.All` 忘了提位宽 ⇒
  `AuthenticationTests.Permission_All_IncludesAll` 红；② host fixture 必须用生成器**重算**（逐字节判据）；
  ③ 只加枚举不加界面复选框 ⇒ 第 5 层宿主面报"`PermShelveAlarm` 没被分类"。
- **⚠ 如实记的两条边界**：① 这一位**还没有被任何设备代码使用**（生成器还没发搁置权限门）⇒ 下一步
  阶段 276 做 `CodeGenerator.Alarm.cs` 的 state 4 迁移 ＋ 权限门 ＋ 到期检查（判据＝生成代码逐字节）；
  ② `UserManagerView` 的 10 枚权限复选框仍是"事件从未订阅"（既有缺陷 `#66` ①，"刻意不接"），
  本阶段只把新一枚按同族登记为 `Inert`，没有顺手改这一族（改它要重写第五层 10 条声明）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1068 行**（非空 **1032**）；负载 `5.14 / 8.58 / 5.77`；
脚本 **29**；路径卫生 **716**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2441 / 315 / 877 / 8`（合计 **3907**）＝ **3907 通过 / 0 失败**。


## 阶段 276（2026-10-01）—— 搁置的**生成代码**（契约 §10 第 ② 步）＋ 生成段权限门

**一句话**：`CodeGenerator.Alarm.cs` 生成 state 4：结构体两个新字段 · 界限宏**单源**自 `AlarmShelvePolicy` ·
`alarm_acknowledge` **搁置中拒绝确认**（且排在 `state==1` 之前）· `alarm_shelve`（权限门 → 时长界 → 拒 state 0/3/4）·
`alarm_unshelve`（恢复 `shelved_under`）· 每臂评估开头判到期（`(int32_t)(now-until)>=0` 防 tick 回绕），
未到期直接 `return`（不评估、不改对外状态）。

- **判据 7 条**：生成代码**逐字节 ＋ 顺序**（"拒绝确认必须在确认支之前""到期块必须在 `triggered` 之前"
  两条顺序断言是这一片的关键 —— 放错位置就永远命中不到）。
- ⚠ **判据形状守规矩**：`TestAssertionStyleGuardTests` 禁止"精确调用文本 + `Assert.Contains`"
  ⇒ 一律 `CSource.AssertText`（本阶段首跑被它拦了一次）。
- **负控 3/3**：摘掉拒绝确认 ⇒ 红 · 摘掉到期恢复 ⇒ 红 · 摘掉权限门 ⇒ 红。
- **⚠ 仍未做**：① `alarm_shelve`/`alarm_unshelve` **还没有设备侧调用入口**（要一个动作/脚本入口把它接出去）；
  ② 设备侧搁置审计（写 `hmi_log`）未做；③ 远程端"已搁置（至 …）"展示随 M3。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1068 行**（非空 **1032**）；负载 `12.64 / 14.33 / 14.33`；
脚本 **29**；路径卫生 **716**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 315 / 2448 / 877 / 8`（合计 **3914**）＝ **3914 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2441 → 2448**（＋7）。


## 阶段 277（2026-10-01）—— 搁置的**设备侧调用入口**（ABI 表 ＋ 生成器初始值）

**一句话**：把 `alarm_shelve`/`alarm_unshelve` 接到 `.so` → 宿主那条 ABI 通路上（与 `alarm_acknowledge`
同一条）：`hmi_ui_api_t` **尾部**追加两个函数指针 ＋ `s_ui_api` 初始值表同步；判据 3 条（含**孪生守卫**）。

- **本片真正的技术点：必须追加在尾部** —— 宿主按偏移读前面的字段，插在报警块中间会让
  `trend_tick`/`datalog_export_csv` 及其后字段偏移整体后移 ⇒ 老宿主读到**错位的函数指针**
  （比缺功能严重得多）。判据把这条钉成可执行的位置断言。
- **⚠ 口径如实写清**：接的是 **`.so` → 宿主** 那条 ABI 通路，与 `alarm_acknowledge` 完全同一条；
  而**本仓里那条通路没有宿主侧调用点**（设备 UI 不在本树里，`alarm_acknowledge` 也一样）
  ⇒ 本阶段是"**ABI 可调、与确认同通路**"，**不是**"界面上有搁置按钮"。
- 🩸 判据自己踩了两个假设：① 生成器里 `.alarm_evaluate` 指向的是 `alarm_engine_evaluate`（入口包装、
  不是同名函数）⇒ 孪生守卫不能对全体断言"名＝函数名"，只对搁置这两条断言；② 夹具没有
  `DataLogConfig` ⇒ 生成表里根本没有 `.datalog_export_csv` ⇒ 顺序断言改成"排在报警块既有项之后
  ＋（若有 csv）再排其后"，避免变成"夹具形状"断言。
- **负控 3/3**：生成器不写那两行 ⇒ 红 · 头文件插到 csv 之前 ⇒ 红 · 生成器写头里没有的项 ⇒ 红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1068 行**（非空 **1032**）；负载 `9.43 / 12.60 / 9.36`；
脚本 **29**；路径卫生 **716**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2451 / 315 / 877 / 8`（合计 **3917**）＝ **3917 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2448 → 2451**（＋3）。


## 阶段 278（2026-10-01）—— 搁置审计接进 `AuditLogView`（补齐 274/276 记下的同一条边界）

**一句话**：引擎发 `ShelveAudited` 事件 ＋ App 层 `AlarmAuditWiring` 接到 `AuthenticationService.AppendAudit`
（唯一写入口）⇒ **成功 / 被拒（含未授权）/ 解除（含到期自动解除）** 三条都进审计表。

- **两条新审计动作**：`AuditAction.AlarmShelved` / `AlarmUnshelved` ＋ `ActionDisplayName` 两档显示名
  （字典两语言各 +2 键）—— 不映射就会落到 `_ => ToString()`，用户看到英文标识符。
- **判据 3 ＋ 1 条**：App 层"三次调用 ⇒ 三条落盘"（含 `denied:forbidden` / 含 `Expired`）· 解绑后不再写 ·
  两档都有本地化名 · Core 层"四次调用 ⇒ 四条事件"。
  🩸 判据按**时间排序**再断言：`AuditLogStore.LoadAll` 给的是**最新在前**，断言顺序会变成断言存储实现。
- **🩸 顺手被 `SingleFileSizeGuardTests` 拦了一次**（治理纪律）：`MainWindow.axaml.cs` 涨了 3 行 ⇒
  按该判据自带纪律**不抬基线**，把接线压成**同一行**且不持有句柄 ⇒ 回到 2983 行（diff `+1/-4`）。
- **负控 3/3**：接线不订阅 ⇒ 红 · 未授权不发事件 ⇒ 红 · 解除不发事件 ⇒ 红。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1068 行**（非空 **1032**）；负载 `6.89 / 12.10 / 10.06`；
脚本 **29**；路径卫生 **717**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2452 / 315 / 880 / 8`（合计 **3921**）＝ **3921 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2451 → 2452**（＋1）· `HmiEditor.App.UiTests` **877 → 880**（＋3）。


## 阶段 279（2026-10-01）—— 设备侧搁置审计（契约 §7 双落点的**另一半**）

**一句话**：`hmi_audit` 下沉为公共 API（设备**唯一审计落点**：串口 ＋ append-only 文件）＋
ABI 两个指针带上**操作人** `const char* by`（宿主知道登录用户、.so 不知道）＋ 生成代码 **6 类写入点**。

- **写入点**：成功搁置 · 被拒(无权限) · 被拒(时长越界) · 被拒(状态) · 手动解除 · **到期自动解除**
  （按报警生成、`by=NULL`）⇒ 生成后共 **8 处** `hmi_audit` 调用。
- **一处刻意的双轨**：同一路径上既有 `ALARM_TRACE`（串口调试、可整段编掉）也有 `hmi_audit`（审计、不可编掉）；
  判据专门断言"审计那一行里不许出现 `ALARM_TRACE`"，把两条轨分清楚。
- **判据**：`DeviceAudit_IsWrittenOnGrantDenyAndUnshelve`（7 个写入点逐条字面量 ＋ 调用点数 ≥7 ＋
  不走可编掉的 trace）；**负控 3/3**（成功不留痕 ⇒ 红 · 被拒不留痕 ⇒ 红 · 到期不留痕 ⇒ 红）。
- **⚠ 如实记的边界**：那两个 ABI 指针**仍没有宿主调用点**（与 `alarm_acknowledge` 同现状）⇒
  `by` 的来源（`hmi_current_user()`）要在宿主接上时补一行；设备 UI 的搁置按钮不在本树。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1068 行**（非空 **1032**）；负载 `8.13 / 8.74 / 8.92`；
脚本 **29**；路径卫生 **717**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2453 / 315 / 880 / 8`（合计 **3922**）＝ **3922 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2452 → 2453**（＋1）。


## 阶段 280（2026-10-01）—— **报警抑制**（切片㈢ 的最后一块）

**一句话**：**搁置**是"已经报了的先别吵"（有状态、有到期、停止播报）；**抑制**是"压根不要报"
（**无状态、无到期、不求值**）。判据核心：抑制期间条件满足 ⇒ **记录数不增**；解除后下一轮**立刻产生**。

- **引擎**：`Suppress`/`Unsuppress`/`IsSuppressed`/`GetSuppressedAlarmIds`（按 ID 序 ⇒ 稳定可判）；
  求值时对抑制中的配置**整条跳过** —— 既不新建记录，**也不推进既有记录**（"先产生再隐藏"不是抑制）。
- **审计**：`SuppressionAudited` 事件 ＋ 两个新审计动作（`AlarmSuppressed`/`AlarmUnsuppressed`）＋ 显示名
  （字典两语言各 +2 键）；`AlarmAuditWiring` 同时订阅（解绑时两条一起解）。
  **两条拒绝路径也留痕**（重复抑制 / 未在抑制）—— 审计的价值一半在"谁试过"。
- **判据 Core 4 ＋ App 1**；**负控 3/3**（求值不跳过 ⇒ 红 · 未授权也放行 ⇒ 红 · 拒绝不留痕 ⇒ 红）。
- **⚠ 如实记**：① **只做编辑器侧**（抑制没有"要上设备"的决定；要上就按 §277/§279 同一套路，无需新设计）；
  ② **配置级"设计抑制"**（某工况下本就不该报）未做 —— 本阶段是**运行时抑制**，前者是独立小片。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1068 行**（非空 **1032**）；负载 `11.32 / 12.61 / 8.61`；
脚本 **29**；路径卫生 **717**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2457 / 315 / 881 / 8`（合计 **3927**）＝ **3927 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2453 → 2457**（＋4）· `HmiEditor.App.UiTests` **880 → 881**（＋1）。


## 阶段 281（2026-10-01）—— `C4=②` **设备侧 TLS 评估**（`docs/2026-10-01-device-tls-assessment.md`）

**一句话**：按用户拍板（设备侧终结 TLS）做评估 —— 摆硬事实、四条路线对照、红线与判据面、待拍板两条。
**本阶段产品/测试 0 行改动。**

- **五条硬事实（实测）**：① T113 生产/OTA 镜像 **`libopenssl=y` 而 `libmbedtls` 未选**；
  ② 版本都已 EOL（mbedTLS **2.2.1** / OpenSSL **1.1.0i**）；③ 镜像有 `libcurl`＋`procd`，
  **无** stunnel/socat/mosquitto；④ `hmi_crypto.h:4-6` 本仓**刻意不引 mbedTLS**；
  ⑤ **设备无 RTC**（时间起步于 `/etc/timestamp`、联网后被 NTP 跳变，`main.c:167` 记过）
  ⇒ **X.509 有效期在开机早期不可靠**。
- **结论/建议**：走 **本机终止代理 ＋ PSK 优先** —— 复用镜像里**已有**的 OpenSSL、`mqtt_client.c` **0 行改动**；
  PSK 把"无 RTC"从根上移掉。**红线**：绝不静默降级为明文（握手失败 ⇒ 拒绝连接 ＋ 审计）。
- **待拍板两条**：PSK 先行还是 X.509 先行（建议 PSK）· 允不允许显式降级（建议永不自动降级）。
- **判据面 5 条**（含"板子上实测尺寸/内存"—— 不做完，"零镜像改动"只是推断）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1068 行**（非空 **1032**）；负载 `2.43 / 9.72 / 8.67`；
脚本 **29**；路径卫生 **717**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2457 / 315 / 881 / 8`（合计 **3927**）＝ **3927 通过 / 0 失败**；
**产品/测试 0 行改动**。


## 阶段 282（2026-10-01）—— **MQTT TLS 配置契约**（`C4=②` 的 M4 第一块）

**一句话**：把评估的红线变成**可执行契约** —— `mqtt.tls=off|psk|ca` ＋ 三个必需项键，
设备侧纯函数 ＋ 编辑器侧对偶 ＋ **跨语言同源样例守卫**；红线：**未知模式拒绝、缺件拒绝、不自动降级**。

- **设备侧**：`mqtt_tls_contract.{h,c}`（`parse_mode` / `mode_token` / `validate`）＋ host 测试
  **7 条样例表**（跨语言同源）＋ 边界（未知模式/大小写/空串/NULL/未知枚举）。
- **编辑器侧**：`MqttTlsContract`（`TryParse(ini)` 给 `unknown-mode` / `psk-incomplete` / `ca-missing`
  **按码**）＋ `CommConfigCodeGen` 的 `MqttParams` 4 个可空 TLS 参数，**配了才发**（老工程 ini 逐字节不变）。
- **判据 C# 12 条 ＋ C 1 组**；**负控 3/3**（未知模式当 off ⇒ 红 · psk 缺件放行 ⇒ 红 · ini 无条件发键 ⇒ 红）。
- **⚠ 仍未做**：① 真正的 TLS 握手（按评估走**代理进程**，那是下一块）；② 界面露出这四个键
  ＋ `MqttDriver.ValidateConnection` 接这套校验；③ 板子上实测尺寸/内存（要真板子）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1095 行**（非空 **1059**）；负载 `8.69 / 11.19 / 7.65`；
脚本 **29**；路径卫生 **720**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 315 / 2469 / 881 / 8`（合计 **3939**）＝ **3939 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2457 → 2469**（＋12）。


## 阶段 283（2026-10-01）—— 驱动侧 TLS 校验 ＋ 界面露出三档（M4 再推一格）

**一句话**：把 282 的契约接进**连接配置**与**驱动校验** —— 模板露出 `off/psk/ca` 三档 ＋ 三个配套字段；
**配错就保存不了**（缺件是 Error），TLS 开着却用 1883 只给 Warning。

- **模板驱动界面** ⇒ 表单自动露出（无需改 axaml）；`ValidateConnection` 接 `MqttTlsContract`。
- **🩸 修掉 282 自己埋的键名缺陷**：`ResolveMqtt` 当时把 **ini 键名**（`mqtt.tls`）当成**连接参数名**读
  （参数是短名 `tls`）—— 282 的判据两边用的是同一个错名字、自洽通过；283 加模板字段时暴露。
  **教训：同源自洽 ≠ 同源正确**；能抓它的只有"另一侧的真实来源"（驱动模板字段名）。已写进负控 nc4。
- **三条插件文案守卫当场拦下**（全部按它们的规矩处置）：硬编码中文基线 11 → 19（4 标签 ＋ 4 文案，
  **键都走 `param.*` / `comm.issue.*`**）· 两本字典各 +8 键 · `comm.issue.*` 四个码进 `CommIssueRegistry`
  并**在探针里真触发**（否则"没有任何机制取到" ⇒ 可达性守卫红）。
- **判据 ＋6**（驱动）＋ ini 判据改用参数名；**负控 5/5**（缺件放行 / 未知模式当 off / 模板丢 Select /
  参数名串回 ini 键 / 探针摘一份参数）。
- **⚠ 仍未做**：真正的 TLS 握手（本机终止代理）· 板子上实测尺寸内存 · "选了 psk 才显示那两个框"的条件显示。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1095 行**（非空 **1059**）；负载 `18.54 / 20.34 / 13.22`；
脚本 **29**；路径卫生 **720**；步 5 逐程序集 `44 / 44 / 4 / 96 / 84 / 2469 / 315 / 881 / 8`（合计 **3945**）＝ **3945 通过 / 0 失败**；
`Hmi.Communication.Tests` **78 → 84**（＋6）。


## 阶段 284（2026-10-01）—— **本机 TLS 终止代理契约**（M4 第三块）

**一句话**：把评估的"本机终止代理"写成**可判契约** —— 三条硬规则（端点决定 / **不许绕过代理** / 生成期就
连回环）在 C 与 C# 两侧都实现并判，配跨语言同源样例与 4 支负控；**刻意不写**代理二进制与 init 脚本。

- **R1 端点决定**：`off` ⇒ 直连 broker；`psk`/`ca` ⇒ `127.0.0.1:<local_port>`（缺省 1883）。
- **R2 不许绕过**：TLS 开着时客户端 host 必须是回环（`127.0.0.1`/`::1`/`localhost`）。
- **R3 生成期落点**：TLS 开着时 `mqtt.host` **绝不**写成 broker 地址，broker 只以
  `mqtt.tls.upstream_host/_port` 出现（**只给代理读**）；`off`/非法取值时**不发** upstream。
- 判据：C 端 9 条样例 ＋ 边界；C# 端跨语言同源守卫 ＋ 7 ＋ 9 条；`MqttTlsContractTests` **12 → 23**。
  **负控 4/4**（C 绕过 ⇒ 红 · C"不许绕过"恒真 ⇒ 红 · 生成器漏 loopback ⇒ 红 · C# 端点解析错误 ⇒ 红）。
- **⚠ 刻意没写**代理二进制与 init 脚本：**启动一个不存在的二进制是假交付** ⇒ 把进程要求
  （`respawn` · 启动顺序 · **只绑回环** · 失败不降级 · 审计事件名）写成契约里**可判条目**，等真实现逐条判。
  专题 `docs/2026-10-01-local-tls-proxy-contract.md`。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1136 行**（非空 **1100**）；负载 `12.61 / 13.19 / 9.31`；
脚本 **29**；路径卫生 **720**；步 5 逐程序集 `44 / 44 / 4 / 84 / 96 / 315 / 2480 / 881 / 8`（合计 **3956**）＝ **3956 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2469 → 2480**（＋11）。


## 阶段 285（2026-10-01）—— 远程写命令的**信封契约**（`§2-#12` M5 第一块）

**一句话**：把提案 §4「失效」那一行（**TTL ＋ 幂等 id**）写成两侧可判的纯函数 —— 主题
`<prefix>/cmd/<target>`、载荷 `key=value;`、三条硬规则（幂等 id 形状 / TTL 上下界 / 过期即丢含"来自未来"）。

- **设备侧**：`mqtt_cmd_contract.{h,c}`（主题 · id 形状 · TTL 界 · 新鲜度 · 载荷拼接）＋ host 判据
  7 条样例表（跨语言同源）＋ 边界 24 条。
- **编辑器侧**：`MqttCommandContract` 对偶（含端到端 `Accept`）＋ **`CommandLedger`**（有界、过期淘汰、
  丢最先到期的）＋ 常量同源守卫（读 C 头比对 4 个宏）＋ 载荷逐字节同序。
- **一条刻意的语义**：**被拒的命令不进台账** —— 否则一次伪造（过期/未来）就能把合法 id 永久占掉，
  之后真命令会被当重复丢掉。
- 判据 **16 条**；**负控 4/4**（过期也收 ⇒ 红 · 未来也收 ⇒ 红 · 台账不拦重复 ⇒ 红 · TTL 上界失效 ⇒ 红，
  其中最后一支**先被跨语言样例表抓住** —— NC 断言据此放宽到"该类至少一条红"，如实记录）。
- **⚠ 仍未做**：主题 ACL（谁能写 `/cmd/`）· 设备侧去重台账 · 值域与写权限校验 · M3 Web 端 UI。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1174 行**（非空 **1138**）；负载 `9.35 / 13.55 / 9.69`；
脚本 **29**；路径卫生 **724**；步 5 逐程序集 `44 / 44 / 4 / 84 / 96 / 2496 / 315 / 881 / 8`（合计 **3972**）＝ **3972 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2480 → 2496**（＋16）。


## 阶段 286（2026-10-01）—— MQTT **主题级 ACL ＋ 角色判定**（提案 §4「授权」）

**一句话**：方向 ＋ 权限位两件事判死 —— **设备只往 `value|alarm|status` 写（上行），远程只往 `cmd/` 写（下行）**；
每类主题一个权限位（`ack/`⇒确认 · `shelve|unshelve/`⇒**搁置位** · 其它⇒操作权 · 读`value|status`⇒查看 ·
读`alarm`⇒报警历史）。三条硬规则：**A1 方向不能反** · **A2 未授权即拒** · **A3 不认识即拒**。

- 设备侧 `mqtt_acl_contract.{h,c}`（分类 / 所需权限 / 端到端判定 + 稳定码）＋ host **24 条样例表**（跨语言同源）。
- 编辑器侧 `MqttTopicAcl` 对偶 ＋ **预置角色掩码的行为判据**（"能确认 ≠ 能搁置"在掩码层可见）。
- **🩸 被既有守卫拦两次**：① `Firmware_DoesNotHandWritePermissionBits` 拦下我写的
  `#define HMI_PERM_* 0x…` 兜底 ⇒ 删兜底、直接吃 `generated_security.h`（编译行补 `-Itests/fixtures`）；
  ② 我自己的新断言被**自己的注释**绊倒（注释里引用了那串字面量）⇒ 断言改成**行首**匹配，
  与守卫同口径。**判据口径必须与守卫一致**。
- **负控 5/5**；⚠ 其中**两支首版不可观测**（前缀 `Contains` / 摘掉 `'/'` 检查都被后续逻辑兜住）⇒
  如实记：那处检查是**冗余但刻意的纵深防御**，负控换成真正改行为的注入，并给样例表补两行能看见前缀规则的语料。
- **⚠ 仍未做**：target 与变量/报警表的存在性比对 · 设备侧去重台账 · 值域校验 · **broker 侧 ACL 配置**（部署物）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1236 行**（非空 **1200**）；负载 `6.04 / 10.82 / 6.66`；
脚本 **29**；路径卫生 **727**；步 5 逐程序集 `44 / 44 / 4 / 96 / 84 / 2500 / 315 / 881 / 8`（合计 **3976**）＝ **3976 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2496 → 2500**（＋4）。


## 阶段 287（2026-10-01）—— 远程写命令的**设备端执行**（幂等台账 ＋ 值校验）

**一句话**：把 §285/§286 的信封与 ACL 落到"设备真的这么判"—— 有界幂等台账（C 与 C# **同语义**）＋
写值校验（**只读优先** · 严格数字 · 量程含端点 · `min>max`=配置错）。

- C：`mqtt_cmd_ledger.{h,c}`（容量 16，过期淘汰，满则丢**最先到期**的）＋ `mqtt_write_guard.{h,c}`
  （返回稳定码 `read-only`/`not-a-number`/`out-of-range`/`bad-target`）。
- 两张跨语言同源样例表（写校验 17 行 / 台账 5 行）；C# `MqttWriteGuardTests` 4 条，
  其中一条**直接解析 C 的台账样例**验证两侧 `CommandLedger` 同语义。
- **顺序即契约**：只读判定先于值合法性（样例行 `0/0.0/100.0/"abc" ⇒ read-only`）。
- 🩸 首跑 3 红是**我自己的语料自相矛盾**（`min=0` 却写 `-0.5` 期望 OK）⇒ 改语料（不是改实现）。
- **负控 4/4**；nc4（台账无界）以 **SIGSEGV(139)** 形态命中 —— 越界写直接崩，比 FAIL 更彻底
  ⇒ NC 判定接受"信号结束"也算红，并如实记录形态。
- **门禁首跑红了一次，与本次改动无关**：`DeviceManagerInputReadFaceTests.FelImage_IsValidatedBeforeTheExternalToolRuns`
  （自查交集 = ∅；单独连跑 3 次全绿 ⇒ 负载下时序抖动）⇒ **红绿两次读数都留档**，重跑整门禁 `GATE_EXIT=0`。
- **⚠ 仍未做**：① 这两个模块**还没有调用点**（设备侧 `/cmd/` 订阅与执行为下一块，且要定"量程从哪来"）；
  ② broker 侧 ACL 配置；③ 远程写审计接线；④ M3 Web 端 UI。

**读数（工作树，重跑）**：`GATE_EXIT=0` / **7 步** / 日志 **1316 行**（非空 **1280**）；负载 `9.88 / 10.75 / 8.87`；
脚本 **29**；路径卫生 **732**；步 5 逐程序集 `44 / 44 / 4 / 96 / 84 / 2504 / 315 / 881 / 8`（合计 **3980**）＝ **3980 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2500 → 2504**（＋4）。


## 阶段 288（2026-10-01）—— **值域口径 ＋ `/cmd/` 调用点**（M5 的"设备真的执行"）

**一句话**：先把"量程从哪来"定下来（**默认 = 数据类型可表示范围**，原始值；Float/未知 ⇒ 不限），
再把 `/cmd/` 的处理顺序写成代码 —— **ACL → 解析 → 新鲜度 → target 存在 → 只读/量程 → 台账 → 写 → 审计**。

- **口径**：`tag_meta_t` 尾部追加 `has_range/min_value/max_value`（宿主存指针不拷贝 ⇒ 尾部安全；
  两个产物 `Generate`/`GenerateHeader` 同步）；Float 不限 ⇒ 宁松勿误拒；将来工程量程由变量定义覆盖。
- **调用点**：`mqtt_cmd_dispatch.{h,c}`；元数据**按值传**（不做 lookup 回调）；宿主能力三回调
  （旧值/写入/审计）；**成功与拒绝都留痕**，成功那条含 `old→new`。
- 判据：C 侧 9 组（含"被拒不进台账""写失败时台账已消耗"）；C# 生成器 3 组。
- 🩸 **三次自我纠错**：① 解析出的字段是指针 ⇒ 改定长缓冲（超长不截断）；② `strtoull("-1")` 静默回绕
  ⇒ 与 C# 口径对齐（先判整串是数字）；③ 负控让判据**崩**：`-1` 却留 `code == NULL`（本契约里 NULL 表示成功）
  ⇒ 补稳定码兜底，并把它写成契约"**返回 -1 必须给非空码**"。
- 🩸 生成代码守卫拦下 `-32768f`（非法 C 浮点常量）⇒ 全改带小数点。
- ⚠ **一次未定论**：首版 lookup 回调出现"返回非空、调用侧收到 NULL"，排查证据**自相矛盾**
  （栈地址 vs static 局部）⇒ **不写成缺陷登记**；处置是改成按值传（少一层回调）。另有**遗留调试块**
  导致同一命令被处理两次、污染计数断言 —— 这部分是自证的。
- **负控 4/4**（nc1 以崩溃形态命中）。
- **⚠ 仍未做**：`mqtt_client.c` 的订阅接线（调用点函数在仓、**没有调用者**）· 远程写审计落 `hmi_audit` ·
  M3 Web 端 · broker ACL · 代理二进制 · 板子实测。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1354 行**（非空 **1318**）；负载 `11.54 / 13.12 / 10.53`；
脚本 **29**；路径卫生 **734**；步 5 逐程序集 `44 / 44 / 4 / 96 / 84 / 2512 / 315 / 881 / 8`（合计 **3988**）＝ **3988 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2504 → 2512**（＋8）。

---

## 阶段 289（2026-10-11）—— **远程写命令的宿主接线**（`§2-#12` M5 的"设备真的执行"最后一公里）

### 289.1 缺的是什么

阶段 288 把 `/cmd/` 的**判定链**（ACL · 信封 · 新鲜度 · 幂等台账 · 只读/量程）全部落地、都有判据，
但**没有调用点**（`hmi_cmd_handle` 只出现在自己的定义与测试里）⇒ "设备真的执行"停在纯函数层。
本节补的就是那条线；顺带暴露并修掉一处**真实漂移**（见 289.3）。

### 289.2 落地

| 面 | 交付 |
|---|---|
| 接线模块 | `app/comm/mqtt_cmd_wiring.{h,c}`：订阅 `<pre>/cmd/<tag>`（**只为可写标签**）· 主题分流 · 按载荷 `target=` 从**采纳的标签表**取元数据（按值传，不做 lookup 回调）· 权限掩码**逐位问** `hmi_has_permission` · 审计接 `hmi_audit`（角色由宿主补） |
| 设备侧接线点 | `variable_table.c`：新增 `hmi_tag_meta_at()`；`var_groups_rebuild()` 注册 cmd 主题；`on_mqtt_message()` 把 `/cmd/` 与 `/set/` **分开路由**（`/set/` 旧通道语义不变） |
| 一致性硬化 | **主题 target ≠ 载荷 target ⇒ 拒绝**（稳定码 `target-mismatch`）：两个字段都能决定"写谁"，ACL 看主题、真正写的是载荷 |
| 🩸 漂移修复 | 阶段 288 给 `tag_meta_t` 尾部追加了 `has_range/min_value/max_value`、生成器**两个**产物都发了，**但仓里那份手工维护的示例产物 `firmware/t113/generated/tags.{h,c}` 没跟着更新** ⇒ "量程从 `tag_meta_t` 取"这一步**根本编译不过**，而 CI 全绿（没有任何判据盯着这份示例产物与生成器的对应关系）。本阶段同步该产物 ＋ 新增漂移守卫 |
| 超长载荷 | ≥384 字节 ⇒ **不截断**（截断会让 target 指向另一个变量），按必然 malformed 处理 |

### 289.3 判据与负控

- **C 侧 ＋31 条**（`tests/hmi_comm_test.c` 的 `[远程写命令接线]`）：订阅面只含 3 个可写标签 ·
  主题分类 4 例 · 端到端写入 ＋ 旧值→新值 ＋ 审计 · 量程**来自标签表**（U16 越界/端点 · Bool 0..1 ·
  `has_range=0` 不限）· 只读拒且缓存一字不变 · target 不一致 · 未授权 · 重复 · 过期 · 来自未来 ·
  未知 target · malformed · 超长 · `on_mqtt_message` 真入口 ＋ `/set/` 不回归。**527 通过 / 0 失败**。
- **C# 侧 ＋4 条**（`DeviceTagsContractDriftTests`）：结构体字段序列与生成器**逐条相同** ·
  两张表每个初始化器**字段数完整** · range 三字段在 · **生成器的两个产物**发同一结构体。
- **负控 8/8**（`.workbuddy/tmp/s289-nc.py`，逐支 sha256 还原）：绕过 ACL · 忽略只读/量程 ·
  摘掉一致性检查 · 订阅面放宽到只读 · 摘掉 `/cmd/` 路由 · 产物退回无 range ·
  生成器两个产物**各自**停发 range。
  ⚠ **第 7 支首跑未咬住**：当时守卫只钉 `GenerateHeader`，摘掉 `Generate`（另一个产物）照样绿
  ⇒ 补 `BothGeneratorProducts_EmitTheSameMetaStruct` 后才咬住 —— 这正是"做不出负控的判据不发布"的价值。

### 289.4 ⚠ 仍未做（如实）

- **broker 侧 ACL 配置物** · **M3 Web 端 UI** · 远程写审计**落盘路径实测**
  （`io->audit → hmi_audit` 已通；审计文件在 `/mnt/UDISK`，需板子）。
- **M2 的 publish/LWT 接线**与"假 broker"主机测试 —— 本项只做了 **M5 那一半**。
- **板子实测**：本阶段全部是主机侧判据（无硬件）。
- `scripts/ci-gate.sh` 的 `GATE_EXIT` 未写进日志尾部（读数工具会如实报"关键项缺失"）——
  本阶段退出码由调用侧记录为 **0**。

**读数（工作树）**：`GATE_EXIT=0` / **7 步** / 日志 **1417 行**（非空 **1381**）；负载 `11.92 / 18.84 / 14.35`；
脚本 **31**；路径卫生 **739**；步 5 逐程序集 `44 / 44 / 4 / 96 / 84 / 2517 / 315 / 881 / 8`（合计 **3993**）＝ **3993 通过 / 0 失败**；
`HmiEditor.Core.Tests` **2517**（含本阶段新增 `DeviceTagsContractDriftTests` 4 条）。
（脚本 **29 → 31** 与路径卫生 **734 → 739** 来自阶段 288 之后**尚未入册**的 t113 bring-up 提交，见台账 §9。）

---

## 阶段 290（2026-10-11）—— M2 的**状态上行 ＋ 遗嘱（LWT）＋ 假 broker 判据**；顺带修掉"订阅表被 start 清空"

### 290.1 缺的是什么

远程访问方案 §5 给 M2 定的判据面原文是「主机测试用**假 broker**（本地 socket 监听）断言报文序列」；
此前所有判据都停在纯函数/单报文层，**没有一条**证明设备真把 CONNECT/SUBSCRIBE/PUBLISH 按契约发出去。
M2 的**报警**那一半需要 ABI 增"逐条枚举告警"（现在只有 `alarm_active_count()`，没有 id/severity/state）
⇒ 拆到下一步；本节做 **状态上行 ＋ LWT ＋ 假 broker**。

### 290.2 落地

| 面 | 交付 |
|---|---|
| LWT（遗嘱） | `mqtt_client`：`mqtt_set_will(topic,payload,retain)` / `mqtt_clear_will()`；CONNECT 里带 will flag(bit2)＋QoS=0＋retain(bit5)，段顺序 ClientId → Will Topic → Will Message；`finish_connect` 缓冲 256 → **512**（加遗嘱后会不够） |
| 上线报状态 | `mqtt_client`：`mqtt_set_connect_cb()`（CONNACK rc=0 后回调一次，**不持锁**）；`variable_table.c` 的 `on_mqtt_connected()` 用契约函数发 `<pre>/status` 的 `on=1;t=…;tb=boot`，**retain=1**（与 LWT 同主题同 retain ⇒ 远程端任何时刻订阅都能立刻看到在线/离线） |
| 设备接线 | `var_groups_rebuild()` 里挂上线回调 ＋ 设遗嘱（主题/payload 都走 `mqtt_contract` 的纯函数） |
| 🩸 顺带修掉一个真缺陷 | `mqtt_start()` 里写着 `s_sub_count = 0` ⇒ 真实启动顺序（`hmi_var_init()` 先注册 `/set/`、`/cmd/` 主题，**再** `hmi_comm_start()`）下**订阅被整表清空**；没有 .so 时（不会再触发标签采纳重注册）表现为"设备连上 broker 却一条远程命令都收不到"。清空订阅是 `mqtt_clear_topics()` 的职责 |
| 假 broker 判据 | 新增 `tests/mqtt_fake_broker_test.c`：127.0.0.1 上起最小 broker（监听＋CONNACK＋SUBACK），**驱动产品代码**走完整链路（`#include variable_table.c`，含 `on_mqtt_connected` 与 `on_mqtt_message`） |

### 290.3 判据与负控

- **C 侧（第 1 步）527 → 550**（＋23）：遗嘱 flag/retain/QoS/段顺序/长度前缀/负载逐字节 ·
  容量（遗嘱 ＋ 三份 63 字符凭据）· `mqtt_clear_will` 复位 · **订阅表跨 `mqtt_start` 存续** ·
  `mqtt_clear_topics` 仍是唯一清空出口。
- **C 侧（新增第 10 步）27 条**：CONNECT 遗嘱三件事与段顺序 · SUBSCRIBE 出现 `<pre>/cmd/SV` ·
  上线自动上报 `<pre>/status`（`on=1`、`;tb=boot`、retain=1）· 下行合法信封 ⇒ **变量缓存真的被写成 7 ＋ 审计一条** ·
  target 不一致 ⇒ 不写 ＋ 留痕 · 过期 ⇒ 不写 ＋ 留痕。
- **负控 10/10**（`.workbuddy/tmp/s290-nc.py`，逐支 sha256 还原）：不置 will flag · 不置 retain ·
  will 段顺序写反 · `mqtt_clear_will` 不清标志 · 上线回调不触发 · `mqtt_start` 又清订阅表 ·
  设备接线不设 LWT · 状态不带 retain · 状态发 `on=0` · 摘掉 `/cmd/` 路由 —— 每支都红在**期望那一条**。
- ⚠ 写这段判据时自己踩了两个坑（都已修，如实记录）：① 接收侧每次调用各自 `have=0`
  ⇒ 一个 recv 里的第二个报文被静默丢掉（改成跨调用持久缓冲）；② 先等 SUBSCRIBE 会**丢掉先到的状态 PUBLISH**
  （上线回调在 CONNACK 处理里就 publish，而 SUBSCRIBE 要等下一次 poll）⇒ 改成同一循环里同时收两类。

### 290.4 ⚠ 仍未做（如实）

- **报警上行**（`<pre>/alarm/<id>` 的 `st=/sev=`）：需给 ABI 加**逐条枚举**（`alarm_snapshot`）＋ 生成器发射 ＋ 宿主周期上报；
  现有 `alarm_active_count()` 不足以产出 `st=`/`sev=`。
- broker 侧 ACL 配置物 · M3 Web UI · 远程写审计**落盘**（已接 `hmi_audit`，路径实测需板子）· 板子实测。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **10 步**）/ 日志 **1485 行**（非空 **1446**）；
负载 `6.54 / 16.30 / 11.28`；脚本 **31**；路径卫生 **740**；步 5 逐程序集
`44 / 44 / 4 / 96 / 84 / 2517 / 315 / 881 / 8`（合计 **3993**）＝ **3993 通过 / 0 失败**；
固件 host 步：通讯 **550** ＋ 新增假 broker **27**，**10 步全过**。

---

## 阶段 291（2026-10-11）—— M2 的**报警上行**（`§2-#12` M2 的最后一块）

### 291.1 缺的是什么

M2 的报警**契约**（阶段 272：`hmi_mqtt_format_alarm_topic/_payload`）早已落地，但设备侧**没有数据源**：
ABI 只有 `alarm_active_count()`（一个数字），拿不到 id / severity / state
⇒ `<prefix>/alarm/<id>` 的 `st=` / `sev=` **根本产不出来**。

### 291.2 落地

| 面 | 交付 |
|---|---|
| ABI 数据类型 | 新增 `app/runtime/hmi_alarm_snapshot.h`：`hmi_alarm_snapshot_t { char id[32]; uint16_t config_id; uint8_t state; uint8_t severity; }` —— **单独一个头、不拉 lvgl**，宿主/.so/主机判据三处共用；id 是**定长缓冲**（不让宿主攥着 .so 的字符串指针） |
| ABI 函数指针 | `hmi_ui_api.h` 尾部追加 `uint16_t (*alarm_snapshot)(hmi_alarm_snapshot_t* out, uint16_t cap)` —— **排在搁置两项之后**（尾部追加是老宿主不漏读函数指针的前提） |
| 生成器 | `CodeGenerator.Alarm.cs` 发射 `_alarm_ids[]`（与 `_alarm_lookup` **同序**）＋ `alarm_snapshot()`（id 定长拷贝 ＋ config_id/state/severity 四字段）；`SharedLibrary` 表里挂 `.alarm_snapshot`。**无报警时不产生任何符号**（空 `_alarm_ids[]` 是非法 C） |
| 宿主发布器 | 新增 `app/comm/mqtt_alarm_publish.{h,c}`：**边沿触发**（(state,severity) 变了才发）· `state==0`（Inactive）跳过 · 主题/payload 都走契约纯函数 · fetch 走**注入**（主机判据因此不拉 lvgl） |
| 设备接线 | 新增 `app/runtime/hmi_alarm_source.c`（**唯一** include `hmi_ui_api.h` 的地方）把 `alarm_snapshot` 接成 fetch；`main.c` 的 `hmi_engine_poll()` 在 `alarm_evaluate` **之后**调一次 ⇒ 快照与求值**同一线程**读，不存在跨线程读 `alarm_record_t` 的问题 |
| 时间基 | `var_uptime_ms` 提升为公开 `hmi_var_uptime_ms()`（`/val/`、`/status/`、`/alarm/` 三处同一个 `tb=boot` 口径） |

### 291.3 判据与负控

- **C 侧（假 broker 第 10 步）27 → 35**（＋8）：只发 1 条（Inactive 那条跳过）· broker 收到 `<pre>/alarm/al1` ·
  载荷逐字节 `st=1;sev=2;t=1000;tb=boot` · **状态未变一条都不重复发** · 状态变化再发一条且载荷 `st=2` ·
  `state=0` 跳过、`state=5` 被契约拒 ⇒ 一条都不发。
- **C# 侧 ＋2**（`AlarmShelveAbiTests` 现 5 例）：ABI 头声明且**排在搁置之后** · 生成表同名直赋且同序 ·
  四字段拷贝（id 定长）· **无报警时不产生** `alarm_snapshot` / `_alarm_ids` 符号。
- **负控 7/7**（`.workbuddy/tmp/s291-nc.py`，逐支 sha256 还原）：生成器不拷 state · 不拷 severity ·
  生成表不挂指针 · ABI 头不声明 · 把指针插到搁置之前（破坏尾部追加）· 摘掉边沿触发 · 忽略格式化失败。
  ⚠ **首跑 nc1/nc2 未咬住**：它们改的是**生成器**，而我最初把检查器配成了 C 侧假 broker 测试 ——
  那个测试用的是**测试侧假 fetch**，根本不跑生成器。改配 C# 侧（断言生成代码形状）后才咬住。
  **这条本身说明了分工**：生成器形状由 C# 断言、发布器行为由 C 端到端。

### 291.4 ⚠ 仍未做（如实）

- `hmi_alarm_source.c`（`.so` → fetch 的胶水）**没有主机判据**：它 include `hmi_ui_api.h`（lvgl），
  而 host 测试刻意不解析 lvgl.h。它的正确性是"一行转发"，靠两侧各自的判据
  （生成器形状 / 发布器行为）＋ 将来的板子实测。
- broker 侧 ACL 配置物 · M3 Web UI · 远程写审计**落盘**实测 · 板子实测。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **10 步**）/ 日志 **1493 行**（非空 **1454**）；
负载 `6.45 / 11.98 / 7.98`；脚本 **31**；路径卫生 **745**；步 5 逐程序集
`44 / 44 / 4 / 96 / 84 / 2519 / 315 / 881 / 8`（合计 **3995**）＝ **3995 通过 / 0 失败**；
固件 host 步：通讯 **550** ＋ 假 broker **35**，**10 步全过**。

---

## 阶段 292（2026-10-11）—— ISA-18.2 切片㈡ 收尾：`AllowedResponseMs` 面板框 ＋ 报警列表按优先级排序/着色

### 292.1 缺的是什么

登记册原话：`AllowedResponseMs`（Core 已有、`AlarmPriority` 已消费）**在编辑器侧 0 消费**；
`AlarmPriority` 在 `HmiEditor.App` **0 命中** ⇒ 优先级算得出来但**界面看不见**，
"优先级 ≠ 严重度"这条 ISA-18.2 的核心结论在编辑器里没有任何落点。

### 292.2 落地

| 面 | 交付 |
|---|---|
| 面板输入 | `AlarmConfigView` 新增 `TxtAllowedResponseMs`（行 20；标签 ＋ 悬停提示）＋ 订阅/回填/写回。口径：**空 ⇒ null**（用该严重度默认值）· 非法文本 ⇒ **保持原值**（与兄弟数值字段"TryParse 失败就不写"同口径）· `0` 合法（Core 当"立即"、按最严处理） |
| 字典 | `alarm.allowed.response` / `alarm.allowed.response.hint` 两语言各 +1 键（zh/en 键数仍相等） |
| 列表着色 | 报警列表的圆点改按**优先级**取色（`PriorityColor`：按名次复用既有严重度色板 ⇒ **不新增设计常量**，色板单一来源仍是 `UiPalette`）；**严重度继续以文字标签保留** |
| 列表记法 | 每项加 `P1…P4`（`AlarmPriority.Label`；语言无关，故不进字典） |
| 列表排序 | **显示序**按优先级（`OrderBy(AlarmPriority.Of)`，稳定排序）。⚠ **模型顺序一字不动** —— 生成器的 `config_id` 就是 `group.Alarms` 的遍历序，重排模型会让既有确认/搁置 id 全部错位 |

### 292.3 判据与负控

- **面板面**：`AlarmConfigTextBoxWiringTests` 规格表 **+4**（`120000` / 空 ⇒ null / 非法 ⇒ 不动 / `0`）；
  `AppTextBoxWiringCensusTests` 该文件 10 → **11**（普查面必须同步，否则接线无判据看着）。
- **列表面**：新增 `AlarmPriorityListSurfaceUiTests`（1 例）：显示序 P2→P3→P4（而声明序是 A/B/C）·
  每项含对应 P 记法 · **点色按优先级**（Critical 严重度 ＋ 宽响应时间 ⇒ 必须是 Warning 色、且 ≠ Critical 色）·
  **模型顺序不变式**（显示只排序，模型不许动）。
- **负控 8/8**（`.workbuddy/tmp/s292-nc.py`，逐支 sha256 还原）：摘接线 · 空文本不写 null ·
  非法文本也写进模型 · 普查计数回退 · 不按优先级排序 · 点色改回严重度 · 不带 P 记法 ·
  **改成重排模型**（这条是"只排序显示"那个决定的守门人）。

### 292.4 ⚠ 仍未做（如实）

- **升级（escalation）本体**：Core 里**没有模型也没有逻辑**（`grep -rIn escalat src/` ⇒ 0）——
  它是 ISA-18.2 切片㈡ 剩下的另一半，需先立模型与设备契约（同"契约先行"的既有做法）。
- M3 远程端展示（搁置/优先级）随 M3 · broker 侧 ACL 配置物 · 板子实测。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **10 步**）/ 日志 **1493 行**（非空 **1454**）；
负载 `8.88 / 9.67 / 6.34`；脚本 **31**；路径卫生 **745**；步 5 逐程序集
`44 / 44 / 4 / 96 / 84 / 2519 / 315 / 882 / 8`（合计 **3996**）＝ **3996 通过 / 0 失败**
（`HmiEditor.App.UiTests` **881 → 882**）。

---

## 阶段 293（2026-10-11）—— `#47b` 收口：**`416` 的产出字段定位**

### 293.1 此前到哪一步

阶段 58 已把**行为**定论并 IL 核实：headless 的 `FontManager` 由 `HeadlessFontManagerStub` 支撑，
其 `TryCreateGlyphTypeface(familyName,…)` **不看族名**、一律加载 `BareMinimum.ttf`，却返回 `true`
⇒ 每个"没见过"的族名都被当成命中登记，文本绘制成本**永久 +416 B/次**。
但登记册一直写着「**416 由哪个字段产生（哪张 `Dictionary`/`List` 在每次绘制时被重放）未定位**」。

### 293.2 定位方法（临时探针，读完即删）

在 `UiTestHost.Run` 的派发体内反射遍历 `FontManager.Current` 的实例字段（深度 ≤4，**含字典条目**），
对比"解析新族名"前后的集合计数，找按新族名个数增长的集合。

### 293.3 结论（字段与读数）

**字段 ＝ `FontManager.Current._fontCollections[<FontCollection>]._glyphTypefaceCache`** ——
一张**按族名做键**的字典。实测（一个派发内）：

- 全新平台里 `_fontCollections` 是 **0 条**；那张 FontCollection 与它的 `_glyphTypefaceCache`
  都是**首次解析时**才建出来的；
- **首次**解析一次性登记回退族 `BareMinimum` ＋ 被请求的族（**+2**）；
- 此后**每多一个没见过的族名恰好 +1 条**；
- 同一个族名重复解析 3 次 ⇒ **一条都不涨**（与阶段 58 的负控同形）。

⇒ 「哪张 Dictionary 在每次绘制时被重放」有了确切答案，也把阶段 58 那句
"平台不重建 ⇒ 族名跨用例累计 ⇒ 绝对字节预算随执行顺序漂"钉到了**具体字段**上。

### 293.4 新判据

新增 `HeadlessFontCacheGrowthTests`（1 例）：热身（排掉一次性 +2）之后，断言
**+1 个新族 = +1** · **同族重复解析 3 次不涨** · **再 +2 个新族 = +3**。
⚠ 定位失败时带着「Avalonia 内部结构变了 ⇒ 本条判据需**重新推导**（别直接删）」的消息红 ——
框架内部结构探针的正当用法。

### 293.5 ⚠ 仍未做（如实）

**`#47` 本体（间歇红）仍未选定／落地修法。** 已确证的机制是：任何线程在 `s_uiThread == null`
的窗口里读一次 `Dispatcher.UIThread` / `get_CurrentDispatcher` 就把它抢走（`Pump` 那条已闸门化，
阶段 60；普查表里另有 4 类未逐条验证的候选入口）。
候选修法 `PerTest → PerAssembly` 会**系统性上抬渲染预算** —— 阶段 58 的前置判据正是为此而设
⇒ 必须**一并改预算判据的口径**，属下一步（不是"改个枚举值"的事）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **10 步**）/ 日志 **1493 行**（非空 **1454**）；
负载 `6.95 / 10.38 / 7.61`；脚本 **31**；路径卫生 **745**；步 5 逐程序集
`44 / 44 / 4 / 84 / 96 / 2519 / 315 / 883 / 8`（合计 **3997**）＝ **3997 通过 / 0 失败**。

---

## 阶段 294（2026-10-11）—— `#47` 修法的**先决条件**：把预算判据与隔离级别解耦；并**实测排除**"切 PerAssembly"

### 294.1 为什么先做这一步

`#47` 的候选修法之一是 `PerTest → PerAssembly`（阶段 56 实测：同一偷手探针在 `PerAssembly` 下 0/2,000,000）。
但阶段 58 早就记着它"**系统性上抬渲染预算**" —— 因为绝对字节预算此前只能靠
"`PerTest` 每次派发重建平台、顺手清掉字体表"才可比。**先把这层耦合解掉**，切换才有讨论余地。

### 294.2 落地

| 面 | 交付 |
|---|---|
| 归一化助手 | 新增 `HeadlessFontCache.Clear()`：反射清空**阶段 293 定位到的那张**按族名键的 `_glyphTypefaceCache`；返回清掉的条目数（0 = 没找到 ⇒ 调用方判据会红并提示"重新推导"） |
| 预算门 | `Gate_CanvasRenderAllocation_StaysWithinBudget` 起量前归一化 ⇒ 读数不再随"之前跑过哪些用例"漂 |
| 前置判据**改写** | `Precondition_PerDispatchPlatformReset_…` → `Precondition_FontCacheNormalization_KeepsTextBudgetsComparable`：**同一个派发内**先污染再清，断言"清完离干净比离污染更近"。族名带 `Guid`（写死名字只在 `PerAssembly` 下"第一次没见过"，那个变量就不成立了）。正控（污染后必须被抬高 ≥100 B）保留 |

### 294.3 读数

`[前置条件] 干净 2,274 / 污染后 3,938 / 归一化后 **2,274** B/次（注入：4 个全新族名 ⇒ 实测抬高 **1,664**）`
—— 归一化**精确**回到干净值，抬高量正好 **416×4**（与阶段 58 的算式吻合）。

### 294.4 🩸 "切 PerAssembly"这条候选：**实测排除**（有读数）

真去切了一次并跑**全量** `App.UiTests`：**108 条红**，其中 **102 条落在 `UiTestHost.EnterGate()`**、
**103 条**是「派发闸门等待超时（疑似自锁）」。⇒ 在当前套件下，隔离切换**不是外科手术**：
它会先把闸门自己搞死。**已回滚**（`UiTestHost.cs` 与 HEAD 逐字节相同，git 无该文件改动）。

这条读数的价值：它把"切隔离"从**候选**变成**已知不可行**（除非先做一轮大的套件适配），
而不是继续挂在待办里当"也许改个枚举值就行"。

### 294.5 ⚠ 仍未做（如实）

- **`#47` 的修法仍未选定／落地**。剩下的方向：把普查表里**未逐条验证**的那几类"派发之外会读
  `Dispatcher.UIThread` / `get_CurrentDispatcher`"的入口（`AvaloniaSynchronizationContext` 三个构造、
  `MediaContext.get_Instance`、`GestureRecognizers` / `TransformTrackingHelper`）逐条验证并在 `UiTestHost` 侧闸门化。
- **跑够轮次**（发布门槛的另一半）：`#47` 只在并发大队出现（历史 ≈2/23、阶段 113 ≈1/8），单跑从未命中
  ⇒ 修法落地后要按纪律给**红/绿双读数**，不能只报"连续绿"。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **10 步**）/ 日志 **1493 行**（非空 **1454**）；
负载 `5.22 / 7.51 / 6.08`；脚本 **31**；路径卫生 **745**；步 5 逐程序集
`44 / 44 / 4 / 96 / 84 / 2519 / 315 / 883 / 8`（合计 **3997**）＝ **3997 通过 / 0 失败**
（`App.UiTests` 单跑 **883/883** 绿）。

---

## 阶段 295（2026-10-11）—— `#47` 的**一条可复现根因**（静态初始化器构造可变画刷抢 Dispatcher）＋ 修法落地

### 295.1 根因：不再依赖"无法复现的历史现场"

临时探针（读完即删）在**派发收尾之后**（`s_uiThread == null`、窗口开着）起一条**非会话线程**：

| 触碰的动作（非会话线程上） | `s_uiThread` 归属 |
|---|---|
| `new ImmutableSolidColorBrush(...)` | `null → null` —— **不抢** |
| `new SolidColorBrush(...)` | `null → 20` —— **抢走** |

与 `#47` 的错误消息**同一条因果链**：下一次派发的平台初始化走
`DefaultRenderLoop.Add → Dispatcher.VerifyAccess()`，此时 `s_uiThread` 已被别的线程持有
⇒ 抛 `The calling thread cannot access this object because a different thread owns it.`
而且**每次必现**（不再是 2/23 的现场）。

### 295.2 为什么这是"我们代码里"的入口

`static readonly IBrush X = new SolidColorBrush(...)` 的构造发生在**类型初始化器**里，
跑在"谁先碰到这个类型"的那个线程上 —— 完全可能不是会话线程、也不在任何派发内。`src/` 共 9 处：

- `Controls/ColorPickerPopup.cs` ×5（外加一个 `Brushes.White`）
- `MainWindow.axaml.cs` 的 `ResourceBrushes` ×4
- `Theme.Brush` 的**回退分支**（`AssetRow` 的两个静态字段会走到它）

### 295.3 修法（已落地）

上述各处一律改用 **`ImmutableSolidColorBrush`**（Avalonia 的无 Dispatcher 版本；仓内
`RenderResourceCache` 早已这么用）。⚠ `MainWindow.axaml.cs` 在**行数守卫**表里（基线 2983）
⇒ 该文件的改动做成**行数中性**：说明并进既有的 `/// <summary>` 行、用全限定名而**不加** `using`。
（首跑就是被这条守卫挡下的 —— 门禁把"顺手 +4 行"当场拦住了。）

### 295.4 判据与负控

- **行为面** `AmbientDispatcherTheftGuardTests`：建平台并让派发收尾（前提自检：归属必须是
  `null` 或会话线程），然后**每个画刷字段各起一条新线程**去触碰，断言"触碰之后归属 ≠ 触碰线程"（6 处）。
- **静态面** `StaticInitializerDispatcherGuardTests`：扫 `src/**/*.cs`，静态字段初始化器里不许 `new`
  会绑 Dispatcher 的可变对象（违规 0）；**反向腿** = 必须扫到 ≥3 条 `Immutable*` 静态初始化器
  （实测 **10** 条）⇒ 防"扫描面失效却永远绿"。
- **负控 3/3**（`.workbuddy/tmp/s295-nc.py`，逐支 sha256 还原）：`ColorPickerPopup` 一处退回可变
  （行为面 + 静态面**都响**）· `Theme` 回退退回可变（行为面响）· 把静态守卫的正则形状写坏（反向腿响）。

### 295.5 红/绿双读数（发布门槛要求的"跑够轮次"）

- **红**：负控 nc1/nc2 就是"修法之前"的形态 —— 触碰那些字段后 `owner` 立刻变成触碰线程，
  下一次派发必抛 `different thread owns it`。
- **绿**：修后**连续 3 轮**全量门禁 `GATE_EXIT=0` / 7 步 / **3999 通过 / 0 失败**（**0 条红**）。
- ⚠ **如实边界**：本轮只**确证并修掉了一条**根因（不再依赖历史那 2/23 的现场）；
  普查表里尚未逐条验证的其它入口（`AvaloniaSynchronizationContext` 三个构造、
  `MediaContext.get_Instance`、`GestureRecognizers`/`TransformTrackingHelper`）**仍在册**。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **10 步**）/ 日志 **1493 行**（非空 **1454**）；
负载 `4.75 / 6.60 / 9.55`；脚本 **31**；路径卫生 **745**；步 5 逐程序集
`44 / 44 / 4 / 84 / 96 / 2519 / 315 / 885 / 8`（合计 **3999**）＝ **3999 通过 / 0 失败**；
另两轮复跑同为 `GATE_EXIT=0` / 0 条红。

---

## 阶段 296（2026-10-11）—— 卫生项（§12 第 6 项；用户拍板：MIT ＋ 删 `build/*.ps1`）

### 296.1 拍板

用户 2026-10-11 定：**LICENSE = MIT**；**`build/*.ps1` 删除**（那 5 个是 Windows 专用：
NativeAOT/win-x64 交叉编译 ＋ 进程回收，而本工程 Linux 为主力平台）。

### 296.2 落地

| 项 | 交付 |
|---|---|
| **LICENSE** | 新增根 `LICENSE`（MIT，`Copyright (c) 2026 zhishu-hmi contributors`）。此前仓库**没有任何许可证文件** ⇒ 对外分发等于"未授权" |
| **`build/*.ps1`** | `git rm` 5 个（`build`/`clean`/`publish-linux-arm64`/`run`/`test-linux-arm64.ps1`），空目录随之消失。发布仍走 `scripts/package-release.sh` ＋ `tools/ReleasePackager` |
| **`.trae-*.zip`** | `git rm --cached` 2 个 IDE 分享包（约 2.2 MB；**文件留在磁盘**）＋ `.gitignore` 新增 `.trae-html-share-packages/` |
| **明文口令** | `runtime_config.h` 的 `HMI_MQTT_PASS "hmi123"` 加**"出厂默认、部署必改"**标注：块注释写清改法（设备 `/mnt/UDISK/hmi/comm_config.ini` 优先；broker 侧开 `allow_anonymous=false` ＋ 主题 ACL），定义行尾再加一个标记 |
| **Mock 夹具** | `generated/screens.c` 头部标明**内置样例（非产品工程）**；真正"换掉"需要仓内**没有**的 `.hmi` 源工程 ⇒ 留在册并写明前置（见 296.5） |

### 296.3 判据与负控 4/4

- `ShippedCredentialHygieneTests`（Core.Tests）：① **必须有** —— 口令行 ±12 行内必须有"部署必改"；
  ② **不许有** —— 设备源码里不得再出现**其它**硬编码凭据宏（`*PASS*`/`*PASSWORD*`/`*SECRET*`/`*TOKEN*` = "…"），
  且带**面自检**（同一张正则必须在已知文件里命中 `HMI_MQTT_PASS`，否则该条会退化成永远通过）。
- `RepoHygieneGuardTests`（Core.Tests）：LICENSE 必须是拍板的 MIT；`build/` 下不得再有 `.ps1`。
- **负控 4/4**（`.workbuddy/tmp/s296-nc.py`，逐支 sha256 还原）：抹掉标注 · 新增凭据宏 ·
  把凭据正则写成死的 · `build/` 里塞回 `.ps1`。
- ⚠ **过程记一笔**：nc1 首跑**没咬住** —— 原因不是判据不灵，而是我的 NC 脚本只替换了**第一处**
  （"部署必改"在块注释与定义行各出现一次）。手工复现证明判据会红，改成**全量替换**后 4/4 咬住。
  这是"负控没红，先怀疑负控"的又一例。

### 296.4 顺带核实（不必动）

`tests/HmiEditor.App.UiTests/TestResults/` 下大量 `.trx`/GUID 目录看着像残留，**实际未被跟踪**
（`git ls-files` = 0）且 `.gitignore:12` 早已有 `TestResults/` ⇒ 不构成仓库卫生问题。

### 296.5 ⚠ 仍未做（如实）

**Mock 夹具的"换掉"**：仓内没有任何 `.hmi` 源工程，也无"导出即生成 `firmware/t113/generated/`"的
可脚本化入口 ⇒ 本轮只能把它**标注**为内置样例（内容仍是 Mock 工程）。要真正换掉，前置是
**先有一个可再导出的示例工程**（或拍板"固件夹具改为构建期生成、不入库"）。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **10 步**）/ 日志 **1493 行**（非空 **1454**）；
负载 `7.35 / 15.55 / 16.49`；脚本 **31**；路径卫生 **745**；步 5 逐程序集
`44 / 44 / 4 / 84 / 96 / 315 / 2523 / 885 / 8`（合计 **4003**）＝ **4003 通过 / 0 失败**
（`HmiEditor.Core.Tests` **2519 → 2523**）。

---

## 阶段 297（2026-10-11）—— `#47` 窄尾巴：普查表里那几类入口**逐条验证完毕**

### 297.1 实测（派发收尾后 `s_uiThread == null`，由**非会话线程**触碰）

| 入口 | 实测 |
|---|---|
| `Dispatcher.UIThread`（**正控**） | 归属 `null → 触碰线程` ⇒ **抢** |
| `Dispatcher.CurrentDispatcher`（**正控**） | **抢** |
| `new AvaloniaSynchronizationContext()` | **抢** |
| `new DispatcherTimer()`（阶段 60 记的路径） | **抢** |
| `MediaContext.Instance`（getter 是 public） | **抢** |
| `GestureRecognizers` | 本版本 Avalonia **无此类型** ⇒ 显式销案 |
| `TransformTrackingHelper`（`Avalonia.Utilities` / `Rendering.Composition` 两个命名空间） | 同上，**无此类型** |

### 297.2 仓库侧（谁在派发之外碰它们）

- `AvaloniaSynchronizationContext` 与 `MediaContext.Instance`：**全仓零调用点**。
- `new DispatcherTimer`：**只在 `src/`**（`DesignCanvas.Input`、`AutoExportService`、`SimulatorView`、
  `AnimationEditorView`、`ScriptEditorView` …），都在 UI 线程路径上；`tests/` 零直接调用。
- `Dispatcher.UIThread`：`src/` 若干（都在 UI 线程）、`tests/` **27 次**（都在派发体内、合法）。

### 297.3 新判据 `AmbientEntryPointInventoryTests`

- **静态面（强制）**：`tests/` 里不得出现 `new …DispatcherTimer(`、`new …AvaloniaSynchronizationContext(`、
  `…Dispatcher.CurrentDispatcher`、`…MediaContext.Instance` 的直接调用。
  ⚠ 用**正则**而不是字面子串 —— 负控实测过：写成 `"new DispatcherTimer"` 会被
  `new Avalonia.Threading.DispatcherTimer()`（全限定名）绕过，那正是"守卫看着在、其实没守"。
- `Dispatcher.UIThread` **降为读数**（27 次）："在不在派发体内"静态判不出来（要 lambda 级分析，本轮没做），
  其派发之外的安全性由 `UiTestHost.Pump` 的闸门 ＋ 已落地修法兜着 —— **这条边界写在判据注释里**。
- **机制面（诊断，默认不跑）**：见 297.4。

### 297.4 🩸 关键教训：机制判据**不能**在套件进程里跑

第一版把机制面做成常驻判据，结果是**全量套件 38 条红**、全是 `#47` 的平台初始化签名
（`The calling thread cannot access this object because a different thread owns it`）。
而且**不是"忘了还原"**：抢完立刻把 `s_uiThread`/`s_currentThreadDispatcher` 写回 `null`
（`ResetAmbientUIThreadOwner`，全程还占着派发闸门），**38 红照旧** ⇒ 这些入口被触碰时会污染
**进程级静态缓存**（不是改一个字段就能还原的）。

⇒ 机制面改成**环境变量开启的单跑诊断**：
```
HMI_RUN_THIEF_DIAGNOSTIC=1 dotnet test tests/HmiEditor.App.UiTests \
  --filter FullyQualifiedName~KnownAmbientEntryPoints_StillStealTheDispatcher
```
单跑读数：五条 `after=触碰线程`、`restored=True ownerNow=null`。默认路径**不执行**并打印
"本期未开诊断（默认不跑：…）"——刻意不装成绿判据。

### 297.5 判据与负控 3/3

新增 `UiTestHost.WithoutDispatch`（占闸门跑"派发之外"的代码）＋ `ResetAmbientUIThreadOwner`（还归属）。
**负控 3/3**（`.workbuddy/tmp/s297-nc.py`）：另一个测试文件里 `new DispatcherTimer` · 机制面触碰改无害动作 ·
**新文件**里塞一处直接调用（证明扫描按目录覆盖，不只看已存在的文件）。逐支 sha256 还原。

### 297.6 ⚠ 仍未做（如实）

`tests/` 里 `Dispatcher.UIThread` 那 27 处**没有机械保证它们在派发体内** —— 需要 lambda 级静态分析
（本轮没做）。目前靠"派发闸门 ＋ 现象一旦出现就是 `#47` 签名"兜着。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **10 步**）/ 日志 **1493 行**（非空 **1454**）；
负载 `4.23 / 10.07 / 6.85`；脚本 **31**；路径卫生 **745**；步 5 逐程序集
`44 / 44 / 4 / 84 / 96 / 2523 / 315 / 887 / 8`（合计 **4005**）＝ **4005 通过 / 0 失败**；
`App.UiTests` 单跑 **887/887**（43 秒）。

---

## 阶段 298（2026-10-11）—— `#47` 最后一条边界收口：`Dispatcher.UIThread` 从"靠现象"变"机械保证"

### 298.1 边界是什么

阶段 297 留的那条：`tests/` 里有 **27 处** `Dispatcher.UIThread`，**没有机械保证**它们都在派发体内 ——
要机械保证得做 lambda 级静态分析。

### 298.2 换思路：不写分析器，改成「唯一入口 ＋ 运行期断言」

- 新增 **`UiTestHost.UIThread`**：**派发体内取 Dispatcher 的唯一合法入口**；不在派发体内就抛，
  并附现场读数（`DescribeSessionState`）。这样"在不在派发里"由**运行期**回答，而不是靠静态猜。
- 把那 27 处里的**真实代码**用法（9 个文件 **19 处**：`…Dispatcher.UIThread.RunJobs(…)` 18 处
  ＋ 哨兵里的 `var ui = Dispatcher.UIThread;` 1 处）全部改走访问器；其余是注释与断言消息里的**提及**，不动。
- **静态面**：扫描前**剥注释 ＋ 掩字符串**（`MaskLiterals`），把 `Dispatcher.UIThread` 收进
  「不许直接引用」清单，要求 **0 命中**；宿主文件是唯一例外，但**只许一处**（访问器内部那行），
  多一处即红、0 处说明访问器被删。

### 298.3 判据与负控 4/4（＋1 支额外）

- `AmbientEntryPointInventoryTests`：静态面 0 命中 · 宿主"只许一处" · 机制面单跑诊断。
- 新增 `UIThreadAccessor_RefusesOutsideADispatch_AndWorksInsideIt`：派发之外**必须抛**（消息点出 `#47`）、
  派发体内返回的 Dispatcher **必须归派发线程**。
- **负控 4/4 ＋ 额外支**（`.workbuddy/tmp/s298-nc.py`，逐支 sha256 还原）：另一个测试文件直接引用 ·
  宿主多一处原始读取 · 拿掉访问器断言 · 机制面触碰改无害 · 新文件里塞一处调用。

### 298.4 过程记两笔（都是"先绊倒自己"）

1. 访问器首版用 `_gateDepth > 0` 判"在派发体内" ⇒ 哨兵 `UIThreadDispatcher_IsOwnedByTheCallingThread`
   **当场红**：**派发体跑在会话线程上**，而闸门是**调用线程**持有的 ⇒ 体内 `_gateDepth` 是 0。
   改用与 `Pump` 同一口径的**会话线程**判定后通过。
2. nc2 首版"注入"是用一处原始读取**替换**了另一处（新旧两段各提到一次）⇒ 净增 0，判据照绿。
   改成"新增一行"后才咬住 —— **判据本身没问题，是负控写错了**。

### 298.5 ⚠ 仍未做（如实）

静态面管的是"**直接引用**"；若有人写出**间接**的环境访问（例如反射拿 getter、
或经第三方库在派发之外碰平台），本判据**抓不到** —— 那类只能靠机制面诊断（单跑）与现象兜。
这条边界写在判据注释里。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **10 步**）/ 日志 **1493 行**（非空 **1454**）；
负载 `8.26 / 12.84 / 9.87`；脚本 **31**；路径卫生 **745**；**0 警告**；步 5 逐程序集
`44 / 44 / 4 / 96 / 84 / 2523 / 315 / 888 / 8`（合计 **4006**）＝ **4006 通过 / 0 失败**；
`App.UiTests` 单跑 **888/888**（41 秒）。

---

## 阶段 299（2026-10-11）—— 固件诊断日志清理（§12 第 5 项里**不需要板子**的那一段）

### 299.1 缺口

台账 §5-③：「诊断 DBG 日志（`main.c` / `lcd_sunxifb.c` / `sunxifb.c`）待清理」。实测形态分两类：

| 形态 | 问题 |
|---|---|
| 19 处 `HMI_LOG_DEBUG("DBG: …")` | **已被级别闸门管住**（默认门槛 Warning ⇒ DEBUG 静默），只是消息自带冗余 `"DBG: "` 前缀 |
| `sunxifb.c` **6 处裸输出**（2 处 `fprintf(stderr, "DBG: …")` ＋ 4 处 `printf("Turn on …")`） | **绕过级别闸门** —— 设备端默认门槛下**照样**打到 stderr/stdout |

即：真正的问题不是"日志太多"，而是**有一类日志不受工程设置的日志级别管辖**。

### 299.2 清理

- `sunxifb.c`：6 处裸输出 → `HMI_LOG_DEBUG(...)`（宏自带 `\n`，格式串去掉尾换行；`perror(...)` 错误上报**不动**）。
- 三个文件的消息里去掉冗余 `"DBG: "` 前缀（级别由宏表达）——`lcd_sunxifb.c` 7 处 ＋ `main.c` 12 处。
- **唯一保留**的裸 `printf` 是 `sunxifb.c` 里那份 FPS 统计：它在 `#if LV_USE_SUNXIFB_DEBUG` **编译期开关**内，
  本来就"要看得自己打开"，不属于绕过闸门。

### 299.3 判据与负控 4/4

新增 `FirmwareDebugLogHygieneTests`（Core.Tests）：

- **不许有**：设备源码里不得再出现消息自带 `"DBG:"` 前缀的诊断（在**原始**文本里找 —— 掩字符串后就找不到了）。
- **反向腿**：整个设备源码里分级日志调用数 **≥ 20**（实测 29）⇒ 防"一刀删光也算清理"
  （删日志不是清理，是把现场排查能力删掉）。
- **裸输出定界**：`lcd_sunxifb.c` 与 `main.c` 的裸 `printf/fprintf(stderr)` **必须为 0**；
  `sunxifb.c` **≤ 1** 且必须存在 `LV_USE_SUNXIFB_DEBUG` 开关（豁免理由得成立）；`perror(...)` 不受限。

**负控 4/4**（`.workbuddy/tmp/s299-nc.py`，逐支 sha256 还原）：重新引入裸 `fprintf(stderr, "DBG: …")` ·
把分级调用换成别的宏（反向腿下界破）· 给 `lcd_sunxifb.c` 加裸 `printf` · 给 `sunxifb.c` 再加一处裸 `printf`。

⚠ 过程记一笔：nc2 首版把 `HMI_LOG_DEBUG` 改成 `HMI_LOG_INFO` —— 而计数集合是
`HMI_LOG_(DEBUG|INFO|WARN|ERR)`，总数不变 ⇒ **假绿**。换成集合外的宏名后才咬住（判据没问题，负控写错了）。

### 299.4 ⚠ 仍未做（如实）

§12 第 5 项另两段都要**板子**：**GT911 整编烧录复验** · **RS485 UART3 dts 验证**。
本轮**没有**碰它们，也没有声称验证过。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **10 步**）/ 日志 **1493 行**（非空 **1454**）；
负载 `6.15 / 10.27 / 6.06`；脚本 **31**；路径卫生 **745**；**0 警告**；步 5 逐程序集
`44 / 44 / 4 / 96 / 84 / 2525 / 315 / 888 / 8`（合计 **4008**）＝ **4008 通过 / 0 失败**
（`HmiEditor.Core.Tests` **2523 → 2525**）。

---

## 阶段 300（2026-10-11）—— TLS：拍板落地 ＋ **设备侧接线**（把已落地的契约真正接上）

### 300.1 用户拍板（已记入登记册 §0.2）

| 条 | 拍板 |
|---|---|
| `device-tls-assessment` §7-1 先行路线 | **两者都做**（PSK ＋ X.509 都实现，按现场选 `mqtt.tls=psk\|ca`）⇒ §6-4 因此**可全判** |
| §7-2 是否允许显式降级 | **① 永不降级**：只有显式配 `mqtt.tls=off` 才走明文，且启动时审计一条 |

### 300.2 先发现了一个**闭环缺口**

阶段 282–284 的账是"契约级 ✅"，实测下来：生成器**确实**写 `mqtt.tls.*` 键（R3 ✅）、
C 与 C# 两侧契约**都在**、判据也在 —— 但是

- `comm_core.c` 的 INI 读取**一条 `mqtt.tls*` 键都没读**；
- `hmi_mqtt_tls_resolve_endpoint` / `assert_no_bypass` 在 C 侧**零调用点**。

⇒ **生成的配置在设备上不生效**：`mqtt.tls=psk` 写了等于没写。这正是"契约级 ✅ / 运行期 ❌"
那两格的实质。

### 300.3 落地

| 面 | 交付 |
|---|---|
| 契约补一层 | `mqtt_tls_contract.{h,c}` 新增 `hmi_mqtt_tls_resolution_t` ＋ **`hmi_mqtt_tls_resolve_config()`**：把「读到的键」**一次算成**「客户端该连哪里 ＋ 能不能用 ＋ 拒绝原因」。内部依次 parse → validate → resolve → **bypass 断言**（把 R2 从"测试里断言"变成"运行期路径上断言"） |
| **fail-closed 形状** | 失败时把 `host` 清空、`port` 归零 ⇒ 调用方**即便漏判返回值**也拿不到一个"能连上 broker"的地址 |
| 原因词汇 | 稳定 ASCII 标识：`tls-mode-unknown` / `tls-config-incomplete` / `tls-endpoint-invalid` / `tls-bypass`（落审计，**不含密钥**） |
| 运行期接线 | `comm_core.c` 读 5 个 TLS 键 → `comm_tls_effective()` 决定 → 非法则 `comm_tls_refuse_notice()`：**MQTT 不启动 ＋ 审计 `mqtt-tls-config-refused`**（Modbus 等链路照常）；合法则客户端连解析出的地址 |
| 构建 | `run_host_tests.sh` 第 10 步补 `mqtt_tls_contract.c`（否则链接不过 —— 这正是"接线真的发生了"的副证） |

### 300.4 判据与负控 4/4

- **纯函数面**：C 侧新样例表 **14 行 × 5 断言**（含"缺键=NULL/空串 ⇒ off"的兼容行）
  ＋ 三支 fail-closed 总断言（psk 缺件 / ca 缺件 / 未知模式都**不得**留下可连地址）。
- **接线面**：键解析 5 条（含空白裁剪）＋ `comm_tls_effective` 8 组（off 直连 / psk 回环 local_port /
  ca 缺省端口 / 缺件拒绝 / 未知模式拒绝 / 端口 0 拒绝 / 审计事件名与原因 / 空出参）。
- **负控 4/4**（`.workbuddy/tmp/s300-nc.py`，逐支 sha256 还原）：
  **配置非法时把 broker 地址交出去（经典降级）** · 未知模式串当 off（静默降级，纯函数面也响）·
  拒绝时不再落审计 · 少读一个 TLS 键。

### 300.5 ⚠ 仍未做（如实）

1. **代理二进制本体**（路线 B 的唯一新代码）：OpenSSL 链接 · PSK 回调 · **X.509 路径** ·
   重连 · respawn · 只绑回环 · 握手失败留痕。
2. **init 脚本四条**（§6-6）：respawn / 先于 hmi_runtime / 只绑回环 / 失败不降级。
3. **板子实测**（§6-5/§6-7 尺寸与内存）—— 需真板。
4. **跨语言同源**：本阶段新增的 `hmi_mqtt_tls_resolve_config()` 是**组合函数**，
   目前**只在 C 侧**（理由：`reason` 是**设备审计**词汇，编辑器不消费它；
   既有的跨语言同源样例（端点解析 9 条 / 校验 7 条）未变）。这条边界**刻意写在这里**。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **10 步**）/ 日志 **1595 行**（非空 **1556**）；
负载 `1.33 / 8.30 / 7.72`；脚本 **31**；路径卫生 **745**；**0 警告**；步 5 逐程序集
`44 / 44 / 4 / 96 / 84 / 2525 / 315 / 888 / 8`（合计 **4008**）＝ **4008 通过 / 0 失败**；
固件 `hmi_comm_test` **550 → 648**。

---

## 阶段 301（2026-10-11）—— TLS 代理：**配置契约**（纯函数面，不含 OpenSSL）

### 301.1 为什么先做契约层

拍板（§7-1 两者都做 / §7-2 永不降级）之后按仓里的"契约先行"推进代理。
契约层能判的范围：代理**该怎么起**（监听/上游/凭据）与**能不能起**（状态码），
这些**不需要 OpenSSL、不需要板子**；真正的握手留到二进制那一轮。

### 301.2 落地：`firmware/t113/app/comm/tls_proxy_config.{h,c}`

| 面 | 内容 |
|---|---|
| **退出码契约** | `0` 可用 · `3` **不需要代理**（`mqtt.tls` 缺键或 `off` ⇒ 脚本正常跳过）· `4` **配置非法**（不启动 ＋ 审计）。数值写死在头文件里 —— init 脚本按数值分派 |
| 纯函数 | `hmi_tls_proxy_resolve()`：把读到的键算成「监听/上游/凭据 ＋ 状态 ＋ 原因」 |
| 文件入口 | `hmi_tls_proxy_config_load()`：**唯一的文件 I/O 点**（读同一个 `comm_config.ini`，键与客户端**没有交叠**） |
| 原因词汇 | `tls-proxy-not-needed` / `tls-mode-unknown` / `tls-config-incomplete` / `tls-endpoint-invalid`（落审计，**不含密钥**） |

### 301.3 三条关键决定（都是"会被判死"的那种）

1. **监听地址恒为 `127.0.0.1`**：没有任何键能改它 —— 契约 §4 明说"绑 `0.0.0.0` 等于把 LAN 的
   明文 MQTT 暴露出去，**比不做 TLS 更危险**"。判据直接断言这个字段恒等于回环常量。
2. **`off`／缺键 ⇒ 状态 3「不需要代理」**，与「模式串不认识 ⇒ 状态 4 拒绝」**严格区分**：
   前者是"没配 TLS"（正常），后者是"配错了"（拒绝）。把它们混成一个状态，
   就等于给了 init 脚本一个"把配置错误当正常跳过"的机会。
3. **fail-closed**：非 0 状态一律不留下监听/上游端点（`listen_port`/`upstream_port` 归零、
   host 清空）—— 调用方即便漏判状态码也拿不到一个可用端点。

### 301.4 判据与负控 4/4

- 纯函数面：**12 行样例 × 3 断言**；`status == 0` 的行再查「监听恒回环 ＋ 监听/上游端口 ＋ 上游 host」，
  非 0 的行查 fail-closed；另加空出参一支。
- 文件读取腿：同一份 INI（含**客户端读的 `mqtt.host/port`**）⇒ 代理只取自己的键（`listen=127.0.0.1:11883`、
  `upstream=10.0.0.5:8883`）；INI **不存在** ⇒ 状态 3。
- **负控 4/4**（`.workbuddy/tmp/s301-nc.py`，逐支 sha256 还原）：**监听绑 `0.0.0.0`** ·
  模式未知当"不需要代理" · 配置非法仍交出 upstream（fail-closed 破）· INI 里少读 `mqtt.tls` 键。
- ⚠ **过程记一笔**：判据**当场抓到实现与语义不一致** —— 显式 `mqtt.tls=off` 我一开始返回了 `0`（可用），
  按契约应为 `3`（不需要）。这正是"样例表先写对、实现被纠"该有的样子。

### 301.5 ⚠ 仍未做（如实）

**代理二进制本体**：OpenSSL 链接 · PSK 回调 · **X.509 路径** · 重连 · respawn · 只绑回环（已由契约钉住）
· 握手失败留痕。**init 脚本四条**（§6-6）。**板子实测**（§6-5/§6-7）。
主机侧 OpenSSL 是 **3.6.5**、设备侧镜像里是 **1.1.0i** ⇒ 二进制那轮要写明这层版本落差怎么处理。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **10 步**）/ 日志 **1669 行**（非空 **1629**）；
负载 `4.19 / 5.74 / 4.62`；脚本 **31**；路径卫生 **747**；**0 警告**；步 5 逐程序集
`44 / 44 / 4 / 96 / 84 / 2525 / 315 / 888 / 8`（合计 **4008**）＝ **4008 通过 / 0 失败**；
固件 `hmi_comm_test` **648 → 721**。

---

## 阶段 302（2026-10-11）—— **代理二进制落地**（路线 B 的唯一新代码）＋ 主机侧真 TLS 端到端

### 302.1 交付：`firmware/t113/app/proxy/tls_proxy.c`

| 面 | 做法 |
|---|---|
| 拓扑 | 明文听 `127.0.0.1:<local_port>`（**地址由契约写死**，代码里也**不接受任何覆盖**）→ 对 `upstream_host:port` 走 TLS |
| **两条路径** | `psk`（`SSL_CTX_set_psk_client_callback` ＋ 十六进制密钥文件）／`ca`（`SSL_CTX_load_verify_locations` ＋ `X509_VERIFY_PARAM_set1_host` 主机名校验）；显式收窄到 TLS1.2（目标是镜像里的 OpenSSL **1.1.0i**） |
| **§6-4 未校时** | X.509 握手**之前**判时钟：早于 `2020-01-01` ⇒ **拒绝 ＋ 审计** `mqtt-tls-clock-unsynced`；**PSK 路径不受影响** |
| **§6-2 永不降级** | 握手失败／上游不可达 ⇒ **关掉客户端连接**，绝不改成明文转发。`tls_upstream()` 失败只审计"大类"，**不把任何材料写进审计** |
| **§6-3 缺件拒启动** | 配置层判"非空"，进程再判"文件真的在"（`access(R_OK)`）⇒ 否则退出 4 ＋ 审计 |
| 留痕 | 审计行与 `hmi_actions.c` 的 `hmi_audit` **同一行形状**（`stamp event user= role= detail`） |
| 用法 | `--check`（按 **0/3/4** 退出，供 init 脚本分派）· `--config PATH` · `--once`（处理一个连接即退出，供测试） |
| 测试缝 | `HMI_TLS_PROXY_AUDIT`（审计路径）· `HMI_TLS_PROXY_MIN_EPOCH`（模拟"无 RTC 开机早期"，§6-4 用） |

### 302.2 判据：主机侧**真 TLS** 端到端（固件主机测试**第 11 步**）

新增 `firmware/t113/tests/check_tls_proxy.sh`（**19 条断言**），`run_host_tests.sh` 从 10 步变 **11 步**：

- 用 `openssl s_server -psk …`（PSK）与自签 CA（X.509）起**真 TLS 假 broker**，用 bash `/dev/tcp` 当"设备客户端"
  ⇒ **双向**端到端：设备→broker 与 broker→设备都真的穿过 TLS（不是只握个手）。
- `ss -tln` **实测监听地址只绑回环**（并断言**没有**绑 `0.0.0.0`）—— 契约 §4 落成运行期读数。
- 退出码 0/3/4；`--check` **不落审计**（校验不是事件）。
- §6-4：CA ＋ 未校时 ⇒ 客户端**一个字节都收不到** ＋ 审计；PSK ＋ 同一条件 ⇒ 照常。
- §6-2：上游不可达 ⇒ 客户端**一个字节都收不到** ＋ 审计 `mqtt-tls-upstream-unreachable`。

**负控 5/5**（`.workbuddy/tmp/s302-nc.py`，逐支 sha256 还原，四条都红在**预期的那条断言**上）：
监听绑 `0.0.0.0` · 去掉 §6-4 时钟闸门（实得 `PONG-CA`）· `off` 被当成"可用" · `--check` 落审计 ·
缺 PSK 密钥文件照启动。

### 302.3 过程记两笔（都是"测试/判据自己被绊倒"）

1. **`signal()` 默认带 `SA_RESTART`** ⇒ `accept()` 被自动重启，SIGTERM 收得到但**退不出来**
   （症状是测试 **hang** 21 分钟）。改成 `poll(200ms)` ＋ `accept`。
2. **§6-3 那条判据在前台起代理且没加超时** —— 负控一注入"照启动"，它就把**整步挂死**。
   这是**判据自身的健壮性缺陷**（不是被测代码的），已改成 `timeout 5 … --once`。

### 302.4 设备侧构建接入

- `app/Makefile`：新增 `hmi-tls-proxy` 目标（链 `-lssl -lcrypto`，复用 `tls_proxy_config.o` / `mqtt_tls_contract.o`）。
- 固件包 `Makefile`：`DEPENDS` 加 `+libopenssl`，`install` 装 `/usr/bin/hmi-tls-proxy`。
- ⚠ **如实**：这两处**没有在设备工具链上编译过**（本机无 Tina SDK 交叉工具链）。主机侧编的是同一个源文件
  （OpenSSL **3.6.5** vs 设备 **1.1.0i**，已用 `OPENSSL_VERSION_NUMBER` 分支处理初始化差异并显式收窄 TLS1.2）；
  真正的设备侧编译验证**要等有 SDK 的环境**。

### 302.5 ⚠ 仍未做

**init 脚本四条**（§6-6：respawn / 先于 hmi_runtime / 只绑回环 / 失败不降级）—— 契约与 `0/3/4` 退出码已就位，
脚本本身还没写。**板子实测**（§6-5/§6-7 尺寸与内存）。X.509 的**轮换/运维文档**。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **11 步**）/ 日志 **1691 行**（非空 **1651**）；
负载 `0.37 / 0.91 / 2.41`；脚本 **31**；路径卫生 **749**；**0 警告**；步 5 逐程序集
`44 / 44 / 4 / 96 / 84 / 2525 / 315 / 888 / 8`（合计 **4008**）＝ **4008 通过 / 0 失败**；
固件主机 **11 步全过**（第 11 步 19/19）。

---

## 阶段 303（2026-10-11）—— init 脚本四条要求（契约 §4）落地

### 303.1 改了什么

`firmware/t113/tina/package/hmi-runtime/files/S97hmiruntime` 新增 `start_tls_proxy()` 并在
`start_service()` 里调用、`stop_service()` 里收尾：

| 契约 §4 要求 | 落地 |
|---|---|
| ① **procd 托管 ＋ respawn** | `procd_open_instance hmi-tls-proxy` ＋ `procd_set_param command … >>$PROXY_LOG 2>&1` ＋ **`procd_set_param respawn`**；非 procd rootfs 走 `start-stop-daemon` 回退（**无 respawn**，与 hmi_runtime 同款、代价同样写在注释里） |
| ② **代理先于 hmi_runtime 可用** | 调用点在 hmi_runtime 的 procd 实例**之前**。⚠ 但**不能更早**：代理的配置在 **UDISK** 上 ⇒ 必须**先挂好 UDISK**、再起代理、再起宿主（早于挂载会读到"没有配置"⇒ 退出码 3 ⇒ **代理静默不起**，那是最难查的一类故障） |
| ③ 监听**只绑回环** | 代码里写死 `HMI_TLS_PROXY_LISTEN_HOST`（无任何键能改）；脚本**不传**任何监听地址；运行期由 `ss` 实测（第 11 步） |
| ④ 失败**不改变** `mqtt.tls` 取值 | 脚本**不写** `comm_config.ini`、**不赋** `mqtt.tls`；三支（0/3/4）都只是"起或不 起"，绝不把客户端切去直连 broker |

**退出码分派**：`--check` 问代理 → `0` 起（procd 实例）· `3` `mqtt.tls=off` ⇒ **正常跳过** ·
其余 ⇒ **不启动**，并**不带 `--check` 再跑一次**让它走"配置非法"分支**落审计**后自行退出（不会进监听）。

### 303.2 判据：文本级守卫（分工明确）

新增 `TlsProxyInitScriptGuardTests`（Core.Tests）：读脚本断言 ①实例块含 `respawn` ·
②`start_tls_proxy` 在 **UDISK 挂载之后、hmi_runtime 实例之前** · ③脚本与代理源码都不含 `0.0.0.0` ·
④不写配置文件（含 `$COMM_CFG`/`${COMM_CFG}` 变量形式）、不赋 `mqtt.tls` · `--check` 三支齐全 ·
stop 也停代理。

**为什么是文本级**：契约 §6 给这条判据定的形状就是「读 init 脚本断言」——脚本要在设备 busybox/procd 上跑，
主机上判不了**行为**；但它的**结构**（谁先谁后、有没有 respawn、失败那支干什么）正是 §4 要守的。
**决策本身不在这里判**：`0/3/4` 的语义由 `check_tls_proxy.sh` 用**真 TLS 端到端**判（19 条）。
两边分工：那边判"代理说得对"，这边判"脚本接得对"。

### 303.3 负控 7/7（其中一支**连过两轮**）

6 支注入 ＋ 1 支额外（把 `start_tls_proxy` 挪到 UDISK **之前**）全部咬住，逐支 sha256 还原。
⚠ **nc4 是"判据自身的漏洞"被负控连着抓了两次**：第一轮我只认**字面路径** `comm_config.ini`
⇒ 注入 `> "$COMM_CFG"` 溜过去；补上变量形式后仍溜过去 —— 因为我在那条检查里也剥了引号，
**把 `"$COMM_CFG"` 里的变量一起剥掉了**。最终定成：写文件检查用**原行**、`mqtt.tls=` 检查才剥引号。
这条"判据改了三版才咬住"如实记在这里。

### 303.4 ⚠ 仍未做

板子实测（§6-5/§6-7 尺寸与内存）· 设备工具链上的编译验证（本机无 Tina SDK）· X.509 轮换/运维文档。

**读数（工作树）**：`GATE_EXIT=0` / **7 步**（固件内部 **11 步**）/ 日志 **1691 行**（非空 **1651**）；
负载 `5.80 / 9.11 / 7.01`；脚本 **32**；路径卫生 **749**；**0 警告**；步 5 逐程序集
`44 / 44 / 4 / 96 / 84 / 2526 / 315 / 888 / 8`（合计 **4009**）＝ **4009 通过 / 0 失败**。

---

## 阶段 318（2026-10-11）—— ② 甲：**按当前生成器重新生成设备树** ＋ "可再导出"升级为逐字节相等

### 318.1 做了什么

1. **加回 5 个控件绑定**（甲的口径：以当前生成器为准）—— 当前 `RuntimeBindingEngine` 从**控件绑定**
   取标签，所以要让 `tags` = 5，绑定必须在。
2. `tools/DeviceExport` 重新生成并覆盖 `firmware/t113/generated/`：
   `screens.c` 23,429 → **25,938 B** · `tags.h` 4,469 → **3,091 B** · `tags.c` 850 → **1,945 B** ·
   `generated_security.h` 7,731 B（不变）。
3. **「非产品工程」标注改为来自数据**：门禁当场拦下 `DeviceFixtureProvenanceGuardTests` ——
   重生成冲掉了阶段 296 我**手工加**的那行标注。修法不是再手工加回去，而是
   ① 生成器发 `// Description:`（工程 `description` 非空时；对真工程也有用），
   ② 把标注写进示例工程的 `description` ⇒ **标注经得起重生成**。
4. **"可再导出"判据升级为逐字节相等**：`DeviceExportReproducibilityTests` 现在断言
   `screens.c` / `tags.h` / `tags.c` / `generated_security.h` **四类全部逐字节等于**库中设备树
   （④=1 去掉 git sha 之后这才可能）＋ 面自检。**已通过。**

### 318.2 读数

`GATE_EXIT=0` / 7 步 / **4012 通过 / 0 失败 / 0 警告**（判据数不变，两条判据被改写）。
`DeviceTagsContractDriftTests` 在**新的** `tags.*` 上仍然通过（因为现在它们就是生成器产物）。

### 318.3 ⚠ 仍未做

**③**：让 `screens.c` **不入库** ＋ 把生成步骤接进设备构建链。

---

## 阶段 317（2026-10-11）—— ④ 生成器戳**只留版本号**（导出可逐字节复现）

用户拍板 ④=1。`CodeGenerator.GeneratorStamp` 原来是
`HmiEditor.CodeGen <ver> (git <短哈希>)`，而 `<ver>` 里还被 SDK 拼进了 `+<sha>`
（实测形如 `1.0.0+4b89d7d27d19…`）⇒ **每次提交都改产物**，`firmware/t113/generated/` 是**入库**的，
于是"可再导出"没法用逐字节相等判、diff 永远有噪声。

改法：两处都摘掉 —— 去掉 `(git …)` 段，并把 `InformationalVersion` 里 `+` 之后的部分截掉；
`ResolveGitCommit()` 一并删除（溯源改由提交信息/发布清单承担）。
**实测**：输出变为 `// Generator: HmiEditor.CodeGen 1.0.0`；四类产物**两次运行逐字节相同**
（`screens.c` `060a7f23…` · `tags.h` `27944369…` · `tags.c` `e85153a4…` · `generated_security.h` `1aa94a23…`）。

**读数**：`GATE_EXIT=0` / 7 步 / **4012 通过 / 0 失败 / 0 警告**。

---

## 阶段 309（2026-10-11）—— ①**可脚本化导出 CLI** 落地 ＋ "可再导出"判据（①＋② 第一步）

### 309.1 `tools/DeviceExport`

把设备树四类产物的出处收敛到一个**可脚本化**入口（此前只有编辑器里的交互式动作）：

```
dotnet run --project tools/DeviceExport -- <project.json> <outDir> [--assets <dir>]
dotnet run --project tools/DeviceExport -- <X.hmi>      <outDir>          # .hmi 是 zip，工具自己解
```

| 产物 | 现场来源 |
|---|---|
| `screens.c` | `CodeGenerator.GenerateSharedLibraryProject(project, null)` |
| `tags.h` / `tags.c` | `RuntimeBindingEngine` ＋ `RuntimeTagsCodeGen` |
| `generated_security.h` | `SimulatorProjectExporter.Export(...)`（在 Core ⇒ 工具/测试都能直接调） |
| `assets/` | 从 `--assets` 目录复制（本轮只复制，不判内容） |

### 309.2 实测：**4 类里 2 类已经逐字节可复现**

用仓内示例工程定义跑当前生成器，与夹具逐字节比对：

| 产物 | 现场 | 夹具 | 判读 |
|---|---|---|---|
| `generated_security.h` | 7,731 B | 7,731 B | ✅ **逐字节相同** |
| 4 张资产 | — | — | ✅ **逐字节相同**（`asset_*` 全部） |
| `screens.c` | 23,159 B | 23,429 B | ❌ 不同（差清单见阶段 308） |
| `tags.h` / `tags.c` | 2,608 / 106 B | 4,469 / 850 B | ❌ 不同 —— 根因：**本工程当前绑定算不出夹具那份标签表**（运行时标签数 = 0） |

⇒ 结论：**"可再导出"对安全头与资产已经是事实**；`screens.c`/`tags.*` 的差距是**真漂移**，
不是判据口径问题。这也把"要不要重新生成夹具"从判断题变成了**有读数的取舍**。

### 309.3 判据与负控

新增 `DeviceExportReproducibilityTests`：在线重建 → 断言 `generated_security.h` **逐字节等于**夹具；
并把另外三类的差值打成**读数**（不装作它们也复现了）；面自检（夹具那份 > 1000 B，防"两个空文件相等"）。
**负控 1/1**：改示例工程 `securityConfig.maxLoginAttempts` ⇒ 现场 7,732 B vs 夹具 7,731 B ⇒ **判据红**（消息精确到字节数）。

### 309.4 ⚠ 仍未做

`screens.c`/`tags.*` 的差怎么收（重新生成夹具？给示例工程补绑定？）· ② 的"构建期生成、不入库" ·
`// Generator: …+<git sha>` 那行要不要做稳定（**待你选**：改生成器 vs 判据归一化）。

**读数**：`GATE_EXIT=0` / 7 步 / **4012 通过 / 0 失败 / 0 警告**。

---

## 阶段 308（2026-10-11）—— "可再导出"**实测**：夹具相对当前生成器**已陈旧**

阶段 307 把源工程入了仓，于是可以真跑一次"再导出"。用临时探针（读完即删）从
`firmware/t113/sample/MockChineseProject/project.json` 重建工程，跑**当前**生成器：

| 产物 | 现场（当前生成器） | 夹具 | 判读 |
|---|---|---|---|
| `screens.c`（忽略 `Project:` 行） | 21,625 B / **514 行** | 21,789 B / **522 行** | **500 行不同** |
| `tags.h` | 2,210 B | 3,535 B | 头措辞不同 ＋ **运行时标签数 = 0** |
| `tags.c` | 98 B | 770 B | 同上 |

**直接原因之一**：当前生成器会多输出一行
`// Generator: HmiEditor.CodeGen 1.0.0+<git sha>`（夹具那份没有）⇒ **逐字节复现本就不可能**
（那行还随提交变）。`tags` 的差距更实质：**本工程当前的绑定算不出夹具里那份标签表**。

⇒ 结论：**这份夹具来自 `MockHmiGen/1.0` 期（2026-08-17）的生成器，相对当前生成器已经陈旧**。
要"可再导出"就必须**重新生成夹具**，而那会改动设备树与主机侧 11 步里的编译/ABI 判据
—— 那属于"换掉"这个**决定**的产物。

**本轮没有动夹具**（避免把主机侧判据打红、也避免把"决定"混进"调查"）。探针已删。

**仍未做 / 待拍板**：① 是否按当前生成器**重新生成** `firmware/t113/generated/`（会动设备树）；
② 若重新生成，是否顺带让 `screens.c` **构建期生成、不入库**（需要可脚本化导出 CLI —— 编辑器里目前是交互式动作）。

**读数**：`GATE_EXIT=0` / 7 步 / **4011 通过 / 0 失败 / 0 警告**；脚本 **33**；路径卫生 **754**。

---

## 阶段 307（2026-10-11）—— **源工程入仓**：示例工程定义 ＋ 确定性重打包 ＋ 链接守卫

用户指出源包在 `/home/chen/文档/` ⇒ **找到并确认了源工程**（阶段 306 结论随之更正）：

- `/home/chen/文档/MockChineseProject.hmi`，`manifest.editorVersion = "MockHmiGen/1.0"`（2026-08-17）；
- 工程名/版本 = `Mock中文显示测试工程` / `2.0.0-test-cn-img` —— 与 `screens.c` 头部**逐字一致**；
- 4 张图 `asset_banner/asset_logo/asset_bg/asset_center.bmp` 的**名字与字节数逐个对上**
  `firmware/t113/generated/assets/`（含 `bg` 与 `center` 的**重复**）。
  ⇒ 之前"源工程不在仓"判断的**依据**（名字对不上）其实来自**当前**的 `MockHmiGen`；源包是**旧版**产物。

### 307.1 入仓策略：只存工程定义，不重复存资产

源包 **24.3 MB**：字体 `asset_font_cjk.ttc` **19.5 MB**（**按约定不入 git**）＋ 4 张 BMP 5.9 MB
（与 `firmware/t113/generated/assets/` **逐字节相同**）⇒ 直接入库等于白存 5.9 MB。

| 入库 | 内容 |
|---|---|
| `firmware/t113/sample/MockChineseProject/` | `project.json`（51,600 B）· `assets.json`（**摘掉字体条目**，1,278 B）· `manifest.json`（`assetCount` 改为 4）· `README.md`（体积构成 ＋ 打法 ＋ 判据对应） |
| `scripts/repack-sample-hmi.sh` | 现场把工程定义 ＋ **设备侧那份资产**打成 `.hmi`；用 python `zipfile`（本机无 `zip`）并**钉死时间戳** ⇒ **确定性** |

实测：两次打包 **sha256 相同**（`7cecf687…`），包 **236,283 B**（deflate 把 BMP 压得很好）。

### 307.2 判据与负控

新增 `SampleProjectProvenanceGuardTests`：工程名/版本与 `screens.c` 头部一致（容忍 `v` 前缀）·
`assets.json` 的每个图片资产按**名按字节数**在 `assets/` 里对上 · **字体条目不许回来** ·
`manifest.assetCount` 与实际一致 · 面自检。
**负控 4/4**（`.workbuddy/tmp/s307-nc.py`，逐支 sha256 还原）：改工程名 · 改一个资产字节数 ·
把字体条目加回来 · 改 `manifest.assetCount`。

`firmware/t113/generated/PROVENANCE.md` 与 `DeviceFixtureProvenanceGuardTests` **随事实改了**
（"源工程不在本仓" → "源工程已入仓 ＋ 字体为何不入库"）：判据跟着事实走，而不是把旧断言留着。

### 307.3 ⚠ 仍未做

**再导出漂移判据**：加载打出来的 `.hmi` → 跑设备侧导出 → 与 `screens.c` 比对。
前提是**可脚本化的导出 CLI**（编辑器里目前是交互式动作）⇒ 这是它没做成的原因。
`screens.c` 是否改为"构建期生成、不入库"仍待拍板。

**读数**：`GATE_EXIT=0` / 7 步 / **4011 通过 / 0 失败 / 0 警告**；脚本 **33**；路径卫生 **754**。

---

## 阶段 306（2026-10-11）—— 固件示例夹具：**来源与清单机械化守门**（§12 第 6 项那道前置）

### 306.1 勘察结论：**源工程不在仓**（这次是查实的，不是推断）

`firmware/t113/generated/` 的 `screens.c` ＋ `assets/` 来自哪？实测：

- 夹具的图片是 `asset_banner/asset_bg/asset_center/asset_logo.bmp`，
  而 `tools/MockHmiGen` 产出的是 `banner_zh/logo_chip/bg_panels.bmp` —— **名字对不上**；
- `MockHmiGen` 能跑（本机实测），但它产出的是 **15.5 MB** 的 `MockChineseProject.hmi`
  （内嵌 **18.6 MB** CJK 字体，而字体按约定**不入 git**）；
- `scripts/verify.sh` 生成的"黄金工程"是给**模拟器**导出用的，不是这份设备夹具的来源。

⇒ **这份夹具不可再导出**。而 `tags.*` 那一半已被 `DeviceTagsContractDriftTests` 与生成器契约绑住
（阶段 289 咬过一次真实漂移）。

### 306.2 两个现状读数（都不是估的）

| 读数 | 值 | 命令 |
|---|---|---|
| 资产体积 | 4 张 BMP 合计 **5,915,416 B ≈ 5.64 MB**（入库） | `ls -l firmware/t113/generated/assets/` 求和 |
| **纯重复** | `asset_bg.bmp` 与 `asset_center.bmp` **逐字节相同**（同 sha256）⇒ **2,457,654 B ≈ 2.34 MB** 重复 | `sha256sum firmware/t113/generated/assets/*.bmp` |

### 306.3 落地：来源档案 ＋ 守卫（**不动夹具内容**）

- 新增 `firmware/t113/generated/PROVENANCE.md`：机器可读**清单**块（两个标记之间）＋ 逐项来源 ＋
  可再导出性 ＋ **两个选项** ＋ 上面两条读数命令。明确写"别拿 `MockHmiGen` 当本目录的来源"。
- 新增 `DeviceFixtureProvenanceGuardTests` **四道**：① 清单与目录**逐项一致**（增删即红）；
  ② `screens.c` 头部必须带「内置示例工程（非产品工程）」标记；③ `PROVENANCE.md` 必须写明
  **源工程不在本仓** ＋ 记录那条"逐字节相同"的重复读数；④ 面自检（清单非空且含 `tags.h`/`tags.c`）。
- **负控 5/5**（`.workbuddy/tmp/s306-nc.py`，逐支 sha256 还原）：清单漏一项 · `screens.c` 丢标记 ·
  不再写"源工程不在仓" · 丢重复读数 · **目录里多一个文件而清单没改**。
  ⚠ nc4 首跑没咬住：那句话在文档里出现**多次**，我只替换了第一处 —— **负控写法不严**（判据只需它在某处出现），
  改成全量替换后才咬住。

**本轮刻意没动夹具内容**：换 `screens.c` 会牵动主机侧 11 步里的编译与 ABI 判据 —— 那应当由"换掉"这个决定
本身带过来，而不是顺手做。

### 306.4 ⚠ 这条**不可能单方面闭环**（要你选一个）

| 选项 | 做法 | 代价 |
|---|---|---|
| **A. 你提供原工程** | 把那份工程的 `.hmi`（或等价最小示例）放进仓 → 重新导出本目录 ⇒ 之后**可再导出**并可加漂移判据 | 需要工程文件；带 CJK 字体时注意体积（约定不入 git） |
| **B. 拍板"改为构建期生成、不入库"** | 设备构建时现跑导出 | 当前**没有**可脚本化的导出 CLI，要先把编辑器导出路径做成 CLI；且 `screens.c` 不再入库后，主机侧若干步要改成"先生成再编" |

**读数**：`GATE_EXIT=0` / 7 步 / **4010 通过 / 0 失败 / 0 警告**；脚本 **33**；路径卫生 **750**。

---

## 阶段 305（2026-10-11）—— 设备侧 TLS 运维手册（凭据轮换 ＋ 故障判读）

### 305.1 交付

`docs/2026-10-11-tls-ops-runbook.md`（新文档，README 文档索引已挂）：拓扑与"谁读哪些键" · 三条红线 ·
**PSK 与 X.509 的轮换**（含"不中断顺序"与回退）· **审计事件表** · **运维判读** · 与判据的对应表 · 仍未做。

### 305.2 三处"文档必须写死"的东西

1. **凭据只在进程启动时读一次** ⇒ 改键/换文件**必须重启**；代理**没有** SIGHUP 热加载
   （如实写成"已知边界，不是遗漏"）。
2. **`ca` 模式的部署前提是"开机先校时"**：代理有硬闸门（系统时间早于 2020-01-01 ⇒ 拒 ＋
   审计 `mqtt-tls-clock-unsynced`；PSK 不受影响）——文档明确写"**不要**为了让它连上而放宽这个闸门"。
3. ⚠ **运维判读那节**（最容易误判的一点）：**TLS 失败 ⇒ 没有 MQTT 连接 ⇒ 连 LWT 都发不出去** ⇒
   远程端**看不到** `on=0`，只会看到"设备没了"。所以"心跳超时"**分不出**"设备真离线"与"TLS 失败"，
   只能靠设备侧审计 ＋ 代理日志；文档给了判读表与最小动作（**不要**直接把心跳超时判定为设备故障）。

### 305.3 事件名逐字核对

审计事件名不是凭记忆写的：`grep -o 'audit("…"'` 对代码核过 —— 代理 **9 个**事件
（`proxy-start`/`-stop`/`config-refused`/`clock-unsynced`/`psk-file-missing`/`ca-file-missing`/
`upstream-unreachable`/`handshake-failed`/`proxy-listen-failed`）＋ 客户端 `config-refused`，
全部与 `tls_proxy.c` / `comm_core.c` **逐字一致**；代理日志路径也对过 init 脚本（`/mnt/UDISK/hmi_tls_proxy.log`）。

### 305.4 ⚠ 仍未做（如实）

板子上**内存**（VmRSS）实测 · 凭据**热加载**（SIGHUP）未做 · **吊销**（CRL/OCSP）未做（撤销靠换 CA/换身份）。

**读数**：本文档只加 `.md` ＋ README 一行 ⇒ 门禁计数不变（**脚本 33 / 路径卫生 750**，与阶段 304 同）；
`GATE_EXIT=0` / 7 步 / **4009 通过 / 0 失败 / 0 警告**。

---

## 阶段 304（2026-10-11）—— 用**真设备工具链 ＋ 设备侧 OpenSSL 1.1.0i** 交叉编译代理

### 304.1 补的是哪一层

主机侧（第 11 步）编的是 **OpenSSL 3.6.5**，而设备镜像里是 **1.1.0i** —— API 落差只有用真工具链 ＋
真头文件才能暴露。用户给出 SDK 位置（`/home/chen/Tina-Linux/`，实际根为 `Tina-Linux/Tina-Linux/`）后补齐。

### 304.2 读数（实测）

| 项 | 值 |
|---|---|
| 工具链 | `arm-openwrt-linux-muslgnueabi-gcc` **6.4.1** |
| 设备侧 OpenSSL | **`OpenSSL 1.1.0i-fips`**（`opensslv.h` 的 `OPENSSL_VERSION_NUMBER = 0x1010009f`） |
| 交叉编译 | **rc=0 / 0 警告**（对 1.1.0i 的头文件）—— 说明代码里的版本宏分支与 API 用法在设备版本上成立 |
| 产物 | **ARM 32-bit / musl** ELF（`/lib/ld-musl-armhf.so.1`） |
| NEEDED | `libssl.so.1.1` · `libcrypto.so.1.1` · `libgcc_s.so.1` · `libc.so` —— **全是镜像里有的** |
| 代理尺寸 | 未剥离 **38,044 B** → **剥离后 12,676 B** |
| 镜像侧 OpenSSL | `libssl.so.1.1` **348,084 B** ＋ `libcrypto.so.1.1` **1,767,680 B**（≈ **2.02 MB**） |
| `app/Makefile` 目标 | 用真工具链 `make hmi-tls-proxy` **rc=0** |

⚠ **评估 §2.8 的那个 6.6 MB ＋ 1.1 MB 是主机 OpenSSL 3.x 的读数**（当时就写明"不能当板上数"）；
现在这一行才是**设备侧**的尺寸读数。**内存（VmRSS）仍需真板** —— §6-5 只补上了"尺寸"半边。

### 304.3 可复跑：`scripts/check-cross-proxy.sh`

**不进 `ci-gate`** —— SDK 在机器上、不在仓库里，门禁必须在没有 SDK 的机器上也能全绿。
所以用**三态退出码**（与代理自己的 0/3/4 同款思路）：`0` 通过 · `3` **跳过**（没找到 SDK —— **不是**失败，
但也**不是**"查过了"）· `1` 失败。判据 7 条：编得过 / 0 警告 / ARM ELF / NEEDED 两个 OpenSSL .so /
`app/Makefile` 目标存在（`make -n` 干跑）/ 镜像里有那两个 .so / 并打印尺寸。

本机跑：**7 通过 / 0 失败，rc=0**。脚本在临时目录里编、**不往仓库落 `.o`**（首轮手工验证时落过，已清）。

**读数（工作树）**：脚本 **32**；**路径卫生 749 → 750**（新增一个脚本）；`scripts/check-cross-proxy.sh` 本机 7/7；其余读数同阶段 303
（`GATE_EXIT=0` / 7 步 / 4009 通过 / 0 失败 / 0 警告 —— 本阶段**只加了一个不进门禁的脚本**，故门禁计数不变）。
