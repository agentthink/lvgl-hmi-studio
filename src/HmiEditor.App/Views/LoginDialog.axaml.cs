using Avalonia.Controls;
using Avalonia.Input;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Views;

/// <summary>
/// 登录对话框 —— 用户身份认证。
/// </summary>
public partial class LoginDialog : ChromeWindow
{
    private readonly AuthenticationService _authService;

    /// <summary>
    /// 登录成功后获取的会话 ID，null 表示未登录。
    /// </summary>
    public string? SessionId { get; private set; }

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public LoginDialog()
    {
        _authService = null!;
        InitializeComponent();
    }

    public LoginDialog(AuthenticationService authService) : this()
    {
        _authService = authService;

        BtnLogin.Click += async (_, _) => await OnLogin();
        BtnCancel.Click += (_, _) => Close();

        // 回车键触发登录
        TxtUsername.KeyDown += OnKeyDown;
        TxtPassword.KeyDown += OnKeyDown;

        Loaded += (_, _) => TxtUsername.Focus();
    }

    private async Task OnLogin()
    {
        var username = TxtUsername.Text?.Trim();
        var password = TxtPassword.Text;

        // ⚠ 用 IsNullOrWhiteSpace 而不是 IsNullOrEmpty（登记 #61t，阶段 98）：服务端那两行是
        // `ArgumentException.ThrowIfNullOrWhiteSpace(username/password)` ⇒ 只挡「null / 空串」时，
        // **纯空白**输入会溜进去、走异常路，界面把 BCL 英文原文原样显示出来
        // （`Value cannot be null or whitespace. (Parameter 'password')`，而中文界面下也是英文）。
        // 站点与服务端的门槛必须**对齐** —— 中间那条缝本身就是一条「英文原文直通」路。
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowError(AppLocalization.Service["login.input.required"]);
            return;
        }

        ErrorText.IsVisible = false;
        BtnLogin.IsEnabled = false;
        BtnLogin.Content = AppLocalization.Service["login.in.progress"];

        try
        {
            var result = await Task.Run(() => _authService.Login(username, password));

            if (result.SessionId is { } sessionId)
            {
                SessionId = sessionId;
                Close();
            }
            else if (result.Issue is { } issue)
            {
                // 按**码**分派文案（登记 #61v，阶段 99）。改动前五档失败
                // （用户不存在 / 锁定 / 禁用 / 过期 / 密码错）一律显示同一句 login.err
                // ⇒ 被锁定的用户不知道要等多久、被禁用的不知道找谁。
                ShowError(ExternalText.Describe(issue));
            }
        }
        catch (Exception ex)
        {
            ShowError(AppLocalization.Service.Format("login.failed", ex.Message));
        }
        finally
        {
            BtnLogin.IsEnabled = true;
            BtnLogin.Content = AppLocalization.Service["login.btn"];
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = OnLogin();
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }
}
