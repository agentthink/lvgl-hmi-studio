using HmiEditor.Device.Build;
using Xunit;

namespace HmiEditor.Device.Tests;

/// <summary>
/// T113SoBuilder 测试：SDK/工具链自动探测、构建上下文解析、交叉编译命令流。
///
/// <para><b>临时目录分两类，不要一刀切</b>：</para>
/// <list type="bullet">
/// <item><description><b>要空间</b>（只构造/断言自建树的结构）→ 用 <c>TestTempDir.NewDir</c>。走空间根，
/// 容量探测阈值高、回退链短，是本类绝大多数用例该走的路。</description></item>
/// <item><description><b>要位置</b>（断言「从某目录<b>向上搜索</b>找不到 SDK / 仓库根」）→ 必须用
/// <c>TestTempDir.NewIsolatedDir</c>，起点落在仓库之外。否则搜索会向上逃逸进真实仓库，
/// 把「配置无效应返回 null」翻成真实的 <c>firmware/Tina-SDK</c>，测试假绿。</description></item>
/// </list>
/// <para>本类属于第二类的只有 3 个：<c>FindSdkRoot_InvalidConfiguredPath_FallsBackToSearchStart</c>、
/// <c>ResolveContext_MissingSdk_ReportsMissingItems</c>、<c>BuildAsync_MissingSdk_FailsWithClearError</c>。
/// 反例：把只需空间的用例一并塞进隔离根（容量探测只 256KB，本机 <c>/tmp</c> 仅 10MB tmpfs），
/// 全量并发时会随机 ENOSPC 变红 —— 见 <c>IsKitRoot_WithMarker_ReturnsTrue</c> 的历史红。</para>
/// </summary>
public class T113SoBuilderTests
{
    /// <summary>构造最小可用 SDK 树（repo/firmware/Tina-SDK + firmware/t113），返回 SDK 根。</summary>
    private static string CreateSdkTree(string root)
    {
        var repo = Path.Combine(root, "repo");
        var sdk = Path.Combine(repo, "firmware", "Tina-SDK");

        var bin = Path.Combine(sdk, "out", "t113-mq_r", "staging_dir", "toolchain", "bin");
        Directory.CreateDirectory(bin);
        var gcc = Path.Combine(bin, "arm-openwrt-linux-muslgnueabi-gcc");
        File.WriteAllText(gcc, "#!/bin/sh\nexit 0\n");

        var lvgl = Path.Combine(sdk, "package", "gui", "lvgl");
        Directory.CreateDirectory(lvgl);
        File.WriteAllText(Path.Combine(lvgl, "lvgl.h"), "/* lvgl */\n");

        var staging = Path.Combine(sdk, "out", "t113-mq_r", "staging_dir", "target", "usr", "include");
        Directory.CreateDirectory(Path.Combine(staging, "freetype2"));
        File.WriteAllText(Path.Combine(staging, "freetype2", "ft2build.h"), "/* ft */\n");

        var fw = Path.Combine(repo, "firmware", "t113");
        Directory.CreateDirectory(Path.Combine(fw, "config"));
        File.WriteAllText(Path.Combine(fw, "config", "lv_conf.h"), "#define LV_USE_FREETYPE 1\n");
        Directory.CreateDirectory(Path.Combine(fw, "app", "runtime"));
        File.WriteAllText(Path.Combine(fw, "app", "runtime", "screen_manager.h"),
            "typedef struct { int x; } screen_layer_ref_t;\n");
        Directory.CreateDirectory(Path.Combine(fw, "generated"));
        File.WriteAllText(Path.Combine(fw, "generated", "tags.h"), "enum { TAG_COUNT = 0 };\n");

        return sdk;
    }

    /// <summary>构造最小独立交叉编译工具包（t113-cross-kit 布局），返回工具包根。</summary>
    private static string CreateKitTree(string root)
    {
        var kit = Path.Combine(root, "kit");
        Directory.CreateDirectory(kit);
        File.WriteAllText(Path.Combine(kit, "kit.json"),
            "{\"kind\":\"zhishu-hmi-t113-cross-kit\",\"version\":1}");

        var bin = Path.Combine(kit, "toolchain", "bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "arm-openwrt-linux-muslgnueabi-gcc"),
            "#!/bin/sh\nexit 0\n");

        Directory.CreateDirectory(Path.Combine(kit, "lvgl"));
        File.WriteAllText(Path.Combine(kit, "lvgl", "lvgl.h"), "/* lvgl */\n");

        var sysroot = Path.Combine(kit, "sysroot", "usr", "include");
        Directory.CreateDirectory(Path.Combine(sysroot, "freetype2"));
        File.WriteAllText(Path.Combine(sysroot, "freetype2", "ft2build.h"), "/* ft */\n");

        Directory.CreateDirectory(Path.Combine(kit, "firmware", "config"));
        File.WriteAllText(Path.Combine(kit, "firmware", "config", "lv_conf.h"), "#define LV_USE_FREETYPE 1\n");
        Directory.CreateDirectory(Path.Combine(kit, "firmware", "app", "runtime"));
        File.WriteAllText(Path.Combine(kit, "firmware", "app", "runtime", "screen_manager.h"),
            "typedef struct { int x; } screen_layer_ref_t;\n");
        Directory.CreateDirectory(Path.Combine(kit, "firmware", "generated"));
        File.WriteAllText(Path.Combine(kit, "firmware", "generated", "tags.h"), "enum { TAG_COUNT = 0 };\n");

        return kit;
    }

    [Fact]
    public void IsKitRoot_WithMarker_ReturnsTrue()
    {
        var root = TestTempDir.NewDir("kit");
        var kit = CreateKitTree(root);
        try
        {
            Assert.True(T113SoBuilder.IsKitRoot(kit));
            Assert.False(T113SoBuilder.IsKitRoot(Path.Combine(kit, "toolchain")));
        }
        finally
        {
            TestTempDir.DeleteDir(root);
        }
    }

    [Fact]
    public void FindSdkRoot_KitDirectory_ReturnsKit()
    {
        var root = TestTempDir.NewDir("kit");
        var kit = CreateKitTree(root);
        var prevEnv = Environment.GetEnvironmentVariable("ZHSHU_HMI_SDK");
        Environment.SetEnvironmentVariable("ZHSHU_HMI_SDK", "/no/such/sdk");
        try
        {
            Assert.Equal(kit, T113SoBuilder.FindSdkRoot(null, kit));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ZHSHU_HMI_SDK", prevEnv);
            TestTempDir.DeleteDir(root);
        }
    }

    [Fact]
    public void ResolveContext_KitLayout_NoMissing()
    {
        var root = TestTempDir.NewDir("kit");
        var kit = CreateKitTree(root);
        try
        {
            var srcDir = Path.Combine(root, "out");
            Directory.CreateDirectory(srcDir);
            var src = Path.Combine(srcDir, "generated_hmi.c");
            File.WriteAllText(src, "int x;\n");

            var ctx = T113SoBuilder.ResolveContext(new T113SoBuildOptions
            {
                SourcePath = src,
                OutputDir = srcDir,
                SdkRoot = kit
            }, root);

            Assert.Empty(ctx.Missing);
            Assert.EndsWith(Path.Combine("toolchain", "bin", "arm-openwrt-linux-muslgnueabi-gcc"), ctx.CompilerPath);
            Assert.Equal(Path.Combine(kit, "lvgl"), ctx.LvglRoot);
            Assert.Equal(Path.Combine(kit, "firmware", "app"), ctx.AppDir);
            Assert.Equal(Path.Combine(kit, "firmware", "config"), ctx.ConfigDir);
            Assert.Equal(Path.Combine(kit, "sysroot", "usr", "include"), ctx.StagingInclude);
            Assert.Equal(kit, ctx.RepoRoot);
        }
        finally
        {
            TestTempDir.DeleteDir(root);
        }
    }

    [Fact]
    public void SysrootRootOf_IncludePath_ReturnsRoot()
    {
        Assert.Equal("/x/sysroot", T113SoBuilder.SysrootRootOf("/x/sysroot/usr/include"));
        Assert.Null(T113SoBuilder.SysrootRootOf(""));
    }

    [Fact]
    public void FindSdkRoot_ConfiguredPath_ReturnsFullPath()
    {
        var root = TestTempDir.NewDir("sdk");
        var sdk = CreateSdkTree(root);
        try
        {
            Assert.Equal(sdk, T113SoBuilder.FindSdkRoot(sdk));
        }
        finally
        {
            TestTempDir.DeleteDir(root);
        }
    }

    [Fact]
    public void FindSdkRoot_InvalidConfiguredPath_FallsBackToSearchStart()
    {
        // 配置路径无效时回退到向上查找；searchStart 指定为空目录 → 返回 null
        var root = TestTempDir.NewIsolatedDir("sdk");
        var prevEnv = Environment.GetEnvironmentVariable("ZHSHU_HMI_SDK");
        Environment.SetEnvironmentVariable("ZHSHU_HMI_SDK", "/no/such/sdk");
        try
        {
            Assert.Null(T113SoBuilder.FindSdkRoot("/no/such/sdk", root));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ZHSHU_HMI_SDK", prevEnv);
            TestTempDir.DeleteDir(root);
        }
    }

    [Fact]
    public void FindToolchainGcc_InSdk_FindsCompiler()
    {
        var root = TestTempDir.NewDir("sdk");
        var sdk = CreateSdkTree(root);
        try
        {
            var gcc = T113SoBuilder.FindToolchainGcc(null, sdk);
            Assert.NotNull(gcc);
            Assert.EndsWith("arm-openwrt-linux-muslgnueabi-gcc", gcc);
        }
        finally
        {
            TestTempDir.DeleteDir(root);
        }
    }

    [Fact]
    public void FindRepoRoot_FromSdk_ReturnsRepo()
    {
        var root = TestTempDir.NewDir("sdk");
        var sdk = CreateSdkTree(root);
        try
        {
            Assert.Equal(Path.Combine(root, "repo"), T113SoBuilder.FindRepoRoot(sdk));
        }
        finally
        {
            TestTempDir.DeleteDir(root);
        }
    }

    [Fact]
    public void ResolveContext_CompleteTree_NoMissing()
    {
        var root = TestTempDir.NewDir("sdk");
        var sdk = CreateSdkTree(root);
        try
        {
            var srcDir = Path.Combine(root, "out");
            Directory.CreateDirectory(srcDir);
            var src = Path.Combine(srcDir, "generated_hmi.c");
            File.WriteAllText(src, "int x;\n");
            var tags = Path.Combine(srcDir, "tags.h");
            File.WriteAllText(tags, "enum { TAG_COUNT = 0 };\n");

            var ctx = T113SoBuilder.ResolveContext(new T113SoBuildOptions
            {
                SourcePath = src,
                OutputDir = srcDir,
                TagsHeaderPath = tags,
                SdkRoot = sdk
            }, root);

            Assert.Empty(ctx.Missing);
            Assert.EndsWith("arm-openwrt-linux-muslgnueabi-gcc", ctx.CompilerPath);
            Assert.Contains("package", ctx.LvglRoot);
            Assert.True(Directory.Exists(Path.Combine(ctx.StagingInclude, "freetype2")));
        }
        finally
        {
            TestTempDir.DeleteDir(root);
        }
    }

    [Fact]
    public void ResolveContext_MissingSdk_ReportsMissingItems()
    {
        var root = TestTempDir.NewIsolatedDir("sdk");
        var prevEnv = Environment.GetEnvironmentVariable("ZHSHU_HMI_SDK");
        Environment.SetEnvironmentVariable("ZHSHU_HMI_SDK", "/no/such/sdk");
        try
        {
            var src = Path.Combine(root, "generated_hmi.c");
            File.WriteAllText(src, "int x;\n");

            // searchStart=root（无 SDK），SdkRoot 无效 → 全部缺失项被报告
            var ctx = T113SoBuilder.ResolveContext(new T113SoBuildOptions
            {
                SourcePath = src,
                OutputDir = root,
                SdkRoot = "/no/such/sdk"
            }, root);

            Assert.Contains(ctx.Missing, m => m.Contains("Tina SDK"));
            Assert.Contains(ctx.Missing, m => m.Contains("screen_manager.h"));
            Assert.Contains(ctx.Missing, m => m.Contains("tags.h"));
        }
        finally
        {
            TestTempDir.DeleteDir(root);
        }
    }

    [Fact]
    public void BuildArguments_ContainsExpectedFlagsAndIncludes()
    {
        var root = TestTempDir.NewDir("sdk");
        var sdk = CreateSdkTree(root);
        try
        {
            var srcDir = Path.Combine(root, "out");
            Directory.CreateDirectory(srcDir);
            var src = Path.Combine(srcDir, "generated_hmi.c");
            File.WriteAllText(src, "int x;\n");

            var ctx = T113SoBuilder.ResolveContext(new T113SoBuildOptions
            {
                SourcePath = src,
                OutputDir = srcDir,
                SdkRoot = sdk
            });
            var args = T113SoBuilder.BuildArguments(ctx, "/mnt/UDISK/hmi/assets", root);

            Assert.Contains("-std=c99", args);
            Assert.Contains("-fPIC", args);
            Assert.Contains(args, a => a.Contains("-DSIM_ASSET_DIR=\"/mnt/UDISK/hmi/assets\""));
            Assert.Contains(args, a => a.StartsWith("-I") && a.Contains("lvgl"));
            Assert.Contains(args, a => a.StartsWith("-I") && a.Contains("runtime"));
            Assert.Contains(args, a => a.StartsWith("-I") && a.Contains("freetype2"));
            Assert.Contains("-c", args);
            var argsList = args.ToList();
            Assert.Equal(src, argsList[argsList.IndexOf("-c") + 1]);
        }
        finally
        {
            TestTempDir.DeleteDir(root);
        }
    }

    [Fact]
    public async Task BuildAsync_WithFakeGcc_ProducesSharedLibrary()
    {
        // 假 gcc 是 shell 脚本 ⇒ 仅 Unix 可执行。用**显式失败**而不是 `if (windows) return;`：
        // 后者是"静默通过"，会让"这条链路在别的平台上根本没验"永远看不见（阶段 120）。
        // ⚠ 仍要写成平台守卫的形状（含 `return`）：平台兼容性分析器（CA1416）只认这种形状，
        //   否则下面 `File.SetUnixFileMode` 会报"可在所有平台上访问"的警告（门禁 0 警告基线）。
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "本用例的假 gcc 是 shell 脚本，仅 Unix 可执行；换平台请补等价的工具链桩");
        }
        var root = TestTempDir.NewDir("sdk");
        var sdk = CreateSdkTree(root);
        try
        {
            // 可执行假 gcc：校验 STAGING_DIR 环境变量已注入，并把 -o 后的文件写出来
            var gccPath = Path.Combine(sdk, "out", "t113-mq_r", "staging_dir", "toolchain", "bin",
                "arm-openwrt-linux-muslgnueabi-gcc");
            File.WriteAllText(gccPath,
                "#!/bin/sh\n" +
                "case \"$STAGING_DIR\" in *\"/staging_dir\") : ;; *) echo \"BAD STAGING_DIR: $STAGING_DIR\" >&2; exit 3;; esac\n" +
                "out=\"\"\nwhile [ $# -gt 0 ]; do\n  if [ \"$1\" = \"-o\" ]; then shift; out=\"$1\"; fi\n  shift\ndone\nif [ -n \"$out\" ]; then printf 'FAKE-ELF' > \"$out\"; fi\nexit 0\n");
            File.SetUnixFileMode(gccPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var srcDir = Path.Combine(root, "out");
            Directory.CreateDirectory(srcDir);
            var src = Path.Combine(srcDir, "generated_hmi.c");
            File.WriteAllText(src, "int x;\n");
            var tags = Path.Combine(srcDir, "tags.h");
            File.WriteAllText(tags, "enum { TAG_COUNT = 0 };\n");

            var builder = new T113SoBuilder();
            var logs = new List<string>();
            builder.Log += logs.Add;

            var result = await builder.BuildAsync(new T113SoBuildOptions
            {
                SourcePath = src,
                OutputDir = srcDir,
                TagsHeaderPath = tags,
                SdkRoot = sdk
            });

            Assert.True(result.Success, string.Join(" | ", result.Logs));
            Assert.NotNull(result.OutputPath);
            Assert.True(File.Exists(result.OutputPath));
            Assert.Equal("libhmi_ui.so", Path.GetFileName(result.OutputPath));
            Assert.Contains(logs, l => l.Contains("已生成"));
        }
        finally
        {
            TestTempDir.DeleteDir(root);
        }
    }

    [Fact]
    public async Task BuildAsync_MissingSdk_FailsWithClearError()
    {
        var root = TestTempDir.NewIsolatedDir("sdk");
        var prevEnv = Environment.GetEnvironmentVariable("ZHSHU_HMI_SDK");
        Environment.SetEnvironmentVariable("ZHSHU_HMI_SDK", "/no/such/sdk");
        try
        {
            var src = Path.Combine(root, "generated_hmi.c");
            File.WriteAllText(src, "int x;\n");

            var result = await new T113SoBuilder().BuildAsync(new T113SoBuildOptions
            {
                SourcePath = src,
                OutputDir = root,
                SdkRoot = "/no/such/sdk"
            }, root);

            Assert.False(result.Success);
            Assert.Contains("Tina SDK", result.Error);
        }
        finally
        {
            TestTempDir.DeleteDir(root);
        }
    }
}
