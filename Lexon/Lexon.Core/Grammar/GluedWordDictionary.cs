namespace Lexon.Core.Grammar;

internal static class GluedWordDictionary
{
    private static readonly HashSet<string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "above", "after", "again", "against", "all", "also", "always",
        "and", "another", "any", "around", "back", "because", "been", "before",
        "being", "below", "between", "both", "but", "came", "can", "cannot",
        "cat", "come", "could", "day", "did", "does", "done", "down", "each",
        "even", "every", "first", "for", "from", "get", "give", "go", "going",
        "good", "got", "had", "has", "have", "help", "her", "here", "him",
        "his", "home", "how", "into", "just", "keep", "know", "last", "left",
        "let", "like", "little", "long", "look", "made", "make", "man", "many",
        "may", "more", "most", "much", "must", "name", "need", "never", "new",
        "next", "not", "now", "off", "old", "once", "one", "only", "other",
        "our", "out", "over", "own", "part", "people", "place", "put", "right",
        "said", "same", "see", "seem", "she", "should", "some", "still", "such",
        "take", "than", "that", "the", "their", "them", "then", "there", "these",
        "they", "thing", "think", "this", "those", "through", "time", "too",
        "under", "until", "upon", "use", "used", "very", "want", "was", "way",
        "well", "went", "were", "what", "when", "where", "which", "while",
        "who", "will", "with", "without", "work", "would", "year", "you", "your",
        "able", "ask", "best", "better", "big", "call", "change", "child",
        "city", "close", "country", "course", "different", "end", "enough",
        "example", "face", "fact", "family", "far", "feel", "few", "find",
        "follow", "food", "friend", "full", "game", "great", "group", "hand",
        "happen", "head", "hear", "high", "house", "important", "information",
        "interest", "job", "kind", "large", "late", "leave", "life", "line",
        "live", "lot", "might", "money", "move", "night", "number", "open",
        "order", "page", "pay", "person", "play", "point", "problem", "program",
        "question", "read", "really", "reason", "room", "run", "school", "set",
        "show", "side", "small", "something", "start", "state", "story", "study",
        "system", "talk", "tell", "today", "together", "turn", "water", "week",
        "where", "why", "word", "world", "write", "young"
    };

    public static bool Contains(string word) => Words.Contains(word);
}
