using System.Windows;
using System.Windows.Controls;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public partial class AppTonePage : UserControl
{
    public AppTonePage()
    {
        InitializeComponent();
    }

    public AppTonePage(AppToneViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    private void OnAddRunningApp(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppToneViewModel vm)
        {
            vm.AddRunningApp();
        }
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppToneViewModel vm)
        {
            vm.RemoveSelected();
        }
    }
}
