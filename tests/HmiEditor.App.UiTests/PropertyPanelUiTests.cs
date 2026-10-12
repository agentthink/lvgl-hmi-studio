using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using FluentAvalonia.UI.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// <see cref="PropertyPanelView"/> 的真实 UI 行为测试。
///
/// <para>这个面板是"改属性 → 写回模型 → 画布重绘/入撤销栈"的枢纽，但它的逻辑全在
/// 事件处理器里（<c>TextChanged</c> / <c>ValueChanged</c> → <c>SaveProperties</c>），
/// 过去没有任何测试能覆盖到 —— 只有把真实控件建起来、真的改一次值，
/// 才能验证"事件接到了、值写回了、通知发出去了"。</para>
///
/// <para>换成 headless 之前，这类回归（例如构造函数里少订阅一个 <c>ValueChanged</c>，
/// 导致改数字不落盘）只能靠人肉点出来。</para>
/// </summary>
public class PropertyPanelUiTests
{
    private sealed record Fixture(Window Window, PropertyPanelView Panel, HmiControl Control);

    /// <summary>把面板挂进真实窗口（<c>OnPropertyChanged</c> 要求 Parent 非空，用于防御 Dock 重组的延迟事件）。</summary>
    private static Fixture Host(HmiControl? control)
    {
        var panel = new PropertyPanelView();
        var window = new Window { Width = 900, Height = 700, Content = panel };
        window.Show();
        UiTestHost.Pump();

        if (control is not null)
            panel.BindControl(control);

        UiTestHost.Pump();
        return new Fixture(window, panel, control!);
    }

    private static HmiControl MakeControl() => new()
    {
        Id = "c1",
        Type = HmiControlType.Label,
        Name = "lbl1",
        Text = "你好",
        X = 10,
        Y = 20,
        Width = 120,
        Height = 30,
    };

    private static T Find<T>(PropertyPanelView panel, string name) where T : Control
    {
        var found = panel.FindControl<T>(name);
        Assert.NotNull(found);
        return found!;
    }

    [Fact]
    public void BindControl_PopulatesEditorsFromModel()
    {
        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            var f = Host(control);

            Assert.Equal(10d, System.Convert.ToDouble(Find<FANumberBox>(f.Panel, "PropX").Value));
            Assert.Equal(20d, System.Convert.ToDouble(Find<FANumberBox>(f.Panel, "PropY").Value));
            Assert.Equal(120d, System.Convert.ToDouble(Find<FANumberBox>(f.Panel, "PropWidth").Value));
            Assert.Equal(30d, System.Convert.ToDouble(Find<FANumberBox>(f.Panel, "PropHeight").Value));
            Assert.Equal("lbl1", Find<TextBox>(f.Panel, "PropName").Text);
            Assert.Equal("你好", Find<TextBox>(f.Panel, "PropText").Text);

            // 标题里带名字与类型，方便确认"当前编辑的是谁"
            // ⚠ 类型**不再**是英文枚举名（阶段 160 起走 `ControlTypeTextKeys`）——
            //   本类**不切语言**（没挂 Language-global 独占集合），所以这里断言"标题 = 名字 + 本语言的类型显示名"
            //   而不是某个语言的字面量；映射本身（26 个成员都有键、zh≠en）由
            //   `ControlTypeDisplayTests` 与 `ReferenceFieldLocalizationGuardTests` 判。
            var info = Find<TextBlock>(f.Panel, "InfoText").Text;
            Assert.NotNull(info);
            Assert.Contains("lbl1", info!);
            Assert.Contains(Services.ControlTypeTextKeys.Describe(control.Type), info!);

            f.Window.Close();
        });
    }

    /// <summary>
    /// 用**真实键盘输入**替换名称输入框内容（Avalonia 12 里程序化赋值 <c>TextBox.Text</c>
    /// 不会触发 <c>TextChanged</c>，只有用户输入才会 —— 这也让测试更接近真实路径）。
    /// </summary>
    private static void TypeInto(Window window, TextBox box, string text)
    {
        box.Focus();
        UiTestHost.Pump();
        box.SelectAll();
        window.KeyTextInput(text);
        UiTestHost.Pump();
    }

    [Fact]
    public void EditingNameTextBox_WritesBackToModel_AndRaisesPropertyChanged()
    {
        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            var f = Host(control);

            HmiControl? notified = null;
            f.Panel.PropertyChanged += (_, c) => notified = c;

            TypeInto(f.Window, Find<TextBox>(f.Panel, "PropName"), "renamed_label");

            Assert.Equal("renamed_label", control.Name);
            Assert.Same(control, notified);
            f.Window.Close();
        });
    }

    [Fact]
    public void TypedName_IsTrimmedBySanitizer()
    {
        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            var f = Host(control);

            TypeInto(f.Window, Find<TextBox>(f.Panel, "PropName"), "  spaced name  ");

            Assert.Equal("spaced name", control.Name);
            f.Window.Close();
        });
    }

    [Fact]
    public void EditingPositionNumberBox_WritesBackToModel_AndRaisesPropertyChanged()
    {
        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            var f = Host(control);

            HmiControl? notified = null;
            f.Panel.PropertyChanged += (_, c) => notified = c;

            // 构造函数里手工订阅了 FANumberBox.ValueChanged（XAML 编译器接不了
            // TypedEventHandler），这里正是对那条订阅的回归保护。
            Find<FANumberBox>(f.Panel, "PropX").Value = 42;
            UiTestHost.Pump();

            Assert.Equal(42, control.X);
            Assert.Same(control, notified);
            f.Window.Close();
        });
    }

    [Fact]
    public void Rebind_DoesNotWritePreviousControlWhenValuesAreRefreshed()
    {
        UiTestHost.Run(() =>
        {
            var first = MakeControl();
            var second = MakeControl();
            second.Id = "c2";
            second.Name = "lbl2";
            second.X = 300;

            var f = Host(first);
            f.Panel.BindControl(second); // 刷新编辑器控件时会触发 TextChanged/ValueChanged
            UiTestHost.Pump();

            // _isUpdating 重入保护：第一个控件不能被"绑定第二个控件"这件事改写。
            Assert.Equal(10, first.X);
            Assert.Equal("lbl1", first.Name);
            Assert.Equal(300, second.X);
            f.Window.Close();
        });
    }

    /// <summary>拒绝一切写入的校验器，用于验证 P1-3「校验不过就保留原值」。</summary>
    private sealed class RejectAllValidator : IPropertyValidator
    {
        public List<string> Seen { get; } = new();

        public string? Validate(HmiControl control, string propertyName, object? value)
        {
            Seen.Add(propertyName);
            return $"{propertyName} 被拒绝";
        }
    }

    [Fact]
    public void RejectedEdit_KeepsModelValue_AndValidatorWasConsulted()
    {
        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            var f = Host(control);

            var validator = new RejectAllValidator();
            f.Panel.CustomValidator = validator;

            TypeInto(f.Window, Find<TextBox>(f.Panel, "PropName"), "should_not_be_written");

            Assert.Equal("lbl1", control.Name);
            Assert.Contains(nameof(HmiControl.Name), validator.Seen);
            f.Window.Close();
        });
    }

    [Fact]
    public void RejectedNumericEdit_KeepsModelValue()
    {
        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            var f = Host(control);

            var validator = new RejectAllValidator();
            f.Panel.CustomValidator = validator;

            Find<FANumberBox>(f.Panel, "PropX").Value = 999;
            UiTestHost.Pump();

            // 位置/尺寸与文本属性一视同仁：校验器拒绝即保留原值
            Assert.Equal(10, control.X);
            Assert.Contains(nameof(HmiControl.X), validator.Seen);
            f.Window.Close();
        });
    }

    /// <summary>
    /// 校验器拒绝的是"这次编辑器改动"，静态钳制仍在写回前生效：
    /// 默认校验器放行，但越界值会被 <c>ClampPosition/ClampSize</c> 收敛。
    /// </summary>
    [Fact]
    public void DefaultValidator_StillClampsPositionAndSize()
    {
        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            var f = Host(control);

            Find<FANumberBox>(f.Panel, "PropX").Value = 200_000;   // 超过 MaxPosition
            Find<FANumberBox>(f.Panel, "PropWidth").Value = 1;     // 低于 MinControlSize
            UiTestHost.Pump();

            Assert.Equal(100_000, control.X);
            Assert.Equal(10, control.Width);
            f.Window.Close();
        });
    }

    /// <summary>
    /// 不变量：**编辑器驱动的写回全部经过可插拔校验器**。
    /// 之前 X/Y/Width/Height 只过静态钳制、绕过插件校验器，插件作者无法预测
    /// 自己的规则何时生效；现在这个测试要求"拒绝一切"的校验器能挡住所有编辑器改动。
    /// </summary>
    [Fact]
    public void RejectingValidator_BlocksEveryEditorDrivenProperty()
    {
        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            var f = Host(control);

            var validator = new RejectAllValidator();
            f.Panel.CustomValidator = validator;

            TypeInto(f.Window, Find<TextBox>(f.Panel, "PropName"), "blocked_name");
            Find<FANumberBox>(f.Panel, "PropX").Value = 777;
            Find<FANumberBox>(f.Panel, "PropWidth").Value = 777;
            Find<FANumberBox>(f.Panel, "PropRadius").Value = 12;
            Find<FANumberBox>(f.Panel, "OpacityBox").Value = 30;
            UiTestHost.Pump();

            // 模型完全未被改动
            Assert.Equal("lbl1", control.Name);
            Assert.Equal(10, control.X);
            Assert.Equal(120, control.Width);
            Assert.Equal(0, control.Radius);
            Assert.NotEqual(30d, control.Opacity);

            // 且校验器确实被逐个征询（含此前被绕过的数值属性）
            foreach (var prop in new[]
                     {
                         nameof(HmiControl.Name), nameof(HmiControl.X), nameof(HmiControl.Width),
                         nameof(HmiControl.Radius), nameof(HmiControl.Opacity),
                     })
            {
                Assert.Contains(prop, validator.Seen);
            }

            f.Window.Close();
        });
    }

    /// <summary>
    /// 有意的例外：**程序化命令**（这里是"重置旋转/镜像"按钮）不被属性校验器否决。
    /// 用户点了按钮就是明确指令，让插件规则静默吞掉命令会让界面"点了没反应"。
    /// 这条测试同时锁住"命令先写模型、随后由命令触发的编辑器刷新再被校验器拒绝"的组合语义。
    /// </summary>
    [Fact]
    public void ProgrammaticCommand_IsNotBlockedByValidator()
    {
        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            control.Rotation = 45;
            control.MirrorX = true;
            control.MirrorY = true;
            var f = Host(control);

            f.Panel.CustomValidator = new RejectAllValidator();

            var reset = Find<Button>(f.Panel, "BtnResetRotation");
            reset.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            UiTestHost.Pump();

            Assert.Equal(0d, control.Rotation);
            Assert.Null(control.RotationDirection);
            Assert.False(control.MirrorX);
            Assert.False(control.MirrorY);
            f.Window.Close();
        });
    }

    [Fact]
    public void BindNull_ShowsScreenPropertiesPanel()
    {
        UiTestHost.Run(() =>
        {
            var f = Host(MakeControl());
            Assert.False(Find<Border>(f.Panel, "ScreenPropsPanel").IsVisible);

            f.Panel.CurrentScreen = new HmiScreen { Id = "s1", Name = "主画面", Width = 800, Height = 480 };
            f.Panel.BindControl(null);
            UiTestHost.Pump();

            Assert.True(Find<Border>(f.Panel, "ScreenPropsPanel").IsVisible);
            f.Window.Close();
        });
    }
}
