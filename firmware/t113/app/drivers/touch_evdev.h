/*
 * touch_evdev.h — 触摸驱动（evdev，见《Linux-T113开发文档-硬件接口》）
 *
 * 设备节点：
 *   电阻触摸 /dev/input/event0    音频插口 /dev/input/event1    电容触摸 /dev/input/event2
 * 默认电容触摸（HMI_TOUCH_DEV_CAP）；切换电阻屏改 runtime_config.h 的 HMI_TOUCH_DEV，
 * 并同时修改 lv_drv_conf.h 的 EVDEV_NAME。
 */
#ifndef HMI_TOUCH_EVDEV_H
#define HMI_TOUCH_EVDEV_H

/* 注册 LVGL 指针输入设备。成功返回 0（节点不存在返回 -1，不阻塞启动） */
int touch_evdev_init(const char* node);
void touch_evdev_deinit(void);

#endif /* HMI_TOUCH_EVDEV_H */
