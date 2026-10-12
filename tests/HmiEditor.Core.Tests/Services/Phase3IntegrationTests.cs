using System;
using System.Linq;
using System.Collections.Generic;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Recipe;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests.Services;

public class Phase3IntegrationTests
{
    // ──────────────────────────────────────────────
    // 1. 报警引擎 + 数据记录器集成测试
    // ──────────────────────────────────────────────

    [Fact]
    public void AlarmTriggered_WithDataLogger_RecordsSample()
    {
        using var engine = new AlarmEngine();
        engine.EvaluationIntervalMs = 50;

        // 配置数据记录器，监控报警关联的变量
        var logConfig = new DataLogConfig
        {
            SampleIntervalMs = 50,
            MaxRecords = 1000,
        };
        logConfig.VariableIds.AddRange(new[] { "var_temp", "var_pressure" });
        using var logger = new DataLogger(logConfig);
        logger.Start();

        // 注册报警
        var alarm = new AlarmConfig
        {
            Name = "高温报警",
            Severity = AlarmSeverity.Critical,
            Condition = new AlarmCondition
            {
                VariableId = "var_temp",
                Type = AlarmConditionType.HighLimit,
                Threshold = 100.0,
            },
        };
        engine.RegisterAlarm(alarm);

        // 触发报警
        engine.UpdateVariableValue("var_temp", 120.0);
        engine.UpdateVariableValue("var_pressure", 50.0);
        engine.Start();

        // 等报警进入 Active（产品状态同步）。不要 Sleep 等定时器回调 —— 原因见 Support/TestWait.cs
        TestWait.Until(() => engine.GetActiveAlarms().Any(r => r.AlarmName == "高温报警" && r.State == AlarmState.Active),
            "var_temp=120 超过阈值 100 后应产生活动报警");

        // 验证报警触发
        var activeAlarms = engine.GetActiveAlarms();
        Assert.NotEmpty(activeAlarms);
        Assert.Contains(activeAlarms, r => r.AlarmName == "高温报警" && r.State == AlarmState.Active);

        // 数据记录器（SampleIntervalMs=50）应已周期性采到样本
        TestWait.Until(() => logger.GetAllSamples().Count >= 1, "数据记录器应周期性采到样本");
        var samples = logger.GetAllSamples();
        Assert.NotEmpty(samples);

        // 手动添加一个包含实际值的采样
        logger.AddSample(new Dictionary<string, double>
        {
            ["var_temp"] = 120.0,
            ["var_pressure"] = 50.0,
        });

        var allSamples = logger.GetAllSamples();
        var lastSample = allSamples[^1];
        Assert.Equal(2, lastSample.Values.Count);

        // 停止引擎
        engine.Stop();
        logger.Stop();
    }

    [Fact]
    public void AlarmTriggered_DataLoggerCapturesMultipleAlarms_CrossReference()
    {
        using var engine = new AlarmEngine();
        engine.EvaluationIntervalMs = 50;

        var logConfig = new DataLogConfig
        {
            MaxRecords = 500,
            VariableIds = { "var_temp", "var_pressure", "var_level" },
        };
        using var logger = new DataLogger(logConfig);
        logger.Start();

        // 注册多个报警
        engine.RegisterAlarm(new AlarmConfig
        {
            Name = "高温报警",
            Severity = AlarmSeverity.Critical,
            Condition = new AlarmCondition { VariableId = "var_temp", Type = AlarmConditionType.HighLimit, Threshold = 100.0 },
        });
        engine.RegisterAlarm(new AlarmConfig
        {
            Name = "低压报警",
            Severity = AlarmSeverity.Warning,
            Condition = new AlarmCondition { VariableId = "var_pressure", Type = AlarmConditionType.LowLimit, Threshold = 20.0 },
        });

        // 触发部分报警
        engine.UpdateVariableValues(new Dictionary<string, double>
        {
            ["var_temp"] = 150.0,
            ["var_pressure"] = 10.0,
            ["var_level"] = 50.0,
        });
        engine.Start();

        // 验证两个报警均已触发（等状态达标，不等固定周期）
        TestWait.Until(() => engine.GetActiveAlarms().Count == 2, "两个报警应均已触发");
        var active = engine.GetActiveAlarms();
        Assert.Equal(2, active.Count);

        // 手动为记录器注入采样
        logger.AddSample(new Dictionary<string, double>
        {
            ["var_temp"] = 150.0,
            ["var_pressure"] = 10.0,
            ["var_level"] = 50.0,
        });

        var samples = logger.GetAllSamples();
        Assert.NotEmpty(samples);
        Assert.True(samples[^1].Values[0] > 0, "温度值应被记录");

        engine.Stop();
        logger.Stop();
    }

    // ──────────────────────────────────────────────
    // 2. 用户认证 + 报警确认集成测试
    // ──────────────────────────────────────────────

    [Fact]
    public void UserLogin_AcknowledgesAlarm_AuditLogRecords()
    {
        using var auth = new AuthenticationService();
        using var engine = new AlarmEngine();
        engine.EvaluationIntervalMs = 50;

        // 注册一个需要确认的报警
        var alarmConfig = new AlarmConfig
        {
            Name = "需确认报警",
            Severity = AlarmSeverity.Critical,
            RequiresAck = true,
            Condition = new AlarmCondition
            {
                VariableId = "var_x",
                Type = AlarmConditionType.HighLimit,
                Threshold = 100.0,
            },
        };
        engine.RegisterAlarm(alarmConfig);

        // 用户登录（操作员角色拥有 AcknowledgeAlarm 权限）
        var sessionId = auth.Login("admin", "admin").SessionId;
        Assert.NotNull(sessionId);
        Assert.True(auth.CheckPermission(sessionId, Permission.AcknowledgeAlarm));

        // 触发报警
        engine.UpdateVariableValue("var_x", 200.0);
        engine.Start();

        TestWait.Until(() => engine.GetActiveAlarms().Any(r => r.AlarmName == "需确认报警"),
            "var_x=200 超过阈值后应产生活动报警");

        var activeRecords = engine.GetActiveAlarms();
        var record = activeRecords.FirstOrDefault(r => r.AlarmName == "需确认报警");
        Assert.NotNull(record);
        Assert.Equal(AlarmState.Active, record!.State);

        // 用登录用户确认报警
        var ackResult = engine.AcknowledgeAlarm(record.Id, "admin");
        Assert.True(ackResult);
        Assert.Equal(AlarmState.Acknowledged, record.State);
        Assert.Equal("admin", record.AcknowledgedBy);

        // 验证确认时间戳已设置
        Assert.NotNull(record.AcknowledgedAt);
        Assert.True(record.AcknowledgedAt!.Value.Kind == DateTimeKind.Utc);

        engine.Stop();
    }

    [Fact]
    public void UserWithoutPermission_CannotAcknowledgeAlarm_ViaPolicy()
    {
        using var auth = new AuthenticationService();

        // 创建一个仅有 ViewScreen 权限的自定义角色
        var restrictedRole = new UserRole
        {
            Id = "role-viewer",
            Name = "Viewer",
            Level = 1,
            Permissions = Permission.ViewScreen,
        };
        auth.RegisterRole(restrictedRole);

        var viewer = new UserProfile
        {
            Username = "viewer1",
            DisplayName = "Viewer 1",
            RoleId = "role-viewer",
        };
        auth.SetUserPassword(viewer, "pass");
        auth.RegisterUser(viewer);

        var sessionId = auth.Login("viewer1", "pass").SessionId;
        Assert.NotNull(sessionId);
        Assert.False(auth.CheckPermission(sessionId, Permission.AcknowledgeAlarm));
        Assert.True(auth.CheckPermission(sessionId, Permission.ViewScreen));
    }

    // ──────────────────────────────────────────────
    // 3. 配方管理 + CSV 导入导出集成测试
    // ──────────────────────────────────────────────

    [Fact]
    public void RecipeManager_ExportImportCsv_RoundTrip()
    {
        var mgr = new RecipeManager();

        // 添加配方
        var recipe = new Recipe
        {
            Name = "高温配方",
            GroupName = "温度控制",
            Description = "适用于高温环境",
        };
        recipe.Items.Add(new RecipeItem
        {
            VariableId = "var_temp",
            Label = "目标温度",
            DefaultValue = 180.0,
            MinValue = 0,
            MaxValue = 300,
            Unit = "°C",
            SortOrder = 1,
        });
        recipe.Items.Add(new RecipeItem
        {
            VariableId = "var_pressure",
            Label = "目标压力",
            DefaultValue = 50.0,
            MinValue = 0,
            MaxValue = 100,
            Unit = "bar",
            SortOrder = 2,
        });
        mgr.AddRecipe(recipe);

        // 验证校验通过
        var errors = mgr.ValidateRecipe(recipe);
        Assert.Empty(errors);

        // 导出 CSV
        var csv = mgr.ExportCsv();
        Assert.Contains("RecipeName", csv);
        Assert.Contains("高温配方", csv);
        Assert.Contains("180.0000", csv);
        Assert.Contains("50.0000", csv);
        Assert.Contains("温度控制", csv);

        // 清空并导入
        mgr.Clear();
        Assert.Empty(mgr.GetAllRecipes());

        var result = mgr.ImportCsv(csv);
        Assert.Equal(1, result.Imported);
        Assert.Empty(result.Issues);
        Assert.Single(mgr.GetAllRecipes());

        // 验证导入的配方
        var imported = mgr.GetAllRecipes().First();
        Assert.Equal("高温配方", imported.Name);
        Assert.Equal("温度控制", imported.GroupName);
        Assert.Equal(2, imported.Items.Count);

        // Verify imported item values
        var tempItem = imported.Items.First(i => i.VariableId == "var_temp");
        Assert.Equal(180.0, tempItem.DefaultValue, 4);
    }

    [Fact]
    public void RecipeManager_MultipleRecipesCsv_RoundTrip()
    {
        var mgr = new RecipeManager();

        // 添加多个配方
        mgr.AddRecipe(new Recipe { Name = "配方A", GroupName = "温度" });
        mgr.AddRecipe(new Recipe { Name = "配方B", GroupName = "压力" });
        mgr.AddRecipe(new Recipe { Name = "配方C", GroupName = "温度" });

        Assert.Equal(3, mgr.GetAllRecipes().Count);

        // 导出 CSV
        var csv = mgr.ExportCsv();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // header + 3 data rows
        Assert.Equal(4, lines.Length);

        // 清空并导入
        mgr.Clear();
        var result = mgr.ImportCsv(csv);
        Assert.Equal(3, result.Imported);
        Assert.Empty(result.Issues);

        // 验证导入后按组分类
        var tempRecipes = mgr.GetRecipesByGroup("温度");
        Assert.Equal(2, tempRecipes.Count);
        var pressureRecipes = mgr.GetRecipesByGroup("压力");
        Assert.Single(pressureRecipes);
    }

    [Fact]
    public void RecipeManager_ExportImportCsv_UpdatesExisting()
    {
        var mgr = new RecipeManager();
        mgr.AddRecipe(new Recipe { Name = "配方1", GroupName = "默认" });

        // 导出带已有配方的 CSV，修改后导入
        var csv = mgr.ExportCsv();
        mgr.ImportCsv(csv);
        var recipe = mgr.GetAllRecipes().First(r => r.Name == "配方1");
        Assert.True(recipe.Version >= 2, "导入已有配方应递增版本号");
    }

    // ──────────────────────────────────────────────
    // 4. 脚本引擎集成测试
    // ──────────────────────────────────────────────

    [Fact]
    public void ScriptEngine_RegisterValidateBind_Integration()
    {
        var engine = new ScriptEngineService();

        // 从模板创建并注册脚本
        var template = ScriptEngineService.GetTemplates().First(t => t.Category == "OnClick");
        var script = engine.CreateFromTemplate(template);
        Assert.NotNull(script);
        Assert.Equal("OnClick", script.Category);
        Assert.Contains("function on_click", script.Code);

        // 验证脚本
        var validationResult = engine.Validate(script);
        Assert.True(validationResult.IsValid);
        Assert.Empty(validationResult.Errors);

        // 加载脚本
        engine.LoadScript(script);
        Assert.Single(engine.GetLoadedScripts());

        // 添加事件绑定
        var binding = new ScriptBinding
        {
            ControlId = "btn_start",
            EventName = "OnClick",
            ScriptId = script.Id,
            FunctionName = "on_click",
        };
        engine.AddBinding(binding);

        Assert.Single(engine.GetAllBindings());
        var controlBindings = engine.GetBindingsForControl("btn_start");
        Assert.Single(controlBindings);
        Assert.Equal("on_click", controlBindings[0].FunctionName);

        // 清理
        engine.Shutdown();
        Assert.False(engine.IsInitialized);
        Assert.Empty(engine.GetLoadedScripts());
        Assert.Empty(engine.GetAllBindings());
    }

    [Fact]
    public void ScriptEngine_MultipleCategoriesAndBindings()
    {
        var engine = new ScriptEngineService();

        // 注册多个类别的脚本
        engine.LoadScript(new HmiScript { Name = "ClickHandler", Category = "OnClick", Code = "function on_click() end" });
        engine.LoadScript(new HmiScript { Name = "TimerHandler", Category = "Timer", Code = "function on_timer() end" });
        engine.LoadScript(new HmiScript { Name = "AlarmHandler", Category = "Alarm", Code = "function on_alarm() end" });

        Assert.Equal(3, engine.GetLoadedScripts().Count);
        Assert.Single(engine.GetScriptsByCategory("OnClick"));
        Assert.Single(engine.GetScriptsByCategory("Timer"));
        Assert.Single(engine.GetScriptsByCategory("Alarm"));

        // 为多个控件添加绑定
        engine.AddBinding(new ScriptBinding { ControlId = "btn_1", EventName = "OnClick", ScriptId = "s1" });
        engine.AddBinding(new ScriptBinding { ControlId = "btn_1", EventName = "OnPress", ScriptId = "s1" });
        engine.AddBinding(new ScriptBinding { ControlId = "btn_2", EventName = "OnClick", ScriptId = "s2" });

        Assert.Equal(3, engine.GetAllBindings().Count);
        Assert.Equal(2, engine.GetBindingsForControl("btn_1").Count);
        Assert.Single(engine.GetBindingsForControl("btn_2"));

        engine.Shutdown();
    }

    // ──────────────────────────────────────────────
    // 5. 全流程集成测试
    // ──────────────────────────────────────────────

    [Fact]
    public void FullWorkflow_LoginConfigureAlarmTriggerLogAcknowledgeAudit_Integration()
    {
        // ── Phase 1: 用户登录 ──
        using var auth = new AuthenticationService();
        var sessionId = auth.Login("admin", "admin").SessionId;
        Assert.NotNull(sessionId);
        Assert.True(auth.CheckPermission(sessionId, Permission.AcknowledgeAlarm));
        Assert.True(auth.CheckPermission(sessionId, Permission.ViewAlarmHistory));

        // ── Phase 2: 配置报警引擎和数据记录器 ──
        using var engine = new AlarmEngine();
        engine.EvaluationIntervalMs = 50;

        var alarmConfig = new AlarmConfig
        {
            Name = "全流程测试报警",
            Severity = AlarmSeverity.Critical,
            RequiresAck = true,
            Condition = new AlarmCondition
            {
                VariableId = "var_main",
                Type = AlarmConditionType.HighLimit,
                Threshold = 100.0,
            },
        };
        engine.RegisterAlarm(alarmConfig);

        var logConfig = new DataLogConfig
        {
            MaxRecords = 200,
            VariableIds = { "var_main" },
        };
        using var logger = new DataLogger(logConfig);
        logger.Start();

        // ── Phase 3: 触发报警 ──
        var alarmTriggered = false;
        engine.AlarmTriggered += (_, _) => alarmTriggered = true;

        engine.UpdateVariableValue("var_main", 150.0);
        engine.Start();

        TestWait.Until(() => alarmTriggered, "报警应被触发（AlarmTriggered 事件）");

        Assert.True(alarmTriggered, "报警应被触发");

        var active = engine.GetActiveAlarms();
        var record = active.FirstOrDefault(r => r.AlarmName == "全流程测试报警");
        Assert.NotNull(record);
        Assert.Equal(AlarmState.Active, record!.State);

        // ── Phase 4: 记录数据 ──
        logger.AddSample(new Dictionary<string, double>
        {
            ["var_main"] = 150.0,
        });

        var samples = logger.GetAllSamples();
        Assert.NotEmpty(samples);

        // ⚠ 这里**不能**写 `samples[^1]`（登记 #55）。`DataLogger.Start()` 走
        // `_sampleTimer.Change(0, SampleIntervalMs)`（默认 1000ms）⇒ 当场排一个「立即到期」的回调，
        // 而 `OnSampleTick` 只塞**全 0 的占位采样**（`DataLogger.cs:232`，注释自己写着「实际数据由外部注入」）。
        // 那个回调由**线程池**派发 ⇒ 负载高时它可能被拖到 `AddSample(150)` **之后**才跑，
        // 于是 `samples[^1]` 变成那条 0.0，断言随机红（实测：门禁并行跑 4 次红 1 次，
        // 串行 5/5 绿，失败那次本用例自身只跑了 22ms ⇒ 是派发被拖后，不是用例变慢）。
        //
        // 本条要验的是「**150 确实被记下来了**」，不是「其后没有任何别的采样」——
        // 后者取决于生产代码定时器的调度，不归这条用例管。
        Assert.True(samples.Any(s => s.Values[0] > 100.0),
            "记录的值应超过报警阈值：注入的 150 应出现在采样里"
            + "（别改回 samples[^1] —— DataLogger 自己的定时器会追加全 0 的占位采样，可能排在它后面）");

        // ── Phase 5: 确认报警 ──
        var ackResult = engine.AcknowledgeAlarm(record.Id, "admin");
        Assert.True(ackResult);
        Assert.Equal(AlarmState.Acknowledged, record.State);
        Assert.Equal("admin", record.AcknowledgedBy);

        // ── Phase 6: 审计日志验证 ──
        var auditLog = auth.GetAuditLog(10);
        Assert.Contains(auditLog, e => e.Action == AuditAction.Login && e.Username == "admin");

        // ── Phase 7: 条件解除 → 报警恢复 ──
        engine.UpdateVariableValue("var_main", 0.0);
        TestWait.Until(() => engine.GetAllRecords().Any(r => r.AlarmName == "全流程测试报警" && r.State == AlarmState.Returned),
            "条件解除后报警应转为 Returned");

        var allRecords = engine.GetAllRecords();
        var returnedRecord = allRecords.FirstOrDefault(r => r.AlarmName == "全流程测试报警");
        Assert.NotNull(returnedRecord);
        Assert.Equal(AlarmState.Returned, returnedRecord!.State);

        // 清理
        engine.Stop();
        logger.Stop();
    }

    [Fact]
    public void FullWorkflow_OperatorLogin_CannotChangeConfig()
    {
        using var auth = new AuthenticationService();

        // 创建操作员用户
        var op = new UserProfile
        {
            Username = "operator1",
            DisplayName = "Operator 1",
            RoleId = "role-operator",
        };
        auth.SetUserPassword(op, "pass");
        auth.RegisterUser(op);

        var sessionId = auth.Login("operator1", "pass").SessionId;
        Assert.NotNull(sessionId);

        // 操作员可以查看画面、操作控件、确认报警
        Assert.True(auth.CheckPermission(sessionId, Permission.ViewScreen));
        Assert.True(auth.CheckPermission(sessionId, Permission.OperateControl));
        Assert.True(auth.CheckPermission(sessionId, Permission.AcknowledgeAlarm));

        // 操作员不能修改配置或管理用户
        Assert.False(auth.CheckPermission(sessionId, Permission.ChangeConfig));
        Assert.False(auth.CheckPermission(sessionId, Permission.ManageUsers));
        Assert.False(auth.CheckPermission(sessionId, Permission.EditRecipe));
    }
}
