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
}
