using System;
using System.Collections.Generic;
using System.Reflection;
using HmiEditor.App;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **测试进程卫生**：把静态总线 <see cref="DockLayoutState"/> 的**订阅**与**共享数据**都清回基线
/// （登记 `#72`：订阅那一半是阶段 134，数据那一半是阶段 139）。
///
/// <para><b>订阅那一半（<see cref="Silence"/>）：</b>产品侧的主窗口在构造函数里把**一大批**处理器订到
/// 静态总线上（<c>Wiring.DockAggregate.cs</c> 等），而**从不退订** —— 对产品是**对的**（主窗口活到
/// 进程结束）。但在**测试进程**里：先跑的主窗口类用例把窗口关了、**订阅还在** ⇒ 之后任何"抬总线事件"
/// 的用例都会打到**已关闭**的窗口上（实测 <c>InvalidOperationException: Cannot show a window with a
/// closed owner.</c>）⇒ 第五层那几条"处理器只抬事件、但构造函数把事件转发到总线"的腿
/// **红不红取决于别的测试跑没跑**。清掉陈旧订阅之后，那些腿就变成**可判的**。</para>
///
/// <para>🩸 <b>更正记录（阶段 139 收尾时做的，写在这里免得下一个人重走）：</b>
/// 本阶段中途量到过"清共享数据会让全量跑**崩/挂**"，据此一度把政策收窄成"只清 <c>Templates</c>"、
/// 并把它归因到 <c>HmiDockFactory</c> 的两处静态读（当时还开了登记 `#76`）。
/// <b>收尾复测推翻了这两条</b>：8 个字段全清 ＋ 宿主恢复字面断言，**全量连跑 3 次全绿**（624 / 31–33 s）。
/// 当时的 6 连败是**进程环境被前面几趟被 kill 掉的测试跑坏掉**造成的（同一批 session 里先有
/// 600 s 超时被杀的 testhost）—— 即"看起来 0/6 vs 对照 4/4 很像因果，其实是环境"。
/// ⇒ 教训（已记进登记表 §61）：**超时/挂死类的观测在结论之前必须换一个干净进程状态复测**；
/// 而 <c>#76</c> 那两处（<c>LayerPanelView.Refresh</c> 与 <c>ProjectExplorerView._currentScreen</c>）
/// 复看都是**判了空**的，不是缺陷。</para>
///
/// <para><b>数据那一半（<see cref="ResetSharedData"/>，阶段 139）：</b>同一族缺陷的**第二个形态**
/// —— 不是"订阅还在"，而是"**数据还在**"。<c>MainWindow.UpdateDockLayoutState()</c> 会往
/// <c>CurrentProject</c> / <c>Templates</c> / <c>ProjectDirectory</c> 等 **8 个**静态属性里写
/// **那一个主窗口的**对象，而它们**永不清空** ⇒ 下一个测试里读它们的东西会拿到**上一个工程**的残留。
/// 实测症状（阶段 133）：<c>FaceplateLibraryView</c> 在 <c>OnAttachedToVisualTree</c> 里
/// <c>if (DockLayoutState.Templates != null) LoadTemplates(…)</c> ⇒ 宿主夹具喂的两条模板在**全量跑**
/// 里被**别人的**模板覆盖（全量 4 条、单跑 2 条）⇒ 那一版只能把断言退成「载荷随选中」的一致性探针。
/// 本阶段把它清回基线之后，那个宿主才敢重新断言**字面模板名**（见 <c>HostButtonEffectUiTests</c>）。</para>
///
/// <para><b>为什么用反射而不是给产品加一个 <c>ResetForTests()</c>：</b>后者要在产品代码里把
/// **45 个**事件逐个赋 null、把 **8 个**数据属性逐个赋默认值 —— 那就是"抄一份清单"，加一个新成员
/// 忘了抄就静默漏掉；这里按"**静态字段**"枚举：委托型归 <see cref="Silence"/>，
/// 其余**必须**在 <see cref="SharedDataPolicy"/> 里被分类（**没分类就抛**，见
/// <see cref="ResetSharedData"/>）⇒ 新增成员**不会**静默漏掉，但**会**逼人来分类。</para>
///
/// <para>🩸 <b>它为什么安全</b>（这条不是"应该没事"，是有机制的）：本类只被
/// <see cref="HostButtonEffectUiTests"/> 与 <see cref="StaticBusHygieneTests"/> 调用，而两个类都挂在
/// <c>[Collection("Language-global")]</c>（<c>DisableParallelization = true</c>）⇒ 它们跑的时候
/// **没有任何别的集合在跑** ⇒ 清总线不会打断别处的并行用例。</para>
/// </summary>
internal static class StaticBusHygiene
{
    /// <summary>共享数据字段的处置：<c>Reset</c> = 清回默认值；<c>Keep</c> = **有意共享**、不许清。</summary>
    internal enum SharedDataAction
    {
        /// <summary>清回该类型的默认值（引用类型 / <c>Nullable&lt;T&gt;</c> ⇒ <c>null</c>）。</summary>
        Reset,

        /// <summary>有意共享、**必须**保持不动（当前一处都没有 —— 留这一档是为了让"不许清"也能被写清理由）。</summary>
        Keep,
    }

    /// <summary>
    /// <b>共享数据字段分类表</b>（阶段 139）：每个**非委托**的静态字段都必须在表里，否则
    /// <see cref="ResetSharedData"/> **抛**。键是**属性名**（不是 <c>&lt;X&gt;k__BackingField</c>）。
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, (SharedDataAction Action, string Reason)> SharedDataPolicy =
        new Dictionary<string, (SharedDataAction, string)>(StringComparer.Ordinal)
        {
            // 8 个字段**全部 Reset**（阶段 139 最终形态）：宿主跑前回到"没有工程/没有屏/没有选中"
            // 的基线 —— 这正是产品**打开工程之前**的状态，每个视图都必须在它下面能构造。
            ["CurrentProject"] = (SharedDataAction.Reset,
                "`MainWindow.UpdateDockLayoutState()` 写的是**那一个主窗口的** `HmiProject` ⇒ 不 clearing 的下一个宿主读到的是上一个工程"),
            ["CurrentScreen"] = (SharedDataAction.Reset,
                "同 `CurrentProject`：切屏时写，测试之间没有任何人清"),
            ["SelectedControl"] = (SharedDataAction.Reset,
                "最典型的「上一次选中」残留：主窗口选中变化时写，测试跑完就留在那里"),
            ["AssetLibrary"] = (SharedDataAction.Reset,
                "同族：主窗口构造期把**它自己的**资源库挂上去（`PropertyPanelView.Assets` / 画布渲染器都读它）"),
            ["ResourceLibrary"] = (SharedDataAction.Reset,
                "同族：`HmiResourceLibrary` 也是主窗口的对象"),
            ["Templates"] = (SharedDataAction.Reset,
                "🩸 阶段 133 实测的污染源：`FaceplateLibraryView` 在 `OnAttachedToVisualTree` 里从它自初始化（全量 4 条 / 单跑 2 条）"),
            ["ProjectDirectory"] = (SharedDataAction.Reset,
                "相对路径解析用的目录：上个工程开着就留在那儿，`AssetItem.FilePath` 会被解析到别处"),
            ["ActiveDrawTool"] = (SharedDataAction.Reset,
                "绘图工具档（`SetDrawTool` 写）：不清的话下一个宿主一进来就是「正在画矩形」的状态"),
        };

    /// <summary>清空总线上**全部**委托型静态字段（＝ 全部事件的后备字段）。返回清掉的字段数。</summary>
    internal static int Silence()
    {
        var cleared = 0;
        foreach (var field in StaticFields())
        {
            if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
            if (field.GetValue(null) is null) continue;   // 本来就是空的，不用动

            field.SetValue(null, null);
            cleared++;
        }

        return cleared;
    }

    /// <summary>
    /// 把**全部非委托**的静态字段清回基线（见 <see cref="SharedDataPolicy"/>）。返回**真的变了**的字段数。
    ///
    /// <para><b>fail-closed</b>：遇到一个**没分类**的静态字段就**抛**，而不是静默跳过 ——
    /// 静默跳过正是 `#72` 本身的形状（"没人清，也没人知道没清"）。这条抛同时让
    /// <see cref="StaticBusHygieneTests"/> 能在**合成语料**上判它（传一张空表进去）。</para>
    ///
    /// <para><paramref name="policy"/> 只在自测里传（默认用 <see cref="SharedDataPolicy"/>）。</para>
    /// </summary>
    internal static int ResetSharedData(
        IReadOnlyDictionary<string, (SharedDataAction Action, string Reason)>? policy = null)
    {
        policy ??= SharedDataPolicy;
        var reset = 0;

        foreach (var field in StaticFields())
        {
            if (typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;   // 订阅那一半归 Silence()

            var name = LogicalName(field);
            if (!policy.TryGetValue(name, out var rule))
                throw new InvalidOperationException(
                    $"`DockLayoutState.{name}` 是一个**没被分类**的静态字段 —— `#72` 的数据那一半要求"
                    + "每个静态字段要么是委托（归 Silence()）、要么在 StaticBusHygiene.SharedDataPolicy 里"
                    + "写清 Reset/Keep 与理由。静默跳过它正是这个缺陷本身的形状。");

            if (rule.Action == SharedDataAction.Keep) continue;

            if (field.IsLiteral || field.IsInitOnly)
                throw new InvalidOperationException(
                    $"`DockLayoutState.{name}` 是 const/readonly，**清不了** ⇒ 它必须在 SharedDataPolicy 里"
                    + "标成 Keep 并写明理由（别再写 Reset）。");

            var baseline = field.FieldType.IsValueType
                ? Activator.CreateInstance(field.FieldType)
                : null;
            var current = field.GetValue(null);
            if (Equals(current, baseline)) continue;    // 本来就在基线上，不用动（与 Silence() 同口径）

            field.SetValue(null, baseline);
            reset++;
        }

        return reset;
    }

    /// <summary><see cref="DockLayoutState"/> 的**全部**静态字段（含编译器生成的属性后备字段）。</summary>
    internal static IEnumerable<FieldInfo> StaticFields() =>
        typeof(DockLayoutState).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    /// <summary>
    /// 把编译器生成的后备字段名（<c>&lt;CurrentProject&gt;k__BackingField</c>）还原成**属性名**；
    /// 普通字段原样返回。分类表用属性名 —— 写表的人读代码时看到的就是属性名。
    /// </summary>
    internal static string LogicalName(FieldInfo field)
    {
        const string suffix = ">k__BackingField";
        var n = field.Name;
        return n.StartsWith('<') && n.EndsWith(suffix, StringComparison.Ordinal)
            ? n[1..^(suffix.Length)]
            : n;
    }
}
