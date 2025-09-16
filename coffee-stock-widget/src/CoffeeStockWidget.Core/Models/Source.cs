using System;

namespace CoffeeStockWidget.Core.Models;

public class Source
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Uri RootUrl { get; set; } = new("https://example.invalid");
    public string ParserType { get; set; } = "Generic"; // e.g., "BlackAndWhite", "Generic"
    public int PollIntervalSeconds { get; set; } = 300;
    public bool Enabled { get; set; } = true;

    // UI theming: default brand color and optional user override (hex ARGB or RGB)
    public string DefaultColorHex { get; set; } = "#FF4CAF50"; // sensible default
    public string? CustomColorHex { get; set; }
}
