using System;
using System.Collections.Generic;

namespace CoffeeStockWidget.Core.Models;

public class AppSettings
{
    public int PollIntervalSeconds { get; set; } = 300;
    public bool RunAtLogin { get; set; } = false;
    public string? SelectedParserType { get; set; }
    public int TransparencyPercent { get; set; } = 90; // 0-100
    public bool BlurEnabled { get; set; } = false;
    public int RetentionDays { get; set; } = 30;
    public int ItemsPerSource { get; set; } = 500;
    public int EventsPerSource { get; set; } = 1000;
    public DateTimeOffset? LastAcknowledgedUtc { get; set; }
    public List<string>? EnabledParsers { get; set; }
    public Dictionary<string, string>? CustomParserColors { get; set; }
    public bool AcrylicEnabled { get; set; } = false;
    public bool AccentHoverTintEnabled { get; set; } = true;
    public bool FetchNotesEnabled { get; set; } = false;
    public int MaxNotesFetchPerRun { get; set; } = 5;
    public int NewTagHours { get; set; } = 24;
    public bool AiSummarizationEnabled { get; set; } = false;
    public string AiModel { get; set; } = "phi:latest";
    public string AiEndpoint { get; set; } = "http://localhost:11434";
    public int AiMaxSummariesPerRun { get; set; } = 3;
    public double AiTemperature { get; set; } = 0.3;
    public double AiTopP { get; set; } = 0.9;
    public int AiMaxTokens { get; set; } = 256;
    public int AiRequestTimeoutSeconds { get; set; } = 45;
}
