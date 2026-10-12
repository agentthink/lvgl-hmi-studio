using HmiEditor.Device.Firmware;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.Device.Tests;

/// <summary>
/// FEL 烧录链路的**可软件验证**部分（阶段 121）。此前 <c>DeviceFlasher</c> 在 <c>tests/</c> 里
/// **零引用** —— 参数拼装、退出码判定、输出收集全都没判据。
///
/// <para>不需要真板子：用一个假 <c>sunxi-fel</c>（shell 脚本）把收到的 argv 打出来，
/// 断言①模板按空格切分且 <c>{image}</c> 被替换、②含空格的镜像路径**作为一个实参**原样到达
/// （走 <c>ArgumentList</c>，不经 shell）、③退出码非 0 时 <c>Success=false</c>。</para>
/// </summary>
public class DeviceFlasherTests
{
    /// <summary>
    /// 本仓库的开发/CI 平台是 Linux；假工具是 shell 脚本，仅 Unix 可执行。
    /// ⚠ 平台判定必须写在**调用 `File.SetUnixFileMode` 的同一个方法里**：
    /// 平台兼容性分析器（CA1416）只认这种"支配该调用"的守卫形状，抽到调用方它就看不见了。
    /// </summary>
    private static string WriteFakeTool(string root, string body)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "本用例的假 sunxi-fel 是 shell 脚本，仅 Unix 可执行；换平台请补等价的工具桩");
        }

        var tool = Path.Combine(root, "sunxi-fel-fake.sh");
        File.WriteAllText(tool, "#!/bin/sh\n" + body);
        File.SetUnixFileMode(tool,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return tool;
    }

    [Fact]
    public async Task FelFlash_TokenizesTemplate_AndKeepsImagePathAsOneArgument()
    {
        var root = TestTempDir.NewDir("fel-ok");
        try
        {
            var image = Path.Combine(root, "a b.bin");   // 路径**含空格**
            File.WriteAllBytes(image, new byte[] { 1, 2, 3 });
            var tool = WriteFakeTool(root,
                "printf 'ARGV:%s\\n' \"$@\"\necho DONE\nexit 0\n");

            var flasher = new DeviceFlasher();
            var logs = new List<string>();
            flasher.Log += logs.Add;

            var result = await flasher.FelFlashAsync(new FelFlashOptions
            {
                ToolPath = tool,
                ImagePath = image,
                ArgumentsTemplate = "-p spiflash-write 0 {image} --extra",
            });

            Assert.True(result.Success);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("ARGV:-p", result.Output);
            Assert.Contains("ARGV:spiflash-write", result.Output);
            Assert.Contains("ARGV:0", result.Output);
            // {image} 含空格 ⇒ 必须仍然是**一个** argv 元素（ArgumentList，不是拼 shell 字符串）
            Assert.Contains($"ARGV:{image}", result.Output);
            Assert.Contains("ARGV:--extra", result.Output);
            Assert.Contains("DONE", result.Output);
            Assert.NotEmpty(logs);
        }
        finally { TestTempDir.DeleteDir(root); }
    }

    [Fact]
    public async Task FelFlash_NonZeroExit_ReportsFailureWithCode()
    {
        var root = TestTempDir.NewDir("fel-fail");
        try
        {
            var image = Path.Combine(root, "fw.bin");
            File.WriteAllBytes(image, new byte[] { 9 });
            var tool = WriteFakeTool(root, "echo boom >&2\nexit 3\n");

            var flasher = new DeviceFlasher();
            var result = await flasher.FelFlashAsync(new FelFlashOptions
            {
                ToolPath = tool,
                ImagePath = image,
            });

            Assert.False(result.Success);
            Assert.Equal(3, result.ExitCode);
            Assert.Contains("boom", result.Output);
        }
        finally { TestTempDir.DeleteDir(root); }
    }
}
