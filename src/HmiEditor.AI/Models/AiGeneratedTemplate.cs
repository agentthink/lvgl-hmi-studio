using System.Text.Json.Serialization;

namespace HmiEditor.AI.Models;

/// <summary>
/// LLM 生成的 Faceplate 模板中间模型 —— 对应 TemplateGenerationSchema.json 的结构化输出。
/// 反序列化后再映射为 Core 的 ControlTemplate。
/// </summary>
public class AiGeneratedTemplate
{
    public AiTemplate Template { get; set; } = new();
}

public class AiTemplate
{
    public string Name { get; set; } = "AI 生成模板";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "通用";
    public List<AiTemplateControl> Controls { get; set; } = new();
    public List<AiTemplateParameter> Parameters { get; set; } = new();
}

public class AiTemplateControl
{
    public string Type { get; set; } = "Label";
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 100;
    public int Height { get; set; } = 40;
    public string? Text { get; set; }
    public string? BgColor { get; set; }
    public string? TextColor { get; set; }
    public int Radius { get; set; } = 4;

    /// <summary>绑定表达式, 可为 "{{Parameters.xxx}}" 占位符或留空。</summary>
    public string? Binding { get; set; }
}

public class AiTemplateParameter
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Type { get; set; } = "String";
    public string? DefaultValue { get; set; }
    public string Description { get; set; } = "";
}
