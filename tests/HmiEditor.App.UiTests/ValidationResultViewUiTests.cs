using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`ValidationResultView`（生成代码验证结果）的按钮面**（阶段 255；登记 `#39` 残余面）。
///
/// <para><b>为什么单独立判据：</b>它由「生成代码 → 验证」那条链在 `MainWindow` 里
/// <c>ShowModalDialog(...)</c> 打开 ⇒ 不是"菜单打开的窗口"（第三层够不到）、也不是停靠面板
/// （第四层够不到）⇒ 专题判据（第五次：§166/§168/§169/§170/本节）。</para>
///
/// <para><b>与既有判据的分工：</b>`FindingDisplayTextGuardTests` 判的是**文案**（徽章/分类列的映射 ＋
/// 「复制全部」那一行怎么拼，是源码扫描面）—— 它**不点按钮、不读剪贴板**；`GenFindingLocalizationGuardTests`
/// 判的是生成器那侧的文案。本判据判的是**按钮点下去真的发生了什么**：写剪贴板 ＋ 提示行 ＋ 关窗。</para>
///
/// <para><b>3 个具名动作控件与桶</b>（面闭合见 <see cref="Face_MatchesTheDeclaredTable"/>）：
/// `BtnCopyAll` = **Moves**（剪贴板文本 ＋ `HintText`）；`BtnClose` = **Moves**（关窗）；
/// `BtnTitleClose` = **Never**（`ChromeWindow` 标题栏关闭按钮 ⇒ 与 `BtnClose` 同后果，保留一条）。</para>
///
/// <para><b>不抓什么：</b>① 不重复判文案映射（`FindingDisplayTextGuardTests` 管）；② 不判真实命中测试；
/// ③ 剪贴板本身是**平台**的 —— 本判据只判"产品把哪段文本交给了它"（headless 桩能读回来，读数里记下来）。</para>
/// </summary>
[Collection("Language-global")]
public class ValidationResultViewUiTests
{
    private readonly ITestOutputHelper _out;

    public ValidationResultViewUiTests(ITestOutputHelper o) => _out = o;

    private static IReadOnlyList<GeneratedCodeFinding> Findings() => new[]
    {
        new GeneratedCodeFinding(FindingSeverity.Error, FindingCategory.Structure,
            "GEN001", "控件缺少必需属性", "Screen main / Control c1"),
        new GeneratedCodeFinding(FindingSeverity.Warning, FindingCategory.Binding,
            "GEN002", "绑定目标不存在", "Screen main / Control c2"),
    };

    /// <summary>期望的剪贴板文本 —— 用**产品自己的**合成函数，不在这儿抄一遍格式。</summary>
    private static string ExpectedCopyText(IReadOnlyList<GeneratedCodeFinding> findings) =>
        string.Join(Environment.NewLine, findings.Select(FindingDisplayText.CopyLine));

    private static ValidationResultView Open(IReadOnlyList<GeneratedCodeFinding> findings)
    {
        var view = new ValidationResultView(findings);
        view.Show();
        UiTestHost.Pump(5);
        return view;
    }

    private static void Click(Control root, string name)
    {
        ControlWiringFace.Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump(3);
    }

    private static string HintText(Control root) => ControlWiringFace.Find<TextBlock>(root, "HintText").Text ?? "";

    /// <summary>读剪贴板（**异步**，不阻塞 UI 线程 —— 阻塞式等待在这里会死锁）。</summary>
    private static async Task<string?> ClipboardText(ValidationResultView dialog)
    {
        var clipboard = TopLevel.GetTopLevel(dialog)?.Clipboard;
        return clipboard is null ? null : await clipboard.TryGetTextAsync();   // Avalonia 12 的读法
    }

    /// <summary>等"剪贴板里出现期望文本"：`CopyAll` 是 fire-and-forget 的 `SetTextAsync` ⇒ 不能只 Pump 一次。</summary>
    private static async Task<string?> WaitForClipboard(ValidationResultView dialog, string expected,
                                                        int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string? current = null;
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            UiTestHost.Pump(2);
            current = await ClipboardText(dialog);
            if (string.Equals(current, expected, StringComparison.Ordinal)) return current;
            await Task.Delay(20);
        }

        return current;
    }

    [Fact]
    public void Face_MatchesTheDeclaredTable()
    {
        UiTestHost.Run(() =>
        {
            var dialog = Open(Findings());
            try
            {
                var actual = dialog.GetLogicalDescendants().OfType<Button>()
                    .Where(b => b.GetType() == typeof(Button))
                    .Where(b => b.Name is not null)
                    .Select(b => b.Name!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();

                Assert.Equal(new[] { "BtnClose", "BtnCopyAll", "BtnTitleClose" }, actual);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public async Task CopyAll_PutsTheComposedTextOnTheClipboard_AndWritesTheHint()
    {
        var findings = Findings();
        var expected = ExpectedCopyText(findings);

        await UiTestHost.RunAsync(async () =>
        {
            var dialog = Open(findings);
            try
            {
                // 自检：点击前提示行**不是**"已复制"（它是 Build 时写的静态提示）
                var copied = AppLocalization.Service[Lang.FindingCopied];
                Assert.NotEqual(copied, HintText(dialog));

                Click(dialog, "BtnCopyAll");

                // ① 提示行被改成"已复制"（字典文案，不冻字符串）
                Assert.Equal(copied, HintText(dialog));

                // ② 剪贴板真的拿到那段文本（产品交给平台的那一份）
                Assert.NotNull(TopLevel.GetTopLevel(dialog)?.Clipboard);
                var onClipboard = await WaitForClipboard(dialog, expected);
                Assert.Equal(expected, onClipboard);

                JudgeReadings.Write(_out, "验证结果 · 复制全部（#39 残余面 · 阶段 255）", new[]
                {
                    "面：具名 Button 3 个 —— BtnCopyAll / BtnClose / BtnTitleClose（双向差集，已断言）",
                    $"提示行：`{HintText(dialog)}`（字典键 {Lang.FindingCopied}）",
                    $"剪贴板文本 {onClipboard?.Length ?? 0} 字符、{expected.Split(Environment.NewLine).Length} 行："
                        + $"`{onClipboard?.Replace(Environment.NewLine, " ⏎ ")}`",
                });
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Close_ClosesTheDialog()
    {
        UiTestHost.Run(() =>
        {
            var dialog = Open(Findings());
            var closed = 0;
            dialog.Closed += (_, _) => closed++;
            try
            {
                Click(dialog, "BtnClose");
                UiTestHost.Pump(3);

                Assert.Equal(1, closed);
                Assert.False(dialog.IsVisible);
            }
            finally { dialog.Close(); }
        });
    }
}
