using System.IO.Compression;
using System.Text;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Serialization;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// .hmi 打包/解包端到端验证 —— 构造包含图片与脚本的完整测试工程，
/// 打包到磁盘 → 校验 ZIP 内部结构 → 解包 → 断言各数据域完整。
/// </summary>
public class HmiPackageEndToEndTests
{
    /// <summary>最小 1x1 透明 PNG（真实有效字节）。</summary>
    private static readonly byte[] TinyPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x62, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

    private const string LuaScriptCode = """
        -- 温度超限报警脚本
        local t = hmi_read("temperature_1")
        if t > 80 then
            hmi_set_text("led_alarm", "高温")
            hmi_set_color("led_alarm", "#FF0000")
        end
        """;

    [Fact]
    public void EndToEnd_RoundTrip_PreservesAllDomains()
    {
        var tempRoot = TestTempDir.NewDir("hmi-e2e");
        try
        {
            // ── 1. 构造测试工程 ──
            var project = new HmiProject
            {
                Name = "TestEngineering",
                ProjectVersion = "2.0.0",
                LvglVersion = "9.5",
                HardwareTargetId = "t113"
            };

            // 屏幕 1：主界面（带绑定按钮）
            var main = new HmiScreen { Name = "Main", Width = 800, Height = 480, BgColor = "#1a1a2e" };
            main.Controls.Add(new HmiControl
            {
                Name = "启动",
                Type = HmiControlType.Button,
                X = 20, Y = 20, Width = 120, Height = 48,
                Text = "启动电机",
                BgColor = "#2e8b57",
                VariableBindingId = "motor_run"
            });
            main.Controls.Add(new HmiControl
            {
                Name = "温度显示",
                Type = HmiControlType.Label,
                X = 20, Y = 100, Width = 160, Height = 32,
                Text = "温度: --",
                VariableBindingId = "temperature_1"
            });
            project.Screens.Add(main);

            // 屏幕 2：报警界面（带 LED 控件）
            var alarmScreen = new HmiScreen { Name = "Alarm", Width = 1024, Height = 600, BgColor = "#101418" };
            alarmScreen.Controls.Add(new HmiControl
            {
                Name = "led_alarm",
                Type = HmiControlType.Led,
                X = 400, Y = 250, Width = 48, Height = 48,
                IndicatorColor = "#00FF00",
                VariableBindingId = "temperature_1"
            });
            project.Screens.Add(alarmScreen);

            // 变量
            project.VariableManager.Variables.Add(new Variable
            {
                Name = "temperature_1", DataType = "Float", Address = "40001",
                ConnectionId = "conn1", Unit = "°C", PollRateMs = 500
            });
            project.VariableManager.Variables.Add(new Variable
            {
                Name = "motor_run", DataType = "Bool", Address = "00001", ConnectionId = "conn1"
            });
            project.VariableManager.Connections.Add(new DeviceConnection
            {
                Name = "conn1", DriverId = "modbus-tcp",
                Config = new() { ["host"] = "192.168.1.10", ["port"] = "502" }
            });

            // Lua 脚本
            project.Scripts.Add(new HmiScript
            {
                Name = "温度报警",
                Code = LuaScriptCode,
                Category = "OnTimer",
                Description = "温度超过 80℃ 时点亮报警 LED",
                IsEnabled = true
            });

            // 报警配置
            var alarmGroup = new AlarmGroup { Name = "温度组" };
            alarmGroup.Alarms.Add(new AlarmConfig
            {
                Name = "高温报警",
                Severity = AlarmSeverity.Critical,
                GroupId = alarmGroup.Id,
                Condition = new AlarmCondition { VariableId = "temperature_1", Type = AlarmConditionType.HighLimit, Threshold = 80 }
            });
            project.AlarmGroups.Add(alarmGroup);

            // 资产：真实 PNG 图片
            var pngPath = Path.Combine(tempRoot, "icon.png");
            File.WriteAllBytes(pngPath, TinyPng);
            var assetLib = new AssetLibrary();
            assetLib.AddAsset(new AssetItem
            {
                Id = "img001",
                Name = "icon",
                Type = AssetType.Image,
                FilePath = pngPath,
                SizeBytes = TinyPng.Length,
                Width = 1,
                Height = 1,
                ColorDepth = 32
            });

            // 图库：SVG 资源
            var reslib = new HmiResourceLibrary();
            reslib.CustomResources.Add(new HmiResourceItem
            {
                Name = "pump_icon",
                DisplayName = "水泵",
                SvgGeometry = "<svg viewBox='0 0 24 24'><path d='M0 0h24v24H0z'/></svg>",
                IsCustom = true
            });

            // ── 2. 打包到磁盘 .hmi ──
            var hmiPath = Path.Combine(tempRoot, "TestEngineering.hmi");
            using (var fs = File.Create(hmiPath))
            {
                HmiPackageService.Pack(fs, new HmiPackageContent(
                    project, assetLib, reslib,
                    item => File.Exists(item.FilePath) ? item.FilePath : null));
            }

            Assert.True(File.Exists(hmiPath));
            Assert.True(new FileInfo(hmiPath).Length > TinyPng.Length, "包应包含压缩后的工程数据与资产");

            // ── 3. 校验 ZIP 内部结构 ──
            using (var zip = ZipFile.OpenRead(hmiPath))
            {
                var names = zip.Entries.Select(e => e.FullName).ToHashSet();
                Assert.Contains("manifest.json", names);
                Assert.Contains("project.json", names);
                Assert.Contains("assets.json", names);
                Assert.Contains("reslib.json", names);
                Assert.Contains(names, n => n.StartsWith("assets/img001.", StringComparison.Ordinal));
            }

            // ── 4. 解包到独立目录 ──
            var unpackDir = Path.Combine(tempRoot, "unpacked");
            HmiPackageResult result;
            using (var fs = File.OpenRead(hmiPath))
            {
                result = HmiPackageService.Unpack(fs, unpackDir);
            }

            // ── 5. 断言各数据域完整 ──
            // 工程元数据 + 屏幕
            Assert.Equal("TestEngineering", result.Project.Name);
            Assert.Equal("2.0.0", result.Project.ProjectVersion);
            Assert.Equal("t113", result.Project.HardwareTargetId);
            Assert.Equal(2, result.Project.Screens.Count);
            var mainUnpacked = result.Project.Screens[0];
            Assert.Equal("Main", mainUnpacked.Name);
            Assert.Equal(800, mainUnpacked.Width);
            Assert.Equal(2, mainUnpacked.Controls.Count);
            Assert.Equal("启动电机", mainUnpacked.Controls[0].Text);
            Assert.Equal("motor_run", mainUnpacked.Controls[0].VariableBindingId);
            Assert.Equal("temperature_1", mainUnpacked.Controls[1].VariableBindingId);

            // 变量 + 连接
            Assert.Equal(2, result.Project.VariableManager.Variables.Count);
            Assert.Equal("192.168.1.10", result.Project.VariableManager.Connections[0].Config["host"]);

            // 脚本代码完整
            var script = Assert.Single(result.Project.Scripts);
            Assert.Equal("温度报警", script.Name);
            Assert.Equal(LuaScriptCode, script.Code);

            // 报警
            var alarm = Assert.Single(result.Project.AlarmGroups).Alarms[0];
            Assert.Equal(80, alarm.Condition.Threshold);

            // 图片资产：字节一致 + 落盘
            var asset = Assert.Single(result.AssetLibrary.Assets);
            Assert.Equal("img001", asset.Id);
            Assert.True(File.Exists(asset.FilePath), "图片应解包落盘");
            Assert.Equal(TinyPng, File.ReadAllBytes(asset.FilePath));

            // 图库
            var resItem = Assert.Single(result.ResourceLibrary!.CustomResources);
            Assert.Equal("pump_icon", resItem.Name);
        }
        finally
        {
            TestTempDir.DeleteDir(tempRoot);
        }
    }
}
