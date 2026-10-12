using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using HmiEditor.Core.Diagnostics;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Services;

/// <summary>
/// 最小文件日志器 —— 把编辑器运行期异常/告警落盘，供现场排障。
///
/// 位置：<c>%APPDATA%/zhishu-hmi/logs/app-yyyyMMdd.log</c>（与 settings.json/theme.json 同目录）。
/// 设计约束：
///   <list type="bullet">
///     <item>日志写入失败绝不抛出 —— 日志是诊断手段，不能反过来拖垮编辑器；</item>
///     <item>按天切分，单文件写入用锁串行化（UI 线程与后台回调都会调用）；</item>
///     <item>Error 同时输出到 stderr，便于从终端启动时直接看到。</item>
///   </list>
/// </summary>
public sealed class FileLogger : IAppLogger
{
    private readonly object _gate = new();
    private readonly string _logDirectory;
    private readonly string? _sessionId = Guid.NewGuid().ToString("N")[..8];

    public FileLogger(string logDirectory)
    {
        _logDirectory = logDirectory;
    }

    /// <summary>默认日志目录：应用数据根下的 logs。</summary>
    public static string DefaultLogDirectory => Path.Combine(AppPaths.DataRoot, "logs");

    public void Debug(string message) => Write("DEBUG", message, null);

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            var line = Format(level, message, exception);
            if (level == "ERROR") Console.Error.WriteLine(line);

            lock (_gate)
            {
                Directory.CreateDirectory(_logDirectory);
                var path = Path.Combine(
                    _logDirectory,
                    $"app-{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
                File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // 日志失败必须静默：绝不因写日志而影响编辑器主流程
        }
    }

    private string Format(string level, string message, Exception? exception)
    {
        var sb = new StringBuilder();
        sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        sb.Append(" [").Append(level).Append(']');
        sb.Append(" [").Append(_sessionId).Append(']');
        sb.Append(" [t").Append(Environment.CurrentManagedThreadId).Append("] ");
        sb.Append(message);
        if (exception is not null)
        {
            sb.AppendLine();
            sb.Append("    ").Append(exception.GetType().FullName).Append(": ").Append(exception.Message);
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                sb.AppendLine();
                sb.Append(exception.StackTrace);
            }
            if (exception.InnerException is not null)
            {
                sb.AppendLine();
                sb.Append("    -- inner: ").Append(exception.InnerException.GetType().FullName)
                  .Append(": ").Append(exception.InnerException.Message);
            }
        }
        return sb.ToString();
    }
}
