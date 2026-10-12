using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`ProjectSettingsView` 的「运行时用户」两个 TextBox 驱动面**（阶段 193）——
/// 把「TextBox 接线普查面」（<see cref="AppTextBoxWiringCensusTests"/>）里
/// <c>ProjectSettingsView.axaml</c> 的那 2 条接线从 `census` 升级成 `driven`。
///
/// <para><b>缺口形状</b>：<c>TxtRuntimeUserName.TextChanged</c> /
/// <c>TxtRuntimeUserDisplayName.TextChanged</c> 在此之前**零判据** —— 它们与
/// `ProjectSettingsRuntimeUserUiTests` 覆盖的"装载 / 增删 / 保存"是两条不同的路
/// （那一族走按钮，这一族走**打字**）。</para>
///
/// <para><b>读的是哪一面（诚实口径）</b>：这两条接线写的是**草稿**里的
/// <see cref="RuntimeUser"/>（面板装载时深拷贝一份，点「应用」才写回工程）——
/// 所以这里读草稿条目；"草稿 → 工程"那一跳由 `ProjectSettingsRuntimeUserUiTests` 看着，
/// 本判据不重复记它的账。两条口径都写进期望值：<c>Username</c> 走 <b>Trim</b>（设备端 strcmp 精确比对），
/// <c>DisplayName</c> <b>不裁剪</b>。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class ProjectSettingsTextBoxWiringTests
{
    private const string SomeHash = "AAECAwQFBgcICQoLDA0ODw==:wNu1hDVTcb2XOXidGgkb4c9GEr6wxjDGAGaH2VWrOLk=";

    /// <summary>安全分类在左侧分类列表里的下标（与 `InitializeCategoryList` 的顺序一致）。</summary>
    private const int SecurityCategoryIndex = 5;

    /// <summary>一条手势：把 <paramref name="Text"/> 写进 <paramref name="Name"/>，断言草稿里的运行时用户读到 <paramref name="Expected"/>。</summary>
    private sealed record Spec(
        string Name,
        string Setting,
        Func<ProjectSettingsView, string> Read,
        string Text,
        string Expected);

    private static readonly Spec[] Specs =
    {
        // 用户名 **Trim**：设备端按 strcmp 精确比对，首尾空格会让"看起来一样的用户名"在设备上对不上
        new("TxtRuntimeUserName", "RuntimeUser.Username",
            v => Draft(v)[0].Username, "  op_renamed  ", "op_renamed"),

        // 显示名 **不裁剪**（只供编辑器辨认，不下发设备）
        new("TxtRuntimeUserDisplayName", "RuntimeUser.DisplayName",
            v => Draft(v)[0].DisplayName ?? "", "  运维管理员  ", "  运维管理员  "),
    };

    /// <summary>
    /// **「没有接线」登记表**：8 个框的 `TextChanged` 是**辅助函数注入**的
    /// （`AttachChangeHandler(TxtAuthor)` 那条线，面 B），由 `TxtAuthor` 那条手势**代表见证**
    /// （见 <see cref="AuthorEdit_MarksDirty_AndOkPersistsIt"/>）——本面不为每个框各写一条手势。
    /// `TxtRuntimeUserPassword` 是**真的**没有接线（由「设置口令」按钮读取）。
    /// </summary>
    private static readonly Dictionary<string, string> WithoutWiring = new(StringComparer.Ordinal)
    {
        ["TxtAuthor"] = "接线经辅助函数 `AttachChangeHandler` 注入（**面 B** 那条线）；由 `AuthorEdit_MarksDirty_AndOkPersistsIt` 代表见证",
        ["TxtProjectVersion"] = "同上（辅助函数注入，代表见证见 `TxtAuthor`）",
        ["TxtCompany"] = "同上（辅助函数注入）",
        ["TxtDescription"] = "同上（工程描述）",
        ["TxtOutputDir"] = "同上（导出目录，另有目录选择器按钮）",
        ["TxtAuditPath"] = "同上（审计路径）",
        ["TxtHistoryPath"] = "同上（历史存储路径）",
        ["TxtDefaultBgColor"] = "同上（默认画布底色）",
        ["TxtDefaultRoleId"] = "同上（默认角色 Id）",
        ["TxtRuntimeUserPassword"] = "口令框：由「设置口令」按钮读取（`BtnSetRuntimeUserPassword`），**真的**没有 TextChanged 接线",
    };

    /// <summary>
    /// **面 B 登记表**：本文件里那条**按名找不到**的接线 —— `AttachChangeHandler&lt;T&gt;` 里的
    /// `txt.TextChanged += (_, _) => _hasChanges = true;`（泛型参数 `txt`，任何 `x:Name` 都对不上）。
    /// ⚠ **阶段 194 起它是"已驱动"的**：调用点传的就是具名控件 ⇒ 用 `TxtAuthor` **代表驱动**它，
    /// 见证选「点确定之后工程真的被保存」（`_hasChanges` 是私有的，而它唯一的消费者就是
    /// `OnOkClicked` 里那句 `if (_hasChanges) ApplyInternal();`）。
    /// </summary>
    private static readonly string[] AnonymousWired = { "txt" };

    private static RuntimeUser User(string username) => new()
    {
        Username = username,
        DisplayName = username,
        RoleId = "role-operator",
        PasswordHash = SomeHash,
        IsEnabled = true,
    };

    private static ListBox UserList(ProjectSettingsView view) =>
        ControlWiringFace.Find<ListBox>(view, "RuntimeUserList");

    /// <summary>草稿里的条目对象（列表项 `Tag` 就是草稿的 <see cref="RuntimeUser"/> 实例）。</summary>
    private static List<RuntimeUser> Draft(ProjectSettingsView view) =>
        UserList(view).Items.OfType<ListBoxItem>().Select(i => (RuntimeUser)i.Tag!).ToList();

    [Fact]
    public void EverySpec_WritesTheRuntimeUserDraftAsDeclared()
    {
        using var sandbox = new DataRootSandbox("projectsettings-textbox");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);
            var project = new HmiProject { Name = "夹具工程" };
            project.Screens.Add(ScreenFactory.Create(project, name: "主画面"));
            project.SecurityConfig.RuntimeUsers = new List<RuntimeUser> { User("op") };

            var view = new ProjectSettingsView(project);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                // 前提自检①：切到「安全」分类（那两个框在那张面板上，够不着就不算驱动面）
                ControlWiringFace.Find<ListBox>(view, "CategoryList").SelectedIndex = SecurityCategoryIndex;
                UiTestHost.Pump(2);

                // 前提自检②：选中第一条运行时用户 ⇒ 详情被**草稿**回填
                Assert.Single(Draft(view));
                UserList(view).SelectedIndex = 0;
                UiTestHost.Pump(2);
                Assert.Equal("op", ControlWiringFace.Find<TextBox>(view, "TxtRuntimeUserName").Text);

                foreach (var spec in Specs)
                {
                    ControlWiringFace.Write(view, spec.Name, spec.Text);
                    var after = spec.Read(view);
                    Assert.True(string.Equals(spec.Expected, after, StringComparison.Ordinal),
                        $"{spec.Name}（{spec.Setting}）驱动 '{spec.Text}' 后读到 '{after}'，期望 '{spec.Expected}'");
                }

                // 反面见证：改的是**草稿**，`project` 里那条还没动（点「应用」才写回 —— 那一跳另有判据）
                Assert.Equal("op", project.SecurityConfig.RuntimeUsers[0].Username);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **面 B 那条接线（`AttachChangeHandler` 里的 `txt.TextChanged`）的驱动面**（阶段 194）：
    /// 它按名找不到（泛型参数），但**调用点传的是具名控件** ⇒ 用 `TxtAuthor` 代表驱动它。
    /// 见证选「点『确定』之后工程真的被保存」——`_hasChanges` 是私有的，而它**唯一**的消费者
    /// 就是 `OnOkClicked` 里那句 `if (_hasChanges) ApplyInternal();`：
    /// 没有这条接线 ⇒ 点确定**什么都不写**（工程停在旧值）。
    /// </summary>
    [Fact]
    public void AuthorEdit_MarksDirty_AndOkPersistsIt()
    {
        using var sandbox = new DataRootSandbox("projectsettings-author");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);
            var project = new HmiProject { Name = "夹具工程", Author = "旧作者" };
            project.Screens.Add(ScreenFactory.Create(project, name: "主画面"));

            var view = new ProjectSettingsView(project);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                // 前提自检：`TxtAuthor` 在第一张面板（工程信息）上，且回填的是**工程里的**值
                Assert.True(ControlWiringFace.Find<Panel>(view, "PanelBasicInfo").IsVisible);
                Assert.Equal("旧作者", ControlWiringFace.Find<TextBox>(view, "TxtAuthor").Text);

                ControlWiringFace.Write(view, "TxtAuthor", "  新作者  ");

                // 保存前不落盘（这条接线只标脏，真正的写入在 `SaveToProject`）
                Assert.Equal("旧作者", project.Author);

                // 点「确定」：`_hasChanges` 为真才 `ApplyInternal()` ⇒ `SaveToProject()`（顺手 Trim）
                ControlWiringFace.Find<Button>(view, "BtnOk")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                UiTestHost.Pump(2);

                Assert.Equal("新作者", project.Author);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **单文件双向差集**：两个运行时用户框有**直接**接线与驱动条目；
    /// 其余 10 个（含辅助函数注入的 9 个 ＋ 真没接线的口令框）在登记表里；
    /// 辅助函数注入的那条线（面 B 的 `txt`）单独认账。
    /// </summary>
    [Fact]
    public void EveryTextBoxAndWiring_IsAccountedFor()
    {
        ControlWiringFace.AssertFullyDriven(
            "ProjectSettingsView.axaml", Specs.Select(s => s.Name).ToArray(), scanFloor: 2,
            WithoutWiring, AnonymousWired);
    }
}
