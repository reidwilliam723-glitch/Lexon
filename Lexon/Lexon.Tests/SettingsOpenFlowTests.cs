using Lexon.SettingsUi;
using Xunit;

namespace Lexon.Tests;

public class SettingsOpenFlowTests
{
    [Fact]
    public void WpfThrowsOnce_OpensClassic_ReportsOnce_AndTheNextRequestTriesWpfAgain()
    {
        var router = new SettingsRouter(classicSettings: false, galleryEnabled: false);
        var wpf = 0;
        var classic = 0;
        var errors = 0;

        SettingsOpenFlow.Open(
            router,
            SettingsEntryPoint.Tray,
            () =>
            {
                wpf++;
                throw new InvalidOperationException("boom");
            },
            () => classic++,
            _ => errors++);

        Assert.Equal(1, wpf);
        Assert.Equal(1, classic);
        Assert.Equal(1, errors);
        Assert.Equal(1, router.WpfFailures);
        Assert.False(router.WpfGaveUp);

        SettingsOpenFlow.Open(
            router,
            SettingsEntryPoint.Shortcut,
            () => wpf++,
            () => classic++,
            _ => errors++);

        Assert.Equal(2, wpf);
        Assert.Equal(1, classic);
        Assert.Equal(1, errors);
    }

    [Fact]
    public void WpfThrowsTwice_ThenTheThirdRequestOpensClassicWithoutWpfOrAnError()
    {
        var router = new SettingsRouter(classicSettings: false, galleryEnabled: false);
        var wpf = 0;
        var classic = 0;
        var errors = 0;

        void Fail() => SettingsOpenFlow.Open(
            router,
            SettingsEntryPoint.Tray,
            () =>
            {
                wpf++;
                throw new InvalidOperationException("boom");
            },
            () => classic++,
            _ => errors++);

        Fail();
        Fail();

        Assert.Equal(2, errors);
        Assert.True(router.WpfGaveUp);

        var wpfBefore = wpf;
        var errorsBefore = errors;
        SettingsOpenFlow.Open(
            router,
            SettingsEntryPoint.ShowEvent,
            () => wpf++,
            () => classic++,
            _ => errors++);

        Assert.Equal(wpfBefore, wpf);
        Assert.Equal(errorsBefore, errors);
        Assert.Equal(3, classic);
    }

    [Fact]
    public void WarmFailure_IncrementsFailuresWithoutReportingAnError()
    {
        var router = new SettingsRouter(classicSettings: false, galleryEnabled: false);
        var classic = 0;
        var errors = 0;

        SettingsOpenFlow.Open(
            router,
            SettingsEntryPoint.Warm,
            () => throw new InvalidOperationException("warm"),
            () => classic++,
            null);

        Assert.Equal(1, classic);
        Assert.Equal(0, errors);
        Assert.Equal(1, router.WpfFailures);
    }

    [Fact]
    public void ClassicSettings_NeverTouchesWpf()
    {
        var router = new SettingsRouter(classicSettings: true, galleryEnabled: false);
        var wpf = 0;
        var classic = 0;
        var errors = 0;

        SettingsOpenFlow.Open(
            router,
            SettingsEntryPoint.Tray,
            () => wpf++,
            () => classic++,
            _ => errors++);

        Assert.Equal(0, wpf);
        Assert.Equal(1, classic);
        Assert.Equal(0, errors);
        Assert.Equal(0, router.WpfFailures);
    }
}
