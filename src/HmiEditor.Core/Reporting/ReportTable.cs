using System.Collections.Generic;

namespace HmiEditor.Core.Reporting;

/// <summary>
/// 报表的一列：<paramref name="Key"/> 是**稳定标识**（判据按它对齐列序），
/// <paramref name="DisplayName"/> 是**给人看的列名**（由调用方从字典取，Core 不认识语言）。
/// </summary>
/// <param name="Key">列键，形如 <c>audit.report.col.time</c>。</param>
/// <param name="DisplayName">列名（已本地化的显示文本）。</param>
public sealed record ReportColumn(string Key, string DisplayName);

/// <summary>
/// 一张报表 —— **数据与呈现分离**：这里只有"标题 ＋ 列 ＋ 行"，
/// 至于输出成 CSV 还是自包含 HTML，由 <see cref="ReportExporter"/> 决定。
///
/// <para><b>为什么不做成"模板引擎"</b>（阶段 172 的选型结论，见
/// <c>docs/2026-10-01-reporting-capability-mvp.md</c>）：本仓的产物要能**逐字节判**，
/// 而模板引擎会把"占位符替换"变成第三方语义；MVP 只需要 标题/生成时间/表头/行 四件，
/// 自己拼比引入依赖便宜，也好判。</para>
/// </summary>
public sealed class ReportTable
{
    /// <summary>报表标题（已本地化）。</summary>
    public string Title { get; init; } = "";

    /// <summary>列（顺序即输出顺序）。</summary>
    public IReadOnlyList<ReportColumn> Columns { get; init; } = new List<ReportColumn>();

    /// <summary>行 —— 每行的单元格数与 <see cref="Columns"/> 必须一致（导出时按列数截/补）。</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = new List<IReadOnlyList<string>>();

    /// <summary>
    /// **按列键投影出一张新表**（阶段 177：导出前的"选列"）：列**按 <paramref name="columnKeys"/> 给的顺序**保留，
    /// 其余丢弃；行只保留对应的那些单元格。
    ///
    /// <para>⚠ 三条刻意的口径：① **未知列键忽略**（配置里的列名可能来自旧版本 —— 忽略比抛异常更不容易把用户挡在门外）；
    /// ② **一个都不剩**时抛 <see cref="ArgumentException"/>（那是调用方的编程错误：界面上不允许取消掉所有列）；
    /// ③ <see cref="CsvOverride"/> **原样带过去**（它是整表的 CSV 形态，列选择对它不生效 —— 这条要写在调用方的注释里）。</para>
    /// </summary>
    public ReportTable WithColumns(IReadOnlyList<string> columnKeys)
    {
        ArgumentNullException.ThrowIfNull(columnKeys);

        var positions = new List<int>();
        foreach (var key in columnKeys)
        {
            var index = -1;
            for (var i = 0; i < Columns.Count; i++)
            {
                if (string.Equals(Columns[i].Key, key, StringComparison.Ordinal)) { index = i; break; }
            }
            if (index >= 0 && !positions.Contains(index)) positions.Add(index);   // 未知键忽略；重复键只取一次
        }

        if (positions.Count == 0)
            throw new ArgumentException("按列键投影后一列都不剩 —— 界面上不允许取消掉所有列", nameof(columnKeys));

        var rows = new List<IReadOnlyList<string>>(Rows.Count);
        foreach (var row in Rows)
        {
            var cells = new string[positions.Count];
            for (var i = 0; i < positions.Count; i++)
            {
                var at = positions[i];
                cells[i] = at < row.Count ? row[at] ?? string.Empty : string.Empty;
            }
            rows.Add(cells);
        }

        return new ReportTable
        {
            Title = Title,
            Columns = positions.Select(i => Columns[i]).ToList(),
            Rows = rows,
            CsvOverride = CsvOverride,
        };
    }

    /// <summary>
    /// **套用报表模板**（阶段 181）：按 <see cref="ReportTemplate"/> 的覆盖意图重排/改名/隐藏列。
    ///
    /// <para>口径（四条，逐条都有判据）：① <paramref name="template"/> 为 <c>null</c> 或空 ⇒ **原样返回**
    /// （"没设模板"不是"什么都不要"）；② 模板里**列到**的键按模板顺序排在前面，**没列到**的键
    /// 按原序**接在后面**（覆盖层语义 —— 新出现的列不会静默消失）；③ 改名的条目替换
    /// <see cref="ReportColumn.DisplayName"/>，但 <see cref="ReportColumn.Key"/> 永远不变
    /// （键是判据与投影的对齐依据）；④ <see cref="ReportTemplateEntry.Hidden"/> 的列被拿掉，
    /// **一个不剩**时抛（同 <see cref="WithColumns"/>）。</para>
    ///
    /// <para>模板里的未知键、重复键都**忽略**；<see cref="CsvOverride"/> 原样带过去
    /// （它有自己的列序，模板对它不生效 —— 与 <see cref="WithColumns"/> 同一条边界）。</para>
    /// </summary>
    public ReportTable ApplyTemplate(ReportTemplate? template)
    {
        if (template is null || template.IsEmpty) return this;

        var order = new List<string>();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in template.Entries)
        {
            if (string.IsNullOrEmpty(entry.Key)) continue;
            // 重复键：**第一条生效**（顺序与改名都是）—— 与 WithColumns 的"重复键只取一次"同口径，
            // 免得同一份模板在两次读取里给出两种列序。
            if (names.ContainsKey(entry.Key) || hidden.Contains(entry.Key) || order.Contains(entry.Key)) continue;
            if (entry.Hidden)
            {
                hidden.Add(entry.Key);
                continue;
            }
            order.Add(entry.Key);
            if (!string.IsNullOrEmpty(entry.DisplayName)) names[entry.Key] = entry.DisplayName!;
        }

        var kept = new List<int>();
        for (var i = 0; i < Columns.Count; i++)
            if (!hidden.Contains(Columns[i].Key)) kept.Add(i);

        if (kept.Count == 0)
            throw new ArgumentException("模板把每一列都隐藏了 —— 那样导不出任何东西", nameof(template));

        // 模板里列到的排前面（按模板顺序），没列到的接在后面（按原序）
        kept.Sort((a, b) =>
        {
            var ta = order.IndexOf(Columns[a].Key);
            var tb = order.IndexOf(Columns[b].Key);
            if (ta < 0 && tb < 0) return a.CompareTo(b);
            if (ta < 0) return 1;
            if (tb < 0) return -1;
            return ta.CompareTo(tb);
        });

        var rows = new List<IReadOnlyList<string>>(Rows.Count);
        foreach (var row in Rows)
        {
            var cells = new string[kept.Count];
            for (var i = 0; i < kept.Count; i++)
            {
                var at = kept[i];
                cells[i] = at < row.Count ? row[at] ?? string.Empty : string.Empty;
            }
            rows.Add(cells);
        }

        var columns = new List<ReportColumn>(kept.Count);
        foreach (var at in kept)
        {
            var column = Columns[at];
            columns.Add(names.TryGetValue(column.Key, out var name)
                ? column with { DisplayName = name }
                : column);
        }

        return new ReportTable
        {
            Title = Title,
            Columns = columns,
            Rows = rows,
            CsvOverride = CsvOverride,
        };
    }

    /// <summary>
    /// **CSV 字节的覆盖形态**（可空）：这条数据源若在 CSV 上**另有更合适的列序/时间基**
    /// （例如历史数据要跟设备 SD 卡 CSV 对齐），就带一份；导出 CSV 时**优先用它**，
    /// 其余格式（xlsx/pdf/html）仍走 <see cref="Columns"/>／<see cref="Rows"/>。
    ///
    /// <para>为什么做成"表上带一份"而不是给导出器加分支：导出器对**所有**报表都通用，
    /// 让它认识"历史数据"这一种数据源会把特例埋进公共路径（阶段 176 的取舍）。</para>
    /// </summary>
    public byte[]? CsvOverride { get; init; }
}
