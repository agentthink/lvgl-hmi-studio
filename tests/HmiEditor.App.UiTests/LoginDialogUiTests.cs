using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HmiEditor.App.Resources;
using HmiEditor.App.Views;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`LoginDialog`（登录窗）的按钮面**（阶段 257；登记 `#39` 残余面）。
///
/// <para><b>为什么单独立判据：</b>它是应用启动链打开的模态窗（`App`/`MainWindow` 那条），
/// 不是"菜单打开的窗口"（第三层够不到）、也不是停靠面板（第四层够不到）⇒ 专题判据（第七次）。</para>
///
/// <para><b>与既有判据的分工：</b>`LoginIssueLocalizationGuardTests` 是**源码扫描面**
/// （查 `LoginDialog.OnLogin` 那几处文案站点有没有码化、有没有接住失败档），它**不点按钮**；
/// 本判据判的是**点 `BtnLogin` 真的发生了什么**：空白输入的早退档、密码错的按码分派、成功档的
/// `SessionId` ＋ 关窗。⚠ 用**沙箱里的真 `AuthenticationService`**（`CreateUser` 造一个用户，
/// 不 mock 服务 —— 这里要判的正是"界面把输入交给了服务、服务的结果又回到了界面"这条链）。</para>
///
/// <para><b>3 个具名动作控件与桶</b>（面闭合见 <see cref="Face_MatchesTheDeclaredTable"/>）：
/// `BtnLogin` = **Moves**（三条腿：空白早退 / 密码错 / 成功关窗）；`BtnCancel` = **Moves**（关窗）；
/// `BtnTitleClose` = **Never**（`ChromeWindow` 标题栏关闭按钮 ⇒ 与 `BtnCancel` 同后果，保留一条）。</para>
///
/// <para><b>不抓什么：</b>① 不重复判文案站点（`LoginIssueLocalizationGuardTests` 管）；
/// ② 不判锁定/禁用/过期那几档（那是**服务**的行为，`AuthenticationService` 侧有自己的判据）；
/// ③ 不判真实命中测试；④ 不判 `App.axaml` 那条启动链。</para>
/// </summary>
[Collection("Language-global")]
public class LoginDialogUiTests
{
    private readonly ITestOutputHelper _out;

    public LoginDialogUiTests(ITestOutputHelper o) => _out = o;

    private const string User = "alice";
    private const string RightPassword = "Passw0rd!";
    private const string WrongPassword = "nope-nope";

    private static (LoginDialog Dialog, AuthenticationService Auth) Open()
    {
        var auth = new AuthenticationService();
        auth.CreateUser(User, "Alice", RightPassword);      // 预置角色 ⇒ 不传 roleId
        var dialog = new LoginDialog(auth);
        dialog.Show();
        UiTestHost.Pump(5);
        return (dialog, auth);
    }

    private static void Click(Control root, string name)
    {
        ControlWiringFace.Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump(3);
    }

    private static TextBlock ErrorBlock(Control root) => ControlWiringFace.Find<TextBlock>(root, "ErrorText");
    private static string ErrorTextOf(Control root) => ErrorBlock(root).Text ?? "";

    private static async Task UntilAsync(Func<bool> condition, string what, int timeoutMs = 10000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            UiTestHost.Pump(2);
            if (condition()) return;
            await Task.Delay(20);
        }

        Assert.Fail($"等不到：{what}（已等 {timeoutMs} ms）");
    }

    [Fact]
    public void Face_MatchesTheDeclaredTable()
    {
        UiTestHost.Run(() =>
        {
            var (dialog, _) = Open();
            try
            {
                var actual = dialog.GetLogicalDescendants().OfType<Button>()
                    .Where(b => b.GetType() == typeof(Button))
                    .Where(b => b.Name is not null)
                    .Select(b => b.Name!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();

                Assert.Equal(new[] { "BtnCancel", "BtnLogin", "BtnTitleClose" }, actual);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public async Task Login_WithWhitespaceInput_EarlyReturnsWithTheRequiredHint()
    {
        await UiTestHost.RunAsync(async () =>
        {
            var (dialog, _) = Open();
            try
            {
                Assert.False(ErrorBlock(dialog).IsVisible);

                // ⚠ 纯空白（不是空串）—— 这一档正是登记 `#61t` 对齐过的门槛
                ControlWiringFace.Write(dialog, "TxtUsername", "   ");
                ControlWiringFace.Write(dialog, "TxtPassword", "   ");

                Click(dialog, "BtnLogin");
                await UntilAsync(() => ErrorBlock(dialog).IsVisible, "空白输入给出必填提示");

                Assert.Equal(AppLocalization.Service["login.input.required"], ErrorTextOf(dialog));
                Assert.Null(dialog.SessionId);       // 没有登录
                Assert.True(dialog.IsVisible);       // 也没关窗

                JudgeReadings.Write(_out, "登录窗 · 空白输入（#39 残余面 · 阶段 257）", new[]
                {
                    "面：具名 Button 3 个 —— BtnLogin / BtnCancel / BtnTitleClose（双向差集，已断言）",
                    $"错误行可见={ErrorBlock(dialog).IsVisible}；文案=`{ErrorTextOf(dialog)}`",
                    $"SessionId={(dialog.SessionId is null ? "null" : dialog.SessionId)}；IsVisible={dialog.IsVisible}",
                });
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShowsTheCodedIssue_AndStaysOpen()
    {
        await UiTestHost.RunAsync(async () =>
        {
            var (dialog, _) = Open();
            try
            {
                ControlWiringFace.Write(dialog, "TxtUsername", User);
                ControlWiringFace.Write(dialog, "TxtPassword", WrongPassword);

                Click(dialog, "BtnLogin");
                await UntilAsync(() => ErrorBlock(dialog).IsVisible, "密码错给出提示");

                var shown = ErrorTextOf(dialog);
                Assert.False(string.IsNullOrWhiteSpace(shown));
                Assert.NotEqual(AppLocalization.Service["login.input.required"], shown);   // 不是"没填"那一档
                Assert.Null(dialog.SessionId);
                Assert.True(dialog.IsVisible);

                JudgeReadings.Write(_out, "登录窗 · 密码错（#39 残余面 · 阶段 257）", new[]
                {
                    "面：具名 Button 3 个（双向差集，已断言）",
                    $"错误行文案=`{shown}`（按 `LoginIssue` 的码分派，与「没填」那档不同）",
                    $"SessionId={(dialog.SessionId is null ? "null" : dialog.SessionId)}；IsVisible={dialog.IsVisible}",
                });
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_SetsSessionAndCloses()
    {
        await UiTestHost.RunAsync(async () =>
        {
            var (dialog, auth) = Open();
            var closed = 0;
            dialog.Closed += (_, _) => closed++;
            try
            {
                ControlWiringFace.Write(dialog, "TxtUsername", User);
                ControlWiringFace.Write(dialog, "TxtPassword", RightPassword);

                Click(dialog, "BtnLogin");
                await UntilAsync(() => dialog.SessionId is not null, "成功档给出会话 Id");

                Assert.NotNull(dialog.SessionId);
                Assert.Equal(1, closed);                 // 成功才关窗
                Assert.False(dialog.IsVisible);
                Assert.Contains(auth.GetAllUsers(), u => u.Username == User);   // 服务侧用户还在（没被登录改坏）

                JudgeReadings.Write(_out, "登录窗 · 成功（#39 残余面 · 阶段 257）", new[]
                {
                    "面：具名 Button 3 个（双向差集，已断言）",
                    $"SessionId=`{dialog.SessionId}`；Closed 触发 {closed} 次；IsVisible={dialog.IsVisible}",
                    "链路：界面读两格 ⇒ `AuthenticationService.Login` ⇒ 成功 ⇒ 记 SessionId ＋ `Close()`",
                });
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Cancel_ClosesTheDialog()
    {
        UiTestHost.Run(() =>
        {
            var (dialog, _) = Open();
            var closed = 0;
            dialog.Closed += (_, _) => closed++;
            try
            {
                Click(dialog, "BtnCancel");
                UiTestHost.Pump(3);

                Assert.Equal(1, closed);
                Assert.False(dialog.IsVisible);
                Assert.Null(dialog.SessionId);
            }
            finally { dialog.Close(); }
        });
    }
}
