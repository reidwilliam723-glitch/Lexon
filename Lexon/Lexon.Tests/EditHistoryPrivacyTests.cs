using Lexon.Core;
using Lexon.Input;
using Lexon.Input.Interfaces;
using Lexon.Profiles;
using Lexon.Settings;
using Lexon.SettingsModel;
using Lexon.Storage;
using Moq;
using Xunit;

namespace Lexon.Tests;

public class EditHistoryPrivacyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lexon-history-" + Guid.NewGuid().ToString("N"));

    public EditHistoryPrivacyTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    [Fact]
    public void Record_TruncatesEachSideTo200Characters()
    {
        var log = new EditHistoryLog();
        var original = new string('a', 500);
        var replacement = new string('b', 500);

        log.Record(original, replacement, DateTime.UtcNow);

        Assert.Equal(200, log.Entries[0].Original.Length);
        Assert.Equal(200, log.Entries[0].Replacement.Length);
        Assert.Equal(original[..199] + "…", log.Entries[0].Original);
        Assert.Equal(replacement[..199] + "…", log.Entries[0].Replacement);
        Assert.Equal(EditHistoryLog.MaxEntries, 20);
    }

    [Fact]
    public void LongEdit_UndoRestoresFullText_AndMatchesHistoryTimestamp()
    {
        var injector = new Mock<ITextInjector>();
        var undo = new UndoManager(injector.Object);
        var history = new EditHistoryLog();
        var original = new string('o', 400);
        var replacement = new string('n', 400);

        undo.RecordOperation(original, replacement);
        var operation = undo.PeekUndo();
        Assert.NotNull(operation);
        history.Record(operation.DeletedText, operation.InsertedText, operation.Timestamp);

        Assert.Equal(operation.Timestamp, history.Entries[0].Utc);
        Assert.Equal(200, history.Entries[0].Original.Length);
        undo.Undo();

        injector.Verify(item => item.DeleteBackward(replacement.Length), Times.Once);
        injector.Verify(item => item.InjectText(original), Times.Once);
    }

    [Fact]
    public void Load_TruncatesExistingLongEntries_AndRewritesTheFile()
    {
        var path = Path.Combine(_dir, "edit-history.json");
        var longText = new string('x', 350);
        File.WriteAllText(path, "[{\"Utc\":\"2026-10-12T12:00:00Z\",\"Original\":\"" + longText + "\",\"Replacement\":\"short\"}]");

        var history = new LocalEditHistory(path);

        Assert.Equal(200, history.Entries[0].Original.Length);
        Assert.EndsWith("…", history.Entries[0].Original, StringComparison.Ordinal);
        var rewritten = File.ReadAllText(path);
        Assert.DoesNotContain(longText, rewritten, StringComparison.Ordinal);
        var again = new LocalEditHistory(path);
        Assert.Equal(history.Entries[0].Original, again.Entries[0].Original);
    }

    [Fact]
    public void Clear_WritesAnEmptyList()
    {
        var path = Path.Combine(_dir, "edit-history.json");
        var history = new LocalEditHistory(path);
        history.Record("before", "after", DateTime.UtcNow);

        history.Clear();

        Assert.Empty(history.Entries);
        Assert.Equal("[]", File.ReadAllText(path).Trim());
    }

    [Fact]
    public void TurningOff_DeletesStoredEntries()
    {
        var path = Path.Combine(_dir, "edit-history.json");
        var history = new LocalEditHistory(path);
        history.Record("secret original", "secret replacement", DateTime.UtcNow);

        history.Enabled = false;

        Assert.Empty(history.Entries);
        var body = File.ReadAllText(path);
        Assert.DoesNotContain("secret original", body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret replacement", body, StringComparison.Ordinal);
        Assert.Equal("[]", body.Trim());
    }

    [Fact]
    public async Task Export_OmitsEditHistoryFile_WithAndWithoutPersonalData()
    {
        var storage = new EncryptedStorage(_dir);
        var profile = new Profile(storage) { Id = "export-history" };
        new AppSettings { APIKey = "sk-personal" }.Write(profile);
        await profile.SaveAsync();
        var sentinel = "EDIT-HISTORY-SENTINEL-not-in-backup";
        File.WriteAllText(
            Path.Combine(_dir, "edit-history.json"),
            "[{\"Original\":\"" + sentinel + "\",\"Replacement\":\"rewritten paragraph\"}]");

        var io = new SettingsImportExport(storage, profile);
        var without = await io.ExportSettingsAsync(includePersonalData: false);
        var withPersonal = await io.ExportSettingsAsync(includePersonalData: true);

        Assert.DoesNotContain(sentinel, without, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, withPersonal, StringComparison.Ordinal);
        Assert.DoesNotContain("edit-history.json", without, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("edit-history.json", withPersonal, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk-personal", without, StringComparison.Ordinal);
        Assert.Contains("sk-personal", withPersonal, StringComparison.Ordinal);
    }
}
