using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Lexon.SettingsUi;

public class RevealPasswordBox : Control
{
    private PasswordBox? _passwordBox;
    private TextBox? _plain;
    private ToggleButton? _reveal;

    static RevealPasswordBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(RevealPasswordBox), new FrameworkPropertyMetadata(typeof(RevealPasswordBox)));
    }

    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.Register(nameof(Password), typeof(string), typeof(RevealPasswordBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordChanged));

    public static readonly DependencyProperty IsRevealedProperty =
        DependencyProperty.Register(nameof(IsRevealed), typeof(bool), typeof(RevealPasswordBox), new PropertyMetadata(false));

    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public bool IsRevealed
    {
        get => (bool)GetValue(IsRevealedProperty);
        set => SetValue(IsRevealedProperty, value);
    }

    public override void OnApplyTemplate()
    {
        if (_passwordBox != null)
        {
            _passwordBox.PasswordChanged -= OnBoxPasswordChanged;
        }

        if (_reveal != null)
        {
            _reveal.Checked -= OnRevealChecked;
            _reveal.Unchecked -= OnRevealUnchecked;
        }

        base.OnApplyTemplate();

        _passwordBox = GetTemplateChild("PART_Password") as PasswordBox;
        if (_passwordBox != null)
        {
            if (_passwordBox.Password != Password)
            {
                _passwordBox.Password = Password ?? string.Empty;
            }

            _passwordBox.PasswordChanged += OnBoxPasswordChanged;
        }

        _plain = GetTemplateChild("PART_Plain") as TextBox;
        _reveal = GetTemplateChild("PART_Reveal") as ToggleButton;
        if (_reveal != null)
        {
            _reveal.Checked += OnRevealChecked;
            _reveal.Unchecked += OnRevealUnchecked;
        }

        ApplyAccessibleName();
    }

    private void ApplyAccessibleName()
    {
        var name = AutomationProperties.GetName(this);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (_passwordBox != null)
        {
            AutomationProperties.SetName(_passwordBox, name);
        }

        if (_plain != null)
        {
            AutomationProperties.SetName(_plain, name);
        }
    }

    private void OnBoxPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_passwordBox != null && Password != _passwordBox.Password)
        {
            Password = _passwordBox.Password;
        }
    }

    private void OnRevealChecked(object sender, RoutedEventArgs e) => IsRevealed = true;

    private void OnRevealUnchecked(object sender, RoutedEventArgs e) => IsRevealed = false;

    private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RevealPasswordBox control
            && control._passwordBox is { } box
            && box.Password != (e.NewValue as string ?? string.Empty))
        {
            box.Password = e.NewValue as string ?? string.Empty;
        }
    }
}
