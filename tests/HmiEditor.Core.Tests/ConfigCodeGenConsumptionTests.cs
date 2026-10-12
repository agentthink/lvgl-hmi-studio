using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 配置模型 CodeGen 消费端测试 —— 验证 ExportConfig / RuntimeConfig 等工程设置子模型
/// 真正被 CodeGenerator 消费，而非仅序列化存盘。
/// </summary>
public class ConfigCodeGenConsumptionTests
{
    // ── ExportConfig.IncludeLgplDisclaimer ──

    [Fact]
    public void ExportConfig_IncludeLgplDisclaimer_EmitsHeader()
    {
        var project = CreateProject();
        project.ExportConfig.IncludeLgplDisclaimer = true;

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("SPDX-License-Identifier: LGPL-2.1-or-later", code);
        Assert.Contains("Links against LVGL (MIT license)", code);
    }

    [Fact]
    public void ExportConfig_LgplDisclaimer_Disabled_OmitsHeader()
    {
        var project = CreateProject();
        project.ExportConfig.IncludeLgplDisclaimer = false;

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.DoesNotContain("SPDX-License-Identifier", code);
    }

    // ── RuntimeConfig.BootScreenId ──

    [Fact]
    public void RuntimeConfig_BootScreenId_SelectsInitialScreen()
    {
        var project = CreateProject();
        project.Screens.Add(new HmiScreen { Id = "s2", Name = "Second", Width = 800, Height = 480 });
        // 指定第二个屏幕为开机屏幕
        project.RuntimeConfig.BootScreenId = "s2";

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("lv_screen_load(screen_s2);", code);
        Assert.DoesNotContain("lv_screen_load(screen_s1);", code);
    }

    [Fact]
    public void RuntimeConfig_BootScreenId_Empty_FallsBackToFirst()
    {
        var project = CreateProject();
        project.Screens.Add(new HmiScreen { Id = "s2", Name = "Second", Width = 800, Height = 480 });
        // BootScreenId 为空 → 回退 Screens[0]
        project.RuntimeConfig.BootScreenId = null;

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("lv_screen_load(screen_s1);", code);
    }

    // ── ExportConfig.GenerateBuildSystem ──

    [Fact]
    public void ExportConfig_BuildSystem_None_ReturnsNull()
    {
        var project = CreateProject();
        project.ExportConfig.GenerateBuildSystem = BuildSystem.None;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.Null(artifact);
    }

    [Fact]
    public void ExportConfig_BuildSystem_CMake_GeneratesCMakeLists()
    {
        var project = CreateProject();
        project.Name = "DemoProject";
        project.ExportConfig.GenerateBuildSystem = BuildSystem.CMake;
        project.ExportConfig.CodeGenOptimization = CodeGenOptimization.Size;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        Assert.Equal("CMakeLists.txt", artifact!.FileName);
        Assert.Contains("project(DemoProject C)", artifact.Content);
        Assert.Contains("generated_hmi.c", artifact.Content);
        Assert.Contains("-Os", artifact.Content); // Size 优化 → -Os
        Assert.Contains("add_executable", artifact.Content);
    }

    [Fact]
    public void ExportConfig_BuildSystem_Makefile_GeneratesMakefile()
    {
        var project = CreateProject();
        project.Name = "DemoProject";
        project.ExportConfig.GenerateBuildSystem = BuildSystem.Makefile;
        project.ExportConfig.CodeGenOptimization = CodeGenOptimization.Speed;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        Assert.Equal("Makefile", artifact!.FileName);
        Assert.Contains("DemoProject: $(HMI_SOURCES)", artifact.Content);
        Assert.Contains("-O2", artifact.Content); // Speed 优化 → -O2
        Assert.Contains("generated_hmi.c", artifact.Content);
    }

    [Fact]
    public void ExportConfig_BuildSystem_PlatformIO_GeneratesPlatformIni()
    {
        var project = CreateProject();
        project.ExportConfig.GenerateBuildSystem = BuildSystem.PlatformIO;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        Assert.Equal("platformio.ini", artifact!.FileName);
        Assert.Contains("[env:native]", artifact.Content);
        // src_filter 包含所有 C/C++ 源，不排除用户添加的辅助文件
        Assert.Contains("src_filter = +<*.c> +<*.cpp>", artifact.Content);
    }

    [Fact]
    public void ExportConfig_BuildSystem_ChineseProjectName_FallsBackToHmiApp()
    {
        var project = CreateProject();
        project.Name = "演示工程"; // 中文项目名无法转合法 C 标识符
        project.ExportConfig.GenerateBuildSystem = BuildSystem.CMake;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        Assert.Contains("project(hmi_app C)", artifact!.Content);
    }

    // ── ExportConfig.GenerateDebugSymbols ──

    [Theory]
    [InlineData(BuildSystem.CMake)]
    [InlineData(BuildSystem.Makefile)]
    [InlineData(BuildSystem.PlatformIO)]
    public void ExportConfig_GenerateDebugSymbols_AddsFlagToAllThreeBuildSystems(BuildSystem bs)
    {
        var project = CreateProject();
        project.ExportConfig.GenerateBuildSystem = bs;
        project.ExportConfig.GenerateDebugSymbols = true;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        Assert.Contains("-g", artifact!.Content);
    }

    [Theory]
    [InlineData(BuildSystem.CMake)]
    [InlineData(BuildSystem.Makefile)]
    [InlineData(BuildSystem.PlatformIO)]
    public void ExportConfig_GenerateDebugSymbols_DefaultAndExplicitFalse_AreByteIdentical(BuildSystem bs)
    {
        // 本项默认 false（老工程 JSON 里也没有这个键）。判据：
        // 「没设过」与「显式设为 false」生成的构建配置逐字节相同 —— 即新增该选项不改变既有产物。
        var untouched = CreateProject();
        untouched.ExportConfig.GenerateBuildSystem = bs;

        var explicitFalse = CreateProject();
        explicitFalse.ExportConfig.GenerateBuildSystem = bs;
        explicitFalse.ExportConfig.GenerateDebugSymbols = false;

        var a = new CodeGenerator().GenerateBuildSystem(untouched);
        var b = new CodeGenerator().GenerateBuildSystem(explicitFalse);

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(a!.Content, b!.Content);
    }

    [Theory]
    [InlineData(BuildSystem.CMake)]
    [InlineData(BuildSystem.Makefile)]
    [InlineData(BuildSystem.PlatformIO)]
    public void ExportConfig_GenerateDebugSymbols_DoesNotTouchGeneratedSource(BuildSystem bs)
    {
        // 契约：本项只影响构建配置里的编译选项，不得改动 generated_hmi.c 的任何一行。
        var withSymbols = CreateProject();
        withSymbols.ExportConfig.GenerateBuildSystem = bs;
        withSymbols.ExportConfig.GenerateDebugSymbols = true;

        var without = CreateProject();
        without.ExportConfig.GenerateBuildSystem = bs;

        Assert.Equal(
            new CodeGenerator().GenerateFullProject(without),
            new CodeGenerator().GenerateFullProject(withSymbols));
    }

    // ── ExportConfig.IncludeComments ──

    [Fact]
    public void ExportConfig_IncludeComments_True_EmitsAnnotations()
    {
        var project = CreateProjectWithControlAndActions();
        project.ExportConfig.IncludeComments = true;

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("// StartBtn (Button)", code);               // 控件标注
        Assert.Contains("// Bound variable: v_motor", code);         // 绑定标注
        Assert.Contains("/* ShowAlarms: navigate to alarm view */", code); // 动作描述
        Assert.Contains("hmi_show_alarms();", code);                 // 代码
    }

    [Fact]
    public void ExportConfig_IncludeComments_False_StripsAnnotations()
    {
        var project = CreateProjectWithControlAndActions();
        project.ExportConfig.IncludeComments = false;

        var code = new CodeGenerator().GenerateFullProject(project);

        // 标注性注释被移除
        Assert.DoesNotContain("// StartBtn (Button)", code);
        Assert.DoesNotContain("// Bound variable: v_motor", code);
        Assert.DoesNotContain("/* ShowAlarms: navigate to alarm view */", code);
        // 代码始终保留
        Assert.Contains("hmi_show_alarms();", code);
        Assert.Contains("lv_obj_set_pos", code);
        // 头部元信息保留
        Assert.Contains("// Auto-generated by HmiEditor", code);
        // TODO 注释保留（悬空屏幕目标的功能性标记）
        Assert.Contains("TODO: Switch to screen 'NonExistent'", code);
    }

    /// <summary>构造含 1 按钮（变量绑定 + 悬空 SwitchScreen + ShowAlarms）的项目用于注释开关验证。</summary>
    private static HmiProject CreateProjectWithControlAndActions()
    {
        var project = new HmiProject { Name = "Test", LvglVersion = "9.2" };
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        var btn = new HmiControl
        {
            Id = "c1", Name = "StartBtn", Type = HmiControlType.Button,
            VariableBindingId = "v_motor", X = 10, Y = 10, Width = 100, Height = 40
        };
        btn.Actions.Add(HmiAction.SwitchScreen("NonExistent")); // 悬空 → 触发 TODO
        btn.Actions.Add(HmiAction.ShowAlarms);
        screen.Controls.Add(btn);
        project.Screens.Add(screen);
        return project;
    }

    // ── ExportConfig.TargetLanguage ──

    [Fact]
    public void ExportConfig_TargetLanguage_C_NoExternCWrapper()
    {
        var project = CreateProject();
        project.ExportConfig.TargetLanguage = TargetLanguage.C;

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.DoesNotContain("extern \"C\"", code);
        Assert.Contains("#include \"lvgl.h\"", code);
        Assert.Contains("extern int  hmi_screen_switch", code);
    }

    [Fact]
    public void ExportConfig_TargetLanguage_Cpp_WrapsFirmwareApiInExternC()
    {
        var project = CreateProject();
        project.ExportConfig.TargetLanguage = TargetLanguage.Cpp;

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("extern \"C\" {", code);
        Assert.Contains("}  /* extern \"C\" — 固件 C API */", code);
        // extern 声明位于包裹块内：open < api < close
        var idxOpen = code.IndexOf("extern \"C\" {");
        var idxApi = code.IndexOf("extern int  hmi_screen_switch");
        var idxClose = code.IndexOf("}  /* extern \"C\"");
        Assert.True(idxOpen >= 0 && idxApi > idxOpen && idxClose > idxApi);
    }

    [Fact]
    public void ExportConfig_TargetLanguage_Cpp_BuildSystemUsesCxx()
    {
        var project = CreateProject();
        project.Name = "DemoProject";
        project.ExportConfig.TargetLanguage = TargetLanguage.Cpp;
        project.ExportConfig.GenerateBuildSystem = BuildSystem.CMake;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        Assert.Contains("project(DemoProject CXX)", artifact!.Content);
        Assert.Contains("CMAKE_CXX_STANDARD 17", artifact.Content);
        Assert.Contains("generated_hmi.cpp", artifact.Content);
    }

    [Fact]
    public void ExportConfig_TargetLanguage_Cpp_MakefileUsesCxx()
    {
        var project = CreateProject();
        project.ExportConfig.TargetLanguage = TargetLanguage.Cpp;
        project.ExportConfig.GenerateBuildSystem = BuildSystem.Makefile;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        Assert.Contains("CXX ?= g++", artifact!.Content);
        Assert.Contains("-std=gnu++17", artifact.Content);
        Assert.Contains("generated_hmi.cpp", artifact.Content);
    }

    // ── ExportConfig.OutputDirectory 解析 ──

    [Fact]
    public void ExportConfig_ResolveOutputDirectory_Relative_CombinesWithProjectDir()
    {
        var cfg = new ExportConfig { OutputDirectory = "./export" };
        var resolved = cfg.ResolveOutputDirectory("/home/user/proj");
        Assert.Equal(System.IO.Path.Combine("/home/user/proj", "./export"), resolved);
    }

    [Fact]
    public void ExportConfig_ResolveOutputDirectory_Absolute_UsedDirectly()
    {
        var cfg = new ExportConfig { OutputDirectory = "/opt/output" };
        Assert.Equal("/opt/output", cfg.ResolveOutputDirectory("/home/user/proj"));
    }

    [Fact]
    public void ExportConfig_ResolveOutputDirectory_EmptyConfig_ReturnsNull()
    {
        var cfg = new ExportConfig { OutputDirectory = "" };
        Assert.Null(cfg.ResolveOutputDirectory("/home/user/proj"));
    }

    [Fact]
    public void ExportConfig_ResolveOutputDirectory_NoProjectDir_ReturnsNull()
    {
        var cfg = new ExportConfig { OutputDirectory = "./export" };
        Assert.Null(cfg.ResolveOutputDirectory(null));
    }

    // ── SecurityConfig.OperationMode → HMI_OPERATION_MODE 编译宏 ──

    [Fact]
    public void SecurityConfig_OperationMode_Designer_EmitsMode0_InCMake()
    {
        var project = CreateProject();
        project.ExportConfig.GenerateBuildSystem = BuildSystem.CMake;
        project.SecurityConfig.OperationMode = OperationMode.Designer;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        // Designer=0 → target_compile_definitions(... HMI_OPERATION_MODE=0)
        Assert.Contains("target_compile_definitions", artifact!.Content);
        Assert.Contains("HMI_OPERATION_MODE=0", artifact.Content);
    }

    [Fact]
    public void SecurityConfig_OperationMode_Operator_EmitsMode1_InMakefile()
    {
        var project = CreateProject();
        project.ExportConfig.GenerateBuildSystem = BuildSystem.Makefile;
        project.SecurityConfig.OperationMode = OperationMode.Operator;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        // Operator=1 → CFLAGS 含 -DHMI_OPERATION_MODE=1
        Assert.Contains("-DHMI_OPERATION_MODE=1", artifact!.Content);
    }

    [Fact]
    public void SecurityConfig_OperationMode_Maintenance_EmitsMode2_InPlatformIO()
    {
        var project = CreateProject();
        project.ExportConfig.GenerateBuildSystem = BuildSystem.PlatformIO;
        project.SecurityConfig.OperationMode = OperationMode.Maintenance;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        // Maintenance=2 → build_flags 含 -DHMI_OPERATION_MODE=2
        Assert.Contains("-DHMI_OPERATION_MODE=2", artifact!.Content);
    }

    [Fact]
    public void SecurityConfig_OperationMode_ReadOnly_EmitsMode3_InCMake()
    {
        var project = CreateProject();
        project.ExportConfig.GenerateBuildSystem = BuildSystem.CMake;
        project.SecurityConfig.OperationMode = OperationMode.ReadOnly;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        // ReadOnly=3 → HMI_OPERATION_MODE=3
        Assert.Contains("HMI_OPERATION_MODE=3", artifact!.Content);
    }

    [Fact]
    public void SecurityConfig_OperationMode_Default_Designer_WhenNotSet()
    {
        // CreateProject 不设置 OperationMode，默认应为 Designer(0)
        var project = CreateProject();
        project.ExportConfig.GenerateBuildSystem = BuildSystem.Makefile;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        Assert.Contains("-DHMI_OPERATION_MODE=0", artifact!.Content);
    }

    // ── SecurityConfig.EnableTamperDetection → HMI_TAMPER_DETECTION 编译宏 ──

    [Fact]
    public void SecurityConfig_TamperDetection_Enabled_EmitsMacro1_InCMake()
    {
        var project = CreateProject();
        project.ExportConfig.GenerateBuildSystem = BuildSystem.CMake;
        project.SecurityConfig.EnableTamperDetection = true;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        // EnableTamperDetection=true → HMI_TAMPER_DETECTION=1
        Assert.Contains("HMI_TAMPER_DETECTION=1", artifact!.Content);
    }

    [Fact]
    public void SecurityConfig_TamperDetection_Disabled_EmitsMacro0_InMakefile()
    {
        var project = CreateProject();
        project.ExportConfig.GenerateBuildSystem = BuildSystem.Makefile;
        project.SecurityConfig.EnableTamperDetection = false;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        // EnableTamperDetection=false → -DHMI_TAMPER_DETECTION=0
        Assert.Contains("-DHMI_TAMPER_DETECTION=0", artifact!.Content);
    }

    [Fact]
    public void SecurityConfig_TamperDetection_Enabled_EmitsMacro1_InPlatformIO()
    {
        var project = CreateProject();
        project.ExportConfig.GenerateBuildSystem = BuildSystem.PlatformIO;
        project.SecurityConfig.EnableTamperDetection = true;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        Assert.Contains("-DHMI_TAMPER_DETECTION=1", artifact!.Content);
    }

    [Fact]
    public void SecurityConfig_TamperDetection_DefaultDisabled_WhenNotSet()
    {
        // CreateProject 不设置 EnableTamperDetection，默认应为 false(0)
        var project = CreateProject();
        project.ExportConfig.GenerateBuildSystem = BuildSystem.CMake;

        var artifact = new CodeGenerator().GenerateBuildSystem(project);

        Assert.NotNull(artifact);
        Assert.Contains("HMI_TAMPER_DETECTION=0", artifact!.Content);
    }

    // ── 辅助 ──

    private static HmiProject CreateProject()
    {
        var project = new HmiProject { Name = "Test", LvglVersion = "9.2" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });
        return project;
    }
}
