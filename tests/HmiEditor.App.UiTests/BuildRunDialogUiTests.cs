using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`BuildRunDialog`（编译并运行）的按钮面**（阶段 259；登记 `#39` 残余面的**最后一格**）。
///
/// <para><b>为什么单独立判据：</b>它由「编译并运行」那条链打开 ⇒ 不是"菜单打开的窗口"（第三层够不到）、
/// 也不是停靠面板（第四层够不到）⇒ 专题判据（第十次，收尾）。</para>
///
/// <para>🩸 <b>本判据必须"只构造、不 `Show()`"</b>：这个窗的构造函数里挂着
/// <c>Loaded += async (_, _) => await RunBuildAndSimulatorAsync();</c> —— **一显示就会真跑构建**
/// （生成代码 → CMake → 编译器，还会 `Process.GetProcessesByName("hmi_sim")` 杀进程）。
/// 与 `LuaHelpDialogUiTests` 同处置：不显示也能 `RaiseEvent(Button.ClickEvent)` 驱动 XAML 挂上的处理器，
/// 而"从未 `Show()` 过的 `Window.Close()` 不抛、且会触发 `Closed`"。</para>
///
/// <para><b>3 个具名动作控件与桶：</b>
/// <list type="bullet">
///   <item><b>`BtnClose`</b> = **Moves**：`Click="OnCloseClicked"` ⇒ `Close()`（`OnClosing` 里的
///     `_cts.Cancel()` 在**无在途构建**时无后果）。</item>
///   <item><b>`BtnStop`</b> = **Never**（**如实划界**）：它的语义是"有在途模拟器进程 ⇒
///     `Kill(entireProcessTree)`，否则 `_cts.Cancel()`" —— 而"在途"只能由**真构建**产生
///     （见上：一显示就跑）；本判据不点它，只把面闭合到它 ＋ 断言它**初始就是关的**
///     （`IsEnabled="False"`，因为 `SetStopEnabled`/`Finish` 只在 Loaded 之后的流程里被调用）。</item>
///   <item><b>`BtnTitleClose`</b> = **Never**：`ChromeWindow` 标题栏关闭按钮 ⇒ 与 `BtnClose` 同后果。</item>
/// </list></para>
///
/// <para><b>不抓什么：</b>① 不判真构建/真编译（那要外部工具链，且会起进程）；② 不判日志框内容；
/// ③ 不判真实命中测试；④ 不判"模拟器真的起来了"（`RunSimulatorAsync` 属真机/外部动作）。</para>
/// </summary>
[Collection("Language-global")]
public class BuildRunDialogUiTests
{
    private readonly ITestOutputHelper _out;

    public BuildRunDialogUiTests(ITestOutputHelper o) => _out = o;

    /// <summary>造窗（**只构造、不 Show** —— 见类注释）。两个目录参数在"不显示"时不会被用到。</summary>
    private static BuildRunDialog New(string sandboxRoot) => new(
        new HmiProject(),
        workDir: Path.Combine(sandboxRoot, "work"),
        lvglDir: Path.Combine(sandboxRoot, "lvgl"));

    private static Button Btn(Control root, string name) => ControlWiringFace.Find<Button>(root, name);

    [Fact]
    public void Face_MatchesTheDeclaredTable()
    {
        using var sandbox = new DataRootSandbox("buildrun-face");
        UiTestHost.Run(() =>
        {
            var dialog = New(sandbox.DataRoot);
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
                Assert.Equal(new[] { "BtnClose", "BtnStop", "BtnTitleClose" }, actual);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void NotShown_ThereforeNoBuildWasStarted_AndBothButtonsStayDisabled()
    {
        using var sandbox = new DataRootSandbox("buildrun-idle");
        UiTestHost.Run(() =>
        {
            var dialog = New(sandbox.DataRoot);
            try
            {
                // ① 两个按钮初值都是关的（XAML `IsEnabled="False"`）：`SetStopEnabled(true)` 只在
                //    `RunBuildAndSimulatorAsync` 的第一行 —— 而它挂在 `Loaded` 上 ⇒ **没显示就没跑构建**。
                Assert.False(Btn(dialog, "BtnStop").IsEnabled);
                Assert.False(Btn(dialog, "BtnClose").IsEnabled);

                // ② 也真的没产生任何构建产物（工作目录没被创建/写入）
                Assert.False(Directory.Exists(Path.Combine(sandbox.DataRoot, "work")));

                // ③ `BtnStop` 在本判据里**不点**（理由见类注释）：它的另一半语义要有"在途构建"
                //    —— 而那是显示本窗才会发生的事。
                UiTestHost.Pump(5);
                Assert.False(Btn(dialog, "BtnStop").IsEnabled);

                JudgeReadings.Write(_out, "编译运行窗 · 未显示（#39 残余面 · 阶段 259）", new[]
                {
                    "面：具名 Button 3 个 —— BtnStop / BtnClose / BtnTitleClose（双向差集，已断言）",
                    $"未 Show ⇒ BtnStop.IsEnabled={Btn(dialog, "BtnStop").IsEnabled}、"
                        + $"BtnClose.IsEnabled={Btn(dialog, "BtnClose").IsEnabled}（构建流程没启动）",
                    "BtnStop 如实记 Never：它的语义要有「在途构建」（真 cmake/编译器/子进程），本判据不点",
                });
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Close_ClosesTheDialog()
    {
        using var sandbox = new DataRootSandbox("buildrun-close");
        UiTestHost.Run(() =>
        {
            var dialog = New(sandbox.DataRoot);
            var closed = 0;
            dialog.Closed += (_, _) => closed++;
            try
            {
                // 从未 Show 过也能驱动 XAML 挂上的处理器（见类注释；`Close()` 也不抛）
                Btn(dialog, "BtnClose").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                UiTestHost.Pump(3);

                Assert.Equal(1, closed);
                Assert.False(dialog.IsVisible);

                JudgeReadings.Write(_out, "编译运行窗 · 关闭（#39 残余面 · 阶段 259）", new[]
                {
                    "面：具名 Button 3 个（双向差集，已断言）",
                    $"点 BtnClose ⇒ Closed 触发 {closed} 次；IsVisible={dialog.IsVisible}",
                    "`OnClosing` 里的 `_cts.Cancel()` 在无在途构建时无后果（本判据就是那一档）",
                });
            }
            finally { dialog.Close(); }
        });
    }
}
