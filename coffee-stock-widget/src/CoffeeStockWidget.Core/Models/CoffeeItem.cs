using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CoffeeStockWidget.Core.Models;

public class CoffeeItem
{
    public int? Id { get; set; }
    public int SourceId { get; set; }
    public string ItemKey { get; set; } = string.Empty; // stable key (SKU or normalized hash)
    public string Title { get; set; } = string.Empty;
    public Uri Url { get; set; } = new Uri("https://example.invalid");
    public int? PriceCents { get; set; }
    public bool InStock { get; set; }
    public DateTimeOffset FirstSeenUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string>? Attributes { get; set; }
    public CoffeeAiSummary? AiSummary { get; set; }
    public DateTimeOffset? AiProcessedUtc { get; set; }
    public string? AiModelVersion { get; set; }
    public string? AiSummaryHash { get; set; }

    public override string ToString() => $"[{ItemKey}] {Title} ({(InStock ? "In Stock" : "OOS")})";
}

public class CoffeeAiSummary
{
    [JsonPropertyName("shortTitle")]
    public string? ShortTitle { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("producer")]
    public string? Producer { get; set; }

    [JsonPropertyName("origin")]
    public string? Origin { get; set; }

    [JsonPropertyName("elevation")]
    public string? Elevation { get; set; }

    [JsonPropertyName("process")]
    public string? Process { get; set; }

    [JsonPropertyName("tastingNotes")]
    public List<string>? TastingNotes { get; set; }
}
