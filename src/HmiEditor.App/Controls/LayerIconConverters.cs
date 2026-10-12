using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HmiEditor.App.Controls;

/// <summary>层面板可见性图标：IsVisible=true 显示 👁，否则显示 🚫。</summary>
public sealed class VisibilityGlyphConverter : IValueConverter
{
    public static readonly VisibilityGlyphConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool visible && visible ? "👁" : "🚫";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>层面板锁定图标：IsLocked=false 显示 🔓，否则显示 🔒。</summary>
public sealed class LockGlyphConverter : IValueConverter
{
    public static readonly LockGlyphConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool locked && locked ? "🔒" : "🔓";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
