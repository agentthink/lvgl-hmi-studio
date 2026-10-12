using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using HmiEditor.App.Resources;

namespace HmiEditor.App.Services;

/// <summary>
/// 应用主题服务 —— 管理内置主题的选择、切换与持久化。
/// 通过直接修改 Application.Resources 中的 SolidColorBrush.Color 实现主题切换，
/// 利用 SolidColorBrush 的属性变更通知自动刷新所有引用，切换无闪烁、无需重建视觉树。
/// </summary>
public sealed class AppThemeService
{
    /// <summary>内置主题标识符（不依赖 Avalonia.ThemeVariant）。</summary>
    public enum HmiTheme { Dark, Light, Ocean }

    private readonly string _persistPath;
    private HmiTheme _current;
    private string? _customAccent;

    /// <summary>
    /// 自定义强调色（用户通过设置覆盖主题默认强调色）。
    /// 为 null 时回退到主题内置强调色。
    /// </summary>
    public string? CustomAccent
    {
        get => _customAccent;
        set
        {
            _customAccent = value;
            Persist();
        }
    }

    /// <summary>当前主题的基础色值表（不含自定义强调色）。</summary>
    private Dictionary<string, string> BasePalette => _palettes[_current];

    /// <summary>
    /// 当前生效的色值表（含自定义强调色覆盖）。
    /// </summary>
    private Dictionary<string, string> EffectivePalette
    {
        get
        {
            var palette = new Dictionary<string, string>(BasePalette);
            if (!string.IsNullOrEmpty(_customAccent))
            {
                palette["HmiAccent"] = _customAccent;
                palette["HmiAccentHover"] = Darken(_customAccent, 0.15);
            }
            return palette;
        }
    }

    // 各主题的色值定义（仅保留与 UI 强相关的颜色 token）
    private static readonly Dictionary<HmiTheme, Dictionary<string, string>> _palettes = new()
    {
        [HmiTheme.Dark] = new()
        {
            ["HmiBgDark"]            = "#0f1925",
            ["HmiBgPanel"]           = "#1a2332",
            ["HmiBgPanelAlt"]        = "#111827",
            ["HmiBorder"]            = "#2a3a55",
            ["HmiAccent"]            = "#3b82f6",
            ["HmiAccentHover"]       = "#2563eb",
            ["HmiTextPrimary"]       = "#e2e8f0",
            ["HmiTextSecondary"]     = "#94a3b8",
            ["HmiTextMuted"]         = "#64748b",
            ["HmiSuccess"]           = "#22c55e",
            ["HmiWarning"]           = "#f59e0b",
            ["HmiDanger"]            = "#ef4444",
            ["HmiBgWindow"]          = "#131c31",
            ["HmiBgDeep"]            = "#0f172a",
            ["HmiBgInput"]           = "#151e30",
            ["HmiBgElevated"]        = "#1e293b",
            ["HmiBgSelected"]        = "#1e3a5f",
            ["HmiInputBorder"]       = "#334155",
            ["HmiTextHint"]          = "#7c8aa0",
            ["HmiTextPrimarySoft"]   = "#cbd5e1",
            ["HmiLink"]              = "#38bdf8",
            ["HmiBgCard"]            = "#1e2c42",   // 卡片/属性分组背景（比面板亮一档）
            ["HmiSuccessDeep"]       = "#065f46",   // 成功动作按钮背景
            ["HmiNeutralDeep"]       = "#4b5563",   // 中性/取消按钮背景
            ["HmiDangerDeep"]        = "#4a1c1c",   // 危险动作按钮背景
            ["HmiWindowBorder"]      = "#4b5e82",   // 窗口轮廓边框（与 HmiBgWindow 高对比）
            ["HmiWindowTitleBg"]     = "#182238",   // 窗口标题栏底色（略亮于窗口底）
        },
        [HmiTheme.Light] = new()
        {
            // 浅色主题视觉优化（参考 M3/Apple HIG/Fluent/Ant Design）：
            // - 避免大面积纯白刺眼，底层背景改用柔和冷灰
            // - 文字用柔和深蓝黑（非纯黑 #000），降低长时间观看疲劳
            // - 层次色阶：panel(#fff) > elevated(#f4f6f9) > panelAlt(#edf1f5) > deep(#e8edf2)
            // - 边框/输入框边框柔化，弱文本对比度 ≥ 4.5:1
            // - 所有背景 token 均为浅色，确保 HmiTextPrimary(#1c2735) 等深色文字对比度 ≥ 4.5:1
            ["HmiBgDark"]            = "#f2f4f7",   // 应用最底背景（冷灰，替代纯白）
            ["HmiBgPanel"]           = "#ffffff",   // 内容面板（最高层级，保持白）
            ["HmiBgPanelAlt"]        = "#edf1f5",   // 菜单栏/状态栏（与内容区分更清晰）
            ["HmiBorder"]            = "#d9e0e8",   // 柔和边框（原 #cbd5e1 过重）
            ["HmiAccent"]            = "#2563eb",   // 品牌蓝（保持）
            ["HmiAccentHover"]       = "#1d4ed8",   // 品牌蓝悬停（保持）
            ["HmiTextPrimary"]       = "#1c2735",   // 柔和深蓝黑主文本（原近黑 #0f172a 生硬）
            ["HmiTextSecondary"]     = "#3d4a5c",   // 次文本（柔和）
            ["HmiTextMuted"]         = "#5d6b7c",   // 弱文本（对比度提至 ≥4.5:1）
            ["HmiSuccess"]           = "#15803d",
            ["HmiWarning"]           = "#b45309",
            ["HmiDanger"]            = "#b91c1c",
            ["HmiBgWindow"]          = "#f7f9fb",   // 对话框底色（微灰，与面板分层）
            ["HmiBgDeep"]            = "#e8edf2",   // 树/深层背景（层次更明显）
            ["HmiBgInput"]           = "#fbfcfd",   // 输入框（微灰柔和，不刺眼）
            ["HmiBgElevated"]        = "#f4f6f9",   // 侧边栏/悬浮层（浅灰区分，原 #e2e8f0 过深）
            ["HmiBgSelected"]        = "#e3edff",   // 选中项（柔和浅蓝，保证 Muted 文字 ≥4.5:1，原 #d7e6fb 仅 4.30:1）
            ["HmiInputBorder"]       = "#b7c1cc",   // 输入框边框（原 #94a3b8 过重）
            ["HmiTextHint"]          = "#64748b",   // 占位/空态提示（原 #8492a3 仅 3.17:1，加深至 4.76:1）
            ["HmiTextPrimarySoft"]   = "#2d3a4c",   // 软正文
            ["HmiLink"]              = "#0369a1",   // 链接（加深保证浅底可读）
            ["HmiBgCard"]            = "#eef1f5",   // 卡片/属性分组背景（浅灰，与白色面板区分；原缺失导致沿用深色 #1e2c42）
            ["HmiSuccessDeep"]       = "#dcfce7",   // 成功动作按钮背景（浅绿，深色文字可读；原缺失沿用深绿+深字 1.96:1）
            ["HmiNeutralDeep"]       = "#e8eaee",   // 中性/取消按钮背景（浅灰）
            ["HmiDangerDeep"]        = "#fee2e2",   // 危险动作按钮背景（浅红，Danger 文字 5.30:1）
            ["HmiWindowBorder"]      = "#b7c1cc",   // 窗口轮廓边框（柔和灰，浅底上清晰可见）
            ["HmiWindowTitleBg"]     = "#f0f3f7",   // 窗口标题栏底色（微灰，与白色内容区分）
        },
        [HmiTheme.Ocean] = new()
        {
            ["HmiBgDark"]            = "#0a1f33",
            ["HmiBgPanel"]           = "#103050",
            ["HmiBgPanelAlt"]        = "#0d2540",
            ["HmiBorder"]            = "#1e4a78",
            ["HmiAccent"]            = "#06b6d4",
            ["HmiAccentHover"]       = "#0891b2",
            ["HmiTextPrimary"]       = "#e0f2fe",
            ["HmiTextSecondary"]     = "#7dd3fc",
            ["HmiTextMuted"]         = "#64748b",
            ["HmiSuccess"]           = "#10b981",
            ["HmiWarning"]           = "#fbbf24",
            ["HmiDanger"]            = "#f87171",
            ["HmiBgWindow"]          = "#0e2540",
            ["HmiBgDeep"]            = "#082030",
            ["HmiBgInput"]           = "#0a2640",
            ["HmiBgElevated"]        = "#143b5e",
            ["HmiBgSelected"]        = "#155e75",
            ["HmiInputBorder"]       = "#1e4a78",
            ["HmiTextHint"]          = "#64748b",
            ["HmiTextPrimarySoft"]   = "#bae6fd",
            ["HmiLink"]              = "#22d3ee",
            ["HmiBgCard"]            = "#163e60",   // 卡片/属性分组背景（海洋蓝，比面板亮一档）
            ["HmiSuccessDeep"]       = "#0f6b52",   // 成功动作按钮背景
            ["HmiNeutralDeep"]       = "#4b5563",   // 中性/取消按钮背景
            ["HmiDangerDeep"]        = "#5c1f2e",   // 危险动作按钮背景
            ["HmiWindowBorder"]      = "#2a5a8f",   // 窗口轮廓边框（海洋蓝，与 HmiBgWindow 高对比）
            ["HmiWindowTitleBg"]     = "#0f2c4a",   // 窗口标题栏底色（略亮于窗口底）
        },
    };

    public AppThemeService(string persistPath)
    {
        _persistPath = persistPath;
        _current = HmiTheme.Dark;
    }

    /// <summary>当前激活的主题。</summary>
    public HmiTheme Current => _current;

    /// <summary>当前主题的显示名称。</summary>
    public string CurrentName => _current switch
    {
        HmiTheme.Light => "Light High Contrast",
        HmiTheme.Ocean => "Ocean Blue",
        _ => "Dark Industrial",
    };

    /// <summary>所有可用的内置主题。</summary>
    public static IReadOnlyList<ThemeInfo> AvailableThemes { get; } = new List<ThemeInfo>
    {
        new("Dark",  "Dark Industrial",     Lang.ThemeDescDark),
        new("Light", "Light High Contrast", Lang.ThemeDescLight),
        new("Ocean", "Ocean Blue",          Lang.ThemeDescOcean),
    };

    /// <summary>
    /// 取某主题那句说明的**当前语言文案**（<paramref name="key"/> 是 <see cref="ThemeInfo.Key"/>）。
    ///
    /// <para><b>解析放在这里、不放在静态表里</b>：静态初始化只跑一次，放已解析文案会把语言冻结住。
    /// 提成公开方法也是为了**可测** —— 直接把 <c>DescriptionKey</c> 当提示文案用
    /// （用户会看到 <c>theme.desc.dark</c>）不会有任何编译期或运行期报错。</para>
    /// </summary>
    public static string DescribeTheme(string key) =>
        AppLocalization.Service[
            AvailableThemes.Single(t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).DescriptionKey];

    /// <summary>
    /// 切换主题：直接更新 Application.Resources 中所有 SolidColorBrush 的 Color。
    /// Avalonia 会自动通知所有引用这些 brush 的控件，实现无闪烁切换。
    /// </summary>
    public void SetTheme(HmiTheme theme)
    {
        ApplyPalette(theme, _customAccent);
        _current = theme;
        SetThemeVariant(theme);
        Persist();
    }

    /// <summary>切换主题并可选设置自定义强调色。</summary>
    public void SetTheme(HmiTheme theme, string? customAccent)
    {
        _customAccent = customAccent;
        ApplyPalette(theme, _customAccent);
        _current = theme;
        SetThemeVariant(theme);
        Persist();
    }

    /// <summary>仅更新自定义强调色（不切换主题）。</summary>
    public void SetCustomAccent(string? hex)
    {
        _customAccent = hex;
        ApplyPalette(_current, _customAccent);
        Persist();
    }

    /// <summary>按名称切换主题（不区分大小写）。</summary>
    public void SetThemeByName(string name)
    {
        foreach (var t in AvailableThemes)
        {
            if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<HmiTheme>(t.Key, true, out var theme))
                {
                    SetTheme(theme);
                }
                return;
            }
        }
    }

    /// <summary>
    /// 循环切换到下一个主题（Dark → Ocean → Light → Dark）。
    /// 用于快捷键 Ctrl+J 快速切换主题。
    /// </summary>
    public HmiTheme CycleTheme()
    {
        var next = _current switch
        {
            HmiTheme.Dark => HmiTheme.Ocean,
            HmiTheme.Ocean => HmiTheme.Light,
            HmiTheme.Light => HmiTheme.Dark,
            _ => HmiTheme.Dark,
        };
        SetTheme(next);
        return next;
    }

    /// <summary>从持久化文件恢复主题。文件不存在或格式错误时回退到 Dark。</summary>
    public void Restore()
    {
        try
        {
            if (!File.Exists(_persistPath)) return;
            var json = File.ReadAllText(_persistPath);
            var obj = JsonSerializer.Deserialize<ThemePersist>(json);
            if (obj == null) return;

            if (!string.IsNullOrEmpty(obj.Theme) &&
                Enum.TryParse<HmiTheme>(obj.Theme, true, out var theme))
            {
                _customAccent = obj.CustomAccent;
                ApplyPalette(theme, _customAccent);
                _current = theme;
                SetThemeVariant(theme);
            }
        }
        catch
        {
            // 持久化损坏时静默回退
        }
    }

    /// <summary>
    /// 将指定主题的色值表应用到 Application.Resources 中的 SolidColorBrush 实例。
    /// 通过修改现有 brush 的 Color 属性触发属性变更通知，所有引用 brush 的 UI 自动更新。
    /// </summary>
    private static void ApplyPalette(HmiTheme theme, string? customAccent)
    {
        var palette = new Dictionary<string, string>(_palettes[theme]);
        if (!string.IsNullOrEmpty(customAccent))
        {
            palette["HmiAccent"] = customAccent;
            palette["HmiAccentHover"] = Darken(customAccent, 0.15);
        }

        var app = Application.Current;
        if (app is null) return;

        var resources = app.Resources;

        foreach (var (colorKey, hex) in palette)
        {
            // 更新 Color 资源（SolidColorBrush 内部通过 StaticResource 引用它）
            if (resources.ContainsKey(colorKey) && resources[colorKey] is Color existing)
            {
                resources[colorKey] = Color.Parse(hex);
            }

            // 直接更新 SolidColorBrush 的 Color 属性 —— 这是无闪烁切换的关键
            var brushKey = colorKey + "Brush";
            if (resources.ContainsKey(brushKey) && resources[brushKey] is SolidColorBrush brush)
            {
                brush.Color = Color.Parse(hex);
            }
        }

        // ═══ 同步更新 Avalonia 系统级菜单资源 ═══
        // 同时更新 RequestedThemeVariant，确保 FluentTheme 内部模板（MenuBase/ContextMenu/Popup）
        // 使用的 ThemeDictionary 资源与实际主题一致，避免浅色主题下菜单仍为深色。
        UpdateSystemMenuResources(resources, theme);
    }

    /// <summary>
    /// 更新 Avalonia 系统级菜单相关资源和 Dock 面板资源。
    /// 这些资源被 FluentTheme 内部模板和 DockTheme 使用，不会随 ThemeDictionaries 自动切换。
    /// </summary>
    private static void UpdateSystemMenuResources(IResourceDictionary resources, HmiTheme theme)
    {
        // 菜单背景：使用 HmiBgPanel 的值（与 ApplyPalette 中 HmiBgPanelBrush 一致）
        var panelBg = GetColor(theme, "HmiBgPanel");
        var panelBorder = GetColor(theme, "HmiBorder");
        var textPrimary = GetColor(theme, "HmiTextPrimary");
        var selectedBg = GetColor(theme, "HmiBgSelected");
        var mutedText = GetColor(theme, "HmiTextMuted");

        // 直接在主资源字典中创建/更新这些系统资源
        SetOrUpdateBrush(resources, "MenuBackgroundBrush", panelBg);
        SetOrUpdateBrush(resources, "MenuBorderBrush", panelBorder);
        SetOrUpdateBrush(resources, "MenuItemForegroundBrush", textPrimary);
        SetOrUpdateBrush(resources, "MenuItemBackgroundBrush", panelBg);
        SetOrUpdateBrush(resources, "MenuItemPointerOverForegroundBrush", textPrimary);
        SetOrUpdateBrush(resources, "MenuItemPointerOverBackgroundBrush", selectedBg);
        SetOrUpdateBrush(resources, "MenuItemSelectedForegroundBrush", textPrimary);
        SetOrUpdateBrush(resources, "MenuItemSelectedBackgroundBrush", selectedBg);
        SetOrUpdateBrush(resources, "MenuItemDisabledForegroundBrush", mutedText);
        SetOrUpdateBrush(resources, "ContextMenuBackgroundBrush", panelBg);
        SetOrUpdateBrush(resources, "ContextMenuBorderBrush", panelBorder);
        SetOrUpdateBrush(resources, "ContextMenuForegroundBrush", textPrimary);
        SetOrUpdateBrush(resources, "SystemControlMenuBackgroundBrush", panelBg);
        SetOrUpdateBrush(resources, "SystemControlPopupBackgroundBrush", panelBg);

        // 同步更新 Color 版本的系统资源（某些 FluentTheme 模板直接引用 Color）
        SetOrUpdateColor(resources, "SystemControlMenuBackground", panelBg);
        SetOrUpdateColor(resources, "SystemControlPopupBackground", panelBg);

        // ═══ Dock 面板资源同步（这些资源在 ThemeDictionaries 中定义，也需要随主题更新）═══
        var panelAlt = GetColor(theme, "HmiBgPanelAlt");
        SetOrUpdateBrush(resources, "ToolChromeBorderBrush", panelBorder);
        SetOrUpdateBrush(resources, "DocumentChromeBorderBrush", panelBorder);
        SetOrUpdateBrush(resources, "ToolContentBorderBrush", panelBorder);
        SetOrUpdateBrush(resources, "DocumentContentBorderBrush", panelBorder);
        SetOrUpdateBrush(resources, "ToolChromeBackgroundBrush", panelAlt);
        SetOrUpdateBrush(resources, "DocumentChromeBackgroundBrush", panelAlt);
    }

    /// <summary>将 RequestedThemeVariant 与实际主题同步，使 FluentTheme 的 ThemeDictionary 正确切换。</summary>
    private static void SetThemeVariant(HmiTheme theme)
    {
        if (Application.Current is null) return;
        var variant = theme switch
        {
            HmiTheme.Light => ThemeVariant.Light,
            HmiTheme.Ocean => ThemeVariant.Dark,
            _              => ThemeVariant.Dark,
        };
        Application.Current.RequestedThemeVariant = variant;
    }

    /// <summary>
    /// 从主题色板获取指定 key 的色值。
    /// </summary>
    private static string GetColor(HmiTheme theme, string key)
    {
        return _palettes[theme].TryGetValue(key, out var hex) ? hex : "#000000";
    }

    /// <summary>
    /// 设置或更新资源字典中的 SolidColorBrush。
    /// 如果资源已存在且是 SolidColorBrush，直接修改 Color；否则创建新的。
    /// </summary>
    private static void SetOrUpdateBrush(IResourceDictionary resources, string key, string hex)
    {
        if (resources.ContainsKey(key) && resources[key] is SolidColorBrush existing)
        {
            existing.Color = Color.Parse(hex);
        }
        else
        {
            resources[key] = new SolidColorBrush(Color.Parse(hex));
        }
    }

    /// <summary>
    /// 设置或更新资源字典中的 Color 资源。
    /// 如果资源已存在且是 Color，直接替换；否则创建新的。
    /// </summary>
    private static void SetOrUpdateColor(IResourceDictionary resources, string key, string hex)
    {
        resources[key] = Color.Parse(hex);
    }

    /// <summary>将 HEX 色值按因子变暗，用于生成悬停态。</summary>
    private static string Darken(string hex, double factor)
    {
        var c = Color.Parse(hex);
        var r = (byte)Math.Max(0, Math.Min(255, (int)(c.R * (1 - factor))));
        var g = (byte)Math.Max(0, Math.Min(255, (int)(c.G * (1 - factor))));
        var b = (byte)Math.Max(0, Math.Min(255, (int)(c.B * (1 - factor))));
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private void Persist()
    {
        try
        {
            var obj = new ThemePersist { Theme = _current.ToString(), CustomAccent = _customAccent };
            var json = JsonSerializer.Serialize(obj);
            File.WriteAllText(_persistPath, json);
        }
        catch { /* 持久化失败不应影响功能 */ }
    }

    private sealed class ThemePersist
    {
        public string? Theme { get; set; }
        public string? CustomAccent { get; set; }
    }
}

/// <summary>主题的描述信息。</summary>
/// <summary>
/// 内置主题的描述：<paramref name="Key"/> 是枚举名（兼作持久化值），<paramref name="Name"/> 是英文名，
/// <paramref name="DescriptionKey"/> 是"一句话说明"的**本地化键**。
///
/// <para><b>第三个字段为什么是键而不是文案</b>：本表是 <c>static</c> 字段、静态初始化只跑一次 ——
/// 在这里放已解析的文案会把**语言冻结在首次访问那一刻**（与
/// <c>ConfigFieldInventory</c> 踩过的是同一个坑）。解析放在显示时（设置对话框的悬停提示）。</para>
/// </summary>
public readonly record struct ThemeInfo(string Key, string Name, string DescriptionKey);
