using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Controls;

/// <summary>
/// AI 生成对话框公共基类：统一"输入 → 生成 → 校验失败展示"流程、busy 状态、状态栏着色与**取消**。
/// 子类需提供命名控件（AiInput/AiStatus/AiGenerate/AiCancel）并实现 <see cref="GenerateAsync"/>。
///
/// <para><b>取消契约（2026-09-15 补）</b>：此前取消按钮只做 <c>Close()</c> —— 窗体关掉，但在途请求
/// 继续跑到超时，完成后还会回写 <c>AiPreviewManager.Pending*</c>（可能盖掉更晚发起的请求结果），
/// 用户看到的是"取消了但没取消"。现在基类持有 CTS，规则如下：</para>
/// <list type="number">
///   <item><see cref="GenerateAsync"/> 的重写**必须**把传入的 token 原样传到 Manager，否则取消无效；</item>
///   <item>取消按钮在 busy 时 → <c>Cancel()</c>（**不关窗**，用户可改描述重试），非 busy 时才关窗；</item>
///   <item>窗体被关闭（含右上角关闭）→ 兜底取消在途请求。</item>
/// </list>
/// <para>取消与失败在 UI 上必须区分：取消走「已取消」文案，失败走「生成失败 + 原因」。</para>
/// </summary>
public abstract class AiGenerateDialogBase<TResult> : ChromeWindow
{
    private CancellationTokenSource? _cts;

    protected AiPreviewManager Manager { get; }
    protected bool Busy { get; private set; }

    /// <summary>生成结果(校验通过时非空)。</summary>
    public TResult? Result { get; protected set; }

    /// <summary>当前项目上下文(变量/主题), 由调用方注入。</summary>
    public HmiProject? CurrentProject { get; set; }

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    protected AiGenerateDialogBase()
    {
        Manager = null!;
    }

    protected AiGenerateDialogBase(AiPreviewManager manager)
    {
        Manager = manager;
    }

    // 命名控件访问（子类 XAML 中声明）
    protected abstract TextBox AiInput { get; }
    protected abstract TextBlock AiStatus { get; }
    protected abstract Button AiGenerate { get; }
    protected abstract Button AiCancel { get; }

    /// <summary>
    /// 子类在此调用对应的 AI 生成方法，并把 <paramref name="ct"/> 原样传到 <c>Manager</c>
    /// （漏传 = 取消按钮变成摆设，守卫 <c>AiCancellationWiringTests</c> 会拦住）。
    /// </summary>
    protected abstract Task<TResult?> GenerateAsync(string input, HmiProject project, CancellationToken ct);

    /// <summary>在途生成的取消源；busy 期间非空。子类只读（用于把 token 继续往下传的场景）。</summary>
    protected CancellationToken InFlightToken => _cts?.Token ?? CancellationToken.None;

    protected async void OnGenerateClicked(object? sender, RoutedEventArgs e)
    {
        var input = AiInput.Text?.Trim();
        if (string.IsNullOrEmpty(input) || Busy) return;

        if (!Manager.IsConfigured)
        {
            SetStatus(AppLocalization.Service["ai.not.configured"], HmiEditor.App.Theme.Warning);
            return;
        }

        if (CurrentProject is null)
        {
            SetStatus(AppLocalization.Service["gen.create.or.open.project"], HmiEditor.App.Theme.TextSecondary);
            return;
        }

        _cts?.Dispose();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var ct = cts.Token;

        SetBusy(true);
        SetStatus(AppLocalization.Service["ai.generating"], HmiEditor.App.Theme.TextSecondary);

        try
        {
            var result = await GenerateAsync(input, CurrentProject, ct);
            if (result is null)
            {
                var issues = Manager.PendingValidation?.Issues;
                var detail = issues is { Count: > 0 }
                    ? string.Join("\n", issues.Take(5).Select(i => "• " + ExternalText.Describe(i)))
                    : AppLocalization.Service["ai.retry.hint"];
                SetStatus(AppLocalization.Service.Format("ai.gen.failed", detail), HmiEditor.App.Theme.Danger);
                return;
            }

            Result = result;
            OnSuccess(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 用户主动取消：不是错误，不报"生成失败"（否则用户以为程序坏了）。
            SetStatus(AppLocalization.Service[Lang.AiCancelled], HmiEditor.App.Theme.TextSecondary);
        }
        catch (Exception ex)
        {
            SetStatus(AppLocalization.Service.Format("ai.gen.failed", ExternalText.FailureReason(ex)), HmiEditor.App.Theme.Danger);
        }
        finally
        {
            // 用局部变量收尾：关窗兜底取消会把字段清掉，靠字段释放会漏（CTS 未被 Dispose）。
            if (ReferenceEquals(_cts, cts)) _cts = null;
            cts.Dispose();
            SetBusy(false);
        }
    }

    /// <summary>
    /// 取消按钮的统一语义：busy 时取消在途请求（保留对话框），否则关闭并返回空结果。
    /// 子类在 <c>InitializeComponent()</c> 之后接上：<c>CancelBtn.Click += OnCancelClicked;</c>
    /// </summary>
    protected void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        if (Busy && _cts is { } cts)
        {
            cts.Cancel();
            return;
        }

        Close(null);
    }

    /// <summary>窗体关闭时兜底取消，避免"关了窗请求还在跑、跑完还回来写 Pending"。</summary>
    protected override void OnClosed(EventArgs e)
    {
        CancelInFlight();
        base.OnClosed(e);
    }

    /// <summary>
    /// 取消在途请求（幂等）。**不**在这里置空/释放字段 —— 释放统一由
    /// <see cref="OnGenerateClicked"/> 的 finally 用局部引用收尾，否则会漏 Dispose。
    /// </summary>
    protected void CancelInFlight()
    {
        if (_cts is not { } cts) return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 生成流程已自行收尾；无需处理。
        }
    }

    /// <summary>生成成功回调：默认关闭并携带结果，子类可覆盖（如图标批量逻辑）。</summary>
    protected virtual void OnSuccess(TResult result) => Close(result);

    protected void SetBusy(bool busy)
    {
        Busy = busy;
        AiGenerate.IsEnabled = !busy;
    }

    protected void SetStatus(string text, IBrush? brush = null)
    {
        AiStatus.Text = text;
        if (brush != null)
            AiStatus.Foreground = brush;
    }
}
