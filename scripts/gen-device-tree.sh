#!/bin/bash
# gen-device-tree.sh — 从仓内示例工程定义生成设备侧产物（阶段 319；§12 第 6 项 ③）
#
# 用户拍板：**构建前在开发机生成、构建机只消费**（设备构建机上没有 dotnet）。
# 于是 ③ 的形状是：
#   · 生成器 = tools/DeviceExport（.NET，跑在开发机）；
#   · 产物落 firmware/t113/generated/，其中 screens.c 等**不入库**，靠本脚本现场生成；
#   · 构建机（Tina SDK）只消费目录里已有的产物 —— 不引入任何 dotnet 依赖。
#
# 用法：
#   bash scripts/gen-device-tree.sh --check    # 只校验，不改（给门禁用：与现场生成逐字节比）
#   bash scripts/gen-device-tree.sh --write    # 写入设备树（开发机出包/构建前用）
#
# 退出码：0 = 一致/已写入；1 = 有漂移或缺文件；2 = 用法错
set -u
cd "$(dirname "$0")/.." || exit 2

MODE="${1:---check}"
case "$MODE" in
    --check|--write) ;;
    *) echo "用法: bash scripts/gen-device-tree.sh [--check|--write]"; exit 2 ;;
esac

DEF=firmware/t113/sample/MockChineseProject
OUT=firmware/t113/generated
# 与设备树里"由生成器产出"的那几类一致（assets/ 是工程资产，由示例工程定义管，不在这里比）
FILES="screens.c tags.h tags.c generated_security.h"

WORK=$(mktemp -d /tmp/hmi_gen_XXXX)
trap 'rm -rf "$WORK"' EXIT

if ! dotnet run --project tools/DeviceExport/DeviceExport.csproj -c Release -- \
        "$DEF/project.json" "$WORK" >"$WORK/gen.log" 2>&1; then
    echo "❌ 生成失败（见下）"; tail -20 "$WORK/gen.log"; exit 1
fi

rc=0
for f in $FILES; do
    if [ ! -f "$OUT/$f" ]; then
        echo "  ❌ 缺 $OUT/$f（未生成过？跑 --write）"; rc=1; continue
    fi
    if cmp -s "$WORK/$f" "$OUT/$f"; then
        echo "  ✅ $f 与现场生成逐字节一致"
    else
        echo "  ❌ $f 与现场生成不一致（漂移：跑 --write 更新，或查生成器/示例工程的改动）"; rc=1
    fi
done

if [ "$MODE" = "--write" ]; then
    for f in $FILES; do cp "$WORK/$f" "$OUT/$f"; done
    echo "  已写入 $OUT/（$FILES）"
    rc=0
fi
exit $rc
