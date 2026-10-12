#!/bin/bash
# check_tls_proxy.sh — 本机 TLS 终止代理的主机侧判据（阶段 302）
#
# 判据面（契约 docs/2026-10-01-local-tls-proxy-contract.md §4/§5 ＋ 评估 §6-2/§6-3/§6-4）：
#   ① 配置校验退出码 0/3/4 —— init 脚本按它分派（0 启动 / 3 跳过 / 4 拒绝）
#   ② PSK 端到端：明文进 → TLS 出，**双向**都通
#   ③ X.509 端到端：同样双向（含对端主机名校验）
#   ④ §6-4 未校时：CA ⇒ **拒绝 ＋ 审计**；PSK ⇒ **不受影响**
#   ⑤ §4 明文监听**只绑回环**（`ss` 实测，不能是 0.0.0.0）
#   ⑥ §6-2 上游不可达 ⇒ 客户端**一个字节都收不到**（绝不降级为明文）
#
# 刻意**不用 set -e**：本脚本自己收集失败并返回汇总码（用 -e 会在第一处失败就退出，
# 后面几条判据的读数就看不到了 —— 那是"看着在查、其实只查了第一条"）。
set -u

cd "$(dirname "$0")/.." || exit 1

PASS=0
FAIL=0
ok()  { PASS=$((PASS + 1)); echo "  PASS  $1"; }
bad() { FAIL=$((FAIL + 1)); echo "  FAIL  $1"; }
eq()  { if [ "$2" = "$3" ]; then ok "$1"; else bad "$1（期望 [$3] 实得 [$2]）"; fi; }
has() { case "$2" in *"$3"*) ok "$1" ;; *) bad "$1（未找到 [$3]，实得 [$2]）" ;; esac; }

PROXY=/tmp/hmi_tls_proxy

# 等端口真的空出来再起下一个代理：前一个进程刚 SIGTERM、内核还没收干净时，
# 新进程会 bind 失败（实测：第一个 PSK 往返就栽在这个竞态上）。
wait_port_free() {
    for _ in $(seq 1 40); do
        ss -tln 2>/dev/null | grep -q ":$1 " || return 0
        sleep 0.1
    done
    return 1
}

# 等端口**真的在监听**（阶段 332）：没有它就分不清"broker 没起来"与"被拒"（上一轮假绿的根源）
wait_port_open() {
    local p="$1" i=0
    while [ $i -lt 60 ]; do
        ss -tln 2>/dev/null | grep -q ":$p " && return 0
        sleep 0.1; i=$((i + 1))
    done
    return 1
}
TLSWORK=$(mktemp -d /tmp/hmi_tls_proxy_XXXX)
trap 'rm -rf "$TLSWORK"' EXIT

# ── 编译（任何诊断输出都算失败：警告也是问题） ──
gcc -std=c99 -D_GNU_SOURCE -Wall -Wextra -Wno-deprecated-declarations \
    -Iapp/runtime -Iapp/comm \
    app/proxy/tls_proxy.c app/comm/tls_proxy_config.c app/comm/mqtt_tls_contract.c \
    -lssl -lcrypto -lpthread -o "$PROXY" 2> "$TLSWORK/build.log"
rc=$?
if [ $rc -ne 0 ]; then
    bad "代理编译失败"; sed 's/^/        /' "$TLSWORK/build.log"; echo "=== 结果: $PASS 通过, $FAIL 失败 ==="; exit 1
fi
if [ -s "$TLSWORK/build.log" ]; then
    bad "代理编译有诊断输出（警告也算）"; sed 's/^/        /' "$TLSWORK/build.log"
else
    ok "代理编译 0 警告"
fi

# ── ① 配置校验退出码 ──
cat > "$TLSWORK/off.ini" <<'EOF'
mqtt.tls=off
mqtt.host=10.0.0.5
EOF
cat > "$TLSWORK/bad.ini" <<'EOF'
mqtt.tls=psk
mqtt.tls.upstream_host=10.0.0.5
EOF
printf '1a2b3c4d5e6f708192a3b4c5d6e7f809\n' > "$TLSWORK/psk.key"
cat > "$TLSWORK/psk.ini" <<EOF
mqtt.tls=psk
mqtt.tls.local_port=11883
mqtt.tls.upstream_host=127.0.0.1
mqtt.tls.upstream_port=18883
mqtt.tls.psk_id=hmi-01
mqtt.tls.psk_file=$TLSWORK/psk.key
EOF

"$PROXY" --check --config "$TLSWORK/off.ini" >/dev/null 2>&1; eq "off ⇒ 退出码 3（不需要代理）" "$?" "3"
"$PROXY" --check --config "$TLSWORK/bad.ini" >/dev/null 2>&1; eq "缺件 ⇒ 退出码 4（拒绝）" "$?" "4"
"$PROXY" --check --config "$TLSWORK/psk.ini" >/dev/null 2>&1; eq "psk 完整 ⇒ 退出码 0（可用）" "$?" "0"
# 校验**不是事件**：--check 不该落审计
rm -f "$TLSWORK/check_audit.log"
HMI_TLS_PROXY_AUDIT="$TLSWORK/check_audit.log" "$PROXY" --check --config "$TLSWORK/psk.ini" >/dev/null 2>&1
eq "--check 不落审计" "$([ -s "$TLSWORK/check_audit.log" ] && echo yes || echo no)" "no"

# ── ⑤ §4 明文监听只绑回环 ──
HMI_TLS_PROXY_AUDIT="$TLSWORK/bind_audit.log" "$PROXY" --config "$TLSWORK/psk.ini" >/dev/null 2>&1 &
BINDPID=$!
sleep 0.6
BINDLINE=$(ss -tln 2>/dev/null | grep ':11883 ' | head -1)
kill $BINDPID 2>/dev/null; wait $BINDPID 2>/dev/null
wait_port_free 11883 || bad "11883 没能释放（前一个代理没退干净）"
has "监听在 127.0.0.1:11883" "$BINDLINE" "127.0.0.1:11883"
case "$BINDLINE" in
    *0.0.0.0:11883*) bad "监听绑到了 0.0.0.0（契约 §4：比不做 TLS 更危险）" ;;
    *) ok "监听没有绑 0.0.0.0" ;;
esac

# ── §6-3 缺件必须拒启动（配置层判"非空"，这里判"文件真的在"） ──
cat > "$TLSWORK/missing.ini" <<EOF
mqtt.tls=psk
mqtt.tls.local_port=11885
mqtt.tls.upstream_host=127.0.0.1
mqtt.tls.upstream_port=18885
mqtt.tls.psk_id=hmi-01
mqtt.tls.psk_file=$TLSWORK/no-such-psk.key
EOF
rm -f "$TLSWORK/missing_audit.log"
# ⚠ 必须 `timeout` ＋ `--once`：这条的**期望**是立刻退出；一旦哪天它真的起来了
#   （负控实测过：把"文件存在性检查"摘掉之后），前台不加超时就会把整步挂死。
timeout 5 env HMI_TLS_PROXY_AUDIT="$TLSWORK/missing_audit.log" \
    "$PROXY" --config "$TLSWORK/missing.ini" --once >/dev/null 2>&1
eq "§6-3 PSK 密钥文件缺失 ⇒ 退出码 4（拒绝启动）" "$?" "4"
has "§6-3 审计有 psk-file-missing" "$(cat "$TLSWORK/missing_audit.log" 2>/dev/null)" "mqtt-tls-psk-file-missing"

# ── 往返脚手架：设备客户端（明文）↔ 代理 ↔ TLS 假 broker ──
# 用法：roundtrip <ini> <listen_port> <client_payload> <broker_payload> <broker_cmd>
roundtrip() {
    local ini="$1" lport="$2" cpay="$3" bpay="$4" bcmd="$5"
    local d="$TLSWORK/rt"
    rm -rf "$d"; mkdir -p "$d"
    ( sleep 3; printf '%s' "$bpay" 2>/dev/null; sleep 6 ) | eval "$bcmd" > "$d/recv.txt" 2> "$d/broker.err" &
    local bpid=$!
    sleep 0.4
    wait_port_free "$lport" || bad "端口 $lport 没释放"
    HMI_TLS_PROXY_AUDIT="$d/audit.log" "$PROXY" --config "$ini" --once > "$d/proxy.out" 2>&1 &
    local ppid=$!
    sleep 0.4
    ( exec 3<>"/dev/tcp/127.0.0.1/$lport" 2>/dev/null; printf '%s' "$cpay" >&3 2>/dev/null; timeout 6 cat <&3 ) > "$d/client.txt" 2>&1
    wait $ppid 2>/dev/null
    kill $bpid 2>/dev/null; wait $bpid 2>/dev/null
    RT_RECV=$(cat "$d/recv.txt" 2>/dev/null)
    RT_CLIENT=$(cat "$d/client.txt" 2>/dev/null)
    RT_AUDIT=$(cat "$d/audit.log" 2>/dev/null)
}

# ── ② PSK 端到端 ──
roundtrip "$TLSWORK/psk.ini" 11883 'PING-DEVICE' 'PONG-BROKER' \
    "openssl s_server -psk 1a2b3c4d5e6f708192a3b4c5d6e7f809 -psk_identity hmi-01 -nocert -tls1_2 -cipher PSK-AES128-GCM-SHA256 -accept 127.0.0.1:18883 -quiet -naccept 1"
eq "PSK：设备→broker（经 TLS）" "$RT_RECV" "PING-DEVICE"
eq "PSK：broker→设备（经 TLS）" "$RT_CLIENT" "PONG-BROKER"
has "PSK：审计有 proxy-start" "$RT_AUDIT" "mqtt-tls-proxy-start"

# ── ③ X.509 端到端 ──
openssl req -x509 -newkey rsa:2048 -keyout "$TLSWORK/ca.key" -out "$TLSWORK/ca.pem" \
    -days 1 -nodes -subj "/CN=zhishu-test-ca" >/dev/null 2>&1
openssl req -newkey rsa:2048 -keyout "$TLSWORK/srv.key" -out "$TLSWORK/srv.csr" -nodes \
    -subj "/CN=127.0.0.1" -addext "subjectAltName=IP:127.0.0.1" >/dev/null 2>&1
openssl x509 -req -in "$TLSWORK/srv.csr" -CA "$TLSWORK/ca.pem" -CAkey "$TLSWORK/ca.key" \
    -CAcreateserial -out "$TLSWORK/srv.pem" -days 1 -copy_extensions copy >/dev/null 2>&1
cat > "$TLSWORK/ca.ini" <<EOF
mqtt.tls=ca
mqtt.tls.local_port=11884
mqtt.tls.upstream_host=127.0.0.1
mqtt.tls.upstream_port=18884
mqtt.tls.ca_path=$TLSWORK/ca.pem
EOF

roundtrip "$TLSWORK/ca.ini" 11884 'PING-CA' 'PONG-CA' \
    "openssl s_server -cert $TLSWORK/srv.pem -key $TLSWORK/srv.key -accept 127.0.0.1:18884 -quiet -naccept 1"
eq "X.509：设备→broker（经 TLS）" "$RT_RECV" "PING-CA"
eq "X.509：broker→设备（经 TLS）" "$RT_CLIENT" "PONG-CA"

# ── ④ §6-4 未校时：CA 拒绝、PSK 不受影响 ──
FUTURE=$(( $(date +%s) + 86400 ))
FUTURE_PSK_INI="$TLSWORK/psk_future.ini"
cp "$TLSWORK/psk.ini" "$FUTURE_PSK_INI"
HMI_TLS_PROXY_MIN_EPOCH=$FUTURE roundtrip "$TLSWORK/ca.ini" 11884 'PING-CA' 'PONG-CA' \
    "openssl s_server -cert $TLSWORK/srv.pem -key $TLSWORK/srv.key -accept 127.0.0.1:18884 -quiet -naccept 1"
eq "§6-4 CA ＋ 时钟未同步 ⇒ 客户端收不到任何东西" "$RT_CLIENT" ""
has "§6-4 审计有 clock-unsynced" "$RT_AUDIT" "mqtt-tls-clock-unsynced"

HMI_TLS_PROXY_MIN_EPOCH=$FUTURE roundtrip "$TLSWORK/psk.ini" 11883 'PING-DEVICE' 'PONG-PSK' \
    "openssl s_server -psk 1a2b3c4d5e6f708192a3b4c5d6e7f809 -psk_identity hmi-01 -nocert -tls1_2 -cipher PSK-AES128-GCM-SHA256 -accept 127.0.0.1:18883 -quiet -naccept 1"
eq "§6-4 PSK ＋ 同一条件 ⇒ 不受影响" "$RT_CLIENT" "PONG-PSK"

# ── ⑥ §6-2 上游不可达：绝不降级为明文 ──
D="$TLSWORK/noup"
rm -rf "$D"; mkdir -p "$D"
HMI_TLS_PROXY_AUDIT="$D/audit.log" "$PROXY" --config "$TLSWORK/psk.ini" --once > "$D/proxy.out" 2>&1 &
PPID2=$!
sleep 0.4
( exec 3<>/dev/tcp/127.0.0.1/11883 2>/dev/null; printf 'PING' >&3 2>/dev/null; timeout 3 cat <&3 ) > "$D/client.txt" 2>&1
wait $PPID2 2>/dev/null
eq "§6-2 上游不可达 ⇒ 客户端收不到任何东西（不降级）" "$(cat "$D/client.txt" 2>/dev/null)" ""
has "§6-2 审计有 upstream-unreachable" "$(cat "$D/audit.log" 2>/dev/null)" "mqtt-tls-upstream-unreachable"

# ── §322 凭据热加载（SIGHUP）：合法重载生效、非法重载被拒，且**都不掉监听** ──────────
# 为什么单独一条：热加载的价值全在"不掉监听、不降级"上 —— 只审计出一行 reload 而进程
# 其实退出了/端口没了，等于没做。
d="$TLSWORK/reload"; mkdir -p "$d"
wait_port_free 11883
HMI_TLS_PROXY_AUDIT="$d/audit.log" "$PROXY" --config "$TLSWORK/psk.ini" > "$d/proxy.out" 2>&1 &
rpid=$!
for i in $(seq 1 60); do grep -q "mqtt-tls-proxy-start" "$d/audit.log" 2>/dev/null && break; sleep 0.1; done
grep -q "mqtt-tls-proxy-start" "$d/audit.log" 2>/dev/null \
    && ok "§322 代理已起（监听就绪）" \
    || { bad "§322 代理没起来，后续断言无意义"; sed 's/^/        /' "$d/proxy.out" | head -5; }

# ① 合法重载 ⇒ 审计 credentials-reloaded，且监听还在
kill -HUP $rpid 2>/dev/null
for i in $(seq 1 60); do grep -q "mqtt-tls-credentials-reloaded" "$d/audit.log" 2>/dev/null && break; sleep 0.1; done
grep -q "mqtt-tls-credentials-reloaded" "$d/audit.log" 2>/dev/null && kill -0 $rpid 2>/dev/null \
    && ok "§322 SIGHUP（合法）⇒ 审计 credentials-reloaded 且监听未掉" \
    || bad "§322 SIGHUP（合法）没生效或进程掉了"

# ② 非法重载（配置被改成 mqtt.tls=off）⇒ **拒绝**重载，留痕且继续跑
cp "$TLSWORK/psk.ini" "$d/psk.ini.good"
printf 'mqtt.tls=off\n' >> "$TLSWORK/psk.ini"
kill -HUP $rpid 2>/dev/null
for i in $(seq 1 60); do grep -q "mqtt-tls-reload-failed" "$d/audit.log" 2>/dev/null && break; sleep 0.1; done
grep -q "mqtt-tls-reload-failed" "$d/audit.log" 2>/dev/null && kill -0 $rpid 2>/dev/null \
    && ok "§322 SIGHUP（非法：off）⇒ 拒绝重载、审计 reload-failed、进程仍在（不降级）" \
    || bad "§322 SIGHUP（非法）没被拒绝，或进程掉了 —— 违反永不降级"

kill $rpid 2>/dev/null; wait $rpid 2>/dev/null
cp "$d/psk.ini.good" "$TLSWORK/psk.ini" 2>/dev/null || true
wait_port_free 11883 || bad "§322 收尾时端口没释放"


# ── ⑧ §332 一次 TLS 往返的**三态**结果：ok / reject / broken ─────────────────────────
# 为什么必须有它：客户端"收到 []"至少有三种含义 ——（a）代理拒绝了握手；（b）broker 没起来；
# （c）转发没回来。把它们混在一起，就会出现"负控不咬却全绿"的假绿（阶段 330 的教训）。
# 三态定义：ok = 拿到期望应答；reject = 没拿到应答**且审计有握手失败证据**；broken = 其余（不可采信）。
tls_probe() {   # $1=ini $2=lport $3=bport $4=broker命令（空=不起）$5=期望应答
    local ini="$1" lport="$2" bport="$3" bcmd="$4" want="$5"
    local d="$TLSWORK/probe_$2"; rm -rf "$d"; mkdir -p "$d"
    PROBE_BROKER_DOWN=0; PROBE_LISTEN_FAIL=0
    if [ -n "$bcmd" ]; then
        ( sleep 1; printf '%s' "$want"; sleep 6 ) | eval "$bcmd" >"$d/broker.out" 2>&1 &
        PROBE_BPID=$!
        wait_port_open "$bport" || PROBE_BROKER_DOWN=1
    else
        PROBE_BROKER_DOWN=1
    fi
    wait_port_free "$lport"
    HMI_TLS_PROXY_AUDIT="$d/audit.log" "$PROXY" --config "$ini" --once >"$d/proxy.out" 2>&1 &
    local ppid=$!
    if ! wait_port_open "$lport"; then PROBE_LISTEN_FAIL=1; fi
    sleep 0.2
    ( exec 3<>"/dev/tcp/127.0.0.1/$lport" 2>/dev/null; printf 'PING' >&3 2>/dev/null; timeout 6 cat <&3 ) >"$d/client.txt" 2>&1
    wait $ppid 2>/dev/null; PROBE_RC=$?
    [ -n "${PROBE_BPID:-}" ] && { kill $PROBE_BPID 2>/dev/null; wait $PROBE_BPID 2>/dev/null; }
    PROBE_CLIENT=$(cat "$d/client.txt" 2>/dev/null)
    PROBE_AUDIT=$(cat "$d/audit.log" 2>/dev/null)
    if [ "$PROBE_BROKER_DOWN" = "1" ] || [ "$PROBE_LISTEN_FAIL" = "1" ] || [ "${PROBE_RC:-0}" -ne 0 ]; then
        PROBE_STATE=broken
    elif [ "$PROBE_CLIENT" = "$want" ]; then
        PROBE_STATE=ok
    elif printf '%s' "$PROBE_AUDIT" | grep -qE "mqtt-tls-handshake-verify|mqtt-tls-handshake-failed"; then
        PROBE_STATE=reject
    else
        PROBE_STATE=broken
    fi
}

cat > "$TLSWORK/dead.ini" <<EOF
mqtt.tls=ca
mqtt.tls.local_port=11887
mqtt.tls.upstream_host=127.0.0.1
mqtt.tls.upstream_port=18887
mqtt.tls.ca_path=$TLSWORK/ca.pem
EOF
# 正控：正常 ca 场景必须 ok（否则探针本身不成立）
cat > "$TLSWORK/probe_ok.ini" <<EOF
mqtt.tls=ca
mqtt.tls.local_port=11888
mqtt.tls.upstream_host=127.0.0.1
mqtt.tls.upstream_port=18888
mqtt.tls.ca_path=$TLSWORK/ca.pem
EOF
PROBE_OK_N=0
for i in 1 2 3; do
    tls_probe "$TLSWORK/probe_ok.ini" 11888 18888 \
        "openssl s_server -cert $TLSWORK/srv.pem -key $TLSWORK/srv.key -accept 127.0.0.1:18888 -quiet -naccept 1" 'PONG-CA'
    [ "$PROBE_STATE" = "ok" ] && PROBE_OK_N=$((PROBE_OK_N + 1))
done
eq "§332 三态正控：正常 ca 场景连跑 3 次都 ok（可复现）" "$PROBE_OK_N" "3"

# 负控：broker 不启动 ⇒ 必须是 broken，**不许**被当成 reject
tls_probe "$TLSWORK/dead.ini" 11887 18887 "" 'PONG-CA'
eq "§332 broken 可识别：broker 没起来 ⇒ broken（不是 reject，也不是 ok）" "$PROBE_STATE" "broken"


echo "=== 结果: $PASS 通过, $FAIL 失败 ==="
[ "$FAIL" -eq 0 ]
