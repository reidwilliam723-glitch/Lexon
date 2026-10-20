using System.Text.RegularExpressions;
using Lexon.AI.Cache;
using Lexon.AI.Interfaces;
using Lexon.AI.Providers;

namespace Lexon.AI;

public static class AiProviderCatalog
{
    public const string Recommended = "OpenAI";
    public const string ProbePrompt = "Reply with the single word ok.";

    public static readonly string[] CloudProviders = ["OpenAI", "Gemini", "DeepSeek"];
    public static readonly string[] AllProviders = ["OpenAI", "Gemini", "DeepSeek", "Ollama", "None"];

    public static bool UsesApiKey(string provider)
        => CloudProviders.Any(name => name.Equals(provider, StringComparison.OrdinalIgnoreCase));

    public static bool IsRecommended(string? provider)
        => Recommended.Equals(provider, StringComparison.OrdinalIgnoreCase);

    public static bool ShowAdvancedByDefault(string? savedProvider)
    {
        if (string.IsNullOrWhiteSpace(savedProvider)
            || savedProvider.Equals("None", StringComparison.OrdinalIgnoreCase)
            || IsRecommended(savedProvider))
        {
            return false;
        }

        return AllProviders.Any(name => name.Equals(savedProvider, StringComparison.OrdinalIgnoreCase));
    }

    public static string? KeyCreationUrl(string provider)
        => provider.ToLowerInvariant() switch
        {
            "openai" => "https://platform.openai.com/api-keys",
            "gemini" => "https://aistudio.google.com/apikey",
            "deepseek" => "https://platform.deepseek.com/api_keys",
            _ => null
        };

    public static string DefaultModel(string provider)
        => provider.ToLowerInvariant() switch
        {
            "openai" => "gpt-4o-mini",
            "gemini" => "gemini-3.8-flash",
            "deepseek" => "deepseek-chat",
            "ollama" => "llama3.2",
            _ => string.Empty
        };

    public static IReadOnlyList<string> Models(string provider)
        => provider.ToLowerInvariant() switch
        {
            "openai" => ["gpt-4o-mini", "gpt-4.1-mini"],
            "gemini" => ["gemini-3.8-flash"],
            "deepseek" => ["deepseek-chat", "deepseek-reasoner"],
            "ollama" => ["llama3.2", "llama3.1", "llama2", "phi3", "qwen2.5"],
            _ => []
        };

    public static bool LooksLikeApiKey(string provider, string? clipboardText)
    {
        var key = FirstLine(clipboardText);
        if (key.Length == 0 || !UsesApiKey(provider))
        {
            return false;
        }

        if (!MatchesProviderPattern(provider, key))
        {
            return false;
        }

        // sk-style prefixes are shared across vendors. If another paid provider
        // also matches, this is not a confident fill — the user can paste.
        foreach (var other in CloudProviders)
        {
            if (!other.Equals(provider, StringComparison.OrdinalIgnoreCase)
                && MatchesProviderPattern(other, key))
            {
                return false;
            }
        }

        return true;
    }

    public static string FirstLine(string? clipboardText)
    {
        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            return string.Empty;
        }

        var text = clipboardText.Trim();
        var end = text.IndexOfAny(['\r', '\n']);
        if (end >= 0)
        {
            text = text[..end].Trim();
        }

        return text.Contains(' ') ? string.Empty : text;
    }

    /// <summary>
    /// Retired model ids keep working by moving to the current replacement.
    /// </summary>
    public static string ResolveModel(string provider, string? model)
    {
        var pick = string.IsNullOrWhiteSpace(model) ? DefaultModel(provider) : model.Trim();
        return (provider.ToLowerInvariant(), pick) switch
        {
            ("gemini", "gemini-2.0-flash" or "gemini-2.0-flash-001" or "gemini-1.5-flash" or "gemini-1.5-pro" or "gemini-2.5-flash" or "gemini-2.5-pro")
                => DefaultModel("gemini"),
            ("openai", "gpt-3.5-turbo" or "gpt-3.5-turbo-0125") => "gpt-4.1-mini",
            _ => pick
        };
    }

    public static IAIProvider Create(string provider, string? apiKey, AIResponseCache? cache = null, string? model = null)
    {
        cache ??= new AIResponseCache();
        var resolved = ResolveModel(provider, model);
        return provider.ToLowerInvariant() switch
        {
            "openai" => new OpenAIProvider(apiKey, null, cache, resolved),
            "gemini" => new GeminiProvider(apiKey, null, cache, resolved),
            "deepseek" => new DeepSeekProvider(apiKey, null, cache, resolved),
            "ollama" => new OllamaProvider(baseUrl: null, model: resolved, null, cache),
            _ => throw new ArgumentException($"Unknown AI provider: {provider}")
        };
    }

    public static async Task<AiProbeResult> ProbeAsync(
        string provider,
        string? apiKey,
        CancellationToken cancellationToken = default,
        string? model = null)
    {
        if (provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return AiProbeResult.Ok("AI provider disconnected.");
        }

        if (UsesApiKey(provider) && string.IsNullOrWhiteSpace(apiKey))
        {
            return AiProbeResult.Error("Paste an API key to connect.");
        }

        try
        {
            var instance = Create(provider, apiKey, model: model);
            return await instance.ProbeAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return AiProbeResult.InvalidCredential(ex.Message);
        }
        catch (Exception ex)
        {
            return AiProbeResult.FromException(ex);
        }
    }

    private static bool MatchesProviderPattern(string provider, string key)
        => provider.ToLowerInvariant() switch
        {
            "openai" => OpenAiKey.IsMatch(key),
            "gemini" => GeminiKey.IsMatch(key),
            // Official docs only document Bearer sk-…. TruffleHog (API-verified)
            // and keyhog treat issued keys as sk- plus exactly 32 lowercase
            // alphanumeric characters.
            "deepseek" => DeepSeekKey.IsMatch(key),
            _ => false
        };

    // Exclude sk- + 32 lowercase alnum so those belong to DeepSeek, not OpenAI.
    private static readonly Regex OpenAiKey = new(
        @"^sk-(?:proj-|svcacct-)[A-Za-z0-9_-]{20,}$|^sk-(?![a-z0-9]{32}$)[A-Za-z0-9_-]{20,}$",
        RegexOptions.Compiled);
    private static readonly Regex GeminiKey = new(@"^AIza[0-9A-Za-z_-]{20,}$", RegexOptions.Compiled);
    private static readonly Regex DeepSeekKey = new(@"^sk-[a-z0-9]{32}$", RegexOptions.Compiled);
}
