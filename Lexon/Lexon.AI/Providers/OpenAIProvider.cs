using Lexon.AI;
using Lexon.AI.Interfaces;
using Lexon.AI.Cache;
using Lexon.Core;
using Lexon.Core.Models;
using System.Text.Json;

namespace Lexon.AI.Providers;

/// <summary>
/// OpenAI-powered suggestion provider
/// </summary>
public class OpenAIProvider : IAIProvider
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "https://api.openai.com/v1";
    private string _model = "gpt-4o-mini";
    private readonly AIResponseCache _cache;
    private long _lastWarmup;

    public string Name => "OpenAI";
    public bool IsFastPath => false;

    public OpenAIProvider(string? apiKey = null, HttpClient? httpClient = null, AIResponseCache? cache = null, string? model = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is required. Set OPENAI_API_KEY environment variable or pass apiKey parameter.");
        }
        
        _httpClient = httpClient ?? AiHttp.CreateClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        _cache = cache ?? new AIResponseCache();
        if (!string.IsNullOrWhiteSpace(model))
        {
            _model = model.Trim();
        }
    }

    public async Task<IEnumerable<Suggestion>> GetSuggestionsAsync(TextContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"{context.PreviousWords}|{context.CurrentWord}|{context.FollowingWords}";
            var cached = _cache.GetCachedResponse("suggest", cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                return ParseSuggestionsFromResponse(cached).Take(5);
            }

            var prompt = AiPromptContext.BuildSuggestionPrompt(context);
            var response = await CallOpenAIAsync(prompt, cancellationToken);

            if (string.IsNullOrEmpty(response))
            {
                return Enumerable.Empty<Suggestion>();
            }

            _cache.CacheResponse("suggest", cacheKey, response);
            return ParseSuggestionsFromResponse(response).Take(5);
        }
        catch (OperationCanceledException)
        {
            // Timeout - return empty suggestions
            return Enumerable.Empty<Suggestion>();
        }
        catch (Exception)
        {
            // Other errors - return empty suggestions
            return Enumerable.Empty<Suggestion>();
        }
    }

    public async Task<string> RewriteTextAsync(
        string text,
        string instruction,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
    {
        try
        {
            var cached = _cache.GetCachedResponse("rewrite", $"{instruction}:{text}");
            if (!string.IsNullOrEmpty(cached))
            {
                progress?.Report(cached);
                return cached;
            }

            var prompt = AiPromptContext.BuildRewritePrompt(text, instruction);
            var response = await StreamChatAsync(
                prompt,
                cancellationToken,
                AiGenerationLimits.RewriteMaxTokens(text),
                AiGenerationLimits.RewriteTimeout,
                progress);

            if (!string.IsNullOrEmpty(response))
            {
                _cache.CacheResponse("rewrite", $"{instruction}:{text}", response);
            }

            return response ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public async Task<string> ImproveGrammarAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check cache first
            var cached = _cache.GetCachedResponse("grammar", text);
            if (!string.IsNullOrEmpty(cached))
            {
                return cached;
            }

            var prompt = $"Improve the grammar and clarity of the following text. Keep the same meaning and tone:\n\n{text}";
            var response = await CallOpenAIAsync(prompt, cancellationToken);
            
            // Cache the response
            if (!string.IsNullOrEmpty(response))
            {
                _cache.CacheResponse("grammar", text, response);
            }
            
            return response ?? text;
        }
        catch (OperationCanceledException)
        {
            return text;
        }
        catch (Exception)
        {
            return text;
        }
    }

    public async Task<string> ChangeToneAsync(string text, string tone, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check cache first
            var cached = _cache.GetCachedResponse($"tone_{tone}", text);
            if (!string.IsNullOrEmpty(cached))
            {
                return cached;
            }

            var prompt = $"Rewrite the following text with a {tone} tone:\n\n{text}";
            var response = await CallOpenAIAsync(prompt, cancellationToken);
            
            // Cache the response
            if (!string.IsNullOrEmpty(response))
            {
                _cache.CacheResponse($"tone_{tone}", text, response);
            }
            
            return response ?? text;
        }
        catch (OperationCanceledException)
        {
            return text;
        }
        catch (Exception)
        {
            return text;
        }
    }

    private async Task<string?> CallOpenAIAsync(
        string prompt,
        CancellationToken cancellationToken,
        int maxTokens = AiGenerationLimits.SuggestionMaxTokens,
        TimeSpan? timeout = null)
    {
        var (response, error) = await PostAsync(prompt, cancellationToken, maxTokens, timeout);
        if (error != null || response == null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseObject = JsonSerializer.Deserialize<JsonElement>(responseJson);
        return responseObject.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }

    public async Task<AiProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var (response, error) = await PostAsync(AiProviderCatalog.ProbePrompt, cancellationToken);
        if (error != null)
        {
            return AiProbeResult.FromException(error);
        }

        if (response == null)
        {
            return AiProbeResult.Unreachable();
        }

        return AiProbeResult.FromHttpStatus(response.StatusCode);
    }

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        var now = Environment.TickCount64;
        if (_lastWarmup != 0 && now - _lastWarmup < 90_000)
        {
            return;
        }

        _lastWarmup = now;
        await AiHttp.PingAsync(_httpClient, $"{_baseUrl}/models", cancellationToken);
    }

    private async Task<string?> StreamChatAsync(
        string prompt,
        CancellationToken cancellationToken,
        int maxTokens,
        TimeSpan timeout,
        IProgress<string>? progress)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[] { new { role = "user", content = prompt } },
            max_completion_tokens = maxTokens,
            temperature = 0,
            stream = true
        };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var (response, error) = await AiHttpCall.PostAsync(
            _httpClient,
            $"{_baseUrl}/chat/completions",
            content,
            timeout,
            cancellationToken);
        if (error != null || response == null || !response.IsSuccessStatusCode)
        {
            response?.Dispose();
            return null;
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await AiStreamParser.ReadSseAsync(stream, AiStreamParser.TryOpenAiDelta, progress, cancellationToken);
        }
        finally
        {
            response.Dispose();
        }
    }

    private async Task<(HttpResponseMessage? Response, Exception? Error)> PostAsync(
        string prompt,
        CancellationToken cancellationToken,
        int maxTokens = AiGenerationLimits.SuggestionMaxTokens,
        TimeSpan? timeout = null)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            max_tokens = maxTokens,
            temperature = 0
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await AiHttpCall.PostAsync(
            _httpClient,
            $"{_baseUrl}/chat/completions",
            content,
            timeout ?? AiGenerationLimits.SuggestionTimeout,
            cancellationToken);
    }

    private IEnumerable<Suggestion> ParseSuggestionsFromResponse(string response)
    {
        var lines = response.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("-") && !trimmed.StartsWith("*"))
            {
                yield return new Suggestion
                {
                    Text = trimmed,
                    Source = "AI",
                    Score = 0.85
                };
            }
        }
    }
}
