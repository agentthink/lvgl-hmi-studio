namespace HmiEditor.App.Services;

/// <summary>
/// 插件侧用户可见文案的**本地化键推导** —— 单一来源。
///
/// <para><b>为什么必须收成一处：</b>插件程序集（<c>Hmi.*.Builtin</c>）拿不到 App 的字典，
/// 所以它们的显示名/描述/字段标签/校验文案都靠 **App 侧按约定推导键**再查字典，
/// 查不到才回落插件原文。推导规则若在视图里写一份、在守卫里再抄一份，
/// 两份必然会漂移 —— 而漂移的表现是"守卫绿着、界面却显示插件中文"，
/// 正是这套机制要防的东西。所以推导只住在这里，视图与守卫都调它。</para>
///
/// <para>约定（五条，全部按 <c>Code</c>/<c>Id</c>/<c>Key</c> 推导，不手写）：
/// <list type="bullet">
///   <item>驱动名：<c>driver.&lt;id，连字符→点&gt;</c>（如 <c>modbus-rtu</c> → <c>driver.modbus.rtu</c>）</item>
///   <item>驱动描述：<c>driver.&lt;id，连字符→点&gt;.description</c></item>
///   <item>配置字段标签：<c>param.&lt;字段 Key，驼峰→点分小写&gt;</c>（如 <c>baud</c> → <c>param.baud</c>）</item>
///   <item>校验结果（**硬件族**）：<c>hw.issue.&lt;Code 小写&gt;</c> —— 见 <see cref="IssueKey"/></item>
///   <item>校验结果（**通讯族**）：<c>comm.issue.&lt;Code 小写&gt;</c> —— 见 <see cref="CommIssueKey"/></item>
/// </list></para>
/// </summary>
internal static class PluginTextKeys
{
    /// <summary>驱动 Id → 显示名键。</summary>
    internal static string DriverNameKey(string driverId) =>
        "driver." + driverId.Replace('-', '.');

    /// <summary>驱动 Id → 描述键。</summary>
    internal static string DriverDescriptionKey(string driverId) =>
        DriverNameKey(driverId) + ".description";

    /// <summary>配置字段 Key → 标签键。</summary>
    internal static string FieldLabelKey(string fieldKey) =>
        "param." + CamelToDotted(fieldKey);

    /// <summary>**硬件族**校验结果 Code → 文案键。</summary>
    internal static string IssueKey(string code) =>
        "hw.issue." + code.ToLowerInvariant();

    /// <summary>
    /// **通讯族**校验结果 Code → 文案键（阶段 104，登记 #61x）。
    ///
    /// <para>与 <see cref="IssueKey"/> 分家而不是抽一个「带前缀参数」的方法，理由只有一条：
    /// 两族的前缀不同（<c>hw.issue.</c> / <c>comm.issue.</c>），而前缀一旦变成调用方的自由变量，
    /// **写错不会有任何编译期反馈** —— 查不到键只会静默回落插件中文，正是这套机制要防的症状。
    /// 两族的记录体也各住各的契约程序集（<c>Hmi.Hardware.Contracts</c> /
    /// <c>Hmi.Communication.Contracts</c>，连 <c>ValidationSeverity</c> 都有两份）。</para>
    ///
    /// <para>通讯族的码由**驱动**给出（<c>Hmi.Communication.Builtin</c> 的四个设计期驱动，
    /// 8 个 Error 码 + 1 个 Warning 码）；App 侧不维护码表 —— 「声明了哪些码」由守卫
    /// **真触发**一遍驱动拿到（<c>PluginTextLocalizationGuardTests</c>），
    /// 而不是抄源码里的字面量。</para>
    /// </summary>
    internal static string CommIssueKey(string code) =>
        "comm.issue." + code.ToLowerInvariant();

    /// <summary>驼峰/帕斯卡字段名 → 点分小写（<c>timeoutMs</c> → <c>timeout.ms</c>）。</summary>
    internal static string CamelToDotted(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length + 4);
        foreach (var ch in s)
        {
            if (char.IsUpper(ch))
            {
                if (sb.Length > 0) sb.Append('.');
                sb.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }
}