namespace Lexon.Settings;

/// <summary>
/// Decides whether the first-run Settings tutorial should open.
/// </summary>
internal static class TutorialLaunch
{
    public static readonly TimeSpan RecentOnboardingWindow = TimeSpan.FromMinutes(10);

    public static bool ShouldShow(
        bool tutorialKeyPresent,
        bool tutorialCompleted,
        bool isFirstRun,
        DateTime? onboardingCompletedUtc,
        DateTime utcNow)
    {
        if (tutorialKeyPresent)
        {
            return !tutorialCompleted;
        }

        if (isFirstRun)
        {
            return true;
        }

        if (onboardingCompletedUtc is DateTime completed)
        {
            var age = utcNow - completed;
            if (age >= TimeSpan.Zero && age <= RecentOnboardingWindow)
            {
                return true;
            }
        }

        return false;
    }
}
