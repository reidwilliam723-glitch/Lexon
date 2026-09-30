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

    public ControlGalleryWindow(ThemeManager? themes = null)
    {
        _themes = themes;
        Title = "Lexon control gallery";
        Width = 720;
        Height = 640;
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
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack = new StackPanel { Margin = new Thickness(16), MaxWidth = 560 };
        stack.Children.Add(new SectionHeader { Title = "Buttons" });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 16) };
        buttons.Children.Add(new Button { Content = "Primary", Margin = new Thickness(0, 0, 8, 0) });
        var secondary = new Button { Content = "Secondary", Margin = new Thickness(0, 0, 8, 0) };
        secondary.SetResourceReference(StyleProperty, "SecondaryButton");
        buttons.Children.Add(secondary);
        var subtle = new Button { Content = "Subtle" };
        subtle.SetResourceReference(StyleProperty, "SubtleButton");
        buttons.Children.Add(subtle);
        stack.Children.Add(buttons);

        stack.Children.Add(new SectionHeader { Title = "Toggle, combo, fields" });
        var toggle = new ToggleSwitch { Margin = new Thickness(0, 8, 0, 8) };
        AutomationProperties.SetName(toggle, "Demo toggle");
        stack.Children.Add(toggle);
        _demoCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 8), MinWidth = 240 };
        _demoCombo.Items.Add("Most Relevant");
        _demoCombo.Items.Add("Most Used");
        _demoCombo.SelectedIndex = 0;
        AutomationProperties.SetName(_demoCombo, "Suggestion sort");
        stack.Children.Add(_demoCombo);
        stack.Children.Add(new TextBox { Text = "Sample text", Margin = new Thickness(0, 0, 0, 8) });
        stack.Children.Add(new RevealPasswordBox { Password = "sk-demo", Margin = new Thickness(0, 0, 0, 16) });

        stack.Children.Add(new SectionHeader { Title = "Lists and chrome" });
        var nav = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
        var general = new NavItem { Content = "General", IsChecked = true, GroupName = "nav", Margin = new Thickness(0, 0, 8, 0) };
        AutomationProperties.SetName(general, "General");
        nav.Children.Add(general);
        var ai = new NavItem { Content = "AI", GroupName = "nav" };
        AutomationProperties.SetName(ai, "AI");
        nav.Children.Add(ai);
        stack.Children.Add(nav);
        var list = new ListBox { Height = 80, Margin = new Thickness(0, 0, 0, 8) };
        list.Items.Add("putty");
        list.Items.Add("mstsc");
        AutomationProperties.SetName(list, "Blocked apps");
        stack.Children.Add(list);
        stack.Children.Add(new Card
        {
            Header = new SectionHeader { Title = "Card" },
            Content = new TextBlock { Text = "1 px border, no shadow." }
        });
        stack.Children.Add(new InfoTip { Tip = "Replaces the WinForms info mark.", Margin = new Thickness(0, 8, 0, 8) });
        stack.Children.Add(new StatusPill { Content = new TextBlock { Text = "Connected" }, Margin = new Thickness(0, 0, 0, 8) });
        stack.Children.Add(new InlineBanner { Content = new TextBlock { Text = "Clipboard listener is attached to this window." }, Margin = new Thickness(0, 0, 0, 16) });
        scroll.Content = stack;
        root.Children.Add(scroll);
        return root;
    }

    private Button ThemeButton(string name)
    {
        var button = new Button { Content = name, Margin = new Thickness(0, 0, 8, 0) };
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
