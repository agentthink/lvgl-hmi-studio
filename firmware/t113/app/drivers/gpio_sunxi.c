/*
 * gpio_sunxi.c — GPIO 驱动实现（sysfs）
 *
 * 说明：系统默认将 PE0/PE1/PE8/PE9 初始化为 ledc 输出，
 * 本驱动通过 /sys/class/gpio 直接控制电平；若需输入捕获，
 * 按《硬件接口》文档删除 dts 中 ledc 配置并重编译镜像。
 */
#include "gpio_sunxi.h"
#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <fcntl.h>
#include <errno.h>

static void sysfs_write(const char* path, const char* val)
{
    int fd = open(path, O_WRONLY);
    if (fd < 0) return;
    if (write(fd, val, strlen(val)) < 0) { /* ignore */ }
    close(fd);
}

int gpio_export_out(int pin)
{
    char path[64], val[16];

    snprintf(val, sizeof(val), "%d\n", pin);
    sysfs_write("/sys/class/gpio/export", val);

    snprintf(path, sizeof(path), "/sys/class/gpio/gpio%d/direction", pin);
    sysfs_write(path, "out");

    snprintf(path, sizeof(path), "/sys/class/gpio/gpio%d/value", pin);
    int fd = open(path, O_WRONLY);
    if (fd < 0) return -1;   /* 被 ledc 占用或导出失败 */
    close(fd);
    return 0;
}

int gpio_write(int pin, int val)
{
    char path[64], buf[8];
    snprintf(path, sizeof(path), "/sys/class/gpio/gpio%d/value", pin);
    int fd = open(path, O_WRONLY);
    if (fd < 0) return -1;
    snprintf(buf, sizeof(buf), "%d\n", val ? 1 : 0);
    int rc = (write(fd, buf, strlen(buf)) > 0) ? 0 : -1;
    close(fd);
    return rc;
}

int gpio_read(int pin)
{
    char path[64];
    snprintf(path, sizeof(path), "/sys/class/gpio/gpio%d/value", pin);
    int fd = open(path, O_RDONLY);
    if (fd < 0) return -1;
    char buf[8] = {0};
    ssize_t n = read(fd, buf, sizeof(buf) - 1);
    close(fd);
    if (n <= 0) return -1;
    return buf[0] == '1' ? 1 : 0;
}

void gpio_unexport(int pin)
{
    char val[16];
    snprintf(val, sizeof(val), "%d\n", pin);
    sysfs_write("/sys/class/gpio/unexport", val);
}
