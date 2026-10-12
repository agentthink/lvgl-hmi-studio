using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **文案术语守卫**（阶段 169 立、阶段 170 随 `#27b` 决策收紧）。
///
/// <para><b>决策（2026-10-01）</b>：统一到「**工程**」（登记 `#27b` 的选项 A）——
/// 与 `HMI 工程包 (.hmi)` / `工程模式` / `工程设置` / 全仓文档一致，且不用动文件类型名。
/// 实施读数：zh 字典 **30 条**「项目」→「工程」（含 44 → 73 条），产品侧配对回落同步。</para>
///
/// <para>本类钉**机器能判**的两件事：</para>
/// <list type="number">
///   <item><b>弃用词不得回流</b>：zh 任一条值不得再含「项目」（白名单**双向**，今天为空）；
///     阶段 169 修复前那一句踩线（<c>confirm.discard.changes</c> =「当前<b>项目</b> … 新建<b>工程</b>？」）
///     就是这条要防的形状。</item>
///   <item><b>en 值不许是中文</b>（语言自名白名单除外）：实测只有 <c>menu.lang.zhCN = "中文"</c> 一条，
///     而那是**语言自名**（语言菜单里就该显示它自己的写法）⇒ 进白名单并**双向**钉住：
///     白名单里的键必须真的含汉字（否则白名单会变成"悄悄放行"的洞）。</item>
/// </list>
///
/// <para><b>⚠ 本类读的是字典文件、不切语言 ⇒ 不需要 <c>[Collection("Language-global")]</c></b>
/// （那条纪律针对"改进程级全局语言"的类；本类只读盘）。</para>
/// </summary>
public class TerminologyConsistencyGuardTests
{
    private static Dictionary<string, string> Zh => PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static Dictionary<string, string> En => PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    /// <summary>两个中文术语 —— 同一个概念的两套写法（`#27b` 的决策对象）。</summary>
    private const string TermA = "工程";
    private const string TermB = "项目";

    /// <summary>
    /// **判据一（阶段 170，`#27b` 决策已拍板：统一到「工程」）**：zh 值里**不许再出现被弃用的
    /// 「项目」**（白名单除外）。
    ///
    /// <para>背景：阶段 169 量到「工程」44 条 vs「项目」30 条，其中 <c>confirm.discard.changes</c>
    /// **一句话里两个词都用**。产品决策统一到「工程」（与 `HMI 工程包 (.hmi)` / `工程模式` /
    /// `工程设置` / 全仓文档一致，且不用动文件类型名）⇒ 本条把"弃用词不得回流"钉死。
    /// 白名单**双向**：名单里的键必须真的含「项目」且**不得**同时含「工程」（一句话一个名字）。</para>
    /// </summary>
    [Fact]
    public void ChineseValues_UseOnlyTheUnifiedTerm()
    {
        string[] allowList = { };   // 今天为空：全仓中文文案统一用「工程」

        var zh = Zh;
        var withRetired = zh.Where(kv => kv.Value.Contains(TermB, StringComparison.Ordinal))
                            .Select(kv => kv.Key)
                            .OrderBy(k => k, StringComparer.Ordinal)
                            .ToList();
        var unexpected = withRetired.Except(allowList, StringComparer.Ordinal).ToList();
        var staleAllow = allowList.Except(withRetired, StringComparer.Ordinal).ToList();

        Assert.True(unexpected.Count == 0,
            $"这些中文文案还在用被弃用的「{TermB}」（`#27b` 已拍板统一到「{TermA}」）："
            + string.Join(" | ", unexpected.Select(k => $"{k} = {zh[k]}")));

        Assert.True(staleAllow.Count == 0,
            $"白名单里的键已经不含「{TermB}」了 ⇒ 名单过期，该删（否则它是「悄悄放行」的洞）："
            + string.Join(", ", staleAllow));

        // 白名单里的键也不许**同时**含统一词（一句话一个名字）
        var mixed = allowList.Where(k => zh.TryGetValue(k, out var v) && v.Contains(TermA, StringComparison.Ordinal)).ToList();
        Assert.True(mixed.Count == 0,
            "白名单里的键同时含两个名字：" + string.Join(", ", mixed));
    }

    /// <summary>
    /// **判据二（反向配重）**：统一之后，「工程」必须**还在广泛使用** ——
    /// 否则上一条会退化成"弃用词没出现"的**假绿**（把文案清空也能满足）。
    /// ⚠ 刻意只钉下界（≥20），**不钉**具体数字 73 —— 那会在每次正常改文案时误伤。
    /// </summary>
    [Fact]
    public void ChineseValues_StillUseTheUnifiedTerm()
    {
        var zh = Zh;
        var total = zh.Count(kv => kv.Value.Contains(TermA, StringComparison.Ordinal));
        Assert.True(total >= 20,
            $"中文文案里含「{TermA}」的只有 {total} 条 —— 统一词失去扫描面"
            + "（要么字典读错了，要么有人把文案清空了）");
    }

    /// <summary>
    /// **判据二**：en 值里**不许有汉字**，白名单（语言自名）除外；且白名单**双向**成立 ——
    /// 名单里的键必须真的含汉字，否则名单就是在悄悄放行（`#27` 的可判那半边）。
    /// </summary>
    [Fact]
    public void EnglishValues_HaveNoChinese_ExceptLanguageSelfNames()
    {
        string[] allowList = { "menu.lang.zhCN" };   // 语言自名：语言菜单里显示它自己的写法

        var en = En;
        var withCjk = en.Where(kv => Cjk.IsMatch(kv.Value)).Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var unexpected = withCjk.Except(allowList, StringComparer.Ordinal).ToList();
        var staleAllow = allowList.Except(withCjk, StringComparer.Ordinal).ToList();

        Assert.True(unexpected.Count == 0,
            "en-US 字典里这些键的值含汉字（英文界面会显示中文）："
            + string.Join(", ", unexpected.Select(k => $"{k} = {en[k]}")));

        Assert.True(staleAllow.Count == 0,
            "白名单里的键在 en 里已经不含汉字了 ⇒ 名单过期，该删（否则它是「悄悄放行」的洞）："
            + string.Join(", ", staleAllow));

        // 前提自检：白名单里那条确实是**语言自名**（不是随便一个中文值）
        Assert.Equal("中文", en["menu.lang.zhCN"]);
        Assert.Equal("中文", Zh["menu.lang.zhCN"]);
    }
}
