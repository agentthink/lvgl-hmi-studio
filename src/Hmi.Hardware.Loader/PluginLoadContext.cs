using System.Reflection;
using System.Runtime.Loader;

namespace Hmi.Hardware.Loader;

/// <summary>
/// 可卸载的插件加载上下文 —— 支持 DLL 插件的热加载和卸载。
/// 使用 isCollectible: true 启用程序集卸载能力。
/// 契约程序集（Hmi.Hardware.Contracts）共享主上下文版本，确保接口类型一致。
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 契约程序集共享主上下文版本，确保接口类型一致
        if (assemblyName.Name == "Hmi.Hardware.Contracts")
            return null;

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path != null ? LoadFromAssemblyPath(path) : null;
    }

    /// <summary>
    /// 从当前上下文加载的程序集中发现指定接口的实现类型。
    /// </summary>
    public IReadOnlyList<Type> FindImplementations<TInterface>()
    {
        var interfaceType = typeof(TInterface);
        var results = new List<Type>();

        foreach (var assembly in Assemblies)
        {
            if (assembly.IsDynamic) continue;
            try
            {
                foreach (var type in assembly.GetExportedTypes())
                {
                    if (type.IsClass && !type.IsAbstract && interfaceType.IsAssignableFrom(type))
                    {
                        results.Add(type);
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // 跳过无法反射的类型
            }
        }

        return results;
    }

    /// <summary>
    /// 卸载插件上下文，释放所有相关程序集。
    /// </summary>
    public void UnloadPlugin()
    {
        Unload();
    }
}
