using Lexon.Core.Models;
using Lexon.Profiles;
using Lexon.SettingsModel;
using Xunit;

namespace Lexon.Tests;

public class AppToneViewModelTests
{
    [Fact]
    public void Load_CopiesNonBlankRows()
    {
        var settings = new AppSettings
        {
            AppCategoryOverrides = ["slack.exe|Casual", "  ", "code.exe|Code"]
        };
        var persist = CountingPersist(out var flushes);
        var vm = new AppToneViewModel(settings, persist, new FakePicker());

        vm.Load();

        Assert.Equal(new[] { "slack.exe|Casual", "code.exe|Code" }, vm.Rows.ToArray());
        Assert.Equal(-1, vm.SelectedRowIndex);
        Assert.Equal(0, vm.SelectedToneIndex);
        Assert.False(vm.IsDirty);
        Assert.False(persist.HasPending);
        Assert.Equal(0, flushes.Value);
    }

    [Fact]
    public void AddRunningApp_ReplacesExistingRowForSameApp()
    {
        var settings = new AppSettings
        {
            AppCategoryOverrides = ["notepad.exe|Casual"]
        };
        var picker = new FakePicker { Result = "Notepad.exe" };
        var persist = CountingPersist(out _);
        var vm = new AppToneViewModel(settings, persist, picker);
        vm.Load();
        vm.SelectedToneIndex = 2; // Code

        vm.AddRunningApp();

        Assert.Equal(
            new[] { AppCategoryMapper.FormatRow("Notepad.exe", AppWritingCategory.Code) },
            vm.Rows.ToArray());
        Assert.True(vm.IsDirty);
        Assert.Equal(vm.Rows.ToList(), settings.AppCategoryOverrides);
    }

    [Fact]
    public void AddRunningApp_ToneLabelsAlwaysParse_CancelledPickerDoesNothing()
    {
        foreach (var label in AppToneViewModel.ToneLabels)
        {
            Assert.True(Enum.TryParse<AppWritingCategory>(label, true, out _));
        }

        var settings = new AppSettings { AppCategoryOverrides = ["a.exe|Casual"] };
        var picker = new FakePicker { Result = null };
        var vm = new AppToneViewModel(settings, CountingPersist(out _), picker);
        vm.Load();
        vm.AddRunningApp();
        Assert.Equal(new[] { "a.exe|Casual" }, vm.Rows.ToArray());
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void AddRunningApp_CancelledPickerDoesNothing()
    {
        var settings = new AppSettings { AppCategoryOverrides = ["a.exe|Casual"] };
        var picker = new FakePicker { Result = "" };
        var vm = new AppToneViewModel(settings, CountingPersist(out _), picker);
        vm.Load();
        vm.AddRunningApp();
        Assert.Equal(new[] { "a.exe|Casual" }, vm.Rows.ToArray());
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void RemoveSelected_WithAndWithoutSelection()
    {
        var settings = new AppSettings
        {
            AppCategoryOverrides = ["a.exe|Casual", "b.exe|Code"]
        };
        var persist = CountingPersist(out var flushes);
        var vm = new AppToneViewModel(settings, persist, new FakePicker());
        vm.Load();

        vm.RemoveSelected();
        Assert.Equal(2, vm.Rows.Count);
        Assert.False(vm.IsDirty);

        vm.SelectedRowIndex = 0;
        Assert.True(vm.CanRemove);
        vm.RemoveSelected();
        Assert.Equal(new[] { "b.exe|Code" }, vm.Rows.ToArray());
        Assert.True(vm.IsDirty);
        Assert.Equal(-1, vm.SelectedRowIndex);
        Assert.False(vm.CanRemove);
        persist.Flush();
        Assert.Equal(1, flushes.Value);
    }

    [Fact]
    public void Flush_WritesOnlyAppCategoryOverrides_AndSkipsBlankRows()
    {
        var profile = new SpyingProfile();
        var settings = new AppSettings { AIModel = "keep", AppCategoryOverrides = ["x.exe|Casual"] };
        settings.Write(profile);
        profile.Written.Clear();

        AppToneViewModel vm = null!;
        var persist = new PersistScheduler(() => OwnedSettingsWriter.Flush(profile, vm));
        vm = new AppToneViewModel(settings, persist, new FakePicker { Result = "y.exe" });
        vm.Load();
        vm.SelectedToneIndex = 1; // Formal
        vm.AddRunningApp();
        persist.Flush();

        Assert.Equal(new[] { AppSettings.AppCategoryOverridesKey }, profile.Written.Distinct().ToArray());
        var loaded = new AppSettings();
        loaded.Read(profile);
        Assert.Equal("keep", loaded.AIModel);
        Assert.DoesNotContain(loaded.AppCategoryOverrides, r => string.IsNullOrWhiteSpace(r));
        Assert.Contains(loaded.AppCategoryOverrides, r => r.Contains("y.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RoundTrip_OwnedKey()
    {
        var profile = new Profile();
        var settings = new AppSettings();
        settings.Read(profile);
        AppToneViewModel vm = null!;
        var persist = new PersistScheduler(() => OwnedSettingsWriter.Flush(profile, vm));
        vm = new AppToneViewModel(settings, persist, new FakePicker { Result = "chrome.exe" });
        vm.Load();
        vm.AddRunningApp();
        persist.Flush();

        var loaded = new AppSettings();
        loaded.Read(profile);
        Assert.Equal(settings.AppCategoryOverrides, loaded.AppCategoryOverrides);
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

    private sealed class FakePicker : IProcessPicker
    {
        public string? Result { get; set; }

        public string? Pick(string prompt) => Result;
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
}
