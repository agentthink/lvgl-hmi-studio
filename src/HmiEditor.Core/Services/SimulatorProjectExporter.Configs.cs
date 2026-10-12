using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;

namespace HmiEditor.Core.Services;

/// <summary>SimulatorProjectExporter 分部 —— 配置类产物（sim_config.h / CMakeLists.txt / lv_conf.h）。</summary>
public static partial class SimulatorProjectExporter
{
    /// <summary>生成 sim_config.h —— 模拟器参数配置（分辨率/帧率/全屏/缩放/自检帧数）。</summary>
    private static string BuildSimConfig(HmiScreen bootScreen)
    {
        return $$"""
                  /* ============================================================
                   * 模拟器配置 —— 修改后重新编译生效; 命令行参数优先级更高
                   * ============================================================ */
                  #ifndef SIM_CONFIG_H
                  #define SIM_CONFIG_H

                  /* 窗口逻辑分辨率（HMI 启动屏幕尺寸） */
                  #define SIM_WINDOW_WIDTH   {{(int)bootScreen.Width}}
                  #define SIM_WINDOW_HEIGHT  {{(int)bootScreen.Height}}

                  /* 窗口标题 */
                  #define SIM_WINDOW_TITLE   "HMI Simulator"

                  /* 目标帧率 (1-240) */
                  #define SIM_TARGET_FPS     60

                  /* 窗口放大倍数 (1-4), 命令行 --scale <n> 可覆盖 */
                  #define SIM_SCALE          1

                  /* --window 指定设备分辨率时的屏幕适配模式:
                   * 0=Fill 拉伸铺满(独立 x/y 缩放，默认)  1=Fit 等比缩放居中(留边) */
                  #define SIM_FIT_MODE       0

                  /* 自检模式渲染帧数 (--selftest 使用) */
                  #define SIM_SELFTEST_FRAMES 300

                  #endif /* SIM_CONFIG_H */
                  """;
    }

    private static string BuildCmakeLists(bool hasImageAssets, bool hasFontAssets, string lvglVersion)
    {
        // 统一 LVGL 版本：分支名跟随 project.LvglVersion，避免 README/CMake 硬编码 v9.2
        // 与 generated_hmi.c 头部的 "LVGL: x.y" 不一致导致 API 不兼容。
        var lvglBranch = $"release/v{lvglVersion}";
        var sourceFiles = new List<string> { "main.c", "generated_hmi.c" };
        if (hasImageAssets) sourceFiles.Add("generated_images.c");
        if (hasFontAssets) sourceFiles.Add("generated_fonts.c");
        
        var sourceList = string.Join("\n                   ", sourceFiles);

        return $$"""
               # ============================================================
               # HMI PC 模拟器 —— 跨平台构建脚本 (由 HMI Editor 自动生成)
               # 支持: Windows (MSVC/MinGW) / Linux (GCC/Clang) / macOS (Clang)
               # 架构: x86 / x64 / ARM / ARM64
               # 用法: cmake -B build -DLVGL_DIR=<lvgl源码路径> && cmake --build build
               # 测试: ctest --test-dir build --output-on-failure
               # ============================================================
               cmake_minimum_required(VERSION 3.16)
               project(hmi_simulator C)

               set(CMAKE_C_STANDARD 11)
               set(CMAKE_C_STANDARD_REQUIRED ON)

               # ── 平台与架构检测 ──
               if(WIN32)
                   message(STATUS "[sim] 平台: Windows")
               elseif(APPLE)
                   message(STATUS "[sim] 平台: macOS")
               else()
                   message(STATUS "[sim] 平台: Linux/Unix")
               endif()

               if(CMAKE_SYSTEM_PROCESSOR MATCHES "aarch64|arm64|ARM64")
                   set(SIM_ARCH "ARM64")
               elseif(CMAKE_SYSTEM_PROCESSOR MATCHES "arm|ARM")
                   set(SIM_ARCH "ARM")
               elseif(CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64|amd64|AMD64")
                   set(SIM_ARCH "x86_64")
               else()
                   set(SIM_ARCH "${CMAKE_SYSTEM_PROCESSOR}")
               endif()
               message(STATUS "[sim] CPU 架构: ${SIM_ARCH} (${CMAKE_SYSTEM_PROCESSOR})")

               # ── 构建类型默认 Release（优化性能, 最小资源消耗） ──
               if(NOT CMAKE_BUILD_TYPE)
                   set(CMAKE_BUILD_TYPE Release CACHE STRING "Build type" FORCE)
               endif()
               if(CMAKE_BUILD_TYPE STREQUAL "Release")
                   add_compile_options(-O2)
               endif()

               # ── SDL2 依赖 ──
               # Windows (vcpkg):   cmake -B build -DCMAKE_TOOLCHAIN_FILE=<vcpkg>/scripts/buildsystems/vcpkg.cmake
               # Windows (MinGW):   pacman -S mingw-w64-x86_64-SDL2
               # Linux:            sudo apt install libsdl2-dev
               # macOS:            brew install sdl2
               if(MSVC)
                   find_package(SDL2 CONFIG REQUIRED)
               else()
                   find_package(PkgConfig REQUIRED)
                   pkg_check_modules(SDL2 REQUIRED IMPORTED_TARGET sdl2)
               endif()

               # ── FreeType 依赖（用于运行时加载 TTF/OTF/TTC 中文字体） ──
               # Windows (vcpkg):   freetype
               # Linux:            sudo apt install libfreetype6-dev
               # macOS:            brew install freetype
               if(MSVC)
                   find_package(Freetype CONFIG)
               else()
                   pkg_check_modules(FREETYPE IMPORTED_TARGET freetype2)
               endif()
               if(FREETYPE_FOUND OR Freetype_FOUND)
                   message(STATUS "FreeType 已找到，启用 LV_USE_FREETYPE 运行时字体加载")
                   set(HAVE_FREETYPE ON)
               else()
                   message(WARNING "FreeType 未找到，中文字体将使用内置子集 SimSun_16_CJK（部分罕见字缺失）")
                   set(HAVE_FREETYPE OFF)
               endif()

               # ── LVGL 源码目录: -DLVGL_DIR=<path> 指定, 默认上级目录 ../lvgl ──
               # 获取: git clone --depth 1 --branch {{lvglBranch}} https://github.com/lvgl/lvgl.git
               set(LVGL_DIR "${LVGL_DIR}" CACHE PATH "LVGL source directory")
               if(NOT EXISTS "${LVGL_DIR}/lvgl.h" OR NOT EXISTS "${LVGL_DIR}/CMakeLists.txt")
                   message(FATAL_ERROR "LVGL source not found at ${LVGL_DIR}. "
                       "Clone it: git clone --depth 1 --branch {{lvglBranch}} https://github.com/lvgl/lvgl.git lvgl")
               endif()

               # lv_conf.h / sim_config.h 位于本目录, 通过 LV_CONF_INCLUDE_SIMPLE 自动加载。
               # 使用全局 include_directories / add_compile_definitions,
               # 使 LVGL 子目录的所有目标 (lvgl / lvgl_examples 等) 都能找到 lv_conf.h。
               include_directories(${CMAKE_CURRENT_SOURCE_DIR})
               add_compile_definitions(LV_CONF_INCLUDE_SIMPLE)

               # FreeType 头文件必须全局可见，因为 lvgl 子目录会编译 lv_freetype.c / lv_draw_vg_lite_label.c 等
               if(HAVE_FREETYPE)
                   if(MSVC AND Freetype_FOUND)
                       include_directories(${FREETYPE_INCLUDE_DIRS})
                   elseif(FREETYPE_FOUND)
                       include_directories(${FREETYPE_INCLUDE_DIRS})
                   endif()
               endif()

               # 仅编译 LVGL 核心库, 跳过 demos/examples, 避免 demo 代码编译进静态库
               # 或旧目标缓存被复用, 导致模拟器显示错误 UI
               # 注意：LVGL 9.0~9.2 使用 LV_CONF_BUILD_DISABLE_*（旧名），9.3+ 改用 CONFIG_LV_BUILD_*（Kconfig 风格），
               # 两个开关都设置以兼容任意版本；同时用 option() 覆盖默认 ON 值（旧名方案对 cache 变量不生效时兜底）。
               set(LV_CONF_BUILD_DISABLE_DEMOS  ON CACHE BOOL "" FORCE)
               set(LV_CONF_BUILD_DISABLE_EXAMPLES ON CACHE BOOL "" FORCE)
               option(CONFIG_LV_BUILD_DEMOS "Build demos" OFF)
               option(CONFIG_LV_BUILD_EXAMPLES "Build examples" OFF)

               add_subdirectory(${LVGL_DIR} lvgl)

               add_executable(hmi_sim
                   {{sourceList}}
               )

               target_include_directories(hmi_sim PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
               target_compile_definitions(hmi_sim PRIVATE LV_CONF_INCLUDE_SIMPLE)
               # 资源目录绝对路径（编译期注入）：资源已复制到 <工程根>/assets，
               # 运行时按绝对路径加载，不依赖进程工作目录（BuildRunDialog 以 build/ 为工作目录，
               # 若用相对路径会导致图片/字体加载失败，进而字体结构体零初始化、渲染崩溃）。
               target_compile_definitions(hmi_sim PRIVATE SIM_ASSET_DIR="${CMAKE_SOURCE_DIR}/assets")
               if(HAVE_FREETYPE)
                   target_compile_definitions(hmi_sim PRIVATE HAVE_FREETYPE=1)
               endif()

               if(MSVC)
                   target_link_libraries(hmi_sim PRIVATE lvgl SDL2::SDL2)
                   if(Freetype_FOUND)
                       # LVGL 的 lv_freetype.c 等源码需要 FreeType 头文件与链接库
                       target_include_directories(lvgl PRIVATE ${FREETYPE_INCLUDE_DIRS})
                       target_link_libraries(hmi_sim PRIVATE Freetype::Freetype)
                       target_link_libraries(lvgl  PRIVATE Freetype::Freetype)
                   endif()
               else()
                   # 非 MSVC（Linux/macOS）需链接 libm（sinf 等数学函数）
                   target_link_libraries(hmi_sim PRIVATE lvgl PkgConfig::SDL2 m)
                   if(FREETYPE_FOUND)
                       # LVGL 的 lv_freetype.c 等源码需要 FreeType 头文件与链接库（全局可见，include_directories 已在上方注入）
                       target_include_directories(lvgl PRIVATE ${FREETYPE_INCLUDE_DIRS})
                       target_link_libraries(hmi_sim PRIVATE PkgConfig::FREETYPE)
                       target_link_libraries(lvgl  PRIVATE PkgConfig::FREETYPE)
                   endif()
               endif()

               # ── 自动化测试: 自检模式 (无头环境: 设置 SDL_VIDEODRIVER=dummy) ──
               enable_testing()
               add_test(NAME simulator_selftest
                        COMMAND hmi_sim --selftest)
               set_tests_properties(simulator_selftest PROPERTIES
                   TIMEOUT 60
                   ENVIRONMENT "SDL_VIDEODRIVER=dummy;SDL_AUDIODRIVER=dummy")

               """;
    }

    private static string BuildLvConf(bool hasFontAssets)
    {
        var fontConfig = hasFontAssets ? """

               /* ── 字体 ──
                * 启用项目字体加载, 支持中文等多语言显示
                * UTF-8 编码支持: 启用中文/多语言文本解析 */
               #define LV_USE_TEXT_UTF8 1
               #define LV_USE_FONT_PLACEHOLDER 0
               /* 项目自定义字体优先，内置字体作为回退方案 */
               #define LV_FONT_MONTSERRAT_12 1
               #define LV_FONT_MONTSERRAT_14 1
               #define LV_FONT_MONTSERRAT_16 1
               #define LV_FONT_SIMSUN_14_CJK 1
               #define LV_FONT_SIMSUN_16_CJK 1
               """ : """

               /* ── 字体 ──
                * 默认使用内置字体, 如需中文支持请添加字体资源
                * UTF-8 编码支持: 启用中文/多语言文本解析 */
               #define LV_USE_TEXT_UTF8 1
               #define LV_USE_FONT_PLACEHOLDER 1
               /* 内置字体作为默认字体 */
               #define LV_FONT_MONTSERRAT_12 1
               #define LV_FONT_MONTSERRAT_14 1
               #define LV_FONT_MONTSERRAT_16 1
               /* 内置中文字体 (含常用 CJK 字符，用于模拟器 fallback) */
               #define LV_FONT_SIMSUN_14_CJK 1
               #define LV_FONT_SIMSUN_16_CJK 1
               /* 将默认字体设置为 SimSun 16 CJK，以保证中文显示 */
               #define LV_FONT_DEFAULT &lv_font_simsun_16_cjk
               """;

        return $$"""
               /* ============================================================
                * lv_conf.h —— 由 HMI Editor 生成 (LVGL v9 模拟器配置)
                * 性能相关: 内存池 / 刷新周期 / 渲染后端 / 监视器
                * ============================================================ */
               #ifndef LV_CONF_H
               #define LV_CONF_H

               /* 颜色深度: 与生成代码的 ARGB8888 canvas 匹配 */
               #define LV_COLOR_DEPTH 32

               /* 日志 */
               #define LV_USE_LOG 1
               #define LV_LOG_LEVEL LV_LOG_LEVEL_WARN

               /* 断言(模拟器下开启便于发现 API 误用) */
               #define LV_USE_ASSERT_NULL 1
               #define LV_USE_ASSERT_MALLOC 1

               /* ── 内存池 ──
                * LV_MEM_SIZE 单位: 字节。BMP 图片运行时 full decode（STRETCH/缩放依赖）+ 截图快照
                * 会一次性分配整张大图缓冲（如 1024x600 ARGB8888 ≈ 2.4MB），默认 256KB 会导致
                * lv_draw_buf_create 返回 NULL、图片加载失败或快照 OOM。建议 ≥ 8MB。 */
               #define LV_MEM_SIZE (8 * 1024 * 1024)

               /* ── 刷新周期(毫秒) ──
                * LV_DISP_DEF_REFR_PERIOD: 默认显示刷新间隔
                * LV_DEF_REFR_PERIOD:      默认任务刷新间隔 */
               #define LV_DISP_DEF_REFR_PERIOD 30
               #define LV_DEF_REFR_PERIOD      30

               /* ── 渲染后端 ──
                * LV_USE_DRAW_SW:   软件渲染(兼容所有 GPU, 默认)
                * 硬件加速后端(如 LV_USE_DRAW_ARM2D/SDL2_RENDERER)按需开启, 需额外依赖 */
               #define LV_USE_DRAW_SW 1

               /* ── 性能/内存监视器 ──
                * 设为 1 后按 LV_DISP_DEF_REFR_PERIOD 在左上角叠加 FPS / 内存占用。
                * 供性能调优使用, 发布前建议关闭。 */
               #define LV_USE_PERF_MONITOR 0
               #define LV_USE_MEM_MONITOR  0

               /* ── SDL 模拟器后端 (LVGL v9.2+ 内置) ── */
               #define LV_USE_SDL 1
               #define LV_SDL_INCLUDE_PATH <SDL2/SDL.h>
               #define LV_SDL_RENDER_MODE LV_DISPLAY_RENDER_MODE_PARTIAL

               /* ── 图片解码器(用于加载 PNG/JPG/BMP 等格式) ── */
               #define LV_USE_LODEPNG 1
               #define LV_USE_LIBPNG 0
               #define LV_USE_TJPGD 1
               #define LV_USE_BMP 1
               #define LV_USE_GIF 0
               #define LV_USE_QRCODE 0

               /* ── FreeType TTF/OTF 字体渲染器：加载完整 CJK/Unicode 字体文件（优先级最高） */
               #define LV_USE_FREETYPE 1
               #define LV_FREETYPE_CACHE_SIZE (16 * 1024 * 1024)   /* 字形缓存（大字库需要 8-16 MB） */
               #define LV_FREETYPE_MAX_FACES 8

               /* ── 文件系统驱动(用于从磁盘加载资源文件) ── */
               #define LV_USE_FS_STDIO 1
               #define LV_FS_STDIO_LETTER 'A'
               #define LV_FS_STDIO_PATH "/"
               #define LV_USE_FS_MEMFS 0

               /* ── 常用组件(生成代码可能使用) ── */
               #define LV_USE_LABEL 1
               #define LV_USE_BUTTON 1
               #define LV_USE_SLIDER 1
               #define LV_USE_SWITCH 1
               #define LV_USE_CHECKBOX 1
               #define LV_USE_IMAGE 1
               #define LV_USE_BAR 1
               #define LV_USE_DROPDOWN 1
               #define LV_USE_TEXTAREA 1
               #define LV_USE_ARC 1
               #define LV_USE_LED 1
               #define LV_USE_SPINNER 1
               #define LV_USE_KEYBOARD 1
               #define LV_USE_TABVIEW 1
               #define LV_USE_LINE 1
               #define LV_USE_TABLE 1
               #define LV_USE_CHART 1
               #define LV_USE_CANVAS 1

               {{fontConfig}}

               #endif /* LV_CONF_H */
               """;
    }
}
