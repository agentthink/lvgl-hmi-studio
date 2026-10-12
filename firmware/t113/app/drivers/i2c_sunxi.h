/*
 * i2c_sunxi.h — I2C 驱动（/dev/i2c-x）
 *
 * 节点（见《硬件接口》）：/dev/i2c-0, /dev/i2c-1, /dev/i2c-3
 * I2C 复用串口管脚，需修改 dts 将 GPIO/串口切换为 I2C 并重编译镜像
 * （例如 i2c-3 使用 PE6/PE7，需移除 UART5 配置），见 tina/dts/README.md。
 */
#ifndef HMI_I2C_SUNXI_H
#define HMI_I2C_SUNXI_H

#include <stdint.h>
#include <stddef.h>

/* 打开设备节点，返回 fd（-1 失败） */
int i2c_open(const char* dev);

/* 写从机寄存器：addr=7位从机地址, reg=寄存器地址, data=数据 */
int i2c_write_reg(int fd, uint8_t addr, uint8_t reg, const uint8_t* data, size_t len);

/* 读从机寄存器（组合事务：先写寄存器地址再读数据） */
int i2c_read_reg(int fd, uint8_t addr, uint8_t reg, uint8_t* data, size_t len);

void i2c_close(int fd);

#endif /* HMI_I2C_SUNXI_H */
