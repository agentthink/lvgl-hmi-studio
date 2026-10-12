using System;
using System.Collections.Generic;

namespace HmiEditor.Core.Reporting;

/// <summary>
/// **模板里的一条**（阶段 181）：某个列键的呈现意图 —— 改名（可空＝保留数据源自带的列名）与
/// 是否**隐藏**。
/// </summary>
/// <param name="Key">列键（与 <see cref="ReportColumn.Key"/> 同域）。</param>
/// <param name="DisplayName">覆盖用的列名；<c>null</c> ＝ 不改名。</param>
/// <param name="Hidden">隐藏这一列（**显式**把某列拿掉 —— 与"没在模板里"是两件事，见类注释）。</param>
public sealed record ReportTemplateEntry(string Key, string? DisplayName = null, bool Hidden = false);

/// <summary>
/// **报表模板**（阶段 181，报表设计器的数据形状）：一组**覆盖条目** —— 列的顺序、列名覆盖、以及
/// **显式隐藏**。
///
/// <para><b>为什么是"覆盖层"而不是"白名单"</b>（本阶段最重要的一个取舍）：阶段 177 的
/// <see cref="ReportTable.WithColumns"/> 是**这一次导出**的白名单（用户在对话框里亲手勾的，当场生效）；
/// 而模板是**长期**存下来的 —— 若也按白名单，历史数据报表里"用户以后新加了一支画笔"就会
/// **永远静默不出现**（数据在看板上，报表里却没有，而且没有任何提示）。所以模板只做三件事：
/// ① 给**已列出的**键定顺序（没列到的按原序**接在后面**）；② 改名；③ 隐藏（必须**显式**写
/// <see cref="ReportTemplateEntry.Hidden"/>，即"这是我决定的"，不是"我上次没勾"）。</para>
///
/// <para><b>未知键忽略</b>（同 <see cref="ReportTable.WithColumns"/>）：模板里可能留着已被删掉的变量；
/// 忽略比抛异常更不容易把用户挡在门外。<b>全部列都被隐藏</b>则抛 —— 那是"导不出任何东西"，
/// 静默给一张空表会让用户以为"这段时间没数据"。</para>
/// </summary>
public sealed record ReportTemplate(IReadOnlyList<ReportTemplateEntry> Entries)
{
    /// <summary>空模板（＝没有任何覆盖意图，套用后原样返回）。</summary>
    public static readonly ReportTemplate Empty = new(Array.Empty<ReportTemplateEntry>());

    /// <summary>没有任何条目 ⇒ 套用它是**恒等**操作。</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>
    /// **把模板铺开成"设计器要显示的那份全列清单"**（阶段 183）：顺序按模板（列到的在前、
    /// 没列到的按原序接在后面），**被隐藏的列留在它原来的位置上**并带
    /// <see cref="ReportTemplateEntry.Hidden"/>，列名取模板覆盖或原列名。
    ///
    /// <para><b>为什么不能直接用 <see cref="ReportTable.ApplyTemplate"/></b>：那是**导出**视图，
    /// 隐藏的列**已经不在里面**了 —— 设计器若拿它当输入，"被隐藏的列"就再也勾不回来
    /// （用户只能靠"恢复默认"把整份模板清掉，而那一刀把他的顺序与改名也一起清了）。
    /// 这一条正是本阶段的第二条口径：**设计器与导出各自要一份不同形状的清单**，
    /// 两份都由这里（唯一的顺序/可见性口径）推出来。</para>
    ///
    /// <para>模板里的**陈旧键**（列清单里已经不存在）忽略；重复键只认第一条
    /// （与 <see cref="ReportTable.ApplyTemplate"/> 同口径）。</para>
    /// </summary>
    public static IReadOnlyList<ReportTemplateEntry> Spread(
        IReadOnlyList<ReportColumn> columns, ReportTemplate? template)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var result = new List<ReportTemplateEntry>(columns.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (template is not null)
        {
            foreach (var entry in template.Entries)
            {
                if (string.IsNullOrEmpty(entry.Key) || !seen.Add(entry.Key)) continue;

                var column = Find(columns, entry.Key);
                if (column is null) continue;   // 陈旧键：列清单里没有它

                result.Add(entry.Hidden
                    // 隐藏的列不算"改过名"（它没在导出里出现过）⇒ 名字一律用原列名，
                    // 免得用户勾回来时看到一个来自旧模板的、自己也认不出的名字
                    ? new ReportTemplateEntry(entry.Key, column.DisplayName, Hidden: true)
                    : new ReportTemplateEntry(entry.Key,
                        string.IsNullOrEmpty(entry.DisplayName) ? column.DisplayName : entry.DisplayName));
            }
        }

        foreach (var column in columns)
        {
            if (seen.Contains(column.Key)) continue;
            result.Add(new ReportTemplateEntry(column.Key, column.DisplayName));
        }

        return result;
    }

    private static ReportColumn? Find(IReadOnlyList<ReportColumn> columns, string key)
    {
        foreach (var column in columns)
            if (string.Equals(column.Key, key, StringComparison.Ordinal)) return column;

        return null;
    }
}
