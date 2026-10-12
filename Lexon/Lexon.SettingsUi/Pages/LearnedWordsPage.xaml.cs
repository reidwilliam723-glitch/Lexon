using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public partial class LearnedWordsPage : UserControl
{
    public LearnedWordsPage(LearnedWordsPageModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    private LearnedWordsPageModel? Model => DataContext as LearnedWordsPageModel;

    private void OnBack(object sender, RoutedEventArgs e) => Model?.Back();

    private void OnAdd(object sender, RoutedEventArgs e) => Model?.Add();

    private void OnUndo(object sender, RoutedEventArgs e) => Model?.UndoLast();

    private void OnClear(object sender, RoutedEventArgs e) => Model?.ClearAll();

    private void OnDelete(object sender, RoutedEventArgs e)
        => Model?.Delete(SelectedWords());

    private void OnNever(object sender, RoutedEventArgs e)
    {
        if (WordList.SelectedItems.Count != 1)
        {
            return;
        }

        Model?.NeverLearn(WordList.SelectedItem as string);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DeleteButton.IsEnabled = WordList.SelectedItems.Count > 0;
        NeverButton.IsEnabled = WordList.SelectedItems.Count == 1;
    }

    private string[] SelectedWords()
        => WordList.SelectedItems.Cast<string>().ToArray();
}
