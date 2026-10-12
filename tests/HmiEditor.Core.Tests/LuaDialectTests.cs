using System;
using System.IO;
using System.Linq;
using HmiEditor.Core.Services;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 方言对齐的判据。见 <c>docs/2026-09-20-lua-dialect-alignment.md</c>。
///
/// <para><b>本组的主题：把"编辑器说的"和"设备上真能跑的"分开记，并让两者的差异无法静默。</b>
/// 两个方向危害不对称 —— 假阳性（编辑器拒、设备能跑）用户会来抱怨；假阴性（编辑器放行、设备炸）
/// 用户在编辑器里**完全看不出来**。所以后者是本组重点。</para>
///
/// <para>本组不试图"修好"方言差异（目标版本未确定，改不了）—— 它做的是三件事：
/// ① 把基准钉死在一处（<see cref="LuaDialect.ParserBaseline"/>）；
/// ② 让所有面向用户的文案**从基准派生**，不许第二处写死；
/// ③ 把"目标一旦确定就必须做的对齐工作"做成会自己红的触发线。</para>
/// </summary>
public class LuaDialectTests
{
    // ── 基准：唯一一处允许出现字面量版本号的地方 ──

    /// <summary>
    /// 基准 = Lua 5.2，因为 MoonSharp 2.0.0 就实现到 5.2。**这是库的硬边界，不是产品选择。**
    ///
    /// <para>这条是本组**唯一**允许写死版本字面量的地方（其他测试都从
    /// <see cref="LuaDialect.ParserBaselineLabel"/> 取）—— 换解析器时才改这里，
    /// 而那时 <c>docs/2026-09-20-lua-syntax-validation.md</c> §3 的实测表要重测。</para>
    /// </summary>
    [Fact]
    public void ParserBaseline_IsLua52_BecauseThatIsWhereMoonSharpStops()
    {
        Assert.Equal(LuaVersion.Lua52, LuaDialect.ParserBaseline);
        Assert.Equal("Lua 5.2", LuaDialect.ParserBaselineLabel);
    }

    /// <summary>
    /// **所有方言文案必须由基准派生** —— 不许有第二处写死版本号。
    /// 反例就是本轮之前的样子：文案里 6 条全写死「Lua 5.2」，基准一变就两处走样。
    /// </summary>
    [Fact]
    public void Hints_AllDeriveFromBaselineLabel()
    {
        Assert.NotEmpty(LuaDialect.Hints);

        foreach (var h in LuaDialect.Hints)
        {
            Assert.Contains(LuaDialect.ParserBaselineLabel, h.Fallback);   // 基准版本
            Assert.Contains(LuaDialect.LabelOf(h.Version), h.Fallback);    // 该写法所属版本
            Assert.StartsWith("near ", h.Marker);
            // 每条都要带"若目标版本更高则应视为误报"的尾巴 —— 它被抽成常量正是因为
            // 散在 6 处必然有一处漏，而漏掉就等于把误报说成了真错。
            Assert.EndsWith(LuaDialect.HintTail, h.Fallback);
            // Code 必须落在语法错族里，否则界面按 script.issue.<code> 查不到键，
            // 会静默掉回中文回落（英文界面显示中文，正是本轮要治的病）。
            Assert.True(LuaSyntaxValidator.IsSyntaxError(h.Code), h.Code);
        }
    }

    // ── 事实表与探针不许漂移 ──

    /// <summary>
    /// 事实表里每条 <c>Example</c> 都必须**逐字**出现在
    /// <c>tools/probe-lua-dialect.lua</c> 里。
    ///
    /// <para>为什么值得一条判据：这张表是"设备侧事实"的载体，而事实的**唯一来源是那个探针**。
    /// 改表不改探针 = 表里出现一条从没被跑过的断言；改探针不改表 = 探针白跑。
    /// 两者必须一起动。</para>
    /// </summary>
    [Fact]
    public void FeatureExamples_AllAppearVerbatimInTheProbe()
    {
        var probe = Path.Combine(SourceScanner.RepoRoot, "tools", "probe-lua-dialect.lua");
        Assert.True(File.Exists(probe), $"探针脚本不存在：{probe}");
        var text = File.ReadAllText(probe);

        var missing = LuaDialect.Features
            .Where(f => !text.Contains(f.Example, StringComparison.Ordinal))
            .Select(f => $"{f.Name} ⇒ 「{f.Example}」")
            .ToList();

        Assert.True(missing.Count == 0,
            "以下写法的示例没有逐字出现在探针里（表与探针漂移了）：\n  " + string.Join("\n  ", missing));
    }

    // ── 假阴性：危险方向 ──

    /// <summary>
    /// SDK 自带的 Lua 是 **5.1.5**（Tina 的 <c>package/utils/lua</c>）。拿它当候选目标时，
    /// 假阴性**恰好是这 6 条** —— 与 <c>tools/probe-lua-dialect.lua</c> 的实测输出逐条对应。
    ///
    /// <para><b>这 6 条里有 5 条编辑器是看不见的</b>（<c>bit32</c> / <c>table.pack</c> /
    /// <c>table.unpack</c> / <c>math.type</c> / <c>utf8</c> 语法全合法 ⇒
    /// 真解析永远放行），只有 <c>goto</c> 是在语法层就分得出来的。</para>
    /// </summary>
    [Fact]
    public void FalseNegatives_ForSdkCandidate_AreExactlyTheSixMeasuredOnes()
    {
        var names = LuaDialect.FalseNegatives(LuaVersion.Lua51).Select(f => f.Name).ToArray();

        Assert.Equal(new[]
        {
            "goto / 标签",
            "bit32 库",
            "table.pack",
            "table.unpack",
            "math.type",
            "utf8 库",
        }, names);

        // 其中 5 条是"语法合法、库不存在" ⇒ 编辑器无论怎么对齐版本都看不见
        Assert.Equal(5, LuaDialect.FalseNegatives(LuaVersion.Lua51)
            .Count(f => f.Stage == LuaRejectStage.Runtime));
    }

    /// <summary>
    /// 反方向：目标 ≥ 5.3 时，5.3/5.4 的运算符会被编辑器拒而设备能跑（**已知误报**，
    /// 就是 <c>LuaDialect.Hints</c> 正在改写的那几条）。
    /// </summary>
    [Fact]
    public void FalsePositives_GrowWithTargetVersion_AsMeasured()
    {
        Assert.Equal(new[] { "整除运算符 //", "按位运算符 & | ~ << >>" },
            LuaDialect.FalsePositives(LuaVersion.Lua53).Select(f => f.Name).ToArray());

        Assert.Equal(new[] { "整除运算符 //", "按位运算符 & | ~ << >>", "<const> / <close> 属性" },
            LuaDialect.FalsePositives(LuaVersion.Lua54).Select(f => f.Name).ToArray());
    }

    /// <summary>
    /// **正控 + 边界**：目标未定或目标 == 基准时，两个集合都必须**空**。
    /// 没有这条，上面两条"集合恰好等于某清单"的判据挡不住"函数恒返回全表"这种坏法。
    /// </summary>
    [Fact]
    public void Gaps_AreEmpty_WhenTargetIsUnknownOrEqualsBaseline()
    {
        foreach (var t in new[] { LuaVersion.Unknown, LuaDialect.ParserBaseline })
        {
            Assert.Empty(LuaDialect.FalseNegatives(t));
            Assert.Empty(LuaDialect.FalsePositives(t));
        }
    }

    /// <summary>
    /// 非版本单调项（<c>中文标识符</c>）**不许**被塞一个假的"最小版本"充数，
    /// 而且它**不在**自动算出的集合里 ⇒ <see cref="LuaDialect.FalseNegatives"/> 给出的是**下界**，
    /// 不是完备清单（它实测在 5.1.5 上确实是假阴性，但成因是 locale / 构建配置，不是版本）。
    /// </summary>
    [Fact]
    public void NonMonotone_StaysOutOfTheVersionModel_SoTheSetIsOnlyALowerBound()
    {
        var nm = Assert.Single(LuaDialect.NonMonotone);
        Assert.Null(nm.Minimum);
        Assert.Equal("中文标识符", nm.Name);

        // 它确实在 5.1.5 上不可用（探针实测），但**故意**不进自动集合 —— 这条断言就是在说这件事。
        Assert.DoesNotContain("中文标识符",
            LuaDialect.FalseNegatives(LuaVersion.Lua51).Select(f => f.Name));
    }

    // ── 触发线：目标一旦确定，对齐工作必须跟着做 ──

    /// <summary>
    /// **触发线**：目标版本当前未定（固件里没有 Lua）。
    ///
    /// <para>这条**红了不代表有 bug** —— 代表有人确定了目标版本。那时要做的不是把这条改绿，而是：
    /// ① 用真实目标重跑 <c>tools/probe-lua-dialect.lua</c>；
    /// ② 按实测结果更新 <see cref="LuaDialect.Features"/> 的 <c>Minimum</c> / <c>Stage</c>；
    /// ③ 处置新出现的假阴性（改提示文案 / 加库白名单，见专题的"三条路径"）；
    /// ④ 改 <c>Dialect_*</c> 那组钉桩与专题文档。</para>
    ///
    /// <para>把"待对齐"写成一条会红的断言，比写在文档里可靠 —— 文档没人读，红色的测试有人修。</para>
    /// </summary>
    [Fact]
    public void Target_IsStillUnknown_AndDecidingItTriggersRealWork()
    {
        if (LuaDialect.Target != LuaVersion.Unknown)
        {
            Assert.Fail(
                $"有人确定了设备侧目标 Lua 版本 = {LuaDialect.LabelOf(LuaDialect.Target)}。"
                + "这不是「把这条改绿」就完了 —— 请按本测试的注释做完整对齐："
                + "① 用真实目标重跑 tools/probe-lua-dialect.lua；"
                + "② 按实测更新 LuaDialect.Features 的 Minimum / Stage；"
                + "③ 处置新出现的假阴性；④ 改 Dialect_* 钉桩与专题文档。");
        }
    }
}
