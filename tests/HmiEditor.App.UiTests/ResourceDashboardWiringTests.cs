using System;
using System.Linq;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 资源仪表盘的**预算来源**接线守卫（登记表 §4 第 29 项"资源预算"）。
///
/// <para><b>它防的是什么：</b>仪表盘曾把预算写死在视图里 —— <c>8MB Flash / 64MB RAM</c>，
/// 注释还写着"可从 HardwareTarget 获取实际值"，但从未获取过；而 T113 目标声明的
/// Flash 预算当时是 16MB（真值 128MB，2026-09-18 修正）。写死的分母会让占用率显示成
/// 真值的 8 倍，且**目标换了也不会变**。这类"声明了但没人用"的预算，靠肉眼看百分比
/// 永远发现不了，所以用源码扫描把它钉住。</para>
///
/// <para>为什么是扫描而不是行为断言：这条逻辑在 <c>MainWindow</c> 的私有方法里，
/// 唯一的行为入口是真实窗口 + 状态栏控件；而缺陷本身是"常量写死"，扫描是正对靶心的判据。
/// 阈值/判读规则的行为判据在 <c>Hmi.Hardware.Tests</c> 的 <c>ResourceBudgetTests</c>。</para>
/// </summary>
public class ResourceDashboardWiringTests
{
    private static string MethodBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"App 源码里找不到方法 {signature}");
        var open = source.IndexOf('{', start);
        Assert.True(open > start, $"{signature} 找不到方法体起始");
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0) return source[open..(i + 1)];
            }
        }
        throw new InvalidOperationException($"{signature} 方法体括号不平衡");
    }

    [Fact]
    public void Scanner_FindsTheDashboardAndSeesItsBudgetSource()
    {
        // 自检：提取器若失灵（方法改名/签名变化），下面的断言会退化成"扫到空串也全绿"
        var (_, text) = AppSourceScanner.Single("MainWindow.axaml.cs");
        var body = MethodBody(text, "private void UpdateResourceDashboard()");

        Assert.True(body.Length > 100, $"提取到的 UpdateResourceDashboard 方法体只有 {body.Length} 字符 —— 扫描器可能已失灵");
        Assert.Contains("ResourceBudget.Evaluate", body, StringComparison.Ordinal);
        Assert.Contains("ResolveCurrentHardwareTarget", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_TakesTheBudgetFromTheHardwareTarget_NotFromHardcodedTotals()
    {
        var (_, text) = AppSourceScanner.Single("MainWindow.axaml.cs");
        var body = MethodBody(text, "private void UpdateResourceDashboard()");

        // 旧的写死分母：一个都不许回来
        Assert.DoesNotContain("8 * 1024 * 1024", body, StringComparison.Ordinal);
        Assert.DoesNotContain("64 * 1024 * 1024", body, StringComparison.Ordinal);
        // 预算必须来自目标契约的 Resources（经过判读器）
        Assert.Contains("Resources", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetResolution_UsesTheFirmwareRegistry_AndToleratesAnUnknownId()
    {
        var (_, text) = AppSourceScanner.Single("MainWindow.axaml.cs");
        var resolver = MethodBody(text, "private IHardwareTarget? ResolveCurrentHardwareTarget()");

        // 型号由固件决定 ⇒ 必须走注册表按 id 取，而不是 if/else 写死某块板
        Assert.Contains("GetTarget", resolver, StringComparison.Ordinal);
        Assert.Contains("_hardwareTargetRegistry", resolver, StringComparison.Ordinal);

        var loader = MethodBody(text, "private static HardwareTargetRegistry LoadHardwareTargets()");
        Assert.Contains("LoadFirmwareTargets", loader, StringComparison.Ordinal);
        // 固件目录缺失时回落内置 T113（与工程设置面板同一条路径）
        Assert.Contains("AllwinnerT113Target", loader, StringComparison.Ordinal);
    }
}
