using System.Text.Json;
using Hmi.Hardware.Contracts;
using Microsoft.Extensions.Logging;

namespace Hmi.Hardware.Loader;

/// <summary>
/// 硬件目标注册中心 —— 管理所有已加载的硬件目标插件。
/// 支持三种加载模式：
/// - Tier 1: 内置插件（LoadBuiltinTargets）
/// - Tier 2: DLL 热插拔（LoadPluginTargets）
/// - Tier 3: JSON 配置（LoadConfigTargets）
/// </summary>
public class HardwareTargetRegistry
{
    private readonly Dictionary<string, IHardwareTarget> _targets = new();
    private readonly ILogger<HardwareTargetRegistry>? _logger;

    /// <summary>已加载的 PluginLoadContext 列表，用于卸载跟踪。</summary>
    private readonly List<PluginLoadContext> _pluginContexts = new();

    public HardwareTargetRegistry(ILogger<HardwareTargetRegistry>? logger = null)
    {
        _logger = logger;
    }

    // ── Tier 1: 内置插件 ──

    /// <summary>
    /// 加载内置硬件目标插件。
    /// </summary>
    public void LoadBuiltinTargets(IEnumerable<IHardwareTarget> builtinTargets)
    {
        foreach (var target in builtinTargets)
        {
            Register(target);
        }
        _logger?.LogInformation("已加载 {Count} 个内置硬件目标", _targets.Count);
    }

    // ── Tier 2: DLL 热插拔 ──

    /// <summary>
    /// 扫描指定目录中的 *.dll 文件，使用 PluginLoadContext 反射加载 IHardwareTarget 实现。
    /// </summary>
    /// <param name="pluginDirectory">插件 DLL 所在目录。</param>
    /// <returns>成功加载的插件数量。</returns>
    public int LoadPluginTargets(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            _logger?.LogWarning("插件目录不存在: {Path}", pluginDirectory);
            return 0;
        }

        var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll");
        var loadedCount = 0;

        foreach (var dllPath in dllFiles)
        {
            try
            {
                var context = new PluginLoadContext(dllPath);
                var assembly = context.LoadFromAssemblyPath(dllPath);
                var implementations = context.FindImplementations<IHardwareTarget>();

                foreach (var implType in implementations)
                {
                    if (Activator.CreateInstance(implType) is IHardwareTarget target)
                    {
                        Register(target);
                        _pluginContexts.Add(context);
                        loadedCount++;
                        _logger?.LogDebug("DLL 插件加载: {Type} (来自 {Dll})", implType.Name, Path.GetFileName(dllPath));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("加载 DLL 失败 {Dll}: {Message}", Path.GetFileName(dllPath), ex.Message);
            }
        }

        _logger?.LogInformation("DLL 插件加载完成: {Count} 个新目标", loadedCount);
        return loadedCount;
    }

    // ── Tier 3: JSON 配置 ──

    /// <summary>
    /// 扫描指定目录中的 *.target.json 文件，反序列化为 TargetConfigJson，通过 ConfigDrivenTarget 适配器注册。
    /// </summary>
    /// <param name="configDirectory">JSON 配置文件所在目录。</param>
    /// <returns>成功加载的配置目标数量。</returns>
    public int LoadConfigTargets(string configDirectory)
    {
        if (!Directory.Exists(configDirectory))
        {
            _logger?.LogWarning("配置目录不存在: {Path}", configDirectory);
            return 0;
        }

        var jsonFiles = Directory.GetFiles(configDirectory, "*.target.json");
        return LoadTargetFiles(jsonFiles, "JSON 配置");
    }

    /// <summary>
    /// 扫描固件根目录，加载每套固件的型号定义文件（firmware/*/firmware.json）。
    /// 型号与固件一一对应：每套固件目录下的 firmware.json 定义该固件的固定型号。
    /// </summary>
    /// <param name="firmwareRoot">固件根目录（firmware/）。</param>
    /// <returns>成功加载的固件型号数量。</returns>
    public int LoadFirmwareTargets(string firmwareRoot)
    {
        if (!Directory.Exists(firmwareRoot))
        {
            _logger?.LogWarning("固件目录不存在: {Path}", firmwareRoot);
            return 0;
        }

        var jsonFiles = Directory.GetDirectories(firmwareRoot)
            .Select(dir => Path.Combine(dir, "firmware.json"))
            .Where(File.Exists)
            .ToArray();
        return LoadTargetFiles(jsonFiles, "固件型号");
    }

    /// <summary>
    /// 公共 JSON 目标加载逻辑：反序列化每个文件并注册。
    /// </summary>
    private int LoadTargetFiles(string[] jsonFiles, string kind)
    {
        var loadedCount = 0;

        foreach (var jsonPath in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                var config = JsonSerializer.Deserialize<TargetConfigJson>(json);
                if (config == null || string.IsNullOrEmpty(config.Id))
                {
                    _logger?.LogWarning("{Kind}无效: {File}", kind, Path.GetFileName(jsonPath));
                    continue;
                }

                var target = new ConfigDrivenTarget(config);
                Register(target);
                loadedCount++;
                _logger?.LogDebug("{Kind}加载: {Id} ({Name})", kind, config.Id, config.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("加载 {Kind}失败 {File}: {Message}",
                    kind, Path.GetFileName(jsonPath), ex.Message);
            }
        }

        _logger?.LogInformation("{Kind}加载完成: {Count} 个新目标", kind, loadedCount);
        return loadedCount;
    }

    // ── 注册与查询 ──

    /// <summary>
    /// 注册单个硬件目标。
    /// </summary>
    public void Register(IHardwareTarget target)
    {
        var id = target.Metadata.Id;
        if (_targets.ContainsKey(id))
        {
            _logger?.LogWarning("硬件目标 {Id} 已存在，将被覆盖", id);
        }
        _targets[id] = target;
        _logger?.LogDebug("已注册硬件目标: {Id} ({Name})", id, target.Metadata.Name);
    }

    public IHardwareTarget? GetTarget(string id) =>
        _targets.GetValueOrDefault(id);

    public IEnumerable<IHardwareTarget> GetAllTargets() => _targets.Values;

    public IEnumerable<string> GetTargetIds() => _targets.Keys;

    // ── 卸载 ──

    /// <summary>
    /// 卸载所有通过 PluginLoadContext 加载的 DLL 插件。
    /// 调用后需等待 GC.Collect() 回收程序集内存。
    /// </summary>
    public int UnloadPluginContexts()
    {
        var count = _pluginContexts.Count;
        foreach (var ctx in _pluginContexts)
        {
            try
            {
                ctx.UnloadPlugin();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("卸载 PluginLoadContext 失败: {Message}", ex.Message);
            }
        }
        _pluginContexts.Clear();
        _logger?.LogInformation("已卸载 {Count} 个插件上下文", count);
        return count;
    }
}
