using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Lexon.SettingsUi;

public static class ComboBoxWheelBehavior
{
    public static readonly DependencyProperty RedirectClosedWheelProperty =
        DependencyProperty.RegisterAttached(
            "RedirectClosedWheel",
            typeof(bool),
            typeof(ComboBoxWheelBehavior),
            new PropertyMetadata(false, OnChanged));

    public static void SetRedirectClosedWheel(DependencyObject element, bool value)
        => element.SetValue(RedirectClosedWheelProperty, value);

    public static bool GetRedirectClosedWheel(DependencyObject element)
        => (bool)element.GetValue(RedirectClosedWheelProperty);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox combo)
        {
            return;
        }

        combo.PreviewMouseWheel -= OnPreviewMouseWheel;
        if ((bool)e.NewValue)
        {
            combo.PreviewMouseWheel += OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ComboBox combo || combo.IsDropDownOpen)
        {
            return;
        }

        e.Handled = true;
        var parent = FindScrollableAncestor(combo);
        parent?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = parent
        });
    }

    private static ScrollViewer? FindScrollableAncestor(DependencyObject child)
    {
        ScrollViewer? first = null;
        var current = VisualTreeHelper.GetParent(child);
        while (current != null)
        {
            if (current is ScrollViewer viewer)
            {
                first ??= viewer;
                if (viewer.ScrollableHeight > 0)
                {
                    return viewer;
                }
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return first;
    }
}
