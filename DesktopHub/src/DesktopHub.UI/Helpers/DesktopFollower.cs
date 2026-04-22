using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Monitors virtual desktop switches and moves tracked windows to the active desktop.
/// Uses the PUBLIC IVirtualDesktopManager API (IsWindowOnCurrentVirtualDesktop + MoveWindowToDesktop).
///
/// Detection strategy -- EVENT-DRIVEN with safety-net poll:
///   1. Install SetWinEventHook(EVENT_SYSTEM_FOREGROUND) on Start(). When the
///      foreground window changes (Alt+Tab, click, Win+Ctrl+Arrow for virtual
///      desktop switch, etc.) we get a callback on the UI thread and run the
///      "move tracked windows to current desktop" logic immediately. This
///      replaces the old 500ms EnumWindows polling loop that was heavily
///      flagged by EDR as a keylogger/spyware pattern.
///   2. A slow safety-net DispatcherTimer (every 5s) handles the rare edge
///      cases where no foreground-change event fires -- notably switching to
///      a completely empty virtual desktop with no visible windows.
///   3. Probe-window fallback for detecting the current desktop ID when no
///      non-owned window is available to query.
/// </summary>
public class DesktopFollower : IDisposable
{
    // Feature gate for the event-driven path. In the unlikely event of a
    // regression vs. the old 500ms polling, flip this to false and the class
    // reverts to the timer-only behavior we shipped previously.
    private const bool UseEventBasedMonitoring = true;

    private readonly DispatcherTimer _timer;
    private readonly List<WeakReference<Window>> _trackedWindows = new();
    private bool _isEnabled = false;
    private bool _disposed = false;

    // Cooldown: after moving windows, skip detection for this duration
    private DateTime _cooldownUntil = DateTime.MinValue;
    private const int CooldownMs = 2000;

    // Track the last desktop we moved to, to avoid redundant moves
    private Guid _lastMovedToDesktopId = Guid.Empty;

    // ─────────────────────── Win32 interop ───────────────────────

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // SetWinEventHook — event-driven "foreground window changed" notifications.
    // Replaces the polling loop; fires only when the foreground actually changes,
    // which is also the exact event we need to detect virtual desktop switches.
    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    private IntPtr _winEventHook = IntPtr.Zero;
    private WinEventDelegate? _winEventDelegate; // keep strong ref so GC doesn't collect

    // Debounce: foreground-change events can cluster rapidly (Task View flicker,
    // etc.). Collapse bursts within this window to a single check.
    private DateTime _lastEventHandledUtc = DateTime.MinValue;
    private const int EventDebounceMs = 150;

    public DesktopFollower()
    {
        // Safety-net timer. When event-based monitoring is on, this is a slow
        // backstop (5s); when it's off, we fall back to the legacy 500ms
        // polling cadence.
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(UseEventBasedMonitoring ? 5000 : 500)
        };
        _timer.Tick += OnTimerTick;
    }

    public void Start()
    {
        if (_disposed) return;

        if (!VirtualDesktopManager.IsAvailable)
        {
            DebugLogger.Log("DesktopFollower: Virtual desktop API not available, cannot follow desktops");
            return;
        }

        _isEnabled = true;
        _cooldownUntil = DateTime.UtcNow.AddMilliseconds(CooldownMs); // Initial cooldown to let windows settle

        if (UseEventBasedMonitoring)
        {
            InstallWinEventHook();
        }

        _timer.Start();
        DebugLogger.Log($"DesktopFollower: Started (mode={(UseEventBasedMonitoring ? "event+safety-net-poll" : "poll-only")}, safety-poll={_timer.Interval.TotalMilliseconds}ms)");
    }

    public void Stop()
    {
        _isEnabled = false;
        _timer.Stop();
        RemoveWinEventHook();
        DebugLogger.Log("DesktopFollower: Stopped monitoring desktop switches");
    }

    public void TrackWindow(Window window)
    {
        if (window == null) return;

        CleanupDeadReferences();

        foreach (var weakRef in _trackedWindows)
        {
            if (weakRef.TryGetTarget(out var existing) && existing == window)
                return;
        }

        _trackedWindows.Add(new WeakReference<Window>(window));
        DebugLogger.Log($"DesktopFollower: Now tracking {window.GetType().Name} (total: {_trackedWindows.Count})");
    }

    public void UntrackWindow(Window window)
    {
        if (window == null) return;

        _trackedWindows.RemoveAll(wr =>
        {
            if (wr.TryGetTarget(out var w))
                return w == window;
            return true;
        });

        DebugLogger.Log($"DesktopFollower: Untracked {window.GetType().Name} (remaining: {_trackedWindows.Count})");
    }

    // ─────────────────────── event hook ───────────────────────

    private void InstallWinEventHook()
    {
        if (_winEventHook != IntPtr.Zero) return;

        // Strong ref to the delegate is required -- Windows stores the raw
        // function pointer and will happily crash us if the managed delegate
        // gets collected.
        _winEventDelegate = OnWinEventForeground;
        _winEventHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventDelegate,
            0, 0, WINEVENT_OUTOFCONTEXT);

        if (_winEventHook == IntPtr.Zero)
        {
            DebugLogger.Log("DesktopFollower: SetWinEventHook returned NULL -- falling back to polling only");
        }
        else
        {
            DebugLogger.Log("DesktopFollower: WinEvent hook installed (EVENT_SYSTEM_FOREGROUND)");
        }
    }

    private void RemoveWinEventHook()
    {
        if (_winEventHook != IntPtr.Zero)
        {
            UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
        _winEventDelegate = null;
    }

    private void OnWinEventForeground(IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (!_isEnabled || _disposed) return;

        // Cheap debounce without locking: foreground events can fire in bursts
        // (Task View flicker, splash screens). We only care about "settled"
        // foreground changes.
        var now = DateTime.UtcNow;
        if ((now - _lastEventHandledUtc).TotalMilliseconds < EventDebounceMs) return;
        _lastEventHandledUtc = now;

        // We're already on the Dispatcher thread when WINEVENT_OUTOFCONTEXT
        // hooks fire in-process (they route to the thread that installed them
        // in a dedicated message pump). To be safe, defer to the Dispatcher
        // so the rest of the code runs on the UI thread.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            RunMoveIfNeeded();
        }
        else
        {
            dispatcher.BeginInvoke(new Action(RunMoveIfNeeded), DispatcherPriority.Background);
        }
    }

    // ─────────────────────── check-and-move logic ───────────────────────

    private void OnTimerTick(object? sender, EventArgs e) => RunMoveIfNeeded();

    /// <summary>
    /// Core detection + move logic. Triggered by either the WinEvent hook
    /// (fast path) or the safety-net timer (slow path, catches edge cases
    /// the hook might miss, e.g. switching to an empty virtual desktop).
    /// </summary>
    private void RunMoveIfNeeded()
    {
        if (!_isEnabled || _disposed) return;

        // Respect cooldown
        if (DateTime.UtcNow < _cooldownUntil) return;

        try
        {
            CleanupDeadReferences();
            if (_trackedWindows.Count == 0) return;

            // Check if any visible tracked window is NOT on the current desktop
            var windowsToMove = new List<(Window window, IntPtr hwnd)>();

            foreach (var weakRef in _trackedWindows)
            {
                if (!weakRef.TryGetTarget(out var window)) continue;
                if (window.Visibility != Visibility.Visible) continue;

                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) continue;

                if (!VirtualDesktopManager.IsWindowOnCurrentDesktop(hwnd))
                {
                    windowsToMove.Add((window, hwnd));
                }
            }

            if (windowsToMove.Count == 0) return;

            // Get the current desktop ID from the foreground window
            var currentDesktopId = GetCurrentDesktopId();

            if (currentDesktopId == Guid.Empty)
            {
                DebugLogger.Log("DesktopFollower: Could not determine current desktop ID, skipping");
                return;
            }

            // Check if this is actually a new desktop (not just re-detecting the same state)
            if (currentDesktopId == _lastMovedToDesktopId)
            {
                // We already tried moving to this desktop - apply cooldown to avoid loop
                _cooldownUntil = DateTime.UtcNow.AddMilliseconds(CooldownMs);
                return;
            }

            DebugLogger.Log($"DesktopFollower: Desktop switch detected! Moving {windowsToMove.Count} window(s) to {currentDesktopId:B}");

            int successCount = 0;
            foreach (var (window, hwnd) in windowsToMove)
            {
                try
                {
                    var success = VirtualDesktopManager.MoveWindowToDesktop(hwnd, currentDesktopId);
                    if (success)
                    {
                        successCount++;
                        DebugLogger.Log($"DesktopFollower: Moved {window.GetType().Name} successfully");
                    }
                    else
                    {
                        DebugLogger.Log($"DesktopFollower: Failed to move {window.GetType().Name} (MoveWindowToDesktop returned false)");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"DesktopFollower: Error moving {window.GetType().Name}: {ex.Message}");
                }
            }

            _lastMovedToDesktopId = currentDesktopId;
            _cooldownUntil = DateTime.UtcNow.AddMilliseconds(CooldownMs);
            DebugLogger.Log($"DesktopFollower: Move complete ({successCount}/{windowsToMove.Count} succeeded), cooldown {CooldownMs}ms");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"DesktopFollower: RunMoveIfNeeded error: {ex.Message}");
            _cooldownUntil = DateTime.UtcNow.AddMilliseconds(CooldownMs);
        }
    }

    /// <summary>
    /// Gets the current virtual desktop ID by finding a window on the active desktop.
    /// The foreground window is always on the current desktop.
    /// </summary>
    private Guid GetCurrentDesktopId()
    {
        try
        {
            // Primary: use the foreground window
            var fgWindow = GetForegroundWindow();
            if (fgWindow != IntPtr.Zero)
            {
                // Make sure it's not one of our own tracked windows
                bool isOurWindow = false;
                foreach (var weakRef in _trackedWindows)
                {
                    if (weakRef.TryGetTarget(out var window))
                    {
                        var ourHwnd = new WindowInteropHelper(window).Handle;
                        if (ourHwnd == fgWindow)
                        {
                            isOurWindow = true;
                            break;
                        }
                    }
                }

                if (!isOurWindow)
                {
                    var id = VirtualDesktopManager.GetWindowDesktopId(fgWindow);
                    if (id != Guid.Empty)
                        return id;
                }
            }

            // Fallback: enumerate visible top-level windows to find one with a valid desktop ID
            Guid foundId = Guid.Empty;
            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd)) return true; // continue

                // Skip our own windows
                foreach (var weakRef in _trackedWindows)
                {
                    if (weakRef.TryGetTarget(out var window))
                    {
                        var ourHwnd = new WindowInteropHelper(window).Handle;
                        if (ourHwnd == hwnd) return true; // continue
                    }
                }

                // Check if this window is on the current desktop
                if (VirtualDesktopManager.IsWindowOnCurrentDesktop(hwnd))
                {
                    var id = VirtualDesktopManager.GetWindowDesktopId(hwnd);
                    if (id != Guid.Empty)
                    {
                        foundId = id;
                        return false; // stop enumeration
                    }
                }

                return true; // continue
            }, IntPtr.Zero);

            if (foundId != Guid.Empty)
                return foundId;

            // Last resort: create a temporary probe window to detect the current desktop.
            // New windows are always created on the active virtual desktop.
            return GetDesktopIdViaProbeWindow();
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"DesktopFollower: GetCurrentDesktopId failed: {ex.Message}");
            return Guid.Empty;
        }
    }

    /// <summary>
    /// Creates a temporary invisible window to detect the current virtual desktop ID.
    /// New windows are always created on the active virtual desktop, so this works
    /// even on empty desktops where no other app windows exist to query.
    /// </summary>
    private Guid GetDesktopIdViaProbeWindow()
    {
        try
        {
            var probeWindow = new Window
            {
                Width = 1,
                Height = 1,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Left = -32000,
                Top = -32000,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Opacity = 0
            };
            probeWindow.Show();
            try
            {
                var probeHwnd = new WindowInteropHelper(probeWindow).Handle;
                if (probeHwnd != IntPtr.Zero)
                {
                    var id = VirtualDesktopManager.GetWindowDesktopId(probeHwnd);
                    if (id != Guid.Empty)
                    {
                        DebugLogger.Log($"DesktopFollower: Detected desktop via probe window: {id:B}");
                        return id;
                    }
                }
            }
            finally
            {
                probeWindow.Close();
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"DesktopFollower: Probe window fallback failed: {ex.Message}");
        }

        return Guid.Empty;
    }

    private void CleanupDeadReferences()
    {
        _trackedWindows.RemoveAll(wr => !wr.TryGetTarget(out _));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        RemoveWinEventHook();
        _trackedWindows.Clear();
    }
}
