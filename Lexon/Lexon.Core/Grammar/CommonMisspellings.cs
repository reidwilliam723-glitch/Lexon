using System.Text.RegularExpressions;

namespace Lexon.Core.Grammar;

/// <summary>
/// Common mechanical misspellings with a single well-defined correction.
/// Used by the rule-based checker and by inline typing suggestions.
/// </summary>
public static class CommonMisspellings
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["teh"] = "the",
        ["adn"] = "and",
        ["taht"] = "that",
        ["thier"] = "their",
        ["recieve"] = "receive",
        ["recieved"] = "received",
        ["recieving"] = "receiving",
        ["beleive"] = "believe",
        ["beleived"] = "believed",
        ["beleiving"] = "believing",
        ["acheive"] = "achieve",
        ["acheived"] = "achieved",
        ["seperate"] = "separate",
        ["seperated"] = "separated",
        ["seperately"] = "separately",
        ["definately"] = "definitely",
        ["occassion"] = "occasion",
        ["occassional"] = "occasional",
        ["occured"] = "occurred",
        ["occurence"] = "occurrence",
        ["untill"] = "until",
        ["wierd"] = "weird",
        ["truely"] = "truly",
        ["adress"] = "address",
        ["accomodate"] = "accommodate",
        ["accomodation"] = "accommodation",
        ["goverment"] = "government",
        ["independant"] = "independent",
        ["neccessary"] = "necessary",
        ["necessery"] = "necessary",
        ["publically"] = "publicly",
        ["enviroment"] = "environment",
        ["enviromental"] = "environmental",
        ["existance"] = "existence",
        ["foriegn"] = "foreign",
        ["freind"] = "friend",
        ["harrass"] = "harass",
        ["harrassment"] = "harassment",
        ["knowlege"] = "knowledge",
        ["liason"] = "liaison",
        ["maintainance"] = "maintenance",
        ["millenium"] = "millennium",
        ["mispell"] = "misspell",
        ["mispelled"] = "misspelled",
        ["noticable"] = "noticeable",
        ["persue"] = "pursue",
        ["priviledge"] = "privilege",
        ["probaly"] = "probably",
        ["reccomend"] = "recommend",
        ["reccomended"] = "recommended",
        ["refered"] = "referred",
        ["relevent"] = "relevant",
        ["religous"] = "religious",
        ["resistence"] = "resistance",
        ["succesful"] = "successful",
        ["sucessful"] = "successful",
        ["tommorrow"] = "tomorrow",
        ["tounge"] = "tongue",
        ["usefull"] = "useful",
        ["visable"] = "visible",
        ["wether"] = "whether",
        ["wich"] = "which",
        ["alot"] = "a lot",
        ["irregardless"] = "regardless",
        ["supposably"] = "supposedly",
        ["undoubtably"] = "undoubtedly",
        ["absense"] = "absence",
        ["accross"] = "across",
        ["agressive"] = "aggressive",
        ["apparant"] = "apparent",
        ["appearence"] = "appearance",
        ["arguement"] = "argument",
        ["basicly"] = "basically",
        ["begining"] = "beginning",
        ["beleif"] = "belief",
        ["buisness"] = "business",
        ["calender"] = "calendar",
        ["catagory"] = "category",
        ["cemetary"] = "cemetery",
        ["collegue"] = "colleague",
        ["comming"] = "coming",
        ["commited"] = "committed",
        ["concious"] = "conscious",
        ["curiousity"] = "curiosity",
        ["decieve"] = "deceive",
        ["desparate"] = "desperate",
        ["diffrent"] = "different",
        ["dissapear"] = "disappear",
        ["dissapoint"] = "disappoint",
        ["embarass"] = "embarrass",
        ["embarassed"] = "embarrassed",
        ["enviroments"] = "environments",
        ["equiptment"] = "equipment",
        ["exmaple"] = "example",
        ["familar"] = "familiar",
        ["finaly"] = "finally",
        ["foward"] = "forward",
        ["fourty"] = "forty",
        ["gaurd"] = "guard",
        ["grammer"] = "grammar",
        ["hieght"] = "height",
        ["hygeine"] = "hygiene",
        ["immediatly"] = "immediately",
        ["incase"] = "in case",
        ["independance"] = "independence",
        ["interupt"] = "interrupt",
        ["jist"] = "gist",
        ["langauge"] = "language",
        ["lenght"] = "length",
        ["libary"] = "library",
        ["lisence"] = "license",
        ["lightening"] = "lightning",
        ["loosing"] = "losing",
        ["neccessarily"] = "necessarily",
        ["negociate"] = "negotiate",
        ["nieghbor"] = "neighbor",
        ["noone"] = "no one",
        ["occassionally"] = "occasionally",
        ["oppurtunity"] = "opportunity",
        ["orginal"] = "original",
        ["oustanding"] = "outstanding",
        ["overide"] = "override",
        ["parrallel"] = "parallel",
        ["peice"] = "piece",
        ["persistant"] = "persistent",
        ["posession"] = "possession",
        ["prefered"] = "preferred",
        ["proffesional"] = "professional",
        ["pronounciation"] = "pronunciation",
        ["psuedo"] = "pseudo",
        ["qustion"] = "question",
        ["realy"] = "really",
        ["reciept"] = "receipt",
        ["refering"] = "referring",
        ["remeber"] = "remember",
        ["repitition"] = "repetition",
        ["rythm"] = "rhythm",
        ["saftey"] = "safety",
        ["sceince"] = "science",
        ["sence"] = "sense",
        ["sieze"] = "seize",
        ["similiar"] = "similar",
        ["speach"] = "speech",
        ["strenght"] = "strength",
        ["succes"] = "success",
        ["supercede"] = "supersede",
        ["tatoo"] = "tattoo",
        ["tendancy"] = "tendency",
        ["thast"] = "that's",
        ["ther"] = "there",
        ["thna"] = "than",
        ["thnaks"] = "thanks",
        ["threshhold"] = "threshold",
        ["tje"] = "the",
        ["transfered"] = "transferred",
        ["tyr"] = "try",
        ["unforseen"] = "unforeseen",
        ["unkown"] = "unknown",
        ["usally"] = "usually",
        ["vaccuum"] = "vacuum",
        ["wans't"] = "wasn't",
        ["wih"] = "with",
        ["wirting"] = "writing",
        ["withing"] = "within",
        ["writting"] = "writing",
        ["yeasr"] = "years",
        ["yuo"] = "you",
        ["im"] = "I'm",
        ["ive"] = "I've",
        ["youve"] = "you've",
        ["weve"] = "we've",
        ["theyve"] = "they've",
        ["couldve"] = "could've",
        ["wouldve"] = "would've",
        ["shouldve"] = "should've",
        ["youll"] = "you'll",
        ["theyll"] = "they'll",
        ["itll"] = "it'll",
        ["thatll"] = "that'll",
        ["whats"] = "what's",
        ["thats"] = "that's",
        ["wheres"] = "where's",
        ["heres"] = "here's",
        ["hows"] = "how's",
        ["whos"] = "who's"
    };

    /// <summary>
    /// Missing apostrophes. Suggested by default; auto-applied only when
    /// Auto-correct contractions is on.
    /// </summary>
    private static readonly HashSet<string> Contractions = new(StringComparer.OrdinalIgnoreCase)
    {
        "im", "ive", "youve", "weve", "theyve", "couldve", "wouldve", "shouldve",
        "youll", "theyll", "itll", "thatll",
        "whats", "thats", "wheres", "heres", "hows", "whos", "thast", "wans't"
    };

    /// <summary>
    /// Real words or short slips that need context. Suggested; never auto-applied.
    /// </summary>
    private static readonly HashSet<string> Ambiguous = new(StringComparer.OrdinalIgnoreCase)
    {
        "ther", "wich", "wether", "sence", "lightening", "loosing", "tyr", "wih", "jist"
    };

    private static readonly (Regex Pattern, string Replacement)[] CachedPatterns =
        Map.Select(pair => (
            new Regex(@"\b" + Regex.Escape(pair.Key) + @"\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            pair.Value)).ToArray();

    public static IReadOnlyList<(Regex Pattern, string Replacement)> Patterns => CachedPatterns;

    public static bool TryAutoCorrect(string word, out string correction, bool includeContractions = false)
    {
        if (!TryCorrect(word, out correction) || Ambiguous.Contains(word))
        {
            correction = string.Empty;
            return false;
        }

        if (Contractions.Contains(word))
        {
            if (!includeContractions)
            {
                correction = string.Empty;
                return false;
            }

            return true;
        }

        return true;
    }

    public static bool TryCorrect(string word, out string correction)
    {
        if (string.IsNullOrEmpty(word))
        {
            correction = string.Empty;
            return false;
        }

        return Map.TryGetValue(word, out correction!);
    }

    public static IEnumerable<KeyValuePair<string, string>> All => Map;
}
