using System.Text.Json;
using System.Text.RegularExpressions;

namespace HmiEditor.AI.Agent;

/// <summary>
/// Agent 模型输出协议。
/// 模型每轮输出必须为以下两种 JSON 之一（受 json_schema 约束）：
///   {"type":"tool_call","tool":"create_screen","arguments":{...}}
///   {"type":"final_answer","text":"...","summary":"..."}
/// 纯 JSON 协议保证对中转/兼容服务（无原生 function-calling 语义）同样可用。
/// </summary>
public static class AgentOutput
{
    public const string Schema = """
    {
      "type": "object",
      "properties": {
        "type": { "type": "string", "enum": ["tool_call", "final_answer"] },
        "tool": { "type": "string" },
        "arguments": { "type": "object" },
        "text": { "type": "string" },
        "summary": { "type": "string" }
      },
      "required": ["type"]
    }
    """;

    /// <summary>解析模型输出。非法输出返回 null（引擎触发一次重试提示）。</summary>
    public static AgentOutputKind? TryParse(string content, out AgentToolCall? toolCall, out string? finalText)
    {
        toolCall = null;
        finalText = null;

        if (string.IsNullOrWhiteSpace(content)) return null;

        // 容错：剥离 ```json 围栏（模型偶尔会输出 markdown）
        var text = content.Trim();
        if (text.StartsWith("```"))
        {
            var first = text.IndexOf('\n');
            if (first >= 0) text = text[(first + 1)..];
            var last = text.LastIndexOf("```", StringComparison.Ordinal);
            if (last >= 0) text = text[..last];
            text = text.Trim();
        }

        // 容错：多候选 JSON 提取。
        // 中转/兼容服务在无 response_format 强约束时，模型常输出
        // 思考过程 + 结果混合文本（"好的，我来处理：{...}"），甚至包含多个 JSON 片段。
        // 逐个候选尝试协议解析，命中即返回；若第一个候选是完整 JSON 对象，行为与原实现一致。
        foreach (var candidate in ExtractJsonCandidates(text))
        {
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                var kind = TryParseProtocol(doc.RootElement, out var call, out var final);
                if (kind is not null)
                {
                    toolCall = call;
                    finalText = final;
                    return kind;
                }
            }
            catch (JsonException)
            {
                // 候选非法 → 尝试下一个
            }
        }

        // 容错：Claude/部分中转模型的 XML 风格工具调用（走 OpenAI 兼容通道时以纯文本返回）：
        //   <tool_call>
        //   <function=open_panel>
        //   <parameter=panel_name>
        //   Assets
        //   </parameter>
        //   </function>
        //   </tool_call>
        var xmlKind = TryParseXml(text, out var xmlCall, out var xmlFinal);
        if (xmlKind is not null)
        {
            toolCall = xmlCall;
            finalText = xmlFinal;
            return xmlKind;
        }

        return null;
    }

    /// <summary>解析 Claude/中转模型的 XML 风格 tool_call（<tool_call><function=..><parameter=..>..</parameter></function></tool_call>）。</summary>
    private static AgentOutputKind? TryParseXml(string text, out AgentToolCall? toolCall, out string? finalText)
    {
        toolCall = null;
        finalText = null;

        // 优先匹配 tool_call 块。
        // 容错：模型输出被截断时开标签可能丢失（只剩 </tool_call> 结尾），此时以结尾标签前的整段内容为块。
        var callStart = text.IndexOf("<tool_call>", StringComparison.OrdinalIgnoreCase);
        if (callStart < 0)
        {
            var orphanClose = text.IndexOf("</tool_call>", StringComparison.OrdinalIgnoreCase);
            if (orphanClose >= 0)
            {
                callStart = 0;
                text = text[..orphanClose];
            }
        }

        if (callStart >= 0)
        {
            var blockEnd = text.IndexOf("</tool_call>", callStart + "<tool_call>".Length, StringComparison.OrdinalIgnoreCase);
            if (blockEnd < 0) blockEnd = text.Length;
            var block = text[callStart..blockEnd];

            var functionMatch = FunctionTagRegex.Match(block);
            if (!functionMatch.Success) return null;
            var functionName = functionMatch.Groups[1].Success ? functionMatch.Groups[1].Value : functionMatch.Groups[2].Value;
            if (string.IsNullOrWhiteSpace(functionName)) return null;

            toolCall = new AgentToolCall(functionName, BuildArgsDoc(block));
            return AgentOutputKind.ToolCall;
        }

        // 兜底：无 <tool_call> 包裹，整段文本本身就是一个 function 调用（输出被截断只剩 function 块）
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("<function", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<invoke", StringComparison.OrdinalIgnoreCase))
        {
            var fnMatch = FunctionTagRegex.Match(text);
            if (fnMatch.Success)
            {
                var fnName = fnMatch.Groups[1].Success ? fnMatch.Groups[1].Value : fnMatch.Groups[2].Value;
                if (!string.IsNullOrWhiteSpace(fnName))
                {
                    toolCall = new AgentToolCall(fnName, BuildArgsDoc(text));
                    return AgentOutputKind.ToolCall;
                }
            }
        }

        // 兜底：<final_answer>text</final_answer>
        var faStart = text.IndexOf("<final_answer>", StringComparison.OrdinalIgnoreCase);
        if (faStart >= 0)
        {
            var faEnd = text.IndexOf("</final_answer>", faStart + "<final_answer>".Length, StringComparison.OrdinalIgnoreCase);
            if (faEnd < 0) faEnd = text.Length;
            finalText = text[(faStart + "<final_answer>".Length)..faEnd].Trim();
            return AgentOutputKind.FinalAnswer;
        }

        return null;
    }

    /// <summary>匹配 &lt;function=name&gt; 或 &lt;function name="name"&gt;（亦兼容 &lt;invoke name=".."&gt;）。</summary>
    private static readonly System.Text.RegularExpressions.Regex FunctionTagRegex = new(
        @"<(?:function|invoke)\b[^>]*?(?:name\s*=\s*[""']([^""']+)[""']|=([A-Za-z_][\w-]*))[^>]*>",
        System.Text.RegularExpressions.RegexOptions.Singleline |
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>匹配 &lt;parameter=key&gt;value&lt;/parameter&gt; 或 &lt;parameter name="key"&gt;value&lt;/parameter&gt;。</summary>
    private static readonly System.Text.RegularExpressions.Regex ParameterTagRegex = new(
        @"<(?:parameter|param)\b[^>]*?(?:name\s*=\s*[""']([^""']+)[""']|=([A-Za-z_][\w-]*))[^>]*>(.*?)</(?:parameter|param)>",
        System.Text.RegularExpressions.RegexOptions.Singleline |
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly JsonSerializerOptions BuildArgsSerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>从 XML 块中提取 &lt;parameter&gt; 参数并序列化为 JsonElement（无参数时为空对象）。
    /// 若参数值本身是 JSON 数组/对象/数字/布尔字面量，则保留其原生类型而非转义为字符串。</summary>
    private static JsonElement BuildArgsDoc(string block)
    {
        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (Match param in ParameterTagRegex.Matches(block))
        {
            var key = param.Groups[1].Success ? param.Groups[1].Value : param.Groups[2].Value;
            var value = param.Groups[3].Value.Trim();
            if (key.Length > 0)
                args[key] = InferJsonValue(value);
        }

        using var argsDoc = JsonDocument.Parse(JsonSerializer.Serialize(args, BuildArgsSerializeOptions));
        return argsDoc.RootElement.Clone();
    }

    /// <summary>对字符串值进行 JSON 类型推断：若像数组/对象/数字/布尔/null 则解析为原生类型。
    /// 兼容模型输出中 JSON 键值被转义（如 \u0022 → "）的情况。</summary>
    private static object InferJsonValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return value;

        // 若值内包含 \u0022（模型常把引号转义为 Unicode），先还原再解析
        if (trimmed.Contains("\\u0022"))
            trimmed = trimmed.Replace("\\u0022", "\"");

        // 快速特征判断：以 [ { 开头 或 是数字/布尔/null 字面量
        var first = trimmed[0];
        var looksLikeJson = first is '[' or '{' ||
            (first is '-' or (>= '0' and <= '9')) ||
            trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("null", StringComparison.OrdinalIgnoreCase);

        if (!looksLikeJson) return value;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => doc.RootElement.Clone(),
                JsonValueKind.Object => doc.RootElement.Clone(),
                JsonValueKind.Number => doc.RootElement.GetRawText(), // 让序列化层还原为数字
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                _ => value
            };
        }
        catch (JsonException)
        {
            return value;
        }
    }

    /// <summary>协议解析：根元素必须是 { type: "tool_call"|"final_answer", ... } 形状。</summary>
    private static AgentOutputKind? TryParseProtocol(
        JsonElement root,
        out AgentToolCall? toolCall,
        out string? finalText)
    {
        toolCall = null;
        finalText = null;

        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty("type", out var typeProp)) return null;
        var type = typeProp.GetString();

        if (type == "tool_call")
        {
            if (!root.TryGetProperty("tool", out var toolProp)) return null;
            var toolName = toolProp.GetString();
            if (string.IsNullOrWhiteSpace(toolName)) return null;

            var args = root.TryGetProperty("arguments", out var argsProp)
                ? argsProp.Clone()
                : JsonDocument.Parse("{}").RootElement.Clone();
            toolCall = new AgentToolCall(toolName, args);
            return AgentOutputKind.ToolCall;
        }

        if (type == "final_answer")
        {
            if (root.TryGetProperty("text", out var textProp)) finalText = textProp.GetString();
            finalText ??= root.TryGetProperty("summary", out var sumProp) ? sumProp.GetString() : "";
            return AgentOutputKind.FinalAnswer;
        }

        return null;
    }

    /// <summary>从文本中收集平衡 JSON 对象候选（按出现顺序去重，最多 12 个），供逐个尝试协议解析。</summary>
    private static IEnumerable<string> ExtractJsonCandidates(string text)
    {
        var seen = new HashSet<string>();
        var searchFrom = 0;
        while (searchFrom < text.Length && seen.Count < 12)
        {
            var start = text.IndexOf('{', searchFrom);
            if (start < 0) break;

            var candidate = ExtractBalancedJson(text, start);
            if (candidate is not null && seen.Add(candidate))
                yield return candidate;

            searchFrom = start + 1;
        }
    }

    /// <summary>从指定 '{' 位置提取平衡 JSON 对象（跳过字符串内的花括号），无法闭合返回 null。</summary>
    private static string? ExtractBalancedJson(string text, int start)
    {
        var inString = false;
        var escaped = false;
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }

            switch (c)
            {
                case '"': inString = true; break;
                case '{': depth++; break;
                case '}' when --depth == 0: return text[start..(i + 1)];
            }
        }
        return null;
    }

    /// <summary>参数 JSON 的实用读取器（容错缺失属性）。</summary>
    public static string GetString(JsonElement args, string name, string fallback = "")
    {
        if (args.ValueKind == JsonValueKind.Object &&
            args.TryGetProperty(name, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString() ?? fallback;
        }
        return fallback;
    }

    public static double GetDouble(JsonElement args, string name, double fallback = 0)
    {
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var d)) return d;
            if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out var s)) return s;
        }
        return fallback;
    }

    /// <summary>
    /// 与 <see cref="GetDouble"/> 同源，但区分「参数没给」与「参数给了 0」。
    /// <para>
    /// <b>用途：</b>需要把「没给」交给下游默认值（例如工程的屏幕默认模板）时用这个，
    /// 不要用 <see cref="GetDouble"/> 填一个自造的常量 —— 那会把真正的默认值静默压掉。
    /// </para>
    /// </summary>
    public static double? GetDoubleOrNull(JsonElement args, string name)
    {
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var d)) return d;
            if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out var s)) return s;
        }
        return null;
    }

    /// <summary>与 <see cref="GetString"/> 同源，但空白串按「没给」处理（返回 null）。</summary>
    public static string? GetStringOrNull(JsonElement args, string name)
    {
        var s = GetString(args, name);
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    public static int GetInt(JsonElement args, string name, int fallback = 0)
    {
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i)) return i;
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var s)) return s;
        }
        return fallback;
    }

    public static bool GetBool(JsonElement args, string name, bool fallback = false)
    {
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
            if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out var s)) return s;
        }
        return fallback;
    }

    /// <summary>将参数对象序列化为紧凑 JSON 文本（用于工具内部反序列化为 Dictionary）。</summary>
    public static Dictionary<string, string> ToStringDict(JsonElement args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (args.ValueKind != JsonValueKind.Object) return dict;

        foreach (var prop in args.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? "",
                _ => prop.Value.GetRawText()
            };
        }
        return dict;
    }
}

/// <summary>模型输出类型。</summary>
public enum AgentOutputKind
{
    ToolCall,
    FinalAnswer
}
