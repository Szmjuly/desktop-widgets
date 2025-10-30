using System;
using System.Collections.Generic;

namespace CoffeeStockWidget.Core.Models;

public class AiSummarizerRequest
{
    public string ItemKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Roaster { get; init; }
    public string? Notes { get; init; }
    public CoffeeProfile? Profile { get; init; }
    public Dictionary<string, string>? Attributes { get; init; }
    public CoffeeItem? SourceItem { get; init; }
}

public class AiSummarizerConfig
{
    public bool Enabled { get; set; } = true;
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "phi:latest";
    public double Temperature { get; set; } = 0.3;
    public double TopP { get; set; } = 0.9;
    public int MaxTokens { get; set; } = 256;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
    public string? SystemPrompt { get; set; }
}
