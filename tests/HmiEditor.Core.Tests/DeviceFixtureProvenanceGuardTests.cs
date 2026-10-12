using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **设备示例产物的"来源与清单"守卫**（2026-10-11，阶段 306）。
///
/// <para><b>为什么需要它</b>：<c>firmware/t113/generated/</c> 里 <c>tags.*</c> 已被
/// <see cref="DeviceTagsContractDriftTests"/> 与生成器契约绑住，但 <c>screens.c</c> 与
/// <c>assets/</c> 的**源工程不在本仓**（<c>scripts/verify.sh</c> 生成的黄金工程不是它 ——
/// 图片名都对不上）⇒ 这个目录**不可再导出**。今天没有任何东西阻止有人：
/// 悄悄换掉 <c>screens.c</c>、塞进/删掉资产、或者误以为"重跑 MockHmiGen 就能重生成"。</para>
///
/// <para><b>四道</b>：① 目录清单与 <c>PROVENANCE.md</c> 里那块机器可读清单**逐项一致**（增删即红）；
/// ② <c>screens.c</c> 头部必须带"内置样例"标记（不许被当产品工程）；
/// ③ <c>PROVENANCE.md</c> 必须点名哪两个部分**源工程不在仓**、并记录"重复资产"这个实际读数；
/// ④ 面自检（清单非空且含 <c>tags.h</c>/<c>tags.c</c>，防解析失效后退化成恒绿）。</para>
///
/// <para><b>它不做什么</b>：不检查产物的**内容**是否正确（那是上表里各条判据的事），
/// 也不代替"提供原工程或拍板改为构建期生成"这个决定。</para>
/// </summary>
public class DeviceFixtureProvenanceGuardTests
{
    private readonly ITestOutputHelper _o;

    public DeviceFixtureProvenanceGuardTests(ITestOutputHelper o) => _o = o;

    private const string Dir = "firmware/t113/generated";
    private const string ProvenanceRel = Dir + "/PROVENANCE.md";
    private const string ScreensRel = Dir + "/screens.c";

    private static string RepoFile(string relative) =>
        Path.Combine(SourceScanner.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string ReadOrFail(string relative)
    {
        var path = RepoFile(relative);
        Assert.True(File.Exists(path), $"文件不存在：{relative}");
        return File.ReadAllText(path);
    }

    /// <summary>PROVENANCE.md 里两个标记之间的清单（相对路径，已排序）。</summary>
    private static List<string> DeclaredInventory(string provenance)
    {
        var m = Regex.Match(provenance,
            @"<!-- INVENTORY:BEGIN -->(?<body>.*?)<!-- INVENTORY:END -->",
            RegexOptions.Singleline);
        if (!m.Success) return new List<string>();
        return m.Groups["body"].Value
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> ActualInventory()
    {
        var root = RepoFile(Dir);
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => !SourceScanner.IsBuildOutput(f))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void DeclaredInventory_MatchesTheDirectory_AndProvenanceStatesTheFacts()
    {
        var provenance = ReadOrFail(ProvenanceRel);
        var declared = DeclaredInventory(provenance);
        var actual = ActualInventory();

        // ⚠ 阶段 319（③）：screens.c 是**生成物**、**不入库**（.gitignore + git rm --cached）。
        //   它缺席是**正常**的（裸跑 dotnet test 而没跑生成）⇒ 不假装查过：把这条读数写成"未查"，
        //   同时机械钉住"它确实没被追踪"（否则有人会把它 git add 回来，③ 就白做了）。
        var generated = "screens.c";
        var generatedPresent = actual.Contains(generated);
        if (!generatedPresent)
        {
            declared.Remove(generated);   // 没生成 ⇒ 不参与清单比对（缺席不是漂移）
        }
        var gitignore = File.ReadAllText(RepoFile(".gitignore"));
        Assert.True(gitignore.Contains("firmware/t113/generated/screens.c", StringComparison.Ordinal),
            "screens.c 必须留在 .gitignore 里（③：开发机生成、构建机只消费）—— 它被重新入库就等于 ③ 没做");

        // ④ 面自检：解析出来的清单必须像回事（否则下面的"逐项一致"可能两边都是空的）
        Assert.True(declared.Count >= 4 && declared.Contains("tags.h") && declared.Contains("tags.c"),
            $"PROVENANCE.md 的清单没解析出来（declared={declared.Count}）—— 标记块被动了？");
        Assert.True(actual.Count >= 4, $"目录里只扫到 {actual.Count} 个文件 —— 扫描面失效？");

        // ① 清单逐项一致
        var missing = declared.Except(actual).ToList();   // 写了但文件不在
        var extra = actual.Except(declared).ToList();     // 文件在但没写
        Assert.True(missing.Count == 0 && extra.Count == 0,
            "firmware/t113/generated 的清单与 PROVENANCE.md 不一致（增删文件必须同步改那两块标记之间的清单）："
            + Environment.NewLine + $"  声明了但不存在：{string.Join(", ", missing)}"
            + Environment.NewLine + $"  存在但没声明：{string.Join(", ", extra)}");

        // ② screens.c 必须标明是内置样例 —— 但它是**生成物**（③不入库）：缺席时这条**未查**
        //    （不假装查过，也不当作漂移）。真兜底是 gen-device-tree.sh --check（缺席即 rc=1），
        //    门禁第 7 步会先跑它。
        if (generatedPresent)
        {
            var screens = ReadOrFail(ScreensRel);
            var head = string.Join('\n', screens.Split('\n').Take(10));
            // 阶段 318 起这条标注**来自工程 description**（生成器发 // Description:），
            // 不再是手工改产物 —— 所以它经得起重生成。
            Assert.True(head.Contains("非产品工程", StringComparison.Ordinal),
                "screens.c 头部丢了「非产品工程」标注 —— 它会被当成真实产品产物。"
                + "该标注应由示例工程的 description 提供（生成器发 // Description:）。");
        }

        // ③ 必须写清"源工程在哪、字体为什么不入库"＋记录重复资产这个实际读数
        //    （阶段 307 起源工程**已入仓**；此前这条断言的是"源工程不在本仓"，随事实改了）
        Assert.True(provenance.Contains("firmware/t113/sample/MockChineseProject", StringComparison.Ordinal),
            "PROVENANCE.md 必须点名源工程的位置（firmware/t113/sample/MockChineseProject）");
        Assert.True(provenance.Contains("scripts/repack-sample-hmi.sh", StringComparison.Ordinal),
            "PROVENANCE.md 必须写明怎么现场打 .hmi（scripts/repack-sample-hmi.sh）");
        Assert.True(provenance.Contains("不入 git", StringComparison.Ordinal),
            "PROVENANCE.md 必须说明**字体按约定不入 git**（否则下次有人会把 19.5 MB 字体塞进来）");
        Assert.Contains("screens.c", provenance, StringComparison.Ordinal);
        Assert.Contains("assets/asset_banner.bmp", provenance, StringComparison.Ordinal);
        Assert.True(provenance.Contains("逐字节相同", StringComparison.Ordinal),
            "PROVENANCE.md 必须记录「asset_bg 与 asset_center 逐字节相同」这个实际读数（2.34 MB 纯重复）");

        JudgeReadings.Write(_o, "设备示例产物的来源与清单（阶段 306）", new[]
        {
            $"清单一致：声明 {declared.Count} 项 ＝ 实际 {actual.Count} 项",
            generatedPresent
                ? "screens.c 在场（生成过）：其清单一致性**已查**"
                : "⚠ screens.c 不在场（未跑生成）：本条对 screens.c **未查** —— 跑 bash scripts/gen-device-tree.sh --write 后再跑门禁",
            "screens.c 头部带「内置示例工程（非产品工程）」标记",
            "PROVENANCE.md 写明源工程位置（sample/MockChineseProject）＋ 重打包脚本 ＋ 字体不入 git ＋ 重复资产读数",
            "本守卫不判产物内容对错（那是 DeviceTagsContractDriftTests / IncludeClosure / ABI 那几条的事）",
        });
    }
}
