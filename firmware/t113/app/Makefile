# ============================================================
# zhishu-HMI T113-S3 运行固件 — 应用 Makefile
#
# 产物：
#   hmi_runtime   — 宿主程序（-rdynamic 导出 TAG_TABLE/hmi_screen_switch_idx 等符号，
#                   供 libhmi_ui.so dlopen 时解析）
#   libhmi_ui.so  — HMI UI 动态库（屏幕/主题/绑定导出 hmi_ui_get_api），
#                   OTA 升级通过 TF/USB/网络替换此 .so 后 killall hmi_runtime 生效
#
# 模式 1：Tina SDK 源码构建（推荐）
#   由 tina/package/hmi-runtime 的 Build/Prepare 将 lvgl/ 复制到本目录同级，构建时：
#     make LVGL_SRC=1 LVGL_DIR=<含 lvgl/ 的目录> \
#          CC=arm-openwrt-linux-gcc CFLAGS="..." LDFLAGS="..."
#
# 模式 2：独立交叉编译（链接 SDK 预编译 lvgl 库）
#     export PATH=$TINA_SDK/.../toolchain/bin:$PATH
#     make CC=arm-openwrt-linux-gcc LVGL_INC="-I..." LVGL_LIBS="-llvgl ..."
# ============================================================

TARGET      := hmi_runtime
UI_SO       := libhmi_ui.so
CROSS       ?= arm-openwrt-linux-
CC          ?= $(CROSS)gcc

LVGL_DIR    ?= .
LVGL_DIR_NAME ?= lvgl
# 注意：Tina Build/Compile 以命令行传入 CFLAGS/LDFLAGS，须用 override 追加
override CFLAGS += -I$(LVGL_DIR)/ -I$(LVGL_DIR)/lvgl
override CFLAGS += -I. -Iruntime -Icomm -Idrivers -Isettings
override CFLAGS += -std=c99 -D_GNU_SOURCE -Wno-unused-parameter
override CFLAGS += -DCONF_G2D_VERSION_NEW   # T113 内核 5.4：sunxifb G2D 使用新版 g2d ioctl（原 sunxifb.mk 注入）
override LDFLAGS += -lpthread -lm -ldl

MAINSRC     := ./main.c
APP_SRCS    := $(wildcard runtime/*.c) $(wildcard comm/*.c) $(wildcard drivers/*.c) $(wildcard settings/*.c)
GEN_HOST_SRCS := tags.c   # 宿主端：TAG_TABLE / TAG_BINDINGS 定义（.so 侧 extern 引用）
# UI .so 编译源：屏幕代码 + 生成的 tags 头（extern 引用宿主 TAG_TABLE）
UI_SRCS     := screens.c

ifeq ($(LVGL_SRC),1)
# ── SDK 源码模式：编译 lvgl v9 + 应用（v9 无独立 lv_drivers，sunxifb 在 drivers/） ──
include $(LVGL_DIR)/lvgl/lvgl.mk
override LDFLAGS += -lfreetype -lpng -lz
# 宿主 hmi_runtime 的 OBJS：main + app + host-tags + LVGL 源码
HOST_OBJS   := $(MAINSRC:.c=.o) $(APP_SRCS:.c=.o) $(GEN_HOST_SRCS:.c=.o) $(CSRCS:.c=.o) $(ASRCS:.S=.o)
# UI .so 的 OBJS：屏幕代码（不含 LVGL 源码，链接宿主 -rdynamic 导出的符号）
UI_OBJS     := $(UI_SRCS:.c=.o)
else
# ── 独立模式：链接预编译库 ──
CFLAGS      += $(LVGL_INC)
LDFLAGS     += $(LVGL_LIBS)
HOST_OBJS   := $(MAINSRC:.c=.o) $(APP_SRCS:.c=.o) $(GEN_HOST_SRCS:.c=.o)
UI_OBJS     := $(UI_SRCS:.c=.o)
endif

# ── hmi_runtime：导出全局符号给 .so ──
# -rdynamic 是 OTA 热加载核心：导出 TAG_TABLE/TAG_BINDINGS/lv_obj_create/
# hmi_screen_switch_idx 等全局符号，dlopen(RTLD_NOW|RTLD_GLOBAL) 时 .so 可解析。
LDFLAGS_HOST = $(LDFLAGS) -rdynamic

# ── libhmi_ui.so：SHARED + UNDEFINED 符号宿主解析 ──
# -shared -fPIC：生成动态库
# -nostartfiles：不链接 CRT（无需 main）
# 不链接 LVGL 库：所有 LVGL/hmi 符号从宿主 hmi_runtime 通过 -rdynamic + RTLD_GLOBAL 解析
CFLAGS_SO   = $(CFLAGS) -fPIC
LDFLAGS_SO  = -shared -nostartfiles

# ── hmi-tls-proxy：本机 TLS 终止代理（阶段 302）──
# 契约 §1 的拓扑：客户端（hmi_runtime）连**回环明文**，代理对 broker 走 TLS。
# 与 hmi_runtime 分成两个进程 ⇒ 产品代码 0 行改动、且代理崩了不拖垮 HMI。
# 链接镜像里**已有**的 OpenSSL（目标 defconfig: libopenssl=y）⇒ 零镜像改动。
PROXY_TARGET := hmi-tls-proxy
PROXY_SRCS   := ./proxy/tls_proxy.c
# 注：comm/tls_proxy_config.c 与 comm/mqtt_tls_contract.c 已在 APP_SRCS 的通配里，
# 这里复用同名的 .o（make 按目标去重），不重复编译。
PROXY_OBJS   := $(PROXY_SRCS:.c=.o) ./comm/tls_proxy_config.o ./comm/mqtt_tls_contract.o
PROXY_LDLIBS := -lssl -lcrypto

.PHONY: all clean

all: $(TARGET) $(UI_SO) $(PROXY_TARGET)

$(TARGET): $(HOST_OBJS)
	$(CC) $(CFLAGS) -o $@ $(HOST_OBJS) $(LDFLAGS_HOST)

$(UI_SO): $(UI_OBJS)
	$(CC) $(CFLAGS_SO) -o $@ $(UI_OBJS) $(LDFLAGS_SO)

$(PROXY_TARGET): $(PROXY_OBJS)
	$(CC) $(CFLAGS) -o $@ $(PROXY_OBJS) $(LDFLAGS) $(PROXY_LDLIBS)

# 宿主 .o：默认规则（不加 -fPIC，保持 -rdynamic 的主程序效率）
%.o: %.c
	$(CC) $(CFLAGS) -c -o $@ $<

# UI .so 的 .o：需要 -fPIC
$(UI_OBJS): CFLAGS := $(CFLAGS_SO)
$(UI_OBJS): %.o: %.c
	$(CC) $(CFLAGS_SO) -c -o $@ $<

clean:
	rm -f $(TARGET) $(UI_SO) $(PROXY_TARGET) $(HOST_OBJS) $(UI_OBJS) $(PROXY_OBJS)
