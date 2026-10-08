using Lexon.Input;
using Xunit;

namespace Lexon.Tests;

public class WebEditorSupportTests
{
    [Fact]
    public void DetectsChromeAndDocs()
    {
        Assert.True(WebEditorSupport.IsBrowserProcess("chrome.exe"));
        Assert.True(WebEditorSupport.IsGoogleDocs("Essay - Google Docs"));
        Assert.True(WebEditorSupport.IsWebDocumentEditor("chrome", "Essay - Google Docs", "Chrome_WidgetWin_1"));
        Assert.False(WebEditorSupport.IsWebDocumentEditor("Cursor", "Lexon.cs", "Chrome_WidgetWin_1"));
        Assert.True(WebEditorSupport.IsCodeEditorShell("Cursor.exe"));
        Assert.False(WebEditorSupport.IsBrowserProcess("notepad"));
        Assert.True(WebEditorSupport.IsWhatsApp("WhatsApp.exe"));
        Assert.True(WebEditorSupport.IsWhatsApp("WhatsApp.Root"));
        Assert.False(WebEditorSupport.IsWhatsApp("chrome.exe"));
    }

    [Fact]
    public void IgnoresWindowTitleAsDocumentText()
    {
        Assert.True(WebEditorSupport.ShouldIgnoreLiveText(
            "Untitled document - Google Docs",
            "Untitled document - Google Docs"));
        Assert.False(WebEditorSupport.ShouldIgnoreLiveText("he are ready", "Untitled document - Google Docs"));
    }

    [Fact]
    public void CountWords_SplitsPhrase()
    {
        Assert.Equal(2, WebEditorSupport.CountWords("he are"));
        Assert.Equal(1, WebEditorSupport.CountWords("teh"));
    }
}
