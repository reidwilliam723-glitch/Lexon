using System.Windows;
using System.Windows.Controls;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public partial class GeneralPage : UserControl
{
    public GeneralPage()
    {
        InitializeComponent();
    }

    public GeneralPage(GeneralSettingsViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    private void OnExportSettings(object sender, RoutedEventArgs e)
        => (DataContext as GeneralSettingsViewModel)?.ExportSettings();

    private void OnImportSettings(object sender, RoutedEventArgs e)
        => (DataContext as GeneralSettingsViewModel)?.ImportSettings();
}
