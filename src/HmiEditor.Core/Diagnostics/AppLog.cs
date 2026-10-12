using System;

namespace HmiEditor.Core.Diagnostics;

/// <summary>
/// 最小日志抽象 —— 编辑器各层（Core / CodeGen / Device / App）共用的诊断出口。
///
/// 为什么不用 Microsoft.Extensions.Logging：Core/CodeGen/Device 目前没有该依赖，
/// 引入会把日志框架泄漏到设备无关的核心库；这里只定义最小契约，
/// 由 App 在启动时安装具体实现（文件 + 控制台）。
/// </summary>
public interface IAppLogger
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message, Exception? exception = null);
    void Error(string message, Exception? exception = null);
}

/// <summary>空实现 —— 未安装日志器时的默认值，保证调用方永不为 null。</summary>
internal sealed class NullAppLogger : IAppLogger
{
    public static readonly NullAppLogger Instance = new();
    public void Debug(string message) { }
    public void Info(string message) { }
    public void Warn(string message, Exception? exception = null) { }
    public void Error(string message, Exception? exception = null) { }
}

/// <summary>
/// 全局日志门面 —— 静态入口，可在任何层调用：
/// <c>AppLog.Error("...", ex)</c>。App 启动时通过 <see cref="Current"/> 安装实现。
/// </summary>
public static class AppLog
{
    private static IAppLogger _current = NullAppLogger.Instance;

    /// <summary>当前日志器实现。默认空实现，App 启动时替换为文件日志器。</summary>
    public static IAppLogger Current
    {
        get => _current;
        set => _current = value ?? NullAppLogger.Instance;
    }

    public static void Debug(string message) => _current.Debug(message);
    public static void Info(string message) => _current.Info(message);
    public static void Warn(string message, Exception? exception = null) => _current.Warn(message, exception);
    public static void Error(string message, Exception? exception = null) => _current.Error(message, exception);

    /// <summary>记录异常并返回，便于 <c>catch (Exception ex) { return AppLog.Record(...); }</c> 风格。</summary>
    public static Exception Record(string message, Exception exception)
    {
        _current.Error(message, exception);
        return exception;
    }
}
