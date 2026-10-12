# zhishu-HMI T113-S3 运行固件

基于《Linux-T113开发文档》（[索引](https://www.yuque.com/zhangjinfeng-sja3x/nt0aga/gl58abnr25m941d7)）开发的 HMI 运行固件，
与 `src/Hmi.Hardware.Builtin/AllwinnerT113Target.cs` 的硬件定义保持一致：

| 项目 | 规格 |
| --- | --- |
| SoC | Allwinner T113-S3（双核 ARM Cortex-A7，NEON-VFPv4） |
| 显示 | 1024x600 RGB666 / RGB565，60Hz，LVGL 9.5.0 |
| 内存 | 128MB DDR3（LVGL 堆 4MB） |
| 存储 | 128MB SPI NAND（W25N01GVZEIG，1 Gbit；可改用 TF 卡启动） |
| 触摸 | 电容 `/dev/input/event2`（默认，GT911 动态探测），电阻 `/dev/input/event0` |
| 通信 | Modbus RTU（RS485）、Modbus TCP（`eth0`）、MQTT（`eth0`/`wlan0`） |

## 目标板（HMI_BOARD_WAVEPI 宏切换，见 app/runtime/runtime_config.h）

| 板 | BOARD | 调试串口 | RS485 | 说明 |
| --- | --- | --- | --- | --- |
| t113-mq_r（默认） | `mq_r` | UART1 `/dev/ttyS1` | UART3 `/dev/ttyS3`（硬件 RS485，dts + mmap MCR） | 原有目标板 |
| WavePI-S6 7 寸 | `bingpi_m2` | UART3 `/dev/ttyS3`（PB6/PB7→CH340E） | UART4 `/dev/ttyS4`（PB2/PB3→SP485，DE/RE=PG8 GPIO 方向控制） | 新目标板，见 [docs/wavepi-s6-migration-plan.md](docs/wavepi-s6-migration-plan.md) |

## 目录结构

```
firmware/t113/
├── README.md                本文件
├── app/                     HMI 运行时源码（LVGL 8.1.0 目标）
│   ├── main.c               入口：sunxifb 显示 + evdev 触摸 + 主循环
│   ├── Makefile             应用编译（Tina 内使用，含 sunxifb.mk）
│   ├── runtime/             运行时核心（变量表/屏幕管理/配置）
│   ├── drivers/             硬件驱动（sunxifb/evdev/GPIO/I2C/PWM）
│   └── comm/                通信模块（Modbus RTU/TCP、MQTT）
├── config/                  LVGL 配置与 v9→v8 兼容层
├── generated/               编辑器导出的工程代码（标签表/屏幕/绑定）
├── tina/                    Tina-Linux SDK 集成（package/自启脚本/build.sh/dts 说明）
├── scripts/                 部署脚本
└── docs/                    烧录指南、版本信息、测试报告
```

## 快速开始（三选一）

1. **SDK 整包集成（推荐量产）**：将 `tina/package/hmi-runtime` 复制到 Tina 源码
   `package/`，执行 `make menuconfig` 选中该包，`make && pack` 产出
   `out/t113-mq_r/tina_t113-mq_r_uart1.img`（mq_r）或
   `out/t113-bingpi_m2/tina_t113-bingpi_m2_uart3.img`（WavePI-S6 7 寸），
   见 [docs/flashing-guide.md](docs/flashing-guide.md)。
2. **SDK 单包编译**：在 Tina 根目录执行 `mm`（或 `mm -B`）单编软件包，
   产物在 `out/<board>/...`，通过 `scp`/`adb` 部署，见 [scripts/deploy.sh](scripts/deploy.sh)。
3. **独立交叉编译**：将 Tina SDK 的 `arm-openwrt-linux-gcc` 加入 PATH，
   在 `app/` 下执行 `make`，链接 SDK 内已编译的 `liblvgl`、`liblv_drivers`。

构建统一走 [tina/build.sh](tina/build.sh)（`BOARD=mq_r|bingpi_m2` 切换目标板）：

## 与编辑器（zhishu-hmi）的集成契约

编辑器 `HmiEditor.CodeGen` 导出的工程代码放入 `generated/`：

- `generated/tags.h` / `tags.c`：`tag_index_t` 枚举 + `TAG_TABLE` 元数据 +
  `TAG_BINDINGS` 通信绑定（由编辑器的绑定引擎/连接配置导出）
- `generated/screens.c`：每屏 `create_screen_xxx()` + `HMI_SCREENS` 注册表
  （编辑器导出的 `create_screen_*` 函数体可直接复用，仅需将 `main()` 替换为
  `screen_manager.h` 的注册表形式；LVGL v9 语法直接编译（SDK 内置 LVGL 9.5））

运行时设备层接口与编辑器代码生成约定完全一致：

```c
extern float var_raw_read(int index);   // 设备层读（返回原始值）
extern void  var_raw_write(int index, float raw);  // 设备层写（含只读保护）
```

上层 `get_tag_value`/`set_tag_value`（含 scale/offset 换算）由编辑器生成在 `tags.c` 中。

## 运行时连接配置（免重编固件）

连接参数（Modbus TCP host/端口/超时、RTU 串口/波特率/校验、MQTT）默认在编译期固化
（`app/comm/comm_core.c`：`generated_comm.h` 优先，缺省 `runtime_config.h`）。
IDE「下载到 HMI」会额外下发 `comm_config.ini` 到设备 `/mnt/UDISK/hmi/`，
宿主启动 `hmi_comm_init` 读取并覆盖编译期默认 —— 之后在 IDE 修改连接参数
下载即生效，无需重编/重烧固件。

配置格式（`CommConfigCodeGen.GenerateRuntimeConfigText` 生成，key=value；
`rtu.dev` 默认按目标板：mq_r=`/dev/ttyS3`，WavePI-S6=`/dev/ttyS4`）：

```
rtu.dev=/dev/ttyS4
rtu.baud=9600
rtu.parity=N
rtu.tmo_ms=300
tcp.host=192.168.100.10
tcp.port=502
tcp.tmo_ms=3000
mqtt.host=192.168.100.10
mqtt.port=1883
mqtt.client_id=hmi-t113
mqtt.username=hmi
mqtt.password=hmi123
mqtt.keepalive=30
```

优先级：`comm_config.ini`（运行时） > `generated_comm.h`（编译期） > `runtime_config.h` 默认。
仅覆盖文件里存在的键；工程无启用连接时 IDE 删除设备旧文件，避免残留配置继续生效。

## 文档

- [docs/flashing-guide.md](docs/flashing-guide.md) — 固件烧录指南（PhoenixSuit / PhoenixCard / SSH）
- [docs/wavepi-s6-migration-plan.md](docs/wavepi-s6-migration-plan.md) — WavePI-S6 7 寸迁移方案（板配置/dts/串口/RS485）
- [docs/version-info.md](docs/version-info.md) — 版本信息与构建记录
- [docs/test-report.md](docs/test-report.md) — 测试报告（基本功能/稳定性/兼容性）
- [tina/dts/README.md](tina/dts/README.md) — 板级 dts 配置说明（UART/I2C/PWM/GPIO/触摸/LCD）
