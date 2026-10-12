/*
 * screen_manager.h — 屏幕管理
 *
 * 屏幕注册表 HMI_SCREENS 由编辑器导出的 generated/screens.c 提供：
 *   static lv_obj_t* create_screen_home(void) { ... }   // 编辑器生成的函数体
 *   const screen_def_t HMI_SCREENS[] = {
 *       { "home", "screen_1", create_screen_home, 1, 0 },
 *       ...
 *   };
 * 每项第 4 个字段 is_public 表示公共页面（多用户共享查看），
 * 第 5 个字段 public_access 为访问级别（0=公开, 1=登录用户, 2=仅管理员）。
 */
#ifndef HMI_SCREEN_MANAGER_H
#define HMI_SCREEN_MANAGER_H

#include "lvgl.h"

#define HMI_MAX_SCREENS 64

typedef lv_obj_t* (*screen_create_fn)(void);

typedef struct {
    const char* name;        /* 屏幕名称（编辑器 HmiScreen.Name） */
    const char* id;          /* 屏幕 Id（编辑器 HmiScreen.Id） */
    screen_create_fn create; /* 创建函数 */
    int is_public;           /* 1=公共页面（多用户共享查看同一屏幕） */
    int public_access;       /* 公共页面访问级别：0=公开, 1=登录用户, 2=仅管理员 */
} screen_def_t;

/* 图层引用（公共页面作为背景图层，被引用后不再作为独立屏幕切换） */
typedef lv_obj_t* (*screen_layer_create_fn)(lv_obj_t* parent);

typedef struct {
    const char* screen_id;        /* 引用方屏幕 id（图层挂载的目标屏幕） */
    const char* layer_id;         /* 被引用公共页面（图层）id */
    screen_layer_create_fn create;/* 图层创建函数 create_screen_<layer_id>_inner */
} screen_layer_ref_t;

extern const screen_def_t HMI_SCREENS[];
extern const int HMI_SCREEN_COUNT;

/* 图层表由 .so 的 hmi_ui_get_layers 提供（screen_manager.c 经 dlsym 获取） */
extern const screen_layer_ref_t* hmi_ui_get_layers(int* count);

/* 创建全部屏幕并加载第一个 */
void hmi_screens_init(void);
/* 宿主级 CJK 字体修复：下拉框主体/弹出列表显式补设工程字体（防中文空白/方框） */
void hmi_screens_fix_fonts(void);

/* 按名称/索引切换屏幕；成功返回 0，失败返回 -1 */
int hmi_screen_switch(const char* name);
int hmi_screen_switch_idx(int index);
int hmi_screen_current_idx(void);

/* 公共页面查询（供权限/动作模块使用） */
int hmi_screen_is_public(int index);
int hmi_screen_public_access(int index);

#endif /* HMI_SCREEN_MANAGER_H */
