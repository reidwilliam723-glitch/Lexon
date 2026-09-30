using Lexon.Profiles;

namespace Lexon.SettingsModel;

/// <summary>
/// Typed view of the profile keys written by Settings. Key names, defaults and
/// value formats match 1.0.8 <c>SettingsForm.ApplySettings</c>.
/// </summary>
public sealed class AppSettings
{
    public const string MinimizeToTrayKey = "MinimizeToTray";
    public const string EnableAutoUpdatesKey = "EnableAutoUpdates";
    public const string OpenSettingsFullScreenKey = "OpenSettingsFullScreen";
    public const string QuickPauseMinutesKey = "QuickPauseMinutes";
    public const string AIProviderKey = "AIProvider";
    public const string APIKeyKey = "APIKey";
    public const string AIKeyValidatedKey = "AIKeyValidated";
    public const string AIModelKey = "AIModel";
    public const string LocalModeKey = "LocalMode";
    public const string AiSuggestionsWhileTypingKey = "AiSuggestionsWhileTyping";
    public const string AiRewriteOnRequestKey = "AiRewriteOnRequest";
    public const string AiPrefetchOnSelectionKey = "AiPrefetchOnSelection";
    public const string BlockedApplicationsKey = "BlockedApplications";
    public const string ThemeKey = "Theme";
    public const string SuggestionSortModeKey = "SuggestionSortMode";
    public const string SuggestionPlacementKey = "SuggestionPlacement";
    public const string RequireConfirmationForEditsKey = "RequireConfirmationForEdits";
    public const string GrammarSensitivityKey = "GrammarSensitivity";
    public const string MuteGrammarForCasualAppsKey = "MuteGrammarForCasualApps";
    public const string EnableRewriteHotkeyKey = "EnableRewriteHotkey";
    public const string GrammarCheckingKey = "GrammarChecking";
    public const string AutoCorrectTyposKey = "AutoCorrectTypos";
    public const string EnableGrammarHotkeyKey = "EnableGrammarHotkey";
    public const string GrammarMutedAppsKey = "GrammarMutedApps";
    public const string AppCategoryOverridesKey = "AppCategoryOverrides";

    public bool MinimizeToTray { get; set; } = true;
    public bool EnableAutoUpdates { get; set; } = true;
    public bool OpenSettingsFullScreen { get; set; }
    public int QuickPauseMinutes { get; set; } = 15;
    public string AIProvider { get; set; } = "None";
    public string APIKey { get; set; } = string.Empty;
    public bool AIKeyValidated { get; set; }
    public string AIModel { get; set; } = string.Empty;
    public bool LocalMode { get; set; }
    public bool AiSuggestionsWhileTyping { get; set; }
    public bool AiRewriteOnRequest { get; set; } = true;
    public bool AiPrefetchOnSelection { get; set; }
    public List<string> BlockedApplications { get; set; } = [];
    public string Theme { get; set; } = "Light";
    public string SuggestionSortMode { get; set; } = "Relevant";
    public string SuggestionPlacement { get; set; } = "Below";
    public bool RequireConfirmationForEdits { get; set; } = true;
    public string GrammarSensitivity { get; set; } = "Medium";
    public bool MuteGrammarForCasualApps { get; set; }
    public bool EnableRewriteHotkey { get; set; } = true;
    public bool GrammarChecking { get; set; } = true;
    public bool AutoCorrectTypos { get; set; } = true;
    public bool EnableGrammarHotkey { get; set; } = true;
    public List<string> GrammarMutedApps { get; set; } = [];
    public List<string> AppCategoryOverrides { get; set; } = [];

    public void Read(Profile profile)
    {
        MinimizeToTray = profile.GetSetting(MinimizeToTrayKey, true);
        EnableAutoUpdates = profile.GetSetting(EnableAutoUpdatesKey, true);
        OpenSettingsFullScreen = profile.GetSetting(OpenSettingsFullScreenKey, false);
        QuickPauseMinutes = profile.GetSetting(QuickPauseMinutesKey, 15);
        var provider = profile.GetSetting(AIProviderKey, "None");
        AIProvider = string.IsNullOrWhiteSpace(provider) ? "None" : provider;
        APIKey = profile.GetSetting(APIKeyKey, string.Empty);
        AIKeyValidated = profile.GetSetting(
            AIKeyValidatedKey,
            !string.IsNullOrEmpty(APIKey) || AIProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase));
        AIModel = profile.GetSetting(AIModelKey, string.Empty);
        LocalMode = profile.GetSetting(LocalModeKey, false);
        AiSuggestionsWhileTyping = profile.GetSetting(AiSuggestionsWhileTypingKey, false);
        AiRewriteOnRequest = profile.GetSetting(AiRewriteOnRequestKey, true);
        AiPrefetchOnSelection = profile.GetSetting(AiPrefetchOnSelectionKey, false);
        BlockedApplications = profile.GetSetting<List<string>>(BlockedApplicationsKey, []) ?? [];
        Theme = profile.GetSetting(ThemeKey, "Light");
        SuggestionSortMode = profile.GetSetting(SuggestionSortModeKey, "Relevant");
        SuggestionPlacement = profile.GetSetting(SuggestionPlacementKey, "Below");
        RequireConfirmationForEdits = profile.GetSetting(RequireConfirmationForEditsKey, true);
        GrammarSensitivity = profile.GetSetting(GrammarSensitivityKey, "Medium");
        MuteGrammarForCasualApps = profile.GetSetting(MuteGrammarForCasualAppsKey, false);
        EnableRewriteHotkey = profile.GetSetting(EnableRewriteHotkeyKey, true);
        GrammarChecking = profile.GetSetting(GrammarCheckingKey, true);
        AutoCorrectTypos = profile.GetSetting(AutoCorrectTyposKey, true);
        EnableGrammarHotkey = profile.GetSetting(EnableGrammarHotkeyKey, true);
        GrammarMutedApps = profile.GetSetting<List<string>>(GrammarMutedAppsKey, []) ?? [];
        AppCategoryOverrides = profile.GetSetting<List<string>>(AppCategoryOverridesKey, []) ?? [];
    }

    public void Write(Profile profile)
    {
        profile.SetSetting(MinimizeToTrayKey, MinimizeToTray);
        profile.SetSetting(EnableAutoUpdatesKey, EnableAutoUpdates);
        profile.SetSetting(OpenSettingsFullScreenKey, OpenSettingsFullScreen);
        profile.SetSetting(QuickPauseMinutesKey, QuickPauseMinutes);
        profile.SetSetting(AIProviderKey, AIProvider);
        profile.SetSetting(APIKeyKey, APIKey);
        profile.SetSetting(AIKeyValidatedKey, AIKeyValidated);
        profile.SetSetting(AIModelKey, AIModel);
        profile.SetSetting(LocalModeKey, LocalMode);
        profile.SetSetting(AiSuggestionsWhileTypingKey, AiSuggestionsWhileTyping);
        profile.SetSetting(AiRewriteOnRequestKey, AiRewriteOnRequest);
        profile.SetSetting(AiPrefetchOnSelectionKey, AiPrefetchOnSelection);
        profile.SetSetting(BlockedApplicationsKey, BlockedApplications);
        profile.SetSetting(ThemeKey, Theme);
        profile.SetSetting(SuggestionSortModeKey, SuggestionSortMode);
        profile.SetSetting(SuggestionPlacementKey, SuggestionPlacement);
        profile.SetSetting(RequireConfirmationForEditsKey, RequireConfirmationForEdits);
        profile.SetSetting(GrammarSensitivityKey, GrammarSensitivity);
        profile.SetSetting(MuteGrammarForCasualAppsKey, MuteGrammarForCasualApps);
        profile.SetSetting(EnableRewriteHotkeyKey, EnableRewriteHotkey);
        profile.SetSetting(GrammarCheckingKey, GrammarChecking);
        profile.SetSetting(AutoCorrectTyposKey, AutoCorrectTypos);
        profile.SetSetting(EnableGrammarHotkeyKey, EnableGrammarHotkey);
        profile.SetSetting(GrammarMutedAppsKey, GrammarMutedApps);
        profile.SetSetting(AppCategoryOverridesKey, AppCategoryOverrides);
    }
}
