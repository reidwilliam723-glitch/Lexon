using System.Windows.Controls;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public partial class AppearancePage : UserControl
{
    public AppearancePage()
    {
        InitializeComponent();
    }

    public AppearancePage(AppearanceSettingsViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }
}
