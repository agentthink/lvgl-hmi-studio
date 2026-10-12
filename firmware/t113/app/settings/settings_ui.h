/*
 * settings_ui.h — 系统设置 UI 公共样式/布局助手
 *
 * 统一字体层级（正文 ≥14px）、配色、间距与控件尺寸，保证 9 个设置页
 * 风格一致。坐标基于内容区（parent）相对坐标，宽度/高度取内容区实际
 * 尺寸（lv_obj_get_content_*），随屏幕分辨率自适应，避免固定像素溢出。
 */
#ifndef SETTINGS_UI_H
#define SETTINGS_UI_H

#include "lvgl.h"
#include "sys_settings.h"

/* ── 字体层级（正文 ≥14px，层级清晰） ── */
#define SET_FONT_BODY    g_font_cn_14   /* 正文/标签/提示 */
#define SET_FONT_TITLE   g_font_cn_16   /* 页标题/强调值 */
#define SET_FONT_BIG     g_font_cn_20   /* 顶部大标题 */

/* ── 配色 ── */
#define SET_CLR_ACC   0x2f6fdb   /* 主题蓝 */
#define SET_CLR_OK    0x2f7d3a   /* 应用/确定（绿） */
#define SET_CLR_WARN  0xd05252   /* 危险/关闭（红） */
#define SET_CLR_BTN   0x3a3f4a   /* 次要按钮 */
#define SET_CLR_SUB   0x8a8f9a   /* 次级文本 */
#define SET_CLR_HIL   0x9fd0ff   /* 高亮/状态文本 */

/* ── 布局间距/控件尺寸 ── */
#define SET_PAD_X    24          /* 内容区左右边距 */
#define SET_LBL_W    130         /* 左侧标签列宽 */
#define SET_CTRL_X   170         /* 控件起始 x（标签右侧） */
#define SET_ROW_H    48          /* 单行行高 */
#define SET_BTN_H    44          /* 按钮高度 */
#define SET_BTN_GAP  14          /* 底部按钮距内容区底边距 */

/* ── 框架固定尺寸（与 sys_settings.c 一致：顶栏 44 + tab 栏 92 = 136；
 *    内容区 pad：左右 12，上下 8） ── */
#define SET_FRAME_TOP   136
#define SET_PAD_LR      12
#define SET_PAD_TB      8

/* ── 内容区尺寸（随分辨率自适应） ──
 * 注意：lv_obj_align 的基准是父对象 coords（含 pad），不是 content 区。
 * 因此模块布局的 y 坐标基于 coords 高度（sh - SET_FRAME_TOP），
 * 顶部元素需从 pad_top(8) 之后开始放置，否则会被内容区裁剪。 */
static inline int set_ui_w(lv_obj_t* parent)
{
    (void)parent;
    int sw = lv_display_get_horizontal_resolution(lv_display_get_default());
    return sw - 2 * SET_PAD_LR;
}
static inline int set_ui_h(lv_obj_t* parent)
{
    (void)parent;
    int sh = lv_display_get_vertical_resolution(lv_display_get_default());
    return sh - SET_FRAME_TOP;   /* 内容区 coords 高度（含 pad） */
}

/* 底部操作按钮行 y 坐标（统一贴底，避免与内容重叠） */
static inline int set_ui_btn_y(lv_obj_t* parent)
{
    return set_ui_h(parent) - SET_BTN_H - SET_BTN_GAP;
}

/* 标签工厂：font 为 NULL 用正文 14px；color_hex<0 用默认文本色 */
static inline lv_obj_t* set_ui_lbl(lv_obj_t* parent, const char* text,
                                   int x, int y, lv_font_t* font, int color_hex)
{
    lv_obj_t* l = lv_label_create(parent);
    lv_label_set_text(l, text);
    lv_obj_set_style_text_font(l, font ? font : SET_FONT_BODY, 0);
    if (color_hex >= 0) lv_obj_set_style_text_color(l, lv_color_hex((uint32_t)color_hex), 0);
    lv_obj_align(l, LV_ALIGN_TOP_LEFT, x, y);
    return l;
}

/* 按钮工厂：文本已居中（16px），统一高度 */
static inline lv_obj_t* set_ui_btn(lv_obj_t* parent, const char* text,
                                   int x, int y, int w,
                                   uint32_t color_hex, lv_event_cb_t cb)
{
    lv_obj_t* b = lv_button_create(parent);
    lv_obj_set_size(b, w, SET_BTN_H);
    lv_obj_align(b, LV_ALIGN_TOP_LEFT, x, y);
    lv_obj_set_style_bg_color(b, lv_color_hex(color_hex), 0);
    lv_obj_add_event_cb(b, cb, LV_EVENT_CLICKED, NULL);
    lv_obj_t* l = lv_label_create(b);
    lv_label_set_text(l, text);
    lv_obj_set_style_text_font(l, SET_FONT_TITLE, 0);
    lv_obj_set_align(l, LV_ALIGN_CENTER);   /* 持续居中（布局时按按钮尺寸重算） */
    return b;
}

/* dropdown 字体（含展开列表，保证中文选项完整显示） */
static inline void set_dd_font(lv_obj_t* dd, lv_font_t* font)
{
    lv_obj_set_style_text_font(dd, font, LV_PART_MAIN);
    /* 右侧箭头：默认 LV_SYMBOL_DOWN(U+F078, FontAwesome) 不在 CJK 字体中会显示方框。
     * 改用 Unicode 三角 ▼(U+25BC)，已包含在 Noto 子集字体中，任意字号均可渲染。 */
    lv_dropdown_set_symbol(dd, "\u25BC");
    lv_obj_set_style_text_font(dd, font, LV_PART_INDICATOR);
    lv_obj_set_style_text_color(dd, lv_color_hex(SET_CLR_SUB), LV_PART_INDICATOR);
    lv_obj_t* list = lv_dropdown_get_list(dd);
    if (list) {
        lv_obj_set_style_text_font(list, font, LV_PART_MAIN);
        lv_obj_set_style_text_font(list, font, LV_PART_SELECTED);
    }
}

/* textarea 字体（含占位符，保证中文占位不显示方框） */
static inline void set_ta_font(lv_obj_t* ta, lv_font_t* font)
{
    lv_obj_set_style_text_font(ta, font, LV_PART_MAIN);
    lv_obj_set_style_text_font(ta, font, LV_PART_TEXTAREA_PLACEHOLDER);
}

#endif /* SETTINGS_UI_H */
