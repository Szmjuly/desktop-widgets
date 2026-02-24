namespace DesktopHub.Core.Models;

/// <summary>
/// A named group of widgets that share a single global hotkey.
/// </summary>
public class HotkeyGroup
{
    public int Modifiers { get; set; }
    public int Key { get; set; }

    /// <summary>
    /// Widget IDs (from <see cref="WidgetIds"/>) included in this group.
    /// Exclusive assignment: each widget belongs to at most one group.
    /// </summary>
    public List<string> Widgets { get; set; } = new();
}
