using System.Collections.Generic;
using System.Linq;

namespace DesktopHub.Core.Models;

/// <summary>
/// Metadata for a single widget — the single source of truth for all widget properties
/// used across Settings, WidgetLauncher, transparency, and navigation.
/// When adding a new widget, register it here and the rest of the system picks it up automatically.
/// </summary>
public class WidgetRegistryEntry
{
    /// <summary>Canonical widget ID (must match WidgetIds constants).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable name shown in UI.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Emoji/icon prefix for settings nav and launcher.</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>Short description shown in widget launcher toggle list.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Whether a transparency slider should be auto-generated in Appearance settings.</summary>
    public bool HasTransparencySlider { get; init; }

    /// <summary>Whether an enable/disable toggle should appear in the Widget Launcher settings tab.</summary>
    public bool HasLauncherToggle { get; init; }

    /// <summary>Whether this widget gets its own settings navigation tab.</summary>
    public bool HasSettingsTab { get; init; }

    /// <summary>Optional override for the settings tab label (defaults to DisplayName if null).</summary>
    public string? SettingsTabLabel { get; init; }

    /// <summary>Default transparency value (0.0 = fully transparent, 1.0 = fully opaque). Used when no saved value exists.</summary>
    public double DefaultTransparency { get; init; } = 0.78;

    /// <summary>Sort order for transparency sliders, launcher toggles, and nav items. Lower = earlier.</summary>
    public int SortOrder { get; init; }

    /// <summary>Resolved label for settings tab (uses SettingsTabLabel if set, otherwise DisplayName).</summary>
    public string ResolvedSettingsTabLabel => SettingsTabLabel ?? DisplayName;
}

/// <summary>
/// Central registry of all widgets in the application.
/// This is the SINGLE SOURCE OF TRUTH — transparency sliders, launcher toggles,
/// settings tabs, and hotkey groups all derive from this list.
/// </summary>
public static class WidgetRegistry
{
    public static readonly IReadOnlyList<WidgetRegistryEntry> All = new[]
    {
        // --- Core UI (no launcher toggle) ---
        new WidgetRegistryEntry
        {
            Id = WidgetIds.SearchOverlay,
            DisplayName = "Search Overlay",
            Icon = "🔍",
            Description = "Main search overlay window",
            HasTransparencySlider = true,
            HasLauncherToggle = false,
            HasSettingsTab = false,
            SortOrder = 10,
        },
        new WidgetRegistryEntry
        {
            Id = WidgetIds.WidgetLauncher,
            DisplayName = "Widget Launcher",
            Icon = "🧩",
            Description = "Floating widget launcher bar",
            HasTransparencySlider = true,
            HasLauncherToggle = false,
            HasSettingsTab = true,
            SettingsTabLabel = "Widget Launcher",
            SortOrder = 20,
        },

        // --- Widgets (shown in launcher) ---
        new WidgetRegistryEntry
        {
            Id = WidgetIds.Timer,
            DisplayName = "Timer",
            Icon = "⏱",
            Description = "Countdown and stopwatch timer",
            HasTransparencySlider = true,
            HasLauncherToggle = true,
            HasSettingsTab = false,
            SortOrder = 30,
        },
        new WidgetRegistryEntry
        {
            Id = WidgetIds.QuickTasks,
            DisplayName = "Quick Tasks",
            Icon = "☑",
            Description = "Task list and to-do manager",
            HasTransparencySlider = true,
            HasLauncherToggle = true,
            HasSettingsTab = true,
            SortOrder = 40,
        },
        new WidgetRegistryEntry
        {
            Id = WidgetIds.DocQuickOpen,
            DisplayName = "Doc Quick Open",
            Icon = "📄",
            Description = "Quick access to project documents",
            HasTransparencySlider = true,
            HasLauncherToggle = true,
            HasSettingsTab = true,
            SortOrder = 50,
        },
        new WidgetRegistryEntry
        {
            Id = WidgetIds.FrequentProjects,
            DisplayName = "Frequent Projects",
            Icon = "⭐",
            Description = "Shows your most frequently launched projects",
            HasTransparencySlider = false,
            HasLauncherToggle = true,
            HasSettingsTab = true,
            SortOrder = 60,
        },
        new WidgetRegistryEntry
        {
            Id = WidgetIds.QuickLaunch,
            DisplayName = "Quick Launch",
            Icon = "🚀",
            Description = "Customizable folder and file launcher",
            HasTransparencySlider = false,
            HasLauncherToggle = true,
            HasSettingsTab = true,
            SortOrder = 70,
        },
        new WidgetRegistryEntry
        {
            Id = WidgetIds.SmartProjectSearch,
            DisplayName = "Smart Project Search",
            Icon = "🔎",
            Description = "Fast semantic, regex, and extension-aware project file search",
            HasTransparencySlider = true,
            HasLauncherToggle = true,
            HasSettingsTab = true,
            SettingsTabLabel = "Smart Search",
            SortOrder = 80,
        },
        new WidgetRegistryEntry
        {
            Id = WidgetIds.CheatSheet,
            DisplayName = "Cheat Sheets",
            Icon = "📋",
            Description = "Engineering cheat sheets and code references",
            HasTransparencySlider = true,
            HasLauncherToggle = true,
            HasSettingsTab = false,
            SortOrder = 90,
        },
    };

    /// <summary>Get a registry entry by widget ID.</summary>
    public static WidgetRegistryEntry? Get(string id) =>
        All.FirstOrDefault(e => e.Id == id);

    /// <summary>All entries that should have a transparency slider in Appearance settings.</summary>
    public static IEnumerable<WidgetRegistryEntry> WithTransparencySlider =>
        All.Where(e => e.HasTransparencySlider).OrderBy(e => e.SortOrder);

    /// <summary>All entries that should appear as toggles in the Widget Launcher settings tab.</summary>
    public static IEnumerable<WidgetRegistryEntry> WithLauncherToggle =>
        All.Where(e => e.HasLauncherToggle).OrderBy(e => e.SortOrder);

    /// <summary>All entries that should have their own settings navigation tab.</summary>
    public static IEnumerable<WidgetRegistryEntry> WithSettingsTab =>
        All.Where(e => e.HasSettingsTab).OrderBy(e => e.SortOrder);
}
