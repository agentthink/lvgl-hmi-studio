using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// App 层「取消链路接线」守卫 —— 静态扫源码。
///
/// <para><b>为什么是静态扫描而不是行为测试</b>：AI 层的 token 传递是**可选参数**
/// （<c>CancellationToken ct = default</c>），漏传照常编译、照常通过行为测试 ——
/// 而漏传的后果是"取消按钮点了没反应"，属于典型的**静默失效**。
/// App 层要验的是"有没有把 token 接上"，这正是源码形态的性质。</para>
///
/// <para><b>背景（2026-09-15 收口前实测）</b>：AI 内部的 token 链路本来是完整的，
/// 但 App 层 <b>6 个入口全线漏传</b> —— <c>AiGenerateDialogBase</c> 的抽象签名里根本没有
/// <c>CancellationToken</c>（2 个对话框继承）、<c>AiChatPanelView</c> 的非 Agent 四个分支、
/// <c>AiIconDialog</c> 的两个生成入口；对话框的取消按钮则只做 <c>Close()</c>。
/// 于是 <c>BatchIconGenerator</c> 里那条 <c>ThrowIfCancellationRequested</c> 在应用里
/// <b>永不可达</b>。</para>
///
/// <para><b>扫描面自检</b>：本文件所有用例都先断言"扫到了预期数量的目标"
/// （有过教训：扫描器范围本身就是最可能的盲区，只扫 <c>.json</c> 漏 <c>.cs</c>）。</para>
/// </summary>
public class AiCancellationWiringTests
{
    // ── 源码定位（扫描器已抽到 AppSourceScanner，与数据根接线守卫共用同一扫描面） ──

    private static List<(string Relative, string Text)> ScanAppSources() => AppSourceScanner.ScanAppSources();

    // ── 规则 1：对话框子类的 GenerateAsync 重写必须带 CancellationToken ──

    private static readonly Regex DialogSubclass =
        new(@"class\s+\w+\s*:\s*AiGenerateDialogBase\s*<", RegexOptions.Compiled);

    private static readonly Regex GenerateOverride =
        new(@"override\s+[^;{}]*?\bGenerateAsync\s*\(([^)]*)\)", RegexOptions.Compiled);

    private static readonly Regex TokenParam = new(@"\bCancellationToken\b", RegexOptions.Compiled);

    [Fact]
    public void EveryDialogSubclass_OverridesGenerateAsync_WithCancellationToken()
    {
        var sources = ScanAppSources();
        var subclasses = sources.Where(s => DialogSubclass.IsMatch(s.Text)).ToList();

        // 自检：真的扫到了子类（本项目 2 个：Script / Template）。
        Assert.True(subclasses.Count >= 2,
            $"扫描面可疑：只扫到 {subclasses.Count} 个 AiGenerateDialogBase 子类 —— 文件清单={string.Join(", ", sources.Select(s => s.Relative))}");

        foreach (var (relative, text) in subclasses)
        {
            var match = GenerateOverride.Match(text);
            Assert.True(match.Success, $"{relative}: 未找到 GenerateAsync 重写（改了签名？）");
            Assert.True(TokenParam.IsMatch(match.Groups[1].Value),
                $"{relative}: GenerateAsync 重写缺少 CancellationToken 形参 —— 取消按钮会变成摆设");
        }
    }

    [Fact]
    public void DialogBase_AbstractSignature_DeclaresCancellationToken()
    {
        var baseFile = ScanAppSources().Single(s => s.Relative.EndsWith("AiGenerateDialogBase.cs", StringComparison.Ordinal));
        var match = Regex.Match(baseFile.Text, @"abstract\s+[^;{}]*?\bGenerateAsync\s*\(([^)]*)\)");

        Assert.True(match.Success, "AiGenerateDialogBase: 未找到 GenerateAsync 的抽象声明");
        Assert.True(TokenParam.IsMatch(match.Groups[1].Value),
            "AiGenerateDialogBase: 抽象签名没有 CancellationToken ⇒ 子类必然漏传");
    }

    // ── 规则 2：所有生成调用点必须把 token 传下去 ──

    /// <summary>
    /// 匹配 App 层对 AI 生成入口的调用（<c>AiPreviewManager</c> 四件套 + 生成器直呼）。
    /// 定义侧（<c>AiPreviewManager.cs</c>）与无参私有包装（<c>GenerateImageAsync()</c> 前面无点）不会命中。
    /// </summary>
    private static readonly Regex GenerateCall = new(
        @"\.(?:Generate(?:Screen|Template|Script|Image)Async|GenerateBatchAsync|GenerateAsync)\s*\(([^()]*)\)",
        RegexOptions.Compiled);

    /// <summary>实参里出现 token / ct（词边界，避免 Project 之类的误判）。</summary>
    private static readonly Regex TokenArgument = new(@"\b(?:token|ct)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void EveryGenerateCallSite_PassesCancellationToken()
    {
        var offenders = new List<string>();
        var checkedCalls = 0;

        foreach (var (relative, text) in ScanAppSources())
        {
            if (relative.EndsWith("AiPreviewManager.cs", StringComparison.Ordinal)) continue; // 定义侧

            foreach (Match m in GenerateCall.Matches(text))
            {
                // 只关心真正的生成调用：`_manager.Generate*Async` / `batch.GenerateBatchAsync` /
                // `ImageGenerator.GenerateAsync` / `IconGenerator.GenerateAsync`
                var line = LineOf(text, m.Index);
                if (!line.Contains("_manager.", StringComparison.Ordinal) &&
                    !line.Contains("ImageGenerator.", StringComparison.Ordinal) &&
                    !line.Contains("IconGenerator.", StringComparison.Ordinal) &&
                    !line.Contains("batch.", StringComparison.Ordinal) &&
                    !line.Contains("Manager.", StringComparison.Ordinal) &&
                    !line.Contains("_llm.", StringComparison.Ordinal) &&
                    !line.Contains("_provider.", StringComparison.Ordinal))
                    continue;

                checkedCalls++;
                if (!TokenArgument.IsMatch(m.Groups[1].Value))
                    offenders.Add($"{relative}: {line.Trim()}");
            }
        }

        // 自检：真的扫到了调用点（当前 7 处：2 对话框 + 4 聊天面板分支 + 1 批量图标）。
        Assert.True(checkedCalls >= 7,
            $"扫描面可疑：只检查到 {checkedCalls} 处生成调用点（预期 ≥7）");

        Assert.True(offenders.Count == 0,
            "以下生成调用点没有传 CancellationToken ⇒ 取消按钮对该路径无效：" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    // ── 规则 3：取消按钮不得"只关窗" ──

    private static readonly Regex CancelOnlyCloses = new(
        @"CancelBtn\.Click\s*\+=\s*[^;]*\bClose\s*\(", RegexOptions.Compiled);

    [Fact]
    public void NoCancelButton_IsWiredToCloseOnly()
    {
        var offenders = ScanAppSources()
            .Where(s => CancelOnlyCloses.IsMatch(s.Text))
            .Select(s => s.Relative)
            .ToList();

        Assert.True(offenders.Count == 0,
            "取消按钮直接绑到 Close(...) ⇒ 在途请求不会被取消（还会回写 Pending）：" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Scanner_ActuallyCoversTheCancelPlumbingFiles()
    {
        var files = ScanAppSources().Select(s => s.Relative).ToList();

        // 自检：扫描面必须包含本轮改动涉及的全部文件，否则上面的规则是空转。
        foreach (var required in new[]
                 {
                     "Controls/AiGenerateDialogBase.cs",
                     "Views/AiChatPanelView.axaml.cs",
                     "Views/AiIconDialog.axaml.cs",
                     "Views/AiScriptDialog.axaml.cs",
                     "Services/AiPreviewManager.cs",
                 })
        {
            Assert.True(files.Any(f => f.Replace('\\', '/').EndsWith(required, StringComparison.Ordinal)),
                $"扫描面漏了 {required}（实际扫到 {files.Count} 个文件）");
        }
    }

    private static string LineOf(string text, int index)
    {
        var start = text.LastIndexOf('\n', Math.Max(0, index - 1));
        var end = text.IndexOf('\n', index);
        if (end < 0) end = text.Length;
        return text[(start + 1)..end];
    }
}
