using System.Collections.Generic;

namespace HmiEditor.Core.Design;

/// <summary>
/// 设计预览 token 目录 —— 把 `design-previews/hmi-editor-r2-preview/colors_and_type.css`
/// 的 `--hmi-*` 色板设计稿化为**结构化、可机检**的对应关系。
///
/// <para><b>为什么需要：</b>09-14 审查（P2-3）指出"设计预览与编辑器模型无共享"——
/// 设计稿的色板改动没有任何机制流入编辑器，设计稿与已实现主题长期各写各的。</para>
///
/// <para><b>单向流入的实现：</b>CSS 是设计稿（上游），本目录是代码侧镜像，
/// 由 `DesignTokenCatalogTests` 锁定「目录中每个 token 的 CSS 变量必须存在于 CSS、
/// 且记录值与 CSS 一致」。设计师改了 CSS → 守卫变红 → 开发者**有意识**地决定
/// ①更新本目录（采纳设计稿）还是 ②写入有意分歧说明（暂不采纳）—— 而不是静默漂移。</para>
///
/// <para><b>与 <see cref="PaletteDefaults"/> 的关系：</b>后者是**已实现主题**的单一颜色来源
/// （设计器渲染/代码生成/主题模型三方共用）。本目录记录每个设计 token 与
/// <see cref="PaletteDefaults"/> 常量的语义对应（<c>PaletteConstant</c> 为空表示
/// 设计稿有该角色、已实现主题没有对应角色）；值不同是**有意分歧**（设计稿未落地），
/// 不是缺陷 —— 分歧被明确记录而不是靠人记住。</para>
/// </summary>
public static class DesignTokens
{
    /// <summary>单个设计 token 的目录条目。</summary>
    public sealed record Token(
        string Role,
        string CssVar,
        string CssValue,
        string? PaletteConstant,
        string Note);

    /// <summary>
    /// 全部色板 token（半径/字体不属色板，不入目录）。
    /// </summary>
    public static readonly IReadOnlyList<Token> All = new[]
    {
        new Token("Background", "--hmi-background", "#0D0E12",
            nameof(PaletteDefaults.Background),
            "画布/页面背景。预览为近黑，现行主题为深蓝 —— 有意分歧（设计稿未落地）。"),
        new Token("Foreground", "--hmi-foreground", "#E4E6EB",
            nameof(PaletteDefaults.Text),
            "主前景/文本。预览 #E4E6EB vs 现行 #e2e8f0 —— 有意分歧。"),
        new Token("Card", "--hmi-card", "#16181F",
            nameof(PaletteDefaults.Surface),
            "卡片面板底。预览 vs 现行 Surface #1a2332 —— 有意分歧。"),
        new Token("CardForeground", "--hmi-card-foreground", "#E4E6EB",
            null,
            "卡片内前景；已实现主题无独立 Card 角色（用 Text）。"),
        new Token("Popover", "--hmi-popover", "#1A1C24",
            null,
            "浮层底；已实现主题无独立 Popover 角色。"),
        new Token("PopoverForeground", "--hmi-popover-foreground", "#E4E6EB",
            null,
            "浮层前景；已实现主题无独立角色。"),
        new Token("Primary", "--hmi-primary", "#3B71F5",
            nameof(PaletteDefaults.Primary),
            "品牌主色。预览靛蓝 #3B71F5 vs 现行蓝 #2563eb —— 有意分歧（设计稿主色更深）。"),
        new Token("PrimaryForeground", "--hmi-primary-foreground", "#FFFFFF",
            null,
            "主色上的前景；已实现主题无独立常量（用 White）。"),
        new Token("Muted", "--hmi-muted", "#1E2029",
            null,
            "次要表面底；已实现主题无独立 Muted 角色。"),
        new Token("MutedForeground", "--hmi-muted-foreground", "#9BA1AE",
            nameof(PaletteDefaults.TextSecondary),
            "次要文本。预览 #9BA1AE vs 现行 #7c8aa0 —— 有意分歧。"),
        new Token("Border", "--hmi-border", "#2A2D38",
            nameof(PaletteDefaults.Border),
            "边框。预览 vs 现行 #334155 —— 有意分歧。"),
        new Token("Input", "--hmi-input", "#2A2D38",
            null,
            "输入控件边框（与 Border 同值）；已实现主题无独立 Input 角色（用 Border）。"),
        new Token("Ring", "--hmi-ring", "#3B71F5",
            null,
            "对焦环（与 Primary 同值）；已实现主题无独立 Ring 角色（用 SkyAccent 另值）。"),
    };

    /// <summary>CSS 变量 → 目录条目的快速索引（测试用）。</summary>
    public static IReadOnlyDictionary<string, Token> ByCssVar { get; } = BuildIndex();

    private static Dictionary<string, Token> BuildIndex()
    {
        var map = new Dictionary<string, Token>(All.Count);
        foreach (var t in All)
            map[t.CssVar] = t;
        return map;
    }
}