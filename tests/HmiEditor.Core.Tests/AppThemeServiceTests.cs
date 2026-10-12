using System;
using System.IO;
using HmiEditor.App.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// AppThemeService 纯逻辑测试（不依赖 Avalonia Application 实例）。
/// ApplyPalette 在 Application.Current 为 null 时静默跳过，因此
/// SetTheme/Restore 的持久化与枚举解析逻辑可在无头环境下直接验证。
/// </summary>
public class AppThemeServiceTests
{
    [Fact]
    public void AvailableThemes_ContainsThreeThemes()
    {
        Assert.Equal(3, AppThemeService.AvailableThemes.Count);
    }

    [Fact]
    public void AvailableThemes_HasExpectedKeys()
    {
        var keys = AppThemeService.AvailableThemes.Select(t => t.Key).ToArray();
        Assert.Contains("Dark", keys);
        Assert.Contains("Light", keys);
        Assert.Contains("Ocean", keys);
    }

    [Theory]
    [InlineData("Dark", "Dark Industrial")]
    [InlineData("Light", "Light High Contrast")]
    [InlineData("Ocean", "Ocean Blue")]
    public void AvailableThemes_NamesMatch(string key, string expectedName)
    {
        var theme = AppThemeService.AvailableThemes.Single(t => t.Key == key);
        Assert.Equal(expectedName, theme.Name);
        Assert.False(string.IsNullOrWhiteSpace(theme.DescriptionKey));
        // 键必须真的落在字典里：找不到会回退成键名 ⇒ 悬停提示会显示 "theme.desc.dark"
        foreach (var lang in new[] { "zh-CN", "en-US" })
            Assert.NotEqual(theme.DescriptionKey,
                HmiEditor.App.Resources.AppLocalization.Service.GetString(theme.DescriptionKey, lang));
    }

    /// <summary>
    /// 主题说明必须**解析成当前语言的文案**，而不是把键原样当文案用 ——
    /// 后者没有任何编译期/运行期报错，用户只会在悬停时看到 <c>theme.desc.dark</c>。
    /// </summary>
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    [InlineData("Ocean")]
    public void DescribeTheme_ResolvesTheKey(string key)
    {
        var info = AppThemeService.AvailableThemes.Single(t => t.Key == key);
        var text = AppThemeService.DescribeTheme(key);

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.NotEqual(info.DescriptionKey, text);          // 键没落进字典 / 没解析都会命中这条
        Assert.Equal(HmiEditor.App.Resources.AppLocalization.Service[info.DescriptionKey], text);
    }

    [Theory]
    [InlineData(AppThemeService.HmiTheme.Dark, "Dark Industrial")]
    [InlineData(AppThemeService.HmiTheme.Light, "Light High Contrast")]
    [InlineData(AppThemeService.HmiTheme.Ocean, "Ocean Blue")]
    public void CurrentName_MapsThemeToDisplayName(AppThemeService.HmiTheme theme, string expected)
    {
        var path = TestTempDir.NewFilePath("theme", ".json");
        var service = new AppThemeService(path);
        try
        {
            service.SetTheme(theme);
            Assert.Equal(expected, service.CurrentName);
        }
        finally
        {
            TestTempDir.DeleteFile(path);
        }
    }

    [Fact]
    public void DefaultTheme_IsDark()
    {
        var path = TestTempDir.NewFilePath("theme", ".json");
        var service = new AppThemeService(path);
        Assert.Equal(AppThemeService.HmiTheme.Dark, service.Current);
    }

    [Theory]
    [InlineData("Ocean", AppThemeService.HmiTheme.Ocean)]
    [InlineData("ocean", AppThemeService.HmiTheme.Ocean)]
    [InlineData("Light", AppThemeService.HmiTheme.Light)]
    [InlineData("Light High Contrast", AppThemeService.HmiTheme.Light)]
    [InlineData("dark industrial", AppThemeService.HmiTheme.Dark)]
    public void SetThemeByName_MatchesKeyOrNameCaseInsensitive(string name, AppThemeService.HmiTheme expected)
    {
        var path = TestTempDir.NewFilePath("theme", ".json");
        var service = new AppThemeService(path);
        try
        {
            service.SetThemeByName(name);
            Assert.Equal(expected, service.Current);
        }
        finally
        {
            TestTempDir.DeleteFile(path);
        }
    }

    [Fact]
    public void SetThemeByName_UnknownName_KeepsCurrent()
    {
        var path = TestTempDir.NewFilePath("theme", ".json");
        var service = new AppThemeService(path);
        try
        {
            service.SetTheme(AppThemeService.HmiTheme.Light);
            service.SetThemeByName("不存在的主题");
            Assert.Equal(AppThemeService.HmiTheme.Light, service.Current);
        }
        finally
        {
            TestTempDir.DeleteFile(path);
        }
    }

    [Fact]
    public void SetTheme_PersistsToFile()
    {
        var path = TestTempDir.NewFilePath("theme", ".json");
        try
        {
            var service = new AppThemeService(path);
            service.SetTheme(AppThemeService.HmiTheme.Ocean);
            Assert.True(File.Exists(path));
            Assert.Contains("Ocean", File.ReadAllText(path));
        }
        finally
        {
            TestTempDir.DeleteFile(path);
        }
    }

    [Fact]
    public void Restore_ReadsPersistedTheme()
    {
        var path = TestTempDir.NewFilePath("theme", ".json");
        try
        {
            new AppThemeService(path).SetTheme(AppThemeService.HmiTheme.Light);

            var restored = new AppThemeService(path);
            restored.Restore();
            Assert.Equal(AppThemeService.HmiTheme.Light, restored.Current);
        }
        finally
        {
            TestTempDir.DeleteFile(path);
        }
    }

    [Fact]
    public void Restore_MissingFile_StaysDark()
    {
        var path = TestTempDir.NewFilePath("theme", ".json");
        var service = new AppThemeService(path);
        service.Restore();
        Assert.Equal(AppThemeService.HmiTheme.Dark, service.Current);
    }

    [Fact]
    public void Restore_InvalidJson_FallsBackToDark()
    {
        var path = TestTempDir.NewFilePath("theme", ".json");
        try
        {
            File.WriteAllText(path, "{ not valid json !!!");
            var service = new AppThemeService(path);
            service.Restore();
            Assert.Equal(AppThemeService.HmiTheme.Dark, service.Current);
        }
        finally
        {
            TestTempDir.DeleteFile(path);
        }
    }

    [Fact]
    public void Restore_UnknownThemeName_FallsBackToDark()
    {
        var path = TestTempDir.NewFilePath("theme", ".json");
        try
        {
            File.WriteAllText(path, """{"Theme":"Rainbow"}""");
            var service = new AppThemeService(path);
            service.Restore();
            Assert.Equal(AppThemeService.HmiTheme.Dark, service.Current);
        }
        finally
        {
            TestTempDir.DeleteFile(path);
        }
    }
}
