using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hmi.Communication.Builtin;
using HmiEditor.AI;
using HmiEditor.AI.Agent;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// Agent 工具 <c>ArgumentsSchema</c> 的契约守卫 —— **渲染后的结果**。
///
/// <para>为什么必须有这一层（静态扫描守不住的部分）：工具 schema 是用
/// <c>$$"""…"""</c> 内插原始字符串拼出来的，enum 由 <see cref="AiSchemaEnums"/> 的
/// 表达式注入。**源码扫描只能看到注入表达式，看不到拼出来长什么样** ——
/// 括号位置写错、占位符放错层，产出的都是非法 JSON，而 provider 拿到非法
/// <c>response_format.json_schema</c> 会静默降级成"无约束"，行为退化成裸提示。
/// 静态那份守卫在 <c>HmiEditor.AI.Tests/AiSchemaEnumContractTests.cs</c>。</para>
///
/// <para>本文件同时钉住每处 enum 与产品侧枚举逐字一致（值 + 顺序）。</para>
/// </summary>
public class AgentToolSchemaContractTests
{
    private static List<AgentTool> CreateTools() =>
        AgentToolFactory.CreateAll(
            new AgentHostBridge
            {
                GetProject = static () => new HmiProject(),
                ReplaceProject = static _ => { },
                MarkChanged = static () => { },
                RefreshPanels = static () => { },
                OpenPanel = static _ => { },
                SwitchScreen = static _ => { },
                GetAuthService = static () => new AuthenticationService(),
                GetRecipeManager = static () => new RecipeManager(),
                SaveProject = static () => Task.CompletedTask,
                ExportCode = static () => Task.CompletedTask,
            },
            static (_, _) => { },
            static () => { });

    /// <summary>深度收集 schema 里所有 <c>enum</c> 数组 —— 返回 (属性所在路径, 成员)。</summary>
    private static List<(string Path, string[] Members)> CollectEnums(JsonElement element, string path = "")
    {
        var found = new List<(string, string[])>();
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name == "enum" && prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        // 非字符串元素（例如"枚举里套了一层数组"）用 <Kind> 占位，
                        // 让断言能报出来，而不是在这里抛 InvalidOperationException
                        found.Add((path,
                            prop.Value.EnumerateArray()
                                .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : $"<{e.ValueKind}>")
                                .ToArray()));
                    }
                    found.AddRange(CollectEnums(prop.Value, $"{path}.{prop.Name}"));
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    found.AddRange(CollectEnums(item, path));
                break;
        }
        return found;
    }

    [Fact]
    public void EveryToolSchema_IsValidJsonObject_WithNonEmptyStringEnums()
    {
        var problems = new List<string>();
        var toolCount = 0;

        foreach (var tool in CreateTools())
        {
            toolCount++;
            if (string.IsNullOrWhiteSpace(tool.ArgumentsSchema)) continue;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(tool.ArgumentsSchema);
            }
            catch (JsonException ex)
            {
                problems.Add($"{tool.Name}: schema 不是合法 JSON —— {ex.Message}");
                continue;
            }

            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    problems.Add($"{tool.Name}: schema 根不是对象");

                foreach (var (path, members) in CollectEnums(doc.RootElement))
                {
                    if (members.Length == 0) problems.Add($"{tool.Name}{path}: enum 为空数组");
                    if (members.Any(string.IsNullOrWhiteSpace))
                        problems.Add($"{tool.Name}{path}: enum 含空白成员");
                    if (members.Any(m => m.StartsWith('<') && m.EndsWith('>')))
                        problems.Add($"{tool.Name}{path}: enum 成员不是字符串 —— " +
                                     string.Join(", ", members.Where(m => m.StartsWith('<'))));
                    if (members.Distinct().Count() != members.Length)
                        problems.Add($"{tool.Name}{path}: enum 成员重复");
                }
            }
        }

        Assert.True(toolCount > 10, $"工具数异常（{toolCount}），工厂可能没返回全部工具");
        Assert.True(problems.Count == 0, string.Join("\n  ", problems));
    }

    /// <summary>按工具名 + 属性名取出该处的 enum（找不到则抛，避免"断言了一个不存在的属性"变成恒绿）。</summary>
    private static string[] EnumOf(string toolName, string property)
    {
        var tool = CreateTools().Single(t => t.Name == toolName);
        Assert.False(string.IsNullOrWhiteSpace(tool.ArgumentsSchema), $"{toolName} 没有 ArgumentsSchema");

        using var doc = JsonDocument.Parse(tool.ArgumentsSchema!);
        var hit = CollectEnums(doc.RootElement)
            .Where(e => e.Path.EndsWith($".{property}", StringComparison.Ordinal))
            .ToArray();

        Assert.True(hit.Length == 1,
            $"{toolName} 里没有恰好一处 '{property}' 的 enum（实际 {hit.Length} 处）—— 断言会失去意义");
        return hit[0].Members;
    }

    [Theory]
    [InlineData("add_control", "type")]
    [InlineData("add_controls_batch", "type")]
    [InlineData("add_control_to_template", "type")]
    public void ControlTypeEnums_MatchGeneratableControlTypes(string tool, string property)
        => Assert.Equal(AiSchemaEnums.GeneratableControlTypes, EnumOf(tool, property));

    [Theory]
    [InlineData("add_control", "type")]
    [InlineData("add_controls_batch", "type")]
    [InlineData("add_control_to_template", "type")]
    public void ControlTypeEnums_DoNotOfferScreenContainer(string tool, string property)
        => Assert.DoesNotContain(nameof(HmiControlType.Screen), EnumOf(tool, property));

    [Fact]
    public void CreateAlarm_Enums_MatchProductEnums()
    {
        Assert.Equal(Enum.GetNames<AlarmConditionType>(), EnumOf("create_alarm", "type"));
        Assert.Equal(Enum.GetNames<AlarmSeverity>(), EnumOf("create_alarm", "severity"));
    }

    [Fact]
    public void UpdateSecurity_OperationModeEnum_MatchesProductEnum()
        => Assert.Equal(Enum.GetNames<OperationMode>(), EnumOf("update_security", "operation_mode"));

    [Fact]
    public void UpdateRuntime_LogLevelEnum_MatchesProductEnum()
        => Assert.Equal(Enum.GetNames<LogLevel>(), EnumOf("update_runtime", "log_level"));

    [Fact]
    public void CreateConnection_DriverEnum_MatchesBuiltinDriverCatalog()
        => Assert.Equal(BuiltinDriverCatalog.Ids, EnumOf("create_connection", "driver"));

    /// <summary>
    /// 取值互不相同的各枚举让断言有区分度：控件类型与告警条件不该"恰好一样长"。
    /// （诊断用 —— 若两条 enum 意外相同，上面那些 Equal 断言会互相掩盖。）
    /// </summary>
    [Fact]
    public void DistinctSchemaEnums_AreNotAccidentallyIdentical()
    {
        var sets = new (string Name, string[] Members)[]
        {
            ("controlTypes", EnumOf("add_control", "type")),
            ("alarmCondition", EnumOf("create_alarm", "type")),
            ("alarmSeverity", EnumOf("create_alarm", "severity")),
            ("operationMode", EnumOf("update_security", "operation_mode")),
            ("logLevel", EnumOf("update_runtime", "log_level")),
            ("driver", EnumOf("create_connection", "driver")),
        };

        foreach (var a in sets)
            foreach (var b in sets)
                if (!ReferenceEquals(a.Members, b.Members) && a.Name != b.Name)
                    Assert.False(a.Members.SequenceEqual(b.Members), $"{a.Name} 与 {b.Name} 的 enum 完全相同");
    }

    // ── 「AI 工具不得广告没有读取方的配置字段」 ──

    /// <summary>PascalCase 字段名 → 工具 schema 用的 snake_case 键（<c>WatchdogTimeoutMs</c> → <c>watchdog_timeout_ms</c>）。</summary>
    private static string ToSnakeCase(string pascal) =>
        Regex.Replace(pascal, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();

    /// <summary>
    /// 允许出现字段名、但**不构成"有读取方"**的落点：AI 工具与设置面板都会写这些字段，
    /// 那是「写入面」；声明类型文件只有属性定义。判"有没有执行路径"必须看白名单之外。
    /// </summary>
    private static readonly string[] RuntimeConfigWriteOnlySites =
    {
        "src/HmiEditor.App/Services/AgentToolFactory.cs",
        "src/HmiEditor.App/Services/ConfigFieldInventory.cs",
        "src/HmiEditor.App/Views/ProjectSettingsView.axaml.cs",
        "src/HmiEditor.App/Views/SettingsDialog.axaml.cs",
        "src/HmiEditor.Core/Models/RuntimeConfig.cs",
    };

    /// <summary>
    /// <c>update_runtime</c> 只允许广告**真的有读取方**的 <see cref="RuntimeConfig"/> 字段。
    ///
    /// <para><b>这条守卫防的是"工具在撒谎"：</b>旧版 schema 广告了 <c>watchdog_timeout_ms</c> 与
    /// <c>enable_hmi_reboot</c>，而这两个字段在设备端**没有执行路径** —— 模型照着描述改一个死键，
    /// 回给用户的却是「已更新运行时配置」。它与阶段 9/10 收口的「UI 在撒谎」同族，
    /// 只是撒谎的界面从设置面板换成了 AI 工具描述。</para>
    ///
    /// <para><b>判据从「真实消费」推导，而不是抄一份清单：</b>判据是"产品源码里（除写入面外）
    /// 有没有 <c>.字段名</c> 的出现" —— 与 <c>ConfigFieldInventoryTests</c> 同一套扫描器与口径。
    /// 于是将来任何人把别的死字段加进工具，这条守卫都会红，不需要记得回来补断言。</para>
    ///
    /// <para><b>刻意不依赖 <see cref="ConfigFieldInventory.NotWired"/>：</b>那份清单**有意**
    /// 不登记"只被 AI 工具写入"这一类（见其类注释 —— 它们要的是补执行路径、不是标未生效）。
    /// 若拿它当判据，本守卫就永远抓不到 <c>enable_hmi_reboot</c>。</para>
    /// </summary>
    [Fact]
    public void UpdateRuntimeSchema_AdvertisesOnlyRuntimeConfigFieldsThatAreActuallyRead()
    {
        var tool = CreateTools().Single(t => t.Name == "update_runtime");
        Assert.False(string.IsNullOrWhiteSpace(tool.ArgumentsSchema), "update_runtime 没有 ArgumentsSchema");

        using var doc = JsonDocument.Parse(tool.ArgumentsSchema!);
        var advertised = doc.RootElement.GetProperty("properties")
            .EnumerateObject().Select(p => p.Name).ToList();

        // 自检：真的读到了参数（空集合会让下面的断言恒绿）
        Assert.True(advertised.Count >= 3,
            $"只读到 {advertised.Count} 个参数，schema 结构可能变了：{string.Join(", ", advertised)}");

        var sources = SourceScanner.ProductSources();
        Assert.True(sources.Count >= 300, $"只扫到 {sources.Count} 个产品源文件，扫描面可能失效");

        // 全量体检：RuntimeConfig 里"除写入面外没有任何读取"的属性
        var deadKeys = new List<string>();
        foreach (var prop in typeof(RuntimeConfig).GetProperties())
        {
            var pattern = new Regex(@"\." + Regex.Escape(prop.Name) + @"\b");
            var hasReader = sources.Any(path =>
                !RuntimeConfigWriteOnlySites.Contains(SourceScanner.Relative(path))
                && pattern.IsMatch(SourceScanner.ReadCode(path)));

            if (!hasReader) deadKeys.Add(ToSnakeCase(prop.Name));
        }

        // 自检：本轮实测的两个死字段必须被算出来 —— 否则探测器失效、整条守卫空转
        Assert.Contains("watchdog_timeout_ms", deadKeys);
        Assert.Contains("enable_hmi_reboot", deadKeys);

        var offending = advertised.Where(deadKeys.Contains).ToList();
        Assert.True(offending.Count == 0,
            "AI 工具广告了没有读取方的配置字段 —— 模型照着描述改了，回给用户的却是「已更新」，" +
            "而设备/产物上什么都不变。要么补执行路径，要么把这个参数从工具里拿掉：\n  " +
            string.Join("\n  ", offending));

        // 描述里也不得出现这些 snake_case 参数名：描述进 system prompt，同样会诱导模型去改死键。
        // （用中文解释"这一项不可改"是允许的，被禁的是**以参数名形式**出现在描述里。）
        foreach (var dead in deadKeys)
            Assert.DoesNotContain(dead, tool.Description, StringComparison.Ordinal);
    }

    /// <summary>
    /// 名字映射本身的正反例 —— <see cref="ToSnakeCase"/> 写错会让上面的"死字段集合"整体错位，
    /// 而错位的表现是**永远绿**（集合里没有匹配项）。这里钉住几个真实字段名。
    /// </summary>
    [Theory]
    [InlineData("WatchdogTimeoutMs", "watchdog_timeout_ms")]
    [InlineData("EnableHmiReboot", "enable_hmi_reboot")]
    [InlineData("ScanIntervalMs", "scan_interval_ms")]
    [InlineData("BootScreenId", "boot_screen_id")]
    [InlineData("LogLevel", "log_level")]
    public void ToSnakeCase_MapsRealRuntimeConfigFields(string pascal, string expected)
        => Assert.Equal(expected, ToSnakeCase(pascal));
}
