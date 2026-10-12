# zhishu-hmi 用户手册

适用对象：使用本编辑器做工业 HMI 画面的**现场工程师**。
本文只描述仓库里已经存在的功能与命令，每条关键说法都标注了事实源文件，便于核对。

> 易漂移的数字不在本文写死：编辑器版本见 `src/HmiEditor.Core/VersionInfo.cs`，
> 固件版本见 `firmware/t113/docs/version-info.md`，SDK 版本见 `global.json`。

## 1. 这个软件是什么

在 PC 上可视化设计 HMI 画面、一键生成 LVGL C 代码；同一份工程既能在 PC 上以
LVGL + SDL2 模拟器预览，也能交叉编译成 `libhmi_ui.so` 下发到全志 T113-S3 设备运行。
编辑器为 .NET 10 + Avalonia 12.1；目标运行时 LVGL 9.5 + FreeType。
依据：`README.md`、`firmware/t113/docs/version-info.md`。

## 2. 工作流总览

```
新建/打开工程 → 拖入控件、编辑画面与属性 → 建变量、绑定、配置报警/趋势/动画/脚本
   → 模拟器预览 → 导出 C 代码 / 编译转换（源码工程 + 交叉编译 libhmi_ui.so）
   → 下载到 HMI（串口或以太网） → 设备重启 hmi_runtime，加载新 .so 生效
```

对应菜单（事实源：`src/HmiEditor.App/MainWindow.axaml`）：

| 步骤 | 菜单项 |
| --- | --- |
| 新建/打开/保存 | 文件 → 新建项目 / 打开项目... / 保存项目 |
| 导出源码 | 文件 → 导出 C 代码... |
| 交叉编译 | 文件 → 编译转换 |
| PC 模拟器 | 文件 → 编译并运行 |
| 导出模拟器工程 | 文件 → 导出模拟器工程(_M)... |
| 代码静态检查 | 文件 → 验证生成代码(_V)... |
| 下发设备 | 文件 → 下载到HMI...（初始灰显，编译转换成功后可用） |

## 3. 安装与启动

### 3.1 环境要求

构建/测试编辑器需要由 `global.json` 固定的 .NET SDK（`rollForward: latestFeature`）与 bash；
固件主机侧测试需要 `gcc`（可选，缺失时门禁跳过）；端到端 C 编译验收需要 LVGL 9.5 源码检出
并把 `LVGL_DIR` 指向其根目录（需含 `lvgl.h`）；固件整编/烧录需要 Tina-Linux SDK（不在本仓库）
与 `openixSuit` 或 `PhoenixSuit`。依据：`README.md` 的「环境要求」。

### 3.2 从源码构建并启动

```bash
# 构建整个解决方案（Release）
dotnet build HmiEditor.slnx -c Release

# 启动编辑器（应用工程是解决方案里的 HmiEditor.App；桌面应用，需要图形会话）
dotnet run --project src/HmiEditor.App/HmiEditor.App.csproj
```

`HmiEditor.slnx` 列出了 13 个 `src/` 工程与 8 个 `tests/` 工程；应用入口是
`src/HmiEditor.App/`（`Program.cs` + `OutputType=WinExe`）。

### 3.3 启动后看到什么

先显示引导页 `src/HmiEditor.App/Views/SplashWindow.axaml`：**新建工程**、**打开工程**、
**最近打开**（`App.axaml.cs` 的启动分支）。选择后进入主窗口 `MainWindow.axaml`。

运行日志写到应用数据目录下的 `logs/`（`src/HmiEditor.App/Services/FileLogger.cs`；
默认根 `%APPDATA%/zhishu-hmi`，见 `src/HmiEditor.Core/Services/AppPaths.cs`）。
数据根可用环境变量 `HMI_APP_DATA_ROOT` 覆盖（`AppPaths.DataRootEnvVar`）。

## 4. 工程文件与工程包

- **`.hmi`（推荐）**：ZIP 工程包，保存对话框默认扩展名即 `.hmi`
  （`MainWindow.axaml.cs` 的 `SaveProjectCore`）。
- **`.hmi.json`（旧格式）**：只能打开（打开对话框过滤器含 `*.hmi.json`），新保存一律写 `.hmi`。

工程包内结构（事实源：`src/HmiEditor.Core/Serialization/HmiPackageService.cs`）：

| 条目 | 内容 |
| --- | --- |
| `manifest.json` | 包元数据（格式版本/工程名/清单统计） |
| `project.json` | `HmiProject` 序列化（屏幕/变量/脚本/报警/趋势/配方等） |
| `assets.json` | 资产清单（图片/SVG/字体） |
| `assets/` | 资产二进制文件，文件名为 `<资产Id>.<扩展名>` |
| `reslib.json` | 工程图库（可选） |

保存前会检测悬空引用并自动修复可修复项，警告显示在状态栏（`SaveToPath`）。
自动保存仅对已保存过 `.hmi` 的工程生效，开关与间隔在 设置 → 交互 中配置
（`MainWindow.axaml.cs` 的 `InitializeAutoSave`）。

## 5. 界面构成

### 5.1 菜单

| 菜单 | 主要项（含界面标注的快捷键） |
| --- | --- |
| 文件 | 新建项目 `Ctrl+N`、打开项目 `Ctrl+O`、保存项目 `Ctrl+S`、导出 C 代码... `Ctrl+E`、编译转换 `Ctrl+B`、编译并运行 `Ctrl+Shift+B`、下载到HMI... `Ctrl+Shift+D`、验证生成代码(_V)... `Ctrl+Shift+V`、导出模拟器工程(_M)...、退出 `Alt+F4` |
| 编辑 | 撤销 `Ctrl+Z`、重做 `Ctrl+Shift+Z`、删除、全选、从选中控件创建模板(_T) `Ctrl+Shift+T`、Faceplate 模板编辑器 |
| 视图 | 屏幕管理器 `Ctrl+M`、主题编辑器 `Ctrl+T`、动画编辑器 `Ctrl+Shift+A`、放大/缩小/重置缩放、主题（深色/海洋蓝/浅色）、面板(_P)（各停靠面板入口）、重新加载面板(_L) `F5` |
| 工具(_T) | 项目设置 `Ctrl+Alt+P`、通讯配置 `Ctrl+Alt+C`、用户管理 `Ctrl+U`、报警配置 `Ctrl+Alt+A`、报警列表 `Ctrl+L`、趋势配置 `Ctrl+R`、脚本编辑器 (Lua) `Ctrl+Shift+S`、配方编辑器 `Ctrl+Shift+R`、变量批量导入 `Ctrl+Alt+V`、变量管理 `Ctrl+Shift+M`、离线仿真 `Ctrl+Shift+I`、标签监视器、交叉引用、审计日志、设备管理 `Ctrl+Alt+D`、登录 `Ctrl+Shift+L`、Git 初始化/提交/历史/刷新 |
| 语言 | 中文 / English |
| 帮助 | 关于、Dock 事件路由诊断 `F12` |

上表快捷键来自 `MainWindow.axaml` 各 `MenuItem` 的 `InputGesture`。

### 5.2 顶部工具栏（CommandBar）

文件（新建/打开/保存/导出 C）、构建与部署（编译转换、编译并运行、下载到HMI）、编辑
（撤销/重做）、变换（旋转 90°、水平/垂直镜像、重置变换）、视图（缩放、**分辨率预览**下拉、
新建屏幕、屏幕管理器、重载面板）、格式（对齐/统一尺寸/等距/组合，选中控件时才显示）、
工具（主题/设置/用户/报警/趋势/脚本/配方/变量导入/变量管理/离线仿真）。依据：`MainWindow.axaml`。

### 5.3 绘图工具栏

主窗口第三行。**绘图工具**（互斥）：选择、矩形、椭圆、折线、圆弧、直线、文本；
**控件快捷按钮**（单击即添加）：按钮、标签、滑动条、开关、复选框、LED 灯、仪表盘、柱状图、
下拉框、表格、图片、旋钮、输入框、键盘。提示文案见 `src/HmiEditor.Core/Resources/Strings.zh-CN.json` 的 `draw.*`、`tb.*`。

### 5.4 可停靠面板

主内容区由 Dock.Avalonia 托管，可拖拽/浮动/自动隐藏，布局可序列化
（`MainWindow.axaml` 的 `DockControl`、`src/HmiEditor.App/Services/HmiDockFactory.cs`）。
入口集中在 视图 → 面板(_P)：工具箱、Faceplate 模板库、原型画布、项目浏览器、属性、
图层、资源管理、HMI 图库、导航流程、审计日志、交叉引用、AI 配置。
面板空白时用 视图 → 重新加载面板(_L) 或 `F5`。

## 6. 画面与控件

### 6.1 工具箱

面板标题「工具箱」（`src/HmiEditor.App/Views/ToolboxView.axaml`）：

- 顶部搜索框按**显示名/分类/控件类型名**过滤，并显示「N 项」计数；无结果显示空状态。
- 分类：容器、输入控件、显示控件、图表控件、选择控件、特殊控件、过程图形、基础图形
  （`src/HmiEditor.Core/Resources/Strings.zh-CN.json` 的 `tb.cat.*`）。
- **添加控件**：单击列表项、把列表项**拖拽到画布**、或选中后按 `Enter`；单击添加落在画布中心
  （`MainWindow.axaml.cs` 的 `OnToolItemSelected`）。
- 搜索框内 `Ctrl+L` 聚焦、`Ctrl+A` 全选；列表内输入可打印字符会跳到搜索框继续筛选，
  `Esc` 清空/回焦点。全局 `Ctrl+L`（非文本编辑状态）也聚焦搜索框
  （`EditorCommandCatalog` 的 `FocusSearch`）。

### 6.2 画布操作

事实源：`src/HmiEditor.App/Controls/DesignCanvas.Input.cs`、`src/HmiEditor.App/Controls/CanvasSelectionPolicy.cs`。

| 操作 | 行为 |
| --- | --- |
| 左键点控件 | 选中；多选组合时选整个组合 |
| `Ctrl` + 左键点控件 | 切换该控件的选中状态 |
| 空白处拖拽 | 框选（左→右「完全包含」，右→左「相交」） |
| `Ctrl` + 点空白 | 立即开始框选 |
| 中键拖拽 | 平移画布 |
| `Ctrl` + 滚轮 | 以鼠标位置为中心缩放 |
| 拖控件主体 / 拖四周手柄 | 移动（多选整体移动）/ 缩放（仅单选显示手柄） |
| 绘图工具 + 左键 | 拖拽绘制；直线/折线为「单击落点、再击确定」 |
| `Esc` | 取消当前手势/退出绘图模式 |

### 6.3 属性面板

`src/HmiEditor.App/Views/PropertyPanelView.axaml`。选中控件后按分区显示：

- 通用：名称、文本、位置/尺寸、不透明度、圆角、边框宽/色、文字色/字体/字号/对齐；
- 屏幕属性（选中屏幕时）：名称、预设尺寸、宽高、方向、背景色/背景图、自适应模式、DPI、
  安全区、是否启动屏幕、是否公共页面、引用图层；
- 视觉状态（VisualState）：背景/文字/边框色、文本、图片；
- **变量绑定**：绑定类型、变量选择器、表达式、绑定模式；
- 值显示：显示类型、小数位、比例、前缀/后缀；
- 控件参数：按控件类型显示最小值/最大值/步长、下拉选项、行列与表头、键盘模式、
  管道方向/流速、阀门类型、表格单元格编辑等；
- 动作（Action）、Faceplate 参数、AI 建议（评分/问题清单/一键修复）。

### 6.4 图层与屏幕

- 图层面板 `LayerPanelView.axaml`：上移/下移、锁定/解锁。
- 屏幕管理器 `ScreenManagerView.axaml`：新增/切换屏幕、屏幕宽高与自适应模式、公共页面、引用图层。
- 工具栏「新建屏幕」新建并切换屏幕（`Wiring.Panels.cs`）。
- 分辨率预览下拉（工具栏）可切换 `关 / 800x480 / 1024x600 / 1280x800 / 480x800`
  只读预览；自适应布局未启用时会提示先到屏幕管理器设置 Fit/Fill
  （`OnResolutionPreviewChanged`、`status.adaptive.not.enabled`）。

## 7. 变量、绑定、报警、趋势、动画

### 7.1 变量与绑定

- 变量管理 `VariableManagerView.axaml`：变量分组增删、变量增删/复制，字段含名称、
  数据类型、地址、连接、从机、单位、缩放、轮询周期、仿真值；可增删报警；有一致性校验；
  还支持 **UDT（用户自定义类型）** 与实例。
- 变量批量导入 `VariableImportView.axaml`：选 CSV → 解析预览（名称/类型/地址/单位/缩放）→ 导入；
  解析器 `src/HmiEditor.Core/Services/CsvVariableImporter.cs`。
- 交叉引用 `CrossReferenceView.axaml`：按目标类型 + 关键字查找引用。
- 绑定：在属性面板「变量绑定」分区选择变量与绑定模式（模型：`VariableBindingData.cs`）。
  设计时可打开画布上的**预览开关**（`DesignCanvasView.axaml` 的 `PreviewToggleButton`，
  提示文案 `canvas.tt.toggle.preview`）让绑定变量的控件显示模拟值；「标签监视器」
  （工具菜单）查看标签当前值并支持手动写入。

### 7.2 报警、趋势、配方、动画

- 报警配置 `AlarmConfigView.axaml`：分组 + 报警条目（条件类型：上限/下限/偏差/位触发/
  状态变化；阈值、死区、延迟、位偏移、期望值、级别、消息、启用、需要确认）。
- 报警列表 `AlarmListView.axaml`：按状态/级别筛选，确认选中报警、清除已确认。
- 趋势 `TrendConfigView.axaml`：趋势条目、每支笔（变量/颜色/线宽/显示点/Y 轴范围/启用）、
  标题、时间窗口、图例/网格/坐标轴开关、录制开关。
- 配方 `RecipeEditorView.axaml`：分组、配方新增/克隆/删除、参数增删与排序、CSV 导入/导出。
- 动画 `AnimationEditorView.axaml`：目标控件、动画属性、触发方式、起止值、时长/延迟、
  路径类型、重复次数、关键帧时间轴、C 代码预览、播放预览。

## 8. 脚本与 AI

### 8.1 Lua 脚本

脚本编辑器（工具菜单）`ScriptEditorView.axaml`：脚本列表、分类筛选、新建/删除；
字段为名称、分类、启用、描述、代码；预置分类 `OnClick`、`OnValueChanged`、`Timer`、`Alarm`。
可用 API（界面内「Lua 帮助」文案 `script.lua.help.text`）：`hmi_read(变量)`、
`hmi_write(变量, 值)`、`hmi_set_control_property(控件, 属性, 值)`。
快捷键 `Ctrl+S` 保存、`F5` 语法校验、`F6` 调试运行
（`ScriptEditorView.axaml.cs` 的 `OnEditorKeyDown`；调试运行复用预览仿真值做沙箱执行）。
「AI 生成」按钮用自然语言生成脚本。

### 8.2 AI 面板

「AI 配置」面板 `AiChatPanelView.axaml`：配置区（Provider：OpenAI 兼容 / Claude、
Endpoint、Model、API Key、保存配置）；**智能体模式**开关（默认开，AI 调用工具操作工程，
完成后可「回滚本次更改」）；新会话、输入框、发送、停止/取消；生成结果有 预览/应用/取消，
失败的会话可重试；消息列表支持流式正文预览与工具步骤轨迹；图片生成结果可保存为 SVG / PNG。

其它 AI 入口：资源管理 →「AI 生成图标」（生成 SVG 图标加入资源库，双击 SVG 资源应用到
选中的 SVG 控件，`AssetManagerView.axaml`）；Faceplate 模板库 →「AI 生成模板」
（`FaceplateLibraryView.axaml`）；脚本编辑器 →「AI 生成」（见上）。

## 9. 模拟器预览

两条独立路径，产出的工程都用 **CMake** + **SDL2** 构建，并需要一份含 `lvgl.h` 与
`CMakeLists.txt` 的 **LVGL 源码**（`src/HmiEditor.App/Services/SimulatorBuildService.cs`、
`src/HmiEditor.Core/Services/SimulatorProjectExporter.cs`）。

**一键编译并运行**：文件 → 编译并运行（`Ctrl+Shift+B`）。LVGL 目录探测顺序：设置 →
环境变量 `LVGL_DIR` → 工作目录上级 → 常用路径（`SimulatorBuildService.FindLvglDirectory`）；
找不到会提示「未找到 LVGL 源码目录，请在设置中配置 LVGL 路径」。流程为 `[1/4] 生成模拟器工程
→ [2/4] cmake configure → [3/4] cmake build → [4/4] 编译成功`，产物 `hmi_sim` 自动运行；
编译失败时**不会**启动模拟器。LVGL 与工作目录可在 设置 → 模拟器 中配置。

**导出模拟器工程（自行编译）**：文件 → 导出模拟器工程(_M)...，选目录后生成
`<目录>/hmi_simulator/`，含 `main.c`、`CMakeLists.txt`、`lv_conf.h`、`generated_hmi.c`、
`generated_security.h`、`generated_runtime.h`、`README.md` 等（`SimulatorProjectExporter.Export`）。
导出前若生成代码存在 Error 级问题会被阻断，需先「验证生成代码」。导出目录的 `README.md`
含平台依赖安装与构建命令：`cmake -B build -DLVGL_DIR=../lvgl .` → `cmake --build build`
→ `./build/hmi_sim --help`。

`hmi_sim` 支持 `--window <WxH>`、`--scale <n>`、`--fps <n>`、`--login <用户名>`、
`--selftest`、`--help`（参数解析见生成的 `main.c`）。无显示环境可自检：
`SDL_VIDEODRIVER=dummy ./build/hmi_sim --selftest`（`scripts/verify.sh` 第 7 步即此用法）。

**离线仿真（数据源）**：工具 → 离线仿真，给变量配信号类型（常量/正弦/方波/斜坡/
递增/随机/手动）、幅值/频率/偏移/噪声、周期与倍速，并有寄存器读写、响应模式与仿真日志。

## 10. 导出 C 代码与编译转换

**导出 C 代码**（`Ctrl+E`）：输出目录取 `ExportConfig.OutputDirectory`（默认 `./export`，
相对工程目录解析）；未配置或工程未保存时弹目录选择框
（`BuildPipelineService.ExportAsync`、`src/HmiEditor.Core/Models/ExportConfig.cs`）。
产物为 `generated_hmi.c`（或按语言设置输出 `.cpp`）+ 构建文件。

**编译转换**（`Ctrl+B`，进度在状态栏），步骤见 `BuildPipelineService.CompileAsync`：
解析输出目录 → 生成源码并写盘 → 额外生成 SharedLibrary 变体 `generated_hmi_so.c`
→ 生成 `tags.h` 并用 T113 工具链交叉编译为 `libhmi_ui.so` → 成功后启用「下载到HMI」。

交叉编译按以下顺序探测 **Tina SDK 或独立工具包**
（`src/HmiEditor.Device/Build/T113SoBuilder.cs`）：设置 → 真机部署 → 「Tina SDK / 工具包目录」
或「交叉编译器路径」→ 环境变量 `ZHSHU_HMI_SDK` → 仓库内 `firmware/Tina-SDK` /
`firmware/Tina-Linux`（需含 `out/`）→ 独立工具包 `t113-cross-kit`（含 `kit.json`）。
未找到工具链时**不中断**，只是不产出 `.so` 并提示：「未找到 T113 交叉工具链，未生成 .so
（可在 设置→真机部署 配置 Tina SDK 目录）」。

**生成代码验证**（`Ctrl+Shift+V`）：跑静态质量门，弹出「生成代码验证」窗口列出
Error/Warning 并在状态栏汇总（`MainWindow.axaml.cs` 的 `ValidateGeneratedCode`、
`src/HmiEditor.Core/Services/GeneratedCodeValidator.cs`）。

## 11. 下载到 HMI 与设备侧生效

文件 → 下载到HMI...（需先编译转换成功）。对话框 `DownloadToHmiView.axaml` 提供：
连接通道（串口：端口+波特率，默认 115200；以太网：默认 `192.168.100.100:23`）、
设备目标目录（默认 `/mnt/UDISK/hmi/src`）、HMI 程序 (.so)（自动检测编译产物，也可
「浏览…」手动选择）、操作日志。

部署内容与设备端路径（事实源：`src/HmiEditor.Device/Updates/HmiDeployService.cs`）：

| 内容 | 设备端路径 |
| --- | --- |
| 源码工程 | `/mnt/UDISK/hmi/src/` |
| 工程资源（图片/字体） | `/mnt/UDISK/hmi/assets/` |
| 预编译 `libhmi_ui.so` | `/mnt/UDISK/hmi/libhmi_ui.so` |
| 运行时连接配置 `comm_config.ini` | `/mnt/UDISK/hmi/comm_config.ini` |

生效方式：`libhmi_ui.so` **只在 hmi_runtime 进程启动时加载一次**，因此部署 `.so` 后会执行
`killall hmi_runtime`，由 procd（`S97hmiruntime` respawn）自动拉起新进程；未自动拉起时回退
`/etc/init.d/S97hmiruntime restart`（或 `S99hmiruntime`）。只上传源码/资源、不部署 `.so` 时
**界面不会更新**（对话框会明确提示）。部署 `comm_config.ini` 让连接参数改完即生效、无需重编
固件；工程无启用连接时会删除设备旧配置文件（`firmware/t113/README.md`）。

## 12. 设备管理与固件

工具 → 设备管理 `Ctrl+Alt+D`（`DeviceManagerView.axaml`）提供：连接/检测设备（型号/架构/
内核/固件版本/HMI 版本）、检查更新、**HMI 程序更新**（`.swu`）、**固件刷写**（`.swu`）、
**USB 更新包生成**（`libhmi_ui.so` + U 盘/TF 卡输出目录）、**FEL 镜像烧录**（工具路径/镜像/参数）。

> **固件烧录/升级/回滚的具体步骤不在本文复述**，一律以固件文档为准：
> `firmware/t113/docs/flashing-guide.md`（NAND 线刷 PhoenixSuit/openixcli、TF 卡启动
> PhoenixCard、openixcli「Boot1 not found in firmware」的成因与 Boot0/Boot1 查找键（镜像本身无需打补丁）、首次启动验证、快捷部署）、
> `firmware/t113/docs/wavepi-s6-flash-guide.md`（WavePI-S6 刷写与刷回官方镜像）、
> `firmware/t113/docs/version-info.md`（当前固件版本、ABI 与已知限制）。
> 独立交叉编译工具包的生成见 `tools/T113KitBuilder/Program.cs`（`--sdk/--out/--zip`）。

## 13. 设置

**全局设置**（工具栏「设置」按钮，`SettingsDialog.axaml`）：主题（深色工业/浅色高对比/
海洋蓝、强调色）、布局（左/右/底面板尺寸、自动隐藏）、交互（动画、网格吸附、网格点间距、
撤销栈、显示网格/标尺、实时预览、自动保存与间隔、吸附步长）、显示（默认字体、字号步进、
画布背景色、默认预览分辨率、工具提示、预览遮罩）、项目浏览器（右键增强菜单）、
模拟器（LVGL 源码目录、工作目录）、真机部署（Tina SDK/工具包目录、交叉编译器路径）。
灰显项**尚不生效**，悬停显示原因（`settings.not.effective.tip`）。

**工程设置**（工具 → 项目设置 `Ctrl+Alt+P`，`ProjectSettingsView.axaml`）6 个分类：基本信息、
显示设置、通信设置、运行时设置、导出设置、安全设置。安全设置可维护**运行时用户表**
（用户名/显示名/角色/启用/密码），它会导出到 `generated_security.h` 随工程下发
（面板内提示文案原文如此）。

## 14. 常见任务速查

| 我想… | 怎么做 |
| --- | --- |
| 新建工程 | 启动页「新建工程」或 文件 → 新建项目 |
| 添加一个按钮 | 工具箱点「按钮」或拖到画布；也可点绘图工具栏的按钮快捷按钮 |
| 让控件显示某变量 | 属性面板 → 变量绑定 → 选变量/绑定模式 |
| 看设计时模拟值 | 打开画布上的预览开关（提示文案 `canvas.tt.toggle.preview`） |
| 建报警 | 工具 → 报警配置；运行期在 工具 → 报警列表 里确认 |
| 加趋势 | 工具 → 趋势配置，添趋势与笔 |
| 写事件脚本 | 工具 → 脚本编辑器 (Lua) `Ctrl+Shift+S`，`F6` 调试运行 |
| 让 AI 改工程 | 视图 → 面板 → AI 配置，填配置后开智能体模式，可一键回滚 |
| PC 上看效果 | 文件 → 编译并运行 `Ctrl+Shift+B`（需 LVGL 源码 + CMake + SDL2） |
| 只导出模拟器工程 | 文件 → 导出模拟器工程(_M)... |
| 出 C 代码 / 出可下发的 .so | 文件 → 导出 C 代码... `Ctrl+E` / 文件 → 编译转换 `Ctrl+B` |
| 下发到设备 | 文件 → 下载到HMI...，选串口或以太网 |
| 检查生成代码质量 | 文件 → 验证生成代码(_V)... |
| 改界面语言 / 换主题 | 菜单 语言 → 中文/English；视图 → 主题，或全局 `Ctrl+J` 循环切换 |
| 面板空白了 | 视图 → 重新加载面板(_L) 或 `F5` |
| 设备端界面没变 | 确认部署了 `libhmi_ui.so`（见第 11 节） |

## 15. 相关文档

- [README.md](../README.md)（仓库结构与环境要求）
- [docs/project-status.md](project-status.md)（状态基线与复现命令）
- [docs/2026-09-17-open-items-register.md](2026-09-17-open-items-register.md)（未闭环项与已知限制）
- [firmware/t113/README.md](../firmware/t113/README.md)（固件结构与连接配置）
- [docs/troubleshooting.md](troubleshooting.md)（故障排查手册）
- [firmware/t113/docs/flashing-guide.md](../firmware/t113/docs/flashing-guide.md)（固件烧录指南）
