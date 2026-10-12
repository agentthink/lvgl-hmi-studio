#!/bin/bash
# check-cross-proxy.sh — 用**设备工具链 + 设备侧 OpenSSL**交叉编译 TLS 终止代理（阶段 304）
#
# 为什么单列一条、**不进 ci-gate**：Tina SDK 在机器上、不在仓库里；门禁必须在**没有 SDK** 的机器上
# 也能全绿。所以这里用三态退出码（与 tls_proxy 的 0/3/4 同款思路）：
#     0 通过 · 3 **跳过**（没找到 SDK —— 不是失败，但也**不是**"查过了"）· 1 失败
#
# 它补的是哪一层：主机侧（`check_tls_proxy.sh`，第 11 步）编的是 OpenSSL **3.6.5**，
# 而设备镜像里是 **1.1.0i**。API 落差只有用真工具链 + 真头文件才能暴露。
#
# 判据：
#   ① 设备工具链能编过（对 1.1.0i 的头文件）        ② 0 警告
#   ③ 产物是 ARM/musl ELF，NEEDED 只有镜像里有的那两个 OpenSSL .so
#   ④ app/Makefile 的 `hmi-tls-proxy` 目标存在（`make -n` 干跑）
#   ⑤ 顺带报尺寸：代理剥离后大小 ＋ 镜像里 libssl/libcrypto（评估 §6-5 的"尺寸"半边）
set -u

cd "$(dirname "$0")/.." || exit 1

SDK=""
for c in "${TINA_SDK:-}" /home/chen/Tina-Linux/Tina-Linux /home/chen/Tina-Linux; do
    if [ -n "$c" ] && [ -d "$c/out/t113-mq_r/staging_dir" ]; then SDK="$c"; break; fi
done
if [ -z "$SDK" ]; then
    echo "跳过：没找到 Tina SDK（设 TINA_SDK，或放在 /home/chen/Tina-Linux）—— 这条**没查**"
    exit 3
fi

ST="$SDK/out/t113-mq_r/staging_dir"
TC="$ST/toolchain/bin/arm-openwrt-linux-muslgnueabi"
export STAGING_DIR="$ST"

PASS=0; FAIL=0
ok()  { PASS=$((PASS + 1)); echo "  PASS  $1"; }
bad() { FAIL=$((FAIL + 1)); echo "  FAIL  $1"; }

WORK=$(mktemp -d /tmp/hmi_cross_proxy_XXXX)
trap 'rm -rf "$WORK"' EXIT

echo "SDK=$SDK"
echo "工具链=$TC-gcc"
# ⚠ 只从 OPENSSL_VERSION_TEXT 那一行抓：文件头注释里也有 "OpenSSL Project"，
#   不限定行的话第一轮读数就是这个（假读数）。
ver=$(grep 'OPENSSL_VERSION_TEXT' "$ST/target/usr/include/openssl/opensslv.h" \
      | grep -o 'OpenSSL [0-9][0-9a-zA-Z.\-]*' | head -1)
echo "设备侧 OpenSSL=$ver"

# ① / ② 编译（工作目录在临时目录里，**不往仓库里落 .o**）
"$TC-gcc" -std=c99 -D_GNU_SOURCE -Wall -Wextra \
    -Ifirmware/t113/app/runtime -Ifirmware/t113/app/comm -I"$ST/target/usr/include" \
    firmware/t113/app/proxy/tls_proxy.c \
    firmware/t113/app/comm/tls_proxy_config.c \
    firmware/t113/app/comm/mqtt_tls_contract.c \
    -L"$ST/target/usr/lib" -lssl -lcrypto -lpthread \
    -o "$WORK/hmi-tls-proxy" 2> "$WORK/build.log"
rc=$?
if [ $rc -ne 0 ]; then
    bad "交叉编译失败"; sed 's/^/        /' "$WORK/build.log"
    echo "=== 结果: $PASS 通过, $FAIL 失败 ==="; exit 1
fi
ok "交叉编译通过（对设备侧 $ver 的头文件）"
if [ -s "$WORK/build.log" ]; then
    bad "交叉编译有诊断输出（警告也算）"; sed 's/^/        /' "$WORK/build.log"
else
    ok "交叉编译 0 警告"
fi

# ③ 产物形态与依赖
file "$WORK/hmi-tls-proxy" | grep -q "ARM" && ok "产物是 ARM ELF" || bad "产物不是 ARM ELF"
"$TC-readelf" -d "$WORK/hmi-tls-proxy" | grep -q 'libssl.so.1.1' && ok "NEEDED 含 libssl.so.1.1" \
    || bad "NEEDED 不含 libssl.so.1.1"
"$TC-readelf" -d "$WORK/hmi-tls-proxy" | grep -q 'libcrypto.so.1.1' && ok "NEEDED 含 libcrypto.so.1.1" \
    || bad "NEEDED 不含 libcrypto.so.1.1"

# ④ app/Makefile 的目标存在（干跑，不落产物）
if make -C firmware/t113/app -n hmi-tls-proxy >/dev/null 2>&1; then
    ok "app/Makefile 的 hmi-tls-proxy 目标存在"
else
    bad "app/Makefile 里没有可用的 hmi-tls-proxy 目标"
fi

# ⑤ 尺寸读数（§6-5 的"尺寸"半边；"内存"半边要真板）
"$TC-strip" "$WORK/hmi-tls-proxy" 2>/dev/null
stripped=$(stat -c%s "$WORK/hmi-tls-proxy" 2>/dev/null)
libssl=$(stat -c%s "$ST/target/usr/lib/libssl.so.1.1" 2>/dev/null)
libcrypto=$(stat -c%s "$ST/target/usr/lib/libcrypto.so.1.1" 2>/dev/null)
echo "  ----  代理（剥离后）: ${stripped:-?} B"
echo "  ----  镜像 libssl.so.1.1: ${libssl:-?} B / libcrypto.so.1.1: ${libcrypto:-?} B"
[ -n "$libssl" ] && [ -n "$libcrypto" ] && ok "镜像里有 libssl/libcrypto（依赖能被解析）" \
    || bad "镜像里找不到 libssl/libcrypto"

echo "=== 结果: $PASS 通过, $FAIL 失败 ==="
[ "$FAIL" -eq 0 ]
