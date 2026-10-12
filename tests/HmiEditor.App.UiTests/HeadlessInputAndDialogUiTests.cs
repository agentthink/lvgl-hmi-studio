using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// headless 平台的输入模拟能力测试：真实命中测试 + Click/键盘事件链路。
///
/// <para>这是后续所有交互式 UI 测试的地基 —— 先证明"能点得动、能敲得进"，
/// 再拿它去测业务视图（例如确认对话框的确定/取消分支）。</para>
/// </summary>
public class HeadlessInputUiTests
{
    /// <summary>在 headless 顶层窗口上点击控件中心（真实命中测试，不是直接 RaiseEvent）。</summary>
    private static void ClickCenter(TopLevel top, Control target)
    {
        var center = target.TranslatePoint(
            new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), top);
        Assert.NotNull(center);
        top.MouseDown(center!.Value, MouseButton.Left);
        top.MouseUp(center.Value, MouseButton.Left);
        UiTestHost.Pump();
    }

    [Fact]
    public void MouseClick_OnButton_RaisesClick()
    {
        var clicks = UiTestHost.Run(() =>
        {
            var button = new Button { Content = "点我", Width = 80, Height = 30 };
            var count = 0;
            button.Click += (_, _) => count++;
            var window = new Window { Width = 200, Height = 100, Content = button };
            window.Show();
            UiTestHost.Pump();

            ClickCenter(window, button);
            ClickCenter(window, button);
            var result = count;
            window.Close();
            return result;
        });

        Assert.Equal(2, clicks);
    }

    [Fact]
    public void KeyboardInput_GoesToFocusedTextBox()
    {
        var text = UiTestHost.Run(() =>
        {
            var box = new TextBox { Width = 120, Height = 30 };
            var window = new Window { Width = 200, Height = 100, Content = box };
            window.Show();
            UiTestHost.Pump();

            box.Focus();
            UiTestHost.Pump();
            window.KeyTextInput("abc123");
            UiTestHost.Pump();
            var result = box.Text;
            window.Close();
            return result;
        });

        Assert.Equal("abc123", text);
    }

    [Fact]
    public void MouseClick_OutsideButton_DoesNotRaiseClick()
    {
        var clicks = UiTestHost.Run(() =>
        {
            var button = new Button { Content = "点我", Width = 60, Height = 24 };
            var panel = new Canvas { Children = { button } };
            Canvas.SetLeft(button, 0);
            Canvas.SetTop(button, 0);
            var count = 0;
            button.Click += (_, _) => count++;
            var window = new Window { Width = 300, Height = 200, Content = panel };
            window.Show();
            UiTestHost.Pump();

            window.MouseDown(new Point(250, 180), MouseButton.Left);
            window.MouseUp(new Point(250, 180), MouseButton.Left);
            UiTestHost.Pump();
            var result = count;
            window.Close();
            return result;
        });

        Assert.Equal(0, clicks);
    }
}

/// <summary>
/// <see cref="AppDialogs"/> 的真实 UI 测试。
///
/// <para>确认对话框是"删除画面/删除模板/删除用户"等不可逆操作的唯一拦截点，
/// 过去完全没有测试 —— 只能靠人肉点。这里真正把对话框弹出来，用模拟鼠标点
/// 确定/取消，断言返回值与文案。</para>
/// </summary>
public class AppDialogsUiTests
{
    private static async Task<bool> AnswerConfirmAsync(bool confirm)
    {
        var owner = new Window { Width = 400, Height = 300 };
        owner.Show();
        UiTestHost.Pump();

        var pending = AppDialogs.ConfirmAsync(owner, "确认删除 ‘主画面’？");
        UiTestHost.Pump();

        var dialog = owner.OwnedWindows.Single();
        var expected = confirm
            ? AppLocalization.Service["gen.confirm.delete"]
            : AppLocalization.Service["gen.cancel"];

        var buttons = dialog.GetVisualDescendants().OfType<Button>().ToList();
        var target = buttons.Single(b => (b.Content as string) == expected);
        var center = target.TranslatePoint(
            new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), dialog);
        Assert.NotNull(center);

        dialog.MouseDown(center!.Value, MouseButton.Left);
        dialog.MouseUp(center.Value, MouseButton.Left);

        var result = await pending;
        owner.Close();
        return result;
    }

    [Fact]
    public Task Confirm_ReturnsTrue_WhenConfirmClicked() =>
        UiTestHost.RunAsync(async () => Assert.True(await AnswerConfirmAsync(confirm: true)));

    [Fact]
    public Task Confirm_ReturnsFalse_WhenCancelClicked() =>
        UiTestHost.RunAsync(async () => Assert.False(await AnswerConfirmAsync(confirm: false)));

    [Fact]
    public void Confirm_ShowsCallerMessage()
    {
        UiTestHost.Run(() =>
        {
            var owner = new Window { Width = 400, Height = 300 };
            owner.Show();
            UiTestHost.Pump();

            _ = AppDialogs.ConfirmAsync(owner, "确认删除 ‘趋势页’？");
            UiTestHost.Pump();

            var dialog = owner.OwnedWindows.Single();
            var texts = dialog.GetVisualDescendants().OfType<TextBlock>()
                .Select(t => t.Text)
                .ToList();

            Assert.Contains("确认删除 ‘趋势页’？", texts);
            dialog.Close();
            owner.Close();
        });
    }
}
