namespace HmiEditor.Core.Services;

/// <summary>
/// 一条**悬空引用**的位置 —— 「**定位码 + 中文回落原文** ＋ 实参」三段式
/// （阶段 93，闭环登记 <b>#61l</b>），与 <see cref="SvgParseError"/> /
/// <see cref="ExpressionSyntaxError"/> / <c>CsvImportIssue</c> 同形。
///
/// <para><b>为什么要有这个类型</b>：<see cref="CrossReferenceAnalyzer.FindDanglingReferences"/>
/// 以前把位置直接**拼成中文串**返回（<c>List&lt;(string Location, string BindingId)&gt;</c>），
/// 那个串被塞进 <c>GeneratedCodeFinding.Location</c> 且 <c>LocationCode</c> 留空
/// ⇒ **英文界面下那一半是中英混排**（`[main] 控件 'value' (Value)`）。
/// 而"这一串归谁拼"曾经是本项没做下去的原因 —— 实测（阶段 93）它只有**一个**
/// 用户可见消费点（生成代码验证结果对话框）；交叉引用面板显示的是**兄弟串**
/// （`ReferenceLocation.Description`，另一套措辞），状态行只取<b>条数</b>。
/// ⇒ 把码交出来即可，不必等"两个界面一起收口"。</para>
///
/// <para><b><see cref="Message"/> 是回落、不是文案</b>：字典缺键时原样显示它
/// （宁可显示中文，也不能把码名显示给用户）。它**逐字保留**了改动前的格式
/// （含方括号与单引号），于是「字典缺键」这条路上用户看到的东西与改动前一致 ——
/// 变的是**正常路径**：现在走 <c>gen.loc.*</c>，中英文各归各。</para>
///
/// <para><b>为什么实参是"数据"而「动作」不是</b>：第三格装的是**位置名**，
/// 悬空有两种载体 —— 控件绑定（属性名）与动作目标。属性名是用户的数据；
/// 而"动作"这个词自己要被翻译 ⇒ 它落在
/// <see cref="FindingLocationCodes.SCREEN_CONTROL_ACTION"/> **那个键的固定词**里。</para>
/// </summary>
/// <param name="Code">定位码，取自 <see cref="FindingLocationCodes"/> 的常量值。</param>
/// <param name="Message">**产出方自带的中文回落原文**。</param>
/// <param name="Args">定位模板的占位符实参；<c>null</c> = 模板不含占位符。</param>
public sealed record DanglingRefLocation(string Code, string Message, IReadOnlyList<object>? Args = null)
{
    /// <summary>控件变量绑定悬空（联动模型：定位到**属性**）。3 实参。</summary>
    /// <param name="screenName">屏幕名。</param>
    /// <param name="controlName">控件名。</param>
    /// <param name="propertyName">绑定的属性名。</param>
    public static DanglingRefLocation OfBinding(string screenName, string controlName, string propertyName)
        => new(FindingLocationCodes.SCREEN_CONTROL_PROPERTY,
            $"[{screenName}] 控件 '{controlName}' ({propertyName})",
            new object[] { screenName, controlName, propertyName });

    /// <summary>**回退旧字段**的控件绑定悬空（没有属性名这一格）。2 实参。</summary>
    /// <param name="screenName">屏幕名。</param>
    /// <param name="controlName">控件名。</param>
    public static DanglingRefLocation OfLegacyBinding(string screenName, string controlName)
        => new(FindingLocationCodes.SCREEN_CONTROL,
            $"[{screenName}] 控件 '{controlName}'",
            new object[] { screenName, controlName });

    /// <summary>**动作目标**悬空（<c>HmiAction.Target</c> 指向已删变量）。2 实参。</summary>
    /// <param name="screenName">屏幕名。</param>
    /// <param name="controlName">控件名。</param>
    public static DanglingRefLocation OfAction(string screenName, string controlName)
        => new(FindingLocationCodes.SCREEN_CONTROL_ACTION,
            $"[{screenName}] 控件 '{controlName}' 动作",
            new object[] { screenName, controlName });
}
