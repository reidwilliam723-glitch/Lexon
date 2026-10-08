using Lexon.Core.Learning;
using Xunit;

namespace Lexon.Tests;

public class LearnedWordPolicyTests
{
    [Fact]
    public void OffSwitch_BlocksEveryApp()
    {
        Assert.False(LearnedWordPolicy.Allows(false, ["notepad"], "chrome"));
        Assert.False(LearnedWordPolicy.Allows(false, [], "notepad"));
    }

    [Fact]
    public void MutedApp_IsSkipped_AndOtherAppsStayOn()
    {
        var muted = new[] { "WINWORD.EXE" };
        Assert.False(LearnedWordPolicy.Allows(true, muted, "winword"));
        Assert.True(LearnedWordPolicy.Allows(true, muted, "notepad"));
    }
}
