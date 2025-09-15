using System;
using System.Collections.Generic;

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

    public override string ToString() => $"[{ItemKey}] {Title} ({(InStock ? "In Stock" : "OOS")})";
}
