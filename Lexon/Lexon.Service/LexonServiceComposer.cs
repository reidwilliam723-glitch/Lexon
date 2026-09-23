using Lexon.Core.Interfaces;
using Lexon.Core.Pipeline;
using Lexon.Storage;
using Lexon.Profiles;
using Lexon.Privacy;
using Lexon.AI;
using Lexon.AI.Interfaces;
using Lexon.AI.Cache;
using Lexon.Input;
using Lexon.Input.Interfaces;
using Lexon.Overlay;
using Lexon.Overlay.Interfaces;
using Lexon.Core.Expansion;
using Lexon.Core;
using Lexon.Core.Learning;
using Lexon.Core.Plugins;
using Lexon.Core.Theming;
using Lexon.Core.Grammar;

namespace Lexon.Service;

/// <summary>
/// Shared composition logic for building LexonService and its dependencies.
/// Used by both console service and WinForms tray app entry points to ensure
/// consistent wiring and avoid divergence.
/// </summary>
public static class LexonServiceComposer
{
    /// <summary>
    /// Result of building the Lexon service composition
    /// </summary>
    public class CompositionResult
    {
        public LexonService Service { get; set; } = null!;
        public IAIProvider? AIProvider { get; set; }
        public IStorage Storage { get; set; } = null!;
        public Profile Profile { get; set; } = null!;
        public PrivacyGuard PrivacyGuard { get; set; } = null!;
        public SuggestionPipeline SuggestionPipeline { get; set; } = null!;
        public TextExpansionManager TextExpansionManager { get; set; } = null!;
        public KeyboardListener KeyboardListener { get; set; } = null!;
        public FocusTracker FocusTracker { get; set; } = null!;
        public TextInjector TextInjector { get; set; } = null!;
        public SuggestionOverlay SuggestionOverlay { get; set; } = null!;
        public SuggestionOverlay GrammarOverlay { get; set; } = null!;
        public KeyboardShortcutManager KeyboardShortcutManager { get; set; } = null!;
        public UndoManager UndoManager { get; set; } = null!;
        public QuickToggleManager QuickToggleManager { get; set; } = null!;
        public PersonalizationManager PersonalizationManager { get; set; } = null!;
        public PluginManager PluginManager { get; set; } = null!;
        public ThemeManager ThemeManager { get; set; } = null!;
        public OverlayThemeHost OverlayThemeHost { get; set; } = null!;
        public IEditConfirmation EditConfirmation { get; set; } = null!;
        public SelectionRewriteService? SelectionRewrite { get; set; }
        public GrammarCheckService? GrammarCheck { get; set; }
        public CloudAiActivityLog CloudAiLog { get; set; } = null!;
        public UpdateManager UpdateManager { get; set; } = null!;
    }

    /// <summary>
    /// Builds the complete Lexon service composition with all dependencies wired.
    /// </summary>
    public static async Task<CompositionResult> BuildAsync()
    {
        // Initialize storage
        var storagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lexon");
        var storage = new EncryptedStorage(storagePath);
        var profile = new Profile(storage) { Id = Profile.DefaultProfileId };
        await profile.LoadAsync();

        // Apply Group Policy settings if available
        ApplyGroupPolicySettings(profile);

        // Initialize update manager
        InitializeUpdateManager(profile);

        // Read settings
        var aiProviderSetting = profile.GetSetting("AIProvider", "None");
        var apiKey = profile.GetSetting("APIKey", string.Empty);
        var localMode = profile.GetSetting("LocalMode", false);
        var suggestionSortMode = profile.GetSetting("SuggestionSortMode", "Relevant");
        var suggestionPlacement = profile.GetSetting("SuggestionPlacement", "Below");
        var requireConfirmation = profile.GetSetting("RequireConfirmationForEdits", true);

        // Initialize privacy guard
        var privacyGuard = new PrivacyGuard();
        ApplyProfileBlockedApplications(profile, privacyGuard);

        // Initialize AI provider with caching (graceful degradation if API key not set)
        var aiCache = new AIResponseCache(500, TimeSpan.FromHours(1));
        IAIProvider? aiProvider = null;

        // Only construct AI provider if not in local mode and provider is not "None"
        if (!localMode && !aiProviderSetting.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                // Use saved API key, fallback to environment variable if empty
                var effectiveApiKey = string.IsNullOrEmpty(apiKey)
                    ? GetEnvironmentKeyForProvider(aiProviderSetting)
                    : apiKey;

                if (!string.IsNullOrEmpty(effectiveApiKey) || aiProviderSetting.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
                {
                    var model = profile.GetSetting("AIModel", AiProviderCatalog.DefaultModel(aiProviderSetting));
                    aiProvider = AiProviderCatalog.Create(aiProviderSetting, effectiveApiKey, aiCache, model);
                }
            }
            catch (InvalidOperationException)
            {
                // API key not configured - continue with dictionary-only suggestions
            }
        }

        // Initialize feedback collector and personalization
        var feedbackCollector = new FeedbackCollector();
        var personalizationManager = new PersonalizationManager(storage, feedbackCollector);
        await personalizationManager.InitializeAsync();
        var cloudAiLog = new CloudAiActivityLog(storage);
        await cloudAiLog.LoadAsync();
        
        // Initialize plugin manager
        var pluginManager = new PluginManager(storage);
        
        // Initialize theme manager
        var themeManager = new ThemeManager(storage);
        await themeManager.InitializeAsync();
        
        // Initialize suggestion pipeline
        var suggestionPipeline = new SuggestionPipeline(privacyGuard);
        suggestionPipeline.SetPersonalizationManager(personalizationManager);
        suggestionPipeline.SetSortMode(suggestionSortMode);
        suggestionPipeline.AddProvider(new TypoSuggestionProvider());
        suggestionPipeline.AddProvider(new GrammarSuggestionProvider
        {
            IsEnabled = () => profile.GetSetting("GrammarChecking", true)
        });
        suggestionPipeline.AddProvider(new DictionarySuggestionProvider(storage));

        // Add AI provider if available
        if (aiProvider != null)
        {
            suggestionPipeline.AddProvider(aiProvider);
        }
        
        // Load plugins and add their providers
        await pluginManager.LoadAllPluginsAsync();
        foreach (var pluginProvider in pluginManager.GetPluginProviders())
        {
            if (pluginManager.IsPluginEnabled(pluginProvider.Name))
            {
                suggestionPipeline.AddProvider(pluginProvider);
            }
        }

        // Initialize text expansion manager
        var textExpansionManager = new TextExpansionManager(storage);
        await textExpansionManager.LoadExpansionsAsync();

        // Initialize input components
        var keyboardListener = new KeyboardListener();
        var focusTracker = new FocusTracker();
        var textInjector = new TextInjector();

        // Initialize overlay (shared theme host wired to core ThemeManager)
        var overlayThemeHost = new OverlayThemeHost(themeManager);
        var suggestionOverlay = new SuggestionOverlay(overlayThemeHost);
        suggestionOverlay.SetPlacement(suggestionPlacement);
        var grammarOverlay = new SuggestionOverlay(
            overlayThemeHost,
            SuggestionOverlay.GrammarClassName,
            "Lexon Grammar",
            "Grammar",
            edgeGap: 24);
        grammarOverlay.SetPlacement(string.Equals(suggestionPlacement, "Above", StringComparison.OrdinalIgnoreCase)
            ? "Below"
            : "Above");
        var previewOverlay = new PreviewOverlay(overlayThemeHost);
        var glanceOverlay = new GlanceOverlay(overlayThemeHost);
        var selectionChip = new SelectionChipOverlay(overlayThemeHost);
        var rewriteMenu = new ActionMenuOverlay(overlayThemeHost);
        var grammarMenu = new ActionMenuOverlay(overlayThemeHost);
        var editConfirmation = new EditConfirmation(previewOverlay, glanceOverlay)
        {
            RequireConfirmation = requireConfirmation
        };

        // Initialize keyboard shortcut manager
        var keyboardShortcutManager = new KeyboardShortcutManager();

        // Initialize undo manager
        var undoManager = new UndoManager(textInjector);

        keyboardShortcutManager.RegisterShortcut("UndoLexon", new KeyboardShortcut
        {
            Key = 0x5A, // Z
            Modifiers = KeyModifiers.Control | KeyModifiers.Shift
        }, (_, _) => undoManager.Undo());

        // Initialize quick toggle manager (double-press Ctrl to disable/enable)
        var quickToggleManager = new QuickToggleManager();
        keyboardListener.KeyPressed += (_, args) => quickToggleManager.HandleKeyPress(args);
        keyboardListener.KeyReleased += (_, args) => quickToggleManager.HandleKeyRelease(args);

        // Initialize writing assistance
        var mouseListener = new MouseListener();
        var selectionRewrite = new SelectionRewriteService(
            aiProvider,
            focusTracker,
            textInjector,
            undoManager,
            editConfirmation,
            rewriteMenu,
            personalizationManager,
            profile,
            privacyGuard,
            cloudAiLog,
            selectionChip,
            glanceOverlay);
        var grammarCheck = new GrammarCheckService(
            aiProvider,
            focusTracker,
            grammarMenu,
            selectionRewrite,
            editConfirmation,
            textInjector,
            undoManager,
            profile,
            privacyGuard,
            glanceOverlay,
            grammarOverlay);

        // Create and wire Lexon service
        var lexonService = new LexonService(
            suggestionPipeline,
            keyboardListener,
            focusTracker,
            suggestionOverlay,
            privacyGuard,
            textInjector,
            textExpansionManager,
            keyboardShortcutManager,
            undoManager,
            grammarOverlay,
            () => profile.GetSetting("AutoCorrectTypos", true),
            personalizationManager
        );

        lexonService.AttachWritingEnhancement(selectionRewrite, grammarCheck, mouseListener);
        WarmupProvider(aiProvider);

        // Wire quick toggle to enable/disable expansion and suggestions
        quickToggleManager.ToggleStateChanged += (_, isEnabled) =>
        {
            textExpansionManager.SetEnabled(isEnabled);
            suggestionPipeline.SetEnabled(isEnabled);
            grammarCheck.SetEnabled(isEnabled);
            if (!isEnabled)
            {
                suggestionOverlay.Hide();
                grammarOverlay.Hide();
            }
        };

        return new CompositionResult
        {
            Service = lexonService,
            AIProvider = aiProvider,
            Storage = storage,
            Profile = profile,
            PrivacyGuard = privacyGuard,
            SuggestionPipeline = suggestionPipeline,
            TextExpansionManager = textExpansionManager,
            KeyboardListener = keyboardListener,
            FocusTracker = focusTracker,
            TextInjector = textInjector,
            SuggestionOverlay = suggestionOverlay,
            GrammarOverlay = grammarOverlay,
            KeyboardShortcutManager = keyboardShortcutManager,
            UndoManager = undoManager,
            QuickToggleManager = quickToggleManager,
            PersonalizationManager = personalizationManager,
            PluginManager = pluginManager,
            ThemeManager = themeManager,
            OverlayThemeHost = overlayThemeHost,
            EditConfirmation = editConfirmation,
            SelectionRewrite = selectionRewrite,
            GrammarCheck = grammarCheck,
            CloudAiLog = cloudAiLog,
            UpdateManager = UpdateManager.Instance
        };
    }

    public static void ApplyAiProvider(CompositionResult composition, IAIProvider? provider)
    {
        foreach (var name in new[] { "OpenAI", "Gemini", "DeepSeek", "Ollama", "AI" })
        {
            composition.SuggestionPipeline.RemoveProvider(name);
        }

        if (provider != null)
        {
            composition.SuggestionPipeline.AddProvider(provider);
        }

        composition.AIProvider = provider;
        composition.SelectionRewrite?.SetProvider(provider);
        composition.GrammarCheck?.SetProvider(provider);
        WarmupProvider(provider);
    }

    private static void WarmupProvider(IAIProvider? provider)
    {
        if (provider == null)
        {
            return;
        }

        _ = Task.Run(() => provider.WarmupAsync());
    }

    private static void ApplyProfileBlockedApplications(Profile profile, PrivacyGuard privacyGuard)
    {
        var blockedApps = profile.GetSetting<List<string>>("BlockedApplications", new List<string>());
        privacyGuard.ReplaceBlockedApplications(blockedApps);
    }

    private static string? GetEnvironmentKeyForProvider(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "openai" => Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            "gemini" => Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
            "deepseek" => Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
            "ollama" => Environment.GetEnvironmentVariable("OLLAMA_API_KEY"),
            _ => null
        };
    }

    private static void ApplyGroupPolicySettings(Profile profile)
    {
        try
        {
            var gpoManager = new GroupPolicyManager();
            gpoManager.Initialize();

            if (!gpoManager.IsGroupPolicyEnabled)
            {
                return; // No Group Policy settings to apply
            }

            var generalSettings = gpoManager.GeneralSettings;
            var aiSettings = gpoManager.AISettings;
            var privacySettings = gpoManager.PrivacySettings;

            // Apply general settings (only if not set by user)
            if (!profile.HasSetting("AutoStart"))
            {
                profile.SetSetting("AutoStart", generalSettings.AutoStart);
            }
            if (!profile.HasSetting("MinimizeToTray"))
            {
                profile.SetSetting("MinimizeToTray", generalSettings.MinimizeToTray);
            }
            if (!profile.HasSetting("LocalMode"))
            {
                profile.SetSetting("LocalMode", generalSettings.LocalMode);
            }

            // Apply AI settings (only if not set by user)
            if (!profile.HasSetting("AIProvider") && !string.IsNullOrEmpty(aiSettings.Provider))
            {
                profile.SetSetting("AIProvider", aiSettings.Provider);
            }
            if (!profile.HasSetting("APIKey") && !string.IsNullOrEmpty(aiSettings.APIKey))
            {
                profile.SetSetting("APIKey", aiSettings.APIKey);
            }

            // Apply privacy settings
            if (!profile.HasSetting("EnableTelemetry"))
            {
                profile.SetSetting("EnableTelemetry", privacySettings.EnableTelemetry);
            }
            if (!profile.HasSetting("EnableCrashReporting"))
            {
                profile.SetSetting("EnableCrashReporting", privacySettings.EnableCrashReporting);
            }

            // Apply blocked applications
            var blockedApps = gpoManager.BlockedApplications;
            if (blockedApps.Length > 0)
            {
                var existingApps = profile.GetSetting<List<string>>("BlockedApplications", new List<string>());
                var mergedApps = existingApps.Union(blockedApps).Distinct().ToList();
                profile.SetSetting("BlockedApplications", mergedApps);
            }
        }
        catch
        {
            // If Group Policy application fails, continue with user settings
        }
    }

    private static void InitializeUpdateManager(Profile profile)
    {
        try
        {
            var updateManager = UpdateManager.Instance;
            var enableUpdates = profile.GetSetting("EnableAutoUpdates", true);
            var updateServer = profile.GetSetting("UpdateServer", "https://lexon.com/updates");
            updateManager.Initialize(enableUpdates, updateServer, TimeSpan.FromHours(24));
        }
        catch
        {
            // Update checks are optional.
        }
    }
}
