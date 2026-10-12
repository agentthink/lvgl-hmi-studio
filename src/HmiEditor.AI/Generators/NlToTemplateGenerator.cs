using System.Text.Json;
using HmiEditor.AI.Inference;
using HmiEditor.AI.Models;
using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;

namespace HmiEditor.AI.Generators;

/// <summary>
/// NL→Faceplate Template 生成器 —— 将用户自然语言描述转换为可复用的 ControlTemplate。
/// 流程: Prompt 构建 → LLM 调用(JSON Schema 约束) → 反序列化 → 映射为 Core 模型。
/// 生成结果含参数接口定义, 控件内可变文本/绑定使用 {{Parameters.xxx}} 占位符。
/// </summary>
public sealed class NlToTemplateGenerator
{
    private readonly ILlmProvider _llm;
    private readonly PromptTemplateEngine _prompts;

    public NlToTemplateGenerator(ILlmProvider llm, PromptTemplateEngine prompts)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
    }

    /// <summary>生成结果: 模板 + token 消耗。</summary>
    public sealed record TemplateGenerationResult(ControlTemplate Template, int InputTokens, int OutputTokens);

    /// <param name="textProgress">可选的模型增量回调（流式）；null 时行为与旧版一致。</param>
    public async Task<TemplateGenerationResult> GenerateAsync(
        string userInput,
        HmiProject projectContext,
        CancellationToken ct = default,
        IProgress<string>? textProgress = null)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            throw new ArgumentException("用户描述不能为空", nameof(userInput));

        var messages = _prompts.BuildTemplateMessages(userInput, projectContext);
        var schema = _prompts.GetTemplateSchema();

        var response = await LlmChat.WithProgressAsync(_llm, new LlmRequest
        {
            Messages = messages,
            JsonSchema = string.IsNullOrWhiteSpace(schema) ? null : schema,
            Temperature = 0.2,
            MaxTokens = 4096
        }, textProgress, ct);

        var generated = ParseResponse(response.Content);
        var template = MapToControlTemplate(generated, projectContext);

        return new TemplateGenerationResult(template, response.InputTokens, response.OutputTokens);
    }

    /// <summary>解析 LLM 输出 JSON → 中间模型。容错: 剥离 ```json 代码块标记。</summary>
    internal static AiGeneratedTemplate? ParseResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var json = content.Trim();
        const string fenceStart = "```";
        if (json.StartsWith(fenceStart, StringComparison.Ordinal))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline > 0) json = json[(firstNewline + 1)..];
            var fenceEnd = json.LastIndexOf(fenceStart, StringComparison.Ordinal);
            if (fenceEnd > 0) json = json[..fenceEnd];
        }

        try
        {
            return JsonSerializer.Deserialize<AiGeneratedTemplate>(json, JsonOptionsDefaults.Llm);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>中间模型 → ControlTemplate。参数类型字符串 → ParameterType 枚举。</summary>
    internal static ControlTemplate MapToControlTemplate(AiGeneratedTemplate? generated, HmiProject project)
    {
        var template = new ControlTemplate
        {
            Name = generated?.Template?.Name ?? "AI 生成模板",
            Description = generated?.Template?.Description ?? "",
            Category = string.IsNullOrWhiteSpace(generated?.Template?.Category) ? "通用" : generated.Template.Category,
            Version = 1
        };

        if (generated?.Template?.Controls is not null)
        {
            foreach (var c in generated.Template.Controls)
            {
                template.Controls.Add(new HmiControl
                {
                    Name = c.Name,
                    Type = AiSchemaEnums.ParseGeneratableControlTypeOrLabel(c.Type),
                    X = c.X,
                    Y = c.Y,
                    Width = c.Width,
                    Height = c.Height,
                    Text = c.Text,
                    BgColor = c.BgColor,
                    TextColor = c.TextColor,
                    Radius = c.Radius,
                    // 绑定表达式: 若为占位符, 转换为 VariableBindingId; 若为变量名, 解析为变量 Id
                    VariableBindingId = ResolveBinding(c.Binding, project)
                });
            }
        }

        if (generated?.Template?.Parameters is not null)
        {
            foreach (var p in generated.Template.Parameters)
            {
                template.Parameters.Add(new TemplateParameter
                {
                    Name = p.Name,
                    DisplayName = string.IsNullOrWhiteSpace(p.DisplayName) ? p.Name : p.DisplayName,
                    Type = ParseParameterType(p.Type),
                    DefaultValue = p.DefaultValue,
                    Description = p.Description
                });
            }
        }

        return template;
    }

    /// <summary>参数字符串类型 → 枚举。未知回退 String。</summary>
    internal static ParameterType ParseParameterType(string type)
    {
        return type.Trim().ToLowerInvariant() switch
        {
            "string" => ParameterType.String,
            "number" => ParameterType.Number,
            "boolean" or "bool" => ParameterType.Boolean,
            "color" => ParameterType.Color,
            "variableid" or "variable" or "binding" => ParameterType.VariableId,
            _ => ParameterType.String
        };
    }

    /// <summary>
    /// 解析绑定表达式:
    /// - "{{Parameters.xxx}}" 占位符 → 保留为占位符语义 (VariableBindingId 设为占位符文本, 展开时替换)
    /// - 项目中的变量名 → 解析为变量 Id
    /// - 其他 → null
    /// </summary>
    private static string? ResolveBinding(string? binding, HmiProject project)
    {
        if (string.IsNullOrWhiteSpace(binding)) return null;
        var trimmed = binding.Trim();

        // 占位符格式 {{Parameters.name}} —— 保留原文供 FaceplateExpander 替换
        if (trimmed.StartsWith("{{", StringComparison.Ordinal) &&
            trimmed.EndsWith("}}", StringComparison.Ordinal))
        {
            return trimmed;
        }

        // 变量名 → Id (不区分大小写)
        var vars = project.VariableManager?.Variables;
        if (vars is not null)
        {
            var match = vars.FirstOrDefault(v =>
                string.Equals(v.Name, trimmed, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match.Id;
        }

        return null;
    }
}
