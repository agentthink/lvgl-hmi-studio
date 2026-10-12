using System;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **面板的"文本样式"三行只对文本类控件显示**（阶段 220，登记 `#96` 的"**统一可见性**"那半）。
///
/// <para><b>此前的问题</b>：`PropFontFamily` / `PropFontSize` / `PropTextAlign` 三行
/// **对所有控件类型都显示**（`PropertyPanelView.Parameters.cs` 的类型分发表里根本没有它们）
/// ⇒ 在形状/Led/Slider 这类**没有文本**的控件上改它们，既不进导出、预览也看不出效果，
/// 用户会以为"设置丢了"。</para>
///
/// <para>这一面把名单钉住：**可见** ＝ Label / Button / CheckBox / TextArea / Dropdown
/// （与 `CodeGenerator.Screen.cs` 的文本块 ＋ `EmitDropdownParameters` 一致）；其余类型**隐藏**。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class PropertyPanelTextRowsVisibilityTests
{
    private static readonly string[] Rows = { "PropFontFamily", "PropFontSize", "PropTextAlign" };
    private static readonly string[] Labels = { "LblFontFamily", "LblFontSize", "LblTextAlign" };

    private static void AssertRows(PropertyPanelView panel, bool expected, string because)
    {
        foreach (var name in Rows)
        {
            var control = ControlWiringFace.Find<Control>(panel, name);
            Assert.True(control.IsVisible == expected,
                $"{name} 的可见性应为 {expected}（{because}），实际 {control.IsVisible}");
        }

        foreach (var name in Labels)
        {
            var label = ControlWiringFace.Find<Control>(panel, name);
            Assert.True(label.IsVisible == expected,
                $"{name} 的可见性应与输入控件一致（{because}），实际 {label.IsVisible}");
        }
    }

    [Fact]
    public void TextRows_AreVisibleForTextBearingTypes_AndHiddenForTheRest()
    {
        using var sandbox = new DataRootSandbox("panel-text-rows-visibility");

        UiTestHost.Run(() =>
        {
            var project = new HmiProject { Name = "P" };
            var screen = new HmiScreen { Id = "s1", Name = "Main" };
            project.Screens.Add(screen);

            var panel = new PropertyPanelView { CurrentScreen = screen };
            var window = new Window { Width = 1000, Height = 900, Content = panel };
            window.Show();
            UiTestHost.Pump(5);
            panel.SetProject(project);
            UiTestHost.Pump(2);

            try
            {
                // 可见：文本类控件（名单与生成器的文本块一致）
                foreach (var type in new[]
                         {
                             HmiControlType.Label, HmiControlType.Button, HmiControlType.CheckBox,
                             HmiControlType.TextArea, HmiControlType.Dropdown,
                         })
                {
                    var control = new HmiControl
                    {
                        Id = "c_" + type, Name = "n_" + type, Type = type,
                        X = 10, Y = 10, Width = 120, Height = 40, Text = "温度",
                    };
                    screen.Controls.Add(control);

                    panel.BindControl(control);
                    UiTestHost.Pump(3);

                    AssertRows(panel, true, type.ToString());
                }

                // 隐藏：没有文本的控件
                foreach (var type in new[]
                         {
                             HmiControlType.Rectangle, HmiControlType.Slider, HmiControlType.Led,
                             HmiControlType.Image, HmiControlType.Gauge,
                         })
                {
                    var control = new HmiControl
                    {
                        Id = "s_" + type, Name = "s_" + type, Type = type,
                        X = 10, Y = 10, Width = 120, Height = 40,
                    };
                    screen.Controls.Add(control);

                    panel.BindControl(control);
                    UiTestHost.Pump(3);

                    AssertRows(panel, false, type.ToString());
                }
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });
    }
}
