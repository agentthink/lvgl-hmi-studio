using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **"可再导出"判据**（2026-10-11，阶段 309）—— 用仓内示例工程定义重跑设备侧导出，与夹具比对。
///
/// <para><b>背景</b>：阶段 307 把源工程入了仓（`firmware/t113/sample/MockChineseProject/`），
/// 阶段 308 用临时探针量出"夹具相对当前生成器已陈旧"。本判据把**能复现的那部分**钉死，
/// 并把**不能复现的那部分**作为读数打出来（不装作它们也复现了）。</para>
///
/// <para><b>实测（阶段 309，CLI `tools/DeviceExport` 与本案同源）</b>：
/// <c>generated_security.h</c> **逐字节相同**（7,731 B）· 4 个资产**逐字节相同**；
/// 而 <c>screens.c</c>（23,159 vs 23,429 B）与 <c>tags.h</c>/<c>tags.c</c>（2,608/106 vs 4,469/850 B）**不同** ——
/// 后者根因是**本工程当前的绑定算不出夹具那份标签表**（运行时标签数 = 0）。</para>
/// </summary>
public class DeviceExportReproducibilityTests
{
    private readonly ITestOutputHelper _o;

    public DeviceExportReproducibilityTests(ITestOutputHelper o) => _o = o;

    private static string Repo(string rel) =>
        Path.Combine(SourceScanner.RepoRoot, rel.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void GeneratedSecurityHeader_IsByteReproducibleFromTheInRepoSampleProject()
    {
        // ⚠ 必须走 TestTempDir：本机 /tmp 只有 10 MB，直接写它会随机假红
        //    （TestTempDirUsageGuardTests 就是为此存在的 —— 本判据首跑正是被它拦下的）
        var work = TestTempDir.NewDir("reexport");
        try
        {
            var project = ProjectSerializer.Deserialize(
                File.ReadAllText(Repo("firmware/t113/sample/MockChineseProject/project.json")));
            Assert.NotNull(project);

            var engine = new RuntimeBindingEngine(project!);
            engine.BuildRuntimeTags();
            // ⚠ 资产库必须建（阶段 313 的教训：传 null 会少发 IMG_PATH_* 声明 ⇒ 假差异）。
            //   条目取自工程定义里的 assets.json，文件指向设备侧 assets/。
            var library = new HmiEditor.Core.Models.AssetLibrary();
            var defDir = Path.GetDirectoryName(Repo("firmware/t113/sample/MockChineseProject/project.json"))!;
            using (var aj = JsonDocument.Parse(File.ReadAllText(Path.Combine(defDir, "assets.json"))))
                foreach (var e in aj.RootElement.GetProperty("assets").EnumerateArray())
                    library.AddAsset(new HmiEditor.Core.Models.AssetItem
                    {
                        Id = e.GetProperty("id").GetString() ?? "",
                        Name = e.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Type = (e.GetProperty("type").GetString() ?? "image") == "font"
                            ? HmiEditor.Core.Models.AssetType.Font : HmiEditor.Core.Models.AssetType.Image,
                        FilePath = Path.Combine(Repo("firmware/t113/generated/assets"),
                            Path.GetFileName(e.GetProperty("filePath").GetString() ?? "")),
                        SizeBytes = e.TryGetProperty("sizeBytes", out var s) ? s.GetInt64() : 0,
                        Width = e.TryGetProperty("width", out var w) ? w.GetInt32() : 0,
                        Height = e.TryGetProperty("height", out var h) ? h.GetInt32() : 0,
                        ColorDepth = e.TryGetProperty("colorDepth", out var cd) ? cd.GetInt32() : 0,
                    });
            var screens = new CodeGenerator().GenerateSharedLibraryProject(project, library);
            var tagsGen = new RuntimeTagsCodeGen();

            // ⚠ screens.c 是**生成物、不入库**（③）⇒ 裸跑 dotnet test 时它可能不在场。
            //   用户拍板（2026-10-11）：这种情形本条判据**直接红**，不做"未查"软跳过 ——
            //   缺席意味着"这条判据根本没在保护任何东西"，红着要你把生成跑起来。
            var screensOnDisk = Repo("firmware/t113/generated/screens.c");
            Assert.True(File.Exists(screensOnDisk),
                "设备树缺少生成物 firmware/t113/generated/screens.c —— 它是生成物、不入库（③：开发机生成、构建机只消费）。"
                + Environment.NewLine + "  先跑：bash scripts/gen-device-tree.sh --write"
                + Environment.NewLine + "  （本条判据按拍板：缺席**直接红**，不软跳过）");

            // ⚠ 面自检：这几个数若变了，说明"源工程 ↔ 夹具"的关系已经不同了，读数要重新测
            var fresh = tagsGen.GenerateHeader(engine.Tags);
            var fixture = File.ReadAllText(Repo("firmware/t113/generated/tags.h"));

            SimulatorProjectExporter.Export(project, screens, work, null, engine.Tags, null);
            var securityPath = Path.Combine(work, "generated_security.h");
            Assert.True(File.Exists(securityPath), "模拟器导出没有产出 generated_security.h");
            var security = File.ReadAllBytes(securityPath);
            var securityFixture = File.ReadAllBytes(Repo("firmware/t113/generated/generated_security.h"));

            JudgeReadings.Write(_o, "设备示例产物的可再导出性（阶段 309/321）", new[]
            {
                $"generated_security.h：现场 {security.Length} B vs 库中 {securityFixture.Length} B",
                $"screens.c：现场 {System.Text.Encoding.UTF8.GetByteCount(screens)} B vs 库中 {new FileInfo(screensOnDisk).Length} B（生成物；参与逐字节比对）",
                $"tags.h：现场 {fresh.Length} B vs 库中 {fixture.Length} B",
                "资产 4 张由 SampleProjectProvenanceGuardTests 按名按字节数守着（逐字节相同）",
                "生成物缺席时本条**直接红**（拍板）：不软跳过 —— 门禁第 7 步会先跑 gen-device-tree.sh --check",
            });

            // === 逐字节相等（④=1 做完后"可再导出"才可能做到这一步）===
            var tracked = new (string Name, byte[] Fresh, byte[] CheckedIn)[]
            {
                ("screens.c", System.Text.Encoding.UTF8.GetBytes(screens), File.ReadAllBytes(screensOnDisk)),
                ("tags.h", System.Text.Encoding.UTF8.GetBytes(fresh),
                    File.ReadAllBytes(Repo("firmware/t113/generated/tags.h"))),
                ("tags.c", System.Text.Encoding.UTF8.GetBytes(tagsGen.Generate(engine.Tags)),
                    File.ReadAllBytes(Repo("firmware/t113/generated/tags.c"))),
                ("generated_security.h", security, securityFixture),
            };
            var pairs = tracked.ToList();
            var bad = pairs.Where(p => !p.Fresh.SequenceEqual(p.CheckedIn))
                           .Select(p => $"{p.Name}（现场 {p.Fresh.Length} B / 库中 {p.CheckedIn.Length} B）")
                           .ToList();
            Assert.True(bad.Count == 0,
                "设备树与「用当前生成器重新导出」的结果不再逐字节相等："
                + Environment.NewLine + string.Join(Environment.NewLine, bad));

            // 面自检：夹具那份是真货（不为空且含权限/用户初始化），否则"相同"可能是两个空文件
            Assert.True(securityFixture.Length > 1000,
                $"夹具 generated_security.h 只有 {securityFixture.Length} B —— 面可疑，先确认夹具没被清空");
        }
        finally
        {
            try { TestTempDir.DeleteDir(work); } catch { /* 清不掉不算失败 */ }
        }
    }
}
