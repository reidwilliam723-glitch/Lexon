namespace Lexon.Core;

public static class CloudAiNames
{
    public static bool IsCloud(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
            || name.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Empty or missing URLs are treated as local (Ollama's default host).
    /// </summary>
    public static bool IsLoopbackEndpoint(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.IsLoopback;
    }

    /// <summary>
    /// Cloud providers always need the typing-suggestion toggle. Ollama only
    /// does when its host is not this machine. Other slow plugins follow
    /// Local-only alone.
    /// </summary>
    public static bool RequiresTypingConsent(string? name, string? endpoint = null)
    {
        if (IsCloud(name))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(name)
            && name.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
            && !IsLoopbackEndpoint(endpoint))
        {
            return true;
        }

        return false;
    }
}
