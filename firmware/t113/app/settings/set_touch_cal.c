/*
 * set_touch_cal.c — 触摸校准模块
 *
 * 5 点校准：依次点击屏幕上的校准十字，记录实际触摸坐标与目标坐标，
 * 计算线性映射（scale/offset），保存到 /etc/hmi/touch_cal.conf：
 *   sx ox sy oy   （LCD 坐标补偿：x' = x*sx + ox, y' = y*sy + oy）
 * touch_evdev.c 启动时读取并应用。
 * 校准点按屏幕分辨率比例分布（响应式），正文 ≥14px。
 */
#include "sys_settings.h"
#include "settings_ui.h"
#include "sys_cmd.h"
#include "hmi_runtime.h"
#include <stdio.h>
#include <string.h>
#include <math.h>

#define CAL_POINT_COUNT 5
#define TOUCH_CAL_CONF  "/etc/hmi/touch_cal.conf"

static lv_point_t s_targets[CAL_POINT_COUNT];   /* 校准目标点（屏幕绝对坐标） */

static lv_obj_t* s_cross = NULL;      /* 十字标记 */
static lv_obj_t* s_hint = NULL;
static lv_obj_t* s_grab = NULL;       /* 全屏点击捕获层 */
static lv_point_t s_captured[CAL_POINT_COUNT];
static int s_step = 0;
static int s_running = 0;

/* ── 内部辅助：终止当前校准（在切 tab / 退出设置页时调用） ── */
void set_touch_cal_abort(void)
{
    if (!s_running) return;
    s_running = 0;
    s_step = 0;
    if (s_grab) { lv_obj_delete_async(s_grab); s_grab = NULL; }
    if (s_cross) { lv_obj_delete_async(s_cross); s_cross = NULL; }
    s_hint = NULL;
    HMI_LOG("sys_settings: touch cal aborted");
}

static void finish_cal(void)
{
    s_running = 0;
    if (s_grab) { lv_obj_delete_async(s_grab); s_grab = NULL; }
    if (s_cross) { lv_obj_delete_async(s_cross); s_cross = NULL; }

    /* 用 4 个角点（索引 0..3）两组对角计算 scale/offset。
       ⚠ 分母必须判零（阶段 121）：用户在同一个位置点两下（或触点没动）会让 scale
       变成 inf/nan，而设备端 touch_evdev **明确拒绝 nan/inf** ⇒ 界面报"校准完成"、
       实际参数从未生效，且用户看不出哪里错了。 */
    float dx1 = (float)(s_captured[0].x - s_captured[1].x);
    float dx2 = (float)(s_captured[2].x - s_captured[3].x);
    float dy1 = (float)(s_captured[0].y - s_captured[2].y);
    float dy2 = (float)(s_captured[1].y - s_captured[3].y);
    if (dx1 == 0.0f || dx2 == 0.0f || dy1 == 0.0f || dy2 == 0.0f) {
        if (s_hint) lv_label_set_text(s_hint, "校准失败：采样点重合，请重新校准");
        HMI_LOG("sys_settings: touch cal rejected (degenerate samples)");
        return;
    }

    float sx1 = (float)(s_targets[0].x - s_targets[1].x) / dx1;
    float sx2 = (float)(s_targets[2].x - s_targets[3].x) / dx2;
    float sy1 = (float)(s_targets[0].y - s_targets[2].y) / dy1;
    float sy2 = (float)(s_targets[1].y - s_targets[3].y) / dy2;
    float sx = (sx1 + sx2) / 2.0f;
    float sy = (sy1 + sy2) / 2.0f;
    float ox = s_targets[0].x - s_captured[0].x * sx;
    float oy = s_targets[0].y - s_captured[0].y * sy;

    if (!isfinite(sx) || !isfinite(ox) || !isfinite(sy) || !isfinite(oy)) {
        if (s_hint) lv_label_set_text(s_hint, "校准失败：参数非法，请重新校准");
        HMI_LOG("sys_settings: touch cal rejected (non-finite)");
        return;
    }

    char conf[128];
    snprintf(conf, sizeof(conf), "%.5f %.5f %.5f %.5f\n", (double)sx, (double)ox, (double)sy, (double)oy);
    if (sys_file_write(TOUCH_CAL_CONF, conf) != 0) {
        if (s_hint) lv_label_set_text(s_hint, "保存校准参数失败！");
        return;
    }
    char msg[96];
    snprintf(msg, sizeof(msg), "校准完成（sx=%.2f ox=%.2f sy=%.2f oy=%.2f）",
             (double)sx, (double)ox, (double)sy, (double)oy);
    if (s_hint) lv_label_set_text(s_hint, msg);
    HMI_LOG("sys_settings: touch cal %s", conf);
}

static void grab_click_cb(lv_event_t* e)
{
    if (!s_running) return;
    lv_obj_t* ta = lv_event_get_target(e);
    lv_indev_t* indev = lv_event_get_indev(e);
    lv_point_t pt;
    if (indev) lv_indev_get_point(indev, &pt);
    else { pt.x = 0; pt.y = 0; }

    s_captured[s_step] = pt;
    s_step++;

    if (s_step >= CAL_POINT_COUNT) {
        finish_cal();
        return;
    }
    /* 移动到下一个校准点（s_cross/s_hint 可能在切 tab 时被清理，需判空） */
    if (s_cross) lv_obj_align(s_cross, LV_ALIGN_TOP_LEFT, s_targets[s_step].x, s_targets[s_step].y);
    if (s_hint) {
        char buf[64];
        snprintf(buf, sizeof(buf), "请点击第 %d 点（十字中心）", s_step + 1);
        lv_label_set_text(s_hint, buf);
    }
    (void)ta;
}

static void btn_start_cb(lv_event_t* e)
{
    (void)e;
    if (s_running) return;
    s_running = 1;
    s_step = 0;

    /* 创建全屏点击捕获层 */
    s_grab = lv_obj_create(lv_screen_active());
    lv_obj_set_size(s_grab, LV_PCT(100), LV_PCT(100));
    lv_obj_align(s_grab, LV_ALIGN_TOP_MID, 0, 0);
    lv_obj_set_style_bg_opa(s_grab, LV_OPA_TRANSP, 0);
    lv_obj_set_style_border_width(s_grab, 0, 0);
    lv_obj_clear_flag(s_grab, LV_OBJ_FLAG_SCROLLABLE);
    lv_obj_add_flag(s_grab, LV_OBJ_FLAG_CLICKABLE);
    lv_obj_add_event_cb(s_grab, grab_click_cb, LV_EVENT_CLICKED, NULL);

    /* 十字标记 */
    s_cross = lv_obj_create(lv_screen_active());
    lv_obj_set_size(s_cross, 40, 40);
    lv_obj_align(s_cross, LV_ALIGN_TOP_LEFT, s_targets[0].x, s_targets[0].y);
    lv_obj_set_style_bg_opa(s_cross, LV_OPA_TRANSP, 0);
    lv_obj_set_style_border_width(s_cross, 2, 0);
    lv_obj_set_style_border_color(s_cross, lv_color_hex(0xff5050), 0);
    lv_obj_t* cross_lbl = lv_label_create(s_cross);
    lv_label_set_text(cross_lbl, "+");
    lv_obj_set_style_text_color(cross_lbl, lv_color_hex(0xff5050), 0);
    lv_obj_set_style_text_font(cross_lbl, SET_FONT_BIG, 0);
    lv_obj_set_align(cross_lbl, LV_ALIGN_CENTER);

    lv_label_set_text(s_hint, "请点击第 1 点（十字中心）");
}

lv_obj_t* set_touch_cal_create(lv_obj_t* parent)
{
    s_running = 0;
    s_step = 0;
    s_cross = NULL;
    s_grab = NULL;

    /* 校准点按屏幕分辨率比例分布（避开顶部标题/导航栏 y>136） */
    int sw = lv_obj_get_width(lv_screen_active());
    int sh = lv_obj_get_height(lv_screen_active());
    s_targets[0] = (lv_point_t){ sw * 15 / 100, sh * 42 / 100 };
    s_targets[1] = (lv_point_t){ sw * 85 / 100, sh * 42 / 100 };
    s_targets[2] = (lv_point_t){ sw * 50 / 100, sh * 62 / 100 };
    s_targets[3] = (lv_point_t){ sw * 15 / 100, sh * 92 / 100 };
    s_targets[4] = (lv_point_t){ sw * 85 / 100, sh * 92 / 100 };

    int w = set_ui_w(parent);

    s_hint = lv_label_create(parent);
    lv_obj_set_style_text_font(s_hint, SET_FONT_TITLE, 0);
    lv_obj_set_style_text_color(s_hint, lv_color_hex(SET_CLR_HIL), 0);
    lv_obj_align(s_hint, LV_ALIGN_TOP_LEFT, SET_PAD_X, 16);
    lv_label_set_text(s_hint, "触摸校准：校准屏幕触摸坐标映射");

    lv_obj_t* tip = lv_label_create(parent);
    lv_label_set_text(tip, "将依次显示 5 个校准点，\n请用手指准确点击每个十字的中心。\n"
                           "校准结果将保存并立即生效。");
    lv_obj_set_style_text_font(tip, SET_FONT_BODY, 0);
    lv_obj_set_style_text_color(tip, lv_color_hex(SET_CLR_SUB), 0);
    lv_obj_align(tip, LV_ALIGN_TOP_LEFT, SET_PAD_X, 68);

    /* 开始校准按钮（底部居中） */
    set_ui_btn(parent, "开始校准", (w - 220) / 2, set_ui_btn_y(parent), 220, SET_CLR_ACC, btn_start_cb);

    return parent;
}
