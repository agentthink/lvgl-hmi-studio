namespace HmiEditor.AI.Generators;

/// <summary>单个图标生成结果。</summary>
public sealed record BatchIconResult(string Name, string SvgText, int ShapeCount, bool Success, string? Error);

/// <summary>批量生成进度报告。</summary>
public sealed record BatchProgress(int Completed, int Total, string CurrentItem);

/// <summary>
/// 图标批量生成器 —— 对图标描述列表逐个调用 IconGenerator。
/// 支持进度报告与取消(通过 CancellationToken)。
///
/// 多样性增强(对标 iconfont.cn 同语义多风格):
/// 批量生成时按语义分类使用 <see cref="VariantSelectionMode.Rotate"/> 轮转策略,
/// 使同一分类下(如泵/电机/阀门均为设备类)连续生成的图标使用不同视觉风格变体,
/// 确保整批图标视觉表现既符合类型特征又具有足够的多样性。
/// </summary>
public sealed class BatchIconGenerator
{
    private readonly IconGenerator _iconGenerator;
    private readonly IconVariantSelector _variantSelector;

    /// <summary>
    /// 固定变体 —— 非 null 时所有图标强制使用该变体(分类仍自动识别);
    /// null 时按 <see cref="VariantSelectionMode.Rotate"/> 轮转, 保证同类图标风格互异。
    /// </summary>
    public IconStyleVariant? FixedVariant { get; set; }

    public BatchIconGenerator(IconGenerator iconGenerator, VariantSelectionMode mode = VariantSelectionMode.Rotate)
    {
        _iconGenerator = iconGenerator ?? throw new ArgumentNullException(nameof(iconGenerator));
        _variantSelector = new IconVariantSelector(mode);
    }

    /// <summary>批量生成进度事件。</summary>
    public event EventHandler<BatchProgress>? ProgressChanged;

    /// <summary>
    /// 批量生成图标。
    /// </summary>
    /// <param name="items">(名称, 描述) 列表。</param>
    /// <param name="ct">取消令牌 —— 调用方传入以支持中途取消。</param>
    /// <returns>每个条目的生成结果(失败条目 Success=false 并带错误信息)。</returns>
    public async Task<IReadOnlyList<BatchIconResult>> GenerateBatchAsync(
        IReadOnlyList<(string Name, string Description)> items,
        CancellationToken ct = default)
    {
        if (items is null || items.Count == 0) return Array.Empty<BatchIconResult>();

        _variantSelector.Reset(); // 新一批开始前重置轮转游标
        var results = new List<BatchIconResult>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (name, description) = items[i];
            ProgressChanged?.Invoke(this, new BatchProgress(i, items.Count, name));

            try
            {
                // 自动分类; 固定变体优先, 否则按分类轮转保证同分类风格互异
                var category = IconCategoryClassifier.Classify(description);
                var variant = FixedVariant ?? _variantSelector.Select(category);
                var result = await _iconGenerator.GenerateAsync(description, category, variant, ct);
                results.Add(new BatchIconResult(name, result.SvgText, result.ShapeCount, true, null));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                results.Add(new BatchIconResult(name, "", 0, false, ex.Message));
            }
        }

        ProgressChanged?.Invoke(this, new BatchProgress(items.Count, items.Count, ""));
        return results;
    }

    /// <summary>将 (名称,描述) 字符串对解析为列表 —— 支持 "名称:描述" 每行一个。</summary>
    public static List<(string Name, string Description)> ParseItemList(string? text)
    {
        var result = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            var separator = trimmed.IndexOf(':');
            if (separator > 0)
            {
                result.Add((trimmed[..separator].Trim(), trimmed[(separator + 1)..].Trim()));
            }
            else
            {
                // 无描述 —— 名称即描述
                result.Add((trimmed, trimmed));
            }
        }
        return result;
    }
}
