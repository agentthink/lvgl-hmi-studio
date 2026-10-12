using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HmiEditor.App.Services;
using HmiEditor.Core.Diagnostics;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 本测试类会临时替换全局 <see cref="AppLog.Current"/>，因此禁止与其他测试并行，
/// 否则别的测试产生的日志会串进断言。
/// </summary>
[CollectionDefinition("AppLog-global", DisableParallelization = true)]
public sealed class AppLogGlobalCollection;

/// <summary>
/// 行为测试：<see cref="UiOperationGuard"/> —— 事件处理器异步体的统一兜底。
///
/// 关注点不是"能不能跑通"，而是四条安全性质：
/// ① 异常绝不再抛给调用方（否则 async void 的兜底路径又会被触发）；
/// ② 失败必定落盘日志（可运维）；
/// ③ 失败必定回调界面（用户可见，避免"点了没反应"）；
/// ④ 用户取消不算失败（不误报）。
/// </summary>
[Collection("AppLog-global")]
public class UiOperationGuardTests
{
    /// <summary>记录日志的测试替身，避免测试写真实日志文件。</summary>
    private sealed class RecordingLogger : IAppLogger
    {
        public List<string> Errors { get; } = new();
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message, Exception? exception = null) { }
        public void Error(string message, Exception? exception = null) => Errors.Add(message);
    }

    private static async Task<T> WithLogger<T>(Func<RecordingLogger, Task<T>> body)
    {
        var previous = AppLog.Current;
        var previousSink = UiOperationGuard.UiSink;
        var logger = new RecordingLogger();
        AppLog.Current = logger;
        try
        {
            return await body(logger);
        }
        finally
        {
            AppLog.Current = previous;
            UiOperationGuard.UiSink = previousSink;
        }
    }

    [Fact]
    public async Task ActionThrowing_DoesNotPropagate_AndReportsBothLogAndUi()
    {
        await WithLogger(async logger =>
        {
            var shown = new List<string>();

            // 不抛异常即为通过；若抛出，本测试会失败而不是静默。
            await UiOperationGuard.RunAsync(
                "删除用户",
                () => throw new InvalidOperationException("磁盘只读"),
                shown.Add);

            Assert.Single(logger.Errors);
            Assert.Contains("删除用户失败", logger.Errors[0]);
            Assert.Single(shown);
            Assert.Contains("删除用户失败", shown[0]);
            Assert.Contains("磁盘只读", shown[0]);
            return 0;
        });
    }

    [Fact]
    public async Task ActionThrowingInsideAsyncBody_IsCaughtAfterAwait()
    {
        await WithLogger(async logger =>
        {
            var shown = new List<string>();

            await UiOperationGuard.RunAsync("导入资源", async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("选择器不可用");
            }, shown.Add);

            Assert.Single(logger.Errors);
            Assert.Single(shown);
            Assert.Contains("选择器不可用", shown[0]);
            return 0;
        });
    }

    [Fact]
    public async Task Success_ReportsNothing()
    {
        await WithLogger(async logger =>
        {
            var shown = new List<string>();
            var ran = false;

            await UiOperationGuard.RunAsync("重命名画面", () =>
            {
                ran = true;
                return Task.CompletedTask;
            }, shown.Add);

            Assert.True(ran);
            Assert.Empty(logger.Errors);
            Assert.Empty(shown);
            return 0;
        });
    }

    [Fact]
    public async Task Cancellation_IsSilent()
    {
        await WithLogger(async logger =>
        {
            var shown = new List<string>();

            await UiOperationGuard.RunAsync("下载到 HMI", () => throw new OperationCanceledException(), shown.Add);

            Assert.Empty(logger.Errors);
            Assert.Empty(shown);
            return 0;
        });
    }

    [Fact]
    public async Task UiReportCallbackThrowing_IsSwallowedAndLogged()
    {
        await WithLogger(async logger =>
        {
            // 状态栏/日志面板在窗口关闭时可能不可用，此时不能让提示本身再次炸掉。
            await UiOperationGuard.RunAsync(
                "编译工程",
                () => throw new InvalidOperationException("工具链缺失"),
                _ => throw new InvalidOperationException("状态栏已销毁"));

            Assert.Equal(2, logger.Errors.Count);
            Assert.Contains("编译工程失败", logger.Errors[0]);
            Assert.Contains("编译工程失败提示未能显示", logger.Errors[1]);
            return 0;
        });
    }

    [Fact]
    public async Task NullUiCallback_StillLogsFailure()
    {
        await WithLogger(async logger =>
        {
            // 没有安装全局出口（无 UI 宿主）时，至少保证落盘。
            UiOperationGuard.UiSink = null;
            await UiOperationGuard.RunAsync("运行 Agent", () => throw new InvalidOperationException("网络超时"), null);

            Assert.Single(logger.Errors);
            Assert.Contains("运行 Agent失败", logger.Errors[0]);
            return 0;
        });
    }

    [Fact]
    public async Task GlobalSink_IsUsedWhenViewHasNoStatusLine()
    {
        await WithLogger(async logger =>
        {
            // 模拟 Shell 安装的全局状态栏出口（属性面板/图元库等视图依赖它）。
            var shown = new List<string>();
            UiOperationGuard.UiSink = shown.Add;

            await UiOperationGuard.RunAsync("删除模板", () => throw new InvalidOperationException("模板被占用"), null);

            Assert.Single(logger.Errors);
            Assert.Single(shown);
            Assert.Contains("删除模板失败: 模板被占用", shown[0]);
            return 0;
        });
    }

    [Fact]
    public async Task ViewLocalStatusLine_TakesPrecedenceOverGlobalSink()
    {
        await WithLogger(async _ =>
        {
            var global = new List<string>();
            var local = new List<string>();
            UiOperationGuard.UiSink = global.Add;

            await UiOperationGuard.RunAsync("选择文件", () => throw new InvalidOperationException("选择器不可用"), local.Add);

            Assert.Single(local);
            Assert.Empty(global); // 有本地状态行时不再打扰主状态栏
            return 0;
        });
    }

    [Fact]
    public async Task GlobalSinkThrowing_IsSwallowedAndLogged()
    {
        await WithLogger(async logger =>
        {
            // 主窗口关闭过程中状态栏可能已销毁，此时不能让提示本身再次炸掉。
            UiOperationGuard.UiSink = _ => throw new InvalidOperationException("状态栏已销毁");

            await UiOperationGuard.RunAsync("删除画面", () => throw new InvalidOperationException("项目只读"), null);

            Assert.Equal(2, logger.Errors.Count);
            Assert.Contains("删除画面失败提示未能显示", logger.Errors[1]);
            return 0;
        });
    }

    [Fact]
    public async Task Run_IsFireAndForget_AndNeverObservesUnhandledException()
    {
        await WithLogger(async logger =>
        {
            var shown = new List<string>();
            var gate = new TaskCompletionSource();

            // Run 立即返回（不等待 action），只把结果导向日志/界面。
            UiOperationGuard.Run("导出代码", async () =>
            {
                await gate.Task;
                throw new InvalidOperationException("磁盘已满");
            }, shown.Add);

            Assert.Empty(shown); // 异步体尚未完成
            gate.SetResult();
            for (var i = 0; i < 100 && shown.Count == 0; i++)
                await Task.Delay(10);

            Assert.Single(shown);
            Assert.Single(logger.Errors);
            return 0;
        });
    }
}
