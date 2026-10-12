using System.Text.Json;
using HmiEditor.AI.Inference;
using HmiEditor.AI.Models;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Serialization;

namespace HmiEditor.AI.Generators;

/// <summary>
/// NL→Lua 脚本生成器 —— 将用户自然语言描述转换为 Lua 脚本 (HmiScript)。
/// 流程: Prompt 构建 → LLM 调用(JSON Schema 约束) → 反序列化 → 映射为 Core 模型。
/// </summary>
public sealed class NlToScriptGenerator
{
    private readonly ILlmProvider _llm;
    private readonly PromptTemplateEngine _prompts;

    public NlToScriptGenerator(ILlmProvider llm, PromptTemplateEngine prompts)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
    }

    /// <summary>生成结果: 脚本 + token 消耗。</summary>
    public sealed record ScriptGenerationResult(HmiScript Script, int InputTokens, int OutputTokens);

    /// <param name="textProgress">可选的模型增量回调（流式）；null 时行为与旧版一致。</param>
    public async Task<ScriptGenerationResult> GenerateAsync(
        string userInput,
        HmiProject projectContext,
        CancellationToken ct = default,
        IProgress<string>? textProgress = null)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            throw new ArgumentException("用户描述不能为空", nameof(userInput));

        var messages = _prompts.BuildScriptMessages(userInput, projectContext);
        var schema = _prompts.GetScriptSchema();

        var response = await LlmChat.WithProgressAsync(_llm, new LlmRequest
        {
            Messages = messages,
            JsonSchema = string.IsNullOrWhiteSpace(schema) ? null : schema,
            Temperature = 0.2,
            MaxTokens = 4096
        }, textProgress, ct);

        var generated = ParseResponse(response.Content);
        var script = MapToHmiScript(generated);

        return new ScriptGenerationResult(script, response.InputTokens, response.OutputTokens);
    }

    /// <summary>解析 LLM 输出 JSON → 中间模型。容错: 剥离 ```json 代码块标记。</summary>
    internal static AiGeneratedScript? ParseResponse(string content)
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
            return JsonSerializer.Deserialize<AiGeneratedScript>(json, JsonOptionsDefaults.Llm);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>中间模型 → HmiScript。分类归一化到已知集合。</summary>
    internal static HmiScript MapToHmiScript(AiGeneratedScript? generated)
    {
        var script = new HmiScript
        {
            Name = generated?.Script?.Name ?? "AI 生成脚本",
            Description = generated?.Script?.Description,
            Category = NormalizeCategory(generated?.Script?.Category),
            Code = generated?.Script?.Code ?? ""
        };
        return script;
    }

    /// <summary>分类归一化: 未知分类回退 OnClick。</summary>
    private static string NormalizeCategory(string? category)
    {
        var known = new[] { "OnClick", "OnValueChanged", "Timer", "Alarm" };
        var match = known.FirstOrDefault(k =>
            string.Equals(k, category, StringComparison.OrdinalIgnoreCase));
        return match ?? "OnClick";
    }
}
