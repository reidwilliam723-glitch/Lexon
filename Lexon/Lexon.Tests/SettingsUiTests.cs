using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Lexon.Core.Theming;
using Lexon.SettingsUi;
using Lexon.Storage;
using Xunit;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.Brush;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfListBoxItem = System.Windows.Controls.ListBoxItem;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfPasswordBox = System.Windows.Controls.PasswordBox;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfToolTip = System.Windows.Controls.ToolTip;

namespace Lexon.Tests;

[Collection("WpfSta")]
public class SettingsUiTests
{
    private readonly WpfStaFixture _sta;

    public SettingsUiTests(WpfStaFixture sta)
    {
        _sta = sta;
    }

    [Fact]
    public void Smoke_LoadsThemesAndLaysOutControls()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempStore();
            var themes = new ThemeManager(store.Storage);
            foreach (var theme in themes.AvailableThemes)
            {
                WpfThemeBridge.ApplyTo(app, theme);
                LayoutControls();
            }

            ApplyWindowsHighContrast(app, themes.CurrentTheme);
            LayoutControls();
        });
    }

    [Fact]
    public void ButtonContrast_IsAtLeastFourPointFive()
    {
        _sta.Run(() =>
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
    public void ButtonStates_DifferFromRest_InBackgroundOrBorder()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempStore();
            var themes = new ThemeManager(store.Storage);
            foreach (var theme in themes.AvailableThemes)
            {
                WpfThemeBridge.ApplyTo(app, theme);
                AssertStatesDiffer(theme.Name);
            }

            ApplyWindowsHighContrast(app, themes.CurrentTheme);
            AssertStatesDiffer("Windows High Contrast");
        });
    }

    [Fact]
    public void ButtonLiveStyle_HasReadableContrast()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempStore();
            var themes = new ThemeManager(store.Storage);
            foreach (var theme in themes.AvailableThemes)
            {
                WpfThemeBridge.ApplyTo(app, theme);
                AssertLiveButtonContrast(theme.Name);
            }

            ApplyWindowsHighContrast(app, themes.CurrentTheme);
            AssertLiveButtonContrast("Windows High Contrast");
        });
    }

    [Fact]
    public void ThemeBridge_DoesNotCollideWithXamlKeys()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempStore();
            var themes = new ThemeManager(store.Storage);
            var created = WpfThemeBridge.Create(themes.CurrentTheme, windowsHighContrast: false);
            var tokens = LoadDictionary("Themes/Tokens.xaml");
            var generic = LoadDictionary("Themes/Generic.xaml");
            foreach (var key in created.Keys)
            {
                Assert.False(tokens.Contains(key), $"Tokens.xaml already defines '{key}', so WpfThemeBridge cannot override it.");
                Assert.False(generic.Contains(key), $"Generic.xaml already defines '{key}', so WpfThemeBridge cannot override it.");
            }

            _ = app;
        });
    }

    [Fact]
    public void ToggleSwitch_EndsAtExpectedX_AfterRapidToggles()
    {
        _sta.Run(() =>
        {
            var app = WpfBootstrap.EnsureApplication();
            using var store = new TempStore();
            var themes = new ThemeManager(store.Storage);
            WpfThemeBridge.ApplyTo(app, themes.CurrentTheme);
            app.Resources["ToggleDuration"] = new Duration(TimeSpan.FromMilliseconds(40));

            var toggle = new ToggleSwitch();
            var window = Offscreen(toggle);
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

    private static void LayoutControls()
    {
        var host = new System.Windows.Controls.WrapPanel();
        foreach (var control in CreateControls())
        {
            host.Children.Add(control);
        }

        var window = Offscreen(host);
        window.Show();
        window.UpdateLayout();
        foreach (FrameworkElement child in host.Children)
        {
            child.ApplyTemplate();
            child.UpdateLayout();
        }

        window.Close();
    }

    private static IEnumerable<FrameworkElement> CreateControls()
    {
        yield return new WpfButton { Content = "Primary" };
        var secondary = new WpfButton { Content = "Secondary" };
        secondary.SetResourceReference(FrameworkElement.StyleProperty, "SecondaryButton");
        yield return secondary;
        var subtle = new WpfButton { Content = "Subtle" };
        subtle.SetResourceReference(FrameworkElement.StyleProperty, "SubtleButton");
        yield return subtle;
        yield return new ToggleSwitch();
        yield return new WpfTextBox { Text = "sample" };
        yield return new WpfPasswordBox();
        yield return new WpfComboBox();
        yield return new WpfListBox();
        yield return new NavItem { Content = "General" };
        yield return new Card();
        yield return new SectionHeader { Title = "Title" };
        yield return new InfoTip { Tip = "Tip" };
        yield return new StatusPill { Content = "Ok" };
        yield return new InlineBanner { Content = "Note" };
        yield return new RevealPasswordBox { Password = "x" };
        yield return new WpfCheckBox { Content = "Check" };
        yield return new WpfRadioButton { Content = "Radio" };
        var tipHost = new WpfButton { Content = "Tip host" };
        tipHost.ToolTip = new WpfToolTip { Content = "Tip" };
        yield return tipHost;
        yield return new WpfScrollBar();
        yield return new WpfListBoxItem { Content = "Item" };
        var menuHost = new WpfButton { Content = "Menu host" };
        var menu = new WpfContextMenu();
        menu.Items.Add(new WpfMenuItem { Header = "Copy" });
        menuHost.ContextMenu = menu;
        yield return menuHost;
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

    private static void AssertStatesDiffer(string label)
    {
        foreach (var (name, button) in StyledButtons())
        {
            var window = Offscreen(button);
            window.Show();
            button.ApplyTemplate();
            button.UpdateLayout();
            var rest = Snapshot(button);
            ApplyTrigger(button, UIElement.IsMouseOverProperty, true);
            var hover = Snapshot(button);
            ResetSnapshot(button, rest);
            ApplyTrigger(button, System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, true);
            var pressed = Snapshot(button);
            AssertVisibleChange($"{label} {name} hover", rest, hover);
            AssertVisibleChange($"{label} {name} pressed", rest, pressed);
            window.Close();
        }
    }

    private static void AssertLiveButtonContrast(string label)
    {
        foreach (var (name, button) in StyledButtons())
        {
            var window = Offscreen(button);
            window.Show();
            button.ApplyTemplate();
            button.UpdateLayout();
            AssertReadable($"{label} {name} rest", button);
            ApplyTrigger(button, UIElement.IsMouseOverProperty, true);
            AssertReadable($"{label} {name} hover", button);
            ApplyTrigger(button, System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, true);
            AssertReadable($"{label} {name} pressed", button);
            window.Close();
        }
    }

    private static IEnumerable<(string Name, WpfButton Button)> StyledButtons()
    {
        yield return ("Primary", new WpfButton { Content = "Primary" });
        var secondary = new WpfButton { Content = "Secondary" };
        secondary.SetResourceReference(FrameworkElement.StyleProperty, "SecondaryButton");
        yield return ("Secondary", secondary);
        var subtle = new WpfButton { Content = "Subtle" };
        subtle.SetResourceReference(FrameworkElement.StyleProperty, "SubtleButton");
        yield return ("Subtle", subtle);
    }

    private static void AssertReadable(string label, WpfButton button)
    {
        var fg = BrushColor(button.Foreground);
        var bg = EffectiveBackground(button);
        var ratio = WcagContrast.Ratio(fg, bg);
        Assert.True(ratio + 0.001 >= 4.5, $"{label}: {ratio:0.00}:1 ({Format(fg)} on {Format(bg)})");
    }

    private static MediaColor EffectiveBackground(WpfButton button)
    {
        var bg = BrushColor(button.Background);
        if (bg.A == 0)
        {
            return BrushColor(button.TryFindResource("BackgroundBrush") as MediaBrush);
        }

        return bg;
    }

    private static (MediaColor Background, MediaColor Border) Snapshot(WpfButton button)
        => (BrushColor(button.Background), BrushColor(button.BorderBrush));

    private static void ResetSnapshot(WpfButton button, (MediaColor Background, MediaColor Border) rest)
    {
        button.Background = new SolidColorBrush(rest.Background);
        button.BorderBrush = new SolidColorBrush(rest.Border);
    }

    private static void AssertVisibleChange(string label, (MediaColor Background, MediaColor Border) rest, (MediaColor Background, MediaColor Border) next)
    {
        var changed = Differs(rest.Background, next.Background) || Differs(rest.Border, next.Border);
        Assert.True(changed, $"{label} is identical to rest (bg {Format(rest.Background)} / border {Format(rest.Border)}).");
    }

    private static bool Differs(MediaColor a, MediaColor b)
        => Math.Abs(a.R - b.R) > 4 || Math.Abs(a.G - b.G) > 4 || Math.Abs(a.B - b.B) > 4 || Math.Abs(a.A - b.A) > 4;

    private static void ApplyTrigger(FrameworkElement element, DependencyProperty property, object value)
    {
        for (var style = element.Style ?? element.TryFindResource(element.GetType()) as Style; style != null; style = style.BasedOn)
        {
            foreach (var trigger in style.Triggers.OfType<Trigger>())
            {
                if (trigger.Property == property && Equals(trigger.Value, value))
                {
                    ApplySetters(element, trigger);
                }
            }
        }
    }

    private static void ApplySetters(FrameworkElement element, Trigger trigger)
    {
        foreach (var setter in trigger.Setters.OfType<Setter>())
        {
            if (setter.Property == null)
            {
                continue;
            }

            if (setter.Value is DynamicResourceExtension dyn && dyn.ResourceKey != null)
            {
                element.SetResourceReference(setter.Property, dyn.ResourceKey);
            }
            else
            {
                element.SetValue(setter.Property, setter.Value);
            }
        }
    }

    private static MediaColor BrushColor(MediaBrush? brush)
    {
        if (brush is SolidColorBrush solid)
        {
            return solid.Color;
        }

        return Colors.Transparent;
    }

    private static void ApplyWindowsHighContrast(WpfApplication app, Theme theme)
    {
        var next = WpfThemeBridge.Create(theme, windowsHighContrast: true);
        next["LexonThemeId"] = WpfThemeBridge.DictionaryKey;
        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => Equals(d["LexonThemeId"], WpfThemeBridge.DictionaryKey));
        if (existing != null)
        {
            app.Resources.MergedDictionaries[app.Resources.MergedDictionaries.IndexOf(existing)] = next;
        }
        else
        {
            app.Resources.MergedDictionaries.Insert(0, next);
        }
    }

    private static ResourceDictionary LoadDictionary(string relative)
        => new()
        {
            Source = new Uri($"pack://application:,,,/Lexon.SettingsUi;component/{relative}", UriKind.Absolute)
        };

    private static Window Offscreen(UIElement content)
        => new()
        {
            Width = 480,
            Height = 240,
            Content = content,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Opacity = 0,
            Left = -20000,
            Top = -20000
        };

    private static string Format(MediaColor c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

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
        => 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);

    private static double Channel(byte value)
    {
        var c = value / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
