namespace DesktopHub.Core.Models;

/// <summary>
/// Canonical string IDs for each widget, used in hotkey group configuration.
/// </summary>
public static class WidgetIds
{
    public const string SearchOverlay      = "SearchOverlay";
    public const string WidgetLauncher     = "WidgetLauncher";
    public const string Timer              = "Timer";
    public const string QuickTasks         = "QuickTasks";
    public const string DocQuickOpen       = "DocQuickOpen";
    public const string FrequentProjects   = "FrequentProjects";
    public const string QuickLaunch        = "QuickLaunch";
    public const string SmartProjectSearch = "SmartProjectSearch";
    public const string CheatSheet          = "CheatSheet";

    public static readonly IReadOnlyList<string> All = new[]
    {
        SearchOverlay, WidgetLauncher, Timer, QuickTasks,
        DocQuickOpen, FrequentProjects, QuickLaunch, SmartProjectSearch,
        CheatSheet
    };

    public static string DisplayName(string id) => id switch
    {
        SearchOverlay      => "Search Overlay",
        WidgetLauncher     => "Widget Launcher",
        Timer              => "Timer",
        QuickTasks         => "Quick Tasks",
        DocQuickOpen       => "Doc Quick Open",
        FrequentProjects   => "Frequent Projects",
        QuickLaunch        => "Quick Launch",
        SmartProjectSearch => "Smart Project Search",
        CheatSheet         => "Cheat Sheets",
        _                  => id
    };
}
