using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.App.Services;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 配置字段受管清单（<see cref="ConfigFieldInventory"/>）的三向一致性守卫。
///
/// <para><b>三向是：</b>清单 ↔ 字段真的存在 ↔ 字段真的没被消费，外加 清单 ↔ 面板标注。
/// 任何一向失衡，这份清单就会从"事实"退化成"一份过期的抄写"，
/// 而它一旦过期，比没有更糟 —— 用户会以为没被标注的就是生效的。</para>
/// </summary>
public class ConfigFieldInventoryTests
{
    /// <summary>扫描「面板标注」调用：<c>MarkNotEffective&lt;T&gt;(nameof(T.Field), Ctrl)</c>。</summary>
    private static readonly Regex MarkCall = new(
        @"MarkNotEffective<([\w\.]+)>\s*\(\s*nameof\(([\w\.]+)\)\s*,\s*(\w+)",
        RegexOptions.Compiled);

    private static string LastSegment(string dotted) =>
        dotted[(dotted.LastIndexOf('.') + 1)..];

    /// <summary>
    /// 允许出现字段名、但**不构成"被消费"**的落点。
    ///
    /// <para><see href="AgentToolFactory.cs"/> 是 AI 工具的配置写入面：
    /// 它把 AI 传来的值写进工程配置，但没有任何执行路径读这些值回来 ——
    /// 「写入」和「消费」是两件事，把它算作消费会让这一类字段从清单里漏掉。
    /// 代价是白名单文件内部如果真的出现了读取，守卫看不出来；可控。</para>
    /// </summary>
    private static IReadOnlyList<string> Whitelist(ConfigFieldEntry entry)
    {
        var declaring = (entry.Type.DeclaringType ?? entry.Type).Name;
        return new[]
        {
            $"src/HmiEditor.App/Services/{declaring}.cs",
            "src/HmiEditor.App/Services/ConfigFieldInventory.cs",
            // AI 工具的配置写入面（只写不读）
            "src/HmiEditor.App/Services/AgentToolFactory.cs",
            // 两个面板：填值与回写都发生在这里
            "src/HmiEditor.App/Views/SettingsDialog.axaml.cs",
            "src/HmiEditor.App/Views/ProjectSettingsView.axaml.cs",
        };
    }

    // ── 守卫 1：清单里的字段必须真的存在于声明的类型上 ──

    [Fact]
    public void InventoryEntries_ExistOnDeclaredType()
    {
        var missing = new List<string>();
        foreach (var entry in ConfigFieldInventory.NotWired)
        {
            var prop = entry.Type.GetProperty(entry.Field);
            if (prop is null || !prop.GetMethod!.IsPublic)
                missing.Add(entry.Key);
        }

        Assert.True(missing.Count == 0,
            $"受管清单里的这些字段在声明类型上不存在（清单写歪了）：{string.Join(", ", missing)}");
    }

    [Fact]
    public void InventoryKeys_AreUnique()
    {
        var dupes = ConfigFieldInventory.NotWired
            .GroupBy(e => e.Key)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(dupes.Count == 0, $"清单键重复：{string.Join(", ", dupes)}");
    }

    [Fact]
    public void Inventory_IsNotEmptyEnoughToBeMeaningful()
    {
        // 扫描面自检：清单推导/登记若失效，下面那些"逐条断言"会变成空跑。
        Assert.True(ConfigFieldInventory.NotWired.Count >= 25,
            $"受管清单只有 {ConfigFieldInventory.NotWired.Count} 条，可能被清空了");
    }

    // ── 守卫 2：登记为"未生效"的字段，确实没有被白名单之外的代码读过 ──

    [Fact]
    public void NotWiredFields_AreNotConsumedOutsideWhitelist()
    {
        var sources = SourceScanner.ProductSources();
        Assert.True(sources.Count >= 300, $"只扫到 {sources.Count} 个产品源文件，扫描面可能失效");

        var offenders = new List<string>();
        var blind = new List<string>();

        foreach (var entry in ConfigFieldInventory.NotWired)
        {
            var pattern = new Regex(@"\." + Regex.Escape(entry.Field) + @"\b");
            var allow = Whitelist(entry);
            var hits = 0;
            var leak = new List<string>();

            foreach (var path in sources)
            {
                var rel = SourceScanner.Relative(path);
                var count = pattern.Matches(SourceScanner.ReadCode(path)).Count;
                if (count == 0) continue;
                hits += count;
                if (!allow.Contains(rel)) leak.Add(rel);
            }

            if (leak.Count > 0)
                offenders.Add($"{entry.Key} ← {string.Join(", ", leak.Distinct())}");

            // 自检：带控件的条目至少该被面板读/写一次；一次都没命中说明扫描器瞎了。
            if (entry.Control is not null && hits == 0)
                blind.Add(entry.Key);
        }

        Assert.True(blind.Count == 0,
            $"这些条目带控件却在产品源码里一次都没被扫到（扫描器或白名单失效）：{string.Join(", ", blind)}");

        Assert.True(offenders.Count == 0,
            "这些字段已被白名单之外的代码消费 —— 说明它们其实生效了，" +
            "请从受管清单里删掉对应条目：\n  " + string.Join("\n  ", offenders));
    }

    // ── 守卫 3：清单 ↔ 面板标注一致，且被标注的控件真的存在于 XAML ──

    [Fact]
    public void PanelMarkings_MatchInventoryAndExistInXaml()
    {
        var sources = AppSourceScanner.ScanAppSources();

        Check(ConfigFieldInventory.AppSurface, "Views/SettingsDialog.axaml.cs", "Views/SettingsDialog.axaml");
        Check(ConfigFieldInventory.ProjectSurface, "Views/ProjectSettingsView.axaml.cs", "Views/ProjectSettingsView.axaml");

        void Check(string surface, string codePath, string xamlPath)
        {
            var code = sources.Single(s => s.Relative == codePath).Text;
            var xaml = File.ReadAllText(Path.Combine(AppSourceScanner.AppRoot, xamlPath));

            var marked = MarkCall.Matches(code)
                .Select(m => (Key: $"{LastSegment(m.Groups[1].Value)}.{LastSegment(m.Groups[2].Value)}",
                              Control: m.Groups[3].Value))
                .ToList();

            Assert.True(marked.Count > 0, $"{codePath} 里一个标注调用都没扫到，扫描面可能失效");

            var expected = ConfigFieldInventory.ForSurface(surface)
                .Where(e => e.Control is not null)
                .ToDictionary(e => e.Key, e => e.Control!);
            var actual = marked.ToDictionary(m => m.Key, m => m.Control);

            Assert.True(expected.Keys.OrderBy(k => k, StringComparer.Ordinal)
                        .SequenceEqual(actual.Keys.OrderBy(k => k, StringComparer.Ordinal)),
                $"{codePath} 的标注集合与受管清单不一致。\n" +
                $"  清单要求标注：{string.Join(", ", expected.Keys.Except(actual.Keys))}\n" +
                $"  面板多标了：{string.Join(", ", actual.Keys.Except(expected.Keys))}");

            // 控件名也要对齐：清单里的名字写错（或改了 XAML 没改清单）必须红，
            // 否则"清单说标 ChkShowRulers、面板其实标了别的控件"这种错位永远看不见。
            var mismatched = expected
                .Where(kv => actual.TryGetValue(kv.Key, out var c) && c != kv.Value)
                .Select(kv => $"{kv.Key}: 清单={kv.Value} 面板={actual[kv.Key]}")
                .ToList();
            Assert.True(mismatched.Count == 0,
                $"{codePath} 的控件名与受管清单对不上：{string.Join("; ", mismatched)}");

            // 被标注的控件名必须真的在这个 XAML 里 —— 否则标注打到了空气上。
            var missing = marked
                .Where(m => !xaml.Contains($"x:Name=\"{m.Control}\""))
                .Select(m => $"{m.Control}（{m.Key}）")
                .ToList();

            Assert.True(missing.Count == 0,
                $"{xamlPath} 里没有这些被标注的控件：{string.Join(", ", missing)}");
        }
    }

    // ── 守卫 4：悬停说明必须入字典，且必须以"键"的形式送进 tooltip ──

    /// <summary>
    /// 每个条目的悬停说明都必须在两本字典里有值。
    ///
    /// <para><b>为什么需要它</b>：这句说明是<b>用户可见</b>的（灰显控件的悬停提示，
    /// 见 <c>NotYetEffective.Mark</c>），而它的键是从 <see cref="ConfigFieldEntry.Key"/>
    /// <b>推导</b>出来的 —— 加一条清单条目时，字典那边**不会**有任何编译期或运行期报错，
    /// 界面上只是安静地显示 <c>cfg.note.RuntimeConfig.MaxRuntimeTasks</c>。
    /// 索引器找不到键时会回退成键名本身，正好用它判定"字典里没有这一条"。</para>
    /// </summary>
    [Fact]
    public void EveryEntry_HasATooltipReasonInBothLanguages()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        var entries = ConfigFieldInventory.NotWired;
        // 自检：清单被清空或反射不到时，下面的循环会变成空跑恒绿
        Assert.True(entries.Count >= 25, $"清单只有 {entries.Count} 条，断言可能已经空跑");

        var missing = new List<string>();
        foreach (var entry in entries)
        {
            foreach (var lang in new[] { "zh-CN", "en-US" })
            {
                if (svc.GetString(entry.NoteKey, lang) == entry.NoteKey)
                    missing.Add($"{entry.Key} → \"{entry.NoteKey}\" 在 {lang} 里没有值");
            }
        }

        Assert.True(missing.Count == 0,
            $"{missing.Count} 条「不生效」说明没进字典（用户悬停时会看到键名）：\n    " +
            string.Join("\n    ", missing));
    }

    /// <summary>
    /// 送进 tooltip 的必须是**键**，不能是解析好的文案。
    ///
    /// <para>理由不是风格：清单是 <c>static</c> 字段，静态初始化只跑一次 ——
    /// 若在清单里就 <c>AppLocalization.Service[…]</c> 解析成字符串，
    /// 语言会被冻结在首次访问那一刻，之后切换语言悬停提示不会跟着变。</para>
    /// </summary>
    [Fact]
    public void TooltipReasons_ArePassedAsKeys()
    {
        var sources = AppSourceScanner.ScanAppSources();
        var callSites = 0;

        foreach (var codePath in new[] { "Views/SettingsDialog.axaml.cs", "Views/ProjectSettingsView.axaml.cs" })
        {
            var code = sources.Single(s => s.Relative == codePath).Text;
            foreach (Match m in Regex.Matches(code, @"NotYetEffective\.Mark\(([^;]*?)\);"))
            {
                callSites++;
                Assert.Contains("entry.NoteKey", m.Groups[1].Value, StringComparison.Ordinal);
            }
        }

        // 两个面板各一处；少了说明扫描失效，多了说明有新调用点没被这条判据覆盖
        Assert.True(callSites >= 2, $"只扫到 {callSites} 处 NotYetEffective.Mark 调用");
    }

    [Fact]
    public void EntriesWithoutControl_AreNotMarked()
    {
        // 反向：没有控件的条目（键是死的、控件不死的，例如 Theme.AccentColor）
        // 绝不能被标成"不生效" —— 那会把一个真能用的控件灰掉。
        var noControl = ConfigFieldInventory.NotWired
            .Where(e => e.Control is null)
            .Select(e => e.Key)
            .ToHashSet();

        var sources = AppSourceScanner.ScanAppSources();
        foreach (var codePath in new[] { "Views/SettingsDialog.axaml.cs", "Views/ProjectSettingsView.axaml.cs" })
        {
            var code = sources.Single(s => s.Relative == codePath).Text;
            foreach (Match m in MarkCall.Matches(code))
            {
                var key = $"{LastSegment(m.Groups[1].Value)}.{LastSegment(m.Groups[2].Value)}";
                Assert.DoesNotContain(key, noControl);
            }
        }
    }
}
