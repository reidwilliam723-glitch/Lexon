using Lexon.Core.Interfaces;
using Lexon.Core.Models;

namespace Lexon.Privacy;

/// <summary>
/// Privacy guard that blocks suggestions in secure fields and blocked applications
/// </summary>
public class PrivacyGuard : IPrivacyGuard
{
    private readonly HashSet<string> _blockedApplications = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _secureFieldIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "pass", "pwd", "secret", "token", "key", "credential", "login", "signin"
    };

    public PrivacyGuard()
    {
        AddDefaultBlockedApplications();
    }

    public bool IsSecureField(TextContext context)
    {
        if (context.IsPasswordField) return true;

        // Check window title for secure field indicators
        var lowerTitle = context.WindowTitle.ToLowerInvariant();
        foreach (var indicator in _secureFieldIndicators)
        {
            if (lowerTitle.Contains(indicator))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsApplicationBlocked(string applicationName)
    {
        return !string.IsNullOrEmpty(applicationName) && _blockedApplications.Contains(applicationName);
    }

    public bool ShouldBlockAssistance(TextContext context)
    {
        if (context == null)
        {
            return true;
        }

        return IsSecureField(context) || IsApplicationBlocked(context.ApplicationName);
    }

    public const string RewriteBlockedMessage =
        "Rewrite isn't available here — this app or field is excluded for privacy.";

    public const string GrammarBlockedMessage =
        "Grammar check isn't available here — this app or field is excluded for privacy.";

    public IReadOnlyCollection<string> GetBlockedApplications()
        => _blockedApplications.ToArray();

    public void AddBlockedApplication(string applicationName)
    {
        _blockedApplications.Add(applicationName);
    }

    public void RemoveBlockedApplication(string applicationName)
    {
        _blockedApplications.Remove(applicationName);
    }

    public void ReplaceBlockedApplications(IEnumerable<string> applicationNames)
    {
        _blockedApplications.Clear();
        AddDefaultBlockedApplications();

        foreach (var applicationName in applicationNames)
        {
            if (!string.IsNullOrWhiteSpace(applicationName))
            {
                _blockedApplications.Add(applicationName.Trim());
            }
        }
    }

    private void AddDefaultBlockedApplications()
    {
        _blockedApplications.Add("mstsc");
        _blockedApplications.Add("putty");
        _blockedApplications.Add("winscp");
    }
}
