using System.Text.RegularExpressions;
using HmiEditor.Core.Mapping;
using HmiEditor.Core.Models;
using HmiEditor.Core.Security;

namespace HmiEditor.Core.Services;

/// <summary>验证结果严重级别。</summary>
public enum FindingSeverity { Error, Warning, Info }

/// <summary>验证结果类别。</summary>
public enum FindingCategory
{
    Structure,    // 屏幕/控件结构问题
    Binding,      // 变量绑定问题
    Expression,   // 表达式绑定问题
    Faceplate,    // 未解析的 Faceplate 参数
    DanglingRef,  // 悬空变量引用
    LvglApi,      // 未知 LVGL API 调用
    RuntimeUser,  // 运行时用户表问题（会让设备端登不上）
    CodeGen       // 生成代码质量问题
}

/// <summary>
/// 单条验证结果。
///
/// <para><b>口径（阶段 89，登记 <b>#61g</b>）</b>：<see cref="Code"/> 是**诊断码**
/// （<see cref="GeneratedCodeIssueCodes"/> / <see cref="FindingLocationCodes"/> 里的常量值）
/// —— App 层按它查字典渲染成用户语言（<c>gen.issue.&lt;码小写&gt;</c> /
/// <c>gen.loc.&lt;码小写&gt;</c>）。<see cref="Message"/> 与 <see cref="Location"/> 是
/// **中文回落原文**（字典缺键时才显示，**逐字保持码化之前的文本**，
/// 所以 Core 侧既有断言不受影响）；<see cref="Args"/> / <see cref="LocationArgs"/>
/// 是字典模板的实参。</para>
///
/// <para><b><see cref="LocationCode"/> 为空串 = 该定位与语言无关，直出 <see cref="Location"/></b>
/// —— 例如 <c>-</c>（没有定位）、屏幕 id 列表、<c>SecurityConfig.RuntimeUsers[{0}]</c>
/// 这些**模型路径**（翻了反而对不上代码）。这一档**没有表**：守它的是
/// <c>GenFindingLocalizationGuardTests.EveryLocation_RendersTheDictionaryTemplate_OrIsLanguageNeutral</c>
/// 里"空码 ⇒ 不许含汉字"那条断言（往这一档塞一条新中文文案 ⇒ 判据红）。
/// ⚠ 本行原写作「由 <c>NeutralLocation</c> 表**双向**守住」—— 那张表**从来不存在**，
/// 是本文件的一处文档漂移，阶段 93 顺手改正（真有的那张表叫 <c>ForeignLocation</c>，
/// 守的是**另一档**，且已随 #61l 闭环删除）。</para>
///
/// <para>⚠ <b>新字段刻意不给 `Code` 默认值</b>：这样编译器会把**全部 14 个站点**点出来
/// （阶段 87 那条「改返回类型＝取证手段」）。<see cref="LocationCode"/> 有默认值是因为
/// 「与语言无关」是一等公民、且它由判据的白名单把守。</para>
/// </summary>
public record GeneratedCodeFinding(
    FindingSeverity Severity,
    FindingCategory Category,
    string Code,
    string Message,
    string Location,
    string LocationCode = "",
    IReadOnlyList<object>? Args = null,
    IReadOnlyList<object>? LocationArgs = null);


/// <summary>
/// I11: 生成代码验证器 —— "设计→生成→验证"闭环的静态质量门。
///
/// 在导出/提交前对项目模型与生成代码做结构化检查:
///   结构(空项目/重复 id/重名屏幕)、Faceplate 未解析参数、
///   表达式语法、悬空变量引用、未知 LVGL API(防 CodeGen 拼写回归)、
///   生成代码完整性(WARNING 残留/缺失 create_screen)。
///
/// 纯函数、无副作用,供编辑器 UI 与 CI/测试共用。
/// </summary>
public static class GeneratedCodeValidator
{
    /// <summary>执行全部检查,返回按严重级别排序的发现列表。</summary>
    public static IReadOnlyList<GeneratedCodeFinding> Validate(
        HmiProject project, string? generatedCode)
    {
        var findings = new List<GeneratedCodeFinding>();
        var code = generatedCode ?? "";

        ValidateStructure(project, findings);
        ValidateFaceplateParams(project, findings);
        ValidateExpressions(project, findings);
        ValidateDanglingReferences(project, findings);
        ValidateRuntimeUsers(project, findings);
        ValidateGeneratedCode(code, findings);

        return findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Category)
            .ToList();
    }

    /// <summary>是否有阻断级(Error)问题 —— 用于导出前拦截。</summary>
    public static bool HasErrors(IReadOnlyList<GeneratedCodeFinding> findings)
        => findings.Any(f => f.Severity == FindingSeverity.Error);

    // ── 结构检查 ──

    private static void ValidateStructure(HmiProject project, List<GeneratedCodeFinding> findings)
    {
        if (project.Screens.Count == 0)
        {
            findings.Add(new(FindingSeverity.Error, FindingCategory.Structure,
                GeneratedCodeIssueCodes.NO_SCREENS,
                "工程没有屏幕,无法生成代码", "-"));
            return;
        }

        // 屏幕重名
        foreach (var group in project.Screens.GroupBy(s => s.Name).Where(g => g.Count() > 1))
        {
            findings.Add(new(FindingSeverity.Warning, FindingCategory.Structure,
                GeneratedCodeIssueCodes.DUPLICATE_SCREEN_NAME,
                $"屏幕名 '{group.Key}' 重复 ({group.Count()} 个)", string.Join(", ", group.Select(s => s.Id)),
                Args: new object[] { group.Key, group.Count() }));
        }

        // 同屏控件 id 冲突
        foreach (var screen in project.Screens)
        {
            foreach (var group in screen.Controls.GroupBy(c => c.Id).Where(g => g.Count() > 1))
            {
                findings.Add(new(FindingSeverity.Error, FindingCategory.Structure,
                    GeneratedCodeIssueCodes.DUPLICATE_CONTROL_ID,
                    $"控件 id '{group.Key}' 在屏幕内重复 ({group.Count()} 个),生成代码将产生同名静态变量",
                    $"屏幕 {screen.Name}", FindingLocationCodes.SCREEN,
                    Args: new object[] { group.Key, group.Count() },
                    LocationArgs: new object[] { screen.Name }));
            }
        }
    }

    // ── Faceplate 未解析参数 ──

    private static void ValidateFaceplateParams(HmiProject project, List<GeneratedCodeFinding> findings)
    {
        foreach (var screen in project.Screens)
        {
            foreach (var control in screen.Controls)
            {
                var location = $"屏幕 {screen.Name} / 控件 {control.Name}";

                // 文本残留
                if (!string.IsNullOrEmpty(control.Text) &&
                    (control.Text.Contains("{{") || control.Text.Contains("}}")))
                {
                    findings.Add(new(FindingSeverity.Error, FindingCategory.Faceplate,
                        GeneratedCodeIssueCodes.FACEPLATE_TEXT_UNRESOLVED,
                        $"控件文本包含未解析的 Faceplate 参数: {Truncate(control.Text)}", location,
                        FindingLocationCodes.SCREEN_CONTROL,
                        Args: new object[] { Truncate(control.Text) },
                        LocationArgs: new object[] { screen.Name, control.Name }));
                }

                // 绑定残留(联动模型)
                foreach (var binding in control.VariableBindings)
                {
                    if (binding.VariableId.Contains("{{") || binding.VariableId.Contains("}}"))
                    {
                        findings.Add(new(FindingSeverity.Error, FindingCategory.Faceplate,
                            GeneratedCodeIssueCodes.FACEPLATE_BINDING_UNRESOLVED,
                            $"属性 '{binding.PropertyName}' 的绑定含未解析 Faceplate 参数: {Truncate(binding.VariableId)}",
                            location, FindingLocationCodes.SCREEN_CONTROL,
                            Args: new object[] { binding.PropertyName, Truncate(binding.VariableId) },
                            LocationArgs: new object[] { screen.Name, control.Name }));
                    }
                }

                // 旧字段回退
                if (!string.IsNullOrEmpty(control.VariableBindingId) &&
                    (control.VariableBindingId.Contains("{{") || control.VariableBindingId.Contains("}}")))
                {
                    findings.Add(new(FindingSeverity.Error, FindingCategory.Faceplate,
                        GeneratedCodeIssueCodes.FACEPLATE_BINDING_LEGACY_UNRESOLVED,
                        $"绑定含未解析 Faceplate 参数: {Truncate(control.VariableBindingId)}", location,
                        FindingLocationCodes.SCREEN_CONTROL,
                        Args: new object[] { Truncate(control.VariableBindingId) },
                        LocationArgs: new object[] { screen.Name, control.Name }));
                }
            }
        }
    }

    // ── 表达式语法 ──

    private static void ValidateExpressions(HmiProject project, List<GeneratedCodeFinding> findings)
    {
        foreach (var screen in project.Screens)
        {
            foreach (var control in screen.Controls)
            {
                foreach (var binding in control.VariableBindings)
                {
                    if (binding.Kind != BindingSourceKind.Expression) continue;

                    var expr = binding.Expression ?? "";
                    if (!ExpressionEngine.TryCompile(expr, out var error))
                    {
                        // ⚠ `Args[1]` 装的是**记录体本身**，不是字符串（阶段 91 / #61i）：
                        //   原因要按用户语言渲染，而 Core 拿不到字典服务 ⇒ 由 App 的
                        //   ExternalText.Describe(GeneratedCodeFinding) 在**填参前**把它渲染掉
                        //   （与 UiOperationGuard.FailureDetail(…, Exception) 先 FailureReason 再填参同形）。
                        //   `Message` 里放的是**回落原文**（error.Message 是中文）—— 字典缺键时原样显示。
                        findings.Add(new(FindingSeverity.Error, FindingCategory.Expression,
                            GeneratedCodeIssueCodes.EXPRESSION_SYNTAX,
                            $"表达式语法错误: {Truncate(expr)} ({error.Message})",
                            $"屏幕 {screen.Name} / 控件 {control.Name} / {binding.PropertyName}",
                            FindingLocationCodes.SCREEN_CONTROL_PROPERTY,
                            Args: new object[] { Truncate(expr), error },
                            LocationArgs: new object[] { screen.Name, control.Name, binding.PropertyName }));
                    }
                }
            }
        }
    }

    // ── 悬空引用 ──

    private static void ValidateDanglingReferences(HmiProject project, List<GeneratedCodeFinding> findings)
    {
        foreach (var (loc, bindingId) in CrossReferenceAnalyzer.FindDanglingReferences(project))
        {
            // ⚠ `loc` 是**记录体**（阶段 93，闭环 <b>#61l</b>），不再是一串拼好的中文：
            //   它自带**定位码**与**实参**，由 App 的 ExternalText.DescribeLocation 按界面语言
            //   渲染（gen.loc.*）；`loc.Message` 是**字典缺键时的回落原文**
            //   （`OfBinding` / `OfLegacyBinding` / `OfAction` 逐字保留改动前的格式）。
            //   本刀之前这里拿的是 CrossReferenceAnalyzer 拼的中文串、LocationCode 留空
            //   ⇒ 英文界面下这一半中英混排。
            findings.Add(new(FindingSeverity.Warning, FindingCategory.DanglingRef,
                GeneratedCodeIssueCodes.DANGLING_REFERENCE,
                $"绑定指向不存在的变量: {bindingId}", loc.Message,
                loc.Code,
                Args: new object[] { bindingId },
                LocationArgs: loc.Args));
        }
    }

    // ── 运行时用户表 ──

    /// <summary>
    /// 运行时用户表检查 —— **复用 <see cref="RuntimeUserValidator"/> 的规则，不重写一份**。
    ///
    /// <para><b>为什么必须在导出前拦（而不是只在设置面板里飘红字）：</b>
    /// 设备端 <c>hmi_actions.c</c> 的 <c>hmi_login()</c> 在 <c>HMI_SEC_USER_COUNT &gt; 0</c> 时
    /// <b>刻意不授权</b> —— 会话只能由登录屏经 <c>hmi_login_submit</c> 建立（否则"点一下登录"
    /// 就以默认角色进入会话，凭据校验被完全绕过）。于是**表里一条可用账户都没有**时，
    /// 任何人都不可能建立会话，而所有带权限门的动作<b>永久拒绝</b> ——
    /// 产物烧到设备上表现为「HMI 点什么都没反应」，而归因成本极高
    /// （面板上的红字在导出后就被忘了）。</para>
    ///
    /// <para><b>定级依据（逐条对应设备端真实后果，不是"看起来严重"）：</b></para>
    /// <list type="bullet">
    /// <item><c>UnknownRole</c> / <c>EnabledWithoutPassword</c> ⇒ <b>Error</b>：
    /// 该账户口令正确也登不上（<c>hmi_role_lookup</c> 返回 -1 / 哈希解析失败即拒绝）。</item>
    /// <item><c>EmptyUsername</c> / <c>DuplicateUsername</c> ⇒ <b>Warning</b>：
    /// 不阻断导出（前者仍可被空输入命中、后者首条仍可用），但属明确配置错误。</item>
    /// </list>
    ///
    /// <para><b>表为空时刻意什么都不报</b>：那是"没配运行时用户表"的既有形态，
    /// 设备端按默认角色授权，行为与凭据功能落地前一致 —— 报错会拦下所有老工程。</para>
    /// </summary>
    private static void ValidateRuntimeUsers(HmiProject project, List<GeneratedCodeFinding> findings)
    {
        var users = project.SecurityConfig?.RuntimeUsers;
        if (users is null || users.Count == 0) return;

        foreach (var issue in RuntimeUserValidator.Validate(users))
        {
            var severity = issue.Kind switch
            {
                RuntimeUserIssueKind.UnknownRole => FindingSeverity.Error,
                RuntimeUserIssueKind.EnabledWithoutPassword => FindingSeverity.Error,
                _ => FindingSeverity.Warning,
            };

            // 映射到**码 + 中文回落 + 实参**，而不是直接产中文 ——
            // RuntimeUserValidator 的类注释写着「文案由 App 层映射」，本表把那次映射
            // 的产物改成码，于是 Core 不再产文案（见 GeneratedCodeIssueCodes 的注释）。
            var (code, message, args) = issue.Kind switch
            {
                RuntimeUserIssueKind.EmptyUsername => (
                    GeneratedCodeIssueCodes.RUNTIME_USER_EMPTY_USERNAME,
                    $"运行时用户[{issue.Index}] 的用户名为空 —— 设备端以 strcmp 比对，登录屏输入空用户名会命中该条目（不是死条目，仍需口令）；请填写用户名。",
                    new object[] { issue.Index }),
                RuntimeUserIssueKind.DuplicateUsername => (
                    GeneratedCodeIssueCodes.RUNTIME_USER_DUPLICATE_USERNAME,
                    $"运行时用户[{issue.Index}] 的用户名 '{issue.Username}' 重复 —— 设备端线性查表首个匹配胜出，排在后面的同名条目永远匹配不到（改它不会有任何反应）。",
                    new object[] { issue.Index, issue.Username }),
                RuntimeUserIssueKind.UnknownRole => (
                    GeneratedCodeIssueCodes.RUNTIME_USER_UNKNOWN_ROLE,
                    $"运行时用户[{issue.Index}] '{issue.Username}' 的角色不在预置角色表内 —— 设备端 hmi_role_lookup 查不到，口令正确也登不上。",
                    new object[] { issue.Index, issue.Username }),
                RuntimeUserIssueKind.EnabledWithoutPassword => (
                    GeneratedCodeIssueCodes.RUNTIME_USER_ENABLED_WITHOUT_PASSWORD,
                    $"运行时用户[{issue.Index}] '{issue.Username}' 已启用但没有密码 —— 设备端口令校验解析失败即拒绝，该账户无法登录（不是免密放行）。",
                    new object[] { issue.Index, issue.Username }),
                _ => (
                    GeneratedCodeIssueCodes.RUNTIME_USER_UNKNOWN,
                    $"运行时用户[{issue.Index}] '{issue.Username}' 配置有问题。",
                    new object[] { issue.Index, issue.Username }),
            };

            findings.Add(new(severity, FindingCategory.RuntimeUser, code, message,
                $"SecurityConfig.RuntimeUsers[{issue.Index}]", Args: args));
        }

        // 整表体检：表非空但一条可用账户都没有 ⇒ 谁都进不去（见方法注释的归因）
        if (RuntimeUserValidator.CountUsable(users) == 0)
        {
            findings.Add(new(FindingSeverity.Error, FindingCategory.RuntimeUser,
                GeneratedCodeIssueCodes.RUNTIME_USER_TABLE_UNUSABLE,
                $"运行时用户表有 {users.Count} 条，但没有一条可用（需同时满足：用户名非空 + 角色在角色表内 + 已启用 + 已设密码）" +
                " ⇒ 设备端任何账户都无法登录，且所有权限门将永久拒绝（表现为「点了没反应」）。",
                "SecurityConfig.RuntimeUsers",
                Args: new object[] { users.Count }));
        }
    }

    // ── 生成代码检查 ──

    private static void ValidateGeneratedCode(string code, List<GeneratedCodeFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            findings.Add(new(FindingSeverity.Error, FindingCategory.CodeGen,
                GeneratedCodeIssueCodes.EMPTY_GENERATED_CODE,
                "生成的代码为空", "-"));
            return;
        }

        if (!code.Contains("create_screen_", StringComparison.Ordinal))
        {
            findings.Add(new(FindingSeverity.Error, FindingCategory.CodeGen,
                GeneratedCodeIssueCodes.MISSING_CREATE_SCREEN,
                "生成代码缺失 create_screen 函数", "-"));
        }

        // CodeGen 输出的警告注释 → 提示
        foreach (var line in code.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("// WARNING:", StringComparison.Ordinal))
            {
                // ⚠ 这条的**消息体就是产物里的注释原文**（`generated_ui.c` 的 `// WARNING: …`）
                //   ⇒ `GENERATOR_WARNING_COMMENT` **刻意没有字典键**（翻了等于改写产物），
                //   走 `ExternalText.Resolve` 的回落分支。详见码表里那条注释与登记 #61k。
                findings.Add(new(FindingSeverity.Warning, FindingCategory.CodeGen,
                    GeneratedCodeIssueCodes.GENERATOR_WARNING_COMMENT,
                    Truncate(trimmed.TrimStart('/', ' ')), "生成代码",
                    FindingLocationCodes.GENERATED_CODE));
            }
        }

        // 未知 LVGL API 调用检测
        foreach (var api in ExtractLvglCalls(code)
                     .Where(a => !KnownLvglApis.Contains(a))
                     .Distinct()
                     .OrderBy(a => a))
        {
            findings.Add(new(FindingSeverity.Warning, FindingCategory.LvglApi,
                GeneratedCodeIssueCodes.UNKNOWN_LVGL_API,
                $"未知的 LVGL API 调用(可能拼写错误或版本差异): {api}", "生成代码",
                FindingLocationCodes.GENERATED_CODE, Args: new object[] { api }));
        }
    }

    // ── 辅助 ──

    private static readonly Regex LvglCallRegex = new(@"\blv_[a-z0-9_]+\s*\(", RegexOptions.Compiled);

    /// <summary>提取代码中的 lv_* 函数调用名(去重)。</summary>
    public static IReadOnlyList<string> ExtractLvglCalls(string code)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in LvglCallRegex.Matches(code))
        {
            var name = m.Value.TrimEnd(' ', '(');
            result.Add(name);
        }
        return result.ToList();
    }

    /// <summary>已知 LVGL API 白名单 = 控件创建函数 ∪ CodeGen 常用调用 ∪ SVG canvas 系列 ∪ 主题/动画/图表/模拟器系列。</summary>
    private static readonly HashSet<string> KnownLvglApis = new(StringComparer.Ordinal)
    {
        // ── 核心运行时 ──
        "lv_init", "lv_task_handler", "lv_tick_inc", "lv_tick_get",
        "lv_delay_ms", "lv_timer_handler",

        // ── 屏幕/显示 ──
        "lv_scr_load", "lv_screen_load", "lv_screen_active",
        "lv_display_get_horizontal_resolution", "lv_display_get_vertical_resolution",

        // ── 控件创建(来自 ControlMappingRegistry) ──
        "lv_obj_create", "lv_button_create", "lv_label_create", "lv_slider_create",
        "lv_switch_create", "lv_checkbox_create", "lv_image_create", "lv_bar_create",
        "lv_dropdown_create", "lv_textarea_create", "lv_arc_create", "lv_keyboard_create",
        "lv_led_create", "lv_spinner_create", "lv_tabview_create", "lv_line_create",
        "lv_table_create", "lv_chart_create", "lv_canvas_create",

        // ── 对象操作 ──
        "lv_obj_set_pos", "lv_obj_set_size", "lv_obj_set_x", "lv_obj_set_y",
        "lv_obj_get_display", "lv_obj_center", "lv_obj_remove_flag",

        // ── 样式 (v8/v9 兼容，v9 部分 API 保留) ──
        "lv_obj_set_style_bg_color", "lv_obj_set_style_radius",
        "lv_obj_set_style_border_width", "lv_obj_set_style_border_color",
        "lv_obj_set_style_text_color", "lv_obj_set_style_text_font",
        "lv_obj_set_style_pad_all", "lv_obj_set_style_opa", "lv_obj_set_style_bg_opa",
        "lv_style_init", "lv_style_set_bg_color", "lv_style_set_radius",
        "lv_style_set_border_width", "lv_style_set_border_color",
        "lv_style_set_text_color", "lv_style_set_text_font",

        // ── 文本/事件 ──
        "lv_label_set_text", "lv_obj_add_event_cb",

        // ── 表格（LVGL 9.x 只有 *_count 形式；*_cnt 为 v8 旧名） ──
        "lv_table_set_row_count", "lv_table_set_column_count", "lv_table_set_cell_value",

        // ── 图表 ──
        "lv_chart_set_type", "lv_chart_set_point_count", "lv_chart_set_div_line_count",
        "lv_chart_add_series", "lv_chart_set_range", "lv_chart_refresh",
        "lv_chart_set_next_value",

        // ── 动画 ──
        "lv_anim_init", "lv_anim_set_exec_cb", "lv_anim_set_var",
        "lv_anim_set_values", "lv_anim_set_duration", "lv_anim_set_delay",
        "lv_anim_set_path_cb", "lv_anim_set_repeat_count", "lv_anim_start",
        "lv_anim_path_linear", "lv_anim_path_ease_in_out", "lv_anim_path_ease_in",
        "lv_anim_path_ease_out", "lv_anim_path_bounce", "lv_anim_path_overshoot",
        "lv_anim_path_step",

        // ── I6 SVG canvas 系列（对照 LVGL 9.5 头文件核对） ──
        // 注意：lv_canvas_set_px_opa / lv_draw_polygon(_dsc_init) 在 LVGL 9.x 中并不存在，
        // 不得再列入白名单，否则生成非法代码时校验器会给出假阴性。
        // 透明填充用 lv_canvas_fill_bg，多边形填充用 lv_draw_triangle 三角扇近似。
        "lv_canvas_set_buffer", "lv_canvas_set_px", "lv_canvas_fill_bg", "lv_canvas_init_layer",
        "lv_canvas_finish_layer", "lv_draw_rect_dsc_init", "lv_draw_line_dsc_init",
        "lv_draw_arc_dsc_init", "lv_draw_triangle_dsc_init", "lv_draw_rect",
        "lv_draw_line", "lv_draw_arc", "lv_draw_triangle",

        // ── SVG 渐变（P1-3）：对照 package/gui/lvgl/src/misc/lv_grad.h 核实 ──
        // 只有 lv_draw_rect 这条路径支持 LINEAR/RADIAL —— 多边形走的三角扇只有 HOR/VER。
        // 声明本身不受 LV_USE_DRAW_SW_COMPLEX_GRADIENTS 保护（该开关只决定渲染分支是否编译），
        // 所以即使固件里开关为 0，下面的名字也依然存在于头文件、不算"未知 API"。
        "lv_grad_linear_init", "lv_grad_radial_init", "lv_grad_radial_set_focal",
        "lv_grad_init_stops",

        // ── 模拟器 SDL 后端 ──
        "lv_sdl_window_create", "lv_sdl_mouse_create",

        // ── 控件参数 / 运行时刷新 / 定时器 ──
        // 说明（2026-09-13）：此前白名单严重不全，约 40 个合法 API 被判为"未知"，
        // 导致该检查长期是噪音（用户会直接忽略）。以下逐条对照 LVGL 9.5 头文件核实后补齐，
        // 并由 GeneratedCodeSemanticTests 断言"全控件工程生成代码零未知 API"防止再次腐化。
        "lv_arc_set_value", "lv_arc_set_range", "lv_arc_set_bg_angles", "lv_arc_set_rotation",
        "lv_bar_set_value", "lv_bar_set_range",
        "lv_slider_set_value", "lv_slider_set_range", "lv_slider_get_value",
        "lv_led_set_brightness",
        "lv_label_set_text_fmt",
        "lv_dropdown_set_options", "lv_dropdown_set_selected", "lv_dropdown_get_list",
        "lv_textarea_set_placeholder_text",
        "lv_table_set_cell_value_fmt", "lv_table_set_column_width",
        "lv_line_set_points",
        "lv_spinner_set_anim_params",
        "lv_obj_add_flag", "lv_obj_remove_flag", "lv_obj_add_state", "lv_obj_remove_state",
        "lv_obj_align", "lv_obj_invalidate", "lv_obj_set_user_data", "lv_obj_get_style_text_font",
        "lv_obj_set_style_text_align", "lv_obj_set_style_border_side",
        "lv_obj_set_style_line_color", "lv_obj_set_style_line_width",
        "lv_obj_set_style_arc_color", "lv_obj_set_style_arc_opa", "lv_obj_set_style_arc_width",
        "lv_event_get_target",
        "lv_timer_create", "lv_timer_delete",
        "lv_canvas_set_draw_buf", "lv_draw_buf_create", "lv_color_black",

        // ── 宏(带括号) ──
        "lv_color_hex"
    };

    /// <summary>
    /// 已知 LVGL API 白名单（只读暴露，供自洽性测试校验）。
    /// 测试会断言：① 白名单与 <c>LvglApiCompatChecker</c> 的"已移除 API"列表无交集；
    /// ② 每个名字都能在 LVGL 头文件派生的对照表
    /// （<c>src/HmiEditor.Core/Resources/lvgl-api-names.txt</c>）中找到。
    /// </summary>
    public static IReadOnlyCollection<string> KnownLvglApiNames => KnownLvglApis;

    private static string Truncate(string text, int max = 60)
        => text.Length <= max ? text : text[..max] + "...";
}
