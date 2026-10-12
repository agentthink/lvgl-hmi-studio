using System;
using HmiEditor.App.Resources;
using HmiEditor.Core.Reporting;

namespace HmiEditor.App.Services;

/// <summary>
/// **审计报表的显示文本**（阶段 172）—— 本仓既有的「**Core 给键、App 推键**」形状
/// （同 <c>ActionTextKeys</c> / <c>ControlTypeTextKeys</c>）：
/// <see cref="AuditLogReport"/> 只认列键，具体用哪个 <see cref="Lang"/> 常量、翻成什么词，都在这里。
///
/// <para><b>为什么不直接把词写进 Core</b>：Core 不认识语言（而且要能跑纯判据）；
/// 而 <c>Lang.*</c> 常量只有在**产品代码里被引用**才算"可达"（登记 `#48` 那条纪律：
/// 只写在注释或字典里不算）⇒ 映射留在 App，键可达性由编译器与守卫一起保证。</para>
/// </summary>
public static class AuditReportText
{
    /// <summary>列键 → 列名。⚠ 列名<strong>复用</strong>用户管理器审计页那一组词
    /// （"时间/用户/操作/结果/来源/详情"），与 <see cref="AuditLogReport.ColumnKeys"/> 一一对应。</summary>
    public static string ColumnName(string coreKey) => coreKey switch
    {
        "user.mgr.audit.time" => AppLocalization.Service[Lang.UserMgrAuditTime],
        "user.mgr.audit.user" => AppLocalization.Service[Lang.UserMgrAuditUser],
        "user.mgr.audit.action" => AppLocalization.Service[Lang.UserMgrAuditAction],
        "user.mgr.audit.result" => AppLocalization.Service[Lang.UserMgrAuditResult],
        "user.mgr.audit.source" => AppLocalization.Service[Lang.UserMgrAuditSource],
        "user.mgr.audit.detail" => AppLocalization.Service[Lang.UserMgrAuditDetail],
        // 未映射的列键**不许静默**：宁可把键显示出来（判据会红），也不要悄悄少一列
        _ => coreKey,
    };

    /// <summary>结果列：<c>gen.success</c> / <c>gen.failed</c>（复用通用词）。</summary>
    public static string ResultText(bool success) => AppLocalization.Service[
        success ? Lang.GenSuccess : Lang.GenFailed];

    /// <summary>标题。</summary>
    public static string Title => AppLocalization.Service[Lang.AuditReportTitle];

    /// <summary>"生成时间"标签。</summary>
    public static string GeneratedLabel => AppLocalization.Service[Lang.AuditReportGenerated];

    /// <summary>把报表需要的显示文本收成一个 <c>localize</c> 委托，交给 Core 造表。</summary>
    public static Func<string, string> Localizer => key => key switch
    {
        AuditLogReport.TitleKey => Title,
        AuditLogReport.ResultOkKey => ResultText(true),
        AuditLogReport.ResultFailKey => ResultText(false),
        _ => ColumnName(key),
    };
}
