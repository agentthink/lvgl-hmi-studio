// 阶段 84 探针：C 类（登记 #61b）的产出方全是 BCL / IO。
//
// 要回答的问题（**不是推理题，是读数题**）：
//   「把失败文案改成『操作名 + 目标路径 + 原因』，用户能多看到什么？」
//   —— 只在**原因那一半不带对象**时才有收益。而 BCL 的 IO 异常**自带路径**。
//
// 跑法（.NET 10 file-based app，不需要 csproj、不落 obj/ 到仓库）：
//     dotnet run tools/probe-bcl-io-messages.cs
//
// 读数落盘（阶段 84 的实测记录）：
//     .workbuddy/tmp/s84-io-msg.txt
//
// 结论（2026-09-23 实测，逐条见 docs/2026-09-23-exception-passthrough-triage.md §84）：
//   ① 6 个场景里 5 个真的抛，**全部自带目标路径**（`Access to the path '…' is denied.` /
//      `Could not find a part of the path '…'.`）⇒ 再包一层「—— <路径>」是**冗余**，
//      且 SaveToPath 那条出现的是 **`.tmp` 兄弟路径**（用户看到的是中间文件名，比没有更困惑）。
//   ② `CurrentUICulture` 切成 zh-CN / en-US，消息**一字不变**（都是英文）⇒ 这就是登记 #59 的原形状：
//      BCL 原文在中文界面下是中英混排，而且**没法翻译**。⇒「不该本地化」的定性成立（阶段 83），
//      但「用户缺的是『是哪个文件』」**不成立**（路径本来就在）。
//   ③ `Path.Combine` 对含 `\0` 的路径**不抛** ⇒ `BuildPipelineService` 的 try 里它不构成失败来源。
//
// ⚠ 姊妹读数（**headless 里不可驱动，故不在本探针内**）：`asset.import.fail` 的图片批量档
//   产出方含 `new Avalonia.Media.Imaging.Bitmap(path)`。阶段 84 在一个**临时**测试里实测过
//   headless 下的它：**文件的、文本的、空的、截断的、乃至真实的 4×4 PNG，全部不抛且
//   `PixelSize` 恒为 (1,1)** —— 真实 PNG 也是 (1,1) ⇒ **headless 的解码器是桩，不是"宽容"**。
//   ⇒ 该档在 headless 里**不可实测**（与阶段 82 的 `IStorageProvider` 同款处境），
//   且该档**结构上拿不到单一路径**（`path` 声明在 foreach 内）⇒ 即使真机上它不带路径，
//   收益也拿不到。（探针跑完即删，形状记在专题文档 §84.2。）

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

const string Root = "/tmp/probe84";

void Probe(string label, Action act)
{
    try { act(); Console.WriteLine($"[{label}] 没有抛 —— 该档不成立"); }
    catch (Exception ex)
    {
        var msg = ex.Message.Replace("\n", "\\n");
        Console.WriteLine($"[{label}] {ex.GetType().Name}");
        Console.WriteLine($"        {msg}");
        Console.WriteLine($"        含目标路径? {msg.Contains(Root, StringComparison.Ordinal)}");
    }
}

async Task ProbeAsync(string label, Func<Task> act)
{
    try { await act(); Console.WriteLine($"[{label}] 没有抛 —— 该档不成立"); }
    catch (Exception ex)
    {
        var msg = ex.Message.Replace("\n", "\\n");
        Console.WriteLine($"[{label}] {ex.GetType().Name}");
        Console.WriteLine($"        {msg}");
        Console.WriteLine($"        含目标路径? {msg.Contains(Root, StringComparison.Ordinal)}");
    }
}

if (Directory.Exists(Root)) Directory.Delete(Root, true);Directory.CreateDirectory(Root);

// 场景 A：SaveToPath —— File.Create(path + ".tmp")，而 path+".tmp" 已是一个**目录**
//（root 下建 "demo.hmi.tmp" 目录，保存目标 root/demo.hmi ⇒ tempPath = root/demo.hmi.tmp）
Directory.CreateDirectory(Path.Combine(Root, "demo.hmi.tmp"));
Probe("A. File.Create(已存在的目录名)", () =>
{
    using var fs = File.Create(Path.Combine(Root, "demo.hmi.tmp"));
});

// 场景 B：目标目录不存在（导出 / CSV 导出的典型失败）
await ProbeAsync("B. File.WriteAllTextAsync(目录不存在)", () =>
    File.WriteAllTextAsync(Path.Combine(Root, "nope", "out.csv"), "x"));

// 场景 C：目标是目录（导出到目录名）
Probe("C. File.WriteAllText(目标是目录)", () =>
    File.WriteAllText(Path.Combine(Root, "demo.hmi.tmp"), "x"));

// 场景 D：读目录（资产 / CSV 导入的典型失败）
await ProbeAsync("D. File.ReadAllTextAsync(目标是目录)", () =>
    File.ReadAllTextAsync(Path.Combine(Root, "demo.hmi.tmp")));

// 场景 E：StreamWriter 包一个目录流 —— RecipeEditorView 用的是 IStorageFile.OpenWriteAsync，
// 拿不到那个句柄，用 FileStream 复刻"打开目录当文件"
Probe("E. new StreamWriter(其实拿到的是目录)", () =>
{
    using var fs = new FileStream(Path.Combine(Root, "demo.hmi.tmp"), FileMode.Open, FileAccess.Write);
    using var w = new StreamWriter(fs);
});

// 场景 F：Path.Combine 会不会抛（BuildPipelineService 把它放在 try 内）
Probe("F. Path.Combine(含 \\0 的路径)", () =>
{
    var bad = Path.Combine(Root, "in\0valid.txt");
    Console.WriteLine("        " + bad);
});

Console.WriteLine();
Console.WriteLine("=== CurrentUICulture 切 en/zh：消息是否跟着变 ===");
foreach (var cu in new[] { "en-US", "zh-CN" })
{
    CultureInfo.CurrentUICulture = new CultureInfo(cu);
    Console.Write($"[{cu}] ");
    try { using var fs = File.Create(Path.Combine(Root, "demo.hmi.tmp")); }
    catch (Exception ex) { Console.WriteLine(ex.Message.Replace("\n", "\\n")); }
}

Directory.Delete(Root, true);
