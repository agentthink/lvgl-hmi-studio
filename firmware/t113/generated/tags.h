/*
 * tags.h — 标签表契约（由 zhishu-HMI 编辑器导出，ABI V3 .so 自包含）
 *
 * tag_meta_t   标签元数据（name/address/read_only/scale/offset）
 * tag_binding_t 通信绑定（driver/addr/unit/dtype/poll_ms）
 * HMI_UI_TAG_META/HMI_UI_TAG_BINDINGS 内嵌于本头（static const），
 * 宿主 hmi_runtime 经 get_tags/get_tag_bindings 采纳（变量表/轮询组/MQTT 主题）。
 */
#ifndef HMI_GENERATED_TAGS_H
#define HMI_GENERATED_TAGS_H

#include <stdint.h>
#include <string.h>

typedef enum {
    TAG_PV = 0,
    TAG_SV = 1,
    TAG_RUN = 2,
    TAG_ALARM = 3,
    TAG_TEMP = 4,
    TAG_COUNT
} tag_index_t;

typedef struct {
    const char* name;
    const char* address;
    uint8_t read_only;   /* 1 = 只读（写操作拒绝） */
    float scale;
    float offset;
    uint8_t has_range;   /* 1 = min/max 有效（默认由数据类型推导；Float ⇒ 0 = 不限） */
    float min_value;     /* 原始值下界（含） */
    float max_value;     /* 原始值上界（含） */
} tag_meta_t;

typedef struct {
    const char* driver;   /* "modbus-rtu" / "modbus-tcp" / "mqtt" / "local" */
    const char* addr;     /* 地址串（"40001"/"00001"/"h:0"/"3:0"...） */
    uint8_t     unit;     /* Modbus 从站号 */
    const char* dtype;    /* Bool/U16/I16/U32/I32/Float */
    uint16_t    poll_ms;  /* 轮询周期（0=不轮询） */
} tag_binding_t;

/* ── 标签元数据（.so 内嵌；宿主经 get_tags 采纳） ── */
static const tag_meta_t HMI_UI_TAG_META[TAG_COUNT > 0 ? TAG_COUNT : 1] = {
    { "PV", "40001", 0, 1.0f, 0.0f, 1, 0.0f, 65535.0f },
    { "SV", "40002", 0, 1.0f, 0.0f, 1, 0.0f, 65535.0f },
    { "RUN", "00001", 0, 1.0f, 0.0f, 1, 0.0f, 1.0f },
    { "ALARM", "40003", 1, 1.0f, 0.0f, 1, 0.0f, 65535.0f },
    { "TEMP", "30001", 1, 0.1f, 0.0f, 0, 0.0f, 0.0f },
};

/* ── 通信绑定（.so 内嵌；宿主经 get_tag_bindings 采纳） ── */
static const tag_binding_t HMI_UI_TAG_BINDINGS[TAG_COUNT > 0 ? TAG_COUNT : 1] = {
    { "modbus-tcp", "40001", 1, "U16", 100 },
    { "modbus-tcp", "40002", 1, "U16", 100 },
    { "modbus-rtu", "00001", 1, "Bool", 100 },
    { "modbus-tcp", "40003", 1, "U16", 100 },
    { "mqtt", "30001", 1, "U16", 100 },
};

/* 设备层接口（firmware app/runtime/variable_table.c 实现） */
extern float var_raw_read(int index);
extern void  var_raw_write(int index, float raw);

/* 应用层读写 —— 自动应用 scale/offset 换算与只读保护（内嵌元数据） */
static inline float get_tag_value(tag_index_t idx)
{
    if (idx < 0 || idx >= TAG_COUNT) return 0.0f;
    const tag_meta_t* t = &HMI_UI_TAG_META[idx];
    return var_raw_read((int)idx) * t->scale + t->offset;
}

static inline uint8_t set_tag_value(tag_index_t idx, float eng)
{
    if (idx < 0 || idx >= TAG_COUNT) return 0;
    const tag_meta_t* t = &HMI_UI_TAG_META[idx];
    if (t->read_only) return 0;    /* 只读保护 */
    float raw = (t->scale != 0.0f) ? (eng - t->offset) / t->scale : eng;
    var_raw_write((int)idx, raw);
    return 1;
}

#endif /* HMI_GENERATED_TAGS_H */
