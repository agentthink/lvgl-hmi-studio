using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using System.IO;
using System.Text.Json;

namespace HmiEditor.App.Services;

/// <summary>
/// HMI 图库资源持久化服务 —— 将内置预置 + 用户自定义资源保存到应用数据目录，
/// 实现跨项目共享、版本记录和持续迭代。
///
/// 版本控制机制：
///   - 每次保存写入带时间戳的历史快照（.hmi-reslib.<yyyyMMddHHmmss>.json）
///   - 保留最近 N 个快照（默认 10 个），自动清理旧版本
///   - 提供 GetHistory() 用于查看版本历史和回滚
/// </summary>
public sealed class ResourceLibraryStore
{
    private const int MaxSnapshots = 10;
    private readonly string _libraryPath;
    private readonly string _historyDir;

    /// <summary>
    /// 「磁盘上的库读不出来」—— 本次会话**禁止覆盖**。
    ///
    /// 为什么必须有这个状态：`TryLoad` 失败时内存里只有内置资源，而 `TrySave` 会把它
    /// 整份写回 `hmi-reslib.json` ⇒ 用户自定义资源被静默清空。旧实现里 `Load()` 裸
    /// `catch { return null; }`、调用方把 null 当"没存过"，再加上退出时无条件保存
    /// ⇒ 一次 JSON 损坏/瞬时读失败就永久丢数据（阶段 120 修复）。
    /// 判据：读失败之后 `TrySave` 必须返回 false 且**一个字节都不写**。
    /// </summary>
    private bool _libraryUnreadable;

    /// <summary>资源库目录路径（应用数据目录下）。</summary>
    public string DirectoryPath { get; }

    /// <summary>上一次 `TryLoad` 的失败原因；成功或文件不存在时为 null。</summary>
    public string? LastLoadError { get; private set; }

    public ResourceLibraryStore(string dataDir)
    {
        DirectoryPath = dataDir;
        _libraryPath = Path.Combine(dataDir, "hmi-reslib.json");
        _historyDir = Path.Combine(dataDir, "reslib-history");
        Directory.CreateDirectory(_historyDir);
    }

    /// <summary>
    /// 从磁盘加载资源库。
    /// 返回 false 表示**文件存在但读不出来**（损坏 / 无权限）—— 此时调用方必须
    /// 把库当只读处理，`TrySave` 会拒绝覆盖；返回 true 且 library 为 null 表示
    /// 文件不存在（正常的空库，可以保存）。
    /// </summary>
    public bool TryLoad(out HmiResourceLibrary? library)
    {
        library = null;
        LastLoadError = null;
        _libraryUnreadable = false;

        if (!File.Exists(_libraryPath)) return true;
        try
        {
            var json = File.ReadAllText(_libraryPath);
            var lib = new HmiResourceLibrary();
            lib.Deserialize(json);
            library = lib;
            return true;
        }
        catch (Exception ex)
        {
            LastLoadError = $"{ex.GetType().Name}: {ex.Message}";
            _libraryUnreadable = true;
            return false;
        }
    }

    /// <summary>
    /// 保存资源库并写入历史快照。
    /// 若本次会话的加载失败（磁盘上的库不可信），**拒绝保存**并返回 false ——
    /// 宁可不保存，也不能把用户的自定义资源换成"只剩内置"的空库。
    /// </summary>
    public bool TrySave(HmiResourceLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (_libraryUnreadable) return false;

        // 1. 写入当前库文件
        var json = library.Serialize();
        File.WriteAllText(_libraryPath, json);

        // 2. 写入时间戳历史快照（版本记录）
        //    毫秒分辨率 + 同名避让：旧实现只到秒，同一秒内两次保存会覆盖同一份快照。
        var stamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var snapshotPath = Path.Combine(_historyDir, $"hmi-reslib.{stamp}.json");
        for (int i = 1; File.Exists(snapshotPath); i++)
            snapshotPath = Path.Combine(_historyDir, $"hmi-reslib.{stamp}-{i}.json");
        File.WriteAllText(snapshotPath, json);

        // 3. 清理旧快照，保留最近 MaxSnapshots 个
        var snapshots = Directory.GetFiles(_historyDir, "hmi-reslib.*.json")
            .OrderByDescending(f => f)
            .ToList();
        foreach (var old in snapshots.Skip(MaxSnapshots))
        {
            try { File.Delete(old); } catch { /* 忽略删除失败 */ }
        }
        return true;
    }

    /// <summary>获取版本历史快照列表（新 → 旧）。</summary>
    public IReadOnlyList<ResourceSnapshotInfo> GetHistory()
    {
        return Directory.GetFiles(_historyDir, "hmi-reslib.*.json")
            .Select(f => new ResourceSnapshotInfo(
                Path.GetFileNameWithoutExtension(f),
                File.GetLastWriteTimeUtc(f),
                File.Exists(f) ? new FileInfo(f).Length : 0))
            .OrderByDescending(s => s.Timestamp)
            .ToList();
    }

    /// <summary>回滚到指定快照；快照不存在返回 false。</summary>
    public bool Rollback(string snapshotName)
    {
        var path = Path.Combine(_historyDir, $"{snapshotName}.json");
        if (!File.Exists(path)) return false;
        File.Copy(path, _libraryPath, overwrite: true);
        return true;
    }
}

/// <summary>资源库版本快照信息。</summary>
public sealed record ResourceSnapshotInfo(string Name, DateTime Timestamp, long SizeBytes);
