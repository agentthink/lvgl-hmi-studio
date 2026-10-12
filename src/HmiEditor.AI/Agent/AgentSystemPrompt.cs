using System.Text;
using HmiEditor.Core.Models;

namespace HmiEditor.AI.Agent;

/// <summary>
/// Agent System Prompt 构建器 —— 协议说明 + 工具清单 + 项目摘要。
/// </summary>
public static class AgentSystemPrompt
{
    /// <summary>
    /// 构建完整 System Prompt。
    /// </summary>
    /// <param name="tools">可用工具列表。</param>
    /// <param name="project">当前项目（可选，提供上下文）。</param>
    /// <param name="useXmlProtocol">true=使用 XML 风格协议（对不支持 json_schema 的模型更稳定）；false=使用 JSON 协议（默认）。</param>
    public static string Build(IEnumerable<AgentTool> tools, HmiProject? project, bool useXmlProtocol = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是运行在 HMI 工程组态软件中的智能体（Agent），可以调用工具直接操作当前 HMI 项目。");
        sb.AppendLine("你可以创建/修改屏幕与控件、管理变量与通讯连接、配置报警/趋势/配方/用户、生成脚本、执行编辑器操作。");
        sb.AppendLine();

        if (useXmlProtocol)
        {
            sb.AppendLine("## 输出协议（严格遵守，只输出 XML 工具调用，不要输出任何解释文字或 markdown）");
            sb.AppendLine("每轮只能输出以下两种之一：");
            sb.AppendLine("1. 调用工具：<tool_call><function=工具名><parameter=参数名>参数值</parameter>...</function></tool_call>");
            sb.AppendLine("2. 完成任务：<final_answer>面向用户的最终总结</final_answer>");
            sb.AppendLine("- 每个 parameter 必须包含参数名和参数值，必填参数不可省略。");
            sb.AppendLine("- 数组参数（如 variables/controls）的值必须是合法 JSON 数组文本，例如：[{\"name\":\"Var1\"},{\"name\":\"Var2\"}]");
            sb.AppendLine("- 必须完整包含 <tool_call> 开标签与 </tool_call> 结束标签。");
        }
        else
        {
            sb.AppendLine("## 输出协议（严格遵守，只输出工具调用 JSON，不要输出任何解释文字或 markdown）");
            sb.AppendLine("每轮只能输出以下两种之一：");
            sb.AppendLine("1. 调用工具：{\"type\":\"tool_call\",\"tool\":\"工具名\",\"arguments\":{...参数...}}");
            sb.AppendLine("2. 完成任务：{\"type\":\"final_answer\",\"text\":\"面向用户的最终总结\",\"summary\":\"一句话要点\"}");
            sb.AppendLine("- arguments 必须是一个非空对象，至少包含该工具的必填参数。严禁输出空对象 {} 或省略必填参数。");
            sb.AppendLine("- 若你以 XML 风格输出 tool_call（<tool_call><function=工具名>...</function></tool_call>），必须完整包含 <tool_call> 开标签与 </tool_call> 结束标签，严禁截断或省略任一部分。");
        }
        sb.AppendLine();
        sb.AppendLine("## 行为准则");
        sb.AppendLine("- 需要多个操作时，可以连续调用多个工具（每次只调用一个，等待工具结果后继续）。");
        sb.AppendLine("- **成组创建务必用批量工具**：多个变量用 create_variables_batch 一次创建；一屏多个控件用 add_controls_batch（配合 auto_grid 自动网格布局）。严禁循环逐个 create_variable/add_control 浪费步数。");
        if (useXmlProtocol)
        {
            sb.AppendLine("- **批量工具调用示例（XML 协议）**：");
            sb.AppendLine("  create_variables_batch: <tool_call><function=create_variables_batch><parameter=group_name>温控组</parameter><parameter=variables>[{\"name\":\"Temp_Set_1\",\"data_type\":\"Float\"},{\"name\":\"Temp_Current_1\",\"data_type\":\"Float\"}]</parameter></function></tool_call>");
            sb.AppendLine("  add_controls_batch: <tool_call><function=add_controls_batch><parameter=screen_name>温控监控屏</parameter><parameter=controls>[{\"type\":\"Label\",\"name\":\"lbl1\",\"text\":\"温度\"},{\"type\":\"NumericDisplay\",\"name\":\"disp1\"}]</parameter></function></tool_call>");
            sb.AppendLine("- 数组参数值必须是合法 JSON 数组文本（如 [{\"name\":\"Var1\"},...]），参数值内的引号使用普通双引号。");
        }
        else
        {
            sb.AppendLine("- **批量工具调用示例**：create_variables_batch 的参数必须是 {\"variables\":[{\"name\":\"Var1\",\"data_type\":\"Float\"},...]}，variables 是数组且至少有一项；add_controls_batch 的参数必须是 {\"screen_name\":\"Screen1\",\"controls\":[{\"type\":\"Label\",\"name\":\"lbl1\"},...]}。");
        }
        sb.AppendLine("- 工具参数必须严格符合工具的参数说明；可选参数可省略，但必填参数必须提供有效值。");
        sb.AppendLine("- 创建实体（屏幕/控件/变量等）时若用户未指定 Id，不要传 Id 字段，系统会自动生成。");
        sb.AppendLine("- 引用已存在实体时优先使用其 Name（按名称精确匹配），必要时先查询列表确认名称。");
        sb.AppendLine("- 某步失败时，先分析工具返回的错误信息并尝试修正参数重试；确实无法完成时输出 final_answer 说明。");
        sb.AppendLine("- 任务完成后必须输出 final_answer 结束，不要继续调用无关工具。");
        sb.AppendLine();

        sb.AppendLine("## 可用工具清单");
        sb.AppendLine("格式: 工具名(参数)，参数名带 ! 为必填，[A|B] 为可选枚举值。");
        sb.AppendLine(AgentTool.BuildToolList(tools));
        sb.AppendLine();

        if (project is not null)
        {
            sb.AppendLine("## 当前项目概览");
            sb.AppendLine(BuildProjectSummary(project));
        }

        return sb.ToString();
    }

    /// <summary>
    /// 构建项目摘要（给模型提供决策上下文；大项目自动截断控制 token）。
    /// </summary>
    public static string BuildProjectSummary(HmiProject project)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"- 项目名称: {project.Name}");

        var screenNames = project.Screens.Select(s => s.Name).Take(15).ToList();
        var screenList = project.Screens.Count > 15
            ? string.Join("、", screenNames) + $" 等 {project.Screens.Count} 个"
            : string.Join("、", screenNames);
        sb.AppendLine($"- 屏幕 ({project.Screens.Count}): {screenList}");

        sb.AppendLine($"- 变量 ({project.VariableManager.Variables.Count})、通讯连接 ({project.VariableManager.Connections.Count})");
        sb.AppendLine($"- 脚本 ({project.Scripts.Count})、模板 ({project.Templates.Count})");
        sb.AppendLine($"- 报警组 ({project.AlarmGroups.Count})、趋势 ({project.TrendConfigs.Count})");
        return sb.ToString();
    }
}
