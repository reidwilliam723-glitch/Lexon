using System.Diagnostics;
using Lexon.SettingsModel;

namespace Lexon.Settings;

internal sealed class ProcessUrlLauncher : IUrlLauncher
{
    public bool TryOpen(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
