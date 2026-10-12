# 报警搁置（Shelving）设备契约（阶段 273）—— `§2-#11` 切片㈢ 另一半的前置

> 用户拍板：**A1=② 搁置设备侧也做** · **A2A3=要权限 ＋ 审计 ＋ 硬上限**。
> 本文是**契约**（先契约、后实现）：定下状态编码、迁移矩阵、权限判定点、审计字段、硬上限口径，
> 以及"实现要动哪些文件与守卫"和"验收怎么判"。**本阶段不写实现代码。**

---

## 1. 为什么搁置必须先有契约

M2 的报警上行契约已经**留了位**：`st=4` 是保留给搁置的（`§188`）。设备侧现在的编码是
`state 0/1/2/3`（`CodeGenerator.Alarm.cs:176-185` 生成），加一档意味着**生成器、设备运行时、
编辑器模型、报表文案四处同时变** —— 没有契约就会出现"设备先升级、远程端不认识 `st=4`"
的时序事故（这正是 M2 用 `int` ＋ 保留位、而不是绑死枚举的原因）。

---

## 2. 语义（写死，逐条可判）

| # | 决定 | 理由 |
|---|---|---|
| S1 | **搁置是一种"停止播报"的状态**（`st=4`），不是删除报警 | 报警仍在、条件仍在求值；现场需要的是"先别吵我"，不是"当它不存在" |
| S2 | **搁置必须带到期时间**，不允许无限搁置 | ISA-18.2 实践：搁置是短期手段；"永久搁置"等于关掉报警 |
| S3 | **载荷不携带底层条件**：搁置期间远程端只看到"已搁置" | 远程端要判的就是"播不播报"；底层条件由设备内部保留，到期/解除时设备**重新发布真实状态**（`st=1/2/3`） |
| S4 | 若将来要远程看到底层条件：**加一个可选 `cond=<0\|1>` 字段**（向后兼容） | M2 的解析器**忽略未知键**（判据 `UnknownKeys_AreIgnored_OnBothContracts`）⇒ 加字段不会打挂老远程端 |
| S5 | 搁置**不改变报警的严重度与优先级** | 优先级仍是 `f(严重度, 允许响应时间)`（切片㊁）；搁置只影响播报 |

---

## 3. 状态编码（5 档，与设备/C# 同号）

| 值 | 名字 | 含义 |
|---|---|---|
| 0 | `Inactive` | 非激活（正常态） |
| 1 | `Active` | 激活中（未确认） |
| 2 | `Acknowledged` | 已确认（条件可能仍满足） |
| 3 | `Returned` | 已恢复 |
| **4** | **`Shelved`** | **已搁置（停止播报中）** |

⇒ 编辑器侧 `AlarmState` 需要**新增 `Shelved = 4`**（枚举显式赋值 ⇒ 生成器的 state 数字**不变**，
只有新增这一档会说 `4`）。

---

## 4. 迁移矩阵（**验收规格**：合法路径与非法路径都逐条判）

事件列：**条件满足**（Cond Met）· **条件解除**（Cond Clear）· **操作员确认**（Ack）·
**搁置**（Shelve）· **搁置到期**（Expire）· **手动解除**（Unshelve）。

| 当前 \ 事件 | Cond Met | Cond Clear | Ack | Shelve | Expire | Unshelve |
|---|---|---|---|---|---|---|
| `Inactive`(0) | → **Active**(1)¹ | — | **拒** `not-active` | **拒** `not-active` | — | — |
| `Active`(1) | — | → **Acknowledged**(2)² / → Returned(3)³ | → **Acknowledged**(2) | → **Shelved**(4)⁴ | — | — |
| `Acknowledged`(2) | — | → **Returned**(3) | **拒** `already-acked` | → **Shelved**(4)⁴ | — | — |
| `Returned`(3) | → **Active**(1)¹ | — | **拒** `returned` | **拒** `returned`⁵ | — | — |
| `Shelved`(4) | （内部求值，**不改变对外状态**） | （同左） | **拒** `shelved`⁶ | **拒** `already-shelved`⁷ | → **恢复真实状态**⁸ | → **恢复真实状态**⁸ |

¹ 延时/死区等条件侧细节沿用现有实现（`pending_since`、`Deadband`）。
² `RequiresAck = true` 时的条件解除路径（现有行为）。
³ `RequiresAck = false` 时的条件解除路径（现有行为）。
⁴ **带到期时间**，且时长必须在 `[MinShelveDuration, MaxShelveDuration]`（见 §6），否则**拒**
`duration-out-of-range`；同时要**有权限**（见 §5），否则 **拒** `forbidden`。
⁵ **已恢复的报警不允许搁置**：它已经不播报了，搁置没有意义（ISA-18.2 同）。
⁶ **搁置中不允许确认** —— 这是本契约里最要紧的一条：搁置期间"确认"会让操作员以为
"我已处理"，而实际上报警被静音了；解锁顺序必须是**先解除搁置、再确认**。
⁷ 已搁置再搁置 ⇒ 拒（要延期就**先解除再搁置**，或走显式的"延长"动作 —— 本契约不提供延长，保持动作集最小）。
⁸ **恢复真实状态**：搁置期间设备仍在对条件求值 ⇒ 解除/到期时发布"如果没搁置，现在应该是什么状态"
（`Active`/`Acknowledged`/`Returned`）。**这一步必须写审计**（见 §7）。

**拒绝要带码**（`AlarmShelveIssue`）：`not-active` / `returned` / `already-acked` / `shelved` /
`already-shelved` / `duration-out-of-range` / `forbidden`。
理由与 `#61v`（登录失败按码分派）同款：界面要能**按码**给不同文案，而不是统一一句"操作失败"。

---

## 5. 权限判定点

| 面 | 决定 |
|---|---|
| 权限点 | **`alarm.shelve`**（新）；解除搁置复用同一点（解除不是特权动作） |
| 判定位置 | **两侧都要判**：编辑器侧 `AlarmEngine.Shelve(recordId, until, subject)` 先判；设备侧由生成代码在动作入口判（与 `hmi_actions_permission_*` 同一套角色模型） |
| 未授权 | **拒绝 ＋ 审计一条**（`result=denied`），**不静默失败** —— 静默失败会让操作员以为是系统卡了 |
| 与现有模型的关系 | 复用 `RuntimeUser.RoleId` 的角色与权限表；**不新建一套权限体系** |

---

## 6. 硬上限（写死并判据化）

| 常量 | 值 | 理由 |
|---|---|---|
| `MinShelveDuration` | **1 分钟** | 低于这个值的搁置没有意义（人手都来不及）；且可防"误触即永久静音" |
| `MaxShelveDuration` | **8 小时** | 一个班次量级；ISA-18.2 实践里搁置是短期手段 |
| 到期行为 | 设备**自动解除**并发布真实状态 | 不允许"到期了还挂着" |
| 时钟 | 设备侧只有开机时长（`tb=boot`）⇒ 到期判定用**单调时钟**（与 `custom_tick_get` 同源） | 墙钟会被 NTP 跳变（`main.c:167` 已记这个坑） |

---

## 7. 审计（双落点，一条都不许省）

| 字段 | 说明 |
|---|---|
| `ShelvedBy` / `ShelvedAt` / `ShelvedUntil` / `ShelveReason` | 谁在什么时候搁置到什么时候、为什么（`Reason` 可空但**面板应引导填写**） |
| `UnshelvedBy` / `UnshelvedAt` / `UnshelveReason` ∈ {`Expired`, `Manual`} | 解除者与解除原因；**到期自动解除也要写**（否则事后看不出"报警为什么突然又开始响"） |
| 结果 | `granted` / `denied`（未授权尝试也留痕） |
| 落点 | 设备 `hmi_log`（`hmi_log.h` 的运行时日志）＋ 编辑器 `AuditLogView`（走既有审计存储） |

---

## 8. 影响清单（实现阶段要动的东西，逐项点名）

| 文件 | 改动 |
|---|---|
| `src/HmiEditor.Core/Models/Alarm/AlarmState.cs` | ＋`Shelved = 4` |
| `src/HmiEditor.Core/Models/Alarm/AlarmRecord.cs` | ＋§7 的审计字段（可空 ⇒ 老数据兼容） |
| `src/HmiEditor.Core/Services/AlarmEngine.cs` | ＋`Shelve`/`Unshelve`/到期扫描 ＋ **拒绝码**；`AcknowledgeAlarm` 增加"搁置中拒绝"分支 |
| `src/HmiEditor.Core/Reporting/AlarmReport.cs` | `StateKey` ＋ `alarm.state.shelved` |
| `src/HmiEditor.CodeGen/CodeGenerator.Alarm.cs` | state 4 的迁移代码 ＋ 权限判定 ＋ 到期检查 |
| `firmware/t113/app/runtime/*`（`hmi_actions.c` / `runtime_config.h`） | 权限表 ＋ 两个时长常量 ＋ 到期解除 |
| 字典（两语言） | `alarm.state.shelved` ＋ 7 个拒绝码文案 ＋ 审计文案 |
| **守卫（会红的）** | `AlarmEngineStaleValueGuardTests` · `AlarmEngineTests` · `AlarmReportTests` · `TrendAndAlarmCodeGenTests` · `LocalizationKeyReachabilityGuardTests`（新键要有消费点） |

---

## 9. 验收规格（判据面，实现完成后必须全绿）

1. **迁移矩阵表驱动**：§4 的每一格（合法 → 目标状态；非法 → 具体码）逐条断言。
2. **到期自动解除**：注入时钟推进 ⇒ 状态回到真实状态 ＋ **审计一条 `Expired`**。
3. **权限**：无 `alarm.shelve` ⇒ 拒绝 ＋ 审计一条 `denied`；有 ⇒ 通过。
4. **时长上下界**：`59 s` / `8 h 1 s` ⇒ `duration-out-of-range`；边界值（1 min / 8 h）⇒ 通过。
5. **契约往返**：`st=4` 的报警 payload 两侧格式化/解析一致（M2 已具备，实现时复用）。
6. **生成代码逐字节**：state 4 的生成代码片段逐字节判（沿用 `TrendAndAlarmCodeGenTests` 的形状）。
7. **负控**：至少三支 —— 摘掉"搁置中拒绝确认" ⇒ 红 · 到期不解除 ⇒ 红 · 未授权也放行 ⇒ 红。

---

## 10. 实现顺序（契约之后的动线）

1. **编辑器模型与引擎**（`AlarmState` ＋ `AlarmRecord` ＋ `AlarmEngine` ＋ 报表文案）—— 独立可判，先做；
2. **生成器与设备运行时**（`CodeGenerator.Alarm.cs` ＋ `hmi_actions.c`）—— ①绿之后做，避免"两侧同时半成品"；
3. **远程端展示**（M3 Web 端一起）：搁置中的报警显示"已搁置（至 …）"，且**不参与播报计数**；
4. **审计与权限**随 ①②同步落地（不留"以后再补审计"的尾巴）。
