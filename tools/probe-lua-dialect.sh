#!/usr/bin/env bash
# 跑 Lua 方言探针（tools/probe-lua-dialect.lua）。
#
# 刻意**不**进 ci-gate：它需要一个"设备侧 Lua"的解释器，而那个东西在 CI 环境里不存在
# （Tina SDK 的 out/ 是构建产物、不在版本库里）。让它进门槛只会逼出一个跳过分支，
# 那种判据等于没有。⇒ 手工探针，但**命令可复现**。
#
# 用法：
#   tools/probe-lua-dialect.sh                 # 自动找 Tina SDK 自带的 host lua
#   tools/probe-lua-dialect.sh /path/to/lua    # 显式指定（换目标 Lua 版本时用这个）
#
# 退出码：0 = 跑完；1 = 找不到解释器；2 = 探针自检失败（见 .lua 里的正控）。
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROBE="$REPO_ROOT/tools/probe-lua-dialect.lua"

CANDIDATES=(
  "$REPO_ROOT/firmware/Tina-SDK/out/t113-mq_r/compile_dir/target/host/lua-5.1.5/src/lua"
  "$REPO_ROOT/firmware/Tina-SDK/out/host/bin/lua"
  "$REPO_ROOT/firmware/Tina-Linux/out/t113-mq_r/compile_dir/target/host/lua-5.1.5/src/lua"
)

LUA="${1:-}"
if [ -z "$LUA" ]; then
  for c in "${CANDIDATES[@]}"; do
    if [ -x "$c" ]; then LUA="$c"; break; fi
  done
fi

if [ -z "$LUA" ] || [ ! -x "$LUA" ]; then
  echo "✗ 找不到可执行的 Lua 解释器。" >&2
  echo "  自动找过这几个位置（Tina SDK 的构建产物，可能未构建）：" >&2
  for c in "${CANDIDATES[@]}"; do echo "    $c" >&2; done
  echo "  ⇒ 请显式指定一个「与设备侧同版本」的解释器，例如：" >&2
  echo "     tools/probe-lua-dialect.sh /usr/bin/lua5.1" >&2
  echo "  ⚠ 不要随便拿系统自带的 lua（很可能是 5.4，测出来的不是设备的事实）。" >&2
  exit 1
fi

echo "解释器：$LUA"
"$LUA" -v 2>&1 || true
echo "──────────────────────────────────────────────"
"$LUA" "$PROBE"
rc=$?
echo "──────────────────────────────────────────────"
echo "PROBE_EXIT=$rc"
exit $rc
