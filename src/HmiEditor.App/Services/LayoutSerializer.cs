using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Services;

/// <summary>
/// 布局序列化器（R1-T2）—— 保存和恢复 Dock 布局状态。
///
/// 设计决策（ADR-R1-T2）：
/// 不直接序列化 IRootDock 对象树（Context 属性为 object/View 实例，
/// 且 Dock.Model 的 IDockSerializer 实现类 API 在 12.1 中不明确）。
/// 采用轻量方案：保存各 Dockable 的用户可调参数（Proportion、IsOpen），
/// 重建布局时由 HmiDockFactory 创建默认结构后应用覆盖。
///
/// 存储路径：用户配置目录下的 .layout.json（与 settings.json 同目录；
/// 2026-08-18 从应用基目录迁移 —— 应用目录在 Linux 部署到 /usr/lib/hmi 后
/// 可能只读，写 .layout.json 会静默失败导致布局不持久化）。
/// </summary>
public static class LayoutSerializer
{
    private static readonly JsonSerializerOptions Options = JsonOptionsDefaults.Default;

    /// <summary>布局文件名。</summary>
    private const string LayoutFileName = ".layout.json";

    /// <summary>获取布局文件的完整路径（应用数据根下的 .layout.json）。</summary>
    public static string GetLayoutFilePath()
    {
        var dataDir = AppPaths.DataRoot;
        try { Directory.CreateDirectory(dataDir); } catch { /* 目录创建失败时回退基目录 */ }
        return Path.Combine(dataDir, LayoutFileName);
    }

    /// <summary>
    /// 从布局树中提取可序列化的状态快照。
    /// 遍历所有 Dockable，记录 Id、Proportion、IsOpen；
    /// 另外记下**当时在可见布局里的工具 id**（`VisibleToolIds`）—— 重启时"**只打开保存的面板**"
    /// 靠的就是这一份（阶段 165，产品决策见登记 `#80`）。
    /// </summary>
    public static LayoutState CaptureState(IDock root)
    {
        var entries = new List<DockableState>();
        var visibleTools = new List<string>();
        CaptureRecursive(root, entries, visibleTools);
        return new LayoutState { Dockables = entries, VisibleToolIds = visibleTools };
    }

    /// <summary>递归遍历布局树，收集每个 Dockable 的状态（以及可见工具的 id）。</summary>
    private static void CaptureRecursive(IDockable dockable, List<DockableState> entries, List<string> visibleTools)
    {
        if (!string.IsNullOrEmpty(dockable.Id))
        {
            entries.Add(new DockableState
            {
                Id = dockable.Id,
                Proportion = GetProportion(dockable),
                IsOpen = GetIsOpen(dockable)
            });

            // ⚠ 只看**可见树** ⇒ 被 × 关掉（早已不在树里）、藏起来（`HiddenDockables`）、
            // 钉住（四张钉列表）的工具都**不会**进这一份 ⇒ 它们重启后**保持关闭**
            // （用户可从「面板」菜单再打开 —— 那条路阶段 162/163 已修好）。
            if (dockable is Tool) visibleTools.Add(dockable.Id!);
        }

        // 递归子 Dockable（ProportionalDock / ToolDock / DocumentDock 的 VisibleDockables）
        if (dockable is IDock dock)
        {
            if (dock.VisibleDockables != null)
            {
                foreach (var child in dock.VisibleDockables)
                {
                    CaptureRecursive(child, entries, visibleTools);
                }
            }
        }
    }

    /// <summary>将保存的状态应用到已有布局树（由 HmiDockFactory.CreateLayout 创建）。</summary>
    public static void ApplyState(IDock root, LayoutState? state)
    {
        if (state?.Dockables == null || state.Dockables.Count == 0) return;

        var index = state.Dockables.ToDictionary(d => d.Id);
        ApplyRecursive(root, index);
    }

    /// <summary>递归应用状态覆盖。</summary>
    private static void ApplyRecursive(IDockable dockable, Dictionary<string, DockableState> state)
    {
        if (!string.IsNullOrEmpty(dockable.Id) && state.TryGetValue(dockable.Id, out var saved))
        {
            SetProportion(dockable, saved.Proportion);
            SetIsOpen(dockable, saved.IsOpen);
        }

        if (dockable is IDock dock && dock.VisibleDockables != null)
        {
            // ⚠ 必须先**快照**：`SetIsOpen(dockable, false)` 会驱动 Dock 自己把那个 dockable
            // 从 `VisibleDockables` 里摘掉（`true` 会插回）—— 边枚举边改会抛
            // "Collection was modified; enumeration operation may not execute."（阶段 164 实测）。
            // 而 `IsOpen = false` 本就是盘面格式允许的值（`DockableState.IsOpen` 是 `bool`），
            // 调用点又在 `MainWindow` 构造期、**没有 try/catch** ⇒ 一个这样的 `.layout.json`
            // 就是**启动期异常**。判据：`ApplyState_WithIsOpenFalseEntry_DoesNotThrow`。
            foreach (var child in dock.VisibleDockables.ToList())
            {
                ApplyRecursive(child, state);
            }
        }
    }

    // ── Proportion / IsOpen 的反射访问 ──
    // Dock.Model 的 IDockable 接口不暴露 Proportion，但具体类型（ProportionalDock、ToolDock）有。

    private static double GetProportion(IDockable dockable)
    {
        var prop = dockable.GetType().GetProperty("Proportion");
        return prop?.GetValue(dockable) is double p ? p : 0;
    }

    private static void SetProportion(IDockable dockable, double proportion)
    {
        var prop = dockable.GetType().GetProperty("Proportion");
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(dockable, proportion);
        }
    }

    private static bool GetIsOpen(IDockable dockable)
    {
        var prop = dockable.GetType().GetProperty("IsOpen");
        return prop?.GetValue(dockable) is bool b && b;
    }

    private static void SetIsOpen(IDockable dockable, bool isOpen)
    {
        var prop = dockable.GetType().GetProperty("IsOpen");
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(dockable, isOpen);
        }
    }

    // ── 文件 IO ──

    /// <summary>保存布局状态到文件。失败时静默忽略（布局持久化为非关键功能）。</summary>
    public static void Save(LayoutState state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, Options);
            File.WriteAllText(GetLayoutFilePath(), json);
        }
        catch
        {
            // 布局持久化是 nice-to-have，不阻塞关闭流程
        }
    }

    /// <summary>从文件加载布局状态。文件不存在或解析失败时返回 null。</summary>
    public static LayoutState? Load()
    {
        try
        {
            var path = GetLayoutFilePath();
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LayoutState>(json, Options);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>布局状态快照 —— 所有 Dockable 的可调参数集合。</summary>
public class LayoutState
{
    public List<DockableState> Dockables { get; set; } = new();

    /// <summary>
    /// 保存时**在可见布局里**的工具 id —— 重启时"**只打开保存的面板**"就按这一份裁剪
    /// （阶段 165，产品决策：被 × 关掉的面板下次启动**保持关闭**，可从「面板」菜单再打开）。
    ///
    /// <para>⚠ <c>null</c> ＝ **旧盘面**（这一版之前写下的 <c>.layout.json</c> 里没有这份信息）
    /// ⇒ 语义是"**信息缺失**"，处理成**全部打开**（向后兼容），**不是**"一个都不要打开"。</para>
    /// </summary>
    public List<string>? VisibleToolIds { get; set; }
}

/// <summary>单个 Dockable 的持久化状态。</summary>
public class DockableState
{
    public string Id { get; set; } = "";
    public double Proportion { get; set; }
    public bool IsOpen { get; set; }
}
