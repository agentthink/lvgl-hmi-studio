/* ============================================================
 * 设备端安全 / 权限模型 —— 由 HMI Editor 生成，请勿手改
 *   单一来源：Permission 枚举 + UserRole 预置角色 + HmiProject.SecurityConfig
 *   消费方：firmware runtime/hmi_actions.c（__has_include 优先引用，
 *           缺省回退「设计模式：不门控」）+ 生成的动作回调门 + 导出的模拟器 main.c
 *           —— 三者的权限判定都调这里的 hmi_perm_allows（唯一实现）。
 * ============================================================ */
#ifndef HMI_GENERATED_SECURITY_H
#define HMI_GENERATED_SECURITY_H

/* ── 运行模式（OperationMode）。设计模式下所有操作可用，其余模式受权限约束 ── */
#define HMI_SEC_MODE_DESIGNER      0
#define HMI_SEC_MODE_OPERATOR      1
#define HMI_SEC_MODE_MAINTENANCE   2
#define HMI_SEC_MODE_READ_ONLY     3

#define HMI_SEC_OPERATION_MODE HMI_SEC_MODE_OPERATOR  /* Operator */

/* 是否执行权限门：Designer(设计模式) 下不门控，导出产物与历史逐字节一致。
 * 其余模式一律门控；调试时可用 -DHMI_SEC_ENFORCE=0 从命令行临时关掉。 */
#ifndef HMI_SEC_ENFORCE
#define HMI_SEC_ENFORCE 1
#endif

/* ── 权限位（Permission 枚举）。设备端**不得**另写一份数值 ── */
#define HMI_PERM_NONE               0x000u
#define HMI_PERM_VIEW_SCREEN        0x001u
#define HMI_PERM_OPERATE_CONTROL    0x002u
#define HMI_PERM_EDIT_RECIPE        0x004u
#define HMI_PERM_ACKNOWLEDGE_ALARM  0x008u
#define HMI_PERM_CHANGE_CONFIG      0x010u
#define HMI_PERM_MANAGE_USERS       0x020u
#define HMI_PERM_VIEW_ALARM_HISTORY 0x040u
#define HMI_PERM_EXPORT_DATA        0x080u
#define HMI_PERM_DEBUG_MODE         0x100u
#define HMI_PERM_SHELVE_ALARM       0x200u
#define HMI_PERM_ALL                0x3FFu

/* ── 权限判定（唯一实现）──
 * 设备端 hmi_has_permission 与模拟器 hmi_require_permission 都调它，
 * 谁也不许再自己写「未登录折算」或 (mask & bits) == bits。 */
#if HMI_SEC_ENFORCE
static inline int hmi_perm_allows(int logged_in, unsigned int perm_mask, unsigned int bits)
{
    if (bits == 0u) return 1;
    /* 未登录：只折算「查看画面」（能浏览/换屏，不能操作控件或改配置）—— 这条语义只在这里 */
    const unsigned int mask = logged_in ? perm_mask : (unsigned int)HMI_PERM_VIEW_SCREEN;
    return (mask & bits) == bits;
}
#endif /* HMI_SEC_ENFORCE */

/* ── 角色表（UserRole 预置角色），掩码为上面权限位的或 ──
 * 固件用它初始化角色数组并按 HMI_SEC_ROLE_COUNT 线性查表。 */
#define HMI_SEC_ROLE_COUNT 3
#define HMI_SEC_ROLES_INIT \
    { "role-operator",         "Operator",         3, 0x24Bu }, \
    { "role-engineer",         "Engineer",         7, 0x2DFu }, \
    { "role-admin",            "Administrator",   15, 0x3FFu }

/* 默认角色：凭据校验落地前，hmi_login 用它激活会话（见 hmi_actions.c 注释） */
#define HMI_SEC_DEFAULT_ROLE_INDEX 0  /* role-operator */
#define HMI_SEC_DEFAULT_ROLE_MASK 0x24Bu  /* Operator */

/* ── 运行时用户表（SecurityConfig.RuntimeUsers）—— 设备登录屏的凭据来源 ──
 * 凭据格式 "Base64Salt:Base64Hash"；算法 = SHA256 迭代 HMI_SEC_HASH_ITERATIONS 次，
 * 起始输入为 salt ‖ utf8(password)。两侧唯一实现：
 *   编辑器 Core/Security/RuntimePasswordHasher.cs
 *   固件   app/runtime/hmi_crypto.c
 * 同一性由**同一组固定向量**两侧各测一遍钉住，改算法必须同时改两侧与向量。
 * 表为空（USER_COUNT == 0）⇒ hmi_login_submit 一律拒绝，
 * hmi_login 退回「按默认角色授权」（既有行为，语义不变）。 */
#define HMI_SEC_USER_COUNT 3
#define HMI_SEC_HASH_ITERATIONS 1000
#define HMI_SEC_SALT_BYTES 16
#define HMI_SEC_PASSWORD_MIN_LEN 6
#define HMI_SEC_MAX_LOGIN_ATTEMPTS 5
#define HMI_SEC_LOCKOUT_MIN 15
#define HMI_SEC_USERS_INIT \
    { "op", "role-operator", "AAECAwQFBgcICQoLDA0ODw==:wNu1hDVTcb2XOXidGgkb4c9GEr6wxjDGAGaH2VWrOLk=", 1 }, \
    { "eng", "role-engineer", "EBESExQVFhcYGRobHB0eHw==:Crs25Mtrbu7xzjNeHEL7A2pa0pc/PcJ067I3ohZ0/U4=", 1 }, \
    { "dis", "role-operator", "AAECAwQFBgcICQoLDA0ODw==:wNu1hDVTcb2XOXidGgkb4c9GEr6wxjDGAGaH2VWrOLk=", 0 }

/* ── 会话超时（SecurityConfig.SessionTimeoutMin / AutoLogoutEnabled / LockScreenId）──
 * 设备端 hmi_has_permission 入口做**惰性评估**，另有 hmi_session_poll() 供主循环周期评估
 * （这样"没人动"才能在自己跳到锁屏屏上体现，而不是等下一次有人来操作才被发现）。
 * 与编辑器 AuthenticationService.CheckPermission **逐条同构**，三条都不能改：
 *   ① 「有人在问权限」本身就算活动（两边都没有输入事件钩子）
 *   ② 判据是**严格大于** elapsed > timeout（毫秒整数比较，与 TotalMinutes > TimeoutMinutes 同义）
 *   ③ 超时 ⇒ 清会话 + 跳锁屏 + **拒绝本次判定**（顺序不能反） */
#define HMI_SEC_SESSION_TIMEOUT_MIN 30  /* <= 0 表示不超时（编辑器 Minimum=1，这是手改 JSON 的兜底） */
#define HMI_SEC_AUTO_LOGOUT 1  /* 0 = 关掉自动登出（编辑器 AutoLogoutEnabled） */
#define HMI_SEC_LOCK_SCREEN_NAME "锁屏"  /* 由 SecurityConfig.LockScreenId 解析而来 */

/* 派生开关：会话超时是否启用。**两个消费方（固件 hmi_actions.c / 导出模拟器 main.c）都读它** ——
 * 各自再按 ENFORCE / AUTO_LOGOUT / TIMEOUT_MIN 算一遍必然漂移，而漂移的表现是
 * 「预览会跳锁屏、设备不会」（或反之），归因成本极高。
 * 放在这里而不是头文件开头：HMI_SEC_ENFORCE 可被 -DHMI_SEC_ENFORCE=0 覆盖，
 * 只有在本行求值才能反映最终的 ENFORCE。 */
#if HMI_SEC_ENFORCE && HMI_SEC_AUTO_LOGOUT && (HMI_SEC_SESSION_TIMEOUT_MIN > 0)
#define HMI_SEC_SESSION_ENABLED 1
#else
#define HMI_SEC_SESSION_ENABLED 0
#endif

/* ── 审计日志（SecurityConfig.EnableAuditLog / AuditLogStoragePath）—— 刻意不下发 ──
 *   1) EnableAuditLog 是**编辑器侧**的操作日志开关（AuthenticationService.RecordAudit），
 *      而编辑器的语义是「**认证事件始终记录，不受它影响**」（安全合规要求）。
 *      设备端只有认证类审计 ⇒ 它在设备端没有可作用的对象，下发一个不生效的宏是误导。
 *   2) AuditLogStoragePath 是**编辑器本机**的路径（直接 fopen）。发给设备就是跨机路径：
 *      用户填 D:\logs\audit.log 在设备上必然静默写不进去。
 *   ⇒ 设备端落点固定 /mnt/UDISK/hmi/audit.log（见 app/runtime/hmi_actions.c 的
 *      HMI_SEC_AUDIT_PATH，可用 -D 覆盖）；导出模拟器写自己输出目录下的 audit.log。 */

/* ── 动作 → 所需权限（与生成的动作回调门同源：ActionPermissionMap） ──
 * 未列出的动作（含 Login/Logout 与未知类型）不门控。
 *   ToggleBit      HMI_PERM_OPERATE_CONTROL  (OperateControl)   [取反]
 *   SetBit         HMI_PERM_OPERATE_CONTROL  (OperateControl)   [置位]
 *   ResetBit       HMI_PERM_OPERATE_CONTROL  (OperateControl)   [复位]
 *   MomentaryOn    HMI_PERM_OPERATE_CONTROL  (OperateControl)   [按1松0]
 *   MomentaryOff   HMI_PERM_OPERATE_CONTROL  (OperateControl)   [按0松1]
 *   SetVariable    HMI_PERM_OPERATE_CONTROL  (OperateControl)   [设置变量]
 *   ExecuteScript  HMI_PERM_OPERATE_CONTROL  (OperateControl)   [执行脚本]
 *   SwitchScreen   HMI_PERM_VIEW_SCREEN  (ViewScreen)   [切换屏幕]
 *   ShowTrends     HMI_PERM_VIEW_SCREEN  (ViewScreen)   [显示趋势]
 *   ShowAlarms     HMI_PERM_VIEW_ALARM_HISTORY  (ViewAlarmHistory)   [显示报警]
 *   Login          -  (不门控)   [登录]
 *   Logout         -  (不门控)   [登出]
 */
#endif /* HMI_GENERATED_SECURITY_H */
