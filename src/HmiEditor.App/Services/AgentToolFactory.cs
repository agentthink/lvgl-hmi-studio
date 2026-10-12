using System.Text;
using System.Text.Json;
using Hmi.Communication.Builtin;
using HmiEditor.AI;
using HmiEditor.AI.Agent;
using HmiEditor.AI.Agent.Tools;
using HmiEditor.AI.Generators;
using HmiEditor.AI.Inference;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Recipe;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Services;

/// <summary>
/// Agent 工具工厂 —— 组装完整工具集（查询 + 写 + 导航 + 用户/配方）。
/// 查询工具来自 AI 层（ProjectQueryTools）；写类工具在此实现，绑定项目操作、
/// 快照钩子与 UI 刷新钩子。
/// </summary>
public static class AgentToolFactory
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>工具名 → 中文展示名（用于 UI 轨迹展示）。</summary>
    public static readonly IReadOnlyDictionary<string, string> ToolDisplayNames =
        new Dictionary<string, string>
        {
            ["query_project"] = "查询项目概览",
            ["list_screens"] = "列出屏幕",
            ["get_screen"] = "查看屏幕详情",
            ["list_controls"] = "列出控件",
            ["list_variables"] = "列出变量",
            ["list_connections"] = "列出通讯连接",
            ["list_scripts"] = "列出脚本",
            ["list_templates"] = "列出模板",
            ["list_alarms"] = "列出报警",
            ["list_trends"] = "列出趋势",
            ["get_security"] = "查询安全配置",
            ["get_runtime_config"] = "查询运行时配置",
            ["list_users"] = "列出用户",
            ["list_recipes"] = "列出配方",
            ["create_screen"] = "新建屏幕",
            ["rename_screen"] = "重命名屏幕",
            ["delete_screen"] = "删除屏幕",
            ["add_control"] = "添加控件",
            ["add_controls_batch"] = "批量添加控件",
            ["update_control"] = "修改控件",
            ["delete_control"] = "删除控件",
            ["create_variable"] = "新建变量",
            ["create_variables_batch"] = "批量新建变量",
            ["update_variable"] = "修改变量",
            ["delete_variable"] = "删除变量",
            ["create_template"] = "新建模板",
            ["add_control_to_template"] = "模板添加控件",
            ["update_template"] = "修改模板",
            ["delete_template"] = "删除模板",
            ["create_trend"] = "新建趋势",
            ["update_trend"] = "修改趋势",
            ["delete_trend"] = "删除趋势",
            ["create_connection"] = "新建通讯连接",
            ["update_connection"] = "修改通讯连接",
            ["create_alarm"] = "新建报警",
            ["update_security"] = "修改安全配置",
            ["update_runtime"] = "修改运行时配置",
            ["create_script"] = "新建脚本",
            ["update_script"] = "修改脚本",
            ["delete_script"] = "删除脚本",
            ["add_user"] = "新建用户",
            ["delete_user"] = "删除用户",
            ["create_recipe"] = "新建配方",
            ["open_panel"] = "打开面板",
            ["switch_screen"] = "切换屏幕",
            ["save_project"] = "保存项目",
            ["export_code"] = "导出代码",
            ["generate_image"] = "生成图片",
        };

    /// <summary>构建全部工具。</summary>
    public static List<AgentTool> CreateAll(
        AgentHostBridge bridge,
        Action<HmiProject, string> beforeWrite,
        Action afterWrite,
        ImageGenerator? imageGenerator = null,
        Action<string, int, int>? onImageGenerated = null)
    {
        var tools = new List<AgentTool>(ProjectQueryTools.Create());
        tools.AddRange(CreateMutationTools(bridge, beforeWrite, afterWrite));
        tools.AddRange(CreateUserRecipeTools(bridge, afterWrite));
        tools.AddRange(CreateNavigationTools(bridge));
        if (imageGenerator is not null)
            tools.Add(CreateGenerateImageTool(imageGenerator, onImageGenerated));
        return tools;
    }

    // ─────────────────────────── 项目建模 ───────────────────────────

    private static IEnumerable<AgentTool> CreateMutationTools(
        AgentHostBridge bridge, Action<HmiProject, string> beforeWrite, Action afterWrite)
    {
        yield return CreateScreenTool(bridge, beforeWrite, afterWrite);
        yield return RenameScreenTool(bridge, beforeWrite, afterWrite);
        yield return DeleteScreenTool(bridge, beforeWrite, afterWrite);
        yield return AddControlTool(bridge, beforeWrite, afterWrite);
        yield return AddControlsBatchTool(bridge, beforeWrite, afterWrite);
        yield return UpdateControlTool(bridge, beforeWrite, afterWrite);
        yield return DeleteControlTool(bridge, beforeWrite, afterWrite);
        yield return CreateVariableTool(bridge, beforeWrite, afterWrite);
        yield return CreateVariablesBatchTool(bridge, beforeWrite, afterWrite);
        yield return UpdateVariableTool(bridge, beforeWrite, afterWrite);
        yield return DeleteVariableTool(bridge, beforeWrite, afterWrite);
        yield return CreateTemplateTool(bridge, beforeWrite, afterWrite);
        yield return AddControlToTemplateTool(bridge, beforeWrite, afterWrite);
        yield return UpdateTemplateTool(bridge, beforeWrite, afterWrite);
        yield return DeleteTemplateTool(bridge, beforeWrite, afterWrite);
        yield return CreateTrendTool(bridge, beforeWrite, afterWrite);
        yield return UpdateTrendTool(bridge, beforeWrite, afterWrite);
        yield return DeleteTrendTool(bridge, beforeWrite, afterWrite);
        yield return CreateConnectionTool(bridge, beforeWrite, afterWrite);
        yield return UpdateConnectionTool(bridge, beforeWrite, afterWrite);
        yield return CreateAlarmTool(bridge, beforeWrite, afterWrite);
        yield return UpdateSecurityTool(bridge, beforeWrite, afterWrite);
        yield return UpdateRuntimeTool(bridge, beforeWrite, afterWrite);
        yield return CreateScriptTool(bridge, beforeWrite, afterWrite);
        yield return UpdateScriptTool(bridge, beforeWrite, afterWrite);
        yield return DeleteScriptTool(bridge, beforeWrite, afterWrite);
    }

    private static AgentTool CreateScreenTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("create_screen",
            "新建一个屏幕。参数: name(必填, 屏幕名称), width/height(尺寸, 不填则用工程设置里的屏幕默认尺寸), bgColor(背景色 #RRGGBB, 不填则用工程默认背景色)。",
            """
            { "type":"object", "properties": { "name":{"type":"string"}, "width":{"type":"number"}, "height":{"type":"number"}, "bgColor":{"type":"string"} }, "required":["name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(AgentToolResult.Failure("屏幕名称不能为空"));
                if (ctx.Project.Screens.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
                    return Task.FromResult(AgentToolResult.Failure($"屏幕 '{name}' 已存在"));

                bw(ctx.Project, call.Name);
                // 尺寸/背景色一律走 ScreenFactory：工具参数没给（null）时落到
                // ScreenDefaultsConfig，而不是各自写死的 1024×600。
                ctx.Project.Screens.Add(ScreenFactory.Create(
                    ctx.Project,
                    name: name,
                    width: AgentOutput.GetDoubleOrNull(call.Arguments, "width"),
                    height: AgentOutput.GetDoubleOrNull(call.Arguments, "height"),
                    bgColor: AgentOutput.GetStringOrNull(call.Arguments, "bgColor")));
                aw();
                return Task.FromResult(AgentToolResult.Success($"已创建屏幕 '{name}' ({(int)ctx.Project.Screens[^1].Width}x{(int)ctx.Project.Screens[^1].Height})", true));
            });

    private static AgentTool RenameScreenTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("rename_screen",
            "重命名屏幕。参数: name(必填, 原名称), new_name(必填, 新名称)。",
            """
            { "type":"object", "properties": { "name":{"type":"string"}, "new_name":{"type":"string"} }, "required":["name","new_name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                var newName = AgentOutput.GetString(call.Arguments, "new_name");
                var screen = FindScreen(ctx.Project, name);
                if (screen is null) return Task.FromResult(AgentToolResult.Failure($"未找到屏幕 '{name}'"));
                if (string.IsNullOrWhiteSpace(newName)) return Task.FromResult(AgentToolResult.Failure("新名称不能为空"));

                bw(ctx.Project, call.Name);
                screen.Name = newName;
                aw();
                return Task.FromResult(AgentToolResult.Success($"屏幕 '{name}' 已重命名为 '{newName}'", true));
            });

    private static AgentTool DeleteScreenTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("delete_screen",
            "删除屏幕。参数: name(必填, 屏幕名称)。项目中至少保留一个屏幕。",
            """
            { "type":"object", "properties": { "name":{"type":"string"} }, "required":["name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                var screen = FindScreen(ctx.Project, name);
                if (screen is null) return Task.FromResult(AgentToolResult.Failure($"未找到屏幕 '{name}'"));
                if (ctx.Project.Screens.Count <= 1) return Task.FromResult(AgentToolResult.Failure("项目至少保留一个屏幕，无法删除"));

                bw(ctx.Project, call.Name);
                ctx.Project.Screens.Remove(screen);
                aw();
                return Task.FromResult(AgentToolResult.Success($"已删除屏幕 '{name}'", true));
            });

    private static AgentTool CreateTrendTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("create_trend",
            "新建趋势图配置。参数: name(必填,趋势名), time_window_ms(时间窗口毫秒,默认60000), max_points(最大数据点,默认600), show_legend(默认true), show_grid(默认true), pens(曲线数组,每项{label,variable,color,line_width}, variable 为变量名)。",
            """
            { "type":"object", "properties": { "name":{"type":"string"}, "time_window_ms":{"type":"number"}, "max_points":{"type":"number"}, "show_legend":{"type":"boolean"}, "show_grid":{"type":"boolean"}, "pens":{"type":"array","items":{"type":"object"}} }, "required":["name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(AgentToolResult.Failure("趋势名称不能为空"));
                if (ctx.Project.TrendConfigs.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
                    return Task.FromResult(AgentToolResult.Failure($"趋势 '{name}' 已存在"));

                bw(ctx.Project, call.Name);
                var trend = new TrendConfig
                {
                    Name = name,
                    TimeWindowMs = AgentOutput.GetInt(call.Arguments, "time_window_ms", 60000),
                    MaxPoints = AgentOutput.GetInt(call.Arguments, "max_points", 600),
                    ShowLegend = AgentOutput.GetBool(call.Arguments, "show_legend", true),
                    ShowGrid = AgentOutput.GetBool(call.Arguments, "show_grid", true)
                };
                AppendTrendPens(trend, call.Arguments, ctx.Project);
                ctx.Project.TrendConfigs.Add(trend);
                aw();
                return Task.FromResult(AgentToolResult.Success($"已创建趋势 '{name}' ({trend.Pens.Count} 条曲线, 窗口{trend.TimeWindowMs}ms)", true));
            });

    private static AgentTool UpdateTrendTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("update_trend",
            "修改趋势配置。参数: name(必填,趋势名), time_window_ms, max_points, show_legend, show_grid, pens(覆盖式更新曲线列表)。",
            """
            { "type":"object", "properties": { "name":{"type":"string"}, "time_window_ms":{"type":"number"}, "max_points":{"type":"number"}, "show_legend":{"type":"boolean"}, "show_grid":{"type":"boolean"}, "pens":{"type":"array","items":{"type":"object"}} }, "required":["name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                var trend = ctx.Project.TrendConfigs.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
                if (trend is null) return Task.FromResult(AgentToolResult.Failure($"未找到趋势 '{name}'。可用: {string.Join("、", ctx.Project.TrendConfigs.Select(t => t.Name))}"));

                bw(ctx.Project, call.Name);
                if (call.Arguments.TryGetProperty("time_window_ms", out var _)) trend.TimeWindowMs = AgentOutput.GetInt(call.Arguments, "time_window_ms", trend.TimeWindowMs);
                if (call.Arguments.TryGetProperty("max_points", out var _)) trend.MaxPoints = AgentOutput.GetInt(call.Arguments, "max_points", trend.MaxPoints);
                if (call.Arguments.TryGetProperty("show_legend", out var _)) trend.ShowLegend = AgentOutput.GetBool(call.Arguments, "show_legend", trend.ShowLegend);
                if (call.Arguments.TryGetProperty("show_grid", out var _)) trend.ShowGrid = AgentOutput.GetBool(call.Arguments, "show_grid", trend.ShowGrid);
                if (call.Arguments.TryGetProperty("pens", out var _))
                {
                    trend.Pens.Clear();
                    AppendTrendPens(trend, call.Arguments, ctx.Project);
                }
                aw();
                return Task.FromResult(AgentToolResult.Success($"趋势 '{name}' 已更新", true));
            });

    private static AgentTool DeleteTrendTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("delete_trend",
            "删除趋势配置。参数: name(必填,趋势名)。",
            """
            { "type":"object", "properties": { "name":{"type":"string"} }, "required":["name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                var trend = ctx.Project.TrendConfigs.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
                if (trend is null) return Task.FromResult(AgentToolResult.Failure($"未找到趋势 '{name}'"));

                bw(ctx.Project, call.Name);
                ctx.Project.TrendConfigs.Remove(trend);
                aw();
                return Task.FromResult(AgentToolResult.Success($"已删除趋势 '{name}'", true));
            });

    /// <summary>从 arguments.pens 数组追加曲线到趋势, variable 视为变量名并解析为变量 Id。</summary>
    private static void AppendTrendPens(TrendConfig trend, JsonElement arguments, HmiProject project)
    {
        if (arguments.ValueKind != JsonValueKind.Object) return;
        if (!arguments.TryGetProperty("pens", out var pensEl) || pensEl.ValueKind != JsonValueKind.Array) return;
        foreach (var item in pensEl.EnumerateArray())
        {
            var label = AgentOutput.GetString(item, "label");
            var varRef = AgentOutput.GetString(item, "variable");
            trend.Pens.Add(new TrendPen
            {
                Label = string.IsNullOrEmpty(label) ? varRef : label,
                VariableId = ResolveVariableId(project, varRef),
                Color = AgentOutput.GetString(item, "color", "#38bdf8"),
                LineWidth = AgentOutput.GetInt(item, "line_width", 2)
            });
        }
    }

    /// <summary>按变量名解析变量 Id(找不到则原样返回变量名, 运行时容错)。</summary>
    private static string ResolveVariableId(HmiProject project, string variableName)
    {
        if (string.IsNullOrEmpty(variableName)) return "";
        var v = project.VariableManager.Variables.FirstOrDefault(x =>
            string.Equals(x.Name, variableName, StringComparison.OrdinalIgnoreCase));
        return v?.Id ?? variableName;
    }

    private static AgentTool AddControlTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("add_control",
            "向指定屏幕添加控件。参数: screen_name(必填), type(必填, 控件类型), name(必填, 控件名), x, y, width, height, text, bgColor, textColor, binding(变量绑定名), radius, opacity。",
            $$"""
            { "type":"object", "properties": { "screen_name":{"type":"string"}, "type":{"type":"string","enum":{{AiSchemaEnums.GeneratableControlTypesJson}}}, "name":{"type":"string"}, "x":{"type":"number"}, "y":{"type":"number"}, "width":{"type":"number"}, "height":{"type":"number"}, "text":{"type":"string"}, "bgColor":{"type":"string"}, "textColor":{"type":"string"}, "binding":{"type":"string"}, "radius":{"type":"number"}, "opacity":{"type":"number"} }, "required":["screen_name","type","name"] }
            """,
            (call, ctx, _) =>
            {
                var screen = FindScreen(ctx.Project, AgentOutput.GetString(call.Arguments, "screen_name"));
                if (screen is null) return Task.FromResult(AgentToolResult.Failure($"未找到屏幕 '{AgentOutput.GetString(call.Arguments, "screen_name")}'"));

                var typeName = AgentOutput.GetString(call.Arguments, "type");
                if (!AiSchemaEnums.TryParseGeneratableControlType(typeName, out var type))
                    return Task.FromResult(AgentToolResult.Failure($"无效控件类型 '{typeName}'。可选: {ControlTypeEnumList}"));

                var ctrlName = AgentOutput.GetString(call.Arguments, "name");
                if (string.IsNullOrWhiteSpace(ctrlName)) return Task.FromResult(AgentToolResult.Failure("控件名称不能为空"));

                bw(ctx.Project, call.Name);
                screen.Controls.Add(new HmiControl
                {
                    Type = type,
                    Name = ctrlName,
                    X = AgentOutput.GetDouble(call.Arguments, "x"),
                    Y = AgentOutput.GetDouble(call.Arguments, "y"),
                    Width = AgentOutput.GetDouble(call.Arguments, "width", 100),
                    Height = AgentOutput.GetDouble(call.Arguments, "height", 40),
                    Text = AgentOutput.GetString(call.Arguments, "text").IfNullOrEmpty(null),
                    BgColor = AgentOutput.GetString(call.Arguments, "bgColor").IfNullOrEmpty(null),
                    TextColor = AgentOutput.GetString(call.Arguments, "textColor").IfNullOrEmpty(null),
                    VariableBindingId = AgentOutput.GetString(call.Arguments, "binding").IfNullOrEmpty(null),
                    Radius = AgentOutput.GetInt(call.Arguments, "radius", 4),
                    Opacity = AgentOutput.GetDouble(call.Arguments, "opacity", 100)
                });
                aw();
                return Task.FromResult(AgentToolResult.Success($"已在屏幕 '{screen.Name}' 添加控件 '{ctrlName}' ({type})", true));
            });

    private static AgentTool AddControlsBatchTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("add_controls_batch",
            $"批量向屏幕添加控件（一次多个，成组界面务必用此工具而非逐个 add_control）。参数: screen_name(必填), controls(必填, 数组, 每项 {{type(必填, 可选: {ControlTypeEnumList}), name(必填), x, y, width, height, text, bgColor, textColor, binding(绑定变量名), radius, opacity}}), auto_grid(可选, 自动网格布局 {{columns(列数,默认2), x0(默认20), y0(默认20), item_width(默认200), item_height(默认80), gap_x(默认20), gap_y(默认20)}}; 提供后未显式指定 x/y 的控件按数组顺序自动入格，未指定宽高则取 item_width/item_height)。",
            ControlsBatchSchema.Replace("[[ENUM]]", AiSchemaEnums.GeneratableControlTypesJson),
            (call, ctx, _) =>
            {
                var screenName = AgentOutput.GetString(call.Arguments, "screen_name");
                var screen = FindScreen(ctx.Project, screenName);
                if (screen is null) return Task.FromResult(AgentToolResult.Failure($"未找到屏幕 '{screenName}'"));

                if (call.Arguments.ValueKind != JsonValueKind.Object ||
                    !call.Arguments.TryGetProperty("controls", out var ctrlsEl) ||
                    ctrlsEl.ValueKind != JsonValueKind.Array || ctrlsEl.GetArrayLength() == 0)
                    return Task.FromResult(AgentToolResult.Failure("controls 必须为非空数组"));

                // 解析可选自动网格
                double gridCols = 2, x0 = 20, y0 = 20, itemW = 200, itemH = 80, gapX = 20, gapY = 20;
                var hasGrid = false;
                if (call.Arguments.TryGetProperty("auto_grid", out var gridEl) && gridEl.ValueKind == JsonValueKind.Object)
                {
                    hasGrid = true;
                    gridCols = Math.Max(1, AgentOutput.GetDouble(gridEl, "columns", 2));
                    x0 = AgentOutput.GetDouble(gridEl, "x0", 20);
                    y0 = AgentOutput.GetDouble(gridEl, "y0", 20);
                    itemW = AgentOutput.GetDouble(gridEl, "item_width", 200);
                    itemH = AgentOutput.GetDouble(gridEl, "item_height", 80);
                    gapX = AgentOutput.GetDouble(gridEl, "gap_x", 20);
                    gapY = AgentOutput.GetDouble(gridEl, "gap_y", 20);
                }

                var pending = new List<HmiControl>();
                var errors = new List<string>();
                var index = 0;
                foreach (var item in ctrlsEl.EnumerateArray())
                {
                    var typeName = AgentOutput.GetString(item, "type");
                    if (!AiSchemaEnums.TryParseGeneratableControlType(typeName, out var type))
                    { errors.Add($"无效类型 '{typeName}'"); continue; }

                    var ctrlName = AgentOutput.GetString(item, "name");
                    if (string.IsNullOrWhiteSpace(ctrlName))
                    { errors.Add("(空名)"); continue; }

                    double x, y, w, h;
                    var hasX = Has(item, "x"); var hasY = Has(item, "y");
                    if (hasGrid && (!hasX || !hasY))
                    {
                        var col = index % (int)gridCols;
                        var row = index / (int)gridCols;
                        x = hasX ? AgentOutput.GetDouble(item, "x") : x0 + col * (itemW + gapX);
                        y = hasY ? AgentOutput.GetDouble(item, "y") : y0 + row * (itemH + gapY);
                    }
                    else
                    {
                        x = AgentOutput.GetDouble(item, "x");
                        y = AgentOutput.GetDouble(item, "y");
                    }

                    w = AgentOutput.GetDouble(item, "width", -1);
                    h = AgentOutput.GetDouble(item, "height", -1);
                    if (hasGrid && w < 0) w = itemW;
                    if (hasGrid && h < 0) h = itemH;

                    pending.Add(new HmiControl
                    {
                        Type = type,
                        Name = ctrlName,
                        X = x,
                        Y = y,
                        Width = w < 0 ? 100 : w,
                        Height = h < 0 ? 40 : h,
                        Text = AgentOutput.GetString(item, "text").IfNullOrEmpty(null),
                        BgColor = AgentOutput.GetString(item, "bgColor").IfNullOrEmpty(null),
                        TextColor = AgentOutput.GetString(item, "textColor").IfNullOrEmpty(null),
                        VariableBindingId = AgentOutput.GetString(item, "binding").IfNullOrEmpty(null),
                        Radius = AgentOutput.GetInt(item, "radius", 4),
                        Opacity = AgentOutput.GetDouble(item, "opacity", 100)
                    });
                    index++;
                }

                if (pending.Count == 0)
                    return Task.FromResult(AgentToolResult.Failure($"无有效控件: {string.Join("; ", errors)}"));

                bw(ctx.Project, call.Name);
                foreach (var c in pending)
                    screen.Controls.Add(c);
                aw();

                var summary = $"已在屏幕 '{screen.Name}' 批量添加 {pending.Count} 个控件";
                if (hasGrid) summary += $"（自动网格 {gridCols} 列）";
                if (errors.Count > 0) summary += $"；跳过 {errors.Count} 个无效项: {string.Join("; ", errors)}";
                return Task.FromResult(AgentToolResult.Success(summary, true));
            });

    private static AgentTool UpdateControlTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("update_control",
            "修改指定屏幕上的控件属性。参数: screen_name(必填), control_name(必填), 及任意可修改属性: x,y,width,height,text,bgColor,textColor,binding,radius,opacity。",
            """
            { "type":"object", "properties": { "screen_name":{"type":"string"}, "control_name":{"type":"string"}, "x":{"type":"number"}, "y":{"type":"number"}, "width":{"type":"number"}, "height":{"type":"number"}, "text":{"type":"string"}, "bgColor":{"type":"string"}, "textColor":{"type":"string"}, "binding":{"type":"string"}, "radius":{"type":"number"}, "opacity":{"type":"number"} }, "required":["screen_name","control_name"] }
            """,
            (call, ctx, _) =>
            {
                var screen = FindScreen(ctx.Project, AgentOutput.GetString(call.Arguments, "screen_name"));
                if (screen is null) return Task.FromResult(AgentToolResult.Failure($"未找到屏幕 '{AgentOutput.GetString(call.Arguments, "screen_name")}'"));

                var ctrlName = AgentOutput.GetString(call.Arguments, "control_name");
                var ctrl = screen.Controls.FirstOrDefault(c => string.Equals(c.Name, ctrlName, StringComparison.OrdinalIgnoreCase));
                if (ctrl is null) return Task.FromResult(AgentToolResult.Failure($"屏幕 '{screen.Name}' 未找到控件 '{ctrlName}'"));

                var args = call.Arguments;
                bw(ctx.Project, call.Name);
                var applied = new List<string>();

                if (Has(args, "x")) { ctrl.X = GetD(args, "x"); applied.Add("x"); }
                if (Has(args, "y")) { ctrl.Y = GetD(args, "y"); applied.Add("y"); }
                if (Has(args, "width")) { ctrl.Width = GetD(args, "width"); applied.Add("width"); }
                if (Has(args, "height")) { ctrl.Height = GetD(args, "height"); applied.Add("height"); }
                if (Has(args, "text")) { ctrl.Text = GetS(args, "text").IfNullOrEmpty(null); applied.Add("text"); }
                if (Has(args, "bgColor")) { ctrl.BgColor = GetS(args, "bgColor").IfNullOrEmpty(null); applied.Add("bgColor"); }
                if (Has(args, "textColor")) { ctrl.TextColor = GetS(args, "textColor").IfNullOrEmpty(null); applied.Add("textColor"); }
                if (Has(args, "binding")) { ctrl.VariableBindingId = GetS(args, "binding").IfNullOrEmpty(null); applied.Add("binding"); }
                if (Has(args, "radius")) { ctrl.Radius = GetI(args, "radius"); applied.Add("radius"); }
                if (Has(args, "opacity")) { ctrl.Opacity = GetD(args, "opacity"); applied.Add("opacity"); }

                if (applied.Count == 0) return Task.FromResult(AgentToolResult.Failure("未提供任何可修改的属性"));

                aw();
                return Task.FromResult(AgentToolResult.Success($"已修改控件 '{ctrlName}': {string.Join(", ", applied)}", true));
            });

    private static AgentTool DeleteControlTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("delete_control",
            "删除指定屏幕上的控件。参数: screen_name(必填), control_name(必填)。",
            """
            { "type":"object", "properties": { "screen_name":{"type":"string"}, "control_name":{"type":"string"} }, "required":["screen_name","control_name"] }
            """,
            (call, ctx, _) =>
            {
                var screen = FindScreen(ctx.Project, AgentOutput.GetString(call.Arguments, "screen_name"));
                if (screen is null) return Task.FromResult(AgentToolResult.Failure($"未找到屏幕 '{AgentOutput.GetString(call.Arguments, "screen_name")}'"));

                var ctrlName = AgentOutput.GetString(call.Arguments, "control_name");
                var ctrl = screen.Controls.FirstOrDefault(c => string.Equals(c.Name, ctrlName, StringComparison.OrdinalIgnoreCase));
                if (ctrl is null) return Task.FromResult(AgentToolResult.Failure($"屏幕 '{screen.Name}' 未找到控件 '{ctrlName}'"));

                bw(ctx.Project, call.Name);
                screen.Controls.Remove(ctrl);
                aw();
                return Task.FromResult(AgentToolResult.Success($"已删除控件 '{ctrlName}'", true));
            });

    private static AgentTool CreateVariableTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("create_variable",
            "新建变量。参数: name(必填, 变量名), data_type(数据类型, 如 Int16/Int32/Float/Bool/String, 默认 Int16), address(寄存器地址), connection_name(所属连接名), unit(单位), poll_rate_ms(轮询周期), group_name(变量分组名, 不存在则自动创建, 缺省用第一个分组)。",
            """
            { "type":"object", "properties": { "name":{"type":"string"}, "data_type":{"type":"string"}, "address":{"type":"string"}, "connection_name":{"type":"string"}, "unit":{"type":"string"}, "poll_rate_ms":{"type":"number"}, "group_name":{"type":"string"} }, "required":["name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(AgentToolResult.Failure("变量名不能为空"));
                if (ctx.Project.VariableManager.Variables.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)))
                    return Task.FromResult(AgentToolResult.Failure($"变量 '{name}' 已存在"));

                var connName = AgentOutput.GetString(call.Arguments, "connection_name");
                var conn = string.IsNullOrWhiteSpace(connName)
                    ? null
                    : ctx.Project.VariableManager.Connections.FirstOrDefault(c => string.Equals(c.Name, connName, StringComparison.OrdinalIgnoreCase));

                bw(ctx.Project, call.Name);
                var groupId = ResolveTargetGroupId(ctx.Project.VariableManager,
                    AgentOutput.GetString(call.Arguments, "group_name").IfNullOrEmpty(null));
                ctx.Project.VariableManager.Variables.Add(new Variable
                {
                    Name = name,
                    DataType = AgentOutput.GetString(call.Arguments, "data_type", "Int16"),
                    Address = AgentOutput.GetString(call.Arguments, "address"),
                    ConnectionId = conn?.Id ?? "",
                    Unit = AgentOutput.GetString(call.Arguments, "unit").IfNullOrEmpty(null),
                    PollRateMs = AgentOutput.GetInt(call.Arguments, "poll_rate_ms", 500),
                    GroupId = groupId
                });
                aw();
                return Task.FromResult(AgentToolResult.Success($"已创建变量 '{name}' (DataType={AgentOutput.GetString(call.Arguments, "data_type", "Int16")})", true));
            });

    /// <summary>
    /// 解析 AI 建变量的目标分组：指定 group_name 则按名查找（不存在则创建）；
    /// 未指定则用第一个分组，无分组时创建默认分组。确保变量始终归属某个分组，
    /// 否则 GroupId 为空的变量在变量管理窗口（按分组过滤显示）中不可见。
    /// </summary>
    private static string ResolveTargetGroupId(VariableManager vm, string? groupName)
    {
        VariableGroup group;
        if (!string.IsNullOrWhiteSpace(groupName))
        {
            var name = groupName.Trim();
            group = vm.Groups.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase))
                    ?? new VariableGroup { Name = name };
            if (!vm.Groups.Contains(group)) vm.Groups.Add(group);
        }
        else
        {
            group = vm.Groups.FirstOrDefault() ?? new VariableGroup { Name = "默认分组" };
            if (!vm.Groups.Contains(group)) vm.Groups.Add(group);
        }
        return group.Id;
    }

    private static AgentTool CreateVariablesBatchTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("create_variables_batch",
            "批量新建变量（一次创建多个，成组变量务必用此工具而非逐个 create_variable）。参数: variables(必填, 数组, 每项 {name(必填), data_type(Int16/Int32/Float/Bool/String, 默认 Int16), address, connection_name, unit, poll_rate_ms, group_name}), 或顶层 group_name 指定统一分组(不存在则自动创建, 缺省用第一个分组)。重名变量自动跳过。",
            """
            { "type":"object", "properties": { "group_name":{"type":"string"}, "variables":{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"},"data_type":{"type":"string"},"address":{"type":"string"},"connection_name":{"type":"string"},"unit":{"type":"string"},"poll_rate_ms":{"type":"number"},"group_name":{"type":"string"}},"required":["name"]}} }, "required":["variables"] }
            """,
            (call, ctx, _) =>
            {
                if (call.Arguments.ValueKind != JsonValueKind.Object ||
                    !call.Arguments.TryGetProperty("variables", out var varsEl) ||
                    varsEl.ValueKind != JsonValueKind.Array || varsEl.GetArrayLength() == 0)
                    return Task.FromResult(AgentToolResult.Failure("variables 必须为非空数组"));

                var topGroupName = AgentOutput.GetString(call.Arguments, "group_name").IfNullOrEmpty(null);
                var pending = new List<Variable>();
                var skipped = new List<string>();
                foreach (var item in varsEl.EnumerateArray())
                {
                    var name = AgentOutput.GetString(item, "name");
                    if (string.IsNullOrWhiteSpace(name)) { skipped.Add("(空名)"); continue; }
                    if (ctx.Project.VariableManager.Variables.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)))
                    { skipped.Add(name); continue; }

                    var connName = AgentOutput.GetString(item, "connection_name");
                    var conn = string.IsNullOrWhiteSpace(connName)
                        ? null
                        : ctx.Project.VariableManager.Connections.FirstOrDefault(c => string.Equals(c.Name, connName, StringComparison.OrdinalIgnoreCase));

                    pending.Add(new Variable
                    {
                        Name = name,
                        DataType = AgentOutput.GetString(item, "data_type", "Int16"),
                        Address = AgentOutput.GetString(item, "address"),
                        ConnectionId = conn?.Id ?? "",
                        Unit = AgentOutput.GetString(item, "unit").IfNullOrEmpty(null),
                        PollRateMs = AgentOutput.GetInt(item, "poll_rate_ms", 500),
                        GroupId = ResolveTargetGroupId(ctx.Project.VariableManager,
                            AgentOutput.GetString(item, "group_name").IfNullOrEmpty(topGroupName))
                    });
                }

                if (pending.Count == 0)
                    return Task.FromResult(AgentToolResult.Failure($"全部变量均已存在，未创建: {string.Join(", ", skipped)}"));

                bw(ctx.Project, call.Name);
                foreach (var v in pending)
                    ctx.Project.VariableManager.Variables.Add(v);
                aw();

                var summary = $"已批量创建 {pending.Count} 个变量: {string.Join(", ", pending.Select(v => v.Name))}";
                if (skipped.Count > 0) summary += $"（跳过已存在: {string.Join(", ", skipped)}）";
                return Task.FromResult(AgentToolResult.Success(summary, true));
            });

    private static AgentTool UpdateVariableTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("update_variable",
            "修改变量属性。参数: name(必填, 变量名), 及任意: address, data_type, unit, connection_name, poll_rate_ms。",
            """
            { "type":"object", "properties": { "name":{"type":"string"}, "address":{"type":"string"}, "data_type":{"type":"string"}, "unit":{"type":"string"}, "connection_name":{"type":"string"}, "poll_rate_ms":{"type":"number"} }, "required":["name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                var v = ctx.Project.VariableManager.Variables.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (v is null) return Task.FromResult(AgentToolResult.Failure($"未找到变量 '{name}'"));

                var args = call.Arguments;
                bw(ctx.Project, call.Name);
                var applied = new List<string>();

                if (Has(args, "address")) { v.Address = GetS(args, "address"); applied.Add("address"); }
                if (Has(args, "data_type")) { v.DataType = GetS(args, "data_type"); applied.Add("data_type"); }
                if (Has(args, "unit")) { v.Unit = GetS(args, "unit").IfNullOrEmpty(null); applied.Add("unit"); }
                if (Has(args, "connection_name"))
                {
                    var conn = ctx.Project.VariableManager.Connections.FirstOrDefault(c =>
                        string.Equals(c.Name, GetS(args, "connection_name"), StringComparison.OrdinalIgnoreCase));
                    v.ConnectionId = conn?.Id ?? "";
                    applied.Add("connection");
                }
                if (Has(args, "poll_rate_ms")) { v.PollRateMs = GetI(args, "poll_rate_ms"); applied.Add("poll_rate_ms"); }

                if (applied.Count == 0) return Task.FromResult(AgentToolResult.Failure("未提供任何可修改的属性"));

                aw();
                return Task.FromResult(AgentToolResult.Success($"已修改变量 '{name}': {string.Join(", ", applied)}", true));
            });

    private static AgentTool DeleteVariableTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("delete_variable",
            "删除变量。参数: name(必填, 变量名)。",
            """
            { "type":"object", "properties": { "name":{"type":"string"} }, "required":["name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                var v = ctx.Project.VariableManager.Variables.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (v is null) return Task.FromResult(AgentToolResult.Failure($"未找到变量 '{name}'"));

                bw(ctx.Project, call.Name);
                ctx.Project.VariableManager.Variables.Remove(v);
                aw();
                return Task.FromResult(AgentToolResult.Success($"已删除变量 '{name}'", true));
            });

    private static AgentTool CreateTemplateTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("create_template",
            "新建 Faceplate 模板（空模板，不含控件）。参数: name(必填, 模板名), category(分类), description(说明)。",
            """
            { "type":"object", "properties": { "name":{"type":"string"}, "category":{"type":"string"}, "description":{"type":"string"} }, "required":["name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(AgentToolResult.Failure("模板名不能为空"));

                bw(ctx.Project, call.Name);
                ctx.Project.Templates.Add(new ControlTemplate
                {
                    Name = name,
                    Category = AgentOutput.GetString(call.Arguments, "category", "通用"),
                    Description = AgentOutput.GetString(call.Arguments, "description")
                });
                aw();
                return Task.FromResult(AgentToolResult.Success($"已创建模板 '{name}'", true));
            });

    private static AgentTool AddControlToTemplateTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("add_control_to_template",
            "向 Faceplate 模板添加控件(相对坐标，左上角0,0)。参数: template_name(必填), type(必填,控件类型), name(必填,控件名), x, y, width, height, text, bgColor, textColor, binding(变量绑定名), radius, opacity。",
            $$"""
            { "type":"object", "properties": { "template_name":{"type":"string"}, "type":{"type":"string","enum":{{AiSchemaEnums.GeneratableControlTypesJson}}}, "name":{"type":"string"}, "x":{"type":"number"}, "y":{"type":"number"}, "width":{"type":"number"}, "height":{"type":"number"}, "text":{"type":"string"}, "bgColor":{"type":"string"}, "textColor":{"type":"string"}, "binding":{"type":"string"}, "radius":{"type":"number"}, "opacity":{"type":"number"} }, "required":["template_name","type","name"] }
            """,
            (call, ctx, _) =>
            {
                var tmplName = AgentOutput.GetString(call.Arguments, "template_name");
                var template = FindTemplate(ctx.Project, tmplName);
                if (template is null) return Task.FromResult(AgentToolResult.Failure($"未找到模板 '{tmplName}'。可用: {string.Join("、", ctx.Project.Templates.Select(t => t.Name))}"));

                var typeName = AgentOutput.GetString(call.Arguments, "type");
                if (!AiSchemaEnums.TryParseGeneratableControlType(typeName, out var type))
                    return Task.FromResult(AgentToolResult.Failure($"无效控件类型 '{typeName}'。可选: {ControlTypeEnumList}"));

                var ctrlName = AgentOutput.GetString(call.Arguments, "name");
                if (string.IsNullOrWhiteSpace(ctrlName)) return Task.FromResult(AgentToolResult.Failure("控件名称不能为空"));

                bw(ctx.Project, call.Name);
                template.Controls.Add(new HmiControl
                {
                    Type = type,
                    Name = ctrlName,
                    X = AgentOutput.GetDouble(call.Arguments, "x"),
                    Y = AgentOutput.GetDouble(call.Arguments, "y"),
                    Width = AgentOutput.GetDouble(call.Arguments, "width", 100),
                    Height = AgentOutput.GetDouble(call.Arguments, "height", 40),
                    Text = AgentOutput.GetString(call.Arguments, "text").IfNullOrEmpty(null),
                    BgColor = AgentOutput.GetString(call.Arguments, "bgColor").IfNullOrEmpty(null),
                    TextColor = AgentOutput.GetString(call.Arguments, "textColor").IfNullOrEmpty(null),
                    VariableBindingId = AgentOutput.GetString(call.Arguments, "binding").IfNullOrEmpty(null),
                    Radius = AgentOutput.GetInt(call.Arguments, "radius", 4),
                    Opacity = AgentOutput.GetDouble(call.Arguments, "opacity", 100)
                });
                aw();
                return Task.FromResult(AgentToolResult.Success($"已向模板 '{template.Name}' 添加控件 '{ctrlName}' ({type})", true));
            });

    private static AgentTool UpdateTemplateTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("update_template",
            "修改模板基础属性。参数: template_name(必填,模板名), name(新名称), category(分类), description(说明)。",
            """
            { "type":"object", "properties": { "template_name":{"type":"string"}, "name":{"type":"string"}, "category":{"type":"string"}, "description":{"type":"string"} }, "required":["template_name"] }
            """,
            (call, ctx, _) =>
            {
                var tmplName = AgentOutput.GetString(call.Arguments, "template_name");
                var template = FindTemplate(ctx.Project, tmplName);
                if (template is null) return Task.FromResult(AgentToolResult.Failure($"未找到模板 '{tmplName}'"));

                bw(ctx.Project, call.Name);
                var newName = AgentOutput.GetString(call.Arguments, "name");
                if (!string.IsNullOrWhiteSpace(newName)) template.Name = newName;
                var cat = AgentOutput.GetString(call.Arguments, "category");
                if (!string.IsNullOrEmpty(cat)) template.Category = cat;
                if (call.Arguments.TryGetProperty("description", out var descEl) && descEl.ValueKind == JsonValueKind.String)
                    template.Description = descEl.GetString() ?? "";
                template.Version++;
                aw();
                return Task.FromResult(AgentToolResult.Success($"模板 '{template.Name}' 已更新 (v{template.Version})", true));
            });

    private static AgentTool DeleteTemplateTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("delete_template",
            "删除模板。参数: template_name(必填,模板名)。",
            """
            { "type":"object", "properties": { "template_name":{"type":"string"} }, "required":["template_name"] }
            """,
            (call, ctx, _) =>
            {
                var tmplName = AgentOutput.GetString(call.Arguments, "template_name");
                var template = FindTemplate(ctx.Project, tmplName);
                if (template is null) return Task.FromResult(AgentToolResult.Failure($"未找到模板 '{tmplName}'"));

                bw(ctx.Project, call.Name);
                ctx.Project.Templates.Remove(template);
                aw();
                return Task.FromResult(AgentToolResult.Success($"已删除模板 '{template.Name}'", true));
            });

    /// <summary>按名称(或 Id)查找模板。</summary>
    private static ControlTemplate? FindTemplate(HmiProject project, string name) =>
        project.Templates.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) ??
        project.Templates.FirstOrDefault(t => t.Id == name);

    // ─────────────────────────── 工程配置 ───────────────────────────

    private static AgentTool CreateConnectionTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("create_connection",
            "新建通讯连接。参数: name(必填, 连接名), driver(必填, " + string.Join("/", BuiltinDriverCatalog.Ids) + "), config(配置键值对象, 如 {\"host\":\"192.168.1.10\",\"port\":\"502\"})。",
            $$"""
            { "type":"object", "properties": { "name":{"type":"string"}, "driver":{"type":"string","enum":{{AiSchemaEnums.JsonArray(BuiltinDriverCatalog.Ids)}}}, "config":{"type":"object"} }, "required":["name","driver"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                var driver = AgentOutput.GetString(call.Arguments, "driver");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(driver))
                    return Task.FromResult(AgentToolResult.Failure("连接名与驱动不能为空"));

                // 驱动 Id 必须在 BuiltinDriverCatalog 里 —— 与 schema 的 enum 同源。
                // 此前无校验：模型给一个 schema 里有、产品侧却没有的名字（如 opcua，真值是 opc-ua），
                // 工具会**成功**返回，而连接落到一个匹配不到任何驱动的 DriverId 上（静默坏数据）。
                var driverId = NormalizeDriver(driver);
                if (!BuiltinDriverCatalog.Ids.Contains(driverId))
                    return Task.FromResult(AgentToolResult.Failure(
                        $"无效驱动 '{driver}'。可选: {string.Join("/", BuiltinDriverCatalog.Ids)}"));
                if (ctx.Project.VariableManager.Connections.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                    return Task.FromResult(AgentToolResult.Failure($"连接 '{name}' 已存在"));

                bw(ctx.Project, call.Name);
                var config = ParseConfigObject(call.Arguments);
                ctx.Project.VariableManager.Connections.Add(new DeviceConnection
                {
                    Name = name,
                    DriverId = driverId,
                    Config = config
                });
                aw();
                return Task.FromResult(AgentToolResult.Success($"已创建通讯连接 '{name}' (驱动 {driverId})", true));
            });

    private static AgentTool UpdateConnectionTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("update_connection",
            "修改通讯连接。参数: name(必填, 连接名), enabled(是否启用), config(配置键值对象)。",
            """
            { "type":"object", "properties": { "name":{"type":"string"}, "enabled":{"type":"boolean"}, "config":{"type":"object"} }, "required":["name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                var conn = ctx.Project.VariableManager.Connections.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                if (conn is null) return Task.FromResult(AgentToolResult.Failure($"未找到连接 '{name}'"));

                bw(ctx.Project, call.Name);
                var applied = new List<string>();
                if (Has(call.Arguments, "enabled"))
                {
                    conn.IsEnabled = AgentOutput.GetBool(call.Arguments, "enabled");
                    applied.Add("enabled");
                }
                if (Has(call.Arguments, "config"))
                {
                    foreach (var (k, v) in ParseConfigObject(call.Arguments))
                        conn.Config[k] = v;
                    applied.Add("config");
                }
                if (applied.Count == 0) return Task.FromResult(AgentToolResult.Failure("未提供任何可修改属性"));

                aw();
                return Task.FromResult(AgentToolResult.Success($"已修改连接 '{name}': {string.Join(", ", applied)}", true));
            });

    private static AgentTool CreateAlarmTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("create_alarm",
            $"新建报警。参数: name(必填, 报警名), severity(级别: {AiSchemaEnums.Csv<AlarmSeverity>()}, 默认 Warning), group_name(组名, 默认'默认'), binding(绑定变量名), threshold(阈值), type(条件: {AiSchemaEnums.Csv<AlarmConditionType>()}, 默认 HighLimit), description。",
            $$"""
            { "type":"object", "properties": { "name":{"type":"string"}, "severity":{"type":"string","enum":{{AiSchemaEnums.JsonArray<AlarmSeverity>()}}}, "group_name":{"type":"string"}, "binding":{"type":"string"}, "threshold":{"type":"number"}, "type":{"type":"string","enum":{{AiSchemaEnums.JsonArray<AlarmConditionType>()}}}, "description":{"type":"string"} }, "required":["name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(AgentToolResult.Failure("报警名不能为空"));

                var groupName = AgentOutput.GetString(call.Arguments, "group_name", "默认");
                var group = ctx.Project.AlarmGroups.FirstOrDefault(g => string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase));

                bw(ctx.Project, call.Name);
                group ??= AddAlarmGroup(ctx.Project, groupName);

                var alarm = new AlarmConfig
                {
                    Name = name,
                    Description = AgentOutput.GetString(call.Arguments, "description").IfNullOrEmpty(null),
                    Severity = ParseEnum<AlarmSeverity>(AgentOutput.GetString(call.Arguments, "severity", "Warning"), AlarmSeverity.Warning),
                    GroupId = group.Id,
                    IsEnabled = true
                };
                var binding = AgentOutput.GetString(call.Arguments, "binding");
                if (!string.IsNullOrWhiteSpace(binding))
                {
                    alarm.Condition.VariableId = binding;
                    alarm.Condition.Type = ParseEnum<AlarmConditionType>(
                        AgentOutput.GetString(call.Arguments, "type", "HighLimit"), AlarmConditionType.HighLimit);
                    alarm.Condition.Threshold = AgentOutput.GetDouble(call.Arguments, "threshold");
                }
                group.Alarms.Add(alarm);

                aw();
                return Task.FromResult(AgentToolResult.Success($"已在组 '{groupName}' 创建报警 '{name}'", true));
            });

    private static AgentTool UpdateSecurityTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("update_security",
            $"修改安全策略。可选参数: session_timeout_min(会话超时分钟), max_login_attempts(最大登录尝试), auto_logout(自动登出), enable_audit_log(审计日志), operation_mode({AiSchemaEnums.Csv<OperationMode>()}), password_min_length, lock_screen_id。",
            $$"""
            { "type":"object", "properties": { "session_timeout_min":{"type":"number"}, "max_login_attempts":{"type":"number"}, "auto_logout":{"type":"boolean"}, "enable_audit_log":{"type":"boolean"}, "operation_mode":{"type":"string","enum":{{AiSchemaEnums.JsonArray<OperationMode>()}}}, "password_min_length":{"type":"number"}, "lock_screen_id":{"type":"string"} }, "required":[] }
            """,
            (call, ctx, _) =>
            {
                var sc = ctx.Project.SecurityConfig;
                var args = call.Arguments;
                var applied = new List<string>();

                bw(ctx.Project, call.Name);
                if (Has(args, "session_timeout_min")) { sc.SessionTimeoutMin = GetI(args, "session_timeout_min"); applied.Add("session_timeout_min"); }
                if (Has(args, "max_login_attempts")) { sc.MaxLoginAttempts = GetI(args, "max_login_attempts"); applied.Add("max_login_attempts"); }
                if (Has(args, "auto_logout")) { sc.AutoLogoutEnabled = GetB(args, "auto_logout"); applied.Add("auto_logout"); }
                if (Has(args, "enable_audit_log")) { sc.EnableAuditLog = GetB(args, "enable_audit_log"); applied.Add("enable_audit_log"); }
                if (Has(args, "operation_mode")) { sc.OperationMode = ParseEnum<OperationMode>(GetS(args, "operation_mode"), sc.OperationMode); applied.Add("operation_mode"); }
                if (Has(args, "password_min_length")) { sc.PasswordMinLength = GetI(args, "password_min_length"); applied.Add("password_min_length"); }
                if (Has(args, "lock_screen_id")) { sc.LockScreenId = GetS(args, "lock_screen_id").IfNullOrEmpty(null); applied.Add("lock_screen_id"); }

                if (applied.Count == 0) return Task.FromResult(AgentToolResult.Failure("未提供任何可修改参数"));

                aw();
                return Task.FromResult(AgentToolResult.Success($"已更新安全配置: {string.Join(", ", applied)}", true));
            });

    /// <summary>
    /// 运行时配置工具。
    ///
    /// <para><b>刻意不提供「看门狗超时」与「允许重启」两个参数：</b>它们对应的
    /// <c>RuntimeConfig.WatchdogTimeoutMs</c> / <c>EnableHmiReboot</c> 在设备端<b>没有执行路径</b>
    /// （T113 是 Linux 用户态，不存在"MCU 喂狗"；<c>EnableHmiReboot</c> 指向的系统变量在编辑器里
    /// 根本不存在），两个字段已登记在 <c>ConfigFieldInventory.NotWired</c> 里、面板上也是灰的。
    /// 旧版把这两个参数写进工具描述与 schema ⇒ 模型"照着描述改一个死键"，回给用户的却是
    /// <c>已更新运行时配置</c> —— 这是"工具在撒谎"（与阶段 9/10 收口的"UI 在撒谎"同族）。
    /// 等设备端真有了执行路径再把参数加回来。</para>
    /// </summary>
    private static AgentTool UpdateRuntimeTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("update_runtime",
            $"修改运行时配置。可选参数: scan_interval_ms(扫描周期), boot_screen_id(启动屏幕), log_level(日志级别: {AiSchemaEnums.Csv<LogLevel>()})。（看门狗超时 / 允许重启两项在设备端当前没有执行路径，不在此工具的可改范围内。）",
            $$"""
            { "type":"object", "properties": { "scan_interval_ms":{"type":"number"}, "boot_screen_id":{"type":"string"}, "log_level":{"type":"string","enum":{{AiSchemaEnums.JsonArray<LogLevel>()}}} }, "required":[] }
            """,
            (call, ctx, _) =>
            {
                var rc = ctx.Project.RuntimeConfig;
                var args = call.Arguments;
                var applied = new List<string>();

                bw(ctx.Project, call.Name);
                if (Has(args, "scan_interval_ms")) { rc.ScanIntervalMs = GetI(args, "scan_interval_ms"); applied.Add("scan_interval_ms"); }
                if (Has(args, "boot_screen_id")) { rc.BootScreenId = GetS(args, "boot_screen_id").IfNullOrEmpty(null); applied.Add("boot_screen_id"); }
                if (Has(args, "log_level")) { rc.LogLevel = ParseEnum<HmiEditor.Core.Models.LogLevel>(GetS(args, "log_level"), rc.LogLevel); applied.Add("log_level"); }

                if (applied.Count == 0) return Task.FromResult(AgentToolResult.Failure("未提供任何可修改参数"));

                aw();
                return Task.FromResult(AgentToolResult.Success($"已更新运行时配置: {string.Join(", ", applied)}", true));
            });

    // ─────────────────────────── 代码脚本 ───────────────────────────

    private static AgentTool CreateScriptTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("create_script",
            "新建 Lua 脚本。参数: name(必填, 脚本名), code(必填, Lua 代码), category(触发分类: OnClick/OnValueChanged/OnLoad/OnTimer, 默认 OnClick), description。可用 API: hmi_read(变量名), hmi_write(变量名,值), hmi_set_visible(控件id,布尔), hmi_set_text(控件id,文本)。",
            """
            { "type":"object", "properties": { "name":{"type":"string"}, "code":{"type":"string"}, "category":{"type":"string"}, "description":{"type":"string"} }, "required":["name","code"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                var code = AgentOutput.GetString(call.Arguments, "code");
                if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(AgentToolResult.Failure("脚本名不能为空"));
                if (string.IsNullOrWhiteSpace(code)) return Task.FromResult(AgentToolResult.Failure("脚本代码不能为空"));

                bw(ctx.Project, call.Name);
                ctx.Project.Scripts.Add(new HmiScript
                {
                    Name = name,
                    Code = code,
                    Category = AgentOutput.GetString(call.Arguments, "category", "OnClick"),
                    Description = AgentOutput.GetString(call.Arguments, "description").IfNullOrEmpty(null),
                    IsEnabled = true
                });
                aw();
                return Task.FromResult(AgentToolResult.Success($"已创建脚本 '{name}' (代码 {code.Length} 字符)", true));
            });

    private static AgentTool UpdateScriptTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("update_script",
            "修改 Lua 脚本。参数: name(必填, 脚本名), 及任意: code, category, description, enabled。",
            """
            { "type":"object", "properties": { "name":{"type":"string"}, "code":{"type":"string"}, "category":{"type":"string"}, "description":{"type":"string"}, "enabled":{"type":"boolean"} }, "required":["name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                var script = ctx.Project.Scripts.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
                if (script is null) return Task.FromResult(AgentToolResult.Failure($"未找到脚本 '{name}'"));

                var args = call.Arguments;
                bw(ctx.Project, call.Name);
                var applied = new List<string>();
                if (Has(args, "code")) { script.Code = GetS(args, "code"); applied.Add("code"); }
                if (Has(args, "category")) { script.Category = GetS(args, "category"); applied.Add("category"); }
                if (Has(args, "description")) { script.Description = GetS(args, "description").IfNullOrEmpty(null); applied.Add("description"); }
                if (Has(args, "enabled")) { script.IsEnabled = GetB(args, "enabled"); applied.Add("enabled"); }

                if (applied.Count == 0) return Task.FromResult(AgentToolResult.Failure("未提供任何可修改属性"));

                script.ModifiedAt = DateTime.UtcNow;
                aw();
                return Task.FromResult(AgentToolResult.Success($"已修改脚本 '{name}': {string.Join(", ", applied)}", true));
            });

    private static AgentTool DeleteScriptTool(AgentHostBridge bridge, Action<HmiProject, string> bw, Action aw) =>
        new("delete_script",
            "删除 Lua 脚本。参数: name(必填, 脚本名)。",
            """
            { "type":"object", "properties": { "name":{"type":"string"} }, "required":["name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                var script = ctx.Project.Scripts.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
                if (script is null) return Task.FromResult(AgentToolResult.Failure($"未找到脚本 '{name}'"));

                bw(ctx.Project, call.Name);
                ctx.Project.Scripts.Remove(script);
                aw();
                return Task.FromResult(AgentToolResult.Success($"已删除脚本 '{name}'", true));
            });

    // ─────────────────────────── 用户 / 配方 ───────────────────────────

    private static IEnumerable<AgentTool> CreateUserRecipeTools(AgentHostBridge bridge, Action afterWrite)
    {
        yield return new AgentTool("list_users",
            "列出所有用户：用户名、显示名、角色、是否启用。无参数。",
            execute: (_, ctx, _) =>
            {
                var users = bridge.GetAuthService().GetAllUsers().Select(u => new
                {
                    username = u.Username,
                    displayName = u.DisplayName,
                    role = u.RoleId,
                    enabled = u.IsEnabled
                });
                return Task.FromResult(AgentToolResult.Success(Json(users)));
            },
            isReadOnly: true);

        yield return new AgentTool("add_user",
            "新建用户。参数: username(必填), password(必填), display_name(显示名), role_id(角色: role-admin/role-engineer/role-operator, 默认 role-operator)。",
            """
            { "type":"object", "properties": { "username":{"type":"string"}, "password":{"type":"string"}, "display_name":{"type":"string"}, "role_id":{"type":"string"} }, "required":["username","password"] }
            """,
            (call, _, _) =>
            {
                var username = AgentOutput.GetString(call.Arguments, "username");
                var password = AgentOutput.GetString(call.Arguments, "password");
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    return Task.FromResult(AgentToolResult.Failure("用户名与密码不能为空"));

                var auth = bridge.GetAuthService();
                if (auth.GetUserByUsername(username) is not null)
                    return Task.FromResult(AgentToolResult.Failure($"用户 '{username}' 已存在"));

                auth.CreateUser(username,
                    AgentOutput.GetString(call.Arguments, "display_name", username),
                    password,
                    AgentOutput.GetString(call.Arguments, "role_id").IfNullOrEmpty(null));
                afterWrite();
                return Task.FromResult(AgentToolResult.Success($"已创建用户 '{username}'", true));
            });

        yield return new AgentTool("delete_user",
            "删除用户。参数: username(必填, 用户名)。",
            """
            { "type":"object", "properties": { "username":{"type":"string"} }, "required":["username"] }
            """,
            (call, _, _) =>
            {
                var username = AgentOutput.GetString(call.Arguments, "username");
                var auth = bridge.GetAuthService();
                var user = auth.GetUserByUsername(username);
                if (user is null) return Task.FromResult(AgentToolResult.Failure($"未找到用户 '{username}'"));

                auth.DeleteUser(user.Id);
                afterWrite();
                return Task.FromResult(AgentToolResult.Success($"已删除用户 '{username}'", true));
            });

        yield return new AgentTool("list_recipes",
            "列出所有配方：名称、分组、条目数、是否启用。无参数。",
            execute: (_, _, _) =>
            {
                var recipes = bridge.GetRecipeManager().GetAllRecipes().Select(r => new
                {
                    name = r.Name,
                    group = r.GroupName,
                    itemCount = r.Items.Count,
                    enabled = r.IsEnabled
                });
                return Task.FromResult(AgentToolResult.Success(Json(recipes)));
            },
            isReadOnly: true);

        yield return new AgentTool("create_recipe",
            "新建配方。参数: name(必填, 配方名), group_name(分组), items(条目数组, 每项 {variable:\"变量名\", value:数值}), description。",
            """
            { "type":"object", "properties": { "name":{"type":"string"}, "group_name":{"type":"string"}, "items":{"type":"array","items":{"type":"object","properties":{"variable":{"type":"string"},"value":{"type":"number"}}}}, "description":{"type":"string"} }, "required":["name"] }
            """,
            (call, _, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "name");
                if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(AgentToolResult.Failure("配方名不能为空"));

                var mgr = bridge.GetRecipeManager();
                var recipe = new Recipe
                {
                    Name = name,
                    GroupName = AgentOutput.GetString(call.Arguments, "group_name", "默认"),
                    Description = AgentOutput.GetString(call.Arguments, "description").IfNullOrEmpty(null)
                };

                if (call.Arguments.ValueKind == JsonValueKind.Object &&
                    call.Arguments.TryGetProperty("items", out var items) &&
                    items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        recipe.Items.Add(new RecipeItem
                        {
                            VariableId = AgentOutput.GetString(item, "variable"),
                            DefaultValue = AgentOutput.GetDouble(item, "value")
                        });
                    }
                }

                if (!mgr.AddRecipe(recipe)) return Task.FromResult(AgentToolResult.Failure($"配方 '{name}' 已存在"));
                afterWrite();
                return Task.FromResult(AgentToolResult.Success($"已创建配方 '{name}' ({recipe.Items.Count} 个条目)", true));
            });
    }

    // ─────────────────────────── 图片生成 ───────────────────────────

    private static AgentTool CreateGenerateImageTool(
        ImageGenerator imageGenerator,
        Action<string, int, int>? onImageGenerated) =>
        new("generate_image",
            "生成 SVG 矢量图片。参数: prompt(必填, 图片描述), width(宽度, 默认200), height(高度, 默认200)。返回 SVG 文本。",
            """
            { "type":"object", "properties": { "prompt":{"type":"string"}, "width":{"type":"number"}, "height":{"type":"number"} }, "required":["prompt"] }
            """,
            async (call, _, ct) =>
            {
                var prompt = AgentOutput.GetString(call.Arguments, "prompt");
                if (string.IsNullOrWhiteSpace(prompt))
                    return AgentToolResult.Failure("图片描述不能为空");

                var width = AgentOutput.GetInt(call.Arguments, "width", 200);
                var height = AgentOutput.GetInt(call.Arguments, "height", 200);

                try
                {
                    var result = await imageGenerator.GenerateAsync(prompt, width, height, ct);
                    onImageGenerated?.Invoke(result.SvgText, result.Width, result.Height);
                    return AgentToolResult.Success(
                        $"已生成 SVG 图片 ({result.Width}x{result.Height}, {result.ShapeCount} 个形状, " +
                        $"消耗 {result.InputTokens + result.OutputTokens} tokens)");
                }
                catch (Exception ex)
                {
                    return AgentToolResult.Failure($"图片生成失败: {ex.Message}");
                }
            },
            isReadOnly: true);

    // ─────────────────────────── UI 导航 ───────────────────────────

    private static IEnumerable<AgentTool> CreateNavigationTools(AgentHostBridge bridge)
    {
        const string panels = "Toolbox,Faceplate,Canvas,ProjectExplorer,AuditLog,CrossReference,Property,Layers,Assets,Navigation,AiChat,ResourceLibrary,VariableManager,ScriptEditor,Simulator,CommunicationConfig,AlarmConfig,TrendConfig,RecipeEditor,UserManager";

        yield return new AgentTool("open_panel",
            $"打开编辑器面板（不修改项目）。参数: panel_name(必填, 面板名)。可用面板: {panels}。",
            $$"""
            { "type":"object", "properties": { "panel_name":{"type":"string","enum":[{{ToJsonList(panels)}}]} }, "required":["panel_name"] }
            """,
            (call, _, _) =>
            {
                var panel = AgentOutput.GetString(call.Arguments, "panel_name");
                if (string.IsNullOrWhiteSpace(panel)) return Task.FromResult(AgentToolResult.Failure("panel_name 不能为空"));
                bridge.OpenPanel(panel);
                return Task.FromResult(AgentToolResult.Success($"已打开面板 '{panel}'"));
            },
            isReadOnly: true);

        yield return new AgentTool("switch_screen",
            "切换画布显示的当前屏幕。参数: screen_name(必填, 屏幕名称)。",
            """
            { "type":"object", "properties": { "screen_name":{"type":"string"} }, "required":["screen_name"] }
            """,
            (call, ctx, _) =>
            {
                var name = AgentOutput.GetString(call.Arguments, "screen_name");
                if (FindScreen(ctx.Project, name) is null)
                    return Task.FromResult(AgentToolResult.Failure($"未找到屏幕 '{name}'"));
                bridge.SwitchScreen(name);
                return Task.FromResult(AgentToolResult.Success($"已切换到屏幕 '{name}'"));
            },
            isReadOnly: true);

        yield return new AgentTool("save_project",
            "保存当前项目到文件。无参数。",
            execute: async (_, _, ct) =>
            {
                await bridge.SaveProject();
                return AgentToolResult.Success("已请求保存项目");
            },
            isReadOnly: true);

        yield return new AgentTool("export_code",
            "导出当前项目的 LVGL 嵌入式代码。无参数。",
            execute: async (_, _, ct) =>
            {
                await bridge.ExportCode();
                return AgentToolResult.Success("已请求导出 LVGL 代码");
            },
            isReadOnly: true);
    }

    // ─────────────────────────── 辅助 ───────────────────────────

    /// <summary>
    /// 控件类型清单（喂给工具 schema 的 <c>enum</c> 与参数说明文本）。
    ///
    /// <para>曾是一条**手抄的 21 项字面量常量**（现由 <see cref="AiSchemaEnums"/> 生成）——
    /// 手抄那份漏了 <c>Rectangle/Ellipse/Polyline/Arc</c>（真值 25 项），
    /// 于是 Agent **永远无法**通过 <c>add_control</c> / <c>add_controls_batch</c> /
    /// <c>add_template_control</c> 添加这 4 种绘图图形：严格 <c>json_schema</c> 模式下
    /// <c>enum</c> 是硬约束，模型根本输出不了它们。</para>
    /// </summary>
    private static string ControlTypeEnumList => AiSchemaEnums.GeneratableControlTypesCsv;

    /// <summary>add_controls_batch 的参数 Schema（[[ENUM]] 占位符在注册时替换为控件类型枚举）。
    /// 注意：不使用内插原始字符串——Schema 内含三连右大括号，与任意 $ 数量的插值定界冲突（CS9007）。
    ///
    /// <para><b>括号账要算对</b>：<c>[[ENUM]]</c> 替换进来的是一整个 JSON 数组
    /// （<c>["Button", …]</c>，**自带方括号**），所以 schema 文本里只能写成
    /// <c>"enum":[[ENUM]]</c> —— 曾经的写法是 <c>"enum":[[[ENUM]]]</c>，那是配合一个
    /// **不带括号**的旧辅助函数（<c>ToJsonList</c>）才成立的；换成自带括号的
    /// <c>AiSchemaEnums</c> 之后就成了 <c>[["Button", …]]</c> ——枚举里套数组，
    /// provider 侧解析不过就静默降级回"无约束"。守卫见
    /// <c>AgentToolSchemaContractTests.EveryToolSchema_IsValidJsonObject_WithNonEmptyStringEnums</c>。</para>
    /// </summary>
    private const string ControlsBatchSchema =
        """
        { "type":"object", "properties": { "screen_name":{"type":"string"}, "controls":{"type":"array","items":{"type":"object","properties":{"type":{"type":"string","enum":[[ENUM]]},"name":{"type":"string"},"x":{"type":"number"},"y":{"type":"number"},"width":{"type":"number"},"height":{"type":"number"},"text":{"type":"string"},"bgColor":{"type":"string"},"textColor":{"type":"string"},"binding":{"type":"string"},"radius":{"type":"number"},"opacity":{"type":"number"}},"required":["type","name"]}}, "auto_grid":{"type":"object","properties":{"columns":{"type":"number"},"x0":{"type":"number"},"y0":{"type":"number"},"item_width":{"type":"number"},"item_height":{"type":"number"},"gap_x":{"type":"number"},"gap_y":{"type":"number"}}} }, "required":["screen_name","controls"] }
        """;

    private static HmiScreen? FindScreen(HmiProject project, string name) =>
        project.Screens.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) ??
        project.Screens.FirstOrDefault(s => s.Id == name);

    private static AlarmGroup AddAlarmGroup(HmiProject project, string name)
    {
        var group = new AlarmGroup { Name = name };
        project.AlarmGroups.Add(group);
        return group;
    }

    private static string NormalizeDriver(string driver) =>
        driver.Replace('_', '-').ToLowerInvariant();

    private static Dictionary<string, string> ParseConfigObject(JsonElement args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("config", out var config) &&
            config.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in config.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? ""
                    : prop.Value.GetRawText();
            }
        }
        return result;
    }

    private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var parsed) ? parsed : fallback;

    private static bool Has(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var _);

    private static string GetS(JsonElement args, string name) => AgentOutput.GetString(args, name);
    private static int GetI(JsonElement args, string name) => AgentOutput.GetInt(args, name);
    private static double GetD(JsonElement args, string name) => AgentOutput.GetDouble(args, name);
    private static bool GetB(JsonElement args, string name) => AgentOutput.GetBool(args, name);

    private static string Json(object value) => JsonSerializer.Serialize(value, JsonOpts);

    private static string ToJsonList(string csv) =>
        string.Join(",", csv.Split(',').Select(p => $"\"{p.Trim()}\""));
}

/// <summary>字符串空值辅助。</summary>
internal static class StringIfEmpty
{
    public static string? IfNullOrEmpty(this string? value, string? fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
