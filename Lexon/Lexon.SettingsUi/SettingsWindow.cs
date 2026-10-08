using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Lexon.Core.Theming;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public sealed class SettingsWindow : Window
{
    private readonly GallerySettingsServices _services;
    private readonly SettingsPagesHost _host;
    private readonly Action<Window>? _showAbout;
    private readonly List<NavItem> _tabs = [];
    private ContentControl _content = null!;
    private bool _destroying;

    public SettingsWindow(ThemeManager? themes, GallerySettingsServices services, Action<Window>? showAbout = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _showAbout = showAbout;
        _host = SettingsPagesHost.GetOrCreate(services, this);
        _host.AddRef();
        _host.Retarget(this);
        _host.OpenFullScreenChanged += open =>
        {
            if (IsVisible)
            {
                WindowState = open ? WindowState.Maximized : WindowState.Normal;
            }
        };

        Title = "Lexon Settings";
        Width = SettingsWindowPlacement.DefaultWidth;
        Height = SettingsWindowPlacement.DefaultHeight;
        MinWidth = SettingsWindowPlacement.MinWidth;
        MinHeight = SettingsWindowPlacement.MinHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI");
        SetResourceReference(BackgroundProperty, "BackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");
        PreviewKeyDown += OnPreviewKeyDown;
        IsVisibleChanged += (_, _) =>
        {
            _host.OnVisible(IsVisible);
            if (IsVisible)
            {
                ApplyPlacement();
                FocusActivePage();
            }
            else
            {
                SaveBounds();
            }
        };
        Activated += (_, _) => _host.NotifyActivated();
        Deactivated += (_, _) => _host.NotifyDeactivated();
        Closing += (_, e) =>
        {
            if (_destroying)
            {
                return;
            }

            e.Cancel = true;
            FlushPendingSaves();
            SaveBounds();
            Hide();
        };
        Closed += (_, _) => _host.Release();
        Content = Build();
    }

    internal IReadOnlyList<string> TabNames => _tabs.Select(tab => (string)tab.Content).ToList();

    internal bool IsGeneralPageVisible => _content.Content == _host.GeneralPage;

    internal GeneralSettingsViewModel? GeneralViewModel => _host.General;

    internal AiSettingsViewModel? AiViewModel => _host.Ai;

    internal void RetargetHost() => _host.Retarget(this);

    internal void NotifyShown()
    {
        _host.NotifyShown();
        ApplyPlacement();
        FocusActivePage();
    }

    internal void NotifyActivated() => _host.NotifyActivated();

    internal void NotifyDeactivated() => _host.NotifyDeactivated();

    internal void FlushPendingSaves() => _host.FlushPendingSaves();

    internal void Destroy()
    {
        _destroying = true;
        Close();
    }

    internal void SelectTab(int index)
    {
        if (index >= 0 && index < _tabs.Count)
        {
            _tabs[index].IsChecked = true;
        }
    }

    private UIElement Build()
    {
        var root = new DockPanel();
        var nav = new StackPanel { Width = 168, Margin = new Thickness(12, 12, 0, 12) };
        KeyboardNavigation.SetTabNavigation(nav, KeyboardNavigationMode.Continue);
        AutomationProperties.SetName(nav, "Settings pages");
        DockPanel.SetDock(nav, Dock.Left);

        AddTab(nav, "General", () => _host.ShowGeneral(_content), first: true);
        if (_host.AiPage != null)
        {
            AddTab(nav, "AI", () => _host.ShowAi(_content));
        }

        if (_host.PrivacyPage != null)
        {
            AddTab(nav, "Privacy", () => _host.ShowPrivacy(_content));
        }

        if (_host.AppearancePage != null)
        {
            AddTab(nav, "Appearance", () => _host.ShowAppearance(_content));
        }

        if (_host.AppTonePage != null)
        {
            AddTab(nav, "App tone", () => _host.ShowAppTone(_content));
        }

        if (_host.WritingPage != null)
        {
            AddTab(nav, "Writing", () => _host.ShowWriting(_content));
        }

        var about = new Button
        {
            Content = "About",
            Margin = new Thickness(0, 16, 12, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        about.SetResourceReference(StyleProperty, "SubtleButton");
        AutomationProperties.SetName(about, "About");
        about.Click += (_, _) => _showAbout?.Invoke(this);
        nav.Children.Add(about);

        root.Children.Add(nav);
        _content = new ContentControl { Margin = new Thickness(8, 0, 0, 0) };
        _host.ShowGeneral(_content);
        root.Children.Add(_content);
        return root;
    }

    private void AddTab(StackPanel nav, string name, Action show, bool first = false)
    {
        var item = new NavItem
        {
            Content = name,
            GroupName = "settingsTabs",
            IsChecked = first,
            Margin = new Thickness(0, 0, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetName(item, name);
        item.Checked += (_, _) =>
        {
            show();
            FocusActivePage();
        };
        if (name == "AI")
        {
            item.Unchecked += (_, _) => _host.Ai?.OnTabLeft();
        }

        nav.Children.Add(item);
        _tabs.Add(item);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (Keyboard.FocusedElement is ComboBox { IsDropDownOpen: true } focused)
            {
                focused.IsDropDownOpen = false;
                e.Handled = true;
            }

            return;
        }

        if (e.Key != Key.Tab || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        Cycle((Keyboard.Modifiers & ModifierKeys.Shift) != 0);
        e.Handled = true;
    }

    internal void Cycle(bool reverse)
    {
        var current = _tabs.FindIndex(tab => tab.IsChecked == true);
        if (_tabs.Count == 0)
        {
            return;
        }

        var next = current < 0 ? 0 : (current + (reverse ? -1 : 1) + _tabs.Count) % _tabs.Count;
        _tabs[next].IsChecked = true;
    }

    private void ApplyPlacement()
    {
        var areas = SettingsWindowPlacement.VisibleWorkAreas();
        var desired = new Rect(
            double.IsNaN(Left) ? 80 : Left,
            double.IsNaN(Top) ? 80 : Top,
            Width,
            Height);
        if (_services.Profile != null
            && SettingsWindowPlacement.TryParse(_services.Profile.GetSetting(SettingsWindowPlacement.Key, string.Empty), out var saved))
        {
            desired = saved;
        }

        var clamped = SettingsWindowPlacement.Clamp(desired, areas);
        Left = clamped.X;
        Top = clamped.Y;
        Width = clamped.Width;
        Height = clamped.Height;
        if (_host.General.OpenSettingsFullScreen)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SaveBounds()
    {
        var profile = _services.Profile;
        if (profile == null)
        {
            return;
        }

        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;
        if (bounds.Width < 1 || bounds.Height < 1)
        {
            return;
        }

        profile.SetSetting(SettingsWindowPlacement.Key, SettingsWindowPlacement.Format(bounds));
        try
        {
            _ = profile.SaveAsync();
        }
        catch
        {
            // In-memory profiles have no storage. The value still sits on the profile.
        }
    }

    private void FocusActivePage()
    {
        if (_content.Content is UIElement page)
        {
            page.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }
    }
}
