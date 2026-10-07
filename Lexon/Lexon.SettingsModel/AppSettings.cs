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
    public const string AutoInsertSpacesKey = "AutoInsertSpaces";
    public const string DocumentConsistencyCheckingKey = "DocumentConsistencyChecking";
    public const string AllowCodeSwitchingKey = "AllowCodeSwitching";
    public const string EnableGrammarHotkeyKey = "EnableGrammarHotkey";
    public const string GrammarMutedAppsKey = "GrammarMutedApps";
    public const string AppCategoryOverridesKey = "AppCategoryOverrides";
    public const string CustomTerminologyKey = "CustomTerminology";
    public const string AppTerminologyOverridesKey = "AppTerminologyOverrides";
    public const string DefaultWritingModeKey = "DefaultWritingMode";

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
    public bool AutoInsertSpaces { get; set; } = true;
    public bool DocumentConsistencyChecking { get; set; } = true;
    public bool AllowCodeSwitching { get; set; } = true;
    public bool EnableGrammarHotkey { get; set; } = true;
    public List<string> GrammarMutedApps { get; set; } = [];
    public List<string> AppCategoryOverrides { get; set; } = [];
    public List<string> CustomTerminology { get; set; } = [];
    public List<string> AppTerminologyOverrides { get; set; } = [];
    public string DefaultWritingMode { get; set; } = "Plain language";

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
        AutoInsertSpaces = profile.GetSetting(AutoInsertSpacesKey, true);
        DocumentConsistencyChecking = profile.GetSetting(DocumentConsistencyCheckingKey, true);
        AllowCodeSwitching = profile.GetSetting(AllowCodeSwitchingKey, true);
        EnableGrammarHotkey = profile.GetSetting(EnableGrammarHotkeyKey, true);
        GrammarMutedApps = profile.GetSetting<List<string>>(GrammarMutedAppsKey, []) ?? [];
        AppCategoryOverrides = profile.GetSetting<List<string>>(AppCategoryOverridesKey, []) ?? [];
        CustomTerminology = profile.GetSetting<List<string>>(CustomTerminologyKey, []) ?? [];
        AppTerminologyOverrides = profile.GetSetting<List<string>>(AppTerminologyOverridesKey, []) ?? [];
        DefaultWritingMode = profile.GetSetting(DefaultWritingModeKey, "Plain language");
    }

    public void Write(Profile profile)
    {
        WriteKeys(
            profile,
            MinimizeToTrayKey,
            EnableAutoUpdatesKey,
            OpenSettingsFullScreenKey,
            QuickPauseMinutesKey,
            AIProviderKey,
            APIKeyKey,
            AIKeyValidatedKey,
            AIModelKey,
            LocalModeKey,
            AiSuggestionsWhileTypingKey,
            AiRewriteOnRequestKey,
            AiPrefetchOnSelectionKey,
            BlockedApplicationsKey,
            ThemeKey,
            SuggestionSortModeKey,
            SuggestionPlacementKey,
            RequireConfirmationForEditsKey,
            GrammarSensitivityKey,
            MuteGrammarForCasualAppsKey,
            EnableRewriteHotkeyKey,
            GrammarCheckingKey,
            AutoCorrectTyposKey,
            AutoInsertSpacesKey,
            DocumentConsistencyCheckingKey,
            AllowCodeSwitchingKey,
            EnableGrammarHotkeyKey,
            GrammarMutedAppsKey,
            AppCategoryOverridesKey,
            CustomTerminologyKey,
            AppTerminologyOverridesKey,
            DefaultWritingModeKey);
    }

    /// <summary>
    /// Writes only the named keys. Gallery pages use this so a stale in-memory
    /// copy cannot overwrite keys another surface (the classic form) just changed.
    /// </summary>
    public void WriteKeys(Profile profile, params string[] keys)
    {
        foreach (var key in keys)
        {
            switch (key)
            {
                case MinimizeToTrayKey:
                    profile.SetSetting(key, MinimizeToTray);
                    break;
                case EnableAutoUpdatesKey:
                    profile.SetSetting(key, EnableAutoUpdates);
                    break;
                case OpenSettingsFullScreenKey:
                    profile.SetSetting(key, OpenSettingsFullScreen);
                    break;
                case QuickPauseMinutesKey:
                    profile.SetSetting(key, QuickPauseMinutes);
                    break;
                case AIProviderKey:
                    profile.SetSetting(key, AIProvider);
                    break;
                case APIKeyKey:
                    profile.SetSetting(key, APIKey);
                    break;
                case AIKeyValidatedKey:
                    profile.SetSetting(key, AIKeyValidated);
                    break;
                case AIModelKey:
                    profile.SetSetting(key, AIModel);
                    break;
                case LocalModeKey:
                    profile.SetSetting(key, LocalMode);
                    break;
                case AiSuggestionsWhileTypingKey:
                    profile.SetSetting(key, AiSuggestionsWhileTyping);
                    break;
                case AiRewriteOnRequestKey:
                    profile.SetSetting(key, AiRewriteOnRequest);
                    break;
                case AiPrefetchOnSelectionKey:
                    profile.SetSetting(key, AiPrefetchOnSelection);
                    break;
                case BlockedApplicationsKey:
                    profile.SetSetting(key, BlockedApplications);
                    break;
                case ThemeKey:
                    profile.SetSetting(key, Theme);
                    break;
                case SuggestionSortModeKey:
                    profile.SetSetting(key, SuggestionSortMode);
                    break;
                case SuggestionPlacementKey:
                    profile.SetSetting(key, SuggestionPlacement);
                    break;
                case RequireConfirmationForEditsKey:
                    profile.SetSetting(key, RequireConfirmationForEdits);
                    break;
                case GrammarSensitivityKey:
                    profile.SetSetting(key, GrammarSensitivity);
                    break;
                case MuteGrammarForCasualAppsKey:
                    profile.SetSetting(key, MuteGrammarForCasualApps);
                    break;
                case EnableRewriteHotkeyKey:
                    profile.SetSetting(key, EnableRewriteHotkey);
                    break;
                case GrammarCheckingKey:
                    profile.SetSetting(key, GrammarChecking);
                    break;
                case AutoCorrectTyposKey:
                    profile.SetSetting(key, AutoCorrectTypos);
                    break;
                case AutoInsertSpacesKey:
                    profile.SetSetting(key, AutoInsertSpaces);
                    break;
                case DocumentConsistencyCheckingKey:
                    profile.SetSetting(key, DocumentConsistencyChecking);
                    break;
                case AllowCodeSwitchingKey:
                    profile.SetSetting(key, AllowCodeSwitching);
                    break;
                case EnableGrammarHotkeyKey:
                    profile.SetSetting(key, EnableGrammarHotkey);
                    break;
                case GrammarMutedAppsKey:
                    profile.SetSetting(key, GrammarMutedApps);
                    break;
                case AppCategoryOverridesKey:
                    profile.SetSetting(key, AppCategoryOverrides);
                    break;
                case CustomTerminologyKey:
                    profile.SetSetting(key, CustomTerminology);
                    break;
                case AppTerminologyOverridesKey:
                    profile.SetSetting(key, AppTerminologyOverrides);
                    break;
                case DefaultWritingModeKey:
                    profile.SetSetting(key, DefaultWritingMode);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(keys), key, "Unknown settings key.");
            }
        }
    }
}
