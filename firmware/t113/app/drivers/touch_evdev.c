/*
 * touch_evdev.c — evdev 触摸驱动实现
 *
 * 支持 ABS_X/ABS_Y（电阻/单点电容）与 ABS_MT_*（多点电容，取最后一个触点）。
 * 坐标按触摸分辨率（HMI_TOUCH_MAX_X/Y）等比缩放到 LCD 分辨率。
 * 触摸分辨率与 event 节点在《LVGL篇》中说明：电阻屏需修改触摸分辨率配置。
 */
#include "touch_evdev.h"
#include "touch_scale.h"
#include "hmi_runtime.h"
#include "runtime_config.h"
#include "sys_settings.h"

#include <fcntl.h>
#include <unistd.h>
#include <errno.h>
#include <string.h>
#include <stdio.h>
#include <stdlib.h>
#include <linux/input.h>

/* 触摸量程必须由板级配置提供（runtime_config.h 按 HMI_BOARD_WAVEPI 给出，
 * 或用 -DHMI_TOUCH_MAX_X/Y=... 覆盖）。这里不再就地兜底硬编码：
 * 驱动默认值与板级实际量程两处事实源，正是 P0-7（边缘不可达）的成因。 */
#if !defined(HMI_TOUCH_MAX_X) || !defined(HMI_TOUCH_MAX_Y)
#error "HMI_TOUCH_MAX_X/Y 未定义：应由 runtime_config.h 按板级提供，或用 -D 覆盖"
#endif

static int s_fd = -1;

/* 触摸校准参数（/etc/hmi/touch_cal.conf: sx ox sy oy，LCD 坐标补偿） */
#define TOUCH_CAL_CONF "/etc/hmi/touch_cal.conf"
static float s_cal_sx = 1.0f, s_cal_ox = 0.0f;
static float s_cal_sy = 1.0f, s_cal_oy = 0.0f;

static void touch_cal_load(void)
{
    FILE* fp = fopen(TOUCH_CAL_CONF, "r");
    if (!fp) return;
    float sx, ox, sy, oy;
    if (fscanf(fp, "%f %f %f %f", &sx, &ox, &sy, &oy) == 4) {
        if (sx > 0.1f && sx < 10.0f) { s_cal_sx = sx; s_cal_ox = ox; }
        if (sy > 0.1f && sy < 10.0f) { s_cal_sy = sy; s_cal_oy = oy; }
    }
    fclose(fp);
}

/* 坐标缩放：触摸控制器坐标 → LCD 坐标（纯函数，见 touch_scale.h 与主机侧测试） */
static lv_coord_t scale_x(int v)
{
    return (lv_coord_t)touch_scale_axis((int32_t)v, HMI_TOUCH_MAX_X, HMI_LCD_WIDTH);
}
static lv_coord_t scale_y(int v)
{
    return (lv_coord_t)touch_scale_axis((int32_t)v, HMI_TOUCH_MAX_Y, HMI_LCD_HEIGHT);
}

static void evdev_read_cb(lv_indev_t* drv, lv_indev_data_t* data)
{
    (void)drv;
    static lv_coord_t last_x = 0, last_y = 0;
    static uint8_t pressed = 0;
    struct input_event ev;

    /* ⚠ 阶段 121：旧写法是 `while (read(...) == sizeof(ev))`，读完既不认 EAGAIN 也不认真错误 ——
       设备被拔出/驱动出错（ENODEV/EBADF/EIO）时循环直接退出，然后**拿陈旧坐标继续上报**，
       而注释里承诺的"其他错误关闭输入"从未发生（s_fd 一直是那个死 fd）。
       现在分三档：够一条事件 → 处理；EAGAIN → 正常收尾；其它错误/EOF → 关输入并抬起。 */
    for (;;) {
        ssize_t n = read(s_fd, &ev, sizeof(ev));
        if (n == (ssize_t)sizeof(ev)) {
            switch (ev.type) {
            case EV_ABS:
                if (ev.code == ABS_X || ev.code == ABS_MT_POSITION_X)
                    last_x = scale_x(ev.value);
                else if (ev.code == ABS_Y || ev.code == ABS_MT_POSITION_Y)
                    last_y = scale_y(ev.value);
                else if (ev.code == ABS_MT_TRACKING_ID && ev.value == -1)
                    pressed = 0;   /* 手指抬起 */
                break;
            case EV_KEY:
                if (ev.code == BTN_TOUCH)
                    pressed = ev.value ? 1 : 0;
                break;
            default:
                break;
            }
            continue;
        }
        if (n < 0 && (errno == EAGAIN || errno == EWOULDBLOCK))
            break;                      /* 非阻塞下"没有更多事件"是正常的 */
        /* 真错误或 EOF：关掉输入，别再用陈旧坐标装作有触摸 */
        HMI_LOG("touch read failed: %s — 关闭触摸输入", strerror(errno));
        close(s_fd);
        s_fd = -1;
        pressed = 0;
        break;
    }
    if (s_fd < 0) {
        data->state = LV_INDEV_STATE_RELEASED;
        return;
    }

    /* 应用触摸校准补偿 */
    data->point.x = (lv_coord_t)(last_x * s_cal_sx + s_cal_ox);
    data->point.y = (lv_coord_t)(last_y * s_cal_sy + s_cal_oy);
    data->state = pressed ? LV_INDEV_STATE_PRESSED : LV_INDEV_STATE_RELEASED;
}

int touch_evdev_init(const char* node)
{
    if (!node) return -1;
    s_fd = open(node, O_RDWR | O_NOCTTY | O_NONBLOCK);
    if (s_fd < 0) {
        HMI_LOG("touch open %s failed: %s", node, strerror(errno));
        return -1;
    }

    touch_cal_load();

    /* LVGL v9：lv_indev_create + lv_indev_set_read_cb
     * （替代 v8 的 lv_indev_drv_init/lv_indev_drv_register 旧 API）。 */
    lv_indev_t* indev = lv_indev_create();
    if (indev == NULL) {
        HMI_LOG("touch indev create failed");
        close(s_fd);
        s_fd = -1;
        return -1;
    }
    lv_indev_set_type(indev, LV_INDEV_TYPE_POINTER);
    lv_indev_set_read_cb(indev, evdev_read_cb);
    HMI_LOG("touch ready (%s)", node);
    return 0;
}

void touch_evdev_deinit(void)
{
    if (s_fd >= 0) {
        close(s_fd);
        s_fd = -1;
    }
}
