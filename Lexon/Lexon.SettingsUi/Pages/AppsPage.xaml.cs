using System.Windows;
using System.Windows.Controls;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public partial class AppsPage : UserControl
{
    public AppsPage()
    {
        InitializeComponent();
    }

    public AppsPage(AppsSettingsViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    private void OnAddRunningApp(object sender, RoutedEventArgs e)
        => (DataContext as AppsSettingsViewModel)?.AddRunningApp();
}
