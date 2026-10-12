/*
 * soft_keyboard.c — 宿主软键盘服务（ABI V3）
 *
 * 中英文输入软键盘 + 数字输入软键盘：
 *   - "num"  数字键盘（小数点/负号/退格/完成）
 *   - "text" 中英文键盘：拼音候选（字频排序）+ 英文字母 + 数字符号页
 *   - 键盘浮层挂在 lv_layer_top()，按键直写焦点 TextArea，
 *     与 .so 工程无关（任意工程的输入框均可输入）。
 */
#include "soft_keyboard.h"
#include "hmi_runtime.h"   /* HMI_LOG */
#include <string.h>

/* main.c 导出的宿主 CJK 字体（中文按键/候选字用） */
extern lv_font_t* g_font_cn_16;
extern lv_font_t* g_font_cn_20;

/* ── 前向声明 ── */
static void kb_ta_focus_cb(lv_event_t* e);
static void kb_ta_defocus_cb(lv_event_t* e);
static void kb_ta_delete_cb(lv_event_t* e);
static void kb_render(void);
static void kb_cand_render(void);
static void kb_update_candidates(void);
void hmi_kb_open(void);

/* ── 键盘模式注册表 ── */
#define HMI_KB_MAX_TAS 64
typedef enum { KB_MODE_TEXT, KB_MODE_NUM } kb_mode_t;
typedef struct {
    lv_obj_t* ta;
    kb_mode_t mode;
} kb_ta_entry_t;
static kb_ta_entry_t s_tas[HMI_KB_MAX_TAS];
static int s_ta_count = 0;

static lv_obj_t* s_focus_ta = NULL;   /* 当前焦点输入框 */
static kb_mode_t  s_mode = KB_MODE_TEXT;

/* ── 键盘 UI 状态 ── */
static lv_obj_t* s_kb = NULL;         /* 键盘浮层面板 */
static lv_obj_t* s_py_label = NULL;   /* 拼音缓冲显示 */
static lv_obj_t* s_cand_row = NULL;   /* 候选字容器 */
static char s_py_buf[24];
static int  s_py_len = 0;
static int  s_shift = 0;
/* text 子布局：0=中文拼音 1=英文 2=数字符号 */
static int  s_text_page = 0;

/* ── 样式基色（工业深色） ── */
#define KB_BG        lv_color_hex(0x14191f)
#define KB_KEY       lv_color_hex(0x27303c)
#define KB_KEY_SPEC  lv_color_hex(0x0e5c66)
#define KB_KEY_ACC   lv_color_hex(0x1d6f7a)
#define KB_TEXT      lv_color_hex(0xe8edf2)

static lv_font_t* kb_font(void)
{
    return g_font_cn_16 ? g_font_cn_16 : LV_FONT_DEFAULT;
}

/* ── 模式注册 ── */
void hmi_set_ta_keyboard_mode(lv_obj_t* ta, const char* mode)
{
    if (!ta || !mode) return;
    kb_mode_t m = (strcmp(mode, "num") == 0) ? KB_MODE_NUM : KB_MODE_TEXT;

    for (int i = 0; i < s_ta_count; i++) {
        if (s_tas[i].ta == ta) { s_tas[i].mode = m; return; }
    }
    if (s_ta_count < HMI_KB_MAX_TAS) {
        s_tas[s_ta_count].ta = ta;
        s_tas[s_ta_count].mode = m;
        s_ta_count++;
    }
}

static kb_mode_t kb_mode_of(lv_obj_t* ta)
{
    for (int i = 0; i < s_ta_count; i++)
        if (s_tas[i].ta == ta) return s_tas[i].mode;
    return KB_MODE_TEXT;
}

static void kb_mode_forget(lv_obj_t* ta)
{
    for (int i = 0; i < s_ta_count; i++) {
        if (s_tas[i].ta == ta) {
            s_tas[i] = s_tas[s_ta_count - 1];
            s_ta_count--;
            return;
        }
    }
}

/* ── 控件树遍历：接管 TextArea ── */
static void kb_instrument(lv_obj_t* obj)
{
    if (!obj) return;
    if (lv_obj_get_class(obj) == &lv_textarea_class) {
        lv_obj_add_flag(obj, LV_OBJ_FLAG_CLICK_FOCUSABLE);
        lv_obj_add_event_cb(obj, kb_ta_focus_cb, LV_EVENT_FOCUSED, NULL);
        lv_obj_add_event_cb(obj, kb_ta_defocus_cb, LV_EVENT_DEFOCUSED, NULL);
        lv_obj_add_event_cb(obj, kb_ta_delete_cb, LV_EVENT_DELETE, NULL);
        return;   /* textarea 内部无输入控件，无需深入 */
    }
    uint32_t n = lv_obj_get_child_count(obj);
    for (uint32_t i = 0; i < n; i++)
        kb_instrument(lv_obj_get_child(obj, (int32_t)i));
}

void hmi_kb_attach(lv_obj_t* screen)
{
    if (!screen) return;
    kb_instrument(screen);
    HMI_LOG("kb attached to screen");
}

/* ── 焦点回调 ── */
static void kb_ta_focus_cb(lv_event_t* e)
{
    s_focus_ta = lv_event_get_target(e);
    s_mode = kb_mode_of(s_focus_ta);
    s_py_len = 0; s_py_buf[0] = '\0';
    s_shift = 0;
    if (s_mode == KB_MODE_TEXT) s_text_page = 0;   /* 中文拼音优先 */
    hmi_kb_open();
}

static void kb_ta_defocus_cb(lv_event_t* e)
{
    (void)e;
    s_focus_ta = NULL;
    hmi_kb_close();
}

static void kb_ta_delete_cb(lv_event_t* e)
{
    lv_obj_t* ta = lv_event_get_target(e);
    if (s_focus_ta == ta) {
        s_focus_ta = NULL;
        hmi_kb_close();
    }
    kb_mode_forget(ta);
}

/* ── UTF-8 工具 ── */
static int utf8_len(uint8_t c)
{
    if (c < 0x80) return 1;
    if ((c & 0xE0) == 0xC0) return 2;
    if ((c & 0xF0) == 0xE0) return 3;
    return 4;
}

/* ── 候选字（拼音前缀匹配 + 字频序去重） ── */
#define HMI_KB_CAND_MAX 12
static char s_cands[HMI_KB_CAND_MAX * 4 + 1];   /* UTF-8 候选串（连续存储） */

static int cand_contains(const char* u, int ulen)
{
    for (int i = 0; s_cands[i];) {
        if (memcmp(s_cands + i, u, (size_t)ulen) == 0) return 1;
        i += utf8_len((uint8_t)s_cands[i]);
    }
    return 0;
}

static void kb_update_candidates(void)
{
    int n = 0;
    s_cands[0] = '\0';
    if (s_py_len > 0) {
        /* 第一遍：精确音节匹配优先（拼音敲全时精确音节的字排最前） */
        for (int i = 0; i < hmi_py_table_count && n < HMI_KB_CAND_MAX; i++) {
            if (strcmp(hmi_py_table[i].py, s_py_buf) != 0) continue;
            const char* chars = hmi_py_table[i].chars;
            for (int k = 0; chars[k] && n < HMI_KB_CAND_MAX;) {
                int ulen = utf8_len((uint8_t)chars[k]);
                if (!cand_contains(chars + k, ulen)) {
                    /* 注意：必须先取 pos 再 memcpy —— memcpy 覆盖旧 NUL 后
                     * strlen 会扫进陈旧缓冲返回错误长度（实机候选串错乱根因） */
                    size_t pos = strlen(s_cands);
                    memcpy(s_cands + pos, chars + k, (size_t)ulen);
                    s_cands[pos + (size_t)ulen] = '\0';
                    n++;
                }
                k += ulen;
            }
        }
        /* 第二遍：前缀匹配（未敲全时按拼音序补足候选） */
        for (int i = 0; i < hmi_py_table_count && n < HMI_KB_CAND_MAX; i++) {
            if (strncmp(hmi_py_table[i].py, s_py_buf, (size_t)s_py_len) != 0) continue;
            const char* chars = hmi_py_table[i].chars;
            for (int k = 0; chars[k] && n < HMI_KB_CAND_MAX;) {
                int ulen = utf8_len((uint8_t)chars[k]);
                if (!cand_contains(chars + k, ulen)) {
                    size_t pos = strlen(s_cands);
                    memcpy(s_cands + pos, chars + k, (size_t)ulen);
                    s_cands[pos + (size_t)ulen] = '\0';
                    n++;
                }
                k += ulen;
            }
        }
    }
    kb_cand_render();
}

/* 取候选串中第 idx 个字符（UTF-8） */
static const char* cand_at(int idx)
{
    int pos = 0;
    for (int i = 0; i < idx && s_cands[pos]; i++)
        pos += utf8_len((uint8_t)s_cands[pos]);
    return s_cands[pos] ? s_cands + pos : NULL;
}

/* ── 按键动作 ── */
typedef enum {
    KA_CHAR,       /* 直接插入 UTF-8 文本 */
    KA_PY,         /* 拼音字母（进入缓冲） */
    KA_BACK,       /* 退格 */
    KA_SHIFT,      /* 大小写切换 */
    KA_PAGE,       /* 中文/英文/数字符号 页切换 */
    KA_SPACE,      /* 空格（中文页选首候选） */
    KA_DONE,       /* 收起键盘 */
    KA_CAND,       /* 候选字（idx 存 text[0]） */
} ka_kind_t;

typedef struct {
    ka_kind_t kind;
    char text[8];
} kb_action_t;

static kb_action_t kb_act(ka_kind_t kind, const char* t)
{
    kb_action_t a = { kind, { 0 } };
    if (t) snprintf(a.text, sizeof(a.text), "%s", t);
    return a;
}

/*
 * 按键动作的**稳定存储**（阶段 120 修复 use-after-return）。
 *
 * ⚠ 绝不能把 `kb_render()` / `kb_cand_render()` 里栈局部 `kb_action_t` 的地址交给
 *   LVGL 当 event user_data：那两个函数一返回指针就悬空，而按键是在**之后**才被点的。
 *   旧写法下每个按键的 user_data 都指向同一个被复用的栈槽 ⇒ `kb_key_cb` 解引用到
 *   「最后写入的那条动作」（按哪个键都可能执行"完成"），且属未定义行为。
 *   这里用两块静态池（主键盘一块、候选行一块），各自的渲染函数在重建前把游标归零；
 *   归零是安全的，因为旧按键此时已被 `lv_obj_delete` 删除、不会再触发回调。
 */
#define KB_MAX_KEYS    64
#define KB_MAX_CANDS   32
static kb_action_t s_key_actions[KB_MAX_KEYS];
static int         s_key_action_count;
static kb_action_t s_cand_actions[KB_MAX_CANDS];
static int         s_cand_action_count;

/* 当前渲染目标池（由 kb_render / kb_cand_render 在构建前指定） */
static kb_action_t* s_cur_actions;
static int*         s_cur_action_count;
static int          s_cur_action_cap;

static void kb_use_action_pool(kb_action_t* pool, int* count, int cap)
{
    s_cur_actions = pool;
    s_cur_action_count = count;
    s_cur_action_cap = cap;
    *count = 0;
}

/* 把动作复制进当前池并返回稳定指针；池满/无动作返回 NULL（按键退化为无动作） */
static const kb_action_t* kb_store_action(const kb_action_t* action)
{
    if (!action || !s_cur_actions || !s_cur_action_count) return NULL;
    if (*s_cur_action_count >= s_cur_action_cap) return NULL;
    kb_action_t* slot = &s_cur_actions[(*s_cur_action_count)++];
    *slot = *action;
    return slot;
}

static void kb_insert(const char* utf8)
{
    if (s_focus_ta) lv_textarea_add_text(s_focus_ta, utf8);
}

static void kb_backspace(void)
{
    if (s_py_len > 0) {
        s_py_buf[--s_py_len] = '\0';
        kb_update_candidates();
        return;
    }
    if (s_focus_ta) lv_textarea_delete_char(s_focus_ta);
}

static void kb_key_cb(lv_event_t* e)
{
    const kb_action_t* a = (const kb_action_t*)lv_event_get_user_data(e);
    if (!a) return;
    switch (a->kind) {
    case KA_CHAR:
        kb_insert(a->text);
        break;
    case KA_PY:
        if (s_py_len < (int)sizeof(s_py_buf) - 2) {
            s_py_buf[s_py_len++] = a->text[0];
            s_py_buf[s_py_len] = '\0';
        }
        kb_update_candidates();
        break;
    case KA_BACK:
        kb_backspace();
        break;
    case KA_SHIFT:
        s_shift = !s_shift;
        kb_render();
        break;
    case KA_PAGE:
        if (strcmp(a->text, "cn") == 0) s_text_page = 0;
        else if (strcmp(a->text, "en") == 0) s_text_page = 1;
        else s_text_page = 2;
        s_py_len = 0; s_py_buf[0] = '\0';
        s_shift = 0;
        kb_render();
        break;
    case KA_SPACE:
        if (s_text_page == 0 && s_cands[0]) {
            const char* one = cand_at(0);
            if (one) {
                char buf[5];
                int ulen = utf8_len((uint8_t)one[0]);
                memcpy(buf, one, (size_t)ulen);
                buf[ulen] = '\0';
                kb_insert(buf);
            }
            s_py_len = 0; s_py_buf[0] = '\0';
            kb_update_candidates();
        } else {
            kb_insert(" ");
        }
        break;
    case KA_DONE:
        hmi_kb_close();
        break;
    case KA_CAND: {
        int idx = (unsigned char)a->text[0];
        const char* one = cand_at(idx);
        if (one) {
            char buf[5];
            int ulen = utf8_len((uint8_t)one[0]);
            memcpy(buf, one, (size_t)ulen);
            buf[ulen] = '\0';
            kb_insert(buf);
        }
        s_py_len = 0; s_py_buf[0] = '\0';
        kb_update_candidates();
        break;
    }
    }
}

/* ── 键盘构建 ── */
static lv_obj_t* kb_make_key(lv_obj_t* parent, int x, int y, int w, int h,
                             const char* label, const kb_action_t* action, int accent)
{
    lv_obj_t* btn = lv_button_create(parent);
    lv_obj_set_pos(btn, x, y);
    lv_obj_set_size(btn, w, h);
    lv_obj_set_style_bg_color(btn, accent ? KB_KEY_ACC : KB_KEY, 0);
    lv_obj_set_style_radius(btn, 6, 0);
    lv_obj_set_style_shadow_width(btn, 0, 0);
    lv_obj_set_style_border_width(btn, 0, 0);

    lv_obj_t* lbl = lv_label_create(btn);
    lv_label_set_text(lbl, label);
    lv_obj_set_style_text_font(lbl, kb_font(), 0);
    lv_obj_set_style_text_color(lbl, KB_TEXT, 0);
    lv_obj_center(lbl);

    if (action) {
        const kb_action_t* stored = kb_store_action(action);
        if (stored) lv_obj_add_event_cb(btn, kb_key_cb, LV_EVENT_CLICKED, (void*)stored);
    }
    return btn;
}

#define KB_PAD  6
#define KB_GAP  4
#define KB_H    44

static int kb_w(void)
{
    lv_display_t* d = lv_display_get_default();
    int w = d ? (int)lv_display_get_horizontal_resolution(d) : 1024;
    return w;
}

static void kb_clear(lv_obj_t* parent)
{
    while (lv_obj_get_child_count(parent) > 0)
        lv_obj_delete(lv_obj_get_child(parent, 0));
    s_py_label = NULL;
    s_cand_row = NULL;
}

/* 候选字渲染（构建/刷新时调用） */
static void kb_cand_render(void)
{
    if (!s_kb || !s_cand_row) return;
    while (lv_obj_get_child_count(s_cand_row) > 0)
        lv_obj_delete(lv_obj_get_child(s_cand_row, 0));
    kb_use_action_pool(s_cand_actions, &s_cand_action_count, KB_MAX_CANDS);

    int x = 0, idx = 0;
    for (int pos = 0; s_cands[pos];) {
        int ulen = utf8_len((uint8_t)s_cands[pos]);
        char one[5];
        memcpy(one, s_cands + pos, (size_t)ulen);
        one[ulen] = '\0';
        char idxc = (char)idx;
        kb_action_t act = kb_act(KA_CAND, "");
        act.text[0] = idxc;
        kb_make_key(s_cand_row, x, 0, 92, 40, one, &act, 0);
        x += 96;
        idx++;
        pos += ulen;
        if (x > kb_w() - 92) break;
    }
}

static void kb_render(void)
{
    if (!s_kb) return;
    kb_clear(s_kb);
    kb_use_action_pool(s_key_actions, &s_key_action_count, KB_MAX_KEYS);
    int W = kb_w();
    int y = KB_PAD;

    /* 中文页顶部：拼音缓冲 + 候选行 */
    if (s_mode == KB_MODE_TEXT && s_text_page == 0) {
        s_py_label = lv_label_create(s_kb);
        lv_label_set_text(s_py_label, s_py_len ? s_py_buf : "拼音");
        lv_obj_set_style_text_font(s_py_label, kb_font(), 0);
        lv_obj_set_style_text_color(s_py_label, KB_TEXT, 0);
        lv_obj_set_pos(s_py_label, 10, y);
        y += 26;

        s_cand_row = lv_obj_create(s_kb);
        lv_obj_set_pos(s_cand_row, 0, y);
        lv_obj_set_size(s_cand_row, W, 44);
        lv_obj_set_style_bg_opa(s_cand_row, 0, 0);
        lv_obj_set_style_border_width(s_cand_row, 0, 0);
        lv_obj_set_style_pad_all(s_cand_row, 0, 0);
        y += 48;
    }

    if (s_mode == KB_MODE_NUM) {
        /* 数字键盘：4×4 */
        static const char* num_keys[16] = {
            "1", "2", "3", "⌫",
            "4", "5", "6", ".",
            "7", "8", "9", "−",
            "完成", "0", "", "✓"
        };
        int cw = (W - KB_PAD * 2 - KB_GAP * 3) / 4;
        for (int r = 0; r < 4; r++) {
            for (int c = 0; c < 4; c++) {
                const char* label = num_keys[r * 4 + c];
                if (!label || !label[0]) continue;
                kb_action_t act = kb_act(KA_CHAR, label);
                if (strcmp(label, "⌫") == 0) act = kb_act(KA_BACK, NULL);
                else if (strcmp(label, "完成") == 0 || strcmp(label, "✓") == 0) act = kb_act(KA_DONE, NULL);
                int accent = (strcmp(label, "完成") == 0 || strcmp(label, "✓") == 0);
                kb_make_key(s_kb, KB_PAD + c * (cw + KB_GAP), y + r * (KB_H + KB_GAP),
                            cw, KB_H, label, &act, accent);
            }
        }
        lv_obj_set_height(s_kb, y + 4 * KB_H + 3 * KB_GAP + KB_PAD);
        return;
    }

    /* 文本键盘（中文拼音/英文/数字符号三页共用 QWERTY 布局） */
    int cw = (W - KB_PAD * 2 - KB_GAP * 9) / 10;
    const char* rows[3][10] = {
        { "q", "w", "e", "r", "t", "y", "u", "i", "o", "p" },
        { "a", "s", "d", "f", "g", "h", "j", "k", "l", "⌫" },
        { "⇧", "z", "x", "c", "v", "b", "n", "m", "中/英", "123" },
    };
    for (int r = 0; r < 3; r++) {
        for (int c = 0; c < 10; c++) {
            const char* label = rows[r][c];
            kb_action_t act = kb_act(KA_CHAR, label);
            char shown[16];
            snprintf(shown, sizeof(shown), "%s", label);

            if (strcmp(label, "⌫") == 0) act = kb_act(KA_BACK, NULL);
            else if (strcmp(label, "⇧") == 0) act = kb_act(KA_SHIFT, NULL);
            else if (strcmp(label, "中/英") == 0) act = kb_act(KA_PAGE, s_text_page == 1 ? "cn" : "en");
            else if (strcmp(label, "123") == 0) act = kb_act(KA_PAGE, s_text_page == 2 ? "cn" : "123");
            else if (s_text_page == 0) act = kb_act(KA_PY, label);
            else if (s_text_page == 1) {
                /* 英文页：直接输入字母，Shift 切换大小写 */
                if (label[0] >= 'a' && label[0] <= 'z') {
                    shown[0] = (char)(s_shift ? label[0] - 32 : label[0]);
                    act = kb_act(KA_CHAR, shown);
                }
            } else {
                /* 数字符号页：数字行直接输入 */
                act = kb_act(KA_CHAR, label);
            }
            int accent = (s_shift && strcmp(label, "⇧") == 0) ||
                         (s_text_page == 1 && strcmp(label, "中/英") == 0) ||
                         (s_text_page == 2 && strcmp(label, "123") == 0);
            kb_make_key(s_kb, KB_PAD + c * (cw + KB_GAP), y + r * (KB_H + KB_GAP),
                        cw, KB_H, shown, &act, accent);
        }
    }
    /* 底行：123/中英 切换、空格、完成 */
    int bottom_y = y + 3 * (KB_H + KB_GAP);
    int w1 = cw * 3, w2 = cw * 4;
    kb_action_t page_act = kb_act(KA_PAGE, s_text_page == 2 ? "cn" : "123");
    kb_make_key(s_kb, KB_PAD, bottom_y, w1, KB_H,
                s_text_page == 2 ? "中/英" : "123", &page_act, 0);
    kb_action_t space_act = kb_act(KA_SPACE, NULL);
    kb_make_key(s_kb, KB_PAD + w1 + KB_GAP, bottom_y, w2, KB_H,
                s_text_page == 0 ? "空格选字" : "空格", &space_act, 0);
    kb_action_t done_act = kb_act(KA_DONE, NULL);
    kb_make_key(s_kb, KB_PAD + w1 + w2 + KB_GAP * 2, bottom_y, w2, KB_H,
                "完成", &done_act, 1);

    lv_obj_set_height(s_kb, bottom_y + KB_H + KB_PAD);
}

/* ── 打开/关闭 ── */
void hmi_kb_open(void)
{
    lv_obj_t* layer = lv_layer_top();
    if (!s_kb) {
        s_kb = lv_obj_create(layer);
        lv_obj_set_style_bg_color(s_kb, KB_BG, 0);
        lv_obj_set_style_bg_opa(s_kb, 255, 0);
        lv_obj_set_style_radius(s_kb, 0, 0);
        lv_obj_set_style_border_width(s_kb, 0, 0);
        lv_obj_set_style_pad_all(s_kb, 0, 0);
        lv_obj_remove_flag(s_kb, LV_OBJ_FLAG_SCROLLABLE);
        lv_obj_remove_flag(s_kb, LV_OBJ_FLAG_CLICK_FOCUSABLE);
    }
    int W = kb_w();
    lv_obj_set_width(s_kb, W);
    lv_obj_align(s_kb, LV_ALIGN_BOTTOM_MID, 0, 0);
    kb_render();
    lv_obj_remove_flag(s_kb, LV_OBJ_FLAG_HIDDEN);
    lv_obj_move_to_index(s_kb, -1);   /* 置顶（= move_foreground，9.3 前无该 API） */
    HMI_LOG("kb open mode=%s", s_mode == KB_MODE_NUM ? "num" : "text");
}

void hmi_kb_close(void)
{
    if (s_kb) lv_obj_add_flag(s_kb, LV_OBJ_FLAG_HIDDEN);
}

/* 调试：向焦点输入框插入文本（远程验证输入路径/中文渲染） */
void hmi_kb_debug_insert(const char* text)
{
    if (!s_focus_ta) hmi_kb_debug_open();
    if (s_focus_ta) lv_textarea_add_text(s_focus_ta, text ? text : "");
    HMI_LOG("kb debug insert: %s", text ? text : "");
}

/* 调试：模拟拼音输入全流程（n、i 键 → 候选 → 选首候选），验证候选表/查表/上屏 */
void hmi_kb_debug_pinyin(void)
{
    if (!s_focus_ta) hmi_kb_debug_open();
    s_mode = KB_MODE_TEXT;
    s_text_page = 0;
    s_py_len = 0; s_py_buf[0] = '\0';
    s_py_buf[s_py_len++] = 'n'; s_py_buf[s_py_len] = '\0';
    kb_update_candidates();
    HMI_LOG("kb pinyin 'n' candidates: %s", s_cands);
    s_py_buf[s_py_len++] = 'i'; s_py_buf[s_py_len] = '\0';
    kb_update_candidates();
    HMI_LOG("kb pinyin 'ni' candidates: %s", s_cands);
    const char* one = cand_at(0);
    if (one) {
        char buf[5];
        int ulen = utf8_len((uint8_t)one[0]);
        memcpy(buf, one, (size_t)ulen);
        buf[ulen] = '\0';
        kb_insert(buf);
        HMI_LOG("kb pinyin selected: %s", buf);
    }
    s_py_len = 0; s_py_buf[0] = '\0';
    kb_update_candidates();
}

/* 调试：SIGUSR1 触发 —— 强制弹键盘（无输入框时创建演示输入框；可指定模式） */
void hmi_kb_debug_open_mode(const char* mode)
{
    if (s_focus_ta) {
        s_mode = (mode && strcmp(mode, "num") == 0) ? KB_MODE_NUM : KB_MODE_TEXT;
        hmi_kb_open();
        return;
    }
    lv_obj_t* layer = lv_layer_top();
    lv_obj_t* ta = lv_textarea_create(layer);
    lv_textarea_set_placeholder_text(ta, "测试输入");
    lv_obj_set_pos(ta, 200, 40);
    lv_obj_set_size(ta, 600, 60);
    lv_obj_set_style_text_font(ta, kb_font(), 0);
    lv_obj_set_style_bg_color(ta, KB_KEY, 0);
    lv_obj_set_style_bg_opa(ta, 255, 0);
    lv_obj_add_flag(ta, LV_OBJ_FLAG_CLICK_FOCUSABLE);
    lv_obj_add_event_cb(ta, kb_ta_focus_cb, LV_EVENT_FOCUSED, NULL);
    lv_obj_add_event_cb(ta, kb_ta_defocus_cb, LV_EVENT_DEFOCUSED, NULL);
    lv_obj_add_event_cb(ta, kb_ta_delete_cb, LV_EVENT_DELETE, NULL);
    hmi_set_ta_keyboard_mode(ta, mode && strcmp(mode, "num") == 0 ? "num" : "text");
    s_focus_ta = ta;
    s_mode = kb_mode_of(ta);
    s_py_len = 0; s_py_buf[0] = '\0';
    s_text_page = 0;
    hmi_kb_open();
}

void hmi_kb_debug_open(void)
{
    hmi_kb_debug_open_mode("text");
}
