/*
 * lcd_sunxifb.h — 显示驱动（sunxifb，见《Linux-T113开发文档-LVGL篇》）
 *
 * sunxifb 接口：
 *   sunxifb_init(rotated)  初始化显示引擎（rotated: LV_DISP_ROT_NONE/90/180/270）
 *   sunxifb_get_sizes()    获取屏幕分辨率（应用无需写死分辨率）
 *   sunxifb_alloc/free     申请系统绘图内存（g2d 需要物理连续内存）
 *   sunxifb_flush          帧缓冲刷新回调（应用不要直接调用）
 */
#ifndef HMI_LCD_SUNXIFB_H
#define HMI_LCD_SUNXIFB_H

/* 初始化显示并注册 LVGL display 驱动。成功返回 0 */
int lcd_sunxifb_init(void);
void lcd_sunxifb_deinit(void);

#endif /* HMI_LCD_SUNXIFB_H */
