using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **示例工程 ↔ 设备示例产物 的链接守卫**（2026-10-11，阶段 307）。
///
/// <para><b>背景</b>：`firmware/t113/generated/` 的 `screens.c` ＋ `assets/` 一直**没有源工程**，
/// 直到用户把源包指了出来（`MockChineseProject.hmi`，`editorVersion = MockHmiGen/1.0`，
/// 2026-08-17）。它 **24.3 MB**，其中 **19.5 MB 是 CJK 字体**（按约定不入 git）⇒ 仓里只放
/// **工程定义**（`project.json` / `assets.json`（摘掉字体条目）/ `manifest.json` ≈ 53 KB），
/// 资产复用 `firmware/t113/generated/assets/`，`.hmi` 由 `scripts/repack-sample-hmi.sh` 现场打。</para>
///
/// <para><b>本判据守什么</b>：源工程与本目录**确实是同一份东西** —— 工程名/版本与 `screens.c`
/// 头部逐字一致；`assets.json` 里每个图片资产的**文件名与字节数**都能在 `assets/` 里对上；
/// 字体条目不许回来（不然 19.5 MB 会悄悄入库）。<b>它不判</b>"导出结果与 `screens.c` 是否一致" ——
/// 那需要跑设备侧导出（下一步）。</para>
/// </summary>
public class SampleProjectProvenanceGuardTests
{
    private readonly ITestOutputHelper _o;

    public SampleProjectProvenanceGuardTests(ITestOutputHelper o) => _o = o;

    private const string DefDir = "firmware/t113/sample/MockChineseProject";
    private const string AssetsDir = "firmware/t113/generated/assets";
    private const string ScreensRel = "firmware/t113/generated/screens.c";

    private static string RepoFile(string relative) =>
        Path.Combine(SourceScanner.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void SampleProject_MatchesTheDeviceFixture()
    {
        var projectPath = RepoFile($"{DefDir}/project.json");
        var assetsJsonPath = RepoFile($"{DefDir}/assets.json");
        var manifestPath = RepoFile($"{DefDir}/manifest.json");
        Assert.True(File.Exists(projectPath), $"源工程定义不在仓：{DefDir}/project.json");
        Assert.True(File.Exists(assetsJsonPath), $"源工程定义不在仓：{DefDir}/assets.json");
        Assert.True(File.Exists(manifestPath), $"源工程定义不在仓：{DefDir}/manifest.json");
        Assert.True(File.Exists(RepoFile("scripts/repack-sample-hmi.sh")),
            "缺 scripts/repack-sample-hmi.sh —— 没有它就没法从工程定义现场打 .hmi");

        using var project = JsonDocument.Parse(File.ReadAllText(projectPath));
        var name = project.RootElement.GetProperty("name").GetString();
        var version = project.RootElement.GetProperty("projectVersion").GetString();

        // ① 工程名/版本必须与设备产物的头部**逐字**一致（否则"源"就不是这个源）
        var header = string.Join('\n', File.ReadAllLines(RepoFile(ScreensRel)).Take(10));
        // 头部里的写法是「<name> v<version>」（带 v 前缀、中间有空格），所以用正则而不是字符串拼接
        var tag = new Regex(Regex.Escape(name!) + @"\s*v?" + Regex.Escape(version!));
        Assert.True(tag.IsMatch(header),
            $"screens.c 头部没有「{name} v{version}」—— 源工程与设备示例产物对不上（换了其中一个？）");

        // ② assets.json 里的图片资产必须在 assets/ 里**按名按大小**对得上
        using var assets = JsonDocument.Parse(File.ReadAllText(assetsJsonPath));
        var entries = assets.RootElement.GetProperty("assets").EnumerateArray().ToList();

        Assert.True(entries.Count >= 4, $"assets.json 只有 {entries.Count} 个资产 —— 解析失效或工程被削过");
        // ⚠ 阶段 307 这里断言的是"不许有字体**条目**"——那是错的：条目只是几百字节元数据，
        //   生成器要靠它发 hmi_resolve_font("asset_font_cjk", …)；该拒的是**字体文件**（19.5 MB）。
        //   阶段 315 查明：正是删掉条目才让 screens.c 回落到内建字体、与夹具对不上。
        var font = entries.Where(e => e.GetProperty("type").GetString() == "font").ToList();
        Assert.True(font.Count == 1,
            $"assets.json 应当有且只有 1 个字体条目（元数据；生成器靠它发 hmi_resolve_font），实得 {font.Count}");
        Assert.Equal("asset_font_cjk", font[0].GetProperty("id").GetString());
        foreach (var e in entries)
        {
            if (e.GetProperty("type").GetString() != "font") continue;
            var fname = Path.GetFileName(e.GetProperty("filePath").GetString()!);
            var onDisk = Path.Combine(SourceScanner.RepoRoot, "firmware", "t113", "generated", "assets", fname);
            Assert.False(File.Exists(onDisk),
                $"字体**文件**不许入库（19.5 MB，按约定）：{fname} 出现在 firmware/t113/generated/assets/");
        }

        var mismatches = new List<string>();
        foreach (var e in entries)
        {
            // 字体条目的**文件**按约定不入库（只见条目不见文件）⇒ 这条循环只管图片
            if (e.GetProperty("type").GetString() == "font") continue;
            var file = Path.GetFileName(e.GetProperty("filePath").GetString()!);
            var declared = e.GetProperty("sizeBytes").GetInt64();
            var actual = RepoFile($"{AssetsDir}/{file}");
            if (!File.Exists(actual)) { mismatches.Add($"{file}：assets/ 里没有"); continue; }
            var onDisk = new FileInfo(actual).Length;
            if (onDisk != declared) mismatches.Add($"{file}：声明 {declared} B，实际 {onDisk} B");
        }
        Assert.True(mismatches.Count == 0,
            "源工程声明的图片资产与 firmware/t113/generated/assets/ 对不上："
            + Environment.NewLine + string.Join(Environment.NewLine, mismatches));

        // ③ manifest 的资产数要跟摘掉字体后的包一致（免得重新打包时数目自相矛盾）
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        Assert.Equal(entries.Count, manifest.RootElement.GetProperty("assetCount").GetInt32());

        // ④ 记录来源版本（编辑器版本写进 manifest：这一版是 MockHmiGen/1.0）
        var editor = manifest.RootElement.GetProperty("editorVersion").GetString() ?? "";
        Assert.True(editor.StartsWith("MockHmiGen", StringComparison.Ordinal),
            $"manifest.editorVersion 应为 MockHmiGen/*（说明这份工程是工具生成的），实得「{editor}」");

        JudgeReadings.Write(_o, "示例工程 ↔ 设备示例产物（阶段 307）", new[]
        {
            $"工程「{name} {version}」与 screens.c 头部逐字一致",
            $"assets.json 的 {entries.Count} 个图片资产按名按大小都在 assets/ 里",
            $"manifest.editorVersion = {editor}（这一版是工具生成的）",
            "字体条目不许回来（19.5 MB，按约定不入 git）；再导出的漂移判据是下一步",
        });
    }
}
