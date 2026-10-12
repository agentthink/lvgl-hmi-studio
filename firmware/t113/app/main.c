/*
 * main.c — zhishu-HMI T113-S3 运行固件入口（.so 热加载模式）
 *
 * 启动序列：
 *   1. lv_init()                        LVGL 核心
 *   2. lcd_sunxifb_init()               显示引擎
 *   3. touch_evdev_init()               触摸
 *   4. hmi_var_init() / hmi_comm_init() 变量/通信
 *   5. hmi_ui_load()                    dlopen libhmi_ui.so（UDISK → /usr/lib/hmi fallback）
 *   6. hmi_screens_init()               从 .so get_screens() 返回数组创建屏幕
 *   7. 主循环 lv_timer_handler()
 */
#include "hmi_runtime.h"
#include "lcd_sunxifb.h"
#include "touch_evdev.h"
#include "comm_core.h"
#include "hmi_ui_api.h"
#include "hmi_alarm_source.h"   /* hmi_alarm_publish_tick（报警上行，阶段 291） */
#include "sys_settings.h"
#include "soft_keyboard.h"
#include <unistd.h>
#include <time.h>
#include <sys/time.h>
#include <stdlib.h>
#include <signal.h>

/* 中文字体（freetype，系统设置模块使用；创建失败回退内嵌 Noto CJK 子集）
 * 完整简体中文字体：Noto Sans CJK SC（44810 字符，自工程资产 asset_font_cjk.ttc
 * 提取的 SC face）。旧 fangsong.ttf 仅 2699 字子集，缺"主/温/压/文/体"等常见字
 * 导致大量方框（tofu），不再使用。
 *
 * 路径策略（2026-08-14）：设备 root 是 squashfs + tmpfs overlay，写 /usr/lib/hmi
 * 重启即丢；/mnt/UDISK（UBIFS）是唯一持久可写分区。故 UDISK 字体（OTA 可更新）
 * 优先，系统 squashfs 内出厂字体作回退。 */
#define HMI_CN_FONT_PATH_UDISK "/mnt/UDISK/hmi/fonts/font_cjk_sc.ttf"
#define HMI_CN_FONT_PATH_SYS   "/usr/lib/hmi/fonts/font_cjk_sc.ttf"
static const char* cn_font_path(void)
{
    /* 系统出厂字体（15.7MB Noto Sans CJK SC，44810 字符）优先——UDISK（UBIFS）
     * 仅 7.4MB 容量不足以存放完整字体，且子集版缺 hmtx 表致 FT_New_Face 失败。
     * 2026-08-19 修复：方框文字根因=UDISK 子集字体 Hmtx_Table_Missing。 */
    if (access(HMI_CN_FONT_PATH_SYS, F_OK) == 0) return HMI_CN_FONT_PATH_SYS;
    return HMI_CN_FONT_PATH_UDISK;
}
LV_FONT_DECLARE(lv_font_source_han_sans_sc_14_cjk);
LV_FONT_DECLARE(lv_font_source_han_sans_sc_16_cjk);
lv_font_t* g_font_cn_12 = NULL;
lv_font_t* g_font_cn_14 = NULL;
lv_font_t* g_font_cn_16 = NULL;
lv_font_t* g_font_cn_20 = NULL;

/* ── 项目无关字体服务（ABI V3 热更新核心） ──
 * 旧方案：宿主为每个工程资产 ID 预定义 font_<id> 符号（font_asset_font_cjk 等），
 * 换工程后新 .so 引用 font_0c8ebf72 → dlopen(RTLD_NOW) 直接失败。
 * 新方案：.so 通过 hmi_resolve_font(id, file, size) 运行时取字体，
 * 字体文件由 IDE 部署到 /mnt/UDISK/hmi/assets/<file>（OTA 可更新），
 * 任意工程/任意资产 ID 均可热更新，无需重编宿主。 */
#define HMI_FONT_CACHE_MAX 16
typedef struct {
    char id[48];
    char file[64];
    int  size;
    lv_font_t* f;
} hmi_font_entry_t;
static hmi_font_entry_t s_font_cache[HMI_FONT_CACHE_MAX];
static int s_font_cache_count = 0;

static lv_font_t* cn_font_create(uint32_t size)
{
    const char* path = cn_font_path();
    lv_font_t* f = lv_freetype_font_create(path,
        LV_FREETYPE_FONT_RENDER_MODE_BITMAP, size, LV_FREETYPE_FONT_STYLE_NORMAL);
    if (!f) HMI_LOG("cn font(size=%u) load failed: %s", (unsigned)size, path);
    return f;
}

/* 在候选目录中定位字体文件（系统出厂目录优先，UDISK 次之——UDISK 空间不足
 * 容纳完整 CJK 字体，且子集字体可能缺 hmtx 表致 FT_New_Face 失败） */
static int font_file_exists(char* out, size_t outsz, const char* file)
{
    static const char* dirs[] = {
        "/usr/lib/hmi/fonts/",
        "/usr/lib/hmi/assets/",
        "/mnt/UDISK/hmi/fonts/",
        "/mnt/UDISK/hmi/assets/",
    };
    for (size_t i = 0; i < sizeof(dirs)/sizeof(dirs[0]); i++) {
        snprintf(out, outsz, "%s%s", dirs[i], file);
        if (access(out, F_OK) == 0) return 1;
    }
    return 0;
}

/* 工程字体解析：文件缺失回退设备 CJK 字体；创建失败回退宿主 CJK 回退字体（永不 NULL） */
lv_font_t* hmi_resolve_font(const char* font_id, const char* font_file, int size)
{
    if (size <= 0) size = 16;
    (void)font_id;

    for (int i = 0; i < s_font_cache_count; i++) {
        hmi_font_entry_t* e = &s_font_cache[i];
        if (e->size == size && strcmp(e->id, font_id ? font_id : "") == 0)
            return e->f;
    }

    char path[128];
    int found = 0;
    if (font_file && font_file[0])
        found = font_file_exists(path, sizeof(path), font_file);
    if (!found)
        found = font_file_exists(path, sizeof(path), "font_cjk_sc.ttf");

    lv_font_t* f = NULL;
    if (found)
        f = lv_freetype_font_create(path,
            LV_FREETYPE_FONT_RENDER_MODE_BITMAP, (uint32_t)size, LV_FREETYPE_FONT_STYLE_NORMAL);
    /* D5 修复：缺失资产回退时按 size 选最近可用嵌入子集（14/16），
     * 不再硬编码 16，避免 20/32px 文本回退后字号变小。 */
    if (!f) f = (size <= 14 && g_font_cn_14) ? g_font_cn_14 : (g_font_cn_16 ? g_font_cn_16 : LV_FONT_DEFAULT);
    if (!f) return LV_FONT_DEFAULT;

    if (s_font_cache_count < HMI_FONT_CACHE_MAX) {
        hmi_font_entry_t* e = &s_font_cache[s_font_cache_count++];
        snprintf(e->id, sizeof(e->id), "%s", font_id ? font_id : "");
        snprintf(e->file, sizeof(e->file), "%s", found ? path : "");
        e->size = size;
        e->f = f;
    }
    HMI_LOG("resolve_font(%s, %s, %d) -> %s", font_id ? font_id : "-",
            font_file ? font_file : "-", size, found ? path : "(cjk fallback)");
    return f;
}

static void hmi_font_cn_init(void)
{
    /* 使用 FreeType 加载完整简体中文字体（Noto Sans CJK SC，44810 字符）
     * 路径优先 UDISK（可 OTA 更新），回退系统出厂字体。
     * 跳过 freetype 的调试模式已移除（修复方框文字问题）。 */
    const char* path = NULL;
    if (access(HMI_CN_FONT_PATH_UDISK, F_OK) == 0) {
        path = HMI_CN_FONT_PATH_UDISK;
    } else if (access(HMI_CN_FONT_PATH_SYS, F_OK) == 0) {
        path = HMI_CN_FONT_PATH_SYS;
    }
    if (path) {
        HMI_LOG_DEBUG("loading CJK font from %s", path);
        g_font_cn_12 = lv_freetype_font_create(path,
            LV_FREETYPE_FONT_RENDER_MODE_BITMAP, 12, LV_FREETYPE_FONT_STYLE_NORMAL);
        g_font_cn_14 = lv_freetype_font_create(path,
            LV_FREETYPE_FONT_RENDER_MODE_BITMAP, 14, LV_FREETYPE_FONT_STYLE_NORMAL);
        g_font_cn_16 = lv_freetype_font_create(path,
            LV_FREETYPE_FONT_RENDER_MODE_BITMAP, 16, LV_FREETYPE_FONT_STYLE_NORMAL);
        g_font_cn_20 = lv_freetype_font_create(path,
            LV_FREETYPE_FONT_RENDER_MODE_BITMAP, 20, LV_FREETYPE_FONT_STYLE_NORMAL);
        HMI_LOG("cn font ready: path=%s (full CJK, %zu chars subset)",
                path ? path : "(none)", 44810);
    } else {
        /* 最终回退：内嵌 CJK 子集（仅作安全兜底，非正常路径） */
        HMI_LOG_DEBUG("CJK font file missing, using embedded fallback");
        g_font_cn_12 = &lv_font_source_han_sans_sc_14_cjk;
        g_font_cn_14 = &lv_font_source_han_sans_sc_14_cjk;
        g_font_cn_16 = &lv_font_source_han_sans_sc_16_cjk;
        g_font_cn_20 = &lv_font_source_han_sans_sc_16_cjk;
        HMI_LOG("cn font ready (embedded fallback, limited subset)");
    }
}

/* LVGL tick 必须用单调时钟（CLOCK_MONOTONIC）：墙钟 gettimeofday 在开机早期
 * 会被 NTP/date 同步跳变（设备无 RTC，时间从 /etc/timestamp 起步、联网后跳到
 * 当前时间），导致 LVGL 刷新定时器到期判断错乱 → 开机自启时画面不渲染（黑屏），
 * 而系统稳定后 restart 的实例正常。2026-08-14 实机验证修复。 */
uint32_t custom_tick_get(void)
{
    static uint64_t start_ms = 0;
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    uint64_t now_ms = (uint64_t)ts.tv_sec * 1000 + ts.tv_nsec / 1000000;
    if (start_ms == 0) start_ms = now_ms;
    return (uint32_t)(now_ms - start_ms);
}

/* 周期驱动报警/趋势引擎（ABI V2 接口接线）。
 * 取当前 .so 的 api 表，把变量快照喂入 alarm_evaluate / trend_tick。
 * 计数按宿主采纳的标签数（hmi_tag_count，ABI V3 热更新动态），
 * 快照用堆缓冲避免旧版静态 TAG_COUNT 与工程失配。 */
#define HMI_ENGINE_MAX_TAGS 256
static float* s_engine_snap = NULL;
static void hmi_engine_poll(void)
{
    const hmi_ui_api_t* api = hmi_ui_get_current_api();
    if (!api) return;
    if (!api->alarm_evaluate && !api->trend_tick) return;   /* 旧 .so 或未配置引擎 */

    int n = hmi_tag_count();
    if (n <= 0) return;
    if (n > HMI_ENGINE_MAX_TAGS) n = HMI_ENGINE_MAX_TAGS;
    if (!s_engine_snap) {
        s_engine_snap = (float*)malloc(sizeof(float) * (size_t)HMI_ENGINE_MAX_TAGS);
        if (!s_engine_snap) return;
    }
    for (int i = 0; i < n; i++) s_engine_snap[i] = var_raw_read(i);

    if (api->alarm_evaluate) api->alarm_evaluate(s_engine_snap, n);
    if (api->trend_tick) {
        uint8_t cnt = (uint8_t)(n > 255 ? 255 : n);
        api->trend_tick(s_engine_snap, cnt);
    }
    /* 报警上行（阶段 291，M2）：紧跟 `alarm_evaluate` —— 快照与求值**同一线程**读，
     * 不存在跨线程读 alarm_record_t 的问题；发布器自己边沿触发（状态没变不发）。 */
    hmi_alarm_publish_tick();
}

/* SIGUSR1 / SIGUSR2：调试信号。
 *
 * ⚠ **处理器里只置标志**（阶段 121）：原实现直接在信号处理器里调
 * `hmi_kb_debug_open_mode` / `hmi_kb_debug_pinyin` / `hmi_kb_debug_insert`，
 * 而它们会进 LVGL、malloc、fprintf —— **都不是 async-signal-safe** 的函数，
 * 等于在任意时刻从信号上下文打断 LVGL ⇒ 死锁/堆损坏（且只在调试时偶发，最难查）。
 * 真正的动作放到主循环里做（正常上下文、与 LVGL 同一线程）。 */
static volatile sig_atomic_t s_debug_kb_pending = 0;
static volatile sig_atomic_t s_debug_insert_pending = 0;

static void on_debug_kb(int sig)
{
    (void)sig;
    s_debug_kb_pending = 1;
}

static void on_debug_insert(int sig)
{
    (void)sig;
    s_debug_insert_pending = 1;
}

/* 主循环：把信号标志变成真正的调试动作。计数语义与原实现逐字一致
   （SIGUSR1：第 1 次 text、之后奇数 num；SIGUSR2：第 1 次拼音、之后固定文本）。 */
static void process_debug_signals(void)
{
    if (s_debug_kb_pending) {
        static int calls = 0;
        s_debug_kb_pending = 0;
        hmi_kb_debug_open_mode((calls++ % 2 == 1) ? "num" : "text");
    }
    if (s_debug_insert_pending) {
        static int calls = 0;
        s_debug_insert_pending = 0;
        if (calls++ == 0) {
            hmi_kb_debug_pinyin();
        } else {
            hmi_kb_debug_insert("你好zhishu");
        }
    }
}

int main(void)
{
    HMI_LOG("zhishu-HMI runtime v%s (target: %s)", HMI_RUNTIME_VERSION_STR, HMI_TARGET_ID);

    lv_init();
    lv_tick_set_cb(custom_tick_get);
    {
        /* 诊断：lv_init 后多次 malloc 定位堆损坏 */
        for (int i = 0; i < 8; i++) {
            size_t sz = (i % 2) ? 4096 : (2457600 - i * 100000);
            void* p = malloc(sz);
            HMI_LOG_DEBUG("malloc#%d size=%u -> %p", i, (unsigned)sz, p);
            if (p) { memset(p, 0xAA, sz); free(p); }
        }
        HMI_LOG_DEBUG("malloc loop done");
    }
    lv_fs_stdio_init();   /* 注册 'A:' 文件系统驱动（生成代码 IMG_PATH "A:/mnt/UDISK/hmi/assets/..."） */
    signal(SIGUSR1, on_debug_kb);   /* kill -USR1 <pid> 弹软键盘调试 */
    signal(SIGUSR2, on_debug_insert);   /* kill -USR2 <pid> 插入测试文本 */
    hmi_font_cn_init();   /* 中文字体 fallback（须在屏幕创建前） */
    {
        /* 诊断：字体加载后堆是否仍正常 */
        void* t = malloc(1024);
        HMI_LOG_DEBUG("post-font malloc=%p", t);
        free(t);
    }

    if (lcd_sunxifb_init() != 0) {
        HMI_LOG("LCD init failed, runtime aborted");
        return -1;
    }
    HMI_LOG_DEBUG("lcd init done");
    if (touch_evdev_init(HMI_TOUCH_DEV) != 0) {
        HMI_LOG("touch init failed (node=%s), continuing without touch", HMI_TOUCH_DEV);
    }
    HMI_LOG_DEBUG("touch init done");

    hmi_var_init();
    HMI_LOG_DEBUG("var init done");
    hmi_comm_init();
    HMI_LOG_DEBUG("comm init done");
    if (hmi_comm_start() != 0) {
        HMI_LOG("comm thread start failed");
    }
    HMI_LOG_DEBUG("comm start done");

    /* ── .so 热加载（UDISK OTA → 系统 fallback） ── */
    (void)hmi_ui_load();   /* 失败返回 -1 但不崩溃，继续运行通信服务 */
    HMI_LOG_DEBUG("ui load done");

    /* 创建全部屏幕（.so 未加载则 hmi_screens_init 什么都不做） */
    hmi_screens_init();
    HMI_LOG_DEBUG("screens init done");
    /* 宿主级 CJK 字体修复：下拉框主体/弹出列表显式补设工程字体（防中文空白/方框） */
    hmi_screens_fix_fonts();

    const hmi_ui_api_t* api = hmi_ui_get_current_api();
    int loaded = api ? 1 : 0;
    int n = 0;
    if (api && api->get_screens) (void)api->get_screens(&n);
    HMI_LOG("screens loaded: %d (ui_loaded=%d)", n, loaded);

    /* 开机设置入口窗口：窗口内单击进设置，超时/退出设置后进入运行页（第 0 屏） */
    sys_settings_boot_start();

    for (;;) {
        lv_timer_handler();
        process_debug_signals();   /* 调试信号：处理器只置标志，动作在这里做（阶段 121） */
        sys_settings_boot_poll();
        hmi_engine_poll();    /* 喂变量快照驱动报警/趋势引擎（无引擎或旧 .so 时为空操作） */
        hmi_session_poll();   /* 会话超时评估（无会话 / 未启用自动登出时为空操作） */
        usleep(5000);
    }
    return 0;
}
