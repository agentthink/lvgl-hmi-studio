# 本机 TLS 终止代理契约（阶段 284）—— `C4=②` 的落地形态

> 上游：`docs/2026-10-01-device-tls-assessment.md`（评估结论：**本机终止代理 ＋ PSK 优先**）。
> 本文把评估的结论写成**可判的契约**：谁连哪里、谁读哪些键、失败怎么留痕、什么绝不允许。
> **本阶段不含代理二进制与 init 脚本** —— 理由见 §7。

---

## 1. 拓扑

```
   ┌──────────────────────── 设备（T113） ────────────────────────┐
   │                                                              │
   │  hmi_runtime（MQTT 客户端，裸 socket）                        │
   │      │  明文 MQTT                                             │
   │      ▼                                                        │
   │  127.0.0.1:<local_port>   ← **只绑回环**（明文绝不进 LAN）      │
   │      │                                                        │
   │  hmi-tls-proxy（本阶段未实现；procd 托管）                     │
   │      │  TLS（psk 优先 / ca 备选）                              │
   └──────┼───────────────────────────────────────────────────────┘
          ▼
     <upstream_host>:<upstream_port>   broker
```

**关键分工**：`mqtt.host`/`mqtt.port` 是**客户端**读的（TLS 开着时 = 回环代理）；
`mqtt.tls.upstream_host`/`upstream_port` 是**代理**读的（= 真 broker）。两侧各读各的，**没有交叠**。

---

## 2. INI 键（唯一来源：`comm_config.ini`）

| 键 | 谁读 | 说明 |
|---|---|---|
| `mqtt.host` / `mqtt.port` | 客户端 | TLS `off` ⇒ broker 地址；TLS `psk`/`ca` ⇒ **`127.0.0.1`** ＋ `local_port` |
| `mqtt.tls` | 两侧 | `off` \| `psk` \| `ca`（缺省 ⇒ `off`，老工程兼容） |
| `mqtt.tls.local_port` | 代理 ＋ 客户端 | 代理监听端口；缺省 `1883` |
| `mqtt.tls.upstream_host` / `upstream_port` | **只给代理** | 真 broker 地址（TLS 开着时才出现） |
| `mqtt.tls.psk_id` / `psk_file` | 代理 | PSK 身份与密钥文件（密钥**不进 ini**，单独文件便于 `chmod 600`） |
| `mqtt.tls.ca_path` | 代理 | X.509 CA 链路径 |

---

## 3. 三条硬规则（**已在 C 与 C# 两侧实现并判**）

| # | 规则 | 实现 | 判据 |
|---|---|---|---|
| **R1** | **端点决定**：`off` ⇒ 直连 broker；`psk`/`ca` ⇒ 连 `127.0.0.1:<local_port>` | C `hmi_mqtt_tls_resolve_endpoint` / C# `TryResolveEndpoint` | 跨语言同源样例（9 条） |
| **R2** | **不许绕过代理**：TLS 开着时客户端 host 必须是回环，否则拒绝 | C `hmi_mqtt_tls_assert_no_bypass` / C# `AssertNoBypass` | 两条独立判据 + 负控 |
| **R3** | **生成期就必须连回环**：TLS 开着时 `mqtt.host` **绝不**写成 broker 地址（broker 只以 `upstream_*` 出现） | `CommConfigCodeGen` | `mqtt.host=127.0.0.1` ＋ `Assert.DoesNotContain("mqtt.host=<broker>")` |

**为什么 R2/R3 要各判一次**：R3 管"**生成的配置**不会绕过"，R2 管"**读配置的人**不会绕过"
（将来代理/客户端实现里凡是从别处拿到 host 的调用点，都要过 R2 这道断言）。

---

## 4. 进程与启动（**待实现，写成要求**）

| 要求 | 为什么 | 将来怎么判 |
|---|---|---|
| procd 托管 ＋ `procd_set_param respawn` | 代理崩了要自己起来；否则 MQTT 静默断（且远程端只看到"设备离线"） | 读 init 脚本断言（与 `S97hmiruntime` 同款写法） |
| 代理**先于** hmi_runtime 可用 | 客户端连不上回环代理会重试，但顺序反了会白刷一屏错误 | init 脚本的 `START=` 序号比对 |
| 明文监听**只绑 `127.0.0.1`** | 绑 `0.0.0.0` 等于把 LAN 的明文 MQTT 暴露出去 —— 这比不做 TLS 更危险 | 脚本/代码里 `listen` 地址断言 |
| 失败**不改变** `mqtt.tls` 取值 | 代理起不来时**不允许**把客户端切去直连 broker（那就是自动降级） | 负控：摘掉代理 ⇒ 客户端仍连回环 |

---

## 5. 失败模式与留痕

| 事件 | 设备侧动作 | 是否落审计 |
|---|---|---|
| 代理启动/停止 | 写 `hmi_audit("mqtt-tls-proxy-start"/"-stop", …)` | ✅ |
| 上游握手失败（证书/PSK/时钟） | 同上，`mqtt-tls-handshake-failed`（**含原因大类，不含密钥**） | ✅ |
| 客户端连不上回环代理 | 客户端自己重试；**不**改连 broker | ✅（节流，避免风暴） |
| 代理配置不全（`psk` 缺件等） | **拒绝启动代理 ＋ 审计**（红线：不降级） | ✅ |

**远程侧注意**：TLS 失败时设备**发不出任何 MQTT 报文** ⇒ 远程端只能通过"心跳超时 ＋ 审计证据"区分
"设备真离线"与"TLS 失败"。运维文档必须写清（评估 §5 已记）。

---

## 6. 判据面（汇总）

| # | 判据 | 现状 |
|---|---|---|
| 1 | C/C# 端点解析同源样例 9 条 | ✅ 本阶段 |
| 2 | 不许绕过（两侧各一条） | ✅ 本阶段 |
| 3 | 生成期 `mqtt.host=127.0.0.1` ＋ 反向断言 | ✅ 本阶段 |
| 4 | 端口解析边界（缺省/0/超界/非数字/空格/NULL） | ✅ 本阶段 |
| 5 | 负控 4 支（见 §8） | ✅ 本阶段 |
| 6 | init 脚本四条要求（respawn/顺序/只绑回环/失败不降级） | ⏳ 代理落地时 |
| 7 | 板子上尺寸/内存实测 | ⏳ 需真板子 |

---

## 7. 为什么本阶段**不**写代理与 init 脚本（如实）

评估里"本路线唯一的新代码"就是代理本身：它要链 OpenSSL、要处理 PSK 回调、要管重连与日志。
**现在写 init 脚本会是一份"启动一个不存在的二进制"的脚本 —— 那是假交付。**
所以本阶段把它拆成两半：**决定逻辑**（谁连哪里、什么算绕过、配置怎么读）先落地并判死；
**进程与握手**留到有真实现时一起做（节 4/5/6 已把要求写成可判条目，届时逐条判）。

---

## 8. 负控（4/4，逐支 sha256 还原）

| 支 | 注入 | 打中 |
|---|---|---|
| nc1 | C 侧 TLS 开着也把 broker 交给客户端 | 端点样例表（C） |
| nc2 | C 侧"不许绕过"恒真 | 边界断言（C） |
| nc3 | 生成器 TLS 开着仍写 `mqtt.host=<broker>` | `IniCodegen_EmitsTlsKeysOnlyWhenConfigured` |
| nc4 | C# 端点解析把 TLS 也解析成 broker | `EndpointSamples_MatchTheDeviceHostTestTable` |
