# WavePI-S6 (T113-S3) 固件编译容器

这是一个用于编译 Tina Linux 固件的 Docker 容器，针对 WavePI-S6 开发板 (T113-S3 SoC) 优化。

## 目录结构

```
bingpi-m2-build/
├── Dockerfile              # 容器定义
├── docker-compose.yml      # Docker Compose 配置
├── build.sh                # 构建脚本
├── docker-entrypoint.sh    # 容器入口脚本
├── README.md               # 本文件
├── Tina/                   # Tina SDK (需挂载)
├── out/                    # 构建输出 (持久化)
├── patches/                # 自定义补丁
└── configs/                # 配置文件
```

## 快速开始

### 1. 准备 Tina SDK

确保将 Tina SDK 挂载到容器中。可以是官方 SDK 或你的修改版本：

```bash
# 下载官方 SDK
git clone https://github.com/Tina-Linux/Tina-SDK.git Tina
# 或者复制已有的 SDK 到当前目录
cp -r /path/to/your/Tina ./Tina
```

### 2. 构建容器

```bash
docker-compose build
```

### 3. 启动容器

```bash
# 交互式进入容器
docker-compose run --rm tina-builder bash

# 或者直接运行构建脚本
docker-compose run --rm tina-builder ./build.sh
```

### 4. 在容器内构建

```bash
# 进入容器后
cd /workspace

# 设置环境
source /workspace/Tina/build/envsetup.sh

# 选择目标板
lunch t113_bingpi_m2-tina

# 构建（使用所有 CPU 核心）
make -j$(nproc)

# 产物在 /workspace/out/t113-bingpi_m2/
```

## 常用命令

### 查看帮助
```bash
./build.sh help
```

### 编译特定目标
```bash
./build.sh bingpi_m2 tina_t113-bingpi_m2_uart3
```

### 清理构建
```bash
make clean
```

## 自定义配置

### 添加字体支持

编辑 `Tina/device/config/chips/t113/configs/bingpi_m2/linux-5.4/board.dts`：

```dts
ctp@14 {
    compatible = "allwinner,goodix";
    device_type = "ctp";
    reg = <0x14>;
    status = "okay";
    ctp_name = "gt9xxnew_ts";
    ctp_twi_id = <0x2>;
    ctp_twi_addr = <0x14>;
    
    // 启用触摸电源
    ctp_power_ldo = <&reg_aldo2>;  // 或使用 GPIO 方式
    ctp_power_ldo_vol = <3300>;
    
    // 触摸分辨率
    ctp_screen_max_x = <0x320>;
    ctp_screen_max_y = <0x1e0>;
    
    // 中断引脚
    ctp_int_port = <&pio PD 22 GPIO_ACTIVE_HIGH>;
    
    // 唤醒引脚
    ctp_wakeup = <&pio PG 10 GPIO_ACTIVE_HIGH>;
};
```

### 启用 FreeType 字体

在 `.config` 中确保：
```
CONFIG_LVGL8_USE_FREETYPE=y
CONFIG_PACKAGE_libfreetype=y
CONFIG_PACKAGE_harfbuzz=y
CONFIG_PACKAGE_pango=y
CONFIG_PACKAGE_fontconfig=y
```

## 构建产物

构建成功后，固件文件位于：
- 调试固件: `out/t113-bingpi_m2/tina_t113-bingpi_m2_uart3.img`
- 根文件系统: `out/t113-bingpi_m2/rootfs.img`
- 内核: `out/t113-bingpi_m2/t113-bingpi_m2-uImage`

## 已知问题

### 触摸不稳定

原因: CTP 电源供应未在 DTS 中正确配置

解决方案:
1. 在 board.dts 中添加 `ctp_power_ldo` 或 `ctp_power_io`
2. 或使用 GPIO 控制电源

### 字体显示方框

原因: 内置字体子集不完整

解决方案:
1. 构建时包含完整 CJK 字体文件
2. 或将字体文件放置在 UDISK 的 `/mnt/UDISK/hmi/fonts/`

## 容器调试

```bash
# 查看容器日志
docker logs tina-bingpi-m2-builder

# 进入正在运行的容器
docker exec -it tina-bingpi-m2-builder bash

# 停止容器
docker-compose down
```

## 注意事项

1. 容器需要挂载完整的 Tina SDK，构建过程会修改文件
2. 输出目录建议使用持久化卷，避免构建失败时数据丢失
3. 首次构建可能需要数小时，视硬件性能而定
4. 确保宿主机有足够的磁盘空间（推荐 50GB+）