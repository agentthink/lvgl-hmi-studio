using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using HmiEditor.Core.Design;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 设计-代码单一事实源守卫（P0-4，2026-09-18）：
/// `design-previews/hmi-editor-r2-preview/colors_and_type.css` 的设计 token
/// 与 `DesignTokens` 目录、`PaletteDefaults` 常量的对应关系**由测试锁定**。
///
/// <para><b>单向流入机制</b>：CSS 是设计稿（上游），`DesignTokens` 是代码侧镜像。
/// 设计师改了 CSS 色值 → 本类变红 → 开发者有意识地更新目录或写明有意分歧
/// （而不是静默漂移）。</para>
/// </summary>
public class DesignTokenCatalogTests
{
    private const string CssRelativePath =
        "design-previews/hmi-editor-r2-preview/colors_and_type.css";

    private static readonly Regex CssVarDecl =
        new(@"(--hmi-[a-z0-9-]+)\s*:\s*([^;]+);", RegexOptions.Compiled);

    /// <summary>解析 CSS 的 `--hmi-*` 变量声明（去掉注释与空白）。</summary>
    private static Dictionary<string, string> ParseCssTokens(string css)
    {
        // 去掉块注释（设计稿头部有说明注释）
        var stripped = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in CssVarDecl.Matches(stripped))
        {
            var name = m.Groups[1].Value;
            var value = m.Groups[2].Value.Trim();
            if (value.StartsWith("\"", StringComparison.Ordinal)) continue; // 字体栈等非色板值不入目录
            map[name] = value;
        }
        return map;
    }

    private static string ReadCss() =>
        File.ReadAllText(Path.Combine(SourceScanner.RepoRoot, CssRelativePath));

    [Fact]
    public void Catalog_EveryTokenExistsInCss_WithMatchingValue()
    {
        // 设计稿是上游：目录里每个 token 的 CSS 变量必须存在于 CSS、且记录值与 CSS 一致。
        // 设计师改 CSS 色值 → 这里红 → 有意识更新 DesignTokens（单向流入的第一步）。
        var cssTokens = ParseCssTokens(ReadCss());
        var violations = DesignTokens.All
            .Where(t => !cssTokens.TryGetValue(t.CssVar, out var cssValue)
                        || !string.Equals(cssValue, t.CssValue, StringComparison.OrdinalIgnoreCase))
            .Select(t => $"{t.CssVar}: 目录={t.CssValue} CSS="
                         + (cssTokens.TryGetValue(t.CssVar, out var v) ? v : "（不存在）"))
            .ToList();

        Assert.True(
            violations.Count == 0,
            "以下设计 token 的目录记录与 colors_and_type.css 不一致"
            + "（CSS 是设计稿上游，改 CSS 后必须同步 DesignTokens 或写明有意分歧）：\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void CssParser_FindsAllColorTokens()
    {
        // 自检：解析器必须真的扫得到色板 token（≥10），防正则退化后空跑。
        var cssTokens = ParseCssTokens(ReadCss());
        var colorTokens = cssTokens
            .Where(kv => kv.Value.StartsWith("#", StringComparison.Ordinal))
            .ToList();
        Assert.True(
            colorTokens.Count >= 10,
            $"colors_and_type.css 只解析到 {colorTokens.Count} 个色板 token（应 ≥10）—— 解析器可能失效。");
        Assert.True(
            DesignTokens.All.Count >= 10,
            $"DesignTokens 目录只有 {DesignTokens.All.Count} 条（应 ≥10）—— 目录可能被误删。");
    }

    [Fact]
    public void Catalog_MappedPaletteConstants_AllExist()
    {
        // 目录里登记的 PaletteDefaults 常量必须真实存在（防抄错常量名）。
        var missing = DesignTokens.All
            .Where(t => t.PaletteConstant != null)
            .Where(t => typeof(PaletteDefaults).GetField(t.PaletteConstant!,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static) is null)
            .Select(t => $"{t.Role} → PaletteDefaults.{t.PaletteConstant}")
            .ToList();

        Assert.True(
            missing.Count == 0,
            "以下 DesignTokens 登记的 PaletteDefaults 常量不存在：\n"
            + string.Join("\n", missing));
    }

    [Fact]
    public void Css_IsNotSilentlyConsumedByEditor_ExplicitCorrespondenceOnly()
    {
        // 核心断言：设计预览的色板**不被编辑器运行时消费**（仍是设计稿/提案），
        // 编辑器用的是 PaletteDefaults（已实现主题）。这条不是缺陷 ——
        // 它把"设计稿与实现是两套"从隐形漂移变成显式、可审的对应关系。
        // 若将来要真正让设计稿驱动编辑器，应先改这条断言 + DesignTokens 的
        // PaletteConstant 映射 + PaletteDefaults 值（三步一起，避免只改一处）。
        var cssTokens = ParseCssTokens(ReadCss());
        var shipped = typeof(PaletteDefaults)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 设计稿的色板值不得被当作已实现主题色使用 —— 即 CSS 色值集合与
        // PaletteDefaults 色值集合不应大量重叠（重叠说明"设计稿色已悄悄进了实现"）。
        var overlap = cssTokens.Values
            .Where(v => v.StartsWith("#", StringComparison.Ordinal))
            .Count(shipped.Contains);

        Assert.True(
            overlap <= 2,
            $"colors_and_type.css 与 PaletteDefaults 有 {overlap} 个相同色值 —— "
            + "设计稿色值疑似已静默进入实现主题；若有意，请走 DesignTokens 映射 + 同步改两者。");
    }

    [Theory]
    [InlineData("hmi-editor-r1-preview/hmi-editor-r1-preview.design")]
    [InlineData("hmi-editor-r2-preview/hmi-editor-r2-preview.design")]
    public void DesignFile_HasStructuredModel(string relative)
    {
        // `.design` 结构化模型验证：必须是合法 JSON 且具备 data[] + config 结构。
        var path = Path.Combine(SourceScanner.RepoRoot, "design-previews", relative);
        Assert.True(File.Exists(path), $"未找到 {relative}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("data", out var data), $"{relative}: 缺 data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.True(data.GetArrayLength() >= 1, $"{relative}: data 为空数组");

        var first = data[0];
        Assert.True(first.TryGetProperty("id", out _), $"{relative}: data[0] 缺 id");
        Assert.True(first.TryGetProperty("type", out _), $"{relative}: data[0] 缺 type");
        Assert.True(first.TryGetProperty("version", out _), $"{relative}: data[0] 缺 version");

        Assert.True(root.TryGetProperty("config", out var config), $"{relative}: 缺 config");
        Assert.Equal(JsonValueKind.Object, config.ValueKind);
    }

    // ── 设计层无裸 hex（设计-代码单一事实源的收尾防线）──

    private static readonly Regex BareHex =
        new(@"""[#][0-9a-fA-F]{6}""", RegexOptions.Compiled);

    /// <summary>
    /// 设计层（src/HmiEditor.Core/Design/）里只有两个文件允许写裸 hex：
    /// `PaletteDefaults`（已实现主题的单一颜色来源）与 `DesignTokens`（设计稿镜像目录）。
    /// 其它文件一律不得散写 `"#rrggbb"` —— 颜色必须经这两个来源流通。
    /// </summary>
    [Fact]
    public void DesignLayer_NoBareHex_OutsideTheTwoTokenSources()
    {
        var designDir = Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.Core", "Design");
        var allowed = new[] { "PaletteDefaults.cs", "DesignTokens.cs" };
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(designDir, "*.cs").OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            if (allowed.Contains(name)) continue;
            foreach (var line in File.ReadLines(file))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                if (BareHex.IsMatch(line))
                    violations.Add($"{name}: {line.Trim()}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "src/HmiEditor.Core/Design/ 发现裸 hex（颜色必须经 PaletteDefaults 或 DesignTokens 流通）：\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void NoBareHexGuard_ItselfDetectsHex()
    {
        // 自检：守卫的判据必须真能识别裸 hex，否则它是"永远绿"的摆设。
        Assert.Matches(BareHex, "var c = \"#0D0E12\";");
        Assert.DoesNotMatch(BareHex, "PaletteDefaults.Background");
        // 守卫的扫描面真实存在（至少两个允许文件里都有 hex，说明判据不是空转）。
        var designDir = Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.Core", "Design");
        var paletteHex = File.ReadLines(Path.Combine(designDir, "PaletteDefaults.cs"))
            .Count(l => BareHex.IsMatch(l));
        var tokensHex = File.ReadLines(Path.Combine(designDir, "DesignTokens.cs"))
            .Count(l => BareHex.IsMatch(l));
        Assert.True(paletteHex >= 20, $"PaletteDefaults.cs 只扫到 {paletteHex} 处 hex");
        Assert.True(tokensHex >= 10, $"DesignTokens.cs 只扫到 {tokensHex} 处 hex");
    }
}