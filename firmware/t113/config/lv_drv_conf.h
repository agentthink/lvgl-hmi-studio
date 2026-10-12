/**
 * lv_drv_conf.h — lv_drivers 配置（T113-S3，基于 Tina SDK lv_examples 模板）
 *
 * USE_SUNXIFB 1：显示走 /dev/fb0（sunxifb）
 * USE_EVDEV  1：EVDEV_NAME 指向触摸节点（电容 event2，电阻屏改 event0）
 */
/* clang-format off */
#if 1

#ifndef LV_DRV_CONF_H
#define LV_DRV_CONF_H

#include "lv_conf.h"

/*********************
 * DELAY INTERFACE
 *********************/
#define LV_DRV_DELAY_INCLUDE  <stdint.h>
#define LV_DRV_DELAY_US(us)  /*delay_us(us)*/
#define LV_DRV_DELAY_MS(ms)  /*delay_ms(ms)*/

/*********************
 * DISPLAY INTERFACE
 *********************/
#define LV_DRV_DISP_INCLUDE         <stdint.h>
#define LV_DRV_DISP_CMD_DATA(val)  /*pin_x_set(val)*/
#define LV_DRV_DISP_RST(val)       /*pin_x_set(val)*/

#define LV_DRV_DISP_SPI_CS(val)          /*spi_cs_set(val)*/
#define LV_DRV_DISP_SPI_WR_BYTE(data)    /*spi_wr(data)*/
#define LV_DRV_DISP_SPI_WR_ARRAY(adr, n) /*spi_wr_mem(adr, n)*/

#define LV_DRV_DISP_PAR_CS(val)          /*par_cs_set(val)*/
#define LV_DRV_DISP_PAR_SLOW             /*par_slow()*/
#define LV_DRV_DISP_PAR_FAST             /*par_fast()*/
#define LV_DRV_DISP_PAR_WR_WORD(data)    /*par_wr(data)*/
#define LV_DRV_DISP_PAR_WR_ARRAY(adr, n) /*par_wr_mem(adr,n)*/

/***************************
 * INPUT DEVICE INTERFACE
 ***************************/
#define LV_DRV_INDEV_INCLUDE     <stdint.h>
#define LV_DRV_INDEV_RST(val)    /*pin_x_set(val)*/
#define LV_DRV_INDEV_IRQ_READ    0

#define LV_DRV_INDEV_SPI_CS(val)            /*spi_cs_set(val)*/
#define LV_DRV_INDEV_SPI_XCHG_BYTE(data)    0

#define LV_DRV_INDEV_I2C_START              /*i2c_start()*/
#define LV_DRV_INDEV_I2C_STOP               /*i2c_stop()*/
#define LV_DRV_INDEV_I2C_RESTART            /*i2c_restart()*/
#define LV_DRV_INDEV_I2C_WR(data)           /*i2c_wr(data)*/
#define LV_DRV_INDEV_I2C_READ(last_read)    0

/*********************
 *  DISPLAY DRIVERS
 *********************/
#ifndef USE_SDL
# define USE_SDL 0
#endif
#ifndef USE_SDL_GPU
# define USE_SDL_GPU 0
#endif
#ifndef USE_MONITOR
# define USE_MONITOR         0
#endif
#ifndef USE_WINDOWS
# define USE_WINDOWS       0
#endif
#ifndef USE_WIN32DRV
# define USE_WIN32DRV       0
#endif
#ifndef USE_GTK
# define USE_GTK       0
#endif
#ifndef USE_WAYLAND
# define USE_WAYLAND       0
#endif
#ifndef USE_SSD1963
# define USE_SSD1963         0
#endif
#ifndef USE_R61581
# define USE_R61581          0
#endif
#ifndef USE_ST7565
# define USE_ST7565          0
#endif
#ifndef USE_GC9A01
# define USE_GC9A01          0
#endif
#ifndef USE_UC1610
# define USE_UC1610          0
#endif
#ifndef USE_SHARP_MIP
# define USE_SHARP_MIP       0
#endif
#ifndef USE_ILI9341
# define USE_ILI9341       0
#endif
#ifndef USE_FBDEV
# define USE_FBDEV           0
#endif
#ifndef USE_BSD_FBDEV
# define USE_BSD_FBDEV		0
#endif

/*-----------------------------------------
 *  Sunxi frame buffer device (/dev/fbx)
 *-----------------------------------------*/
#ifndef USE_SUNXIFB
#  define USE_SUNXIFB           1
#endif
#if USE_SUNXIFB
#  define SUNXIFB_PATH          "/dev/fb0"
#endif

#ifndef USE_DRM
#  define USE_DRM           0
#endif

/*********************
 *  INPUT DEVICES
 *********************/
#ifndef USE_XPT2046
#  define USE_XPT2046         0
#endif
#ifndef USE_FT5406EE8
#  define USE_FT5406EE8       0
#endif
#ifndef USE_AD_TOUCH
#  define USE_AD_TOUCH        0
#endif
#ifndef USE_MOUSE
#  define USE_MOUSE           0
#endif
#ifndef USE_MOUSEWHEEL
#  define USE_MOUSEWHEEL      0
#endif
#ifndef USE_LIBINPUT
#  define USE_LIBINPUT           0
#endif
#ifndef USE_BSD_LIBINPUT
#  define USE_BSD_LIBINPUT       0
#endif

/*-------------------------------------------------
 * Mouse or touchpad as evdev interface (Linux)
 *------------------------------------------------*/
#ifndef USE_EVDEV
#  define USE_EVDEV           1
#endif
#ifndef USE_BSD_EVDEV
#  define USE_BSD_EVDEV       0
#endif
#if USE_EVDEV || USE_BSD_EVDEV
#  define EVDEV_NAME   "/dev/input/event0"   /* 电阻触摸（配套屏为电阻屏）；电容屏改 "/dev/input/event2" */
#  define EVDEV_SWAP_AXES         0
#  define EVDEV_CALIBRATE         0
#  if EVDEV_CALIBRATE
#    define EVDEV_HOR_MIN         0
#    define EVDEV_HOR_MAX      4096
#    define EVDEV_VER_MIN         0
#    define EVDEV_VER_MAX      4096
#  endif
#endif

#ifndef USE_XKB
#  define USE_XKB           0
#endif
#ifndef USE_KEYBOARD
#  define USE_KEYBOARD        0
#endif

#endif /*LV_DRV_CONF_H*/

#endif /*End of "Content enable"*/
