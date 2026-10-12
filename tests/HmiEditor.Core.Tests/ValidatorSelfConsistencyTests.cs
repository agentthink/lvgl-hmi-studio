using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 生成代码校验体系的自洽性回归。
///
/// 背景（2026-09-13 审查）：<c>GeneratedCodeValidator.KnownLvglApis</c> 是**手工维护**的白名单，
/// 已经腐化过一次 —— 漏了约 40 个生成代码实际使用的合法 API（该检查沦为噪音），
/// 还混入过 LVGL 9.x 中不存在的 v8 旧名（会造成假阴性：非法 API 被放行）。
/// 这里用「LVGL 头文件派生的对照表」把白名单钉住：
///   1) 白名单每个名字必须真实存在（对照表由 scripts/gen-lvgl-api-list.sh 生成）；
///   2) 白名单不得与 LvglApiCompatChecker 的"已移除 API"列表重叠；
///   3) 对照表本身必须足够大，避免被截断后让 ① 变成空断言。
/// </summary>
public class ValidatorSelfConsistencyTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HmiEditor.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("未找到仓库根目录（HmiEditor.slnx）");
    }

    private static readonly Lazy<HashSet<string>> LvglApiNames = new(() =>
    {
        var path = Path.Combine(FindRepoRoot(),
            "src", "HmiEditor.Core", "Resources", "lvgl-api-names.txt");
        Assert.True(File.Exists(path),
            $"缺少 LVGL API 对照表: {path}（应为 scripts/gen-lvgl-api-list.sh 的产物）");

        return File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);
    });

    [Fact]
    public void LvglApiList_IsSubstantial()
    {
        // 防止"对照表被截断/生成失败"导致下面的存在性断言变成空断言
        Assert.True(LvglApiNames.Value.Count > 1000,
            $"LVGL API 对照表只有 {LvglApiNames.Value.Count} 条，疑似被截断；请重新运行生成脚本");

        // 抽查几个"必须有"的名字（含对象式兼容别名，它是上一版抽取逻辑漏掉的类型）
        foreach (var name in new[]
                 {
                     "lv_obj_create", "lv_label_set_text", "lv_chart_set_axis_range",
                     "lv_chart_set_range", "lv_scr_load", "lv_canvas_fill_bg",
                 })
        {
            Assert.Contains(name, LvglApiNames.Value);
        }
    }

    [Fact]
    public void Whitelist_EveryEntryExistsInLvglSources()
    {
        var missing = GeneratedCodeValidator.KnownLvglApiNames
            .Where(n => !LvglApiNames.Value.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "KnownLvglApis 白名单中存在 LVGL 源文件里找不到的名字（陈旧或拼写错误，会造成假阴性）："
            + string.Join(", ", missing));
    }

    [Fact]
    public void Whitelist_And_RemovedApis_AreDisjoint()
    {
        var removedPrefixes = LvglApiCompatChecker.RemovedApiPatterns
            .Select(p => p.TrimEnd('('))
            .ToArray();

        var conflicts = GeneratedCodeValidator.KnownLvglApiNames
            .Where(name => removedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(conflicts.Count == 0,
            "同一 API 不能既在 KnownLvglApis 白名单、又在 RemovedApis 已移除列表："
            + string.Join(", ", conflicts));
    }
}
