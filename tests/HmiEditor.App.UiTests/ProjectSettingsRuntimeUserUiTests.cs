using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Resources;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// `ProjectSettingsView` 的**运行时用户表**行为测试 —— 补上 `docs/2026-09-16-runtime-user-editor-ui.md` §7.1
/// 登记的缺口（"App 层没有 ProjectSettingsView 的测试基建……草稿语义 / 两层拷贝这类绑在对话框生命周期上的
/// 行为仍只有人工验证"）。
///
/// <para><b>为什么这组行为值得单独钉：</b>该面板的取消语义**不是**由框架提供的，而是靠两条不变量撑着 ——
/// ① 装载时深拷贝（<c>BindRuntimeUsers</c>），② 保存时再拷一层（<c>SaveRuntimeUsers</c>）。
/// 少第一层 → 编辑期就在改模型，"取消"取消了也没用；少第二层 → 模型与草稿共享引用，
/// 点完确定再编辑**仍会改到模型**。两者都**编译通过、界面看起来正常**，
/// 只有真的驱动一遍对话框生命周期才看得出来。</para>
///
/// <para><b>为什么用 <c>RaiseEvent</c> 而不是真实命中测试点击：</b>本文件测的是**接线与语义**
/// （事件订阅 + 处理器 + 控件交互）；命中测试/布局是另一条轴，已由 <c>HeadlessInputUiTests</c>
/// 与 <c>ViewSmokeUiTests</c> 覆盖。用户表控件位于安全分类面板的 <c>ScrollViewer</c> 内，
/// 用坐标点击会让测试依赖"窗口高度够不够把按钮露出来"这种与语义无关的条件。</para>
///
/// <para><b>为什么每个用例整体跑在 <see cref="UiTestHost.Run"/> 里：</b>控件必须在 headless UI 线程上
/// 构造（<c>ChromeWindow</c> 的构造函数要拿平台服务），否则报
/// "Unable to locate 'Avalonia.Platform.IWindowingPlatform'"。</para>
/// </summary>
public class ProjectSettingsRuntimeUserUiTests
{
    private const string SomeHash = "AAECAwQFBgcICQoLDA0ODw==:wNu1hDVTcb2XOXidGgkb4c9GEr6wxjDGAGaH2VWrOLk=";

    /// <summary>安全分类在左侧分类列表里的下标（与 <c>InitializeCategoryList</c> 的顺序一致）。</summary>
    private const int SecurityCategoryIndex = 5;

    private static RuntimeUser User(
        string username,
        string roleId = "role-operator",
        string passwordHash = SomeHash,
        bool enabled = true) => new()
        {
            Username = username,
            DisplayName = username,
            RoleId = roleId,
            PasswordHash = passwordHash,
            IsEnabled = enabled,
        };

    private static HmiProject Project(params RuntimeUser[] users)
    {
        var project = new HmiProject();
        project.Screens.Add(ScreenFactory.Create(project, name: "main"));
        project.SecurityConfig.RuntimeUsers = users.ToList();
        return project;
    }

    /// <summary>在 headless UI 线程上打开面板、切到「安全」分类、跑用例、收尾关窗。</summary>
    private static void WithPanel(HmiProject project, Action<ProjectSettingsView> body) =>
        UiTestHost.Run(() =>
        {
            var view = new ProjectSettingsView(project);
            try
            {
                view.Show();
                UiTestHost.Pump();

                var categories = view.FindControl<ListBox>("CategoryList");
                Assert.True(categories is not null, "面板里找不到分类列表 'CategoryList'（XAML 改名了？）");
                categories!.SelectedIndex = SecurityCategoryIndex;
                UiTestHost.Pump();

                body(view);
            }
            finally
            {
                view.Close();
            }
        });

    private static T Ctl<T>(ProjectSettingsView view, string name) where T : Control
    {
        var found = view.FindControl<T>(name);
        Assert.True(found is not null, $"面板里找不到控件 '{name}'（XAML 改名了？）");
        return found!;
    }

    private static void Click(ProjectSettingsView view, string name)
    {
        Ctl<Button>(view, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump();
    }

    private static ListBox UserList(ProjectSettingsView view) => Ctl<ListBox>(view, "RuntimeUserList");

    /// <summary>草稿里的条目对象（列表项 <c>Tag</c> 就是草稿的 <see cref="RuntimeUser"/> 实例）。</summary>
    private static List<RuntimeUser> DraftRows(ProjectSettingsView view) =>
        UserList(view).Items.OfType<ListBoxItem>().Select(i => (RuntimeUser)i.Tag!).ToList();

    // ══════════════════ 装载 ══════════════════

    [Fact]
    public void SecurityPanel_PopulatesRuntimeUsersFromProject() =>
        WithPanel(Project(User("op"), User("eng", "role-engineer")), view =>
        {
            var rows = DraftRows(view);
            Assert.Equal(2, rows.Count);
            Assert.Equal(new[] { "op", "eng" }, rows.Select(r => r.Username).ToArray());
        });

    /// <summary>
    /// **第一层拷贝**：装载后草稿与模型**不共享对象**。
    /// 直接改草稿条目的字段，模型必须纹丝不动 —— 这正是"编辑期不改模型"的最小判据。
    /// </summary>
    [Fact]
    public void EditingDraft_DoesNotTouchModel_FirstLayerCopy()
    {
        var project = Project(User("op"));

        WithPanel(project, view =>
        {
            var draftRow = Assert.Single(DraftRows(view));

            // 装载即深拷贝：草稿条目不是模型里那一个对象
            Assert.NotSame(draftRow, project.SecurityConfig.RuntimeUsers[0]);

            // 直接改草稿条目的字段 —— 模型必须纹丝不动
            draftRow.Username = "renamed-in-draft";
            draftRow.RoleId = "role-admin";
            draftRow.IsEnabled = false;

            var modelUser = project.SecurityConfig.RuntimeUsers[0];
            Assert.Equal("op", modelUser.Username);
            Assert.Equal("role-operator", modelUser.RoleId);
            Assert.True(modelUser.IsEnabled);
        });
    }

    // ══════════════════ 增删只动草稿 ══════════════════

    [Fact]
    public void AddUser_ChangesDraftOnly()
    {
        var project = Project(User("op"));
        WithPanel(project, view =>
        {
            Click(view, "BtnAddRuntimeUser");

            Assert.Equal(2, DraftRows(view).Count);
            // 模型仍是 1 条 —— 新增只进了草稿
            Assert.Single(project.SecurityConfig.RuntimeUsers);
        });
    }

    [Fact]
    public void RemoveUser_ChangesDraftOnly()
    {
        var project = Project(User("op"), User("eng", "role-engineer"));
        WithPanel(project, view =>
        {
            UserList(view).SelectedIndex = 0;
            UiTestHost.Pump();
            Click(view, "BtnRemoveRuntimeUser");

            Assert.Equal(new[] { "eng" }, DraftRows(view).Select(r => r.Username).ToArray());
            Assert.Equal(2, project.SecurityConfig.RuntimeUsers.Count);
        });
    }

    // ══════════════════ 取消：模型必须原样 ══════════════════

    /// <summary>
    /// 取消语义：整个面板只在 <c>SaveToProject</c> 那一刻写模型，所以"编辑 + 取消"必须什么都不留。
    /// <para>这是**用户可见**的：改错了点取消却留下改动，比没有取消按钮更糟。</para>
    /// </summary>
    [Fact]
    public void Cancel_AfterEditing_LeavesModelUntouched()
    {
        var project = Project(User("op"));
        WithPanel(project, view =>
        {
            // 增、改、删三种编辑都做一遍
            Click(view, "BtnAddRuntimeUser");
            DraftRows(view)[0].Username = "renamed";
            UserList(view).SelectedIndex = 0;
            UiTestHost.Pump();
            Click(view, "BtnRemoveRuntimeUser");

            Click(view, "BtnCancel");

            var modelUser = Assert.Single(project.SecurityConfig.RuntimeUsers);
            Assert.Equal("op", modelUser.Username);
            Assert.Equal(SomeHash, modelUser.PasswordHash);
            Assert.True(modelUser.IsEnabled);
        });
    }

    // ══════════════════ 确定/应用：写模型 + 第二层拷贝 ══════════════════

    /// <summary>
    /// **第二层拷贝**：保存后模型与草稿**不共享引用**。
    /// <para>反例（少这一层）：把草稿列表直接赋给模型 ⇒ 之后接着编辑草稿会继续改到模型，
    /// 于是"确定"之后的编辑绕过了保存语义。断言方式就是"保存后改草稿，模型不变"。</para>
    /// </summary>
    [Fact]
    public void Apply_WritesModel_AndSecondLayerCopyPreventsAliasing()
    {
        var project = Project(User("op"));
        WithPanel(project, view =>
        {
            Click(view, "BtnApply");

            var saved = Assert.Single(project.SecurityConfig.RuntimeUsers);
            Assert.Equal("op", saved.Username);

            // 保存后继续改草稿（含新增）——模型必须不受影响
            var draftRow = Assert.Single(DraftRows(view));
            Assert.NotSame(draftRow, saved);

            draftRow.Username = "changed-after-apply";
            Click(view, "BtnAddRuntimeUser");

            var afterModel = Assert.Single(project.SecurityConfig.RuntimeUsers);
            Assert.Equal("op", afterModel.Username);

            // 直接对照：草稿那一条的改动没有渗进模型
            Assert.NotEqual("changed-after-apply", afterModel.Username);
        });
    }

    /// <summary>「确定」等价于"应用 + 关闭"；有改动时同样写模型。</summary>
    [Fact]
    public void Ok_WithChanges_WritesModelAndCloses()
    {
        var project = Project(User("op"));
        WithPanel(project, view =>
        {
            Click(view, "BtnAddRuntimeUser");
            Click(view, "BtnOk");

            Assert.Equal(2, project.SecurityConfig.RuntimeUsers.Count);
            Assert.False(view.IsVisible);
        });
    }

    /// <summary>
    /// 无改动时「确定」**不重写模型** —— 这不是洁癖：<c>OnOkClicked</c> 以 <c>_hasChanges</c> 为闸门，
    /// 一旦有人把闸门去掉，"打开设置再关掉"就会把面板装载时归一化过的值静默写回工程。
    /// 判据用"模型对象实例是否被替换"来观察（写回一定换实例，因为 <c>SaveRuntimeUsers</c> 会 Clone）。
    /// </summary>
    [Fact]
    public void Ok_WithoutChanges_DoesNotRewriteModel()
    {
        var project = Project(User("op"));
        var before = project.SecurityConfig.RuntimeUsers[0];

        WithPanel(project, view => Click(view, "BtnOk"));

        Assert.Same(before, project.SecurityConfig.RuntimeUsers[0]);
    }

    // ══════════════════ 提示与口令安全 ══════════════════

    /// <summary>
    /// **表非空但一条可用都没有** ⇒ 提示区必须说成"问题"，而不是落到「共 0 个可用账户」的常规文案。
    ///
    /// <para>这条是本轮新写测试**抓出来的真缺陷 #1**：四条规则都不覆盖"账户被停用"，所以全停用的表
    /// 返回零问题、落进"正常"分支，显示成「共 0 个可用账户」（常规色）—— 而这条形态的后果是
    /// **设备端谁都登不上**，阶段 12 已把它判为导出级 Error。方法注释当时就写着"要分开说"，
    /// 但那个分支并不存在。判据同时看**文案**与**颜色**：只改文案不改色仍会被扫一眼当成说明。</para>
    /// </summary>
    [Fact]
    public void TableNotEmptyButNothingUsable_IsShownAsProblem() =>
        WithPanel(Project(User("op", enabled: false), User("eng", "role-engineer", enabled: false)), view =>
        {
            var block = Ctl<TextBlock>(view, "TxtRuntimeUserIssues");
            var text = block.Text ?? "";

            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.Contains("2", text);                                  // 说清表里有几条
            Assert.NotEqual(AppLocalization.Service["project.settings.security.users.table.empty"], text);
            Assert.NotEqual(AppLocalization.Service.Format("project.settings.security.users.table.ok", 0), text);
            Assert.Same(HmiEditor.App.Theme.Warning, block.Foreground);   // 且必须是警示色
        });

    /// <summary>
    /// **装载即正确**：打开一个已配账户的工程，提示区必须说"共 N 个可用账户"。
    ///
    /// <para>这条钉的是本轮抓出来的**真缺陷 #2**：<c>BindRuntimeUsers</c> 在构造期
    /// <c>InitializeRuntimeUserSection()</c> **之后**才跑（那时草稿还是空的），却不重算提示 ⇒
    /// 打开已配账户的工程时提示区一直显示"用户表为空 —— 设备端不校验凭据，按默认角色直接授权" ——
    /// **两句话都与事实相反**。它只在装载时错：随便动一下任一控件就会被后续调用纠正，
    /// 所以人工点两下就再也看不见了。</para>
    /// </summary>
    [Fact]
    public void Load_WithConfiguredUsers_ShowsUsableCount_NotEmptyHint() =>
        WithPanel(Project(User("op"), User("eng", "role-engineer")), view =>
        {
            var block = Ctl<TextBlock>(view, "TxtRuntimeUserIssues");

            Assert.Equal(
                AppLocalization.Service.Format("project.settings.security.users.table.ok", 2),
                block.Text);
            Assert.Same(HmiEditor.App.Theme.TextPrimarySoft, block.Foreground);
        });

    [Fact]
    public void EmptyTable_ShowsEmptyHint() =>
        WithPanel(Project(), view =>
        {
            var issues = Ctl<TextBlock>(view, "TxtRuntimeUserIssues").Text ?? "";
            Assert.False(string.IsNullOrWhiteSpace(issues));
        });

    /// <summary>
    /// 选中已有口令的账户时，**明文框必须为空**（只显示"已设/未设"），
    /// 且不得把哈希回填进输入框 —— 界面里出现哈希本身就等于泄露材料。
    /// </summary>
    [Fact]
    public void SelectingUser_NeverShowsSecretInPlaintextBox() =>
        WithPanel(Project(User("op")), view =>
        {
            UserList(view).SelectedIndex = 0;
            UiTestHost.Pump();

            var box = Ctl<TextBox>(view, "TxtRuntimeUserPassword").Text ?? "";
            Assert.Equal("", box);
            Assert.DoesNotContain(SomeHash, box);
            Assert.False(string.IsNullOrWhiteSpace(Ctl<TextBlock>(view, "TxtRuntimeUserPasswordState").Text));
        });

    [Fact]
    public void NewUser_HasNoDefaultPassword_AndSaysSoInIssues() =>
        WithPanel(Project(User("op")), view =>
        {
            // 新增账户刻意不给默认口令（默认口令几乎不会被改）；问题行会指出这一点
            Click(view, "BtnAddRuntimeUser");

            Assert.Equal("", DraftRows(view).Last().PasswordHash);
            Assert.False(string.IsNullOrWhiteSpace(Ctl<TextBlock>(view, "TxtRuntimeUserIssues").Text));
        });

    /// <summary>
    /// **口令下限按 UTF-8 字节数判，与设备端 <c>strlen</c> 同口径**（登记表 §4 第 20 项）。
    ///
    /// <para>旧实现用 <c>password.Length</c>（UTF-16 码元 ≈ 字符数），于是同一个口令两侧结论不同：
    /// 下限 8 时"电机启动"4 个字 = 12 字节，**设备端放行、编辑器却报"至少 8 位"**。
    /// 本用例两侧都判：4 字（12 字节）必须通过；2 字（6 字节）必须仍被拒 ——
    /// 后者同时证明判的是**字节**，而不是"只要含汉字就放行"。</para>
    /// </summary>
    [Fact]
    public void PasswordMinimum_IsMeasuredInUtf8Bytes_MatchingDevice() =>
        WithPanel(Project(User("op")), view =>
        {
            Ctl<FluentAvalonia.UI.Controls.FANumberBox>(view, "NumPwdMinLen").Value = 8;
            UserList(view).SelectedIndex = 0;
            UiTestHost.Pump();

            var original = DraftRows(view)[0].PasswordHash;

            // 4 个汉字 = 12 字节 ≥ 8 ⇒ 应当接受（按字符数判会误拒）
            Ctl<TextBox>(view, "TxtRuntimeUserPassword").Text = "电机启动";
            Click(view, "BtnSetRuntimeUserPassword");

            var afterAccepted = DraftRows(view)[0].PasswordHash;
            Assert.NotEqual(original, afterAccepted);
            Assert.False(string.IsNullOrEmpty(afterAccepted));

            // 2 个汉字 = 6 字节 < 8 ⇒ 仍须拒绝
            Ctl<TextBox>(view, "TxtRuntimeUserPassword").Text = "电机";
            Click(view, "BtnSetRuntimeUserPassword");

            Assert.Equal(afterAccepted, DraftRows(view)[0].PasswordHash);
        });
}
