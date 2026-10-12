using System.Windows;
using System.Windows.Controls;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public partial class WritingStatsPage : UserControl
{
    public WritingStatsPage(WritingStatsPageModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    private void OnBack(object sender, RoutedEventArgs e)
        => (DataContext as WritingStatsPageModel)?.Back();

    private void OnRefresh(object sender, RoutedEventArgs e)
        => (DataContext as WritingStatsPageModel)?.Refresh();
}
