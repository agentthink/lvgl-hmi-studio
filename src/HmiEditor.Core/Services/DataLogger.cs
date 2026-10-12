using HmiEditor.Core.Models.Trend;

namespace HmiEditor.Core.Services;

/// <summary>
/// 数据记录引擎 —— 嵌入式友好的环形缓冲区 + 定时采样。
///
/// 设计要点 (data-pipeline-reliability):
///   - Freshness SLI: 采样间隔容差（实际 vs 期望采样时间偏差）
///   - 环形缓冲区: 避免动态内存分配，Flash 扇区对齐
///   - 满策略: OverwriteOldest（默认） / StopWhenFull
///   - 发布检查: 每次采样验证值完整性和时间戳单调性
///   - 变更采样 (Change-of-Value): 变量变化超过死区才记录，对标 FactoryTalk View Data Log
/// </summary>
public class DataLogger : IDisposable
{
    private readonly DataLogConfig _config;
    private readonly TrendSample[] _ringBuffer;

    /// <summary>
    /// 归一化后的容量 / 采样间隔（**判据的一部分**）。
    ///
    /// `DataLogConfig.MaxRecords` / `SampleIntervalMs` 来自工程 JSON 与 AI 工具参数
    /// （`create_trend` / `update_trend` 的 `max_points`，schema 里**没有下界**），而它们
    /// 直接进环形缓冲与 `Timer`：`MaxRecords <= 0` 会让 `_ringBuffer[_writeIndex]` 抛
    /// `IndexOutOfRangeException`、`% MaxRecords` 抛 `DivideByZeroException`，且抛在
    /// **定时器线程**上、`OnSampleTick` 无捕获 ⇒ 整个进程终止；`SampleIntervalMs <= 0`
    /// 会让 `Timer.Change(0, 0)` 直接抛。上界则防一个手改/模型给出的天文数字把内存打爆。
    /// </summary>
    private readonly int _maxRecords;
    private readonly int _sampleIntervalMs;

    private const int MaxRecordsFloor = 1;
    private const int MaxRecordsCeiling = 1_000_000;
    private const int SampleIntervalFloorMs = 1;
    private const int SampleIntervalCeilingMs = 3_600_000;   /* 1 小时 */

    private int _writeIndex;
    private int _sampleCount;
    private bool _isFull;
    private bool _disposed;

    private readonly Timer? _sampleTimer;
    private readonly object _lock = new();

    // ── Freshness SLI tracking ──
    private DateTime _lastSampleTime;
    private int _missedSamples;
    private int _totalSamples;

    /// <summary>数据已更新事件（通知趋势图刷新）</summary>
    public event EventHandler<TrendSample>? SampleAdded;

    /// <summary>缓冲区满事件</summary>
    public event EventHandler? BufferFull;

    /// <summary>采样丢失事件 (freshness violation)</summary>
    public event EventHandler<int>? SampleMissed;

    /// <summary>日志输出</summary>
    public Action<string>? Log { get; set; }

    /// <summary>数据记录器配置</summary>
    public DataLogConfig Config => _config;

    /// <summary>当前采样数</summary>
    public int SampleCount
    {
        get { lock (_lock) return _sampleCount; }
    }

    /// <summary>缓冲区容量</summary>
    public int Capacity => _maxRecords;

    /// <summary>是否已满</summary>
    public bool IsFull
    {
        get { lock (_lock) return _isFull; }
    }

    /// <summary>丢失采样次数 (freshness SLI)</summary>
    public int MissedSamples
    {
        get { lock (_lock) return _missedSamples; }
    }

    /// <summary>总采样次数</summary>
    public int TotalSamples
    {
        get { lock (_lock) return _totalSamples; }
    }

    /// <summary>最后一次采样时间</summary>
    public DateTime LastSampleTime
    {
        get { lock (_lock) return _lastSampleTime; }
    }

    public DataLogger(DataLogConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _maxRecords = Math.Clamp(config.MaxRecords, MaxRecordsFloor, MaxRecordsCeiling);
        _sampleIntervalMs = Math.Clamp(config.SampleIntervalMs, SampleIntervalFloorMs, SampleIntervalCeilingMs);
        _ringBuffer = new TrendSample[_maxRecords];
        _sampleTimer = new Timer(OnSampleTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>启动记录</summary>
    public void Start()
    {
        _lastSampleTime = DateTime.UtcNow;
        _sampleTimer?.Change(0, _sampleIntervalMs);
        Log?.Invoke($"[DataLogger] 启动 — 间隔={_sampleIntervalMs}ms, 容量={_maxRecords}");
    }

    /// <summary>停止记录</summary>
    public void Stop()
    {
        _sampleTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        Log?.Invoke($"[DataLogger] 停止 — 共采 {_totalSamples} 条, 丢失 {_missedSamples} 条");
    }

    // ── 手动采样（由外部变量更新触发）──

    /// <summary>
    /// 手动添加一个采样点（基于当前变量值）。
    /// 用于 Change-of-Value 模式或外部触发。
    /// </summary>
    public void AddSample(Dictionary<string, double> currentValues)
    {
        var sample = new TrendSample { Timestamp = DateTime.UtcNow };

        foreach (var varId in _config.VariableIds)
        {
            sample.Values.Add(currentValues.TryGetValue(varId, out var v) ? v : 0.0);
        }

        AddSampleInternal(sample);
    }

    /// <summary>
    /// 获取当前环形缓冲区中的所有采样（按时间升序）。
    /// </summary>
    public IReadOnlyList<TrendSample> GetAllSamples()
    {
        lock (_lock)
        {
            var count = _sampleCount;
            var result = new TrendSample[count];

            if (_isFull)
            {
                // 环形：从 writeIndex 开始读取 count 个
                for (int i = 0; i < count; i++)
                {
                    result[i] = _ringBuffer[(_writeIndex + i) % _maxRecords];
                }
            }
            else
            {
                // 线性：从 0 到 writeIndex
                Array.Copy(_ringBuffer, 0, result, 0, count);
            }

            return result;
        }
    }

    /// <summary>
    /// 获取指定时间窗口内的采样（用于提取图表显示数据）。
    /// </summary>
    public IReadOnlyList<TrendSample> GetSamplesInWindow(DateTime from, DateTime to)
    {
        var all = GetAllSamples();
        return all.Where(s => s.Timestamp >= from && s.Timestamp <= to).ToList();
    }

    /// <summary>
    /// 获取最近 N 条采样。
    /// </summary>
    public IReadOnlyList<TrendSample> GetRecentSamples(int count)
    {
        var all = GetAllSamples();
        return all.TakeLast(Math.Min(count, all.Count)).ToList();
    }

    /// <summary>
    /// **导出为 CSV（设备列序）**（阶段 176 从"零调用"接成历史报表的 CSV 形态）。
    ///
    /// <para><b>与设备侧的关系（实测，别想当然）</b>：设备端由生成的固件写 CSV
    /// （<c>DataLogExportCsvFn</c>），列序 = 时间戳 ＋ 各变量，值用 <c>%.3f</c>，分隔符取
    /// <c>DataLogConfig.CsvDelimiter</c>（阶段 176 之前是**硬编码逗号** ⇒ 已修）。
    /// 但**时间基不同**：设备写的是 <c>lv_tick_get()</c>（**开机时长 ms**），编辑器只有墙钟
    /// ⇒ 本方法第一列是 **UTC epoch 毫秒**，表头因此写成 <c>timestamp_utc_ms</c>（**不冒充**
    /// <c>timestamp_ms</c>）：列序可直接与 SD 卡 CSV 对齐，时间列的口径写在表头里。</para>
    ///
    /// <para>行尾用 <c>\n</c>（与设备一致，**不是** <c>AppendLine</c> 的平台相关行尾）；
    /// 分隔符为空时回落逗号。<see cref="ExportCsvBytes"/> 再补 UTF-8 BOM（Excel 友好）。</para>
    /// </summary>
    public string ExportCsv()
    {
        var all = GetAllSamples();
        if (all.Count == 0) return "";

        var delim = string.IsNullOrEmpty(_config.CsvDelimiter) ? "," : _config.CsvDelimiter;
        var sb = new System.Text.StringBuilder();

        // 表头：时间列名**写明时间基**（设备是开机时长、编辑器是 UTC 墙钟）
        sb.Append("timestamp_utc_ms");
        foreach (var varId in _config.VariableIds)
            sb.Append(delim).Append(varId);
        sb.Append('\n');

        foreach (var sample in all)
        {
            var utc = DateTime.SpecifyKind(sample.Timestamp, DateTimeKind.Utc);
            sb.Append(new DateTimeOffset(utc).ToUnixTimeMilliseconds());
            foreach (var val in sample.Values)
                sb.Append(delim).Append(val.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// <see cref="ExportCsv"/> 的**字节**形态：UTF-8 **BOM** ＋ 正文（Excel 打开带 CJK 的 CSV 需要 BOM）。
    /// 报表导出走这一份（见 <c>ReportTable.CsvOverride</c>）。
    /// </summary>
    public byte[] ExportCsvBytes()
    {
        var text = ExportCsv();
        if (text.Length == 0) return Array.Empty<byte>();

        var payload = System.Text.Encoding.UTF8.GetBytes(text);
        var bytes = new byte[3 + payload.Length];
        bytes[0] = 0xEF; bytes[1] = 0xBB; bytes[2] = 0xBF;
        Array.Copy(payload, 0, bytes, 3, payload.Length);
        return bytes;
    }

    /// <summary>清空缓冲区和计数器</summary>
    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_ringBuffer, 0, _maxRecords);
            _writeIndex = 0;
            _sampleCount = 0;
            _isFull = false;
            _missedSamples = 0;
            _totalSamples = 0;
        }
        Log?.Invoke("[DataLogger] 已清空");
    }

    // ── 内部 ──

    private void OnSampleTick(object? state)
    {
        if (_disposed) return;

        // ⚠ 定时器回调里的异常**没有调用方能接**（ThreadPool 线程），会直接终止进程。
        //   这里兜住所有异常：采样失败只丢掉这一次，不许把编辑器带走。
        try
        {
            // Freshness check: 检查实际间隔
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastSampleTime).TotalMilliseconds;
            if (elapsed > _sampleIntervalMs * 1.5)
            {
                var missed = (int)(elapsed / _sampleIntervalMs) - 1;
                Interlocked.Add(ref _missedSamples, missed);
                SampleMissed?.Invoke(this, missed);
                Log?.Invoke($"[DataLogger] ⚠ 新鲜度违规: 距上次 {elapsed:F0}ms, 丢失约 {missed} 次采样");
            }

            _lastSampleTime = now;
            Interlocked.Increment(ref _totalSamples);

            // 创建一个占位采样（实际数据由外部注入）
            var sample = new TrendSample { Timestamp = now };
            for (int i = 0; i < _config.VariableIds.Count; i++)
                sample.Values.Add(0.0);

            AddSampleInternal(sample);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[DataLogger] 采样失败（已忽略）: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void AddSampleInternal(TrendSample sample)
    {
        lock (_lock)
        {
            // 满策略检查：在写入前拒绝
            if (_isFull && _config.FullPolicy == LogFullPolicy.StopWhenFull)
                return;

            _ringBuffer[_writeIndex] = sample;
            _writeIndex = (_writeIndex + 1) % _maxRecords;

            if (_isFull)
            {
                // OverwriteOldest: writeIndex 已经前进，无需额外操作
            }
            else
            {
                _sampleCount++;
                if (_sampleCount >= _maxRecords)
                {
                    _isFull = true;
                    if (_config.FullPolicy == LogFullPolicy.StopWhenFull)
                    {
                        Stop();
                        BufferFull?.Invoke(this, EventArgs.Empty);
                        Log?.Invoke("[DataLogger] ⚠ 缓冲区已满 — 停止记录");
                        return;
                    }
                    BufferFull?.Invoke(this, EventArgs.Empty);
                    Log?.Invoke("[DataLogger] 缓冲区已满 — 覆盖最旧记录");
                }
            }
        }

        SampleAdded?.Invoke(this, sample);
    }

    // ── IDisposable ──

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _sampleTimer?.Dispose();
        Clear();
        Log?.Invoke("[DataLogger] 已释放");
    }
}
