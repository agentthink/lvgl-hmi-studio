/*
 * gpio_sunxi.h — GPIO 驱动（sysfs 接口）
 *
 * 管脚（见《硬件接口》）：开发板顶部双排针 PE0~PE9
 *   PE0,PE1,PE8,PE9 普通 GPIO（系统默认经 ledc 初始化为输出）
 *   PE2~PE7 默认作 3 路串口（UART2/4/5），改用 GPIO/I2C 需修改 dts
 * sysfs 编号：PE 组基址 128 → PE0=128, PE1=129, PE8=136, PE9=137
 * （如 Tina 内核 GPIO 基址不同，请以 /sys/kernel/debug/gpio 或 gpioinfo 实测为准）
 */
#ifndef HMI_GPIO_SUNXI_H
#define HMI_GPIO_SUNXI_H

#include <stdint.h>

/* 导出并配置为输出；返回 0 成功（被 ledc 占用时返回 -1） */
int gpio_export_out(int pin);

/* 输出电平 0/1 */
int gpio_write(int pin, int val);
/* 读取电平（需在 dts 删除 ledc 占用后自行初始化为输入） */
int gpio_read(int pin);

void gpio_unexport(int pin);

#endif /* HMI_GPIO_SUNXI_H */
