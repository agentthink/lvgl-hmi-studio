/*
 * hmi_alarm_source.h — 设备侧报警上行的**一轮**入口（阶段 291）
 *
 * 只有这一个声明、**不拉 lvgl**：通信线程（comm_core.c）要用它，而通信模块不解析 lvgl.h。
 * 实现见 hmi_alarm_source.c（那里才 include hmi_ui_api.h 取 .so 的能力表）。
 */
#ifndef HMI_ALARM_SOURCE_H
#define HMI_ALARM_SOURCE_H

/* 取一次 .so 的报警快照并按契约上行；没有 .so / .so 不支持快照 ⇒ 空操作。 */
void hmi_alarm_publish_tick(void);

#endif /* HMI_ALARM_SOURCE_H */
