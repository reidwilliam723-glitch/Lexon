using System.Windows;
using System.Windows.Controls;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public partial class PrivacyPage : UserControl
{
    public PrivacyPage()
    {
        InitializeComponent();
    }

    public PrivacyPage(PrivacySettingsViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    private void OnAddRunningApp(object sender, RoutedEventArgs e)
        => (DataContext as PrivacySettingsViewModel)?.AddRunningApp();

    private void OnShowActivityLog(object sender, RoutedEventArgs e)
        => (DataContext as PrivacySettingsViewModel)?.ShowActivityLog();

    private void OnShowPrivacyPreview(object sender, RoutedEventArgs e)
        => (DataContext as PrivacySettingsViewModel)?.ShowPrivacyPreview();
}
