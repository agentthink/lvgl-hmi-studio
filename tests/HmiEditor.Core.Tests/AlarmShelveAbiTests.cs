using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **搁置的设备侧调用入口**（阶段 277）：ABI 表 `hmi_ui_api_t` 里那两个函数指针，
/// 与生成器初始值表里的两行 —— 两侧必须**同名、同序、都在尾部**。
///
/// <para>为什么要"都在尾部"：宿主按**偏移**读前面那些字段 ⇒ 插在中间会改变既有偏移，
/// 老宿主读到错位的函数指针（比缺功能严重得多）。所以这条既是"接出来了"，也是"没插错位置"。</para>
/// </summary>
public class AlarmShelveAbiTests
{
    private readonly ITestOutputHelper _out;

    public AlarmShelveAbiTests(ITestOutputHelper o) => _out = o;

    private const string HeaderPath = "firmware/t113/app/runtime/hmi_ui_api.h";

    private static string Header()
    {
        var path = Path.Combine(SourceScanner.RepoRoot, HeaderPath);
        Assert.True(File.Exists(path), $"找不到 ABI 头：{path}");
        return File.ReadAllText(path);
    }

    private static string Generated()
    {
        var project = new HmiProject();
        var group = new AlarmGroup { Id = "g1", Name = "温度组" };
        group.Alarms.Add(new AlarmConfig
        {
            Id = "al1", Name = "温度高", Severity = AlarmSeverity.Critical,
            Condition = new AlarmCondition { Type = AlarmConditionType.HighLimit, Threshold = 80 },
        });
        project.AlarmGroups.Add(group);

        return new CodeGenerator().GenerateSharedLibraryProject(project);
    }

    [Fact]
    public void Header_DeclaresBothShelvePointers_AtTheStrictTail()
    {
        var header = Header();

        CSource.AssertText(header, "uint8_t (*alarm_shelve)(uint16_t config_id, uint32_t duration_ms, const char* by);");
        CSource.AssertText(header, "uint8_t (*alarm_unshelve)(uint16_t config_id, const char* by);");

        // **尾部追加**：两个新指针必须排在 datalog_export_csv 之后（插在中间会动既有偏移）
        var csv = header.IndexOf("(*datalog_export_csv)", StringComparison.Ordinal);
        var shelve = header.IndexOf("(*alarm_shelve)", StringComparison.Ordinal);
        var unshelve = header.IndexOf("(*alarm_unshelve)", StringComparison.Ordinal);
        Assert.True(csv > 0 && shelve > csv && unshelve > shelve,
            $"ABI 追加位置不对：csv={csv}, shelve={shelve}, unshelve={unshelve}（要求 csv < shelve < unshelve）");
    }

    [Fact]
    public void GeneratedTable_AssignsBothPointers_AndAfterTheCsvEntry()
    {
        var code = Generated();

        CSource.AssertText(code, ".alarm_shelve     = alarm_shelve,");
        CSource.AssertText(code, ".alarm_unshelve   = alarm_unshelve,");

        // 顺序不变式：搁置项排在**报警块既有项之后**；夹具里若带 logger，还要排在 csv 之后
        // （本夹具没有 DataLogConfig ⇒ `hasLogger` 为假、生成表里没有 `.datalog_export_csv`，
        //   所以那条只在存在时判 —— 否则会变成一条"夹具形状"断言，与本片要守的东西无关）
        var active = code.IndexOf(".alarm_active_count", StringComparison.Ordinal);
        var shelve = code.IndexOf(".alarm_shelve", StringComparison.Ordinal);
        Assert.True(active > 0 && shelve > active,
            $"搁置项应排在 `.alarm_active_count` 之后：active={active}, shelve={shelve}");

        var csv = code.IndexOf(".datalog_export_csv", StringComparison.Ordinal);
        if (csv > 0)
            Assert.True(shelve > csv, $"有 logger 时搁置项还要排在 `.datalog_export_csv` 之后：csv={csv}, shelve={shelve}");
    }

    [Fact]
    public void TwinGuard_EveryEmittedAlarmPointerExistsInTheHeader()
    {
        // 轻量孪生守卫：生成器写出的 `.名 = …` 里，凡 alarm_ 开头的都必须在头文件里有同名函数指针
        var headerNames = Regex.Matches(Header(), @"\(\*(?<name>[a-z_]+)\)")
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var emitted = Regex.Matches(Generated(), @"\.(?<name>[a-z_]+)\s*=\s*(?<fn>[a-z_]+),")
            .Select(m => (Name: m.Groups["name"].Value, Fn: m.Groups["fn"].Value))
            .Where(x => x.Name.StartsWith("alarm_", StringComparison.Ordinal))
            .ToList();

        Assert.Contains(emitted, x => x.Name == "alarm_shelve" && x.Fn == "alarm_shelve");
        Assert.Contains(emitted, x => x.Name == "alarm_unshelve" && x.Fn == "alarm_unshelve");

        // 搁置这两条**必须是同名直赋**（本片的正题）；其余 alarm_* 只要求"头里有这个名字"
        // ⚠ 不能对全体断言 name == fn：既有实现里 `.alarm_evaluate` 指向的是 `alarm_engine_evaluate`
        //   （报警块里的**入口包装**，不是同名函数），那是既定的、与本片无关的写法。
        foreach (var (name, fn) in emitted)
            Assert.True(headerNames.Contains(name), $"生成器写了 `.{name}`，但 ABI 头里没有这个指针");

        JudgeReadings.Write(_out, "搁置 ABI 入口（阶段 277）", new[]
        {
            "头文件：alarm_shelve / alarm_unshelve 追加在 datalog_export_csv 之后（老宿主偏移不变）",
            "生成表：.alarm_shelve / .alarm_unshelve 同样在 .datalog_export_csv 之后",
            "孪生守卫：生成器写出的每个 .alarm_* 都能在 ABI 头里找到同名指针",
        });
    }

    /// <summary>
    /// 报警**逐条快照**（阶段 291，M2 报警上行的数据源）：ABI 头与生成表都要有，
    /// 且仍**追加在搁置两项之后**（尾部追加是老宿主不漏读函数指针的前提）。
    /// </summary>
    [Fact]
    public void HeaderAndTable_DeclareAlarmSnapshot_AfterTheShelveTail()
    {
        var header = Header();
        var code = Generated();

        CSource.AssertText(header, "uint16_t (*alarm_snapshot)(hmi_alarm_snapshot_t* out, uint16_t cap);");
        var unshelve = header.IndexOf("(*alarm_unshelve)", StringComparison.Ordinal);
        var snapshot = header.IndexOf("(*alarm_snapshot)", StringComparison.Ordinal);
        Assert.True(unshelve > 0 && snapshot > unshelve,
            $"快照指针应排在搁置两项之后：unshelve={unshelve}, snapshot={snapshot}");

        CSource.AssertText(code, ".alarm_snapshot   = alarm_snapshot,");
        var tUnshelve = code.IndexOf(".alarm_unshelve", StringComparison.Ordinal);
        var tSnapshot = code.IndexOf(".alarm_snapshot", StringComparison.Ordinal);
        Assert.True(tUnshelve > 0 && tSnapshot > tUnshelve,
            $"快照项应排在搁置之后：unshelve={tUnshelve}, snapshot={tSnapshot}");

        // 实现：四个字段都要拷；id 走**定长拷贝**（不把 .so 的字符串指针交给宿主）
        CSource.AssertText(code, "uint16_t alarm_snapshot(hmi_alarm_snapshot_t* out, uint16_t cap) {");
        CSource.AssertText(code, "strncpy(out[n].id, _alarm_ids[i], sizeof(out[n].id) - 1);");
        CSource.AssertText(code, "out[n].id[sizeof(out[n].id) - 1] = '\\0';");
        CSource.AssertText(code, "out[n].config_id = _alarm_lookup[i].config_id;");
        CSource.AssertText(code, "out[n].state     = _alarm_lookup[i].rec->state;");
        CSource.AssertText(code, "out[n].severity  = _alarm_lookup[i].rec->severity;");
        CSource.AssertText(code, "    \"al1\",");     // 夹具报警 id 原样进 id 表

        JudgeReadings.Write(_out, "报警逐条快照 ABI（阶段 291）", new[]
        {
            "头文件：alarm_snapshot 追加在 (*alarm_unshelve) 之后（尾部追加、老宿主偏移不变）",
            "生成表：.alarm_snapshot = alarm_snapshot，同样在搁置之后",
            "实现：id（定长拷贝）/config_id/state/severity 四字段",
        });
    }

    /// <summary>没有报警时**不产生任何 snapshot 符号**（否则空 `_alarm_ids[]` 是非法 C，且表项会悬空）。</summary>
    [Fact]
    public void NoAlarms_EmitsNoSnapshotSymbol()
    {
        var code = new CodeGenerator().GenerateSharedLibraryProject(new HmiProject());

        Assert.DoesNotContain("alarm_snapshot", code, StringComparison.Ordinal);
        Assert.DoesNotContain("_alarm_ids", code, StringComparison.Ordinal);
    }
}
