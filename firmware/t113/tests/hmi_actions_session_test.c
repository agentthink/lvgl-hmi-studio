/*
 * hmi_actions_session_test.c — 会话超时 + 审计链路的主机侧行为测试
 *
 * 被测对象：app/runtime/hmi_actions.c（固件与设备共用同一份实现）
 * 会话策略来源：tests/fixtures/generated_security.h —— 它是**编辑器生成器的真实产物**
 *   （配置 SessionTimeoutMin=30 / AutoLogoutEnabled=1 / LockScreenId 指向
 *    Id="screen-lock"、名字="锁屏" 的屏），由 C# 侧
 *   SecurityConfigCodeGenTests.HostTestFixture_MatchesGeneratorOutput 逐字节守卫。
 *   ⇒ 这里读到的 HMI_SEC_LOCK_SCREEN_NAME == "锁屏" 正是**生成期解析**的结果
 *   （Id ≠ 名字，所以"拿到 Id 却当名字用"这类 bug 会被下面的断言直接抓住）。
 *
 * 核心判据是**与编辑器 AuthenticationService.CheckPermission 的同构性**（三条）：
 *   ① 「有人在问权限」本身就算活动 —— 权限判定刷新活动时间
 *   ② 判据**严格大于**	—— 恰好 30 分钟不超时，过 1 毫秒才超时
 *   ③ 超时 ⇒ 清会话 + 跳锁屏 + **拒绝本次判定**
 *   ④ 判定入口的**顺序**是「评估超时 → 打点 → 判定」。两条顺序各对应一个真实缺陷：
 *      评估跑到判定之后 = 把这次操作送出去；打点跑到评估之前 = 超时永不触发。
 *      见第 8 节（打点有效）与第 9b 节（顺序正确）。
 *
 * 时间全部用一个可控的假 tick 推进，不需要真等 30 分钟；推进量由 fixture 的
 * HMI_SEC_SESSION_TIMEOUT_MIN 算出，**不写死 30** —— fixture 改配置这条测试仍然有效。
 *
 * 编译（见 run_host_tests.sh 第 7 步）：
 *   gcc … -DHMI_SEC_AUDIT_PATH="…/audit.log" tests/hmi_actions_session_test.c \
 *        app/runtime/hmi_actions.c app/runtime/hmi_crypto.c
 * -DHMI_SEC_AUDIT_PATH 是**必须**的：不传就会去写设备路径 /mnt/UDISK/…（主机上必失败）。
 * 下面第 0 节有一条判据专门守这件事。
 */
#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include "hmi_actions.h"

/* ── 宿主原语假实现 ──
 * 记录"被请求切到哪块屏"，不真的切屏（屏管理不属于本测试范围）。
 * s_switch_result 用来构造"工程里没有这块屏"那条分支。 */
static char s_last_switch[64];
static int  s_switch_calls = 0;
static int  s_switch_result = 0;    /* 默认 0：屏都存在（超时的正例需要它） */

int hmi_screen_switch(const char* name)
{
    snprintf(s_last_switch, sizeof(s_last_switch), "%s", name ? name : "");
    s_switch_calls++;
    return s_switch_result;
}

int hmi_screen_switch_idx(int index)
{
    (void)index;
    s_switch_calls++;
    return 0;
}

/* ── 可控单调 tick（毫秒）──
 * 会话超时是本文件**唯一**的时间依赖；真实固件里这个值由 LVGL 提供。
 * 起点刻意给一个非零的奇数，免得"deadline 恰好等于起始 tick"把某些判据掩盖掉。 */
static uint32_t s_tick_ms = 1234567u;

uint32_t lv_tick_get(void) { return s_tick_ms; }

static void advance_ms(uint32_t ms)          { s_tick_ms += ms; }
static void advance_minutes(int minutes)     { s_tick_ms += (uint32_t)minutes * 60000u; }

/* ── 迷你断言 ── */
static int s_pass;
static int s_fail;

static void check(int condition, const char* what)
{
    if (condition) {
        s_pass++;
        printf("  PASS  %s\n", what);
    } else {
        s_fail++;
        printf("  FAIL  %s\n", what);
    }
}

/* ── 审计文件探针 ──
 * 起点必须先删文件：否则上一轮跑剩的内容会让下面的 contains 变成**恒真判据**
 * （"审计写了"这条就永远绿，哪怕本次一条都没写）。 */
static int file_exists(const char* path)
{
    FILE* f = fopen(path, "r");
    if (f == NULL) return 0;
    fclose(f);
    return 1;
}

static int file_contains(const char* path, const char* needle)
{
    FILE* f = fopen(path, "r");
    char line[512];
    if (f == NULL) return 0;
    while (fgets(line, sizeof(line), f) != NULL) {
        if (strstr(line, needle) != NULL) { fclose(f); return 1; }
    }
    fclose(f);
    return 0;
}

/* 第 1 次登录用的凭据 —— 与 hmi_actions_permission_test.c / credential_test.c 同一组 */
#define USER_OP      "op"
#define PASS_OP      "op1234"
#define USER_ENG     "eng"

int main(void)
{
    printf("=== hmi_actions 会话超时 + 审计 ===\n");

    /* ── 0. 前提自检：跑不到这些前提，下面的断言全是空转 ── */
    printf("--- 前提 ---\n");
    check(HMI_SEC_ENFORCE == 1, "fixture 带 HMI_SEC_ENFORCE=1（否则本测试无意义）");
    check(HMI_SEC_AUTO_LOGOUT == 1, "fixture 带 HMI_SEC_AUTO_LOGOUT=1（关掉就没有超时可言）");
    check(HMI_SEC_SESSION_TIMEOUT_MIN > 0, "fixture 的超时阈值 > 0");
    /* 派生开关是**预处理器**算出来的 —— C# 侧只能守"派生块形态"（谁在 #if 分支里是字面量，
     * 断言不出取值），取值只能在真编译器下验。这条就是那另一半。 */
    check(HMI_SEC_SESSION_ENABLED == 1, "fixture 下 HMI_SEC_SESSION_ENABLED 被预处理器算成 1");
    check(HMI_SEC_LOCK_SCREEN_NAME[0] != '\0', "fixture 解析出了锁屏屏名（非空）");
    check(strcmp(HMI_SEC_LOCK_SCREEN_NAME, "锁屏") == 0,
          "锁屏屏名是「锁屏」（由 Id=screen-lock 生成期解析，Id≠名字 ⇒ 混用必被抓）");
    check(strcmp(HMI_SEC_AUDIT_PATH, "/mnt/UDISK/hmi/audit.log") != 0,
          "本测试已用 -DHMI_SEC_AUDIT_PATH 指到临时文件（否则会去写设备路径）");
    if (HMI_SEC_ENFORCE != 1 || HMI_SEC_AUTO_LOGOUT != 1 || HMI_SEC_SESSION_TIMEOUT_MIN <= 0
        || HMI_SEC_LOCK_SCREEN_NAME[0] == '\0' || HMI_SEC_SESSION_ENABLED != 1) {
        printf("=== 结果: %d 通过, %d 失败 ===\n", s_pass, s_fail);
        return 1;
    }

    (void)remove(HMI_SEC_AUDIT_PATH);
    check(!file_exists(HMI_SEC_AUDIT_PATH), "起点：审计文件已清空（否则 contains 会变恒真）");

    /* ── 1. 无会话时 poll 是空操作 ── */
    printf("--- 无会话 ---\n");
    s_switch_calls = 0;
    check(hmi_session_poll() == 0, "无会话时 poll 返回 0");
    check(s_switch_calls == 0, "无会话时 poll 不切屏");
    check(hmi_is_logged_in() == 0, "确实未登录");

    /* ── 2~4. 登录 → 未超时 / 恰好等于阈值 / 过 1 毫秒 ── */
    printf("--- 超时边界（阈值 %d 分钟）---\n", HMI_SEC_SESSION_TIMEOUT_MIN);
    check(hmi_login_submit(USER_OP, PASS_OP) == 0, "op + 正确口令登录成功");
    check(hmi_is_logged_in() == 1, "会话已建立");

    advance_minutes(HMI_SEC_SESSION_TIMEOUT_MIN - 1);
    check(hmi_session_poll() == 0, "阈值 -1 分钟：未超时");
    check(hmi_is_logged_in() == 1, "阈值 -1 分钟：会话仍在");

    advance_minutes(1);   /* 累计正好等于阈值 */
    check(hmi_session_poll() == 0, "恰好达到阈值：不超时（判据是严格大于，与编辑器同义）");
    check(hmi_is_logged_in() == 1, "恰好达到阈值：会话仍在");

    /* ── 5. 过 1 毫秒即锁定 ── */
    printf("--- 超时锁定 ---\n");
    advance_ms(1);
    s_switch_calls = 0;
    s_last_switch[0] = '\0';
    check(hmi_session_poll() == 1, "阈值 +1 毫秒：超时");
    check(hmi_is_logged_in() == 0, "超时后会话被清除");
    check(strcmp(hmi_current_user(), "") == 0, "超时后当前用户为空串");
    check(strcmp(hmi_current_role(), "") == 0, "超时后当前角色为空串");
    check(hmi_current_level() == -1, "超时后安全等级为 -1");
    check(s_switch_calls == 1, "超时跳了一次屏");
    check(strcmp(s_last_switch, HMI_SEC_LOCK_SCREEN_NAME) == 0, "跳向生成期解析出的锁屏屏");

    /* ── 6. 已超时后再 poll：幂等，不重复切屏 ── */
    s_switch_calls = 0;
    check(hmi_session_poll() == 0, "已登出后再次 poll 返回 0");
    check(s_switch_calls == 0, "已登出后再次 poll 不重复切屏");

    /* ── 7. 超时后权限判定立刻按"未登录"折算 ── */
    check(hmi_has_permission(HMI_PERM_OPERATE_CONTROL) == 0, "超时后：拒绝操作控件");
    check(hmi_has_permission(HMI_PERM_CHANGE_CONFIG) == 0, "超时后：拒绝修改配置");
    check(hmi_has_permission(HMI_PERM_VIEW_SCREEN) == 1, "超时后：仍允许查看画面（未登录语义）");

    /* ── 8. 「有人在问权限」本身就算活动（判据 ①，本改动的核心） ──
     * 判据必须落在 hmi_session_poll() 上：hmi_is_logged_in() 只读标志、**不评估**超时，
     * 所以"打点缺失"时它照样为真 —— 拿它当判据是空转的（这一点是负向验证逼出来的）。
     * 若打点缺失：距**登录**已达 2×阈值 ⇒ poll 会判超时 ⇒ 下面第一条就红。 */
    printf("--- 活动打点（权限判定刷新活动时间）---\n");
    check(hmi_login_submit(USER_OP, PASS_OP) == 0, "重新登录");
    advance_minutes(HMI_SEC_SESSION_TIMEOUT_MIN - 1);
    check(hmi_require_permission(HMI_PERM_OPERATE_CONTROL) == 1, "阈值 -1 分钟时操作控件：放行（并刷新活动时间）");
    advance_minutes(HMI_SEC_SESSION_TIMEOUT_MIN - 1);
    check(hmi_session_poll() == 0, "距登录已 2 倍阈值，但距上次活动只有阈值 -1 分钟：不判超时");
    check(hmi_is_logged_in() == 1, "同上：会话仍在");

    /* ── 9. poll **不**刷新活动时间（否则超时永不触发） ──
     * 承上：deadline = 上次活动 + 阈值。推进到恰好 deadline 仍不超，再过 1ms 就超 ——
     * 这条能成立，就证明中间的 poll **没有**把 deadline 往后推。 */
    printf("--- poll 不刷新活动时间 ---\n");
    advance_minutes(1);
    check(hmi_session_poll() == 0, "恰好到 deadline：不超时（说明 poll 没刷新活动时间）");
    advance_ms(1);
    check(hmi_session_poll() == 1, "过 deadline 1 毫秒即超时（poll 未能续命）");

    /* ── 9b. 空闲超阈值后的**第一次判定**：顺序必须是「先判超时、后打点」 ──
     * 反了（先打点续命再评估）⇒ touch 先把 deadline 推到 now+阈值 ⇒ 评估必然算未超时
     * ⇒ 判定按旧掩码**放行**，于是"空闲之后的那次操作被送出去" —— 正是超时要防的事。
     * 与第 9 节是两件事：那条防"周期性续命"（poll 自己刷），这条防"判定入口自身的续命顺序"。
     * 第 9 节结束时会话已被清，所以这里要重新登录。 */
    printf("--- 空闲后首次判定：先判超时、后打点 ---\n");
    check(hmi_login_submit(USER_OP, PASS_OP) == 0, "再登录");
    advance_minutes(HMI_SEC_SESSION_TIMEOUT_MIN + 1);   /* 空闲已超阈值 */
    s_switch_calls = 0;
    check(hmi_require_permission(HMI_PERM_OPERATE_CONTROL) == 0,
          "空闲超阈值后判定：拒绝（说明超时评估发生在打点之前）");
    check(hmi_is_logged_in() == 0, "空闲超阈值后判定：会话已被清除");
    check(s_switch_calls == 1, "空闲超阈值后判定：跳了锁屏");

    /* ── 10. 登出：会话清空，之后无论过多久都不再超时 ── */
    printf("--- 显式登出 ---\n");
    check(hmi_login_submit(USER_OP, PASS_OP) == 0, "再登录");
    hmi_logout();
    check(hmi_is_logged_in() == 0, "登出后未登录");
    s_switch_calls = 0;
    advance_minutes(HMI_SEC_SESSION_TIMEOUT_MIN * 10);
    check(hmi_session_poll() == 0, "登出后过 10 倍阈值：poll 不判超时");
    check(s_switch_calls == 0, "登出后 poll 不切屏（会话已空）");

    /* ── 11. 锁屏屏不在工程里：仍然清会话，只是不切屏 ── */
    printf("--- 锁屏屏缺失 ---\n");
    check(hmi_login_submit(USER_OP, PASS_OP) == 0, "再登录");
    s_switch_result = -1;                       /* 构造"工程里没有这块屏" */
    advance_minutes(HMI_SEC_SESSION_TIMEOUT_MIN + 1);
    check(hmi_session_poll() == 1, "超时判定仍然成立（不依赖切屏成功）");
    check(hmi_is_logged_in() == 0, "会话仍被清除（安全边界不受切屏失败影响）");
    s_switch_result = 0;

    /* ── 12. 审计落盘 ── */
    printf("--- 审计落盘 ---\n");
    check(file_exists(HMI_SEC_AUDIT_PATH), "审计文件已创建");
    check(file_contains(HMI_SEC_AUDIT_PATH, "session-open"), "含 session-open（会话建立）");
    check(file_contains(HMI_SEC_AUDIT_PATH, "session-timeout"), "含 session-timeout（超时）");
    check(file_contains(HMI_SEC_AUDIT_PATH, "logout"), "含 logout（显式登出）");
    check(file_contains(HMI_SEC_AUDIT_PATH, "user=op"), "审计行带 user=op（不是空用户名）");

    /* 失败登录 → login-reject（含具体原因） */
    check(hmi_login_submit(USER_OP, "definitely-wrong") != 0, "错误口令被拒");
    check(file_contains(HMI_SEC_AUDIT_PATH, "login-reject"), "含 login-reject（登录被拒）");
    check(file_contains(HMI_SEC_AUDIT_PATH, "bad password"), "含具体原因 bad password");

    /* 连续失败达上限 → lockout（放最后：它会把 eng 锁住） */
    for (int i = 0; i < HMI_SEC_MAX_LOGIN_ATTEMPTS; i++)
        (void)hmi_login_submit(USER_ENG, "definitely-wrong");
    check(file_contains(HMI_SEC_AUDIT_PATH, "lockout"), "含 lockout（连续失败达上限）");

    /* 审计行的时间戳形态：YYYY-MM-DDTHH:MM:SS —— 只要求"日期段"存在即可，
     * 主机时钟可能是任意值，不能断言具体时刻。 */
    {
        int year_ok = 0;
        FILE* f = fopen(HMI_SEC_AUDIT_PATH, "r");
        char line[512];
        while (f != NULL && fgets(line, sizeof(line), f) != NULL) {
            /* 行首前 4 位是年份数字 + 紧跟 '-' */
            if (line[0] >= '2' && line[1] >= '0' && line[2] >= '0' && line[3] >= '0'
                && line[4] == '-') { year_ok = 1; break; }
        }
        if (f != NULL) fclose(f);
        check(year_ok, "审计行以 20xx- 形态的墙钟时间戳开头");
    }

    printf("=== 结果: %d 通过, %d 失败 ===\n", s_pass, s_fail);
    return s_fail == 0 ? 0 : 1;
}
