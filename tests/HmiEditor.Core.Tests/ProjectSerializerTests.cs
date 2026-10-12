using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Animation;
using HmiEditor.Core.Serialization;
using Xunit;

namespace HmiEditor.Core.Tests;

public class ProjectSerializerTests
{
    [Fact]
    public void Serialize_RoundTrip_ProducesEquivalentProject()
    {
        var project = new HmiProject
        {
            Name = "TestProject",
            LvglVersion = "9.2",
            HardwareTargetId = "allwinner-t113"
        };
        var screen = new HmiScreen
        {
            Name = "Main Screen",
            Width = 800,
            Height = 480,
            BgColor = "#131c31"
        };
        screen.Controls.Add(new HmiControl
        {
            Name = "TestButton",
            Type = HmiControlType.Button,
            X = 100,
            Y = 200,
            Width = 120,
            Height = 44,
            Text = "Click Me",
            BgColor = "#38bdf8"
        });
        project.Screens.Add(screen);

        // 添加变量/通讯模型
        project.VariableManager.Connections.Add(new DeviceConnection
        {
            Name = "PLC1",
            DriverId = "modbus-rtu",
            Config = new Dictionary<string, string> { ["port"] = "COM1", ["baud"] = "9600" }
        });
        project.VariableManager.Variables.Add(new Variable
        {
            Name = "Temperature",
            Address = "40001",
            DataType = "Int16",
            ConnectionId = project.VariableManager.Connections[0].Id
        });

        var json = ProjectSerializer.Serialize(project);
        Assert.False(string.IsNullOrEmpty(json));

        var deserialized = ProjectSerializer.Deserialize(json);
        Assert.NotNull(deserialized);
        Assert.Equal(project.Name, deserialized!.Name);
        Assert.Equal(project.LvglVersion, deserialized.LvglVersion);
        Assert.Single(deserialized.Screens);
        Assert.Single(deserialized.Screens[0].Controls);
        Assert.Equal("TestButton", deserialized.Screens[0].Controls[0].Name);
        Assert.Equal(HmiControlType.Button, deserialized.Screens[0].Controls[0].Type);

        // 验证变量模型
        Assert.Single(deserialized.VariableManager.Connections);
        Assert.Single(deserialized.VariableManager.Variables);
        Assert.Equal("Temperature", deserialized.VariableManager.Variables[0].Name);
    }

    [Fact]
    public void Serialize_EmptyProject_ReturnsValidJson()
    {
        var project = new HmiProject();
        var json = ProjectSerializer.Serialize(project);
        Assert.False(string.IsNullOrEmpty(json));
        Assert.Contains("untitled", json.ToLower());
    }

    [Fact]
    public void Deserialize_NullJson_ReturnsNull()
    {
        var result = ProjectSerializer.Deserialize(null!);
        Assert.Null(result);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsNull()
    {
        var result = ProjectSerializer.Deserialize("{invalid json}");
        Assert.Null(result);
    }

    [Fact]
    public void Deserialize_MinimalJson_ReturnsProject()
    {
        var json = @"{""name"":""Minimal"",""lvglVersion"":""9.2"",""screens"":[]}";
        var project = ProjectSerializer.Deserialize(json);
        Assert.NotNull(project);
        Assert.Equal("Minimal", project!.Name);
    }

    [Fact]
    public void RoundTrip_WithVariableBinding_PreservesBinding()
    {
        var project = new HmiProject { Name = "BindingTest" };
        var screen = new HmiScreen { Name = "Screen1", Width = 480, Height = 272 };
        var ctrl = new HmiControl
        {
            Name = "TempLabel",
            Type = HmiControlType.Label,
            X = 10, Y = 10, Width = 200, Height = 30
        };
        screen.Controls.Add(ctrl);
        project.Screens.Add(screen);

        project.VariableManager.Variables.Add(new Variable
        {
            Name = "Temp",
            Address = "40001",
            DataType = "Float32"
        });

        var json = ProjectSerializer.Serialize(project);
        var restored = ProjectSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Single(restored!.Screens);
        Assert.Single(restored.Screens[0].Controls);
        Assert.Single(restored.VariableManager.Variables);
        Assert.Equal("Temp", restored.VariableManager.Variables[0].Name);
    }

    [Fact]
    public void RoundTrip_WithAnimations_PreservesAnimationConfigs()
    {
        var project = new HmiProject { Name = "AnimTest" };
        project.Screens.Add(new HmiScreen { Name = "Main", Width = 800, Height = 480 });

        var anim = new AnimationConfig
        {
            Name = "Fade In",
            ControlId = "btn01",
            PropertyName = "Opacity",
            StartValue = 0,
            EndValue = 255,
            DurationMs = 500,
            DelayMs = 100,
            PathType = AnimationPathType.EaseInOut,
            RepeatCount = -1,
            IsEnabled = true,
            Trigger = "OnLoad",
            Description = "Button fade-in effect"
        };
        anim.Keyframes.Add(new AnimationKeyframe
        {
            TimePercent = 0,
            Value = 0,
            PathType = AnimationPathType.Linear
        });
        anim.Keyframes.Add(new AnimationKeyframe
        {
            TimePercent = 100,
            Value = 255,
            PathType = null
        });
        project.AnimationConfigs.Add(anim);

        var json = ProjectSerializer.Serialize(project);
        var restored = ProjectSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Single(restored!.AnimationConfigs);

        var restoredAnim = restored.AnimationConfigs[0];
        Assert.Equal("Fade In", restoredAnim.Name);
        Assert.Equal("btn01", restoredAnim.ControlId);
        Assert.Equal("Opacity", restoredAnim.PropertyName);
        Assert.Equal(0, restoredAnim.StartValue);
        Assert.Equal(255, restoredAnim.EndValue);
        Assert.Equal(500, restoredAnim.DurationMs);
        Assert.Equal(100, restoredAnim.DelayMs);
        Assert.Equal(AnimationPathType.EaseInOut, restoredAnim.PathType);
        Assert.Equal(-1, restoredAnim.RepeatCount);
        Assert.True(restoredAnim.IsEnabled);
        Assert.Equal("OnLoad", restoredAnim.Trigger);
        Assert.Equal("Button fade-in effect", restoredAnim.Description);

        Assert.Equal(2, restoredAnim.Keyframes.Count);
        Assert.Equal(0, restoredAnim.Keyframes[0].TimePercent);
        Assert.Equal(0, restoredAnim.Keyframes[0].Value);
        Assert.Equal(AnimationPathType.Linear, restoredAnim.Keyframes[0].PathType);
        Assert.Equal(100, restoredAnim.Keyframes[1].TimePercent);
        Assert.Null(restoredAnim.Keyframes[1].PathType);
    }

    [Fact]
    public void RoundTrip_MultipleAnimations_PreservesAll()
    {
        var project = new HmiProject { Name = "MultiAnim" };

        for (int i = 0; i < 3; i++)
        {
            project.AnimationConfigs.Add(new AnimationConfig
            {
                Name = $"Anim{i}",
                PropertyName = "Opacity",
                IsEnabled = i % 2 == 0,
                Trigger = i == 0 ? "OnLoad" : "OnClick"
            });
        }

        var json = ProjectSerializer.Serialize(project);
        var restored = ProjectSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Equal(3, restored!.AnimationConfigs.Count);
        Assert.Equal("Anim0", restored.AnimationConfigs[0].Name);
        Assert.True(restored.AnimationConfigs[0].IsEnabled);
        Assert.False(restored.AnimationConfigs[1].IsEnabled);
        Assert.Equal("OnClick", restored.AnimationConfigs[2].Trigger);
    }

    [Fact]
    public void Serialize_NewProjectSettings_IncludedInJson()
    {
        var project = new HmiProject
        {
            Name = "SettingsTest",
            Description = "测试描述",
            Author = "张三",
            Company = "ACME",
            ProjectVersion = "2.1.0"
        };

        var json = ProjectSerializer.Serialize(project);

        // 验证新子模型属性都出现在 JSON 中
        Assert.Contains("runtimeConfig", json);
        Assert.Contains("scanIntervalMs", json);
        Assert.Contains("logLevel", json);
        Assert.Contains("exportConfig", json);
        Assert.Contains("targetLanguage", json);
        Assert.Contains("securityConfig", json);
        Assert.Contains("operationMode", json);
        Assert.Contains("editorConfig", json);
        Assert.Contains("gridSize", json);
        Assert.Contains("screenDefaultsConfig", json);
        Assert.Contains("defaultWidth", json);
        Assert.Contains("touchCalibration", json);

        // 验证元数据属性
        Assert.Contains("description", json);
        Assert.Contains("测试描述", json);
        Assert.Contains("author", json);
        Assert.Contains("张三", json);
        Assert.Contains("projectVersion", json);
        Assert.Contains("2.1.0", json);

        // 验证枚举序列化为 camelCase 字符串
        Assert.Contains("\"warning\"", json);    // LogLevel.Warning
        Assert.Contains("\"designer\"", json);   // OperationMode.Designer
        Assert.Contains("\"landscape\"", json);   // ScreenOrientation.Landscape
        Assert.Contains("\"autoReconnect\"", json); // ConnectionRecoveryMode.AutoReconnect
    }

    [Fact]
    public void RoundTrip_NewProjectSettings_PreservesAllValues()
    {
        var project = new HmiProject
        {
            Name = "RoundTripTest",
            Description = "往返测试",
            Author = "李四",
            Company = "TestCorp",
            ProjectVersion = "3.0.0"
        };

        // 修改各子模型的非默认值
        project.RuntimeConfig.ScanIntervalMs = 200;
        project.RuntimeConfig.LogLevel = LogLevel.Debug;
        project.RuntimeConfig.EnableCrashDump = false;
        project.RuntimeConfig.BootScreenId = "screen01";

        project.ExportConfig.TargetLanguage = TargetLanguage.Cpp;
        project.ExportConfig.OutputDirectory = "/opt/output";
        project.ExportConfig.GenerateDebugSymbols = true;
        project.ExportConfig.CodeGenOptimization = CodeGenOptimization.Speed;

        project.SecurityConfig.OperationMode = OperationMode.Maintenance;
        project.SecurityConfig.SessionTimeoutMin = 60;
        project.SecurityConfig.EnableAuditLog = false;

        project.EditorConfig.GridSize = 4;
        project.EditorConfig.SnapToGrid = false;
        project.EditorConfig.PreferredLanguage = "en-US";

        project.ScreenDefaultsConfig.DefaultWidth = 1024;
        project.ScreenDefaultsConfig.DefaultHeight = 600;
        project.ScreenDefaultsConfig.DefaultColorDepth = "ARGB8888";
        project.ScreenDefaultsConfig.DefaultOrientation = ScreenOrientation.Portrait;
        project.ScreenDefaultsConfig.TouchCalibration.M11 = 0.98;
        project.ScreenDefaultsConfig.TouchCalibration.M22 = 0.97;

        project.VariableManager.DefaultScanIntervalMs = 250;
        project.VariableManager.DefaultTimeoutMs = 1000;
        project.VariableManager.ConnectionRecoveryMode = ConnectionRecoveryMode.ManualReconnect;

        var json = ProjectSerializer.Serialize(project);
        var restored = ProjectSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Equal("RoundTripTest", restored!.Name);
        Assert.Equal("往返测试", restored.Description);
        Assert.Equal("李四", restored.Author);
        Assert.Equal("TestCorp", restored.Company);
        Assert.Equal("3.0.0", restored.ProjectVersion);

        // RuntimeConfig
        Assert.Equal(200, restored.RuntimeConfig.ScanIntervalMs);
        Assert.Equal(LogLevel.Debug, restored.RuntimeConfig.LogLevel);
        Assert.False(restored.RuntimeConfig.EnableCrashDump);
        Assert.Equal("screen01", restored.RuntimeConfig.BootScreenId);

        // ExportConfig
        Assert.Equal(TargetLanguage.Cpp, restored.ExportConfig.TargetLanguage);
        Assert.Equal("/opt/output", restored.ExportConfig.OutputDirectory);
        Assert.True(restored.ExportConfig.GenerateDebugSymbols);
        Assert.Equal(CodeGenOptimization.Speed, restored.ExportConfig.CodeGenOptimization);

        // SecurityConfig
        Assert.Equal(OperationMode.Maintenance, restored.SecurityConfig.OperationMode);
        Assert.Equal(60, restored.SecurityConfig.SessionTimeoutMin);
        Assert.False(restored.SecurityConfig.EnableAuditLog);

        // EditorConfig
        Assert.Equal(4, restored.EditorConfig.GridSize);
        Assert.False(restored.EditorConfig.SnapToGrid);
        Assert.Equal("en-US", restored.EditorConfig.PreferredLanguage);

        // ScreenDefaultsConfig
        Assert.Equal(1024, restored.ScreenDefaultsConfig.DefaultWidth);
        Assert.Equal(600, restored.ScreenDefaultsConfig.DefaultHeight);
        Assert.Equal("ARGB8888", restored.ScreenDefaultsConfig.DefaultColorDepth);
        Assert.Equal(ScreenOrientation.Portrait, restored.ScreenDefaultsConfig.DefaultOrientation);
        Assert.Equal(0.98, restored.ScreenDefaultsConfig.TouchCalibration.M11);
        Assert.Equal(0.97, restored.ScreenDefaultsConfig.TouchCalibration.M22);

        // VariableManager 扩展
        Assert.Equal(250, restored.VariableManager.DefaultScanIntervalMs);
        Assert.Equal(1000, restored.VariableManager.DefaultTimeoutMs);
        Assert.Equal(ConnectionRecoveryMode.ManualReconnect, restored.VariableManager.ConnectionRecoveryMode);
    }

    [Fact]
    public void Deserialize_OldJson_WithMissingNewFields_UsesDefaults()
    {
        // 模拟旧版 JSON —— 缺少所有新字段
        var legacyJson = """
{
  "name": "OldProject",
  "lvglVersion": "8.3",
  "screens": [],
  "theme": {},
  "variableManager": {
    "connections": [],
    "variables": [],
    "groups": []
  }
}
""";
        var project = ProjectSerializer.Deserialize(legacyJson);

        Assert.NotNull(project);
        Assert.Equal("OldProject", project!.Name);

        // 新子模型应使用默认值（new() 初始化器）
        Assert.NotNull(project.RuntimeConfig);
        Assert.Equal(100, project.RuntimeConfig.ScanIntervalMs);
        Assert.Equal(LogLevel.Warning, project.RuntimeConfig.LogLevel);

        Assert.NotNull(project.ExportConfig);
        Assert.Equal(TargetLanguage.C, project.ExportConfig.TargetLanguage);

        Assert.NotNull(project.SecurityConfig);
        Assert.Equal(OperationMode.Designer, project.SecurityConfig.OperationMode);

        Assert.NotNull(project.EditorConfig);
        Assert.Equal(8, project.EditorConfig.GridSize);

        Assert.NotNull(project.ScreenDefaultsConfig);
        // 默认屏宽对齐固件型号（T113 7寸 1024×600），见 ScreenDefaultsConfig.DefaultWidth 默认值
        Assert.Equal(1024, project.ScreenDefaultsConfig.DefaultWidth);
        Assert.Null(project.ScreenDefaultsConfig.SafeAreaPadding);

        // 元数据字段为 null（旧 JSON 不存在）
        Assert.Null(project.Description);
        Assert.Null(project.Author);
        Assert.Null(project.Company);
    }

    [Fact]
    public void SafeAreaPadding_Null_SkippedInJson()
    {
        var project = new HmiProject();

        // SafeAreaPadding 默认为 null，应被跳过
        var json = ProjectSerializer.Serialize(project);
        Assert.DoesNotContain("safeAreaPadding", json);

        // 设置非空后应被包含
        project.ScreenDefaultsConfig.SafeAreaPadding = new ScreenPadding(24);
        json = ProjectSerializer.Serialize(project);
        Assert.Contains("safeAreaPadding", json);
        // System.Text.Json camelCase: Left→left, also IsZero is serialized as isZero
        Assert.Contains("left", json);
        Assert.Contains("24", json);
    }
}
