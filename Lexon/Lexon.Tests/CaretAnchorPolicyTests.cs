using Lexon.Input;
using Xunit;

namespace Lexon.Tests;

public class CaretAnchorPolicyTests
{
    [Fact]
    public void FreezesOverlayInCursorNotChrome()
    {
        Assert.True(CaretAnchorPolicy.FreezeOverlayWhileWordContinues("Cursor.exe"));
        Assert.False(CaretAnchorPolicy.FreezeOverlayWhileWordContinues("WhatsApp.exe"));
        Assert.False(CaretAnchorPolicy.FreezeOverlayWhileWordContinues("chrome"));
    }

    [Fact]
    public void RejectsToolbarPointsAboveRenderWidget()
    {
        Assert.True(CaretAnchorPolicy.IsLikelyBrowserChrome(80, 40, 140));
        Assert.False(CaretAnchorPolicy.IsLikelyBrowserChrome(200, 40, 140));
    }

    [Fact]
    public void DetectsCaretTeleportVersusTyping()
    {
        Assert.True(CaretAnchorPolicy.IsTeleport(400, 400, 80, 80, 20));
        Assert.True(CaretAnchorPolicy.IsTypingDrift(400, 400, 412, 400, 20));
        Assert.False(CaretAnchorPolicy.IsTypingDrift(400, 400, 80, 80, 20));
    }

    [Fact]
    public void WhatsAppAnchorFollowsTypedTextOnTheTextLine()
    {
        var first = CaretAnchorPolicy.PlaceInComposer(100, 700, 500, 760, charsBeforeWord: 0);
        var next = CaretAnchorPolicy.PlaceInComposer(100, 700, 500, 760, charsBeforeWord: 6);
        Assert.NotNull(first);
        Assert.NotNull(next);
        Assert.True(next.Value.X > first.Value.X);
        Assert.Equal(CaretAnchorPolicy.ComposerTextLineHeight, first.Value.LineHeight);
        Assert.True(first.Value.Y > 700);
        Assert.True(first.Value.Y < 760 - 10);

        var pulledLeft = CaretAnchorPolicy.PlaceInComposer(100, 700, 500, 760, charsBeforeWord: 6, trackedCaretX: 108);
        Assert.Equal(next.Value.X, pulledLeft!.Value.X);
    }
}
