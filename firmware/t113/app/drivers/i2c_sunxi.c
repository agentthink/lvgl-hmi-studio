/*
 * i2c_sunxi.c — I2C 驱动实现（/dev/i2c-x，ioctl I2C_RDWR）
 */
#include "i2c_sunxi.h"
#include <fcntl.h>
#include <unistd.h>
#include <string.h>
#include <sys/ioctl.h>
#include <linux/i2c.h>
#include <linux/i2c-dev.h>

int i2c_open(const char* dev)
{
    return open(dev, O_RDWR | O_NOCTTY);
}

int i2c_write_reg(int fd, uint8_t addr, uint8_t reg, const uint8_t* data, size_t len)
{
    uint8_t buf[32 + 1];
    if (len > sizeof(buf) - 1) return -1;
    buf[0] = reg;
    if (len) memcpy(&buf[1], data, len);

    struct i2c_msg msg;
    msg.addr = addr;
    msg.flags = 0;                    /* 写 */
    msg.len = (uint16_t)(1 + len);
    msg.buf = buf;
    struct i2c_rdwr_ioctl_data ioc;
    ioc.msgs = &msg;
    ioc.nmsgs = 1;
    return ioctl(fd, I2C_RDWR, &ioc) < 0 ? -1 : 0;
}

int i2c_read_reg(int fd, uint8_t addr, uint8_t reg, uint8_t* data, size_t len)
{
    if (len > 32) return -1;
    struct i2c_msg msgs[2];
    uint8_t regbuf = reg;

    msgs[0].addr = addr;
    msgs[0].flags = 0;                /* 写寄存器地址 */
    msgs[0].len = 1;
    msgs[0].buf = &regbuf;

    msgs[1].addr = addr;
    msgs[1].flags = I2C_M_RD;         /* 读数据 */
    msgs[1].len = (uint16_t)len;
    msgs[1].buf = data;

    struct i2c_rdwr_ioctl_data ioc;
    ioc.msgs = msgs;
    ioc.nmsgs = 2;
    return ioctl(fd, I2C_RDWR, &ioc) < 0 ? -1 : 0;
}

void i2c_close(int fd)
{
    if (fd >= 0) close(fd);
}
