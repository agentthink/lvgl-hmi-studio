# 硬编码开发机路径：一个模式牵出 4 处真缺陷；顺带清掉 154MB 孤儿产物

> 2026-09-15。起点是一件"机械活儿"——把仓库里两份陈旧的模拟器产物重导出一次
> （`docs/archive/2026-08/hmi-design-simulator-fix-plan.md` 的 T6，以及
> `docs/2026-08-19-project-audit-and-development-plan.md` 里那条一直没勾选的待办）。
> 做着做着发现：「产物陈旧」不是原因，它是被一个更硬的缺陷**反复生产**出来的。

## 1. 起点：这两份产物到底误导过什么

不是"可能误导"，是**已经误导过一次，而且留下了书面记录**。

`docs/consistency-review-2026-08-19.md:40` 当时给出的结论：

> **证据**：`sim-output/main.c` L272–276 主循环仅 `lv_timer_handler(); lv_tick_inc(5); lv_delay_ms(frame_delay);`，
> 无 `sim_refresh_bindings()` 调用。而 `sim_refresh_bindings(tick)` 由
> `CodeGenerator.GenerateSimRefreshBindings` 生成，全仓**无任何调用方**。

结论是错的。同一份文档的后续版本 `docs/2026-08-19-project-audit-and-development-plan.md:327`
自己纠正了：

> 审查需要区分"生成器源码"和"已导出的模拟器目录"……因此这里的真实问题是**模拟器导出产物没有及时再生成/未纳入一致性校验**，
> 而不是当前导出器源码永远不生成调用。

同文档 `:526` 立了一条待办，至今未勾选：

> - [ ] 每次模拟器导出都重新生成 `main.c`，禁止仓库中的 `sim-output/` 旧产物掩盖生成器行为；
>       增加"导出产物包含刷新调用"的测试。

本轮做的事，正是把这条待办落地。

## 2. 归因：旧产物"陈旧"只是症状

先用当时的历史版本核对，避免把"能力演进"误判成"缺陷"。取两份产物生成时刻的 HEAD：

| 产物 | mtime | 当时的 HEAD | `generated_hmi.c` 行数 | `Runtime tags` |
|---|---|---|---|---|
| `sim-output/` | 2026-08-13 22:12 | `fb27930` | 497 | **0** |
| `sim-output-mock/` | 2026-08-17 15:54 | `bdd459d` | 475 | **0** |
| 当前 HEAD 重新导出 | 2026-09-15 19:03 | `e5055c6` (+本轮) | **1200** | **3** |

把 `scripts/verify.sh` 的产物断言集抽出来，横向跑三份产物：

| # | 断言 | 旧 `sim-output` | 旧 `sim-output-mock` | 新导出 |
|---|---|---|---|---|
| 1–5 | 三个源文件存在 / `lv_init` / `lv_*_create` | ✅ | ✅ | ✅ |
| 6 | `Runtime tags: [1-9]` | ❌ 0 | ❌ 0 | ✅ 3 |
| 7 | `apply_theme_styles()` 调用 | ✅ | ✅ | ✅ |
| 8 | `main.c` 含 `sim_refresh_bindings` | ❌ | ❌ | ✅ |
| 9 | `main.c` 含 `alarm_engine_evaluate` | ❌ | ❌ | ✅ |
| 10 | `main.c` 含 `trend_engine_tick` | ❌ | ❌ | ✅ |
| 11 | `main.c` 的 `int hmi_screen_switch(const char* name)` | ❌ | ❌ | ✅ |
| 12 | `LV_CANVAS_BUF_SIZE` | ❌ | ❌ | ✅ |
| 13 | 不含已删除的 LVGL9 API | ✅ | ✅ | ✅ |

**7/13 通过 vs 13/13 通过。** 其中第 11 条要单独说明：`hmi_screen_switch` 在 8/13 与 8/17 的
`SimulatorProjectExporter.cs` 里**一次都不出现**（`git show <HEAD>:<file> | grep -c` = 0），
所以旧产物没有它是**当时的正常状态**，不构成缺陷。真正的缺陷是下面四条。

## 3. 逐项修复

### 3.1 `tools/MockHmiGen` 硬编码开发机家目录（2 处）

```csharp
var baseDir = args.Length > 0 ? args[0] : "/home/chen/文档";                          // L10  ← 会真的写盘
Console.WriteLine($"       \"{hmiPath}\" \"/home/chen/Git/zhishu-hmi/sim-output-mock\" \"/home/chen/Git/lvgl\"");  // L551
```

L10 不是"提示语"，是**默认输出目录**：无参运行时 `Directory.CreateDirectory(workDir)` +
`File.Create(hmiPath)` 都会落在 `/home/chen/文档` 下。后果是三条实打实的故障：

1. 在他人机器/CI 上，会凭空创建 `/home/<别人>/文档` 并往里写文件（越界写盘）；
2. 产物路径随机器漂移，导出的模拟器工程在他人机器上不可复现；
3. L551 的提示语把人指向本机绝对路径，复制粘贴即失败。

**连带证据（受害现场）**：本机 `/home/chen/文档/MockChineseProject.hmi`（15.8 MB，2026-08-17）
与 `/home/chen/文档/MockHmiAssets/`（22 MB）就是当年无参运行留下的。本次**只报告不删除**——
它在用户家目录里，不属于仓库，删不删由用户决定。

修法照 `tools/SimExport/Program.cs` 的既有范式（它早先已修，MockHmiGen 漏了）：

```csharp
// 相对路径 + GetFullPath，缺省落在当前工作目录；verify.sh 显式传临时目录
var baseDir = Path.GetFullPath(args.Length > 0 ? args[0] : "mock-hmi-out");
```

提示语改为拼真实路径 + `$LVGL_DIR`（与 SimExport 的参数/环境变量约定一致）。

**验证**（在 cwd 下无参运行）：

```
资源目录: /home/chen/.cache/zhishu-hmi-verify/mockgen-cwd/mock-hmi-out/MockHmiAssets
输出 .hmi: /home/chen/.cache/zhishu-hmi-verify/mockgen-cwd/mock-hmi-out/MockChineseProject.hmi
...
       "/home/chen/.cache/.../mock-hmi-out/MockChineseProject.hmi" ".../mock-hmi-out/sim-out" "$LVGL_DIR"
```

### 3.2 同一个模式还牵着 3 处（其中 1 处在 CI 路径上）

写守卫脚本时用**全文件类型**重扫（之前只扫了 `.cs`），多出 3 处：

| 文件 | 内容 | 性质 |
|---|---|---|
| `firmware/t113/host_test/host_main.c:30` | `#define CN_FONT_PATH "/home/chen/Git/zhishu-hmi/firmware/t113/host_test/fangsong.ttf"` | **真缺陷**：指向仓库内文件却用绝对路径；他人机器上必然加载失败，静默退化成内置位图字体（`cn_font_init` 的 fallback），host 测试就不再覆盖真实的 FreeType 渲染路径 |
| `firmware/t113/scripts/flash_tf.sh:18` | `IMG_DEFAULT="/home/chen/Tina-Linux/..."` | 本机烧录快捷脚本的默认值 |
| `firmware/t113/scripts/flash_wavepi.sh:26` | `SDK_OUT="/home/chen/Git/zhishu-hmi/firmware/Tina-SDK/out/..."` | 同上 |

修法分别为：

- `host_main.c`：路径改由**构建系统注入**，源码只留相对兜底。
  ```c
  #ifndef CN_FONT_PATH
  #define CN_FONT_PATH "fangsong.ttf"   /* 兜底：相对运行目录 */
  #endif
  ```
  Makefile：`CFLAGS += -DCN_FONT_PATH='"$(CURDIR)/fangsong.ttf"'`
  —— `$(CURDIR)` 随构建机器自动正确，且不依赖运行时 cwd。
- 两个烧录脚本：`$HOME` / 仓库相对定位 + 环境变量可覆盖。
  ```bash
  IMG_DEFAULT="${ZHISHU_TINA_IMG:-$HOME/Tina-Linux/Tina-Linux/out/t113-mq_r/tina_t113-mq_r_uart1.img}"
  SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  SDK_OUT="${ZHISHU_TINA_SDK_OUT:-$REPO_ROOT/firmware/Tina-SDK/out/t113-bingpi_m2}"
  ```

`/usr/share/fonts/noto-cjk/...`（MockHmiGen 的系统字体候选）**不报**——那是 Linux 标准位置，
不是家目录。

### 3.3 `host_test` 长期编译不过（改 3.2 时撞出来的）

按 3.2 改完 Makefile 后编译，报：

```
host_main.c:245:5: error: implicit declaration of function 'sys_settings_gate_poll';
    did you mean 'sys_settings_boot_poll'? [-Wimplicit-function-declaration]
```

核实：`sys_settings_gate_poll` **全仓只有这一处调用、没有任何定义**；板子侧
（`firmware/t113/app/main.c:296`）调用的名字是 `sys_settings_boot_poll()`。
即 host 工具停留在旧命名，**长期编译不过**——它是个手工可视化布局测试，不在 CI 里，
所以没人发现。

安全改名的依据：`sys_settings.c` 里 `static int s_boot_done = 1;  /* 初始视为已完成（未启动） */`，
而 host 侧从不调用 `sys_settings_boot_start()`，因此 `sys_settings_boot_poll()` 在 host 场景下
**恒为空操作**——改名不会改变本工具行为，只是恢复"能编译"。

改后：`make -j4` 成功产出 `hmi_settings_test`（仅剩既有的 4 条
`assignment discards 'const' qualifier` 警告，与本次无关）。

### 3.4 `scripts/verify.sh` 手工跑 selftest 缺 `SDL_VIDEODRIVER=dummy`

本机跑 E2E 时 `hmi_sim --selftest` 以「SDL 窗口创建失败」退出。分层排查：

| 观察 | 结论 |
|---|---|
| `Xvfb :0` 连接报 `Authorization required` | DISPLAY 上的 X server 需要认证 |
| `Xvfb`（自起）段错误，栈里有 `libEGL_nvidia.so.0` → `swrast_dri.so` | 本机 NVIDIA EGL 劫持 Mesa 软渲染，与项目无关 |
| `SDL_VIDEODRIVER=x11` 探针 → `SDL_Init FAILED: x11 not available` | **本机 SDL2 未编入 x11 后端** |
| `SDL_VIDEODRIVER=dummy` 探针 → `SDL_Init OK; SDL_CreateWindow OK` | 无头路径可用 |

而生成的 `CMakeLists.txt` 里 CTest 用例**已经**声明了环境变量：

```cmake
set_tests_properties(simulator_selftest PROPERTIES
    TIMEOUT 60
    ENVIRONMENT "SDL_VIDEODRIVER=dummy;SDL_AUDIODRIVER=dummy")
```

只有 `verify.sh` 里那次**手工**调用没带。补上：

```bash
SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy \
    "$REAL_SIM_DIR/build/hmi_sim" --selftest > "$REAL_SIM_DIR/selftest.log" 2>&1
```

改前退出码 1（窗口创建失败），改后：

```
[selftest] render 300 frames...
[selftest] PASS (display=0x..., active_screen=0x...)
SELFTEST_EXIT=0
```

## 4. 门禁：`scripts/check-path-hygiene.sh` + `ci-gate.sh [1c]`

规则（**判据是"路径被引号包裹"，不是"路径出现"**）：

| 项 | 内容 |
|---|---|
| 范围 | `src/` `tools/` `scripts/` `firmware/`（排除 `firmware/Tina-*` 第三方 SDK） |
| 有意排除 | `tests/` —— 夹具需要构造 `/home/user/proj` 这类**通用占位**绝对路径 |
| 模式 | `["']/(home\|Users)/[A-Za-z0-9._-]+/` |
| 理由 | 注释/文档里裸写 `/home/xxx/...` 不报（历史记录需要）；代码里写 `"/home/xxx/..."` 才报（那才会被真正使用） |

**使用契约**：在注释里举例路径时不要用 ASCII 引号包住它，否则会撞上本检查（负向验证 NV-3b 已实测）。

> **这条契约在写它的同一个文件里就被违反了一次**：`check-path-hygiene.sh` 首版的头部注释里
> 写了带 ASCII 引号的示例路径，结果接入 `ci-gate.sh` 后第一次跑门禁就被自己拦下：
>
> ```
> ❌ 产品/工具代码中出现硬编码的开发机家目录路径：
> scripts/check-path-hygiene.sh
> 21:#   ❌ 代码里写 "/home/xxx/..." —— 报（这才是会被真正使用的常量）
> ```
>
> 修法是把示例改用中文引号（不触发 ASCII 引号判据）。教训有两条：
> ①「注释里别加引号」是个**易犯**的约束，写文档/注释时容易顺手违反；
> ②**新守卫接入门禁后必须真跑一次门禁** —— 单独跑守卫脚本只能证明它对该仓库当前状态成立，
> 证明不了它在 `ci-gate.sh` 的上下文（`set -euo pipefail` + 步骤串联）里也成立。

接入 `ci-gate.sh` 作为 `[1c]`，紧跟既有的 `[1b]`（`sed -i` 改写源码检查）之后——
`[1b]` 就是"静态 grep + die"的既有先例，风格一致，且零编译开销。

```
✅ 无硬编码开发机家目录路径（已检查 558 个文件）
```

## 5. 负向验证（守卫）

| # | 注入 | 期望 | 实测 |
|---|---|---|---|
| NV-1 | `tools/SimExport/Program.cs` 加 `var nvProbe = "/home/nv/HMI";` | 恰好 1 处命中 | ✅ 命中 `Program.cs:16`，文件/行号/内容全对，exit 1 |
| NV-2 | 同处改 `"/Users/nv/HMI"` | `/Users` 分支生效（非死代码） | ✅ 命中 1 处 |
| NV-3 | 注释里裸写 `/home/chen/Git/...`（**无引号**） | **不**命中 | ✅ 不命中 |
| NV-3b | 注释里写 `"/home/user/proj"`（**带引号**） | 命中 | ✅ 命中，据此写下上面的使用契约 |

NV-1/NV-2 都采用"**保留已修基准**、只注入一处新破坏"的做法，使红集恰好为 1 条、可干净归因。

## 6. 孤儿产物目录（154 MB）

| 目录 | 体积 | git | 引用它的位置 | 处置 |
|---|---|---|---|---|
| `sim-output/` | 82 MB | 被 `.gitignore` 排除 | 无脚本消费；仅被多份文档当作"证据"引用 | **已删除** |
| `sim-output-mock/` | 72 MB | 同上 | 同上 | **已删除** |

删除前先把 17 个源文件（`main.c` / `generated_*.c` / `CMakeLists.txt` / 配置 / README，
共 34.7 KB）快照到仓库外的 `~/.cache/zhishu-hmi-verify/old-artifacts-snapshot.tar.gz`，
使第 2 节的对照表日后仍可复现；`build/`（编译中间物）与 `assets/`（测试资源）不备份 ——
两者都能由 `verify.sh` / `SimExport` 随时重新生成。

`.gitignore` 条目收敛为 `sim-output*/`（覆盖未来同族目录）+ 新增 `mock-hmi-out/`
（MockHmiGen 的新默认输出），并写明"正常验收走 `verify.sh`，它写 `$TMPDIR`，不落仓库"。

删目录的同时保住"断言集"：`scripts/verify.sh` 本就在导出后校验
`grep -q "sim_refresh_bindings" "$REAL_SIM_DIR/main.c"` 等 16 条 —— 这正是
`2026-08-19-project-audit-and-development-plan.md:526` 要求的那个测试。**仓库里不再留产物 = 没有人能再误读旧产物。**

## 7. 遗留（本轮不动，登记在此）

| 项 | 说明 |
|---|---|
| `/home/chen/文档/MockChineseProject.hmi` + `MockHmiAssets/`（38 MB） | 硬编码年代的遗物，在用户家目录，按规则只报告不处置 |
| `.dbg/`（207 MB，未跟踪） | 帧缓冲 dump（`fb0_*.raw` 各 4.7 MB）+ 构建日志，纯调试残留 |
| `tests/HmiEditor.Core.Tests/ConfigCodeGenConsumptionTests` 的 `/home/user/proj` | 夹具用绝对路径字面量，SUT 是纯字符串拼接 ⇒ 在 Windows 上断言会挂（平台耦合，非本轮范围） |
| `host_main.c` 的 4 条 `discards 'const' qualifier` 警告 | 既有，无害 |
| Dock.Avalonia 面板空白 GUI 冒烟；AI 助手 SSE 流式/取消 + `hmi_actions.c` 权限校验 | 更远的未开工项 |

## 8. 验证清单

| 验证 | 结果 |
|---|---|
| `bash scripts/check-path-hygiene.sh` | ✅ 0 命中（558 文件） |
| 守卫负向验证 NV-1/2/3/3b | ✅ 4/4 符合预期 |
| `bash -n` 全部改动脚本 | ✅ |
| `dotnet build tools/MockHmiGen` | ✅ 0 警告 0 错误 |
| MockHmiGen 无参运行落 `cwd/mock-hmi-out` | ✅ 提示语给真实路径 + `$LVGL_DIR` |
| `make -j4`（`firmware/t113/host_test`） | ✅ 产出 `hmi_settings_test`，无 error |
| `bash firmware/t113/tests/run_host_tests.sh` | ✅ 18 通过 0 失败 / 9 通过 0 失败 |
| `scripts/verify.sh` 端到端（含真 LVGL 9.5 编译） | ✅ 全量测试 **2136 通过 / 0 失败**；Golden 工程 2 屏 36 控件；cmake 编译成功 + `--selftest` PASS + `ctest` 100% |
| `sim-output/`、`sim-output-mock/` 已移除 | ✅ 回收 **154 MB**；源码快照留在仓库外（34.7 KB / 17 文件） |

`verify.sh` 末尾输出：

```
Golden project model: 2 screens, 36 controls
Simulator CMake build, selftest and CTest: passed (.../real-simulator/build/hmi_sim)
LVGL: /home/chen/Git/lvgl
Verification passed (managed code and tools).
```
