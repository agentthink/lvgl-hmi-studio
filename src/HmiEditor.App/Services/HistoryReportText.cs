using System;
using HmiEditor.App.Resources;
using HmiEditor.Core.Reporting;

namespace HmiEditor.App.Services;

/// <summary>
/// **历史数据报表的显示文本**（阶段 175）—— 与 <see cref="AuditReportText"/> / <see cref="AlarmReportText"/> 同形。
///
/// <para>⚠ 这一条只有**两个**可翻译项：标题与"时间"列名。**变量列名一律用变量 Id 本身**
/// （数据标识符不翻 —— 与登记 `#27c` 的判定同一条边界），所以 <see cref="ColumnName"/> 对变量列
/// **原样返回**，这不是"忘了映射"。</para>
/// </summary>
public static class HistoryReportText
{
    /// <summary>标题。</summary>
    public static string Title => AppLocalization.Service[Lang.TrendReportTitle];

    /// <summary>把报表需要的显示文本收成 <c>localize</c> 委托。</summary>
    public static Func<string, string> Localizer => key =>
        string.Equals(key, HistoryReport.TitleKey, StringComparison.Ordinal)
            ? Title
            : string.Equals(key, HistoryReport.TimeColumnKey, StringComparison.Ordinal)
                ? AppLocalization.Service[Lang.UserMgrAuditTime]
                : key;   // 变量 Id：原样（数据标识符，不翻译）
}
