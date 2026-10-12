/*
 * hmi_ui_api.h — HMI .so 与宿主 hmi_runtime 之间的 ABI 契约
 *
 * T113 OTA 热加载方案：
 *   HMI 工程生成代码（屏幕/主题/标签绑定）编译为 libhmi_ui.so，
 *   hmi_runtime 通过 dlopen(/mnt/UDISK/hmi/libhmi_ui.so, RTLD_NOW | RTLD_GLOBAL)
 *   动态加载，dlsym("hmi_ui_get_api") 获取回调表。
 *   优先加载 UDISK（OTA 升级），失败回退 /usr/lib/hmi/libhmi_ui.so（出厂）。
 *
 * ABI 版本校验（hmi_ui_api_t.abi_version / lvgl_version）：
 *   宿主和 .so 编译时的 LVGL 大版本 / ABI 版本必须一致，
 *   否则 hmi_ui_load 返回 -1 拒绝加载（避免符号不匹配崩溃）。
 */
#ifndef HMI_UI_API_H
#define HMI_UI_API_H

#include <stdint.h>
#include "lvgl.h"
#include "screen_manager.h"   /* screen_def_t */
#include "tags.h"             /* tag_meta_t, tag_binding_t, TAG_COUNT */
#include "hmi_alarm_snapshot.h" /* hmi_alarm_snapshot_t（无 lvgl 依赖，见该头注释） */

#ifdef __cplusplus
extern "C" {
#endif

/* ── ABI / LVGL 版本（与宿主编译时一致才算兼容） ── */
/* V3：项目无关字体服务（hmi_resolve_font）+ 标签表宿主采纳（get_tags 驱动设备层）
 *     + 软键盘模式标记（hmi_set_ta_keyboard_mode）。
 *     V2 之前 .so extern 引用宿主 TAG_TABLE/font_<assetid> 符号 —— 工程变更即失配，
 *     dlopen(RTLD_NOW) 直接失败；V3 起 .so 自包含标签/字体解析，可热更新任意工程。 */
#define HMI_UI_ABI_VERSION     3u
#define HMI_UI_LVGL_VERSION    0x905u /* LVGL 9.5 → 0x0905，与 LVGL_VERSION_MAJOR*256+LVGL_VERSION_MINOR 同构 */

typedef struct {
    uint32_t abi_version;      /* MUST == HMI_UI_ABI_VERSION */
    uint32_t lvgl_version;     /* MUST == HMI_UI_LVGL_VERSION */
    uint32_t ui_version;       /* UI 工程版本（HmiProject.ProjectVersion 主版本），OTA 比对用 */
    const char* build_id;      /* 构建 ID（Name-vProjectVersion），日志用 */

    /* 获取屏幕注册表（屏幕数量写 *count），返回数组指针 */
    const screen_def_t*  (*get_screens)(int* count);
    /* 获取标签元数据（数量写 *count）。V3 起宿主在 hmi_ui_load 时采纳该表
     * 驱动设备层（缓存尺寸/只读保护/轮询组/MQTT 主题），替换 .so 即替换标签表。 */
    const tag_meta_t*    (*get_tags)(int* count);
    /* 获取标签绑定表（数量写 *count），与 get_tags 同序。 */
    const tag_binding_t* (*get_tag_bindings)(int* count);

    /* 初始化回调（创建第一个屏幕前调用，传入根活动屏幕或 NULL）；
     * 主题样式 apply（lv_obj_set_style_* 设置全局默认样式）放这里。 */
    void (*init)(lv_obj_t* root_scr);
    /* 卸载前清理（可选，可为 NULL） */
    void (*teardown)(void);

    /* ── 报警引擎接口（可选，NULL 表示无报警；ABI V2+） ── */
    /* 周期调用：将当前变量快照喂入报警引擎求值（null 检查后再调用）。
     * var_values：变量值数组；var_count：数组长度（调用方需保证 var_count 足够覆盖所有启用报警的变量索引）。 */
    void (*alarm_evaluate)(float* var_values, int var_count);
    /* 用户主动确认某条报警（按 config_id）。返回 1=成功确认，0=未找到或未激活。 */
    uint8_t  (*alarm_acknowledge)(uint16_t config_id);
    /* 返回当前 Active 状态的报警数量（用于 UI 报警灯/汇总条）。 */
    uint16_t (*alarm_active_count)(void);

    /* ── 趋势引擎接口（可选，NULL 表示无趋势；ABI V2+） ── */
    /* 周期调用：喂入变量快照，引擎内部同时分发到趋势图更新与数据记录。 */
    void (*trend_tick)(float* values, uint8_t count);
    /* CSV 导出：把指定 logger（log_idx=0..N-1，对应 DataLogConfigs 顺序）的环形缓冲按时间顺序写出。
     * write_fn 每块调用一次（可多次，含表头与各数据行）。 */
    void (*datalog_export_csv)(int log_idx, void (*write_fn)(const char*));

    /* ── 报警搁置（可选，NULL 表示该 .so 不支持搁置；阶段 277）──
     * ⚠ **追加在结构体尾部**是刻意的：老宿主按偏移读前面那些字段 ⇒ 尾部追加不改变任何既有偏移，
     * 老宿主照常工作、新宿主按需读这两个指针（比插在报警块中间安全得多）。
     *   搁置：停止播报，必须给时长（ms），返回 1=成功（权限/时长/状态在实现侧已判）；
     *        `by` = 操作人（可 NULL）—— **宿主知道登录用户，.so 不知道**，所以由调用方传进来，
     *        生成的实现会把它写进设备审计（`hmi_audit`）。
     *   解除：只在搁置中有效，恢复搁置前的对外状态，返回 1=成功；`by` 同上。 */
    uint8_t (*alarm_shelve)(uint16_t config_id, uint32_t duration_ms, const char* by);
    uint8_t (*alarm_unshelve)(uint16_t config_id, const char* by);

    /* ── 报警逐条快照（可选，NULL = 该 .so 不支持；阶段 291）──
     * 把当前每条报警的 (id, config_id, state, severity) 写进 `out`，返回**写入条数**（≤ cap）。
     * 只读、不改变报警状态。为什么需要：M2 的 `<prefix>/alarm/<id>` 要 `st=`/`sev=`，
     * 而 `alarm_active_count()` 只给一个数字。`state == 0`（Inactive）**不属于可上行状态**
     * （见 mqtt_contract.h 的 1..4 约定），调用方自行跳过。
     * ⚠ 追加在结构体**尾部**（与搁置两项同一理由：老宿主按偏移读前面的字段 ⇒ 尾部追加不动任何既有偏移）。 */
    uint16_t (*alarm_snapshot)(hmi_alarm_snapshot_t* out, uint16_t cap);
} hmi_ui_api_t;

/* .so 唯一导出函数：返回静态 hmi_ui_api_t 结构体指针（NULL=失败） */
const hmi_ui_api_t* hmi_ui_get_api(void);

/* ── 宿主 side：动态库加载/卸载接口（screen_manager.c 实现） ── */

/* 加载顺序：
 *   1. /mnt/UDISK/hmi/libhmi_ui.so   (OTA 升级)
 *   2. /usr/lib/hmi/libhmi_ui.so     (出厂 fallback)
 * 加载成功返回 0，全部失败返回 -1（宿主 FATAL 日志但不崩溃）。
 * 调用 dlopen(..., RTLD_NOW | RTLD_GLOBAL)，RTLD_GLOBAL 确保
 * .so 能解析宿主通过 -rdynamic 导出的 hmi_screen_switch_idx /
 * var_raw_read / hmi_resolve_font 等全局符号。
 */
int  hmi_ui_load(void);

/* 卸载 .so（OTA 重启前或退出时可选调用） */
void hmi_ui_unload(void);

/* 获取当前已加载的 API 表指针（未加载返回 NULL） */
const hmi_ui_api_t* hmi_ui_get_current_api(void);

/* ── 宿主 side：.so 可调用的宿主服务（dlopen 符号解析，ABI V3+） ── */

/* 项目无关字体服务（main.c 实现）：
 * 按工程字体资产 (font_id, font_file, size) 创建/复用 FreeType 字体。
 * 字体文件优先 /mnt/UDISK/hmi/assets/<file>（IDE 部署），回退 /usr/lib/hmi/assets、
 * /mnt/UDISK/hmi/fonts、/usr/lib/hmi/fonts；全部缺失回退设备 CJK 字体，
 * 创建失败返回已就绪的宿主 CJK 回退字体（永不 NULL）。
 * size<=0 使用宿主默认字号（16）。 */
lv_font_t* hmi_resolve_font(const char* font_id, const char* font_file, int size);

/* 软键盘模式标记（soft_keyboard.c 实现，生成代码在 TextArea 创建时调用）：
 *   "num"  数字键盘（小数点/负号）
 *   "text" 中英文键盘（拼音/英文字母，默认）
 * 未调用时宿主按默认 text 处理。 */
void hmi_set_ta_keyboard_mode(lv_obj_t* ta, const char* mode);

/* 当前生效标签数（宿主采纳 .so 标签表后的数量；main.c 引擎轮询用） */
int hmi_tag_count(void);

#ifdef __cplusplus
}
#endif

#endif /* HMI_UI_API_H */
