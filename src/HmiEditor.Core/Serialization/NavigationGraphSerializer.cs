using System.IO;
using System.Text.Json;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.Serialization;

/// <summary>
/// 导航流图的 JSON 序列化器 —— 独立 .nav.json 存储，不侵入 .hmi.json。
/// </summary>
public static class NavigationGraphSerializer
{
    private static readonly JsonSerializerOptions Options = JsonOptionsDefaults.Default;

    /// <summary>将导航图序列化为带缩进的 JSON 字符串。</summary>
    public static string Serialize(NavigationGraph graph)
    {
        return JsonSerializer.Serialize(graph, Options);
    }

    /// <summary>从 JSON 字符串反序列化导航图。失败返回 null。</summary>
    public static NavigationGraph? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<NavigationGraph>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentNullException)
        {
            return null;
        }
    }

    /// <summary>将导航图写入 .nav.json 文件。</summary>
    public static void SaveToFile(NavigationGraph graph, string path)
    {
        var json = Serialize(graph);
        File.WriteAllText(path, json);
    }

    /// <summary>从文件加载导航图。失败返回 null。</summary>
    public static NavigationGraph? LoadFromFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return Deserialize(json);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
