using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// <see cref="LocalizationPlaceholders"/> 的**边界判定表**（阶段 43）。
///
/// <para><b>为什么单开一条</b>：这个判据是运行期 <c>Format</c> 与四条守卫的**共同判据**，
/// 它一旦判错，错的不是某一条测试而是"用户界面上有没有花括号"。</para>
///
/// <para><b>每条都写清"旧口径会怎样"</b>：这个判据是从"按占位符出现次数数"改过来的，
/// 而那次改动同时修了两个反向的错（假红：重复占位符；漏报：索引有洞）。
/// 判定表把两个方向都钉住 —— 只有一行"期望值"是证不出这件事的。</para>
/// </summary>
public class LocalizationPlaceholdersTests
{
    [Fact]
    public void MatchesArgumentCount_FollowsTheDecisionTable()
    {
        var cases = new (string Template, int Args, bool Expected, string Why)[]
        {
            ("{0}", 1, true, "最普通的一条"),
            ("{0} 与 {1}", 2, true, "顺序正常的两个占位符"),
            ("{0} 与 {0}", 1, true,
                "同一个占位符出现两次是**合法**的 —— 旧口径按出现次数数成 2 ≠ 1 个实参，会假红"),
            ("{0} 与 {2}", 2, false,
                "索引**有洞** —— 旧口径数出 2 == 2 会放过，运行期 string.Format 直接抛 FormatException"),
            ("{1}", 1, false, "索引不从 0 起"),
            ("{0} 与 {1}", 1, false, "实参给少了 ⇒ 用户看到花括号字面量"),
            ("{0}", 2, false, "实参给多了 ⇒ 运行期不抛（多余实参被忽略），但调用点几乎总是写错了"),
            ("{0:N2} 与 {1}", 2, true, "带格式说明符也要认出来"),
            ("{{0}} 与 {0}", 1, true,
                "转义花括号 {{0}} 是**字面量**：真占位符只有后面那个 {0}。"
                + "旧口径会把两处的 {0} 都数上 ⇒ 2 ≠ 1 个实参，假红"),
            ("没有任何占位符", 0, true, "零实参配零占位符"),
            ("没有任何占位符", 1, false, "有实参却没占位符"),
        };

        var problems = new List<string>();
        foreach (var (template, args, expected, why) in cases)
        {
            var actual = LocalizationPlaceholders.MatchesArgumentCount(template, args);
            if (actual != expected)
                problems.Add($"「{template}」配 {args} 个实参：期望 {expected}、实得 {actual}（{why}）");
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Indexes_AreDistinctAscendingAndSkipEscapedBraces()
    {
        // 乱序 + 重复 ⇒ 去重升序；转义花括号整对跳过
        Assert.Equal(new[] { 0, 1 }, LocalizationPlaceholders.Indexes("{1} 与 {0} 与 {1}"));
        Assert.Empty(LocalizationPlaceholders.Indexes("{{0}} 与 }} 与 {{"));

        // 诊断文本要能直接读（失败消息里靠它区分"少一个"和"有洞"）
        Assert.Equal("[{0}, {1}]", LocalizationPlaceholders.DescribeIndexes("{1}{0}"));
        Assert.Equal("无占位符", LocalizationPlaceholders.DescribeIndexes("纯文本"));
    }
}
