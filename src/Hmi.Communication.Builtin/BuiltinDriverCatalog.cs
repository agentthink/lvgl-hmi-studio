using Hmi.Communication.Contracts;

namespace Hmi.Communication.Builtin;

/// <summary>
/// 内置（设计期）通讯驱动清单的**唯一来源**。
///
/// <para>此前这份清单只以**内联 <c>new</c> 数组**的形式存在于
/// <c>CommunicationConfigView</c> 的构造函数里，而 Agent 工具的 JSON Schema
/// （<c>AgentToolFactory.CreateConnectionTool</c>）另外手抄了一份驱动名清单 ——
/// 两份不一致，且**没有任何测试或报错会提醒你**：schema 里写的是 <c>opcua</c>，
/// 而真实驱动 Id 是 <c>opc-ua</c>（<c>NormalizeDriver</c> 只把下划线换成连字符，
/// 补不出中间那个连字符）⇒ 模型照着 schema 生成 <c>opcua</c> 时，
/// <c>create_connection</c> 会静默建出一个**匹配不到任何驱动**的连接。</para>
/// </summary>
public static class BuiltinDriverCatalog
{
    /// <summary>新建全部内置设计期驱动实例（每次调用返回全新实例）。</summary>
    public static IReadOnlyList<ICommunicationDriver> CreateAll() =>
    [
        new ModbusRtuDriver(),
        new ModbusTcpDriver(),
        new OpcUaDriver(),
        new MqttDriver(),
    ];

    /// <summary>
    /// 驱动 Id 清单 —— 顺序 = <see cref="CreateAll"/> 的声明顺序（确定性，可安全用于 schema）。
    /// </summary>
    public static IReadOnlyList<string> Ids { get; } =
        CreateAll().Select(d => d.Metadata.Id).ToArray();
}
