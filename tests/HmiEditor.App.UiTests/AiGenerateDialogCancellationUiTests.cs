using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HmiEditor.AI.Inference;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// AI 生成对话框的「取消」行为守卫（headless 真可视树 + 真基类逻辑）。
///
/// <para><b>为什么不能只靠静态扫描</b>：静态守卫能证明"token 传下去了"，
/// 但证明不了"取消按钮真的取消了那个 token、且不提前关窗、取消后 busy 会复位"。
/// 这三件事只有把真对话框跑起来才看得见。</para>
///
/// <para><b>收口前的缺陷形态</b>：取消按钮直接 <c>Close(null)</c> —— 窗体关掉、请求照跑，
/// 跑完还回写 <c>AiPreviewManager.Pending*</c>（可能盖掉更晚发起的请求结果）。</para>
/// </summary>
public class AiGenerateDialogCancellationUiTests
{
    /// <summary>
    /// 探针对话框：实现基类要求的最小控件；<see cref="GenerateAsync"/> 一直挂着直到被取消，
    /// 并记录收到的 token —— 断言"取消的是不是同一个 token"就靠它。
    /// </summary>
    private sealed class ProbeDialog : AiGenerateDialogBase<string>
    {
        private readonly TextBox _input = new();
        private readonly TextBlock _status = new();
        private readonly Button _generate = new();
        private readonly Button _cancel = new();

        public ProbeDialog(AiPreviewManager manager) : base(manager) =>
            Closed += (_, _) => ClosedFired = true;

        public List<CancellationToken> Tokens { get; } = new();
        public bool ClosedFired { get; private set; }
        public string StatusText => _status.Text ?? "";
        public bool IsBusy => Busy;
        public bool GenerateEnabled => _generate.IsEnabled;

        public void SetInput(string text) => _input.Text = text;

        public void ClickGenerate() => OnGenerateClicked(null, new RoutedEventArgs());

        public void ClickCancel() => OnCancelClicked(null, new RoutedEventArgs());

        /// <summary>模拟用户关窗（含右上角关闭），不依赖 headless 窗口是否 Show 过。</summary>
        public void SimulateClosed() => OnClosed(EventArgs.Empty);

        protected override TextBox AiInput => _input;
        protected override TextBlock AiStatus => _status;
        protected override Button AiGenerate => _generate;
        protected override Button AiCancel => _cancel;

        protected override async Task<string?> GenerateAsync(string input, HmiProject project, CancellationToken ct)
        {
            Tokens.Add(ct);
            await Task.Delay(Timeout.Infinite, ct);
            return null; // 不可达：取消后抛 OCE
        }
    }

    private static AiPreviewManager CreateManager() => new(new LlmProviderConfig
    {
        Provider = "openai",
        ApiKey = "test",
        Model = "m",
    });

    private static ProbeDialog CreateDialog(AiPreviewManager manager)
    {
        var dialog = new ProbeDialog(manager) { CurrentProject = new HmiProject() };
        dialog.SetInput("生成一屏反应釜监控画面");
        return dialog;
    }

    /// <summary>把取消引发的那串异步续体（catch → finally）跑到落定。</summary>
    private static void DrainContinuations()
    {
        for (var i = 0; i < 8; i++)
        {
            UiTestHost.Pump(1);
            UiTestHost.UIThread.RunJobs();
        }
    }

    [Fact]
    public void CancelWhileGenerating_CancelsTheTokenTheRequestIsUsing()
    {
        UiTestHost.Run(() =>
        {
            using var manager = CreateManager();
            var dialog = CreateDialog(manager);

            dialog.ClickGenerate();

            // 自检：生成真的起来了，并且我们拿到了它正在用的那个 token。
            Assert.Single(dialog.Tokens);
            Assert.True(dialog.IsBusy, "点了生成之后应处于 busy");
            Assert.False(dialog.Tokens[0].IsCancellationRequested);

            dialog.ClickCancel();

            Assert.True(dialog.Tokens[0].IsCancellationRequested,
                "取消按钮必须取消**在途请求正在用的那个 token**（此前只 Close，请求照跑）");
        });
    }

    [Fact]
    public void CancelWhileGenerating_KeepsTheDialogOpen()
    {
        UiTestHost.Run(() =>
        {
            using var manager = CreateManager();
            var dialog = CreateDialog(manager);

            dialog.ClickGenerate();
            dialog.ClickCancel();

            // busy 时取消**不关窗**：用户要能看着"已取消"改描述重试。
            // （关窗是另一条语义，由 CancelWhenIdle 那类路径负责。）
            Assert.False(dialog.ClosedFired, "busy 时取消不应关窗");
        });
    }

    [Fact]
    public void CancelWhileGenerating_ShowsCancelled_NotGenerationFailed()
    {
        UiTestHost.Run(() =>
        {
            using var manager = CreateManager();
            var dialog = CreateDialog(manager);

            dialog.ClickGenerate();
            dialog.ClickCancel();
            DrainContinuations();

            // 取消是预期结果：文案必须是「已取消」，且不能把取消当失败（否则用户以为程序坏了）。
            Assert.Equal(AppLocalization.Service[Lang.AiCancelled], dialog.StatusText);
            Assert.DoesNotContain("生成失败", dialog.StatusText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void CancelledRun_EndsBusyState_SoTheUserCanRetry()
    {
        UiTestHost.Run(() =>
        {
            using var manager = CreateManager();
            var dialog = CreateDialog(manager);

            dialog.ClickGenerate();
            dialog.ClickCancel();
            DrainContinuations();

            // 取消后必须收尾：busy 复位、生成按钮重新可点、CTS 已释放（否则后续运行会漏 Dispose）。
            Assert.False(dialog.IsBusy, "取消后 busy 必须复位");
            Assert.True(dialog.GenerateEnabled, "取消后应能改内容重新生成");
        });
    }

    [Fact]
    public void ClosingTheDialogWhileGenerating_CancelsInFlightGeneration()
    {
        UiTestHost.Run(() =>
        {
            using var manager = CreateManager();
            var dialog = CreateDialog(manager);

            dialog.ClickGenerate();
            Assert.Single(dialog.Tokens);

            dialog.SimulateClosed();

            Assert.True(dialog.Tokens[0].IsCancellationRequested,
                "关窗必须兜底取消：否则请求会继续跑到超时，回来还会写 Pending*");
        });
    }
}
