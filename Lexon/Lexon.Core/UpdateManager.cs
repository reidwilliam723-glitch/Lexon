using System.Diagnostics;
using System.Reflection;

namespace Lexon.Core;

/// <summary>
/// Checks a version endpoint and notifies. Does not auto-install or replace the running binary.
/// </summary>
public class UpdateManager
{
    private string _updateServerUrl;
    private readonly string _currentVersion;
    private readonly HttpClient _httpClient;
    private TimeSpan _checkInterval;
    private DateTime _lastCheckTime;
    private bool _isEnabled = true;
    private static UpdateManager? _instance;

    public event EventHandler<UpdateCheckResult>? UpdateFound;

    public static UpdateManager Instance
    {
        get
        {
            _instance ??= new UpdateManager();
            return _instance;
        }
    }

    public UpdateManager()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, GetApplicationVersion(), "https://lexon.com/updates")
    {
    }

    public UpdateManager(HttpClient httpClient, string currentVersion, string updateServerUrl)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _currentVersion = string.IsNullOrWhiteSpace(currentVersion) ? "0.0.0" : currentVersion;
        _updateServerUrl = string.IsNullOrWhiteSpace(updateServerUrl) ? "https://lexon.com/updates" : updateServerUrl.TrimEnd('/');
        _checkInterval = TimeSpan.FromHours(24);
        _lastCheckTime = DateTime.MinValue;
    }

    public void Initialize(bool enabled, string? serverUrl = null, TimeSpan? checkInterval = null)
    {
        if (AppPackaging.IsPackaged)
        {
            _isEnabled = false;
            return;
        }

        _isEnabled = enabled;
        if (!string.IsNullOrEmpty(serverUrl))
        {
            _updateServerUrl = serverUrl.TrimEnd('/');
        }

        if (checkInterval.HasValue)
        {
            _checkInterval = checkInterval.Value;
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool forceCheck = false)
    {
        if (AppPackaging.IsPackaged || !_isEnabled)
        {
            return new UpdateCheckResult { Success = true, UpdateAvailable = false };
        }

        if (!forceCheck && !UpdatePolicy.ShouldCheckNow(_lastCheckTime, DateTime.UtcNow, _checkInterval))
        {
            return new UpdateCheckResult { Success = true, UpdateAvailable = false, Skipped = true };
        }

        try
        {
            _lastCheckTime = DateTime.UtcNow;
            var versionInfo = await GetLatestVersionInfoAsync();
            if (versionInfo == null)
            {
                return new UpdateCheckResult { Success = false, Error = "Failed to retrieve version information" };
            }

            var updateAvailable = UpdatePolicy.IsNewer(versionInfo.Version, _currentVersion);
            var result = new UpdateCheckResult
            {
                Success = true,
                UpdateAvailable = updateAvailable,
                CurrentVersion = _currentVersion,
                LatestVersion = versionInfo.Version,
                ReleaseNotes = versionInfo.ReleaseNotes,
                DownloadUrl = versionInfo.DownloadUrl,
                FileSize = versionInfo.FileSize,
                ReleaseDate = versionInfo.ReleaseDate,
                Sha256 = versionInfo.Sha256,
                PrivacyNotes = versionInfo.PrivacyNotes,
                AutoInstall = false
            };

            if (updateAvailable)
            {
                UpdateFound?.Invoke(this, result);
            }

            return result;
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Opens the published download page. Never replaces the running process.
    /// </summary>
    public static void OpenDownloadPage(string? downloadUrl)
    {
        if (AppPackaging.IsPackaged || string.IsNullOrWhiteSpace(downloadUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = downloadUrl,
            UseShellExecute = true
        });
    }

    public static bool VerifyFileHash(string path, string? expectedSha256)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return false;
        }

        return UpdatePolicy.MatchesSha256(File.ReadAllBytes(path), expectedSha256);
    }

    private async Task<VersionInfo?> GetLatestVersionInfoAsync()
    {
        var response = await _httpClient.GetAsync($"{_updateServerUrl}/version.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return UpdatePolicy.ParseManifest(json);
    }

    private static string GetApplicationVersion()
    {
        try
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version
                ?? Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString(3) ?? "2.0.0";
        }
        catch
        {
            return "2.0.0";
        }
    }

    public void SetEnabled(bool enabled) => _isEnabled = enabled;
}

public class UpdateCheckResult
{
    public bool Success { get; set; }
    public bool UpdateAvailable { get; set; }
    public bool Skipped { get; set; }
    public bool AutoInstall { get; set; }
    public string? CurrentVersion { get; set; }
    public string? LatestVersion { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Sha256 { get; set; }
    public string? PrivacyNotes { get; set; }
    public long? FileSize { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? Error { get; set; }
}

public class VersionInfo
{
    public string Version { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string PrivacyNotes { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime ReleaseDate { get; set; }
}
