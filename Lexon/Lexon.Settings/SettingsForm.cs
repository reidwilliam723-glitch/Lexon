using Lexon.AI;
using Lexon.AI.Interfaces;
using Lexon.Core.Interfaces;
using Lexon.Storage;
using Lexon.Profiles;
using Lexon.Privacy;
using Lexon.Core.Theming;
using Lexon.Core.Pipeline;
using Lexon.Core.Learning;
using Lexon.Core.Expansion;
using Lexon.Core;
using Lexon.Core.Models;
using Lexon.Overlay.Interfaces;
using Lexon.SettingsModel;
using Lexon.Ui;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Lexon.Settings;

public partial class SettingsForm : Form
{
    private readonly IStorage _storage;
    private readonly Profile _profile;
    private readonly PrivacyGuard _privacyGuard;
    private readonly ThemeManager? _themeManager;
    private readonly SuggestionPipeline? _suggestionPipeline;
    private readonly ISuggestionOverlay? _suggestionOverlay;
    private readonly PersonalizationManager? _personalization;
    private readonly TextExpansionManager? _expansions;
    private readonly IEditConfirmation? _editConfirmation;
    private readonly CloudAiActivityLog? _cloudAiLog;
    private readonly bool _ownsProfile;

    private CheckBox _chkAutoStart = null!;
    private CheckBox _chkMinimizeToTray = null!;
    private CheckBox _chkCheckUpdates = null!;
    private CheckBox _chkOpenFullScreen = null!;
    private ComboBox _cmbQuickPause = null!;
    private ComboBox _cmbAIProvider = null!;
    private TextBox _txtAPIKey = null!;
    private Label _lblAiRecommended = null!;
    private LinkLabel _lnkMoreProviders = null!;
    private Label _lblAiStatus = null!;
    private Button _btnGetApiKey = null!;
    private Button _btnCancelWait = null!;
    private ComboBox _cmbAiModel = null!;
    private Label _lblAiModel = null!;
    private CheckBox _chkEnableRewriteHotkey = null!;
    private CheckBox _chkEnableGrammarHotkey = null!;
    private CheckBox _chkGrammarChecking = null!;
    private CheckBox _chkAutoCorrectTypos = null!;
    private CheckBox _chkAutoInsertSpaces = null!;
    private CheckBox _chkAutoCorrectContractions = null!;
    private CheckBox _chkDocumentConsistency = null!;
    private CheckBox _chkAllowCodeSwitching = null!;
    private ComboBox _cmbDefaultWritingMode = null!;
    private CheckBox _chkLocalMode = null!;
    private CheckBox _chkAiTyping = null!;
    private CheckBox _chkAiRewrite = null!;
    private CheckBox _chkAiPrefetch = null!;
    private Label _lblAiActive = null!;
    private Label _lblAiExplain = null!;
    private TextBox _txtBlockedApps = null!;
    private Button _btnAddBlockedApp = null!;
    private ComboBox _cmbTheme = null!;
    private ComboBox _cmbSuggestionSort = null!;
    private ComboBox _cmbSuggestionPlacement = null!;
    private CheckBox _chkRequireConfirmation = null!;
    private ComboBox _cmbAppToneCategory = null!;
    private ListBox _lstAppTone = null!;
    private Button _btnWritingStats = null!;
    private Button _btnLearnedWords = null!;
    private Button _btnTerminology = null!;
    private Label _lblStyleSummary = null!;
    private Button _btnResetStyle = null!;
    private ListBox _lstAdaptations = null!;
    private Button _btnUndoAdaptation = null!;
    private ComboBox _cmbGrammarSensitivity = null!;
    private CheckBox _chkMuteCasualGrammar = null!;
    private TextBox _txtGrammarMutedApps = null!;
    private CheckBox _chkUseLearnedWords = null!;
    private TextBox _txtLearnedMutedApps = null!;
    private Button _btnExportLearning = null!;
    private Button _btnImportLearning = null!;
    private Button _btnExportSettings = null!;
    private Button _btnImportSettings = null!;
    private Button _btnAiLog = null!;
    private Button _btnPrivacyPreview = null!;
    private TableLayoutPanel _layoutHost = null!;
    private FlowLayoutPanel _sectionGeneral = null!;
    private FlowLayoutPanel _sectionAi = null!;
    private FlowLayoutPanel _sectionPrivacy = null!;
    private FlowLayoutPanel _sectionAppearance = null!;
    private FlowLayoutPanel _sectionAppTone = null!;
    private FlowLayoutPanel _sectionWriting = null!;
    private FlowLayoutPanel _columnLeft = null!;
    private FlowLayoutPanel _columnMiddle = null!;
    private FlowLayoutPanel _columnRight = null!;
    private FlowLayoutPanel _blockedRow = null!;
    private FlowLayoutPanel _toneRow = null!;
    private bool _wideLayoutActive;
    private FormClosingEventHandler? _minimizeToTrayHandler;
    private readonly System.Windows.Forms.Timer _persistTimer;
    private readonly System.Windows.Forms.Timer _aiProbeTimer;
    private readonly System.Windows.Forms.Timer _clipboardWatchTimeout;
    private readonly Action<IAIProvider?>? _applyAiProvider;
    private readonly AiAccessPolicy? _accessPolicy;
    private readonly Func<string?>? _installedProviderName;
    private readonly AppSettings _appSettings = new();
    private readonly AiProbeSession _ai = new();
    private readonly PersistScheduler _persist;
    private bool _loading;
    private bool _aiAdvancedVisible;
    private bool _watchingClipboard;
    private string? _waitingProvider;
    private bool _settingsRevealed;
    private int _paintFreeze;
    private ToolTip? _infoTip;
    private int _fieldLeft;
    private int _fieldMiddle;
    private int _fieldRight;
    private int _fieldListHeight;

    internal bool LayoutIsWide => _wideLayoutActive;

    public SettingsForm(Profile? profile, PrivacyGuard? privacyGuard, IStorage? storage, ThemeManager? themeManager = null, SuggestionPipeline? suggestionPipeline = null, ISuggestionOverlay? suggestionOverlay = null, PersonalizationManager? personalization = null, TextExpansionManager? expansions = null, IEditConfirmation? editConfirmation = null, Action<IAIProvider?>? applyAiProvider = null, CloudAiActivityLog? cloudAiLog = null, AiAccessPolicy? accessPolicy = null, Func<string?>? installedAiProviderName = null)
    {
        _ownsProfile = profile == null;
        var storagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lexon");
        _storage = storage ?? new EncryptedStorage(storagePath);
        _profile = profile ?? new Profile(_storage) { Id = Profile.DefaultProfileId };
        _privacyGuard = privacyGuard ?? new PrivacyGuard();
        _themeManager = themeManager;
        _suggestionPipeline = suggestionPipeline;
        _suggestionOverlay = suggestionOverlay;
        _personalization = personalization;
        _expansions = expansions;
        _editConfirmation = editConfirmation;
        _applyAiProvider = applyAiProvider;
        _cloudAiLog = cloudAiLog;
        _accessPolicy = accessPolicy;
        _installedProviderName = installedAiProviderName;
        _persist = new PersistScheduler(() =>
        {
            ApplySettings();
            _ = _profile.SaveAsync();
        });
        _ai.IsAlive = () => !IsDisposed;
        _ai.InstalledProviderName = InstalledProviderName;
        _ai.ApplyProvider = provider => _applyAiProvider?.Invoke(provider);
        _ai.OnStatusChanged = () =>
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(PaintAiFromSession);
                return;
            }

            PaintAiFromSession();
        };
        _ai.ResolveModelAfterSuccess = _ =>
        {
            EnsureDefaultModel(SelectedUiProvider());
            return SelectedModel();
        };
        _ai.Persist = () =>
        {
            if (!_loading)
            {
                ApplySettings();
                SchedulePersist();
            }
        };
        _persistTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _persistTimer.Tick += (_, _) =>
        {
            _persist.IsLoading = _loading;
            _persist.TryFlushDue(DateTime.UtcNow);
            if (!_persist.HasPending)
            {
                _persistTimer.Stop();
            }
        };
        _aiProbeTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _aiProbeTimer.Tick += (_, _) =>
        {
            _aiProbeTimer.Stop();
            _ = ProbeAiAsync();
        };
        _clipboardWatchTimeout = new System.Windows.Forms.Timer { Interval = 3 * 60 * 1000 };
        _clipboardWatchTimeout.Tick += (_, _) => StopClipboardWatch("Timed out waiting for a key. Paste it here if you already copied it.");

        if (_ownsProfile)
        {
            SettingsPaintProbe.Log("ctor LoadAsync start");
            _profile.LoadAsync().GetAwaiter().GetResult();
            SettingsPaintProbe.Log("ctor LoadAsync done");
        }

        SettingsPaintProbe.Log("ctor InitializeComponent start");
        InitializeComponent();
        SettingsPaintProbe.Log("ctor InitializeComponent done");
        LoadSettings();
        SettingsPaintProbe.Log("ctor LoadSettings done");
        HookAutoApply();
        SetupMinimizeToTrayBehavior();
        Shown += OnSettingsShown;
        Resize += (_, _) =>
        {
            if (SettingsPaintProbe.Enabled)
            {
                SettingsPaintProbe.Resize++;
                SettingsPaintProbe.Log($"Resize state={WindowState} size={Size} wide={_wideLayoutActive}");
            }

            UpdateLayoutMode();
        };
        FormClosing += (_, _) =>
        {
            StopClipboardWatch();
            FlushPersist();
        };
        FormClosed += (_, _) =>
        {
            if (_themeManager != null)
            {
                _themeManager.ThemeChanged -= OnExternalThemeChanged;
            }

            StopClipboardWatch();
            _persistTimer.Stop();
            _persistTimer.Dispose();
            _infoTip?.Dispose();
            _aiProbeTimer.Stop();
            _aiProbeTimer.Dispose();
            _clipboardWatchTimeout.Stop();
            _clipboardWatchTimeout.Dispose();
            _ai.Gate.Invalidate();
            _ai.Gate.Dispose();
        };
        VisibleChanged += (_, _) =>
        {
            if (!Visible)
            {
                StopClipboardWatch();
            }
        };
        if (!IsHandleCreated)
        {
            // Forces this form's handle - and every child control's handle - to
            // exist now, off-screen, so ApplyTheme() below takes the flicker-safe
            // ApplyToTreeWithoutFlicker path instead of silently falling back to
            // the unprotected one just because nothing has been shown yet.
            SettingsPaintProbe.Log("ctor CreateControl start");
            CreateControl();
            SettingsPaintProbe.Log("ctor CreateControl done");
        }

        ApplyTheme();
        ComboWheel.GuardTree(this);
        UpdateLayoutMode();
        if (_themeManager != null)
        {
            _themeManager.ThemeChanged += OnExternalThemeChanged;
        }
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Lexon Settings";
        ClientSize = new Size(700, 800);
        MinimumSize = new Size(680, 600);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        Font = new Font("Segoe UI", 9);
        Icon = Lexon.Ui.LexonIconFactory.CreateApplicationIcon();
        Padding = new Padding(24, 20, 24, 16);
        ThemeUi.EnableBufferedPaint(this);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(8, 8, 8, 0)
        };
        var btnClose = new Button { Text = "Close", AutoSize = true, Padding = new Padding(16, 6, 16, 6), Margin = new Padding(0, 0, 8, 0) };
        btnClose.Click += (_, _) => Close();
        var btnAbout = new Button { Text = "About", AutoSize = true, Padding = new Padding(16, 6, 16, 6) };
        btnAbout.Click += OnAboutClicked;
        footer.Controls.Add(btnClose);
        footer.Controls.Add(btnAbout);
        Controls.Add(footer);

        _layoutHost = new CompositedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 6,
            AutoScroll = true,
            Padding = new Padding(8),
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        _layoutHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _layoutHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        _layoutHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        for (var i = 0; i < 6; i++)
        {
            _layoutHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        ThemeUi.EnableBufferedPaint(_layoutHost);

        _columnLeft = ColumnHost();
        _columnMiddle = ColumnHost();
        _columnRight = ColumnHost();

        _chkAutoStart = Check("Start with Windows");
        _chkMinimizeToTray = Check("Minimize to system tray");
        _chkCheckUpdates = Check("Check for updates on startup");
        _chkOpenFullScreen = Check("Open Settings full screen");
        _cmbQuickPause = Combo(360);
        _cmbQuickPause.Items.AddRange(new object[]
        {
            "Until I turn it back on",
            "1 minute",
            "5 minutes",
            "15 minutes",
            "30 minutes",
            "1 hour"
        });
        _cmbQuickPause.SelectedIndex = 3;
        _btnExportSettings = new Button
        {
            Text = "Export settings…",
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 8, 8)
        };
        _btnExportSettings.Click += OnExportSettingsClicked;
        _btnImportSettings = new Button
        {
            Text = "Import settings…",
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 0, 8)
        };
        _btnImportSettings.Click += OnImportSettingsClicked;
        _sectionGeneral = Section(
            "General",
            Hint(_chkAutoStart, "Launch Lexon when you sign in to Windows."),
            Hint(_chkMinimizeToTray, "Close hides Settings. Lexon stays in the tray."),
            Hint(_chkCheckUpdates, "Asks before installing anything."),
            Hint(_chkOpenFullScreen, "Opens maximized so the three-column layout is used."),
            Caption("Double-press Ctrl pauses for"),
            Hint(_cmbQuickPause, "How long Lexon stays off after you double-press Ctrl. It turns itself back on when the time is up, or sooner if you double-press Ctrl again."),
            Caption("Backup"),
            Hint(_btnExportSettings, "Save settings and app rules as a JSON file. Does not include API keys or learned data."),
            Hint(_btnImportSettings, "Restore settings and app rules from a Lexon settings file. Learned vocabulary stays under Writing."));

        _lblAiRecommended = Caption("OpenAI (recommended)");
        _lnkMoreProviders = new LinkLabel
        {
            Text = "More providers",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        _lnkMoreProviders.LinkClicked += (_, _) => ShowAdvancedProviders();
        _cmbAIProvider = Combo(360);
        _cmbAIProvider.Items.AddRange(AiProviderCatalog.AllProviders.Cast<object>().ToArray());
        _cmbAIProvider.SelectedIndex = 0;
        _cmbAIProvider.Visible = false;
        _txtAPIKey = Field(360, isPassword: true);
        _btnGetApiKey = new Button
        {
            Text = "Get your API key",
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 8, 8)
        };
        _btnGetApiKey.Click += (_, _) => OnGetApiKeyClicked();
        _btnCancelWait = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 0, 8),
            Visible = false
        };
        _btnCancelWait.Click += (_, _) => StopClipboardWatch("Cancelled. You can still paste a key.");
        _lblAiStatus = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            Margin = new Padding(0, 0, 0, 8),
            ForeColor = Color.FromArgb(90, 90, 90),
            Text = "Paste a key to connect. Lexon will check it automatically."
        };
        _lblAiModel = Caption("Model");
        _cmbAiModel = Combo(360);
        _lblAiActive = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            Margin = new Padding(0, 0, 0, 4),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(50, 50, 50),
            Text = "AI is off"
        };
        _lblAiExplain = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            Margin = new Padding(0, 0, 0, 8),
            ForeColor = Color.FromArgb(90, 90, 90),
            Text = "When cloud AI suggestions are on, the words around your cursor are sent to your chosen provider as you type. Rewrites send only the text you select. Local Ollama on this PC is not gated by the typing toggle. Nothing is sent in Local-only mode or in blocked apps and password fields."
        };
        _chkAiTyping = Check("Send words around the cursor to cloud AI while I type");
        _chkAiRewrite = Check("Allow AI rewrites of selected text");
        _chkAiPrefetch = Check("Prepare a rewrite as soon as I select text");
        _sectionAi = Section(
            "AI",
            Hint(_lblAiRecommended, "Paste an API key to connect. OpenAI is recommended."),
            _lnkMoreProviders,
            _cmbAIProvider,
            Hint(_btnGetApiKey, "Opens the provider’s key page, then watches your clipboard for a matching key only. Lexon does not store or send anything else from the clipboard. Cancel anytime."),
            Caption("API key"),
            _txtAPIKey,
            _btnCancelWait,
            _lblAiActive,
            _lblAiStatus,
            _lblAiExplain,
            Hint(_chkAiTyping, "Off by default. Applies to OpenAI, Gemini, DeepSeek, and Ollama that is not on this PC. Local Ollama and plugins still work unless Local-only is on."),
            Hint(_chkAiRewrite, "Explicit rewrites from the Aa chip or shortcut. Does not send text until you ask."),
            Hint(_chkAiPrefetch, "Off by default. When on, selecting text may send it before you pick a rewrite."),
            _lblAiModel,
            Hint(_cmbAiModel, "Filled automatically. Change only if you want a different model."));

        _chkLocalMode = Check("Local-only mode (no cloud)");
        _blockedRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 8)
        };
        _txtBlockedApps = Field(240);
        _txtBlockedApps.Margin = new Padding(0, 4, 8, 0);
        _btnAddBlockedApp = new Button
        {
            Text = "Add running app…",
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            Margin = Padding.Empty
        };
        _btnAddBlockedApp.Click += OnAddBlockedAppClicked;
        _blockedRow.Controls.Add(_txtBlockedApps);
        _blockedRow.Controls.Add(_btnAddBlockedApp);
        _btnAiLog = new Button
        {
            Text = "View cloud AI activity log…",
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 4, 0, 8)
        };
        _btnAiLog.Click += OnAiLogClicked;
        _btnPrivacyPreview = new Button
        {
            Text = PrivacyDisclosure.ButtonLabel,
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 4, 0, 8)
        };
        _btnPrivacyPreview.Click += OnPrivacyPreviewClicked;
        _sectionPrivacy = Section(
            "Privacy",
            Hint(_chkLocalMode, "Turns off every AI provider immediately, including Ollama. Saved keys stay so you can turn it back on."),
            Caption("Blocked apps"),
            Hint(_blockedRow, "Password fields are always skipped. Add comma-separated process names, for example outlook.exe, or pick a running app."),
            Hint(_btnAiLog, "Shows recent cloud AI requests from this PC."),
            Hint(_btnPrivacyPreview, "Opens the first-use privacy explanation again."));

        _cmbTheme = Combo(360);
        _cmbTheme.Items.AddRange(new[] { "Light", "Dark", "High Contrast" });
        _cmbTheme.SelectedIndex = 0;
        _cmbSuggestionSort = Combo(360);
        _cmbSuggestionSort.Items.AddRange(new[] { "Most Relevant", "Most Used" });
        _cmbSuggestionSort.SelectedIndex = 0;
        _cmbSuggestionPlacement = Combo(360);
        _cmbSuggestionPlacement.Items.AddRange(new[] { "Below the word", "Above the word" });
        _cmbSuggestionPlacement.SelectedIndex = 0;
        _chkRequireConfirmation = Check("Preview AI rewrites before applying");
        _sectionAppearance = Section(
            "Appearance",
            Caption("Theme"),
            Hint(_cmbTheme, "Colours for Settings and the overlay."),
            Caption("Suggestion order"),
            Hint(_cmbSuggestionSort, "How chips are sorted when several matches appear."),
            Caption("Suggestion position"),
            Hint(_cmbSuggestionPlacement, "Where the overlay sits relative to the current word."),
            Hint(_chkRequireConfirmation, "Show a preview before an AI rewrite is applied."));

        _lstAppTone = new ListBox { Width = 360, Height = 110, Margin = new Padding(0, 0, 0, 8) };
        _toneRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        _cmbAppToneCategory = Combo(120);
        _cmbAppToneCategory.Items.AddRange(new object[] { "Casual", "Formal", "Code", "Neutral" });
        _cmbAppToneCategory.SelectedIndex = 0;
        var btnAddTone = new Button
        {
            Text = "Add running app…",
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(8, 0, 8, 0)
        };
        btnAddTone.Click += (_, _) => OnAddAppToneClicked();
        var btnRemoveTone = new Button { Text = "Remove", AutoSize = true };
        btnRemoveTone.Click += (_, _) => RemoveAppToneOverride();
        _toneRow.Controls.Add(_cmbAppToneCategory);
        _toneRow.Controls.Add(btnAddTone);
        _toneRow.Controls.Add(btnRemoveTone);
        _sectionAppTone = Section(
            "App tone",
            Hint(_lstAppTone, "Defaults cover Slack, Teams, Outlook, Word, and editors. Add a row only to override."),
            Caption("Tone"),
            Hint(_toneRow, "Pick a tone, then choose a running app. Remove uses the selected row."));

        _btnWritingStats = new Button
        {
            Text = "View writing stats…",
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 4, 0, 8)
        };
        _btnWritingStats.Click += OnWritingStatsClicked;
        _btnLearnedWords = new Button
        {
            Text = "Manage learned words…",
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 4, 0, 8)
        };
        _btnLearnedWords.Click += OnLearnedWordsClicked;
        _btnTerminology = new Button
        {
            Text = "Manage terminology…",
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 4, 0, 16)
        };
        _btnTerminology.Click += OnTerminologyClicked;
        _btnExportLearning = new Button
        {
            Text = "Export learned data…",
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 4, 8, 8)
        };
        _btnExportLearning.Click += OnExportLearningClicked;
        _btnImportLearning = new Button
        {
            Text = "Import learned data…",
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 4, 0, 8)
        };
        _btnImportLearning.Click += OnImportLearningClicked;
        _lblStyleSummary = new Label { AutoSize = true, MaximumSize = new Size(360, 0), Margin = new Padding(0, 4, 0, 8) };
        _btnResetStyle = new Button
        {
            Text = "Reset writing style",
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 0, 0, 8)
        };
        _btnResetStyle.Click += OnResetStyleClicked;
        _lstAdaptations = new ListBox { Width = 360, Height = 72, Margin = new Padding(0, 0, 0, 8) };
        _btnUndoAdaptation = new Button { Text = "Undo selected adjustment", AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
        _btnUndoAdaptation.Click += OnUndoAdaptationClicked;
        _chkEnableRewriteHotkey = Check("Rewrite shortcut (Ctrl+Alt+R)");
        _chkGrammarChecking = Check("Suggest grammar fixes automatically");
        _chkAutoCorrectTypos = Check("Auto-correct high-confidence spelling");
        _chkAutoInsertSpaces = Check("Auto-insert missing spaces");
        _chkAutoCorrectContractions = Check("Auto-correct contractions");
        _chkDocumentConsistency = Check("Flag inconsistent spelling of the same term");
        _chkAllowCodeSwitching = Check("Allow other languages mid-sentence");
        _chkEnableGrammarHotkey = Check("Grammar shortcut (Ctrl+Alt+G)");
        _cmbDefaultWritingMode = Combo(360);
        _cmbDefaultWritingMode.Items.AddRange(WritingViewModel.WritingModeLabels.ToArray());
        _cmbDefaultWritingMode.SelectedIndex = 0;
        _cmbGrammarSensitivity = Combo(360);
        _cmbGrammarSensitivity.Items.AddRange(new[] { "Low", "Medium", "High" });
        _cmbGrammarSensitivity.SelectedIndex = 1;
        _chkMuteCasualGrammar = Check("Mute grammar checks in casual apps");
        _txtGrammarMutedApps = Field(360);
        _chkUseLearnedWords = Check("Use learned words");
        _txtLearnedMutedApps = Field(360);
        _sectionWriting = Section(
            "Writing",
            Hint(_btnWritingStats, "Words, pace, and style collected while you type."),
            Hint(_btnLearnedWords, "Vocabulary Lexon learned from you."),
            Hint(_chkUseLearnedWords, "Offer vocabulary Lexon learned from your typing. Turn this off to stop it in every app."),
            Caption("Don't use learned words in these apps"),
            Hint(_txtLearnedMutedApps, "Comma-separated process names. Learned words stay on in every other app."),
            Hint(_btnTerminology, "Names and product terms protected from autocorrect and spelling."),
            Hint(_btnExportLearning, "Save learned vocabulary and style as a JSON file."),
            Hint(_btnImportLearning, "Restore learned vocabulary and style from a JSON file."),
            Caption("Detected writing style"),
            _lblStyleSummary,
            Hint(_btnResetStyle, "Clears the detected style for this profile."),
            Caption("Style adjustments"),
            Hint(_lstAdaptations, "From repeated rejections of a suggestion."),
            _btnUndoAdaptation,
            Caption("Grammar"),
            Hint(_chkGrammarChecking, "Homophones, agreement, possessives, and punctuation. Tab accepts a fix. Never applied automatically."),
            Hint(_chkAutoCorrectTypos, "Unambiguous misspellings such as teh → the. Applied as you type."),
            Hint(_chkAutoInsertSpaces, "Adds a space after commas and periods, and trims extra spaces. Skips code, URLs, and numbers."),
            Hint(_chkAutoCorrectContractions, "I'm, what's, I've, and similar. Off by default; they stay as Tab suggestions."),
            Hint(_chkDocumentConsistency, "Warns when a name or term appears with two different spellings in the same text."),
            Hint(_chkAllowCodeSwitching, "Do not treat foreign or mixed-script words as typos."),
            Caption("Grammar sensitivity"),
            Hint(_cmbGrammarSensitivity, "Higher flags more issues."),
            Hint(_chkMuteCasualGrammar, "Skip grammar in chat and other casual apps."),
            Caption("Muted grammar apps"),
            Hint(_txtGrammarMutedApps, "Comma-separated process names."),
            Hint(_chkEnableRewriteHotkey, "You can also select text and click Aa."),
            Caption("Default writing mode"),
            Hint(_cmbDefaultWritingMode, "Used first until you pick another mode; the menu then remembers your last choice."),
            Hint(_chkEnableGrammarHotkey, "Run a grammar check immediately."));

        Controls.Add(_layoutHost);
        _layoutHost.Controls.Add(_sectionGeneral, 0, 0);
        _layoutHost.Controls.Add(_sectionAi, 0, 1);
        _layoutHost.Controls.Add(_sectionPrivacy, 0, 2);
        _layoutHost.Controls.Add(_sectionAppearance, 0, 3);
        _layoutHost.Controls.Add(_sectionAppTone, 0, 4);
        _layoutHost.Controls.Add(_sectionWriting, 0, 5);
        ApplyCompactLayout();

        ResumeLayout(false);
    }

    private static FlowLayoutPanel ColumnHost() => new()
    {
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoSize = true,
        Margin = Padding.Empty,
        Padding = Padding.Empty
    };

    private static void FillColumn(FlowLayoutPanel column, params Control[] children)
    {
        column.SuspendLayout();
        try
        {
            column.Controls.Clear();
            foreach (var child in children)
            {
                column.Controls.Add(child);
            }
        }
        finally
        {
            column.ResumeLayout(false);
        }
    }

    private static FlowLayoutPanel Section(string title, params Control[] children)
    {
        var section = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        var heading = Heading(title);
        if (title == "General")
        {
            heading.Margin = new Padding(0, 0, 0, 8);
        }

        section.Controls.Add(heading);
        foreach (var child in children)
        {
            section.Controls.Add(child);
        }

        return section;
    }

    private static Label Heading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 10, FontStyle.Bold),
        Margin = new Padding(0, 16, 0, 8)
    };

    private static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 4, 0, 4)
    };

    private static CheckBox Check(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 2, 0, 6)
    };

    private static ComboBox Combo(int width)
    {
        var combo = new ThemedComboBox
        {
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, 0, 8)
        };
        ThemeUi.AttachComboDrawing(combo);

        // AttachComboDrawing switches the box to OwnerDrawFixed, so every repaint of
        // the closed box and each list item is custom-painted. That flickers without
        // double buffering.
        ThemeUi.EnableBufferedPaint(combo);
        return combo;
    }

    private static TextBox Field(int width, bool isPassword = false) => new()
    {
        Width = width,
        UseSystemPasswordChar = isPassword,
        Margin = new Padding(0, 0, 0, 8)
    };

    private const string InfoBadgeTag = "settings-info-badge";

    private Control Hint(Control control, string text)
    {
        var badge = Info(text);
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
            Margin = control.Margin,
            Padding = Padding.Empty
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(0, 0, 6, 0);
        control.Anchor = AnchorStyles.Left;
        badge.Anchor = AnchorStyles.None;
        row.Controls.Add(control, 0, 0);
        row.Controls.Add(badge, 1, 0);
        return row;
    }

    private Label Info(string text)
    {
        _infoTip ??= new ToolTip
        {
            ShowAlways = true,
            AutoPopDelay = 20000,
            InitialDelay = 300,
            ReshowDelay = 100,
            UseAnimation = false,
            UseFading = false
        };

        var badge = new Label
        {
            Text = string.Empty,
            AutoSize = false,
            Size = new Size(14, 16),
            ForeColor = Color.FromArgb(130, 130, 138),
            Cursor = Cursors.Help,
            Margin = Padding.Empty,
            Tag = InfoBadgeTag
        };
        badge.Paint += DrawInfoMark;
        _infoTip.SetToolTip(badge, text);
        return badge;
    }

    private static void DrawInfoMark(object? sender, PaintEventArgs e)
    {
        if (sender is not Label badge)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(badge.ForeColor);
        var x = badge.ClientSize.Width / 2f;
        const float dot = 3.2f;
        e.Graphics.FillEllipse(brush, x - (dot / 2f), 0.6f, dot, dot);
        const float stemWidth = 1.8f;
        const float stemTop = 6.4f;
        var stemHeight = Math.Max(7f, badge.ClientSize.Height - stemTop - 1.2f);
        e.Graphics.FillRectangle(brush, x - (stemWidth / 2f), stemTop, stemWidth, stemHeight);
    }

    private void OnSettingsShown(object? sender, EventArgs e)
    {
        Activate();
        BringToFront();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style |= 0x02000000; // WS_CLIPCHILDREN
            return cp;
        }
    }

    protected override void SetVisibleCore(bool value)
    {
        if (value && !_settingsRevealed)
        {
            if (!IsHandleCreated)
            {
                CreateHandle();
            }

            BeginPaintFreeze();
            try
            {
                SettingsPaintProbe.Log($"SetVisibleCore layout visible={Visible} state={WindowState}");
                if (_chkOpenFullScreen.Checked)
                {
                    PrepareFullScreenLayout();
                    WindowState = FormWindowState.Maximized;
                }

                UpdateLayoutMode();
            }
            finally
            {
                EndPaintFreeze();
            }

            _settingsRevealed = true;
        }

        base.SetVisibleCore(value);
    }

    internal void Present()
    {
        ApplyOpenFullScreenPreference();
        Show();
        BringToFront();
        TopMost = true;
        Activate();
        TopMost = false;
    }

    private void ApplyOpenFullScreenPreference()
    {
        if (_chkOpenFullScreen.Checked)
        {
            OpenFullScreen();
            return;
        }

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }
    }

    private void OpenFullScreen()
    {
        if (WindowState == FormWindowState.Maximized && _wideLayoutActive)
        {
            return;
        }

        BeginPaintFreeze();
        try
        {
            PrepareFullScreenLayout();
            WindowState = FormWindowState.Maximized;
        }
        finally
        {
            EndPaintFreeze();
        }
    }

    private void PrepareFullScreenLayout()
    {
        if (_wideLayoutActive)
        {
            return;
        }

        ApplyWideLayout();
        ApplyPredictedMaximizedFieldSizes();
    }

    private void UpdateLayoutMode()
    {
        if (_layoutHost == null || _sectionGeneral == null)
        {
            return;
        }

        var wide = WindowState == FormWindowState.Maximized;
        if (SettingsPaintProbe.Enabled)
        {
            SettingsPaintProbe.LayoutMode++;
            SettingsPaintProbe.Log($"UpdateLayoutMode wideWanted={wide} wideActive={_wideLayoutActive} handle={IsHandleCreated} visible={Visible} state={WindowState} size={Size}");
        }

        if (wide == _wideLayoutActive)
        {
            ApplyFieldSizes();
            return;
        }

        if (wide)
        {
            ApplyWideLayout();
        }
        else
        {
            ApplyCompactLayout();
        }
    }

    private void ApplyCompactLayout()
    {
        SettingsPaintProbe.Compact++;
        SettingsPaintProbe.Log("ApplyCompactLayout");
        BeginPaintFreeze();
        SuspendLayout();
        _layoutHost.SuspendLayout();
        try
        {
            ConfigureGrid(100f, 0f, 0f, fillRows: false);
            RemoveColumnsFromHost();
            Place(_sectionGeneral, 0, 0);
            Place(_sectionAi, 0, 1);
            Place(_sectionPrivacy, 0, 2);
            Place(_sectionAppearance, 0, 3);
            Place(_sectionAppTone, 0, 4);
            Place(_sectionWriting, 0, 5);
            _wideLayoutActive = false;
            Padding = new Padding(24, 20, 24, 16);
        }
        finally
        {
            _layoutHost.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
            ApplyFieldSizes();
            EndPaintFreeze();
        }
    }

    private void ApplyWideLayout()
    {
        SettingsPaintProbe.Wide++;
        SettingsPaintProbe.Log("ApplyWideLayout");
        BeginPaintFreeze();
        SuspendLayout();
        _layoutHost.SuspendLayout();
        try
        {
            ConfigureGrid(33.3f, 33.3f, 33.4f, fillRows: false);
            FillColumn(_columnLeft, _sectionGeneral, _sectionAi, _sectionPrivacy);
            FillColumn(_columnMiddle, _sectionAppearance, _sectionWriting);
            FillColumn(_columnRight, _sectionAppTone);
            Place(_columnLeft, 0, 0);
            Place(_columnMiddle, 1, 0);
            Place(_columnRight, 2, 0);
            _wideLayoutActive = true;
            Padding = new Padding(28, 24, 28, 20);
        }
        finally
        {
            _layoutHost.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
            ApplyFieldSizes();
            EndPaintFreeze();
        }
    }

    private void ConfigureGrid(float col0, float col1, float col2, bool fillRows)
    {
        SetColumnStyle(0, col0);
        SetColumnStyle(1, col1);
        SetColumnStyle(2, col2);
        for (var i = 0; i < 6; i++)
        {
            if (fillRows && i < 3)
            {
                _layoutHost.RowStyles[i] = new RowStyle(SizeType.Percent, i < 2 ? 33.3f : 33.4f);
            }
            else
            {
                _layoutHost.RowStyles[i] = new RowStyle(SizeType.AutoSize);
            }
        }
    }

    private void SetColumnStyle(int index, float percent)
    {
        _layoutHost.ColumnStyles[index] = percent <= 0
            ? new ColumnStyle(SizeType.Absolute, 0)
            : new ColumnStyle(SizeType.Percent, percent);
    }

    private void RemoveColumnsFromHost()
    {
        foreach (var column in new[] { _columnLeft, _columnMiddle, _columnRight })
        {
            if (column != null && _layoutHost.Controls.Contains(column))
            {
                _layoutHost.Controls.Remove(column);
            }
        }
    }

    private void Place(Control control, int column, int row, int rowSpan = 1)
    {
        if (!_layoutHost.Controls.Contains(control))
        {
            _layoutHost.Controls.Add(control);
        }

        _layoutHost.SetColumnSpan(control, 1);
        _layoutHost.SetRowSpan(control, 1);
        _layoutHost.SetCellPosition(control, new TableLayoutPanelCellPosition(column, row));
        if (rowSpan > 1)
        {
            _layoutHost.SetRowSpan(control, rowSpan);
        }
    }

    private void BeginPaintFreeze()
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        if (_paintFreeze++ != 0)
        {
            SettingsPaintProbe.Log($"BeginPaintFreeze nested count={_paintFreeze}");
            return;
        }

        SettingsPaintProbe.FreezeBegin++;
        SettingsPaintProbe.Log("WM_SETREDRAW freeze");
        SendMessage(Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
    }

    private void EndPaintFreeze()
    {
        if (_paintFreeze == 0)
        {
            return;
        }

        if (--_paintFreeze > 0 || !IsHandleCreated || IsDisposed)
        {
            SettingsPaintProbe.Log($"EndPaintFreeze nested/skip count={_paintFreeze}");
            return;
        }

        SettingsPaintProbe.FreezeEnd++;
        SettingsPaintProbe.Log("WM_SETREDRAW thaw");
        SendMessage(Handle, WmSetRedraw, (IntPtr)1, IntPtr.Zero);
        if (Visible)
        {
            Invalidate(true);
            Update();
        }
    }

    private void ApplyFieldSizes()
    {
        SettingsPaintProbe.FieldSizes++;
        if (SettingsPaintProbe.Enabled && (SettingsPaintProbe.FieldSizes <= 25 || SettingsPaintProbe.FieldSizes % 10 == 0))
        {
            SettingsPaintProbe.Log($"ApplyFieldSizes n={SettingsPaintProbe.FieldSizes} wide={_wideLayoutActive} size={Size}");
        }

        if (_layoutHost == null)
        {
            return;
        }

        if (_wideLayoutActive)
        {
            var cols = _layoutHost.GetColumnWidths();
            var left = cols.Length > 0 ? Math.Max(280, cols[0] - 36) : 280;
            var middle = cols.Length > 1 ? Math.Max(280, cols[1] - 36) : 280;
            var right = cols.Length > 2 ? Math.Max(280, cols[2] - 36) : 280;
            ApplyWideFieldSizes(left, middle, right, WideListHeight());
            return;
        }

        ApplyCompactFieldSizes();
    }

    private void ApplyPredictedMaximizedFieldSizes()
    {
        var field = PredictedMaximizedColumnFieldWidth();
        ApplyWideFieldSizes(field, field, field, WideListHeight());
    }

    private int WideListHeight()
    {
        var height = ClientSize.Height > 0 ? ClientSize.Height : Screen.FromControl(this).WorkingArea.Height;
        return Math.Clamp((int)(height * 0.18), 160, 260);
    }

    private int PredictedMaximizedColumnFieldWidth()
    {
        var area = Screen.FromControl(this).WorkingArea;
        var frame = Width - ClientSize.Width;
        var inner = area.Width - frame - Padding.Horizontal - _layoutHost.Padding.Horizontal - 24;
        return Math.Max(280, inner / 3 - 36);
    }

    private void ApplyWideFieldSizes(int left, int middle, int right, int listHeight)
    {
        if (FieldsAlreadySized(left, middle, right, listHeight))
        {
            FitBlockedAppsRow(left);
            return;
        }

        var freeze = Visible && IsHandleCreated && _paintFreeze == 0;
        if (freeze)
        {
            BeginPaintFreeze();
        }

        try
        {
            SetWidth(_cmbAIProvider, left);
            SetWidth(_txtAPIKey, left);
            SetWidth(_cmbAiModel, left);
            _lblAiRecommended.MaximumSize = new Size(left, 0);
            _lblAiStatus.MaximumSize = new Size(left, 0);
            _lblAiActive.MaximumSize = new Size(left, 0);
            _lblAiExplain.MaximumSize = new Size(left, 0);
            FitBlockedAppsRow(left);
            SetWidth(_cmbQuickPause, left);
            SetWidth(_cmbTheme, middle);
            SetWidth(_cmbSuggestionSort, middle);
            SetWidth(_cmbSuggestionPlacement, middle);
            SetWidth(_lstAdaptations, middle);
            SetWidth(_cmbGrammarSensitivity, middle);
            SetWidth(_cmbDefaultWritingMode, middle);
            SetWidth(_txtGrammarMutedApps, middle);
            SetWidth(_txtLearnedMutedApps, middle);
            SetWidth(_lstAppTone, right);
            if (Math.Abs(_lstAppTone.Height - listHeight) >= 2)
            {
                _lstAppTone.Height = listHeight;
            }
        }
        finally
        {
            if (freeze)
            {
                EndPaintFreeze();
            }
        }
    }

    private void ApplyCompactFieldSizes()
    {
        if (FieldsAlreadySized(360, 360, 360, 110))
        {
            FitBlockedAppsRow(360);
            return;
        }

        var freeze = Visible && IsHandleCreated && _paintFreeze == 0;
        if (freeze)
        {
            BeginPaintFreeze();
        }

        try
        {
            SetWidth(_cmbAIProvider, 360);
            SetWidth(_txtAPIKey, 360);
            SetWidth(_cmbAiModel, 360);
            _lblAiRecommended.MaximumSize = new Size(360, 0);
            _lblAiStatus.MaximumSize = new Size(360, 0);
            _lblAiActive.MaximumSize = new Size(360, 0);
            _lblAiExplain.MaximumSize = new Size(360, 0);
            FitBlockedAppsRow(360);
            SetWidth(_cmbQuickPause, 360);
            SetWidth(_cmbTheme, 360);
            SetWidth(_cmbSuggestionSort, 360);
            SetWidth(_cmbSuggestionPlacement, 360);
            SetWidth(_lstAdaptations, 360);
            SetWidth(_cmbGrammarSensitivity, 360);
            SetWidth(_cmbDefaultWritingMode, 360);
            SetWidth(_txtGrammarMutedApps, 360);
            SetWidth(_txtLearnedMutedApps, 360);
            SetWidth(_lstAppTone, 360);
            if (_lstAppTone.Height != 110)
            {
                _lstAppTone.Height = 110;
            }
        }
        finally
        {
            if (freeze)
            {
                EndPaintFreeze();
            }
        }
    }

    private bool FieldsAlreadySized(int left, int middle, int right, int listHeight)
    {
        if (Math.Abs(left - _fieldLeft) < 8
            && Math.Abs(middle - _fieldMiddle) < 8
            && Math.Abs(right - _fieldRight) < 8
            && Math.Abs(listHeight - _fieldListHeight) < 8)
        {
            return true;
        }

        _fieldLeft = left;
        _fieldMiddle = middle;
        _fieldRight = right;
        _fieldListHeight = listHeight;
        return false;
    }

    private static void SetWidth(Control control, int width)
    {
        if (Math.Abs(control.Width - width) < 2)
        {
            return;
        }

        control.Width = width;
    }

    private void FitBlockedAppsRow(int columnWidth)
    {
        var buttonWidth = Math.Max(_btnAddBlockedApp.Width, _btnAddBlockedApp.GetPreferredSize(Size.Empty).Width);
        const int badge = 16;
        const int gaps = 16;
        SetWidth(_txtBlockedApps, Math.Max(120, columnWidth - buttonWidth - badge - gaps));
        if (_blockedRow.Parent is Control hint)
        {
            hint.MaximumSize = new Size(columnWidth, 0);
        }
    }

    private void OnWritingStatsClicked(object? sender, EventArgs e)
    {
            using var form = new WritingStatsForm(_personalization, _expansions, _themeManager);
            form.ShowDialog(this);
    }

    private void OnExportSettingsClicked(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Lexon settings (*.json)|*.json",
            FileName = "lexon-settings.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var backup = new SettingsImportExport(_storage, _profile);
            var json = backup.ExportSettingsAsync(includePersonalData: false).GetAwaiter().GetResult();
            File.WriteAllText(dialog.FileName, json);
            MessageBox.Show(this, "Settings exported.", "Export");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Couldn't save that file.", "Export");
        }
    }

    private void OnImportSettingsClicked(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "Lexon settings (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string json;
        try
        {
            json = File.ReadAllText(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Couldn't read that file.", "Import");
            return;
        }

        var backup = new SettingsImportExport(_storage, _profile);
        if (backup.ImportSettingsAsync(json, overwrite: true).GetAwaiter().GetResult())
        {
            LoadSettings();
            _privacyGuard?.ReplaceBlockedApplications(_appSettings.BlockedApplications);
            MessageBox.Show(this, "Settings imported.", "Import");
        }
        else
        {
            MessageBox.Show(this, "That file is not a valid Lexon settings export.", "Import");
        }
    }

    private void OnExportLearningClicked(object? sender, EventArgs e)
    {
        if (_personalization == null)
        {
            MessageBox.Show(this, "Learned data is available while Lexon is running.", "Export");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Lexon learning (*.json)|*.json",
            FileName = "lexon-learning.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, _personalization.ExportLearningData());
    }

    private void OnImportLearningClicked(object? sender, EventArgs e)
    {
        if (_personalization == null)
        {
            MessageBox.Show(this, "Learned data is available while Lexon is running.", "Import");
            return;
        }

        using var dialog = new OpenFileDialog { Filter = "Lexon learning (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var json = File.ReadAllText(dialog.FileName);
        if (_personalization.ImportLearningData(json))
        {
            RefreshLearnedUi();
            MessageBox.Show(this, "Imported learned vocabulary and writing style.", "Import");
        }
        else
        {
            MessageBox.Show(this, "That file is not a valid Lexon learning export.", "Import");
        }
    }

    private void OnAiLogClicked(object? sender, EventArgs e)
    {
        using var form = new CloudAiActivityForm(_cloudAiLog);
        form.ShowDialog(this);
    }

    private void OnPrivacyPreviewClicked(object? sender, EventArgs e)
    {
        MessageBox.Show(
            this,
            PrivacyDisclosure.Body,
            PrivacyDisclosure.Title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnLearnedWordsClicked(object? sender, EventArgs e)
    {
            using var form = new LearnedWordsForm(_suggestionPipeline, _themeManager);
            form.ShowDialog(this);
    }

    private void OnTerminologyClicked(object? sender, EventArgs e)
    {
        using var form = new TerminologyForm(
            _appSettings.CustomTerminology,
            _appSettings.AppTerminologyOverrides,
            _themeManager);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _appSettings.CustomTerminology = form.GlobalTerms.ToList();
        _appSettings.AppTerminologyOverrides = form.AppOverrideRows.ToList();
        ApplyNow();
    }

    private void RefreshLearnedUi()
    {
        _lblStyleSummary.Text = _personalization?.GetStyleSummary() ?? "Available while Lexon is running.";
        _lstAdaptations.Items.Clear();
        if (_personalization == null)
        {
            return;
        }

        foreach (var record in _personalization.GetAdaptations().Where(a => !a.Undone))
        {
            _lstAdaptations.Items.Add(record);
        }
    }

    private void OnResetStyleClicked(object? sender, EventArgs e)
    {
        if (_personalization == null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            "Clear the learned writing-style profile only? Vocabulary and other personalization are left unchanged.",
            "Reset writing style",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (result != DialogResult.Yes)
        {
            return;
        }

        _personalization.ResetWritingStyle();
        RefreshLearnedUi();
    }

    private void OnUndoAdaptationClicked(object? sender, EventArgs e)
    {
        if (_personalization == null || _lstAdaptations.SelectedItem is not AdaptationRecord record)
        {
            return;
        }

        _personalization.UndoAdaptation(record.Id);
        RefreshLearnedUi();
    }

    private void SetupMinimizeToTrayBehavior()
    {
        if (_minimizeToTrayHandler != null)
        {
            FormClosing -= _minimizeToTrayHandler;
            _minimizeToTrayHandler = null;
        }

        if (_chkMinimizeToTray.Checked)
        {
            _minimizeToTrayHandler = (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
            FormClosing += _minimizeToTrayHandler;
        }
    }

    private void HookAutoApply()
    {
        _chkAutoStart.CheckedChanged += (_, _) => ApplyNow();
        _chkMinimizeToTray.CheckedChanged += (_, _) => ApplyNow();
        _chkCheckUpdates.CheckedChanged += (_, _) => ApplyNow();
        _chkOpenFullScreen.CheckedChanged += (_, _) =>
        {
            ApplyNow();
            if (_loading)
            {
                return;
            }

            if (_chkOpenFullScreen.Checked)
            {
                OpenFullScreen();
            }
            else if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Normal;
            }
        };
        _cmbQuickPause.SelectedIndexChanged += (_, _) => ApplyNow();
        _chkLocalMode.CheckedChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }

            if (_chkLocalMode.Checked)
            {
                EnterLocalOnly();
                return;
            }

            LeaveLocalOnly();
        };
        _chkAiTyping.CheckedChanged += (_, _) => ApplyAiPolicyNow();
        _chkAiRewrite.CheckedChanged += (_, _) => ApplyAiPolicyNow();
        _chkAiPrefetch.CheckedChanged += (_, _) => ApplyAiPolicyNow();
        _cmbAIProvider.SelectedIndexChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }

            UpdateAiEntryMode();
            StopClipboardWatch();
            ScheduleAiProbe();
        };
        _cmbAiModel.SelectedIndexChanged += (_, _) => ApplyNow();
        _chkEnableRewriteHotkey.CheckedChanged += (_, _) => ApplyNow();
        _chkGrammarChecking.CheckedChanged += (_, _) => ApplyNow();
        _chkUseLearnedWords.CheckedChanged += (_, _) => ApplyNow();
        _chkAutoCorrectTypos.CheckedChanged += (_, _) => ApplyNow();
        _chkAutoInsertSpaces.CheckedChanged += (_, _) => ApplyNow();
        _chkAutoCorrectContractions.CheckedChanged += (_, _) => ApplyNow();
        _chkDocumentConsistency.CheckedChanged += (_, _) => ApplyNow();
        _chkAllowCodeSwitching.CheckedChanged += (_, _) => ApplyNow();
        _chkEnableGrammarHotkey.CheckedChanged += (_, _) => ApplyNow();
        _cmbDefaultWritingMode.SelectedIndexChanged += (_, _) => ApplyNow();
        _cmbSuggestionSort.SelectedIndexChanged += (_, _) => ApplyNow();
        _cmbSuggestionPlacement.SelectedIndexChanged += (_, _) => ApplyNow();
        _chkRequireConfirmation.CheckedChanged += (_, _) => ApplyNow();
        _cmbGrammarSensitivity.SelectedIndexChanged += (_, _) => ApplyNow();
        _chkMuteCasualGrammar.CheckedChanged += (_, _) => ApplyNow();
        _cmbTheme.SelectedIndexChanged += OnThemeChanged;
        _txtAPIKey.TextChanged += (_, _) =>
        {
            SchedulePersist();
            ScheduleAiProbe();
        };
        _txtAPIKey.Leave += (_, _) =>
        {
            _aiProbeTimer.Stop();
            _ = ProbeAiAsync();
        };
        _txtBlockedApps.TextChanged += (_, _) => SchedulePersist();
        _txtGrammarMutedApps.TextChanged += (_, _) => SchedulePersist();
        _txtLearnedMutedApps.TextChanged += (_, _) => SchedulePersist();
    }

    private void LoadSettings()
    {
        _loading = true;
        _persist.IsLoading = true;
        _appSettings.Read(_profile);
        _chkAutoStart.Checked = WindowsStartup.IsEnabled();
        _chkMinimizeToTray.Checked = _appSettings.MinimizeToTray;
        _chkCheckUpdates.Checked = _appSettings.EnableAutoUpdates;
        _chkOpenFullScreen.Checked = _appSettings.OpenSettingsFullScreen;
        _cmbQuickPause.SelectedIndex = QuickPauseOptions.IndexFromMinutes(_appSettings.QuickPauseMinutes);

        _ai.ActiveProvider = _appSettings.AIProvider;
        _ai.ActiveApiKey = _appSettings.APIKey;
        _ai.AiValidated = _appSettings.AIKeyValidated;
        var providerIndex = _cmbAIProvider.Items.IndexOf(_ai.ActiveProvider);
        _cmbAIProvider.SelectedIndex = providerIndex >= 0 ? providerIndex : _cmbAIProvider.Items.IndexOf(AiProviderCatalog.Recommended);
        _txtAPIKey.Text = _ai.ActiveApiKey;
        FillModelChoices(_ai.ActiveProvider.Equals("None", StringComparison.OrdinalIgnoreCase) ? AiProviderCatalog.Recommended : _ai.ActiveProvider, _appSettings.AIModel);
        if (AiProviderCatalog.ShowAdvancedByDefault(_ai.ActiveProvider))
        {
            ShowAdvancedProviders();
        }

        UpdateAiEntryMode();
        _chkLocalMode.Checked = _appSettings.LocalMode;
        _chkAiTyping.Checked = _appSettings.AiSuggestionsWhileTyping;
        _chkAiRewrite.Checked = _appSettings.AiRewriteOnRequest;
        _chkAiPrefetch.Checked = _appSettings.AiPrefetchOnSelection;
        UpdateAiEntryMode();

        _txtBlockedApps.Text = BlockedAppList.FormatCsv(_appSettings.BlockedApplications);

        var themeIndex = _cmbTheme.Items.IndexOf(_appSettings.Theme);
        _cmbTheme.SelectedIndex = themeIndex >= 0 ? themeIndex : 0;

        _cmbSuggestionSort.SelectedIndex = string.Equals(_appSettings.SuggestionSortMode, "Used", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _cmbSuggestionPlacement.SelectedIndex = string.Equals(_appSettings.SuggestionPlacement, "Above", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _chkRequireConfirmation.Checked = _appSettings.RequireConfirmationForEdits;
        var grammarIndex = _cmbGrammarSensitivity.Items.IndexOf(_appSettings.GrammarSensitivity);
        _cmbGrammarSensitivity.SelectedIndex = grammarIndex >= 0 ? grammarIndex : 1;
        _chkMuteCasualGrammar.Checked = _appSettings.MuteGrammarForCasualApps;
        _chkEnableRewriteHotkey.Checked = _appSettings.EnableRewriteHotkey;
        _chkGrammarChecking.Checked = _appSettings.GrammarChecking;
        _chkAutoCorrectTypos.Checked = _appSettings.AutoCorrectTypos;
        _chkAutoInsertSpaces.Checked = _appSettings.AutoInsertSpaces;
        _chkAutoCorrectContractions.Checked = _appSettings.AutoCorrectContractions;
        _chkDocumentConsistency.Checked = _appSettings.DocumentConsistencyChecking;
        _chkAllowCodeSwitching.Checked = _appSettings.AllowCodeSwitching;
        _chkEnableGrammarHotkey.Checked = _appSettings.EnableGrammarHotkey;
        var modeIndex = _cmbDefaultWritingMode.Items.IndexOf(_appSettings.DefaultWritingMode);
        _cmbDefaultWritingMode.SelectedIndex = modeIndex >= 0 ? modeIndex : 0;
        _txtGrammarMutedApps.Text = BlockedAppList.FormatCsv(_appSettings.GrammarMutedApps);
        _chkUseLearnedWords.Checked = _appSettings.UseLearnedWords;
        _txtLearnedMutedApps.Text = BlockedAppList.FormatCsv(_appSettings.LearnedWordsMutedApps);
        _lstAppTone.Items.Clear();
        foreach (var row in _appSettings.AppCategoryOverrides)
        {
            _lstAppTone.Items.Add(row);
        }

        RefreshLearnedUi();
        _loading = false;
        _persist.IsLoading = false;
        SyncAiConnectionFromLoad();
        PushAiPolicy();
        if (_chkLocalMode.Checked)
        {
            _ai.Connection.EnterLocalOnly();
            PaintAiStatus(Color.FromArgb(140, 100, 0));
        }
        else
        {
            ScheduleAiProbe();
        }
    }

    private void ApplyNow()
    {
        if (_loading)
        {
            return;
        }

        ApplySettings();
        SchedulePersist();
    }

    private void SchedulePersist()
    {
        _persist.IsLoading = _loading;
        if (_loading)
        {
            return;
        }

        _persist.Schedule(DateTime.UtcNow);
        if (!_persistTimer.Enabled)
        {
            _persistTimer.Start();
        }
    }

    private void FlushPersist()
    {
        _persistTimer.Stop();
        _persist.IsLoading = _loading;
        _persist.Flush();
    }

    private void ApplySettings()
    {
        var blockedApps = BlockedAppList.Parse(_txtBlockedApps.Text);

        WindowsStartup.SetEnabled(_chkAutoStart.Checked);

        _appSettings.MinimizeToTray = _chkMinimizeToTray.Checked;
        _appSettings.EnableAutoUpdates = _chkCheckUpdates.Checked;
        _appSettings.OpenSettingsFullScreen = _chkOpenFullScreen.Checked;
        _appSettings.QuickPauseMinutes = QuickPauseOptions.MinutesFromIndex(_cmbQuickPause.SelectedIndex);
        _appSettings.AIProvider = _ai.ActiveProvider;
        _appSettings.APIKey = _ai.ActiveApiKey;
        _appSettings.AIKeyValidated = _ai.AiValidated;
        _appSettings.AIModel = SelectedModel();
        _appSettings.LocalMode = _chkLocalMode.Checked;
        _appSettings.AiSuggestionsWhileTyping = _chkAiTyping.Checked;
        _appSettings.AiRewriteOnRequest = _chkAiRewrite.Checked;
        _appSettings.AiPrefetchOnSelection = _chkAiPrefetch.Checked;
        _appSettings.BlockedApplications = blockedApps;
        _appSettings.Theme = _cmbTheme.SelectedItem?.ToString() ?? "Light";
        _appSettings.SuggestionSortMode = _cmbSuggestionSort.SelectedIndex == 1 ? "Used" : "Relevant";
        _appSettings.SuggestionPlacement = _cmbSuggestionPlacement.SelectedIndex == 1 ? "Above" : "Below";
        _appSettings.RequireConfirmationForEdits = _chkRequireConfirmation.Checked;
        _appSettings.GrammarSensitivity = _cmbGrammarSensitivity.SelectedItem?.ToString() ?? "Medium";
        _appSettings.MuteGrammarForCasualApps = _chkMuteCasualGrammar.Checked;
        _appSettings.EnableRewriteHotkey = _chkEnableRewriteHotkey.Checked;
        _appSettings.GrammarChecking = _chkGrammarChecking.Checked;
        _appSettings.AutoCorrectTypos = _chkAutoCorrectTypos.Checked;
        _appSettings.AutoInsertSpaces = _chkAutoInsertSpaces.Checked;
        _appSettings.AutoCorrectContractions = _chkAutoCorrectContractions.Checked;
        _appSettings.DocumentConsistencyChecking = _chkDocumentConsistency.Checked;
        _appSettings.AllowCodeSwitching = _chkAllowCodeSwitching.Checked;
        _appSettings.EnableGrammarHotkey = _chkEnableGrammarHotkey.Checked;
        _appSettings.DefaultWritingMode = _cmbDefaultWritingMode.SelectedItem?.ToString()
            ?? WritingViewModel.WritingModeLabels[0];
        _appSettings.GrammarMutedApps = BlockedAppList.ParseMutedGrammar(_txtGrammarMutedApps.Text);
        _appSettings.UseLearnedWords = _chkUseLearnedWords.Checked;
        _appSettings.LearnedWordsMutedApps = BlockedAppList.ParseMutedGrammar(_txtLearnedMutedApps.Text);
        _appSettings.AppCategoryOverrides = _lstAppTone.Items.Cast<object>().Select(i => i.ToString()!).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        _appSettings.Write(_profile);

        if (_editConfirmation != null)
        {
            _editConfirmation.RequireConfirmation = _appSettings.RequireConfirmationForEdits;
        }

        _privacyGuard.ReplaceBlockedApplications(blockedApps);
        _suggestionPipeline?.SetSortMode(_appSettings.SuggestionSortMode);
        _suggestionOverlay?.SetPlacement(_appSettings.SuggestionPlacement);
        PushAiPolicy();
        SetupMinimizeToTrayBehavior();
    }

    private void ShowAdvancedProviders()
    {
        _aiAdvancedVisible = true;
        _cmbAIProvider.Visible = true;
        _lnkMoreProviders.Visible = false;
        _lblAiRecommended.Text = "Provider";
        UpdateAiEntryMode();
    }

    private string SelectedUiProvider()
        => AiModelChoices.ResolveUiProvider(_aiAdvancedVisible, _cmbAIProvider.SelectedItem?.ToString());

    private void UpdateAiEntryMode()
    {
        var provider = SelectedUiProvider();
        var needsKey = AiProviderCatalog.UsesApiKey(provider) && !_chkLocalMode.Checked;
        _txtAPIKey.Visible = needsKey;
        _btnGetApiKey.Visible = needsKey;
        _lblAiModel.Visible = !provider.Equals("None", StringComparison.OrdinalIgnoreCase) && !_chkLocalMode.Checked;
        _cmbAiModel.Visible = _lblAiModel.Visible;
        if (_cmbAiModel.Visible)
        {
            FillModelChoices(provider, SelectedModel());
        }
        if (!needsKey)
        {
            StopClipboardWatch();
        }
        _lblAiRecommended.Visible = true;
        if (!_aiAdvancedVisible)
        {
            _lblAiRecommended.Text = "OpenAI (recommended)";
        }
        else if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            _lblAiRecommended.Text = "Ollama (local)";
        }
        else if (provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            _lblAiRecommended.Text = "No AI provider";
        }
        else
        {
            _lblAiRecommended.Text = provider;
        }

        var aiFeaturesEnabled = !_chkLocalMode.Checked;
        _chkAiTyping.Enabled = aiFeaturesEnabled;
        _chkAiRewrite.Enabled = aiFeaturesEnabled;
        _chkAiPrefetch.Enabled = aiFeaturesEnabled;
    }

    private void ScheduleAiProbe()
    {
        if (_loading)
        {
            return;
        }

        _aiProbeTimer.Stop();
        _aiProbeTimer.Start();
    }

    private void SetAiStatus(string text, Color color)
    {
        _lblAiStatus.Text = text;
        _lblAiStatus.ForeColor = color;
    }

    private void PaintAiStatus(Color color)
    {
        _lblAiActive.Text = _ai.Connection.ActiveLine;
        SetAiStatus(_ai.Connection.Detail, color);
    }

    private string? InstalledProviderName()
    {
        var name = _installedProviderName?.Invoke();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private void SyncAiConnectionFromLoad()
    {
        if (_chkLocalMode.Checked)
        {
            return;
        }

        var keyPresent = _ai.AiValidated && !_ai.ActiveProvider.Equals("None", StringComparison.OrdinalIgnoreCase);
        _ai.Connection.SeedFromInstalled(InstalledProviderName(), SelectedModel(), keyPresent);
        PaintAiStatus(string.IsNullOrEmpty(InstalledProviderName())
            ? Color.FromArgb(90, 90, 90)
            : Color.FromArgb(0, 120, 80));
    }

    private void PushAiPolicy()
    {
        var changed = _accessPolicy?.Update(
            _chkLocalMode.Checked,
            _chkAiTyping.Checked,
            _chkAiRewrite.Checked,
            _chkAiPrefetch.Checked) ?? false;
        if (changed)
        {
            _suggestionPipeline?.BumpAiEpoch();
        }
    }

    private void ApplyAiPolicyNow()
    {
        if (_loading)
        {
            return;
        }

        PushAiPolicy();
        ApplyNow();
    }

    private void EnterLocalOnly()
    {
        _aiProbeTimer.Stop();
        StopClipboardWatch();
        _ai.EnterLocalOnly();
        UpdateAiEntryMode();
    }

    private void LeaveLocalOnly()
    {
        ApplyNow();
        UpdateAiEntryMode();
        ScheduleAiProbe();
    }

    private async Task ProbeAiAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        var provider = SelectedUiProvider();
        await _ai.ProbeAsyncCore(
            _chkLocalMode.Checked,
            provider,
            _txtAPIKey.Text.Trim(),
            SelectedModel(),
            _watchingClipboard);
    }

    private void PaintAiFromSession()
    {
        var color = _ai.Connection.State switch
        {
            AiConnectionState.LocalOnlyOff => Color.FromArgb(140, 100, 0),
            AiConnectionState.Checking => Color.FromArgb(0, 90, 160),
            AiConnectionState.Connected => Color.FromArgb(0, 120, 80),
            AiConnectionState.Failed => _ai.Connection.Detail.StartsWith("Lexon could not switch", StringComparison.Ordinal)
                ? Color.FromArgb(140, 100, 0)
                : Color.FromArgb(180, 40, 40),
            _ => Color.FromArgb(90, 90, 90)
        };
        PaintAiStatus(color);
    }

    private const int WmClipboardUpdate = 0x031D;
    private const int WmSetRedraw = 0x000B;
    private const int WmSysCommand = 0x0112;
    private const int ScMaximize = 0xF030;
    private const int ScRestore = 0xF120;

    [DllImport("user32.dll")]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    protected override void WndProc(ref Message m)
    {
        if (SettingsPaintProbe.Enabled)
        {
            SettingsPaintProbe.OnWndProc(m.Msg);
        }

        if (m.Msg == WmClipboardUpdate && _watchingClipboard)
        {
            TryFillKeyFromClipboard();
        }

        if (m.Msg == WmSysCommand)
        {
            var command = (int)m.WParam & 0xFFF0;
            if (command == ScMaximize)
            {
                if (!_wideLayoutActive)
                {
                    BeginPaintFreeze();
                    try
                    {
                        ApplyWideLayout();
                        ApplyPredictedMaximizedFieldSizes();
                    }
                    finally
                    {
                        EndPaintFreeze();
                    }
                }

                base.WndProc(ref m);
                return;
            }

            if (command == ScRestore)
            {
                base.WndProc(ref m);
                BeginPaintFreeze();
                try
                {
                    UpdateLayoutMode();
                }
                finally
                {
                    EndPaintFreeze();
                }

                return;
            }
        }

        base.WndProc(ref m);
    }

    private void OnGetApiKeyClicked()
    {
        var provider = SelectedUiProvider();
        var url = AiProviderCatalog.KeyCreationUrl(provider);
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            SetAiStatus("Could not open the key page. Paste a key here instead.", Color.FromArgb(180, 40, 40));
            return;
        }

        StartClipboardWatch(provider);
    }

    private void StartClipboardWatch(string provider)
    {
        StopClipboardWatch();
        if (!IsHandleCreated)
        {
            CreateHandle();
        }

        _waitingProvider = provider;
        if (!AddClipboardFormatListener(Handle))
        {
            SetAiStatus("Could not watch the clipboard. Paste your key here after you copy it.", Color.FromArgb(140, 100, 0));
            return;
        }

        _watchingClipboard = true;
        _btnCancelWait.Visible = true;
        _clipboardWatchTimeout.Stop();
        _clipboardWatchTimeout.Start();
        SetAiStatus("Lexon is watching your clipboard for the next few minutes to catch a key that matches this provider. It only looks for a matching key and does not store or send anything else. Cancel anytime.", Color.FromArgb(0, 90, 160));
    }

    private void StopClipboardWatch(string? status = null)
    {
        _clipboardWatchTimeout.Stop();
        if (_watchingClipboard && IsHandleCreated)
        {
            RemoveClipboardFormatListener(Handle);
        }

        _watchingClipboard = false;
        _waitingProvider = null;
        _btnCancelWait.Visible = false;
        if (!string.IsNullOrEmpty(status))
        {
            SetAiStatus(status, Color.FromArgb(90, 90, 90));
        }
    }

    private void TryFillKeyFromClipboard()
    {
        var provider = _waitingProvider;
        if (string.IsNullOrEmpty(provider) || !Clipboard.ContainsText())
        {
            return;
        }

        string text;
        try
        {
            text = Clipboard.GetText();
        }
        catch
        {
            return;
        }

        if (!AiProviderCatalog.LooksLikeApiKey(provider, text))
        {
            return;
        }

        var key = AiProviderCatalog.FirstLine(text);
        StopClipboardWatch();
        _txtAPIKey.Text = key;
        _aiProbeTimer.Stop();
        _ = ProbeAiAsync();
    }

    private void FillModelChoices(string provider, string? selected)
    {
        var models = AiModelChoices.ForProvider(provider, selected);
        if (models.Count == 0)
        {
            return;
        }

        var pick = AiModelChoices.ResolveSelected(provider, string.IsNullOrWhiteSpace(selected) ? null : selected);
        if (!models.Contains(pick))
        {
            pick = models[0];
        }

        var previous = _loading;
        _loading = true;
        _cmbAiModel.Items.Clear();
        foreach (var model in models)
        {
            _cmbAiModel.Items.Add(model);
        }

        var index = _cmbAiModel.Items.IndexOf(pick);
        _cmbAiModel.SelectedIndex = index >= 0 ? index : 0;
        _loading = previous;
    }

    private string SelectedModel()
    {
        var provider = SelectedUiProvider();
        return _cmbAiModel.SelectedItem?.ToString()
            ?? AiProviderCatalog.DefaultModel(provider);
    }

    private void EnsureDefaultModel(string provider)
    {
        if (_cmbAiModel.SelectedItem == null || _cmbAiModel.Items.Count == 0)
        {
            FillModelChoices(provider, AiProviderCatalog.DefaultModel(provider));
        }
    }

    private void OnAddBlockedAppClicked(object? sender, EventArgs e)
    {
        using var picker = new ProcessPickerForm("Select a running application to block:");
        if (picker.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(picker.SelectedProcessName))
        {
            var name = ApplicationName.Normalize(picker.SelectedProcessName);
            var currentApps = BlockedAppList.Parse(_txtBlockedApps.Text);
            if (BlockedAppList.TryAdd(currentApps, name, out _))
            {
                _txtBlockedApps.Text = BlockedAppList.FormatCsv(currentApps);
                ApplyNow();
            }
        }
    }

    private void OnAboutClicked(object? sender, EventArgs e)
    {
        using var aboutForm = new AboutForm();
        aboutForm.ShowDialog(this);
    }

    private void OnExternalThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(OnExternalThemeChanged, sender, e);
            return;
        }

        ApplyTheme();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_loading || _cmbTheme.SelectedItem == null || _themeManager == null)
        {
            return;
        }

        var selectedTheme = _cmbTheme.SelectedItem.ToString() ?? "Light";
        _cmbTheme.DroppedDown = false;

        // SetTheme raises ThemeChanged, which this window handles by re-theming
        // itself. Applying again here recoloured and repainted the whole form a
        // second time, doubling the visible work of every theme switch.
        _themeManager.SetTheme(selectedTheme);

        ApplyNow();
    }

    private void ApplyTheme()
    {
        if (_themeManager == null)
        {
            return;
        }

        ThemeUi.ApplyToTreeWithoutFlicker(this, _themeManager.CurrentTheme);
        RecolorInfoTags();
    }

    private void RecolorInfoTags()
    {
        var badge = Color.FromArgb(130, 130, 138);
        if (_themeManager != null)
        {
            var fg = ThemeUi.Foreground(_themeManager.CurrentTheme);
            var bg = ThemeUi.Background(_themeManager.CurrentTheme);
            badge = Color.FromArgb((fg.R + bg.R) / 2, (fg.G + bg.G) / 2, (fg.B + bg.B) / 2);
        }

        RecolorInfoTags(this, badge);
    }

    private static void RecolorInfoTags(Control root, Color badge)
    {
        if (root is Label label && Equals(label.Tag, InfoBadgeTag))
        {
            label.ForeColor = badge;
        }

        foreach (Control child in root.Controls)
        {
            RecolorInfoTags(child, badge);
        }
    }

    private void OnAddAppToneClicked()
    {
        if (!Enum.TryParse<AppWritingCategory>(_cmbAppToneCategory.SelectedItem?.ToString(), true, out var category))
        {
            return;
        }

        using var picker = new ProcessPickerForm("Select a running application for this tone:");
        if (picker.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(picker.SelectedProcessName))
        {
            return;
        }

        AddAppToneOverride(picker.SelectedProcessName, category);
    }

    private void AddAppToneOverride(string processName, AppWritingCategory category)
    {
        var rows = _lstAppTone.Items.Cast<object>().Select(i => i.ToString() ?? string.Empty);
        var next = AppToneList.AddOrReplace(rows, processName, category);
        _lstAppTone.Items.Clear();
        foreach (var row in next)
        {
            _lstAppTone.Items.Add(row);
        }

        ApplyNow();
    }

    private void RemoveAppToneOverride()
    {
        if (_lstAppTone.SelectedIndex < 0)
        {
            return;
        }

        var rows = _lstAppTone.Items.Cast<object>().Select(i => i.ToString() ?? string.Empty);
        var next = AppToneList.RemoveAt(rows, _lstAppTone.SelectedIndex);
        _lstAppTone.Items.Clear();
        foreach (var row in next)
        {
            _lstAppTone.Items.Add(row);
        }

        ApplyNow();
    }

    public void ReloadBlockedApplications()
    {
        if (IsDisposed || _txtBlockedApps == null)
        {
            return;
        }

        var blocked = _profile.GetSetting<List<string>>("BlockedApplications", []) ?? [];
        var next = BlockedAppList.FormatCsv(blocked);
        if (_txtBlockedApps.Text != next)
        {
            var loading = _loading;
            _loading = true;
            _txtBlockedApps.Text = next;
            _loading = loading;
        }
    }

}
