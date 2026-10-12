# 权限模型下沉到设备执行链路（2026-09-16）

> 承接 `docs/2026-09-14-optimization-directions.md` 的 **P2-2** 前半：
> 「`hmi_login` 接入 `UserRole` 模型」⇒ 把编辑器的权限模型真正**下沉**到固件动作链路，
> 让「越权动作被拦」在设备端真实生效。**真实凭据校验仍未做**（见 §8 遗留）。

## 1. 问题：模型齐了，设备端不认识它

编辑器侧权限体系一直是完整的：

| 部位 | 状态 |
|---|---|
| `Core/Models/User/Permission.cs` | 9 个权限位（`[Flags]`，`All = 0x1FF`）✅ |
| `Core/Models/User/UserRole.cs` | 3 个预置角色（Operator 3 / Engineer 7 / Admin 15 级）✅ |
| `Core/Models/SecurityConfig.cs` | `OperationMode`（Designer/Operator/Maintenance/ReadOnly）✅ |
| `AuthenticationService` | 登录、锁定、超时、审计 ✅（编辑器进程内） |
| **设备端** | `hmi_actions.c` 只有 `s_logged_in` + `s_username`，注释写着「权限校验**留待后续**接入 UserRole」❌ |

于是 `hmi_login()` 的语义是"点亮一个布尔量"，`hmi_show_alarms()` 之类动作**任何人都能触发**。

## 2. 为什么不能在固件里手写一份权限表

最直接的写法是往 `hmi_actions.c` 里贴一组 `#define HMI_PERM_OPERATE_CONTROL 0x02u` + 角色掩码表。
**这是错的**，而且错得很安静：编辑器后来加了权限位（比如 `DebugMode`）或调了角色权限，
设备端那份不会跟着变 —— 表现是"新权限在设备上永远为假"，而代码里看不出任何异常。

⇒ 立契约：**设备端不得出现 `#define HMI_PERM_…`**（由 §6 的守卫钉住）。

## 3. 三个单一来源（编辑器侧）

| 关注点 | 唯一来源 | 谁消费 |
|---|---|---|
| 动作 → 所需权限 | `Core/CodeGen/ActionPermissionMap.cs` | ① 动作回调门（发射器）② 生成头的注释表 |
| 枚举 → C 宏名/字面量 | `Core/CodeGen/PermissionMacros.cs` | 生成头 + 发射器（**不得各自拼 `"HMI_PERM_"`**） |
| 头的整体内容 | `Core/CodeGen/SecurityConfigCodeGen.cs` | `SimulatorProjectExporter`（落盘 `generated_security.h`） |

`ActionPermissionMap.Entries` 是**双向完整**的：既不能漏 `ActionTypeMeta` 的任何一个动作，
也不能出现未声明的类型 —— 两条各有一个断言（见 §6）。

映射表本身只有两条"语义决定"，都写进了类注释，免得后人"顺手统一"：

| 动作 | 所需权限 | 为什么 |
|---|---|---|
| `ToggleBit`/`SetBit`/`ResetBit`/`MomentaryOn`/`MomentaryOff`/`SetVariable`/`ExecuteScript` | `OperateControl` | 会改设备状态 |
| `SwitchScreen`/`ShowTrends` | `ViewScreen` | 只是换屏/看图，最低档"能看画面" |
| `ShowAlarms` | `ViewAlarmHistory` | 报警列表含历史记录 |
| `Login`/`Logout` | **不门控** | 给 Logout 加门 ⇒「权限不够就登不出去」死锁；Login 更不能用门锁自己 |
| 未知类型 | **不门控** | 未知动作不产生任何调用，门控它没有意义（覆盖完整性由双向断言保证，不靠这条兜底） |

## 4. 生成物：`generated_security.h`

内容（全部由枚举/模型算出，**没有一个手写数值**）：

- `HMI_SEC_MODE_*`（`OperationMode` 全成员）+ `HMI_SEC_OPERATION_MODE`
- `HMI_SEC_ENFORCE`（0/1）—— `Designer` 下 0，其余模式 1；包在 `#ifndef` 里以便 `-D` 临时覆盖
- `HMI_PERM_*`（`Permission` 全成员，含 `NONE`/`ALL`）
- `HMI_SEC_ROLE_COUNT` / `HMI_SEC_ROLES_INIT` / `HMI_SEC_DEFAULT_ROLE_INDEX`
- 文件末尾一张**动作 → 权限**注释表（与发射器同源）

## 5. 三处消费点

### 5.1 固件宿主：`hmi_actions.{h,c}`

```c
/* hmi_actions.h —— 引用与缺省回退都在这一处 */
#if defined(__has_include)
#  if __has_include("generated_security.h")
#    include "generated_security.h"
#  endif
#endif
#ifndef HMI_SEC_ENFORCE          /* 缺头 ⇒ 设计模式：不门控 */
#  define HMI_SEC_ENFORCE 0
#  define HMI_SEC_ROLE_COUNT 0
#  define HMI_SEC_DEFAULT_ROLE_INDEX (-1)
#endif
```

回退**刻意不提供任何权限位数值** —— `ENFORCE=0` 时所有门都被编译掉，不需要知道位定义，
于是「C 侧不得另写权限位」这条在缺头时同样成立。与 `comm_core.c` 消费 `generated_comm.h`
是同一套路（`__has_include` 优先 + 缺省回退），行为对既有工程零破坏。

会话侧新增：`hmi_login_as_role(user, role_id)`（**唯一接缝**，登录屏/脚本/未来的凭据校验都走它）、
`hmi_has_permission`、`hmi_require_permission`、`hmi_current_role`、`hmi_current_level`。
`hmi_logout` 把 mask/role/level 一并复位。

**未登录语义只折算一处**（`hmi_has_permission`）：

```c
const unsigned int mask = s_logged_in ? s_perm_mask : (unsigned int)HMI_PERM_VIEW_SCREEN;
```

即"未登录 = 能看画面、不能操作控件"。"未登录"的语义散进每个动作是这类系统的经典退化路径。

### 5.2 发射器：动作回调门

`CodeGenerator.AppendActionBody` 首行，**仅当** `SecurityConfig.OperationMode != Designer`：

```c
if (!hmi_require_permission(HMI_PERM_OPERATE_CONTROL)) return;  /* 取反 需要 OperateControl */
```

两点刻意如此：

- **默认（Designer）零变化**：不发射门 ⇒ 历史导出产物逐字节不变，既有对拍/回归测试零改动。
  项目的 `SecurityConfig` 默认就是 Designer（有断言钉住），语义也一致（"设计模式下所有操作可用"）。
- **`project == null` 时不发射**：与既有的 `SwitchScreen/ExecuteScript` 悬空目标校验同一约定
  （null = 不校验），免得"少传一个参数"从"少条注释"升级成"行为变了"。

### 5.3 生成模块前导

生成代码里出现 `HMI_PERM_*` 就必须能看到定义，所以 `generated_hmi.c` 前导也加了同样的
`__has_include("generated_security.h")` 块（设计模式下不发门 ⇒ 缺这份头也能编译）。

## 6. 测试与守卫

C# 侧 **37 条**（`ActionPermissionMapTests` 13 + `SecurityConfigCodeGenTests` 24）：

| 关注 | 断言 |
|---|---|
| 映射双向完整 | 反射 `ActionTypeMeta` 常量，缺失/多余/重复各一条 |
| **映射语义正确** | 与一张**独立写出来的**期望表逐项对拍（见下方"§7 暴露的缺口"） |
| 会话动作不被门控、未知动作不炸 | `Required(Login/Logout/未知/null)` = null |
| 门的发射 | 每个受管动作在 Operator 下**逐个**断言宏名正确；Login/Logout 不发射 |
| 默认零变化 | 遍历全部动作，Designer 下不得出现 `if (!hmi_require_permission` |
| 门不得写死数值 | 门的实参必须是 `HMI_PERM_` 前缀 |
| 头与模型对拍 | 权限位/运行模式**现算一遍再比**（不是快照）；角色表 id/name/level/mask 逐字段 |
| 默认角色回退要留痕 | 未知 `DefaultRoleId` ⇒ 索引 0 **且**写出警告注释 |
| 导出配套 | `Export` 产出 `generated_security.h`，且 `generated_hmi.c` 里的门引用它提供的宏 |
| 设备端不手写权限位 | 扫 `firmware/**`（排除 vendored SDK）只允许生成物文件出现权限位定义；**判据自身另有 5 正例 + 6 反例**钉住 |
| 固件消费生成头 | `hmi_actions.h` 有 `__has_include` + 回退；`hmi_actions.c` **不得**再写一份 |
| fixture 真实性 | `tests/fixtures/generated_security.h` 必须逐字节等于生成器输出 |

固件侧 **34 条断言**（`firmware/t113/tests/hmi_actions_permission_test.c`，接进 `run_host_tests.sh` 第 4 步，
`ci-gate.sh` 自动覆盖）：未登录拒绝受限动作 + **受限动作被短路（无切屏副作用）** + 默认角色登录放行
+ 按角色 Id 升/降权 + 未知角色登录失败**不动现有会话** + logout 复位 + `hmi_current_level`。
权限模型由 `tests/fixtures/generated_security.h` 提供 —— 它是**生成器的真实产物**（§6 末行断言钉住），
所以 C 测试测到的角色表就是设备端将来拿到的那一份，不是手抄的。

## 7. 负向验证（6 组）

判据：每条注入都必须让**指定**测试变红 —— 不红就说明守卫是空的。
脚本是一次性的（`.refactor-tmp/nv-permission.py`，随本轮清理）：默认只打印计划，`--run` 才注入；
每条跑完**立刻还原**，跑前/跑后各确认一次基线全绿。下表"注入"列即完整的注入清单，
照它可原样复现（`find` 用正则、命中数必须恰好 1，否则中止）。

| # | 注入 | 期望命中 | 实测 |
|---|---|---|---|
| a | `ActionPermissionMap` 里 `ToggleBit` 的权限改成 `null` | `Entries_MatchIntendedPermissions_Exactly` | ✅ 3 条红 |
| b | `ActionPermissionMap` 删掉 `ShowTrends` 一条 | `Entries_CoverDeclaredActionTypes_BothDirections` | ✅ 3 条红 |
| c | 发射器忽略 `OperationMode`，无条件发门 | `Gate_DesignerMode_IsNeverEmitted` | ✅ 1 条红 |
| d | `hmi_actions.h` 回退分支加一行 `#  define HMI_PERM_…` | `Firmware_DoesNotHandWritePermissionBits` | ✅ 1 条红（**修判据后**，见 §7.1） |
| e | fixture 改一个角色掩码（`0x04B` → `0x05B`） | `HostTestFixture_MatchesGeneratorOutput` | ✅ 1 条红 |
| f | `hmi_has_permission` 去掉"未登录折算" | C host 测试 | ✅ 34 通过 → **33 通过 / 1 失败** |

a / b 各自额外红的两条（`Export_WritesSecurityHeader_AndGeneratedCodeUsesIt`、
`HostTestFixture_MatchesGeneratorOutput`）不是噪声而是**连锁证据**：改了映射表 ⇒ 生成头内容变 ⇒
"导出产物引用了它"与"fixture 逐字节等于产物"必然同时红。三处同源，一改必全动 ——
如果它们**没**跟着红，才说明生成物不是同源算出来的。

### 7.1 NV 暴露的两个洞（均已修）

| 洞 | 症状 | 修法 |
|---|---|---|
| **映射语义无人钉住** | `Gate_OperatorMode_…` 这类断言，两侧都取自 `Entries`（表给期望、发射器读表）⇒ 把 `ToggleBit` 的权限改成 `null` 只是把它从 `Where(e => e.Permission is not null)` 里**过滤掉**，测试照样全绿 | 新增 `Entries_MatchIntendedPermissions_Exactly`：一份**独立写出**的期望表（12 项）与映射逐项对拍，并断言期望表自身双向完整 |
| **守卫认不出 `#  define` 风格** | 原判据是 `line.TrimStart().StartsWith("#define HMI_PERM_")`，而设备端回退块**自己的风格就是 `#  define`**（与相邻 `#  if` / `#  endif` 对齐）。照这个风格手写一位权限位，守卫**完全看不到** —— NV (d) 第一次跑就是 0 红 | 判据收进 `PermissionMacros.LooksLikeHandWrittenDefinition`（正则 `^\s*#\s*define\s+HMI_PERM_`），并给判据本身加 **5 正例 / 6 反例**（`HandWrittenPermissionBit_Detection_…`）：判据一旦退化成"什么都匹配不到"，守卫会立刻红，而不是永远绿 |

两条同属一类：**守卫写得像样，但"断言的对象"和"判据的宽度"各有一个盲区**。
第一条是断言**自洽**（两侧同源）导致改错不可见；第二条是判据**比现实窄**（少考虑一种同义写法）。
读代码看不出来 —— 代码本身没错 —— 只有真去注入一次才会露出来。

## 8. 遗留

1. ~~**真实凭据校验未实现**（P2-2 后半）：需要凭据存储 + 哈希下发 + 登录屏契约。~~
   **已关闭（2026-09-16）** ⇒ `docs/2026-09-16-runtime-credential-verification.md`
   （四环归因 + 三刀切法 + 跨语言算法契约）。要点：那四环此前**一次都没通过** ——
   凭据来源不落盘、下发只有角色表、设备端无哈希实现、登录屏取值从未发射
   （生成代码里一次都没出现过 `lv_textarea_get_text`）。原描述的"按 `DefaultRoleId` 直接授权"
   现在**只在工程未配运行时用户表时**保留；配了用户表则 `hmi_login` 只跳屏、不授权。
   **权限门本身是真实生效的**（§6 已覆盖）。
2. ~~**模拟器侧 4 个动作 + `hmi_require_permission` 只有 `extern` 没有定义**（本轮新发现的缺陷）~~
   **已关闭** ⇒ `docs/2026-09-16-simulator-export-link-selfcontained.md`。
   守卫是 `SimulatorLinkContractTests`：从生成代码**反推**它要求宿主提供什么（穷举而非逐个列名字），
   于是新增宿主 API 调用点时漏补定义会在单测阶段变红，而不是等 `cmake` 链接期。
3. `firmware/t113/tests/run_host_tests.sh` 的 gcc 产物原本落在固定名 `/tmp/hmi_*` 且从不清理
   （本机 `/tmp` 仅 10 MB）—— 本轮顺手加了 `trap … EXIT` 清理。
