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

/// <summary>SimulatorProjectExporter 分部 —— main.c 生成（SDL/LVGL 主循环、标签表、登录门、引擎轮询）。</summary>
public static partial class SimulatorProjectExporter
{
    private static string BuildMainC(HmiProject project, IReadOnlyList<string> screenFunctions, HmiScreen bootScreen, CResourceSymbols symbols, IReadOnlyList<RuntimeTag>? tags = null)
    {
        var sb = new StringBuilder();
        static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        sb.AppendLine("/* ============================================================");
        sb.AppendLine(" * LVGL PC 模拟器入口 —— 由 HMI Editor 自动生成, 请勿手改");
        sb.AppendLine(" * 支持平台: Windows / Linux / macOS");
        sb.AppendLine(" * 输入设备: 鼠标(含滚轮/触摸) / 键盘");
        sb.AppendLine(" * 运行参数: --scale <n> --fps <n> --login <用户名> --selftest --help");
        sb.AppendLine(" * ============================================================ */");
        sb.AppendLine("#include <stdio.h>");
        sb.AppendLine("#include <stdlib.h>");
        sb.AppendLine("#include <string.h>");
        sb.AppendLine("#include <math.h>");
        sb.AppendLine("#include <time.h>    /* 会话超时的单调时钟 clock_gettime —— 见 sim_now_ms */");
        sb.AppendLine("#include \"lvgl.h\"");
        sb.AppendLine("#if defined(LV_USE_SDL) && LV_USE_SDL");
        // LVGL 9.x 标准仓库把 SDL 驱动放在 src/drivers/sdl/，且 lvgl 的 CMake 目标
        // 通过 PUBLIC 暴露源码根目录（env_support/cmake/os_desktop.cmake），
        // 因此这里必须用「相对 LVGL 源码根」的路径；旧写法 include/lvgl/drivers/sdl/
        // 只适用于特定 lv_port 目录布局，在标准检出下会 fatal error。
        sb.AppendLine("#include \"src/drivers/sdl/lv_sdl_window.h\"");
        sb.AppendLine("#include \"src/drivers/sdl/lv_sdl_mouse.h\"");
        sb.AppendLine("#include \"src/drivers/sdl/lv_sdl_keyboard.h\"");
        sb.AppendLine("#endif");
        sb.AppendLine("#include \"sim_config.h\"");
        // 权限模型：与固件的 __has_include 不同，这里**无条件 include** —— 它和 main.c 是同一次导出的
        // 产物（导出器必然写出它，产物清单契约也钉着），不存在"缺这份头"的情况。
        // 得到：HMI_PERM_* 权限位、HMI_SEC_ENFORCE、hmi_perm_allows() 判定策略、HMI_SEC_DEFAULT_ROLE_MASK。
        sb.AppendLine("/* 权限模型（生成物）：权限位 + 判定策略 hmi_perm_allows + 默认角色掩码 */");
        sb.AppendLine("#include \"generated_security.h\"");
        var hasAlarmEngine = project.AlarmGroups.Any(g => g.Alarms.Any(a => a.IsEnabled)) || project.VariableManager.Variables.Any(v => v.Alarms is { Count: > 0 } && v.Alarms.Any(a => a.IsEnabled));
        var hasTrendEngine = project.TrendConfigs.Any(c => c.IsEnabled);
        if (hasAlarmEngine) sb.AppendLine("void alarm_engine_evaluate(const float* values, int count);");
        if (hasTrendEngine) sb.AppendLine("void trend_engine_tick(const float* values, uint8_t count);");
        sb.AppendLine();
        sb.AppendLine("#ifndef HMI_ENGINE_MAX_TAGS");
        sb.AppendLine("#define HMI_ENGINE_MAX_TAGS 256");
        sb.AppendLine("#endif");
        sb.AppendLine();

        // 字体加载声明（如果有字体资源）：用 LV_USE_FREETYPE 从磁盘 TTF/OTF/TTC 动态加载
        if (symbols.Fonts.Count > 0)
        {
            // 外部声明：字体变量（由 generated_fonts.c 作为全局符号定义）
            foreach (var font in symbols.Fonts)
            {
                sb.AppendLine(CResourceDeclarations.FontExtern(font.Fragment));
            }
            sb.AppendLine();
            // 外部声明：字体初始化函数（由 generated_fonts.c 提供）
            sb.AppendLine("#if LV_USE_FREETYPE");
            sb.AppendLine("/* 通过 FreeType 动态加载项目字体（支持完整 CJK 字形） */");
            sb.AppendLine("void init_font_assets(void);");
            sb.AppendLine("#endif");
            sb.AppendLine();
            sb.AppendLine("static lv_font_t* g_loaded_font = NULL;");
            sb.AppendLine();
            sb.AppendLine("/* 加载项目字体（支持中文显示） */");
            sb.AppendLine("static void load_project_font(void)");
            sb.AppendLine("{");
            sb.AppendLine("#if LV_USE_FREETYPE");
            sb.AppendLine("    /* 调用 generated_fonts.c: init_font_assets() 通过 FreeType 从文件加载字体,");
            sb.AppendLine("     * 然后取第一个字体资产作为全局默认备选。 */");
            sb.AppendLine("    init_font_assets();");
            sb.AppendLine($"    g_loaded_font = &{CResourceDeclarations.FontVariable(symbols.Fonts[0].Fragment)};");
            sb.AppendLine("    printf(\"[sim] 已加载项目字体 asset: %s\\n\", g_loaded_font ? \"OK\" : \"FAIL\");");
            sb.AppendLine("#else");
            sb.AppendLine("    /* 无 FreeType 支持，字体退化为占位。请在 lv_conf.h 中启用 LV_USE_FREETYPE。 */");
            sb.AppendLine("#endif");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        sb.AppendLine("/* 生成代码中的屏幕工厂函数（非 static，跨编译单元可见） */");
        foreach (var fn in screenFunctions)
            sb.AppendLine($"extern lv_obj_t* {fn}(void);");
        sb.AppendLine();
        sb.AppendLine("/* 生成代码中的主题样式初始化（由 generated_hmi.c 定义，见 GenerateThemeCode）。");
        sb.AppendLine(" * 在 lv_screen_load 之后调用，将背景色/文本色/CJK 字体应用到活动屏幕。 */");
        sb.AppendLine("extern void apply_theme_styles(void);");
        sb.AppendLine();

        // 模拟绑定刷新：无真实设备层时，由 generated_hmi.c 的 sim_refresh_bindings(tick) 用正弦波
        // 模拟绑定变量值并刷新显示控件（该函数能访问 obj_xxx static 变量与 TAG 表），
        // 使模拟器呈现与设计器预览模式一致的动态效果（标签数值跳动、进度条/仪表联动）。
        if (tags != null && tags.Count > 0)
        {
            sb.AppendLine("/* 绑定模拟刷新（由 generated_hmi.c 实现，能访问控件与 TAG 表） */");
            sb.AppendLine("extern void sim_refresh_bindings(uint32_t tick);");
            sb.AppendLine();
        }
        var orderedScreenNames = screenFunctions
            .Select(fn => fn.StartsWith("create_screen_", StringComparison.Ordinal) ? fn.Substring("create_screen_".Length) : fn)
            .Select(id => project.Screens.FirstOrDefault(s => s.Id == id)?.Name ?? id)
            .ToList();
        sb.AppendLine("static lv_obj_t* (*const screen_factories[])(void) =");
        sb.AppendLine("{");
        foreach (var fn in screenFunctions)
            sb.AppendLine($"    {fn},");
        sb.AppendLine("};");
        sb.AppendLine("#define SCREEN_COUNT ((int)(sizeof(screen_factories) / sizeof(screen_factories[0])))");
        sb.AppendLine();
        sb.AppendLine("static const char* const screen_names[] =");
        sb.AppendLine("{");
        foreach (var n in orderedScreenNames)
            sb.AppendLine($"    {Q(n)},");
        sb.AppendLine("};");
        sb.AppendLine("/* screens[] 为文件级数组：main 创建后 hmi_screen_switch 按名称切换 */");
        sb.AppendLine("static lv_obj_t* screens[SCREEN_COUNT];");
        sb.AppendLine();
        sb.AppendLine("/* ── PC 模拟器宿主运行时（对应固件 app/runtime/ 那几个文件）────────────────");
        sb.AppendLine(" * 生成代码只**声明**这些符号（extern），实现必须由宿主提供。导出工程因此必须是自洽的：");
        sb.AppendLine(" * 少一个定义，cmake 到链接期就是 undefined reference —— 而那一步单元测试覆盖不到。");
        sb.AppendLine(" * 守卫：SimulatorLinkContractTests 穷举「生成代码的 extern 函数 − 工程内已定义」。 */");
        sb.AppendLine();
        sb.AppendLine("int hmi_screen_switch(const char* name)");
        sb.AppendLine("{");
        sb.AppendLine("    for (int i = 0; i < SCREEN_COUNT; i++) {");
        sb.AppendLine("        if (strcmp(screen_names[i], name) == 0) {");
        sb.AppendLine("            lv_screen_load(screens[i]);");
        sb.AppendLine("            return 0;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("    return -1;");
        sb.AppendLine("}");
        sb.AppendLine();
        // 标签存储：真机由 variable_table.c + 通讯层提供，模拟器用一块 RAM 表顶上。
        // 长度与 generated_hmi.c 的 TAG_COUNT 同源（都由导出器手里的 tags 列表算出），守卫比对两者。
        var tagCount = Math.Max(1, tags?.Count ?? 0);
        sb.AppendLine("/* 设备层变量读写（真机 = variable_table.c + 通讯层；模拟器 = 一块 RAM）");
        sb.AppendLine($" * SIM_TAG_COUNT 必须等于 generated_hmi.c 的 TAG_COUNT —— 由 SimulatorLinkContractTests 钉住 */");
        sb.AppendLine($"#define SIM_TAG_COUNT {tagCount}");
        sb.AppendLine("static float s_tag_raw[SIM_TAG_COUNT];");
        sb.AppendLine();
        sb.AppendLine("float var_raw_read(int index)");
        sb.AppendLine("{");
        sb.AppendLine("    if (index < 0 || index >= SIM_TAG_COUNT) return 0.0f;");
        sb.AppendLine("    return s_tag_raw[index];");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("void var_raw_write(int index, float raw)");
        sb.AppendLine("{");
        sb.AppendLine("    if (index < 0 || index >= SIM_TAG_COUNT) return;");
        sb.AppendLine("    s_tag_raw[index] = raw;");
        sb.AppendLine("}");
        sb.AppendLine();
        // 会话超时的锁屏目标：**生成期**解析成屏名（与设备端同一份解析结果 ——
        // 设备端从 generated_security.h 读 HMI_SEC_LOCK_SCREEN_NAME，这里直接内联同一个名字）。
        var simLockScreen = LockScreenTargets.Resolve(project.SecurityConfig, project.Screens);

        sb.AppendLine("/* 会话状态 —— 与固件 hmi_actions.c 同名同义（未登录 = 只能看画面） */");
        sb.AppendLine("static int s_sim_logged_in = 0;");
        sb.AppendLine("static unsigned int s_sim_perm_mask = 0u;");
        sb.AppendLine();
        sb.AppendLine("/* ── 会话超时（与固件 hmi_actions.c 逐条同构）──");
        sb.AppendLine(" * 三条语义不能改：① 「有人在问权限」本身就算活动（两边都没有输入事件钩子）；");
        sb.AppendLine(" * ② 判据**严格大于**；③ 超时 ⇒ 清会话 + 跳锁屏 + **拒绝本次判定**。");
        sb.AppendLine(" *");
        sb.AppendLine(" * 是否启用读的是**生成的** HMI_SEC_SESSION_ENABLED —— 刻意不在这里按");
        sb.AppendLine(" * ENFORCE/AUTO_LOGOUT/TIMEOUT_MIN 再推一遍：两处各推一遍必然漂移，而漂移的表现是");
        sb.AppendLine(" * 「预览会跳锁屏、设备不会」，归因成本极高。");
        sb.AppendLine(" *");
        sb.AppendLine(" * 时间源用 clock_gettime(CLOCK_MONOTONIC)，**刻意不用 lv_tick_get**：");
        sb.AppendLine(" * --login 建立会话发生在 lv_init() 之前，那时 LVGL 的 tick 源还没注册。 */");
        sb.AppendLine("static uint32_t s_sim_session_deadline_ms = 0u;   /* 0 = 无会话（常规路径不碰时钟） */");
        sb.AppendLine();
        sb.AppendLine("static uint32_t sim_now_ms(void)");
        sb.AppendLine("{");
        sb.AppendLine("#if defined(CLOCK_MONOTONIC)");
        sb.AppendLine("    struct timespec ts;");
        sb.AppendLine("    if (clock_gettime(CLOCK_MONOTONIC, &ts) == 0)");
        sb.AppendLine("        return (uint32_t)((uint64_t)ts.tv_sec * 1000u + (uint64_t)ts.tv_nsec / 1000000u);");
        sb.AppendLine("#endif");
        sb.AppendLine("    return (uint32_t)((uint64_t)time(NULL) * 1000u);   /* 回退：墙钟（秒精度，够用） */");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("static void sim_session_touch(void)");
        sb.AppendLine("{");
        sb.AppendLine("#if HMI_SEC_SESSION_ENABLED");
        sb.AppendLine("    if (s_sim_logged_in)");
        sb.AppendLine("        s_sim_session_deadline_ms = sim_now_ms() + (uint32_t)HMI_SEC_SESSION_TIMEOUT_MIN * 60000u;");
        sb.AppendLine("#endif");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/* 评估超时。返回 1 = 判定超时（会话已清、锁屏屏已切）。 */");
        sb.AppendLine("static int sim_session_expire(void)");
        sb.AppendLine("{");
        sb.AppendLine("#if HMI_SEC_SESSION_ENABLED");
        sb.AppendLine("    if (!s_sim_logged_in) { s_sim_session_deadline_ms = 0u; return 0; }");
        sb.AppendLine("    if (s_sim_session_deadline_ms == 0u) return 0;");
        sb.AppendLine("    if ((int32_t)(sim_now_ms() - s_sim_session_deadline_ms) <= 0) return 0;");
        sb.AppendLine();
        sb.AppendLine("    s_sim_logged_in = 0;");
        sb.AppendLine("    s_sim_perm_mask = 0u;");
        sb.AppendLine("    s_sim_session_deadline_ms = 0u;");
        sb.AppendLine("    fprintf(stderr, \"[sim] 会话超时（%d 分钟无操作）：已登出\\n\", HMI_SEC_SESSION_TIMEOUT_MIN);");
        if (simLockScreen.ScreenName != null)
        {
            sb.AppendLine($"    hmi_screen_switch({Q(simLockScreen.ScreenName)});   /* 生成期解析出的锁屏屏 */");
        }
        else
        {
            sb.AppendLine("    /* 工程内没有可用的锁屏/登录屏 —— 与设备端一致：会话已清除，只是不切屏 */");
            sb.AppendLine("    fprintf(stderr, \"[sim] 没有可用的锁屏屏（LockScreenId 未配置且无登录屏）\\n\");");
        }
        sb.AppendLine("    return 1;");
        sb.AppendLine("#else");
        sb.AppendLine("    return 0;");
        sb.AppendLine("#endif");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/* 主循环每轮调用 —— 让「没人动」能在屏幕上自己体现。");
        sb.AppendLine(" * 刻意**不**刷新活动时间：周期函数若能刷新，超时就永远不会触发。");
        sb.AppendLine(" * 审计落盘是**设备端**行为（写 /mnt/UDISK/hmi/audit.log）；预览时看 stderr 即可。 */");
        sb.AppendLine("int hmi_session_poll(void)");
        sb.AppendLine("{");
        sb.AppendLine("    return sim_session_expire();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/* 权限门。判定策略 hmi_perm_allows 来自生成的 generated_security.h ——");
        sb.AppendLine(" * 与固件 hmi_has_permission 调的是同一个函数，两边不可能分叉。 */");
        sb.AppendLine("int hmi_require_permission(unsigned int bits)");
        sb.AppendLine("{");
        sb.AppendLine("#if HMI_SEC_ENFORCE");
        sb.AppendLine("    /* 惰性超时评估 —— 必须在判定**之前**：超时的那一刻会话已被清掉，");
        sb.AppendLine("     * 下面的判定自然按「未登录」折算（拒绝）。顺序反了等于把这次操作送出去。 */");
        sb.AppendLine("    (void)sim_session_expire();");
        sb.AppendLine("    /* 「有人在问权限」本身就算活动 —— 与固件 hmi_has_permission 同一处打点，");
        sb.AppendLine("     * 也与编辑器 AuthenticationService.CheckPermission 的 LastActivityAt 同义。 */");
        sb.AppendLine("    sim_session_touch();");
        sb.AppendLine("    if (!hmi_perm_allows(s_sim_logged_in, s_sim_perm_mask, bits)) {");
        sb.AppendLine("        fprintf(stderr, \"[sim] permission denied: need 0x%03X (logged_in=%d, mask=0x%03X)\\n\",");
        sb.AppendLine("                bits, s_sim_logged_in, s_sim_perm_mask);");
        sb.AppendLine("        return 0;");
        sb.AppendLine("    }");
        sb.AppendLine("#else");
        sb.AppendLine("    (void)bits;   /* 设计模式：全放开（与设备一致） */");
        sb.AppendLine("#endif");
        sb.AppendLine("    return 1;");
        sb.AppendLine("}");
        sb.AppendLine();

        // 三个系统动作：门与固件同源，目标屏在**生成期**按候选表解析（模拟器知道工程的全部屏名，
        // 不必像设备端那样运行时模糊匹配）。候选表本身是单一来源 ScreenNavTargets。
        void EmitNav(string funcName, string? screenName, IReadOnlyList<string> candidates, string what)
        {
            if (screenName != null)
            {
                sb.AppendLine($"    hmi_screen_switch({Q(screenName)});   /* 生成期按候选表命中：{ScreenNavTargets.Describe(candidates)} */");
            }
            else
            {
                sb.AppendLine($"    /* 工程内没有{what}（候选：{ScreenNavTargets.Describe(candidates)}）—— 与固件一致：记日志、不切屏 */");
                sb.AppendLine($"    fprintf(stderr, \"[sim] {funcName}: 工程内没有{what}\\n\");");
            }
        }

        sb.AppendLine("void hmi_show_alarms(void)");
        sb.AppendLine("{");
        sb.AppendLine("#if HMI_SEC_ENFORCE");
        sb.AppendLine("    if (!hmi_require_permission(HMI_PERM_VIEW_ALARM_HISTORY)) return;");
        sb.AppendLine("#endif");
        EmitNav("hmi_show_alarms", ScreenNavTargets.Resolve(project, ScreenNavTargets.Alarm),
            ScreenNavTargets.Alarm, "报警屏");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("void hmi_show_trends(void)");
        sb.AppendLine("{");
        sb.AppendLine("#if HMI_SEC_ENFORCE");
        sb.AppendLine("    if (!hmi_require_permission(HMI_PERM_VIEW_SCREEN)) return;");
        sb.AppendLine("#endif");
        EmitNav("hmi_show_trends", ScreenNavTargets.Resolve(project, ScreenNavTargets.Trends),
            ScreenNavTargets.Trends, "趋势屏");
        sb.AppendLine("}");
        sb.AppendLine();
        // ── 登录：预览身份 + 凭据提交入口 ──
        // 模拟器**不做**凭据校验 —— SHA-256/Base64 只在设备端编译（app/runtime/hmi_crypto.c）。
        // 这不是"省事"，而是**拒绝让校验逻辑长出第二份实现**：固件的 hmi_login_submit 含查表顺序、
        // 长度短路、失败锁定等一串判据，在这里重写一遍必然与设备漂移，而漂移的表现是
        // "预览能登、设备登不上" —— 归因成本极高（见 SimulatorLoginContractTests）。
        // 替代做法：把「用户 → 权限掩码」在**生成期**算好内联进来，用 --login <用户名> 选预览身份。
        // 零运行时重复，且"点一下登录按钮就以默认角色进入会话"这条最危险的形态被彻底去掉。
        var previewUsers = new List<(string User, string Mask)>();
        foreach (var u in project.SecurityConfig?.RuntimeUsers ?? new List<RuntimeUser>())
        {
            if (!u.IsEnabled) continue;                         // 停用账户与设备端一致：登不进
            if (string.IsNullOrEmpty(u.Username)) continue;     // 空用户名没有可用的预览标识（设备端空输入反而会命中它，见 RuntimeUserValidator.EmptyUsername）
            var role = SecurityConfigCodeGen.BuiltInRoles.FirstOrDefault(r => r.Id == u.RoleId);
            if (role is null) continue;                         // 角色不在表内 ⇒ 设备端 hmi_role_lookup 也返回 -1
            previewUsers.Add((u.Username, SecurityConfigCodeGen.MaskHex(role)));
        }

        sb.AppendLine("/* ── 登录：预览身份（生成期算好的「用户 → 权限掩码」）──");
        sb.AppendLine(" * 模拟器**不做**凭据校验 —— 口令哈希只在设备端编译（hmi_crypto.c），");
        sb.AppendLine(" * 以免校验逻辑长出第二份实现后与设备漂移。要预览「某身份登录后」的效果，");
        sb.AppendLine(" * 用命令行 --login <用户名>；不带该参数就是未登录状态（与设备一致：只能看画面）。");
        sb.AppendLine(" * 与设备的**唯一刻意差异**：模拟器不做失败锁定（那是安全加固，不是预览对象）。 */");
        sb.AppendLine("static const char* s_sim_preview_user = NULL;");
        sb.AppendLine($"#define SIM_PREVIEW_LOGIN_COUNT {previewUsers.Count}");
        if (previewUsers.Count > 0)
        {
            sb.AppendLine("static const struct { const char* username; unsigned int mask; } s_sim_preview_logins[SIM_PREVIEW_LOGIN_COUNT] = {");
            for (var i = 0; i < previewUsers.Count; i++)
                sb.AppendLine($"    {{ {Q(previewUsers[i].User)}, {previewUsers[i].Mask} }}" + (i == previewUsers.Count - 1 ? "" : ","));
            sb.AppendLine("};");
        }
        sb.AppendLine();
        sb.AppendLine("/* --login <用户名>：以某个已配置身份进入会话（只改会话状态，不切屏）。 */");
        sb.AppendLine("static void sim_preview_login(void)");
        sb.AppendLine("{");
        sb.AppendLine("    if (s_sim_preview_user == NULL) return;   /* 没传 --login：保持未登录 */");
        sb.AppendLine("#if SIM_PREVIEW_LOGIN_COUNT > 0");
        sb.AppendLine("    int i;");
        sb.AppendLine("    for (i = 0; i < SIM_PREVIEW_LOGIN_COUNT; i++) {");
        sb.AppendLine("        if (strcmp(s_sim_preview_logins[i].username, s_sim_preview_user) == 0) {");
        sb.AppendLine("            s_sim_logged_in = 1;");
        sb.AppendLine("            s_sim_perm_mask = s_sim_preview_logins[i].mask;");
        sb.AppendLine("            sim_session_touch();   /* 登录即活动：新会话从「现在」起算（与固件/编辑器同义） */");
        sb.AppendLine("            fprintf(stderr, \"[sim] --login %s: 以该身份进入会话（掩码 0x%03X）；模拟器不校验口令，设备端仍需输入\\n\",");
        sb.AppendLine("                    s_sim_preview_user, s_sim_perm_mask);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("    fprintf(stderr, \"[sim] --login %s: 用户表里没有这个用户（或它已停用）—— 会话不建立\\n\", s_sim_preview_user);");
        sb.AppendLine("#else");
        sb.AppendLine("    fprintf(stderr, \"[sim] --login %s: 本工程未配置运行时用户表（HMI_SEC_USER_COUNT == 0）\\n\", s_sim_preview_user);");
        sb.AppendLine("#endif");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/* 凭据提交入口 —— 生成的登录按钮回调会调它。");
        sb.AppendLine(" * 模拟器一律拒绝并说清原因：不静默放行，也不假装校验过。 */");
        sb.AppendLine("int hmi_login_submit(const char* user, const char* password)");
        sb.AppendLine("{");
        sb.AppendLine("    (void)password;");
        sb.AppendLine("    fprintf(stderr, \"[sim] hmi_login_submit(%s): 模拟器不做凭据校验 —— 用 --login <用户名> 预览登录后的效果\\n\",");
        sb.AppendLine("            user ? user : \"(null)\");");
        sb.AppendLine("    return -1;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("void hmi_login(void)");
        sb.AppendLine("{");
        sb.AppendLine("    /* 语义与固件一致：**有运行时用户表时只跳屏、不授权** —— 否则点一下登录按钮就已经");
        sb.AppendLine("     * 以默认角色进入会话，凭据校验被完全绕过（这正是本功能落地前的状态）。");
        sb.AppendLine("     * 表为空时才退回「按默认角色授权」，掩码由生成器算好（HMI_SEC_DEFAULT_ROLE_MASK）。 */");
        sb.AppendLine("#if HMI_SEC_USER_COUNT == 0");
        sb.AppendLine("    s_sim_logged_in = 1;");
        sb.AppendLine("#if HMI_SEC_ENFORCE");
        sb.AppendLine("    s_sim_perm_mask = HMI_SEC_DEFAULT_ROLE_MASK;");
        sb.AppendLine("#else");
        sb.AppendLine("    s_sim_perm_mask = 0u;   /* 设计模式：掩码无意义 */");
        sb.AppendLine("#endif");
        sb.AppendLine("    sim_session_touch();   /* 按默认角色授权的会话同样从「现在」起算（与固件 hmi_login_as_role 同义） */");
        sb.AppendLine("#else");
        sb.AppendLine("    fprintf(stderr, \"[sim] hmi_login: 工程配了运行时用户表 —— 无凭据提交就不建立会话，用 --login <用户名> 预览\\n\");");
        sb.AppendLine("#endif");
        EmitNav("hmi_login", ScreenNavTargets.Resolve(project, ScreenNavTargets.Login),
            ScreenNavTargets.Login, "登录屏");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("void hmi_logout(void)");
        sb.AppendLine("{");
        sb.AppendLine("    s_sim_logged_in = 0;");
        sb.AppendLine("    s_sim_perm_mask = 0u;");
        sb.AppendLine("    s_sim_session_deadline_ms = 0u;   /* 清会话计时（与固件 hmi_logout 同义） */");
        sb.AppendLine("    /* 回默认屏（索引 0），与固件 hmi_screen_switch_idx(0) 同义 */");
        sb.AppendLine("    hmi_screen_switch(screen_names[0]);");
        sb.AppendLine("    fprintf(stderr, \"[sim] hmi_logout: session cleared, returned to default screen\\n\");");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("/* ── 模拟器软键盘服务（点击 TextArea 弹出 LVGL 内置键盘，项目无关）──");
        sb.AppendLine(" * 与固件宿主软键盘（soft_keyboard.c）同思路：接管屏幕内全部 TextArea 的");
        sb.AppendLine(" * 焦点事件，聚焦时在 lv_layer_top() 弹出内置 lv_keyboard（英文/数字/符号），");
        sb.AppendLine(" * 失焦或点击键盘 OK 键收起。数字键盘由生成代码 user_data 标记（Number 输入框）。 */");
        sb.AppendLine("static lv_obj_t* s_sim_kb = NULL;");
        sb.AppendLine("static void sim_kb_close(void);");
        sb.AppendLine("static void sim_kb_ready_cb(lv_event_t* e)");
        sb.AppendLine("{");
        sb.AppendLine("    (void)e;");
        sb.AppendLine("    sim_kb_close();   /* 点击键盘 OK 键收起 */");
        sb.AppendLine("}");
        sb.AppendLine("static void sim_kb_close(void)");
        sb.AppendLine("{");
        sb.AppendLine("    if (s_sim_kb) lv_obj_add_flag(s_sim_kb, LV_OBJ_FLAG_HIDDEN);");
        sb.AppendLine("}");
        sb.AppendLine("static void sim_kb_open(lv_obj_t* ta)");
        sb.AppendLine("{");
        sb.AppendLine("    if (!s_sim_kb) {");
        sb.AppendLine("        s_sim_kb = lv_keyboard_create(lv_layer_top());");
        sb.AppendLine("        /* 深色工业风（与设计器/固件软键盘基色一致） */");
        sb.AppendLine("        lv_obj_set_style_bg_color(s_sim_kb, lv_color_hex(0x14191f), 0);");
        sb.AppendLine("        lv_obj_set_style_bg_opa(s_sim_kb, LV_OPA_COVER, 0);");
        sb.AppendLine("        lv_obj_set_style_radius(s_sim_kb, 0, 0);");
        sb.AppendLine("        lv_obj_set_style_border_width(s_sim_kb, 0, 0);");
        sb.AppendLine("        lv_obj_set_style_pad_all(s_sim_kb, 6, 0);");
        sb.AppendLine("        lv_obj_set_style_bg_color(s_sim_kb, lv_color_hex(0x27303c), LV_PART_ITEMS);");
        sb.AppendLine("        lv_obj_set_style_text_color(s_sim_kb, lv_color_hex(0xe8edf2), LV_PART_ITEMS);");
        sb.AppendLine("        lv_obj_set_style_radius(s_sim_kb, 4, LV_PART_ITEMS);");
        sb.AppendLine("        lv_obj_set_size(s_sim_kb, LV_PCT(100), LV_PCT(45));");
        sb.AppendLine("        lv_obj_align(s_sim_kb, LV_ALIGN_BOTTOM_MID, 0, 0);");
        sb.AppendLine("        lv_obj_add_event_cb(s_sim_kb, sim_kb_ready_cb, LV_EVENT_READY, NULL);");
        sb.AppendLine("    }");
        sb.AppendLine("    lv_keyboard_set_textarea(s_sim_kb, ta);");
        sb.AppendLine("    lv_keyboard_set_mode(s_sim_kb, lv_obj_get_user_data(ta) != NULL");
        sb.AppendLine("        ? LV_KEYBOARD_MODE_NUMBER : LV_KEYBOARD_MODE_TEXT_LOWER);");
        sb.AppendLine("    lv_obj_remove_flag(s_sim_kb, LV_OBJ_FLAG_HIDDEN);");
        sb.AppendLine("    lv_obj_move_to_index(s_sim_kb, -1);   /* 置顶 */");
        sb.AppendLine("}");
        sb.AppendLine("static void sim_kb_ta_focus_cb(lv_event_t* e)");
        sb.AppendLine("{");
        sb.AppendLine("    sim_kb_open(lv_event_get_target(e));");
        sb.AppendLine("}");
        sb.AppendLine("static void sim_kb_ta_defocus_cb(lv_event_t* e)");
        sb.AppendLine("{");
        sb.AppendLine("    (void)e;");
        sb.AppendLine("    sim_kb_close();");
        sb.AppendLine("}");
        sb.AppendLine("static void sim_kb_instrument(lv_obj_t* obj)");
        sb.AppendLine("{");
        sb.AppendLine("    if (!obj) return;");
        sb.AppendLine("    if (lv_obj_get_class(obj) == &lv_textarea_class) {");
        sb.AppendLine("        lv_obj_add_flag(obj, LV_OBJ_FLAG_CLICK_FOCUSABLE);");
        sb.AppendLine("        lv_obj_add_event_cb(obj, sim_kb_ta_focus_cb, LV_EVENT_FOCUSED, NULL);");
        sb.AppendLine("        lv_obj_add_event_cb(obj, sim_kb_ta_defocus_cb, LV_EVENT_DEFOCUSED, NULL);");
        sb.AppendLine("        return;   /* textarea 内部无输入控件，无需深入 */");
        sb.AppendLine("    }");
        sb.AppendLine("    uint32_t n = lv_obj_get_child_count(obj);");
        sb.AppendLine("    for (uint32_t i = 0; i < n; i++)");
        sb.AppendLine("        sim_kb_instrument(lv_obj_get_child(obj, (int32_t)i));");
        sb.AppendLine("}");
        sb.AppendLine("/* 屏幕创建后调用：接管屏幕内全部 TextArea 输入（点击聚焦弹软键盘） */");
        sb.AppendLine("static void sim_kb_attach(lv_obj_t* screen)");
        sb.AppendLine("{");
        sb.AppendLine("    if (screen) sim_kb_instrument(screen);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/* ── 运行时配置（命令行参数优先于 sim_config.h） ── */");
        sb.AppendLine("typedef struct {");
        sb.AppendLine("    int  scale;");
        sb.AppendLine("    int  fps;");
        sb.AppendLine("    int  selftest;");
        sb.AppendLine("    int  window_w;   /* 0=默认 SIM_WINDOW_WIDTH  */");
        sb.AppendLine("    int  window_h;   /* 0=默认 SIM_WINDOW_HEIGHT */");
        sb.AppendLine("} runtime_cfg_t;");
        sb.AppendLine();
        sb.AppendLine("static void print_help(const char* prog)");
        sb.AppendLine("{");
        sb.AppendLine("    printf(\"用法: %s [选项]\\n\", prog);");
        sb.AppendLine("    printf(\"  --window <WxH>      设备分辨率/画布 (如 1280x800)，0=设计分辨率，自动铺满/适配\\n\");");
        sb.AppendLine("    printf(\"  --scale <n>         窗口放大倍数 (1-4，不改画布)\\n\");");
        sb.AppendLine("    printf(\"  --fps <n>           目标帧率 (1-240)\\n\");");
        sb.AppendLine("    printf(\"  --login <用户名>   以工程配置的某身份进入会话（模拟器不校验口令，仅预览用）\\n\");");
        sb.AppendLine("    printf(\"  --selftest          自检模式: 渲染 %d 帧后输出 PASS/FAIL 并退出\\n\", SIM_SELFTEST_FRAMES);");
        sb.AppendLine("    printf(\"  --help              显示本帮助\\n\");");
        sb.AppendLine("    printf(\"默认: 分辨率 %dx%d, 帧率 %d, 缩放 %d\\n\",");
        sb.AppendLine("           SIM_WINDOW_WIDTH, SIM_WINDOW_HEIGHT, SIM_TARGET_FPS, SIM_SCALE);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("static int parse_int(const char* s, int fallback)");
        sb.AppendLine("{");
        sb.AppendLine("    char* end = NULL;");
        sb.AppendLine("    long v = strtol(s, &end, 10);");
        sb.AppendLine("    return (end && *end == '\\0') ? (int)v : fallback;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("static void parse_args(int argc, char** argv, runtime_cfg_t* cfg)");
        sb.AppendLine("{");
        sb.AppendLine("    for (int i = 1; i < argc; i++) {");
        sb.AppendLine("        if (strcmp(argv[i], \"--selftest\") == 0)  cfg->selftest = 1;");
        sb.AppendLine("        else if (strcmp(argv[i], \"--help\") == 0 || strcmp(argv[i], \"-h\") == 0) { print_help(argv[0]); exit(0); }");
        sb.AppendLine("        else if (strcmp(argv[i], \"--window\") == 0 && i + 1 < argc) {");
        sb.AppendLine("            const char* s = argv[++i];");
        sb.AppendLine("            const char* x = strchr(s, 'x');");
        sb.AppendLine("            if (x) {");
        sb.AppendLine("                /* 按 'x' 切分后分别解析（parse_int 要求整串数字，含 'x' 会解析失败） */");
        sb.AppendLine("                char buf[32];");
        sb.AppendLine("                size_t len = (size_t)(x - s);");
        sb.AppendLine("                if (len >= sizeof(buf)) len = sizeof(buf) - 1;");
        sb.AppendLine("                memcpy(buf, s, len); buf[len] = '\\0';");
        sb.AppendLine("                int w = parse_int(buf, 0);");
        sb.AppendLine("                int h = parse_int(x + 1, 0);");
        sb.AppendLine("                if (w > 0 && h > 0) { cfg->window_w = w; cfg->window_h = h; }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        else if (strcmp(argv[i], \"--scale\") == 0 && i + 1 < argc) { int s = parse_int(argv[++i], 1); cfg->scale = s < 1 ? 1 : (s > 4 ? 4 : s); }");
        sb.AppendLine("        else if (strcmp(argv[i], \"--fps\") == 0 && i + 1 < argc) { int f = parse_int(argv[++i], 60); cfg->fps = f < 1 ? 1 : (f > 240 ? 240 : f); }");
        sb.AppendLine("        else if (strcmp(argv[i], \"--login\") == 0 && i + 1 < argc) s_sim_preview_user = argv[++i];");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/* ── 自检模式: 渲染 N 帧并验证显示/屏幕就绪 ── */");
        sb.AppendLine("static int run_selftest(void)");
        sb.AppendLine("{");
        sb.AppendLine("    printf(\"[selftest] render %d frames...\\n\", SIM_SELFTEST_FRAMES);");
        sb.AppendLine("    for (int i = 0; i < SIM_SELFTEST_FRAMES; i++) {");
        sb.AppendLine("        lv_timer_handler();");
        sb.AppendLine("        lv_delay_ms(2);");
        sb.AppendLine("    }");
        sb.AppendLine("    lv_display_t* disp = lv_display_get_default();");
        sb.AppendLine("    lv_obj_t* active = lv_screen_active();");
        sb.AppendLine("    if (!disp)     { printf(\"[selftest] FAIL: 无显示设备\\n\"); return 1; }");
        sb.AppendLine("    if (!active)   { printf(\"[selftest] FAIL: 无活动屏幕\\n\"); return 1; }");
        sb.AppendLine("    printf(\"[selftest] PASS (display=%p, active_screen=%p)\\n\", (void*)disp, (void*)active);");
        sb.AppendLine("    return 0;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/* ── 屏幕自适应（--window 指定设备分辨率时铺满/适配画布）──");
        sb.AppendLine(" * SIM_FIT_MODE: 0=Fill 拉伸铺满(独立 x/y 缩放，默认)；1=Fit 等比缩放居中(留边)。");
        sb.AppendLine(" * 用真实布局坐标（非 transform），保证 snapshot / 输入命中均正确。");
        sb.AppendLine(" * 屏幕根对象铺满画布（背景色随尺寸铺满），直接子对象（背景图 + 控件）按比例缩放。 */");
        sb.AppendLine("static void scale_child_pos_size(lv_obj_t* c, float sx, float sy, float ox, float oy)");
        sb.AppendLine("{");
        sb.AppendLine("    lv_coord_t x = lv_obj_get_x(c);");
        sb.AppendLine("    lv_coord_t y = lv_obj_get_y(c);");
        sb.AppendLine("    lv_coord_t w = lv_obj_get_width(c);");
        sb.AppendLine("    lv_coord_t h = lv_obj_get_height(c);");
        sb.AppendLine("    lv_obj_set_pos(c, (lv_coord_t)(x * sx + ox + 0.5f), (lv_coord_t)(y * sy + oy + 0.5f));");
        sb.AppendLine("    lv_obj_set_size(c, (lv_coord_t)(w * sx + 0.5f), (lv_coord_t)(h * sy + 0.5f));");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("static void fit_screen_to_canvas(lv_obj_t* scr)");
        sb.AppendLine("{");
        sb.AppendLine("    lv_display_t* d = lv_obj_get_display(scr);");
        sb.AppendLine("    lv_coord_t dw = lv_display_get_horizontal_resolution(d);");
        sb.AppendLine("    lv_coord_t dh = lv_display_get_vertical_resolution(d);");
        sb.AppendLine("    lv_coord_t sw = lv_obj_get_width(scr);");
        sb.AppendLine("    lv_coord_t sh = lv_obj_get_height(scr);");
        sb.AppendLine("    if (dw <= 0 || dh <= 0 || sw <= 0 || sh <= 0) return;");
        sb.AppendLine("    if (dw == sw && dh == sh) return;  /* 已匹配，无需缩放 */");
        sb.AppendLine();
        sb.AppendLine("    float sx = (float)dw / (float)sw;");
        sb.AppendLine("    float sy = (float)dh / (float)sh;");
        sb.AppendLine("    float ox = 0.0f, oy = 0.0f;");
        sb.AppendLine("#if SIM_FIT_MODE == 1");
        sb.AppendLine("    /* Fit：等比缩放，居中，四周留屏幕背景色 */");
        sb.AppendLine("    float s = sx < sy ? sx : sy;");
        sb.AppendLine("    sx = sy = s;");
        sb.AppendLine("    ox = ((float)dw - (float)sw * s) * 0.5f;");
        sb.AppendLine("    oy = ((float)dh - (float)sh * s) * 0.5f;");
        sb.AppendLine("#endif");
        sb.AppendLine();
        sb.AppendLine("    /* 屏幕根对象铺满画布（背景色随尺寸铺满，避免缩放后露边） */");
        sb.AppendLine("    lv_obj_set_size(scr, dw, dh);");
        sb.AppendLine();
        sb.AppendLine("    /* 直接子对象（背景图 + 各控件）按比例缩放 */");
        sb.AppendLine("    uint32_t n = lv_obj_get_child_count(scr);");
        sb.AppendLine("    for (uint32_t i = 0; i < n; i++) {");
        sb.AppendLine("        lv_obj_t* c = lv_obj_get_child(scr, (int32_t)i);");
        sb.AppendLine("        if (c) scale_child_pos_size(c, sx, sy, ox, oy);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("int main(int argc, char** argv)");
        sb.AppendLine("{");
        sb.AppendLine("    /* 运行时配置: 默认取 sim_config.h, 命令行可覆盖 */");
        sb.AppendLine("    runtime_cfg_t cfg = { SIM_SCALE, SIM_TARGET_FPS, 0, 0, 0 };");
        sb.AppendLine("    parse_args(argc, argv, &cfg);");
        sb.AppendLine();
        sb.AppendLine("    /* --login <用户名>：预览身份（只改会话状态，不依赖 LVGL 是否就绪） */");
        sb.AppendLine("    sim_preview_login();");
        sb.AppendLine();
        sb.AppendLine("    lv_init();");
        sb.AppendLine();

        // 加载项目字体（如果有字体资源）
        if (symbols.Fonts.Count > 0)
        {
            sb.AppendLine("    /* 加载项目字体 */");
            sb.AppendLine("    load_project_font();");
            sb.AppendLine("    if (g_loaded_font) {");
            sb.AppendLine("        /* 设置项目字体为全局默认 */");
            sb.AppendLine("        lv_font_t* font_to_use = g_loaded_font;");
            sb.AppendLine("        /* 注: LVGL 9 中设置默认字体需要通过主题或样式机制，此处仅用于保证字体被链接 */");
            sb.AppendLine("        /* 具体控件字体请在 generated_hmi.c 中通过 lv_obj_set_style_text_font 设置 */");
            sb.AppendLine("        (void)font_to_use;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("    /* 创建 SDL 窗口。全屏/最大化请使用窗口管理器。 */");
        sb.AppendLine("    /* 画布（display 逻辑分辨率）：--window 指定则用（模拟设备分辨率），");
        sb.AppendLine("     * 否则默认 bootScreen 设计分辨率。 */");
        sb.AppendLine("    /* 窗口放大：--scale 用 lv_sdl_window_set_zoom 实现 —— 画布不变、仅 SDL 窗口/纹理放大，");
        sb.AppendLine("     * 避免把画布分辨率一起放大导致画面缩在左上角/留白（修复「拉伸模式显示不全」）。 */");
        sb.AppendLine("    int32_t canvas_w = cfg.window_w > 0 ? cfg.window_w : (int32_t)SIM_WINDOW_WIDTH;");
        sb.AppendLine("    int32_t canvas_h = cfg.window_h > 0 ? cfg.window_h : (int32_t)SIM_WINDOW_HEIGHT;");
        sb.AppendLine("    lv_display_t* disp = lv_sdl_window_create(canvas_w, canvas_h);");
        sb.AppendLine("    if (disp == NULL) { fprintf(stderr, \"SDL 窗口创建失败\\n\"); return 1; }");
        sb.AppendLine("    if (cfg.scale > 1) lv_sdl_window_set_zoom(disp, (float)cfg.scale);");
        sb.AppendLine("    lv_sdl_window_set_title(disp, SIM_WINDOW_TITLE);");
        sb.AppendLine("    /* 禁用窗口自由缩放：SDL 后端 resize 会改写画布分辨率却不重排内容，导致画面异常。");
        sb.AppendLine("     * 需要不同分辨率请用 --window（配合屏幕自适应）或 --scale（zoom 放大）。 */");
        sb.AppendLine("    lv_sdl_window_set_resizeable(disp, false);");
        sb.AppendLine();
        sb.AppendLine("    /* 注册输入设备: 鼠标(含滚轮) / 键盘; SDL 鼠标驱动同时处理触摸事件 (SDL_FINGER*) */");
        sb.AppendLine("    lv_sdl_mouse_create();");
        sb.AppendLine("    lv_sdl_keyboard_create();");
        sb.AppendLine();
        sb.AppendLine("    /* 预创建全部屏幕, 加载第一个 */");
        sb.AppendLine("    /* screens[] 为文件级数组（hmi_screen_switch 需要访问） */");
        sb.AppendLine("    for (int i = 0; i < SCREEN_COUNT; i++)");
        sb.AppendLine("        screens[i] = screen_factories[i]();");
        sb.AppendLine("    /* 接管全部 TextArea 输入：点击输入框弹出软键盘（模拟器软键盘服务） */");
        sb.AppendLine("    for (int i = 0; i < SCREEN_COUNT; i++)");
        sb.AppendLine("        sim_kb_attach(screens[i]);");
        sb.AppendLine("    lv_screen_load(screens[0]);");
        sb.AppendLine("    printf(\"[sim] 已加载启动屏幕 screens[0]=%p (共 %d 个屏幕)\\n\", (void*)screens[0], SCREEN_COUNT);");
        sb.AppendLine("    fflush(stdout);");
        sb.AppendLine();
        sb.AppendLine("    /* 屏幕自适应：--window 指定非设计分辨率时，把各屏幕铺满/适配到画布（SIM_FIT_MODE）。 */");
        sb.AppendLine("    for (int i = 0; i < SCREEN_COUNT; i++)");
        sb.AppendLine("        fit_screen_to_canvas(screens[i]);");
        sb.AppendLine();
        sb.AppendLine("    /* 应用主题样式：必须在 lv_screen_load 之后调用，使 apply_theme_styles 内部的");
        sb.AppendLine("     * lv_screen_active() 能拿到已加载的启动屏幕，从而把背景色/文本色/CJK 字体");
        sb.AppendLine("     * 正确应用到屏幕上（否则中文显示为 tofu、文本颜色缺失）。 */");
        sb.AppendLine("    apply_theme_styles();");
        sb.AppendLine();
        sb.AppendLine("    /* 自检模式: 渲染后立即退出, 供自动化测试 (ctest) 使用 */");
        sb.AppendLine("    if (cfg.selftest) return run_selftest();");
        sb.AppendLine();
        sb.AppendLine("    printf(\"[sim] 运行中: 画布 %dx%d @ %d fps (缩放x%d)。关闭窗口退出。\\n\",");
        sb.AppendLine("           canvas_w, canvas_h, cfg.fps, cfg.scale);");
        sb.AppendLine();
        sb.AppendLine("    /* 主循环: 目标帧率节流 */");
        sb.AppendLine("    uint32_t frame_delay = 1000 / (uint32_t)cfg.fps;");
        sb.AppendLine("    uint32_t sim_tick = 0;");
        sb.AppendLine("    static float s_sim_engine_snap[HMI_ENGINE_MAX_TAGS];");
        sb.AppendLine("    while (1) {");
        sb.AppendLine("        lv_timer_handler();");
        if (tags != null && tags.Count > 0)
        {
            sb.AppendLine("        sim_refresh_bindings(sim_tick);   /* 模拟绑定变量并刷新显示控件 */");
        }
        // 报警/趋势引擎轮询（与固件 main.c hmi_engine_poll 语义一致，使用正弦波模拟变量快照）
        bool hasAlarm = project.AlarmGroups.Any(g => g.Alarms.Any(a => a.IsEnabled))
            || project.VariableManager.Variables.Any(v => v.Alarms is { Count: > 0 } && v.Alarms.Any(a => a.IsEnabled));
        bool hasTrend = project.TrendConfigs.Any(c => c.IsEnabled);
        if (hasAlarm || hasTrend)
        {
            int n = Math.Min(tags?.Count ?? 0, 256);
            sb.AppendLine($"        int s_engine_n = {n};");
            sb.AppendLine("        for (int i = 0; i < s_engine_n; i++)");
            sb.AppendLine("            s_sim_engine_snap[i] = 50.0f + 45.0f * sinf(sim_tick * 0.05f + (float)i * 0.7f);");
            if (hasAlarm)
                sb.AppendLine("        alarm_engine_evaluate(s_sim_engine_snap, s_engine_n);");
            if (hasTrend)
                sb.AppendLine("        trend_engine_tick(s_sim_engine_snap, (uint8_t)s_engine_n);");
        }
        sb.AppendLine("        hmi_session_poll();   /* 会话超时评估（无会话 / 未启用自动登出时为空操作） */");
        sb.AppendLine("        sim_tick++;");
        sb.AppendLine("        lv_tick_inc(5);");
        sb.AppendLine("        lv_delay_ms(frame_delay);");
        sb.AppendLine("    }");
        sb.AppendLine("    return 0;");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
