using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Serialization;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **打开失败的文案：必须带目标文件 + 两条打开路径同一条模板 + 原因必须过码化出口**
/// （登记表 <c>docs/2026-09-17-open-items-register.md</c> §4 <b>#60</b>）。
///
/// <para><b>#60 实测含三件</b>（阶段 82 探针，读数落 <c>.workbuddy/tmp/probe60.txt</c>）：</para>
/// <list type="number">
///   <item><b>60-A · 文案不含文件名</b>：两条路径的模板里**都没有**为文件留的槽位
///     （<c>status.open.failed</c> = <c>打开失败: {0}</c>、<c>ui.op.failed.detail</c> = <c>{0}失败: {1}</c>）
///     ⇒ 用户看到"工程包已损坏"不知道是**哪个**文件。既有先例（<c>status.file.not.found</c>）
///     的调用点传的是**完整路径**，本阶段跟它。</item>
///   <item><b>60-B · 两条路径的模板本来就分叉</b>：zh 下 7/7 条码化失败的文案都不同
///     （<c>打开工程失败: …</c> vs <c>打开失败: …</c>）。两者是同一个用户意图，
///     分叉没有任何文档依据 ⇒ 本阶段**合模**成一条。</item>
///   <item><b>60-C · 文件选择器那条绕过码化出口</b>：<c>MainWindow.axaml.cs:1168</c> 写的是
///     <c>LocService.Format("status.open.failed", ex.Message)</c> —— <c>ex.Message</c> 是 Core 的
///     **中文**回落原文（阶段 81 的设计要求回落自身可用 ⇒ 它必然已格式化好），
///     ⇒ 探针实测 <b>7/7 条在英文界面下中英混排</b>（原因那一半随语言切换 **0** 条）。
///     这是**阶段 81 收口漏掉的一处**（那条注释自己写着"以后新增的失败出口默认就是码化的"，
///     而这一处是"原有的、没被记起来"的）。</item>
/// </list>
///
/// <para><b>四条判据，重点是"期望值不从被测对象来"</b>：本文件的期望文案一律由
/// <b>字典模板 + <c>string.Format</c></b> 直接拼出（<see cref="Expected"/>），**不调**
/// <c>UiOperationGuard.FailureDetail</c> / <c>ExternalText.FailureReason</c> ——
/// 用被测对象算期望等于把判据写成恒真。真实的 <c>Code</c>/<c>Args</c> 来自
/// <see cref="PackageIssueLocalizationGuardTests.Probe"/> 的**真实触发**，坏包字节来自
/// 同一类的 <c>BadPackages()</c>（夹具只此一份）。</para>
///
/// <para><b>刻意覆盖不到的边界（已实测，不是猜测）</b>：<c>OpenProjectCore</c> 那条**选文件**
/// 的一步在 headless 里**不可驱动**，而且不是测试基建的欠缺 ——
/// <c>IStorageProvider</c> 族被标为"非客户端可实现"（编译器原文：
/// <i>This interface or abstract class is -not- implementable by user code !</i>），
/// 唯一可能的替代基类 <c>NoopStorageProvider</c> 是 <c>internal</c>（<c>CS0122</c>）
/// ⇒ 连"注册一个返回指定文件的工厂"都做不到。所以路径 2 走的是**与路径 1 同一个出口**
/// 这一事实由 <see cref="ExceptionMessagePassthroughFace_MatchesBaseline"/> 在**源码面**上钉住
/// （谁把路径 2 改回自己渲染，那里会红）。</para>
/// </summary>
[Collection("Language-global")]
public class OpenFailureFilePathUiTests
{
    private readonly ITestOutputHelper _out;

    public OpenFailureFilePathUiTests(ITestOutputHelper output) => _out = output;

    private const int DriveBudgetMs = 5000;

    /// <summary>驱动前给状态栏立的**唯一标记**（"上弦"）—— 让"状态栏被换掉"本身成为可判属性。</summary>
    private const string Sentinel = "⧗ 上弦（本行必须被换掉）";

    /// <summary>驱动预算内把状态栏推到谓词成立；返回是否推到。</summary>
    private static (bool Done, int Rounds, long Ms) Drive(Func<bool> done)
    {
        var start = Environment.TickCount64;
        var rounds = 0;
        while (Environment.TickCount64 - start < DriveBudgetMs)
        {
            UiTestHost.UIThread.RunJobs(DispatcherPriority.Loaded);
            UiTestHost.Pump(1);
            rounds++;
            if (done()) return (true, rounds, Environment.TickCount64 - start);
            System.Threading.Thread.Sleep(1);
        }
        return (false, rounds, Environment.TickCount64 - start);
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

    private static TextBlock StatusTextBlock(MainWindow window)
    {
        var block = window.FindControl<TextBlock>("StatusText");
        Assert.True(block is not null, "主窗口里解析不出 StatusText");
        return block!;
    }

    private static string Status(MainWindow window) => StatusTextBlock(window).Text ?? "";

    // ══════════════════════════ 期望值（只用字典，不碰被测对象） ══════════════════════════

    /// <summary>
    /// 一条失败在**当前语言**下应有的完整文案 = <c>Format(模板, 操作名, 目标路径, 原因)</c>，
    /// 其中原因由**该档自己的字典模板**渲染。三件各自独立 ⇒ 措辞改了判据跟着走。
    /// </summary>
    private static string Expected(string targetPath, PackageIssueLocalizationGuardTests.Hit hit)
    {
        var loc = AppLocalization.Service;
        var template = loc[Lang.UiOpFailedDetailFile];
        var reasonTemplate = loc[ExternalText.PackageIssueKey(hit.Code)];
        var reason = hit.Args is { Count: > 0 } args
            ? string.Format(CultureInfo.InvariantCulture, reasonTemplate, args.ToArray())
            : reasonTemplate;

        return string.Format(CultureInfo.InvariantCulture, template,
            loc[Lang.UiOpOpenProject], targetPath, reason);
    }

    /// <summary>严格 CJK 判定 —— 中英混排也能判出来。</summary>
    private static bool HasCjk(string s)
    {
        foreach (var c in s)
            if (c is >= '\u4E00' and <= '\u9FFF') return true;
        return false;
    }

    // ══════════════════════════ 判据 1：端到端（路径 1 · 最近工程） ══════════════════════════

    /// <summary>
    /// <b>60-A + 60-B 的端到端证明</b>：9 条**真实坏包**落成真实文件（阶段 105 又加了 2 条
    /// **中途失败**：`reslib.json` 坏 JSON / 资产指向目录条目），
    /// 走唯一不弹选择器的打开入口 <c>StartWithOpenFile</c>，状态栏必须
    /// **逐字等于**「打开工程失败: &lt;完整路径&gt; —— &lt;该档字典原因&gt;」。
    ///
    /// <para>每次驱动前把状态栏置成唯一标记（"上弦"）—— 否则"状态栏里有这个路径"
    /// 可能只是上一轮的残留被读成了本轮结果（阶段 80 同款做法）。</para>
    /// </summary>
    [Fact]
    public void EveryBadPackage_ShowsTheTargetFilePath_EndToEnd()
        => UiTestHost.Run(() =>
        {
            var dir = TestTempDir.NewDir("openfail-filepath");
            var window = new MainWindow(new SettingsService(Path.Combine(dir, "settings.json")));
            window.Show();
            DriveLayout(window);

            var hits = PackageIssueLocalizationGuardTests.Probe();
            var bads = PackageIssueLocalizationGuardTests.BadPackages();
            Assert.True(hits.Count == bads.Length,
                $"触发面与夹具长度不一致（{hits.Count} vs {bads.Length}）—— 探针可能静默跳过了某几条");

            var readings = new List<string>();
            var problems = new List<string>();

            for (var i = 0; i < bads.Length; i++)
            {
                var path = Path.Combine(dir, $"bad{i}.hmi");
                File.WriteAllBytes(path, bads[i].Bytes);

                StatusTextBlock(window).Text = Sentinel;
                window.StartWithOpenFile(path);
                var (done, rounds, ms) = Drive(() => Status(window).Contains(path, StringComparison.Ordinal));
                DriveLayout(window, 2);

                var got = Status(window);
                var want = Expected(path, hits[i]);
                readings.Add($"  [{i + 1}] {bads[i].Label}（驱动 {rounds} 轮 / {ms} ms）");
                readings.Add($"      {got}");

                if (!done)
                    problems.Add($"{bads[i].Label}：状态栏在 {DriveBudgetMs} ms 内没出现文件路径（仍是「{got}」）");
                else if (!string.Equals(got, want, StringComparison.Ordinal))
                    problems.Add($"{bads[i].Label}：\n      实际: {got}\n      期望: {want}");
            }

            JudgeReadings.Write(_out, "打开失败的端到端文案（路径 1 · 含文件路径）", readings);

            Assert.True(problems.Count == 0,
                "打开失败的文案与「操作名 + 完整路径 + 字典原因」不一致（#60）：\n" + string.Join("\n", problems));
        });

    // ══════════════════════════ 判据 2：出口层 × 两种语言 ══════════════════════════

    /// <summary>
    /// <b>60-C 的修法判据</b>：出口 <c>FailureDetail(操作名, 目标路径, 异常)</c> 在**两种语言**下
    /// 都必须 (a) 带上目标路径、(b) 原因那一半来自**该语言的字典**（⇒ 英文界面下不含汉字）。
    ///
    /// <para><b>为什么"相等"与"不含 CJK"两条都要</b>：单看 <c>== Expected()</c> 在
    /// "两本字典都写成了中文"时照样绿；单看"不含 CJK"则管不了路径与操作名。
    /// 反过来，本判据也**把病根正面断言一次**（<c>ex.Message</c> 在英文界面下**含** CJK）——
    /// 否则"不含 CJK"这条断言可能在"根本渲染不出东西"时也成立。</para>
    /// </summary>
    [Fact]
    public void Outlet_KeepsPathAndLocalizedReason_InBothLanguages()
        => UiTestHost.Run(() =>
        {
            var loc = AppLocalization.Service;
            var original = loc.CurrentLanguage;
            const string Path_ = "/tmp/openfail-sample/project.hmi";
            var problems = new List<string>();
            var readings = new List<string>();

            try
            {
                foreach (var lang in new[] { "zh-CN", "en-US" })
                {
                    loc.SwitchLanguage(lang);
                    var cjkFree = 0;
                    var rawCjk = 0;

                    foreach (var hit in PackageIssueLocalizationGuardTests.Probe().Where(h => h.IsCoded))
                    {
                        var ex = new HmiPackageException(hit.Code, hit.Message, hit.Args?.ToArray());
                        var got = UiOperationGuard.FailureDetail(Lang.UiOpOpenProject, Path_, ex);
                        var want = Expected(Path_, hit);

                        if (!string.Equals(got, want, StringComparison.Ordinal))
                            problems.Add($"[{lang}] {hit.Code}：\n      实际: {got}\n      期望: {want}");
                        if (!got.Contains(Path_, StringComparison.Ordinal))
                            problems.Add($"[{lang}] {hit.Code}：文案里没有目标路径");

                        if (!HasCjk(got)) cjkFree++;
                        if (HasCjk(ex.Message)) rawCjk++;   // 病根：ex.Message 恒为中文回落原文

                        if (lang == "en-US" && hit.Code == "ZIP_UNREADABLE")
                            readings.Add($"      en 出口: {got}");
                    }

                    readings.Add($"  [{lang}] 码化失败 {PackageIssueLocalizationGuardTests.Probe().Count(h => h.IsCoded)} 条："
                                 + $"渲染结果不含汉字 {cjkFree} 条 / ex.Message 含汉字 {rawCjk} 条");
                }
            }
            finally
            {
                loc.SwitchLanguage(original);
            }

            JudgeReadings.Write(_out, "打开失败出口 × 两种语言", readings);

            // 成对断言：英文界面下出口必须是纯英文，而**原始异常消息**必须仍是中文
            // —— 后者是 60-C 的病根本身，前者是修好之后的样子。两条一起才说明判据在区分这两件事。
            loc.SwitchLanguage("en-US");
            try
            {
                var hit = PackageIssueLocalizationGuardTests.Probe().First(h => h.IsCoded);
                var ex = new HmiPackageException(hit.Code, hit.Message, hit.Args?.ToArray());
                var rendered = UiOperationGuard.FailureDetail(Lang.UiOpOpenProject, Path_, ex);

                Assert.True(!HasCjk(rendered), $"英文界面下出口文案仍含汉字：{rendered}");
                Assert.True(HasCjk(ex.Message), $"英文界面下 ex.Message 竟然不含汉字 —— 病根不成立，"
                    + $"本判据的'不含 CJK'那半可能是恒真：{ex.Message}");
            }
            finally
            {
                loc.SwitchLanguage(original);
            }

            Assert.True(problems.Count == 0,
                "打开失败出口没有同时给出「目标路径 + 该语言的字典原因」：\n" + string.Join("\n", problems));
        });

    // ══════════════════════════ 判据 3：合模不得回流 ══════════════════════════

    /// <summary>
    /// 60-B 的**结构**判据：那条分叉出去的模板（键 <c>status.open.failed</c>）不许回来。
    ///
    /// <para>它已经从两本字典与 <c>Lang</c> 常量表里删掉（可达性守卫也会盯着
    /// "字典里有、没人能取到"），这里再正面钉一次 —— 因为**键回来时通常会带着消费者一起回来**，
    /// 那条守卫就看不见了。</para>
    /// </summary>
    [Fact]
    public void DivergentOpenFailureTemplate_IsGone()
    {
        const string Key = "status.open.failed";

        var zh = PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
        var en = PluginTextLocalizationGuardTests.LoadDictionary("en-US");
        var constants = typeof(Lang)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (f.Name, Value: (string)f.GetRawConstantValue()!))
            .ToList();

        Assert.False(zh.ContainsKey(Key), $"zh 字典里又出现了 {Key} —— 两条打开路径的模板又开始分叉了");
        Assert.False(en.ContainsKey(Key), $"en 字典里又出现了 {Key} —— 两条打开路径的模板又开始分叉了");

        var owner = constants.Where(c => c.Value == Key).Select(c => c.Name).ToList();
        Assert.True(owner.Count == 0,
            $"Lang 里又出现了指向 {Key} 的常量：{string.Join(", ", owner)} —— 合模被回退了");

        // 自检：常量表必须真的读到东西（反射失效时上面那条会恒真）
        Assert.True(constants.Count >= 1500, $"只反射到 {constants.Count} 个 Lang 常量 —— 取数方式失效了");
    }
}
