using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`ProjectSettingsView` 的数字框驱动面**（阶段 195）—— 这一族里它有两件事要做：
///
/// <list type="number">
/// <item><b>面 A（1 条）</b>：<c>NumPwdMinLen.ValueChanged</c> 只更新**口令提示**的占位文案
/// （"口令至少 N 个字符"）—— 读 `TxtRuntimeUserPassword.PlaceholderText` 里那个数，
/// **不读整句**（文案走字典）。</item>
/// <item><b>面 B（1 条）</b>：`AttachChangeHandler&lt;T&gt;` 里的泛型参数 <c>nud</c>
/// （`nud.ValueChanged += (_, _) => _hasChanges = true;`）—— 用 <c>NumPwdMinLen</c> **代表驱动**它，
/// 见证选「点『确定』之后工程真的被保存」（`_hasChanges` 唯一的消费者就是 `OnOkClicked` 里那句
/// `if (_hasChanges) ApplyInternal();`，而它写进工程的那一格正是 `PasswordMinLength`）。</item>
/// </list>
///
/// <para><b>「没（直接）接线」的 15 个数字框</b>（DPI / 通讯 / 运行期那些）走
/// `SaveToProject()` 一次性读取 —— 不是缺口，但要显式登记（见 <c>WithoutWiring</c>）。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class ProjectSettingsNumberBoxWiringTests
{
    /// <summary>驱动面表：`NumPwdMinLen` 一条（面 A 的那条接线）。</summary>
    private static readonly string[] Driven = { "NumPwdMinLen" };

    /// <summary>
    /// **面 B 登记表**：`AttachChangeHandler&lt;T&gt;` 里那两条按名找不到的接线 ——
    /// <c>nud</c>（`NumericUpDown.ValueChanged`）与 **<c>fanumber</c>**
    /// （`fa:FANumberBox.ValueChanged`；后者是阶段 195 修 `#90` 时补的：本视图的 16 个数字框
    /// **全是** FANumberBox，而它**不继承** NumericUpDown ⇒ 原先一个都不标脏）。
    /// 本面用 `NumPwdMinLen` **代表驱动**这两条（见 <see cref="PasswordMinLength_MarksDirty_AndOkPersistsIt"/>）。
    /// </summary>
    private static readonly string[] AnonymousWired = { "nud", "fanumber" };

    /// <summary>
    /// **「没有（直接）接线」登记表**：这 15 个框由 `SaveToProject()` 在点「确定/应用」时
    /// 一次性读取 —— 没有直接的 `ValueChanged` 接线，不是缺口。
    /// </summary>
    private static readonly Dictionary<string, string> WithoutWiring = new(StringComparer.Ordinal)
    {
        ["NumScreenWidth"] = "屏幕尺寸：由 `SaveToProject()` 一次性读取（点确定/应用），没有直接 ValueChanged 接线",
        ["NumScreenHeight"] = "同上（屏幕尺寸）",
        ["NumDpiScale"] = "同上（DPI 缩放）",
        ["NumCommScanInterval"] = "同上（通讯扫描间隔）",
        ["NumCommTimeout"] = "同上（通讯超时）",
        ["NumRetryCount"] = "同上（重试次数）",
        ["NumRetryBackoff"] = "同上（重试退避）",
        ["NumRuntimeScan"] = "同上（运行期扫描间隔）",
        ["NumWatchdog"] = "同上（看门狗超时）",
        ["NumHeartbeat"] = "同上（心跳间隔）",
        ["NumCrashSector"] = "同上（崩溃转储扇区）",
        ["NumMaxTasks"] = "同上（最大任务数）",
        ["NumSessionTimeout"] = "同上（会话超时）",
        ["NumMaxAttempts"] = "同上（最大登录尝试）",
        ["NumLockout"] = "同上（锁定时长）",
    };

    [Fact]
    public void PasswordMinLength_MarksDirty_AndOkPersistsIt()
    {
        using var sandbox = new DataRootSandbox("projectsettings-numberbox");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);
            var project = new HmiProject { Name = "夹具工程" };
            project.Screens.Add(ScreenFactory.Create(project, name: "主画面"));
            project.SecurityConfig.PasswordMinLength = 8;   // 起点（与手势要写的 12 不同）

            var view = new ProjectSettingsView(project);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                // 前提自检：回填的是**工程里的**值
                Assert.Equal(8d, ControlWiringFace.NumberValue(view, "NumPwdMinLen"));

                ControlWiringFace.WriteNumber(view, "NumPwdMinLen", 12);

                // ── 面 A：提示占位文案跟着变（只判"含那个数"，不读整句）──
                var hint = ControlWiringFace.Find<TextBox>(view, "TxtRuntimeUserPassword").PlaceholderText ?? "";
                Assert.True(hint.Contains("12", StringComparison.Ordinal),
                    $"口令提示里应当出现新的下限 12，实际：'{hint}'");

                // 保存前不落盘（这一族只标脏 ＋ 更新提示，真正写工程在 `SaveToProject`）
                Assert.Equal(8, project.SecurityConfig.PasswordMinLength);

                // ── 面 B（nud）代表见证：点「确定」⇒ `_hasChanges` 为真 ⇒ 落盘 ──
                ControlWiringFace.Find<Button>(view, "BtnOk")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                UiTestHost.Pump(2);

                Assert.Equal(12, project.SecurityConfig.PasswordMinLength);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **单文件双向差集**：`NumPwdMinLen` 有驱动条目；15 个走"保存"的框在登记表里；
    /// 辅助函数注入的那条（面 B 的 `nud`）单独认账。
    /// </summary>
    [Fact]
    public void EveryNumberBoxAndWiring_IsAccountedFor()
    {
        ControlWiringFace.AssertFullyDrivenNumberBoxes(
            "ProjectSettingsView.axaml", Driven, scanFloor: 1, WithoutWiring, AnonymousWired);
    }
}
