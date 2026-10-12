/*
 * lcd_sunxifb.c — 显示驱动实现（LVGL v9，使用仓库内置 sunxifb 驱动）
 *
 * sunxifb 接口（本仓库 app/drivers/sunxifb.[ch]，自 littlevgl-8 移植）：
 *   sunxifb_init(rotated) / sunxifb_exit()
 *   sunxifb_get_sizes(&w,&h)
 *   sunxifb_alloc(size, label) / sunxifb_free(void**, label)
 *   sunxifb_flush(disp, area, px_map)  —— LVGL v9 刷屏回调
 * 编译：-I<src根>（含 lvgl/），链接 lvgl 源码 + -luapi（G2D）。
 */
#include "lcd_sunxifb.h"
#include "hmi_runtime.h"
#include "lvgl.h"
#include "sunxifb.h"

static lv_color_t* s_buf = NULL;
static lv_display_t* s_disp = NULL;

int lcd_sunxifb_init(void)
{
    lv_display_rotation_t rotated = LV_DISPLAY_ROTATION_0;

    /* 初始化显示引擎 */
    sunxifb_init(rotated);
    HMI_LOG_DEBUG("sunxifb_init done");

    /* 获取实际分辨率（dts 屏参，无需写死） */
    uint32_t w = HMI_LCD_WIDTH, h = HMI_LCD_HEIGHT;
    sunxifb_get_sizes(&w, &h);
    HMI_LOG_DEBUG("sizes=%ux%u", w, h);

    /* 申请绘图缓冲区（sunxifb_alloc 申请物理连续内存以启用 g2d）。
     * 注意 v9 的 lv_color_t 为 3 字节 RGB，而显示格式 XRGB8888 为 4 字节/像素。 */
    uint32_t px_bytes = LV_COLOR_FORMAT_GET_SIZE(LV_COLOR_FORMAT_XRGB8888);
    HMI_LOG_DEBUG("px_bytes=%u alloc=%u", px_bytes, (unsigned)(w * h * px_bytes));
    /* 诊断：fb mmap 后 malloc 是否正常 */
    void* probe = malloc(4096);
    HMI_LOG_DEBUG("post-fbmap malloc(4096)=%p", probe);
    free(probe);
    s_buf = (lv_color_t*)sunxifb_alloc(w * h * px_bytes, "hmi_runtime");
    HMI_LOG_DEBUG("alloc returned buf=%p", (void*)s_buf);
    HMI_LOG_DEBUG("alloc done buf=%p", (void*)s_buf);
    if (s_buf == NULL) {
        HMI_LOG("sunxifb_alloc failed (%ux%u)", w, h);
        sunxifb_exit();
        return -1;
    }

    /* LVGL v9：lv_display_create + set_flush_cb + set_buffers
     * （替代 v8 的 lv_disp_draw_buf_init/lv_disp_drv_* 旧 API）。
     * 缓冲为全屏单缓冲 + PARTIAL 渲染模式，与 v8 行为一致。 */
    s_disp = lv_display_create((int32_t)w, (int32_t)h);
    HMI_LOG_DEBUG("display create done disp=%p", (void*)s_disp);
    if (s_disp == NULL) {
        HMI_LOG("lv_display_create failed (%ux%u)", w, h);
        sunxifb_free((void**)&s_buf, "hmi_runtime");
        s_buf = NULL;
        sunxifb_exit();
        return -1;
    }
    lv_display_set_flush_cb(s_disp, sunxifb_flush);
    lv_display_set_color_format(s_disp, LV_COLOR_FORMAT_XRGB8888);
    lv_display_set_buffers(s_disp, s_buf, NULL,
                           (uint32_t)(w * h * px_bytes),
                           LV_DISPLAY_RENDER_MODE_PARTIAL);

    HMI_LOG("LCD %ux%u (sunxifb /dev/fb0)", w, h);
    return 0;
}

void lcd_sunxifb_deinit(void)
{
    if (s_buf) sunxifb_free((void**)&s_buf, "hmi_runtime");
    s_buf = NULL;
    sunxifb_exit();
}
