using Lexon.AI.Interfaces;
using Lexon.Core;
using Lexon.Core.Interfaces;
using Lexon.Core.Learning;
using Lexon.Core.Models;
using Lexon.Input;
using Lexon.Input.Interfaces;
using Lexon.Overlay.Interfaces;
using Lexon.Privacy;
using Lexon.Profiles;
using Lexon.Service;
using Lexon.SettingsModel;
using Moq;
using Xunit;

namespace Lexon.Tests;

public class AiSendCheckTests
{
    [Theory]
    [InlineData(AiSendScope.Prefetch, "OpenAI will receive the selected text before you choose a rewrite.")]
    [InlineData(AiSendScope.Rewrite, "OpenAI will receive the selected text for this rewrite.")]
    [InlineData(AiSendScope.Typing, "OpenAI will receive the words around the caret.")]
    public void Message_NamesProviderAndScope(AiSendScope scope, string expected)
        => Assert.Equal(expected, AiSendCheck.Message("OpenAI", scope));

    [Fact]
    public void NeedsPrompt_UntilThatScopeIsAllowed()
    {
        Assert.True(AiSendCheck.NeedsPrompt(true, [], AiSendScope.Prefetch, true));
        Assert.False(AiSendCheck.NeedsPrompt(true, ["Prefetch"], AiSendScope.Prefetch, true));
        Assert.True(AiSendCheck.NeedsPrompt(true, ["Prefetch"], AiSendScope.Rewrite, true));
        Assert.False(AiSendCheck.NeedsPrompt(false, [], AiSendScope.Prefetch, true));
        Assert.False(AiSendCheck.NeedsPrompt(true, [], AiSendScope.Prefetch, false));
    }
}

public class AppControlEditorTests
{
    [Fact]
    public void Apply_UpdatesTheExistingListsForOneApp()
    {
        var settings = new AppSettings
        {
            GrammarMutedApps = ["slack.exe"],
            LearnedWordsMutedApps = ["slack"]
        };

        AppControlEditor.Apply(settings, new AppControlState("Slack", true, "Formal", true, false));

        Assert.Equal(["Slack"], settings.BlockedApplications);
        Assert.Contains("slack.exe|Formal", settings.AppCategoryOverrides);
        Assert.DoesNotContain("slack.exe", settings.GrammarMutedApps);
        Assert.Contains("Slack", settings.LearnedWordsMutedApps);

        var read = AppControlEditor.Read(settings, "slack.exe");
        Assert.True(read.BlockAssistance);
        Assert.Equal("Formal", read.Tone);
        Assert.True(read.Grammar);
        Assert.False(read.LearnedWords);
        Assert.Contains("Slack", AppControlEditor.Apps(settings));
    }
}

public class AppsSettingsViewModelTests
{
    [Fact]
    public void BlockingAnApp_UpdatesTheSharedBlockedList()
    {
        var settings = new AppSettings();
        var persist = new PersistScheduler(() => { });
        var vm = new AppsSettingsViewModel(settings, persist, new Picker());
        vm.Load();
        vm.SelectApp("notepad");
        vm.BlockAssistance = true;

        Assert.Equal(["notepad"], settings.BlockedApplications);
        Assert.True(vm.IsDirty);
    }

    private sealed class Picker : IProcessPicker
    {
        public string? Pick(string title) => "notepad";
    }
}

public class PrefetchSendCheckTests
{
    [Fact]
    public async Task Prefetch_AsksBeforeSendingTheSelection()
    {
        var ai = new Mock<IAIProvider>();
        ai.Setup(p => p.Name).Returns("OpenAI");
        ai.Setup(p => p.RewriteTextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<string>?>()))
            .ReturnsAsync("rewritten");

        var focus = new Mock<IFocusTracker>();
        focus.Setup(f => f.GetCurrentContext()).Returns(new TextContext
        {
            ApplicationName = "notepad",
            FullText = "hello there",
            CursorPosition = 11
        });
        focus.Setup(f => f.GetCaretScreenPosition()).Returns((10, 10));
        string selected = "hello there";
        focus.Setup(f => f.TryGetSelectedText(out selected)).Returns(true);

        var menu = new Mock<IActionMenuOverlay>();
        var policy = new AiAccessPolicy();
        policy.Update(false, false, true, true);
        var injector = new Mock<ITextInjector>();
        var service = new SelectionRewriteService(
            ai.Object,
            focus.Object,
            injector.Object,
            new UndoManager(injector.Object),
            new Mock<IEditConfirmation>().Object,
            menu.Object,
            new PersonalizationManager(new Mock<IStorage>().Object, new FeedbackCollector()),
            new Profile { Id = "t" },
            new PrivacyGuard(),
            accessPolicy: policy);

        service.ConsiderSelectionAffordance();

        ai.Verify(p => p.RewriteTextAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<IProgress<string>?>()), Times.Never);
        menu.Verify(m => m.ShowMenu(
            It.Is<IReadOnlyList<string>>(items => items.Contains(AiSendCheck.SendOnce)),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.Is<string>(caption => caption.Contains("OpenAI") && caption.Contains("before you choose a rewrite"))));

        menu.Raise(m => m.ItemSelected += null, new ActionMenuItemEventArgs { Text = AiSendCheck.SendOnce });
        await Task.Delay(50);
        ai.Verify(p => p.RewriteTextAsync(
            "hello there",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<IProgress<string>?>()), Times.Once);
    }
}
