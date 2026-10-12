using HmiEditor.AI.Generators;
using HmiEditor.AI.Validation;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Scripting;

namespace HmiEditor.AI;

/// <summary>
/// AI 编排器 —— 统一入口。意图分类 → 路由到生成器 → 输出验证。
/// 上层 UI (AI Chat Panel) 只依赖此类的 ProcessAsync。
/// </summary>
public sealed class AiOrchestrator
{
    private readonly NlToScreenGenerator _screenGenerator;
    private readonly NlToTemplateGenerator _templateGenerator;
    private readonly NlToScriptGenerator _scriptGenerator;
    private readonly ImageGenerator? _imageGenerator;

    public AiOrchestrator(
        NlToScreenGenerator screenGenerator,
        NlToTemplateGenerator? templateGenerator = null,
        NlToScriptGenerator? scriptGenerator = null,
        ImageGenerator? imageGenerator = null)
    {
        _screenGenerator = screenGenerator ?? throw new ArgumentNullException(nameof(screenGenerator));
        _templateGenerator = templateGenerator ?? throw new ArgumentNullException(nameof(templateGenerator));
        _scriptGenerator = scriptGenerator ?? throw new ArgumentNullException(nameof(scriptGenerator));
        _imageGenerator = imageGenerator;
    }

    /// <summary>生成结果 —— 屏幕/模板/脚本/图片 + 验证 + token 消耗。</summary>
    public sealed record OrchestrationResult(
        HmiScreen? Screen,
        ControlTemplate? Template,
        HmiScript? Script,
        string? ImageSvg,
        int ImageWidth,
        int ImageHeight,
        AiValidationResult Validation,
        string Intent,
        int InputTokens,
        int OutputTokens);

    /// <summary>处理用户输入: 意图分类 → 路由 → 验证。</summary>
    /// <param name="textProgress">可选的模型增量回调（流式）；null 时行为与旧版一致。</param>
    public async Task<OrchestrationResult> ProcessAsync(
        string userInput,
        HmiProject projectContext,
        CancellationToken ct = default,
        IProgress<string>? textProgress = null)
    {
        var intent = ClassifyIntent(userInput);

        return intent switch
        {
            AiIntent.Screen => await GenerateScreenAsync(userInput, projectContext, ct, textProgress),
            AiIntent.Template => await GenerateTemplateAsync(userInput, projectContext, ct, textProgress),
            AiIntent.Script => await GenerateScriptAsync(userInput, projectContext, ct, textProgress),
            AiIntent.Image => await GenerateImageAsync(userInput, ct, textProgress),
            _ => BuildNotSupportedResult(intent)
        };
    }

    private async Task<OrchestrationResult> GenerateScreenAsync(
        string userInput, HmiProject projectContext, CancellationToken ct, IProgress<string>? textProgress)
    {
        var result = await _screenGenerator.GenerateAsync(userInput, projectContext, ct, textProgress);
        var validation = AiOutputValidator.ValidateScreen(result.Screen, projectContext);

        return new OrchestrationResult(
            result.Screen, null, null, null, 0, 0, validation,
            AiIntent.Screen.ToString(),
            result.InputTokens, result.OutputTokens);
    }

    private async Task<OrchestrationResult> GenerateTemplateAsync(
        string userInput, HmiProject projectContext, CancellationToken ct, IProgress<string>? textProgress)
    {
        var result = await _templateGenerator.GenerateAsync(userInput, projectContext, ct, textProgress);
        var validation = AiOutputValidator.ValidateTemplate(result.Template);

        return new OrchestrationResult(
            null, result.Template, null, null, 0, 0, validation,
            AiIntent.Template.ToString(),
            result.InputTokens, result.OutputTokens);
    }

    private async Task<OrchestrationResult> GenerateScriptAsync(
        string userInput, HmiProject projectContext, CancellationToken ct, IProgress<string>? textProgress)
    {
        var result = await _scriptGenerator.GenerateAsync(userInput, projectContext, ct, textProgress);
        var validation = AiOutputValidator.ValidateScript(result.Script);

        return new OrchestrationResult(
            null, null, result.Script, null, 0, 0, validation,
            AiIntent.Script.ToString(),
            result.InputTokens, result.OutputTokens);
    }

    private async Task<OrchestrationResult> GenerateImageAsync(
        string userInput, CancellationToken ct, IProgress<string>? textProgress)
    {
        if (_imageGenerator is null)
        {
            var notSupported = new AiValidationResult();
            notSupported.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Info, AiIssueCodes.IMAGE_GENERATOR_MISSING, "图片生成器未配置。"));
            return new OrchestrationResult(
                null, null, null, null, 0, 0, notSupported,
                AiIntent.Image.ToString(), 0, 0);
        }

        // 从用户输入中尝试解析尺寸 "200x300" / "200×300" / "200*300"
        var (width, height) = ParseImageSize(userInput, 200, 200);
        var result = await _imageGenerator.GenerateAsync(userInput, width, height, ct, textProgress);
        var validation = new AiValidationResult();

        return new OrchestrationResult(
            null, null, null, result.SvgText, result.Width, result.Height, validation,
            AiIntent.Image.ToString(),
            result.InputTokens, result.OutputTokens);
    }

    /// <summary>从用户输入中解析图片尺寸提示（如 "200x300"），未找到则使用默认值。</summary>
    private static (int W, int H) ParseImageSize(string input, int defaultW, int defaultH)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            input, @"(\d{2,4})\s*[x×*]\s*(\d{2,4})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var w) && int.TryParse(match.Groups[2].Value, out var h))
            return (w, h);
        return (defaultW, defaultH);
    }

    private static OrchestrationResult BuildNotSupportedResult(AiIntent intent)
    {
        var notSupported = new AiValidationResult();
        notSupported.Issues.Add(AiValidationIssue.Of(
            AiValidationSeverity.Info, AiIssueCodes.INTENT_UNSUPPORTED,
            "当前版本暂不支持 '{0}' 意图, 支持: 屏幕生成 / 模板生成 / 脚本生成 / 图片生成。", intent));
        return new OrchestrationResult(null, null, null, null, 0, 0, notSupported, intent.ToString(), 0, 0);
    }

    /// <summary>意图类型。</summary>
    public enum AiIntent
    {
        Screen,      // 生成屏幕
        Template,    // 生成 Faceplate 模板
        Script,      // 生成 Lua 脚本 (Phase 3)
        Image,       // 生成 SVG 图片
        Unknown
    }

    /// <summary>
    /// 意图分类 —— Phase 1/2 基于关键词的轻量分类,后续可替换为 LLM 分类。
    /// </summary>
    public static AiIntent ClassifyIntent(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput)) return AiIntent.Unknown;

        var text = userInput.ToLowerInvariant();

        // 图片生成意图优先匹配
        if (ContainsAny(text, new[] { "图片", "矢量图", "svg", "画一个图", "生成图", "插画", "图标", "icon", "image", "drawing", "插图" }))
            return AiIntent.Image;

        // 脚本意图优先匹配
        if (ContainsAny(text, new[] { "脚本", "lua", "script", "逻辑", "联动", "点击时", "超过" }))
            return AiIntent.Script;

        // 模板/面盘意图
        if (ContainsAny(text, new[] { "模板", "面盘", "faceplate", "组件", "复用", "封装" }))
            return AiIntent.Template;

        // 屏幕/界面意图
        if (ContainsAny(text, new[] { "屏幕", "界面", "画面", "页面", "屏", "创建", "生成", "设计一个", "做个", "添加", "screen", "ui" }))
            return AiIntent.Screen;

        return AiIntent.Unknown;
    }

    private static bool ContainsAny(string text, string[] keywords)
    {
        return keywords.Any(k => text.Contains(k, StringComparison.Ordinal));
    }
}
