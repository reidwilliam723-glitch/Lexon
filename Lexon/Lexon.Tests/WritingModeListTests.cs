using Lexon.Service;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class WritingModeListTests
{
    [Fact]
    public void SettingsLabels_MatchRewriteDefaultChoices_InOrder()
    {
        Assert.Equal(SelectionRewriteService.DefaultModeChoices, WritingViewModel.WritingModeLabels);
    }
}