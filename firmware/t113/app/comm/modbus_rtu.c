/*
 * modbus_rtu.c — Modbus RTU 驱动实现（RS485）
 *
 * 目标板（HMI_BOARD_WAVEPI，见 runtime_config.h）：
 *   t113-mq_r（HMI_RS485_GPIO_PIN=0）：UART3 /dev/ttyS3，硬件 RS485 模式
 *     （dts linux,rs485-enabled-at-boot-time + /dev/mem 写 MCR 寄存器）
 *   WavePI-S6（HMI_RS485_GPIO_PIN=200, PG8）：UART4 /dev/ttyS4，SP485 收发器
 *     DE/RE 由 GPIO 方向控制（发送前拉高、tcdrain 后拉低），无需硬件 RS485。
 */
#include "modbus_rtu.h"
#include "comm_core.h"
#include "runtime_config.h"
#include "gpio_sunxi.h"
#include <pthread.h>
#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <fcntl.h>
#include <errno.h>
#include <termios.h>
#include <sys/select.h>
#include <sys/ioctl.h>
#include <sys/time.h>
#include <stdint.h>
#include <sys/stat.h>
#include <sys/sysmacros.h>

#ifdef __linux__
#include <sys/mman.h>
#endif

#ifdef TIOCSRS485
#include <linux/serial.h>
#endif

#define MB_RTU_MAX_FRAME 260

#if HMI_RS485_GPIO_PIN == 0
/*
 * T113 UART3 RS485 硬件寄存器定义（mq_r 路径，见 sunxi-uart.h）
 *   基地址 0x02500c00（dtsi: uart@2500c00）
 *   MCR       offset 0x10  bit7:6 = 0b10 (RS485 模式)
 *   RS485     offset 0xc0  bit3 = RXBFA (9th-bit NMM 模式)
 */
#define T113_UART3_BASE      0x02500c00UL
#define SUNXI_UART_MCR_OFF   0x10
#define SUNXI_UART_RS485_OFF 0xc0
#define SUNXI_UART_MCR_MODE_MASK   0xC0u
#define SUNXI_UART_MCR_MODE_RS485  0x80u   /* bit7=1, bit6=0 */
#define SUNXI_UART_RS485_RXBFA     0x08u

/*
 * 通过 /dev/mem 直接写 UART3 的 RS485 硬件寄存器。
 *
 * 背景：T113 sunxi-uart 驱动的 TIOCSRS485 被 Linux tty 层
 *（tty_port_ioctl）以 ENOTTY 拦截（驱动未注册 rs485_config 回调），
 * 到不了驱动的 sw_uart_ioctl。正确的修复是 dts 加
 * linux,rs485-enabled-at-boot-time（见 patch 0001）让驱动 probe 时设置
 * rs485conf.flags，startup 时写硬件。但该修复需要重新编译 kernel/firmware。
 *
 * 此函数作为**临时 workaround**：在 open+set_baud 之后直接写 MCR/RS485
 * 寄存器启用硬件 RS485 模式。注意：
 *   - 必须在 set_baud（tcsetattr→set_termios）之后调用，否则 set_termios
 *     会用缓存的 sw_uport->mcr（UART 模式）覆盖 MCR
 *   - 正常 write/read 不会重置 MCR（只有 break/modem 控制才覆盖）
 */
static int enable_rs485_hw_via_memmap(void)
{
#ifdef __linux__
    /* Tina Linux devtmpfs 可能未创建 /dev/mem，尝试用 mknod 创建 */
    if (access("/dev/mem", F_OK) != 0) {
        mknod("/dev/mem", S_IFCHR | 0600, makedev(1, 1));
    }
    int fd = open("/dev/mem", O_RDWR | O_SYNC);
    if (fd < 0) {
        fprintf(stderr, "[modbus-rtu] /dev/mem open failed: %s\n", strerror(errno));
        return -1;
    }
    /* 页对齐映射（2026-08-18 实机定位）：mmap offset 必须 4K 对齐，
     * T113_UART3_BASE=0x02500C00 页内偏移 0xC00 —— 直接 mmap 该地址返回 EINVAL
     * （"Invalid argument"），RS485 硬件模式从未启用，从站收不到请求。 */
    uintptr_t uart_page = T113_UART3_BASE & ~0xFFFUL;          /* 0x02500000 */
    uintptr_t uart_inpage = T113_UART3_BASE & 0xFFF;           /* 0xC00 */
    volatile uint8_t* regs = (volatile uint8_t*)mmap(NULL, 0x1000,
        PROT_READ | PROT_WRITE, MAP_SHARED, fd, uart_page);
    if (regs == MAP_FAILED) {
        fprintf(stderr, "[modbus-rtu] mmap UART3 failed: %s\n", strerror(errno));
        close(fd);
        return -1;
    }
    /* MCR[7:6] = 0b10 (RS485 模式)，保留其他位 */
    volatile uint32_t* mcr = (volatile uint32_t*)(regs + uart_inpage + SUNXI_UART_MCR_OFF);
    uint32_t mcr_val = *mcr;
    mcr_val = (mcr_val & ~SUNXI_UART_MCR_MODE_MASK) | SUNXI_UART_MCR_MODE_RS485;
    *mcr = mcr_val;
    /* RS485 寄存器 |= RXBFA (9th-bit NMM 模式) */
    volatile uint32_t* rs485 = (volatile uint32_t*)(regs + uart_inpage + SUNXI_UART_RS485_OFF);
    *rs485 |= SUNXI_UART_RS485_RXBFA;

    fprintf(stderr, "[modbus-rtu] RS485 HW enabled via /dev/mem (MCR=0x%02x RS485=0x%02x)\n",
            (unsigned)*mcr, (unsigned)*rs485);
    munmap((void*)regs, 0x1000);
    close(fd);
    return 0;
#else
    return -1;
#endif
}
#else /* HMI_RS485_GPIO_PIN > 0：GPIO 方向控制（WavePI-S6） */

/* 导出 PG8 并初始化为输出低电平（默认接收态）；返回 0 成功 */
static int rs485_gpio_init(void)
{
    if (gpio_export_out(HMI_RS485_GPIO_PIN) != 0) {
        fprintf(stderr, "[modbus-rtu] RS485 GPIO export failed (pin=%d)\n", HMI_RS485_GPIO_PIN);
        return -1;
    }
    gpio_write(HMI_RS485_GPIO_PIN, 0);   /* DE=0 → RE 使能，接收态 */
    fprintf(stderr, "[modbus-rtu] RS485 GPIO dir control ready (pin=%d)\n", HMI_RS485_GPIO_PIN);
    return 0;
}
/* 发送前：DE=1（驱动器使能） */
static void rs485_gpio_tx(void) { gpio_write(HMI_RS485_GPIO_PIN, 1); }
/* 发送后：DE=0（回到接收态，等从站响应） */
static void rs485_gpio_rx(void) { gpio_write(HMI_RS485_GPIO_PIN, 0); }
#endif /* HMI_RS485_GPIO_PIN */

static pthread_mutex_t s_lock = PTHREAD_MUTEX_INITIALIZER;
static int s_fd = -1;
static int s_tmo_ms = 300;

/* ── CRC16 (Modbus, poly 0xA001, init 0xFFFF) ── */
uint16_t mb_rtu_crc16(const uint8_t* data, size_t len)
{
    uint16_t crc = 0xFFFF;
    for (size_t i = 0; i < len; i++) {
        crc ^= data[i];
        for (int b = 0; b < 8; b++) {
            if (crc & 0x0001) crc = (crc >> 1) ^ 0xA001;
            else              crc >>= 1;
        }
    }
    return crc;
}

/* ── 帧构建（帧尾追加 CRC，低字节在前） ── */
int mb_rtu_build_frame(uint8_t unit, uint8_t fn, const uint8_t* payload,
                       size_t plen, uint8_t* frame, size_t fcap)
{
    size_t total = 2 + plen + 2;   /* unit + fn + pdu + crc */
    if (fcap < total) return -1;
    frame[0] = unit;
    frame[1] = fn;
    if (plen) memcpy(&frame[2], payload, plen);
    uint16_t crc = mb_rtu_crc16(frame, total - 2);
    frame[total - 2] = (uint8_t)(crc & 0xFF);        /* 低字节在前 */
    frame[total - 1] = (uint8_t)(crc >> 8);
    return (int)total;
}

/* ── 响应解析与校验 ── */
int mb_rtu_parse_frame(const uint8_t* frame, size_t len, uint8_t expect_unit,
                       uint8_t expect_fn, uint8_t* pdu, size_t* pdulen,
                       uint8_t* excode)
{
    if (len < 5) return -1;                          /* 至少 unit+fn+bc+2crc */
    if (frame[0] != expect_unit) return -1;
    if (frame[1] == (expect_fn | 0x80)) {            /* 异常响应 */
        if (excode) *excode = frame[2];
        return -2;
    }
    if (frame[1] != expect_fn) return -1;
    uint8_t bc = frame[2];
    size_t expected = 3 + (size_t)bc + 2;
    if (len < expected) return -1;
    if (mb_rtu_crc16(frame, expected - 2) !=
        (uint16_t)(frame[expected - 2] | ((uint16_t)frame[expected - 1] << 8)))
        return -1;
    if (pdu && pdulen) {
        if (*pdulen < bc) return -1;
        memcpy(pdu, &frame[3], bc);
        *pdulen = bc;
    }
    return 0;
}

/* ── 读响应 PDU 字节数校验 ──
 *
 * Modbus 规定读响应的字节数域 `bc` = 2×寄存器数（位读 = ceil(n/8)）。
 * `mb_rtu_parse_frame` 只按 `frame[2]` **自称**的 bc 校验 CRC —— 它不知道调用方要几个，
 * 所以一个「bc 偏小但 CRC 合法」的响应能通过解析。调用方若直接按 count 索引 pdu，
 * 就会读到未初始化/陈旧字节，把垃圾值写进变量缓存（阶段 121 修复）。
 */
int mb_rtu_check_read_pdu(size_t pdulen, size_t expected_bytes)
{
    return pdulen == expected_bytes ? 0 : -1;
}

/* ── 写响应校验（fn5 / fn6 / fn16 的回显） ──
 *
 * ⚠ **不能**把写回显交给 mb_rtu_parse_frame()：那是**读响应**解析器 —— 它把 frame[2]
 *   当字节数 bc、按 3+bc 定位 CRC。而写响应里 frame[2] 是**地址高位**，于是合法回显
 *   必然被判 CRC 失败 ⇒ **每一次 RTU 写都被静默拒绝**（阶段 120 修复；调用方
 *   hmi_var_drain_writes 原先还忽略返回值，所以现场看不到任何报错）。
 *
 * 三种写响应的形状：
 *   fn5/fn6  [unit][fn][addr_hi][addr_lo][val_hi][val_lo][crc_lo][crc_hi]   （回显 addr+value）
 *   fn16     [unit][fn][addr_hi][addr_lo][cnt_hi][cnt_lo][crc_lo][crc_hi]   （回显 addr+count）
 *   异常     [unit][fn|0x80][excode][crc_lo][crc_hi]
 *
 * 返回 0 = 回显一致；-1 = 长度/CRC/从站/功能码/回显不符；-2 = 从站异常响应（excode 出参）。
 */
static int mb_rtu_check_write_echo(const uint8_t* frame, size_t len, uint8_t expect_unit,
                                   uint8_t expect_fn, const uint8_t* echo, size_t echo_len,
                                   uint8_t* excode)
{
    if (len >= 5 && frame[0] == expect_unit && frame[1] == (uint8_t)(expect_fn | 0x80)) {
        if (mb_rtu_crc16(frame, 3) !=
            (uint16_t)(frame[3] | ((uint16_t)frame[4] << 8))) return -1;
        if (excode) *excode = frame[2];
        return -2;
    }

    size_t total = 2 + echo_len + 2;                 /* unit + fn + 回显 + CRC */
    if (len < total) return -1;
    if (frame[0] != expect_unit || frame[1] != expect_fn) return -1;
    if (mb_rtu_crc16(frame, total - 2) !=
        (uint16_t)(frame[total - 2] | ((uint16_t)frame[total - 1] << 8))) return -1;
    if (memcmp(&frame[2], echo, echo_len) != 0) return -1;
    return 0;
}

/* ── 串口配置 ── */
static int set_baud(int fd, int baud)
{
    struct termios t;
    if (tcgetattr(fd, &t) != 0) return -1;
    cfmakeraw(&t);
    t.c_cflag |= CLOCAL | CREAD;
    t.c_cflag &= ~CSTOPB;
    speed_t sp;
    switch (baud) {
        case 1200: sp = B1200; break;
        case 2400: sp = B2400; break;
        case 4800: sp = B4800; break;
        case 9600: sp = B9600; break;
        case 19200: sp = B19200; break;
        case 38400: sp = B38400; break;
        case 57600: sp = B57600; break;
        case 115200: sp = B115200; break;
        default: sp = B9600; break;
    }
    cfsetispeed(&t, sp);
    cfsetospeed(&t, sp);
    t.c_cc[VMIN] = 0;
    t.c_cc[VTIME] = 0;
    return tcsetattr(fd, TCSANOW, &t);
}

int mb_rtu_init(const char* dev, int baud, char parity, int timeout_ms)
{
    (void)parity;   /* 硬件侧按 dts 配置校验位；此处统一 8N1 */
    s_tmo_ms = timeout_ms > 0 ? timeout_ms : 300;
    if (s_fd >= 0) close(s_fd);
    s_fd = open(dev, O_RDWR | O_NOCTTY);
    if (s_fd < 0) {
        fprintf(stderr, "[modbus-rtu] open %s failed: %s\n", dev, strerror(errno));
        return -1;
    }
    if (set_baud(s_fd, baud) != 0) {
        fprintf(stderr, "[modbus-rtu] configure %s failed\n", dev);
        close(s_fd);
        s_fd = -1;
        return -1;
    }
    /*
     * RS485 方向控制（按目标板，见 runtime_config.h）：
     *   HMI_RS485_GPIO_PIN>0（WavePI-S6）：GPIO 控制 SP485 DE/RE（发送前拉高/后拉低）
     *   HMI_RS485_GPIO_PIN==0（mq_r）：
     *     TIOCSRS485 被 tty 层拦截（ENOTTY），dts 修复需要重编固件。
     *     在 set_baud 之后直接通过 /dev/mem 写 UART3 硬件寄存器启用 RS485 模式。
     *     正式修复见 patch 0001（dts 加 linux,rs485-enabled-at-boot-time）。
     */
#if HMI_RS485_GPIO_PIN > 0
    rs485_gpio_init();
#else
    enable_rs485_hw_via_memmap();
#ifdef TIOCSRS485
    /*
     * RS485 半双工：尝试运行时启用（跨平台兼容路径）。
     * 注意：T113 sunxi-uart 驱动未注册 uart_port.rs485_config 回调，
     * Linux tty 层 tty_port_ioctl 会以 ENOTTY 拒绝 TIOCSRS485，到不了底层驱动；
     * T113 平台需在 dts 中加 linux,rs485-enabled-at-boot-time 由驱动启动时启用。
     * 详见 firmware/t113/tina/dts/README.md §1.1 与 patch 0001。
     * 其它平台（如标准 8250_dw with em485）若支持 TIOCSRS485 则此处生效。
     */
    struct serial_rs485 rs485;
    memset(&rs485, 0, sizeof(rs485));
    rs485.flags = SER_RS485_ENABLED | SER_RS485_RTS_ON_SEND;
    rs485.delay_rts_before_send = 1;
    rs485.delay_rts_after_send = 1;
    if (ioctl(s_fd, TIOCSRS485, &rs485) != 0) {
        fprintf(stderr, "[modbus-rtu] TIOCSRS485 failed: %s\n", strerror(errno));
    } else {
        fprintf(stderr, "[modbus-rtu] TIOCSRS485 OK\n");
    }
    /* 诊断：ioctl 后读 UART3 RS485/MCR 寄存器实际值 */
    {
        int mfd = open("/dev/mem", O_RDWR | O_SYNC);
        if (mfd >= 0) {
            volatile uint8_t* r = (volatile uint8_t*)mmap(NULL, 0x1000, PROT_READ | PROT_WRITE,
                MAP_SHARED, mfd, (T113_UART3_BASE & ~0xFFFUL));
            if (r != MAP_FAILED) {
                volatile uint32_t* mcr = (volatile uint32_t*)(r + (T113_UART3_BASE & 0xFFF) + SUNXI_UART_MCR_OFF);
                volatile uint32_t* rs = (volatile uint32_t*)(r + (T113_UART3_BASE & 0xFFF) + SUNXI_UART_RS485_OFF);
                fprintf(stderr, "[modbus-rtu] after ioctl: MCR=0x%02x RS485=0x%02x\n",
                        (unsigned)*mcr, (unsigned)*rs);
                munmap((void*)r, 0x1000);
            }
            close(mfd);
        }
    }
#endif /* TIOCSRS485 */
#endif /* HMI_RS485_GPIO_PIN */
    return 0;
}

void mb_rtu_deinit(void)
{
    if (s_fd >= 0) { close(s_fd); s_fd = -1; }
}

/* 读取期望响应：按字节间隔与超时返回实际读到的帧 */
static int read_frame(int fd, uint8_t* buf, size_t cap, size_t min_expect, int tmo_ms)
{
    size_t n = 0;
    struct timeval tv;
    int tmo = tmo_ms;
    while (n < cap) {
        tv.tv_sec = 0;
        tv.tv_usec = tmo * 1000;
        fd_set rfds;
        FD_ZERO(&rfds);
        FD_SET(fd, &rfds);
        int r = select(fd + 1, &rfds, NULL, NULL, &tv);
        if (r <= 0) break;                  /* 超时/错误 */
        ssize_t got = read(fd, &buf[n], cap - n);
        if (got <= 0) break;
        n += (size_t)got;
        if (n >= min_expect) break;         /* 期望长度已满足 */
        tmo = 5;                            /* 后续按字符间隔等待（5ms） */
    }
    return (int)n;
}

/* 块读：1/2 读位，3/4 读寄存器 */
int mb_rtu_read_regs(uint8_t unit, uint8_t fn, uint16_t off, uint16_t count, uint16_t* out)
{
    if (!out || count == 0 || count > 125) return -1;
    uint8_t req[5];
    req[0] = (uint8_t)(off >> 8); req[1] = (uint8_t)off;
    req[2] = (uint8_t)(count >> 8); req[3] = (uint8_t)count;

    uint8_t frame[MB_RTU_MAX_FRAME];
    size_t flen = (size_t)mb_rtu_build_frame(unit, fn, req, 4, frame, sizeof(frame));
    if ((int)flen < 0) return -1;

    pthread_mutex_lock(&s_lock);
    if (s_fd < 0) { pthread_mutex_unlock(&s_lock); return -1; }

    tcflush(s_fd, TCIOFLUSH);
#if HMI_RS485_GPIO_PIN > 0
    rs485_gpio_tx();   /* DE=1：进入发送方向（SP485） */
#endif
    if (write(s_fd, frame, flen) != (ssize_t)flen) {
#if HMI_RS485_GPIO_PIN > 0
        rs485_gpio_rx();
#endif
        pthread_mutex_unlock(&s_lock);
        return -1;
    }
    tcdrain(s_fd);   /* 等待发送完成，避免 RS485 方向切换竞态 */
#if HMI_RS485_GPIO_PIN > 0
    rs485_gpio_rx();   /* DE=0：回到接收方向，等待从站响应 */
#endif

    size_t byte_cnt = (fn == MB_FC_READ_COILS || fn == MB_FC_READ_DISCRETE)
                          ? (size_t)((count + 7) / 8)
                          : (size_t)(count * 2);
    size_t min_expect = 3 + byte_cnt + 2;
    uint8_t rbuf[MB_RTU_MAX_FRAME];
    int rlen = read_frame(s_fd, rbuf, sizeof(rbuf), min_expect, s_tmo_ms);
    pthread_mutex_unlock(&s_lock);
    if (rlen < (int)min_expect) return -1;

    uint8_t pdu[256];
    size_t pdulen = sizeof(pdu);
    uint8_t excode = 0;
    int rc = mb_rtu_parse_frame(rbuf, (size_t)rlen, unit, fn, pdu, &pdulen, &excode);
    if (rc == -2) {
        fprintf(stderr, "[modbus-rtu] unit=%d fn=0x%02X exception 0x%02X\n", unit, fn, excode);
        return -1;
    }
    if (rc != 0) return -1;

    /* ⚠ 解析器只按 frame[2] **自称**的字节数校验 CRC ⇒ 必须再核一次"bc 是否恰好等于期望"：
       一个「bc 偏小但 CRC 合法」的响应能通过解析，而下面按 count 索引 pdu 就会读到
       未初始化/陈旧字节 ⇒ 垃圾值进变量缓存（阶段 121 修复）。 */
    if (mb_rtu_check_read_pdu(pdulen, byte_cnt) != 0) {
        fprintf(stderr, "[modbus-rtu] unit=%d fn=0x%02X 字节数不符: 期望 %zu, 实为 %zu\n",
                unit, fn, byte_cnt, pdulen);
        return -1;
    }

    if (fn == MB_FC_READ_COILS || fn == MB_FC_READ_DISCRETE) {
        for (uint16_t i = 0; i < count; i++)
            out[i] = (pdu[i / 8] >> (i % 8)) & 1;
    } else {
        for (uint16_t i = 0; i < count; i++)
            out[i] = (uint16_t)((pdu[i * 2] << 8) | pdu[i * 2 + 1]);
    }
    return 0;
}

/* 单点写：fn5 线圈（val≠0→0xFF00）/ fn6 寄存器 */
int mb_rtu_write_reg(uint8_t unit, uint8_t fn, uint16_t off, uint16_t val)
{
    uint16_t v = (fn == MB_FC_WRITE_COIL) ? (val ? 0xFF00 : 0x0000) : val;
    uint8_t req[4];
    req[0] = (uint8_t)(off >> 8); req[1] = (uint8_t)off;
    req[2] = (uint8_t)(v >> 8);   req[3] = (uint8_t)v;

    uint8_t frame[MB_RTU_MAX_FRAME];
    size_t flen = (size_t)mb_rtu_build_frame(unit, fn, req, 4, frame, sizeof(frame));
    if ((int)flen < 0) return -1;

    pthread_mutex_lock(&s_lock);
    if (s_fd < 0) { pthread_mutex_unlock(&s_lock); return -1; }
    tcflush(s_fd, TCIOFLUSH);
#if HMI_RS485_GPIO_PIN > 0
    rs485_gpio_tx();
#endif
    if (write(s_fd, frame, flen) != (ssize_t)flen) {
#if HMI_RS485_GPIO_PIN > 0
        rs485_gpio_rx();
#endif
        pthread_mutex_unlock(&s_lock);
        return -1;
    }
    tcdrain(s_fd);
#if HMI_RS485_GPIO_PIN > 0
    rs485_gpio_rx();
#endif
    uint8_t rbuf[MB_RTU_MAX_FRAME];
    int rlen = read_frame(s_fd, rbuf, sizeof(rbuf), 8, s_tmo_ms);
    pthread_mutex_unlock(&s_lock);
    if (rlen < 5) return -1;                         /* 最短是 5 字节异常响应 */

    uint8_t echo[4] = { req[0], req[1], req[2], req[3] };
    uint8_t excode = 0;
    int rc = mb_rtu_check_write_echo(rbuf, (size_t)rlen, unit, fn, echo, sizeof(echo), &excode);
    if (rc == -2) { fprintf(stderr, "[modbus-rtu] write exception 0x%02X\n", excode); return -1; }
    return rc == 0 ? 0 : -1;
}

/* 多寄存器写：fn16 */
int mb_rtu_write_regs(uint8_t unit, uint16_t off, const uint16_t* regs, uint16_t count)
{
    if (!regs || count == 0 || count > 123) return -1;
    uint8_t req[2 + 1 + 2 * count];
    req[0] = (uint8_t)(off >> 8); req[1] = (uint8_t)off;
    req[2] = (uint8_t)(count * 2);
    for (uint16_t i = 0; i < count; i++) {
        req[3 + i * 2]     = (uint8_t)(regs[i] >> 8);
        req[3 + i * 2 + 1] = (uint8_t)regs[i];
    }

    uint8_t frame[MB_RTU_MAX_FRAME];
    size_t flen = (size_t)mb_rtu_build_frame(unit, MB_FC_WRITE_MULTI_REG, req, sizeof(req), frame, sizeof(frame));
    if ((int)flen < 0) return -1;

    pthread_mutex_lock(&s_lock);
    if (s_fd < 0) { pthread_mutex_unlock(&s_lock); return -1; }
    tcflush(s_fd, TCIOFLUSH);
#if HMI_RS485_GPIO_PIN > 0
    rs485_gpio_tx();
#endif
    if (write(s_fd, frame, flen) != (ssize_t)flen) {
#if HMI_RS485_GPIO_PIN > 0
        rs485_gpio_rx();
#endif
        pthread_mutex_unlock(&s_lock);
        return -1;
    }
    tcdrain(s_fd);
#if HMI_RS485_GPIO_PIN > 0
    rs485_gpio_rx();
#endif
    uint8_t rbuf[MB_RTU_MAX_FRAME];
    int rlen = read_frame(s_fd, rbuf, sizeof(rbuf), 8, s_tmo_ms);
    pthread_mutex_unlock(&s_lock);
    if (rlen < 5) return -1;                         /* 最短是 5 字节异常响应 */

    /* 回显 = 起始地址 + 寄存器个数（fn16 就是这两项） */
    uint8_t echo[4] = { req[0], req[1], (uint8_t)(count >> 8), (uint8_t)count };
    uint8_t excode = 0;
    int rc = mb_rtu_check_write_echo(rbuf, (size_t)rlen, unit, MB_FC_WRITE_MULTI_REG,
                                     echo, sizeof(echo), &excode);
    if (rc == -2) { fprintf(stderr, "[modbus-rtu] write multi exception 0x%02X\n", excode); return -1; }
    return rc == 0 ? 0 : -1;
}

#ifdef HMI_UNIT_TEST
/*
 * 仅供 host 测试：暴露写回显校验器。
 * 它本身跑不到真实串口（`mb_rtu_write_reg*` 需要 fd），但**判据的核心**正是
 * 「写回显的形状 ≠ 读响应的形状」—— 这一点不需要串口就能钉死。
 */
int mb_rtu_test_check_write_echo(const uint8_t* frame, size_t len, uint8_t expect_unit,
                                 uint8_t expect_fn, const uint8_t* echo, size_t echo_len,
                                 uint8_t* excode)
{
    return mb_rtu_check_write_echo(frame, len, expect_unit, expect_fn, echo, echo_len, excode);
}
#endif
