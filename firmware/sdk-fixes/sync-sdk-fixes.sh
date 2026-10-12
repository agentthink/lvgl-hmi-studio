#!/bin/bash
# ============================================================================
# sync-sdk-fixes.sh — 将仓库维护的 SDK 修复文件同步到本地 Tina SDK 环境
#
# 背景:
#   firmware/Tina-Linux/ 下部分路径（如 build/、out/）被 firmware/.gitignore
#   忽略，无法通过 git 入库。为避免修复丢失，把修复后的文件副本保存在本目录
#   （firmware/sdk-fixes/，可被 git 跟踪），本脚本按清单同步到 SDK 对应路径。
#
# 用法:
#   ./firmware/sdk-fixes/sync-sdk-fixes.sh
#
# 说明:
#   1) FILES 数组每行为 "SDK相对路径|本目录文件名"，新增修复时追加一行即可
#   2) 同步前自动备份 SDK 原有文件（首次不同步时）
#   3) 同步后逐一校验一致性
# ============================================================================
set -euo pipefail

# 自动定位 SDK 目录（本脚本位于 firmware/sdk-fixes/，SDK 在 firmware/Tina-Linux/）
FIX_DIR="$(cd "$(dirname "$0")" && pwd)"
SDK_DIR="$(cd "$FIX_DIR/../Tina-Linux" && pwd)"

[ -d "$SDK_DIR" ] || { echo "ERROR: SDK 目录不存在: $SDK_DIR"; exit 1; }

# 修复文件清单: "SDK相对路径|本目录文件名"
#  - build/kernel-defaults.mk: 内核构建收敛（Configure/Default 与 SetInitramfs
#    注入后各执行一次 olddefconfig，避免 Kconfig NEW 项导致 syncconfig 交互卡死）
FILES=(
  "build/kernel-defaults.mk|kernel-defaults.mk"
)

echo "SDK 目录: $SDK_DIR"
echo "修复源目录: $FIX_DIR"
echo "=============================================="

synced=0
for entry in "${FILES[@]}"; do
  sdk_rel="${entry%%|*}"
  fix_file="${entry#*|}"
  src="$FIX_DIR/$fix_file"
  dst="$SDK_DIR/$sdk_rel"

  if [ ! -f "$src" ]; then
    echo "SKIP: 缺少修复源文件 $src"
    continue
  fi

  mkdir -p "$(dirname "$dst")"

  # 首次同步/内容不同时备份 SDK 原文件
  if [ -f "$dst" ] && ! cmp -s "$src" "$dst"; then
    bak="$dst.sdk.bak.$(date +%Y%m%d%H%M%S)"
    cp -f "$dst" "$bak"
    echo "BACKUP: $sdk_rel -> $bak"
  fi

  cp -f "$src" "$dst"
  echo "SYNC:   $sdk_rel"
  synced=$((synced + 1))
done

echo "=============================================="
echo "--- 一致性校验 ---"
failed=0
for entry in "${FILES[@]}"; do
  sdk_rel="${entry%%|*}"
  fix_file="${entry#*|}"
  if cmp -s "$FIX_DIR/$fix_file" "$SDK_DIR/$sdk_rel"; then
    echo "OK:     $sdk_rel"
  else
    echo "FAIL:   $sdk_rel（与修复源不一致）"
    failed=$((failed + 1))
  fi
done

echo "=============================================="
echo "完成: 同步 $synced 个文件, 校验失败 $failed"
[ "$failed" -eq 0 ] || exit 1
