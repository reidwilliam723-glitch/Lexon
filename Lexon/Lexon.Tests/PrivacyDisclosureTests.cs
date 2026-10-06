using Lexon.Core;
using Lexon.Onboarding.Wizard;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class PrivacyDisclosureTests
{
    [Fact]
    public void Disclosure_ContainsLocalStayLeaveAndStayLocalGuidance()
    {
        Assert.Contains("Stays on this PC", PrivacyDisclosure.Body, StringComparison.Ordinal);
        Assert.Contains("Can leave this PC", PrivacyDisclosure.Body, StringComparison.Ordinal);
        Assert.Contains("Stay fully local", PrivacyDisclosure.Body, StringComparison.Ordinal);
        Assert.Contains("Local-only", PrivacyDisclosure.Body, StringComparison.Ordinal);
        Assert.Contains("off by default", PrivacyDisclosure.Body, StringComparison.Ordinal);
        Assert.Contains("API key", PrivacyDisclosure.Body, StringComparison.Ordinal);
        Assert.Equal("What Lexon can see…", PrivacyDisclosure.ButtonLabel);
    }

    [Fact]
    public void PrivacyDisclosureStep_RendersSharedTitleAndBody()
    {
        using var panel = new Panel { Width = 640, Height = 480 };
        var step = new PrivacyDisclosureStep();
        step.Initialize(panel, new Lexon.Onboarding.Models.OnboardingState());

        var texts = panel.Controls
            .Cast<Control>()
            .SelectMany(Flatten)
            .OfType<Label>()
            .Select(l => l.Text)
            .ToList();

        Assert.Contains(texts, t => t.Contains(PrivacyDisclosure.Title, StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("Stays on this PC", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("Stay fully local", StringComparison.Ordinal));
    }

    [Fact]
    public void ShowPrivacyPreview_ShowsSharedDisclosure()
    {
        var messages = new FakeMessages();
        var vm = new PrivacySettingsViewModel(
            new AppSettings(),
            new PersistScheduler(() => { }),
            new FakePolicy(),
            new FakePicker(),
            new FakeLog(),
            messages: messages);

        vm.ShowPrivacyPreview();

        Assert.Single(messages.Infos);
        Assert.Equal(PrivacyDisclosure.Title, messages.Infos[0].Caption);
        Assert.Equal(PrivacyDisclosure.Body, messages.Infos[0].Text);
    }

    private static IEnumerable<Control> Flatten(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }

    private sealed class FakeMessages : IMessageService
    {
        public List<(string Text, string Caption)> Infos { get; } = [];

        public void Info(string text, string caption) => Infos.Add((text, caption));

        public bool Confirm(string text, string caption) => true;
    }

    private sealed class FakePolicy : IAiPolicyPublisher
    {
        public void Publish(bool localOnly, bool typing, bool rewrite, bool prefetch)
        {
        }
    }

    private sealed class FakePicker : IProcessPicker
    {
        public string? Pick(string prompt) => null;
    }

    private sealed class FakeLog : ICloudAiActivityViewer
    {
        public void Show()
        {
        }
    }
}
