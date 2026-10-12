using System.Text;
using System.Text.Json;
using HmiEditor.Core.Models;

namespace HmiEditor.AI.Agent;

/// <summary>
/// Agent 工具调用 —— 模型输出中解析出的单次工具调用。
/// </summary>
public sealed record AgentToolCall(string Name, JsonElement Arguments);

/// <summary>
/// 工具执行结果。
/// </summary>
public sealed record AgentToolResult(bool Ok, string Summary, string? Error = null, bool Changed = false)
{
    public static AgentToolResult Success(string summary, bool changed = false) =>
        new(true, summary, null, changed);

    public static AgentToolResult Failure(string error) =>
        new(false, error, error);
}

/// <summary>
/// 工具执行上下文 —— 携带当前项目。写操作由 App 层在闭包中捕获所需服务完成 UI 刷新。
/// </summary>
public sealed class AgentToolContext
{
    public required HmiProject Project { get; init; }

    /// <summary>当前会话 ID（一次 Agent 运行一个会话）。</summary>
    public required string SessionId { get; init; }
}

/// <summary>
/// Agent 工具定义。ArgumentsSchema 为参数 JSON Schema（注入 system prompt 约束模型输出）。
/// 查询类工具在 AI 层实现；写类工具由 App 层闭包实现（绑定项目操作 + UI 刷新 + 回滚快照）。
/// </summary>
public sealed class AgentTool
{
    public AgentTool(
        string name,
        string description,
        string? argumentsSchema = null,
        Func<AgentToolCall, AgentToolContext, CancellationToken, Task<AgentToolResult>>? execute = null,
        bool isReadOnly = false)
    {
        Name = name;
        Description = description;
        Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        ArgumentsSchema = argumentsSchema;
        IsReadOnly = isReadOnly;
    }

    /// <summary>工具唯一名称（模型通过此名调用）。</summary>
    public string Name { get; }

    /// <summary>工具功能描述（中文，注入 system prompt）。</summary>
    public string Description { get; }

    /// <summary>参数 JSON Schema（对象结构）。null 表示无参数。</summary>
    public string? ArgumentsSchema { get; }

    /// <summary>是否只读查询（不回滚/不标记变更）。</summary>
    public bool IsReadOnly { get; }

    /// <summary>执行委托。</summary>
    public Func<AgentToolCall, AgentToolContext, CancellationToken, Task<AgentToolResult>> Execute { get; }

    public Task<AgentToolResult> ExecuteAsync(AgentToolCall call, AgentToolContext ctx, CancellationToken ct) =>
        Execute(call, ctx, ct);

    /// <summary>
    /// 工具集合转 JSON 工具清单文本（注入 system prompt）。
    /// </summary>
    public static string BuildToolListJson(IEnumerable<AgentTool> tools)
    {
        var list = tools.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            arguments = string.IsNullOrWhiteSpace(t.ArgumentsSchema) ? null : ParseSchema(t.ArgumentsSchema)
        });
        return JsonSerializer.Serialize(list, JsonOptions);
    }

    /// <summary>
    /// 工具集合转紧凑文本清单（注入 system prompt，token 约为完整 JSON Schema 的 1/3）。
    /// 格式: "- name(参数a!, 参数b[枚举A|枚举B]): 描述"，参数名带 ! 为必填。
    /// </summary>
    public static string BuildToolList(IEnumerable<AgentTool> tools)
    {
        var sb = new StringBuilder();
        foreach (var t in tools)
        {
            sb.Append("- ").Append(t.Name).Append(BuildSignature(t.ArgumentsSchema))
              .Append(": ").AppendLine(t.Description);
        }
        return sb.ToString();
    }

    /// <summary>从参数 JSON Schema 提取紧凑签名 "(a!, b, c[X|Y])"；无参数/解析失败返回 "()"。</summary>
    private static string BuildSignature(string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema)) return "()";
        try
        {
            using var doc = JsonDocument.Parse(schema);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return "()";

            var required = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.Array)
                foreach (var r in req.EnumerateArray())
                    if (r.ValueKind == JsonValueKind.String) required.Add(r.GetString() ?? "");

            var parts = new List<string>();
            if (root.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in props.EnumerateObject())
                {
                    var name = p.Name + (required.Contains(p.Name) ? "!" : "");
                    if (p.Value.ValueKind == JsonValueKind.Object &&
                        p.Value.TryGetProperty("enum", out var en) && en.ValueKind == JsonValueKind.Array)
                    {
                        var vals = string.Join("|", en.EnumerateArray()
                            .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText()));
                        if (vals.Length > 0) name += $"[{vals}]";
                    }
                    parts.Add(name);
                }
            }
            return "(" + string.Join(", ", parts) + ")";
        }
        catch (JsonException)
        {
            return "()";
        }
    }

    private static object? ParseSchema(string schema)
    {
        try
        {
            return JsonSerializer.Deserialize<object>(schema);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };
}
