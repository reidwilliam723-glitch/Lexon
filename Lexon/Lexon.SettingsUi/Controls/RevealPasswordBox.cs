using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Lexon.SettingsUi;

public class RevealPasswordBox : Control
{
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
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Password") is PasswordBox box)
        {
            if (box.Password != Password)
            {
                box.Password = Password ?? string.Empty;
            }

            box.PasswordChanged += (_, _) =>
            {
                if (Password != box.Password)
                {
                    Password = box.Password;
                }
            };
        }

        if (GetTemplateChild("PART_Reveal") is ToggleButton toggle)
        {
            toggle.Checked += (_, _) => IsRevealed = true;
            toggle.Unchecked += (_, _) => IsRevealed = false;
        }
    }

    private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RevealPasswordBox control
            && control.Template?.FindName("PART_Password", control) is PasswordBox box
            && box.Password != (e.NewValue as string ?? string.Empty))
        {
            box.Password = e.NewValue as string ?? string.Empty;
        }
    }
}
