# 未完成开发计划台账（2026-10-11）

> **用途**：把散在总纲与各轮计划里的**未完成项**收成一张横向台账，每项给一条**可复跑的只读核对命令**。
> **权威口径不变**：单个未闭环项的权威状态仍是 `docs/2026-09-17-open-items-register.md`
> （裸号 `#X` = 该册 §4；`§N-#X` = 带节前缀条目）。本表是**跨计划汇总 ＋ 核对命令**，不替代它。
> **基线**：阶段 288（`§204`，2026-10-09）；登记册内**无**「阶段 289+」。
> **生成方式**：三路只读复核（登记册 §0~§5 逐行取**最后一条**状态附记；2026-10 立项方案的里程碑；
> 08-19 / 09-13 旧计划代码核验）＋ 本表命令**逐条由维护者复跑**。
> **只读声明**：全部命令是 grep / ls / sed / git，默认 `cd` 到仓库根；**本表不跑** build / test / 门禁 / `verify.sh`
> —— 那属另一类证据（见 §13 诚实边界）。

约定：`期望` 是命令的预期读数；读数不符时**以现场为准**，并回来改本表。

---

## 0. 摘要

| 类别 | 数量 | 卡在什么 |
|---|---:|---|
| §1 发布门槛（没做不发布） | 3 | 人（母语校对）· 实机（LVGL 回归）· 工程（间歇红 + `416` 定位） |
| §2 已立项计划后半程 | 4 个计划 / 约 20 子项 | 工程 · 外部（broker / Web 框架）· 硬件 · 用户数值 |
| §3 需产品决策 | 7 项 | 用户拍板（5 条 `§2-#1`~`#5` ＋ 归档数值 ＋ 抑制是否下发） |
| §4 需先立功能 | 10 | 先立机制 / 协议变更 / 重构 |
| §5 需硬件 / 实机 | 9 | 板子 · 外设 · Windows 环境 |
| §6 工程化与卫生 | 19（含 1 条**明文口令**） | 工程 |
| §7 旧计划（08-19 / 09-13）残余 | 8 项 | 已并入 §4~§6，此处单独留档来源 |
| §8 环境 / 本地残留 | 5 | 环境事实，非仓库缺陷 |
| §9 未入册的最新工作流 | 1 条 | 固件 bring-up（Oct 9–11，14 提交） |

**一句话**：没有里程碑级烂尾；未完成集中在 **① 需人/需实机 · ② 已立项计划的后半程 · ③ 需拍板/先立功能**，
外加**未写进登记册的固件 bring-up 收尾**。

---

## 1. 发布门槛（登记册 §0.1，阶段 261 用户拍板：没做就不发布）

| id | 状态 | 未完成的（人工/实机那半） | 卡在 |
|---|---|---|---|
| `#27` | 部分闭环 ⇒ 门槛 | en-US **母语者逐字校对**（语气/大小写/缩写地道性）。产物是 127 条偏长 ＋ 38 条同值清单 | 人 |
| `#28` | 仍开着 ⇒ 门槛 | LVGL v8→v9 **实机回归** ＋ 运行时 v8 遗留写法**人工核对** | 实机 + 人 |
| `#47` `#47b` | ✅ `#47b` 已定位；`#47` 的**一条可复现根因已修**（阶段 295） | ✅ `416` 产出字段＝`FontManager._fontCollections[<FontCollection>]._glyphTypefaceCache`（按族名键；新族 +1、同族不涨）。✅ 阶段 294：预算判据与隔离级别解耦（2,274 → 3,938 → **2,274**）。✅ 阶段 295：**根因＝静态初始化器里 `new SolidColorBrush` 会抢走 `Dispatcher.s_uiThread`**（探针实测 `null → 调用线程`；`Immutable*` 不抢）⇒ `src/` 9 处改用 `ImmutableSolidColorBrush` ＋ 行为/静态两面判据 ＋ 负控 3/3 ＋ **连续 3 轮门禁 0 红**。⛔ 「切 `PerAssembly`」已实测排除（108 红/102 条闸门自锁）。**剩余**：普查表里那几类入口**已逐条验证完（阶段 297）** —— `AvaloniaSynchronizationContext` 构造 · `new DispatcherTimer` · `MediaContext.Instance` **实测都会抢**（仓库侧零/仅 UI 线程调用点），`GestureRecognizers`/`TransformTrackingHelper` 本版本**不存在**（销案）；判据 `AmbientEntryPointInventoryTests`（静态面强制 ＋ 机制面单跑诊断）。✅ 阶段 298：`Dispatcher.UIThread` 也进机械面 —— 新增 `UiTestHost.UIThread`（派发体内唯一合法入口，自带运行期断言），19 处真实代码用法改走它；静态面（剥注释＋掩字符串）要求**直接引用 0 命中**，宿主**只许一处**。**剩余（很窄）**：静态面只抓**直接引用**，间接访问（反射取 getter／第三方库在派发外碰平台）抓不到，只能靠机制面单跑诊断与现象兜 | 工程（很窄，低优先） |

```bash
awk '/^### 0\.1 发布前必须做/,/^## 1\./' docs/2026-09-17-open-items-register.md   # §0.1 门槛表 + §0.2 拍板记录
sed -n '4p;85p' docs/2026-10-01-en-us-mechanical-audit.md     # 自述「需要母语级判断，我不冒充」
grep -rn "class DispatcherOwnershipContractTests" tests/      # #47/#47b 的哨兵所在
```

---

## 2. 已立项计划的后半程

### 2.1 远程访问 / Web 部署 `§2-#12`（方案：`docs/2026-10-01-remote-access-proposal.md` §5）

| 里程碑 | 状态 | 剩余工作 | 卡在 |
|---|---|---|---|
| M1 主题/payload 契约 ＋ 两端解析器 | ✅ 阶段 264 | — | — |
| M2 报警/状态上行（+ LWT） | ✅ **已落地（阶段 290 状态/LWT ＋ 291 报警）** | ✅ LWT 注册 · 上线 `<pre>/status` `on=1`（retain）· **报警上行** `<pre>/alarm/<id>` 的 `st=`/`sev=`（ABI `alarm_snapshot` ＋ 生成器发射 ＋ 宿主**边沿触发**发布器，与 `alarm_evaluate` 同线程）· 假 broker 端到端 **35** 条（驱动产品代码）。**剩余（非本里程碑）**：`hmi_alarm_source.c` 胶水无主机判据（需板子实测）· broker 侧 ACL 配置物 · M3 Web UI | 外部（broker/Web） |
| M3 远程只读端（Web） | ❌ 未开工 | Web 端（MQTT over WebSocket）· 只读展示 · 已搁置报警展示「已搁置（至 …）」· 写操作确认/审计界面 | Web 框架 ＋ 运行时 MQTT 客户端（编辑器侧只有设计期驱动）＋ broker |
| M4 TLS/ACL/认证 | 🔶 部分 | 代理**二进制** ＋ init 脚本 · broker 侧 **ACL 配置物** · broker 认证配置 · 板子尺寸/内存实测 · X.509「未校时拒绝」路径 | 工程 / 硬件 / 外部 |
| M5 双向写 ＋ 审计 | 🔶 部分（**接线已落地，阶段 289**） | ✅ 阶段 289：`mqtt_cmd_wiring.{h,c}` 把 `/cmd/` 订阅 → `hmi_cmd_handle` 接通（元数据从 `tag_meta_t` 按值取、权限掩码逐位问 `hmi_has_permission`、审计接 `hmi_audit`），并补了「主题 target ≠ 载荷 target」一致性拒绝；设备产物 `tags.{h,c}` 与生成器的量程契约同步 ＋ 新增漂移守卫。**剩余**：broker 侧 ACL 配置物 · M3 Web UI · 远程写审计**落盘**（`io->audit` → `hmi_audit` 已通，设备审计文件路径实测待板子） | 外部 / Web |
| M6 现场试点（单站点） | ❌ 未做 | —— | M1–M5 ＋ 现场 |

```bash
# M2/M5 的关键读数：app 内**调用点**，而非定义
grep -rn "hmi_cmd_handle" firmware/t113/app/                  # 期望：仅 .h 注释/声明 + .c 定义 ⇒ 调用点 0
grep -rn "hmi_mqtt_format_alarm_topic\|hmi_mqtt_format_status_topic" firmware/t113/app/   # 期望：仅 .h/.c 定义
grep -rn "listen(\|socket(" firmware/t113/tests/ ; echo "exit=$?"      # 期望：无命中（无假 broker）
# M3/M4
grep -rIn --exclude-dir=bin --exclude-dir=obj "websocket\|WebSocket\|MqttRuntimeDriver" src/ ; echo "exit=$?"   # 期望：无
ls firmware/t113/app/comm/ | grep -i proxy ; echo "exit=$?"            # 期望：无（无代理实现）
ls firmware/t113/tina/ | grep -iE "tls|proxy" ; echo "exit=$?"         # 期望：无（无代理 init 脚本）
grep -rIln "acl_file\|topic read\|topic write" . --include=*.conf --include=*.acl ; echo "exit=$?"   # 期望：无（无 broker ACL 配置物）
```

### 2.2 ISA-18.2 报警模型 `§2-#11` ＋ 搁置契约（`docs/2026-10-01-alarm-shelving-contract.md`）

| 切片 | 状态 | 剩余工作 | 卡在 |
|---|---|---|---|
| ㊀ 职责字段（原因/响应/后果）＋ 主报警表 | ✅ 阶段 266（＋完备率 KPI 阶段 271） | — | — |
| ㊁ 优先级与升级 | 🔶 优先级面**已收口（阶段 292）**；升级仍缺 | ✅ 阶段 292：`AllowedResponseMs` **面板输入框**（空=用严重度默认）＋ 报警列表按优先级**排序/着色**（`P1…P4`，颜色按名次复用色板；**只排显示、不动模型**）。**剩余**：③ **升级(escalation)本体**未实现（`grep -rIn escalat src/` ⇒ 0）—— 需先立模型与设备契约 | 工程 |
| ㈢ 搁置/抑制 ＋ KPI | 🔶 两侧主体完成（阶段 268/273–280） | ① 设备侧搁置的**宿主调用点**（设备 UI 按钮 ＋ `by=hmi_current_user()` 来源 —— 设备 UI **不在本仓**）② 运行期抑制**是否下发设备**（未拍板）③ 配置级「设计抑制」④ M3 远程展示「已搁置（至…）」 | 外部设备 UI / 拍板 / 工程 |
| 搁置契约 §10-③ 远程端展示 | ❌ 未做 | 随 M3 | M3 |

```bash
grep -rIn --include=*.cs --include=*.axaml "AllowedResponseMs" src/HmiEditor.App/   # 面板有消费（阶段 292 起）
grep -rIn --include=*.cs "AlarmPriority" src/HmiEditor.App/ | wc -l                 # > 0（列表排序/着色，阶段 292）
grep -rIn "escalat" src/ | wc -l                                                    # 0（升级本体仍缺）
awk '/^## 185 /,/^## 197 /' docs/2026-09-17-open-items-register.md | grep -o "阶段 [0-9]*" | sort -u   # ㈢ 已落地阶段
```

### 2.3 设备侧 TLS ＋ 本机终止代理（`device-tls-assessment.md` / `local-tls-proxy-contract.md`）

| 项 | 状态 | 剩余工作 | 卡在 |
|---|---|---|---|
| 评估本体（A/A′/B/C 四条路线） | ✅ 阶段 281 | — | — |
| §6-1 配置契约（`mqtt.tls=off\|psk\|ca`＋同源样例） | ✅ 阶段 282–284 | — | — |
| §6-2 不许降级：契约级 ✅ / **真实注入** | 🔶 | 经真实客户端/代理的注入测试 | 代理二进制 |
| §6-3 缺证书必须拒启动：校验 ✅ / **运行期** | 🔶 → **设备侧已接线（阶段 300）** | 缺件 ⇒ MQTT 不启动 ＋ 审计一条（**已在 `comm_core.c` 判**）；仍缺"经代理"那一半 | 代理二进制 |
| §6-4 未校时行为（X.509 拒 / PSK 不受影响） | ❌ | 需代理 X.509 路径 | 工程 |
| §6-5 板子上尺寸/内存实测 | 🔶 **尺寸半边已有设备侧读数（阶段 304）** | ✅ 交叉编译读数：代理剥离后 **12,676 B**；镜像 `libssl` **348 KB** ＋ `libcrypto` **1.69 MB**。**剩余**：内存（VmRSS）需真板 | 硬件 |
| 路线 B 代理二进制 | 🔶 **已落地（阶段 302）＋ 设备工具链验证（阶段 304）** | 主机侧真 TLS 端到端 19 条断言 ＋ 负控 5/5；对 `OpenSSL 1.1.0i-fips` 交叉编译 0 警告（`scripts/check-cross-proxy.sh`）；**剩余**：init 脚本已落地（303）· 板子**内存**实测 | 硬件 |
| 代理契约 R1/R2/R3 | ✅ 阶段 284 | — | — |
| **设备侧接线**（读 `mqtt.tls*` → 决定 → 拒启动＋审计） | ✅ **阶段 300** | 发现并补上"契约落地但设备不读键"的闭环缺口 | — |
| 代理契约 §6-6/§6-7/§4·§5 进程与失败留痕 | 🔶 **§6-6 init 四条已落地（阶段 303）**；§6-7 需板子 | 判据 `TlsProxyInitScriptGuardTests`（负控 7/7）；**剩余**：板子实测 | 硬件 |
| 评估 §7-1 PSK 先行 vs X.509 先行 | ✅ **已拍板：两者都做**（阶段 300） | — | — |
| 评估 §7-2 是否允许「显式降级」 | ✅ **已拍板：永不降级**（阶段 300） | — | — |

```bash
grep -n "§6-5\|§6-6\|§6-7\|⏳\|待真板\|需真板" docs/2026-10-01-device-tls-assessment.md docs/2026-10-01-local-tls-proxy-contract.md
grep -n "PSK 先行\|X.509 先行\|显式降级" docs/2026-10-01-device-tls-assessment.md
```

### 2.4 历史归档 `§2-#10`（`docs/2026-10-01-historian-capacity-worksheet.md`）

| 项 | 状态 | 剩余工作 | 卡在 |
|---|---|---|---|
| 量化表（公式 ＋ 三档典型值） | ✅ 阶段 261 | — | — |
| §5 现场参数 | ❌ | `N · T · D · 卡容量/等级 · CoV允许/不允许 · 掉电=可丢最后N秒/一条不许丢` —— 已有参数**名字**、**无数值** | **用户** |
| 选型定档（①现状 / ②SD 轮转 / ③专用归档） | ❌ | 等 §5 填完 | 用户 |
| §6-1 容量估算纯函数 ＋ 表驱动单测 | ❌ | —— | 工程 |
| §6-2 轮转策略判据（删最旧 / 跨切片有序） | ❌ | —— | 工程 |
| §6-3 掉电安全写入协议 ＋ 注入中断负控 | ❌ | —— | 工程 |
| §6-4 `HistoryStoragePath` 移出「未下发」 | ❌ | 仍列在字段账「未下发」 | 工程 |

```bash
sed -n '73,97p' docs/2026-10-01-historian-capacity-worksheet.md      # §5 待填参数 + §6 将来判据面
grep -rn "HistoryStoragePath" src/ --include=*.cs                    # 期望：仍出现在 RuntimeConfig / 字段账
```

---

## 3. 需**产品决策**（登记册 §2，阶段 231 复核「仍成立」）

| id | 项 | 需要你定什么 | 核对 |
|---|---|---|---|
| `§2-#1` | `TemplateGenerationSchema.category` 7 项 enum 硬约束自由字段 | 去掉 enum 会改模型输出分布；不去掉则「锅炉控制模板」被迫选不贴切分类 | schema `:9-12` vs 自由字符串模型 |
| `§2-#2` | `BatchIconGenerator` 取消后是否保留已生成条目 | 「半成品留不留」；出口可行（`ProgressChanged` 已逐条报进度） | `AiIconDialog.axaml.cs:298-303` 自写「需 partial-results 出口」 |
| `§2-#3` | `ScanIntervalMs` 与 `DefaultScanIntervalMs` 语义重复 | 哪个面板权威、另一个删还是留 | 设备侧读前者；后者账为 `StoredOnly` |
| `§2-#4` | `EnableHmiReboot` 面板是否标注「不生效」 | 标注口径（补执行路径 vs 标未生效） | `ChkEnableReboot` 无任何提示 |
| `§2-#5` | 旧目录 `%APPDATA%/HmiEditor` 是否清理 | 删用户数据不可逆 | `AppPaths.cs:78-115` 只读保留、从不清理 |
| `§2-#10` | 历史归档选型 | 见 §2.4 的现场参数**数值** | 量化表 |
| 抑制 | 运行期抑制**是否下发设备** | §196.4 未拍板 | 登记册 §196.4 |

```bash
grep -n "\"enum\"" -A4 src/HmiEditor.AI/Inference/PromptTemplates/TemplateGenerationSchema.json
grep -n "ChkEnableReboot" src/HmiEditor.App/Views/ProjectSettingsView.axaml
grep -n "LegacyAppFolderName\|LegacyDataRoot" src/HmiEditor.Core/Services/AppPaths.cs
grep -n "Shelf\|抑制" docs/2026-09-17-open-items-register.md | sed -n '1,5p'
```

### 3.1 已拍板、**不要重复问**（阶段 270/271）

`A1=②` 搁置设备侧也做 · `A2A3=权限＋审计＋硬上限` · `B=沿用 M1 形状` · `C2=③全可写` ·
`C1=①Web` · `C3=③先局域网云后置` · `C4=②设备侧终结 TLS` · `D1=③可空＋KPI 完备率` · `D2=优先级 1=最高`。
⚠ 登记册 `§186.2` 仍把 `D1` 列为待定，已被 `§187.1` 取代。

---

## 4. 需**先立功能**（登记册 §3，阶段 232 复核「仍成立」）

| id | 项 | 要闭环得先做什么 | 核对命令 / 期望 |
|---|---|---|---|
| `§3-#9` | 5 个 `RuntimeConfig` 字段未下发 | 先立统一心跳帧 / 任务池 / 崩溃转储机制 | `grep -rn "HeartbeatIntervalMs\|MaxRuntimeTasks\|EnableCrashDump" firmware/t113/` ⇒ 0 |
| `§3-#10` | `EnableTamperDetection` 已下发、固件零消费 | 先定义「防什么篡改 / 校验对象 / 检出后怎么办」 | `grep -rn "TAMPER" firmware/t113/` ⇒ 0 |
| `§3-#11` | `WatchdogTimeoutMs` / `EnableHmiReboot` 死字段 | 补执行路径；不删是为老工程 JSON 兼容 | `grep -rn "WatchdogTimeoutMs" firmware/t113/` ⇒ 0 |
| `§3-#12` | 并行 `tool_call` | 协议（一次多调用）＋ 调度 ＋ 回填顺序 | `grep -rn "out AgentToolCall" src/HmiEditor.AI/Agent/AgentOutput.cs` ⇒ 单个 |
| `§3-#13` | AI Schema 属性集过通用 | 逐控件族扩 schema ＋ 映射（Tank 液位 / Pipe 流向） | 见下方命令 ⇒ 仅注释命中 |
| `§3-#14` | 极端迟到仍会串帧 | 协议级手段（命令哨兵 / 设备序列号） | `grep -rn "__HMI_DONE_" src/ firmware/t113/` ⇒ 0 |
| `§3-#16` | OPC UA 深度 | Browse 服务 ＋ 节点树 UI；订阅推送与安全策略列第二阶段 | `grep -rn "Browse\|Subscribe" src/Hmi.Communication.Builtin/OpcUa/` ⇒ 0 |
| `§3-#17` | SVG 残余 | 图案/滤镜/嵌入位图/文本仍跳过；真渐变只在轴对齐矩形/圆；**设备侧无实机验证** | `grep -n "图案\|滤镜" src/HmiEditor.Core/Services/SvgParser.cs` |
| `§3-#18` | 过程图形符号族（26 vs 40~80+） | Motor / 泵 / 搅拌器全链（类型→工具箱→渲染→CodeGen→Golden） | 见下方命令 ⇒ 26 个成员 |
| `§3-#19` | 生成链统一发射器 | 抽 `IControlEmitter` 收敛模拟器/设备/导出三套 | `grep -rn "IControlEmitter" src/` ⇒ 0 |

```bash
grep -rn --exclude-dir=bin --exclude-dir=obj "Tank\|Pipe\|Valve" src/HmiEditor.AI/     # 期望：仅 2 处注释
grep -c "additionalProperties" src/HmiEditor.AI/Inference/PromptTemplates/ScreenGenerationSchema.json
awk '/^public enum HmiControlType/,/^}/' src/HmiEditor.Core/Models/HmiControlType.cs \
  | grep -cE '^\s+[A-Z][A-Za-z]+,(\s+//.*)?$'                                          # 期望：26
grep -rn "Motor\|Agitator\|Pump" src/HmiEditor.Core/Models/HmiControlType.cs ; echo "exit=$?"   # 期望：无命中
```

---

## 5. 需**硬件 / 实机**（登记册 `#29` 残余 ＋ 08-19 旧计划 ＋ 各方案）

| 项 | 状态 | 未完成 | 核对 |
|---|---|---|---|
| RAM 预算实测 | ❌ | 在跑的设备读 `meminfo` / LVGL 堆峰值 | `grep -n "仍是声明值\|RamSizeBytes" docs/2026-09-18-resource-budget-measurement.md` |
| RK3506 资源预算 | ❌ | 无固件镜像、无原理图/BOM ⇒ 8 MiB/64 MiB 是声明值 | `grep -n "未经实测" src/Hmi.Hardware.Builtin/RockchipRK3506Target.cs` ⇒ `:28` |
| `ResourceEstimator` 系数校准 | ❌ | 系数是硬编码「经验值」，无实测体积比对 | `sed -n '1,10p' src/HmiEditor.Core/Services/ResourceEstimator.cs` |
| Windows 发布包 ＋ 代码签名 | ❌ | 只在 Linux 实测过；**只有 sha256、没有签名** | `grep -rIn "signtool\|osslsigncode\|Authenticode\|\.pfx" scripts/ tools/ .workflow/ ; echo "exit=$?"` |
| 08-19 P0-3 GT911 触摸 | ❌ 硬件 | 诊断结论「芯片正常、**感应层信号未到芯片**」＝硬件问题；无修复后复测 | `firmware/t113/docs/touch-hardware-diagnosis.md` |
| 08-19 P0-4 RS485（R74/PG9） | ❌ 硬件 | 无 RX 波形 / Modbus 四操作验收记录 | `grep -rn "R74\|PG9" firmware/t113/` |
| 板子侧 TLS 尺寸/内存 | ❌ | 评估 §6-5、代理契约 §6-7 | 见 §2.3 |
| 8 小时稳定性 ＋ 100/500/1000 控件性能矩阵 | ❌ | 总纲 §九要求，仓内无对应测试 | `grep -rIln "1000 控件\|8 小时\|8h" tests/ scripts/ ; echo "exit=$?"` |
| SVG 设备端实机验证 | ❌ | C 段（画布）已闭环，**设备侧**只有 `lv_conf` 文本探针 | `grep -rn "LV_USE_DRAW_SW_COMPLEX_GRADIENTS" firmware/t113/config/lv_conf.h` |

---

## 6. 工程化与卫生

### 6.1 判据 / 架构残余

| 项 | 状态 | 未完成 | 核对 |
|---|---|---|---|
| `#61f` | 仍开着（模板已立） | 其余 A 类产出方「返回值带码」：`GeneratedCodeValidator`(4 站) / `CsvVariableImporter`(3 站) / `FontSubsetAnalyzer` / `RecipeManager` | 登记册 `#61f` |
| `#61` | 仍开着（本体 2 站） | 直通面基线 6 站，其中 `status.verify.failed` / `audit.verify.code.failed` | `grep -rn "ExceptionMessagePassthroughFaceTests" tests/` |
| `#61p` | 仍开着（只登记） | `ReferenceType.ExpressionVariable` 零产出 —— 删成员前须先量「有无历史工程读它」 | `grep -rn "ExpressionVariable" src/` |
| `#43` | 部分闭环（小） | 负控 N9 已过期（它注入的正是被删的 4 行） | `grep -n "check-xaml-duplicate-wiring" scripts/ci-gate.sh` |
| `#42` | 仍开着（小） | 动作面判据「绿时无读数」（统一出口 `JudgeReadings.cs` 已在） | `ls tests/HmiEditor.App.UiTests/JudgeReadings.cs` |
| `#58` | ⚠ 前提已变 | 本会话 `id -u`=1000、`chmod 000` 后读失败 ⇒ 边界④**可试补判据** | `id -u` |
| `#88` | 边界（小） | 其它视图的 TextBox 接线未覆盖（后续候选） | 登记册 `#88` |
| 设备通道自动重连 | ❌ | 已改 `SemaphoreSlim` / 短帧错误 / 统一异常，但**无自动重连** | `grep -rIn "Reconnect" src/HmiEditor.Device/ src/HmiEditor.Core/Services/ ; echo "exit=$?"` |
| LVGL **v10** 兼容 | ❌ | 兼容检查只覆盖 8.3–9.5 | `grep -n "SupportedLvglVersions" -A2 src/HmiEditor.CodeGen/LvglApiCompatChecker.cs` |
| 控件级锚点（左/中/右） | ❌ | 模型无 Anchor 字段 | `grep -c "Anchor" src/HmiEditor.Core/Models/HmiControl.cs` ⇒ 0 |
| 审计**电子签名** | ❌ | `AuditLogStore` 无签名 | `grep -rIn "Signature\|VerifySignature" src/HmiEditor.Core/Services/AuditLogStore.cs \| wc -l` ⇒ 0 |
| 模拟器**截图回归** ＋ 硬件冒烟 | ❌ | 仓内 0 命中 | `grep -rIln "screenshot" tests/ scripts/ .workflow/ \| wc -l` ⇒ 0 |
| CI 缺 `clang -fsyntax-only` | ❌ | 只有 `verify.sh` 的真实 CMake 编译 | `grep -rn "fsyntax-only" scripts/ .workflow/ \| wc -l` ⇒ 0 |
| 09-13 建议的 4 个服务类未抽出 | ❌ | `GitPanelController` / `BuildOrchestrator` / `PreviewRuntimeController` / `ProjectFileService` | `MainWindow.axaml.cs` 仍 2983 行 |

### 6.2 ⚠ 卫生项（含 1 条安全项）

| 项 | 实测 | 核对命令 |
|---|---|---|
| **明文 MQTT 口令硬编码** | ✅ **已标注（阶段 296）**：`firmware/t113/app/runtime/runtime_config.h` 的 `HMI_MQTT_PASS "hmi123"` 带「出厂默认、部署必改」块注释 ＋ 行尾标记；守卫 `ShippedCredentialHygieneTests`（必须有标注 ＋ 不许有别的硬编码凭据） | `sed -n '/HMI_MQTT_PASS/p' firmware/t113/app/runtime/runtime_config.h` |
| `LICENSE` 缺失 | ✅ **已加 MIT（阶段 296，用户拍板）**；守卫 `RepoHygieneGuardTests` | `head -1 LICENSE` |
| `build/*.ps1` 仍在 | ✅ **已删 5 个（阶段 296，用户拍板）**；`build/` 目录随之消失；守卫同上 | `ls build/*.ps1 2>&1 \| head -1` |
| `.trae-html-share-packages/*.zip` **仍被 git 跟踪** | ✅ **已取消跟踪（阶段 296）**：`git rm --cached` ＋ `.gitignore` 新增该目录（文件留在磁盘） | `git ls-files .trae-html-share-packages/ \| wc -l` ⇒ 0 |
| 固件夹具仍是 Mock 工程 | ✅ **已收口（阶段 309–320）** | ① CLI ＋ ②甲 按当前生成器重生成（四类逐字节可复现，④=1）＋ ③ 生成物不入库、开发机生成/构建机只消费（门禁第 7 步查一致性，`build.sh` 缺产物即拒包）。证据：`git ls-files firmware/t113/generated/` 无 `screens.c`；`gen-device-tree.sh --check` 缺席 rc=1；`build.sh` 无 dotnet | —— |

---

## 7. 旧计划残余（来源留档）

| 来源 | 项 | 状态 | 已并入 |
|---|---|---|---|
| 08-19 §4 P2 | LVGL v10 / 控件锚点 / 审计签名 / 截图＋硬件冒烟 | 全部未做 | §6.1 |
| 08-19 §4 P1-1 | 统一绑定刷新契约 | 部分（`BindingSemantics` ＋ `RefreshBindingsParityTests` 已在，但仍是两套发射器） | §4 `§3-#19` |
| 08-19 §4 P1-5 | 过程图形路线 | **本体已闭环**（Tank/Pipe/Valve 全链） | 剩余＝§4 `§3-#18` |
| 08-19 §4 P0-3 / P0-4 | GT911 触摸 / RS485 | 硬件待办 | §5 |
| 08-19 P0-5 正式镜像收口 | 未找到最终镜像 SHA256 / 回归记录 | **未核**（本机未跑 `build.sh`） | §13 |
| 09-13 §3 P0-1…P0-8 | 已修 | **已闭环**（除 P0-7 触摸） | §10 |
| 09-13 §4 P1-2/3/5/6 | 部分 | 见 §6.1 | §6.1 |
| 09-13 §5 P2-1…P2-4 | 部分 | 见 §6.2 | §6.2 |

---

## 8. 环境 / 本地残留（登记册 §5，**非仓库缺陷**）

| id | 项 | 核对 |
|---|---|---|
| `§5-#27` | `App.UiTests` Debug 全量并发 testhost 挂死（**未复测**） | `grep -n "CONFIG=Release" scripts/ci-gate.sh` |
| `§5-#28` | `.dbg/` **209 MB** 未跟踪残留 | `du -sh .dbg` |
| `§5-#29` | `~/文档/MockChineseProject.hmi`（15.8 MB）＋ `MockHmiAssets/`（22 MB） | `ls -la ~/文档/ 2>/dev/null \| head`（按路径卫生**只报告不处置**） |
| `§5-#30` | 沙箱删除守卫拦 `rm -rf "$GOLDEN_DIR"`（**未复测**） | —— |
| `§5-#31` | `~/.local/share/NuGet/http-cache` 只读（成因在，已不在门禁路径） | `touch ~/.local/share/NuGet/http-cache/x 2>&1 \| head -1` |

---

## 9. ⚠ 未写进登记册的最新工作流（Oct 9–11，14 个提交）

登记册最后一节是阶段 288（2026-10-09）；其后 14 个提交全是 **T113 设备链路**，**尚未入册**：

```bash
git log --pretty='%h %ad %s' --date=short -14
```

- 已修：找回 SSH/dropbear ＋ 2048 位 host key · 板载 eth0 静态 `192.168.100.100/24` · adbd 看护 ·
  `.so` 热 OTA 的 `swu_next=hmi_restart` 消费端 · `S97hmiruntime` 的 stop/restart 失效 · 刷机链路（Boot 类型/构建退出码）。
- **收尾遗留**（`firmware/t113/docs/wavepi-s6-migration-plan.md` §8 遗留）：
  ① GT911 修复**待整编烧录验证**；② 触摸驱动无 `ctp_power_io`（`ctp_power is fail` 警告）；
  ③ ~~诊断 DBG 日志（`main.c` / `lcd_sunxifb.c` / `sunxifb.c`）待清理~~ → ✅ **已清理（阶段 299）**：
  19 处消息去 `"DBG: "` 前缀 ＋ `sunxifb.c` **6 处裸输出**（2 处 `fprintf(stderr, "DBG: …")` ＋ 4 处 `printf("Turn on …")`）
  改走 `HMI_LOG_DEBUG`（原先**绕过级别闸门**，默认门槛 Warning 下照样打）；唯一保留的裸 `printf` 是
  `#if LV_USE_SUNXIFB_DEBUG` 内的 FPS 统计。守卫 `FirmwareDebugLogHygieneTests`（我 0 命中 ＋ 反向腿定界 ＋ 裸输出上限）。
  **仍遗留**：① 与 ② 需要板子。

```bash
sed -n '248,259p' firmware/t113/docs/wavepi-s6-migration-plan.md
sed -n '73,80p' firmware/t113/docs/test-report.md     # F4 RS485 待 dts；F7–F9 待外设
```

---

## 10. 已闭环 —— **勿重复投入**

- 07-30 主报告 **G1–G20** 全部闭环；08-02 增量 **I1–I12** 全部闭环（残余见 §4）。
- 09-18 优化计划 **P0-1…P0-5** 全部完成；P1-1 表达式驱动视觉、P1-3 SVG A+B+C 段、P1-6 Dock 其余拖拽路线 均已闭环。
- B1–B4（行数守卫 / `PropertyPanelView` 拆分 / `SimulatorProjectExporter` 拆分 / 覆盖率工具链）已闭环。
- 登记册 §4 主体已抽干：`#15`–`#20`、`#22`–`#26`、`#27b`/`#27c`/`#27d`、`#30`–`#39`、`#41`/`#44`–`#46`、`#48`–`#57`、
  `#59`–`#61x`（除 `#61`/`#61f`/`#61p`）、`#62`–`#100` 均按尾部状态收口。
- 已交付能力：报表模板/设计器/PDF·xlsx/定时自动导出/自包含 HTML 打印 · 表达式驱动视觉 · 过程图形 Tank/Pipe/Valve 全链 ·
  SVG 渐变/transform 解析+几何+CodeGen+画布+像素判据 · Dock 布局持久化与拖拽回归网 · 覆盖率工具链 ·
  本地化扫描面 0 ＋ 插件程序集守卫 · 设计-代码单一事实源 · 09-13 P0-1…P0-8（除触摸）。
- **有意不做**：`#21` 口令强度不校验 · `#66` 9 枚 `Perm*` 不接（空壳处理器）· `§2-#8` 按进程分目录（破坏增量编译复用）·
  `#51`/`#61k`/`#85`（判据诚实边界 / 翻译=改写产物 / AI 提示词刻意不本地化）。
- **复核后不成立（登记有误，非缺陷）**：`#20b` · `#24` · `#82` · `#76`（误诊已撤回）。

---

## 11. 待拍板清单（去重后）

| # | 待定 | 出处 |
|---|---|---|
| 1 | TLS：**PSK 先行 vs X.509 先行** | `device-tls-assessment §7-1` |
| 2 | TLS：**是否允许「显式降级」** | `device-tls-assessment §7-2` |
| 3 | `§2-#10` 现场参数**数值**：`N / T / D / 卡容量·等级 / CoV / 掉电` | 量化表 §5 |
| 4 | `§2-#1`~`§2-#5` 五条 | 登记册 §2 |
| 5 | 运行期抑制**是否下发设备** | 登记册 §196.4 |

---

## 12. 建议动手顺序

1. **M5/M2 订阅接线**：`mqtt_client.c` 订阅 → `hmi_cmd_handle`；报警/状态 publish ＋ LWT；补**假 broker** 主机测试
   （纯工程、可主机判，解锁 M2/M5 的最大缺口）。
   → **M5 那半已落地（阶段 289）**：`app/comm/mqtt_cmd_wiring.{h,c}` ＋ 31 条主机判据 ＋ 8 支负控；
   → **M2 的状态/LWT 那半也已落地（阶段 290）**：`mqtt_set_will`/`mqtt_set_connect_cb` ＋ 上线 `<pre>/status`（retain）
   ＋ 假 broker 端到端（驱动产品代码）＋ 10 支负控（含修掉 `mqtt_start` 清空订阅表）；
   → **M2 的报警那半也已落地（阶段 291）**：ABI `alarm_snapshot` ＋ 生成器发射 ＋ 宿主边沿触发发布器；
   假 broker 第 10 步 **35** 条；负控 7/7。
   **剩余（不属本项里程碑）**：broker 侧 ACL 配置物 · M3 Web UI · 板子实测。
2. **ISA-18.2 收尾**：`AllowedResponseMs` 面板框 ＋ 报警列表按优先级着色/排序。
   → **优先级面已收口（阶段 292）**：面板可空输入框（空=默认值）· 列表 `P1…P4` ＋ 按优先级排序/着色
   （只排显示、模型顺序不动）· 判据 ＋4 规格 / ＋1 用例 / 普查 10→11 · **负控 8/8**；
   **剩余**：**升级(escalation)本体**（Core 无模型无逻辑，需先立模型与设备契约）。
3. **`#47`/`416` 定位**：发布门槛里唯一纯软件项。
   → **`416` 已定位（阶段 293）**：`FontManager.Current._fontCollections[<FontCollection>]._glyphTypefaceCache`
   （按族名做键；+1 新族 = +1 条、同族重复解析不涨）＋ 判据 `HeadlessFontCacheGrowthTests`；
   → **阶段 294**：预算判据与隔离级别解耦（`HeadlessFontCache.Clear()`；读数 2,274 → 3,938 → **2,274**）
   ＋ 负控 2/2；**候选修法「切 `PerAssembly`」实测排除**（全量 108 红／102 条闸门自锁，已回滚）。
   → **阶段 295**：找到并修掉**一条可复现根因** —— 静态初始化器里 `new SolidColorBrush`（AvaloniaObject）
   会把 `Dispatcher.s_uiThread` 抢到调用线程（探针实测 `null → 调用线程`；`ImmutableSolidColorBrush` 不抢）；
   `src/` 9 处改用 `Immutable*` ＋ 行为/静态两面判据 ＋ 负控 3/3 ＋ **连续 3 轮门禁 0 红**（3999/0）。
   **剩余（窄，低优先）**：普查表里那几类入口**已逐条验证完（阶段 297）** ——
   `AvaloniaSynchronizationContext` / `new DispatcherTimer` / `MediaContext.Instance` 实测都会抢、
   仓库侧零或仅 UI 线程调用点；`GestureRecognizers`/`TransformTrackingHelper` 本版本不存在（销案）。
   **阶段 298**：`Dispatcher.UIThread` 也收进机械面（`UiTestHost.UIThread` 唯一入口 ＋ 静态面 0 命中 ＋
   宿主只许一处）。**剩余（很窄）**：静态面只抓直接引用，**间接**访问（反射／第三方库）抓不到。
4. **代理二进制 ＋ init 脚本**：解锁 TLS §6-2/§6-3/§6-4/§6-6；前置是 §11-1 拍板。
   → **阶段 300：拍板已落**（§7-1 **两者都做** PSK＋X.509；§7-2 **永不降级**），并**补上设备侧接线**：
   原先 `comm_core.c` 一条 `mqtt.tls*` 键都没读、C 契约零调用点 ⇒ 生成的配置在设备上**不生效**；
   现已读键 → 决定（`hmi_mqtt_tls_resolve_config`）→ 非法则 **MQTT 不启动 ＋ 审计**（fail-closed），
   纯函数面 14 行样例 ＋ 接线面 19 条判据，**负控 4/4**（含"经典降级"那支）。
   **阶段 301**：代理**配置契约**已落（`tls_proxy_config.{h,c}`：退出码 0/3/4 ＋ 纯函数 ＋ INI 入口；
   监听恒回环、`off`⇒"不需要"与"模式写错"严格区分、fail-closed），判据 12 行样例 ＋ 文件腿，**负控 4/4**。
   **阶段 302**：**代理二进制本体已落地**（`app/proxy/tls_proxy.c`）：PSK ＋ X.509 **两条路径** ·
   只绑回环（`ss` 实测）· §6-2 永不降级 · §6-3 缺件拒启动 · §6-4 未校时拒 X.509（PSK 不受影响）· 审计同 `hmi_audit` 行形状；
   主机侧用 `openssl s_server -psk`／自签 CA 起**真 TLS 假 broker** 做**双向**端到端，固件主机测试 **10 → 11 步**（19 条断言），
   **负控 5/5**；设备侧构建接入（app Makefile 目标 ＋ 包 `+libopenssl` 与安装）。
   **阶段 303**：**init 脚本四条已落地**（`S97hmiruntime` 的 `start_tls_proxy()`）：procd ＋ respawn、
   代理**先于** hmi_runtime 但**晚于** UDISK 挂载、只绑回环、失败不改 `mqtt.tls`；退出码 0/3/4 分派
   （4 那一支**不启动**并落审计）；`stop_service` 也停代理。判据 `TlsProxyInitScriptGuardTests`（文本级）
   ＋ `check_tls_proxy.sh`（语义级），**负控 7/7**。
   **剩余**：板子上**内存**（VmRSS）实测（§6-5 的内存半边 / §6-7）。
   （**尺寸**半边与**设备工具链编译验证**已在**阶段 304** 补上：对 `OpenSSL 1.1.0i-fips` 交叉编译 0 警告，
   代理剥离后 **12,676 B**，镜像 `libssl` 348 KB ＋ `libcrypto` 1.69 MB；可复跑 `scripts/check-cross-proxy.sh`。）
   （**X.509 轮换/运维文档**已在**阶段 305** 落地：`docs/2026-10-11-tls-ops-runbook.md`。）
5. **固件 bring-up 收尾**：GT911 整编烧录复验 · DBG 日志清理 · RS485 UART3 dts。
   → **DBG 日志清理已完成（阶段 299）**：19 处去 `"DBG: "` 前缀 ＋ `sunxifb.c` 6 处绕过闸门的裸输出
   改走 `HMI_LOG_DEBUG`；守卫 `FirmwareDebugLogHygieneTests`，**负控 4/4**。
   **剩余**：**GT911 整编烧录复验** · **RS485 UART3 dts** —— 两者都要板子（不在纯代码侧）。
6. **卫生项**：`LICENSE` · 明文 MQTT 口令标注 · 清 `build/*.ps1` · 取消跟踪 `.trae-*.zip` · 换掉 Mock 夹具。
   → **阶段 296 完成前四项**（用户拍板 MIT ＋ 删 `.ps1`）：新增 `LICENSE`(MIT) · 口令加「出厂默认、部署必改」
   标注 · `git rm` 5 个 `.ps1` · `git rm --cached` 2 个 zip ＋ `.gitignore`；守卫 `ShippedCredentialHygieneTests`
   ＋ `RepoHygieneGuardTests`，**负控 4/4**。
   **已收口（阶段 309–320；用户拍板 ①＋②＋③）**：
   - **①** `tools/DeviceExport`（可脚本化导出 CLI，含资产库与字体条目）✅
   - **②甲** 按**当前生成器**重新生成设备树（`screens.c`/`tags.{h,c}`/`generated_security.h`），
     夹具跟着更新，主机侧 11 步判据全部修好 ✅
   - **④** 生成器戳只留版本号（去 git sha）⇒ 导出**可逐字节复现** ✅
   - **③** 生成物**不入库**、**开发机生成、构建机只消费**：
     (a) `scripts/gen-device-tree.sh`（`--check`/`--write`）· (b) 接进 `ci-gate.sh` **第 7 步**
     （固件 host 测试之前）· (c) `screens.c` 未追踪（`.gitignore`；在场逐字节比、缺席**直接红**（见阶段 321））
     · (d) `tina/build.sh` 拷 `src-generated` 前查四类产物、缺则打印"跑 `--write`"并 `exit 1` ✅
   - **端到端证据**：`git ls-files` 无 `screens.c` 且 `git check-ignore` 命中；门禁第 7 步实测通过；
   ⚠ **阶段 321 修正（用户拍板）**：`DeviceExportReproducibilityTests` 的「生成物缺席」从「明确未查（绿）」改成**直接红**并指路 `gen-device-tree.sh --write`；理由是缺席等于这条判据**没在保护任何东西**。（`DeviceFixtureProvenanceGuardTests` 的腿②仍是「缺席未查」软行为，与这条不一致 —— **待用户定**是否一并对齐。）
     缺席时 `gen-device-tree.sh --check` **rc=1**；`build.sh` **不含 dotnet**。
7. **等数值/拍板**：`§2-#10` 选型与 §6 判据面 · `§2-#1`~`§2-#5` · TLS 两问 · 抑制是否下发。

---

## 13. 方法与诚实边界

- 本表为**静态证据**（grep / ls / sed / git）。**未跑** `dotnet build` / `dotnet test` / `scripts/ci-gate.sh` / `scripts/verify.sh`
  （最新读数见 `docs/project-status.md` 阶段 288：`GATE_EXIT=0` / 7 步 / **3988 通过 / 0 失败**）。
- **未能复现的断言（不作为发现）**：`scripts/deploy.sh:18 PASS=tina` —— 该文件**不存在**，全仓 `PASS=`/`PASSWORD=` 扫描只命中
  `runtime_config.h:39`。原报告此条已剔除。
- **本机无法核验**：GT911 / RS485 的真实硬件状态（只能读文档自述）· RAM 真实占用（需在跑的设备）·
  Windows 包（无 Windows 环境）· 08-19 P0-5 正式镜像的 SHA256/回归记录（未跑 `build.sh`）·
  en-US 仓外是否曾有人工抽检（仓内无痕迹 = 无证据）。
- 登记册自身的口径提醒仍然有效：**文档可能滞后，以代码为准**；本表每条都给了命令，读数不符请以现场改本表。
- 本表与登记册的分工：**单项权威 = 登记册；跨计划汇总与核对命令 = 本表**。

---

## 附录 A：阶段 322–323 记录（含一次更正）

**阶段 322 —— TLS 凭据/配置热加载（SIGHUP）已落地**（提交 `e67183b`）：
`tls_proxy.c` 加"信号处理器只置位 + 主循环里重载"；合法 ⇒ 审计 `mqtt-tls-credentials-reloaded`
且**监听不断**；非法（缺件/模式写错）⇒ 审计 `mqtt-tls-reload-failed` 并**保留旧凭据**；
⚠ 新配置写着 `mqtt.tls=off` 时**拒绝**这次重载（`tls-proxy-not-needed-refused`），否则一次误编辑就把
运行中的 TLS 静默关掉。判据 `check_tls_proxy.sh` 19 → **22 条**；**负控 2/2**（去掉 SIGHUP 安装 ⇒
"合法"那条红；事件名改错 ⇒ "非法"那条红）。runbook 里"代理没有 SIGHUP 热加载/轮换需重启"的表述已改。

**阶段 323 —— CRL 吊销：实现写了但**不生效**，已整体撤回**：
加了 `mqtt.tls.crl_path`（结构体 + INI 键，不进 `resolve` 签名以免牵动契约样例表）、启动时
"给了 crl_path 却读不到 ⇒ 拒绝启动"（fail-closed）、`tls_upstream` 里 `X509_STORE_add_crl`
＋ `X509_V_FLAG_CRL_CHECK[_ALL]`。判据里**被吊销的 broker 证书照样连上**（报「期望 [] 实得 [PONG-CA]」），
而"空 CRL 照常连上""缺 crl 文件 ⇒ exit 4"是绿的 ⇒ 与"CRL 装上了、但里面没有那条吊销记录"一致。
最可能原因：`openssl ca -revoke` 用在了一张**不是经 `openssl ca` 数据库签发**的证书上
（那份 CA 是 `req -x509` 自签、srv 是 `x509 -req` 签的）。
**撤回原因**：判据红 ⇒ 不把不生效的功能留在主干；撤回后 TLS 步骤回到 **22 通过 / 0 失败**。
**下一步**：① 测试里先 `openssl crl -text` 断言 CRL 里**确有该序列号**（否则后续断言毫无意义）；
② 让 srv 证书**经 `openssl ca` 签发**（或先把序列号写进 `index.txt`）再 `-revoke`；
③ 正反两半（吊销⇒拒 / 空 CRL⇒通）都在了才算判据成立；④ 复核 `CRL_CHECK_ALL` 对整条链的作用。

**更正（关于 `e67183b` 的提交信息）**：那条信息里写了"台账同步"，但那次台账补丁**没有命中任何行却静默通过**
（我的 `for` 循环找不到目标就什么都不做，而 `print` 无条件说"已更新"）—— **台账当时没被改动**。
这是本任务里同类错误第 4 次（前三次是锚点未命中/语法错而中止）。根治办法已定为：
**补丁脚本必须断言"替换次数 ≥ 1"**，或用无锚点的**追加**方式（本附录就是这么加的）。

---

## 附录 B：CRL 诊断（阶段 325）——**已排除"OpenSSL 用法错误"**

**做法**：把代理里那段 CRL 代码（`X509_STORE_add_crl` + `X509_STORE_set_flags` +
`X509_VERIFY_PARAM_set_flags`，且 `SSL_CTX_load_verify_locations` 在其**之前**）**原样搬到独立小程序**，
直接连一台用被吊销证书的 `openssl s_server`。三个变体实测：

| 变体 | 结果 |
|---|---|
| ① 无 CRL（基线） | `SSL_connect=1 verify=0 (ok)` |
| ② CRL 在 `SSL_CTX_load_verify_locations` **之后**（**代理的顺序**） | `SSL_connect=-1 verify=23 (certificate revoked)` ✅ |
| ③ CRL 在之前 | 同样 `verify=23` ✅ |

⇒ **"OpenSSL 用法/顺序不对"这个假设已被排除**。同一套调用在独立程序里能拒，在代理里不拒 ⇒
问题在**代理自身的流程**，最可能是这两条之一：
（a）**那个 CRL 块根本没跑** —— 即 `cfg->crl_path[0] == '\0'` 在使用点仍是空（配置值没到达结构体，
    尽管测试已断言 ini 里确有 `crl_path=` 行）；
（b）块跑了但装到了**另一个** store（例如 `SSL_CTX_load_verify_locations` 之后 store 被替换）。

**下一轮的第一步（很便宜、能一刀切开 a/b）**：在代理的 CRL 块入口加一条审计
（如 `mqtt-tls-crl-applied`，detail 带 `add_crl` 的返回值与 crl 文件基名），然后跑 §324 用例：
- 审计里**没有**这条 ⇒ 是 (a)，去查 ini→结构体这一段；
- 审计里**有**且 `add_crl=1` ⇒ 是 (b)，改走"自建 `X509_STORE` ＋ `SSL_CTX_set1_verify_cert_store()`"。

**同时**：`check_tls_proxy.sh` 的探针目录要支持 `KEEP=1` 保留现场（阶段 323/324 两次都因为 trap
清掉临时目录而无法事后取证 —— 这种"查不到"本身就是我浪费了两轮的原因）。

**当前状态**：CRL 相关改动**全部撤回**（判据红 ⇒ 不留主干）；TLS 步骤 **22 通过 / 0 失败**；
交叉编译 **7/7**；`firmware/t113/tests/check_tls_proxy.sh` 里已有一条**前提断言**（CRL 里必须有该序列号）
的写法记在附录 A，可直接复用。

**附录 B 续（阶段 327）—— 实现已被证明正确，疑点收敛到"测试夹具的构造"**

手工场景（**用与测试同一份 `/tmp/hmi_tls_proxy` 二进制**，s_server 用被吊销证书）：

| 手工跑法 | 客户端收到 | 审计 |
|---|---|---|
| `--config` ＋ 不加 `--once` | **`[]`（被拒 ✓）** | `crl-enter` → `crl-added(add-crl-ok)` |
| `--config` ＋ **`--once`** | **`[]`（被拒 ✓）** | （去噪声后只剩 start/stop） |

同时读清了 `roundtrip()` 本体：它**原样** `--config "$ini"`，不重写 ini（"助手丢了 crl_path"的假设排除）；
`--once` 也已排除。⇒ **代理这一段是对的**（两次独立手工验证），**测试里"实得 PONG-CRL"来自测试夹具**。

**下一轮的第一步（把最后一处没断言的东西断言上）**：测试里目前只断言了"CRL 里确有 srv2 的序列号"，
**没断言 CRL 的签发者与 srv2 的签发者是同一个 CA** —— 这正是唯一剩下的、能解释"CRL 含该序列号却仍放行"的
差异（`openssl crl -noout -issuer` vs `openssl x509 -in srv2.pem -noout -issuer`）。做法：
① 在 §324 块里把这两条 issuer 打出来并断言相等；② 把 CRL 的条目数与 srv2 序列号一起 dump；
③ 若 issuer 相等仍放行，就在测试里用**与手工场景完全相同的那套 CA/证书文件**（先证后用）。
另：`check_tls_proxy.sh` 要支持 **`KEEP=1`** 保留 `$TLSWORK`（本阶段三轮都因 trap 清目录而无法事后取证）。

**附录 B 续二（阶段 328）—— 同一二进制：手工 harness 拒绝，测试脚本放行；差异尚未定位**

本轮把疑点逐条排除（每条都有实测）：

| 假设 | 结果 |
|---|---|
| 测试夹具构造不对 | ❌ 排除：测试里已断言「CRL 含 srv2 序列号 **且签发者一致**（`issuer=CN=zhishu-test-ca`）」 |
| `roundtrip` 助手重写 ini / `--once` 差异 | ❌ 排除：读清助手原样传 `--config`；`--once` 手工复现同样拒绝 |
| 端口与前面用例冲突 | ❌ 排除：改用 11886/18886 后现象不变 |
| 残留 `s_server` / 代理进程占端口 | ❌ 排除：`ss -tlnp` 与 `pgrep` 均为空 |
| 配置没到结构体 | ❌ 排除：代理审计 `mqtt-tls-crl-configured ... /crlca/revoked.crl`（**带了路径**） |
| "缺 CRL 文件 ⇒ 拒绝启动" | ✅ 本判据现在是**绿的**（exit 4 ＋ 审计 `crl-file-missing`） |

**同一份代码/二进制**：手工 harness（`/tmp/crlrun`，显式启停 + `/dev/tcp`）**拒绝**（客户端收到 `[]`）；
`check_tls_proxy.sh` 里**放行**（客户端收到 `PONG-CRL`），连"把手工配方原样搬进脚本"也一样放行。
⇒ 差异在**测试脚本的运行环境**里，本轮**未定位**。

**下一轮的第一步（能一刀切开"验的是哪张证书"）**：在代理握手成功后审计**对端证书的序列号与签发者**
（`SSL_get_peer_certificate` → `X509_get_serialNumber`/`X509_get_issuer_name`），失败路径也打。
如果审计出来的不是 srv2 的序列号（1000），说明**代理连到的 broker 根本不是 srv2** ——
那就去查测试里 `s_server` 的实际启动情况（把 broker 的 stdout/stderr 也留住）。
另：`check_tls_proxy.sh` 要支持 **`KEEP=1`**（这已是第 4 轮因 trap 清目录而无法事后取证）。

**给"不再反复重贴"定的规矩**：CRL 代码**不再单独贴进主干**，直到测试脚本里正反两半同时绿为止；
若下一轮仍不通，就把 CRL 明确标成「实现已验证、测试夹具未打通、**暂不入库**」，不再重贴。

**附录 B 续三（阶段 329）—— 我上一轮的结论是错的；真正的原因收窄到"SSL 对象建得太早"**

**先纠正（重要）**：附录 B 续一写了"实现已被两次独立手工验证为正确"——**这个结论站不住**。
本轮用**已验证会吊销**的夹具（`openssl verify -crl_check` 报 `verification failed`）复跑，
代理**照样放行**，并且审计给出了决定性证据：

```
mqtt-tls-crl-configured   role= /tmp/fxprobe/crlca/revoked.crl      ← 配置到位、文件可读
mqtt-tls-peer-cert        role= serial=1000 issuer=/CN=Test CA     ← **验的就是被吊销那张**
客户端收到: [PONG-CRL]                                              ← 却接受了
```

⇒ 之前那两次 `[]` 的"拒绝"，很可能不是 CRL 拒绝（例如 broker 时序），我当时**拿结果当结论**了。

**真正的差异（唯一还站得住的）**：**`SSL_new` 的时机**
- 独立探针（**会拒绝**）：先设 store 标志/param 标志 → **再** `SSL_new`
- 代理（**放行**）：`tls_upstream` 最上面就 `SSL_new` → 之后才 `SSL_CTX_load_verify_locations` + CRL 块
⇒ 标志设在**已经建好的 SSL 对象之后**，这个 SSL 的验证参数可能仍按**旧状态**走。

**下一轮（很具体，一次就能判）**：在代理里**把 CA/CRL 设置挪到 `SSL_new` 之前**（或建完 SSL 后再
`SSL_set1_param(ssl, …)`／重建 SSL）。判据已现成：**§324 正反两半**（吊销⇒收不到东西 / 空 CRL⇒照常连上）
＋ 前提断言（CRL 含序列号且签发者一致）。**仍然遵守上轮定的规矩**：不再单独贴进主干，直到脚本里两半同时绿。

**附录 B 续四（阶段 330）—— 重排确实改变了行为，但我的判据夹具不可信 ⇒ 不能据此入库**

本轮做对了什么：把 `tls_upstream` 的 **ctx 级设置（CA/verify/CRL）挪到 `SSL_new` 之前**，
`check_tls_proxy.sh` 一度 **26/26 全绿**（含 §324 正反两半：被吊销⇒收不到东西 · 空 CRL⇒照常连上）。

**但随后的负控把这份绿否掉了**（每条都断言过注入命中）：

| 负控 | 期望 | 实测 |
|---|---|---|
| 删 `X509_STORE_set_flags(store, CRL_CHECK…)` | 被吊销那条应变红 | **仍绿**（26/26） |
| 删 ctx param 的 CRL 标志块 | 应聘 | **仍绿**（26/26） |
| **删掉整个 CRL 块**（`add_crl` 都不做） | 被吊销那条应**变绿**（放行） | 被吊销那条**仍"绿"**，而**对照（空 CRL）变成 `[]` 失败** |

⇒ 一个都不咬，且"删掉全部 CRL 处理"后**对照也失败** ⇒ **我的 `crl_case` 自建夹具结果不可复现**
（同一套代码在不同运行里给出相反结果：时而 `PONG-CRL`、时而 `[]`）。**这样的绿不能当"CRL 生效"**。

**结论（本目标内的处置）**：CRL **实现存在但判据不可信** ⇒ **不入库**（遵守上轮定的规矩）。
**下一步必须先修判据夹具本身**，再谈 CRL：
1. `crl_case` 里对 **broker 就绪**与**端口空闲**做显式等待（现在是 `sleep 0.5/0.8` 拍脑袋），
   并把 broker 的 stdout/stderr 留住（`-quiet` 把证据吞了）；
2. `check_tls_proxy.sh` 加 **`KEEP=1`** 保留 `$TLSWORK`（第 5 次因为 trap 清目录而无法事后取证）；
3. 用**同一夹具连跑 5 次**，要求结果一致（**可复现性本身**先作为判据），再挂正反两半；
4. 届时重贴 CRL 代码（补丁脚本 `.workbuddy/tmp/crl-patch.py` 已能一键 apply/revert）。

**附录 B 收尾（阶段 331）—— 最终结论：CRL 未交付，且判据夹具不可信**

本轮先把判据夹具修成**可复现**（新增 `wait_port_open` 等 broker 就绪；连跑 3 次结果一致 24/2），
它立刻暴露了两种**互斥**的失败模式：
1. 测试脚本里：审计**只有** `crl-configured`（连 `proxy-start` 都没有）⇒ 代理在处理该连接时异常退出；
2. 手工同场景：审计有 `proxy-start` ＋ **`peer-cert serial=1000`（握手成功）**，客户端却收到 `[]`。

⇒ **`[]` 至少有三种含义**（被拒 / broker 没起来 / 转发没回来），**不能作为「吊销生效」的证据**；
而第 2 种模式直接证明：**被吊销的证书握手成功 ⇒ CRL 未强制执行**。

**最终处置（本目标内到此为止）**：
- CRL 相关代码**全部不入库**（补丁留在 `.workbuddy/tmp/crl-patch.py`，含 apply/revert 与诊断审计，可随时续做）；
- runbook 已**如实改写**为「证书吊销（CRL/OCSP）**未实现**」并附实测证据与过程索引；
- 契约里**没有**新增 `crl_path` 键（未交付就不该进契约）。
**要真正交付 CRL，前置是先把「一次连接的结果」做成可观测**（broker 就绪 ＋ 进程存活 ＋ 握手结果
的三态判据，且退出码与审计必须能区分），否则任何绿都是假的 —— 这是本目标最贵的一课。

**附录 B 续五（阶段 332）—— CRL 的**前置**已交付：一次 TLS 往返的"三态"判据**

`check_tls_proxy.sh` 新增 `wait_port_open` ＋ `tls_probe`，把"一次往返的结果"打成**三态**：

| 态 | 定义 |
|---|---|
| `ok` | 拿到期望应答（握手成功 ＋ 转发成功） |
| `reject` | 没拿到应答 **且** 审计有握手失败证据（`handshake-verify` / `handshake-failed`） |
| `broken` | 其余（broker 没起来 / 代理没监听 / 退出码非 0）⇒ **不可采信** |

**判据 2 条（都绿，24/0）**：
- **正控**：正常 ca 场景**连跑 3 次全部 `ok`**（可复现性本身作为判据）；
- **负控**：broker 不启动 ⇒ **`broken`**，**不许**被当成 `reject`（这正是阶段 330 假绿的来源）。

⇒ 从此 `[]` 不再有歧义：**要判定"被拒"，必须有握手证据**。这也是交付 CRL 的真正前置。
**下一轮**：用新探针重贴 CRL（`.workbuddy/tmp/crl-patch.py apply`），把 `revoked`/`empty` 两种场景
跑成 `reject`/`ok` 的**确定结论** —— 若 `revoked` 仍得 `ok`，那就是**可复现的"未生效"结论**，
按"未交付"收口并保留证据；若得 `reject`，CRL 即可入库。

**附录 B 终章（阶段 333，目标末轮）—— CRL 判据**仍不可信**；并且冒出一条更值得查的线索**

用上轮交付的三态探针做定案实验，结果**否掉了"可以定案"**：

| 实验 | 期望 | 实测 |
|---|---|---|
| §324 被吊销（CRL 含该序列号、签发者一致、`openssl verify` 独立确认会吊销） | `reject` | **`ok`** |
| §324 对照：空 CRL（未吊销） | `ok` | `ok` ✓ |
| **可达性正控：用一张不相干的 CA 去验同一台 broker** | **`reject`** | **`ok`** ❌ |

⇒ **可达性正控没通过** ⇒ 我的探针**尚未被证明能产出 `reject`** ⇒ 因此"被吊销⇒ok"**不能**读成
"CRL 未强制执行"（也可能是**探针根本测不出拒绝**）。**本轮不给 CRL 下结论。**

⚠ **更值得查的线索（新开项）**：用**不相干的 CA** 居然也握手成功、客户端拿到应答 —— 若属实，
说明**代理可能根本没在强制校验 broker 证书**（`SSL_CTX_set_verify(SSL_VERIFY_PEER)` ＋
`SSL_CTX_load_verify_locations()` 的组合未生效）。这比 CRL 严重得多，**优先级高于 CRL**：
先查清"校验到底有没有生效"（用三态探针 ＋ 对端证书审计），CRL 才有意义。

**下一步（明确两步）**：
1. 先做**能产出 reject 的判据**（CA 不匹配这条必须先红）—— 查代理的 verify 路径（是否 `SSL_VERIFY_PEER`
   被 `SSL_CTX_set_verify` 之后又被覆盖／是否 `SSL_get_verify_result` 根本没被检查）；
2. 校验确认生效后，再回来判 CRL（补丁 `.workbuddy/tmp/crl-patch.py` 可一键重贴）。

**本目标（①三项收尾）到此的确定结论**：工具链编译 ✅ · SIGHUP 热加载 ✅ · **CRL 未交付**
（判据不可信，且发现"可能不校验证书"这一更高优先级线索）· 板子内存需硬件（不在目标内）。
