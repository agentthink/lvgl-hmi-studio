using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using HmiEditor.App.Resources;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Services;

/// <summary>
/// 编辑器命令 —— 与"谁触发"无关的操作标识。
///
/// <para>快捷键与画布右键菜单此前各写了一份 switch，覆盖的 15 个操作**完全相同**
/// （见 <see cref="EditorCommandCatalog.ToCommand"/>），属于同一份业务逻辑的两份实现，
/// 已经开始漂移（例：Ctrl+Shift+V 本意是"垂直等距排列"，却先命中了 Paste 分支）。
/// 现在两条输入路径都收敛到 <see cref="EditorCommandCatalog"/> + 唯一的分发实现。</para>
/// </summary>
public enum EditorCommand
{
    Copy, Paste, Duplicate, Delete,
    Group, Ungroup,
    AlignLeft, AlignCenterH, AlignRight,
    AlignTop, AlignCenterV, AlignBottom,
    SameSize, DistributeH, DistributeV,
    SelectAll, ZoomIn, ZoomOut, ZoomReset,
    CancelGesture, FocusSearch, CycleTheme,
}

/// <summary>一条快捷键绑定：按键 + 修饰键 → 命令。</summary>
/// <param name="Key">主键。</param>
/// <param name="Modifiers">要求的修饰键（严格相等匹配，见 <see cref="EditorCommandCatalog.Resolve"/>）。</param>
/// <param name="Command">对应命令。</param>
public sealed record ShortcutBinding(Key Key, KeyModifiers Modifiers, EditorCommand Command)
{
    /// <summary>
    /// 人类可读的按键组合，**由 <see cref="Key"/> / <see cref="Modifiers"/> 推导**。
    ///
    /// <para>这里原来是一个手写的 <c>string Description</c> 参数（如 <c>"Ctrl+小键盘+ 放大"</c>），
    /// 内容与 <see cref="Key"/>+<see cref="Modifiers"/> 完全重复 —— 改了键位忘了改文案就会
    /// 静默说谎，而它唯一的去处（<see cref="EditorCommandCatalog.FindConflicts"/> 的冲突诊断）
    /// 恰恰就是用来判断"到底哪里撞了"的。改成推导后这类漂移在结构上不可能发生。</para>
    ///
    /// <para>刻意用 <see cref="Key"/> 的枚举名（<c>OemPlus</c>/<c>NumPad0</c>）而不是 <c>+</c>/<c>小键盘0</c>：
    /// 冲突消息是**开发者诊断**（异常/测试失败信息），枚举名才可搜索、也不会被本地化。</para>
    /// </summary>
    public string Chord => FormatChord(Key, Modifiers);

    private static string FormatChord(Key key, KeyModifiers modifiers)
    {
        var parts = new List<string>(4);
        if (modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");
        parts.Add(key == Key.Escape ? "Esc" : key.ToString());
        return string.Join("+", parts);
    }
}

/// <summary>
/// 命令目录：快捷键表 + 右键菜单映射 + 纯查询逻辑。
///
/// <para>把"快捷键表"从 163 行的 <c>switch</c> 里提出来，换来三件原本做不到的事：
/// ① 可以在无 UI 的情况下断言每个绑定确实解析到预期命令；
/// ② 可以断言**没有冲突**（同一按键组合绑定到两个命令）——switch 里写重了只会静默取第一个；
/// ③ 可以断言右键菜单的每个动作都能映射到命令，避免两条路径再次分叉。</para>
/// </summary>
public static class EditorCommandCatalog
{
    /// <summary>修饰键掩码：Meta/其它位不参与匹配。</summary>
    private const KeyModifiers RelevantModifiers =
        KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt;

    /// <summary>
    /// 全部快捷键绑定。顺序不影响解析（<see cref="Resolve"/> 要求修饰键严格相等），
    /// 但仍按"文件/编辑 → 选择 → 缩放 → 组合 → 对齐 → 排列 → 其它"排列以便阅读。
    /// </summary>
    public static IReadOnlyList<ShortcutBinding> Bindings { get; } = new ShortcutBinding[]
    {
        // 文件/编辑
        new(Key.L, KeyModifiers.Control, EditorCommand.FocusSearch),
        new(Key.C, KeyModifiers.Control, EditorCommand.Copy),
        new(Key.V, KeyModifiers.Control, EditorCommand.Paste),
        new(Key.D, KeyModifiers.Control, EditorCommand.Duplicate),
        new(Key.Delete, KeyModifiers.None, EditorCommand.Delete),

        // 选择
        new(Key.A, KeyModifiers.Control, EditorCommand.SelectAll),
        new(Key.Escape, KeyModifiers.None, EditorCommand.CancelGesture),

        // 缩放
        new(Key.OemPlus, KeyModifiers.Control, EditorCommand.ZoomIn),
        new(Key.Add, KeyModifiers.Control, EditorCommand.ZoomIn),
        new(Key.OemMinus, KeyModifiers.Control, EditorCommand.ZoomOut),
        new(Key.Subtract, KeyModifiers.Control, EditorCommand.ZoomOut),
        new(Key.D0, KeyModifiers.Control, EditorCommand.ZoomReset),
        new(Key.NumPad0, KeyModifiers.Control, EditorCommand.ZoomReset),

        // 组合
        new(Key.G, KeyModifiers.Control, EditorCommand.Group),
        new(Key.G, KeyModifiers.Control | KeyModifiers.Shift, EditorCommand.Ungroup),

        // 对齐（Alt+H/J/K 左/中/右，Alt+T/M/B 上/中/下）
        new(Key.H, KeyModifiers.Alt, EditorCommand.AlignLeft),
        new(Key.J, KeyModifiers.Alt, EditorCommand.AlignCenterH),
        new(Key.K, KeyModifiers.Alt, EditorCommand.AlignRight),
        new(Key.T, KeyModifiers.Alt, EditorCommand.AlignTop),
        new(Key.M, KeyModifiers.Alt, EditorCommand.AlignCenterV),
        new(Key.B, KeyModifiers.Alt, EditorCommand.AlignBottom),

        // 排列
        new(Key.H, KeyModifiers.Control | KeyModifiers.Shift, EditorCommand.DistributeH),
        new(Key.V, KeyModifiers.Control | KeyModifiers.Shift, EditorCommand.DistributeV),
        new(Key.S, KeyModifiers.Control | KeyModifiers.Shift, EditorCommand.SameSize),

        // 主题
        new(Key.J, KeyModifiers.Control, EditorCommand.CycleTheme),
    };

    /// <summary>
    /// 解析按键组合 → 命令。修饰键**严格相等**匹配。
    ///
    /// <para>这里比原 <c>switch</c> 的 <c>when</c> 条件更严格，是有意的收紧：原写法
    /// <c>case Key.Delete when !ctrl</c> 会放行 <c>Alt+Delete</c>，
    /// <c>case Key.Escape:</c> 会放行 <c>Ctrl+Escape</c>；现在只认列在表里的组合。</para>
    /// </summary>
    public static ShortcutBinding? Resolve(Key key, KeyModifiers modifiers)
    {
        var wanted = modifiers & RelevantModifiers;
        return Bindings.FirstOrDefault(b => b.Key == key && Normalize(b.Modifiers) == wanted);
    }

    /// <summary>
    /// 文本编辑场景（TextBox/ComboBox/NumberBox）不拦截全局快捷键 ——
    /// 否则 Ctrl+A / Ctrl+C 会破坏文本编辑语义。
    /// </summary>
    public static bool IsTextEditingContext(object? focusedElement) =>
        focusedElement is TextBox or ComboBox
        || focusedElement?.GetType().Name.Contains("NumberBox", StringComparison.Ordinal) == true;

    /// <summary>
    /// 画布右键菜单动作 → 命令。两条输入路径共享同一实现，避免再次各写一遍。
    /// </summary>
    public static EditorCommand ToCommand(CanvasContextAction action) => action switch
    {
        CanvasContextAction.Copy => EditorCommand.Copy,
        CanvasContextAction.Paste => EditorCommand.Paste,
        CanvasContextAction.Duplicate => EditorCommand.Duplicate,
        CanvasContextAction.Delete => EditorCommand.Delete,
        CanvasContextAction.AlignLeft => EditorCommand.AlignLeft,
        CanvasContextAction.AlignCenterH => EditorCommand.AlignCenterH,
        CanvasContextAction.AlignRight => EditorCommand.AlignRight,
        CanvasContextAction.AlignTop => EditorCommand.AlignTop,
        CanvasContextAction.AlignCenterV => EditorCommand.AlignCenterV,
        CanvasContextAction.AlignBottom => EditorCommand.AlignBottom,
        CanvasContextAction.SameSize => EditorCommand.SameSize,
        CanvasContextAction.DistributeH => EditorCommand.DistributeH,
        CanvasContextAction.DistributeV => EditorCommand.DistributeV,
        CanvasContextAction.Group => EditorCommand.Group,
        CanvasContextAction.Ungroup => EditorCommand.Ungroup,
        _ => throw new ArgumentOutOfRangeException(
            nameof(action), action, "画布右键菜单动作未映射到编辑器命令"),
    };

    private static LocalizationService Loc => AppLocalization.Service;

    /// <summary>
    /// 命令的默认提示文案（对齐/排列类用于"未选中控件"提示，也用于撤销/重做菜单项 ——
    /// 见 <c>MainWindow</c> 的 <c>project.undo + UndoDescription</c>）。**用户可见，故必须入字典。**
    /// </summary>
    public static string Describe(EditorCommand command) => command switch
    {
        EditorCommand.Group => Loc[Lang.CmdDescGroup],
        EditorCommand.Ungroup => Loc[Lang.CmdDescUngroup],
        EditorCommand.AlignLeft => Loc[Lang.CmdDescAlignLeft],
        EditorCommand.AlignCenterH => Loc[Lang.CmdDescAlignCenterH],
        EditorCommand.AlignRight => Loc[Lang.CmdDescAlignRight],
        EditorCommand.AlignTop => Loc[Lang.CmdDescAlignTop],
        EditorCommand.AlignCenterV => Loc[Lang.CmdDescAlignCenterV],
        EditorCommand.AlignBottom => Loc[Lang.CmdDescAlignBottom],
        EditorCommand.SameSize => Loc[Lang.CmdDescSameSize],
        EditorCommand.DistributeH => Loc[Lang.CmdDescDistributeH],
        EditorCommand.DistributeV => Loc[Lang.CmdDescDistributeV],
        _ => command.ToString(),
    };

    /// <summary>
    /// 同一按键组合被绑定到多个命令即为冲突。返回冲突描述（空 = 无冲突）。
    /// 这是原 <c>switch</c> 写法**无法**表达的检查：写重了只会静默命中第一个分支。
    /// </summary>
    public static IReadOnlyList<string> FindConflicts() => FindConflicts(Bindings);

    /// <summary>
    /// <see cref="FindConflicts()"/> 的**可注入**版本。
    ///
    /// <para><b>为什么需要它</b>：原实现只读静态 <see cref="Bindings"/>，而这张表里
    /// 本来就没有冲突 —— 于是"无冲突"这条断言在分组逻辑写错时**照样是绿的**
    /// （守卫绿了 ≠ 守卫有能力红）。留一个能灌构造数据的入口，才谈得上验证它真的会报。</para>
    /// </summary>
    internal static IReadOnlyList<string> FindConflicts(IEnumerable<ShortcutBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        return bindings
            .GroupBy(b => (b.Key, Normalize(b.Modifiers)))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.First().Chord} is bound to " +
                         string.Join(" / ", g.Select(b => b.Command.ToString())))
            .ToList();
    }

    private static KeyModifiers Normalize(KeyModifiers modifiers) => modifiers & RelevantModifiers;
}
