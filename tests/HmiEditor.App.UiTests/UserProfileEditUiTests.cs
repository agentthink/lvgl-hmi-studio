using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 用户管理「详情面板」两处编辑的**行为判据**（阶段 124，闭环登记 #66②）。
///
/// <para><b>病根</b>：<c>OnDetailChanged</c> 写在 <see cref="UserManagerView"/> 里、注释也齐，
/// 但**全仓全历史零引用** —— <c>DetailDisplayName.LostFocus</c> 与
/// <c>DetailRoleCombo.SelectionChanged</c> 两条订阅一个都没有 ⇒ 在界面上改显示名或改角色
/// **静默丢失**（改完关窗再看，值没变、审计里也没有）。它同时证明了一件事：
/// 「处理器写了」与「处理器会被调用」是两件事，只读代码看不出来。</para>
///
/// <para><b>本类管什么</b>（三条腿，各管一件不同的事）：</para>
/// <list type="number">
///   <item><b>落盘 ＋ 审计</b>：改显示名 ⇒ 模型变了、审计里多一条
///     <see cref="AuditAction.UserProfileChanged"/>（不是 <c>UserPermissionChanged</c> ——
///     显示名不是权限）；改角色 ⇒ 模型变了、审计里多一条 <c>UserPermissionChanged</c>。</item>
///   <item><b>幂等</b>：同值再失焦一次**不写第二条审计**（否则每次划过输入框都留一条空记录）。</item>
///   <item><b>回填是惰性的</b>：在两个**角色不同**的用户之间切换选中项，详情下拉会被程序性拨动 ——
///     那一下**一个字都不许写**（差量判定 ＋ 程序性回填守卫）。
///     ⚠ 这一条是**反向**判据：它挡的是"把选中变化误当成用户改角色"。</item>
/// </list>
///
/// <para><b>为什么用真焦点迁移而不是 <c>RaiseEvent(LostFocusEvent)</c></b>：订阅的是
/// <c>TextBox.LostFocus</c>，而「失焦」在产品里是用户点别处造成的。无头（headless）宿主上
/// <c>Focus()</c> 推键盘输入是活的（见 <c>HeadlessInputAndDialogUiTests</c>）⇒ 走真迁移，
/// 顺手把「这个事件在真实焦点迁移下会到」也一起验了。</para>
///
/// <para><b>本类不碰的两件事</b>（免得下一个人以为漏了）：① <b>不动全局语言</b>
/// ⇒ 不挂 <c>Language-global</c> 集合（读的都是语言无关的量：模型字段、审计动作、
/// 用户名；标题只判「含用户名」）；② <b>不判 <c>ShowUserDetail</c> 里那条回填守卫</b>
/// —— 它今天拿不出红腿（理由逐字写在产品代码里），属未证余量。</para>
/// </summary>
public class UserProfileEditUiTests
{
    // ── 夹具 ──

    private static T Require<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name)
           ?? throw new InvalidOperationException($"{window.GetType().Name} 里找不到具名 {typeof(T).Name} {name}");

    private static UserManagerView Open(AuthenticationService auth)
    {
        var view = new UserManagerView(auth);
        view.Show();
        UiTestHost.Pump();
        return view;
    }

    /// <summary>把 <c>UserList</c> 里指定用户名的条目装上（装不上就抛 —— 静默装不上等于布景没生效）。</summary>
    private static void SelectUser(Window window, string username)
    {
        var list = Require<ListBox>(window, "UserList");
        list.SelectedItem = list.Items.OfType<ListBoxItem>()
            .FirstOrDefault(i => i.Tag is UserProfile u && u.Username == username)
            ?? throw new InvalidOperationException($"UserList 里没有用户 {username} —— 夹具变了，布景要跟着改");
        UiTestHost.Pump();
    }

    /// <summary>
    /// 真实的焦点迁移：先把焦点给输入框（否则它**从来没拿到过**焦点，也就无所谓"失去"），
    /// 再交给同选项卡里另一个可聚焦控件 ⇒ <c>LostFocus</c>。每一步都 <c>Pump</c>。
    ///
    /// <para>⚠ 落点必须选**当前选项卡里**的控件：<c>BtnRefreshLog</c> 在「审计日志」页，
    /// 那一页默认没被选中 ⇒ 控件不可见 ⇒ <c>Focus()</c> 返回 <c>false</c>、焦点根本没走，
    /// 于是 <c>LostFocus</c> 不触发、判据静默变成"改名字没生效"（实测过一次）。</para>
    /// </summary>
    private static void BlurByMovingFocusAway(Window window, TextBox box)
    {
        box.Focus();
        UiTestHost.Pump();

        var elsewhere = Require<TextBox>(window, "NewUsername");
        Assert.True(elsewhere.Focus(), "焦点没走成 —— 落点控件不在当前选项卡/不可聚焦，本判据会静默失效");
        UiTestHost.Pump();
        Assert.False(box.IsFocused, "焦点还留在显示名框上 ⇒ 焦点迁移没发生");
    }

    /// <summary>
    /// <c>UserList</c> 里某个用户那一行的**渲染文案**。
    /// ⚠ 行内容是**嵌套**的（横向 StackPanel → 状态点 Border ＋ 纵向 StackPanel → 两个 TextBlock），
    /// 所以必须递归遍历 —— 只取第一层 <c>Children.OfType&lt;TextBlock&gt;()</c> 会拿到空串，
    /// 判据就退化成"恒真找不到"（实测踩过一次：报的是 <c>String: ""</c>）。
    /// </summary>
    private static string RowText(Window window, string username)
    {
        var list = Require<ListBox>(window, "UserList");
        var item = list.Items.OfType<ListBoxItem>()
            .FirstOrDefault(i => i.Tag is UserProfile u && u.Username == username);
        Assert.True(item is not null, $"UserList 里没有 {username} 这一行");

        var texts = new List<string>();
        CollectText(item!.Content, texts);
        return string.Join(" | ", texts);
    }

    private static void CollectText(object? node, List<string> texts)
    {
        switch (node)
        {
            case TextBlock { Text: { Length: > 0 } text }:
                texts.Add(text);
                break;
            case Border { Child: { } child }:
                CollectText(child, texts);
                break;
            case Panel panel:
                foreach (var child in panel.Children) CollectText(child, texts);
                break;
        }
    }

    private static string DetailRoleTag(Window window)
        => (Require<ComboBox>(window, "DetailRoleCombo").SelectedItem as ComboBoxItem)?.Tag as string ?? "";

    // ══════════════════════════════════════════════════════════════════
    //  ① 显示名：失焦即落盘，且只落一次

    [Fact]
    public void DisplayNameEdit_CommitsOnBlur_AndAuditsExactlyOnce()
    {
        UiTestHost.Run(() =>
        {
            var auth = new AuthenticationService();
            var view = Open(auth);
            try
            {
                SelectUser(view, "admin");

                // 前置读数：选中一个用户本身**不许留痕**（它只是回填界面）
                Assert.Empty(auth.GetAuditLog(500));

                var box = Require<TextBox>(view, "DetailDisplayName");
                box.Text = "运维管理员";
                BlurByMovingFocusAway(view, box);

                var admin = auth.GetUserByUsername("admin");
                Assert.NotNull(admin);
                Assert.Equal("运维管理员", admin!.DisplayName);

                // 改的是**资料**不是权限 ⇒ 审计动作必须分开（混进 UserPermissionChanged 会让
                // 审计里"权限变更"名不副实）
                var entry = Assert.Single(auth.GetAuditLog(500));
                Assert.Equal(AuditAction.UserProfileChanged, entry.Action);
                Assert.Equal("admin", entry.Username);

                // 显示名那条臂不许顺手改角色
                Assert.Equal("role-admin", admin.RoleId);

                // 列表行的显示名要跟着变（RefreshUserList 的手印）
                Assert.Contains("运维管理员", RowText(view, "admin"));

                // 标题尾要给反馈（ShowStatus 写的是窗口标题后缀）——只判"含用户名"，不冻文案
                Assert.Contains("admin", view.Title);

                // 幂等：同值再失焦一次，审计仍是那一条
                box.Text = "运维管理员";
                BlurByMovingFocusAway(view, box);
                Assert.Single(auth.GetAuditLog(500));
            }
            finally
            {
                view.Close();
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════
    //  ② 角色：选中即落盘、审计、面板重建

    [Fact]
    public void RoleEdit_PersistsAudits_AndRebuildsTheListRow()
    {
        UiTestHost.Run(() =>
        {
            var auth = new AuthenticationService();
            var view = Open(auth);
            try
            {
                SelectUser(view, "admin");

                // 前置：详情下拉回填的是**模型里**的角色（admin ⇒ role-admin）
                Assert.Equal("role-admin", DetailRoleTag(view));

                var combo = Require<ComboBox>(view, "DetailRoleCombo");
                combo.SelectedItem = combo.Items.OfType<ComboBoxItem>()
                    .First(i => (i.Tag as string) == UserRole.BuiltInEngineer.Id);
                UiTestHost.Pump();

                var admin = auth.GetUserByUsername("admin");
                Assert.NotNull(admin);
                Assert.Equal(UserRole.BuiltInEngineer.Id, admin!.RoleId);

                // 角色确实是权限 ⇒ 走既有的 UserPermissionChanged（与 SetUserEnabled / ChangeUserRole 同源）
                var entry = Assert.Single(auth.GetAuditLog(500));
                Assert.Equal(AuditAction.UserPermissionChanged, entry.Action);

                // RefreshUserList() 重建了列表行：角色角标必须已经是新角色的**本地化**名
                Assert.Contains(RoleTextKeys.DisplayName(UserRole.BuiltInEngineer), RowText(view, "admin"));

                // 面板重建后下拉被拨回**模型里**的新角色（不是留在手势拨的那个下标上）
                Assert.Equal(UserRole.BuiltInEngineer.Id, DetailRoleTag(view));

                Assert.Contains("admin", view.Title);
            }
            finally
            {
                view.Close();
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════
    //  ③ 反向：换选中（角色不同）时，程序性回填一个字都不许写

    [Fact]
    public void SelectingAnotherUser_WithADifferentRole_WritesNothing()
    {
        UiTestHost.Run(() =>
        {
            var auth = new AuthenticationService();
            // admin 是 role-admin；造一个 role-engineer 的用户 ⇒ 详情下拉在两者之间**必须**被拨动
            auth.CreateUser("tester", "测试员", "Abc12345", UserRole.BuiltInEngineer.Id);
            var baseline = auth.GetAuditLog(500).Count;   // CreateUser 自己落一条，从它之后算起

            var view = Open(auth);
            try
            {
                SelectUser(view, "admin");
                SelectUser(view, "tester");

                // 手势真的生效了（否则"什么都没写"是零信息）
                Assert.Equal(UserRole.BuiltInEngineer.Id, DetailRoleTag(view));
                Assert.Equal("测试员", Require<TextBox>(view, "DetailDisplayName").Text);

                // 而两边的模型一字未动、审计也没有新条目
                Assert.Equal(baseline, auth.GetAuditLog(500).Count);
                Assert.Equal(UserRole.BuiltInAdministrator.Id, auth.GetUserByUsername("admin")!.RoleId);
                Assert.Equal(UserRole.BuiltInEngineer.Id, auth.GetUserByUsername("tester")!.RoleId);
                Assert.Equal("测试员", auth.GetUserByUsername("tester")!.DisplayName);
            }
            finally
            {
                view.Close();
            }
        });
    }
}
