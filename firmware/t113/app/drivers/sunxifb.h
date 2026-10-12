/**
 * @file sunxifb.h
 *
 * sunxifb — Allwinner sunxi framebuffer 显示驱动（LVGL v9 移植版）
 *
 * 原文件位于 Tina SDK littlevgl-8/lv_drivers/display/sunxifb.h，
 * 因 SDK 升级 LVGL 9.x 后不再提供 lv_drivers，故移植到本仓库：
 *  - 删除 lv_drv_conf.h 依赖，开关宏直接在此定义（对齐原 SDK 配置）
 *  - flush_cb 签名由 v8 (lv_disp_drv_t*, lv_color_t*) 改为 v9 (lv_display_t*, uint8_t*)
 */

#ifndef SUNXIFB_H
#define SUNXIFB_H

#ifdef __cplusplus
extern "C" {
#endif

/*********************
 *      INCLUDES
 *********************/
#include "lvgl.h"

/*********************
 *      DEFINES（原 lv_drv_conf.h + sunxifb.mk 的等价配置）
 *********************/
#define USE_SUNXIFB 1
#define USE_SUNXIFB_DOUBLE_BUFFER 1
#define USE_SUNXIFB_CACHE 1
/* WavePI-S6（bingpi_m2）禁用 G2D：sunxifb_init 的 G2D 分支走 ION
 * （SunxiMemOpen/Palloc），WavePi 内核 ION 驱动差异导致 sunxifb_mem_alloc
 * 破坏堆 → 后续 malloc 崩溃（2026-08-19 实机定位）。纯软件 fb 渲染。
 * t113-mq_r 若需恢复硬件加速，可改回 1（mq_r ION 正常）。 */
#define USE_SUNXIFB_G2D 0
#define USE_SUNXIFB_G2D_ROTATE 0

/* G2D draw-unit 加速（fill/blit/blend/scale）未接入，保持 0：
 * 运行时仅使用 flush 路径的旋转 blit（sunxifb_g2d_blit_to_fb），
 * 避免编译依赖 v8 私有 API 的 sunxifb_g2d_scale 等函数。 */
#define LV_USE_SUNXIFB_G2D_FILL 0
#define LV_USE_SUNXIFB_G2D_BLEND 0
#define LV_USE_SUNXIFB_G2D_BLIT 0
#define LV_USE_SUNXIFB_G2D_SCALE 0

#if USE_SUNXIFB

/**********************
 * GLOBAL PROTOTYPES
 **********************/
void sunxifb_init(uint32_t rotated);
void sunxifb_exit(void);
void sunxifb_flush(lv_display_t * disp, const lv_area_t * area, uint8_t * px_map);
void sunxifb_get_sizes(uint32_t *width, uint32_t *height);
void* sunxifb_alloc(size_t size, char *label);
void sunxifb_free(void **data, char *label);
#ifdef USE_SUNXIFB_DOUBLE_BUFFER
bool sunxifb_get_dbuf_en(void);
int sunxifb_set_dbuf_en(lv_display_t * disp, bool dbuf_en);
#endif /* USE_SUNXIFB_DOUBLE_BUFFER */

#endif  /*USE_SUNXIFB*/

#ifdef __cplusplus
} /* extern "C" */
#endif

#endif /*SUNXIFB_H*/
