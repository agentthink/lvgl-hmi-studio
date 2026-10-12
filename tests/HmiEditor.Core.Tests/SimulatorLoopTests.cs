using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// I11: 生成代码验证器 + 模拟器工程导出器测试 —— "设计→生成→验证"闭环。
/// </summary>
public class SimulatorLoopTests
{
    // ── 测试基建 ──

    private static HmiProject BuildCleanProject()
    {
        var project = new HmiProject();
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c1", Name = "StartBtn", Type = HmiControlType.Button,
            X = 10, Y = 20, Width = 100, Height = 40, Text = "启动"
        });
        project.Screens.Add(screen);
        return project;
    }

    // ── 验证器: 干净项目 ──

    [Fact]
    public void Validate_CleanProject_NoErrors()
    {
        var project = BuildCleanProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project);

        var findings = GeneratedCodeValidator.Validate(project, code);

        Assert.DoesNotContain(findings, f => f.Severity == FindingSeverity.Error);
    }

    [Fact]
    public void Validate_EmptyProject_ReportsError()
    {
        var project = new HmiProject();
        var findings = GeneratedCodeValidator.Validate(project, "");

        Assert.Contains(findings, f =>
            f.Severity == FindingSeverity.Error && f.Category == FindingCategory.Structure);
    }

    // ── 验证器: 各类问题检出 ──

    [Fact]
    public void Validate_DuplicateControlId_ReportsError()
    {
        var project = BuildCleanProject();
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "c1", Name = "Dup", Type = HmiControlType.Label, X = 0, Y = 0
        });

        var findings = GeneratedCodeValidator.Validate(project, "create_screen_s1");

        Assert.Contains(findings, f =>
            f.Severity == FindingSeverity.Error && f.Category == FindingCategory.Structure
            && f.Message.Contains("c1"));
    }

    [Fact]
    public void Validate_UnresolvedFaceplate_ReportsError()
    {
        var project = BuildCleanProject();
        project.Screens[0].Controls[0].Text = "状态: {{state}}";

        var findings = GeneratedCodeValidator.Validate(project, "create_screen_s1");

        Assert.Contains(findings, f =>
            f.Severity == FindingSeverity.Error && f.Category == FindingCategory.Faceplate);
    }

    [Fact]
    public void Validate_InvalidExpression_ReportsError()
    {
        var project = BuildCleanProject();
        project.Screens[0].Controls[0].VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Text",
            Kind = BindingSourceKind.Expression,
            Expression = "1 + * 2"
        });

        var findings = GeneratedCodeValidator.Validate(project, "create_screen_s1");

        Assert.Contains(findings, f =>
            f.Severity == FindingSeverity.Error && f.Category == FindingCategory.Expression);
    }

    [Fact]
    public void Validate_DanglingReference_ReportsWarning()
    {
        var project = BuildCleanProject();
        project.Screens[0].Controls[0].VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Text",
            VariableId = "deleted_var",
            Direction = BindingDirection.ReadOnly
        });

        var findings = GeneratedCodeValidator.Validate(project, "create_screen_s1");

        Assert.Contains(findings, f =>
            f.Severity == FindingSeverity.Warning && f.Category == FindingCategory.DanglingRef
            && f.Message.Contains("deleted_var"));
    }

    [Fact]
    public void Validate_UnknownLvglApi_ReportsWarning()
    {
        var code = """
            static lv_obj_t* create_screen_s1(void) {
                lv_obj_t* o = lv_obj_create(NULL);
                lv_obj_set_legacy_mode(o, 1);  /* 不存在的 API */
                return o;
            }
            """;

        var findings = GeneratedCodeValidator.Validate(BuildCleanProject(), code);

        Assert.Contains(findings, f =>
            f.Severity == FindingSeverity.Warning && f.Category == FindingCategory.LvglApi
            && f.Message.Contains("lv_obj_set_legacy_mode"));
    }

    [Fact]
    public void Validate_KnownLvglApi_NoWarning()
    {
        var code = """
            static lv_obj_t* create_screen_s1(void) {
                lv_obj_t* o = lv_obj_create(NULL);
                lv_obj_set_size(o, 100, 50);
                lv_obj_set_pos(o, 10, 20);
                lv_label_set_text(o, "hi");
                return o;
            }
            """;

        var findings = GeneratedCodeValidator.Validate(BuildCleanProject(), code);

        Assert.DoesNotContain(findings, f => f.Category == FindingCategory.LvglApi);
    }

    [Fact]
    public void Validate_CodeGenWarningComment_Reported()
    {
        var project = BuildCleanProject();
        project.Screens[0].Controls[0].VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Text",
            VariableId = "{{param}}",
            Direction = BindingDirection.ReadOnly
        });

        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var findings = GeneratedCodeValidator.Validate(project, code);

        // CodeGen 对未解析 Faceplate 参数输出 WARNING 注释 → 双重检出(Faceplate Error + CodeGen Warning)
        Assert.Contains(findings, f => f.Category == FindingCategory.CodeGen);
    }

    [Fact]
    public void ExtractLvglCalls_FindsFunctions()
    {
        var code = "lv_obj_create(NULL); lv_obj_set_size(o, 1, 2); lv_obj_create(NULL);";
        var calls = GeneratedCodeValidator.ExtractLvglCalls(code);
        Assert.Contains("lv_obj_create", calls);
        Assert.Contains("lv_obj_set_size", calls);
        Assert.Equal(2, calls.Count); // 去重
    }

    // ── 导出器 ──

    private static string NewTempDir() => TestTempDir.NewDir("hmi-sim-test-");

    [Fact]
    public void ExtractScreenFunctions_ParsesAll()
    {
        var code = """
            static lv_obj_t* create_screen_a(void) { return 0; }
            static lv_obj_t* create_screen_b(void) { return 0; }
            """;
        var functions = SimulatorProjectExporter.ExtractScreenFunctions(code);
        Assert.Equal(new[] { "create_screen_a", "create_screen_b" }, functions);
    }

    [Fact]
    public void Export_GeneratesAllFiles()
    {
        var project = BuildCleanProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            var result = SimulatorProjectExporter.Export(project, code, dir);

            Assert.Equal(new[] { "main.c", "sim_config.h", "CMakeLists.txt", "lv_conf.h", "generated_hmi.c", "generated_comm.h", "generated_security.h", "generated_runtime.h", "README.md" },
                result.Files);
            Assert.Equal(new[] { "create_screen_s1" }, result.ScreenFunctions);
            Assert.True(File.Exists(Path.Combine(dir, "main.c")));
            Assert.True(File.Exists(Path.Combine(dir, "sim_config.h")));
            Assert.True(File.Exists(Path.Combine(dir, "generated_hmi.c")));
            Assert.True(File.Exists(Path.Combine(dir, "generated_comm.h")));
            Assert.True(File.Exists(Path.Combine(dir, "generated_security.h")));
            Assert.True(File.Exists(Path.Combine(dir, "generated_runtime.h")));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void Export_MainC_RegistersScreenFactory()
    {
        var project = BuildCleanProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            Assert.Contains("create_screen_s1", mainC);
            CSource.AssertCode(mainC, "lv_screen_load(screens[0])");
            Assert.Contains("lv_sdl_window_create", mainC);
            CSource.AssertCode(mainC, "lv_timer_handler()");
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void Export_MainC_RegistersInputDevicesAndSelftest()
    {
        var project = BuildCleanProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            // 外设兼容: 鼠标(含滚轮/触摸) / 键盘
            CSource.AssertCode(mainC, "lv_sdl_mouse_create()");
            CSource.AssertCode(mainC, "lv_sdl_keyboard_create()");

            // 可配置: 命令行参数
            Assert.Contains("--scale", mainC);
            Assert.Contains("--fps", mainC);

            // 自动化测试: 自检模式
            Assert.Contains("--selftest", mainC);
            Assert.Contains("run_selftest", mainC);
            Assert.Contains("[selftest] PASS", mainC);

            // 性能: 帧率节流
            Assert.Contains("frame_delay", mainC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void Export_MainC_ScaleUsesZoomNotCanvasResize()
    {
        // 回归防护：--scale 必须用 lv_sdl_window_set_zoom 放大窗口（画布不变），
        // 而不是把画布分辨率一起乘 scale（那样会导致画面缩在左上角/留白）。
        var project = BuildCleanProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            // 画布恒为 --window/设计分辨率，不乘 scale
            CSource.AssertCall(mainC, "lv_sdl_window_create(canvas_w, canvas_h)");
            // --scale 走 zoom
            CSource.AssertCall(mainC, "lv_sdl_window_set_zoom(disp, (float)cfg.scale)");
            // 旧的错误写法（窗口尺寸乘 scale 当作画布）不得再出现
            Assert.DoesNotContain("win_w * cfg.scale", mainC);
            Assert.DoesNotContain("canvas_w * cfg.scale", mainC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void Export_MainC_AdaptsWindowResolutionToCanvas()
    {
        // 回归防护：--window 指定非设计分辨率时，必须用 fit_screen_to_canvas 铺满/适配画布，
        // 并禁用窗口自由缩放（避免 SDL resize 改画布却不重排）。
        var project = BuildCleanProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            // 屏幕自适应函数存在，且在屏幕加载后对所有屏幕调用
            Assert.Contains("static void fit_screen_to_canvas", mainC);
            CSource.AssertCode(mainC, "fit_screen_to_canvas(screens[i])");
            // 屏幕根对象铺满画布 + 子对象缩放
            CSource.AssertCode(mainC, "lv_obj_set_size(scr, dw, dh)");
            Assert.Contains("scale_child_pos_size", mainC);
            // 禁用窗口自由缩放（方案 E）
            CSource.AssertCode(mainC, "lv_sdl_window_set_resizeable(disp, false)");
            CSource.AssertNoCode(mainC, "lv_sdl_window_set_resizeable(disp, true)");
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void Export_SimConfig_ContainsTunables()
    {
        var project = BuildCleanProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var config = File.ReadAllText(Path.Combine(dir, "sim_config.h"));

            Assert.Contains("SIM_WINDOW_WIDTH", config);
            Assert.Contains("SIM_WINDOW_HEIGHT", config);
            Assert.Contains("SIM_TARGET_FPS", config);
            Assert.Contains("SIM_SCALE", config);
            Assert.Contains("SIM_SELFTEST_FRAMES", config);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void Export_Cmake_CrossPlatformWithCtest()
    {
        var project = BuildCleanProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var cmake = File.ReadAllText(Path.Combine(dir, "CMakeLists.txt"));

            Assert.Contains("generated_hmi.c", cmake);
            Assert.Contains("main.c", cmake);
            Assert.Contains("add_subdirectory", cmake);
            Assert.Contains("LV_CONF_INCLUDE_SIMPLE", cmake);

            // 跨平台: Windows / Linux / macOS 分支
            Assert.Contains("MSVC", cmake);
            Assert.Contains("WIN32", cmake);
            Assert.Contains("APPLE", cmake);

            // CPU 架构检测
            Assert.Contains("CMAKE_SYSTEM_PROCESSOR", cmake);
            Assert.Contains("SIM_ARCH", cmake);

            // 性能: Release 默认优化
            Assert.Contains("-O2", cmake);

            // 自动化测试: ctest 集成
            CSource.AssertCode(cmake, "enable_testing()");
            Assert.Contains("simulator_selftest", cmake);
            Assert.Contains("SDL_VIDEODRIVER=dummy", cmake);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void Export_LvConf_HasPerformanceTuning()
    {
        var project = BuildCleanProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var conf = File.ReadAllText(Path.Combine(dir, "lv_conf.h"));

            Assert.Contains("LV_MEM_SIZE", conf);
            Assert.Contains("LV_DISP_DEF_REFR_PERIOD", conf);
            Assert.Contains("LV_USE_PERF_MONITOR", conf);
            Assert.Contains("LV_USE_MEM_MONITOR", conf);
            Assert.Contains("LV_USE_DRAW_SW", conf);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void Export_Readme_DocumentsSupportMatrixAndFAQ()
    {
        var project = BuildCleanProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var readme = File.ReadAllText(Path.Combine(dir, "README.md"));

            Assert.Contains("支持矩阵", readme);
            Assert.Contains("Windows", readme);
            Assert.Contains("macOS", readme);
            Assert.Contains("FAQ", readme);
            Assert.Contains("sim_config.h", readme);
            Assert.Contains("ctest", readme);
            Assert.Contains("安全", readme);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void Export_CmakeReferencesGeneratedSource()
    {
        var project = BuildCleanProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var cmake = File.ReadAllText(Path.Combine(dir, "CMakeLists.txt"));

            Assert.Contains("generated_hmi.c", cmake);
            Assert.Contains("main.c", cmake);
            Assert.Contains("add_subdirectory", cmake);
            Assert.Contains("LV_CONF_INCLUDE_SIMPLE", cmake);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void Export_NoScreenFunctions_Throws()
    {
        var project = BuildCleanProject();
        var dir = NewTempDir();
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                SimulatorProjectExporter.Export(project, "int x = 0;", dir));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── 端到端: 验证→导出闭环 ──

    [Fact]
    public void FullLoop_CleanProject_ValidateThenExport()
    {
        var project = BuildCleanProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var findings = GeneratedCodeValidator.Validate(project, code);

        // 验证通过才允许导出
        Assert.False(GeneratedCodeValidator.HasErrors(findings));

        var dir = NewTempDir();
        try
        {
            var result = SimulatorProjectExporter.Export(project, code, dir);
            Assert.NotEmpty(result.Files);
            Assert.Single(result.ScreenFunctions);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void FullLoop_BrokenProject_BlockedByValidator()
    {
        var project = BuildCleanProject();
        project.Screens[0].Controls[0].Text = "{{unresolved}}";

        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var findings = GeneratedCodeValidator.Validate(project, code);

        // 未解析 Faceplate → Error → 模拟器导出应被阻断
        Assert.True(GeneratedCodeValidator.HasErrors(findings));
        Assert.Contains(findings, f => f.Category == FindingCategory.Faceplate);
    }

    // ── 模拟器导出产物一致性 (D1/D7 修复) ──
    // 防止 sim-output/main.c 旧产物掩盖生成器 bug:每次导出 main.c 必须包含
    //   1) sim_refresh_bindings(sim_tick) 调用
    //   2) sim_tick++ 自增
    //   3) 当工程有 enabled alarm 时: alarm_engine_evaluate 调用
    //   4) 当工程有 enabled trend 时: trend_engine_tick 调用

    private static AlarmGroup CreateAlarmGroup(string alarmId)
        => new()
        {
            Id = alarmId + "-g",
            Name = "测试报警",
            Alarms = { new AlarmConfig { Id = alarmId, Name = "温度高", IsEnabled = true, Condition = new AlarmCondition { Type = AlarmConditionType.HighLimit, Threshold = 80 } } }
        };

    private static TrendConfig CreateTrend(string id)
        => new()
        {
            Id = id, Name = "温度趋势", MaxPoints = 100,
            Pens = { new TrendPen { Label = "温度", Color = "#ef4444", VariableId = "v-temp", IsEnabled = true } },
            IsEnabled = true,
        };

    [Fact]
    public void Export_MainC_BindingsRefresh_AlwaysPolled()
    {
        // D1 回归：当工程有变量绑定 + Export 传入 tags 时，main.c 必须包含 sim_refresh_bindings 钩子
        var project = BuildCleanProject();
        var btn = project.Screens[0].Controls[0];
        btn.VariableBindings.Add(new VariableBindingData
        {
            VariableId = "v-temp",
            Direction = BindingDirection.ReadOnly,
            PropertyName = "Text",
        });
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v-temp", Name = "温度", Address = "40001", DataType = "Float",
        });
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        // 用项目变量的最小 RuntimeTag 列表传入 Export
        var tags = new List<RuntimeTag>
        {
            new() { VariableId = "v-temp", Address = "40001", DataType = "Float" },
        };
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assetLibrary: null, tags: tags);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            // 调用形参为 sim_tick 值传递(不是 sim_tick++),
            // 自增发生在主循环末尾,这样报警/趋势引擎也能读 sim_tick
            CSource.AssertCode(mainC, "sim_refresh_bindings(sim_tick);");
            Assert.Contains("sim_tick++;", mainC);
            CSource.AssertNoCode(mainC, "sim_refresh_bindings(sim_tick++);");
        }
        finally { TestTempDir.DeleteDir(dir); }
    }

    [Fact]
    public void Export_MainC_AlarmEngine_PolledWhenEnabled()
    {
        // D7 回归：工程有 enabled alarm 时，main.c 必须包含 alarm_engine_evaluate 调用
        var project = BuildCleanProject();
        project.AlarmGroups.Add(CreateAlarmGroup("test-alarm"));
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            CSource.AssertCode(mainC, "alarm_engine_evaluate(s_sim_engine_snap, s_engine_n);");
            // 报警引擎存在时也会调用趋势引擎吗？这里没有 trend，所以 trend 不应出现
            CSource.AssertNoCode(mainC, "trend_engine_tick(");
        }
        finally { TestTempDir.DeleteDir(dir); }
    }

    [Fact]
    public void Export_MainC_TrendEngine_PolledWhenEnabled()
    {
        // D7 回归：工程有 enabled trend 时，main.c 必须包含 trend_engine_tick 调用
        var project = BuildCleanProject();
        project.TrendConfigs.Add(CreateTrend("tr-temp"));
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            CSource.AssertCode(mainC, "trend_engine_tick(s_sim_engine_snap, (uint8_t)s_engine_n);");
            // 趋势引擎存在但没有 alarm，所以 alarm 不应出现
            CSource.AssertNoCode(mainC, "alarm_engine_evaluate(");
        }
        finally { TestTempDir.DeleteDir(dir); }
    }

    [Fact]
    public void Export_MainC_NoEngines_WhenDisabled()
    {
        // D7 防护：工程无 enabled alarm/trend 时，main.c 不得包含任何引擎调用
        var project = BuildCleanProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            CSource.AssertNoCode(mainC, "alarm_engine_evaluate(");
            CSource.AssertNoCode(mainC, "trend_engine_tick(");
        }
        finally { TestTempDir.DeleteDir(dir); }
    }

    [Fact]
    public void Export_MainC_VariableLevelAlarm_AlsoTriggersEngine()
    {
        // D7 防护：变量级报警（不是工程级 AlarmGroups）也应触发引擎轮询
        var project = BuildCleanProject();
        var v = new Variable
        {
            Id = "v-temp", Name = "温度", Address = "40001", DataType = "Float",
            ConnectionId = "",
        };
        v.Alarms.Add(new VariableAlarmConfig
        {
            Id = "var-alarm-1", Name = "温度高", IsEnabled = true,
            Type = AlarmConditionType.HighLimit, Threshold = 80,
        });
        project.VariableManager.Variables.Add(v);
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            CSource.AssertCode(mainC, "alarm_engine_evaluate(s_sim_engine_snap, s_engine_n);");
        }
        finally { TestTempDir.DeleteDir(dir); }
    }
    [Fact]
    public void FullLoop_RuntimeEnabledProject_MainC_WiresAllEngines()
    {
        // Golden 运行时场景回归：绑定 + 报警 + 趋势同时存在时，
        // Export 传入 tags 后 main.c 必须包含动态刷新和两个引擎调用
        // （防止 SimExport 漏传标签表导致主循环退化为静态显示）。
        var project = BuildCleanProject();
        project.VariableManager.Connections.Add(new DeviceConnection
        {
            Id = "conn1", Name = "PLC", DriverId = "modbus-tcp"
        });
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v-temp", Name = "温度", Address = "40001",
            DataType = "Float", ConnectionId = "conn1"
        });
        var label = new HmiControl
        {
            Id = "lbl_temp", Name = "温度", Type = HmiControlType.Label
        };
        label.VariableBindings.Add(new VariableBindingData
        {
            VariableId = "v-temp", Direction = BindingDirection.ReadOnly, PropertyName = "Text"
        });
        project.Screens[0].Controls.Add(label);
        project.AlarmGroups.Add(CreateAlarmGroup("test-alarm"));
        project.TrendConfigs.Add(CreateTrend("tr-temp"));

        var bindingEngine = new RuntimeBindingEngine(project);
        bindingEngine.BuildRuntimeTags();

        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, tags: bindingEngine.Tags);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));
            var genC = File.ReadAllText(Path.Combine(dir, "generated_hmi.c"));

            CSource.AssertCode(mainC, "sim_refresh_bindings(sim_tick);");
            CSource.AssertCode(mainC, "alarm_engine_evaluate(s_sim_engine_snap, s_engine_n);");
            CSource.AssertCode(mainC, "trend_engine_tick(s_sim_engine_snap, (uint8_t)s_engine_n);");
            CSource.AssertCode(genC, "void sim_refresh_bindings(uint32_t tick)");
        }
        finally { TestTempDir.DeleteDir(dir); }
    }



    [Fact]
    public void Export_MainC_DefinesScreenSwitchRuntime()
    {
        // Golden 动作回归：工程含 SwitchScreen 动作时，main.c 必须提供
        // hmi_screen_switch 实现，否则生成代码 extern 引用在链接期失败。
        var project = BuildCleanProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.SwitchScreen("Main"));
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            CSource.AssertCode(mainC, "int hmi_screen_switch(const char* name)");
            CSource.AssertCode(mainC, "lv_screen_load(screens[i]);");
            Assert.Contains("static const char* const screen_names[]", mainC);

        }
        finally { TestTempDir.DeleteDir(dir); }
    }

    // ── 字体尺寸回退 (D5 修复) ──
    // 当 FreeType 加载失败或 LV_USE_FREETYPE 未启用时，字体回退必须按 size 取最近可用
    // 嵌入 CJK 子集（14/16），不能硬编码 14px 导致大字号文本变小。

    [Fact]
    public void Export_FontsC_GeneratesSizeAwareFallback()
    {
        // D5 回归：工程有字体资产时，generated_fonts.c 必须包含 sim_fallback_cjk_font 函数
        var project = BuildCleanProject();
        var lib = new AssetLibrary();
        lib.AddAsset(new AssetItem { Id = "fnt-cjk", Name = "CJK", Type = AssetType.Font, FilePath = "/tmp/fake.ttf" });
        project.Theme.FontSize = 18;  // 触发非默认字号
        var code = new CodeGenerator().GenerateSimulatorModule(project, lib);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assetLibrary: lib);
            var fontsC = File.ReadAllText(Path.Combine(dir, "generated_fonts.c"));

            // 模拟器回退必须使用 LVGL 公共默认字体，避免依赖不存在的 SimSun 符号。
            Assert.Contains("sim_fallback_cjk_font", fontsC);
            CSource.AssertCode(fontsC, "return lv_font_get_default();");
            CSource.AssertCode(fontsC, "(void)size;");
            // 旧的硬编码 fallback 不能再出现
            Assert.DoesNotContain("= *lv_font_simsun_14_cjk;", fontsC);
        }
        finally { TestTempDir.DeleteDir(dir); }
    }

    [Fact]
    public void Export_FontsC_FallbackSize_PickedByRequest()
    {
        // D5 回归：生成代码在 FreeType 加载失败分支应调用 sim_fallback_cjk_font(实际 size)，
        // 而不是直接用 lv_font_simsun_14_cjk（避免 32px 文本回退成 14px）。
        var project = BuildCleanProject();
        var lib = new AssetLibrary();
        lib.AddAsset(new AssetItem { Id = "fnt-cjk", Name = "CJK", Type = AssetType.Font, FilePath = "/tmp/fake.ttf" });
        project.Theme.FontSize = 24;  // 字号 24 → fallback 走 16px（>14）
        var code = new CodeGenerator().GenerateSimulatorModule(project, lib);
        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assetLibrary: lib);
            var fontsC = File.ReadAllText(Path.Combine(dir, "generated_fonts.c"));

            // 加载失败分支必须调用 sim_fallback_cjk_font(24)，且 asset id 的 `-` 经 Sanitize 变成 `_`
            CSource.AssertCode(fontsC, "sim_fallback_cjk_font(FONT_PXSIZE_fnt_cjk)");
            Assert.DoesNotContain("= *lv_font_simsun_14_cjk;", fontsC);
        }
        finally { TestTempDir.DeleteDir(dir); }
    }
}
