using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Lexon.Core.Theming;

namespace Lexon.SettingsUi;

public sealed class ControlGalleryWindow : Window
{
    private readonly ThemeManager? _themes;
    private readonly ClipboardHwndListener _clipboard = new();
    private ComboBox _demoCombo = null!;
    private UIElement _demoHost = null!;

    public ControlGalleryWindow(ThemeManager? themes = null)
    {
        _themes = themes;
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
        Loaded += OnLoaded;
        Closed += (_, _) => _clipboard.Dispose();
        Content = Build();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
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
        root.Children.Add(scroll);
        return root;
    }

    private Button ThemeButton(string name)
    {
        var button = new Button { Content = name, Margin = new Thickness(0, 0, 8, 0) };
        AutomationProperties.SetName(button, name);
        if (name != "Light")
        {
            button.SetResourceReference(StyleProperty, "SecondaryButton");
        }

        button.Click += (_, _) => _themes?.SetTheme(name);
        return button;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _clipboard.Attach(this);
    }
}
