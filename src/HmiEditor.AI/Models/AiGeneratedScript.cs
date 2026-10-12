namespace HmiEditor.AI.Models;

/// <summary>
/// LLM 生成的 Lua 脚本中间模型 —— 对应 ScriptGenerationSchema.json 的结构化输出。
/// 反序列化后再映射为 Core 的 HmiScript。
/// </summary>
public class AiGeneratedScript
{
    public AiScript Script { get; set; } = new();
}

public class AiScript
{
    public string Name { get; set; } = "AI 生成脚本";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "OnClick";
    public string Code { get; set; } = "";
}
