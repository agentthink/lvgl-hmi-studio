/*
 * set_serial.c — 串口设置模块
 *
 * 配置串口号/波特率/数据位/停止位/校验位，提供参数测试与应用。
 * 保存路径：/etc/hmi/serial.conf（文本格式：dev baud data stop parity）
 * 布局：5 行 44px 行距，控件 16px，底部统一操作栏。
 */
#include "sys_settings.h"
#include "settings_ui.h"
#include "sys_cmd.h"
#include "hmi_runtime.h"
#include "runtime_config.h"
#include <stdio.h>
#include <string.h>
#include <fcntl.h>
#include <unistd.h>

static lv_obj_t* s_dd_dev = NULL;
static lv_obj_t* s_dd_baud = NULL;
static lv_obj_t* s_dd_data = NULL;
static lv_obj_t* s_dd_stop = NULL;
static lv_obj_t* s_dd_parity = NULL;
static lv_obj_t* s_result_lbl = NULL;

#define SERIAL_CONF "/etc/hmi/serial.conf"

/* 可选串口与默认项按板级提供（见 app/runtime/board_profile.h），本文件不再写 #if 分支 */
static const char* SERIAL_DEVS[] = HMI_SERIAL_DEVS_STTY;
static const char* SERIAL_BAUDS[] = { "9600", "19200", "38400", "57600", "115200" };

/* 构造 stty 参数 */
static void build_stty(char* cmd, size_t sz)
{
    int dev = lv_dropdown_get_selected(s_dd_dev);
    int baud = lv_dropdown_get_selected(s_dd_baud);
    int data = lv_dropdown_get_selected(s_dd_data);   /* 0..3 → 5..8 */
    int stop = lv_dropdown_get_selected(s_dd_stop);   /* 0/1 → 1/2 */
    int par = lv_dropdown_get_selected(s_dd_parity);  /* 0 None 1 Even 2 Odd */

    snprintf(cmd, sz, "stty -F %s %s cs%d %s %s 2>&1",
             SERIAL_DEVS[dev], SERIAL_BAUDS[baud], data + 5,
             stop == 0 ? "-cstopb" : "cstopb",
             par == 0 ? "-parenb" : (par == 1 ? "parenb -parodd" : "parenb parodd"));
}

static void do_apply(void)
{
    char cmd[256];
    build_stty(cmd, sizeof(cmd));
    int rc = sys_cmd_exec(cmd);
    if (rc != 0) {
        lv_label_set_text(s_result_lbl, "应用失败：stty 执行出错");
        return;
    }
    /* 保存配置 */
    int dev = lv_dropdown_get_selected(s_dd_dev);
    int baud = lv_dropdown_get_selected(s_dd_baud);
    int data = lv_dropdown_get_selected(s_dd_data);
    int stop = lv_dropdown_get_selected(s_dd_stop);
    int par = lv_dropdown_get_selected(s_dd_parity);
    char conf[128];
    snprintf(conf, sizeof(conf), "%s %s %d %d %d\n",
             SERIAL_DEVS[dev], SERIAL_BAUDS[baud], data + 5, stop + 1, par);
    sys_file_write(SERIAL_CONF, conf);
    lv_label_set_text(s_result_lbl, "串口参数已应用并保存");
    HMI_LOG("sys_settings: serial apply %s %s %d-%d par=%d",
            SERIAL_DEVS[dev], SERIAL_BAUDS[baud], data + 5, stop + 1, par);
}

static void do_test(void)
{
    char cmd[256];
    build_stty(cmd, sizeof(cmd));
    if (sys_cmd_exec(cmd) != 0) {
        lv_label_set_text(s_result_lbl, "测试失败：串口参数配置错误");
        return;
    }
    int dev = lv_dropdown_get_selected(s_dd_dev);
    int fd = open(SERIAL_DEVS[dev], O_RDWR | O_NOCTTY | O_NONBLOCK);
    if (fd < 0) {
        lv_label_set_text(s_result_lbl, "测试失败：无法打开串口设备");
        return;
    }
    /* 发送测试字节（无回环时提示检查接线） */
    const char test[] = "HMI TEST\r\n";
    ssize_t n = write(fd, test, sizeof(test) - 1);
    close(fd);
    if (n < 0) {
        lv_label_set_text(s_result_lbl, "测试失败：发送数据出错");
    } else {
        lv_label_set_text(s_result_lbl, "测试通过：参数有效，已发送测试帧");
    }
}

static void btn_test_cb(lv_event_t* e) { (void)e; do_test(); }
static void btn_apply_cb(lv_event_t* e) { (void)e; do_apply(); }

static lv_obj_t* make_dd(lv_obj_t* parent, const char* name, int y, const char* opts, int sel)
{
    lv_obj_t* lbl = lv_label_create(parent);
    lv_label_set_text(lbl, name);
    lv_obj_set_style_text_font(lbl, SET_FONT_BODY, 0);
    lv_obj_align(lbl, LV_ALIGN_TOP_LEFT, SET_PAD_X, y + 9);

    lv_obj_t* dd = lv_dropdown_create(parent);
    lv_obj_set_size(dd, 240, 38);
    lv_obj_align(dd, LV_ALIGN_TOP_LEFT, SET_CTRL_X, y);
    lv_dropdown_set_options(dd, opts);
    lv_dropdown_set_selected(dd, sel);
    set_dd_font(dd, g_font_cn_16);
    return dd;
}

lv_obj_t* set_serial_create(lv_obj_t* parent)
{
    int w = set_ui_w(parent);

    /* 内容区被 lv_obj_clean 清空后，下列静态指针已失效；先置 NULL，
     * 防止按钮/下拉等回调在重建前访问已释放对象（UAF）。 */
    s_dd_dev = NULL;
    s_dd_baud = NULL;
    s_dd_data = NULL;
    s_dd_stop = NULL;
    s_dd_parity = NULL;
    s_result_lbl = NULL;

    s_result_lbl = lv_label_create(parent);
    lv_obj_set_style_text_font(s_result_lbl, SET_FONT_TITLE, 0);
    lv_obj_set_style_text_color(s_result_lbl, lv_color_hex(SET_CLR_HIL), 0);
    lv_obj_align(s_result_lbl, LV_ALIGN_TOP_LEFT, SET_PAD_X, SET_PAD_TB);
    lv_label_set_text(s_result_lbl, "串口参数配置");

    s_dd_dev = make_dd(parent, "串口号", 40,
        HMI_SERIAL_DEVS_MENU, HMI_SERIAL_DEVS_DEFAULT);
    s_dd_baud = make_dd(parent, "波特率", 84,
        "9600\n19200\n38400\n57600\n115200", 4);
    s_dd_data = make_dd(parent, "数据位", 128, "5\n6\n7\n8", 3);
    s_dd_stop = make_dd(parent, "停止位", 172, "1\n2", 0);
    s_dd_parity = make_dd(parent, "校验位", 216, "None\nEven\nOdd", 0);

    /* 右侧说明 */
    lv_obj_t* tip = lv_label_create(parent);
    lv_label_set_text(tip, "提示：测试将向所选串口发送一帧\n测试数据；无回环接线时请用示波\n器或短接 TX/RX 验证。");
    lv_obj_set_style_text_font(tip, SET_FONT_BODY, 0);
    lv_obj_set_style_text_color(tip, lv_color_hex(SET_CLR_SUB), 0);
    lv_obj_align(tip, LV_ALIGN_TOP_RIGHT, -SET_PAD_X, 96);

    /* 按钮（底部居中） */
    int btn_y = set_ui_btn_y(parent);
    int bx = (w - 300) / 2;
    set_ui_btn(parent, "参数测试", bx, btn_y, 140, SET_CLR_BTN, btn_test_cb);
    set_ui_btn(parent, "应用", bx + 160, btn_y, 140, SET_CLR_OK, btn_apply_cb);

    /* 读取已保存配置填充 */
    char conf[128];
    if (sys_file_read_line(SERIAL_CONF, conf, sizeof(conf)) == 0) {
        char dev[32] = "/dev/ttyS4"; int baud = 115200, data = 8, stop = 1, par = 0;
        if (sscanf(conf, "%31s %d %d %d %d", dev, &baud, &data, &stop, &par) == 5) {
            for (int i = 0; i < 4; i++)
                if (strcmp(SERIAL_DEVS[i], dev) == 0) lv_dropdown_set_selected(s_dd_dev, i);
            /* 按数值匹配波特率 */
            char bs[8]; snprintf(bs, sizeof(bs), "%d", baud);
            for (int i = 0; i < 5; i++)
                if (strcmp(SERIAL_BAUDS[i], bs) == 0) lv_dropdown_set_selected(s_dd_baud, i);
            lv_dropdown_set_selected(s_dd_data, data >= 5 ? data - 5 : 3);
            lv_dropdown_set_selected(s_dd_stop, stop >= 2 ? 1 : 0);
            lv_dropdown_set_selected(s_dd_parity, par < 3 ? par : 0);
        }
    }

    return parent;
}
