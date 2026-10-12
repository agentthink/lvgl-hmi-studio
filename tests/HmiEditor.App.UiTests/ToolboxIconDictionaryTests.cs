using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **工具箱图标 ↔ 图标字典的单一来源判据**（2026-09-22，阶段 72）。
///
/// <para><b>为什么需要它：</b>同一个控件类型的图标在两个地方各画一份 ——
/// 「主窗口绘图工具栏 / 快捷按钮」走 <c>Assets/Icons.axaml</c> 的资源键，
/// 而「工具箱」走 <see cref="ToolboxView.ControlIcon"/> 里的一张 **C# switch 表**
/// （几何是手写的字符串字面量，再 <c>StreamGeometry.Parse</c>）。两者分属两套命名空间、
/// 两套扫描面 ⇒ **没有任何判据能看见它们的关系**。</para>
///
/// <para><b>实测（阶段 72 首跑）：</b>26 个控件类型里 <b>19 个</b>与字典里的键同语义
/// （<c>IconQuick&lt;T&gt;</c> 14 个 / <c>IconDraw&lt;T&gt;</c> 5 个），其中 <b>15 个逐字相同</b>、
/// <b>4 个已经分叉</b>：<c>Rectangle</c> / <c>Ellipse</c> / <c>Polyline</c> / <c>Arc</c>
/// （<c>IconDraw</c> 那 5 个里只有 <c>IconDrawLine</c> 没分叉）。
/// 用户在**同一个界面**里能同时看到两者（主窗口绘图工具栏 + 工具箱「基础绘图图形」分类）
/// ⇒ 同一个「矩形」，两处画得不一样。</para>
///
/// <para>⚠ <b>两个口径别混</b>：首轮探针是按「几何串在字典里**存不存在**」判的，得
/// <b>16 / 4 / 6</b>（逐字相同 / 已分叉 / 只在工具箱）—— 其中 <c>Ellipse</c> 的圆恰好逐字等于
/// <c>IconQuickLed</c>，被那个口径**误收进「逐字相同」**（几何全等但语义不同）。
/// 本判据按**镜像键**判，得 <b>19 / 7</b>。两个都对，问的是不同的问题；
/// 本文件里所有数字都是**镜像键口径**（`16 + 4 ≠ 19` 不是笔误）。</para>
///
/// <para><b>为什么这个形状比阶段 71 那个更危险：</b>阶段 71 的 21 条是「同一份几何两处存放、
/// 逐字相同」——它只会在**下一次**改动时才分叉；这里的 4 条**当时就已经分叉了**，
/// 是当下可见的视觉不一致，而且静默（编译期零警告、运行期不抛）。</para>
///
/// <para><b>机制（不写「类型 → 键」的硬表）：</b>镜像键按**命名约定**推导 ——
/// <c>IconDraw&lt;枚举名&gt;</c> 或 <c>IconQuick&lt;枚举名&gt;</c>。
/// 表会过期，约定不会。代价是这条判据只在**名字对得上**时成立 ⇒
/// 「字典里没有镜像键」的 7 个类型走 <see cref="NoDictionaryMirror"/> 差异表，
/// 且**双向**：有人补了镜像键却不接工具箱 ⇒ 红（逼他做选择，见判据二的失败消息）。</para>
///
/// <para><b>两端都取运行期，不做全仓源扫描：</b>工具箱侧**真的调用**
/// <see cref="ToolboxView.ControlIcon"/>（它被改成 <c>internal</c> 只为这个，
/// <c>HmiEditor.App.csproj</c> 早已 <c>InternalsVisibleTo</c> 本程序集）；
/// 「这个键在运行期真的能解析成一个 <see cref="Geometry"/>」也**真的**走
/// <c>Application.Current.Resources.TryGetResource</c>。</para>
///
/// <para>⚠ 只有**字典侧几何的文本**取自 <c>Assets/Icons.axaml</c> 的源文件
/// （见 <see cref="DictionaryGeometryTexts"/>）—— 这不是偷懒，是因为
/// Avalonia **没有**公开「几何 → 路径文本」的 API。两个维度：
/// <list type="number">
///   <item><b>文本维</b>：<c>ControlIcon(type)</c> 的返回值 vs 字典里**写的**那个字符串，
///     <c>Ordinal</c> 逐字比较。缺陷的定义本来就是「两处**写法**不一致」，这是主判据。</item>
///   <item><b>运行期维</b>：<c>StreamGeometry.Parse(ControlIcon(type)).Bounds</c> vs
///     资源对象自己的 <c>Bounds</c>（对象来自 <c>TryGetResource</c> 的 <c>out</c>）。
///     只到 <c>Bounds</c>、**不是**内容相等 —— 它证明的是「运行期真加载了那份几何」，
///     不是「两份几何逐点相同」。</item>
/// </list></para>
///
/// <para><b>两个已经踩过的坑（都留在这里，别再踩）：</b>
/// <list type="number">
///   <item>🩸 <c>resources[key]</c> **索引器不查 <c>MergedDictionaries</c>**，而
///     <c>Icons.axaml</c> 正是靠 <c>ResourceInclude</c> 合并进来的 ⇒ 索引器对合并来的键
///     返回 <c>null</c>。而 <c>TryGetResource</c> 又能查到（它会查合并字典）
///     ⇒ 「查到了却取出 null」，对 null 取成员直接 <c>NullReferenceException</c>。
///     首跑 3 条判据一起红、栈都指向同一行。**只认 <c>out</c> 值。**</item>
///   <item>🩸 <c>StreamGeometry.ToString()</c> 返回的是**类型名**
///     <c>Avalonia.Media.StreamGeometry</c>，**不是**路径数据。首版拿它当「几何文本」比较
///     ⇒ 那一维是**恒真的假绿**（读数里 19 行最后一列全是同一个类型名才暴露出来）。
///     同义陷阱：<c>Path.Data</c> 的「引用相等」之所以是阶段 71 那条判据唯一可用的硬判据，
///     就是因为**内容**在这里根本比不了。</item>
/// </list></para>
/// </summary>
public class ToolboxIconDictionaryTests
{
    private readonly ITestOutputHelper _output;

    public ToolboxIconDictionaryTests(ITestOutputHelper output) => _output = output;

    /// <summary>镜像键的命名前缀（按此顺序探测，实测两组无交集）。</summary>
    private static readonly string[] MirrorPrefixes = ["IconDraw", "IconQuick"];

    /// <summary>从 <c>Icons.axaml</c> 里逐字抽 <c>&lt;StreamGeometry x:Key="K"&gt;几何&lt;/StreamGeometry&gt;</c>。</summary>
    private static readonly Regex KeyedGeometry = new(
        @"<StreamGeometry\s+x:Key=""(?<key>[^""]+)""\s*>(?<geo>[^<]*)</StreamGeometry>",
        RegexOptions.Compiled);

    /// <summary>
    /// 字典里**没有**镜像键的控件类型。每条必须写明**为什么没有** ——
    /// 这不是豁免表，是「按命名约定推导」这条机制**看不见**的那部分。
    /// </summary>
    private static readonly Dictionary<HmiControlType, string> NoDictionaryMirror = new()
    {
        [HmiControlType.Screen] =
            "工具箱用它表示「屏幕容器」这个可拖入的对象；字典里的 IconNewScreen 是「新建屏幕」这个**动作**，语义不同、刻意不共用字形",
        [HmiControlType.Spinner] = "只在工具箱出现的控件（主窗口既无快捷按钮、也无同语义图标键）",
        [HmiControlType.TabView] = "同上",
        [HmiControlType.Svg] = "同上",
        [HmiControlType.Tank] = "过程图形控件，只在工具箱出现",
        [HmiControlType.Pipe] = "同上",
        [HmiControlType.Valve] = "同上",
    };

    /// <summary>
    /// 字典里**属于镜像族**（<c>IconDraw*</c> / <c>IconQuick*</c>）、但名字后半段
    /// **不是** <see cref="HmiControlType"/> 成员的键 —— 这是**反方向**的那一半。
    ///
    /// <para>为什么要单独守：另外三条判据全都走「**枚举 → 键**」这一个方向，
    /// 字典侧多出来的镜像族键**没有任何判据看得见**。它有两种命运：
    /// ① 本来就是绘图工具（<c>Select</c>/<c>Text</c>）—— 合法；
    /// ② 曾经有对应控件、后来枚举成员被改名或删掉了，键留了下来 ——
    /// 那是「字典里养着一个没人用的字形」，而 <c>IconDrawFoo</c> 这种名字会让下一个人
    /// **以为存在**对应的控件类型。阶段 71 的 28.7 记过同类的单向漏洞
    /// （`IconResetTransform` / `IconReloadPanels` 共用一份几何，也是「定义侧」问题）。</para>
    /// </summary>
    private static readonly Dictionary<string, string> MirrorKeyWithoutControlType = new(StringComparer.Ordinal)
    {
        ["IconDrawSelect"] = "选择工具：主窗口绘图工具栏的指针工具，不是可拖入的画布控件 ⇒ 没有 HmiControlType",
        ["IconDrawText"] = "文本工具：同上（在画布上放文本用；Label **控件**走 IconQuickLabel）",
    };

    /// <summary>一次探测：某个控件类型的工具箱几何文本、字典几何文本、以及两维比较的结果。</summary>
    private sealed record Probe(
        HmiControlType Type, string Key,
        string ToolboxText, string DictionaryText,
        Rect ToolboxBounds, Rect DictionaryBounds,
        bool SameText, bool SameBounds);

    private sealed record Reading(
        List<Probe> WithMirror,
        List<HmiControlType> WithoutMirror,
        List<string> Ambiguous,
        List<string> TextMissing,
        int DictionaryGeometryCount);

    /// <summary>
    /// 字典源文件里**写的**几何文本。见类注释：Avalonia 没有公开的「几何 → 路径文本」API，
    /// 所以字典侧的文本只能从源文件读；`TryGetResource` 那条腿负责证明
    /// 「这个键在运行期真的有值、且是个 Geometry」（见 <see cref="Read"/> 的运行期维）。
    /// </summary>
    private static Dictionary<string, string> DictionaryGeometryTexts()
    {
        var path = Path.Combine(AppSourceScanner.AppRoot, "Assets", "Icons.axaml");
        var axaml = File.ReadAllText(path);
        return KeyedGeometry.Matches(axaml).ToDictionary(
            m => m.Groups["key"].Value,
            m => m.Groups["geo"].Value.Trim(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// 在 headless UI 线程上**一次派发**把整批类型探完（逐类型派发没必要）。
    /// </summary>
    private static Reading Read() => UiTestHost.Run(() =>
    {
        var resources = Application.Current!.Resources;
        var texts = DictionaryGeometryTexts();
        var withMirror = new List<Probe>();
        var without = new List<HmiControlType>();
        var ambiguous = new List<string>();
        var textMissing = new List<string>();

        foreach (var type in Enum.GetValues<HmiControlType>())
        {
            var name = type.ToString();

            // ⚠ 只认 TryGetResource 的 out 值，**不要回头用 `resources[key]` 索引器**：
            //   索引器不查 MergedDictionaries / 父级，而 Icons.axaml 正是合并进来的
            //   ⇒ 索引器对合并来的键返回 null，后面对它取成员直接 NRE。
            //   本判据首跑就栽在这（3 条一起红，栈都指向这一行）。
            var hits = new List<(string Key, Geometry Geometry)>();
            foreach (var prefix in MirrorPrefixes)
            {
                if (resources.TryGetResource(prefix + name, null, out var v) && v is Geometry g)
                    hits.Add((prefix + name, g));
            }

            if (hits.Count > 1)
                ambiguous.Add($"{type} → {string.Join(" 与 ", hits.Select(h => h.Key))}");
            if (hits.Count == 0) { without.Add(type); continue; }

            var (key, live) = hits[0];
            var fromToolbox = ToolboxView.ControlIcon(type);

            if (!texts.TryGetValue(key, out var fromDict))
            {
                textMissing.Add($"{type} → {key}");
                continue;
            }

            withMirror.Add(new Probe(
                type, key,
                fromToolbox, fromDict,
                StreamGeometry.Parse(fromToolbox).Bounds, live.Bounds,
                string.Equals(fromToolbox, fromDict, StringComparison.Ordinal),
                StreamGeometry.Parse(fromToolbox).Bounds == live.Bounds));
        }

        return new Reading(withMirror, without, ambiguous, textMissing, texts.Count);
    });

    /// <summary>
    /// 读数出口（<c>HMI_READINGS_OUT</c>，见 <see cref="JudgeReadings"/>）——
    /// 阶段 72 之前这一族在**绿**的时候什么都看不见：19 个镜像键逐个绿，
    /// 但「哪 19 个、各自对准了哪个键、两维比较各是什么」只在红的时候才打印。
    /// 按 <see cref="JudgeReadings"/> 的原文：读数不做成断言，它是**证词**。
    /// </summary>
    private static List<string> MirrorLines(Reading r) =>
        r.WithMirror
         .Select(p => $"  {p.Type,-11} {p.Key,-22} text={(p.SameText ? "same" : "DIFF")}  "
                    + $"bounds={(p.SameBounds ? "same" : "DIFF")}  {p.ToolboxText}")
         .ToList();

    [Fact]
    public void EveryToolboxIcon_MatchesItsDictionaryIcon()
    {
        var r = Read();

        JudgeReadings.Write(_output, "工具箱图标/字典镜像对照",
            new[] { $"types={r.WithMirror.Count + r.WithoutMirror.Count}  withMirror={r.WithMirror.Count}"
                  + $"  withoutMirror={r.WithoutMirror.Count}  ambiguous={r.Ambiguous.Count}"
                  + $"  字典几何条目={r.DictionaryGeometryCount}  textMissing={r.TextMissing.Count}" }
            .Concat(MirrorLines(r)).ToList());

        // ── 面自检：推导挂掉时「没有不一致」是恒真的 ──
        // 顺序＝「更上游的输入面」在前。反例（阶段 72 负控 case 7）：抽取正则写坏 ⇒
        // 19 个类型全部掉进 textMissing、withMirror 变空 —— 两条都会命，但先命中的
        // 必须是最上游那条，否则下游断言永远见不到光（那就成了一条死断言）。
        Assert.True(r.DictionaryGeometryCount >= 60,
            $"从 Assets/Icons.axaml 只抽到 {r.DictionaryGeometryCount} 条几何（实测应 65）—— "
            + "抽取正则或源文件路径可能失效（那会让「两边一致」变成恒真）。");

        Assert.True(r.TextMissing.Count == 0,
            "这些键在**运行期**能解析成 Geometry，但在 Icons.axaml 的源文本里抽不到几何 —— "
            + "说明「源文本」这条腿与「运行期」那条腿已经不同源了，比它守的缺陷本身更严重：\n    "
            + string.Join("\n    ", r.TextMissing));

        Assert.True(r.WithMirror.Count >= 19,
            $"只有 {r.WithMirror.Count} 个控件类型在图标字典里找到镜像键（应 ≥19）—— "
            + "命名约定或资源解析可能失效。有镜像的：" + string.Join("、", r.WithMirror.Select(p => p.Type)));

        var bad = r.WithMirror
            .Where(p => !(p.SameText && p.SameBounds))
            .Select(p => $"{p.Type} ↔ {p.Key}"
                      + $"\n        工具箱: {p.ToolboxText}   Bounds {p.ToolboxBounds}"
                      + $"\n        字典　: {p.DictionaryText}   Bounds {p.DictionaryBounds}")
            .ToList();

        Assert.True(bad.Count == 0,
            $"{bad.Count} 个工具箱图标与它的字典镜像键**几何不一致** —— 同一个控件"
            + "在主窗口与工具箱里画得不一样（编译期零警告、运行期不抛，只是长得不同）：\n    "
            + string.Join("\n    ", bad));
    }

    [Fact]
    public void ToolboxTypesWithoutDictionaryMirror_AreStillTheKnownSet()
    {
        var r = Read();

        var undocumented = r.WithoutMirror.Select(t => t.ToString()).ToList();

        JudgeReadings.Write(_output, "工具箱图标/无字典镜像的类型",
            new[] { $"实测 {undocumented.Count} 个 / 差异表 {NoDictionaryMirror.Count} 条" }
            .Concat(r.WithoutMirror.Select(t =>
                $"  {t,-11} {NoDictionaryMirror.GetValueOrDefault(t, "!! 不在差异表里")}"))
            .ToList());

        Assert.True(undocumented.Count >= 6,
            $"「没有镜像键」的类型只剩 {undocumented.Count} 个（实测应有 7 个）—— "
            + "要么命名约定变了，要么探测面失效了。");

        Assert.True(undocumented.Count == NoDictionaryMirror.Count,
            $"差异表 {NoDictionaryMirror.Count} 条，实测 {undocumented.Count} 个类型没有镜像键。"
            + "两种可能，别混：① **新加了 `HmiControlType`**（那是新控件 ⇒ 要给图标字典加 "
            + "`IconQuick<T>`/`IconDraw<T>` **并**接进工具箱）；② 命名约定变了。"
            + "**别直接往差异表里塞。**");

        var stale = NoDictionaryMirror.Keys
            .Where(t => !r.WithoutMirror.Contains(t))
            .Select(t => $"{t}   （{NoDictionaryMirror[t]}）")
            .ToList();

        Assert.True(stale.Count == 0,
            "差异表里这些类型**在图标字典里已经有镜像键了** —— 说明有人给字典补了键，"
            + "却没让工具箱用它（那就是「同一个控件两套图标」的翻版）。"
            + "两条路选一条：把差异表这行**删掉并让几何对齐**（判据一会接着盯），"
            + "或者把那个键**改名/删掉**：\n    " + string.Join("\n    ", stale));
    }

    /// <summary>
    /// 推导必须是**无歧义**的：某个类型若同时存在 <c>IconDraw&lt;T&gt;</c> 与
    /// <c>IconQuick&lt;T&gt;</c>，<see cref="Read"/> 会静默取前者、后者永远不被检查。
    /// </summary>
    [Fact]
    public void MirrorKeyDerivation_IsUnambiguous()
    {
        var r = Read();

        JudgeReadings.Write(_output, "工具箱图标/镜像键推导歧义",
            new[] { $"ambiguous={r.Ambiguous.Count}（0 = 每个控件类型至多命中一个镜像键）" });

        Assert.True(r.Ambiguous.Count == 0,
            "这些控件类型在图标字典里**同时**有 IconDraw<T> 与 IconQuick<T> 两个镜像键 —— "
            + "推导会静默取前者、后者不受检查。要么删掉多余的那个，要么把推导规则写明确：\n    "
            + string.Join("\n    ", r.Ambiguous));
    }

    /// <summary>键命中的镜像前缀；不属于镜像族则 <c>null</c>。</summary>
    private static string? MirrorPrefixOf(string key) =>
        MirrorPrefixes.FirstOrDefault(p => key.StartsWith(p, StringComparison.Ordinal));

    /// <summary>
    /// **反方向闭合**：字典里属于镜像族、却没有对应 <see cref="HmiControlType"/> 成员的键。
    /// 与判据二（枚举有、字典没有）方向相反 —— 两条合起来才把「枚举 ↔ 字典」这个面封住。
    /// </summary>
    [Fact]
    public void DictionaryMirrorKeys_WithoutAControlType_AreStillTheKnownSet()
    {
        var all = DictionaryGeometryTexts().Keys;
        var types = Enum.GetValues<HmiControlType>().Select(t => t.ToString())
                        .ToHashSet(StringComparer.Ordinal);

        var mirrorKeys = all.Where(k => MirrorPrefixOf(k) is not null).ToList();
        var orphans = mirrorKeys
            .Where(k => !types.Contains(k[MirrorPrefixOf(k)!.Length..]))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        JudgeReadings.Write(_output, "工具箱图标/镜像族键的反向闭合",
            new[] { $"字典里镜像族键 {mirrorKeys.Count} 个（IconDraw 7 + IconQuick 14 = 21）"
                  + $" / HmiControlType 成员 {types.Count} 个 ⇒ 无对应类型的键 {orphans.Count} 个"
                  + $"（差异表 {MirrorKeyWithoutControlType.Count} 条）" }
            .Concat(orphans.Select(k =>
                $"  {k,-18} {MirrorKeyWithoutControlType.GetValueOrDefault(k, "!! 不在差异表里")}"))
            .ToList());

        Assert.True(mirrorKeys.Count >= 21,
            $"字典里只找到 {mirrorKeys.Count} 个镜像族键（实测应 21）—— "
            + "命名约定或抽取面失效了（那会让「没有孤儿」变成恒真）。");

        var undocumented = orphans.Where(k => !MirrorKeyWithoutControlType.ContainsKey(k)).ToList();
        Assert.True(undocumented.Count == 0,
            "字典里这些镜像族键的名字后半段**不是** HmiControlType 成员。两条路，别混："
            + "① 它本来就是绘图工具/非控件图标 ⇒ **写明理由**加进差异表；"
            + "② 曾经有对应控件、枚举成员被改名或删掉了 ⇒ 那个键是残留，"
            + "而 `IconDrawFoo` 这种名字会让下一个人以为存在对应控件：\n    "
            + string.Join("\n    ", undocumented));

        var stale = MirrorKeyWithoutControlType.Keys
            .Where(k => !orphans.Contains(k, StringComparer.Ordinal))
            .ToList();
        Assert.True(stale.Count == 0,
            "差异表里这些键**又对上 HmiControlType 成员了** —— 说明枚举补了成员"
            + "（那就该走「给工具箱接图标」那条路，判据一/二接着盯），或者键被改名了。"
            + "两种都不该留着这行：\n    " + string.Join("\n    ", stale));
    }
}
