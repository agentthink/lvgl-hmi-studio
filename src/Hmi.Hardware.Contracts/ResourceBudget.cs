namespace Hmi.Hardware.Contracts;

/// <summary>预算判读结论。</summary>
public enum BudgetVerdict
{
    /// <summary>目标未声明预算（<see cref="ResourceConstraints"/> 里该轴 ≤ 0）——**不做判断**。</summary>
    Unknown,

    /// <summary>用量正常。</summary>
    Ok,

    /// <summary>用量偏高（≥ <see cref="ResourceBudget.ElevatedPercent"/>）。</summary>
    Elevated,

    /// <summary>接近上限（≥ <see cref="ResourceBudget.NearLimitPercent"/>）。</summary>
    NearLimit,

    /// <summary>超出预算（用量 &gt; 预算）——这是硬失败，不是"可能有问题"。</summary>
    Over
}

/// <summary>一个资源轴上的"实测/估算 vs 预算"读数。</summary>
public sealed record BudgetReading(
    string Axis,
    long UsedBytes,
    long BudgetBytes,
    double Percent,
    BudgetVerdict Verdict)
{
    /// <summary>剩余预算（超预算时为 0，不返回负数）。</summary>
    public long FreeBytes => Math.Max(0, BudgetBytes - UsedBytes);
}

/// <summary>
/// **资源预算判据**（登记表 §4 第 29 项"资源预算校准"）。
///
/// <para>此前只有 <see cref="ResourceConstraints"/> 里的**声明值**，没有任何一处把"实测/估算"
/// 与它相比 —— 于是 T113 的 Flash 预算写错成 16 MiB（真值 128 MiB）也不会有东西报。
/// 这里给出唯一一处判读规则；阈值不是新拍的，而是把编辑器仪表盘里既有的 70%/90% 配色阈值
/// 提出来（原先硬编码在 <c>MainWindow.UpdateResourceDashboard</c>），仪表盘与测量工具共用。</para>
/// </summary>
public static class ResourceBudget
{
    /// <summary>偏高阈值（%，与仪表盘黄色同源）。</summary>
    public const double ElevatedPercent = 70;

    /// <summary>接近上限阈值（%，与仪表盘红色同源）。</summary>
    public const double NearLimitPercent = 90;

    /// <summary>
    /// 判读一个资源轴。<paramref name="budgetBytes"/> ≤ 0 表示目标**未声明**该轴预算 ⇒
    /// <see cref="BudgetVerdict.Unknown"/>，而不是"通过"（缺依据就不下结论）。
    /// 恰好用满（100%）判为接近上限而非超预算 —— 超预算是"装不下"。
    /// </summary>
    public static BudgetReading Evaluate(string axis, long usedBytes, long budgetBytes)
    {
        if (budgetBytes <= 0)
            return new BudgetReading(axis, usedBytes, budgetBytes, 0, BudgetVerdict.Unknown);

        var percent = (double)usedBytes / budgetBytes * 100.0;
        var verdict = usedBytes > budgetBytes ? BudgetVerdict.Over
            : percent >= NearLimitPercent ? BudgetVerdict.NearLimit
            : percent >= ElevatedPercent ? BudgetVerdict.Elevated
            : BudgetVerdict.Ok;
        return new BudgetReading(axis, usedBytes, budgetBytes, percent, verdict);
    }
}
