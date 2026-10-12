using Lexon.Core;
using Lexon.Core.Grammar;
using Xunit;

namespace Lexon.Tests;

public class EverydayLexonTests
{
    [Fact]
    public void Spelling_SouthAfricanUsesUkForms()
    {
        Assert.True(SpellingConventions.TryPreferred("color", "US", out var us));
        Assert.Equal("color", us);
        Assert.True(SpellingConventions.TryPreferred("color", "UK", out var uk));
        Assert.Equal("colour", uk);
        Assert.True(SpellingConventions.TryPreferred("organize", "South African", out var sa));
        Assert.Equal("organise", sa);
        Assert.Equal("US", SpellingConventions.NormalizeChoice("nope"));
    }

    [Fact]
    public void QuietHours_WrapsOvernight()
    {
        var start = new TimeOnly(22, 0);
        var end = new TimeOnly(7, 0);
        Assert.True(QuietHours.IsQuiet(new TimeOnly(23, 30), start, end, true));
        Assert.True(QuietHours.IsQuiet(new TimeOnly(6, 0), start, end, true));
        Assert.False(QuietHours.IsQuiet(new TimeOnly(12, 0), start, end, true));
        Assert.False(QuietHours.IsQuiet(new TimeOnly(23, 30), start, end, false));
        var endAt = QuietHours.WindowEnd(new DateTime(2026, 10, 12, 23, 0, 0), start, end);
        Assert.Equal(new DateTime(2026, 10, 13, 7, 0, 0), endAt);
    }

    [Fact]
    public void EditHistory_DisabledDoesNotRecord_AndClearEmpties()
    {
        var log = new EditHistoryLog { Enabled = false };
        log.Record("teh", "the");
        Assert.Empty(log.Entries);
        log.Enabled = true;
        log.Record("teh", "the");
        log.Record("recieve", "receive");
        Assert.Equal("recieve", log.Entries[0].Original);
        Assert.Equal("receive", log.Entries[0].Replacement);
        log.Clear();
        Assert.Empty(log.Entries);
    }

    [Fact]
    public void DiagnosticReport_OmitsTypedText()
    {
        const string typed = "secret draft about the merger";
        var report = LexonStatusReport.Format(new LexonStatusSnapshot
        {
            Version = "1.10.5",
            IsRunning = true,
            IsEnabled = true,
            CurrentProcess = "notepad.exe",
            FocusedWindow = true,
            Spelling = "UK",
            HistoryEnabled = true,
            HistoryCount = 2
        });
        Assert.Contains("notepad.exe", report);
        Assert.Contains("does not include typed text", report);
        Assert.DoesNotContain(typed, report);
        Assert.True(LexonStatusReport.CanInteract(new LexonStatusSnapshot
        {
            IsRunning = true,
            IsEnabled = true,
            FocusedWindow = true
        }));
        Assert.False(LexonStatusReport.CanInteract(new LexonStatusSnapshot
        {
            IsRunning = true,
            IsEnabled = true,
            FocusedWindow = true,
            SecureField = true
        }));
    }
}
