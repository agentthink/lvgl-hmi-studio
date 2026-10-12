/*
 * hmi_log.h — 设备端日志宏（独立成文件，**刻意不 include lvgl.h**）
 *
 * 为什么单拎出来：聚合头 hmi_runtime.h 会拉进 lvgl.h，而主机侧单元测试没有 LVGL
 * （见 tests/run_host_tests.sh）。日志门槛这条链路要能在主机上真的编译并断言
 * 「哪个级别会输出」，就不能挂在聚合头后面 —— 否则闭环变成"只有注释声称生效"。
 *
 * 两档用法，别混：
 *   HMI_LOG(...)                     —— 无条件关键信息（启动横幅 / FATAL / 操作审计）。
 *                                      永远输出、不受门槛约束：那些是"没它就没法归因"的
 *                                      信息，让日志级别能把它们静音等于关掉现场排查能力。
 *   HMI_LOG_DEBUG/INFO/WARN/ERR(...) —— 分级日志，按 HMI_LOG_LEVEL 裁剪。
 *
 * 门槛语义：输出「严重度 >= HMI_LOG_LEVEL」的日志，与编辑器侧 Models.LogLevel 枚举
 * 同向（数字越大越严重）。默认 Warning(2) ⇒ DEBUG 与 INFO 静默；要看调试输出就把
 * 工程设置 → 运行时 → 日志级别调到 Debug 后重新导出。
 *
 * 门槛与级别常量由编辑器下发的 generated_runtime.h 提供（RuntimeConfig 下沉产物）。
 * 缺这份头时分级宏退化为无条件输出（见下面 HMI_LOG_AT 的 #else 分支）—— 不带生成头的
 * 手写构建也能编，行为与历史一致。
 *
 * HMI_LOG_AT 的 #else 分支**丢弃 lvl 实参**：参数名不出现在替换列表里，按 C 标准
 * 该实参连展开都不会展开，所以缺生成头时 HMI_LOG_LVL_* 未定义也不会编译报错。
 */
#ifndef HMI_LOG_H
#define HMI_LOG_H

#include <stdio.h>

#if defined(__has_include)
#  if __has_include("generated_runtime.h")
#    include "generated_runtime.h"
#  endif
#endif

/* 默认走 stderr（设备端可重定向到 syslog/文件）。 */
#define HMI_LOG(fmt, ...) \
    fprintf(stderr, "[hmi] " fmt "\n", ##__VA_ARGS__)

#ifdef HMI_LOG_LEVEL
#  define HMI_LOG_AT(lvl, fmt, ...) \
       do { if ((lvl) >= HMI_LOG_LEVEL) fprintf(stderr, "[hmi] " fmt "\n", ##__VA_ARGS__); } while (0)
#else
#  define HMI_LOG_AT(lvl, fmt, ...) \
       fprintf(stderr, "[hmi] " fmt "\n", ##__VA_ARGS__)
#endif

/* 级别常量 HMI_LOG_LVL_* 由生成头给出（设备端不得另写一份数值）。 */
#define HMI_LOG_DEBUG(fmt, ...) HMI_LOG_AT(HMI_LOG_LVL_DEBUG,   fmt, ##__VA_ARGS__)
#define HMI_LOG_INFO(fmt, ...)  HMI_LOG_AT(HMI_LOG_LVL_INFO,    fmt, ##__VA_ARGS__)
#define HMI_LOG_WARN(fmt, ...)  HMI_LOG_AT(HMI_LOG_LVL_WARNING, fmt, ##__VA_ARGS__)
#define HMI_LOG_ERR(fmt, ...)   HMI_LOG_AT(HMI_LOG_LVL_ERROR,   fmt, ##__VA_ARGS__)

#endif /* HMI_LOG_H */
