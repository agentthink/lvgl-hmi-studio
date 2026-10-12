/**
 * @file sunxifb.c
 *
 */

/*********************
 *      INCLUDES
 *********************/
#include "sunxifb.h"
#include "hmi_runtime.h"   /* HMI_LOG（诊断日志） */
#if USE_SUNXIFB

#include <stdlib.h>
#include <unistd.h>
#include <stddef.h>
#include <stdio.h>
#include <fcntl.h>
#include <sys/mman.h>
#include <sys/ioctl.h>
#include <linux/fb.h>

#ifdef LV_USE_SUNXIFB_DEBUG
#include <sys/time.h>
#endif /* LV_USE_SUNXIFB_DEBUG */

#if USE_SUNXIFB_G2D
#include "sunximem.h"
#include "sunxig2d.h"
#endif /* USE_SUNXIFB_G2D */

/*********************
 *      DEFINES
 *********************/
#ifndef SUNXIFB_PATH
#define SUNXIFB_PATH  "/dev/fb0"
#endif

/**********************
 *      TYPEDEFS
 **********************/

/**********************
 *      STRUCTURES
 **********************/

/**********************
 *  STATIC PROTOTYPES
 **********************/

/**********************
 *  STATIC VARIABLES
 **********************/
static struct fb_var_screeninfo vinfo;
static struct fb_fix_screeninfo finfo;
static char *fbp = 0;
static long int screensize = 0;
static int fbfd = 0;
static uint32_t fbp_w;
static uint32_t fbp_h;
static uint32_t fbp_line_length;

#ifdef USE_SUNXIFB_DOUBLE_BUFFER
#define FBIO_CACHE_SYNC         0x4630
#define FBIO_ENABLE_CACHE       0x4631
#define FBIO_GET_IONFD          0x4632
#define FBIO_GET_PHY_ADDR       0x4633
#define FBIOGET_DMABUF          _IOR('F', 0x21, struct fb_dmabuf_export)

struct sunxifb_info {
    char *screenfbp[2];
    uint32_t fbnum;
    uint32_t fbindex;
    volatile bool dbuf_en;
#if USE_SUNXIFB_G2D
#if USE_SUNXIFB_G2D_ROTATE
    g2d_blt_flags_h rotated;
    char *rotatefbp;
    uintptr_t rotatefbp_phy;
    uint32_t rotatefbp_w;
    uint32_t rotatefbp_h;
#endif /* USE_SUNXIFB_G2D_ROTATE */
#endif /* USE_SUNXIFB_G2D */
};

static struct sunxifb_info sinfo;
#endif /* USE_SUNXIFB_DOUBLE_BUFFER */

/**********************
 *      MACROS
 **********************/

/**********************
 *   GLOBAL FUNCTIONS
 **********************/
void sunxifb_init(uint32_t rotated) {
    // Open the file for reading and writing
    fbfd = open(SUNXIFB_PATH, O_RDWR);
    if (fbfd == -1) {
        perror("Error: cannot open framebuffer device");
        return;
    }

    // Get fixed screen information
    if (ioctl(fbfd, FBIOGET_FSCREENINFO, &finfo) == -1) {
        perror("Error reading fixed information");
        return;
    }

    // Get variable screen information
    if (ioctl(fbfd, FBIOGET_VSCREENINFO, &vinfo) == -1) {
        perror("Error reading variable information");
        return;
    }

#if defined(USE_SUNXIFB_DOUBLE_BUFFER) && defined(USE_SUNXIFB_CACHE)
    // Speed up the speed of buf copy
    uintptr_t args[2] = { 1, 0 };
    if (ioctl(fbfd, FBIO_ENABLE_CACHE, args) < 0) {
        perror("Error: FBIO_ENABLE_CACHE fail");
    }
#endif /* USE_SUNXIFB_CACHE */

    // Figure out the size of the screen in bytes
    screensize = finfo.smem_len; //finfo.line_length * vinfo.yres;

    // Map the device to memory
    HMI_LOG_DEBUG("fb smem_len=%u line_len=%u xres=%u yres=%u bpp=%u",
            (unsigned)finfo.smem_len, (unsigned)finfo.line_length,
            (unsigned)vinfo.xres, (unsigned)vinfo.yres,
            (unsigned)vinfo.bits_per_pixel);
    fbp = (char*) mmap(0, screensize, PROT_READ | PROT_WRITE, MAP_SHARED, fbfd,
            0);
    HMI_LOG_DEBUG("fb mmap -> %p (size=%u)", fbp, (unsigned)screensize);
    if ((intptr_t) fbp == -1) {
        perror("Error: failed to map framebuffer device to memory");
        return;
    }

    HMI_LOG_DEBUG("wh=%dx%d, vwh=%dx%d, bpp=%d, rotated=%d", vinfo.xres, vinfo.yres,
            vinfo.xres_virtual, vinfo.yres_virtual, vinfo.bits_per_pixel,
            rotated);

    fbp_w = vinfo.xres;
    fbp_h = vinfo.yres;
    fbp_line_length = finfo.line_length;

#ifdef USE_SUNXIFB_DOUBLE_BUFFER
    memset(&sinfo, 0, sizeof(struct sunxifb_info));
    sinfo.dbuf_en = true;
    // Do not clear fb and pointer to back fb
    sinfo.fbnum = (uint32_t)(screensize / (finfo.line_length * vinfo.yres));
    if (sinfo.fbnum > 1) {
        HMI_LOG_DEBUG("Turn on double buffering");

        sinfo.screenfbp[0] = fbp;
        sinfo.screenfbp[1] = fbp + finfo.line_length * vinfo.yres;

        if (vinfo.yoffset == 0) {
            sinfo.fbindex = !sinfo.fbindex;
            fbp = sinfo.screenfbp[sinfo.fbindex];
        }

#if USE_SUNXIFB_G2D
        HMI_LOG_DEBUG("Turn on 2d hardware acceleration");

        sunxifb_g2d_init(vinfo.bits_per_pixel);

        sunxifb_mem_init();

#if USE_SUNXIFB_G2D_ROTATE
        HMI_LOG_DEBUG("Turn on 2d hardware acceleration rotate");

        sinfo.rotatefbp = sunxifb_mem_alloc(finfo.line_length * vinfo.yres, "sunxifb_rotate");
        if (sinfo.rotatefbp == NULL) {
            perror("Error: cannot malloc rotate buffer");
            return;
        }

        fbp = sinfo.rotatefbp;
        sinfo.rotatefbp_phy = (uintptr_t) sunxifb_mem_get_phyaddr(
                sinfo.rotatefbp);

        switch (rotated) {
        case LV_DISPLAY_ROTATION_90:
            sinfo.rotated = G2D_ROT_270;
            sinfo.rotatefbp_w = vinfo.yres;
            sinfo.rotatefbp_h = vinfo.xres;
            fbp_w = vinfo.yres;
            fbp_h = vinfo.xres;
            fbp_line_length = fbp_w * vinfo.bits_per_pixel / 8;
            break;
        case LV_DISPLAY_ROTATION_180:
            sinfo.rotated = G2D_ROT_180;
            sinfo.rotatefbp_w = vinfo.xres;
            sinfo.rotatefbp_h = vinfo.yres;
            break;
        case LV_DISPLAY_ROTATION_270:
            sinfo.rotated = G2D_ROT_90;
            sinfo.rotatefbp_w = vinfo.yres;
            sinfo.rotatefbp_h = vinfo.xres;
            fbp_w = vinfo.yres;
            fbp_h = vinfo.xres;
            fbp_line_length = fbp_w * vinfo.bits_per_pixel / 8;
            break;
        default:
            sinfo.rotated = G2D_ROT_0;
            sinfo.rotatefbp_w = vinfo.xres;
            sinfo.rotatefbp_h = vinfo.yres;
            break;
        }
#endif /* USE_SUNXIFB_G2D_ROTATE */
#endif /* USE_SUNXIFB_G2D */
    }
#else
    memset(fbp, 0, screensize);
#endif /* USE_SUNXIFB_DOUBLE_BUFFER */
}

void sunxifb_exit(void) {
#ifdef USE_SUNXIFB_DOUBLE_BUFFER
#ifdef USE_SUNXIFB_CACHE
    uintptr_t args[2] = { 0, 0 };
    if (ioctl(fbfd, FBIO_ENABLE_CACHE, args) < 0) {
        perror("Error: FBIO_ENABLE_CACHE fail");
    }
#endif /* USE_SUNXIFB_CACHE */

#if USE_SUNXIFB_G2D
    sunxifb_g2d_deinit();

#if USE_SUNXIFB_G2D_ROTATE
    sunxifb_mem_free((void **) &sinfo.rotatefbp, "sunxifb_rotate");
#endif /* USE_SUNXIFB_G2D_ROTATE */
    sunxifb_mem_deinit();
#endif /* USE_SUNXIFB_G2D */
#endif /* USE_SUNXIFB_DOUBLE_BUFFER */
    close(fbfd);
}

/**
 * Flush a buffer to the marked area（LVGL v9 flush_cb 签名）
 * @param disp pointer to the display where this function belongs
 * @param area an area where to copy `px_map`
 * @param px_map an array of pixels (raw bytes) to copy to the `area` part of the screen
 */
void sunxifb_flush(lv_display_t *disp, const lv_area_t *area,
        uint8_t *px_map) {
    if (fbp == NULL || area->x2 < 0 || area->y2 < 0
            || area->x1 > (int32_t) fbp_w - 1
            || area->y1 > (int32_t) fbp_h - 1) {
        lv_display_flush_ready(disp);
        return;
    }

    /*Truncate the area to the screen*/
    int32_t act_x1 = area->x1 < 0 ? 0 : area->x1;
    int32_t act_y1 = area->y1 < 0 ? 0 : area->y1;
    int32_t act_x2 =
            area->x2 > (int32_t) fbp_w - 1 ? (int32_t) fbp_w - 1 : area->x2;
    int32_t act_y2 =
            area->y2 > (int32_t) fbp_h - 1 ? (int32_t) fbp_h - 1 : area->y2;

    lv_coord_t w = (act_x2 - act_x1 + 1);
    long int location = 0;
    long int byte_location = 0;
    unsigned char bit_location = 0;

    /*32 bit per pixel
      ⚠ **只支持 32bpp**（阶段 121）：旧条件写成 `==32 || ==24`，而分支体按
      `uint32_t*`、4 字节/像素拷贝 ⇒ 24bpp（3 字节/像素）帧缓冲会被**越界写**。
      板级配置是 XRGB8888(32bpp)；24bpp 显式拒绝并记一行日志，不再"照 32 处理"。 */
    if (vinfo.bits_per_pixel == 32) {
        uint32_t *fbp32 = (uint32_t*) fbp;
        int32_t y;
        for (y = act_y1; y <= act_y2; y++) {
#ifdef USE_SUNXIFB_DOUBLE_BUFFER
            if (sinfo.fbnum > 1)
                location = act_x1 + y * fbp_line_length / 4;
            else
#endif /* USE_SUNXIFB_DOUBLE_BUFFER */
                location = (act_x1 + vinfo.xoffset)
                        + (y + vinfo.yoffset) * finfo.line_length / 4;
            memcpy(&fbp32[location], px_map,
                    (act_x2 - act_x1 + 1) * 4);
            px_map += w * 4;
        }
    }
    /*24 bit per pixel：**显式拒绝**（本驱动未实现 3 字节/像素路径，按 32 处理会越界写）。
      阶段 121 的处置决定：**不实现 24bpp**，理由有二 ——
      ① 板级配置是 XRGB8888(32bpp)，24bpp 路径在本仓**没有任何实机/帧缓冲可验证**，
         写一段没跑过的像素打包代码，风险高于收益；
      ② 现状不是"少个功能"，而是"越界写"（旧代码把它并进 32 分支）—— 拒绝即已消除缺陷。
      真要支持 24bpp：按 BGR888 逐像素 3 字节写，并用真实 24bpp fb 做像素级验证后再落。 */
    else if (vinfo.bits_per_pixel == 24) {
        HMI_LOG("sunxifb: 24bpp 帧缓冲不受支持（仅实现 32/16bpp），跳过该刷新区域");
    }
    /*16 bit per pixel*/
    else if (vinfo.bits_per_pixel == 16) {
        uint16_t *fbp16 = (uint16_t*) fbp;
        int32_t y;
        for (y = act_y1; y <= act_y2; y++) {
#ifdef USE_SUNXIFB_DOUBLE_BUFFER
            if (sinfo.fbnum > 1)
                location = act_x1 + y * fbp_line_length / 2;
            else
#endif /* USE_SUNXIFB_DOUBLE_BUFFER */
                location = (act_x1 + vinfo.xoffset)
                        + (y + vinfo.yoffset) * finfo.line_length / 2;
            memcpy(&fbp16[location], px_map,
                    (act_x2 - act_x1 + 1) * 2);
            px_map += w * 2;
        }
    }
    /*8 bit per pixel*/
    else if (vinfo.bits_per_pixel == 8) {
        uint8_t *fbp8 = (uint8_t*) fbp;
        int32_t y;
        for (y = act_y1; y <= act_y2; y++) {
#ifdef USE_SUNXIFB_DOUBLE_BUFFER
            if (sinfo.fbnum > 1)
                location = act_x1 + y * fbp_line_length;
            else
#endif /* USE_SUNXIFB_DOUBLE_BUFFER */
                location = (act_x1 + vinfo.xoffset)
                        + (y + vinfo.yoffset) * finfo.line_length;
            memcpy(&fbp8[location], px_map, (act_x2 - act_x1 + 1));
            px_map += w;
        }
    }
    /*1 bit per pixel*/
    else if (vinfo.bits_per_pixel == 1) {
        uint8_t *fbp8 = (uint8_t*) fbp;
        int32_t x;
        int32_t y;
        for (y = act_y1; y <= act_y2; y++) {
            for (x = act_x1; x <= act_x2; x++) {
#ifdef USE_SUNXIFB_DOUBLE_BUFFER
                if (sinfo.fbnum > 1)
                    location = x + y * fbp_w;
                else
#endif /* USE_SUNXIFB_DOUBLE_BUFFER */
                    location = (x + vinfo.xoffset)
                            + (y + vinfo.yoffset) * vinfo.xres;
                byte_location = location / 8; /* find the byte we need to change */
                bit_location = location % 8; /* inside the byte found, find the bit we need to change */
                fbp8[byte_location] &= ~(((uint8_t)(1)) << bit_location);
                fbp8[byte_location] |= ((uint8_t)(*px_map))
                        << bit_location;
                px_map++;
            }

            px_map += area->x2 - act_x2;
        }
    } else {
        /*Not supported bit per pixel*/
    }

    //May be some direct update command is required
    //ret = ioctl(state->fd, FBIO_UPDATE, (unsigned long)((uintptr_t)rect));

#ifdef USE_SUNXIFB_DOUBLE_BUFFER
    if (sinfo.fbnum > 1 && sinfo.dbuf_en && lv_display_flush_is_last(disp)) {
#ifdef USE_SUNXIFB_CACHE
        uintptr_t args[2];
        args[0] = (uintptr_t) sinfo.screenfbp[sinfo.fbindex];
        args[1] = finfo.line_length * vinfo.yres;
        if (ioctl(fbfd, FBIO_CACHE_SYNC, args) < 0) {
            perror("Error: FBIO_CACHE_SYNC fail");
        }
#endif /* USE_SUNXIFB_CACHE */

#if USE_SUNXIFB_G2D_ROTATE
        sunxifb_mem_flush_cache(sinfo.rotatefbp,
                finfo.line_length * vinfo.yres);
        sunxifb_g2d_blit_to_fb(sinfo.rotatefbp_phy, sinfo.rotatefbp_w,
                sinfo.rotatefbp_h, 0, 0, sinfo.rotatefbp_w,
                sinfo.rotatefbp_h, finfo.smem_start, vinfo.xres_virtual,
                vinfo.yres_virtual, 0, sinfo.fbindex * vinfo.yres, vinfo.xres,
                vinfo.yres, sinfo.rotated);
#endif /* USE_SUNXIFB_G2D_ROTATE */

        vinfo.yoffset = sinfo.fbindex * vinfo.yres;
        if (ioctl(fbfd, FBIOPAN_DISPLAY, &vinfo) < 0) {
            perror("Error: FBIOPAN_DISPLAY fail");
        }

#if defined(USE_SUNXIFB_G2D) && !defined(USE_SUNXIFB_G2D_ROTATE)
        sunxifb_g2d_blit_to_fb(finfo.smem_start, vinfo.xres_virtual,
                vinfo.yres_virtual, 0, sinfo.fbindex * vinfo.yres, vinfo.xres,
                vinfo.yres, finfo.smem_start, vinfo.xres_virtual,
                vinfo.yres_virtual, 0, !sinfo.fbindex * vinfo.yres, vinfo.xres,
                vinfo.yres, G2D_ROT_0);
#elif !defined(USE_SUNXIFB_G2D_ROTATE)
        memcpy(sinfo.screenfbp[!sinfo.fbindex], sinfo.screenfbp[sinfo.fbindex],
                finfo.line_length * vinfo.yres);
#endif /* USE_SUNXIFB_G2D && !USE_SUNXIFB_G2D_ROTATE */

        sinfo.fbindex = !sinfo.fbindex;
#ifndef USE_SUNXIFB_G2D_ROTATE
        fbp = sinfo.screenfbp[sinfo.fbindex];
#endif /* USE_SUNXIFB_G2D_ROTATE */

#ifdef LV_USE_SUNXIFB_DEBUG
        static struct timeval new, old;
        static uint32_t cur_fps, avg_fps, max_fps, min_fps = 60, fps_cnt, first;
        gettimeofday(&new, NULL);
        if (new.tv_sec * 1000 - old.tv_sec * 1000 >= 1000) {
            if (first > 4) {
                if (first > 64) {
                    fps_cnt = 0;
                    avg_fps = 0;
                    max_fps = 0;
                    min_fps = 60;
                    first = 4;
                }
                fps_cnt++;
                avg_fps += cur_fps;
                if (max_fps < cur_fps)
                    max_fps = cur_fps;
                if (min_fps > cur_fps)
                    min_fps = cur_fps;
            }

            if (fps_cnt > 0)
                printf("sunxifb_flush fps_cnt=%u cur_fps=%u, max_fps=%u, "
                        "min_fps=%u, cur_page=%d, avg_fps=%.2f\n", fps_cnt,
                        cur_fps, max_fps, min_fps, sinfo.fbindex,
                        (float) avg_fps / (float) fps_cnt);

            first++;
            old = new;
            cur_fps = 0;
        } else {
            cur_fps++;
        }
#endif /* LV_USE_SUNXIFB_DEBUG */
    }
#endif /* USE_SUNXIFB_DOUBLE_BUFFER */

    lv_display_flush_ready(disp);
}

void sunxifb_get_sizes(uint32_t *width, uint32_t *height) {
    if (width)
        *width = vinfo.xres;

    if (height)
        *height = vinfo.yres;
}

void* sunxifb_alloc(size_t size, char *label) {
#if LV_USE_SUNXIFB_G2D_FILL || LV_USE_SUNXIFB_G2D_BLIT \
    || LV_USE_SUNXIFB_G2D_BLEND || LV_USE_SUNXIFB_G2D_SCALE
    return sunxifb_mem_alloc(size, label);
#else
    return malloc(size);
#endif
}

void sunxifb_free(void **data, char *label) {
#if LV_USE_SUNXIFB_G2D_FILL || LV_USE_SUNXIFB_G2D_BLIT \
    || LV_USE_SUNXIFB_G2D_BLEND || LV_USE_SUNXIFB_G2D_SCALE
    sunxifb_mem_free(data, label);
#else
    if (*data != NULL) {
        free(*data);
        *data = NULL;
    }
#endif
}

#ifdef USE_SUNXIFB_DOUBLE_BUFFER
bool sunxifb_get_dbuf_en() {
    return sinfo.dbuf_en;
}

int sunxifb_set_dbuf_en(lv_display_t *disp, bool dbuf_en) {
    (void)disp;
    if (sinfo.dbuf_en == dbuf_en)
        return 0;

    /* v9 不再通过 lv_disp_drv_t->draw_buf 暴露 flushing 状态，
     * 此处省略原 v8 的 `if (drv->draw_buf->flushing) return -2;` 检查。 */

#ifdef USE_SUNXIFB_CACHE
    uintptr_t args[2] = { dbuf_en, 0 };
    if (ioctl(fbfd, FBIO_ENABLE_CACHE, args) < 0) {
        perror("Error: FBIO_ENABLE_CACHE fail");
        return -1;
    }

    munmap(sinfo.screenfbp[0], finfo.line_length * vinfo.yres);

    // Re mmap the device to memory, settings will be effective
    fbp = (char*) mmap(0, screensize, PROT_READ | PROT_WRITE, MAP_SHARED, fbfd,
            0);
    if ((intptr_t) fbp == -1) {
        perror("Error: failed to map framebuffer device to memory");
        return -1;
    }

    sinfo.screenfbp[0] = fbp;
    sinfo.screenfbp[1] = fbp + finfo.line_length * vinfo.yres;
#endif /* USE_SUNXIFB_CACHE */

    if (dbuf_en) {
        vinfo.yoffset = sinfo.fbindex * vinfo.yres;
        if (ioctl(fbfd, FBIOPAN_DISPLAY, &vinfo) < 0) {
            perror("Error: FBIOPAN_DISPLAY fail");
        }

#if USE_SUNXIFB_G2D
        sunxifb_g2d_blit_to_fb(finfo.smem_start, vinfo.xres_virtual,
                vinfo.yres_virtual, 0, sinfo.fbindex * vinfo.yres, vinfo.xres,
                vinfo.yres, finfo.smem_start, vinfo.xres_virtual,
                vinfo.yres_virtual, 0, !sinfo.fbindex * vinfo.yres, vinfo.xres,
                vinfo.yres, G2D_ROT_0);
#else
        memcpy(sinfo.screenfbp[!sinfo.fbindex], sinfo.screenfbp[sinfo.fbindex],
                finfo.line_length * vinfo.yres);
#endif /* USE_SUNXIFB_G2D */
    }

    sinfo.fbindex = !sinfo.fbindex;
#if USE_SUNXIFB_G2D_ROTATE
    fbp = sinfo.rotatefbp;
#else
    fbp = sinfo.screenfbp[sinfo.fbindex];
#endif /* USE_SUNXIFB_G2D_ROTATE */

    sinfo.dbuf_en = dbuf_en;
    return 0;
}
#endif /* USE_SUNXIFB_DOUBLE_BUFFER */

/**********************
 *   STATIC FUNCTIONS
 **********************/

#endif
