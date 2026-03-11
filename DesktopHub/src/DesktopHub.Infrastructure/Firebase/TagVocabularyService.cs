using System.Collections.Concurrent;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Logging;

namespace DesktopHub.Infrastructure.Firebase;

/// <summary>
/// Provides dropdown/autocomplete suggestions for tag fields.
/// Purely local — derives suggestions from TagFieldRegistry defaults +
/// values observed in the local project_tags cache. No Firebase access.
/// </summary>
public class TagVocabularyService : ITagVocabularyService
{
    // fieldKey → sorted list of known values
    private readonly ConcurrentDictionary<string, SortedSet<string>> _vocabulary = new(StringComparer.OrdinalIgnoreCase);

    public TagVocabularyService()
    {
        // Seed from TagFieldRegistry defaults
        foreach (var field in TagFieldRegistry.Fields)
        {
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var val in field.SuggestedValues)
                set.Add(val);
            _vocabulary[field.Key] = set;
        }
        InfraLogger.Log($"TagVocabularyService: Seeded vocabulary with {_vocabulary.Count} fields from defaults");
    }

    public List<string> GetValues(string fieldKey)
    {
        if (_vocabulary.TryGetValue(fieldKey, out var set))
            return set.ToList();
        return new List<string>();
    }

    public void RefreshFromCache(IReadOnlyDictionary<string, ProjectTags> cachedTags)
    {
        if (cachedTags.Count == 0) return;

        var added = 0;
        foreach (var (_, tags) in cachedTags)
        {
            added += MergeValue("voltage", tags.Voltage);
            added += MergeValue("phase", tags.Phase);
            added += MergeValue("amperage_service", tags.AmperageService);
            added += MergeValue("amperage_generator", tags.AmperageGenerator);
            added += MergeValue("generator_brand", tags.GeneratorBrand);
            added += MergeValue("generator_load_kw", tags.GeneratorLoadKw);
            added += MergeValue("hvac_type", tags.HvacType);
            added += MergeValue("hvac_brand", tags.HvacBrand);
            added += MergeValue("hvac_tonnage", tags.HvacTonnage);
            added += MergeValue("hvac_load_kw", tags.HvacLoadKw);
            added += MergeValue("square_footage", tags.SquareFootage);
            added += MergeValue("build_type", tags.BuildType);
            added += MergeValue("location_city", tags.LocationCity);
            added += MergeValue("location_state", tags.LocationState);
            added += MergeValue("location_municipality", tags.LocationMunicipality);
            added += MergeValue("location_address", tags.LocationAddress);
            added += MergeValue("stamping_engineer", tags.StampingEngineer);

            foreach (var eng in tags.Engineers)
                added += MergeValue("engineers", eng);
            foreach (var cr in tags.CodeReferences)
                added += MergeValue("code_refs", cr);
        }

        if (added > 0)
            InfraLogger.Log($"TagVocabularyService: Refreshed from cache, added {added} new suggestion values");
    }

    /// <summary>
    /// Merge a single value into the vocabulary for a field key.
    /// Returns 1 if newly added, 0 if already existed or empty.
    /// </summary>
    private int MergeValue(string fieldKey, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var trimmed = value.Trim();
        if (trimmed.Length > 200) return 0;

        var set = _vocabulary.GetOrAdd(fieldKey, _ => new SortedSet<string>(StringComparer.OrdinalIgnoreCase));
        return set.Add(trimmed) ? 1 : 0;
    }
}
