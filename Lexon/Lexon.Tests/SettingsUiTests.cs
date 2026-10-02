using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Lexon.Core.Theming;
using Lexon.SettingsUi;
using Lexon.Storage;
using Xunit;
using MediaColor = System.Windows.Media.Color;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfPasswordBox = System.Windows.Controls.PasswordBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Lexon.Tests;

public class SettingsUiTests
{
    [Fact]
    public void Smoke_LoadsThemesAndInstantiatesControls()
    {
        Sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempStore();
            var themes = new ThemeManager(store.Storage);
            foreach (var theme in themes.AvailableThemes)
            {
                WpfThemeBridge.ApplyTo(app, theme);
                InstantiateControls();
            }

            var windowsHc = WpfThemeBridge.Create(themes.CurrentTheme, windowsHighContrast: true);
            Assert.Contains("OnPrimaryHover", windowsHc.Keys.Cast<object>().Select(k => k.ToString()));
            InstantiateControls();
        });
    }

    [Fact]
    public void ButtonContrast_IsAtLeastFourPointFive()
    {
        Sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempStore();
            var themes = new ThemeManager(store.Storage);
            foreach (var theme in themes.AvailableThemes)
            {
                AssertThemeContrast(WpfThemeBridge.Create(theme, windowsHighContrast: false), theme.Name);
            }

            AssertThemeContrast(WpfThemeBridge.Create(themes.CurrentTheme, windowsHighContrast: true), "Windows High Contrast");
            _ = app;
        });
    }

    [Fact]
    public void ToggleSwitch_EndsAtExpectedX_AfterRapidToggles()
    {
        Sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempStore();
            var themes = new ThemeManager(store.Storage);
            WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);
            app.Resources["ToggleDuration"] = new Duration(TimeSpan.FromMilliseconds(40));

            var toggle = new ToggleSwitch();
            var window = new Window
            {
                Width = 80,
                Height = 40,
                Content = toggle,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Opacity = 0
            };
            window.Show();
            Pump(50);
            toggle.ApplyTemplate();

            toggle.IsChecked = true;
            Pump(80);
            Assert.Equal(ToggleSwitch.OnX, toggle.ThumbX, 1);

            toggle.IsChecked = false;
            Pump(80);
            Assert.Equal(ToggleSwitch.OffX, toggle.ThumbX, 1);

            toggle.IsChecked = true;
            toggle.IsChecked = false;
            toggle.IsChecked = true;
            toggle.IsChecked = false;
            Pump(80);
            Assert.Equal(ToggleSwitch.OffX, toggle.ThumbX, 1);

            window.Close();
        });
    }

    private static void InstantiateControls()
    {
        _ = new WpfButton { Content = "Primary" };
        var secondary = new WpfButton { Content = "Secondary" };
        secondary.SetResourceReference(FrameworkElement.StyleProperty, "SecondaryButton");
        var subtle = new WpfButton { Content = "Subtle" };
        subtle.SetResourceReference(FrameworkElement.StyleProperty, "SubtleButton");
        _ = new ToggleSwitch();
        _ = new WpfTextBox { Text = "sample" };
        _ = new WpfPasswordBox();
        _ = new WpfComboBox();
        _ = new WpfListBox();
        _ = new NavItem { Content = "General" };
        _ = new Card();
        _ = new SectionHeader { Title = "Title" };
        _ = new InfoTip { Tip = "Tip" };
        _ = new StatusPill { Content = "Ok" };
        _ = new InlineBanner { Content = "Note" };
        _ = new RevealPasswordBox { Password = "x" };
        _ = new System.Windows.Controls.CheckBox { Content = "Check" };
        _ = new System.Windows.Controls.RadioButton { Content = "Radio" };
        _ = new System.Windows.Controls.ToolTip { Content = "Tip" };
        _ = new System.Windows.Controls.Primitives.ScrollBar();
        _ = new System.Windows.Controls.ListBoxItem { Content = "Item" };
        _ = new System.Windows.Controls.ContextMenu();
        _ = new System.Windows.Controls.MenuItem { Header = "Copy" };
    }

    private static void AssertThemeContrast(ResourceDictionary dict, string label)
    {
        MediaColor ColorOf(string key) => (MediaColor)dict[key];

        var cases = new (string Name, MediaColor Fg, MediaColor Bg)[]
        {
            ("Primary rest", ColorOf("OnPrimary"), ColorOf("Primary")),
            ("Primary hover", ColorOf("OnPrimaryHover"), ColorOf("PrimaryHover")),
            ("Primary pressed", ColorOf("OnPrimary"), ColorOf("PrimaryPressed")),
            ("Secondary rest", ColorOf("Text"), ColorOf("SurfaceRaised")),
            ("Secondary hover", ColorOf("Text"), ColorOf("Surface")),
            ("Secondary pressed", ColorOf("Text"), ColorOf("Background")),
            ("Subtle rest", ColorOf("Primary"), ColorOf("Background")),
            ("Subtle hover", ColorOf("Primary"), ColorOf("PrimaryTint")),
            ("Subtle pressed", ColorOf("Primary"), ColorOf("Surface"))
        };

        foreach (var (name, fg, bg) in cases)
        {
            var ratio = WcagContrast.Ratio(fg, bg);
            Assert.True(ratio + 0.001 >= 4.5, $"{label} {name}: {ratio:0.00}:1 ({Format(fg)} on {Format(bg)})");
        }
    }

    private static string Format(MediaColor c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private sealed class TempStore : IDisposable
    {
        public EncryptedStorage Storage { get; }
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "lexon-ui-tests-" + Guid.NewGuid().ToString("N"));

        public TempStore()
        {
            Directory.CreateDirectory(_dir);
            Storage = new EncryptedStorage(_dir);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch
            {
                // temp
            }
        }
    }
}

internal static class Sta
{
    public static void Run(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}

internal static class WcagContrast
{
    public static double Ratio(MediaColor a, MediaColor b)
    {
        var l1 = Luminance(a);
        var l2 = Luminance(b);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(MediaColor color)
    {
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static double Channel(byte value)
    {
        var c = value / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
