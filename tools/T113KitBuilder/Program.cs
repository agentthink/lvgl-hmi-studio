using System.Diagnostics;
using System.Text.Json;
using HmiEditor.Device.Build;

// ============================================================
// T113KitBuilder — 从 Tina SDK 提取"独立交叉编译工具包"（t113-cross-kit）
//
// 用途：客户机安装编辑器后无需完整 Tina SDK（几十 GB），只需本工具包
// （约 300MB），即可在 IDE 内完成 libhmi_ui.so 交叉编译（编译转换）。
// 开发机用 SDK 自动探测即可；给客户的包用本工具生成。
//
// 用法：
//   dotnet run --project tools/T113KitBuilder -- \
//       [--sdk <Tina SDK 根目录>] --out <工具包输出目录> [--zip]
//
// 工具包布局（与 HmiEditor.Device/T113SoBuilder 的 kit 探测约定一致）：
//   <out>/
//   ├── kit.json            kind = "zhishu-hmi-t113-cross-kit"
//   ├── toolchain/          预编译 ARM 工具链（可整体重定位，276MB）
//   ├── lvgl/               LVGL 源码根（lvgl.h + src，27MB）
//   ├── sysroot/usr/include 目标板 musl/freetype2 头文件
//   ├── firmware/
//   │   ├── config/         lv_conf.h / lv_drv_conf.h
//   │   ├── app/            screen_manager.h / hmi_ui_api.h 等契约头
//   │   └── generated/      默认 tags.h（兜底）
//   └── README.txt          客户使用说明
// ============================================================

const string KitKind = "zhishu-hmi-t113-cross-kit";
var sdk = "";
var outDir = "";
var zip = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--sdk": sdk = args[++i]; break;
        case "--out": outDir = args[++i]; break;
        case "--zip": zip = true; break;
        default:
            Console.WriteLine($"未知参数: {args[i]}（支持 --sdk <dir> --out <dir> [--zip]）");
            return 1;
    }
}
if (string.IsNullOrEmpty(outDir))
{
    Console.WriteLine("用法: T113KitBuilder [--sdk <SDK目录>] --out <工具包目录> [--zip]");
    return 1;
}

// 1. 定位 SDK（复用 T113SoBuilder 探测：配置 → 环境变量 → 向上查找）
var sdkRoot = T113SoBuilder.FindSdkRoot(sdk.Length > 0 ? sdk : null);
if (sdkRoot is null)
{
    Console.WriteLine("✗ 未找到 Tina SDK：请用 --sdk 指定，或设置 ZHSHU_HMI_SDK / 在仓库内运行");
    return 1;
}
Console.WriteLine($"[SDK] {sdkRoot}");

// 2. 定位固件契约头来源仓库（SDK 同级 firmware/t113）
var repoRoot = T113SoBuilder.FindRepoRoot(sdkRoot);
if (repoRoot is null)
{
    Console.WriteLine("✗ 未找到固件契约头（SDK 同级 firmware/t113/app/runtime）");
    return 1;
}
var fwBase = Path.Combine(repoRoot, "firmware", "t113");
Console.WriteLine($"[固件] {fwBase}");

// 3. 定位预编译工具链与 sysroot
var prebuilt = Path.Combine(sdkRoot, "prebuilt", "gcc", "linux-x86", "arm", "toolchain-sunxi-musl", "toolchain");
if (!Directory.Exists(Path.Combine(prebuilt, "bin")))
{
    Console.WriteLine($"✗ 未找到预编译工具链: {prebuilt}");
    return 1;
}
var stagingInclude = T113SoBuilder.FindStagingInclude(sdkRoot);
if (stagingInclude is null)
{
    Console.WriteLine("✗ 未找到 staging target usr/include（freetype2）");
    return 1;
}
var lvglSrc = Path.Combine(sdkRoot, "package", "gui", "lvgl");
if (!File.Exists(Path.Combine(lvglSrc, "lvgl.h")))
{
    Console.WriteLine($"✗ 未找到 LVGL 源码根: {lvglSrc}");
    return 1;
}

// 4. 复制组件（先清空旧输出，保证幂等）
outDir = Path.GetFullPath(outDir);
if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
var kitFw = Path.Combine(outDir, "firmware");
Console.WriteLine($"[输出] {outDir}");

CopyDir(prebuilt, Path.Combine(outDir, "toolchain"), "工具链");
CopyLvgl(lvglSrc, Path.Combine(outDir, "lvgl"));
CopyDir(stagingInclude, Path.Combine(outDir, "sysroot", "usr", "include"), "sysroot 头文件");
CopyDir(Path.Combine(fwBase, "config"), Path.Combine(kitFw, "config"), "固件配置");
CopyDir(Path.Combine(fwBase, "app"), Path.Combine(kitFw, "app"), "固件契约头");
CopyDir(Path.Combine(fwBase, "generated"), Path.Combine(kitFw, "generated"), "默认标签头");

// 5. 元数据 + 说明
var meta = new Dictionary<string, object>
{
    ["kind"] = KitKind,
    ["version"] = 1,
    ["created"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
    ["sourceSdk"] = sdkRoot,
    ["targetBoard"] = "t113-mq_r",
    ["toolchainTriplet"] = "arm-openwrt-linux-muslgnueabi",
    ["toolchainGcc"] = "toolchain/bin/arm-openwrt-linux-muslgnueabi-gcc",
    ["lvgl"] = "lvgl",
    ["sysrootInclude"] = "sysroot/usr/include",
    ["firmware"] = "firmware"
};
File.WriteAllText(Path.Combine(outDir, "kit.json"),
    JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

var lvglVer = "9.5";
try
{
    var vh = Path.Combine(lvglSrc, "lv_version.h");
    if (File.Exists(vh))
    {
        foreach (var line in File.ReadAllLines(vh))
        {
            if (line.Contains("LVGL_VERSION_MAJOR"))
                lvglVer = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Last().TrimEnd('U', 'u');
        }
    }
}
catch { }
File.WriteAllText(Path.Combine(outDir, "README.txt"), BuildReadme(lvglVer));

Console.WriteLine($"✓ 工具包生成完成: {outDir}");
Console.WriteLine($"  大小: {DirSize(outDir) / 1024.0 / 1024.0:F0} MB（完整 SDK 为几十 GB）");

if (zip)
{
    var zipPath = outDir.TrimEnd('/') + ".zip";
    Console.WriteLine($"[打包] {zipPath}");
    try
    {
        var psi = new ProcessStartInfo("zip", $"-r -q \"{zipPath}\" .")
        {
            WorkingDirectory = outDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = Process.Start(psi);
        p?.WaitForExit();
        if (p?.ExitCode == 0 && File.Exists(zipPath))
            Console.WriteLine($"✓ ZIP: {zipPath} ({new FileInfo(zipPath).Length / 1024.0 / 1024.0:F0} MB)");
        else
            Console.WriteLine("（zip 打包未完成；可直接整个目录分发给客户）");
    }
    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
    {
        // 无 zip 命令 → 回退 tar.gz（保留软链；Linux 客户直接解包）
        var tgz = outDir.TrimEnd('/') + ".tar.gz";
        try
        {
            var psi = new ProcessStartInfo("tar", $"-czf \"{tgz}\" -C \"{outDir}\" .")
            {
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
            if (p?.ExitCode == 0 && File.Exists(tgz))
                Console.WriteLine($"✓ TAR.GZ: {tgz} ({new FileInfo(tgz).Length / 1024.0 / 1024.0:F0} MB)");
            else
                Console.WriteLine("（打包未完成；可直接整个目录分发给客户）");
        }
        catch
        {
            Console.WriteLine("（未找到 zip/tar 命令，跳过打包；可直接整个目录分发给客户）");
        }
    }
}
return 0;

// ── 工具 ──

static void CopyDir(string src, string dst, string what)
{
    Directory.CreateDirectory(dst);
    // Linux 开发机优先 cp -a（保软链/权限/快）；不可用则逐项递归
    if (!OperatingSystem.IsWindows())
    {
        var psi = new ProcessStartInfo("cp", $"-a \"{src}/.\" \"{dst}/\"")
        {
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        using var p = Process.Start(psi);
        p?.WaitForExit();
        if (p?.ExitCode == 0)
        {
            Console.WriteLine($"  ✓ {what} → {dst}");
            return;
        }
    }
    foreach (var item in Directory.EnumerateFileSystemEntries(src))
    {
        var target = Path.Combine(dst, Path.GetFileName(item));
        if (Directory.Exists(item)) CopyDir(item, target, "");
        else if (File.Exists(item))
        {
            // 软链 → 重建软链；普通文件 → 复制
            var link = File.ResolveLinkTarget(item, returnFinalTarget: false);
            if (link is not null && File.GetAttributes(item).HasFlag(FileAttributes.ReparsePoint))
            {
                try
                {
                    File.CreateSymbolicLink(target, link.LinkTarget);
                    continue;
                }
                catch { }
            }
            File.Copy(item, target, true);
        }
    }
    Console.WriteLine($"  ✓ {what} → {dst}");
}

static void CopyLvgl(string src, string dst)
{
    // 仅复制编译所需：根头文件 + src/ + libs/；跳过 tests/build/demos/docs/scripts/examples 等
    Directory.CreateDirectory(dst);
    foreach (var f in Directory.EnumerateFiles(src))
        File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
    foreach (var sub in new[] { "src", "libs" })
    {
        var s = Path.Combine(src, sub);
        if (Directory.Exists(s)) CopyDir(s, Path.Combine(dst, sub), "");
    }
    Console.WriteLine($"  ✓ LVGL 头/源码 → {dst}（已剔除 tests/build/demos/docs/scripts/examples）");
}

static long DirSize(string dir)
{
    // Linux 用 du -sb（正确处理软链）；否则跳过软链递归统计
    if (!OperatingSystem.IsWindows())
    {
        try
        {
            var psi = new ProcessStartInfo("du", $"-sb \"{dir}\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
            var outText = p?.StandardOutput.ReadToEnd() ?? "";
            var first = outText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (long.TryParse(first, out var duBytes)) return duBytes;
        }
        catch { }
    }
    long total = 0;
    var opts = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, RecurseSubdirectories = true };
    foreach (var f in Directory.EnumerateFiles(dir, "*", opts))
    {
        try { total += new FileInfo(f).Length; } catch { }
    }
    return total;
}

static string BuildReadme(string lvglVer) => $"""
zhishu-HMI T113 交叉编译工具包（t113-cross-kit）
================================================

用途：客户机无需完整 Tina SDK（几十 GB），安装本工具包后即可在
zhishu-HMI 编辑器内完成「编译转换 → 交叉编译 libhmi_ui.so」。

内容：
  toolchain/    ARM 交叉工具链（arm-openwrt-linux-muslgnueabi-gcc）
  lvgl/         LVGL {lvglVer} 源码头（编译生成代码用）
  sysroot/      目标板 musl/freetype2 头文件
  firmware/     固件契约头（screen_manager.h/hmi_ui_api.h/tags.h/lv_conf.h）

编辑器配置（任选其一）：
  1. 设置 → 真机部署 → Tina SDK 根目录 = 本工具包目录；
  2. 或环境变量 ZHSHU_HMI_SDK = 本工具包目录。

注意：
  - 工具链为 Linux x86-64 原生程序：Windows 客户请配合 WSL2 使用；
  - 工具包随固件契约（ABI）版本更新而更新，请与固件版本配套分发。
""";
