using HmiEditor.App.Resources;
using HmiEditor.Core.Services;

// ── 只抑制 CS8524，**绝不**抑制 CS8509（2026-09-22 探针实测，两码是分开的）：
//    · 本文件两个 switch 都是**全覆盖 + 无默认支** ⇒ 编译器报 CS8524，消息举例是
//      **未命名值**「(FindingSeverity)3」——这是 C# 对枚举 switch 的**固定噪声**
//      （枚举永远可能持有一个未声明值），与本文件要挡的事无关；
//    · 一旦**新增具名取值**而没人接文案 ⇒ 报的是 CS8509，消息举例是那个具名成员。
//    ⇒ 抑制噪声、留住信号：新增枚举值仍会让 ci-gate.sh（按 ": warning " 行判红）当场红。
//    实测对照见 .workbuddy/tmp/enumprobe/Program.cs（3/3 -> CS8524；2/3 -> CS8509）。
#pragma warning disable CS8524

namespace HmiEditor.App.Services;

/// <summary>
/// 「生成代码验证结果」这一族的**唯一显示映射**（登记 #61j，阶段 92）。
///
/// <para><b>它挡的是什么</b>：这一族的文案以前有**两条独立实现** —— 对话框里的徽章/分类走字典
/// （<c>ValidationResultView.CreateItem</c> 的两个 <c>switch</c>），而「复制全部」那一行是
/// <c>$"[{f.Severity}] {f.Category}: …"</c> **直接插值枚举名** ⇒ 同一条 finding 在两处语言不同：
/// 界面上写着「结构」、复制出来却是 <c>Structure</c>（中文界面下也如此）。收成一份之后两处**同源**。</para>
///
/// <para><b>为什么两个 <c>switch</c> 表达式刻意**不给 <c>_</c> 支**</b>：给了默认支，
/// 新增一个枚举取值就会**静默落进**默认支的文案（今天的 <c>Info</c> 与 <c>CodeGen</c> 正是这样活着的：
/// 它们由 <c>_ =&gt;</c> 承担，不是显式档）⇒ 用户会看到一条**假分类**。不给默认支时，
/// 新增**具名**取值 = 编译器报 <c>CS8509</c>（消息举例即那个具名成员）⇒
/// 本仓库门禁（<c>ci-gate.sh</c> 按 <c>: warning </c> 行判红）当场红。</para>
///
/// <para><b>两档警告码必须分清</b>（2026-09-22 探针实测，<c>.workbuddy/tmp/enumprobe/</c>）：
/// 全覆盖而无默认支 ⇒ <c>CS8524</c>（举例是**未命名值**）；缺一个具名值 ⇒ <c>CS8509</c>（举例是具名成员）。
/// 本文件用文件级 <c>#pragma</c> **只**关掉前者，后者保持活跃 —— 判据同时钉住
/// 「被抑制的码恰好是 CS8524」与「全仓不许出现对 CS8509 的抑制」。</para>
///
/// <para><b>两个品牌字面量（<c>Faceplate</c> / <c>LVGL API</c>）刻意不翻</b>：它们是产品/技术名，
/// 翻了反而错（「面板」/「LVGL 接口」都不是那个东西的名字）。这条「判过的不做」由
/// <c>FindingDisplayTextGuardTests</c> 从**两个方向**守着：① 它们的文本**不许随语言变**
/// （其余 6 个分类正好相反）；② <c>finding.category.</c> 前缀下的字典键集合必须**恰好**是那 6 个分类的键
/// （给品牌档加键 ⇒ 红）。</para>
///
/// <para><b>复制行的标点为什么也进字典</b>（<c>finding.copy.line</c>）：与 #61k 同族 ——
/// 「这一行长什么样」应该有**一处权威**，否则「加一列/改个括号」就得在两处改。
/// 模板两本**刻意同值**（中文在这里也沿用半角标点，与今天逐字一致）⇒ 这次只换实参语言，不动行的形态。</para>
/// </summary>
internal static class FindingDisplayText
{
    /// <summary>品牌字面量：未解析的 Faceplate 参数。</summary>
    internal const string BrandFaceplate = "Faceplate";

    /// <summary>品牌字面量：未知的 LVGL API 调用。</summary>
    internal const string BrandLvglApi = "LVGL API";

    /// <summary>严重级别 → 用户语言文案（徽章与复制行共用）。</summary>
    internal static string SeverityText(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Error => AppLocalization.Service[Lang.GenError],
        FindingSeverity.Warning => AppLocalization.Service[Lang.GenWarning],
        FindingSeverity.Info => AppLocalization.Service[Lang.FindingLevelInfo],
    };

    /// <summary>分类 → 用户语言文案（列表的分类列与复制行共用；后两档是**品牌字面量**，不查字典）。</summary>
    internal static string CategoryText(FindingCategory category) => category switch
    {
        FindingCategory.Structure => AppLocalization.Service[Lang.FindingCategoryStructure],
        FindingCategory.Binding => AppLocalization.Service[Lang.FindingCategoryBinding],
        FindingCategory.Expression => AppLocalization.Service[Lang.FindingCategoryExpression],
        FindingCategory.Faceplate => BrandFaceplate,
        FindingCategory.DanglingRef => AppLocalization.Service[Lang.FindingCategoryDangling],
        FindingCategory.LvglApi => BrandLvglApi,
        FindingCategory.RuntimeUser => AppLocalization.Service[Lang.FindingCategoryRuntimeUser],
        FindingCategory.CodeGen => AppLocalization.Service[Lang.FindingCategoryGeneration],
    };

    /// <summary>
    /// 「复制全部」里的一行：结构（方括号/冒号/圆括号）走字典模板，三段实参各走
    /// **同一个**文案映射（级别、分类）与 <see cref="ExternalText"/>（描述、定位）。
    /// </summary>
    internal static string CopyLine(GeneratedCodeFinding finding)
        => AppLocalization.Service.Format(
            Lang.FindingCopyLine,
            SeverityText(finding.Severity),
            CategoryText(finding.Category),
            ExternalText.Describe(finding),
            ExternalText.DescribeLocation(finding));
}
