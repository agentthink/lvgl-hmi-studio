using System.Text.RegularExpressions;
using HmiEditor.CodeGen;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// <c>generated_runtime.h</c> 的生成契约，以及 <see cref="RuntimeConfig"/> 十个字段
/// **下发状态**的守卫。
///
/// <para>为什么这个类存在：那十个字段在工程设置面板里都有 UI、其中一部分还能被 AI 工具改，
/// 但此前只有 <c>BootScreenId</c> 真的走到了设备 —— 其余改完只写进工程 JSON，设备行为纹丝不动。
/// 本轮把固件端已有接收位点的两个（<see cref="RuntimeConfig.ScanIntervalMs"/> /
/// <see cref="RuntimeConfig.LogLevel"/>）接上，剩下的如实标注为"未下发 / 待立功能"。
/// 本类钉住的就是这个状态：<b>多接一个字段、或产物里悄悄多出一个宏，都必须在这里显式改一行</b>
/// —— 否则"配了不通设备"会以新的形式再长回来。</para>
/// </summary>
public class RuntimeConfigCodeGenTests
{
    // ══════════════════ A. 值 → 宏 ══════════════════

    [Fact]
    public void ScanIntervalMs_EmitsPollIntervalMacro()
    {
        var header = RuntimeConfigCodeGen.Generate(new RuntimeConfig { ScanIntervalMs = 250 });

        Assert.Contains("#define HMI_POLL_INTERVAL_MS 250", header);
    }

    /// <summary>
    /// 每个配置值**只**影响它自己那个宏 —— 一个改动牵动别的输出，说明生成器里混进了不该有的耦合
    /// （例如把日志级别的分支写进了轮询周期那段）。判据是"逐行比对后只差一行"。
    /// </summary>
    [Fact]
    public void EachConfigValue_OnlyChangesItsOwnMacro()
    {
        var baseline = Lines(RuntimeConfigCodeGen.Generate(new RuntimeConfig()));

        var scanDiff = ChangedLines(
            baseline, Lines(RuntimeConfigCodeGen.Generate(new RuntimeConfig { ScanIntervalMs = 250 })));
        Assert.Single(scanDiff);
        Assert.Contains("HMI_POLL_INTERVAL_MS", scanDiff[0]);

        var logDiff = ChangedLines(
            baseline, Lines(RuntimeConfigCodeGen.Generate(new RuntimeConfig { LogLevel = LogLevel.Error })));
        Assert.Single(logDiff);
        Assert.Contains("HMI_LOG_LEVEL", logDiff[0]);
    }

    /// <summary>
    /// 非法周期挡住并且**留下诊断**。0 或负值会让固件采集线程忙等烧 CPU，
    /// 而"静默回退"与"根本没配"在产物里看不出区别 ⇒ 必须有一行说明原因。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(60001)]
    [InlineData(int.MaxValue)]
    public void ScanIntervalMs_OutOfRange_FallsBackToFirmwareDefaultWithDiagnostic(int bad)
    {
        var header = RuntimeConfigCodeGen.Generate(new RuntimeConfig { ScanIntervalMs = bad });

        var fallback = $"#define HMI_POLL_INTERVAL_MS {RuntimeConfigCodeGen.FallbackScanIntervalMs}";
        Assert.Contains(fallback, header);
        Assert.Contains("超出合法区间", header);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(60000)]
    public void ScanIntervalMs_WithinRange_IsEmittedAsIs(int good)
    {
        var header = RuntimeConfigCodeGen.Generate(new RuntimeConfig { ScanIntervalMs = good });

        var expected = $"#define HMI_POLL_INTERVAL_MS {good}";
        Assert.Contains(expected, header);
        Assert.DoesNotContain("超出合法区间", header);
    }

    // ══════════════════ B. 日志门槛 ══════════════════

    /// <summary>
    /// 门槛与级别常量都必须**从枚举推**。判据也从枚举推（不抄字面量）——
    /// 这样枚举改了、生成器没跟上的话，是这条测试先红，而不是设备端静默用了旧值。
    /// </summary>
    [Fact]
    public void LogLevel_ThresholdAndConstants_AreDerivedFromTheEnum()
    {
        foreach (var level in Enum.GetValues<LogLevel>())
        {
            var header = RuntimeConfigCodeGen.Generate(new RuntimeConfig { LogLevel = level });

            var threshold = $"#define HMI_LOG_LEVEL {(int)level}";
            Assert.Contains(threshold, header);

            // 级别常量那几行做了列宽对齐（等号后空白数不等），所以按空白宽松匹配，
            // 而不是写死一个空格 —— 否则对齐宽度一变测试就红，红的原因还跟被测语义无关。
            var constant = $@"^#define[ \t]+{RuntimeConfigCodeGen.LogLevelMacro(level)}[ \t]+{(int)level}$";
            Assert.True(Regex.IsMatch(header, constant, RegexOptions.Multiline),
                $"级别常量未按枚举发射：{RuntimeConfigCodeGen.LogLevelMacro(level)} 应为 {(int)level}");
        }
    }

    [Fact]
    public void LogLevel_InvalidValue_FallsBackToEnumDefaultWithDiagnostic()
    {
        var header = RuntimeConfigCodeGen.Generate(new RuntimeConfig { LogLevel = (LogLevel)99 });

        var fallback = $"#define HMI_LOG_LEVEL {(int)RuntimeConfigCodeGen.FallbackLogLevel}";
        Assert.Contains(fallback, header);
        Assert.Contains("不是合法级别", header);
    }

    /// <summary>级别常量的**枚举 → C 名**映射只有一处实现，名字形态本身也是契约。</summary>
    [Fact]
    public void LogLevelMacro_UsesDistinctPrefix_NotConfusableWithThreshold()
    {
        Assert.Equal("HMI_LOG_LVL_DEBUG", RuntimeConfigCodeGen.LogLevelMacro(LogLevel.Debug));
        Assert.Equal("HMI_LOG_LVL_WARNING", RuntimeConfigCodeGen.LogLevelMacro(LogLevel.Warning));

        // 常量名与门槛名不能撞（撞了就没法区分"门槛"与"某个级别"）
        Assert.NotEqual("HMI_LOG_LEVEL", RuntimeConfigCodeGen.LogLevelMacro(LogLevel.Debug));
    }

    // ══════════════════ C. 产物形态 ══════════════════

    /// <summary>
    /// 产物里的宏**精确等于**这张清单 —— 多一个就红。
    ///
    /// <para>这是本类最值钱的一条：它是"十个字段下发状态"在产物侧的对应物。
    /// 有人给某个原先未下发的字段接上宏时，这里会红，逼他去同步更新
    /// ①<see cref="RuntimeConfig"/> 里该字段的状态标注 ②本清单 ③下面的字段期望表。</para>
    /// </summary>
    [Fact]
    public void Header_MacroSet_IsExactlyTheExpectedList()
    {
        var header = RuntimeConfigCodeGen.Generate(new RuntimeConfig());

        var expected = new[]
        {
            "HMI_GENERATED_RUNTIME_H",
            "HMI_LOG_LEVEL",
            "HMI_LOG_LVL_DEBUG",
            "HMI_LOG_LVL_ERROR",
            "HMI_LOG_LVL_FATAL",
            "HMI_LOG_LVL_INFO",
            "HMI_LOG_LVL_WARNING",
            "HMI_POLL_INTERVAL_MS",
        };

        Assert.Equal(expected, DefineMacroNames(header));
    }

    /// <summary>
    /// 两个可配值都用 <c>#ifndef</c> 包住 ⇒ 可以用 <c>-D</c> 临时覆盖。
    /// host 测试就是靠它跑出三种门槛的（见 <c>firmware/t113/tests/hmi_log_test.c</c>）；
    /// 去掉包裹会让那边一个都跑不起来 —— 但那是在 C 侧，这里先把契约钉住。
    /// </summary>
    [Fact]
    public void ConfigValues_AreWrappedInIfndef_SoDashDCanOverride()
    {
        var raw = RuntimeConfigCodeGen.Generate(new RuntimeConfig { ScanIntervalMs = 250 })
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("#ifndef HMI_POLL_INTERVAL_MS\n#define HMI_POLL_INTERVAL_MS 250\n#endif", raw);
        Assert.Contains("#ifndef HMI_LOG_LEVEL\n#define HMI_LOG_LEVEL 2\n#endif", raw);
    }

    /// <summary>空配置按默认值处理，不抛。</summary>
    [Fact]
    public void Generate_NullConfig_TreatsAsDefaults()
    {
        var header = RuntimeConfigCodeGen.Generate(null);
        var asDefault = RuntimeConfigCodeGen.Generate(new RuntimeConfig());

        Assert.Equal(asDefault, header);
    }

    // ══════════════════ D. 字段下发状态期望表 ══════════════════

    /// <summary>
    /// <see cref="RuntimeConfig"/> 的字段集与"是否进 <c>generated_runtime.h</c>"的期望表。
    ///
    /// <para><b>为什么必须显式写出来</b>：若写成"遍历属性、断言不下发"，把某个字段改名或
    /// 改成 <c>null</c> 只会让它从循环里消失，测试照样绿 —— 这是"受管清单"类守卫的经典失效形态。
    /// 这里做**双向完整**比对：多一个字段、少一个字段都红。</para>
    /// </summary>
    [Fact]
    public void RuntimeConfig_FieldSet_MatchesTheEmissionExpectationTable()
    {
        // 期望表独立写出（不反射生成）
        var emitted = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["ScanIntervalMs"] = true,        // → HMI_POLL_INTERVAL_MS
            ["LogLevel"] = true,              // → HMI_LOG_LEVEL
            ["BootScreenId"] = false,         // 闭环方式不同：进生成代码（lv_screen_load），不进本头
            ["HistoryStoragePath"] = false,
            ["WatchdogTimeoutMs"] = false,
            ["HeartbeatIntervalMs"] = false,
            ["EnableCrashDump"] = false,
            ["CrashDumpSector"] = false,
            ["MaxRuntimeTasks"] = false,
            ["EnableHmiReboot"] = false,
        };

        var actualFields = typeof(RuntimeConfig).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(emitted.Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray(), actualFields);

        // 本轮只接了 2 个。接第三个时这条会红 —— 那是**有意变更**，来这里显式改，
        // 顺手把 RuntimeConfig 上该字段的「未下发」标注一起改掉。
        Assert.Equal(2, emitted.Values.Count(v => v));
    }

    // ══════════════════ E. 导出产物 ══════════════════

    [Fact]
    public void Export_WritesGeneratedRuntimeHeaderWithConfiguredValue()
    {
        var project = new HmiProject { Name = "RuntimeExport" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "主画面", Width = 800, Height = 480 });
        project.RuntimeConfig.ScanIntervalMs = 300;
        project.RuntimeConfig.LogLevel = LogLevel.Error;

        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = TestTempDir.NewDir("runtime-header-export");
        try
        {
            var result = SimulatorProjectExporter.Export(project, code, dir);

            Assert.Contains("generated_runtime.h", result.Files);

            var header = File.ReadAllText(Path.Combine(dir, "generated_runtime.h"));
            Assert.Contains("#define HMI_POLL_INTERVAL_MS 300", header);
            Assert.Contains("#define HMI_LOG_LEVEL 3", header);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ══════════════════ F. host 侧 fixture ══════════════════

    /// <summary>
    /// host 测试用的 fixture 必须是**生成器的真实产物** —— 否则 <c>hmi_log_test.c</c> 测的是手抄的宏，
    /// 「与编辑器一致」这条就成摆设。生成器输出一变本用例就红
    /// ⇒ 用下面这个配置重算后写回 fixture（<c>tests/fixtures/generated_runtime.h</c>）。
    /// </summary>
    [Fact]
    public void HostTestFixture_MatchesGeneratorOutput()
    {
        var path = Path.Combine(FindRepoRoot(), "firmware/t113/tests/fixtures/generated_runtime.h");
        Assert.True(File.Exists(path), $"未找到 host 测试 fixture：{path}");

        var expected = RuntimeConfigCodeGen.Generate(
            new RuntimeConfig { ScanIntervalMs = 250, LogLevel = LogLevel.Warning });

        Assert.Equal(expected, File.ReadAllText(path));
    }

    // ══════════════════ G. 固件消费侧 ══════════════════

    /// <summary>
    /// 两个消费点都必须走 <c>__has_include</c> 并且**各自留回退** —— 这样没带生成头的
    /// 手写构建仍能编、行为与历史一致；带上就自动生效。少任何一半，这个闭环就是坏的：
    /// 要么老工程编不过，要么新配置到不了设备。
    /// </summary>
    [Fact]
    public void FirmwareConsumers_PullTheHeaderViaHasInclude_AndKeepFallbacks()
    {
        var runtimeDir = Path.Combine(FindRepoRoot(), "firmware/t113/app/runtime");

        var poll = File.ReadAllText(Path.Combine(runtimeDir, "runtime_config.h"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        var log = File.ReadAllText(Path.Combine(runtimeDir, "hmi_log.h"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        var probe = "__has_include(\"generated_runtime.h\")";
        var include = "include \"generated_runtime.h\"";

        Assert.Contains(probe, poll);
        Assert.Contains(include, poll);
        Assert.Contains(probe, log);
        Assert.Contains(include, log);

        // 轮询周期：缺头时回退固件历史默认值（采集循环无条件要用这个值，必须有数字）
        Assert.Contains("#ifndef HMI_POLL_INTERVAL_MS", poll);
        Assert.Contains("#  define HMI_POLL_INTERVAL_MS 100", poll);

        // 日志：缺头时分级宏退化为无条件输出，而不是编译不过
        Assert.Contains("#ifdef HMI_LOG_LEVEL", log);
        Assert.Contains("#  define HMI_LOG_AT", log);
    }

    /// <summary>
    /// 设备端**不得**手写 <c>HMI_LOG_LVL_*</c> / <c>HMI_LOG_LEVEL</c> 的数值 —— 级别常量只有
    /// 编辑器一处来源。唯一允许的对象是生成物本身（导出目录与 host 测试 fixture，
    /// 后者由 <see cref="HostTestFixture_MatchesGeneratorOutput"/> 逐字节钉住）。
    ///
    /// <para>扫描面排除 vendored SDK（<c>Tina-Linux/</c>、<c>Tina-SDK/</c>，含断链符号链接）。</para>
    /// </summary>
    [Fact]
    public void Firmware_DoesNotHandWriteLogLevelConstants()
    {
        const string GeneratedHeaderName = "generated_runtime.h";

        var firmwareRoot = Path.Combine(FindRepoRoot(), "firmware");
        Assert.True(Directory.Exists(firmwareRoot), $"未找到 firmware 目录：{firmwareRoot}");

        var offenders = new List<string>();
        var generatedHeaders = 0;

        foreach (var file in EnumerateFirmwareSources(firmwareRoot))
        {
            var rel = Path.GetRelativePath(firmwareRoot, file).Replace('\\', '/');
            if (rel.StartsWith("Tina-Linux/", StringComparison.Ordinal)) continue;   // vendored SDK
            if (rel.StartsWith("Tina-SDK/", StringComparison.Ordinal)) continue;     // vendored SDK（含断链）

            var isGenerated = Path.GetFileName(file) == GeneratedHeaderName;
            if (isGenerated) generatedHeaders++;

            var lineNo = 0;
            foreach (var line in ReadLinesSafely(file))
            {
                lineNo++;
                if (!LooksLikeLogLevelDefine(line)) continue;
                if (isGenerated) continue;   // 生成物：级别常量本来就该定义在这里
                offenders.Add($"{rel}:{lineNo}");
            }
        }

        var detail = string.Join(", ", offenders);
        Assert.True(offenders.Count == 0,
            $"设备端不得手写日志级别常量（唯一来源是生成的 {GeneratedHeaderName}）：{detail}");

        Assert.True(generatedHeaders > 0,
            $"没扫到任何 {GeneratedHeaderName} —— 守卫的扫描面可能已失效（例如 fixture 被移走）");
    }

    /// <summary>
    /// 守卫的**判据本身**要被钉住 —— 否则判据退化成"什么都匹配不到"时，上面那条守卫永远绿。
    /// 正例覆盖各种预处理指令缩进写法（含设备端回退块在用的 <c>#  define</c>）。
    /// </summary>
    [Theory]
    [InlineData("#define HMI_LOG_LVL_DEBUG 0")]
    [InlineData("#  define HMI_LOG_LVL_INFO 1")]
    [InlineData("# define HMI_LOG_LVL_WARNING 2")]
    [InlineData("#\tdefine HMI_LOG_LVL_ERROR 3")]
    [InlineData("    #  define   HMI_LOG_LEVEL 2")]
    public void LogLevelDefine_Detection_CatchesEveryIndentStyle(string line)
    {
        Assert.True(LooksLikeLogLevelDefine(line), $"这条手写定义没被守卫判据识别：{line}");
    }

    /// <summary>反例：不该被误判的（引用常量、注释、相邻前缀）—— 判据宽松不等于乱抓。</summary>
    [Theory]
    [InlineData("if (level >= HMI_LOG_LVL_WARNING) return;")]
    [InlineData("/* 设备端不得另写 HMI_LOG_LVL_ 数值 */")]
    [InlineData("#ifdef HMI_LOG_LEVEL")]
    [InlineData("#ifndef HMI_POLL_INTERVAL_MS")]
    [InlineData("#  define HMI_POLL_INTERVAL_MS 100")]
    [InlineData("#  define HMI_LOG_AT(x) 1")]
    public void LogLevelDefine_Detection_DoesNotOverreach(string line)
    {
        Assert.False(LooksLikeLogLevelDefine(line), $"判据不该命中这条：{line}");
    }

    // ══════════════════ 辅助 ══════════════════

    private static readonly Regex DefineRegex =
        new(@"^[ \t]*#[ \t]*define[ \t]+([A-Za-z_]\w*)", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>产物里出现的全部 <c>#define</c> 宏名（去重、字典序）。</summary>
    private static string[] DefineMacroNames(string header) =>
        DefineRegex.Matches(header).Select(m => m.Groups[1].Value)
            .Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();

    /// <summary>
    /// 是否在**手写**日志级别常量。判据按语法而不是字面：设备端回退块自己的风格是
    /// <c>#  define</c>，只认 <c>"#define "</c> 会漏。
    /// </summary>
    private static readonly Regex HandWrittenLogLevelDefine =
        new(@"^[ \t]*#[ \t]*define[ \t]+HMI_LOG_(?:LVL_[A-Z]|LEVEL\b)", RegexOptions.Compiled);

    private static bool LooksLikeLogLevelDefine(string line) => HandWrittenLogLevelDefine.IsMatch(line);

    private static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    /// <summary>逐行比对，返回基线里对应位置**不同**的那些行（先断言两边行数相同）。</summary>
    private static List<string> ChangedLines(string[] baseline, string[] other)
    {
        Assert.Equal(baseline.Length, other.Length);

        var changed = new List<string>();
        for (var i = 0; i < baseline.Length; i++)
            if (!string.Equals(baseline[i], other[i], StringComparison.Ordinal))
                changed.Add(other[i]);
        return changed;
    }

    /// <summary>递归枚举固件源文件；<b>跳过符号链接</b>（vendored SDK 里有断链，递归会抛）。</summary>
    private static IEnumerable<string> EnumerateFirmwareSources(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] entries;
            try { entries = Directory.GetFileSystemEntries(dir); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var entry in entries)
            {
                var info = new FileInfo(entry);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;   // 符号链接：不跟随
                if (Directory.Exists(entry)) { stack.Push(entry); continue; }
                if (entry.EndsWith(".c", StringComparison.Ordinal) || entry.EndsWith(".h", StringComparison.Ordinal))
                    yield return entry;
            }
        }
    }

    /// <summary>读文件；读不动（断链/权限）就当作"没有内容"，不让守卫因为环境问题红。</summary>
    private static IEnumerable<string> ReadLinesSafely(string path)
    {
        try { return File.ReadLines(path).ToList(); }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HmiEditor.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException($"向上未找到仓库根（含 HmiEditor.slnx）。起点：{AppContext.BaseDirectory}");
    }
}
