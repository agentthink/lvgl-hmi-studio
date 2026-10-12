using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HmiEditor.App.Resources;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`FontSubsetView`（字体子集化分析）的按钮面**（阶段 258；登记 `#39` 残余面）。
///
/// <para><b>为什么单独立判据：</b>它由生成链那条 `ShowModalDialog(...)` 打开 ⇒ 不是"菜单打开的窗口"
/// （第三层够不到）、也不是停靠面板（第四层够不到）⇒ 专题判据（第八次）。</para>
///
/// <para><b>🩸 为什么这一条也要动产品（加一条缝）：</b>`ExportCharset()` 的落点来自
/// `SaveFilePickerAsync`，headless 下桩返回 null ⇒ 在 `if (file == null) return;` 处早退
/// ⇒ **"导出的文件里到底写了什么"原理上够不到**（只能记 Inert「平台桩」）。阶段 258 给它加了
/// **internal 注入缝**（`Func&lt;Task&lt;string?&gt;&gt;`，替掉"平台另存为"这一步；传 null 与加缝前逐字相同），
/// 与阶段 209/254 同一处置。⇒ 判据能给一个沙箱路径，`ExportCharsetFile(report, path)` 真的落盘、内容可判。</para>
///
/// <para><b>3 个具名动作控件与桶</b>：`BtnExportCharset` = **Moves**（沙箱里出现 `charset.txt`
/// 且内容 = 分析器给的字符集；提示行更新）；`BtnClose` = **Moves**（关窗）；
/// `BtnTitleClose` = **Never**（标题栏关闭按钮 ⇒ 与 `BtnClose` 同后果，保留一条）。</para>
/// </summary>
[Collection("Language-global")]
public class FontSubsetViewUiTests
{
    private readonly ITestOutputHelper _out;

    public FontSubsetViewUiTests(ITestOutputHelper o) => _out = o;

    /// <summary>造一个有可见文本的工程（字符集非空 —— 空集会让"导出了什么"退化成恒真）。</summary>
    private static HmiProject Project()
    {
        var project = new HmiProject();
        var screen = ScreenFactory.Create(project, name: "main");
        screen.Controls.Add(new HmiControl
        {
            Id = "lbl1", Name = "标题", Type = HmiControlType.Label, Text = "温度报警",
        });
        project.Screens.Add(screen);
        return project;
    }

    /// <summary>期望的导出内容：分析器给的字符集，每个字符一行（与 `ExportCharsetFile` 同一来源）。</summary>
    private static string ExpectedCharset(HmiProject project)
    {
        var report = FontSubsetAnalyzer.Analyze(project, fontSizePx: 20, bpp: 16);
        var sb = new StringBuilder();
        foreach (var c in report.Characters) sb.Append(c).Append('\n');
        return sb.ToString();
    }

    private static FontSubsetView Open(HmiProject project, string savePath)
    {
        var view = new FontSubsetView(project, () => Task.FromResult<string?>(savePath));
        view.Show();
        UiTestHost.Pump(5);
        return view;
    }

    private static void Click(Control root, string name)
    {
        ControlWiringFace.Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump(3);
    }

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

    [Fact]
    public void Face_MatchesTheDeclaredTable()
    {
        using var sandbox = new DataRootSandbox("fontsubset-face");
        UiTestHost.Run(() =>
        {
            // 缝给的路径这两个事实里**永远用不到**（不点导出）；只用沙箱根下的名字，不碰系统临时目录
            // —— `TestTempDirUsageGuardTests` 明确禁止测试直接用 `Path.GetTempPath()`。
            var view = Open(Project(), Path.Combine(sandbox.DataRoot, "unused-charset.txt"));
            try
            {
                var actual = view.GetLogicalDescendants().OfType<Button>()
                    .Where(b => b.GetType() == typeof(Button))
                    .Where(b => b.Name is not null)
                    .Select(b => b.Name!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();

                Assert.Equal(new[] { "BtnClose", "BtnExportCharset", "BtnTitleClose" }, actual);
            }
            finally { view.Close(); }
        });
    }

    [Fact]
    public async Task ExportCharset_WritesTheAnalyzedCharacters_AndUpdatesTheHint()
    {
        using var sandbox = new DataRootSandbox("fontsubset-export");
        Directory.CreateDirectory(sandbox.DataRoot);

        await UiTestHost.RunAsync(async () =>
        {
            var project = Project();
            var path = Path.Combine(sandbox.DataRoot, "charset.txt");
            var view = Open(project, path);
            try
            {
                Assert.False(File.Exists(path));
                var hintBefore = ControlWiringFace.Find<TextBlock>(view, "ExportHintText").Text;

                Click(view, "BtnExportCharset");
                await UntilAsync(() => File.Exists(path), "导出把文件落到缝给的路径上");

                // ① 文件内容 = 分析器给的字符集（每个字符一行、UTF-8 无 BOM）
                var expected = ExpectedCharset(project);
                Assert.Equal(expected, File.ReadAllText(path, Encoding.UTF8));
                Assert.True(expected.Length > 0, "夹具工程没有可见文本 ⇒ 这条判据会恒真");

                // ② 提示行被更新（与导出前不同；文案来自字典的 `font.exported`）
                var hintAfter = ControlWiringFace.Find<TextBlock>(view, "ExportHintText").Text ?? "";
                Assert.NotEqual(hintBefore, hintAfter);
                Assert.NotEqual("", hintAfter);

                JudgeReadings.Write(_out, "字体子集 · 导出字符集（#39 残余面 · 阶段 258）", new[]
                {
                    "面：具名 Button 3 个 —— BtnExportCharset / BtnClose / BtnTitleClose（双向差集，已断言）",
                    $"缝给路径 `{path}` ⇒ 文件 {new FileInfo(path).Length} 字节、"
                        + $"{expected.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length} 个字符（每字符一行）",
                    $"提示行：`{hintBefore}` → `{hintAfter}`",
                });
            }
            finally { view.Close(); }
        });
    }

    [Fact]
    public void Close_ClosesTheDialog()
    {
        using var sandbox = new DataRootSandbox("fontsubset-close");
        UiTestHost.Run(() =>
        {
            var view = Open(Project(), Path.Combine(sandbox.DataRoot, "unused-charset.txt"));
            var closed = 0;
            view.Closed += (_, _) => closed++;
            try
            {
                Click(view, "BtnClose");
                UiTestHost.Pump(3);

                Assert.Equal(1, closed);
                Assert.False(view.IsVisible);
            }
            finally { view.Close(); }
        });
    }
}
