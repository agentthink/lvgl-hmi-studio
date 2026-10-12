using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// App 层「流式接线」守卫 —— 静态扫源码。
///
/// <para><b>为什么必须扫源码</b>：<c>textProgress</c> 与 <c>CancellationToken</c> 一样是
/// **可选参数**（<c>IProgress&lt;string&gt;? textProgress = null</c>）。漏传照常编译、
/// 照常通过行为测试，后果却是"长任务期间界面完全静止" —— 典型静默失效。
/// 行为测试能验"流式本身对不对"（AI 层已覆盖），App 层要验的是
/// "有没有把观察者接上"，这正是源码形态的性质。</para>
///
/// <para><b>扫描面自检</b>：每个用例先断言"扫到了预期数量的目标"
/// —— 扫描器范围本身是最可能的盲区（见 <see cref="AiCancellationWiringTests"/> 的同类注释）。</para>
/// </summary>
public class AiStreamingWiringTests
{
    private static List<(string Relative, string Text)> Scan() => AppSourceScanner.ScanAppSources();

    /// <summary>匹配 App 层对 <c>AiPreviewManager</c> 四件套的调用。</summary>
    private static readonly Regex ManagerGenerateCall = new(
        @"_manager\.Generate(?:Screen|Template|Script|Image)Async\s*\(([^()]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex AgentRunCall = new(
        @"AgentHost!?\.RunAsync\s*\(([^()]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex OrchestratorProcessCall = new(
        @"_orchestrator!?\.ProcessAsync\s*\(([^()]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex TextProgressArg = new(@"\btextProgress\b", RegexOptions.Compiled);

    private static string PanelSource() =>
        Scan().Single(s => s.Relative.Replace('\\', '/').EndsWith("Views/AiChatPanelView.axaml.cs", StringComparison.Ordinal)).Text;

    private static string PreviewManagerSource() =>
        Scan().Single(s => s.Relative.Replace('\\', '/').EndsWith("Services/AiPreviewManager.cs", StringComparison.Ordinal)).Text;

    [Fact]
    public void AiChatPanel_EveryNonAgentGenerationBranch_PassesTextProgress()
    {
        var calls = ManagerGenerateCall.Matches(PanelSource());

        // 自检：四条分支（模板/脚本/图片/屏幕）一个都不能少
        Assert.True(calls.Count >= 4,
            $"扫描面可疑：聊天面板只找到 {calls.Count} 处生成调用（预期 4 条意图分支）");

        var offenders = calls
            .Where(m => !TextProgressArg.IsMatch(m.Groups[1].Value))
            .Select(m => m.Value)
            .ToList();

        Assert.True(offenders.Count == 0,
            "以下生成分支没有接流式观察者 ⇒ 该意图的生成过程界面全静止：" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void AiChatPanel_AgentRun_PassesTextProgress()
    {
        var calls = AgentRunCall.Matches(PanelSource());

        Assert.True(calls.Count >= 1, "扫描面可疑：聊天面板未找到 AgentHost.RunAsync 调用");
        Assert.All(calls.Cast<Match>(), m =>
            Assert.True(TextProgressArg.IsMatch(m.Groups[1].Value),
                $"Agent 运行未接流式观察者 ⇒ 多步任务期间只见步骤、模型输出看不到：{m.Value}"));
    }

    [Fact]
    public void AiPreviewManager_ForwardsTextProgressToOrchestrator()
    {
        var calls = OrchestratorProcessCall.Matches(PreviewManagerSource());

        // 自检：四件套（屏幕/模板/脚本/图片）各一处
        Assert.True(calls.Count >= 4,
            $"扫描面可疑：AiPreviewManager 只找到 {calls.Count} 处 ProcessAsync 调用（预期 4 处）");

        var offenders = calls
            .Where(m => !TextProgressArg.IsMatch(m.Groups[1].Value))
            .Select(m => m.Value)
            .ToList();

        Assert.True(offenders.Count == 0,
            "以下 ProcessAsync 调用没有把观察者转发下去 ⇒ 观察者在半路被丢掉：" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Scanner_ActuallyCoversTheStreamingPlumbingFiles()
    {
        var files = Scan().Select(s => s.Relative).ToList();

        foreach (var required in new[]
                 {
                     "Views/AiChatPanelView.axaml.cs",
                     "Services/AiPreviewManager.cs",
                     "Views/ChatEntry.cs",
                 })
        {
            Assert.True(files.Any(f => f.Replace('\\', '/').EndsWith(required, StringComparison.Ordinal)),
                $"扫描面漏了 {required}（实际扫到 {files.Count} 个文件）");
        }
    }

    // ── ChatEntry 的流式可见性语义（纯逻辑，不依赖渲染） ──

    [Fact]
    public void ChatEntry_LivePreview_RequiresStreamingFlagAndNonEmptyPreview()
    {
        var entry = new ChatEntry();

        // 未开始流式：即使有预览文本也不显示（避免运行结束后残留）
        Assert.False(entry.HasLivePreview);

        entry.LivePreview = "abc";
        Assert.False(entry.HasLivePreview);

        entry.IsStreaming = true;
        Assert.True(entry.HasLivePreview);

        // 空预览不显示（避免空框闪一下）
        entry.LivePreview = "";
        Assert.False(entry.HasLivePreview);

        // 收尾：撤下流式标志即隐藏，无需清空文本
        entry.LivePreview = "abc";
        entry.IsStreaming = false;
        Assert.False(entry.HasLivePreview);
    }
}
