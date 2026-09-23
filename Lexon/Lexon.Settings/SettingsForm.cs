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
    private static readonly int[] QuickPauseChoices = [0, 1, 5, 15, 30, 60];
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
    private CheckBox _chkLocalMode = null!;
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
    private Label _lblStyleSummary = null!;
    private Button _btnResetStyle = null!;
    private ListBox _lstAdaptations = null!;
    private Button _btnUndoAdaptation = null!;
    private ComboBox _cmbGrammarSensitivity = null!;
    private CheckBox _chkMuteCasualGrammar = null!;
    private TextBox _txtGrammarMutedApps = null!;
    private Button _btnExportLearning = null!;
    private Button _btnImportLearning = null!;
    private Button _btnAiLog = null!;
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
    private CancellationTokenSource? _aiProbeCts;
    private bool _loading;
    private bool _aiAdvancedVisible;
    private bool _watchingClipboard;
    private string? _waitingProvider;
    private string _activeProvider = "None";
    private string _activeApiKey = string.Empty;
    private bool _aiValidated;
    private bool _settingsRevealed;
    private int _paintFreeze;
    private ToolTip? _infoTip;
    private int _fieldLeft;
    private int _fieldMiddle;
    private int _fieldRight;
    private int _fieldListHeight;

    internal bool LayoutIsWide => _wideLayoutActive;

    public SettingsForm()
        : this(null, null, null)
    {
    }

    public SettingsForm(Profile? profile, PrivacyGuard? privacyGuard, IStorage? storage, ThemeManager? themeManager = null, SuggestionPipeline? suggestionPipeline = null, ISuggestionOverlay? suggestionOverlay = null, PersonalizationManager? personalization = null, TextExpansionManager? expansions = null, IEditConfirmation? editConfirmation = null, Action<IAIProvider?>? applyAiProvider = null, CloudAiActivityLog? cloudAiLog = null)
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
        _persistTimer = new System.Windows.Forms.Timer { Interval = 400 };
        _persistTimer.Tick += (_, _) => FlushPersist();
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
            _aiProbeCts?.Cancel();
            _aiProbeCts?.Dispose();
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
        _sectionGeneral = Section(
            "General",
            Hint(_chkAutoStart, "Launch Lexon when you sign in to Windows."),
            Hint(_chkMinimizeToTray, "Close hides Settings. Lexon stays in the tray."),
            Hint(_chkCheckUpdates, "Asks before installing anything."),
            Hint(_chkOpenFullScreen, "Opens maximized so the three-column layout is used."),
            Caption("Double-press Ctrl pauses for"),
            Hint(_cmbQuickPause, "How long Lexon stays off after you double-press Ctrl. It turns itself back on when the time is up, or sooner if you double-press Ctrl again."));

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
        _sectionAi = Section(
            "AI",
            Hint(_lblAiRecommended, "Paste an API key to connect. OpenAI is recommended."),
            _lnkMoreProviders,
            _cmbAIProvider,
            Hint(_btnGetApiKey, "Opens the provider’s key page, then waits for you to copy a key."),
            Caption("API key"),
            _txtAPIKey,
            _btnCancelWait,
            _lblAiStatus,
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
        _sectionPrivacy = Section(
            "Privacy",
            Hint(_chkLocalMode, "No cloud AI. Dictionary suggestions still work."),
            Caption("Blocked apps"),
            Hint(_blockedRow, "Password fields are always skipped. Add comma-separated process names, for example outlook.exe, or pick a running app."),
            Hint(_btnAiLog, "Shows recent cloud AI requests from this PC."));

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
            Margin = new Padding(0, 4, 0, 16)
        };
        _btnLearnedWords.Click += OnLearnedWordsClicked;
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
        _chkAutoCorrectTypos = Check("Auto-correct known typos");
        _chkEnableGrammarHotkey = Check("Grammar shortcut (Ctrl+Alt+G)");
        _cmbGrammarSensitivity = Combo(360);
        _cmbGrammarSensitivity.Items.AddRange(new[] { "Low", "Medium", "High" });
        _cmbGrammarSensitivity.SelectedIndex = 1;
        _chkMuteCasualGrammar = Check("Mute grammar checks in casual apps");
        _txtGrammarMutedApps = Field(360);
        _sectionWriting = Section(
            "Writing",
            Hint(_btnWritingStats, "Words, pace, and style collected while you type."),
            Hint(_btnLearnedWords, "Vocabulary Lexon learned from you."),
            Hint(_btnExportLearning, "Save learned vocabulary and style as a JSON file."),
            Hint(_btnImportLearning, "Restore learned vocabulary and style from a JSON file."),
            Caption("Detected writing style"),
            _lblStyleSummary,
            Hint(_btnResetStyle, "Clears the detected style for this profile."),
            Caption("Style adjustments"),
            Hint(_lstAdaptations, "From repeated rejections of a suggestion."),
            _btnUndoAdaptation,
            Caption("Grammar"),
            Hint(_chkGrammarChecking, "Local rules: agreement, typos, punctuation. Tab accepts a fix. No shortcut required."),
            Hint(_chkAutoCorrectTypos, "Only the built-in misspelling list. Learned words are left alone."),
            Caption("Grammar sensitivity"),
            Hint(_cmbGrammarSensitivity, "Higher flags more issues."),
            Hint(_chkMuteCasualGrammar, "Skip grammar in chat and other casual apps."),
            Caption("Muted grammar apps"),
            Hint(_txtGrammarMutedApps, "Comma-separated process names."),
            Hint(_chkEnableRewriteHotkey, "You can also select text and click Aa."),
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
            FitBlockedAppsRow(left);
            SetWidth(_cmbQuickPause, left);
            SetWidth(_cmbTheme, middle);
            SetWidth(_cmbSuggestionSort, middle);
            SetWidth(_cmbSuggestionPlacement, middle);
            SetWidth(_lstAdaptations, middle);
            SetWidth(_cmbGrammarSensitivity, middle);
            SetWidth(_txtGrammarMutedApps, middle);
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
            FitBlockedAppsRow(360);
            SetWidth(_cmbQuickPause, 360);
            SetWidth(_cmbTheme, 360);
            SetWidth(_cmbSuggestionSort, 360);
            SetWidth(_cmbSuggestionPlacement, 360);
            SetWidth(_lstAdaptations, 360);
            SetWidth(_cmbGrammarSensitivity, 360);
            SetWidth(_txtGrammarMutedApps, 360);
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

    private void OnLearnedWordsClicked(object? sender, EventArgs e)
    {
            using var form = new LearnedWordsForm(_suggestionPipeline, _themeManager);
            form.ShowDialog(this);
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
            ApplyNow();
            UpdateAiEntryMode();
            ScheduleAiProbe();
        };
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
        _chkAutoCorrectTypos.CheckedChanged += (_, _) => ApplyNow();
        _chkEnableGrammarHotkey.CheckedChanged += (_, _) => ApplyNow();
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
    }

    private void LoadSettings()
    {
        _loading = true;
        _chkAutoStart.Checked = WindowsStartup.IsEnabled();
        _chkMinimizeToTray.Checked = _profile.GetSetting("MinimizeToTray", true);
        _chkCheckUpdates.Checked = _profile.GetSetting("EnableAutoUpdates", true);
        _chkOpenFullScreen.Checked = _profile.GetSetting("OpenSettingsFullScreen", false);
        _cmbQuickPause.SelectedIndex = PauseIndexFromMinutes(_profile.GetSetting("QuickPauseMinutes", 15));

        var provider = _profile.GetSetting("AIProvider", "None");
        _activeProvider = string.IsNullOrWhiteSpace(provider) ? "None" : provider;
        _activeApiKey = _profile.GetSetting("APIKey", string.Empty);
        _aiValidated = _profile.GetSetting("AIKeyValidated", !string.IsNullOrEmpty(_activeApiKey) || _activeProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase));
        var providerIndex = _cmbAIProvider.Items.IndexOf(_activeProvider);
        _cmbAIProvider.SelectedIndex = providerIndex >= 0 ? providerIndex : _cmbAIProvider.Items.IndexOf(AiProviderCatalog.Recommended);
        _txtAPIKey.Text = _activeApiKey;
        FillModelChoices(_activeProvider.Equals("None", StringComparison.OrdinalIgnoreCase) ? AiProviderCatalog.Recommended : _activeProvider, _profile.GetSetting("AIModel", string.Empty));
        if (AiProviderCatalog.ShowAdvancedByDefault(_activeProvider))
        {
            ShowAdvancedProviders();
        }

        UpdateAiEntryMode();
        SetAiStatus(
            _aiValidated && !_activeProvider.Equals("None", StringComparison.OrdinalIgnoreCase)
                ? (_activeProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
                    ? "Ollama is the saved provider. Checking…"
                    : "Key saved. Checking…")
                : "Paste a key to connect. Lexon will check it automatically.",
            _aiValidated ? Color.FromArgb(0, 120, 80) : Color.FromArgb(90, 90, 90));
        _chkLocalMode.Checked = _profile.GetSetting("LocalMode", false);

        var blockedApps = _profile.GetSetting<List<string>>("BlockedApplications", new List<string>());
        _txtBlockedApps.Text = string.Join(", ", blockedApps);

        var currentTheme = _profile.GetSetting("Theme", "Light");
        var themeIndex = _cmbTheme.Items.IndexOf(currentTheme);
        _cmbTheme.SelectedIndex = themeIndex >= 0 ? themeIndex : 0;

        var sortMode = _profile.GetSetting("SuggestionSortMode", "Relevant");
        _cmbSuggestionSort.SelectedIndex = string.Equals(sortMode, "Used", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        var placement = _profile.GetSetting("SuggestionPlacement", "Below");
        _cmbSuggestionPlacement.SelectedIndex = string.Equals(placement, "Above", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _chkRequireConfirmation.Checked = _profile.GetSetting("RequireConfirmationForEdits", true);
        var grammarSensitivity = _profile.GetSetting("GrammarSensitivity", "Medium");
        var grammarIndex = _cmbGrammarSensitivity.Items.IndexOf(grammarSensitivity);
        _cmbGrammarSensitivity.SelectedIndex = grammarIndex >= 0 ? grammarIndex : 1;
        _chkMuteCasualGrammar.Checked = _profile.GetSetting("MuteGrammarForCasualApps", false);
        _chkEnableRewriteHotkey.Checked = _profile.GetSetting("EnableRewriteHotkey", true);
        _chkGrammarChecking.Checked = _profile.GetSetting("GrammarChecking", true);
        _chkAutoCorrectTypos.Checked = _profile.GetSetting("AutoCorrectTypos", true);
        _chkEnableGrammarHotkey.Checked = _profile.GetSetting("EnableGrammarHotkey", true);
        var mutedGrammar = _profile.GetSetting<List<string>>("GrammarMutedApps", new List<string>());
        _txtGrammarMutedApps.Text = string.Join(", ", mutedGrammar);
        _lstAppTone.Items.Clear();
        foreach (var row in _profile.GetSetting<List<string>>("AppCategoryOverrides", new List<string>()))
        {
            _lstAppTone.Items.Add(row);
        }

        RefreshLearnedUi();
        _loading = false;
        if (!_chkLocalMode.Checked)
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
        if (_loading)
        {
            return;
        }

        _persistTimer.Stop();
        _persistTimer.Start();
    }

    private void FlushPersist()
    {
        _persistTimer.Stop();
        if (_loading)
        {
            return;
        }

        ApplySettings();
        _ = _profile.SaveAsync();
    }

    private void ApplySettings()
    {
        var blockedApps = _txtBlockedApps.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        WindowsStartup.SetEnabled(_chkAutoStart.Checked);

        var selectedTheme = _cmbTheme.SelectedItem?.ToString() ?? "Light";
        var selectedSortMode = _cmbSuggestionSort.SelectedIndex == 1 ? "Used" : "Relevant";
        var selectedPlacement = _cmbSuggestionPlacement.SelectedIndex == 1 ? "Above" : "Below";

        _profile.SetSetting("MinimizeToTray", _chkMinimizeToTray.Checked);
        _profile.SetSetting("EnableAutoUpdates", _chkCheckUpdates.Checked);
        _profile.SetSetting("OpenSettingsFullScreen", _chkOpenFullScreen.Checked);
        _profile.SetSetting("QuickPauseMinutes", MinutesFromPauseIndex(_cmbQuickPause.SelectedIndex));
        _profile.SetSetting("AIProvider", _activeProvider);
        _profile.SetSetting("APIKey", _activeApiKey);
        _profile.SetSetting("AIKeyValidated", _aiValidated);
        _profile.SetSetting("AIModel", SelectedModel());
        _profile.SetSetting("LocalMode", _chkLocalMode.Checked);
        _profile.SetSetting("BlockedApplications", blockedApps);
        _profile.SetSetting("Theme", selectedTheme);
        _profile.SetSetting("SuggestionSortMode", selectedSortMode);
        _profile.SetSetting("SuggestionPlacement", selectedPlacement);
        _profile.SetSetting("RequireConfirmationForEdits", _chkRequireConfirmation.Checked);
        _profile.SetSetting("GrammarSensitivity", _cmbGrammarSensitivity.SelectedItem?.ToString() ?? "Medium");
        _profile.SetSetting("MuteGrammarForCasualApps", _chkMuteCasualGrammar.Checked);
        _profile.SetSetting("EnableRewriteHotkey", _chkEnableRewriteHotkey.Checked);
        _profile.SetSetting("GrammarChecking", _chkGrammarChecking.Checked);
        _profile.SetSetting("AutoCorrectTypos", _chkAutoCorrectTypos.Checked);
        _profile.SetSetting("EnableGrammarHotkey", _chkEnableGrammarHotkey.Checked);
        var mutedGrammar = _txtGrammarMutedApps.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        _profile.SetSetting("GrammarMutedApps", mutedGrammar);
        var toneRows = _lstAppTone.Items.Cast<object>().Select(i => i.ToString()!).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        _profile.SetSetting("AppCategoryOverrides", toneRows);
        if (_editConfirmation != null)
        {
            _editConfirmation.RequireConfirmation = _chkRequireConfirmation.Checked;
        }

        _privacyGuard.ReplaceBlockedApplications(blockedApps);
        _suggestionPipeline?.SetSortMode(selectedSortMode);
        _suggestionOverlay?.SetPlacement(selectedPlacement);
        SetupMinimizeToTrayBehavior();
    }

    private static int MinutesFromPauseIndex(int index) =>
        index >= 0 && index < QuickPauseChoices.Length ? QuickPauseChoices[index] : 15;

    private static int PauseIndexFromMinutes(int minutes)
    {
        var index = Array.IndexOf(QuickPauseChoices, minutes);
        return index >= 0 ? index : 3;
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
    {
        if (_aiAdvancedVisible)
        {
            return _cmbAIProvider.SelectedItem?.ToString() ?? AiProviderCatalog.Recommended;
        }

        return AiProviderCatalog.Recommended;
    }

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

    private async Task ProbeAiAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        if (_chkLocalMode.Checked)
        {
            SetAiStatus("Local-only mode is on, so AI is not used.", Color.FromArgb(140, 100, 0));
            return;
        }

        var provider = SelectedUiProvider();
        var key = _txtAPIKey.Text.Trim();

        if (provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            _activeProvider = "None";
            _activeApiKey = string.Empty;
            _aiValidated = true;
            SetAiStatus("AI provider disconnected.", Color.FromArgb(90, 90, 90));
            ApplyNow();
            _applyAiProvider?.Invoke(null);
            return;
        }

        if (AiProviderCatalog.UsesApiKey(provider) && string.IsNullOrEmpty(key))
        {
            if (!_watchingClipboard)
            {
                SetAiStatus("Paste a key to connect. Lexon will check it automatically.", Color.FromArgb(90, 90, 90));
            }

            return;
        }

        var checking = provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
            ? "Checking for Ollama running locally…"
            : "Checking key…";
        SetAiStatus(checking, Color.FromArgb(0, 90, 160));

        _aiProbeCts?.Cancel();
        _aiProbeCts?.Dispose();
        _aiProbeCts = new CancellationTokenSource();
        var token = _aiProbeCts.Token;

        AiProbeResult result;
        try
        {
            result = await AiProviderCatalog.ProbeAsync(provider, key, token, SelectedModel());
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (IsDisposed || token.IsCancellationRequested)
        {
            return;
        }

        if (!result.Succeeded)
        {
            SetAiStatus(result.Message, Color.FromArgb(180, 40, 40));
            return;
        }

        _activeProvider = provider;
        _activeApiKey = AiProviderCatalog.UsesApiKey(provider) ? key : string.Empty;
        _aiValidated = true;
        EnsureDefaultModel(provider);
        SetAiStatus(provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase) ? "Ollama found." : "Connected.", Color.FromArgb(0, 120, 80));
        ApplyNow();
        try
        {
            _applyAiProvider?.Invoke(AiProviderCatalog.Create(provider, key, model: SelectedModel()));
        }
        catch (Exception)
        {
            SetAiStatus("Connected, but Lexon could not switch providers until restart.", Color.FromArgb(140, 100, 0));
        }
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
        SetAiStatus("Waiting for you to copy your key from the browser…", Color.FromArgb(0, 90, 160));
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
        var models = AiProviderCatalog.Models(provider).ToList();
        if (models.Count == 0)
        {
            return;
        }

        var pick = string.IsNullOrWhiteSpace(selected) ? AiProviderCatalog.DefaultModel(provider) : selected;
        if (!models.Contains(pick))
        {
            models.Insert(0, pick);
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
            var currentApps = _txtBlockedApps.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (!currentApps.Contains(picker.SelectedProcessName))
            {
                currentApps.Add(picker.SelectedProcessName);
                _txtBlockedApps.Text = string.Join(", ", currentApps);
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
        var app = AppCategoryMapper.EnsureExeExtension(processName);
        if (string.IsNullOrEmpty(app))
        {
            return;
        }

        var row = AppCategoryMapper.FormatRow(app, category);
        for (var i = _lstAppTone.Items.Count - 1; i >= 0; i--)
        {
            if (AppCategoryMapper.TryParseRow(_lstAppTone.Items[i]?.ToString(), out var existing, out _) && existing == app)
            {
                _lstAppTone.Items.RemoveAt(i);
            }
        }

        _lstAppTone.Items.Add(row);
        ApplyNow();
    }

    private void RemoveAppToneOverride()
    {
        if (_lstAppTone.SelectedIndex >= 0)
        {
            _lstAppTone.Items.RemoveAt(_lstAppTone.SelectedIndex);
            ApplyNow();
        }
    }

    private void OnAboutClicked(object? sender, EventArgs e)
    {
        using var about = new AboutForm();
        about.ShowDialog(this);
    }

    public void ReloadBlockedApplications()
    {
        if (IsDisposed || _txtBlockedApps == null)
        {
            return;
        }

        var blocked = _profile.GetSetting<List<string>>("BlockedApplications", []) ?? [];
        var next = string.Join(", ", blocked);
        if (_txtBlockedApps.Text != next)
        {
            var loading = _loading;
            _loading = true;
            _txtBlockedApps.Text = next;
            _loading = loading;
        }
    }

}
