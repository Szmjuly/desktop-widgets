using System.Collections.Generic;
using System.Windows.Input;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Shared hotkey formatting utility used by SearchOverlay, SettingsWindow, and App.
/// </summary>
public static class HotkeyFormatter
{
    public static string FormatHotkey(int modifiers, int key)
    {
        var parts = new List<string>();

        if ((modifiers & (int)GlobalHotkey.MOD_CONTROL) != 0)
            parts.Add("Ctrl");

        if ((modifiers & (int)GlobalHotkey.MOD_ALT) != 0)
            parts.Add("Alt");

        if ((modifiers & (int)GlobalHotkey.MOD_SHIFT) != 0)
            parts.Add("Shift");

        if ((modifiers & (int)GlobalHotkey.MOD_WIN) != 0)
            parts.Add("Win");

        var keyLabel = KeyInterop.KeyFromVirtualKey(key);
        var keyText = keyLabel != Key.None ? keyLabel.ToString() : $"0x{key:X}";
        parts.Add(keyText);

        return string.Join("+", parts);
    }
}
