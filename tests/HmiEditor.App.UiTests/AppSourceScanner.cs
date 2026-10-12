using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HmiEditor.TestSupport;

namespace HmiEditor.App.UiTests;

/// <summary>
/// App 层源码扫描器 —— 静态「接线守卫」共用。
///
/// <para><b>为什么抽出来：</b>从 <see cref="AiCancellationWiringTests"/> 抽出，因为出现了
/// 第二个消费者（<see cref="DataRootMigrationWiringTests"/>）。复制一份"找仓库根 + 枚举 .cs"
/// 就等于制造第二套扫描面 —— 两份的 <c>bin/obj</c> 过滤条件与根目录解析方式日后必然漂移，
/// 而**扫描面本身**正是这类守卫最大的盲区（扫了个空也会全绿）。</para>
/// </summary>
internal static class AppSourceScanner
{
    /// <summary>仓库根（含 <c>HmiEditor.slnx</c>）；找不到直接抛，不猜位置。</summary>
    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HmiEditor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("未找到仓库根目录（HmiEditor.slnx）");
    }

    /// <summary>App 项目源码根。</summary>
    public static string AppRoot { get; } = Path.Combine(FindRepoRoot(), "src", "HmiEditor.App");

    /// <summary>构建输出目录：源码扫描必须跳过，否则会读到 <c>obj/</c> 里的生成副本。</summary>
    public static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>扫 <c>src/HmiEditor.App</c> 下全部 .cs（含 .axaml.cs），返回 (相对路径, 文本)。</summary>
    public static List<(string Relative, string Text)> ScanAppSources() => Scan("*.cs");

    /// <summary>
    /// 扫 <c>src/HmiEditor.App</c> 下全部 .axaml（不含 .axaml.cs —— 那是 <see cref="ScanAppSources"/> 的面）。
    ///
    /// <para><b>为什么单列一个面：</b>本地化键有**两种消费方式** —— 代码里
    /// <c>Format(key, args…)</c> 与 XAML 里 <c>{res:Loc key}</c>（无实参）。
    /// 只看代码那一侧，会把"同一个键被两种方式共用"这类缺陷整个漏掉。</para>
    /// </summary>
    public static List<(string Relative, string Text)> ScanAppXaml() => Scan("*.axaml");

    /// <summary>
    /// 扫 <c>src/</c> 下**全部项目**的 .cs（排除 bin/obj），路径相对**仓库根**
    /// （形如 <c>src/HmiEditor.Core/…</c>）—— 给"面不该只覆盖 App 层"的守卫用。
    ///
    /// <para><b>为什么要它：</b><see cref="ScanAppSources"/> 的面**写死在 App**，于是
    /// "同一形状的站点出现在 Core / AI"这件事在守卫眼里不存在（阶段 82 的 J3 就是这样，
    /// 登记 #61 / 寄存器 §39.10 记的"面的边界"）。凡是守**产品代码写法**的判据，
    /// 面都该从 <c>src/</c> 全量取 —— 枚举器唯一走 <see cref="SourceScanner.ProductSources"/>
    /// （它已经排除 bin/obj 并排序），本方法只补"读文本 + 转仓库相对路径"。</para>
    /// </summary>
    public static List<(string Relative, string Text)> ScanAllProductSources() =>
        SourceScanner.ProductSources()
            .Select(f => (SourceScanner.Relative(f), File.ReadAllText(f)))
            .ToList();

    private static List<(string Relative, string Text)> Scan(string pattern) =>
        Directory.EnumerateFiles(AppRoot, pattern, SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f))
            .Select(f => (Path.GetRelativePath(AppRoot, f).Replace('\\', '/'), File.ReadAllText(f)))
            .ToList();

    /// <summary>取单个 App 源文件（按相对路径后缀匹配）；**必须恰好一个**，否则抛。</summary>
    public static (string Relative, string Text) Single(string relativeSuffix) =>
        ScanAppSources().Single(s => s.Relative.EndsWith(relativeSuffix, StringComparison.Ordinal));
}
