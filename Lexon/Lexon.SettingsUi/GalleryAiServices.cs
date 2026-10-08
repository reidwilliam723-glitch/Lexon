using System.Windows;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public sealed class GalleryClipboardWatch : IClipboardWatch
{
    private readonly ClipboardHwndListener _listener;
    private Window _window;

    public GalleryClipboardWatch(ClipboardHwndListener listener, Window window)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _listener.ClipboardUpdated += (_, _) => Updated?.Invoke(this, EventArgs.Empty);
    }

    public void Retarget(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var wasListening = _listener.IsListening;
        if (wasListening)
        {
            _listener.Detach();
        }

        _window = window;
        if (wasListening)
        {
            _listener.Attach(_window);
        }
    }

    public event EventHandler? Updated;

    public bool Start()
    {
        _listener.Attach(_window);
        return _listener.IsListening;
    }

    public void Stop() => _listener.Detach();

    public string? ReadText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class DispatcherDelayScheduler : IDelayScheduler
{
    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = delay
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
        return new TimerHandle(timer);
    }

    private sealed class TimerHandle : IDisposable
    {
        private System.Windows.Threading.DispatcherTimer? _timer;

        public TimerHandle(System.Windows.Threading.DispatcherTimer timer) => _timer = timer;

        public void Dispose()
        {
            _timer?.Stop();
            _timer = null;
        }
    }
}
