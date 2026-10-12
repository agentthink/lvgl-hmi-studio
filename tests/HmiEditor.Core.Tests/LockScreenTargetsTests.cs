using System.Collections.Generic;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// <see cref="LockScreenTargets"/> —— 会话超时的锁屏目标**生成期解析**。
///
/// <para><b>为什么这个解析必须单独测：</b>它的输入是一个屏幕 <b>Id</b>、输出是一个屏幕 <b>名字</b>，
/// 而设备端 <c>hmi_screen_switch</c> 是按名字匹配的。如果解析退化成"把 Id 当名字用"，
/// 生成头里的宏在设备上永远匹配不到任何屏 —— 而且**失败是静默的**
/// （<c>hmi_screen_switch</c> 返回 -1、只记一行日志），现场表现只是"超时后屏幕不跳"。
/// 所以每条分支（命中 / 未配 / Id 不存在 / 没有登录屏 / 没有屏幕清单）都要有正反例。</para>
/// </summary>
public class LockScreenTargetsTests
{
    /// <summary>刻意让 Id 与名字**不同名** —— 否则"拿到 Id 却当名字用"这个 bug 会被静默放过。</summary>
    private static List<HmiScreen> Screens() =>
    [
        new HmiScreen { Id = "screen-main", Name = "Main" },
        new HmiScreen { Id = "screen-lock", Name = "锁屏" },
        new HmiScreen { Id = "screen-login", Name = "login" },
    ];

    [Fact]
    public void ConfiguredId_ResolvesToScreenName_NotTheIdItself()
    {
        var r = LockScreenTargets.Resolve(new SecurityConfig { LockScreenId = "screen-lock" }, Screens());

        Assert.True(r.FromConfiguredId);
        Assert.Null(r.Problem);
        Assert.Equal("锁屏", r.ScreenName);
        // 判据自检：解析结果**不得**是 Id 本身
        Assert.NotEqual("screen-lock", r.ScreenName);
    }

    [Fact]
    public void UnknownId_FallsBackToLoginScreen_AndReportsProblem()
    {
        var r = LockScreenTargets.Resolve(new SecurityConfig { LockScreenId = "screen-deleted" }, Screens());

        Assert.False(r.FromConfiguredId);
        Assert.Equal("login", r.ScreenName);              // 回退登录屏（配了却不存在，本意是"别停原画面"）
        Assert.NotNull(r.Problem);
        Assert.Contains("screen-deleted", r.Problem);     // 诊断要指名道姓，事故才可查
    }

    [Fact]
    public void NoId_FallsBackToLoginScreen_WithoutProblem()
    {
        var r = LockScreenTargets.Resolve(new SecurityConfig(), Screens());

        Assert.False(r.FromConfiguredId);
        Assert.Equal("login", r.ScreenName);
        Assert.Null(r.Problem);   // 不配 LockScreenId 是正常用法，不是"有问题"
    }

    [Fact]
    public void NoId_AndNoLoginScreen_YieldsNullWithProblem()
    {
        var screens = new List<HmiScreen> { new() { Id = "s1", Name = "Main" } };
        var r = LockScreenTargets.Resolve(new SecurityConfig(), screens);

        Assert.Null(r.ScreenName);     // 不切屏 —— 但设备端**仍然清会话**
        Assert.NotNull(r.Problem);
    }

    [Fact]
    public void UnknownId_AndNoLoginScreen_YieldsNullWithProblem()
    {
        var screens = new List<HmiScreen> { new() { Id = "s1", Name = "Main" } };
        var r = LockScreenTargets.Resolve(new SecurityConfig { LockScreenId = "nope" }, screens);

        Assert.Null(r.ScreenName);
        Assert.NotNull(r.Problem);
        Assert.Contains("nope", r.Problem);
    }

    [Fact]
    public void NullScreenList_YieldsNullWithProblem()
    {
        // Generate(config) 的简化重载走的就是这条：宁可设备端不切屏，也不要猜一个可能不存在的名字
        var r = LockScreenTargets.Resolve(new SecurityConfig { LockScreenId = "screen-lock" }, null);

        Assert.Null(r.ScreenName);
        Assert.NotNull(r.Problem);
    }

    [Fact]
    public void NullConfig_TreatedAsUnconfigured()
    {
        var r = LockScreenTargets.Resolve(null, Screens());

        Assert.False(r.FromConfiguredId);
        Assert.Equal("login", r.ScreenName);
    }

    /// <summary>
    /// 回退登录屏时按**候选表顺序**取，不是按屏幕顺序 —— 与固件 <c>try_switch</c> 一致。
    /// 两边不一致会导致"预览跳 A 屏、设备跳 B 屏"。
    /// </summary>
    [Fact]
    public void LoginFallback_FollowsCandidateOrder_NotScreenOrder()
    {
        var screens = new List<HmiScreen>
        {
            new() { Id = "b", Name = "登录屏" },   // 候选表里靠后，但屏幕顺序在前
            new() { Id = "a", Name = "login" },    // 候选表第一项
        };

        var r = LockScreenTargets.Resolve(new SecurityConfig(), screens);

        Assert.Equal("login", r.ScreenName);
    }
}
