/*
 * screen_manager.c — 屏幕管理实现（.so dlopen 热加载版本）
 *
 * 加载顺序：
 *   1. /mnt/UDISK/hmi/libhmi_ui.so  (OTA 升级)
 *   2. /usr/lib/hmi/libhmi_ui.so    (出厂 fallback)
 *   两个都失败：打印 FATAL 日志但不崩溃，通信线程继续运行
 */
#include "screen_manager.h"
#include "hmi_ui_api.h"
#include "hmi_runtime.h"   /* HMI_LOG */
#include "soft_keyboard.h" /* 软键盘接管（ABI V3） */
#include <dlfcn.h>
#include <stdlib.h>
#include <string.h>

/* 宿主 CJK 字体（main.c 初始化；仅作修复回退，正常走 hmi_resolve_font 工程字体） */
extern lv_font_t* g_font_cn_16;

#define HMI_MAX_SCREENS 64
#define HMI_MAX_LAYERS  32
#define HMI_LAYER_REF_MAX 128   /* 图层引用表最大条目数 */

/* ── dlopen 句柄与 API 表 ── */
static void* s_ui_handle = NULL;
static const hmi_ui_api_t* s_ui_api = NULL;

/* 屏幕实例与注册表（从 .so get_screens 返回） */
static lv_obj_t* s_screens[HMI_MAX_SCREENS];
static int s_current = -1;
static int s_screen_count = 0;
static const screen_def_t* s_screen_defs = NULL;

/* 共享图层（公共页面作为背景图层）：每个唯一图层仅创建一份 detached 实例，
 * 随屏幕切换转移挂载（hmi_screen_apply_layers）。 */
static const screen_layer_ref_t* s_layer_defs = NULL;
static int s_layer_def_count = 0;
static lv_obj_t* s_layers[HMI_MAX_LAYERS];
static const char* s_layer_ids[HMI_MAX_LAYERS];
static int s_layer_count = 0;

/* ── 加载路径 ── */
#define HMI_UI_OTA_PATH   "/mnt/UDISK/hmi/libhmi_ui.so"
#define HMI_UI_SYS_PATH   "/usr/lib/hmi/libhmi_ui.so"

static int try_load(const char* path)
{
    void* h = dlopen(path, RTLD_NOW | RTLD_GLOBAL);
    if (!h) {
        HMI_LOG("dlopen(%s) fail: %s", path, dlerror());
        return -1;
    }
    const hmi_ui_api_t* (*get_api)(void) =
        (const hmi_ui_api_t* (*)(void))dlsym(h, "hmi_ui_get_api");
    if (!get_api) {
        HMI_LOG("dlsym(hmi_ui_get_api) fail in %s: %s", path, dlerror());
        dlclose(h);
        return -1;
    }
    const hmi_ui_api_t* api = get_api();
    if (!api) {
        HMI_LOG("hmi_ui_get_api returned NULL in %s", path);
        dlclose(h);
        return -1;
    }
    if (api->abi_version != HMI_UI_ABI_VERSION) {
        HMI_LOG("%s: ABI mismatch (so=%u host=%u)",
                path, (unsigned)api->abi_version, (unsigned)HMI_UI_ABI_VERSION);
        dlclose(h);
        return -1;
    }
    if (api->lvgl_version != HMI_UI_LVGL_VERSION) {
        HMI_LOG("%s: LVGL mismatch (so=0x%x host=0x%x)",
                path, (unsigned)api->lvgl_version, (unsigned)HMI_UI_LVGL_VERSION);
        dlclose(h);
        return -1;
    }

    int n = 0;
    if (api->get_screens) (void)api->get_screens(&n);
    s_ui_handle = h;
    s_ui_api = api;
    HMI_LOG("UI loaded from %s (screens=%d, ui_ver=%u, build=%s)",
            path, n, (unsigned)(api->ui_version),
            api->build_id ? api->build_id : "(null)");
    return 0;
}

int hmi_ui_load(void)
{
    if (try_load(HMI_UI_OTA_PATH) == 0) goto success;
    if (try_load(HMI_UI_SYS_PATH) == 0) goto success;
    HMI_LOG("FATAL: system .so load failed, UI unavailable");
    return -1;

success:
    /* 图层引用表（公共页面作为背景图层）：旧版 .so 无此符号时禁用图层转移 */
    s_layer_defs = NULL;
    s_layer_def_count = 0;
    if (s_ui_handle) {
        const screen_layer_ref_t* (*get_layers)(int*) =
            (const screen_layer_ref_t* (*)(int*))dlsym(s_ui_handle, "hmi_ui_get_layers");
        if (get_layers) {
            s_layer_defs = get_layers(&s_layer_def_count);
        }
    }

    /* ABI V3：采纳 .so 标签表（元数据 + 绑定）驱动设备层 ——
     * 缓存/只读保护/轮询组/MQTT 主题全部按工程标签重建，替换 .so 即替换标签表。
     * 在 api->init 之前采纳（.so 初始化可能读标签）。 */
    if (s_ui_api && s_ui_api->get_tags && s_ui_api->get_tag_bindings) {
        int tn = 0, bn = 0;
        const tag_meta_t* metas = s_ui_api->get_tags(&tn);
        const tag_binding_t* binds = s_ui_api->get_tag_bindings(&bn);
        if (tn < 0) tn = 0;
        if (bn < 0) bn = 0;
        int cnt = tn < bn ? tn : bn;
        hmi_tags_adopt(metas, binds, cnt);
    }

    if (s_ui_api && s_ui_api->init) {
        s_ui_api->init(NULL);
    }
    return 0;
}

void hmi_ui_unload(void)
{
    /* 先销毁本模块创建的所有屏幕 LVGL 对象，再 dlclose。
     * 顺序必须正确：lv_obj_delete 只操作宿主堆上的 LVGL 节点，
     * 不回调 .so 内代码，因此需在 dlclose 前完成，否则旧一代屏幕对象
     * 会在每次 OTA 热加载时累积泄漏。 */
    for (int i = 0; i < HMI_MAX_SCREENS; i++) {
        if (s_screens[i]) {
            lv_obj_delete(s_screens[i]);
            s_screens[i] = NULL;
        }
    }
    if (s_current >= 0 && s_current < HMI_MAX_SCREENS) {
        s_screens[s_current] = NULL;
    }

    /* 销毁共享图层对象（公共页面作为背景图层） */
    for (int i = 0; i < s_layer_count; i++) {
        if (s_layers[i]) {
            lv_obj_delete(s_layers[i]);
            s_layers[i] = NULL;
        }
    }
    s_layer_count = 0;
    s_layer_defs = NULL;
    s_layer_def_count = 0;

    if (s_ui_api && s_ui_api->teardown) {
        s_ui_api->teardown();
    }
    if (s_ui_handle) {
        dlclose(s_ui_handle);
    }
    s_ui_handle = NULL;
    s_ui_api = NULL;
    s_screen_defs = NULL;
    s_screen_count = 0;
    s_current = -1;
}

const hmi_ui_api_t* hmi_ui_get_current_api(void)
{
    return s_ui_api;
}

/* ── 屏幕创建 / 切换 ── */

/* 将当前屏幕引用的共享图层挂载到目标屏幕（置于背景层），并隐藏其余图层。
 * 切换前图层可能挂在其他屏幕上，lv_obj_set_parent 会自动从旧 parent 移除。 */
static void hmi_screen_apply_layers(int index)
{
    if (!s_screen_defs || index < 0 || index >= s_screen_count) return;
    lv_obj_t* target = s_screens[index];
    if (!target) return;
    const char* screen_id = s_screen_defs[index].id;
    if (!screen_id) return;

    /* 先隐藏全部共享图层 */
    for (int j = 0; j < s_layer_count; j++) {
        if (s_layers[j]) lv_obj_add_flag(s_layers[j], LV_OBJ_FLAG_HIDDEN);
    }

    /* 挂载当前屏幕引用的图层 */
    for (int i = 0; i < s_layer_def_count && i < HMI_LAYER_REF_MAX; i++) {
        const screen_layer_ref_t* ref = &s_layer_defs[i];
        if (!ref || !ref->screen_id || !ref->layer_id) continue;
        if (strcmp(ref->screen_id, screen_id) != 0) continue;
        for (int j = 0; j < s_layer_count; j++) {
            if (s_layers[j] && s_layer_ids[j] && strcmp(s_layer_ids[j], ref->layer_id) == 0) {
                lv_obj_set_parent(s_layers[j], target);
                lv_obj_remove_flag(s_layers[j], LV_OBJ_FLAG_HIDDEN);
                lv_obj_move_background(s_layers[j]);
                break;
            }
        }
    }
}

void hmi_screens_init(void)
{
    if (!s_ui_api || !s_ui_api->get_screens) return;

    int n = 0;
    s_screen_defs = s_ui_api->get_screens(&n);
    if (!s_screen_defs || n <= 0) {
        HMI_LOG("get_screens returned empty");
        return;
    }
    if (n > HMI_MAX_SCREENS) n = HMI_MAX_SCREENS;
    s_screen_count = n;

    for (int i = 0; i < n; i++) {
        if (s_screen_defs[i].create) {
            s_screens[i] = s_screen_defs[i].create();
        }
        /* ABI V3：接管屏幕内全部 TextArea 输入（焦点回调 + 软键盘） */
        if (s_screens[i]) hmi_kb_attach(s_screens[i]);
        /* 主题可继承默认（文本色/字体）：按屏调用 init 直接应用到工程屏。
         * 初始化时 lv_screen_active() 是 LVGL 默认屏，工程屏幕不继承其样式，
         * 不在每屏应用主题则中文字体回落 montserrat → 全部 tofu 方框。 */
        if (s_screens[i] && s_ui_api && s_ui_api->init) {
            s_ui_api->init(s_screens[i]);
        }
    }

    /* 创建共享图层对象（公共页面作为背景）：每个唯一图层仅创建一份 detached 实例 */
    s_layer_count = 0;
    for (int i = 0; i < s_layer_def_count && i < HMI_LAYER_REF_MAX; i++) {
        const screen_layer_ref_t* ref = &s_layer_defs[i];
        if (!ref || !ref->create || !ref->layer_id) continue;
        int found = -1;
        for (int j = 0; j < s_layer_count; j++) {
            if (s_layer_ids[j] && strcmp(s_layer_ids[j], ref->layer_id) == 0) { found = j; break; }
        }
        if (found >= 0) continue;
        if (s_layer_count >= HMI_MAX_LAYERS) break;
        lv_obj_t* layer = ref->create(NULL);   /* detached 顶层对象 */
        if (!layer) continue;
        lv_obj_add_flag(layer, LV_OBJ_FLAG_HIDDEN);
        s_layers[s_layer_count] = layer;
        s_layer_ids[s_layer_count] = ref->layer_id;
        s_layer_count++;
    }

    if (s_screen_count > 0 && s_screens[0]) {
        s_current = 0;
        hmi_screen_apply_layers(0);
        lv_screen_load(s_screens[0]);
    }
}

int hmi_screen_switch_idx(int index)
{
    if (index < 0 || index >= s_screen_count || index >= HMI_MAX_SCREENS) return -1;
    if (s_screens[index] == NULL) return -1;
    hmi_kb_close();   /* 切屏收起软键盘 */
    s_current = index;
    hmi_screen_apply_layers(index);
    lv_screen_load(s_screens[index]);
    return 0;
}

int hmi_screen_switch(const char* name)
{
    if (!name || !s_screen_defs) return -1;
    for (int i = 0; i < s_screen_count; i++)
        if (s_screen_defs[i].name && strcmp(s_screen_defs[i].name, name) == 0)
            return hmi_screen_switch_idx(i);
    return -1;
}

int hmi_screen_current_idx(void)
{
    return s_current;
}

/* ── 宿主级中文字体修复（2026-08-17 实机：下拉框文字空白/弹出列表方框） ──
 * 背景：.so 的 hmi_ui_apply_theme(scr) 在屏幕创建完成后才把 CJK 字体设到屏幕，
 * 而子控件（下拉框主体、下拉弹出列表）在创建时已把字体解析并缓存为默认
 * montserrat（无中文字形）→ 中文渲染为空白或方框；生成代码对列表设字体时
 * 取的仍是当时的默认字体。LVGL 继承样式缓存不随父级刷新，需显式重设。
 * 修复：屏幕加载后遍历控件树，把仍是默认字体的下拉框主体与其列表显式设为
 * 工程 CJK 字体（hmi_resolve_font 返回与 .so 相同的缓存实例）。项目无关，
 * 仅覆盖默认字体，不破坏工程自设字体。 */
/* 下拉框主题浅色文字（与 .so apply_theme 的屏幕文字色一致；防深色默认主题文字
 * 在深色控件上不可见 —— 下拉框在创建时缓存了默认深色文字色，不随父级继承刷新） */
#define HMI_FIX_TEXT_COLOR 0xe2e8f0

static bool hmi_color_is_dark(lv_color_t c)
{
    /* 亮度启发式：RGB 和低于阈值视为深色（默认主题文字 #212121 → 深） */
    return (uint32_t)c.blue + c.green + c.red < 300;
}

static void hmi_fix_font_tree(lv_obj_t* obj, lv_font_t* f)
{
    if (!obj) return;
    if (lv_obj_has_class(obj, &lv_dropdown_class)) {
        lv_font_t* main_f = lv_obj_get_style_text_font(obj, 0);
        lv_obj_t* list = lv_dropdown_get_list(obj);
        lv_font_t* list_f = list ? lv_obj_get_style_text_font(list, 0) : NULL;
        HMI_LOG("hmi: font repair dd main=%p(default=%d) list=%p(default=%d) -> %p",
                (void*)main_f, main_f == LV_FONT_DEFAULT, (void*)list_f,
                list_f == LV_FONT_DEFAULT, (void*)f);
        if (main_f == LV_FONT_DEFAULT) {
            lv_obj_set_style_text_font(obj, f, 0);
        }
        if (hmi_color_is_dark(lv_obj_get_style_text_color(obj, 0))) {
            lv_obj_set_style_text_color(obj, lv_color_hex(HMI_FIX_TEXT_COLOR), 0);
        }
        if (list) {
            /* 列表字体补设为 CJK（白底列表保持默认深色文字，不改色） */
            lv_obj_set_style_text_font(list, f, 0);
            lv_obj_set_style_text_font(list, f, LV_PART_SELECTED);
        }
    }
    uint32_t n = lv_obj_get_child_count(obj);
    for (uint32_t k = 0; k < n; k++) {
        hmi_fix_font_tree(lv_obj_get_child(obj, k), f);
    }
}

void hmi_screens_fix_fonts(void)
{
    lv_font_t* f = hmi_resolve_font("asset_font_cjk", "asset_font_cjk.ttc", 16);
    if (!f || f == LV_FONT_DEFAULT) f = g_font_cn_16;
    if (!f) return;

    for (int i = 0; i < s_screen_count; i++) {
        if (s_screens[i]) {
            HMI_LOG("hmi: font repair screen[%d]=%p children=%u font=%p", i,
                    (void*)s_screens[i],
                    (unsigned)lv_obj_get_child_count(s_screens[i]),
                    (void*)lv_obj_get_style_text_font(s_screens[i], 0));
            hmi_fix_font_tree(s_screens[i], f);
        }
    }
    for (int i = 0; i < s_layer_count; i++) {
        if (s_layers[i]) hmi_fix_font_tree(s_layers[i], f);
    }
    HMI_LOG("hmi: font repair applied (dropdown CJK)");
}

/* ── 公共页面查询（多用户共享查看 + 权限控制） ── */

int hmi_screen_is_public(int index)
{
    if (!s_screen_defs || index < 0 || index >= s_screen_count) return 0;
    return s_screen_defs[index].is_public ? 1 : 0;
}

int hmi_screen_public_access(int index)
{
    if (!s_screen_defs || index < 0 || index >= s_screen_count) return 0;
    return s_screen_defs[index].public_access;
}
