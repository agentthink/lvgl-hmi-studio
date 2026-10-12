using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using BindingDirection = HmiEditor.Core.Models.BindingDirection;

namespace HmiEditor.Core.Design;

/// <summary>
/// R2-T2 设计时预览值解析器 —— 纯函数，将控件绑定 + 模拟值提供器解析为
/// 预览模式下应显示的文本/进度。不修改模型，无副作用。
///
/// 适应度函数 R2-FF（预览模式隔离：0 写操作）：
///   该类型只读取 HmiControl 与外部提供器，绝不写回模型属性。
///   DesignCanvas 在预览模式下调用此解析器渲染模拟值，退出预览后模型保持原样。
/// </summary>
public static class PreviewValueResolver
{
    /// <summary>
    /// 解析控件在预览模式下应显示的文本。
    /// 返回 null 表示未启用预览、控件无绑定、或提供器无值 —— 调用方应回退到编辑态渲染。
    /// </summary>
    /// <param name="control">目标控件（只读引用，不会被修改）。</param>
    /// <param name="valueProvider">变量ID → 模拟值 的查找函数。null 表示未启用预览。</param>
    public static string? ResolveDisplayText(
        HmiControl control,
        Func<string, double?>? valueProvider)
    {
        ArgumentNullException.ThrowIfNull(control);

        var result = ResolveBindingResult(control, valueProvider, propertyName: null);
        if (result is null) return null;

        // 表达式结果为字符串（如三元 "high"/"low"）时直接显示
        if (result.Value.Type == ExpressionResultType.String)
            return result.Value.StringValue;

        return FormatValue(control.Type, result.Value.AsNumber());
    }

    /// <summary>
    /// 解析控件在预览模式下应显示的文本（支持按变量数据类型格式化）。
    /// 综合控件值显示配置（ValueDisplayType/ValueScaleFactor/ValuePrefix/ValueSuffix）。
    /// </summary>
    /// <param name="control">目标控件（只读引用，不会被修改）。</param>
    /// <param name="valueProvider">变量ID → 模拟值 的查找函数。null 表示未启用预览。</param>
    /// <param name="typeProvider">变量ID → 数据类型（"Bool"/"Int16"/"Float32"/"String" 等）的查找函数，用于 Auto 判断。</param>
    /// <param name="unitProvider">变量ID → 单位（如 "℃"/"%"）的查找函数；控件 ValueSuffix 为空时作为后缀兜底，可为 null。</param>
    public static string? ResolveDisplayText(
        HmiControl control,
        Func<string, double?>? valueProvider,
        Func<string, string?>? typeProvider,
        Func<string, string?>? unitProvider = null)
    {
        ArgumentNullException.ThrowIfNull(control);

        var result = ResolveBindingResult(control, valueProvider, propertyName: null);
        if (result is null) return null;

        // 字符串结果（表达式/字符串变量）：加前后缀后直接显示
        if (result.Value.Type == ExpressionResultType.String)
            return WrapText(control, result.Value.StringValue ?? "");

        var value = result.Value.AsNumber();
        var dataType = ResolveBoundDataType(control, typeProvider);
        var unit = ResolveBoundUnit(control, unitProvider);
        return FormatDisplayValue(control, value, dataType, unit);
    }

    /// <summary>
    /// 按指定属性名解析控件绑定变量对应的预览文本。
    /// 联动模型扩展 —— 支持控件多属性绑定不同变量（如 Value 绑定速度、Text 绑定名称）。
    /// </summary>
    public static string? ResolveDisplayText(
        HmiControl control,
        Func<string, double?>? valueProvider,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(control);

        var result = ResolveBindingResult(control, valueProvider, propertyName);
        if (result is null) return null;

        if (result.Value.Type == ExpressionResultType.String)
            return result.Value.StringValue;

        return FormatValue(control.Type, result.Value.AsNumber());
    }

    /// <summary>
    /// 解析绑定的求值结果（统一处理变量源与表达式源）。
    /// 表达式源由 ExpressionBindingEvaluator 求值，语法错误或表达式为空时回退 null。
    /// </summary>
    private static ExpressionResult? ResolveBindingResult(
        HmiControl control,
        Func<string, double?>? valueProvider,
        string? propertyName,
        bool allowLegacyFallback = true)
    {
        // 无提供器 → 非预览模式，回退到编辑态
        if (valueProvider is null) return null;

        // 定位绑定条目：指定属性优先，否则取主显示绑定
        VariableBindingData? binding = null;
        foreach (var b in control.VariableBindings)
        {
            if (b.Direction == BindingDirection.WriteOnly) continue;
            if (propertyName is null || string.Equals(b.PropertyName, propertyName, StringComparison.Ordinal))
            {
                binding = b;
                break;
            }
        }

        // 旧字段回退（无绑定集合时的单一绑定）
        if (binding is null)
        {
            // 旧字段只承载「主显示绑定」，视觉属性（颜色/可见性）不得走这条回退 ——
            // 否则控件一旦配了旧字段，请求 TextColor 就会把显示值当成颜色用。
            if (!allowLegacyFallback) return null;
            if (string.IsNullOrEmpty(control.VariableBindingId)) return null;
            binding = new VariableBindingData
            {
                PropertyName = propertyName ?? "Text",
                VariableId = control.VariableBindingId,
                Direction = BindingDirection.ReadOnly,
            };
        }

        // double? 提供器 → 表达式求值器所需解析器
        Func<string, ExpressionResult?> resolver = name =>
            valueProvider(name) is double v ? ExpressionResult.Number(v) : null;

        return ExpressionBindingEvaluator.EvaluateBinding(binding, resolver);
    }

    // ── 表达式驱动视觉属性（P1-1）────────────────────────────────────────
    //  颜色/可见性与「值」走同一条绑定解析路径（ExpressionBindingEvaluator），
    //  差别只在取值域与回退语义：视觉属性**不走旧字段回退**，且非预览模式一律返回 null，
    //  由调用方回退到静态配置 —— 编辑态显示的永远是设计值，与 WinCC/Ignition 的语义一致。

    /// <summary>
    /// 解析视觉属性绑定在预览模式下的原始求值结果。
    /// 返回 null 表示未启用预览、该属性无绑定、或表达式求值失败 —— 调用方应回退到静态配置值。
    /// </summary>
    /// <param name="control">目标控件（只读引用，不会被修改）。</param>
    /// <param name="valueProvider">变量ID → 模拟值 的查找函数。null 表示未启用预览。</param>
    /// <param name="propertyName">视觉属性名，取自 <see cref="BindingVisualProperty"/>。</param>
    public static ExpressionResult? ResolveVisualValue(
        HmiControl control,
        Func<string, double?>? valueProvider,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(control);
        return ResolveBindingResult(control, valueProvider, propertyName, allowLegacyFallback: false);
    }

    /// <summary>
    /// 解析表达式驱动的颜色，返回设计器颜色字符串（<c>#RRGGBB</c>）。
    /// null 表示该属性无绑定 / 求值失败 / 非预览模式 —— 调用方回退到控件静态颜色。
    /// 数值 → 颜色的换算由 <see cref="BindingVisualProperty.ColorFromNumber"/> 单点定义，
    /// 与生成代码里的 <c>lv_color_hex((uint32_t)(expr))</c> 同规则。
    /// </summary>
    /// <param name="control">目标控件（只读引用）。</param>
    /// <param name="valueProvider">变量ID → 模拟值 的查找函数。</param>
    /// <param name="propertyName">颜色属性名（TextColor / BgColor）。</param>
    public static string? ResolveVisualColor(
        HmiControl control,
        Func<string, double?>? valueProvider,
        string propertyName)
    {
        var result = ResolveVisualValue(control, valueProvider, propertyName);
        return result is null ? null : BindingVisualProperty.ColorFromNumber(result.Value.AsNumber());
    }

    /// <summary>
    /// 解析表达式驱动的可见性。null 表示该属性无绑定 / 求值失败 / 非预览模式 ——
    /// 调用方回退到 <see cref="HmiControl.IsVisible"/>。
    /// 判真与生成代码一致：非零即可见。
    /// </summary>
    /// <param name="control">目标控件（只读引用）。</param>
    /// <param name="valueProvider">变量ID → 模拟值 的查找函数。</param>
    public static bool? ResolveVisualVisibility(
        HmiControl control,
        Func<string, double?>? valueProvider)
    {
        var result = ResolveVisualValue(
            control, valueProvider, BindingVisualProperty.IsVisible.PropertyName);
        return result?.AsBoolean();
    }

    /// <summary>
    /// 按控件类型格式化模拟值为人可读字符串。
    /// 布尔类控件显示 ON/OFF；仪表/进度类显示一位小数；其余显示两位小数。
    /// </summary>
    public static string FormatValue(HmiControlType type, double value)
    {
        // NaN/Infinity 视为无效，由调用方回退
        if (double.IsNaN(value) || double.IsInfinity(value)) return "---";

        return type switch
        {
            // 布尔/状态指示类：非零视为 ON
            HmiControlType.Led
                or HmiControlType.Switch
                or HmiControlType.CheckBox => value != 0 ? "ON" : "OFF",

            // 仪表/旋钮/进度条：一位小数 + 单位占位
            HmiControlType.Gauge
                or HmiControlType.Bar
                or HmiControlType.Knob
                or HmiControlType.Tank => value.ToString("F1"),

            // 阀门：ON/OFF
            HmiControlType.Valve => value != 0 ? "ON" : "OFF",

            // 滑块/下拉：整数
            HmiControlType.Slider
                or HmiControlType.Dropdown => ((int)Math.Round(value)).ToString(),

            // 其余（Label/Text 等）：两位小数
            _ => value.ToString("F2")
        };
    }

    /// <summary>
    /// 按控件值显示配置格式化数值（值类型 / 缩放比例 / 前后缀）。
    /// 数值类型应用缩放：显示值 = 原始值 × ValueScaleFactor。
    ///
    /// 实现委托给 <see cref="BindingSemantics"/> —— 与 PC 模拟器、固件生成代码共用同一规则，
    /// 避免"设计器显示 12.34、设备显示 12.3"这类跨端漂移。
    /// </summary>
    /// <param name="control">目标控件（只读）。</param>
    /// <param name="rawValue">绑定解析出的原始数值。</param>
    /// <param name="variableDataType">绑定的变量数据类型（"Bool"/"Int16"/"Float32"/"String"），用于 Auto 判断，可为 null。</param>
    public static string FormatDisplayValue(HmiControl control, double rawValue, string? variableDataType, string? variableUnit = null)
        => BindingSemantics.For(control, variableDataType, variableUnit).FormatDisplay(rawValue);

    /// <summary>定位绑定所引用的变量 ID 并查询其数据类型（表达式绑定无法确定类型，返回 null）。</summary>
    private static string? ResolveBoundDataType(HmiControl control, Func<string, string?>? typeProvider)
    {
        if (typeProvider is null) return null;

        VariableBindingData? binding = null;
        foreach (var b in control.VariableBindings)
        {
            if (b.Direction == BindingDirection.WriteOnly) continue;
            binding = b;
            break;
        }

        if (binding is not null)
        {
            if (binding.Kind == BindingSourceKind.Expression) return null;
            return typeProvider(binding.VariableId);
        }

        return string.IsNullOrEmpty(control.VariableBindingId)
            ? null
            : typeProvider(control.VariableBindingId);
    }

    /// <summary>定位绑定所引用的变量单位（表达式绑定/无提供器返回 null）。</summary>
    private static string? ResolveBoundUnit(HmiControl control, Func<string, string?>? unitProvider)
    {
        if (unitProvider is null) return null;

        foreach (var b in control.VariableBindings)
        {
            if (b.Direction == BindingDirection.WriteOnly) continue;
            if (b.Kind == BindingSourceKind.Expression) return null;
            return unitProvider(b.VariableId);
        }

        return string.IsNullOrEmpty(control.VariableBindingId)
            ? null
            : unitProvider(control.VariableBindingId);
    }

    /// <summary>为显示文本添加前后缀。</summary>
    private static string WrapText(HmiControl control, string body)
    {
        var prefix = control.ValuePrefix;
        var suffix = control.ValueSuffix;
        if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix)) return body;
        return $"{prefix}{body}{suffix}";
    }

    /// <summary>
    /// 解析 Gauge/Bar 类控件在预览模式下的归一化进度（0..1），
    /// 用于驱动弧形/进度条渲染。
    /// 无可用值或量程非正时返回 null。
    /// </summary>
    /// <param name="control">目标控件（只读引用）。</param>
    /// <param name="valueProvider">变量ID → 模拟值 的查找函数。</param>
    /// <param name="min">量程下限（默认 0）。</param>
    /// <param name="max">量程上限（默认 100）。</param>
    public static double? ResolveProgress(
        HmiControl control,
        Func<string, double?>? valueProvider,
        double min = 0,
        double max = 100)
    {
        ArgumentNullException.ThrowIfNull(control);

        var result = ResolveBindingResult(control, valueProvider, propertyName: null);
        if (result is null || result.Value.Type == ExpressionResultType.String) return null;

        var value = result.Value.AsNumber();

        var range = max - min;
        if (range <= 0) return null;

        return Math.Clamp((value - min) / range, 0, 1);
    }

    /// <summary>
    /// 判定控件是否为预览模式下应显示模拟值的"显示型控件"。
    /// 输入型控件（Button/Keyboard 等）在预览模式下仍渲染原样（仅禁用交互）。
    /// </summary>
    public static bool IsDisplayControl(HmiControlType type) => type switch
    {
        HmiControlType.Label
            or HmiControlType.Gauge
            or HmiControlType.Bar
            or HmiControlType.Knob
            or HmiControlType.Led
            or HmiControlType.Slider
            or HmiControlType.CheckBox
            or HmiControlType.Switch
            or HmiControlType.Dropdown
            or HmiControlType.Tank
            or HmiControlType.Valve => true,
        _ => false
    };
}
