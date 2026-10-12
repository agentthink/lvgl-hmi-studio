using System.Text;
using System.Text.Json;
using HmiEditor.Core.Models;

namespace HmiEditor.AI.Agent.Tools;

/// <summary>
/// 查询类工具 —— 只读项目信息，供 Agent 决策与回答项目问题。
/// 返回紧凑 JSON 文本（模型消费），避免无关字段膨胀上下文。
/// </summary>
public static class ProjectQueryTools
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>构建全部查询工具。</summary>
    public static List<AgentTool> Create()
    {
        return new List<AgentTool>
        {
            QueryProject(),
            ListScreens(),
            GetScreen(),
            ListControls(),
            ListVariables(),
            ListConnections(),
            ListScripts(),
            ListTemplates(),
            ListAlarms(),
            ListTrends(),
            GetSecurityConfig(),
            GetRuntimeConfig(),
        };
    }

    private static AgentTool QueryProject() => new(
        "query_project",
        "查询当前 HMI 项目概览：名称、版本、屏幕/变量/脚本/模板/报警/趋势数量。无参数。",
        execute: static (_, ctx, _) => Task.FromResult(AgentToolResult.Success(BuildSummary(ctx.Project))));

    private static AgentTool ListScreens() => new(
        "list_screens",
        "列出所有屏幕：Id、名称、尺寸、控件数量、背景色。无参数。",
        execute: static (_, ctx, _) =>
        {
            var screens = ctx.Project.Screens.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                width = s.Width,
                height = s.Height,
                controlCount = s.Controls.Count,
                bgColor = s.BgColor
            });
            return Task.FromResult(AgentToolResult.Success(Json(screens)));
        },
        isReadOnly: true);

    private static AgentTool GetScreen() => new(
        "get_screen",
        "获取单个屏幕的详细信息（名称/尺寸/背景色/控件列表）。参数: screen_name(必填, 屏幕名称)。",
        execute: static (call, ctx, _) =>
        {
            var name = AgentOutput.GetString(call.Arguments, "screen_name");
            var screen = FindScreen(ctx.Project, name);
            if (screen is null)
                return Task.FromResult(AgentToolResult.Failure($"未找到屏幕 '{name}'。可用屏幕: {string.Join("、", ctx.Project.Screens.Select(s => s.Name))}"));

            var info = new
            {
                id = screen.Id,
                name = screen.Name,
                width = screen.Width,
                height = screen.Height,
                bgColor = screen.BgColor,
                controls = screen.Controls.Select(c => new
                {
                    name = c.Name,
                    type = c.Type.ToString(),
                    x = c.X, y = c.Y, width = c.Width, height = c.Height,
                    text = c.Text,
                    bgColor = c.BgColor,
                    textColor = c.TextColor,
                    binding = c.VariableBindingId
                })
            };
            return Task.FromResult(AgentToolResult.Success(Json(info)));
        },
        argumentsSchema: """
        { "type":"object", "properties": { "screen_name": { "type":"string" } }, "required":["screen_name"] }
        """,
        isReadOnly: true);

    private static AgentTool ListControls() => new(
        "list_controls",
        "列出指定屏幕上的所有控件。参数: screen_name(必填, 屏幕名称)。",
        execute: static (call, ctx, _) =>
        {
            var name = AgentOutput.GetString(call.Arguments, "screen_name");
            var screen = FindScreen(ctx.Project, name);
            if (screen is null)
                return Task.FromResult(AgentToolResult.Failure($"未找到屏幕 '{name}'。可用屏幕: {string.Join("、", ctx.Project.Screens.Select(s => s.Name))}"));

            var controls = screen.Controls.Select(c => new
            {
                name = c.Name,
                type = c.Type.ToString(),
                x = c.X, y = c.Y, width = c.Width, height = c.Height,
                text = c.Text, binding = c.VariableBindingId
            });
            return Task.FromResult(AgentToolResult.Success(Json(controls)));
        },
        argumentsSchema: """
        { "type":"object", "properties": { "screen_name": { "type":"string" } }, "required":["screen_name"] }
        """,
        isReadOnly: true);

    private static AgentTool ListVariables() => new(
        "list_variables",
        "列出所有变量：名称、数据类型、地址、所属连接、单位、模拟值。无参数。",
        execute: static (_, ctx, _) =>
        {
            var vars = ctx.Project.VariableManager.Variables.Select(v => new
            {
                name = v.Name,
                dataType = v.DataType,
                address = v.Address,
                connection = ctx.Project.VariableManager.Connections.FirstOrDefault(c => c.Id == v.ConnectionId)?.Name,
                unit = v.Unit,
                simulated = v.SimulatedValue
            });
            return Task.FromResult(AgentToolResult.Success(Json(vars)));
        },
        isReadOnly: true);

    private static AgentTool ListConnections() => new(
        "list_connections",
        "列出所有通讯连接：名称、驱动、启用状态、关键配置。无参数。",
        execute: static (_, ctx, _) =>
        {
            var conns = ctx.Project.VariableManager.Connections.Select(c => new
            {
                name = c.Name,
                driver = c.DriverId,
                enabled = c.IsEnabled,
                config = c.Config
            });
            return Task.FromResult(AgentToolResult.Success(Json(conns)));
        },
        isReadOnly: true);

    private static AgentTool ListScripts() => new(
        "list_scripts",
        "列出所有 Lua 脚本：名称、分类、是否启用、代码长度、说明。无参数。",
        execute: static (_, ctx, _) =>
        {
            var scripts = ctx.Project.Scripts.Select(s => new
            {
                name = s.Name,
                category = s.Category,
                enabled = s.IsEnabled,
                codeLength = s.Code.Length,
                description = s.Description
            });
            return Task.FromResult(AgentToolResult.Success(Json(scripts)));
        },
        isReadOnly: true);

    private static AgentTool ListTemplates() => new(
        "list_templates",
        "列出所有 Faceplate 模板：名称、分类、控件数、参数数。无参数。",
        execute: static (_, ctx, _) =>
        {
            var templates = ctx.Project.Templates.Select(t => new
            {
                name = t.Name,
                category = t.Category,
                controlCount = t.Controls.Count,
                parameterCount = t.Parameters.Count
            });
            return Task.FromResult(AgentToolResult.Success(Json(templates)));
        },
        isReadOnly: true);

    private static AgentTool ListAlarms() => new(
        "list_alarms",
        "列出所有报警配置：名称、严重级别、是否启用、条件绑定。无参数。",
        execute: static (_, ctx, _) =>
        {
            var alarms = ctx.Project.AlarmGroups
                .SelectMany(g => g.Alarms)
                .Select(a => new
                {
                    name = a.Name,
                    severity = a.Severity.ToString(),
                    enabled = a.IsEnabled,
                    condition = a.Condition?.VariableId
                });
            return Task.FromResult(AgentToolResult.Success(Json(alarms)));
        },
        isReadOnly: true);

    private static AgentTool ListTrends() => new(
        "list_trends",
        "列出所有趋势配置：名称、采样周期、通道(画笔)数。无参数。",
        execute: static (_, ctx, _) =>
        {
            var trends = ctx.Project.TrendConfigs.Select(t => new
            {
                name = t.Name,
                interval = t.TimeWindowMs,
                penCount = t.Pens.Count
            });
            return Task.FromResult(AgentToolResult.Success(Json(trends)));
        },
        isReadOnly: true);

    private static AgentTool GetSecurityConfig() => new(
        "get_security",
        "查询安全策略配置：操作模式、会话超时、登录尝试上限、审计日志开关、锁屏界面。无参数。",
        execute: static (_, ctx, _) => Task.FromResult(AgentToolResult.Success(Json(ctx.Project.SecurityConfig))),
        isReadOnly: true);

    private static AgentTool GetRuntimeConfig() => new(
        "get_runtime_config",
        "查询运行时配置：扫描周期、看门狗、日志级别、启停控制。无参数。",
        execute: static (_, ctx, _) => Task.FromResult(AgentToolResult.Success(Json(ctx.Project.RuntimeConfig))),
        isReadOnly: true);

    // ── 辅助 ──

    private static string BuildSummary(HmiProject project)
    {
        var sb = new StringBuilder();
        sb.Append($"项目 '{project.Name}' (版本 {project.ProjectVersion}): ");
        sb.Append($"{project.Screens.Count} 屏幕, {project.VariableManager.Variables.Count} 变量, ");
        sb.Append($"{project.VariableManager.Connections.Count} 连接, {project.Scripts.Count} 脚本, ");
        sb.Append($"{project.Templates.Count} 模板, {project.AlarmGroups.Count} 报警组, {project.TrendConfigs.Count} 趋势。");
        return sb.ToString();
    }

    private static HmiScreen? FindScreen(HmiProject project, string name) =>
        project.Screens.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) ??
        project.Screens.FirstOrDefault(s => s.Id == name);

    private static string Json(object value) => JsonSerializer.Serialize(value, JsonOpts);
}
