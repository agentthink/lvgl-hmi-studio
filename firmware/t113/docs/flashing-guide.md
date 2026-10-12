# zhishu-HMI T113-S3 固件烧录指南

依据《Linux-T113开发文档-开箱上手》整理。T113 支持 **NAND Flash 启动** 与
**TF 卡启动** 两种方式；镜像产出于 Tina SDK（`tina/build.sh all <TINA_ROOT> [BOARD]`）：
- `BOARD=mq_r`（默认）→ `out/t113-mq_r/tina_t113-mq_r_uart1.img`
- `BOARD=bingpi_m2`（**WavePI-S6 7 寸**）→ `out/t113-bingpi_m2/tina_t113-bingpi_m2_uart3.img`

## 0. 前置准备

- 镜像文件：`tina_t113-mq_r_uart1.img` 或 `tina_t113-bingpi_m2_uart3.img`
  （`tina/build.sh all` 打包产物）
- USB 线（数据）＋ 5V/1A 电源（**勿直连 PC type-C 口**，建议充电器 A 口供电，
  供电不足会导致以太网/WIFI 异常）
- 串口：mq_r 板接 UART1（ttyS1）；**WavePI-S6 板接 UART3（ttyS3，板载 CH340E，
  Windows 需装 CH341SER 驱动，见资料包 1_工具软件/串口驱动）**，115200
- Linux 工具：**openixcli**（命令行）/ **openixSuit**（GUI，`openixsuit-app`）
  ```bash
  # 检测设备（FEL 模式，VID:PID=1f3a:efe8）
  openixcli scan
  # 全盘擦除烧录 + 自动重启
  openixcli flash -m full_erase -a reboot <镜像路径>
  ```
  udev 规则已备（/etc/udev/rules.d/99-sunxi-fel.rules，MODE 0666 免 sudo）。

> ⚠ **「Boot1 not found in firmware」的真相（2026-10-10 读源码 + 实机对照复测）**
> openixcli 按 **(maintype, subtype) 二元组**查找（`OpenixCLI/src/flash/fes_handler/boot_download.rs`）：
> - Boot1（PKG 模式 + NAND/eMMC）→ `12345678` / `BOOTPKG-00000000`（SPI-NOR 为 `BOOTPKG-NOR00000`）
> - Boot0（NAND/SPINAND）→ `BOOT` / `BOOT0_0000000000`
>
> Tina `pack` 的产物**本来就满足**这两个键 ⇒ **镜像无需任何修补**，直接
> `openixcli flash` 即可。2026-10-10 实测：未打补丁的
> `tina_t113-bingpi_m2_uart3.img` 一次通过（5 分区 + Boot0/Boot1 全部写入并校验，退出码 0）。
>
> ⚠ **不要再打 2026-08-17 的旧补丁**：它把 `boot_package.fex` maintype 改成 `BOOTPKG`、
> `boot0_nand.fex` 改成 `BOOT0`，**恰好把上面两个查找都改坏** —— 分区先烧完并校验，
> 然后必然卡在「Boot1 not found in firmware」。同一镜像打补丁后失败、不打补丁成功，
> 已对照复测。
>
> 手里若有被旧补丁改坏的镜像，用现在的工具**归一**回期望值（幂等；正常镜像逐字节不变）：
> ```bash
> python3 tools/patch_image_boottype.py --check <镜像>          # 只检查：退出码 0 = 可直接烧录
> python3 tools/patch_image_boottype.py <镜像> <输出镜像>        # 归一到新文件
> ```
> 字段偏移：条目 = 文件名位置-36，maintype@+8（8B）/ subtype@+16（16B），每条目 1024B。
- Windows 工具：PhoenixSuit（NAND 线刷）、PhoenixCard V4.2.8（TF 卡制作）
  （WavePI-S6 资料包用 **PhoenixSuit + FEL 键**，见快速上手 PDF；烧录前可先烧出厂镜像
  `7_ 出厂验证镜像/tina_t113-bingpi_m2_uart3_7.img` 验证板子本身）

## ⚠ 本板固件构建必备配置（2026-08-16 实机定位）

本板（SPI NAND 启动）从 SDK 出包必须满足以下条件，否则卡开机画面/OP-TEE 报错。

`tina/build.sh` **每次构建自动处理**的（幂等，不需要人工介入）：
①（`fix_env_bootcmd`）、②（`fix_sys_config_storage`）、`lv_examples` 清理
（`fix_disable_lv_examples`）、**板载网络**（`fix_board_network`：eth0 静态
`192.168.100.100/24` + 移除 `/etc/profile` 的出厂 eth0 重配块；**开机起 dropbear 在
本包的 `S97hmiruntime` 里**，见 §5）、
以及 **HMI `.so` 热 OTA 的 `swu_next=hmi_restart` 分支**
（`fix_swupdate_hmi_restart`，补进 SDK 的 `package/allwinner/swupdate/swupdate_cmd.sh`：
清 `swu_next` 标记 + `/etc/init.d/S97hmiruntime restart`，**不依赖 procd respawn**）。
> 后者的由来：设计稿要求 swupdate 写完 UDISK 新 `.so` 后由 `swu_next=hmi_restart` 触发
> 重启，但 SDK 出厂脚本只处理 `swu_next=reboot` ⇒ 热 OTA **实际要等重启才生效**。
> 2026-10-10 由 build.sh 补上；`S97hmiruntime` 的 `restart` 已在同一轮修好
> （只定义 `stop()` 会被 rc.common 覆盖成 `stop_service`，见 §3）。

③④⑤⑥⑦ 为 SDK 级配置（build.sh 每次构建会校验提示）。

> ⚠ **SDK 级包开关要改 `target/allwinner/t113-<board>/defconfig`，不要改 `.config`**
> （2026-10-10 实测定位）：本 SDK 的 `.config` 是**从板级 defconfig 派生**的 —— 手工
> `sed .config` 打开 `CONFIG_PACKAGE_openssh-server=y` 后构建，符号会被原样翻回
> `is not set`；对照 `.config` 与 `defconfig` 的 `CONFIG_PACKAGE_*=y` 计数完全一致
> （110/110）。这也解释了为什么 §⑥ 的 procd 想开却总开不上。
> 例：`sed -i 's|^# CONFIG_PACKAGE_procd is not set$|CONFIG_PACKAGE_procd=y|' \
>   target/allwinner/t113-bingpi_m2/defconfig`
> ⑦ 由 `build.sh` 的 `fix_ssh_server` 自动写 defconfig（幂等）。

| # | 位置 | 配置 | 原因 |
|---|---|---|---|
| ① | `device/config/chips/t113/configs/mq_r/env.cfg` | `bootcmd=...setargs_nand boot_normal` | 默认 setargs_mmc（SD 参数）→ 内核 root=/dev/mmcblk0p5 挂根失败 |
| ② | `.../mq_r/sys_config.fex` | `storage_type = 1` | 改为 5(spinand) 会导致 OP-TEE 硬件校验 error1（保持 SDK 默认 1）|
| ③ | SDK `.config` | `CONFIG_PACKAGE_hmi-runtime=y` | 默认 m（模块）→ 出厂镜像不含 hmi-runtime 包，开机无 HMI |
| ④ | `.../t113/configs/default/boot_package.cfg` | 注释 `item=optee` | 本板 OP-TEE 硬件校验 error1（check_hardware_info）→ 禁用 TEE |
| ⑤ | `.../mq_r/linux-5.4/config-5.4` | `CONFIG_ARM_PSCI=n`、`CONFIG_CAN*=n`（含 CAN_SUN8I） | 无 TEE 时 PSCI SMC 无人应答 → 内核开机即崩；CAN 驱动在 TEE 侧，内核侧需关 |
| ⑥ | `target/allwinner/t113-<board>/defconfig`（**不是** `.config`） | `CONFIG_PACKAGE_procd=y` | **仅 bingpi_m2/WavePI-S6 需要**：该板镜像默认**没有 procd**（`/sbin/init → busybox`）。不打开时 `S97hmiruntime` 的 procd 分支是空转、开机**静默不启动 HMI**（2026-10-10 实机定位）。mq_r 镜像自带 procd，同一脚本在那边正常。不打开也可用（脚本有 `start-stop-daemon` 回退），但**失去 respawn** ⇒ OTA `killall hmi_runtime` 后不会自动拉起 |
| ⑦ | 同上（**defconfig**） | `CONFIG_PACKAGE_dropbear=y` | 出厂 base-files 带 `/etc/init.d/sshd`，但**包本身没装**（`/usr/sbin/sshd`、`/etc/ssh/` 都不存在）⇒ 「§4 SSH 登录」在自建镜像上不成立（22 端口拒绝连接，2026-10-10 实测）。用 **dropbear** 而不是 openssh：`CONFIG_PACKAGE_openssh-server=y` 在本 SDK 上**直接编不过**（openssl 未启用 ECC ⇒ 链接期 `undefined reference to kex_ecdh_keypair`）。⚠ dropbear 自带的 `/etc/init.d/dropbear` 在本镜像上**用不了**（它依赖 uci），故由 `rc.modules` **直起**并自建 2048 位 host key —— 见 §5。**build.sh 的 `fix_ssh_server` 每次构建自动写 defconfig（并回退 openssh 符号）** |

> 代价说明：禁用 TEE 后本板运行在非安全世界（无 secure boot/CAN 硬件加速；
> CAN 总线功能暂不可用，当前产品通信为 RS485 Modbus/MQTT，不受影响）。
> 若换用与芯片匹配的 OP-TEE 二进制，可恢复 ③④⑤ 的原始配置。

## 1. 方式一：NAND Flash 线刷（PhoenixSuit）

1. 安装 USB 驱动：`Tools/全志USB烧录驱动20201229.zip`
2. 打开 `AllwinnertechPhoeniSuit.exe`，选择镜像，选择「全盘擦除升级」
3. 将 USB 插入开发板 **USB-OTG 口**
4. **按住 FEL 键 → 按一下 RST 键松开**，出现「开始烧录固件」提示后松开 FEL
5. 烧录完成自动重启，观察 UART1 debug 串口日志（115200）

> 注意：插入 SD 启动卡时开发板**优先从 SD 卡启动**；烧录 NAND 前请拔掉 SD 卡。

## 2. 方式二：TF 卡启动（PhoenixCard）

适用于镜像较大（NAND 128MB 不足）或快速迭代调试：

1. 读卡器插入 TF 卡 → 连接 PC
2. 打开 PhoenixCard V4.2.8，选择镜像 → 「制作启动卡」
3. 完成后将 TF 卡插入开发板卡槽 → 重新上电即从 SD 启动

## 3. 开机自启动配置

开发板路径：`/etc/init.d/S97hmiruntime`（本固件自带，见
[tina/package/hmi-runtime/files/S97hmiruntime](../tina/package/hmi-runtime/files/S97hmiruntime)）。
取消自启：删除该文件或 `chmod -x`。

脚本**两种初始化系统都能起**（2026-10-10 修，实机验证）：

| rootfs 情况 | 行为 |
|---|---|
| 有真 procd（`/sbin/procd` **且** `/lib/functions/procd.sh`） | 走 procd：`procd_set_param respawn` ⇒ 崩溃 / OTA `killall hmi_runtime` 后自动拉起。**mq_r 镜像属此类** |
| 无 procd（busybox init；`/etc/rc.common` 的 `procd_*` 只是空壳） | 走回退：`start-stop-daemon -S -b` 后台拉起，pidfile `/var/run/hmi_runtime.pid`。**无 respawn** ⇒ OTA 后需 `/etc/init.d/S97hmiruntime restart`。**bingpi_m2 默认属此类**；想恢复 respawn/OTA 自愈请开 `CONFIG_PACKAGE_procd=y`（见上表 ⑥） |

> ⚠ **更正（2026-10-10 复核 rc.common 源码）**：早先的说明写过「旧版脚本因为 `command`
> 排在 `respawn` 之前、而空壳 `procd_set_param` 只在 respawn 已置位时才 `exec`，于是开机
> 什么都不启动」—— **这条推断是错的**。rc.common 的 `command` 分支里 `exec $@ >/dev/null`
> 位于 respawn 判断**之外**，即 `command` **无条件** `exec`（`/etc/init.d/adbd` 正是这个形状，
> 而 adbd 确实起来了）。准确的说法是：
>
> - bingpi_m2 镜像里**没有真 procd**（`/sbin/init → busybox`，无 `/sbin/procd` 与
>   `/lib/functions/procd.sh`），所以 `procd_*` 那一整套在它上面**没有进程托管语义**
>   （`procd_open_instance`/`close_instance` 直接 `return 0`）：不 respawn、不管 pidfile、
>   `oom_score_adj` 也不生效（`adbd` 的 `-17` 因此同样没落地）。
> - 因此脚本改为 `procd_available()` 判定 + 显式 `start-stop-daemon` 回退：**不是**因为
>   旧写法"必然不启动"，而是因为显式路径**确定**（不依赖空壳语义）、并能补上 pidfile 与
>   重复启动守卫。实机验证：新脚本下开机自启 3 次全部成立（含两次断电重上电）。

> ⚠ SDK 自带的 `S99swupdate_autorun` 会在开机时启动 LVGL widgets demo
> （`lv_examples 0 &`），抢占 `/dev/fb0` 把 HMI 界面盖掉（2026-08-17 实机定位）。
> 已双保险处理：`build.sh` 的 `fix_disable_lv_examples` 将
> `CONFIG_PACKAGE_lv_examples` 置 n（镜像不含 demo）；`S97hmiruntime` 每次开机
> 注释掉 S99swupdate_autorun 中的 lv_examples 行（S97 先于 S99 运行）。

### 系统设置入口（开机窗口）

开机后 **5 秒**（`HMI_SETTINGS_BOOT_WINDOW_MS`，`app/runtime/runtime_config.h`）
内单击屏幕任意位置进入系统设置；窗口超时未点击、或按设置页右上角「返回」退出后，
自动进入工程运行页（第 0 屏）。设该宏为 0 可禁用开机窗口。

## 4. 首次启动验证

```sh
# 串口/SSH 登录（默认 192.168.100.100，root / tina）
login: root
password: tina

# 查看运行日志（持久日志在 UDISK：/var/log 是 tmpfs，脚本把 stderr 重定向到 UDISK）
cat /mnt/UDISK/hmi_runtime.log

# 进程检查（本镜像 busybox 无 pgrep -x，用 pidof）
pidof hmi_runtime

# 手动拉起 / 停止 / 重启（自启未生效时）
/etc/init.d/S97hmiruntime start
/etc/init.d/S97hmiruntime stop
/etc/init.d/S97hmiruntime restart

# 网络（eth0 默认 IP 192.168.100.100）
ifconfig eth0
ping 192.168.100.20
```

预期日志：`LCD 1024x600 (sunxifb /dev/fb0)`、`cn font ready`、
`screens loaded: N (ui_loaded=1)`、`sys_settings: boot window started`。

## 5. 修改网络 / WIFI

- **有线静态 IP + SSH（默认已配好）**：`build.sh` 的 `fix_board_network` 做三件事（幂等）：
  1. 把 `192.168.100.100/24` 写进 base-files 的 `rc.modules`（跟在 `ifconfig eth0 up` 之后）；
  2. **开机起 SSH 服务端（dropbear）——由本包自带的 `S97hmiruntime`（rc.final 阶段）负责**；
     `rc.modules` 里**不再**写任何 SSH 行（只保留上面的 eth0 IP）。两个"不能"（都实测过）：
     ① **不能**走 `/etc/init.d/dropbear start` —— 本镜像**没有 uci**，OpenWrt 的 dropbear
     init 走 `config_load`/`config_foreach`，报 `/etc/rc.common: line 269: uci_load: not found`；
     ② **不能**在 `rc.modules` 里起 —— 那个阶段（rcS source）太早，实测那里起的
     dropbear 开机后**都不见了**；根因是 host key 从来没生成成功（见下条坑），
     而 S97 跑在 rc.final、与"成功拉起 hmi_runtime"是同一上下文，已实机验证。
     ⚠ **必须避开的坑**：镜像里 `/etc/dropbear/dropbear_rsa_host_key` 是 dropbear 包
     `touch` 出来的 **0 字节占位**，而 dropbearkey **拒绝覆盖已存在的文件**
     （`Couldn't create new file ...: File exists` / `Exited: Failed to generate key.`）
     ⇒ 就地生成**必然失败**，没 key 的 dropbear 起不来（这正是"有时能起有时不能"的原因：
     overlay 里恰好残留过一份手工生成的有效 key 时看着是好的，一 `full_erase` 刷机就暴露）。
     S97 里按 OpenWrt 原版 `dropbear.init` 的做法**先写 `/tmp/dropbear/` 再 `mv` 过去**，
     并且用 `-s 2048`（dropbearkey 2015.71 默认只给 1024 位，现代 OpenSSH 会拒）。
     关闭：把 `S97hmiruntime` 里 `HMI_SSH_AT_BOOT` 的默认值改为 0。
  3. **删掉 `/etc/profile` 里的出厂 eth0 重配块**（`mode=1`：无条件 `ifconfig eth0 down`
     + 静态 `192.168.10.6` + 默认路由 `192.168.10.1`）。

  覆盖项：`HMI_ETH0_IP` / `HMI_ETH0_MASK`（默认 `192.168.100.100` / `255.255.255.0`）、
  `HMI_ETH0_MASK` 同上。校验：

  ```sh
  ifconfig eth0            # 期望 inet 192.168.100.100
  ping -c2 192.168.100.50  # 开发机（enp8s0 的 manual 地址）
  ```

  > ⚠ **用现代 OpenSSH 连本板要显式放开老算法**：板上是 dropbear **2015.71**，
  > 只提供 `diffie-hellman-group14-sha1` + RSA/SHA-1 主机密钥，而 OpenSSH ≥ 8.8 默认
  > 已禁用它们。`HostKeyAlgorithms` 必须**精确指定** `ssh-rsa`（写 `+ssh-rsa` 会把
  > dropbear 不认识的 `rsa-sha2-*` 排在前面 ⇒ 服务端报 `No matching algo hostkey` 直接断）：
  > ```sh
  > ssh -o KexAlgorithms=+diffie-hellman-group14-sha1 \
  >     -o HostKeyAlgorithms=ssh-rsa -o RequiredRSASize=1024 \
  >     root@192.168.100.100        # 口令 tina
  > ```

  > **为何要补（2026-10-10 实机取证）**：
  > ① 出厂 base-files **只做 `ifconfig eth0 up`、不给地址** ⇒ 本指南 §4 写的
  > 「eth0 默认 192.168.100.100」在自建镜像上原本**不成立**（网线插着、L2 up，但 `.100`
  > 无应答、邻居表 INCOMPLETE）。
  > ② 更隐蔽的一条：`/etc/profile` 会被**任何登录 shell（含 `adb shell`）** source，而那段
  > 出厂网络块**无条件**把 eth0 `down`/`up` 并改成 `192.168.10.6` —— 于是在 `rc.modules`
  > 之后把 `192.168.100.100` **覆盖掉**（开发机的 `192.168.100.0/24` 与 `192.168.10.0/24`
  > 互不可达 ⇒ 看着就是"网络不通"），而且**每次登录都踩断自己的 SSH 会话**。
  > 补上之后多了一条**与 USB/adb 无关**的通道 —— adbd 在本镜像上没有 procd 监管、
  > 一掉线必须重启（见 §7 专条），网络通道不受此影响。
- WIFI（STA）：修改 `/etc/wpa_supplicant.conf` 的 ssid/psk 后执行：

```sh
wpa_supplicant -i wlan0 -Dnl80211 -c /etc/wpa_supplicant.conf -B
udhcpc -i wlan0 &
```

> WIFI 模块与 4G 模块共用同一 USB，测试 WIFI 时取下 4G 模块；仅支持 2.4G 路由器。

## 6. 快捷部署（不重新烧录）

```sh
# 开发板联网后：
./scripts/deploy.sh 192.168.100.100
```

## 7. 常见问题

| 现象 | 处理 |
| --- | --- |
| 烧录后无显示 | 检查 LCD 排线（断电插拔）；确认 `uboot-board.dts`/`board.dts` 屏参 |
| SSH 拒绝登录 | 串口执行 `rm /etc/ssh/ssh_host_*` 后重启 |
| 触摸无反应 | 确认 `/dev/input/event2` 存在；电阻屏需改节点与分辨率（见 tina/dts/README.md） |
| 镜像打包失败（分区不足） | 调整 `sys_partition.fex` 分区大小后重新打包 |
| Modbus 无响应 | 检查 `runtime_config.h` 从站号/地址/波特率；RS485 A/B 线序 |
| **adb 开机几分钟后掉线** | 见下方专条：板子没死、gadget 还在，是 `adbd` 退出后**无人拉起** —— 镜像现已带看护自动拉回 |

### adb 掉线（2026-10-10 实机取证）

**现象**：`adb devices` 变空；但 `lsusb` 里 `18d1:d002`（Tina ADB gadget）**仍在**、
`/proc/uptime` **连续**（板子没重启）、`dmesg` 无 OOM / 无 `killed process` / 无 panic、
内存宽裕（实测 `MemAvailable` 84MB、`hmi_runtime` `VmRSS` 仅 9MB）。

**机制**：本镜像**没有 procd**，`adbd` 由 `rcS` 的 `rc_load_script()` 读
`/etc/init.d/load_script.conf` 起**一次**（`/etc/init.d/adbd boot &` → `rc.common`
空壳最终 `exec /bin/adbd -D`）。**没有任何监管**（procd 缺失 ⇒ 不 respawn），
所以 `adbd` 一旦退出就再也不会回来，直到重启。附带影响：`adbd` init 里的
`procd_set_param oom_score_adj -17` 同样**不生效**。

**触发因素**：与 `wavepi-s6-flash-guide.md` §四 记的 USB 供电/链路余量问题同类
（本板在 hub 后面、FEL 阶段就出现过传输超时）。掉线都发生在 adb **大流量**操作
（拉 2.4MB framebuffer、拉 dmesg）前后。

**已随镜像修复（2026-10-10）**：`hmi-runtime` 包新增
[`hmi_adbd_watchdog.sh`](../tina/package/hmi-runtime/files/hmi_adbd_watchdog.sh)，
由 `S97hmiruntime` 开机拉起：每 30s `pidof adbd`，不在就用**与 `rcS` 完全相同**的
`/etc/init.d/adbd boot` 拉回，并写一行到 `/mnt/UDISK/adbd_watchdog.log`。
（为何不直接开 procd：见上表 ⑥ —— 那会顺带改变 `adbd`/`hmi_runtime` 的整个托管
语义；看护循环只有一个每 30s 醒一次的 busybox ash，代价可忽略。）

实机验证：`killall adbd` → 25s 后 `pidof adbd` 仍为空、`adb devices` 也空
（**证实无监管**，采样器同时记下 `!!! adbd GONE`）；启动看护后 40s 内 adbd 回来
（日志 `adbd gone -> /etc/init.d/adbd boot` / `after restart: adbd=[1080]`），
且新实例重新认领了 `/dev/usb-ffs/adb/ep0..2`。

> ⚠ 设备侧自动恢复，**主机侧可能仍需重新枚举**：`adb kill-server`，必要时对
> gadget 做一次 USB 复位（或拔插数据线），`adb devices` 才会重新看到设备
> （实测复位后立即恢复）。所以"adb 掉了"不再等于"必须重启板子"。

**触发因素的缓解**（机制之外，仍是好习惯）：独立 5V 供电 + 数据线直连主机后置
USB 口（不经 hub）；掉线都发生在 adb **大流量**操作（拉 2.4MB framebuffer、拉
dmesg）前后。

**复现取证**用 [`firmware/t113/scripts/diag_watch.sh`](../scripts/diag_watch.sh)
（adb 一掉就只能走网络或串口，故两种都给出）：

```sh
# 经 adb（掉线前）
adb push firmware/t113/scripts/diag_watch.sh /tmp/
adb shell "chmod +x /tmp/diag_watch.sh; \
           start-stop-daemon -S -b -x /bin/sh -- /tmp/diag_watch.sh"   # -b 脱离 adb 会话
# 或经网络（推荐，adb 掉线后仍可用）
ssh <客户端选项> root@192.168.100.100 "cat > /tmp/diag_watch.sh" < firmware/t113/scripts/diag_watch.sh
ssh <客户端选项> root@192.168.100.100 "chmod +x /tmp/diag_watch.sh; \
           start-stop-daemon -S -b -x /bin/sh -- /tmp/diag_watch.sh"
# 取回（UDISK 持久，重启不丢）：
adb pull /mnt/UDISK/diag/    # 或 ssh ... "cat /mnt/UDISK/diag/watch.log"
# watch.log = 时间线（!!! adbd GONE / +++ adbd back）；dmesg.at-adbd-gone = 掉线那刻内核日志
```
