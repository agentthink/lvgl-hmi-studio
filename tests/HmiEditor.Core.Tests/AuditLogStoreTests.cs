using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// I3: 审计日志持久化单元测试。
/// 覆盖 JSONL append/load 往返、损坏行容错、认证服务落盘与去重加载。
/// </summary>
public class AuditLogStoreTests
{
    private static string NewTempFile() =>
        TestTempDir.NewFilePath("hmi-audit", ".jsonl");

    [Fact]
    public void AppendAndLoad_Roundtrip_ReturnsAllEntriesDescending()
    {
        var path = NewTempFile();
        try
        {
            var first = new AuditLogEntry
            {
                Username = "admin",
                Action = AuditAction.Login,
                Detail = "用户登录",
                Success = true
            };
            var second = new AuditLogEntry
            {
                Username = "admin",
                Action = AuditAction.DataExported,
                Detail = "导出 C 代码: /tmp/out.c",
                Success = true
            };

            Assert.True(AuditLogStore.Append(path, first));
            Assert.True(AuditLogStore.Append(path, second));

            var loaded = AuditLogStore.LoadAll(path);

            Assert.Equal(2, loaded.Count);
            Assert.Equal(AuditAction.DataExported, loaded[0].Action);
            Assert.Equal(AuditAction.Login, loaded[1].Action);
            Assert.Equal("admin", loaded[0].Username);
        }
        finally
        {
            TestTempDir.DeleteFile(path);
        }
    }

    [Fact]
    public void LoadAll_MissingFile_ReturnsEmpty()
    {
        var loaded = AuditLogStore.LoadAll(NewTempFile());
        Assert.Empty(loaded);
    }

    [Fact]
    public void LoadAll_CorruptLines_AreSkipped()
    {
        var path = NewTempFile();
        try
        {
            var valid = new AuditLogEntry
            {
                Username = "admin",
                Action = AuditAction.Logout,
                Detail = "用户登出",
                Success = true
            };
            Assert.True(AuditLogStore.Append(path, valid));
            File.AppendAllText(path, "this is not valid json{truncated" + Environment.NewLine);

            var loaded = AuditLogStore.LoadAll(path);

            Assert.Single(loaded);
            Assert.Equal(AuditAction.Logout, loaded[0].Action);
        }
        finally
        {
            TestTempDir.DeleteFile(path);
        }
    }

    [Fact]
    public void Authentication_RecordAudit_PersistsToFileAndRaisesEvent()
    {
        var path = NewTempFile();
        AuthenticationService? auth = null;
        try
        {
            auth = new AuthenticationService { AuditLogFilePath = path };
            AuditLogEntry? raised = null;
            auth.AuditLogged += (_, entry) => raised = entry;

            auth.RecordAudit(AuditAction.DataExported, "导出 C 代码: out.c", true);

            Assert.NotNull(raised);
            Assert.Equal(AuditAction.DataExported, raised!.Action);

            var fromFile = AuditLogStore.LoadAll(path);
            Assert.Single(fromFile);
            Assert.Equal("导出 C 代码: out.c", fromFile[0].Detail);
            Assert.True(fromFile[0].Success);
        }
        finally
        {
            auth?.Dispose();
            TestTempDir.DeleteFile(path);
        }
    }

    [Fact]
    public void Authentication_LoadAuditLogFromFile_LoadsAndDedupes()
    {
        var path = NewTempFile();
        try
        {
            var producer = new AuthenticationService { AuditLogFilePath = path };
            producer.RecordAudit(AuditAction.Login, "用户登录", true);
            producer.RecordAudit(AuditAction.ConfigChanged, "修改主题", true);
            producer.Dispose();

            var consumer = new AuthenticationService { AuditLogFilePath = path };
            var added = consumer.LoadAuditLogFromFile(path);

            Assert.Equal(2, added);
            Assert.Equal(2, consumer.GetAuditLog().Count);

            // 再次加载应全部去重
            var addedAgain = consumer.LoadAuditLogFromFile(path);
            Assert.Equal(0, addedAgain);
            Assert.Equal(2, consumer.GetAuditLog().Count);
        }
        finally
        {
            TestTempDir.DeleteFile(path);
        }
    }

    [Fact]
    public void Authentication_Login_AppendsAuditToFile()
    {
        var path = NewTempFile();
        AuthenticationService? auth = null;
        try
        {
            auth = new AuthenticationService { AuditLogFilePath = path };

            var session = auth.Login("admin", "admin", "test-source").SessionId;

            Assert.NotNull(session);

            var fromFile = AuditLogStore.LoadAll(path);
            Assert.Contains(fromFile, e =>
                e.Action == AuditAction.Login && e.Username == "admin" && e.Success);
        }
        finally
        {
            auth?.Dispose();
            TestTempDir.DeleteFile(path);
        }
    }
}
