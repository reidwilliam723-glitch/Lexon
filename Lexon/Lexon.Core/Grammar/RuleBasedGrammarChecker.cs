using System.Text.RegularExpressions;

namespace Lexon.Core.Grammar;

/// <summary>
/// Local, deterministic grammar and spelling rules (LanguageTool-style):
/// subject-verb agreement, confused words, punctuation, and common typos.
/// </summary>
public static class RuleBasedGrammarChecker
{
    private static readonly Regex RepeatedWord = Rx(@"\b(\w+)\s+\1\b");
    private static readonly Regex CouldOf = Rx(@"\b(could|would|should|might)\s+of\b");
    private static readonly Regex HeAre = Rx(@"\b(he|she|it)\s+are\b");
    private static readonly Regex HeWere = Rx(@"\b(he|she|it)\s+were\b");
    private static readonly Regex TheyIs = Rx(@"\b(they|we|you)\s+is\b");
    private static readonly Regex TheyWas = Rx(@"\b(they|we|you)\s+was\b");
    private static readonly Regex IIs = Rx(@"\bI\s+is\b");
    private static readonly Regex IAre = Rx(@"\bI\s+are\b");
    private static readonly Regex HeHave = Rx(@"\b(he|she|it)\s+have\b");
    private static readonly Regex HeDont = Rx(@"\b(he|she|it)\s+don't\b");
    private static readonly Regex TheyDoesnt = Rx(@"\b(they|we|you|I)\s+doesn't\b");
    private static readonly Regex ItsA = Rx(@"\bits\s+(a|an|the|been|going|not|time)\b");
    private static readonly Regex ItsOwn = Rx(@"\bit's\s+(own|paws|head|tail|place|way|name|color|colour)\b");
    private static readonly Regex YourA = Rx(@"\byour\s+(a|an|the|going|not|welcome)\b");
    private static readonly Regex YoureName = Rx(@"\byou're\s+(name|house|car|own|email|phone)\b");
    private static readonly Regex TheirIs = Rx(@"\btheir\s+(is|are|was|were)\b");
    private static readonly Regex ThereGoing = Rx(@"\bthere\s+(going|gonna)\b");
    private static readonly Regex TheyreHouse = Rx(@"\bthey're\s+(house|car|own|names?)\b");
    private static readonly Regex WhosHouse = Rx(@"\bwho's\s+(house|car|name|idea)\b");
    private static readonly Regex ThenMe = Rx(@"\bthen\s+(me|him|her|us|them|I)\b");
    private static readonly Regex TooMuch = Rx(@"\bto\s+(much|many|late|soon|bad|good)\b");
    private static readonly Regex UsedTo = Rx(@"\b(use|suppose)\s+to\b");
    private static readonly Regex LetsGo = Rx(@"\blets\s+(go|see|try|get|do|make)\b");
    private static readonly Regex AVowel = Rx(@"\ba\s+([aeiouAEIOU]\w*)\b");
    private static readonly Regex AnConsonant = Rx(@"\ban\s+([bcdfghjklmnpqrstvwxyzBCDFGHJKLMNPQRSTVWXYZ]\w*)\b");
    private static readonly Regex SentenceCap = Rx(@"(?<=[.!?]\s+)[a-z]");
    private static readonly Regex CapitalI = Rx(@"\bi\b");
    private static readonly Regex ExtraSpace = Rx(@"[ \t]{2,}");
    private static readonly Regex SpaceBeforePunct = Rx(@"\s+([,.;:!?])");
    private static readonly Regex SpaceAfterPunct = Rx(@"([,.;:!?])(\S)");
    private static readonly Regex SideAffect = Rx(@"\bside\s+affects?\b");
    private static readonly Regex TakeAffect = Rx(@"\b(take|takes|took|taken)\s+affect\b");
    private static readonly Regex IntoAffect = Rx(@"\binto\s+affect\b");
    private static readonly Regex WillEffect = Rx(@"\b(will|would|could|should|can|may|might)\s+effect\s+(the|a|an|my|your|our|their)\b");
    private static readonly Regex LooseWeight = Rx(@"\bloose\s+(weight|my|your|the|a|an|his|her|our|their)\b");
    private static readonly Regex TooLose = Rx(@"\btoo\s+lose\b");
    private static readonly Regex LoseFit = Rx(@"\blose\s+(fitting|fit|change|change)\b");
    private static readonly Regex AcceptFor = Rx(@"\baccept\s+for\b");
    private static readonly Regex EveryoneAccept = Rx(@"\b(everyone|everybody|anyone|anybody|all)\s+accept\b");
    private static readonly Regex PeekPeak = Rx(@"\bpeek\s+(performance|hours|season|time|demand)\b");
    private static readonly Regex PeakAt = Rx(@"\bpeak\s+(at|inside|around|through|into)\b");
    private static readonly Regex InThePassed = Rx(@"\bin\s+the\s+passed\b");
    private static readonly Regex QuietGood = Rx(@"\bquiet\s+(good|bad|nice|sure|a)\b");
    private static readonly Regex QuiteDown = Rx(@"\bquite\s+down\b");
    private static readonly Regex IllBe = Rx(@"\bill\s+(be|see|go|get|do|make|take|have)\b");
    private static readonly Regex IdLike = Rx(@"\bid\s+(like|love|rather|better)\b");
    private static readonly Regex IntroComma = Rx(@"\b(However|Yes|No|Well|Anyway)\s+(I|we|you|they|he|she|it|the|this|that|my)\b");
    private static readonly Regex PossessiveNoun = Rx(@"\b(cat|dog|child|company|user|customer|client|team|boss|friend)s\s+(food|house|name|car|room|idea|opinion|email|phone|work)\b");
    private static readonly Regex DoubleDot = Rx(@"(?<!\.)\.\.(?!\.)");
    private static readonly Regex QuestionDot = Rx(@"(?<=^|[.!?]\s)(Who|What|Where|When|Why|How)\b[^.?!\n]{2,120}\.");
    private static readonly Regex HyphenCompound = Rx(@"\b(well known|long term|real time|high quality)\s+(?!(for|as|to|that|of|in)\b)(\w+)");

    private static readonly HashSet<string> AnExceptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "hour", "hours", "honest", "honor", "honour", "heir", "heirs", "mba", "fbi", "html", "sos"
    };

    private static readonly HashSet<string> AExceptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "university", "unique", "one", "once", "european", "euro", "user", "uniform", "unicorn", "used", "unit"
    };

    private static readonly (Regex Pattern, string Replacement)[] Contractions =
    [
        (Rx(@"\bdont\b"), "don't"),
        (Rx(@"\bcant\b"), "can't"),
        (Rx(@"\bwont\b"), "won't"),
        (Rx(@"\bisnt\b"), "isn't"),
        (Rx(@"\barent\b"), "aren't"),
        (Rx(@"\bwasnt\b"), "wasn't"),
        (Rx(@"\bwerent\b"), "weren't"),
        (Rx(@"\bdidnt\b"), "didn't"),
        (Rx(@"\bdoesnt\b"), "doesn't"),
        (Rx(@"\bhavent\b"), "haven't"),
        (Rx(@"\bhasnt\b"), "hasn't"),
        (Rx(@"\bhadnt\b"), "hadn't"),
        (Rx(@"\bcouldnt\b"), "couldn't"),
        (Rx(@"\bwouldnt\b"), "wouldn't"),
        (Rx(@"\bshouldnt\b"), "shouldn't"),
        (Rx(@"\byoure\b"), "you're"),
        (Rx(@"\btheyre\b"), "they're"),
        (Rx(@"\bthats\b"), "that's"),
        (Rx(@"\bwhats\b"), "what's"),
        (Rx(@"\bwheres\b"), "where's"),
        (Rx(@"\bim\b"), "I'm"),
        (Rx(@"\bive\b"), "I've"),
        (Rx(@"\byouve\b"), "you've"),
        (Rx(@"\bweve\b"), "we've"),
        (Rx(@"\btheyve\b"), "they've"),
        (Rx(@"\bcouldve\b"), "could've"),
        (Rx(@"\bwouldve\b"), "would've"),
        (Rx(@"\bshouldve\b"), "should've"),
        (Rx(@"\byoull\b"), "you'll"),
        (Rx(@"\btheyll\b"), "they'll"),
        (Rx(@"\bitll\b"), "it'll"),
        (Rx(@"\bthatll\b"), "that'll"),
        (Rx(@"\bheres\b"), "here's"),
        (Rx(@"\bhows\b"), "how's"),
        (Rx(@"\bwhos\b"), "who's")
    ];

    public static IReadOnlyList<GrammarMatch> Find(
        string? text,
        string sensitivity = "Medium",
        Func<string, bool>? skipSpellingWord = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var allowed = AllowedCategories(sensitivity);
        var matches = new List<GrammarMatch>();

        foreach (var (pattern, replacement) in CommonMisspellings.Patterns)
        {
            AddAll(
                matches,
                text,
                pattern,
                replacement,
                "Spelling",
                GrammarRuleCategory.Typo,
                allowed,
                skipSpellingWord);
        }

        foreach (var (pattern, replacement) in Contractions)
        {
            AddAll(matches, text, pattern, replacement, "Missing apostrophe", GrammarRuleCategory.Typo, allowed);
        }

        AddMapped(matches, text, RepeatedWord, m => m.Groups[1].Value, "Repeated word", GrammarRuleCategory.Typo, allowed);
        AddMapped(matches, text, CouldOf, m => m.Groups[1].Value + " have", "Use \"have\", not \"of\"", GrammarRuleCategory.Agreement, allowed);
        AddMapped(matches, text, HeAre, m => m.Groups[1].Value + " is", "Subject-verb agreement", GrammarRuleCategory.Agreement, allowed);
        AddMapped(matches, text, HeWere, m => m.Groups[1].Value + " was", "Subject-verb agreement", GrammarRuleCategory.Agreement, allowed);
        AddMapped(matches, text, TheyIs, m => m.Groups[1].Value + " are", "Subject-verb agreement", GrammarRuleCategory.Agreement, allowed);
        AddMapped(matches, text, TheyWas, m => m.Groups[1].Value + " were", "Subject-verb agreement", GrammarRuleCategory.Agreement, allowed);
        AddAll(matches, text, IIs, "I am", "Subject-verb agreement", GrammarRuleCategory.Agreement, allowed);
        AddAll(matches, text, IAre, "I am", "Subject-verb agreement", GrammarRuleCategory.Agreement, allowed);
        AddMapped(matches, text, HeHave, m => m.Groups[1].Value + " has", "Subject-verb agreement", GrammarRuleCategory.Agreement, allowed);
        AddMapped(matches, text, HeDont, m => m.Groups[1].Value + " doesn't", "Subject-verb agreement", GrammarRuleCategory.Agreement, allowed);
        AddMapped(matches, text, TheyDoesnt, m => m.Groups[1].Value + " don't", "Subject-verb agreement", GrammarRuleCategory.Agreement, allowed);
        AddMapped(matches, text, ItsA, m => "it's " + m.Groups[1].Value, "Use \"it's\" for \"it is\"", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, ItsOwn, m => "its " + m.Groups[1].Value, "Use \"its\" for possession", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, YourA, m => "you're " + m.Groups[1].Value, "Use \"you're\" for \"you are\"", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, YoureName, m => "your " + m.Groups[1].Value, "Use \"your\" for possession", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, TheirIs, m => "there " + m.Groups[1].Value, "Use \"there\" for place/existence", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, ThereGoing, m => "they're " + m.Groups[1].Value, "Use \"they're\" for \"they are\"", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, TheyreHouse, m => "their " + m.Groups[1].Value, "Use \"their\" for possession", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, WhosHouse, m => "whose " + m.Groups[1].Value, "Use \"whose\" for possession", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, ThenMe, m => "than " + m.Groups[1].Value, "Use \"than\" for comparison", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, TooMuch, m => "too " + m.Groups[1].Value, "Use \"too\" for degree", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, UsedTo, m => m.Groups[1].Value + "d to", "Missing -d on \"used/supposed to\"", GrammarRuleCategory.Agreement, allowed);
        AddMapped(matches, text, LetsGo, m => "let's " + m.Groups[1].Value, "Use \"let's\" for \"let us\"", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, SideAffect, SideAffectFix, "Use \"effect\" for the result", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, TakeAffect, m => m.Groups[1].Value + " effect", "Use \"effect\" for the result", GrammarRuleCategory.ConfusedWord, allowed);
        AddAll(matches, text, IntoAffect, "into effect", "Use \"effect\" for the result", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, WillEffect, m => m.Groups[1].Value + " affect " + m.Groups[2].Value, "Use \"affect\" for the verb", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, LooseWeight, m => "lose " + m.Groups[1].Value, "Use \"lose\" for misplacing", GrammarRuleCategory.ConfusedWord, allowed);
        AddAll(matches, text, TooLose, "too loose", "Use \"loose\" for not tight", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, LoseFit, m => "loose " + m.Groups[1].Value, "Use \"loose\" for not tight", GrammarRuleCategory.ConfusedWord, allowed);
        AddAll(matches, text, AcceptFor, "except for", "Use \"except\" for exclusion", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, EveryoneAccept, m => m.Groups[1].Value + " except", "Use \"except\" for exclusion", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, PeekPeak, m => "peak " + m.Groups[1].Value, "Use \"peak\" for a high point", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, PeakAt, m => "peek " + m.Groups[1].Value, "Use \"peek\" for a glance", GrammarRuleCategory.ConfusedWord, allowed);
        AddAll(matches, text, InThePassed, "in the past", "Use \"past\" for time", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, QuietGood, m => "quite " + m.Groups[1].Value, "Use \"quite\" for degree", GrammarRuleCategory.ConfusedWord, allowed);
        AddAll(matches, text, QuiteDown, "quiet down", "Use \"quiet\" for sound", GrammarRuleCategory.ConfusedWord, allowed);
        AddMapped(matches, text, IllBe, m => "I'll " + m.Groups[1].Value, "Use \"I'll\" for \"I will\"", GrammarRuleCategory.Typo, allowed);
        AddMapped(matches, text, IdLike, m => "I'd " + m.Groups[1].Value, "Use \"I'd\" for \"I would\"", GrammarRuleCategory.Typo, allowed);
        AddMapped(matches, text, IntroComma, m => m.Groups[1].Value + ", " + m.Groups[2].Value, "Add a comma after the intro word", GrammarRuleCategory.Punctuation, allowed);
        AddMapped(matches, text, PossessiveNoun, PossessiveFix, "Use a possessive apostrophe", GrammarRuleCategory.Punctuation, allowed);
        AddAll(matches, text, DoubleDot, "...", "Use an ellipsis", GrammarRuleCategory.Punctuation, allowed);
        AddMapped(matches, text, QuestionDot, m => m.Value[..^1] + "?", "Use a question mark", GrammarRuleCategory.Punctuation, allowed);
        AddMapped(matches, text, HyphenCompound, HyphenFix, "Hyphenate this compound", GrammarRuleCategory.Punctuation, allowed);
        AddMapped(matches, text, AVowel, AToAn, "Use \"an\" before a vowel sound", GrammarRuleCategory.Agreement, allowed);
        AddMapped(matches, text, AnConsonant, AnToA, "Use \"a\" before a consonant sound", GrammarRuleCategory.Agreement, allowed);
        AddMapped(matches, text, SentenceCap, m => m.Value.ToUpperInvariant(), "Capitalize the start of a sentence", GrammarRuleCategory.Punctuation, allowed);
        AddAll(matches, text, CapitalI, "I", "Capitalize \"I\"", GrammarRuleCategory.Punctuation, allowed);
        AddAll(matches, text, ExtraSpace, " ", "Extra space", GrammarRuleCategory.Punctuation, allowed);
        AddMapped(matches, text, SpaceBeforePunct, m => m.Groups[1].Value, "Remove space before punctuation", GrammarRuleCategory.Punctuation, allowed);
        AddSpaceAfterPunct(matches, text, allowed);
        if (allowed.Contains(GrammarRuleCategory.Punctuation))
        {
            foreach (var glued in GluedWordSplitter.Find(text))
            {
                TryAdd(matches, glued);
            }
        }

        return matches
            .OrderBy(m => m.Start)
            .ThenBy(m => m.Length)
            .ToList();
    }

    public static string Apply(string text, GrammarMatch match)
    {
        if (string.IsNullOrEmpty(text)
            || match.Start < 0
            || match.Start + match.Length > text.Length)
        {
            return text;
        }

        return text.Remove(match.Start, match.Length).Insert(match.Start, match.Replacement);
    }

    private static HashSet<GrammarRuleCategory> AllowedCategories(string sensitivity)
        => sensitivity switch
        {
            "Low" => [GrammarRuleCategory.Typo, GrammarRuleCategory.Agreement],
            "High" =>
            [
                GrammarRuleCategory.Typo,
                GrammarRuleCategory.Agreement,
                GrammarRuleCategory.ConfusedWord,
                GrammarRuleCategory.Punctuation
            ],
            _ =>
            [
                GrammarRuleCategory.Typo,
                GrammarRuleCategory.Agreement,
                GrammarRuleCategory.ConfusedWord,
                GrammarRuleCategory.Punctuation
            ]
        };

    private static void AddSpaceAfterPunct(
        List<GrammarMatch> matches,
        string text,
        HashSet<GrammarRuleCategory> allowed)
    {
        if (!allowed.Contains(GrammarRuleCategory.Punctuation))
        {
            return;
        }

        foreach (Match match in SpaceAfterPunct.Matches(text))
        {
            if (SpacingNormalizer.ShouldSkipSpaceAfter(text, match.Index))
            {
                continue;
            }

            TryAdd(matches, new GrammarMatch(
                match.Index,
                match.Length,
                match.Value,
                match.Groups[1].Value + " " + match.Groups[2].Value,
                "Add a space after punctuation",
                GrammarRuleCategory.Punctuation));
        }
    }

    private static void AddAll(
        List<GrammarMatch> matches,
        string text,
        Regex pattern,
        string replacement,
        string message,
        GrammarRuleCategory category,
        HashSet<GrammarRuleCategory> allowed,
        Func<string, bool>? skipWord = null)
    {
        if (!allowed.Contains(category))
        {
            return;
        }

        foreach (Match match in pattern.Matches(text))
        {
            if (skipWord?.Invoke(match.Value) == true)
            {
                continue;
            }

            TryAdd(matches, new GrammarMatch(
                match.Index,
                match.Length,
                match.Value,
                PreserveCase(match.Value, replacement),
                message,
                category));
        }
    }

    private static void AddMapped(
        List<GrammarMatch> matches,
        string text,
        Regex pattern,
        Func<Match, string> replacement,
        string message,
        GrammarRuleCategory category,
        HashSet<GrammarRuleCategory> allowed)
    {
        if (!allowed.Contains(category))
        {
            return;
        }

        foreach (Match match in pattern.Matches(text))
        {
            var next = replacement(match);
            TryAdd(matches, new GrammarMatch(
                match.Index,
                match.Length,
                match.Value,
                PreserveCase(match.Value, next),
                message,
                category));
        }
    }

    private static void TryAdd(List<GrammarMatch> matches, GrammarMatch candidate)
    {
        if (string.IsNullOrEmpty(candidate.Replacement)
            || string.Equals(candidate.Original, candidate.Replacement, StringComparison.Ordinal))
        {
            return;
        }

        if (matches.Any(existing => Overlaps(existing, candidate)))
        {
            return;
        }

        matches.Add(candidate);
    }

    private static bool Overlaps(GrammarMatch left, GrammarMatch right)
        => left.Start < right.Start + right.Length && right.Start < left.Start + left.Length;

    private static string PreserveCase(string original, string replacement)
    {
        if (replacement.Length == 0)
        {
            return replacement;
        }

        if (original.Length > 0 && char.IsUpper(original[0]))
        {
            return char.ToUpperInvariant(replacement[0]) + replacement[1..];
        }

        return replacement;
    }

    private static string AToAn(Match match)
    {
        var noun = match.Groups[1].Value;
        return AExceptions.Contains(noun) ? match.Value : "an " + noun;
    }

    private static string AnToA(Match match)
    {
        var noun = match.Groups[1].Value;
        return AnExceptions.Contains(noun) ? match.Value : "a " + noun;
    }

    private static string SideAffectFix(Match match)
        => match.Value.Contains("affects", StringComparison.OrdinalIgnoreCase) ? "side effects" : "side effect";

    private static string PossessiveFix(Match match)
        => match.Groups[1].Value + "'s " + match.Groups[2].Value;

    private static string HyphenFix(Match match)
        => match.Groups[1].Value.Replace(' ', '-') + " " + match.Groups[3].Value;

    private static Regex Rx(string pattern)
        => new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
