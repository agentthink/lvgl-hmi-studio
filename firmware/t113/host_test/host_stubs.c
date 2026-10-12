/*
 * host_stubs.c — PC 端宿主桩实现
 *
 * settings 模块依赖的宿主接口在 PC 上无真实设备，提供最小桩：
 *   - 屏幕管理：当前屏固定为索引 0，切换为空操作
 *   - .so UI：未加载（hmi_ui_get_current_api 返回 NULL）
 *   - 变量表 / 通信：空实现
 */
#include "screen_manager.h"
#include "hmi_ui_api.h"
#include "variable_table.h"
#include "comm_core.h"

int hmi_screen_current_idx(void)
{
    return 0;
}

int hmi_screen_switch_idx(int index)
{
    (void)index;
    return 0;
}

int hmi_screen_switch(const char* name)
{
    (void)name;
    return 0;
}

void hmi_screens_init(void)
{
}

int hmi_ui_load(void)
{
    return -1;
}

void hmi_ui_unload(void)
{
}

const hmi_ui_api_t* hmi_ui_get_current_api(void)
{
    return NULL;
}

float var_raw_read(int index)
{
    (void)index;
    return 0.0f;
}

void var_raw_write(int index, float raw)
{
    (void)index;
    (void)raw;
}

int hmi_var_init(void)
{
    return 0;
}

int hmi_comm_init(void)
{
    return 0;
}

int hmi_comm_start(void)
{
    return 0;
}
