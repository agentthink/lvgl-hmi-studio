namespace HmiEditor.Simulator;

/// <summary>
/// **模拟器**失败诊断码 —— 单一来源，值**不带**键前缀（前缀由 App 侧
/// <c>ExternalText.SimulatorIssueKey</c> 拼成 <c>sim.issue.</c>）。
///
/// <para><b>为什么与异常类分开</b>：机制守卫 <b>M15</b>（<c>LocalizationKeyReachabilityGuardTests</c>）
/// 是**反射**本类的 <c>public const string</c> 字段来枚举"声明了哪些码"的
/// （与 <c>SvgParseIssueCodes</c> / <c>CsvImportIssueCodes</c> / <c>ExpressionSyntaxIssueCodes</c>
/// 同形）。中文回落原文那个常量若也住在这一族里，就会**多出一条假码** ——
/// 它的键不存在，机制面因此虚高。⇒ 回落留在 <see cref="SimulatorNotConnectedException"/>
/// 内部（<c>private const</c>）。</para>
///
/// <para><b>为什么码值放产出方项目</b>：码由**产出方**声明是本仓库既定约定
/// （App 侧只负责"码 → 键"推导）。本族的产出方在 <c>HmiEditor.Simulator</c> 里，
/// 与 <c>HmiEditor.Device</c> 的 <c>DeviceChannelException</c> 同一处法。</para>
///
/// <para><b>为什么只有一码</b>：实测 <c>CommunicationSimulator</c> 只有两个抛点
/// （<c>ReadRegister</c> / <c>WriteRegister</c>）、语义也只有一个（未连接）、无实参。
/// ⚠ 若将来出现**第二档**（例如"地址越界"），往本类加码即可 —— 别把多个语义塞进同一个码。</para>
/// </summary>
public static class SimulatorIssueCodes
{
    /// <summary>模拟器未连接（<c>IsConnected == false</c> 时读写寄存器）。</summary>
    public const string NOT_CONNECTED = "NOT_CONNECTED";
}
