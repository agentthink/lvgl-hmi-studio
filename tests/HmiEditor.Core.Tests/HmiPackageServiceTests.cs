using System.IO.Compression;
using System.Text;
using System.Text.Json;
using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// .hmi 工程包（ZIP）打包/解包往返测试。
/// </summary>
public class HmiPackageServiceTests
{
    [Fact]
    public void PackUnpack_RoundTrips_ProjectAndAssets()
    {
        // 构造工程
        var project = new HmiProject { Name = "TestProject", ProjectVersion = "1.2.0" };
        var screen = new HmiScreen { Name = "Screen1", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl { Name = "Btn1", Type = HmiControlType.Button, Text = "OK" });
        project.Screens.Add(screen);

        // 构造资产文件
        var tempRoot = TestTempDir.NewDir("hmi-pkg");
        try
        {
            var assetFile = Path.Combine(tempRoot, "icon.png");
            File.WriteAllBytes(assetFile, new byte[] { 1, 2, 3, 4, 5 });

            var assetLib = new AssetLibrary();
            assetLib.AddAsset(new AssetItem
            {
                Id = "a1",
                Name = "icon",
                Type = AssetType.Image,
                FilePath = assetFile,
                SizeBytes = 5,
                Width = 16,
                Height = 16
            });

            // 打包到内存
            using var ms = new MemoryStream();
            HmiPackageService.Pack(ms, new HmiPackageContent(
                project, assetLib, null,
                item => File.Exists(item.FilePath) ? item.FilePath : null));

            // 解包
            ms.Position = 0;
            var unpackDir = Path.Combine(tempRoot, "unpack");
            var result = HmiPackageService.Unpack(ms, unpackDir);

            // 工程数据完整
            Assert.Equal("TestProject", result.Project.Name);
            Assert.Equal("1.2.0", result.Project.ProjectVersion);
            Assert.Single(result.Project.Screens);
            Assert.Equal("Btn1", result.Project.Screens[0].Controls[0].Name);

            // 资产落盘 + FilePath 更新为绝对路径
            var unpacked = Assert.Single(result.AssetLibrary.Assets);
            Assert.True(File.Exists(unpacked.FilePath), "资产文件应落盘");
            Assert.Equal(5, new FileInfo(unpacked.FilePath).Length);
            Assert.StartsWith(unpackDir, unpacked.FilePath);
            Assert.Single(result.AssetFilePaths);
        }
        finally
        {
            TestTempDir.DeleteDir(tempRoot);
        }
    }

    [Fact]
    public void Pack_MissingAssetFile_IsReportedInsteadOfSilentlySkipped()
    {
        // 阶段 121：资产条目在库里、但磁盘上的文件不在（fp 断链）。
        // 旧行为：打包静默 continue、解包静默 continue ⇒ 重新打开的工程带着断引用，零提示。
        var project = new HmiProject { Name = "MissingAsset" };
        var assetLib = new AssetLibrary();
        assetLib.AddAsset(new AssetItem
        {
            Id = "a9",
            Name = "logo-丢失",
            Type = AssetType.Image,
            FilePath = "/definitely/not/there/logo.png",
        });

        var root = TestTempDir.NewDir("hmi-pkg-missing");
        try
        {
            using var ms = new MemoryStream();
            HmiPackageService.Pack(ms, new HmiPackageContent(project, assetLib, null, item => item.FilePath));

            // 打包侧必须**自己**把缺失资产记进 manifest（不依赖解包侧的兜底）；
            // 否则"包内 assets.json 列着、assets/ 里没有"这个不一致就没有任何账。
            ms.Position = 0;
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true))
            {
                var manifestEntry = zip.GetEntry("manifest.json");
                Assert.NotNull(manifestEntry);
                using var reader = new StreamReader(manifestEntry!.Open());
                var manifest = JsonSerializer.Deserialize<HmiPackageManifest>(
                    reader.ReadToEnd(), HmiEditor.Core.Serialization.JsonOptionsDefaults.Default);
                Assert.NotNull(manifest);
                Assert.Equal(new[] { "logo-丢失" }, manifest!.MissingAssets);
            }

            ms.Position = 0;
            var result = HmiPackageService.Unpack(ms, Path.Combine(root, "unpack"));

            // 元数据不丢（条目仍在 assets.json 里），但缺文件必须被**点名**
            Assert.Single(result.AssetLibrary.Assets);
            var missing = Assert.Single(result.MissingAssets!);
            Assert.Equal("logo-丢失", missing);
            Assert.Empty(result.AssetFilePaths);
        }
        finally
        {
            TestTempDir.DeleteDir(root);
        }
    }

    [Fact]
    public void Pack_WithResourceLibrary_RoundTrips()
    {
        var project = new HmiProject { Name = "P" };
        var reslib = new HmiResourceLibrary();
        reslib.CustomResources.Add(new HmiResourceItem
        {
            Name = "res1",
            DisplayName = "资源1",
            SvgGeometry = "<svg/>"
        });

        using var ms = new MemoryStream();
        HmiPackageService.Pack(ms, new HmiPackageContent(project, new AssetLibrary(), reslib, _ => null));

        ms.Position = 0;
        var result = HmiPackageService.Unpack(ms,
            TestTempDir.NewDir("hmi-unpack2"));

        Assert.NotNull(result.ResourceLibrary);
        Assert.Contains(result.ResourceLibrary!.CustomResources, r => r.Name == "res1" && r.SvgGeometry == "<svg/>");
    }

    [Fact]
    public void Pack_MissingAssetFile_IsSkipped()
    {
        var project = new HmiProject { Name = "P" };
        var assetLib = new AssetLibrary();
        assetLib.AddAsset(new AssetItem
        {
            Id = "ghost",
            Name = "ghost",
            Type = AssetType.Image,
            FilePath = "/nonexistent/path/ghost.png"
        });

        using var ms = new MemoryStream();
        HmiPackageService.Pack(ms, new HmiPackageContent(project, assetLib, null,
            item => File.Exists(item.FilePath) ? item.FilePath : null));

        ms.Position = 0;
        var result = HmiPackageService.Unpack(ms,
            TestTempDir.NewDir("hmi-unpack3"));

        Assert.Single(result.AssetLibrary.Assets);
        Assert.Empty(result.AssetFilePaths); // 缺失文件不入包
    }

    /// <summary>
    /// 非 ZIP ⇒ **码化**失败（阶段 81 / 登记 #59）。
    ///
    /// <para>改动前这里断言的是 <c>InvalidDataException</c>，而它的 <c>Message</c> 是 BCL 的
    /// <c>End of Central Directory record could not be found.</c> ⇒ 一路透传到用户可见的状态栏。
    /// 现在断言两件**可判**的东西：类型是 <see cref="HmiPackageException"/>，
    /// 且 <c>Code</c> == <c>ZIP_UNREADABLE</c>（App 层据此查 <c>package.issue.zip_unreadable</c>）。</para>
    ///
    /// <para>⚠ <c>Assert.Throws&lt;T&gt;</c> 是**精确**类型匹配，不能写成 <c>InvalidDataException</c>
    /// —— <c>HmiPackageException</c> 的基类是 <c>Exception</c>（那个类型在 .NET 里是
    /// <b>sealed</b>，派生不了，实测 <c>CS0509</c>）。</para>
    /// </summary>
    [Fact]
    public void Unpack_NonZip_Throws()
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("not a zip file"));
        var ex = Assert.Throws<HmiPackageException>(() =>
            HmiPackageService.Unpack(ms, TestTempDir.NewDir("hmi-unpack-invalid")));

        Assert.Equal(HmiPackageIssueCodes.ZIP_UNREADABLE, ex.Code);
        Assert.Null(ex.Args);
    }


    /// <summary>
    /// **坏包解包不建目标目录** —— <c>Unpack</c> 的落点契约，也就是"打开失败不留工作区目录"
    /// 这条不变量的**被调方**一半（调用方一半在 <c>ProjectFileOpenFailureUiTests</c>）。
    ///
    /// <para><b>它守的缺陷形状：</b>调用方（<c>MainWindow.OpenHmiFile</c>）原来在解包**之前**就把
    /// 落点建出来，包一坏就留下一个空目录；而会话目录的兜底释放在**进程退出** ⇒ 一次会话里反复
    /// 打开坏包会一路攒（阶段 80 实测：5 次失败 = 5 个空目录）。修法不是"建了再删"，而是
    /// **不提前建**：落点先只算路径，由 <c>Unpack</c> 在**全部前置校验通过之后**才
    /// <c>Directory.CreateDirectory(assetDir)</c>。本判据钉的正是这条 —— 目标目录由**被调方**
    /// 在第一次真正写盘时自建，坏包**按构造**不留痕迹。</para>
    ///
    /// <para><b>为什么它得单独存在：</b>App 层那条判据要构造窗口、依赖会话临时根（进程级环境变量），
    /// 并行大队里得靠"唯一工程名 + 全数据根扫描"才自治 —— 而本判据传进去的目标目录是**判据自己造的、
    /// 独占的**，不读任何共享状态 ⇒ 它不受并发影响。两种失守因此能分开定位：
    /// 被调方把建目录提前 ⇒ 本判据红；调用点又变回 <c>create: true</c> ⇒ App 层判据红。</para>
    ///
    /// <para><b>正控是必须的</b>：若 <c>Unpack</c> 哪天干脆不建任何目录，"坏包不建目录"就能由
    /// "永远不建"满足 ⇒ 判据变装饰。所以最后一步断言好包解包**必须**把目标目录建出来。</para>
    ///
    /// <para>旧格式 <c>.hmi.json</c> 不走 <c>Unpack</c>（走 <c>ProjectSerializer</c>），
    /// 它的失败路径由 App 层那条判据覆盖 —— 这里只覆盖 ZIP 分支。</para>
    /// </summary>
    [Fact]
    public void Unpack_BrokenPackage_LeavesNoTargetDirectoryBehind()
    {
        var root = TestTempDir.NewDir("hmi-unpack-broken");
        try
        {
            // 5 条坏包，正好是 Unpack 里 CreateDirectory 之前的那几道前置校验：
            // 非 ZIP（ZipArchive 构造）/ 缺 manifest / 版本不符 / 缺 project.json / manifest 坏 JSON。
            var cases = new (string Label, byte[] Bytes)[]
            {
                ("非 ZIP（随机字节）", NonZipBytes(256)),
                ("缺 manifest.json", ZipBytes(("project.json", ProjectJson("无清单")))),
                ("manifest 版本不符（9999）", ZipBytes(
                    ("manifest.json", ManifestJson(9999)), ("project.json", ProjectJson("版本不符")))),
                ("缺 project.json", ZipBytes(("manifest.json", ManifestJson(1)))),
                ("manifest.json 是坏 JSON", ZipBytes(
                    ("manifest.json", "{ 这不是 JSON"), ("project.json", ProjectJson("坏清单")))),
            };

            var errors = new List<string>();
            foreach (var (label, bytes) in cases)
            {
                // 目标目录**必须**处于"还没被建出来"的状态 —— 判据要断言的正是这一点。
                var target = Path.Combine(root, "target-" + Guid.NewGuid().ToString("N")[..8]);
                Assert.False(Directory.Exists(target), $"前置条件不成立：「{target}」居然已经存在");

                using var input = new MemoryStream(bytes);
                var threw = false;
                try { HmiPackageService.Unpack(input, target); }
                catch (Exception) { threw = true; }

                if (!threw)
                    errors.Add($"{label}：居然没抛异常（坏包被当成了好包）");
                if (Directory.Exists(target))
                    errors.Add($"{label}：目标目录被建出来了（{target}）—— "
                               + "失败的解包不该留下任何还要人收拾的东西");
            }

            // 正控：好包**必须**建出目标目录（否则上面那条可以靠"永远不建"满足）。
            var okTarget = Path.Combine(root, "target-ok");
            using (var good = new MemoryStream(ZipBytes(
                ("manifest.json", ManifestJson(HmiPackageService.CurrentFormatVersion)),
                ("project.json", ProjectJson("好包")))))
            {
                HmiPackageService.Unpack(good, okTarget);
            }

            Assert.True(errors.Count == 0,
                "坏包解包在磁盘上留下了痕迹：\n  - " + string.Join("\n  - ", errors));
            Assert.True(Directory.Exists(okTarget),
                $"正控失败：好包解包之后目标目录「{okTarget}」仍不存在 ⇒ "
                + "上面「坏包不建目录」的断言可以由「Unpack 永远不建目录」满足（判据无效）");
        }
        finally { TestTempDir.DeleteDir(root); }
    }

    /// <summary>
    /// **中途失败**（<c>Directory.CreateDirectory(assetDir)</c> **之后**）同样**码化**、
    /// 同样**不留目标目录** —— 阶段 105 补的另一半（阶段 80 §8.6 / §38.10 第 2 条那条
    /// 写着"无修法无判据"的缺口）。
    ///
    /// <para><b>缺口原来长什么样（阶段 105 探针实测，不是推断）</b>：
    /// ① <c>reslib.json</c> 坏 JSON ⇒ 抛 <c>System.Text.Json.JsonException</c>，消息里带
    /// <c>LineNumber</c> / <c>BytePositionInLine</c> ⇒ 与登记 <b>#59</b> 同一个病
    /// （.NET 英文原文直通状态栏）；
    /// ② <c>assets.json</c> 指向一条以 <c>/</c> 结尾的条目 ⇒ <c>ExtractToFile</c> 的目标是
    /// **已存在的目录** ⇒ <c>UnauthorizedAccessException</c>（同样是 BCL 英文原文）。
    /// 两条都在 <c>CreateDirectory</c> 之后 ⇒ 落点里留下 <c>&lt;目标&gt;/assets/</c>，
    /// 而"打开失败不留工作区目录"那条不变量当时只对**前置校验**那几条成立。</para>
    ///
    /// <para><b>本判据钉三件</b>：① 两条都抛 <see cref="HmiPackageException"/> 且码正确；
    /// ② 失败后目标目录**不存在**（回滚生效）；③ **两条正控** —— 好包必须建出目标目录、
    /// 带资产的包必须把资产真落盘（否则 ② 可以由"Unpack 永远不建目录 / 永远不解资产"满足）。</para>
    /// </summary>
    [Fact]
    public void Unpack_MidFailure_LeavesNoTargetDirectoryBehind()
    {
        var root = TestTempDir.NewDir("hmi-unpack-midfail");
        try
        {
            var cases = new (string Label, string Code, byte[] Bytes)[]
            {
                ("reslib.json 是坏 JSON", HmiPackageIssueCodes.RESLIB_INVALID,
                    ZipBytes(("manifest.json", ManifestJson(1)),
                             ("project.json", ProjectJson("坏图库")),
                             ("reslib.json", "{ 这不是 JSON"))),
                ("资产条目是目录条目（assets/）", HmiPackageIssueCodes.ASSET_EXTRACT_FAILED,
                    ZipBytes(("manifest.json", ManifestJson(1)),
                             ("project.json", ProjectJson("目录条目")),
                             ("assets.json", AssetsJson("a1", "assets/")),
                             ("assets/", ""))),
            };

            var errors = new List<string>();
            foreach (var (label, code, bytes) in cases)
            {
                var target = Path.Combine(root, "target-" + Guid.NewGuid().ToString("N")[..8]);
                Assert.False(Directory.Exists(target), $"前置条件不成立：「{target}」居然已经存在");

                using var input = new MemoryStream(bytes);
                try
                {
                    HmiPackageService.Unpack(input, target);
                    errors.Add($"{label}：居然没抛异常（坏包被当成了好包）");
                }
                catch (HmiPackageException ex)
                {
                    if (ex.Code != code) errors.Add($"{label}：码是 {ex.Code}，期望 {code}");
                }
                catch (Exception ex)
                {
                    // 非码化 ⇒ 状态栏会显示 .NET 英文原文（#59 的形状）。
                    errors.Add($"{label}：抛的不是码化异常（{ex.GetType().Name}）");
                }

                if (Directory.Exists(target))
                    errors.Add($"{label}：中途失败在磁盘上留下了痕迹（{target}）；留下的东西："
                               + string.Join(" | ", Directory.EnumerateFileSystemEntries(
                                   target, "*", SearchOption.AllDirectories)));
            }

            // 正控 ①：好包**必须**建出目标目录（否则上面那条可以靠"永远不建"满足）。
            var okTarget = Path.Combine(root, "target-ok");
            using (var good = new MemoryStream(ZipBytes(
                ("manifest.json", ManifestJson(HmiPackageService.CurrentFormatVersion)),
                ("project.json", ProjectJson("好包")))))
            {
                HmiPackageService.Unpack(good, okTarget);
            }

            // 正控 ②：带资产的包**必须**把资产真落盘（否则"资产那一支失败"可以靠"什么都不解"满足）。
            var assetTarget = Path.Combine(root, "target-asset");
            using (var withAsset = new MemoryStream(ZipBytes(
                ("manifest.json", ManifestJson(HmiPackageService.CurrentFormatVersion)),
                ("project.json", ProjectJson("带资产")),
                ("assets.json", AssetsJson("a2", "assets/a2.png")),
                ("assets/a2.png", "PNG-PLACEHOLDER"))))
            {
                var ok = HmiPackageService.Unpack(withAsset, assetTarget);
                var landed = Path.Combine(assetTarget, "assets", "a2.png");
                if (ok.AssetFilePaths.Count != 1 || !File.Exists(landed))
                    errors.Add($"正控失败：带资产的包没把资产落盘（{landed}）");
            }

            Assert.True(errors.Count == 0,
                "中途失败（建目录之后的失败）没有收口干净：\n  - " + string.Join("\n  - ", errors));
            Assert.True(Directory.Exists(okTarget), $"正控失败：好包解包之后「{okTarget}」仍不存在");
        }
        finally { TestTempDir.DeleteDir(root); }
    }

    // ── 坏包夹具 ──

    /// <summary>定长伪随机字节；前 4 字节刻意不是 <c>PK\x03\x04</c>。</summary>
    private static byte[] NonZipBytes(int size)
    {
        var data = new byte[size];
        for (var i = 0; i < size; i++) data[i] = (byte)((i * 131 + 17) % 251);
        return data;
    }

    /// <summary>
    /// 内存里手搓一个 ZIP。**用 <c>params</c> 而不是收数组**：元组元素的**可空性不协变** ——
    /// 收数组时调用点推 <c>(string, string)[]</c>、形参要 <c>(string, string?)[]</c> ⇒ 报 <c>CS8620</c>
    /// （阶段 80 实测：独立跑全绿、是被门禁的「基线 0 警告」拦下来的）。
    /// 走 <c>params</c> 之后每个实参**逐个**转换，<c>string</c> → <c>string?</c> 是放宽方向。
    /// </summary>
    private static byte[] ZipBytes(params (string Name, string? Text)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in entries)
            {
                if (text is null) continue;
                var entry = zip.CreateEntry(name);
                using var stream = entry.Open();
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(text);
            }
        }
        return ms.ToArray();
    }

    private static string ManifestJson(int formatVersion)
        => JsonSerializer.Serialize(
            new HmiPackageManifest { FormatVersion = formatVersion, ProjectName = "坏包" },
            JsonOptionsDefaults.Default);

    private static string ProjectJson(string name)
        => ProjectSerializer.Serialize(new HmiProject { Name = name, ProjectVersion = "1.0.0" });

    /// <summary>
    /// <c>assets.json</c> 夹具：**一条**资产记录，<paramref name="filePath"/> 就是包内相对路径。
    /// 走真实序列化（不手写 JSON）—— 手写的形状一旦与模型分家，判据测的就是那份手写副本。
    /// </summary>
    private static string AssetsJson(string id, string filePath)
    {
        var lib = new AssetLibrary();
        lib.AddAsset(new AssetItem { Id = id, Name = id, Type = AssetType.Image, FilePath = filePath });
        return JsonSerializer.Serialize(lib, JsonOptionsDefaults.Default);
    }

    [Fact]
    public void Pack_WithProjectJsonSnapshot_ExtractsManifestMeta()
    {
        // 验证自动保存使用的「预序列化 projectJson」打包入口：manifest 应从快照提取工程名/版本
        var project = new HmiProject { Name = "SnapshotProj", ProjectVersion = "3.1.0" };
        using var ms = new MemoryStream();
        HmiPackageService.Pack(ms, ProjectSerializer.Serialize(project), new AssetLibrary(), null, null);

        ms.Position = 0;
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var manifestEntry = zip.GetEntry("manifest.json")!;
        using var reader = new StreamReader(manifestEntry.Open());
        var manifest = JsonSerializer.Deserialize<HmiPackageManifest>(reader.ReadToEnd(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal("SnapshotProj", manifest.ProjectName);
        Assert.Equal("3.1.0", manifest.ProjectVersion);
        Assert.Equal(HmiPackageService.CurrentFormatVersion, manifest.FormatVersion);
    }
}
