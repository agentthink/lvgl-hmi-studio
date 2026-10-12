using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HmiEditor.AI.Inference;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`AiIconDialog` 的按钮面**（阶段 253；登记 `#39` 残余面清单里最大的一块 —— 残余 7 / 面 8）。
///
/// <para><b>为什么单独立判据：</b>它是被**停靠面板**的 `AssetManagerView.BtnAiIcon` 打开的对话框
/// （第四层只把那个入口记成 `Popup`、判"弹了没有"，**不判里面**）；而第三层
/// （`DialogInnerInvocationUiTests`）的宿主定义是"**从主菜单打开的窗口**" ⇒ 它也进不去
/// （与 `TableCellEditorDialog` 同形，见 §166.1/§168.1）。⇒ 走**生产构造路径 ＋ `Show()`** 的专题判据。</para>
///
/// <para><b>8 个具名动作控件与桶</b>（面闭合见 <see cref="Face_MatchesTheDeclaredTable"/>）：
/// <list type="bullet">
///   <item><b>`ImageModeBtn` / `IconModeBtn`</b>（ToggleButton，XAML 属性接线）：切模式 —— 面板可见性 ＋
///     两个 Toggle 的选中态 ＋ 窗口标题 ＋ 清空状态行。⚠ **顺序是语义**：初始 `_imageMode == false`，
///     所以先点 `IconModeBtn` 是 `SwitchMode(false)` 的**提前 return**（无变化）；先点 `ImageModeBtn` 才切得动。</item>
///   <item><b>`CancelBtn`</b>（ctor `Click +=`）：非 busy 时 `Close(Results)` ⇒ 关窗（本判据放在最后）。</item>
///   <item><b>`SaveSvgBtn` / `SavePngBtn` / `AddToLibraryBtn`</b>：首行都判 `_pendingImageSvg`（或 `_busy`）
///     ⇒ 无图片结果时**提前 return**；真保存还走 `SaveFilePickerAsync`（平台桩）。本判据如实记 `Inert`。</item>
///   <item><b>`GenerateBtn`</b>：**不点** —— 它的效果（两种模式各读了哪些格、结果回流到状态行）
///     已由 `AiIconDialogInputReadFaceTests` 用**假生成源**判过（`IIconGenerationSource` 那条注入缝，
///     阶段 209）；本判据点它只会再走一遍真 provider 的早退分支，属重复记账。</item>
///   <item><b>`BtnTitleClose`</b>：`ChromeWindow` 标题栏的关闭按钮 ⇒ 与 `CancelBtn` 同一后果（关窗），
///     本判据不点（留一条），理由写在声明表里。</item>
/// </list></para>
///
/// <para><b>不抓什么：</b>① 不判真实命中测试；② 不判生成质量/网络；③ 不判保存路径的落盘（平台桩）；
/// ④ 不重复判输入框的读取点（那有专门的判据）。</para>
/// </summary>
[Collection("Language-global")]
public class AiIconDialogButtonUiTests
{
    private readonly ITestOutputHelper _out;

    public AiIconDialogButtonUiTests(ITestOutputHelper o) => _out = o;

    private enum Bucket
    {
        /// <summary>点下去会改声明的分量。</summary>
        Moves,

        /// <summary>点下去本判据的读数一字不变（提前 return 档）。</summary>
        Inert,

        /// <summary>不点：理由必填（≥10 字符）。</summary>
        Never,
    }

    private sealed record Decl(string Name, Bucket Bucket, string Why);

    /// <summary>面 ＋ 桶 ＋ 理由（与第四/五层的表同一口径：没被分类的具名按钮要被点名）。</summary>
    private static readonly Decl[] Declared =
    {
        new("ImageModeBtn", Bucket.Moves,
            "XAML `Click=\"OnImageModeClicked\"` ⇒ `SwitchMode(true)`：切到图片模式"),
        new("IconModeBtn", Bucket.Moves,
            "XAML `Click=\"OnIconModeClicked\"` ⇒ `SwitchMode(false)`：切回图标模式（⚠ 必须排在 ImageModeBtn 之后，"
            + "否则初始就是 icon 模式、命中 `if (_imageMode == imageMode) return;`）"),
        new("CancelBtn", Bucket.Moves,
            "ctor `CancelBtn.Click += OnCancelClicked`：非 busy ⇒ `Close(Results)` ⇒ 关窗（放在最后一条）"),
        new("SaveSvgBtn", Bucket.Inert,
            "`OnSaveSvgClicked` 首行 `if (string.IsNullOrEmpty(_pendingImageSvg)) return;` ⇒ 无图片结果时不动作；"
            + "真保存走 `SaveFilePickerAsync`（平台桩）"),
        new("SavePngBtn", Bucket.Inert,
            "`OnSavePngClicked` 同形：先判 `_pendingImageSvg`，再走 `SaveFilePickerAsync`"),
        new("AddToLibraryBtn", Bucket.Inert,
            "`OnAddToLibraryClicked` 首行 `if (string.IsNullOrEmpty(_pendingImageSvg) || _busy) return;`"),
        new("GenerateBtn", Bucket.Never,
            "它的效果已由 `AiIconDialogInputReadFaceTests` 用假生成源判过（读格实参 ＋ 结果回流状态行）；"
            + "本判据点它只会再走真 provider 的早退分支 ⇒ 重复记账"),
        new("BtnTitleClose", Bucket.Never,
            "`ChromeWindow` 标题栏的关闭按钮 ⇒ 与 `CancelBtn` 同一后果（关窗）；本判据保留 `CancelBtn` 那一条"),
    };

    private static AiIconDialog Open()
    {
        using var manager = new AiPreviewManager(new LlmProviderConfig { Provider = "openai", ApiKey = "test", Model = "m" });
        var dialog = new AiIconDialog(manager);
        dialog.Show();
        UiTestHost.Pump(5);
        return dialog;
    }

    private static void Click(Control root, string name)
    {
        ControlWiringFace.Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump(3);
    }

    private static bool Visible(Control root, string name) => ControlWiringFace.Find<Control>(root, name).IsVisible;
    private static string TextOf(Control root, string name) => ControlWiringFace.Find<TextBlock>(root, name).Text ?? "";

    [Fact]
    public void Face_MatchesTheDeclaredTable()
    {
        UiTestHost.Run(() =>
        {
            var dialog = Open();
            try
            {
                var actual = dialog.GetLogicalDescendants().OfType<Button>()
                    .Where(b => b.GetType() == typeof(Button) || b is ToggleButton)
                    .Where(b => b.Name is not null)
                    .Select(b => b.Name!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();

                var declared = Declared.Select(d => d.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
                Assert.Equal(declared, actual);   // 双向差集：少一个 / 多一个都红

                // 表卫生：Never 必须写理由；至少两条会被真点（否则这张面等于没判）
                Assert.All(Declared.Where(d => d.Bucket == Bucket.Never),
                    d => Assert.True(d.Why.Length >= 10, $"{d.Name} 记成 Never 但理由太短"));
                Assert.True(Declared.Count(d => d.Bucket != Bucket.Never) >= 2,
                    "声明表里真点的按钮少于 2 条 —— 这张面等于没判");
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void ModeToggles_SwitchPanelsTogglesAndTitle()
    {
        UiTestHost.Run(() =>
        {
            var dialog = Open();
            try
            {
                // 初态：图标模式（IconPanel 可见、IconModeBtn 选中）
                Assert.True(Visible(dialog, "IconPanel"));
                Assert.False(Visible(dialog, "ImagePanel"));
                Assert.True(ControlWiringFace.Find<ToggleButton>(dialog, "IconModeBtn").IsChecked == true);

                // ① 先点 IconModeBtn = 提前 return（初始就是图标模式）⇒ 一字不变
                Click(dialog, "IconModeBtn");
                Assert.True(Visible(dialog, "IconPanel"));
                Assert.False(Visible(dialog, "ImagePanel"));

                // ② 点 ImageModeBtn ⇒ 切到图片模式
                Click(dialog, "ImageModeBtn");
                Assert.False(Visible(dialog, "IconPanel"));
                Assert.True(Visible(dialog, "ImagePanel"));
                Assert.True(ControlWiringFace.Find<ToggleButton>(dialog, "ImageModeBtn").IsChecked == true);
                Assert.False(ControlWiringFace.Find<ToggleButton>(dialog, "IconModeBtn").IsChecked == true);
                Assert.Equal(AppLocalization.Service[Lang.WinAiImage], dialog.Title);
                Assert.Equal(AppLocalization.Service[Lang.WinAiImage], TextOf(dialog, "TitleText"));
                Assert.Equal("", TextOf(dialog, "StatusText"));       // 切模式清空状态行

                // 记下**图片模式那一瞬**的读数（下面还要切回去，别在读数里写成"最后的状态"）
                var imagePanelVisible = Visible(dialog, "ImagePanel");
                var iconPanelVisible = Visible(dialog, "IconPanel");
                var imageTitle = dialog.Title;

                // ③ 点回 IconModeBtn ⇒ 切回图标模式
                Click(dialog, "IconModeBtn");
                Assert.True(Visible(dialog, "IconPanel"));
                Assert.False(Visible(dialog, "ImagePanel"));
                Assert.Equal(AppLocalization.Service[Lang.WinAiIcon], dialog.Title);

                JudgeReadings.Write(_out, "AI 图标对话框 · 模式切换（#39 残余面 · 阶段 253）", new[]
                {
                    "面：具名动作控件 8 个（Button 族 ＋ ToggleButton）—— 声明表见本类 `Declared`（双向差集，已断言）",
                    $"切到图片模式后：IconPanel 可见={iconPanelVisible} / ImagePanel 可见={imagePanelVisible}；"
                        + $"Title=`{imageTitle}`",
                    "点 IconModeBtn（初始态）= 提前 return（一字不变）；点 ImageModeBtn / 再点 IconModeBtn = 两次真切换",
                });
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void SaveAndLibraryButtons_AreNoOpsWithoutAPendingImage()
    {
        UiTestHost.Run(() =>
        {
            var dialog = Open();
            try
            {
                var statusBefore = TextOf(dialog, "StatusText");

                // 三个按钮的首行都判 `_pendingImageSvg`（本判据没有图片结果）⇒ 提前 return
                Click(dialog, "SaveSvgBtn");
                Click(dialog, "SavePngBtn");
                Click(dialog, "AddToLibraryBtn");

                Assert.True(dialog.IsVisible, "这三个按钮都不该关窗");
                Assert.Empty(dialog.Results);
                Assert.Equal(statusBefore, TextOf(dialog, "StatusText"));
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void CancelButton_ClosesTheDialogWithNoResults()
    {
        UiTestHost.Run(() =>
        {
            var dialog = Open();
            var closed = 0;
            dialog.Closed += (_, _) => closed++;
            try
            {
                Click(dialog, "CancelBtn");
                UiTestHost.Pump(3);

                Assert.Equal(1, closed);                  // `Close(Results)` 真的调了
                Assert.False(dialog.IsVisible);
                Assert.Empty(dialog.Results);             // 没生成过 ⇒ 结果为空

                JudgeReadings.Write(_out, "AI 图标对话框 · 取消（#39 残余面 · 阶段 253）", new[]
                {
                    "面：具名动作控件 8 个 —— CancelBtn 是 3 条会被真点的声明之一（另两条是两个模式 Toggle）",
                    $"点它之后：Closed 触发 {closed} 次；IsVisible={dialog.IsVisible}；Results {dialog.Results.Count} 条",
                    "另 3 条 Inert（SaveSvg/SavePng/AddToLibrary 无图片结果时提前 return）＋ 2 条 Never（GenerateBtn 由输入读点判据判过、BtnTitleClose 同 Cancel 后果）",
                });
            }
            finally { dialog.Close(); }
        });
    }
}
