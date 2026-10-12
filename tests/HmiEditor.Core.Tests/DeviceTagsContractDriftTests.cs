using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 设备生成物 <c>firmware/t113/generated/tags.{h,c}</c> 与**生成器契约**不得漂移
/// （2026-10-11，阶段 289）。
///
/// <para><b>起因（一次真实漂移）</b>：阶段 288 给 <c>tag_meta_t</c> 尾部追加了
/// <c>has_range/min_value/max_value</c>（远程写的值域口径），生成器
/// <c>RuntimeTagsCodeGen.Generate/GenerateHeader</c> 两处都发了 —— 但仓里那份**手工维护的
/// 示例产物**（设备 host 测试与 Tina 构建都直接吃它）没跟着更新。后果：设备侧
/// "量程从 <c>tag_meta_t</c> 取"这一步**根本编译不过**，而 CI 全绿，因为没有任何判据
/// 盯着"这份示例产物与生成器的对应关系"。</para>
///
/// <para><b>三道</b>：① 结构体字段**序列**与生成器现场产出的头逐条相同（注释措辞可比）；
/// ② 两张表（<c>HMI_UI_TAG_META</c> / <c>TAG_TABLE</c>）的每个初始化器都是**完整字段数**；
/// ③ 面自检（表非空 ＋ 生成器字段数 ＝ 8），防解析失效后退化成恒绿。</para>
/// </summary>
public class DeviceTagsContractDriftTests
{
    private const string HeaderPath = "firmware/t113/generated/tags.h";
    private const string SourcePath = "firmware/t113/generated/tags.c";

    /// <summary>name / address / read_only / scale / offset ＋ has_range / min_value / max_value</summary>
    private const int MetaFieldCount = 8;

    private static string Read(string relative)
    {
        var path = Path.Combine(SourceScanner.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"设备生成物不存在：{relative}");
        return File.ReadAllText(path);
    }

    /// <summary>剥掉注释（同行 <c>/* */</c> 与 <c>//</c>）；跨行注释的每一行都不以 `;` 结尾，自然被滤掉。</summary>
    private static string StripComments(string line)
    {
        line = Regex.Replace(line, @"/\*.*?\*/", " ");
        var slash = line.IndexOf("//", StringComparison.Ordinal);
        if (slash >= 0) line = line[..slash];
        return line;
    }

    /// <summary><c>tag_meta_t</c> 里**按序**的字段声明（剥注释后仍以 `;` 结尾的行）。</summary>
    private static List<string> MetaStructFields(string text)
    {
        var end = text.IndexOf("} tag_meta_t;", StringComparison.Ordinal);
        Assert.True(end > 0, "找不到 tag_meta_t 结构体的结尾");
        var start = text.LastIndexOf("typedef struct", end, StringComparison.Ordinal);
        Assert.True(start >= 0, "找不到 tag_meta_t 结构体的起点");
        var body = text[(text.IndexOf('{', start) + 1)..end];

        var fields = new List<string>();
        foreach (var raw in body.Split('\n'))
        {
            var line = StripComments(raw).Trim();
            if (line.Length == 0 || !line.EndsWith(';')) continue;
            fields.Add(line);
        }
        return fields;
    }

    /// <summary>表里每个初始化器的字段个数（顶层逗号数 ＋ 1）。</summary>
    private static List<int> InitializerArities(string text, string tableName)
    {
        // ⚠ 必须**按定义形状**定位：表名在文件头的说明注释、枚举引用、get/set 函数里都会出现
        //   （第一版用 IndexOf 直接命中注释，于是 `=` 落进枚举 ⇒ 解析出 0 条 ⇒ 判据自己先红了）。
        var definition = Regex.Match(text,
            Regex.Escape(tableName) + @"\s*(?:\[[^\]]*\])?\s*=\s*\{");
        Assert.True(definition.Success, $"找不到表 {tableName} 的定义（形如 `名字[...] = {{`）");
        var open = definition.Index + definition.Length - 1;   // 指向该定义的那个 '{'

        var depth = 0;
        var i = open;
        for (; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') { depth--; if (depth == 0) break; }
        }
        Assert.True(i < text.Length, $"表 {tableName} 的花括号不闭合");

        var arities = new List<int>();
        foreach (var raw in text[(open + 1)..i].Split('\n'))
        {
            var line = StripComments(raw).Trim();
            if (line.Length == 0 || line[0] != '{') continue;
            var close = line.LastIndexOf('}');
            if (close <= 0) continue;
            arities.Add(line[1..close].Count(c => c == ',') + 1);
        }
        return arities;
    }

    private static IReadOnlyList<RuntimeTag> SampleTags() => new[]
    {
        new RuntimeTag
        {
            VariableId = "PV", Address = "40001", Direction = BindingDirection.ReadWrite,
            Scale = 1.0, Offset = 0.0, DataType = "UInt16"
        },
        new RuntimeTag
        {
            VariableId = "TEMP", Address = "30001", Direction = BindingDirection.ReadOnly,
            Scale = 0.1, Offset = 0.0, DataType = "Float"
        },
    };

    [Fact]
    public void DeviceTagsHeader_MetaStruct_MatchesGeneratorFieldForField()
    {
        var generated = MetaStructFields(new RuntimeTagsCodeGen().GenerateHeader(SampleTags()));
        var committed = MetaStructFields(Read(HeaderPath));

        // 面自检：生成器的契约字段数就是 8 —— 它变了这条判据要跟着醒
        Assert.Equal(MetaFieldCount, generated.Count);
        Assert.Equal(generated, committed);
    }

    [Fact]
    public void DeviceTagsArtifacts_EveryInitializer_HasAllMetaFields()
    {
        foreach (var (path, table) in new[] { (HeaderPath, "HMI_UI_TAG_META"), (SourcePath, "TAG_TABLE") })
        {
            var arities = InitializerArities(Read(path), table);
            Assert.True(arities.Count >= 1,
                $"{path} 的 {table} 一条初始化器都没解析出来 —— 面塌了，本判据会退化成永远通过");
            Assert.All(arities, n => Assert.True(n == MetaFieldCount,
                $"{path} 的 {table} 有一条初始化器只有 {n} 个字段（应为 {MetaFieldCount}）" +
                " —— 结构体加了字段却没同步这张表（阶段 288 的真实漂移形状）"));
        }
    }

    [Fact]
    public void DeviceTagsHeader_DeclaresTheRangeContractFields()
    {
        var header = Read(HeaderPath);
        Assert.Contains("has_range", header, StringComparison.Ordinal);
        Assert.Contains("min_value", header, StringComparison.Ordinal);
        Assert.Contains("max_value", header, StringComparison.Ordinal);
    }

    /// <summary>
    /// 生成器的**两个**产物必须发同一份结构体：<c>Generate</c>（运行时表）与
    /// <c>GenerateHeader</c>（.so 头）。只钉住一个的话，另一个被改回单字段形状时判据照样绿
    /// —— 阶段 289 的负控 nc7 正是这么咬出来的。
    /// </summary>
    [Fact]
    public void BothGeneratorProducts_EmitTheSameMetaStruct()
    {
        var gen = new RuntimeTagsCodeGen();
        var fromTable = MetaStructFields(gen.Generate(SampleTags()));
        var fromHeader = MetaStructFields(gen.GenerateHeader(SampleTags()));

        Assert.Equal(MetaFieldCount, fromTable.Count);
        Assert.Equal(fromHeader, fromTable);
    }
}
