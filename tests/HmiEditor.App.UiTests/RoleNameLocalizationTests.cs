using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 角色名/角色描述的**本地化接线**判据（闭环登记 #50）。
///
/// <para><b>病根（阶段 64 由键可达性判据暴露）</b>：字典里 <c>role.operator</c> /
/// <c>role.engineer</c> / <c>role.administrator</c> 三条键**没有任何消费点** ——
/// 角色名走的是模型硬编码（<c>UserRole.BuiltInOperator</c> 的 <c>Name = "Operator"</c>），
/// 而<b>角色名与角色描述都是只读展示</b>（<c>RoleNameBox</c> 是 <c>IsReadOnly="True"</c> 的 TextBox、
/// <c>RoleDescText</c> 是只读 TextBlock，<c>OnSaveRole</c> 只写 Level 与 Permissions）
/// ⇒ 它们本该走字典，却把界面语言交给了模型里那几个英文字面量。</para>
///
/// <para><b>阶段 66 的读数（改之前 → 改之后）</b>：两个角色下拉的 <c>ItemsSource</c> 直接装
/// <see cref="UserRole"/> 对象且没有 <c>ItemTemplate</c> ⇒ Avalonia 只能渲染 <c>ToString()</c>，
/// 从视觉树里实测到的原文是 <c>"HmiEditor.Core.Models.User.UserRole"</c>（**类型全名**，
/// 不是"英文名"—— 见 <see cref="RoleDisplaySurface_IsReported"/>）；角色列表是
/// <c>Administrator (Lv.15)</c> + 中文描述（中英混排）。接线后是
/// <c>ComboBoxItem(Content="操作员 (Lv.3)", Tag=role-operator)</c> 与
/// <c>管理员 (Lv.15) | 管理员 — 完全权限</c>。</para>
///
/// <para><b>本类管什么</b>：</para>
/// <list type="number">
///   <item>「预置角色 Id ↔ 键」的**同源双向**：<see cref="UserRole.BuiltInAll"/> 的每个 Id 都要能推出键，
///     映射表里也不许有孤儿（谁加角色不给键、谁留孤儿键，都红）。</item>
///   <item>字典里 <c>role.*</c> 家族与文案表**双向一致** —— 这条同时钉住「不许再出现没有角色的键」，
///     <c>role.maintenance</c> 就是被它清退的那一个。</item>
///   <item>显示值**确实来自字典**（== 字典值、且 ≠ 键名本身）且**不是**模型里的英文原名
///     （!= <c>UserRole.Name</c>）。三条合起来才证伪「本地化其实没生效」：只断言"等于字典值"
///     会自指（<c>LocalizationService</c> 对不存在的键**原样返回键名**），只断言"不等于英文名"
///     会被"随便换个字符串"蒙过。</item>
///   <item>端到端（<see cref="UserManagerView"/>）：两个角色下拉的条目必须是带
///     <c>Tag = 角色 Id</c> 的 <see cref="ComboBoxItem"/>、文本是本地化名，且**选中框渲染出来的
///     是那句话而不是类型名**；角色列表三项是「本地化名 (Lv.n)」+ 本地化描述。</item>
///   <item><b>选中即回填</b>（登记 #53）：在列表里点一下，右侧详情必须跟着变 ——
///     这一条是**接线**判据，不是本地化判据（见下）。</item>
/// </list>
///
/// <para><b>为什么第 5 条在这里</b>（登记 #53，阶段 66 顺带发现）：本类构造
/// <see cref="UserManagerView"/> 时实测 <c>RoleNameBox.Text</c> 在选中角色后**仍是空串** ——
/// 追下去发现 <c>UserList</c>/<c>RoleList</c> 的 <c>SelectionChanged</c> **从首个提交
/// （<c>1fc6701</c>, 2026-07-30）起就从未接线**（XAML 无属性、代码无 <c>+=</c>、
/// <c>ChromeWindow</c> 也不做任何自动接线）⇒ 右侧详情区永远空白，而
/// <c>_selectedUser</c>/<c>_selectedRole</c> 全靠它赋值 ⇒ 改名/改角色/删除/启停/重置口令/解锁
/// 与整块角色管理（等级 + 9 个权限勾选 + 保存）全部**静默失效**。
/// 它同时是 #50 的**前提**：不接线，角色详情那两处本地化根本观测不到。</para>
///
/// <para><b>刻意不动的地方</b>（避免下一个人以为漏了）：生成头 <c>HMI_SEC_ROLES_INIT</c> 的 name 列、
/// 以及 <c>AuthenticationService</c> 的调试日志，都继续用模型的 <c>Name</c> ——
/// 前者是产物契约（不随 UI 语言变，改它要动 golden），后者不是 UI。</para>
/// </summary>
public class RoleNameLocalizationTests
{
    private readonly ITestOutputHelper _out;

    public RoleNameLocalizationTests(ITestOutputHelper output) => _out = output;

    // ══════════════════════════════════════════════════════════════════
    //  ① 读数（不是断言）

    /// <summary>
    /// **读数**（不是断言）：把「角色名/描述在各处到底显示成什么」摊开。
    ///
    /// <para>阶段 66 的两档对拍：接线前是「下拉条目类型 = <c>UserRole</c>、选中框文本 = 类型全名、
    /// 详情区空串」，接线后是「条目 = <c>ComboBoxItem</c>、文本 = 字典值」。</para>
    /// </summary>
    [Fact]
    public void RoleDisplaySurface_IsReported()
    {
        var readings = new List<string>();

        UiTestHost.Run(() =>
        {
            var auth = new AuthenticationService();
            var view = new UserManagerView(auth);
            try
            {
                view.Show();
                UiTestHost.Pump();

                readings.Add($"语言              {AppLocalization.Service.CurrentLanguage}");
                readings.Add("── 模型（预置角色表，唯一清单 UserRole.BuiltInAll）──");
                foreach (var role in UserRole.BuiltInAll)
                    readings.Add($"    {role.Id,-16} Name={role.Name,-14} Level={role.Level,2}  Desc=\"{role.Description}\"");

                readings.Add("── 字典里的 role.* ──");
                foreach (var lang in new[] { "zh-CN", "en-US" })
                    foreach (var key in KeysMatching(lang, "role."))
                        readings.Add($"    [{lang}] {key,-24} = \"{AppLocalization.Service.GetString(key, lang)}\"");

                readings.Add("── 角色下拉（UserManagerView）──");
                var newCombo = Require<ComboBox>(view, "NewRoleCombo");
                readings.Add($"    NewRoleCombo     条目 {newCombo.Items.Count} 个：{DescribeItems(newCombo)}");
                readings.Add($"    NewRoleCombo     选中框实际渲染：{RenderSelected(newCombo)}");

                // 详情区的角色下拉要先选中一个用户才会被回填（ShowUserDetail 负责）
                Require<ListBox>(view, "UserList").SelectedIndex = 0;
                UiTestHost.Pump();
                var detailCombo = Require<ComboBox>(view, "DetailRoleCombo");
                readings.Add($"    DetailRoleCombo  条目 {detailCombo.Items.Count} 个：{DescribeItems(detailCombo)}");
                readings.Add($"    DetailRoleCombo  选中框实际渲染：{RenderSelected(detailCombo)}");

                // 角色列表与详情区同理：先选中一个角色
                var roleList = Require<ListBox>(view, "RoleList");
                roleList.SelectedIndex = 0;
                UiTestHost.Pump();
                readings.Add("── 角色列表（左栏）──");
                foreach (var item in roleList.Items)
                    readings.Add($"    {RenderItem(item)}");

                readings.Add("── 角色详情（右栏，只读展示；已选中左栏第一项）──");
                readings.Add($"    名称 RoleNameBox  = \"{Require<TextBox>(view, "RoleNameBox").Text}\"");
                readings.Add($"    描述 RoleDescText = \"{Require<TextBlock>(view, "RoleDescText").Text}\"");

                readings.Add("── 项目设置：运行时用户表的角色下拉 ──");
                var settings = new ProjectSettingsView(NewProject());
                try
                {
                    settings.Show();
                    UiTestHost.Pump();
                    var cmb = settings.FindControl<ComboBox>("CmbRuntimeUserRole");
                    Assert.True(cmb is not null, "工程设置里找不到 'CmbRuntimeUserRole'（XAML 改名了？）");
                    readings.Add($"    CmbRuntimeUserRole 条目 {cmb!.Items.Count} 个：{DescribeItems(cmb)}");
                }
                finally
                {
                    settings.Close();
                }

                readings.Add("── 键可达性差异表里的 role.* ──");
                var kept = LocalizationKeyReachabilityGuardTests.KeptUnreachableKeys
                    .Where(k => k.StartsWith("role.", StringComparison.Ordinal))
                    .OrderBy(k => k, StringComparer.Ordinal).ToList();
                readings.Add($"    差异表共 {LocalizationKeyReachabilityGuardTests.KeptUnreachableKeys.Count} 条，"
                             + $"其中 role.* {kept.Count} 条：{(kept.Count == 0 ? "(无)" : string.Join(" · ", kept))}");
            }
            finally
            {
                view.Close();
            }
        });

        JudgeReadings.Write(_out, "角色显示面", readings);
        Assert.NotEmpty(readings);
    }

    // ══════════════════════════════════════════════════════════════════
    //  ② 同源双向：角色表 ↔ 文案表

    /// <summary>
    /// <see cref="UserRole.BuiltInAll"/> 与 <see cref="RoleTextKeys"/> 的映射表**必须一对一**。
    /// 两个方向都要断言，因为它们防的是两件事：给角色表加了行却忘了给键（界面回落成英文名/类型名）、
    /// 留了一条指向不存在角色的键（键家族从此有毒）。
    /// </summary>
    [Fact]
    public void BuiltInRoles_AndRoleTextMap_AreOneToOne()
    {
        var roleIds = UserRole.BuiltInAll.Select(r => r.Id).ToList();
        var mappedIds = RoleTextKeys.MappedRoleIds;

        var missing = roleIds.Except(mappedIds, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0,
            "这些预置角色在 RoleTextKeys 的映射表里没有键 ⇒ 界面上会显示模型里的英文名（或类型名）：\n   - "
            + string.Join("\n   - ", missing)
            + "\n   给 UserRole.BuiltInAll 加角色时，必须同时给 RoleTextKeys.Map 加一行。");

        var orphan = mappedIds.Except(roleIds, StringComparer.Ordinal).ToList();
        Assert.True(orphan.Count == 0,
            "RoleTextKeys 的映射表里有**孤儿**（指向不存在的角色）：\n   - "
            + string.Join("\n   - ", orphan)
            + "\n   孤儿行永远推不出任何界面，只会让键家族与角色表对不上。");

        foreach (var role in UserRole.BuiltInAll)
        {
            Assert.True(RoleTextKeys.NameKey(role.Id) is not null, $"{role.Id} 推不出显示名键");
            Assert.True(RoleTextKeys.DescriptionKey(role.Id) is not null, $"{role.Id} 推不出描述键");
        }

        // 表外 Id 必须推不出键（否则「自定义角色不翻译」这条语义就没了）
        Assert.Null(RoleTextKeys.NameKey("role-not-a-builtin"));
        Assert.Null(RoleTextKeys.DescriptionKey("role-not-a-builtin"));
    }

    // ══════════════════════════════════════════════════════════════════
    //  ③ 键家族闭合：字典 role.* == 文案表（两本、双向）

    /// <summary>
    /// 两本字典里 <c>role.*</c> 的键集合与文案表**双向相等**。
    ///
    /// <para>这条同时是「<c>role.maintenance</c> 不许回来」的钉子和「新增角色文案必须先有角色」的门：
    /// 阶段 66 清退 <c>role.maintenance</c> 的理由正是**没有第 4 个预置角色**
    /// （<c>UserRole.BuiltInAll</c> 三个、固件角色表同样三条，见
    /// <c>firmware/t113/tests/fixtures/generated_security.h:54-57</c>）——
    /// 将来真加了第 4 个角色，本判据会红并逼着把键补回来；反过来，没有角色却先加键也红。</para>
    /// </summary>
    [Fact]
    public void RoleKeyFamily_InBothDictionaries_MatchesRoleTextMap()
    {
        var expected = RoleTextKeys.AllKeys.ToHashSet(StringComparer.Ordinal);

        foreach (var lang in new[] { "zh-CN", "en-US" })
        {
            var inDict = KeysMatching(lang, "role.").ToHashSet(StringComparer.Ordinal);

            var nobodyReads = inDict.Except(expected).OrderBy(k => k, StringComparer.Ordinal).ToList();
            Assert.True(nobodyReads.Count == 0,
                $"[{lang}] 字典里有这些 role.* 键，但文案表里没有对应角色 ⇒ 它们不可能被任何界面取到：\n   - "
                + string.Join("\n   - ", nobodyReads)
                + "\n   两条路：接到 RoleTextKeys 上（若角色真的存在），或按陈旧残留清退"
                + "（口径见 docs/2026-09-17-open-items-register.md §21.3 与阶段 66 的 role.maintenance）。");

            var missing = expected.Except(inDict).OrderBy(k => k, StringComparer.Ordinal).ToList();
            Assert.True(missing.Count == 0,
                $"[{lang}] 文案表用到的这些键在字典里不存在 ⇒ LocalizationService 会**原样返回键名**，界面显示 role.xxx：\n   - "
                + string.Join("\n   - ", missing));
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  ④ 显示值来自字典，且不是模型里的英文名

    /// <summary>
    /// 三条一起断言，缺一条就会被蒙过：<c>== 字典值</c>、<c>≠ 键名</c>（<c>LocalizationService</c>
    /// 对不存在的键原样返回键名）、<c>≠ 模型里的英文原名</c>。
    /// </summary>
    [Fact]
    public void DisplayedRoleText_ComesFromDictionary_NotFromModelName()
    {
        var readings = new List<string>();

        foreach (var role in UserRole.BuiltInAll)
        {
            var nameKey = RoleTextKeys.NameKey(role.Id)!;
            var descKey = RoleTextKeys.DescriptionKey(role.Id)!;
            var name = RoleTextKeys.DisplayName(role);
            var desc = RoleTextKeys.DisplayDescription(role);

            readings.Add($"{role.Id,-16} 名：\"{name}\"（模型 Name=\"{role.Name}\"，键 {nameKey}）");
            readings.Add($"{role.Id,-16} 述：\"{desc}\"（键 {descKey}）");

            Assert.Equal(AppLocalization.Service[nameKey], name);
            Assert.NotEqual(nameKey, name);                       // 等于键名 ⇒ 字典里根本没这条键
            Assert.NotEqual(role.Name, name);                     // 还是模型里的英文名 ⇒ 根本没走字典
            Assert.False(string.IsNullOrWhiteSpace(name), $"{role.Id} 的显示名为空");

            Assert.Equal(AppLocalization.Service[descKey], desc);
            Assert.NotEqual(descKey, desc);
            Assert.False(string.IsNullOrWhiteSpace(desc),
                $"{role.Id} 的描述为空（模型里的中文字面量已搬进字典，忘了加键就会落到这里）");
        }

        JudgeReadings.Write(_out, "角色显示值（字典 vs 模型）", readings);
    }

    // ══════════════════════════════════════════════════════════════════
    //  ⑤ 端到端：三个下拉 + 角色列表

    [Fact]
    public void RoleSurfaces_RenderLocalizedText()
    {
        UiTestHost.Run(() =>
        {
            var view = new UserManagerView(new AuthenticationService());
            try
            {
                view.Show();
                UiTestHost.Pump();

                // 详情区的角色下拉要**先选中一个用户**才会被回填（ShowUserDetail 负责）；
                // 没选中 ⇒ SelectedItem 为 null ⇒ 视觉树里连一个 TextBlock 都没有，
                // 「选中框渲染成什么」这条就无从读起。这一句同时是 #53 的前置：不接线就选不亮。
                Require<ListBox>(view, "UserList").SelectedIndex = 0;
                UiTestHost.Pump();
                Assert.True(Require<ComboBox>(view, "DetailRoleCombo").SelectedItem is not null,
                    "选中用户后详情区的角色下拉仍是空的 —— UserList.SelectionChanged 没接线（登记 #53）");

                var expected = UserRole.BuiltInAll
                    .ToDictionary(r => r.Id, RoleTextKeys.DisplayNameWithLevel, StringComparer.Ordinal);

                foreach (var name in new[] { "NewRoleCombo", "DetailRoleCombo" })
                {
                    var combo = Require<ComboBox>(view, name);
                    var items = combo.Items.OfType<ComboBoxItem>().ToList();

                    Assert.True(items.Count == UserRole.BuiltInAll.Length,
                        $"{name} 的条目不是 {UserRole.BuiltInAll.Length} 个 ComboBoxItem（实测 {items.Count} 个）—— "
                        + "是不是又把 UserRole 对象直接塞进 ItemsSource 了？那样 Avalonia 只能渲染 ToString()（类型全名）。");

                    foreach (var item in items)
                    {
                        var id = item.Tag as string;
                        Assert.True(id is not null && expected.ContainsKey(id),
                            $"{name} 有条目的 Tag={item.Tag} 不是预置角色 Id");
                        Assert.Equal(expected[id!], item.Content as string);
                    }

                    // 选中框是**真的渲染出来**的那句话（不是类型名）
                    var rendered = VisibleText(combo);
                    Assert.False(rendered.Contains(nameof(UserRole), StringComparison.Ordinal),
                        $"{name} 的选中框渲染成了类型名：\"{rendered}\"");
                    Assert.Contains(rendered, expected.Values);
                }

                var roleList = Require<ListBox>(view, "RoleList");
                var texts = roleList.Items.OfType<ListBoxItem>()
                    .Select(i => i.Content).OfType<Panel>()
                    .Select(p => string.Join(" | ", p.Children.OfType<TextBlock>().Select(t => t.Text)))
                    .ToList();

                Assert.Equal(UserRole.BuiltInAll.Length, texts.Count);
                foreach (var role in UserRole.BuiltInAll)
                {
                    Assert.Contains(texts, t => t.StartsWith(RoleTextKeys.DisplayNameWithLevel(role), StringComparison.Ordinal));
                    Assert.Contains(texts, t => t.Contains(RoleTextKeys.DisplayDescription(role), StringComparison.Ordinal));
                    // 模型里的英文原名不许出现在这一栏（它就是被修掉的那个症状）
                    Assert.DoesNotContain(texts, t => t.StartsWith(role.Name + " (", StringComparison.Ordinal));
                }
            }
            finally
            {
                view.Close();
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════
    //  ⑥ 选中即回填（登记 #53）

    /// <summary>
    /// 点列表 ⇒ 右侧详情跟着变。<b>这条是接线判据</b>：两个方向都测 ——
    /// ① 选中后详情**等于该条目对应的值**；② **换一个选中**，详情**跟着换**
    /// （只测 ① 的话，"构造期就预填了某个值"也能满足它）。
    /// </summary>
    [Fact]
    public void SelectingUserOrRole_FillsTheDetailPane()
    {
        var readings = new List<string>();

        UiTestHost.Run(() =>
        {
            var auth = new AuthenticationService();

            // 默认只有一条预置用户（admin）⇒「换一个选中」得先有第二条才换得动。
            // 直接走服务层造（不点 UI），这样本用例只管「选中 → 回填」这一条链。
            auth.CreateUser("tester", "测试员", "Abc12345", UserRole.BuiltInEngineer.Id);

            var view = new UserManagerView(auth);
            try
            {
                view.Show();
                UiTestHost.Pump();

                // ── 角色表：选 0 ⇒ 换 1 ⇒ 详情必须跟着换 ──
                var roleList = Require<ListBox>(view, "RoleList");
                var nameBox = Require<TextBox>(view, "RoleNameBox");
                var descText = Require<TextBlock>(view, "RoleDescText");

                var pickedNames = new List<string>();
                foreach (var idx in new[] { 0, 1 })
                {
                    roleList.SelectedIndex = idx;
                    UiTestHost.Pump();

                    var role = ((ListBoxItem)roleList.Items[idx]!).Tag as UserRole;
                    Assert.True(role is not null, $"RoleList 第 {idx} 项的 Tag 不是 UserRole（重建列表时漏了 Tag ？）");

                    // 先判「填没填」，再判「填得对不对」—— 反了的话，没接线时只会报一句
                    // 泛泛的 "Strings differ"，看不出根因（N8 的报错点名了 #53，N7 没有）。
                    Assert.False(string.IsNullOrWhiteSpace(nameBox.Text),
                        "选中角色后 RoleNameBox 仍是空 —— RoleList.SelectionChanged 没接线（登记 #53）");
                    Assert.Equal(RoleTextKeys.DisplayName(role!), nameBox.Text);
                    Assert.Equal(RoleTextKeys.DisplayDescription(role!), descText.Text);
                    pickedNames.Add(nameBox.Text!);
                }
                Assert.NotEqual(pickedNames[0], pickedNames[1]);
                readings.Add($"角色详情回填：{pickedNames[0]} → {pickedNames[1]}（随选择变化）");

                // ── 用户表：选 0 ⇒ 换 1 ⇒ 详情必须跟着换 ──
                var userList = Require<ListBox>(view, "UserList");
                var detailName = Require<TextBlock>(view, "DetailUsername");
                var detailCombo = Require<ComboBox>(view, "DetailRoleCombo");

                var usernames = new List<string>();
                foreach (var idx in new[] { 0, 1 })
                {
                    userList.SelectedIndex = idx;
                    UiTestHost.Pump();

                    var user = ((ListBoxItem)userList.Items[idx]!).Tag as UserProfile;
                    Assert.True(user is not null, $"UserList 第 {idx} 项的 Tag 不是 UserProfile（重建列表时漏了 Tag ？）");

                    Assert.False(string.IsNullOrWhiteSpace(detailName.Text),
                        "选中用户后 DetailUsername 仍是空 —— UserList.SelectionChanged 没接线（登记 #53）");
                    Assert.Equal(user!.Username, detailName.Text);
                    Assert.Equal(user.RoleId, (detailCombo.SelectedItem as ComboBoxItem)?.Tag as string);
                    usernames.Add(detailName.Text!);
                }
                Assert.NotEqual(usernames[0], usernames[1]);
                readings.Add($"用户详情回填：{usernames[0]} → {usernames[1]}（角色下拉按 Id 选中）");
            }
            finally
            {
                view.Close();
            }
        });

        JudgeReadings.Write(_out, "选中即回填（#53）", readings);
    }

    // ══════════════════════════════════════════════════════════════════
    //  读数/断言共用的小工具

    private static HmiProject NewProject()
    {
        var project = new HmiProject();
        project.Screens.Add(ScreenFactory.Create(project, name: "main"));
        return project;
    }

    /// <summary>字典里某个前缀下的键（读**字典文件本身**，不走本地化服务 —— 服务的语言是可切的）。</summary>
    private static IEnumerable<string> KeysMatching(string language, string prefix)
    {
        var path = Path.Combine(SourceScanner.RepoRoot,
            "src", "HmiEditor.Core", "Resources", $"Strings.{language}.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateObject()
            .Select(p => p.Name)
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>按名字取控件，取不到就带上下文失败（而不是让后面 NRE 出一个看不出原因的错）。</summary>
    private static T Require<T>(Control root, string name) where T : Control
    {
        var found = root.FindControl<T>(name);
        Assert.True(found is not null, $"找不到控件 '{name}'（XAML 改名了？）");
        return found!;
    }

    private static string DescribeItems(ComboBox combo)
        => "[" + string.Join(" | ", combo.Items.Cast<object?>().Select(RenderItem)) + "]";

    /// <summary>
    /// 一个条目**实际会被渲染成什么**：是控件就走它的视觉树取文本（下拉选中框走的也是这条路），
    /// 不是控件就只能是 <c>ToString()</c>（Avalonia 找不到 DataTemplate 时的兜底）。
    /// </summary>
    private static string RenderItem(object? item) => item switch
    {
        null => "(null)",
        ComboBoxItem box => $"ComboBoxItem(Content=\"{box.Content}\", Tag={box.Tag})",
        // 列表项在没被布局 realize 之前视觉树是空的 —— 内容只能从它自己的 Content 上读
        ListBoxItem { Content: Panel panel }
            => "«" + string.Join(" | ", panel.Children.OfType<TextBlock>().Select(t => t.Text)) + "»",
        Control ctl => $"\"{VisibleText(ctl)}\"",
        _ => $"\"{item}\"（非控件 ⇒ 渲染 ToString()）",
    };

    /// <summary>下拉「选中框」实际渲染出的文本（要把视觉树跑起来才看得见）。</summary>
    private static string RenderSelected(ComboBox combo)
    {
        var text = VisibleText(combo);
        return string.IsNullOrEmpty(text) ? "(视觉树里没有 TextBlock)" : $"\"{text}\"";
    }

    private static string VisibleText(Control control)
        => string.Join("", control.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
}
