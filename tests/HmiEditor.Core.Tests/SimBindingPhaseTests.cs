using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 模拟正弦相位（<see cref="SimBinding.PhaseFor"/>）的**可复现性**守卫。
///
/// <para><b>为什么这个测试类存在</b>：相位原本由
/// <c>Math.Abs(key.GetHashCode()) % 360</c> 推导，而 .NET 的字符串哈希**每进程随机加盐**
/// → 同一份源码每次导出，<c>sim_refresh_bindings</c> 的相位都不同（实测同一份源码连跑两次，
/// 20 行相位值各异）。后果是导出产物**不满足「同源同物」**：可复现构建、产物哈希比对、
/// 基于 diff 的回归门禁全部失效 —— 本次重构的产物逐字节比对就是被它拖住的。</para>
///
/// <para><b>本类怎么守</b>：用**黄金值**（golden values）钉死若干输入的输出。
/// 黄金值是跨进程稳定的常量，所以只要有人把实现换回任何依赖运行时的哈希
/// （<c>string.GetHashCode</c>、<c>RuntimeHelpers.GetHashCode</c>、随机种子…），
/// 这里立刻变红 —— 而这正是「同进程内调两次」这类断言抓不到的（同一进程内盐相同）。</para>
///
/// <para>跨进程一致性的端到端证据在测试套件之外：用产物 dump harness 在两个独立进程里
/// 各跑一遍全入口语料，<c>manifest.txt</c>（sha256 清单）必须逐字节一致。见
/// <c>docs/2026-09-14-sim-phase-reproducibility.md</c>。</para>
/// </summary>
public class SimBindingPhaseTests
{
    // ───────────────────── ① 确定性：黄金值 ─────────────────────

    /// <summary>
    /// 相位是**跨进程稳定**的常量。改动 <see cref="SimBinding.PhaseFor"/> 的算法会改动这些值
    /// —— 那是有意为之的行为变更，必须同步更新本表；但**绝不能**出现"每次运行都不一样"。
    /// </summary>
    [Theory]
    [InlineData("lbl", 315)]
    [InlineData("bar", 216)]
    [InlineData("sld", 342)]
    [InlineData("arc", 101)]
    [InlineData("led", 31)]
    [InlineData("sw", 245)]
    [InlineData("cb", 125)]
    [InlineData("c0", 311)]
    [InlineData("c1", 112)]
    [InlineData("c2", 133)]
    [InlineData("a8f3c1d2", 277)]          // 设计器自动生成的 8 位十六进制 id 形态
    [InlineData("温度", 45)]                 // 中文标识（UTF-16LE 上界字节参与散列）
    [InlineData("罐体A", 142)]
    [InlineData("tbl#0:0", 4)]
    [InlineData("tbl#0:1", 239)]
    [InlineData("tbl#1:0", 13)]
    [InlineData("tbl#2:2", 5)]
    [InlineData("", 256)]
    public void PhaseFor_IsStableAcrossProcesses_GoldenValues(string key, int expected)
    {
        Assert.Equal(expected, SimBinding.PhaseFor(key));
    }

    /// <summary>null 按空输入处理（不抛异常），且与空串同值 —— 与旧实现的行为边界对齐。</summary>
    [Fact]
    public void PhaseFor_NullAndEmpty_TreatedAsEmptyInput()
    {
        Assert.Equal(SimBinding.PhaseFor(string.Empty), SimBinding.PhaseFor(null));
        Assert.InRange(SimBinding.PhaseFor(null), 0, SimBinding.PhaseDegrees - 1);
    }

    // ───────────────────── ② 取值域 ─────────────────────

    [Fact]
    public void PhaseFor_AlwaysWithinZeroTo359()
    {
        var probes = new List<string> { string.Empty };
        probes.AddRange(Enumerable.Range(0, 500).Select(i => $"c{i}"));
        probes.AddRange(Enumerable.Range(0, 100).Select(i => $"控件{i}"));
        probes.Add(new string('x', 512));
        probes.Add("with space");
        probes.Add("tab\tand\nnewline");
        probes.Add("snowman ☃ emoji 🚀");

        foreach (var key in probes)
            Assert.InRange(SimBinding.PhaseFor(key), 0, SimBinding.PhaseDegrees - 1);
    }

    // ───────────────────── ③ 分布质量：防"退回等差规律" ─────────────────────

    /// <summary>
    /// 相位存在的意义是让同屏多个绑定的模拟曲线**互相错开**。若哈希对「只有末字节不同」
    /// 的短串退化成等差（裸 FNV-1a 就是这样：<c>c0/c2/c4/c6</c> → 6/12/18/24，
    /// 相邻相位差的取值只有 19/50 种），曲线会呈现机械的同步骤动 —— 一眼假。
    /// 本测试把"离散度"钉住。
    /// </summary>
    [Fact]
    public void PhaseFor_AdjacentKeysProduceUncorrelatedPhases()
    {
        var phases = Enumerable.Range(0, 50).Select(i => SimBinding.PhaseFor($"c{i}")).ToList();
        var deltas = Enumerable.Range(0, phases.Count - 1)
            .Select(i => ((phases[i + 1] - phases[i]) % 360 + 360) % 360)
            .ToList();

        // 相邻差几乎全不同 → 没有等差规律（实测收尾混淆后 47/50；裸 FNV-1a 只有 19/50）
        Assert.True(deltas.Distinct().Count() >= 40,
            $"相邻相位差只有 {deltas.Distinct().Count()}/49 种 —— 相位呈等差规律，模拟曲线会同步骤动");
    }

    [Fact]
    public void PhaseFor_LargeKeySetCoversMostOfTheRange()
    {
        var distinct = Enumerable.Range(0, 1000)
            .Select(i => SimBinding.PhaseFor($"c{i}"))
            .Distinct().Count();

        // 1000 个 id 覆盖 340/360（实测）；留出余量防轻微退化
        Assert.True(distinct >= 330, $"1000 个 id 只覆盖 {distinct}/360 个相位值");
    }

    // ───────────────────── ④ 单元格坐标参与散列 ─────────────────────

    [Fact]
    public void PhaseForCell_DistinguishesRowAndColumn()
    {
        var seen = new List<int>();
        for (var r = 0; r < 3; r++)
            for (var c = 0; c < 3; c++)
                seen.Add(SimBinding.PhaseForCell("tbl", r, c));

        Assert.Equal(9, seen.Distinct().Count());
    }

    /// <summary>
    /// 键用分隔符拼接而非数值相加：旧实现把 <c>id + row*31 + col*17</c> 拼成裸串，
    /// <c>("a1", 0, 1)</c> 与 <c>("a", 1, 1)</c> 会拼成同一个串 → 相位碰撞。
    /// </summary>
    [Fact]
    public void PhaseForCell_NoAmbiguousConcatenationCollisions()
    {
        Assert.NotEqual(SimBinding.PhaseForCell("a1", 0, 1), SimBinding.PhaseForCell("a", 1, 1));
        Assert.NotEqual(SimBinding.PhaseForCell("a", 0, 1), SimBinding.PhaseForCell("a0", 0, 1));
        Assert.NotEqual(SimBinding.PhaseForCell("tbl", 1, 11), SimBinding.PhaseForCell("tbl", 11, 1));
    }

    [Fact]
    public void PhaseForCell_DiffersFromBareControlPhase()
    {
        // 同一个控件的"整体相位"与它的单元格相位不应重合（两者含义不同）
        Assert.NotEqual(SimBinding.PhaseFor("tbl"), SimBinding.PhaseForCell("tbl", 0, 0));
    }

    // ───────────────────── ⑤ 端到端：产物里的相位就是推导出的常量 ─────────────────────

    /// <summary>
    /// 把「唯一实现 → 生成产物」串起来：模拟器模块里发出的相位字面量必须等于
    /// <see cref="SimBinding.PhaseFor"/> 对同 id 的结果。这样相位一旦不可复现，
    /// 产物断言会跟着红，而不是只红在工具类上。
    /// </summary>
    [Fact]
    public void SimulatorModule_EmitsPhaseDerivedFromStableHash()
    {
        var project = BoundProject();
        var sim = new CodeGenerator().GenerateSimulatorModule(project, null);

        // lbl 的黄金相位 = 315（与上面 Theory 同源）
        Assert.Equal(315, SimBinding.PhaseFor("lbl"));
        CSource.AssertCode(sim, "sinf(t + 315.0f * 0.017453f)");
    }

    [Fact]
    public void TableCell_EmitsPhaseDerivedFromStableHash()
    {
        var project = BoundProject();
        var sim = new CodeGenerator().GenerateSimulatorModule(project, null);

        // 单元格 ("tbl", 0, 0) 的黄金相位 = 4（与 PhaseForCell_DistinguishesRowAndColumn 同源）
        Assert.Equal(4, SimBinding.PhaseForCell("tbl", 0, 0));
        CSource.AssertCode(sim, "sinf(t + 4.0f * 0.017453f)");
    }

    /// <summary>同一项目连续生成两次必须逐字节一致 —— 去掉掩码后的直接验收。</summary>
    [Fact]
    public void SimulatorModule_IsByteIdenticalAcrossGenerations()
    {
        var first = new CodeGenerator().GenerateSimulatorModule(BoundProject(), null);
        var second = new CodeGenerator().GenerateSimulatorModule(BoundProject(), null);

        // 两个 BoundProject() 各自 new 了 id 字面量相同的控件，产物理应完全一致
        Assert.Equal(first, second);
    }

    // ───────────────────── fixture ─────────────────────

    private static HmiProject BoundProject()
    {
        var project = new HmiProject { Name = "PhaseProbe", LvglVersion = "9.5" };
        var screen = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(screen);

        var vm = project.VariableManager;
        vm.Connections.Add(new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" });
        vm.Variables.Add(new Variable
        {
            Id = "v1", Name = "温度", Address = "40001", DataType = "Float",
            ConnectionId = "c1", Unit = "℃",
        });

        void Bind(HmiControl c)
        {
            c.VariableBindings.Add(new VariableBindingData
            {
                VariableId = "v1", Direction = BindingDirection.ReadOnly, PropertyName = "Value",
            });
            screen.Controls.Add(c);
        }

        Bind(new HmiControl
        {
            Id = "lbl", Type = HmiControlType.Label, X = 0, Y = 0, Width = 100, Height = 30,
            MinValue = 0, MaxValue = 100,
        });
        Bind(new HmiControl
        {
            Id = "tbl", Type = HmiControlType.Table, X = 0, Y = 40, Width = 300, Height = 120,
            RowCount = 2, ColumnCount = 2,
            Cells = new List<TableCellData>
            {
                new() { Row = 0, Col = 0, VariableId = "v1", DecimalPlaces = 1 },
            },
        });
        return project;
    }
}
