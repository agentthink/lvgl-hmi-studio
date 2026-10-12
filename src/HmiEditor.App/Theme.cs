using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using HmiEditor.App;

namespace HmiEditor.App;

/// <summary>
/// 从 App.axaml 全局资源中解析设计 token 画刷，供 code-behind 在动态创建控件时使用，
/// 避免在各文件散落硬编码 hex / Color.FromRgb。
/// </summary>
public static class Theme
{
    /// <summary>按资源 Key 解析画刷；缺失时返回透明画刷以避免空引用抖动。</summary>
    public static IBrush Brush(string key)
    {
        if (Application.Current?.Resources != null &&
            Application.Current.Resources.TryGetResource(key, null, out var value) &&
            value is IBrush brush)
        {
            return brush;
        }
        // ⚠ 必须用**不可变**画刷（阶段 295）：`new SolidColorBrush(...)` 是 AvaloniaObject，
        //    Dispatcher 尚未建立时会在构造中把它**抢到当前线程**（实测：派发之外 `null → 调用线程`）。
        //    本回退会被**静态字段初始化器**在任意线程上走到（例如 `AssetRow.OrphanBrush`）⇒
        //    用可变画刷就是一个可复现的"抢 Dispatcher"入口（登记 #47 的一条根因）。
        return new ImmutableSolidColorBrush(Colors.Transparent);
    }

    /// <summary>按资源 Key 解析颜色；缺失时返回透明色。</summary>
    public static Color Color(string key)
    {
        if (Application.Current?.Resources != null &&
            Application.Current.Resources.TryGetResource(key, null, out var value) &&
            value is Color c)
        {
            return c;
        }
        return Colors.Transparent;
    }

    // ── 常用 token 快捷访问 ──
    public static IBrush BgDark => Brush("HmiBgDarkBrush");
    public static IBrush BgPanel => Brush("HmiBgPanelBrush");
    public static IBrush BgPanelAlt => Brush("HmiBgPanelAltBrush");
    public static IBrush BgDeep => Brush("HmiBgDeepBrush");
    public static IBrush BgInput => Brush("HmiBgInputBrush");
    public static IBrush BgElevated => Brush("HmiBgElevatedBrush");
    public static IBrush BgSelected => Brush("HmiBgSelectedBrush");
    public static IBrush BgWindow => Brush("HmiBgWindowBrush");
    public static IBrush Border => Brush("HmiBorderBrush");
    public static IBrush InputBorder => Brush("HmiInputBorderBrush");
    public static IBrush Accent => Brush("HmiAccentBrush");
    public static IBrush AccentHover => Brush("HmiAccentHoverBrush");
    public static IBrush TextPrimary => Brush("HmiTextPrimaryBrush");
    public static IBrush TextPrimarySoft => Brush("HmiTextPrimarySoftBrush");
    public static IBrush TextSecondary => Brush("HmiTextSecondaryBrush");
    public static IBrush TextMuted => Brush("HmiTextMutedBrush");
    public static IBrush TextHint => Brush("HmiTextHintBrush");
    public static IBrush Link => Brush("HmiLinkBrush");
    public static IBrush Success => Brush("HmiSuccessBrush");
    public static IBrush Warning => Brush("HmiWarningBrush");
    public static IBrush Danger => Brush("HmiDangerBrush");

    // ── 模板分类语义色 ──
    public static IBrush CatIndustrial => Brush("HmiCatIndustrialBrush");
    public static IBrush CatPiping => Brush("HmiCatPipingBrush");
    public static IBrush CatInstrument => Brush("HmiCatInstrumentBrush");
    public static IBrush CatSafety => Brush("HmiCatSafetyBrush");
    public static IBrush CatGeneral => Brush("HmiCatGeneralBrush");
}

/// <summary>
/// 领域/业务语义色集中定义（严重级别、连接状态、校验状态、脚本事件徽章等）。
/// 不属主题 token（避免污染主题），但集中一处以便统一维护。
/// </summary>
public static class UiPalette
{
    // ── 报警严重级别 ──
    public static readonly Color SeverityInfo = Avalonia.Media.Color.FromRgb(59, 130, 246);
    public static readonly Color SeverityWarning = Avalonia.Media.Color.FromRgb(234, 179, 8);
    public static readonly Color SeverityCritical = Avalonia.Media.Color.FromRgb(249, 115, 22);
    public static readonly Color SeverityEmergency = Avalonia.Media.Color.FromRgb(239, 68, 68);

    // ── 连接/运行状态 ──
    public static readonly Color Connected = Avalonia.Media.Color.FromRgb(22, 163, 74);
    public static readonly Color ConnectedBright = Avalonia.Media.Color.FromRgb(52, 211, 153);
    public static readonly Color Disconnected = Avalonia.Media.Color.FromRgb(239, 68, 68);
    public static readonly Color Running = Avalonia.Media.Color.FromRgb(5, 150, 105);
    public static readonly Color Stopped = Avalonia.Media.Color.FromRgb(220, 38, 38);

    // ── 校验/脚本状态 ──
    public static readonly Color ValidationOk = Avalonia.Media.Color.FromRgb(74, 222, 128);
    public static readonly Color ValidationError = Avalonia.Media.Color.FromRgb(248, 113, 113);
    public static readonly Color ValidationIdle = Avalonia.Media.Color.FromRgb(148, 163, 184);

    /// <summary>
    /// **警告**（非阻断）：语法没问题、但发现了需要用户知情的风险
    /// —— 脚本编辑器的「运行前检查」（引用了设备上可能不存在的库 / 字段）用它。
    /// 不能借用 <see cref="ValidationError"/>：那会让"待定"看起来像"错误"。
    /// </summary>
    public static readonly Color ValidationWarn = Avalonia.Media.Color.FromRgb(250, 204, 21);

    // ── 脚本事件徽章 ──
    public static readonly Color EventOnClick = Avalonia.Media.Color.FromRgb(96, 165, 250);
    public static readonly Color EventOnValueChanged = Avalonia.Media.Color.FromRgb(74, 222, 128);
    public static readonly Color EventTimer = Avalonia.Media.Color.FromRgb(251, 191, 36);
    public static readonly Color EventAlarm = Avalonia.Media.Color.FromRgb(248, 113, 113);
    public static readonly Color EventDefault = Avalonia.Media.Color.FromRgb(148, 163, 184);
}
