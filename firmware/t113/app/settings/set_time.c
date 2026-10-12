/*
 * set_time.c — 系统时间设置模块
 *
 * 6 个字段（年/月/日/时/分/秒）用 lv_spinbox 调整，支持 + / - 按钮。
 * 保存：date -s "YYYY-MM-DD HH:MM:SS" + hwclock -w（写入 RTC）
 * 取消：重新读取系统时间刷新界面
 * 布局：三列响应式分布，正文 16px，底部统一操作栏。
 */
#include "sys_settings.h"
#include "settings_ui.h"
#include "sys_cmd.h"
#include "hmi_runtime.h"
#include <time.h>
#include <stdio.h>
#include <string.h>

typedef struct {
    lv_obj_t* spin;
    int min, max;
    int digits;
} time_field_t;

static time_field_t s_f[6];   /* 0年 1月 2日 3时 4分 5秒 */
static lv_obj_t* s_cur_lbl = NULL;

#define FIELD_YEAR  0
#define FIELD_MON   1
#define FIELD_DAY   2
#define FIELD_HOUR  3
#define FIELD_MIN   4
#define FIELD_SEC   5

/* ── 字段加减按钮回调（user_data：减=idx，加=idx+10） ── */
static void btn_adj_cb(lv_event_t* e)
{
    int raw = (int)(intptr_t)lv_event_get_user_data(e);
    bool up = raw >= 10;
    int idx = raw % 10;
    if (idx < 0 || idx >= 6 || !s_f[idx].spin) return;
    if (up) lv_spinbox_increment(s_f[idx].spin);
    else    lv_spinbox_decrement(s_f[idx].spin);
}

/* 创建单个字段：label 在上，[- spin +] 在下方 */
static void make_field(lv_obj_t* parent, const char* name,
                       int x, int y, int min, int max, int value, int digits, int idx)
{
    lv_obj_t* lbl = lv_label_create(parent);
    lv_label_set_text(lbl, name);
    lv_obj_set_style_text_font(lbl, SET_FONT_TITLE, 0);
    lv_obj_align(lbl, LV_ALIGN_TOP_LEFT, x, y);

    lv_obj_t* bm = lv_button_create(parent);
    lv_obj_set_size(bm, 36, 40);
    lv_obj_align(bm, LV_ALIGN_TOP_LEFT, x, y + 26);
    lv_obj_set_style_bg_color(bm, lv_color_hex(SET_CLR_BTN), 0);
    lv_obj_add_event_cb(bm, btn_adj_cb, LV_EVENT_CLICKED, (void*)(intptr_t)idx);
    lv_obj_t* lm = lv_label_create(bm);
    lv_label_set_text(lm, "-");
    lv_obj_set_style_text_font(lm, SET_FONT_TITLE, 0);
    lv_obj_set_align(lm, LV_ALIGN_CENTER);

    lv_obj_t* sp = lv_spinbox_create(parent);
    lv_obj_set_size(sp, 128, 40);
    lv_obj_align(sp, LV_ALIGN_TOP_LEFT, x + 38, y + 26);
    lv_obj_set_style_text_font(sp, SET_FONT_TITLE, 0);
    /* spinbox 白底黑字：显式设黑字，避免继承内容区白色文本色 */
    lv_obj_set_style_text_color(sp, lv_color_black(), 0);
    lv_spinbox_set_range(sp, min, max);
    lv_spinbox_set_digit_format(sp, digits, 0);
    lv_spinbox_set_step(sp, 1);
    lv_spinbox_set_value(sp, value);

    lv_obj_t* bp = lv_button_create(parent);
    lv_obj_set_size(bp, 36, 40);
    lv_obj_align(bp, LV_ALIGN_TOP_LEFT, x + 168, y + 26);
    lv_obj_set_style_bg_color(bp, lv_color_hex(SET_CLR_BTN), 0);
    lv_obj_add_event_cb(bp, btn_adj_cb, LV_EVENT_CLICKED, (void*)(intptr_t)(idx + 10));
    lv_obj_t* lp = lv_label_create(bp);
    lv_label_set_text(lp, "+");
    lv_obj_set_style_text_font(lp, SET_FONT_TITLE, 0);
    lv_obj_set_align(lp, LV_ALIGN_CENTER);

    s_f[idx].spin = sp;
    s_f[idx].min = min;
    s_f[idx].max = max;
    s_f[idx].digits = digits;
}

/* 读取系统时间填充字段 */
static void refresh_from_system(void)
{
    time_t now = time(NULL);
    struct tm t;
    localtime_r(&now, &t);
    if (s_f[FIELD_YEAR].spin) lv_spinbox_set_value(s_f[FIELD_YEAR].spin, t.tm_year + 1900);
    if (s_f[FIELD_MON].spin)  lv_spinbox_set_value(s_f[FIELD_MON].spin, t.tm_mon + 1);
    if (s_f[FIELD_DAY].spin)  lv_spinbox_set_value(s_f[FIELD_DAY].spin, t.tm_mday);
    if (s_f[FIELD_HOUR].spin) lv_spinbox_set_value(s_f[FIELD_HOUR].spin, t.tm_hour);
    if (s_f[FIELD_MIN].spin)  lv_spinbox_set_value(s_f[FIELD_MIN].spin, t.tm_min);
    if (s_f[FIELD_SEC].spin)  lv_spinbox_set_value(s_f[FIELD_SEC].spin, t.tm_sec);
}

static void update_cur_lbl(void)
{
    if (!s_cur_lbl) return;
    time_t now = time(NULL);
    struct tm t;
    localtime_r(&now, &t);
    char buf[64];
    snprintf(buf, sizeof(buf), "当前时间：%04d-%02d-%02d %02d:%02d:%02d",
             t.tm_year + 1900, t.tm_mon + 1, t.tm_mday,
             t.tm_hour, t.tm_min, t.tm_sec);
    lv_label_set_text(s_cur_lbl, buf);
}

/* ── 保存：确认弹窗 ── */
static void do_save(void)
{
    int y = (int)lv_spinbox_get_value(s_f[FIELD_YEAR].spin);
    int mo = (int)lv_spinbox_get_value(s_f[FIELD_MON].spin);
    int d = (int)lv_spinbox_get_value(s_f[FIELD_DAY].spin);
    int h = (int)lv_spinbox_get_value(s_f[FIELD_HOUR].spin);
    int mi = (int)lv_spinbox_get_value(s_f[FIELD_MIN].spin);
    int se = (int)lv_spinbox_get_value(s_f[FIELD_SEC].spin);

    /* 基本校验 */
    if (mo < 1 || mo > 12 || d < 1 || d > 31 || h < 0 || h > 23 ||
        mi < 0 || mi > 59 || se < 0 || se > 59 || y < 2020 || y > 2099) {
        lv_label_set_text(s_cur_lbl, "错误：日期/时间超出有效范围！");
        return;
    }
    char cmd[160];
    snprintf(cmd, sizeof(cmd), "date -s \"%04d-%02d-%02d %02d:%02d:%02d\"",
             y, mo, d, h, mi, se);
    int rc = sys_cmd_exec(cmd);
    if (rc != 0) {
        lv_label_set_text(s_cur_lbl, "保存失败（date 命令执行出错）");
        return;
    }
    /* 写入 RTC：-u 表示 RTC 存 UTC（与 /etc/init.d/sysfixtime 的
     * stop 保存命令一致，避免时区差导致重启后时间偏移 8 小时） */
    sys_cmd_exec("hwclock -w -u -f /dev/rtc0 2>/dev/null");
    update_cur_lbl();
    HMI_LOG("sys_settings: time saved %04d-%02d-%02d %02d:%02d:%02d", y, mo, d, h, mi, se);
}

static void btn_save_cb(lv_event_t* e)
{
    (void)e;
    do_save();
}

static void btn_cancel_cb(lv_event_t* e)
{
    (void)e;
    refresh_from_system();
    update_cur_lbl();
}

/* ── 模块创建 ── */
lv_obj_t* set_time_create(lv_obj_t* parent)
{
    memset(s_f, 0, sizeof(s_f));
    s_cur_lbl = NULL;

    /* 当前时间标签 */
    s_cur_lbl = lv_label_create(parent);
    lv_obj_set_style_text_font(s_cur_lbl, SET_FONT_TITLE, 0);
    lv_obj_set_style_text_color(s_cur_lbl, lv_color_hex(SET_CLR_HIL), 0);
    lv_obj_align(s_cur_lbl, LV_ALIGN_TOP_MID, 0, SET_PAD_TB);
    update_cur_lbl();

    /* 读取当前系统时间 */
    time_t now = time(NULL);
    struct tm t;
    localtime_r(&now, &t);
    int vals[6] = { t.tm_year + 1900, t.tm_mon + 1, t.tm_mday,
                    t.tm_hour, t.tm_min, t.tm_sec };
    int mins[6]  = { 2020, 1, 1, 0, 0, 0 };
    int maxs[6]  = { 2099, 12, 31, 23, 59, 59 };
    int digs[6]  = { 4, 2, 2, 2, 2, 2 };

    const char* names[6] = { "年", "月", "日", "时", "分", "秒" };
    /* 三列响应式分布：列间距随内容区宽度自适应 */
    int w = set_ui_w(parent);
    const int field_w = 204;                 /* [- spin +] 组宽 */
    int col_gap = (w - 2 * SET_PAD_X - 3 * field_w) / 2;
    if (col_gap < 12) col_gap = 12;
    int xs[6] = {
        SET_PAD_X, SET_PAD_X + field_w + col_gap, SET_PAD_X + 2 * (field_w + col_gap),
        SET_PAD_X, SET_PAD_X + field_w + col_gap, SET_PAD_X + 2 * (field_w + col_gap)
    };
    int ys[6] = { 44, 44, 44, 140, 140, 140 };

    for (int i = 0; i < 6; i++) {
        make_field(parent, names[i], xs[i], ys[i], mins[i], maxs[i], vals[i], digs[i], i);
    }

    /* 底部操作栏（居中） */
    int btn_y = set_ui_btn_y(parent);
    int bx = (w - 300) / 2;
    set_ui_btn(parent, "保存", bx, btn_y, 140, SET_CLR_OK, btn_save_cb);
    set_ui_btn(parent, "取消", bx + 160, btn_y, 140, SET_CLR_BTN, btn_cancel_cb);

    return parent;
}
