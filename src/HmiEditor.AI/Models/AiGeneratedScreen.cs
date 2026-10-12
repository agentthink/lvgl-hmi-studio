using System.Text.Json.Serialization;

namespace HmiEditor.AI.Models;

/// <summary>
/// LLM 生成的屏幕中间模型 —— 对应 ScreenGenerationSchema.json 的结构化输出。
/// 反序列化后再映射为 Core 的 HmiScreen。
/// </summary>
public class AiGeneratedScreen
{
    public AiScreen Screen { get; set; } = new();
}

public class AiScreen
{
    public string Name { get; set; } = "AI 生成屏幕";

    /// <summary>
    /// 屏幕宽度。null = LLM 没给 ⇒ 由 <c>ScreenFactory</c> 落到工程的屏幕默认尺寸。
    /// <para>
    /// 刻意<b>不</b>给默认值：曾经这里是 <c>int Width = 800</c>，于是「LLM 没给」和
    /// 「LLM 说要 800」不可区分，工程设置里的默认尺寸（1024×600）永远被 800×480 压掉。
    /// </para>
    /// </summary>
    public int? Width { get; set; }

    /// <summary>屏幕高度。语义同 <see cref="Width"/>。</summary>
    public int? Height { get; set; }

    public string? BgColor { get; set; }
    public List<AiControl> Controls { get; set; } = new();
}

public class AiControl
{
    public string Type { get; set; } = "Label";
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 100;
    public int Height { get; set; } = 40;
    public string? Text { get; set; }
    public string? BgColor { get; set; }
    public string? TextColor { get; set; }
    public int Radius { get; set; } = 4;

    /// <summary>绑定的变量名（LLM 引用项目变量列表中的 Name，映射时解析为 Id）。</summary>
    [JsonPropertyName("variableBindingId")]
    public string? VariableName { get; set; }
}
