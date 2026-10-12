using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>
/// 新建屏幕的<b>唯一工厂</b> —— 工程内所有「凭空造出一个 HmiScreen」的路径都必须走这里。
/// <para>
/// <b>为什么要有它：</b>此前有 5 条各自独立的路径在造屏幕，尺寸来源各不相同
/// （屏幕管理器读工程默认、工具栏硬编码 1024×600、新建工程硬编码、AI Agent 工具默认 1024×600、
/// AI 自然语言生成默认 800×480），而 <see cref="ScreenDefaultsConfig"/> 的其余字段
/// （自适应模式 / 背景色）一条都没被读到。结果「屏幕默认模板」这个分组在界面上摆着，
/// 实际只有 1/5 的路径听它的话。
/// </para>
/// <para>
/// <b>优先级契约：显式参数 &gt; 工程默认值。</b>调用方拿到 LLM / 工具参数时，
/// 「参数没给」必须传 <c>null</c>，不能填一个自造的常量默认值 ——
/// 否则工程设置里的默认尺寸会被静默压掉（这正是 800×480 那个 bug 的成因）。
/// </para>
/// <para>
/// <b>不在这里做的事：</b>不把屏幕加进 <c>project.Screens</c>（由调用方决定加入时机与撤销点），
/// 也不生成 Id 之外的状态。
/// </para>
/// </summary>
public static class ScreenFactory
{
    /// <summary>
    /// 工程完全没有屏幕、也没有可用的 <see cref="ScreenDefaultsConfig"/> 时的最后兜底尺寸。
    /// 对齐 T113 7 寸屏（1024×600）。正常路径不应走到这里。
    /// </summary>
    public const int FallbackWidth = 1024;

    /// <summary>兜底高度。见 <see cref="FallbackWidth"/>。</summary>
    public const int FallbackHeight = 600;

    /// <summary>
    /// 按工程默认模板造一个新屏幕。
    /// </summary>
    /// <param name="project">目标工程；为 null 时用兜底尺寸（仅限无可依托上下文的场景）。</param>
    /// <param name="name">屏幕名。null/空白 → 按现有屏幕数生成「Screen N」。</param>
    /// <param name="width">
    /// 显式宽度。null → 取工程默认。**「参数没给」必须传 null**，不要填常量。
    /// </param>
    /// <param name="height">显式高度。语义同 <paramref name="width"/>。</param>
    /// <param name="bgColor">
    /// 显式背景色。null → 取工程默认（工程默认也可能为 null，表示跟随主题）。
    /// </param>
    /// <returns>尚未加入 <c>project.Screens</c> 的新屏幕。</returns>
    public static HmiScreen Create(
        HmiProject? project,
        string? name = null,
        double? width = null,
        double? height = null,
        string? bgColor = null)
    {
        var cfg = project?.ScreenDefaultsConfig;

        var w = Sanitize(width ?? (double?)cfg?.DefaultWidth, FallbackWidth);
        var h = Sanitize(height ?? (double?)cfg?.DefaultHeight, FallbackHeight);

        return new HmiScreen
        {
            Name = ResolveName(project, name),
            Width = w,
            Height = h,
            // 显式参数优先；两者都没有时交给工程默认（可能为 null = 跟随主题背景）。
            BgColor = bgColor ?? cfg?.DefaultBgColor,
            AdaptiveMode = cfg?.DefaultAdaptiveMode ?? ScreenAdaptiveMode.None,
        };
    }

    /// <summary>
    /// 按工程现有屏幕数生成下一个默认名称（<c>Screen N</c>，N = 现有数 + 1）。
    /// </summary>
    public static string NextScreenName(HmiProject? project)
    {
        var count = project?.Screens.Count ?? 0;
        return $"Screen {count + 1}";
    }

    private static string ResolveName(HmiProject? project, string? name) =>
        string.IsNullOrWhiteSpace(name) ? NextScreenName(project) : name.Trim();

    /// <summary>
    /// 把「配置里可能是 0 或负数的尺寸」收拾成可用的整数像素。
    /// 工程设置面板允许手输，<c>DefaultWidth = 0</c> 在 JSON 里是合法值，
    /// 但会生成 <c>lv_obj_set_size(screen, 0, 0)</c> —— 一个看不见的屏幕。
    /// </summary>
    private static double Sanitize(double? value, int fallback)
    {
        if (value is not double v || !double.IsFinite(v) || v <= 0) return fallback;
        return Math.Round(v);
    }
}
