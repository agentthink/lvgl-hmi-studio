using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 守卫：**产品代码**（<c>src/</c>）必须通过 <see cref="HmiEditor.Core.Services.AppPaths"/>
/// 解析应用数据根与临时根，不得各自去碰系统临时目录 / <c>SpecialFolder</c>。
///
/// <para><b>为什么需要：</b>原有守卫（<c>TestTempDirUsageGuardTests</c>）的扫描面是
/// <c>FindTestsRoot()</c> —— **只扫 <c>tests/</c>，不扫 <c>src/</c>**。于是产品侧
/// <c>Path.Combine(Path.GetTempPath(), "zhishu_hmi_comm_config.ini")</c> 这类写法长期无守卫，
/// 实测后果是：<c>/tmp</c>（10 MB tmpfs）被占满时
/// <c>File.WriteAllTextAsync</c> 抛 <c>IOException: No space left on device</c>，
/// 让 <c>DeployAsync_RuntimeConfigText_UploadsCommConfig</c> 间歇红；
/// 而另一个测试的断言恰好依赖那个文件 —— 测试侧一处违规都没有。</para>
///
/// <para><b>四条不变量（例外表为空）：</b></para>
/// <list type="number">
///   <item><c>src/</c> 全域**零**出现系统临时目录 API（唯一例外是注释里对它的说明）。</item>
///   <item><c>SpecialFolder.ApplicationData</c> / <c>LocalApplicationData</c> 只允许出现在
///   <c>AppPaths.cs</c> —— 历史数据位置同样收在这里（<c>LegacyDataRoot</c>）。</item>
///   <item>历史目录名字面量 <c>"HmiEditor"</c> 不得在 <c>AppPaths.cs</c> 之外被重新拼出来
///   —— 否则"两个目录名"会随调用方重新长出来。</item>
///   <item>临时根（<c>TempRoot</c>）只允许在 <c>AppPaths.cs</c> 出现 ——
///   外部只能拿 API 给出的**完整路径**，不得读根自己拼；否则唯一名产物会绕过会话层，
///   重新变成"无主目录"。</item>
/// </list>
///
/// <para>这与「测试怎么写」类守卫互补：本条守的是**产品代码是什么**，所以它是静态扫描而不是
/// 行为断言 —— 行为断言无法证伪"某处新建了一个 <c>Path.GetTempPath()</c>"。</para>
/// </summary>
public class AppPathsSingleSourceGuardTests
{
    /// <summary>系统临时目录 API：出现即违规（注释除外）。</summary>
    private static readonly string[] ForbiddenTempApis =
    {
        "Path.GetTempPath()",
        "Directory.CreateTempSubdirectory(",
        "Path.GetTempFileName()",
    };

    /// <summary>应用数据根 API：只允许在唯一来源里出现（外加下列历史白名单）。</summary>
    private static readonly string[] ForbiddenDataRootApis =
    {
        "SpecialFolder.ApplicationData",
        "SpecialFolder.LocalApplicationData",
    };

    /// <summary>
    /// 历史目录名字面量：带引号的形式不得出现在唯一来源之外。
    /// 不带引号的 `HmiEditor` 是**命名空间前缀**（`HmiEditor.Core` 等），不该被误报 ——
    /// 判据取「引号包裹的字符串字面量」，与 `check-path-hygiene.sh` 的思路一致。
    /// </summary>
    private const string LegacyFolderLiteral = "\"HmiEditor\"";

    /// <summary>
    /// 例外表：**必须为空**。
    ///
    /// <para>曾经有两条例外 —— <c>LlmConfigStore</c> 与 <c>MainWindow</c> 各自拼
    /// <c>%APPDATA%/HmiEditor</c>。第十二轮把历史位置收进 <c>AppPaths.LegacyDataRoot</c>
    /// （读侧兼容、写侧唯一）之后，例外就消失了。</para>
    ///
    /// <para>留这张空表并**断言它为空**，是因为「没有例外」本身就是被守的不变量：
    /// 没有这条断言，下次往白名单加一条不会有任何阻力 —— 而"多处各自解析"
    /// 正是从加第一条例外开始的。</para>
    /// </summary>
    private static readonly Dictionary<string, string> DataRootAllowed = new();

    [Fact]
    public void ProductCode_DoesNotUseSystemTempDirectoryAtAll()
    {
        var (srcRoot, files) = ScanSourceTree();
        var offenders = new List<string>();

        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(srcRoot, file).Replace('\\', '/');
            var lineNo = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNo++;
                if (IsComment(line)) continue;      // 注释里说明这些 API 是允许的
                foreach (var api in ForbiddenTempApis)
                {
                    if (line.Contains(api, StringComparison.Ordinal))
                        offenders.Add($"{rel}:{lineNo}  {api}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            $"产品代码不得直接使用系统临时目录（本机 /tmp 仅 10 MB，写满即 IOException）。{Environment.NewLine}"
            + $"请改用 AppPaths.TempSubdir / TempPath / NewTempFilePath / NewTempSubdir / AssetDir。{Environment.NewLine}"
            + $"违规点（{offenders.Count}）：{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", offenders));
    }

    [Fact]
    public void AppDataRoot_IsResolvedOnlyByAppPaths()
    {
        var (srcRoot, files) = ScanSourceTree();
        var offenders = new List<string>();

        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(srcRoot, file).Replace('\\', '/');
            if (rel == AppPathsSource) continue;                 // 唯一来源自身
            if (DataRootAllowed.ContainsKey(rel)) continue;      // 刻意的例外（当前为空）

            var lineNo = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNo++;
                if (IsComment(line)) continue;
                foreach (var api in ForbiddenDataRootApis)
                {
                    if (line.Contains(api, StringComparison.Ordinal))
                        offenders.Add($"{rel}:{lineNo}  {api}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            $"应用数据根只能由 AppPaths 解析（否则又会出现多处各自 Path.Combine 的分叉）。{Environment.NewLine}"
            + $"历史位置也已收进 AppPaths（LegacyDataRoot / LegacyDataFile）—— 升级兼容请用它，"
            + $"不要自己拼 %APPDATA%/HmiEditor。{Environment.NewLine}"
            + $"请改用 AppPaths.DataRoot / DataFile / LegacyDataRoot / AssetDir / TempRoot。{Environment.NewLine}"
            + $"违规点（{offenders.Count}）：{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", offenders));
    }

    /// <summary>
    /// 历史目录名不得在唯一来源之外被重新拼出来。
    ///
    /// <para>这条守的是"两个数据根"这个结构本身：`SpecialFolder` 那条只能挡住
    /// "自己去解析 %APPDATA%"，挡不住 <c>Path.Combine(某处已解析的根, "HmiEditor")</c>
    /// —— 而后者同样能长回第二个数据根。判据取「带引号的名字面量」，
    /// 不带引号的 <c>HmiEditor.Core</c> 之类命名空间前缀不会被误报。</para>
    /// </summary>
    [Fact]
    public void LegacyDataFolderName_IsNotRecomposedOutsideItsSingleSource()
    {
        var (srcRoot, files) = ScanSourceTree();
        var offenders = new List<string>();

        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(srcRoot, file).Replace('\\', '/');
            if (rel == AppPathsSource) continue;

            var lineNo = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNo++;
                if (IsComment(line)) continue;
                if (line.Contains(LegacyFolderLiteral, StringComparison.Ordinal))
                    offenders.Add($"{rel}:{lineNo}  {line.Trim()}");
            }
        }

        Assert.True(offenders.Count == 0,
            $"历史数据目录名只能在 AppPaths 里出现一次（{Environment.NewLine}"
            + $"否则「两个数据根」会随调用方重新长出来）。{Environment.NewLine}"
            + $"升级兼容请用 AppPaths.LegacyDataRoot / LegacyDataFile。{Environment.NewLine}"
            + $"违规点（{offenders.Count}）：{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", offenders));
    }

    /// <summary>唯一来源自身的相对路径（豁免所有"只在 AppPaths 里出现"类不变量）。</summary>
    private const string AppPathsSource = "HmiEditor.Core/Services/AppPaths.cs";

    /// <summary>
    /// 临时根标识符：外部不得出现。注意 <c>SessionTempRoot</c> 含同一 token —— 同样在禁列
    /// （外部拿 <c>NewTempSubdir</c> 的返回值就够了，读根自己拼就会绕过会话层）。
    /// </summary>
    private const string TempRootToken = "TempRoot";

    /// <summary>
    /// 唯一名临时产物只能经 <c>AppPaths</c> 生成。
    ///
    /// <para><b>为什么这条必须有静态守卫：</b>"产物落在哪个会话目录下"是**接线性质**
    /// （谁把名字拼到哪儿），行为测试只能证明 AppPaths 自己的实现是对的，
    /// 证不了"没有第二个人在别处又拼了一份"。而会话层的整体释放依赖的正是
    /// 「所有唯一名产物都在同一个会话目录下」这个**全称**性质 —— 一处漏网，
    /// 那个目录就永久残留。</para>
    /// </summary>
    [Fact]
    public void TempRoot_IsNotComposedOutsideItsSingleSource()
    {
        var (srcRoot, files) = ScanSourceTree();
        var offenders = new List<string>();

        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(srcRoot, file).Replace('\\', '/');
            if (rel == AppPathsSource) continue;

            var lineNo = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNo++;
                if (IsComment(line)) continue;
                if (line.Contains(TempRootToken, StringComparison.Ordinal))
                    offenders.Add($"{rel}:{lineNo}  {line.Trim()}");
            }
        }

        Assert.True(offenders.Count == 0,
            $"临时根只能由 AppPaths 解析 —— 外部读根自己拼路径会绕过会话层，"
            + $"让唯一名产物重新变成「无主目录」（永不释放）。{Environment.NewLine}"
            + $"请改用 AppPaths.NewTempSubdir / NewTempFilePath / TempSubdir / TempPath / AssetDir。{Environment.NewLine}"
            + $"违规点（{offenders.Count}）：{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", offenders));
    }

    /// <summary>
    /// 临时产物的三类清理（会话释放 / 孤儿会话清理 / 历史形态清理）必须都有产品侧调用点。
    ///
    /// <para>这条守的是「实现了没人调」= 死代码：泄漏不会因为"写了个清理函数"而消失，
    /// 只会因为"它真的被调了"而消失。行为测试覆盖不到应用启动/退出路径
    /// （那是 Avalonia 生命周期），所以这里用静态接线断言补上。</para>
    /// </summary>
    [Fact]
    public void SessionCleanup_IsWiredIntoTheApplication()
    {
        var (srcRoot, files) = ScanSourceTree();
        string[] required = ["ReleaseSessionTemp(", "PruneStaleSessions(", "PruneLegacyFlatWorkDirs("];
        var callers = required.ToDictionary(r => r, _ => new List<string>());

        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(srcRoot, file).Replace('\\', '/');
            if (rel == AppPathsSource) continue;      // 定义处不算调用点

            var lineNo = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNo++;
                if (IsComment(line)) continue;        // 注释掉的调用不算
                foreach (var api in required)
                {
                    if (line.Contains(api, StringComparison.Ordinal))
                        callers[api].Add($"{rel}:{lineNo}");
                }
            }
        }

        foreach (var (api, where) in callers)
        {
            Assert.True(where.Count > 0,
                $"{api}…) 没有产品侧调用点 —— 会话临时目录要么永不释放、要么孤儿永不清。"
                + "（正常退出应在 MainWindow.CleanupOnShutdown 释放，启动应在 App 初始化时清孤儿。）");
        }
    }

    /// <summary>
    /// 例外表必须为空；万一将来又加了例外，那些条目也得指向**真实存在**的文件。
    ///
    /// <para>后半句守的是「守卫静默退化」：白名单文件被改名/删除后，守卫继续全绿、
    /// 实际已经放行了别处。前半句守的是「例外本身要刻意」—— 加一条就得改这里，
    /// 改的时候必须想清楚为什么要开例外。</para>
    /// </summary>
    [Fact]
    public void AppDataRootAllowList_IsEmpty_AndAnyEntryPointsToARealFile()
    {
        Assert.True(DataRootAllowed.Count == 0,
            "应用数据根不得再有例外 —— 历史位置请收进 AppPaths.LegacyDataRoot / LegacyDataFile"
            + "（读侧兼容、写侧唯一），而不是在各调用方自己拼路径。");

        var (srcRoot, _) = ScanSourceTree();
        foreach (var (rel, reason) in DataRootAllowed)
        {
            var full = Path.Combine(srcRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(full), $"白名单指向的文件不存在：{rel}（豁免理由：{reason}）");
        }
    }

    /// <summary>
    /// 扫描面自检：静态扫描最容易「扫了个空所以全绿」。
    /// 这里同时断言**数量下限**与**关键文件必须在面内**。
    /// </summary>
    [Fact]
    public void Scanner_ActuallyCoversTheSourceTree()
    {
        var (srcRoot, files) = ScanSourceTree();
        var rels = files.Select(f => Path.GetRelativePath(srcRoot, f).Replace('\\', '/')).ToHashSet();

        Assert.True(files.Count >= 100, $"扫描面过小（{files.Count} 个 .cs），疑似根目录解析错误：{srcRoot}");

        foreach (var must in new[]
                 {
                     "HmiEditor.Core/Services/AppPaths.cs",
                     "HmiEditor.Device/Updates/HmiDeployService.cs",
                     "HmiEditor.Device/Build/T113SoBuilder.cs",
                     "HmiEditor.App/MainWindow.axaml.cs",
                     "HmiEditor.App/Services/BuildPipelineService.cs",
                     "HmiEditor.App/Views/AiIconDialog.axaml.cs",
                     "HmiEditor.App/Views/DownloadToHmiView.axaml.cs",
                 })
        {
            Assert.True(rels.Contains(must), $"关键文件不在扫描面内：{must}");
        }
    }

    /// <summary>纯注释行（<c>//</c>、<c>///</c>、块注释续行 <c>*</c>）不产生实际写入。</summary>
    private static bool IsComment(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("*", StringComparison.Ordinal)
            || trimmed.StartsWith("/*", StringComparison.Ordinal);
    }

    /// <summary>返回 <c>src/</c> 根与其中全部 <c>.cs</c> 文件（跳过 <c>bin/</c>、<c>obj/</c>）。</summary>
    private static (string Root, List<string> Files) ScanSourceTree()
    {
        var srcRoot = Path.Combine(FindRepoRoot(), "src");
        Assert.True(Directory.Exists(srcRoot), $"未找到 src 目录：{srcRoot}");

        var files = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/", StringComparison.Ordinal)
                        && !f.Contains("/bin/", StringComparison.Ordinal))
            .ToList();

        return (srcRoot, files);
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

        throw new InvalidOperationException(
            $"向上未找到仓库根（含 HmiEditor.slnx）。起点：{AppContext.BaseDirectory}");
    }
}
