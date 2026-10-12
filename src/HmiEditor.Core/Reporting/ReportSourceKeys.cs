using System;

namespace HmiEditor.Core.Reporting;

/// <summary>
/// **报表数据源的稳定标识**（阶段 182）：三处要用**同一批**字符串 ——
/// ① 报表包的文件名前缀（<see cref="ReportBundle.FileName"/> 的 <c>kind</c>）；
/// ② 报表模板的存储键（按数据源各存一份）；
/// ③ 判据与 UI 里的列举。
///
/// <para>为什么收成一处：这三处**必须**是同一个值，但任何一处写错都**不会报错**
/// （模板会静默套不上、或者报表包文件名与模板对不上）。收成常量之后，"面板传下去的 kind"
/// 与"模板读写的 key"由判据逐档对齐（同 <c>AlarmReport</c>/<c>AuditReportText</c> 那类映射的纪律）。</para>
///
/// <para>值**刻意不本地化**：它们是产物文件名与偏好存储键，不是界面文案
/// （同登记 `#27c` 的"落盘标识符不翻"）。</para>
/// </summary>
public static class ReportSourceKeys
{
    /// <summary>审计日志。</summary>
    public const string Audit = "audit";

    /// <summary>报警记录。</summary>
    public const string Alarm = "alarm";

    /// <summary>历史数据（趋势采样）。</summary>
    public const string History = "history";

    /// <summary>界面上按这个顺序列出来。</summary>
    public static readonly string[] All = { Audit, Alarm, History };

    /// <summary>是不是已知的数据源键（模板读写用它挡脏数据）。</summary>
    public static bool IsKnown(string? source)
    {
        foreach (var known in All)
            if (string.Equals(known, source, StringComparison.Ordinal)) return true;

        return false;
    }
}
