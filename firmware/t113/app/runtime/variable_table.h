/*
 * variable_table.h — 变量表（设备层实现）
 *
 * 实现编辑器代码生成约定的设备层接口：
 *   var_raw_read / var_raw_write
 * 缓存由通信线程通过 hmi_var_poll_once 刷新，UI 线程只读缓存。
 */
#ifndef HMI_VARIABLE_TABLE_H
#define HMI_VARIABLE_TABLE_H

#include <stdint.h>
#include "comm_core.h"
#include "tags.h"   /* tag_meta_t / tag_binding_t（标签表采纳接口） */

/* 变量原始值缓存访问（线程安全） */
float var_raw_read(int index);
void  var_raw_write(int index, float raw);

/* MQTT 订阅回调专用：仅更新缓存，不回发设备、不触发发布回环 */
void var_set_from_sub(int index, float v);

/* 初始化：采纳编译期默认标签表并构建轮询组 */
int hmi_var_init(void);

/* ABI V3 热更新：宿主采纳 .so 标签表（metas/bindings 为 NULL 时回退编译期默认表）。
 * 缓存/只读保护/轮询组/MQTT 主题全部按新表重建；hmi_ui_load 成功后调用。 */
int hmi_tags_adopt(const tag_meta_t* metas, const tag_binding_t* bindings, int count);

/* 当前生效标签数（引擎轮询等使用） */
int hmi_tag_count(void);

/* 通信线程钩子 */
void hmi_var_poll_once(void);       /* 轮询下一个组并刷新缓存 */
void hmi_var_drain_writes(void);    /* 消费写请求队列 */

/* MQTT 周期发布扫描：检测到缓存变化后发布（通信线程调用，建议 1s 一次） */
void hmi_var_mqtt_publish_scan(void);

/* 标签名 → 索引（MQTT 主题分发用；未找到返回 -1） */
int hmi_tag_index_by_name(const char* name);

/* 标签元数据只读访问（远程写命令的 target 元数据 / 订阅注册用）；越界返回 NULL。
 * 返回的是**当前生效表**里的那一项（采纳 .so 标签表后即工程表）—— 不要缓存指针，
 * hmi_tags_adopt 会整体换表。 */
const tag_meta_t* hmi_tag_meta_at(int index);

/* 开机时长 ms（单调时钟）—— 所有上行的 `tb=boot` 时间基都用它（报警上行同此口径）。 */
unsigned long long hmi_var_uptime_ms(void);

#endif /* HMI_VARIABLE_TABLE_H */
