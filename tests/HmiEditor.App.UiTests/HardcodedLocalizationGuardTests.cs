using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **硬编码中文守卫**：App 层「该本地化但还没本地化」的字符串**只许减少，不许增加**。
///
/// <para><b>为什么需要它</b>：登记表把「运行时动态文案未入字典」列为遗留项已经很多轮了，
/// 但每一轮都只是"这一批补完了"，**没有任何机制阻止新功能继续写死中文** ——
/// 于是清单永远追不上。这与本项目处理「面板上有、其实不生效」用的
/// <c>ConfigFieldInventory</c> 是同一套路：**先把结论固化成数据，再让守卫盯住它**。</para>
///
/// <para><b>判据（分类式，不是 sink 式）</b>：扫 <c>src/HmiEditor.App</c> 下所有
/// <c>.cs/.axaml</c>，剥掉注释后取出**含中日韩字符的字符串字面量**，扣掉下面几类明确排除项，
/// 其余全算"待本地化"。刻意**不用"看这一行是不是控件赋值"**那种写法：
/// 多行实参（例如 <c>PopulateTaggedCombo(CboAlarmType, new (string,object)[] { ("上限报警 (H)", …) })</c>）
/// 的 sink 在**上一行**，按行匹配会整片漏掉 —— 实测那样只有 98 条，而真实是 226 条。</para>
///
/// <para><b>排除项与理由（逐条可审）</b>：</para>
/// <list type="bullet">
/// <item><c>Debug.WriteLine</c> / <c>Console.</c> / <c>Trace.</c> / <c>DiagnosticLog</c>：调试输出，用户看不到。
/// <b><c>DiagnosticLog</c> 是实测补上的</b>：<c>PreviewRuntimeService</c> 的诊断出口默认写
/// <c>Debug.WriteLine</c>，而 <c>MainWindow</c> **不注入**它 ⇒ 应用里它只进 Debug；
/// 但 sink 的定义在**属性初始化器**里（不在调用点那一行），按行判定看不见它，
/// 于是那条中文消息被判成"待本地化"（假阳性）。处置不是给它建字典键（没人会看到），
/// 而是**让 sink 的名字自己声明类别**（<c>Log</c> → <c>DiagnosticLog</c>）并把它加进本表 ——
/// 与 <c>Debug.</c>/<c>Console.</c>/<c>Trace.</c> 同类：凭"名字里写明的出口"判定，
/// 而不是靠"这一行长什么样"。</item>
/// <item>异常消息（<c>throw new …Exception("…")</c>）：面向开发者；即便被 <c>ex.Message</c> 透出，
/// 也属于"诊断信息不该翻译"的惯例（翻译后无法搜索）。</item>
/// <item>已本地化的语句（出现 <c>AppLocalization</c>/<c>LocService</c>）：那里的中文通常只是传参。
/// <b>但这一条以前是"按行整片豁免"，实测在 <c>DeviceManagerView</c>/<c>DownloadToHmiView</c> 里
/// 连坐藏了 13 条用户可见文案</b>：<c>Log($"[提示] {AppLocalization.Service[…] }")</c> 这类日志行，
/// 正文入了字典，段落标签 <c>[提示]</c> 却是写死的中文，而整行因为含 <c>AppLocalization</c> 被豁免。
/// 现改为"按语句、只豁免包裹它的那个调用"，这 13 条标签已入字典（<c>Lang.DeviceLogTag*</c>）。</item>
/// <item><b>正则字面量</b>（<c>new Regex(@"…")</c>）：那是**模式**不是文案 ——
/// <c>SimulatorBuildService</c> 里那条 <c>@"(?:error:|错误:|fatal error|…)"</c> 用来识别编译器的错误行，
/// **翻译它会直接改变匹配行为**（英文编译器输出就再也认不出来了）。它含中文只是因为它同时要认
/// 中文输出，与"用户看到的文案"无关。</item>
/// <item><c>AgentToolFactory.cs</c> / <c>AgentSystemPrompt.cs</c>：**AI 提示词**。翻译它会改变模型行为，
/// 属独立议题（要与 prompt 契约一起评估），不该混进 UI 文案清理。</item>
/// <item><c>DockEventRoutingDiagnostics.cs</c>：内部诊断报告（菜单里手动触发的排障工具）。</item>
/// <item><c>Program.cs</c> / <c>AgentHost.cs</c>：启动期日志与内部异常文案。</item>
/// </list>
///
/// <para><b>维护方式</b>：本地化掉一批后，**把对应文件的数字改小**（守卫按**相等**判定，
/// 数字既不能变大也不能"忘了改"）。若确认某条属于上面的排除类别，应改进分类规则而不是调数字。</para>
/// </summary>
public class HardcodedLocalizationGuardTests
{
    private const string AppRootRelative = "src/HmiEditor.App";

    /// <summary>
    /// 各文件"待本地化"字面量条数的**实测基线**（2026-09-17）。
    /// 只许减小；减小后必须同步改这里（相等判定，防漂移）。
    ///
    /// <para><b>2026-09-17 判据修正带来的一次性重算</b>：扫描从"按行"改成"按语句"
    /// （见 <see cref="IsExcluded"/>）后总数为 187（旧基线 226）。其中
    /// <c>TemplateManager.cs</c> 由 1 **升到 4** —— 这是**量具修正，不是新增代码**：
    /// 旧规则按行整片豁免"含 <c>AppLocalization</c> 的行"，而该文件里
    /// <c>new() { …, Name = "启动", Text = AppLocalization.Service[…] }</c> 这种行
    /// 既有硬编码<b>控件名</b>又有本地化调用，于是 3 个控件名被连坐豁免。
    /// 控件名（<c>HmiControl.Name</c>）在项目树/属性面板里用户可见，因此**照实计入**；
    /// 但"控件名要不要跟着 UI 语言翻译"是产品决定（改了会让生成物随语言变化），
    /// 已登记为待决项，不在本批次里擅自改动。</para>
    /// </summary>
    private static readonly Dictionary<string, int> Baseline = new(StringComparer.Ordinal)
    {
        // 27 → 2：下拉显示名/列表标签已入字典；剩下两条是**落盘的数据标识**
        // （VariableAlarmConfig.Name 的默认值与兜底值，见登记表 §4 第 27c 项），
        // 翻译它们会让工程文件内容随 UI 语言变化 ⇒ 不做，等产品决策。
        ["Views/VariableManagerView.axaml.cs"] = 2,
        // 24 → 2：操作名/状态提示/撤销描述已入字典；剩下两条是**落盘的资源库元数据**
        // （HmiResourceItem 的 SubCategory/Description，_resourceLibraryStore 会存盘，
        // 见登记表 §4 第 27c 项）⇒ 与"落盘标识符"同类，等产品决策。
        ["MainWindow.axaml.cs"] = 2,
        // 3 → 1：文件类型显示名已入字典；剩下的是**写进资源条目的图片名**
        // （`AI图片_{w}x{h}` 会随资源一起落盘，见登记表 §4 第 27c 项）。
        ["Views/AiIconDialog.axaml.cs"] = 1,
        ["Controls/TemplateManager.cs"] = 4,   // 判据修正后由 1 升到 4：3 个控件名不再被连坐豁免
    };

    /// <summary>整文件排除（理由见类注释）。</summary>
    private static readonly string[] ExcludedFiles =
    {
        "AgentToolFactory.cs",      // AI 提示词
        "AgentSystemPrompt.cs",     // AI 提示词
        "DockEventRoutingDiagnostics.cs", // 内部诊断报告
        "Program.cs",               // 启动期日志
        "AgentHost.cs",             // 内部异常文案
    };

    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);
    private static readonly Regex Literal = new("\"((?:[^\"\\\\\n]|\\\\.)*)\"", RegexOptions.Compiled);
    /// <summary>只用来判定"这个调用头是不是本地化 API"（用于上面的"非首实参不豁免"例外）。</summary>
    private static readonly Regex LocalizationCall = new(@"AppLocalization|LocService", RegexOptions.Compiled);

    private static readonly Regex ExcludedStatement = new(
        @"Debug\.WriteLine|Console\.|Trace\.|DiagnosticLog|Regex|throw new|Exception\(|AppLocalization|LocService",
        RegexOptions.Compiled);

    // ────────────────────────── 扫描 ──────────────────────────

    /// <summary>剥掉注释与字符串外的内容保留；字符串字面量原样保留（只处理行/块注释）。</summary>
    internal static string StripComments(string source)
    {
        var sb = new StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
                continue;
            }
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i = Math.Min(i + 2, source.Length);
                continue;
            }
            // XAML 注释是 <!-- -->（.cs 里不会出现，一并处理免得两套逻辑）
            if (c == '<' && i + 3 < source.Length && source[i + 1] == '!' && source[i + 2] == '-' && source[i + 3] == '-')
            {
                i += 4;
                while (i + 2 < source.Length && !(source[i] == '-' && source[i + 1] == '-' && source[i + 2] == '>')) i++;
                i = Math.Min(i + 3, source.Length);
                continue;
            }
            if (c == '"')
            {
                var j = i + 1;
                while (j < source.Length)
                {
                    if (source[j] == '\\') { j += 2; continue; }
                    if (source[j] == '"') { j++; break; }
                    if (source[j] == '\n') break;
                    j++;
                }
                sb.Append(source, i, Math.Min(j, source.Length) - i);
                i = j;
                continue;
            }

            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// 字面量是否落在上面声明的排除类别里。**按"包住它的那个调用"判断，而不是按行**。
    ///
    /// <para>按行匹配有实测漏网：<c>throw new ArgumentOutOfRangeException(</c> 换行后，
    /// 参数列表里的中文（<c>EditorCommandCatalog.cs</c> 的默认分支）关键字在**上一行**，
    /// 于是这条明确写着"异常消息面向开发者"的字符串被算成了待本地化 ——
    /// 判据与实现自相矛盾。这里从字面量往回找**最近一个未闭合的 <c>(</c>**（且不跨过
    /// <c>;</c>/<c>{</c>/<c>}</c>/<c>(</c>），用该调用的"头"去匹配排除词；找不到包裹调用时
    /// （普通赋值、字段初始化、switch 分支）退回**整条语句前缀**。</para>
    ///
    /// <para>退回语句前缀会连带排除同一语句里的其他中文（例如 <c>Loc["k"] + "中文参数"</c>），
    /// 这是刻意的 —— 类注释里"已本地化的行"本来就按整行排除；但它不跨语句，
    /// 也不会跨到**更外层的调用**（<c>;</c>/<c>{</c>/<c>}</c>/<c>(</c> 都算边界），
    /// 所以 <c>foreach (var x in LocService[…]) items.Add(new Item("中文"))</c>
    /// 里那个中文实参仍会被计入（见 <c>IsExcluded_*</c> 测试）。</para>
    /// </summary>
    internal static bool IsExcluded(string code, int literalStart)
    {
        // 往回找包住该字面量的最近一个 '('；已配对的括号跳过，语句/块边界直接停
        var depth = 0;
        var open = -1;
        for (var i = literalStart - 1; i >= 0; i--)
        {
            var c = code[i];
            if (c == ')') depth++;
            else if (c == '(')
            {
                if (depth == 0) { open = i; break; }
                depth--;
            }
            else if (depth == 0 && (c == ';' || c == '{' || c == '}')) break;
        }

        var anchor = open < 0 ? literalStart : open;
        var stmtStart = anchor;
        // 边界含 `,`：对象/集合初始化器里的**兄弟属性**必须切断 —— 否则
        // `new ChatEntry { RoleDisplay = Loc[…], Content = "中文" }` 里的 `Content`
        // 会继承兄弟的 `AppLocalization` 豁免（实测踩到：两条真正的用户可见聊天文案被藏了）。
        while (stmtStart > 0 && code[stmtStart - 1] != ';' && code[stmtStart - 1] != '{'
               && code[stmtStart - 1] != '}' && code[stmtStart - 1] != '(' && code[stmtStart - 1] != ',')
            stmtStart--;

        var head = code.Substring(stmtStart, anchor - stmtStart);
        if (!ExcludedStatement.IsMatch(head)) return false;

        // 例外：本地化调用的**非首实参**若是字面量，那是"值"而不是"键/模板" ——
        // 它会原样显示给用户（英文界面下就是中英混排），所以**不豁免**。
        // 实测踩到：`Loc.Format("ai.gen.failed", "PNG 渲染失败")` 里的中文是状态栏上的一句话。
        // 判据：调用括号与字面量之间若存在**顶层逗号**，说明它不是第一个实参。
        // （副作用：`Format("单一实参里带,逗号")` 会被误判成"非首实参" ⇒ 多算不放过，
        //  方向是"宁可多报"，不会漏。）
        // 只对**本地化调用**生效：`throw new X(nameof(a), a, "中文")` 的实参列表里也有逗号，
        // 但那一条属于"异常消息"类别，不该被这条例外翻回来（实测：一刀切会让它重新计入）。
        if (open >= 0 && LocalizationCall.IsMatch(head) && HasTopLevelComma(code, open, literalStart))
            return false;

        return true;
    }

    /// <summary>从 <paramref name="openParen"/>+1 到 <paramref name="end"/> 之间是否有**顶层**逗号。</summary>
    private static bool HasTopLevelComma(string code, int openParen, int end)
    {
        var depth = 0;
        for (var i = openParen + 1; i < end; i++)
        {
            switch (code[i])
            {
                case '(': depth++; break;
                case ')': depth--; break;
                case ',' when depth == 0: return true;
            }
        }
        return false;
    }

    /// <param name="ignoreExclusions">
    /// 为 <c>true</c> 时**不套用**排除类别 —— 只给自检用：扫描面上已经没有未分类的中文之后，
    /// "扫到多少条"不再是进度表，于是自检不能再挂在条数上（0 的一半还是 0 ⇒ 空跑恒绿）。
    /// 改成"关掉排除项仍然扫得到东西"，这才是**独立于基线**的仪器自检。
    /// </param>
    private static Dictionary<string, int> Scan(bool ignoreExclusions = false)
    {
        var root = Path.Combine(SourceScanner.RepoRoot, AppRootRelative);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                     .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                                 || p.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
                     .Where(p => !SourceScanner.IsBuildOutput(p)))
        {
            var fileName = Path.GetFileName(path);
            if (ExcludedFiles.Contains(fileName, StringComparer.Ordinal)) continue;

            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            var code = StripComments(File.ReadAllText(path));

            var hits = 0;
            foreach (Match m in Literal.Matches(code))
            {
                if (!Cjk.IsMatch(m.Groups[1].Value)) continue;
                if (!ignoreExclusions && IsExcluded(code, m.Index)) continue;
                hits++;
            }

            if (hits > 0) counts[relative] = hits;
        }

        return counts;
    }

    // ────────────────────────── 判据 ──────────────────────────

    [Fact]
    public void Scanner_StillWorks_WhenExclusionsAreDisabled()
    {
        // 仪器自检，且**不挂在基线上**：扫描面清零之后，"扫到 0 条"既可能是真的干净、
        // 也可能是扫描器坏了。关掉排除类别再扫一次 —— 还有东西，说明扫描器活着。
        var raw = Scan(ignoreExclusions: true);

        Assert.True(raw.Count >= 3,
            $"关掉排除项也只扫到 {raw.Count} 个文件含中文 —— 扫描器可能失效");
        Assert.True(raw.Values.Sum() >= 15,
            $"关掉排除项只扫到 {raw.Values.Sum()} 条 —— 扫描器可能失效");
    }

    [Fact]
    public void NoUnclassifiedHardcodedChinese()
    {
        var actual = Scan();

        // 扫描面已清零：App 层不再有"该本地化却没本地化"的中文。
        // 剩下的中文都落在 Scan 的排除类别里（调试输出 / 异常消息 / 诊断 sink / 正则 / 已本地化语句），
        // 每一类在类注释里都有理由；进来一条新的未分类文案，这里立刻红。
        Assert.True(actual.Count == 0,
            "这些文件还有**未分类**的硬编码中文（要么入字典，要么补一条有理由的排除类别，"
            + "不要只调数字）：\n    "
            + string.Join("\n    ", actual.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}: {kv.Value}")));
    }

    // ────────────────────────── 扫描器自检 ──────────────────────────

    [Fact]
    public void StripComments_RemovesCommentsButKeepsStringLiterals()
    {
        const string src = """
            // "注释里的中文" 不该被计入
            var a = "真实的中文";
            /* 块注释里的中文
               "也不该计入" */
            var b = "another";
            """;

        var stripped = StripComments(src);

        Assert.Contains("真实的中文", stripped, StringComparison.Ordinal);
        Assert.DoesNotContain("注释里的中文", stripped, StringComparison.Ordinal);
        Assert.DoesNotContain("也不该计入", stripped, StringComparison.Ordinal);
        Assert.Contains("another", stripped, StringComparison.Ordinal);
    }

    [Fact]
    public void ExcludedStatement_MatchesTheJustifiedCategories()
    {
        // 负例：这四类都不该被算作"待本地化"
        Assert.Matches(ExcludedStatement, """Debug.WriteLine("调试中文");""");
        Assert.Matches(ExcludedStatement, """throw new InvalidOperationException("内部中文");""");
        Assert.Matches(ExcludedStatement, """var s = AppLocalization.Service["key"] + "中文参数";""");

        // 正例：普通控件赋值必须被算进来
        Assert.DoesNotMatch(ExcludedStatement, """StatusText.Text = "文件不存在";""");
    }

    /// <summary>把源码里第一个中文字面量的位置喂给 <see cref="IsExcluded"/>。</summary>
    private static bool ExcludedIn(string source)
    {
        var code = StripComments(source);
        foreach (Match m in Literal.Matches(code))
            if (Cjk.IsMatch(m.Groups[1].Value))
                return IsExcluded(code, m.Index);
        throw new InvalidOperationException("这段源码里没有中文实参字面量，测不到东西");
    }

    [Fact]
    public void IsExcluded_SeesThrowNewAcrossLines()
    {
        // 回归：异常消息的关键字在**上一行**时也必须排除（修复前这条被算成待本地化）
        const string src = """
            _ => throw new ArgumentOutOfRangeException(
                nameof(action), action, "动作未映射"),
            """;
        Assert.True(ExcludedIn(src), "多行 throw 的异常消息应被排除");
    }

    [Fact]
    public void IsExcluded_DoesNotLeakIntoTheNextStatement()
    {
        // 前一条语句已本地化，不代表后一条语句的中文可以豁免
        const string src = """
            A.Text = LocService["标题"];
            B.Text = "文件不存在";
            """;
        var code = StripComments(src);
        var matches = Literal.Matches(code).Cast<Match>().Where(m => Cjk.IsMatch(m.Groups[1].Value)).ToList();
        Assert.Equal(2, matches.Count);
        Assert.True(IsExcluded(code, matches[0].Index), "已本地化语句里的传参中文应被排除");
        Assert.False(IsExcluded(code, matches[1].Index), "下一条语句的硬编码中文不该被连带豁免");
    }

    /// <summary>
    /// 回归（实测踩到）：对象/集合初始化器里，**兄弟属性**用了本地化 API
    /// <b>不能</b>豁免本属性 —— 否则会重演 §9.1b 那种连坐，
    /// 而这次藏的是两条真正的用户可见聊天文案。
    /// </summary>
    [Fact]
    public void IsExcluded_DoesNotInheritASiblingPropertysExemption()
    {
        const string src = """
            var entry = new ChatEntry
            {
                RoleDisplay = LocService[Lang.AiRoleAssistant],
                Content = "图片生成失败，请重试或修改描述。"
            };
            """;
        Assert.False(ExcludedIn(src), "兄弟属性里的本地化调用不该豁免本属性的中文");
    }

    /// <summary>
    /// 本地化调用的实参里：**第一个是键**（属"已本地化语句"，豁免），
    /// **后面的中文是值** —— 它会原样显示给用户（英文界面下中英混排），必须计入。
    /// 实测：`Format("ai.gen.failed", "PNG 渲染失败")` 就是这种。
    /// </summary>
    [Fact]
    public void IsExcluded_CountsLocalizedCallArgumentsAfterTheFirst()
    {
        Assert.False(ExcludedIn("""StatusText.Text = LocService.Format("ai.gen.failed", "PNG 渲染失败");"""),
            "第二个实参起的中文是**值**，不该被豁免");
        Assert.True(ExcludedIn("""StatusText.Text = LocService.Format("中文键");"""),
            "作为键的字面量仍属已本地化语句");
    }

    [Fact]
    public void IsExcluded_CountsLiteralInsideACallEvenUnderLocalizedForeach()
    {
        // 包裹它的是 new Item(...)，而不是外层含 LocService 的 foreach —— 必须计入
        const string src = """
            foreach (var x in LocService["list"])
                items.Add(new Item("中文"));
            """;
        Assert.False(ExcludedIn(src), "被普通调用包裹的中文实参不该被外层循环头上的 LocService 豁免");
    }
}
