using System.Text;
using System.Text.Json;

namespace Lexon.AI;

public static class AiStreamParser
{
    public static string? TryOpenAiDelta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var delta = choices[0].GetProperty("delta");
            return delta.TryGetProperty("content", out var content) ? content.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? TryGeminiText(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var content = candidates[0].GetProperty("content");
            if (!content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
            {
                return null;
            }

            var text = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("thought", out var thought)
                    && thought.ValueKind == JsonValueKind.True)
                {
                    continue;
                }

                if (part.TryGetProperty("text", out var piece))
                {
                    text.Append(piece.GetString());
                }
            }

            return text.Length == 0 ? null : text.ToString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? TryOllamaResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("response", out var text) ? text.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static async Task<string> ReadSseAsync(
        Stream stream,
        Func<string, string?> extractDelta,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        bool deltasAreCumulative = false)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        var acc = new StringBuilder();
        var lastReport = 0L;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (line.Length == 0)
            {
                continue;
            }

            const string prefix = "data:";
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line[prefix.Length..].Trim();
            if (payload.Length == 0 || payload == "[DONE]")
            {
                if (payload == "[DONE]")
                {
                    break;
                }

                continue;
            }

            ApplyChunk(acc, extractDelta(payload), deltasAreCumulative);
            lastReport = Report(progress, acc, lastReport, force: false);
        }

        Report(progress, acc, lastReport, force: true);
        return acc.ToString();
    }

    public static async Task<string> ReadNdjsonAsync(
        Stream stream,
        Func<string, string?> extractDelta,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        var acc = new StringBuilder();
        var lastReport = 0L;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            ApplyChunk(acc, extractDelta(line), cumulative: false);
            lastReport = Report(progress, acc, lastReport, force: false);
        }

        Report(progress, acc, lastReport, force: true);
        return acc.ToString();
    }

    private static void ApplyChunk(StringBuilder acc, string? piece, bool cumulative)
    {
        if (string.IsNullOrEmpty(piece))
        {
            return;
        }

        if (cumulative)
        {
            var soFar = acc.ToString();
            if (piece.StartsWith(soFar, StringComparison.Ordinal))
            {
                acc.Clear();
                acc.Append(piece);
                return;
            }
        }

        acc.Append(piece);
    }

    private static long Report(IProgress<string>? progress, StringBuilder acc, long lastReport, bool force)
    {
        if (progress == null)
        {
            return lastReport;
        }

        var now = Environment.TickCount64;
        if (!force && now - lastReport < 40)
        {
            return lastReport;
        }

        progress.Report(acc.ToString());
        return now;
    }
}
