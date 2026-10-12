/*
 * set_sound.c — 声音设置模块
 *
 * - 系统音量：slider 0-100 → amixer 控制（audiocodec）
 * - 蜂鸣器：开关（配置项；无独立蜂鸣器硬件时仅记录）
 * 布局：正文 14px、值 16px，控件宽度随内容区自适应。
 */
#include "sys_settings.h"
#include "settings_ui.h"
#include "sys_cmd.h"
#include "hmi_runtime.h"
#include <stdio.h>

static lv_obj_t* s_slider = NULL;
static lv_obj_t* s_val_lbl = NULL;
static lv_obj_t* s_buzz_sw = NULL;

#define SOUND_CONF "/etc/hmi/sound.conf"

static void slider_cb(lv_event_t* e)
{
    (void)e;
    if (!s_val_lbl) return;
    int v = (int)lv_slider_get_value(s_slider);
    char buf[32];
    snprintf(buf, sizeof(buf), "%d %%", v);
    lv_label_set_text(s_val_lbl, buf);
}

static void btn_apply_cb(lv_event_t* e)
{
    (void)e;
    int v = (int)lv_slider_get_value(s_slider);
    int buzz = lv_obj_has_state(s_buzz_sw, LV_STATE_CHECKED) ? 1 : 0;
    /* 尝试设置主音量（不同固件控件名不同，忽略失败） */
    char cmd[128];
    snprintf(cmd, sizeof(cmd),
        "amixer -D hw:audiocodec cset name='Headphone volume' %d%% 2>/dev/null; "
        "amixer -D hw:audiocodec cset name='ADC3 volume' %d 2>/dev/null", v, v * 2);
    sys_cmd_exec(cmd);
    char conf[64];
    snprintf(conf, sizeof(conf), "volume=%d buzz=%d\n", v, buzz);
    sys_file_write(SOUND_CONF, conf);
    if (s_val_lbl) lv_label_set_text(s_val_lbl, "已保存");
    HMI_LOG("sys_settings: sound apply volume=%d buzz=%d", v, buzz);
}

lv_obj_t* set_sound_create(lv_obj_t* parent)
{
    int w = set_ui_w(parent);

    /* 内容区被 lv_obj_clean 清空后，下列静态指针已失效；先置 NULL，
     * 防止 slider/switch/按钮回调在重建前访问已释放对象（UAF）。 */
    s_slider = NULL;
    s_val_lbl = NULL;
    s_buzz_sw = NULL;

    set_ui_lbl(parent, "系统音量", SET_PAD_X, 20, SET_FONT_BODY, -1);
    s_slider = lv_slider_create(parent);
    lv_obj_set_size(s_slider, 440, 20);
    lv_obj_align(s_slider, LV_ALIGN_TOP_LEFT, SET_CTRL_X, 16);
    lv_slider_set_range(s_slider, 0, 100);
    lv_slider_set_value(s_slider, 50, LV_ANIM_OFF);
    lv_obj_add_event_cb(s_slider, slider_cb, LV_EVENT_VALUE_CHANGED, NULL);

    s_val_lbl = lv_label_create(parent);
    lv_label_set_text(s_val_lbl, "50 %");
    lv_obj_set_style_text_font(s_val_lbl, SET_FONT_TITLE, 0);
    lv_obj_align(s_val_lbl, LV_ALIGN_TOP_LEFT, SET_CTRL_X + 450, 14);

    set_ui_lbl(parent, "蜂鸣器", SET_PAD_X, 80, SET_FONT_BODY, -1);
    s_buzz_sw = lv_switch_create(parent);
    lv_obj_align(s_buzz_sw, LV_ALIGN_TOP_LEFT, SET_CTRL_X, 72);
    lv_obj_add_state(s_buzz_sw, LV_STATE_CHECKED);

    set_ui_lbl(parent, "开启", SET_CTRL_X + 60, 80, SET_FONT_BODY, -1);

    /* 应用按钮（底部居中） */
    set_ui_btn(parent, "应用", (w - 140) / 2, set_ui_btn_y(parent), 140, SET_CLR_OK, btn_apply_cb);

    return parent;
}
