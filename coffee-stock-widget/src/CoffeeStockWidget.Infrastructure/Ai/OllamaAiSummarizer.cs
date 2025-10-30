using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CoffeeStockWidget.Core.Abstractions;
using CoffeeStockWidget.Core.Models;

namespace CoffeeStockWidget.Infrastructure.Ai;

public class OllamaAiSummarizer : IAiSummarizer, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private static readonly string DefaultSystemPrompt =
        "You are a coffee expert that produces concise tasting summaries. Always respond with a compact JSON object using the fields shortTitle, summary, producer, origin, elevation, process, tastingNotes.";

    private readonly HttpClient _httpClient;
    private bool _disposed;

    public OllamaAiSummarizer(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultClient();
    }

    public async Task<CoffeeAiSummary?> SummarizeAsync(AiSummarizerRequest request, AiSummarizerConfig config, CancellationToken ct = default)
    {
        if (!config.Enabled)
        {
            return request.SourceItem?.AiSummary;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(config.Timeout);

        var payload = new
        {
            model = config.Model,
            prompt = BuildPrompt(request),
            system = string.IsNullOrWhiteSpace(config.SystemPrompt) ? DefaultSystemPrompt : config.SystemPrompt,
            stream = false,
            format = "json",
            options = new
            {
                temperature = config.Temperature,
                top_p = config.TopP,
                num_predict = config.MaxTokens
            }
        };

        var endpoint = config.Endpoint?.TrimEnd('/') ?? "http://localhost:11434";
        var uri = new Uri(new Uri(endpoint), "/api/generate");

        using var response = await _httpClient.PostAsJsonAsync(uri, payload, JsonOptions, linkedCts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: linkedCts.Token).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("response", out var responseNode))
        {
            return null;
        }

        var raw = responseNode.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var jsonText = TryExtractJson(raw!);
        if (jsonText == null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CoffeeAiSummary>(jsonText, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static HttpClient CreateDefaultClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    private static string BuildPrompt(AiSummarizerRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Summarize the following coffee for a retail product card.");
        sb.AppendLine("Provide a short highlight title (max 40 chars) and an engaging sentence-length summary (max 220 chars).");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(request.Roaster))
        {
            sb.Append("Roaster: ").AppendLine(request.Roaster!.Trim());
        }
        sb.Append("Title: ").AppendLine(request.Title.Trim());
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            sb.AppendLine();
            sb.AppendLine("Tasting Notes Text:");
            sb.AppendLine(request.Notes!.Trim());
        }
        if (request.Profile != null)
        {
            if (!string.IsNullOrWhiteSpace(request.Profile.Producer))
            {
                sb.Append("Producer: ").AppendLine(request.Profile.Producer.Trim());
            }
            if (!string.IsNullOrWhiteSpace(request.Profile.Origin))
            {
                sb.Append("Origin: ").AppendLine(request.Profile.Origin.Trim());
            }
            if (!string.IsNullOrWhiteSpace(request.Profile.Process))
            {
                sb.Append("Process: ").AppendLine(request.Profile.Process.Trim());
            }
            if (request.Profile.TastingNotes is { Count: > 0 })
            {
                sb.Append("ProfileNotes: ").AppendLine(string.Join(", ", request.Profile.TastingNotes));
            }
        }
        if (request.Attributes is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("Attributes:");
            foreach (var kv in request.Attributes)
            {
                if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value)) continue;
                sb.Append("- ").Append(kv.Key.Trim()).Append(": ").AppendLine(kv.Value.Trim());
            }
        }
        sb.AppendLine();
        sb.AppendLine("Respond with a JSON object matching the schema:");
        sb.AppendLine("{\"shortTitle\":string, \"summary\":string, \"producer\":string|null, \"origin\":string|null, \"elevation\":string|null, \"process\":string|null, \"tastingNotes\":[string] }");
        return sb.ToString();
    }

    private static string? TryExtractJson(string text)
    {
        var first = text.IndexOf('{');
        var last = text.LastIndexOf('}');
        if (first >= 0 && last > first)
        {
            return text.Substring(first, last - first + 1);
        }
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
