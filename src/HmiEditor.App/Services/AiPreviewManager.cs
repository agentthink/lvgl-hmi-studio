using HmiEditor.AI;
using HmiEditor.App.Resources;
using HmiEditor.AI.Generators;
using HmiEditor.AI.Inference;
using HmiEditor.AI.Validation;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Services;

/// <summary>
/// AI 预览管理器 —— UI 层与 AI 核心层的桥接。
/// 持有配置与编排器, 负责:
///   - 配置变更 → 重建 Provider + 生成器 + 编排器
///   - 生成请求 → 返回带验证结果的屏幕/模板
///   - 维护"当前预览"状态(生成后可预览/应用/取消)
/// </summary>
public sealed class AiPreviewManager : IDisposable
{
    private AiOrchestrator? _orchestrator;
    private ILlmProvider? _provider;
    private LlmProviderConfig _config;

    /// <summary>当前生效的配置。</summary>
    public LlmProviderConfig Config => _config;

    /// <summary>当前 LLM Provider 实例（供 AgentHost 复用同一 Provider）。</summary>
    public ILlmProvider? CurrentProvider => _provider;

    /// <summary>是否已配置有效的 Provider(有 API Key 且选了 Provider)。</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_config.Provider) &&
        !string.IsNullOrWhiteSpace(_config.ApiKey);

    /// <summary>最近一次生成结果(供预览/应用)。</summary>
    public HmiScreen? PendingScreen { get; private set; }

    /// <summary>最近一次生成的模板(供预览/应用)。</summary>
    public ControlTemplate? PendingTemplate { get; private set; }

    /// <summary>最近一次生成的脚本(供预览/应用)。</summary>
    public HmiEditor.Core.Models.Scripting.HmiScript? PendingScript { get; private set; }

    /// <summary>图标生成器(懒创建, 供 AssetManager 使用)。</summary>
    public IconGenerator? IconGenerator { get; private set; }

    /// <summary>图片生成器(通用 SVG 图片, 供 AI 面板使用)。</summary>
    public ImageGenerator? ImageGenerator { get; private set; }

    /// <summary>最近一次生成的图片 SVG(供预览/导出)。</summary>
    public string? PendingImageSvg { get; private set; }

    /// <summary>最近一次生成的图片尺寸。</summary>
    public (int Width, int Height) PendingImageSize { get; private set; }

    /// <summary>最近一次验证结果。</summary>
    public AiValidationResult? PendingValidation { get; private set; }

    /// <summary>最近一次 token 消耗(输入/输出)。</summary>
    public (int Input, int Output) LastTokens { get; private set; }

    public AiPreviewManager(LlmProviderConfig? config = null)
    {
        _config = config ?? LlmConfigStore.Load();
        RebuildOrchestrator();
    }

    /// <summary>更新配置并重建编排器。返回是否成功。</summary>
    public bool UpdateConfig(LlmProviderConfig config)
    {
        _config = config;
        ClearPending();

        if (!IsConfigured)
        {
            // 未配置: 清空 Provider/编排器, 使 UI 状态与运行时能力保持一致,
            // 避免残留旧配置的 Provider 在"未配置"状态下仍被调用。
            ReleaseProvider();
            _orchestrator = null;
            return true;
        }

        try
        {
            RebuildOrchestrator();
            LlmConfigStore.Save(config);
            return true;
        }
        catch (Exception ex)
        {
            ReleaseProvider();
            _orchestrator = null;
            System.Diagnostics.Debug.WriteLine($"[AI] 配置重建失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>释放当前 Provider(若可释放)并清空引用。</summary>
    private void ReleaseProvider()
    {
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
            _provider = null;
        }
        IconGenerator = null;
        ImageGenerator = null;
    }

    /// <summary>释放 Provider 持有的 HTTP 连接等资源。</summary>
    public void Dispose()
    {
        ReleaseProvider();
        _orchestrator = null;
    }

    /// <summary>
    /// 生成屏幕。未配置时抛出 InvalidOperationException。
    /// </summary>
    /// <param name="textProgress">可选的模型增量回调（流式）；null 时行为与旧版一致。</param>
    public async Task<HmiScreen?> GenerateScreenAsync(
        string userInput,
        HmiProject project,
        CancellationToken ct = default,
        IProgress<string>? textProgress = null)
    {
        EnsureConfigured();

        var result = await _orchestrator!.ProcessAsync(userInput, project, ct, textProgress);

        PendingValidation = result.Validation;
        LastTokens = (result.InputTokens, result.OutputTokens);
        PendingTemplate = null;

        PendingScreen = result.Validation.HasErrors ? null : result.Screen;
        return PendingScreen;
    }

    /// <summary>
    /// 生成 Faceplate 模板。未配置时抛出 InvalidOperationException。
    /// </summary>
    public async Task<ControlTemplate?> GenerateTemplateAsync(
        string userInput,
        HmiProject project,
        CancellationToken ct = default,
        IProgress<string>? textProgress = null)
    {
        EnsureConfigured();

        var result = await _orchestrator!.ProcessAsync(userInput, project, ct, textProgress);

        PendingValidation = result.Validation;
        LastTokens = (result.InputTokens, result.OutputTokens);
        PendingScreen = null;

        PendingTemplate = result.Validation.HasErrors ? null : result.Template;
        return PendingTemplate;
    }

    /// <summary>
    /// 生成 Lua 脚本。未配置时抛出 InvalidOperationException。
    /// </summary>
    public async Task<HmiEditor.Core.Models.Scripting.HmiScript?> GenerateScriptAsync(
        string userInput,
        HmiProject project,
        CancellationToken ct = default,
        IProgress<string>? textProgress = null)
    {
        EnsureConfigured();

        var result = await _orchestrator!.ProcessAsync(userInput, project, ct, textProgress);

        PendingValidation = result.Validation;
        LastTokens = (result.InputTokens, result.OutputTokens);
        PendingScreen = null;
        PendingTemplate = null;

        PendingScript = result.Validation.HasErrors ? null : result.Script;
        return PendingScript;
    }

    /// <summary>
    /// 生成 SVG 图片。未配置时抛出 InvalidOperationException。
    /// </summary>
    public async Task<string?> GenerateImageAsync(
        string userInput,
        HmiProject project,
        CancellationToken ct = default,
        IProgress<string>? textProgress = null)
    {
        EnsureConfigured();

        var result = await _orchestrator!.ProcessAsync(userInput, project, ct, textProgress);

        PendingValidation = result.Validation;
        LastTokens = (result.InputTokens, result.OutputTokens);
        PendingScreen = null;
        PendingTemplate = null;
        PendingScript = null;

        if (result.Validation.HasErrors || string.IsNullOrEmpty(result.ImageSvg))
        {
            PendingImageSvg = null;
            return null;
        }

        PendingImageSvg = result.ImageSvg;
        PendingImageSize = (result.ImageWidth, result.ImageHeight);
        return PendingImageSvg;
    }

    /// <summary>清除待处理结果。</summary>
    public void ClearPending()
    {
        PendingScreen = null;
        PendingTemplate = null;
        PendingScript = null;
        PendingImageSvg = null;
        PendingValidation = null;
    }

    private void EnsureConfigured()
    {
        if (_orchestrator is null)
            throw new InvalidOperationException(AppLocalization.Service["ai.err.not.configured"]);
    }

    private void RebuildOrchestrator()
    {
        if (!IsConfigured)
        {
            _orchestrator = null;
            return;
        }

        if (_provider is IDisposable oldDisposable)
        {
            oldDisposable.Dispose();
        }
        _provider = LlmProviderFactory.Create(_config);
        var prompts = new PromptTemplateEngine();
        var screenGenerator = new NlToScreenGenerator(_provider, prompts);
        var templateGenerator = new NlToTemplateGenerator(_provider, prompts);
        var scriptGenerator = new NlToScriptGenerator(_provider, prompts);
        IconGenerator = new IconGenerator(_provider, prompts);
        ImageGenerator = new ImageGenerator(_provider, prompts);
        _orchestrator = new AiOrchestrator(screenGenerator, templateGenerator, scriptGenerator, ImageGenerator);
    }
}
