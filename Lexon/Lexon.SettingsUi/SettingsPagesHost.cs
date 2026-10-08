using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Lexon.Profiles;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

/// <summary>
/// Builds the six settings pages once per <see cref="GallerySettingsServices"/>
/// so the gallery and the Settings window share the same view models.
/// </summary>
internal sealed class SettingsPagesHost : IDisposable
{
    private readonly GallerySettingsServices _services;
    private readonly DispatcherTimer _persistTimer;
    private readonly ClipboardHwndListener _clipboard = new();
    private readonly GalleryClipboardWatch? _ownedWatch;
    private int _refs;
    private bool _disposed;
    private bool _shown;

    private SettingsPagesHost(GallerySettingsServices services, Window owner)
    {
        _services = services;
        Func<AppSettings> liveSnapshot = services.LiveSnapshot
            ?? (() => OwnedSettingsWriter.BuildLiveSnapshot(services.Profile ?? new Profile(), services.Pages));

        General = new GeneralSettingsViewModel(
            services.Settings,
            services.Startup,
            services.Persist,
            services.SettingsBackup,
            services.FileDialogs,
            services.Messages,
            OnSettingsImported);
        General.OpenFullScreenChanged += open => OpenFullScreenChanged?.Invoke(open);
        General.PropertyChanged += OnSettingsPropertyChanged;
        General.UserEdited += () => OnPageUserEdited(General);
        GeneralPage = new GeneralPage(General);
        services.Pages.Add(General);

        if (services.AiPolicy != null
            && services.AiSession != null
            && services.UrlLauncher != null
            && services.DelayScheduler != null)
        {
            var clipboardWatch = services.ClipboardWatch;
            if (clipboardWatch == null)
            {
                _ownedWatch = new GalleryClipboardWatch(_clipboard, owner);
                clipboardWatch = _ownedWatch;
            }

            Ai = new AiSettingsViewModel(
                services.Settings,
                services.Persist,
                services.AiSession,
                services.AiPolicy,
                clipboardWatch,
                services.UrlLauncher,
                services.DelayScheduler,
                () => liveSnapshot().LocalMode,
                liveSnapshot,
                action =>
                {
                    var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                    dispatcher.BeginInvoke(action);
                });
            Ai.PropertyChanged += OnSettingsPropertyChanged;
            Ai.UserEdited += () => OnPageUserEdited(Ai);
            AiPage = new AiPage(Ai);
            services.Pages.Add(Ai);
        }

        if (services.AiPolicy != null && services.ProcessPicker != null && services.ActivityViewer != null)
        {
            Privacy = new PrivacySettingsViewModel(
                services.Settings,
                services.Persist,
                services.AiPolicy,
                services.ProcessPicker,
                services.ActivityViewer,
                services.Reload,
                liveSnapshot,
                services.Messages);
            Privacy.PropertyChanged += OnSettingsPropertyChanged;
            Privacy.UserEdited += () => OnPageUserEdited(Privacy);
            if (Ai != null)
            {
                Privacy.LocalOnlyChanged += local => Ai.OnLocalOnlyChanged(local);
            }

            PrivacyPage = new PrivacyPage(Privacy);
            services.Pages.Add(Privacy);
        }

        if (services.ThemeSwitcher != null)
        {
            Appearance = new AppearanceSettingsViewModel(services.Settings, services.Persist, services.ThemeSwitcher);
            Appearance.PropertyChanged += OnSettingsPropertyChanged;
            Appearance.UserEdited += () => OnPageUserEdited(Appearance);
            AppearancePage = new AppearancePage(Appearance);
            services.Pages.Add(Appearance);
        }

        if (services.ProcessPicker != null)
        {
            AppTone = new AppToneViewModel(services.Settings, services.Persist, services.ProcessPicker);
            AppTone.PropertyChanged += OnSettingsPropertyChanged;
            AppTone.UserEdited += () => OnPageUserEdited(AppTone);
            AppTonePage = new AppTonePage(AppTone);
            services.Pages.Add(AppTone);
        }

        if (services.WritingDialogs != null && services.FileDialogs != null && services.Messages != null)
        {
            Writing = new WritingViewModel(
                services.Settings,
                services.Persist,
                services.Personalization,
                services.WritingDialogs,
                services.FileDialogs,
                services.Messages);
            Writing.PropertyChanged += OnSettingsPropertyChanged;
            Writing.UserEdited += () => OnPageUserEdited(Writing);
            WritingPage = new WritingPage(Writing);
            services.Pages.Add(Writing);
        }

        if (services.ProcessPicker != null)
        {
            Apps = new AppsSettingsViewModel(services.Settings, services.Persist, services.ProcessPicker);
            Apps.Attach(Privacy, AppTone, Writing);
            Apps.PropertyChanged += OnSettingsPropertyChanged;
            Apps.UserEdited += () => OnPageUserEdited(Apps);
            AppsPage = new AppsPage(Apps);
            services.Pages.Add(Apps);
        }

        Retarget(owner);
        _persistTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _persistTimer.Tick += (_, _) =>
        {
            services.Persist.TryFlushDue(DateTime.UtcNow);
            if (!services.Persist.HasPending)
            {
                _persistTimer.Stop();
            }
        };
    }

    public event Action<bool>? OpenFullScreenChanged;

    public GeneralSettingsViewModel General { get; }

    public AiSettingsViewModel? Ai { get; }

    public PrivacySettingsViewModel? Privacy { get; }

    public AppearanceSettingsViewModel? Appearance { get; }

    public AppToneViewModel? AppTone { get; }

    public WritingViewModel? Writing { get; }

    public AppsSettingsViewModel? Apps { get; }

    public GeneralPage GeneralPage { get; }

    public AiPage? AiPage { get; }

    public PrivacyPage? PrivacyPage { get; }

    public AppearancePage? AppearancePage { get; }

    public AppTonePage? AppTonePage { get; }

    public WritingPage? WritingPage { get; }

    public AppsPage? AppsPage { get; }

    public ClipboardHwndListener Clipboard => _clipboard;

    public static SettingsPagesHost GetOrCreate(GallerySettingsServices services, Window owner)
    {
        if (services.BoundHost != null)
        {
            return services.BoundHost;
        }

        var host = new SettingsPagesHost(services, owner);
        services.BoundHost = host;
        return host;
    }

    public void AddRef() => _refs++;

    public void Release()
    {
        _refs--;
        if (_refs <= 0)
        {
            Dispose();
        }
    }

    public void Retarget(Window owner)
    {
        _services.AttachOwner?.Invoke(owner);
        _ownedWatch?.Retarget(owner);
    }

    public void OnVisible(bool visible)
    {
        _shown = visible;
        if (visible)
        {
            ReloadCleanPages();
            _persistTimer.Start();
            return;
        }

        Ai?.OnWindowHidden();
        FlushPendingSaves();
    }

    public void NotifyShown()
    {
        _shown = true;
        ReloadCleanPages();
        _persistTimer.Start();
    }

    public void FlushPendingSaves()
    {
        _persistTimer.Stop();
        _services.Save();
    }

    public void NotifyActivated() => ReloadCleanPages();

    public void NotifyDeactivated() => FlushPendingSaves();

    public void ShowGeneral(System.Windows.Controls.ContentControl content)
    {
        content.Content = GeneralPage;
        ReloadPage(General);
    }

    public void ShowAi(System.Windows.Controls.ContentControl content)
    {
        content.Content = AiPage;
        _services.Reload();
        Ai?.OnTabSelected();
    }

    public void ShowPrivacy(System.Windows.Controls.ContentControl content)
    {
        content.Content = PrivacyPage;
        ReloadPage(Privacy);
    }

    public void ShowAppearance(System.Windows.Controls.ContentControl content)
    {
        content.Content = AppearancePage;
        ReloadPage(Appearance);
    }

    public void ShowAppTone(System.Windows.Controls.ContentControl content)
    {
        content.Content = AppTonePage;
        ReloadPage(AppTone);
    }

    public void ShowWriting(System.Windows.Controls.ContentControl content)
    {
        content.Content = WritingPage;
        ReloadPage(Writing);
        Writing?.RefreshLearning();
    }

    public void ShowApps(System.Windows.Controls.ContentControl content)
    {
        content.Content = AppsPage;
        ReloadPage(Apps);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        FlushPendingSaves();
        Ai?.Dispose();
        _clipboard.Dispose();
        if (ReferenceEquals(_services.BoundHost, this))
        {
            _services.BoundHost = null;
        }
    }

    private void OnSettingsImported()
    {
        _services.Reload();
        General.Load();
        Ai?.Load();
        Privacy?.Load();
        Appearance?.Load();
        AppTone?.Load();
        Writing?.Load();
        Writing?.RefreshLearning();
        Apps?.Load();
        _services.ApplyLive?.Invoke();
        _services.AfterSettingsImport?.Invoke();
    }

    private void ReloadCleanPages()
    {
        _services.Reload();
        if (!General.IsDirty)
        {
            General.Load();
        }

        if (Ai is { IsDirty: false })
        {
            Ai.Load();
        }

        if (Privacy is { IsDirty: false })
        {
            Privacy.Load();
        }

        if (Appearance is { IsDirty: false })
        {
            Appearance.Load();
        }

        if (AppTone is { IsDirty: false })
        {
            AppTone.Load();
        }

        if (Writing is { IsDirty: false })
        {
            Writing.Load();
        }
        else
        {
            Writing?.RefreshLearning();
        }

        if (Apps is { IsDirty: false })
        {
            Apps.Load();
        }

        if (_services.Pages.Any(static p => p.IsDirty))
        {
            FlushPendingSaves();
        }
    }

    private void ReloadPage(IOwnedSettingsPage? page)
    {
        if (page == null || page.IsDirty)
        {
            return;
        }

        _services.Reload();
        switch (page)
        {
            case GeneralSettingsViewModel general:
                general.Load();
                break;
            case AiSettingsViewModel ai:
                ai.Load();
                break;
            case AppearanceSettingsViewModel appearance:
                appearance.Load();
                break;
            case PrivacySettingsViewModel privacy:
                privacy.Load();
                break;
            case AppToneViewModel appTone:
                appTone.Load();
                break;
            case WritingViewModel writing:
                writing.Load();
                break;
            case AppsSettingsViewModel apps:
                apps.Load();
                break;
        }
    }

    private void OnPageUserEdited(IOwnedSettingsPage page)
    {
        if (_services.Persist.IsLoading)
        {
            return;
        }

        if (page is not AiSettingsViewModel)
        {
            _services.ApplyLive?.Invoke();
        }

        if (_shown)
        {
            _persistTimer.Start();
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_services.Persist.IsLoading)
        {
            return;
        }

        if (e.PropertyName == nameof(GeneralSettingsViewModel.StatusMessage)
            || e.PropertyName == nameof(WritingViewModel.StyleSummary)
            || e.PropertyName == nameof(WritingViewModel.SelectedAdaptationIndex)
            || e.PropertyName == nameof(WritingViewModel.CanUndoAdaptation)
            || e.PropertyName == nameof(AiSettingsViewModel.StatusActiveLine)
            || e.PropertyName == nameof(AiSettingsViewModel.StatusDetail)
            || e.PropertyName == nameof(AiSettingsViewModel.StatusKind)
            || e.PropertyName == nameof(AiSettingsViewModel.IsWaitingForClipboard)
            || e.PropertyName == nameof(AiSettingsViewModel.Heading)
            || e.PropertyName == nameof(AiSettingsViewModel.ShowKeyBox)
            || e.PropertyName == nameof(AiSettingsViewModel.ShowGetKeyButton)
            || e.PropertyName == nameof(AiSettingsViewModel.ShowModel)
            || e.PropertyName == nameof(AiSettingsViewModel.AiFlagsEnabled)
            || e.PropertyName == nameof(AiSettingsViewModel.ShowProviderCombo)
            || e.PropertyName == nameof(AiSettingsViewModel.ShowMoreProvidersLink)
            || e.PropertyName == nameof(AiSettingsViewModel.AdvancedVisible))
        {
            return;
        }

        if (_shown)
        {
            _persistTimer.Start();
        }
    }
}
