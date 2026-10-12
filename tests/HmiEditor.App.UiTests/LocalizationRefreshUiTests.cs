using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Data;
using HmiEditor.App.Resources;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 语言切换后**已有绑定是否自动刷新** —— `docs/localization-summary.md` §五.2 登记的遗留项。
///
/// <para><b>缺陷形态：</b>全仓 XAML 原本一律使用
/// <c>{Binding [key], Source={x:Static res:AppLocalization.Service}}</c> 这种**索引器绑定**。
/// 索引器绑定在 Avalonia 里**不可观察** —— 索引器节点不订阅
/// <c>INotifyPropertyChanged</c>，因此无论服务发什么通知，切语言后这些文本**原地不动**。
/// 此前只能靠 <c>MainWindow.OnLanguageChanged</c> 手动重建 Dock 面板兜底，
/// 于是**菜单栏/工具栏这类不做重建的区域切语言后一直是旧语言**。</para>
///
/// <para><b>修法：</b>新增 <see cref="LocExtension"/>（<c>{res:Loc key}</c>）—— 把绑定改成
/// 绑到一个**会通知**的 <see cref="LocalizedString"/>.Value；全仓 1287 处一次性迁移。</para>
///
/// <para><b>为什么必须有行为测试：</b>"切了语言但界面没变"是**用户可见**的静默失效 ——
/// 编译通过、逻辑测试全绿。断言用"两个语言的实际字符串不同"（文案改了不会红）；
/// 另有一条**对照组**证明测试宿主本身能观察到刷新（否则"没刷新"可能是宿主的问题）。</para>
///
/// <para><b>为什么整个类必须独占运行</b>（2026-09-18 门禁实测）：这些用例会切换
/// <see cref="AppLocalization.Service"/> 的**全局当前语言**并在结束时切回；期间任何并发读字典的
/// 测试都可能拿到另一种语言。门禁全量并行时就撞出过一次假红 ——
/// <c>UiOperationGuardContractTests</c> 前后两次读同一键，得到 "Save project失败" 与
/// "保存工程失败" 两个不同语言的结果。这不是产品缺陷，是**测试之间共享全局状态**，
/// 处置与 <c>AppDataRoot-global</c> 同一解法：放进禁止并行的集合。</para>
/// </summary>
[CollectionDefinition("Language-global", DisableParallelization = true)]
public sealed class LanguageGlobalCollection;

[Collection("Language-global")]
public class LocalizationRefreshUiTests
{
    /// <summary>zh-CN 与 en-US 取值不同的键（本地化测试已保证两语言键集合一致）。</summary>
    private const string Key = "ai.generating";

    [Fact]
    public void LocExtension_Binding_RefreshesWhenLanguageSwitches()
    {
        UiTestHost.Run(() =>
        {
            var service = AppLocalization.Service;
            var original = service.CurrentLanguage;
            try
            {
                service.SwitchLanguage("zh-CN");

                // 与 XAML 里 {res:Loc key} 等价的路径：扩展产出"绑到会通知对象"的绑定
                var binding = Assert.IsType<Binding>(new LocExtension(Key).ProvideValue(null!));

                var text = new TextBlock();
                text.Bind(TextBlock.TextProperty, binding);

                var window = new Window { Width = 400, Height = 120, Content = text };
                window.Show();
                UiTestHost.Pump();

                var chinese = text.Text;
                Assert.False(string.IsNullOrEmpty(chinese), "绑定的初始取值为空，测试前提不成立");
                Assert.False(string.Equals(chinese, Key, StringComparison.Ordinal),
                    $"键 '{Key}' 在 zh-CN 里没有解析出文案（拿到的是键名本身）");

                service.SwitchLanguage("en-US");
                UiTestHost.Pump();

                Assert.NotEqual(chinese, text.Text);
            }
            finally
            {
                // 静态单例：不还原会污染同程序集里的其它 UI 测试
                service.SwitchLanguage(original);
            }
        });
    }

    /// <summary>
    /// **对照组**：同一个宿主里，索引器绑定（旧写法）**不会**刷新。
    ///
    /// <para>这条不是为了守产品行为，而是守**判据本身**：如果没有它，
    /// 上面那条 `LocExtension` 测试变绿也可能只是因为"宿主/渲染泵让所有绑定都重算了"。
    /// 有了对照组，"刷新"才真的是被 `LocExtension` 带来的。
    /// 同时它也是这次迁移的**理由存档** —— 证明旧写法确实不可观察。</para>
    /// </summary>
    [Fact]
    public void IndexerBinding_DoesNotRefresh_ControlGroup()
    {
        UiTestHost.Run(() =>
        {
            var service = AppLocalization.Service;
            var original = service.CurrentLanguage;
            try
            {
                service.SwitchLanguage("zh-CN");

                var text = new TextBlock();
                text.Bind(TextBlock.TextProperty, new Binding($"[{Key}]") { Source = service });

                var window = new Window { Width = 400, Height = 120, Content = text };
                window.Show();
                UiTestHost.Pump();

                var chinese = text.Text;
                Assert.False(string.IsNullOrEmpty(chinese));

                service.SwitchLanguage("en-US");
                UiTestHost.Pump();

                // 旧写法：不变（这正是它被淘汰的原因）。若某天 Avalonia 支持了索引器通知，
                // 这条会红 —— 那时可以删掉迁移，属于"好消息的红"。
                Assert.Equal(chinese, text.Text);
            }
            finally
            {
                service.SwitchLanguage(original);
            }
        });
    }

    /// <summary>
    /// 包装实例按 key 复用：<see cref="LocalizedString"/> 构造时订阅静态事件，
    /// 每次 new 一个会让订阅者只增不减（DataTemplate 里尤其严重）。
    /// </summary>
    [Fact]
    public void Get_ReturnsSameInstancePerKey_AndDistinctAcrossKeys()
    {
        var a1 = AppLocalization.Get("menu.file");
        var a2 = AppLocalization.Get("menu.file");
        var b = AppLocalization.Get("menu.edit");

        Assert.Same(a1, a2);
        Assert.NotSame(a1, b);
    }

    /// <summary>
    /// 不支持的语言、以及与当前相同的语言都不产生任何通知 ——
    /// 冗余通知会让每次无谓切换触发全界面重绑。
    /// </summary>
    [Fact]
    public void SwitchLanguage_NoOpCases_DoNotRaiseChange()
    {
        var service = AppLocalization.Service;
        var original = service.CurrentLanguage;
        try
        {
            service.SwitchLanguage("zh-CN");

            var raised = 0;
            void Handler(object? _, System.ComponentModel.PropertyChangedEventArgs __) => raised++;

            service.PropertyChanged += Handler;
            try
            {
                service.SwitchLanguage("zh-CN");        // 同语言
                service.SwitchLanguage("fr-FR");        // 不支持
                Assert.Equal(0, raised);

                service.SwitchLanguage("en-US");        // 真切换
                Assert.True(raised > 0, "真切换语言没有发出属性变更通知");
            }
            finally
            {
                service.PropertyChanged -= Handler;
            }
        }
        finally
        {
            service.SwitchLanguage(original);
        }
    }

    // ── 回归守卫：不得再引入"不可观察的索引器绑定" ──

    private static readonly Regex IndexerBindingToLocalization = new(
        @"\{Binding\s+\[[^\]]+\]\s*,\s*Source=\{x:Static\s+res:AppLocalization\.Service\}\}",
        RegexOptions.Compiled);

    /// <summary>
    /// 全仓 <c>.axaml</c> 不得再出现 <c>{Binding [key], Source={x:Static res:AppLocalization.Service}}</c>。
    ///
    /// <para><b>为什么这条守卫是"静默失效"型：</b>重新写回旧写法的代码**照常编译、照常显示正确文案**
    /// —— 只是永远不会随语言切换刷新。任何行为测试都看不出差别（除非真的挂上可视树切一次语言），
    /// 所以只能用源码形态把它钉住。</para>
    /// </summary>
    [Fact]
    public void NoXaml_UsesUnobservableIndexerBindingForLocalization()
    {
        var files = Directory.EnumerateFiles(
                AppSourceScanner.AppRoot, "*.axaml", SearchOption.AllDirectories)
            .Where(f => !SourceScanner.IsBuildOutput(f))
            .ToList();

        // 扫描面自检：真的扫到了一批 XAML（否则下面的断言恒绿）
        Assert.True(files.Count >= 30, $"只扫到 {files.Count} 个 .axaml，扫描面可能失效");

        var offenders = new List<string>();
        foreach (var path in files)
        {
            var text = File.ReadAllText(path);
            if (IndexerBindingToLocalization.IsMatch(text))
                offenders.Add(Path.GetRelativePath(AppSourceScanner.AppRoot, path).Replace('\\', '/'));
        }

        Assert.True(offenders.Count == 0,
            "这些 XAML 仍用不可观察的索引器绑定做本地化 —— 切语言后不会刷新，请改用 {res:Loc key}：" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// 正面钉子：新写法确实在用（防止"守卫绿了但其实一处都没迁"）。
    /// </summary>
    [Fact]
    public void Xaml_ActuallyUsesLocExtension()
    {
        var files = Directory.EnumerateFiles(
                AppSourceScanner.AppRoot, "*.axaml", SearchOption.AllDirectories)
            .Where(f => !SourceScanner.IsBuildOutput(f))
            .ToList();

        var hits = files.Sum(f => Regex.Matches(File.ReadAllText(f), @"\{res:Loc\s+[a-zA-Z0-9._-]+\}").Count);

        Assert.True(hits >= 1000, $"只找到 {hits} 处 {{res:Loc ...}}，迁移可能被回退");
    }

    /// <summary>
    /// **静态守卫**：任何**切换全局当前语言**的测试类都必须挂本集合。
    ///
    /// <para><b>为什么需要它</b>：「这些用例独占运行」这条约定从 2026-09-18 起就在，
    /// 但它是**靠人记得**的。漏挂的表现不是自己红、而是**别的类**假红，
    /// 且症状随字典内容而变 —— 于是它比普通缺陷更难归因。阶段 95 实测：新建的判据类
    /// 漏挂 ⇒ `EditorCommandCatalogTests` 报
    /// 「Expected: Not <c>"Ungroup"</c>; Actual: <c>"Ungroup"</c>」
    /// （en 字典里 <c>cmd.desc.ungroup</c> 的英文值恰好与枚举名同形，`Describe` 的返回值其实是对的）。
    /// 同一过滤器：**与大队同进程 5/5 红 / 单独跑 5/5 绿**。
    /// 顺带查出另外两个游离的类（`ActionTextLocalizationGuardTests` 与 `OpenFailureFilePathUiTests`）。</para>
    ///
    /// <para><b>为什么锚在"切语言"这个动作上、而不是类名清单</b>：清单是"下次记得加我"的形状
    /// （登记 #54 的病）。扫描面是 `tests/HmiEditor.App.UiTests` 的**顶层源文件**，
    /// 判据是「文件里出现 <c>SwitchLanguage(</c> ⇒ 某个 <c>public class</c> 的**前一行**必须是本集合标注」。</para>
    ///
    /// <para><b>为什么标注要断在"类声明前一行"</b>：只断"文件里出现过那个串"会被
    /// **注释/散文**满足 —— 本判据自己的文件里就写着那个串（#48「名字即引用」同族）。</para>
    ///
    /// <para><b>单向判据</b>：挂了集合但不切语言的类**合法且常见**（它们读字典，同样需要独占期）
    /// ⇒ 不反判，否则一片假红。下界防「面找错 ⇒ 恒真」。</para>
    /// </summary>
    [Fact]
    public void EveryTestClassThatSwitchesLanguage_IsInTheGlobalCollection()
    {
        var dir = Path.Combine(SourceScanner.RepoRoot, "tests", "HmiEditor.App.UiTests");
        var files = Directory.EnumerateFiles(dir, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f => !SourceScanner.IsBuildOutput(f))
            .ToList();

        Assert.True(files.Count >= 50, $"只扫到 {files.Count} 个测试源文件 —— 扫描面找错了");

        var switchers = files
            .Where(f => File.ReadAllText(f).Contains("SwitchLanguage(", StringComparison.Ordinal))
            .ToList();

        Assert.True(switchers.Count >= 12,
            $"只找到 {switchers.Count} 个切语言的源文件（下界 12）—— 扫描面或匹配串失效，下面的断言会恒真");

        var missing = switchers.Where(f => !HasCollectionAttribute(File.ReadAllText(f)))
            .Select(f => Path.GetFileName(f)!)
            .ToList();

        Assert.True(missing.Count == 0,
            "这些测试类会**切换全局当前语言**，却没挂 [Collection(\"Language-global\")]"
            + "（DisableParallelization = true）⇒ 与大队并行时会打红**别的**类"
            + "（症状随字典内容而变，极难归因）：\n  " + string.Join("\n  ", missing));
    }

    /// <summary>
    /// 与上一条**同族、不同全局量**：凡是构造 <see cref="DataRootSandbox"/> 的测试类，
    /// 都必须挂同一个禁止并行的集合（2026-09-24 阶段 100 门禁实测）。
    ///
    /// <para><b>为什么它需要单独一条</b>：上一条盯的是 <c>SwitchLanguage(</c>（改进程级**语言**），
    /// 这一条盯的是 <c>new DataRootSandbox</c>（改进程级**数据根** —— 它的构造 / 析构直接
    /// <c>Environment.SetEnvironmentVariable</c>，而 <c>AppPaths.DataRoot</c> 读同一个变量）。
    /// 两个量互不相干 ⇒ 「有人切语言时没挂集合」那条判据**看不见**「有人改数据根时没挂集合」。</para>
    ///
    /// <para><b>它挡的那个红长什么样</b>（阶段 100 门禁实测过一次）：某个用例在断言里读
    /// <c>AppPaths.DataRoot</c>，读到的是**另一个并发用例**的沙箱根 ⇒
    /// 「当前数据根「…/opensave-save-data-…」不在扫描面的候选集合里」。
    /// ⚠ 更坏的是**反方向**：用例读到别人的数据根却**照样通过** ⇒ **假绿**。
    /// 所以它不能只当"低频噪声"处理 —— 低频的是**复现率**，不是**后果**。</para>
    ///
    /// <para><b>为什么挂 <c>Language-global</c> 而不是新建一个集合名</b>：本集合是
    /// <c>DisableParallelization = true</c>（独占）⇒ 挂进来就与**所有**其他类串行；
    /// 而 <c>AppDataRoot-global</c> **不解决问题** —— xunit 里**不同集合之间照样并行**。</para>
    /// </summary>
    [Fact]
    public void EveryTestClassThatUsesDataRootSandbox_IsInTheGlobalCollection()
    {
        var dir = Path.Combine(SourceScanner.RepoRoot, "tests", "HmiEditor.App.UiTests");
        var files = Directory.EnumerateFiles(dir, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f => !SourceScanner.IsBuildOutput(f))
            .ToList();

        Assert.True(files.Count >= 50, $"只扫到 {files.Count} 个测试源文件 —— 扫描面找错了");

        var consumers = files
            .Where(f => File.ReadAllText(f).Contains("new DataRootSandbox", StringComparison.Ordinal))
            .ToList();

        // ⚠ 下界**刻意不贴着实测值**（阶段 100 实测 11 个）：口径失效时它会掉到个位数，
        // 而"又加了一个消费者"是**正常增长**、不该红。
        Assert.True(consumers.Count >= 8,
            $"只找到 {consumers.Count} 个用 DataRootSandbox 的源文件（下界 8）—— "
            + "扫描面或匹配串失效，下面的断言会恒真");

        var missing = consumers
            .Where(f => !HasCollectionAttribute(File.ReadAllText(f)))
            .Select(f => Path.GetFileName(f)!)
            .ToList();

        Assert.True(missing.Count == 0,
            "这些测试类会**改进程级数据根**（`new DataRootSandbox` ⇒ `Environment.SetEnvironmentVariable`），"
            + "却没挂 [Collection(\"Language-global\")]（DisableParallelization = true）⇒ "
            + "与大队并行时会让**别的**用例读到错误的数据根（症状：`AppPaths.DataRoot` 不在候选集合里；"
            + "更坏的是静默读到别人的根 ⇒ 假绿）：\n  " + string.Join("\n  ", missing));
    }

    /// <summary>某个 <c>public class</c> 的**前一行**是不是本集合标注。</summary>
    private static bool HasCollectionAttribute(string text)
    {
        const string attribute = "[Collection(\"Language-global\")]";
        var lines = text.Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("public class ", StringComparison.Ordinal)
                && string.Equals(lines[i - 1].Trim(), attribute, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
