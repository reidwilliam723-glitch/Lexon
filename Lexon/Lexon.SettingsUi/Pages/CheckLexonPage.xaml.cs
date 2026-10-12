using System.Windows;
using System.Windows.Controls;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public partial class CheckLexonPage : UserControl
{
    public CheckLexonPage(CheckLexonViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
        => (DataContext as CheckLexonViewModel)?.RefreshStatus();

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        var report = (DataContext as CheckLexonViewModel)?.ReportText;
        if (string.IsNullOrWhiteSpace(report))
        {
            return;
        }

        Clipboard.SetText(report);
    }

    private void OnClear(object sender, RoutedEventArgs e)
        => (DataContext as CheckLexonViewModel)?.ClearHistory();

    private void OnUndo(object sender, RoutedEventArgs e)
        => (DataContext as CheckLexonViewModel)?.UndoLatest();
}
