using HmiEditor.Core.Models;

namespace HmiEditor.AI;

/// <summary>
/// AI / Agent 侧 schema 与提示词里出现的**枚举词表的唯一来源**。
///
/// <para><b>为什么要有这个类</b>：这些词表会以三种形态出现在三处地方 ——
/// ① JSON Schema 的 <c>enum</c> 数组（OpenAI 严格 <c>response_format.json_schema</c> 下
/// 是**硬约束**，模型无法输出数组之外的值）；② 工具描述 / System 提示里的可读清单；
/// ③ 解析返回值时的白名单。三处手抄同一份清单必然漂移，而且**漂移不会报错**：
/// 模型照着 schema 生成一个产品侧根本不存在的值，消费侧静默回退或静默匹配失败。</para>
///
/// <para><b>实测过的漂移</b>（2026-09-15，收口前）：
/// 控件类型手抄 21 个（真值 25，缺 <c>Rectangle/Ellipse/Polyline/Arc</c>）；
/// <c>operation_mode</c> 写 <c>Runtime</c>（<see cref="OperationMode"/> 里根本没有，真名 <c>Operator</c>）；
/// 报警条件写 <c>OnTrue/OnFalse</c>（<see cref="AlarmConditionType"/> 里没有）；
/// 驱动写 <c>opcua</c>（真实驱动 Id 是 <c>opc-ua</c>）。</para>
/// </summary>
public static class AiSchemaEnums
{
    /// <summary>
    /// AI **可生成**的控件类型名 —— 唯一来源是 <see cref="HmiControlType"/> 枚举，排除
    /// <see cref="HmiControlType.Screen"/>。
    ///
    /// <para><b>注意这条排除是 AI 侧的策略，不是"Screen 非法"</b>：
    /// <see cref="HmiControlType.Screen"/> 对**人类用户**是合法控件（工具箱"容器"分类里可拖入，
    /// <c>ControlMappingRegistry</c> 把它映射到 <c>lv_obj_create</c>，
    /// <c>ControlPipelineCompletenessTests</c> 要求它为每个枚举成员都提供 create 函数与本地化名）。
    /// AI 不该生成它，是因为嵌套容器需要成体系的父子语义（<c>Children</c>），
    /// 而 AI 生成的是扁平控件列表 —— 生成出来的容器必然是个空壳。</para>
    /// </summary>
    public static IReadOnlyList<string> GeneratableControlTypes { get; } =
        Enum.GetNames<HmiControlType>()
            .Where(n => n != nameof(HmiControlType.Screen))
            .ToArray();

    /// <summary>
    /// 解析 AI 给出的控件类型名 —— **策略与 <see cref="GeneratableControlTypes"/> 同一个源**：
    /// 非枚举成员拒绝，<see cref="HmiControlType.Screen"/> 也拒绝。
    ///
    /// <para>调用方请注意：provider 的降级路径（<c>json_object</c> / 无 <c>response_format</c>）
    /// 不会受 schema 的 <c>enum</c> 约束，所以模型**确实**可能给出 <c>Screen</c> ——
    /// 本方法存在的意义就是兜住那条路径。</para>
    /// </summary>
    public static bool TryParseGeneratableControlType(string? name, out HmiControlType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (!Enum.TryParse<HmiControlType>(name, ignoreCase: true, out var parsed)) return false;
        if (parsed == HmiControlType.Screen) return false;

        type = parsed;
        return true;
    }

    /// <summary>
    /// 同一策略的"永不失败"变体 —— 解析不出来的名字一律退化成 <see cref="HmiControlType.Label"/>。
    ///
    /// <para>两个 AI 生成器（自然语言 → 屏幕 / 自然语言 → 模板）用它把模型的输出转成控件：
    /// 宁可退化成能显示的文本控件，也不要把一个空壳容器控件塞进用户工程。</para>
    /// </summary>
    public static HmiControlType ParseGeneratableControlTypeOrLabel(string? name)
        => TryParseGeneratableControlType(name, out var type) ? type : HmiControlType.Label;

    /// <summary>枚举成员名 → JSON 字符串数组字面量（不含外层引号），如 <c>["A", "B"]</c>。</summary>
    public static string JsonArray<TEnum>() where TEnum : struct, Enum
        => JsonArray(Enum.GetNames<TEnum>());

    /// <summary>字符串清单 → JSON 字符串数组字面量。</summary>
    public static string JsonArray(IEnumerable<string> names)
        => "[" + string.Join(", ", names.Select(n => $"\"{n}\"")) + "]";

    /// <summary>枚举成员名 → 逗号分隔清单（工具描述 / 提示词里的可读列表用）。顺序 = 声明顺序。</summary>
    public static string Csv<TEnum>() where TEnum : struct, Enum
        => string.Join(",", Enum.GetNames<TEnum>());

    // ── GeneratableControlTypes 的两种紧凑形态 ──
    // 刻意做成属性而不是让调用方自己 JsonArray(...)/string.Join(...)：
    // 「哪些类型可生成」这件事的**定义**只有 GeneratableControlTypes 一处，
    // 形态只是它的呈现 —— 调用点越短，越没人会顺手再抄一份。

    /// <summary>AI 可生成控件类型名 → JSON 字符串数组字面量（JSON Schema 的 <c>enum</c> 用）。</summary>
    public static string GeneratableControlTypesJson { get; } = JsonArray(GeneratableControlTypes);

    /// <summary>AI 可生成控件类型名 → 逗号分隔清单（工具参数说明文本用）。</summary>
    public static string GeneratableControlTypesCsv { get; } = string.Join(",", GeneratableControlTypes);
}
