namespace HmiEditor.Simulator;

/// <summary>
/// **模拟器未连接**（登记 <b>#61d</b>，阶段 97）——
/// <c>CommunicationSimulator.ReadRegister</c> / <c>WriteRegister</c> 在 <c>IsConnected == false</c>
/// 时抛出。改动前抛的是 **裸 <c>InvalidOperationException("Simulator not connected")</c>**：
/// </summary>
/// <list type="bullet">
///   <item>消息是**英文硬编码** ⇒ 中文界面下 <c>模拟器视图</c>的日志里混进一句英文
///     （<c>读取失败: Simulator not connected</c>）；</item>
///   <item><c>InvalidOperationException</c> 没有码 ⇒ <c>ExternalText.FailureReason</c> 的四支分叉
///     一支都接不住它（<c>SystemFailureCodes</c> 的表里也没有它，那是**实测过才加**的一张表）
///     ⇒ 只能落第 ④ 支回落原文。换成带码异常，码这条路才通到界面。</item>
/// </list>
///
/// <para><b>为什么只有一档、没有码表 record</b>：实测该文件里<b>只有两个抛点</b>
/// （<c>ReadRegister</c> / <c>WriteRegister</c>，`grep "Simulator not connected"` 命中 2 处），
/// 语义也只有一个（未连接）、无实参 ⇒ 引入 `码 + 实参 + 回落` 三段式是空壳。
/// ⚠ 若将来出现**第二档**（例如"地址越界"），照 <c>HmiEditor.Core.Services.UserAdminIssue</c> 族
/// 同形拆出 <c>SimulatorIssue</c> record 与 <c>SimulatorIssueCodes</c>，
/// 别把多个语义塞进本类型的 <see cref="Code"/>。</para>
///
/// <para><b><see cref="Exception.Message"/> 用中文回落</b>：它是字典缺键时的显示内容
/// （= App 侧 <c>sim.issue.not_connected</c> 的回落），也是日志里的可读信息 ——
/// 保留英文原文或换成码名都是退步。</para>
/// </summary>
public sealed class SimulatorNotConnectedException : Exception
{
    /// <summary>中文回落原文（字典缺键时原样显示）。**刻意是 <c>private</c>**：
    /// 诊断码在 <see cref="SimulatorIssueCodes"/> 里，而 M15 机制守卫是**反射**扫那个类的
    /// <c>public const string</c> —— 两类常量同住一个类会让"码表"多出一条**假码**（它的键不存在 ⇒ 面虚高）。</summary>
    private const string Fallback = "模拟器未连接";

    public SimulatorNotConnectedException()
        : base(Fallback)
    {
    }

    /// <summary>本异常的诊断码 —— App 侧渲染时查字典的唯一依据。</summary>
    public string Code => SimulatorIssueCodes.NOT_CONNECTED;
}
