using System;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Services;

/// <summary>
/// **搁置审计的接线**（阶段 278；契约 `docs/2026-10-01-alarm-shelving-contract.md` §7）：
/// 把 <see cref="AlarmEngine.ShelveAudited"/> 事件接到 <see cref="AuthenticationService.AppendAudit"/>。
///
/// <para><b>为什么单独一个类</b>：① 引擎在 Core、审计写在 AuthService ⇒ 接线属于 App 层；
/// ② 抽出来才能**不构造 MainWindow 就判它**（MainWindow 构造约 928 ms，且会把一大堆服务拉起来）。</para>
///
/// <para>三条都留痕（契约 §5/§7）：**成功** · **被拒（含未授权）** · **解除**（含**到期自动解除**）。
/// 去掉任何一条，事后都看不出"谁把报警静音了""报警为什么突然又开始响"。</para>
/// </summary>
public static class AlarmAuditWiring
{
    /// <summary>订阅引擎的搁置审计事件并写进审计（返回取消订阅的句柄，便于宿主释放）。</summary>
    public static IDisposable Attach(AlarmEngine engine, AuthenticationService auth)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(auth);

        void OnAudit(object? _, AlarmShelveAudit e)
        {
            var action = e.Shelved ? AuditAction.AlarmShelved : AuditAction.AlarmUnshelved;
            var detail = BuildDetail(e);
            // ⚠ 未授权时 `By` 可能为空 ⇒ 用空串（审计表里"谁"这一列宁可空，也不许写"未知"这种假值）
            auth.AppendAudit(action, userId: e.By ?? "", username: e.By ?? "", detail: detail, success: e.Granted);
        }

        engine.ShelveAudited += OnAudit;

        // 抑制（阶段 280）：与搁置同一条审计通路 —— 抑制期间"不产生记录"，事后必须查得出是谁按的
        void OnSuppress(object? _, AlarmSuppressAudit e)
        {
            var action = e.Suppressed ? AuditAction.AlarmSuppressed : AuditAction.AlarmUnsuppressed;
            var detail = BuildSuppressDetail(e);
            auth.AppendAudit(action, userId: e.By ?? "", username: e.By ?? "", detail: detail, success: e.Granted);
        }

        engine.SuppressionAudited += OnSuppress;

        return new Subscription(() =>
        {
            engine.ShelveAudited -= OnAudit;
            engine.SuppressionAudited -= OnSuppress;
        });
    }

    /// <summary>明细串（<c>报警名 · 结果 · 原因/到期</c>）—— 审计表里要能一眼看懂。</summary>
    internal static string BuildDetail(AlarmShelveAudit e)
    {
        var what = e.Shelved ? "shelve" : "unshelve";
        var result = e.Granted ? (e.Shelved ? "granted" : e.Reason ?? "unshelve") : $"denied:{e.IssueCode}";
        var until = e.Until is { } t ? $" until={t:O}" : "";
        var reason = string.IsNullOrWhiteSpace(e.Reason) ? "" : $" reason={e.Reason}";
        return $"{e.AlarmName} · {what} · {result}{until}{reason}";
    }

    /// <summary>抑制的明细串（<c>报警名 · suppress/unsuppress · 结果/原因</c>）。</summary>
    internal static string BuildSuppressDetail(AlarmSuppressAudit e)
    {
        var what = e.Suppressed ? "suppress" : "unsuppress";
        var result = e.Granted ? (e.Reason is { Length: > 0 } r ? $"granted reason={r}" : "granted") : $"denied:{e.IssueCode}";
        return $"{e.AlarmName} · {what} · {result}";
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
        }
    }
}
