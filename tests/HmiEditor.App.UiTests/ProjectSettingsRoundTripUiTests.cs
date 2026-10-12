using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// `ProjectSettingsView` 的**配置往返保真**测试 —— 把行为级覆盖从「运行时用户表」扩展到其余 5 个分类
/// （基本 / 显示 / 通讯 / 运行时 / 导出）。对应 `docs/2026-09-17-open-items-register.md` §4 第 17 项。
///
/// <para><b>为什么用"工程 JSON 逐字节不变"当判据，而不是逐字段断言：</b>这个面板有约 50 个"一个控件对一个字段"
/// 的绑定，逐字段手写断言必然漏掉几个 —— 而漏掉的那个正好就是将来出问题的地方。
/// 把整个工程序列化后比对，**任何**字段的静默改写都会红，包括以后新加的字段。
/// 失败时按行给出差异，直接点名是哪个字段。</para>
///
/// <para><b>它守的是什么缺陷：</b>本项目已经吃过一次 —— `SaveToProject` 里那段注释记载着
/// *"曾经的实现是无条件 foreach 覆盖，导致每次打开工程设置点确定都会静默重置用户逐屏手工调过的尺寸"*。
/// 同类失效还有"某字段装载时读了、保存时没写"（用户改了没反应）与"保存时归一化成了默认值"
/// （用户没改却被改）。三者都**编译通过、界面正常**，只有"打开→应用→比对"能看出来。</para>
/// </summary>
public class ProjectSettingsRoundTripUiTests
{
    private const int SecurityCategoryIndex = 5;

    // ────────────────────────── 夹具 ──────────────────────────

    /// <summary>
    /// 一个**每个配置字段都取非默认值**的工程。
    /// <para>都取**合法**取值（枚举成员名 / 真实屏幕 Id），否则测的就不是"往返保真"而是"非法值归一"。</para>
    /// </summary>
    private static HmiProject RichProject()
    {
        var project = new HmiProject
        {
            Description = "描述-非默认",
            Author = "作者-非默认",
            Company = "公司-非默认",
            ProjectVersion = "2.3.1",
            LvglVersion = "9.4",
            // HardwareTargetId 刻意留空：目标清单来自 firmware/*/firmware.json，
            // 测试宿主目录下没有那些文件 ⇒ 下拉为空 ⇒ 任何非空 Id 都会被归一成 null。
            // 那是"清单缺失时的回退"，不是往返缺陷（见 HardwareTarget 专项用例）。
        };

        var main = ScreenFactory.Create(project, name: "main");
        var spare = ScreenFactory.Create(project, name: "spare");
        project.Screens.Add(main);
        project.Screens.Add(spare);

        project.ScreenDefaultsConfig.DefaultWidth = 1280;
        project.ScreenDefaultsConfig.DefaultHeight = 800;
        project.ScreenDefaultsConfig.DefaultColorDepth = "ARGB8888";
        project.ScreenDefaultsConfig.DefaultOrientation = ScreenOrientation.Portrait;
        project.ScreenDefaultsConfig.DefaultAdaptiveMode = ScreenAdaptiveMode.Fill;
        project.ScreenDefaultsConfig.DefaultBgColor = "#123456";
        project.ScreenDefaultsConfig.DpiScale = 1.5;

        project.VariableManager.DefaultScanIntervalMs = 250;
        project.VariableManager.DefaultTimeoutMs = 5000;
        project.VariableManager.MaxRetryCount = 5;
        project.VariableManager.RetryBackoffMs = 500;
        project.VariableManager.EnableWriteCoalescing = false;
        project.VariableManager.ConnectionRecoveryMode = ConnectionRecoveryMode.FailFast;

        project.RuntimeConfig.ScanIntervalMs = 200;
        project.RuntimeConfig.LogLevel = LogLevel.Error;
        project.RuntimeConfig.HistoryStoragePath = "/data/history";
        project.RuntimeConfig.WatchdogTimeoutMs = 9000;
        project.RuntimeConfig.HeartbeatIntervalMs = 2000;
        project.RuntimeConfig.EnableCrashDump = false;
        project.RuntimeConfig.CrashDumpSector = 7;
        project.RuntimeConfig.MaxRuntimeTasks = 8;
        project.RuntimeConfig.EnableHmiReboot = false;
        project.RuntimeConfig.BootScreenId = spare.Id;

        project.ExportConfig.TargetLanguage = TargetLanguage.Cpp;
        project.ExportConfig.GenerateBuildSystem = BuildSystem.Makefile;
        project.ExportConfig.OutputDirectory = "./out";
        project.ExportConfig.CodeStyle = CodeStyle.Allman;
        project.ExportConfig.ResourceNaming = ResourceNaming.PascalCase;
        project.ExportConfig.CodeGenOptimization = CodeGenOptimization.Readability;
        project.ExportConfig.GenerateDebugSymbols = true;
        project.ExportConfig.IncludeComments = false;
        project.ExportConfig.MergeResources = false;
        project.ExportConfig.IncludeLgplDisclaimer = false;

        project.SecurityConfig.OperationMode = OperationMode.Maintenance;
        project.SecurityConfig.SessionTimeoutMin = 45;
        project.SecurityConfig.EnableAuditLog = false;
        project.SecurityConfig.AuditLogStoragePath = "/var/log/hmi.jsonl";
        project.SecurityConfig.PasswordMinLength = 12;
        project.SecurityConfig.MaxLoginAttempts = 3;
        project.SecurityConfig.LockoutDurationMin = 20;
        project.SecurityConfig.AutoLogoutEnabled = false;
        project.SecurityConfig.LockScreenId = main.Id;
        project.SecurityConfig.EnableTamperDetection = true;
        project.SecurityConfig.DefaultRoleId = "role-admin";
        project.SecurityConfig.RuntimeUsers = new List<RuntimeUser>
        {
            new()
            {
                Username = "op",
                DisplayName = "Operator",
                RoleId = "role-operator",
                PasswordHash = "AAECAwQFBgcICQoLDA0ODw==:wNu1hDVTcb2XOXidGgkb4c9GEr6wxjDGAGaH2VWrOLk=",
                IsEnabled = true,
            }
        };

        return project;
    }

    private static void WithPanel(HmiProject project, Action<ProjectSettingsView> body) =>
        UiTestHost.Run(() =>
        {
            var view = new ProjectSettingsView(project);
            try
            {
                view.Show();
                UiTestHost.Pump();
                view.FindControl<ListBox>("CategoryList")!.SelectedIndex = SecurityCategoryIndex;
                UiTestHost.Pump();
                body(view);
            }
            finally
            {
                view.Close();
            }
        });

    private static void Click(ProjectSettingsView view, string name) =>
        view.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>按行给出 JSON 差异，失败时直接点名改动的字段（整串比对读不出东西）。</summary>
    private static string LineDiff(string before, string after)
    {
        var a = before.Split('\n').Select(l => l.Trim()).ToList();
        var b = after.Split('\n').Select(l => l.Trim()).ToList();
        var removed = a.Except(b).Select(l => $"  仅改动前有: {l}");
        var added = b.Except(a).Select(l => $"  仅改动后有: {l}");
        return string.Join("\n", removed.Concat(added));
    }

    // ────────────────────────── 核心不变量 ──────────────────────────

    /// <summary>
    /// **打开面板 → 点「应用」→ 工程必须逐字节不变。**
    /// <para>这是本文件的主判据：任何"装载读了保存没写""保存时归一化成默认值"都会在这里现形。</para>
    /// </summary>
    [Fact]
    public void Apply_WithoutEditing_PreservesEveryConfigField()
    {
        var project = RichProject();
        var before = ProjectSerializer.Serialize(project);

        WithPanel(project, view => Click(view, "BtnApply"));

        var after = ProjectSerializer.Serialize(project);
        Assert.True(before == after,
            "打开工程设置并点「应用」后，工程内容发生了变化（应逐字段原样）：\n" + LineDiff(before, after));
    }

    /// <summary>
    /// **改一个字段 → 应用 → 只有那一个字段变。** 证明「应用」真的把改动写下去，
    /// 同时其余字段不跟着漂移（与上一条互补：上一条证明"没改的不动"，这条证明"改了的动了"）。
    /// </summary>
    [Fact]
    public void Apply_AfterEditingOneField_ChangesOnlyThatField()
    {
        var project = RichProject();
        // 与**同一个实例**改动前的序列化比对：屏幕 Id / 时间戳每次新建都不同，
        // 拿"新建的等价工程"来比会把它们全算成差异（首版就是这么写的，红了才知道）。
        var before = ProjectSerializer.Serialize(project);

        WithPanel(project, view =>
        {
            view.FindControl<FANumberBox>("NumCommTimeout")!.Value = 7777;
            Click(view, "BtnApply");
        });

        Assert.Equal(7777, project.VariableManager.DefaultTimeoutMs);

        var changed = LineDiff(before, ProjectSerializer.Serialize(project));
        var lines = changed.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.True(lines.Length == 2,
            "除 DefaultTimeoutMs 外还有字段发生变化（期望恰好一删一增）：\n" + changed);
        Assert.Contains("defaultTimeoutMs", changed);
        Assert.Contains("7777", changed);
    }

    /// <summary>
    /// 每个分类的**代表性字段**都能从 UI 写回模型（装载方向已由往返测试覆盖）。
    /// <para>取值刻意与模型默认不同，避免"碰巧相等"造成的假通过。</para>
    /// </summary>
    [Fact]
    public void EveryCategory_RepresentativeField_WritesBackToModel()
    {
        var project = RichProject();

        WithPanel(project, view =>
        {
            view.FindControl<TextBox>("TxtAuthor")!.Text = "改过的作者";
            view.FindControl<FANumberBox>("NumScreenWidth")!.Value = 640;
            view.FindControl<FANumberBox>("NumRetryBackoff")!.Value = 1250;
            view.FindControl<FANumberBox>("NumMaxTasks")!.Value = 3;
            view.FindControl<TextBox>("TxtOutputDir")!.Text = "./changed";
            view.FindControl<FANumberBox>("NumSessionTimeout")!.Value = 7;

            Click(view, "BtnApply");
        });

        Assert.Equal("改过的作者", project.Author);
        Assert.Equal(640, project.ScreenDefaultsConfig.DefaultWidth);
        Assert.Equal(1250, project.VariableManager.RetryBackoffMs);
        Assert.Equal(3, project.RuntimeConfig.MaxRuntimeTasks);
        Assert.Equal("./changed", project.ExportConfig.OutputDirectory);
        Assert.Equal(7, project.SecurityConfig.SessionTimeoutMin);
    }

    /// <summary>
    /// **屏幕尺寸的「应用」不得连带改写已存在屏幕**（历史缺陷的回归锁）。
    /// <para>`SaveToProject` 里那段注释记载：曾经的实现是无条件 `foreach` 覆盖所有屏幕尺寸，
    /// 于是"每次打开工程设置点确定"都会静默重置用户逐屏手工调过的尺寸。
    /// 契约是 `ScreenDefaultsConfig` **只影响后续新建的屏幕**，改已有屏幕要走显式的「应用到全部屏幕」。</para>
    /// </summary>
    [Fact]
    public void Apply_DoesNotTouchExistingScreens_OnlyDefaults()
    {
        var project = RichProject();
        var mainBefore = (project.Screens[0].Width, project.Screens[0].Height);
        var spareBefore = (project.Screens[1].Width, project.Screens[1].Height);

        WithPanel(project, view =>
        {
            view.FindControl<FANumberBox>("NumScreenWidth")!.Value = 480;
            view.FindControl<FANumberBox>("NumScreenHeight")!.Value = 320;
            Click(view, "BtnApply");
        });

        Assert.Equal(480, project.ScreenDefaultsConfig.DefaultWidth);
        Assert.Equal(320, project.ScreenDefaultsConfig.DefaultHeight);
        // 已有屏幕保持原样
        Assert.Equal(mainBefore, (project.Screens[0].Width, project.Screens[0].Height));
        Assert.Equal(spareBefore, (project.Screens[1].Width, project.Screens[1].Height));
    }

    /// <summary>
    /// 空表与空字符串的归一化方向必须**不丢用户意图**：
    /// 可空的文本字段清空后保存应回到 `null`（而不是空串），
    /// 否则下游 `<c>string.IsNullOrWhiteSpace</c> ?? </c>` 判定与 JSON 都会多出一段无意义内容。
    /// </summary>
    [Fact]
    public void ClearingOptionalTextFields_WritesNullNotEmptyString()
    {
        var project = RichProject();

        WithPanel(project, view =>
        {
            view.FindControl<TextBox>("TxtHistoryPath")!.Text = "";
            view.FindControl<TextBox>("TxtAuditPath")!.Text = "";
            view.FindControl<TextBox>("TxtDefaultBgColor")!.Text = "";
            Click(view, "BtnApply");
        });

        Assert.Null(project.RuntimeConfig.HistoryStoragePath);
        Assert.Null(project.SecurityConfig.AuditLogStoragePath);
        Assert.Null(project.ScreenDefaultsConfig.DefaultBgColor);
    }

    /// <summary>
    /// 空选择项不得把工程里的值改写成默认：
    /// 未选屏幕（"使用默认"项）保存后 `BootScreenId` / `LockScreenId` 应为 `null`，
    /// 而**不是**被塞回第一块屏幕的 Id。
    /// </summary>
    [Fact]
    public void UnsetScreenSelectors_SaveNull_NotFirstScreen()
    {
        var project = RichProject();
        project.RuntimeConfig.BootScreenId = null;
        project.SecurityConfig.LockScreenId = null;

        WithPanel(project, view => Click(view, "BtnApply"));

        Assert.Null(project.RuntimeConfig.BootScreenId);
        Assert.Null(project.SecurityConfig.LockScreenId);
    }

    /// <summary>
    /// 非空 `ProjectVersion` / `OutputDirectory` 的往返（这两项在模型上是**非空**默认值，
    /// 装载时把 null 读成默认值、保存时把空串写回默认值属有意归一，见下一用例）。
    /// </summary>
    [Fact]
    public void NonDefaultVersionAndOutputDir_RoundTripExactly()
    {
        var project = RichProject();

        WithPanel(project, view => Click(view, "BtnApply"));

        Assert.Equal("2.3.1", project.ProjectVersion);
        Assert.Equal("./out", project.ExportConfig.OutputDirectory);
    }

    /// <summary>
    /// **有意归一（钉住它，免得被当缺陷"修"成保留 null）**：`ProjectVersion` 与 `OutputDirectory`
    /// 在模型上是**非空**属性、默认值分别是 `1.0.0` 与 `./export`。
    /// null 对它们不是有意义的状态 —— 保留 null 会让 `ResolveOutputDirectory` 直接返回 null、
    /// 导出对话框拿不到默认目录。所以"打开即归一到默认值"是设计，不是静默改写。
    /// </summary>
    [Fact]
    public void NullVersionAndOutputDir_NormalizeToModelDefaults_Intentionally()
    {
        var project = RichProject();
        project.ProjectVersion = null!;
        project.ExportConfig.OutputDirectory = null!;

        WithPanel(project, view => Click(view, "BtnApply"));

        Assert.Equal("1.0.0", project.ProjectVersion);
        Assert.Equal("./export", project.ExportConfig.OutputDirectory);
    }

    /// <summary>
    /// **失效的型号 Id 不得被悄悄绑到另一块板**（本轮往返测试抓出的真缺陷的回归锁）。
    ///
    /// <para>`HardwareTargetId` 是可空字段，`null` 语义明确（生成物 <c>// Target: generic</c>、
    /// 状态栏回落 generic、校验行隐藏）。旧实现 `FindHardwareTargetItem` 在无匹配时回退到
    /// **第一项**，于是 `SaveToProject` 会把用户从未选择过的型号持久化进工程 ——
    /// 打开设置点一下「应用」，生成物就换了板子；而当工程里存的是**失效 Id** 时，
    /// 回退会把它绑到**另一块板**而不是诚实地说"未指定"。</para>
    /// </summary>
    [Fact]
    public void UnknownHardwareTargetId_BecomesUnspecified_NeverAnotherBoard()
    {
        var project = RichProject();
        project.HardwareTargetId = "board-that-no-longer-exists";

        WithPanel(project, view => Click(view, "BtnApply"));

        Assert.Null(project.HardwareTargetId);
    }

    /// <summary>
    /// 互补：**有效的**型号 Id 必须原样保留（修法不能把正常路径也变成"未指定"）。
    /// <para>存在的 Id 从真实下拉项里取 —— 测试宿主加载的固件清单可能与仓库不同，
    /// 写死 <c>"allwinner-t113"</c> 会让这条用例依赖环境。</para>
    /// </summary>
    [Fact]
    public void ValidHardwareTargetId_RoundTripsExactly()
    {
        // 第一趟：只为了问出"有哪些真实型号"
        var realId = UiTestHost.Run(() =>
        {
            var probe = new ProjectSettingsView(RichProject());
            try
            {
                probe.Show();
                UiTestHost.Pump();
                var items = probe.FindControl<ComboBox>("CmbHardwareTarget")!.Items.OfType<ComboBoxItem>().ToList();
                Assert.True(items.Count > 0, "没有加载到任何固件型号（firmware/*/firmware.json 缺失？）");
                return (string)items[0].Tag!;
            }
            finally
            {
                probe.Close();
            }
        });

        // 第二趟：带着真实 Id 走完整往返
        var project = RichProject();
        project.HardwareTargetId = realId;

        WithPanel(project, view => Click(view, "BtnApply"));

        Assert.Equal(realId, project.HardwareTargetId);
    }

    /// <summary>
    /// **表外 LVGL 版本不得被静默升到默认值**（登记表 §4 第 18 项）。
    ///
    /// <para>`LvglVersion` 不是纯展示字段：`LvglApiCompatChecker` 按 9.x / 8.x **分支改写生成代码**，
    /// 导出器还按它填 CMake 分支名。候选表（`8.3…9.5`）里没有它时，装载让 `SelectedItem` 变 null、
    /// 保存又把 null 归一成 `"9.5"` ⇒ **打开设置点一下确定就把 8.x 工程悄悄升成 9.x**，
    /// 生成代码的 API 兼容改写随之改变语义。</para>
    /// </summary>
    [Fact]
    public void OutOfListLvglVersion_IsPreserved_NotSilentlyUpgraded()
    {
        var project = RichProject();
        project.LvglVersion = "8.2"; // 候选表从 8.3 起

        WithPanel(project, view =>
        {
            // 下拉必须真的显示工程里那个版本（而不是空白）
            Assert.Equal("8.2", view.FindControl<ComboBox>("CmbLvglVersion")!.SelectedItem as string);
            Click(view, "BtnApply");
        });

        Assert.Equal("8.2", project.LvglVersion);
    }

    /// <summary>互补：表**内**版本照旧原样往返（修法不能把所有版本都变成"原样保留"而丢掉归一）。</summary>
    [Fact]
    public void KnownLvglVersion_RoundTripsExactly() =>
        WithPanel(RichProject(), view =>
        {
            Assert.Equal("9.4", view.FindControl<ComboBox>("CmbLvglVersion")!.SelectedItem as string);
            Click(view, "BtnApply");
        });

    /// <summary>
    /// **表外的色深值不得留下"空白下拉 + 仍然落盘一个值"**（登记表 §4 第 19 项）。
    ///
    /// <para>`ResolveColorDepthItem` 的文档写明了这个理由（回退到第一项而不是留 `SelectedItem = null`
    /// —— 后者会让下一次保存把无效值静默改写成默认值），但它此前**从未被调用**：
    /// 装载走的是裸赋值，于是下拉显示空白、保存却仍写入一个色深，用户无从知道最终落到哪个值。
    /// 本用例把该 helper 的意图钉住：界面必须显示一个**具体**的候选，落盘的必须是枚举内成员。</para>
    /// </summary>
    [Fact]
    public void InvalidStoredColorDepth_ShowsConcreteSelection_AndSavesValidEnumMember()
    {
        var project = RichProject();
        project.ScreenDefaultsConfig.DefaultColorDepth = "NoSuchDepth";

        WithPanel(project, view =>
        {
            var selected = view.FindControl<ComboBox>("CmbColorDepth")!.SelectedItem as string;
            Assert.False(string.IsNullOrEmpty(selected),
                "无效色深时下拉显示空白 —— 界面在撒谎（保存仍会写一个值）");

            Click(view, "BtnApply");
        });

        Assert.NotEqual("NoSuchDepth", project.ScreenDefaultsConfig.DefaultColorDepth);
        Assert.Contains(project.ScreenDefaultsConfig.DefaultColorDepth,
            Enum.GetNames<Hmi.Hardware.Contracts.ColorDepth>());
    }
}
