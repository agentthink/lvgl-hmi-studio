using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HmiEditor.Core.Design;
using HmiEditor.Core.Mapping;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Animation;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;
using HmiEditor.Core.CodeGen;

namespace HmiEditor.CodeGen;

/// <summary>
/// CodeGenerator 分部 —— 屏幕发射 —— 单屏创建函数、顶层包装、图层引用表、图层校验、自适应布局。
/// </summary>
public partial class CodeGenerator
{
    /// <summary>
    /// 生成单个 Screen 的 C 创建函数。
    /// 若提供 <paramref name="expander"/>，会先展开 Faceplate 实例再生成代码。
    /// </summary>
    /// <param name="availableImageIds">可用图片资源 sanitized ID 集合；null 表示所有引用的图片都可用（兼容旧调用）。</param>
    /// <param name="availableFontIds">可用字体资源 sanitized ID 集合；null 表示不校验字体资源可用性。</param>
    /// <summary>屏幕 DPI 倍率（阶段 228；默认 1.0 ⇒ 与不换算逐字节相同）。</summary>
    private double _dpiScale = 1.0;

    /// <summary>安全区是否生效（阶段 227）。无工程或全零内边距 ⇒ false ⇒ 生成物逐字节不变。</summary>
    private bool _safeActive;

    /// <summary>安全区（内容区）边界：左/上/右/下（屏幕坐标）。</summary>
    private (double Left, double Top, double Right, double Bottom) _safeRect;


    /// <summary>
    /// **安全区吸附**（阶段 227）：把控件矩形夹进安全区。
    /// 只夹**位置**（不改尺寸）：控件比安全区还大时只能贴左上（`Left/Top`），并在生成代码里留注释。
    /// </summary>
    private void SafeClampPosition(HmiControl control, out double x, out double y)
    {
        // ⚠ 用 out 参数而不是返回元组：阶段 204 的"逐字段消费账"靠**方法签名正则**归因
        //   （`(double X, double Y)` 这种返回类型里带括号会让它认不出方法名 ⇒ 记成"(顶层)"）。
        if (!_safeActive)
        {
            x = control.X;
            y = control.Y;
            return;
        }

        var maxX = Math.Max(_safeRect.Left, _safeRect.Right - control.Width);
        var maxY = Math.Max(_safeRect.Top, _safeRect.Bottom - control.Height);
        x = Math.Clamp(control.X, _safeRect.Left, maxX);
        y = Math.Clamp(control.Y, _safeRect.Top, maxY);
    }

    public string GenerateScreenCode(HmiScreen screen, HmiTheme? theme = null, FaceplateExpander? expander = null, IReadOnlyList<RuntimeTag>? tags = null, HmiProject? project = null, HashSet<string>? availableImageIds = null, HashSet<string>? availableFontIds = null)
    {
        var sb = new StringBuilder();

        // 单独调用 GenerateScreenCode 时同步 IncludeComments（project 非空才读取，避免覆盖 GenerateFullProject 的设置）
        if (project != null) _includeComments = project.ExportConfig.IncludeComments;

        // ── 屏幕安全区 ⇒ **生成几何**（阶段 227，登记 #98 的"进导出"）──
        // 语义：`ScreenDefaultsConfig.SafeAreaPadding` 是**异形屏避让**（见 `prop.screen.safe.area`）⇒
        // 导出时把**越界控件吸附进安全区**（只动越界的，不动本来就在区内的）。
        // ⚠ 默认（无工程 / 全零内边距）⇒ `_safeInset` 全零 ⇒ **输出逐字节不变**（判据钉住）。
        // ── 屏幕 DPI ⇒ **生成几何的整体换算**（阶段 228，`#98` 进导出的第二半）──
        // 语义：`DpiScale` 是"逻辑像素 → 设备像素"的倍率 ⇒ **坐标/尺寸/圆角/边宽/字号**一律乘它；
        // **语义数值**（值比例、阈值、时长）不乘。默认 1.0 ⇒ `CDim` 与 `CNumber` 逐字节相同（零影响）。
        var dpi = project?.ScreenDefaultsConfig?.DpiScale ?? 1.0;
        _dpiScale = dpi > 0 ? dpi : 1.0;

        var padding = project?.ScreenDefaultsConfig?.SafeAreaPadding;
        var safeLeft = padding?.Left ?? 0;
        var safeTop = padding?.Top ?? 0;
        var safeRight = padding?.Right ?? 0;
        var safeBottom = padding?.Bottom ?? 0;
        _safeActive = padding is not null
            && (safeLeft > 0 || safeTop > 0 || safeRight > 0 || safeBottom > 0)
            && screen.Width - safeLeft - safeRight > 0
            && screen.Height - safeTop - safeBottom > 0;
        _safeRect = (Left: safeLeft, Top: safeTop,
                     Right: screen.Width - safeRight, Bottom: screen.Height - safeBottom);

        if (_safeActive)
            // ⚠ 注释里的坐标用**设计空间**（编辑器里看到的数），代码是**设备空间**（乘过 DPI）——
            //    两者在 DPI=1 时相同；这样设计师看注释能直接对上画布。
            Cmt(sb, $"// 安全区（异形屏避让）：内容区 ({CNumber(_safeRect.Left)}, {CNumber(_safeRect.Top)})"
                + $"–({CNumber(_safeRect.Right)}, {CNumber(_safeRect.Bottom)})，越界控件会被吸附进来（坐标为设计空间）");


        // 展开 Faceplate 实例 —— 展开后输出等价的原子控件列表
        var controlsToGenerate = expander != null
            ? expander.ExpandScreen(screen)
            : screen.Controls.ToList();

        // 文件级 static 声明 —— 使控件指针在 create_screen_*() 和 apply_animations() 间共享
        foreach (var control in controlsToGenerate)
        {
            sb.AppendLine($"static lv_obj_t* {CIdentifier.ControlObject(control.Id)};");

            // I6: SVG 控件需要 canvas 像素缓冲（文件级静态数组）
            if (control.Type == HmiControlType.Svg)
            {
                sb.AppendLine(SvgToLvglConverter.GenerateBufferDeclaration(control));
            }

            // I7: 过程图形控件同样需要 canvas 像素缓冲；运行时更新函数必须放在
            // 文件作用域（放进 create_screen_*() 会成为嵌套函数定义，GCC 报"存储类无效"）。
            if (control.Type is HmiControlType.Tank or HmiControlType.Pipe or HmiControlType.Valve)
            {
                sb.AppendLine(ProcessControlCodeGen.GenerateBufferDeclaration(control));
                sb.AppendLine(ProcessControlCodeGen.GenerateUpdateFunction(control));
            }

            // I10: 椭圆（canvas 组合绘制）同样需要像素缓冲
            if (control.Type == HmiControlType.Ellipse)
            {
                sb.AppendLine(ShapeControlCodeGen.GenerateBufferDeclaration(control));
            }
        }
        if (controlsToGenerate.Count > 0)
            sb.AppendLine();

        // I9: 自适应布局回调 —— 在 create_screen 之前生成（非 None 模式）
        if (screen.AdaptiveMode != ScreenAdaptiveMode.None && controlsToGenerate.Count > 0)
        {
            sb.AppendLine(GenerateAdaptiveLayout(screen, controlsToGenerate));
        }

        // M4: 生成动作回调函数（在 create_screen 函数之前）
        var actionCallbacks = GenerateActionCallbacks(controlsToGenerate, tags, project, screen);
        if (!string.IsNullOrEmpty(actionCallbacks))
        {
            sb.AppendLine(actionCallbacks);
        }

        // 阶段 229（#92 功能那半）：状态文本/垂直对齐的运行时回调 —— 同样必须排在 create_screen_* 之前
        var visualStateCallbacks = GenerateVisualStateRuntimeCallbacks(controlsToGenerate);
        if (!string.IsNullOrEmpty(visualStateCallbacks))
        {
            sb.AppendLine(visualStateCallbacks);
        }

        sb.AppendLine($"static lv_obj_t* {CIdentifier.ScreenFactoryInner(screen.Id)}(lv_obj_t* parent)");
        sb.AppendLine("{");
        sb.AppendLine($"    lv_obj_t* screen = lv_obj_create(parent);");
        sb.AppendLine($"    lv_obj_set_size(screen, {CDim(screen.Width)}, {CDim(screen.Height)});");

        if (!string.IsNullOrEmpty(screen.BgColor))
        {
            var themeColor = theme?.ToLvglColor(screen.BgColor);
            sb.AppendLine($"    lv_obj_set_style_bg_color(screen, {themeColor}, 0);");
        }
        else if (theme != null)
        {
            sb.AppendLine($"    lv_obj_set_style_bg_color(screen, {theme.ToLvglColor(theme.BackgroundColor)}, 0);");
        }

        // 屏幕背景图片：创建全屏背景图对象并移动到屏幕背景层。
        // 若资源不在 AssetLibrary（availableImageIds 非空且不含该 id）则跳过，避免生成未声明标识符。
        if (!string.IsNullOrEmpty(screen.BgImageAssetId))
        {
            var sanitizedBg = CIdentifier.ForAsset(screen.BgImageAssetId);
            if (availableImageIds == null || availableImageIds.Contains(sanitizedBg))
            {
                var mode = screen.BgImageMode;
                var opa = (int)Math.Round(Math.Clamp(screen.BgImageOpacity, 0, 100) * 2.55);
                sb.AppendLine($"    /* Screen background image: {EscapeComment(screen.BgImageAssetId)} (mode={mode}, opacity={opa}) */");
                sb.AppendLine($"    lv_obj_t* bg_img = lv_image_create(screen);");
                sb.AppendLine($"    lv_image_set_src(bg_img, {CResourceDeclarations.ImagePathMacro(sanitizedBg)});");
                sb.AppendLine($"    lv_obj_set_size(bg_img, {CDim(screen.Width)}, {CDim(screen.Height)});");
                sb.AppendLine($"    lv_image_set_inner_align(bg_img, {LvglImageAlign(mode)});");
                sb.AppendLine($"    lv_obj_set_style_opa(bg_img, {opa}, 0);");
                sb.AppendLine($"    lv_obj_move_background(bg_img);");
            }
            else
            {
                Cmt(sb, $"    // 跳过：背景图资源 {screen.BgImageAssetId} 不在 AssetLibrary 中");
            }
        }

        sb.AppendLine();

        foreach (var control in controlsToGenerate)
        {
            // I6: SVG 控件走专用 canvas 生成路径（形状绘制在图层内完成）
            if (control.Type == HmiControlType.Svg)
            {
                Cmt(sb, $"    // {control.Name} ({control.Type})");
                sb.AppendLine(SvgToLvglConverter.GenerateCanvasCode(control));
                sb.AppendLine();
                continue;
            }

            // I7: 过程图形控件走专用 canvas 生成路径
            if (control.Type is HmiControlType.Tank or HmiControlType.Pipe or HmiControlType.Valve)
            {
                Cmt(sb, $"    // {control.Name} ({control.Type})");
                sb.AppendLine(ProcessControlCodeGen.GenerateCanvasCode(control));
                sb.AppendLine();
                continue;
            }

            // I10: 椭圆走专用 canvas 生成路径
            if (control.Type == HmiControlType.Ellipse)
            {
                Cmt(sb, $"    // {control.Name} ({control.Type})");
                sb.AppendLine(ShapeControlCodeGen.GenerateCode(control));
                sb.AppendLine();
                continue;
            }

            var createFunc = ControlMappingRegistry.GetCreateFunction(control.Type);
            Cmt(sb, $"    // {control.Name} ({control.Type})");
            sb.AppendLine($"    {CIdentifier.ControlObject(control.Id)} = {createFunc}(screen);");
            SafeClampPosition(control, out var emitX, out var emitY);
            if (!_safeActive || (Math.Abs(emitX - control.X) < 0.0001 && Math.Abs(emitY - control.Y) < 0.0001))
                sb.AppendLine($"    lv_obj_set_pos({CIdentifier.ControlObject(control.Id)}, {CDim(control.X)}, {CDim(control.Y)});");
            else
            {
                Cmt(sb, $"    // 安全区吸附（设计空间）：({CNumber(control.X)}, {CNumber(control.Y)}) → ({CNumber(emitX)}, {CNumber(emitY)})");
                sb.AppendLine($"    lv_obj_set_pos({CIdentifier.ControlObject(control.Id)}, {CDim(emitX)}, {CDim(emitY)});");
            }
            sb.AppendLine($"    lv_obj_set_size({CIdentifier.ControlObject(control.Id)}, {CDim(control.Width)}, {CDim(control.Height)});");

            // 文本（对齐 LVGL v9 官方 API）
            //   Label / CheckBox 直接 set_text；Button 需在按钮内创建子 Label 并 set_text
            //   （lv_label_set_text 传 Button 对象会导致类型越界访问，损坏内存堆）
            if (!string.IsNullOrEmpty(control.Text))
            {
                if (control.Type == HmiControlType.CheckBox)
                {
                    sb.AppendLine($"    lv_checkbox_set_text({CIdentifier.ControlObject(control.Id)}, \"{EscapeCString(control.Text)}\");");
                    // 🩸 阶段 219 补（登记 #96 的"导出补齐"）：**预览**一直按 `TextAlign` 画 CheckBox 文本
                    //   （`DesignCanvas.Renderers.cs` 的 RenderCheckBox → DrawAlignedText(..., control.TextAlign, ...)），
                    //   而导出此前**只设文本、不设对齐** ⇒ 改了「水平对齐」预览动了、导出没动。
                    sb.AppendLine($"    lv_obj_set_style_text_align({CIdentifier.ControlObject(control.Id)}, {LvglTextAlign(control.TextAlign)}, 0);");
                }
                else if (control.Type == HmiControlType.TextArea)
                {
                    // 🩸 阶段 219 补（登记 #99）：**预览**会画 TextArea 的 `Text`（占位文本另走 placeholder），
                    //   而导出此前**从不发射 `lv_textarea_set_text`** ⇒ 面板里输入的文本在设备上消失。
                    sb.AppendLine($"    lv_textarea_set_text({CIdentifier.ControlObject(control.Id)}, \"{EscapeCString(control.Text)}\");");
                    sb.AppendLine($"    lv_obj_set_style_text_align({CIdentifier.ControlObject(control.Id)}, {LvglTextAlign(control.TextAlign)}, 0);");
                }
                else if (control.Type == HmiControlType.Button)
                {
                    var btnLabel = CIdentifier.Prefixed("label_", control.Id);
                    sb.AppendLine($"    lv_obj_t* {btnLabel} = lv_label_create({CIdentifier.ControlObject(control.Id)});");
                    sb.AppendLine($"    lv_label_set_text({btnLabel}, \"{EscapeCString(control.Text)}\");");
                    sb.AppendLine($"    lv_obj_set_style_text_align({btnLabel}, {LvglTextAlign(control.TextAlign)}, 0);");
                    sb.AppendLine($"    lv_obj_center({btnLabel});");
                }
                else if (control.Type == HmiControlType.Label)
                {
                    sb.AppendLine($"    lv_label_set_text({CIdentifier.ControlObject(control.Id)}, \"{EscapeCString(control.Text)}\");");
                    sb.AppendLine($"    lv_obj_set_style_text_align({CIdentifier.ControlObject(control.Id)}, {LvglTextAlign(control.TextAlign)}, 0);");
                }
            }

            // 背景色（Label 等控件默认 bg_opa=0，配置颜色后需显式设为不透明才能看见）
            // Gauge/Knob 例外：BgColor 用于弧线颜色（LV_PART_MAIN arc_color），
            // 若在此填充中心背景会出现方块底色，与设计器 270° 弧形态不一致。
            // Arc 同理：BgColor 是轨道弧色（I10），禁止填充对象背景。
            if (!string.IsNullOrEmpty(control.BgColor) &&
                control.Type is not HmiControlType.Gauge and not HmiControlType.Knob
                    and not HmiControlType.Arc)
            {
                var colorHex = control.BgColor.TrimStart('#');
                sb.AppendLine($"    lv_obj_set_style_bg_color({CIdentifier.ControlObject(control.Id)}, {theme?.ToLvglColor(control.BgColor) ?? $"lv_color_hex(0x{colorHex})"}, 0);");
                sb.AppendLine($"    lv_obj_set_style_bg_opa({CIdentifier.ControlObject(control.Id)}, LV_OPA_COVER, 0);");
            }

            // 文本色
            if (!string.IsNullOrEmpty(control.TextColor))
            {
                var textColorHex = control.TextColor.TrimStart('#');
                sb.AppendLine($"    lv_obj_set_style_text_color({CIdentifier.ControlObject(control.Id)}, {theme?.ToLvglColor(control.TextColor) ?? $"lv_color_hex(0x{textColorHex})"}, 0);");
            }

            // 文本字体（自定义字体族 / 自定义字号）
            {
                var fontExpr = GetControlFontExpression(control, theme, availableFontIds);
                if (fontExpr != null)
                {
                    sb.AppendLine($"#if LV_USE_FREETYPE");
                    sb.AppendLine($"    lv_obj_set_style_text_font({CIdentifier.ControlObject(control.Id)}, {fontExpr}, 0);");
                    sb.AppendLine($"#endif");
                }
                else if (BuiltInFontSizeFor(control, theme) is { } builtInSize)
                {
                    // 🩸 阶段 220（#97）：没有字体资产时，字号不再被静默丢掉 —— 用 LVGL 内置
                    //   `lv_font_montserrat_{12,16}` 就近近似；用条件编译包住，设备没启用该档就整段消失。
                    sb.AppendLine($"#if LV_FONT_MONTSERRAT_{builtInSize}");
                    sb.AppendLine($"    lv_obj_set_style_text_font({CIdentifier.ControlObject(control.Id)}, &lv_font_montserrat_{builtInSize}, 0);");
                    sb.AppendLine($"#endif");
                }
            }

            // 圆角
            if (control.Radius > 0)
            {
                sb.AppendLine($"    lv_obj_set_style_radius({CIdentifier.ControlObject(control.Id)}, {control.Radius}, 0);");
            }

            // 边框（Arc 例外：BorderWidth 是弧线宽度 lv_arc arc_width，不是对象边框）
            if (control.BorderWidth > 0 && control.Type != HmiControlType.Arc)
            {
                sb.AppendLine($"    lv_obj_set_style_border_width({CIdentifier.ControlObject(control.Id)}, {control.BorderWidth}, 0);");
                // 控件级边框色（未配置时继承主题默认，与设计器 ParseColor 回退一致）
                if (!string.IsNullOrEmpty(control.BorderColor))
                {
                    var borderColor = theme?.ToLvglColor(control.BorderColor)
                        ?? $"lv_color_hex(0x{control.BorderColor.TrimStart('#')})";
                    sb.AppendLine($"    lv_obj_set_style_border_color({CIdentifier.ControlObject(control.Id)}, {borderColor}, 0);");
                }
            }

            // 状态化样式：遍历 VisualStates 生成 LV_STATE_PRESSED/CHECKED/DISABLED 样式覆盖
            GenerateVisualStates(sb, control, theme, availableImageIds);

            // 控件专属功能参数（量程/选项/占位/行列/页签/图片缩放等）
            GenerateControlParameters(sb, control, theme);

            // Label 垂直对齐：LVGL 无内置 vertical-align，通过 content-size + 重定位实现
            if (control.Type == HmiControlType.Label && control.TextVAlign != TextVAlignMode.Middle)
            {
                var o = CIdentifier.ControlObject(control.Id);
                // 局部高度变量：仅在本块内声明/引用，故直接用 Prefixed（不存在"两处拼接"）
                var h = CIdentifier.Prefixed("_h_", control.Id);
                sb.AppendLine($"    lv_obj_set_height({o}, LV_SIZE_CONTENT);");
                sb.AppendLine($"    lv_obj_update_layout({o});");
                sb.AppendLine($"    int32_t {h} = lv_obj_get_height({o});");
                sb.AppendLine($"    if ({h} > {CDim(control.Height)}) {{");
                sb.AppendLine($"        {h} = {CDim(control.Height)};");
                sb.AppendLine($"        lv_obj_set_height({o}, {h});");
                sb.AppendLine($"    }}");
                var vAlignExpr = control.TextVAlign switch
                {
                    TextVAlignMode.Top => $"{CDim(control.Y)}",
                    TextVAlignMode.Bottom => $"{CDim(control.Y)} + {CDim(control.Height)} - {h}",
                    _ => $"{CDim(control.Y)} + ({CDim(control.Height)} - {h}) / 2"
                };
                sb.AppendLine($"    lv_obj_set_y({o}, {vAlignExpr});");
            }

            // 旋转/镜像：围绕控件中心（LVGL transform：rotation 0.1° 单位，scale 256=1.0，负值镜像）
            AppendTransform(sb, CIdentifier.ControlObject(control.Id), control);

            // 变量绑定（注释形式输出，便于追踪 Faceplate 展开后的绑定关系）
            // 联动模型：遍历 VariableBindings 集合，回退旧字段
            if (control.VariableBindings.Count > 0)
            {
                foreach (var binding in control.VariableBindings)
                {
                    if (binding.VariableId.Contains("{{"))
                    {
                        Cmt(sb, $"    // WARNING: Unresolved Faceplate parameter in binding '{binding.PropertyName}' (skipped)");
                        continue;
                    }

                    // 表达式绑定：校验语法并附 C 转译结果（供运行时集成）
                    if (binding.Kind == BindingSourceKind.Expression)
                    {
                        var expr = binding.Expression ?? "";
                        if (!ExpressionEngine.TryCompile(expr, out var exprError))
                        {
                            // ⚠ **刻意用回落原文（中文），不调 ExternalText.Describe**
                            //   （阶段 91 / #61i）：这句注释写进产物 generated_ui.c，读者是看 C 代码的人，
                            //   不随界面语言变 —— 与 GeneratedCodeIssueCodes.GENERATOR_WARNING_COMMENT
                            //   同一类"判过的不做"（登记 #61k）。
                            Cmt(sb, $"    // WARNING: Invalid expression for '{binding.PropertyName}': {exprError.Message} (skipped)");
                            continue;
                        }
                        var cExpr = ExpressionEngine.ToCExpression(expr);
                        Cmt(sb, $"    // Expression binding: {binding.PropertyName} = {expr}");
                        if (!string.IsNullOrEmpty(cExpr))
                            Cmt(sb, $"    //   C: {cExpr}");
                        continue;
                    }

                    Cmt(sb, $"    // Bound variable: {binding.VariableId} ({binding.PropertyName}, {binding.Direction})");
                }
            }
            else if (!string.IsNullOrEmpty(control.VariableBindingId) && !control.VariableBindingId.Contains("{{"))
            {
                Cmt(sb, $"    // Bound variable: {control.VariableBindingId}");
            }
            else if (!string.IsNullOrEmpty(control.VariableBindingId) && control.VariableBindingId.Contains("{{"))
            {
                // L2: 未替换的 Faceplate 参数占位符 —— 输出警告注释（不输出原始占位符文本）
                Cmt(sb, $"    // WARNING: Unresolved Faceplate parameter in variable binding (skipped)");
            }

            // 值显示格式化配置（Label 等显示型控件：值类型/缩放比例/小数位/前后缀，供运行时按此格式化值）
            if (control.ValueDisplayType != ValueDisplayType.Auto
                || control.ValueScaleFactor != 1.0
                || control.ValueDecimalPlaces != 2
                || !string.IsNullOrEmpty(control.ValuePrefix)
                || !string.IsNullOrEmpty(control.ValueSuffix))
            {
                var fmtParts = new List<string>
                {
                    $"displayType={control.ValueDisplayType}",
                    $"scale={CNumber(control.ValueScaleFactor)}",
                    $"decimals={control.ValueDecimalPlaces}"
                };
                if (!string.IsNullOrEmpty(control.ValuePrefix)) fmtParts.Add($"prefix=\"{control.ValuePrefix}\"");
                if (!string.IsNullOrEmpty(control.ValueSuffix)) fmtParts.Add($"suffix=\"{control.ValueSuffix}\"");
                Cmt(sb, $"    // Value display: {string.Join(", ", fmtParts)}");
            }

            // M4: 事件回调注册（联动模型：遍历 Actions 集合，回退旧字段）
            if (control.Actions.Count > 0)
            {
                foreach (var action in control.Actions)
                {
                    if (action.ActionType != "None")
                        AppendEventRegistration(sb, control, action);
                }
            }
            else if (control.Action != null && control.Action.ActionType != "None")
            {
                AppendEventRegistration(sb, control, control.Action);
            }

            sb.AppendLine();
        }

        // I9: 自适应布局回调在创建完成后应用（仅非 None 模式）
        if (screen.AdaptiveMode != ScreenAdaptiveMode.None && controlsToGenerate.Count > 0)
        {
            sb.AppendLine($"    {CIdentifier.AdaptiveLayoutFn(screen.Id)}(screen);");
        }

        sb.AppendLine("    return screen;");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// 生成独立屏幕的顶层包装函数 create_screen_&lt;id&gt;()。
    /// 仅对"未被引用为图层的屏幕"生成 —— 被引用的公共页面只以图层（_inner 工厂）形式存在，
    /// 不再提供独立全屏入口（保持 HMI_SCREENS[] 注册表与运行时行为一致）。
    /// 注意：非 static，供模拟器 main.c 等外部编译单元链接（_inner 保持 static 文件内共享）。
    /// </summary>
    public string GenerateScreenTopLevelWrapper(HmiScreen screen)
    {
        return $"lv_obj_t* {CIdentifier.ScreenFactory(screen.Id)}(void) {{ return {CIdentifier.ScreenFactoryInner(screen.Id)}(NULL); }}";
    }

    /// <summary>
    /// 生成图层引用注册表 HMI_LAYER_REFS[]。
    /// 每个条目描述：引用方屏幕（screen_id）→ 被引用公共页面图层（layer_id），
    /// 图层对象由 create_screen_&lt;layer_id&gt;_inner 创建（detached），运行时随屏幕切换转移挂载。
    /// 无引用时输出占位条目并返回空表（引用计数 0）。
    /// </summary>
    public string GenerateLayerRefsTable(HmiProject project)
    {
        var sb = new StringBuilder();
        var refs = new List<string>();
        foreach (var screen in project.Screens)
        {
            foreach (var layerId in screen.ReferencedLayerIds)
            {
                if (string.IsNullOrEmpty(layerId)) continue;
                refs.Add($"    {{ \"{screen.Id}\", \"{layerId}\", {CIdentifier.ScreenFactoryInner(layerId)} }},");
            }
        }

        sb.AppendLine("/* ── 图层引用表（公共页面作为背景图层，随屏幕切换转移挂载） ── */");
        if (refs.Count == 0)
        {
            sb.AppendLine("const screen_layer_ref_t HMI_LAYER_REFS[1] = { { NULL, NULL, NULL } };");
            sb.AppendLine("const int HMI_LAYER_REF_COUNT = 0;");
        }
        else
        {
            sb.AppendLine("const screen_layer_ref_t HMI_LAYER_REFS[] = {");
            sb.AppendLine(string.Join(Environment.NewLine, refs));
            sb.AppendLine("};");
            sb.AppendLine($"const int HMI_LAYER_REF_COUNT = {refs.Count};");
        }
        sb.AppendLine();
        sb.AppendLine("/* 图层表访问器：宿主机 hmi_runtime 通过 dlsym(\"hmi_ui_get_layers\") 获取。");
        sb.AppendLine("   旧版 .so 无此符号时运行时不启用图层转移（向后兼容）。 */");
        sb.AppendLine("const screen_layer_ref_t* hmi_ui_get_layers(int* count)");
        sb.AppendLine("{");
        sb.AppendLine("    if (count) *count = HMI_LAYER_REF_COUNT;");
        sb.AppendLine("    return HMI_LAYER_REFS;");
        sb.AppendLine("}");
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// 校验图层引用关系，将错误以注释形式写入生成代码（不阻断生成）。
    /// </summary>
    private void EmitLayerValidationComments(StringBuilder sb, HmiProject project)
    {
        foreach (var error in ScreenLayerRefs.Validate(project))
        {
            Cmt(sb, $"// WARNING: 图层引用校验失败 —— {error}");
        }
    }

    /// <summary>
    /// I9: 生成自适应布局回调 —— 依据 display 实际分辨率重算控件位置与尺寸。
    /// Fit: 等比缩放 + 居中；Fill: 横纵独立拉伸。
    /// </summary>
    private static string GenerateAdaptiveLayout(HmiScreen screen, List<HmiControl> controls)
    {
        var sb = new StringBuilder();
        var isFit = screen.AdaptiveMode == ScreenAdaptiveMode.Fit;

        sb.AppendLine($"static void {CIdentifier.AdaptiveLayoutFn(screen.Id)}(lv_obj_t* screen)");
        sb.AppendLine("{");
        sb.AppendLine($"    lv_coord_t base_w = {screen.Width:0.#}, base_h = {screen.Height:0.#};");
        sb.AppendLine("    lv_coord_t act_w = lv_display_get_horizontal_resolution(lv_obj_get_display(screen));");
        sb.AppendLine("    lv_coord_t act_h = lv_display_get_vertical_resolution(lv_obj_get_display(screen));");
        sb.AppendLine("    if (act_w == 0 || act_h == 0) return;");
        sb.AppendLine("    float sx = (float)act_w / base_w, sy = (float)act_h / base_h;");
        if (isFit)
        {
            sb.AppendLine("    float s = sx < sy ? sx : sy;  /* 等比缩放 */");
            sb.AppendLine("    float ox = (act_w - base_w * s) * 0.5f, oy = (act_h - base_h * s) * 0.5f;");
        }
        else
        {
            sb.AppendLine("    float ox = 0.0f, oy = 0.0f;  /* Fill: 拉伸铺满 */");
        }

        foreach (var control in controls)
        {
            // I6/I10: Svg/Ellipse 使用 canvas, 尺寸由绘制定义（像素缓冲固定，不可随分辨率重设）
            if (control.Type is HmiControlType.Svg or HmiControlType.Ellipse) continue;
            var scaleX = isFit ? "s" : "sx";
            var scaleY = isFit ? "s" : "sy";
            sb.AppendLine($"    lv_obj_set_pos({CIdentifier.ControlObject(control.Id)}, (lv_coord_t)({control.X:0.#} * {scaleX} + ox), (lv_coord_t)({control.Y:0.#} * {scaleY} + oy));");
            sb.AppendLine($"    lv_obj_set_size({CIdentifier.ControlObject(control.Id)}, (lv_coord_t)({control.Width:0.#} * {scaleX}), (lv_coord_t)({control.Height:0.#} * {scaleY}));");
        }

        sb.AppendLine("}");
        sb.AppendLine();
        return sb.ToString();
    }
}
