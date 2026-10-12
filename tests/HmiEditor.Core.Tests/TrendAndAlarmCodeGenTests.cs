using HmiEditor.CodeGen;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Trend;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 验证趋势图 / 报警代码生成器的正确性 —— 重点覆盖曾存在的两类运行时缺陷：
///  1) GenerateTrendCode 多 pen 趋势向 lv_chart_set_next_value 传 NULL series（空指针崩溃）；
///  2) GenerateAlarmCode 的 config 配置表索引与生成顺序耦合（误用可产生越界 index）。
///
/// 注意：本类多处语料的报警 id 故意含 '-'（如 "alarm-1"）。早年这些断言写成字面量
/// <c>Assert.Contains("check_alarm_alarm-1")</c>，等于**把非法 C 标识符锁成期望行为** ——
/// <c>check_alarm_alarm-1</c> 在 C 里是减法表达式，作为函数定义直接语法错误，只是当时生成器
/// 也裸拼才一直"绿"。收口后再这样断言会失败。凡涉及符号名的断言一律走 <see cref="CIdentifier"/>
/// 同一构造器：既保留非法 id 语料（顺带覆盖净化），又不会把缺陷写进期望。
/// </summary>
public class TrendAndAlarmCodeGenTests
{
    private static TrendPen Pen(string label, string color, string varId = "v1", bool enabled = true)
        => new() { Label = label, Color = color, VariableId = varId, IsEnabled = enabled };

    // ── 趋势图 ──

    [Fact]
    public void GenerateTrendCode_MultiPen_StoresEachSeriesAndNeverPassesNull()
    {
        var generator = new CodeGenerator();
        var chart = new TrendConfig
        {
            Id = "tr1", Name = "温度/压力", MaxPoints = 300, ShowGrid = true,
            Pens =
            {
                Pen("温度", "#ef4444", "v-temp"),
                Pen("压力", "#38bdf8", "v-press"),
            }
        };

        var code = generator.GenerateTrendCode(new[] { chart }, Array.Empty<DataLogConfig>());

        // 每个启用 pen 的 series 都需要被存储（lv_chart_add_series 返回值必须保留）
        Assert.Contains("_series_tr1[0] = lv_chart_add_series", code);
        Assert.Contains("_series_tr1[1] = lv_chart_add_series", code);
        // series 数组声明
        Assert.Contains("static lv_chart_series_t* _series_tr1[2]", code);

        // 更新函数必须遍历 series 数组并传入非 NULL 指针，绝不能再出现 NULL series
        CSource.AssertNoCode(code, "lv_chart_set_next_value(_chart_tr1, NULL");
        CSource.AssertCode(code, "lv_chart_set_next_value(_chart_tr1, _series_tr1[i], values[i])");
    }

    [Fact]
    public void GenerateTrendCode_SinglePen_StoresAndFeedsSeries()
    {
        var generator = new CodeGenerator();
        var chart = new TrendConfig
        {
            Id = "tr2", Name = "单曲线", MaxPoints = 100,
            Pens = { Pen("流量", "#22c55e", "v-flow") }
        };

        var code = generator.GenerateTrendCode(new[] { chart }, Array.Empty<DataLogConfig>());

        Assert.Contains("_series_tr2[0] = lv_chart_add_series", code);
        CSource.AssertNoCode(code, "lv_chart_set_next_value(_chart_tr2, NULL");
        // 单 pen 仍走循环，取 values[0]
        Assert.Contains("values[i]", code);
    }

    [Fact]
    public void GenerateTrendCode_DisabledPens_AreExcludedAndNoNullSeries()
    {
        var generator = new CodeGenerator();
        var chart = new TrendConfig
        {
            Id = "tr3", Name = "含禁用 pen",
            Pens =
            {
                Pen("启用曲线", "#22c55e", "v-on"),
                Pen("禁用曲线", "#999999", "v-off", enabled: false),
            }
        };

        var code = generator.GenerateTrendCode(new[] { chart }, Array.Empty<DataLogConfig>());

        // 只有启用的 pen 生成 series，且禁用 pen 不会产生 _series_tr3[1]
        Assert.Contains("_series_tr3[0] = lv_chart_add_series", code);
        Assert.DoesNotContain("_series_tr3[1] = lv_chart_add_series", code);
        Assert.Contains("static lv_chart_series_t* _series_tr3[1]", code);
        CSource.AssertNoCode(code, "lv_chart_set_next_value(_chart_tr3, NULL");
    }

    [Fact]
    public void GenerateTrendCode_NoEnabledPen_CompilesWithoutNullSeriesOrCrash()
    {
        var generator = new CodeGenerator();
        var chart = new TrendConfig
        {
            Id = "tr4", Name = "无曲线",
            Pens = { Pen("禁用1", "#111111", "v1", enabled: false) }
        };

        var code = generator.GenerateTrendCode(new[] { chart }, Array.Empty<DataLogConfig>());

        // 不能出现 NULL series 调用；空图表用 (void) 消参避免未使用警告
        CSource.AssertNoCode(code, "lv_chart_set_next_value(_chart_tr4, NULL");
        CSource.AssertCode(code, "(void)values; (void)count;");
        // 即使没有任何启用 pen，也应生成 chart_create
        CSource.AssertCode(code, "lv_chart_create(parent)");
    }

    [Fact]
    public void GenerateTrendCode_NoChartsOrLoggers_ReturnsCommentOnly()
    {
        var generator = new CodeGenerator();
        var code = generator.GenerateTrendCode(Array.Empty<TrendConfig>(), Array.Empty<DataLogConfig>());
        Assert.Contains("No trend charts or data loggers configured.", code);
        Assert.DoesNotContain("lv_chart_create", code);
    }

    [Fact]
    public void GenerateTrendCode_EmitsNullGuardForUpdateAndSample()
    {
        var generator = new CodeGenerator();
        var chart = new TrendConfig { Id = "tr5", Name = "守卫测试", Pens = { Pen("温度", "#ef4444", "v_temp") } };
        var logger = new DataLogConfig { Id = "log5", MaxRecords = 16 };

        var code = generator.GenerateTrendCode(new[] { chart }, new[] { logger });

        // trend_update：values==NULL 时跳过并刷新，防 OOB 读
        CSource.AssertCode(code, "if (values == NULL) { TREND_TRACE(\"chart-tr5 update with NULL values, skipped\");");
        // datalog_sample：无有效数据不入环形缓冲
        CSource.AssertCode(code, "if (values == NULL || count == 0) return;");
        // 数据点 trace（由 TREND_TRACE_ENABLE 控制）
        CSource.AssertCode(code, "TREND_TRACE(\"chart-tr5 update: n=%u values[0]=%.3f\"");
    }

    [Fact]
    public void GenerateTrendCode_EmitsCsvExport()
    {
        var generator = new CodeGenerator();
        var logger = new DataLogConfig { Id = "logcsv", MaxRecords = 32 };
        logger.VariableIds.AddRange(new[] { "temp", "press" });

        var code = generator.GenerateTrendCode(Array.Empty<TrendConfig>(), new[] { logger });

        // CSV 导出函数生成：回调写入 + 表头（含绑定变量名）+ 时间顺序回放
        CSource.AssertCode(code, "void datalog_export_csv_logcsv(void (*write_fn)(const char*))");
        CSource.AssertCode(code, "write_fn(\"timestamp_ms\")");
        CSource.AssertCode(code, "write_fn(\",temp\")");
        CSource.AssertCode(code, "write_fn(\",press\")");
        CSource.AssertCode(code, "snprintf(buf, sizeof(buf), \"%lu\", (unsigned long)s->timestamp_ms);");
        CSource.AssertCode(code, "snprintf(buf, sizeof(buf), \",%.3f\", (double)s->values[j]);");
        CSource.AssertCode(code, "if (!s->is_valid) continue;");
    }

    [Fact]
    public void GenerateAlarmCode_Evaluate_EmitsBoundsGuard()
    {
        var generator = new CodeGenerator();
        var groups = new[] { CreateGroup("温度异常", "b1"), CreateGroup("通讯故障", "b2") };

        var code = generator.GenerateAlarmCode(groups);

        // evaluate 越界保护：NULL 输入直接返回；越界报警跳过求值（防 OOB 读）
        CSource.AssertCode(code, "if (var_values == NULL || var_count <= 0) return;");
        CSource.AssertCode(code, "if (0 < var_count) check_alarm_b1(var_values[0]);");
        CSource.AssertCode(code, "if (1 < var_count) check_alarm_b2(var_values[1]);");
    }

    // ── 报警 ──

    [Fact]
    public void GenerateAlarmCode_MultipleGroups_GeneratesCheckFunctions()
    {
        var generator = new CodeGenerator();
        var groups = new[]
        {
            CreateGroup("温度异常", "alarm-1"),
            CreateGroup("通讯故障", "alarm-2"),
        };

        var code = generator.GenerateAlarmCode(groups);

        // 每个启用的报警生成独立检查函数，不依赖配置表位置索引（无越界风险）
        Assert.Contains(CIdentifier.AlarmCheckFn("alarm-1"), code);
        Assert.Contains(CIdentifier.AlarmCheckFn("alarm-2"), code);
        Assert.Contains("alarm_engine_evaluate", code);
        // 触发条件 HighLimit 规范化
        Assert.Contains("val >=", code);
    }

    [Fact]
    public void GenerateAlarmCode_DisabledAlarm_IsOmitted()
    {
        var generator = new CodeGenerator();
        var enabled = CreateGroup("启用组", "enabled-alarm");
        var disabled = CreateGroup("禁用组", "disabled-alarm");
        disabled.Alarms[0].IsEnabled = false;

        var code = generator.GenerateAlarmCode(new[] { enabled, disabled });

        Assert.Contains(CIdentifier.AlarmCheckFn("enabled-alarm"), code);
        // 禁用项无论净化与否都不应出现（"check_alarm_disabled" 是两种形态的公共前缀）
        Assert.DoesNotContain("check_alarm_disabled", code);
    }

    [Fact]
    public void GenerateAlarmCode_NoGroups_ReturnsEmptyMarker()
    {
        var generator = new CodeGenerator();
        var code = generator.GenerateAlarmCode(Array.Empty<AlarmGroup>());
        Assert.Contains("No alarms configured.", code);
    }

    // ── 接入主生成流程的验证 ──

    [Fact]
    public void GenerateFullProject_WithTrendsAndAlarms_EmitsFixedSnippets()
    {
        var project = new HmiProject { Name = "接线验证", LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });
        project.TrendConfigs.Add(new TrendConfig
        {
            Id = "tr1", Name = "温度", MaxPoints = 100,
            Pens = { Pen("温度", "#ef4444", "v-temp"), Pen("压力", "#38bdf8", "v-press") }
        });
        project.AlarmGroups.Add(CreateGroup("温度异常", "injected-alarm"));

        var code = new CodeGenerator().GenerateFullProject(project);

        // 报警与趋势代码已并入主生成输出
        Assert.Contains(CIdentifier.AlarmCheckFn("injected-alarm"), code);
        Assert.Contains("trend_init_tr1", code);
        // 趋势多 pen 修复：series 非 NULL（不再生成 lv_chart_set_next_value(..., NULL, ...)）
        CSource.AssertNoCode(code, "lv_chart_set_next_value(_chart_tr1, NULL");
        Assert.Contains("_series_tr1[0] = lv_chart_add_series", code);
        Assert.Contains("_series_tr1[1] = lv_chart_add_series", code);
    }

    [Fact]
    public void GenerateSharedLibraryProject_WithTrendsAndAlarms_EmitsFixedSnippets()
    {
        var project = new HmiProject { Name = "端子接线", LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });
        project.TrendConfigs.Add(new TrendConfig
        {
            Id = "tr1", Name = "压力", MaxPoints = 50,
            Pens = { Pen("流量", "#22c55e", "v-flow") }
        });
        project.AlarmGroups.Add(CreateGroup("通讯故障", "injected-alarm2"));

        var code = new CodeGenerator().GenerateSharedLibraryProject(project);

        Assert.Contains(CIdentifier.AlarmCheckFn("injected-alarm2"), code);
        Assert.Contains("trend_init_tr1", code);
        CSource.AssertNoCode(code, "lv_chart_set_next_value(_chart_tr1, NULL");
        Assert.Contains("_series_tr1[0] = lv_chart_add_series", code);
    }

    [Fact]
    public void GenerateSharedLibraryProject_NoTrendsOrAlarms_NoExtraSymbols()
    {
        var project = new HmiProject { Name = "空项目", LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });

        var code = new CodeGenerator().GenerateSharedLibraryProject(project);

        // 空配置只产生占位注释，不生成额外 C 符号
        Assert.Contains("No alarms configured.", code);
        Assert.Contains("No trend charts or data loggers configured.", code);
        Assert.DoesNotContain("check_alarm_", code);
        Assert.DoesNotContain("trend_init_", code);
    }

    [Fact]
    public void GenerateSharedLibraryProject_WithDataLogConfigs_DeliversCsvIntoOutput()
    {
        var project = new HmiProject { Name = "带记录器", LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });
        var dlog = new DataLogConfig { Id = "logmain", MaxRecords = 64 };
        dlog.VariableIds.AddRange(new[] { "temp" });
        project.DataLogConfigs.Add(dlog);

        var code = new CodeGenerator().GenerateSharedLibraryProject(project);

        // HmiProject.DataLogConfigs 通过主流程传入 → 环形缓冲 + CSV 导出进入 .so 产物
        CSource.AssertCode(code, "void datalog_sample_logmain(");
        CSource.AssertCode(code, "void datalog_export_csv_logmain(void (*write_fn)(const char*))");
        CSource.AssertCode(code, "write_fn(\",temp\")");
    }

    [Fact]
    public void GenerateSharedLibraryProject_Teardown_ResetsInternalState()
    {
        var project = new HmiProject { Name = "带资源", LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });
        project.TrendConfigs.Add(new TrendConfig { Id = "trt", Name = "温度", Pens = { Pen("温度", "#ef4444", "v_t") } });
        project.AlarmGroups.Add(CreateGroup("报警", "alt"));

        var code = new CodeGenerator().GenerateSharedLibraryProject(project);

        // teardown 绑定到 api 表，清空趋势缓存指针 + 复位报警记录
        Assert.Contains(".teardown         = hmi_ui_teardown,", code);
        CSource.AssertCode(code, "static void hmi_ui_teardown(void)");
        Assert.Contains("_chart_trt = NULL;", code);
        CSource.AssertCode(code, "alarm_engine_init();");
    }

    [Fact]
    public void GenerateSharedLibraryProject_AbiV2_ExposesEngineCallbacks()
    {
        var project = new HmiProject { Name = "带引擎", LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });
        project.TrendConfigs.Add(new TrendConfig { Id = "trt", Name = "温度", Pens = { Pen("温度", "#ef4444", "v_t") } });
        project.AlarmGroups.Add(CreateGroup("报警", "alt"));
        var dlog = new DataLogConfig { Id = "logv2", MaxRecords = 8 };
        project.DataLogConfigs.Add(dlog);

        var code = new CodeGenerator().GenerateSharedLibraryProject(project);

        // ABI V2：报警/趋势/CSV 引擎函数通过 hmi_ui_api_t 暴露给宿主
        Assert.Contains(".alarm_evaluate   = alarm_engine_evaluate,", code);
        Assert.Contains(".alarm_acknowledge= alarm_acknowledge,", code);
        Assert.Contains(".alarm_active_count = alarm_get_active_count,", code);
        Assert.Contains(".trend_tick       = trend_engine_tick,", code);
        Assert.Contains(".datalog_export_csv = hmi_datalog_export_csv,", code);
        // CSV 分发器按 log_idx 路由
        CSource.AssertCode(code, "static void hmi_datalog_export_csv(int log_idx, void (*write_fn)(const char*))");
        CSource.AssertCode(code, "case 0: datalog_export_csv_logv2(write_fn); break;");
    }

    [Fact]
    public void GenerateSharedLibraryProject_ThemeFallsBackToEmbeddedCjk()
    {
        var project = new HmiProject { Name = "主题字体", LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });

        var code = new CodeGenerator().GenerateSharedLibraryProject(project);

        // .so 模式无字体资产时回退固件嵌入的 Source Han Sans CJK 子集
        // （而非 LV_FONT_DEFAULT=Montserrat，后者无中文字形会渲染成方框）。
        // 此行为对齐固件保底中文字体（lv_conf.h 内嵌 lv_font_source_han_sans_sc_14_cjk）。
        CSource.AssertCode(code, "lv_obj_set_style_text_font(scr, &lv_font_source_han_sans_sc_14_cjk,");
        Assert.DoesNotContain("&lv_font_default", code);
    }

    [Fact]
    public void GenerateSharedLibraryProject_Teardown_EmptyProject_IsNoop()
    {
        var project = new HmiProject { Name = "空工程", LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });

        var code = new CodeGenerator().GenerateSharedLibraryProject(project);

        // 无报警/趋势/绑定刷新：teardown 为空操作函数体，仍绑定到 api 表
        Assert.Contains(".teardown         = hmi_ui_teardown,", code);
        CSource.AssertCode(code, "(void)0;  /* 无报警/趋势/绑定刷新资源，teardown 为空操作 */");
        CSource.AssertNoCode(code, "alarm_engine_init();");
    }

    [Fact]
    public void GenerateSharedLibraryProject_BindingRefresh_TeardownDeletesTimer()
    {
        var project = new HmiProject { Name = "绑定生命周期", LvglVersion = "9.5" };
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        var label = new HmiControl { Id = "lbl1", Name = "温度", Type = HmiControlType.Label };
        label.VariableBindings.Add(new VariableBindingData
        {
            VariableId = "v-temp", Direction = BindingDirection.ReadOnly, PropertyName = "Text"
        });
        screen.Controls.Add(label);
        project.Screens.Add(screen);
        project.VariableManager.Connections.Add(new DeviceConnection
        { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" });
        project.VariableManager.Variables.Add(new Variable
        { Id = "v-temp", Name = "温度", Address = "40001", DataType = "Float", ConnectionId = "c1" });

        var code = new CodeGenerator().GenerateSharedLibraryProject(project);

        // 热加载卸载前必须删除绑定刷新 timer，避免回调访问已释放的屏幕对象。
        CSource.AssertCode(code, "s_ui_refresh_timer = lv_timer_create(hmi_ui_refresh_timer_cb, 100, NULL);");
        CSource.AssertCode(code, "if (s_ui_refresh_timer) { lv_timer_delete(s_ui_refresh_timer); s_ui_refresh_timer = NULL; }");
        Assert.DoesNotContain("无报警/趋势/绑定刷新资源，teardown 为空操作", code);
    }

    [Fact]
    public void GenerateSharedLibraryProject_BooleanControls_DeviceRefresh_IsGenerated()
    {
        var project = new HmiProject { Name = "布尔刷新", LvglVersion = "9.5" };
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        foreach (var (id, type) in new[] { ("led1", HmiControlType.Led), ("sw1", HmiControlType.Switch), ("cb1", HmiControlType.CheckBox) })
        {
            var control = new HmiControl { Id = id, Name = id, Type = type };
            control.VariableBindings.Add(new VariableBindingData { VariableId = "v-bool", Direction = BindingDirection.ReadOnly, PropertyName = "Value" });
            screen.Controls.Add(control);
        }
        project.Screens.Add(screen);
        project.VariableManager.Variables.Add(new Variable { Id = "v-bool", Name = "状态", Address = "1", DataType = "Bool" });

        var code = new CodeGenerator().GenerateSharedLibraryProject(project);

        Assert.Contains("hmi_ui_refresh_timer_cb", code);
        CSource.AssertCode(code, "lv_led_set_brightness(obj_led1, (v) != 0.0f ? 255 : 0);");
        CSource.AssertCode(code, "lv_obj_add_state(obj_sw1, LV_STATE_CHECKED);");
        CSource.AssertCode(code, "lv_obj_remove_state(obj_sw1, LV_STATE_CHECKED);");
        CSource.AssertCode(code, "lv_obj_add_state(obj_cb1, LV_STATE_CHECKED);");
        CSource.AssertCode(code, "lv_obj_remove_state(obj_cb1, LV_STATE_CHECKED);");
        CSource.AssertCode(code, "get_tag_value(TAG_v_bool)");
    }

    [Fact]
    public void GenerateAlarmCode_AcknowledgeLookup_IsGenerated()
    {
        var generator = new CodeGenerator();
        var groups = new[] { CreateGroup("温度异常", "ack1"), CreateGroup("通讯故障", "ack2") };

        var code = generator.GenerateAlarmCode(groups);

        // 补全的确认功能：注册表 + 真实 alarm_acknowledge（不再空实现 return 0）
        Assert.Contains("_alarm_lookup[]", code);
        Assert.Contains("&_rec_ack1", code);
        Assert.Contains("&_rec_ack2", code);
        Assert.Contains("r->state = 2;", code);
        CSource.AssertCode(code, "r->acknowledged_at = lv_tick_get();");
        CSource.AssertCode(code, "alarm_acknowledge(uint16_t config_id)");
        // 每条 check 赋 config_id
        Assert.Contains("r->config_id = 0;", code);
        Assert.Contains("r->config_id = 1;", code);
    }

    [Fact]
    public void GenerateAlarmCode_Threshold_FloatLiteral_HasDecimalPoint()
    {
        var generator = new CodeGenerator();
        var group = CreateGroup("阈值", "thr1");
        group.Alarms[0].Condition.Threshold = 80.0;
        group.Alarms[0].Condition.ExpectedValue = 0.0;

        var code = generator.GenerateAlarmCode(new[] { group });

        // 修正后使用标准 C 浮点字面量（80.0f 而非非标准的 80f）
        Assert.DoesNotContain(">= 80f", code);
        Assert.Contains(">= 80.0f", code);
        Assert.Contains("0.0f", code);
    }

    [Fact]
    public void GenerateAlarmCode_HighLimitDeadband_GeneratesClearedExpression()
    {
        var generator = new CodeGenerator();
        var group = CreateGroup("温度异常", "db1");
        group.Alarms[0].Condition.Threshold = 80.0;
        group.Alarms[0].Condition.Deadband = 2.0;

        var code = generator.GenerateAlarmCode(new[] { group });

        // 触发：>= 80.0f；解除需回落穿越死区：<= 78.0f (80 - 2)
        CSource.AssertCode(code, "triggered = (val >= 80.0f)");
        CSource.AssertCode(code, "cleared = (val <= 78.0f)");
    }

    [Fact]
    public void GenerateAlarmCode_LowLimitDeadband_GeneratesClearedExpression()
    {
        var generator = new CodeGenerator();
        var group = CreateGroup("低限报警", "db2");
        group.Alarms[0].Condition.Threshold = 5.0;
        group.Alarms[0].Condition.Deadband = 1.0;
        group.Alarms[0].Condition.Type = AlarmConditionType.LowLimit;

        var code = generator.GenerateAlarmCode(new[] { group });

        CSource.AssertCode(code, "triggered = (val <= 5.0f)");
        CSource.AssertCode(code, "cleared = (val >= 6.0f)");
    }

    [Fact]
    public void GenerateAlarmCode_DelayTrigger_GeneratesPendingLogic()
    {
        var generator = new CodeGenerator();
        var group = CreateGroup("延时报警", "dl1");
        group.Alarms[0].Condition.Threshold = 80.0;
        group.Alarms[0].Condition.DelayMs = 1000;

        var code = generator.GenerateAlarmCode(new[] { group });

        // 延时触发挂起 + 超时激活 + 提前取消
        Assert.Contains("pending_since", code);
        CSource.AssertCode(code, "else if ((uint32_t)(now - r->pending_since) >= 1000u)");
        Assert.Contains("condition un-met during delay -> cancel pending", code);
    }

    [Fact]
    public void GenerateAlarmCode_InitAndActiveCount_AreGenerated()
    {
        var generator = new CodeGenerator();
        var groups = new[] { CreateGroup("温度异常", "ic1"), CreateGroup("通讯故障", "ic2") };

        var code = generator.GenerateAlarmCode(groups);

        // 复位 API：冷启动/热重载时清零所有记录（含 pending_since）
        CSource.AssertCode(code, "void alarm_engine_init(void)");
        CSource.AssertCode(code, "memset(&_rec_ic1, 0, sizeof(_rec_ic1));");
        CSource.AssertCode(code, "memset(&_rec_ic2, 0, sizeof(_rec_ic2));");
        // 活动报警计数 API
        CSource.AssertCode(code, "uint16_t alarm_get_active_count(void)");
        CSource.AssertCode(code, "if (_rec_ic1.state == 1) n++;");
        CSource.AssertCode(code, "if (_rec_ic2.state == 1) n++;");
        Assert.Contains("return n;", code);
    }

    private static AlarmGroup CreateGroup(string name, string alarmId)
    {
        var group = new AlarmGroup { Id = alarmId + "-g", Name = name };
        group.Alarms.Add(new AlarmConfig
        {
            Id = alarmId,
            Name = name,
            Severity = AlarmSeverity.Critical,
            RequiresAck = true,
            Condition = new AlarmCondition
            {
                Type = AlarmConditionType.HighLimit,
                Threshold = 80.0,
                Deadband = 2.0,
                DelayMs = 1000,
            }
        });
        return group;
    }
}
