using Lexon.Input;
using Xunit;

namespace Lexon.Tests;

public class CaretAnchorPolicyTests
{
    [Fact]
    public void FreezesOverlayInCursorNotChrome()
    {
        Assert.True(CaretAnchorPolicy.FreezeOverlayWhileWordContinues("Cursor.exe"));
        Assert.True(CaretAnchorPolicy.FreezeOverlayWhileWordContinues("WhatsApp.exe"));
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
    public void WhatsAppAnchorStaysInsideTheComposer()
    {
        var inside = CaretAnchorPolicy.ChooseComposerAnchor(100, 700, 500, 760, 0, (40, 200), (180, 720));
        Assert.Equal((180, 720), inside);

        var fallback = CaretAnchorPolicy.ChooseComposerAnchor(100, 700, 500, 760, 6, (40, 200));
        Assert.NotNull(fallback);
        Assert.True(fallback.Value.X > 100);
        Assert.Equal(700, fallback.Value.Y);
        Assert.True(fallback.Value.X < 500);
    }
}
