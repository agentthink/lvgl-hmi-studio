/*
 * set_maint.c — 系统维护模块
 *
 * - 重启设备
 * - 恢复出厂设置（清 UDISK 配置 + HMI 程序，回退出厂 .so）
 * - OTA 升级入口（调用 /usr/bin/hmi_ota.sh）
 * 所有操作均带确认弹窗。布局：按钮行距 56px，提示 14px。
 */
#include "sys_settings.h"
#include "settings_ui.h"
#include "sys_cmd.h"
#include "hmi_runtime.h"
#include <stdio.h>
#include <string.h>

static lv_obj_t* s_lbl = NULL;

static void reboot_device(void)
{
    HMI_LOG("sys_settings: reboot device");
    sys_cmd_exec("sync");
    sys_cmd_exec("reboot");
}

static void factory_reset(void)
{
    HMI_LOG("sys_settings: factory reset");
    /* 清除 UDISK 配置与 HMI 程序（回退出厂 .so） */
    sys_cmd_exec("rm -rf /mnt/UDISK/hmi 2>/dev/null");
    sys_cmd_exec("rm -f /etc/hmi/serial.conf /etc/hmi/display.conf /etc/hmi/touch_cal.conf 2>/dev/null");
    sys_cmd_exec("sync");
    sys_cmd_exec("reboot");
}

static void ota_update(void)
{
    HMI_LOG("sys_settings: OTA update via hmi_ota.sh");
    if (sys_cmd_exec("test -x /usr/bin/hmi_ota.sh") != 0) {
        lv_label_set_text(s_lbl, "未找到升级脚本 hmi_ota.sh");
        return;
    }
    lv_label_set_text(s_lbl, "正在执行升级脚本（TF/USB/网络）…");
    /* 后台执行，避免阻塞 UI */
    sys_cmd_exec("/usr/bin/hmi_ota.sh > /tmp/hmi_ota.log 2>&1 &");
}

/* 确认弹窗：footer 按钮各自注册回调（v9 msgbox 无 get_active_btn） */
#define DLG_CANCEL_FLAG 0x80000000
static void dlg_btn_cb(lv_event_t* e)
{
    lv_obj_t* btn = lv_event_get_target(e);
    int raw = (int)(intptr_t)lv_event_get_user_data(e);
    int action = raw & 0x0fffffff;
    int is_ok = !(raw & DLG_CANCEL_FLAG);
    /* 找到 msgbox：btn -> footer -> mbox */
    lv_obj_t* footer = lv_obj_get_parent(btn);
    lv_obj_t* mbox = footer ? lv_obj_get_parent(footer) : NULL;
    if (is_ok) {
        if (action == 0) reboot_device();
        else if (action == 1) factory_reset();
        else if (action == 2) ota_update();
    }
    /* 在回调内 close msgbox 是安全的：LVGL 9.5 的 lv_msgbox_close 内部
     * 使用 lv_obj_delete_async 延迟到事件派发结束后删除，无 use-after-free。 */
    if (mbox) lv_msgbox_close(mbox);
}

static void confirm_do(lv_obj_t* parent, const char* title, const char* text, int action)
{
    lv_obj_t* mbox = lv_msgbox_create(lv_screen_active());
    lv_msgbox_add_title(mbox, title);
    lv_msgbox_add_text(mbox, text);
    lv_obj_t* b_ok = lv_msgbox_add_footer_button(mbox, "确定");
    lv_obj_t* b_no = lv_msgbox_add_footer_button(mbox, "取消");
    lv_obj_add_event_cb(b_ok, dlg_btn_cb, LV_EVENT_CLICKED, (void*)(intptr_t)(action));
    lv_obj_add_event_cb(b_no, dlg_btn_cb, LV_EVENT_CLICKED, (void*)(intptr_t)(action | DLG_CANCEL_FLAG));
    (void)parent;
}

static void btn_reboot_cb(lv_event_t* e) { (void)e;
    confirm_do(NULL, "重启设备", "确定立即重启设备？", 0); }
static void btn_factory_cb(lv_event_t* e) { (void)e;
    confirm_do(NULL, "恢复出厂设置", "将清除 UDISK 配置与 HMI 程序\n并回退出厂状态，确定继续？", 1); }
static void btn_ota_cb(lv_event_t* e) { (void)e;
    confirm_do(NULL, "系统升级", "将执行升级脚本\n（TF 卡/USB/网络自动检测），确定？", 2); }

lv_obj_t* set_maint_create(lv_obj_t* parent)
{
    s_lbl = NULL;   /* 内容区被 lv_obj_clean 清空后旧指针失效 */

    s_lbl = lv_label_create(parent);
    lv_obj_set_style_text_font(s_lbl, SET_FONT_TITLE, 0);
    lv_obj_set_style_text_color(s_lbl, lv_color_hex(SET_CLR_HIL), 0);
    lv_obj_align(s_lbl, LV_ALIGN_TOP_LEFT, SET_PAD_X, SET_PAD_TB);
    lv_label_set_text(s_lbl, "系统维护");

    set_ui_btn(parent, "重启设备", SET_PAD_X, 44, 260, SET_CLR_BTN, btn_reboot_cb);
    set_ui_btn(parent, "恢复出厂设置", SET_PAD_X, 100, 260, SET_CLR_WARN, btn_factory_cb);
    set_ui_btn(parent, "系统升级（OTA）", SET_PAD_X, 156, 260, SET_CLR_ACC, btn_ota_cb);

    lv_obj_t* tip = lv_label_create(parent);
    lv_label_set_text(tip, "升级说明：请先将升级包放入 TF 卡根目录或 U 盘，\n或确保板卡已联网；脚本将自动检测并执行。");
    lv_obj_set_style_text_font(tip, SET_FONT_BODY, 0);
    lv_obj_set_style_text_color(tip, lv_color_hex(SET_CLR_SUB), 0);
    lv_obj_align(tip, LV_ALIGN_TOP_LEFT, SET_PAD_X, 224);

    return parent;
}
