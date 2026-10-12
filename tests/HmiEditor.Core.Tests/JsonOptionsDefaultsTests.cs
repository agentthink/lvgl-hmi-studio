using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using HmiEditor.Core.Serialization;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// <see cref="JsonOptionsDefaults"/> 预设配置的行为验证测试。
///
/// 验证策略: 不直接断言配置属性值（那是实现细节），而是通过实际序列化/反序列化
/// 操作验证输出 JSON 的特征（缩进、命名、转义、枚举形式、null 处理等），
/// 确保预设对外暴露的"契约"稳定。
/// </summary>
public class JsonOptionsDefaultsTests
{
    /// <summary>
    /// 测试用模型 —— 包含枚举、中文、可空属性、普通属性，覆盖所有需要验证的序列化特征。
    /// </summary>
    private class SampleModel
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public SampleLogLevel Level { get; set; } = SampleLogLevel.Warning;
        public string ChineseText { get; set; } = "测试中文";
    }

    private enum SampleLogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    // ═══════════════════════════════════════════════
    //  Default 预设
    // ═══════════════════════════════════════════════

    [Fact]
    public void Default_Serialize_ProducesIndentedJson()
    {
        var json = JsonSerializer.Serialize(new SampleModel { Name = "Test" }, JsonOptionsDefaults.Default);

        // 缩进输出应包含换行符
        Assert.Contains("\n", json);
    }

    [Fact]
    public void Default_Serialize_UsesCamelCasePropertyNames()
    {
        var json = JsonSerializer.Serialize(new SampleModel { Name = "Test" }, JsonOptionsDefaults.Default);

        // 属性名应为 camelCase: "name" 而非 "Name"
        Assert.Contains("\"name\"", json);
        Assert.DoesNotContain("\"Name\"", json);
    }

    [Fact]
    public void Default_Serialize_SkipsNullProperties()
    {
        var model = new SampleModel { Name = "Test", Description = null };

        var json = JsonSerializer.Serialize(model, JsonOptionsDefaults.Default);

        // null 属性应被忽略
        Assert.DoesNotContain("description", json);
    }

    [Fact]
    public void Default_Serialize_IncludesNonNullNullableProperties()
    {
        var model = new SampleModel { Name = "Test", Description = "有值" };

        var json = JsonSerializer.Serialize(model, JsonOptionsDefaults.Default);

        Assert.Contains("\"description\"", json);
        Assert.Contains("有值", json);
    }

    [Fact]
    public void Default_Serialize_PreservesChineseCharactersWithoutEscaping()
    {
        var model = new SampleModel { Name = "测试", ChineseText = "中文内容不转义" };

        var json = JsonSerializer.Serialize(model, JsonOptionsDefaults.Default);

        // 中文字符应原样输出，而非 \uXXXX 转义
        Assert.Contains("测试", json);
        Assert.Contains("中文内容不转义", json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public void Default_Serialize_ConvertsEnumToCamelCaseString()
    {
        var model = new SampleModel { Level = SampleLogLevel.Warning };

        var json = JsonSerializer.Serialize(model, JsonOptionsDefaults.Default);

        // 枚举应序列化为 camelCase 字符串 "warning"，而非数字 2 或 PascalCase "Warning"
        Assert.Contains("\"warning\"", json);
        Assert.DoesNotContain("\"Warning\"", json);
        Assert.DoesNotContain("\"level\": 2", json);
    }

    [Fact]
    public void Default_RoundTrip_PreservesAllValues()
    {
        var original = new SampleModel
        {
            Name = "往返",
            Description = "描述",
            Level = SampleLogLevel.Error,
            ChineseText = "中文"
        };

        var json = JsonSerializer.Serialize(original, JsonOptionsDefaults.Default);
        var restored = JsonSerializer.Deserialize<SampleModel>(json, JsonOptionsDefaults.Default);

        Assert.NotNull(restored);
        Assert.Equal(original.Name, restored!.Name);
        Assert.Equal(original.Description, restored.Description);
        Assert.Equal(original.Level, restored.Level);
        Assert.Equal(original.ChineseText, restored.ChineseText);
    }

    [Fact]
    public void Default_Deserialize_AcceptsCamelCaseEnumString()
    {
        var json = """{"name":"x","level":"error"}""";

        var model = JsonSerializer.Deserialize<SampleModel>(json, JsonOptionsDefaults.Default);

        Assert.NotNull(model);
        Assert.Equal(SampleLogLevel.Error, model!.Level);
    }

    // ═══════════════════════════════════════════════
    //  Compact 预设
    // ═══════════════════════════════════════════════

    [Fact]
    public void Compact_Serialize_ProducesSingleLineJson()
    {
        var json = JsonSerializer.Serialize(new SampleModel { Name = "Test" }, JsonOptionsDefaults.Compact);

        // 不应包含换行符 —— JSONL 一行一条记录
        Assert.DoesNotContain("\n", json);
        Assert.DoesNotContain("\r", json);
    }

    [Fact]
    public void Compact_Serialize_StillUsesCamelCase()
    {
        // Compact 派生自 Default，应继承 camelCase 命名策略
        var json = JsonSerializer.Serialize(new SampleModel { Name = "Test" }, JsonOptionsDefaults.Compact);

        Assert.Contains("\"name\"", json);
        Assert.DoesNotContain("\"Name\"", json);
    }

    [Fact]
    public void Compact_Serialize_StillSkipsNullProperties()
    {
        // Compact 应继承 Default 的 WhenWritingNull 行为
        var model = new SampleModel { Name = "Test", Description = null };

        var json = JsonSerializer.Serialize(model, JsonOptionsDefaults.Compact);

        Assert.DoesNotContain("description", json);
    }

    [Fact]
    public void Compact_Serialize_StillPreservesChineseCharacters()
    {
        // Compact 应继承 Default 的 UnsafeRelaxedJsonEscaping
        var json = JsonSerializer.Serialize(new SampleModel { Name = "中文" }, JsonOptionsDefaults.Compact);

        Assert.Contains("中文", json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public void Compact_Serialize_StillConvertsEnumToCamelCaseString()
    {
        // Compact 应继承 Default 的 JsonStringEnumConverter
        var json = JsonSerializer.Serialize(new SampleModel { Level = SampleLogLevel.Info }, JsonOptionsDefaults.Compact);

        Assert.Contains("\"info\"", json);
    }

    [Fact]
    public void Compact_Serialize_IsJsonlCompatible()
    {
        // 模拟 JSONL 场景：多条记录拼接成多行文本，每行一条 JSON
        var records = new[]
        {
            new SampleModel { Name = "记录1", Level = SampleLogLevel.Info },
            new SampleModel { Name = "记录2", Level = SampleLogLevel.Error }
        };

        var jsonl = string.Join("\n", records.Select(r => JsonSerializer.Serialize(r, JsonOptionsDefaults.Compact)));
        var lines = jsonl.Split('\n');

        // 应为 2 行，每行都是独立的合法 JSON
        Assert.Equal(2, lines.Length);
        foreach (var line in lines)
        {
            var restored = JsonSerializer.Deserialize<SampleModel>(line, JsonOptionsDefaults.Compact);
            Assert.NotNull(restored);
        }
    }

    // ═══════════════════════════════════════════════
    //  Llm 预设
    // ═══════════════════════════════════════════════

    [Fact]
    public void Llm_Deserialize_IsCaseInsensitive_AcceptsPascalCase()
    {
        // LLM 输出可能为 PascalCase
        var json = """{"Name":"Test","Level":2}""";

        var model = JsonSerializer.Deserialize<SampleModel>(json, JsonOptionsDefaults.Llm);

        Assert.NotNull(model);
        Assert.Equal("Test", model!.Name);
    }

    [Fact]
    public void Llm_Deserialize_IsCaseInsensitive_AcceptsMixedCase()
    {
        // LLM 输出大小写不可控，PropertyNameCaseInsensitive 仅处理大小写（不处理 snake_case）
        var json = """{"NAME":"Test","CHINESETEXT":"中文"}""";

        var model = JsonSerializer.Deserialize<SampleModel>(json, JsonOptionsDefaults.Llm);

        Assert.NotNull(model);
        Assert.Equal("Test", model!.Name);
        Assert.Equal("中文", model.ChineseText);
    }

    [Fact]
    public void Llm_Serialize_UsesCamelCase()
    {
        var json = JsonSerializer.Serialize(new SampleModel { Name = "Test" }, JsonOptionsDefaults.Llm);

        Assert.Contains("\"name\"", json);
        Assert.DoesNotContain("\"Name\"", json);
    }

    [Fact]
    public void Llm_Serialize_DoesNotConvertEnumToString()
    {
        // Llm 不包含 JsonStringEnumConverter，枚举应序列化为数字
        var json = JsonSerializer.Serialize(new SampleModel { Level = SampleLogLevel.Warning }, JsonOptionsDefaults.Llm);

        // Warning = 2，应输出数字
        Assert.Contains("\"level\":2", json);
        Assert.DoesNotContain("\"warning\"", json);
    }

    [Fact]
    public void Llm_Serialize_EscapesNonAsciiCharacters()
    {
        // Llm 不包含 UnsafeRelaxedJsonEscaping，中文应被转义
        var json = JsonSerializer.Serialize(new SampleModel { Name = "中文" }, JsonOptionsDefaults.Llm);

        // 应包含 \u 转义序列
        Assert.Contains("\\u", json);
        // 不应原样包含中文字符
        Assert.DoesNotContain("中文", json);
    }

    [Fact]
    public void Llm_Serialize_KeepsNullProperties()
    {
        // Llm 不配置 WhenWritingNull，null 属性应保留
        var model = new SampleModel { Name = "Test", Description = null };

        var json = JsonSerializer.Serialize(model, JsonOptionsDefaults.Llm);

        Assert.Contains("\"description\":null", json);
    }

    // ═══════════════════════════════════════════════
    //  LlmConfig 预设
    // ═══════════════════════════════════════════════

    [Fact]
    public void LlmConfig_Serialize_ProducesIndentedJson()
    {
        // LlmConfig 在 Llm 基础上启用缩进
        var json = JsonSerializer.Serialize(new SampleModel { Name = "Test" }, JsonOptionsDefaults.LlmConfig);

        Assert.Contains("\n", json);
    }

    [Fact]
    public void LlmConfig_Deserialize_IsCaseInsensitive()
    {
        // LlmConfig 应继承 Llm 的 CaseInsensitive
        var json = """{"Name":"Test","Description":"desc"}""";

        var model = JsonSerializer.Deserialize<SampleModel>(json, JsonOptionsDefaults.LlmConfig);

        Assert.NotNull(model);
        Assert.Equal("Test", model!.Name);
        Assert.Equal("desc", model.Description);
    }

    [Fact]
    public void LlmConfig_Serialize_KeepsNullProperties()
    {
        // LlmConfig 继承 Llm，不忽略 null
        var model = new SampleModel { Name = "Test", Description = null };

        var json = JsonSerializer.Serialize(model, JsonOptionsDefaults.LlmConfig);

        // 带缩进时冒号后有空格
        Assert.Contains("\"description\": null", json);
    }

    [Fact]
    public void LlmConfig_Serialize_DoesNotConvertEnumToString()
    {
        // LlmConfig 继承 Llm，不包含枚举转换器
        var json = JsonSerializer.Serialize(new SampleModel { Level = SampleLogLevel.Error }, JsonOptionsDefaults.LlmConfig);

        // Error = 3，带缩进时冒号后有空格
        Assert.Contains("\"level\": 3", json);
        Assert.DoesNotContain("\"error\"", json);
    }

    // ═══════════════════════════════════════════════
    //  预设派生关系
    // ═══════════════════════════════════════════════

    [Fact]
    public void Compact_InheritsConvertersFromDefault()
    {
        // Compact 通过拷贝构造派生自 Default，应共享枚举转换器
        var json = JsonSerializer.Serialize(new SampleModel { Level = SampleLogLevel.Debug }, JsonOptionsDefaults.Compact);

        // 共享的 JsonStringEnumConverter 应将 Debug 序列化为 "debug"
        Assert.Contains("\"debug\"", json);
    }

    [Fact]
    public void LlmConfig_InheritsCaseInsensitiveFromLlm()
    {
        // LlmConfig 通过拷贝构造派生自 Llm，应继承 CaseInsensitive
        var json = """{"NAME":"abc"}""";

        var model = JsonSerializer.Deserialize<SampleModel>(json, JsonOptionsDefaults.LlmConfig);

        Assert.NotNull(model);
        Assert.Equal("abc", model!.Name);
    }

    [Fact]
    public void AllPresets_SupportRoundTripDeserialization()
    {
        // 所有预设都应能正确反序列化自身序列化的输出
        var original = new SampleModel
        {
            Name = "RoundTrip",
            Description = "desc",
            Level = SampleLogLevel.Info,
            ChineseText = "中文"
        };

        foreach (var options in new[]
                 {
                     JsonOptionsDefaults.Default,
                     JsonOptionsDefaults.Compact,
                     JsonOptionsDefaults.Llm,
                     JsonOptionsDefaults.LlmConfig
                 })
        {
            var json = JsonSerializer.Serialize(original, options);
            var restored = JsonSerializer.Deserialize<SampleModel>(json, options);

            Assert.NotNull(restored);
            Assert.Equal(original.Name, restored!.Name);
            Assert.Equal(original.Description, restored.Description);
            Assert.Equal(original.Level, restored.Level);
            Assert.Equal(original.ChineseText, restored.ChineseText);
        }
    }
}
