using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HmiEditor.Core.Serialization;

/// <summary>
/// JSON 序列化选项预设中心 —— 统一管理项目中所有 <see cref="JsonSerializerOptions"/> 实例。
///
/// 设计目标:
///   - 避免各序列化器重复配置造成的风格漂移
///   - 枚举一律序列化为 camelCase 字符串（与 .hmi.json 等核心文件保持一致）
///   - 中文字符不转义为 \uXXXX（保留可读性）
///   - 属性名统一 camelCase
///
/// 预设清单:
///   <list type="bullet">
///     <item><see cref="Default"/>     — 持久化 JSON 文件（.hmi.json / .nav.json / .layout.json / 模板）</item>
///     <item><see cref="Compact"/>     — JSONL 逐行日志（审计日志）</item>
///     <item><see cref="Llm"/>         — LLM HTTP 通讯（容错大小写）</item>
///     <item><see cref="LlmConfig"/>   — LLM 配置文件（人工可读、容错大小写）</item>
///   </list>
///
/// 使用方式:
///   <code>
///   JsonSerializer.Serialize(obj, JsonOptionsDefaults.Default);
///   JsonSerializer.Deserialize&lt;T&gt;(json, JsonOptionsDefaults.Llm);
///   </code>
///
/// 注意: <see cref="JsonSerializerOptions"/> 的转换器列表在拷贝构造时是浅拷贝（共享引用），
/// 因为 <see cref="JsonStringEnumConverter"/> 是无状态的，这种共享是安全的。
/// </summary>
public static class JsonOptionsDefaults
{
    /// <summary>
    /// 标准预设 —— 适用于需要持久化到磁盘的 JSON 文件。
    ///
    /// 使用此预设的序列化器:
    /// <list type="bullet">
    ///   <item><see cref="ProjectSerializer"/>       — .hmi.json 项目文件</item>
    ///   <item><see cref="NavigationGraphSerializer"/> — .nav.json 导航流图</item>
    ///   <item>LayoutSerializer                       — .layout.json Dock 布局</item>
    ///   <item>TemplateManager                        — Faceplate 模板 JSON</item>
    /// </list>
    ///
    /// 配置: 缩进 + CamelCase + 忽略 null + 中文不转义 + 枚举 camelCase 字符串。
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // null 属性不写入 JSON，减小文件体积并保持向后兼容（旧版本读取时缺失字段使用 C# 默认值）
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // 默认 Encoder 会将中文等非 ASCII 字符转义为 \uXXXX，不利于人工阅读与 git diff
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // 枚举序列化为 camelCase 字符串（如 "warning"），而非数字（如 2），提升可读性并防止枚举重排破坏兼容性
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// 紧凑预设 —— 适用于 JSONL 逐行日志（如 <see cref="AuditLogStore"/>）。
    ///
    /// 在 <see cref="Default"/> 基础上关闭缩进：JSONL 格式要求每行一条独立 JSON 记录，
    /// 缩进会引入换行符，破坏"一行一条"的语义。
    /// </summary>
    public static readonly JsonSerializerOptions Compact = new(Default)
    {
        WriteIndented = false
    };

    /// <summary>
    /// LLM 通讯预设 —— 适用于与外部 LLM API 交互（<see cref="HmiEditor.AI.Inference.LlmHttpProviderBase"/>、
    /// <see cref="HmiEditor.AI.Generators.NlToScreenGenerator"/> 等）。
    ///
    /// 配置: 大小写不敏感 + CamelCase。
    /// 不包含 <see cref="JsonStringEnumConverter"/>: LLM 中间模型（AiGeneratedScreen 等）几乎无枚举字段，
    /// 且 LLM 输出的 JSON 已为字符串，加上转换器反而可能引入意外的枚举校验失败。
    /// 不包含 <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>: LLM API 通讯走 HTTP，
    /// 转义不影响功能，保留默认 Encoder 更安全。
    /// </summary>
    public static readonly JsonSerializerOptions Llm = new()
    {
        // 大小写不敏感: LLM 输出字段名大小写不可控（可能 PascalCase / snake_case / camelCase 混用）
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// LLM 配置文件预设 —— 适用于 ai.config.json 等用户可能手动编辑的 LLM 配置文件
    /// （<see cref="HmiEditor.App.Services.LlmConfigStore"/>）。
    ///
    /// 在 <see cref="Llm"/> 基础上启用缩进，便于人工阅读与修改。
    /// 保留 <c>PropertyNameCaseInsensitive</c>: 用户手动编辑时可能写成 PascalCase（如 "ApiKey"），
    /// 大小写不敏感可避免解析失败。
    /// </summary>
    public static readonly JsonSerializerOptions LlmConfig = new(Llm)
    {
        WriteIndented = true
    };
}
