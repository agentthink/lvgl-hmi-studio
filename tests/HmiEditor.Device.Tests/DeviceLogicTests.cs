using System.Text;
using HmiEditor.Device.Detection;
using HmiEditor.Device.Packaging;
using HmiEditor.Device.Updates;
using Xunit;

namespace HmiEditor.Device.Tests;

public class DeviceLogicTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("2.1.0", "2.1.0")]
    [InlineData("v2.1.0", "2.1.0")]
    [InlineData("V2.1.0 (build 42)", "2.1.0")]
    [InlineData("HMI version: 2.1.0-rc1", "2.1.0-rc1")]
    [InlineData("not a version", "")]
    public void ParseHmiVersion_ExtractsSemver(string raw, string expected)
    {
        Assert.Equal(expected, DeviceDetector.ParseHmiVersion(raw));
    }

    [Theory]
    [InlineData("VERSION_ID=\"1.2.3\"", "1.2.3")]
    [InlineData("NAME=\"Tina\"\nVERSION=\"2.0.0\"", "2.0.0")]
    [InlineData("BUILD_ID=20260807", "20260807")]
    [InlineData("", "")]
    public void ParseOsReleaseVersion_ExtractsVersion(string osRelease, string expected)
    {
        Assert.Equal(expected, DeviceDetector.ParseOsReleaseVersion(osRelease));
    }

    [Theory]
    [InlineData("2.1.0", "2.1.0", UpdateCheckResultKind.UpToDate)]
    [InlineData("2.2.0", "2.1.0", UpdateCheckResultKind.DeviceNewer)]
    [InlineData("2.1.0", "2.2.0", UpdateCheckResultKind.UpdateAvailable)]
    [InlineData("v2.1.0", "2.1.0", UpdateCheckResultKind.UpToDate)]
    [InlineData("2.10.0", "2.9.0", UpdateCheckResultKind.DeviceNewer)]
    [InlineData("2.1.0", "", UpdateCheckResultKind.Unknown)]
    [InlineData("", "2.1.0", UpdateCheckResultKind.DeviceNewer)]
    public void CheckForUpdate_ClassifiesVersions(string local, string device, UpdateCheckResultKind expected)
    {
        var result = HmiUpdateService.CheckForUpdate(local, device);
        Assert.Equal(expected, result.Kind);
    }

    [Theory]
    [InlineData("2.1.0", "2.1.0", 0)]
    [InlineData("2.2.0", "2.1.0", 1)]
    [InlineData("2.1.0", "2.2.0", -1)]
    [InlineData("2.1.0", "2.1.0-rc1", 0)] // 数字段相同视为同版本
    [InlineData("2.10.0", "2.9.9", 1)]
    [InlineData("", "1.0.0", -1)]
    [InlineData("1.0.0", "", 1)]
    public void CompareVersions_NumericComparison(string a, string b, int expectedSign)
    {
        var cmp = HmiUpdateService.CompareVersions(a, b);
        Assert.Equal(expectedSign, Math.Sign(cmp));
    }
}

public class UsbUpdatePackagerTests
{
    [Fact]
    public async Task BuildAsync_WithHmiLibrary_GeneratesSwuScriptAndReadme()
    {
        // Arrange
        var dir = TestTempDir.NewDir("usb");
        var soPath = Path.Combine(dir, "libhmi_ui.so");
        File.WriteAllBytes(soPath, Encoding.UTF8.GetBytes("ELF fake"));

        var packager = new UsbUpdatePackager();

        // Act
        var result = await packager.BuildAsync(new UsbUpdateOptions
        {
            OutputDirectory = dir,
            HmiLibraryPath = soPath,
            HmiVersion = "3.0.0"
        });

        // Assert
        Assert.NotNull(result.HmiSwuPath);
        Assert.True(File.Exists(result.HmiSwuPath));
        Assert.Equal($"hmi-3.0.0.swu", Path.GetFileName(result.HmiSwuPath));

        var script = File.ReadAllText(result.AutoInstallScriptPath);
        Assert.Contains("hmi_ota.sh", script);
        Assert.Contains("swupdate", script);

        var readme = File.ReadAllText(result.ReadmePath);
        Assert.Contains("3.0.0", readme);

        // swu 内容校验：默认无 sha256（兼容当前 T113 固件 swupdate），含 version 文件
        var entries = CpioTestReader.Read(File.ReadAllBytes(result.HmiSwuPath));
        Assert.Equal(4, entries.Count);
        Assert.DoesNotContain("sha256 = \"", Encoding.UTF8.GetString(entries[0].Data));
        Assert.Equal("3.0.0\n", Encoding.UTF8.GetString(entries[2].Data));

        TestTempDir.DeleteDir(dir);
    }

    [Fact]
    public async Task BuildAsync_WithIncludeHashes_GeneratesSha256Package()
    {
        var dir = TestTempDir.NewDir("usb");
        var soPath = Path.Combine(dir, "libhmi_ui.so");
        File.WriteAllBytes(soPath, Encoding.UTF8.GetBytes("ELF fake"));

        var result = await new UsbUpdatePackager().BuildAsync(new UsbUpdateOptions
        {
            OutputDirectory = dir,
            HmiLibraryPath = soPath,
            HmiVersion = "1.0.0",
            IncludeHashes = true
        });

        var entries = CpioTestReader.Read(File.ReadAllBytes(result.HmiSwuPath!));
        Assert.Contains("sha256 = \"", Encoding.UTF8.GetString(entries[0].Data));

        TestTempDir.DeleteDir(dir);
    }

    [Fact]
    public void BuildAsync_WithoutAnyPayload_Throws()
    {
        var dir = TestTempDir.NewDir("usb");
        var packager = new UsbUpdatePackager();

        Assert.Throws<ArgumentException>(() =>
            packager.BuildAsync(new UsbUpdateOptions { OutputDirectory = dir }).GetAwaiter().GetResult());

        TestTempDir.DeleteDir(dir);
    }

    [Fact]
    public void BuildAsync_WithInvalidFirmwareImageName_Throws()
    {
        var dir = TestTempDir.NewDir("usb");
        var packager = new UsbUpdatePackager();

        Assert.Throws<ArgumentException>(() =>
            packager.BuildAsync(new UsbUpdateOptions
            {
                OutputDirectory = dir,
                FirmwareImages = new Dictionary<string, string> { ["bogus"] = "/tmp/x" }
            }).GetAwaiter().GetResult());

        TestTempDir.DeleteDir(dir);
    }
}
