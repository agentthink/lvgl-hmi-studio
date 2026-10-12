using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **输入框「谁读」面**（阶段 196）：TextBox / 数字框那两个族只算**接了线**的框（写侧），
/// 而 `Views/` 下还有一大批具名输入框**没有接线** —— 它们由"点保存/应用"那条路**一次性读取**。
/// 「没接线」本身不是缺口，但**前提是有人读它**：一个只被回填、从来没人读的输入框，
/// 用户打字之后什么都不会发生（这正是 `#90` 那一类缺陷的形状）。
///
/// <para><b>这一面要回答的问题</b>：每个**没接线**的具名输入框，必须是下面三者之一 ——</para>
/// <list type="number">
/// <item><b>有读点</b>：代码里有 <c>X.member</c> 的**非赋值**出现（`X.Text` / `X.Value` … 后面不是 `=`），
/// 或者有**裸名**出现（`X` 作为实参 / 转发属性 <c>=&gt; X</c> —— 说明有人拿到了这个控件）；</item>
/// <item><b>只读展示</b>：在 <see cref="DisplayOnly"/> 登记表里写明理由
/// （`IsReadOnly` 的回填框：存在的意义就是"给别人看"，本来就不该被读）；</item>
/// <item>否则**当场红**（并点名 —— 新加一个没人读的输入框会被挡住）。</item>
/// </list>
///
/// <para><b>反过来也判</b>：登记表里的每一条都必须真的存在（名字写歪 ⇒ 红）、
/// **真的没有读点**（登记表不能用来藏一个其实有人读的框）、且**没接线**
/// （接了线的框归那两个接线面管）。</para>
///
/// <para><b>实测（2026-10-01，阶段 199 修正后的分账）</b>：`Views/` 下 **213 个具名输入框**
/// （按「文件＋名字」计）分成四桶，**加起来正好 213**：</para>
/// <list type="bullet">
/// <item><b>117 个已接线</b> —— code-behind <c>X.TextChanged += …</c> **91 个**（归那两个接线面）＋
/// **XAML 属性式 <c>TextChanged="…"</c> 26 个**（阶段 199 才量出来的第三形态 ⇒
/// <see cref="AppXamlAttributeWiringFaceTests"/>）。⚠ 阶段 196 那一版把这 26 个也算进"没接线"，
/// 读数 91 / 122 / 118 / 4 里有三个数偏了 —— 见 `docs/2026-10-01-xaml-attribute-wiring-face.md` 的勘误。</item>
/// <item><b>92 个没接线、有读点</b>（由"点保存/应用"或动作处理器一次性读取）—— 读点就是"不是缺口"的证据；</item>
/// <item><b>4 个只读展示</b>（登记表）；</item>
/// <item><b>0 个「没人读」</b>（这一格要被守住）。</item>
/// </list>
/// <para>其中 2 个是**经转发属性读**的：`AiScriptDialog` / `AiTemplateDialog` 的 `InputBox`
/// （`protected override TextBox AiInput => InputBox;` —— 基类拿它读写）。</para>
/// </summary>
public class AppUnwiredInputReadFaceTests
{
    /// <summary>只读展示框的登记表（键＝文件＋控件名，值＝理由）。</summary>
    private static readonly Dictionary<(string File, string Name), string> DisplayOnly = new()
    {
        [("DeviceManagerView.axaml", "TxtDeviceVersion")] =
            "只读展示（`IsReadOnly=\"True\"`）：由设备信息回填（`info.HmiVersion` / `result.DeviceVersion`），没人读它",
        [("DownloadToHmiView.axaml", "TxtSoPath")] =
            "只读展示（`IsReadOnly=\"True\"`）：由「选择 .so」回填；真正下发用的是那个路径变量，不是这个框",
        [("Editors/VariablePicker.axaml", "DisplayField")] =
            "只读展示（`IsReadOnly=\"True\"`）：`UpdateDisplay()` 写选中变量的显示名，没人读它",
        [("UserManagerView.axaml", "RoleNameBox")] =
            "只读展示（#50：角色名与描述都是只读展示）：由 `ShowRoleDetail` 回填，没人读它",
    };

    private sealed record Box(string File, string Name);

    /// <summary>
    /// 一扫描的读数：具名框 · 已接线（两种形态）· **其中属性式的** · 有读点 · 只读展示 · **没人读**。
    /// </summary>
    /// <param name="WiredByCodeBehind">code-behind <c>X.TextChanged += …</c>（阶段 189~198 那几张账扫的形态）。</param>
    /// <param name="WiredByXaml">XAML 属性式 <c>TextChanged="…"</c>（**阶段 199 才量出来**：26 条，见
    /// <see cref="AppXamlAttributeWiringFaceTests"/>）。</param>
    private sealed record ScanResult(
        List<Box> Declared,
        HashSet<string> Wired,
        HashSet<string> WiredByCodeBehind,
        HashSet<string> WiredByXaml,
        HashSet<string> Read,
        List<Box> Unread);

    private static string Key(Box box) => box.File + "::" + box.Name;

    private static ScanResult Scan()
    {
        var root = Path.Combine(
            SourceScanner.RepoRoot, ControlWiringFace.ViewsRelative.Replace('/', Path.DirectorySeparatorChar));

        var declared = new List<Box>();
        var wired = new HashSet<string>(StringComparer.Ordinal);
        var wiredByCodeBehind = new HashSet<string>(StringComparer.Ordinal);
        var wiredByXaml = new HashSet<string>(StringComparer.Ordinal);
        var read = new HashSet<string>(StringComparer.Ordinal);
        var unread = new List<Box>();

        foreach (var axamlPath in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, axamlPath).Replace('\\', '/');
            var axaml = File.ReadAllText(axamlPath);

            var names = ControlWiringFace.NamedTextBoxes(axaml)
                .Concat(ControlWiringFace.NamedNumberBoxes(axaml))
                .OrderBy(n => n, StringComparer.Ordinal)
                .Select(n => new Box(rel, n))
                .ToArray();
            if (names.Length == 0) continue;
            declared.AddRange(names);

            // 同一个视图类的**全部** partial（`Foo.axaml` → `Foo*.cs`）
            var dir = Path.GetDirectoryName(axamlPath)!;
            var stem = Path.GetFileNameWithoutExtension(axamlPath);
            var partials = Directory.GetFiles(dir, stem + "*.cs");
            if (partials.Length == 0) continue;

            var code = string.Join("\n",
                partials.OrderBy(p => p, StringComparer.Ordinal)
                        .Select(p => SourceScanner.StripComments(File.ReadAllText(p))));

            // 两个接线形态都要算"已接线"：code-behind `+=` 与 XAML 属性式（阶段 199 的勘误）
            var xamlWiredNames = ControlWiringFace.XamlWiredTextBoxes(axaml)
                .Keys.Concat(ControlWiringFace.XamlWiredNumberBoxes(axaml).Keys)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var box in names)
            {
                var name = Regex.Escape(box.Name);
                var byCodeBehind = Regex.IsMatch(code, $@"(?<![\w.]){name}\.(?:TextChanged|ValueChanged)\s*\+=");
                var byXaml = xamlWiredNames.Contains(box.Name);
                if (byCodeBehind || byXaml)
                {
                    wired.Add(Key(box));
                    (byXaml ? wiredByXaml : wiredByCodeBehind).Add(Key(box));
                    continue;
                }

                // 读点：`X.member` 后面不是赋值号（`==` 算读），或**裸名**出现（实参 / 转发属性）
                if (HasReadSite(code, box.Name)) read.Add(Key(box));
                else unread.Add(box);
            }
        }

        return new ScanResult(declared, wired, wiredByCodeBehind, wiredByXaml, read, unread);
    }

    /// <summary>
    /// "这个框有没有读点"的判定（**抽成纯函数**，好让它自己有判据 —— 见
    /// <see cref="ReadDetection_SelfTest"/>）：`X.member` 后面不是赋值号（`==` 算读、`+=` 算写），
    /// 或者出现**裸名** `X`（实参 / 转发属性 `=&gt; X` —— 说明有人拿到了这个控件）。
    /// </summary>
    internal static bool HasReadSite(string strippedCode, string name)
    {
        var escaped = Regex.Escape(name);
        foreach (Match m in Regex.Matches(strippedCode, $@"(?<![\w.]){escaped}(?![\w])\s*(\.\s*\w+)?"))
        {
            if (!m.Groups[1].Success)
            {
                // 裸名：**但"写侧助手函数的实参"不算读** —— `AttachChangeHandler(TxtAuthor)`
                // 只是给这个框挂"改了就标脏"，它既不读值也不保存；把它算成读会让
                // "标脏了却没人读"这种形状悄悄溜过去（阶段 196 nc1 就是这一刀）。
                var before = strippedCode[..m.Index];
                var isWriteSideArg = before.EndsWith("AttachChangeHandler(", StringComparison.Ordinal)
                                     || before.EndsWith("MarkNotEffective(", StringComparison.Ordinal);
                if (!isWriteSideArg) return true;
                continue;
            }

            var after = strippedCode[(m.Index + m.Length)..].TrimStart();
            var isAssign = after.StartsWith("=", StringComparison.Ordinal)
                           && !after.StartsWith("==", StringComparison.Ordinal);
            var isCompound = after.StartsWith("+=", StringComparison.Ordinal)
                             || after.StartsWith("-=", StringComparison.Ordinal);
            if (!isAssign && !isCompound) return true;
        }

        return false;
    }

    /// <summary>
    /// **扫描面自检 ＋ 没人读的框必须登记**：每个没接线的具名输入框，要么有读点、要么在
    /// <see cref="DisplayOnly"/> 里；否则点名（新加一个"只回填、没人读"的输入框会被当场挡住）。
    /// </summary>
    [Fact]
    public void EveryUnwiredInput_IsReadOrRegisteredAsDisplayOnly()
    {
        var scan = Scan();

        // 面自检：扫描塌了（改名/目录挪走）时下面全是空集差集 ⇒ 恒绿
        Assert.True(scan.Declared.Count >= 200,
            $"只扫到 {scan.Declared.Count} 个具名输入框 —— 扫描面可能塌了（阶段 196 实测 213 个）");
        Assert.True(scan.Wired.Count >= 110,
            $"只扫到 {scan.Wired.Count} 个已接线输入框 —— 扫描面可能塌了（阶段 199 实测 117 ＝ code-behind 91 ＋ XAML 属性式 26）");
        Assert.True(scan.WiredByXaml.Count >= 25,
            $"只扫到 {scan.WiredByXaml.Count} 条 XAML 属性式接线 —— 第三形态的扫描面可能塌了（阶段 199 实测 26 条）");
        Assert.True(scan.Read.Count >= 85,
            $"只扫到 {scan.Read.Count} 个「没接线但有读点」的输入框 —— 扫描面可能塌了（阶段 199 实测 92 个）");

        var offenders = scan.Unread
            .Where(b => !DisplayOnly.ContainsKey((b.File, b.Name)))
            .Select(b => $"{b.File}::{b.Name}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "这些具名输入框**没有接线、也没有读点**（用户打了字没人读 ⇒ 与 #90 同一类形状）：\n  "
            + string.Join("\n  ", offenders)
            + "\n若它本来就是只读展示框，请登记进 DisplayOnly 并写明理由；否则它是个缺口。");
    }

    /// <summary>
    /// **登记表自己的双向差集**：每条登记都必须真的存在（名字写歪 ⇒ 红）、**真的没有读点**
    /// （不许拿登记表藏一个其实有人读的框）、且**没接线**（接了线的框归那两个接线面）。
    /// </summary>
    [Fact]
    public void DisplayOnlyRegistry_IsHonest()
    {
        var scan = Scan();

        var missing = new List<string>();
        var actuallyRead = new List<string>();
        var actuallyWired = new List<string>();

        foreach (var ((file, name), _) in DisplayOnly)
        {
            var box = new Box(file, name);
            var key = Key(box);

            if (!scan.Declared.Any(b => b.File == file && b.Name == name))
                missing.Add($"{file}::{name}");
            if (scan.Read.Contains(key))
                actuallyRead.Add($"{file}::{name}");
            if (scan.Wired.Contains(key))
                actuallyWired.Add($"{file}::{name}");
        }

        Assert.True(missing.Count == 0,
            $"DisplayOnly 登记表里写了这些框，但 XAML 里没有（改名/删除后要同步）：{string.Join(", ", missing)}");
        Assert.True(actuallyRead.Count == 0,
            $"这些框登记为「只读展示」，但代码里**有读点** —— 登记表不许拿来藏一个其实有人读的框："
            + string.Join(", ", actuallyRead));
        Assert.True(actuallyWired.Count == 0,
            $"这些框登记为「只读展示」，但它们**接了线** —— 那归两个接线面管：{string.Join(", ", actuallyWired)}");
    }

    /// <summary>
    /// **读点判定自测**：把"读"与"写"的六种形状钉住 —— 这一面全靠这个判定，
    /// 它一旦把"写"误判成"读"，整面就会**假绿**（每个框都算有人读）。
    /// </summary>
    [Fact]
    public void ReadDetection_SelfTest()
    {
        // 读
        Assert.True(HasReadSite("var a = TxtAuthor.Text ?? \"\";", "TxtAuthor"));
        Assert.True(HasReadSite("if (TxtAuthor.Text == \"x\") { }", "TxtAuthor"));          // `==` 是读
        // ⚠ 写侧助手函数的实参**不算读**：`AttachChangeHandler(TxtAuthor)` 只给这个框挂
        //   "改了就标脏"，它既不读值也不保存 —— 把它算成读会让"标脏了却没人读"悄悄溜过去
        //   （阶段 196 nc1 就是这一刀：把 SaveToProject 里 TxtAuthor 的读换成常量之后，
        //    这条口径必须让它变成"没人读"）。
        Assert.False(HasReadSite("AttachChangeHandler(TxtAuthor);", "TxtAuthor"));
        Assert.True(HasReadSite("var a = TxtAuthor.Text;", "TxtAuthor"));                   // 真读
        Assert.True(HasReadSite("protected override TextBox AiInput => InputBox;", "InputBox")); // 裸名（转发属性）

        // 写
        Assert.False(HasReadSite("TxtAuthor.Text = _project.Author ?? \"\";", "TxtAuthor"));
        Assert.False(HasReadSite("TxtAuthor.Text += \"x\";", "TxtAuthor"));
        // 注释里的名字不算 —— 但**剥注释是调用方的责任**（本面走 SourceScanner.StripComments）：
        Assert.False(HasReadSite(
            SourceScanner.StripComments("// 只提了一句 TxtAuthor 的注释\nTxtAuthor.Text = \"\";"), "TxtAuthor"));
        Assert.False(HasReadSite("OtherBox.Text = \"\";", "TxtAuthor"));                    // 别的框不算
        Assert.False(HasReadSite("TxtAuthorX.Text = \"\";", "TxtAuthor"));                  // 前缀相同的名字不算
    }



    /// <summary>
    /// **「只读展示」的登记必须有 XAML 依据**（阶段 197）：登记表里那 4 条必须真的带
    /// `IsReadOnly="True"` —— 否则"只读展示"这个理由就是空话（一个**可编辑**、又没人读的输入框
    /// 恰恰是本面要抓的缺口）。
    /// </summary>
    [Fact]
    public void DisplayOnlyRegistrations_AreReallyReadOnlyInXaml()
    {
        var root = Path.Combine(
            SourceScanner.RepoRoot, ControlWiringFace.ViewsRelative.Replace('/', Path.DirectorySeparatorChar));

        var bad = new List<string>();
        foreach (var ((file, name), _) in DisplayOnly)
        {
            var axamlPath = Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(axamlPath), $"DisplayOnly 登记里的文件不存在：{file}");

            var axaml = File.ReadAllText(axamlPath);
            var tag = Regex.Match(axaml,
                $@"<TextBox\b[^>]*x:Name=""{Regex.Escape(name)}""[^>]*>", RegexOptions.Singleline);
            if (!tag.Success)
            {
                bad.Add($"{file}::{name}（XAML 里找不到这个 Named TextBox）");
                continue;
            }

            if (!tag.Value.Contains("IsReadOnly=\"True\"", StringComparison.Ordinal))
                bad.Add($"{file}::{name}（这个 TextBox 没有 `IsReadOnly=\"True\"`）");
        }

        Assert.True(bad.Count == 0,
            "登记为「只读展示」的框必须在 XAML 里**真的只读**（否则它是个可编辑、却没人读的输入框）：\n  "
            + string.Join("\n  ", bad));
    }

}
