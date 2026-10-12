using HmiEditor.App.Resources;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;

// ── 只抑制 CS8524，**绝不**抑制 CS8509（同 FindingDisplayText，2026-09-22 探针实测）：
//    · 下面的 switch 是**全覆盖 + 无默认支** ⇒ 编译器报 CS8524，举例是**未命名值**
//      （(ReferenceDescriptionKind)99 —— 枚举永远可能持有一个未声明值），与本文件要挡的事无关；
//    · 一旦**新增具名取值**而没人接文案 ⇒ 报 CS8509，举例是那个具名成员
//      ⇒ ci-gate.sh（按 ": warning " 行判红）当场红。
//    ⇒ 抑制噪声、留住信号。⚠ 替代写法（写成 `_ =>` 默认支）**刻意不用**：
//      默认支会把新取值静默吞掉，用户看到的是**假文案**（阶段 92 的 #61j 实测）。
#pragma warning disable CS8524

namespace HmiEditor.App.Services;

/// <summary>
/// **交叉引用面板「描述列」的键推导 —— 唯一实现**（闭环登记 #61n，阶段 95）。
///
/// <para><b>它挡的是什么</b>：面板的 <c>{Binding Description}</c> 以前直显 Core 拼好的中文整句
/// （<c>CrossReferenceAnalyzer</c> 的 9 条 <c>Description = ...</c> 字面量）⇒ 英文界面下那 7 条
/// 仍是中文（动作那 2 条已由阶段 94 的 <see cref="ActionTextKeys"/> 接管）。这一层就是**渲染点**：
/// Core 只给「种类 + 实参」，跨语言的那一步在 App。</para>
///
/// <para><b>为什么分派依据是 <see cref="ReferenceDescriptionKind"/> 而不是
/// <c>ReferenceType</c></b>：类型决定不了模板 —— 同一个 <c>VariableBinding</c> 下有两种措辞
/// （联动模型带方向 / 回退旧字段不带），同一个 <c>ActionTarget</c> 下也有两种
/// （<c>动作 {0}</c> / <c>{0} → 切换到 {1}</c>）。用类型分派会把这两对**粘到一份文案**上。</para>
///
/// <para><b>与 Core 那 9 条中文的关系</b>（本项最容易做错的地方，与 #61m 同形）：
/// Core 的 <c>Description</c> **必须留在原地** —— 它是字典缺键时的回落原文，
/// 也是 Core 测试与「不经界面」的消费者看到的那个值。本表是它的**界面侧孪生**，
/// 因此两份「同一件事」的一致性由 <c>ReferenceDescriptionLocalizationGuardTests</c> 用
/// **相等式**钉住（zh 渲染逐字等于 Core 回落），而不是靠约定。</para>
///
/// <para><b>阶段 96（闭环 #61o）把一行里剩下的三个枚举名也收掉了</b>：<c>ReferenceType</c>
/// 走本类的 <see cref="TypeName"/>、绑定方向走 <see cref="DirectionName"/>、控件类型走
/// <see cref="ControlTypeTextKeys"/>（后者与工具箱条目、画布默认名**共用一份**，不再各写一遍）。
/// 阶段 95 把这条边界写在注释里（「一次只放开一个变量」），阶段 96 一次收掉。</para>
/// </summary>
internal static class ReferenceTextKeys
{
    /// <summary>
    /// 一行描述 —— 交叉引用面板每行的 <c>Description</c> 列都走这里。
    ///
    /// <para>⚠ <b>每个分支的 <c>Format</c> 实参个数必须是字面量</b>：占位符个数按描述种类而异
    /// （1 / 2 个），写成「把实参装进数组再统一 <c>Format</c>」会让
    /// <c>LocalizedFormatCallSiteTests</c> 的 C1（实参个数 == 占位符个数）失去意义 ——
    /// 那条判据读的就是调用点上的**字面实参个数**。</para>
    /// </summary>
    internal static string Describe(ReferenceLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return location.DescriptionKind switch
        {
            ReferenceDescriptionKind.VariableBinding =>
                AppLocalization.Service.Format(Lang.CrossRefDescBinding,
                    location.ControlName, DirectionName(location.BindingDirection)),

            ReferenceDescriptionKind.LegacyVariableBinding =>
                AppLocalization.Service.Format(Lang.CrossRefDescLegacyBinding, location.ControlName),

            // 动作行：实参组合按动作类型而异 ⇒ 不在这里拼模板，交给 ActionTextKeys
            //（它按 action.ActionType 分派、每支的实参个数同样是字面量）。
            // ⚠ `ActionDetail` 却拿不到动作 —— 是**矛盾状态**（Core 两个站点都挂了对象）⇒
            //   回落成 Core 原文而不是编一句出来；判据从两个方向钉这对不变量。
            ReferenceDescriptionKind.ActionDetail =>
                location.Action is { } action
                    ? ActionTextKeys.DescribeReference(action)
                    : location.Description,

            ReferenceDescriptionKind.ScreenReference =>
                AppLocalization.Service.Format(Lang.CrossRefDescScreenSwitch,
                    location.ControlName, location.TargetId),

            ReferenceDescriptionKind.ScriptRun =>
                AppLocalization.Service.Format(Lang.CrossRefDescScriptRun,
                    location.ControlName, location.TargetId),

            ReferenceDescriptionKind.ScriptBinding =>
                AppLocalization.Service.Format(Lang.CrossRefDescScriptBinding,
                    location.ScriptFunctionName, location.PropertyOrEvent),

            // **诚实边界**（显式写出来，不用 `_ =>`）：枚举没赋值 ⇒ 落 Unset ⇒ 原样显示 Core 的
            // 中文回落。这不是缺陷（Core 的 ReferenceLocation 是公开类型、外部可以自己构造），
            // 而是让"忘了填"表现为**看得见的中文**、而不是一条假文案。
            ReferenceDescriptionKind.Unset => location.Description,
        };
    }

    /// <summary>
    /// 引用类型名 —— 面板行首那行蓝色小字（<see cref="ReferenceRow.ReferenceType"/>）。
    ///
    /// <para>改动前是 <c>ReferenceType.ToString()</c> ⇒ 中文界面下显示 <c>VariableBinding</c> /
    /// <c>ActionTarget</c> / <c>ScriptBinding</c> 这些英文枚举名（阶段 96，闭环 #61o）。</para>
    ///
    /// <para><b>四个成员都给键</b>，包括当前**零产出**的 <see cref="ReferenceType.ExpressionVariable"/>：
    /// 分派表按枚举成员全建，是为了让「将来开始产出」不会静默回落成英文名
    /// （与 <c>ReferenceDescriptionKind</c> 全建键同一口径；该缺口另登记在新子项里）。</para>
    /// </summary>
    internal static string TypeName(ReferenceType type) => type switch
    {
        ReferenceType.VariableBinding => AppLocalization.Service[Lang.CrossRefTypeVariableBinding],
        ReferenceType.ActionTarget => AppLocalization.Service[Lang.CrossRefTypeActionTarget],
        ReferenceType.ScriptBinding => AppLocalization.Service[Lang.CrossRefTypeScriptBinding],
        ReferenceType.ExpressionVariable => AppLocalization.Service[Lang.CrossRefTypeExpressionVariable],
    };

    /// <summary>
    /// 绑定方向名 —— 填进 <see cref="Describe"/> 里 <c>cross.ref.desc.binding</c> 的 <c>{1}</c>。
    ///
    /// <para>改动前那个实参直接传枚举值 ⇒ <c>Format</c> 的 <c>object[]</c> 装箱后 ToString()，
    /// 界面显示 <c>ReadWrite</c>（阶段 96，闭环 #61o）。⚠ 改完**实参个数仍是 2**，
    /// <c>LocalizedFormatCallSiteTests</c> 的 C1 不受影响。</para>
    ///
    /// <para><b>为什么用 <c>cross.ref.dir.*</c> 而不是复用现成的两族</b>：见 <c>Lang.cs</c> 里那 7 条
    /// 常量上方的注释（<c>prop.bind.*</c> 缺 WriteOnly；复用它们会动 <c>UnusedBaseline</c>）。</para>
    /// </summary>
    internal static string DirectionName(BindingDirection direction) => direction switch
    {
        BindingDirection.ReadOnly => AppLocalization.Service[Lang.CrossRefDirReadOnly],
        BindingDirection.WriteOnly => AppLocalization.Service[Lang.CrossRefDirWriteOnly],
        BindingDirection.ReadWrite => AppLocalization.Service[Lang.CrossRefDirReadWrite],
    };
}
