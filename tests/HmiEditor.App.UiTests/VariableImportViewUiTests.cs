using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`VariableImportView`（变量 CSV 导入）的按钮面**（阶段 254；登记 `#39` 残余面）。
///
/// <para><b>为什么单独立判据：</b>它由**项目树配置节点** `variable_import` 打开
/// （`ProjectExplorerView` → `DockLayoutState.RaiseConfigNodeActivated` → `MainWindow.OpenConfigWindow`
/// → `ShowModalDialog(new VariableImportView(...))`）⇒ 不是"菜单打开的窗口"，第三层进不去；
/// 也不是停靠面板 ⇒ 第四层够不到。⇒ 专题判据（第四次，见 §166/§168/§169）。</para>
///
/// <para><b>🩸 为什么这一条要动产品代码（加一条缝）：</b>`_selectedFilePath` **只能**由平台文件选择器写入；
/// headless 下选择器桩返回空集合 ⇒ 那一格永远是空 ⇒ 按本仓纪律只能把 <c>BtnParse</c>/<c>BtnImport</c>
/// 记成 `Inert`（"首行早退"），**恰好把最强的两条腿记没了**。阶段 254 给
/// <see cref="VariableImportView"/> 加了一条 **internal 注入缝**（`Func&lt;Task&lt;string?&gt;&gt;`，
/// 把"平台选择器"这一步换掉；传 null 与加缝前逐字相同）—— 与阶段 209 给 `AiIconDialog` 加
/// `IIconGenerationSource` 同一处置。⇒ 本判据能驱动**整条链**：选文件 → 解析 → 写入模型。</para>
///
/// <para><b>5 个具名动作控件与桶</b>（面闭合见 <see cref="Face_MatchesTheDeclaredTable"/>）：
/// `BtnSelectFile` / `BtnParse` / `BtnImport` / `BtnCancel` = **Moves**（四条各有自己的模型/界面落点）；
/// `BtnTitleClose` = **Never**（`ChromeWindow` 标题栏关闭按钮 ⇒ 与 `BtnCancel` 同后果，保留一条）。</para>
///
/// <para><b>不抓什么：</b>① 不判真实命中测试；② 不判平台选择器本身（缝把它换掉了 —— 那是框架的）；
/// ③ 不判 `CsvVariableImporter` 的解析规则（有专门的 Core 判据与 `CsvImportIssueLocalizationGuardTests`）；
/// ④ 不判导入后的落盘（`MainWindow` 的事）。</para>
/// </summary>
[Collection("Language-global")]
public class VariableImportViewUiTests
{
    private readonly ITestOutputHelper _out;

    public VariableImportViewUiTests(ITestOutputHelper o) => _out = o;

    /// <summary>两份变量的合法 CSV（表头 `Name,DataType,Address,Unit`）。</summary>
    private const string Csv = "Name,DataType,Address,Unit\n温度,Int16,MW10,℃\n压力,Int16,MW12,bar\n";

    private static string WriteCsv(DataRootSandbox sandbox)
    {
        Directory.CreateDirectory(sandbox.DataRoot);   // 沙箱根不保证已建（与其它用它的判据同处置）
        var path = Path.Combine(sandbox.DataRoot, "vars.csv");
        File.WriteAllText(path, Csv);
        return path;
    }

    private static VariableImportView Open(VariableManager manager, string csvPath)
    {
        var dialog = new VariableImportView(manager, () => Task.FromResult<string?>(csvPath));
        dialog.Show();
        UiTestHost.Pump(5);
        return dialog;
    }

    /// <summary>`ParseFile` / `ImportVariables` 的处理器是 async（真读文件）⇒ 必须等条件成立，不能只 Pump。</summary>
    private static async Task UntilAsync(Func<bool> condition, string what, int timeoutMs = 10000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            UiTestHost.Pump(2);
            if (condition()) return;
            await Task.Delay(20);
        }

        Assert.Fail($"等不到：{what}（已等 {timeoutMs} ms）");
    }

    private static void Click(Control root, string name)
    {
        ControlWiringFace.Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump(3);
    }

    private static Button Btn(Control root, string name) => ControlWiringFace.Find<Button>(root, name);
    private static string TextOf(Control root, string name) => ControlWiringFace.Find<TextBlock>(root, name).Text ?? "";
    private static int PreviewCount(Control root) => ControlWiringFace.Find<ListBox>(root, "PreviewList").Items.Count;

    [Fact]
    public void Face_MatchesTheDeclaredTable()
    {
        UiTestHost.Run(() =>
        {
            using var sandbox = new DataRootSandbox("varimport-face");
            var dialog = Open(new VariableManager(), WriteCsv(sandbox));
            try
            {
                var actual = dialog.GetLogicalDescendants().OfType<Button>()
                    .Where(b => b.GetType() == typeof(Button))
                    .Where(b => b.Name is not null)
                    .Select(b => b.Name!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();

                // 双向差集：少一个（控件被删/改名）与多一个（新按钮没人分类）都红
                Assert.Equal(new[] { "BtnCancel", "BtnImport", "BtnParse", "BtnSelectFile", "BtnTitleClose" }, actual);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void SelectFile_RecordsTheChosenPathAndEnablesParse()
    {
        UiTestHost.Run(() =>
        {
            using var sandbox = new DataRootSandbox("varimport-select");
            var csvPath = WriteCsv(sandbox);
            var dialog = Open(new VariableManager(), csvPath);
            try
            {
                // 初态：没有文件 ⇒ 解析入口关着
                Assert.False(Btn(dialog, "BtnParse").IsEnabled);

                Click(dialog, "BtnSelectFile");

                Assert.Equal("vars.csv", TextOf(dialog, "FilePathText"));   // 只显示文件名
                Assert.True(Btn(dialog, "BtnParse").IsEnabled);
                Assert.False(Btn(dialog, "BtnImport").IsEnabled);            // 还没解析 ⇒ 导入关着
                Assert.Contains(csvPath, TextOf(dialog, "StatusText"));      // 状态行给出**完整路径**

                JudgeReadings.Write(_out, "变量导入 · 选文件（#39 残余面 · 阶段 254）", new[]
                {
                    "面：具名 Button 5 个 —— 声明表见本类 xmldoc（双向差集，已断言）",
                    $"缝给的路径 `{csvPath}` ⇒ 文件名格 `{TextOf(dialog, "FilePathText")}`；"
                        + $"BtnParse.IsEnabled={Btn(dialog, "BtnParse").IsEnabled}；BtnImport.IsEnabled={Btn(dialog, "BtnImport").IsEnabled}",
                    "⚠ 缝只换掉**平台选择器**这一步；`ApplySelectedFile` 是两条路径共用的产品代码",
                });
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public async Task Parse_BuildsThePreviewAndEnablesImport()
    {
        using var sandbox = new DataRootSandbox("varimport-parse");
        var manager = new VariableManager();
        await UiTestHost.RunAsync(async () =>
        {
            var dialog = Open(manager, WriteCsv(sandbox));
            try
            {
                Click(dialog, "BtnSelectFile");
                Assert.Equal(0, PreviewCount(dialog));

                Click(dialog, "BtnParse");
                await UntilAsync(() => PreviewCount(dialog) == 2, "解析把两份变量填进预览");

                Assert.Equal(2, PreviewCount(dialog));               // 两份变量都进预览
                Assert.True(Btn(dialog, "BtnImport").IsEnabled);
                Assert.Empty(manager.Variables);                     // 解析**不写模型**（要等导入）

                JudgeReadings.Write(_out, "变量导入 · 解析（#39 残余面 · 阶段 254）", new[]
                {
                    "面：具名 Button 5 个 —— 声明表见本类 xmldoc（双向差集，已断言）",
                    $"预览条数 {PreviewCount(dialog)}；BtnImport.IsEnabled={Btn(dialog, "BtnImport").IsEnabled}；"
                        + $"模型里变量数 {manager.Variables.Count}（解析不落模型）",
                    $"状态行：`{TextOf(dialog, "StatusText")}`",
                });
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public async Task Import_WritesOnlyTheParsedVariables_AndSkipsDuplicates()
    {
        using var sandbox = new DataRootSandbox("varimport-import");
        var manager = new VariableManager();
        manager.Variables.Add(new Variable { Name = "温度", DataType = "Int16", Address = "MW99" });
        await UiTestHost.RunAsync(async () =>
        {
            var dialog = Open(manager, WriteCsv(sandbox));
            try
            {
                Click(dialog, "BtnSelectFile");
                Click(dialog, "BtnParse");
                await UntilAsync(() => PreviewCount(dialog) == 2, "解析完成（导入的前置）");

                Click(dialog, "BtnImport");
                await UntilAsync(() => manager.Variables.Count == 2, "导入写入模型");

                // 同名「温度」被跳过（产品语义：按名字去重），只新增「压力」
                Assert.Equal(2, manager.Variables.Count);
                Assert.Contains(manager.Variables, v => v.Name == "压力");
                Assert.Equal("MW99", manager.Variables.Single(v => v.Name == "温度").Address);   // 原有的没被覆盖
                Assert.False(Btn(dialog, "BtnImport").IsEnabled);                                // 导完自己关掉
                Assert.True(dialog.IsVisible, "导入不该关窗");

                JudgeReadings.Write(_out, "变量导入 · 导入（#39 残余面 · 阶段 254）", new[]
                {
                    "面：具名 Button 5 个 —— 声明表见本类 xmldoc（双向差集，已断言）",
                    $"导入前模型 1 条（同名「温度」）⇒ 导入后 {manager.Variables.Count} 条；"
                        + $"BtnImport.IsEnabled={Btn(dialog, "BtnImport").IsEnabled}",
                    $"状态行：`{TextOf(dialog, "StatusText")}`",
                });
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Cancel_ClosesTheDialog()
    {
        UiTestHost.Run(() =>
        {
            using var sandbox = new DataRootSandbox("varimport-cancel");
            var dialog = Open(new VariableManager(), WriteCsv(sandbox));
            var closed = 0;
            dialog.Closed += (_, _) => closed++;
            try
            {
                Click(dialog, "BtnCancel");
                UiTestHost.Pump(3);

                Assert.Equal(1, closed);
                Assert.False(dialog.IsVisible);
            }
            finally { dialog.Close(); }
        });
    }
}
