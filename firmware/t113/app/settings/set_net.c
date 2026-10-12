/*
 * set_net.c — 网络设置模块
 *
 * - 连接方式：有线(eth0) / 无线(wlan0)
 * - IP 模式：DHCP(动态) / 静态
 * - 静态：IP / 掩码 / 网关 / DNS（textarea + 弹出键盘）
 * - 状态区显示当前连接 IP 与链接状态
 * 布局：正文 ≥14px，控件 16px，行距 46px 防重叠，宽度随内容区自适应。
 */
#include "sys_settings.h"
#include "settings_ui.h"
#include "sys_cmd.h"
#include "hmi_runtime.h"
#include <stdio.h>
#include <string.h>
#include <ctype.h>

static lv_obj_t* s_mode_dd = NULL;   /* 有线/无线 */
static lv_obj_t* s_dhcp_sw = NULL;   /* DHCP 开关 */
static lv_obj_t* s_ta_ip = NULL;
static lv_obj_t* s_ta_mask = NULL;
static lv_obj_t* s_ta_gw = NULL;
static lv_obj_t* s_ta_dns = NULL;
static lv_obj_t* s_status_lbl = NULL;
static lv_obj_t* s_kb = NULL;

/* ── 简单 IPv4 校验（xxx.xxx.xxx.xxx，数字+点） ── */
static int is_ipv4(const char* s)
{
    if (!s || !*s) return 0;
    int len = 0;
    for (const char* p = s; *p; p++) {
        if (isdigit((unsigned char)*p)) { len++; }
        else if (*p == '.') { len++; }
        else return 0;
    }
    return len > 0 && len <= 15;
}

/* ── 键盘管理：textarea 点击弹出，支持三种关闭方式 ──
 * 1) 键盘自带 CLOSE(✕)/OK 按钮（LV_EVENT_CANCEL/READY）
 * 2) 点击输入框外部任意区域关闭
 * 3) 物理键盘 Esc 键关闭
 * 键盘按钮使用 LVGL 内置 montserrat 字体（含 ✕/✓ 符号），不用中文
 * 字体，避免 CLOSE 按钮显示为方框。 */
static void kb_hide(void)
{
    if (s_kb) lv_obj_add_flag(s_kb, LV_OBJ_FLAG_HIDDEN);
}

/* 供框架在 tab 切换/关闭设置页时主动收起键盘 */
void set_net_kb_hide(void)
{
    kb_hide();
}

static void kb_event_cb(lv_event_t* e)
{
    lv_event_code_t code = lv_event_get_code(e);
    if (code == LV_EVENT_READY || code == LV_EVENT_CANCEL) {
        kb_hide();
    }
    else if (code == LV_EVENT_KEY) {
        /* 物理键盘 Esc 关闭 */
        uint32_t key = lv_indev_get_key(lv_indev_active());
        if (key == LV_KEY_ESC) kb_hide();
    }
}

/* 点击外部区域（不在键盘、不在任何 textarea 上）→ 隐藏键盘 */
static void kb_screen_click_cb(lv_event_t* e)
{
    if (!s_kb || lv_obj_has_flag(s_kb, LV_OBJ_FLAG_HIDDEN)) return;
    lv_obj_t* target = lv_event_get_target(e);
    if (target == s_kb) return;                       /* 点在键盘上 */
    lv_obj_t* cur = target;
    while (cur) {
        if (lv_obj_check_type(cur, &lv_textarea_class)) return;  /* 点在输入框上 */
        cur = lv_obj_get_parent(cur);
    }
    kb_hide();
}

static void ta_focus_cb(lv_event_t* e)
{
    lv_obj_t* ta = lv_event_get_target(e);
    if (!s_kb) {
        s_kb = lv_keyboard_create(lv_screen_active());
        lv_obj_set_size(s_kb, LV_PCT(100), 220);
        lv_obj_align(s_kb, LV_ALIGN_BOTTOM_MID, 0, 0);
        /* 键盘按钮用默认字体（montserrat），保证 ✕/✓ 符号可见 */
        lv_obj_set_style_text_font(s_kb, &lv_font_montserrat_14, LV_PART_MAIN);
        lv_obj_add_event_cb(s_kb, kb_event_cb, LV_EVENT_READY, NULL);
        lv_obj_add_event_cb(s_kb, kb_event_cb, LV_EVENT_CANCEL, NULL);
        lv_obj_add_event_cb(s_kb, kb_event_cb, LV_EVENT_KEY, NULL);
        /* 屏幕点击外部区域关闭 */
        lv_obj_add_event_cb(lv_screen_active(), kb_screen_click_cb, LV_EVENT_CLICKED, NULL);
    }
    lv_keyboard_set_textarea(s_kb, ta);
    lv_obj_clear_flag(s_kb, LV_OBJ_FLAG_HIDDEN);
}

static lv_obj_t* make_ta_row(lv_obj_t* parent, const char* name, int y, const char* init)
{
    lv_obj_t* lbl = lv_label_create(parent);
    lv_label_set_text(lbl, name);
    lv_obj_set_style_text_font(lbl, SET_FONT_BODY, 0);
    lv_obj_align(lbl, LV_ALIGN_TOP_LEFT, SET_PAD_X, y + 9);

    lv_obj_t* ta = lv_textarea_create(parent);
    lv_obj_set_size(ta, set_ui_w(parent) - SET_PAD_X - SET_CTRL_X, 40);
    lv_obj_align(ta, LV_ALIGN_TOP_LEFT, SET_CTRL_X, y);
    /* textarea 白底黑字：显式设黑字，避免继承内容区白色文本色 */
    lv_obj_set_style_text_color(ta, lv_color_black(), 0);
    lv_textarea_set_placeholder_text(ta, name);
    lv_textarea_set_one_line(ta, true);
    lv_textarea_set_text(ta, init ? init : "");
    set_ta_font(ta, g_font_cn_16);
    lv_obj_add_event_cb(ta, ta_focus_cb, LV_EVENT_FOCUSED, NULL);
    lv_obj_add_event_cb(ta, ta_focus_cb, LV_EVENT_CLICKED, NULL);
    /* 物理键盘按键事件路由到聚焦对象（textarea），而非键盘本体；
     * 在 textarea 上注册 LV_EVENT_KEY 才能捕获 Esc 关闭键盘 */
    lv_obj_add_event_cb(ta, kb_event_cb, LV_EVENT_KEY, NULL);
    return ta;
}

/* ── 刷新状态区 ── */
static void refresh_status(void)
{
    if (!s_status_lbl) return;
    char buf[512];
    char ip[64] = "-", state[64] = "-";
    sys_cmd_get(ip, sizeof(ip), "ifconfig eth0 2>/dev/null | sed -n '2p' | awk '{print $2}' | cut -d: -f2");
    sys_cmd_get(state, sizeof(state), "cat /sys/class/net/eth0/carrier 2>/dev/null");
    if (strcmp(state, "1") == 0) {
        snprintf(buf, sizeof(buf), "有线 eth0：IP %s  （已连接）", ip);
    } else {
        snprintf(buf, sizeof(buf), "有线 eth0：IP %s  （未连接）", ip);
    }
    lv_label_set_text(s_status_lbl, buf);
}

/* ── 应用 ── */
static void do_apply(void)
{
    char ip[64], mask[64], gw[64], dns[64];
    const char* iface = lv_dropdown_get_selected(s_mode_dd) == 0 ? "eth0" : "wlan0";
    bool dhcp = lv_obj_has_state(s_dhcp_sw, LV_STATE_CHECKED);

    if (dhcp) {
        char cmd[128];
        snprintf(cmd, sizeof(cmd), "ifconfig %s 0.0.0.0 2>/dev/null; udhcpc -i %s -b 2>/dev/null", iface, iface);
        sys_cmd_exec(cmd);
        lv_label_set_text(s_status_lbl, "已切换到 DHCP，正在获取地址…");
    } else {
        snprintf(ip, sizeof(ip), "%s", lv_textarea_get_text(s_ta_ip));
        snprintf(mask, sizeof(mask), "%s", lv_textarea_get_text(s_ta_mask));
        snprintf(gw, sizeof(gw), "%s", lv_textarea_get_text(s_ta_gw));
        snprintf(dns, sizeof(dns), "%s", lv_textarea_get_text(s_ta_dns));

        if (!is_ipv4(ip))   { lv_label_set_text(s_status_lbl, "错误：IP 地址格式不正确"); return; }
        if (!is_ipv4(mask)) { lv_label_set_text(s_status_lbl, "错误：子网掩码格式不正确"); return; }

        char cmd[256];
        snprintf(cmd, sizeof(cmd), "ifconfig %s %s netmask %s 2>&1", iface, ip, mask);
        sys_cmd_exec(cmd);
        if (is_ipv4(gw)) {
            sys_cmd_exec("route del default 2>/dev/null");
            snprintf(cmd, sizeof(cmd), "route add default gw %s 2>&1", gw);
            sys_cmd_exec(cmd);
        }
        if (is_ipv4(dns)) {
            snprintf(cmd, sizeof(cmd), "echo 'nameserver %s' > /etc/resolv.conf", dns);
            sys_cmd_exec(cmd);
        }
        lv_label_set_text(s_status_lbl, "静态 IP 已应用");
    }
    HMI_LOG("sys_settings: net apply iface=%s dhcp=%d", iface, dhcp);
    refresh_status();
}

static void btn_apply_cb(lv_event_t* e)
{
    (void)e;
    do_apply();
}

/* ── 模块创建 ── */
lv_obj_t* set_net_create(lv_obj_t* parent)
{
    /* 键盘对象挂在设置屏幕上（跨 tab 常驻），此处仅收起不重建，
     * 避免每次进入网络页重复创建导致对象泄漏。 */
    set_net_kb_hide();

    /* 内容区被 lv_obj_clean 清空后，下列指针已失效；必须置 NULL，
     * 防止回调（如屏幕级 CLICKED 触发的 kb_screen_click_cb）在重建前
     * 访问到已释放对象（UAF）。s_kb 常驻屏幕，单独保留。 */
    s_mode_dd  = NULL;
    s_dhcp_sw  = NULL;
    s_ta_ip    = NULL;
    s_ta_mask  = NULL;
    s_ta_gw    = NULL;
    s_ta_dns   = NULL;
    s_status_lbl = NULL;

    int w = set_ui_w(parent);

    /* 状态区 */
    s_status_lbl = lv_label_create(parent);
    lv_obj_set_style_text_font(s_status_lbl, SET_FONT_TITLE, 0);
    lv_obj_set_style_text_color(s_status_lbl, lv_color_hex(SET_CLR_HIL), 0);
    lv_obj_align(s_status_lbl, LV_ALIGN_TOP_LEFT, SET_PAD_X, SET_PAD_TB);
    refresh_status();

    /* 行：连接方式 + DHCP */
    set_ui_lbl(parent, "连接方式", SET_PAD_X, 40, SET_FONT_BODY, -1);
    s_mode_dd = lv_dropdown_create(parent);
    lv_obj_set_size(s_mode_dd, 180, 38);
    lv_obj_align(s_mode_dd, LV_ALIGN_TOP_LEFT, SET_CTRL_X, 32);
    lv_dropdown_set_options(s_mode_dd, "有线(eth0)\n无线(wlan0)");
    lv_dropdown_set_selected(s_mode_dd, 0);
    set_dd_font(s_mode_dd, SET_FONT_BODY);

    set_ui_lbl(parent, "IP 模式", 390, 40, SET_FONT_BODY, -1);
    s_dhcp_sw = lv_switch_create(parent);
    lv_obj_align(s_dhcp_sw, LV_ALIGN_TOP_LEFT, 480, 32);
    /* 默认检测：有网关则为静态，否则 DHCP */
    lv_obj_add_state(s_dhcp_sw, LV_STATE_CHECKED);   /* 默认 DHCP */

    set_ui_lbl(parent, "DHCP 动态", 540, 40, SET_FONT_BODY, -1);

    /* 静态参数 */
    s_ta_ip   = make_ta_row(parent, "IP 地址", 84,  "");
    s_ta_mask = make_ta_row(parent, "子网掩码", 130, "255.255.255.0");
    s_ta_gw   = make_ta_row(parent, "默认网关", 176, "");
    s_ta_dns  = make_ta_row(parent, "DNS 服务器", 222, "");

    /* 应用按钮（底部居中） */
    set_ui_btn(parent, "应用", (w - 140) / 2, set_ui_btn_y(parent), 140, SET_CLR_OK, btn_apply_cb);

    return parent;
}
