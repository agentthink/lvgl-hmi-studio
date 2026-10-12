/*
 * hmi_log_test.c — 日志门槛与轮询周期的下沉契约（generated_runtime.h）
 *
 * 被测对象：app/runtime/hmi_log.h（日志门槛）、app/runtime/runtime_config.h（轮询周期）
 *
 * 为什么要有这个用例：工程设置 → 运行时 里的「扫描周期 / 日志级别」此前**配了到不了设备**
 * —— 固件把这两个值都硬编码了。本用例钉的是**真实输出行为**（把 stderr 抓下来数哪几行出来了），
 * 刻意不比对产物文本：文本对而宏没生效，正是这类"配置下沉"最典型的坏法。
 *
 * 四个场景由 run_host_tests.sh 用 -DHMI_LOG_TEST_SCENARIO=n 选择；每个场景的期望值在本文件里
 * **逐场景硬编码**，不从 HMI_LOG_LEVEL 反推 —— 反推等于把被测逻辑抄进判据，实现写反时一起错。
 *
 *   1 = 带生成头，fixture 的 HMI_LOG_LEVEL=2（Warning）⇒ DEBUG/INFO 静默
 *   2 = 带生成头，-DHMI_LOG_LEVEL=0 覆盖（Debug）    ⇒ 全开
 *   3 = 带生成头，-DHMI_LOG_LEVEL=3 覆盖（Error）    ⇒ 只留 ERROR 及以上
 *   4 = **不带生成头**（-I 不含 tests/fixtures）      ⇒ 分级宏退化无条件输出 + 周期回退 100
 *
 * 刻意**不 include hmi_runtime.h**：那个聚合头会拉进 lvgl.h，而主机侧没有 LVGL。
 */
/* fileno/dup/dup2 是 POSIX 而非 C99，得在**任何系统头之前**声明特性宏。
 * 写在源码里而不是靠调用方传 -D_GNU_SOURCE：这个头文件自己要能独立编过。 */
#ifndef _GNU_SOURCE
#  define _GNU_SOURCE
#endif

#include <stdio.h>
#include <string.h>
#include <unistd.h>

#include "hmi_log.h"
#include "runtime_config.h"

/* ── 场景与逐场景期望表 ── */
#ifndef HMI_LOG_TEST_SCENARIO
#  define HMI_LOG_TEST_SCENARIO 1
#endif

#if HMI_LOG_TEST_SCENARIO == 1
#  define EXP_POLL_MS 250
#  define EXP_DBG 0
#  define EXP_INFO 0
#  define EXP_WARN 1
#  define EXP_ERR 1
#  define EXP_HAS_LEVEL_CONSTS 1
#elif HMI_LOG_TEST_SCENARIO == 2
#  define EXP_POLL_MS 250
#  define EXP_DBG 1
#  define EXP_INFO 1
#  define EXP_WARN 1
#  define EXP_ERR 1
#  define EXP_HAS_LEVEL_CONSTS 1
#elif HMI_LOG_TEST_SCENARIO == 3
#  define EXP_POLL_MS 250
#  define EXP_DBG 0
#  define EXP_INFO 0
#  define EXP_WARN 0
#  define EXP_ERR 1
#  define EXP_HAS_LEVEL_CONSTS 1
#elif HMI_LOG_TEST_SCENARIO == 4
#  define EXP_POLL_MS 100
#  define EXP_DBG 1
#  define EXP_INFO 1
#  define EXP_WARN 1
#  define EXP_ERR 1
#  define EXP_HAS_LEVEL_CONSTS 0
#else
#  error "未知的 HMI_LOG_TEST_SCENARIO（只定义了 1..4）"
#endif

/* 场景与生成头是否真的在，互为前提 —— 在编译期自检，避免 -I 配错时静默测了错的东西。 */
#if HMI_LOG_TEST_SCENARIO == 4
#  ifdef HMI_LOG_LEVEL
#    error "场景 4 要求不带生成头，但 HMI_LOG_LEVEL 已被定义（-I 里混进了 tests/fixtures？）"
#  endif
#else
#  ifndef HMI_LOG_LEVEL
#    error "场景 1..3 要求带生成头，但 HMI_LOG_LEVEL 未定义（漏了 -Itests/fixtures？）"
#  endif
#endif

#ifdef HMI_LOG_LEVEL
#  define LOG_LEVEL_FOR_PRINT HMI_LOG_LEVEL
#else
#  define LOG_LEVEL_FOR_PRINT (-1)   /* -1 = 无门槛（回退成无条件输出） */
#endif

/* ── 结果计数 ── */
static int s_pass = 0;
static int s_fail = 0;

static void check(int cond, const char* what)
{
    if (cond)
    {
        s_pass++;
        printf("  PASS  %s\n", what);
    }
    else
    {
        s_fail++;
        printf("  FAIL  %s\n", what);
    }
}

/* ── stderr 捕获 ──
 * 分级宏写 stderr，所以判据必须落在"实际输出"上。把 stderr 重定向到文件、跑完读回来。 */
static int s_saved_stderr = -1;
static const char* const CAPTURE_PATH = "/tmp/hmi_log_test_capture.txt";

static void capture_begin(void)
{
    fflush(stderr);
    s_saved_stderr = dup(fileno(stderr));
    if (freopen(CAPTURE_PATH, "w", stderr) == NULL)
    {
        s_fail++;
        printf("  FAIL  stderr 重定向失败 —— 捕获机制本身失效，后面的输出判据无意义\n");
    }
}

static void capture_end(char* out, size_t outsz)
{
    fflush(stderr);
    if (s_saved_stderr >= 0)
    {
        dup2(s_saved_stderr, fileno(stderr));
        close(s_saved_stderr);
        s_saved_stderr = -1;
    }
    out[0] = '\0';
    FILE* fp = fopen(CAPTURE_PATH, "r");
    if (fp != NULL)
    {
        size_t n = fread(out, 1, outsz - 1, fp);
        out[n] = '\0';
        fclose(fp);
    }
}

static int has(const char* hay, const char* needle) { return strstr(hay, needle) != NULL; }

int main(void)
{
    char out[8192];

    printf("=== 日志门槛 / 轮询周期下沉契约（scenario=%d）===\n", HMI_LOG_TEST_SCENARIO);
    printf("    HMI_POLL_INTERVAL_MS=%d（期望 %d）  HMI_LOG_LEVEL=%d\n",
           HMI_POLL_INTERVAL_MS, EXP_POLL_MS, LOG_LEVEL_FOR_PRINT);

    /* 1) 轮询周期：带生成头时取配置值，缺头时回退固件历史默认值。 */
    check(HMI_POLL_INTERVAL_MS == EXP_POLL_MS, "轮询周期取到本场景期望的值");

    /* 2) 级别常量的存在性（场景 4 缺生成头 ⇒ 不该存在）。 */
#if EXP_HAS_LEVEL_CONSTS
    check(HMI_LOG_LVL_DEBUG == 0 && HMI_LOG_LVL_INFO == 1 && HMI_LOG_LVL_WARNING == 2
              && HMI_LOG_LVL_ERROR == 3 && HMI_LOG_LVL_FATAL == 4,
          "级别常量与编辑器 LogLevel 枚举同向对齐（0..4，数字越大越严重）");
#else
    check(1, "缺生成头 ⇒ 不提供级别常量（设备端不得另写一份数值）");
#endif

    /* 3) 门槛真的裁剪输出 —— 五个级别各发一行，看谁出来了。 */
    capture_begin();
    HMI_LOG_DEBUG("probe-debug");
    HMI_LOG_INFO("probe-info");
    HMI_LOG_WARN("probe-warn");
    HMI_LOG_ERR("probe-error");
    HMI_LOG("probe-always");
    capture_end(out, sizeof(out));

    check(has(out, "probe-debug") == (EXP_DBG != 0),   "DEBUG 级输出与否符合门槛");
    check(has(out, "probe-info")  == (EXP_INFO != 0),  "INFO  级输出与否符合门槛");
    check(has(out, "probe-warn")  == (EXP_WARN != 0),  "WARN  级输出与否符合门槛");
    check(has(out, "probe-error") == (EXP_ERR != 0),   "ERROR 级输出与否符合门槛");

    /* 4) HMI_LOG(...) 定位为**无条件关键信息**：任何门槛下都必须出来。
     *    它被静音等于把启动横幅 / FATAL / 操作审计一起关掉。 */
    check(has(out, "probe-always"), "无条件 HMI_LOG(...) 不受门槛约束（任何级别下都输出）");

    /* 5) 输出格式仍带 [hmi] 前缀（门槛不该顺手改掉日志形状）。 */
    check(has(out, "[hmi] probe-always"), "输出仍带 [hmi] 前缀");

    /* 扫描面自检：判据条数少于预期说明某段逻辑没跑到（例如捕获失效后 out 恒为空）。 */
    if (s_pass + s_fail < 5)
    {
        printf("  FAIL  判据条数异常（%d < 5）—— 扫描面可能失效\n", s_pass + s_fail);
        s_fail++;
    }

    printf("=== 结果: %d 通过, %d 失败 ===\n", s_pass, s_fail);
    return s_fail > 0 ? 1 : 0;
}
