using Avalonia.Controls;
using HmiEditor.App.Resources;

namespace HmiEditor.App.Controls;

/// <summary>
/// 「面板上有、其实不生效」的统一标注。
///
/// <para><b>它解决的是什么：</b>配置项不生效时，最糟的处理不是留着，而是<b>不留任何痕迹</b> ——
/// 用户在对话框里改了、点确定、值也被记住了，然后行为一点没变，
/// 而他无从判断是自己理解错了还是软件坏了。标注（灰显 + 悬停说明原因）
/// 让"这一项现在不生效"变成用户一眼可见的事实。</para>
///
/// <para><b>标注的判据不在这里，在 <c>ConfigFieldInventory</c>。</b>
/// 这一层只负责"怎么显示"；"哪些要标"由那份受管清单决定，
/// 并有守卫断言清单与面板标注一致 —— 免得清单越写越全、面板越标越少。</para>
/// </summary>
internal static class NotYetEffective
{
    /// <summary>灰显不透明度：能看清，但明显弱于生效项。</summary>
    private const double DimmedOpacity = 0.55;

    /// <summary>
    /// 给控件打上"尚未生效"标注：灰显 + 悬停提示原因。
    /// </summary>
    /// <param name="reasonKey">
    /// 原因的**本地化键**（不是解析好的文案）。解析放在这里、而不是放在清单里：
    /// 清单是静态字段，静态初始化只跑一次 —— 提前解析会把语言冻结在首次访问那一刻，
    /// 之后切换语言，悬停提示不会跟着变。
    /// </param>
    public static void Mark(Control control, string reasonKey)
    {
        control.Opacity = DimmedOpacity;
        var loc = AppLocalization.Service;
        ToolTip.SetTip(control, loc.Format(Lang.SettingsNotEffectiveTip, loc[reasonKey]));
    }
}
