using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **en-US 的机械核对**（阶段 226，登记 `#27`）—— 只做机器能判的部分，**不装作做了逐字校对**。
///
/// <para><b>既有守卫的边界</b>：`LocalizationTests` 保证两本字典**键集**一致，
/// `LangKeyCoverageTests` 保证 `Lang` 常量在两语里都解析得出 —— 但**取值内容**没人看：
/// 英文里混进中文、占位符 `{0}`/`{1}` 少一个（运行时 <c>string.Format</c> 会**抛异常**或显示裸占位符）、
/// 空串、以及"英文＝中文"（多半是漏翻）都过得去。</para>
///
/// <para><b>这一面判四件事</b>：① 两语**占位符**（含格式说明，如 <c>{0:F2}</c>）必须一致；
/// ② 英文里不许出现 CJK（**唯一例外**逐条登记：语言切换项"中文"）；③ 取值不得为空/空白；
/// ④ **两语取值完全相同**的键必须逐条登记（新冒出来一条 ⇒ 要么翻，要么登记理由）。
/// 另：把"英文明显偏长"（≥40 字符且 &gt;3× 中文）的清单**打出来**给人看（带只减不增的上限）。</para>
///
/// <para>⚠ **本面不判**：术语统一、语气、语序、大小写惯例、缩写是否地道 —— 那些需要母语级判断。
/// 本面交付的是"机器能挑出的错已挑完 ＋ 一份给人过目的清单"（见 `docs/2026-10-01-en-us-mechanical-audit.md`）。</para>
/// </summary>
public class EnUsMechanicalAuditTests
{
    private const string ZhPath = "src/HmiEditor.Core/Resources/Strings.zh-CN.json";
    private const string EnPath = "src/HmiEditor.Core/Resources/Strings.en-US.json";

    /// <summary>英文里**允许**出现 CJK 的键（逐条理由）。</summary>
    private static readonly Dictionary<string, string> CjkAllowed = new(StringComparer.Ordinal)
    {
        ["menu.lang.zhCN"] = "语言切换项：英文界面里这一项本来就该显示「中文」",
    };

    /// <summary>
    /// 两语取值**完全相同**的键：全部是技术名词/单位/格式模板/语言名 —— 相同才是正确翻译。
    /// 新冒出来一条相同值 ⇒ 本判据红：要么补翻译，要么在下面登记理由。
    /// </summary>
    private static readonly Dictionary<string, string> IdenticalAllowed = new(StringComparer.Ordinal)
    {
        ["ai.api.key"] = "API Key",
        ["ai.api.key.hint"] = "sk-...",
        ["ai.endpoint"] = "Endpoint",
        ["ai.model"] = "Model",
        ["ai.model.hint"] = "gpt-4o / qwen2.5 / claude-sonnet-4",
        ["ai.provider"] = "Provider",
        ["ai.provider.claude"] = "Claude",
        ["cb.resolution.1024x600"] = "1024x600",
        ["cb.resolution.1280x800"] = "1280x800",
        ["data.type.bool"] = "Bool",
        ["data.type.float32"] = "Float32",
        ["data.type.int16"] = "Int16",
        ["data.type.int32"] = "Int32",
        ["data.type.string"] = "String",
        ["data.type.uint16"] = "UInt16",
        ["data.type.uint32"] = "UInt32",
        ["device.log.tag.fel"] = "FEL",
        ["driver.modbus.rtu"] = "Modbus RTU",
        ["driver.modbus.tcp"] = "Modbus TCP",
        ["driver.mqtt"] = "MQTT",
        ["driver.opc.ua"] = "OPC UA",
        ["finding.copy.line"] = "[{0}] {1}: {2} ({3})",
        ["menu.lang.enUS"] = "English",
        ["menu.lang.zhCN"] = "中文",
        ["param.endpoint.url"] = "Endpoint URL",
        ["param.qos"] = "QoS",
        ["project.settings.display.default.dpi"] = "DPI",
        ["project.version"] = "v1.0.0",
        ["prop.screen.safe.bottom"] = "B",
        ["prop.screen.safe.left"] = "L",
        ["prop.screen.safe.right"] = "R",
        ["prop.screen.safe.top"] = "T",
        ["script.alarm"] = "Alarm",
        ["script.on.click"] = "OnClick",
        ["script.on.value.changed"] = "OnValueChanged",
        ["script.timer"] = "Timer",
        ["status.git.info"] = "Git: {0}",
        ["var.udt.tab"] = "UDT",
    };

    /// <summary>英文"明显偏长"的条数上限（只减不增；当前基线见专题读数）。</summary>
    private const int LongOutlierBaseline = 127;

    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff\u3000-\u303f\uff00-\uffef]", RegexOptions.Compiled);

    /// <summary>取值里的占位符（含格式说明），排序后比较 —— 位置无关，只要求**集合**一致。</summary>
    private static List<string> Placeholders(string value) =>
        Regex.Matches(value, @"\{(\d+)(?::[^}]*)?\}").Select(m => m.Value).OrderBy(x => x, StringComparer.Ordinal).ToList();

    private static Dictionary<string, string> Load(string relative)
    {
        var path = Path.Combine(SourceScanner.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"字典不存在：{relative}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
    }

    private static (Dictionary<string, string> Zh, Dictionary<string, string> En) Both() => (Load(ZhPath), Load(EnPath));

    private readonly ITestOutputHelper _output;
    public EnUsMechanicalAuditTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Face_ScansTheWholeDictionary()
    {
        var (zh, en) = Both();

        Assert.True(zh.Count >= 2000, $"只扫到 {zh.Count} 条中文键 —— 字典读取面塌了（阶段 226 实测 2363）");
        Assert.Equal(zh.Keys.OrderBy(k => k, StringComparer.Ordinal), en.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    /// <summary>① 占位符（含格式说明）必须一致：少一个 ⇒ 运行时显示裸占位符或直接抛异常。</summary>
    [Fact]
    public void Placeholders_MatchBetweenLanguages()
    {
        var (zh, en) = Both();
        var problems = new List<string>();

        foreach (var key in zh.Keys)
        {
            var zhPh = Placeholders(zh[key]);
            var enPh = Placeholders(en[key]);
            if (!zhPh.SequenceEqual(enPh))
                problems.Add($"{key}: zh=[{string.Join(",", zhPh)}] en=[{string.Join(",", enPh)}]");
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} 条键的占位符两语不一致（会显示裸占位符或抛异常）：\n  " + string.Join("\n  ", problems));
    }

    /// <summary>② 英文里不许有 CJK（唯一例外逐条登记）。</summary>
    [Fact]
    public void EnglishCjk_IsOnlyTheRegisteredLanguageName()
    {
        var (_, en) = Both();
        var problems = new List<string>();

        foreach (var (key, value) in en)
        {
            if (!Cjk.IsMatch(value)) continue;
            if (!CjkAllowed.ContainsKey(key))
                problems.Add($"{key} = {value}");
        }

        // 登记的例外必须**真的**含 CJK（否则登记过期）
        var ghosts = CjkAllowed.Keys.Where(k => !en.TryGetValue(k, out var v) || !Cjk.IsMatch(v)).ToArray();

        Assert.True(problems.Count == 0,
            "英文取值里出现中文（要么翻，要么在 CjkAllowed 里登记理由）：\n  " + string.Join("\n  ", problems));
        Assert.True(ghosts.Length == 0, "CjkAllowed 里这些键已经不含中文了（登记过期）：" + string.Join(", ", ghosts));
    }

    /// <summary>③ 取值不得为空/空白（空串在界面上就是"什么都没有"）。</summary>
    [Fact]
    public void NoEmptyValues()
    {
        var (zh, en) = Both();
        var problems = new List<string>();

        foreach (var dictionary in new[] { (Name: "zh-CN", Map: zh), (Name: "en-US", Map: en) })
            problems.AddRange(dictionary.Map.Where(kv => string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => $"{dictionary.Name}:{kv.Key}"));

        Assert.True(problems.Count == 0, "空/空白取值：\n  " + string.Join("\n  ", problems));
    }

    /// <summary>④ 两语取值完全相同 ⇒ 必须逐条登记（新的一条要么翻，要么写明为什么相同是对的）。</summary>
    [Fact]
    public void IdenticalValues_AreRegistered()
    {
        var (zh, en) = Both();

        var unregistered = zh.Keys
            .Where(k => zh[k] == en[k] && !IdenticalAllowed.ContainsKey(k))
            .Select(k => $"{k} = {zh[k]}")
            .ToArray();

        var ghosts = IdenticalAllowed.Keys
            .Where(k => !zh.TryGetValue(k, out var zv) || !en.TryGetValue(k, out var ev) || zv != ev)
            .ToArray();

        Assert.True(unregistered.Length == 0,
            $"这些键两语取值相同但没登记（多半是漏翻）：\n  " + string.Join("\n  ", unregistered));
        Assert.True(ghosts.Length == 0,
            "IdenticalAllowed 里这些键已经不同了（登记过期，请删掉）：" + string.Join(", ", ghosts));
    }

    /// <summary>
    /// ⑤ **给人看的清单**：英文明显偏长（≥40 字符且 &gt;3× 中文）的键 —— 打印前 15 条 ＋ 总数，
    /// 并对总数做**只减不增**上限（新增长句要么改短，要么有意识地把基线抬上去）。
    /// </summary>
    [Fact]
    public void LongEnglishValues_AreReported()
    {
        var (zh, en) = Both();

        var outliers = zh.Keys
            .Select(k => (Key: k, Zh: zh[k].Length, En: en[k].Length,
                          Ratio: en[k].Length / (double)Math.Max(zh[k].Length, 1)))
            .Where(x => x.En >= 40 && x.Ratio > 3.0)
            .OrderByDescending(x => x.Ratio)
            .ToArray();

        _output.WriteLine($"英文偏长（≥40 字符且 >3× 中文）：{outliers.Length} 条（基线 {LongOutlierBaseline}）");
        foreach (var x in outliers.Take(15))
            _output.WriteLine($"  {x.Ratio:0.0}x  {x.Key}  zh {x.Zh} → en {x.En}");

        Assert.True(outliers.Length <= LongOutlierBaseline,
            $"英文偏长的条数 {outliers.Length} 超过基线 {LongOutlierBaseline} —— 新增长句请改短，" +
            "或确认后把基线抬上去（本判据是「只减不增」式的卫生线，不是逐字校对）。");
    }
}
