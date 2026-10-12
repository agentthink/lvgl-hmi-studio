using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 导出产物的<b>内部自洽</b>守卫 —— 只看 <c>SimulatorProjectExporter</c> 自己产出的文件，
/// 不跨到 <c>CodeGenerator</c> 那条链路。
///
/// <para><b>为什么需要这一层（2026-09-14）：</b>跨链路契约测试
/// （<c>CodeGenAssetSymbolContractTests</c>）对字体侧用的是<b>子集关系</b>断言，而字体符号
/// 在导出侧曾由 <b>8 处</b>调用点各自重算净化名 —— 只漂移其中一处时，其余调用点仍产出正确
/// 名字，并集被补齐，子集断言<b>依然成立</b>。负向验证实测：单点漂移 → 5/5 全过（假绿）；
/// 8 处全漂移 → 恰好 1 项红。这就是敏感度不足。</para>
///
/// <para>本类改用<b>严格相等</b>：某个片段在产物里以多种角色出现
/// （路径宏名 / 变量定义 / 变量 extern / 取地址 / 占位数组名），这些角色必须给出
/// <b>逐字符相同</b>的片段；且最终收口到「产物片段集合 == <see cref="CResourceSymbols"/>
/// 算出的集合」。任一处漂移，等式立刻破裂 —— 单点漂移即可捕获。</para>
///
/// <para><b>比对的坑：</b>不能用 <c>font_(\w+)_(\d+)</c> 这类「基/变体拆分」正则 ——
/// 基字体 id 若以 <c>_数字</c> 结尾（如 <c>font-1</c> → <c>font_1</c>），文本上与
/// 「基片段 <c>font</c> + 字号 <c>1</c>」完全同形，拆分会误判。故一律按<b>原始句柄名</b>比对。</para>
///
/// <para>另：正则用 <c>\w</c>（.NET 下含 Unicode 字母）而非 <c>[A-Za-z0-9_]</c>，
/// 因此中文片段（如 <c>font_中文字体</c>）也能被纳入比对 —— 跨链路那套正则做不到这点。</para>
/// </summary>
public class ExportResourceSymbolTests
{
    // ══════════════════════ 测试基建 ══════════════════════

    private static AssetItem ImageAsset(string id, string filePath = "/nonexistent/logo.bmp")
        => new() { Id = id, Name = $"图片_{id}", Type = AssetType.Image, FilePath = filePath, Width = 8, Height = 8, ColorDepth = 32 };

    private static AssetItem FontAsset(string id, string filePath = "/nonexistent/cjk.ttf", int height = 20)
        => new() { Id = id, Name = $"字体_{id}", Type = AssetType.Font, FilePath = filePath, Height = height };

    /// <summary>
    /// 项目 + 资产：刻意包含<b>需要净化的 id</b>（连字符）、<b>中文 id</b>、<b>空 id</b>
    /// （走兜底分支），以及一个非主题字号的控件（产生 <c>font_x_NN</c> 变体）。
    /// </summary>
    private static (HmiProject Project, AssetLibrary Assets) Fixture()
    {
        var assets = new AssetLibrary();
        assets.AddAsset(ImageAsset("img-1", "/nonexistent/logo.bmp"));
        assets.AddAsset(ImageAsset("图片2", "/nonexistent/截图.png"));
        assets.AddAsset(ImageAsset(string.Empty, "/nonexistent/placeholder.bmp"));
        assets.AddAsset(FontAsset("font-1", "/nonexistent/cjk.ttf", height: 20));
        assets.AddAsset(FontAsset("中文字体", "/nonexistent/zh.ttf", height: 0));
        assets.AddAsset(FontAsset(string.Empty, "/nonexistent/fallback.ttf", height: 24));

        var project = new HmiProject();
        project.Theme = new HmiTheme { FontSize = 14 };

        var screen = new HmiScreen { Id = "s1", Name = "主画面", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "lbl-1", Name = "标题", Type = HmiControlType.Label, Text = "运行中",
            X = 10, Y = 10, Width = 200, Height = 40,
            FontFamily = "font-1", FontSize = 22,           // ≠ 主题 14 → font_font_1_22
        });
        screen.Controls.Add(new HmiControl
        {
            Id = "img-ctl", Name = "图", Type = HmiControlType.Image, ImageAssetId = "img-1",
            X = 10, Y = 60, Width = 100, Height = 100, StretchMode = "Fill",
        });
        project.Screens.Add(screen);

        return (project, assets);
    }

    /// <summary>导出到独立临时目录，回读三份含资源符号的产物。</summary>
    private static (string MainC, string ImagesC, string FontsC) ExportArtifacts(
        HmiProject project, AssetLibrary assets)
    {
        var dir = TestTempDir.NewDir("export-symbols");
        try
        {
            var code = new HmiEditor.CodeGen.CodeGenerator().GenerateSimulatorModule(project, assets);
            SimulatorProjectExporter.Export(project, code, dir, assets);
            return (
                File.ReadAllText(Path.Combine(dir, "main.c")),
                File.ReadAllText(Path.Combine(dir, "generated_images.c")),
                File.ReadAllText(Path.Combine(dir, "generated_fonts.c")));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    private static (string MainC, string ImagesC, string FontsC) ExportFixture()
    {
        var (project, assets) = Fixture();
        return ExportArtifacts(project, assets);
    }

    /// <summary>按行首模式抓第 1 个捕获组，返回去重集合。</summary>
    private static HashSet<string> Grab(string code, string pattern)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(code, pattern, RegexOptions.Multiline))
            set.Add(m.Groups[1].Value);
        return set;
    }

    /// <summary>
    /// <c>generated_fonts.c</c> 里所有 <c>lv_font_t font_&lt;name&gt;;</c> 的 <b>原始</b> <c>name</c>。
    /// 含基字体与变尺寸字体，<b>不做</b>基/变体拆分（见类文档注释的歧义说明）。
    /// </summary>
    private static HashSet<string> FontHandleNames(string fontsC)
        => Grab(fontsC, @"^lv_font_t font_(\w+);");

    private static string Show(IEnumerable<string> items)
        => "{" + string.Join(", ", items.OrderBy(x => x, StringComparer.Ordinal)) + "}";

    /// <summary>断两个集合严格相等，失败信息里给出双向差异，便于定位是哪一侧漂了。</summary>
    private static void AssertSameSet(HashSet<string> actual, HashSet<string> expected, string what)
    {
        var onlyActual = actual.Except(expected).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var onlyExpected = expected.Except(actual).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.True(
            onlyActual.Count == 0 && onlyExpected.Count == 0,
            $"{what} 不在同一片段集合上：\n"
            + $"  仅左侧有（{onlyActual.Count}）: {Show(onlyActual)}\n"
            + $"  仅右侧有（{onlyExpected.Count}）: {Show(onlyExpected)}\n"
            + $"  左侧全部: {Show(actual)}\n"
            + $"  右侧全部: {Show(expected)}");
    }

    // ══════════════════════ A. generated_fonts.c 内部 ══════════════════════

    [Fact]
    public void FontsC_PathMacros_And_PxSizeMacros_CoverExactlyTheSameFragments()
    {
        var (_, _, fontsC) = ExportFixture();

        var pathMacros = Grab(fontsC, @"^#define FONT_PATH_(\w+)\s");
        var pxMacros = Grab(fontsC, @"^#define FONT_PXSIZE_(\w+)\s");

        Assert.NotEmpty(pathMacros);
        AssertSameSet(pxMacros, pathMacros, "FONT_PXSIZE_* 与 FONT_PATH_*");
    }

    [Fact]
    public void FontsC_CreateCall_OnlyUsesMacrosThatExist()
    {
        // init_font_assets() 里每个 lv_freetype_font_create 的路径宏实参，
        // 必须是一个已 #define 的 FONT_PATH_* —— 若某一组内部片段漂移，
        // 这里会出现「用了不存在的宏」而不是「集合不等」。
        var (_, _, fontsC) = ExportFixture();

        var createArgs = Grab(fontsC, @"^\s*FONT_PATH_(\w+),\s*$");
        var pathMacros = Grab(fontsC, @"^#define FONT_PATH_(\w+)\s");

        Assert.NotEmpty(createArgs);
        Assert.True(
            createArgs.IsSubsetOf(pathMacros),
            $"lv_freetype_font_create 用到了未定义的路径宏：{Show(createArgs.Except(pathMacros))}"
            + $"（已定义：{Show(pathMacros)}）");
    }

    [Fact]
    public void FontsC_EveryMacro_DefinedExactlyOnce()
    {
        // 同一片段重复 #define 是编译告警/错误；多调用点重算时最容易出现。
        var (_, _, fontsC) = ExportFixture();

        foreach (var (name, pattern) in new[]
        {
            (Name: "FONT_PATH_*", Pattern: @"^#define FONT_PATH_(\w+)\s"),
            (Name: "FONT_PXSIZE_*", Pattern: @"^#define FONT_PXSIZE_(\w+)\s"),
            (Name: "字体句柄定义", Pattern: @"^lv_font_t font_(\w+);"),
        })
        {
            var all = Regex.Matches(fontsC, pattern, RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value).ToList();
            var duplicates = all.GroupBy(x => x, StringComparer.Ordinal)
                .Where(g => g.Count() > 1).Select(g => $"{g.Key}×{g.Count()}").ToList();
            Assert.True(duplicates.Count == 0,
                $"generated_fonts.c 的{name}存在重复：{Show(duplicates)}");
        }
    }

    // ══════════════════════ B. main.c 与 generated_fonts.c 之间 ══════════════════════

    [Fact]
    public void MainC_FontExterns_AreExactlyTheBaseFontsDeclaredInFontsC()
    {
        // main.c 的 extern 只覆盖**基字体**（不含变尺寸变体），
        // 且与 generated_fonts.c 的 FONT_PATH_* 宏一一对应 —— 严格相等。
        // 这正是「局部漂移会被并集掩盖」的反面：这里没有并集，只有一对一。
        var (mainC, _, fontsC) = ExportFixture();

        var externs = Grab(mainC, @"^\s*extern lv_font_t font_(\w+);");
        var pathMacros = Grab(fontsC, @"^#define FONT_PATH_(\w+)\s");

        Assert.NotEmpty(externs);
        AssertSameSet(externs, pathMacros, "main.c 的 extern 字体声明与 generated_fonts.c 的 FONT_PATH_* 宏");
    }

    [Fact]
    public void MainC_DefaultFontPointer_RefersToAnExternedFont()
    {
        // load_project_font() 里取默认字体地址：&font_X 的 X 必须是 main.c 自己 extern 过的，
        // 否则链接期未定义引用（报错指向 main.c，根因在 generated_fonts.c）。
        var (mainC, _, fontsC) = ExportFixture();

        var externs = Grab(mainC, @"^\s*extern lv_font_t font_(\w+);");
        var taken = Grab(mainC, @"&font_(\w+);");
        var declared = FontHandleNames(fontsC);

        var single = Assert.Single(taken.ToList());
        Assert.True(externs.Contains(single),
            $"默认字体取地址 font_{single} 未在 main.c 中 extern（extern 集合：{Show(externs)}）");
        Assert.True(declared.Contains(single),
            $"默认字体取地址 font_{single} 在 generated_fonts.c 中没有定义（已定义：{Show(declared)}）");
    }

    // ══════════════════════ C. generated_images.c 内部 ══════════════════════

    [Fact]
    public void ImagesC_PathMacros_And_Descriptors_CoverExactlyTheSameFragments()
    {
        // 图片侧四种角色：路径宏、extern 声明、定义、LV_IMG_DECLARE，再加占位数组名。
        // 五者必须给出同一套片段。
        var (_, imagesC, _) = ExportFixture();

        var pathMacros = Grab(imagesC, @"^#define IMG_PATH_(\w+)\s");
        var externs = Grab(imagesC, @"^extern const lv_img_dsc_t img_(\w+);");
        var defs = Grab(imagesC, @"^const lv_img_dsc_t img_(\w+) = \{");
        var declares = Grab(imagesC, @"^LV_IMG_DECLARE\(img_(\w+)\);");
        var emptyMaps = Grab(imagesC, @"^static const unsigned char _img_(\w+)_empty_map\[1\] = \{0\};");

        Assert.NotEmpty(pathMacros);
        AssertSameSet(externs, pathMacros, "图片 extern 声明与 IMG_PATH_* 宏");
        AssertSameSet(defs, pathMacros, "图片描述符定义与 IMG_PATH_* 宏");
        AssertSameSet(declares, pathMacros, "LV_IMG_DECLARE 与 IMG_PATH_* 宏");
        AssertSameSet(emptyMaps, pathMacros, "占位数组名与 IMG_PATH_* 宏");
    }

    [Fact]
    public void ImagesC_EachDescriptor_DeclaredOnceAndDefinedOnce()
    {
        var (_, imagesC, _) = ExportFixture();

        foreach (var (name, pattern) in new[]
        {
            (Name: "extern 声明", Pattern: @"^extern const lv_img_dsc_t img_(\w+);"),
            (Name: "描述符定义", Pattern: @"^const lv_img_dsc_t img_(\w+) = \{"),
            (Name: "LV_IMG_DECLARE", Pattern: @"^LV_IMG_DECLARE\(img_(\w+)\);"),
        })
        {
            var all = Regex.Matches(imagesC, pattern, RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value).ToList();
            var duplicates = all.GroupBy(x => x, StringComparer.Ordinal)
                .Where(g => g.Count() > 1).Select(g => $"{g.Key}×{g.Count()}").ToList();
            Assert.True(duplicates.Count == 0,
                $"generated_images.c 的{name}存在重复：{Show(duplicates)}");
        }
    }

    // ══════════════════════ D. 收口：产物 == 符号表 ══════════════════════

    [Fact]
    public void ArtifactFragments_EqualTheSymbolTableExactly()
    {
        // 收口断言：产物里出现的资源符号片段集合，必须**恰好等于**符号表算出的集合。
        // 表始终走 CIdentifier.ForAsset，所以只要产物任一环节自行重算并漂移，
        // 该环节的集合就会与表不等 —— 单点漂移即可捕获（补上跨链路子集断言的盲区）。
        var (project, assets) = Fixture();
        var images = assets.Assets.Where(a => a.Type == AssetType.Image).ToList();
        var fonts = assets.Assets.Where(a => a.Type == AssetType.Font).ToList();
        var table = CResourceSymbols.Create(images, fonts, project);
        var (mainC, imagesC, fontsC) = ExportArtifacts(project, assets);

        var expectedImages = table.Images.Select(i => i.Fragment).ToHashSet(StringComparer.Ordinal);
        var expectedFonts = table.Fonts.Select(f => f.Fragment).ToHashSet(StringComparer.Ordinal);
        var expectedHandles = new HashSet<string>(expectedFonts, StringComparer.Ordinal);
        foreach (var v in table.FontVariants) expectedHandles.Add($"{v.FontFragment}_{v.Size}");

        AssertSameSet(Grab(imagesC, @"^#define IMG_PATH_(\w+)\s"), expectedImages, "IMG_PATH_* 与符号表图片片段");
        AssertSameSet(Grab(fontsC, @"^#define FONT_PATH_(\w+)\s"), expectedFonts, "FONT_PATH_* 与符号表字体片段");
        AssertSameSet(Grab(mainC, @"^\s*extern lv_font_t font_(\w+);"), expectedFonts, "main.c extern 与符号表字体片段");
        AssertSameSet(FontHandleNames(fontsC), expectedHandles, "字体句柄定义（基 + 变体）与符号表");
    }

    [Fact]
    public void SymbolTable_IsTheOnlySourceForTheVariantFragments()
    {
        // 变尺寸字体的基片段必须能在基字体宏里找到 —— 否则 font_X_22 会引用不存在的 FONT_PATH_X。
        // 用表算出「变体基片段」的期望集合，再与产物的 FONT_PATH_* 比对。
        var (project, assets) = Fixture();
        var fonts = assets.Assets.Where(a => a.Type == AssetType.Font).ToList();
        var table = CResourceSymbols.Create(new List<AssetItem>(), fonts, project);
        var (_, _, fontsC) = ExportArtifacts(project, assets);

        var variants = table.FontVariants.Select(v => v.FontFragment).ToHashSet(StringComparer.Ordinal);
        var pathMacros = Grab(fontsC, @"^#define FONT_PATH_(\w+)\s");

        Assert.NotEmpty(variants);   // Fixture 里有一个 FontSize=22 的控件
        Assert.True(variants.IsSubsetOf(pathMacros),
            $"变尺寸字体的基片段没有对应 FONT_PATH_* 宏：{Show(variants.Except(pathMacros))}（已定义：{Show(pathMacros)}）");
    }
}
