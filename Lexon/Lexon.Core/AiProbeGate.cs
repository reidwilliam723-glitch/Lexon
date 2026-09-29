namespace Lexon.Core;

/// <summary>
/// Generation counter so a late AI probe cannot apply after Local-only
/// (or a newer probe) has already taken over.
/// </summary>
public sealed class AiProbeGate : IDisposable
{
    private int _generation;
    private CancellationTokenSource _cts = new();
    private readonly object _ctsLock = new();

    public readonly record struct Ticket(int Generation);

    public CancellationToken Token
    {
        get
        {
            lock (_ctsLock)
            {
                return _cts.Token;
            }
        }
    }

    /// <summary>
    /// Starts a new probe generation. Older tickets become stale.
    /// </summary>
    public Ticket Begin()
    {
        var generation = Interlocked.Increment(ref _generation);
        return new Ticket(generation);
    }

    /// <summary>
    /// Makes every outstanding ticket stale and cancels in-flight work.
    /// </summary>
    public void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        CancelAndReplace();
    }

    public bool IsCurrent(Ticket ticket)
        => Volatile.Read(ref _generation) == ticket.Generation;

    public void Dispose()
    {
        lock (_ctsLock)
        {
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _cts.Dispose();
        }
    }

    private void CancelAndReplace()
    {
        CancellationTokenSource previous;
        lock (_ctsLock)
        {
            previous = _cts;
            _cts = new CancellationTokenSource();
        }

        try
        {
            previous.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        previous.Dispose();
    }
}
