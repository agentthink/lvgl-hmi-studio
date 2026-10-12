/*
 * sys_cmd.c — 系统命令封装实现
 */
#include "sys_cmd.h"
#include <stdio.h>
#include <stdlib.h>
#include <stdarg.h>
#include <string.h>
#include <errno.h>

#define SYS_CMD_MAX_LEN 512

int sys_cmd_exec(const char* fmt, ...)
{
    char cmd[SYS_CMD_MAX_LEN];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(cmd, sizeof(cmd), fmt, ap);
    va_end(ap);
    return system(cmd);
}

int sys_cmd_get(char* out, size_t outsz, const char* fmt, ...)
{
    char cmd[SYS_CMD_MAX_LEN];
    char line[256];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(cmd, sizeof(cmd), fmt, ap);
    va_end(ap);

    FILE* fp = popen(cmd, "r");
    if (!fp) return -1;
    size_t used = 0;
    if (outsz > 0) out[0] = '\0';
    while (fgets(line, sizeof(line), fp) && used + 1 < outsz) {
        size_t n = strlen(line);
        if (used + n >= outsz) n = outsz - used - 1;
        memcpy(out + used, line, n);
        used += n;
        out[used] = '\0';
    }
    pclose(fp);
    return 0;
}

int sys_file_read_line(const char* path, char* buf, size_t bufsz)
{
    FILE* fp = fopen(path, "r");
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
    FILE* fp = fopen(path, "w");
    if (!fp) return -1;
    size_t n = strlen(content);
    size_t w = fwrite(content, 1, n, fp);
    fclose(fp);
    return (w == n) ? 0 : -1;
}
