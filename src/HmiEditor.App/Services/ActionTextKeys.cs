using System;
using System.Collections.Generic;
using HmiEditor.App.Resources;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Services;

/// <summary>
/// **动作展示名的键推导 —— 唯一实现**（闭环登记 #61m）。
///
/// <para><b>为什么需要它</b>：控件动作在界面上有三个露出点，改动前全部是 Core 拼的中文 ——
/// ① 属性面板的动作类型**下拉框**（<c>HmiAction.GetAvailableActions</c> 的 <c>ActionMeta.DisplayName</c>）；
/// ② 属性面板的动作 **chip 列表**（<c>ActionTypeMeta.DisplayName</c>）；
/// ③ **交叉引用面板**的 <c>Description</c> 列（<c>HmiAction.GetDescription()</c>）。
/// 英文界面下这三处全是中文（chip 对「无动作」还显示英文 id <c>None</c>）。</para>
///
/// <para><b>为什么键推导住在 App 层</b>：与 <see cref="RoleTextKeys"/> / <see cref="PluginTextKeys"/>
/// 同一条理由 —— Core 是库、不引 <c>Lang</c>/<c>LocalizationService</c>；「把模型的东西翻译成键」
/// 是**视图侧**的事，<c>Lang</c> 常量也只有 App 侧能引用。</para>
///
/// <para><b>与 Core 那两份中文的关系</b>（本项最容易做错的地方）：
/// <list type="bullet">
/// <item><see cref="ActionTypeMeta.DisplayName"/> 的 12 条中文**必须留在** Core ——
/// 它们是**生成产物**里的注释（<c>CodeGenerator.Actions.cs</c> / <c>SecurityConfigCodeGen.cs</c>），
/// 产物可复现性要求它们冻结；</item>
/// <item><see cref="HmiAction.GetDescription"/> 的 13 条中文也留在 Core，作为**面板的回落原文**
/// （字典缺键时才显示）；</item>
/// <item>本表是它们的**界面侧孪生** —— 因此两份「同一件事」的一致性**必须由判据钉住**，
/// 而不是靠约定：<c>ActionTextLocalizationGuardTests</c> 用**相等式**断言
/// zh 字典的 <c>action.type.*</c> 逐字等于 <see cref="ActionTypeMeta.DisplayName"/>
/// 对同样 12 个 Id 的返回值。两份会漂移，所以要钉。</item>
/// </list></para>
///
/// <para><b>下拉框与 chip 为什么共用一套措辞</b>：改动前 <c>SetBit</c> 在下拉里是
/// <c>"置位 (ON)"</c>、在 chip 里是 <c>"置位"</c>，而 <c>SetVariable</c> **在同一个方法的不同控件表里**
/// 既是 <c>"设置变量"</c> 又是 <c>"设置变量值"</c> ⇒ 同一概念多套措辞（与登记 #61j
/// 「同一族文案有两条独立实现」同病）。本表**收敛成一套**，措辞取 Core 那一套
/// （与产物注释一致）。⇒ 三处**用户可见的措辞变化**已登记：下拉里的
/// <c>置位 (ON)</c> → <c>置位</c>、<c>复位 (OFF)</c> → <c>复位</c>、<c>设置变量值</c> → <c>设置变量</c>。</para>
/// </summary>
internal static class ActionTextKeys
{
    /// <summary>目标/取值为空时占位显示的字符（语言无关的标点，不进字典）。</summary>
    private const string Placeholder = "?";

    /// <summary>
    /// 本表覆盖的动作类型 Id（13 个：<see cref="ActionTypeMeta"/> 的 12 个公开 Id 常量 ＋ <c>None</c>）。
    ///
    /// <para><b>用 <see cref="HmiAction.None"/><c>.ActionType</c> 而不是字面量 <c>"None"</c></b>：
    /// <c>"None"</c> 这个 Id 只在工厂里出现一次（与 <see cref="RoleTextKeys"/>「用模型的
    /// <c>.Id</c>、不用字面量」同一条纪律 —— Id 字符串不出现第二遍）。</para>
    ///
    /// <para><b>守卫拿它与 <see cref="ActionTypeMeta"/> 的公开 Id 常量双向对拍</b>：漏一个 ⇒ 红
    /// （否则那一个动作类型会静默回落成英文 Id）。</para>
    /// </summary>
    internal static readonly IReadOnlyList<string> MappedActionTypes = new[]
    {
        // None 排第一：下拉框与 chip 都把它当默认项。
        HmiAction.None.ActionType,
        ActionTypeMeta.ToggleBit,
        ActionTypeMeta.SetBit,
        ActionTypeMeta.ResetBit,
        ActionTypeMeta.MomentaryOn,
        ActionTypeMeta.MomentaryOff,
        ActionTypeMeta.SetVariable,
        ActionTypeMeta.SwitchScreen,
        ActionTypeMeta.ExecuteScript,
        ActionTypeMeta.ShowAlarms,
        ActionTypeMeta.ShowTrends,
        ActionTypeMeta.Login,
        ActionTypeMeta.Logout,
    };

    /// <summary>是否为「无动作」（<c>null</c>/空串按无动作处理 —— 与 <see cref="HmiAction"/> 的默认值一致）。</summary>
    private static bool IsNone(string? actionType) =>
        string.IsNullOrEmpty(actionType)
        || string.Equals(actionType, HmiAction.None.ActionType, StringComparison.Ordinal);

    /// <summary>
    /// 动作类型名（**短名**）—— 属性面板的动作下拉与动作 chip 共用。
    /// 未知类型回落成**原始 Id**（结构上中性的 ASCII，与 <see cref="ActionTypeMeta.DisplayName"/> 同口径）。
    /// </summary>
    internal static string TypeName(string? actionType)
    {
        if (IsNone(actionType)) return AppLocalization.Service[Lang.ActionTypeNone];

        return actionType switch
        {
            ActionTypeMeta.ToggleBit => AppLocalization.Service[Lang.ActionTypeToggleBit],
            ActionTypeMeta.SetBit => AppLocalization.Service[Lang.ActionTypeSetBit],
            ActionTypeMeta.ResetBit => AppLocalization.Service[Lang.ActionTypeResetBit],
            ActionTypeMeta.MomentaryOn => AppLocalization.Service[Lang.ActionTypeMomentaryOn],
            ActionTypeMeta.MomentaryOff => AppLocalization.Service[Lang.ActionTypeMomentaryOff],
            ActionTypeMeta.SetVariable => AppLocalization.Service[Lang.ActionTypeSetVariable],
            ActionTypeMeta.SwitchScreen => AppLocalization.Service[Lang.ActionTypeSwitchScreen],
            ActionTypeMeta.ExecuteScript => AppLocalization.Service[Lang.ActionTypeExecuteScript],
            ActionTypeMeta.ShowAlarms => AppLocalization.Service[Lang.ActionTypeShowAlarms],
            ActionTypeMeta.ShowTrends => AppLocalization.Service[Lang.ActionTypeShowTrends],
            ActionTypeMeta.Login => AppLocalization.Service[Lang.ActionTypeLogin],
            ActionTypeMeta.Logout => AppLocalization.Service[Lang.ActionTypeLogout],
            _ => actionType ?? "",
        };
    }

    /// <summary>
    /// 动作的**一句描述**（带目标/取值）—— 交叉引用面板的动作行用它。
    /// 未知类型回落到 <see cref="HmiAction.GetDescription"/>（Core 的兜底：原样返回 Id）。
    ///
    /// <para>⚠ <b>每个分支的 <c>Format</c> 实参个数必须是字面量</b>：模板占位符个数按动作类型而异
    /// （0 / 1 / 2），写成「把实参装进数组再统一 <c>Format</c>」会让
    /// <c>LocalizedFormatCallSiteTests</c> 的 C1（实参个数 == 占位符个数）失去意义 ——
    /// 那条判据读的就是调用点上的**字面实参个数**。</para>
    /// </summary>
    internal static string Describe(HmiAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var target = action.Target ?? Placeholder;
        var value = action.Value ?? Placeholder;

        if (IsNone(action.ActionType)) return AppLocalization.Service[Lang.ActionDescNone];

        return action.ActionType switch
        {
            ActionTypeMeta.SwitchScreen =>
                AppLocalization.Service.Format(Lang.ActionDescSwitchScreen, target),
            ActionTypeMeta.SetVariable =>
                AppLocalization.Service.Format(Lang.ActionDescSetVariable, target, value),
            ActionTypeMeta.ExecuteScript =>
                AppLocalization.Service.Format(Lang.ActionDescExecuteScript, target),
            ActionTypeMeta.SetBit =>
                AppLocalization.Service.Format(Lang.ActionDescSetBit, target),
            ActionTypeMeta.ResetBit =>
                AppLocalization.Service.Format(Lang.ActionDescResetBit, target),
            ActionTypeMeta.ToggleBit =>
                AppLocalization.Service.Format(Lang.ActionDescToggleBit, target),
            ActionTypeMeta.MomentaryOn =>
                AppLocalization.Service.Format(Lang.ActionDescMomentaryOn, target),
            ActionTypeMeta.MomentaryOff =>
                AppLocalization.Service.Format(Lang.ActionDescMomentaryOff, target),
            ActionTypeMeta.ShowAlarms => AppLocalization.Service[Lang.ActionDescShowAlarms],
            ActionTypeMeta.ShowTrends => AppLocalization.Service[Lang.ActionDescShowTrends],
            ActionTypeMeta.Login => AppLocalization.Service[Lang.ActionDescLogin],
            ActionTypeMeta.Logout => AppLocalization.Service[Lang.ActionDescLogout],
            _ => action.GetDescription(),
        };
    }

    /// <summary>
    /// 交叉引用面板的一行 —— 外面再包一层「动作 {0}」（那个前缀是**中文词**，
    /// 所以它自己也要一个键，不能硬拼）。
    /// </summary>
    internal static string DescribeReference(HmiAction action) =>
        AppLocalization.Service.Format(Lang.ActionRefAction, Describe(action));
}
