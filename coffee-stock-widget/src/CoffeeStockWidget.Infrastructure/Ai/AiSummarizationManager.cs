using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoffeeStockWidget.Core.Abstractions;
using CoffeeStockWidget.Core.Models;
using CoffeeStockWidget.Core.Services;

namespace CoffeeStockWidget.Infrastructure.Ai;

public sealed class AiSummarizationManager : IDisposable
{
    private readonly IAiSummarizer _summarizer;
    private bool _disposed;

    public AiSummarizationManager(IAiSummarizer summarizer)
    {
        _summarizer = summarizer;
    }

    public async Task<int> ApplySummariesAsync(IReadOnlyList<CoffeeItem> items,
        IReadOnlyDictionary<string, CoffeeItem> previous,
        AppSettings settings,
        bool forceAll = false,
        CancellationToken ct = default)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        var config = CreateConfig(settings);
        int applied = 0;
        int limit = forceAll ? int.MaxValue : Math.Max(1, settings.AiMaxSummariesPerRun);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            var fingerprint = AiSummaryHasher.ComputeFingerprint(item);
            if (previous.TryGetValue(item.ItemKey, out var prev))
            {
                CopyExistingSummaryIfApplicable(item, prev, fingerprint, forceAll);
            }

            var alreadySatisfied = item.AiSummary != null && string.Equals(item.AiSummaryHash, fingerprint, StringComparison.Ordinal);
            if (!config.Enabled || alreadySatisfied)
            {
                continue;
            }

            if (!forceAll && applied >= limit)
            {
                continue;
            }

            var request = BuildRequest(item, previous);
            CoffeeAiSummary? summary;
            try
            {
                summary = await _summarizer.SummarizeAsync(request, config, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                continue;
            }

            if (summary == null)
            {
                continue;
            }

            NormalizeSummary(summary);
            item.AiSummary = summary;
            item.AiProcessedUtc = DateTimeOffset.UtcNow;
            item.AiModelVersion = config.Model;
            item.AiSummaryHash = fingerprint;
            applied++;
        }

        return applied;
    }

    private static void CopyExistingSummaryIfApplicable(CoffeeItem target, CoffeeItem previous, string fingerprint, bool forceAll)
    {
        if (previous.AiSummary == null)
        {
            return;
        }

        if (!forceAll && !string.Equals(previous.AiSummaryHash, fingerprint, StringComparison.Ordinal))
        {
            return;
        }

        target.AiSummary = previous.AiSummary;
        target.AiProcessedUtc = previous.AiProcessedUtc;
        target.AiModelVersion = previous.AiModelVersion;
        target.AiSummaryHash = previous.AiSummaryHash;
    }

    private static AiSummarizerConfig CreateConfig(AppSettings settings)
    {
        return new AiSummarizerConfig
        {
            Enabled = settings.AiSummarizationEnabled,
            Endpoint = settings.AiEndpoint,
            Model = settings.AiModel,
            Temperature = settings.AiTemperature,
            TopP = settings.AiTopP,
            MaxTokens = Math.Max(32, settings.AiMaxTokens),
            Timeout = TimeSpan.FromSeconds(Math.Max(5, settings.AiRequestTimeoutSeconds)),
            SystemPrompt = null
        };
    }

    private static AiSummarizerRequest BuildRequest(CoffeeItem item, IReadOnlyDictionary<string, CoffeeItem> previous)
    {
        string? notes = null;
        if (item.Attributes != null && item.Attributes.TryGetValue("notes", out var n))
        {
            notes = n;
        }

        CoffeeProfile? profile = null;
        if (item.Attributes != null && item.Attributes.TryGetValue("profile", out var profileJson))
        {
            try
            {
                profile = System.Text.Json.JsonSerializer.Deserialize<CoffeeProfile>(profileJson);
            }
            catch
            {
                profile = null;
            }
        }

        string? roaster = null;
        if (previous.TryGetValue(item.ItemKey, out var prev) && prev.SourceId == item.SourceId)
        {
            roaster = prev.Attributes != null && prev.Attributes.TryGetValue("roaster", out var r) ? r : roaster;
        }

        return new AiSummarizerRequest
        {
            ItemKey = item.ItemKey,
            Title = item.Title,
            Roaster = roaster,
            Notes = notes,
            Profile = profile,
            Attributes = item.Attributes,
            SourceItem = item
        };
    }

    private static void NormalizeSummary(CoffeeAiSummary summary)
    {
        if (summary.TastingNotes != null)
        {
            summary.TastingNotes = summary.TastingNotes
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();
        }

        if (summary.ShortTitle != null)
        {
            summary.ShortTitle = summary.ShortTitle.Trim();
        }
        if (summary.Summary != null)
        {
            summary.Summary = summary.Summary.Trim();
        }
        if (summary.Producer != null)
        {
            summary.Producer = summary.Producer.Trim();
        }
        if (summary.Origin != null)
        {
            summary.Origin = summary.Origin.Trim();
        }
        if (summary.Elevation != null)
        {
            summary.Elevation = summary.Elevation.Trim();
        }
        if (summary.Process != null)
        {
            summary.Process = summary.Process.Trim();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_summarizer is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _disposed = true;
    }
}
