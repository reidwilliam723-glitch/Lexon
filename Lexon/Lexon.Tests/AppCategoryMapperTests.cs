using Lexon.Core.Models;
using Xunit;

namespace Lexon.Tests;

public class AppCategoryMapperTests
{
    [Fact]
    public void Slack_IsCasual()
    {
        Assert.Equal(AppWritingCategory.Casual, AppCategoryMapper.Resolve("Slack.exe"));
    }

    [Fact]
    public void Outlook_IsFormal()
    {
        Assert.Equal(AppWritingCategory.Formal, AppCategoryMapper.Resolve("OUTLOOK.EXE"));
    }

    [Fact]
    public void Unknown_IsNeutral()
    {
        Assert.Equal(AppWritingCategory.Neutral, AppCategoryMapper.Resolve("notepad.exe"));
    }

    [Fact]
    public void Terminals_AreCode()
    {
        Assert.Equal(AppWritingCategory.Code, AppCategoryMapper.Resolve("pwsh.exe"));
        Assert.Equal(AppWritingCategory.Code, AppCategoryMapper.Resolve("WindowsTerminal.exe"));
    }

    [Fact]
    public void Override_Wins()
    {
        var overrides = new Dictionary<string, AppWritingCategory>(StringComparer.OrdinalIgnoreCase)
        {
            ["notepad.exe"] = AppWritingCategory.Formal
        };
        Assert.Equal(AppWritingCategory.Formal, AppCategoryMapper.Resolve("notepad.exe", overrides));
    }

    [Fact]
    public void EnsureExeExtension_AddsExeWhenMissing()
    {
        Assert.Equal("slack.exe", AppCategoryMapper.EnsureExeExtension("Slack"));
        Assert.Equal("outlook.exe", AppCategoryMapper.EnsureExeExtension("outlook.exe"));
        Assert.Equal("chrome.exe", AppCategoryMapper.EnsureExeExtension("Chrome.EXE"));
        Assert.Equal("notepad.exe", AppCategoryMapper.EnsureExeExtension("notepad.exe.exe"));
    }
}
