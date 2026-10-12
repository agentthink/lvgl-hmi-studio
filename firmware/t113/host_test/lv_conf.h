/**
 * lv_conf.h — LVGL 9.5 配置（T113-S3 目标）
 *
 * 自 v8.3 配置迁移：
 *  - LV_MEM_CUSTOM → LV_USE_STDLIB_MALLOC（LV_STDLIB_BUILTIN + LV_MEM_SIZE）
 *  - LV_DISP_DEF_REFR_PERIOD → LV_DEF_REFR_PERIOD
 *  - LV_DRAW_COMPLEX → LV_DRAW_SW_COMPLEX（默认 1）
 *  - LV_LAYER_SIMPLE_BUF_SIZE → LV_DRAW_LAYER_SIMPLE_BUF_SIZE
 *  - LV_USE_PNG → LV_USE_LIBPNG（libpng）
 *  - LV_USE_BTN/BTNMATRIX/IMG/IMGBTN → LV_USE_BUTTON/BUTTONMATRIX/IMAGE/IMAGEBUTTON
 *  - LV_TICK_CUSTOM 移除 → main.c 调用 lv_tick_set_cb(custom_tick_get)
 *  - LV_SPRINTF_USE_FLOAT 移除 → LV_USE_FLOAT
 *
 * 与 AllwinnerT113Target.cs 对齐：LV_COLOR_DEPTH 32、LV_MEM_SIZE 4MB、
 * LV_USE_FREETYPE、LV_USE_LIBPNG（SDK 已启用 libfreetype 2.9.1 / libpng）。
 */
#ifndef LV_CONF_H
#define LV_CONF_H

#include <stdint.h>

/*====================
   COLOR SETTINGS
 *====================*/
#define LV_COLOR_DEPTH 32

/*=========================
   STDLIB & MEMORY SETTINGS
 *=========================*/
#define LV_USE_STDLIB_MALLOC LV_STDLIB_BUILTIN
#define LV_USE_STDLIB_STRING LV_STDLIB_BUILTIN
#define LV_USE_STDLIB_SPRINTF LV_STDLIB_BUILTIN

/* 4MB LVGL 堆（128MB DDR3）；PC 端测试加大避免 freetype 缓存耗尽 */
#define LV_MEM_SIZE (16U * 1024U * 1024U)
#define LV_MEM_POOL_EXPAND_SIZE 0

/*====================
   HAL SETTINGS
 *====================*/
#define LV_DEF_REFR_PERIOD 16          /* ~60fps */
#define LV_DPI_DEF 130
#define LV_USE_OS LV_OS_NONE           /* 单线程主循环 */

/*=======================
 * RENDERING CONFIGURATION
 *=======================*/
#define LV_DRAW_BUF_STRIDE_ALIGN 1     /* flush 时行缓冲无 stride 填充 */
#define LV_DRAW_SW_COMPLEX 1
#define LV_DRAW_SW_CIRCLE_CACHE_SIZE 4
#define LV_DRAW_LAYER_SIMPLE_BUF_SIZE (24 * 1024)

/*============
 * LOGGING
 *============*/
#define LV_USE_LOG 1
#if LV_USE_LOG
    #define LV_LOG_LEVEL LV_LOG_LEVEL_WARN
    #define LV_LOG_PRINTF 1
#endif

/*===========
 * ASSERTS
 *===========*/
#define LV_USE_ASSERT_NULL 1
#define LV_USE_ASSERT_MALLOC 1
#define LV_USE_ASSERT_STYLE 0
#define LV_USE_ASSERT_MEM_INTEGRITY 0
#define LV_USE_ASSERT_OBJ 0

/*===========
 * OTHERS
 *===========*/
#define LV_USE_FLOAT 1                 /* lv_value_precise_t 使用 float */
#define LV_USE_MATRIX 0
#define LV_USE_PERF_MONITOR 0
#define LV_USE_MEM_MONITOR 0
#define LV_USE_SYSMON 0

/*==================
 *   FONT USAGE
 *===================*/
#define LV_FONT_MONTSERRAT_8  0
#define LV_FONT_MONTSERRAT_10 0
#define LV_FONT_MONTSERRAT_12 1
#define LV_FONT_MONTSERRAT_14 1
#define LV_FONT_MONTSERRAT_16 1
#define LV_FONT_MONTSERRAT_18 0
#define LV_FONT_MONTSERRAT_20 1
#define LV_FONT_MONTSERRAT_22 0
#define LV_FONT_MONTSERRAT_24 0
#define LV_FONT_MONTSERRAT_26 0
#define LV_FONT_MONTSERRAT_28 0
#define LV_FONT_MONTSERRAT_30 0
#define LV_FONT_MONTSERRAT_32 0
#define LV_FONT_MONTSERRAT_34 0
#define LV_FONT_MONTSERRAT_36 0
#define LV_FONT_MONTSERRAT_38 0
#define LV_FONT_MONTSERRAT_40 0
#define LV_FONT_MONTSERRAT_42 0
#define LV_FONT_MONTSERRAT_44 0
#define LV_FONT_MONTSERRAT_46 0
#define LV_FONT_MONTSERRAT_48 0
#define LV_FONT_MONTSERRAT_28_COMPRESSED 0
#define LV_FONT_DEJAVU_16_PERSIAN_HEBREW 0
#define LV_FONT_UNSCII_8  0
#define LV_FONT_UNSCII_16 0
/* PC 测试用 LVGL 内置思源黑体 CJK 位图字体（freetype 26.x 与 LVGL 9.5
 * 集成渲染 CJK 异常；板子固件用 freetype fangsong.ttf，不受影响） */
#define LV_FONT_SOURCE_HAN_SANS_SC_14_CJK 1
#define LV_FONT_SOURCE_HAN_SANS_SC_16_CJK 1
#define LV_FONT_CUSTOM_DECLARE
#define LV_FONT_DEFAULT &lv_font_montserrat_14
#define LV_FONT_FMT_TXT_LARGE 0
#define LV_USE_FONT_COMPRESSED 0
#define LV_USE_FONT_PLACEHOLDER 1

/*=================
 *  TEXT SETTINGS
 *=================*/
#define LV_TXT_ENC LV_TXT_ENC_UTF8
#define LV_TXT_BREAK_CHARS " ,.;:-_)]}"
#define LV_TXT_LINE_BREAK_LONG_LEN 0
#define LV_TXT_LINE_BREAK_LONG_PRE_MIN_LEN 3
#define LV_TXT_LINE_BREAK_LONG_POST_MIN_LEN 3
#define LV_TXT_COLOR_CMD "#"
#define LV_USE_BIDI 0
#define LV_USE_ARABIC_PERSIAN_CHARS 0

/*==================
 *  WIDGETS（编辑器生成代码覆盖：默认全开，除不需要的动画/桌面控件）
 *================*/
#define LV_WIDGETS_HAS_DEFAULT_VALUE 1
#define LV_USE_ANIMIMG    1
#define LV_USE_ARC        1
#define LV_USE_ARCLABEL   1
#define LV_USE_BAR        1
#define LV_USE_BUTTON     1
#define LV_USE_BUTTONMATRIX 1
#define LV_USE_CALENDAR   1
#define LV_USE_CANVAS     1
#define LV_USE_CHART      1
#define LV_USE_CHECKBOX   1
#define LV_USE_DROPDOWN   1
#define LV_USE_IMAGE      1
#define LV_USE_IMAGEBUTTON 1
#define LV_USE_KEYBOARD   1
#define LV_USE_LABEL      1
#define LV_USE_LED        1
#define LV_USE_LINE       1
#define LV_USE_LIST       1
#define LV_USE_LOTTIE     0
#define LV_USE_MENU       1
#define LV_USE_MSGBOX     1
#define LV_USE_ROLLER     1
#define LV_USE_SCALE      1
#define LV_USE_SLIDER     1
#define LV_USE_SPAN       1
#define LV_USE_SPINBOX    1
#define LV_USE_SPINNER    1
#define LV_USE_SWITCH     1
#define LV_USE_TABLE      1
#define LV_USE_TABVIEW    1
#define LV_USE_TEXTAREA   1
#define LV_USE_TILEVIEW   1
#define LV_USE_WIN        1

/*-----------
 * Themes
 *----------*/
#define LV_USE_THEME_DEFAULT 1
#define LV_USE_THEME_SIMPLE 1
#define LV_USE_THEME_MONO 1

/*-----------
 * Layouts
 *----------*/
#define LV_USE_FLEX 1
#define LV_USE_GRID 1

/*---------------------
 * 3rd party libraries
 *--------------------*/
/* 图片解码：SDK 内置 libpng 为旧版（无 png_image 简化 API），无法编译 LVGL v9 的
 * lv_libpng.c，改用 LVGL 内置 lodepng（纯 C、无外部依赖）提供 PNG 解码。 */
#define LV_USE_LIBPNG 0
#define LV_USE_LODEPNG 1
#define LV_USE_BMP 1
#define LV_USE_TJPGD 0
#define LV_USE_LIBJPEG_TURBO 0
#define LV_USE_LIBWEBP 0
#define LV_USE_GIF 0
#define LV_USE_QRCODE 0
#define LV_USE_BARCODE 0
#define LV_USE_RLOTTIE 0
#define LV_USE_FFMPEG 0
#define LV_USE_TINY_TTF 0
#define LV_USE_GLTF 0
#define LV_USE_GSTREAMER 0

/* FreeType（SDK 已启用 libfreetype 2.9.1） */
#define LV_USE_FREETYPE 1
#if LV_USE_FREETYPE
    #define LV_FREETYPE_CACHE_FT_GLYPH_CNT 256
    #define LV_FREETYPE_CACHE_FT_FACES 4
    #define LV_FREETYPE_CACHE_FT_SIZES 4
#endif

/* 矢量图形（SVG 需 LV_USE_VECTOR_GRAPHIC + ThorVG，编辑器 Svg 控件按需开启） */
#define LV_USE_VECTOR_GRAPHIC 0
#define LV_USE_THORVG_INTERNAL 0

/*------------------
 * DEVICE DRIVERS（全部使用自研驱动，禁用 SDK 内置）
 *------------------*/
#define LV_USE_EVDEV 0
#define LV_USE_LIBINPUT 0
#define LV_USE_LINUX_FBDEV 0
#define LV_USE_LINUX_DRM 0
#define LV_USE_X11 0
#define LV_USE_WAYLAND 0
#define LV_USE_SDL 0
#define LV_USE_OPENGLES 0
#define LV_USE_GLFW 0
#define LV_USE_QNX 0

/*============
 * OTHERS
 *============*/
#define LV_USE_SNAPSHOT 0
#define LV_USE_MONKEY 0
#define LV_USE_GRIDNAV 0
#define LV_USE_FRAGMENT 0
#define LV_USE_IMGFONT 0
#define LV_USE_OBSERVER 1
#define LV_USE_MSG 0
#define LV_USE_IME_PINYIN 0
#define LV_USE_COLOR_FILTER 0

/*==================
* BUILD OPTIONS
*==================*/
#define LV_BUILD_EXAMPLES 0
#define LV_BUILD_DEMOS 0

#endif /*LV_CONF_H*/
