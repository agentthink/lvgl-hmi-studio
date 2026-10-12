using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **用户文档守卫**（登记表 §4 第 29 项：用户文档 / 故障排查手册）。
///
/// <para>文档最大的失效方式不是写得不好，而是**指向不存在的东西**：命令、脚本、路径、
/// 快捷键一旦改名，文档不会报错、也没人会发现 —— 现场工程师照着敲才发现。
/// 这里把文档里可机检的部分变成判据：</para>
/// <list type="number">
///   <item>代码标记里出现的仓库相对路径必须真实存在（支持单层 <c>*</c> 通配）；</item>
///   <item>markdown 相对链接必须落到真实文件；</item>
///   <item><c>bash scripts/xxx.sh</c> 引用的脚本必须存在且**可执行**；</item>
///   <item>文档里写的快捷键必须在 App 源码里找得到（菜单 <c>InputGesture</c> / 命令目录 / 脚本编辑器）。</item>
/// </list>
/// <para>另有自检：扫描到的路径/链接/快捷键数量低于下限即红 —— 否则正则一失效，
/// 本守卫会退化成"什么都没扫到也全绿"。</para>
/// </summary>
public class UserGuideDocTests
{
    private static readonly string[] Docs = { "docs/user-guide.md", "docs/troubleshooting.md" };

    private static readonly string[] TopLevelDirs =
        { "src", "tests", "tools", "scripts", "firmware", "hardware", "docs", "patches", "build" };

    private static string RepoRoot => SourceScanner.RepoRoot;

    private static IEnumerable<(string Doc, string Text)> ReadDocs() =>
        Docs.Select(d => (d, File.ReadAllText(Path.Combine(RepoRoot, d))));

    private static bool IsRepoPath(string token)
    {
        var trimmed = token.Trim().TrimEnd('/');
        return TopLevelDirs.Any(dir => trimmed.StartsWith(dir + "/", StringComparison.Ordinal));
    }

    /// <summary>单层 <c>*</c> 通配匹配（够用即可：文档里只用过 <c>firmware/*/firmware.json</c> 这种）。</summary>
    private static bool GlobExists(string pattern)
    {
        var parts = pattern.TrimEnd('/').Split('/');
        return MatchSegments(RepoRoot, parts, 0);
    }

    private static bool MatchSegments(string dir, string[] parts, int index)
    {
        if (index >= parts.Length) return File.Exists(dir) || Directory.Exists(dir);
        if (!Directory.Exists(dir)) return false;

        var segment = parts[index];
        if (!segment.Contains('*'))
            return MatchSegments(Path.Combine(dir, segment), parts, index + 1);

        var regex = "^" + Regex.Escape(segment).Replace("\\*", "[^/]*") + "$";
        return Directory.EnumerateFileSystemEntries(dir)
            .Any(entry => Regex.IsMatch(Path.GetFileName(entry), regex) &&
                          MatchSegments(entry, parts, index + 1));
    }

    [Fact]
    public void BothDocumentsExistAndAreSubstantial()
    {
        foreach (var (doc, text) in ReadDocs())
        {
            Assert.True(text.Length > 3000, $"{doc} 只有 {text.Length} 字符 —— 用户文档不该这么薄");
            Assert.Contains("# ", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ScannerSeesEnoughToBeWorthTrusting()
    {
        var (paths, links, shortcuts, scripts) = ScanAll();

        Assert.True(paths.Count >= 40, $"只扫到 {paths.Count} 条仓库路径 —— 正则可能已失效");
        Assert.True(links.Count >= 3, $"只扫到 {links.Count} 条相对链接");
        Assert.True(shortcuts.Count >= 15, $"只扫到 {shortcuts.Count} 个快捷键");
        Assert.True(scripts.Count >= 3, $"只扫到 {scripts.Count} 个脚本引用");
    }

    [Fact]
    public void EveryReferencedRepoPathExists()
    {
        var (paths, _, _, _) = ScanAll();
        var missing = paths
            .Where(p => !GlobExists(p))
            .Select(p => p)
            .Distinct()
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "文档引用了不存在的仓库路径：\n  " + string.Join("\n  ", missing));
    }

    [Fact]
    public void EveryRelativeMarkdownLinkResolves()
    {
        var (_, links, _, _) = ScanAll();
        var missing = new List<string>();
        foreach (var (doc, target) in links)
        {
            var fromDoc = Path.Combine(RepoRoot, "docs", target);
            var fromRoot = Path.Combine(RepoRoot, target);
            if (!File.Exists(fromDoc) && !File.Exists(fromRoot) && !Directory.Exists(fromRoot))
                missing.Add($"{doc} → {target}");
        }

        Assert.True(missing.Count == 0,
            "文档里有指向不存在目标的链接：\n  " + string.Join("\n  ", missing));
    }

    [Fact]
    public void EveryReferencedScriptExistsAndIsExecutable()
    {
        var (_, _, _, scripts) = ScanAll();
        var problems = new List<string>();
        foreach (var script in scripts.Distinct())
        {
            var full = Path.Combine(RepoRoot, script);
            if (!File.Exists(full))
            {
                problems.Add($"{script} 不存在");
                continue;
            }
            if (!OperatingSystem.IsWindows() &&
                (File.GetUnixFileMode(full) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
            {
                problems.Add($"{script} 没有可执行位（文档让读者 `bash` 它，但仓库里不是可执行文件）");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void EveryShortcutInTheDocs_AppearsSomewhereInTheAppSources()
    {
        var appRoot = Path.Combine(RepoRoot, "src", "HmiEditor.App");
        var appText = string.Join("\n", Directory.EnumerateFiles(appRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".axaml", StringComparison.Ordinal)))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText));

        var (_, _, shortcuts, _) = ScanAll();
        var missing = shortcuts
            .Where(s => !appText.Contains(s, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "文档写了 App 源码里找不到的快捷键（改名后文档没跟上）：\n  " + string.Join("\n  ", missing));
    }

    // ── 扫描 ──

    private static (List<string> Paths, List<(string Doc, string Target)> Links, List<string> Shortcuts, List<string> Scripts) ScanAll()
    {
        var paths = new List<string>();
        var links = new List<(string, string)>();
        var shortcuts = new List<string>();
        var scripts = new List<string>();

        foreach (var (doc, text) in ReadDocs())
        {
            foreach (Match m in Regex.Matches(text, @"`([^`\n]+)`"))
            {
                var token = m.Groups[1].Value.Trim();
                if (IsRepoPath(token) && !token.Contains(' ') && !token.Contains('='))
                    paths.Add(token.TrimEnd('/'));
            }

            foreach (Match m in Regex.Matches(text, @"\[[^\]]*\]\(([^)\s]+)\)"))
            {
                var target = m.Groups[1].Value;
                if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith('#') || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                    continue;
                links.Add((doc, target.Split('#')[0]));
            }

            foreach (Match m in Regex.Matches(text, @"\b(?:Ctrl|Alt|Shift|Cmd)\+(?:[A-Za-z0-9]+\+)*[A-Za-z0-9]+\b"))
                shortcuts.Add(m.Value);

            foreach (Match m in Regex.Matches(text, @"bash\s+(scripts/[A-Za-z0-9._-]+\.sh)"))
                scripts.Add(m.Groups[1].Value);
        }

        return (paths, links, shortcuts, scripts);
    }
}
