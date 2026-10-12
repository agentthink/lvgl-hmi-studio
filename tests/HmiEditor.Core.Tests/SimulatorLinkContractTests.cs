using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **链接契约**：生成代码引用的每个"宿主符号"（<c>extern</c> 函数声明），
/// 导出工程里都必须有定义 —— 否则 <c>cmake --build</c> 到链接期才炸
/// （<c>undefined reference to 'hmi_show_alarms'</c>），而**所有单元测试都是绿的**。
///
/// <para><b>为什么是穷举而不是逐个列名字：</b>这类缺陷的形态是"新加了一个宿主 API 的调用点，
/// 忘了给宿主补实现"。逐个符号写断言等于把清单抄进测试 —— 新增符号时测试不会自己长出来。
/// 这里改成从**生成代码反推它要求宿主提供什么**，于是新符号天然被纳入。</para>
///
/// <para>与 <c>SimulatorLoopTests.Export_MainC_DefinesScreenSwitchRuntime</c> 的关系：
/// 那条是这一条的前身（只盯 <c>hmi_screen_switch</c> 一个符号），保留它不重复——
/// 它同时钉住了实现细节（按名称查 <c>screen_names[]</c>），本类只钉"有没有定义"。</para>
/// </summary>
public class SimulatorLinkContractTests
{
    // ── 判据 ──

    /// <summary>生成代码里的 <c>extern</c> **函数**声明名（变量 extern 不在此列，见类注释）。</summary>
    private static List<string> DeclaredExternFunctions(string code)
    {
        var rx = new Regex(@"^[ \t]*extern\b[^;{}()]*?\b([A-Za-z_]\w*)[ \t]*\(", RegexOptions.Multiline);
        return rx.Matches(code).Select(m => m.Groups[1].Value).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    /// <summary>该源码里是否存在 <paramref name="name"/> 的**定义**（带函数体，而非又一条声明）。</summary>
    private static bool Defines(string source, string name)
    {
        var rx = new Regex(
            @"^[ \t]*(?:[A-Za-z_]\w*[ \t]+|\*[ \t]*)+" + Regex.Escape(name) + @"[ \t]*\([^;{}]*\)[ \t\r\n]*\{",
            RegexOptions.Multiline);
        return rx.IsMatch(source);
    }

    // ── 语料 ──

    /// <summary>
    /// 一个按钮一个动作，**覆盖 <see cref="ActionTypeMeta"/> 的全部类型**；
    /// 运行模式给 <see cref="OperationMode.Operator"/> 以便把权限门也发射出来
    /// （门调用 <c>hmi_require_permission</c>，同样是宿主符号）。
    /// </summary>
    private static HmiProject BuildProjectUsingEveryAction()
    {
        var project = new HmiProject { Name = "LinkContract", LvglVersion = "9.2" };
        project.SecurityConfig.OperationMode = OperationMode.Operator;

        var conn = new DeviceConnection { Id = "conn1", Name = "PLC1", DriverId = "modbus-tcp" };
        project.VariableManager.Connections.Add(conn);
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v_motor", Name = "motor", Address = "40002",
            DataType = "Bool", ConnectionId = "conn1"
        });

        var main = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(main);
        // 三张"导航目标屏"：让 show_alarms/show_trends/login 都有真实落点
        project.Screens.Add(new HmiScreen { Id = "s2", Name = "报警列表", Width = 800, Height = 480 });
        project.Screens.Add(new HmiScreen { Id = "s3", Name = "趋势图", Width = 800, Height = 480 });
        project.Screens.Add(new HmiScreen { Id = "s4", Name = "登录屏", Width = 800, Height = 480 });

        var actions = new HmiAction[]
        {
            HmiAction.ToggleBit("v_motor"),
            HmiAction.SetBit("v_motor"),
            HmiAction.ResetBit("v_motor"),
            HmiAction.MomentaryOn("v_motor"),
            HmiAction.MomentaryOff("v_motor"),
            HmiAction.SetVariable("v_motor", "1"),
            HmiAction.SwitchScreen("Main"),
            HmiAction.ExecuteScript("script1"),
            HmiAction.ShowAlarms,
            HmiAction.ShowTrends,
            HmiAction.Login,
            HmiAction.Logout,
        };

        for (var i = 0; i < actions.Length; i++)
        {
            var btn = new HmiControl
            {
                Id = $"c{i}", Name = $"Btn{i}", Type = HmiControlType.Button,
                VariableBindingId = "v_motor", BindingMode = "ReadWrite",
            };
            btn.Actions.Add(actions[i]);
            main.Controls.Add(btn);
        }

        return project;
    }

    // ── 断言 ──

    [Fact]
    public void GeneratedModule_ExternFunctions_AllHaveDefinitionsInExportedProject()
    {
        var project = BuildProjectUsingEveryAction();
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = TestTempDir.NewDir("link-contract");
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);

            var generated = File.ReadAllText(Path.Combine(dir, "generated_hmi.c"));
            var declared = DeclaredExternFunctions(generated);

            // 扫描面自检：语料若没覆盖到，这里会先失败，而不是"没缺定义"的假绿
            Assert.True(declared.Count >= 6,
                $"只解析出 {declared.Count} 个 extern 函数声明（{string.Join(", ", declared)}）—— 语料或正则失效");

            // 宿主源 = 导出工程里除生成模块本身以外的所有 .c
            var hostSources = Directory.GetFiles(dir, "*.c")
                .Where(f => Path.GetFileName(f) != "generated_hmi.c")
                .ToDictionary(f => Path.GetFileName(f), File.ReadAllText);

            Assert.NotEmpty(hostSources);

            // 判据自检：已知存在的符号必须被判为"有定义"，杜撰的必须判为"无"
            Assert.Contains(hostSources, kv => Defines(kv.Value, "hmi_screen_switch"));
            Assert.DoesNotContain(hostSources, kv => Defines(kv.Value, "no_such_symbol_zzz"));

            // 扫描面自检（具体符号）：凭据提交必须真的被穷举扫进来 —— 它是这条守卫最有价值的
            // 捕获对象（新加了一个宿主 API 调用点却忘了给模拟器补定义 ⇒ 只在 cmake 链接期炸）。
            // 单断一条，避免"正则与语料同时退化 ⇒ 看着全绿其实什么都没查"。
            Assert.Contains("hmi_login_submit", declared);

            var missing = declared
                .Where(name => !hostSources.Any(kv => Defines(kv.Value, name)))
                .ToList();

            Assert.True(missing.Count == 0,
                "生成代码引用了这些宿主符号，但导出工程里没有定义 —— 导出的模拟器工程会在**链接期**失败"
                + "（单元测试全绿也挡不住）：" + Environment.NewLine
                + "  " + string.Join(Environment.NewLine + "  ", missing));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    /// <summary>
    /// 权限门引用的宿主符号不能在 <see cref="OperationMode.Designer"/>（默认）下出现 ——
    /// 否则等于给"默认零变化"开了个后门：设计模式不发射门，就不该依赖 <c>hmi_require_permission</c>。
    /// </summary>
    [Fact]
    public void DesignerProject_DoesNotDependOnPermissionHostSymbol()
    {
        var project = BuildProjectUsingEveryAction();
        project.SecurityConfig.OperationMode = OperationMode.Designer;
        var code = new CodeGenerator().GenerateSimulatorModule(project);

        var declared = DeclaredExternFunctions(code);
        Assert.NotEmpty(declared);   // 扫描面自检
        Assert.Contains("hmi_screen_switch", declared);
        // 前置声明本身可以存在（导出工程也提供了定义），但**不得有调用点**
        Assert.DoesNotContain("if (!hmi_require_permission", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// 标签存储的长度必须与生成代码里的 <c>TAG_COUNT</c> 同源。
    ///
    /// <para>两块表各自算长度就必然漂移：<c>var_raw_read(idx)</c> 拿到的 idx 来自
    /// 生成代码的 <c>TAG_xxx</c> 枚举（长度 = <c>TAG_COUNT</c>），而存储是宿主给的。
    /// 宿主偏小 ⇒ <b>越界读</b>被静默吞掉（读回 0.0f，表现为"变量值永远是 0"）；
    /// 宿主偏大 ⇒ 只是浪费内存。所以这里钉的是"相等"，不是"不小于"。</para>
    /// </summary>
    [Fact]
    public void SimulatorTagStore_SizeMatchesGeneratedTagCount()
    {
        // 复刻生产接线：CodeGenerator 内部自己建标签（= RuntimeBindingEngine.Tags），
        // SimulatorBuildService 把**同一份**列表传给 Export。
        var project = new HmiProject { Name = "TagStore", LvglVersion = "9.2" };
        var main = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(main);

        var ids = new[] { "v_a", "v_b", "v_c" };
        for (var i = 0; i < ids.Length; i++)
        {
            project.VariableManager.Variables.Add(new Variable
            {
                Id = ids[i], Name = ids[i], Address = $"4000{i + 1}", DataType = "Float"
            });
            var label = new HmiControl { Id = $"c{i}", Name = $"L{i}", Type = HmiControlType.Label };
            label.VariableBindings.Add(new VariableBindingData
            {
                VariableId = ids[i], Direction = BindingDirection.ReadOnly, PropertyName = "Text",
            });
            main.Controls.Add(label);
        }

        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();
        Assert.Equal(3, engine.Tags.Count);   // 语料自检：否则"两边都退化成 1"也会绿

        var dir = TestTempDir.NewDir("tag-store");
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assetLibrary: null, tags: engine.Tags);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));
            var generated = File.ReadAllText(Path.Combine(dir, "generated_hmi.c"));

            var generatedTagCount = ParseTagCountFromGeneratedModule(generated);
            Assert.Equal(3, generatedTagCount);   // 解析面自检

            var m = Regex.Match(mainC, @"#define\s+SIM_TAG_COUNT\s+(\d+)");
            Assert.True(m.Success, "main.c 里没有 SIM_TAG_COUNT 定义（宿主标签存储不存在）");
            Assert.Equal(generatedTagCount, int.Parse(m.Groups[1].Value));

            CSource.AssertCode(mainC, "static float s_tag_raw[SIM_TAG_COUNT];");
            // 两个访问函数都按同一个宏设界
            CSource.AssertCode(mainC, "if (index < 0 || index >= SIM_TAG_COUNT) return 0.0f;");
            CSource.AssertCode(mainC, "if (index < 0 || index >= SIM_TAG_COUNT) return;");
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    /// <summary>
    /// 没有绑定变量时，标签存储兜底长度为 <b>1</b> —— 长度 0 的数组在 C 里不合法。
    /// 此时生成代码不发 <c>tag_index_t</c>（<c>// Runtime tags: 0</c>），两边的契约退化成"都用不上"。
    /// </summary>
    [Fact]
    public void SimulatorTagStore_IsNeverZeroLength()
    {
        var project = new HmiProject { Name = "TagStoreEmpty", LvglVersion = "9.2" };
        var main = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        main.Controls.Add(new HmiControl { Id = "c1", Name = "B", Type = HmiControlType.Button });
        project.Screens.Add(main);

        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = TestTempDir.NewDir("tag-store-empty");
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            CSource.AssertCode(mainC, "#define SIM_TAG_COUNT 1");
            CSource.AssertCode(mainC, "static float s_tag_raw[SIM_TAG_COUNT];");
            Assert.DoesNotContain("s_tag_raw[0]", mainC);   // 不是靠"顺手给个元素"绕过去的

            var generated = File.ReadAllText(Path.Combine(dir, "generated_hmi.c"));
            Assert.DoesNotContain("tag_index_t", generated);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── 辅助 ──

    /// <summary>
    /// 从生成模块的 <c>typedef enum { TAG_a = 0, …, TAG_COUNT } tag_index_t;</c>
    /// 解析出标签数（成员数 − 1，末位 <c>TAG_COUNT</c> 即总数）。
    /// </summary>
    private static int ParseTagCountFromGeneratedModule(string generated)
    {
        var block = Regex.Match(generated,
            @"typedef enum\s*\{(?<body>[^}]*)\}\s*tag_index_t\s*;", RegexOptions.Singleline);
        Assert.True(block.Success, "generated_hmi.c 里没有 tag_index_t 定义 —— 语料或解析失效");

        var members = Regex.Matches(block.Groups["body"].Value, @"\bTAG_[A-Za-z0-9_]*\b")
            .Select(x => x.Value).ToList();
        // 扫描面自检：拿到的是索引枚举（末位固定是总数哨兵），不是别的 TAG_* 列表
        Assert.Equal("TAG_COUNT", members[^1]);
        return members.Count - 1;
    }
}
