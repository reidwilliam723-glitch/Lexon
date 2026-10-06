using Lexon.Profiles;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class WritingViewModelTests
{
    [Fact]
    public void Load_MapsGrammarDefaultsAndOwnedKeys()
    {
        var settings = new AppSettings();
        var persist = CountingPersist(out var flushes);
        var vm = new WritingViewModel(settings, persist);
        vm.Load();

        Assert.True(vm.GrammarChecking);
        Assert.True(vm.AutoCorrectTypos);
        Assert.Equal(1, vm.SensitivityIndex);
        Assert.False(vm.MuteGrammarForCasualApps);
        Assert.Equal(string.Empty, vm.MutedAppsText);
        Assert.True(vm.EnableRewriteHotkey);
        Assert.True(vm.EnableGrammarHotkey);
        Assert.False(vm.IsDirty);
        Assert.Equal(0, flushes.Value);
        Assert.Equal(
            new[]
            {
                AppSettings.GrammarCheckingKey,
                AppSettings.AutoCorrectTyposKey,
                AppSettings.DocumentConsistencyCheckingKey,
                AppSettings.AllowCodeSwitchingKey,
                AppSettings.GrammarSensitivityKey,
                AppSettings.MuteGrammarForCasualAppsKey,
                AppSettings.GrammarMutedAppsKey,
                AppSettings.EnableRewriteHotkeyKey,
                AppSettings.EnableGrammarHotkeyKey,
                AppSettings.CustomTerminologyKey,
                AppSettings.AppTerminologyOverridesKey
            },
            WritingViewModel.OwnedKeyList);
        Assert.True(vm.DocumentConsistencyChecking);
        Assert.True(vm.AllowCodeSwitching);
    }

    [Theory]
    [InlineData("High", 2)]
    [InlineData("low", 0)]
    [InlineData("Nope", 1)]
    [InlineData("", 1)]
    public void Load_UnknownSensitivityFallsBackToMedium(string stored, int index)
    {
        var settings = new AppSettings { GrammarSensitivity = stored };
        var vm = new WritingViewModel(settings, CountingPersist(out _));
        vm.Load();
        Assert.Equal(index, vm.SensitivityIndex);
    }

    [Fact]
    public void Edits_UpdateAppSettingsAndMarkDirty_NotDuringLoad()
    {
        var settings = new AppSettings();
        var persist = CountingPersist(out var flushes);
        var vm = new WritingViewModel(settings, persist);
        vm.Load();
        Assert.Equal(0, flushes.Value);

        vm.GrammarChecking = false;
        vm.AutoCorrectTypos = false;
        vm.SensitivityIndex = 2;
        vm.MuteGrammarForCasualApps = true;
        vm.MutedAppsText = "Discord.exe, Slack.exe";
        vm.EnableRewriteHotkey = false;
        vm.EnableGrammarHotkey = false;

        Assert.False(settings.GrammarChecking);
        Assert.False(settings.AutoCorrectTypos);
        Assert.Equal("High", settings.GrammarSensitivity);
        Assert.True(settings.MuteGrammarForCasualApps);
        Assert.Equal(new[] { "Discord.exe", "Slack.exe" }, settings.GrammarMutedApps);
        Assert.False(settings.EnableRewriteHotkey);
        Assert.False(settings.EnableGrammarHotkey);
        Assert.True(vm.IsDirty);
        persist.Flush();
        Assert.Equal(1, flushes.Value);
    }

    [Fact]
    public void MutedApps_TrimOnly_DoesNotNormalise()
    {
        var settings = new AppSettings();
        var vm = new WritingViewModel(settings, CountingPersist(out _));
        vm.Load();
        vm.MutedAppsText = " Outlook.EXE , discord ";
        Assert.Equal(new[] { "Outlook.EXE", "discord" }, settings.GrammarMutedApps);
    }

    [Fact]
    public void MutedApps_ReloadKeepsTextWhenParsedListUnchanged()
    {
        var settings = new AppSettings { GrammarMutedApps = ["Outlook.EXE"] };
        var vm = new WritingViewModel(settings, CountingPersist(out _));
        vm.Load();
        const string odd = "Outlook.EXE,";
        vm.MutedAppsText = odd;
        settings.GrammarMutedApps = BlockedAppList.ParseMutedGrammar(odd);
        vm.MarkClean();
        vm.Load();
        Assert.Equal(odd, vm.MutedAppsText);
    }

    [Fact]
    public void Flush_WritesOnlySevenOwnedKeys()
    {
        var profile = new SpyingProfile();
        var settings = new AppSettings { AIModel = "keep" };
        settings.Write(profile);
        profile.Written.Clear();

        WritingViewModel vm = null!;
        var persist = new PersistScheduler(() => OwnedSettingsWriter.Flush(profile, vm));
        vm = new WritingViewModel(settings, persist);
        vm.Load();
        vm.GrammarChecking = false;
        persist.Flush();

        Assert.Equal(
            WritingViewModel.OwnedKeyList.OrderBy(k => k).ToArray(),
            profile.Written.Distinct().OrderBy(k => k).ToArray());
        Assert.Equal("keep", profile.GetSetting(AppSettings.AIModelKey, ""));
    }

    [Fact]
    public void RoundTrip_AllOwnedKeys()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Read(profile);
        WritingViewModel vm = null!;
        var persist = new PersistScheduler(() => OwnedSettingsWriter.Flush(profile, vm));
        vm = new WritingViewModel(settings, persist);
        vm.Load();
        vm.GrammarChecking = false;
        vm.AutoCorrectTypos = false;
        vm.SensitivityIndex = 0;
        vm.MuteGrammarForCasualApps = true;
        vm.MutedAppsText = "chat.exe";
        vm.EnableRewriteHotkey = false;
        vm.EnableGrammarHotkey = false;
        persist.Flush();

        var loaded = new AppSettings();
        loaded.Read(profile);
        Assert.False(loaded.GrammarChecking);
        Assert.False(loaded.AutoCorrectTypos);
        Assert.Equal("Low", loaded.GrammarSensitivity);
        Assert.True(loaded.MuteGrammarForCasualApps);
        Assert.Equal(new[] { "chat.exe" }, loaded.GrammarMutedApps);
        Assert.False(loaded.EnableRewriteHotkey);
        Assert.False(loaded.EnableGrammarHotkey);
    }

    [Fact]
    public void ShowTerminology_UpdatesSettingsAndOwnedKeys()
    {
        var profile = new SpyingProfile();
        var settings = new AppSettings();
        WritingViewModel vm = null!;
        var persist = new PersistScheduler(() => OwnedSettingsWriter.Flush(profile, vm));
        var dialogs = new ApplyingTerminologyDialogs();
        vm = new WritingViewModel(settings, persist, dialogs: dialogs);
        vm.Load();
        Assert.False(vm.IsDirty);

        vm.ShowTerminology();

        Assert.Equal(["Lexon"], settings.CustomTerminology);
        Assert.Equal(["code.exe|Kube"], settings.AppTerminologyOverrides);
        Assert.True(vm.IsDirty);
        persist.Flush();
        Assert.Contains(AppSettings.CustomTerminologyKey, profile.Written);
        Assert.Contains(AppSettings.AppTerminologyOverridesKey, profile.Written);
        Assert.Equal(["Lexon"], profile.GetSetting<List<string>>(AppSettings.CustomTerminologyKey, []));
        Assert.Equal(["code.exe|Kube"], profile.GetSetting<List<string>>(AppSettings.AppTerminologyOverridesKey, []));
    }

    [Fact]
    public void Export_WritesFileAndHonoursCancel()
    {
        var personalization = new FakePersonalization { ExportJson = "{\"ok\":true}" };
        var files = new FakeFiles();
        var messages = new FakeMessages();
        var vm = new WritingViewModel(new AppSettings(), CountingPersist(out _), personalization, files: files, messages: messages);
        vm.Load();

        files.SavePath = null;
        vm.ExportLearning();
        Assert.Empty(files.Written);

        var path = Path.Combine(Path.GetTempPath(), "lexon-learn-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            files.SavePath = path;
            vm.ExportLearning();
            Assert.Equal("{\"ok\":true}", File.ReadAllText(path));
            Assert.Empty(messages.Infos);
        }
        finally
        {
            try { File.Delete(path); } catch { /* temp */ }
        }
    }

    [Fact]
    public void Import_SuccessRefreshesAndShowsMessage()
    {
        var personalization = new FakePersonalization
        {
            ImportResult = true,
            Style = "after",
            Adaptations = [new AdaptationItem { Id = "1", DisplayText = "adj" }]
        };
        var files = new FakeFiles();
        var messages = new FakeMessages();
        var path = Path.Combine(Path.GetTempPath(), "lexon-import-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{}");
        files.OpenPath = path;
        try
        {
            var vm = new WritingViewModel(new AppSettings(), CountingPersist(out _), personalization, files: files, messages: messages);
            vm.Load();
            personalization.Style = "imported";
            vm.ImportLearning();
            Assert.Equal("imported", vm.StyleSummary);
            Assert.Single(vm.Adaptations);
            Assert.Contains(messages.Infos, m => m.Text == WritingViewModel.ImportSuccessMessage && m.Caption == "Import");
        }
        finally
        {
            try { File.Delete(path); } catch { /* temp */ }
        }
    }

    [Fact]
    public void Import_InvalidShowsErrorAndChangesNothing()
    {
        var personalization = new FakePersonalization
        {
            ImportResult = false,
            Style = "before",
            Adaptations = [new AdaptationItem { Id = "1", DisplayText = "keep" }]
        };
        var files = new FakeFiles();
        var messages = new FakeMessages();
        var path = Path.Combine(Path.GetTempPath(), "lexon-bad-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{not}");
        files.OpenPath = path;
        try
        {
            var vm = new WritingViewModel(new AppSettings(), CountingPersist(out _), personalization, files: files, messages: messages);
            vm.Load();
            vm.ImportLearning();
            Assert.Equal("before", vm.StyleSummary);
            Assert.Single(vm.Adaptations);
            Assert.Contains(messages.Infos, m => m.Text == WritingViewModel.ImportInvalidMessage);
            Assert.Equal(1, personalization.ImportCalls);
        }
        finally
        {
            try { File.Delete(path); } catch { /* temp */ }
        }
    }

    [Fact]
    public void Reset_NeedsYes()
    {
        var personalization = new FakePersonalization { Style = "styled" };
        var messages = new FakeMessages { ConfirmResult = false };
        var vm = new WritingViewModel(new AppSettings(), CountingPersist(out _), personalization, messages: messages);
        vm.Load();
        vm.ResetWritingStyle();
        Assert.Equal(0, personalization.ResetCalls);

        messages.ConfirmResult = true;
        vm.ResetWritingStyle();
        Assert.Equal(1, personalization.ResetCalls);
    }

    [Fact]
    public void Undo_CallsSelectedId()
    {
        var personalization = new FakePersonalization
        {
            Adaptations =
            [
                new AdaptationItem { Id = "abc", DisplayText = "one" },
                new AdaptationItem { Id = "def", DisplayText = "two" }
            ]
        };
        var vm = new WritingViewModel(new AppSettings(), CountingPersist(out _), personalization);
        vm.Load();
        vm.SelectedAdaptationIndex = 1;
        vm.UndoSelectedAdaptation();
        Assert.Equal(["def"], personalization.UndoneIds);
    }

    [Fact]
    public void NullPersonalization_ShowsUnavailableAndMessages()
    {
        var messages = new FakeMessages();
        var files = new FakeFiles { SavePath = "x", OpenPath = "y" };
        var vm = new WritingViewModel(new AppSettings(), CountingPersist(out _), null, files: files, messages: messages);
        vm.Load();
        Assert.Equal(WritingViewModel.UnavailableStyleSummary, vm.StyleSummary);
        Assert.Empty(vm.Adaptations);
        vm.ExportLearning();
        vm.ImportLearning();
        vm.ResetWritingStyle();
        vm.UndoSelectedAdaptation();
        Assert.Contains(messages.Infos, m => m.Caption == "Export");
        Assert.Contains(messages.Infos, m => m.Caption == "Import");
        Assert.Equal(0, messages.Confirms);
    }

    [Fact]
    public void ExportImport_IoErrorsShowMessageNotException()
    {
        var personalization = new FakePersonalization { ExportJson = "data" };
        var files = new FakeFiles();
        var messages = new FakeMessages();
        var vm = new WritingViewModel(new AppSettings(), CountingPersist(out _), personalization, files: files, messages: messages);
        vm.Load();

        var dir = Path.Combine(Path.GetTempPath(), "lexon-io-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            files.SavePath = dir; // WriteAllText on a directory throws
            vm.ExportLearning();
            Assert.Contains(messages.Infos, m => m.Text == WritingViewModel.SaveFailedMessage);

            files.OpenPath = Path.Combine(dir, "missing.json");
            vm.ImportLearning();
            Assert.Contains(messages.Infos, m => m.Text == WritingViewModel.ReadFailedMessage);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* temp */ }
        }
    }

    private static PersistScheduler CountingPersist(out Holder flushes)
    {
        var holder = new Holder();
        flushes = holder;
        return new PersistScheduler(() => holder.Value++);
    }

    private sealed class Holder
    {
        public int Value;
    }

    private sealed class SpyingProfile : Profile
    {
        public List<string> Written { get; } = [];

        public override void SetSetting<T>(string key, T value)
        {
            Written.Add(key);
            base.SetSetting(key, value);
        }
    }

    private sealed class ApplyingTerminologyDialogs : IWritingDialogs
    {
        public void ShowWritingStats()
        {
        }

        public void ShowLearnedWords()
        {
        }

        public void ShowTerminology(
            IReadOnlyList<string> globalTerms,
            IReadOnlyList<string> appOverrideRows,
            Action<IReadOnlyList<string>, IReadOnlyList<string>> onApply)
        {
            onApply(["Lexon"], ["code.exe|Kube"]);
        }
    }

    private sealed class FakePersonalization : IPersonalizationService
    {
        public string Style { get; set; } = "summary";
        public string ExportJson { get; set; } = "{}";
        public bool ImportResult { get; set; } = true;
        public List<AdaptationItem> Adaptations { get; set; } = [];
        public List<string> UndoneIds { get; } = [];
        public int ResetCalls { get; private set; }
        public int ImportCalls { get; private set; }

        public string GetStyleSummary() => Style;

        public IReadOnlyList<AdaptationItem> GetAdaptations() => Adaptations;

        public void UndoAdaptation(string id) => UndoneIds.Add(id);

        public void ResetWritingStyle()
        {
            ResetCalls++;
            Style = "cleared";
            Adaptations = [];
        }

        public string ExportLearningData() => ExportJson;

        public bool ImportLearningData(string json)
        {
            ImportCalls++;
            return ImportResult;
        }
    }

    private sealed class FakeFiles : IFileDialogService
    {
        public string? SavePath { get; set; }
        public string? OpenPath { get; set; }
        public List<string> Written { get; } = [];

        public string? PickSavePath(string filter, string suggestedName) => SavePath;

        public string? PickOpenPath(string filter) => OpenPath;
    }

    private sealed class FakeMessages : IMessageService
    {
        public bool ConfirmResult { get; set; } = true;
        public List<(string Text, string Caption)> Infos { get; } = [];
        public int Confirms { get; private set; }

        public void Info(string text, string caption) => Infos.Add((text, caption));

        public bool Confirm(string text, string caption)
        {
            Confirms++;
            return ConfirmResult;
        }
    }
}
