using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public partial class AiPage : UserControl
{
    public AiPage()
    {
        InitializeComponent();
    }

    public AiPage(AiSettingsViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    private void OnMoreProviders(object sender, RoutedEventArgs e)
        => (DataContext as AiSettingsViewModel)?.ShowMoreProviders();

    private void OnGetApiKey(object sender, RoutedEventArgs e)
        => (DataContext as AiSettingsViewModel)?.GetApiKey();

    private void OnCancelWait(object sender, RoutedEventArgs e)
        => (DataContext as AiSettingsViewModel)?.CancelWait();

    private void OnKeyFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Only probe when focus leaves the whole key control (not Show/Hide).
        if (e.NewValue is false)
        {
            (DataContext as AiSettingsViewModel)?.OnKeyLostFocus();
        }
    }
}

public sealed class AiStatusBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var kind = value is AiStatusKind k ? k : AiStatusKind.Secondary;
        var key = kind switch
        {
            AiStatusKind.Info => "InfoBrush",
            AiStatusKind.Warning => "WarningBrush",
            AiStatusKind.Success => "SuccessBrush",
            AiStatusKind.Error => "ErrorBrush",
            _ => "TextSecondaryBrush"
        };

        if (Application.Current?.TryFindResource(key) is Brush brush)
        {
            return brush;
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
