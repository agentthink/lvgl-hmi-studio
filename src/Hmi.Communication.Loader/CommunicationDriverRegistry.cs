using Microsoft.Extensions.Logging;
using Hmi.Communication.Contracts;

namespace Hmi.Communication.Loader;

/// <summary>
/// 通讯驱动注册中心 —— 管理所有已加载的通讯协议驱动。
/// 与 HardwareTargetRegistry 模式完全一致。
/// </summary>
public class CommunicationDriverRegistry
{
    private readonly Dictionary<string, ICommunicationDriver> _drivers = new();
    private readonly ILogger<CommunicationDriverRegistry>? _logger;

    public CommunicationDriverRegistry(ILogger<CommunicationDriverRegistry>? logger = null)
    {
        _logger = logger;
    }

    public void LoadBuiltinDrivers(IEnumerable<ICommunicationDriver> builtinDrivers)
    {
        foreach (var driver in builtinDrivers)
        {
            Register(driver);
        }
        _logger?.LogInformation("已加载 {Count} 个内置通讯驱动", _drivers.Count);
    }

    public void Register(ICommunicationDriver driver)
    {
        var id = driver.Metadata.Id;
        if (_drivers.ContainsKey(id))
        {
            _logger?.LogWarning("通讯驱动 {Id} 已存在，将被覆盖", id);
        }
        _drivers[id] = driver;
        _logger?.LogDebug("已注册通讯驱动: {Id} ({Name})", id, driver.Metadata.Name);
    }

    public ICommunicationDriver? GetDriver(string id) =>
        _drivers.GetValueOrDefault(id);

    public IEnumerable<ICommunicationDriver> GetAllDrivers() => _drivers.Values;

    public IEnumerable<string> GetDriverIds() => _drivers.Keys;

    public IEnumerable<ICommunicationDriver> GetDriversByTransport(TransportType transport) =>
        _drivers.Values.Where(d => d.SupportedTransports.Contains(transport));
}
