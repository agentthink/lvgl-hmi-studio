/*
 * sys_settings.c — 系统设置主框架（tab 导航 + 入口检测 + 屏幕切换）
 *
 * 屏幕结构（1024x600）：
 *   ┌────────────────────────────────┐
 *   │ 标题 "系统设置"           [X]  │  顶部栏
 *   ├────────────────────────────────┤
 *   │ 时间 网络 串口 信息 显示 存储…  │  tab 按钮栏（2 行）
 *   ├────────────────────────────────┤
 *   │                               │
 *   │       内容区（当前模块）        │
 *   │                               │
 *   └────────────────────────────────┘
 */
#include "sys_settings.h"
#include "screen_manager.h"
#include "hmi_runtime.h"
#include <string.h>

#define SET_TAB_COUNT 9

static const char* const TAB_NAMES[SET_TAB_COUNT] = {
    "时间", "网络", "串口", "信息", "显示",
    "存储", "维护", "声音", "触摸"
};

typedef lv_obj_t* (*tab_create_fn)(lv_obj_t*);
static const tab_create_fn TAB_CREATORS[SET_TAB_COUNT] = {
    set_time_create, set_net_create, set_serial_create, set_info_create,
    set_display_create, set_storage_create, set_maint_create, set_sound_create,
    set_touch_cal_create
};

/* ── 状态 ── */
static lv_obj_t* s_scr = NULL;      /* 设置屏幕对象 */
static lv_obj_t* s_content = NULL;  /* 内容区容器 */
static lv_obj_t* s_tab_btns[SET_TAB_COUNT] = {0};
static int  s_cur_tab = 0;
static int  s_active = 0;
static int  s_prev_screen = -1;

/* ── 内部函数 ── */
static void show_tab(int idx);
static void build_scr(void);
static void bubble_tree(lv_obj_t* obj);

/* ── 开机设置入口窗口 ──
 * 开机后 HMI_SETTINGS_BOOT_WINDOW_MS 毫秒内，单击屏幕任意位置进入系统设置；
 * 窗口超时（未点击）或退出设置页后，进入工程运行页（第 0 屏）。
 * 实现：主屏之上叠加全屏透明点击捕获层（仿触摸校准 grab 层），点击即进设置；
 * 主循环 sys_settings_boot_poll() 负责超时收尾。 */
static lv_obj_t* s_boot_scr = NULL;
static lv_obj_t* s_boot_grab = NULL;
static uint32_t s_boot_start_ms = 0;
static int s_boot_done = 1;   /* 初始视为已完成（未启动） */

/* 收尾开机窗口：删除开机屏与点击捕获层（幂等） */
static void boot_finish(void)
{
    if (s_boot_grab) { lv_obj_delete(s_boot_grab); s_boot_grab = NULL; }
    if (s_boot_scr)  { lv_obj_delete(s_boot_scr);  s_boot_scr  = NULL; }
    s_boot_done = 1;
}

static void boot_tap_cb(lv_event_t* e)
{
    (void)e;
    if (s_boot_done) return;
    sys_settings_show();
    /* 从开机窗口进入设置：退出设置后回到工程运行页（第 0 屏）。
     * 开机阶段 hmi_screen_current_idx() 恒为 0（hmi_screens_init 初始），
     * 这里显式覆盖，避免 .so 未加载（s_current=-1）时退出设置无处可回。 */
    s_prev_screen = 0;
    boot_finish();
    HMI_LOG("sys_settings: boot tap -> settings");
}

/* 启动开机窗口（main.c 在 hmi_screens_init 之后调用；HMI_SETTINGS_BOOT_WINDOW_MS=0 禁用） */
void sys_settings_boot_start(void)
{
    if (!s_boot_done) return;
    if (HMI_SETTINGS_BOOT_WINDOW_MS <= 0) return;

    s_boot_scr = lv_obj_create(NULL);
    lv_obj_set_style_bg_color(s_boot_scr, lv_color_hex(0x0f172a), 0);
    lv_obj_set_style_pad_all(s_boot_scr, 0, 0);
    lv_obj_clear_flag(s_boot_scr, LV_OBJ_FLAG_SCROLLABLE);

    lv_obj_t* title = lv_label_create(s_boot_scr);
    lv_label_set_text(title, "zhishu-HMI");
    lv_obj_set_style_text_font(title, g_font_cn_20, 0);
    lv_obj_set_style_text_color(title, lv_color_white(), 0);
    lv_obj_align(title, LV_ALIGN_CENTER, 0, -50);

    lv_obj_t* hint = lv_label_create(s_boot_scr);
    lv_label_set_text(hint, "点击屏幕进入系统设置\n等待进入运行界面...");
    lv_obj_set_style_text_font(hint, g_font_cn_16, 0);
    lv_obj_set_style_text_color(hint, lv_color_hex(0x94a3b8), 0);
    lv_obj_align(hint, LV_ALIGN_CENTER, 0, 10);

    s_boot_grab = lv_obj_create(s_boot_scr);
    lv_obj_set_size(s_boot_grab, LV_PCT(100), LV_PCT(100));
    lv_obj_set_style_bg_opa(s_boot_grab, LV_OPA_TRANSP, 0);
    lv_obj_set_style_border_width(s_boot_grab, 0, 0);
    lv_obj_clear_flag(s_boot_grab, LV_OBJ_FLAG_SCROLLABLE);
    lv_obj_add_flag(s_boot_grab, LV_OBJ_FLAG_CLICKABLE);
    lv_obj_add_event_cb(s_boot_grab, boot_tap_cb, LV_EVENT_CLICKED, NULL);

    s_boot_start_ms = lv_tick_get();
    s_boot_done = 0;
    lv_screen_load(s_boot_scr);
    HMI_LOG("sys_settings: boot window started (%d ms)", HMI_SETTINGS_BOOT_WINDOW_MS);
}

/* 主循环轮询：已进设置则收尾窗口；超时未点击则进入工程运行页 */
void sys_settings_boot_poll(void)
{
    if (s_boot_done) return;
    if (s_active) { boot_finish(); return; }
    if (lv_tick_get() - s_boot_start_ms < (uint32_t)HMI_SETTINGS_BOOT_WINDOW_MS) return;
    /* 先切到运行页再删开机屏（先删活动屏会触发 LVGL "active screen was deleted" 警告） */
    if (hmi_screen_switch_idx(0) != 0) {
        HMI_LOG("sys_settings: boot window expired (no runtime screen)");
    }
    boot_finish();
}

/* ── 显示 / 隐藏 ── */
int sys_settings_is_active(void)
{
    return s_active;
}

void sys_settings_hide(void)
{
    if (!s_active) return;
    s_active = 0;
    /* 退出设置页：收起网络键盘、终止触摸校准，避免屏幕级覆盖层常驻 */
    set_net_kb_hide();
    set_touch_cal_abort();
    if (s_prev_screen >= 0) {
        hmi_screen_switch_idx(s_prev_screen);
    }
    s_prev_screen = -1;
}

void sys_settings_show(void)
{
    if (s_active) return;
    s_prev_screen = hmi_screen_current_idx();
    if (s_scr == NULL) build_scr();
    s_active = 1;
    lv_screen_load(s_scr);
    show_tab(s_cur_tab);
}

/* ── tab 切换 ── */
static void tab_click_cb(lv_event_t* e)
{
    lv_obj_t* btn = lv_event_get_target(e);
    int idx = (int)(intptr_t)lv_event_get_user_data(e);
    if (idx >= 0 && idx < SET_TAB_COUNT) show_tab(idx);
    (void)btn;
}

static void show_tab(int idx)
{
    /* 切换 tab 时终止可能进行中的触摸校准：十字/捕获层挂在屏幕上、
     * 不随内容区清理，若不终止会成为常驻覆盖层并遗留 s_running 卡死。 */
    if (idx != s_cur_tab) set_touch_cal_abort();
    s_cur_tab = idx;
    /* 更新 tab 按钮高亮：
     * 高亮必须用 LV_STATE_CHECKED 选择器——主题对 CHECKED 态定义了默认
     * 样式，优先级高于 DEFAULT 选择器，用 selector 0 设置蓝色会被覆盖。 */
    for (int i = 0; i < SET_TAB_COUNT; i++) {
        if (s_tab_btns[i]) {
            if (i == idx) {
                lv_obj_add_state(s_tab_btns[i], LV_STATE_CHECKED);
                lv_obj_set_style_bg_color(s_tab_btns[i], lv_color_hex(0x2f6fdb), LV_STATE_CHECKED);
            } else {
                lv_obj_remove_state(s_tab_btns[i], LV_STATE_CHECKED);
                lv_obj_set_style_bg_color(s_tab_btns[i], lv_color_hex(0x3a3f4a), LV_STATE_DEFAULT);
            }
        }
    }
    /* 收起网络页屏幕键盘（若已弹出） */
    set_net_kb_hide();
    /* 重建内容区 */
    if (s_content) lv_obj_clean(s_content);
    if (TAB_CREATORS[idx]) {
        TAB_CREATORS[idx](s_content);
    }
    /* 使内容区所有后代对象冒泡 CLICKED 事件到屏幕：
     * LVGL v9 默认不带 EVENT_BUBBLE 标志，若不加此标志，点击内容区
     * 内控件（按钮/标签/下拉等）不会触发屏幕级 LV_EVENT_CLICKED，
     * 导致 set_net 键盘的"点击外部关闭"机制失效。 */
    bubble_tree(s_content);
}

/* 递归给对象及其后代添加 EVENT_BUBBLE（供键盘点击外部关闭使用） */
static void bubble_tree(lv_obj_t* obj)
{
    if (!obj) return;
    lv_obj_add_flag(obj, LV_OBJ_FLAG_EVENT_BUBBLE);
    uint32_t n = lv_obj_get_child_count(obj);
    for (uint32_t i = 0; i < n; i++) {
        bubble_tree(lv_obj_get_child(obj, i));
    }
}

/* ── 关闭按钮 ── */
static void close_cb(lv_event_t* e)
{
    (void)e;
    sys_settings_hide();
}

/* ── 构建主界面 ── */
static void build_scr(void)
{
    s_scr = lv_obj_create(NULL);
    lv_obj_set_style_bg_color(s_scr, lv_color_hex(0x22252b), 0);
    lv_obj_set_style_pad_all(s_scr, 0, 0);
    lv_obj_clear_flag(s_scr, LV_OBJ_FLAG_SCROLLABLE);

    /* 顶部标题栏 */
    lv_obj_t* head = lv_obj_create(s_scr);
    lv_obj_set_size(head, LV_PCT(100), 44);
    lv_obj_align(head, LV_ALIGN_TOP_MID, 0, 0);
    lv_obj_set_style_bg_color(head, lv_color_hex(0x2f6fdb), 0);
    lv_obj_set_style_border_width(head, 0, 0);
    lv_obj_set_style_pad_all(head, 0, 0);
    /* 主题默认文本色为深灰（面向浅色 UI），在深色背景上不可见。
     * TEXT_COLOR 样式可继承，此处设白色后所有后代 label 自动继承。 */
    lv_obj_set_style_text_color(head, lv_color_white(), 0);
    lv_obj_clear_flag(head, LV_OBJ_FLAG_SCROLLABLE);

    lv_obj_t* title = lv_label_create(head);
    lv_label_set_text(title, "系统设置");
    lv_obj_set_style_text_font(title, g_font_cn_20, 0);
    lv_obj_align(title, LV_ALIGN_LEFT_MID, 16, 0);

    lv_obj_t* btn_close = lv_button_create(head);
    lv_obj_set_size(btn_close, 64, 34);
    lv_obj_align(btn_close, LV_ALIGN_RIGHT_MID, -10, 0);
    lv_obj_set_style_bg_color(btn_close, lv_color_hex(0xd05252), 0);
    lv_obj_add_event_cb(btn_close, close_cb, LV_EVENT_CLICKED, NULL);
    lv_obj_t* lbl_close = lv_label_create(btn_close);
    lv_label_set_text(lbl_close, "返回");
    lv_obj_set_style_text_font(lbl_close, g_font_cn_16, 0);
    lv_obj_set_align(lbl_close, LV_ALIGN_CENTER);

    /* 屏幕分辨率（不依赖对象布局状态：刚创建对象的 coords 未布局，
     * lv_obj_get_width 返回 0，会导致按钮宽度为负而渲染失败） */
    int scr_w = lv_display_get_horizontal_resolution(lv_display_get_default());
    int scr_h = lv_display_get_vertical_resolution(lv_display_get_default());

    /* tab 按钮栏：2 行（5 + 4），按钮宽度随屏宽均分（响应式） */
    lv_obj_t* tabbar = lv_obj_create(s_scr);
    lv_obj_set_size(tabbar, LV_PCT(100), 92);
    lv_obj_align(tabbar, LV_ALIGN_TOP_MID, 0, 44);
    lv_obj_set_style_bg_color(tabbar, lv_color_hex(0x2b2f37), 0);
    lv_obj_set_style_border_width(tabbar, 0, 0);
    lv_obj_set_style_pad_all(tabbar, 0, 0);
    lv_obj_set_style_pad_top(tabbar, 6, 0);
    lv_obj_set_style_pad_bottom(tabbar, 6, 0);
    lv_obj_set_style_pad_left(tabbar, 10, 0);
    lv_obj_set_style_pad_right(tabbar, 10, 0);
    lv_obj_set_style_text_color(tabbar, lv_color_white(), 0);
    lv_obj_clear_flag(tabbar, LV_OBJ_FLAG_SCROLLABLE);

    const int row1 = 5;                        /* 第一行按钮数 */
    const int tab_gap = 8;                     /* 按钮间距 */
    int tab_span = scr_w - 20;                 /* 可用宽（减左右 pad） */
    int btn_w = (tab_span - (row1 - 1) * tab_gap) / row1;
    int btn_h = (92 - 12 - tab_gap) / 2;       /* 2 行均分高度 */
    for (int i = 0; i < SET_TAB_COUNT; i++) {
        int row = (i < row1) ? 0 : 1;
        int col = (i < row1) ? i : (i - row1);
        lv_obj_t* b = lv_button_create(tabbar);
        lv_obj_set_size(b, btn_w, btn_h);
        lv_obj_align(b, LV_ALIGN_TOP_LEFT,
                     10 + col * (btn_w + tab_gap), 6 + row * (btn_h + tab_gap));
        lv_obj_set_style_bg_color(b, lv_color_hex(0x3a3f4a), 0);
        lv_obj_add_event_cb(b, tab_click_cb, LV_EVENT_CLICKED, (void*)(intptr_t)i);
        lv_obj_t* l = lv_label_create(b);
        lv_label_set_text(l, TAB_NAMES[i]);
        lv_obj_set_style_text_font(l, g_font_cn_16, 0);
        lv_obj_set_align(l, LV_ALIGN_CENTER);
        s_tab_btns[i] = b;
    }

    /* 内容区：高度 = 屏高 - 顶栏(44) - tab栏(92)，随分辨率自适应。
     * 必须用显式像素尺寸（不能用 LV_PCT(100) 高度，否则内容区会延伸到
     * 屏外导致底部按钮不可见；也不能用未布局对象的 get_width 计算）。 */
    s_content = lv_obj_create(s_scr);
    lv_obj_set_size(s_content, scr_w, scr_h - 44 - 92);
    lv_obj_align(s_content, LV_ALIGN_TOP_MID, 0, 136);
    lv_obj_set_style_bg_color(s_content, lv_color_hex(0x22252b), 0);
    lv_obj_set_style_border_width(s_content, 0, 0);
    lv_obj_set_style_pad_all(s_content, 12, 0);
    lv_obj_set_style_pad_top(s_content, 8, 0);
    lv_obj_set_style_pad_bottom(s_content, 8, 0);
    /* 主题默认文本色为深灰（面向浅色 UI），在深色背景上不可见。
     * TEXT_COLOR 样式可继承，此处设白色后所有后代 label（字段名、
     * 提示文本等）自动继承，不再逐个设置。 */
    lv_obj_set_style_text_color(s_content, lv_color_white(), 0);
    /* 内容区必须保持不可滚动（SCROLLABLE）：
     * LVGL 9 中 SCROLLABLE 对象会把子对象裁剪到"滚动区域"，若滚动区域
     * 计算异常（子 label 未计入），label 会被整体裁剪而不渲染（表现为
     * 信息页/字段名等文本空白）。各模块布局均已控制在内容区高度内，
     * 无需滚动，故此处禁止 SCROLLABLE。 */
    lv_obj_clear_flag(s_content, LV_OBJ_FLAG_SCROLLABLE);
}
