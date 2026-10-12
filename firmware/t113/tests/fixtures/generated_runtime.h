/* ============================================================
 * 设备端运行时参数 —— 由 HMI Editor 生成，请勿手改
 *   单一来源：HmiProject.RuntimeConfig
 *   消费方：firmware runtime/runtime_config.h（变量表轮询周期）
 *           与 runtime/hmi_log.h（日志门槛）—— 两处都用
 *           __has_include 优先引用本头，缺省回退历史默认值。
 * ============================================================ */
#ifndef HMI_GENERATED_RUNTIME_H
#define HMI_GENERATED_RUNTIME_H

/* ── 变量表轮询周期（毫秒）。RuntimeConfig.ScanIntervalMs ──
 * 固件 comm_core.c 的采集循环按此周期休眠（variable_table.c 在周期内合并块读）。
 * 0 或负值会让采集线程忙等烧 CPU ⇒ 生成期挡在区间的外面。 */
#ifndef HMI_POLL_INTERVAL_MS
#define HMI_POLL_INTERVAL_MS 250
#endif

/* ── 日志门槛。RuntimeConfig.LogLevel —— 输出「严重度 >= 本值」的日志 ──
 * 固件 runtime/hmi_log.h 的分级宏族（HMI_LOG_DEBUG/INFO/WARN/ERR）按它裁剪；
 * HMI_LOG(...) 定位为**无条件关键信息**，不受本门槛约束。
 * 级别常量由 LogLevel 枚举推导 —— 设备端不得另写一份数值。 */
#define HMI_LOG_LVL_DEBUG   0
#define HMI_LOG_LVL_INFO    1
#define HMI_LOG_LVL_WARNING 2
#define HMI_LOG_LVL_ERROR   3
#define HMI_LOG_LVL_FATAL   4
#ifndef HMI_LOG_LEVEL
#define HMI_LOG_LEVEL 2
#endif

#endif /* HMI_GENERATED_RUNTIME_H */
