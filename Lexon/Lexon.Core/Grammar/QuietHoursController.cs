namespace Lexon.Core.Grammar;

public enum QuietHoursAction
{
    None,
    Pause,
    Resume
}

/// <summary>
/// Quiet-hours pause state, separate from the tray timer so it can be tested
/// without the WinForms host.
/// </summary>
internal sealed class QuietHoursController
{
    public bool Enabled { get; private set; } = true;

    public bool PausedByQuietHours { get; private set; }

    public DateTime? OverrideUntil { get; private set; }

    public void Adopt(bool enabled) => Enabled = enabled;

    public bool SuppressesQuiet(DateTime now)
        => OverrideUntil is DateTime until && now < until;

    public QuietHoursAction Tick(DateTime now, bool quiet, DateTime windowEnd)
    {
        if (OverrideUntil is DateTime until)
        {
            if (now < until)
            {
                quiet = false;
            }
            else
            {
                OverrideUntil = null;
            }
        }

        if (quiet && Enabled)
        {
            Enabled = false;
            PausedByQuietHours = true;
            return QuietHoursAction.Pause;
        }

        if (!quiet && PausedByQuietHours)
        {
            PausedByQuietHours = false;
            Enabled = true;
            return QuietHoursAction.Resume;
        }

        return QuietHoursAction.None;
    }

    public void UserEnabled(DateTime now, bool quiet, DateTime windowEnd)
    {
        Enabled = true;
        PausedByQuietHours = false;
        if (quiet)
        {
            OverrideUntil = windowEnd;
        }

        _ = now;
    }

    public void ManualPause()
    {
        Enabled = false;
        PausedByQuietHours = false;
    }

    public void Resume(DateTime now, bool quiet, DateTime windowEnd)
    {
        if (quiet)
        {
            OverrideUntil = windowEnd;
        }

        PausedByQuietHours = false;
        Enabled = true;
        _ = now;
    }
}
