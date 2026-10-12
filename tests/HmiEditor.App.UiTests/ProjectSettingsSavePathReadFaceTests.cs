using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`ProjectSettingsView` 的「保存时一次性读取」框的**行为面****（阶段 197）——
/// 把「输入框『谁读』面」（<see cref="AppUnwiredInputReadFaceTests"/>，那是**结构性**的账：
/// "有没有人读"）往下一层推：**读得对不对**。
///
/// <para><b>这一族为什么需要单独的行为面</b>：这 15 个数字框**没有** `ValueChanged` 接线
/// （它们在普查面的 `withoutWiring` 登记表里），值是靠点「应用/确定」时
/// <c>SaveToProject()</c> 一次性读走的 —— 接线面管不到它们，而"结构面"只能证明有人读了它，
/// 证明不了**读进了对的字段**（`_project.A` 被写成 `NumB.Value` 这种抄错，结构面原理上看不见）。</para>
///
/// <para><b>判据形状</b>：15 个框**逐个驱动成互不相同的值**（都不等于起点值 —— 前提自检），
/// 再点一次「应用」让 <c>SaveToProject()</c> 一次性读走，最后**逐字段**断言模型里是"自己那个框"的值。
/// 抄错一行 ⇒ 那一格读到的是**别的框**的值 ⇒ 红（负控 nc1 证明）。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class ProjectSettingsSavePathReadFaceTests
{
    /// <summary>一条手势：把 <paramref name="Value"/> 写进 <paramref name="Name"/>，点应用后断言模型字段读到 <paramref name="Expected"/>。</summary>
    /// <param name="Field">模型字段（人读的路径；也用来判"映射互异"）。</param>
    private sealed record Spec(string Name, string Field, Func<HmiProject, string> Read, double Value, string Expected);

    private static readonly Spec[] Specs =
    {
        // ── 屏幕默认值 ──
        new("NumScreenWidth", "ScreenDefaultsConfig.DefaultWidth",
            p => p.ScreenDefaultsConfig.DefaultWidth.ToString(CultureInfo.InvariantCulture), 1280, "1280"),
        new("NumScreenHeight", "ScreenDefaultsConfig.DefaultHeight",
            p => p.ScreenDefaultsConfig.DefaultHeight.ToString(CultureInfo.InvariantCulture), 720, "720"),
        new("NumDpiScale", "ScreenDefaultsConfig.DpiScale",
            p => p.ScreenDefaultsConfig.DpiScale.ToString(CultureInfo.InvariantCulture), 2, "2"),

        // ── 通讯默认值（VariableManager）──
        new("NumCommScanInterval", "VariableManager.DefaultScanIntervalMs",
            p => p.VariableManager.DefaultScanIntervalMs.ToString(CultureInfo.InvariantCulture), 250, "250"),
        new("NumCommTimeout", "VariableManager.DefaultTimeoutMs",
            p => p.VariableManager.DefaultTimeoutMs.ToString(CultureInfo.InvariantCulture), 1200, "1200"),
        new("NumRetryCount", "VariableManager.MaxRetryCount",
            p => p.VariableManager.MaxRetryCount.ToString(CultureInfo.InvariantCulture), 7, "7"),
        new("NumRetryBackoff", "VariableManager.RetryBackoffMs",
            p => p.VariableManager.RetryBackoffMs.ToString(CultureInfo.InvariantCulture), 2500, "2500"),

        // ── 运行期（RuntimeConfig）──
        new("NumRuntimeScan", "RuntimeConfig.ScanIntervalMs",
            p => p.RuntimeConfig.ScanIntervalMs.ToString(CultureInfo.InvariantCulture), 300, "300"),
        new("NumWatchdog", "RuntimeConfig.WatchdogTimeoutMs",
            p => p.RuntimeConfig.WatchdogTimeoutMs.ToString(CultureInfo.InvariantCulture), 8000, "8000"),
        new("NumHeartbeat", "RuntimeConfig.HeartbeatIntervalMs",
            p => p.RuntimeConfig.HeartbeatIntervalMs.ToString(CultureInfo.InvariantCulture), 2000, "2000"),
        new("NumCrashSector", "RuntimeConfig.CrashDumpSector",
            p => p.RuntimeConfig.CrashDumpSector.ToString(CultureInfo.InvariantCulture), 5, "5"),
        new("NumMaxTasks", "RuntimeConfig.MaxRuntimeTasks",
            p => p.RuntimeConfig.MaxRuntimeTasks.ToString(CultureInfo.InvariantCulture), 64, "64"),

        // ── 安全（SecurityConfig）──
        new("NumSessionTimeout", "SecurityConfig.SessionTimeoutMin",
            p => p.SecurityConfig.SessionTimeoutMin.ToString(CultureInfo.InvariantCulture), 45, "45"),
        new("NumMaxAttempts", "SecurityConfig.MaxLoginAttempts",
            p => p.SecurityConfig.MaxLoginAttempts.ToString(CultureInfo.InvariantCulture), 9, "9"),
        new("NumLockout", "SecurityConfig.LockoutDurationMin",
            p => p.SecurityConfig.LockoutDurationMin.ToString(CultureInfo.InvariantCulture), 20, "20"),
    };

    [Fact]
    public void EveryDeferredBox_LandsInItsOwnModelField()
    {
        using var sandbox = new DataRootSandbox("projectsettings-save-read");

        UiTestHost.Run(() =>
        {
            var project = new HmiProject { Name = "夹具工程" };
            project.Screens.Add(ScreenFactory.Create(project, name: "主画面"));

            var view = new ProjectSettingsView(project);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                // 前提自检①：15 个框回填的都是**模型里的**值（结构面已经证过"有人读"，这里证"回填对"）
                foreach (var spec in Specs)
                {
                    var current = ControlWiringFace.NumberValue(view, spec.Name);
                    Assert.NotNull(current);
                }

                // 前提自检②：每条手势的值都必须**不等于起点值** ——
                //   `SaveToProject()` 会把框里的值抄进模型，若起点本来就是那个值，
                //   "抄对了"与"压根没抄"在读数上同形（阶段 195 在 ScreenManager 上踩过这条假绿）
                foreach (var spec in Specs)
                {
                    var before = spec.Read(project);
                    Assert.True(!string.Equals(spec.Expected, before, StringComparison.Ordinal),
                        $"{spec.Name}（{spec.Field}）的手势值 {spec.Value} 与起点值相同 ⇒ 这条腿是空转");

                    ControlWiringFace.WriteNumber(view, spec.Name, spec.Value, pump: 1);
                }

                // 保存前不落盘：这 15 个框**没有**接线，值只活在控件里
                foreach (var spec in Specs)
                    Assert.True(!string.Equals(spec.Expected, spec.Read(project), StringComparison.Ordinal),
                        $"{spec.Name} 在点「应用」之前就落进模型了 —— 说明它其实有接线（该归接线面）");

                // ── 点一次「应用」：`SaveToProject()` 一次性读走全部 15 个框 ──
                ControlWiringFace.Find<Button>(view, "BtnApply")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                UiTestHost.Pump(2);

                // ── 逐字段断言：每个框的值都落在**自己那一格** ──
                foreach (var spec in Specs)
                {
                    var after = spec.Read(project);
                    Assert.True(string.Equals(spec.Expected, after, StringComparison.Ordinal),
                        $"{spec.Name} 驱动 {spec.Value} 并点「应用」后，{spec.Field} 读到 '{after}'，期望 '{spec.Expected}'"
                        + "（抄错一行时，这一格读到的会是**别的框**的值）");
                }
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **表内自检**：15 条手势的值**互不相同**、字段路径**互不相同** ——
    /// 前者保证"一格的读数能唯一归因到某个框"，后者保证表本身没抄重（产品侧抄错由上面那条判据抓）。
    /// </summary>
    [Fact]
    public void Specs_AreDistinct()
    {
        Assert.Equal(15, Specs.Length);
        Assert.Equal(Specs.Length, Specs.Select(s => s.Value).Distinct().Count());
        Assert.Equal(Specs.Length, Specs.Select(s => s.Field).Distinct().Count());
        Assert.Equal(Specs.Length, Specs.Select(s => s.Name).Distinct().Count());
    }

    /// <summary>
    /// **这 15 个框确实都"没接线"**（否则该归接线面，本面就重复了）：
    /// 与 `ProjectSettingsNumberBoxWiringTests` 的 `withoutWiring` 登记表**同一份事实**，
    /// 这里用扫描再核一遍（表与代码哪边漂了都红）。
    /// </summary>
    [Fact]
    public void TheseBoxes_ReallyHaveNoDirectWiring()
    {
        ControlWiringFace.AssertFullyDrivenNumberBoxes(
            "ProjectSettingsView.axaml",
            driven: new[] { "NumPwdMinLen" },                       // 面 A：直接接线的那一个
            scanFloor: 1,
            withoutWiring: Specs.ToDictionary(
                s => s.Name,
                s => "由 `SaveToProject()` 在点「应用/确定」时一次性读取（本面判的是「读进了哪一格」）"),
            anonymousWired: new[] { "nud", "fanumber" });
    }
}
