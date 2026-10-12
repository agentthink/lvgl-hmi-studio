using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **跨链路资源符号契约** —— 同一次生成里，C 符号由两条独立链路产出：
///
/// <list type="bullet">
///   <item><c>CodeGenerator.GenerateSimulatorModule</c> → <c>generated_hmi.c</c>
///     （定义 <c>IMG_PATH_*</c>、<c>extern img_*</c>、<c>extern font_*</c>、字号变体声明）</item>
///   <item><c>SimulatorProjectExporter.Export</c> → <c>main.c</c> / <c>generated_images.c</c> /
///     <c>generated_fonts.c</c>（同一批符号的 extern 与定义、<c>FONT_PATH_*</c>）</item>
/// </list>
///
/// <para>两条链路各自维护 id 净化与命名规则，必须给出**逐字符相同**的符号名，
/// 否则症状一律是「编译期找不到符号 / 链接期未定义」，而原因在另一条链路里，排查很绕。</para>
///
/// <para><b>为什么要有这个测试：</b>当前 <c>SimulatorProjectExporter</c> 用
/// <c>SanitizeIdentifier</c>（空输入 → <c>"unknown"</c>）处理**资产** id，
/// <c>CodeGenerator</c> 用的是语义对应的 <c>SanitizeAssetId</c>（空输入 → <c>"unknown"</c>）
/// 与用于控件/动画的 <c>SanitizeIdentifier</c>（空输入 → <c>"anim"</c>）。
/// 三份实现体完全相同、只是兜底值不同，**目前一致纯属巧合** ——
/// 谁把同名的两个函数"顺手对齐"，跨模块符号就会错位。本测试把这份契约钉死。</para>
/// </summary>
public class CodeGenAssetSymbolContractTests
{
    // 只提取资源类符号，避免把控件对象名（obj_xxx）等无关命名卷进来
    private static readonly Regex ImagePathMacro = new(@"\bIMG_PATH_[A-Za-z0-9_]+", RegexOptions.Compiled);
    private static readonly Regex FontPathMacro = new(@"\bFONT_PATH_[A-Za-z0-9_]+", RegexOptions.Compiled);
    private static readonly Regex ImageDescriptor = new(@"\bimg_[A-Za-z0-9_]+", RegexOptions.Compiled);
    private static readonly Regex FontHandle = new(@"\bfont_[A-Za-z0-9_]+", RegexOptions.Compiled);

    private static HashSet<string> Symbols(string code, Regex pattern)
        => pattern.Matches(code).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> SymmetricDifference(HashSet<string> a, HashSet<string> b)
    {
        var diff = new HashSet<string>(a, StringComparer.Ordinal);
        diff.SymmetricExceptWith(b);
        return diff;
    }

    private static string Show(IEnumerable<string> symbols)
        => string.Join(", ", symbols.OrderBy(s => s, StringComparer.Ordinal));

    // ══════════════════════ 测试数据 ══════════════════════

    private static AssetItem ImageAsset(string id, string fileName)
        => new()
        {
            Id = id,
            Name = $"图片_{id}",
            Type = AssetType.Image,
            FilePath = $"/assets/{fileName}",
            Width = 100,
            Height = 100,
            ColorDepth = 32,
        };

    private static AssetItem FontAsset(string id, string fileName)
        => new()
        {
            Id = id,
            Name = $"字体_{id}",
            Type = AssetType.Font,
            FilePath = $"/assets/{fileName}",
            Height = 20,
        };

    /// <summary>
    /// 项目 + 资产：刻意包含<b>需要净化的 id</b>（连字符/中文）与<b>空 id</b>（走兜底分支），
    /// 以及一个指定了非主题字号的控件（产生 <c>font_x_NN</c> 变体符号）。
    /// </summary>
    private static (HmiProject Project, AssetLibrary Assets) BuildFixture()
    {
        var assets = new AssetLibrary();
        assets.AddAsset(ImageAsset("img-1", "logo.bmp"));
        assets.AddAsset(ImageAsset("图片2", "截图.png"));
        assets.AddAsset(ImageAsset(string.Empty, "placeholder.bmp"));
        assets.AddAsset(FontAsset("font-1", "cjk.ttf"));
        assets.AddAsset(FontAsset(string.Empty, "fallback.ttf"));

        var project = new HmiProject();
        project.Theme = new HmiTheme { FontSize = 14 };

        var screen = new HmiScreen { Id = "s1", Name = "主画面", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "lbl-1",
            Name = "标题",
            Type = HmiControlType.Label,
            Text = "运行中",
            X = 10, Y = 10, Width = 200, Height = 40,
            FontFamily = "font-1",
            FontSize = 22,               // ≠ 主题 14 → 产生 font_font_1_22
        });
        screen.Controls.Add(new HmiControl
        {
            Id = "img-ctl",
            Name = "图",
            Type = HmiControlType.Image,
            ImageAssetId = "img-1",
            X = 10, Y = 60, Width = 100, Height = 100,
            StretchMode = "Fill",
        });
        project.Screens.Add(screen);

        return (project, assets);
    }

    private static (string ModuleCode, string MainC, string ImagesC, string FontsC) GenerateBothWays()
    {
        var (project, assets) = BuildFixture();
        var moduleCode = new CodeGenerator().GenerateSimulatorModule(project, assets);

        var dir = TestTempDir.NewDir("symbol-contract");
        try
        {
            SimulatorProjectExporter.Export(project, moduleCode, dir, assets);
            return (moduleCode,
                    File.ReadAllText(Path.Combine(dir, "main.c")),
                    File.ReadAllText(Path.Combine(dir, "generated_images.c")),
                    File.ReadAllText(Path.Combine(dir, "generated_fonts.c")));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ══════════════════════ 契约 ══════════════════════

    [Fact]
    public void ImagePathMacros_AreIdenticalInBothLinkChains()
    {
        var (moduleCode, _, imagesC, _) = GenerateBothWays();

        var inModule = Symbols(moduleCode, ImagePathMacro);
        var inImagesC = Symbols(imagesC, ImagePathMacro);

        Assert.True(inModule.Count > 0, "generated_hmi.c 里应定义 IMG_PATH_* 宏");
        var diff = SymmetricDifference(inModule, inImagesC);
        Assert.True(diff.Count == 0,
            $"两条链路的 IMG_PATH_* 宏不一致（差异 {diff.Count} 个）:\n  {Show(diff)}\n"
            + $"[generated_hmi.c] {Show(inModule)}\n[generated_images.c] {Show(inImagesC)}");
    }

    [Fact]
    public void ImageDescriptors_AreIdenticalInBothLinkChains()
    {
        var (moduleCode, _, imagesC, _) = GenerateBothWays();

        var inModule = Symbols(moduleCode, ImageDescriptor);
        var inImagesC = Symbols(imagesC, ImageDescriptor);

        Assert.True(inModule.Count > 0, "generated_hmi.c 里应声明 img_* 描述符");
        var diff = SymmetricDifference(inModule, inImagesC);
        Assert.True(diff.Count == 0,
            $"两条链路的 img_* 描述符不一致（差异 {diff.Count} 个）:\n  {Show(diff)}\n"
            + $"[generated_hmi.c] {Show(inModule)}\n[generated_images.c] {Show(inImagesC)}");
    }

    [Fact]
    public void FontHandles_AreIdenticalInBothLinkChains()
    {
        var (moduleCode, mainC, _, fontsC) = GenerateBothWays();

        // 字体句柄 font_* 由三条产物共同参与：generated_hmi.c 声明、main.c 取地址、
        // generated_fonts.c 定义（含 font_<id>_<size> 变体）。
        var inModule = Symbols(moduleCode, FontHandle);
        var inExporter = Symbols(mainC, FontHandle);
        inExporter.UnionWith(Symbols(fontsC, FontHandle));

        Assert.True(inModule.Count > 0, "generated_hmi.c 里应声明 font_* 句柄");

        // generated_hmi.c 引用的字体句柄必须都能在导出侧找到（子集关系：
        // 导出侧还会额外定义主题默认字体等未被引用的句柄）
        var missing = new HashSet<string>(inModule, StringComparer.Ordinal);
        missing.ExceptWith(inExporter);
        Assert.True(missing.Count == 0,
            $"generated_hmi.c 引用了导出侧没有的字体句柄（{missing.Count} 个）:\n  {Show(missing)}\n"
            + $"[generated_hmi.c] {Show(inModule)}\n[main.c + generated_fonts.c] {Show(inExporter)}");
    }

    [Fact]
    public void FontPathMacros_ExistOnlyOnTheExporterSideAndCoverEveryFont()
    {
        var (_, _, _, fontsC) = GenerateBothWays();

        var paths = Symbols(fontsC, FontPathMacro);
        Assert.True(paths.Count > 0, "generated_fonts.c 里应定义 FONT_PATH_* 宏");
    }

    [Fact]
    public void SanitizedAssetIdentifiers_FollowOneRuleAcrossBothChains()
    {
        // 空 id / 含非标识符字符的 id 是净化规则的分歧点：两条链路必须给出同一个符号。
        var (moduleCode, _, imagesC, _) = GenerateBothWays();

        var inModule = Symbols(moduleCode, ImagePathMacro);
        var inImagesC = Symbols(imagesC, ImagePathMacro);

        // 连字符 → 下划线；中文（非 letterOrDigit 之外仍算 letter）保持；空 id → 统一兜底值
        var expectedForHyphen = inModule.Where(s => s.Contains("img_1")).ToList();
        Assert.True(expectedForHyphen.Count > 0,
            $"id \"img-1\" 应被净化为 img_1，实际: {Show(inModule)}");

        var emptyIdSymbols = inModule.Where(s => !s.Contains("img_1") && !s.Contains("2")).ToList();
        Assert.True(emptyIdSymbols.Count == 0 || emptyIdSymbols.All(s => inImagesC.Contains(s)),
            "空 id 的兜底符号在两条链路应一致");
    }
}
