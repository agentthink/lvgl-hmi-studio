# 设备侧 TLS 运维手册（阶段 305）—— 凭据轮换与故障判读

> 上游：`docs/2026-10-01-device-tls-assessment.md`（结论：本机终止代理 ＋ PSK/X.509 **都做**）
> `docs/2026-10-01-local-tls-proxy-contract.md`（拓扑 / 键 / 四条硬规则）
> 用户拍板（2026-10-11，登记册 §0.2）：§7-1 **两者都做**；§7-2 **永不降级**。
>
> 实现：`firmware/t113/app/proxy/tls_proxy.c`（代理）· `firmware/t113/app/comm/comm_core.c`（客户端侧决定）
> `firmware/t113/app/comm/tls_proxy_config.{h,c}`（配置契约）· `.../files/S97hmiruntime`（开机编排）
> 判据：`firmware/t113/tests/check_tls_proxy.sh`（真 TLS 端到端 19 条）· `scripts/check-cross-proxy.sh`
> `tests/HmiEditor.Core.Tests/TlsProxyInitScriptGuardTests.cs` · `ShippedCredentialHygieneTests`

---

## 1. 拓扑与"谁读哪些键"

```
hmi_runtime（裸 socket MQTT）
   │ 明文
   ▼
127.0.0.1:<mqtt.tls.local_port>        ← 只绑回环，明文绝不进 LAN
   │
hmi-tls-proxy（procd 托管，respawn）
   │ TLS（psk 优先 / ca 备选）
   ▼
<mqtt.tls.upstream_host>:<upstream_port>   真 broker
```

| 键（唯一来源 `/mnt/UDISK/hmi/comm_config.ini`） | 谁读 | 说明 |
|---|---|---|
| `mqtt.host` / `mqtt.port` | **客户端** | TLS `off` ⇒ broker 地址；`psk`/`ca` ⇒ `127.0.0.1` ＋ `local_port` |
| `mqtt.tls` | 两侧 | `off` \| `psk` \| `ca`；**缺省 = off**（老工程不带这个键照旧） |
| `mqtt.tls.local_port` | 两侧 | 缺省 `1883` |
| `mqtt.tls.upstream_host` / `upstream_port` | **只给代理** | 真 broker；`upstream_port` 缺省 `8883` |
| `mqtt.tls.psk_id` / `psk_file` | 代理 | 密钥**不进 ini**，单独文件便于 `chmod 600` |
| `mqtt.tls.ca_path` | 代理 | X.509 CA 链路径 |

**凭据热加载（阶段 322 起已支持）**：`kill -HUP $(pidof hmi-tls-proxy)` ⇒ 代理重读配置与凭据、
**不掉监听**；合法则审计 `mqtt-tls-credentials-reloaded`，非法（缺件/模式写错/**改成 `off`**）则审计
`mqtt-tls-reload-failed` 并**保留旧凭据继续跑**（绝不降级）。仍可整服务重启：
`/etc/init.d/S97hmiruntime restart`。

---

## 2. 三条红线（违反任何一条，问题都比"没上 TLS"更严重）

1. **永不降级**：握手失败 ⇒ 代理**关掉客户端连接**，绝不改成明文转发；
   也**不会**在代理起不来时把客户端切去直连 broker。只有显式 `mqtt.tls=off` 才走明文。
2. **只绑回环**：明文监听地址在代码里写死 `127.0.0.1`（`HMI_TLS_PROXY_LISTEN_HOST`），**没有任何键能改它**。
3. **缺件拒启动**：`psk` 缺 `psk_id`/`psk_file`、`ca` 缺 `ca_path`、或文件不可读 ⇒
   **退出码 4 ＋ 审计**，MQTT 不启动（不是"退回明文"，也不是"跳过校验"）。

---

## 3. 凭据轮换

### 3.1 PSK

**格式**：密钥文件是**十六进制**串（可含空白/冒号分隔），例如
`printf '1a2b3c4d5e6f708192a3b4c5d6e7f809\n' > /mnt/UDISK/hmi/psk.key`。
非十六进制字符 ⇒ 代理**拒绝**（不猜、不做容错），审计 `mqtt-tls-handshake-failed`。

**轮换（不中断顺序）**：

```sh
# ① broker 侧：**先**把新身份加上（两台设备/两个 id 同时可用）
# ② 设备侧：写入新密钥与身份（权限收紧）
printf '<新密钥hex>' > /mnt/UDISK/hmi/psk.key.new
chmod 600 /mnt/UDISK/hmi/psk.key.new
mv -f /mnt/UDISK/hmi/psk.key.new /mnt/UDISK/hmi/psk.key
sed -i 's/^mqtt\.tls\.psk_id=.*/mqtt.tls.psk_id=<新id>/' /mnt/UDISK/hmi/comm_config.ini
# ③ 重启（凭据只在启动时读）
/etc/init.d/S97hmiruntime restart
# ④ 看审计确认起来了（下面 §4）
grep mqtt-tls /mnt/UDISK/hmi/audit.log | tail -5
# ⑤ broker 侧：确认设备已用新 id 连上后，**再**撤掉旧 id
```

**回退**：把旧 `psk.key` / `psk_id` 换回来再重启 —— **不是**把 `mqtt.tls` 改成 `off`。
（真要走明文，那是**显式配置决定**，必须留一条 `mqtt-tls-config-*` 之外的变更记录。）

### 3.2 X.509

**约束：设备没有 RTC**。时间从 `/etc/timestamp` 起步、联网后被 NTP **跳变** ⇒
开机早期证书有效期判定不可靠。代理因此有一条**硬闸门**：

> `ca` 模式下，若系统时间早于 **2020-01-01**（`HMI_TLS_PROXY_MIN_EPOCH`），
> **拒绝握手 ＋ 审计 `mqtt-tls-clock-unsynced`**；`psk` 路径**不受影响**。

⇒ **`ca` 模式的部署前提是"开机必须先校时"**（镜像里有 `curl`，可 `curl -sI https://…` 取 `Date` 头，
或 `ntpdate`）。**不要**为了让它连上而放宽这个闸门 —— 那是把校验变成建议。

**轮换**：

```sh
# ① 新 CA 链（或同一 CA 新签的服务端证书）下发到设备
printf '<新 ca 链 PEM>' > /mnt/UDISK/hmi/ca.pem.new
mv -f /mnt/UDISK/hmi/ca.pem.new /mnt/UDISK/hmi/ca.pem
# ② 重启（同时确认系统时间已同步）
/etc/init.d/S97hmiruntime restart
date -u                       # 必须已是当前时间；仍是 1970/2019 ⇒ 先校时
grep -E "mqtt-tls-(proxy-start|handshake-failed|clock-unsynced)" /mnt/UDISK/hmi/audit.log | tail -5
```

**证书里必须有设备的对端标识**：代理用 `X509_VERIFY_PARAM_set1_host(upstream_host)` 校验
**主机名/IP** —— 用 IP 连就得有 `subjectAltName = IP:<addr>`（本仓的主机侧端到端判据就是这么签的）。

---

## 4. 审计事件表（判读的第一手材料）

落点 `/mnt/UDISK/hmi/audit.log`（`HMI_SEC_AUDIT_PATH`），行形状与 `hmi_audit` **同款**：

```
<ISO8601Z> <event> user= role= <detail>
```

| 事件 | 谁写 | 含义 | 处置 |
|---|---|---|---|
| `mqtt-tls-proxy-start` | 代理 | 代理起来开始监听（`detail` = `psk`/`ca`） | 正常 |
| `mqtt-tls-proxy-stop` | 代理 | 代理收到 SIGTERM/SIGINT 正常退出 | 若**频繁**出现 ⇒ 看是否被谁 kill、或配置/凭据问题导致起不来后被反复拉起 |
| `mqtt-tls-config-refused` | 代理**或**客户端 | 配置非法（`detail` = `tls-mode-unknown`/`tls-config-incomplete`/`tls-endpoint-invalid`/`tls-bypass`） | 按 `detail` 改 ini；**不要**改成 `off` 来"先连上" |
| `mqtt-tls-clock-unsynced` | 代理 | `ca` 模式且系统时间早于闸门 | **先校时**再重启；PSK 模式不会有这条 |
| `mqtt-tls-psk-file-missing` / `mqtt-tls-ca-file-missing` | 代理 | 凭据文件不存在/不可读 | 下发文件 ＋ `chmod 600` |
| `mqtt-tls-upstream-unreachable` | 代理 | 连不上 broker（网络/DNS/端口） | 查网络与 broker；**设备仍在监听**，恢复后自动重连 |
| `mqtt-tls-handshake-failed` | 代理 | TLS 握手失败（`detail` = `psk`/`ca`/`ca-load`/`upstream-write`） | 凭据不匹配、CA 链不对、对端标识不符、或时钟问题 |
| `mqtt-tls-credentials-reloaded` | 代理 | SIGHUP 重载成功（`detail` = `psk`/`ca`） | 正常：轮换完成、**监听未断** |
| `mqtt-tls-reload-failed` | 代理 | SIGHUP 重载被拒（`detail` = 原因，含 `tls-proxy-not-needed-refused`） | 看清原因再改；**旧凭据仍在用**，不是掉线 |
| `mqtt-tls-proxy-listen-failed` | 代理 | `bind` 失败（端口被占/地址不可用） | 查是否有残留代理进程与 `local_port` 冲突 |

**审计里不会有密钥材料**：`detail` 全是原因标识；这条本身有判据守着
（`check_tls_proxy.sh` 断言"审计不含密钥/身份材料"）。

---

## 5. ⚠ 运维判读：**TLS 失败时，设备一个 MQTT 报文都发不出去**

这是本方案最容易误判的一点：**TLS 失败 ⇒ 没有 MQTT 连接 ⇒ 连 LWT（遗嘱）都发不出去**。
所以远程端**看不到** `on=0`，只会看到"设备没了"。**只靠 MQTT 侧区分不出下列两种**：

| 现象（远程视角） | 可能原因 A | 可能原因 B |
|---|---|---|
| 心跳超时、`<pre>/status` 停在 `on=1`、没有 `on=0` | **设备真离线**（断电/断网） | **TLS 失败**（设备在线、HMI 正常，只是连不上 broker） |

**唯一可靠的区分手段是设备侧证据**（审计 ＋ 代理日志）：

```sh
# 设备在线（SSH/adb 通）⇒ 直接看
tail -20 /mnt/UDISK/hmi/audit.log
tail -20 /mnt/UDISK/hmi_tls_proxy.log          # 代理 stderr（init 脚本重定向到这里）
pidof hmi-tls-proxy && pidof hmi_runtime       # 两个都在 ⇒ 是"连不上"不是"设备死了"
```

判读表：

| 审计里看到 | 结论 | 下一步 |
|---|---|---|
| 最近有 `mqtt-tls-proxy-start`，之后 `mqtt-tls-upstream-unreachable` 反复出现 | 设备在线、网络或 broker 不可达 | 查网络/broker；设备侧无需改配置 |
| `mqtt-tls-handshake-failed`（`psk`/`ca`） | 凭据/CA/对端标识不匹配 | 走 §3 轮换流程核对 |
| `mqtt-tls-clock-unsynced` | `ca` 模式且未校时 | **先校时**再重启 |
| `mqtt-tls-config-refused` | 配置缺件/写错 | 修 ini；**不许**改 `off` 绕过 |
| **一条 `mqtt-tls-*` 都没有**（且 `/mnt/UDISK/hmi/audit.log` 在更新） | 代理**根本没起来** | 看 init 脚本输出与 `ping`/`pidof`；确认 `mqtt.tls` 不是 `off` |
| 审计文件时间戳停在很久以前 | 设备真离线（断电/系统没起来） | 现场处置 |

**给远程运维的最小动作**：把"设备心跳超时"一律当作**需要现场/带外确认**的事件，
不要直接判定为"设备故障"—— 先问一句"设备 SSH 通不通、审计最后一条是什么"。

---

## 6. 与本仓判据的对应（哪些性质被机械守着）

| 性质 | 守着它的判据 |
|---|---|
| 端点决定 / 不许绕过（两侧同源） | `MqttTlsContractTests`（C#）＋ `hmi_comm_test.c`（C）的样例表 |
| 缺件拒启动 ＋ 审计 ＋ fail-closed | `hmi_comm_test.c` 的 `test_comm_tls_wiring`（19 条） |
| 只绑回环 | `check_tls_proxy.sh` 的 `ss` 实测 ＋ `TlsProxyInitScriptGuardTests`（脚本/源码不得含 `0.0.0.0`） |
| 永不降级（§6-2） | `check_tls_proxy.sh`："上游不可达 ⇒ 客户端零字节" |
| 未校时（§6-4） | `check_tls_proxy.sh`：CA 拒 ＋ 审计；PSK 不受影响 |
| init 四条（§6-6） | `TlsProxyInitScriptGuardTests` |
| 审计不含密钥材料 | `check_tls_proxy.sh` 的断言 |
| 出厂默认口令有"部署必改"标注 | `ShippedCredentialHygieneTests` |
| 设备工具链上编得过（1.1.0i） | `scripts/check-cross-proxy.sh`（三态；不进 `ci-gate`，因 SDK 不在仓库里） |

## 7. 仍未做（如实）

- **板子上内存（VmRSS）实测**：尺寸已有设备侧读数（代理剥离后 **12,676 B**；镜像 `libssl` **348 KB**
  ＋ `libcrypto` **1.69 MB**），内存那半边要真板。
- ~~凭据热加载（SIGHUP）未做~~ ⇒ **阶段 322 已支持**（见 §1 与审计事件表；`off` 会被**拒绝**重载）。
- **证书吊销（CRL/OCSP）未实现 —— 这是实测结论，不是「没来得及做」**：
  尝试过 `mqtt.tls.crl_path` ＋ `X509_STORE_add_crl` ＋ `X509_V_FLAG_CRL_CHECK[_ALL]`，
  但判据给出**相反证据**：审计里出现 `mqtt-tls-peer-cert serial=1000`（**握手成功**），
  而该证书**确实被同一 CA 的 CRL 吊销**（`openssl verify -crl_check` 独立确认会吊销）。
  ⇒ **代理目前不强制执行吊销**；撤销仍只能靠「换 CA / 换身份」（见 §3.2）。
  实现与判据的尝试过程、以及「判据夹具本身不可复现」的问题，见
  `docs/2026-10-11-unfinished-plan-ledger.md` 附录 B（含六轮实测记录）。
