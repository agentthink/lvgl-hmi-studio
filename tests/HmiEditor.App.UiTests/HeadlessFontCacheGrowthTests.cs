using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Avalonia.Media;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`416` 的产出字段定位**（`#47b` 的遗留，阶段 293）。
///
/// <para><b>此前到哪一步、缺哪一步</b>：阶段 58 已把**行为**定论并 IL 核实 —— headless 的
/// <c>FontManager</c> 由 <c>HeadlessFontManagerStub</c> 支撑，其 <c>TryCreateGlyphTypeface(familyName,…)</c>
/// **不看族名**、一律加载 <c>BareMinimum.ttf</c>，却返回 <c>true</c> ⇒ 每个"没见过"的族名都被当成
/// 命中登记进管理器，文本绘制成本**永久 +416 B/次**。但登记册一直写着
/// 「**416 由哪个字段产生（哪张 `Dictionary`/`List` 在每次绘制时被重放）未定位**」——
/// 本条就是那一步。</para>
///
/// <para><b>定位结果（本阶段实测）</b>：
/// <c>FontManager.Current._fontCollections[&lt;FontCollection&gt;]._glyphTypefaceCache</c>
/// —— 一张**按族名做键**的字典。判别实验（一个派发内）：
/// 首次解析同时登记回退族 <c>BareMinimum</c> ＋ 被请求的族（**+2**，一次性）；
/// 之后<b>每多一个没见过的族名恰好 +1 条</b>；同一个族名重复解析 3 次<b>一条都不再涨</b>。</para>
///
/// <para><b>为什么这条判据值得留着</b>：它是"预算为什么会跨用例漂"的**最后一块拼图**，
/// 也是 <c>PerTest</c>→<c>PerAssembly</c> 那个取舍的关键输入 —— 平台不重建 ⇒ 这张按族名键的
/// 缓存**跨用例累计** ⇒ 绝对字节预算随执行顺序漂。⚠ 它是**框架内部结构**探针：
/// 字段名一变就会红，那时要**重新推导**（而不是删掉本判据）。</para>
/// </summary>
public class HeadlessFontCacheGrowthTests
{
    private readonly ITestOutputHelper _o;

    public HeadlessFontCacheGrowthTests(ITestOutputHelper o) => _o = o;

    private static object? Field(object obj, string name) =>
        obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
           ?.GetValue(obj);

    /// <summary>定位 `FontManager._fontCollections[*]._glyphTypefaceCache`（不写死集合的键）。</summary>
    private static IDictionary? GlyphTypefaceCache()
    {
        if (Field(FontManager.Current, "_fontCollections") is not IDictionary collections) return null;
        foreach (DictionaryEntry e in collections)
        {
            if (e.Value != null && Field(e.Value, "_glyphTypefaceCache") is IDictionary cache) return cache;
        }
        return null;
    }

    private static void Resolve(string family)
    {
        var tf = new Typeface(new FontFamily(family), FontStyle.Normal, FontWeight.Normal);
        Assert.True(FontManager.Current.TryGetGlyphTypeface(tf, out _),
            "headless 桩必须对任意族名返回 true —— 这正是缺陷的另一半（未命中却算命中）");
    }

    [Fact]
    public void EachUnseenFamily_AddsExactlyOneEntry_ToTheGlyphTypefaceCache()
    {
        UiTestHost.Run(() =>
        {
            var tag = Guid.NewGuid().ToString("N");

            // ⚠ 定位必须**在热身之后**：全新平台里 `_fontCollections` 是空的（0 条），
            //   那张 FontCollection 与它的 `_glyphTypefaceCache` 都是**首次解析时**才建出来的。
            //   热身同时还排掉"一次性登记回退族 BareMinimum ＋ 被请求族"（+2）那一步，
            //   后面量的才是"每多一个没见过族名 +1"这件事本身。
            Resolve($"cacheprobe-{tag}-warm");

            var cache = GlyphTypefaceCache();
            Assert.True(cache != null,
                "定位失败：FontManager._fontCollections[*]._glyphTypefaceCache 不在 —— " +
                "Avalonia 内部结构变了，本条判据需**重新推导**（别直接删）");
            var c1 = cache!.Count;

            Resolve($"cacheprobe-{tag}-a");
            Assert.Equal(c1 + 1, cache.Count);                       // 新族名 ⇒ +1

            for (var i = 0; i < 3; i++) Resolve($"cacheprobe-{tag}-a");
            Assert.Equal(c1 + 1, cache.Count);                       // 同族重复解析 ⇒ 不涨

            Resolve($"cacheprobe-{tag}-b");
            Resolve($"cacheprobe-{tag}-c");
            Assert.Equal(c1 + 3, cache.Count);                       // 累计 = 不同族名个数

            JudgeReadings.Write(_o, "416 的产出字段（阶段 293）", new[]
            {
                "字段：FontManager.Current._fontCollections[<FontCollection>]._glyphTypefaceCache（键＝族名）",
                $"实测：热身 {c1} → +1 个新族 {c1 + 1}；同族重复解析 3 次仍 {c1 + 1}；再 +2 个新族 {c1 + 3}",
                "⇒ #47b 的『哪张 Dictionary 在每次绘制时被重放』＝这张按族名键的 _glyphTypefaceCache",
            });
        });
    }
}
