using HmiEditor.Core.Models;

namespace HmiEditor.Core.CodeGen;

/// <summary>图片资源 → 导出侧 C 符号。</summary>
/// <param name="Id">原始资源 id（仅用于注释）。</param>
/// <param name="Name">显示名（仅用于注释）。</param>
/// <param name="Fragment">符号片段：<c>img_<b>Fragment</b></c> / <c>IMG_PATH_<b>Fragment</b></c>。</param>
/// <param name="FileName">磁盘文件名：<c>SIM_ASSET_DIR "/<b>FileName</b>"</c>。</param>
public sealed record ImageSymbol(string Id, string Name, string Fragment, string FileName);

/// <summary>字体资源 → 导出侧 C 符号。</summary>
/// <param name="Id">原始资源 id（仅用于注释）。</param>
/// <param name="Name">显示名（仅用于注释）。</param>
/// <param name="Fragment">符号片段：<c>font_<b>Fragment</b></c> / <c>FONT_PATH_<b>Fragment</b></c> / <c>FONT_PXSIZE_<b>Fragment</b></c>。</param>
/// <param name="FileName">磁盘文件名。</param>
/// <param name="PixelSize">初始化像素尺寸（<c>Height &gt; 0</c> 时取 Height，否则 20）。</param>
public sealed record FontSymbol(string Id, string Name, string Fragment, string FileName, int PixelSize);

/// <summary>控件自定义字号的字体变体符号 <c>font_<b>FontFragment</b>_<b>Size</b></c>。</summary>
public sealed record FontVariantSymbol(string FontFragment, int Size);

/// <summary>
/// 导出侧资源符号表 —— **每批资源只在这里计算一次**，各产物代码段一律查表。
///
/// <para><b>为什么需要这张表（2026-09-14 引入）：</b>同一个符号此前在
/// <c>SimulatorProjectExporter</c> 内部被 <b>9 处</b>调用点各自重算净化名
/// （字体 8 处、图片 3 处，另有 <c>control.FontFamily</c> 一处）。统一净化实现
/// （<see cref="CIdentifier"/>）只能保证「规则相同」，不能保证「调用点不漂移」——
/// 任一处分支条件写歪或兜底值不一致，**同一份导出产物内部就会自相矛盾**：
/// <c>generated_fonts.c</c> 定义 <c>font_X</c>、<c>main.c</c> 却 <c>extern font_Y</c>，
/// 症状是链接期未定义引用，而两处代码各自看都「没问题」。</para>
///
/// <para>样例（负向验证实测）：把导出侧字体净化改成分散的多份实现后，
/// <c>generated_fonts.c</c> 里同时出现了 <c>font_FBREAK</c> 与 <c>font_FONTBREAK</c>。</para>
///
/// <para><b>契约：</b>产物里出现的每个资源符号，其「片段」必须来自本表；
/// 产物代码段不得再自行调用 <see cref="CIdentifier"/> 去算资源符号。
/// 守卫见 <c>CodeGenAssetSymbolContractTests</c> 与 <c>ExportResourceSymbolTests</c>。</para>
/// </summary>
public sealed class CResourceSymbols
{
    /// <summary>字体资源在无 <c>Height</c> 时的默认初始化像素尺寸（历史取值）。</summary>
    public const int DefaultFontPixelSize = 20;

    /// <summary>主题未配置字号时的默认值（历史取值）。</summary>
    public const int DefaultThemeFontSize = 14;

    private CResourceSymbols(
        IReadOnlyList<ImageSymbol> images,
        IReadOnlyList<FontSymbol> fonts,
        IReadOnlyList<FontVariantSymbol> fontVariants)
    {
        Images = images;
        Fonts = fonts;
        FontVariants = fontVariants;
    }

    /// <summary>图片符号，顺序与传入的图片资源一致。</summary>
    public IReadOnlyList<ImageSymbol> Images { get; }

    /// <summary>字体符号，顺序与传入的字体资源一致（<c>Fonts[0]</c> 即默认字体）。</summary>
    public IReadOnlyList<FontSymbol> Fonts { get; }

    /// <summary>控件自定义字号变体，已去重并按 (字片段, 字号) 排序。</summary>
    public IReadOnlyList<FontVariantSymbol> FontVariants { get; }

    /// <summary>默认字体片段（首个字体资源），无字体资源时为 <c>null</c>。</summary>
    public string? DefaultFontFragment => Fonts.Count > 0 ? Fonts[0].Fragment : null;

    /// <summary>
    /// 从资源库与项目计算整批符号。<b>全流程只调用一次</b>（在 <c>Export</c> 入口）。
    /// </summary>
    public static CResourceSymbols Create(
        IReadOnlyList<AssetItem> imageAssets,
        IReadOnlyList<AssetItem> fontAssets,
        HmiProject project)
    {
        ArgumentNullException.ThrowIfNull(imageAssets);
        ArgumentNullException.ThrowIfNull(fontAssets);
        ArgumentNullException.ThrowIfNull(project);

        var images = new List<ImageSymbol>(imageAssets.Count);
        foreach (var asset in imageAssets)
        {
            images.Add(new ImageSymbol(
                asset.Id,
                asset.Name,
                CIdentifier.ForAsset(asset.Id),
                FileNameOf(asset.FilePath)));
        }

        var fonts = new List<FontSymbol>(fontAssets.Count);
        foreach (var asset in fontAssets)
        {
            fonts.Add(new FontSymbol(
                asset.Id,
                asset.Name,
                CIdentifier.ForAsset(asset.Id),
                FileNameOf(asset.FilePath),
                asset.Height > 0 ? asset.Height : DefaultFontPixelSize));
        }

        var defaultFontFragment = fonts.Count > 0 ? fonts[0].Fragment : null;
        var variants = CollectFontVariants(project, defaultFontFragment);

        return new CResourceSymbols(images, fonts, variants);
    }

    /// <summary>
    /// 收集控件自定义字号需要的变尺寸字体组合（<c>font_&lt;字片段&gt;_&lt;字号&gt;</c>）。
    ///
    /// <para>与主题默认字号相同的控件被跳过 —— 它们由基础字体变量 <c>font_&lt;片段&gt;</c> 覆盖。
    /// 控件未指定 <c>FontFamily</c> 时用默认字体片段。</para>
    ///
    /// <para><b>行为保持（勿改）</b>：<c>FontFamily</c> 即使不在字体资源库内也会生成变体符号，
    /// 不做存在性过滤 —— 过滤会改变既有产物。</para>
    /// </summary>
    private static List<FontVariantSymbol> CollectFontVariants(HmiProject project, string? defaultFontFragment)
    {
        var result = new HashSet<(string FontId, int Size)>();
        var themeSize = project.Theme?.FontSize ?? DefaultThemeFontSize;

        foreach (var screen in project.Screens)
        {
            foreach (var control in screen.Controls)
            {
                var size = control.FontSize > 0 ? control.FontSize : themeSize;
                if (size == themeSize) continue; // 主题默认字号由基础字体变量覆盖

                var fontId = !string.IsNullOrEmpty(control.FontFamily)
                    ? CIdentifier.ForAsset(control.FontFamily)
                    : defaultFontFragment;

                if (string.IsNullOrEmpty(fontId)) continue;
                result.Add((fontId, size));
            }
        }

        return result
            .OrderBy(x => x.FontId)
            .ThenBy(x => x.Size)
            .Select(x => new FontVariantSymbol(x.FontId, x.Size))
            .ToList();
    }

    /// <summary>
    /// 取资源文件在磁盘上的文件名（导出时统一转成正斜杠再取末段，兼容 Windows 录入的路径）。
    /// </summary>
    private static string FileNameOf(string? filePath)
        => Path.GetFileName((filePath ?? string.Empty).Replace("\\", "/"));
}
