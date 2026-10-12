using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Services;

/// <summary>
/// 窗口几何持久化服务 —— 保存/恢复主窗口与子窗口的位置、大小、最大化状态。
///
/// 数据存储：用户配置目录下 windows.json（与 settings.json 同目录）。
/// 以窗口类型名（Type.FullName）为键分别保存，避免各窗口互相覆盖。
/// 布局持久化非关键功能：任何 IO/序列化失败均静默忽略，不影响启动与关闭。
/// </summary>
public static class WindowGeometryService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const string FileName = "windows.json";

    /// <summary>获取持久化文件路径（应用数据根）。</summary>
    private static string GetFilePath()
    {
        var dataDir = AppPaths.DataRoot;
        try { Directory.CreateDirectory(dataDir); } catch { }
        return Path.Combine(dataDir, FileName);
    }

    /// <summary>按窗口类型名读取保存的几何；无记录返回 null。</summary>
    public static WindowGeometry? Load(string windowTypeName)
    {
        try
        {
            var all = LoadAll();
            return all.TryGetValue(windowTypeName, out var geo) ? geo : null;
        }
        catch { return null; }
    }

    /// <summary>保存指定窗口的几何。</summary>
    public static void Save(string windowTypeName, WindowGeometry geometry)
    {
        try
        {
            var all = LoadAll();
            all[windowTypeName] = geometry;
            var json = JsonSerializer.Serialize(all, Options);
            File.WriteAllText(GetFilePath(), json);
        }
        catch { /* 非关键功能 */ }
    }

    private static Dictionary<string, WindowGeometry> LoadAll()
    {
        try
        {
            var path = GetFilePath();
            if (!File.Exists(path)) return new Dictionary<string, WindowGeometry>();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, WindowGeometry>>(json, Options)
                   ?? new Dictionary<string, WindowGeometry>();
        }
        catch { return new Dictionary<string, WindowGeometry>(); }
    }
}

/// <summary>窗口几何快照（位置 X/Y、大小 Width/Height、窗口状态）。</summary>
public class WindowGeometry
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string WindowState { get; set; } = "Normal";
}
