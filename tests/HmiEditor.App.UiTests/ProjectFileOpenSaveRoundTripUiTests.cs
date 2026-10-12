using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// `MainWindow` 的**工程文件打开/保存路径**行为判据
/// （登记表 <c>docs/2026-09-17-open-items-register.md</c> §4 第 37 项遗留 ③「文件打开/保存路径未覆盖」）。
///
/// <para><b>缺的是什么：</b>`MainWindow.axaml.cs` 早在阶段 46 就有"能构造 / 接线闭合 / 点了菜单有反应"
/// 三层判据，但**没有一条碰过磁盘**。打开与保存这两条路径上全部是"只在真跑一次才会发生"的动作：
/// 解包 ZIP、换掉 `_currentProject`、记录最近工程、把工程写回原路径、
/// 以及**保存前对悬空引用做静默级联修复**（`AutoFixIntegrityIssues` 会改用户数据）。
/// 它们编译期全绿、界面看着正常，只有"打开 → 保存 → 再打开"能看出来。
/// 参考先例：§4 第 16/17 项（`ProjectSettingsView`）正是靠这种往返判据**各抓出 1~2 个真缺陷**。</para>
///
/// <para><b>为什么必须走生产入口：</b>`StartWithOpenFile(path)` 是唯一**不弹选择器**的打开入口
/// （splash 的"最近工程"就走它）；`BtnSaveProject` 是保存的生产入口。
/// 而 `OpenProjectCore` / `SaveProjectCore` 前半段只是**选择器外壳** ——
/// headless 下 `StorageProvider` 的选择器**静默返回空**（实测：点保存时 `OwnedWindows` 不变、
/// 状态栏不变、文件不动），所以直接调外壳等于什么都没测。本类只走那两个真入口。</para>
///
/// <para><b>驱动方式 —— 本阶段的实测教训（🩸 别用"轮数"当预算）：</b>
/// `StartWithOpenFile` 是 `async void`，headless 里靠 `UiTestHost.UIThread.RunJobs` 清队驱动。
/// 一开始我用"清 N 轮"，结果 90 轮 ≈ 几毫秒，**池线程还没被调度**，于是把"还没跑完"误读成"卡住"，
/// 白查了两轮。真正的量是**墙钟**：改成"清队 + `Thread.Sleep(1)`，直到谓词成立或超时"之后，
/// 打开在 **28 轮 / 114 ms** 内稳定落地。所以这里一律用 <see cref="Drive"/>（墙钟预算），
/// 且每条用例都**先断言驱动确实收敛**，否则报一条点名"根本没走完"的失败，而不是让后面的断言去解释。</para>
///
/// <para><b>断言的是内容，不是"没抛异常"：</b>每个用例都断言**可分辨的内容**
/// （标题里的工程名 / 状态栏里的目标屏 / 状态栏计数 / 落盘包里的控件序列 / 文件 mtime 真的变了 /
/// 两份 `project.json` 逐字节关系）。"点一下不抛"这种判据在本路径上**恒绿**（异常都被
/// `SaveToPath` 的 try/catch 收进状态栏了），等于零信息。</para>
///
/// <para><b>本类刻意不碰的两件事（各自登记，不混进来）</b>：</para>
/// <list type="number">
///   <item>**打开工程后工程被标脏**（`_hasUnsavedChanges = true`、标题带 `*`）。
///     已取证：触发链是 `OpenHmiFile:1060 LoadProjectIntoPanels` → 导航图 / 属性面板经静态总线转发
///     → 宿主订阅 → `SetUnsavedChanges(true)`；且"只清队不推进渲染"也照样发生 ⇒
///     它必然发生在 `OpenHmiFile:1062` 的 `SetUnsavedChanges(false)` **之后**。
///     这是**产品行为问题**，不是"路径没覆盖"，所以不在本类断言它（拿它当断言会把缺陷固化）。</item>
///   <item>**保存会给工程补 `safeAreaPadding { left/top/right/bottom/isZero }`**（原包里没有该字段）
///     —— 属于"新字段的默认值被写出"的 schema 演进，无害。它的真正意义是：
///     **「打开→原样保存→逐字节等价」在这条链路上必然红**（模板注入 + 这个补字段），
///     所以本类刻意**不用**那条判据，改用更强的**幂等**判据（见 <see cref="SaveIsIdempotent_AcrossOpenSaveCycles"/>）。</item>
/// </list>
/// </summary>
// 见 LocalizationRefreshUiTests.EveryTestClassThatUsesDataRootSandbox_IsInTheGlobalCollection：
// 本类构造 DataRootSandbox（改进程级 AppPaths.DataRoot）⇒ 必须与大队独占运行（阶段 100 门禁实测）。
[Collection("Language-global")]
public class ProjectFileOpenSaveRoundTripUiTests
{
    private readonly ITestOutputHelper _output;

    public ProjectFileOpenSaveRoundTripUiTests(ITestOutputHelper output) => _output = output;

    /// <summary>驱动预算（墙钟毫秒）。实测打开 114 ms / 保存 8 ms，5 s 是 40 倍余量。</summary>
    private const int DriveBudgetMs = 5000;

    /// <summary>包内条目名（与 <c>HmiPackageService</c> 的约定一致）。</summary>
    private const string ProjectEntry = "project.json";

    // ══════════════════════════ 夹具 ══════════════════════════

    /// <summary>
    /// 一个**内容可分辨**的工程：2 个屏幕 / 首屏 2 个具名控件 / 1 个变量 / 非默认作者与版本。
    /// <para>刻意不用 <c>new HmiProject()</c> —— 那样"打开真的装了内容"与"打开只是没崩"分不开。</para>
    /// </summary>
    private static HmiProject RichProject(string name)
    {
        var project = new HmiProject
        {
            Name = name,
            ProjectVersion = "7.7.7",
            Author = "往返判据作者",
            Description = "打开/保存往返保真夹具",
        };

        var main = ScreenFactory.Create(project, name: "主画面");
        main.Controls.Add(new HmiControl { Name = "液位表", Type = HmiControlType.Label, X = 11, Y = 22 });
        main.Controls.Add(new HmiControl { Name = "启动钮", Type = HmiControlType.Button, X = 33, Y = 44 });
        project.Screens.Add(main);
        project.Screens.Add(ScreenFactory.Create(project, name: "报警画面"));

        project.VariableManager.Variables.Add(
            new Variable { Name = "液位", Address = "D100", DataType = "Int16" });

        return project;
    }

    /// <summary>
    /// 把工程打成 <c>.hmi</c> 并返回路径。
    ///
    /// <para>⚠ 目录必须是**仓库之外**的（<see cref="TestTempDir.NewIsolatedDir"/>）：打开/保存都会走
    /// <c>RefreshGitState → GitWorkflowService.GetState(_projectDirectory)</c>，那里会**起 git 进程**做
    /// `rev-parse`；落在仓库里会向上发现父仓库的 <c>.git</c>，把"普通目录"判成仓库、再多跑几条 git 命令。</para>
    /// </summary>
    private static string PackFixture(string dir, string fileName, HmiProject project)
    {
        var path = Path.Combine(dir, fileName);
        // ⚠ 必须先把流关掉：`HmiPackageService.Pack` 建的 ZipArchive 是 `leaveOpen: true`，
        //    句柄不释放时回读同一文件会以 IOException 失败（本类第一版就是这么红的）。
        using (var fs = File.Create(path))
            HmiPackageService.Pack(fs, new HmiPackageContent(project, new AssetLibrary(), null, _ => null), "test");
        return path;
    }

    /// <summary>读包内一个文本条目（纯 ZIP 读，不经过产品代码）。</summary>
    private static string ReadEntry(string hmiPath, string entryName)
    {
        using var zip = ZipFile.OpenRead(hmiPath);
        var entry = zip.GetEntry(entryName);
        Assert.True(entry is not null, $"包 {Path.GetFileName(hmiPath)} 里没有条目 {entryName}");
        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }

    // ══════════════════════════ 驱动 ══════════════════════════

    /// <summary>
    /// **墙钟**驱动的清队循环：清一次派发队列（让 `async void` 的续体落地）+ 睡 1 ms
    /// 让线程池有机会跑，直到 <paramref name="done"/> 成立或预算用尽。
    /// 返回 <c>Done=false</c> 时调用方必须**显式报红**，别让后面的断言去猜。
    /// </summary>
    private static (bool Done, int Rounds, long Ms) Drive(Func<bool> done)
    {
        var sw = Stopwatch.StartNew();
        var rounds = 0;
        while (sw.ElapsedMilliseconds < DriveBudgetMs)
        {
            UiTestHost.UIThread.RunJobs(DispatcherPriority.Loaded);
            UiTestHost.Pump(1);
            rounds++;
            if (done()) return (true, rounds, sw.ElapsedMilliseconds);
            Thread.Sleep(1);
        }
        return (false, rounds, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// 打一个**哨兵时刻**并**回读自检**。
    ///
    /// <para>为什么要自检：几条保存判据的输入是「文件 mtime 变没变」，而它成立的前提是
    /// **哨兵真的立住了**。文件系统时间戳精度不足、或读回时被取整时，
    /// `LastWriteTimeUtc != sentinel` 会**恒真** ⇒ 判据假绿（"文件根本没写"也会被算成写了）。
    /// 这是本类里唯一一处「判据的输入面」需要自证的地方。</para>
    /// </summary>
    private static DateTime SetSentinel(string path)
    {
        // 固定用 2001-01-01：远早于现在 ⇒ 任何真实写入都会改掉它；同时不依赖"现在"。
        var sentinel = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, sentinel);
        var readBack = File.GetLastWriteTimeUtc(path);
        Assert.True(readBack.Equals(sentinel),
            $"哨兵时刻没立住（写入 {sentinel:O}，回读 {readBack:O}）—— 此时「mtime 被改掉」这条判据会恒真，"
            + "不能用它证明写盘发生过");

        return sentinel;
    }

    /// <summary>推进布局与渲染（Dock 的惰性内容、状态栏计数都依赖它）。</summary>
    private static void DriveLayout(Window window, int rounds = 3)
    {
        for (var i = 0; i < rounds; i++)
        {
            UiTestHost.UIThread.RunJobs(DispatcherPriority.Loaded);
            window.Measure(new Size(1024, 600));
            window.Arrange(new Rect(0, 0, 1024, 600));
            UiTestHost.Pump(2);
        }
        UiTestHost.UIThread.RunJobs(DispatcherPriority.Background);
    }

    private static MainWindow ShowMainWindow(string settingsPath)
    {
        var window = new MainWindow(new SettingsService(settingsPath));
        window.Show();
        DriveLayout(window);
        return window;
    }

    /// <summary>`Window.Title` 可为 null（Avalonia 契约）⇒ 判据里统一走这个非空取用器，免得每条断言都要写 `??`。</summary>
    private static string TitleOf(MainWindow window) => window.Title ?? string.Empty;

    private static string Status(MainWindow window) =>
        window.FindControl<TextBlock>("StatusText")?.Text ?? "<无 StatusText>";

    private static string Counts(MainWindow window) =>
        window.FindControl<TextBlock>("ControlCountText")?.Text ?? "<无 ControlCountText>";

    private static void Click(MainWindow window, string buttonName)
    {
        var button = window.FindControl<Button>(buttonName);
        Assert.True(button is not null, $"主窗口里解析不出按钮 {buttonName}");
        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>
    /// 打开一个工程并驱动到完成。
    ///
    /// <para><b>终态谓词为什么用标题</b>：`Title` 在 `OpenHmiFile` 的**收尾处**才被设成带工程名
    /// （第 1062 行的 `SetUnsavedChanges(false)` 与第 1063 行都设它），所以「标题含工程名」≈ 打开已跑完。
    /// ⚠ **不要**用 `_currentProject` 当谓词：它在第 1044 行就被赋值，会读到**中途态**
    /// （本类第一版就这么误判过"打开后没落地"）。</para>
    /// </summary>
    private static (bool Ok, string Diag) OpenAndSettle(MainWindow window, string hmiPath, string projectName)
    {
        window.StartWithOpenFile(hmiPath);
        var (done, rounds, ms) = Drive(() => TitleOf(window).Contains(projectName, StringComparison.Ordinal));
        DriveLayout(window, 2);
        return (done, $"驱动 {rounds} 轮 / {ms} ms；Title={window.Title}；StatusText={Status(window)}");
    }

    // ══════════════════════════ 用例 ══════════════════════════

    /// <summary>
    /// **打开**：包里的内容真的装进窗口了吗？—— 标题、状态栏计数、状态栏当前屏三处可分辨读数。
    /// <para>它守的缺陷形状：解包成功但没换 `_currentProject`（窗口停在 Untitled）、
    /// 换掉了但没切屏（状态栏还指着旧屏）、屏幕/控件计数与包内不符。</para>
    /// </summary>
    [Fact]
    public async Task Open_LoadsPackageContentIntoTheWindow()
    {
        var readings = new List<string>();
        using var sandbox = new DataRootSandbox("opensave-open");
        var dir = TestTempDir.NewIsolatedDir("opensave-open");
        var hmiPath = PackFixture(dir, "demo.hmi", RichProject("探针工程"));

        await UiTestHost.RunAsync(async () =>
        {
            var window = ShowMainWindow(Path.Combine(sandbox.DataRoot, "settings.json"));
            try
            {
                var (ok, diag) = OpenAndSettle(window, hmiPath, "探针工程");
                readings.Add($"[打开] 收敛={ok}；{diag}");
                Assert.True(ok, $"打开「{Path.GetFileName(hmiPath)}」在 {DriveBudgetMs} ms 内没有落地 —— {diag}");

                // 项目侧真值（直接从包读，不经过窗口）
                var packed = ProjectSerializer.Deserialize(ReadEntry(hmiPath, ProjectEntry))!;
                var expectedCounts = $"{MainWindow.LocService["general.controls"]}: "
                    + $"{packed.Screens[0].Controls.Count} | {MainWindow.LocService["general.screens"]}: {packed.Screens.Count}";
                readings.Add($"[打开] 包内真值：Name={packed.Name}；屏幕={packed.Screens.Count}；"
                             + $"首屏控件={packed.Screens[0].Controls.Count}（{string.Join(",", packed.Screens[0].Controls.Select(c => c.Name))}）");
                readings.Add($"[打开] 窗口读数：Title={window.Title}；Counts={Counts(window)}");

                var errors = new List<string>();

                if (!TitleOf(window).Contains(packed.Name, StringComparison.Ordinal))
                    errors.Add($"标题里没有包内工程名「{packed.Name}」：Title={window.Title}");

                if (Counts(window) != expectedCounts)
                    errors.Add($"状态栏计数与包内不符：期望「{expectedCounts}」实测「{Counts(window)}」");

                // 切到了包里的第一屏（状态栏那半句带上当前屏名）
                if (!Status(window).Contains(packed.Screens[0].Name, StringComparison.Ordinal))
                    errors.Add($"状态栏没有指向包内第一屏「{packed.Screens[0].Name}」：StatusText={Status(window)}");
                JudgeReadings.Write(_output, "opensave/open", readings);
                Assert.True(errors.Count == 0,
                    "打开路径的内容断言失败（打开会把包里的工程装进窗口）：\n  - " + string.Join("\n  - ", errors));
            }
            finally { window.Close(); }
        });
    }

    /// <summary>
    /// **保存（已知路径）**：点工具栏保存时，不该弹选择器，而应**直接写回原路径**。
    ///
    /// <para><b>判据为什么落在"文件真的变了 + 状态栏报了文件名"上</b>：
    /// headless 下的选择器是**静默空返回**（不抛、不弹窗），所以"没弹窗"本身不可判
    /// （`OwnedWindows` 恒为 0 两种分支都一样 —— 这里只当读数）。真正有判别力的两条是：
    /// ① 哨兵 mtime 被改掉（`SaveToPath` 里 `File.Move` 那一步的执行证据）；
    /// ② 状态栏 = 已保存 + 文件名（`SaveToPath` 末尾那句，且 `displayName` 来自 `_currentHmiPath`）。
    /// 若错走了选择器分支，两条**同时**不成立。</para>
    /// </summary>
    [Fact]
    public async Task SaveWithKnownPath_WritesBackToTheSameFileWithoutPicking()
    {
        var readings = new List<string>();
        using var sandbox = new DataRootSandbox("opensave-save");
        var dir = TestTempDir.NewIsolatedDir("opensave-save");
        var hmiPath = PackFixture(dir, "demo.hmi", RichProject("探针工程"));

        await UiTestHost.RunAsync(async () =>
        {
            var window = ShowMainWindow(Path.Combine(sandbox.DataRoot, "settings.json"));
            try
            {
                var (opened, openDiag) = OpenAndSettle(window, hmiPath, "探针工程");
                Assert.True(opened, $"前置条件不成立：打开没落地，后面的保存断言无从谈起 —— {openDiag}");

                var sentinel = SetSentinel(hmiPath);
                var lenBefore = new FileInfo(hmiPath).Length;
                var ownedBefore = window.OwnedWindows.Count;

                Click(window, "BtnSaveProject");
                var (saved, rounds, ms) = Drive(() => new FileInfo(hmiPath).LastWriteTimeUtc != sentinel);
                DriveLayout(window, 2);

                var after = new FileInfo(hmiPath);
                var expectedStatus = MainWindow.LocService["project.status.saved"] + ": demo.hmi";
                readings.Add($"[保存] 收敛={saved}（{rounds} 轮 / {ms} ms）；文件 {lenBefore} → {after.Length} B；"
                             + $"mtime 被改={after.LastWriteTimeUtc != sentinel}");
                readings.Add($"[保存] StatusText=「{Status(window)}」；OwnedWindows {ownedBefore} → {window.OwnedWindows.Count}"
                             + $"（读数：headless 下选择器不建窗，这个数两种分支都为 0）");
                readings.Add($"[保存] 残留 .tmp={File.Exists(hmiPath + ".tmp")}");

                var errors = new List<string>();
                if (!saved)
                    errors.Add($"点了保存但文件 mtime 仍是哨兵值（{sentinel:O}）⇒ 没有写回原路径"
                               + $"（大概率错走了选择器分支）。StatusText=「{Status(window)}」");
                if (after.LastWriteTimeUtc != sentinel && after.Length == 0)
                    errors.Add("保存后文件为空");
                if (Status(window) != expectedStatus)
                    errors.Add($"状态栏不是「{expectedStatus}」而是「{Status(window)}」"
                               + "（保存成功且 displayName 取自已知路径才会这么写）");
                if (File.Exists(hmiPath + ".tmp"))
                    errors.Add($"留下中转文件 {Path.GetFileName(hmiPath)}.tmp —— `SaveToPath` 是「写 .tmp → File.Move」两步，"
                               + "残留说明第二步没跑（用的是覆盖写而非常规的 Move 时也会露出来）");
                JudgeReadings.Write(_output, "opensave/save", readings);
                Assert.True(errors.Count == 0,
                    "已知路径保存的行为断言失败：\n  - " + string.Join("\n  - ", errors));
            }
            finally { window.Close(); }
        });
    }

    /// <summary>
    /// **改一处 → 保存 → 从磁盘重开**：用户的改动必须落盘，且工程其余内容不被顺手改坏。
    ///
    /// <para>改动走**生产入口**（点工具栏「快速按钮」加一个控件），不是直接改模型 ——
    /// 否则测的只是"序列化器会写字段"，而没测"界面上的动作真的进了要保存的那份工程"。
    /// 核对时**直接解包磁盘上的文件**，不经过窗口：这样"窗口里的内存对了但盘上没写"会立刻现形。</para>
    /// </summary>
    [Fact]
    public async Task EditThenSave_ChangeSurvivesOnDiskAndNothingElseChanges()
    {
        var readings = new List<string>();
        using var sandbox = new DataRootSandbox("opensave-edit");
        var dir = TestTempDir.NewIsolatedDir("opensave-edit");
        var hmiPath = PackFixture(dir, "demo.hmi", RichProject("探针工程"));
        var before = ProjectSerializer.Deserialize(ReadEntry(hmiPath, ProjectEntry))!;

        await UiTestHost.RunAsync(async () =>
        {
            var window = ShowMainWindow(Path.Combine(sandbox.DataRoot, "settings.json"));
            try
            {
                var (opened, openDiag) = OpenAndSettle(window, hmiPath, "探针工程");
                Assert.True(opened, $"前置条件不成立：打开没落地 —— {openDiag}");

                Click(window, "BtnQuickButton");
                DriveLayout(window, 2);

                var sentinel = SetSentinel(hmiPath);
                Click(window, "BtnSaveProject");
                var (saved, rounds, ms) = Drive(() => new FileInfo(hmiPath).LastWriteTimeUtc != sentinel);
                DriveLayout(window, 2);
                Assert.True(saved, $"点了保存但文件没被写（{rounds} 轮 / {ms} ms）；StatusText=「{Status(window)}」");

                var after = ProjectSerializer.Deserialize(ReadEntry(hmiPath, ProjectEntry))!;
                var errors = new List<string>();

                // ⚠ 顺序有讲究：**先判结构、再取 [0]**。保存若把工程写成空壳，`after.Screens[0]`
                //    会抛 ArgumentOutOfRangeException —— 那会把一条可读的判据消息换成裸异常
                //    （阶段 79 负控 NC3 实测踩过：只说"索引越界"，不说"落盘包没了屏幕"）。
                readings.Add($"[改动] 落盘包结构：屏幕={after.Screens.Count}；"
                             + $"首屏控件={(after.Screens.Count > 0 ? after.Screens[0].Controls.Count.ToString() : "<无屏幕>")}");
                if (after.Screens.Count == 0)
                {
                    errors.Add("落盘包里一个屏幕都没有 ⇒ 保存把工程写成了空壳，控件与内容断言无从谈起");
                }
                else
                {
                    readings.Add($"[改动] 生产入口加控件：首屏 {before.Screens[0].Controls.Count} → {after.Screens[0].Controls.Count} 个；"
                                 + $"名字={string.Join(",", after.Screens[0].Controls.Select(c => c.Name))}");

                    if (after.Screens[0].Controls.Count != before.Screens[0].Controls.Count + 1)
                        errors.Add($"落盘包的控件数没 +1：{before.Screens[0].Controls.Count} → {after.Screens[0].Controls.Count}");

                    // 新增的那个必须在，且原有的两个一个不少（防"整体替换成默认工程"这种假通过）
                    var namesAfter = after.Screens[0].Controls.Select(c => c.Name).ToList();
                    foreach (var original in before.Screens[0].Controls)
                        if (!namesAfter.Contains(original.Name))
                            errors.Add($"原有控件「{original.Name}」在落盘包里消失了（现存：{string.Join(",", namesAfter)}）");
                }

                // 其余内容不得被顺手改掉
                if (after.Name != before.Name) errors.Add($"工程名被改：{before.Name} → {after.Name}");
                if (after.Screens.Count != before.Screens.Count)
                    errors.Add($"屏幕数被改：{before.Screens.Count} → {after.Screens.Count}");
                if (after.VariableManager.Variables.Count != before.VariableManager.Variables.Count)
                    errors.Add($"变量数被改：{before.VariableManager.Variables.Count} → {after.VariableManager.Variables.Count}");
                if (after.Author != before.Author) errors.Add($"作者被改：{before.Author} → {after.Author}");
                if (after.ProjectVersion != before.ProjectVersion)
                    errors.Add($"工程版本被改：{before.ProjectVersion} → {after.ProjectVersion}");
                JudgeReadings.Write(_output, "opensave/edit", readings);
                Assert.True(errors.Count == 0,
                    "改动往返断言失败（改一处→保存→从磁盘解包）：\n  - " + string.Join("\n  - ", errors));
            }
            finally { window.Close(); }
        });
    }

    /// <summary>
    /// **保存是幂等的**：打开 → 保存（S1）→ 再用另一个窗口打开 S1 → 保存（S2），
    /// 两份 `project.json` 必须**逐字节相同**。
    ///
    /// <para><b>为什么用幂等而不是"与原包逐字节等价"</b>（后者实测会红，且红在有意的行为上）：
    /// ① 打开时 `InitializeTemplateManager` **有意**把 4 个预置模板注入工程（去重），
    /// 于是第一次保存必然把 `"templates": []` 撑成 4 个模板（实测 10832 → 25802 字符）；
    /// ② 保存还会给工程补上原包没有的 `safeAreaPadding { left/top/right/bottom/isZero }`（+123 字符）。
    /// 两条都不是缺陷 ⇒ 拿"逐字节等价"当判据等于把一个有意行为判成红的。
    /// 而**幂等**只依赖产品行为本身：**保存不得每存一次就给工程加一层料**
    /// （模板重复注入、默认值反复重算、集合被重复追加都会让它红 —— 实测当前是绿的：S1 == S2）。</para>
    ///
    /// <para><b>前置条件必须一起断言</b>：幂等有可能"平凡地"成立（两次都没写）。
    /// 所以第二次保存同样用哨兵 mtime 证明它**真的写了**。</para>
    /// </summary>
    [Fact]
    public async Task SaveIsIdempotent_AcrossOpenSaveCycles()
    {
        var readings = new List<string>();
        using var sandbox = new DataRootSandbox("opensave-idem");
        var dir = TestTempDir.NewIsolatedDir("opensave-idem");
        var hmiPath = PackFixture(dir, "demo.hmi", RichProject("探针工程"));
        var jsonOriginal = ReadEntry(hmiPath, ProjectEntry);

        await UiTestHost.RunAsync(async () =>
        {
            // ── 第一轮：打开 → 保存 → S1 ──
            var windowA = ShowMainWindow(Path.Combine(sandbox.DataRoot, "settings-a.json"));
            string jsonS1;
            try
            {
                var (opened, openDiag) = OpenAndSettle(windowA, hmiPath, "探针工程");
                Assert.True(opened, $"前置条件不成立：第一轮打开没落地 —— {openDiag}");

                var sentinelA = SetSentinel(hmiPath);
                Click(windowA, "BtnSaveProject");
                var (saved, rounds, ms) = Drive(() => new FileInfo(hmiPath).LastWriteTimeUtc != sentinelA);
                DriveLayout(windowA, 2);
                Assert.True(saved, $"第一轮保存没写盘（{rounds} 轮 / {ms} ms）；StatusText=「{Status(windowA)}」");

                jsonS1 = ReadEntry(hmiPath, ProjectEntry);
                var s1 = ProjectSerializer.Deserialize(jsonS1)!;
                readings.Add($"[S1] 原包 {jsonOriginal.Length} → S1 {jsonS1.Length} 字符；"
                             + $"模板={s1.Templates.Count}；屏幕={s1.Screens.Count}；"
                             + $"首屏控件={(s1.Screens.Count > 0 ? s1.Screens[0].Controls.Count : -1)}");
            }
            finally { windowA.Close(); }

            // ── 第二轮：**另一个窗口**打开 S1 → 保存 → S2 ──
            var windowB = ShowMainWindow(Path.Combine(sandbox.DataRoot, "settings-b.json"));
            string jsonS2;
            try
            {
                var (opened, openDiag) = OpenAndSettle(windowB, hmiPath, "探针工程");
                Assert.True(opened, $"第二轮打开没落地 —— {openDiag}");

                var sentinelB = SetSentinel(hmiPath);
                Click(windowB, "BtnSaveProject");
                var (saved, rounds, ms) = Drive(() => new FileInfo(hmiPath).LastWriteTimeUtc != sentinelB);
                DriveLayout(windowB, 2);
                Assert.True(saved, $"**幂等可能是平凡的**：第二轮保存其实没写盘（{rounds} 轮 / {ms} ms）；"
                                   + $"StatusText=「{Status(windowB)}」");

                jsonS2 = ReadEntry(hmiPath, ProjectEntry);
                var s2 = ProjectSerializer.Deserialize(jsonS2)!;
                var s1Templates = ProjectSerializer.Deserialize(jsonS1)!.Templates.Count;
                readings.Add($"[S2] S1 {jsonS1.Length} → S2 {jsonS2.Length} 字符；逐字节相同={jsonS1 == jsonS2}；"
                             + $"模板 {s1Templates} → {s2.Templates.Count}；屏幕 {s2.Screens.Count}");
            }
            finally { windowB.Close(); }

            if (jsonS1 != jsonS2)
            {
                var a = jsonS1.Split('\n');
                var b = jsonS2.Split('\n');
                var shown = 0;
                for (var i = 0; i < Math.Min(a.Length, b.Length) && shown < 5; i++)
                    if (a[i] != b[i])
                    {
                        readings.Add($"    首个差异第 {i + 1} 行：S1={Cut(a[i])} | S2={Cut(b[i])}");
                        shown++;
                    }
                readings.Add($"    行数 {a.Length} vs {b.Length}");
            }
            JudgeReadings.Write(_output, "opensave/idempotent", readings);
            Assert.True(jsonS1 == jsonS2,
                $"保存不幂等：同一份工程「打开→保存」两次得到的 project.json 不同（{jsonS1.Length} vs {jsonS2.Length} 字符）"
                + " —— 保存路径每存一次就往工程里加东西。差异现场见读数。");
        });

        static string Cut(string s) => s.Length <= 140 ? s : s.Substring(0, 140) + "…";
    }

    /// <summary>
    /// **不存在的路径**：状态栏点名报"文件不存在"，并且**不把当前工程换掉**、不弹窗、不抛。
    /// <para>这条路径的失败形状很具体：早期实现里若省掉 `File.Exists` 早退，`File.OpenRead` 会抛
    /// `FileNotFoundException`，被外层 catch 成一句笼统的"打开失败"，用户看到的是"No such file"
    /// 而不是自己刚点的那个路径。断言就落在"状态栏里有没有那个路径"上。</para>
    /// </summary>
    [Fact]
    public async Task OpenMissingPath_ReportsThePathAndKeepsCurrentProject()
    {
        var readings = new List<string>();
        using var sandbox = new DataRootSandbox("opensave-missing");
        var dir = TestTempDir.NewIsolatedDir("opensave-missing");
        var missing = Path.Combine(dir, "nope.hmi");

        await UiTestHost.RunAsync(async () =>
        {
            var window = ShowMainWindow(Path.Combine(sandbox.DataRoot, "settings.json"));
            try
            {
                var titleBefore = TitleOf(window);
                var ownedBefore = window.OwnedWindows.Count;

                window.StartWithOpenFile(missing);
                var (reported, rounds, ms) = Drive(() => Status(window).Contains(missing, StringComparison.Ordinal));
                DriveLayout(window, 2);

                var expected = MainWindow.LocService.Format(Lang.StatusFileNotFound, missing);
                readings.Add($"[不存在] 收敛={reported}（{rounds} 轮 / {ms} ms）；StatusText=「{Status(window)}」");
                readings.Add($"[不存在] 期望文案=「{expected}」；Title 未变={window.Title == titleBefore}；"
                             + $"OwnedWindows {ownedBefore} → {window.OwnedWindows.Count}");

                var errors = new List<string>();
                if (!reported)
                    errors.Add($"状态栏里没有出现那个不存在的路径「{missing}」：StatusText=「{Status(window)}」"
                               + "（能报出用户点的路径才说明走了早退分支，而不是把异常原文丢出来）");
                if (Status(window) != expected)
                    errors.Add($"状态栏不是「{expected}」而是「{Status(window)}」");
                if (TitleOf(window) != titleBefore)
                    errors.Add($"打开失败却换了当前工程：Title「{titleBefore}」→「{window.Title}」");
                if (window.OwnedWindows.Count != ownedBefore)
                    errors.Add("打开不存在的路径弹了窗（应该只写状态栏）");
                JudgeReadings.Write(_output, "opensave/missing", readings);
                Assert.True(errors.Count == 0,
                    "不存在路径的行为断言失败：\n  - " + string.Join("\n  - ", errors));
            }
            finally { window.Close(); }
        });
    }
}
