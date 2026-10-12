using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **「读点在动作处理器」的输入框台账**（阶段 207）—— 阶段 196 的「谁读」面把**所有**没接线的
/// 具名输入框分成了"有读点/只读展示/没人读"；这一面再把其中**读点落在 `On*Clicked` 这类动作处理器里**
/// 的那些挑出来单独成账，因为它们的可驱动性完全取决于"那个动作在 headless 下能不能跑到"。
///
/// <para><b>为什么值得单独一张账</b>：这批框的读点**不在保存路径**上（属性面板那批走
/// `SaveToProject`），而散在**动作处理器**里 —— 处理器常常直连真实服务（设备/网络/LLM/模态对话框），
/// 于是"有没有判据"跟"有没有注入缝"绑在一起。阶段 205~207 已经为此补了两处缝
/// （`DeviceManagerView`、`DownloadToHmiView`），这张账就是把**剩下的**逐个点名登记。</para>
///
/// <para><b>判据形状</b>：① 扫描 ↔ 台账**双向闭合**（新冒出一个"读点在动作处理器"的框 ⇒ 红）；
/// ② `driven` 条目必须指向**真实存在**的判据文件；③ `registered` 条目必须写清**为什么不可驱动**
/// （≥30 字）；④ 面自检下限。</para>
/// </summary>
public class ActionHandlerReadInventoryTests
{
    /// <summary>一条：框（视图::控件）→ 判据文件（driven）或 nil，另附说明。</summary>
    private sealed record Entry(string View, string Name, string? JudgeFile, string Note);

    /// <summary>
    /// 台账。`JudgeFile` 非空 ⇒ 已驱动（那个判据文件里必须真的**点名**这个框）；
    /// 为空 ⇒ 登记不可驱动，`Note` 写清为什么。
    /// </summary>
    private static readonly Entry[] Entries =
    {
        // ── 已驱动：DeviceManagerView（阶段 205/206：注入缝补齐后 13 个全驱动）──
        new("DeviceManagerView.axaml", "TxtHost", "DeviceManagerInputReadFaceTests.cs", "假通道工厂记 TCP 参数"),
        new("DeviceManagerView.axaml", "TxtTcpPort", "DeviceManagerInputReadFaceTests.cs", "同上（含非法值回退 23 的口径）"),
        new("DeviceManagerView.axaml", "TxtBaudRate", "DeviceManagerInputReadFaceTests.cs", "假工厂记串口参数（含回退默认波特率）"),
        new("DeviceManagerView.axaml", "TxtLocalVersion", "DeviceManagerInputReadFaceTests.cs", "假更新服务记本地版本"),
        new("DeviceManagerView.axaml", "TxtHmiSwu", "DeviceManagerInputReadFaceTests.cs", "假更新服务记 .swu 路径"),
        new("DeviceManagerView.axaml", "TxtFwSwu", "DeviceManagerInputReadFaceTests.cs", "假刷写器记固件包路径"),
        new("DeviceManagerView.axaml", "TxtHmiLib", "DeviceManagerInputReadFaceTests.cs", "真打包器产出 hmi-<版本>.swu"),
        new("DeviceManagerView.axaml", "TxtOutputDir", "DeviceManagerInputReadFaceTests.cs", "同上（产出目录来自这一格）"),
        new("DeviceManagerView.axaml", "TxtPkgVersion", "DeviceManagerInputReadFaceTests.cs", "同上（包名里的版本来自这一格）"),
        new("DeviceManagerView.axaml", "TxtFelTool", "DeviceManagerInputReadFaceTests.cs", "假刷写器记 FEL 三格"),
        new("DeviceManagerView.axaml", "TxtFelImage", "DeviceManagerInputReadFaceTests.cs", "同上 ＋ 镜像不存在的校验分支"),
        new("DeviceManagerView.axaml", "TxtFelArgs", "DeviceManagerInputReadFaceTests.cs", "同上（参数模板）"),

        // ── 已驱动：DownloadToHmiView（阶段 207：补 IHmiDeployService ＋ 通道工厂两条缝）──
        new("DownloadToHmiView.axaml", "TxtHost", "DownloadToHmiInputReadFaceTests.cs", "假通道工厂记 TCP 参数"),
        new("DownloadToHmiView.axaml", "TxtTcpPort", "DownloadToHmiInputReadFaceTests.cs", "同上"),
        new("DownloadToHmiView.axaml", "TxtBaudRate", "DownloadToHmiInputReadFaceTests.cs", "假工厂记串口参数"),
        new("DownloadToHmiView.axaml", "TxtRemoteDir", "DownloadToHmiInputReadFaceTests.cs", "假部署服务记目标目录（含**空白 ⇒ 回退默认**：#95 的判据）"),

        // ── 已驱动：三个搜索框（阶段 200）──

        // ── 登记不可驱动（逐条写清原因）──
        //    ⚠ `PropertyPanelView` 那几个色框**不在本账里**：它们是 **XAML 属性式接线**
        //      （`TextChanged="OnPropertyChanged"` 一族）⇒ 归阶段 199 那条接线面的登记表；
        //      本账只管**没接线**、读点却在动作处理器里的框。
        // ── 已驱动：AiIconDialog（阶段 209：补 `IIconGenerationSource` 注入缝后 4 个全驱动）──
        new("AiIconDialog.axaml", "InputBox", "AiIconDialogInputReadFaceTests.cs", "批量条目列表进假源"),
        new("AiIconDialog.axaml", "ImageInputBox", "AiIconDialogInputReadFaceTests.cs", "单图提示词（裁剪后）进假源"),
        new("AiIconDialog.axaml", "WidthBox", "AiIconDialogInputReadFaceTests.cs", "画布宽度进假源（320）"),
        new("AiIconDialog.axaml", "HeightBox", "AiIconDialogInputReadFaceTests.cs", "画布高度进假源（240）"),

        new("TagMonitorView.axaml", "TxtWriteValue", "TagMonitorInputReadFaceTests.cs",
            "假值源记写入（`RuntimeBindingEngine` 本来就吃 `IRuntimeValueSource`，**不需要新产品缝**）"),

    };

    /// <summary>动作处理器的方法名形态（与阶段 207 的普查口径一致）。</summary>
    private static readonly Regex ActionHandler = new(
        @"^(On\w*(Clicked|Async|Changed|Send|Login|Confirm|Ok|Closed)|Try\w+|Search|Append\w*|Generate\w*)$",
        RegexOptions.Compiled);

    private static readonly Regex MethodPattern = new(
        @"\n\s*(?:public|private|protected|internal|static|async)[^\n(]*?\s(\w+)\s*\(",
        RegexOptions.Compiled);

    /// <summary>扫描：视图 ⇒ 那些"没接线、读点在动作处理器里"的具名输入框（框名 → 处理器名）。</summary>
    private static Dictionary<string, Dictionary<string, string>> Scan()
    {
        var root = Path.Combine(
            SourceScanner.RepoRoot, ControlWiringFace.ViewsRelative.Replace('/', Path.DirectorySeparatorChar));

        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var axamlPath in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, axamlPath).Replace('\\', '/');
            var axaml = File.ReadAllText(axamlPath);

            // 具名输入框（TextBox ＋ 两种数字框）——**跳过 `IsReadOnly="True"` 的只读展示框**：
            // 用户改不了它们，也就不存在"读点在动作处理器里却驱动不了"这回事
            // （`BuildRunDialog.OutputLog` 就是这么被扫进来的：只读日志框，读 `Text.Length` 做截断）。
            var names = Regex.Matches(axaml, @"<(?:TextBox|NumericUpDown|fa:FANumberBox)\b[^>]*>", RegexOptions.Singleline)
                .Select(m => m.Value)
                .Where(tag => !Regex.IsMatch(tag, @"IsReadOnly=""True""", RegexOptions.IgnoreCase))
                .Select(tag => Regex.Match(tag, @"x:Name=""([A-Za-z_]\w*)"""))
                .Where(m => m.Success)
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            if (names.Length == 0) continue;

            var dir = Path.GetDirectoryName(axamlPath)!;
            var stem = Path.GetFileNameWithoutExtension(axamlPath);
            var partials = Directory.GetFiles(dir, stem + "*.cs");
            if (partials.Length == 0) continue;

            var code = string.Join("\n",
                partials.OrderBy(p => p, StringComparer.Ordinal)
                        .Select(p => SourceScanner.StripComments(File.ReadAllText(p))));

            var hits = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var name in names)
            {
                // 接了线的框归接线面 —— **两种形态都要跳**：code-behind `+=` 与 XAML 属性式
                // （阶段 199 的教训：只认前者会把属性式接线的框误判成"没接线"）。
                if (Regex.IsMatch(code, $@"(?<![\w.]){Regex.Escape(name)}\.(?:TextChanged|ValueChanged)\s*\+="))
                    continue;
                if (ControlWiringFace.XamlWiredTextBoxes(axaml).ContainsKey(name)
                    || ControlWiringFace.XamlWiredNumberBoxes(axaml).ContainsKey(name))
                {
                    continue;
                }

                // 读点：与「谁读」面同一把尺子（`HasReadSite` 是那一面的 internal 纯函数）
                if (!AppUnwiredInputReadFaceTests.HasReadSite(code, name)) continue;

                var match = Regex.Match(code, $@"(?<![\w.]){Regex.Escape(name)}(?![\w])\s*(\.\s*\w+)?");
                if (!match.Success) continue;

                var head = code[..match.Index];
                var methods = MethodPattern.Matches(head);
                var method = methods.Count > 0 ? methods[^1].Groups[1].Value : "(顶层)";
                if (ActionHandler.IsMatch(method)) hits[name] = method;
            }

            if (hits.Count > 0) result[rel] = hits;
        }

        return result;
    }

    private static string Key(string view, string name) => view + "::" + name;

    [Fact]
    public void Face_MatchesTheInventory()
    {
        var scan = Scan();

        var scanned = scan.SelectMany(kv => kv.Value.Keys.Select(n => Key(kv.Key, n)))
            .OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var registered = Entries.Select(e => Key(e.View, e.Name))
            .OrderBy(s => s, StringComparer.Ordinal).ToArray();

        var missing = scanned.Except(registered).ToArray();
        var ghosts = registered.Except(scanned).ToArray();

        Assert.True(missing.Length == 0,
            "这些输入框的**读点在动作处理器里**，但台账里没有它们 —— 要么驱动起来、要么登记理由：\n  "
            + string.Join("\n  ", missing));
        Assert.True(ghosts.Length == 0,
            "台账里这些条目在扫描面里对不上（框改名/接线了/读点搬家了）：\n  "
            + string.Join("\n  ", ghosts));

        Assert.True(Entries.Length >= 21,
            $"台账只剩 {Entries.Length} 条 —— 阶段 210 实测 21 条（13 DeviceManager 除只读展示 ＋ 4 DownloadToHmi ＋ 4 AiIcon ＋ TagMonitor），面可能塌了");

        // 扫描口径自检：**可写**的 `TxtWriteValue` 必须在面内；**只读**的 `OutputLog` 必须在面外
        Assert.True(scan.TryGetValue("TagMonitorView.axaml", out var tagMonitor)
                    && tagMonitor.ContainsKey("TxtWriteValue"),
            "扫描面丢了 TagMonitorView.TxtWriteValue（可写输入框）");
        Assert.False(scan.TryGetValue("BuildRunDialog.axaml", out var buildRun)
                     && buildRun.ContainsKey("OutputLog"),
            "扫描面把只读展示框 OutputLog 当成输入框了（应当由 IsReadOnly=\"True\" 排除）");
    }

    [Fact]
    public void EveryEntry_IsDrivenOrExplained()
    {
        var problems = new List<string>();
        var driven = 0;
        var registered = 0;

        foreach (var e in Entries)
        {
            if (e.JudgeFile is { Length: > 0 } judge)
            {
                driven++;
                var path = Path.Combine(SourceScanner.RepoRoot, "tests/HmiEditor.App.UiTests", judge);
                if (!File.Exists(path))
                {
                    problems.Add($"{e.View}::{e.Name} 的判据文件不存在：{judge}");
                    continue;
                }

                // 指针级核对：判据里必须真的**点名**这个框
                if (!Regex.IsMatch(File.ReadAllText(path), $@"""{Regex.Escape(e.Name)}"""))
                    problems.Add($"{e.View}::{e.Name} 说由 {judge} 驱动，但那个判据里没有点名这个框");
            }
            else
            {
                registered++;
                if (e.Note.Length < 30)
                    problems.Add($"{e.View}::{e.Name} 既没有判据、理由也写得太短（{e.Note.Length} 字）");
            }
        }

        Assert.True(problems.Count == 0, "台账核对不过：\n  " + string.Join("\n  ", problems));

        // 🎯 棘轮（阶段 210）：这一族的**全部**输入框都驱动起来了 —— `Registered` 必须是空的。
        //   真要登记，得先确认"连假实现也观测不到"，并把理由写足（阶段 210 就抓到一条**登记错了**的：
        //   `TagMonitorView.TxtWriteValue` 当时写的是"要模拟器/设备侧的注入缝"，而 `RuntimeBindingEngine`
        //   本来就吃 `IRuntimeValueSource` —— 登记理由必须**验过**）。
        Assert.True(driven >= 21, $"只有 {driven} 条已驱动 —— 阶段 210 实测 21 条");
        Assert.True(registered == 0,
            "有框被登记为「不可驱动」—— 先验一遍理由（阶段 210 的经验：有一条其实早就可驱动）："
            + string.Join(", ", Entries.Where(e => e.JudgeFile is null).Select(e => e.View + "::" + e.Name)));
    }

    /// <summary>
    /// **登记的那批必须真的"读点在动作处理器里"**：抽查它们的处理器名与台账口径一致，
    /// 免得有人把"其实已经能驱动"的框塞进登记表当遮羞布。
    /// </summary>
    [Fact]
    public void RegisteredEntries_AreReallyInActionHandlers()
    {
        var scan = Scan();
        var problems = new List<string>();

        foreach (var e in Entries.Where(x => x.JudgeFile is null))
        {
            if (!scan.TryGetValue(e.View, out var boxes) || !boxes.TryGetValue(e.Name, out var handler))
            {
                problems.Add($"{e.View}::{e.Name}：扫描面里根本没有它 —— 登记已过期");
                continue;
            }

            if (!ActionHandler.IsMatch(handler))
                problems.Add($"{e.View}::{e.Name}：读点在 `{handler}` 里，不像是动作处理器");
        }

        Assert.True(problems.Count == 0, "登记项核对不过：\n  " + string.Join("\n  ", problems));
    }
}
