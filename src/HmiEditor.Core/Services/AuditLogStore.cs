using System.Text.Json;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Serialization;

namespace HmiEditor.Core.Services;

/// <summary>
/// 审计日志 JSONL 持久化存储 —— append-only，一行一条 JSON 记录。
///
/// 设计原则 (identity-and-secrets):
///   - append-only：只追加不覆写，天然防篡改（配合外部文件权限可满足审计要求）
///   - 容错读取：损坏/截断行跳过并记日志，不中断整体加载
///   - 枚举序列化为 camelCase 字符串，与项目配置保持一致
/// </summary>
public static class AuditLogStore
{
    private static readonly JsonSerializerOptions Options = JsonOptionsDefaults.Compact;

    private static readonly object AppendLock = new();

    /// <summary>
    /// 追加一条审计记录到日志文件（自动创建目录）。失败不抛出 —— 审计失败
    /// 不应阻断业务操作，仅通过返回值告知调用方。
    /// </summary>
    public static bool Append(string filePath, AuditLogEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            var json = JsonSerializer.Serialize(entry, Options);
            lock (AppendLock)
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.AppendAllText(filePath, json + Environment.NewLine);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 读取日志文件全部条目（按文件内顺序，即时间升序）。
    /// 损坏行跳过，返回前按时间倒序排列（最新在前，对齐 UI 展示习惯）。
    /// </summary>
    public static IReadOnlyList<AuditLogEntry> LoadAll(string filePath)
    {
        if (!File.Exists(filePath)) return Array.Empty<AuditLogEntry>();

        var entries = new List<AuditLogEntry>();
        try
        {
            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<AuditLogEntry>(line, Options);
                    if (entry != null) entries.Add(entry);
                }
                catch
                {
                    // 跳过损坏/截断行（如日志被外部工具截断）
                }
            }
        }
        catch
        {
            // 文件不可读时返回已解析部分
        }

        return entries.OrderByDescending(e => e.Timestamp).ToList();
    }
}
