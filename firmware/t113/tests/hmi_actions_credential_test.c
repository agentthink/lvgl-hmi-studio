/*
 * hmi_actions_credential_test.c — 凭据校验链路的主机侧行为测试
 *
 * 被测对象：app/runtime/hmi_actions.c 的 hmi_login_submit / hmi_login
 * 数据来源：tests/fixtures/generated_security.h —— **编辑器生成器的真实产物**
 *   （SecurityConfig.OperationMode = Operator，RuntimeUsers = op / eng / dis），
 *   由 C# 侧 SecurityConfigCodeGenTests.HostTestFixture_MatchesGeneratorOutput 逐字节守卫。
 *
 * 口令与 salt 与编辑器侧 Support/RuntimeCredentialVectors.cs **同源**：
 *   op  / op1234   (role-operator, 启用)
 *   eng / eng1234  (role-engineer, 启用)
 *   dis / op1234   (role-operator, **停用**；刻意与 op 同口令 ⇒ "停用被拒"不受口令错干扰)
 *
 * 覆盖：
 *   1) 正确凭据 → 建立会话，角色/权限取自用户表
 *   2) 错误口令 → 拒绝，且**不改动**现有会话
 *   3) 未知用户 → 拒绝（用户名大小写敏感）
 *   4) 停用账户 → 拒绝（哪怕口令正确）
 *   5) 空口令 / 过短口令 / NULL 参数 → 拒绝
 *   6) 连续失败达阈值 → 锁定，连**正确**口令也拒；锁定期过后自动解锁
 *   7) 有用户表时 hmi_login 只跳屏、不授权
 *
 * 编译（见 run_host_tests.sh）：gcc -std=c99 -Wall -Wextra -Iapp/runtime -Itests/fixtures
 */
#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include "hmi_actions.h"

/* ── 宿主原语假实现 ── */
static char s_last_switch[64];
static int  s_switch_result = -1;    /* 默认：工程里没有这个屏 */

int hmi_screen_switch(const char* name)
{
    snprintf(s_last_switch, sizeof(s_last_switch), "%s", name ? name : "");
    return s_switch_result;
}

int hmi_screen_switch_idx(int index)
{
    (void)index;
    return 0;
}

/* ── LVGL tick 假实现（可推进）──
 * hmi_actions.c 用 lv_tick_get() 计"锁定时长"。用可控值推进，避免测锁定要真等 15 分钟。
 * 这也顺带证明了实现确实走的是**单调 tick**而不是墙钟（墙钟在这里根本不可注入）。 */
static uint32_t s_tick_ms = 5000u;

uint32_t lv_tick_get(void)
{
    return s_tick_ms;
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

int main(void)
{
    int i;

    printf("=== hmi_actions 凭据校验 ===\n");

    /* 前提：fixture 必须是"带用户表 + 执行权限门"的那一份，否则下面全是空转 */
    check(HMI_SEC_ENFORCE == 1, "fixture 带 HMI_SEC_ENFORCE=1");
    check(HMI_SEC_USER_COUNT >= 3, "fixture 至少 3 个运行时用户（op/eng/dis）");
    if (HMI_SEC_ENFORCE != 1 || HMI_SEC_USER_COUNT < 3) {
        printf("=== 结果: %d 通过, %d 失败 ===\n", s_pass, s_fail);
        return 1;
    }

    /* ── 1. 正确凭据 ── */
    printf("--- 正确凭据 ---\n");
    check(hmi_login_submit("op", "op1234") == 0, "op + op1234 → 成功");
    check(hmi_is_logged_in() == 1, "会话已建立");
    check(strcmp(hmi_current_user(), "op") == 0, "会话记录了提交的用户名");
    check(strcmp(hmi_current_role(), "role-operator") == 0, "角色取自用户表");
    check(hmi_current_level() == 3, "operator 等级 3");
    check(hmi_has_permission(HMI_PERM_OPERATE_CONTROL) == 1, "operator：允许操作控件");
    check(hmi_has_permission(HMI_PERM_CHANGE_CONFIG) == 0, "operator：拒绝修改配置");

    check(hmi_login_submit("eng", "eng1234") == 0, "eng + eng1234 → 成功");
    check(strcmp(hmi_current_role(), "role-engineer") == 0, "角色切换为 role-engineer");
    check(hmi_current_level() == 7, "engineer 等级 7");
    check(hmi_has_permission(HMI_PERM_CHANGE_CONFIG) == 1, "engineer：允许修改配置");
    check(hmi_has_permission(HMI_PERM_MANAGE_USERS) == 0, "engineer：仍拒绝管理用户");

    /* ── 2. 错误口令：拒绝且不动会话 ── */
    printf("--- 错误口令 ---\n");
    check(hmi_login_submit("op", "wrong-password") == -1, "错误口令 → 拒绝");
    check(hmi_is_logged_in() == 1, "失败不改动会话（仍处于登录态）");
    check(strcmp(hmi_current_user(), "eng") == 0, "失败不改动当前用户（仍是上一个成功的 eng）");
    check(hmi_has_permission(HMI_PERM_CHANGE_CONFIG) == 1, "失败不改动现有权限");

    check(hmi_login_submit("op", "op12345") == -1, "只差一个字符的口令也拒绝");
    check(hmi_login_submit("op", "op123") == -1, "少一位的口令拒绝");
    check(hmi_login_submit("op", "op1234 ") == -1, "末尾多一个空格拒绝（不做 trim）");

    /* ── 3. 未知用户（用户名大小写敏感）── */
    printf("--- 未知用户 ---\n");
    check(hmi_login_submit("nobody", "op1234") == -1, "未知用户拒绝");
    check(hmi_login_submit("OP", "op1234") == -1, "用户名大小写敏感（OP ≠ op）");
    check(hmi_login_submit("", "op1234") == -1, "空用户名拒绝");
    check(strcmp(hmi_current_user(), "eng") == 0, "未知用户尝试不改动会话");

    /* ── 4. 停用账户：口令正确也拒绝 ── */
    printf("--- 停用账户 ---\n");
    check(hmi_login_submit("dis", "op1234") == -1, "停用账户 + **正确**口令 → 仍拒绝");
    check(strcmp(hmi_current_user(), "eng") == 0, "停用账户尝试不改动会话");

    /* ── 5. 空口令 / 过短口令 / NULL ── */
    printf("--- 空口令 / 过短 / NULL ---\n");
    check(hmi_login_submit("op", "") == -1, "空口令拒绝");
    {
        /* 构造一个恰好短于下限的口令，确认下限短路确实生效（而不是碰巧哈希不匹配） */
        char shortpw[8];
        for (i = 0; i < HMI_SEC_PASSWORD_MIN_LEN - 1 && i < 7; i++) shortpw[i] = 'x';
        shortpw[i] = '\0';
        check(hmi_login_submit("op", shortpw) == -1, "短于 HMI_SEC_PASSWORD_MIN_LEN 的口令拒绝");
    }
    check(hmi_login_submit(NULL, "op1234") == -1, "user 为 NULL 拒绝");
    check(hmi_login_submit("op", NULL) == -1, "password 为 NULL 拒绝");

    /* 上面这些"格式非法"的尝试**不计入**失败计数 —— 它们连哈希都没跑，不构成一次猜测
     * （否则锁定就成了 DoS 手段：连发短口令即可锁死任意已知账户）。
     * 连提 MAX+2 次后再试正确口令：若实现把它们计入，op 早已锁死，下一条会红。 */
    for (i = 0; i < HMI_SEC_MAX_LOGIN_ATTEMPTS + 2; i++)
        (void)hmi_login_submit("op", "x");
    check(hmi_login_submit("op", "op1234") == 0,
          "格式非法的尝试不计入失败计数：连提 MAX+2 次短口令后正确口令仍通过");

    /* ── 6. 连续失败锁定 ── */
    printf("--- 连续失败锁定 ---\n");
    /* 先用一次成功登录把 op 的失败计数清零（上面的负例已经累加过），
     * 否则"再失败几次到阈值"的数量就说不清了 */
    check(hmi_login_submit("op", "op1234") == 0, "锁定实验前：op 可正常登录（同时清零失败计数）");

    for (i = 0; i < HMI_SEC_MAX_LOGIN_ATTEMPTS; i++)
        (void)hmi_login_submit("op", "wrong-password");

    check(hmi_login_submit("op", "op1234") == -1,
          "连续失败达 HMI_SEC_MAX_LOGIN_ATTEMPTS 后，**正确**口令也被拒（锁定生效）");
    check(strcmp(hmi_current_user(), "op") == 0, "锁定不销毁已有会话（只是拒绝新登录）");

    /* 推进单调 tick 越过锁定时长 —— 若实现误用墙钟，这里推进对它无效，下一条会红 */
    s_tick_ms += (uint32_t)HMI_SEC_LOCKOUT_MIN * 60000u + 1u;
    check(hmi_login_submit("op", "op1234") == 0, "锁定期过后自动解锁，正确口令恢复");

    /* 解锁后失败计数应从零重算：再失败 MAX-1 次仍不应锁定 */
    for (i = 0; i < HMI_SEC_MAX_LOGIN_ATTEMPTS - 1; i++)
        (void)hmi_login_submit("op", "wrong-password");
    check(hmi_login_submit("op", "op1234") == 0,
          "解锁后计数已清零：再失败 MAX-1 次仍未锁定");

    /* ── 7. 有用户表时 hmi_login 只跳屏、不授权 ── */
    printf("--- hmi_login：只跳屏、不授权 ---\n");
    hmi_logout();
    check(hmi_is_logged_in() == 0, "logout 后未登录");
    s_switch_result = 0;    /* 构造"工程里有登录屏" */
    s_last_switch[0] = '\0';
    hmi_login();
    check(s_last_switch[0] != '\0', "hmi_login 会去切登录屏");
    check(hmi_is_logged_in() == 0, "但**不**建立会话（凭据未提交，不得自动授权）");
    check(hmi_has_permission(HMI_PERM_OPERATE_CONTROL) == 0, "会话未建立 ⇒ 操作控件被拒");

    printf("=== 结果: %d 通过, %d 失败 ===\n", s_pass, s_fail);
    return s_fail == 0 ? 0 : 1;
}
