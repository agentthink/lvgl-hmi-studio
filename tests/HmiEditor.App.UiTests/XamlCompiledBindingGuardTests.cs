using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **编译绑定开关守卫** —— 把一条**隐含且自相矛盾**的默认值变成显式契约（2026-09-22，阶段 69）。
///
/// <para><b>起因（本阶段实测，不是推测）：</b>登记表 #38 遗留①「只查键能不能解析、不查类型对不对」
/// 的复核过程中顺带量出来的东西 —— 全仓 <b>一处都没有声明</b>
/// <c>AvaloniaUseCompiledBindingsByDefault</c>，而 Avalonia 12.1.0 包内**两个文件给了相反的值**
/// （都以「未设置时才生效」为条件）：</para>
/// <code>
/// build/Avalonia.props:8              &lt;AvaloniaUseCompiledBindingsByDefault ...&gt;true&lt;/...&gt;
/// build/AvaloniaBuildTasks.targets:5  &lt;AvaloniaUseCompiledBindingsByDefault ...&gt;false&lt;/...&gt;
/// </code>
/// <para>今天之所以是 <c>true</c>，靠的是 <b>.props 先于 .targets 导入</b>这一个**顺序巧合**
/// —— 包一升级改了任一处，全仓 <c>{Binding}</c> 的编译期校验就没了。</para>
///
/// <para><b>而「失去校验」这件事是静默的</b>（这才是它值得一条判据的原因）：
/// 编译绑定关掉后 XAML 里的 <c>{Binding}</c> 退回<b>反射绑定</b>，
/// 编译期 0 警告；而运行期的失败出口 <c>Avalonia.Logging.Logger.Sink</c>
/// 全仓无人接（<c>grep ILogSink src/ tests/</c> 零命中）⇒
/// 绑定路径写错会一路静默到界面上（颜色错、值不刷新）。</para>
///
/// <para><b>实测对照（`.workbuddy/tmp/exp-cb.py` 注入台，两个方向都跑过）：</b></para>
/// <list type="table">
///   <item><description><b>true</b>：<c>UserManagerView.axaml</c> 的 `DataTemplate`
///     （<c>DataType="{x:Type models:AuditLogEntry}"</c>）里把一个 <c>{Binding}</c> 绑到
///     **不存在的属性** ⇒ <c>error AVLN2000</c>，**build-fail**。</description></item>
///   <item><description><b>false</b>：**同一个注入** ⇒ <c>BUILD_EXIT=0</c>、0 警告 0 错误；
///     产物里 <c>{Binding}</c> 编译成 <c>ReflectionBindingExtension</c>
///     （true 下是 <c>CompiledBindingExtension</c>）；MSBuild 落盘的证词同步由
///     <c>true</c> 变 <c>false</c>。</description></item>
/// </list>
///
/// <para><b>判据由四维组成</b>（缺任何一维都会留下一个绕过口）：</para>
/// <list type="number">
///   <item><description><b>声明必须在</b> —— 面是「所有含 <c>.axaml</c> 的项目」，
///     逐个向上找**最近**的 <c>Directory.Build.props</c>（这正是 MSBuild 的查找规则：
///     命中第一个就停），要求它有显式声明且值为 true。</description></item>
///   <item><description><b>声明必须传导</b> —— 光写了不算：读 MSBuild **自己落盘**的
///     <c>*.GeneratedMSBuildEditorConfig.editorconfig</c>（<c>build_property.*</c> 那几行，
///     它由 MSBuild 在编译前真实求值后写出），要求证词与声明一致。
///     这一维是「显式声明真的生效了」的唯一非推断证据。</description></item>
///   <item><description><b>剥注释必须在起作用</b> —— 见
///     <see cref="CommentStripping_IsWhatKeepsProseFromSatisfyingTheSearch"/>：
///     本项目的注释里**正在**讨论这个属性名，所以「找不找得到」必须先证明
///     不是被自己的散文满足的（#48 / #52 各踩过一次）。</description></item>
///   <item><description><b>唯一出处 + 不许改道</b> —— 属性只许声明一次（两处声明 = 谁是权威说不清），
///     且不许有 <c>ImportDirectoryBuildProps</c> / <c>DirectoryBuildPropsPath</c>
///     把查找改道（那会让第 1 维的「向上找」结论失效）。</description></item>
/// </list>
///
/// <para><b>本层刻意不判的</b>：① 不判「某个具体绑定对不对」——
/// 那是编译器的事，本层只保证**编译器仍然在管**；② 不判 <c>{StaticResource}</c>
/// 资源键 —— 键缺失不受本开关影响，由
/// <see cref="XamlResourceKeyClosureTests"/> 的静态面守；③ 不判
/// <c>XamlDynamicSetters</c> 之类的生成物是否存在 —— 实测它由 **XAML 预编译**产生，
/// 与本开关**无关**（注入 false 后它照旧存在），拿它当证据是错的。</para>
/// </summary>
public class XamlCompiledBindingGuardTests
{
    private const string PropertyName = "AvaloniaUseCompiledBindingsByDefault";

    /// <summary>会把 <c>Directory.Build.props</c> 的查找改道的两个 MSBuild 属性名。</summary>
    private static readonly string[] LookupOverrideNames =
    {
        "ImportDirectoryBuildProps",
        "DirectoryBuildPropsPath",
    };

    private readonly ITestOutputHelper _output;

    public XamlCompiledBindingGuardTests(ITestOutputHelper output) => _output = output;

    // ──────────────────────────────────────────────
    // 面：所有含 .axaml 的项目
    // ──────────────────────────────────────────────

    /// <summary>
    /// 含 <c>.axaml</c> 的项目目录（去重、排序）。
    ///
    /// <para><b>只走 <c>src/</c></b>：仓库根不能全量遍历 —— <c>firmware/</c> 挂着整套 Tina SDK
    /// （实测 174 万个文件，全量 walk 会被沙箱 SIGTERM）。而 <c>src/</c> 是唯一放 XAML 的地方。</para>
    ///
    /// <para><b>面从 <c>.axaml</c> 反推，不写死项目名</b>：写死就变成「新增一个带 XAML 的项目
    /// 不会进面」（#42 那类静默退化的形状）。</para>
    /// </summary>
    private static List<string> XamlProjectDirectories()
    {
        var srcRoot = Path.Combine(SourceScanner.RepoRoot, "src");
        if (!Directory.Exists(srcRoot)) return new List<string>();

        return Directory.EnumerateFiles(srcRoot, "*.axaml", SearchOption.AllDirectories)
            .Where(p => !SourceScanner.IsBuildOutput(p))
            .Select(ProjectDirectoryOf)
            .Where(d => d is not null)
            .Select(d => d!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>从项目下的任意文件向上找第一个含 <c>.csproj</c> 的目录。</summary>
    private static string? ProjectDirectoryOf(string anyFileUnderProject)
    {
        var dir = Path.GetDirectoryName(anyFileUnderProject);
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.EnumerateFiles(dir, "*.csproj").Any()) return dir;
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
        return null;
    }

    /// <summary>
    /// MSBuild 的规则：从项目目录向上找**第一个** <c>Directory.Build.props</c>，命中即停
    /// （除非那个文件自己 Import 了上一级）。所以「最近的那个」就是权威。
    /// </summary>
    internal static string? NearestDirectoryBuildProps(string projectDirectory)
    {
        var dir = projectDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "Directory.Build.props");
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
        return null;
    }

    // ──────────────────────────────────────────────
    // 读一个 <属性> 元素的赋值表达式
    // ──────────────────────────────────────────────

    /// <summary>
    /// 读出属性元素的**赋值表达式**（元素名不区分命名空间前缀）。
    ///
    /// <para><b>锚点落在元素上，不落在名字字符串上</b>：落在名字上，注释里提一次属性名
    /// 就算「声明过了」—— 本项目的注释里**正在**提它（见
    /// <see cref="CommentStripping_IsWhatKeepsProseFromSatisfyingTheSearch"/>）。</para>
    ///
    /// <para>带 <c>Condition</c> 的声明返回 <c>null</c>：条件化的「显式声明」在别的条件下不生效，
    /// 它不是契约而是一个可能失效的开关（由 <see cref="DeclarationsOf"/> 单独点名报红）。</para>
    /// </summary>
    internal static string? DeclaredValue(string xmlText, string propertyName)
    {
        // 值元组不是可空类型 ⇒ 不能写 `FirstOrDefault(...)?.Value`（CS0023）。
        var unconditional = DeclarationsOf(xmlText, propertyName)
            .Where(d => d.Condition is null)
            .ToList();
        return unconditional.Count == 0 ? null : unconditional[0].Value;
    }

    /// <summary>所有 <paramref name="propertyName"/> 声明处（含带 <c>Condition</c> 的）。</summary>
    internal static List<(string? Condition, string Value)> DeclarationsOf(string xmlText, string propertyName)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlText);
        }
        catch (System.Xml.XmlException)
        {
            // 调用方按「一个声明都没有」处理并报红。
            return new List<(string? Condition, string Value)>();
        }

        return doc.Descendants()
            .Where(e => string.Equals(e.Name.LocalName, propertyName, StringComparison.Ordinal))
            .Select(e => (Condition: e.Attribute("Condition")?.Value, Value: e.Value.Trim()))
            .ToList();
    }

    /// <summary>MSBuild 认 <c>true</c> / <c>True</c> / <c>TRUE</c>。</summary>
    private static bool IsTrue(string? value) => bool.TryParse(value, out var b) && b;

    private static string Rel(string absolute) => SourceScanner.Relative(absolute);

    // ──────────────────────────────────────────────
    // 判据一：每个含 XAML 的项目，其权威 Directory.Build.props 必须有显式声明
    // ──────────────────────────────────────────────

    [Fact]
    public void EveryXamlProject_DeclaresCompiledBindingsOnItsNearestDirectoryBuildProps()
    {
        var projects = XamlProjectDirectories();
        Assert.True(projects.Count > 0,
            "面是空的（src/ 下没扫到任何 .axaml）⇒ 这条判据恒真。"
            + "要么真正的 XAML 项目搬走了，要么枚举逻辑坏了 —— 两种都不该是绿的");

        var readings = new List<string>();
        var offenders = new List<string>();

        foreach (var project in projects)
        {
            var props = NearestDirectoryBuildProps(project);
            if (props is null)
            {
                offenders.Add($"{Rel(project)}: 向上找不到任何 Directory.Build.props，"
                              + $"于是 {PropertyName} 只能取包内默认值（隐含）");
                readings.Add($"{Rel(project)} :: <无 Directory.Build.props>");
                continue;
            }

            var text = SourceScanner.StripXmlComments(File.ReadAllText(props));
            var declarations = DeclarationsOf(text, PropertyName);
            var value = DeclaredValue(text, PropertyName);

            readings.Add($"{Rel(project)} -> {Rel(props)} :: {declarations.Count} 处声明，"
                         + $"取到的值 = {value ?? "<未声明>"}");

            foreach (var (condition, conditionalValue) in declarations.Where(d => d.Condition is not null))
                offenders.Add($"{Rel(props)}: 声明带了 Condition=\"{condition}\"（值 {conditionalValue}）"
                              + " —— 条件化的声明在别的条件下会失效，它不是契约；请改成无条件声明");

            if (declarations.Count == 0)
                offenders.Add($"{Rel(props)}: 没有显式声明 {PropertyName}。"
                              + "此时实际值来自 Avalonia 包内**两个相反**的默认（.props=true / .targets=false），"
                              + "靠导入顺序才今天是 true —— 包一升级就可能静默失去编译期绑定校验");
            else if (declarations.Count > 1)
                offenders.Add($"{Rel(props)}: {PropertyName} 声明了 {declarations.Count} 处 —— "
                              + "谁是权威说不清（MSBuild 取最后一个，但读代码的人看不出）");
            else if (!IsTrue(value))
                offenders.Add($"{Rel(props)}: {PropertyName}={value} —— 必须是 true。"
                              + "设成 false 会让 XAML 里的 {Binding} 退回反射绑定，"
                              + "绑定路径写错从 `error AVLN2000` 变成编译期静默、运行期也静默"
                              + "（Logger.Sink 无人接）");
        }

        JudgeReadings.Write(_output, "编译绑定守卫/声明面", readings);

        Assert.True(offenders.Count == 0,
            $"含 .axaml 的项目共 {projects.Count} 个，其中 {offenders.Count} 个没有可依赖的显式声明："
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    // ──────────────────────────────────────────────
    // 判据二：MSBuild 落盘的证词必须与声明一致（传导）
    // ──────────────────────────────────────────────

    /// <summary>
    /// 读 MSBuild **自己写出**的 <c>build_property.*</c>，与声明对照。
    ///
    /// <para><b>为什么需要这一维</b>：第 1 维只证明「文件里写了 true」，证明不了「编译时真的是 true」
    /// —— 中间还隔着「这个 <c>Directory.Build.props</c> 真的被导入了吗」「有没有别的地方后设了它」。
    /// 而 MSBuild 求值结果会落到 <c>obj/&lt;cfg&gt;/&lt;tfm&gt;/*.GeneratedMSBuildEditorConfig.editorconfig</c>
    /// （因为 <c>Avalonia.Generators.props</c> 把它挂进了 <c>CompilerVisibleProperty</c>），
    /// 那就是**非推断**的证据。</para>
    ///
    /// <para><b>为什么 fail-soft</b>：它是 <c>obj/</c> 产物。干净克隆、且只跑这个测试类、
    /// 又没先构建时它不存在 —— 那种情况下本维**跳过**（读数里写明），不制造假红。
    /// 门禁与正常开发都先构建，所以这一维在真正被用到的地方都有牙齿。</para>
    /// </summary>
    [Fact]
    public void MsBuildTestimony_AgreesWithTheDeclaration()
    {
        var srcRoot = Path.Combine(SourceScanner.RepoRoot, "src");
        var testimonyFiles = Directory.Exists(srcRoot)
            ? Directory.EnumerateFiles(srcRoot, "*GeneratedMSBuildEditorConfig.editorconfig",
                                       SearchOption.AllDirectories).ToList()
            : new List<string>();

        if (testimonyFiles.Count == 0)
        {
            JudgeReadings.Write(_output, "编译绑定守卫/MSBuild 证词", new[]
            {
                "没找到任何 *.GeneratedMSBuildEditorConfig.editorconfig ⇒ 本维跳过（不做断言）。",
                "它是 obj/ 产物：干净克隆 + 未构建时本来就没有。门禁与正常开发都先构建，那时本维生效。",
            });
            return;
        }

        // 面：只对**含 .axaml 的项目**的证词做断言 —— 其余项目不引用 Avalonia 的 XAML 编译链，
        // 证词里压根没有这个属性（它们是「不适用」，不是「失败」）。把它们算成失败是假红。
        var xamlProjects = XamlProjectDirectories();

        var readings = new List<string>();
        var offenders = new List<string>();
        var checkedOwners = new HashSet<string>(StringComparer.Ordinal);
        var notApplicable = 0;

        foreach (var file in testimonyFiles.OrderBy(f => f, StringComparer.Ordinal))
        {
            // 证词文件位于 obj/<cfg>/<tfm>[/<rid>]/；同一项目的多个配置**逐个看、不去重** ——
            // 两份配置给出不同的值，本身就是缺陷。
            var line = File.ReadLines(file)
                .FirstOrDefault(l => l.StartsWith("build_property." + PropertyName, StringComparison.Ordinal));

            var value = line?.Split('=', 2) is { Length: 2 } parts ? parts[1].Trim() : null;
            var stamp = File.GetLastWriteTime(file).ToString("yyyy-MM-dd HH:mm:ss");
            var ownerDir = ProjectDirectoryOf(file);

            if (ownerDir is null || !xamlProjects.Contains(ownerDir, StringComparer.Ordinal))
            {
                notApplicable++;
                continue;
            }

            checkedOwners.Add(Rel(ownerDir));
            readings.Add($"{Rel(file)} :: {PropertyName}={value ?? "<缺失>"}  (mtime {stamp})");

            if (value is null)
                offenders.Add($"{Rel(file)}: 含 XAML 的项目，证词里却没有 {PropertyName} 这一行 —— "
                              + "属性没被暴露给编译器（检查 Avalonia.Generators.props 是否还在）");
            else if (!IsTrue(value))
                offenders.Add($"{Rel(file)}: MSBuild 求值得到 **{value}**，与显式声明的 true 不一致 ⇒ "
                              + "声明没有传导到编译期（被别的 props/csproj 后设了，或该文件的最近 "
                              + "Directory.Build.props 不是我们声明的那个）");
        }

        readings.Add($"（另有 {notApplicable} 份证词属于不含 .axaml 的项目 —— 这个属性对它们没有意义，不判）");
        JudgeReadings.Write(_output, "编译绑定守卫/MSBuild 证词", readings);

        Assert.True(checkedOwners.Count > 0,
            $"没扫到任何**含 .axaml 的项目**的 MSBuild 证词 ⇒ 这条判据实际什么都没判。"
            + $"（共扫到 {testimonyFiles.Count} 份证词，{notApplicable} 份不属于含 XAML 的项目）");

        Assert.True(offenders.Count == 0,
            $"MSBuild 落盘的证词与显式声明不一致（覆盖 {checkedOwners.Count} 个含 XAML 的项目）："
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    // ──────────────────────────────────────────────
    // 判据三：剥注释这件事本身必须有效（否则判据被自己的散文满足）
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>两侧都断言</b>（#48 的教训）：
    /// <list type="number">
    ///   <item>不剥注释时，注释里抄一遍完整元素**就会**被读到（值恰好是错的 <c>false</c>）
    ///     —— 这就是病根，不是假设；</item>
    ///   <item>剥注释后，读到的是真正的声明（<c>true</c>）。</item>
    /// </list>
    ///
    /// <para>没有这条，把 <see cref="DeclaredValue"/> 前面的 <c>StripXmlComments</c> 去掉也照样全绿，
    /// 而那时判据其实是在读注释 —— 本项目的构建文件里**正在**讨论这个属性名，
    /// 所以这不是一个假想的风险。</para>
    /// </summary>
    [Fact]
    public void CommentStripping_IsWhatKeepsProseFromSatisfyingTheSearch()
    {
        const string synthetic = """
            <Project>
              <!-- 文档里顺手续一遍（值故意与真声明相反）：
                   <AvaloniaUseCompiledBindingsByDefault>false</AvaloniaUseCompiledBindingsByDefault> -->
              <PropertyGroup>
                <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
              </PropertyGroup>
            </Project>
            """;

        // ── 子串面（判据四用的就是它）──
        // 计数口径要写清楚：**一个元素算 2 次**（开标签 + 闭标签各含一次属性名）。
        // 不剥注释 ⇒ 4 次（注释里那个元素 2 + 真声明 2）⇒ 任何 `text.Contains(...)` 式的判据
        // 在这里都**分不清**哪几次是真的。这就是「被自己的散文满足」的病根本体。
        Assert.Equal(4, CountOccurrences(synthetic, PropertyName));

        // 剥注释后 ⇒ 只剩真声明那 2 次。
        var stripped = SourceScanner.StripXmlComments(synthetic);
        Assert.Equal(2, CountOccurrences(stripped, PropertyName));

        // ── 元素面（判据一用的写法）──
        // 实测更正（2026-09-22 首跑，这条断言最初写反了）：`XDocument.Parse` 里**注释不是元素**，
        // 所以元素面**天然免疫**散文 —— 不剥注释也读到真声明的 true。
        // ⇒ 判据一里那一步 StripXmlComments 是**冗余保险**（成本为零，且把意图写在代码里）；
        //   真正**必需**它的地方是上面那条子串面（判据四）。
        Assert.Equal("true", DeclaredValue(synthetic, PropertyName));
        Assert.Equal("true", DeclaredValue(stripped, PropertyName));

        JudgeReadings.Write(_output, "编译绑定守卫/剥注释自检", new[]
        {
            "子串面：未剥注释命中 4 次（含注释里那个元素的 2 次）/ 剥注释后 2 次 —— 剥注释在这个面上是必需的",
            "元素面：两条路径都读到 true（XDocument 里注释不是元素，天然免疫）—— 那一处是冗余保险",
        });
    }

    /// <summary>子串出现次数（判据自检用；刻意不用正则，避免把正则语义混进来）。</summary>
    private static int CountOccurrences(string haystack, string needle)
        => haystack.Split(needle, StringSplitOptions.None).Length - 1;

    // ──────────────────────────────────────────────
    // 判据四：唯一出处 + 不许把 Directory.Build.props 的查找改道
    // ──────────────────────────────────────────────

    /// <summary>
    /// 两个绕过口，任一出现即红：
    /// <list type="number">
    ///   <item><b>改道</b>：<c>ImportDirectoryBuildProps=false</c> /
    ///     <c>DirectoryBuildPropsPath=…</c> 会让判据一那句「向上找最近的」结论失效
    ///     —— 那时文件还在、但根本没被导入；</item>
    ///   <item><b>第二出处</b>：除权威 <c>Directory.Build.props</c> 外，任何 <c>.csproj</c>
    ///     又设了一遍本属性 —— 项目文件在 <c>.props</c> **之后**求值，所以它能盖掉声明，
    ///     而判据一（只看 props）看不见这件事。</item>
    /// </list>
    /// </summary>
    [Fact]
    public void ThePropertyHasExactlyOneDeclarationSite_AndNothingReroutesBuildPropsLookup()
    {
        var srcRoot = Path.Combine(SourceScanner.RepoRoot, "src");
        Assert.True(Directory.Exists(srcRoot), "src/ 不存在 —— 面失效，判据会恒真");

        var buildFiles = Directory
            .EnumerateFiles(srcRoot, "*.*", SearchOption.AllDirectories)
            .Where(p => !SourceScanner.IsBuildOutput(p))
            .Where(p => p.EndsWith(".csproj", StringComparison.Ordinal)
                        || p.EndsWith(".props", StringComparison.Ordinal)
                        || p.EndsWith(".targets", StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(buildFiles.Count > 0, "src/ 下一个构建文件都没扫到 —— 枚举逻辑坏了，判据会恒真");

        var readings = new List<string>();
        var offenders = new List<string>();

        foreach (var file in buildFiles)
        {
            var text = SourceScanner.StripXmlComments(File.ReadAllText(file));
            var isAuthoritativeProps = string.Equals(
                Path.GetFileName(file), "Directory.Build.props", StringComparison.Ordinal);
            var declarations = DeclarationsOf(text, PropertyName);

            if (declarations.Count > 0)
                readings.Add($"{Rel(file)} :: {declarations.Count} 处 {PropertyName} 声明");

            // ① 改道
            foreach (var name in LookupOverrideNames)
                if (text.Contains(name, StringComparison.Ordinal))
                    offenders.Add($"{Rel(file)}: 出现了 {name} —— 它会把 Directory.Build.props 的查找改道，"
                                  + "于是判据一那句「向上找最近的」不再成立（文件在、但没被导入）");

            // ② 第二出处（权威 props 之外的任何地方）
            if (!isAuthoritativeProps && declarations.Count > 0)
                offenders.Add($"{Rel(file)}: 在这里又声明了一遍 {PropertyName} —— "
                              + "项目文件在 Directory.Build.props **之后**求值，它能盖掉声明，"
                              + "而只看 props 的判据一看不见。唯一出处必须是那个 props");
        }

        readings.Add($"共扫 {buildFiles.Count} 个构建文件");
        JudgeReadings.Write(_output, "编译绑定守卫/唯一出处", readings);

        Assert.True(offenders.Count == 0,
            $"发现 {offenders.Count} 处会让「显式声明」失效的写法："
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }
}
