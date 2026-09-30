namespace Lexon.SettingsModel;

/// <summary>
/// Debounced persist matching SettingsForm's 400 ms timer: each schedule
/// restarts the delay; Flush always runs the save action unless loading.
/// </summary>
public sealed class PersistScheduler
{
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(400);

    private readonly Action _flush;
    private readonly TimeSpan _delay;
    private DateTime _dueUtc;
    private bool _pending;

    public bool IsLoading { get; set; }
    public bool HasPending => _pending;

    public PersistScheduler(Action flush, TimeSpan? delay = null)
    {
        _flush = flush ?? throw new ArgumentNullException(nameof(flush));
        _delay = delay ?? DefaultDelay;
    }

    public void Schedule(DateTime utcNow)
    {
        if (IsLoading)
        {
            return;
        }

        _pending = true;
        _dueUtc = utcNow + _delay;
    }

    public bool TryFlushDue(DateTime utcNow)
    {
        if (!_pending || utcNow < _dueUtc)
        {
            return false;
        }

        Flush();
        return true;
    }

    public void Flush()
    {
        _pending = false;
        if (IsLoading)
        {
            return;
        }

        _flush();
    }
}
