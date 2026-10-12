using HmiEditor.Core.Models;

namespace HmiEditor.AI.Services;

/// <summary>
/// 变量绑定推荐引擎 —— 基于规则 + 命名相似度,为控件推荐最合适的变量绑定。
/// 不依赖 LLM,确定性算法,可单元测试。
/// </summary>
public static class VariableMatchService
{
    /// <summary>推荐结果条目。</summary>
    public sealed record VariableMatch(Variable Variable, double Score, string Reason);

    /// <summary>
    /// 为控件推荐 Top N 变量绑定。
    /// </summary>
    /// <param name="control">目标控件(其 Name 参与命名匹配)。</param>
    /// <param name="variableManager">项目变量管理器。</param>
    /// <param name="maxResults">最多返回条数。</param>
    public static List<VariableMatch> Recommend(HmiControl control, VariableManager variableManager, int maxResults = 3)
    {
        if (variableManager?.Variables is null || variableManager.Variables.Count == 0)
            return new List<VariableMatch>();

        var controlType = control.Type;
        var controlName = control.Name ?? "";
        var keywords = ExtractKeywords(controlName);

        var scored = new List<VariableMatch>();
        foreach (var variable in variableManager.Variables)
        {
            var score = ScoreVariable(variable, controlType, keywords);
            if (score > 0)
            {
                scored.Add(new VariableMatch(variable, score, BuildReason(variable, score)));
            }
        }

        return scored
            .OrderByDescending(m => m.Score)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// 变量评分:
    /// 1. 类型兼容 +50
    /// 2. 名称关键词匹配: 全词 +30 / 部分 +15
    /// 3. 语义前缀(温度/压力/速度等)匹配 +20
    /// 4. 名称长度惩罚(过短名称噪声大)
    /// </summary>
    private static double ScoreVariable(Variable variable, HmiControlType controlType, HashSet<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(variable.Name)) return 0;

        double score = 0;

        // 1. 类型兼容性
        if (IsDataTypeCompatible(controlType, variable.DataType))
            score += 50;

        // 2. 名称关键词匹配 + 语义别名匹配
        var varName = variable.Name;
        if (keywords.Count > 0)
        {
            var lowerVar = varName.ToLowerInvariant();
            var matchedAny = false;
            foreach (var kw in keywords)
            {
                if (lowerVar.Equals(kw, StringComparison.Ordinal))
                {
                    score += 30;
                    matchedAny = true;
                }
                else if (lowerVar.Contains(kw, StringComparison.Ordinal))
                {
                    score += 15;
                    matchedAny = true;
                }

                // 语义别名匹配 (中文关键词 → 英文变量名)
                foreach (var alias in SemanticAliases(kw))
                {
                    if (lowerVar.Contains(alias, StringComparison.Ordinal))
                    {
                        score += 20;
                        matchedAny = true;
                        break;
                    }
                }
            }
            if (!matchedAny) return 0; // 无任何关键词/别名匹配,不推荐
        }

        // 4. 名称过短惩罚 (单字符变量名噪声大)
        if (varName.Length <= 2)
            score -= 10;

        return score;
    }

    /// <summary>提取控件名称关键词: 去常见后缀 + 拆分词。</summary>
    private static HashSet<string> ExtractKeywords(string name)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(name)) return result;

        // 去除常见显示后缀
        var cleaned = name
            .Replace("显示", "")
            .Replace("显示值", "")
            .Replace("标签", "")
            .Replace("状态", "")
            .Replace("按钮", "")
            .Replace("控件", "")
            .Trim();

        if (cleaned.Length > 0)
            result.Add(cleaned);

        // 中英文关键词
        if (cleaned.Contains("温度") || cleaned.Contains("temp", StringComparison.OrdinalIgnoreCase))
            result.Add("温度");
        if (cleaned.Contains("压力") || cleaned.Contains("press", StringComparison.OrdinalIgnoreCase))
            result.Add("压力");
        if (cleaned.Contains("速度") || cleaned.Contains("speed", StringComparison.OrdinalIgnoreCase))
            result.Add("速度");
        if (cleaned.Contains("电流") || cleaned.Contains("current", StringComparison.OrdinalIgnoreCase))
            result.Add("电流");
        if (cleaned.Contains("电压") || cleaned.Contains("voltage", StringComparison.OrdinalIgnoreCase))
            result.Add("电压");
        if (cleaned.Contains("流量") || cleaned.Contains("flow", StringComparison.OrdinalIgnoreCase))
            result.Add("流量");
        if (cleaned.Contains("液位") || cleaned.Contains("level", StringComparison.OrdinalIgnoreCase))
            result.Add("液位");
        if (cleaned.Contains("电机") || cleaned.Contains("motor", StringComparison.OrdinalIgnoreCase))
            result.Add("电机");
        if (cleaned.Contains("开关") || cleaned.Contains("switch", StringComparison.OrdinalIgnoreCase))
            result.Add("开关");
        if (cleaned.Contains("启停") || cleaned.Contains("start", StringComparison.OrdinalIgnoreCase))
            result.Add("启停");

        return result;
    }

    /// <summary>中文关键词 → 英文别名 (用于匹配变量名)。</summary>
    private static IEnumerable<string> SemanticAliases(string keyword)
    {
        return keyword switch
        {
            "温度" => new[] { "temp", "temperature" },
            "压力" => new[] { "press", "pressure" },
            "速度" => new[] { "speed", "velocity" },
            "电流" => new[] { "current", "amp" },
            "电压" => new[] { "voltage", "volt" },
            "流量" => new[] { "flow", "rate" },
            "液位" => new[] { "level", "liquid" },
            "电机" => new[] { "motor", "engine" },
            "开关" => new[] { "switch", "toggle" },
            "启停" => new[] { "start", "stop", "run" },
            _ => Array.Empty<string>()
        };
    }

    /// <summary>数据类型兼容性 —— 与 VariablePicker.GetCompatibleDataTypes 保持一致的规则。</summary>
    internal static bool IsDataTypeCompatible(HmiControlType controlType, string? dataType)
    {
        if (string.IsNullOrEmpty(dataType)) return true;

        // UDT 类型只允许 Table
        if (dataType.StartsWith("UDT:", StringComparison.Ordinal))
            return controlType == HmiControlType.Table;

        var compatible = controlType switch
        {
            HmiControlType.Led or HmiControlType.Switch or HmiControlType.CheckBox
                => new[] { "Bool", "Int16", "Int32", "UInt16", "UInt32" },
            HmiControlType.Gauge or HmiControlType.Bar or HmiControlType.Knob
                or HmiControlType.Slider or HmiControlType.Dropdown
                => new[] { "Int16", "Int32", "Float", "Double", "UInt16", "UInt32" },
            _ => new[] { "Bool", "Int16", "Int32", "Float", "Double", "UInt16", "UInt32", "String" }
        };

        return compatible.Contains(dataType);
    }

    private static string BuildReason(Variable variable, double score)
    {
        if (score >= 80) return "类型兼容且名称高度匹配";
        if (score >= 60) return "类型兼容且名称部分匹配";
        if (score >= 50) return "仅类型兼容";
        return "名称匹配";
    }
}
