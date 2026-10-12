using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 测试策略守卫：禁止新增「精确调用文本 + 《字符串包含》」式断言。
///
/// <para>本仓库已把这类断言迁移到 <see cref="CSource"/> 的结构化断言。之所以要加守卫，
/// 是因为这种写法很自然、很容易在新测试里复发，而它的两个毛病都很隐蔽：
/// ① 生成器只是改了缩进/换行/空格，测试就变红（假失败）；
/// ② 同样的文本出现在注释或字符串里时断言又会变绿（假通过）。</para>
///
/// <para>这是**唯一**一处用源码扫描做断言的地方 —— 它守的是「测试怎么写」，
/// 而不是「产品代码是什么」，后者一律用行为测试。</para>
/// </summary>
public class TestAssertionStyleGuardTests
{
    /// <summary>
    /// 允许保留精确调用文本断言的例外：
    /// <list type="bullet">
    ///   <item><c>Support/CSource.cs</c> 与 <c>CSourceHelperTests.cs</c> —— 断言工具自身的示例与自测。</item>
    ///   <item><c>Hmi.Hardware.Tests/PluginLoadingTests.cs</c> —— 断言对象是手写的插件样例工程，
    ///     不是生成器输出（没有排版漂移问题）。</item>
    /// </list>
    /// </summary>
    private static readonly string[] AllowedFiles =
    {
        Path.Combine("Support", "CSource.cs"),
        "CSourceHelperTests.cs",
        "TestAssertionStyleGuardTests.cs",
        "PluginLoadingTests.cs",
    };

    private static readonly Regex AssertionCall =
        new(@"Assert\.(Contains|DoesNotContain)\(", RegexOptions.Compiled);

    private static readonly Regex StringLiteral =
        new("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

    [Fact]
    public void NoExactCallSubstringAssertionsOutsideAllowList()
    {
        var root = FindRepoRoot();
        var testsDir = Path.Combine(root, "tests");
        Assert.True(Directory.Exists(testsDir), $"未找到 tests 目录: {testsDir}");

        var violations = new List<string>();
        // 扫描面自检（阶段 121）：枚举一旦失效（目录换了/glob 改了），下面一条都不会走
        // ⇒ violations 恒为 0 ⇒ 守卫变成"永不失败"的摆设。实测 100+，下限取 100。
        var sources = EnumerateTestSources(testsDir).ToList();
        Assert.True(sources.Count >= 100,
            $"只枚举到 {sources.Count} 个测试源文件 —— 枚举面塌了，本判据会退化成永远通过");

        foreach (var file in sources)
        {
            var relative = Path.GetRelativePath(testsDir, file);
            if (AllowedFiles.Any(a => relative.EndsWith(a, StringComparison.Ordinal)))
                continue;

            var lineNo = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNo++;
                if (!AssertionCall.IsMatch(line))
                    continue;
                // 跳过注释行：文档里的示例不算断言。
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                    trimmed.StartsWith("*", StringComparison.Ordinal) ||
                    trimmed.StartsWith("/*", StringComparison.Ordinal))
                    continue;

                foreach (Match m in StringLiteral.Matches(line))
                {
                    var value = m.Groups[1].Value;
                    // 「一次带实参的调用」是脆弱的信号：内含 ( 与 ,
                    if (value.Contains('(') && value.Contains(','))
                        violations.Add($"{relative}:{lineNo}  {trimmed}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"发现 {violations.Count} 处「精确调用文本 + 字符串包含」断言，"
            + "请改用 CSource.AssertCode / AssertCall / AssertText：\n"
            + string.Join("\n", violations.Take(40)));
    }

    [Fact]
    public void GuardItself_DetectsTheBrittlePattern()
    {
        // 守卫自测（不落盘）：同样的判定逻辑必须能识别出脆弱写法，
        // 否则这个守卫只是"永不失败"的摆设。
        const string sample = "        Assert.Contains(\"lv_obj_set_pos(obj_c1, 10, 20)\", code);";
        var found = StringLiteral.Matches(sample)
            .Select(m => m.Groups[1].Value)
            .Any(v => v.Contains('(') && v.Contains(','));

        Assert.True(found);

        // 迁移后的写法不应被判为违规。
        const string migrated = "        CSource.AssertCode(code, \"lv_obj_set_pos(obj_c1, 10, 20)\");";
        Assert.DoesNotMatch(AssertionCall, migrated);
    }

    private static IEnumerable<string> EnumerateTestSources(string testsDir) =>
        Directory.EnumerateFiles(testsDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HmiEditor.slnx")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
