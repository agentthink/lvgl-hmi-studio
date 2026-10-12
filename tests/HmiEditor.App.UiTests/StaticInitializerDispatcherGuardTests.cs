using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **静态初始化器不得构造"会绑 Dispatcher"的 Avalonia 对象**（阶段 295；`#47` 的静态面守门人）。
///
/// <para>行为面的判据（<see cref="AmbientDispatcherTheftGuardTests"/>）只覆盖**今天已知的那几个字段**；
/// 这条守的是**规则本身**：<c>static readonly X = new SolidColorBrush(...)</c> 这类写法，
/// 构造发生在类型初始化器里、跑在"谁先碰到这个类型"的线程上 ⇒ 一旦不在派发内，
/// 就会把 <c>Dispatcher.s_uiThread</c> 抢到那个线程，下一次派发报
/// <c>"different thread owns it"</c>（登记 `#47`）。</para>
///
/// <para><b>两条腿</b>：① 违规面必须为 0；② **反向腿** —— 必须扫得到 <c>Immutable*</c> 的静态初始化器
/// （否则说明正则的"形状"没对上，本判据会退化成永远通过）。</para>
///
/// <para><b>已知边界</b>（写下来免得被当全量保证）：按**行**匹配 ⇒
/// "`static` 与 `= new …(` 分两行"的写法扫不到；只扫 <c>src/</c>（产品代码），
/// <c>tests/</c> 里的同类写法不在本判据面上。</para>
/// </summary>
public class StaticInitializerDispatcherGuardTests
{
    private readonly ITestOutputHelper _o;

    public StaticInitializerDispatcherGuardTests(ITestOutputHelper o) => _o = o;

    /// <summary>会绑 Dispatcher 的**可变** Avalonia 对象（静态初始化器里不许 new；`Immutable*` 不在列）。</summary>
    private static readonly string[] DispatcherBound =
    {
        "SolidColorBrush", "LinearGradientBrush", "RadialGradientBrush", "ImageBrush", "VisualBrush",
        "Pen", "FormattedText", "StreamGeometry", "DrawingGroup", "GeometryGroup",
    };

    private static readonly Regex StaticFieldInitializer = new(
        @"\bstatic\b[^;=]*=\s*new\s+(?<type>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Compiled);

    [Fact]
    public void NoStaticInitializer_ConstructsADispatcherBoundAvaloniaObject()
    {
        var offenders = new List<string>();
        var immutableWitnesses = new List<string>();

        foreach (var file in SourceScanner.ProductSources())
        {
            var code = SourceScanner.ReadCode(file);   // 已剥注释
            var rel = SourceScanner.Relative(file);

            foreach (var line in code.Split('\n'))
            {
                var m = StaticFieldInitializer.Match(line);
                if (!m.Success) continue;

                var type = m.Groups["type"].Value;
                if (type.StartsWith("Immutable", StringComparison.Ordinal))
                    immutableWitnesses.Add($"{type} @ {rel}");
                else if (DispatcherBound.Contains(type, StringComparer.Ordinal))
                    offenders.Add($"{rel}: {line.Trim()}");
            }
        }

        JudgeReadings.Write(_o, "静态初始化器不绑 Dispatcher（阶段 295）", new[]
        {
            $"违规 0 条；Immutable* 见证 {immutableWitnesses.Count} 条（反向腿下界 3）",
            "规则：static readonly 画刷/画笔/文本布局必须用 Immutable* 版本（静态初始化器跑在任意线程上）",
        });

        Assert.True(offenders.Count == 0,
            "静态字段初始化器里构造了会绑 Dispatcher 的 Avalonia 对象 —— 类型初始化器跑在任意线程上，"
            + "这会把 Dispatcher.s_uiThread 抢走，下一次派发报 'different thread owns it'（登记 #47）："
            + Environment.NewLine + string.Join(Environment.NewLine, offenders)
            + Environment.NewLine + "修法：改用对应的 Immutable* 版本。");

        Assert.True(immutableWitnesses.Count >= 3,
            $"反向腿失败：只扫到 {immutableWitnesses.Count} 条 `Immutable*` 静态初始化器（下界 3）"
            + " —— 说明本判据的匹配形状没对上，它会退化成'永远通过'。");
    }
}
