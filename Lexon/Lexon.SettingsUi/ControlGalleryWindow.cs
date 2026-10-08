using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Lexon.Core.Theming;
using Lexon.Profiles;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public sealed class ControlGalleryWindow : Window
{
    private readonly ThemeManager? _themes;
    private readonly GallerySettingsServices? _services;
    private readonly ClipboardHwndListener _clipboard = new();
    private ComboBox _demoCombo = null!;
    private UIElement _demoHost = null!;
    private UIElement _controlsContent = null!;
    private ContentControl? _contentHost;
    private NavItem? _controlsTab;
    private NavItem? _generalTab;
    private NavItem? _aiTab;
    private NavItem? _privacyTab;
    private NavItem? _appearanceTab;
    private NavItem? _appToneTab;
    private NavItem? _writingTab;
    private NavItem? _appsTab;
    private GeneralPage? _generalPage;
    private AiPage? _aiPage;
    private PrivacyPage? _privacyPage;
    private AppearancePage? _appearancePage;
    private AppTonePage? _appTonePage;
    private WritingPage? _writingPage;
    private AppsPage? _appsPage;
    private GeneralSettingsViewModel? _generalVm;
    private AiSettingsViewModel? _aiVm;
    private PrivacySettingsViewModel? _privacyVm;
    private AppearanceSettingsViewModel? _appearanceVm;
    private AppToneViewModel? _appToneVm;
    private WritingViewModel? _writingVm;
    private AppsSettingsViewModel? _appsVm;
    private SettingsPagesHost? _host;

    public ControlGalleryWindow(ThemeManager? themes = null, GallerySettingsServices? services = null)
    {
        _themes = themes;
        _services = services;
        Title = "Lexon control gallery";
        Width = 720;
        Height = 720;
        MinWidth = 480;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI");
        SetResourceReference(BackgroundProperty, "BackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");
        PreviewKeyDown += OnPreviewKeyDown;
        IsVisibleChanged += OnIsVisibleChanged;
        Activated += OnActivated;
        Deactivated += OnDeactivated;
        Closed += (_, _) => _host?.Release();

        if (services != null)
        {
            _host = SettingsPagesHost.GetOrCreate(services, this);
            _host.AddRef();
            _host.Retarget(this);
            _host.OpenFullScreenChanged += OnOpenFullScreenChanged;
            _generalVm = _host.General;
            _generalPage = _host.GeneralPage;
            _aiVm = _host.Ai;
            _aiPage = _host.AiPage;
            _privacyVm = _host.Privacy;
            _privacyPage = _host.PrivacyPage;
            _appearanceVm = _host.Appearance;
            _appearancePage = _host.AppearancePage;
            _appToneVm = _host.AppTone;
            _appTonePage = _host.AppTonePage;
            _writingVm = _host.Writing;
            _writingPage = _host.WritingPage;
            _appsVm = _host.Apps;
            _appsPage = _host.AppsPage;
        }

        Content = Build();
    }

    internal bool HasGeneralTab => _generalTab != null;

    internal bool HasAiTab => _aiTab != null;

    internal bool HasAppearanceTab => _appearanceTab != null;

    internal bool HasPrivacyTab => _privacyTab != null;

    internal bool HasAppToneTab => _appToneTab != null;

    internal bool HasWritingTab => _writingTab != null;

    internal bool HasAppsTab => _appsTab != null;

    internal int GalleryTabCount
        => (_controlsTab != null ? 1 : 0)
           + (_generalTab != null ? 1 : 0)
           + (_aiTab != null ? 1 : 0)
           + (_privacyTab != null ? 1 : 0)
           + (_appearanceTab != null ? 1 : 0)
           + (_appToneTab != null ? 1 : 0)
           + (_writingTab != null ? 1 : 0)
           + (_appsTab != null ? 1 : 0);

    internal bool IsGeneralPageVisible => _generalPage != null && _contentHost?.Content == _generalPage;

    internal bool IsAiPageVisible => _aiPage != null && _contentHost?.Content == _aiPage;

    internal bool IsAppearancePageVisible => _appearancePage != null && _contentHost?.Content == _appearancePage;

    internal bool IsPrivacyPageVisible => _privacyPage != null && _contentHost?.Content == _privacyPage;

    internal bool IsAppTonePageVisible => _appTonePage != null && _contentHost?.Content == _appTonePage;

    internal bool IsWritingPageVisible => _writingPage != null && _contentHost?.Content == _writingPage;

    internal GeneralSettingsViewModel? GeneralViewModel => _generalVm;

    internal AiSettingsViewModel? AiViewModel => _aiVm;

    internal ClipboardHwndListener ClipboardListener => _host?.Clipboard ?? _clipboard;

    internal AppearanceSettingsViewModel? AppearanceViewModel => _appearanceVm;

    internal PrivacySettingsViewModel? PrivacyViewModel => _privacyVm;

    internal AppToneViewModel? AppToneViewModel => _appToneVm;

    internal WritingViewModel? WritingViewModel => _writingVm;

    internal void SelectGeneralTab()
    {
        if (_generalTab != null)
        {
            _generalTab.IsChecked = true;
        }
    }

    internal void SelectControlsTab()
    {
        if (_controlsTab != null)
        {
            _controlsTab.IsChecked = true;
        }
    }

    internal void SelectAiTab()
    {
        if (_aiTab != null)
        {
            _aiTab.IsChecked = true;
        }
    }

    internal void SelectAppearanceTab()
    {
        if (_appearanceTab != null)
        {
            _appearanceTab.IsChecked = true;
        }
    }

    internal void SelectPrivacyTab()
    {
        if (_privacyTab != null)
        {
            _privacyTab.IsChecked = true;
        }
    }

    internal void SelectAppToneTab()
    {
        if (_appToneTab != null)
        {
            _appToneTab.IsChecked = true;
        }
    }

    internal void SelectWritingTab()
    {
        if (_writingTab != null)
        {
            _writingTab.IsChecked = true;
        }
    }

    internal void NotifyShown() => _host?.NotifyShown();

    internal void FlushPendingSaves() => _host?.FlushPendingSaves();

    /// <summary>
    /// Test seam for activate: reload clean pages from the profile.
    /// </summary>
    internal void NotifyActivated() => _host?.NotifyActivated();

    /// <summary>
    /// Test seam for deactivate: flush pending dirty pages immediately.
    /// </summary>
    internal void NotifyDeactivated() => _host?.NotifyDeactivated();

    private void OnActivated(object? sender, EventArgs e) => NotifyActivated();

    private void OnDeactivated(object? sender, EventArgs e) => NotifyDeactivated();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (Keyboard.FocusedElement is ComboBox { IsDropDownOpen: true } focused)
        {
            focused.IsDropDownOpen = false;
            e.Handled = true;
            return;
        }

        if (_demoCombo is { IsDropDownOpen: true })
        {
            _demoCombo.IsDropDownOpen = false;
            e.Handled = true;
            return;
        }

        Close();
    }

    private UIElement Build()
    {
        var root = new DockPanel();
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16) };
        toolbar.Children.Add(ThemeButton("Light"));
        toolbar.Children.Add(ThemeButton("Dark"));
        toolbar.Children.Add(ThemeButton("High Contrast"));
        var disabled = new ToggleSwitch { Margin = new Thickness(16, 0, 8, 0) };
        AutomationProperties.SetName(disabled, "Disabled");
        var disabledLabel = new TextBlock
        {
            Text = "Disabled",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        disabled.Checked += (_, _) => _demoHost.IsEnabled = false;
        disabled.Unchecked += (_, _) => _demoHost.IsEnabled = true;
        toolbar.Children.Add(disabled);
        toolbar.Children.Add(disabledLabel);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        _controlsContent = BuildControls();

        if (_services == null)
        {
            root.Children.Add(_controlsContent);
            return root;
        }

        var tabs = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(16, 0, 16, 8)
        };
        KeyboardNavigation.SetTabNavigation(tabs, KeyboardNavigationMode.Once);
        KeyboardNavigation.SetDirectionalNavigation(tabs, KeyboardNavigationMode.Cycle);
        AutomationProperties.SetName(tabs, "Gallery tabs");

        _controlsTab = new NavItem
        {
            Content = "Controls",
            IsChecked = true,
            GroupName = "galleryTabs",
            Margin = new Thickness(0, 0, 8, 4)
        };
        AutomationProperties.SetName(_controlsTab, "Controls");
        _controlsTab.Checked += (_, _) => ShowControlsContent();

        _generalTab = new NavItem { Content = "General", GroupName = "galleryTabs", Margin = new Thickness(0, 0, 8, 4) };
        AutomationProperties.SetName(_generalTab, "General");
        _generalTab.Checked += (_, _) => ShowGeneralContent();

        tabs.Children.Add(_controlsTab);
        tabs.Children.Add(_generalTab);

        if (_aiPage != null)
        {
            _aiTab = new NavItem { Content = "AI", GroupName = "galleryTabs", Margin = new Thickness(0, 0, 8, 4) };
            AutomationProperties.SetName(_aiTab, "AI");
            _aiTab.Checked += (_, _) => ShowAiContent();
            _aiTab.Unchecked += (_, _) => _aiVm?.OnTabLeft();
            tabs.Children.Add(_aiTab);
        }

        if (_privacyPage != null)
        {
            _privacyTab = new NavItem { Content = "Privacy", GroupName = "galleryTabs", Margin = new Thickness(0, 0, 8, 4) };
            AutomationProperties.SetName(_privacyTab, "Privacy");
            _privacyTab.Checked += (_, _) => ShowPrivacyContent();
            tabs.Children.Add(_privacyTab);
        }

        if (_appearancePage != null)
        {
            _appearanceTab = new NavItem { Content = "Appearance", GroupName = "galleryTabs", Margin = new Thickness(0, 0, 8, 4) };
            AutomationProperties.SetName(_appearanceTab, "Appearance");
            _appearanceTab.Checked += (_, _) => ShowAppearanceContent();
            tabs.Children.Add(_appearanceTab);
        }

        if (_appTonePage != null)
        {
            _appToneTab = new NavItem { Content = "App tone", GroupName = "galleryTabs", Margin = new Thickness(0, 0, 8, 4) };
            AutomationProperties.SetName(_appToneTab, "App tone");
            _appToneTab.Checked += (_, _) => ShowAppToneContent();
            tabs.Children.Add(_appToneTab);
        }

        if (_writingPage != null)
        {
            _writingTab = new NavItem { Content = "Writing", GroupName = "galleryTabs", Margin = new Thickness(0, 0, 8, 4) };
            AutomationProperties.SetName(_writingTab, "Writing");
            _writingTab.Checked += (_, _) => ShowWritingContent();
            tabs.Children.Add(_writingTab);
        }

        if (_appsPage != null)
        {
            _appsTab = new NavItem { Content = "Apps", GroupName = "galleryTabs", Margin = new Thickness(0, 0, 8, 4) };
            AutomationProperties.SetName(_appsTab, "Apps");
            _appsTab.Checked += (_, _) => ShowAppsContent();
            tabs.Children.Add(_appsTab);
        }

        DockPanel.SetDock(tabs, Dock.Top);
        root.Children.Add(tabs);

        _contentHost = new ContentControl { Content = _controlsContent };
        root.Children.Add(_contentHost);
        return root;
    }

    private UIElement BuildControls()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack = new StackPanel { Margin = new Thickness(16), MaxWidth = 560 };
        _demoHost = stack;

        stack.Children.Add(new SectionHeader { Title = "Buttons" });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 16) };
        var primary = new Button { Content = "Primary", Margin = new Thickness(0, 0, 8, 0) };
        AutomationProperties.SetName(primary, "Primary");
        buttons.Children.Add(primary);
        var secondary = new Button { Content = "Secondary", Margin = new Thickness(0, 0, 8, 0) };
        secondary.SetResourceReference(StyleProperty, "SecondaryButton");
        AutomationProperties.SetName(secondary, "Secondary");
        buttons.Children.Add(secondary);
        var subtle = new Button { Content = "Subtle" };
        subtle.SetResourceReference(StyleProperty, "SubtleButton");
        AutomationProperties.SetName(subtle, "Subtle");
        buttons.Children.Add(subtle);
        stack.Children.Add(buttons);

        stack.Children.Add(new SectionHeader { Title = "Toggle, combo, fields" });
        var toggle = new ToggleSwitch { Margin = new Thickness(0, 8, 0, 8) };
        AutomationProperties.SetName(toggle, "Demo toggle");
        stack.Children.Add(toggle);
        _demoCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 8), MinWidth = 240, MaxDropDownHeight = 140 };
        foreach (var item in new[]
                 {
                     "Most Relevant", "Most Used", "Newest", "Oldest", "A to Z", "Z to A",
                     "Recently edited", "Recently opened", "By author", "By size",
                     "By language", "By folder", "Pinned first", "Unsorted"
                 })
        {
            _demoCombo.Items.Add(item);
        }

        _demoCombo.SelectedIndex = 0;
        AutomationProperties.SetName(_demoCombo, "Suggestion sort");
        stack.Children.Add(_demoCombo);

        var text = new TextBox { Text = "Sample text", Margin = new Thickness(0, 0, 0, 8) };
        AutomationProperties.SetName(text, "Sample text");
        stack.Children.Add(text);

        var password = new RevealPasswordBox { Password = "sk-demo", Margin = new Thickness(0, 0, 0, 16) };
        AutomationProperties.SetName(password, "API key");
        stack.Children.Add(password);

        stack.Children.Add(new SectionHeader { Title = "Check and radio" });
        var checks = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
        var check = new CheckBox { Content = "Grammar checking", IsChecked = true, Margin = new Thickness(0, 0, 16, 0) };
        AutomationProperties.SetName(check, "Grammar checking");
        checks.Children.Add(check);
        var radioA = new RadioButton { Content = "Below", IsChecked = true, GroupName = "place", Margin = new Thickness(0, 0, 16, 0) };
        AutomationProperties.SetName(radioA, "Below");
        var radioB = new RadioButton { Content = "Above", GroupName = "place" };
        AutomationProperties.SetName(radioB, "Above");
        checks.Children.Add(radioA);
        checks.Children.Add(radioB);
        stack.Children.Add(checks);

        stack.Children.Add(new SectionHeader { Title = "Lists and chrome" });
        var nav = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
        var general = new NavItem { Content = "General", IsChecked = true, GroupName = "nav", Margin = new Thickness(0, 0, 8, 0) };
        AutomationProperties.SetName(general, "General");
        nav.Children.Add(general);
        var ai = new NavItem { Content = "AI", GroupName = "nav" };
        AutomationProperties.SetName(ai, "AI");
        nav.Children.Add(ai);
        stack.Children.Add(nav);

        var list = new ListBox { Height = 96, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var app in new[] { "putty", "mstsc", "notepad", "chrome", "slack", "outlook", "teams", "figma" })
        {
            list.Items.Add(app);
        }

        AutomationProperties.SetName(list, "Blocked apps");
        stack.Children.Add(list);

        var chrome = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var tipButton = new Button { Content = "Hover for tip", Margin = new Thickness(0, 0, 8, 0) };
        tipButton.SetResourceReference(StyleProperty, "SecondaryButton");
        tipButton.ToolTip = "SurfaceRaised tooltip with wrapping text that stays readable in Dark and High Contrast.";
        AutomationProperties.SetName(tipButton, "Hover for tip");
        chrome.Children.Add(tipButton);

        var menuButton = new Button { Content = "Open menu" };
        menuButton.SetResourceReference(StyleProperty, "SecondaryButton");
        AutomationProperties.SetName(menuButton, "Open menu");
        var menu = new ContextMenu();
        var copy = new MenuItem { Header = "Copy", InputGestureText = "Ctrl+C" };
        AutomationProperties.SetName(copy, "Copy");
        menu.Items.Add(copy);
        menu.Items.Add(new Separator());
        var paste = new MenuItem { Header = "Paste", InputGestureText = "Ctrl+V" };
        AutomationProperties.SetName(paste, "Paste");
        menu.Items.Add(paste);
        var delete = new MenuItem { Header = "Delete", IsEnabled = false };
        AutomationProperties.SetName(delete, "Delete");
        menu.Items.Add(delete);
        menuButton.ContextMenu = menu;
        menuButton.Click += (_, _) =>
        {
            menu.PlacementTarget = menuButton;
            menu.IsOpen = true;
        };
        chrome.Children.Add(menuButton);
        stack.Children.Add(chrome);

        stack.Children.Add(new Card
        {
            Header = new SectionHeader { Title = "Card" },
            Content = new TextBlock { Text = "1 px border, no shadow. Right-click the sample text for a menu." }
        });
        var tip = new InfoTip { Tip = "Replaces the WinForms info mark.", Margin = new Thickness(0, 8, 0, 8) };
        AutomationProperties.SetName(tip, "Replaces the WinForms info mark.");
        stack.Children.Add(tip);
        stack.Children.Add(new StatusPill { Content = new TextBlock { Text = "Connected" }, Margin = new Thickness(0, 0, 0, 8) });
        stack.Children.Add(new InlineBanner { Content = new TextBlock { Text = "Clipboard listener is attached to this window." }, Margin = new Thickness(0, 0, 0, 16) });

        scroll.Content = stack;
        return scroll;
    }

    private Button ThemeButton(string name)
    {
        var button = new Button { Content = name, Margin = new Thickness(0, 0, 8, 0) };
        AutomationProperties.SetName(button, name);
        if (name != "Light")
        {
            button.SetResourceReference(StyleProperty, "SecondaryButton");
        }

        button.Click += (_, _) =>
        {
            if (_appearanceVm != null)
            {
                _appearanceVm.SelectThemeByName(name);
                return;
            }

            _themes?.SetTheme(name);
        };
        return button;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        => _host?.OnVisible(IsVisible);

    private void ShowControlsContent()
    {
        if (_contentHost != null)
        {
            _contentHost.Content = _controlsContent;
        }
    }

    private void ShowGeneralContent()
    {
        if (_contentHost != null && _host != null)
        {
            _host.ShowGeneral(_contentHost);
        }
    }

    private void ShowAiContent()
    {
        if (_contentHost != null && _host != null)
        {
            _host.ShowAi(_contentHost);
        }
    }

    private void ShowAppearanceContent()
    {
        if (_contentHost != null && _host != null)
        {
            _host.ShowAppearance(_contentHost);
        }
    }

    private void ShowPrivacyContent()
    {
        if (_contentHost != null && _host != null)
        {
            _host.ShowPrivacy(_contentHost);
        }
    }

    private void ShowAppToneContent()
    {
        if (_contentHost != null && _host != null)
        {
            _host.ShowAppTone(_contentHost);
        }
    }

    private void ShowAppsContent()
    {
        if (_contentHost != null && _host != null)
        {
            _host.ShowApps(_contentHost);
        }
    }

    private void ShowWritingContent()
    {
        if (_contentHost != null && _host != null)
        {
            _host.ShowWriting(_contentHost);
        }
    }

    private void OnOpenFullScreenChanged(bool open)
    {
        if (!IsVisible)
        {
            return;
        }

        WindowState = open ? WindowState.Maximized : WindowState.Normal;
    }
}
