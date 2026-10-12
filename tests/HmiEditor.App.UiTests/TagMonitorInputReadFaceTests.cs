using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`TagMonitorView.TxtWriteValue` 的读点行为面**（阶段 210）—— 「读点在动作处理器」这一族的
/// **最后一个**视图。
///
/// <para><b>🩸 为什么它值得单独一笔记</b>：这张台账里它原先被**登记成"不可驱动"**，理由是
/// "要驱动得先有模拟器/设备侧的注入缝" —— 这个理由是**错的**：`RuntimeBindingEngine` 本来就吃
/// <see cref="IRuntimeValueSource"/>（`TryRead` / `TryWrite` / `IsConnected`），而视图的构造又
/// **本来就接收引擎**（`TagMonitorView(RuntimeBindingEngine, …)`）⇒ 一个假值源就够了，
/// **不需要任何产品缝**。⇒ 教训：**登记的理由必须验一遍**（台账里那条"抽查读点确实在动作处理器里"
/// 只查了形态，查不出理由本身是否成立）。</para>
///
/// <para><b>读点</b>：`TxtWriteValue`（工程值文本）在 `TryWriteValue` 里被 <c>double.TryParse</c>
/// 解析后交给引擎 → `IRuntimeValueSource.TryWrite`。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class TagMonitorInputReadFaceTests
{
    /// <summary>假值源：记下每一次写入。</summary>
    private sealed class FakeValueSource : IRuntimeValueSource
    {
        public Dictionary<string, double> Written { get; } = new(StringComparer.Ordinal);
        public bool Connected { get; set; } = true;

        public bool IsConnected => Connected;
        public string Name => "FakeValueSource";

        public bool TryRead(string variableId, out double value)
        {
            value = 0;
            return false;
        }

        public bool TryWrite(string variableId, double value)
        {
            Written[variableId] = value;
            return true;
        }
    }

    /// <summary>一个可写变量 ＋ 一个 `ReadWrite` 绑定的控件（`BuildRuntimeTags` 会产出可写标签）。</summary>
    private static HmiProject CreateProject()
    {
        var project = new HmiProject { Name = "TagMonitorFixture" };

        project.VariableManager.Connections.Add(new DeviceConnection
        {
            Id = "conn1",
            Name = "PLC1",
            DriverId = "modbus-tcp",
        });

        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v_temp",
            Name = "temperature",
            Address = "40001",
            DataType = "Float",
            ConnectionId = "conn1",
        });

        var screen = new HmiScreen { Id = "s1", Name = "Main" };
        screen.Controls.Add(new HmiControl
        {
            Id = "c1",
            Name = "TempBox",
            Type = HmiControlType.TextArea,
            VariableBindingId = "v_temp",
            BindingMode = "ReadWrite",
        });
        project.Screens.Add(screen);

        return project;
    }

    private static void Click(Control root, string name)
    {
        ControlWiringFace.Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump(2);
    }

    [Fact]
    public void WriteValue_ReachesTheValueSource()
    {
        using var sandbox = new DataRootSandbox("tagmonitor-write-value");

        UiTestHost.Run(() =>
        {
            var project = CreateProject();
            var engine = new RuntimeBindingEngine(project);
            engine.BuildRuntimeTags();
            var source = new FakeValueSource();
            engine.ValueSource = source;

            var view = new TagMonitorView(engine);
            view.Show();
            UiTestHost.Pump(5);
            // 走生产序列：宿主（`Wiring.Panels.cs`）建完视图就会调一次 `RefreshRows()`
            // —— 写入目标下拉是在那里被填上的（构造里不填）。
            view.RefreshRows();
            UiTestHost.Pump(2);
            try
            {
                // 前提自检：写入目标下拉被填上了（≥1 个可写标签、默认选中第一个）
                var target = ControlWiringFace.Find<ComboBox>(view, "CmbWriteTarget");
                Assert.True(target.ItemCount >= 1, $"写入目标下拉是空的（夹具里没有可写标签）：{target.ItemCount} 项");
                Assert.NotNull(target.SelectedItem);

                // ① 正常值 ⇒ 值源收到（工程值原文，夹具的 Scale/Offset 都是默认 1/0）
                ControlWiringFace.Write(view, "TxtWriteValue", "12.5");
                Click(view, "BtnWrite");
                Assert.True(source.Written.TryGetValue("v_temp", out var written),
                    $"值源没收到写入；摘要：{ControlWiringFace.Find<TextBlock>(view, "SummaryText").Text}");
                Assert.Equal(12.5, written);

                // ② 非法文本 ⇒ **不写**（`double.TryParse` 失败就早退）
                ControlWiringFace.Write(view, "TxtWriteValue", "abc");
                Click(view, "BtnWrite");
                Assert.Equal(12.5, source.Written["v_temp"]);   // 还是上一次那个值

                // ③ 空 ⇒ 同样不写
                ControlWiringFace.Write(view, "TxtWriteValue", "");
                Click(view, "BtnWrite");
                Assert.Equal(12.5, source.Written["v_temp"]);

                // ④ 有值但源未连接 ⇒ 引擎拒绝（不是本视图的锅，但口径要钉住：不静默"成功"）
                source.Connected = false;
                ControlWiringFace.Write(view, "TxtWriteValue", "20");
                Click(view, "BtnWrite");
                Assert.Equal(12.5, source.Written["v_temp"]);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }
}
