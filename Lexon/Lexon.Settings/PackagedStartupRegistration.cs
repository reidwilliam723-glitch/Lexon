using Lexon.SettingsModel;
using Windows.ApplicationModel;

namespace Lexon.Settings;

/// <summary>
/// MSIX StartupTask registration for "Start with Windows".
/// </summary>
internal sealed class PackagedStartupRegistration : IStartupRegistration
{
    public const string TaskId = "LexonStartup";

    public const string DisabledByUserMessage =
        "Lexon startup was turned off in Windows. Turn it on in Settings → Apps → Startup.";

    public const string DisabledByPolicyMessage =
        "Windows policy blocks Lexon from starting with Windows.";

    private StartupAvailability _availability = StartupAvailability.Available;
    private string? _statusHint;

    public StartupAvailability Availability => _availability;

    public string? StatusHint => _statusHint;

    public bool IsEnabled()
    {
        try
        {
            var task = GetTask();
            ApplyState(task.State);
            return task.State == StartupTaskState.Enabled
                || task.State == StartupTaskState.EnabledByPolicy;
        }
        catch
        {
            _availability = StartupAvailability.Unavailable;
            _statusHint = "Couldn't read the Windows startup setting.";
            return false;
        }
    }

    public bool TrySetEnabled(bool enabled)
    {
        try
        {
            var task = GetTask();
            if (enabled)
            {
                if (task.State == StartupTaskState.Enabled
                    || task.State == StartupTaskState.EnabledByPolicy)
                {
                    ApplyState(task.State);
                    return true;
                }

                if (task.State == StartupTaskState.DisabledByUser)
                {
                    ApplyState(task.State);
                    return false;
                }

                if (task.State == StartupTaskState.DisabledByPolicy)
                {
                    ApplyState(task.State);
                    return false;
                }

                var result = task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
                ApplyState(result);
                return result == StartupTaskState.Enabled
                    || result == StartupTaskState.EnabledByPolicy;
            }

            if (task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
            {
                task.Disable();
            }

            ApplyState(GetTask().State);
            return !IsEnabled();
        }
        catch
        {
            _availability = StartupAvailability.Unavailable;
            _statusHint = "Couldn't change the Windows startup setting.";
            return false;
        }
    }

    private static StartupTask GetTask()
        => StartupTask.GetAsync(TaskId).AsTask().GetAwaiter().GetResult();

    private void ApplyState(StartupTaskState state)
    {
        switch (state)
        {
            case StartupTaskState.Enabled:
            case StartupTaskState.EnabledByPolicy:
                _availability = StartupAvailability.Available;
                _statusHint = null;
                break;
            case StartupTaskState.Disabled:
                _availability = StartupAvailability.Available;
                _statusHint = null;
                break;
            case StartupTaskState.DisabledByUser:
                _availability = StartupAvailability.DisabledByUser;
                _statusHint = DisabledByUserMessage;
                break;
            case StartupTaskState.DisabledByPolicy:
                _availability = StartupAvailability.DisabledByPolicy;
                _statusHint = DisabledByPolicyMessage;
                break;
            default:
                _availability = StartupAvailability.Unavailable;
                _statusHint = "Couldn't change the Windows startup setting.";
                break;
        }
    }
}
