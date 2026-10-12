/*
 * host_main.c — PC 端系统设置布局测试（SDL2 显示 + 鼠标输入）
 *
 * 复用板子同一套 settings/*.c 源码，在 PC 上以 800x480 SDL 窗口可视化
 * 验证布局效果。系统命令由 host_sys_cmd.c 提供模拟数据（IP/容量/序列号等）。
 *
 * 操作：
 *   鼠标             点击交互（等同触摸）
 *   数字键 1~9       模拟点击对应 tab（时间/网络/串口/信息/显示/存储/维护/声音/触摸）
 *   F12              保存当前画面到 /tmp/hmi_settings.bmp
 *   ESC / 关窗       退出
 * 环境变量 HMI_AUTO=1：自动遍历 9 个 tab，各保存一张截图到 /tmp/hmi_tabN.bmp 后退出。
 */
#include "lvgl.h"
#include "sys_settings.h"
#include "hmi_runtime.h"
#include <SDL.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/time.h>

#define SCR_W 800
#define SCR_H 480

/* 中文字体文件路径：
 * 使用与固件一致的裁剪字体（从 Noto Sans CJK 子集化，包含全部设置界面字符）。
 * 原 fangsong.ttf 是残缺字体（20902 个 CJK 字符仅 158 个有轮廓），会导致方框。
 *
 * 路径由构建系统注入（Makefile: -DCN_FONT_PATH="$(CURDIR)/fangsong.ttf"）。
 * 不硬编码开发机家目录 —— 否则在他人机器/CI 上必然加载失败，静默退化成
 * 内置位图字体（见 cn_font_init 的 fallback），host 测试就不再覆盖真实的
 * FreeType 渲染路径。
 */
#ifndef CN_FONT_PATH
#define CN_FONT_PATH "fangsong.ttf"   /* 兜底：相对运行目录 */
#endif

/* LVGL 内置思源黑体 CJK 位图字体（fallback 用） */
LV_FONT_DECLARE(lv_font_source_han_sans_sc_14_cjk);
LV_FONT_DECLARE(lv_font_source_han_sans_sc_16_cjk);

/* 中文字体（freetype 渲染，与板子 main.c 一致） */
lv_font_t* g_font_cn_12 = NULL;
lv_font_t* g_font_cn_14 = NULL;
lv_font_t* g_font_cn_16 = NULL;
lv_font_t* g_font_cn_20 = NULL;

static SDL_Window*   g_win;
static SDL_Renderer* g_ren;
static SDL_Texture*  g_tex;
static int g_mx = SCR_W / 2, g_my = SCR_H / 2, g_mdown = 0;
static int g_release_after = 0;   /* 模拟点击：按下保持 N 帧后释放 */
static int g_done = 0;
static uint8_t g_frame[SCR_W * SCR_H * 4];   /* LVGL 最后一帧缓存（截图用） */

uint32_t custom_tick_get(void)
{
    static uint64_t start_ms = 0;
    if (start_ms == 0) {
        struct timeval tv;
        gettimeofday(&tv, NULL);
        start_ms = (uint64_t)tv.tv_sec * 1000 + tv.tv_usec / 1000;
    }
    struct timeval tv;
    gettimeofday(&tv, NULL);
    uint64_t now_ms = (uint64_t)tv.tv_sec * 1000 + tv.tv_usec / 1000;
    return (uint32_t)(now_ms - start_ms);
}

static lv_font_t* cn_font_create(uint32_t size)
{
    lv_font_t* f = lv_freetype_font_create(CN_FONT_PATH,
        LV_FREETYPE_FONT_RENDER_MODE_BITMAP, size, LV_FREETYPE_FONT_STYLE_NORMAL);
    if (!f) fprintf(stderr, "[host] cn font(size=%u) load failed: %s\n", (unsigned)size, CN_FONT_PATH);
    return f;
}

static void cn_font_init(void)
{
    /* 与板子 main.c 一致：freetype 加载 fangsong.ttf 渲染完整中文 */
    lv_freetype_init(256);

    /* 回退项为什么要显式去 const：LVGL 内置字体是 **const**
     * （`LV_FONT_DECLARE` 展开为 `extern const lv_font_t <name>;`，见 lv_font.h），
     * 而 g_font_cn_* 的类型是**非 const** 的 `lv_font_t*` —— 这不是本文件的疏忽：
     * 这四个全局同时被**设备侧** `app/main.c` 定义、并被共享头 `sys_settings.h`
     * 与 `settings_ui.h` 的 `set_ta_font(lv_obj_t*, lv_font_t*)` 使用，
     * 设备上它们装的是 FreeType 动态创建的可变字体对象。
     * 把整条链改成 const 要动 7 个固件文件（含设备运行时）去换 4 条 **host 工具**警告，
     * 不成比例；而这里的转换是安全的——这些指针只被喂给 LVGL 的只读字体 API
     * （`lv_obj_set_style_text_font` / 只做判空与比较），从不经它们写入。
     * 显式转换只为消掉 -Wdiscarded-qualifiers，避免真警告被这 4 条噪音淹没。 */
    g_font_cn_12 = cn_font_create(12);  if (!g_font_cn_12) g_font_cn_12 = (lv_font_t*)&lv_font_source_han_sans_sc_14_cjk;
    g_font_cn_14 = cn_font_create(14);  if (!g_font_cn_14) g_font_cn_14 = (lv_font_t*)&lv_font_source_han_sans_sc_14_cjk;
    g_font_cn_16 = cn_font_create(16);  if (!g_font_cn_16) g_font_cn_16 = (lv_font_t*)&lv_font_source_han_sans_sc_16_cjk;
    g_font_cn_20 = cn_font_create(20);  if (!g_font_cn_20) g_font_cn_20 = (lv_font_t*)&lv_font_source_han_sans_sc_16_cjk;
    fprintf(stderr, "[host] cn font: freetype %s (fallback to bitmap if failed)\n", CN_FONT_PATH);
}

/* ── LVGL 显示 flush：按 area 从整屏 draw buffer 取数据更新 ──
 * LVGL 9 的 flush_cb 传入的 px_map 是「整屏 draw buffer」起始地址，
 * area 是本次要刷新的屏幕区域（refreshed_area）。必须按
 *   src = px + (area->y1 * stride + area->x1) * bpp
 * 定位到 area 对应的像素（行跨距 = 整屏 stride = SCR_W*4，
 * LV_DRAW_BUF_STRIDE_ALIGN=1 无额外对齐）。
 * 若直接整屏拷贝 px 或忽略 area 偏移，切换 tab（部分区域刷新）后会错位。 */
/* 调试：递归 invalid 所有子对象 */
static void invalid_all(lv_obj_t* o)
{
    lv_obj_invalidate(o);
    uint32_t n = lv_obj_get_child_count(o);
    for (uint32_t i = 0; i < n; i++) invalid_all(lv_obj_get_child(o, i));
}

static void sdl_flush_cb(lv_display_t* disp, const lv_area_t* area, uint8_t* px)
{
    int32_t w = lv_area_get_width(area);
    int32_t h = lv_area_get_height(area);
    /* LVGL 9 的 flush_cb 传入 px_map = 整屏 draw buffer 起始地址，area 为
     * 本次刷新区域；按 area 偏移取数据（行跨距 = 整屏 stride = SCR_W*4，
     * LV_DRAW_BUF_STRIDE_ALIGN=1 无额外对齐）。 */
    const uint8_t* src = px + ((int64_t)area->y1 * SCR_W + area->x1) * 4;
    SDL_Rect r = { area->x1, area->y1, w, h };
    SDL_UpdateTexture(g_tex, &r, src, SCR_W * 4);
    for (int32_t y = 0; y < h; y++) {
        memcpy(g_frame + ((area->y1 + y) * SCR_W + area->x1) * 4,
               src + y * SCR_W * 4, (size_t)w * 4);
    }
    SDL_RenderClear(g_ren);
    SDL_RenderCopy(g_ren, g_tex, NULL, NULL);
    SDL_RenderPresent(g_ren);
    lv_display_flush_ready(disp);
}

/* ── LVGL 输入：鼠标位置/状态（等同触摸） ── */
static void sdl_mouse_read_cb(lv_indev_t* drv, lv_indev_data_t* d)
{
    (void)drv;
    d->point.x = g_mx;
    d->point.y = g_my;
    d->state = g_mdown ? LV_INDEV_STATE_PRESSED : LV_INDEV_STATE_RELEASED;
}

/* tab 按钮坐标（与 sys_settings.c 布局公式一致），模拟点击 */
static void sim_tap_tab(int idx)
{
    if (idx < 0 || idx > 8) return;
    const int row1 = 5, gap = 8, pad_l = 10, tabbar_y = 44;
    int btn_w = (SCR_W - 20 - (row1 - 1) * gap) / row1;   /* 149 */
    int btn_h = (92 - 12 - gap) / 2;                       /* 36 */
    int row = idx < row1 ? 0 : 1;
    int col = idx < row1 ? idx : idx - row1;
    g_mx = pad_l + col * (btn_w + gap) + btn_w / 2;
    g_my = tabbar_y + 6 + row * (btn_h + gap) + btn_h / 2;
    g_mdown = 1;
    g_release_after = 3;   /* 保持 3 帧后释放，确保 LVGL 判定为 CLICKED */
}

static void save_snapshot(const char* path)
{
    /* 直接从 LVGL 帧缓存生成 BMP（保证是完整 UI 帧，不受 SDL 渲染时序影响） */
    SDL_Surface* s = SDL_CreateRGBSurfaceFrom(g_frame, SCR_W, SCR_H, 32, SCR_W * 4,
                                              0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000);
    if (!s) {
        fprintf(stderr, "[host] snapshot surface fail\n");
        return;
    }
    SDL_SaveBMP(s, path);
    SDL_FreeSurface(s);
    printf("[host] snapshot -> %s\n", path);
}

/* 调试：转储对象树（类型/位置/尺寸/文本），定位布局问题 */
static const char* obj_cls_name(lv_obj_t* c)
{
    if (lv_obj_check_type(c, &lv_label_class))     return "lv_label";
    if (lv_obj_check_type(c, &lv_button_class))    return "lv_button";
    if (lv_obj_check_type(c, &lv_dropdown_class))  return "lv_dropdown";
    if (lv_obj_check_type(c, &lv_textarea_class))  return "lv_textarea";
    if (lv_obj_check_type(c, &lv_spinbox_class))   return "lv_spinbox";
    if (lv_obj_check_type(c, &lv_switch_class))    return "lv_switch";
    if (lv_obj_check_type(c, &lv_slider_class))    return "lv_slider";
    if (lv_obj_check_type(c, &lv_keyboard_class))  return "lv_keyboard";
    if (lv_obj_check_type(c, &lv_msgbox_class))    return "lv_msgbox";
    if (lv_obj_check_type(c, &lv_obj_class))       return "lv_obj";
    return "?";
}

static void dump_objs(lv_obj_t* o, int d)
{
    uint32_t n = lv_obj_get_child_count(o);
    for (uint32_t i = 0; i < n; i++) {
        lv_obj_t* c = lv_obj_get_child(o, i);
        int32_t x = lv_obj_get_x(c), y = lv_obj_get_y(c);
        int32_t w = lv_obj_get_width(c), h = lv_obj_get_height(c);
        /* 屏幕绝对坐标（累加父链） */
        int32_t ax = x, ay = y;
        lv_obj_t* p = lv_obj_get_parent(c);
        while (p) { ax += lv_obj_get_x(p); ay += lv_obj_get_y(p); p = lv_obj_get_parent(p); }
        const char* txt = "";
        if (lv_obj_check_type(c, &lv_label_class)) txt = lv_label_get_text(c);
        const char* hid = lv_obj_has_flag(c, LV_OBJ_FLAG_HIDDEN) ? " HIDDEN" : "";
        int opa = (int)lv_obj_get_style_opa(c, LV_PART_MAIN);
        lv_area_t crd;
        lv_obj_get_coords(c, &crd);
        fprintf(stderr, "%*s[%s]rel(%d,%d)abs(%d,%d)%dx%d coords(%d,%d,%d,%d)%s opa=%d %s\n", d * 2, "",
                obj_cls_name(c), (int)x, (int)y, (int)ax, (int)ay, (int)w, (int)h,
                (int)crd.x1, (int)crd.y1, (int)crd.x2, (int)crd.y2,
                hid, opa, txt);
        dump_objs(c, d + 1);
    }
}

static void pump_sdl_events(void)
{
    SDL_Event e;
    while (SDL_PollEvent(&e)) {
        switch (e.type) {
        case SDL_QUIT:
            g_done = 1;
            break;
        case SDL_KEYDOWN:
            if (e.key.keysym.sym == SDLK_ESCAPE) g_done = 1;
            else if (e.key.keysym.sym >= SDLK_1 && e.key.keysym.sym <= SDLK_9)
                sim_tap_tab(e.key.keysym.sym - SDLK_1);
            else if (e.key.keysym.sym == SDLK_F12)
                save_snapshot("/tmp/hmi_settings.bmp");
            break;
        case SDL_KEYUP:
            if (e.key.keysym.sym >= SDLK_1 && e.key.keysym.sym <= SDLK_9)
                g_mdown = 0;
            break;
        case SDL_MOUSEMOTION:
            g_mx = e.motion.x;
            g_my = e.motion.y;
            break;
        case SDL_MOUSEBUTTONDOWN:
            g_mx = e.button.x;
            g_my = e.button.y;
            g_mdown = 1;
            break;
        case SDL_MOUSEBUTTONUP:
            g_mdown = 0;
            break;
        default:
            break;
        }
    }
}

static void lv_step(void)
{
    if (g_release_after > 0 && --g_release_after == 0) g_mdown = 0;
    lv_timer_handler();
    /* 与板子主循环对齐（app/main.c: sys_settings_boot_poll）。
     * host 侧从不调用 sys_settings_boot_start()，而 s_boot_done 初值为 1
     * （"初始视为已完成（未启动）"，见 sys_settings.c），故这里恒为空操作；
     * 保留是为了让本工具的主循环结构与固件保持一致。
     * 注：此处曾写作 sys_settings_gate_poll() —— 该函数在 app/settings 中
     * 已不存在（重命名为 boot_poll），导致本工具长期编译不过。 */
    sys_settings_boot_poll();
}

int main(void)
{
    if (SDL_Init(SDL_INIT_VIDEO) != 0) {
        fprintf(stderr, "[host] SDL_Init fail: %s\n", SDL_GetError());
        return 1;
    }
    g_win = SDL_CreateWindow("HMI Settings Layout Test (800x480)",
                             SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED,
                             SCR_W, SCR_H, 0);
    if (!g_win) { fprintf(stderr, "[host] window fail\n"); return 1; }
    g_ren = SDL_CreateRenderer(g_win, -1, SDL_RENDERER_ACCELERATED);
    g_tex = SDL_CreateTexture(g_ren, SDL_PIXELFORMAT_ARGB8888,
                              SDL_TEXTUREACCESS_STREAMING, SCR_W, SCR_H);

    /* ── LVGL 初始化 ── */
    lv_init();
    lv_tick_set_cb(custom_tick_get);
    cn_font_init();

    lv_display_t* disp = lv_display_create(SCR_W, SCR_H);
    lv_draw_buf_t* buf = lv_draw_buf_create(SCR_W, SCR_H, LV_COLOR_FORMAT_NATIVE,
                                            LV_STRIDE_AUTO);
    if (!buf) {
        fprintf(stderr, "[host] draw buf fail\n");
        return 1;
    }
    lv_display_set_draw_buffers(disp, buf, NULL);
    lv_display_set_flush_cb(disp, sdl_flush_cb);
    lv_display_set_render_mode(disp, LV_DISPLAY_RENDER_MODE_FULL);

    lv_indev_t* indev = lv_indev_create();
    lv_indev_set_type(indev, LV_INDEV_TYPE_POINTER);
    lv_indev_set_read_cb(indev, sdl_mouse_read_cb);

    sys_settings_show();   /* 直接进入系统设置（板子上为开机多次点击触发） */

    /* ── 最小化实验：隔离 label 渲染根因（HMI_TEST=1 触发） ── */
    if (getenv("HMI_TEST")) {
        lv_obj_t* scr = lv_screen_active();
        lv_obj_clean(scr);
        /* A: text 先设、font 后设（= set_time.c make_field 字段名模式） */
        lv_obj_t* a = lv_label_create(scr);
        lv_label_set_text(a, "A字段");
        lv_obj_set_style_text_font(a, g_font_cn_16, 0);
        lv_obj_align(a, LV_ALIGN_TOP_LEFT, 20, 20);
        /* B: font 先设、text 后设（= 当前时间模式） */
        lv_obj_t* b = lv_label_create(scr);
        lv_obj_set_style_text_font(b, g_font_cn_16, 0);
        lv_label_set_text(b, "B时间");
        lv_obj_align(b, LV_ALIGN_TOP_LEFT, 20, 60);
        /* C: 仅 text（默认字体） */
        lv_obj_t* c = lv_label_create(scr);
        lv_label_set_text(c, "C默认");
        lv_obj_align(c, LV_ALIGN_TOP_LEFT, 20, 100);
        /* D: text → font → 再 set_text（二次设置） */
        lv_obj_t* d = lv_label_create(scr);
        lv_label_set_text(d, "D首次");
        lv_obj_set_style_text_font(d, g_font_cn_16, 0);
        lv_label_set_text(d, "D二次");
        lv_obj_align(d, LV_ALIGN_TOP_LEFT, 20, 140);
        /* E: font → align → text（当前时间精确顺序：align 在 text 前） */
        lv_obj_t* e = lv_label_create(scr);
        lv_obj_set_style_text_font(e, g_font_cn_16, 0);
        lv_obj_align(e, LV_ALIGN_TOP_LEFT, 20, 180);
        lv_label_set_text(e, "E时间");
        /* F: text → align → font（align 后改 font） */
        lv_obj_t* f = lv_label_create(scr);
        lv_label_set_text(f, "F文字");
        lv_obj_align(f, LV_ALIGN_TOP_LEFT, 20, 220);
        lv_obj_set_style_text_font(f, g_font_cn_16, 0);
        for (int fr = 0; fr < 40; fr++) { lv_step(); SDL_Delay(16); }
        save_snapshot("/tmp/hmi_test.bmp");
        /* G: 全新屏幕 + 简单 label（排除 clean 干扰） */
        lv_obj_t* scr2 = lv_obj_create(NULL);
        lv_obj_set_style_bg_color(scr2, lv_color_hex(0x22252b), 0);
        lv_obj_clear_flag(scr2, LV_OBJ_FLAG_SCROLLABLE);
        lv_screen_load(scr2);
        lv_obj_t* g1 = lv_label_create(scr2);
        lv_label_set_text(g1, "G1年");
        lv_obj_set_style_text_font(g1, g_font_cn_16, 0);
        lv_obj_align(g1, LV_ALIGN_TOP_LEFT, 20, 20);
        lv_obj_t* g2 = lv_label_create(scr2);
        lv_obj_set_style_text_font(g2, g_font_cn_16, 0);
        lv_label_set_text(g2, "G2年");
        lv_obj_align(g2, LV_ALIGN_TOP_LEFT, 20, 60);
        lv_obj_t* g3 = lv_label_create(scr2);
        lv_label_set_text(g3, "G3年");
        lv_obj_align(g3, LV_ALIGN_TOP_LEFT, 20, 100);
        for (int fr = 0; fr < 40; fr++) { lv_step(); SDL_Delay(16); }
        save_snapshot("/tmp/hmi_test2.bmp");
        fprintf(stderr, "===== HMI_TEST2 dump =====\n");
        dump_objs(scr2, 0);
        g_done = 1;
    }

    /* ── dropdown 展开测试：HMI_DD=1 进入 tab 后点击 dropdown 展开截图 ── */
    if (getenv("HMI_DD")) {
        const char* only = getenv("HMI_TAB");
        int t = only ? atoi(only) : 1;   /* 默认网络页 */
        sim_tap_tab(t);
        for (int f = 0; f < 40 && !g_done; f++) { pump_sdl_events(); lv_step(); SDL_Delay(16); }
        /* 点击第一个 dropdown（网络页 s_mode_dd 在 abs(170,136+32+19)=187 附近） */
        g_mx = 200; g_my = 187; g_mdown = 1; g_release_after = 3;
        for (int f = 0; f < 30 && !g_done; f++) { pump_sdl_events(); lv_step(); SDL_Delay(16); }
        fprintf(stderr, "===== DROPDOWN EXPANDED DUMP =====\n");
        dump_objs(lv_screen_active(), 0);
        /* 关闭 dropdown：点击别处 */
        g_mx = 400; g_my = 300; g_mdown = 1; g_release_after = 3;
        for (int f = 0; f < 30 && !g_done; f++) { pump_sdl_events(); lv_step(); SDL_Delay(16); }
        /* 显示页 dropdown */
        sim_tap_tab(4);
        for (int f = 0; f < 40 && !g_done; f++) { pump_sdl_events(); lv_step(); SDL_Delay(16); }
        g_mx = 200; g_my = 227; g_mdown = 1; g_release_after = 3;
        for (int f = 0; f < 30 && !g_done; f++) { pump_sdl_events(); lv_step(); SDL_Delay(16); }
        save_snapshot("/tmp/hmi_dd_disp.bmp");
        g_done = 1;
    }

    /* ── 自动模式：遍历 9 个 tab 截图后退出；HMI_TAB=N 只测第 N 个 ──
     * HMI_COMPARE=1：每个 tab 先截"原始状态"再 update_layout 后截"修复状态"对比 */
    if (getenv("HMI_AUTO")) {
        const char* only = getenv("HMI_TAB");
        int start = only ? atoi(only) : 0;
        int end = only ? atoi(only) + 1 : 9;
        for (int t = start; t < end && !g_done; t++) {
            sim_tap_tab(t);
            for (int f = 0; f < 40 && !g_done; f++) {
                pump_sdl_events();
                lv_step();
                SDL_Delay(16);
            }
            char path[64];
            if (getenv("HMI_COMPARE")) {
                /* 原始状态截图（无 update_layout） */
                snprintf(path, sizeof(path), "/tmp/hmi_tab%d_raw.bmp", t);
                save_snapshot(path);
                if (getenv("HMI_DUMP")) {
                    fprintf(stderr, "===== TAB %d RAW =====\n", t);
                    dump_objs(lv_screen_active(), 0);
                }
                /* update_layout 后截图 */
                lv_obj_update_layout(lv_screen_active());
                invalid_all(lv_screen_active());
                for (int f = 0; f < 10; f++) { lv_step(); SDL_Delay(16); }
                snprintf(path, sizeof(path), "/tmp/hmi_tab%d_fix.bmp", t);
                save_snapshot(path);
            } else {
                if (getenv("HMI_DUMP")) {
                    fprintf(stderr, "===== TAB %d =====\n", t);
                    dump_objs(lv_screen_active(), 0);
                }
                /* 实验：强制布局 + 递归逐个 invalid 所有子对象 */
                lv_obj_update_layout(lv_screen_active());
                invalid_all(lv_screen_active());
                for (int f = 0; f < 10; f++) { lv_step(); SDL_Delay(16); }
                snprintf(path, sizeof(path), "/tmp/hmi_tab%d.bmp", t);
                save_snapshot(path);
            }
        }
        g_done = 1;
    }

    /* ── 交互主循环 ── */
    while (!g_done) {
        pump_sdl_events();
        lv_step();
        SDL_Delay(5);
    }

    lv_draw_buf_destroy(buf);
    SDL_DestroyTexture(g_tex);
    SDL_DestroyRenderer(g_ren);
    SDL_DestroyWindow(g_win);
    SDL_Quit();
    return 0;
}
