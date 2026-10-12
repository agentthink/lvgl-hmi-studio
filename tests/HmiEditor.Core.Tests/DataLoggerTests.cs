using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

public class DataLoggerTests
{
    // ── TrendPen 模型测试 ──

    [Fact]
    public void TrendPen_DefaultValues_AreSane()
    {
        var pen = new TrendPen();
        Assert.Equal("#38bdf8", pen.Color);
        Assert.Equal(2, pen.LineWidth);
        Assert.True(pen.IsEnabled);
        Assert.False(pen.ShowPoints);
    }

    [Fact]
    public void TrendPen_CanBindVariable()
    {
        var pen = new TrendPen
        {
            Label = "温度曲线",
            VariableId = "var-temp",
            Color = "#ef4444",
            LineWidth = 3,
            YAxisMin = 0,
            YAxisMax = 200
        };
        Assert.Equal("温度曲线", pen.Label);
        Assert.Equal("var-temp", pen.VariableId);
        Assert.Equal("#ef4444", pen.Color);
        Assert.Equal(0, pen.YAxisMin);
        Assert.Equal(200, pen.YAxisMax);
    }

    // ── TrendConfig 模型测试 ──

    [Fact]
    public void TrendConfig_DefaultValues_AreSane()
    {
        var config = new TrendConfig();
        Assert.False(string.IsNullOrEmpty(config.Id));
        Assert.Equal(60000, config.TimeWindowMs);
        Assert.Equal(600, config.MaxPoints);
        Assert.True(config.ShowLegend);
        Assert.True(config.ShowGrid);
        Assert.True(config.IsRealtime);
        Assert.True(config.IsEnabled);
    }

    [Fact]
    public void TrendConfig_CanAddPens()
    {
        var config = new TrendConfig { Name = "主趋势图" };
        config.Pens.Add(new TrendPen { Label = "温度", VariableId = "v1", Color = "#ef4444" });
        config.Pens.Add(new TrendPen { Label = "压力", VariableId = "v2", Color = "#38bdf8" });

        Assert.Equal(2, config.Pens.Count);
        Assert.Equal("温度", config.Pens[0].Label);
        Assert.Equal("压力", config.Pens[1].Label);
    }

    // ── DataLogConfig 模型测试 ──

    [Fact]
    public void DataLogConfig_DefaultFullPolicy_IsOverwriteOldest()
    {
        var config = new DataLogConfig();
        Assert.Equal(LogFullPolicy.OverwriteOldest, config.FullPolicy);
        Assert.Equal(1000, config.SampleIntervalMs);
        Assert.Equal(86400, config.MaxRecords);
    }

    [Fact]
    public void DataLogConfig_CanSetVariables()
    {
        var config = new DataLogConfig
        {
            StoragePath = "/sd/log1.csv",
            CsvDelimiter = ";",
            EnableCompression = true
        };
        config.VariableIds.AddRange(new[] { "var-temp", "var-pressure", "var-flow" });

        Assert.Equal(3, config.VariableIds.Count);
        Assert.Contains("var-temp", config.VariableIds);
        Assert.Equal("/sd/log1.csv", config.StoragePath);
        Assert.Equal(";", config.CsvDelimiter);
        Assert.True(config.EnableCompression);
    }

    // ── TrendSample 模型测试 ──

    [Fact]
    public void TrendSample_CanStoreValues()
    {
        var sample = new TrendSample
        {
            Timestamp = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc)
        };
        sample.Values.AddRange(new[] { 25.5, 101.3, 1.8 });

        Assert.Equal(3, sample.Values.Count);
        Assert.Equal(25.5, sample.Values[0]);
        Assert.Equal(101.3, sample.Values[1]);
        Assert.False(sample.IsCompressed);
    }

    // ── DataLogger 引擎测试 ──

    [Fact]
    public void Logger_StartStop_UpdatesSampleCount()
    {
        using var logger = new DataLogger(new DataLogConfig
        {
            SampleIntervalMs = 50,
            MaxRecords = 100,
            VariableIds = { "var-x" }
        });

        logger.Start();

        // 等首个样本（Timer 首个回调虽在 0ms，但排在 ThreadPool 上，不能假设它已执行）
        TestWait.Until(() => logger.GetAllSamples().Count >= 1,
            "Start 后应至少采到 1 个样本");

        // 再等下一个样本 —— 证明是「按 SampleIntervalMs 周期采样」，
        // 而不只是首帧触发了一次
        var firstCount = logger.GetAllSamples().Count;
        TestWait.Until(() => logger.GetAllSamples().Count > firstCount,
            "采样应按 SampleIntervalMs 周期持续进行");

        logger.Stop();

        var samples = logger.GetAllSamples();
        Assert.True(samples.Count >= 2, $"Expected >= 2 samples, got {samples.Count}");
    }

    [Fact]
    public void Logger_AddSample_RecordsData()
    {
        using var logger = new DataLogger(new DataLogConfig
        {
            MaxRecords = 10,
            VariableIds = { "v1", "v2" }
        });

        logger.AddSample(new Dictionary<string, double>
        {
            ["v1"] = 100.0,
            ["v2"] = 200.0
        });
        logger.AddSample(new Dictionary<string, double>
        {
            ["v1"] = 150.0,
            ["v2"] = 250.0
        });

        var samples = logger.GetAllSamples();
        Assert.Equal(2, samples.Count);
        Assert.Equal(100.0, samples[0].Values[0], 1e-6);
        Assert.Equal(200.0, samples[0].Values[1], 1e-6);
        Assert.Equal(150.0, samples[1].Values[0], 1e-6);
    }

    [Fact]
    public void Logger_CircularBuffer_OverwritesWhenFull()
    {
        using var logger = new DataLogger(new DataLogConfig
        {
            MaxRecords = 5,
            VariableIds = { "v1" },
            FullPolicy = LogFullPolicy.OverwriteOldest
        });

        // Write 8 samples into a 5-slot buffer
        for (int i = 0; i < 8; i++)
        {
            logger.AddSample(new Dictionary<string, double> { ["v1"] = i * 10.0 });
        }

        Assert.True(logger.IsFull);
        Assert.Equal(5, logger.SampleCount);

        var samples = logger.GetAllSamples();
        // Oldest should be overwritten; newest 5 values: 30, 40, 50, 60, 70
        Assert.Equal(5, samples.Count);
        Assert.Equal(30.0, samples[0].Values[0], 1e-6);
        Assert.Equal(70.0, samples[4].Values[0], 1e-6);
    }

    [Fact]
    public void Logger_StopWhenFull_StopsLogging()
    {
        using var logger = new DataLogger(new DataLogConfig
        {
            MaxRecords = 3,
            VariableIds = { "v1" },
            FullPolicy = LogFullPolicy.StopWhenFull
        });

        for (int i = 0; i < 5; i++)
        {
            logger.AddSample(new Dictionary<string, double> { ["v1"] = i * 10.0 });
        }

        Assert.True(logger.IsFull);
        var samples = logger.GetAllSamples();
        // Should have only 3 samples (0, 10, 20)
        Assert.Equal(3, samples.Count);
        Assert.Equal(0.0, samples[0].Values[0], 1e-6);
        Assert.Equal(20.0, samples[2].Values[0], 1e-6);
    }

    [Fact]
    public void Logger_GetSamplesInWindow_FiltersCorrectly()
    {
        using var logger = new DataLogger(new DataLogConfig
        {
            MaxRecords = 10,
            VariableIds = { "v1" }
        });

        var t0 = new DateTime(2026, 7, 29, 10, 0, 0, DateTimeKind.Utc);

        // Simulate samples at known timestamps
        for (int i = 0; i < 5; i++)
        {
            // AddSample uses DateTime.UtcNow, so we test with GetRecentSamples instead
            logger.AddSample(new Dictionary<string, double> { ["v1"] = i * 10.0 });
        }

        // Use GetRecentSamples for deterministic testing
        var recent2 = logger.GetRecentSamples(2);
        Assert.Equal(2, recent2.Count);
        Assert.Equal(30.0, recent2[0].Values[0], 1e-6);
        Assert.Equal(40.0, recent2[1].Values[0], 1e-6);
    }

    [Fact]
    public void Logger_ExportCsv_ProducesValidFormat()
    {
        using var logger = new DataLogger(new DataLogConfig
        {
            MaxRecords = 3,
            VariableIds = { "var-temp", "var-pressure" }
        });

        logger.AddSample(new Dictionary<string, double>
        {
            ["var-temp"] = 25.5,
            ["var-pressure"] = 101.3
        });

        var csv = logger.ExportCsv();
        // 阶段 176：表头从 "Timestamp" 改成 **写明时间基** 的 "timestamp_utc_ms"
        //（设备侧是 lv_tick_get() 开机时长，编辑器是 UTC 墙钟 ⇒ 不冒充 timestamp_ms）
        Assert.Contains("timestamp_utc_ms", csv);
        Assert.Contains("var-temp", csv);
        Assert.Contains("var-pressure", csv);
        Assert.Contains("25.500", csv);
        Assert.Contains("101.300", csv);
    }

    [Fact]
    public void Logger_Clear_ResetsAllCounters()
    {
        using var logger = new DataLogger(new DataLogConfig
        {
            MaxRecords = 5,
            VariableIds = { "v1" }
        });

        logger.AddSample(new Dictionary<string, double> { ["v1"] = 1.0 });
        logger.AddSample(new Dictionary<string, double> { ["v1"] = 2.0 });
        logger.Clear();

        Assert.Equal(0, logger.SampleCount);
        Assert.False(logger.IsFull);
        Assert.Empty(logger.GetAllSamples());
        Assert.Empty(logger.ExportCsv());
    }

    [Fact]
    public void Logger_StopWhenFullEvent_Fires()
    {
        var fullFired = false;
        using var logger = new DataLogger(new DataLogConfig
        {
            MaxRecords = 2,
            VariableIds = { "v1" },
            FullPolicy = LogFullPolicy.OverwriteOldest
        });

        logger.BufferFull += (_, _) => fullFired = true;

        for (int i = 0; i < 3; i++)
        {
            logger.AddSample(new Dictionary<string, double> { ["v1"] = i * 10.0 });
        }

        Assert.True(fullFired);
    }

    [Fact]
    public void Logger_SampleAddedEvent_Fires()
    {
        var eventFired = false;
        using var logger = new DataLogger(new DataLogConfig
        {
            MaxRecords = 10,
            VariableIds = { "v1" }
        });

        logger.SampleAdded += (_, _) => eventFired = true;
        logger.AddSample(new Dictionary<string, double> { ["v1"] = 42.0 });

        Assert.True(eventFired);
    }

    [Fact]
    public void Logger_EmptyExport_ReturnsEmptyString()
    {
        using var logger = new DataLogger(new DataLogConfig
        {
            MaxRecords = 5,
            VariableIds = { "v1" }
        });

        var csv = logger.ExportCsv();
        Assert.Empty(csv);
    }

    [Fact]
    public void TrendSample_Compressed_FlagWorks()
    {
        var sample = new TrendSample { IsCompressed = true };
        Assert.True(sample.IsCompressed);
    }

    // ── 归一化：容量 / 采样间隔的上下界（阶段 120）──
    //
    // 这两个值来自工程 JSON 与 AI 工具参数（`max_points` 的 schema **没有下界**），
    // 而它们直接进环形缓冲与 `Timer`。旧实现直接用原值 ⇒ `MaxRecords = 0` 时
    // `_ringBuffer[0]` 抛 IndexOutOfRange、`% 0` 抛 DivideByZero，且抛在定时器线程上
    // （无捕获 ⇒ 进程终止）；`SampleIntervalMs = 0` 让 `Timer.Change(0, 0)` 抛。

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Ctor_NonPositiveMaxRecords_ClampsToUsableCapacity(int maxRecords)
    {
        using var logger = new DataLogger(new DataLogConfig
        {
            MaxRecords = maxRecords,
            VariableIds = { "v1" }
        });

        Assert.Equal(1, logger.Capacity);

        // 关键：真的采一次 —— 旧实现会在这里抛（数组为 0 长 / 取模 0）
        logger.AddSample(new Dictionary<string, double> { ["v1"] = 1.0 });
        Assert.Single(logger.GetAllSamples());
    }

    [Fact]
    public void Ctor_HugeMaxRecords_ClampsToCeiling()
    {
        using var logger = new DataLogger(new DataLogConfig { MaxRecords = int.MaxValue });
        Assert.Equal(1_000_000, logger.Capacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Start_NonPositiveInterval_DoesNotThrow(int interval)
    {
        using var logger = new DataLogger(new DataLogConfig
        {
            MaxRecords = 8,
            SampleIntervalMs = interval,
            VariableIds = { "v1" }
        });

        logger.Start();   // 旧实现：Timer.Change(0, 0) 抛 ArgumentOutOfRangeException
        logger.Stop();
    }
}
