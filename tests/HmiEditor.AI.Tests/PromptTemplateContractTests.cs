using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using HmiEditor.AI.Inference;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// Prompt 模板与 JSON Schema 契约守卫。
///
/// 背景：控件类型清单在「System 提示的 {control_types}」与「JSON Schema 的 enum」里各出现一次。
/// 严格 json_schema 模式下 enum 是**硬约束**，所以两边一旦漂移就会出现
/// 「提示告诉模型支持某类型，Schema 却拒绝它」—— 模型永远生成不出该控件，而提示里又宣告了它。
/// 实测历史漂移：ScreenGenerationSchema 缺 Tank/Pipe/Valve；
/// TemplateGenerationSchema 还额外缺 Rectangle/Ellipse/Polyline/Arc（只列到 18 个）。
///
/// 现在两份 Schema 的控件类型 enum 都由 <see cref="HmiControlType"/> 枚举注入，
/// `parameters[].type` 由 <see cref="ParameterType"/> 注入，本文件把这件事钉住。
///
/// 另一类静默失败：<c>Replace</c> 对不存在的占位符是**无操作**，模板文件缺失时
/// <c>LoadTemplate</c> 也只返回空串 —— 占位符拼错不会有任何报错，只会把 {xxx} 原文
/// 泄漏给模型。ImageGenerationSystem.txt 曾写作 viewBox="0 0 {W} {H}"（未被替换），
/// 模型有照抄成非法 SVG 的风险。本文件同时钉住「模板里出现的占位符都必须已登记」。
///
/// 有意**不**纳入同源化的字段：TemplateGenerationSchema 的 `category` enum
/// （通用/电机控制/…）。产品侧 `ControlTemplate.Category` 是**自由字符串**（默认"通用"），
/// 没有对应枚举可派生 —— 它是「硬约束了一个自由字段」，与「手抄枚举导致漂移」是**两回事**，
/// 见 docs/2026-09-15-ai-prompt-schema-single-source.md 的「已知相邻问题」。
/// </summary>
public class PromptTemplateContractTests
{
    private static readonly string TemplatesDir =
        Path.Combine(AppContext.BaseDirectory, "Inference", "PromptTemplates");

    /// <summary>
    /// 花括号占位符形态。刻意不用更宽的模式：模板里含 JSON/SVG 示例
    /// （如 {"screen": {...}}），其花括号后跟引号或点号，不应被当成占位符。
    /// </summary>
    private static readonly Regex PlaceholderPattern =
        new(@"\{[A-Za-z_][A-Za-z0-9_]*\}", RegexOptions.Compiled);

    private static PromptTemplateEngine NewEngine() => new(TemplatesDir);

    private static HmiProject NewProject() => new();

    private static string[] TemplateFiles() =>
        Directory.GetFiles(TemplatesDir, "*", SearchOption.TopDirectoryOnly);

    private static IEnumerable<string> PlaceholdersIn(string fileContent) =>
        PlaceholderPattern.Matches(fileContent).Select(m => m.Value);

    // ── 模板部署 ──

    [Fact]
    public void TemplateDirectory_IsDeployedNextToTheAssembly()
    {
        Assert.True(Directory.Exists(TemplatesDir),
            $"模板目录未随构建部署到输出目录: {TemplatesDir}");

        var files = TemplateFiles();
        // 5 组生成器（Screen/Template/Script/Icon/Image）× (System.txt + User.txt) + 3 个 Schema
        Assert.True(files.Count(f => f.EndsWith(".txt", StringComparison.Ordinal)) >= 10,
            $"模板 .txt 数量异常: {files.Length} 个文件");
        Assert.True(files.Count(f => f.EndsWith(".json", StringComparison.Ordinal)) >= 3,
            $"Schema .json 数量异常: {files.Length} 个文件");
    }

    // ── 占位符登记（双向） ──

    [Fact]
    public void EveryPlaceholder_InEveryTemplate_IsRegistered()
    {
        var unregistered = new List<string>();
        foreach (var file in TemplateFiles())
        {
            foreach (var ph in PlaceholdersIn(File.ReadAllText(file)))
            {
                var name = ph[1..^1];
                if (!PromptTemplateEngine.KnownPlaceholders.Contains(name))
                    unregistered.Add($"{Path.GetFileName(file)}: {ph}");
            }
        }

        Assert.True(unregistered.Count == 0,
            "模板里出现未登记的占位符（Replace 不会替换它，会原文泄漏给模型）:\n  " +
            string.Join("\n  ", unregistered));
    }

    [Fact]
    public void EveryRegisteredPlaceholder_IsUsedBySomeTemplate()
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in TemplateFiles())
            foreach (var ph in PlaceholdersIn(File.ReadAllText(file)))
                used.Add(ph[1..^1]);

        var unused = PromptTemplateEngine.KnownPlaceholders.Where(p => !used.Contains(p)).ToArray();
        Assert.True(unused.Length == 0,
            "KnownPlaceholders 里登记了但没有任何模板使用的占位符（死登记，应删除）: " +
            string.Join(", ", unused));
    }

    // ── 提示词正文里的手抄清单（enum 扫描覆盖不到的一类） ──

    /// <summary>
    /// 「支持 A, B, C, D」这种**用逗号串起 ≥4 个控件类型名**的正文片段，必须改成
    /// <c>{control_types}</c> 占位符。
    ///
    /// <para><b>为什么单独守这一条</b>：它既不是 JSON schema 的 <c>enum</c>（静态扫描看不到），
    /// 也不是占位符（登记守卫看不到）—— 是一段**散文**。
    /// 实测：<c>TemplateGenerationSystem.txt</c> 一直手抄着 18 个类型名（真值 25，
    /// 缺 <c>Tank/Pipe/Valve/Rectangle/Ellipse/Polyline/Arc</c>），而同一个生成器的 schema 的
    /// <c>enum</c> 已宣告支持它们 ⇒ 模型被提示词告知"不支持"，被 schema 告知"支持"。</para>
    ///
    /// <para>判据用「逗号/顿号/斜杠串起 ≥4 个名字」而不是「出现 ≥N 个名字」，
    /// 后者会被正常散文误伤（例："Label 在数值控件上方"、"text: 文本内容（Label/Button 使用）"）。</para>
    /// </summary>
    [Fact]
    public void PromptTemplates_ListingControlTypes_UseThePlaceholder()
    {
        var alternation = string.Join("|", ExpectedControlTypes().Select(Regex.Escape));
        var copiedList = new Regex(
            $@"(?:{alternation})(?:\s*[,、/]\s*(?:{alternation})){{3,}}",
            RegexOptions.Compiled);

        var problems = new List<string>();
        foreach (var file in TemplateFiles().Where(f => f.EndsWith(".txt", StringComparison.Ordinal)))
        {
            var content = File.ReadAllText(file);
            var hit = copiedList.Match(content);
            if (!hit.Success) continue;

            // 同一份文件里既是占位符又留着手抄清单 → 手抄那份仍是漂移源，照样要报
            problems.Add($"{Path.GetFileName(file)}: 正文里手抄了控件类型清单 " +
                         $"“{hit.Value}” —— 应改用 {{control_types}} 占位符");
        }

        Assert.True(problems.Count == 0, string.Join("\n  ", problems));
    }

    /// <summary>
    /// 反向：<b>凡手抄过清单的模板，其 System 提示必须真的被注入过</b> ——
    /// 否则把清单换成 <c>{control_types}</c> 只是把"漂移"换成了"泄漏占位符"。
    /// （由 <see cref="RenderedPrompts_LeaveNoUnreplacedPlaceholder"/> 兜底，这里给出更早的语义信号。）
    /// </summary>
    [Fact]
    public void TemplateSystemPrompt_ActuallyReceivesControlTypes()
    {
        var system = NewEngine().BuildTemplateMessages("温控面板", NewProject())
            .Single(m => m.Role == "system");

        foreach (var name in ExpectedControlTypes())
            Assert.Contains(name, system.Content);

        Assert.DoesNotContain("{control_types}", system.Content);
    }

    // ── 渲染结果无残留 ──

    [Fact]
    public void RenderedPrompts_LeaveNoUnreplacedPlaceholder()
    {
        var engine = NewEngine();
        var project = NewProject();

        var conversations = new (string Name, List<LlmMessage> Messages)[]
        {
            ("Screen", engine.BuildScreenMessages("温度监控界面", project)),
            ("Template", engine.BuildTemplateMessages("温控面板模板", project)),
            ("Script", engine.BuildScriptMessages("报警时记录日志", project)),
            ("Icon", engine.BuildIconMessages("电机启动")),
            ("Image", engine.BuildImageMessages("循环水泵示意图", 512, 256)),
        };

        var leftovers = new List<string>();
        foreach (var (name, messages) in conversations)
            foreach (var msg in messages)
                leftovers.AddRange(PlaceholdersIn(msg.Content ?? "")
                    .Select(ph => $"{name}/{msg.Role}: {ph}"));

        Assert.True(leftovers.Count == 0,
            "渲染后的提示词里仍有未替换的占位符:\n  " + string.Join("\n  ", leftovers));
    }

    [Fact]
    public void ImagePrompt_InjectsConcreteCanvasSizeIntoBothSystemAndUser()
    {
        var messages = NewEngine().BuildImageMessages("示意图", 512, 256);

        foreach (var msg in messages)
        {
            Assert.Contains("512", msg.Content);
            Assert.Contains("256", msg.Content);
        }
    }

    // ── Schema enum 与枚举同源 ──

    private static string[] ScreenSchemaEnum() => ExtractEnum(
        NewEngine().GetScreenSchema(),
        "properties", "screen", "properties", "controls", "items", "properties", "type", "enum");

    private static string[] TemplateSchemaEnum() => ExtractEnum(
        NewEngine().GetTemplateSchema(),
        "properties", "template", "properties", "controls", "items", "properties", "type", "enum");

    private static string[] ExtractEnum(string schemaJson, params string[] path)
    {
        using var doc = JsonDocument.Parse(schemaJson);
        var el = doc.RootElement;
        foreach (var segment in path)
            el = el.GetProperty(segment);

        Assert.Equal(JsonValueKind.Array, el.ValueKind);
        return el.EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    private static string[] ExpectedControlTypes() =>
        PromptTemplateEngine.ControlTypeNames.ToArray();

    [Fact]
    public void ScreenSchema_ControlTypeEnum_MatchesHmiControlType()
        => Assert.Equal(ExpectedControlTypes(), ScreenSchemaEnum());

    [Fact]
    public void TemplateSchema_ControlTypeEnum_MatchesHmiControlType()
        => Assert.Equal(ExpectedControlTypes(), TemplateSchemaEnum());

    [Fact]
    public void BothSchemas_ExcludeScreenRootContainer()
    {
        foreach (var names in new[] { ScreenSchemaEnum(), TemplateSchemaEnum() })
            Assert.DoesNotContain(nameof(HmiControlType.Screen), names);
    }

    private static string[] TemplateSchemaParameterTypeEnum() => ExtractEnum(
        NewEngine().GetTemplateSchema(),
        "properties", "template", "properties", "parameters", "items", "properties", "type", "enum");

    [Fact]
    public void TemplateSchema_ParameterTypeEnum_MatchesParameterType()
        => Assert.Equal(Enum.GetNames<ParameterType>(), TemplateSchemaParameterTypeEnum());

    /// <summary>
    /// 枚举顺序决定注入后的 enum 顺序 —— 顺序不稳会让发给模型的 Schema 每次不同，
    /// 破坏"同一输入得到同一提示词"（影响缓存命中与 A/B 对比）。
    /// </summary>
    [Fact]
    public void ControlTypeNames_FollowEnumDeclarationOrder()
    {
        var expected = Enum.GetNames<HmiControlType>()
            .Where(n => n != nameof(HmiControlType.Screen))
            .ToArray();

        Assert.Equal(expected, ExpectedControlTypes());
    }

    /// <summary>同一条理由：参数类型清单也必须直接来自枚举，且顺序稳定。</summary>
    [Fact]
    public void ParameterTypeNames_MatchEnumExactly()
    {
        Assert.Equal(Enum.GetNames<ParameterType>(), PromptTemplateEngine.ParameterTypeNames);
        Assert.Empty(PromptTemplateEngine.ParameterTypeNames.Except(Enum.GetNames<ParameterType>()));
    }

    /// <summary>
    /// Schema 的唯一用途是发给 provider 作为 response_format.json_schema，
    /// 所以渲染后必须是**合法 JSON**，否则 provider 侧解析失败 → 静默降级为无约束。
    /// </summary>
    [Fact]
    public void BothSchemas_AreValidJsonAfterRender()
    {
        foreach (var json in new[] { NewEngine().GetScreenSchema(), NewEngine().GetTemplateSchema() })
        {
            var ex = Record.Exception(() =>
            {
                using var doc = JsonDocument.Parse(json);
                Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
            });
            Assert.True(ex is null, $"渲染后的 Schema 不是合法 JSON: {ex?.Message}");
        }
    }

    private static (string Name, string Json)[] AllSchemas()
    {
        var engine = NewEngine();
        return
        [
            ("Screen", engine.GetScreenSchema()),
            ("Template", engine.GetTemplateSchema()),
            ("Script", engine.GetScriptSchema()),
        ];
    }

    /// <summary>
    /// **每一个** <c>Get*Schema()</c> 都必须走渲染链。
    ///
    /// <para>曾经的漏洞：<c>GetScriptSchema()</c> 直接 <c>LoadTemplate</c>（没进 <c>RenderSchema</c>），
    /// 所以一旦它的 schema 文件里出现占位符，那个 <c>{xxx}</c> 会**原样发给模型**
    /// —— <c>Replace</c> 对不存在的占位符是静默无操作。加可选枚举时正好踩中这条。</para>
    /// </summary>
    [Fact]
    public void AllSchemas_LeaveNoUnreplacedPlaceholder_AndAreValidJson()
    {
        var problems = new List<string>();
        foreach (var (name, json) in AllSchemas())
        {
            foreach (var ph in PlaceholdersIn(json))
                problems.Add($"{name}: 未替换的占位符 {ph}");

            var ex = Record.Exception(() =>
            {
                using var doc = JsonDocument.Parse(json);
                Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
            });
            if (ex is not null) problems.Add($"{name}: 不是合法 JSON —— {ex.Message}");
        }

        Assert.True(problems.Count == 0, string.Join("\n  ", problems));
    }

    /// <summary>
    /// <c>AllSchemas()</c> 是**手抄清单**：办法上新增一个 <c>Get*Schema()</c> 却忘了登记，
    /// 上面那条渲染守卫就完全看不到它 —— 属于「扫描面本身就是盲区」。
    /// 这里用反射把两边钉成**双向**：声明了几个 <c>Get*Schema</c>，就必须覆盖几个。
    /// </summary>
    [Fact]
    public void AllSchemas_CoversEveryGetSchemaMethod()
    {
        var declared = typeof(PromptTemplateEngine)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.ReturnType == typeof(string)
                        && m.GetParameters().Length == 0
                        && m.Name.StartsWith("Get", StringComparison.Ordinal)
                        && m.Name.EndsWith("Schema", StringComparison.Ordinal))
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        var covered = AllSchemas()
            .Select(s => $"Get{s.Name}Schema")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.True(covered.SequenceEqual(declared),
            "AllSchemas() 与 PromptTemplateEngine 上的 Get*Schema 方法不一致 —— " +
            "新增 schema 方法必须登记进 AllSchemas()，否则渲染守卫会静默漏掉它。\n  " +
            $"声明: {string.Join(", ", declared)}\n  " +
            $"已覆盖: {string.Join(", ", covered)}");
    }
}
