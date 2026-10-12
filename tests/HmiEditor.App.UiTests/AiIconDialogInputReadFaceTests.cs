using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.AI.Generators;
using HmiEditor.AI.Inference;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`AiIconDialog` 那 4 个输入框的读点行为面**（阶段 209）—— 「读点在动作处理器」这一族的第三个视图。
///
/// <para><b>读点</b>：`InputBox`（批量图标的条目列表，`GenerateIconsAsync`）·
/// `ImageInputBox`（单图提示词）· `WidthBox` / `HeightBox`（画布尺寸）——
/// 后三者都在 `GenerateImageAsync` 里。</para>
///
/// <para><b>为什么以前测不了</b>：对话框原先直接吃 `AiPreviewManager`（**sealed** 且自建 provider）⇒
/// 真正的读点在 `ImageGenerator.GenerateAsync(prompt, w, h, ct)` 与 `BatchIconGenerator` 的**实参**里，
/// 没有缝就只能测"未配置 / 空输入"两条早退分支，而跑真生成要走真 LLM。
/// 阶段 209 加了 <c>IIconGenerationSource</c> 这条缝（默认实现逐字搬用原逻辑）⇒ 假源即可。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class AiIconDialogInputReadFaceTests
{
    /// <summary>假生成源：记下四条读取点的**实参**，返回预定的 SVG。</summary>
    private sealed class FakeSource : IIconGenerationSource
    {
        public bool IsConfigured { get; set; } = true;
        public bool HasIconGenerator { get; set; } = true;
        public bool HasImageGenerator { get; set; } = true;

        public string? ImagePrompt { get; private set; }
        public (int Width, int Height)? ImageSize { get; private set; }
        public IReadOnlyList<(string Name, string Description)>? IconItems { get; private set; }
        public IconStyleVariant? FixedVariant { get; private set; }

        public Task<ImageGenerator.ImageGenerationResult> GenerateImageAsync(
            string prompt, int width, int height, CancellationToken ct)
        {
            ImagePrompt = prompt;
            ImageSize = (width, height);
            return Task.FromResult(new ImageGenerator.ImageGenerationResult(
                "<svg id=\"fake\"/>", width, height, 3, 11, 22));
        }

        public Task<IReadOnlyList<BatchIconResult>> GenerateIconsAsync(
            IReadOnlyList<(string Name, string Description)> items,
            IconStyleVariant? fixedVariant,
            CancellationToken ct)
        {
            IconItems = items;
            FixedVariant = fixedVariant;
            return Task.FromResult<IReadOnlyList<BatchIconResult>>(
                items.Select(i => new BatchIconResult(i.Name, "<svg/>", 2, true, null)).ToArray());
        }
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

    private static void Click(Control root, string name)
    {
        ControlWiringFace.Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump(2);
    }

    /// <summary>造对话框：`AiPreviewManager` 只用来满足构造（生成全走假源）。</summary>
    private static AiIconDialog Open(FakeSource source, string? assetDirectory)
    {
        using var manager = new AiPreviewManager(new LlmProviderConfig { Provider = "openai", ApiKey = "test", Model = "m" });
        var dialog = new AiIconDialog(manager, source) { AssetDirectory = assetDirectory };
        dialog.Show();
        UiTestHost.Pump(5);
        return dialog;
    }

    [Fact]
    public async Task ImageInputs_ReachTheGenerationSource()
    {
        using var sandbox = new DataRootSandbox("aiicondialog-image-inputs");
        Directory.CreateDirectory(sandbox.DataRoot);

        await UiTestHost.RunAsync(async () =>
        {
            var source = new FakeSource();
            var dialog = Open(source, sandbox.DataRoot);
            try
            {
                // 切到图片模式（ToggleButton 的 Click 处理器 → `_imageMode = true`）
                Click(dialog, "ImageModeBtn");

                ControlWiringFace.Write(dialog, "ImageInputBox", "  一个蓝色水泵图标  ");
                ControlWiringFace.WriteNumber(dialog, "WidthBox", 320);
                ControlWiringFace.WriteNumber(dialog, "HeightBox", 240);

                Click(dialog, "GenerateBtn");
                await UntilAsync(() => source.ImageSize is not null, "假源收到单图生成请求");

                // 三格都真的被读：提示词（裁剪后）＋ 两个尺寸
                Assert.Equal("一个蓝色水泵图标", source.ImagePrompt);
                Assert.Equal((320, 240), source.ImageSize);

                // 结果回流：对话框状态行里有"已生成"的量词（用同一来源的键值，不冻文案）
                await UntilAsync(
                    () => (ControlWiringFace.Find<TextBlock>(dialog, "ResultText").Text ?? "").Length > 0,
                    "结果文本被写上");
                Assert.Contains("11", ControlWiringFace.Find<TextBlock>(dialog, "ResultText").Text ?? "");   // Token: 11 in / 22 out
            }
            finally
            {
                dialog.Close();
                UiTestHost.Pump();
            }
        });
    }

    [Fact]
    public async Task IconInput_ReachesTheGenerationSource()
    {
        using var sandbox = new DataRootSandbox("aiicondialog-icon-inputs");
        Directory.CreateDirectory(sandbox.DataRoot);

        await UiTestHost.RunAsync(async () =>
        {
            var source = new FakeSource();
            var dialog = Open(source, sandbox.DataRoot);
            try
            {
                // 批量模式：条目列表形如「名字,描述」每行一项
                ControlWiringFace.Write(dialog, "InputBox", "泵: 蓝色水泵\n阀: 红色阀门");

                Click(dialog, "GenerateBtn");
                await UntilAsync(() => source.IconItems is not null, "假源收到批量生成请求");

                Assert.Equal(2, source.IconItems!.Count);
                Assert.Equal("泵", source.IconItems[0].Name);
                Assert.Equal("阀", source.IconItems[1].Name);

                // 变体下拉未选 ⇒ 自动多样（null）；选了固定变体 ⇒ 传下去
                Assert.Null(source.FixedVariant);
                await UntilAsync(() => source.IconItems is not null, "（幂等等待，避免竞态）");
            }
            finally
            {
                dialog.Close();
                UiTestHost.Pump();
            }
        });
    }

    [Fact]
    public async Task EmptyInputs_DoNotReachTheSource()
    {
        using var sandbox = new DataRootSandbox("aiicondialog-empty-inputs");
        Directory.CreateDirectory(sandbox.DataRoot);

        await UiTestHost.RunAsync(async () =>
        {
            var source = new FakeSource();
            var dialog = Open(source, sandbox.DataRoot);
            try
            {
                // 空提示词 ⇒ 早退（`input` 为空时 `GenerateImageAsync` 直接 return）
                Click(dialog, "ImageModeBtn");
                ControlWiringFace.Write(dialog, "ImageInputBox", "   ");
                Click(dialog, "GenerateBtn");
                await Task.Delay(80);
                Assert.Null(source.ImageSize);

                // 未配置 ⇒ 早退（并且状态行给出提示）
                source.IsConfigured = false;
                ControlWiringFace.Write(dialog, "ImageInputBox", "有内容但未配置");
                Click(dialog, "GenerateBtn");
                await UntilAsync(
                    () => (ControlWiringFace.Find<TextBlock>(dialog, "StatusText").Text ?? "").Length > 0,
                    "未配置时状态行有提示");
                Assert.Null(source.ImageSize);
            }
            finally
            {
                dialog.Close();
                UiTestHost.Pump();
            }
        });
    }
}
