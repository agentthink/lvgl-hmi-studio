using HmiEditor.App.Resources;
using HmiEditor.Core.Models;

// ── 只抑制 CS8524，**绝不**抑制 CS8509（同 ReferenceTextKeys / FindingDisplayText）：
//    · 下面的 switch 是**全覆盖 + 无默认支** ⇒ 编译器报 CS8524，举例是**未命名值**
//      （(HmiControlType)99 —— 枚举永远可能持有一个未声明值），与本文件要挡的事无关；
//    · 一旦 HmiControlType **新增具名取值**而没人接文案 ⇒ 报 CS8509，举例是那个具名成员
//      ⇒ ci-gate.sh（按 ": warning " 行判红）当场红。
//    ⇒ 抑制噪声、留住信号。
#pragma warning disable CS8524

namespace HmiEditor.App.Services;

/// <summary>
/// **控件类型显示名的键推导 —— 唯一实现**（阶段 96，闭环登记 #61o）。
///
/// <para><b>它挡的是什么</b>：<c>HmiControlType</c> 的显示名在界面上有**三个**露出点，
/// 而改动前是**两份手工维护的 <c>Lang.Tb*</c> 表**（同一映射写两遍）：
/// <list type="number">
///   <item>工具箱条目名（<c>ToolboxView.PopulateItems</c>，26 条）；</item>
///   <item>画布新建控件的**默认显示文案**（<c>DesignCanvas.BuildControl</c> 的 <c>Text</c>）**与
///     界面上那个本地化别名**（<see cref="ControlNameDefaults.Display(string, HmiControlType)"/>，
///     阶段 159 起）—— ⚠ 阶段 159 之前这里还兼着**持久化名字**，那条已按登记 #61r 摘掉：
///     工程里存的是中性名（<c>Button1</c>），本方法只负责"给人看"的那一份；</item>
///   <item>交叉引用面板的**控件类型列**（阶段 96 新增 —— 原先直显英文枚举名
///     <c>Label</c> / <c>Tank</c>）。</item>
/// </list>
/// ⇒ 第 ② 份**已经分叉**：它漏了 <c>Screen</c>，于是从工具箱拖「屏幕」建出来的控件默认名是
/// 英文 <c>Screen1</c>，而同一个控件在工具箱里叫「屏幕」。同一形状的教训见
/// <c>docs/2026-09-22-toolbox-icon-single-source.md</c>（同一个控件两套几何）。</para>
///
/// <para><b>为什么键推导住在 App 层</b>：与 <see cref="RoleTextKeys"/> / <see cref="ActionTextKeys"/> /
/// <see cref="ReferenceTextKeys"/> 同一条理由 —— Core 是库、不引 <c>Lang</c>/<c>LocalizationService</c>；
/// 而且 <c>Lang</c> 常量也只有 App 侧能引用。</para>
///
/// <para><b>为什么不叫 <c>ControlTypeNames</c></b>：那个名字已被
/// <c>HmiEditor.AI.Inference.PromptTemplateEngine.ControlTypeNames</c> 占用，语义完全不同
/// （AI schema 里**可生成**的控件类型 Id 清单，不是显示名）⇒ 同名不同义会被读成同一个东西。
/// 名字形态跟 <c>*TextKeys</c> 家族走。</para>
///
/// <para><b>为什么没有默认支</b>（本项目对枚举分派的一贯要求）：
/// <c>_ =&gt; type.ToString()</c> 这种兜底会把**新增的控件类型**静默渲染成英文枚举名 ——
/// 用户看到的是**假文案**，而且编译面、字典面、台账面**全都不响**（阶段 92 的 #61j 实测过）。
/// 无默认支时，新增具名成员 ⇒ 编译器报 <b>CS8509</b> 并**点名**那个成员 ⇒ 门禁当场红。
/// ⇒ 原来的 <c>_ =&gt; type.ToString()</c> 因此消失，这是**刻意的**：它可达的输入只有
/// <c>(HmiControlType)</c> 的非法值，而本方法的三条调用路径都来自工具箱点击 / 拖放 /
/// 面板行的真实控件（<c>control.Type</c>），非法值不会出现在那里。</para>
/// </summary>
internal static class ControlTypeTextKeys
{
    /// <summary>
    /// 控件类型的显示名 —— 工具箱条目、画布默认名、交叉引用面板类型列**共用这一份**。
    ///
    /// <para>键就是工具箱已经用了很久的那 26 条 <c>tb.*</c>（<c>tb.screen</c> = 屏幕 /
    /// <c>tb.textarea</c> = 输入框 / <c>tb.spinner</c> = 加载动画 …），两本字典里都齐
    /// —— 本方法只负责**分派**，不新造文案。</para>
    ///
    /// <para>顺序与 <see cref="HmiControlType"/> 的声明顺序一致，便于与枚举对拍
    /// （判据 <c>ReferenceFieldLocalizationGuardTests</c> 用反射双向钉住「26 个成员都有键」）。</para>
    /// </summary>
    internal static string Describe(HmiControlType type) => type switch
    {
        HmiControlType.Screen => AppLocalization.Service[Lang.TbScreen],
        HmiControlType.Button => AppLocalization.Service[Lang.TbButton],
        HmiControlType.Label => AppLocalization.Service[Lang.TbLabel],
        HmiControlType.Slider => AppLocalization.Service[Lang.TbSlider],
        HmiControlType.Switch => AppLocalization.Service[Lang.TbSwitch],
        HmiControlType.CheckBox => AppLocalization.Service[Lang.TbCheckbox],
        HmiControlType.Image => AppLocalization.Service[Lang.TbImage],
        HmiControlType.Bar => AppLocalization.Service[Lang.TbBar],
        HmiControlType.Dropdown => AppLocalization.Service[Lang.TbDropdown],
        HmiControlType.TextArea => AppLocalization.Service[Lang.TbTextarea],
        HmiControlType.Gauge => AppLocalization.Service[Lang.TbGauge],
        HmiControlType.Knob => AppLocalization.Service[Lang.TbKnob],
        HmiControlType.Keyboard => AppLocalization.Service[Lang.TbKeyboard],
        HmiControlType.Led => AppLocalization.Service[Lang.TbLed],
        HmiControlType.Spinner => AppLocalization.Service[Lang.TbSpinner],
        HmiControlType.TabView => AppLocalization.Service[Lang.TbTabview],
        HmiControlType.Line => AppLocalization.Service[Lang.TbLine],
        HmiControlType.Table => AppLocalization.Service[Lang.TbTable],
        HmiControlType.Svg => AppLocalization.Service[Lang.TbSvg],
        HmiControlType.Tank => AppLocalization.Service[Lang.TbTank],
        HmiControlType.Pipe => AppLocalization.Service[Lang.TbPipe],
        HmiControlType.Valve => AppLocalization.Service[Lang.TbValve],
        HmiControlType.Rectangle => AppLocalization.Service[Lang.TbRectangle],
        HmiControlType.Ellipse => AppLocalization.Service[Lang.TbEllipse],
        HmiControlType.Polyline => AppLocalization.Service[Lang.TbPolyline],
        HmiControlType.Arc => AppLocalization.Service[Lang.TbArc],
    };
}
