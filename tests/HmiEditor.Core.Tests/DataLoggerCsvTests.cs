using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **历史数据 CSV（设备列序）判据**（阶段 176）：把原本"零调用"的 <see cref="DataLogger.ExportCsv"/>
/// 接成历史报表的 CSV 形态 —— 判据钉死它到底长什么样（表头写明时间基、分隔符取配置、行尾 <c>\n</c>、
/// 值 <c>F3</c>、BOM 在字节形态里）。
/// </summary>
public class DataLoggerCsvTests
{
    private static DataLogger Logger(string delimiter, params string[] vars)
    {
        var logger = new DataLogger(new DataLogConfig
        {
            SampleIntervalMs = 1000,
            MaxRecords = 100,
            VariableIds = vars.ToList(),
            CsvDelimiter = delimiter,
            IsEnabled = false,
        });
        return logger;
    }

    private static readonly DateTime T0 = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ExportCsv_HeaderNamesTheTimeBase_AndUsesConfiguredDelimiter()
    {
        using var logger = Logger(";", "Temp", "Pressure");
        logger.AddSample(new Dictionary<string, double> { ["Temp"] = 1.5, ["Pressure"] = 2.25 });

        var lines = logger.ExportCsv().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // 表头**写明时间基**：设备是 lv_tick_get()（开机时长），编辑器是 UTC 墙钟 ⇒ 不冒充 timestamp_ms
        Assert.Equal("timestamp_utc_ms;Temp;Pressure", lines[0]);
        Assert.Equal(2, lines.Length);

        var cells = lines[1].Split(';');
        Assert.Equal(3, cells.Length);
        Assert.True(long.TryParse(cells[0], out _), $"第一列应当是 epoch 毫秒整数：{cells[0]}");
        Assert.Equal("1.500", cells[1]);   // %.3f 口径（与设备一致）
        Assert.Equal("2.250", cells[2]);

        // 行尾是 \n（与设备一致），不是平台相关的 AppendLine
        Assert.DoesNotContain("\r", logger.ExportCsv(), StringComparison.Ordinal);
    }

    /// <summary>空分隔符 ⇒ 回落逗号（否则表头会粘成一列）。</summary>
    [Fact]
    public void ExportCsv_EmptyDelimiter_FallsBackToComma()
    {
        using var logger = Logger("", "A");
        logger.AddSample(new Dictionary<string, double> { ["A"] = 1 });

        Assert.StartsWith("timestamp_utc_ms,A", logger.ExportCsv(), StringComparison.Ordinal);
    }

    /// <summary>字节形态带 UTF-8 BOM（Excel 打开 CJK 需要）；空数据 ⇒ 空数组（**不是**只有一个 BOM）。</summary>
    [Fact]
    public void ExportCsvBytes_PrependsBom_ButEmptyStaysEmpty()
    {
        using var logger = Logger(",", "A");
        Assert.Empty(logger.ExportCsvBytes());     // 没有采样 ⇒ 空，别写一个 3 字节的 BOM 出来

        logger.AddSample(new Dictionary<string, double> { ["A"] = 1 });
        var bytes = logger.ExportCsvBytes();
        Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "字节形态没有 UTF-8 BOM");
        Assert.Equal(logger.ExportCsv(), System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    /// <summary>时间列是 **UTC epoch 毫秒** —— 判据用固定时刻核对（不是"看着像整数"）。</summary>
    [Fact]
    public void ExportCsv_TimeColumnIsUtcEpochMilliseconds()
    {
        using var logger = Logger(",", "A");
        logger.AddSample(new Dictionary<string, double> { ["A"] = 1 });
        var samples = logger.GetAllSamples();
        var expected = new DateTimeOffset(DateTime.SpecifyKind(samples[0].Timestamp, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        var line = logger.ExportCsv().Split('\n', StringSplitOptions.RemoveEmptyEntries)[1];
        Assert.Equal(expected.ToString(), line.Split(',')[0]);
    }

    [Fact]
    public void ExportCsv_IsDeterministic()
    {
        using var logger = Logger(",", "A");
        logger.AddSample(new Dictionary<string, double> { ["A"] = 1 });
        Assert.Equal(logger.ExportCsv(), logger.ExportCsv());
    }
}
