using System.Windows;
using System.Windows.Controls;

namespace Lexon.SettingsUi;

public class NavItem : RadioButton
{
    static NavItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NavItem), new FrameworkPropertyMetadata(typeof(NavItem)));
    }
}

public class Card : HeaderedContentControl
{
    static Card()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Card), new FrameworkPropertyMetadata(typeof(Card)));
    }
}

public class SectionHeader : Control
{
    static SectionHeader()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SectionHeader), new FrameworkPropertyMetadata(typeof(SectionHeader)));
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SectionHeader), new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}

public class InfoTip : Button
{
    static InfoTip()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(InfoTip), new FrameworkPropertyMetadata(typeof(InfoTip)));
    }

    public static readonly DependencyProperty TipProperty =
        DependencyProperty.Register(nameof(Tip), typeof(string), typeof(InfoTip), new PropertyMetadata(string.Empty));

    public string Tip
    {
        get => (string)GetValue(TipProperty);
        set => SetValue(TipProperty, value);
    }
}

public class StatusPill : ContentControl
{
    static StatusPill()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(StatusPill), new FrameworkPropertyMetadata(typeof(StatusPill)));
    }
}

public class InlineBanner : ContentControl
{
    static InlineBanner()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(InlineBanner), new FrameworkPropertyMetadata(typeof(InlineBanner)));
    }
}
