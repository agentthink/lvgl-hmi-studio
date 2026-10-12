# zhishu-HMI T113-S3 固件版本信息

## 当前版本：v1.1.0（ABI V3 热更新 + 软键盘 + 开机设置窗口 + WavePI-S6 7 寸支持）

| 项 | 值 |
| --- | --- |
| 固件名称 | hmi-runtime |
| 版本 | 1.1.0 (release 1) |
| 目标平台 | Allwinner T113-S3（t113-mq_r 板 / WavePI-S6 7 寸 bingpi_m2，HMI_BOARD_WAVEPI 切换） |
| 目标架构 | ARM Cortex-A7 双核（arm-openwrt-linux） |
| 显示 | 1024x600 RGB666，双缓冲，sunxifb + g2d |
| GUI | LVGL 9.5.0（Tina SDK package/gui/lvgl） |
| 系统 | Tina-Linux（OpenWrt 5.4 内核） |
| 通信 | Modbus RTU (RS485：mq_r=/dev/ttyS3, WavePI=/dev/ttyS4) / Modbus TCP / MQTT 3.1.1 |
| 中文字体 | Noto Sans CJK SC 完整简体（font_cjk_sc.ttf，16MB 内置出厂 + UDISK 可更新） |
| 软键盘 | 数字键盘 / 中英文键盘（拼音 IME，417 音节 6033 候选字，字频排序） |
| 构建工具链 | `arm-openwrt-linux-muslgnueabi-gcc`（SDK prebuilt） |
| 与编辑器对齐 | `AllwinnerT113Target.cs`（1024x600 / 128MB DDR3 / 128MB SPI NAND） |

## 构建记录

```
源码：firmware/t113/  （zhishu-hmi 仓库）
编译：tina/build.sh all <TINA_ROOT> [BOARD]
  BOARD=mq_r      → out/t113-mq_r/tina_t113-mq_r_uart1.img
  BOARD=bingpi_m2 → out/t113-bingpi_m2/tina_t113-bingpi_m2_uart3.img（WavePI-S6 7 寸）
固件版本宏：HMI_RUNTIME_VERSION_STR（app/runtime/hmi_runtime.h）
```

## WavePI-S6 7 寸迁移（2026-08-18）

详见 [docs/wavepi-s6-migration-plan.md](wavepi-s6-migration-plan.md)：

- 板配置合入 `device/config/chips/t113/configs/bingpi_m2/` + `target/allwinner/t113-bingpi_m2/`
- bingpi_m2 dts 启用 1024×600（board.dts 1453–1494 行 / uboot-board.dts 429–471 行），触摸 max 改 0x400/0x258
  > **现状校正（2026-09-13）**：已提交的 `bingpi_m2/linux-5.4/board.dts` 实际仍为
  > `ctp_screen_max_x/y = 0x320/0x1e0`（800×480，注释写明"保留官方触摸控制器范围"），
  > 即上面这条"改 0x400/0x258"**并未落到源码**。因此固件侧
  > `runtime_config.h` 的 `HMI_TOUCH_MAX_X/Y` 现在按该 DTS 取 **800×480** 归一
  > （此前按 1024×600 归一导致右侧 224px/底部 120px 触摸不可达，审查 P0-7）。
  > 二者必须保持一致：**改 dts 的 `ctp_screen_max` 就必须同步改这两个宏**（或用
  > `-DHMI_TOUCH_MAX_X/Y=...` 覆盖）。
- 串口：调试 UART3（/dev/ttyS3），RS485 UART4（/dev/ttyS4，SP485 DE/RE=PG8 GPIO 方向控制）
- modbus_rtu.c 按 `HMI_RS485_GPIO_PIN`（WavePI=200）切换 GPIO 方向控制 / mq_r 硬件 RS485
- build.sh 参数化 `BOARD=mq_r|bingpi_m2`
- WavePI-S6 出厂即带 optee（官方验证），不禁 TEE/PSCI/CAN（与 mq_r 不同）

## v1.1.0 核心变更（2026-08-16）

### 热更新（ABI V3，项目无关）
- **字体**：.so 不再 extern `font_<assetid>` 符号（换工程即 dlopen 失败），
  改由宿主 `hmi_resolve_font(font_id, file, size)` 运行时解析（FreeType 缓存 16 槽）；
  字体文件优先 `/mnt/UDISK/hmi/assets/<file>`（IDE 部署/OTA），回退出厂目录与设备 CJK 字体。
- **标签表**：.so 内嵌 `HMI_UI_TAG_META/HMI_UI_TAG_BINDINGS`（tags.h 自包含），
  宿主 `hmi_ui_load` 时经 `get_tags/get_tag_bindings` 采纳，缓存/只读保护/轮询组/
  MQTT 主题全部按工程标签重建 —— 替换 .so 即替换标签表，工程无关。
- **ABI 版本**：`HMI_UI_ABI_VERSION = 3`；宿主与 .so 均按版本严格校验。

### 软键盘（soft_keyboard.c + pinyin_ime_data.c）
- 宿主接管全部 TextArea（屏幕创建后遍历控件树挂焦点回调），任意工程输入框可用；
- 数字键盘（num）/ 中英文键盘（text：拼音候选 + 英文字母 + 数字符号页）；
- 拼音输入：前缀匹配 + 精确音节优先，候选按字频排序（pinyin-data + 词频表生成）；
- 生成代码 `hmi_set_ta_keyboard_mode(ta, "num"|"text")` 标记模式；
- 调试：`kill -USR1` 弹键盘（奇偶次 text/num），`kill -USR2` 拼音流程自测/插文本。

### 字体内置
- `files/font_cjk_sc.ttf`（Noto Sans CJK SC 全量，16MB）随固件出厂；
  缺失时构建不中断（Makefile 条件安装 + 警告），运行时回退 UDISK/内嵌子集。

### 开机设置入口窗口 + 清理 LVGL demo（2026-08-17）
- **入口改为开机窗口**：开机后 `HMI_SETTINGS_BOOT_WINDOW_MS`（默认 5000ms，
  `app/runtime/runtime_config.h`）内单击屏幕任意位置进入系统设置；窗口超时未点击
  或退出设置页后自动进入工程运行页（第 0 屏）。取代原「60s 内 5 连击」隐藏入口。
- **清理 lv_examples**：SDK 自带 `S99swupdate_autorun` 开机启动 LVGL widgets demo
  抢占 /dev/fb0 盖住 HMI（实机定位）。`tina/build.sh` 新增 `fix_disable_lv_examples`
  删除 SDK 内 lv_examples 包目录 + `CONFIG_PACKAGE_lv_examples` 置 n（仅 sed .config
  会被 conf --defconfig 翻回 y，删包才根治）；`S97hmiruntime` 另在开机时注释掉
  S99swupdate_autorun 中的 lv_examples 行（双保险，S97 先于 S99）。

### 显示/字体实机修复（2026-08-17）
- **LVGL 堆 4MB → 16MB**（`config/lv_conf.h`）：工程大图（1024x600 ARGB8888≈2.4MB）
  经 lv_bmp.c 完整解码路径需整图缓冲，4MB 堆分配失败导致图片空白（实机定位）。
- **宿主级字体修复 `hmi_screens_fix_fonts()`**（screen_manager.c）：屏幕创建后
  遍历控件树，把下拉框弹出列表字体补设为工程 CJK 字体（生成代码对列表设字体时
  取到的还是默认 montserrat → 弹出列表中文方框）；并把下拉框主体文字色补设为
  浅色 0xe2e8f0（创建时缓存了默认深色文字 → 深色控件上不可见）。项目无关。

## 功能清单（v1.0.0 起）

- [x] 显示驱动：sunxifb 初始化 / 分辨率自适应 / 双缓冲（g2d 可选加速）
- [x] 触摸驱动：evdev（电容 /dev/input/event2 默认，电阻可切），坐标缩放
- [x] 屏幕管理：多屏创建 / 注册表 / 按名称切换（generated/screens.c）
- [x] 变量表：缓存、scale/offset 换算、只读保护、块读合并轮询、写队列
- [x] Modbus RTU：RS485（/dev/ttyS3），CRC16，异常响应，线程安全
- [x] Modbus TCP：MBAP，TID 线程安全递增 + 响应 TID 校验（防帧交错），自动重连
- [x] MQTT：QoS0 发布/订阅，断线重连自动重订阅，保活
- [x] GPIO / I2C / PWM：sysfs 电平控制、I2C 寄存器读写、PWM 周期/占空比
- [x] 开机自启：/etc/init.d/S99hmiruntime

## 变更记录

### v1.0.0（2026-08-03）
- 首版交付：运行时框架、通信驱动、硬件驱动、Tina 集成、文档与测试。

## 已知限制

1. 运行时基于 Tina SDK 内置 **LVGL 9.5** 编译（编辑器导出 v9 语法直接可用，
   `config/lv_v8_compat.h` 已随 v9 升级移除；sunxifb 显示驱动已移植进 `app/drivers/`）。
2. MQTT 仅支持 QoS0；Float/U32 多寄存器读写按大端序高字在前约定。
3. GPIO 输入捕获需删除 dts 中 ledc 配置后重编译镜像（见 tina/dts/README.md）。
4. 电阻触摸需同步修改触摸节点与触摸分辨率（见 tina/dts/README.md）。
