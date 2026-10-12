using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.App.Resources;
using HmiEditor.App.Views;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 报警类型/级别下拉的「本地化键 ↔ 枚举」配对守卫。
///
/// <para><b>为什么需要它</b>：下拉里 <b>标签</b>与 <b>Tag</b>（写回模型的枚举）是两个必须对齐的
/// 事实。把标签写串了 —— 例如 <c>HighLimit</c> 配上"下限报警" —— <b>没有任何东西会报</b>：
/// 下拉项安静地显示错的类型名，而 Tag 仍然正确，所以连保存下来的数据都是对的，
/// 事后翻数据也看不出来。本地化改造把"手写文案"换成"键"时，这类串位正好是最容易犯的错。</para>
///
/// <para>三条断言各管一件事：① <b>覆盖面</b> —— 枚举新增了成员却忘了加下拉项（用户无法选到）；
/// ② <b>配对</b> —— 键与枚举的对应关系（写串了）；③ <b>可解析</b> —— 键真的在字典里，
/// 否则用户看到的是键名。</para>
/// </summary>
public class AlarmOptionLocalizationTests
{
    private static LocalizationService Loc()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();
        return svc;
    }

    [Fact]
    public void AlarmTypeOptions_CoverEveryEnumMemberExactlyOnce()
    {
        var covered = VariableManagerView.AlarmTypeOptions.Select(o => o.Type).ToList();
        var all = Enum.GetValues<AlarmConditionType>();

        Assert.Equal(all.OrderBy(t => t), covered.OrderBy(t => t));
        Assert.Equal(covered.Count, covered.Distinct().Count());   // 无重复项
    }

    [Fact]
    public void AlarmSeverityOptions_CoverEveryEnumMemberExactlyOnce()
    {
        var covered = VariableManagerView.AlarmSeverityOptions.Select(o => o.Severity).ToList();
        var all = Enum.GetValues<AlarmSeverity>();

        Assert.Equal(all.OrderBy(s => s), covered.OrderBy(s => s));
        Assert.Equal(covered.Count, covered.Distinct().Count());
    }

    [Fact]
    public void AlarmTypeOptions_PairTheRightLabelWithTheRightEnum()
    {
        // 期望的语义配对（人写的第二份副本 —— 故意如此：它抓的正是"抄键时抄串了"）
        var expected = new Dictionary<AlarmConditionType, string>
        {
            [AlarmConditionType.HighLimit] = Lang.VarAlarmTypeHigh,
            [AlarmConditionType.LowLimit] = Lang.VarAlarmTypeLow,
            [AlarmConditionType.HighHighLimit] = Lang.VarAlarmTypeHighHigh,
            [AlarmConditionType.LowLowLimit] = Lang.VarAlarmTypeLowLow,
            [AlarmConditionType.BitTrigger] = Lang.VarAlarmTypeBit,
            [AlarmConditionType.Deviation] = Lang.VarAlarmTypeDeviation,
            [AlarmConditionType.StateChange] = Lang.VarAlarmTypeStateChange,
            [AlarmConditionType.RateOfChange] = Lang.VarAlarmTypeRateOfChange,
        };

        foreach (var (labelKey, type) in VariableManagerView.AlarmTypeOptions)
        {
            Assert.True(expected.TryGetValue(type, out var want), $"{type} 没有期望配对");
            Assert.Equal(want, labelKey);
        }
    }

    [Fact]
    public void AlarmSeverityOptions_PairTheRightLabelWithTheRightEnum()
    {
        var expected = new Dictionary<AlarmSeverity, string>
        {
            [AlarmSeverity.Info] = Lang.VarAlarmSeverityInfo,
            [AlarmSeverity.Warning] = Lang.VarAlarmSeverityWarning,
            [AlarmSeverity.Critical] = Lang.VarAlarmSeverityCritical,
            [AlarmSeverity.Emergency] = Lang.VarAlarmSeverityEmergency,
        };

        foreach (var (labelKey, severity) in VariableManagerView.AlarmSeverityOptions)
        {
            Assert.True(expected.TryGetValue(severity, out var want), $"{severity} 没有期望配对");
            Assert.Equal(want, labelKey);
        }
    }

    /// <summary>
    /// 下拉项的**标签**必须由字典解析出来 —— 这一条补的是上面几条的盲区。
    ///
    /// <para>上面几条断的是**配对表**（<c>AlarmTypeOptions[].LabelKey</c>）；它们**不会**发现
    /// "把键直接当标签用"（<c>(o.LabelKey, …)</c>）这种写法 —— 那样用户会在下拉里看到
    /// <c>var.alarm.type.high</c>。这条断言让"标签 == 字典里的值"且"标签 != 键"。</para>
    /// </summary>
    [Fact]
    public void DropdownItems_ResolveLabelsThroughTheDictionary()
    {
        var svc = Loc();

        var typeItems = VariableManagerView.BuildAlarmTypeItems().ToList();
        Assert.Equal(VariableManagerView.AlarmTypeOptions.Length, typeItems.Count);
        for (var i = 0; i < typeItems.Count; i++)
        {
            var key = VariableManagerView.AlarmTypeOptions[i].LabelKey;
            Assert.Equal(svc.GetString(key, "zh-CN"), typeItems[i].Label);
            Assert.NotEqual(key, typeItems[i].Label);   // 直接把键当标签用会被这条抓到
        }
        Assert.Equal(typeItems.Count, typeItems.Select(i => i.Label).Distinct().Count());

        var sevItems = VariableManagerView.BuildAlarmSeverityItems().ToList();
        Assert.Equal(VariableManagerView.AlarmSeverityOptions.Length, sevItems.Count);
        for (var i = 0; i < sevItems.Count; i++)
        {
            var key = VariableManagerView.AlarmSeverityOptions[i].LabelKey;
            Assert.Equal(svc.GetString(key, "zh-CN"), sevItems[i].Label);
            Assert.NotEqual(key, sevItems[i].Label);
        }
        Assert.Equal(sevItems.Count, sevItems.Select(i => i.Label).Distinct().Count());
    }

    [Fact]
    public void EveryOptionLabel_ResolvesInBothLanguages_AndIsDistinct()
    {
        var svc = Loc();
        var keys = VariableManagerView.AlarmTypeOptions.Select(o => o.LabelKey)
            .Concat(VariableManagerView.AlarmSeverityOptions.Select(o => o.LabelKey))
            .ToList();

        foreach (var key in keys)
        {
            foreach (var lang in new[] { "zh-CN", "en-US" })
            {
                // 索引器找不到键时回退成键名 —— 用它判定"字典里有这一条"
                Assert.NotEqual(key, svc.GetString(key, lang));
            }
        }

        // 两个不同的类型/级别显示成同一句话 ⇒ 用户没法区分（写串键或复制粘贴忘改）
        foreach (var group in new[]
                 {
                     VariableManagerView.AlarmTypeOptions.Select(o => o.LabelKey),
                     VariableManagerView.AlarmSeverityOptions.Select(o => o.LabelKey),
                 })
        {
            var labels = group.Select(k => svc.GetString(k, "zh-CN")).ToList();
            Assert.Equal(labels.Count, labels.Distinct().Count());
        }
    }
}
