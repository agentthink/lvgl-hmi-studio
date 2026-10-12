#include "mqtt_write_guard.h"
#include <math.h>
#include <stdlib.h>
#include <string.h>

const char* hmi_mqtt_write_issue_token(int code)
{
    switch (code) {
        case HMI_MQTT_WRITE_OK:            return NULL;
        case HMI_MQTT_WRITE_READ_ONLY:     return "read-only";
        case HMI_MQTT_WRITE_NOT_A_NUMBER:  return "not-a-number";
        case HMI_MQTT_WRITE_OUT_OF_RANGE:  return "out-of-range";
        case HMI_MQTT_WRITE_BAD_TARGET:    return "bad-target";
        default:                           return NULL;
    }
}

/* 严格解析：整串都要是数字（不许空白、不许尾随垃圾、不许 NaN/inf） */
static int parse_strict(const char* text, double* out)
{
    char* end = NULL;
    double v;

    if (text == NULL || text[0] == '\0') return -1;
    for (const char* p = text; *p != '\0'; p++)
        if (*p == ' ' || *p == '\t' || *p == '\n' || *p == '\r') return -1;

    v = strtod(text, &end);
    if (end == text || end == NULL || *end != '\0') return -1;
    if (isnan(v) || isinf(v)) return -1;                   /* "nan"/"inf" 文本也拒 */

    *out = v;
    return 0;
}

int hmi_mqtt_write_check(int writable, double min, double max, const char* value_text,
                         double* out_value, const char** reason)
{
    double v = 0.0;
    const char** set_reason = reason;

    if (min > max) {
        if (set_reason) *set_reason = hmi_mqtt_write_issue_token(HMI_MQTT_WRITE_BAD_TARGET);
        return HMI_MQTT_WRITE_BAD_TARGET;
    }

    /* 只读优先：值再合法也不许写 */
    if (writable == 0) {
        if (set_reason) *set_reason = hmi_mqtt_write_issue_token(HMI_MQTT_WRITE_READ_ONLY);
        return HMI_MQTT_WRITE_READ_ONLY;
    }

    if (parse_strict(value_text, &v) != 0) {
        if (set_reason) *set_reason = hmi_mqtt_write_issue_token(HMI_MQTT_WRITE_NOT_A_NUMBER);
        return HMI_MQTT_WRITE_NOT_A_NUMBER;
    }

    if (v < min || v > max) {
        if (set_reason) *set_reason = hmi_mqtt_write_issue_token(HMI_MQTT_WRITE_OUT_OF_RANGE);
        return HMI_MQTT_WRITE_OUT_OF_RANGE;
    }

    if (out_value != NULL) *out_value = v;
    if (set_reason) *set_reason = NULL;
    return HMI_MQTT_WRITE_OK;
}
