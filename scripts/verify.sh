#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

# ── Configuration 与 scripts/ci-gate.sh 对齐 ──
#
# 默认 Release，理由有两条，都是实测出来的：
#   1. 同一个仓库的两套门禁跑不同 Configuration，结论不可比；
#   2. **Debug 全量并发下 `HmiEditor.App.UiTests` 的 testhost 会崩**（2026-09-17 实测，
#      VSTest blame 报「测试主机进程崩溃」，崩时 9 个不同测试类同时处于运行中，
#      无托管异常、无堆栈 ⇒ 原生崩溃或栈溢出）。同一个项目单独跑
#      **142 项 / 12.5 s 全绿**，走 slnx 只跑它 24 s 全绿 ⇒ 与"并行跑大队"强相关，
#      Release 下（ci-gate.sh）连跑两轮全绿。
#      ⇒ 端到端验收跑不通的直接原因是这个崩溃，不是生成链。
# 需要复现 Debug 行为时：CONFIG=Debug bash scripts/verify.sh
CONFIG="${CONFIG:-Release}"

# 与 ci-gate.sh 同款挂死防护：某个用例**静默**超时即中止并点名（而不是无限等）。
# 阶段 120：verify.sh 此前既没有 --blame-hang，也**没有把
# `DockPanelInvocationUiTests` 从大队里排掉** —— 而 ci-gate.sh 记录了该类在同进程
# 大队里会让 testhost 僵死。两份门禁的隔离策略必须一致，否则"端到端验收会挂"
# 而"快速门禁过"这种最难查的分歧就会出现。
TEST_HANG_TIMEOUT="${TEST_HANG_TIMEOUT:-180s}"
BLAME_HANG=(--blame-hang --blame-hang-timeout "$TEST_HANG_TIMEOUT" --blame-hang-dump-type none)

echo "[1/7] Restoring solution and validation tools ($CONFIG)..."
dotnet restore HmiEditor.slnx --nologo
dotnet restore tools/MockHmiGen/MockHmiGen.csproj --nologo
dotnet restore tools/SimExport/SimExport.csproj --nologo

echo "[2/7] Building solution..."
dotnet build HmiEditor.slnx -c "$CONFIG" --no-restore --nologo

# ── 第 3、4 步拆两段：与 scripts/ci-gate.sh 第 [3] 步同一原因 ──
#
# 本沙箱对「首次出现的**文件**路径」收约 60 ms，且该开销**全局串行**（8 进程并行
# ≈ 8 × 单进程，20 核闲置）。大队会把这条队列灌满，令 `git init` 的子进程从 1 ms
# 涨到 4~10 s，撞穿 `GitWorkflowService.GitProcessWaitMs`(15 s) ⇒
# `InitializeRepository` 返回 false ⇒ `InitializeRepository_SecondCall_Idempotent` 红。
#
# 2026-09-17 未拆时实测：本步骤红在 `InitializeRepository_SecondCall_Idempotent`，
# 该用例耗时 1 m 31 s（≈ 六次撞穿窗口）。完整归因与「为什么调窗口/调并行度都不是
# 解药」见 docs/2026-09-17-sandbox-newpath-serialization.md。
# 拆开后两段之和 = 全量，**一条判据都没放宽**：用例照跑、断言照旧，只是不再与大队
# 争那条串行队列。
echo "[3/7] Running tests (bulk, excluding cases that spawn git / open 12 dock panels)..."
dotnet test HmiEditor.slnx -c "$CONFIG" --no-build --no-restore --nologo \
    --logger "console;verbosity=minimal" \
    "${BLAME_HANG[@]}" \
    --filter 'FullyQualifiedName!~GitWorkflowServiceTests&FullyQualifiedName!~DockPanelInvocationUiTests'

echo "[4/7] Running isolation-sensitive tests in isolation (empty queue)..."
dotnet test tests/HmiEditor.Core.Tests/HmiEditor.Core.Tests.csproj -c "$CONFIG" \
    --no-build --no-restore --nologo \
    --logger "console;verbosity=minimal" \
    "${BLAME_HANG[@]}" \
    --filter 'FullyQualifiedName~GitWorkflowServiceTests'

# 停靠面板用例：与 ci-gate.sh 第 [6] 步同因（同进程大队里 testhost 僵死，登记 #45）
dotnet test tests/HmiEditor.App.UiTests/HmiEditor.App.UiTests.csproj -c "$CONFIG" \
    --no-build --no-restore --nologo \
    --logger "console;verbosity=minimal" \
    "${BLAME_HANG[@]}" \
    --filter 'FullyQualifiedName~DockPanelInvocationUiTests'

echo "[5/7] Building validation tools..."
dotnet build tools/MockHmiGen/MockHmiGen.csproj -c "$CONFIG" --no-restore --nologo
dotnet build tools/SimExport/SimExport.csproj -c "$CONFIG" --no-restore --nologo

echo "[6/7] Generating Golden Project fixture..."
TMP_ROOT="${TMPDIR:-/tmp}"
# 早失败：Golden 工程与 LVGL 全量编译产物（数百 MB）都落在临时目录下。
# 若它是容量很小的 tmpfs（某些发行版的 /tmp 只有 10 MB），脚本会在编译到一半时
# 以 ENOSPC 失败，报错点看上去与真实原因无关。这里提前给出可执行的指引。
AVAIL_KB="$(df -Pk "$TMP_ROOT" 2>/dev/null | awk 'NR==2 {print $4}')"
if [ "${AVAIL_KB:-0}" -lt 1048576 ]; then
  echo "ERROR: 临时目录 $TMP_ROOT 可用空间不足 1 GB（当前约 ${AVAIL_KB:-0} KB）。"
  echo "       LVGL 全量编译需要数百 MB；请改用有空间的目录："
  echo "       TMPDIR=\$HOME/.cache/zhishu-hmi-verify LVGL_DIR=<lvgl 源码> bash scripts/verify.sh"
  exit 1
fi
GOLDEN_DIR="$TMP_ROOT/zhishu-hmi-golden"
rm -rf "$GOLDEN_DIR"
dotnet run --project tools/MockHmiGen/MockHmiGen.csproj -c "$CONFIG" --no-build --no-restore -- "$GOLDEN_DIR" >"$TMP_ROOT/zhishu-hmi-golden.log"
test -s "$GOLDEN_DIR/MockChineseProject.hmi"
test -s "$GOLDEN_DIR/MockHmiAssets/NotoSansCJK_Regular.ttc"
if command -v unzip >/dev/null 2>&1; then
  unzip -t "$GOLDEN_DIR/MockChineseProject.hmi" >/dev/null
  GOLDEN_LIST="$(unzip -Z1 "$GOLDEN_DIR/MockChineseProject.hmi")"
  for required in project.json manifest.json assets.json; do
    grep -qx "$required" <<< "$GOLDEN_LIST" || { echo "ERROR: Golden package missing $required"; exit 1; }
  done
  if command -v jq >/dev/null 2>&1; then
    unzip -p "$GOLDEN_DIR/MockChineseProject.hmi" project.json > "$GOLDEN_DIR/project.json"
    SCREEN_COUNT="$(jq ".screens | length" "$GOLDEN_DIR/project.json")"
    CONTROL_COUNT="$(jq "[.screens[] | (.controls // []) | length] | add" "$GOLDEN_DIR/project.json")"
    test "$SCREEN_COUNT" -ge 2
    test "$CONTROL_COUNT" -ge 28
    echo "Golden project model: $SCREEN_COUNT screens, $CONTROL_COUNT controls"
  else
    echo "NOTICE: jq not installed; model count checks skipped."
  fi
  echo "Golden package archive: valid"
else
  echo "NOTICE: unzip not installed; package archive inspection skipped."
fi
echo "Golden Project generated: $GOLDEN_DIR"

echo "[7/7] Exporting and testing Golden simulator sources..."
SIM_DIR="$GOLDEN_DIR/simulator"
FAKE_LVGL="$GOLDEN_DIR/fake-lvgl"
mkdir -p "$FAKE_LVGL"
printf "/* compile-only probe; real LVGL required for C build */\n" > "$FAKE_LVGL/lvgl.h"
set +e
dotnet run --project tools/SimExport/SimExport.csproj -c "$CONFIG" --no-build --no-restore -- "$GOLDEN_DIR/MockChineseProject.hmi" "$SIM_DIR" "$FAKE_LVGL" >"$TMP_ROOT/zhishu-hmi-sim-export.log" 2>&1
SIM_EXIT=$?
set -e
test -s "$SIM_DIR/generated_hmi.c"
test -s "$SIM_DIR/main.c"
test -s "$SIM_DIR/CMakeLists.txt"
# 生成产物清单：权威来源是 tests/HmiEditor.Core.Tests/SimulatorLoopTests.cs 中那条
# 断言导出文件集的用例。这里只显式断言**三条独立的下沉链**（权限 / 通讯 / 运行时参数）
# 与 sim_config.h —— 它们缺失时 C 编译只会报「找不到头文件」，在这里断言能把失败点
# 直接指到缺的是哪条链。（generated_fonts.c / generated_images.c 由 CMakeLists 编译，
# 缺失即编译失败，不重复断言。）
for required in generated_security.h generated_comm.h generated_runtime.h sim_config.h; do
  test -s "$SIM_DIR/$required" || { echo "ERROR: 生成产物缺少 $required"; exit 1; }
done
grep -q "lv_init" "$SIM_DIR/main.c"
grep -Eq "lv_[a-z_]+_create" "$SIM_DIR/generated_hmi.c"
grep -q "create_screen_screen001" "$SIM_DIR/generated_hmi.c"
grep -q "create_screen_screen002" "$SIM_DIR/generated_hmi.c"
grep -q "lv_slider_create" "$SIM_DIR/generated_hmi.c"
grep -q "lv_bar_create" "$SIM_DIR/generated_hmi.c"
grep -q "lv_table_create" "$SIM_DIR/generated_hmi.c"
grep -q "LVGL" "$SIM_DIR/generated_hmi.c"
# Golden 运行时动作/标签校验：防止导出后退化为静态画面。
grep -q "set_tag_value(TAG_v_golden_temp" "$SIM_DIR/generated_hmi.c"
grep -q "hmi_screen_switch" "$SIM_DIR/generated_hmi.c"
# 至少要有 1 个运行时标签（不锁死具体数量，避免夹具扩充即失败）
grep -Eq "Runtime tags: [1-9][0-9]*" "$SIM_DIR/generated_hmi.c"
# canvas 类控件回归：不得再出现 LVGL 9.x 中不存在的 API，且缓冲必须按字节分配
if grep -q "lv_canvas_set_px_opa\|lv_draw_polygon" "$SIM_DIR/generated_hmi.c"; then
  echo "ERROR: 生成代码包含 LVGL 9.x 不存在的 canvas/draw API"; exit 1
fi
grep -q "LV_CANVAS_BUF_SIZE" "$SIM_DIR/generated_hmi.c"
if [ "$SIM_EXIT" -eq 0 ]; then
  echo "Simulator export/build: passed"
else
  echo "Simulator source export: passed; C build deferred (exit $SIM_EXIT)"
fi

if command -v cmake >/dev/null 2>&1; then
  echo "CMake: $(cmake --version | head -1)"
  if [ -n "${LVGL_DIR:-}" ] && [ -f "$LVGL_DIR/lvgl.h" ]; then
    REAL_SIM_DIR="$GOLDEN_DIR/real-simulator"
    rm -rf "$REAL_SIM_DIR"
    dotnet run --project tools/SimExport/SimExport.csproj -c "$CONFIG" --no-build --no-restore -- "$GOLDEN_DIR/MockChineseProject.hmi" "$REAL_SIM_DIR" "$LVGL_DIR" >"$TMP_ROOT/zhishu-hmi-real-sim.log" 2>&1
    test -x "$REAL_SIM_DIR/build/hmi_sim"
    # 无头运行：selftest 只渲染固定帧数并校验显示/活动屏幕，不需要真实 X server。
    # CTest 用例（simulator_selftest）已声明 SDL_VIDEODRIVER=dummy；这里的手工调用
    # 必须显式带上，否则在无 X11、或本机 SDL2 未编入 x11 后端的机器上会以
    # 「SDL 窗口创建失败」退出（SDL_Init 失败时 lv_sdl_window_create 返回 NULL）。
    SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy \
        "$REAL_SIM_DIR/build/hmi_sim" --selftest > "$REAL_SIM_DIR/selftest.log" 2>&1
    grep -q "PASS" "$REAL_SIM_DIR/selftest.log"
    grep -q "sim_refresh_bindings" "$REAL_SIM_DIR/main.c"
    grep -q "alarm_engine_evaluate" "$REAL_SIM_DIR/main.c"
    grep -q "trend_engine_tick" "$REAL_SIM_DIR/main.c"
    grep -qF "int hmi_screen_switch(const char* name)" "$REAL_SIM_DIR/main.c"
    (cd "$REAL_SIM_DIR/build" && ctest --output-on-failure)
    echo "Simulator CMake build, selftest and CTest: passed ($REAL_SIM_DIR/build/hmi_sim)"
  fi
else
  echo "NOTICE: cmake not installed; C simulator compilation remains pending."
fi
if [ -n "${LVGL_DIR:-}" ] && [ -f "$LVGL_DIR/lvgl.h" ]; then
  echo "LVGL: $LVGL_DIR"
else
  echo "NOTICE: LVGL_DIR is not configured; C simulator compilation remains pending."
fi
echo "Verification passed (managed code and tools)."
