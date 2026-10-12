/*
 * soft_keyboard.h — 宿主软键盘服务（ABI V3）
 *
 * 设计：
 *   - .so 的 TextArea 控件由宿主统一接管输入：hmi_kb_attach() 在屏幕创建后
 *     遍历控件树，给每个 lv_textarea 挂 CLICK_FOCUSABLE + 焦点回调。
 *   - 用户点击输入框 → 宿主在 lv_layer_top() 弹出软键盘浮层：
 *       "num"  数字键盘（小数点/负号，生成代码 hmi_set_ta_keyboard_mode(ta,"num")）
 *       "text" 中英文键盘（拼音候选 + 英文字母 + 数字符号页，默认）
 *   - 键盘按键直接调用 lv_textarea_add_text / lv_textarea_del_char 写入
 *     焦点输入框 —— 与工程无关，任意 .so 的输入框均可输入。
 *   - 中文拼音：pinyin_ime_data.c 提供音节→候选字表（字频排序）。
 */
#ifndef HMI_SOFT_KEYBOARD_H
#define HMI_SOFT_KEYBOARD_H

#include "lvgl.h"

/* 拼音表条目：音节 → 候选字串（UTF-8，字频降序） */
typedef struct {
    const char* py;     /* 拼音音节（无调号，小写） */
    const char* chars;  /* 候选字串（UTF-8） */
} hmi_py_entry_t;

/* pinyin_ime_data.c 提供（由工具从 pinyin-data + 字频表生成） */
extern const hmi_py_entry_t hmi_py_table[];
extern const int hmi_py_table_count;

/* 屏幕创建后调用：遍历控件树接管全部 TextArea 输入 */
void hmi_kb_attach(lv_obj_t* screen);

/* 关闭软键盘（屏幕切换/焦点丢失时调用） */
void hmi_kb_close(void);

/* 生成代码调用：标记输入框键盘模式（"num"/"text"，未调用默认 text） */
void hmi_set_ta_keyboard_mode(lv_obj_t* ta, const char* mode);

/* 调试：强制弹出软键盘到指定输入框（无输入框时创建演示框；SIGUSR1 触发用） */
void hmi_kb_debug_open(void);

/* 调试：同上，可指定键盘模式（"text"/"num"，数字键盘验证用） */
void hmi_kb_debug_open_mode(const char* mode);

/* 调试：向当前焦点输入框插入文本（SIGUSR2 触发，远程验证输入/中文渲染） */
void hmi_kb_debug_insert(const char* text);

/* 调试：模拟拼音输入全流程（n/i → 候选 → 选首候选），验证候选表与上屏 */
void hmi_kb_debug_pinyin(void);

#endif /* HMI_SOFT_KEYBOARD_H */
