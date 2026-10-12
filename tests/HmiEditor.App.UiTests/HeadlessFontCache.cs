using System.Collections;
using System.Reflection;
using Avalonia.Media;

namespace HmiEditor.App.UiTests;

/// <summary>
/// headless 字体表的**归一化**（阶段 294）。
///
/// <para><b>为什么会需要它</b>：阶段 58/293 已把机制钉到具体字段 ——
/// <c>FontManager.Current._fontCollections[&lt;FontCollection&gt;]._glyphTypefaceCache</c>
/// 是一张**按族名做键**的字典：headless 的桩不看族名、一律返回 <c>BareMinimum.ttf</c> 却算命中，
/// 于是**每多一个没见过族名就多一条记录**，文本绘制成本永久 +416 B/次。</para>
///
/// <para><b>它挡了什么</b>：本文件那些**绝对字节**预算判据（`CanvasRenderBaselineTests`）
/// 此前只能靠"`PerTest` 每次派发重建平台 ⇒ 顺手清掉字体表"才可比。这把预算判据
/// **绑死在隔离级别上**（切 `PerAssembly` 就系统性上抬 ⇒ 阶段 56 的越线）。
/// 显式归一化之后，"从干净字体表起量"成了**判据自己**的事，与隔离级别无关 ——
/// 这也是 <c>#47</c> 那条间歇红能去动隔离级别的前提。</para>
///
/// <para>⚠ 这是**框架内部结构**的操作：字段名一变就返回 0（此时调用方的前置判据会红，
/// 提示"重新推导"）。刻意不静默失败 —— 返回清掉的条目数供读数。</para>
/// </summary>
internal static class HeadlessFontCache
{
    /// <summary>清空按族名键的 glyph typeface 缓存；返回清掉的条目数（0 = 没找到那张表）。</summary>
    internal static int Clear()
    {
        if (Field(FontManager.Current, "_fontCollections") is not IDictionary collections) return 0;

        var removed = 0;
        foreach (DictionaryEntry e in collections)
        {
            if (e.Value == null) continue;
            if (Field(e.Value, "_glyphTypefaceCache") is not IDictionary cache) continue;
            removed += cache.Count;
            cache.Clear();
        }
        return removed;
    }

    private static object? Field(object obj, string name) =>
        obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
           ?.GetValue(obj);
}
