using System.Text.Json;
using System.Text.RegularExpressions;
using HmiEditor.AI;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// AI / Agent 侧 JSON Schema 里「枚举词表」的契约守卫。
///
/// <para><b>为什么需要它</b>：这些词表会同时出现在 JSON Schema 的 <c>enum</c>（严格
/// <c>response_format.json_schema</c> 下是**硬约束**，模型只能输出数组内的值）、工具描述文本、
/// 以及解析白名单三处。手抄必然漂移，而且**漂移不报错** —— 消费侧要么静默回退默认值、
/// 要么静默匹配失败，工具却照样回报"已应用"。</para>
///
/// <para><b>实测漂移（2026-09-15 收口前）</b>：控件类型手抄 21 项（真值 25，缺
/// <c>Rectangle/Ellipse/Polyline/Arc</c>）；<c>operation_mode</c> 写了
/// <c>Runtime</c>（<see cref="OperationMode"/> 里没有这个名字）；报警条件写了
/// <c>OnTrue/OnFalse</c>（<see cref="AlarmConditionType"/> 里没有）；驱动写了
/// <c>opcua</c>（真实驱动 Id 是 <c>opc-ua</c>）。</para>
///
/// <para><b>本文件的扫描面必须覆盖 <c>.cs</c></b>：上一轮的守卫只扫了
/// <c>Inference/PromptTemplates/*.json</c>，于是藏在 <c>.cs</c> 字符串模板里的
/// 那份 21 项清单整整漏了一轮。教训是**守卫的范围本身就是最可能的盲区**。</para>
/// </summary>
public class AiSchemaEnumContractTests
{
    // ── 源码枚举字面量扫描 ──

    /// <summary>定位 <c>"enum": [</c> —— 只匹配**字面量**数组的起头（占位符形态是 <c>{{…}}</c> / <c>[[…]]</c>，匹配不到）。</summary>
    private static readonly Regex EnumLiteralStart = new(@"""enum""\s*:\s*\[", RegexOptions.Compiled);

    /// <summary>
    /// 允许出现在源码里的**非产品枚举**字面量：成员集合 + 为什么它不是产品枚举。
    ///
    /// <para>登记是**有意**的：这些清单没有可以派生的产品侧枚举，只能手写；把它们显式列出来，
    /// 才能让"新增一处手抄清单"变成一个必须过评审的动作。用集合比较，与书写顺序无关。</para>
    /// </summary>
    private static readonly (string[] Members, string Reason)[] NonProductEnumLiterals =
    [
        (["final_answer", "tool_call"],
            "Agent 输出协议判别字段（AgentOutput 约束模型输出的信封格式），非产品侧枚举"),
        (["通用", "电机控制", "温控", "阀门", "泵", "仪表", "自定义"],
            "模板库分类标签：产品侧 ControlTemplate.Category 是**自由字符串**（默认\"通用\"），"
            + "没有可派生的枚举 ⇒ 这是「硬约束了一个自由字段」而非「手抄枚举漂移」。"
            + "已评估并决定单独处理（去掉 enum 或补产品侧清单），暂登记以免守卫在这里假红"),
    ];

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HmiEditor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("未找到仓库根目录（HmiEditor.slnx）");
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>从 <c>[</c> 起找配对的 <c>]</c>，返回内部文本（不含括号）；未闭合返回 null。</summary>
    private static string? ReadArrayBody(string text, int openBracket)
    {
        var depth = 0;
        for (var i = openBracket; i < text.Length; i++)
        {
            if (text[i] == '[') depth++;
            else if (text[i] == ']' && --depth == 0) return text[(openBracket + 1)..i];
        }
        return null;
    }

    /// <summary>扫 <c>src/</c> 下全部 .cs / .json，产出 (相对路径, 数组内容)。</summary>
    private static List<(string File, string Body)> ScanEnumLiterals()
    {
        var root = FindRepoRoot();
        var found = new List<(string, string)>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (ext is not (".cs" or ".json") || IsBuildOutput(file)) continue;

            var text = File.ReadAllText(file);
            foreach (Match m in EnumLiteralStart.Matches(text))
            {
                var open = m.Index + m.Length - 1;      // 指向 '['
                var body = ReadArrayBody(text, open);
                if (body is not null) found.Add((Path.GetRelativePath(root, file), body));
            }
        }

        return found;
    }

    /// <summary>数组内部的成员名（去引号去空白），顺序保留。</summary>
    private static string[] Members(string body) =>
        body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.Trim('"'))
            .ToArray();

    /// <summary>产品侧全部枚举（扫 Core 程序集，新加枚举自动纳入）。</summary>
    private static List<(Type Type, HashSet<string> Members)> ProductEnums() =>
        typeof(HmiControlType).Assembly.GetTypes()
            .Where(t => t.IsEnum)
            .Select(t => (t, Enum.GetNames(t).ToHashSet(StringComparer.Ordinal)))
            .ToList();

    [Fact]
    public void EveryLiteralEnum_InSource_IsEitherAProductEnum_OrRegistered()
    {
        var product = ProductEnums();
        var unregistered = new List<string>();

        foreach (var (file, body) in ScanEnumLiterals())
        {
            // 占位符形态（[[ENUM]] / {{...}} / {control_type_enum}）不是字面量清单，跳过
            if (body.Contains('[') || body.Contains('{')) continue;

            var members = Members(body);
            if (members.Length == 0) continue;

            // 判据：必须**等于**某个产品枚举的成员全集（不是"子集"—— 子集关系会让
            //        "缺了 4 个成员"的手抄清单照样过关，那正是本轮要抓的东西）
            var hitsProduct = product.Any(p => p.Members.SetEquals(members));
            if (hitsProduct) continue;

            if (NonProductEnumLiterals.Any(r => r.Members.ToHashSet(StringComparer.Ordinal).SetEquals(members)))
                continue;

            // 顺带指出它像是哪个产品枚举的"残缺副本"，便于直接定位
            var closest = product
                .Where(p => members.All(p.Members.Contains))
                .Select(p => $"{p.Type.Name}({p.Members.Count} 项, 少 {p.Members.Count - members.Length} 项)")
                .FirstOrDefault();

            unregistered.Add($"{file}: [{string.Join(", ", members)}]" +
                             (closest is null ? "  ← 不是任何产品枚举的子集" : $"  ← 疑似 {closest} 的残缺副本"));
        }

        Assert.True(unregistered.Count == 0,
            "源码里出现了未登记的字面量 enum 清单（要么改成从枚举注入，要么登记进 NonProductEnumLiterals 并写明理由）:\n  " +
            string.Join("\n  ", unregistered));
    }

    /// <summary>
    /// 反向：登记表里不能有"源码中已不存在"的条目 —— 否则登记表会腐化成噪音，下一个人照抄它。
    /// </summary>
    [Fact]
    public void RegisteredNonProductLiterals_StillExistInSource()
    {
        var seen = ScanEnumLiterals()
            .Where(x => !x.Body.Contains('[') && !x.Body.Contains('{'))
            .Select(x => Members(x.Body).ToHashSet(StringComparer.Ordinal))
            .ToList();

        var stale = NonProductEnumLiterals
            .Where(r => !seen.Any(s => s.SetEquals(r.Members)))
            .Select(r => string.Join(", ", r.Members))
            .ToArray();

        Assert.True(stale.Length == 0,
            "NonProductEnumLiterals 里登记了但源码中已不存在的条目（应删除）: " + string.Join(" / ", stale));
    }

    /// <summary>
    /// 扫描面自检：**必须有 .cs 命中**。
    /// 没有这条，扫描器一旦因为路径/扩展名写错而扫不到 .cs，上面那条断言会永远绿（假绿）。
    /// </summary>
    [Fact]
    public void Scanner_ActuallyCoversCsFiles()
    {
        var hits = ScanEnumLiterals();
        Assert.NotEmpty(hits);
        Assert.Contains(hits, h => h.File.EndsWith(".cs", StringComparison.Ordinal));

        // 本组的两个已知字面量必须在扫描结果里（一个 .cs、一个 .json）
        Assert.Contains(hits, h => h.File.EndsWith("AgentOutput.cs", StringComparison.Ordinal));
        Assert.Contains(hits, h => h.File.EndsWith("TemplateGenerationSchema.json", StringComparison.Ordinal));
    }

    // ── AiSchemaEnums 契约 ──

    [Fact]
    public void GeneratableControlTypes_IsEnumMinusScreenContainer()
    {
        var expected = Enum.GetNames<HmiControlType>()
            .Where(n => n != nameof(HmiControlType.Screen))
            .ToArray();

        Assert.Equal(expected, AiSchemaEnums.GeneratableControlTypes);
        Assert.DoesNotContain(nameof(HmiControlType.Screen), AiSchemaEnums.GeneratableControlTypes);
    }

    [Fact]
    public void GeneratableControlTypes_CompactForms_MatchTheList()
    {
        var json = JsonDocument.Parse(AiSchemaEnums.GeneratableControlTypesJson);
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Equal(
            AiSchemaEnums.GeneratableControlTypes,
            json.RootElement.EnumerateArray().Select(e => e.GetString()!).ToArray());

        Assert.Equal(
            string.Join(",", AiSchemaEnums.GeneratableControlTypes),
            AiSchemaEnums.GeneratableControlTypesCsv);
    }

    [Fact]
    public void TryParseGeneratableControlType_AcceptsEveryGeneratableType()
    {
        foreach (var name in AiSchemaEnums.GeneratableControlTypes)
        {
            Assert.True(AiSchemaEnums.TryParseGeneratableControlType(name, out var parsed), name);
            Assert.Equal(Enum.Parse<HmiControlType>(name), parsed);
            // 大小写不敏感
            Assert.True(AiSchemaEnums.TryParseGeneratableControlType(name.ToLowerInvariant(), out _), name);
        }
    }

    [Theory]
    [InlineData(nameof(HmiControlType.Screen), "容器控件是给人类拖的，AI 生成扁平列表造不出有意义的容器")]
    [InlineData("Tank2", "非枚举成员")]
    [InlineData("", "空串")]
    [InlineData("   ", "空白")]
    [InlineData(null, "null")]
    public void TryParseGeneratableControlType_Rejects(string? name, string why)
    {
        Assert.False(AiSchemaEnums.TryParseGeneratableControlType(name, out var parsed), why);
        Assert.Equal(default, parsed);
    }

    [Fact]
    public void ParseGeneratableControlTypeOrLabel_FallsBackToLabel()
    {
        Assert.Equal(HmiControlType.Label, AiSchemaEnums.ParseGeneratableControlTypeOrLabel("Screen"));
        Assert.Equal(HmiControlType.Label, AiSchemaEnums.ParseGeneratableControlTypeOrLabel("不存在的类型"));
        Assert.Equal(HmiControlType.Tank, AiSchemaEnums.ParseGeneratableControlTypeOrLabel("Tank"));
    }

    // ── 各产品枚举的 JsonArray 形态必须是合法 JSON 且逐字等于枚举 ──

    [Fact]
    public void JsonArray_ForEverySchemaEnum_IsValidJsonMatchingTheEnum()
    {
        var cases = new (string Name, string Json, string[] Expected)[]
        {
            ("HmiControlType", AiSchemaEnums.JsonArray<HmiControlType>(), Enum.GetNames<HmiControlType>()),
            ("ParameterType", AiSchemaEnums.JsonArray<ParameterType>(), Enum.GetNames<ParameterType>()),
            ("OperationMode", AiSchemaEnums.JsonArray<OperationMode>(), Enum.GetNames<OperationMode>()),
            ("LogLevel", AiSchemaEnums.JsonArray<LogLevel>(), Enum.GetNames<LogLevel>()),
            ("AlarmConditionType", AiSchemaEnums.JsonArray<AlarmConditionType>(), Enum.GetNames<AlarmConditionType>()),
            ("AlarmSeverity", AiSchemaEnums.JsonArray<AlarmSeverity>(), Enum.GetNames<AlarmSeverity>()),
        };

        foreach (var (name, json, expected) in cases)
        {
            var parsed = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Array, parsed.RootElement.ValueKind);

            // 顺序 = 枚举声明顺序（顺序不稳会让发给模型的 schema 每次不同）
            var actual = parsed.RootElement.EnumerateArray().Select(e => e.GetString()!).ToArray();
            Assert.Equal(expected, actual);
            Assert.NotEmpty(actual);
            Assert.False(string.IsNullOrWhiteSpace(name));
        }
    }
}
