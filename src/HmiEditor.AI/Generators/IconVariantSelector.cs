namespace HmiEditor.AI.Generators;

/// <summary>
/// 变体选择策略。
/// </summary>
public enum VariantSelectionMode
{
    /// <summary>按分类使用代表性变体(稳定一致, 默认)。</summary>
    Default,
    /// <summary>批量轮转: 同一分类下连续图标使用不同变体(最大化视觉差异)。</summary>
    Rotate,
    /// <summary>随机变体(每次生成不可预测)。</summary>
    Random
}

/// <summary>
/// 图标变体选择器 —— 依据分类与选择策略确定生成时使用的 <see cref="IconStyleVariant"/>。
/// 设计目标(对标 iconfont.cn 多样性):
///   - Default: 每类图标有代表性风格, 保证类别内的视觉一致性
///   - Rotate:   批量生成时按分类轮转, 使"泵/电机/阀门"等同类设备呈现不同风格
///   - Random:   完全随机, 用于探索性生成
/// </summary>
public sealed class IconVariantSelector
{
    private readonly VariantSelectionMode _mode;
    private readonly Random _rng;
    private readonly Dictionary<IconCategory, int> _rotationCursor = new();

    public IconVariantSelector(VariantSelectionMode mode = VariantSelectionMode.Default, int? seed = null)
    {
        _mode = mode;
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>分类 → 代表性变体(Default 模式)。</summary>
    public static IconStyleVariant GetDefaultVariant(IconCategory category) => category switch
    {
        IconCategory.Device => IconStyleVariant.Linear,
        IconCategory.Action => IconStyleVariant.Filled,
        IconCategory.Status => IconStyleVariant.Outline,
        IconCategory.Navigation => IconStyleVariant.Linear,
        IconCategory.Process => IconStyleVariant.Geometric,
        IconCategory.Communication => IconStyleVariant.Rounded,
        IconCategory.Safety => IconStyleVariant.Accent,
        IconCategory.Environment => IconStyleVariant.Minimal,
        _ => IconStyleVariant.Linear
    };

    /// <summary>为指定分类选择变体。</summary>
    public IconStyleVariant Select(IconCategory category)
    {
        switch (_mode)
        {
            case VariantSelectionMode.Rotate:
                return NextRotation(category);
            case VariantSelectionMode.Random:
                return IconVariantCatalog.All[_rng.Next(IconVariantCatalog.All.Length)];
            default:
                return GetDefaultVariant(category);
        }
    }

    /// <summary>
    /// 轮转策略: 每个分类维护游标, 从 <see cref="IconVariantCatalog.All"/> 中偏移选择。
    /// 同一分类下第一次用代表性变体, 之后依次偏移, 保证同类图标风格互不重复。
    /// </summary>
    private IconStyleVariant NextRotation(IconCategory category)
    {
        var all = IconVariantCatalog.All;
        var defaultIdx = Array.IndexOf(all, GetDefaultVariant(category));

        _rotationCursor.TryGetValue(category, out var counter);
        _rotationCursor[category] = counter + 1;

        var offset = counter % all.Length;
        return all[(defaultIdx + offset) % all.Length];
    }

    /// <summary>重置轮转游标(新一批生成前调用)。</summary>
    public void Reset()
    {
        _rotationCursor.Clear();
    }
}
