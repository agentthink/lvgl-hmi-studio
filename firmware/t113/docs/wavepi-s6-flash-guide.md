# WavePI-S6 固件刷写与问题修复总结

> 日期：2026-08-25
> 目标板：WavePI-S6（全志 T113-S3 / R528，7 寸 1024×600 屏，SPI NAND 启动）

---

## 一、固件修复内容（已完成并验证）

### 1. 启动崩溃修复（核心）

| 问题 | 根因 | 修复 |
|------|------|------|
| 固件刷入后无法启动 | 内核缺少 `CONFIG_SQUASHFS_ZLIB`，无法解压 zlib 压缩的 rootfs | `config-5.4` 启用 `CONFIG_SQUASHFS_ZLIB=y` |
| boot-resource 无 DTB | `board.dtb` 未生成，u-boot 无法加载设备树 | 手动编译 `board.dts → board.dtb`（68KB） |
| 工具链 ld 段错误 | musl 工具链 `ld -b binary` 有 bug | 用 `objcopy --rename-section` 手动生成 piggy.o |
| 触摸 pinmux 导致挂起 | `pinctrl-0` 强制 PD22 为 gpio_in，中断申请失败 | 移除 ctp 节点 pinctrl-0 引用（与 factory 一致） |

### 2. 触摸屏修复

- **GT911 配置**：I2C 地址 `0x14`，中断引脚 PD22（hwirq 86），唤醒 PG10
- **runtime_config.h**：`HMI_TOUCH_DEV` 按 `HMI_BOARD_WAVEPI` 自动选择
  - WavePI：`/dev/input/event2`（GT911 电容屏）
  - mq_r：`/dev/input/event0`（电阻屏）
- **硬件诊断结论**：GT911 芯片正常（ID "911"，固件 6.0，I2C 通信正常），
  但感应层无触摸信号 → **FPC 排线接触不良/触摸屏损坏**（硬件问题）

### 3. 字体显示修复

- **main.c**：恢复 FreeType 加载完整 CJK 字体
  - 优先 `/usr/lib/hmi/fonts/font_cjk_sc.ttf`（16MB，44810 字符）
  - 回退 UDISK → 内嵌子集
- **lv_conf.h**：`LV_USE_FREETYPE=1`
- **构建配置**：harfbuzz/pango/fontconfig 已启用

---

## 二、固件产物

| 文件 | 说明 | 大小 |
|------|------|------|
| `out/t113-bingpi_m2/tina_t113-bingpi_m2_uart3.img` | 完整自研固件 | 39MB |
| `out/t113-bingpi_m2/final_test.img` | 测试镜像（factory boot_package + 自研内核/rootfs） | 39MB |
| `out/t113-bingpi_m2/boot.img` | 内核（含 squashfs zlib 支持） | 4.6MB |
| `out/t113-bingpi_m2/rootfs.img` | 根文件系统（含字体） | 30MB |

---

## 三、刷写方法

### 前置条件（⚠️ 非常重要）

**板子必须有稳定供电！** 实测仅 USB 供电时：
- FEL 模式 USB 只能协商到 12M（Full Speed）
- USB 传输窗口仅约 **5 秒**，之后 PHY 失锁，所有传输超时
- openixcli/sunxi-fel 无法完成刷写

**正确供电方式**：
1. **5V/1A 充电头**（或移动电源）→ 数据线 → 板子 Type-C 供电口
2. 电脑 USB → 数据线 → 板子另一个 Type-C 口（仅数据）

### 方法 1：命令行刷写（推荐）

```bash
# 1. 进入 FEL 模式（按住 FEL + 按 RST）
# 2. 运行刷写脚本
cd /home/chen/Git/zhishu-hmi/firmware/t113/scripts
./flash_wavepi.sh --check     # 先检查 USB 状态（可选）
./flash_wavepi.sh             # 刷写完整自研固件
./flash_wavepi.sh --test      # 刷写测试镜像
./flash_wavepi.sh --factory   # 刷回官方镜像
```

### 方法 2：手动刷写

```bash
# 完整自研固件（$SDK_OUT 的取值见 flash_wavepi.sh；可用 ZHISHU_TINA_SDK_OUT 覆盖）
openixcli flash -m full_erase -a reboot \
  "$SDK_OUT/tina_t113-bingpi_m2_uart3.img"

# 官方 factory 镜像（目录可用 ZHISHU_FACTORY_IMG_DIR 覆盖）
openixcli flash -m full_erase -a reboot \
  "$HOME/下载/WavePi/Wave Pi/7_ 出厂验证镜像/7寸1024x600_电容触摸/tina_t113-bingpi_m2_uart3_7.img"
```

### 方法 3：TF 卡刷写（无需 USB 传输）

1. Windows 上运行 PhoenixCard（`$HOME/下载/WavePi/Wave Pi/1_工具软件/SD_CARD镜像烧录工具/phoenixcard4.2.8/PhoenixCard.exe`）
2. 选择固件镜像，写入 TF 卡
3. TF 卡插入板子，上电自动烧录

---

## 四、USB 供电问题诊断

### 症状
- 板子进入 FEL 后，`sunxi-fel -l` 能列出设备
- 但任何 bulk 传输（64B 到 1MB）都超时
- 重连后仅短暂可用（5 秒），随后劣化

### 根因
板子仅靠 USB 供电，传输时电流波动导致电压跌落，USB PHY 失锁。

### 验证方法
```bash
# 检查 USB 速度（480=良好, 12=供电不足）
cat /sys/bus/usb/devices/1-1/speed

# pyusb 传输测试
python3 -c "
import usb.core
dev = usb.core.find(idVendor=0x1f3a, idProduct=0xefe8)
dev.write(0x01, b'\x00'*64, timeout=3000)
print('传输 OK')
"
```

### 解决方案
1. **独立 5V 电源**（最有效）
2. **换高质量 USB 数据线**
3. **带供电的 USB hub**

---

## 五、常见问题

### Q: 刷写时报 "Device reconnect failed"
A: U-Boot 执行后 USB 重连失败，供电不足。连接独立电源后重试。

### Q: 刷写时报 "Encrypted firmware not supported"
A: openixcli 不接受非标准格式镜像。使用 `tina_t113-bingpi_m2_uart3.img`
（由 `pack_img.sh` 正确打包的 IMAGEWTY 格式）。

### Q: 板子刷入自研固件后黑屏/无法启动
A: 检查 boot-resource 是否包含 DTB（`python3 提取检查`），
以及内核是否包含 squashfs zlib（`strings 检查`）。

### Q: 触摸屏无反应
A: 这是硬件问题（GT911 芯片正常但感应层无信号），
请检查触摸屏 FPC 排线连接，或更换触摸屏。

---

## 六、构建固件（如需重新构建）

```bash
# 环境
cd /home/chen/Git/zhishu-hmi/firmware/Tina-SDK
source build/envsetup.sh
lunch t113_bingpi_m2-tina

# 完整构建
make -j$(nproc)

# 打包 PhoenixImage（生成 tina_t113-bingpi_m2_uart3.img）
./scripts/pack_img.sh -c sun8iw20p1 -p tina -b t113-bingpi_m2 -d uart3 \
  -s none -m normal -w none -v none -i none \
  -t /home/chen/Git/zhishu-hmi/firmware/Tina-SDK
```

### 注意事项（构建经验）
1. **内核重编**：修改 config-5.4 后需删除旧产物强制重编
   ```bash
   rm -f lichee/linux-5.4/arch/arm/boot/zImage
   rm -f out/t113-bingpi_m2/boot.img
   ```
2. **工具链**：musl 工具链在
   `prebuilt/gcc/linux-x86/arm/toolchain-sunxi-musl/toolchain/bin/`
3. **piggy.o 生成**：若 ld 段错误，用 objcopy 手动生成
   ```bash
   objcopy -I binary -O elf32-littlearm -B arm \
     --rename-section .data=.piggydata piggy_data piggy_tmp.o
   objcopy --redefine-sym _binary_piggy_data_start=input_data \
     --redefine-sym _binary_piggy_data_end=input_data_end \
     piggy_tmp.o piggy.o
   ```
4. **board.dtb**：若 boot-resource 无 DTB，手动编译
   ```bash
   cpp -nostdinc -I <kernel>/include -I <kernel>/arch/arm/boot/dts \
     -undef -D__DTS__ -x assembler-with-cpp board.dts -o /tmp/board.dts
   dtc -I dts -O dtb -o board.dtb /tmp/board.dts
   ```

---

## 七、Git 提交记录

```
6dcd67e fix(t113): enable CONFIG_SQUASHFS_ZLIB for rootfs mount
ad504c6 fix(t113): revert ctp pinctrl-0 (caused boot hang)
22ae811 fix(t113): GT911 touch pinmux + hardware diagnosis report
```
