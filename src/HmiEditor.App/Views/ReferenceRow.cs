using HmiEditor.Core.Services;

namespace HmiEditor.App.Views;

/// <summary>
/// 交叉引用面板的**行**（阶段 94 闭环登记 #61m，阶段 95 闭环登记 #61n）。
///
/// <para><b>为什么需要这一层</b>：面板原先把 <see cref="ReferenceLocation"/> 直接塞进
/// <c>ListBox.ItemsSource</c>，模板里 <c>{Binding Description}</c> 直显 Core 拼的中文句子 ——
/// 而 Core 是库、不引本地化 ⇒ **没有渲染点**，本地化无处生效。</para>
///
/// <para><b>渲染点现在在哪</b>：<see cref="Description"/> 转发给
/// <see cref="Services.ReferenceTextKeys.Describe"/> —— 那是**唯一**的分派处
/// （按 <see cref="ReferenceLocation.DescriptionKind"/> 选模板，动作那一档再交给
/// <see cref="Services.ActionTextKeys"/>）。本类刻意**不再**自己判 <c>Action</c> 非空：
/// 同一件事在两处判会漂移，而且分派散开之后「新增一种描述种类」就得记得改两个地方。</para>
///
/// <para><b>属性名与模板一一对应</b>：<c>CrossReferenceView.axaml</c> 的
/// <c>x:DataType</c> 已是本类型，模板里的
/// <c>ReferenceType</c> / <c>ControlName</c> / <c>ControlType</c> / <c>Description</c> /
/// <c>ScreenName</c> 五个绑定**逐字未动**。</para>
///
/// <para><b>三个枚举名现在都走字典了</b>（阶段 96，闭环 <b>#61o</b>）：<see cref="ReferenceType"/>
/// 走 <see cref="Services.ReferenceTextKeys.TypeName"/>，<see cref="ControlType"/> 走
/// <see cref="Services.ControlTypeTextKeys.Describe"/>（与工具箱、画布默认名**同一份**），
/// 而 <c>Description</c> 里那个方向枚举名走 <see cref="Services.ReferenceTextKeys.DirectionName"/>。
/// 阶段 94/95 两次把「一次只放开一个变量」写在这条注释里，阶段 96 一次收掉。</para>
///
/// <para><b>语言切换</b>：面板由 <c>MainWindow.RebuildAllPanels</c> 在语言变化时**整体重建**
/// （新实例、空结果）⇒ 本层不需要 <c>INotifyPropertyChanged</c>，与改动前一致。</para>
/// </summary>
public sealed class ReferenceRow
{
    private readonly ReferenceLocation _source;

    public ReferenceRow(ReferenceLocation source) => _source = source;

    /// <summary>引用类型（走字典，跨语言）。</summary>
    public string ReferenceType => Services.ReferenceTextKeys.TypeName(_source.ReferenceType);

    /// <summary>
    /// 控件名称 —— 走 <see cref="Services.ControlNameDefaults.Display(string, HmiControlType)"/>：
    /// 默认名（<c>Button1</c>）在这里显示成本地化别名（<c>按钮1</c>），用户改过的名原样显示
    /// （登记 #61r，阶段 159）。
    /// </summary>
    public string ControlName => Services.ControlNameDefaults.Display(_source.ControlName, _source.ControlType);

    /// <summary>控件类型（走字典，跨语言；与工具箱条目共用一份）。</summary>
    public string ControlType => Services.ControlTypeTextKeys.Describe(_source.ControlType);

    /// <summary>屏幕名称。</summary>
    public string ScreenName => _source.ScreenName;

    /// <summary>描述：按描述种类走字典（跨语言）；Unset 与矛盾状态才回落 Core 的原文。</summary>
    public string Description => Services.ReferenceTextKeys.Describe(_source);
}
