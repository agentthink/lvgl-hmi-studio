using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HmiEditor.Core.Models.Trend;

namespace HmiEditor.Core.Reporting;

/// <summary>
/// **历史数据（趋势采样）报表**（阶段 175，报表能力第三片）：把 <see cref="TrendSample"/> 序列翻成一张
/// <see cref="ReportTable"/>。
///
/// <para><b>列是动态的</b>：第一列"时间" ＋ 每个变量一列 —— 列名用**变量 Id 本身**
/// （它是数据标识符、不是界面文案 ⇒ 不翻译，与登记 `#27c` 的"落盘标识符不翻"同一条边界）。
/// 变量顺序必须与 <see cref="TrendSample.Values"/> 的**取样顺序**一致，而这个顺序只有
/// <c>DataLogger.Config.VariableIds</c> 说得准（用户中途改画笔不会改已采样的对齐）⇒ 调用方要传**记录时**的那一份。</para>
///
/// <para><b>行数封顶</b>：环形缓冲里可能几万条采样，直接导全量会产出几十 MB 的文件、还会把 UI 卡住
/// ⇒ 默认只取**最近</b> <see cref="DefaultMaxRows"/> 条（截断发生在取数一侧，报表只是如实渲染）。
/// 截断**在产物里不留痕**（刻意：多一行"已截断"的说明会把列数搞乱）—— 需要完整数据的用设备侧
/// SD 卡 CSV（那是另一个产物，见专题 §11.4）。</para>
/// </summary>
public static class HistoryReport
{
    /// <summary>默认最多导多少行（最近的那些）。</summary>
    public const int DefaultMaxRows = 5000;

    /// <summary>时间列的键 —— 复用"时间"这个既有词。</summary>
    public const string TimeColumnKey = "user.mgr.audit.time";

    /// <summary>标题键。</summary>
    public const string TitleKey = "trend.report.title";

    /// <summary>
    /// **列清单**（阶段 183）：时间列 ＋ 每个变量一列（列名＝变量 Id 本身）。
    /// 与 <see cref="From"/> 共用同一份构造 —— 列是**动态的**，两份清单漂移会导致
    /// "设计器里排好的顺序在导出时对不上"，而且不会报错。
    /// </summary>
    public static IReadOnlyList<ReportColumn> Columns(IReadOnlyList<string> variableIds, Func<string, string> localize)
    {
        ArgumentNullException.ThrowIfNull(variableIds);
        ArgumentNullException.ThrowIfNull(localize);

        var columns = new List<ReportColumn> { new(TimeColumnKey, localize(TimeColumnKey)) };
        columns.AddRange(variableIds.Select(id => new ReportColumn(id, id)));
        return columns;
    }

    /// <summary>
    /// 造表。<paramref name="variableIds"/> 是**采样时的**变量顺序（见类注释）；
    /// <paramref name="maxRows"/> ≤ 0 表示不封顶。
    /// </summary>
    public static ReportTable From(
        IEnumerable<TrendSample> samples,
        IReadOnlyList<string> variableIds,
        Func<string, string> localize,
        int maxRows = DefaultMaxRows,
        byte[]? csvOverride = null,
        ReportTimeRange? range = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(variableIds);
        ArgumentNullException.ThrowIfNull(localize);

        var columns = Columns(variableIds, localize).ToList();

        // ⚠ **先按时间范围过滤，再封顶**：反过来的话，范围筛选拿到的只是「最近 N 条里的那些」——
        //   用户选了 7 天却只看到最近 5000 条里落在 7 天内的部分，读数会小得莫名其妙（阶段 178 判据钉着）。
        var all = range is { } r
            ? samples.Where(s => r.Contains(s.Timestamp)).ToList()
            : samples.ToList();
        var taken = maxRows > 0 && all.Count > maxRows
            ? all.Skip(all.Count - maxRows).ToList()   // **最近**的那些
            : all;

        var rows = new List<IReadOnlyList<string>>(taken.Count);
        foreach (var sample in taken)
        {
            var cells = new string[variableIds.Count + 1];
            cells[0] = ReportExporter.FormatUtc(sample.Timestamp);
            for (var i = 0; i < variableIds.Count; i++)
                cells[i + 1] = i < sample.Values.Count
                    ? sample.Values[i].ToString("0.####", CultureInfo.InvariantCulture)
                    : string.Empty;   // 采样比变量列短 ⇒ 留空（不是 0：0 是"真的是 0"）
            rows.Add(cells);
        }

        return new ReportTable
        {
            Title = localize(TitleKey),
            Columns = columns,
            Rows = rows,
            // 历史数据的 CSV 跟设备 SD 卡 CSV 对齐（列序一致、时间基写在表头里）——见 DataLogger.ExportCsv
            CsvOverride = csvOverride,
        };
    }
}
