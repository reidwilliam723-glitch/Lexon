using System.Net;
using System.Net.Http.Headers;

namespace Lexon.AI;

internal static class AiHttp
{
    private static readonly SocketsHttpHandler SharedHandler = new()
    {
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(4),
        MaxConnectionsPerServer = 4,
        EnableMultipleHttp2Connections = true,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        Expect100ContinueTimeout = TimeSpan.Zero
    };

    private static HttpClient? _keepClient;
    private static string? _keepUrl;
    private static readonly System.Threading.Timer KeepAlive = new(
        _ =>
        {
            var client = _keepClient;
            var url = _keepUrl;
            if (client == null || string.IsNullOrEmpty(url))
            {
                return;
            }

            _ = PingAsync(client, url, CancellationToken.None);
        },
        null,
        Timeout.Infinite,
        Timeout.Infinite);

    public static HttpClient CreateClient()
    {
        var client = new HttpClient(SharedHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(60),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        client.DefaultRequestHeaders.ExpectContinue = false;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public static async Task PingAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        _keepClient = client;
        _keepUrl = url;
        KeepAlive.Change(TimeSpan.FromSeconds(35), TimeSpan.FromSeconds(35));
        var (response, _) = await AiHttpCall.GetAsync(client, url, TimeSpan.FromSeconds(3), cancellationToken);
        response?.Dispose();
    }
}
