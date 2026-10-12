using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
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
/// `MainWindow` 的**工程文件打开失败路径**行为判据
/// （登记表 <c>docs/2026-09-17-open-items-register.md</c> §4 第 37 项遗留 ③ 的**边界④**：
/// 「权限不足 / 包损坏」未覆盖。阶段 79 只覆盖了打开成功 / 保存成功两条路径）。
///
/// <para><b>为什么这一层值得单独一组判据：</b>失败路径与成功路径的**共同前缀**是"先建工作目录、
/// 再解包"，分歧点在异常抛出的那一刻。成功路径上"换掉 `_currentProject` / 记最近工程 / 写标题"
/// 这些动作**全在解包之后**，所以它们天然不可能被半路的异常污染 —— 但这是**当前实现的巧合**，
/// 不是写在任何地方的不变量。判据要钉的正是这条不变量：
/// **失败的打开不改变用户看得见/用得着的一切**（标题、控件计数、最近工程列表、保存目标）。</para>
///
/// <para><b>本组判据刻意不断言的三件事（都是读数，不是缺口）：</b></para>
/// <list type="number">
///   <item>**文案的具体措辞**：只断言**结构** —— 状态栏 = `打开工程失败: &lt;非空原因&gt;`
///     （模板与操作名都从字典取），措辞改了判据跟着走。
///     ⚠ 这一档在阶段 80 时还带着一个**真缺口**：① 非 ZIP / ⑤ manifest 坏 JSON 两档把 .NET 的
///     原始异常消息（`End of Central Directory record could not be found.`、`System.Text.Json` 的
///     `'0xE8' is an invalid start of a property name…`）直接摆进了状态栏 ⇒ 用户看到英文。
///     该缺口已在**阶段 81** 闭环（`Unpack` 的失败码化 + `package.issue.*` 字典键），
///     反回归判据住在 `PackageIssueLocalizationGuardTests.RenderedReason_IsNotTheRawDotNetMessage`
///     —— **刻意不在本文件里加英文片段断言**：那等于把措辞钉进一条"只钉结构"的端到端判据，
///     与它的定调冲突（措辞归字典那条守卫管）。</item>
///   <item>**"权限不足"档**。本沙箱 `id -u=0`，`File.SetUnixFileMode(path, None)` 之后
///     **照样能打开**（阶段 80 探针实测，见 `project-status.md`）⇒ 这一档在本环境**不可复现**，
///     写出来的判据只会是一条永远绿的装饰。诚实做法是把它记在文档里，而不是假装覆盖了。</item>
///   <item>**旧格式 `.hmi.json` 打开后"保存"要另选路径**（`_currentHmiPath = null`）——
///     `SaveProjectCore` 的注释写明这是**有意行为**（"新工程 / legacy JSON（未绑定文件路径）
///     → 需选择保存位置"），不是缺陷。</item>
/// </list>
///
/// <para><b>走的是生产入口</b>：`StartWithOpenFile(path)`（唯一不弹选择器的打开入口）与
/// `BtnSaveProject`。理由同阶段 79 —— headless 下选择器**静默空返回**，
/// 直接调 `OpenProjectCore` 那种"选择器外壳"等于什么都没测。</para>
///
/// <para><b>状态栏为什么要"上弦"：</b>坏输入 ⑥（内容非 JSON）与 ⑦（内容是 JSON `null`）
/// 落到的**是同一句文案**（`工程文件解析失败`，两条不同根因、同一措辞 —— 这也是读数）。
/// 不上弦的话，⑦ 那条的"驱动到状态栏变了"会在**上一轮留下的同一句话**上立刻成立 ⇒
/// 把"根本没跑"读成"跑完了"。所以每次尝试前把状态栏置成一个**唯一标记**再驱动到它被换掉：
/// 于是"状态栏**被换掉**"本身变成一条可判的属性，而不是一个假设。
/// （与 <see cref="ProjectFileOpenSaveRoundTripUiTests.SetSentinel"/> 对 mtime 做的事同构：
/// 给观测出口立一个会被覆写的哨兵。）</para>
/// </summary>
// 见 LocalizationRefreshUiTests.EveryTestClassThatUsesDataRootSandbox_IsInTheGlobalCollection：
// 本类构造 DataRootSandbox（改进程级 AppPaths.DataRoot）⇒ 必须与大队独占运行（阶段 100 门禁实测）。
[Collection("Language-global")]
public class ProjectFileOpenFailureUiTests
{
    private readonly ITestOutputHelper _output;

    public ProjectFileOpenFailureUiTests(ITestOutputHelper output) => _output = output;

    /// <summary>驱动预算（墙钟毫秒）。失败路径实测 0~2 轮 / 0~2 ms，5 s 是三个数量级余量。</summary>
    private const int DriveBudgetMs = 5000;

    private const string ManifestEntry = "manifest.json";
    private const string ProjectEntry = "project.json";

    /// <summary>走 <c>HmiPackageService.Unpack</c> 的失败（ZIP 分支）。</summary>
    private const string ZipKind = "zip";

    /// <summary>走旧格式 <c>.hmi.json</c> 的失败（legacy 分支）—— 两种分支的失败点不同，都要覆盖。</summary>
    private const string LegacyKind = "legacy";

    /// <summary>一条坏输入。<paramref name="Label"/> 只进读数与失败消息。</summary>
    private sealed record BadInput(string Label, string Kind, string Path);

    // ══════════════════════════ 窗口读数 ══════════════════════════

    private static string TitleOf(MainWindow window) => window.Title ?? string.Empty;

    private static string Status(MainWindow window) =>
        window.FindControl<TextBlock>("StatusText")?.Text ?? "<无 StatusText>";

    private static string Counts(MainWindow window) =>
        window.FindControl<TextBlock>("ControlCountText")?.Text ?? "<无 ControlCountText>";

    /// <summary>把状态栏置成唯一标记（"上弦"）。见类注释：这是让"状态栏被换掉"可判的前提。</summary>
    private static void ArmStatusBar(MainWindow window, string marker) =>
        window.FindControl<TextBlock>("StatusText")!.Text = marker;

    /// <summary>
    /// 「打开工程失败: 」这个**前缀**（不含原因）。从字典推导，不写字面量 ——
    /// 措辞改了判据跟着走，而"是不是这条模板 + 有没有原因"这两件事不变。
    /// </summary>
    private static string FailurePrefix() =>
        MainWindow.LocService.Format(
            Lang.UiOpFailedDetail, MainWindow.LocService[Lang.UiOpOpenProject], string.Empty);

    // ══════════════════════════ 驱动 ══════════════════════════

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

    private static void Click(MainWindow window, string buttonName)
    {
        var button = window.FindControl<Button>(buttonName);
        Assert.True(button is not null, $"主窗口里解析不出按钮 {buttonName}");
        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static void OpenAndSettle(MainWindow window, string hmiPath, string projectName)
    {
        window.StartWithOpenFile(hmiPath);
        var (done, rounds, ms) = Drive(() => TitleOf(window).Contains(projectName, StringComparison.Ordinal));
        DriveLayout(window, 2);
        Assert.True(done,
            $"前置条件不成立：好包「{Path.GetFileName(hmiPath)}」在 {DriveBudgetMs} ms 内没打开"
            + $"（驱动 {rounds} 轮 / {ms} ms；Title={window.Title}；StatusText={Status(window)}）");
    }

    /// <summary>给文件立一个**会被任何真实写入覆写**的哨兵时刻，并回读自检（防"mtime 判据恒真"）。</summary>
    private static DateTime SetSentinel(string path)
    {
        var sentinel = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, sentinel);
        Assert.True(File.GetLastWriteTimeUtc(path).Equals(sentinel),
            $"哨兵时刻没立住（回读 {File.GetLastWriteTimeUtc(path):O}）⇒「文件被写过」这条判据会恒真");
        return sentinel;
    }

    // ══════════════════════════ 夹具 ══════════════════════════

    /// <summary>内容可分辨的工程：打开后标题/计数都能与"没打开"区分开。</summary>
    private static HmiProject RichProject(string name)
    {
        var project = new HmiProject
        {
            Name = name,
            ProjectVersion = "8.8.8",
            Author = "打开失败判据作者",
        };
        var main = ScreenFactory.Create(project, name: "主画面");
        main.Controls.Add(new HmiControl { Name = "液位表", Type = HmiControlType.Label, X = 11, Y = 22 });
        project.Screens.Add(main);
        return project;
    }

    /// <summary>把工程打成 <c>.hmi</c>（关掉流再回读 —— `Pack` 是 `leaveOpen: true`，不关会以 IOException 回读失败）。</summary>
    private static string PackFixture(string dir, string fileName, HmiProject project)
    {
        var path = Path.Combine(dir, fileName);
        using (var fs = File.Create(path))
        {
            HmiPackageService.Pack(
                fs, new HmiPackageContent(project, new AssetLibrary(), null, _ => null), "test");
        }
        return path;
    }

    private static string WriteText(string dir, string fileName, string text)
    {
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }

    /// <summary>定长伪随机字节，前 4 字节刻意不是 `PK\x03\x04`。</summary>
    private static string NonZip(string dir, string fileName, int size)
    {
        var path = Path.Combine(dir, fileName);
        var data = new byte[size];
        for (var i = 0; i < size; i++) data[i] = (byte)((i * 131 + 17) % 251);
        File.WriteAllBytes(path, data);
        return path;
    }

    /// <summary>
    /// 手搓 ZIP，逐条目给定内容（<c>null</c> 文本 = 该条目不写）。
    ///
    /// <para><b>⚠ 为什么是 <c>params</c> 而不是收数组</b>：元组元素的**可空性不协变** ——
    /// 调用点写 <c>new[] { (Key, Json) }</c> 推出的是 <c>(string, string)[]</c>，
    /// 而形参要 <c>(string Name, string? Text)[]</c> ⇒ 4 处调用全报 <c>CS8620</c>
    /// （阶段 80 实测：本判据的独立运行全绿，是门禁的「基线 0 警告」把它拦下来的 ——
    /// `dotnet test` 不会因为警告失败）。走 <c>params</c> 之后每个实参**逐个**转换，
    /// <c>string</c> → <c>string?</c> 是放宽方向，不再需要显式数组类型。</para>
    /// </summary>
    private static string HandZip(string dir, string fileName, params (string Name, string? Text)[] entries)
    {
        var path = Path.Combine(dir, fileName);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var (name, text) in entries)
            {
                if (text is null) continue;
                var entry = zip.CreateEntry(name);
                using var stream = entry.Open();
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(text);
            }
        }
        return path;
    }

    private static string ManifestJson(int formatVersion)
        => JsonSerializer.Serialize(
            new HmiPackageManifest { FormatVersion = formatVersion, ProjectName = "坏包" },
            JsonOptionsDefaults.Default);

    private static string ProjectJson(string name) => ProjectSerializer.Serialize(RichProject(name));

    /// <summary>
    /// 5 条**包损坏**（走 `Unpack`）+ 2 条**旧格式 JSON 损坏**（走 legacy 分支）。
    /// 目录必须是**仓库之外**的：打开会走 `RefreshGitState → GitWorkflowService.GetState(_projectDirectory)`，
    /// 落在仓库里会向上发现父仓库的 `.git`，把普通目录判成仓库再多跑几条 git 命令。
    /// </summary>
    private static List<BadInput> BuildBadInputs(string dir)
    {
        return new List<BadInput>
        {
            new("① 非 ZIP 的 .hmi（随机字节）", ZipKind, NonZip(dir, "nonzip.hmi", 512)),
            new("② ZIP 缺 manifest.json", ZipKind,
                HandZip(dir, "nomanifest.hmi", (ProjectEntry, ProjectJson("无清单")))),
            new("③ manifest 版本不符（9999）", ZipKind,
                HandZip(dir, "badversion.hmi",
                    (ManifestEntry, ManifestJson(9999)),
                    (ProjectEntry, ProjectJson("版本不符")))),
            new("④ ZIP 缺 project.json", ZipKind,
                HandZip(dir, "noproject.hmi", (ManifestEntry, ManifestJson(1)))),
            new("⑤ manifest.json 是坏 JSON", ZipKind,
                HandZip(dir, "badmanifest.hmi",
                    (ManifestEntry, "{ 这不是 JSON"),
                    (ProjectEntry, ProjectJson("坏清单")))),
            new("⑥ 旧格式 .hmi.json 内容非 JSON", LegacyKind,
                WriteText(dir, "garbage.hmi.json", "★★★ 随便一段中文 ★★★")),
            new("⑦ 旧格式 .hmi.json 内容是 JSON null", LegacyKind,
                WriteText(dir, "null.hmi.json", "null")),
        };
    }

    // ══════════════════════════ 用例 ══════════════════════════

    /// <summary>
    /// **包损坏 / 旧格式损坏 ⇒ 状态栏报失败，且用户手上的工程一个字都不变。**
    ///
    /// <para>它守的缺陷形状（四条腿，每条各自可失守）：</para>
    /// <list type="number">
    ///   <item>失败**没**写状态栏（用户点了没反应）或写成"成功"（阅读器/写错了 catch）；</item>
    ///   <item>失败**只报操作名、丢掉原因**（`FailureSummary` 被误用 ⇒ 状态栏 `打开工程失败` 后面什么都没有）；</item>
    ///   <item>`_currentProject` / 标题 / 控件计数被半路换掉（把解包**之后**的赋值提前到解包之前 ——
    ///     这正是本阶段要钉的不变量："污染点全在解包之后"是当前实现的巧合）；</item>
    ///   <item>`RecentProjects` 被塞进一个**打不开的路径**（下次启动的"最近工程"点一下就失败）。</item>
    /// </list>
    ///
    /// <para>最后一条断言把腿 ③ 钉得更深：失败序列跑完之后**点保存，必须仍然写回原好包**——
    /// 这是 `_currentHmiPath` 没被重指向的**端到端可观测证据**（比反射读私有字段强：
    /// 它走的是用户真会走的那条路）。</para>
    /// </summary>
    [Fact]
    public async Task CorruptInputs_ReportFailureAndChangeNothingTheUserCanSee()
    {
        var readings = new List<string>();
        using var sandbox = new DataRootSandbox("openfail-corrupt");
        var dir = TestTempDir.NewIsolatedDir("openfail-corrupt");
        var settingsPath = Path.Combine(sandbox.DataRoot, "settings.json");
        var goodPath = Path.Combine(dir, "good.hmi");
        var uniqueName = "ZZF80" + Guid.NewGuid().ToString("N")[..8];

        await UiTestHost.RunAsync(async () =>
        {
            var settings = new SettingsService(settingsPath);
            var window = new MainWindow(settings);
            try
            {
                window.Show();
                DriveLayout(window);
                PackFixture(dir, "good.hmi", RichProject(uniqueName));
                OpenAndSettle(window, goodPath, uniqueName);

                var titleAfterOpen = TitleOf(window);
                var countsAfterOpen = Counts(window);
                var recentAfterOpen = settings.RecentProjects.List.ToList();
                var prefix = FailurePrefix();
                readings.Add($"[前置] 好包已打开：Title={titleAfterOpen}；Controls={countsAfterOpen}；"
                             + $"RecentProjects={string.Join(" | ", recentAfterOpen)}");
                readings.Add($"[前置] 失败文案前缀（从字典推导）=「{prefix}」"
                             + "（原因那一半刻意不钉措辞；阶段 80 时那一半还是 .NET 英文原文，"
                             + "阶段 81 已把它码化成 package.issue.* 的字典文案 —— "
                             + "反回归判据在 PackageIssueLocalizationGuardTests）");

                var inputs = BuildBadInputs(dir);
                var reasons = new List<string>();
                var errors = new List<string>();

                for (var i = 0; i < inputs.Count; i++)
                {
                    var input = inputs[i];
                    var marker = $"<<openfail-arm-{i + 1}>>";
                    ArmStatusBar(window, marker);

                    window.StartWithOpenFile(input.Path);
                    // 驱动**只**要求"被换掉"（新鲜值），"形状对不对"留给下面分开判 ——
                    // 把两件事合成一个驱动谓词的话，"换成了错的形状"会被归到"根本没换"那一条上，
                    // 把人指到错的地方（NC3 实测：注入"只报操作名"之后，报出来的居然是
                    // "标记仍在 ⇒ 点了没反应"，而真问题是没有原因）。
                    var (done, rounds, ms) = Drive(() => Status(window) != marker);
                    DriveLayout(window, 2);

                    var status = Status(window);
                    var reason = status.StartsWith(prefix, StringComparison.Ordinal)
                        ? status[prefix.Length..]
                        : "";
                    reasons.Add(reason);

                    readings.Add($"  [{i + 1}] {input.Label}（{input.Kind}）");
                    readings.Add($"      · 状态栏被换成失败文案={done}（驱动 {rounds} 轮 / {ms} ms）；"
                                 + $"文案=「{status}」");
                    readings.Add($"      · Title={TitleOf(window)}；Controls={Counts(window)}；"
                                 + $"RecentProjects={settings.RecentProjects.List.Count} 条"
                                 + $"（含本次坏路径={settings.RecentProjects.List.Contains(input.Path)}）");

                    if (!done)
                        errors.Add($"{input.Label}：状态栏没有被换掉（标记「{marker}」仍在；"
                                   + $"现在的文案=「{status}」）—— 「点了没反应」或"
                                   + "「写成了成功」这两种形状之一");
                    else if (!status.StartsWith(prefix, StringComparison.Ordinal))
                        errors.Add($"{input.Label}：失败文案的形状不对（状态栏=「{status}」）—— "
                                   + $"应形如「{prefix}<原因>」（操作名 + 原因）："
                                   + "少了操作名就不知道是哪一步失败，少了原因就不知道为什么失败");
                    else if (reason.Length == 0)
                        errors.Add($"{input.Label}：失败文案里没有原因（状态栏=「{status}」）—— "
                                   + "用户只知道「打开工程失败」、不知道失败在哪");
                    if (TitleOf(window) != titleAfterOpen)
                        errors.Add($"{input.Label}：标题被改了（{titleAfterOpen} ⇒ {TitleOf(window)}）");
                    if (Counts(window) != countsAfterOpen)
                        errors.Add($"{input.Label}：控件计数被改了（{countsAfterOpen} ⇒ {Counts(window)}）");
                    if (!settings.RecentProjects.List.SequenceEqual(recentAfterOpen))
                        errors.Add($"{input.Label}：最近工程列表被改了"
                                   + $"（{string.Join(" | ", recentAfterOpen)} ⇒ "
                                   + $"{string.Join(" | ", settings.RecentProjects.List)}）");
                }

                // 腿 ③ 的端到端加固：失败序列之后，保存**必须**仍然写回那个好包。
                var sentinel = SetSentinel(goodPath);
                Click(window, "BtnSaveProject");
                var (saved, saveRounds, saveMs) = Drive(() => new FileInfo(goodPath).LastWriteTimeUtc != sentinel);
                DriveLayout(window, 2);
                var expectedSaveStatus = MainWindow.LocService["project.status.saved"] + ": good.hmi";
                readings.Add($"[收尾] 保存仍写回原好包={saved}（{saveRounds} 轮 / {saveMs} ms）；"
                             + $"StatusText=「{Status(window)}」");
                if (!saved)
                    errors.Add($"失败序列跑完之后点保存，**没有**写回原好包（mtime 仍是哨兵）⇒ "
                               + "`_currentHmiPath` 被某条失败路径重指向了别处");
                else if (Status(window) != expectedSaveStatus)
                    errors.Add($"保存后状态栏不是「{expectedSaveStatus}」而是「{Status(window)}」"
                               + "（`displayName` 取自 `_currentHmiPath`）");

                // 腿 ①/② 的强度：5 条**包损坏**的原因必须两两不同 —— 否则说明某种"一网打尽"的
                // catch 把不同损坏形态收敛成了同一句话，用户拿不到定位信息。
                var zipReasons = inputs.Select((x, i) => (x, i))
                    .Where(t => t.x.Kind == ZipKind).Select(t => reasons[t.i]).ToList();
                var distinct = zipReasons.Distinct(StringComparer.Ordinal).Count();
                readings.Add($"[读数] 包损坏 {zipReasons.Count} 条的原因去重后 {distinct} 条；"
                             + $"旧格式 {inputs.Count - zipReasons.Count} 条的原因="
                             + string.Join(" / ", inputs.Select((x, i) => (x, i))
                                 .Where(t => t.x.Kind == LegacyKind).Select(t => $"「{reasons[t.i]}」")));
                if (distinct != zipReasons.Count)
                    errors.Add($"5 条包损坏的原因只有 {distinct} 条互不相同的（应 {zipReasons.Count} 条）："
                               + "不同损坏形态被收敛成同一句话 ⇒ 用户无法定位是哪一种");

                JudgeReadings.Write(_output, "openfail/corrupt", readings);
                Assert.True(errors.Count == 0,
                    "打开失败的「不污染」不变量被破坏：\n  - " + string.Join("\n  - ", errors)
                    + "\n\n读数：\n" + string.Join("\n", readings));
            }
            finally { window.Close(); }
        });
    }

    /// <summary>
    /// **路径不存在**：走的是 `StartWithOpenFileCore` 的**早退分支**（`File.Exists` 不成立就返回），
    /// 与"包坏了"不是同一条代码路径、也不是同一套文案（`文件不存在: &lt;路径&gt;`，
    /// 由 <c>LocService.Format(Lang.StatusFileNotFound, path)</c> 渲染，**不带**"打开工程失败"前缀）。
    ///
    /// <para>所以这条判据**必须**与上一条分开：并进去就会把两套文案混成一个断言 ——
    /// 而它们恰好是两种用户动作（"我点错文件了" vs "文件是坏的"），混起来等于丢信息。</para>
    ///
    /// <para>路径**逐字**断言（走字典模板，不是硬编码散文）：这一档的文案由我们自己拼，
    /// 含目标路径，是可契约化的；上一条的原因那一半来自异常消息，刻意不契约化。</para>
    /// </summary>
    [Fact]
    public async Task MissingPath_ReportsThePathAndChangesNothingTheUserCanSee()
    {
        var readings = new List<string>();
        using var sandbox = new DataRootSandbox("openfail-missing");
        var dir = TestTempDir.NewIsolatedDir("openfail-missing");
        var settingsPath = Path.Combine(sandbox.DataRoot, "settings.json");
        var goodPath = Path.Combine(dir, "good.hmi");
        var uniqueName = "ZZF80" + Guid.NewGuid().ToString("N")[..8];
        var missingPath = Path.Combine(dir, "missing.hmi");

        await UiTestHost.RunAsync(async () =>
        {
            var settings = new SettingsService(settingsPath);
            var window = new MainWindow(settings);
            try
            {
                window.Show();
                DriveLayout(window);
                PackFixture(dir, "good.hmi", RichProject(uniqueName));
                OpenAndSettle(window, goodPath, uniqueName);

                Assert.False(File.Exists(missingPath), $"前置条件不成立：「{missingPath}」居然存在");

                var titleAfterOpen = TitleOf(window);
                var countsAfterOpen = Counts(window);
                var recentAfterOpen = settings.RecentProjects.List.ToList();

                const string marker = "<<openfail-missing-arm>>";
                ArmStatusBar(window, marker);
                window.StartWithOpenFile(missingPath);
                var expected = MainWindow.LocService.Format(Lang.StatusFileNotFound, missingPath);
                var (done, rounds, ms) = Drive(
                    () => Status(window) != marker && Status(window).Contains(missingPath, StringComparison.Ordinal));
                DriveLayout(window, 2);

                var status = Status(window);
                readings.Add($"[路径不存在] 收敛={done}（{rounds} 轮 / {ms} ms）；"
                             + $"StatusText=「{status}」；期望=「{expected}」");
                readings.Add($"[路径不存在] Title={TitleOf(window)}；Controls={Counts(window)}；"
                             + $"RecentProjects={string.Join(" | ", settings.RecentProjects.List)}");

                var errors = new List<string>();
                if (!done)
                    errors.Add($"状态栏没有被换掉（标记仍在；现在=「{status}」）");
                if (status != expected)
                    errors.Add($"状态栏不是「{expected}」而是「{status}」"
                               + "（这一档由 `StatusFileNotFound` 渲染、**不带**「打开工程失败」前缀）");
                if (TitleOf(window) != titleAfterOpen)
                    errors.Add($"标题被改了（{titleAfterOpen} ⇒ {TitleOf(window)}）");
                if (Counts(window) != countsAfterOpen)
                    errors.Add($"控件计数被改了（{countsAfterOpen} ⇒ {Counts(window)}）");
                if (!settings.RecentProjects.List.SequenceEqual(recentAfterOpen))
                    errors.Add($"最近工程列表被改了（{string.Join(" | ", recentAfterOpen)} ⇒ "
                               + $"{string.Join(" | ", settings.RecentProjects.List)}）");
                if (settings.RecentProjects.List.Contains(missingPath))
                    errors.Add("不存在的路径被记进了最近工程列表");

                JudgeReadings.Write(_output, "openfail/missing", readings);
                Assert.True(errors.Count == 0,
                    "「路径不存在」这一档的断言失败：\n  - " + string.Join("\n  - ", errors)
                    + "\n\n读数：\n" + string.Join("\n", readings));
            }
            finally { window.Close(); }
        });
    }

    /// <summary>
    /// **失败的打开不留工作区目录**（阶段 80 探针量到的真缺陷：修之前 5 次失败 = 5 个空目录）。
    ///
    /// <para><b>它的病根是"顺序"</b>：`OpenHmiFile` 的 ZIP 分支原来先
    /// `CreateWorkspaceDirectory(_currentProject)` 把**解包目标目录**建出来，才去 `File.OpenRead` + `Unpack`。
    /// 包一坏，那个目录**一个文件都没解出来**、也没人再引用它 ⇒ 空目录；而会话目录的兜底释放点在
    /// **进程退出**，所以一次会话里反复打开坏包会一路攒（阶段 80 探针实测：5 次失败 = 5 个空目录）。
    /// **修法是结构性的**：调用点改成 `create: false`（只定路径、不建目录）——`Unpack` 在全部前置校验
    /// （非 ZIP / manifest / 版本 / project.json）**通过之后**才 `Directory.CreateDirectory(assetDir)`，
    /// 那之前抛出就什么都没建。所以本判据守的是「**不提前建**」这条结构性质，
    /// 不是「建了会删干净」——后者要多一个删除器，而删除器本身是破坏性机制、还得自带守卫。</para>
    ///
    /// <para><b>为什么这里的"前后比较"是自治的</b>（本仓库踩过一次这类坑：对**共享**资源做前后快照
    /// ⇒ 单跑绿、进门禁红 —— 本判据第一版就是这么红的）：
    /// <c>AppPaths.SessionTempRoot</c> 由 <c>DataRoot</c> 推导，而 <c>DataRoot</c> 来自**进程级**
    /// 环境变量 `HMI_APP_DATA_ROOT`，<see cref="DataRootSandbox"/> 在构造/释放时改写它 ⇒
    /// **并行用例把根抢来抢去**，本用例 body 跑在哪个根下并不由本用例决定
    /// （门禁实测：body 期间根是另一个用例的 <c>opensave-idem-data-*</c>）。
    /// 第一版判据据此断言"会话根必须落在我的沙箱里"，于是大队并发下必然红 —— 那是
    /// **判据的前提不成立**，不是被测对象有问题。改成两条归属机制之后才是自治的：</para>
    /// <list type="number">
    ///   <item>**扫描面覆盖所有可能的数据根**：<c>DataRootSandbox</c> 的数据根一律从"构造那一刻的
    ///     数据根"的**同级**派生，而基线数据根被 <c>TestAppDataRootSetup</c> 钉在
    ///     `&lt;repo&gt;/.hmi-test-tmp/appdata` ⇒ 不管环境变量怎么被抢，所有沙箱数据根都平铺在
    ///     **同一个父目录**下，枚举那个父目录即可；</item>
    ///   <item>**归属靠唯一工程名**：只有本用例会拿 `ZZF80&lt;8 位 hex&gt;` 当工程名，
    ///     所以"名字带这个前缀的目录"必然是本用例打开工程的产物 —— 与它落在哪个根下无关。</item>
    /// </list>
    /// <para>比较窗口也随之从"整场前后快照"压到**单次坏输入的前后**（毫秒级）：别的用例释放自己
    /// 沙箱时会连它下面的目录一起带走，窗口越小越不容易把那种噪音读成信号。</para>
    ///
    /// <para><b>为什么用"唯一工程名"当过滤器</b>：会话根里同时还有别的目录
    /// （好包打开时按**上一个**工程名建的那个）。只数 `<唯一名>-*` 前缀的目录，
    /// 就能把"本次打开造成的"与"别的来源的"分开 ⇒ 计数有可归属的含义。</para>
    /// </summary>
    [Fact]
    public async Task FailedOpen_LeavesNoWorkspaceDirectoryBehind()
    {
        var readings = new List<string>();
        using var sandbox = new DataRootSandbox("openfail-workspace");
        var dir = TestTempDir.NewIsolatedDir("openfail-workspace");
        var settingsPath = Path.Combine(sandbox.DataRoot, "settings.json");
        var goodPath = Path.Combine(dir, "good.hmi");
        var uniqueName = "ZZF80" + Guid.NewGuid().ToString("N")[..8];

        await UiTestHost.RunAsync(async () =>
        {
            // 前提自证：扫描面 =「数据根的公共父目录 × 本进程会话 id」。
            // 它成立的前提是**所有沙箱数据根都建在同一个父目录下** ⇒ 那一刻的数据根必须落在候选集合里。
            // 哪天有人把沙箱建成两层、或换掉 `TestAppDataRootSetup` 的重定向落点，这条会红，
            // 而不是让下面的扫描静默地什么都找不到（那会让每条比较都变成 0 == 0）。
            Assert.True(DataRootCandidates(sandbox.DataRoot).Contains(AppPaths.DataRoot, StringComparer.Ordinal),
                $"当前数据根「{AppPaths.DataRoot}」不在扫描面的候选集合里"
                + $"（候选 = {string.Join(", ", DataRootCandidates(sandbox.DataRoot))}）⇒ "
                + "扫描面公式过时了，这条判据会静默地退化成恒真");

            var window = new MainWindow(new SettingsService(settingsPath));
            try
            {
                window.Show();
                DriveLayout(window);
                PackFixture(dir, "good.hmi", RichProject(uniqueName));
                OpenAndSettle(window, goodPath, uniqueName);

                // 好包打开本身会建 1 个 `<唯一名>-<hex>`（`_assetDirectory`，按**新**工程名）
                // ⇒ 基线是 1，不是 0。这里读到的个数同时是"扫描面/层级公式成立"的证据：
                // 扫不到东西的话，下面每条比较都会退化成 0 == 0（正反都绿）。
                var baseline = WorkspaceDirsAnywhere(uniqueName, sandbox.DataRoot);
                readings.Add($"[基线] 好包打开后，全进程数据根里 `{uniqueName}*` 目录 = {baseline.Count} 个"
                             + $"（{string.Join(", ", baseline)}）");
                readings.Add($"[基线] 扫描面 = {ScanFace(sandbox.DataRoot)}");
                Assert.True(baseline.Count >= 1,
                    $"基线是 0 ⇒ 扫描面没匹配上任何受本次打开影响的目录（判据会退化成恒真的 0==0）："
                    + $"扫描面={ScanFace(sandbox.DataRoot)}；各会话目录内容="
                    + DescribeScanFace(sandbox.DataRoot, uniqueName));

                var inputs = BuildBadInputs(dir);
                var errors = new List<string>();
                var prefix = FailurePrefix();
                for (var i = 0; i < inputs.Count; i++)
                {
                    var input = inputs[i];

                    // 扫描面在**这次尝试的前后各取一次**（不是"相对整场基线"）：别的用例释放自己的
                    // 沙箱时会把落在它下面的目录一并带走 —— 窗口越小，越不容易把并发噪音读成信号。
                    var before = WorkspaceDirsAnywhere(uniqueName, sandbox.DataRoot);

                    // ⚠ **必须上弦**。失败文案的前缀对每条输入都一样，不上弦的话第二条起的驱动谓词
                    // 会被**上一条**留下的同一句文案立刻满足 ⇒ 读数落在"目录已建、清理还没跑"的
                    // 中间态上。本判据第一版就是这么红的（②④⑤ 各报一次 `1 ⇒ 2`，而**收尾又回到 1**
                    // —— 清理其实生效了，是**量早了**）。上弦之后"状态栏被换掉"= 失败路径整条走完
                    // （`OpenHmiFile` 的 catch 里清理，`StartWithOpenFileCore` 的 catch 才写状态栏）。
                    var marker = $"<<openfail-ws-{i + 1}>>";
                    ArmStatusBar(window, marker);
                    window.StartWithOpenFile(input.Path);
                    Drive(() => Status(window) != marker
                                && (Status(window).StartsWith(prefix, StringComparison.Ordinal)
                                    || Status(window).Contains(input.Path, StringComparison.Ordinal)));
                    DriveLayout(window, 2);

                    var after = WorkspaceDirsAnywhere(uniqueName, sandbox.DataRoot);
                    var added = after.Except(before, StringComparer.Ordinal).ToList();
                    readings.Add($"      · {input.Label}（{input.Kind}）⇒ `{uniqueName}*` "
                                 + $"{before.Count} ⇒ {after.Count}（新增 {added.Count}）");

                    // **只查"新增"**：这条判据管的是"失败的打开**多**建了什么"。
                    // "变少"不归它管 —— 并行用例释放自己的沙箱就会让目录消失，与本缺陷无关
                    // （判据不量自己不该管的量）。`baseline` 仍然要读：它是"扫描面还成立"的证词。
                    if (added.Count > 0)
                        errors.Add($"{input.Label}：失败的打开留下了工作区目录"
                                   + $"（`{uniqueName}*` 新增 {added.Count} 个："
                                   + $"{string.Join(", ", added)}）");
                }

                var final = WorkspaceDirsAnywhere(uniqueName, sandbox.DataRoot);
                readings.Add($"[收尾] {inputs.Count} 次失败之后，`{uniqueName}*` = {final.Count} 个"
                             + $"（本场基线 {baseline.Count}；扫描面 {ScanFace(sandbox.DataRoot)}）");

                JudgeReadings.Write(_output, "openfail/workspace", readings);
                Assert.True(errors.Count == 0,
                    "失败的打开在工作区里留下了痕迹（打开失败不该建出任何还要人收拾的东西）：\n  - "
                    + string.Join("\n  - ", errors) + "\n\n读数：\n" + string.Join("\n", readings));
            }
            finally { window.Close(); }
        });
    }

    // ══════════════════════════ 工作目录：可归属的扫描 ══════════════════════════

    /// <summary>本进程的会话 id（`&lt;pid&gt;-&lt;token&gt;`）：进程内唯一，且**与数据根无关**。</summary>
    private static string SessionId() => Path.GetFileName(AppPaths.SessionTempRoot);

    /// <summary>
    /// 本进程**可能**用过的所有数据根：<paramref name="referenceDataRoot"/> 的**父目录**下的所有子目录。
    ///
    /// <para><b>为什么是这个面：</b><see cref="DataRootSandbox"/> 的数据根从"构造那一刻的数据根"的
    /// **同级**派生（<c>Path.Combine(parent, "&lt;prefix&gt;-data-&lt;id&gt;")</c>），而基线数据根被
    /// <c>TestAppDataRootSetup</c> 钉在 `&lt;repo&gt;/.hmi-test-tmp/appdata` ⇒ 不管并行用例怎么
    /// 互相抢那个**进程级**环境变量，所有沙箱数据根都平铺在**同一个父目录**下。</para>
    ///
    /// <para>父目录从 <paramref name="referenceDataRoot"/> 推 —— 它是 <see cref="DataRootSandbox"/> 的
    /// 实例属性、**构造后不再变**；刻意不读 <c>AppPaths.DataRoot</c>，那个随时会被别的用例改掉。</para>
    /// </summary>
    private static List<string> DataRootCandidates(string referenceDataRoot)
    {
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(referenceDataRoot));
        return parent is null ? new List<string>() : SafeDirs(parent);
    }

    /// <summary>
    /// 枚举子目录；并行用例正在删自己的沙箱时可能抛 ⇒ 吞掉。
    /// 扫描面失效**不会**因此静默：调用方的防空转断言守着这件事。
    /// </summary>
    private static List<string> SafeDirs(string dir)
    {
        try
        {
            return Directory.Exists(dir) ? Directory.GetDirectories(dir).ToList() : new List<string>();
        }
        catch (Exception)
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// **全进程范围**内、名字以 <paramref name="prefix"/> 开头的会话工作目录（完整路径，排序）。
    ///
    /// <para>深度写死为 `&lt;数据根&gt;/temp/.sessions/&lt;会话 id&gt;/` —— **刻意不递归**
    /// （仓库根下那棵 <c>firmware/</c> 有 174 万文件，递归枚举会被 SIGTERM 掉）。层级名是
    /// <see cref="AppPaths"/> 的实现细节：哪天真改了名，防空转断言会红，
    /// 而不是让每条比较静默地退化成"两边都是 0"。</para>
    ///
    /// <para><b>归属靠名字，不靠"根归我独占"</b>：工程名是本用例生成的 `ZZF80&lt;8 位 hex&gt;`，
    /// 只有本用例会拿它当工程名 ⇒ 命中者必然是本用例打开工程的产物，与落在哪个根下无关。</para>
    /// </summary>
    private static List<string> WorkspaceDirsAnywhere(string prefix, string referenceDataRoot)
    {
        var session = SessionId();
        var found = new List<string>();
        foreach (var dataRoot in DataRootCandidates(referenceDataRoot))
        {
            foreach (var dir in SafeDirs(Path.Combine(dataRoot, "temp", ".sessions", session)))
            {
                if (Path.GetFileName(dir).StartsWith(prefix, StringComparison.Ordinal))
                    found.Add(dir);
            }
        }
        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// <summary>扫描面的人读描述（进读数，便于在"什么都没扫到"时定位）。</summary>
    private static string ScanFace(string referenceDataRoot)
        => $"{DataRootCandidates(referenceDataRoot).Count} 个数据根 × temp/.sessions/{SessionId()}/"
           + $"（父目录={Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(referenceDataRoot))}）";

    /// <summary>扫描面里各会话目录的内容摘要（防空转断言失败时用来看"那里到底有什么"）。</summary>
    private static string DescribeScanFace(string referenceDataRoot, string highlightPrefix)
    {
        var session = SessionId();
        var parts = new List<string>();
        foreach (var dataRoot in DataRootCandidates(referenceDataRoot))
        {
            var names = SafeDirs(Path.Combine(dataRoot, "temp", ".sessions", session))
                .Select(Path.GetFileName)
                .Select(n => n is null
                    ? ""
                    : n.StartsWith(highlightPrefix, StringComparison.Ordinal) ? $"[{n}]" : n)
                .ToList();
            parts.Add($"{Path.GetFileName(dataRoot)}: {names.Count} 个"
                      + (names.Count == 0 ? "" : $"（{string.Join(", ", names.Take(6))}…）"));
        }
        return string.Join(" | ", parts);
    }
}
