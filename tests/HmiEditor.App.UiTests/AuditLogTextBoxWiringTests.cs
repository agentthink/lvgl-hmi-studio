using System;
using System.Collections;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`AuditLogView` 的 `TextBox → 过滤` 驱动面**（阶段 190）——
/// 把「TextBox 接线普查面」（<see cref="AppTextBoxWiringCensusTests"/>）里
/// <c>AuditLogView.axaml</c> 的那 1 条接线从 `census` 升级成 `driven`。
///
/// <para><b>缺口形状</b>：`FilterBox.TextChanged += (_, _) => ApplyFilter()` 在此之前
/// **零判据** —— 把这条接线摘掉、或把 `ApplyFilter` 里的 `Trim()` 删掉，全仓没有一条会红。
/// 真实输入下它工作，但测试面为零（与登记 `#88` 同一类）。</para>
///
/// <para><b>读的是哪一面</b>：过滤的**可观察效果就是列表本身**（`AuditList.ItemsSource` 是
/// 那份投影），所以这里读的不是界面文字，而是 `ApplyFilter` 写进列表的那一份投影。
/// 起点数据由**真的**调 <c>AuthenticationService.CreateUser</c> 造出（走生产审计路径），
/// 而不是手搓 <c>AuditLogEntry</c> 塞进去 —— 后者会绕过「审计到底记没记」这件事。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class AuditLogTextBoxWiringTests
{
    /// <summary>一条手势：把 <paramref name="Text"/> 写进 <paramref name="Name"/>，断言列表里剩 <paramref name="Expected"/> 条。</summary>
    private sealed record Spec(string Name, string Text, int Expected, string Why);

    private static readonly Spec[] Specs =
    {
        // 两条审计条目的 Username 分别是 alpha_op / beta_op，Detail 都含「创建用户」
        new("FilterBox", "alpha_op", 1, "按用户名过滤（唯一命中一条）"),
        new("FilterBox", "  创建用户  ", 2, "**裁剪**后过滤：两侧空白不算内容，Detail 里两条都含「创建用户」"),
        new("FilterBox", "zzz-没有这条", 0, "过滤到空集（不是『过滤没生效』）"),
        new("FilterBox", "", 2, "清空 ⇒ 回到全量（**不是**留在上一次的过滤结果上）"),
    };

    /// <summary>当前列表里显示的那些条目数（`ApplyFilter` / `Refresh` 写进去的那份投影）。</summary>
    private static int Shown(AuditLogView view)
    {
        var list = ControlWiringFace.Find<ListBox>(view, "AuditList");
        return list.ItemsSource is IEnumerable source
            ? source.Cast<object>().Count()
            : list.ItemCount;
    }

    [Fact]
    public void EverySpec_FiltersTheListAsDeclared()
    {
        using var sandbox = new DataRootSandbox("audit-textbox");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);

            // 造两条**可分辨**的审计条目（走生产路径：CreateUser 自己 AppendAudit）
            depot.Auth.CreateUser("alpha_op", "甲操作员", "Pw12345!");
            depot.Auth.CreateUser("beta_op", "乙操作员", "Pw12345!");

            var view = new AuditLogView();
            view.Attach(depot.Auth);

            var window = new Window { Width = 900, Height = 600, Content = view };
            window.Show();
            UiTestHost.Pump(3);
            try
            {
                // 前提自检：起点必须是「2 条、未被谁预先过滤过」—— 否则下面的计数断言不成立
                Assert.Equal(2, depot.Auth.GetAuditLog(500).Count);
                Assert.Equal(2, Shown(view));

                foreach (var spec in Specs)
                {
                    ControlWiringFace.Write(view, spec.Name, spec.Text);
                    var shown = Shown(view);
                    Assert.True(spec.Expected == shown,
                        $"过滤 '{spec.Text}'（{spec.Why}）后列表里读到 {shown} 条，期望 {spec.Expected} 条");
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// **单文件双向差集**：`AuditLogView.axaml` 的每个具名 `TextBox` 与每条 `TextChanged` 接线
    /// 都必须在本表里（反面同样成立）—— 新增一条没人管的接线会立刻红。
    /// </summary>
    [Fact]
    public void EveryTextBoxAndWiring_IsDrivenByTheTable()
    {
        ControlWiringFace.AssertFullyDriven(
            "AuditLogView.axaml", Specs.Select(s => s.Name).ToArray(), scanFloor: 1);
    }
}
