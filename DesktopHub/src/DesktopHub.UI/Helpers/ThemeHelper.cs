using System.Windows;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Brushes = System.Windows.Media.Brushes;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Utility for resolving theme brushes and colors from code-behind.
/// Avoids hardcoded hex strings in .cs files.
/// </summary>
public static class ThemeHelper
{
    // ── Brushes (cached per call — resolves from current app resources) ──

    public static SolidColorBrush TextPrimary => Brush("TextPrimaryBrush");
    public static SolidColorBrush TextSecondary => Brush("TextSecondaryBrush");
    public static SolidColorBrush TextTertiary => Brush("TextTertiaryBrush");
    public static SolidColorBrush TextOnAccent => Brush("TextOnAccentBrush");
    public static SolidColorBrush Accent => Brush("AccentBrush");
    public static SolidColorBrush AccentHover => Brush("AccentHoverBrush");
    public static SolidColorBrush AccentLight => Brush("AccentLightBrush");
    public static SolidColorBrush Surface => Brush("SurfaceBrush");
    public static SolidColorBrush SurfaceSolid => Brush("SurfaceSolidBrush");
    public static SolidColorBrush Card => Brush("CardBrush");
    public static SolidColorBrush CardBorder => Brush("CardBorderBrush");
    public static SolidColorBrush Border => Brush("BorderBrush");
    public static SolidColorBrush BorderSubtle => Brush("BorderSubtleBrush");
    public static SolidColorBrush Hover => Brush("HoverBrush");
    public static SolidColorBrush HoverMedium => Brush("HoverMediumBrush");
    public static SolidColorBrush HoverStrong => Brush("HoverStrongBrush");
    public static SolidColorBrush Selected => Brush("SelectedBrush");
    public static SolidColorBrush InputBackground => Brush("InputBackgroundBrush");
    public static SolidColorBrush WindowBackground => Brush("WindowBackgroundBrush");
    public static SolidColorBrush Gold => Brush("GoldBrush");
    public static SolidColorBrush GoldDark => Brush("GoldDarkBrush");
    public static SolidColorBrush GoldBackground => Brush("GoldBackgroundBrush");
    public static SolidColorBrush Orange => Brush("OrangeBrush");
    public static SolidColorBrush OrangeBackground => Brush("OrangeBackgroundBrush");
    public static SolidColorBrush Red => Brush("RedBrush");
    public static SolidColorBrush RedBackground => Brush("RedBackgroundBrush");
    public static SolidColorBrush Green => Brush("GreenBrush");
    public static SolidColorBrush GreenBackground => Brush("GreenBackgroundBrush");
    public static SolidColorBrush Blue => Brush("BlueBrush");
    public static SolidColorBrush BlueBackground => Brush("BlueBackgroundBrush");
    public static SolidColorBrush BlueText => Brush("BlueTextBrush");
    public static SolidColorBrush Purple => Brush("PurpleBrush");
    public static SolidColorBrush PurpleBackground => Brush("PurpleBackgroundBrush");
    public static SolidColorBrush SubtleOverlay => Brush("SubtleOverlayBrush");
    public static SolidColorBrush FaintOverlay => Brush("FaintOverlayBrush");
    public static SolidColorBrush ScrollbarThumb => Brush("ScrollbarThumbBrush");
    public static SolidColorBrush ToggleOff => Brush("ToggleOffBrush");
    public static SolidColorBrush ToggleOn => Brush("ToggleOnBrush");

    // ── Colors ──

    public static Color TextPrimaryColor => GetColor("TextPrimaryColor");
    public static Color TextSecondaryColor => GetColor("TextSecondaryColor");
    public static Color TextTertiaryColor => GetColor("TextTertiaryColor");
    public static Color AccentColor => GetColor("AccentColor");
    public static Color HoverColor => GetColor("HoverColor");
    public static Color HoverMediumColor => GetColor("HoverMediumColor");
    public static Color SelectedColor => GetColor("SelectedColor");
    public static Color BorderColor => GetColor("BorderColor");
    public static Color GreenColor => GetColor("GreenColor");
    public static Color BlueColor => GetColor("BlueColor");
    public static Color OrangeColor => GetColor("OrangeColor");
    public static Color RedColor => GetColor("RedColor");
    public static Color PurpleColor => GetColor("PurpleColor");
    public static Color GoldColor => GetColor("GoldColor");

    // ── Helpers ──

    /// <summary>
    /// Create a SolidColorBrush from a Color with optional alpha override.
    /// </summary>
    public static SolidColorBrush BrushFrom(Color color, byte? alpha = null)
    {
        var c = alpha.HasValue ? Color.FromArgb(alpha.Value, color.R, color.G, color.B) : color;
        var brush = new SolidColorBrush(c);
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush Brush(string key)
    {
        if (System.Windows.Application.Current?.TryFindResource(key) is SolidColorBrush b)
            return b;
        return Brushes.Transparent as SolidColorBrush ?? new SolidColorBrush(Colors.Transparent);
    }

    public static Color GetColor(string key)
    {
        if (System.Windows.Application.Current?.TryFindResource(key) is Color c)
            return c;
        return Colors.Transparent;
    }
}
