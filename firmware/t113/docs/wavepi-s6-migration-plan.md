# zhishu-HMI 固件迁移方案：t113-mq_r → WavePI-S6 (T113-S3)

> 状态：方案稿（2026-08-18），**已确认使用 7 寸 1024×600 屏**（与现有工程分辨率一致）
> 目标：把现有 `firmware/t113`（基于 Tina SDK `t113-mq_r` 板配置，7 寸 1024×600）
> 迁移到 **WavePI-S6 开发板（7 寸屏）**（全志 T113-S3，资料包 `/home/chen/下载/WavePi/`）。
> 迁移后固件仍需支持：LVGL 9.5 UI + Modbus RTU(RS485)/TCP + MQTT + 软键盘 +
> .so 热更新 + 开机设置窗口。

---

## 1. 背景：现有固件（t113-mq_r）基线

| 项 | 值 |
| --- | --- |
| SoC | Allwinner T113-S3（sun8iw20p1，双核 Cortex-A7） |
| SDK | Tina-Linux（OpenWrt，kernel 5.4.61，brandy-2.0） |
| 板配置 | `device/config/chips/t113/configs/mq_r/` + `target/allwinner/t113-mq_r/` |
| 屏幕 | 7 寸 1024×600 RGB666（`HMI_LCD_WIDTH/HEIGHT` 写死） |
| 显示驱动 | sunxifb（`/dev/fb0`）+ G2D 硬件加速（`-luapi`），LVGL 9.5.0 |
| 触摸 | GT911 电容 `/dev/input/event2`（电阻备选 event0） |
| 调试串口 | **UART1** `/dev/ttyS1`（镜像名 `tina_t113-mq_r_uart1.img`） |
| RS485 | **UART3** `/dev/ttyS3`（PG8/PG9，dts `linux,rs485-enabled-at-boot-time` + mmap 写 MCR 寄存器） |
| 存储 | SPI NAND 启动（storage_type=1），`bootcmd=run setargs_nand boot_normal` |
| 通信 | Modbus RTU(RS485) / Modbus TCP(eth0) / MQTT(wlan0/eth0) |
| 构建 | `tina/build.sh all <TINA_ROOT>` → `out/t113-mq_r/tina_t113-mq_r_uart1.img` |
| 已有实机修复 | OP-TEE 禁用、内核关 PSCI/CAN、lv_examples 清理、boot_package maintype 补丁、字体回退、CLOCK_MONOTONIC tick |

## 2. WavePI-S6 板差异（资料包 `/home/chen/下载/WavePi/`）

资料结构：`Wave Pi/`（0_必看 ~ 11_LVGL_QT例程）+ `Tina/`（已解压 SDK 源码树）。

### 2.1 SDK 基线

| 项 | WavePi SDK（`/home/chen/下载/WavePi/Tina`） | 现有 SDK（firmware/Tina-SDK） |
| --- | --- | --- |
| 内核 | linux-5.4（**SUBLEVEL=61，与现有一致**） | linux-5.4.61 |
| brandy | brandy-2.0 / u-boot-2018（一致） | 同 |
| 板配置 | `configs/WavePI_S6/` + `configs/bingpi_m2/` + `mq_r/` + `evb1/` | 仅 `mq_r/` + `evb1/` |
| target | `t113-WavePI_S6/`、`t113-bingpi_m2/`、`t113-mq_r/` | 仅 `t113-mq_r/`、`t113-evb1/` |
| LVGL | **只有 littlevgl-6 / littlevgl-8（8.1.0 例程）**，**无 lvgl 9.5 包** | `package/gui/lvgl`（**LVGL 9.5.0**，6842 文件） |
| 出厂镜像 | 5 寸 `tina_t113-WavePI_S6_uart3_5.img`、7 寸 `tina_t113-bingpi_m2_uart3_7.img` | `tina_t113-mq_r_uart1.img` |

**结论：WavePi SDK 与现有 SDK 是同源基线（kernel 5.4.61 一致），差异只在板配置与 LVGL 包。**
WavePi SDK 缺 LVGL 9.5 包，这是迁移的最关键缺口（见 §4 路线决策）。

### 2.2 硬件接口差异（核心）

| 接口 | t113-mq_r（现有） | WavePI-S6（目标） |
| --- | --- | --- |
| 调试串口 | **UART1** ttyS1（镜像 uart1） | **UART3** ttyS3（PB6/PB7 → 板载 CH340E，`uart_debug_port=3`，console=ttyS3） |
| RS485 | **UART3** ttyS3（PG8/PG9，硬件 RS485 模式） | **UART4** ttyS4（PB2/PB3 → **SP485 芯片 U28**，DE/RE 由 **PG8(RS485_EN)** 软件控制） |
| CAN | 内核已关（TEE 侧） | **CAN1**（PB4/PB5 → SN65HVD230 U34，dts `can1 status=okay`） |
| 触摸 | GT911 @ twi2 0x5d，INT=PB6/WKUP=PB7 | GT911 @ **twi2 0x14**，INT=**PD22**/WKUP=PG10，max_x/y=800×480（出厂默认；**7 寸需改 1024×600 = 0x400/0x258**） |
| LCD 默认 | 1024×600（dts 已启用） | dts 注释块：480×272 / 800×480（**默认启用**）/ **1024×600（7 寸，注释块需解开）** / 1280×800(MIPI) |
| fb | fb0 1024×600 | fb0 800×480（`fb0_width/height` 注释未写死，走 lcd0；7 寸切 1024×600 后跟随 lcd0） |
| 存储 | SPI NAND，bootcmd setargs_nand | SPI NAND，bootcmd 默认 `setargs_nand boot_normal`（**无需再打 bootcmd 补丁**） |
| 额外外设 | — | SSD1306 OLED（twi？GPIO-SPI）、AHT10 温湿度（twi0）、4G 模块位、eeprom@50 |

**最关键的冲突**：
1. **调试口与 RS485 串口互换**：现有固件 RS485 在 ttyS3（UART3），WavePi 上 UART3 是调试口，
   RS485 移到了 **UART4（ttyS4）**。`runtime_config.h`、`modbus_rtu.c`（硬编码 `T113_UART3_BASE=0x02500C00`）、
   dts patch（0001 给 uart3 加 RS485 属性）全部要改。
2. **RS485 方向控制方式不同**：mq_r 用 sunxi-uart 硬件 RS485（dts `linux,rs485-enabled-at-boot-time` +
   mmap 写 MCR 寄存器）；WavePi 的 SP485 是**独立收发器**，DE/RE 复用为 **GPIO PG8**，
   需要应用层在发送前拉高 PG8、发送后拉低（半双工方向切换），或确认 UART4 硬件 RS485 是否可用。
3. **LVGL 9.5 包缺失**（WavePi SDK 无 `package/gui/lvgl`）。
4. **屏幕默认 800×480**：现有工程按 1024×600 出图；**已确认用 7 寸 1024×600**，
   编辑器目标与生成代码无需改，只需在 dts 启用 1024×600 屏参块（见 §3.2）。

> **7 寸屏决策（2026-08-18 用户确认）**：板配置用 **bingpi_m2**（官方 7 寸出厂镜像
> `tina_t113-bingpi_m2_uart3_7.img` 即基于该配置；bingpi_m2 与 WavePI_S6 的 dts
> 仅 OLED 引脚注释不同，其余一致）。dts 需：① 注释掉 800×480 启用块（bingpi_m2
> `linux-5.4/board.dts` ~1390–1450 行），② 解开 1024×600 注释块（~1453–1494 行），
> ③ 触摸 max 改 `0x400/0x258`（=1024×600，与现有 mq_r 相同值）；uboot-board.dts
> 同步处理（800×480 块 388–428 行 ↔ 1024×600 注释块 429–471 行）。

## 3. 迁移范围清单

### 3.1 SDK 板配置（device/config + target）

| 文件 | 动作 |
| --- | --- |
| `device/config/chips/t113/configs/WavePI_S6/` | 从 WavePi SDK 复制（含 linux-5.4/board.dts、uboot-board.dts、sys_config.fex、env.cfg、BoardConfig.mk、boot-resource） |
| `device/config/chips/t113/configs/bingpi_m2/` | **从 WavePi SDK 复制（7 寸屏主配置，官方 7 寸镜像即基于此）** |
| `target/allwinner/t113-WavePI_S6/` | 从 WavePi SDK 复制（defconfig、BoardConfig.mk、TinaProducts.mk、vendorsetup.sh 等） |
| `target/allwinner/t113-bingpi_m2/` | **从 WavePi SDK 复制（7 寸屏配套 target）** |
| `.config` | 现有 SDK 的 `.config` 已含 `CONFIG_PACKAGE_hmi-runtime=y`、`libuapi/libfreetype/libpng`；切板后需确认 `CONFIG_TARGET_BOARD` 与 lunch 名 |

### 3.2 板级 dts 改动（**bingpi_m2**/linux-5.4/board.dts，7 寸屏）

- **LCD（必须）**：注释掉 800×480 启用块（~1390–1450 行），解开 1024×600 注释块
  （~1453–1494 行：`lcd_x=1024, lcd_y=600, dclk=51, ht=1344, vt=635`）。
  确认 `&disp` 的 `screen0_output_mode`/fb 尺寸跟随 lcd0；`rgb18_pins_a` 与现有
  sunxifb 驱动匹配（mq_r 曾用 `HMI_LCD_RGB24` 宏切 rgb24，WavePi 板默认 rgb18，
  7 寸屏若为 RGB666 保持 rgb18 即可）。
- **触摸（必须）**：GT911 `ctp_twi_addr=0x14`、`ctp_int_port=PD22` 保持；
  `ctp_screen_max_x/y` 改 **0x400/0x258**（1024×600，参照现有 mq_r 的 0x400/0x258）。
  > **现状校正（2026-09-13）**：已提交的 bingpi_m2 `board.dts` **仍是 `0x320/0x1e0`
  > （800×480）**，即本条未落地。固件 `runtime_config.h` 的 `HMI_TOUCH_MAX_X/Y`
  > 现按实际 DTS 取 800×480 归一（审查 P0-7：按 1024×600 归一会让右侧 224px、
  > 底部 120px 触摸不可达）。**若后续按本条把 dts 改成 0x400/0x258，必须同步把
  > `HMI_TOUCH_MAX_X/Y` 改回 1024/600**，否则会出现"超量程拉伸"的反向偏差。
- **UART3**：保留 `status="okay"`（调试口），**移除/不应用** mq_r 的
  `linux,rs485-enabled-at-boot-time` 补丁（那是给 UART3 的）。
- **UART4**：`status="okay"`（RS485 用）。评估是否给 uart4 加 RS485 硬件模式，还是走 GPIO 方向控制。
- **can1**：默认 `okay`；若保持现有"禁用 TEE/内核关 CAN"策略，需把 can1 改 disabled 或内核关
  `CONFIG_CAN_SUN8I`（按现有 flashing-guide ⑤）。
- **SSD1306 / AHT10 / eeprom**：默认已配，不影响 HMI；注意 OLED 的 gpio-spi 可能与别处抢引脚，验证时不冲突即可。
- **uboot-board.dts（同步）**：800×480 启用块（388–428 行）↔ 1024×600 注释块（429–471 行），
  同样切换，保证 uboot logo 阶段分辨率正确。

### 3.3 应用层（firmware/t113/app）

| 文件 | 改动 |
| --- | --- |
| `runtime/runtime_config.h` | **`HMI_LCD_WIDTH/HEIGHT` 保持 1024×600（7 寸，与现有一致，无需改）**；`HMI_DEV_UART_RS485` → `/dev/ttyS4`；`HMI_DEV_UART_DEBUG` → `/dev/ttyS3`；触摸节点按实机 input 枚举（GT911 通常 event2，需上板确认） |
| `comm/modbus_rtu.c` | `T113_UART3_BASE` → UART4 基址（dtsi：uart4 = 0x02501000）；RS485 方向控制改为 **GPIO PG8**（`gpio_export_out` + 发送前写 1/发送后写 0，见 `drivers/gpio_sunxi.c`），或按 uart4 硬件 RS485 模式改写 mmap 逻辑 |
| `comm/comm_core.c` / `generated_comm.h` 缺省 | RTU 设备缺省值同步改 `/dev/ttyS4` |
| `drivers/gpio_sunxi.*` | 确认 PG8 引脚号换算（P 组 `G`=6 → 6*32+8=200）与现有 `HMI_GPIO_*` 宏风格一致 |
| `tina/package/hmi-runtime/Makefile` | 若在 WavePi SDK 上重建：`LVGL_SDK_DIR` 指向 WavePi SDK 里新增的 `package/gui/lvgl`；DEPENDS 不变 |

### 3.4 构建脚本（tina/build.sh）

- `set_model` / `fix_env_bootcmd` / `fix_sys_config_storage` / `fix_disable_lv_examples` /
  `check_sdk_config` 全部硬编码 `configs/mq_r`、`out/t113-mq_r`、`lunch t113_mq_r-tina`、
  镜像名 `tina_t113-mq_r_uart1.img` → 需参数化 **BOARD=bingpi_m2**（7 寸屏）：
  - `lunch t113_bingpi_m2-tina`
  - 产物 `out/t113-bingpi_m2/tina_t113-bingpi_m2_uart3.img`
  - dts 路径 `configs/bingpi_m2/`
- `fix_env_bootcmd`：WavePi 出厂 env.cfg 已是 `bootcmd=run setargs_nand boot_normal`，可跳过或保留幂等校验。
- `fix_sys_config_storage`：WavePi `storage_type=1` 与 mq_r 相同，保持。
- `fix_disable_lv_examples`：WavePi SDK `package/gui/littlevgl-8/lv_examples` 存在，**必须删**（出厂脚本自启 demo 抢占 fb0，同 mq_r 问题）。

### 3.5 编辑器侧（宿主机，非固件）

- **7 寸 1024×600：编辑器无需改出图**（与现有 `AllwinnerT113Target.cs` 的 1024×600 一致）。
- 仅需确认 `CommConfigCodeGen.cs` 的 `rtu.dev` 默认值：IDE「下载到 HMI」下发的
  `comm_config.ini` 中 `rtu.dev` 应改 `/dev/ttyS4`（或由用户在连接配置里改）。

### 3.6 烧录与验证

- 镜像：`tina_t113-WavePI_S6_uart3.img`（PhoenixSuit SPI-NAND 线刷；Windows 驱动 CH340 装好）。
- 参考出厂镜像（资料包 `7_ 出厂验证镜像/`）先验证板子本身：5 寸 = WavePI_S6 镜像、7 寸 = bingpi_m2 镜像。
- 上板验证清单：串口登录（UART3 115200）→ `/dev/fb0` 分辨率 → `evtest` 查 GT911 event 节点 →
  烧入 HMI 固件 → 屏幕渲染 → RS485 回环（ttyS4 + PG8 方向）→ Modbus TCP/MQTT。

## 4. 路线决策

### 路线 A（推荐）：现有 SDK 为主，合入 WavePI_S6 板配置

```
在 firmware/Tina-SDK（已含 lvgl 9.5 + hmi-runtime 集成 + 全部实机修复）基础上：
1. 复制 WavePi SDK 的 configs/WavePI_S6/（+bingpi_m2/）到 device/config/chips/t113/configs/
2. 复制 target/allwinner/t113-WavePI_S6/ 到 target/allwinner/
3. 按 §3.2 改 WavePI_S6 dts（LCD/触摸/UART4/RS485）
4. 按 §3.3 改应用层（runtime_config.h + modbus_rtu.c + comm_core.c）
5. 按 §3.4 参数化 build.sh（BOARD=WavePI_S6）
6. build.sh all → tina_t113-WavePI_S6_uart3.img
```

**优点**：LVGL 9.5 包、hmi-runtime 包、lv_examples 清理、OP-TEE/PSCI 修复全部现成；
内核同源（5.4.61）无 ABI 风险；改动集中在板配置 + 串口/RS485 适配。
**风险**：现有 SDK 的 mq_r 板 dts 被 build.sh 手工改过（HMI_LCD_RGB24 宏），合入 WavePI_S6 时
不要污染；确认 WavePI_S6 dts 里 LCD 引脚组（rgb18）与现有 sunxifb 驱动匹配。

### 路线 B（备选）：WavePi SDK 为主，移植 lvgl 9.5 + hmi-runtime

```
在 /home/chen/下载/WavePi/Tina 上：
1. 复制现有 SDK 的 package/gui/lvgl（9.5，247MB）到 WavePi SDK
2. 复制 firmware/t113/tina/package/hmi-runtime/ 到 package/
3. .config 勾选 hmi-runtime + libfreetype/libpng/libuapi，关 lv_examples
4. 应用层改动同 §3.3；build.sh 需从零写（WavePi SDK 无 build.sh）
5. 重新处理 OP-TEE/PSCI/存储等实机坑（flashing-guide ①②③④⑤）
```

**优点**：SDK 板配置天然带 WavePI_S6（官方出厂验证过）。
**缺点**：LVGL 9.5 包搬运 247MB、全部实机修复重做、构建脚本重写 —— 工作量大且重复。
**适用**：若现有 SDK 与 WavePi SDK 版本差异过大（如内核补丁不同）才考虑。

> **建议：先走路线 A**。若合入后内核/驱动行为异常（例如 WavePi 板级差异导致
> 现有 SDK 内核补丁不兼容），再降级到路线 B。

## 5. 分步执行计划

| # | 步骤 | 产出/验收 |
| --- | --- | --- |
| 1 | 备份现有 SDK 板配置；复制 **bingpi_m2**（+WavePI_S6 可选）板配置与 target 到现有 SDK | `ls configs/` 见 bingpi_m2 |
| 2 | **7 寸屏**：bingpi_m2 dts 启用 1024×600 块（board.dts + uboot-board.dts），触摸 max 改 0x400/0x258 | dts 编译通过，fb 尺寸 1024×600 |
| 3 | 应用层：runtime_config.h（UART4 触摸节点）、modbus_rtu.c（UART4 基址 + PG8 方向）、comm_core.c 缺省 | 交叉编译 0 错误 |
| 4 | build.sh 参数化 BOARD=bingpi_m2（lunch 名/镜像名/dts 路径） | `out/t113-bingpi_m2/tina_t113-bingpi_m2_uart3.img` |
| 5 | 烧录出厂镜像验证板子（屏幕/触摸/串口），再烧 HMI 固件 | 串口有日志、屏幕出 HMI 界面 |
| 6 | RS485 回环验证（ttyS4+PG8）、Modbus TCP、MQTT、软键盘、.so 热更新 | 与 mq_r 版功能对齐 |
| 7 | 更新 README.md / version-info.md / flashing-guide.md / firmware.json（t113→WavePI-S6 7 寸） | 文档一致 |

## 6. 风险与注意事项

1. **RS485 方向控制**：WavePi SP485 的 DE/RE=PG8 是纯 GPIO，若 UART4 硬件 RS485（MCR 模式）不可用，
   必须用 GPIO 切换（发送前拉高、发送后拉低，注意末尾字节/串口 FIFO 排空时机，否则丢最后一个字节）。
   建议先试 `linux,rs485-enabled-at-boot-time`（若 sunxi-uart 对 uart4 同样生效）→ 不行再 GPIO。
2. **触摸 event 节点号**：GT911 在 WavePi 上的 `/dev/input/eventX` 编号需上板 `evtest`/`cat /proc/bus/input/devices` 确认，不能沿用 event2 假设。
3. **屏幕选择影响全局**：已定 7 寸 1024×600 —— 与现有工程一致，编辑器/生成代码/`HMI_LCD_WIDTH/HEIGHT`
   均无需改；仅 dts 启用 1024×600 块 + 触摸 max_x/y 改 0x400/0x258。
   若未来换 5 寸 800×480，则需反过来（dts 切回 800×480、触摸 0x320/0x1e0、编辑器出图改 800×480）。
   注意：**7 寸官方出厂镜像用 bingpi_m2 板配置**（`tina_t113-bingpi_m2_uart3_7.img`），
   5 寸用 WavePI_S6 板配置 —— 本方案按 7 寸走 bingpi_m2。
4. **WavePi SDK 的 LVGL**：出厂例程是 8.1.0；现有固件是 9.5.0。**不要**用 littlevgl-8 重写，
   直接复用现有 `package/gui/lvgl`（9.5）。
5. **CH340E 串口**：WavePi 板载 CH340E 接 UART3，Windows 调试需装 CH341SER 驱动（资料包 1_工具软件/串口驱动）。
6. **分区/存储**：WavePi 出厂 sys_partition 与 mq_r 结构相近（boot/rootfs/UDISK），
   `storage_type=1` 一致；不必改分区，除非固件体积超限。
7. **7 寸镜像用 bingpi_m2 板名**：资料包 7 寸镜像名 `tina_t113-bingpi_m2_uart3_7.img`，说明官方 7 寸屏
   用 bingpi_m2 配置（与 WavePI_S6 的 dts 仅 OLED 引脚注释不同）；**本方案 7 寸屏即走 bingpi_m2**。

## 7. 参考资料（资料包内）

- `0_.必看_快速上手文档/WavePi-S6快速上手_V2.0.pdf`：烧录/启动/网络/接口测试
- `2_快速上手编程教程/WavePI-S6上手编程教程.docx`：SDK 目录、交叉编译、U 盘传输
- `5_原理图&外扩引脚说明/`：WavePI-S6原理图.pdf（U28 SP485 / U34 SN65HVD230 / CH340E）、外扩引脚说明.png
- `7_ 出厂验证镜像/`：5 寸 `tina_t113-WavePI_S6_uart3_5.img`、7 寸 `tina_t113-bingpi_m2_uart3_7.img`
- `8_SDK/`：单独内核/Tina/Uboot 配置、board.dts、环境配置方法
- `11_LVGL_QT例程/LVGL/`：LVGL 8.1.0 移植教程（仅参考流程，版本不采用）、上电自运行方法
- 原理图串口分配（§2.2 结论来源）：UART3=PB6/PB7(debug)、UART4=PB2/PB3(SP485)、CAN1=PB4/PB5、RS485_EN=PG8

---

*本文档由移植调研生成；关键硬件结论来自 WavePi SDK dts/sys_config/原理图，实测以板为准。*

---

## 8. 实机验证结论（2026-08-19）

### 已解决

| 问题 | 根因 | 修复 |
| --- | --- | --- |
| hmi_runtime 崩溃（malloc 2.4MB SIGSEGV） | **sunxifb G2D/ION 破坏 musl 堆**：WavePi 内核 ION 驱动差异导致 sunxifb_init 的 G2D 分支（SunxiMemOpen/Palloc）写坏堆元数据 | `sunxifb.h` `USE_SUNXIFB_G2D=0` + `G2D_ROTATE=0`；sunxifb.c/sunximem.h/sunxig2d.h 的 `#ifdef`→`#if` 值判断、`defined(LV_USE_SUNXIFB_G2D_*)`→值判断 |
| 大 malloc 失败 | **overcommit_memory=0 时 CommitLimit 仅 54MB**（cma=48M），LVGL 池+fb+freetype 提交超限 | S97hmiruntime 开机 `echo 1 > /proc/sys/vm/overcommit_memory` |
| 触摸 INT 引脚配错 | dts `ctp_int_port=PD22`，原理图 TP_INT=**PE5**（LCD FPC 引脚3） | 改 `ctp_int_port = <&pio PE 5>` |
| twi2 engine-mode EINVAL | 疑似 DMA 时序（START 超时） | 尝试禁 twi2 `dmas`（PIO 模式，见 §8.2） |

### 验证结果

- 固件 `tina_t113-bingpi_m2_uart3.img` 烧录成功，HMI 在 7 寸 1024×600 正常显示
- `screens loaded: 2 (ui_loaded=1)`、字体修复、Modbus 轮询、开机设置窗口全部正常
- 串口 ttyUSB0（CH340）、adb（20080411）

### 遗留

1. **GT911 触摸无响应（已定位，2026-08-19 用户确认屏型号）**：用户确认触摸屏是
   **t113-mq_r 板配套的屏**（mq_r 上可正常触摸）。该屏 GT911 实际 I2C 地址
   **0x5d**（mq_r dts 配 0x5d + INT=PB6 + WKUP=PB7 + revert 1/1 工作正常），
   而 WavePi 官方 dts 配 0x14 + PD22 —— **地址与引脚全不匹配此屏**。
   修复（bingpi_m2 dts）：ctp@5d / ctp_twi_addr=0x5d / ctp_int_port=PE5
   （WavePi 板 FPC TP_INT）/ ctp_wakeup=PG10（TP_RST）/ revert 1/1（对齐 mq_r）。
   另恢复 twi2 DMA（mq_r 同配置工作正常，之前 EINVAL 是 0x14 地址 NACK 所致，非 DMA）。
   待整编烧录验证。
2. 触摸驱动无 `ctp_power_io` 配置（`ctp_power is fail` 警告，但驱动用 dummy regulator 继续）
3. 诊断 DBG 日志（main.c/lcd_sunxifb.c/sunxifb.c）待清理
