using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 守卫：报警引擎评估一轮时，**变量值必须在「单个配置」的评估里就地读**，
/// 不得由调用方传一份「整轮开始时抓到的一次性快照」。
///
/// <para><b>缺陷 #70（2026-09-26 阶段 117 修）：</b><c>EvaluateAll</c> 原本在入口
/// <c>lock (_lock)</c> 里把 <c>_lastVariableValues</c> 拷成一份 <c>values</c>，
/// 再在循环里传给 <c>EvaluateCondition</c>。而 <c>System.Threading.Timer</c> 的回调排在
/// ThreadPool 上、可被抢占任意时长 ⇒ 拿着**旧值**的那一轮可能落到「值已被改回、
/// 记录已 Returned」**之后**：那一刻 <c>activeRecord</c>（唯一的非 Returned 记录）为 null、
/// 而旧值判出的 <c>isTriggered</c> 仍为真 ⇒ **新建一条 Active 记录**，同一报警出现两条；
/// <c>GetAllRecords()</c> 按 <c>OccurredAt</c> 降序 ⇒ <c>First()</c> 取到新建的那条 Active，
/// <c>AlarmEngineTests.Engine_TriggerThenReturn_CreatesReturnedRecord</c> 在 <c>:298</c> 间歇红
/// （实测复现率：Core.Tests 整程序集并行跑 12 次红 1 次）。</para>
///
/// <para><b>为什么必须是静态判据：</b>这是个**时序**缺陷 —— 行为断言只能证明
/// 「这一次跑出了 Returned」，证不了「调用方拿的是不是一份快照」。形状一旦回退
/// （再有人为了"少进几次锁"把值拷出来），行为测试也只是**偶发**红，
/// 下一个人很难把它与"环境抖动"分开 —— 这正是 #70 只能靠反复跑才被发现的原因。</para>
///
/// <para><b>三条腿互相配重（关键）：</b></para>
/// <list type="number">
///   <item><b>签名腿</b>：<c>EvaluateCondition</c> 的形参表不得出现
///   <c>Dictionary&lt;string, double&gt;</c> —— 不接受快照形参。</item>
///   <item><b>入口腿</b>：<c>EvaluateAll</c> 的方法体不得含标识符 <c>_lastVariableValues</c> ——
///   它一旦在入口方法里露头，就说明值是在**循环之前**抓的，之后整个循环拿的都是那一刻的值。
///   刻意**不列举**快照写法（<c>new Dictionary&lt;&gt;(…)</c> / <c>ToDictionary</c> /
///   <c>.Values</c> / <c>[…]</c>）—— 枚举是"面只数形状"，换个等价写法就消失。</item>
///   <item><b>就地读腿</b>：<c>EvaluateCondition</c> 的方法体**必须**出现 <c>_lastVariableValues</c>
///   —— 这是前两条的**反向配重**。前两条都是"不许有"型，最省事的"改法"是**把就地读删掉**
///   （那样引擎根本不再读值，前两条反而更绿）。🩸 面到 0 ⇒ 改性质：只有"不许有"、
///   没有"必须有"的判据立不住。</item>
/// </list>
///
/// <para><b>输入面刻意**只有那两个方法**、不是整个文件：</b>文件里
/// <c>UpdateVariableValues(Dictionary&lt;string, double&gt; values)</c> 是**写入口**、
/// 字段声明 <c>private readonly Dictionary&lt;string, double&gt; _lastVariableValues = new()</c>
/// 是**定义面** —— 拿整个文件当输入面，它们自己就会被判违规（铁律③：输入面不得含定义面）。</para>
/// </summary>
public class AlarmEngineStaleValueGuardTests
{
    /// <summary>被守的唯一文件（仓库相对路径）。</summary>
    private const string EngineSource = "src/HmiEditor.Core/Services/AlarmEngine.cs";

    /// <summary>
    /// 变量值表字段名。三条腿都围着它转；一旦改名，会先打红**扫描面自检**
    /// （见 <see cref="Scanner_ActuallyCoversTheAlarmEngine"/>），而不是让三条腿安静跳过。
    /// </summary>
    private const string ValuesField = "_lastVariableValues";

    private static readonly Regex SnapshotParameter =
        new(@"Dictionary\s*<\s*string\s*,\s*double\s*>", RegexOptions.Compiled);

    // ── 判定 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 一条腿的违规读数。<see cref="Leg"/> 让「收红集」能与被守的三条腿**交叉核对** ——
    /// 只断言"非空"的话，任何一条腿的误报看起来都一样。
    /// </summary>
    private sealed record Offender(string Leg, string Detail);

    /// <summary>
    /// 三条腿一次跑完。真实文件与**内嵌负控语料**走的是**同一个**函数 ——
    /// 否则负控证明的只是"另一段代码会红"，不是"这条判据会红"。
    ///
    /// <para>方法不在场 ⇒ 该腿不判违规（<see cref="TryLocate"/> 返回 false）。
    /// 「在不在场」由扫描面自检单独守，免得一次改名把红打到一条讲不清所以然的腿上。</para>
    /// </summary>
    private static List<Offender> FindViolations(string masked, string code)
    {
        var found = new List<Offender>();

        if (TryLocate(masked, "EvaluateAll", out _, out var allBody))
        {
            if (code[allBody.Start..allBody.End].Contains(ValuesField, StringComparison.Ordinal))
                found.Add(new Offender("入口腿",
                    $"EvaluateAll 的方法体里出现了 {ValuesField} —— 值是在整轮循环**之前**抓的，"
                    + "之后整个循环拿的都是那一刻的值（缺陷 #70）。"));
        }

        if (TryLocate(masked, "EvaluateCondition", out var condSig, out var condBody))
        {
            var signature = code[condSig.Start..condSig.End];
            if (SnapshotParameter.IsMatch(signature))
                found.Add(new Offender("签名腿",
                    $"EvaluateCondition 的形参表接了快照：{Collapse(signature)}"));

            var body = code[condBody.Start..condBody.End];
            if (!body.Contains(ValuesField, StringComparison.Ordinal))
                found.Add(new Offender("就地读腿",
                    $"EvaluateCondition 的方法体里没有 {ValuesField} —— 值表根本没人读了"
                    + "（把就地读删掉也能让另外两条腿变绿，所以这条是它们的前提）。"));
        }

        return found;
    }

    /// <summary>
    /// 定位一个方法：返回**签名区间**（<c>void 方法名(</c> 的起点 → 方法体花括号）与
    /// **方法体区间**（花括号**内部**，不含花括号本身）。
    ///
    /// <para><b>为什么在 <paramref name="masked"/> 上定位、在 <paramref name="code"/> 上取内容：</b>
    /// 花括号配对不能在带字面量的文本上做 —— 将来有人在日志串里写一个 <c>}</c>，
    /// 配对数就会偏，红在一条与被守对象无关的腿上。<c>MaskLiterals</c> 把字面量内容抹成空格、
    /// **保留插值孔**，且与 <c>StripComments</c> **长度与行数都相同**（<c>SourceScanner</c> 的不变量）
    /// ⇒ 下标可直接在两份视图之间复用。</para>
    /// </summary>
    private static bool TryLocate(
        string masked, string methodName,
        out (int Start, int End) signature, out (int Start, int End) body)
    {
        signature = default;
        body = default;

        var decl = $"void {methodName}(";
        var at = masked.IndexOf(decl, StringComparison.Ordinal);
        if (at < 0) return false;

        Assert.True(masked.IndexOf(decl, at + 1, StringComparison.Ordinal) < 0,
            $"`{decl}` 在扫描面里出现多次 —— 定位不唯一，判据会判错方法。");

        var open = masked.IndexOf('{', at);
        Assert.True(open > at, $"`{decl}` 之后找不到方法体起始花括号。");

        signature = (at, open);
        body = (open + 1, MatchBrace(masked, open));
        return true;
    }

    /// <summary>返回与 <paramref name="open"/> 处的 <c>{</c> 配对的那个 <c>}</c> 的下标。</summary>
    private static int MatchBrace(string masked, int open)
    {
        var depth = 0;
        for (var i = open; i < masked.Length; i++)
        {
            if (masked[i] == '{') depth++;
            else if (masked[i] == '}' && --depth == 0) return i;
        }

        Assert.Fail($"花括号不平衡（起始下标 {open}）—— 扫描面不是一个完整的方法体。");
        return -1;
    }

    /// <summary>把多行签名压成一行，让红消息里能直接看出形参长什么样。</summary>
    private static string Collapse(string text) =>
        Regex.Replace(text, @"\s+", " ").Trim();

    private static (string Masked, string Code) ReadEngine()
    {
        var full = Path.Combine(SourceScanner.RepoRoot, EngineSource);
        Assert.True(File.Exists(full), $"未找到被守文件：{full}");
        return (SourceScanner.ReadMasked(full), SourceScanner.ReadCode(full));
    }

    // ── 判据 ───────────────────────────────────────────────────────────────

    /// <summary>主判据：真实源文件上三条腿全绿。</summary>
    [Fact]
    public void Engine_ReadsVariableValuesInPlace()
    {
        var (masked, code) = ReadEngine();
        var offenders = FindViolations(masked, code);

        Assert.True(offenders.Count == 0,
            $"报警引擎的变量值必须**就地读**（缺陷 #70：入口抓快照 ＋ Timer 回调被抢占任意时长 "
            + $"⇒ 拿旧值判真 ⇒ 同一报警两条记录 ⇒ AlarmEngineTests 在 :298 间歇红）。{Environment.NewLine}"
            + $"违规腿（{offenders.Count}）：{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", offenders.Select(o => $"[{o.Leg}] {o.Detail}")));
    }

    /// <summary>
    /// 扫描面自检：三条腿都得有对象可判。
    ///
    /// <para>静态扫描最容易「扫了个空所以全绿」—— 本守卫尤其危险：
    /// <see cref="FindViolations"/> 对**不在场**的方法刻意不判违规（免得改名把红打到错腿上），
    /// 于是两个方法被改名/拆分之后，主判据会**安静地变绿**。
    /// 所以这里把「两个方法在场、字段名还在、文件有实体」这几条前提单独钉住。</para>
    ///
    /// <para>下界刻意放低（只要求 ≥300 行，实测 401）：🩸 面掉下来时先红的应该是**自检**，
    /// 而下界贴着实测值会让每一次**正确**的收缩都多红一条。</para>
    /// </summary>
    [Fact]
    public void Scanner_ActuallyCoversTheAlarmEngine()
    {
        var (masked, code) = ReadEngine();
        var full = Path.Combine(SourceScanner.RepoRoot, EngineSource);

        var lines = File.ReadAllLines(full).Length;
        Assert.True(lines >= 300, $"{EngineSource} 只有 {lines} 行 —— 疑似读错文件或文件被截断。");

        Assert.True(code.Contains("void EvaluateAll(", StringComparison.Ordinal),
            $"{EngineSource} 里找不到 `void EvaluateAll(` —— 入口腿没有对象可判（改名了？）。");
        Assert.True(code.Contains("void EvaluateCondition(", StringComparison.Ordinal),
            $"{EngineSource} 里找不到 `void EvaluateCondition(` —— 签名腿/就地读腿没有对象可判（改名了？）。");
        Assert.True(code.Contains(ValuesField, StringComparison.Ordinal),
            $"{EngineSource} 里找不到字段 `{ValuesField}` —— 就地读腿会误报「值表没人读」（改名了？）。");

        // 两份视图的下标要能互相复用，前提是长度一致（SourceScanner 的不变量）。
        Assert.True(masked.Length == code.Length,
            $"剥注释视图（{code.Length}）与抹字面量视图（{masked.Length}）长度不等 —— 下标复用不成立。");
    }

    // ── 负控（内嵌）与正控（全合成） ────────────────────────────────────────

    /// <summary>
    /// 负控 ①：**入口腿**必须红，且点名的就是这一条腿。
    ///
    /// <para>语料**逐字**取自修复前的产品代码
    /// （<c>git show HEAD:src/HmiEditor.Core/Services/AlarmEngine.cs</c> 的 223–249 行）——
    /// 不手写、不改写：手写的"看起来像缺陷"的语料证明不了判据认得出**真的那一版**。</para>
    ///
    /// <para>🩸 只断言"非空"不够：另两条腿若一起误报，读数看起来一样。
    /// 所以同时钉**命中数 == 1** 与**命中腿 == 入口腿**（收红集与汇总行交叉核对）。</para>
    /// </summary>
    [Fact]
    public void NegativeControl_EntrySnapshotIsRejected()
    {
        const string defect = """
            private void EvaluateAll(object? state)
            {
                if (_disposed || !IsRunning) return;

                List<AlarmConfig> configs;
                Dictionary<string, double> values;

                lock (_lock)
                {
                    configs = _alarmConfigs.Values.Where(c => c.IsEnabled).ToList();
                    values = new Dictionary<string, double>(_lastVariableValues);
                }

                var now = DateTime.UtcNow;

                foreach (var config in configs)
                {
                    try
                    {
                        EvaluateCondition(config, values, now);
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke($"[AlarmEngine] 评估异常: {config.Name}: {ex.Message}");
                    }
                }
            }
            """;

        var offenders = FindViolations(SourceScanner.MaskLiterals(defect), SourceScanner.StripComments(defect));

        var only = Assert.Single(offenders);
        Assert.Equal("入口腿", only.Leg);
    }

    /// <summary>
    /// 负控 ②：**签名腿**与**就地读腿**必须红。
    ///
    /// <para>语料第一行**逐字**取自修复前的产品代码（HEAD 第 251 行）；方法体是**合成**的空体 ——
    /// 这两条腿只看"形参表"与"体内有没有 <c>_lastVariableValues</c>"，与体内容无关
    /// （🩸 内嵌负控的正控全合成：负控语料允许只为目标腿而合成）。</para>
    /// </summary>
    [Fact]
    public void NegativeControl_SnapshotParameterIsRejected()
    {
        const string defect = """
            private void EvaluateCondition(AlarmConfig config, Dictionary<string, double> values, DateTime now)
            {
            }
            """;

        var offenders = FindViolations(SourceScanner.MaskLiterals(defect), SourceScanner.StripComments(defect));

        Assert.Equal(2, offenders.Count);
        Assert.Equal(new[] { "签名腿", "就地读腿" }, offenders.Select(o => o.Leg));
    }

    /// <summary>
    /// 负控 ③：就地读被**注释掉**时必须红。
    ///
    /// <para>这条钉的是「判据真的在**剥注释之后**的文本上判」—— 若哪天把 <c>ReadCode</c>
    /// 换成 <c>File.ReadAllText</c>，注释里的那串字会把就地读腿**喂绿**（假通过），
    /// 而"值表没人读"这个后果一模一样。</para>
    /// </summary>
    [Fact]
    public void NegativeControl_CommentedOutInPlaceReadIsRejected()
    {
        const string defect = """
            private void EvaluateCondition(AlarmConfig config, DateTime now)
            {
                // double currentValue;
                // if (!_lastVariableValues.TryGetValue(config.Condition.VariableId, out currentValue))
                //     return;
                double currentValue = 0;
            }
            """;

        var offenders = FindViolations(SourceScanner.MaskLiterals(defect), SourceScanner.StripComments(defect));

        var only = Assert.Single(offenders);
        Assert.Equal("就地读腿", only.Leg);
    }

    /// <summary>
    /// 正控（**全合成**）：修好之后的形状必须**零违规**。
    ///
    /// <para>没有这一条，三个负控证明的只是"这三条腿总会红"—— 那是个永不绿的死判据。
    /// 语料与真实文件同形但**不引用**它：真实文件将来合法演变
    /// （例如就地读抽成私有小方法）时，这条正控仍在，用来分辨「判据坏了」与「产品变了」。</para>
    /// </summary>
    [Fact]
    public void PositiveControl_FixedShapeIsAccepted()
    {
        const string fixedShape = """
            private void EvaluateAll(object? state)
            {
                if (_disposed || !IsRunning) return;

                List<AlarmConfig> configs;

                lock (_lock)
                {
                    configs = _alarmConfigs.Values.Where(c => c.IsEnabled).ToList();
                }

                var now = DateTime.UtcNow;

                foreach (var config in configs)
                {
                    try
                    {
                        EvaluateCondition(config, now);
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke($"[AlarmEngine] 评估异常: {config.Name}: {ex.Message}");
                    }
                }
            }

            private void EvaluateCondition(AlarmConfig config, DateTime now)
            {
                double currentValue;
                lock (_lock)
                {
                    if (!_lastVariableValues.TryGetValue(config.Condition.VariableId, out currentValue))
                        return;
                }
            }
            """;

        var offenders = FindViolations(SourceScanner.MaskLiterals(fixedShape), SourceScanner.StripComments(fixedShape));

        Assert.True(offenders.Count == 0,
            $"修好后的形状被判违规了 —— 判据过紧（不是产品问题）：{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", offenders.Select(o => $"[{o.Leg}] {o.Detail}")));
    }
}
