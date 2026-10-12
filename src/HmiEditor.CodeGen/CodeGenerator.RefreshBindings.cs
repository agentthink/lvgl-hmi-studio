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
/// CodeGenerator 分部 —— 绑定值刷新发射 —— 模拟器侧 / 设备侧 / 两者共享的唯一实现。
/// </summary>
public partial class CodeGenerator
{
    /// <summary>
    /// 生成绑定模拟刷新函数 sim_refresh_bindings(tick)（PC 模拟器专用）。
    /// 无真实设备层时，用正弦波 + 控件相位差模拟绑定变量值，按控件类型刷新：
    ///   Label → lv_label_set_text_fmt（数值 + 单位）
    ///   Bar → lv_bar_set_value；Slider → lv_slider_set_value；Gauge/Knob → lv_arc_set_value
    /// 遍历所有控件的 VariableBindings（同一变量可绑定多个控件，逐个刷新）。
    /// </summary>
    private static string GenerateSimRefreshBindings(IReadOnlyList<RuntimeTag>? tags, HmiProject project)
    {
        var (code, _) = GenerateRefreshBindings(project, tags, deviceMode: false);
        return code;
    }

    /// <summary>
    /// 生成设备端绑定值周期刷新（SharedLibrary/实机专用，2026-08-18）：
    /// <c>sim_refresh_bindings</c> 是 PC 模拟器专用（正弦波模拟）；实机 .so 此前缺失绑定
    /// 刷新 → 变量缓存更新后 UI 控件永不刷新（屏幕停留在初始值，"Modbus 无数据"假象）。
    /// 本函数生成：
    ///   <list type="bullet">
    ///     <item><c>hmi_ui_refresh_timer_cb</c> —— 周期读 <c>get_tag_value(TAG_x)</c> 真实值
    ///       刷新绑定控件（Led 亮度/Switch 状态/Label 文本/Bar/Slider/Arc 值/Table 单元格）；</item>
    ///     <item><c>hmi_ui_start_binding_refresh</c> —— 创建 LVGL timer（宿主 lv_timer_handler
    ///       驱动；<c>s_ui_refresh_timer</c> 判空防多屏 init 重复创建）。</item>
    ///   </list>
    /// 仅刷新标签表（<paramref name="tags"/>）中存在的变量，避免生成 TAG_ 未定义常量。
    /// 返回 (生成代码, 是否存在绑定刷新)；HasRefresh 供 teardown 决定是否释放 timer。
    /// </summary>
    private static (string Code, bool HasRefresh) GenerateDeviceRefreshBindings(HmiProject project, IReadOnlyList<RuntimeTag>? tags)
        => GenerateRefreshBindings(project, tags, deviceMode: true);

    /// <summary>
    /// 绑定刷新代码的**共享实现** —— PC 模拟器与设备端 .so 的唯一生成路径。
    ///
    /// 两者原本是两份各自演化的代码（控件分支逐类型重复），已实际导致行为漂移
    /// （Led/Switch 判真不同、Label 小数位不同、设备端缺少某些控件分支）。
    /// 现在只有三处差异，全部由 <paramref name="deviceMode"/> 参数化：
    ///   <list type="number">
    ///     <item>值来源：模拟器 = 正弦波模拟；设备 = <c>get_tag_value(TAG_x)</c> 读真实缓存；</item>
    ///     <item>空指针守卫：设备端控件可能尚未创建，需要 <c>if (obj_x)</c>；模拟器不需要；</item>
    ///     <item>变量过滤与外壳：设备端仅刷新标签表中的变量，并附带 timer 启停外壳。</item>
    ///   </list>
    /// </summary>
    private static (string Code, bool HasRefresh) GenerateRefreshBindings(
        HmiProject project, IReadOnlyList<RuntimeTag>? tags, bool deviceMode)
    {
        var tagIds = new HashSet<string>(tags?.Select(t => t.VariableId) ?? Enumerable.Empty<string>());

        // ── 1) 收集绑定：视觉通路（颜色/可见性）与值通路分开 ──
        //    设备模式仅保留标签表中的变量，避免生成未定义的 TAG_ 常量；
        //    表达式绑定则要求**引用的每个变量**都在标签表里，否则表达式里会出现未定义的 TAG_。
        var visualTargets = new List<BindingRefreshTarget>();
        var valueTargets = new List<BindingRefreshTarget>();

        // 走 Faceplate **展开后**的控件清单 —— 克隆控件的 VariableBindings 是深拷贝来的
        // （参数占位符也已替换成实参），用原始清单会让它们静默不刷新。
        foreach (var c in EnumerateRenderableControls(project))
        {
            if (c.VariableBindings.Count > 0)
            {
                foreach (var b in c.VariableBindings)
                {
                    // 未替换的 Faceplate 参数占位符：值通路历来跳过，视觉通路同样跳过
                    if (ContainsUnresolvedPlaceholder(b.VariableId) || ContainsUnresolvedPlaceholder(b.Expression))
                        continue;

                    var target = BuildRefreshTarget(c, b, tagIds, deviceMode);
                    if (target is null) continue;

                    (target.Visual is not null ? visualTargets : valueTargets).Add(target);
                }
            }
            else if (!string.IsNullOrEmpty(c.VariableBindingId) && !ContainsUnresolvedPlaceholder(c.VariableBindingId)
                     && (!deviceMode || tagIds.Contains(c.VariableBindingId)))
            {
                valueTargets.Add(new BindingRefreshTarget(
                    c, c.VariableBindingId, Expression: null, Visual: null, PropertyName: "Text"));
            }
        }

        // ── 2) 表格单元格绑定（Table.Cells 中 IsBound 的单元格） ──
        var tableCells = new List<(HmiControl Control, TableCellData Cell)>();
        // 同上：表格克隆体的单元格绑定也必须进刷新表
        foreach (var c in EnumerateRenderableControls(project))
        {
            if (c.Type != HmiControlType.Table || c.Cells == null) continue;
            foreach (var cell in c.Cells)
            {
                if (!cell.IsBound || cell.Row < 0 || cell.Col < 0) continue;
                if (deviceMode && (string.IsNullOrEmpty(cell.VariableId) || !tagIds.Contains(cell.VariableId!))) continue;
                tableCells.Add((c, cell));
            }
        }

        if (valueTargets.Count == 0 && visualTargets.Count == 0 && tableCells.Count == 0)
        {
            return (deviceMode
                ? "/* No bound variables in tag table — 实机静态显示 */" + Environment.NewLine
                : "/* No bound variables — 模拟器静态显示 */" + Environment.NewLine, false);
        }

        // ── 3) 变量 ID → 单位 / 数据类型（供 BindingSemantics 使用） ──
        var unitById = new Dictionary<string, string>();
        var dataTypeById = new Dictionary<string, string>();
        foreach (var v in project.VariableManager.Variables)
        {
            if (!string.IsNullOrEmpty(v.Unit)) unitById[v.Id] = v.Unit;
            if (!string.IsNullOrEmpty(v.DataType)) dataTypeById[v.Id] = v.DataType;
        }

        var sb = new StringBuilder();
        if (deviceMode)
        {
            sb.AppendLine("/* ── 绑定值周期刷新（真实设备层：读 get_tag_value 驱动 UI）──");
            sb.AppendLine(" * 实机 .so 专用：PC 模拟器的 sim_refresh_bindings 用正弦波模拟，");
            sb.AppendLine(" * 设备端必须读真实缓存值刷新，否则变量数据到缓存后屏幕不更新。");
            sb.AppendLine(" * timer 由宿主主循环 lv_timer_handler() 驱动（~100ms）。 */");
            sb.AppendLine("static lv_timer_t* s_ui_refresh_timer = NULL;");
            sb.AppendLine("static void hmi_ui_refresh_timer_cb(lv_timer_t* t)");
            sb.AppendLine("{");
            sb.AppendLine("    (void)t;");
        }
        else
        {
            sb.AppendLine("/* 绑定模拟刷新（PC 模拟器）—— 正弦波模拟绑定变量，刷新显示控件 */");
            sb.AppendLine("#include <math.h>");
            sb.AppendLine("void sim_refresh_bindings(uint32_t tick)");
            sb.AppendLine("{");
            sb.AppendLine("    float t = (float)(tick % 4000) * 0.02f;  /* 时间轴循环, 约 80s 周期 */");
        }

        var handled = new HashSet<string>();

        // ── 4) 值通路刷新 ──
        foreach (var target in valueTargets)
        {
            var ctrl = target.Control;
            var ctrlId = CIdentifier.ForControlSymbol(ctrl.Id);
            // 模拟器按「控件 + 属性」去重（同一控件多绑定只刷新一次）；设备模式保持历史行为逐绑定输出
            if (!deviceMode && !handled.Add(ctrlId + ":" + target.PropertyName)) continue;

            var tagConst = "TAG_" + RuntimeTagsCodeGen.SanitizeIdentifierPublic(target.VariableId);
            var min = ctrl.MinValue; var max = ctrl.MaxValue;
            if (max <= min) { max = min + 100; }

            // 值声明：模拟器 = 正弦波 + 相位差；设备 = 标签缓存值；表达式源两端都走 ToCExpression
            var valueDecl = BuildValueDeclaration(target, min, max, deviceMode, sb);

            // 单条语句：设备模式包 `if (obj_x)` 空指针守卫，模拟器直接输出
            void Stmt(string statement)
                => sb.AppendLine(deviceMode ? $"        if (obj_{ctrlId}) {statement}" : $"        {statement}");

            switch (ctrl.Type)
            {
                case HmiControlType.Label:
                {
                    dataTypeById.TryGetValue(target.VariableId, out var labelDt);
                    unitById.TryGetValue(target.VariableId, out var labelUnit);
                    var labelSem = BindingSemantics.For(ctrl, labelDt, labelUnit);
                    sb.AppendLine("    {");
                    sb.AppendLine(valueDecl);
                    if (deviceMode)
                    {
                        var labelStmt = BuildBindingTextStatement($"obj_{ctrlId}", "v", labelSem, "").TrimStart();
                        sb.AppendLine($"        if (obj_{ctrlId}) {{ {labelStmt} lv_obj_invalidate(obj_{ctrlId}); }}");
                    }
                    else
                    {
                        sb.AppendLine($"        (void){tagConst};");
                        sb.AppendLine(BuildBindingTextStatement($"obj_{ctrlId}", "v", labelSem, "        "));
                        sb.AppendLine($"        lv_obj_invalidate(obj_{ctrlId});  /* 强制重绘, 确保文本刷新 */");
                    }
                    sb.AppendLine("    }");
                    break;
                }
                case HmiControlType.Bar:
                {
                    sb.AppendLine("    {");
                    sb.AppendLine(valueDecl);
                    if (!deviceMode) sb.AppendLine($"        (void){tagConst};");
                    Stmt($"lv_bar_set_value(obj_{ctrlId}, (int32_t)v, LV_ANIM_OFF);");
                    // 游标 + 小三角跟随指示器末端（ShowPointer=true 时对象存在）
                    if (ctrl.ShowPointer == true)
                    {
                        var w = ctrl.Width;
                        var curD = (int)Math.Round(Math.Min(w, ctrl.Height) * 0.7);
                        sb.AppendLine($"        lv_obj_align({CIdentifier.Prefixed("_cur_", ctrl.Id)}, LV_ALIGN_LEFT_MID, (int)((v - {CFloat(min)}) / {CFloat(max - min)} * {w} + 0.5f) - 1 - {curD / 2}, 0);");
                        sb.AppendLine($"        lv_obj_align({CIdentifier.Prefixed("_curtri_", ctrl.Id)}, LV_ALIGN_LEFT_MID, (int)((v - {CFloat(min)}) / {CFloat(max - min)} * {w} + 0.5f) - 1 - 7, -{Math.Round(Math.Min(w, ctrl.Height) * 0.7 / 2 * 0.88 - 4)});");
                    }
                    sb.AppendLine("    }");
                    break;
                }
                case HmiControlType.Slider:
                {
                    sb.AppendLine("    {");
                    sb.AppendLine(valueDecl);
                    if (!deviceMode) sb.AppendLine($"        (void){tagConst};");
                    Stmt($"lv_slider_set_value(obj_{ctrlId}, (int32_t)v, LV_ANIM_OFF);");
                    // knob 顶部小三角跟随（ShowPointer != false 时对象存在）
                    if (ctrl.ShowPointer != false)
                    {
                        var w = ctrl.Width;
                        var r = Math.Min(w, ctrl.Height) / 2.0;
                        sb.AppendLine($"        lv_obj_align({CIdentifier.Prefixed("_knobtri_", ctrl.Id)}, LV_ALIGN_LEFT_MID, (int)((v - {CFloat(min)}) / {CFloat(max - min)} * {w} + 0.5f) - 10, -{Math.Round(r * 0.88 - 4)});");
                    }
                    sb.AppendLine("    }");
                    break;
                }
                case HmiControlType.Gauge:
                case HmiControlType.Knob:
                case HmiControlType.Arc:
                    sb.AppendLine("    {");
                    sb.AppendLine(valueDecl);
                    if (!deviceMode) sb.AppendLine($"        (void){tagConst};");
                    Stmt($"lv_arc_set_value(obj_{ctrlId}, (int32_t)v);");
                    sb.AppendLine("    }");
                    break;
                case HmiControlType.Led:
                    sb.AppendLine("    {");
                    sb.AppendLine(valueDecl);
                    if (!deviceMode) sb.AppendLine($"        (void){tagConst};");
                    Stmt($"lv_led_set_brightness(obj_{ctrlId}, {CBoolTruth("v")} ? 255 : 0);");
                    sb.AppendLine("    }");
                    break;
                case HmiControlType.Switch:
                case HmiControlType.CheckBox:
                    sb.AppendLine("    {");
                    sb.AppendLine(valueDecl);
                    if (deviceMode)
                    {
                        sb.AppendLine($"        if (obj_{ctrlId}) {{");
                        sb.AppendLine($"            if ({CBoolTruth("v")}) lv_obj_add_state(obj_{ctrlId}, LV_STATE_CHECKED);");
                        sb.AppendLine($"            else lv_obj_remove_state(obj_{ctrlId}, LV_STATE_CHECKED);");
                        sb.AppendLine($"        }}");
                    }
                    else
                    {
                        sb.AppendLine($"        (void){tagConst};");
                        sb.AppendLine($"        if ({CBoolTruth("v")}) lv_obj_add_state(obj_{ctrlId}, LV_STATE_CHECKED);");
                        sb.AppendLine($"        else lv_obj_remove_state(obj_{ctrlId}, LV_STATE_CHECKED);");
                    }
                    sb.AppendLine("    }");
                    break;
            }
        }

        // ── 5) 视觉通路刷新：颜色 / 可见性（P1-1）──
        //    值来源与值通路同源（变量 = 正弦波/标签缓存，表达式 = ToCExpression），
        //    差别只在「怎么用这个值」—— 那由 VisualBindingCodeGen 单点决定。
        foreach (var target in visualTargets)
        {
            var ctrl = target.Control;
            var ctrlId = CIdentifier.ForControlSymbol(ctrl.Id);
            if (!deviceMode && !handled.Add("visual:" + ctrlId + ":" + target.PropertyName)) continue;

            var min = ctrl.MinValue; var max = ctrl.MaxValue;
            if (max <= min) { max = min + 100; }

            sb.AppendLine("    {");
            sb.AppendLine(BuildValueDeclaration(target, min, max, deviceMode, sb));
            if (deviceMode)
            {
                sb.AppendLine($"        if (obj_{ctrlId}) {{");
                VisualBindingCodeGen.EmitRefresh(sb, "            ", $"obj_{ctrlId}", "v", target.Visual!);
                sb.AppendLine("        }");
            }
            else
            {
                VisualBindingCodeGen.EmitRefresh(sb, "        ", $"obj_{ctrlId}", "v", target.Visual!);
            }
            sb.AppendLine("    }");
        }

        // ── 表格单元格绑定：逐单元格刷新 ──
        foreach (var (ctrl, cell) in tableCells)
        {
            var ctrlId = CIdentifier.ForControlSymbol(ctrl.Id);
            var cellVar = cell.VariableId!;
            var tagConst = "TAG_" + RuntimeTagsCodeGen.SanitizeIdentifierPublic(cellVar);
            unitById.TryGetValue(cellVar, out var cellUnit);
            var prefix = EscapeFormatLiteral(cell.Prefix);
            var suffix = EscapeFormatLiteral(cell.Suffix ?? cellUnit);
            var decimals = Math.Clamp(cell.DecimalPlaces, 0, BindingFormat.MaxDecimals);
            var fmt = prefix + "%." + decimals + "f" + suffix;

            sb.AppendLine("    {");
            if (deviceMode)
            {
                sb.AppendLine($"        float v = get_tag_value({tagConst});");
                sb.AppendLine($"        if (obj_{ctrlId}) lv_table_set_cell_value_fmt(obj_{ctrlId}, {cell.Row}, {cell.Col}, \"{fmt}\", (double)v);");
            }
            else
            {
                var phase = SimBinding.PhaseForCell(ctrl.Id, cell.Row, cell.Col);
                sb.AppendLine($"        float v = {SimBinding.TableDefaultExpr}(t + {phase}.0f * {SimBinding.Deg2RadC});");
                sb.AppendLine($"        (void){tagConst};");
                sb.AppendLine($"        lv_table_set_cell_value_fmt(obj_{ctrlId}, {cell.Row}, {cell.Col}, \"{fmt}\", (double)v);");
            }
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        if (deviceMode)
        {
            sb.AppendLine("static void hmi_ui_start_binding_refresh(void)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!s_ui_refresh_timer)");
            sb.AppendLine("        s_ui_refresh_timer = lv_timer_create(hmi_ui_refresh_timer_cb, 100, NULL);");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return (sb.ToString(), true);
    }

    // ── 绑定刷新的目标收集与值声明（视觉通路与值通路共用，避免两份取值规则）──

    /// <summary>
    /// 一条待刷新的绑定。
    /// <paramref name="Visual"/> 非 null 表示走视觉通路（颜色/可见性），
    /// 为 null 表示走既有的值通路（文本/进度/状态）。
    /// </summary>
    private sealed record BindingRefreshTarget(
        HmiControl Control,
        string VariableId,
        string? Expression,
        BindingVisualProperty? Visual,
        string PropertyName);

    /// <summary>
    /// 解析一条绑定为刷新目标；返回 null 表示这条绑定**不应**生成刷新代码
    /// （空变量、语法错误、设备端缺标签 —— 后者会生成未定义的 TAG_ 常量）。
    /// </summary>
    private static BindingRefreshTarget? BuildRefreshTarget(
        HmiControl control, VariableBindingData binding, HashSet<string> tagIds, bool deviceMode)
    {
        var visual = BindingVisualProperty.Find(binding.PropertyName);

        if (binding.Kind == BindingSourceKind.Expression)
        {
            var expr = binding.Expression;
            if (string.IsNullOrWhiteSpace(expr)) return null;
            if (!ExpressionEngine.TryCompile(expr, out _)) return null;

            // 设备端：表达式引用的**每个**变量都必须在标签表里，否则 ToCExpression 会引用未定义的 TAG_ 常量
            if (deviceMode)
            {
                foreach (var name in ExpressionEngine.ExtractVariableNames(expr))
                {
                    if (!tagIds.Contains(name)) return null;
                }
            }

            return new BindingRefreshTarget(control, VariableId: "", expr, visual, binding.PropertyName);
        }

        if (string.IsNullOrEmpty(binding.VariableId)) return null;
        if (deviceMode && !tagIds.Contains(binding.VariableId)) return null;

        return new BindingRefreshTarget(control, binding.VariableId, Expression: null, visual, binding.PropertyName);
    }

    /// <summary>
    /// 生成值声明 <c>float v = ...;</c>，必要时**先**往 <paramref name="sb"/> 写预置行
    /// （模拟器端的表达式绑定需要先把引用的变量声明成局部正弦波）。
    /// </summary>
    private static string BuildValueDeclaration(
        BindingRefreshTarget target, double min, double max, bool deviceMode, StringBuilder sb)
    {
        if (target.Expression is not null)
        {
            if (!deviceMode) return BuildSimExpressionDeclaration(target.Expression, sb);

            // 设备端：变量接到既有取值机制上，不引入新的运行时取值函数
            var expr = ExpressionEngine.ToCExpression(
                target.Expression,
                name => $"get_tag_value(TAG_{RuntimeTagsCodeGen.SanitizeIdentifierPublic(name)})");
            return $"        float v = (float)({expr});";
        }

        if (deviceMode)
            return $"        float v = get_tag_value(TAG_{RuntimeTagsCodeGen.SanitizeIdentifierPublic(target.VariableId)});";

        var phase = SimBinding.PhaseFor(target.Control.Id);
        var mid = (min + max) / 2;
        var amp = (max - min) * SimBinding.AmplitudeRatio;
        return $"        float v = {CFloat(mid)} + {CFloat(amp)} * sinf(t + {phase}.0f * {SimBinding.Deg2RadC});";
    }

    /// <summary>
    /// 模拟器端的表达式绑定：把表达式引用的每个变量声明成局部正弦波（与变量绑定同一口径，
    /// 模拟器没有真实值来源），再返回 <c>float v = (float)(&lt;表达式&gt;);</c>。
    /// 每条绑定各自成块，故局部名不会相撞。
    /// </summary>
    private static string BuildSimExpressionDeclaration(string expression, StringBuilder sb)
    {
        var mapper = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in ExpressionEngine.ExtractVariableNames(expression).OrderBy(n => n, StringComparer.Ordinal))
        {
            var local = CIdentifier.Prefixed("_bxv_", name);
            mapper[name] = local;
            var phase = SimBinding.PhaseFor(name);
            sb.AppendLine($"        float {local} = {SimBinding.TableDefaultExpr}(t + {phase}.0f * {SimBinding.Deg2RadC});");
        }

        var cExpr = ExpressionEngine.ToCExpression(
            expression,
            name => mapper.TryGetValue(name, out var local) ? local : "0.0f");
        return $"        float v = (float)({cExpr});";
    }

    /// <summary>未替换的 Faceplate 参数占位符（<c>{{arg}}</c>）—— 生成刷新代码前必须跳过。</summary>
    private static bool ContainsUnresolvedPlaceholder(string? text)
        => text is not null && text.Contains("{{", StringComparison.Ordinal);
}
