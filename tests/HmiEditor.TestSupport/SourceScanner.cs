using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HmiEditor.TestSupport;

/// <summary>
/// 源码扫描工具 —— 给「结构守卫」型测试用：断言某种写法在 <c>src/</c> 下只允许出现在指定位置。
///
/// <para><b>抽出来的理由：</b>这类守卫有一条共通的坑 —— <b>必须先剥注释再匹配</b>。
/// 规则本身是要被文档化的（例如「不要直接 <c>new HmiScreen</c>」「色深候选项来自枚举，别硬编码」），
/// 而讲解规则的注释里必然出现被禁止的那串字面量。不剥注释，规则会把自己的文档判为违规，
/// 下一个人看到的是一条莫名其妙的红，接着把注释删掉 —— 规则从「有文档」退化成「没文档」。</para>
///
/// <para><b>另一条更隐蔽的坑（踩过）：</b>剥注释不能是「先正则替换块注释、再替换行注释」——
/// 因为注释正文里可能写着路径通配符，
/// 例如 <c>/// 由 firmware/*/firmware.json 生成</c>。那串 <c>/*</c> 会被当成块注释开头，
/// 一路吞到下一个 <c>*/</c>（在两处这类注释之间，整整 150 行代码被吃掉了）；
/// 先剥行注释也救不了，因为块注释正文里带 <c>//</c>（URL）会把 <c>*/</c> 一起删掉。
/// 正确做法是**单遍状态机**：只有处在「代码」状态时才认注释开头，
/// 并且区分 <c>"…"</c> / <c>@"…"</c> / <c>"""…"""</c> / <c>'…'</c> 四种字面量。</para>
///
/// <para><b>可校验的不变量：</b><see cref="StripComments"/> 的输出与输入
/// <b>长度相同、行数相同</b>（注释字符被替换成空格，换行原样保留）。
/// 任何「吞掉一大段代码」的缺陷都会立刻打破这条不变量 —— 见
/// <c>SourceScanner_StripComments_PreservesLengthAndLineCountOnWholeCorpus</c>。</para>
/// </summary>
public static class SourceScanner
{
    /// <summary>仓库根（含 HmiEditor.slnx）。从测试宿主基目录向上找。</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>
    /// 把注释替换成空格。字符串字面量原样保留（它们也是代码）。
    /// 输出与输入长度相同、行数相同。
    /// </summary>
    public static string StripComments(string source) => Scan(source, maskLiterals: false);

    /// <summary>
    /// 把注释**和字符串/字符字面量的内容**都替换成空格（引号本身保留）；
    /// <b>插值字符串的 <c>{…}</c> 孔原样保留</b> —— 孔里是真代码，不是文本。
    /// 输出与输入长度相同、行数相同。
    ///
    /// <para><b>为什么需要它：</b>有些文件里嵌着**别的语言的源码字符串** ——
    /// 典型是生成器把 C 代码逐行 <c>sb.AppendLine("struct timespec ts;")</c> 写出来。
    /// 剥注释不够：那串 C 代码在"声明扫描"眼里就是一条 C# 声明
    /// （实测：把 <c>struct timespec</c> 当成了一个 C# 类型，进了"死类型"候选）。
    /// 判断「某个 C# 标识符有没有被引用」时，字面量里的同名文本**不是引用**，
    /// 所以这一类扫描必须用本方法而不是 <see cref="StripComments"/>。</para>
    ///
    /// <para><b>为什么孔要保留：</b><c>$"…{LvglMacros.LabelLongMode(x)}…"</c> 里那处引用是
    /// <b>唯一</b>一处 —— 整串抹掉就会把活类型判成死类型（实测踩过）。
    /// 注意反向的坑：抹掉字面量之后，**守卫自己基线里的那些名字字符串**
    /// （形如 <c>"TypeName",</c> 的登记项）也不再计入 —— 这正是想要的（自指不能算引用，
    /// 否则基线里每个名字都会被自己"用活"）。</para>
    ///
    /// <para><b>已知边界：</b>原始字符串 <c>$"""…"""</c> 整体抹掉 —— 它的孔用花括号**个数**
    /// 决定，规则与普通插值串不同，本方法不做特例。真出现时表现为"某类型被漏判成死"，
    /// 而相等判定会立刻把这条差异报出来，不会静默。</para>
    /// </summary>
    public static string MaskLiterals(string source) => Scan(source, maskLiterals: true);

    private static string Scan(string source, bool maskLiterals)
    {
        var sb = new StringBuilder(source.Length);
        var i = 0;

        while (i < source.Length)
        {
            var c = source[i];

            if (c == '/' && Peek(source, i, 1) == '/')
            {
                i = SkipLineComment(source, i, sb);
                continue;
            }

            if (c == '/' && Peek(source, i, 1) == '*')
            {
                i = SkipBlockComment(source, i, sb);
                continue;
            }

            // @$" / $@" / @" —— 逐字字符串：反斜杠不是转义，"" 才表示一个引号
            if (c == '@' && (Peek(source, i, 1) == '"'
                             || (Peek(source, i, 1) == '$' && Peek(source, i, 2) == '"')))
            {
                // $@" 的 $ 在 @ 前面；@$" 的 $ 紧接着 @
                var interpolated = (i > 0 && source[i - 1] == '$') || Peek(source, i, 1) == '$';
                i = CopyVerbatimString(source, i, sb, maskLiterals, interpolated);
                continue;
            }

            if (c == '"' && Peek(source, i, 1) == '"' && Peek(source, i, 2) == '"')
            {
                // 原始字符串整体抹掉：`$"""…"""` 的孔用花括号**个数**决定，规则与普通插值串不同，
                // 这里不做特例（见 MaskLiterals 的说明）。
                i = CopyRawString(source, i, sb, maskLiterals);
                continue;
            }

            if (c == '"')
            {
                var interpolated = i > 0 && source[i - 1] == '$';
                i = CopyRegularString(source, i, sb, maskLiterals, interpolated);
                continue;
            }

            if (c == '\'')
            {
                i = CopyCharLiteral(source, i, sb, maskLiterals);
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>读取源文件并剥注释。</summary>
    public static string ReadCode(string absolutePath) =>
        StripComments(File.ReadAllText(absolutePath));

    /// <summary>读取源文件并把注释与字面量内容都抹掉。</summary>
    public static string ReadMasked(string absolutePath) =>
        MaskLiterals(File.ReadAllText(absolutePath));

    /// <summary>
    /// 把 <b>XML 注释</b>（<c>&lt;!-- … --&gt;</c>）替换成空格，注释**外**的内容原样保留 ⇒
    /// 输出与输入<b>长度相同、行数相同</b>，下标与行号都能回原串上取。
    ///
    /// <para><b>它与 <see cref="StripComments"/> 是同一条道理的两种语言</b>：讲规则的注释里
    /// 必然出现被引用的那串字面量。本仓实测：<c>App.axaml</c> 第 20 行的 XML 注释里写着
    /// <c>（使用 {StaticResource IconXxx} 引用…）</c> —— 不剥注释的资源键盘点会把
    /// <c>IconXxx</c> 报成「引用了但从未定义」的**假缺口**。</para>
    ///
    /// <para><b>两个反面写法（都踩过）：</b>①把注释整体<b>删掉</b>（<c>Replace(text, "")</c>）⇒
    /// 长度变了，之后用 <c>m.Index</c> 反查行号**全部前移**；
    /// ②把注释（连换行）整体换成等长空格 ⇒ 长度对了但<b>行数塌了</b>，
    /// 注释之后的每个行号都偏小。所以换行必须原样保留。</para>
    ///
    /// <para><b>已知边界：</b>不认 CDATA 段（<c>XML</c> 里 <c>&lt;!--</c> 不能嵌套，
    /// 故「找下一个 <c>--&gt;</c>」是正确语义）；未闭合注释吃到文末
    /// （与 <see cref="StripComments"/> 同：宁可多抹，不可漏抹）。</para>
    /// </summary>
    public static string StripXmlComments(string xaml)
    {
        var sb = new StringBuilder(xaml.Length);
        var i = 0;

        while (i < xaml.Length)
        {
            if (xaml[i] == '<' && Peek(xaml, i, 1) == '!' && Peek(xaml, i, 2) == '-' && Peek(xaml, i, 3) == '-')
            {
                var close = xaml.IndexOf("-->", i + 4, StringComparison.Ordinal);
                var stop = close < 0 ? xaml.Length : close + 3;
                for (var k = i; k < stop; k++)
                    sb.Append(xaml[k] == '\n' ? '\n' : ' ');
                i = stop;
                continue;
            }

            sb.Append(xaml[i]);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>在剥注释后的源码里数 <paramref name="pattern"/> 的命中次数。</summary>
    public static int CountMatches(string absolutePath, Regex pattern) =>
        pattern.Matches(ReadCode(absolutePath)).Count;

    /// <summary><c>src/</c> 下所有产品源码（排除 bin/obj），按路径排序。</summary>
    public static IReadOnlyList<string> ProductSources() => Enumerate(Path.Combine(RepoRoot, "src"));

    /// <summary>给定目录下所有 .cs（排除 bin/obj），按路径排序。</summary>
    public static IReadOnlyList<string> Enumerate(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

    /// <summary>相对仓库根的正斜杠路径（用于白名单比对）。</summary>
    public static string Relative(string absolutePath) =>
        Path.GetRelativePath(RepoRoot, absolutePath).Replace('\\', '/');

    /// <summary>是否位于编译输出目录（bin / obj）。</summary>
    public static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static char Peek(string s, int i, int offset) =>
        i + offset < s.Length ? s[i + offset] : '\0';

    private static int SkipLineComment(string s, int i, StringBuilder sb)
    {
        while (i < s.Length && s[i] != '\n')
        {
            sb.Append(' ');
            i++;
        }
        return i;
    }

    private static int SkipBlockComment(string s, int i, StringBuilder sb)
    {
        while (i < s.Length)
        {
            if (s[i] == '*' && Peek(s, i, 1) == '/')
            {
                sb.Append("  ");
                return i + 2;
            }
            // 换行原样保留，保证行数不变量成立（未闭合注释也不破坏它）
            sb.Append(s[i] == '\n' ? '\n' : ' ');
            i++;
        }
        return i;
    }

    /// <summary>字面量内容：mask 时换成空格（换行原样保留，维持行数不变量）。</summary>
    private static void AppendContent(StringBuilder sb, char ch, bool mask) =>
        sb.Append(mask && ch != '\n' ? ' ' : ch);

    private static int CopyVerbatimString(string s, int i, StringBuilder sb, bool mask, bool interpolated)
    {
        while (i < s.Length && s[i] != '"')
        {
            sb.Append(s[i]);
            i++;
        }
        if (i >= s.Length) return i;

        i = AppendRun(s, i, sb, '"', 1);
        var depth = 0;
        while (i < s.Length)
        {
            if (mask && interpolated)
            {
                if (s[i] == '{' && Peek(s, i, 1) == '{') { sb.Append("  "); i += 2; continue; }
                if (s[i] == '}' && Peek(s, i, 1) == '}') { sb.Append("  "); i += 2; continue; }
                if (s[i] == '{') { depth++; sb.Append('{'); i++; continue; }
                if (depth > 0)
                {
                    if (s[i] == '}') depth--;
                    sb.Append(s[i]);
                    i++;
                    continue;
                }
            }

            if (s[i] == '"')
            {
                if (Peek(s, i, 1) == '"')
                {
                    // 逐字字符串里的 "" 是一个转义引号 —— 属**内容**
                    if (mask) sb.Append("  ");
                    else sb.Append("\"\"");
                    i += 2;
                    continue;
                }
                sb.Append('"');
                return i + 1;
            }
            AppendContent(sb, s[i], mask);
            i++;
        }
        return i;
    }

    private static int CopyRawString(string s, int i, StringBuilder sb, bool mask)
    {
        var quotes = 0;
        while (Peek(s, i, quotes) == '"') quotes++;
        i = AppendRun(s, i, sb, '"', quotes);

        while (i < s.Length)
        {
            if (s[i] == '"')
            {
                var run = 0;
                while (Peek(s, i, run) == '"') run++;
                if (run >= quotes)
                {
                    i = AppendRun(s, i, sb, '"', run);
                    return i;
                }
                i = AppendRun(s, i, sb, '"', run);
                continue;
            }
            AppendContent(sb, s[i], mask);
            i++;
        }
        return i;
    }

    private static int CopyRegularString(string s, int i, StringBuilder sb, bool mask, bool interpolated)
    {
        sb.Append('"');
        i++;
        var depth = 0;
        while (i < s.Length)
        {
            var c = s[i];

            // 插值孔 `{…}` 里是**真代码**，原样保留；`{{` / `}}` 是字面花括号，属内容。
            // 不这么做的话，`$"…{LvglMacros.LabelLongMode(x)}…"` 这种引用会被整串抹掉 =
            // 把唯一一处引用抹成零（实测踩过）。
            if (mask && interpolated)
            {
                if (c == '{' && Peek(s, i, 1) == '{') { sb.Append("  "); i += 2; continue; }
                if (c == '}' && Peek(s, i, 1) == '}') { sb.Append("  "); i += 2; continue; }
                if (c == '{') { depth++; sb.Append('{'); i++; continue; }
                if (depth > 0)
                {
                    if (c == '}') depth--;
                    sb.Append(c);
                    i++;
                    continue;
                }
            }

            if (c == '\\' && depth == 0)
            {
                if (mask)
                {
                    AppendContent(sb, c, true);
                    if (i + 1 < s.Length) AppendContent(sb, s[i + 1], true);
                }
                else
                {
                    sb.Append(c);
                    if (i + 1 < s.Length) sb.Append(s[i + 1]);
                }
                i += 2;
                continue;
            }
            AppendContent(sb, c, mask);
            i++;
            if (c == '"' || c == '\n') return i;
        }
        return i;
    }

    private static int CopyCharLiteral(string s, int i, StringBuilder sb, bool mask)
    {
        sb.Append('\'');
        i++;
        while (i < s.Length)
        {
            var c = s[i];
            if (c == '\\')
            {
                if (mask)
                {
                    AppendContent(sb, c, true);
                    if (i + 1 < s.Length) AppendContent(sb, s[i + 1], true);
                }
                else
                {
                    sb.Append(c);
                    if (i + 1 < s.Length) sb.Append(s[i + 1]);
                }
                i += 2;
                continue;
            }
            AppendContent(sb, c, mask);
            i++;
            if (c == '\'' || c == '\n') return i;
        }
        return i;
    }

    private static int AppendRun(string s, int i, StringBuilder sb, char ch, int count)
    {
        for (var k = 0; k < count && i < s.Length; k++)
        {
            sb.Append(ch);
            i++;
        }
        return i;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HmiEditor.slnx")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                $"向上未找到仓库根（含 HmiEditor.slnx）。起点：{AppContext.BaseDirectory}");

        return dir.FullName;
    }
}
