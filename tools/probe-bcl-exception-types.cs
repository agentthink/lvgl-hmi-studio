// 阶段 85 探针：给登记 #61e（"给 BCL 异常**类型**建码"）量码表该收哪些类型。
//
// 要回答的问题（**读数题，不是推理题**）：
//   「按异常**类型**映射到一句可翻译的中文，这个映射的下界是什么？」
//   三个子问题：
//     ① 本仓库真实调用面（File/Directory/Path/StreamWriter/StreamReader/ZipArchive/JsonSerializer）
//        在各种坏输入下**实际**抛哪些类型？
//     ② 每个类型的 `Message` **带不带目标路径**？（阶段 84 已量 5/5 带，这里要把面铺开）
//     ③ 换个 `CurrentUICulture` 消息变不变？（若变 ⇒ 也许不用码化；若不变 ⇒ 必须码化）
//
// 跑法（.NET 10 file-based app，不需要 csproj、不往仓库落 obj/）：
//     dotnet run tools/probe-bcl-exception-types.cs
//
// 读数落盘：
//     .workbuddy/tmp/s85-bcl-types.txt
//
// ⚠ 姊妹探针 `tools/probe-bcl-io-messages.cs`（阶段 84）：它量的是"原文带不带路径"
//   （结论：带 ⇒ C 类"带上对象"零收益）。本探针量的是"**该按哪些类型分档**"，
//   是 #61e 的输入。两者**不重复**：那个回答"要不要码化"，这个回答"码表收谁"。

using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

const string Root = "/tmp/probe85";
var seen = new List<(string Label, string Type, string Msg, bool HasPath)>();

void Probe(string label, Action act, string? expectPath = null)
{
    try
    {
        act();
        Console.WriteLine($"[{label}] 没有抛");
        seen.Add((label, "（不抛）", "", false));
    }
    catch (Exception ex)
    {
        var msg = ex.Message.Replace("\n", "\\n");
        var has = msg.Contains(expectPath ?? Root, StringComparison.Ordinal);
        Console.WriteLine($"[{label}] {ex.GetType().Name}");
        Console.WriteLine($"        {msg}");
        Console.WriteLine($"        含目标路径? {has}   HResult=0x{ex.HResult:X8}");
        seen.Add((label, ex.GetType().Name, msg, has));
    }
}

async Task ProbeAsync(string label, Func<Task> act)
{
    try
    {
        await act();
        Console.WriteLine($"[{label}] 没有抛");
        seen.Add((label, "（不抛）", "", false));
    }
    catch (Exception ex)
    {
        var msg = ex.Message.Replace("\n", "\\n");
        var has = msg.Contains(Root, StringComparison.Ordinal);
        Console.WriteLine($"[{label}] {ex.GetType().Name}");
        Console.WriteLine($"        {msg}");
        Console.WriteLine($"        含目标路径? {has}   HResult=0x{ex.HResult:X8}");
        seen.Add((label, ex.GetType().Name, msg, has));
    }
}

if (Directory.Exists(Root)) Directory.Delete(Root, true);
Directory.CreateDirectory(Root);

var dirAsFile = Path.Combine(Root, "adir");
Directory.CreateDirectory(dirAsFile);
var missing = Path.Combine(Root, "nope", "x.txt");
var plainFile = Path.Combine(Root, "plain.txt");
File.WriteAllText(plainFile, "hello");

Console.WriteLine("=== A. 目录类 ===");
// A1 Directory.CreateDirectory 撞到一个**已存在的同名文件** ⇒ 真实场景：%TEMP% 下同名文件占位
var collide = Path.Combine(Root, "collide");
File.WriteAllText(collide, "x");
Probe("A1 Directory.CreateDirectory 撞同名文件", () => Directory.CreateDirectory(collide));
// A2 Directory.Delete 不存在
Probe("A2 Directory.Delete 不存在", () => Directory.Delete(Path.Combine(Root, "ghost")));
// A3 Directory.Delete 非空
Directory.CreateDirectory(Path.Combine(dirAsFile, "inner"));
Probe("A3 Directory.Delete 非空目录（无 recursive）", () => Directory.Delete(dirAsFile));
Directory.Delete(Path.Combine(dirAsFile, "inner"));
// A4 Directory.GetFiles 不存在
Probe("A4 Directory.GetFiles 目录不存在", () => Directory.GetFiles(Path.Combine(Root, "ghost2")));

Console.WriteLine();
Console.WriteLine("=== B. 写盘类 ===");
// B1 目标目录不存在
Probe("B1 File.WriteAllText 父目录不存在", () => File.WriteAllText(missing, "x"));
await ProbeAsync("B2 File.WriteAllTextAsync 父目录不存在", () => File.WriteAllTextAsync(missing, "x"));
// B3 目标是目录
Probe("B3 File.WriteAllText 目标是目录", () => File.WriteAllText(dirAsFile, "x"));
// B4 File.Create 目标是目录
Probe("B4 File.Create 目标是目录", () => { using var fs = File.Create(dirAsFile); });
// B5 new StreamWriter 目标是目录
Probe("B5 new StreamWriter 目标是目录", () => { using var w = new StreamWriter(dirAsFile); });

Console.WriteLine();
Console.WriteLine("=== C. 读取类 ===");
// C1 文件不存在（同步）
Probe("C1 File.ReadAllText 文件不存在", () => File.ReadAllText(Path.Combine(Root, "ghost.txt")));
// C2 文件不存在（异步）
await ProbeAsync("C2 File.ReadAllTextAsync 文件不存在", () => File.ReadAllTextAsync(Path.Combine(Root, "ghost.txt")));
// C3 目标是目录
await ProbeAsync("C3 File.ReadAllTextAsync 目标是目录", () => File.ReadAllTextAsync(dirAsFile));
// C4 File.OpenRead 不存在
Probe("C4 File.OpenRead 不存在", () => { using var s = File.OpenRead(Path.Combine(Root, "ghost.txt")); });
// C5 new StreamReader 不存在
Probe("C5 new StreamReader 不存在", () => { using var r = new StreamReader(Path.Combine(Root, "ghost.txt")); });
// C6 File.OpenRead 目标是目录
Probe("C6 File.OpenRead 目标是目录", () => { using var s = File.OpenRead(dirAsFile); });

Console.WriteLine();
Console.WriteLine("=== D. 移动 / 复制 / 删除 ===");
// D1 File.Move 源不存在
Probe("D1 File.Move 源不存在", () => File.Move(Path.Combine(Root, "ghost.txt"), Path.Combine(Root, "z.txt")));
// D2 File.Move 目标已存在（Linux .NET 9+ 默认不覆盖 ⇒ IOException）
Probe("D2 File.Move 目标已存在", () => File.Move(plainFile, plainFile));
// D3 File.Copy 源不存在
Probe("D3 File.Copy 源不存在", () => File.Copy(Path.Combine(Root, "ghost.txt"), Path.Combine(Root, "z.txt")));
// D4 File.Copy 目标目录不存在
Probe("D4 File.Copy 目标目录不存在", () => File.Copy(plainFile, missing));
// D5 File.Delete 目录
Probe("D5 File.Delete 目标是目录", () => File.Delete(dirAsFile));
// D6 File.Delete 不存在（预期不抛）
Probe("D6 File.Delete 不存在", () => File.Delete(Path.Combine(Root, "ghost.txt")));

Console.WriteLine();
Console.WriteLine("=== E. 容器 / 反序列化 ===");
// E1 非 ZIP
Probe("E1 new ZipArchive 非 ZIP 字节", () =>
{
    using var ms = new MemoryStream(Encoding.UTF8.GetBytes("this is definitely not a zip archive"));
    using var z = new ZipArchive(ms, ZipArchiveMode.Read);
});
// E2 坏 JSON。
// ⚠ 用 `JsonDocument.Parse` 而不是 `JsonSerializer.Deserialize<T>`：file-based app 的隐式属性里
//    反射序列化是被禁的（`JsonSerializerIsReflectionEnabledByDefault=false`），`Deserialize<T>`
//    会先抛 `InvalidOperationException`（"Reflection-based serialization has been disabled…"）
//    —— 那是**探针运行条件**造成的，不是仓库事实，取不到 `JsonException` 的读数。
//    `JsonDocument.Parse` 抛的就是 `JsonException`，与应用里 `JsonSerializer` 失败**同一类型**。
Probe("E2 JsonDocument.Parse 坏 JSON", () => JsonDocument.Parse("{ 这不是 JSON"));

Console.WriteLine();
Console.WriteLine("=== F. 环境相关（区分能力探针）===");
// F1 磁盘写满：/dev/full 在 Linux 上写必 ENOSPC（⚠ 消息里带的是 `/dev/full`，不是 Root）
Probe("F1 File.WriteAllText 写 /dev/full（ENOSPC）", () => File.WriteAllText("/dev/full", "x"), "/dev/full");
// F2 单段文件名超 NAME_MAX(255)
Probe("F2 文件名超长（NAME_MAX）", () => File.WriteAllText(Path.Combine(Root, new string('a', 300)), "x"));
// F3 全路径超长
Probe("F3 整条路径超长", () =>
{
    var deep = Root;
    for (var i = 0; i < 40; i++) deep = Path.Combine(deep, new string('b', 200));
    File.WriteAllText(Path.Combine(deep, "x.txt"), "x");
});
// F4 路径含 \0
Probe("F4 路径含 \\0", () => File.WriteAllText(Path.Combine(Root, "in\0valid.txt"), "x"));
// F5 路径为空
Probe("F5 路径为空串", () => File.WriteAllText("", "x"));

Console.WriteLine();
Console.WriteLine("=== G. CurrentUICulture 切 en/zh：消息变不变 ===");
foreach (var cu in new[] { "en-US", "zh-CN" })
{
    CultureInfo.CurrentUICulture = new CultureInfo(cu);
    Console.WriteLine($"-- [{cu}] --");
    try { File.ReadAllText(Path.Combine(Root, "ghost.txt")); }
    catch (Exception ex) { Console.WriteLine($"   FileNotFound      : {ex.Message.Replace("\n", "\\n")}"); }
    try { File.WriteAllText(missing, "x"); }
    catch (Exception ex) { Console.WriteLine($"   DirNotFound       : {ex.Message.Replace("\n", "\\n")}"); }
    // ⚠ 每轮**新建**一个非空目录：复用同一个的话，第一次 Delete 失败会留下它、第二次就变成
    //   "目录不存在" —— 那会把 `DirNotEmpty` 这一行打成**假读数**（阶段 85 首跑实测踩到）。
    var busy = Path.Combine(Root, "busy-" + cu);
    Directory.CreateDirectory(busy);
    File.WriteAllText(Path.Combine(busy, "f.txt"), "x");
    try { Directory.Delete(busy); }
    catch (Exception ex) { Console.WriteLine($"   DirNotEmpty       : {ex.Message.Replace("\n", "\\n")}"); }
    try { JsonDocument.Parse("{ 坏"); }
    catch (Exception ex) { Console.WriteLine($"   JsonException     : {ex.Message.Replace("\n", "\\n")}"); }
}

Console.WriteLine();
Console.WriteLine("=== H. 汇总：类型 → 出现次数 / 是否带路径 ===");
foreach (var g in seen.GroupBy(s => s.Type).OrderBy(g => g.Key, StringComparer.Ordinal))
    Console.WriteLine($"    {g.Key,-32} {g.Count(),2} 次   带路径 {g.Count(x => x.HasPath)}/{g.Count(x => x.Type != "（不抛）")}");

Directory.Delete(Root, true);
