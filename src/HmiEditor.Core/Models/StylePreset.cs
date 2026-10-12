using System.Collections.Generic;

namespace HmiEditor.Core.Models;

/// <summary>预设样式模板 —— 一组预定义的主题色值。</summary>
public record StylePreset(string Name, string Description, HmiTheme Theme)
{
    private static HmiTheme MakeTheme(string name,
        string primary, string secondary,
        string surface, string background, string text,
        string textSecondary, string border,
        string success, string warning, string error,
        int fontSize = 14, int cornerRadius = 6, int borderWidth = 1) => new()
    {
        Name = name,
        PrimaryColor = primary,
        SecondaryColor = secondary,
        SurfaceColor = surface,
        BackgroundColor = background,
        TextColor = text,
        TextSecondaryColor = textSecondary,
        BorderColor = border,
        SuccessColor = success,
        WarningColor = warning,
        ErrorColor = error,
        FontSize = fontSize,
        CornerRadius = cornerRadius,
        BorderWidth = borderWidth
    };

    public static IReadOnlyList<StylePreset> DefaultPresets { get; } = new List<StylePreset>
    {
        new("工业灰", "深色工业风格，默认主题", MakeTheme(
            "工业灰",
            "#2563eb", "#059669",
            "#1a2332", "#131c31", "#e2e8f0",
            "#7c8aa0", "#334155",
            "#22c55e", "#f59e0b", "#ef4444")),

        new("海洋蓝", "蓝色调，沉稳专业", MakeTheme(
            "海洋蓝",
            "#0284c7", "#0ea5e9",
            "#0c2233", "#082139", "#e0f2fe",
            "#7cc5e7", "#1e3a5f",
            "#10b981", "#f59e0b", "#ef4444",
            cornerRadius: 8)),

        new("高对比度", "浅色背景，深色文字，适合光线强的环境", MakeTheme(
            "高对比度",
            "#1d4ed8", "#059669",
            "#ffffff", "#f8fafc", "#0f172a",
            "#64748b", "#cbd5e1",
            "#16a34a", "#d97706", "#dc2626",
            fontSize: 16, borderWidth: 2)),

        new("暗夜绿", "深绿色调，护眼风格", MakeTheme(
            "暗夜绿",
            "#22c55e", "#10b981",
            "#0d1f12", "#0a1a0e", "#dcfce7",
            "#6ee7b7", "#166534",
            "#4ade80", "#f59e0b", "#ef4444")),

        new("暖色调", "橙色/琥珀色系，工业复古感", MakeTheme(
            "暖色调",
            "#ea580c", "#d97706",
            "#2d1f12", "#1c130b", "#fef3c7",
            "#d4a373", "#92400e",
            "#22c55e", "#f59e0b", "#ef4444",
            cornerRadius: 4)),
    };
}
