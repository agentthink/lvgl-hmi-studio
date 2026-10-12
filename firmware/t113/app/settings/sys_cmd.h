/*
 * sys_cmd.h — 系统命令封装（T113 系统设置模块使用）
 *
 * 提供 system()/popen() 的安全包装，用于执行 date/ifconfig/udhcpc/stty
 * /df/hwclock 等 busybox 命令，并读取命令输出。
 */
#ifndef SYS_CMD_H
#define SYS_CMD_H

#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/* 执行命令，返回 system() 返回值（0 成功，非 0 失败） */
int sys_cmd_exec(const char* fmt, ...);

/* 执行命令并捕获输出（最多 outsz-1 字节），返回 0 成功 */
int sys_cmd_get(char* out, size_t outsz, const char* fmt, ...);

/* 读取文件首行内容到 buf（去掉换行），返回 0 成功 */
int sys_file_read_line(const char* path, char* buf, size_t bufsz);

/* 写字符串到文件，返回 0 成功 */
int sys_file_write(const char* path, const char* content);

#ifdef __cplusplus
}
#endif

#endif /* SYS_CMD_H */
