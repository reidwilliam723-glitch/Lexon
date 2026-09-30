using Lexon.Core;
using Lexon.Core.Models;
using Lexon.Privacy;
using Xunit;

namespace Lexon.Tests;

public class ApplicationNameTests
{
    [Theory]
    [InlineData("putty.exe", "putty")]
    [InlineData("PUTTY.EXE", "PUTTY")]
    [InlineData("putty", "putty")]
    [InlineData(@"C:\Program Files\PuTTY\putty.exe", "putty")]
    [InlineData(" chrome.EXE ", "chrome")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_StripsPathAndExe(string? input, string expected)
    {
        Assert.Equal(expected, ApplicationName.Normalize(input));
    }
}

public class PrivacyGuardNormalizationTests
{
    [Fact]
    public void BlockedAppsMatchExeAndBareNames()
    {
        var guard = new PrivacyGuard();
        Assert.True(guard.IsApplicationBlocked("putty.exe"));
        Assert.True(guard.IsApplicationBlocked("PuTTY"));
        Assert.True(guard.ShouldBlockAssistance(new TextContext { ApplicationName = "putty.exe" }));

        guard.AddBlockedApplication("notepad.exe");
        Assert.True(guard.IsApplicationBlocked("notepad"));
        Assert.True(guard.IsApplicationBlocked(@"C:\Windows\notepad.exe"));
    }
}

public class TextReplacementTests
{
    [Fact]
    public void ActiveSelection_UsesOneBackspace()
    {
        Assert.Equal(1, TextReplacement.BackspacesForReplace("hello 👋 world", selectionStillActive: true));
    }

    [Fact]
    public void CaretReplace_CountsGraphemesNotUtf16Length()
    {
        const string emoji = "👍";
        Assert.Equal(2, emoji.Length);
        Assert.Equal(1, TextUnits.GraphemeCount(emoji));
        Assert.Equal(1, TextReplacement.BackspacesForReplace(emoji, selectionStillActive: false));
        Assert.Equal(5, TextReplacement.BackspacesForReplace("hello", selectionStillActive: false));
    }

    [Fact]
    public void EmptyOldText_DeletesNothing()
    {
        Assert.Equal(0, TextReplacement.BackspacesForReplace("", selectionStillActive: true));
        Assert.Equal(0, TextReplacement.BackspacesForReplace(null, selectionStillActive: false));
    }
}

public class SecureFieldPolicyTests
{
    [Fact]
    public void NativePassword_IsSecure()
    {
        Assert.True(SecureFieldPolicy.IsSecure(nativePasswordStyle: true, uiaIsPassword: false, controlHandleValid: true));
    }

    [Fact]
    public void UiaPassword_IsSecure()
    {
        Assert.True(SecureFieldPolicy.IsSecure(nativePasswordStyle: false, uiaIsPassword: true, controlHandleValid: true));
    }

    [Fact]
    public void UiaFalse_IsNotSecure()
    {
        Assert.False(SecureFieldPolicy.IsSecure(nativePasswordStyle: false, uiaIsPassword: false, controlHandleValid: true));
    }

    [Fact]
    public void UnknownOrMissingHandle_FailsClosed()
    {
        Assert.True(SecureFieldPolicy.IsSecure(nativePasswordStyle: false, uiaIsPassword: null, controlHandleValid: true));
        Assert.True(SecureFieldPolicy.IsSecure(nativePasswordStyle: false, uiaIsPassword: null, controlHandleValid: false));
    }
}
