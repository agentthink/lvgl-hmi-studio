#!/usr/bin/env bash
# ============================================================
# T113 Tina-Linux SDK 构建/打包脚本
# 用法（在 SDK 内，TINA_ROOT 指向 Tina-Linux 源码根目录）：
#   ./build.sh config <TINA_ROOT> [BOARD]  打开软件包配置（勾选 hmi-runtime）
#   ./build.sh pkg   <TINA_ROOT> [BOARD]   mm 单编 hmi-runtime 软件包
#   ./build.sh all   <TINA_ROOT> [BOARD]   整编 + pack 打包固件
#   ./build.sh pack  <TINA_ROOT> [BOARD]   仅打包固件
# BOARD：mq_r（默认）=t113-mq_r 板（UART1 调试/UART3 RS485/硬件 RS485）；
#        bingpi_m2 = WavePI-S6 7 寸板（UART3 调试/UART4 RS485/GPIO 方向控制）
#   也可用环境变量：BOARD=bingpi_m2 ./build.sh all <TINA_ROOT>
# 产物：
#   <TINA_ROOT>/out/t113-mq_r/tina_t113-mq_r_uart1.img     （BOARD=mq_r）
#   <TINA_ROOT>/out/t113-bingpi_m2/tina_t113-bingpi_m2_uart3.img （BOARD=bingpi_m2）
# 烧录见 docs/flashing-guide.md（PhoenixSuit / PhoenixCard）。
# ============================================================
set -e

CMD="$1"
TINA="${2:-$(pwd)}"
BOARD="${BOARD:-${3:-mq_r}}"        # 环境变量优先，其次 $3，默认 mq_r

[ -d "$TINA/package" ] || {
    echo "error: $TINA 不是 Tina-Linux SDK 根目录（缺少 package/）"
    exit 1
}

# 归一化为绝对路径：后续 `cd "$TINA"` 后再引用 $TINA 的相对路径会失效
# （相对路径下 `cd "$TINA/package/hmi-runtime"` 会静默失败并跳过 mm 编译）。
TINA="$(cd "$TINA" && pwd)"

# ── 板级参数（按 BOARD 切换） ──
# 与板强相关的路径/名称集中在此，其它函数一律引用这些变量：
#   LUNCH_NAME       lunch 目标名（t113_<board>-tina）
#   BOARD_DIR        SDK 板配置目录名（device/config/chips/t113/configs/<dir>）
#   IMG_NAME         最终镜像名（含 uart 号）
#   OUT_DIR          SDK out/<dir>
#   WAVEPI           应用层 HMI_BOARD_WAVEPI 宏（1=WavePI，0=mq_r）
case "$BOARD" in
mq_r)
    LUNCH_NAME="t113_mq_r-tina"
    BOARD_DIR="mq_r"
    IMG_NAME="tina_t113-mq_r_uart1.img"
    OUT_DIR="t113-mq_r"
    WAVEPI=0
    ;;
bingpi_m2)
    LUNCH_NAME="t113_bingpi_m2-tina"
    BOARD_DIR="bingpi_m2"
    IMG_NAME="tina_t113-bingpi_m2_uart3.img"
    OUT_DIR="t113-bingpi_m2"
    WAVEPI=1
    ;;
*)
    echo "error: 未知 BOARD=$BOARD（支持 mq_r / bingpi_m2）"
    exit 1
    ;;
esac

echo "board: $BOARD  (lunch=$LUNCH_NAME, dts=$BOARD_DIR, img=$IMG_NAME, wavepi=$WAVEPI)"

# ── 固件板型宏切换（应用层 HMI_BOARD_WAVEPI） ──
# 需保持一致的文件：
#   app: firmware/t113/app/runtime/runtime_config.h（HMI_BOARD_WAVEPI 决定串口/RS485 方向）
#   SDK dts 由 BoardConfig/lunch 选择，本脚本不修改 dts 内容（bingpi_m2 出厂即 1024×600
#   启用块 + UART3 调试/UART4 RS485，见 docs/wavepi-s6-migration-plan.md §3.2）。
set_model() {
    local fw="$(cd "$(dirname "$0")/.." && pwd)"   # firmware/t113

    # 板型宏改为「编译期注入」：导出给 hmi-runtime/Makefile，
    # 由其以 -DHMI_BOARD_WAVEPI=$(HMI_BOARD_WAVEPI) 传给编译器。
    #
    # 这里此前用 `sed -i` 改写被 git 跟踪的 runtime_config.h：
    #   1) 每次构建都污染工作区（git status 出现脏文件，易被误提交）；
    #   2) 切板型后源码与产物不一致，构建不可复现；
    #   3) 板级差异被"隐式配置"藏起来，读代码看不出当前是哪块板。
    export HMI_BOARD_WAVEPI="$WAVEPI"

    echo "model: HMI_BOARD_WAVEPI=$WAVEPI (board=$BOARD, 编译期注入 -D)"

    # 仅作漂移提示：board_profile.h 的默认值应与本次构建板型一致
    # （-D 会覆盖它，但默认值漂移说明有人改错了地方）
    local header_default
    header_default="$(sed -n 's/^#define HMI_BOARD_WAVEPI[[:space:]]*\([01]\).*/\1/p' \
        "$fw/app/runtime/board_profile.h" | head -1)"
    if [ -z "$header_default" ]; then
        echo "warning: board_profile.h 未找到 HMI_BOARD_WAVEPI 默认值"
    elif [ "$header_default" != "$WAVEPI" ]; then
        echo "info: board_profile.h 默认 HMI_BOARD_WAVEPI=$header_default，本次 -D 注入 $WAVEPI（已覆盖）"
    fi
}

# 部署 hmi-runtime 软件包到 SDK
deploy_package() {
    local dst="$TINA/package/hmi-runtime"
    local fw="$(cd "$(dirname "$0")/.." && pwd)"   # firmware/t113
    mkdir -p "$dst"
    # 包文件（Makefile + init + conf）
    cp -r "$fw/tina/package/hmi-runtime/." "$dst/"
    # 应用/配置/生成代码（供 Build/Prepare 使用）；先清空防旧文件残留
    rm -rf "$dst/src" "$dst/src-config" "$dst/src-generated"
    mkdir -p "$dst/src" "$dst/src-config" "$dst/src-generated"
    cp -r "$fw/app/." "$dst/src/"
    cp -r "$fw/config/." "$dst/src-config/"

    # ── 设备树的**前置检查**（阶段 319；③：开发机生成、构建机只消费）────────────
    # screens.c/tags.* 是**生成物**，按拍板**不入库** ⇒ 构建机（Tina SDK，没有 dotnet）
    # 只消费目录里已有的内容。缺产物时必须在**这里**就报清楚，而不是等交叉编译报
    # "找不到 create_screen_*" 那种要往回猜的错。
    local missing=""
    for f in screens.c tags.h tags.c generated_security.h; do
        [ -s "$fw/generated/$f" ] || missing="$missing $f"
    done
    if [ -n "$missing" ]; then
        echo "✗ 设备树缺生成物：$missing" >&2
        echo "  设备树的 screens.c/tags.* 由生成器产出、**不入库**。" >&2
        echo "  在**开发机**上先生成，再回到构建机出包：" >&2
        echo "      bash scripts/gen-device-tree.sh --write" >&2
        echo "  （导出/校验口径见 docs/2026-10-11-unfinished-plan-ledger.md §12 第 6 项；" >&2
        echo "    生成器 = tools/DeviceExport，示例工程定义 = firmware/t113/sample/MockChineseProject/）" >&2
        exit 1   # ⚠ build.sh 里没有 die()（它是 ci-gate.sh 的）—— 首版我照抄了，bash -n 查不出来
                 #    （只查语法），会在真正缺产物时炸成 "die: command not found" 而不是给出上面的提示。
    fi
    cp -r "$fw/generated/." "$dst/src-generated/"
    echo "deployed hmi-runtime -> $dst"
}

# 修正 env.cfg 启动参数：本板从 NAND 启动（squashfs@UBI），SDK 默认 bootcmd
# 是 setargs_mmc（SD 卡启动）→ 内核拿 root=/dev/mmcblk0p5 挂根失败卡开机画面。
# 2026-08-16 实机定位；每次构建强制刷回 setargs_nand。
# WavePI-S6（bingpi_m2）出厂 env.cfg 已是 setargs_nand，此函数幂等无害。
fix_env_bootcmd() {
    local cfg="$TINA/device/config/chips/t113/configs/$BOARD_DIR/env.cfg"
    [ -f "$cfg" ] || { echo "warning: env.cfg 不存在: $cfg"; return 0; }
    sed -i 's|^bootcmd=.*|bootcmd=sleep ${bootdelay}; run setargs_nand boot_normal|' "$cfg"
    grep -q "^bootcmd=.*setargs_nand" "$cfg" \
        && echo "env.cfg: bootcmd -> setargs_nand (NAND 启动)" \
        || echo "error: env.cfg bootcmd 修正失败"
}

# 修正 sys_config.fex 存储介质配置。
# ⚠ 实测（2026-08-16）：storage_type 改为 5(spinand) 后，boot0 注入的配置会让
#   OP-TEE（S3 版本，硬件校验 allowlist）报 check_hardware_info hardware check
#   error1 卡死开机 —— 本板实际可用值就是 SDK 默认的 1（boot0 的 spinand 支持
#   为编译期行为，与该项无关）。每次构建强制刷回 1，防止误改。
fix_sys_config_storage() {
    local cfg="$TINA/device/config/chips/t113/configs/$BOARD_DIR/sys_config.fex"
    [ -f "$cfg" ] || { echo "warning: sys_config.fex 不存在: $cfg"; return 0; }
    sed -i 's/^storage_type[[:space:]]*=.*/storage_type    = 1/' "$cfg"
    grep -q "^storage_type[[:space:]]*=.*1" "$cfg" \
        && echo "sys_config.fex: storage_type -> 1 (与 OP-TEE 硬件校验兼容)" \
        || echo "error: sys_config.fex storage_type 修正失败"
}

# 禁用/清理 SDK 自带 LVGL demo（lv_examples）：
# SDK 出厂脚本 /etc/init.d/S99swupdate_autorun 开机无条件启动 `lv_examples 0 &`，
# 抢占 /dev/fb0 把 HMI 界面盖掉（2026-08-17 实机定位）。两层处理：
#   1) 删除 SDK 内 lv_examples 软件包目录（其 Config.in 无 PACKAGE_ 符号定义，
#      仅由 Makefile 生成，且 tmp/.config-package.in 有 `default y if DEFAULT_lv_examples`，
#      仅 sed .config 会被 conf --defconfig 翻回 y —— 必须删包才能根治）；
#   2) .config 里 CONFIG_PACKAGE_lv_examples 置 n（防残留符号）。
# S97hmiruntime 另在开机时注释掉 S99swupdate_autorun 中的 lv_examples 行（双保险）。
fix_disable_lv_examples() {
    local sdk_cfg="$TINA/.config"
    local pkg_dir="$TINA/package/gui/littlevgl-8/lv_examples"
    [ -d "$pkg_dir" ] && rm -rf "$pkg_dir" \
        && echo "lv_examples 包目录已删除: $pkg_dir"
    [ -f "$sdk_cfg" ] || { echo "warning: SDK .config 不存在: $sdk_cfg"; return 0; }
    sed -i 's/^CONFIG_PACKAGE_lv_examples=y$/CONFIG_PACKAGE_lv_examples=n/' "$sdk_cfg"
    sed -i 's/^CONFIG_PACKAGE_lv_examples=m$/CONFIG_PACKAGE_lv_examples=n/' "$sdk_cfg"
    grep -q "^CONFIG_PACKAGE_lv_examples=n" "$sdk_cfg" \
        && echo "SDK .config: CONFIG_PACKAGE_lv_examples -> n (禁用 LVGL widgets demo)" \
        || echo "info: .config 无 CONFIG_PACKAGE_lv_examples（包已删除，属正常）"
}

# 给 SDK 的 swupdate_cmd.sh 补 `swu_next=hmi_restart` 分支 —— HMI `.so` 热 OTA 的
# 最后一环。设计见 .trae/documents/t113-spinand-ota-hmi-so.md：
#   sw-description 写 UDISK 新 .so + `bootenv` 置 swu_next=hmi_restart，
#   随后需有人重启 hmi_runtime，新进程启动时加载 UDISK 上的新 .so。
# 但 SDK 出厂脚本只处理 `swu_next=reboot`（清单里「swupdate_cmd.sh 含 hmi_restart
# 分支」那一格始终没勾）⇒ swu_next=hmi_restart 无人消费，热 OTA 实际要等到重启才生效。
# 这里补上：清标记 + 调 S97hmiruntime restart（= rc.common 默认 stop;start），
# **不依赖 procd respawn**（bingpi_m2 的 rootfs 没有 procd，靠 respawn 必然不通）。
fix_swupdate_hmi_restart() {
    local f="$TINA/package/allwinner/swupdate/swupdate_cmd.sh"
    [ -f "$f" ] || { echo "warning: swupdate_cmd.sh 不存在: $f"; return 0; }
    if grep -q "hmi_restart" "$f"; then
        echo "swupdate_cmd.sh: hmi_restart 分支已存在（跳过）"
        return 0
    fi
    if ! grep -q "swu_next: ##" "$f"; then
        echo "warning: swupdate_cmd.sh 未找到 'swu_next: ##' 锚点（SDK 版本可能已变），未打 hmi_restart 补丁" >&2
        return 0
    fi
    awk '
        { print }
        !done && /swu_next: ##/ {
            print "        # zhishu-hmi 补丁：HMI .so 热 OTA —— 清标记并重启 hmi_runtime，"
            print "        # 新进程启动时加载 UDISK 上刚写入的 libhmi_ui.so（不依赖 procd respawn）"
            print "        if [ x\"$swu_next\" = \"xhmi_restart\" ]; then"
            print "            fw_setenv swu_next"
            print "            [ -x /etc/init.d/S97hmiruntime ] && /etc/init.d/S97hmiruntime restart"
            print "        fi"
            done=1
        }
    ' "$f" > "$f.zhishu.tmp" && mv "$f.zhishu.tmp" "$f"
    grep -q "hmi_restart" "$f" \
        && echo "swupdate_cmd.sh: 已补 swu_next=hmi_restart 分支（清标记 + S97hmiruntime restart）" \
        || echo "error: swupdate_cmd.sh hmi_restart 补丁写入后校验失败" >&2
}

# ── 板载网络：统一到 rc.modules ──
#
# 三件事（都幂等）：
#  1) rc.modules 里给 eth0 配**静态 IP** —— 文档承诺的 192.168.100.100/24（flashing-guide §5）。
#  2) rc.modules 里开机起 sshd —— 文档承诺的 SSH 通道，原先只由 /etc/profile 在**登录时**
#     才起（没人登录就没有 SSH，而 adb 一掉线又没人能登录 ⇒ 死锁）。
#  3) /etc/profile 里删掉**出厂 eth0 重配块**（`mode=1` → 无条件 `ifconfig eth0 down` +
#     静态 192.168.10.6 + 默认路由 192.168.10.1）。它是 2026-10-10 实测到的真凶：
#     /etc/profile 会被**任何登录 shell（含 adb shell）** source，于是它在 rc.modules
#     之后把 192.168.100.100 **覆盖成 192.168.10.6**（开发机侧 192.168.100.0/24 与
#     192.168.10.0/24 互不可达 ⇒ 看着"网络不通"），并且每次登录都 down/up eth0 ⇒
#     **SSH 一登录就踩断自己**。
#
# 为何是静态而非 DHCP：现场为工控静态网段（Modbus TCP / MQTT 对端默认 192.168.100.10），
# 开发机侧 enp8s0 也是 `ipv4.method=manual` 的 192.168.100.50/24，该网段没有 DHCP。
#
# 覆盖项：HMI_ETH0_IP / HMI_ETH0_MASK（默认 192.168.100.100 / 255.255.255.0）、
#         HMI_SSH_AT_BOOT=0（不开机起 sshd，保持"登录才起"的出厂姿态）。
fix_board_network() {
    local ip="${HMI_ETH0_IP:-192.168.100.100}"
    local mask="${HMI_ETH0_MASK:-255.255.255.0}"
    local base="$TINA/target/allwinner/t113-$BOARD_DIR/busybox-init-base-files"
    local rcm="$base/etc/init.d/rc.modules"
    local prof="$base/etc/profile"

    # ── 1) rc.modules：静态 IP ──
    if [ ! -f "$rcm" ]; then
        echo "warning: rc.modules 不存在: $rcm（跳过板载网络）" >&2
    elif grep -q "zhishu-hmi: eth0 ip" "$rcm"; then
        echo "rc.modules: eth0 静态 IP 已存在（跳过）"
    elif ! grep -q "^ifconfig eth0 up" "$rcm"; then
        echo "warning: rc.modules 未找到 'ifconfig eth0 up' 锚点（SDK 版本可能已变），未打网络补丁" >&2
    else
        awk -v ip="$ip" -v mask="$mask" '
            { print }
            !done && /^ifconfig eth0 up[[:space:]]*$/ {
                print ""
                print "# zhishu-hmi: eth0 ip —— 板载默认地址（flashing-guide §5）"
                print "# 现场静态网段（对端 Modbus/MQTT 默认 192.168.100.10），不做 DHCP；"
                print "# 构建时可用 HMI_ETH0_IP / HMI_ETH0_MASK 覆盖。"
                print "ifconfig eth0 " ip " netmask " mask " up"
                done=1
            }
        ' "$rcm" > "$rcm.zhishu.tmp" && mv "$rcm.zhishu.tmp" "$rcm"
        grep -q "zhishu-hmi: eth0 ip" "$rcm" \
            && echo "rc.modules: eth0 静态 IP -> $ip netmask $mask" \
            || echo "error: rc.modules eth0 IP 补丁写入后校验失败" >&2
    fi

    # ── 2) rc.modules：**只清理**历史上打过的 SSH 行 ──
    # SSH 的启动已移到本包自带的 S97hmiruntime（rc.final 阶段、环境就绪）；
    # 见那里的注释：rc.modules 阶段太早 —— 实测裸起与 start-stop-daemon 起的
    # dropbear 开机后都消失（host key 生成在早期也可能拿不到熵 → 无 key 的
    # dropbear 立即退出）。这里只负责把旧行清干净，避免两处重复起。
    if [ -f "$rcm" ]; then
        sed -i '\|^# zhishu-hmi: sshd at boot|d' "$rcm"
        sed -i '\#^pidof sshd >/dev/null 2>&1 || /etc/init.d/sshd start$#d' "$rcm"
        sed -i '\|^# zhishu-hmi: ssh at boot|d' "$rcm"
        sed -i '\#^pidof dropbear >/dev/null 2>&1 || /etc/init.d/dropbear start$#d' "$rcm"
        sed -i '\#^pidof dropbear >/dev/null 2>&1 || start-stop-daemon -S -b -x /usr/sbin/dropbear#d' "$rcm"
        sed -i '\#^\[ -s /etc/dropbear/dropbear_rsa_host_key \]#d' "$rcm"
        if grep -q "dropbear" "$rcm"; then
            echo "warning: rc.modules 仍残留 dropbear 行，请检查" >&2
        else
            echo "rc.modules: SSH 启动已移出（现由 S97hmiruntime 负责）"
        fi
    fi

    # ── 3) /etc/profile：删掉出厂 eth0 重配块 ──
    if [ ! -f "$prof" ]; then
        echo "warning: profile 不存在: $prof（跳过）" >&2
    elif grep -q "zhishu-hmi: 出厂 eth0 重配块已移除" "$prof"; then
        echo "profile: 出厂 eth0 重配块已移除（跳过）"
    elif ! grep -q "^mode=1" "$prof"; then
        echo "info: profile 无 'mode=1' 出厂网络块（SDK 版本可能已变），未改 profile"
    else
        sed -i '/^mode=1/,/^fi$/d' "$prof"
        sed -i '/^#network config$/a # zhishu-hmi: 出厂 eth0 重配块已移除（原为无条件 静态 192.168.10.6 + ifconfig eth0 down\/up，会覆盖 rc.modules 的 IP 且每次登录踩断 SSH）' "$prof"
        if grep -q "zhishu-hmi: 出厂 eth0 重配块已移除" "$prof"; then
            echo "profile: 已移除出厂 eth0 重配块"
        else
            echo "error: profile 补丁写入后校验失败" >&2
        fi
        # 登录时的 SSH 启动加 pidof 守卫并指向 dropbear，避免与开机那次重复拉起
        grep -q "^/etc/init.d/sshd start$" "$prof" \
            && sed -i 's#^/etc/init.d/sshd start$#pidof dropbear >/dev/null 2>\&1 || /etc/init.d/dropbear start#' "$prof"
    fi

    # profile 里那句登录时 SSH 启动统一对齐 dropbear（幂等；覆盖早期版本留下的 openssh 版）
    if [ -f "$prof" ] && grep -q "^pidof sshd >/dev/null 2>&1 || /etc/init.d/sshd start$" "$prof"; then
        sed -i 's#^pidof sshd >/dev/null 2>&1 || /etc/init.d/sshd start$#pidof dropbear >/dev/null 2>\&1 || /etc/init.d/dropbear start#' "$prof"
        echo "profile: 登录时 SSH 启动已改为 dropbear"
    fi
}

# 在**板级 defconfig** 里打开 SSH 服务端（dropbear），找回文档承诺的「SSH 登录（root/tina）」。
#
# ⚠ 为何改 defconfig 而不是 .config（2026-10-10 实测定位）：
#   本 SDK 的 `.config` 是**从板级 defconfig 派生**的 —— 手工 `sed .config` 打开
#   `CONFIG_PACKAGE_openssh-server=y` 后构建，符号会被原样翻回 `is not set`；
#   对照 `.config` 与 `target/allwinner/t113-bingpi_m2/defconfig` 的
#   `CONFIG_PACKAGE_*=y` 计数完全一致（110/110）。SDK 级包开关要持久生效必须写进
#   defconfig。这条也解释了指南表 ⑥ 那个 procd 缺口。
#
# ⚠ 为何用 dropbear 而不是 openssh：
#   `CONFIG_PACKAGE_openssh-server=y` 会让构建**直接失败**（本 SDK 的 openssl 未启用
#   ECC，openssh 8.0p1 链接期报 `undefined reference to kex_ecdh_keypair`
#   —— 这也正是 Tina 默认关掉它的原因）。dropbear 体积小、依赖少、源码已在
#   dl/dropbear-2015.71.tar.bz2，且自带 `/etc/init.d/dropbear`。
#   同时把 openssh 两个符号**显式关回去**，避免上次试开留下的 =y 继续让构建失败。
fix_ssh_server() {
    local f="$TINA/target/allwinner/t113-$BOARD_DIR/defconfig"
    [ -f "$f" ] || { echo "warning: defconfig 不存在: $f（跳过 SSH 包）" >&2; return 0; }

    local changed=0

    # 1) 回退 openssh（编不过，见上）
    local k
    for k in openssh-server openssh-keygen; do
        if grep -q "^CONFIG_PACKAGE_$k=y$" "$f"; then
            sed -i "s|^CONFIG_PACKAGE_$k=y$|# CONFIG_PACKAGE_$k is not set|" "$f"
            echo "defconfig: 回退 $k（openssh 在本 SDK 编不过）"
            changed=1
        fi
    done

    # 2) 开启 dropbear
    if ! grep -q "^CONFIG_PACKAGE_dropbear=y$" "$f"; then
        if grep -q "^# CONFIG_PACKAGE_dropbear is not set$" "$f"; then
            sed -i 's|^# CONFIG_PACKAGE_dropbear is not set$|CONFIG_PACKAGE_dropbear=y|' "$f"
        else
            printf 'CONFIG_PACKAGE_dropbear=y\n' >> "$f"
        fi
        changed=1
    fi

    if [ "$changed" = "1" ]; then
        echo "defconfig: 已开启 dropbear（SSH 服务端）"
    else
        echo "defconfig: SSH 包配置已就绪（跳过）"
    fi
    grep -q "^CONFIG_PACKAGE_dropbear=y$" "$f" \
        || echo "warning: defconfig 里 dropbear 未开启，镜像将没有 SSH" >&2
}

# 校验 SDK 级必备配置（详见 docs/flashing-guide.md「本板固件构建必备配置」）：
# ③ hmi-runtime 包必须 y（=m 时出厂镜像不含 HMI）。
# 仅对 mq_r 检查 ④⑤（boot_package 禁 OP-TEE、内核关 PSCI/CAN）：
#   mq_r 板 OP-TEE 二进制与板不匹配（硬件校验 error1 → 卡开机画面），必须禁 TEE；
#   WavePI-S6（bingpi_m2）出厂镜像即带 optee + can1=okay（官方验证可跑），
#   OP-TEE 与板匹配，**无需禁 TEE/PSCI/CAN** —— 保持官方配置即可。
# 仅提示不修改（SDK 配置由构建者一次性设置）。
check_sdk_config() {
    local sdk_cfg="$TINA/.config"
    local bp_cfg="$TINA/device/config/chips/t113/configs/default/boot_package.cfg"
    local k_cfg="$TINA/device/config/chips/t113/configs/$BOARD_DIR/linux-5.4/config-5.4"

    # 硬失败项：缺这一条打出的镜像**不含 HMI 运行时**（设备起不来界面），
    # 属于"看起来构建成功、实际交付不可用"，必须在整编/打包阶段直接拦住。
    if ! grep -q "^CONFIG_PACKAGE_hmi-runtime=y" "$sdk_cfg" 2>/dev/null; then
        echo "error: SDK .config 缺少 CONFIG_PACKAGE_hmi-runtime=y（当前镜像不会包含 HMI 运行时）" >&2
        echo "       请先执行: $0 config $TINA $BOARD   勾选 hmi-runtime 后再整编/打包。" >&2
        exit 1
    fi
    [ ! -d "$TINA/package/gui/littlevgl-8/lv_examples" ] \
        || echo "⚠ lv_examples 包目录仍存在（应删除；残留时 conf --defconfig 可能把配置翻回 y → demo 抢占显示）"
    if [ "$BOARD" = "mq_r" ]; then
        grep -q "^item=optee" "$bp_cfg" 2>/dev/null \
            && echo "⚠ boot_package.cfg: optee 未禁用（mq_r 板 OP-TEE 硬件校验 error1 → 卡开机画面）"
        grep -q "^CONFIG_ARM_PSCI=y" "$k_cfg" 2>/dev/null \
            && echo "⚠ 内核 config: CONFIG_ARM_PSCI 必须为 n（mq_r 无 TEE 时开机即崩）"
        grep -q "^CONFIG_CAN_SUN8I=y" "$k_cfg" 2>/dev/null \
            && echo "⚠ 内核 config: CONFIG_CAN_SUN8I 必须为 n（mq_r CAN 驱动在 TEE 侧）"
    else
        echo "info: BOARD=$BOARD 保持官方 TEE/PSCI/CAN 配置（WavePI-S6 出厂即带 optee，无需禁用）"
        # bingpi_m2 镜像默认**不含 procd**（busybox init）。后果不是"跑不起来"，而是
        # **没人监管**：adbd 与 hmi_runtime 都只被起一次，进程退出后不会自动回来
        # —— 实测 adbd 在开机几分钟后掉线，USB 链路自己恢复到 480M 了 adbd 也没回来，
        # 必须重启（2026-10-10）。这里只提示，不阻断：脚本有 start-stop-daemon 回退，
        # 热 OTA 也已由 swu_next=hmi_restart 接管（fix_swupdate_hmi_restart）。
        grep -q "^CONFIG_PACKAGE_procd=y" "$sdk_cfg" 2>/dev/null \
            || echo "⚠ SDK .config 未启用 CONFIG_PACKAGE_procd：本镜像 adbd/hmi_runtime 均无 procd 监管（不 respawn），进程退出只能重启恢复（见 docs/flashing-guide.md 表 ⑥）"
    fi
}

# ── 打包（带产物断言） ──
# 为何不直接写 `pack`：Tina SDK 的 `pack()` 末尾是
#     independently_pack=$(grep "CONFIG_SUPPORT_PACK_OUT_OF_TINA=y" $T/.config)
# 该选项未设时 grep 返回 1（`# CONFIG_SUPPORT_PACK_OUT_OF_TINA is not set`），
# 而本脚本开头是 `set -e` ⇒ **pack 已经成功、镜像已经产出**，脚本仍以退出码 1 结束，
# 且打不出末尾那行 `=== done ===`（2026-10-10 实机定位：日志有 `pack finish` 与镜像路径，
# 退出码却是 1）。把「失败」建立在会被环境左右的退出码上不可靠，故这里关掉 errexit
# 调 pack，改用**产物断言**判定：镜像存在，且是本次调用新产出的。
run_pack() {
    local img="$TINA/out/$OUT_DIR/$IMG_NAME"
    local start rc mtime
    start="$(date +%s)"
    set +e
    pack
    rc=$?
    set -e

    [ -f "$img" ] || { echo "error: pack 未产出镜像：$img" >&2; exit 1; }
    mtime="$(stat -c %Y "$img" 2>/dev/null || echo 0)"
    if [ "$mtime" -lt "$start" ]; then
        echo "error: pack 返回 $rc，且镜像未更新（疑似沿用旧产物，本次打包实际没执行）：$img" >&2
        exit 1
    fi
    [ "$rc" -eq 0 ] \
        || echo "info: pack 返回 $rc（Tina pack() 末尾 grep 的已知假失败；镜像已更新，视为成功）"
    echo "packed: $img ($(du -h "$img" | cut -f1))"
}

case "$CMD" in
config)
    set_model
    deploy_package
    cd "$TINA"
    make menuconfig
    ;;
pkg)
    set_model
    deploy_package
    cd "$TINA"
    # 加载 Tina 构建环境（mm/pack 等为 envsetup.sh 提供的函数）
    source build/envsetup.sh
    lunch "$LUNCH_NAME"
    # mm 需在包目录内执行（scripts/mm.sh 向上查找含 define Package 的 Makefile）
    cd "$TINA/package/hmi-runtime"
    # 为何不用 `mm -B` 的退出码判成败：SDK 的 scripts/mm.sh 会**吞掉**内层 make 的失败
    # （实测编译 fatal error 时 `build.sh pkg` 仍返回 0 —— 「看起来构建成功、实际没产物」，
    # 正是最该拦住的一类）。故同样关掉 errexit，用**产物断言**判定。
    PKG_BUILT="$TINA/build/hmi-runtime/src/hmi_runtime"
    PKG_START="$(date +%s)"
    set +e
    mm -B
    PKG_RC=$?
    set -e

    [ -x "$PKG_BUILT" ] \
        || { echo "error: mm 未产出 $PKG_BUILT（编译很可能失败，见上方 make 输出）" >&2; exit 1; }
    if [ "$(stat -c %Y "$PKG_BUILT" 2>/dev/null || echo 0)" -lt "$PKG_START" ]; then
        echo "error: mm 返回 $PKG_RC，且 $PKG_BUILT 未重新编译（疑似 mm 根本没执行）" >&2
        exit 1
    fi
    [ "$PKG_RC" -eq 0 ] \
        || echo "info: mm 返回 $PKG_RC，但产物已重新编译，视为成功"
    echo "built: $PKG_BUILT ($(du -h "$PKG_BUILT" | cut -f1))"
    ;;
all)
    set_model
    deploy_package
    fix_env_bootcmd
    fix_sys_config_storage
    fix_disable_lv_examples
    fix_swupdate_hmi_restart
    fix_board_network
    fix_ssh_server
    check_sdk_config
    cd "$TINA"
    source build/envsetup.sh
    lunch "$LUNCH_NAME"
    make
    run_pack
    ;;
pack)
    set_model
    fix_env_bootcmd
    fix_sys_config_storage
    fix_disable_lv_examples
    fix_swupdate_hmi_restart
    fix_board_network
    fix_ssh_server
    check_sdk_config
    cd "$TINA"
    # pack 是 envsetup.sh 提供的函数，需先加载构建环境（原脚本漏了此步 → pack: command not found）
    source build/envsetup.sh
    lunch "$LUNCH_NAME"
    run_pack
    ;;
*)
    echo "usage: $0 {config|pkg|all|pack} <TINA_ROOT> [BOARD]"
    echo "BOARD: mq_r(默认)=t113-mq_r 板 / bingpi_m2=WavePI-S6 7 寸板"
    exit 1
    ;;
esac

echo "=== done. 产物目录: $TINA/out/$OUT_DIR/ ==="
