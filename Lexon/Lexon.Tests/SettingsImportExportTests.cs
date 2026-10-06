using Lexon.Core.Interfaces;
using Lexon.Profiles;
using Lexon.SettingsModel;
using Lexon.Storage;
using Xunit;

namespace Lexon.Tests;

public class SettingsImportExportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lexon-settings-io-" + Guid.NewGuid().ToString("N"));
    private readonly EncryptedStorage _storage;

    public SettingsImportExportTests()
    {
        Directory.CreateDirectory(_dir);
        _storage = new EncryptedStorage(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    [Fact]
    public async Task Export_WithoutPersonalData_OmitsApiKey()
    {
        var profile = new Profile(_storage) { Id = "export-test" };
        var settings = new AppSettings
        {
            AIProvider = "OpenAI",
            APIKey = "sk-secret-must-not-export",
            AIKeyValidated = true,
            MinimizeToTray = false,
            BlockedApplications = ["putty"],
            AppCategoryOverrides = ["notepad=Casual"],
            CustomTerminology = ["Lexon"],
            AppTerminologyOverrides = ["code.exe|Kube"]
        };
        settings.Write(profile);
        await profile.SaveAsync();

        var io = new SettingsImportExport(_storage, profile);
        var json = await io.ExportSettingsAsync(includePersonalData: false);

        Assert.DoesNotContain("sk-secret-must-not-export", json, StringComparison.Ordinal);
        Assert.Contains("BlockedApplications", json, StringComparison.Ordinal);
        Assert.Contains("AppCategoryOverrides", json, StringComparison.Ordinal);
        Assert.Contains("CustomTerminology", json, StringComparison.Ordinal);
        Assert.Contains("AppTerminologyOverrides", json, StringComparison.Ordinal);
        Assert.Contains("MinimizeToTray", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RoundTrip_RestoresBlockedAppsAndToggles()
    {
        var source = new Profile(_storage) { Id = "source" };
        var settings = new AppSettings
        {
            MinimizeToTray = false,
            LocalMode = true,
            SuggestionSortMode = "Used",
            BlockedApplications = ["chrome", "slack"],
            AppCategoryOverrides = ["code=Code"]
        };
        settings.Write(source);
        await source.SaveAsync();

        var io = new SettingsImportExport(_storage, source);
        var json = await io.ExportSettingsAsync(includePersonalData: false);

        var target = new Profile(_storage) { Id = "target" };
        target.SetSetting(AppSettings.MinimizeToTrayKey, true);
        target.SetSetting(AppSettings.LocalModeKey, false);
        await target.SaveAsync();

        var import = new SettingsImportExport(_storage, target);
        Assert.True(await import.ImportSettingsAsync(json, overwrite: true));

        var loaded = new AppSettings();
        loaded.Read(target);
        Assert.False(loaded.MinimizeToTray);
        Assert.True(loaded.LocalMode);
        Assert.Equal("Used", loaded.SuggestionSortMode);
        Assert.Equal(["chrome", "slack"], loaded.BlockedApplications);
        Assert.Equal(["code=Code"], loaded.AppCategoryOverrides);
    }
}

public class GeneralSettingsBackupTests
{
    [Fact]
    public void ExportSettings_WritesFile_WithoutPersonalData()
    {
        var backup = new FakeBackup { Json = """{"Version":"1.0","Settings":{"MinimizeToTray":false}}""" };
        var files = new FakeFiles { SavePath = Path.Combine(Path.GetTempPath(), "lexon-test-settings.json") };
        var messages = new FakeMessages();
        var vm = new GeneralSettingsViewModel(
            new AppSettings(), new SilentStartup(), new PersistScheduler(() => { }),
            backup, files, messages);
        vm.Load();
        vm.ExportSettings();

        Assert.True(File.Exists(files.SavePath));
        Assert.Equal(backup.Json, File.ReadAllText(files.SavePath!));
        Assert.False(backup.LastIncludePersonal);
        Assert.Contains(GeneralSettingsViewModel.ExportSuccessMessage, messages.Infos.Select(i => i.Text));
        try { File.Delete(files.SavePath!); } catch { /* temp */ }
    }

    [Fact]
    public void ImportSettings_CallsAfterImport_AndReloads()
    {
        var profileSettings = new AppSettings { MinimizeToTray = false };
        var settings = new AppSettings { MinimizeToTray = true };
        var backup = new FakeBackup
        {
            ImportOk = true,
            OnImport = () =>
            {
                settings.MinimizeToTray = false;
            }
        };
        var files = new FakeFiles { OpenPath = "dummy.json", OpenContent = "{}" };
        var after = 0;
        var vm = new GeneralSettingsViewModel(
            settings, new SilentStartup(), new PersistScheduler(() => { }),
            backup, files, new FakeMessages(), () => after++);
        vm.Load();
        Assert.True(vm.MinimizeToTray);

        vm.ImportSettings();

        Assert.Equal(1, after);
        Assert.True(backup.LastOverwrite);
        Assert.False(vm.MinimizeToTray);
        _ = profileSettings;
    }

    [Fact]
    public void ImportSettings_Invalid_DoesNotCallAfterImport()
    {
        var backup = new FakeBackup { ImportOk = false };
        var files = new FakeFiles { OpenPath = "bad.json", OpenContent = "nope" };
        var after = 0;
        var messages = new FakeMessages();
        var vm = new GeneralSettingsViewModel(
            new AppSettings(), new SilentStartup(), new PersistScheduler(() => { }),
            backup, files, messages, () => after++);
        vm.Load();
        vm.ImportSettings();
        Assert.Equal(0, after);
        Assert.Contains(GeneralSettingsViewModel.ImportInvalidMessage, messages.Infos.Select(i => i.Text));
    }

    private sealed class SilentStartup : IStartupRegistration
    {
        public bool IsEnabled() => false;

        public bool TrySetEnabled(bool enabled) => true;
    }

    private sealed class FakeBackup : ISettingsBackupService
    {
        public string Json { get; set; } = "{}";
        public bool ImportOk { get; set; } = true;
        public bool LastIncludePersonal { get; private set; } = true;
        public bool LastOverwrite { get; private set; }
        public Action? OnImport { get; set; }

        public string ExportJson(bool includePersonalData = false)
        {
            LastIncludePersonal = includePersonalData;
            return Json;
        }

        public bool ImportJson(string json, bool overwrite = true)
        {
            LastOverwrite = overwrite;
            if (ImportOk)
            {
                OnImport?.Invoke();
            }

            return ImportOk;
        }
    }

    private sealed class FakeFiles : IFileDialogService
    {
        public string? SavePath { get; set; }
        public string? OpenPath { get; set; }
        public string OpenContent { get; set; } = "{}";

        public string? PickSavePath(string filter, string suggestedName) => SavePath;

        public string? PickOpenPath(string filter)
        {
            if (!string.IsNullOrEmpty(OpenPath) && OpenPath != "dummy.json" && OpenPath != "bad.json")
            {
                return OpenPath;
            }

            if (string.IsNullOrEmpty(OpenPath))
            {
                return null;
            }

            var temp = Path.Combine(Path.GetTempPath(), "lexon-import-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(temp, OpenContent);
            OpenPath = temp;
            return temp;
        }
    }

    private sealed class FakeMessages : IMessageService
    {
        public List<(string Text, string Caption)> Infos { get; } = [];

        public void Info(string text, string caption) => Infos.Add((text, caption));

        public bool Confirm(string text, string caption) => true;
    }
}
