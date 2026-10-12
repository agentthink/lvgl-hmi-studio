using System.Security.Cryptography;
using System.Text;
using HmiEditor.Device.Packaging;
using Xunit;

namespace HmiEditor.Device.Tests;

public class SwuPackageBuilderTests
{
    [Fact]
    public async Task BuildAsync_HmiTemplate_ReplacesSha256AndProducesExpectedStructure()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes("fake libhmi_ui.so content");
        var outputPath = TestTempDir.NewFilePath("hmi", ".swu");
        var builder = new SwuPackageBuilder();

        // Act
        await builder.BuildAsync(
            SwuTemplates.HmiUi,
            new[]
            {
                new SwuPayloadFile { CpioName = "libhmi_ui.so", Data = payload },
                new SwuPayloadFile { CpioName = "version", Data = Encoding.UTF8.GetBytes("2.1.0\n") }
            },
            outputPath,
            version: "2.1.0");

        // Assert
        var archive = File.ReadAllBytes(outputPath);
        var entries = CpioTestReader.Read(archive);

        Assert.Equal(4, entries.Count);
        Assert.Equal("sw-description", entries[0].Name);
        Assert.Equal("libhmi_ui.so", entries[1].Name);
        Assert.Equal("version", entries[2].Name);
        Assert.Equal("cpio_item_md5", entries[3].Name);

        // sw-description：sha256 占位符已被真实哈希替换，版本已更新
        var desc = Encoding.UTF8.GetString(entries[0].Data);
        Assert.DoesNotContain("@libhmi_ui.so", desc);
        Assert.DoesNotContain("@version", desc);
        Assert.Contains("version = \"2.1.0\"", desc);
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(payload));
        Assert.Contains($"sha256 = \"{expectedHash}\"", desc);

        // payload 数据完整
        Assert.Equal(payload, entries[1].Data);
        Assert.Equal("2.1.0\n", Encoding.UTF8.GetString(entries[2].Data));

        // cpio_item_md5：包含 sw-description 与 payload 的 md5 行
        var md5Text = Encoding.UTF8.GetString(entries[3].Data);
        Assert.Contains("  sw-description", md5Text);
        Assert.Contains("  libhmi_ui.so", md5Text);
        Assert.Contains("  version", md5Text);

        // 校验每行格式 md5 + 两空格 + 文件名
        var lines = md5Text.Trim().Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Matches(@"^[0-9a-f]{32}  \S+$", lines[0]);

        TestTempDir.DeleteFile(outputPath);
    }

    [Fact]
    public void BuildAsync_MissingReferencedPayload_Throws()
    {
        // HmiUi 模板引用 libhmi_ui.so 和 version，缺少任一 payload 应报错
        var outPath = TestTempDir.NewFilePath("x", ".swu");
        var builder = new SwuPackageBuilder();

        var ex = Assert.Throws<ArgumentException>(() =>
            builder.BuildAsync(
                SwuTemplates.HmiUi,
                new[] { new SwuPayloadFile { CpioName = "libhmi_ui.so", Data = new byte[] { 1, 2, 3 } } },
                outPath).GetAwaiter().GetResult());
        Assert.Contains("version", ex.Message);
    }

    [Fact]
    public async Task BuildAsync_FirmwareTemplate_AcceptsAllImages()
    {
        // Arrange
        var outPath = TestTempDir.NewFilePath("fw", ".swu");
        var builder = new SwuPackageBuilder();
        var payloads = SwuTemplates.FirmwareImageNames
            .Select((name, i) => new SwuPayloadFile
            {
                CpioName = name,
                Data = Encoding.UTF8.GetBytes($"image-{name}-{i}")
            })
            .ToArray();

        // Act
        await builder.BuildAsync(SwuTemplates.FirmwareUbi, payloads, outPath, version: "1.0.0");

        // Assert
        var entries = CpioTestReader.Read(File.ReadAllBytes(outPath));
        Assert.Equal(1 + payloads.Length + 1, entries.Count); // desc + 5 images + md5
        var desc = Encoding.UTF8.GetString(entries[0].Data);
        foreach (var name in SwuTemplates.FirmwareImageNames)
            Assert.DoesNotContain($"@{name}", desc); // 所有占位符均被替换
        Assert.Equal("cpio_item_md5", entries[^1].Name);

        TestTempDir.DeleteFile(outPath);
    }

    [Fact]
    public void BuildAsync_MissingPayloadFile_Throws()
    {
        var builder = new SwuPackageBuilder();
        var outPath = TestTempDir.NewFilePath("x", ".swu");

        Assert.Throws<FileNotFoundException>(() =>
            builder.BuildAsync(
                SwuTemplates.HmiUi,
                new[] { new SwuPayloadFile { CpioName = "libhmi_ui.so", LocalPath = "/nonexistent.so" } },
                outPath).GetAwaiter().GetResult());
    }
}
