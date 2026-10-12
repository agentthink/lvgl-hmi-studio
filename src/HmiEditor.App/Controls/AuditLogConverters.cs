using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HmiEditor.App.Resources;
using HmiEditor.App.Views;
using HmiEditor.Core.Models.User;

namespace HmiEditor.App.Controls;

/// <summary>将 AuditAction 映射为本地化显示名。</summary>
public sealed class AuditActionNameConverter : IValueConverter
{
    public static readonly AuditActionNameConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is AuditAction action ? UserManagerView.ActionDisplayName(action) : "";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>将 UTC DateTime 转本地时间并按 MM-dd HH:mm:ss 格式化。</summary>
public sealed class AuditTimestampConverter : IValueConverter
{
    public static readonly AuditTimestampConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime dt ? dt.ToLocalTime().ToString("MM-dd HH:mm:ss") : "";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>将审计结果 Success 布尔值映射为本地化文本（"成功"/"失败"）。</summary>
public sealed class AuditResultTextConverter : IValueConverter
{
    public static readonly AuditResultTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool ok && ok
            ? AppLocalization.Service["gen.success"]
            : AppLocalization.Service["gen.failed"];

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
