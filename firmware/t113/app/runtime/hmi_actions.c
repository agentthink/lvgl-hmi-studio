/*
 * hmi_actions.c — 系统级动作 + 会话/权限实现
 *
 * 由编辑器代码生成器在动作回调中调用（ShowAlarms/ShowTrends/Login/Logout）。
 * 屏幕切换复用宿主 screen_manager 的 hmi_screen_switch（按名字匹配生成代码的屏名）。
 * 若工程未包含对应屏幕，则输出日志提示（不崩溃），保证导出代码始终可链接运行。
 *
 * ── 权限模型（2026-09-16 下沉） ──
 * 权限位 / 角色表来自编辑器生成的 generated_security.h，本文件不定义任何权限数值。
 * 头缺失时按"设计模式：不门控"回退 —— 导出工程没带这份头也能编译，且行为与历史一致。
 * 动作回调门（if (!hmi_require_permission(...)) return;）由生成器输出，仅在
 * HMI_SEC_ENFORCE=1 时存在，所以"设计模式产物逐字节不变"。
 *
 * ── 本文件不 include hmi_runtime.h ──
 * 宿主聚合头会拉进 lvgl.h，而主机侧单测（firmware/t113/tests）不建 LVGL 环境。
 * 所以这里只声明用到的两个宿主原语 + 自带同格式日志宏，使本文件可被 gcc 直接编译测试。
 */
#include "hmi_actions.h"
#include "hmi_crypto.h"
#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include <time.h>    /* 审计日志的时间戳（**墙钟**）。与超时判定用的单调 tick 是两件事，见 hmi_audit 注释 */

/* 宿主原语（screen_manager.c / LVGL 提供；主机测试自带假实现） */
int hmi_screen_switch(const char* name);
int hmi_screen_switch_idx(int index);

/* LVGL 的毫秒 tick（单调）。**只**用于"连续失败后的锁定时长" ——
 * 刻意不用墙钟：NTP 校时会让"锁 15 分钟"变成锁 3 分钟或锁 40 分钟
 * （项目里已有一次"墙钟回卷"的教训）。 */
extern uint32_t lv_tick_get(void);

/* 日志：格式与 hmi_runtime.h 的 HMI_LOG 一致（"[hmi] ..."，走 stderr） */
#ifndef HMI_ACT_LOG
#define HMI_ACT_LOG(fmt, ...) fprintf(stderr, "[hmi] " fmt "\n", ##__VA_ARGS__)
#endif

/* ── 生成的权限头 ──
 * 引用与缺省回退都在 hmi_actions.h 里（唯一一处），这样本文件、以及任何 include 本头的
 * 生成代码/登录屏，看到的权限宏与"缺省语义"完全一致。 */

/* ── 最小会话状态（进程内，无持久化） ── */
static char s_username[32];
static int  s_logged_in  = 0;
static int  s_role_index = -1;      /* 角色表下标；-1 = 未登录/未知 */
static unsigned int s_perm_mask = 0u; /* 仅登录后有意义（未登录的权限由 hmi_has_permission 折算） */

/* ── 审计日志 ──
 * 只记**安全事件**（会话建立 / 登录失败 / 锁定 / 超时 / 登出），不记调试输出 —— 后者走 HMI_ACT_LOG。
 * 刻意的分工：审计要能事后追责，所以它落盘、带时间戳、且**不受任何编译期日志级别影响**。
 *
 * **时间戳用墙钟（time/localtime），而"锁定时长 / 会话超时"用单调 tick —— 这两件事不能混：**
 *   - 审计：必须人能读懂。tick 的起点是开机时刻，写进日志没人知道那是几点。
 *   - 超时：必须单调。项目里已有一次墙钟回卷的教训（NTP 校时会让"锁 15 分钟"变成锁 3 分钟）。
 *
 * 落点由 HMI_SEC_AUDIT_PATH 给出。**生成期刻意不下发它**：落点本来就是消费方相关的
 * （设备端写唯一持久分区 UDISK，模拟器写自己的输出目录），生成头没有能力表达这种差异。
 * 超过大小上限就滚成 .1，只保留一代 —— 审计的价值是"最近发生过什么"，
 * 无上限增长会把唯一持久分区写满，那是设备变砖级别的事故。
 *
 * 为什么**不**受 SecurityConfig.EnableAuditLog 控制：编辑器侧那条语义是
 * 「**认证事件始终记录，不受它影响**」（安全合规要求，见 AuthenticationService.RecordAudit），
 * 而本文件记的全是认证/会话事件 ⇒ 在设备端跟着那个开关走反而会违反它。
 */
#ifndef HMI_SEC_AUDIT_PATH
#define HMI_SEC_AUDIT_PATH "/mnt/UDISK/hmi/audit.log"
#endif
#ifndef HMI_SEC_AUDIT_MAX_BYTES
#define HMI_SEC_AUDIT_MAX_BYTES (256L * 1024L)
#endif

void hmi_audit(const char* event, const char* user, const char* role, const char* detail)
{
    char stamp[32];
    time_t now = time(NULL);
    struct tm* tm_local = localtime(&now);

    if (tm_local != NULL)
        strftime(stamp, sizeof(stamp), "%Y-%m-%dT%H:%M:%S", tm_local);
    else
        snprintf(stamp, sizeof(stamp), "epoch=%ld", (long)now);

    /* 1) 先写串口 —— 落盘失败时这条记录不能凭空消失 */
    HMI_ACT_LOG("AUDIT %s %s user=%s role=%s %s",
                stamp, event, user ? user : "", role ? role : "", detail ? detail : "");

    /* 2) 再落盘。打不开就只留串口，且**每次都报** ——
     * 免得"配了审计却一直没有日志"变成一次静默失败（那正是本轮在修的问题类型）。 */
    {
        FILE* f = fopen(HMI_SEC_AUDIT_PATH, "a");
        if (f == NULL) {
            HMI_ACT_LOG("hmi_audit: 无法追加到 '%s'（目录不存在 / 无写权限）—— 本条只留在串口",
                        HMI_SEC_AUDIT_PATH);
            return;
        }

        (void)fseek(f, 0L, SEEK_END);
        if (ftell(f) > HMI_SEC_AUDIT_MAX_BYTES) {
            (void)fclose(f);
            (void)remove(HMI_SEC_AUDIT_PATH ".1");
            if (rename(HMI_SEC_AUDIT_PATH, HMI_SEC_AUDIT_PATH ".1") != 0)
                HMI_ACT_LOG("hmi_audit: 轮转失败（%s）", HMI_SEC_AUDIT_PATH);
            f = fopen(HMI_SEC_AUDIT_PATH, "a");
            if (f == NULL) return;
        }

        fprintf(f, "%s %s user=%s role=%s %s\n",
                stamp, event, user ? user : "", role ? role : "", detail ? detail : "");
        (void)fclose(f);
    }
}

/* ── 会话超时 ──
 * 与编辑器 AuthenticationService.CheckPermission **逐条同构**（三条都不能"顺手改"）：
 *   ① 「有人在问权限」本身就算活动 —— 两边都**没有**输入事件钩子，刷新活动时间的动作就发生在
 *      权限判定里。这不是为省事的妥协，而是编辑器侧既有的权威语义。
 *   ② 判据是**严格大于**（elapsed > timeout）—— 编辑器写的是 TotalMinutes > TimeoutMinutes。
 *   ③ 超时 ⇒ 清会话 + 跳锁屏 + **拒绝本次判定**。顺序不能反：先放行再登出等于把这次操作送出去。
 *
 * 只在 HMI_SEC_ENFORCE 下生效：设计模式（含缺 generated_security.h）不门控，超时没有安全含义，
 * 而"设计模式产物行为不变"是硬契约。
 *
 * **活动打点刻意不引入新的宿主原语**（不要求接 lv_indev 读回调）：生成代码里凡需要权限的动作
 * 都会走 hmi_require_permission ⇒ hmi_has_permission ⇒ 打点。ActionPermissionMap 的 12 个动作里
 * 只有 Login/Logout 不门控 —— 而登录本身会重置计时、登出本身会清除会话，覆盖是完整的。
 *
 * 是否启用由**生成头**的 HMI_SEC_SESSION_ENABLED 决定（缺头时由 hmi_actions.h 回退为 0）。
 * 刻意**不在这里**按 ENFORCE / AUTO_LOGOUT / TIMEOUT_MIN 重新推导 —— 导出的模拟器 main.c 读的是
 * 同一个名字；两处各推一遍必然漂移，而漂移的表现是"预览会跳锁屏、设备不会"，归因成本极高。
 */

#if HMI_SEC_SESSION_ENABLED
/* 会话截止时刻（tick 毫秒）。0 = 无会话 ⇒ 常规路径完全不碰 tick
 * （与 s_user_lock_until_ms 同一约定：不引入无谓的时间依赖）。 */
static uint32_t s_session_deadline_ms;
#endif

/* 刷新活动时间。**由权限判定与登录调用** —— 这是"谁算活动"的全部实现，见上面第 ① 条。 */
static void hmi_session_touch(void)
{
#if HMI_SEC_SESSION_ENABLED
    if (s_logged_in)
        s_session_deadline_ms = lv_tick_get() + (uint32_t)HMI_SEC_SESSION_TIMEOUT_MIN * 60000u;
#endif
}

/*
 * 评估会话是否已超时。返回 1 = 本次调用判定超时（会话刚被清除、锁屏屏已切）。
 *
 * 判据：now > deadline ⇔ elapsed > timeout（严格大于，与编辑器同义）。
 * 用 int32_t 相减 ⇒ 跨 tick 环绕（约 49.7 天）仍然安全 —— 与 hmi_user_locked 同一形态。
 */
static int hmi_session_expire(const char* why)
{
#if HMI_SEC_SESSION_ENABLED
    if (!s_logged_in) { s_session_deadline_ms = 0u; return 0; }
    if (s_session_deadline_ms == 0u) return 0;
    if ((int32_t)(lv_tick_get() - s_session_deadline_ms) <= 0) return 0;

    /* 审计必须在清会话**之前**：之后 s_username 已空，日志里就没有"是谁超时的" */
    hmi_audit("session-timeout", s_username, hmi_current_role(), why);

    s_logged_in  = 0;
    s_username[0] = '\0';
    s_role_index = -1;
    s_perm_mask  = 0u;
    s_session_deadline_ms = 0u;

    /* 跳锁屏屏。名字由**生成期**从 SecurityConfig.LockScreenId 解析而来（设备端按名字切屏）。
     * 空串 = 工程里没有可用的锁屏/登录屏 ⇒ 不切屏：会话**已经**清掉了，安全边界不受影响，
     * 丢的只是"屏幕自己跳走"这个视觉提示。 */
    {
        const char* lock = HMI_SEC_LOCK_SCREEN_NAME;
        if (lock != NULL && lock[0] != '\0') {
            if (hmi_screen_switch(lock) == 0)
                HMI_ACT_LOG("hmi_session: 已跳到锁屏屏 '%s'", lock);
            else
                HMI_ACT_LOG("hmi_session: 锁屏屏 '%s' 不在工程里（会话已清除，但不切屏）", lock);
        }
    }
    return 1;
#else
    (void)why;
    return 0;
#endif
}

/*
 * 周期性评估会话超时 —— 供主循环调用（设备端 app/main.c、导出模拟器 main.c）。
 *
 * 为什么不能只靠惰性评估：惰性评估只在"有人来问权限"时才跑，于是"没人动"这件事
 * 要等下一个人来操作才会被发现 —— 安全边界不受影响（那一刻照样拒绝），
 * 但屏幕上不会自己跳锁屏，而"看到锁屏"正是操作员判断"我需要重新登录"的唯一线索。
 *
 * 与 hmi_engine_poll / sys_settings_boot_poll 同级：无会话时是空操作。
 */
int hmi_session_poll(void)
{
    return hmi_session_expire("idle timeout");
}

/* ── 角色表（由生成头的宏展开，数值全部来自编辑器） ── */
typedef struct { const char* id; const char* name; int level; unsigned int mask; } hmi_role_t;
#if HMI_SEC_ROLE_COUNT > 0
static const hmi_role_t s_roles[] = { HMI_SEC_ROLES_INIT };
#endif

/* 按角色 Id 查表；role_id 为空用默认角色。查不到返回 -1。 */
static int hmi_role_lookup(const char* role_id)
{
#if HMI_SEC_ROLE_COUNT > 0
    if (role_id == NULL) {
        if (HMI_SEC_DEFAULT_ROLE_INDEX < 0) return -1;
        return HMI_SEC_DEFAULT_ROLE_INDEX;
    }
    for (int i = 0; i < HMI_SEC_ROLE_COUNT; i++) {
        if (strcmp(s_roles[i].id, role_id) == 0) return i;
    }
#else
    (void)role_id;
#endif
    return -1;
}

/* 尝试按候选名字切换到目标屏幕；任一命中即返回 0 */
static int try_switch(const char* names[], int count)
{
    for (int i = 0; i < count; i++) {
        if (hmi_screen_switch(names[i]) == 0) {
            HMI_ACT_LOG("hmi_actions: switched to screen '%s'", names[i]);
            return 0;
        }
    }
    return -1;
}

/* ── 会话 ── */

int hmi_login_as_role(const char* user, const char* role_id)
{
    const int index = hmi_role_lookup(role_id);
    if (index < 0) {
        HMI_ACT_LOG("hmi_login_as_role: unknown role '%s' (no role table or not generated)",
                    role_id ? role_id : "(default)");
        return -1;
    }

    snprintf(s_username, sizeof(s_username), "%s", user ? user : "");
    s_logged_in  = 1;
    s_role_index = index;
#if HMI_SEC_ROLE_COUNT > 0
    s_perm_mask = s_roles[index].mask;
#endif
    hmi_session_touch();   /* 登录即活动：新会话从"现在"起算（与编辑器新建 UserSession 同义） */
    HMI_ACT_LOG("hmi_login_as_role: user '%s' role '%s' mask 0x%03X",
                s_username, hmi_current_role(), s_perm_mask);
    /* 会话建立的**唯一接缝**就在这里 ⇒ 审计也只在这一处记，不会漏掉任何一条建立路径
     * （hmi_login_submit 与"没有用户表时 hmi_login 的默认授权"都汇到这里）。 */
    hmi_audit("session-open", s_username, hmi_current_role(), "session established");
    return 0;
}

/* ── 运行时用户表 + 凭据校验 ──
 *
 * 表本身来自生成的 generated_security.h（HMI_SEC_USERS_INIT）——
 * 本文件不定义任何用户名、角色或哈希，只消费。表为空时不定义任何静态数据，
 * hmi_login_submit 直接拒绝：于是"工程没配用户"不会退化成"随便输点什么都能进"
 * （那正是本功能落地前的状态）。 */
#if HMI_SEC_USER_COUNT > 0
typedef struct {
    const char* username;
    const char* role_id;
    const char* hash;        /* "Base64Salt:Base64Hash" */
    int         enabled;
} hmi_user_t;

static const hmi_user_t s_users[] = { HMI_SEC_USERS_INIT };

/* 连续失败计数与锁定截止（按用户下标）。锁定截止用 lv_tick_get() 的毫秒，0 = 未锁定 ——
 * 刻意用 0 作哨兵：这样"从未失败过"的常规路径完全不碰 tick。 */
static int      s_user_fails[HMI_SEC_USER_COUNT];
static uint32_t s_user_lock_until_ms[HMI_SEC_USER_COUNT];

static int hmi_user_lookup(const char* username)
{
    int i;
    if (username == NULL) return -1;
    for (i = 0; i < HMI_SEC_USER_COUNT; i++) {
        if (strcmp(s_users[i].username, username) == 0) return i;   /* 大小写敏感，与编辑器一致 */
    }
    return -1;
}

/* 是否处于锁定期。到点则**自动解锁并清零计数**（与编辑器 UserProfile.LockedUntilUtc 同义）。 */
static int hmi_user_locked(int idx)
{
    if (s_user_lock_until_ms[idx] == 0u) return 0;
    if ((int32_t)(lv_tick_get() - s_user_lock_until_ms[idx]) >= 0) {
        s_user_lock_until_ms[idx] = 0u;
        s_user_fails[idx] = 0;
        HMI_ACT_LOG("hmi_user_locked: 用户 '%s' 锁定期已过，自动解锁", s_users[idx].username);
        return 0;
    }
    return 1;
}

static void hmi_user_register_failure(int idx)
{
    s_user_fails[idx]++;
    if (HMI_SEC_MAX_LOGIN_ATTEMPTS > 0 && s_user_fails[idx] >= HMI_SEC_MAX_LOGIN_ATTEMPTS) {
        s_user_lock_until_ms[idx] = lv_tick_get() + (uint32_t)HMI_SEC_LOCKOUT_MIN * 60000u;
        HMI_ACT_LOG("hmi_user_register_failure: 用户 '%s' 连续失败 %d 次，锁定 %d 分钟",
                    s_users[idx].username, s_user_fails[idx], HMI_SEC_LOCKOUT_MIN);
        /* 锁定是独立的安全事件（它改变了该账户接下来的可用性），单记一条 ——
         * 只靠"bad password"那几条能数出次数，但看不出"从这一刻起被锁了"。 */
        hmi_audit("lockout", s_users[idx].username, s_users[idx].role_id,
                  "max login attempts reached");
    }
}

static void hmi_user_clear_failures(int idx)
{
    s_user_fails[idx] = 0;
    s_user_lock_until_ms[idx] = 0u;
}

/*
 * 校验明文口令与存盘哈希。
 * 任何一处不合规（格式、长度、解码失败）一律返回 0 —— 拒绝登录，而不是"尽力解析出点什么"。
 * 哈希计算与编辑器 RuntimePasswordHasher.Verify 逐位一致（见 hmi_crypto.h 的跨语言契约）。
 */
static int hmi_verify_password(const char* password, const char* stored)
{
    const char* colon;
    char salt_b64[64];
    size_t salt_b64_len;
    unsigned char salt[HMI_SEC_SALT_BYTES];
    unsigned char expected[HMI_SHA256_DIGEST_BYTES];
    unsigned char actual[HMI_SHA256_DIGEST_BYTES];
    int salt_len, hash_len;

    if (stored == NULL || stored[0] == '\0') return 0;   /* 空哈希 = 该账户无法登录 */

    colon = strchr(stored, ':');
    if (colon == NULL) return 0;

    salt_b64_len = (size_t)(colon - stored);
    if (salt_b64_len == 0u || salt_b64_len >= sizeof(salt_b64)) return 0;
    memcpy(salt_b64, stored, salt_b64_len);
    salt_b64[salt_b64_len] = '\0';

    salt_len = hmi_base64_decode(salt_b64, salt, sizeof(salt));
    if (salt_len != HMI_SEC_SALT_BYTES) return 0;

    hash_len = hmi_base64_decode(colon + 1, expected, sizeof(expected));
    if (hash_len != HMI_SHA256_DIGEST_BYTES) return 0;

    if (hmi_sha256_iter(salt, (size_t)salt_len, password, HMI_SEC_HASH_ITERATIONS, actual) != 0)
        return 0;

    return hmi_ct_equal(expected, actual, HMI_SHA256_DIGEST_BYTES);
}
#endif /* HMI_SEC_USER_COUNT > 0 */

int hmi_login_submit(const char* user, const char* password)
{
#if HMI_SEC_USER_COUNT > 0
    int idx;

    if (user == NULL || password == NULL) {
        HMI_ACT_LOG("hmi_login_submit: user/password 为 NULL，拒绝");
        hmi_audit("login-reject", "", "", "null user or password");
        return -1;
    }

    idx = hmi_user_lookup(user);
    if (idx < 0) {
        HMI_ACT_LOG("hmi_login_submit: 未知用户 '%s'", user);
        hmi_audit("login-reject", user, "", "unknown user");
        return -1;
    }
    if (!s_users[idx].enabled) {
        HMI_ACT_LOG("hmi_login_submit: 用户 '%s' 已停用", user);
        hmi_audit("login-reject", user, s_users[idx].role_id, "account disabled");
        return -1;
    }
    if (hmi_user_locked(idx)) {
        HMI_ACT_LOG("hmi_login_submit: 用户 '%s' 处于锁定期内，拒绝", user);
        hmi_audit("login-reject", user, s_users[idx].role_id, "account locked");
        return -1;
    }

    /* 口令下限短路：省掉一次 1000 轮哈希。这是**省算力**，不是密码策略执行点 ——
     * 真实口令受 SecurityConfig.PasswordMinLength 约束，一定不短于它。
     *
     * 刻意**不计入**失败计数：长度不足的输入根本没进入密码空间（连哈希都没跑），
     * 等价于"格式非法的输入"，不构成一次猜测。计入有三条坏处而零收益：
     *   1) 安全上探测不到任何东西 —— 攻击者本就知道自己输了几位；
     *   2) 操作员手滑少输一位就推进锁定倒计时（5 次锁 15 分钟）；
     *   3) 把锁定本身变成 DoS 手段 —— 连发短口令即可锁死任意已知账户。 */
    if (strlen(password) < (size_t)HMI_SEC_PASSWORD_MIN_LEN) {
        HMI_ACT_LOG("hmi_login_submit: 用户 '%s' 口令长度不足 %d，拒绝（不计入失败计数）",
                    user, HMI_SEC_PASSWORD_MIN_LEN);
        /* 审计**照记**（不计入失败计数 ≠ 不留痕）：它是"有人试图登录"的证据，
         * 而审计不是计数器，不会把这条 DoS 面重新引入。 */
        hmi_audit("login-reject", user, s_users[idx].role_id, "password below minimum length");
        return -1;
    }

    if (!hmi_verify_password(password, s_users[idx].hash)) {
        hmi_user_register_failure(idx);
        HMI_ACT_LOG("hmi_login_submit: 用户 '%s' 口令错误（连续失败 %d/%d）",
                    user, s_user_fails[idx], HMI_SEC_MAX_LOGIN_ATTEMPTS);
        hmi_audit("login-reject", user, s_users[idx].role_id, "bad password");
        return -1;
    }

    hmi_user_clear_failures(idx);
    HMI_ACT_LOG("hmi_login_submit: 用户 '%s' 校验通过", user);

    /* 校验通过后仍走**唯一接缝** —— 角色→权限的映射只有 hmi_login_as_role 一条路
     * （session-open 的审计也由那一处记，这里不重复）。 */
    return hmi_login_as_role(s_users[idx].username, s_users[idx].role_id);
#else
    (void)user;
    (void)password;
    HMI_ACT_LOG("hmi_login_submit: 工程未下发运行时用户表（HMI_SEC_USER_COUNT == 0），拒绝登录");
    hmi_audit("login-reject", user ? user : "", "", "no runtime user table generated");
    return -1;
#endif
}

void hmi_login(void)
{
    /* 切到工程内的登录屏（如有）。
     *
     * ⚠ 配了运行时用户表的工程，这里**不授权**：会话必须由登录屏经 hmi_login_submit 建立。
     *   若此处仍按默认角色自动授权，则"点一下登录按钮"就已经以默认角色进入会话 ——
     *   凭据校验被完全绕过，登录屏形同虚设。 */
    const char* candidates[] = { "login", "login_screen", "登录", "登录屏", "Login" };

#if HMI_SEC_USER_COUNT == 0
    if (hmi_login_as_role("", NULL) != 0) {
#if !HMI_SEC_ENFORCE
        /* 设计模式（含缺 generated_security.h）：会话只作"已登录"标记，无权限含义 */
        s_logged_in = 1;
#endif
    }
#endif

    if (try_switch(candidates, sizeof(candidates) / sizeof(candidates[0])) != 0) {
#if HMI_SEC_USER_COUNT > 0
        HMI_ACT_LOG("hmi_login: 工程配了运行时用户表但没有登录屏 —— 无凭据提交入口，会话不建立"
                    "（请添加名为 login / 登录 的屏幕，并在其上放用户名框与密码框）");
#else
        HMI_ACT_LOG("hmi_login: session active (no dedicated login screen; boot password gate still applies)");
#endif
    }
}

void hmi_logout(void)
{
    /* 审计在清会话**之前**（之后 s_username 已空，日志里就没有"是谁登出的"） */
    if (s_logged_in)
        hmi_audit("logout", s_username, hmi_current_role(), "explicit logout by user");

    s_logged_in  = 0;
    s_username[0] = '\0';
    s_role_index = -1;
    s_perm_mask  = 0u;
#if HMI_SEC_SESSION_ENABLED
    s_session_deadline_ms = 0u;
#endif
    /* 返回默认屏（索引 0）。
     * 与超时锁定**刻意不同**：主动登出回操作画面，被动超时跳锁屏屏 ——
     * 后者需要让现场的人看见"我已被登出"（否则他会以为按钮坏了）。 */
    hmi_screen_switch_idx(0);
    HMI_ACT_LOG("hmi_logout: session cleared, returned to default screen");
}

int hmi_is_logged_in(void)   { return s_logged_in; }
const char* hmi_current_user(void) { return s_logged_in ? s_username : ""; }

const char* hmi_current_role(void)
{
#if HMI_SEC_ROLE_COUNT > 0
    if (s_logged_in && s_role_index >= 0 && s_role_index < HMI_SEC_ROLE_COUNT)
        return s_roles[s_role_index].id;
#endif
    return "";
}

int hmi_current_level(void)
{
#if HMI_SEC_ROLE_COUNT > 0
    if (s_logged_in && s_role_index >= 0 && s_role_index < HMI_SEC_ROLE_COUNT)
        return s_roles[s_role_index].level;
#endif
    return -1;
}

/* ── 权限 ── */

int hmi_has_permission(unsigned int bits)
{
#if HMI_SEC_ENFORCE
    /* 1) 惰性会话超时评估 —— **必须在判定之前**：超时的那一刻会话就被清掉，
     *    于是下面那行自然按"未登录"折算（拒绝）。顺序反了（先按旧掩码放行再登出）
     *    等于把这一次操作送出去 —— 那正是"超时"要防的事。
     *
     * 这个接缝是安全的：设备端**只有 hmi_require_permission 会调本函数**（生成的动作门走的就是它），
     * 没有周期性调用者 —— 若有，活动时间会被刷成"永久在线"，超时永不触发。 */
    (void)hmi_session_expire("permission check after idle");

    /* 2) 「有人在问权限」本身就算活动 —— 与编辑器 AuthenticationService.CheckPermission 里
     *    那句 session.LastActivityAt = UtcNow 逐条同义，也是本模块**唯一**的活动打点
     *    （刻意不接 lv_indev 读回调，见上方"活动打点"注释）。 */
    hmi_session_touch();

    /* 3) 判定公式（含"未登录只折算查看画面"这条语义）在生成的 hmi_perm_allows 里 ——
     *    模拟器侧调的是同一个函数，两边不可能分叉。 */
    return hmi_perm_allows(s_logged_in, s_perm_mask, bits);
#else
    (void)bits;
    return 1;   /* 设计模式：全放开（与历史行为一致） */
#endif
}

int hmi_require_permission(unsigned int bits)
{
    if (hmi_has_permission(bits)) return 1;
    HMI_ACT_LOG("permission denied: need 0x%03X, user '%s' role '%s' level %d",
                bits, hmi_current_user(), hmi_current_role(), hmi_current_level());
    return 0;
}

/* ── 系统动作（受权限门约束，门只在 ENFORCE 模式下编译进来） ── */

void hmi_show_alarms(void)
{
#if HMI_SEC_ENFORCE
    if (!hmi_require_permission(HMI_PERM_VIEW_ALARM_HISTORY)) return;
#endif
    const char* candidates[] = { "alarm", "alarms", "alarm_list", "报警", "报警列表", "AlarmList" };
    if (try_switch(candidates, sizeof(candidates) / sizeof(candidates[0])) != 0)
        HMI_ACT_LOG("hmi_show_alarms: alarm screen not found in project (expected a screen named alarm/alarm_list/报警列表)");
}

void hmi_show_trends(void)
{
#if HMI_SEC_ENFORCE
    if (!hmi_require_permission(HMI_PERM_VIEW_SCREEN)) return;
#endif
    const char* candidates[] = { "trend", "trends", "trend_view", "趋势", "趋势图", "TrendView" };
    if (try_switch(candidates, sizeof(candidates) / sizeof(candidates[0])) != 0)
        HMI_ACT_LOG("hmi_show_trends: trend screen not found in project (expected a screen named trend/trend_view/趋势图)");
}
