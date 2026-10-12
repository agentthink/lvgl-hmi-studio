using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Layout;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Controls;
using HmiEditor.App;

namespace HmiEditor.App.Views;

/// <summary>
/// 用户管理对话框 —— 管理用户、角色和权限，查看审计日志。
/// 三选项卡设计：用户管理 / 角色管理 / 审计日志。
/// </summary>
public partial class UserManagerView : ChromeWindow
{
    private readonly AuthenticationService _auth;
    private UserProfile? _selectedUser;
    private UserRole? _selectedRole;
    private bool _isUpdatingRole;
    private bool _isUpdatingDetail;

    /// <summary>审计日志条目集合 —— 由虚拟化 ListBox 绑定，避免全量渲染。</summary>
    private readonly System.Collections.ObjectModel.ObservableCollection<AuditLogEntry> _auditEntries = new();

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public UserManagerView()
    {
        _auth = null!;
        InitializeComponent();
    }

    public UserManagerView(AuthenticationService auth) : this()
    {
        _auth = auth;
        _auth.AuditLogged += OnAuditLogged;
        AuditLogPanel.ItemsSource = _auditEntries;

        // 详情面板的两处编辑（阶段 124，#66②）：`OnDetailChanged` 此前**全历史零引用** ⇒
        // 改显示名/角色静默丢失。显示名在**失焦**时提交（不逐键写模型 + 逐键审计），
        // 角色下拉在选中变化时提交；两者都走 _auth 的"改模型 + 写审计"入口。
        DetailDisplayName.LostFocus += OnDetailChanged;
        DetailRoleCombo.SelectionChanged += OnDetailChanged;

        InitializeRoles();
        RefreshUserList();
        RefreshRoleList();
        RefreshAuditLog();
    }

    // ── 初始化 ──

    /// <summary>
    /// 初始化两个角色下拉。
    ///
    /// <para>#50：条目的 <c>Content</c> 是**本地化显示名 + 等级**、<c>Tag</c> 是角色 Id ——
    /// 不是把 <see cref="UserRole"/> 对象直接塞进 <c>ItemsSource</c>。后者在没有
    /// <c>ItemTemplate</c> 时只能渲染 <c>ToString()</c>，也就是类型全名
    /// <c>HmiEditor.Core.Models.User.UserRole</c>（阶段 66 从视觉树里实测到的原文，
    /// 见 <c>RoleNameLocalizationTests.RoleDisplaySurface_IsReported</c>）。
    /// 这与 <c>ProjectSettingsView.CmbRuntimeUserRole</c> 早就在用的写法一致。</para>
    ///
    /// <para><b>两个下拉各建一批 ComboBoxItem</b>：<see cref="ComboBoxItem"/> 是控件，
    /// 同一个实例不能同时挂在两个下拉的视觉树里 —— 共享一个列表会在第二处构造时抛
    /// "already has a visual parent"。</para>
    /// </summary>
    private void InitializeRoles()
    {
        var roles = _auth.GetAllRoles();
        NewRoleCombo.ItemsSource = roles
            .Select(r => new ComboBoxItem { Content = RoleTextKeys.DisplayNameWithLevel(r), Tag = r.Id })
            .ToList();
        NewRoleCombo.SelectedIndex = 0;

        DetailRoleCombo.ItemsSource = roles
            .Select(r => new ComboBoxItem { Content = RoleTextKeys.DisplayNameWithLevel(r), Tag = r.Id })
            .ToList();
    }

    /// <summary>按角色 Id 选中下拉项（条目是 ComboBoxItem ⇒ 不能直接赋模型对象）。</summary>
    private static void SelectRoleById(ComboBox combo, string? roleId) =>
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(i => string.Equals(i.Tag as string, roleId, StringComparison.Ordinal));

    // ════════════════════════════════════════════════════════════════
    // Tab 1: 用户管理
    // ════════════════════════════════════════════════════════════════

    private void RefreshUserList()
    {
        UserList.Items.Clear();
        var users = _auth.GetAllUsers();

        foreach (var user in users.OrderBy(u => u.Username))
        {
            var item = CreateUserListItem(user);
            UserList.Items.Add(item);
        }

        if (_selectedUser != null)
        {
            var match = UserList.Items
                .OfType<ListBoxItem>()
                .FirstOrDefault(i => i.Tag is UserProfile u && u.Id == _selectedUser.Id);
            if (match != null)
                UserList.SelectedItem = match;
        }
    }

    private ListBoxItem CreateUserListItem(UserProfile user)
    {
        var role = _auth.GetRole(user.RoleId);

        var nameText = new TextBlock
        {
            Text = user.DisplayName ?? user.Username,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
        };

        var infoParts = new List<string> { $"@{user.Username}" };
        if (role != null)
            infoParts.Add(RoleTextKeys.DisplayName(role));   // #50：预置角色名走字典，不用模型里的英文原名
        if (!user.IsEnabled)
            infoParts.Add(AppLocalization.Service["gen.disabled"]);
        if (user.IsLocked)
            infoParts.Add(AppLocalization.Service[Lang.UserMgrLocked]);

        var infoText = new TextBlock
        {
            Foreground = HmiEditor.App.Theme.TextHint,
            FontSize = 11,
            Text = string.Join(" · ", infoParts),
        };

        var statusDot = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = user.IsEnabled && !user.IsLocked
                ? new SolidColorBrush(UiPalette.ConnectedBright)
                : new SolidColorBrush(UiPalette.Disconnected),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var infoPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { nameText, infoText },
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { statusDot, infoPanel },
        };

        return new ListBoxItem
        {
            Content = panel,
            Tag = user,
            Padding = new Thickness(8),
        };
    }

    private void OnUserSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (UserList.SelectedItem is ListBoxItem item && item.Tag is UserProfile user)
        {
            _selectedUser = user;
            ShowUserDetail(user);
        }
    }

    private void ShowUserDetail(UserProfile user)
    {
        // 程序性回填不许触发 OnDetailChanged：`SelectRoleById` 就是给下拉赋选中项，
        // 在"换一个用户"时它**一定**会引发 SelectionChanged（与 ShowRoleDetail 的
        // `_isUpdatingRole` 同形，那是本视图早就有的写法）。
        //
        // ⚠ **诚实边界**：这条守卫今天**拿不出红腿** —— `OnDetailChanged` 的两条臂都是**差量**判定
        // （角色比 Id、显示名同值不写审计），而三个调用点（OnUserSelectionChanged / OnToggleEnabled /
        // OnUnlockUser）都在调用前就把 `_selectedUser` 指向被回填的那个人 ⇒ 即使事件漏过来也是空操作。
        // 它挡的是**顺序变动**：谁把 `OnUserSelectionChanged` 里 `_selectedUser = user` 挪到
        // `ShowUserDetail(user)` **之后**，漏过来的事件就会拿**上一个**用户去比对 ⇒ 写成跨用户改角色。
        // 属**未证的余量**，不是判据 —— 别把它当"已经测过了"。
        var wasUpdating = _isUpdatingDetail;
        _isUpdatingDetail = true;
        try
        {
            var role = _auth.GetRole(user.RoleId);

            DetailUsername.Text = user.Username;
            DetailDisplayName.Text = user.DisplayName;

            // #50：下拉条目是 ComboBoxItem（Tag = 角色 Id）⇒ 按 Id 选中，不能直接赋模型对象
            SelectRoleById(DetailRoleCombo, role?.Id);

            DetailEnabled.Text = user.IsEnabled ? AppLocalization.Service["alarm.enabled"] : AppLocalization.Service["user.mgr.disable"];
            // 状态着色由 .disabled 类驱动，取代 code-behind 双来源漂移
            DetailEnabled.Classes.Set("disabled", !user.IsEnabled);

            if (user.IsLocked)
            {
                DetailLocked.Text = AppLocalization.Service[Lang.UserMgrLocked];
                DetailLocked.IsVisible = true;
                BtnUnlock.IsVisible = true;
            }
            else
            {
                DetailLocked.Text = "";
                DetailLocked.IsVisible = false;
                BtnUnlock.IsVisible = false;
            }

            BtnToggleEnabled.Content = user.IsEnabled ? AppLocalization.Service["user.mgr.disable"] : AppLocalization.Service["alarm.enabled"];
            // 状态着色由 .user-disabled 类驱动，取代 code-behind 双来源漂移
            BtnToggleEnabled.Classes.Set("user-disabled", !user.IsEnabled);
        }
        finally
        {
            _isUpdatingDetail = wasUpdating;
        }
    }

    /// <summary>
    /// 详情面板的显示名 / 角色改动（阶段 124 接线，#66②）。
    ///
    /// <para><b>本方法此前是死的</b>：全仓、全历史零引用（从未订阅）⇒ 用户改显示名或角色
    /// **静默丢失**。现在由 <c>DetailDisplayName.LostFocus</c> 与
    /// <c>DetailRoleCombo.SelectionChanged</c> 触发；落盘与审计都走
    /// <see cref="AuthenticationService"/> 的既有入口（与"启用/禁用""重置密码"等按钮同一纪律）。</para>
    ///
    /// <para>⚠ <b>顺序是判据的一部分</b>：角色必须**先**改。因为收尾的
    /// <c>RefreshUserList()</c> 会经列表选中变化重填详情面板，那一步会把下拉拨回**模型里**的角色 ——
    /// 先刷新再读下拉，角色改动就永远读不到。</para>
    /// </summary>
    private void OnDetailChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingDetail || _selectedUser == null) return;

        var user = _selectedUser;

        // 两处改动各自报告 —— 用**列表拼一句**而不是先后两次 ShowStatus：
        // ShowStatus 写的是窗口标题后缀（只有一个槽位），先后调用会让前一条被后一条盖掉，
        // 于是"两处一起改"时用户只看到一半。空列表 ⇒ 一次都没改 ⇒ 不报状态。
        var messages = new List<string>(2);

        // ① 角色（先做，理由见方法头）
        if ((DetailRoleCombo.SelectedItem as ComboBoxItem)?.Tag is string newRoleId &&
            !string.Equals(newRoleId, user.RoleId, StringComparison.Ordinal))
        {
            _auth.ChangeUserRole(user.Id, newRoleId);
            messages.Add(AppLocalization.Service.Format(Lang.UserMgrRoleChanged, user.Username));
        }

        // ② 显示名（值没变时 UpdateUserDisplayName 内部不写审计；这里也不报"已更新"）
        var nameBefore = user.DisplayName;
        if (_auth.UpdateUserDisplayName(user.Id, DetailDisplayName.Text) &&
            !string.Equals(nameBefore, user.DisplayName, StringComparison.Ordinal))
        {
            messages.Add(AppLocalization.Service.Format(Lang.UserMgrProfileUpdated, user.Username));
        }

        if (messages.Count > 0)
            ShowStatus(string.Join(" ", messages));

        // ③ 列表项里的显示名/角色角标要跟着变（重建后会重新选中当前用户并回填面板）
        RefreshUserList();
    }

    private void OnAddUser(object? sender, RoutedEventArgs e)
    {
        var username = NewUsername.Text?.Trim();
        var password = NewPassword.Text;
        var roleId = (NewRoleCombo.SelectedItem as ComboBoxItem)?.Tag as string;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowStatus(AppLocalization.Service[Lang.UserMgrInputUserPwd]);
            return;
        }

        // 显示名可选：未输入时回退到用户名（此处 username 已保证非空）
        var displayName = NewDisplayName.Text?.Trim() ?? username;

        // 检查用户名是否已存在
        if (_auth.GetUserByUsername(username) != null)
        {
            ShowStatus(AppLocalization.Service.Format(Lang.UserMgrUsernameExists, username));
            return;
        }

        // 角色可选：未选择时 CreateUser 使用 SecurityConfig.DefaultRoleId 作为默认角色
        // 捕获 UserAdminException（角色未注册/密码长度不足）显示友好提示，避免未处理异常。
        // ⚠ 阶段 97（#61d）：产出方从裸 ArgumentException 换成**带码异常** ⇒ catch 类型必须跟着换，
        //    否则异常穿透出去（新类型不继承 ArgumentException，那是刻意的：见 UserAdminException 的注释）。
        UserProfile newUser;
        try
        {
            newUser = _auth.CreateUser(username, displayName, password, roleId);
        }
        catch (UserAdminException ex)
        {
            ShowStatus(AppLocalization.Service.Format(Lang.UserMgrCreateFailed, ExternalText.FailureReason(ex)));
            return;
        }

        NewUsername.Text = "";
        NewPassword.Text = "";
        NewDisplayName.Text = "";

        _selectedUser = newUser;
        RefreshUserList();
        ShowStatus(AppLocalization.Service.Format(Lang.UserMgrAddedUser, username));
    }

    private void OnDeleteUser(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpDeleteUser, async () =>
        {
            if (_selectedUser == null) return;

            // 不允许删除自己（当前会话用户）
            if (_selectedUser.Username == "admin")
            {
                ShowStatus(AppLocalization.Service[Lang.UserMgrCannotDeleteAdmin]);
                return;
            }

            // 删除为不可逆操作，先确认。
            var username = _selectedUser.Username;
            var ok = await AppDialogs.ConfirmAsync(
                this, AppLocalization.Service.Format("user.mgr.confirm.delete", username));
            if (!ok) return;

            _auth.DeleteUser(_selectedUser.Id);
            _selectedUser = null;
            RefreshUserList();
            ClearUserDetail();
            ShowStatus(AppLocalization.Service.Format("user.mgr.deleted.user", username));
        }, ShowStatus);

    private void OnToggleEnabled(object? sender, RoutedEventArgs e)
    {
        if (_selectedUser == null) return;

        var newState = !_selectedUser.IsEnabled;
        _auth.SetUserEnabled(_selectedUser.Id, newState);
        ShowUserDetail(_selectedUser);
        RefreshUserList();
        ShowStatus(newState ? AppLocalization.Service.Format(Lang.UserMgrEnabled, _selectedUser.Username) : AppLocalization.Service.Format(Lang.UserMgrDisabled, _selectedUser.Username));
    }

    private void OnResetPassword(object? sender, RoutedEventArgs e)
    {
        if (_selectedUser == null) return;

        var passwordBox = new TextBox
        {
            Background = HmiEditor.App.Theme.BgDeep,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            BorderThickness = new Thickness(1),
            PlaceholderText = AppLocalization.Service[Lang.UserMgrInputNewPwd],
        };

        var confirmBtn = new Button
        {
            Content = AppLocalization.Service[Lang.UserMgrConfirmReset],
            Background = new SolidColorBrush(Color.FromRgb(30, 64, 175)),
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 6),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled = false,
        };

        var dialog = new Window
        {
            Title = AppLocalization.Service["user.mgr.reset.pwd"],
            Width = 320,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = HmiEditor.App.Theme.BgWindow,
            Content = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                Margin = new Thickness(16),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = AppLocalization.Service.Format(Lang.UserMgrSetPwdFor, _selectedUser.Username),
                                Foreground = HmiEditor.App.Theme.TextPrimary,
                                FontSize = 14,
                            },
                            passwordBox,
                        },
                    },
                    confirmBtn,
                },
            },
        };

        passwordBox.TextChanged += (_, _) =>
        {
            confirmBtn.IsEnabled = !string.IsNullOrWhiteSpace(passwordBox.Text);
        };

        confirmBtn.Click += (_, _) =>
        {
            var newPass = passwordBox.Text;
            if (!string.IsNullOrWhiteSpace(newPass))
            {
                // ⚠ 阶段 97（#61d）：本站在补 catch 之前**一个都不接** ——
                //    同一个产出方（SetUserPassword）在「创建用户」那条路有 catch、这里没有，
                //    密码长度不足时异常直接冒泡出事件处理器（未处理的 Avalonia 异常）。
                //    ⇒ 同一产出方的两条路径一起收口，不许只收一半。
                try
                {
                    _auth.SetUserPassword(_selectedUser, newPass);
                }
                catch (Exception ex)
                {
                    ShowStatus(AppLocalization.Service.Format(Lang.UserMgrPwdResetFailed, ExternalText.FailureReason(ex)));
                    return;
                }

                dialog.Close();
                RefreshUserList();
                ShowStatus(AppLocalization.Service.Format(Lang.UserMgrPwdReset, _selectedUser.Username));
            }
        };

        dialog.ShowDialog(this);
    }

    private void OnUnlockUser(object? sender, RoutedEventArgs e)
    {
        if (_selectedUser == null) return;

        _selectedUser.IsLocked = false;
        _selectedUser.FailedLoginAttempts = 0;
        ShowUserDetail(_selectedUser);
        RefreshUserList();
        ShowStatus(AppLocalization.Service["gen.unlocked"] + $": {_selectedUser.Username}");
    }

    private void ClearUserDetail()
    {
        DetailUsername.Text = "";
        DetailDisplayName.Text = "";
        DetailRoleCombo.SelectedItem = null;
        DetailEnabled.Text = "";
        DetailLocked.Text = "";
        BtnUnlock.IsVisible = false;
    }

    // ════════════════════════════════════════════════════════════════
    // Tab 2: 角色管理
    // ════════════════════════════════════════════════════════════════

    private void RefreshRoleList()
    {
        RoleList.Items.Clear();
        var roles = _auth.GetAllRoles();

        foreach (var role in roles.OrderByDescending(r => r.Level))
        {
            var textBlock = new TextBlock
            {
                Text = RoleTextKeys.DisplayNameWithLevel(role),
                Foreground = role.IsBuiltIn ? HmiEditor.App.Theme.TextPrimary : HmiEditor.App.Theme.TextSecondary,
                FontSize = 13,
            };

            var descText = new TextBlock
            {
                Text = RoleTextKeys.DisplayDescription(role),
                Foreground = HmiEditor.App.Theme.TextHint,
                FontSize = 11,
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Children = { textBlock, descText },
            };

            RoleList.Items.Add(new ListBoxItem
            {
                Content = panel,
                Tag = role,
                Padding = new Thickness(8),
            });
        }
    }

    private void OnRoleSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RoleList.SelectedItem is ListBoxItem item && item.Tag is UserRole role)
        {
            _selectedRole = role;
            ShowRoleDetail(role);
        }
    }

    private void ShowRoleDetail(UserRole role)
    {
        _isUpdatingRole = true;

        // #50：名字与描述都是**只读展示**（RoleNameBox 是 IsReadOnly 的 TextBox、RoleDescText 是
        // 只读 TextBlock，且 OnSaveRole 只写 Level 与 Permissions）⇒ 它们该跟界面语言走，
        // 不该显示模型里的英文字面量。模型的 Name/Description 仍保留：前者进生成头
        // HMI_SEC_ROLES_INIT（产物契约 —— 不随 UI 语言变），后者供**自定义**角色用（表外角色没有键）。
        RoleNameBox.Text = RoleTextKeys.DisplayName(role);
        RoleLevelSlider.Value = role.Level;
        RoleLevelText.Text = role.Level.ToString();
        RoleDescText.Text = RoleTextKeys.DisplayDescription(role);

        // 设置权限复选框
        SetPermissionCheckbox(PermViewScreen, Permission.ViewScreen, role.Permissions);
        SetPermissionCheckbox(PermOperateControl, Permission.OperateControl, role.Permissions);
        SetPermissionCheckbox(PermEditRecipe, Permission.EditRecipe, role.Permissions);
        SetPermissionCheckbox(PermAcknowledgeAlarm, Permission.AcknowledgeAlarm, role.Permissions);
        SetPermissionCheckbox(PermShelveAlarm, Permission.ShelveAlarm, role.Permissions);
        SetPermissionCheckbox(PermChangeConfig, Permission.ChangeConfig, role.Permissions);
        SetPermissionCheckbox(PermManageUsers, Permission.ManageUsers, role.Permissions);
        SetPermissionCheckbox(PermViewAlarmHistory, Permission.ViewAlarmHistory, role.Permissions);
        SetPermissionCheckbox(PermExportData, Permission.ExportData, role.Permissions);
        SetPermissionCheckbox(PermDebugMode, Permission.DebugMode, role.Permissions);

        _isUpdatingRole = false;
    }

    private static void SetPermissionCheckbox(CheckBox checkBox, Permission permission, Permission currentPermissions)
    {
        checkBox.IsChecked = (currentPermissions & permission) == permission;
    }

    private void OnRoleLevelChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingRole || _selectedRole == null) return;
        var level = (int)e.NewValue;
        RoleLevelText.Text = level.ToString();
    }

    private void OnPermissionChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingRole || _selectedRole == null) return;
    }

    private void OnSaveRole(object? sender, RoutedEventArgs e)
    {
        if (_selectedRole == null) return;

        if (_selectedRole.IsBuiltIn)
        {
            // 更新预置角色的等级和权限
            _selectedRole.Level = (int)RoleLevelSlider.Value;
            _selectedRole.Permissions = GetPermissionsFromUI();
            RefreshRoleList();
            RefreshUserList();
            ShowStatus(AppLocalization.Service.Format(Lang.UserMgrRoleUpdated, RoleTextKeys.DisplayName(_selectedRole)));
        }
        else
        {
            _selectedRole.Level = (int)RoleLevelSlider.Value;
            _selectedRole.Permissions = GetPermissionsFromUI();
            RefreshRoleList();
            RefreshUserList();
            ShowStatus(AppLocalization.Service.Format("user.mgr.saved.role", RoleTextKeys.DisplayName(_selectedRole)));
        }
    }

    private Permission GetPermissionsFromUI()
    {
        var perms = Permission.None;
        if (PermViewScreen.IsChecked == true) perms |= Permission.ViewScreen;
        if (PermOperateControl.IsChecked == true) perms |= Permission.OperateControl;
        if (PermEditRecipe.IsChecked == true) perms |= Permission.EditRecipe;
        if (PermAcknowledgeAlarm.IsChecked == true) perms |= Permission.AcknowledgeAlarm;
        if (PermShelveAlarm.IsChecked == true) perms |= Permission.ShelveAlarm;
        if (PermChangeConfig.IsChecked == true) perms |= Permission.ChangeConfig;
        if (PermManageUsers.IsChecked == true) perms |= Permission.ManageUsers;
        if (PermViewAlarmHistory.IsChecked == true) perms |= Permission.ViewAlarmHistory;
        if (PermExportData.IsChecked == true) perms |= Permission.ExportData;
        if (PermDebugMode.IsChecked == true) perms |= Permission.DebugMode;
        return perms;
    }

    // ════════════════════════════════════════════════════════════════
    // Tab 3: 审计日志
    // ════════════════════════════════════════════════════════════════

    private void RefreshAuditLog()
    {
        _auditEntries.Clear();
        var entries = _auth.GetAuditLog(500);

        foreach (var entry in entries)
        {
            _auditEntries.Add(entry);
        }
    }

    /// <summary>将 AuditAction 映射为本地化显示名（供 DataTemplate 转换器使用）。</summary>
    public static string ActionDisplayName(AuditAction action) => action switch
    {
        AuditAction.Login => AppLocalization.Service["login.btn"],
        AuditAction.Logout => AppLocalization.Service[Lang.UserMgrAuditLogout],
        AuditAction.LoginFailed => AppLocalization.Service["user.mgr.login.failed"],
        AuditAction.UserCreated => AppLocalization.Service[Lang.UserMgrAuditCreateUser],
        AuditAction.UserDeleted => AppLocalization.Service["user.mgr.delete"],
        AuditAction.UserPermissionChanged => AppLocalization.Service[Lang.UserMgrAuditPermChange],
        // 阶段 124（#66②）：显示名变更走独立动作 ⇒ 审计表里必须有自己的显示名，
        // 否则落到 `_ => action.ToString()`，用户看到的是 "UserProfileChanged" 英文标识符。
        AuditAction.UserProfileChanged => AppLocalization.Service[Lang.UserMgrAuditProfileChange],
        AuditAction.ConfigChanged => AppLocalization.Service[Lang.UserMgrAuditConfigChange],
        AuditAction.PasswordChanged => AppLocalization.Service[Lang.UserMgrAuditPwdChange],
        AuditAction.BreakGlassAccess => AppLocalization.Service[Lang.UserMgrAuditEmergency],
        AuditAction.DataExported => AppLocalization.Service[Lang.UserMgrAuditExport],
        // 阶段 278：搁置/解除也进审计 ⇒ 这两档必须有自己的显示名（否则落到 `_ => ToString()`，
        // 用户看到的又是英文标识符 —— 与 `UserProfileChanged` 当初那条同一个道理）
        AuditAction.AlarmShelved => AppLocalization.Service["user.mgr.audit.alarm.shelve"],
        AuditAction.AlarmUnshelved => AppLocalization.Service["user.mgr.audit.alarm.unshelve"],
        AuditAction.AlarmSuppressed => AppLocalization.Service["user.mgr.audit.alarm.suppress"],
        AuditAction.AlarmUnsuppressed => AppLocalization.Service["user.mgr.audit.alarm.unsuppress"],
        _ => action.ToString(),
    };

    private void OnRefreshLog(object? sender, RoutedEventArgs e)
    {
        RefreshAuditLog();
        ShowStatus(AppLocalization.Service[Lang.UserMgrAuditRefreshed]);
    }

    private void OnClearLog(object? sender, RoutedEventArgs e)
    {
        // 审计日志设计为 append-only，不支持清空。这里的"清空"仅指 UI 显示
        _auditEntries.Clear();
        ShowStatus(AppLocalization.Service[Lang.UserMgrAuditCleared]);
    }

    private void OnAuditLogged(object? sender, AuditLogEntry entry)
    {
        // 如果当前在审计日志页，自动追加新条目
        if (AuditLogPanel != null)
        {
            _auditEntries.Insert(0, entry);
        }
    }

    // ── 辅助方法 ──

    private void ShowStatus(string message)
    {
        // 更新窗口标题后缀以显示状态信息
        Title = $"{AppLocalization.Service["win.user.manager"]} — {message}";
    }

    protected override void OnClosed(EventArgs e)
    {
        // 生命周期对称：窗口关闭时退订认证服务事件，避免服务持有引用导致泄漏。
        if (_auth != null)
            _auth.AuditLogged -= OnAuditLogged;
        base.OnClosed(e);
    }
}


