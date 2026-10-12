/*
 * comm_core.h — 通信模块统一接口
 *
 * 驱动类型与编辑器 SupportedCommunicationDrivers 对齐：
 *   modbus-rtu (RS485：mq_r=/dev/ttyS3, WavePI=/dev/ttyS4)、modbus-tcp (eth0:502)、mqtt
 *
 * 线程模型：
 *   - 主线程（LVGL）通过 var_raw_read 读缓存，var_raw_write 写缓存+投递写请求
 *   - 通信线程轮询设备写入缓存，并消费写请求队列（hmi_comm_poll）
 *   - 缓存与队列由 pthread_mutex 保护
 */
#ifndef HMI_COMM_CORE_H
#define HMI_COMM_CORE_H

#include <stdint.h>

/* 驱动类型标识 */
typedef enum hmi_driver_type {
    HMI_DRV_NONE       = 0,
    HMI_DRV_MODBUS_RTU = 1,   /* "modbus-rtu"  → RS485（设备路径见 runtime_config.h） */
    HMI_DRV_MODBUS_TCP = 2,   /* "modbus-tcp"  → eth0:502 */
    HMI_DRV_MQTT       = 3,   /* "mqtt"        → 遥测/远程设定 */
} hmi_driver_t;

/* Modbus 功能码（GB/T 19582 / Modbus Application Protocol V1.1b3） */
#define MB_FC_READ_COILS       0x01
#define MB_FC_READ_DISCRETE    0x02
#define MB_FC_READ_HOLDING     0x03
#define MB_FC_READ_INPUT       0x04
#define MB_FC_WRITE_COIL       0x05
#define MB_FC_WRITE_REG        0x06
#define MB_FC_WRITE_MULTI_REG  0x10

/* 由驱动名字符串获取类型；未知返回 HMI_DRV_NONE */
hmi_driver_t hmi_driver_from_name(const char* name);

/*
 * 解析标签地址串为 (功能码, 寄存器偏移)。
 * 支持三种格式：
 *   "40001"/"30001"/"10001"/"00001"  Modbus 数据区规范（与编辑器 RuntimeTag.Address 一致）
 *   "h:0" / "c:5"                     显式类型前缀（h=保持寄存器, c=线圈, i=输入寄存器, d=离散输入）
 *   "3:0" / "1:7"                     显式功能码:偏移
 * 返回 0 成功，-1 解析失败。
 */
int hmi_tag_parse_addr(const char* s, uint8_t* fn, uint16_t* off);

/* ── 生命周期 ── */
int  hmi_comm_init(void);          /* 初始化各驱动（打开串口/建立连接，失败不阻塞启动） */
int  hmi_comm_start(void);         /* 创建通信线程（内部调用 hmi_comm_poll 循环） */
void hmi_comm_stop(void);          /* 停止线程并关闭驱动 */

/* ── 轮询与读写（通信线程内使用；也可由外部线程调用，内部加锁） ── */
void hmi_comm_poll(void);          /* 轮询一次所有驱动（块读合并） */
int  hmi_comm_read_regs(hmi_driver_t drv, uint8_t unit, uint8_t fn,
                        uint16_t off, uint16_t count, uint16_t* out);
int  hmi_comm_write_reg(hmi_driver_t drv, uint8_t unit, uint8_t fn,
                        uint16_t off, uint16_t val);
int  hmi_comm_write_regs(hmi_driver_t drv, uint8_t unit, uint16_t off,
                         const uint16_t* regs, uint16_t count);

#endif /* HMI_COMM_CORE_H */
