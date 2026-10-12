/*
 * pwm_sunxi.c — PWM 驱动实现（/sys/class/pwm）
 */
#include "pwm_sunxi.h"
#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <fcntl.h>
#include <stdlib.h>

static void pwm_sysfs(const char* path, const char* val)
{
    int fd = open(path, O_WRONLY);
    if (fd < 0) return;
    if (write(fd, val, strlen(val)) < 0) { /* ignore */ }
    close(fd);
}

static void pwm_sysfs_num(const char* path, uint32_t val)
{
    char buf[32];
    snprintf(buf, sizeof(buf), "%u\n", val);
    pwm_sysfs(path, buf);
}

static void pwm_path(int chip, const char* n, const char* attr, char* out, size_t cap)
{
    snprintf(out, cap, "/sys/class/pwm/pwmchip%d/pwm%s/%s", chip, n, attr);
}

int pwm_init(int chip, const char* n)
{
    char path[96];
    snprintf(path, sizeof(path), "/sys/class/pwm/pwmchip%d/pwm%s", chip, n);
    if (access(path, F_OK) == 0) return 0;   /* 已导出 */

    char buf[16];
    snprintf(buf, sizeof(buf), "%s\n", n);
    snprintf(path, sizeof(path), "/sys/class/pwm/pwmchip%d/export", chip);
    pwm_sysfs(path, buf);
    return 0;
}

int pwm_set_period_ns(int chip, const char* n, uint32_t ns)
{
    char path[96];
    pwm_path(chip, n, "period", path, sizeof(path));
    pwm_sysfs_num(path, ns);
    return 0;
}

int pwm_set_duty_ns(int chip, const char* n, uint32_t ns)
{
    char path[96];
    pwm_path(chip, n, "duty_cycle", path, sizeof(path));
    pwm_sysfs_num(path, ns);
    return 0;
}

int pwm_set_enable(int chip, const char* n, int on)
{
    char path[96];
    pwm_path(chip, n, "enable", path, sizeof(path));
    pwm_sysfs(path, on ? "1\n" : "0\n");
    return 0;
}

int pwm_set_duty_percent(int chip, const char* n, uint16_t percent_x1000)
{
    /* 需先设置周期；占空比 = 周期 * 千分比 / 1000 */
    char path[96];
    uint32_t period = 0;
    char buf[32];

    pwm_path(chip, n, "period", path, sizeof(path));
    int fd = open(path, O_RDONLY);
    if (fd < 0) return -1;
    ssize_t r = read(fd, buf, sizeof(buf) - 1);
    close(fd);
    if (r <= 0) return -1;
    buf[r] = '\0';
    period = (uint32_t)strtoul(buf, NULL, 10);
    if (period == 0) return -1;

    uint32_t duty = (uint32_t)((uint64_t)period * percent_x1000 / 1000);
    pwm_path(chip, n, "duty_cycle", path, sizeof(path));
    pwm_sysfs_num(path, duty);
    return 0;
}
