# 口令下限的**单位**：一处漏网的码元口径 ＋ 一句对用户说谎的文案（阶段 168，闭环 `#83`）

> **一句话**：`SecurityConfig.PasswordMinLength` 的语义（模型注释、编辑器、设备端 `strlen`）是
> **UTF-8 字节数**，但 `AuthenticationService.SetUserPassword`（「用户管理 → 重置密码」那条产品路径）
> 仍按 **UTF-16 码元**（≈字符数）判 ⇒ **同一个口令两条产品路径结论相反**；而它抛出的文案
> `user.issue.password_too_short` 还写着「个字符 / characters」，参数却是字节数 ⇒
> 对 CJK 用户**同时**是"拒了不该拒的"＋"报了个不是自己输入长度的数"。

---

## 1. 现场（修复前）

| 位置 | 口径 | 下限 6 时对 `"电机"`（2 字 = 6 字节 = 2 码元） |
|---|---|---|
| `SecurityConfig.PasswordMinLength` 的**语义**（模型注释） | UTF-8 字节 | —— |
| `ProjectSettingsView.OnSetRuntimeUserPassword`（工程设置 → 运行时用户） | `Encoding.UTF8.GetByteCount` | **接受**（6 ≥ 6） |
| 设备端登录屏短路（`hmi_actions.c`） | `strlen`（字节） | **放行**（6 ≥ 6） |
| `AuthenticationService.SetUserPassword`（用户管理 → 重置密码） | **`plainPassword.Length`（码元）** | **拒绝**（2 < 6）← 漏网 |

可撞到的路径：`UserManagerView` 的「重置密码」对话框 → `_auth.SetUserPassword(...)` →
`UserAdminException` → 消息经 `ExternalText.FailureReason(ex)` **显示给用户**（阶段 97 补的 catch）。

文案实测（修复前）：

```
zh: "密码长度不足，最小要求 {0} 个字符（当前 {1}）"          ← 参数是字节数
en: "Password is too short: at least {0} characters required (currently {1})"
```

⇒ 用户输入 **1 个汉字**（3 字节）会被拒，提示却写「当前 **3**」——数字对不上他键入的长度，
而且单位写的是「字符」。**同一个键**在编辑器的另一条消息里（`...users.password.too.short`）
是诚实的：`"密码至少 {0} 个字节（一个汉字算 3 字节）"`。

---

## 2. 修复（三处）

1. `AuthenticationService.SetUserPassword`：改用 `Encoding.UTF8.GetByteCount(plainPassword)`，
   并把**字节数**作为 `actual` 传下去（与模型注释、编辑器、设备端四处一致）。
2. `UserAdminIssue.PasswordTooShort` 的**回落原文** ＋ 两本字典的 `user.issue.password_too_short`
   一起改成按字节说（zh 值必须与回落**一字不差** —— 既有判据 `PasswordFallbackMatchesZh_…` 钉着）：
   - zh：`密码长度不足，最小要求 {0} 个字节（当前 {1} 个字节，一个汉字算 3 个）`
   - en：`Password is too short: at least {0} bytes required (currently {1}; a CJK character counts as 3)`
3. `ProjectSettingsView.OnSetRuntimeUserPassword` 的 `<summary>`：原来写「长度按**字符数**判」，
   与紧随其后的 `GetByteCount` **相反** ⇒ 更正为字节，并写明它与设备端/服务侧同口径。

---

## 3. 判据与负控

| 判据 | 判什么 |
|---|---|
| `AuthenticationTests.SetUserPassword_MinimumLength_IsCountedInUtf8Bytes`（Core） | 下限 6、口令 `"电机"`（**前提自检**：字节 6 而码元 2）⇒ 必须能设、能登 |
| `AuthenticationTests.SetUserPassword_TooShort_ReportsBothCountsInBytes`（Core） | 真正过短（`"电a"` = 4 字节 / 2 码元）仍被拒，且 `Issue.Args` 里两个数都是**字节**（`6` / `4`，不是 `2`）⇒ 单位在**产出方**就被钉住 |
| `UserAdminIssueLocalizationGuardTests.PasswordTooShort_CopyNamesBytes_MatchingTheEnforcementUnit` | zh/en 文案必须写「字节 / bytes」且**不许**再出现「个字符 / characters」；**前提自检**：Core 回落原文也按字节说 |
| `UserAdminIssueLocalizationGuardTests.PasswordMinimum_IsCountedInBytes_AtBothEnforcementSites` | **源码契约**：两个执行点都必须 `GetByteCount`（去注释后判 —— 注释里正在讨论这件事） |

**负控 3 支**（`.workbuddy/tmp/s168-nc.py`，逐支独立日志 ＋ 还原 SHA256 逐字节相同）：

| 控制 | 注入 | 实测红在哪 |
|---|---|---|
| nc1 | `SetUserPassword` 退回码元 | Core 判据红：`"电机"` 直接被拒（⚠ 且注入态下消息说「字节」而数是**码元**，正是这场缺陷的两种病一起回来） |
| nc2 | 字典文案改回「字符」 | 文案单位判据红（`没写单位「字节」`） |
| nc3 | `ProjectSettingsView` 退回 `password.Length` | 执行点源码契约红（`没按 UTF-8 字节数判`） |

---

## 4. 与登记 `#20b` 的关系：那一条**不是**产品决策，是登记把两件事混为一谈

`#20b` 原文：「**设备端按字符数判**（若要把口令下限在**字符**意义上收紧）」，理由栏写「需 C 侧 UTF-8
码点计数 ＋ 现有凭据迁移方案（否则按字节放行过的 CJK 口令会直接登不上 = 把现场用户锁在门外）」。

**复核实测**（`firmware/t113/app/runtime/hmi_actions.c:395-405` 的源码注释，逐字）：

> 口令下限短路：省掉一次 1000 轮哈希。**这是省算力，不是密码策略执行点** —— 真实口令受
> `SecurityConfig.PasswordMinLength` 约束，一定不短于它。

⇒ 设备端那处 `strlen` **不承担策略**，它只是"别为明显过短的输入跑 1000 轮哈希"的短路，而且刻意
**不计入失败计数**（避免把锁定变成 DoS 面）。**收紧它零安全收益、只会拒绝合法旧口令** ⇒
`#20b` 按「**复核后不成立（登记有误：把省算力短路当成了策略执行点）**」结案。

**残留的、真正的问题是另一件事**（新登记 `#84`，产品决策）：
要不要把口令下限的**语义**从"字节"改成"字符"？—— 落点应当是**设置口令**那一处
（明文在场、用户在场，**收紧不会锁门**，旧哈希照旧可用），要改的是 `SecurityConfig` 的语义、
两处编辑器校验与生成的 `HMI_SEC_PASSWORD_MIN_LEN` 注释；设备端那处短路**保持字节**（宁可宽，不可锁门）。

---

## 5. 诚实边界

1. 判据只覆盖**两处**执行点（Core 服务 ＋ 编辑器）与设备端那处短路（源码契约在 §3 表里钉的是前两处；
   设备端由既有固件用例 `hmi_actions_credential_test.c` 覆盖"短于下限拒绝"）。若将来新增第三条设置口令的
   路径，需要**同时**接同一口径（本判据不会自动发现新路径）。
2. 判据不判**翻译质量** —— 它判的是"单位名与执行口径一致"；en 措辞是否地道仍属 `#27` 的边界。
3. 文案里保留了「一个汉字算 3 个」这句解释：它只在**字节**口径下成立；若哪天按 `#84` 改成字符口径，
   这句与两条判据会一起红 —— 这正是想要的效果（口径改动必须同时改文案与判据）。

---

## 6. 读数

- **门禁** 与 **全量 App.UiTests**：见登记册 §84。
- **产品代码**：3 个文件（`AuthenticationService`、`UserAdminIssue` 回落、`ProjectSettingsView` 注释）
  ＋ 两本字典各 1 条；
  **判据**：Core `AuthenticationTests` ＋2、App `UserAdminIssueLocalizationGuardTests` ＋2。
