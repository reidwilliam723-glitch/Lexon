using Lexon.Core.Grammar;
using Xunit;

namespace Lexon.Tests;

public class TerminologyListTests
{
    [Fact]
    public void Normalize_TrimsWhitespace()
    {
        Assert.Equal("Lexon", TerminologyList.Normalize("  Lexon  "));
        Assert.Equal(string.Empty, TerminologyList.Normalize("   "));
        Assert.Equal(string.Empty, TerminologyList.Normalize(null));
    }

    [Fact]
    public void IsProtected_MatchesGlobalAndAppCaseInsensitive()
    {
        Assert.True(TerminologyList.IsProtected("lexon", ["Lexon"], null));
        Assert.True(TerminologyList.IsProtected("Kube", null, ["kube"]));
        Assert.False(TerminologyList.IsProtected("teh", ["Lexon"], ["kube"]));
    }

    [Fact]
    public void ParseGlobal_DedupesCaseInsensitive()
    {
        var parsed = TerminologyList.ParseGlobal(["Lexon", "lexon", "  Kube  ", ""]);
        Assert.Equal(["Lexon", "Kube"], parsed);
    }

    [Fact]
    public void FormatAndParseAppRow_RoundTrips()
    {
        var row = TerminologyList.FormatAppRow("Code", ["Lexon", "Kube"]);
        Assert.Equal("code.exe|Lexon|Kube", row);
        Assert.True(TerminologyList.TryParseAppRow(row, out var app, out var terms));
        Assert.Equal("code.exe", app);
        Assert.Equal(["Lexon", "Kube"], terms);
    }

    [Fact]
    public void ResolveTerms_MergesGlobalAndMatchingApp()
    {
        var resolved = TerminologyList.ResolveTerms(
            "Slack.EXE",
            ["Lexon"],
            ["slack.exe|Kube|Helm", "code.exe|Dotnet"]);
        Assert.Contains("Lexon", resolved);
        Assert.Contains("Kube", resolved);
        Assert.Contains("Helm", resolved);
        Assert.DoesNotContain("Dotnet", resolved);
    }

    [Fact]
    public void ParseAppRows_MergesDuplicateApps_KeepingFirstSpelling()
    {
        var map = TerminologyList.ParseAppRows(["code.exe|Foo|Bar", "Code|Baz|foo"]);
        Assert.True(map.TryGetValue("code.exe", out var terms));
        Assert.Equal(["Foo", "Bar", "Baz"], terms);
    }

    [Fact]
    public void FormatAppRow_RejectsTermsThatContainAPipe()
    {
        Assert.Equal(string.Empty, TerminologyList.FormatAppRow("a.exe", ["x|y"]));
        Assert.Equal("a.exe|ok", TerminologyList.FormatAppRow("a.exe", ["ok", "x|y"]));
    }

    [Fact]
    public void ResolveTerms_WithoutAppStillReturnsGlobal()
    {
        var resolved = TerminologyList.ResolveTerms(null, ["Lexon"], ["slack.exe|Kube"]);
        Assert.Equal(new HashSet<string>(["Lexon"], StringComparer.OrdinalIgnoreCase), resolved);
    }
}
