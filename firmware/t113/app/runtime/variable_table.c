/*
 * variable_table.c — 变量表（设备层实现）
 *
 * 职责：
 *   - 维护变量原始值缓存（UI 线程读，通信线程写）
 *   - 实现 var_raw_read / var_raw_write 设备层接口
 *   - 由 TAG_BINDINGS 静态构建轮询组（同驱动/从站/功能码且地址连续的标签合并为块读）
 *   - 消费写请求队列（float → 寄存器转换后经通信模块下发）
 *   - MQTT 订阅分发与变化发布扫描
 *
 * 数据类型支持：Bool / U16 / I16 / U32 / I32 / Float
 * 地址规范与 hmi_tag_parse_addr 一致（编辑器 RuntimeTag.Address 的 "40001" 形式）。
 */
#include "variable_table.h"
#include "runtime_config.h"
#include "mqtt_client.h"
#include "mqtt_contract.h"   /* 主题/payload 契约 v1（阶段 264，登记 §2-#12 的 M1） */
#include "mqtt_cmd_wiring.h" /* 远程写命令的宿主接线（订阅/元数据/权限 → hmi_cmd_handle） */
#include "tags.h"
#include <pthread.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <math.h>
#include <time.h>

/* 主机侧单元测试支持：暴露内部函数与状态符号（HMI_UNIT_TEST 由测试编译指定） */
#ifdef HMI_UNIT_TEST
#define static
#endif

/* ── 变量缓存 ── */
/* ABI V3 热更新：标签表不再固定编译进宿主使用。宿主启动先用编译期默认表
 * （tags.c，与出厂 .so 一致），hmi_ui_load 采纳 .so 的 get_tags/get_tag_bindings
 * 后切换到工程标签表（hmi_tags_adopt），缓存/只读保护/轮询组/MQTT 主题全部按
 * 采纳表重建 —— 替换 .so 即替换标签表，工程无关。 */
#define HMI_ADOPT_MAX_TAGS 256
static const tag_meta_t*    g_meta  = NULL;   /* 当前生效标签元数据 */
static const tag_binding_t* g_bind  = NULL;   /* 当前生效通信绑定 */
static int g_count = 0;                       /* 当前生效标签数 */

static float* s_cache = NULL;
static float* s_last_pub = NULL;
static pthread_mutex_t s_lock = PTHREAD_MUTEX_INITIALIZER;

#ifdef HMI_UNIT_TEST
/*
 * 测试注入：非 0 时让 hmi_tags_adopt 的**新缓存分配失败**。
 * 没有这个开关，"分配失败时全局表必须保持不变"这条判据在主机侧造不出来
 * （正常环境里 calloc 不会失败）；用它才能把失败路径变成可执行的断言。
 */
int s_tags_adopt_fail_alloc = 0;
#endif

/* ── 写请求队列（UI 线程投递，通信线程消费；标签采纳时重置） ── */
#define HMI_WQ_SIZE 32
typedef struct { int idx; float raw; } wq_item_t;
static wq_item_t s_wq[HMI_WQ_SIZE];
static volatile int s_wq_head = 0;
static volatile int s_wq_tail = 0;

int hmi_tag_count(void)
{
    return g_count;
}

/* 清空 MQTT 订阅（mqtt_client.c 实现），采纳新标签表后重注册避免主题重复累积 */
void mqtt_clear_topics(void);

/* 轮询组重建（定义在本文件后方；标签表采纳后调用） */
void var_groups_rebuild(void);

/* 标签表采纳：拷贝宿主侧副本后切换 g_meta/g_bind/g_count，
 * 重建缓存与轮询组。metas/bindings 为 NULL 时回退编译期默认表；
 * 非 NULL 时严格采纳 count（可为 0 = 无标签工程）。 */
int hmi_tags_adopt(const tag_meta_t* metas, const tag_binding_t* bindings, int count)
{
    static tag_meta_t    s_meta_copy[HMI_ADOPT_MAX_TAGS];
    static tag_binding_t s_bind_copy[HMI_ADOPT_MAX_TAGS];

    pthread_mutex_lock(&s_lock);

    const tag_meta_t*    new_meta;
    const tag_binding_t* new_bind;
    int                  new_count;

    if (!metas || !bindings) {
        /* 回退编译期默认表（出厂 .so 契约） */
        // ⚠ 阶段 318：改用**生成器的规范名**（tags.h 里的 static 表）。
        //   旧名 TAG_TABLE/TAG_BINDINGS 是那份**手工维护**的宿主 tags.c 的产物；
        //   阶段 318 起设备树由生成器重新生成，规范名是 HMI_UI_TAG_META/HMI_UI_TAG_BINDINGS
        //   （全仓只有本处真正引用旧名，见 grep）。
        new_meta = HMI_UI_TAG_META;
        new_bind = HMI_UI_TAG_BINDINGS;
        new_count = TAG_COUNT;
    } else {
        if (count < 0) count = 0;
        if (count > HMI_ADOPT_MAX_TAGS) count = HMI_ADOPT_MAX_TAGS;
        for (int i = 0; i < count; i++) {
            s_meta_copy[i] = metas[i];
            s_bind_copy[i] = bindings[i];
        }
        new_meta = s_meta_copy;
        new_bind = s_bind_copy;
        new_count = count;
    }

    /* ⚠ 顺序是判据的一部分：**先分配新缓存，成功之后才切换全局元数据/计数**。
       旧写法先写 g_meta/g_bind/g_count、再 calloc 新 s_cache/s_last_pub；一旦分配失败
       就直接 return -1，而 g_count 已经变大、s_cache 仍是旧的小块 ⇒ 之后任何按
       g_count 的索引（轮询、MQTT 发布扫描）都是**堆越界读写**（阶段 120 修复）。 */
    float* nc = (float*)calloc((size_t)(new_count > 0 ? new_count : 1), sizeof(float));
    float* np = (float*)calloc((size_t)(new_count > 0 ? new_count : 1), sizeof(float));
#ifdef HMI_UNIT_TEST
    if (s_tags_adopt_fail_alloc) { free(nc); free(np); nc = NULL; np = NULL; }
#endif
    if (!nc || !np) {
        free(nc); free(np);
        pthread_mutex_unlock(&s_lock);
        fprintf(stderr, "[hmi] tags_adopt: alloc failed（保持原表不变）\n");
        return -1;
    }

    g_meta = new_meta;
    g_bind = new_bind;
    g_count = new_count;
    free(s_cache); free(s_last_pub);
    s_cache = nc; s_last_pub = np;
    s_wq_head = s_wq_tail = 0;

    pthread_mutex_unlock(&s_lock);

    var_groups_rebuild();
    fprintf(stderr, "[hmi] tags adopted: count=%d\n", g_count);
    return 0;
}

/* ── 轮询组 ── */
#define HMI_MAX_GROUP_REGS  120
#define HMI_MAX_GROUPS      64
#define HMI_SLOT_EMPTY      0xFFFF
typedef struct {
    hmi_driver_t drv;
    uint8_t  unit;
    uint8_t  fn;
    uint16_t off;                 /* 起始寄存器偏移 */
    uint16_t count;               /* 块内寄存器个数 */
    uint16_t idx[HMI_MAX_GROUP_REGS];  /* 每寄存器槽位对应的标签索引（0xFFFF=无标签） */
} poll_group_t;
static poll_group_t s_groups[HMI_MAX_GROUPS];
static int s_group_count = 0;
static int s_poll_cursor = 0;
static int s_poll_ticks = 0;      /* MQTT 发布扫描计数 */

/* ── 工具函数 ── */

/* 数据类型占用的寄存器个数 */
static int dtype_regs(const char* d)
{
    if (d && (strcmp(d, "Float") == 0 || strcmp(d, "U32") == 0 || strcmp(d, "I32") == 0))
        return 2;
    return 1;
}

/* 原始值缓存读 */
float var_raw_read(int index)
{
    if (index < 0 || index >= g_count) return 0.0f;
    float v;
    pthread_mutex_lock(&s_lock);
    v = s_cache[index];
    pthread_mutex_unlock(&s_lock);
    return v;
}

/* MQTT 订阅回调：仅写缓存 */
void var_set_from_sub(int index, float v)
{
    if (index < 0 || index >= g_count) return;
    pthread_mutex_lock(&s_lock);
    s_cache[index] = v;
    pthread_mutex_unlock(&s_lock);
}

/* 设备层写：更新缓存 + 投递写请求（通信线程真正下发） */
void var_raw_write(int index, float raw)
{
    if (index < 0 || index >= g_count) return;
    pthread_mutex_lock(&s_lock);
    s_cache[index] = raw;
    /* 只读保护：不投递写请求 */
    if (g_meta && g_meta[index].read_only) {
        pthread_mutex_unlock(&s_lock);
        return;
    }
    int next = (s_wq_tail + 1) % HMI_WQ_SIZE;
    if (next != s_wq_head) {          /* 满则丢弃（记录日志由调用侧保证不溢出） */
        s_wq[s_wq_tail].idx = index;
        s_wq[s_wq_tail].raw = raw;
        s_wq_tail = next;
    }
    pthread_mutex_unlock(&s_lock);
}

/* 标签名 → 索引 */
int hmi_tag_index_by_name(const char* name)
{
    if (!g_meta) return -1;
    for (int i = 0; i < g_count; i++)
        if (strcmp(g_meta[i].name, name) == 0) return i;
    return -1;
}

/* 标签元数据只读访问（远程写命令的 target 元数据 / 订阅注册用） */
const tag_meta_t* hmi_tag_meta_at(int index)
{
    if (!g_meta || index < 0 || index >= g_count) return NULL;
    return &g_meta[index];
}

/* ── 轮询组构建 ── */

/* MQTT 订阅消息回调（定义见文件尾部） */
static void on_mqtt_message(const char* topic, const char* payload, int len, void* user);

/* MQTT 连接建立回调（上线报状态；定义见文件尾部） */
static void on_mqtt_connected(void);

/* 开机时长 ms（单调时钟；定义见文件尾部）—— 上线状态/报警上行的时间基都用它 */
unsigned long long hmi_var_uptime_ms(void);

static int group_fits(poll_group_t* g, uint16_t off, int regs)
{
    if (off < g->off) return 0;                       /* 只允许向前扩展 */
    uint16_t end = (uint16_t)(off + regs);
    if (end < off) return 0;                          /* 溢出 */
    if (end <= g->off) return 0;
    uint16_t need = (uint16_t)(end - g->off);
    if (need > HMI_MAX_GROUP_REGS) return 0;
    if (need > g->count) {                            /* 允许 ≤16 寄存器间隔的合并 */
        if ((need - g->count) > 16 && off > (uint16_t)(g->off + g->count)) return 0;
    }
    return 1;
}

static int group_add(poll_group_t* g, int tag_idx, uint16_t off, int regs)
{
    uint16_t end = (uint16_t)(off + regs);
    uint16_t need = (uint16_t)(end - g->off);
    /* 仅向前扩展：need 超过当前 count 才补空槽并推进 count；
     * 标签乱序（如 off2 先于 off1 处理）时 need ≤ count，不得回退 count、
     * 也不得清空已填槽位（旧实现 g->count = need 会回退并抹掉槽内标签）。 */
    if (need > g->count) {
        for (int j = g->count; j < need && j < HMI_MAX_GROUP_REGS; j++)
            g->idx[j] = HMI_SLOT_EMPTY;
        g->count = need;
    }
    for (int j = 0; j < regs; j++) {
        uint16_t slot = (uint16_t)(off - g->off + j);
        if (slot < HMI_MAX_GROUP_REGS && g->idx[slot] == HMI_SLOT_EMPTY)
            g->idx[slot] = (uint16_t)tag_idx;
    }
    return 1;
}

/* 轮询组重建（标签表采纳/初始化时调用） */
void var_groups_rebuild(void)
{
    s_group_count = 0;
    if (!g_bind) return;
    for (int i = 0; i < g_count; i++) {
        const tag_binding_t* b = &g_bind[i];
        hmi_driver_t drv = hmi_driver_from_name(b->driver);
        fprintf(stderr, "[hmi]   binding[%d] driver=\"%s\" drv=%d addr=\"%s\" unit=%u dtype=\"%s\"\n",
                i, b->driver ? b->driver : "(null)", (int)drv,
                b->addr ? b->addr : "(null)", b->unit, b->dtype ? b->dtype : "(null)");
        if (drv != HMI_DRV_MODBUS_RTU && drv != HMI_DRV_MODBUS_TCP) continue;

        uint8_t fn; uint16_t off;
        if (hmi_tag_parse_addr(b->addr, &fn, &off) != 0) {
            fprintf(stderr, "[hmi]   binding[%d] addr parse FAILED\n", i);
            continue;
        }
        int regs = dtype_regs(b->dtype);

        int placed = 0;
        for (int g = 0; g < s_group_count; g++) {
            poll_group_t* pg = &s_groups[g];
            if (pg->drv == drv && pg->unit == b->unit && pg->fn == fn &&
                group_fits(pg, off, regs)) {
                group_add(pg, i, off, regs);
                placed = 1;
                break;
            }
        }
        if (!placed && s_group_count < HMI_MAX_GROUPS) {
            poll_group_t* pg = &s_groups[s_group_count++];
            memset(pg, 0, sizeof(*pg));
            pg->drv = drv; pg->unit = b->unit; pg->fn = fn; pg->off = off;
            for (int j = 0; j < HMI_MAX_GROUP_REGS; j++) pg->idx[j] = HMI_SLOT_EMPTY;
            group_add(pg, i, off, regs);
        }
    }
    /* MQTT 标签：注册订阅主题（断线自动重订阅；先清空旧主题避免重复累积） */
    mqtt_set_message_cb(on_mqtt_message);
    mqtt_clear_topics();
    if (g_meta) {
        for (int i = 0; i < g_count; i++) {
            const tag_binding_t* b = &g_bind[i];
            if (hmi_driver_from_name(b->driver) != HMI_DRV_MQTT) continue;
            char topic[128];
            snprintf(topic, sizeof(topic), "%s/set/%s", HMI_MQTT_TOPIC_PRE, g_meta[i].name);
            mqtt_register_topic(topic, (void*)(intptr_t)i);   /* user 即标签索引 */
        }
    }
    /* 远程写命令：<pre>/cmd/<tag> —— 只为**可写**标签订阅；判定链在 mqtt_cmd_wiring → hmi_cmd_handle。 */
    hmi_cmd_wiring_register_topics();

    /* ── M2：状态上行 ＋ 遗嘱（阶段 290）──
     *   上线：CONNACK 后发布 `<pre>/status` 的 `on=1`（retain ⇒ 晚加入的远程端立刻看到状态）；
     *   离线：同主题、同 retain 的 `on=0` 注册成 **LWT**，由 broker 在设备掉线时代发。
     *   两处都走 mqtt_contract 的纯函数（主题/payload 契约的唯一实现）。 */
    mqtt_set_connect_cb(on_mqtt_connected);
    {
        char wt[192];
        if (hmi_mqtt_format_status_topic(wt, sizeof(wt), HMI_MQTT_TOPIC_PRE) == 0)
            mqtt_set_will(wt, HMI_MQTT_STATUS_LWT_PAYLOAD, 1);
    }
    /* 诊断：打印轮询组构建结果（2026-08-18 实机排查） */
    fprintf(stderr, "[hmi] poll groups: count=%d\n", s_group_count);
    for (int gi = 0; gi < s_group_count; gi++) {
        const poll_group_t* pg = &s_groups[gi];
        fprintf(stderr, "[hmi]   group[%d] drv=%d unit=%u fn=0x%02X off=%u count=%u\n",
                gi, (int)pg->drv, pg->unit, pg->fn, pg->off, pg->count);
    }
}

int hmi_var_init(void)
{
    /* 初始状态：采纳编译期默认表（与出厂 .so 契约一致；hmi_ui_load 后再切换工程表） */
    hmi_tags_adopt(NULL, NULL, 0);
    return 0;
}

/* ── 值转换 ── */

static float regs_to_float(const char* dtype, const uint16_t* r, int rn)
{
    if (rn < 2 && dtype_regs(dtype) == 2) return 0.0f;
    if (strcmp(dtype, "Bool") == 0)  return (r[0] != 0) ? 1.0f : 0.0f;
    if (strcmp(dtype, "I16") == 0)   return (float)(int16_t)r[0];
    /* ⚠ U32 必须按**无符号**拼：`r[0] << 16` 走的是有符号 int，≥0x8000 的寄存器
       会得到负值 ⇒ 读数变负（阶段 121 修复）。I32/Float 的转换本来就带符号/按位。 */
    if (strcmp(dtype, "U32") == 0)   return (float)(((uint32_t)r[0] << 16) | r[1]);
    if (strcmp(dtype, "I32") == 0)   return (float)(int32_t)(((uint32_t)r[0] << 16) | r[1]);
    if (strcmp(dtype, "Float") == 0) {
        uint32_t u = ((uint32_t)r[0] << 16) | r[1];
        float f; memcpy(&f, &u, 4);
        return f;
    }
    return (float)r[0];   /* U16 默认 */
}

static int float_to_regs(const char* dtype, float v, uint16_t* r, int rn)
{
    if (rn < 2 && dtype_regs(dtype) == 2) return -1;
    if (strcmp(dtype, "Bool") == 0) { r[0] = (v > 0.5f) ? 1 : 0; return 1; }
    if (strcmp(dtype, "I16") == 0)  { r[0] = (uint16_t)(int16_t)v; return 1; }
    /* 越界/负值先钳到目标域再转换 —— 直接把负数 float 转 uint32_t 是未定义行为 */
    if (strcmp(dtype, "U32") == 0) {
        double d = (v <= 0.0f) ? 0.0 : (v >= 4294967295.0f ? 4294967295.0 : (double)v);
        uint32_t u = (uint32_t)d;
        r[0] = (uint16_t)(u >> 16); r[1] = (uint16_t)(u & 0xFFFF);
        return 2;
    }
    if (strcmp(dtype, "I32") == 0) {
        double d = (v <= -2147483648.0f) ? -2147483648.0
                 : (v >= 2147483647.0f ? 2147483647.0 : (double)v);
        uint32_t u = (uint32_t)(int32_t)d;
        r[0] = (uint16_t)(u >> 16); r[1] = (uint16_t)(u & 0xFFFF);
        return 2;
    }
    if (strcmp(dtype, "Float") == 0) {
        uint32_t u; memcpy(&u, &v, 4);
        r[0] = (uint16_t)(u >> 16); r[1] = (uint16_t)(u & 0xFFFF);
        return 2;
    }
    r[0] = (uint16_t)v; return 1;   /* U16 默认 */
}

/* 通信线程：轮询一个组（round-robin），刷新缓存 */
void hmi_var_poll_once(void)
{
    if (s_group_count == 0) return;
    poll_group_t* g = &s_groups[s_poll_cursor];
    /* ⚠ 这行曾经是**无条件** stderr 输出，而本函数由通信线程按 ~100ms 轮询调用 ⇒
       现场日志被它刷满（阶段 121）。诊断信息保留，但改成编译期开关。 */
#ifdef HMI_VAR_DEBUG
    fprintf(stderr, "[hmi] poll gcount=%d cursor=%d drv=%d unit=%u fn=0x%02X off=%u count=%u\n",
            s_group_count, s_poll_cursor, (int)g->drv, g->unit, g->fn, g->off, g->count);
#endif
    s_poll_cursor = (s_poll_cursor + 1) % s_group_count;

    uint16_t regs[HMI_MAX_GROUP_REGS];
    if (hmi_comm_read_regs(g->drv, g->unit, g->fn, g->off, g->count, regs) != 0)
        return;

    pthread_mutex_lock(&s_lock);
    for (int j = 0; j < g->count; j++) {
        uint16_t ti = g->idx[j];
        if (ti == HMI_SLOT_EMPTY || ti >= g_count) continue;
        const tag_binding_t* b = &g_bind[ti];
        int rn = (int)g->count - j;   /* 剩余寄存器数 */
        if (dtype_regs(b->dtype) == 2 && rn < 2) continue;
        s_cache[ti] = regs_to_float(b->dtype, &regs[j], rn);
    }
    pthread_mutex_unlock(&s_lock);
}

/* 通信线程：消费写请求队列 */
void hmi_var_drain_writes(void)
{
    while (s_wq_head != s_wq_tail) {
        int idx = s_wq[s_wq_head].idx;
        float raw = s_wq[s_wq_head].raw;
        s_wq_head = (s_wq_head + 1) % HMI_WQ_SIZE;

        const tag_binding_t* b = &g_bind[idx];
        hmi_driver_t drv = hmi_driver_from_name(b->driver);
        if (drv != HMI_DRV_MODBUS_RTU && drv != HMI_DRV_MODBUS_TCP) continue;

        uint8_t fn; uint16_t off;
        if (hmi_tag_parse_addr(b->addr, &fn, &off) != 0) {
            fprintf(stderr, "[hmi] 变量写失败: %s 地址无法解析 (addr=%s)\n",
                    g_meta[idx].name, b->addr);
            continue;
        }

        uint16_t r[2];
        int n = float_to_regs(b->dtype, raw, r, 2);
        if (n < 0) {
            fprintf(stderr, "[hmi] 变量写失败: %s 数据类型无法编码 (dtype=%s)\n",
                    g_meta[idx].name, b->dtype ? b->dtype : "");
            continue;
        }

        /* 实际发出的功能码（`fn` 是地址串解析出来的**读**功能码，只有线圈分支与它一致；
           日志要记的是真正发出去的那个，否则现场照着日志查会觉得"写的怎么是 03"） */
        uint8_t wfn;
        int rc;
        if (fn == MB_FC_WRITE_COIL || (b->dtype && strcmp(b->dtype, "Bool") == 0)) {
            wfn = MB_FC_WRITE_COIL;
            rc = hmi_comm_write_reg(drv, b->unit, wfn, off, r[0]);
        } else if (n == 1) {
            wfn = MB_FC_WRITE_REG;
            rc = hmi_comm_write_reg(drv, b->unit, wfn, off, r[0]);
        } else {
            wfn = MB_FC_WRITE_MULTI_REG;
            rc = hmi_comm_write_regs(drv, b->unit, off, r, (uint16_t)n);
        }

        /* ⚠ 返回值**必须看**：RTU 写的回显校验曾经恒失败（拿读解析器校验写响应），
           而这里忽略 rc ⇒ 现场表现是"点了没反应"、日志里一个字都没有（阶段 120）。
           失败按**写请求**记一行 —— 写是事件驱动的，不是 100ms 轮询，不会刷屏。 */
        if (rc != 0) {
            fprintf(stderr, "[hmi] 变量写失败: %s (%s addr=%s unit=%u fn=%u off=%u) rc=%d\n",
                    g_meta[idx].name, b->driver, b->addr, (unsigned)b->unit,
                    (unsigned)wfn, (unsigned)off, rc);
        }
    }
}

/* MQTT 订阅消息回调：topic=<pre>/set/<name>，user=标签索引 */
static void on_mqtt_message(const char* topic, const char* payload, int len, void* user)
{
    if (!topic || !payload || len <= 0) return;
    /* 下行命令（<pre>/cmd/<target>）走**另一条路**：ACL / 信封 / 新鲜度 / 幂等台账 / 量程
     * 都在 mqtt_cmd_wiring → hmi_cmd_handle 里；与 /set/ 的"裸值直接写缓存"语义不同。 */
    if (hmi_cmd_wiring_is_cmd_topic(topic)) {
        hmi_cmd_wiring_on_message(topic, payload, len);
        return;
    }
    if (!user) return;
    char buf[64];
    int n = len < (int)sizeof(buf) - 1 ? len : (int)sizeof(buf) - 1;
    memcpy(buf, payload, (size_t)n);
    buf[n] = '\0';
    var_set_from_sub((int)(intptr_t)user, (float)atof(buf));
}

/* 开机时长 ms（单调时钟）—— 契约里 `tb=boot` 的时间基就指它。
 * 理由与 main.c 的 custom_tick_get 相同：设备无 RTC，墙钟会被 NTP/date 跳变。 */
unsigned long long hmi_var_uptime_ms(void)
{
    static unsigned long long start_ms = 0;
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    unsigned long long now_ms = (unsigned long long)ts.tv_sec * 1000ULL
                              + (unsigned long long)(ts.tv_nsec / 1000000);
    if (start_ms == 0) start_ms = now_ms;
    return now_ms - start_ms;
}

/* 上线报状态（M2，阶段 290）：CONNACK 成功后被 mqtt_client 回调一次。
 *   · 主题/payload 走 mqtt_contract 的纯函数（契约唯一实现）；
 *   · `t` 用**开机时长**、`tb=boot` 显式声明（设备无 RTC，与 /val/ 上行同一口径）；
 *   · **retain=1**：与 LWT 同主题同 retain ⇒ 远程端任何时刻订阅都能立刻拿到"在线/离线"，
 *     而不是等下一次变化。retain 不进主题/payload 契约（它是会话属性，不是报文内容）。
 * ⚠ 本函数由通信线程调用且**不持 s_lock**（回调点就在锁外）⇒ 里面可以安全 publish。 */
static void on_mqtt_connected(void)
{
    char topic[192], payload[64];
    if (hmi_mqtt_format_status_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE) != 0) return;
    if (hmi_mqtt_format_status_payload(payload, sizeof(payload), 1 /* on */,
                                       hmi_var_uptime_ms(), 0 /* tb=boot */) != 0) return;
    if (mqtt_publish(topic, payload, (int)strlen(payload), 1 /* retain */) != 0)
        fprintf(stderr, "[hmi] status publish failed (not connected?)\n");
}

/* MQTT 变化发布扫描：缓存值相对上次发布变化即发布（1s 周期） */
void hmi_var_mqtt_publish_scan(void)
{
    if (++s_poll_ticks < 10) return;   /* ~1s */
    s_poll_ticks = 0;

    pthread_mutex_lock(&s_lock);
    for (int i = 0; i < g_count; i++) {
        const tag_binding_t* b = &g_bind[i];
        if (hmi_driver_from_name(b->driver) != HMI_DRV_MQTT) continue;
        if (s_cache[i] == s_last_pub[i]) continue;
        s_last_pub[i] = s_cache[i];
        char topic[192], payload[64];
        /* 契约 v1（阶段 264）：主题与 payload 都走 `mqtt_contract.c` 的纯函数 —— 改造前这里
         * 是就地拼串，payload 只有 `%g`（无时间戳、无质量位），远程端无法判断"何时""可信吗"。
         * 时间基**显式声明** `tb=boot`：设备无 RTC，这里给的是开机时长 ms（与 main.c 的
         * custom_tick_get 同一个单调时钟口径，理由见那里的注释：墙钟会被 NTP 跳变）。 */
        if (hmi_mqtt_format_value_topic(topic, sizeof(topic), HMI_MQTT_TOPIC_PRE, g_meta[i].name) != 0)
            continue;
        if (hmi_mqtt_format_value_payload(payload, sizeof(payload), (double)s_cache[i],
                                          hmi_var_uptime_ms(), 0 /* tb=boot */, 1 /* q=1：质量位保留 */) != 0)
            continue;
        pthread_mutex_unlock(&s_lock);
        mqtt_publish(topic, payload, (int)strlen(payload), 0);
        pthread_mutex_lock(&s_lock);
    }
    pthread_mutex_unlock(&s_lock);
}
