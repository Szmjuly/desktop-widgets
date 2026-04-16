using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Best-effort registry mapping well-known global shortcuts to apps that commonly register them.
/// Windows does NOT expose which app owns a registered hotkey, so this is a heuristic:
/// look up the combo in the table, then check if that app's process is actually running.
/// If both match we can be reasonably confident it's the culprit.
/// </summary>
public static class KnownHotkeyRegistry
{
    public record KnownApp(string DisplayName, string ProcessName);

    private static readonly List<(int Modifiers, int Key, KnownApp[] Candidates)> _table = BuildTable();

    private static List<(int, int, KnownApp[])> BuildTable()
    {
        int CTRL = (int)GlobalHotkey.MOD_CONTROL;
        int ALT  = (int)GlobalHotkey.MOD_ALT;
        int SHIFT = (int)GlobalHotkey.MOD_SHIFT;
        int WIN  = (int)GlobalHotkey.MOD_WIN;
        int VK(Key k) => KeyInterop.VirtualKeyFromKey(k);

        return new List<(int, int, KnownApp[])>
        {
            (CTRL | ALT, VK(Key.Space), new[]
            {
                new KnownApp("Claude Desktop", "Claude"),
            }),
            (ALT, VK(Key.Space), new[]
            {
                new KnownApp("PowerToys Run", "PowerToys.PowerLauncher"),
                new KnownApp("Flow Launcher", "Flow.Launcher"),
                new KnownApp("Wox", "Wox"),
            }),
            (WIN, VK(Key.C), new[]
            {
                new KnownApp("Copilot", "Copilot"),
            }),
            (WIN, VK(Key.G), new[]
            {
                new KnownApp("Xbox Game Bar", "GameBar"),
            }),
            (CTRL | SHIFT, VK(Key.OemTilde), new[]
            {
                new KnownApp("Terminal", "WindowsTerminal"),
            }),
        };
    }

    /// <summary>
    /// Looks up the combo. If a known app is registered for it AND that process is running
    /// with an activatable main window, returns the match. Otherwise null.
    /// </summary>
    public static ConflictMatch? FindRunningConflict(int modifiers, int key)
    {
        var entry = _table.FirstOrDefault(e => e.Modifiers == modifiers && e.Key == key);
        if (entry.Candidates == null) return null;

        foreach (var candidate in entry.Candidates)
        {
            Process[] procs;
            try { procs = Process.GetProcessesByName(candidate.ProcessName); }
            catch { continue; }

            var match = procs.FirstOrDefault(p =>
            {
                try { return p.MainWindowHandle != IntPtr.Zero; }
                catch { return false; }
            });
            if (match != null)
            {
                return new ConflictMatch(candidate.DisplayName, match);
            }
        }
        return null;
    }

    public record ConflictMatch(string DisplayName, Process Process);

    // Win32
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    public static bool BringToForeground(Process process)
    {
        try
        {
            var handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero) return false;
            ShowWindow(handle, SW_RESTORE);
            return SetForegroundWindow(handle);
        }
        catch
        {
            return false;
        }
    }
}
