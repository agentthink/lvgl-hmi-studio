/*
 * hmi_alarm_source.c — 把 .so 的 `alarm_snapshot` 接成发布器的 fetch（阶段 291）
 *
 * 为什么单独一个编译单元：`hmi_ui_api.h` 拉 lvgl.h，而发布器与它的主机判据都不能拉
 * （firmware/t113/tests 刻意不解析 lvgl.h）。于是"取数"留在这里（设备侧），
 * 发布器只认函数指针（主机侧可注入假实现）。
 */
#include "hmi_alarm_source.h"
#include "hmi_ui_api.h"          /* hmi_ui_get_current_api / hmi_alarm_snapshot_t */
#include "mqtt_alarm_publish.h"
#include "variable_table.h"      /* hmi_var_uptime_ms */

static int fetch_from_ui(hmi_alarm_snapshot_t* out, int cap)
{
    const hmi_ui_api_t* api = hmi_ui_get_current_api();
    if (api == NULL || api->alarm_snapshot == NULL || out == NULL || cap <= 0) return 0;
    return (int)api->alarm_snapshot(out, (uint16_t)cap);
}

void hmi_alarm_publish_tick(void)
{
    /* `tb=boot`：与 /val/ 上行同一时间基（设备无 RTC，见 mqtt_contract.h）。 */
    hmi_alarm_publish_scan(fetch_from_ui, hmi_var_uptime_ms(), 0);
}
