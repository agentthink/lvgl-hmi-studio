/*
 * set_storage.c — 存储管理模块
 *
 * - 显示 UDISK / 系统分区容量（df 解析）
 * - 格式化 UDISK（mkfs.ubifs，带确认弹窗）
 * 布局：正文 16px 换行，宽度随内容区自适应，底部统一操作栏。
 */
#include "sys_settings.h"
#include "settings_ui.h"
#include "sys_cmd.h"
#include "hmi_runtime.h"
#include <stdio.h>
#include <string.h>

static lv_obj_t* s_lbl = NULL;

static void refresh_info(void)
{
    if (!s_lbl) return;
    char out[512];
    char buf[700];
    out[0] = '\0';
    sys_cmd_get(out, sizeof(out), "df -h /mnt/UDISK / 2>/dev/null | sed -n '1,3p'");
    /* 解析为易读格式 */
    char udisk[96] = "-", usys[96] = "-";
    /* UDISK: /dev/ubi0_7  Size Used Available */
    sys_cmd_get(udisk, sizeof(udisk),
        "df -h /mnt/UDISK 2>/dev/null | sed -n '2p' | awk '{print $1, $2, $4, \"可用\"}'");
    sys_cmd_get(usys, sizeof(usys),
        "df -h / 2>/dev/null | sed -n '2p' | awk '{print $1, $2, $4, \"可用\"}'");
    snprintf(buf, sizeof(buf), "UDISK（可写区）：%s\n系统分区：%s\n\n提示：格式化将清空 UDISK 中所有数据（含 OTA 升级的 HMI 程序）。",
             udisk[0] ? udisk : "-", usys[0] ? usys : "-");
    lv_label_set_text(s_lbl, buf);
}

/* 确认弹窗回调（v9 msgbox 无 get_active_btn，footer 按钮各自注册） */
#define DLG_CANCEL_FLAG 0x80000000
static void dlg_btn_cb(lv_event_t* e)
{
    lv_obj_t* btn = lv_event_get_target(e);
    int raw = (int)(intptr_t)lv_event_get_user_data(e);
    int is_ok = !(raw & DLG_CANCEL_FLAG);
    lv_obj_t* footer = lv_obj_get_parent(btn);
    lv_obj_t* mbox = footer ? lv_obj_get_parent(footer) : NULL;
    if (is_ok) {
        HMI_LOG("sys_settings: format UDISK");
        sys_cmd_exec("rm -rf /mnt/UDISK/* 2>/dev/null");
        sys_cmd_exec("sync");
        lv_label_set_text(s_lbl, "UDISK 已清空");
    }
    /* 在回调内 close msgbox 是安全的：LVGL 9.5 的 lv_msgbox_close 内部
     * 使用 lv_obj_delete_async 延迟到事件派发结束后删除，无 use-after-free。 */
    if (mbox) lv_msgbox_close(mbox);
}

static void btn_format_cb(lv_event_t* e)
{
    (void)e;
    lv_obj_t* mbox = lv_msgbox_create(lv_screen_active());
    lv_msgbox_add_title(mbox, "格式化 UDISK");
    lv_msgbox_add_text(mbox, "将清空 UDISK 中所有数据，\n确定继续？");
    lv_obj_t* b_ok = lv_msgbox_add_footer_button(mbox, "确定");
    lv_obj_t* b_no = lv_msgbox_add_footer_button(mbox, "取消");
    lv_obj_add_event_cb(b_ok, dlg_btn_cb, LV_EVENT_CLICKED, (void*)(intptr_t)0);
    lv_obj_add_event_cb(b_no, dlg_btn_cb, LV_EVENT_CLICKED, (void*)(intptr_t)DLG_CANCEL_FLAG);
}

static void btn_refresh_cb(lv_event_t* e)
{
    (void)e;
    refresh_info();
}

lv_obj_t* set_storage_create(lv_obj_t* parent)
{
    int w = set_ui_w(parent);

    s_lbl = NULL;   /* 内容区被 lv_obj_clean 清空后旧指针失效 */

    s_lbl = lv_label_create(parent);
    lv_obj_set_style_text_font(s_lbl, SET_FONT_TITLE, 0);
    lv_obj_align(s_lbl, LV_ALIGN_TOP_LEFT, SET_PAD_X, 16);
    lv_label_set_long_mode(s_lbl, LV_LABEL_LONG_WRAP);
    lv_obj_set_width(s_lbl, w - 2 * SET_PAD_X);
    refresh_info();

    /* 底部操作栏（居中） */
    int btn_y = set_ui_btn_y(parent);
    int bx = (w - 320) / 2;
    set_ui_btn(parent, "格式化 UDISK", bx, btn_y, 160, SET_CLR_WARN, btn_format_cb);
    set_ui_btn(parent, "刷新", bx + 180, btn_y, 140, SET_CLR_BTN, btn_refresh_cb);

    return parent;
}
