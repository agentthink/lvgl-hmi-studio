using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 图库持久化的**数据安全判据**（阶段 120）。
///
/// <para>背景：`Load()` 曾是裸 `catch { return null; }`，调用方把 null 当"没存过"，
/// 而退出时又无条件 `Save()` ⇒ 一次 JSON 损坏或瞬时读失败，磁盘上的
/// <c>hmi-reslib.json</c> 就被"只剩内置资源"的空库覆盖，用户自定义资源静默消失。</para>
///
/// <para>现在的契约：`TryLoad` 返回 false 表示**文件存在但读不出来**；此后
/// `TrySave` 必须拒绝写盘并返回 false。这条判据的核心一行是
/// 「拒绝之后文件内容逐字节不变」—— 只断言返回值会被"先写再返回 false"的实现骗过。</para>
/// </summary>
public class ResourceLibraryStoreTests
{
    [Fact]
    public void TryLoad_MissingFile_IsNotAnError()
    {
        var dir = TestTempDir.NewDir("reslib-missing");
        try
        {
            var store = new ResourceLibraryStore(dir);

            Assert.True(store.TryLoad(out var library));   // 没存过 = 正常空库
            Assert.Null(library);
            Assert.Null(store.LastLoadError);
        }
        finally { TestTempDir.DeleteDir(dir); }
    }

    [Fact]
    public void TrySave_ThenTryLoad_RoundTrips()
    {
        var dir = TestTempDir.NewDir("reslib-roundtrip");
        try
        {
            var store = new ResourceLibraryStore(dir);

            Assert.True(store.TrySave(new HmiResourceLibrary()));
            Assert.True(store.TryLoad(out var library));
            Assert.NotNull(library);
        }
        finally { TestTempDir.DeleteDir(dir); }
    }

    [Fact]
    public void TryLoad_CorruptFile_ReportsFailure()
    {
        var dir = TestTempDir.NewDir("reslib-corrupt");
        try
        {
            var store = new ResourceLibraryStore(dir);
            File.WriteAllText(Path.Combine(dir, "hmi-reslib.json"), "{ this is not json");

            Assert.False(store.TryLoad(out var library));
            Assert.Null(library);
            Assert.False(string.IsNullOrEmpty(store.LastLoadError));
        }
        finally { TestTempDir.DeleteDir(dir); }
    }

    [Fact]
    public void TrySave_AfterFailedLoad_RefusesToOverwrite()
    {
        var dir = TestTempDir.NewDir("reslib-refuse-overwrite");
        try
        {
            var path = Path.Combine(dir, "hmi-reslib.json");
            const string original = "{ corrupt payload that must survive }";
            File.WriteAllText(path, original);

            var store = new ResourceLibraryStore(dir);
            Assert.False(store.TryLoad(out _));

            // 用一份"只有内置资源"的库去存 —— 必须被拒绝，且文件一个字节都不许变
            Assert.False(store.TrySave(new HmiResourceLibrary()));
            Assert.Equal(original, File.ReadAllText(path));

            // 也不许留下历史快照（否则回滚面会被污染）
            Assert.Empty(store.GetHistory());
        }
        finally { TestTempDir.DeleteDir(dir); }
    }

    [Fact]
    public void TrySave_Twice_KeepsBothSnapshots()
    {
        var dir = TestTempDir.NewDir("reslib-snapshots");
        try
        {
            var store = new ResourceLibraryStore(dir);

            // 旧实现的时间戳只到秒 ⇒ 同一秒内两次保存写同一个快照文件，版本历史丢一条
            Assert.True(store.TrySave(new HmiResourceLibrary()));
            Assert.True(store.TrySave(new HmiResourceLibrary()));

            Assert.Equal(2, store.GetHistory().Count);
        }
        finally { TestTempDir.DeleteDir(dir); }
    }
}
