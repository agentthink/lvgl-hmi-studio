using HmiEditor.App.Resources;
using HmiEditor.Core.Reporting;

namespace HmiEditor.App.Services;

/// <summary>
/// **时间范围对话框的显示文本**（阶段 180）—— 本仓既有的「**Core 给键、App 推键**」形状
/// （同 <see cref="AuditReportText"/> / <see cref="AlarmReportText"/> / <see cref="HistoryReportText"/>）：
/// <see cref="ReportRangeOptions"/> 只给 <c>report.range.*</c> 键，用哪个 <see cref="Lang"/> 常量、
/// 翻成什么词，都在这里。
///
/// <para><b>为什么值得多这一层</b>：此前对话框直接 <c>AppLocalization.Service[LabelKey(choice)]</c>，
/// 于是 <c>Lang.ReportRangeAll</c>… 这一组常量**在代码里根本没有出现点**（键只在运行时拼出来），
/// 只能靠 <c>LangConstantUsageGuardTests</c> 的基线兜着。多这一层之后，六个档位的键都由**常量**
/// 供给，且下面那条"App 映射 ≡ Core 键"的判据能把两份抄本钉在一起。</para>
///
/// <para><b>未映射的档位不许静默</b>：<c>_</c> 分支回落到「全部」，但
/// <c>ReportRangeTextTests</c> 会按 <see cref="ReportRangeOptions.All"/> **逐个核对**
/// 映射与 Core 键一致、且文案各不相同（新增档位忘了加映射 ⇒ 判据红，而不是界面上悄悄少一档）。</para>
/// </summary>
public static class ReportRangeText
{
    /// <summary>档位 → <see cref="Lang"/> 常量键。</summary>
    public static string Key(ReportRangeChoice choice) => choice switch
    {
        ReportRangeChoice.LastHour => Lang.ReportRangeLastHour,
        ReportRangeChoice.Last24Hours => Lang.ReportRangeLast24h,
        ReportRangeChoice.Last7Days => Lang.ReportRangeLast7d,
        ReportRangeChoice.Last30Days => Lang.ReportRangeLast30d,
        ReportRangeChoice.Custom => Lang.ReportRangeCustom,
        _ => Lang.ReportRangeAll,
    };

    /// <summary>档位 → 用户看到的词。</summary>
    public static string Label(ReportRangeChoice choice) => AppLocalization.Service[Key(choice)];

    /// <summary>"从"标签（自定义起止日期那一行）。</summary>
    public static string FromLabel => AppLocalization.Service[Lang.ReportRangeFrom];

    /// <summary>"到"标签。</summary>
    public static string ToLabel => AppLocalization.Service[Lang.ReportRangeTo];
}
