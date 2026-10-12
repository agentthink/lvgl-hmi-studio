/*
 * sys_settings.h — 系统设置模块公共接口
 *
 * 入口：开机后 HMI_SETTINGS_BOOT_WINDOW_MS 毫秒内单击屏幕任意位置进入
 * 系统设置；窗口超时未点击、或退出设置页后进入工程运行页（第 0 屏）。
 * 由 main.c 调用 sys_settings_boot_start() 启动窗口，主循环调用
 * sys_settings_boot_poll() 处理超时收尾。
 *
 * 模块（9 个 tab）：
 *   时间 / 网络 / 串口 / 信息 / 显示 / 存储 / 维护 / 声音 / 触摸
 */
#ifndef SYS_SETTINGS_H
#define SYS_SETTINGS_H

#include "lvgl.h"

#ifdef __cplusplus
extern "C" {
#endif

/* ── 中文字体（freetype，main.c 初始化，系统设置各模块使用） ── */
extern lv_font_t* g_font_cn_12;
extern lv_font_t* g_font_cn_14;
extern lv_font_t* g_font_cn_16;
extern lv_font_t* g_font_cn_20;

/* ── 开机设置入口窗口（main.c 启动窗口 / 主循环轮询收尾） ── */
void sys_settings_boot_start(void);
void sys_settings_boot_poll(void);

/* ── 显示 / 隐藏系统设置 ── */
void sys_settings_show(void);
void sys_settings_hide(void);
int  sys_settings_is_active(void);

/* ── 各设置页创建函数（parent 为内容区，返回创建的根对象，由框架 lv_obj_clean 管理） ── */
lv_obj_t* set_time_create(lv_obj_t* parent);
lv_obj_t* set_net_create(lv_obj_t* parent);
lv_obj_t* set_serial_create(lv_obj_t* parent);
lv_obj_t* set_info_create(lv_obj_t* parent);
lv_obj_t* set_display_create(lv_obj_t* parent);
lv_obj_t* set_storage_create(lv_obj_t* parent);
lv_obj_t* set_maint_create(lv_obj_t* parent);
lv_obj_t* set_sound_create(lv_obj_t* parent);
lv_obj_t* set_touch_cal_create(lv_obj_t* parent);

/* ── 网络页屏幕键盘：tab 切换时由框架主动收起 ── */
void set_net_kb_hide(void);

/* ── 触摸校准：tab 切换/退出设置页时由框架终止校准（清理屏幕级捕获层） ── */
void set_touch_cal_abort(void);

#ifdef __cplusplus
}
#endif

#endif /* SYS_SETTINGS_H */
