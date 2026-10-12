/*
 * host_sys_cmd.c — PC 端系统命令模拟（返回模拟数据）
 *
 * 与板子 sys_cmd.c 同接口；在 PC 上模拟 ifconfig/df/cpuinfo/backlight 等
 * 输出，使设置界面显示真实感数据（IP/容量/序列号/背光节点）。
 * 配置文件路径 /etc/hmi/* 重映射到 /tmp/*，可在 PC 上模拟保存/加载流程。
 */
#include "sys_cmd.h"
#include <stdio.h>
#include <stdlib.h>
#include <stdarg.h>
#include <string.h>

#define SYS_CMD_MAX_LEN 512

/* 把 /etc/hmi/ 前缀映射到 /tmp/（PC 无该目录，模拟持久化） */
static void map_conf_path(const char* src, char* dst, size_t sz)
{
    const char* prefix = "/etc/hmi/";
    if (strncmp(src, prefix, strlen(prefix)) == 0) {
        snprintf(dst, sz, "/tmp/%s", src + strlen(prefix));
    } else {
        snprintf(dst, sz, "%s", src);
    }
}

int sys_cmd_exec(const char* fmt, ...)
{
    char cmd[SYS_CMD_MAX_LEN];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(cmd, sizeof(cmd), fmt, ap);
    va_end(ap);
    fprintf(stderr, "[host-cmd] exec: %s\n", cmd);
    /* 模拟：test -x 检查脚本存在 → 成功；其余一律成功返回 */
    if (strstr(cmd, "test -x")) return 0;
    return 0;
}

int sys_cmd_get(char* out, size_t outsz, const char* fmt, ...)
{
    char cmd[SYS_CMD_MAX_LEN];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(cmd, sizeof(cmd), fmt, ap);
    va_end(ap);

    const char* data = "";
    if (strstr(cmd, "carrier")) {
        data = "1\n";                                    /* eth0 已连接 */
    } else if (strstr(cmd, "ifconfig eth0")) {
        data = "eth0      Link encap:Ethernet  HWaddr 02:11:22:33:44:55\n"
               "          inet addr:192.168.1.100  Bcast:192.168.1.255  Mask:255.255.255.0\n"
               "          UP BROADCAST RUNNING MULTICAST  MTU:1500  Metric:1\n";
    } else if (strstr(cmd, "1,3p")) {
        data = "Filesystem      Size  Used Available Use% Mounted on\n"
               "/dev/ubi0_7     163M   12M     151M   8% /mnt/UDISK\n"
               "/dev/root       32M   26M      6.3M  82% /\n";
    } else if (strstr(cmd, "df -h /mnt/UDISK 2>/dev/null")) {
        data = "/dev/ubi0_7 163M 151M 可用\n";
    } else if (strstr(cmd, "df -h / 2>/dev/null")) {
        data = "/dev/root 32M 6.3M 可用\n";
    } else if (strstr(cmd, "/proc/cpuinfo")) {
        data = "8b7f6a5c4d3e2f10\n";                /* 模拟 sed 提取后的 Serial 值（单行） */
    } else if (strstr(cmd, "ls /sys/class/backlight")) {
        data = "pwm-backlight\n";                        /* 背光节点 */
    }

    if (outsz > 0) out[0] = '\0';
    size_t n = strlen(data);
    if (n >= outsz) n = outsz - 1;
    memcpy(out, data, n);
    out[n] = '\0';
    fprintf(stderr, "[host-cmd] get: %s => \"%s\"\n", cmd, out[0] ? out : "(empty)");
    return 0;
}

int sys_file_read_line(const char* path, char* buf, size_t bufsz)
{
    char mapped[256];
    map_conf_path(path, mapped, sizeof(mapped));
    FILE* fp = fopen(mapped, "r");
    if (!fp) return -1;
    if (bufsz > 0) buf[0] = '\0';
    if (fgets(buf, (int)bufsz, fp) == NULL) {
        fclose(fp);
        return -1;
    }
    fclose(fp);
    size_t n = strlen(buf);
    while (n > 0 && (buf[n-1] == '\n' || buf[n-1] == '\r')) buf[--n] = '\0';
    return 0;
}

int sys_file_write(const char* path, const char* content)
{
    char mapped[256];
    map_conf_path(path, mapped, sizeof(mapped));
    FILE* fp = fopen(mapped, "w");
    if (!fp) return -1;
    size_t n = strlen(content);
    size_t w = fwrite(content, 1, n, fp);
    fclose(fp);
    fprintf(stderr, "[host-cmd] write %s (%zu bytes)\n", mapped, n);
    return (w == n) ? 0 : -1;
}
