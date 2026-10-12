/*
 * set_display.c — 显示/背光设置模块
 *
 * - 背光亮度：slider 0-255，写入背光 sysfs（/sys/class/backlight 节点 brightness）
 * - 背光休眠：配置项（写入 /etc/hmi/display.conf），实际休眠由运行时 timer 决定
 * 布局：正文 14px、值 16px，控件宽度随内容区自适应。
 */
#include "sys_settings.h"
#include "settings_ui.h"
#include "sys_cmd.h"
#include "hmi_runtime.h"
#include <stdio.h>
#include <string.h>

static lv_obj_t* s_slider = NULL;
static lv_obj_t* s_dd_blank = NULL;
static lv_obj_t* s_val_lbl = NULL;

#define DISPLAY_CONF "/etc/hmi/display.conf"

/* 查找背光 sysfs 节点 */
static int backlight_write(int brightness)
{
    char path[128];
    /* 常见路径：/sys/class/backlight 下的 brightness 节点 */
    if (sys_cmd_get(path, sizeof(path),
        "ls /sys/class/backlight/ 2>/dev/null | sed -n '1p'") == 0 && path[0]) {
        char full[192];
        snprintf(full, sizeof(full), "echo %d > /sys/class/backlight/%s/brightness", brightness, path);
        return sys_cmd_exec(full);
    }
    return -1;
}

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
    int blank = lv_dropdown_get_selected(s_dd_blank);
    if (backlight_write(v) != 0) {
        lv_label_set_text(s_val_lbl, "背光写入失败（无 backlight 节点）");
        return;
    }
    char conf[64];
    snprintf(conf, sizeof(conf), "brightness=%d blank=%d\n", v, blank);
    sys_file_write(DISPLAY_CONF, conf);
    lv_label_set_text(s_val_lbl, "已应用并保存");
    HMI_LOG("sys_settings: display apply brightness=%d blank=%d", v, blank);
}

lv_obj_t* set_display_create(lv_obj_t* parent)
{
    int w = set_ui_w(parent);

    /* 内容区被 lv_obj_clean 清空后，下列静态指针已失效；先置 NULL，
     * 防止 slider/dropdown/按钮回调在重建前访问已释放对象（UAF）。 */
    s_slider = NULL;
    s_dd_blank = NULL;
    s_val_lbl = NULL;

    set_ui_lbl(parent, "背光亮度", SET_PAD_X, 20, SET_FONT_BODY, -1);
    s_slider = lv_slider_create(parent);
    lv_obj_set_size(s_slider, 440, 20);
    lv_obj_align(s_slider, LV_ALIGN_TOP_LEFT, SET_CTRL_X, 16);
    lv_slider_set_range(s_slider, 0, 100);
    lv_slider_set_value(s_slider, 80, LV_ANIM_OFF);
    lv_obj_add_event_cb(s_slider, slider_cb, LV_EVENT_VALUE_CHANGED, NULL);

    s_val_lbl = lv_label_create(parent);
    lv_label_set_text(s_val_lbl, "80 %");
    lv_obj_set_style_text_font(s_val_lbl, SET_FONT_TITLE, 0);
    lv_obj_align(s_val_lbl, LV_ALIGN_TOP_LEFT, SET_CTRL_X + 450, 14);

    set_ui_lbl(parent, "背光休眠", SET_PAD_X, 80, SET_FONT_BODY, -1);
    s_dd_blank = lv_dropdown_create(parent);
    lv_obj_set_size(s_dd_blank, 240, 38);
    lv_obj_align(s_dd_blank, LV_ALIGN_TOP_LEFT, SET_CTRL_X, 72);
    lv_dropdown_set_options(s_dd_blank, "从不休眠\n30 秒\n1 分钟\n5 分钟");
    lv_dropdown_set_selected(s_dd_blank, 1);
    set_dd_font(s_dd_blank, SET_FONT_BODY);

    lv_obj_t* tip = lv_label_create(parent);
    lv_label_set_text(tip, "提示：休眠时间在重启后由运行时生效。");
    lv_obj_set_style_text_font(tip, SET_FONT_BODY, 0);
    lv_obj_set_style_text_color(tip, lv_color_hex(SET_CLR_SUB), 0);
    lv_obj_align(tip, LV_ALIGN_TOP_LEFT, SET_PAD_X, 140);

    /* 应用按钮（底部居中） */
    set_ui_btn(parent, "应用", (w - 140) / 2, set_ui_btn_y(parent), 140, SET_CLR_OK, btn_apply_cb);

    return parent;
}
