/*
 * hmi_actions.h — 系统级动作 + 会话/权限 API
 *
 * 由编辑器代码生成器在动作回调中调用（ShowAlarms/ShowTrends/Login/Logout）。
 * 屏幕切换复用宿主 screen_manager 的 hmi_screen_switch（按名字匹配生成代码的屏名）。
 *
 * 会话与权限：**权限位与角色表不在这里定义** —— 它们来自编辑器生成的
 * generated_security.h（本模块用 __has_include 优先引用；缺省按"设计模式：不门控"回退，
 * 见 hmi_actions.c 顶部）。这样"哪些位有效、每个角色有哪些位"始终只有编辑器一处来源。
 *
 * 凭据校验（2026-09-16 落地）：**运行时用户表**由编辑器随工程下发到 generated_security.h
 * （HMI_SEC_USERS_INIT，凭据为 "Base64Salt:Base64Hash"），本模块用 hmi_crypto.c 的
 * SHA-256 迭代哈希校验。两条入口，语义不同：
 *   - hmi_login_submit(user, pass)：登录屏提交凭据 → 校验 → 通过后走 hmi_login_as_role
 *   - hmi_login()：工程**没有**用户表时才按 SecurityConfig.DefaultRoleId 自动授权（既有行为）；
 *     配了用户表时它只负责跳到登录屏，**绝不**自动授权 —— 否则登录屏还没提交、会话已建立
 *
 * 会话超时 + 审计（2026-09-16 落地）：会话超时与编辑器 AuthenticationService.CheckPermission
 * **逐条同构** —— 「有人在问权限」本身就算活动（两边都没有输入事件钩子）、判据严格大于、
 * 超时即清会话 + 跳锁屏 + **拒绝本次判定**。活动打点只有 hmi_has_permission 一处，
 * 刻意不接 lv_indev 读回调；hmi_session_poll() 供主循环周期评估（让"没人动"能在屏幕上自己体现）。
 * 审计（hmi_actions.c 的 hmi_audit）把安全事件落到 HMI_SEC_AUDIT_PATH：只记认证/会话事件，
 * 且**恒开** —— 与编辑器"认证事件始终记录、不受 EnableAuditLog 影响"同义。
 */
#ifndef HMI_ACTIONS_H
#define HMI_ACTIONS_H

/* ── 权限模型：编辑器生成的 generated_security.h 优先，缺省回退"设计模式：不门控" ──
 * 头缺失时**不提供任何权限位数值**：ENFORCE=0 时所有门都被编译掉，不需要知道位定义，
 * 于是"C 侧不得另写一份权限位"这条不变量在缺头时同样成立。
 * 头存在时它总是同时给出 ENFORCE / ROLE_COUNT / ROLE_S* / DEFAULT_ROLE_INDEX，
 * 因此用 HMI_SEC_ENFORCE 作唯一判据即可。 */
#if defined(__has_include)
#  if __has_include("generated_security.h")
#    include "generated_security.h"
#  endif
#endif
#ifndef HMI_SEC_ENFORCE
#  define HMI_SEC_ENFORCE 0
#  define HMI_SEC_ROLE_COUNT 0
#  define HMI_SEC_DEFAULT_ROLE_INDEX (-1)
/* 无用户表：hmi_login_submit 一律拒绝（见 hmi_actions.c）。
 * 必须在这里给出 —— 否则"头不存在"这条路径上 `#if HMI_SEC_USER_COUNT` 会撞未定义标识符，
 * 而"头在、表空"那条路径由生成头给出同名 0，两条路径的语义才一致。 */
#  define HMI_SEC_USER_COUNT 0
/* 会话超时 / 锁屏 / 审计路径：同族的"必须在这里给出"（理由同上）——
 * 否则"头不存在"路径上 `#if HMI_SEC_AUTO_LOGOUT && HMI_SEC_SESSION_TIMEOUT_MIN > 0`
 * 会被预处理器当成 `0 && 0 > 0`（未定义标识符按 0 处置），产物行为虽对，
 * 但那是**巧合**而非契约，且 -Wundef 下会报警。
 * 缺头 = 设计模式（不门控）⇒ 超时没有安全含义，故给"不超时"，与
 * "头在、但 OperationMode=Designer"那条路径（HMI_SEC_ENFORCE=0）语义一致。 */
#  define HMI_SEC_SESSION_TIMEOUT_MIN 0
#  define HMI_SEC_AUTO_LOGOUT 0
#  define HMI_SEC_LOCK_SCREEN_NAME ""
/* 派生开关（生成头也定义它，取值同为 0/1）：两个消费方读同一个名字，不许各自推导。 */
#  define HMI_SEC_SESSION_ENABLED 0
#endif

/* 显示报警列表视图（尝试切换到名为 alarm/alarm_list/报警列表 的屏幕） */
void hmi_show_alarms(void);

/* 显示趋势图视图（尝试切换到名为 trend/trend_view/趋势图 的屏幕） */
void hmi_show_trends(void);

/*
 * 请求登录 —— 切到工程里的登录屏（如有）。语义按"有没有运行时用户表"分流：
 *   - 无用户表（HMI_SEC_USER_COUNT == 0）：同时按默认角色激活会话（既有行为，产物不变）
 *   - 有用户表：只跳屏、**不授权** —— 会话必须由登录屏经 hmi_login_submit 校验后建立
 */
void hmi_login(void);

/*
 * 用登录屏上输入的凭据登录。
 *
 * 返回 0 = 成功（会话已建立，权限取自用户表里该用户的角色）；-1 = 失败，包括：
 * 用户未知 / 账户停用 / 口令错误 / 连续失败已锁定 / 工程未下发用户表。
 *
 * user 为用户名（**大小写敏感**，与用户表逐字节比较）；
 * password 为 UTF-8 C 字符串，按 UTF-8 字节参与哈希（与编辑器 RuntimePasswordHasher 一致）。
 * 校验通过后仍走 hmi_login_as_role —— 权限来源只有那一条路，这里不另建会话。
 */
int hmi_login_submit(const char* user, const char* password);

/* 清除当前用户会话并返回默认屏幕 */
void hmi_logout(void);

/*
 * 周期性评估会话超时 —— 供主循环每轮调用（设备端 app/main.c、导出模拟器 main.c）。
 * 返回 1 = 本次调用判定超时（会话已清除、锁屏屏已切）；0 = 无会话或尚未超时。
 *
 * 惰性评估（hmi_has_permission 入口）已经保证"超时之后的下一次操作必被拒"，
 * 本函数补的是另一半：让"没人动"这件事能在屏幕上**自己**体现出来 ——
 * 现场的人靠"看到锁屏"才知道自己需要重新登录。无会话时是空操作，
 * 与 hmi_engine_poll / sys_settings_boot_poll 同级。
 *
 * 它**不**刷新活动时间（只有 hmi_has_permission 会刷新）：周期函数若能刷新，
 * 超时就永远不会触发。
 */
int hmi_session_poll(void);

/* ── 会话查询 API ── */
int         hmi_is_logged_in(void);
const char* hmi_current_user(void);

/**
 * 写一条**审计**（唯一落点：串口 ＋ append-only 文件 `<HMI_SEC_AUDIT_PATH>`，带轮转）。
 *
 * <p>阶段 279 起对 .so 公开：生成的报警搁置代码也走这一处 —— 免得"会话审计"与"报警搁置审计"
 * 各写一个文件、各有各的格式。`role` 传空串即可（.so 侧不知道角色；要角色得由宿主补）。</p>
 */
void hmi_audit(const char* event, const char* user, const char* role, const char* detail);
const char* hmi_current_role(void);   /* 未登录返回 "" */
int         hmi_current_level(void);  /* 角色安全等级 0-15；未登录 -1 */

/* ── 权限 API ── */
/*
 * 按角色 Id 建立会话（role_id 为空则用 generated_security.h 的默认角色）。
 * 返回 0 成功；-1 = 角色未知或没有角色表（此时会话不变）。
 * 这是登录屏 / 脚本 / 未来凭据校验接入权限体系的**唯一接缝**。
 */
int hmi_login_as_role(const char* user, const char* role_id);

/* 当前会话是否含**全部**给定位。未登录时只含"查看画面"；HMI_SEC_ENFORCE=0 时恒为 1。 */
int hmi_has_permission(unsigned int bits);

/* 动作门用：无权限则记日志并返回 0，有权限返回 1。 */
int hmi_require_permission(unsigned int bits);

#endif /* HMI_ACTIONS_H */
