using System.Windows;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public sealed class GalleryClipboardWatch : IClipboardWatch
{
    private readonly ClipboardHwndListener _listener;
    private bool _watching;

    public GalleryClipboardWatch(ClipboardHwndListener listener)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _listener.ClipboardUpdated += (_, _) =>
        {
            if (_watching)
            {
                Updated?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public event EventHandler? Updated;

    public bool Start()
    {
        _watching = _listener.IsListening;
        return _watching;
    }

    public void Stop() => _watching = false;

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
