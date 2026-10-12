#!/usr/bin/env bash
# ============================================================
# resource-budget.sh —— 资源预算「实测 vs 预算」（登记表 §4 第 29 项）
#
# 真实逻辑在 tools/BudgetCheck（C#，判读规则在 Hmi.Hardware.Contracts/ResourceBudget）：
#   ① 实测整包镜像  vs 目标声明的 FlashSizeBytes；
#   ② 分区表推出的 UDISK 剩余  vs 待部署 UI 产物（--so / --assets）。
#
# 需要本机有 SDK 构建产物；没有产物时工具会打印"未实测"并以 0 退出
# （有实测轴超预算才退出 1）—— CI 上没有几十 GB 的 SDK，本脚本是给有产物的机器用的。
#
# 用法：
#   bash scripts/resource-budget.sh [--target allwinner-t113]
#        [--image <tina_*.img>] [--partition <sys_partition.fex]
#        [--so <libhmi_ui.so>] [--assets <设备资源目录>]
# ============================================================
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

CONFIG="${CONFIG:-Release}"
exec dotnet run --project tools/BudgetCheck/BudgetCheck.csproj -c "$CONFIG" -- "$@"
