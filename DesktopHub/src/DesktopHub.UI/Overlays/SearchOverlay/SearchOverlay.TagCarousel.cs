using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DesktopHub.Core.Models;
using DesktopHub.UI.Services;

namespace DesktopHub.UI;

/// <summary>
/// Tag carousel: horizontal scrolling chips below search bar for quick tag-based filtering.
/// Populates from cached tags, auto-refreshes, and injects tag:value queries on click.
/// </summary>
public partial class SearchOverlay
{
    /// <summary>
    /// ViewModel for a single tag carousel chip.
    /// </summary>
    private class TagCarouselChipViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string DisplayKey { get; set; } = string.Empty;

        public override string ToString() => $"{Key}:{Value}";
    }

    /// <summary>
    /// Refresh the tag carousel with recent/popular tag values from the cache.
    /// </summary>
    private void RefreshTagCarousel()
    {
        if (_tagService == null) return;

        var mode = _settings.GetTagDisplayMode();
        if (mode != "carousel")
        {
            TagCarouselContainer.Visibility = Visibility.Collapsed;
            return;
        }

        var maxChips = _settings.GetTagCarouselMaxChips();
        var allTags = _tagService.GetAllCachedTags();

        if (allTags.Count == 0)
        {
            TagCarouselContainer.Visibility = Visibility.Collapsed;
            return;
        }

        // Collect all non-empty tag field values across all projects, count frequency
        var tagValueCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var tagKeyForValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, tags) in allTags)
        {
            CollectTagValues(tags, tagValueCounts, tagKeyForValue);
        }

        // Sort by frequency descending, take top N
        var chips = tagValueCounts
            .OrderByDescending(kvp => kvp.Value)
            .Take(maxChips)
            .Select(kvp =>
            {
                var compositeKey = kvp.Key;
                tagKeyForValue.TryGetValue(compositeKey, out var canonicalKey);
                var def = TagFieldRegistry.GetByKey(canonicalKey ?? "");
                var displayKey = def?.DisplayName ?? canonicalKey ?? compositeKey;

                // Extract just the value part from the composite key
                var colonIdx = compositeKey.IndexOf('|');
                var valueStr = colonIdx >= 0 ? compositeKey[(colonIdx + 1)..] : compositeKey;

                return new TagCarouselChipViewModel
                {
                    Key = displayKey,
                    Value = valueStr,
                    DisplayKey = canonicalKey ?? ""
                };
            })
            .ToList();

        if (chips.Count > 0)
        {
            TagCarouselList.ItemsSource = chips;
            TagCarouselContainer.Visibility = Visibility.Visible;
        }
        else
        {
            TagCarouselContainer.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Collect all non-empty tag values from a ProjectTags instance into frequency maps.
    /// Uses "key|value" as composite key to track unique tag field+value pairs.
    /// </summary>
    private static void CollectTagValues(ProjectTags tags,
        Dictionary<string, int> valueCounts,
        Dictionary<string, string> keyForValue)
    {
        void Add(string canonicalKey, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var composite = $"{canonicalKey}|{value}";
            valueCounts.TryGetValue(composite, out var count);
            valueCounts[composite] = count + 1;
            keyForValue.TryAdd(composite, canonicalKey);
        }

        Add("voltage", tags.Voltage);
        Add("phase", tags.Phase);
        Add("amperage_service", tags.AmperageService);
        Add("amperage_generator", tags.AmperageGenerator);
        Add("generator_brand", tags.GeneratorBrand);
        Add("generator_load_kw", tags.GeneratorLoadKw);
        Add("hvac_type", tags.HvacType);
        Add("hvac_brand", tags.HvacBrand);
        Add("hvac_tonnage", tags.HvacTonnage);
        Add("hvac_load_kw", tags.HvacLoadKw);
        Add("square_footage", tags.SquareFootage);
        Add("build_type", tags.BuildType);
        Add("location_city", tags.LocationCity);
        Add("location_state", tags.LocationState);
        Add("location_municipality", tags.LocationMunicipality);
        Add("stamping_engineer", tags.StampingEngineer);

        foreach (var (key, value) in tags.Custom)
            Add(key, value);
    }

    /// <summary>
    /// Handle click on a tag carousel chip — inject the tag:value query into the search bar.
    /// </summary>
    private void TagCarouselChip_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TagCarouselChipViewModel chip)
        {
            var queryKey = chip.DisplayKey;
            if (string.IsNullOrEmpty(queryKey))
                queryKey = chip.Key.ToLowerInvariant().Replace(" ", "_");

            var tagQuery = $"{queryKey}:{chip.Value}";

            // If search box already has text, append with semicolon separator
            var currentText = SearchBox.Text.Trim();
            if (!string.IsNullOrEmpty(currentText) && !currentText.EndsWith(";"))
            {
                SearchBox.Text = $"{currentText}; {tagQuery}";
            }
            else
            {
                SearchBox.Text = tagQuery;
            }

            SearchBox.CaretIndex = SearchBox.Text.Length;

            _lastQuerySource = QuerySources.TagCarousel;

            // Track telemetry
            TelemetryAccessor.TrackTag(
                TelemetryEventType.TagCarouselClicked,
                tagKey: chip.DisplayKey,
                tagValue: chip.Value,
                source: "carousel");

            // Tag carousel clicks are NOT added to search history — the carousel
            // already serves as the tag "history" and adding them would cause
            // duplicate chips (tag carousel + history pills) for the same queries.

            e.Handled = true;
        }
    }
}
