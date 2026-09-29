using Lexon.AI;
using Lexon.AI.Interfaces;
using Lexon.AI.Cache;
using Lexon.Core;
using Lexon.Core.Models;
using System.Text.Json;

namespace Lexon.AI.Providers;

/// <summary>
/// Ollama-powered suggestion provider (local AI)
/// </summary>
public class OllamaProvider : IAIProvider
{
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private readonly AIResponseCache _cache;
    private long _lastWarmup;

    public string Name => "Ollama";
    public bool IsFastPath => false;
    public string? NetworkEndpoint => _baseUrl;

    public OllamaProvider(string? baseUrl = null, string? model = null, HttpClient? httpClient = null, AIResponseCache? cache = null)
    {
        _baseUrl = baseUrl ?? "http://localhost:11434";
        _model = model ?? "llama3.2";
        _httpClient = httpClient ?? AiHttp.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
        _cache = cache ?? new AIResponseCache();
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
            var response = await CallOllamaAsync(prompt, cancellationToken);

            if (string.IsNullOrEmpty(response))
            {
                return Enumerable.Empty<Suggestion>();
            }

            _cache.CacheResponse("suggest", cacheKey, response);
            return ParseSuggestionsFromResponse(response).Take(5);
        }
        catch (OperationCanceledException)
        {
            return Enumerable.Empty<Suggestion>();
        }
        catch (Exception)
        {
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
            var response = await StreamGenerateAsync(
                prompt,
                cancellationToken,
                AiGenerationLimits.RewriteMaxTokens(text),
                AiGenerationLimits.RewriteTimeout,
                progress);

            if (!string.IsNullOrEmpty(response))
            {
                _cache.CacheResponse("rewrite", $"{instruction}:{text}", response);
            }

            return response ?? text;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return text;
        }
    }

    public async Task<string> ImproveGrammarAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var cached = _cache.GetCachedResponse("grammar", text);
            if (!string.IsNullOrEmpty(cached))
            {
                return cached;
            }

            var prompt = $"Improve the grammar and clarity of the following text. Keep the same meaning and tone:\n\n{text}";
            var response = await CallOllamaAsync(prompt, cancellationToken);

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
            var cached = _cache.GetCachedResponse($"tone_{tone}", text);
            if (!string.IsNullOrEmpty(cached))
            {
                return cached;
            }

            var prompt = $"Rewrite the following text with a {tone} tone:\n\n{text}";
            var response = await CallOllamaAsync(prompt, cancellationToken);

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

    private string BuildSuggestionPrompt(TextContext context) => AiPromptContext.BuildSuggestionPrompt(context);

    private async Task<string?> CallOllamaAsync(
        string prompt,
        CancellationToken cancellationToken,
        int maxTokens = 100,
        TimeSpan? timeout = null)
    {
        var (response, error) = await PostGenerateAsync(prompt, cancellationToken, maxTokens, timeout);
        if (error != null || response == null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseObject = JsonSerializer.Deserialize<JsonElement>(responseJson);

        if (responseObject.TryGetProperty("response", out var responseElement))
        {
            return responseElement.GetString();
        }

        return null;
    }

    public async Task<AiProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var (response, error) = await AiHttpCall.GetAsync(_httpClient, $"{_baseUrl}/api/tags", _timeout, cancellationToken);
        if (error != null)
        {
            return AiProbeResult.Unreachable("Ollama was not found. Make sure Ollama is running locally.");
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            return AiProbeResult.Unreachable("Ollama was not found. Make sure Ollama is running locally.");
        }

        return AiProbeResult.Ok("Ollama is running locally.");
    }

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        var now = Environment.TickCount64;
        if (_lastWarmup != 0 && now - _lastWarmup < 90_000)
        {
            return;
        }

        _lastWarmup = now;
        await AiHttp.PingAsync(_httpClient, $"{_baseUrl}/api/tags", cancellationToken);
    }

    private async Task<string?> StreamGenerateAsync(
        string prompt,
        CancellationToken cancellationToken,
        int maxTokens,
        TimeSpan timeout,
        IProgress<string>? progress)
    {
        var requestBody = new
        {
            model = _model,
            prompt,
            stream = true,
            options = new { temperature = 0.7, num_predict = maxTokens }
        };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var (response, error) = await AiHttpCall.PostAsync(
            _httpClient,
            $"{_baseUrl}/api/generate",
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
            return await AiStreamParser.ReadNdjsonAsync(stream, AiStreamParser.TryOllamaResponse, progress, cancellationToken);
        }
        finally
        {
            response.Dispose();
        }
    }

    private async Task<(HttpResponseMessage? Response, Exception? Error)> PostGenerateAsync(
        string prompt,
        CancellationToken cancellationToken,
        int maxTokens = 100,
        TimeSpan? timeout = null)
    {
        var requestBody = new
        {
            model = _model,
            prompt = prompt,
            stream = false,
            options = new
            {
                temperature = 0.7,
                num_predict = maxTokens
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await AiHttpCall.PostAsync(
            _httpClient,
            $"{_baseUrl}/api/generate",
            content,
            timeout ?? _timeout,
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
                    Source = "Ollama",
                    Score = 0.85
                };
            }
        }
    }

    /// <summary>
    /// Check if Ollama is available and running
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get available models from Ollama
    /// </summary>
    public async Task<IEnumerable<string>> GetAvailableModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<JsonElement>(responseJson);

            if (responseObject.TryGetProperty("models", out var models))
            {
                return models.EnumerateArray()
                    .Select(m => m.GetProperty("name").GetString())
                    .Where(name => !string.IsNullOrEmpty(name))!;
            }
        }
        catch
        {
            // Return empty list if we can't get models
        }

        return Enumerable.Empty<string>();
    }
}