using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HmiEditor.AI.Inference;
using HmiEditor.AI.Generators;
using HmiEditor.App.Services;

namespace HmiEditor.App.Services;

/// <summary>
/// **AI 图标对话框的"生成源"注入缝**（阶段 209，与 `#94`/`#95` 的设备侧两处同套路）——
/// 把"对话框怎么生成"与 `AiPreviewManager` 的**具体类型**解耦。
///
/// <para><b>为什么需要它</b>：`AiIconDialog` 原先直接吃 <c>AiPreviewManager</c>（**sealed**，且自己
/// <c>new</c> provider），于是它那 4 个输入框（`InputBox` / `ImageInputBox` / `WidthBox` / `HeightBox`）
/// 的读点在 headless 下**只能测早退分支**（未配置 / 空输入）—— 真正的读取点在
/// `ImageGenerator` 的单图生成与 `BatchIconGenerator` 的批量生成这两条路的**实参**里
/// （提示词 / 画布尺寸 / 条目列表 / 固定变体），没有缝就观测不到，而要跑真生成就得真 LLM 调用。</para>
///
/// <para>⚠ 这里刻意**不写出带括号的调用字样**：`AiCancellationWiringTests` 是**文本级**守卫
/// （连注释一起扫，匹配到 `Xxx.GenerateAsync(...)` 就要求实参里有 token）—— 注释里写个省略号的
/// 假调用会被它误伤（本轮就撞过一次）。</para>
///
/// <para><b>默认实现逐字搬用原逻辑</b>（<see cref="PreviewManagerIconSource"/>）：能力位读
/// <c>IsConfigured</c>/<c>IconGenerator</c>/<c>ImageGenerator</c>，两个生成方法各自转发到
/// <c>ImageGenerator.GenerateAsync</c> 与「`BatchIconGenerator` ＋ `FixedVariant`」，行为零变化。</para>
/// </summary>
internal interface IIconGenerationSource
{
    /// <summary>是否已配置可用的 Provider（原 `AiPreviewManager.IsConfigured`）。</summary>
    bool IsConfigured { get; }

    /// <summary>批量图标生成器是否可用（原 `IconGenerator is not null`）。</summary>
    bool HasIconGenerator { get; }

    /// <summary>单图生成器是否可用（原 `ImageGenerator is not null`）。</summary>
    bool HasImageGenerator { get; }

    /// <summary>生成单张彩色 SVG（原 `ImageGenerator.GenerateAsync`）。</summary>
    Task<ImageGenerator.ImageGenerationResult> GenerateImageAsync(string prompt, int width, int height, CancellationToken ct);

    /// <summary>批量生成图标（原「`BatchIconGenerator` ＋ `FixedVariant` ＋ `GenerateBatchAsync`」）。</summary>
    Task<IReadOnlyList<BatchIconResult>> GenerateIconsAsync(
        IReadOnlyList<(string Name, string Description)> items,
        IconStyleVariant? fixedVariant,
        CancellationToken ct);
}

/// <summary>默认生成源：把 <see cref="AiPreviewManager"/> 的能力原样转发（行为与加缝之前逐字相同）。</summary>
internal sealed class PreviewManagerIconSource : IIconGenerationSource
{
    private readonly AiPreviewManager _manager;

    internal PreviewManagerIconSource(AiPreviewManager manager) => _manager = manager;

    public bool IsConfigured => _manager.IsConfigured;

    public bool HasIconGenerator => _manager.IconGenerator is not null;

    public bool HasImageGenerator => _manager.ImageGenerator is not null;

    public Task<ImageGenerator.ImageGenerationResult> GenerateImageAsync(string prompt, int width, int height, CancellationToken ct)
        => _manager.ImageGenerator!.GenerateAsync(prompt, width, height, ct);

    public Task<IReadOnlyList<BatchIconResult>> GenerateIconsAsync(
        IReadOnlyList<(string Name, string Description)> items,
        IconStyleVariant? fixedVariant,
        CancellationToken ct)
    {
        var batch = new BatchIconGenerator(_manager.IconGenerator!);
        if (fixedVariant is { } variant) batch.FixedVariant = variant;
        return batch.GenerateBatchAsync(items, ct);
    }
}
