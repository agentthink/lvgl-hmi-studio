using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HmiEditor.App.Resources;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **Lang 常量 ↔ 字典键 的闭合检查**：<see cref="Lang"/> 里的每个键都必须在
/// <c>Strings.zh-CN.json</c> / <c>Strings.en-US.json</c> 里有值。
///
/// <para><b>为什么需要它</b>：<see cref="LocalizationService"/> 找不到键时**回退成键名本身**
/// （<c>svc["cmd.desc.align.left"]</c> 原样返回 <c>"cmd.desc.align.left"</c>），
/// 于是"忘了往字典里加一条"不会抛异常、也不会编译失败 —— 界面上只是安静地显示一串
/// 键名。既有测试只保证了两本字典**彼此**键集一致（<c>LocalizationTests</c> 的
/// parity 断言），但**没有**保证 App 侧声明的常量真的落在字典里：
/// 两本字典一起漏掉同一个键时，parity 依然是绿的。</para>
///
/// <para><b>为什么用反射枚举常量</b>：手写维护一份键清单就是又一份会漂移的抄本 ——
/// 新增常量时恰好会忘了同步它，而这正是要防的错。反射拿到的是**声明本身**。</para>
/// </summary>
public class LangKeyCoverageTests
{
    private static List<(string Field, string Key)> LangConstants() =>
        typeof(Lang)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (f.Name, (string)f.GetRawConstantValue()!))
            .ToList();

    [Fact]
    public void LangScanner_ActuallySeesTheConstants()
    {
        // 自检：反射若拿不到常量（字段改名/改类型），下面的断言会变成空跑恒绿
        var constants = LangConstants();
        Assert.True(constants.Count > 1000,
            $"只反射到 {constants.Count} 个 Lang 常量 —— 断言可能已经空跑");
    }

    [Fact]
    public void EveryLangConstant_ResolvesInBothLanguages()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        var missing = new List<string>();
        foreach (var (field, key) in LangConstants())
        {
            foreach (var lang in new[] { "zh-CN", "en-US" })
            {
                // 回退语义：找不到就返回键名自身 —— 用它判定"字典里没有这一条"
                if (svc.GetString(key, lang) == key)
                    missing.Add($"Lang.{field} → \"{key}\" 在 {lang} 字典里没有值（界面会直接显示键名）");
            }
        }

        Assert.True(missing.Count == 0,
            $"{missing.Count} 个 Lang 常量在字典里缺失：\n    " + string.Join("\n    ", missing));
    }

    [Fact]
    public void LangConstants_AreUnique()
    {
        // 同一个字符串键被两个常量声明时，改一处会"意外"改到另一处文案
        var duplicates = LangConstants()
            .GroupBy(c => c.Key, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ← {string.Join(" / ", g.Select(c => c.Field))}")
            .ToList();

        Assert.True(duplicates.Count == 0,
            "Lang 里有重复的键值：\n    " + string.Join("\n    ", duplicates));
    }
}
