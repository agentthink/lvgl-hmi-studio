# zhishu-hmi 故障排查手册

面向使用编辑器的现场工程师。
每条形如：**症状（你看到什么）→ 原因 → 处置（可执行命令/操作）→ 依据（仓库路径）**。
文中引用的错误文案均摘自源码，可直接搜索定位。

## 1. 构建与测试

### 1.1 `dotnet build` 报 SDK 版本不匹配 / 找不到 .NET 10

- **原因**：仓库用 `global.json` 固定 SDK 版本并要求 `rollForward: latestFeature`、不允许预览版。
- **处置**：在该目录执行 `dotnet --version` 核对，安装符合 `global.json` 的 .NET SDK 后重试。
- **依据**：`global.json`；各 `*.csproj` 的 `TargetFramework`。

### 1.2 测试写盘失败：`IOException: disk was full` / `No space left on device`

- **原因**：导出类测试会写出整套模拟器工程（`main.c`、`generated_*.c`、`CMakeLists.txt` 等），
  系统 `TMPDIR` 若指向小容量 tmpfs（例如 10 MB）就会写满。
- **处置**：把临时目录指到仓库外的大目录，例如
  `HMI_TEST_TMPDIR=/path/to/spacious/dir dotnet test HmiEditor.slnx -c Release`；
  缺省回退到仓库内 `.hmi-test-tmp/`（已 gitignore）。
- **注意**：**不要**指向本仓库内部——git 会向上发现仓库自身的 `.git`，使 Git 工作流测试把普通目录
  判成仓库、把历史读成父仓库的提交，表现为一堆看似产品缺陷的断言失败。
- **依据**：`README.md`「常见问题」、`tests/HmiEditor.TestSupport/TestTempDir.cs`。

### 1.3 `scripts/verify.sh` 中途因空间不足失败，报错点却看着无关

- **原因**：Golden 工程（含字体资源）与 LVGL 全量编译产物（数百 MB）都落在临时目录下。
- **处置**：脚本第 [6] 步自带早失败检查；按提示把 `TMPDIR` 指到仓库外 ≥1 GB 的目录：
  `TMPDIR=$HOME/.cache/zhishu-hmi-verify LVGL_DIR=~/Git/lvgl bash scripts/verify.sh`。
- **依据**：`scripts/verify.sh`；`docs/project-status.md`「复现命令」。

### 1.4 `scripts/ci-gate.sh` 报 `/dev/fd/63: 没有那个文件或目录`

- **症状**：门禁在「bash -n 脚本语法检查」前报 `/dev/fd/63` / `/dev/fd/62` 打不开。
- **原因**：这是**历史问题**——旧版脚本用进程替换 + `mapfile` 收集脚本清单，依赖 bash 的 `/dev/fd`；
  受限沙箱里会静默拿到空清单（假门禁）。现行脚本已改为 `mktemp` 临时文件 + 清单非空校验。
- **处置**：若仍复现，说明跑的是旧脚本或改动被回退：核对 `scripts/ci-gate.sh` 第 [1] 步是否为
  「临时文件收集 + `script_count -gt 0` 校验」；不要用「手工展开这一步」绕过门禁。
- **依据**：`scripts/ci-gate.sh` 第 [1] 步注释、`README.md`「常见问题」。

### 1.5 路径卫生门禁失败（硬编码开发机家目录）

- **症状**：门禁「路径卫生」步骤指出某文件里有被引号包裹的 `/home/<用户名>/...`。
- **原因**：产品/工具代码不得硬编码开发机家目录（会在他人机器上越界写盘、产物路径漂移、
  提示语复制粘贴即失败）。
- **处置**：改为相对路径 / 命令行参数 / 环境变量 / 从仓库相对定位；注释与文档里裸写路径允许。
- **依据**：`scripts/check-path-hygiene.sh`。

### 1.6 应用启动即崩且没有日志 / `AppBuilder` 报重复键

- **症状**：报 `ArgumentException: An item with the same key has already been added.
  Key: /Assets/Icons.axaml`。
- **原因**：Avalonia 资源目录里该键重复（同一个 `.axaml` 被同时当作 `AvaloniaResource` 与
  默认 glob 的 `AvaloniaXaml`）；增量构建把 XAML 编译步骤错开时可能残留。
- **处置**：对整个解决方案做一次干净构建（`dotnet build HmiEditor.slnx -c Release`），
  不要单独增量构建 `HmiEditor.App` 工程。
- **依据**：`src/HmiEditor.App/HmiEditor.App.csproj` 注释、
  `docs/2026-09-17-avalonia-resource-duplicate-key.md`；
  启动阶段日志位置见 `src/HmiEditor.App/Services/FileLogger.cs`（数据根下 `logs/`，
  数据根可用 `HMI_APP_DATA_ROOT` 覆盖，见 `src/HmiEditor.Core/Services/AppPaths.cs`）。

### 1.7 固件主机侧测试被跳过或失败

- **原因**：`run_host_tests.sh` 用 `gcc` 编译/运行 **9** 个主机侧步骤（前 8 步是 host 测试，
  第 9 步是 `lv_conf.h` 配置守卫）；无 gcc 时门禁默认跳过，
  CI 用 `CI_REQUIRE_FIRMWARE_TESTS=1` 转成失败。
- **处置**：安装 `gcc`；单独验证：`bash firmware/t113/tests/run_host_tests.sh`。
- **依据**：`README.md`、`scripts/ci-gate.sh` 第 **[7]** 步、`firmware/t113/tests/run_host_tests.sh`。

## 2. 导出与编译

### 2.1 「未找到 LVGL 源码目录，请在设置中配置 LVGL 路径」

- **原因**：没找到可用的 LVGL 源码目录；探测顺序为 设置 → 环境变量 `LVGL_DIR` → 工作目录上级
  → 常用路径。
- **处置**：在 设置 → 模拟器 →「LVGL 源码目录」填目录，或设置 `LVGL_DIR`；该目录必须同时含
  `lvgl.h` 与 `CMakeLists.txt`（否则报「LVGL 源码无效: …」）。
- **依据**：`src/HmiEditor.App/MainWindow.axaml.cs` 的 `BuildAndRunSimulator`、
  `src/HmiEditor.App/Services/SimulatorBuildService.cs` 的 `FindLvglDirectory`。

### 2.2 「未找到 cmake，请安装 CMake 后重试」

- **处置**：安装 CMake（Linux: `sudo apt install cmake`），或设置 `CMAKE_EXECUTABLE` 指定路径。
- **依据**：`SimulatorBuildService.cs` 的 `FindCmake`。

### 2.3 编译转换提示「未找到 T113 交叉工具链，未生成 .so」

- **症状**：源码生成成功、`下载到HMI` 也解锁了，但没有 `.so` 产物。
- **原因**：这是**软失败**（不中断流程）：未找到 Tina SDK 或独立交叉编译工具包。
- **处置**：在 设置 → 真机部署 配置「Tina SDK / 工具包目录」或「交叉编译器路径」；也可设置
  环境变量 `ZHSHU_HMI_SDK`，或把 SDK 放在仓库内 `firmware/Tina-SDK` / `firmware/Tina-Linux`
  （需含 `out/`）。客户机可用独立工具包（含 `kit.json`）。
- **依据**：`src/HmiEditor.App/Services/BuildPipelineService.cs` 的 `compile.so.toolchain.missing`；
  `src/HmiEditor.Device/Build/T113SoBuilder.cs` 的 `FindSdkRoot`。

### 2.4 交叉编译日志逐条列出「✗ 未找到 …」

前置缺失时一次性列出原因（不抛异常）。常见几条：

| 日志文案（节选） | 含义与处置 |
| --- | --- |
| `未找到 Tina SDK/交叉编译工具包（设置 T113 SDK 目录或环境变量 ZHSHU_HMI_SDK）` | 见 2.3 |
| `未找到固件头契约（.so 编译需要 screen_manager.h/hmi_ui_api.h；工具包为 <kit>/firmware/app）` | SDK 不在仓库 `firmware/` 下时推断不出仓库根；改用仓库内 SDK 或工具包 |
| `未找到交叉编译器 arm-openwrt-linux-*-gcc（SDK out/staging 或工具包 toolchain/bin）` | 在设置里显式指定该 `*-gcc` 完整路径 |
| `未找到 LVGL 源码根（SDK package/gui/lvgl 或工具包 lvgl）` | SDK 需有 `package/gui/lvgl`（含 `lvgl.h`） |
| `未找到固件 LVGL 配置 lv_conf.h` / `未找到 freetype2 头文件` / `未找到固件运行头文件 screen_manager.h` | SDK 布局需为 `<repo>/firmware/t113/{config,app/runtime}`，且 `out/` 下有 staging |
| `未找到标签契约头 tags.h（请先编译转换生成工程 tags.h）` | 回到编辑器执行「编译转换」，它会在输出目录先生成 `tags.h` |

- **依据**：`T113SoBuilder.ResolveContext`（以上 missing 文案都在此）。

### 2.5 「交叉编译失败 (exit N)」/「链接失败 (exit N)」

- **原因**：工具链找到了，但编译或链接报错（状态栏已逐行回显 `[.so]` 输出）。
- **处置**：按 `[.so]` 前缀的编译器输出定位；确认 SDK staging 与所用工具链匹配。
- **依据**：`T113SoBuilder.BuildAsync`。

### 2.6 「模拟器工程导出被阻断: 生成代码存在 Error 级问题」

- **原因**：导出模拟器工程前会自动跑静态验证，有 Error 级问题即阻断。
- **处置**：先执行 文件 → 验证生成代码(_V)...，按结果窗口清单修正后重试。
- **依据**：`MainWindow.axaml.cs` 的 `ExportSimulatorProjectCore`（`status.simulator.blocked.errors`）、
  `src/HmiEditor.Core/Services/GeneratedCodeValidator.cs`。

### 2.7 打开 `.hmi` 报「缺少 manifest.json」/「不支持的工程包格式版本」

- **原因**：文件不是有效工程包，或由更新版本编辑器创建（格式版本更高）。
- **处置**：确认文件来自本编辑器的「保存项目」；跨版本工程用创建它的编辑器打开。
- **依据**：`src/HmiEditor.Core/Serialization/HmiPackageService.cs` 的 `Unpack`。

## 3. 设备与下载

### 3.1 下载完成但设备界面没变

- **原因**：`libhmi_ui.so` 只在 hmi_runtime 进程启动时加载一次；只上传源码/资源不更新界面。
- **处置**：确认下载对话框里「HMI 程序 (.so)」已选中；没有 `.so` 时先做「编译转换」，或用
  「浏览…」手动选择交叉编译产物。部署 `.so` 后编辑器会 `killall hmi_runtime`，
  让 procd 自动拉起新进程。
- **依据**：`src/HmiEditor.Device/Updates/HmiDeployService.cs`；文案 `download.so.missing.hint`
  （`src/HmiEditor.Core/Resources/Strings.zh-CN.json`）。

### 3.2 提示「所选 .so 不是 ARM ELF…无法在 T113 加载，已跳过部署」

- **原因**：本地 ELF 头校验失败（非 32 位小端 EM_ARM），dlopen 必然失败。
- **处置**：换用 T113 交叉编译产物（见 2.3/2.4），不要用 PC 上编出的 `.so`。
- **依据**：`HmiDeployService.IsArmElf`。

### 3.3 「部署完成，但 hmi_runtime 未自动重启」

- **原因**：procd 未托管（旧固件或手动启动），`killall` 后无人拉起；编辑器已回退执行
  `/etc/init.d/S97hmiruntime restart`（或 `S99hmiruntime`）仍失败。
- **处置**：串口/SSH 登录设备查进程与日志，确认 `/etc/init.d/S97hmiruntime` 存在且可执行。
- **依据**：`HmiDeployService.DeployAsync` 的重启与兜底分支；
  `firmware/t113/docs/flashing-guide.md`「开机自启动配置」。

### 3.4 日志显示 `ui_loaded=0`（新程序加载失败）

- **原因**：`.so` 已部署并重启，但新 HMI 程序加载失败——通常是与设备固件的 ABI/标签表不匹配。
- **处置**：核对设备固件与工程运行时版本（`firmware/t113/docs/version-info.md` 的 ABI 说明），
  必要时先升级宿主固件再下发 `.so`。
- **依据**：`HmiDeployService.DeployAsync` 的 `ui_loaded=0` 分支。

### 3.5 触摸、通讯与固件烧录类

这三类一律按固件文档排查，不在本文发明步骤：

- 触摸无反应 / 边缘点不到：`firmware/t113/docs/touch-hardware-diagnosis.md`、
  `firmware/t113/docs/flashing-guide.md` 第 7 节、`firmware/t113/docs/version-info.md`
  （触摸量程与 DTS 必须一致）、`firmware/t113/tina/dts/README.md`。
- Modbus / RS485 不通：先看 `firmware/t113/docs/flashing-guide.md` 第 7 节；连接参数可在编辑器
  「通讯配置」改完经「下载到 HMI」下发 `comm_config.ini` 免重编固件（格式与优先级见
  `firmware/t113/README.md`）。
- 固件烧录失败：`firmware/t113/docs/flashing-guide.md`（openixcli「Boot1 not found in firmware」
  的成因与 Boot0/Boot1 的 (maintype, subtype) 查找键、图像归一/自检脚本
  `tools/patch_image_boottype.py`（`--check`）、NAND 线刷、TF 卡启动、快捷部署
  `firmware/t113/scripts/deploy.sh`）、`firmware/t113/docs/wavepi-s6-flash-guide.md`
  （供电不足致 USB 传输超时、`Device reconnect failed`、刷回官方镜像 `flash_wavepi.sh --factory`）。

## 4. 已知限制（不要当成新缺陷反复排查）

### 4.1 Debug 全量并发下 `HmiEditor.App.UiTests` 的 testhost 会崩/挂死

- **症状**：`dotnet test HmiEditor.slnx`（Debug）跑到 UI 测试程序集时长时间无输出，或报
  「测试主机进程崩溃」；单独跑该项目却恒定全绿。
- **原因**：已定性为「进全量就出事」的间歇缺陷（复现率约 2/3），成因未定位；同一沙箱还存在一条
  与 headless 会话无关的失败入口（测试宿主根本起不来）。
- **处置**：门禁与端到端验收**只认 Release**（`scripts/ci-gate.sh` 与 `scripts/verify.sh`
  默认 `CONFIG=Release`）；不要拿 Debug 全量读数下结论。需要当场点名挂死测试时可加
  `--blame-hang --blame-hang-timeout 90s --blame-hang-dump-type none`（注意 blame 本身是混淆变量）。
- **依据**：`docs/project-status.md`「当前阻断项」第 12 条、
  `docs/2026-09-17-open-items-register.md` §5 第 27 项、
  `docs/2026-09-17-verify-end-to-end-revalidation.md`。

### 4.2 其余已知限制

| 说明（原因） | 处置 | 依据 |
| --- | --- | --- |
| Git 工作流用例全量并行偶发红：沙箱对「首次出现的文件路径」收约 60 ms 且全局串行，`git init` 撞穿 15 s 等待窗口。**（2026-09-27 阶段 115 又撞一次，指纹如下）**失败用例**耗时 50 s ≈ 3 × 15 s**，而断言落在**下游**那一条：`Commit_ThenHistory_ShowsEntry` 报 `Assert.NotEmpty() Failure: Collection was empty` （因为 `InitializeRepository` / `Commit` 的**返回值被测试丢弃** ⇒ 撞穿窗口这件事不报自己、只在下游显出「历史是空的」）。**证据**：同一批 8 条用例独立复跑 **5/5 绿 / 每次 241 ms**（`.workbuddy/tmp/s115-gitprobe1.log`）；同一棵树的门禁重跑，那一批 **551 ms**（`s115-gate1c.log`）⇒ **慢的那次与代码无关**，也不是门禁自身负载（两次门禁步 5 读数几乎相同） | 门禁已把 spawn git 的用例拆出单独跑（判据未放宽）；手工排查用 `--filter` 单独跑；⚠ **认这条指纹**：红的是非 `InitializeRepository*` 的同类用例也可以，只要「耗时 ≈ 15 s 的整数倍 ＋ 断言在下游」。**建议顺手改一处**（代价 2 行、不放开任何判据）：把那批用例的返回值从「丢弃」改成断言（`Assert.True(InitializeRepository(dir))` ＋ `var (ok, err) = Commit(...); Assert.True(ok, err)`）⇒ 失败消息就直接点名 `git 命令超时`，省掉一次 50 s 黑箱取证 | `scripts/ci-gate.sh` 第 [6] 步注释、`docs/2026-09-17-sandbox-newpath-serialization.md`；2026-09-27 那次现场：`.workbuddy/tmp/s115-gate1.log`（红，mtime `20:20:39`）／`s115-gate1b.log` ＋ `s115-gate1c.log`（绿，`20:25:13` / `20:29:51`） |
| `RuntimeConfig` 部分字段不走设备：`HistoryStoragePath`/`HeartbeatIntervalMs`/`MaxRuntimeTasks`/`EnableCrashDump`/`CrashDumpSector` 未下发，`EnableTamperDetection` 零消费，`WatchdogTimeoutMs`/`EnableHmiReboot` 是死字段（保留为老工程 JSON 兼容） | 在工程设置改这些项不会改变设备行为，闭环需先立机制 | `docs/2026-09-17-open-items-register.md` §3 第 9–11 项、`docs/2026-09-16-runtime-config-sinking.md` |
| 编辑器侧一键出发布包：`bash scripts/package-release.sh`（发布编辑器 + 生成版本清单/校验和/升级回滚说明，可选 `--with-kit` 带 T113 交叉工具包）；出包后 `bash scripts/package-release.sh --verify <包目录>` 复核；固件侧 OTA 热更新 + UDISK 优先 + 出厂回退见 `firmware/t113/docs/version-info.md` | 出包与升级回滚步骤见包内 `UPGRADE.md` | `scripts/package-release.sh`、`tools/ReleasePackager/Program.cs`、`docs/2026-09-18-release-packaging.md` |
| 极端迟到的设备回包仍可能串帧（晚于首字节宽限窗口） | 避免通道繁忙时下发超长命令；失败重试 | `docs/2026-09-17-open-items-register.md` §3 第 14 项、`docs/2026-09-16-channel-frame-completion-contract.md` |
