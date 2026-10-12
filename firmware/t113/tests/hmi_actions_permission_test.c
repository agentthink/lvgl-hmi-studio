/*
 * hmi_actions_permission_test.c — 会话/权限链路的主机侧行为测试
 *
 * 被测对象：app/runtime/hmi_actions.c（固件与设备共用同一份实现）
 * 权限模型 + 运行时用户表来源：tests/fixtures/generated_security.h —— 它是**编辑器生成器的真实产物**
 *   （SecurityConfig.OperationMode = Operator，且带 RuntimeUsers = op / eng 两个账户），
 *   由 C# 侧的 SecurityConfigCodeGenTests.HostTestFixture_MatchesGeneratorOutput 逐字节守卫，
 *   所以这里测到的角色表/权限位/用户凭据就是设备端将来拿到的那一份，不是手抄的。
 *
 * 覆盖（P2-2 验收口径「未登录拒绝受限动作」）：
 *   1) 未登录：允许查看画面，拒绝操作控件 / 改配置 / 看报警历史
 *   2) 未登录时受限动作被**短路**（不发生屏幕切换副作用）
 *   3) hmi_login 在**有用户表**的工程里只跳登录屏、**不建立会话**
 *   4) hmi_login_submit 用真实凭据建立会话，角色取自用户表
 *   5) 按角色 Id 登录：engineer 放行 ChangeConfig，admin 拿到全部权限
 *   6) 未知角色：登录失败且**不改动**现有会话
 *   7) logout：会话与权限一并复位
 *
 * 凭据本身的正误（错口令 / 停用 / 锁定 / 未知用户 / 空哈希）在
 * hmi_actions_credential_test.c 里测；本文件只关心"会话 → 权限"这条链。
 *
 * 编译（见 run_host_tests.sh）：gcc … -Iapp/runtime -Itests/fixtures
 */
#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include "hmi_actions.h"

/* ── 宿主原语假实现 ──
 * 只记录"被请求切到哪个屏"，不真的切屏（屏管理不属于本测试的范围）。
 * s_switch_result 用来构造"工程里没有这个屏"的分支。 */
static char s_last_switch[64];
static int  s_switch_result = -1;    /* 默认：任何屏都不存在 */
static int  s_switch_idx_calls = 0;

int hmi_screen_switch(const char* name)
{
    snprintf(s_last_switch, sizeof(s_last_switch), "%s", name ? name : "");
    return s_switch_result;
}

/* ── LVGL tick 假实现 ──
 * hmi_actions.c 的"连续失败锁定"用 lv_tick_get() 计时（真实固件由 LVGL 提供）。
 * 本测试不构造锁定场景，但链接期需要符号；返回一个可控的单调值即可。
 * 锁定场景本身在 hmi_actions_credential_test.c 里测。 */
static uint32_t s_tick_ms = 1000u;

uint32_t lv_tick_get(void)
{
    return s_tick_ms;
}

int hmi_screen_switch_idx(int index)
{
    (void)index;
    s_switch_idx_calls++;
    return 0;
}

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

static void reset_switch_probe(void)
{
    s_last_switch[0] = '\0';
}

int main(void)
{
    printf("=== hmi_actions 会话/权限 ===\n");

    /* 前提：fixture 必须是"执行权限门 + 带运行时用户表"的那一份，否则下面全是空转 */
    check(HMI_SEC_ENFORCE == 1, "fixture 带 HMI_SEC_ENFORCE=1（否则本测试无意义）");
    check(HMI_SEC_ROLE_COUNT >= 3, "角色表至少含 3 个预置角色");
    check(HMI_SEC_USER_COUNT >= 2, "fixture 带运行时用户表（第 4 节凭据用例的前提）");
    if (HMI_SEC_ENFORCE != 1 || HMI_SEC_ROLE_COUNT < 3 || HMI_SEC_USER_COUNT < 2) {
        printf("=== 结果: %d 通过, %d 失败 ===\n", s_pass, s_fail);
        return 1;
    }

    /* ── 1. 未登录 ── */
    printf("--- 未登录 ---\n");
    check(hmi_is_logged_in() == 0, "初始未登录");
    check(strcmp(hmi_current_user(), "") == 0, "未登录时当前用户为空串");
    check(strcmp(hmi_current_role(), "") == 0, "未登录时当前角色为空串");
    check(hmi_current_level() == -1, "未登录时安全等级为 -1");
    check(hmi_has_permission(HMI_PERM_VIEW_SCREEN) == 1, "未登录：允许查看画面");
    check(hmi_has_permission(HMI_PERM_OPERATE_CONTROL) == 0, "未登录：拒绝操作控件");
    check(hmi_has_permission(HMI_PERM_CHANGE_CONFIG) == 0, "未登录：拒绝修改配置");
    check(hmi_has_permission(HMI_PERM_VIEW_ALARM_HISTORY) == 0, "未登录：拒绝查看报警历史");
    check(hmi_has_permission(0u) == 1, "不要求权限的调用恒放行");

    /* ── 2. 受限动作被短路（无副作用） ── */
    printf("--- 未登录：受限动作被短路 ---\n");
    reset_switch_probe();
    hmi_show_alarms();
    check(s_last_switch[0] == '\0', "未登录调用 ShowAlarms 不触发任何屏幕切换（被权限门短路）");
    check(hmi_require_permission(HMI_PERM_OPERATE_CONTROL) == 0, "require_permission 未登录返回 0");

    /* ── 3. 有用户表时 hmi_login 只跳屏、不授权 ──
     * 这是凭据校验落地带来的**语义变更**：此前 hmi_login 会按默认角色直接授权，
     * 于是登录屏还没提交、会话已经建立 —— 凭据校验被完全绕过、登录屏形同虚设。
     * 下面的 s_switch_result=0 用来构造"工程里确实有登录屏"这条分支。 */
    printf("--- hmi_login（有用户表：只跳屏，不授权） ---\n");
    reset_switch_probe();
    s_switch_result = 0;
    hmi_login();
    s_switch_result = -1;   /* 立刻复原：其余用例仍按"工程里没有这个屏"构造 */
    check(s_last_switch[0] != '\0', "hmi_login 会去切登录屏");
    check(hmi_is_logged_in() == 0, "但不建立会话（否则登录屏形同虚设）");
    check(hmi_has_permission(HMI_PERM_OPERATE_CONTROL) == 0, "会话未建立 ⇒ 受限动作仍被拒");

    /* ── 4. 凭据提交建立会话 ── */
    printf("--- hmi_login_submit（fixture 用户表：op / op1234）---\n");
    check(hmi_login_submit("op", "op1234") == 0, "op + 正确口令 → 成功");
    check(hmi_is_logged_in() == 1, "登录后会话建立");
    check(strcmp(hmi_current_user(), "op") == 0, "会话记录的是提交的用户名（不再是空串）");
    check(strcmp(hmi_current_role(), "role-operator") == 0, "角色取自用户表");
    check(hmi_has_permission(HMI_PERM_OPERATE_CONTROL) == 1, "operator：允许操作控件");
    check(hmi_has_permission(HMI_PERM_VIEW_ALARM_HISTORY) == 1, "operator：允许查看报警历史");
    check(hmi_has_permission(HMI_PERM_CHANGE_CONFIG) == 0, "operator：仍拒绝修改配置");
    reset_switch_probe();
    hmi_show_alarms();
    check(s_last_switch[0] != '\0', "有权限后 ShowAlarms 会去切屏（说明门放行且动作体正常执行）");

    /* ── 5. 按角色 Id 切换 ── */
    printf("--- 按角色 Id 登录 ---\n");
    check(hmi_login_as_role("eng", "role-engineer") == 0, "登录 engineer 成功");
    check(strcmp(hmi_current_user(), "eng") == 0, "会话记录了用户名");
    check(strcmp(hmi_current_role(), "role-engineer") == 0, "会话记录了角色 Id");
    check(hmi_current_level() == 7, "engineer 等级 7");
    check(hmi_has_permission(HMI_PERM_CHANGE_CONFIG) == 1, "engineer：允许修改配置");
    check(hmi_has_permission(HMI_PERM_MANAGE_USERS) == 0, "engineer：仍拒绝管理用户");

    check(hmi_login_as_role("root", "role-admin") == 0, "登录 admin 成功");
    check(hmi_current_level() == 15, "admin 等级 15");
    check(hmi_has_permission(HMI_PERM_ALL) == 1, "admin：拥有全部权限");

    /* ── 6. 未知角色：失败且不动会话 ── */
    printf("--- 未知角色 ---\n");
    check(hmi_login_as_role("x", "role-not-exist") == -1, "未知角色登录返回 -1");
    check(strcmp(hmi_current_role(), "role-admin") == 0, "登录失败不改动现有会话");
    check(hmi_has_permission(HMI_PERM_ALL) == 1, "登录失败不改动现有权限");

    /* ── 7. logout ── */
    printf("--- logout ---\n");
    const int idx_before = s_switch_idx_calls;
    hmi_logout();
    check(s_switch_idx_calls == idx_before + 1, "logout 回默认屏（索引 0）");
    check(hmi_is_logged_in() == 0, "logout 后未登录");
    check(hmi_has_permission(HMI_PERM_OPERATE_CONTROL) == 0, "logout 后权限一并复位");

    printf("=== 结果: %d 通过, %d 失败 ===\n", s_pass, s_fail);
    return s_fail == 0 ? 0 : 1;
}
