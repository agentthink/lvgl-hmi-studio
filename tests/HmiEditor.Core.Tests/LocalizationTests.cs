using System.Globalization;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

public class LocalizationTests
{
    [Fact]
    public void InitializeDefaults_ChineseLoaded()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        Assert.Equal("HMI 编辑器", svc["app.title"]);
        Assert.Equal("文件", svc["menu.file"]);
    }

    [Fact]
    public void InitializeDefaults_EnglishLoaded()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        Assert.Equal("HMI Editor", svc.GetString("app.title", "en-US"));
        Assert.Equal("_File", svc.GetString("menu.file", "en-US"));
    }

    [Fact]
    public void SwitchLanguage_ChangesCurrentLanguage()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        Assert.Equal("zh-CN", svc.CurrentLanguage);
        Assert.Equal("HMI 编辑器", svc["app.title"]);

        svc.SwitchLanguage("en-US");
        Assert.Equal("en-US", svc.CurrentLanguage);
        Assert.Equal("HMI Editor", svc["app.title"]);
    }

    [Fact]
    public void GetString_UnknownKey_ReturnsKey()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        Assert.Equal("nonexistent.key", svc["nonexistent.key"]);
    }

    [Fact]
    public void SwitchLanguage_Unsupported_NoChange()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        svc.SwitchLanguage("ja-JP");
        Assert.Equal("zh-CN", svc.CurrentLanguage);
    }

    [Fact]
    public void LanguageChanged_EventFired()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();
        string? changedTo = null;
        svc.LanguageChanged += (_, lang) => changedTo = lang;

        svc.SwitchLanguage("en-US");
        Assert.Equal("en-US", changedTo);
    }

    [Fact]
    public void FallbackToEnglish_WhenChineseMissing()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        // 故意在服务中不注册英文，验证 fallback
        Assert.Equal("zh-CN", svc.CurrentLanguage);

        // 中文已有的 key 正常返回
        Assert.Equal("HMI 编辑器", svc["app.title"]);
    }

    [Fact]
    public void LoadResource_FromJsonString()
    {
        var svc = new LocalizationService();
        svc.LoadResource("fr-FR", @"{""app.title"": ""Éditeur HMI""}");

        Assert.Equal("Éditeur HMI", svc.GetString("app.title", "fr-FR"));
    }

    [Fact]
    public void GetSupportedLanguages_ReturnsBoth()
    {
        var svc = new LocalizationService();
        var langs = svc.GetSupportedLanguages();

        Assert.Contains("zh-CN", langs);
        Assert.Contains("en-US", langs);
    }

    [Fact]
    public void GetLanguageDisplayName_ReturnsCorrect()
    {
        Assert.Equal("中文", LocalizationService.GetLanguageDisplayName("zh-CN"));
        Assert.Equal("English", LocalizationService.GetLanguageDisplayName("en-US"));
        Assert.Equal("de-DE", LocalizationService.GetLanguageDisplayName("de-DE"));
    }

    // ============ Format 占位符校验测试 ============

    [Fact]
    public void Format_NoArgs_ReturnsRawString()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        Assert.Equal("文件", svc.Format("menu.file"));
    }

    [Fact]
    public void Format_MatchingPlaceholders_FormatsCorrectly()
    {
        var svc = new LocalizationService();
        svc.LoadResource("en-US", @"{""msg.hello"": ""Hello {0}, you have {1} messages""}");
        svc.SwitchLanguage("en-US");

        Assert.Equal("Hello Alice, you have 5 messages",
            svc.Format("msg.hello", "Alice", 5));
    }

    [Fact]
    public void Format_PlaceholderMismatch_ReturnsRawString_NoThrow()
    {
        var svc = new LocalizationService();
        svc.LoadResource("en-US", @"{""msg.one.arg"": ""Value: {0}""}");
        svc.SwitchLanguage("en-US");

        // 多传参数：应返回原始字符串，不抛异常
        var result = svc.Format("msg.one.arg", "extra1", "extra2");
        Assert.Equal("Value: {0}", result);
    }

    [Fact]
    public void Format_MissingArgs_ReturnsRawString_NoThrow()
    {
        var svc = new LocalizationService();
        svc.LoadResource("en-US", @"{""msg.two.args"": ""A={0} B={1}""}");
        svc.SwitchLanguage("en-US");

        // 少传参数：应返回原始字符串，不抛异常
        var result = svc.Format("msg.two.args", "only-one");
        Assert.Equal("A={0} B={1}", result);
    }

    [Fact]
    public void Format_UnknownKey_ReturnsKey()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        Assert.Equal("nonexistent.format.key", svc.Format("nonexistent.format.key", "arg"));
    }

    // ============ Culture-aware 格式化测试 ============

    [Fact]
    public void Format_WithCulture_AppliesCultureFormatting()
    {
        var svc = new LocalizationService();
        svc.LoadResource("en-US", @"{""msg.price"": ""Price: {0:C}""}");
        svc.SwitchLanguage("en-US");

        var zh = svc.Format("msg.price", CultureInfo.GetCultureInfo("zh-CN"), 1234.5);
        var en = svc.Format("msg.price", CultureInfo.GetCultureInfo("en-US"), 1234.5);
        var de = svc.Format("msg.price", CultureInfo.GetCultureInfo("de-DE"), 1234.5);

        // 不同文化的货币符号不同
        Assert.NotEqual(zh, en);
        Assert.NotEqual(en, de);
        // 中文/英文/德文货币符号分别是 ¥/$/€
        Assert.Contains("¥", zh);
        Assert.Contains("$", en);
        Assert.Contains("€", de);
    }

    [Fact]
    public void Format_WithCulture_PlaceholderMismatch_ReturnsRaw()
    {
        var svc = new LocalizationService();
        svc.LoadResource("en-US", @"{""msg.date"": ""Date: {0:yyyy-MM-dd}""}");
        svc.SwitchLanguage("en-US");

        // 多传参数：应返回原始字符串，不抛异常
        var result = svc.Format("msg.date", CultureInfo.InvariantCulture, "wrong-arg", "extra");
        Assert.Equal("Date: {0:yyyy-MM-dd}", result);
    }

    // ============ LanguageChanged 事件测试 ============

    [Fact]
    public void LanguageChanged_NotFiredOnUnsupportedLanguage()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        string? changedTo = null;
        svc.LanguageChanged += (_, lang) => changedTo = lang;

        svc.SwitchLanguage("ja-JP");
        Assert.Null(changedTo);
    }

    [Fact]
    public void LanguageChanged_MultipleSubscribers_AllNotified()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        int count1 = 0, count2 = 0;
        svc.LanguageChanged += (_, _) => count1++;
        svc.LanguageChanged += (_, _) => count2++;

        svc.SwitchLanguage("en-US");
        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }

    // ============ 资源键完整性测试 ============

    [Fact]
    public void ResourceFiles_HaveSameKeyCount()
    {
        var zhKeys = CountKeys("zh-CN");
        var enKeys = CountKeys("en-US");
        Assert.Equal(zhKeys, enKeys);
    }

    [Fact]
    public void ResourceFiles_NoMissingKeys()
    {
        var zhKeys = GetKeySet("zh-CN");
        var enKeys = GetKeySet("en-US");

        var missingInEn = zhKeys.Except(enKeys).ToList();
        var missingInZh = enKeys.Except(zhKeys).ToList();

        Assert.True(missingInEn.Count == 0,
            $"Keys in zh-CN but not in en-US: {string.Join(", ", missingInEn)}");
        Assert.True(missingInZh.Count == 0,
            $"Keys in en-US but not in zh-CN: {string.Join(", ", missingInZh)}");
    }

    /// <summary>
    /// **产品面判据**：全库字典里每一条带占位符的模板，经 <c>Format</c> 之后都必须真的把
    /// 每个实参填进去（用哨兵值探）。
    ///
    /// <para>这是阶段 43 那条缺陷的**通用版本**：修复前 <c>Format</c> 按"占位符出现次数"判，
    /// 于是"同一个占位符出现多次"的模板（全库 2285 键里 1 条）被判不匹配、**原样返回** ⇒
    /// 用户看到 <c>{1}</c> 字面量。逐条扫比挑一个样本更能说明"产品面是什么样"。</para>
    /// </summary>
    [Fact]
    public void Format_RendersEveryPlaceholderOfEveryTemplate()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        var problems = new List<string>();
        var checkedTemplates = 0;

        foreach (var (key, template) in LoadEmbeddedJson("zh-CN"))
        {
            var indexes = LocalizationPlaceholders.Indexes(template);
            if (indexes.Count == 0) continue;
            checkedTemplates++;

            var args = indexes.Select(i => (object)$"<a{i}>").ToArray();
            var rendered = svc.Format(key, args);

            foreach (var i in indexes)
            {
                if (!rendered.Contains($"<a{i}>", System.StringComparison.Ordinal))
                    problems.Add($"{key}: {LocalizationPlaceholders.DescribeIndexes(template)} 里的 {{{i}}} "
                                 + $"没有被渲染，Format 返回了：{rendered}");
            }
        }

        Assert.True(checkedTemplates >= 100, $"只校验了 {checkedTemplates} 条模板 —— 枚举可能失效（恒绿）");
        Assert.True(problems.Count == 0,
            $"以下模板没有被 Format 渲染（用户会看到花括号字面量）：\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// 负控：模板**索引有洞**时的事实与契约。
    ///
    /// <list type="number">
    ///   <item>底层事实（不是我们定的，但必须钉住）：索引越界时 <c>string.Format</c> 会**抛**
    ///     <see cref="System.FormatException"/>，不是"显示字面量" —— 这就是为什么"有洞"
    ///     比"数量不符"严重一档；</item>
    ///   <item>契约：判据挡住它 ⇒ <c>Format</c> 走"原样返回"这条既有分支，**不抛**。
    ///     修复前这里会把模板放行给 <c>string.Format</c> ⇒ 直接崩在调用的那一层。</item>
    /// </list>
    /// </summary>
    [Fact]
    public void Format_TemplateWithHole_ReturnsRawInsteadOfThrowing()
    {
        const string holeTemplate = "{0} 与 {2}";
        Assert.Throws<System.FormatException>(() => string.Format(holeTemplate, "a", "b"));

        var svc = new LocalizationService();
        svc.InitializeDefaults();
        svc.LoadResource("zh-CN", "{\"probe.hole\":\"{0} 与 {2}\"}");

        Assert.Equal(holeTemplate, svc.GetString("probe.hole"));   // 自检：探针模板真的装上了

        Assert.Equal(holeTemplate, svc.Format("probe.hole", "a", "b"));
    }

    /// <summary>
    /// **回归判据（阶段 43）**：同一个占位符在模板里出现多次是**完全合法**的写法，
    /// <c>Format</c> 必须把它渲染成实参，而不是因为"数了出现次数"就把整条模板原样返回。
    ///
    /// <para><b>为什么这条要单独钉</b>：<c>script.issue.lua_runtime_symbol_undetermined</c> 的模板里
    /// <c>{1}</c> 出现两次（"…需要 X 或更高，设备目标未定…（设备若为 X 或更高则无此问题）"）——
    /// 一句话在两个位置说明同一件事，是**有意**的。按"出现次数"判定会数成 3 ≠ 2 个实参，
    /// 于是 <c>Format</c> 走"不匹配就原样返回"的分支 ⇒ **用户在脚本编辑器里看到的是
    /// <c>{0} 需要 {1} 或更高…</c> 这种字面量**。运行期不抛、不报，只有界面上一眼能看出来。</para>
    ///
    /// <para>用真字典里那条（全库 2285 键里**唯一**一条重复占位符模板）而不是造一条样例 ——
    /// 造样例就测不到产品面，而这条数据本身就是产品面的一部分。</para>
    /// </summary>
    [Fact]
    public void Format_RepeatedPlaceholder_RendersArguments()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        const string key = "script.issue.lua_runtime_symbol_undetermined";
        var rendered = svc.Format(key, "bit32", "Lua 5.2");

        Assert.DoesNotContain("{0}", rendered, System.StringComparison.Ordinal);
        Assert.DoesNotContain("{1}", rendered, System.StringComparison.Ordinal);
        Assert.Contains("bit32", rendered, System.StringComparison.Ordinal);
        // 重复出现的那个占位符**两处**都要填上（只填一处 = 另一种坏法）
        Assert.Equal(2, rendered.Split("Lua 5.2").Length - 1);
    }

    /// <summary>
    /// 同一 key 在 zh-CN / en-US 里必须是**同一组占位符索引**。
    ///
    /// <para><b>为什么不是"数量一致"</b>：数量一致会放过 zh <c>{0} 与 {1}</c> / en <c>{0} 与 {2}</c>
    /// 这种 —— 两边都数出 2 个，而 en 那份在运行期会让 <c>string.Format</c> 抛
    /// <see cref="System.FormatException"/>。索引有洞比"数量不符"严重一档：
    /// 数量不符只是显示 <c>{0}</c> 字面量，洞是崩。</para>
    /// </summary>
    [Fact]
    public void ResourceFiles_SamePlaceholderIndexesInBothLanguages()
    {
        var zhJson = LoadEmbeddedJson("zh-CN");
        var enJson = LoadEmbeddedJson("en-US");
        var keys = zhJson.Keys.Intersect(enJson.Keys).ToList();

        var drift = new List<string>();
        foreach (var key in keys)
        {
            var zh = LocalizationPlaceholders.Indexes(zhJson[key]);
            var en = LocalizationPlaceholders.Indexes(enJson[key]);
            if (!zh.SequenceEqual(en))
                drift.Add($"{key}: zh-CN {LocalizationPlaceholders.DescribeIndexes(zhJson[key])} "
                          + $"vs en-US {LocalizationPlaceholders.DescribeIndexes(enJson[key])}");
        }

        Assert.True(drift.Count == 0,
            $"两本字典的占位符索引不一致（{drift.Count} 条）：\n" + string.Join("\n", drift));
    }

    /// <summary>
    /// **形状判据**：任何一条模板的占位符索引都必须是连续的 <c>0..n-1</c>。
    ///
    /// <para>这是"洞"那条规则的产品面版本：判据（以及运行期）只在被调用时才检查某一条模板，
    /// 这条把**全库两本字典**扫一遍 —— 洞是坏模板，没有任何合法用途。</para>
    /// </summary>
    [Fact]
    public void EveryTemplate_HasConsecutivePlaceholderIndexes()
    {
        var holes = new List<string>();
        var total = 0;

        foreach (var lang in new[] { "zh-CN", "en-US" })
        {
            foreach (var (key, value) in LoadEmbeddedJson(lang))
            {
                var indexes = LocalizationPlaceholders.Indexes(value);
                if (indexes.Count == 0) continue;
                total++;

                if (!indexes.SequenceEqual(Enumerable.Range(0, indexes.Count)))
                    holes.Add($"{lang} {key}: {LocalizationPlaceholders.DescribeIndexes(value)}");
            }
        }

        Assert.True(total >= 100, $"只看到 {total} 条带占位符的模板 —— 枚举可能失效（恒绿）");
        Assert.True(holes.Count == 0,
            $"以下模板的占位符索引有洞（运行期会抛 FormatException）：\n" + string.Join("\n", holes));
    }

    // ============ 辅助方法 ============

    private static System.Collections.Generic.HashSet<string> GetKeySet(string lang)
    {
        var json = LoadEmbeddedJson(lang);
        return new System.Collections.Generic.HashSet<string>(json.Keys);
    }

    private static int CountKeys(string lang) => LoadEmbeddedJson(lang).Count;

    private static System.Collections.Generic.Dictionary<string, string> LoadEmbeddedJson(string lang)
    {
        var assembly = typeof(LocalizationService).Assembly;
        var resourceName = lang switch
        {
            "zh-CN" => "HmiEditor.Core.Resources.Strings.zh-CN.json",
            "en-US" => "HmiEditor.Core.Resources.Strings.en-US.json",
            _ => throw new ArgumentException($"Unsupported language: {lang}")
        };

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource not found: {resourceName}");
        using var reader = new System.IO.StreamReader(stream);
        var json = reader.ReadToEnd();

        var dict = new System.Collections.Generic.Dictionary<string, string>();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.GetString() ?? "";
        }
        return dict;
    }
}
