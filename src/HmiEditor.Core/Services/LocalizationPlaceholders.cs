using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace HmiEditor.Core.Services;

/// <summary>
/// 模板占位符的**唯一判据** —— 回答一个问题：模板里的 <c>{0}</c> / <c>{1:N2}</c>
/// 与调用方给的实参**对得上吗**。
///
/// <para><b>为什么必须唯一</b>：这个判据在本仓库曾有**四份实现、两种口径**
/// （运行期 <see cref="LocalizationService.Format"/> 一份、测试守卫三份），而对"什么算合法模板"
/// 的定义并不一致 ⇒ 结果是**守卫绿着、运行期在界面上显示 <c>{0}</c> 字面量**。真实发生过：
/// <c>script.issue.lua_runtime_symbol_undetermined</c> 的模板里 <c>{1}</c> 出现两次（有意写法），
/// 按"出现次数"数成 3 ≠ 2 个实参 ⇒ <c>Format</c> 走"不匹配就原样返回"分支 ⇒ 用户看到字面量。
/// 判据一旦分家，守卫守的就不再是运行期的行为。</para>
///
/// <para><b>判定规则</b>（两条都要成立）：</para>
/// <list type="number">
///   <item><b>不同索引的个数 == 实参个数</b> —— 不是"占位符出现次数"。
///     <c>string.Format</c> 允许同一个占位符出现多次（<c>"{0} 与 {0}"</c> 配一个实参也是合法的），
///     按出现次数数会把它误判成"实参不够"；</item>
///   <item><b>索引恰好是 <c>0..n-1</c></b> —— 只比个数会漏掉"有洞"：
///     <c>{0}</c> 与 <c>{2}</c> 两个占位符、两个实参，个数对得上，而 <c>string.Format</c> 会
///     <b>抛</b> <see cref="FormatException"/>。所以洞的后果不是"显示字面量"而是"崩"，
///     这条必须由判据挡住。</item>
/// </list>
///
/// <para><b>转义花括号</b>（<c>{{</c> / <c>}}</c>）按 .NET 语义是**字面量花括号**，不算占位符 ——
/// 把它们算进去会反过来制造"实参给多了"的假红。</para>
///
/// <para><b>方向上的取舍</b>：本判据比"运行期能跑通"更严一档 —— 实参**多于**占位符时
/// <c>string.Format</c> 不抛（多余实参被静默忽略），本判据仍判不符。因为"多给一个实参"
/// 在调用点几乎总是写错了（比如模板改了、调用点没改），而静默忽略正是它难发现的原因。</para>
/// </summary>
public static class LocalizationPlaceholders
{
    /// <summary>
    /// 匹配一个占位符（可选带格式说明符），或一对转义花括号。
    /// 转义分支放在最前，正则引擎从左到右扫描 ⇒ <c>{{0}}</c> 会先吃掉 <c>{{</c>，
    /// 里面的 <c>0</c> 就不会被当成占位符。
    /// </summary>
    private static readonly Regex Token = new(@"\{\{|\}\}|\{(\d+)(?::[^}]*)?\}", RegexOptions.Compiled);

    /// <summary>
    /// 模板里的**占位符索引**：去重、升序。转义花括号不计入。
    /// </summary>
    public static IReadOnlyList<int> Indexes(string template)
    {
        if (string.IsNullOrEmpty(template)) return Array.Empty<int>();

        var indexes = new List<int>();
        foreach (Match m in Token.Matches(template))
        {
            if (!m.Groups[1].Success) continue;   // {{ 或 }} —— 字面量，不是占位符
            var index = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            if (!indexes.Contains(index)) indexes.Add(index);
        }

        indexes.Sort();
        return indexes;
    }

    /// <summary>
    /// 模板与实参个数是否**恰好**对得上（见类型注释的两条规则）。
    /// </summary>
    public static bool MatchesArgumentCount(string template, int argumentCount)
    {
        var indexes = Indexes(template);
        if (indexes.Count != argumentCount) return false;

        for (var i = 0; i < argumentCount; i++)
        {
            if (indexes[i] != i) return false;
        }

        return true;
    }

    /// <summary>
    /// 把模板的占位符索引渲染成一段给人看的文字（<c>[{0}, {1}]</c> / <c>无占位符</c>），
    /// 供失败消息与诊断日志点名 —— 只说"个数不符"看不出是"少一个"还是"有洞"。
    /// </summary>
    public static string DescribeIndexes(string template)
    {
        var indexes = Indexes(template);
        return indexes.Count == 0
            ? "无占位符"
            : "[" + string.Join(", ", indexes.Select(i => "{" + i.ToString(CultureInfo.InvariantCulture) + "}")) + "]";
    }
}
