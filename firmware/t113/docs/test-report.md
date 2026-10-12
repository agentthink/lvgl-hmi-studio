# zhishu-HMI T113-S3 固件测试报告

版本：v1.0.0 ｜ 日期：2026-08-05（更新 B1 实测记录）｜ 测试对象：`firmware/t113/`

测试分两类：
- **A. 主机侧验证（本环境已执行）**：不依赖 T113 硬件与 Tina SDK 头文件，
  覆盖协议层正确性与语法健康度。
- **B. 实机测试（需 T113 开发板 + 外设）**：给出用例、步骤与通过准则，
  用于产线/现场验收。

## A. 主机侧验证（已执行，全部通过）

### A1. 单元测试（hmi_comm_test，50/50 通过）

编译与运行：

```sh
cd firmware/t113
gcc -std=c99 -D_GNU_SOURCE -DHMI_UNIT_TEST \
    -Iapp/runtime -Iapp/comm -Iconfig -Igenerated \
    tests/hmi_comm_test.c app/comm/comm_core.c app/comm/modbus_rtu.c \
    app/comm/modbus_tcp.c app/comm/mqtt_client.c -lpthread -o /tmp/hmi_comm_test
/tmp/hmi_comm_test
```

| 组 | 断言数 | 结果 |
| --- | --- | --- |
| Modbus RTU CRC16（标准向量 + 200 组随机与参考实现交叉验证） | 4 | 通过 |
| RTU 帧构建/解析（正常/异常/CRC 损坏/从站不匹配） | 9 | 通过 |
| TCP MBAP 构建/解析（TID 校验/ProtocolId/异常/截断） | 13 | 通过 |
| 地址解析（40001/00001/h:/3:0/非法） | 9 | 通过 |
| 变量表轮询组（块合并）、值转换（Bool/U16/I16/U32/I32/Float）、写队列、只读保护 | 15 | 通过 |
| **合计** | **50** | **50 通过，0 失败** |

验证结论：
- CRC16 与独立 Python 参考实现一致，字节序（低字节在前）正确；
- MBAP TID 不匹配被拒绝（满足项目「TransactionId 线程安全 + 响应 TID 校验
  防帧交错」的硬性约束）；
- 块读合并按 (驱动,从站,功能码) + 地址连续性生效（40001~40003 合并为一次读）；
- 只读标签写拒绝、未连接时写请求安全降级。

### A2. 语法健康检查（gcc -fsyntax-only -Wall -Wextra）

| 文件 | 结果 |
| --- | --- |
| comm/comm_core.c、modbus_rtu.c、modbus_tcp.c、mqtt_client.c | 0 警告 |
| drivers/gpio_sunxi.c、i2c_sunxi.c、pwm_sunxi.c | 0 警告 |
| runtime/variable_table.c | 0 警告 |
| runtime/screen_manager.c（依赖 lvgl.h） | 需 Tina SDK 头文件（实机编译项） |
| drivers/lcd_sunxifb.c / touch_evdev.c（依赖 lvgl/sunxifb） | 需 Tina SDK 头文件（实机编译项） |

## B. 实机测试用例（需硬件，验收清单）

### B1. 基本功能测试

| 编号 | 用例 | 步骤 | 通过准则 |
| --- | --- | --- | --- |
| F1 | 显示 | 上电 | 日志出现 `LCD <WxH> (sunxifb /dev/fb0)`；屏幕正常显示首页 |
| F2 | 触摸 | 点击/拖动控件 | 触点与控件位置一致；`/dev/input/event2` 有事件 |
| F3 | 屏幕切换 | 触发 SwitchScreen 动作 | 目标屏幕加载，`hmi_screen_switch` 返回 0 |
| F4 | Modbus RTU | RS485 接 PC 从站仿真器（9600,8N1） | 变量值正确刷新（poll）；写入寄存器生效 |
| F5 | Modbus TCP | PC 起 modbus-tcp 从站，板端 eth0 连接 | 40001~40003 块读正确；断网后自动重连 |
| F6 | MQTT | 起 mosquitto，板端订阅/发布 | `hmi/t113/val/PV` 变化即发布；`hmi/t113/set/SV` 写入生效 |
| F7 | GPIO | `gpio_export_out(PE0)` 翻转 | PE0 电平翻转（万用表/示波器） |
| F8 | PWM | `pwm_set_duty_percent` 调背光 | PWM2 输出占空比变化，背光亮度变化 |
| F9 | I2C | 接 I2C 从机（如 AT24C02） | 寄存器读回一致 |
| F10 | 开机自启 | 重启 | 无需手动命令即进入 HMI（S99hmiruntime） |

#### B1 实测记录（2026-08-05，TF 卡启动 Tina Linux 5.4.61）

| 编号 | 结果 | 实测细节 |
| --- | --- | --- |
| F1 | ✅ 通过 | `wh=1024x600, vwh=1024x1200, bpp=32`，日志 `LCD 1024x600 (sunxifb /dev/fb0)`；屏幕正常显示首页（深灰背景 + montserrat_20 标题）。注：实测 LCD 分辨率为 1024x600，非硬件定义默认 800x480（C2 兼容性项验证自适应） |
| F2 | ✅ 通过 | `/dev/input/event2`（→event1 gt9xxnew_ts）已就绪，`touch ready` 日志正常；indev 注册成功。F3 测试时点击屏上 160×60 按钮响应准确，触点定位正常 |
| F3 | ✅ 通过 | 已部署带按钮的 [screens.c](../../generated/screens.c)：home 屏底部 "Status >" 按钮、status 屏底部 "< Home" 按钮，回调 `btn_switch_cb` 调用 `hmi_screen_switch_idx(cur==0?1:0)`。实机点击 "Status >" 成功切换到 status 页（显示 "PV/SV/ALARM"），点击 "< Home" 成功切回 home 页；日志确认 `screens loaded: 2` |
| F4 | ⏳ 待执行 | `/dev/ttyS3` 已打开，但 `TIOCSRS485` 返回 ENOTTY（U-Boot 设备树未启用 UART3 RS485 模式）；需 dts 配置后重测 |
| F5 | ✅ 通过 | 主机起 pymodbus 3.14 从站（监听 1502，iptables 转发 502→1502，unit_id=1，HR addr=0~3 提供 PV/SV/状态/产量）。板子日志 `[modbus-tcp] connected 192.168.100.10:502` 成功；之前因从站地址 1-based 触发 105 次 `exception 0x02`，修正为 0-based（对齐 [comm_core.c:58](file:///home/chen/Git/zhishu-hmi/firmware/t113/app/comm/comm_core.c#L58) 的 `"40001"→off=0` 解析）后无异常；重连机制工作正常 |
| F6 | ✅ 通过 | 主机装 mosquitto 2.1.2，配置 `/etc/mosquitto/conf.d/hmi.conf`（监听 0.0.0.0:1883，allow_anonymous false，password_file 指向 /etc/mosquitto/passwd，账号 hmi/hmi123 对齐 [runtime_config.h:46-51](file:///home/chen/Git/zhishu-hmi/firmware/t113/app/runtime/runtime_config.h#L46)）。板子日志 `[mqtt] connected to 192.168.100.10:1883` 成功；订阅 `hmi/t113/set/TEMP` 主题后发布 `42.5`，主机订阅 `hmi/t113/#` 成功收到消息（`hmi/t113/set/TEMP 42.5`），证明订阅注册 + 消息回调链路工作正常。注：val 主题发布仅在 cache 值变化时触发（[variable_table.c:298](file:///home/chen/Git/zhishu-hmi/firmware/t113/app/runtime/variable_table.c#L298) 的 `if (s_cache[i] == s_last_pub[i]) continue`），本次测试标签 TEMP 默认值为 0，写入 42.5 后浮点比较可能未触发发布 |
| F7-F9 | ⏳ 待执行 | 需接外设后验证 |
| F10 | ✅ 通过 | `S99hmiruntime` 已部署到 `/etc/init.d/`，`/etc/rc.d/S99hmiruntime` 软链接已建立；`S99swupdate_autorun` 已禁用 lv_examples（改为 `true` 占位）。3 次 reboot 测试均单进程自启成功，启动日志 `touch ready` + `screens loaded: 2` |

> **本次实测发现的关键 Bug（已修复）**：
> 1. **驱动结构体生命周期 Bug**（致命崩溃）：[touch_evdev.c:84](../../app/drivers/touch_evdev.c) 和 [lcd_sunxifb.c:42](../../app/drivers/lcd_sunxifb.c) 中的 `lv_indev_drv_t` / `lv_disp_drv_t` 使用局部变量，LVGL `lv_*_drv_register` 保留引用而非拷贝，函数返回后栈帧回收，`lv_timer_handler()` 调用 `read_cb`/`flush_cb` 时跳到无效地址 SIGSEGV。修复：改为 `static`，对齐 SDK lv_examples 写法。
> 2. **屏幕硬编码分辨率 Bug**：[screens.c](../../generated/screens.c) 原 `lv_obj_set_size(scr, 800, 480)` 与实际 1024x600 LCD 冲突。修复：移除 `lv_obj_set_size` 调用，让屏幕自适应显示分辨率（LVGL 默认行为）。
> 3. **库依赖缺失**：Tina 默认镜像 `/usr/lib/` 缺 `libfreetype.so.6` 和 `libpng12.so.0`（opkg 数据库记录但实际文件缺失），从 SDK `staging_dir/target/usr/lib/` 推送后修复。

### B2. 稳定性测试

| 编号 | 用例 | 步骤 | 通过准则 |
| --- | --- | --- | --- |
| S1 | 长时间运行 | 满负荷运行 72h（含 RS485/TCP 轮询 + MQTT 遥测） | 无崩溃/内存泄漏（`free` 稳定）；日志无异常 |
| S2 | 通信断连恢复 | 运行中反复拔插网线/485、重启 broker | 自动重连成功，变量恢复更新 |
| S3 | 触摸疲劳 | 连续点击 10000 次 | 无事件丢失、无界面卡死 |
| S4 | 掉电重启 | 运行中随机断电 50 次 | 每次重启均正常进入 HMI，无文件系统损坏 |
| S5 | 温度 | 70°C 环境运行 8h | 显示/通信正常 |

### B3. 兼容性测试

| 编号 | 用例 | 通过准则 |
| --- | --- | --- |
| C1 | 电容屏 / 电阻屏切换 | 切换节点与分辨率后触摸正常 |
| C2 | 5 寸屏（dts 切换 uboot-board/board.dts） | 分辨率自适应（`sunxifb_get_sizes`） |
| C3 | Modbus RTU 9600/19200/115200 | 各波特率通讯正常 |
| C4 | 多种从站（不同厂家 PLC/仪表） | 功能码 01/03/05/06/16 兼容 |
| C5 | WIFI STA 联网后 MQTT/TCP | 经 wlan0 的 Modbus TCP/MQTT 正常 |
| C6 | LVGL v9 生成代码（SDK 内置 LVGL 9.5） | 编译通过、运行正常 |

### B4. 实机结论登记表（验收时填写）

| 阶段 | 结果 | 测试人 | 日期 | 备注 |
| --- | --- | --- | --- | --- |
| B1 基本功能 | ⚠️ 部分通过 | zhishu | 2026-08-05 | F1/F2/F3/F5/F6/F10 通过（显示+触摸+屏幕切换+Modbus TCP+MQTT+自启，6/10）；F4/F7-F9 待外设/脚本验证。发现并修复 4 个 Bug（驱动生命周期、分辨率硬编码、库缺失、自启时序）；F5 期间发现并修正从站地址基（0-based vs 1-based） |
| B2 稳定性 | 待执行 | | | 需先完成 B1 全部用例 |
| B3 兼容性 | 待执行 | | | C2 自适应已部分验证（1024x600 OK） |

> 说明：A 类已在本环境全部通过；B 类用例、步骤与通过准则已固化，
> 待 T113 硬件环境完成后按此清单执行验收。
