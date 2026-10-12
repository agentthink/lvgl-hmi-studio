using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dock.Model.Core;
using System.Collections.Concurrent;

namespace HmiEditor.App;

/// <summary>
/// Dock 内容视图定位器。
///
/// [dock-blank-after-drag] 最终方案：
/// 1. 优先复用 <c>dockable.Context</c> 里的共享实例（工厂只在 Context 还不是 Control 时兜底）
/// 2. 静态注册表 —— <c>_activeViews</c> 记录每个 dockable ID 实际显示的视图，
///    <c>RebuildAllPanels</c> / <c>MainWindow</c> 的面板属性都从它取
/// 3. OnAttachedToVisualTree —— 每个 View 在附加时从 DockLayoutState 自初始化
///
/// <para><b>不变量：<see cref="Match(object?)"/> 与 <see cref="Build(object?)"/> 必须用同一条判据</b>
/// （即 <see cref="CanResolve"/>）。<c>Match</c> 返回 false 时 Avalonia **不会**调用 <c>Build</c>，
/// 所以只要两者判据不一致，「<c>Build</c> 能产出内容」的那条分支就是死代码。</para>
/// </summary>
public class ViewLocator : IDataTemplate
{
    private static readonly ConcurrentDictionary<string, Func<Control>> _factories = new();

    /// <summary>
    /// 当前每个 dockable ID 对应的活动视图实例。
    /// RebuildAllPanels 从此获取实际显示的视图（而非依赖 MainWindow 字段）。
    /// </summary>
    private static readonly ConcurrentDictionary<string, Control> _activeViews = new();

    /// <summary>
    /// 注册某个 dockable ID 的兜底工厂。
    ///
    /// <para>键空间不变量：调用方必须传**该 dockable 自己的 <c>Id</c>**，
    /// 不要另写一份字面量（<c>HmiDockFactory</c> 已统一传 <c>xTool.Id</c>）。
    /// 两套手写键空间当前恰好一致，但没有任何编译期约束阻止它们分叉，
    /// 而分叉的表现是"该面板**静默**回退到工厂、或干脆空白"，不会报错。</para>
    /// </summary>
    public static void RegisterFactory(string dockableId, Func<Control> factory)
    {
        _factories[dockableId] = factory;
    }

    /// <summary>已注册工厂的 dockable ID（升序）。守卫用它和真实 layout 的 ID 集合做**双向**比对。</summary>
    public static IReadOnlyList<string> RegisteredFactoryIds =>
        _factories.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();

    /// <summary>
    /// 获取当前 dockable ID 对应的活动视图。
    /// 用于 RebuildAllPanels 等需要操作实际显示视图的场景。
    /// </summary>
    public static Control? GetActiveView(string dockableId)
    {
        return _activeViews.TryGetValue(dockableId, out var control) ? control : null;
    }

    /// <summary>
    /// 能否为该 dockable 产出内容视图 —— <see cref="Match(object?)"/> 与 <see cref="Build(object?)"/> 的
    /// **唯一**判据。
    ///
    /// <para><b>为什么单独抽出来</b>：<c>Match</c> 曾经写成
    /// <c>data is IDockable { Context: Control }</c>，而"<c>Context</c> 是 <c>Control</c>"恰恰是
    /// <c>Build</c> 里**工厂回退分支的前置条件**（该分支的存在意义就是"Context 还不是 Control 时创建它"）。
    /// 于是 <c>Match</c> 返回 false ⇒ Avalonia 不调 <c>Build</c> ⇒ 12 处 <c>RegisterFactory</c>
    /// 全部是**按构造不可达的死代码**；一旦 Dock 重组把 <c>Context</c> 抹掉
    /// （归档问题单 dock-blank-after-drag 的假设 D：重组后 <c>Context</c> 变化导致 <c>Match</c> 返回 false），
    /// 该面板**永久空白且没有任何回退**。</para>
    /// </summary>
    public static bool CanResolve(IDockable dockable) =>
        dockable.Context is Control || _factories.ContainsKey(dockable.Id);

    public Control? Build(object? data)
    {
        if (data is not IDockable dockable) return null;
        if (!CanResolve(dockable)) return null;   // 与 Match 同一判据，见 CanResolve 的注释

        // [dock-blank-after-drag] 优先复用 dockable.Context 中的共享实例。
        // HmiDockFactory 在 CreateLayout 时已将各面板的共享 View 实例赋给 Tool.Context
        // （如 _toolboxView / _canvasView / _propertyPanelView），这些实例内部持有数据字段，
        // 且 MainWindow 的备选字段（_toolboxPanel 等）也指向同一实例。
        // 此前"工厂优先"策略会为每个 dockable 反复 new 新实例，导致：
        //   1) _activeViews 里的实例与 HmiDockFactory/MainWindow 持有的共享实例不一致，
        //      事件路由（GetActiveView 回退到字段）操作的是不同对象；
        //   2) 新实例未持有任何数据，Dock 重组后内容空白。
        var instance = dockable.Context as Control ?? _factories[dockable.Id]();

        // 注册为活动视图
        _activeViews[dockable.Id] = instance;
        dockable.Context = instance;

        return instance;
    }

    public bool Match(object? data)
    {
        return data is IDockable dockable && CanResolve(dockable);
    }
}
