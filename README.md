# zhishu-hmi

面向工业现场的 HMI（人机界面）编辑器：**在 PC 上可视化设计画面，一键生成 LVGL C 代码**，
同一份工程既能在 PC 上以 SDL 模拟器预览，也能编译成共享库下发到 T113-S3 设备运行。

| 项目 | 说明 |
| --- | --- |
| 编辑器 | C# / .NET 10 + Avalonia 12.1（跨平台桌面，Linux 为主力开发平台） |
| 目标运行时 | LVGL 9.5 + FreeType（中文矢量/点阵字体） |
| 目标设备 | 全志 T113-S3，1024x600 RGB666，SPI NAND，Modbus RTU/TCP + MQTT |

## 仓库结构

| 目录 | 内容 |
| --- | --- |
| `src/` | 编辑器与服务端源码，**12** 个 C# 项目（App / Core / CodeGen / AI / Device / Simulator / 通讯与硬件插件各 3）；`HmiEditor.Scripting` 已于 2026-09-20 整程序集下架，由 Core 内的 `LuaSyntaxValidator` 承接真解析 |
| `tests/` | **8** 个测试程序集（单元 / 回归 / headless UI / 端到端）+ `HmiEditor.TestSupport` 支撑库 |
| `tools/` | 命令行工具：`MockHmiGen`（生成 Golden 夹具工程）、`SimExport`（导出并编译模拟器）、`T113KitBuilder` |
| `firmware/t113/` | 设备侧：HMI 运行时（C）、Tina 打包脚本、板级配置、主机侧测试 |
| `hardware/` | KiCad 原理图 / PCB / BOM |
| `docs/` | 状态基线、审查报告、ADR、设计与调研文档（历史产物在 `docs/archive/`） |
| `scripts/` | 质量门禁与辅助脚本 |
| `patches/` | 针对上游 LVGL 的补丁（负 scale 镜像修复等） |

## 环境要求

| 用途 | 需要 |
| --- | --- |
| 构建 / 测试编辑器 | .NET SDK **10.0.112**（版本由 `global.json` 固定）、bash |
| 固件主机侧测试 | `gcc`（可选；缺失时门禁会跳过，CI 上以 `CI_REQUIRE_FIRMWARE_TESTS=1` 转成失败） |
| 端到端 C 编译验收 | LVGL 9.5 源码检出，`LVGL_DIR` 指向其根目录（需含 `lvgl.h`） |
| 固件整编 / 烧录 | Tina-Linux SDK（数十 GB，**不在本仓库**）、`openixSuit` 或 `PhoenixSuit` |

## 快速开始

```bash
# 构建（Release）
dotnet build HmiEditor.slnx -c Release

# 全量测试
dotnet test HmiEditor.slnx -c Release --no-build
```

## 质量门禁

```bash
# 快速门禁：脚本语法 + 构建 + 全量测试 + 固件主机侧测试（无外部依赖）
bash scripts/ci-gate.sh

# 端到端验收：Golden 工程导出 → 真实 LVGL CMake 编译 → 300 帧自检 → CTest
LVGL_DIR=~/Git/lvgl bash scripts/verify.sh
```

CI 为 Gitee Go，配置在 `.workflow/`：`csharp-gate.yml` 直接调 `ci-gate.sh`，并以
`CI_REQUIRE_FIRMWARE_TESTS=1` 运行，保证门禁「能失败」而不是纸面绿色。

## 固件

```bash
# 主机侧单元测试：通讯/变量表、触摸坐标归一化、板级配置（不需要硬件与 SDK）
bash firmware/t113/tests/run_host_tests.sh

# 整编 / 打包（需先准备 Tina-Linux SDK，TINA_ROOT 指向其根目录）
cd firmware/t113/tina
./build.sh all <TINA_ROOT>                    # 默认板 t113-mq_r
BOARD=bingpi_m2 ./build.sh all <TINA_ROOT>    # WavePI-S6 7 寸板
```

产物为 `<TINA_ROOT>/out/t113-mq_r/tina_t113-mq_r_uart1.img`（`bingpi_m2` 对应
`tina_t113-bingpi_m2_uart3.img`）。烧录步骤见 `firmware/t113/docs/flashing-guide.md`
与 `wavepi-s6-flash-guide.md`。

板级差异（调试串口 / RS485 串口与方向 GPIO / 触摸量程 / 屏参）的**唯一事实源**是
`firmware/t113/app/runtime/board_profile.h`，改板级参数请只改这里。

## 常见问题

**测试报 `IOException: disk was full`？**

导出类测试会写出整套模拟器工程（`main.c`、`generated_*.c`、`CMakeLists.txt` 等）。
若系统 `TMPDIR` 指向小容量 tmpfs（例如 10 MB），测试会自动降级到仓库内 `.hmi-test-tmp/`
（已 gitignore）；也可用环境变量显式指定：

```bash
HMI_TEST_TMPDIR=/path/to/spacious/dir dotnet test HmiEditor.slnx -c Release
```

注意**不要**把它指向本仓库内部的目录：`git` 会在这些目录里向上发现仓库自身的 `.git`，
使 Git 工作流测试把「普通目录」判定成仓库、把历史读成父仓库的提交（表现为一堆看似产品缺陷的断言失败）。
这类 git 测试已自行避开 git 工作树，但把临时目录放在仓库外最省心。
同理，跑 `scripts/verify.sh` 时若系统 `TMPDIR` 空间不足（Golden 工程含字体资源），
也应把 `TMPDIR` 指向**仓库外**的有空间目录。

**`scripts/ci-gate.sh` 报 `/dev/fd/63: 没有那个文件或目录`？**

这是**历史问题**：旧版脚本用进程替换 + `mapfile` 收集脚本清单，依赖 bash 的 `/dev/fd` 支持，
受限沙箱 / 精简容器里会静默拿到空清单（门禁变成永远通过的假门禁）。现已改为
`mktemp` 临时文件收集**并校验清单非空**（见 `scripts/ci-gate.sh` 第 [1] 步注释）。
若现在仍看到 `/dev/fd/63`，说明跑的是旧脚本（或改动被回退）——先核对 `scripts/ci-gate.sh` 是否为最新。

## 文档入口

| 文档 | 用途 |
| --- | --- |
| `docs/user-guide.md` | **用户手册**：从构建启动到设计、模拟预览、导出编译、下载到设备的完整操作 |
| `docs/troubleshooting.md` | **故障排查手册**：症状 → 原因 → 处置 → 依据 |
| `docs/project-status.md` | **状态基线**：构建/测试/验收的实测数字与复现命令（数字必须来自实测） |
| `docs/2026-09-17-open-items-register.md` | **未闭环项的唯一权威登记表**（状态以它为准） |
| `docs/2026-10-11-unfinished-plan-ledger.md` | **未完成计划台账**：跨计划汇总 ＋ 每项可复跑的只读核对命令（单项权威仍是上一行） |
| `docs/2026-10-11-tls-ops-runbook.md` | **设备侧 TLS 运维手册**：凭据轮换（PSK/X.509）· 审计事件表 · **TLS 失败时如何与"真离线"区分** |
| `docs/2026-09-18-mainstream-gap-check-and-optimization-plan.md` | 与主流软件差距核查 + 优化计划（07-30/08-02 差距矩阵全部闭环后的那一轮） |
| `docs/2026-09-14-optimization-directions.md` | 更早一轮的检查结论与优化项 |
| `docs/2026-09-13-project-review-and-optimization.md` | 上一轮全面审查（P0/P1/P2 分级，含证据） |
| `docs/adr/` | 架构决策记录（含 UI 是否 MVVM 化的决策） |
| `firmware/t113/README.md` | 固件结构、板级差异、目录说明 |
| `docs/archive/2026-08/` | 已归档的历史诊断报告与验证截图 |

## 工程约定

- **包版本单一来源**：根 `Directory.Packages.props` 统一声明，`*.csproj` 内不写 `Version`。
- **编译期特性**：`src/` 与 `tests/` 各自的 `Directory.Build.props` 已开启
  `Nullable` / `ImplicitUsings`，并对齐 `TargetFramework`。
- **设计常量单一来源**：颜色、尺寸、角度等以 `src/HmiEditor.Core/Design/` 为准，不要散写裸 RGB/hex。
- **生成代码断言**：用 `tests/HmiEditor.Core.Tests/Support/CSource.cs` 按「调用点」匹配，
  不要写 `Assert.Contains("lv_obj_set_pos(obj_c1, 10, 20)")` 这类与排版绑定的断言。
- **测试临时目录**：用 `tests/HmiEditor.TestSupport/TestTempDir.cs`（`NewDir` / `NewFilePath`；创建即登记，进程退出兜底清扫），不要直接调 `Path.GetTempPath()`。
- **归档而不是随手放根目录**：一次性报告与截图请落到 `docs/` 或 `docs/archive/`，
  根目录只保留工程入口文件。
