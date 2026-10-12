
---

## 2026-08-19 GT911 触摸排查记录（WavePi-S6）

### 问题现象
- GT911 电容触摸屏在 WavePi-S6 板上无响应
- I2C 地址 0x5d 和 0x14 均无 ACK
- dmesg 显示 `probe of 2-005d failed with error -1`

### 排查过程

#### 1. 确认 I2C 总线状态
- 出厂镜像和自编译镜像均失败
- 50PIN 主屏插入时：总线卡死（`bus not idle`），因为 mq_r 屏的 VLED+ (~9.6V) 灌入 PE12/PE13
- 断开 50PIN 主屏后：总线恢复正常，但 GT911 仍无响应

#### 2. 确认 J17 触摸座定义（从原理图提取）
```
J17 Pin 1: PE12 (SDA)
J17 Pin 2: PE13 (SCL)
J17 Pin 3: TP_INT (PE5)
J17 Pin 4: ? (可能是 GND)
J17 Pin 5: ? (可能是 VDD)
J17 Pin 6: TP_RST (PG10)
```

#### 3. 确认 50PIN 屏引脚冲突
- mq_r 配套屏 (SZCLX070H) 50PIN 引脚 1/2 = VLED+（背光 ~9.6V）
- WavePi J7 主座引脚 1/2 = PE12/PE13（I2C 触摸信号）
- **两者不兼容！** 直接插入会导致背光不亮 + I2C 总线被拉死

#### 4. 硬件验证
- J17 Pin 4/5 测量有 3.3V 供电 ✅
- FPC 引脚排列与 J17 一致 ✅
- FPC 方向正确（Pin 1 对齐）✅
- 断开 50PIN 屏后 I2C 总线恢复 ✅

#### 5. 仍无响应的原因（未解决）
可能原因：
- FPC 接触不良（需要重新插拔测试）
- GT911 芯片损坏
- I2C 时序/上拉电阻问题
- FPC 实际引脚顺序与理论不符

### 解决方案
**等待 WavePi 官方兼容屏到货**，该屏的 50PIN FPC 是定制定义：
- 引脚 1/2/3/6 = I2C 触摸信号（SDA/SCL/INT/RST）
- 背光电源由其他专用引脚提供

### WavePi 官方 dts 配置参考
```dts
ctp@5d {
    compatible = "allwinner,goodix";
    reg = <0x5d>;
    ctp_twi_id = <0x2>;
    ctp_int_port = <&pio PE 5 GPIO_ACTIVE_HIGH>;
    ctp_wakeup = <&pio PG 10 GPIO_ACTIVE_HIGH>;
    ctp_screen_max_x = <0x400>;
    ctp_screen_max_y = <0x258>;
};
```

### 待办事项
- [ ] 官方屏到货后测试触摸功能
- [ ] 确认官方屏 50PIN 引脚定义与 J7 匹配
- [ ] 验证显示 + 触摸同时工作

