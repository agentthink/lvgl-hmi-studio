using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using HmiEditor.App;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 行为测试：<see cref="EditorCommandCatalog"/> —— 快捷键表与命令映射。
///
/// <para>这些断言在原实现（163 行 <c>switch</c> + 78 行右键菜单 <c>switch</c>）下**做不到**：
/// 表写重了只会静默命中第一个分支，两条路径覆盖的操作集也无法比对。</para>
/// </summary>
public class EditorCommandCatalogTests
{
    [Fact]
    public void Bindings_HaveNoConflicts()
    {
        var conflicts = EditorCommandCatalog.FindConflicts();
        Assert.True(conflicts.Count == 0,
            "快捷键冲突：\n" + string.Join("\n", conflicts));
    }

    [Fact]
    public void EveryBinding_ResolvesToItsOwnCommand()
    {
        foreach (var binding in EditorCommandCatalog.Bindings)
        {
            var resolved = EditorCommandCatalog.Resolve(binding.Key, binding.Modifiers);
            Assert.NotNull(resolved);
            Assert.Equal(binding.Command, resolved!.Command);
        }
    }

    [Fact]
    public void Resolve_IsStrictAboutModifiers()
    {
        // Ctrl+Shift+V 必须解析为"垂直等距排列"—— 原 switch 里 Paste 分支
        // (case Key.V when ctrl && !alt) 排在前面，导致该快捷键实际执行粘贴。
        var resolved = EditorCommandCatalog.Resolve(Key.V, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.NotNull(resolved);
        Assert.Equal(EditorCommand.DistributeV, resolved!.Command);

        // 未列出的额外修饰键不再命中（原 when 条件会放行 Alt+Delete / Ctrl+Escape）
        Assert.Null(EditorCommandCatalog.Resolve(Key.Delete, KeyModifiers.Alt));
        Assert.Null(EditorCommandCatalog.Resolve(Key.Escape, KeyModifiers.Control));
        Assert.Null(EditorCommandCatalog.Resolve(Key.H, KeyModifiers.Control));   // 仅 Ctrl+H 未绑定
        Assert.Null(EditorCommandCatalog.Resolve(Key.G, KeyModifiers.Alt));
    }

    [Fact]
    public void Resolve_IgnoresUnrelatedModifierBits()
    {
        // Meta 位不参与匹配（不同平台/键盘布局会产生额外位）
        var resolved = EditorCommandCatalog.Resolve(Key.C, KeyModifiers.Control | KeyModifiers.Meta);
        Assert.Equal(EditorCommand.Copy, resolved!.Command);
    }

    [Fact]
    public void Resolve_UnboundKey_ReturnsNull()
    {
        Assert.Null(EditorCommandCatalog.Resolve(Key.F12, KeyModifiers.None));
        Assert.Null(EditorCommandCatalog.Resolve(Key.Z, KeyModifiers.Control)); // 撤销走菜单快捷键
    }

    [Theory]
    [InlineData(Key.C, KeyModifiers.Control, EditorCommand.Copy)]
    [InlineData(Key.V, KeyModifiers.Control, EditorCommand.Paste)]
    [InlineData(Key.D, KeyModifiers.Control, EditorCommand.Duplicate)]
    [InlineData(Key.Delete, KeyModifiers.None, EditorCommand.Delete)]
    [InlineData(Key.A, KeyModifiers.Control, EditorCommand.SelectAll)]
    [InlineData(Key.Escape, KeyModifiers.None, EditorCommand.CancelGesture)]
    [InlineData(Key.G, KeyModifiers.Control, EditorCommand.Group)]
    [InlineData(Key.G, KeyModifiers.Control | KeyModifiers.Shift, EditorCommand.Ungroup)]
    [InlineData(Key.H, KeyModifiers.Alt, EditorCommand.AlignLeft)]
    [InlineData(Key.B, KeyModifiers.Alt, EditorCommand.AlignBottom)]
    [InlineData(Key.H, KeyModifiers.Control | KeyModifiers.Shift, EditorCommand.DistributeH)]
    [InlineData(Key.S, KeyModifiers.Control | KeyModifiers.Shift, EditorCommand.SameSize)]
    [InlineData(Key.J, KeyModifiers.Control, EditorCommand.CycleTheme)]
    [InlineData(Key.J, KeyModifiers.Alt, EditorCommand.AlignCenterH)]
    [InlineData(Key.L, KeyModifiers.Control, EditorCommand.FocusSearch)]
    public void DocumentedShortcuts_MapToExpectedCommand(Key key, KeyModifiers modifiers, EditorCommand expected)
    {
        Assert.Equal(expected, EditorCommandCatalog.Resolve(key, modifiers)!.Command);
    }

    // ── 文本编辑场景 ─────────────────────────────────────────

    [Fact]
    public void TextEditingContext_IsDetected()
    {
        Assert.True(EditorCommandCatalog.IsTextEditingContext(new TextBox()));
        Assert.True(EditorCommandCatalog.IsTextEditingContext(new ComboBox()));
        Assert.False(EditorCommandCatalog.IsTextEditingContext(new Button()));
        Assert.False(EditorCommandCatalog.IsTextEditingContext(new Border()));
        Assert.False(EditorCommandCatalog.IsTextEditingContext(null));
    }

    // ── 右键菜单映射 ─────────────────────────────────────────

    [Fact]
    public void EveryContextAction_MapsToACommand()
    {
        // 右键菜单动作枚举必须**全部**有对应命令：漏一个会在运行时抛异常，
        // 也会让"两条输入路径覆盖同一操作集"这个前提悄悄失效。
        foreach (CanvasContextAction action in Enum.GetValues<CanvasContextAction>())
        {
            var command = EditorCommandCatalog.ToCommand(action); // 未映射会抛
            Assert.True(Enum.IsDefined(command), $"{action} → {command}");
        }
    }

    [Fact]
    public void ContextMenuAndShortcuts_CoverTheSameOperationSet()
    {
        var viaMenu = Enum.GetValues<CanvasContextAction>()
            .Select(EditorCommandCatalog.ToCommand)
            .ToHashSet();

        // 右键菜单的 15 个动作都能在快捷键表里找到对应命令（操作集不再分叉）
        var viaShortcuts = EditorCommandCatalog.Bindings.Select(b => b.Command).ToHashSet();
        var missing = viaMenu.Where(c => !viaShortcuts.Contains(c)).ToList();
        Assert.True(missing.Count == 0, "以下菜单命令没有快捷键绑定：" + string.Join(", ", missing));

        // 且菜单动作彼此不重复映射到同一命令（Copy/Paste 等各自唯一）
        var duplicates = Enum.GetValues<CanvasContextAction>()
            .GroupBy(EditorCommandCatalog.ToCommand)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ← {string.Join(" / ", g)}")
            .ToList();
        Assert.True(duplicates.Count == 0, "菜单动作重复映射：" + string.Join("; ", duplicates));
    }

    [Fact]
    public void Describe_OfSelectionCommands_IsHumanReadable()
    {
        foreach (var command in new[]
                 {
                     EditorCommand.Group, EditorCommand.Ungroup, EditorCommand.AlignLeft,
                     EditorCommand.DistributeV, EditorCommand.SameSize,
                 })
        {
            var text = EditorCommandCatalog.Describe(command);
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.NotEqual(command.ToString(), text); // 有提示文案，而不是枚举名
        }
    }

    /// <summary>
    /// <c>Describe</c> 的文案会出现在撤销/重做菜单项上（<c>MainWindow</c>：
    /// <c>Loc["project.undo"] + " " + UndoDescription</c>），所以它**必须真的来自字典**。
    ///
    /// <para>这里断言"等于 <c>Loc[Lang.CmdDesc*]</c>"之外，还特意断言**不等于键名** ——
    /// <see cref="HmiEditor.Core.Services.LocalizationService"/> 找不到键时返回键名自身，
    /// 只断言"非空、非枚举名"的话，键写错时测试照样是绿的。</para>
    /// </summary>
    [Fact]
    public void Describe_ComesFromTheDictionary_NotFromTheKeyName()
    {
        var pairs = new (EditorCommand Command, string Key)[]
        {
            (EditorCommand.Group, Lang.CmdDescGroup),
            (EditorCommand.Ungroup, Lang.CmdDescUngroup),
            (EditorCommand.AlignLeft, Lang.CmdDescAlignLeft),
            (EditorCommand.AlignCenterH, Lang.CmdDescAlignCenterH),
            (EditorCommand.AlignRight, Lang.CmdDescAlignRight),
            (EditorCommand.AlignTop, Lang.CmdDescAlignTop),
            (EditorCommand.AlignCenterV, Lang.CmdDescAlignCenterV),
            (EditorCommand.AlignBottom, Lang.CmdDescAlignBottom),
            (EditorCommand.SameSize, Lang.CmdDescSameSize),
            (EditorCommand.DistributeH, Lang.CmdDescDistributeH),
            (EditorCommand.DistributeV, Lang.CmdDescDistributeV),
        };

        foreach (var (command, key) in pairs)
        {
            var text = EditorCommandCatalog.Describe(command);
            Assert.Equal(AppLocalization.Service[key], text);
            Assert.NotEqual(key, text);                       // 键没落进字典时会回退成键名
            Assert.NotEqual(command.ToString(), text);
        }
    }

    [Fact]
    public void Chord_IsDerivedFromKeyAndModifiers()
    {
        // 手工写死的说明文案已删除，改成从绑定数据推导 —— 断言顺序与特例名
        static string[] ChordsOf(EditorCommand c) =>
            EditorCommandCatalog.Bindings.Where(b => b.Command == c).Select(b => b.Chord).ToArray();

        Assert.Equal(new[] { "Ctrl+C" }, ChordsOf(EditorCommand.Copy));
        Assert.Equal(new[] { "Ctrl+Shift+G" }, ChordsOf(EditorCommand.Ungroup)); // 修饰键顺序 Ctrl→Shift
        Assert.Equal(new[] { "Delete" }, ChordsOf(EditorCommand.Delete));
        Assert.Equal(new[] { "Esc" }, ChordsOf(EditorCommand.CancelGesture));    // Escape 显示成 Esc
        // 放大绑了两次（主键盘 + / 小键盘 +）：推导必须逐条跟着数据走，不能只有一条对
        Assert.Equal(new[] { "Ctrl+OemPlus", "Ctrl+Add" }, ChordsOf(EditorCommand.ZoomIn));
        Assert.Equal(new[] { "Ctrl+D0", "Ctrl+NumPad0" }, ChordsOf(EditorCommand.ZoomReset));
    }

    [Fact]
    public void FindConflicts_ActuallyReportsAConflict()
    {
        // 回归：原实现只读静态 Bindings，而表里本来就没有冲突 ——
        // "无冲突"这条断言在分组逻辑写坏时依然是绿的（守卫绿了 ≠ 守卫有能力红）。
        var conflicts = EditorCommandCatalog.FindConflicts(new[]
        {
            new ShortcutBinding(Key.C, KeyModifiers.Control, EditorCommand.Copy),
            new ShortcutBinding(Key.C, KeyModifiers.Control, EditorCommand.CycleTheme),
        });

        var only = Assert.Single(conflicts);
        Assert.Contains("Ctrl+C", only, StringComparison.Ordinal);
        Assert.Contains(nameof(EditorCommand.Copy), only, StringComparison.Ordinal);
        Assert.Contains(nameof(EditorCommand.CycleTheme), only, StringComparison.Ordinal);

        // 同键但修饰键不同 → 不算冲突（严格相等匹配）
        Assert.Empty(EditorCommandCatalog.FindConflicts(new[]
        {
            new ShortcutBinding(Key.C, KeyModifiers.Control, EditorCommand.Copy),
            new ShortcutBinding(Key.C, KeyModifiers.Shift, EditorCommand.Copy),
        }));

        // 无冲突表 → 空
        Assert.Empty(EditorCommandCatalog.FindConflicts(new[]
        {
            new ShortcutBinding(Key.C, KeyModifiers.Control, EditorCommand.Copy),
            new ShortcutBinding(Key.V, KeyModifiers.Control, EditorCommand.Paste),
        }));
    }
}
