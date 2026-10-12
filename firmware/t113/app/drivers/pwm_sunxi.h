/*
 * pwm_sunxi.h — PWM 驱动（/sys/class/pwm，见《硬件接口》）
 *
 * T113 提供 2 路可编程 PWM：PWM2、PWM3（需在 dts 使能 pwm 节点，
 * 见 tina/dts/README.md）。PWM2 常用于 LCD 背光调节。
 */
#ifndef HMI_PWM_SUNXI_H
#define HMI_PWM_SUNXI_H

#include <stdint.h>

/* chip=sysfs pwmchip 编号, n=通道名（"2"/"3"）。成功返回 0 */
int pwm_init(int chip, const char* n);

/* 周期/占空比（纳秒） */
int pwm_set_period_ns(int chip, const char* n, uint32_t ns);
int pwm_set_duty_ns(int chip, const char* n, uint32_t ns);

/* 使能/关闭输出 */
int pwm_set_enable(int chip, const char* n, int on);

/* 便捷函数：按占空比百分比（0-1000，千分比精度）配置 */
int pwm_set_duty_percent(int chip, const char* n, uint16_t percent_x1000);

#endif /* HMI_PWM_SUNXI_H */
