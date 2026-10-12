/*
 * set_info.c — 固件信息展示模块（只读）
 *
 * 显示：固件版本、编译日期、硬件型号、设备序列号、HMI UI 版本。
 * 布局：5 行 44px 行距，正文 16px，提示 14px。
 */
#include "sys_settings.h"
#include "settings_ui.h"
#include "sys_cmd.h"
#include "hmi_runtime.h"
#include "screen_manager.h"
#include "hmi_ui_api.h"
#include <stdio.h>
#include <string.h>

static void info_row(lv_obj_t* parent, const char* name, const char* value, int y)
{
    lv_obj_t* l1 = lv_label_create(parent);
    lv_label_set_text(l1, name);
    lv_obj_set_style_text_font(l1, SET_FONT_TITLE, 0);
    lv_obj_set_style_text_color(l1, lv_color_hex(SET_CLR_SUB), 0);
    lv_obj_align(l1, LV_ALIGN_TOP_LEFT, SET_PAD_X, y);

    lv_obj_t* l2 = lv_label_create(parent);
    lv_label_set_text(l2, value);
    lv_obj_set_style_text_font(l2, SET_FONT_TITLE, 0);
    lv_obj_align(l2, LV_ALIGN_TOP_LEFT, 240, y);
}

lv_obj_t* set_info_create(lv_obj_t* parent)
{
    char buf[128];

    /* 固件版本 */
    snprintf(buf, sizeof(buf), "v%s", HMI_RUNTIME_VERSION_STR);
    info_row(parent, "固件版本", buf, 16);

    /* 编译日期 */
    snprintf(buf, sizeof(buf), "%s %s", __DATE__, __TIME__);
    info_row(parent, "编译日期", buf, 60);

    /* 硬件型号 */
    info_row(parent, "硬件型号", HMI_TARGET_ID, 104);

    /* 序列号（从 /proc/cpuinfo serial 读取，失败显示默认） */
    char serial[64] = "T113-00000001";
    sys_cmd_get(serial, sizeof(serial), "cat /proc/cpuinfo 2>/dev/null | sed -n 's/Serial\\s*: //p' | sed -n '1p'");
    if (strlen(serial) == 0) snprintf(serial, sizeof(serial), "T113-00000001");
    info_row(parent, "设备序列号", serial, 148);

    /* HMI UI 版本（来自 .so） */
    const hmi_ui_api_t* api = hmi_ui_get_current_api();
    if (api && api->build_id) {
        snprintf(buf, sizeof(buf), "%s", api->build_id);
    } else {
        snprintf(buf, sizeof(buf), "(未加载 UI)");
    }
    info_row(parent, "HMI UI 版本", buf, 192);

    /* 说明（只读） */
    lv_obj_t* tip = lv_label_create(parent);
    lv_label_set_text(tip, "以上信息为只读，由系统固件自动生成。");
    lv_obj_set_style_text_font(tip, SET_FONT_BODY, 0);
    lv_obj_set_style_text_color(tip, lv_color_hex(SET_CLR_SUB), 0);
    lv_obj_align(tip, LV_ALIGN_TOP_LEFT, SET_PAD_X, 238);

    return parent;
}
