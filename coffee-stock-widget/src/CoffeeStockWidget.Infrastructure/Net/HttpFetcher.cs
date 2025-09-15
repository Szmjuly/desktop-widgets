using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoffeeStockWidget.Core.Abstractions;

namespace CoffeeStockWidget.Infrastructure.Net;

public class HttpFetcher : IHttpFetcher
{
    private static readonly HttpClient _http = CreateClient();
    private static readonly ConcurrentDictionary<string, DateTimeOffset> _lastByHost = new();

    private readonly int _minDelayPerHostMs;

    public HttpFetcher(int minDelayPerHostMs = 1500)
    {
        _minDelayPerHostMs = minDelayPerHostMs;
    }

    public async Task<string> GetStringAsync(Uri uri, System.Collections.Generic.IDictionary<string, string>? headers = null, CancellationToken ct = default)
    {
        await RespectPerHostDelayAsync(uri.Host, ct).ConfigureAwait(false);
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        req.Headers.UserAgent.ParseAdd("CoffeeStockWidget/1.0 (+https://example.invalid)");
        if (headers != null)
        {
            foreach (var kv in headers)
            {
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        var charset = resp.Content.Headers.ContentType?.CharSet;
        var encoding = !string.IsNullOrWhiteSpace(charset) ? Encoding.GetEncoding(charset) : Encoding.UTF8;
        return encoding.GetString(bytes);
    }

    private async Task RespectPerHostDelayAsync(string host, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var last = _lastByHost.GetOrAdd(host, now);
        var delta = now - last;
        if (delta.TotalMilliseconds < _minDelayPerHostMs)
        {
            var delay = _minDelayPerHostMs - (int)delta.TotalMilliseconds;
            if (delay > 0) await Task.Delay(delay, ct).ConfigureAwait(false);
        }
        _lastByHost[host] = DateTimeOffset.UtcNow;
    }

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        return client;
    }
}
