using HmiEditor.App.Resources;
using HmiEditor.Core.Diagnostics;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Services;

/// <summary>
/// UI 事件处理器里「异步操作」的统一兜底。
///
/// <para><b>为什么需要它：</b>UI 事件处理器的签名必须是 <c>void</c>，所以异步处理器
/// 只能写成 <c>async void</c>。而 <c>async void</c> 的异常无法被调用方捕获，只会抛给
/// 同步上下文 —— 全局 Dispatcher 处理器能保证进程不崩，但用户看到的是「点了没反应」。
/// 本类把这类操作统一收口：失败必定 <b>落盘日志 + 回调界面反馈</b>。</para>
///
/// <para><b>推荐写法：</b>处理器保持同步 <c>void</c>，把异步体交给
/// <see cref="Run(string, Func{Task}, Action{string})"/>：</para>
/// <code>
/// private void OnDeleteUser(object? sender, RoutedEventArgs e) =>
///     UiOperationGuard.Run(Lang.UiOpDeleteUser,
///         async () => { ... await AppDialogs.ConfirmAsync(...); ... },
///         ShowStatus);
/// </code>
///
/// <para>与 <c>Program</c> 中的全局 Dispatcher 处理器是互补关系：全局处理器是最后一道
/// 防线（防崩溃），本类是第一道（保证用户可见）。</para>
/// </summary>
public static class UiOperationGuard
{
    /// <summary>
    /// 全局界面反馈出口 —— 由 Shell（<c>MainWindow</c>）在启动时安装，通常写主窗口状态栏。
    ///
    /// <para>作用是覆盖「视图本身没有状态栏」的情况：属性面板、图元库等 UserControl 没有
    /// 自己的提示行，若只落盘日志，用户仍然看到「点了没反应」。这类视图调用
    /// <see cref="Run(string, Func{Task}, Action{string})"/> 时不传 <c>onError</c>，
    /// 失败信息就会回落到这里。</para>
    ///
    /// <para>未安装（如单元测试、无 UI 宿主）时为 <c>null</c>，此时只落盘日志。</para>
    /// </summary>
    public static Action<string>? UiSink { get; set; }

    /// <summary>
    /// 启动一个不阻塞 UI 线程的异步操作，立即返回。
    /// </summary>
    /// <param name="operationKey">
/// 操作名的**本地化键**（<c>Lang.UiOp*</c>），用于日志与失败提示。
/// <para><b>为什么是键而不是文案</b>：这个字符串会出现在**用户可见**的失败提示里
/// （<c>report($"{操作名}失败: …")</c> 落到视图状态行或全局状态栏），
/// 传文案就等于在调用点写死中文 —— 英文界面下会中英混排。</para>
/// </param>
    /// <param name="action">要执行的异步体；其同步前缀仍按调用顺序执行到第一个 await。</param>
    /// <param name="onError">
    /// 本视图的失败提示回调（通常写视图自己的状态栏或日志面板），入参为已格式化好的提示文本；
    /// 传 <c>null</c> 表示回落到 <see cref="UiSink"/>（无 UI 宿主时只落盘日志）。
    /// </param>
    public static void Run(string operationKey, Func<Task> action, Action<string>? onError = null) =>
        // RunAsync 自身吞掉全部异常，因此这里丢弃 Task 不会产生「未观察的异常」。
        _ = RunAsync(operationKey, action, onError);

    /// <summary>
    /// 「操作失败」的**落盘**短句，与 <see cref="RunAsync"/> 用的是同一套模板。
    ///
    /// <para><b>为什么公开</b>：别处原本抄了一份逐字相同的模板 ——
    /// <c>MainWindow.ReportOperationFailure</c> 里写着 <c>"{操作}失败"</c> /
    /// <c>"{操作}失败: {原因}"</c>，与这里一模一样。本地化时一处改了另一处必然漏
    /// （实测就是这么发现的：这一层改完，那一层的两条中文还在）。
    /// 把模板收成唯一实现后，"两处措辞漂移"在结构上不可能发生。</para>
    /// </summary>
    public static string FailureSummary(string operationKey) =>
        AppLocalization.Service.Format(Lang.UiOpFailed, AppLocalization.Service[operationKey]);

    /// <summary>「操作失败: 原因」的**人可见**文案（模板同上）。</summary>
    public static string FailureDetail(string operationKey, string reason) =>
        AppLocalization.Service.Format(Lang.UiOpFailedDetail, AppLocalization.Service[operationKey], reason);

    /// <summary>
    /// 同上，但**原因来自异常** —— 先过 <see cref="ExternalText.FailureReason"/> 把码化异常
    /// 渲染成字典文案（未码化的异常仍回落 <c>ex.Message</c>）。
    ///
    /// <para><b>为什么收成重载、而不是让各处自己取 <c>ex.Message</c></b>：原因那一半与
    /// 上面那条注释是同一个病 —— 各处自己写 <c>ex.Message</c>，码化就只覆盖了"记得改"的那几处。
    /// 收成一处的代价是零，收益是"以后新增的失败出口默认就是码化的"。</para>
    /// </summary>
    public static string FailureDetail(string operationKey, Exception ex) =>
        FailureDetail(operationKey, ExternalText.FailureReason(ex));

    /// <summary>
    /// 打开/保存类操作：失败文案里**必须带上目标文件**（登记表 §4 <b>#60</b> ——
    /// 用户看到「打开工程失败: 工程包已损坏」时不知道出问题的是**哪个文件**）。
    ///
    /// <para><b>形状 = 操作名 + 目标路径 + 原因，三件各自占一个实参</b>：把路径拼进原因串
    /// （<c>reason = path + ": " + ex.Message</c>）也能"显示出来"，但此后"哪一半是文件、
    /// 哪一半是原因"在文案里**不可判** —— 于是"换一种排版"要去改拼接代码，而不是改字典。
    /// 这正是 #60 写下的那条修法方向（文件名作为**实参**进模板）。</para>
    ///
    /// <para><b>为什么两条打开路径都必须走它</b>：它们此前分叉成两句措辞
    /// （<c>打开工程失败: …</c> / <c>打开失败: …</c>），且文件选择器那条**绕过**
    /// <see cref="ExternalText.FailureReason"/> 直接把 <c>ex.Message</c> 交给模板 ⇒
    /// 英文界面下原因那一半照旧是中文（阶段 82 探针实测 **7/7 中英混排**）。</para>
    ///
    /// <para><b><paramref name="targetPath"/> 为 <c>null</c> 时回落到不带文件的那条模板</b>：
    /// 这不是"留个后门"，而是有一类调用点**结构上就拿不到路径** —— <c>MainWindow.OpenProject</c>
    /// 的外层 catch 包着**文件选择器本身**，那一刻用户还没选任何文件（<c>filePath</c> 在函数
    /// 内部声明、不在它的作用域里）。给那种调用点编一个路径比留空更糟，所以留空是**正确**表达。
    /// 除它之外，凡路径在作用域内的失败渲染都必须带路径（登记表 §4 <b>#60</b>）。</para>
    ///
    /// <para><b>🩸 但它不是「凡失败都该改用它」的万能出口（阶段 84 实测定性）</b>：
    /// 登记 <b>#61</b> 的 C 类（<c>status.save.failed</c>(1) / <c>gen.export.failed</c>(2) /
    /// <c>audit.export.c.code.failed</c>(2) / <c>comm.slave.var.export.failed</c>(1) /
    /// <c>asset.import.fail</c>(3)，共 <b>9 处</b>）曾被记为「该带上对象」，阶段 84 逐处实测后
    /// <b>改期</b>了 —— 它们的产出方清一色是 BCL / IO，而 <b>BCL 的 IO 原文自带目标路径</b>
    /// （实测 <c>Access to the path '…' is denied.</c> / <c>Could not find a part of the path '…'.</c>，
    /// 且 <c>CurrentUICulture</c> 切中英文<b>一字不变</b>，全是英文）⇒ 当时在这些站点上用本重载，
    /// 路径会在文案里出现<b>两次</b>，其中一个是 <c>.tmp</c> 兄弟路径（<c>MainWindow.SaveToPath</c>
    /// 写的是 <c>path + ".tmp"</c>）⇒ <b>用户看到的第二个路径是中间文件名，比不带更困惑</b>。
    /// （这一段是**历史定性**：阶段 86 已按下面两条把 9 站里的 8 站迁完。）</para>
    ///
    /// <para><b>真方向是「产出方先码化」—— 阶段 85 已落地，阶段 86 据此迁完</b>：要让用户同时读到
    /// 「哪个文件」+「为什么（他的语言）」，必须先给 <b>BCL 异常类型</b>建码
    /// （<see cref="SystemFailureCodes"/>，登记 <b>#61e</b>，已完成）。
    /// 码化之后原因那一半<b>不再带路径</b>（7 个码全是 0 实参短句）⇒ 上面那条"路径出现两次"
    /// 的理由<b>随之消失</b>，<b>本重载现在才产生真实收益</b>。
    /// ⇒ 阶段 83 钉下的「收口 ≠ 改名」里属于 BCL 的那一半（"BCL 异常没有码，换过去只落回落分支、
    /// 用户所见一字不变"）<b>已随 #61e 一起作废</b>。</para>
    ///
    /// <para><b>#61b（C 类带上对象）已闭环 · 阶段 86</b>：9 站里 8 站迁到本重载 ——
    /// <c>status.save.failed</c> / <c>gen.export.failed</c>×2 / <c>audit.export.c.code.failed</c>×2 /
    /// <c>comm.slave.var.export.failed</c> / <c>asset.import.fail</c>×2（SVG 与字体两档）；
    /// 第 9 站是 <c>asset.import.fail</c> 的<b>批量档</b>，当时按「一次导入多个文件，那个文件结构上
    /// 就不存在」归了 <b>#61a</b> —— <b>阶段 101 实测那句话不成立</b>（<c>path</c> 就在
    /// <c>foreach</c> 里）⇒ 与 <c>status.svg.apply.failed</c> 一并迁完，9 站全闭。
    /// 迁的过程里两件事是**逐处判断**、不能照抄的：
    /// ① <b>操作名</b> —— <c>gen.export.failed</c> 那两条语义重复（导出代码 vs 导出**配方** CSV）
    /// 且分属两个视图 ⇒ 拆成 <c>UiOpExportCode</c> / <c>UiOpExportRecipe</c>；
    /// ② <b>传哪个路径</b> —— 失败点常落在 <c>path + ".tmp"</c> 上，把中间文件名当"目标文件"
    /// 报给用户依旧是错的（只是它<b>不会</b>再自己冒出来，因为原因那半已经不带了）。
    /// ⚠ 另有两处要把一个<b>承接变量</b>提到 <c>try</c> 外（<c>CommunicationConfigView</c> 的 <c>file</c>、
    /// <c>BuildPipelineService.CompileAsync</c> 的 <c>targetDir</c> 都声明在 <c>try</c> 内）——
    /// 编译器会当场报 <c>CS0103</c>，别用 <c>null</c> 糊过去（那就把"带上对象"又丢了）。
    /// 探针 <c>tools/probe-bcl-io-messages.cs</c> / <c>tools/probe-bcl-exception-types.cs</c>，
    /// 读数与完整推理见 <c>docs/2026-09-23-exception-passthrough-triage.md</c> §84–86。</para>
    /// </summary>
    public static string FailureDetail(string operationKey, string? targetPath, Exception ex) =>
        targetPath is null
            ? FailureDetail(operationKey, ex)
            : AppLocalization.Service.Format(Lang.UiOpFailedDetailFile,
                AppLocalization.Service[operationKey],
                targetPath,
                ExternalText.FailureReason(ex));

    /// <summary>
    /// 与 <see cref="Run"/> 行为一致，但返回可等待的 <see cref="Task"/>，便于测试与顺序编排。
    /// 该方法<b>永不抛出</b>。
    /// </summary>
    public static async Task RunAsync(string operationKey, Func<Task> action, Action<string>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            await action().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // 用户取消（关闭对话框、取消下载）不是失败，不该弹错误提示。
        }
        catch (Exception ex)
        {
            AppLog.Error(FailureSummary(operationKey), ex);

            // 优先用视图自己的状态行；视图没有状态行时回落到全局状态栏。
            var report = onError ?? UiSink;
            if (report is null)
                return;

            try
            {
                report(FailureDetail(operationKey, ex));
            }
            catch (Exception reportEx)
            {
                // 界面提示本身失败不能再抛（否则又回到 async void 的兜底路径）。
                AppLog.Error(AppLocalization.Service.Format(Lang.UiOpReportFailed, AppLocalization.Service[operationKey]), reportEx);
            }
        }
    }
}
