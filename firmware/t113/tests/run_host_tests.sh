#!/usr/bin/env bash
# 主机侧单元测试运行脚本（不依赖 T113 硬件 / Tina SDK 头文件）
# 用法：./run_host_tests.sh
#
# 覆盖：
#   1) hmi_comm_test      —— 通讯配置解析 / 变量表轮询组 / 数据类型转换 /
#      MQTT 报文构建（长主题、长凭据、**遗嘱 LWT** 的容量边界，编译带 -fstack-protector-all）/
#      **MQTT 主题-payload 契约 v1**（阶段 264：跨语言样例表 + 非法参数 + 容量不足不写半截）/
#      **远程写命令接线**（阶段 289：/cmd/ → hmi_cmd_handle）/
#      **遗嘱与订阅表存续**（阶段 290：`mqtt_start` 不再清空宿主已注册的订阅）
#   2) touch_scale_test   —— 触摸坐标归一化（含屏幕边缘可达性回归，审查 P0-7）
#      并分别以「默认板级(WavePI) / mq_r / -D 覆盖」三种配置各跑一遍，
#      实测板级量程取值与覆盖优先级。
#   3) board_profile_test —— 板级配置单一来源（board_profile.h）的取值与跨字段不变量，
#      同样三种配置各跑一遍。
#   4) hmi_crypto_test    —— 密码学原语（SHA-256 / Base64 / 定长比较）。
#      期望值全部来自**外部真值**：标准 SHA-256 向量（覆盖 0/3/43/56/64/65/112 字节）
#      + 由 Python hashlib 独立算出的本工程向量 —— 后者与编辑器侧
#      RuntimePasswordHasherTests / RuntimeCredentialVectors 用的是**同一组**。
#   5) hmi_actions_permission_test —— 会话/权限链路（未登录拒绝受限动作、凭据提交建会话、
#      按角色放行、logout 复位）。
#   6) hmi_actions_credential_test —— 凭据校验链路（正确/错误口令、未知用户、停用账户、
#      空/短口令、连续失败锁定与自动解锁）。
#      权限模型 + 运行时用户表来自 tests/fixtures/generated_security.h —— 它是编辑器的
#      **真实生成产物**，由 C# 侧 SecurityConfigCodeGenTests 逐字节守卫。
#   7) hmi_actions_session_test   —— 会话超时 + 审计落盘（阈值边界严格大于、权限判定即活动、
#      poll 不刷新活动时间、超时清会话并跳生成期解析出的锁屏屏、审计文件内容）。
#      会话策略同样来自那份 fixture（SessionTimeoutMin=30 / AutoLogoutEnabled=1 /
#      LockScreenId → 屏名「锁屏」），所以测到的就是设备端将来拿到的那一份。
#   8) hmi_log_test       —— 运行时参数下沉：日志门槛（HMI_LOG_LEVEL）与轮询周期
#      （HMI_POLL_INTERVAL_MS）来自 tests/fixtures/generated_runtime.h（同样是生成器的真实
#      产物，由 C# 侧 RuntimeConfigCodeGenTests 逐字节守卫）。判据落在**实际 stderr 输出**上
#      （捕获后数哪几行出来了），而不是产物文本 —— 文本对而宏没生效是这类下沉最典型的坏法。
#      四个场景：生成头在/不在 × 门槛三级；场景 4 刻意不带 fixture（测回退路径）。
#   9) check_lv_conf_flags.sh —— 跟踪配置（config/lv_conf.h）的关键项守卫：SVG 渐变开关
#      必须为 1，且 LV_GRADIENT_MAX_STOPS 必须与 C# 侧 SvgGradientGeometry.MaxStops 同源。
#      **性质与前 8 步不同**：前 8 步是"跑起来看行为"，这一步是不跑代码的配置判据 +
#      一次让 LVGL 真去消费这份配置的预处理探针（前 8 步没有任何一步会解析 lv_conf.h）。
#  10) mqtt_fake_broker_test —— **假 broker**（127.0.0.1 上的最小 broker：监听 + CONNACK + SUBACK）：
#      CONNECT 的遗嘱三件事与段顺序 · SUBSCRIBE 含 `<pre>/cmd/<tag>` · 上线自动上报
#      `<pre>/status`（retain=1）· 下行 PUBLISH 真的被设备消息回调收到。纯 socket、无外部依赖。
set -e
cd "$(dirname "$0")/.."

# gcc 产物是固定名、落在小容量 /tmp 上（本机 /tmp 仅 10 MB），跑完顺手清掉，
# 否则反复跑门禁会累积成"固定名 + 不清理"的垃圾。审计临时文件同理。
TMP_OUT=(
    /tmp/hmi_comm_test
    /tmp/hmi_touch_scale_test
    /tmp/hmi_board_profile_test
    /tmp/hmi_crypto_test
    /tmp/hmi_actions_permission_test
    /tmp/hmi_actions_credential_test
    /tmp/hmi_actions_session_test
    /tmp/hmi_actions_session_audit.log
    /tmp/hmi_log_test
    /tmp/hmi_log_test_capture.txt
    /tmp/mqtt_fake_broker_test
)
cleanup() { for f in "${TMP_OUT[@]}"; do rm -f "$f"; done; }
trap cleanup EXIT

echo "########## [1/11] 通讯与变量表 ##########"
OUT=/tmp/hmi_comm_test
# -fstack-protector-all 是**判据的一部分**，不是加固选项：MQTT 报文构建器曾用
# `uint8_t vh[8]` 装 128 字节上限的主题 ⇒ 栈越界。越界写的是**函数内部**的栈缓冲，
# 输出字节可能碰巧仍然正确，普通断言原理上看不见它；插上金丝雀后写穿即 abort
# （阶段 120 负控实测：修复前该测试以 `*** stack smashing detected ***` 终止）。
gcc -std=c99 -D_GNU_SOURCE -DHMI_UNIT_TEST -Wall -Wextra -fstack-protector-all \
    -Iapp/runtime -Iapp/comm -Iapp/drivers -Iconfig -Igenerated \
    -Itests/fixtures \
    tests/hmi_comm_test.c \
    app/comm/comm_core.c app/comm/modbus_rtu.c \
    app/comm/modbus_tcp.c app/comm/mqtt_client.c app/comm/mqtt_contract.c \
    app/comm/mqtt_tls_contract.c app/comm/tls_proxy_config.c \
    app/comm/mqtt_cmd_contract.c \
    app/comm/mqtt_acl_contract.c \
    app/comm/mqtt_cmd_ledger.c app/comm/mqtt_write_guard.c app/comm/mqtt_cmd_dispatch.c \
    app/comm/mqtt_cmd_wiring.c \
    app/drivers/gpio_sunxi.c \
    -lpthread -o "$OUT"

"$OUT"

echo
echo "########## [2/11] 触摸坐标归一化 ##########"
TOUCH_INC="-Iapp/drivers -Iapp/runtime"
OUT_TOUCH=/tmp/hmi_touch_scale_test

echo "--- 默认板级（WavePI-S6：DTS 800x480）---"
gcc -std=c99 -Wall -Wextra $TOUCH_INC tests/touch_scale_test.c -o "$OUT_TOUCH"
"$OUT_TOUCH"

echo "--- t113-mq_r（HMI_BOARD_WAVEPI=0）---"
gcc -std=c99 -Wall -Wextra -DHMI_BOARD_WAVEPI=0 $TOUCH_INC \
    tests/touch_scale_test.c -o "$OUT_TOUCH"
"$OUT_TOUCH"

echo "--- -D 覆盖优先（HMI_TOUCH_MAX_X=900）---"
gcc -std=c99 -Wall -Wextra -DHMI_TOUCH_MAX_X=900 -DHMI_TOUCH_SCALE_TEST_OVERRIDE_X=900 $TOUCH_INC \
    tests/touch_scale_test.c -o "$OUT_TOUCH"
"$OUT_TOUCH"

echo
echo "########## [3/11] 板级配置 ##########"
BOARD_INC="-Iapp/runtime -Iapp/drivers"
OUT_BOARD=/tmp/hmi_board_profile_test

echo "--- 默认板级（WavePI-S6）---"
gcc -std=c99 -Wall -Wextra $BOARD_INC tests/board_profile_test.c -o "$OUT_BOARD"
"$OUT_BOARD"

echo "--- t113-mq_r（HMI_BOARD_WAVEPI=0）---"
gcc -std=c99 -Wall -Wextra -DHMI_BOARD_WAVEPI=0 $BOARD_INC \
    tests/board_profile_test.c -o "$OUT_BOARD"
"$OUT_BOARD"

echo "--- -D 覆盖（HMI_TOUCH_MAX_X=900）---"
gcc -std=c99 -Wall -Wextra -DHMI_TOUCH_MAX_X=900 -DHMI_BOARD_PROFILE_TEST_OVERRIDE=900 $BOARD_INC \
    tests/board_profile_test.c -o "$OUT_BOARD"
"$OUT_BOARD"

echo
echo "########## [4/11] 密码学原语（hmi_crypto） ##########"
OUT_CRYPTO=/tmp/hmi_crypto_test
gcc -std=c99 -Wall -Wextra -Iapp/runtime \
    tests/hmi_crypto_test.c app/runtime/hmi_crypto.c \
    -o "$OUT_CRYPTO"
"$OUT_CRYPTO"

echo
echo "########## [5/11] 会话与权限（hmi_actions） ##########"
# 权限模型来自编辑器生成头（tests/fixtures/generated_security.h），由 __has_include 命中。
# hmi_actions.c 的凭据校验依赖 hmi_crypto.c，必须一并链接。
ACTION_INC="-Iapp/runtime -Itests/fixtures"
ACTION_SRC=(app/runtime/hmi_actions.c app/runtime/hmi_crypto.c)
OUT_ACTION=/tmp/hmi_actions_permission_test
gcc -std=c99 -Wall -Wextra $ACTION_INC \
    tests/hmi_actions_permission_test.c "${ACTION_SRC[@]}" \
    -o "$OUT_ACTION"
"$OUT_ACTION"

echo
echo "########## [6/11] 凭据校验（hmi_login_submit） ##########"
OUT_CRED=/tmp/hmi_actions_credential_test
gcc -std=c99 -Wall -Wextra $ACTION_INC \
    tests/hmi_actions_credential_test.c "${ACTION_SRC[@]}" \
    -o "$OUT_CRED"
"$OUT_CRED"

echo
echo "########## [7/11] 会话超时与审计（hmi_session_poll / hmi_audit） ##########"
# -DHMI_SEC_AUDIT_PATH 是**必须**的：hmi_actions.c 里的缺省值是设备路径
# /mnt/UDISK/hmi/audit.log（唯一持久可写分区），主机上写不进去。
# 测试自己有一条判据守着这件事（不变式：绝不能拿设备路径跑主机测试）。
OUT_SESSION=/tmp/hmi_actions_session_test
AUDIT_LOG=/tmp/hmi_actions_session_audit.log
gcc -std=c99 -Wall -Wextra $ACTION_INC -DHMI_SEC_AUDIT_PATH="\"$AUDIT_LOG\"" \
    tests/hmi_actions_session_test.c "${ACTION_SRC[@]}" \
    -o "$OUT_SESSION"
"$OUT_SESSION"

echo
echo "########## [8/11] 运行时参数下沉（日志门槛 / 轮询周期） ##########"
# 四个场景各编一次：生成头在/不在 × 门槛三级。期望值逐场景硬编码在测试源码里。
# 场景 4 **刻意不带** -Itests/fixtures —— 它测的就是"缺生成头时回退历史默认值"。
RUN_INC="-Iapp/runtime -Itests/fixtures"
OUT_LOG=/tmp/hmi_log_test

echo "--- 场景 1：带生成头，HMI_LOG_LEVEL=2（fixture 默认 Warning）---"
gcc -std=c99 -Wall -Wextra -DHMI_LOG_TEST_SCENARIO=1 $RUN_INC \
    tests/hmi_log_test.c -o "$OUT_LOG"
"$OUT_LOG"

echo "--- 场景 2：-DHMI_LOG_LEVEL=0 覆盖（Debug ⇒ 全开）---"
gcc -std=c99 -Wall -Wextra -DHMI_LOG_TEST_SCENARIO=2 -DHMI_LOG_LEVEL=0 $RUN_INC \
    tests/hmi_log_test.c -o "$OUT_LOG"
"$OUT_LOG"

echo "--- 场景 3：-DHMI_LOG_LEVEL=3 覆盖（Error ⇒ 只留 ERROR 及以上）---"
gcc -std=c99 -Wall -Wextra -DHMI_LOG_TEST_SCENARIO=3 -DHMI_LOG_LEVEL=3 $RUN_INC \
    tests/hmi_log_test.c -o "$OUT_LOG"
"$OUT_LOG"

echo "--- 场景 4：不带生成头（回退无条件输出 + 周期 100）---"
gcc -std=c99 -Wall -Wextra -DHMI_LOG_TEST_SCENARIO=4 -Iapp/runtime \
    tests/hmi_log_test.c -o "$OUT_LOG"
"$OUT_LOG"

echo
echo "########## [9/11] 跟踪的固件配置守卫（lv_conf 关键项） ##########"
# 为什么需要单独一步：上面 8 步虽然都带 `-Iconfig`，但**没有一个会去解析 lv_conf.h**
# （host 测试刻意不拉 lvgl.h，见 hmi_log.h 独立成文件的原因）⇒
# LV_USE_DRAW_SW_COMPLEX_GRADIENTS 被删掉时，整条门禁不会有任何报警。
# 详见 tests/check_lv_conf_flags.sh 的文件头（含 include 顺序为何是判据的一部分）。
bash tests/check_lv_conf_flags.sh

echo
echo "########## [10/11] 假 broker：上线/订阅/上报/下行 ##########"
# 方案 §5 给 M2 定的判据面原文就是「主机测试用**假 broker**（本地 socket 监听）断言报文序列」。
# 本步在 127.0.0.1 上起最小 broker（监听 + CONNACK + SUBACK），驱动**产品代码本身**走完整链路：
#   ① CONNECT 的遗嘱三件事与段顺序；② SUBSCRIBE 含 <pre>/cmd/<tag>（阶段 289 接线）；
#   ③ 上线自动上报 <pre>/status 且 retain=1（真实 on_mqtt_connected）；
#   ④/⑤ 下行信封经 on_mqtt_message → mqtt_cmd_wiring → hmi_cmd_handle 真的执行/拒绝；
#   ⑥ 报警上行 `<pre>/alarm/<id>` 的 st=/sev=（阶段 291），且**边沿触发**（状态没变不重复发）。
# 源文件列表与第 [1] 步同源（变量表 + 通信 + 远程写判定链），另给 hmi_actions 三个假实现。
OUT_FAKEBROKER=/tmp/mqtt_fake_broker_test
gcc -std=c99 -D_GNU_SOURCE -DHMI_UNIT_TEST -Wall -Wextra -fstack-protector-all \
    -Iapp/runtime -Iapp/comm -Iapp/drivers -Iconfig -Igenerated -Itests/fixtures \
    tests/mqtt_fake_broker_test.c \
    app/comm/comm_core.c app/comm/modbus_rtu.c app/comm/modbus_tcp.c \
    app/comm/mqtt_client.c app/comm/mqtt_contract.c app/comm/mqtt_cmd_contract.c \
    app/comm/mqtt_tls_contract.c \
    app/comm/mqtt_cmd_dispatch.c app/comm/mqtt_cmd_ledger.c app/comm/mqtt_write_guard.c \
    app/comm/mqtt_acl_contract.c app/comm/mqtt_cmd_wiring.c app/comm/mqtt_alarm_publish.c \
    app/drivers/gpio_sunxi.c \
    -lpthread -o "$OUT_FAKEBROKER"
"$OUT_FAKEBROKER"

echo
echo "########## [11/11] TLS 终止代理：配置校验 ＋ PSK/X.509 端到端（阶段 302） ##########"
# 判据面（契约 §4/§5 ＋ 评估 §6-2/§6-3/§6-4）：配置校验退出码 0/3/4（init 脚本按它分派）·
# PSK 与 X.509 两条路径的**双向**端到端 · §6-4 未校时（CA 拒 / PSK 不受影响）·
# §4 明文监听只绑回环（`ss` 实测）· §6-2 上游不可达 ⇒ 客户端一个字节都收不到（绝不降级）。
# 细节与"为什么这么判"都在脚本里；本步只负责调用与传播退出码。
bash tests/check_tls_proxy.sh

echo
# 刻意不打印 "exit=$?" —— set -e 下若任一步失败脚本已在失败处退出，这里永远是 0，
# 那是**恒真的假判据**：看着像"检查过退出码"，实际什么都没检查。退出码由调用方（ci-gate）取。
echo "########## 11 步全部通过 ##########"
