# ============================================================
# host_test — PC 端系统设置布局测试
#
# 编译仓库 LVGL 9.5 源码 + 复用 app/settings/*.c，用 SDL2 窗口
# (800x480) 可视化验证布局。系统命令由 host_sys_cmd.c 模拟。
#
# 用法：
#   make             编译出 ./hmi_settings_test
#   ./hmi_settings_test            交互运行（鼠标=触摸，数字键1~9切换tab）
#   HMI_AUTO=1 ./hmi_settings_test 自动遍历9个tab截图 /tmp/hmi_tabN.bmp
#   make clean
# ============================================================

APP_ROOT := ../app
LVGL_DIR := lvgl
OBJDIR   := objs

CFLAGS += -DLV_CONF_INCLUDE_SIMPLE -std=c99 -D_GNU_SOURCE -Wno-unused-parameter
# 中文字体绝对路径由构建系统注入：$(CURDIR) 随构建机器自动正确，
# 源码里不再写死开发机家目录（否则换机器/CI 必然加载失败）。
CFLAGS += -DCN_FONT_PATH='"$(CURDIR)/fangsong.ttf"'
CFLAGS += -I. -I$(APP_ROOT) -I$(APP_ROOT)/runtime -I$(APP_ROOT)/comm \
          -I$(APP_ROOT)/drivers -I$(APP_ROOT)/settings -I../generated
CFLAGS += $(shell pkg-config --cflags sdl2 freetype2)
LDLIBS += $(shell pkg-config --libs sdl2 freetype2) -lm -lpthread

HOST_SRCS := host_main.c host_sys_cmd.c host_stubs.c
# 复用板子设置模块（排除板子版 sys_cmd.c，改用 host_sys_cmd.c 模拟）
SET_SRCS  := $(filter-out $(APP_ROOT)/settings/sys_cmd.c,$(wildcard $(APP_ROOT)/settings/*.c))

include $(LVGL_DIR)/lvgl.mk    # 提供 CSRCS / ASRCS（LVGL_PATH=./lvgl）

# 三种源分别输出到 objs/{host,settings,lvgl}/ 保留子路径，避免同名冲突
OBJS := $(patsubst %.c,$(OBJDIR)/host/%.o,$(HOST_SRCS)) \
        $(patsubst %.c,$(OBJDIR)/settings/%.o,$(notdir $(SET_SRCS))) \
        $(patsubst $(LVGL_DIR)/%.c,$(OBJDIR)/lvgl/%.o,$(CSRCS)) \
        $(patsubst $(LVGL_DIR)/%.S,$(OBJDIR)/lvgl/%.o,$(ASRCS))

TARGET := hmi_settings_test

all: $(TARGET)

$(TARGET): $(OBJS)
	$(CC) $(CFLAGS) -o $@ $(OBJS) $(LDLIBS)

$(OBJDIR)/host/%.o: %.c
	@mkdir -p $(dir $@)
	$(CC) $(CFLAGS) -c -o $@ $<

$(OBJDIR)/settings/%.o: $(APP_ROOT)/settings/%.c $(APP_ROOT)/settings/settings_ui.h $(APP_ROOT)/settings/sys_settings.h $(APP_ROOT)/settings/sys_cmd.h
	@mkdir -p $(dir $@)
	$(CC) $(CFLAGS) -c -o $@ $<

$(OBJDIR)/lvgl/%.o: $(LVGL_DIR)/%.c
	@mkdir -p $(dir $@)
	$(CC) $(CFLAGS) -c -o $@ $<

$(OBJDIR)/lvgl/%.o: $(LVGL_DIR)/%.S
	@mkdir -p $(dir $@)
	$(CC) $(CFLAGS) -c -o $@ $<

clean:
	rm -rf $(TARGET) $(OBJDIR)

.PHONY: all clean
