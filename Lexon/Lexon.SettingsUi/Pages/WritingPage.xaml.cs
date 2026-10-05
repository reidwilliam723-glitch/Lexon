using System.Windows;
using System.Windows.Controls;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public partial class WritingPage : UserControl
{
    public WritingPage()
    {
        InitializeComponent();
    }

    public WritingPage(WritingViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    private void OnWritingStats(object sender, RoutedEventArgs e)
        => (DataContext as WritingViewModel)?.ShowWritingStats();

    private void OnLearnedWords(object sender, RoutedEventArgs e)
        => (DataContext as WritingViewModel)?.ShowLearnedWords();

    private void OnExport(object sender, RoutedEventArgs e)
        => (DataContext as WritingViewModel)?.ExportLearning();

    private void OnImport(object sender, RoutedEventArgs e)
        => (DataContext as WritingViewModel)?.ImportLearning();

    private void OnResetStyle(object sender, RoutedEventArgs e)
        => (DataContext as WritingViewModel)?.ResetWritingStyle();

    private void OnUndoAdaptation(object sender, RoutedEventArgs e)
        => (DataContext as WritingViewModel)?.UndoSelectedAdaptation();
}
