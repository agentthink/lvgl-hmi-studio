using System.Text.Json;
using HmiEditor.AI.Inference;
using HmiEditor.AI.Models;
using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;

namespace HmiEditor.AI.Generators;

/// <summary>
/// NL→Screen 生成器 —— 将用户自然语言描述转换为 HmiScreen 模型。
/// 流程: Prompt 构建 → LLM 调用(JSON Schema 约束) → 反序列化 → 映射为 Core 模型。
/// 输出为"预览"对象,由调用方决定是否应用到项目(非破坏性工作流)。
/// </summary>
public sealed class NlToScreenGenerator
{
    private readonly ILlmProvider _llm;
    private readonly PromptTemplateEngine _prompts;

    public NlToScreenGenerator(ILlmProvider llm, PromptTemplateEngine prompts)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
    }

    /// <summary>生成结果: 屏幕 + token 消耗。</summary>
    public sealed record ScreenGenerationResult(HmiScreen Screen, int InputTokens, int OutputTokens);

    /// <param name="textProgress">
    /// 可选的模型增量回调（流式）。放在 <paramref name="ct"/> 之后是为了不破坏既有位置参数调用；
    /// null 时行为与旧版完全一致（见 <see cref="LlmChat.WithProgressAsync"/>）。
    /// </param>
    public async Task<ScreenGenerationResult> GenerateAsync(
        string userInput,
        HmiProject projectContext,
        CancellationToken ct = default,
        IProgress<string>? textProgress = null)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            throw new ArgumentException("用户描述不能为空", nameof(userInput));

        // 1. 构建 Prompt (含项目上下文)
        var messages = _prompts.BuildScreenMessages(userInput, projectContext);
        var schema = _prompts.GetScreenSchema();

        // 2. 调用 LLM, 约束 JSON Schema (模板缺失/为空时降级为无约束调用, 不阻断生成)
        var response = await LlmChat.WithProgressAsync(_llm, new LlmRequest
        {
            Messages = messages,
            JsonSchema = string.IsNullOrWhiteSpace(schema) ? null : schema,
            Temperature = 0.2,
            MaxTokens = 4096
        }, textProgress, ct);

        // 3. 反序列化结构化输出 (JSON 可能被包裹在 markdown 代码块中, 容错剥离)
        // 无效 JSON 回退默认屏幕(不抛异常), 由 AiOutputValidator 兜底提示。
        var generated = ParseResponse(response.Content);

        // 4. 映射为 Core 模型
        var screen = MapToHmiScreen(generated, projectContext);

        return new ScreenGenerationResult(screen, response.InputTokens, response.OutputTokens);
    }

    /// <summary>解析 LLM 输出 JSON → 中间模型。容错: 剥离 ```json 代码块标记。</summary>
    internal static AiGeneratedScreen? ParseResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var json = content.Trim();
        // 剥离 ```json ... ``` 包裹
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
            return JsonSerializer.Deserialize<AiGeneratedScreen>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>大小写不敏感 + camelCase —— 兼容 LLM 输出与 JSON Schema 命名。</summary>
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsDefaults.Llm;

    /// <summary>中间模型 → HmiScreen。变量名解析为项目中的变量 Id; 无匹配时留空。</summary>
    internal static HmiScreen MapToHmiScreen(AiGeneratedScreen? generated, HmiProject project)
    {
        // 尺寸/自适应/背景色统一走 ScreenFactory：LLM 没给尺寸时落到工程的屏幕默认模板
        // （此前写死 800×480，与工程默认 1024×600 及运行时目标都不一致）。
        var screen = ScreenFactory.Create(
            project,
            name: generated?.Screen?.Name ?? "AI 生成屏幕",
            width: generated?.Screen?.Width,
            height: generated?.Screen?.Height,
            bgColor: generated?.Screen?.BgColor);

        if (generated?.Screen?.Controls is null)
            return screen;

        foreach (var c in generated.Screen.Controls)
        {
            var control = new HmiControl
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
                VariableBindingId = ResolveVariableId(c.VariableName, project)
            };
            screen.Controls.Add(control);
        }

        return screen;
    }

    /// <summary>变量名 → 项目中的变量 Id。不区分大小写, 未匹配返回 null。</summary>
    private static string? ResolveVariableId(string? variableName, HmiProject project)
    {
        if (string.IsNullOrEmpty(variableName)) return null;
        var vars = project.VariableManager?.Variables;
        if (vars is null) return null;

        var match = vars.FirstOrDefault(v =>
            string.Equals(v.Name, variableName, StringComparison.OrdinalIgnoreCase));
        return match?.Id;
    }
}
