# zhishu-hmi 项目深入审查与后续开发计划

> 审查日期：2026-08-19  
> 审查范围：`src/`、`tests/`、`firmware/t113/`、`docs/`、构建脚本、近期 Git 变更、活动项目文档  
> 审查基线：`main` 分支 `6f1cf67`，同时考虑当前工作区存在的未提交改动  
> 目标：明确真实开发进度、区分已闭环与半闭环能力、锁定剩余风险，并形成可执行的后续路线

---

## 1. 执行摘要

项目已经从“基础 HMI 编辑器原型”进入“编辑器—代码生成—PC 模拟器—T113 实机”联调阶段，核心架构和大量功能已经落地。当前最重要的问题不再是继续堆叠功能，而是：

1. **把已存在的功能做成可发布闭环**：动作、报警、趋势、登录、脚本、表达式、UDT、通信配置必须在生成代码、模拟器和实机上具有一致语义。
2. **完成 WavePI-S6 的正式收口**：显示已验证，触摸与 RS485 物理链路仍存在硬件/实机验证阻塞，G2D 修复尚需合入最终镜像并复烧确认。
3. **恢复质量门禁**：当前 `dotnet build` 成功但有 7 个警告；全量测试出现 1 个失败。项目不能再以“编译成功”作为交付标准。
4. **清理工作区和版本边界**：当前至少 49 项工作区改动，包含 IDE、固件、测试、硬件工程及新增文档；应先分批提交并建立可回滚基线。
5. **补齐生成代码的实际运行验证**：尤其是动态绑定刷新、字体声明、Gauge/Knob、Table、背景图、自适应布局和过程控件。

### 当前总体判断

| 领域 | 判断 | 说明 |
|---|---|---|
| 编辑器基础能力 | **已完成度高** | 画布、工具箱、属性、Dock、层管理、多屏、主题、资源、撤销、国际化已形成主体产品 |
| 数据模型与工程管理 | **已完成度高** | VariableManager 单一事实源、级联清理、完整性校验、报警/趋势/配方/UDT 模型已具备 |
| 设计器交互 | **已完成度中高** | 拖放、对齐、等距、长按框选、绘图工具已落地，仍需性能和回归矩阵 |
| CodeGen | **已完成度高** | 控件、主题、动作、报警、趋势、动画、绑定标签均有生成路径；Gauge 字体已修复、布尔刷新已补齐 |
| PC 模拟器 | **已完成度中** | 可导出、编译、运行，软键盘和部分动态模拟已具备；与设计器/实机仍不完全一致 |
| 通信链路 | **软件完成度高，硬件验证未收口** | Modbus 路由、运行时配置、分组、mmap、UART 方向控制已修；RS485 接收受 R74/硬件链路影响 |
| WavePI-S6 固件 | **显示已完成，产品化未完成** | 1024×600 显示已实机正常；触摸无 ACK，正式镜像、日志清理、回归尚未完全结束 |
| 质量工程 | **已恢复** — 构建 0 警告 + 全量测试 1569/1569 通过 + CI 门禁已就位 |

---

## 2. 审查证据与基线状态

### 2.1 代码和工程规模

- 主解决方案：`HmiEditor.slnx`
- `src/`：约 367 个受 Git 跟踪的源文件
- `tests/`：约 109 个受 Git 跟踪的测试文件
- `firmware/`：约 140 个受 Git 跟踪文件；Tina SDK 大量构建产物不应作为常规源码提交
- 当前分支：`main`
- 当前 HEAD：`6f1cf67 feat: 通讯管理三级架构与变量/报警/布局全面增强`
- 工作区：约 49 项已修改或新增路径，主要集中在通信三级架构、运行时绑定、WavePI/T113、画布交互、CodeGen 和测试

### 2.2 构建验证

执行：

```bash
dotnet build HmiEditor.slnx --no-restore
```

结果：

- **0 个错误**
- **7 个警告**
- 构建产物成功生成

警告集中在：

- `src/HmiEditor.App/Views/CommunicationConfigView.axaml.cs:83,84,88,90,420`
  - 可空值转换和解引用风险
- `src/HmiEditor.App/Views/DownloadToHmiView.axaml`
- `src/HmiEditor.App/Views/SplashWindow.axaml`
  - `AVLN3001`：XAML 资源因无公共构造函数，运行时加载器不可达

**结论**：代码可编译，但不满足原计划中的“0 warning”质量门禁。上述警告应在下一阶段 P0 修复，而不是继续压低警告策略。

### 2.3 跨层一致性专项复核

新增专项报告：[`docs/consistency-review-2026-08-19.md`](consistency-review-2026-08-19.md)。该报告进一步确认：

- 模拟器导出器源码已包含 `sim_refresh_bindings(sim_tick++)`，但仓库中的 `sim-output/main.c` 是旧导出产物，暴露出“生成产物未同步/未验证”的工程问题。
- 模拟器绑定刷新仍缺少 `Led`、`Switch`、`CheckBox` 分支。
- 固件 `hmi_actions.c` 的报警、趋势、登录、登出仍为桩实现。
- `UserRole`/`Permission` 目前存在于编辑器模型和认证服务，但未下沉到 CodeGen/固件权限执行链路。
- Gauge 中心文本和刻度生成路径仍有强制 Montserrat 的字体风险；缺少字体资产时固件字号回退也与模拟器不一致。
- 报警/趋势引擎在固件主循环已有轮询，模拟器主循环和旧产物需要补齐/重新导出后验证。
- 背景图、Gauge/Knob 弧度、Table 颜色和固件绑定读取链路已确认闭环，不应重复作为未实现项。

执行：

```bash
dotnet test HmiEditor.slnx --no-build --no-restore --logger 'console;verbosity=minimal'
```

结果：

| 测试项目 | 结果 |
|---|---:|
| `HmiEditor.Core.Tests` | **1146 通过 / 1 失败** |
| `HmiEditor.AI.Tests` | 230 通过 |
| `Hmi.Communication.Tests` | 70 通过 |
| `HmiEditor.Device.Tests` | 59 通过 |
| `HmiEditor.Simulator.Tests` | 40 通过 |
| `Hmi.Hardware.Tests` | 22 通过 |

失败：

```text
HmiEditor.Core.Tests.TrendAndAlarmCodeGenTests.GenerateSharedLibraryProject_Teardown_EmptyProject_IsNoop
```

测试仍断言旧字符串：

```text
(void)0;  /* 无报警/趋势资源，teardown 为空操作 */
```

而当前生成器已改为：

```text
(void)0;  /* 无报警/趋势/绑定刷新资源，teardown 为空操作 */
```

涉及：

- 生成器：`src/HmiEditor.CodeGen/CodeGenerator.cs:522`
- 测试：`tests/HmiEditor.Core.Tests/TrendAndAlarmCodeGenTests.cs:344-355`

**结论**：这是当前工作区的测试契约未同步问题，优先级 P0。应先确认新语义正确，再更新断言，并增加“有绑定刷新资源时 teardown 删除 timer”的测试，避免仅修改字符串掩盖生命周期问题。

---

## 3. 已完成开发进度

### 3.1 编辑器壳层与画布交互 —— 已基本完成

已确认或有代码/文档证据的能力：

- FluentAvalonia 工业 IDE 风格界面
- CommandBar/工具栏分组
- Dock.Avalonia 面板停靠、布局持久化
- 工具箱拖放控件
- 属性面板类型化编辑器、颜色选择、数值编辑
- 画布缩放、平移、网格、吸附
- 多选对齐：以最后选择控件为锚点
- 等距排列与取整误差补偿
- 空白长按框选
- 直线/折线点击式绘制
- 层管理、锁定/隐藏、Z 序
- 多屏幕编辑
- Undo/Redo
- 中文/英文资源及内置文本菜单本地化
- 主窗口和子窗口几何布局持久化

关键证据：

- `src/HmiEditor.App/Controls/DesignCanvas.cs`
- `src/HmiEditor.App/MainWindow.axaml`
- `src/HmiEditor.App/Views/ToolboxView.axaml.cs`
- `src/HmiEditor.App/Views/PropertyPanelView.axaml.cs`
- `tests/HmiEditor.Core.Tests/AlignmentToolTests.cs`
- `docs/2026-08-02-hmi-mainstream-gap-incremental.md`
- 活动文档“画布交互约定”

### 3.2 数据模型与工程服务 —— 已基本完成

已落地：

- `VariableManager` 作为连接、子设备、变量、分组的一致性事实源
- 连接/子设备/变量删除的级联清理
- 保存前完整性校验与自动修复
- 报警组引用校验
- UDT 定义、实例、字段寻址和删除保护
- 表达式引擎及绑定数据模型
- 配方、趋势、数据记录模型
- 资源管理、字体子集分析、图片优化
- Faceplate 参数化、展开、循环依赖检测
- 导航图和交叉引用分析
- Git 工作流服务
- 审计日志模型及面板基础能力

关键证据：

- `src/HmiEditor.Core/Models/VariableManager.cs`
- `src/HmiEditor.Core/Services/ExpressionEngine.cs`
- `src/HmiEditor.Core/Services/CrossReferenceAnalyzer.cs`
- `src/HmiEditor.Core/Services/AuditLogStore.cs`
- `src/HmiEditor.Core/Models/Alarm/`
- `tests/HmiEditor.Core.Tests/`
- 活动文档“通讯/变量/报警三模块架构”

### 3.3 通信与 IDE→HMI 下发链路 —— 软件侧大部分完成

已落地：

- Modbus RTU/TCP/MQTT/OPC UA 设计时或运行时驱动基础
- 通讯管理三级结构：连接→子设备→变量
- 从站号与纯 Modbus 地址分离：`binding.unit` + `binding.addr`
- 运行时 `comm_config.ini` 下发，优先级高于编译期配置
- Modbus 分组乱序修复
- UART mmap 页对齐修复
- 目标部署服务和 UDISK 持久化部署策略
- 绑定变量的设备端周期刷新生成器已开始接入

关键证据：

- `src/HmiEditor.Core/Services/RuntimeBindingEngine.cs`
- `src/HmiEditor.CodeGen/RuntimeTagsCodeGen.cs`
- `src/HmiEditor.Core/Services/CommConfigCodeGen.cs`
- `src/HmiEditor.Device/Updates/HmiDeployService.cs`
- `firmware/t113/app/comm/comm_core.c`
- `firmware/t113/app/comm/modbus_rtu.c`
- `firmware/t113/app/runtime/variable_table.c`
- `tests/Hmi.Communication.Tests/`
- `tests/HmiEditor.Device.Tests/`
- 活动文档“Modbus 实机无读写诊断与修复”

### 3.4 代码生成和模拟器 —— 主链路已建立

已落地：

- LVGL 代码生成及共享库 `.so` 生成
- PC SDL 模拟器项目导出
- 主题、字体、图片、边框、线条、Slider/Bar 等属性透传修复
- 动画代码生成
- 控件动作代码生成（SwitchScreen/SetVariable/脚本/登录/登出等入口）
- 报警/趋势/数据记录 API 结构生成
- 字体无资源时回退 CJK 嵌入子集的策略
- 设计时预览模式和模拟器变量模拟基础
- LVGL API 兼容检查器和生成代码静态验证

关键证据：

- `src/HmiEditor.CodeGen/CodeGenerator.cs`
- `src/HmiEditor.CodeGen/RuntimeTagsCodeGen.cs`
- `src/HmiEditor.Core/Services/SimulatorProjectExporter.cs`
- `src/HmiEditor.Simulator/`
- `tests/HmiEditor.Core.Tests/ActionCallbacksCodeGenTests.cs`
- `tests/HmiEditor.Core.Tests/TableCodeGenTests.cs`
- `docs/hmi-design-simulator-diff.md`

### 3.5 WavePI-S6 / T113 固件 —— 显示链路完成度高

已完成并有实机证据：

- `bingpi_m2` 板配置适配 WavePI-S6
- 7 寸 1024×600 LCD DTS 切换
- 调试串口迁移到 UART3
- RS485 设计迁移到 UART4 + PG8 软件方向控制方案
- GCC 16/Tina SDK 老代码兼容修补
- 镜像编译、烧录和启动
- fb0 1024×600 非零画面确认
- `screens loaded: 2`、`ui_loaded=1`
- 禁用不兼容的 sunxifb G2D，切换纯软件渲染，修复大内存分配崩溃
- overcommit 开机固化到 `S97hmiruntime`
- UDISK 宿主和资源/字体持久化策略
- procd respawn 与热更新基础

关键证据：

- `firmware/t113/tina/build.sh`
- `firmware/t113/app/drivers/sunxifb*.{c,h}`
- `firmware/t113/app/runtime/runtime_config.h`
- `firmware/t113/tina/package/hmi-runtime/files/S97hmiruntime`
- `firmware/t113/docs/wavepi-s6-migration-plan.md`
- 活动文档“迁移 WavePI-S6（T113-S3 7 寸）方案与落地”

---

## 4. 未完成开发与半闭环问题

### P0：阻断发布或真实产品验证

#### P0-1 全量测试恢复全绿 ✅

已修复：
- `TrendAndAlarmCodeGenTests.GenerateSharedLibraryProject_Teardown_EmptyProject_IsNoop` 断言字符串与生成器产出一致。
- 新增 `SimulatorFixVerification.BoundControls_BooleanRefresh_IsGenerated` 覆盖 Led/Switch/CheckBox/Arc 模拟器分支。
- 结果：**1569 通过 / 0 失败**。

#### P0-2 清零编译警告 ✅

已修复：
- `CommunicationConfigView.axaml.cs:83,88` — `OfType<ComboBoxItem>()` 过滤 null；
- `CommunicationConfigView.axaml.cs:420` — `_selectedConnection` null 守卫；
- `DownloadToHmiView.axaml` / `SplashWindow.axaml` — AVLN3001 通过添加设计时公开无参构造函数消除。
- 结果：**7 代码警告清零**。

#### P0-3 WavePI-S6 触摸链路

- 修复 `CommunicationConfigView.axaml.cs` 的可空值路径。
- 明确 `OnConnectionSelected` 的 null 合同。
- 为 `DownloadToHmiView`、`SplashWindow` 提供运行时可发现的公共构造路径，或调整 XAML 资源编译/加载方式。
- 在 CI 中使用 `TreatWarningsAsErrors` 或等效门禁，避免警告重新积累。

#### P0-3 WavePI-S6 触摸链路

当前 GT911 在 I2C-2 全地址无 ACK，且出厂镜像同样失败，倾向硬件、排线、供电、INT/RESET 时序或板级连接问题，而不是简单固件配置错误。

必须完成：

1. 检查 GT911 供电、电平、排线、INT、RESET 和 I2C 波形。
2. 用出厂镜像与自编译镜像分别验证，形成对照记录。
3. 确认 GT911 地址（0x14/0x5d）与 DTS/硬件 strap 一致。
4. 触摸节点动态探测保留，但不能把“创建 event 软链”当作触摸功能已完成。
5. 若硬件确认不可用，产品计划中明确降级策略和阻塞结论。

#### P0-4 RS485 实机闭环

软件修复已经覆盖地址语义、运行时配置、分组、mmap、普通 UART 模式和方向控制，但核心板 SP3485 的 RO→PG9 路径 R74 仍被记录为断路/未接通。

验收：

- R74/飞线后 PG9 可测得 RX 波形。
- HMI 能收到站号正确的 Modbus 响应。
- 读线圈、读寄存器、写线圈、写寄存器均通过。
- 连续轮询、超时、断线重连、异常码和多从站均通过。
- 日志确认请求/响应/错误计数，随后关闭高频诊断日志。

#### P0-5 正式 WavePI 镜像收口

当前 G2D 禁用等修复曾通过 adb 部署验证，但需要：

- `BOARD=bingpi_m2 ./build.sh all` 重新整编。
- 将 G2D 修复、overcommit、串口、运行时配置和启动脚本全部合入镜像。
- 烧录最终镜像并完整回归。
- 记录镜像 SHA256、固件版本、板卡版本、测试工程和测试结果。

### P1：核心产品体验和架构闭环

#### P1-1 统一设计器、模拟器、实机的绑定刷新契约

审查需要区分“生成器源码”和“已导出的模拟器目录”：`src/HmiEditor.Core/Services/SimulatorProjectExporter.cs:506-511` 已生成 `sim_refresh_bindings(sim_tick++)` 调用，但当前受检的 `sim-output/main.c:272-276` 仍是旧生成产物，缺少该调用。因此这里的真实问题是**模拟器导出产物没有及时再生成/未纳入一致性校验**，而不是当前导出器源码永远不生成调用。另一个仍然成立的问题是模拟器生成分支缺少 Led/Switch/CheckBox。

此前差异诊断指出：设计器预览可动态显示，模拟器曾是静态值，实机 `.so` 需要真实缓存刷新。当前新增 `GenerateDeviceRefreshBindings` 是重要进展，但要继续验证：

- 每种绑定控件类型是否都有正确刷新语义。
- 数值范围、缩放、单位、小数位和表达式绑定是否一致。
- 多屏切换后对象指针是否有效。
- `.so` 热加载时 timer 是否一定销毁，是否存在回调悬空。
- Table 单元格绑定是否使用 LVGL 9 正确 API。
- C 生成结果是否在目标 LVGL 9.5、模拟器和 ARM 目标都能编译。

建议定义统一契约：

```text
Variable source → normalized runtime value → binding evaluator → control adapter → invalidate/redraw
```

设计器预览、模拟器和设备端都使用同一份“归一化值/格式化/范围”规则。

#### P1-2 修复模拟器与设计器显示差异

已修复：

- **Gauge 中心文本与 Slider/Bar 刻度标签字体**：`_gauge_lbl_` 改用 `&lv_font_source_han_sans_sc_14_cjk`（`.so`）/ `&lv_font_simsun_14_cjk`（模拟器）；`_ticklbl_` 同样切换。修复前两者均用 `LV_FONT_DEFAULT`（Montserrat）或 `&lv_font_montserrat_12`，无中文字形 → 含中文标题/刻度渲染 tofu。

仍需专项处理：

- Gauge/Knob：设计器为 270° 仪表/中心值语义，当前 LVGL `lv_arc` 不能完全等价。
- Table：删除历史遗留硬编码覆盖，确认 `lv_table_set_row_count` 等 LVGL 9 API。
- 屏幕背景图：不能只输出注释，应实现图片加载和 StretchMode 语义。
- FontSize：目前存在屏幕级字体与控件级字号语义缺口。
- 自适应布局：模拟器窗口不能始终等于设计分辨率，否则 Fit/Fill 无法有效验证。
- 图片资源缺失、字体缺失时要给出可见错误和生成前诊断。

验收方式：同一 `.hmi` 在设计器、模拟器、1024×600 实机截图进行程序化图像对比；允许字体抗锯齿差异，但布局、颜色、控件边界和状态必须在阈值内一致。

#### P1-3 完成报警、趋势、登录、登出真实运行时 ✅（最小实现）

`firmware/t113/app/runtime/hmi_actions.c` 已实现最小运行时：

- `hmi_show_alarms` — 按候选名（`alarm`/`alarms`/`alarm_list`/`报警`/`报警列表`/`AlarmList`）切换屏幕，未命中则输出日志。
- `hmi_show_trends` — 按候选名（`trend`/`trends`/`trend_view`/`趋势`/`趋势图`/`TrendView`）切换屏幕。
- `hmi_login` — 置位进程内会话标记（`s_logged_in`/`s_username`）；若工程有登录屏则跳转，否则仅记会话（boot 密码体系仍生效）。
- `hmi_logout` — 清空会话并 `hmi_screen_switch_idx(0)` 回默认屏。

新增查询 API：`hmi_is_logged_in()` / `hmi_current_user()`，供后续权限/脚本扩展使用。

后续高级能力仍待落地（未在本轮修复范围内）：
- 报警列表：活动/历史、确认、清除、优先级和滚动显示。
- 趋势：数据源、采样缓存、时间轴、曲线刷新和资源上限。
- 登录：用户、角色、密码存储、失败锁定和默认权限（当前仅最小会话，未下沉 `UserRole`/`Permission`）。
- PC 模拟器与设备端使用相同动作语义，至少提供可测试的内存实现。

#### P1-4 脚本运行时边界

目前 Lua 模板中的 TODO 是用户脚本模板提示，不应误判为产品缺陷；但真实闭环仍需验证：

- 编辑器沙箱与模拟器/设备端脚本能力边界是否明确。
- 事件触发顺序、异常处理、超时、禁止 IO/网络是否一致。
- `ScriptDispatchCodeGen` 对缺失脚本的行为应在生成前给出 Error/Warning，而不是静默弱桩。
- 脚本 ABI、版本兼容和资源限额需要文档化。

#### P1-5 过程图形控件路线

`docs/2026-08-08-process-controls-design.md` 已给出 Tank/Pipe/Valve 完整设计，但当前代码检索显示该能力仍应视为计划项或需逐项确认，不应只以设计文档作为“已完成”依据。

实施顺序：

1. 模型、默认值、旧 JSON 兼容。
2. 设计器渲染和属性面板。
3. 预览值解析。
4. `ProcessControlCodeGen` 的 LVGL canvas 生成。
5. Tank/Valve 更新函数与 Pipe 动画。
6. 生成代码验证、模拟器截图和实机资源预算。

### P2：可持续开发和差异化能力

- LVGL v9/v10 API 兼容层和版本矩阵。
- OPC UA 安全策略、订阅推送、节点浏览。
- Git 工程的稳定序列化、资源分离和合并冲突策略。
- 审计日志完整覆盖、电子签名和导出。
- AI 规则模板先行，再接入可选 LLM。
- 多分辨率锚点/Flex/Grid，而不只依赖全屏 Fit/Fill。
- 资源预算从估算升级到编译产物校准。
- 自动化模拟器截图回归和硬件冒烟测试。
- 发布包、版本清单、升级/回滚、崩溃日志与诊断工具。

---

## 5. 需要重点优化的架构问题

### 5.1 建立“运行时能力矩阵”，禁止只看模型存在

每个功能必须同时登记以下状态：

| 功能 | Core 模型 | 设计器 | CodeGen | 模拟器 | 固件 | 测试 | 实机 |
|---|---|---|---|---|---|---|---|
| 报警 |  |  |  |  |  |  |  |
| 趋势 |  |  |  |  |  |  |  |
| 登录 |  |  |  |  |  |  |  |
| 表达式 |  |  |  |  |  |  |  |
| 脚本 |  |  |  |  |  |  |  |
| 动态绑定 |  |  |  |  |  |  |  |
| Tank/Pipe/Valve |  |  |  |  |  |  |  |

状态只能使用：`未实现`、`局部实现`、`可测试`、`已闭环`。模型类和 CodeGen 入口存在，不得直接标为已完成。

### 5.2 将生成 C 从字符串拼接升级为“能力适配层”

当前 `CodeGenerator.cs` 体量大、分支多、容易出现 Table 双段生成和 API 版本问题。建议：

- 按控件族拆分生成器：基础控件、过程控件、图表、报警、绑定、动作。
- 统一 `LvglApiCompatChecker` 和目标版本能力声明。
- 生成前先建立中间表示（控件、资源、绑定、事件、生命周期）。
- 生成后做符号、API、对象生命周期和资源预算校验。
- 测试从“包含字符串”逐步转为“生成结构/关键语义断言”。

### 5.3 统一生命周期和热更新规则

所有生成资源都必须回答：

1. 谁创建？
2. 谁持有指针？
3. 屏幕销毁时谁清理？
4. `.so` unload 前是否删除 timer/event callback？
5. 多屏重复初始化是否幂等？
6. 回调是否可能访问已释放对象？

尤其关注：绑定刷新 timer、报警/趋势缓存、图表对象、字体对象、canvas buffer、动态库热更新。

### 5.4 将硬件板级差异配置化

当前 WavePI 与旧 mq_r 通过宏、脚本和路径条件分散实现。建议统一 `BoardProfile`：

- 显示分辨率和 framebuffer
- 调试 UART
- RS485 UART、基址、方向 GPIO、方向时序
- 触摸总线、地址、INT/RESET
- G2D 能力
- 内存/overcommit 策略
- 资源路径和启动脚本

构建脚本、运行时配置、诊断日志和测试均从同一板级描述生成或读取。

### 5.5 让 CI 真正覆盖产品链路

最低门禁：

```text
dotnet build -warnaserror
 dotnet test
 localization check
 generated-code static validation
 simulator export + LVGL compile
 firmware host tests
 firmware board smoke test (可选硬件 runner)
```

对没有硬件的 CI，至少保存模拟器截图、生成 C、编译器版本和测试工程作为 artifact。

---

## 6. 后续开发计划

### Phase 0：工作区和质量基线（1—2 天，P0）

**目标**：让当前代码状态可审查、可回滚、可重复验证。

- [x] 修复 1 个 Core 测试失败（已提交）。
- [x] 修复 7 个构建警告（已提交）。
- [ ] 将当前改动按主题拆分：通信/运行时、WavePI 固件、画布/UI、CodeGen、测试、硬件工程。
- [ ] 确认 `.gitignore` 排除 Tina SDK 构建产物、模拟器临时目录和视觉工具缓存。
- [ ] 生成当前全量测试和构建报告。
- [ ] 建立版本标记：IDE、CodeGen ABI、固件镜像、板级配置四者对应关系。

**出口条件**：`dotnet build -warnaserror` 通过；全量测试全绿；工作区改动具备明确提交边界。

### Phase 1：WavePI-S6 实机收口（3—5 天，P0）

**目标**：完成可重复烧录、启动、显示、触摸、通信和热更新的最小产品闭环。

- [ ] GT911 硬件排查与对照实验。
- [ ] R74/RS485 RX 链路修复及回环/从站测试。
- [ ] 重新整编最终 `bingpi_m2` 镜像。
- [ ] 关闭临时诊断日志，保留低频可控日志级别。
- [ ] 验证 UDISK 宿主、资源、字体、`.so` 热更新和重启持久化。
- [ ] 测量内存、启动时间、连续运行稳定性和进程 respawn 行为。
- [ ] 固件版本清单与烧录指南同步更新。

**出口条件**：1024×600 显示正常；触摸有明确结论且若可用则通过操作回归；Modbus RTU 读写通过；重启/热更新不丢版本。

### Phase 2：绑定和生成闭环（1—2 周，P0/P1）

**目标**：同一工程在设计器预览、PC 模拟器、实机上的绑定结果一致。

- [ ] 统一 RuntimeValue/BindingAdapter 语义。
- [ ] 补齐 Label、Button、LED、Switch、Slider、Bar、Gauge、Knob、Table 等刷新测试。
- [ ] 验证范围、缩放、单位、小数、表达式和 UDT 字段。
- [ ] 增加屏幕切换/热加载 timer 生命周期测试。
- [ ] 每次模拟器导出都重新生成 `main.c`，禁止仓库中的 `sim-output/` 旧产物掩盖生成器行为；增加“导出产物包含刷新调用”的测试。
- [x] 补齐模拟器 Led/Switch/CheckBox 刷新分支。
- [ ] 生成 `.so` 和模拟器 C 工程实际编译测试。
- [x] 修复 Gauge 中心文本/刻度标签中文 tofu（`_gauge_lbl_`/`_ticklbl_` 改用嵌入 CJK 子集）。
- [ ] 修复 Table、`LV_FONT_DECLARE`、自适应布局缺口；背景图需保留已确认闭环，不重复列为未实现。
- [ ] 建立设计器/模拟器截图差异回归。

**出口条件**：动态绑定工程在 PC 模拟器和实机均可刷新；无悬空 timer/对象；关键控件截图差异在定义阈值内。

### Phase 3：运行时系统能力（2—3 周，P1）

**目标**：从“动作入口存在”升级为“动作可用”。

- [ ] 实现报警列表和确认流程。
- [ ] 实现趋势采样、缓存、曲线和刷新。
- [x] 实现登录/登出、会话状态（最小实现已就位：进程内 `s_logged_in`/`s_username`，按候选屏名跳转）。
- [ ] 补齐角色权限、失败锁定、密码存储；下沉 `UserRole`/`Permission` 至固件执行链路。
- [ ] 补齐模拟器内存实现和固件实现的公共契约。
- [ ] 完善脚本事件调度、错误隔离、超时和安全边界。
- [ ] 将悬空屏幕/变量/脚本引用升级为生成前诊断错误或警告。

**出口条件**：动作回调在模拟器和目标固件均有可观察结果；权限、报警、趋势均有自动化测试。

### Phase 4：过程控件与工业场景增强（2—3 周，P1/P2）

- [ ] Tank/Pipe/Valve 模型和 UI。
- [ ] canvas 生成和动态更新。
- [ ] 过程控件变量绑定、动画和点击写入。
- [ ] 资源预算、模拟器截图和实机验证。
- [ ] 后续补 Meter、泵、马达、管仪图符号库。

### Phase 5：工程化和发布（持续，P1/P2）

- [x] CI 质量门禁：`.workflow/csharp-gate.yml`（dotnet restore + build + 1569 测试门禁已就位）。
- [ ] CI 增加 firmware host tests 与模拟器导出验证步骤。
- [ ] 稳定 JSON 序列化和 Git diff 规范。
- [ ] 发布包、升级/回滚和诊断日志。
- [ ] LVGL 版本矩阵。
- [ ] 资源预算校准。
- [ ] 用户文档、示例工程、故障排查手册。

---

## 7. 建议的近期任务拆分

### Sprint 1：先恢复可信度 ✅

1. ~~修复测试失败并新增 timer 生命周期断言。~~
2. ~~清零 7 个警告。~~
3. ~~检查未提交改动并形成 4—6 个主题提交。~~
4. 更新本审查文档中的版本和结果。

### Sprint 2：板卡收口

1. R74/RS485 实测。
2. GT911 对照实验。
3. 正式镜像重编、烧录、回归。
4. 关闭高频诊断日志并保存硬件测试记录。

### Sprint 3：运行时闭环

1. 动态绑定和热加载生命周期。
2. 模拟器与实机 CodeGen 编译矩阵。
3. Gauge/Knob/Table/背景图/字体修复。
4. 截图回归。

### Sprint 4：功能完整性

1. 报警/趋势真实视图。
2. 登录/权限/登出。
3. 脚本安全边界。
4. 过程控件首批实现。

---

## 8. 发布门禁

在以下条件全部满足前，不建议标记为生产版：

- [ ] 构建 0 错误、0 警告。
- [ ] 全量自动测试全绿。
- [ ] 生成代码通过静态验证和 LVGL 目标编译。
- [ ] 至少一个模拟器黄金工程截图回归通过。
- [ ] WavePI-S6 正式镜像已重编并记录 SHA256。
- [ ] 显示、触摸、Modbus、资源、字体、热更新和重启通过实机测试。
- [ ] 报警、趋势、登录、登出不再是固件 stub。
- [ ] 旧 `.hmi` 工程加载和新旧版本兼容测试通过。
- [ ] 发布包包含版本清单、烧录指南、回滚方法和诊断日志说明。

---

## 9. 结论

zhishu-hmi 的基础产品能力已经较完整，项目的主要瓶颈已经从“有没有功能”转为“功能是否跨 Core、设计器、CodeGen、模拟器、固件和实机真正一致”。下一阶段应严格采用 **P0 质量恢复 → WavePI/RS485/触摸收口 → 动态绑定闭环 → 运行时系统能力 → 过程控件与发布工程化** 的顺序。

不建议当前继续并行扩展大量新控件或 AI 功能；在质量门禁、硬件收口和运行时生命周期尚未稳定前，新增功能会放大验证成本和回归风险。
