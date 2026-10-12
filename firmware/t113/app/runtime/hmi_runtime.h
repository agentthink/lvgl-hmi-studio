/*
 * hmi_runtime.h — 运行时公共接口
 *
 * 职责：
 *   - 聚合所有运行时头文件（配置/变量表/屏幕管理/通信）
 *   - 提供版本信息；日志宏转发到 hmi_log.h（那份不拉 lvgl，主机侧可单测）
 *   - 声明设备层接口 var_raw_read / var_raw_write（与编辑器代码生成约定一致）
 */
#ifndef HMI_RUNTIME_H
#define HMI_RUNTIME_H

#include <stdint.h>
#include <stddef.h>
#include <string.h>
#include <stdio.h>

#include "lvgl.h"

/* 固件版本（与 docs/version-info.md 同步） */
#define HMI_RUNTIME_VERSION_MAJOR 1
#define HMI_RUNTIME_VERSION_MINOR 1
#define HMI_RUNTIME_VERSION_PATCH 0
#define HMI_RUNTIME_VERSION_STR   "1.1.0"

/* 目标平台标识（与 AllwinnerT113Target.Metadata.Id 对齐） */
#define HMI_TARGET_ID "allwinner-t113"

/* ── 日志宏 —— 定义在独立的 hmi_log.h 里 ──
 * 独立成文件是为了让主机侧单测能直接 include 它（本聚合头会拉进 lvgl.h，而主机侧没有 LVGL）。
 * 两档用法（无条件 HMI_LOG / 受门槛约束的分级宏）与门槛语义见该文件头注释。 */
#include "hmi_log.h"

/* 运行时配置与生成代码（lv_conf.h/tags.h 通过编译期 -I 提供，见 app/Makefile） */
#include "runtime_config.h"
#include "lv_conf.h"
#include "tags.h"
#include "variable_table.h"
#include "screen_manager.h"
#include "hmi_actions.h"

/* ── 设备层接口（编辑器生成的 get_tag_value/set_tag_value 依赖） ── */
float var_raw_read(int index);              /* 读变量原始值（缓存） */
void  var_raw_write(int index, float raw);  /* 写变量原始值（缓存+下发设备） */

#endif /* HMI_RUNTIME_H */
