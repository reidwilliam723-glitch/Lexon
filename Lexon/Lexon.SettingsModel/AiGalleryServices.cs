namespace Lexon.SettingsModel;

public enum AiStatusKind
{
    Secondary,
    Info,
    Warning,
    Success,
    Error
}

public interface IClipboardWatch
{
    bool Start();

    void Stop();

    event EventHandler? Updated;

    string? ReadText();
}

public interface IUrlLauncher
{
    bool TryOpen(string url);
}

public interface IDelayScheduler
{
    IDisposable Schedule(TimeSpan delay, Action action);
}
