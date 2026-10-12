# 会话超时端到端落地 + 审计落盘

> 2026-09-16（第 20 轮）。承接阶段 7（运行时用户表的编辑器 UI）留下的安全链路缺口。

## 1. 症状：一处配了却完全没生效的功能

阶段 7 做完后，安全设置面板里已经能配 11 项。盘点到设备端落地情况时发现：

| # | 字段 | UI | 下发路径 | 设备端消费 | 结论 |
|---|---|---|---|---|---|
| 1 | `OperationMode` | 下拉 | `HMI_SEC_ENFORCE`（生成头） | ✅ 门控 | 有效 |
| 2 | `PasswordMinLength` | 数字 | `HMI_SEC_PASSWORD_MIN_LEN` | ✅ `hmi_login_submit` | 有效 |
| 3 | `MaxLoginAttempts` | 数字 | `HMI_SEC_MAX_LOGIN_ATTEMPTS` | ✅ `hmi_user_register_failure` | 有效 |
| 4 | `LockoutDurationMin` | 数字 | `HMI_SEC_LOCKOUT_MIN` | ✅ `s_user_lock_until_ms` | 有效 |
| 5 | `SessionTimeoutMin` | 数字，**默认 30** | ❌ 无 | ❌ 无 | **设备端缺失** |
| 6 | `AutoLogoutEnabled` | 勾选，**默认开** | ❌ 无 | ❌ 无 | **设备端缺失** |
| 7 | `LockScreenId` | 屏下拉 | ❌ 无 | ❌ 无 | **设备端缺失** |
| 8 | `EnableAuditLog` | 勾选，默认开 | ❌ 无 | ⚠️ 有串口日志但不受此开关控制 | **设备端缺失** |
| 9 | `AuditLogStoragePath` | 文本框 | ❌ 无 | ❌ 无 | **设备端缺失** |
| 10 | `EnableTamperDetection` | 勾选，默认关 | ✅ `-DHMI_TAMPER_DETECTION=0/1` | ❌ **零消费**（固件里连 `#if` 都没有） | **下发了没人读** |
| 11 | `RuntimeUsers` | 表格（阶段 7） | `HMI_SEC_USERS_INIT` | ✅ `hmi_login_submit` | 有效 |

**顺带修正一条记错的事实**：`docs/2026-09-16-runtime-credential-verification.md:204` 登记的是
「`LockScreenId` / `SessionTimeoutMin` / `AutoLogoutEnabled` / `EnableTamperDetection` /
`AuditLogStoragePath` **已下发到设备端**，设备端尚未消费」——只对第 10 项成立。
其余四个**从来没下发**：全仓库 grep 只在 C# 侧（`AuthenticationService` + UI + `AgentToolFactory`）
出现，生成器与固件零引用。记错的方向是**乐观的**（以为"只差消费"），实际是"链路根本没接"。

同理 `CodeGenerator.BuildSystem.cs:175` 那句注释「固件代码可通过 `#if HMI_TAMPER_DETECTION`
条件编译启用/禁用 Flash 扇区 CRC 校验」也是**把没验证过的行为写进了注释**——固件里没这个 `#if`。
（本轮已改写，见 §8。这不是"记错了某个事实"，是**写下了一个从未实现的行为**。）

**为什么先做会话超时**（而不是 `EnableTamperDetection` 或审计）：

1. **它是上一轮的半截**。阶段 6 把"登录"做通了（凭据校验 → 会话 → 权限），但没有"登出"：
   `hmi_logout` 只能被显式调用，没有任何超时 ⇒ 操作员登录一次可以**永远保持会话**，
   凭据校验建立的安全边界在无人看管的设备上**不衰减**。
2. **它默认就是开的**：`AutoLogoutEnabled = true` / `SessionTimeoutMin = 30` 是模型默认值 ⇒
   **所有工程都受影响**，用户不用主动去配。而 `EnableTamperDetection` 默认 `false`。
3. **有现成形态可抄**：锁定判据 `(int32_t)(lv_tick_get() - deadline) >= 0` 已在
   `hmi_user_locked` 里验证过（单调 tick + 环绕安全），超时是同一种判据。

## 2. 决定一：与编辑器 `AuthenticationService.CheckPermission` 逐条同构

设备端的权威语义不是"想出来的"，是**编辑器侧已经实现的那一份**：

```csharp
// AuthenticationService.CheckPermission
if (_securityConfig?.AutoLogoutEnabled != false &&
    (DateTime.UtcNow - session.LastActivityAt).TotalMinutes > session.TimeoutMinutes)
{
    session.IsExpired = true;
    _sessions.TryRemove(sessionId, out _);
    SessionLocked?.Invoke(this, _securityConfig?.LockScreenId);   // 跳锁屏
    return false;                                                 // 拒绝本次判定
}
session.LastActivityAt = DateTime.UtcNow;                         // 判定即活动
```

三条抄进设备端（`hmi_actions.c`），一条都不能"顺手改"：

| # | 语义 | 设备端实现 |
|---|---|---|
| ① | **「有人在问权限」本身就算活动** | `hmi_has_permission` 入口调 `hmi_session_touch()` |
| ② | **判据严格大于**（`>`，不是 `>=`） | `(int32_t)(lv_tick_get() - deadline) > 0` |
| ③ | **超时 ⇒ 清会话 + 跳锁屏 + 拒绝本次判定** | `hmi_session_expire()` 在判定**之前**调用 |

前三行合起来就是判定入口的顺序「**评估超时 → 打点 → 判定**」。两条顺序各对应一个真实缺陷，
两侧都补了判据（C# 比文本先后 + C 测行为）：

| 顺序被破坏 | 后果 |
|---|---|
| 评估跑到**判定之后** | 超时那一刻的操作先被放行、再登出 ⇒ 这次操作被送出去了 |
| 打点跑到**评估之前** | `touch` 先把 deadline 推到 now+阈值 ⇒ 紧接着的评估必然算"未超时" ⇒ **超时永不触发** |

第二条更隐蔽：它不是"某一次操作漏过去"，是**这一整条防线不存在**。而它在纸面上几乎看不出来 ——
`touch` 和 `expire` 只是换了行序，两行都在、都能编译、单看任一行都合理。
**第一版只有第一条判据**（只比 `expire` 与 `decide` 的先后），负向验证把第二条造出来时才暴露这个洞。

第 ③ 条的顺序是硬要求：先放行再登出等于把这次操作送出去了，而那正是"超时"要防的事。

## 3. 决定二：活动打点复用已有接缝（不新增宿主原语）

**关键观察**：编辑器侧**从来就没有输入事件钩子**——它的"活动"定义就是"有人在问权限"。
所以设备端照抄这个接缝**不是**为了省符号的妥协，而是同构。

三条路对比过：

| 方案 | 覆盖 | 结论 |
|---|---|---|
| **A. 新增 `hmi_session_touch()` 由 `lv_indev` 读回调调用** | 最准 | 代价：新增宿主符号 ⇒ 动 `SimulatorLinkContractTests` 的穷举契约 + 模拟器补定义 |
| **B. 在已有接缝打点** | 换屏 / 写量 / 报警趋势 | ✅ **采用** |
| C. 不做 idle，登录后固定时长强制登出 | — | 不是超时是限时，工程师调参数调一半被踢 |

B 的覆盖度靠 `ActionPermissionMap` 保证：12 个动作里**只有 Login/Logout 不门控**，而登录本身会重置计时、
登出本身会清除会话 ⇒ 覆盖是完整的。

**一个必须先查清的风险**：如果 `hmi_has_permission` 会被**周期性**调用，活动时间就被刷成
"永久在线"，超时永不触发。实测（`Grep`）：设备端 `hmi_has_permission` **只被 `hmi_require_permission`
调用**，而生成代码里只发射 `hmi_require_permission`（`CodeGenerator.Actions.cs:281`）——
**没有周期性调用者**，所以打点放在判定入口是安全的。

`hmi_session_poll()` 则**刻意不刷新**活动时间（周期函数若能刷新，超时就永远不会触发），
它只负责让"没人动"这件事能在屏幕上**自己**体现（否则要等下一个来操作的人才会发现）。

## 4. 决定三：`LockScreenId` 是 Id，设备端按名字切屏 ⇒ 生成期解析

`SecurityConfig.LockScreenId` 是屏幕 **Id**（编辑器内部标识），而设备端 `hmi_screen_switch`
按**屏幕名字**匹配。两者的对应关系只存在于工程文件里，固件编译期不知道。

新建 `Core/CodeGen/LockScreenTargets.cs`（唯一来源）做生成期解析，与 `ScreenNavTargets`
同一条原则：「生成期能算出来的，就不要留到运行时猜」。四条分支：

| 配置 | 结果 | 诊断注释 |
|---|---|---|
| `LockScreenId` 命中 | 用该屏的**名字** | 无 |
| `LockScreenId` 配了但屏不存在 | **回退登录屏** | `⚠ … 在工程里不存在，已回退登录屏 "login"` |
| 未配 `LockScreenId` | 回退登录屏（与 `hmi_login` 同一张候选表） | 无（不配是正常用法，不是问题） |
| 没有屏幕清单（`Generate(config)` 简化重载） | 空串 ⇒ 不切屏 | `⚠ 生成时未提供屏幕清单…` |

**回退目标刻意选登录屏而不是"系统默认锁屏（黑屏+时间）"**（模型注释里那么写的）：
会话超时的语义是"这个人已经不被信任了"，重新获得信任的唯一途径就是登录。选登录屏还带来一个
实际好处——**不配 `LockScreenId` 的工程也有正确行为**。而"黑屏+时间"要求固件实现一块并不存在的
系统锁屏，本轮不做。

**解析不出来也不是错误**：`ScreenName` 为 `null` 时设备端**仍然清除会话**，只是不切屏。
安全边界不受影响（下一次权限判定照样拒绝），丢的只是"屏幕自己跳走"这个视觉提示。

## 5. 审计：落点固定，且**刻意不下发**那两个字段

设备端审计只记**安全事件**（会话建立 / 登录被拒 / 锁定 / 超时 / 登出），落盘到
`/mnt/UDISK/hmi/audit.log`（唯一持久可写分区，与 `assets/`、`fonts/`、`comm_config.ini` 同级），
超过 256 KB 滚成 `.1`（只保留一代——审计的价值是"最近发生过什么"，无上限增长会把唯一持久分区写满）。

**两个字段都不下发**，理由各不相同：

1. `EnableAuditLog` —— 它是**编辑器侧**的操作日志开关（`AuthenticationService.RecordAudit`），
   而编辑器的语义是「**认证事件始终记录，不受它影响**」（安全合规要求）。
   设备端只有认证类审计 ⇒ 它在设备端**没有可作用的对象**，下发一个不生效的宏是误导。
   （这一条是任务设计时的修正：原计划"下发 `HMI_SEC_AUDIT_LOG`"是错的。）
2. `AuditLogStoragePath` —— 它是**编辑器本机**的路径（`AuditLogFilePath` 直接拿它 `fopen`）。
   发给设备就是**跨机路径**：用户填 `D:\logs\audit.log` 在设备上必然写不进去，而且失败是静默的。

两件事都写进了生成头的可见注释（`AppendAuditPolicy`），免得后来人"好心"把它们补上。
`SecurityConfigCodeGenTests.AuditPolicy_IsDeliberatelyNotEmitted_ButExplained` 守着这条，
并反向守"字段本身没被删掉"。

**时间戳用墙钟、超时用单调 tick** ——这两件事不能混：审计必须人能读懂（tick 的起点是开机时刻），
超时判定必须单调（NTP 校时会让"锁 15 分钟"变成锁 3 分钟，项目里已有一次墙钟回卷的教训）。

## 6. 决定四：派生开关下沉到生成头（拒绝第二份判定）

模拟器侧要读"超时是否启用"。如果它按 `ENFORCE && AUTO_LOGOUT && TIMEOUT_MIN > 0` 再算一遍，
就长出了**第二份判定**——漂移的表现是「预览会跳锁屏、设备不会」，归因成本极高。

所以在生成头里发射：

```c
#if HMI_SEC_ENFORCE && HMI_SEC_AUTO_LOGOUT && (HMI_SEC_SESSION_TIMEOUT_MIN > 0)
#define HMI_SEC_SESSION_ENABLED 1
#else
#define HMI_SEC_SESSION_ENABLED 0
#endif
```

固件（`hmi_actions.c`）与模拟器（生成的 `main.c`）**读同一个名字**，
各自不再推导。放在该段末尾而不是文件开头：`HMI_SEC_ENFORCE` 可被 `-DHMI_SEC_ENFORCE=0` 覆盖，
只有在那一行求值才能反映最终的 `ENFORCE`。

## 7. 验证

| 项 | 结果 |
|---|---|
| Core 定向（生成器 / 锁屏解析 / 模拟器会话契约 / 链接契约） | 全绿（会话契约 6 项） |
| 固件 host 测试（7 步） | 全绿，新增会话/审计 **56 项**（原 51 + 顺序判据 5） |
| 负向验证 | **9/9 命中**（5 组 C# + 4 组 C），还原后复跑全绿 |
| 全量 `ci-gate.sh` | 见 `project-status.md` 验证记录 |

**教训一（第一版写错、已修）：派生开关的取值在 C# 里断言不出。**
文本里 `#if` 之后永远是字面的 `1`（`0` 在 `#else` 分支里），断言"紧跟 `#if` 的是几"是**恒真判据**。
正确分工：C# 守"派生块形态 + 只有一处推导"，取值交给真编译器
（`hmi_actions_session_test.c` 断言 `HMI_SEC_SESSION_ENABLED == 1`，并验证超时行为真的发生）。

**教训二：首轮负向验证 3/8 没被捕获 —— 三次都不是"验证失败"，是判据或注入有洞。**
三种成因各命中一次（"红数少于预期也要查"的实例）：

| 组 | 现象 | 真因 | 修法 |
|---|---|---|---|
| 模拟器权限门顺序 | 注入后**仍然全绿** | **注入与它声称的缺陷不等价**：只把 `expire` 挪到 `touch` 之后，它仍在 `decide` 之前 ⇒ 判据**正确地**保持绿 | 换成"打点先于评估"这个**真实的**缺陷；并补上 `expireAt < touchAt` —— 原来根本没人守这条顺序，是个真洞 |
| 固件超时判据 `>=` | 红了，但点名的是别的用例 | **needle 写错**：消息里是 `**恰好**达到阈值…`，needle 少了 `**` | 去掉 `check()` 消息里的 `**`（printf 文本用 markdown 强调本来就无意义），让 needle 是精确子串 |
| 权限判定不打点 | 红了，但点名的是别的用例 | **判据挑弱了**：真正红的是紧邻的 `hmi_session_poll() == 0`，而 needle 挑的是 `hmi_is_logged_in() == 1` | 把判据换成 poll 那条，并留注释写明为什么前者不能当判据 |

第三条值得单说，因为它是一条**通用**结论：`hmi_is_logged_in()` 是**惰性**的 —— 超时只在
"有人来问"时才被评估。所以任何"会话是否已超时"的断言都**必须触发一次评估**
（`hmi_session_poll()` 或一次权限判定），拿状态读取函数当判据永远是空转的：
打点缺失、判据写反，它都照样为真。

（首轮 3 组未捕获 ⇒ 修判据与注入 ⇒ 复跑 9/9 全中。**未捕获的那 3 组里藏着一个真漏洞**：
`expire` 必须先于 `touch` 这件事两侧都没人守，而破坏它的后果是"超时永不触发"。）

新增测试：

| 文件 | 内容 |
|---|---|
| `tests/HmiEditor.Core.Tests/LockScreenTargetsTests.cs` | 锁屏解析 8 项（命中 / Id 不存在 / 未配 / 无登录屏 / 无屏幕清单 / null config / 候选表顺序） |
| `tests/HmiEditor.Core.Tests/SimulatorSessionContractTests.cs` | 模拟器会话契约 6 项（`hmi_session_poll` 定义 + 主循环调用 / 评估先于打点、打点先于判定 / 切屏用名字不用 Id / 两条登录路径都起表 / logout 清表 / 设计模式门被编译掉） |
| `tests/HmiEditor.Core.Tests/SecurityConfigCodeGenTests.cs` | 新增 4 项（会话宏 / 派生块形态 / 审计不下发 / 锁屏解析诊断可见） |
| `firmware/t113/tests/hmi_actions_session_test.c` | 固件侧 56 项（阈值边界严格大于 / 权限判定即活动 / poll 不刷新活动时间 / **空闲超阈值后的首次判定必须拒绝** / 超时清会话+跳屏 / 锁屏屏缺失仍清会话 / 审计文件内容与时间戳形态） |

## 8. 遗留

- **`EnableTamperDetection` 仍然零消费**：`-DHMI_TAMPER_DETECTION` 下发了，固件不读。
  做成闭环需要先定义"防什么篡改、校验对象是谁"（`/rom` 是只读 squashfs）、以及
  "检测到异常后怎么办"。属于**新功能**而非补缺口，故本轮不做。
  生成器里那句注释**已重写**（不是"标注一下就算了"，是直接删掉那句行为承诺）：
  原文写的是"固件代码可通过 `#if HMI_TAMPER_DETECTION` 条件编译启用 Flash 扇区 CRC 校验"，
  而 grep 全库确认固件**一次都没引用过**它 —— 把没验证过的行为写进注释，后来人会当事实用
  （阶段 7 已因同一原因修过一处同类注释）。现注释首句就是「当前状态：下发了但零消费」，
  并列出要闭环得先定的三件事。
- **模拟器不做审计落盘**：审计是设备端行为（写 UDISK），预览时看 `stderr` 即可。
  刻意不为它加第三份文件写入实现。
- **模拟器不做失败锁定**（更早轮次登记的刻意差异，未变）。
- 会话超时在**模拟器里很难手工验证**（要等 30 分钟）：可以临时把 `SessionTimeoutMin` 配成 1 分钟。
  自动化覆盖不了"窗口里真的跳屏了"这一段。
