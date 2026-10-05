using System.Windows;
using Microsoft.Win32;

namespace Lexon.SettingsUi;

public sealed class WpfFileDialogService : SettingsModel.IFileDialogService
{
    public Window? Owner { get; set; }

    public string? PickSavePath(string filter, string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = suggestedName
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickOpenPath(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }
}

public sealed class WpfMessageService : SettingsModel.IMessageService
{
    public Window? Owner { get; set; }

    public void Info(string text, string caption)
    {
        if (Owner != null)
        {
            MessageBox.Show(Owner, text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public bool Confirm(string text, string caption)
    {
        var result = Owner != null
            ? MessageBox.Show(Owner, text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }
}
