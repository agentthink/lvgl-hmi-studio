using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HmiEditor.App.Views;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`UserManagerView` 的面 B 驱动面**（阶段 194）—— 该文件里那条接线在
/// **重置口令对话框**里：`passwordBox.TextChanged += (_, _) => confirmBtn.IsEnabled = …`
/// （两个控件都是 `OnResetPassword` 里现造的局部变量，任何 `x:Name` 都对不上 ⇒ 普查面记在面 B）。
///
/// <para><b>入口</b>：对话框是 `dialog.ShowDialog(this)` 弹出来的 ⇒ 它是宿主窗口的**从属窗口**，
/// 可以从 `view.OwnedWindows` 拿到（与 `DialogInnerInvocationUiTests` 同一手法）；
/// 再在那棵可视树里取那个 `TextBox`（唯一）与那个 `Button`（唯一）。</para>
///
/// <para><b>读的是哪一面</b>：这条接线的**全部产出就是那个按钮的使能态**（没有模型落点）——
/// 所以这里读 `IsEnabled`，并把"空白不算内容"这条口径也钉住。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class UserManagerTextBoxWiringTests
{
    /// <summary>
    /// 本面驱动的**接线标识**（`UserManagerView.axaml.cs` 里那个局部变量名）。
    /// 普查面的面 B 表按它核对"这条接线有人认账" —— 改名后不更新就红。
    /// </summary>
    private static readonly string[] WiredIdentifiers = { "passwordBox" };

    [Fact]
    public void PasswordBox_TogglesTheConfirmButtonAsDeclared()
    {
        using var sandbox = new DataRootSandbox("usermanager-textbox");

        UiTestHost.Run(() =>
        {
            var view = new UserManagerView(new AuthenticationService());
            view.Show();
            UiTestHost.Pump(3);

            Window? dialog = null;
            try
            {
                // 前提自检：列表里有用户，且能选中一条（`OnResetPassword` 首行判 `_selectedUser == null`）
                var list = ControlWiringFace.Find<ListBox>(view, "UserList");
                Assert.NotEmpty(list.Items);
                list.SelectedIndex = 0;
                UiTestHost.Pump(2);

                // 点「重置口令」⇒ `OnResetPassword` 现造对话框并 `ShowDialog(this)`
                ControlWiringFace.Find<Button>(view, "BtnResetPassword")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                UiTestHost.Pump(3);

                dialog = Assert.Single(view.OwnedWindows);

                var box = Assert.Single(dialog.GetLogicalDescendants().OfType<TextBox>());
                var confirm = Assert.Single(dialog.GetLogicalDescendants().OfType<Button>());

                // 口径：出厂**禁用**；空白**不算内容**；有内容才启用
                Assert.False(confirm.IsEnabled);

                ControlWiringFace.Write(dialog, box, "   ", pump: 1);
                Assert.False(confirm.IsEnabled);

                ControlWiringFace.Write(dialog, box, "Pw12345!", pump: 1);
                Assert.True(confirm.IsEnabled);

                // 再清空 ⇒ 回到禁用（不是"启用过一次就常亮"）
                ControlWiringFace.Write(dialog, box, "", pump: 1);
                Assert.False(confirm.IsEnabled);
            }
            finally
            {
                dialog?.Close();
                UiTestHost.Pump();
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **面 B 逐条认账**：那条对话框里的接线（`passwordBox`）在本面的表里，且与扫描一致。
    /// </summary>
    [Fact]
    public void EveryAnonymousWiring_IsAccountedFor()
    {
        ControlWiringFace.AssertAnonymousFace("UserManagerView.axaml", WiredIdentifiers, scanFloor: 1);
    }
}
