using System;
using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models.Animation;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 缓动路径映射「单一来源」契约。
///
/// <para>历史：<c>lv_anim_path_*</c> 映射曾有<b>三份逐字相同</b>的 switch ——
/// <see cref="AnimationConfig.GetPathCCode"/>、<c>CodeGenerator</c> 的私有静态副本、
/// 动画编辑器预览（<c>AnimationEditorView</c>）的私有静态副本。任一处补值另两处不会跟着动，
/// 而三处都不会有任何编译告警。</para>
///
/// <para>现在唯一实现是 <see cref="AnimationPathHelper.GetPathCCode"/>，其余调用点一律委托。
/// 本类用<b>行为</b>（而不是扫源码 —— 本仓的约定是「源码扫描只用来守测试怎么写，
/// 产品代码一律行为测试」）把它钉住：枚举每加一个成员，
/// <see cref="ExpectationTable_CoversEveryEnumMember"/> 先红，
/// 逼作者同时更新期望表与实现。</para>
/// </summary>
public class AnimationPathSingleSourceTests
{
    /// <summary>测试侧期望表（oracle）—— 必须覆盖 <see cref="AnimationPathType"/> 的每一个成员。</summary>
    private static readonly (AnimationPathType Path, string CCode)[] Expectations =
    {
        (AnimationPathType.Linear, "lv_anim_path_linear"),
        (AnimationPathType.EaseInOut, "lv_anim_path_ease_in_out"),
        (AnimationPathType.EaseIn, "lv_anim_path_ease_in"),
        (AnimationPathType.EaseOut, "lv_anim_path_ease_out"),
        (AnimationPathType.Bounce, "lv_anim_path_bounce"),
        (AnimationPathType.Overshoot, "lv_anim_path_overshoot"),
        (AnimationPathType.Step, "lv_anim_path_step"),
    };

    public static TheoryData<AnimationPathType, string> PathCases()
    {
        var data = new TheoryData<AnimationPathType, string>();
        foreach (var (path, ccode) in Expectations)
            data.Add(path, ccode);
        return data;
    }

    [Fact]
    public void ExpectationTable_CoversEveryEnumMember()
    {
        // 期望表就是「枚举上限」：新增 AnimationPathType 成员却不更新它 → 本用例红。
        var declared = Enum.GetValues<AnimationPathType>();
        Assert.Equal(declared.Length, Expectations.Length);
        foreach (var v in declared)
            Assert.Contains(Expectations, e => e.Path == v);
    }

    [Fact]
    public void Names_AreDistinct()
    {
        // 复制粘贴式补值最容易出的错：两个成员映射到同一个宏。
        // 两侧都要断：
        //   ① oracle 自身 —— 期望表抄错时这条会红（否则错的是"标准答案"）；
        //   ② **产品侧** —— 唯一实现漂移成一个重复值时这条也会红。
        // （负向验证发现：只断 oracle 的话，产品漂移只能被 …Agree 的"期望不符"间接打到，
        //   这条断言本身对产品完全无感。）
        var oracle = Expectations.Select(e => e.CCode).ToArray();
        Assert.Equal(oracle.Length, oracle.Distinct().Count());

        var product = Enum.GetValues<AnimationPathType>()
            .Select(AnimationPathHelper.GetPathCCode).ToArray();
        Assert.Equal(product.Length, product.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(PathCases))]
    public void ModelApi_And_Helper_Agree(AnimationPathType path, string expected)
    {
        // 模型 API（对外契约）与唯一实现必须同值 —— 委托关系被钉住。
        Assert.Equal(expected, AnimationPathHelper.GetPathCCode(path));
        Assert.Equal(expected, new AnimationConfig { PathType = path }.GetPathCCode());
    }

    [Theory]
    [MemberData(nameof(PathCases))]
    public void EmitterPathCallback_UsesSameMapping(AnimationPathType path, string expected)
    {
        var code = new CodeGenerator().GenerateAnimationCode(new[] { Anim(path) });

        CSource.AssertCode(code, $"lv_anim_set_path_cb(&a, {expected});");
    }

    [Theory]
    [MemberData(nameof(PathCases))]
    public void KeyframeComment_UsesSameMapping(AnimationPathType path, string expected)
    {
        // 关键帧注释是**另一个**调用点（曾由 CodeGenerator 的私有副本提供服务）—— 一并钉住。
        var anim = Anim(AnimationPathType.Linear);
        anim.Keyframes.Add(new AnimationKeyframe { TimePercent = 50, Value = 128, PathType = path });

        var code = new CodeGenerator().GenerateAnimationCode(new[] { anim });

        CSource.AssertText(code, $"({expected})");
    }

    [Theory]
    [MemberData(nameof(PathCases))]
    public void KeyframeWithoutOwnPath_FallsBackToAnimationPath(AnimationPathType path, string expected)
    {
        var anim = Anim(path);
        anim.Keyframes.Add(new AnimationKeyframe { TimePercent = 50, Value = 128 });

        var code = new CodeGenerator().GenerateAnimationCode(new[] { anim });

        CSource.AssertText(code, $"({expected})");
    }

    [Fact]
    public void Helper_UndefinedEnumValue_FallsBackToEaseInOut()
    {
        // 兜底必须是合法 LVGL 符号（不是空串、不带空格）。
        Assert.Equal("lv_anim_path_ease_in_out", AnimationPathHelper.GetPathCCode((AnimationPathType)9999));
    }

    private static AnimationConfig Anim(AnimationPathType path) => new()
    {
        Id = "an1", Name = "淡入", ControlId = "c1",
        PropertyName = "Opacity", StartValue = 0, EndValue = 255,
        DurationMs = 500, DelayMs = 0, Trigger = "OnLoad", PathType = path,
    };
}
