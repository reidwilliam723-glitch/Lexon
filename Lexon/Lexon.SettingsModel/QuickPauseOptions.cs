namespace Lexon.SettingsModel;

public static class QuickPauseOptions
{
    public static readonly int[] Minutes = [0, 1, 5, 15, 30, 60];

    public static int MinutesFromIndex(int index)
        => index >= 0 && index < Minutes.Length ? Minutes[index] : 15;

    public static int IndexFromMinutes(int minutes)
    {
        var index = Array.IndexOf(Minutes, minutes);
        return index >= 0 ? index : 3;
    }
}
