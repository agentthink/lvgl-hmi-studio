using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **全仓「数字框 → 接线」普查面**（阶段 195）：把 TextBox 那一族（<see cref="AppTextBoxWiringCensusTests"/>）
/// 的做法搬到**数字框族**（<c>NumericUpDown</c> / <c>fa:FANumberBox</c> 的 <c>ValueChanged</c> 接线）。
///
/// <para><b>实测（阶段 195）</b>：`Views/` 下 **60 个具名数字框**，其中 **43 条**接了 `ValueChanged`
/// （4 个文件）。两个面：</para>
/// <list type="bullet">
/// <item><b>面 A</b>：本文件的具名数字框 ∩ 本文件的 `ValueChanged` 接线 —— 4 个文件 43 条。
/// 其中 3 个文件**逐条驱动**（`perBox`：表里每个具名框都要出现在驱动判据里）；
/// `PropertyPanelView`（31 条）按 **`enumerated`** 记 —— 它的 31 条已经由**枚举驱动**的
/// `PropertyPanelWiringTests`（从逻辑树枚举 70 个编辑器、逐个写值断言模型，阶段 118 起）
/// 覆盖，重复逐条记账是**重复劳动**；`enumerated` 行的核对是**指针级**的
/// （判据文件存在 ＋ 提到该视图 ＋ 含它自己的"未覆盖必须为空"自检字样）——
/// 强证据在那一族判据自己身上（阶段 118 的三条实测）。</item>
/// <item><b>面 B</b>：接了线但**按名找不到**的 —— 1 个文件 1 条
/// （`ProjectSettingsView` 里 `AttachChangeHandler&lt;T&gt;` 的泛型参数 `nud`）——
/// 由 `ProjectSettingsNumberBoxWiringTests` 用 `NumPwdMinLen` **代表驱动**
/// （见证：点「确定」之后工程真的被保存）。</item>
/// </list>
///
/// <para>⚠ **两个族的驱动纪律相反**：TextBox 程序化赋值**不触发** `TextChanged`（要补发事件），
/// 数字框程序化赋值**会触发** `ValueChanged`（写值即可）—— 见 <see cref="ControlWiringFace"/>。</para>
/// </summary>
public class AppNumberBoxWiringCensusTests
{
    /// <summary>面 A 的一行：<paramref name="Status"/> 是 <c>perBox</c> / <c>enumerated</c> / <c>census</c>。</summary>
    private sealed record FileFace(string File, int Count, string Status, string? Driver = null);

    /// <summary>面 B 的一行；<paramref name="Driver"/> 为驱动判据文件名（`null` ＝ 还没驱动）。</summary>
    private sealed record AnonymousFace(string File, int Count, string? Driver = null);

    /// <summary>**面 A：具名数字框 ∩ 有 ValueChanged 接线**（阶段 195 实测，逐行可审）。</summary>
    private static readonly FileFace[] WiredNamedNumberBoxes =
    {
        new("PropertyPanelView.axaml", 31, "enumerated", "PropertyPanelWiringTests.cs"),
        new("SettingsDialog.axaml", 9, "perBox", "SettingsDialogNumberBoxWiringTests.cs"),
        new("ScreenManagerView.axaml", 2, "perBox", "ScreenManagerNumberBoxWiringTests.cs"),
        new("ProjectSettingsView.axaml", 1, "perBox", "ProjectSettingsNumberBoxWiringTests.cs"),
    };

    /// <summary>
    /// **面 B：接了线但按名找不到**（辅助函数里的泛型参数 `nud` ＋ 阶段 195 修 `#90` 补的那条
    /// `fanumber` —— 两条都在 `AttachChangeHandler` 里）。
    /// </summary>
    private static readonly AnonymousFace[] AnonymousWiredNumberBoxes =
    {
        new("ProjectSettingsView.axaml", 2, "ProjectSettingsNumberBoxWiringTests.cs"),
    };

    /// <summary>逐视图算两个面（外加**面 B 的接线标识**）。</summary>
    private static (Dictionary<string, int> A, Dictionary<string, int> B,
                    Dictionary<string, string[]> BNames) Scan()
    {
        var root = Path.Combine(
            SourceScanner.RepoRoot, ControlWiringFace.ViewsRelative.Replace('/', Path.DirectorySeparatorChar));
        var faceA = new Dictionary<string, int>(StringComparer.Ordinal);
        var faceB = new Dictionary<string, int>(StringComparer.Ordinal);
        var bNames = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var axamlPath in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            var codePath = axamlPath + ".cs";
            if (!File.Exists(codePath)) continue;

            var named = ControlWiringFace.NamedNumberBoxes(File.ReadAllText(axamlPath));
            var wired = ControlWiringFace.WiredByValueChanged(
                SourceScanner.StripComments(File.ReadAllText(codePath)));
            var rel = Path.GetRelativePath(root, axamlPath).Replace('\\', '/');

            var a = named.Intersect(wired, StringComparer.Ordinal).Count();
            var anonymous = wired.Except(named, StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal).ToArray();
            if (a > 0) faceA[rel] = a;
            if (anonymous.Length > 0)
            {
                faceB[rel] = anonymous.Length;
                bNames[rel] = anonymous;
            }
        }

        return (faceA, faceB, bNames);
    }

    /// <summary>**面 A 双向差集**：逐文件条数必须与表一致（自检：总数 ≥ 40）。</summary>
    [Fact]
    public void FaceA_MatchesTheCensus()
    {
        var (faceA, _, _) = Scan();

        var total = faceA.Values.Sum();
        Assert.True(total >= 40,
            $"只扫到 {total} 条「具名数字框 ＋ ValueChanged 接线」—— 扫描面可能塌了（阶段 195 实测 43 条）");

        var expected = WiredNamedNumberBoxes.ToDictionary(f => f.File, f => f.Count, StringComparer.Ordinal);

        var missing = faceA.Where(kv => !expected.ContainsKey(kv.Key))
            .Select(kv => $"{kv.Key}（实测 {kv.Value} 条）").OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var stale = expected.Where(kv => !faceA.ContainsKey(kv.Key))
            .Select(kv => $"{kv.Key}（表里 {kv.Value} 条）").OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var drifted = expected.Where(kv => faceA.TryGetValue(kv.Key, out var n) && n != kv.Value)
            .Select(kv => $"{kv.Key}: 表={kv.Value} 实测={faceA[kv.Key]}").OrderBy(s => s, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0,
            "这些视图有「具名数字框 ＋ ValueChanged 接线」却不在普查表里（新增的接线必须有人管）：\n  "
            + string.Join("\n  ", missing));
        Assert.True(stale.Length == 0,
            "普查表里有这些文件，但实测已经没有接线（接线/框被删了 ⇒ 同步本表）：\n  " + string.Join("\n  ", stale));
        Assert.True(drifted.Length == 0,
            "这些文件的条数对不上（多一条少一条都要来解释）：\n  " + string.Join("\n  ", drifted));
    }

    /// <summary>**面 B 双向差集 ＋ 逐文件认账**：条数 + 判据文件里逐条点名接线标识。</summary>
    [Fact]
    public void FaceB_MatchesTheCensus()
    {
        var (_, faceB, bNames) = Scan();

        var total = faceB.Values.Sum();
        Assert.True(total >= 2, $"只扫到 {total} 条「按名找不到的接线」—— 扫描面可能塌了（阶段 195 实测 2 条）");

        var expected = AnonymousWiredNumberBoxes.ToDictionary(f => f.File, f => f.Count, StringComparer.Ordinal);

        var missing = faceB.Keys.Where(k => !expected.ContainsKey(k)).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var stale = expected.Keys.Where(k => !faceB.ContainsKey(k)).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var drifted = expected.Where(kv => faceB.TryGetValue(kv.Key, out var n) && n != kv.Value)
            .Select(kv => $"{kv.Key}: 表={kv.Value} 实测={faceB[kv.Key]}").ToArray();

        Assert.True(missing.Length == 0, $"这些文件新出现「按名找不到的接线」，请登记：{string.Join(", ", missing)}");
        Assert.True(stale.Length == 0, $"这些文件的「按名找不到的接线」没了，请同步本表：{string.Join(", ", stale)}");
        Assert.True(drifted.Length == 0, $"条数对不上：{string.Join("; ", drifted)}");

        foreach (var face in AnonymousWiredNumberBoxes.Where(f => f.Driver is not null))
        {
            var driverPath = Path.Combine(
                SourceScanner.RepoRoot, "tests", "HmiEditor.App.UiTests", face.Driver!);
            Assert.True(File.Exists(driverPath),
                $"{face.File} 标了驱动判据 {face.Driver}，但文件不存在 —— 「已驱动」是空话");

            var driver = File.ReadAllText(driverPath);
            Assert.True(bNames.TryGetValue(face.File, out var identifiers) && identifiers.Length > 0,
                $"{face.File} 表里说它是面 B 且已驱动，但扫描没扫到它的接线标识 —— 表与代码对不上");
            foreach (var identifier in identifiers!)
                Assert.Contains($"\"{identifier}\"", driver, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// **`perBox` 行不许是空话**：判据文件必须存在，且该文件里出现它的**每一个具名数字框**
    /// （面 A 用"每个具名框"核对；`enumerated` 行不看这条 —— 走下面那条）。
    /// </summary>
    [Fact]
    public void EveryPerBoxFile_HasADriverThatCoversItsBoxes()
    {
        var perBox = WiredNamedNumberBoxes.Where(f => f.Status == "perBox").ToArray();
        Assert.True(perBox.Length >= 3,
            $"标了 perBox 的文件只有 {perBox.Length} 个 —— 状态列可能退化了（阶段 195 实测 3 个）");

        foreach (var face in perBox)
        {
            Assert.False(string.IsNullOrWhiteSpace(face.Driver), $"{face.File} 标了 perBox 却没写驱动判据文件名");

            var driverPath = Path.Combine(
                SourceScanner.RepoRoot, "tests", "HmiEditor.App.UiTests", face.Driver!);
            Assert.True(File.Exists(driverPath), $"{face.File} 的驱动判据 {face.Driver} 不存在 —— 「已驱动」是空话");

            var driver = File.ReadAllText(driverPath);
            var axaml = File.ReadAllText(Path.Combine(
                SourceScanner.RepoRoot,
                ControlWiringFace.ViewsRelative.Replace('/', Path.DirectorySeparatorChar),
                face.File));

            var names = ControlWiringFace.NamedNumberBoxes(axaml);
            Assert.NotEmpty(names);
            foreach (var name in names)
                Assert.Contains($"\"{name}\"", driver, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// **`enumerated` 行是"指针级"核对**（阶段 195 的口径）：判据文件存在 ＋ 提到该视图 ＋
    /// 含它自己的"未覆盖必须为空"自检字样（`uncovered`）。⚠ 它**不**逐条点名控件 ——
    /// 那一族判据是**枚举驱动**的（从逻辑树枚举、逐个写值断言模型），按名点不到。
    /// 强证据在那一族判据自己身上，本行只保证"指针没断"。
    /// </summary>
    [Fact]
    public void EveryEnumeratedFile_PointsAtAnEnumerationDriver()
    {
        var enumerated = WiredNamedNumberBoxes.Where(f => f.Status == "enumerated").ToArray();
        Assert.True(enumerated.Length >= 1,
            $"标了 enumerated 的文件只有 {enumerated.Length} 个 —— 状态列可能退化了（阶段 195 实测 1 个）");

        foreach (var face in enumerated)
        {
            Assert.False(string.IsNullOrWhiteSpace(face.Driver), $"{face.File} 标了 enumerated 却没写判据文件名");

            var driverPath = Path.Combine(
                SourceScanner.RepoRoot, "tests", "HmiEditor.App.UiTests", face.Driver!);
            Assert.True(File.Exists(driverPath), $"{face.File} 指向的枚举驱动判据 {face.Driver} 不存在");

            var driver = File.ReadAllText(driverPath);
            var viewName = Path.GetFileNameWithoutExtension(face.File);
            Assert.Contains(viewName, driver, StringComparison.Ordinal);
            Assert.Contains("uncovered", driver, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// **状态列合法 ＋ 每条接线恰好进一个面**：`面A(文件) + 面B(文件) == 该文件的接线总数`。
    /// </summary>
    [Fact]
    public void EveryWiring_IsClassifiedExactlyOnce()
    {
        Assert.All(WiredNamedNumberBoxes,
            f => Assert.Contains(f.Status, new[] { "perBox", "enumerated", "census" }));
        Assert.All(WiredNamedNumberBoxes.Where(f => f.Status == "census"),
            f => Assert.True(f.Driver is null, $"{f.File} 是 census 却写了判据文件"));

        var (faceA, faceB, _) = Scan();
        Assert.NotEmpty(faceA);

        var root = Path.Combine(
            SourceScanner.RepoRoot, ControlWiringFace.ViewsRelative.Replace('/', Path.DirectorySeparatorChar));
        var bad = new List<string>();
        foreach (var axamlPath in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            var codePath = axamlPath + ".cs";
            if (!File.Exists(codePath)) continue;

            var named = ControlWiringFace.NamedNumberBoxes(File.ReadAllText(axamlPath));
            var wired = ControlWiringFace.WiredByValueChanged(
                SourceScanner.StripComments(File.ReadAllText(codePath)));
            if (wired.Count == 0) continue;

            var rel = Path.GetRelativePath(root, axamlPath).Replace('\\', '/');
            var a = faceA.TryGetValue(rel, out var n1) ? n1 : 0;
            var b = faceB.TryGetValue(rel, out var n2) ? n2 : 0;
            if (a + b != wired.Count) bad.Add($"{rel}: 面A={a} 面B={b} 合计={a + b} ≠ 接线总数={wired.Count}");
        }

        Assert.True(bad.Count == 0, "这些文件的接线没有被两个面恰好分完：\n  " + string.Join("\n  ", bad));
    }
}
