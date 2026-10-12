using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **全仓「TextBox → 接线」普查面**（阶段 189）：把 `#88` 那一族缺口从"`SettingsDialog` 已驱动"
/// 推广成**一张跨视图的账**。
///
/// <para><b>为什么是普查而不是逐条驱动</b>：全仓 `Views/` 下有 **153 个具名 <c>TextBox</c>**，其中
/// **48 条**真的接了 <c>TextChanged</c>（11 个文件）。逐条驱动需要每个宿主自己的夹具与"从模型读值"的面
/// —— 那是 <c>HostButtonEffectUiTests</c> 花了十几个阶段才长到 23 个宿主的量级。
/// 所以这里先做**能立刻做且不会过期的那一半**：把面**数清楚**、把"新增一个没人管的接线"变成红。</para>
///
/// <para><b>两个面（两边都双向差集）</b>：</para>
/// <list type="bullet">
/// <item><b>面 A</b>：本文件的具名 <c>TextBox</c> ∩ 本文件的 <c>TextChanged</c> 接线 —— 11 个文件 48 条。
/// 阶段 193 起 **11 个文件全部 <c>driven</c>**（阶段 188 的 <c>SettingsDialog</c>；阶段 190 的
/// <c>AlarmConfigView</c> / <c>AnimationEditorView</c> / <c>AuditLogView</c>；阶段 191 的
/// <c>SimulatorView</c>；阶段 192 的 <c>ScriptEditorView</c>；阶段 193 的
/// <c>CommunicationConfigView</c> / <c>Editors/VariablePicker</c> / <c>ProjectSettingsView</c> /
/// <c>RecipeEditorView</c> / <c>PropertyPanelView</c> —— 每个驱动面同时登记"没接线的具名框"
/// （`withoutWiring`）与"按名找不到的接线"（面 B 的 `<c>anonymousWired</c>`））、
/// **0 个 <c>blocked</c>**（阶段 191 有 1 个：<c>ScriptEditorView</c> 的 <c>Parent == null</c>
/// 前提不满足；阶段 192 修掉 `#89` 之后它转成 <c>driven</c> —— 这一档**机制留着**
/// （下一个"接线在但前提不满足"的视图直接用它），组合规则的牙齿由
/// <see cref="StatusAllowsDriver_SelfTest"/> ＋ 负控证明）、**0 个 <c>census</c>**。</item>
/// <item><b>面 B</b>：接了线但**按名找不到**的 —— 4 个文件 9 条，形态有两种：代码里 <c>new</c> 出来的框
/// （例：<c>var txtParamName = new TextBox { … }</c>），以及**辅助函数里的泛型参数**
/// （`AttachChangeHandler&lt;TextBox&gt;(ctl)` 里的 `txt.TextChanged += …` / `WireTextChanged(tb)`）。
/// 阶段 194 起**逐文件驱动**（各用**不同的入口**：可视化树按 `Grid` 坐标定位 / 从
/// `OwnedWindows` 钻进从属对话框 / 用"代表手势"代跑辅助函数那条线）⇒ 表里那 4 行的
/// `Driver` 列必须给出判据文件，而且那个文件里要**逐条点名**这些接线标识
/// （判据 2 会核对：重命名后不更新就红）。</item>
/// </list>
///
/// <para><b>升级配方</b>（把一个文件从"只普查"变成"已驱动"）：① 建该宿主的窗口（生产构造）；
/// ② 表里每条手势＝（框名 · 驱动文本 · **从模型读值的委托** · 期望值）；
/// ③ 驱动时写文本 ＋ **补发 <c>TextChangedEvent</c>**（程序化赋值不触发，阶段 187 实测；
/// 共享原语见 <see cref="ControlWiringFace"/>）；
/// ④ 把本节对应行的状态改成 <c>driven</c>、写上驱动判据文件名，并让判据覆盖该文件的**每个**具名框
/// （第 3 条判据会核对"driven 不许是空话"）。</para>
/// </summary>
public class AppTextBoxWiringCensusTests
{
    /// <summary>面 A 的一行：<paramref name="Status"/> 只会是 <c>driven</c> / <c>blocked</c> / <c>census</c>。</summary>
    /// <param name="Driver"><c>driven</c> ＝ 驱动判据文件名；<c>blocked</c> ＝ **前提判据**文件名
    /// （接线在、但在本仓 harness 里驱动不了，判据把那条前提量出来）；
    /// <c>census</c> 必须留空 —— "只普查"这件事得说得清。</param>
    private sealed record FileFace(string File, int Count, string Status, string? Driver = null);

    /// <summary>
    /// **面 A：具名 TextBox ∩ 有 TextChanged 接线**（阶段 189 实测，逐行可审）。
    /// ⚠ 数字**必须**与实测一致：涨了 = 有人加了接线却没管它；降了 = 有人删了接线/框但没同步本表。
    /// </summary>
    private static readonly FileFace[] WiredNamedTextBoxes =
    {
        // ── 已驱动（驱动面覆盖每条接线；"没接线的具名框"在驱动面的 withoutWiring 登记表里）──
        new("AlarmConfigView.axaml", 11, "driven", "AlarmConfigTextBoxWiringTests.cs"),         // 阶段 190；阶段 266 +3（ISA-18.2 职责三框）；阶段 292 +1（允许响应时间）
        new("AnimationEditorView.axaml", 9, "driven", "AnimationEditorTextBoxWiringTests.cs"), // 阶段 190
        new("AuditLogView.axaml", 1, "driven", "AuditLogTextBoxWiringTests.cs"),               // 阶段 190
        new("SettingsDialog.axaml", 7, "driven", "SettingsTextBoxWiringTests.cs"),             // 阶段 188
        new("SimulatorView.axaml", 7, "driven", "SimulatorTextBoxWiringTests.cs"),             // 阶段 191
        new("ScriptEditorView.axaml", 3, "driven", "ScriptEditorTextBoxWiringTests.cs"),       // 阶段 192（修 #89）

        // ── 阶段 193：面 A 收口（11 个文件全部 driven）──
        new("CommunicationConfigView.axaml", 4, "driven", "CommunicationConfigTextBoxWiringTests.cs"),
        new("Editors/VariablePicker.axaml", 1, "driven", "VariablePickerTextBoxWiringTests.cs"),
        new("ProjectSettingsView.axaml", 2, "driven", "ProjectSettingsTextBoxWiringTests.cs"),
        new("PropertyPanelView.axaml", 1, "driven", "PropertyPanelTextBoxWiringTests.cs"),
        new("RecipeEditorView.axaml", 6, "driven", "RecipeEditorTextBoxWiringTests.cs"),
    };

    /// <summary>
    /// **面 B：接了线但按名找不到的输入框**（代码里 <c>new</c> 出来的；现有驱动法够不着）。
    /// </summary>
    /// <summary>
    /// 面 B 的一行：<paramref name="Driver"/> 为驱动判据文件名（阶段 194 起逐文件驱动），
    /// `null` ＝ 如实登记的"还没驱动"。
    /// </summary>
    private sealed record AnonymousFace(string File, int Count, string? Driver = null);

    /// <summary>
    /// **面 B：接了线但按名找不到的输入框**（代码里 <c>new</c> 出来的；或辅助函数里的泛型参数）。
    /// 阶段 194 起逐文件驱动：给了 <paramref name="Driver"/> 的，那个判据文件里必须**逐条点名**
    /// 这些"按名找不到"的接线标识（判据 2 会核对 —— 重命名后不更新就红）。
    /// </summary>
    private static readonly AnonymousFace[] AnonymousWiredTextBoxes =
    {
        new("FaceplateTemplateEditorView.axaml", 6, "FaceplateTemplateEditorTextBoxWiringTests.cs"),
        new("ProjectSettingsView.axaml", 1, "ProjectSettingsTextBoxWiringTests.cs"),
        new("StyleEditorView.axaml", 1, "StyleEditorTextBoxWiringTests.cs"),
        new("UserManagerView.axaml", 1, "UserManagerTextBoxWiringTests.cs"),
    };

    /// <summary>
    /// 逐视图算两个面（返回 (面A, 面B, 全部接线) 的文件 → 条数，外加**面 B 的接线标识**）。
    /// </summary>
    private static (Dictionary<string, int> A, Dictionary<string, int> B, Dictionary<string, int> All,
                    Dictionary<string, string[]> BNames) Scan()
    {
        var root = Path.Combine(
            SourceScanner.RepoRoot, ControlWiringFace.ViewsRelative.Replace('/', Path.DirectorySeparatorChar));
        var faceA = new Dictionary<string, int>(StringComparer.Ordinal);
        var faceB = new Dictionary<string, int>(StringComparer.Ordinal);
        var all = new Dictionary<string, int>(StringComparer.Ordinal);
        var bNames = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var axamlPath in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            var codePath = axamlPath + ".cs";
            if (!File.Exists(codePath)) continue;

            var axaml = File.ReadAllText(axamlPath);
            var code = SourceScanner.StripComments(File.ReadAllText(codePath));

            var named = ControlWiringFace.NamedTextBoxes(axaml);
            var wired = ControlWiringFace.WiredNames(code);
            var rel = Path.GetRelativePath(root, axamlPath).Replace('\\', '/');

            var a = named.Intersect(wired, StringComparer.Ordinal).Count();
            var anonymous = wired.Except(named, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            if (a > 0) faceA[rel] = a;
            if (anonymous.Length > 0)
            {
                faceB[rel] = anonymous.Length;
                bNames[rel] = anonymous;
            }

            if (wired.Count > 0) all[rel] = wired.Count;
        }

        return (faceA, faceB, all, bNames);
    }

    /// <summary>
    /// **面 A 双向差集**：实测的"具名 TextBox ＋ 有接线"逐文件条数必须与表一致 ——
    /// 新增一条没人管的接线（无论新文件还是既有文件）都会红。
    /// </summary>
    [Fact]
    public void FaceA_MatchesTheCensus()
    {
        var (faceA, _, _, _) = Scan();

        // 面自检：扫描面塌了（目录改名/枚举失效）时下面全是空集差集 ⇒ 恒绿
        var total = faceA.Values.Sum();
        Assert.True(total >= 40, $"只扫到 {total} 条「具名 TextBox ＋ 有接线」—— 扫描面可能塌了（阶段 189 实测 48 条）");

        var expected = WiredNamedTextBoxes.ToDictionary(f => f.File, f => f.Count, StringComparer.Ordinal);

        var missing = faceA.Where(kv => !expected.ContainsKey(kv.Key))
            .Select(kv => $"{kv.Key}（实测 {kv.Value} 条）").OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var stale = expected.Where(kv => !faceA.ContainsKey(kv.Key))
            .Select(kv => $"{kv.Key}（表里 {kv.Value} 条）").OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var drifted = expected.Where(kv => faceA.TryGetValue(kv.Key, out var n) && n != kv.Value)
            .Select(kv => $"{kv.Key}: 表={kv.Value} 实测={faceA[kv.Key]}").OrderBy(s => s, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0,
            "这些视图有「具名 TextBox ＋ TextChanged 接线」却不在普查表里（新增的输入接线必须有人管）：\n  "
            + string.Join("\n  ", missing));
        Assert.True(stale.Length == 0,
            "普查表里有这些文件，但实测已经没有接线（接线/框被删了 ⇒ 同步本表）：\n  " + string.Join("\n  ", stale));
        Assert.True(drifted.Length == 0,
            "这些文件的条数对不上（多一条少一条都要来解释）：\n  " + string.Join("\n  ", drifted));
    }

    /// <summary>
    /// **面 B 双向差集 ＋ 逐文件认账**（阶段 194 起）：按名找不到的那一族同样要数清楚，
    /// 而且**给了驱动判据的行**，那个文件里必须逐条点名这些接线标识（`"txtParamName"` 之类）——
    /// 重命名之后不更新，这里就红（面 A 用"每个具名框"核对，面 B 没有具名框，改用**接线标识**核对）。
    /// </summary>
    [Fact]
    public void FaceB_MatchesTheCensus()
    {
        var (_, faceB, _, bNames) = Scan();

        var total = faceB.Values.Sum();
        Assert.True(total >= 5, $"只扫到 {total} 条「按名找不到的接线」—— 扫描面可能塌了（阶段 189 实测 9 条）");

        var expected = AnonymousWiredTextBoxes.ToDictionary(f => f.File, f => f.Count, StringComparer.Ordinal);

        var missing = faceB.Keys.Where(k => !expected.ContainsKey(k)).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var stale = expected.Keys.Where(k => !faceB.ContainsKey(k)).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var drifted = expected.Where(kv => faceB.TryGetValue(kv.Key, out var n) && n != kv.Value)
            .Select(kv => $"{kv.Key}: 表={kv.Value} 实测={faceB[kv.Key]}").ToArray();

        Assert.True(missing.Length == 0, $"这些文件新出现「按名找不到的接线」，请登记：{string.Join(", ", missing)}");
        Assert.True(stale.Length == 0, $"这些文件的「按名找不到的接线」没了，请同步本表：{string.Join(", ", stale)}");
        Assert.True(drifted.Length == 0, $"条数对不上：{string.Join("; ", drifted)}");

        // 给了驱动判据的行：文件必须存在，且**逐条点名**该文件里那些"按名找不到"的接线标识
        foreach (var face in AnonymousWiredTextBoxes.Where(f => f.Driver is not null))
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
    /// **状态列不许是空话**（阶段 190 立、阶段 191 加 <c>blocked</c>）：
    /// <list type="bullet">
    /// <item><c>driven</c>：必须给出**存在**的驱动判据文件，且该文件里出现它的**每一个具名
    /// <c>TextBox</c>**（否则"已驱动"只是表里的一行字）；</item>
    /// <item><c>blocked</c>（阶段 191 新增）：接线在、但**在本仓 harness 里驱动不了**（前提不满足）
    /// —— 必须给出**前提判据**文件，同样要覆盖每个具名框（本阶段只有 `ScriptEditorView`，
    /// 取证 ⇒ <c>#89</c>）；</item>
    /// <item><c>census</c>：**不许**写判据文件 —— "只普查"这件事得说得清。</item>
    /// </list>
    /// </summary>
    /// <summary>
    /// **三态与判据文件名的组合规则**（`driven`／`blocked` 必须有判据文件；`census` 必须没有；
    /// 别的状态一律不合法）。抽成**纯函数**是为了让"当前 0 个 <c>blocked</c> 行"这一档
    /// 仍然有覆盖 —— 见 <see cref="StatusAllowsDriver_SelfTest"/>。
    /// </summary>
    private static bool StatusAllowsDriver(string status, string? driver) => status switch
    {
        "driven" or "blocked" => !string.IsNullOrWhiteSpace(driver),
        "census" => driver is null,
        _ => false,
    };

    /// <summary>
    /// 组合规则**自测**（阶段 192 加）：`#89` 修好之后 <c>blocked</c> 这一档**一行都没有**了，
    /// 若不给自己配一条判据，"这一档还咬不咬人"就没人回答。七条断言把六种合法/非法组合
    /// ＋ 一个拼错的状态全钉住。
    /// </summary>
    [Fact]
    public void StatusAllowsDriver_SelfTest()
    {
        Assert.True(StatusAllowsDriver("driven", "X.cs"));
        Assert.True(StatusAllowsDriver("blocked", "X.cs"));
        Assert.True(StatusAllowsDriver("census", null));

        Assert.False(StatusAllowsDriver("driven", null));
        Assert.False(StatusAllowsDriver("blocked", null));
        Assert.False(StatusAllowsDriver("census", "X.cs"));
        Assert.False(StatusAllowsDriver("drivenX", "X.cs"));   // 状态拼错不许被静默当成 census
    }

    [Fact]
    public void EveryDrivenFile_HasADriverTestThatCoversItsTextBoxes()
    {
        Assert.All(WiredNamedTextBoxes, f => Assert.True(
            StatusAllowsDriver(f.Status, f.Driver),
            $"{f.File} 的状态/判据组合不合法：status={f.Status} driver={f.Driver ?? "(空)"}"
            + "（driven/blocked 必须有判据文件；census 必须没有）"));

        var driven = WiredNamedTextBoxes.Where(f => f.Status == "driven").ToArray();
        var blocked = WiredNamedTextBoxes.Where(f => f.Status == "blocked").ToArray();

        Assert.True(driven.Length >= 11,
            $"标了 driven 的文件只有 {driven.Length} 个 —— 状态列可能退化了（阶段 193 实测 11 个 ＝ 面 A 全量）");
        Assert.True(driven.Length + blocked.Length >= 11,
            $"有判据的文件只有 {driven.Length + blocked.Length} 个 —— 账塌了（阶段 193 实测 11 个）");

        foreach (var face in driven.Concat(blocked))
        {
            var kind = face.Status == "driven" ? "驱动判据" : "前提判据";

            var driverPath = Path.Combine(
                SourceScanner.RepoRoot, "tests", "HmiEditor.App.UiTests", face.Driver!);
            Assert.True(File.Exists(driverPath),
                $"{face.File} 的{kind} {face.Driver} 不存在 —— 「{face.Status}」是空话");

            var driver = File.ReadAllText(driverPath);
            var axaml = File.ReadAllText(Path.Combine(
                SourceScanner.RepoRoot,
                ControlWiringFace.ViewsRelative.Replace('/', Path.DirectorySeparatorChar),
                face.File));

            var names = ControlWiringFace.NamedTextBoxes(axaml);
            Assert.NotEmpty(names);
            foreach (var name in names)
                Assert.Contains($"\"{name}\"", driver, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// **每一条接线恰好进一个面**：<c>面A(文件) + 面B(文件) == 该文件的接线总数</c>。
    /// 一个文件**可以同时出现在两个面里**（<c>ProjectSettingsView</c> 就是：2 条具名 ＋ 1 条按名找不到）
    /// —— 所以"不重叠"是**接线级**的性质，不是文件级的（阶段 189 第一版写错就在这里）。
    /// </summary>
    [Fact]
    public void EveryWiring_IsClassifiedExactlyOnce()
    {
        var (faceA, faceB, all, _) = Scan();

        Assert.NotEmpty(faceA);
        Assert.NotEmpty(faceB);

        var bad = new List<string>();
        foreach (var (file, total) in all)
        {
            var a = faceA.TryGetValue(file, out var n1) ? n1 : 0;
            var b = faceB.TryGetValue(file, out var n2) ? n2 : 0;
            if (a + b != total) bad.Add($"{file}: 面A={a} 面B={b} 合计={a + b} ≠ 接线总数={total}");
        }

        Assert.True(bad.Count == 0, "这些文件的接线没有被两个面恰好分完：\n  " + string.Join("\n  ", bad));
    }
}
