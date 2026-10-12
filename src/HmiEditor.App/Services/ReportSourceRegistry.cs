using System;
using System.Collections.Generic;
using HmiEditor.Core.Reporting;

namespace HmiEditor.App.Services;

/// <summary>
/// **运行期数据源的注册表**（阶段 185，登记 `#87` 的第二片）：谁手里有数据，谁就把"取一张表"的委托
/// 注册进来；定时/自动导出（186）只认这张表，不去认识任何面板。
///
/// <para><b>为什么必须有它</b>：三个数据源的**归属不同** —— 审计与报警挂在组合根
/// （<c>MainWindow</c> 的 <c>_authService</c> / <c>_alarmEngine</c>），而**历史采样
/// （<c>DataLogger</c>）是 <c>TrendConfigView</c> 开记录时 <c>new</c> 出来、随对话框生死的**
/// ⇒ 应用层根本拿不到它。让"定时导出"去认识三个面板是最坏的做法（面板能关、能开两个），
/// 所以中间加一层注册表：<b>面板在，就有源；面板走了，那个源就"缺"</b>，由导出侧如实记进
/// <c>index.txt</c> 的 skipped 段，而不是假装导过了。</para>
///
/// <para><b>线程</b>：注册/注销发生在 UI 线程（面板打开/关闭），而取数发生在定时器线程
/// ⇒ 一律<b>加锁快照</b>：<see cref="TryBuild"/> 先在锁里取出委托、再到锁外调用
/// （取数可能很慢，不能占着锁）。</para>
///
/// <para><b>键必须来自 <see cref="ReportSourceKeys"/></b>：未知键直接抛 —— 它会一路变成
/// 报表包文件名与偏好存储键，"写错一个字母"在这里就要红，而不是等用户发现某个源永远没导。</para>
/// </summary>
public sealed class ReportSourceRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<string, Func<ReportTable>> _providers = new(StringComparer.Ordinal);

    /// <summary>注册（或覆盖）某个数据源的取数委托。</summary>
    public void Register(string sourceKey, Func<ReportTable> provider)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceKey);
        ArgumentNullException.ThrowIfNull(provider);
        if (!ReportSourceKeys.IsKnown(sourceKey))
            throw new ArgumentException($"未知的数据源键：{sourceKey}", nameof(sourceKey));

        lock (_lock) _providers[sourceKey] = provider;
    }

    /// <summary>注销（没注册过就是空操作 —— 关闭路径上重复注销不该抛）。</summary>
    public void Unregister(string sourceKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceKey);

        lock (_lock) _providers.Remove(sourceKey);
    }

    /// <summary>注销全部（主窗口关闭时收口用）。</summary>
    public void Clear()
    {
        lock (_lock) _providers.Clear();
    }

    /// <summary>当前**可用**的数据源（顺序同 <see cref="ReportSourceKeys.All"/>，判据与界面都靠它稳定）。</summary>
    public IReadOnlyList<string> Registered
    {
        get
        {
            lock (_lock) return InOrder(_providers.ContainsKey);
        }
    }

    /// <summary>
    /// 已知但**当前不可用**的数据源 —— 自动导出把它写进索引的 skipped 段，
    /// 让"这次少导了什么"在产物里看得见（`#87` 的核心要求之一）。
    /// </summary>
    public IReadOnlyList<string> Missing
    {
        get
        {
            lock (_lock) return InOrder(key => !_providers.ContainsKey(key));
        }
    }

    /// <summary>取某个数据源的表；**没注册 ⇒ <c>null</c>**（不抛：这是正常状态，不是错误）。</summary>
    public ReportTable? TryBuild(string sourceKey)
    {
        Func<ReportTable>? provider;
        lock (_lock)
        {
            if (!_providers.TryGetValue(sourceKey, out provider)) return null;
        }

        return provider();   // 锁外调用：取数可能很慢（几千行采样）
    }

    private static IReadOnlyList<string> InOrder(Func<string, bool> predicate)
    {
        var result = new List<string>(ReportSourceKeys.All.Length);
        foreach (var key in ReportSourceKeys.All)
            if (predicate(key)) result.Add(key);

        return result;
    }
}
