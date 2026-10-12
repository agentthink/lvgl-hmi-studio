using HmiEditor.App.Resources;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Services;

/// <summary>
/// **控件默认名的唯一来源**（阶段 159，闭环登记 #61r）。
///
/// <para><b>它挡的是什么</b>：改动前画布新建控件的<b>持久化名字</b>取的是
/// <see cref="ControlTypeTextKeys.Describe"/>（<b>本地化</b>显示名）＋序号 ——
/// 中文界面建出来叫 <c>按钮1</c>、英文界面建出来叫 <c>Button1</c>。而这个名字
/// <b>写进工程文件、并进生成代码的符号</b> ⇒ 同一份工程在两种语言下编辑，得到的是
/// <b>两份不同的工程</b>（导出也就不可复现）。这与「同一工程两次导出应逐字节一致」
/// 是同一条纪律，只不过对象是<b>工程文件自身</b>。</para>
///
/// <para><b>两条纪律分开</b>（本类就是那条分界线）：
/// <list type="number">
///   <item><b>持久化名与语言无关</b> —— <see cref="UniqueName"/> 给出 <c>Button1</c> /
///     <c>TextArea2</c>（<see cref="HmiControlType"/> 的枚举名 ＋ 序号）。ASCII、稳定、
///     直接可做 C 符号，也不会随界面语言漂。</item>
///   <item><b>界面上显示本地化别名</b> —— <see cref="Display(string, HmiControlType)"/> 把
///     <b>默认名形状</b>（中性前缀 ＋ 纯数字）翻成 <c>按钮1</c>（zh）/ <c>Button1</c>（en）；
///     <b>用户改过名的控件原样显示</b>（<c>MyLamp</c> 在两种语言下都是 <c>MyLamp</c>）。</item>
/// </list>
/// 两者用 <see cref="ToPersisted"/> 接起来：它就是 <see cref="Display(string, HmiControlType)"/>
/// 的<b>左逆</b> —— 「界面上显示什么、写回什么」于是成为<b>不动点</b>，用户按看到的名字改一遍
/// 也不会把工程写回成"随语言变"的形状。</para>
///
/// <para><b>三种名字各走哪条路</b>：
/// <list type="bullet">
///   <item><c>Button1</c>（默认名）⇒ 显示 <c>按钮1</c>；写回仍是 <c>Button1</c>；</item>
///   <item><c>报警灯</c>（用户改的名）⇒ 原样显示、原样写回；</item>
///   <item><c>按钮1</c>（<b>旧工程</b>里那种按语言建出来的名，或英文界面下显示成
///     <c>Button1</c>）⇒ <b>显示不变</b>、写回时归一到 <c>Button1</c>（一次<b>迁移</b>：
///     用户看不到任何变化，但工程从此可复现）。</item>
/// </list></para>
///
/// <para>⚠ <b>为什么不加一个 <c>IsAutoNamed</c> 标志位</b>：那要动工程 schema、要迁移、
/// 还要兼容旧包；而"默认名形状"本身是可判的（前缀 ＋ 纯数字）—— 代价是**手工**把控件命名成
/// <c>Button1</c> 时也会被当成默认名（显示成 <c>按钮1</c>）。这个代价写在这里，是已知边界。</para>
/// </summary>
internal static class ControlNameDefaults
{
    /// <summary>中性前缀 = 枚举名（<c>Button</c> / <c>TextArea</c> / <c>Screen</c> …），与语言无关。</summary>
    internal static string NeutralPrefix(HmiControlType type) => type.ToString();

    /// <summary>
    /// 生成一个不与 <paramref name="existingNames"/> 冲突的中性名（<c>Button1</c> / <c>Button2</c> …）。
    /// <para>序号从 1 起、取第一个空位（与改动前的行为逐字相同，只是前缀换了）。</para>
    /// </summary>
    internal static string UniqueName(HmiControlType type, IEnumerable<string> existingNames)
    {
        var prefix = NeutralPrefix(type);
        var taken = existingNames as ICollection<string> ?? existingNames.ToList();
        var index = 1;
        while (taken.Contains($"{prefix}{index}")) index++;
        return $"{prefix}{index}";
    }

    /// <summary>控件的界面显示名（见类注释的两条纪律）。</summary>
    internal static string Display(HmiControl control) => Display(control.Name, control.Type);

    /// <summary>
    /// 界面显示名：<b>默认名形状</b>（该类型的中性前缀 ＋ 纯数字）→ 本地化别名；
    /// 其余（用户改的名、旧工程里按语言建的名、前缀对不上的名）→ <b>原样</b>。
    /// </summary>
    internal static string Display(string name, HmiControlType type)
        => TrySplit(name, NeutralPrefix(type), out var index)
            ? $"{ControlTypeTextKeys.Describe(type)}{index}"
            : name;

    /// <summary>
    /// 写回：把界面上显示的名字映射回<b>持久化名</b>（<see cref="Display(string, HmiControlType)"/> 的左逆）。
    /// <list type="bullet">
    ///   <item>显示的是别名（<c>按钮1</c>）⇒ 落盘成中性名（<c>Button1</c>）—— 旧工程因此被迁移；</item>
    ///   <item>其它一切（用户自己的名字、已经是中性名的）⇒ 原样落盘。</item>
    /// </list>
    /// </summary>
    internal static string ToPersisted(string shown, HmiControlType type)
        => TrySplit(shown, ControlTypeTextKeys.Describe(type), out var index)
            ? $"{NeutralPrefix(type)}{index}"
            : shown;

    /// <summary>
    /// <paramref name="name"/> 是不是 <paramref name="prefix"/> ＋ 纯数字（且数字 &gt; 0）。
    /// ⚠ 用 <see cref="StringComparison.Ordinal"/>：名字是标识符，不做文化敏感比较
    /// （否则土耳其语的 <c>I/i</c> 之类会咬人）。
    /// </summary>
    private static bool TrySplit(string name, string prefix, out int index)
    {
        index = 0;
        if (string.IsNullOrEmpty(name) || name.Length <= prefix.Length) return false;
        if (!name.StartsWith(prefix, StringComparison.Ordinal)) return false;

        var digits = name.AsSpan(prefix.Length);
        foreach (var ch in digits)
            if (ch is < '0' or > '9') return false;

        return int.TryParse(digits, out index) && index > 0;
    }
}
